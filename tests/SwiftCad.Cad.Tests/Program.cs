using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using SwiftCad.Cad;
using SwiftCad.Core;

var failed = 0; var passed = 0;
void Test(string name, Action body)
{
    try { body(); passed++; Console.WriteLine("  ok   " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("  FAIL " + name + "\n       " + ex.GetType().Name + ": " + ex.Message); }
}
static void True(bool c, string what) { if (!c) throw new Exception(what); }
static void Near(double e, double a, double tol, string what) { if (Math.Abs(e - a) > tol) throw new Exception($"{what}: mong đợi {e}, nhận {a}"); }

// Bản vẽ kiểm thử dựng bằng ACadSharp: kích thước biết trước, chữ tiếng Việt, block lồng, layer tắt, đối tượng chưa hỗ trợ.
static CadDocument BuildFixture()
{
    var doc = new CadDocument();
    doc.Header.InsUnits = UnitsType.Millimeters;
    var wall = new Layer("A-WALL") { Color = new ACadSharp.Color(1) };
    var anno = new Layer("G-ANNO") { Color = new ACadSharp.Color(2) };
    var hidden = new Layer("Z-HIDDEN") { IsOn = false };
    doc.Layers.Add(wall); doc.Layers.Add(anno); doc.Layers.Add(hidden);

    doc.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(12000, 0, 0), Layer = wall });
    doc.Entities.Add(new Circle { Center = new XYZ(3000, 2000, 0), Radius = 160, Layer = wall });
    doc.Entities.Add(new Arc { Center = new XYZ(8000, 1000, 0), Radius = 900, StartAngle = Math.PI / 2, EndAngle = Math.PI, Layer = wall });
    var pl = new LwPolyline(new LwPolyline.Vertex(0, 0) { Bulge = 1 }, new LwPolyline.Vertex(10, 0)) { Layer = wall };
    doc.Entities.Add(pl);
    doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "PHÒNG KỸ THUẬT ĐIỆN", InsertPoint = new XYZ(8350, 2300, 0), Height = 260, Layer = anno });
    doc.Entities.Add(new MText { Value = "GHI CHÚ\\PDÒNG 2", InsertPoint = new XYZ(100, 5000, 0), Height = 200, Layer = anno });
    doc.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(1, 1, 0), Layer = hidden });

    var block = new BlockRecord("VAN");
    block.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(100, 0, 0) });
    doc.BlockRecords.Add(block);
    doc.Entities.Add(new Insert(block) { InsertPoint = new XYZ(500, 500, 0), XScale = 2, YScale = 2, Rotation = Math.PI / 2, Layer = wall });

    doc.Entities.Add(new Ellipse { Center = new XYZ(0, 0, 0), MajorAxisEndPoint = new XYZ(50, 0, 0), RadiusRatio = 0.5, Layer = wall });
    return doc;
}

static ImportReport RoundTrip(CadDocument doc, string ext, bool binary = false)
{
    var ms = new MemoryStream();
    if (ext == ".dwg") DwgWriter.Write(ms, doc); else DxfWriter.Write(ms, doc, binary);
    // Bộ ghi của ACadSharp đóng stream; ToArray vẫn dùng được trên MemoryStream đã đóng.
    using var read = new MemoryStream(ms.ToArray());
    return CadImporter.Read(read, "TEST-fixture" + ext);
}

