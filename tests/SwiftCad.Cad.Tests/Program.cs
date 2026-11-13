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
    block.Entities.Add(new Circle { Center = new XYZ(0, 0, 0), Radius = 50 });
    doc.BlockRecords.Add(block);
    doc.Entities.Add(new Insert(block) { InsertPoint = new XYZ(500, 500, 0), XScale = 2, YScale = 2, Rotation = Math.PI / 2, Layer = wall });

    doc.Entities.Add(new Ellipse { Center = new XYZ(0, 0, 0), MajorAxisEndPoint = new XYZ(50, 0, 0), RadiusRatio = 0.5, Layer = wall });

    // Chữ xoay và căn lề (review Codex PR #3).
    doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "XOAY90", InsertPoint = new XYZ(1000, 1000, 0), Height = 100, Rotation = Math.PI / 2, Layer = anno });
    doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "GIUA", InsertPoint = new XYZ(1900, 1950, 0), AlignmentPoint = new XYZ(2000, 2000, 0),
        HorizontalAlignment = TextHorizontalAlignment.Middle, Height = 100, Layer = anno });
    doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "CANPHAI", InsertPoint = new XYZ(2500, 2500, 0), AlignmentPoint = new XYZ(2600, 2600, 0),
        HorizontalAlignment = TextHorizontalAlignment.Right, VerticalAlignment = TextVerticalAlignmentType.Top, Height = 100, Layer = anno });
    doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "DAN2DIEM", InsertPoint = new XYZ(0, 6000, 0), AlignmentPoint = new XYZ(0, 7000, 0),
        HorizontalAlignment = TextHorizontalAlignment.Aligned, Height = 100, Layer = anno });
    doc.Entities.Add(new MText { Value = "TAMGIUA", InsertPoint = new XYZ(3000, 3000, 0), Height = 100, AttachmentPoint = AttachmentPointType.MiddleCenter, Layer = anno });
    doc.Entities.Add(new MText { Value = "DONGA\\PDONGB", InsertPoint = new XYZ(4000, 0, 0), Height = 100, AttachmentPoint = AttachmentPointType.BottomRight,
        AlignmentPoint = new XYZ(0, 1, 0), LineSpacing = 1, Layer = anno });
    var tagBlock = new BlockRecord("NHAN");
    tagBlock.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "TRONGBLOCK", InsertPoint = new XYZ(-50, -10, 0), AlignmentPoint = new XYZ(0, 0, 0),
        HorizontalAlignment = TextHorizontalAlignment.Center, Height = 20 });
    tagBlock.Entities.Add(new AttributeDefinition { Tag = "SO", Value = "KHUON", InsertPoint = new XYZ(0, -40, 0), Height = 20 });
    doc.BlockRecords.Add(tagBlock);
    var tagIns = new Insert(tagBlock) { InsertPoint = new XYZ(5000, 5000, 0), XScale = 2, YScale = 2, Rotation = Math.PI / 2, Layer = anno };
    // Insert(block) tự tạo AttributeEntity từ khuôn; đặt giá trị như AutoCAD sau khi chèn.
    var att = tagIns.Attributes.Single(a => a.Tag == "SO");
    att.Value = "Đ01"; att.InsertPoint = new XYZ(5100, 5000, 0); att.Height = 40; att.Rotation = Math.PI / 2;
    doc.Entities.Add(tagIns);
    doc.Entities.Add(new Hatch { Layer = wall });
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
        var c = m.Entities.OfType<CircleEntity>().Single(e => Math.Abs(e.Center.X - 3000) < 1e-9); Near(160, c.Radius, 1e-9, "bán kính");
        var a = m.Entities.OfType<ArcEntity>().Single(); Near(90, a.StartDeg, 1e-9, "góc đầu"); Near(180, a.EndDeg, 1e-9, "góc cuối");
    });
    Test("polyline có bulge thành nửa đường tròn", () =>
    {
        var p = m.Entities.OfType<PolylineEntity>().Single(e => !e.Closed && e.Points.Count > 3);
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
    Test("tròn trong block vẫn hiển thị (Explode đổi thành Ellipse)", () =>
    {
        var c = m.Entities.OfType<CircleEntity>().SingleOrDefault(e => Math.Abs(e.Center.X - 500) < 1e-6 && Math.Abs(e.Center.Y - 500) < 1e-6);
        True(c is not null, "thiếu đường tròn trong block"); Near(100, c!.Radius, 1e-6, "bán kính sau tỷ lệ 2");
    });
    Test("ellipse thật thành polyline, không còn bị bỏ qua", () =>
    {
        True(r.Skipped.GetValueOrDefault("Ellipse") == 0, "Ellipse vẫn bị bỏ qua");
        var e = m.Entities.OfType<PolylineEntity>().Single(p => p.Closed && p.Points.Count > 60);
        Near(50, e.Points.Max(p => p.X), 1e-6, "bán trục lớn");
    });
    SwiftCad.Core.TextEntity T(string text) => m.Entities.OfType<SwiftCad.Core.TextEntity>().Single(x => x.Text == text);
    Test("TEXT xoay 90° giữ góc", () => { var t = T("XOAY90"); Near(90, t.RotationDeg, 1e-6, "góc"); Near(1000, t.Position.X, 1e-6, "x"); });
    Test("TEXT căn Middle neo tại điểm căn", () =>
    {
        var t = T("GIUA"); True(t.HAlign == TextHAlign.Center && t.VAlign == TextVAlign.Middle, $"{t.HAlign}/{t.VAlign}");
        Near(2000, t.Position.X, 1e-6, "x"); Near(2000, t.Position.Y, 1e-6, "y");
    });
    Test("TEXT căn phải-trên", () =>
    {
        var t = T("CANPHAI"); True(t.HAlign == TextHAlign.Right && t.VAlign == TextVAlign.Top, $"{t.HAlign}/{t.VAlign}");
        Near(2600, t.Position.X, 1e-6, "x");
    });
    Test("TEXT Aligned hiển thị gần đúng và được đếm", () =>
    {
        Near(90, T("DAN2DIEM").RotationDeg, 1e-6, "hướng theo hai điểm");
        True(r.Approximated.GetValueOrDefault("TEXT Aligned/Fit") == 1, "chưa đếm Aligned");
    });
    Test("MTEXT MiddleCenter: căn giữa, chân chữ thấp hơn điểm gắn nửa cao chữ", () =>
    {
        var t = T("TAMGIUA"); True(t.HAlign == TextHAlign.Center, t.HAlign.ToString());
        Near(3000, t.Position.X, 1e-6, "x"); Near(2950, t.Position.Y, 1e-6, "y");
    });
    Test("MTEXT BottomRight xoay 90°: hai dòng đúng vị trí", () =>
    {
        var a = T("DONGA"); var b = T("DONGB");
        Near(90, a.RotationDeg, 1e-6, "góc"); True(a.HAlign == TextHAlign.Right, "căn phải");
        Near(4000 - 500.0 / 3, a.Position.X, 1e-6, "x dòng 1"); Near(0, a.Position.Y, 1e-6, "y dòng 1");
        Near(4000, b.Position.X, 1e-6, "x dòng 2"); Near(0, b.Position.Y, 1e-6, "y dòng 2");
    });
    Test("chữ trong block xoay 90° tỷ lệ 2: góc, cỡ, điểm căn đúng", () =>
    {
        var t = T("TRONGBLOCK");
        Near(90, t.RotationDeg, 1e-6, "góc"); Near(40, t.Height, 1e-6, "cao"); True(t.HAlign == TextHAlign.Center, "căn giữa");
        Near(5000, t.Position.X, 1e-6, "x neo"); Near(5000, t.Position.Y, 1e-6, "y neo");
    });
    Test("giá trị thuộc tính block hiển thị, khuôn thuộc tính thì không", () =>
    {
        var t = T("Đ01"); Near(90, t.RotationDeg, 1e-6, "góc"); Near(5100, t.Position.X, 1e-6, "x");
        var bad = m.Entities.OfType<SwiftCad.Core.TextEntity>().Where(x => x.Text is "KHUON" or "SO").Select(x => $"{x.Text}@{x.Position.X:0},{x.Position.Y:0}").ToList();
        True(bad.Count == 0, "hiện khuôn thuộc tính: " + string.Join(" ", bad));
    });
    Test("layer tắt trong tệp thì ẩn mặc định", () =>
    {
        True(!m.Layers.Single(l => l.Name == "Z-HIDDEN").VisibleByDefault, "Z-HIDDEN phải ẩn");
        True(m.Layers.Single(l => l.Name == "A-WALL").VisibleByDefault, "A-WALL phải hiện");
    });
    Test("đối tượng chưa hỗ trợ được đếm, không bỏ im lặng", () =>
        True(r.Skipped.GetValueOrDefault("Hatch") == 1, "Skipped: " + string.Join(",", r.Skipped.Select(k => k.Key + "=" + k.Value))));
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

// Đo riêng việc đọc trong một tiến trình mới (đỉnh bộ nhớ không lẫn phần tạo tệp): SWIFTCAD_PERF_READ=<đường dẫn>.
var perfRead = Environment.GetEnvironmentVariable("SWIFTCAD_PERF_READ");
if (!string.IsNullOrEmpty(perfRead))
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    ImportReport rep;
    using (var fs = File.OpenRead(perfRead)) rep = CadImporter.Read(fs, Path.GetFileName(perfRead));
    sw.Stop();
    Console.WriteLine($"PERF {Path.GetFileName(perfRead)}: {new FileInfo(perfRead).Length / 1048576.0:N1} MB, {sw.Elapsed.TotalSeconds:N2} s, " +
                      $"{rep.Model.Entities.Count:N0} đối tượng, đỉnh bộ nhớ tiến trình {System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0:N0} MB");
    return 0;
}

// Đo tệp lớn (Issue #4): SWIFTCAD_PERF=<số đối tượng>. Tệp DXF tạo sinh dạng mặt bằng MEP lặp lại, không phải bản vẽ khách hàng.
if (int.TryParse(Environment.GetEnvironmentVariable("SWIFTCAD_PERF"), out var perfN) && perfN > 0)
{
    Console.WriteLine($"Đo tệp lớn ({perfN:N0} đối tượng)");
    var big = new CadDocument();
    big.Header.InsUnits = UnitsType.Millimeters;
    var lw = new Layer("M-DUCT"); var la = new Layer("G-ANNO"); big.Layers.Add(lw); big.Layers.Add(la);
    var van = new BlockRecord("VAN");
    van.Entities.Add(new Circle { Center = new XYZ(0, 0, 0), Radius = 80 });
    van.Entities.Add(new Line { StartPoint = new XYZ(-80, -80, 0), EndPoint = new XYZ(80, 80, 0) });
    big.BlockRecords.Add(van);
    var rnd = new Random(7);
    for (var i = 0; i < perfN; i++)
    {
        double x = (i % 1000) * 600, y = (i / 1000) * 600;
        switch (i % 5)
        {
            case 0: big.Entities.Add(new Line { StartPoint = new XYZ(x, y, 0), EndPoint = new XYZ(x + 500, y + rnd.Next(400), 0), Layer = lw }); break;
            case 1: big.Entities.Add(new LwPolyline(new LwPolyline.Vertex(x, y), new LwPolyline.Vertex(x + 300, y) { Bulge = 0.3 }, new LwPolyline.Vertex(x + 300, y + 300)) { Layer = lw }); break;
            case 2: big.Entities.Add(new Circle { Center = new XYZ(x, y, 0), Radius = 120, Layer = lw }); break;
            case 3: big.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "ỐNG " + i, InsertPoint = new XYZ(x, y, 0), Height = 100, Layer = la }); break;
            default: big.Entities.Add(new Insert(van) { InsertPoint = new XYZ(x, y, 0), Layer = lw }); break;
        }
    }
    foreach (var ext in new[] { ".dxf", ".dwg" })
    {
        var path = Path.Combine(Path.GetTempPath(), "swiftcad-perf" + ext);
        if (ext == ".dwg") DwgWriter.Write(path, big); else DxfWriter.Write(path, big);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var before = GC.GetTotalMemory(true);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ImportReport rep;
        using (var fs = File.OpenRead(path)) rep = CadImporter.Read(fs, "perf" + ext);
        sw.Stop();
        var peakWorkingSet = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64;
        var held = GC.GetTotalMemory(true) - before;
        Console.WriteLine($"       {ext}: tệp {new FileInfo(path).Length / 1048576.0:N1} MB, đọc + chuyển đổi {sw.Elapsed.TotalSeconds:N2} s, " +
                          $"{rep.Model.Entities.Count:N0} đối tượng hiển thị, mô hình giữ lại {held / 1048576.0:N0} MB, đỉnh bộ nhớ tiến trình {peakWorkingSet / 1048576.0:N0} MB");
        GC.KeepAlive(rep);
        var keep = Environment.GetEnvironmentVariable("SWIFTCAD_PERF_KEEP");
        if (!string.IsNullOrEmpty(keep)) File.Copy(path, Path.Combine(keep, "swiftcad-perf" + ext), true);
        File.Delete(path);
    }
}

Console.WriteLine($"\n{passed} đạt, {failed} lỗi");
return failed == 0 ? 0 : 1;
