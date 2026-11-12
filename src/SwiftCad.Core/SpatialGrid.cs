namespace SwiftCad.Core;

/// <summary>
/// Chỉ mục lưới đều để chỉ lấy đối tượng có thể nằm trong khung nhìn. Đối tượng lớn (phủ quá nhiều ô)
/// nằm trong danh sách luôn kiểm tra. Kết quả giữ đúng thứ tự vẽ ban đầu, vì WIPEOUT phụ thuộc thứ tự.
/// </summary>
public sealed class SpatialGrid
{
    private const int Cells = 128;
    private const int MaxCellsPerEntity = 256;

    private readonly Bounds2 _bounds;
    private readonly double _cw, _ch;
    private readonly List<int>[] _cells = new List<int>[Cells * Cells];
    private readonly List<int> _always = new();
    private readonly Bounds2[] _entityBounds;
    private readonly int[] _stamp;
    private int _current;

    public int EntityCount => _entityBounds.Length;

    public SpatialGrid(IReadOnlyList<Entity> entities, Bounds2 bounds)
    {
        _bounds = bounds;
        _cw = Math.Max(bounds.Width, 1e-9) / Cells;
        _ch = Math.Max(bounds.Height, 1e-9) / Cells;
        _entityBounds = new Bounds2[entities.Count];
        _stamp = new int[entities.Count];
        for (var i = 0; i < entities.Count; i++)
        {
            var eb = entities[i].ExtendBounds(Bounds2.Empty);
            _entityBounds[i] = eb;
            if (eb.IsEmpty) { _always.Add(i); continue; }
            var (x0, y0, x1, y1) = CellRange(eb);
            var count = (x1 - x0 + 1) * (y1 - y0 + 1);
            if (count > MaxCellsPerEntity) { _always.Add(i); continue; }
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                    (_cells[y * Cells + x] ??= new List<int>()).Add(i);
        }
    }

    private (int, int, int, int) CellRange(Bounds2 b)
    {
        int X(double v) => Math.Clamp((int)Math.Floor((v - _bounds.MinX) / _cw), 0, Cells - 1);
        int Y(double v) => Math.Clamp((int)Math.Floor((v - _bounds.MinY) / _ch), 0, Cells - 1);
        return (X(b.MinX), Y(b.MinY), X(b.MaxX), Y(b.MaxY));
    }

    /// <summary>Chỉ số đối tượng có thể hiển thị trong vùng (theo bản vẽ), tăng dần theo thứ tự vẽ.</summary>
    public List<int> Query(Bounds2 view)
    {
        _current++;
        var result = new List<int>();
        void Take(int i)
        {
            if (_stamp[i] == _current) return;
            _stamp[i] = _current;
            var eb = _entityBounds[i];
            if (!eb.IsEmpty && (eb.MaxX < view.MinX || eb.MinX > view.MaxX || eb.MaxY < view.MinY || eb.MinY > view.MaxY)) return;
            result.Add(i);
        }
        foreach (var i in _always) Take(i);
        if (!view.IsEmpty)
        {
            var clipped = new Bounds2(Math.Max(view.MinX, _bounds.MinX), Math.Max(view.MinY, _bounds.MinY),
                                      Math.Min(view.MaxX, _bounds.MaxX), Math.Min(view.MaxY, _bounds.MaxY));
            if (clipped.MinX <= clipped.MaxX && clipped.MinY <= clipped.MaxY)
            {
                var (x0, y0, x1, y1) = CellRange(clipped);
                for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                    {
                        var cell = _cells[y * Cells + x];
                        if (cell is null) continue;
                        foreach (var i in cell) Take(i);
                    }
            }
        }
        result.Sort();
        return result;
    }
}