foreach (var (label, ext, binary) in new[] { ("DXF ASCII", ".dxf", false), ("DXF binary", ".dxf", true), ("DWG AC1032", ".dwg", false) })
{
    Console.WriteLine(label);
    ImportReport r = null!;
    Test("đọc không lỗi", () => r = RoundTrip(BuildFixture(), ext, binary));
    if (r is null) continue;
    var m = r.Model;
    Test("không gắn cờ Demo, đơn vị mm", () => { True(!m.IsDemo, "IsDemo"); True(m.Unit == DrawingUnit.Millimeter, "unit " + m.Unit); });
    Test("đường 12000 mm giữ đúng tọa độ", () =>
    {
        var l = m.Entities.OfType<LineEntity>().First(e => e.Layer == "A-WALL" && Math.Abs(e.B.X - 12000) < 1e-6);
        Near(12000, l.A.DistanceTo(l.B), 1e-9, "độ dài");
    });
    Test("tròn và cung (độ)", () =>
    {
        var c = m.Entities.OfType<CircleEntity>().Single(); Near(160, c.Radius, 1e-9, "bán kính");
        var a = m.Entities.OfType<ArcEntity>().Single(); Near(90, a.StartDeg, 1e-9, "góc đầu"); Near(180, a.EndDeg, 1e-9, "góc cuối");
    });
    Test("polyline có bulge thành nửa đường tròn", () =>
    {
        var p = m.Entities.OfType<PolylineEntity>().Single(e => e.Points.Count > 3);
        foreach (var pt in p.Points) Near(5, pt.DistanceTo(new Point2(5, 0)), 1e-6, "điểm trên cung");
        Near(0, p.Points[0].X, 1e-9, "đầu"); Near(10, p.Points[^1].X, 1e-9, "cuối");
    });
    Test("chữ tiếng Việt giữ nguyên Unicode", () =>
        True(m.Entities.OfType<SwiftCad.Core.TextEntity>().Any(t => t.Text == "PHÒNG KỸ THUẬT ĐIỆN"), "thiếu chữ"));
    Test("MTEXT tách dòng", () =>
    {
        var t = m.Entities.OfType<SwiftCad.Core.TextEntity>().Select(x => x.Text).ToList();
        True(t.Contains("GHI CHÚ") && t.Contains("DÒNG 2"), string.Join(" | ", t));
    });
    Test("tìm chữ chạy trên dữ liệu thật", () =>
        True(TextSearch.Find(m, "ky thuat", new LayerState()).Count == 1, "không tìm thấy"));
    Test("block được mở rộng với tỷ lệ 2 và xoay 90°", () =>
    {
        var l = m.Entities.OfType<LineEntity>().Single(e => Math.Abs(e.A.X - 500) < 1e-6 && Math.Abs(e.A.Y - 500) < 1e-6);
        Near(200, l.A.DistanceTo(l.B), 1e-6, "độ dài sau tỷ lệ"); Near(500, l.B.X, 1e-6, "x sau xoay"); Near(700, l.B.Y, 1e-6, "y sau xoay");
    });
    Test("layer tắt trong tệp thì ẩn mặc định", () =>
    {
        True(!m.Layers.Single(l => l.Name == "Z-HIDDEN").VisibleByDefault, "Z-HIDDEN phải ẩn");
        True(m.Layers.Single(l => l.Name == "A-WALL").VisibleByDefault, "A-WALL phải hiện");
    });
    Test("đối tượng chưa hỗ trợ được đếm, không bỏ im lặng", () =>
        True(r.Skipped.GetValueOrDefault("Ellipse") == 1, "Skipped: " + string.Join(",", r.Skipped.Select(k => k.Key + "=" + k.Value))));
}

// Ghi tệp DXF kiểm thử cho kiểm tra luồng trên emulator (tools/ci/fixtures). Chỉ chạy khi được yêu cầu.
var fixtureOut = Environment.GetEnvironmentVariable("SWIFTCAD_WRITE_FIXTURE");
if (!string.IsNullOrEmpty(fixtureOut)) { DxfWriter.Write(fixtureOut, BuildFixture()); Console.WriteLine("Đã ghi " + fixtureOut); }

Console.WriteLine("Khác");
Test("giải mã \\U+XXXX của DXF cũ", () =>
    True(CadImporter.DecodeUnicode("PH\\U+00D2NG \\U+0110\\U+1ec6N") == "PHÒNG ĐỆN", CadImporter.DecodeUnicode("PH\\U+00D2NG \\U+0110\\U+1ec6N")));
Test("tệp hỏng báo CadImportException", () =>
{
    var threw = false;
    try { CadImporter.Read(new MemoryStream("not a drawing"u8.ToArray()), "x.dwg"); } catch (CadImportException) { threw = true; }
    True(threw, "phải ném CadImportException");
});
Test("đuôi tệp lạ bị từ chối", () =>
{
    var threw = false;
    try { CadImporter.Read(new MemoryStream([1]), "x.pdf"); } catch (CadImportException) { threw = true; }
    True(threw, "phải từ chối");
});

// Tệp mẫu do AutoCAD tạo trong repo ACadSharp (MIT). Chỉ chạy khi có đường dẫn, không chép vào repo này.
var samples = Environment.GetEnvironmentVariable("SWIFTCAD_ACAD_SAMPLES");
if (!string.IsNullOrEmpty(samples) && Directory.Exists(samples))
{
    Console.WriteLine("Tệp mẫu AutoCAD trong repo ACadSharp");
    foreach (var f in Directory.EnumerateFiles(samples, "sample_*.*").Where(f => f.EndsWith(".dwg") || f.EndsWith(".dxf")).Order())
    {
        Test(Path.GetFileName(f), () =>
        {
            using var s = File.OpenRead(f);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = CadImporter.Read(s, Path.GetFileName(f));
            Console.WriteLine($"       {r.Model.Entities.Count} đối tượng, {r.Model.Layers.Count} layer, bỏ qua {r.SkippedTotal}, {r.Model.Unit}, {sw.ElapsedMilliseconds} ms");
        });
    }
}

Console.WriteLine($"\n{passed} đạt, {failed} lỗi");
return failed == 0 ? 0 : 1;
