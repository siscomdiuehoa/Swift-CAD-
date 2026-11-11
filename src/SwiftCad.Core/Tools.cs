using System.Globalization;
using System.Text;

namespace SwiftCad.Core;

/// <summary>Trạng thái bật/tắt layer của một phiên xem. Không ghi vào tệp gốc.</summary>
public sealed class LayerState
{
    private readonly HashSet<string> _hidden = new(StringComparer.Ordinal);
    public bool IsVisible(string layer) => !_hidden.Contains(layer);
    public void Set(string layer, bool visible) { if (visible) _hidden.Remove(layer); else _hidden.Add(layer); }
    public void ShowAll() => _hidden.Clear();
    public int HiddenCount => _hidden.Count;

    public static IEnumerable<DrawingLayer> Filter(IEnumerable<DrawingLayer> layers, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return layers;
        var q = TextSearch.Fold(query);
        return layers.Where(l => TextSearch.Fold(l.Name).Contains(q, StringComparison.Ordinal));
    }
}

public readonly record struct TextHit(int EntityIndex, Point2 Position, double Height, string Text);

public static class TextSearch
{
    /// <summary>Chuẩn hóa để so khớp: bỏ dấu, không phân biệt hoa thường ("phong" khớp "PHÒNG").</summary>
    public static string Fold(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = char.ToLowerInvariant(ch);
            sb.Append(c == 'đ' ? 'd' : c);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Tìm chữ trong bản vẽ, chỉ trên layer đang hiện. Thứ tự: trên xuống, trái sang phải.</summary>
    public static IReadOnlyList<TextHit> Find(DrawingModel model, string? query, LayerState layers)
    {
        var result = new List<TextHit>();
        if (string.IsNullOrWhiteSpace(query)) return result;
        var q = Fold(query);
        if (q.Length == 0) return result;
        for (var i = 0; i < model.Entities.Count; i++)
        {
            if (model.Entities[i] is TextEntity t && layers.IsVisible(t.Layer)
                && Fold(t.Text).Contains(q, StringComparison.Ordinal))
                result.Add(new TextHit(i, t.Position, t.Height, t.Text));
        }
        result.Sort((a, b) =>
        {
            var c = b.Position.Y.CompareTo(a.Position.Y);
            return c != 0 ? c : a.Position.X.CompareTo(b.Position.X);
        });
        return result;
    }
}

public enum SnapKind { None = 0, Endpoint, Midpoint, Center }

public readonly record struct SnapResult(Point2 Point, SnapKind Kind);

public static class SnapEngine
{
    /// <summary>
    /// Tìm điểm bắt gần nhất trong bán kính tolerance (đơn vị bản vẽ). Ưu tiên Endpoint, rồi Center, rồi Midpoint
    /// khi khoảng cách chênh nhau dưới 25% tolerance. Không có điểm nào thì trả đúng điểm chạm với Kind = None.
    /// </summary>
    public static SnapResult Find(DrawingModel model, LayerState layers, Point2 p, double tolerance)
    {
        var best = new SnapResult(p, SnapKind.None);
        var bestScore = double.PositiveInfinity;

        void Consider(Point2 c, SnapKind kind)
        {
            var d = c.DistanceTo(p);
            if (d > tolerance) return;
            var bias = kind switch { SnapKind.Endpoint => 0.0, SnapKind.Center => 0.125, _ => 0.25 } * tolerance;
            var score = d + bias;
            if (score < bestScore) { bestScore = score; best = new SnapResult(c, kind); }
        }

        foreach (var e in model.Entities)
        {
            if (!layers.IsVisible(e.Layer)) continue;
            switch (e)
            {
                case LineEntity l:
                    Consider(l.A, SnapKind.Endpoint); Consider(l.B, SnapKind.Endpoint);
                    Consider(Point2.Mid(l.A, l.B), SnapKind.Midpoint);
                    break;
                case PolylineEntity pl:
                    var n = pl.Points.Count;
                    for (var i = 0; i < n; i++)
                    {
                        Consider(pl.Points[i], SnapKind.Endpoint);
                        if (i + 1 < n) Consider(Point2.Mid(pl.Points[i], pl.Points[i + 1]), SnapKind.Midpoint);
                        else if (pl.Closed && n > 2) Consider(Point2.Mid(pl.Points[i], pl.Points[0]), SnapKind.Midpoint);
                    }
                    break;
                case CircleEntity c:
                    Consider(c.Center, SnapKind.Center);
                    break;
                case ArcEntity a:
                    Consider(a.Center, SnapKind.Center);
                    Consider(a.PointAt(a.StartDeg), SnapKind.Endpoint);
                    Consider(a.PointAt(a.EndDeg), SnapKind.Endpoint);
                    break;
            }
        }
        return best;
    }
}
