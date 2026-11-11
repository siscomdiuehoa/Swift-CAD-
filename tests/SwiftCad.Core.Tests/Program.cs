using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SwiftCad.Core;

var failed = 0; var passed = 0;
void Test(string name, Action body)
{
    try { body(); passed++; Console.WriteLine("  ok   " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("  FAIL " + name + "\n       " + ex.Message); }
}
static void Eq<T>(T expected, T actual, string what = "")
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"{what} mong đợi <{expected}> nhưng nhận <{actual}>");
}
static void Near(double expected, double actual, double tol = 1e-6, string what = "")
{
    if (Math.Abs(expected - actual) > tol) throw new Exception($"{what} mong đợi {expected} nhưng nhận {actual}");
}
static void True(bool c, string what) { if (!c) throw new Exception(what); }

var model = DemoDrawing.Create();
var all = new LayerState();
var vi = CultureInfo.GetCultureInfo("vi-VN");
var en = CultureInfo.GetCultureInfo("en-US");

Console.WriteLine("Đơn vị và phép đo");
Test("quy đổi mm → m → in", () =>
{
    Near(12.0, Units.Convert(12000, DrawingUnit.Millimeter, DrawingUnit.Meter));
    Near(472.44094488, Units.Convert(12000, DrawingUnit.Millimeter, DrawingUnit.Inch), 1e-6);
    Near(25.4, Units.Convert(1, DrawingUnit.Inch, DrawingUnit.Millimeter));
    Near(304.8, Units.Convert(1, DrawingUnit.Foot, DrawingUnit.Millimeter));
});
Test("đo cạnh phòng 12000 mm từ tọa độ", () =>
{
    var m = new Measurement(new(0, 0), new(12000, 0));
    Near(12000, m.RawDistance);
    Near(12.0, m.DistanceIn(DrawingUnit.Millimeter, DrawingUnit.Meter)!.Value);
});
Test("đường chéo phòng kỹ thuật 4000 x 3000 = 5000", () =>
    Near(5000, new Measurement(new(8000, 0), new(12000, 3000)).RawDistance));
Test("đơn vị nguồn chưa biết: không tự quy đổi", () =>
{
    var m = new Measurement(new(0, 0), new(100, 0));
    True(m.DistanceIn(DrawingUnit.Unknown, DrawingUnit.Meter) is null, "phải trả null");
    Near(100, m.DistanceIn(DrawingUnit.Unknown, DrawingUnit.Unknown)!.Value);
});
Test("định dạng số theo vùng", () =>
{
    Eq("12,000.0 mm", Units.Format(12000, DrawingUnit.Millimeter, en));
    Eq("12.000,0 mm", Units.Format(12000, DrawingUnit.Millimeter, vi));
    Eq("12,000 m", Units.Format(12, DrawingUnit.Meter, vi));
    Eq("46.72", Units.Format(46.72, DrawingUnit.Unknown, en).Substring(0, 5));
});

Console.WriteLine("Camera");
Test("fit đưa tâm bản vẽ vào giữa khung", () =>
{
    var v = new ViewTransform(); v.Fit(model.Bounds, 360, 520, 16);
    var (sx, sy) = v.WorldToScreen(model.Bounds.Center);
    Near(180, sx, 1e-6); Near(260, sy, 1e-6);
    var (x0, _) = v.WorldToScreen(new(model.Bounds.MinX, 0));
    var (x1, _) = v.WorldToScreen(new(model.Bounds.MaxX, 0));
    True(x0 >= 16 - 1e-6 && x1 <= 344 + 1e-6, "bản vẽ phải nằm trong lề");
});
Test("screen ↔ world khứ hồi, trục Y lật", () =>
{
    var v = new ViewTransform(); v.Fit(model.Bounds, 390, 600, 16); v.Pan(13, -7); v.ZoomAt(100, 200, 2.5);
    var w = v.ScreenToWorld(123.4, 456.7); var (sx, sy) = v.WorldToScreen(w);
    Near(123.4, sx, 1e-6); Near(456.7, sy, 1e-6);
    True(v.ScreenToWorld(0, 0).Y > v.ScreenToWorld(0, 100).Y, "Y màn hình xuống thì Y bản vẽ giảm");
});
Test("zoom giữ điểm dưới ngón tay", () =>
{
    var v = new ViewTransform(); v.Fit(model.Bounds, 360, 520, 16);
    var before = v.ScreenToWorld(90, 300); v.ZoomAt(90, 300, 3);
    var after = v.ScreenToWorld(90, 300);
    Near(before.X, after.X, 1e-6); Near(before.Y, after.Y, 1e-6);
});
Test("xoay máy giữ tâm nhìn và tỷ lệ", () =>
{
    var v = new ViewTransform(); v.Fit(model.Bounds, 360, 520, 16); v.ZoomAt(50, 60, 4);
    var c = v.ScreenToWorld(180, 260); var s = v.Scale;
    v.Resize(360, 520, 640, 300);
    var c2 = v.ScreenToWorld(320, 150);
    Near(c.X, c2.X, 1e-6); Near(c.Y, c2.Y, 1e-6); Near(s, v.Scale);
});
Test("số đo không đổi sau pan/zoom", () =>
{
    var v = new ViewTransform(); v.Fit(model.Bounds, 360, 520, 16);
    var (ax, ay) = v.WorldToScreen(new(0, 0)); var (bx, by) = v.WorldToScreen(new(12000, 0));
    var d1 = v.ScreenToWorld(ax, ay).DistanceTo(v.ScreenToWorld(bx, by));
    v.Pan(40, 90); v.ZoomAt(10, 10, 7.3);
    (ax, ay) = v.WorldToScreen(new(0, 0)); (bx, by) = v.WorldToScreen(new(12000, 0));
    var d2 = v.ScreenToWorld(ax, ay).DistanceTo(v.ScreenToWorld(bx, by));
    Near(12000, d1, 1e-6); Near(12000, d2, 1e-6);
});
Test("tọa độ lớn (1e7) vẫn chính xác dưới 1e-3", () =>
{
    var v = new ViewTransform();
    v.Fit(new Bounds2(1e7, 2e7, 1e7 + 5000, 2e7 + 5000), 360, 520, 16);
    var p = new Point2(1e7 + 1234.567, 2e7 + 4321.123);
    var (sx, sy) = v.WorldToScreen(p); var q = v.ScreenToWorld(sx, sy);
    Near(p.X, q.X, 1e-3); Near(p.Y, q.Y, 1e-3);
});

