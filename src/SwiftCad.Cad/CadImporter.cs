using System.Globalization;
using System.Text.RegularExpressions;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using SwiftCad.Core;

namespace SwiftCad.Cad;

/// <summary>Kết quả đọc tệp: mô hình hình học và những gì chưa hiển thị được, để báo trung thực cho người dùng.</summary>
public sealed class ImportReport
{
    public required DrawingModel Model { get; init; }
    /// <summary>Số đối tượng bỏ qua theo loại (Hatch, Dimension, Spline…): bản đầu chưa vẽ các loại này.</summary>
    public required IReadOnlyDictionary<string, int> Skipped { get; init; }
    public required IReadOnlyList<string> MissingXrefs { get; init; }
    public required IReadOnlyList<string> Fonts { get; init; }
    /// <summary>Đối tượng có hiển thị nhưng gần đúng (TEXT Aligned/Fit, MTEXT tự xuống dòng…), để cảnh báo cụ thể.</summary>
    public required IReadOnlyDictionary<string, int> Approximated { get; init; }
    public int SkippedTotal => Skipped.Values.Sum();
    public int ApproximatedTotal => Approximated.Values.Sum();
}

public sealed class CadImportException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Đọc DWG/DXF bằng ACadSharp và chuyển sang DrawingModel. Bản đầu hỗ trợ: LINE, LWPOLYLINE (có bulge), POLYLINE 2D,
/// CIRCLE, ARC, ELLIPSE, TEXT, MTEXT (chữ thuần), INSERT (mở rộng block lồng nhau). Các loại khác được đếm vào Skipped.
/// </summary>
public static class CadImporter
{
    public const int MaxInsertDepth = 16;

