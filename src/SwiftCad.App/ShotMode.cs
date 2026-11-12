#if SWIFTCAD_SHOTS
using SwiftCad.Core;
using SwiftCad.App.Pages;

namespace SwiftCad.App;

/// <summary>
/// Chế độ chụp ảnh cho CI. Chỉ biên dịch khi build với -p:SwiftCadShots=true.
/// Mở thẳng một màn hình ở trạng thái xác định để chụp trên Android emulator và iOS simulator.
/// Nguồn yêu cầu: intent extra "swiftcad_shot" (Android) hoặc biến môi trường SWIFTCAD_SHOT (iOS simctl).
/// Ngôn ngữ luôn theo thiết bị, để ảnh kiểm chứng đúng luồng "Theo thiết bị".
/// Mục Gần đây trong ảnh là dữ liệu kiểm thử do chế độ này tạo, tên bắt đầu bằng "TEST-".
/// </summary>
public static class ShotMode
{
    public static string? Requested;
    /// <summary>Chạy sau khi OpeningPage đọc xong tệp thật và mở màn hình xem.</summary>
    public static Action<ViewerPage>? AfterOpen;

    public static Page? Build()
    {
        var shot = Requested ?? Environment.GetEnvironmentVariable("SWIFTCAD_SHOT");
        if (string.IsNullOrWhiteSpace(shot)) return null;

        AppState.Settings.Language = AppLanguage.Device;
        AppState.Settings.OnboardingDone = shot != "welcome";
        AppState.Settings.DisplayUnit = DrawingUnit.Unknown;
        L.Apply(AppLanguage.Device);
        if (shot == "drawings-empty") ClearRecents(); else if (shot != "welcome") SeedRecents();

        switch (shot)
        {
            case "welcome": return new WelcomePage();
            case "drawings":
            case "drawings-empty": return Nav.BuildRoot();
            case "settings": return Nav.BuildRoot(HomeTab.Settings);
            case "saved": return Nav.BuildRoot(HomeTab.Saved);
            case "picker": return WithHome(h => h.ShotOpenPicker());
            case "cloud": return WithHome(h => h.ShotOpenCloud("Google Drive"));
        }
        if (shot.StartsWith("viewer", StringComparison.Ordinal))
        {
            if (shot.StartsWith("viewer-cad", StringComparison.Ordinal))
            {
                // Tệp DXF thật do ACadSharp ghi từ bản vẽ kiểm thử tự tạo; app đọc lại qua đúng luồng OpeningPage → CadImporter.
                var cad = WriteCadFixture();
                var cadTool = shot["viewer-cad".Length..].TrimStart('-');
                AfterOpen = v => v.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(900), () => v.ShotSetup(cadTool.Length == 0 ? "none" : cadTool));
                return WithViewer(new OpeningPage(cad), _ => { });
            }
            if (shot == "viewer-real")
            {
                var real = AppState.Recents.Load().FirstOrDefault(r => r.Source == FileSource.Device);
                return WithViewer(new OpeningPage(real!), _ => { });
            }
            var tool = shot["viewer".Length..].TrimStart('-');
            return WithViewer(new ViewerPage(), v => v.ShotSetup(tool));
        }
        return null;
    }

    private static Page WithHome(Action<HomePage> after)
    {
        var home = new HomePage(HomeTab.Drawings);
        var nav = new NavigationPage(home) { BarBackgroundColor = Theme.Bg, BarTextColor = Theme.Text };
        var done = false;
        home.Loaded += async (_, _) =>
        {
            if (done) return; done = true;
            await Task.Delay(800);
            after(home);
        };
        return nav;
    }

    private static Page WithViewer(Page viewer, Action<ViewerPage> after)
    {
        var home = new HomePage(HomeTab.Drawings);
        var nav = new NavigationPage(home) { BarBackgroundColor = Theme.Bg, BarTextColor = Theme.Text };
        var done = false;
        home.Loaded += async (_, _) =>
        {
            if (done) return; done = true;
            await nav.PushAsync(viewer, false);
            await Task.Delay(800);
            if (viewer is ViewerPage vp) after(vp);
        };
        return nav;
    }

    private static void ClearRecents()
    {
        foreach (var r in AppState.Recents.Load()) AppState.Recents.Remove(r.Id);
    }

    /// <summary>Tạo tệp DXF kiểm thử thật trong vùng dữ liệu của app và ghi vào Gần đây qua đúng kho dữ liệu của app.</summary>
    private static void SeedRecents()
    {
        ClearRecents();
        Directory.CreateDirectory(AppState.DrawingsDir);
        var now = DateTimeOffset.UtcNow;
        (string name, int kb, int minutesAgo, bool saved)[] items =
        [
            ("TEST-Tầng 1 - Điện chiếu sáng.dxf", 412, 25, true),
            ("TEST-HVAC-L2-Supply-Air-Layout-Rev-C.dxf", 1830, 190, false),
            ("TEST-PCCC-B1.dxf", 96, 60 * 26, false),
        ];
        foreach (var (name, _, ago, saved) in items)
        {
            // Tệp DXF thật do ACadSharp ghi, nội dung tự tạo (không phải bản vẽ khách hàng).
            var doc = new ACadSharp.CadDocument();
            doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;
            for (var i = 0; i <= 6; i++)
            {
                doc.Entities.Add(new ACadSharp.Entities.Line { StartPoint = new CSMath.XYZ(i * 2000, 0, 0), EndPoint = new CSMath.XYZ(i * 2000, 8000, 0) });
                doc.Entities.Add(new ACadSharp.Entities.Line { StartPoint = new CSMath.XYZ(0, i * 8000 / 6.0, 0), EndPoint = new CSMath.XYZ(12000, i * 8000 / 6.0, 0) });
            }
            doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = name, InsertPoint = new CSMath.XYZ(0, -900, 0), Height = 300 });
            doc.Entities.Add(new ACadSharp.Entities.Hatch());
            var ms = new MemoryStream();
            ACadSharp.IO.DxfWriter.Write(ms, doc);
            var bytes = ms.ToArray();
            string key;
            using (var hs = new MemoryStream(bytes)) key = FileIdentity.ComputeKey(hs);
            var path = Path.Combine(AppState.DrawingsDir, key + ".dxf");
            File.WriteAllBytes(path, bytes);
            var entry = AppState.Recents.Touch(new RecentFile
            {
                DisplayName = name, Source = FileSource.Device, LocalPath = path, SizeBytes = bytes.Length, FileKey = key,
            }, now.AddMinutes(-ago));
            if (saved && !entry.Bookmarked) AppState.Recents.ToggleBookmark(entry.Id);
        }
    }

    /// <summary>Bản vẽ kiểm thử MEP: tường 12000 x 8000 mm, block van chèn 3 lần (tỷ lệ, xoay), chữ tiếng Việt, MTEXT, layer tắt, hatch chưa hỗ trợ.</summary>
    private static RecentFile WriteCadFixture()
    {
        var doc = new ACadSharp.CadDocument();
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;
        var wall = new ACadSharp.Tables.Layer("A-WALL") { Color = new ACadSharp.Color(4) };
        var anno = new ACadSharp.Tables.Layer("G-ANNO") { Color = new ACadSharp.Color(2) };
        var pipe = new ACadSharp.Tables.Layer("P-PIPE") { Color = new ACadSharp.Color(3) };
        var hidden = new ACadSharp.Tables.Layer("Z-HIDDEN") { Color = new ACadSharp.Color(1), IsOn = false };
        foreach (var l in new[] { wall, anno, pipe, hidden }) doc.Layers.Add(l);
        static CSMath.XYZ P(double x, double y) => new(x, y, 0);
        doc.Entities.Add(new ACadSharp.Entities.LwPolyline(new ACadSharp.Entities.LwPolyline.Vertex(0, 0), new ACadSharp.Entities.LwPolyline.Vertex(12000, 0),
            new ACadSharp.Entities.LwPolyline.Vertex(12000, 8000), new ACadSharp.Entities.LwPolyline.Vertex(0, 8000)) { IsClosed = true, Layer = wall });
        doc.Entities.Add(new ACadSharp.Entities.Line { StartPoint = P(8000, 0), EndPoint = P(8000, 3000), Layer = wall });
        doc.Entities.Add(new ACadSharp.Entities.Line { StartPoint = P(8000, 3000), EndPoint = P(12000, 3000), Layer = wall });
        doc.Entities.Add(new ACadSharp.Entities.Arc { Center = P(8000, 1000), Radius = 900, StartAngle = Math.PI / 2, EndAngle = Math.PI, Layer = wall });
        doc.Entities.Add(new ACadSharp.Entities.LwPolyline(new ACadSharp.Entities.LwPolyline.Vertex(400, 400), new ACadSharp.Entities.LwPolyline.Vertex(400, 2000) { Bulge = 0.4 },
            new ACadSharp.Entities.LwPolyline.Vertex(7600, 2000)) { Layer = pipe });
        var block = new ACadSharp.Tables.BlockRecord("VAN-DN100");
        block.Entities.Add(new ACadSharp.Entities.Circle { Center = P(0, 0), Radius = 160 });
        block.Entities.Add(new ACadSharp.Entities.Line { StartPoint = P(-160, -160), EndPoint = P(160, 160) });
        block.Entities.Add(new ACadSharp.Entities.Line { StartPoint = P(-160, 160), EndPoint = P(160, -160) });
        doc.BlockRecords.Add(block);
        foreach (var (x, s, r) in new[] { (2000.0, 1.0, 0.0), (4500.0, 2.0, Math.PI / 4), (7000.0, 1.5, Math.PI / 2) })
            doc.Entities.Add(new ACadSharp.Entities.Insert(block) { InsertPoint = P(x, 2000), XScale = s, YScale = s, Rotation = r, Layer = pipe });
        doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "PHÒNG KỸ THUẬT ĐIỆN", InsertPoint = P(8300, 1500), Height = 260, Layer = anno });
        doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "VĂN PHÒNG", InsertPoint = P(3000, 5000), Height = 400, Layer = anno });
        doc.Entities.Add(new ACadSharp.Entities.MText { Value = "GHI CHÚ: ỐNG CHWS DN100\\PVAN CỔNG x3", InsertPoint = P(400, 7600), Height = 220, Layer = anno });
        doc.Entities.Add(new ACadSharp.Entities.Line { StartPoint = P(0, 0), EndPoint = P(12000, 8000), Layer = hidden });
        doc.Entities.Add(new ACadSharp.Entities.Ellipse { Center = P(10000, 6000), MajorAxisEndPoint = P(800, 0), RadiusRatio = 0.5, Layer = wall });
        var ms = new MemoryStream();
        ACadSharp.IO.DxfWriter.Write(ms, doc);
        var bytes = ms.ToArray();
        string key;
        using (var hs = new MemoryStream(bytes)) key = FileIdentity.ComputeKey(hs);
        Directory.CreateDirectory(AppState.DrawingsDir);
        var path = Path.Combine(AppState.DrawingsDir, key + ".dxf");
        File.WriteAllBytes(path, bytes);
        return AppState.Recents.Touch(new RecentFile
        {
            DisplayName = "TEST-MEP-Tang1.dxf", Source = FileSource.Device, LocalPath = path, SizeBytes = bytes.Length, FileKey = key,
        }, DateTimeOffset.UtcNow);
    }
}
#endif
