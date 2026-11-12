using SwiftCad.Core;

namespace SwiftCad.Cad;

/// <summary>
/// Phép biến đổi affine 2D: x' = A·x + C·y + Tx, y' = B·x + D·y + Ty.
/// Dùng để tự đặt nội dung block vào bản vẽ (vị trí, xoay, tỷ lệ, lật), không dựa vào hàm tách block của thư viện đọc.
/// </summary>
public readonly record struct Xf(double A, double B, double C, double D, double Tx, double Ty)
{
    public static readonly Xf Identity = new(1, 0, 0, 1, 0, 0);
    /// <summary>Lật trục X: hệ tọa độ đối tượng (OCS) khi pháp tuyến là (0,0,-1).</summary>
    public static readonly Xf FlipX = new(-1, 0, 0, 1, 0, 0);

    public static Xf Translate(double x, double y) => new(1, 0, 0, 1, x, y);
    public static Xf Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
    public static Xf Rotate(double radians)
    {
        var c = Math.Cos(radians); var s = Math.Sin(radians);
        return new Xf(c, s, -s, c, 0, 0);
    }

    public Point2 Apply(Point2 p) => new(A * p.X + C * p.Y + Tx, B * p.X + D * p.Y + Ty);

    /// <summary>this ∘ inner: áp dụng inner trước, rồi this.</summary>
    public Xf Then(Xf inner) => new(
        A * inner.A + C * inner.B, B * inner.A + D * inner.B,
        A * inner.C + C * inner.D, B * inner.C + D * inner.D,
        A * inner.Tx + C * inner.Ty + Tx, B * inner.Tx + D * inner.Ty + Ty);

    public double ScaleX => Math.Sqrt(A * A + B * B);
    public double ScaleY => Math.Sqrt(C * C + D * D);
    public bool Mirrored => A * D - B * C < 0;
    /// <summary>Tỷ lệ đều và hai trục vuông góc: đường tròn vẫn là đường tròn.</summary>
    public bool IsSimilarity => Math.Abs(ScaleX - ScaleY) <= 1e-9 * Math.Max(1, Math.Max(ScaleX, ScaleY)) && Math.Abs(A * C + B * D) <= 1e-9 * Math.Max(1, ScaleX * ScaleY);
    public double DirectionDeg(double localDeg)
    {
        var r = localDeg * Math.PI / 180;
        var x = A * Math.Cos(r) + C * Math.Sin(r); var y = B * Math.Cos(r) + D * Math.Sin(r);
        return Math.Atan2(y, x) * 180 / Math.PI;
    }
}