Console.WriteLine("Bắt điểm");
Test("bắt đầu mút góc phòng", () =>
{
    var r = SnapEngine.Find(model, all, new(11950, 40), 150);
    Eq(SnapKind.Endpoint, r.Kind); Eq(new Point2(12000, 0), r.Point);
});
Test("bắt trung điểm đoạn tường", () =>
{
    var r = SnapEngine.Find(model, all, new(2500, 5030), 150);
    Eq(SnapKind.Midpoint, r.Kind); Eq(new Point2(2500, 5000), r.Point);
});
Test("bắt tâm van", () =>
{
    var r = SnapEngine.Find(model, all, new(3040, 2010), 150);
    Eq(SnapKind.Center, r.Kind); Eq(new Point2(3000, 2000), r.Point);
});
Test("không có điểm gần: trả điểm chạm", () =>
{
    var p = new Point2(6000, 3300); var r = SnapEngine.Find(model, all, p, 50);
    Eq(SnapKind.None, r.Kind); Eq(p, r.Point);
});
Test("layer tắt thì không bắt điểm", () =>
{
    var ls = new LayerState(); ls.Set(DemoDrawing.LPipe, false);
    Eq(SnapKind.None, SnapEngine.Find(model, ls, new(3040, 2010), 100).Kind);
});

Console.WriteLine("Tìm chữ và layer");
Test("tìm không dấu, không phân biệt hoa thường", () =>
{
    Eq(3, TextSearch.Find(model, "phong", all).Count, "phong");
    Eq(3, TextSearch.Find(model, "PHÒNG", all).Count, "PHÒNG");
    Eq(1, TextSearch.Find(model, "kỹ thuật điện", all).Count);
    Eq(1, TextSearch.Find(model, "ky thuat dien", all).Count);
    Eq(3, TextSearch.Find(model, "sad", all).Count);
    Eq(0, TextSearch.Find(model, "khongco", all).Count);
    Eq(0, TextSearch.Find(model, "  ", all).Count);
});
Test("kết quả sắp từ trên xuống", () =>
{
    var h = TextSearch.Find(model, "phòng", all);
    True(h[0].Position.Y >= h[1].Position.Y && h[1].Position.Y >= h[2].Position.Y, "thứ tự Y");
    Eq("PHÒNG HỌP 01", h[0].Text);
});
Test("tắt layer loại kết quả; hiện tất cả khôi phục", () =>
{
    var ls = new LayerState(); ls.Set(DemoDrawing.LText, false);
    Eq(0, TextSearch.Find(model, "phòng", ls).Count); Eq(1, ls.HiddenCount);
    ls.ShowAll(); Eq(3, TextSearch.Find(model, "phòng", ls).Count);
});
Test("lọc danh sách layer", () =>
{
    Eq(1, LayerState.Filter(model.Layers, "duct").Count());
    Eq(6, LayerState.Filter(model.Layers, "").Count());
});
Test("bản vẽ Demo gắn cờ IsDemo, đơn vị mm, bao 12000 x 8000 có mặt", () =>
{
    True(model.IsDemo, "IsDemo"); Eq(DrawingUnit.Millimeter, model.Unit);
    True(model.Bounds.Width > 12000 && model.Bounds.Height > 8000, "bounds");
});

