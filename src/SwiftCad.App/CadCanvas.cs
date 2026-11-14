using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SwiftCad.Core;

namespace SwiftCad.App;

public enum CanvasMode { Pan, Measure, Note }

/// <summary>
/// Khung vẽ CAD: vẽ thẳng từ mô hình hình học trung gian bằng SkiaSharp, nét mảnh, không bóng, không hiệu ứng.
/// Một ngón kéo để di chuyển, hai ngón để zoom. Ở chế độ Đo, kéo một ngón để ngắm điểm (tâm ngắm lệch lên trên ngón tay).
/// </summary>
public class CadCanvas : SKCanvasView
{
    public DrawingModel? Model { get; private set; }
    public LayerState Layers { get; } = new();
    public ViewTransform Camera { get; } = new();
    public CanvasMode Mode { get; set; } = CanvasMode.Pan;
    public SKColor CanvasColor { get; set; } = new(0x11, 0x1C, 0x25);

    public IReadOnlyList<TextHit> Hits { get; set; } = [];
    public int CurrentHit { get; set; } = -1;
    public IReadOnlyList<Note> Notes { get; set; } = [];
    /// <summary>Loại ghi chú khi ở chế độ Ghi chú: Chữ (chạm), Mũi tên hoặc Khoanh vùng (kéo một ngón).</summary>
    public NoteKind NoteTool { get; set; } = NoteKind.Text;
    public SnapResult? MeasureA { get; private set; }
    public SnapResult? MeasureB { get; private set; }
    public SnapResult? Aim { get; private set; }
    public string MeasureLabel { get; set; } = "";

    public event Action? MeasureChanged;
    public event Action<SnapResult?>? AimChanged;
    public event Action<Point2>? NoteRequested;
    /// <summary>Vẽ xong mũi tên (điểm đầu → đầu mũi tên) hoặc vùng khoanh (hai góc đối).</summary>
    public event Action<NoteKind, Point2, Point2>? ShapeDrawn;

    private Point2? _dragStart, _dragEnd;
    private bool DrawsShapes => Mode == CanvasMode.Note && NoteTool is NoteKind.Arrow or NoteKind.Cloud;
    private static readonly SKColor AnnotColor = new(0xFF, 0x45, 0x3A);

    private readonly Dictionary<long, SKPoint> _touches = new();
    private SKPoint _downAt;
    private bool _moved;
    private float _w, _h, _density = 1;
    private bool _fitted;

    private const float TapSlopDp = 10;
    private const float AimOffsetDp = 64;
    private const float SnapRadiusDp = 22;

    public CadCanvas()
    {
        EnableTouchEvents = true;
        PaintSurface += OnPaint;
        Touch += OnTouch;
        // Xoay máy hoặc đổi bố cục: buộc vẽ lại theo kích thước mới (lỗi phát hiện ở kiểm tra luồng: khung trống sau khi xoay).
        SizeChanged += (_, _) => InvalidateSurface();
    }

    /// <summary>Chỉ mục lưới để chỉ vẽ đối tượng trong khung nhìn; dựng một lần khi mở bản vẽ.</summary>
    private SpatialGrid? _grid;
    /// <summary>Màu layer dựng một lần, không tạo lại mỗi khung hình.</summary>
    private Dictionary<string, SKColor> _layerColors = new();

    public void SetModel(DrawingModel model)
    {
        Model = model;
        _grid = new SpatialGrid(model.Entities, model.Bounds);
        _layerColors = model.Layers.ToDictionary(l => l.Name, l => new SKColor(l.ColorArgb));
        _fitted = false;
        InvalidateSurface();
    }

