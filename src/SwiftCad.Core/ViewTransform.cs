namespace SwiftCad.Core;

/// <summary>
/// Camera 2D: screen = (world.X * Scale + OffsetX, -world.Y * Scale + OffsetY).
/// Đơn vị màn hình do bên gọi quyết định (pixel), lớp này không phụ thuộc UI.
/// </summary>
public sealed class ViewTransform
{
    public double Scale { get; private set; } = 1;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }

    public const double MinScale = 1e-9;
    public const double MaxScale = 1e9;

    public (double X, double Y) WorldToScreen(Point2 w) => (w.X * Scale + OffsetX, -w.Y * Scale + OffsetY);
    public Point2 ScreenToWorld(double sx, double sy) => new((sx - OffsetX) / Scale, -(sy - OffsetY) / Scale);

    public void Fit(Bounds2 bounds, double width, double height, double padding)
    {
        if (bounds.IsEmpty || width <= 0 || height <= 0) return;
        var w = Math.Max(bounds.Width, 1e-9);
        var h = Math.Max(bounds.Height, 1e-9);
        var availW = Math.Max(width - 2 * padding, 1);
        var availH = Math.Max(height - 2 * padding, 1);
        Scale = Math.Clamp(Math.Min(availW / w, availH / h), MinScale, MaxScale);
        var c = bounds.Center;
        OffsetX = width / 2 - c.X * Scale;
        OffsetY = height / 2 + c.Y * Scale;
    }

    public void Pan(double dx, double dy) { OffsetX += dx; OffsetY += dy; }

    /// <summary>Zoom quanh một điểm màn hình: điểm bản vẽ dưới ngón tay giữ nguyên vị trí.</summary>
    public void ZoomAt(double sx, double sy, double factor)
    {
        if (!(factor > 0) || double.IsInfinity(factor)) return;
        var before = ScreenToWorld(sx, sy);
        Scale = Math.Clamp(Scale * factor, MinScale, MaxScale);
        OffsetX = sx - before.X * Scale;
        OffsetY = sy + before.Y * Scale;
    }

    /// <summary>Khi kích thước khung thay đổi (xoay máy, mở bảng công cụ): giữ nguyên tâm nhìn và tỷ lệ.</summary>
    public void Resize(double oldW, double oldH, double newW, double newH)
    {
        if (oldW <= 0 || oldH <= 0) return;
        var c = ScreenToWorld(oldW / 2, oldH / 2);
        OffsetX = newW / 2 - c.X * Scale;
        OffsetY = newH / 2 + c.Y * Scale;
    }

    public void CenterOn(Point2 world, double width, double height)
    {
        OffsetX = width / 2 - world.X * Scale;
        OffsetY = height / 2 + world.Y * Scale;
    }
}
