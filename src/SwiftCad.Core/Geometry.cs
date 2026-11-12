namespace SwiftCad.Core;

/// <summary>Điểm trong hệ tọa độ bản vẽ (đơn vị của bản vẽ, trục Y hướng lên).</summary>
public readonly record struct Point2(double X, double Y)
{
    public double DistanceTo(Point2 o)
    {
        var dx = o.X - X; var dy = o.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    public static Point2 Mid(Point2 a, Point2 b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
}

public readonly record struct Bounds2(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public Point2 Center => new((MinX + MaxX) / 2, (MinY + MaxY) / 2);
    public bool IsEmpty => !(MaxX >= MinX && MaxY >= MinY);
    public static Bounds2 Empty => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);
    public Bounds2 Include(Point2 p) => new(Math.Min(MinX, p.X), Math.Min(MinY, p.Y), Math.Max(MaxX, p.X), Math.Max(MaxY, p.Y));
}

public abstract record Entity(string Layer)
{
    public abstract Bounds2 ExtendBounds(Bounds2 b);
}

public sealed record LineEntity(string Layer, Point2 A, Point2 B) : Entity(Layer)
{
    public override Bounds2 ExtendBounds(Bounds2 b) => b.Include(A).Include(B);
}

public sealed record PolylineEntity(string Layer, IReadOnlyList<Point2> Points, bool Closed) : Entity(Layer)
{
    public override Bounds2 ExtendBounds(Bounds2 b)
    {
        foreach (var p in Points) b = b.Include(p);
        return b;
    }
}

/// <summary>
/// Vùng tô đa giác. UseBackground = true: vùng che (WIPEOUT) được tô bằng màu nền, che các đối tượng vẽ trước nó.
/// UseBackground = false: tô màu layer (HATCH đặc, SOLID).
/// </summary>
public sealed record FillEntity(string Layer, IReadOnlyList<Point2> Points, bool UseBackground) : Entity(Layer)
{
    public override Bounds2 ExtendBounds(Bounds2 b)
    {
        foreach (var p in Points) b = b.Include(p);
        return b;
    }
}

public sealed record CircleEntity(string Layer, Point2 Center, double Radius) : Entity(Layer)
{
    public override Bounds2 ExtendBounds(Bounds2 b) =>
        b.Include(new(Center.X - Radius, Center.Y - Radius)).Include(new(Center.X + Radius, Center.Y + Radius));
}

/// <summary>Cung tròn ngược chiều kim đồng hồ từ StartDeg đến EndDeg (độ, như quy ước DXF).</summary>
public sealed record ArcEntity(string Layer, Point2 Center, double Radius, double StartDeg, double EndDeg) : Entity(Layer)
{
    public Point2 PointAt(double deg)
    {
        var r = deg * Math.PI / 180.0;
        return new(Center.X + Radius * Math.Cos(r), Center.Y + Radius * Math.Sin(r));
    }
    public double SweepDeg
    {
        get { var s = (EndDeg - StartDeg) % 360.0; return s <= 0 ? s + 360.0 : s; }
    }
    public override Bounds2 ExtendBounds(Bounds2 b) =>
        b.Include(new(Center.X - Radius, Center.Y - Radius)).Include(new(Center.X + Radius, Center.Y + Radius));
}

public enum TextHAlign { Left = 0, Center, Right }
public enum TextVAlign { Baseline = 0, Bottom, Middle, Top }

/// <summary>
/// Chữ một dòng. Position là điểm neo theo HAlign/VAlign (mặc định: trái, chân chữ), Height theo đơn vị bản vẽ,
/// RotationDeg ngược chiều kim đồng hồ quanh điểm neo.
/// </summary>
public sealed record TextEntity(string Layer, Point2 Position, double Height, string Text,
    double RotationDeg = 0, TextHAlign HAlign = TextHAlign.Left, TextVAlign VAlign = TextVAlign.Baseline, double WidthFactor = 1)
    : Entity(Layer)
{
    /// <summary>Bề rộng ước lượng khi chưa đo được bằng font thật.</summary>
    public double EstimatedWidth => Height * 0.6 * Text.Length * WidthFactor;

    /// <summary>
    /// Độ lệch từ điểm neo tới điểm đầu chân chữ, trong hệ trục của chữ (x theo chiều chữ, y lên).
    /// width là bề rộng chữ cùng đơn vị với height.
    /// </summary>
    public static (double Dx, double Dy) BaselineOffset(double width, double height, TextHAlign h, TextVAlign v) =>
        (h switch { TextHAlign.Center => -width / 2, TextHAlign.Right => -width, _ => 0 },
         v switch { TextVAlign.Bottom => height * 0.2, TextVAlign.Middle => -height / 2, TextVAlign.Top => -height, _ => 0 });

    /// <summary>Bốn góc khung chữ trong tọa độ bản vẽ (đã xoay).</summary>
    public Point2[] Corners(double? width = null)
    {
        var w = width ?? EstimatedWidth;
        var (dx, dy) = BaselineOffset(w, Height, HAlign, VAlign);
        var r = RotationDeg * Math.PI / 180; var c = Math.Cos(r); var s = Math.Sin(r);
        Point2 T(double x, double y) => new(Position.X + x * c - y * s, Position.Y + x * s + y * c);
        var y0 = dy - Height * 0.2; var y1 = dy + Height;
        return [T(dx, y0), T(dx + w, y0), T(dx + w, y1), T(dx, y1)];
    }

    public override Bounds2 ExtendBounds(Bounds2 b)
    {
        foreach (var p in Corners()) b = b.Include(p);
        return b;
    }
}

/// <summary>VisibleByDefault = false khi layer đang tắt hoặc đóng băng trong tệp gốc.</summary>
public sealed record DrawingLayer(string Name, uint ColorArgb, bool VisibleByDefault = true);

/// <summary>Mô hình hình học trung gian phục vụ hiển thị, tìm, đo. Độc lập với thư viện đọc CAD và UI.</summary>
public sealed class DrawingModel
{
    public required string Name { get; init; }
    public required DrawingUnit Unit { get; init; }
    public required IReadOnlyList<DrawingLayer> Layers { get; init; }
    public required IReadOnlyList<Entity> Entities { get; init; }
    /// <summary>True khi hình học do ứng dụng tự tạo để minh họa, không đọc từ tệp DWG/DXF.</summary>
    public bool IsDemo { get; init; }

    private Bounds2? _bounds;
    public Bounds2 Bounds
    {
        get
        {
            if (_bounds is { } b) return b;
            var acc = Bounds2.Empty;
            foreach (var e in Entities) acc = e.ExtendBounds(acc);
            _bounds = acc;
            return acc;
        }
    }
}
