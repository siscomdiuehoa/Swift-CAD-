namespace SwiftCad.Core;

/// <summary>
/// Bản vẽ minh họa do ứng dụng tự tạo (mặt bằng MEP một tầng, đơn vị mm). KHÔNG đọc từ tệp DWG/DXF.
/// Kích thước biết trước dùng để kiểm thử phép đo: phòng lớn 12000 x 8000, phòng kỹ thuật 4000 x 3000.
/// Chữ trong bản vẽ là nội dung bản vẽ nên giữ nguyên, không dịch theo ngôn ngữ giao diện.
/// </summary>
public static class DemoDrawing
{
    public const string FileName = "MEP-Floor-01.demo";
    public const string FileKey = "demo-mep-floor-01";

    public const string LWall = "A-WALL";
    public const string LGrid = "S-GRID";
    public const string LDuct = "M-DUCT";
    public const string LPipe = "P-PIPE";
    public const string LElec = "E-POWER";
    public const string LText = "G-ANNO";

    public static DrawingModel Create()
    {
        var layers = new List<DrawingLayer>
        {
            new(LWall, 0xFFE6EDF3),
            new(LGrid, 0xFF6B7F92),
            new(LDuct, 0xFF4FC3F7),
            new(LPipe, 0xFF81C784),
            new(LElec, 0xFFFFB74D),
            new(LText, 0xFFFFF176),
        };
        var e = new List<Entity>();

        // Tường bao 12000 x 8000 và phòng kỹ thuật 4000 x 3000 ở góc dưới phải.
        e.Add(new PolylineEntity(LWall, [new(0, 0), new(12000, 0), new(12000, 8000), new(0, 8000)], true));
        e.Add(new PolylineEntity(LWall, [new(8000, 0), new(8000, 3000), new(12000, 3000)], false));
        e.Add(new LineEntity(LWall, new(0, 5000), new(5000, 5000)));
        e.Add(new LineEntity(LWall, new(5000, 5000), new(5000, 8000)));
        // Cửa: cánh mở 900.
        e.Add(new LineEntity(LWall, new(8000, 1000), new(7100, 1000)));
        e.Add(new ArcEntity(LWall, new(8000, 1000), 900, 90, 180));

        // Lưới trục 4000 theo X, 4000 theo Y.
        string[] xs = ["1", "2", "3", "4"];
        for (var i = 0; i < 4; i++)
        {
            var x = i * 4000.0;
            e.Add(new LineEntity(LGrid, new(x, -1200), new(x, 9200)));
            e.Add(new CircleEntity(LGrid, new(x, 9600), 400));
            e.Add(new TextEntity(LGrid, new(x - 110, 9450), 300, xs[i]));
        }
        string[] ys = ["A", "B", "C"];
        for (var i = 0; i < 3; i++)
        {
            var y = i * 4000.0;
            e.Add(new LineEntity(LGrid, new(-1200, y), new(13200, y)));
            e.Add(new CircleEntity(LGrid, new(-1600, y), 400));
            e.Add(new TextEntity(LGrid, new(-1710, y - 150), 300, ys[i]));
        }

        // Ống gió chính 600 rộng chạy dọc, 3 nhánh và miệng gió.
        e.Add(new PolylineEntity(LDuct, [new(600, 6200), new(11400, 6200), new(11400, 6800), new(600, 6800)], true));
        foreach (var x in new[] { 2500.0, 6500.0, 10000.0 })
        {
            e.Add(new LineEntity(LDuct, new(x - 150, 6200), new(x - 150, 4400)));
            e.Add(new LineEntity(LDuct, new(x + 150, 6200), new(x + 150, 4400)));
            e.Add(new PolylineEntity(LDuct, [new(x - 300, 4400), new(x + 300, 4400), new(x + 300, 3800), new(x - 300, 3800)], true));
            e.Add(new LineEntity(LDuct, new(x - 300, 4400), new(x + 300, 3800)));
            e.Add(new LineEntity(LDuct, new(x - 300, 3800), new(x + 300, 4400)));
            e.Add(new TextEntity(LDuct, new(x + 400, 4000), 180, "SAD 600x600"));
        }
        e.Add(new TextEntity(LDuct, new(4200, 6400), 200, "SA 1200x600"));

        // Ống nước: tuyến chính và van.
        e.Add(new PolylineEntity(LPipe, [new(400, 400), new(400, 4600), new(4600, 4600)], false));
        e.Add(new PolylineEntity(LPipe, [new(400, 2000), new(7600, 2000), new(7600, 400)], false));
        e.Add(new CircleEntity(LPipe, new(3000, 2000), 160));
        e.Add(new CircleEntity(LPipe, new(400, 3200), 160));
        e.Add(new TextEntity(LPipe, new(1200, 2150), 200, "CHWS DN100"));
        e.Add(new TextEntity(LPipe, new(3250, 1650), 180, "VAN CỔNG DN100"));

        // Điện: tủ điện và máng cáp.
        e.Add(new PolylineEntity(LElec, [new(10800, 400), new(11600, 400), new(11600, 1000), new(10800, 1000)], true));
        e.Add(new TextEntity(LElec, new(10850, 600), 200, "MSB-01"));
        e.Add(new PolylineEntity(LElec, [new(11200, 1000), new(11200, 7600), new(600, 7600)], false));
        e.Add(new TextEntity(LElec, new(6000, 7250), 200, "MÁNG CÁP 300x100"));
        foreach (var x in new[] { 1500.0, 3500.0, 6500.0, 9500.0 })
            e.Add(new CircleEntity(LElec, new(x, 5400), 120));

        // Tên phòng và ghi chú.
        e.Add(new TextEntity(LText, new(8350, 2300), 260, "PHÒNG KỸ THUẬT ĐIỆN"));
        e.Add(new TextEntity(LText, new(1200, 7000), 260, "PHÒNG HỌP 01"));
        e.Add(new TextEntity(LText, new(4300, 2900), 300, "VĂN PHÒNG"));
        e.Add(new TextEntity(LText, new(0, -2000), 320, "MẶT BẰNG MEP TẦNG 1 - TỶ LỆ 1:100"));

        return new DrawingModel
        {
            Name = FileName,
            Unit = DrawingUnit.Millimeter,
            Layers = layers,
            Entities = e,
            IsDemo = true,
        };
    }
}