    /// <summary>Khung nhìn hiện tại trong tọa độ bản vẽ (kể cả khi chưa có kích thước).</summary>
    private Bounds2 VisibleWorldBounds()
    {
        var a = Camera.ScreenToWorld(0, 0); var b = Camera.ScreenToWorld(_w, _h);
        return new Bounds2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    public void Fit()
    {
        if (Model is null || _w <= 0) return;
        Camera.Fit(Model.Bounds, _w, _h, 24 * _density);
        InvalidateSurface();
    }

    public void ClearMeasure() { MeasureA = MeasureB = Aim = null; MeasureLabel = ""; MeasureChanged?.Invoke(); InvalidateSurface(); }

    /// <summary>Đưa một điểm bản vẽ vào giữa khung; phóng to nếu chữ cao dưới 14 dp trên màn hình.</summary>
    public void FocusOn(Point2 world, double worldHeight = 0)
    {
        if (_w <= 0) return;
        var minPx = 14 * _density;
        if (worldHeight > 0 && worldHeight * Camera.Scale < minPx)
            Camera.ZoomAt(_w / 2, _h / 2, minPx / (worldHeight * Camera.Scale));
        Camera.CenterOn(world, _w, _h);
        InvalidateSurface();
    }

    // ---------- Chạm ----------

    /// <summary>Xóa trạng thái chạm đang dở (sau khi đổi kiểu ghi chú hoặc sau một hộp thoại), để chạm sau không bị kẹt.</summary>
    public void ResetGestures()
    {
        _touches.Clear();
        _dragStart = _dragEnd = null;
        _moved = false;
        SetAim(null);
        InvalidateSurface();
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;
        if (Model is null) return;
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _touches[e.Id] = e.Location;
                if (_touches.Count == 1)
                {
                    _downAt = e.Location; _moved = false;
                    _dragStart = DrawsShapes ? Camera.ScreenToWorld(e.Location.X, e.Location.Y) : null;
                    _dragEnd = null;
                }
                else { _moved = true; SetAim(null); _dragStart = _dragEnd = null; } // hai ngón: di chuyển, thu phóng; bỏ hình đang vẽ
                break;

            case SKTouchAction.Moved:
                if (!_touches.TryGetValue(e.Id, out var prev)) break;
                if (_touches.Count == 1)
                {
                    if (!_moved && Distance(e.Location, _downAt) > TapSlopDp * _density) _moved = true;
                    if (_moved)
                    {
                        if (Mode == CanvasMode.Measure) SetAim(SnapAt(e.Location.X, e.Location.Y - AimOffsetDp * _density));
                        else if (_dragStart is not null) _dragEnd = Camera.ScreenToWorld(e.Location.X, e.Location.Y);
                        else Camera.Pan(e.Location.X - prev.X, e.Location.Y - prev.Y);
                    }
                    _touches[e.Id] = e.Location;
                }
                else if (_touches.Count == 2)
                {
                    var other = _touches.First(t => t.Key != e.Id).Value;
                    var oldMid = Mid(prev, other); var newMid = Mid(e.Location, other);
                    var oldDist = Distance(prev, other); var newDist = Distance(e.Location, other);
                    Camera.Pan(newMid.X - oldMid.X, newMid.Y - oldMid.Y);
                    if (oldDist > 1 && newDist > 1) Camera.ZoomAt(newMid.X, newMid.Y, newDist / oldDist);
                    _touches[e.Id] = e.Location;
                }
                InvalidateSurface();
                break;

            case SKTouchAction.Released:
                var single = _touches.Count == 1 && _touches.ContainsKey(e.Id);
                _touches.Remove(e.Id);
                if (!single) break;
                if (Mode == CanvasMode.Measure)
                {
                    // Chạm nhanh: đặt điểm tại chỗ chạm. Kéo rồi thả: đặt điểm tại tâm ngắm.
                    var point = _moved ? Aim : SnapAt(e.Location.X, e.Location.Y);
                    SetAim(null);
                    if (point is { } p) PlaceMeasurePoint(p);
                }
                else if (_dragStart is { } ds)
                {
                    var de = _dragEnd;
                    _dragStart = _dragEnd = null;
                    if (_moved && de is { } end) ShapeDrawn?.Invoke(NoteTool, ds, end);
                }
                else if (Mode == CanvasMode.Note && !_moved)
                {
                    NoteRequested?.Invoke(Camera.ScreenToWorld(e.Location.X, e.Location.Y));
                }
                InvalidateSurface();
                break;

            case SKTouchAction.Cancelled:
                _touches.Remove(e.Id);
                SetAim(null);
                _dragStart = _dragEnd = null;
                InvalidateSurface();
                break;
        }
    }

    private SnapResult SnapAt(float sx, float sy) =>
        SnapEngine.Find(Model!, Layers, Camera.ScreenToWorld(sx, sy), SnapRadiusDp * _density / Camera.Scale);

    private void SetAim(SnapResult? aim) { Aim = aim; AimChanged?.Invoke(aim); }

