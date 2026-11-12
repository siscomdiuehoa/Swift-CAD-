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
    public int SkippedTotal => Skipped.Values.Sum();
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
        foreach (var e in doc.Entities) Add(e, entities, skipped, 0);

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
        return new ImportReport { Model = model, Skipped = skipped, MissingXrefs = xrefs, Fonts = fonts };
    }

    private static void Add(ACadSharp.Entities.Entity e, List<SwiftCad.Core.Entity> output, Dictionary<string, int> skipped, int depth)
    {
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
            case ACadSharp.Entities.TextEntity t:
                if (!string.IsNullOrWhiteSpace(t.Value))
                    output.Add(new SwiftCad.Core.TextEntity(layer, P(t.InsertPoint), t.Height, DecodeUnicode(t.Value)));
                break;
            case MText m:
                var lines = m.GetPlainTextLines();
                for (var i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    // Điểm chèn MTEXT là góc trên trái; dòng i nằm thấp dần.
                    var y = m.InsertPoint.Y - m.Height * (1 + i * 1.6);
                    output.Add(new SwiftCad.Core.TextEntity(layer, new Point2(m.InsertPoint.X, y), m.Height, DecodeUnicode(lines[i])));
                }
                break;
            case Insert ins:
                if (depth >= MaxInsertDepth || ins.Block is null) { Count(skipped, "Insert"); break; }
                if (ins.Block.Flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRef)) { Count(skipped, "XRef"); break; }
                IEnumerable<ACadSharp.Entities.Entity> parts;
                try { parts = ins.Explode().ToList(); }
                catch (Exception) { Count(skipped, "Insert"); break; }
                foreach (var part in parts) Add(part, output, skipped, depth + 1);
                break;
            default:
                Count(skipped, e.GetType().Name);
                break;
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