Console.WriteLine("Ngôn ngữ");
Test("theo thiết bị, dự phòng tiếng Anh", () =>
{
    Eq("vi", LanguageResolver.Resolve(AppLanguage.Device, "vi-VN"));
    Eq("en", LanguageResolver.Resolve(AppLanguage.Device, "en-GB"));
    Eq("en", LanguageResolver.Resolve(AppLanguage.Device, "ja-JP"));
    Eq("en", LanguageResolver.Resolve(AppLanguage.Device, null));
    Eq("vi", LanguageResolver.Resolve(AppLanguage.Vietnamese, "ja-JP"));
    Eq("en", LanguageResolver.Resolve(AppLanguage.English, "vi-VN"));
});
Test("văn hóa định dạng giữ vùng thiết bị khi cùng ngôn ngữ", () =>
{
    Eq("en-GB", LanguageResolver.FormatCulture("en", CultureInfo.GetCultureInfo("en-GB")).Name);
    Eq("vi-VN", LanguageResolver.FormatCulture("vi", CultureInfo.GetCultureInfo("ja-JP")).Name);
    Eq("en-US", LanguageResolver.FormatCulture("en", CultureInfo.GetCultureInfo("vi-VN")).Name);
});

Console.WriteLine("Lưu trữ");
var tmp = Path.Combine(Path.GetTempPath(), "swiftcad-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
Test("cài đặt: lưu ngôn ngữ và bỏ qua đăng nhập qua lần mở lại", () =>
{
    var p = Path.Combine(tmp, "settings.json");
    Eq(AppLanguage.Device, new SettingsStore(p).Load().Language);
    new SettingsStore(p).Save(new AppSettings { Language = AppLanguage.Vietnamese, OnboardingDone = true, DisplayUnit = DrawingUnit.Meter });
    var s = new SettingsStore(p).Load();
    Eq(AppLanguage.Vietnamese, s.Language); True(s.OnboardingDone, "onboarding"); Eq(DrawingUnit.Meter, s.DisplayUnit);
    True(!s.DemoSignedIn, "mặc định là khách");
});
Test("cài đặt: tệp hỏng không làm sập", () =>
{
    var p = Path.Combine(tmp, "bad.json"); File.WriteAllText(p, "{not json");
    Eq(AppLanguage.Device, new SettingsStore(p).Load().Language);
});
Test("gần đây: thêm, mở lại không trùng, sắp theo lần mở, đánh dấu, xóa, lọc", () =>
{
    var st = new RecentStore(Path.Combine(tmp, "recent.json"));
    var t0 = new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);
    var a = st.Touch(new RecentFile { DisplayName = "Tầng 1 - Điện.dwg", Source = FileSource.Device, FileKey = "aa", SizeBytes = 2048 }, t0);
    st.Touch(new RecentFile { DisplayName = "HVAC-L2.dxf", Source = FileSource.Device, FileKey = "bb" }, t0.AddMinutes(5));
    st.Touch(new RecentFile { DisplayName = "Tầng 1 - Điện.dwg", Source = FileSource.Device, FileKey = "aa" }, t0.AddMinutes(9));
    var items = st.Load();
    Eq(2, items.Count); Eq("Tầng 1 - Điện.dwg", items[0].DisplayName); Eq(2048L, items[0].SizeBytes);
    True(st.ToggleBookmark(a.Id), "bookmark on"); True(st.Load().Single(r => r.Id == a.Id).Bookmarked, "persist");
    Eq(1, RecentStore.Filter(st.Load(), "tang 1").Count());
    Eq(1, RecentStore.Filter(st.Load(), "hvac").Count());
    st.Remove(a.Id); Eq(1, st.Load().Count);
});
Test("ghi chú: lưu riêng theo khóa tệp, không đụng tệp gốc", () =>
{
    var original = Path.Combine(tmp, "goc.dxf"); File.WriteAllText(original, "0\nSECTION\n0\nEOF\n");
    string key; using (var s = File.OpenRead(original)) key = FileIdentity.ComputeKey(s);
    var before = File.ReadAllBytes(original); var stamp = File.GetLastWriteTimeUtc(original);
    var ns = new NoteStore(Path.Combine(tmp, "notes"));
    var n = ns.Add(key, new(3000, 2000), "  Kiểm tra van  ", DateTimeOffset.UtcNow);
    ns.Add(key, new(1, 2), "B", DateTimeOffset.UtcNow);
    var loaded = new NoteStore(Path.Combine(tmp, "notes")).Load(key);
    Eq(2, loaded.Count); Eq("Kiểm tra van", loaded[0].Text); Near(3000, loaded[0].X);
    True(before.SequenceEqual(File.ReadAllBytes(original)) && stamp == File.GetLastWriteTimeUtc(original), "tệp gốc bị thay đổi");
    Eq(0, ns.Load("khac").Count);
    ns.Remove(key, n.Id); Eq(1, ns.Load(key).Count);
});
Test("khóa tệp đổi khi nội dung đổi; khóa lạ bị từ chối", () =>
{
    var k1 = FileIdentity.ComputeKey(new MemoryStream([1, 2, 3])); var k2 = FileIdentity.ComputeKey(new MemoryStream([1, 2, 4]));
    True(k1 != k2 && k1.Length == 64, "sha256");
    var threw = false; try { new NoteStore(tmp).Load("../x"); } catch (ArgumentException) { threw = true; }
    True(threw, "phải từ chối khóa có ký tự đường dẫn");
});
Test("phần mở rộng và dung lượng", () =>
{
    True(FileIdentity.IsSupportedExtension("A.DWG") && FileIdentity.IsSupportedExtension("b.dxf"), "dwg/dxf");
    True(!FileIdentity.IsSupportedExtension("c.pdf"), "pdf");
    Eq("1,5 MB", FileIdentity.FormatSize(1572864, vi)); Eq("1.5 MB", FileIdentity.FormatSize(1572864, en));
});
try { Directory.Delete(tmp, true); } catch { }

