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
        var ctx = new Ctx(entities, skipped, approx, Xf.Identity, null, null, 0);
        foreach (var e in doc.Entities) AddSafe(e, ctx);

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

    /// <summary>
    /// Ngữ cảnh chuyển đổi. Xf: phép biến đổi từ tọa độ cục bộ (block) ra bản vẽ. InheritLayer: layer của INSERT, dùng cho
    /// đối tượng trên layer "0" trong block. BlockColor: màu của INSERT, dùng cho đối tượng màu ByBlock.
    /// </summary>
    private sealed record Ctx(List<SwiftCad.Core.Entity> Output, Dictionary<string, int> Skipped, Dictionary<string, int> Approx,
        Xf Xf, string? InheritLayer, uint? BlockColor, int Depth);

    /// <summary>Một đối tượng hỏng chỉ bị bỏ qua và được đếm, không làm hỏng cả bản vẽ.</summary>
    private static void AddSafe(ACadSharp.Entities.Entity e, Ctx ctx)
    {
        try { Add(e, ctx); }
        catch (Exception)
        {
            Count(ctx.Skipped, e.GetType().Name + " dữ liệu lỗi");
        }
    }

    private static string LayerOf(ACadSharp.Entities.Entity e, Ctx ctx)
    {
        var name = e.Layer?.Name ?? "0";
        // Trong block, đối tượng trên layer "0" theo layer của INSERT.
        return name == "0" && ctx.InheritLayer is not null ? ctx.InheritLayer : name;
    }

    /// <summary>Màu riêng: ByLayer = null (theo layer); ByBlock = màu của INSERT; còn lại = màu của đối tượng.</summary>
    private static uint? ColorOf(ACadSharp.Entities.Entity e, Ctx ctx)
    {
        var c = e.Color;
        if (c.IsByLayer) return null;
        if (c.IsByBlock) return ctx.BlockColor;
        return Argb(c);
    }

    private static uint Argb(ACadSharp.Color c)
    {
        var (r, g, b) = (c.R, c.G, c.B);
        // Màu 7 (trắng/đen) và màu quá tối: hiển thị sáng trên nền tối.
        if (r + g + b < 120) (r, g, b) = ((byte)230, (byte)237, (byte)243);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    /// <summary>Hệ tọa độ đối tượng (OCS): pháp tuyến hướng xuống (0,0,-1) nghĩa là trục X bị lật.</summary>
    private static Xf Ocs(XYZ normal, Ctx ctx) => normal.Z < 0 ? ctx.Xf.Then(Xf.FlipX) : ctx.Xf;

    private static void Add(ACadSharp.Entities.Entity e, Ctx ctx)
    {
        // Đối tượng bị đánh dấu ẩn (ví dụ các trạng thái không dùng của block động) thì AutoCAD không vẽ.
        if (e.IsInvisible) return;
        var layer = LayerOf(e, ctx);
        var color = ColorOf(e, ctx);
        switch (e)
        {
            case Line l:
                ctx.Output.Add(new LineEntity(layer, ctx.Xf.Apply(P(l.StartPoint)), ctx.Xf.Apply(P(l.EndPoint))) { Color = color });
                break;
            case Arc a: // Arc kế thừa Circle nên phải xét trước.
                EmitArc(P(a.Center), a.Radius, a.StartAngle, a.EndAngle, Ocs(a.Normal, ctx), layer, color, ctx);
                break;
            case Circle c:
                EmitCircle(P(c.Center), c.Radius, Ocs(c.Normal, ctx), layer, color, ctx);
                break;
            case Ellipse el:
                // ELLIPSE lưu trong hệ tọa độ thế giới của block; xấp xỉ 72 điểm rồi biến đổi.
                EmitPoly(el.PolygonalVertexes(72).Select(v => new Point2(v.X, v.Y)).ToList(), el.IsFullEllipse, ctx.Xf, layer, color, ctx);
                break;
            case LwPolyline pl:
                var lw = PolylinePoints(pl.Vertices.Select(v => (new Point2(v.Location.X, v.Location.Y), v.Bulge)).ToList(), pl.IsClosed);
                if (lw is not null) EmitPoly(lw, pl.IsClosed, Ocs(pl.Normal, ctx), layer, color, ctx);
                break;
            case Polyline2D p2:
                var old = PolylinePoints(p2.Vertices.Select(v => (new Point2(v.Location.X, v.Location.Y), v.Bulge)).ToList(), p2.IsClosed);
                if (old is not null) EmitPoly(old, p2.IsClosed, Ocs(p2.Normal, ctx), layer, color, ctx);
                break;
            case ACadSharp.Entities.Hatch ht:
                AddHatch(ht, layer, color, ctx);
                break;
            case ACadSharp.Entities.Spline sp:
                AddSpline(sp, layer, color, ctx);
                break;
            case ACadSharp.Entities.Solid so:
                // Thứ tự đỉnh SOLID theo AutoCAD: 1, 2, 4, 3 tạo thành đường bao.
                var sx = Ocs(so.Normal, ctx);
                ctx.Output.Add(new FillEntity(layer, new[] { so.FirstCorner, so.SecondCorner, so.FourthCorner, so.ThirdCorner }.Select(v => sx.Apply(P(v))).ToList(), false) { Color = color });
                break;
            case CadWipeoutBase:
                // WIPEOUT chỉ che nét phía dưới bằng màu nền. Bản này chưa dựng đúng vị trí và thứ tự vẽ của vùng che,
                // nên không vẽ (vẽ sai sẽ che mất nét thật) và báo trong cảnh báo.
                Count(ctx.Skipped, "Wipeout");
                break;
            case Dimension dm:
                // Hình của kích thước nằm trong block ẩn (*D...), đã ở tọa độ của không gian chứa nó.
                if (dm.Block is null || ctx.Depth >= MaxInsertDepth) { Count(ctx.Skipped, e.GetType().Name); break; }
                var dctx = ctx with { InheritLayer = layer, BlockColor = color, Depth = ctx.Depth + 1 };
                foreach (var part in dm.Block.Entities) AddSafe(part, dctx);
                break;
            case Leader ld:
                if (ld.Vertices.Count >= 2) EmitPoly(ld.Vertices.Select(P).ToList(), false, ctx.Xf, layer, color, ctx);
                break;
            case AttributeDefinition:
                // Khuôn thuộc tính: AutoCAD hiển thị giá trị ở Insert.Attributes, không hiển thị khuôn trong block.
                if (ctx.Depth == 0) AddText((ACadSharp.Entities.TextEntity)e, layer, color, ctx);
                break;
            case ACadSharp.Entities.TextEntity t:
                AddText(t, layer, color, ctx);
                break;
            case MText m:
                AddMText(m, layer, color, ctx);
                break;
            case Insert ins:
                AddInsert(ins, layer, color, ctx);
                break;
            default:
                Count(ctx.Skipped, e.GetType().Name);
                break;
        }
    }

    /// <summary>
    /// INSERT: tự tính phép biến đổi (OCS · tịnh tiến · xoay · tỷ lệ · trừ điểm gốc block) và chuyển đổi đệ quy nội dung block
    /// trong tọa độ cục bộ. Không dùng Insert.Explode() của ACadSharp vì hàm đó đặt sai HATCH, góc chữ và điểm căn chữ.
    /// </summary>
    private static void AddInsert(Insert ins, string layer, uint? color, Ctx ctx)
    {
        if (ctx.Depth >= MaxInsertDepth || ins.Block is null) { Count(ctx.Skipped, "Insert"); return; }
        if (ins.Block.Flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRef)) { Count(ctx.Skipped, "XRef"); return; }
        var bp = ins.Block.BlockEntity?.BasePoint ?? XYZ.Zero;
        var local = Xf.Translate(ins.InsertPoint.X, ins.InsertPoint.Y)
            .Then(Xf.Rotate(ins.Rotation))
            .Then(Xf.Scale(ins.XScale, ins.YScale))
            .Then(Xf.Translate(-bp.X, -bp.Y));
        var child = ctx with { Xf = Ocs(ins.Normal, ctx).Then(local), InheritLayer = layer, BlockColor = color, Depth = ctx.Depth + 1 };
        foreach (var part in ins.Block.Entities) AddSafe(part, child);
        // Giá trị thuộc tính block (số hiệu, mã thiết bị…) đã ở tọa độ của không gian chứa INSERT.
        foreach (var att in ins.Attributes)
            if (!att.Flags.HasFlag(AttributeFlags.Hidden))
                AddText(att, LayerOf(att, ctx with { InheritLayer = layer }), ColorOf(att, ctx with { BlockColor = color }), ctx);
    }

    private static void EmitPoly(List<Point2> local, bool closed, Xf xf, string layer, uint? color, Ctx ctx)
    {
        if (local.Count < 2) return;
        ctx.Output.Add(new PolylineEntity(layer, local.Select(xf.Apply).ToList(), closed) { Color = color });
    }

    private static void EmitCircle(Point2 center, double radius, Xf xf, string layer, uint? color, Ctx ctx)
    {
        if (xf.IsSimilarity)
        {
            ctx.Output.Add(new CircleEntity(layer, xf.Apply(center), radius * xf.ScaleX) { Color = color });
            return;
        }
        // Tỷ lệ không đều: đường tròn thành ellipse, xấp xỉ 72 điểm.
        var pts = Enumerable.Range(0, 72).Select(i => { var t = i * Math.PI / 36; return new Point2(center.X + radius * Math.Cos(t), center.Y + radius * Math.Sin(t)); }).ToList();
        EmitPoly(pts, true, xf, layer, color, ctx);
    }

    /// <summary>Cung ngược chiều kim đồng hồ từ start đến end (radian, trong hệ cục bộ).</summary>
    private static void EmitArc(Point2 center, double radius, double startRad, double endRad, Xf xf, string layer, uint? color, Ctx ctx)
    {
        var sweep = (endRad - startRad) % (2 * Math.PI);
        if (sweep <= 0) sweep += 2 * Math.PI;
        if (xf.IsSimilarity)
        {
            // Lật trục đảo chiều cung: cung ngược chiều kim đồng hồ sau khi lật đi từ điểm cuối cũ tới điểm đầu cũ.
            var s = xf.DirectionDeg(Deg(startRad)); var e = xf.DirectionDeg(Deg(startRad + sweep));
            var (a0, a1) = xf.Mirrored ? (e, s) : (s, e);
            ctx.Output.Add(new ArcEntity(layer, xf.Apply(center), radius * xf.ScaleX, a0, a1) { Color = color });
            return;
        }
        var n = Math.Max(2, (int)Math.Ceiling(sweep / (Math.PI / 36)));
        var pts = Enumerable.Range(0, n + 1).Select(i => { var t = startRad + sweep * i / n; return new Point2(center.X + radius * Math.Cos(t), center.Y + radius * Math.Sin(t)); }).ToList();
        EmitPoly(pts, false, xf, layer, color, ctx);
    }

    /// <summary>Đưa chữ từ hệ cục bộ ra bản vẽ: điểm neo, hướng chữ và chiều cao theo phép biến đổi.</summary>
    private static void EmitText(SwiftCad.Core.TextEntity t, Xf xf, uint? color, Ctx ctx)
    {
        var rot = xf.DirectionDeg(t.RotationDeg);
        var up = xf.DirectionDeg(t.RotationDeg + 90);
        // Chiều cao theo độ dài vectơ "lên" của chữ sau biến đổi; bề rộng theo vectơ "ngang".
        var r = t.RotationDeg * Math.PI / 180;
        var hx = xf.A * -Math.Sin(r) + xf.C * Math.Cos(r); var hy = xf.B * -Math.Sin(r) + xf.D * Math.Cos(r);
        var wx = xf.A * Math.Cos(r) + xf.C * Math.Sin(r); var wy = xf.B * Math.Cos(r) + xf.D * Math.Sin(r);
        var hs = Math.Sqrt(hx * hx + hy * hy); var ws = Math.Sqrt(wx * wx + wy * wy);
        _ = up;
        ctx.Output.Add(t with
        {
            Position = xf.Apply(t.Position), Height = t.Height * hs, RotationDeg = rot,
            WidthFactor = hs > 1e-12 ? t.WidthFactor * ws / hs : t.WidthFactor, Color = color,
        });
    }

    /// <summary>
    /// TEXT: giữ góc xoay và căn lề. Căn khác trái-chân chữ thì neo tại AlignmentPoint (mã nhóm 11) như AutoCAD.
    /// Aligned/Fit (co giãn giữa hai điểm) hiển thị gần đúng: trái-chân chữ, hướng theo hai điểm, được đếm để cảnh báo.
    /// </summary>
    private static void AddText(ACadSharp.Entities.TextEntity t, string layer, uint? color, Ctx ctx)
    {
        if (string.IsNullOrWhiteSpace(t.Value)) return;
        var xf = Ocs(t.Normal, ctx);
        var text = DecodeUnicode(t.Value);
        var ha = t.HorizontalAlignment; var va = t.VerticalAlignment;
        var rot = Deg(t.Rotation);
        if (ha is TextHorizontalAlignment.Aligned or TextHorizontalAlignment.Fit)
        {
            var d = new Point2(t.AlignmentPoint.X - t.InsertPoint.X, t.AlignmentPoint.Y - t.InsertPoint.Y);
            if (Math.Abs(d.X) + Math.Abs(d.Y) > 1e-9) rot = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
            Count(ctx.Approx, "TEXT Aligned/Fit");
            EmitText(new SwiftCad.Core.TextEntity(layer, P(t.InsertPoint), t.Height, text, rot, WidthFactor: t.WidthFactor), xf, color, ctx);
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
        EmitText(new SwiftCad.Core.TextEntity(layer, P(anchor), t.Height, text, rot, h, v, t.WidthFactor), xf, color, ctx);
    }

    /// <summary>
    /// MTEXT: tách dòng, giữ góc xoay và điểm gắn (9 vị trí). Khoảng cách dòng = 5/3 × cao chữ × hệ số giãn dòng
    /// (mặc định AutoCAD). Dòng dài hơn khung (AutoCAD tự xuống dòng) được đếm là hiển thị gần đúng.
    /// </summary>
    private static void AddMText(MText m, string layer, uint? color, Ctx ctx)
    {
        // Mã định dạng bật/tắt gạch chân, gạch trên, gạch ngang (\\L \\l \\O \\o \\K \\k) không phải nội dung chữ.
        if (m.Value.Contains('\\')) m.Value = MTextToggles.Replace(m.Value, "");
        var lines = m.GetPlainTextLines().Select(DecodeUnicode).ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return;
        var xf = Ocs(m.Normal, ctx);
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
            EmitText(te, xf, color, ctx);
        }
    }

    /// <summary>
    /// HATCH: mỗi biên thành một vòng điểm trong hệ cục bộ rồi biến đổi. HATCH đặc tô màu; HATCH mẫu chỉ vẽ đường bao
    /// (đếm là gần đúng). Cung trong biên tự lấy mẫu theo tâm, bán kính, góc và chiều quay.
    /// </summary>
    private static void AddHatch(ACadSharp.Entities.Hatch ht, string layer, uint? color, Ctx ctx)
    {
        var xf = Ocs(ht.Normal, ctx);
        var loops = new List<List<Point2>>();
        foreach (var bp in ht.Paths)
        {
            var pts = new List<Point2>();
            foreach (var edge in bp.Edges)
            {
                switch (edge)
                {
                    case ACadSharp.Entities.Hatch.BoundaryPath.Line ln:
                        AppendPoint(pts, P(ln.Start)); AppendPoint(pts, P(ln.End));
                        break;
                    case ACadSharp.Entities.Hatch.BoundaryPath.Polyline pl:
                        var poly = PolylinePoints(pl.Vertices.Select(v => (new Point2(v.X, v.Y), v.Z)).ToList(), pl.IsClosed);
                        if (poly is not null) foreach (var p in poly) AppendPoint(pts, p);
                        break;
                    case ACadSharp.Entities.Hatch.BoundaryPath.Arc arc:
                        // Cung thuận chiều kim đồng hồ: góc lưu theo chiều ngược lại (quy ước DXF).
                        var (a0, a1) = arc.CounterClockWise ? (arc.StartAngle, arc.EndAngle) : (-arc.StartAngle, -arc.EndAngle);
                        var sw = arc.CounterClockWise ? PositiveSweep(a1 - a0) : -PositiveSweep(a0 - a1);
                        var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sw) / (Math.PI / 36)));
                        for (var i = 0; i <= n; i++)
                        {
                            var t = a0 + sw * i / n;
                            AppendPoint(pts, new Point2(arc.Center.X + arc.Radius * Math.Cos(t), arc.Center.Y + arc.Radius * Math.Sin(t)));
                        }
                        break;
                    case ACadSharp.Entities.Hatch.BoundaryPath.Ellipse el:
                        foreach (var v in el.PolygonalVertexes(64)) AppendPoint(pts, P(v));
                        break;
                    case ACadSharp.Entities.Hatch.BoundaryPath.Spline sp:
                        foreach (var v in sp.PolygonalVertexes(64)) AppendPoint(pts, P(v));
                        break;
                    default:
                        Count(ctx.Skipped, "HATCH edge " + edge.GetType().Name);
                        break;
                }
            }
            // Đóng vòng: bỏ đỉnh cuối nếu trùng đỉnh đầu (biên HATCH thường khép kín bằng đoạn cuối).
            if (pts.Count > 1 && pts[^1].DistanceTo(pts[0]) < 1e-9) pts.RemoveAt(pts.Count - 1);
            if (pts.Count >= 3) loops.Add(pts);
        }
        if (loops.Count == 0) { Count(ctx.Skipped, "Hatch"); return; }
        if (loops.Count > 1) Count(ctx.Approx, "HATCH nhiều vòng (lỗ) tô từng vòng");
        foreach (var loop in loops)
        {
            var world = loop.Select(xf.Apply).ToList();
            if (ht.IsSolid)
                ctx.Output.Add(new FillEntity(layer, world, false) { Color = color });
            else
            {
                Count(ctx.Approx, "HATCH mẫu chỉ vẽ đường bao");
                ctx.Output.Add(new PolylineEntity(layer, world, true) { Color = color });
            }
        }
    }

    private static double PositiveSweep(double d)
    {
        d %= 2 * Math.PI;
        return d <= 0 ? d + 2 * Math.PI : d;
    }

    private static void AppendPoint(List<Point2> pts, Point2 p)
    {
        if (pts.Count > 0 && pts[^1].DistanceTo(p) < 1e-9) return;
        pts.Add(p);
    }

    /// <summary>SPLINE: xấp xỉ bằng polyline; spline chỉ có điểm khớp thì nối các điểm khớp. Cả hai được đếm là gần đúng.</summary>
    private static void AddSpline(ACadSharp.Entities.Spline sp, string layer, uint? color, Ctx ctx)
    {
        if (sp.ControlPoints.Count < 2)
        {
            var fit = sp.FitPoints.Select(f => new Point2(f.X, f.Y)).ToList();
            if (fit.Count < 2) { Count(ctx.Skipped, "Spline không có điểm"); return; }
            Count(ctx.Approx, "SPLINE (điểm khớp) nối đoạn thẳng");
            EmitPoly(fit, false, ctx.Xf, layer, color, ctx);
            return;
        }
        var pts = sp.PolygonalVertexes(64).Select(P).ToList();
        if (pts.Count < 2) { Count(ctx.Skipped, "Spline"); return; }
        Count(ctx.Approx, "SPLINE xấp xỉ bằng đoạn thẳng");
        EmitPoly(pts, sp.IsClosed, ctx.Xf, layer, color, ctx);
    }

    private static void Count(Dictionary<string, int> d, string k) => d[k] = d.GetValueOrDefault(k) + 1;

    /// <summary>Chuyển polyline có bulge thành chuỗi điểm: mỗi cung xấp xỉ bằng đoạn thẳng, tối đa 10° mỗi đoạn.</summary>
    internal static List<Point2>? PolylinePoints(IReadOnlyList<(Point2 P, double Bulge)> v, bool closed)
    {
        if (v.Count < 2) return null;
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
        return pts;
    }

    private static Point2 P(XYZ p) => new(p.X, p.Y);
    private static Point2 P(XY p) => new(p.X, p.Y);
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

    private static readonly Regex MTextToggles = new(@"\\[LlOoKk]", RegexOptions.Compiled);
    private static readonly Regex UnicodeEscape = new(@"\\U\+([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    /// <summary>Tệp DXF R2000 trở về trước lưu ký tự ngoài ASCII dạng \U+XXXX; ACadSharp không tự giải mã.</summary>
    public static string DecodeUnicode(string s) =>
        s.Contains("\\U+", StringComparison.OrdinalIgnoreCase)
            ? UnicodeEscape.Replace(s, m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString())
            : s;
}