    public static ImportReport Read(Stream stream, string fileName)
    {
        CadDocument doc;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            doc = ext switch
            {
                ".dwg" => DwgReader.Read(stream),
                ".dxf" => DxfReader.Read(stream),
                _ => throw new CadImportException("unsupported-extension"),
            };
        }
        catch (CadImportException) { throw; }
        catch (Exception ex)
        {
            throw new CadImportException("read-failed", ex);
        }
        return Convert(doc, fileName);
    }

    public static ImportReport Convert(CadDocument doc, string fileName)
    {
        var entities = new List<SwiftCad.Core.Entity>();
        var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        var approx = new Dictionary<string, int>(StringComparer.Ordinal);
        var ctx = new Ctx(entities, skipped, approx);
        foreach (var e in doc.Entities) Add(e, ctx, 0);

        var layers = doc.Layers
            .Select(l => new DrawingLayer(l.Name, LayerColor(l), l.IsOn && !l.Flags.HasFlag(LayerFlags.Frozen)))
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var xrefs = doc.BlockRecords
            .Where(b => b.Flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRef) || b.Flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRefOverlay))
            .Select(b => string.IsNullOrEmpty(b.BlockEntity?.XRefPath) ? b.Name : b.BlockEntity.XRefPath)
            .Distinct().ToList();
        var fonts = doc.TextStyles.Select(t => t.Filename).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var model = new DrawingModel
        {
            Name = fileName,
            Unit = MapUnit(doc.Header.InsUnits),
            Layers = layers,
            Entities = entities,
            IsDemo = false,
        };
        return new ImportReport { Model = model, Skipped = skipped, MissingXrefs = xrefs, Fonts = fonts, Approximated = approx };
    }

    private sealed record Ctx(List<SwiftCad.Core.Entity> Output, Dictionary<string, int> Skipped, Dictionary<string, int> Approx);

    private static void Add(ACadSharp.Entities.Entity e, Ctx ctx, int depth)
    {
        var output = ctx.Output; var skipped = ctx.Skipped;
        var layer = e.Layer?.Name ?? "0";
        switch (e)
        {
            case Line l:
                output.Add(new LineEntity(layer, P(l.StartPoint), P(l.EndPoint)));
                break;
            case Arc a: // Arc kế thừa Circle nên phải xét trước.
                output.Add(new ArcEntity(layer, P(a.Center), a.Radius, Deg(a.StartAngle), Deg(a.EndAngle)));
                break;
            case Circle c:
                output.Add(new CircleEntity(layer, P(c.Center), c.Radius));
                break;
            case Ellipse el:
                // Insert.Explode() của ACadSharp đổi mọi CIRCLE trong block thành ELLIPSE, nên phải xử lý loại này.
                var major = new Point2(el.MajorAxisEndPoint.X, el.MajorAxisEndPoint.Y);
                var radius = Math.Sqrt(major.X * major.X + major.Y * major.Y);
                if (el.IsFullEllipse && Math.Abs(el.RadiusRatio - 1) < 1e-9)
                    output.Add(new CircleEntity(layer, P(el.Center), radius));
                else
                    output.Add(new PolylineEntity(layer, el.PolygonalVertexes(72).Select(v => new Point2(v.X, v.Y)).ToList(), el.IsFullEllipse));
                break;
            case LwPolyline pl:
                AddPolyline(layer, pl.Vertices.Select(v => (new Point2(v.Location.X, v.Location.Y), v.Bulge)).ToList(), pl.IsClosed, output);
                break;
            case AttributeDefinition:
                // Định nghĩa thuộc tính chỉ là khuôn trong block; AutoCAD hiển thị giá trị ở Insert.Attributes.
                break;
            case ACadSharp.Entities.TextEntity t:
                AddText(t, layer, ctx);
                break;
            case MText m:
                AddMText(m, layer, ctx);
                break;
            case Insert ins:
                if (depth >= MaxInsertDepth || ins.Block is null) { Count(skipped, "Insert"); break; }
                if (ins.Block.Flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRef)) { Count(skipped, "XRef"); break; }
                List<ACadSharp.Entities.Entity> parts;
                try
                {
                    parts = ins.Explode().ToList();
                    // ACadSharp 3.8.35 không biến đổi AlignmentPoint của TEXT khi Explode; Explode giữ đúng thứ tự
                    // Block.Entities nên tự biến đổi điểm căn theo cùng ma trận.
                    var originals = ins.Block.Entities.ToList();
                    var tr = ins.GetTransform();
                    for (var i = 0; i < parts.Count && i < originals.Count; i++)
                    {
                        if (parts[i] is not ACadSharp.Entities.TextEntity pt || originals[i] is not ACadSharp.Entities.TextEntity ot) continue;
                        pt.AlignmentPoint = tr.ApplyTransform(ot.AlignmentPoint);
                        // Góc chữ sau Explode của ACadSharp 3.8.35 sai dấu với block xoay (kiểm thử: +90° thành -90°);
                        // tính lại từ hướng chân chữ đã biến đổi.
                        var o = tr.ApplyTransform(ot.InsertPoint);
                        var u = tr.ApplyTransform(new XYZ(ot.InsertPoint.X + Math.Cos(ot.Rotation), ot.InsertPoint.Y + Math.Sin(ot.Rotation), ot.InsertPoint.Z));
                        pt.Rotation = Math.Atan2(u.Y - o.Y, u.X - o.X);
                    }
                }
                catch (Exception) { Count(skipped, "Insert"); break; }
                foreach (var part in parts) Add(part, ctx, depth + 1);
                // Giá trị thuộc tính block (số hiệu, mã thiết bị…) đã ở tọa độ bản vẽ.
                foreach (var att in ins.Attributes)
                    if (!att.Flags.HasFlag(AttributeFlags.Hidden)) AddText(att, att.Layer?.Name ?? layer, ctx);
                break;
            default:
                Count(skipped, e.GetType().Name);
                break;
        }
    }

    /// <summary>
    /// TEXT: giữ góc xoay và căn lề. Căn khác trái-chân chữ thì neo tại AlignmentPoint (mã nhóm 11) như AutoCAD.
    /// Aligned/Fit (co giãn giữa hai điểm) hiển thị gần đúng: trái-chân chữ, hướng theo hai điểm, được đếm để cảnh báo.
    /// </summary>
    private static void AddText(ACadSharp.Entities.TextEntity t, string layer, Ctx ctx)
    {
        if (string.IsNullOrWhiteSpace(t.Value)) return;
        var text = DecodeUnicode(t.Value);
        var ha = t.HorizontalAlignment; var va = t.VerticalAlignment;
        var rot = Deg(t.Rotation);
        if (ha is TextHorizontalAlignment.Aligned or TextHorizontalAlignment.Fit)
        {
            var d = new Point2(t.AlignmentPoint.X - t.InsertPoint.X, t.AlignmentPoint.Y - t.InsertPoint.Y);
            if (Math.Abs(d.X) + Math.Abs(d.Y) > 1e-9) rot = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
            Count(ctx.Approx, "TEXT Aligned/Fit");
            ctx.Output.Add(new SwiftCad.Core.TextEntity(layer, P(t.InsertPoint), t.Height, text, rot, WidthFactor: t.WidthFactor));
            return;
        }
        var h = ha switch
        {
            TextHorizontalAlignment.Center or TextHorizontalAlignment.Middle => TextHAlign.Center,
            TextHorizontalAlignment.Right => TextHAlign.Right,
            _ => TextHAlign.Left,
        };
        var v = ha == TextHorizontalAlignment.Middle ? TextVAlign.Middle : va switch
        {
            TextVerticalAlignmentType.Bottom => TextVAlign.Bottom,
            TextVerticalAlignmentType.Middle => TextVAlign.Middle,
            TextVerticalAlignmentType.Top => TextVAlign.Top,
            _ => TextVAlign.Baseline,
        };
        var anchor = h == TextHAlign.Left && v == TextVAlign.Baseline ? t.InsertPoint : t.AlignmentPoint;
        ctx.Output.Add(new SwiftCad.Core.TextEntity(layer, P(anchor), t.Height, text, rot, h, v, t.WidthFactor));
    }

    /// <summary>
    /// MTEXT: tách dòng, giữ góc xoay và điểm gắn (9 vị trí). Khoảng cách dòng = 5/3 × cao chữ × hệ số giãn dòng
    /// (mặc định AutoCAD). Dòng dài hơn khung (AutoCAD tự xuống dòng) được đếm là hiển thị gần đúng.
    /// </summary>
    private static void AddMText(MText m, string layer, Ctx ctx)
    {
        var lines = m.GetPlainTextLines().Select(DecodeUnicode).ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return;
        var hgt = m.Height;
        var pitch = hgt * 5.0 / 3.0 * (m.LineSpacing > 0 ? m.LineSpacing : 1);
        var blockH = hgt + (lines.Count - 1) * pitch;
        var ap = (int)m.AttachmentPoint; // 1..9: hàng Top/Middle/Bottom × cột Left/Center/Right
        var col = (ap - 1) % 3; var row = (ap - 1) / 3;
        var h = col switch { 1 => TextHAlign.Center, 2 => TextHAlign.Right, _ => TextHAlign.Left };
        var top = row switch { 1 => blockH / 2, 2 => blockH, _ => 0 };
        var r = m.Rotation; var c = Math.Cos(r); var s = Math.Sin(r);
        var rot = Deg(r);
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var y = top - hgt - i * pitch;
            var p = new Point2(m.InsertPoint.X - y * s, m.InsertPoint.Y + y * c);
            var te = new SwiftCad.Core.TextEntity(layer, p, hgt, lines[i], rot, h, TextVAlign.Baseline);
            if (m.RectangleWidth > 0 && te.EstimatedWidth > m.RectangleWidth * 1.05) Count(ctx.Approx, "MTEXT wrap");
            ctx.Output.Add(te);
        }
    }

    private static void Count(Dictionary<string, int> d, string k) => d[k] = d.GetValueOrDefault(k) + 1;

    /// <summary>Chuyển polyline có bulge thành chuỗi điểm: mỗi cung xấp xỉ bằng đoạn thẳng, tối đa 10° mỗi đoạn.</summary>
    internal static void AddPolyline(string layer, IReadOnlyList<(Point2 P, double Bulge)> v, bool closed, List<SwiftCad.Core.Entity> output)
    {
        if (v.Count < 2) return;
        var pts = new List<Point2>();
        var segs = closed ? v.Count : v.Count - 1;
        for (var i = 0; i < segs; i++)
        {
            var (a, bulge) = v[i];
            var b = v[(i + 1) % v.Count].P;
            pts.Add(a);
            if (Math.Abs(bulge) < 1e-9) continue;
            var theta = 4 * Math.Atan(bulge);
            var chord = a.DistanceTo(b);
            if (chord < 1e-12) continue;
            var r = chord / (2 * Math.Sin(Math.Abs(theta) / 2));
            var mid = Point2.Mid(a, b);
            var nx = -(b.Y - a.Y) / chord; var ny = (b.X - a.X) / chord;
            var h = r * Math.Cos(theta / 2) * Math.Sign(bulge);
            var c = new Point2(mid.X + nx * h, mid.Y + ny * h);
            var start = Math.Atan2(a.Y - c.Y, a.X - c.X);
            var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(theta) / (Math.PI / 18)));
            for (var k = 1; k < n; k++)
            {
                var ang = start + theta * k / n;
                pts.Add(new Point2(c.X + r * Math.Cos(ang), c.Y + r * Math.Sin(ang)));
            }
        }
        if (!closed) pts.Add(v[^1].P);
        output.Add(new PolylineEntity(layer, pts, closed));
    }

    private static Point2 P(XYZ p) => new(p.X, p.Y);
    private static double Deg(double rad) => rad * 180.0 / Math.PI;

    private static uint LayerColor(Layer l)
    {
        try
        {
            var c = l.Color;
            var (r, g, b) = (c.R, c.G, c.B);
            // Màu 7 (trắng/đen) và màu quá tối: hiển thị sáng trên nền tối.
            if (r + g + b < 120) (r, g, b) = ((byte)230, (byte)237, (byte)243);
            return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
        }
        catch (Exception)
        {
            return 0xFFE6EDF3u;
        }
    }

    public static DrawingUnit MapUnit(UnitsType u) => u switch
    {
        UnitsType.Millimeters => DrawingUnit.Millimeter,
        UnitsType.Centimeters => DrawingUnit.Centimeter,
        UnitsType.Meters => DrawingUnit.Meter,
        UnitsType.Inches => DrawingUnit.Inch,
        UnitsType.Feet => DrawingUnit.Foot,
        _ => DrawingUnit.Unknown,
    };

    private static readonly Regex UnicodeEscape = new(@"\\U\+([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    /// <summary>Tệp DXF R2000 trở về trước lưu ký tự ngoài ASCII dạng \U+XXXX; ACadSharp không tự giải mã.</summary>
    public static string DecodeUnicode(string s) =>
        s.Contains("\\U+", StringComparison.OrdinalIgnoreCase)
            ? UnicodeEscape.Replace(s, m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString())
            : s;
}
