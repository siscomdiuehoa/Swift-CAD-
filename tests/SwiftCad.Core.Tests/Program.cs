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
Test("kích thước 0 thoáng qua khi xoay không làm mất bản vẽ", () =>
{
    var v = new ViewTransform(); v.Fit(model.Bounds, 360, 520, 16);
    var c = v.ScreenToWorld(180, 260);
    v.Resize(360, 520, 0, 0); v.Resize(360, 520, 640, 300);
    var c2 = v.ScreenToWorld(320, 150);
    Near(c.X, c2.X, 1e-6); Near(c.Y, c2.Y, 1e-6);
    True(v.Intersects(model.Bounds, 640, 300), "bản vẽ phải còn trong khung");
});
Test("phát hiện bản vẽ ra ngoài khung nhìn", () =>
{
    var v = new ViewTransform(); v.Fit(model.Bounds, 360, 520, 16); v.Pan(5000, 0);
    True(!v.Intersects(model.Bounds, 360, 520), "phải báo ngoài khung");
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

Console.WriteLine("Chữ xoay và căn lề");
Test("chữ xoay 90°: khung bao dựng đứng", () =>
{
    var t = new TextEntity("0", new(1000, 1000), 100, "ABCDE", 90);
    var b = t.ExtendBounds(Bounds2.Empty);
    True(b.Height > b.Width * 2, $"khung {b.Width}x{b.Height}");
    Near(1000, b.MinY, 1e-9); Near(1000 + t.EstimatedWidth, b.MaxY, 1e-9);
});
Test("căn giữa-giữa: điểm neo nằm giữa khung chữ", () =>
{
    var t = new TextEntity("0", new(0, 0), 100, "ABCD", 0, TextHAlign.Center, TextVAlign.Middle);
    var c = t.Corners(400);
    Near(-200, c[0].X, 1e-9); Near(200, c[1].X, 1e-9);
    Near(-50, c[3].Y - 100 - 0, 1e-9);
});
Test("căn phải xoay 180°: chữ nằm bên phải điểm neo", () =>
{
    var t = new TextEntity("0", new(0, 0), 10, "AB", 180, TextHAlign.Right);
    var c = t.Corners(20);
    Near(20, c[0].X, 1e-9); Near(0, c[1].X, 1e-9);
});

Console.WriteLine("Chỉ mục lưới và vùng tô");
Test("lưới trả đủ đối tượng giao khung nhìn, đúng thứ tự vẽ, không trùng", () =>
{
    var ents = new List<SwiftCad.Core.Entity>();
    var rnd = new Random(3);
    for (var i = 0; i < 5000; i++)
    {
        double x = rnd.NextDouble() * 10000, y = rnd.NextDouble() * 10000;
        ents.Add(new LineEntity("0", new(x, y), new(x + rnd.NextDouble() * 80, y + rnd.NextDouble() * 80)));
    }
    ents.Add(new FillEntity("0", [new(-100, -100), new(20000, -100), new(20000, 20000), new(-100, 20000)], true)); // vùng lớn
    var bounds = ents.Aggregate(Bounds2.Empty, (b, e) => e.ExtendBounds(b));
    var grid = new SwiftCad.Core.SpatialGrid(ents, bounds);
    var view = new Bounds2(3000, 3000, 4000, 4000);
    var got = grid.Query(view);
    True(got.SequenceEqual(got.OrderBy(i => i)), "không theo thứ tự vẽ");
    True(got.Distinct().Count() == got.Count, "trùng");
    var expect = Enumerable.Range(0, ents.Count).Where(i => { var b = ents[i].ExtendBounds(Bounds2.Empty); return b.MaxX >= view.MinX && b.MinX <= view.MaxX && b.MaxY >= view.MinY && b.MinY <= view.MaxY; }).ToList();
    foreach (var i in expect) True(got.Contains(i), "thiếu đối tượng giao khung: " + i);
    True(got.Count < ents.Count / 4, $"lọc chưa đủ: {got.Count}/{ents.Count}");
});
Test("vùng tô có khung bao đúng", () =>
{
    var f = new FillEntity("0", [new(1, 2), new(5, 2), new(5, 9)], false);
    var b = f.ExtendBounds(Bounds2.Empty);
    Near(1, b.MinX, 1e-9); Near(9, b.MaxY, 1e-9);
});

Test("trình chọn tệp không dùng kiểu octet-stream (ẩn tệp không liên quan)", () =>
{
    True(!CadFiles.PickerMimeTypes.Contains("application/octet-stream"), "còn octet-stream");
    True(CadFiles.PickerMimeTypes.Contains("image/vnd.dwg") && CadFiles.PickerMimeTypes.Contains("image/vnd.dxf"), "thiếu kiểu DWG/DXF");
});

Console.WriteLine("Đăng nhập Firebase (máy chủ giả)");
{
    var fixedNow = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    var cfg = new SwiftCad.Core.Auth.FirebaseConfig("TEST_KEY", "swift-cad-test");
    Test("đổi id_token Google lấy phiên: đúng địa chỉ, khóa, providerId, thời hạn", () =>
    {
        var h = new FakeHttp(_ => (200, """{"localId":"u1","email":"a@b.vn","displayName":"A","idToken":"ID1","refreshToken":"RT1","expiresIn":"3600"}"""));
        var c = new SwiftCad.Core.Auth.FirebaseAuthClient(new HttpClient(h), cfg, () => fixedNow);
        var s = c.SignInWithIdTokenAsync(SwiftCad.Core.Auth.AuthProvider.Google, "GTOKEN", "N1").GetAwaiter().GetResult();
        True(h.LastUrl!.StartsWith("https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=TEST_KEY"), h.LastUrl);
        True(h.LastBody!.Contains("id_token=GTOKEN") && h.LastBody.Contains("providerId=google.com") && h.LastBody.Contains("nonce=N1"), h.LastBody);
        Eq("u1", s.UserId); Eq("a@b.vn", s.Email); Eq("RT1", s.RefreshToken);
        Eq(fixedNow.AddHours(1), s.ExpiresUtc);
    });
    Test("Apple dùng providerId apple.com; tên lấy từ fullName khi thiếu displayName", () =>
    {
        var h = new FakeHttp(_ => (200, """{"localId":"u2","fullName":"Nam Phong","idToken":"I","refreshToken":"R","expiresIn":"3600"}"""));
        var s = new SwiftCad.Core.Auth.FirebaseAuthClient(new HttpClient(h), cfg, () => fixedNow)
            .SignInWithIdTokenAsync(SwiftCad.Core.Auth.AuthProvider.Apple, "AT").GetAwaiter().GetResult();
        True(h.LastBody!.Contains("providerId=apple.com"), h.LastBody); Eq("Nam Phong", s.DisplayName);
    });
    Test("lỗi Firebase trả mã lỗi rõ ràng", () =>
    {
        var h = new FakeHttp(_ => (400, """{"error":{"code":400,"message":"INVALID_IDP_RESPONSE : bad token"}}"""));
        var threw = "";
        try { new SwiftCad.Core.Auth.FirebaseAuthClient(new HttpClient(h), cfg).SignInWithIdTokenAsync(SwiftCad.Core.Auth.AuthProvider.Google, "x").GetAwaiter().GetResult(); }
        catch (SwiftCad.Core.Auth.AuthException ex) { threw = ex.Code; }
        Eq("INVALID_IDP_RESPONSE", threw);
    });
    Test("làm mới phiên giữ email, đổi token và thời hạn", () =>
    {
        var h = new FakeHttp(_ => (200, """{"id_token":"ID2","refresh_token":"RT2","expires_in":"3600","user_id":"u1"}"""));
        var c = new SwiftCad.Core.Auth.FirebaseAuthClient(new HttpClient(h), cfg, () => fixedNow);
        var old = new SwiftCad.Core.Auth.AuthSession("u1", "a@b.vn", "A", "google.com", "ID1", "RT1", fixedNow.AddMinutes(-1));
        var s = c.RefreshAsync(old).GetAwaiter().GetResult();
        True(h.LastUrl!.StartsWith("https://securetoken.googleapis.com/v1/token?key=TEST_KEY"), h.LastUrl);
        True(h.LastBody!.Contains("grant_type=refresh_token") && h.LastBody.Contains("refresh_token=RT1"), h.LastBody);
        Eq("ID2", s.IdToken); Eq("RT2", s.RefreshToken); Eq("a@b.vn", s.Email); Eq(fixedNow.AddHours(1), s.ExpiresUtc);
    });
    Test("xóa tài khoản: làm mới nếu sắp hết hạn rồi gọi accounts:delete với idToken mới", () =>
    {
        var calls = new List<string>();
        var h = new FakeHttp(req => { calls.Add(req); return req.Contains("securetoken") ? (200, """{"id_token":"NEW","refresh_token":"R2","expires_in":"3600"}""") : (200, "{}"); });
        var c = new SwiftCad.Core.Auth.FirebaseAuthClient(new HttpClient(h), cfg, () => fixedNow);
        c.DeleteAccountAsync(new SwiftCad.Core.Auth.AuthSession("u", null, null, "google.com", "OLD", "R1", fixedNow)).GetAwaiter().GetResult();
        Eq(2, calls.Count); True(calls[1].Contains("accounts:delete"), calls[1]); True(h.LastBody!.Contains("\"idToken\":\"NEW\""), h.LastBody);
    });
    Test("PKCE: challenge đúng chuẩn RFC 7636 (ví dụ trong RFC)", () =>
        Eq("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", SwiftCad.Core.Auth.Pkce.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")));
    Test("URL đăng nhập Google có PKCE S256, state, nonce, đúng redirect", () =>
    {
        var u = SwiftCad.Core.Auth.Pkce.GoogleAuthorizeUrl("cid", "http://127.0.0.1:5000/", "CH", "ST", "NO");
        True(u.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?") && u.Contains("code_challenge=CH") && u.Contains("code_challenge_method=S256")
             && u.Contains("state=ST") && u.Contains("nonce=NO") && u.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A5000%2F"), u);
    });
    Test("phiên lưu/đọc lại được; chuỗi hỏng trả null", () =>
    {
        var s = new SwiftCad.Core.Auth.AuthSession("u", "e", "n", "apple.com", "i", "r", fixedNow);
        Eq(s, SwiftCad.Core.Auth.AuthSessionCodec.Deserialize(SwiftCad.Core.Auth.AuthSessionCodec.Serialize(s)));
        True(SwiftCad.Core.Auth.AuthSessionCodec.Deserialize("{hỏng") is null, "phải null");
    });
}

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
Test("ngôn ngữ mới: Tây Ban Nha, Indonesia, Trung giản thể; phồn thể dùng tiếng Anh", () =>
{
    Eq("es", LanguageResolver.Resolve(AppLanguage.Device, "es-MX"));
    Eq("id", LanguageResolver.Resolve(AppLanguage.Device, "id-ID"));
    Eq("id", LanguageResolver.Resolve(AppLanguage.Device, "in_ID")); // mã cũ trên Android
    Eq("zh-Hans", LanguageResolver.Resolve(AppLanguage.Device, "zh-CN"));
    Eq("zh-Hans", LanguageResolver.Resolve(AppLanguage.Device, "zh-Hans-SG"));
    Eq("en", LanguageResolver.Resolve(AppLanguage.Device, "zh-TW"));
    Eq("en", LanguageResolver.Resolve(AppLanguage.Device, "zh-Hant-HK"));
    Eq("zh-Hans", LanguageResolver.Resolve(AppLanguage.ChineseSimplified, "vi-VN"));
    Eq("es", LanguageResolver.Resolve(AppLanguage.Spanish, null));
    Eq(5, LanguageResolver.Languages.Count);
    True((int)AppLanguage.Vietnamese == 2, "không đổi số của lựa chọn đã lưu");
});
Test("định dạng số theo ngôn ngữ mới", () =>
{
    Eq("es-MX", LanguageResolver.FormatCulture("es", CultureInfo.GetCultureInfo("es-MX")).Name);
    Eq("es-ES", LanguageResolver.FormatCulture("es", CultureInfo.GetCultureInfo("en-US")).Name);
    Eq("zh-CN", LanguageResolver.FormatCulture("zh-Hans", CultureInfo.GetCultureInfo("en-US")).Name);
    Eq("zh-CN", LanguageResolver.FormatCulture("zh-Hans", CultureInfo.GetCultureInfo("zh-CN")).Name);
    Eq("id-ID", LanguageResolver.FormatCulture("id", CultureInfo.GetCultureInfo("id-ID")).Name);
    Eq("12.000,0 mm", Units.Format(12000, DrawingUnit.Millimeter, CultureInfo.GetCultureInfo("id-ID")));
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
Test("ghi chú chữ, mũi tên, khoanh vùng: lưu hai điểm, chữ tùy chọn; tệp cũ không có Kind vẫn đọc được", () =>
{
    var dir = Path.Combine(tmp, "notes2"); var ns = new NoteStore(dir); var key = "cc";
    ns.Add(key, NoteKind.Text, new(10, 20), new(99, 99), " Van DN100 ", DateTimeOffset.UtcNow);
    ns.Add(key, NoteKind.Arrow, new(0, 0), new(500, 300), null, DateTimeOffset.UtcNow);
    ns.Add(key, NoteKind.Cloud, new(100, 100), new(400, 250), "Sửa lại", DateTimeOffset.UtcNow);
    var l = new NoteStore(dir).Load(key);
    Eq(3, l.Count);
    Eq(NoteKind.Text, l[0].Kind); Eq("Van DN100", l[0].Text); Near(10, l[0].X2); Near(20, l[0].Y2);
    Eq(NoteKind.Arrow, l[1].Kind); Eq("", l[1].Text); Near(500, l[1].X2); Near(300, l[1].Y2);
    Near(250, l[2].Anchor.X); Near(175, l[2].Anchor.Y);
    var threw = 0;
    try { ns.Add(key, NoteKind.Text, new(1, 1), new(1, 1), "  ", DateTimeOffset.UtcNow); } catch (ArgumentException) { threw++; }
    try { ns.Add(key, NoteKind.Cloud, new(1, 1), new(1, 1), "x", DateTimeOffset.UtcNow); } catch (ArgumentException) { threw++; }
    Eq(2, threw);
    var json = File.ReadAllText(Directory.GetFiles(dir).Single());
    True(!json.Contains("Anchor") && !json.Contains("HasSecondPoint"), "thuộc tính tính toán bị ghi vào JSON");
    // Tệp ghi chú của bản trước (không có Kind/X2/Y2) đọc thành ghim.
    File.WriteAllText(Path.Combine(dir, "dd.notes.json"), "[{\"Id\":\"a\",\"X\":5,\"Y\":6,\"Text\":\"cũ\",\"CreatedUtc\":\"2026-11-10T00:00:00+00:00\"}]");
    var old = ns.Load("dd").Single();
    Eq(NoteKind.Pin, old.Kind); Near(5, old.Anchor.X);
});
Test("nhận diện DWG/DXF theo nội dung, không theo tên", () =>
{
    static byte[] B(string s) => System.Text.Encoding.Latin1.GetBytes(s);
    Eq(".dwg", CadFiles.DetectExtension(B("AC1032\0\0\0\0\0\0\u0003")));
    Eq(".dwg", CadFiles.DetectExtension(B("AC1015\0")));
    Eq(".dwg", CadFiles.DetectExtension(B("AC1.50\0")));
    Eq(".dxf", CadFiles.DetectExtension(B("  0\r\nSECTION\r\n  2\r\nHEADER\r\n")));
    Eq(".dxf", CadFiles.DetectExtension(B("ï»¿999\nDXF tu tao\n0\nSECTION\n")));
    Eq(".dxf", CadFiles.DetectExtension(B("AutoCAD Binary DXF\r\n\u001a\0")));
    Eq<string?>(null, CadFiles.DetectExtension(B("%PDF-1.7\n")));
    Eq<string?>(null, CadFiles.DetectExtension(B("PK\u0003\u0004")));
    Eq<string?>(null, CadFiles.DetectExtension(B("ACAD")));
    Eq<string?>(null, CadFiles.DetectExtension([]));
    var dwg = B("AC1032\0");
    Eq("ĐHKK Mr.Sơn.dwg", CadFiles.ResolveName("ĐHKK Mr.Sơn.dwg", dwg));
    Eq("1699872000000.dwg", CadFiles.ResolveName("1699872000000", dwg));
    Eq("drawing.dwg", CadFiles.ResolveName(null, dwg));
    Eq("x.DXF", CadFiles.ResolveName("x.DXF", B("0\nSECTION\n")));
    Eq<string?>(null, CadFiles.ResolveName("ban-ve.dwg", B("%PDF-1.4")));
});
Test("danh sách tải về: chỉ tệp CAD, mới nhất đầu tiên, không trùng", () =>
{
    var t = new DateTimeOffset(2026, 11, 13, 1, 0, 0, TimeSpan.Zero);
    var list = CadFiles.NewestFirst(
    [
        new DeviceCadFile("cu.dwg", 1, t.AddDays(-3), "u1", "Zalo"),
        new DeviceCadFile("bao-gia.pdf", 1, t.AddDays(1), "u2", "Zalo"),
        new DeviceCadFile("moi.DXF", 1, t, "u3", "Zalo"),
        new DeviceCadFile("ĐHKK Mr.Sơn.dwg", 1, t.AddHours(1), "u4", "Zalo"),
        new DeviceCadFile("ĐHKK Mr.Sơn.dwg", 1, t.AddHours(1), "u4", "Download"),
        new DeviceCadFile("app.apk", 1, t.AddDays(2), "u5", "Download"),
    ]);
    Eq("ĐHKK Mr.Sơn.dwg,moi.DXF,cu.dwg", string.Join(",", list.Select(f => f.Name)));
    Eq(1, CadFiles.NewestFirst(list, 1).Count);
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
    Test("mọi ngôn ngữ trong LanguageResolver đều có tệp tài nguyên đủ khóa, đúng tham số", () =>
    {
        foreach (var code in LanguageResolver.Supported.Where(c => c != "en"))
        {
            var path = Path.Combine(locDir, $"AppResources.{code}.resx");
            True(File.Exists(path), "thiếu tệp " + path);
            var r = Resx(path);
            var miss = enR.Keys.Except(r.Keys).Concat(r.Keys.Except(enR.Keys)).ToList();
            True(miss.Count == 0, code + " lệch khóa: " + string.Join(", ", miss));
            var empty = r.Where(k => string.IsNullOrWhiteSpace(k.Value)).Select(k => k.Key).ToList();
            True(empty.Count == 0, code + " giá trị rỗng: " + string.Join(", ", empty));
            var bad = enR.Keys.Where(k => Regex.Matches(enR[k], @"\{\d\}").Count != Regex.Matches(r[k], @"\{\d\}").Count).ToList();
            True(bad.Count == 0, code + " lệch tham số: " + string.Join(", ", bad));
        }
    });
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

// Đo chỉ mục lưới (Issue #6): SWIFTCAD_GRID_PERF=<số đối tượng>. Đo trên máy kiểm tra, không phải Android.
if (int.TryParse(Environment.GetEnvironmentVariable("SWIFTCAD_GRID_PERF"), out var gridN) && gridN > 0)
{
    var rnd = new Random(11);
    var big = new List<SwiftCad.Core.Entity>(gridN);
    for (var i = 0; i < gridN; i++)
    {
        double x = rnd.NextDouble() * 850000, y = rnd.NextDouble() * 500000;
        big.Add(i % 3 == 0 ? new LineEntity("0", new(x, y), new(x + 400, y + 300))
              : new CircleEntity("0", new(x, y), 150));
    }
    var sb = big.Aggregate(Bounds2.Empty, (b, e) => e.ExtendBounds(b));
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var g = new SwiftCad.Core.SpatialGrid(big, sb);
    var build = sw.Elapsed.TotalMilliseconds;
    sw.Restart();
    var view = new Bounds2(100000, 100000, 160000, 160000); // khoảng 1/80 diện tích: một phần nhỏ khi phóng to
    var hits = 0;
    for (var k = 0; k < 200; k++) hits = g.Query(view).Count;
    var query = sw.Elapsed.TotalMilliseconds / 200;
    Console.WriteLine($"Chỉ mục lưới {gridN:N0} đối tượng: dựng {build:N0} ms; truy vấn khung nhìn {query:N2} ms (trung bình, {hits:N0} đối tượng trong khung).");
}

Console.WriteLine($"\n{passed} đạt, {failed} lỗi");
return failed == 0 ? 0 : 1;


/// <summary>Máy chủ HTTP giả: ghi lại yêu cầu cuối, trả phản hồi định sẵn.</summary>
sealed class FakeHttp(Func<string, (int Status, string Body)> respond) : HttpMessageHandler
{
    public string? LastUrl; public string? LastBody;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastUrl = request.RequestUri!.ToString();
        LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var (status, body) = respond(LastUrl);
        return new HttpResponseMessage((System.Net.HttpStatusCode)status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}