Console.WriteLine("Tài nguyên dịch");
var root = AppContext.BaseDirectory;
while (root is not null && !File.Exists(Path.Combine(root, "global.json"))) root = Path.GetDirectoryName(root);
var locDir = root is null ? null : Path.Combine(root, "src", "SwiftCad.App", "Resources", "Localization");
if (locDir is null || !Directory.Exists(locDir)) { Console.WriteLine("  (bỏ qua: không tìm thấy thư mục tài nguyên)"); }
else
{
    static Dictionary<string, string> Resx(string p) =>
        XDocument.Load(p).Root!.Elements("data").ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")!.Value);
    var enR = Resx(Path.Combine(locDir, "AppResources.resx"));
    var viR = Resx(Path.Combine(locDir, "AppResources.vi.resx"));
    Test("EN và VI có cùng bộ khóa, không giá trị rỗng", () =>
    {
        var miss = enR.Keys.Except(viR.Keys).Concat(viR.Keys.Except(enR.Keys)).ToList();
        True(miss.Count == 0, "lệch khóa: " + string.Join(", ", miss));
        var empty = enR.Concat(viR).Where(k => string.IsNullOrWhiteSpace(k.Value)).Select(k => k.Key).ToList();
        True(empty.Count == 0, "giá trị rỗng: " + string.Join(", ", empty));
    });
    Test("tham số {0} khớp giữa hai ngôn ngữ", () =>
    {
        var bad = enR.Keys.Where(k => Regex.Matches(enR[k], @"\{\d\}").Count != Regex.Matches(viR[k], @"\{\d\}").Count).ToList();
        True(bad.Count == 0, string.Join(", ", bad));
    });
    Test("mọi khóa dùng trong mã đều có trong tài nguyên; không có khóa thừa", () =>
    {
        var appDir = Path.Combine(root!, "src", "SwiftCad.App");
        var used = Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bL\.(?:T|F)\(""([A-Za-z0-9_]+)""").Select(m => m.Groups[1].Value))
            .ToHashSet();
        var missing = used.Except(enR.Keys).ToList();
        True(missing.Count == 0, "thiếu trong resx: " + string.Join(", ", missing));
        var unused = enR.Keys.Except(used).ToList();
        True(unused.Count == 0, "khóa không dùng: " + string.Join(", ", unused));
    });
    Test("mã giao diện không chứa chữ tiếng Việt viết cứng", () =>
    {
        var appDir = Path.Combine(root!, "src", "SwiftCad.App");
        var bad = new List<string>();
        foreach (var f in Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            // Dữ liệu kiểm thử của chế độ chụp ảnh (chỉ có trong bản dựng CI) được phép chứa chữ tiếng Việt.
            if (f.EndsWith("ShotMode.cs") || f.EndsWith(".Shots.cs")) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"(?:[^\"\\\\]|\\\\.)*\""))
                if (Regex.IsMatch(m.Value, @"[À-ỹ]") && !m.Value.Contains("Tiếng Việt")) bad.Add(Path.GetFileName(f) + ": " + m.Value);
        }
        True(bad.Count == 0, string.Join(" | ", bad));
    });
}

Console.WriteLine($"\n{passed} đạt, {failed} lỗi");
return failed == 0 ? 0 : 1;