    internal void PlaceMeasurePoint(SnapResult p)
    {
        if (MeasureA is null || MeasureB is not null) { MeasureA = p; MeasureB = null; }
        else MeasureB = p;
        MeasureChanged?.Invoke();
    }

    private static float Distance(SKPoint a, SKPoint b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static SKPoint Mid(SKPoint a, SKPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    // ---------- Vẽ ----------

    private void OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(CanvasColor);
        float w = e.Info.Width, h = e.Info.Height;
        _density = Width > 0 ? (float)(w / Width) : 1;

        if (Model is null) { _w = w; _h = h; return; }
        if (w < 2 || h < 2) return; // kích thước tạm thời khi đổi bố cục
        if (!_fitted) { _w = w; _h = h; Camera.Fit(Model.Bounds, w, h, 24 * _density); _fitted = true; }
        else if (w != _w || h != _h)
        {
            // Xoay máy hoặc mở/đóng bảng công cụ: giữ tâm nhìn và tỷ lệ.
            Camera.Resize(_w, _h, w, h); _w = w; _h = h;
            // An toàn: nếu bản vẽ đã ra hẳn ngoài khung nhìn thì đưa về vừa khung, không để khung trống.
            if (!Camera.Intersects(Model.Bounds, w, h)) Camera.Fit(Model.Bounds, w, h, 24 * _density);
        }

        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1f, 1.1f * _density) };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var font = new SKFont(SKTypeface.Default);
        var colors = _layerColors;

        // Chỉ duyệt đối tượng có thể nằm trong khung nhìn; thứ tự tăng dần giữ đúng thứ tự vẽ (WIPEOUT che nét trước đó).
        var visible = _grid?.Query(VisibleWorldBounds()) ?? Enumerable.Range(0, Model.Entities.Count).ToList();
        foreach (var idx in visible)
        {
            var ent = Model.Entities[idx];
            if (!Layers.IsVisible(ent.Layer)) continue;
            // Màu riêng của đối tượng nếu có; không thì theo layer.
            var color = ent.Color is uint own ? new SKColor(own) : colors.TryGetValue(ent.Layer, out var c) ? c : SKColors.White;
            stroke.Color = color;
            switch (ent)
            {
                case FillEntity fe when fe.Points.Count >= 3:
                    using (var pb = new SKPathBuilder())
                    {
                        pb.MoveTo(S(fe.Points[0]));
                        for (var i = 1; i < fe.Points.Count; i++) pb.LineTo(S(fe.Points[i]));
                        pb.Close();
                        fill.Color = fe.UseBackground ? CanvasColor : color;
                        using (var built = pb.Detach()) canvas.DrawPath(built, fill);
                    }
                    break;
                case LineEntity l:
                    canvas.DrawLine(S(l.A), S(l.B), stroke);
                    break;
                case PolylineEntity p when p.Points.Count > 1:
                    using (var path = new SKPathBuilder())
                    {
                        path.MoveTo(S(p.Points[0]));
                        for (var i = 1; i < p.Points.Count; i++) path.LineTo(S(p.Points[i]));
                        if (p.Closed) path.Close();
                        using (var built = path.Detach()) canvas.DrawPath(built, stroke);
                    }
                    break;
                case CircleEntity ci:
                    canvas.DrawCircle(S(ci.Center), (float)(ci.Radius * Camera.Scale), stroke);
                    break;
                case ArcEntity a:
                    var ctr = S(a.Center); var r = (float)(a.Radius * Camera.Scale);
                    using (var path = new SKPathBuilder())
                    {
                        // Trục Y màn hình lật so với bản vẽ nên góc đổi dấu.
                        path.AddArc(new SKRect(ctr.X - r, ctr.Y - r, ctr.X + r, ctr.Y + r), (float)-a.StartDeg, (float)-a.SweepDeg);
                        using (var built = path.Detach()) canvas.DrawPath(built, stroke);
                    }
                    break;
                case TextEntity t:
                    var size = (float)(t.Height * Camera.Scale);
                    if (size < 3f) break;
                    font.Size = size; fill.Color = color;
                    DrawRotatedText(canvas, t, font, fill);
                    break;
            }
        }

