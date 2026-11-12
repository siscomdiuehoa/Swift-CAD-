using System.Globalization;

namespace SwiftCad.Core;

public enum DrawingUnit { Unknown = 0, Millimeter, Centimeter, Meter, Inch, Foot }

public static class Units
{
    /// <summary>Số mét trong một đơn vị. Unknown không quy đổi được.</summary>
    public static double MetersPer(DrawingUnit u) => u switch
    {
        DrawingUnit.Millimeter => 0.001,
        DrawingUnit.Centimeter => 0.01,
        DrawingUnit.Meter => 1.0,
        DrawingUnit.Inch => 0.0254,
        DrawingUnit.Foot => 0.3048,
        _ => throw new InvalidOperationException("Đơn vị chưa xác định, không thể quy đổi."),
    };

    public static double Convert(double value, DrawingUnit from, DrawingUnit to)
    {
        if (from == to) return value;
        return value * MetersPer(from) / MetersPer(to);
    }

    /// <summary>Ký hiệu đơn vị theo chuẩn quốc tế, không dịch.</summary>
    public static string Symbol(DrawingUnit u) => u switch
    {
        DrawingUnit.Millimeter => "mm",
        DrawingUnit.Centimeter => "cm",
        DrawingUnit.Meter => "m",
        DrawingUnit.Inch => "in",
        DrawingUnit.Foot => "ft",
        _ => "",
    };

    public static int DefaultDecimals(DrawingUnit u) => u switch
    {
        DrawingUnit.Millimeter => 1,
        DrawingUnit.Centimeter => 2,
        DrawingUnit.Meter => 3,
        DrawingUnit.Inch => 2,
        DrawingUnit.Foot => 3,
        _ => 3,
    };

    /// <summary>Định dạng số đo theo vùng (dấu thập phân, dấu nhóm) kèm ký hiệu đơn vị nếu có.</summary>
    public static string Format(double value, DrawingUnit unit, CultureInfo culture)
    {
        var n = value.ToString("N" + DefaultDecimals(unit), culture);
        var s = Symbol(unit);
        return s.Length == 0 ? n : n + " " + s;
    }
}

/// <summary>Kết quả đo: luôn giữ giá trị gốc theo tọa độ bản vẽ, quy đổi khi hiển thị.</summary>
public readonly record struct Measurement(Point2 A, Point2 B)
{
    public double RawDistance => A.DistanceTo(B);

    /// <summary>Trả null khi đơn vị nguồn chưa xác định và người dùng yêu cầu đơn vị khác.</summary>
    public double? DistanceIn(DrawingUnit source, DrawingUnit display)
    {
        if (display == DrawingUnit.Unknown || display == source) return RawDistance;
        if (source == DrawingUnit.Unknown) return null;
        return Units.Convert(RawDistance, source, display);
    }
}