        DrawHits(canvas, font, stroke, fill);
        DrawNotes(canvas, font, stroke, fill);
        DrawMeasure(canvas, font, stroke, fill);
    }

    private SKPoint S(Point2 p)
    {
        var (x, y) = Camera.WorldToScreen(p);
        return new SKPoint((float)x, (float)y);
    }

    /// <summary>
    /// Đặt hệ trục của chữ: gốc tại điểm neo, quay theo góc chữ (trục Y màn hình lật nên quay âm),
    /// trả về gốc chân chữ (đầu dòng) trong hệ đó, theo pixel. Bề rộng đo bằng font thật.
    /// </summary>
    private (float X, float Y, float Width) BeginText(SKCanvas canvas, TextEntity t, SKFont font)
    {
        var anchor = S(t.Position);
        canvas.Save();
        canvas.Translate(anchor.X, anchor.Y);
        if (t.RotationDeg != 0) canvas.RotateDegrees((float)-t.RotationDeg);
        if (Math.Abs(t.WidthFactor - 1) > 1e-6) canvas.Scale((float)t.WidthFactor, 1);
        var width = font.MeasureText(t.Text);
        var (dx, dy) = TextEntity.BaselineOffset(width, font.Size, t.HAlign, t.VAlign);
        return ((float)dx, (float)-dy, width);
    }

    private void DrawRotatedText(SKCanvas canvas, TextEntity t, SKFont font, SKPaint fill)
    {
        var (x, y, _) = BeginText(canvas, t, font);
        canvas.DrawText(t.Text, x, y, SKTextAlign.Left, font, fill);
        canvas.Restore();
    }

    private void DrawHits(SKCanvas canvas, SKFont font, SKPaint stroke, SKPaint fill)
    {
        if (Model is null) return;
        for (var i = 0; i < Hits.Count; i++)
        {
            if (Model.Entities[Hits[i].EntityIndex] is not TextEntity t) continue;
            font.Size = Math.Max((float)(t.Height * Camera.Scale), 1f);
            var (x, y, width) = BeginText(canvas, t, font);
            var pad = 4 * _density;
            var rect = new SKRect(x - pad, y - font.Size - pad, x + width + pad, y + font.Size * 0.25f + pad);
            if (i == CurrentHit) { fill.Color = new SKColor(0xFF, 0xD5, 0x4F, 0x55); canvas.DrawRect(rect, fill); }
            stroke.Color = new SKColor(0xFF, 0xD5, 0x4F);
            stroke.StrokeWidth = (i == CurrentHit ? 2.2f : 1.2f) * _density;
            canvas.DrawRect(rect, stroke);
            canvas.Restore();
        }
        stroke.StrokeWidth = Math.Max(1f, 1.1f * _density);
    }

    /// <summary>
    /// Ghi chú vẽ chồng lên bản vẽ, không ghi vào tệp gốc. Nét và chữ có cỡ cố định trên màn hình để luôn đọc được.
    /// Ghim (bản cũ): vòng tròn đánh số. Chữ: chấm neo và nhãn chữ. Mũi tên: nét chỉ tới điểm, chữ ở đầu kia.
    /// Khoanh vùng: khung dạng đám mây, chữ phía trên góc trái.
    /// </summary>
    private void DrawNotes(SKCanvas canvas, SKFont font, SKPaint stroke, SKPaint fill)
    {
        var r = 11 * _density;
        for (var i = 0; i < Notes.Count; i++)
        {
            var n = Notes[i];
            var a = S(new Point2(n.X, n.Y));
            switch (n.Kind)
            {
                case NoteKind.Text:
                    fill.Color = AnnotColor; canvas.DrawCircle(a, 3.5f * _density, fill);
                    TextLabel(canvas, font, stroke, fill, new SKPoint(a.X + 4 * _density, a.Y - 4 * _density), n.Text, alignRight: false);
                    break;
                case NoteKind.Arrow:
                    var b = S(new Point2(n.X2, n.Y2));
                    DrawArrow(canvas, stroke, fill, a, b);
                    if (n.Text.Length > 0)
                        TextLabel(canvas, font, stroke, fill, new SKPoint(b.X >= a.X ? a.X - 4 * _density : a.X + 4 * _density, a.Y + 11 * _density), n.Text, alignRight: b.X >= a.X);
                    break;
                case NoteKind.Cloud:
                    var c = S(new Point2(n.X2, n.Y2));
                    DrawCloud(canvas, stroke, a, c);
                    if (n.Text.Length > 0)
                        TextLabel(canvas, font, stroke, fill, new SKPoint(Math.Min(a.X, c.X), Math.Min(a.Y, c.Y) - 8 * _density), n.Text, alignRight: false);
                    break;
                default:
                    font.Size = 12 * _density;
                    fill.Color = new SKColor(0x08, 0x78, 0xFF); canvas.DrawCircle(a, r, fill);
                    stroke.Color = SKColors.White; canvas.DrawCircle(a, r, stroke);
                    fill.Color = SKColors.White;
                    canvas.DrawText((i + 1).ToString(L.FormatCulture), a.X, a.Y + 4 * _density, SKTextAlign.Center, font, fill);
                    break;
            }
        }
        // Hình đang kéo (chưa lưu).
        if (_dragStart is { } ds && _dragEnd is { } de)
        {
            if (NoteTool == NoteKind.Arrow) DrawArrow(canvas, stroke, fill, S(ds), S(de));
            else DrawCloud(canvas, stroke, S(ds), S(de));
        }
        stroke.StrokeWidth = Math.Max(1f, 1.1f * _density);
    }

    private void DrawArrow(SKCanvas canvas, SKPaint stroke, SKPaint fill, SKPoint from, SKPoint to)
    {
        stroke.Color = AnnotColor; stroke.StrokeWidth = 2.2f * _density;
        var dx = to.X - from.X; var dy = to.Y - from.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;
        var ux = dx / len; var uy = dy / len;
        var head = Math.Min(14 * _density, len * 0.6f);
        var baseX = to.X - ux * head; var baseY = to.Y - uy * head;
        canvas.DrawLine(from.X, from.Y, baseX, baseY, stroke);
        using var pb = new SKPathBuilder();
        pb.MoveTo(to);
        pb.LineTo(baseX - uy * head * 0.45f, baseY + ux * head * 0.45f);
        pb.LineTo(baseX + uy * head * 0.45f, baseY - ux * head * 0.45f);
        pb.Close();
        fill.Color = AnnotColor;
        using var built = pb.Detach();
        canvas.DrawPath(built, fill);
    }

    /// <summary>Khung đám mây (revision cloud) theo hình chữ nhật hai góc đối; mỗi vòng cung khoảng 16 dp.</summary>
    private void DrawCloud(SKCanvas canvas, SKPaint stroke, SKPoint p1, SKPoint p2)
    {
        stroke.Color = AnnotColor; stroke.StrokeWidth = 2f * _density;
        float l = Math.Min(p1.X, p2.X), t = Math.Min(p1.Y, p2.Y), rgt = Math.Max(p1.X, p2.X), btm = Math.Max(p1.Y, p2.Y);
        if (rgt - l < 2 || btm - t < 2) return;
        // Đi theo chiều kim đồng hồ trên màn hình; pháp tuyến ra ngoài của cạnh (dx, dy) là (dy, -dx).
        SKPoint[] corners = [new(l, t), new(rgt, t), new(rgt, btm), new(l, btm)];
        var step = 16 * _density;
        using var pb = new SKPathBuilder();
        pb.MoveTo(corners[0]);
        for (var k = 0; k < 4; k++)
        {
            var from = corners[k]; var to = corners[(k + 1) % 4];
            var ex = to.X - from.X; var ey = to.Y - from.Y;
            var elen = MathF.Sqrt(ex * ex + ey * ey);
            var count = Math.Max(1, (int)MathF.Round(elen / step));
            var nx = ey / elen; var ny = -ex / elen;
            var bulge = elen / count * 0.55f;
            for (var j = 0; j < count; j++)
            {
                var s0 = (float)j / count; var s1 = (float)(j + 1) / count; var sm = (s0 + s1) / 2;
                pb.QuadTo(from.X + ex * sm + nx * bulge, from.Y + ey * sm + ny * bulge, from.X + ex * s1, from.Y + ey * s1);
            }
        }
        pb.Close();
        using var built = pb.Detach();
        canvas.DrawPath(built, stroke);
    }

    /// <summary>Nhãn chữ một dòng trên nền tối viền đỏ; đặt góc dưới trái (hoặc dưới phải) tại điểm cho trước.</summary>
    private void TextLabel(SKCanvas canvas, SKFont font, SKPaint stroke, SKPaint fill, SKPoint at, string text, bool alignRight)
    {
        var label = text.Length > 48 ? text[..47] + "\u2026" : text;
        label = label.Replace('\n', ' ');
        font.Size = 14 * _density;
        var pad = 6 * _density; var h = 24 * _density;
        var w = font.MeasureText(label) + 2 * pad;
        var left = alignRight ? at.X - w : at.X;
        var box = new SKRect(left, at.Y - h, left + w, at.Y);
        fill.Color = new SKColor(0x11, 0x1C, 0x25, 0xE6);
        canvas.DrawRoundRect(box, 6 * _density, 6 * _density, fill);
        stroke.Color = AnnotColor; stroke.StrokeWidth = 1.4f * _density;
        canvas.DrawRoundRect(box, 6 * _density, 6 * _density, stroke);
        fill.Color = SKColors.White;
        canvas.DrawText(label, left + pad, at.Y - 7 * _density, SKTextAlign.Left, font, fill);
    }

    private void DrawMeasure(SKCanvas canvas, SKFont font, SKPaint stroke, SKPaint fill)
    {
        var accent = new SKColor(0xFF, 0x6E, 0x40);
        stroke.Color = accent; stroke.StrokeWidth = 1.6f * _density;
        if (MeasureA is { } a) Marker(canvas, stroke, S(a.Point), a.Kind);
        if (MeasureB is { } b) Marker(canvas, stroke, S(b.Point), b.Kind);
        if (MeasureA is { } a2 && (MeasureB ?? Aim) is { } end)
        {
            var p1 = S(a2.Point); var p2 = S(end.Point);
            canvas.DrawLine(p1, p2, stroke);
            if (MeasureB is not null && MeasureLabel.Length > 0)
            {
                font.Size = 14 * _density;
                var mid = Mid(p1, p2); var tw = font.MeasureText(MeasureLabel); var pad = 6 * _density;
                var box = new SKRect(mid.X - tw / 2 - pad, mid.Y - 26 * _density, mid.X + tw / 2 + pad, mid.Y - 4 * _density);
                fill.Color = new SKColor(0x11, 0x1C, 0x25, 0xE6); canvas.DrawRoundRect(box, 6 * _density, 6 * _density, fill);
                canvas.DrawRoundRect(box, 6 * _density, 6 * _density, stroke);
                fill.Color = SKColors.White;
                canvas.DrawText(MeasureLabel, mid.X, mid.Y - 10 * _density, SKTextAlign.Center, font, fill);
            }
        }
        if (Aim is { } aim)
        {
            // Tâm ngắm: dấu cộng lớn và ký hiệu loại điểm bắt.
            var o = S(aim.Point); var len = 18 * _density;
            stroke.Color = SKColors.White;
            canvas.DrawLine(o.X - len, o.Y, o.X + len, o.Y, stroke);
            canvas.DrawLine(o.X, o.Y - len, o.X, o.Y + len, stroke);
            stroke.Color = accent;
            Marker(canvas, stroke, o, aim.Kind);
        }
        stroke.StrokeWidth = Math.Max(1f, 1.1f * _density);
    }

    /// <summary>Ký hiệu loại điểm: vuông = đầu mút, tam giác = trung điểm, tròn = tâm, chéo = điểm tự do.</summary>
    private void Marker(SKCanvas canvas, SKPaint stroke, SKPoint o, SnapKind kind)
    {
        var s = 7 * _density;
        switch (kind)
        {
            case SnapKind.Endpoint:
                canvas.DrawRect(new SKRect(o.X - s, o.Y - s, o.X + s, o.Y + s), stroke);
                break;
            case SnapKind.Midpoint:
                using (var path = new SKPathBuilder())
                {
                    path.MoveTo(o.X, o.Y - s); path.LineTo(o.X + s, o.Y + s); path.LineTo(o.X - s, o.Y + s); path.Close();
                    using (var built = path.Detach()) canvas.DrawPath(built, stroke);
                }
                break;
            case SnapKind.Center:
                canvas.DrawCircle(o, s, stroke);
                break;
            default:
                canvas.DrawLine(o.X - s, o.Y - s, o.X + s, o.Y + s, stroke);
                canvas.DrawLine(o.X - s, o.Y + s, o.X + s, o.Y - s, stroke);
                break;
        }
    }
}
