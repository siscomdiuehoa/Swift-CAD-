using SwiftCad.Core;

namespace SwiftCad.App.Pages;

/// <summary>Ba tab Bản vẽ / Đã lưu / Cài đặt với thanh dưới nổi mềm. Nội dung cuộn, thanh dưới không che danh sách.</summary>
public partial class HomePage : ContentPage
{
    private HomeTab _tab;
    private readonly Grid _root = new() { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
    private readonly ContentView _content = new();
    private readonly Dictionary<HomeTab, ToolButton> _tabButtons = new();
    private VerticalStackLayout? _recentHost;
    private VerticalStackLayout? _downloadsHost;
    private string _filter = "";
    private bool _showAll;
    private bool _showAllDownloads;
    private const int RecentPreview = 5;
    private const int DownloadsPreview = 6;
    /// <summary>Kết quả quét gần nhất, hiện ngay khi quay lại màn hình trong lúc quét lại.</summary>
    private static List<DeviceCadFile>? _downloadsCache;

    public HomePage(HomeTab tab)
    {
        _tab = tab;
        BackgroundColor = Theme.Bg;
        SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);

        var bar = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 4 };
        AddTab(bar, 0, HomeTab.Drawings, Icons.File, L.T("tab_drawings"));
        AddTab(bar, 1, HomeTab.Saved, Icons.Bookmark, L.T("tab_saved"));
        AddTab(bar, 2, HomeTab.Settings, Icons.Settings, L.T("tab_settings"));
        var barSurface = new SoftSurface(bar, Theme.RadiusBlock, new Thickness(6)) { Margin = new Thickness(Theme.Margin, 8, Theme.Margin, 12) };

        _root.Add(_content, 0, 0);
        _root.Add(barSurface, 0, 1);
        Content = _root;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Dựng lại khi quay về từ màn hình xem để cập nhật Gần đây và Đã lưu.
        Show(_tab);
        // Tệp do ứng dụng khác gửi sang trong lúc giao diện chưa sẵn sàng.
        _ = IncomingFiles.DeliverAsync();
    }

    private void AddTab(Grid bar, int col, HomeTab tab, string icon, string label)
    {
        var b = new ToolButton(icon, label, () => Show(tab));
        _tabButtons[tab] = b;
        bar.Add(b, col);
    }

    private void Show(HomeTab tab)
    {
        _tab = tab;
        foreach (var (t, b) in _tabButtons) b.SetSelected(t == tab);
        var body = tab switch
        {
            HomeTab.Saved => BuildSaved(),
            HomeTab.Settings => BuildSettings(),
            _ => BuildDrawings(),
        };
        _content.Content = new ScrollView { Content = body };
    }

    private static VerticalStackLayout Column() => new() { Padding = new Thickness(Theme.Margin, 12, Theme.Margin, 8), Spacing = Theme.Gap };

    private static Label Heading(string text, double size = Theme.FontTitle)
    {
        var l = Ui.Text(text, size, bold: true);
        SemanticProperties.SetHeadingLevel(l, size >= Theme.FontTitle ? SemanticHeadingLevel.Level1 : SemanticHeadingLevel.Level2);
        return l;
    }

    // ---------- Bản vẽ ----------

    private View BuildDrawings()
    {
        var col = Column();

        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
        header.Add(Ui.Logo(), 0);
        header.Add(Ui.Text("Swift CAD", 18, bold: true), 1);
        header.Add(Ui.IconButton(Icons.Settings, L.T("tab_settings"), () => Show(HomeTab.Settings)), 2);
        col.Add(header);

        col.Add(Heading(L.T("tab_drawings")));
        var search = new SearchField(L.T("search_drawings"), q => { _filter = q; RefreshRecents(); });
        search.Entry.Text = _filter;
        col.Add(search);

        col.Add(new PrimaryButton(L.T("pick_file"), () => _ = PickAsync()));
        // Trình chọn tệp chỉ hiện DWG/DXF. Máy gán kiểu lạ cho tệp DWG thì vẫn còn đường mở mọi loại tệp.
        var anyFile = Ui.Text(L.T("pick_any_file"), Theme.FontSub, Theme.Primary);
        anyFile.MinimumHeightRequest = Theme.Touch; anyFile.HorizontalTextAlignment = TextAlignment.Center;
        anyFile.Tap(() => _ = PickAsync(anyType: true));
        col.Add(anyFile);

        if (DeviceCadFiles.IsSupported)
        {
            var dlHeader = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            dlHeader.Add(Heading(L.T("downloads_title"), 18), 0);
            if (DeviceCadFiles.Folders().Count > 0)
            {
                var folders = Ui.Text(L.T("downloads_folders"), Theme.FontSub, Theme.Primary, bold: true);
                folders.MinimumHeightRequest = Theme.Touch; folders.MinimumWidthRequest = Theme.Touch; folders.HorizontalTextAlignment = TextAlignment.End;
                folders.Tap(() => _ = FoldersMenuAsync());
                dlHeader.Add(folders, 1);
            }
            col.Add(dlHeader);
            _downloadsHost = new VerticalStackLayout { Spacing = 12 };
            col.Add(_downloadsHost);
            _ = RefreshDownloadsAsync();
        }

        var cloud = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = Theme.Gap };
        // Tên dịch vụ là tên riêng, không dịch. Biểu tượng đám mây trung tính, không dùng logo có bản quyền.
        cloud.Add(new SourceTile(Icons.Cloud, "Google Drive", L.T("cloud_connect"), L.F("a11y_cloud", "Google Drive"), () => _ = CloudAsync("Google Drive")), 0);
        cloud.Add(new SourceTile(Icons.Cloud, "Dropbox", L.T("cloud_connect"), L.F("a11y_cloud", "Dropbox"), () => _ = CloudAsync("Dropbox")), 1);
        col.Add(cloud);

        var recentHeader = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        recentHeader.Add(Heading(L.T("recent"), 18), 0);
        var seeAll = Ui.Text(_showAll ? L.T("see_less") : L.T("see_all"), Theme.FontSub, Theme.Primary, bold: true);
        seeAll.MinimumHeightRequest = Theme.Touch; seeAll.MinimumWidthRequest = Theme.Touch; seeAll.HorizontalTextAlignment = TextAlignment.End;
        seeAll.Tap(() => { _showAll = !_showAll; Show(HomeTab.Drawings); });
        recentHeader.Add(seeAll, 1);
        col.Add(recentHeader);

        _recentHost = new VerticalStackLayout { Spacing = 12 };
        col.Add(_recentHost);
        RefreshRecents();

        col.Add(Heading(L.T("samples"), 18));
        col.Add(new DrawingRow(DemoDrawing.FileName, L.T("sample_subtitle"), demo: true,
            L.F("a11y_open_file", DemoDrawing.FileName), () => _ = OpenDemoAsync()));
        return col;
    }

    private void RefreshRecents()
    {
        if (_recentHost is null) return;
        _recentHost.Clear();
        var all = AppState.Recents.Load();
        var items = RecentStore.Filter(all, _filter).ToList();
        if (all.Count == 0)
        {
            // Không giả lịch sử: chỉ dẫn người dùng tới Chọn tệp.
            _recentHost.Add(Ui.Text(L.T("recent_empty"), Theme.FontSub, Theme.TextSub));
            return;
        }
        if (items.Count == 0)
        {
            _recentHost.Add(Ui.Text(L.F("recent_no_match", _filter.Trim()), Theme.FontSub, Theme.TextSub));
            return;
        }
        foreach (var f in _showAll ? items : items.Take(RecentPreview)) _recentHost.Add(Row(f));
    }

    private DrawingRow Row(RecentFile f) => new(f.DisplayName, Ui.FileSubtitle(f), demo: false,
        L.F("a11y_open_file", f.DisplayName), () => _ = OpenRecentAsync(f),
        L.F("a11y_file_menu", f.DisplayName), () => _ = MenuAsync(f));

    private async Task PickAsync(bool anyType = false) => await HandleImportAsync(await Files.PickAndImportAsync(anyType));

    private async Task HandleImportAsync(ImportResult r)
    {
        switch (r.Status)
        {
            case ImportStatus.Opened:
                await Navigation.PushAsync(new OpeningPage(r.File!));
                break;
            case ImportStatus.Unsupported:
                await DisplayAlertAsync(L.T("error_unsupported_title"), L.T("error_unsupported_message"), L.T("ok"));
                break;
            case ImportStatus.Failed:
                await DisplayAlertAsync(L.T("error_open_title"), L.T("error_open_message"), L.T("ok"));
                break;
            // Cancelled: người dùng hủy, giữ nguyên màn hình và không báo gì.
        }
    }

    // ---------- Mới tải về (Android) ----------

    /// <summary>Tệp DWG/DXF trong thư mục đã cho phép (ví dụ Tải xuống › Zalo), mới tải về đứng đầu. Chỉ hiện tệp CAD.</summary>
    private async Task RefreshDownloadsAsync()
    {
        var host = _downloadsHost;
        if (host is null) return;
        if (DeviceCadFiles.Folders().Count == 0)
        {
            host.Clear();
            var box = new VerticalStackLayout { Spacing = 10 };
            box.Add(Ui.Text(L.T("downloads_explain"), Theme.FontSub, Theme.TextSub));
            box.Add(new PrimaryButton(L.T("downloads_grant"), () => _ = AddFolderAsync(), secondary: true));
            host.Add(new SoftSurface(box));
            return;
        }
        if (_downloadsCache is { } cached) FillDownloads(host, cached);
        else { host.Clear(); host.Add(Ui.Text(L.T("downloads_scanning"), Theme.FontSub, Theme.TextSub)); }

        var files = await DeviceCadFiles.ScanAsync();
        _downloadsCache = files;
        // Màn hình đã dựng lại trong lúc quét: chỉ điền vào khung còn hiện.
        if (ReferenceEquals(host, _downloadsHost)) FillDownloads(host, files);
    }

    private void FillDownloads(VerticalStackLayout host, List<DeviceCadFile> files)
    {
        host.Clear();
        if (files.Count == 0) { host.Add(Ui.Text(L.T("downloads_empty"), Theme.FontSub, Theme.TextSub)); return; }
        foreach (var f in _showAllDownloads ? files : files.Take(DownloadsPreview))
        {
            var sub = f.Folder + " · " + FileIdentity.FormatSize(f.SizeBytes, L.FormatCulture)
                + (f.ModifiedUtc > DateTimeOffset.MinValue ? " · " + f.ModifiedUtc.ToLocalTime().ToString("g", L.FormatCulture) : "");
            host.Add(new DrawingRow(f.Name, sub, demo: false, L.F("a11y_open_file", f.Name), () => _ = OpenDeviceFileAsync(f)));
        }
        if (files.Count > DownloadsPreview)
        {
            var more = Ui.Text(_showAllDownloads ? L.T("see_less") : L.T("see_all"), Theme.FontSub, Theme.Primary, bold: true);
            more.MinimumHeightRequest = Theme.Touch; more.HorizontalTextAlignment = TextAlignment.Center;
            more.Tap(() => { _showAllDownloads = !_showAllDownloads; FillDownloads(host, files); });
            host.Add(more);
        }
    }

    private async Task AddFolderAsync()
    {
        var go = await DisplayAlertAsync(L.T("downloads_grant"), L.T("downloads_grant_help"), L.T("continue"), L.T("cancel"));
        if (!go) return;
        if (await DeviceCadFiles.AddFolderAsync()) { _downloadsCache = null; Show(HomeTab.Drawings); }
    }

    private async Task FoldersMenuAsync()
    {
        var folders = DeviceCadFiles.Folders();
        var add = L.T("downloads_add_folder");
        var options = new List<string> { add };
        options.AddRange(folders.Select(f => L.F("downloads_remove_folder", f.Name)));
        var choice = await DisplayActionSheetAsync(L.T("downloads_folders"), L.T("cancel"), null, options.ToArray());
        if (choice == add) { await AddFolderAsync(); return; }
        var idx = options.IndexOf(choice ?? "") - 1;
        if (idx >= 0 && idx < folders.Count)
        {
            DeviceCadFiles.RemoveFolder(folders[idx].Uri);
            _downloadsCache = null;
            Show(HomeTab.Drawings);
        }
    }

    private async Task OpenDeviceFileAsync(DeviceCadFile f)
    {
        ImportResult r;
        try
        {
            using var s = await Task.Run(() => DeviceCadFiles.Open(f));
            r = s is null ? new ImportResult(ImportStatus.Failed, null) : await Files.ImportAsync(s, f.Name);
        }
        catch (Exception)
        {
            // Tệp đã bị xóa hoặc quyền thư mục bị thu hồi.
            r = new ImportResult(ImportStatus.Failed, null);
            _downloadsCache = null;
        }
        await HandleImportAsync(r);
    }

    /// <summary>
    /// Chưa có tích hợp trực tiếp: hướng dẫn chọn nhà cung cấp trong trình chọn tệp hệ thống.
    /// Không ghi trạng thái kết nối giả, không thu mật khẩu.
    /// </summary>
    private async Task CloudAsync(string provider)
    {
        var go = await DisplayAlertAsync(provider, L.F("cloud_guide", provider), L.T("cloud_open_picker"), L.T("cancel"));
        if (go) await PickAsync();
    }

    private Task OpenDemoAsync() => Navigation.PushAsync(new ViewerPage());

    private async Task OpenRecentAsync(RecentFile f)
    {
        if (File.Exists(f.LocalPath))
        {
            AppState.Recents.Touch(f, DateTimeOffset.UtcNow);
            await Navigation.PushAsync(new OpeningPage(f));
            return;
        }
        // Bản sao không còn: cho chọn lại hoặc gỡ khỏi danh sách.
        var pickAgain = L.T("pick_again"); var remove = L.T("menu_remove");
        var choice = await DisplayActionSheetAsync(L.F("error_missing_file", f.DisplayName), L.T("cancel"), null, pickAgain, remove);
        if (choice == pickAgain) await PickAsync();
        else if (choice == remove) { AppState.Recents.Remove(f.Id); Show(_tab); }
    }

    private async Task MenuAsync(RecentFile f)
    {
        var bookmark = f.Bookmarked ? L.T("menu_unbookmark") : L.T("menu_bookmark");
        var share = L.T("menu_share_copy"); var remove = L.T("menu_remove");
        var choice = await DisplayActionSheetAsync(f.DisplayName, L.T("cancel"), null, bookmark, share, remove);
        if (choice == bookmark) { AppState.Recents.ToggleBookmark(f.Id); Show(_tab); }
        else if (choice == share) await Files.ShareCopyAsync(f);
        else if (choice == remove) { AppState.Recents.Remove(f.Id); Show(_tab); }
    }

    // ---------- Đã lưu ----------

    private View BuildSaved()
    {
        var col = Column();
        col.Add(Heading(L.T("tab_saved")));
        col.Add(Ui.Text(L.T("saved_explain"), Theme.FontSub, Theme.TextSub));
        var saved = AppState.Recents.Load().Where(r => r.Bookmarked).ToList();
        if (saved.Count == 0) col.Add(new SoftSurface(Ui.Text(L.T("saved_empty"), Theme.FontBody, Theme.TextSub)));
        foreach (var f in saved) col.Add(Row(f));
        return col;
    }

    // ---------- Cài đặt ----------

    private View BuildSettings()
    {
        var s = AppState.Settings;
        var col = Column();
        col.Add(Heading(L.T("tab_settings")));

        // Tài khoản mẫu: không có máy chủ, không thu thông tin thật. Khách vẫn mở được tệp trên máy.
        var account = new VerticalStackLayout { Spacing = 8 };
        var accHead = new HorizontalStackLayout { Spacing = 8 };
        accHead.Add(Ui.Icon(Icons.Person, Theme.TextSub));
        accHead.Add(Ui.Text(L.T("settings_account"), Theme.FontBody, bold: true));
        accHead.Add(Ui.DemoBadge());
        account.Add(accHead);
        account.Add(Ui.Text(s.DemoSignedIn ? L.T("account_signed_in") : L.T("account_guest"), Theme.FontSub, Theme.TextSub));
        account.Add(new PrimaryButton(s.DemoSignedIn ? L.T("account_sign_out") : L.T("welcome_sign_in_demo"), async () =>
        {
            if (!s.DemoSignedIn) await DisplayAlertAsync(L.T("account_demo_title"), L.T("account_demo_message"), L.T("ok"));
            s.DemoSignedIn = !s.DemoSignedIn; AppState.SaveSettings(); Show(HomeTab.Settings);
        }, secondary: true));
        col.Add(new SoftSurface(account));

        var lang = Group(L.T("settings_language"));
        AddLanguage(lang, AppLanguage.Device, L.T("lang_device"));
        AddLanguage(lang, AppLanguage.English, "English");
        AddLanguage(lang, AppLanguage.Vietnamese, "Tiếng Việt");
        col.Add(new SoftSurface(lang));

        var unit = Group(L.T("settings_unit"));
        AddUnit(unit, DrawingUnit.Unknown, L.T("unit_from_drawing"));
        AddUnit(unit, DrawingUnit.Millimeter, L.T("unit_mm"));
        AddUnit(unit, DrawingUnit.Meter, L.T("unit_m"));
        AddUnit(unit, DrawingUnit.Inch, L.T("unit_in"));
        col.Add(new SoftSurface(unit));

        var canvas = Group(L.T("settings_canvas"));
        canvas.Add(new OptionRow(L.T("canvas_dark"), !s.BlackCanvas, () => { s.BlackCanvas = false; AppState.SaveSettings(); Show(HomeTab.Settings); }));
        canvas.Add(new OptionRow(L.T("canvas_black"), s.BlackCanvas, () => { s.BlackCanvas = true; AppState.SaveSettings(); Show(HomeTab.Settings); }));
        col.Add(new SoftSurface(canvas));

        var about = Group(L.T("settings_about"));
        about.Add(Ui.Text(L.F("about_version", AppInfo.Current.VersionString), Theme.FontSub, Theme.TextSub));
        about.Add(Ui.Text(L.T("about_no_ads"), Theme.FontSub, Theme.TextSub));
        about.Add(new OptionRow(L.T("settings_licenses"), false, () => _ = Navigation.PushAsync(new LicensesPage())));
        col.Add(new SoftSurface(about));
        return col;
    }

    private static VerticalStackLayout Group(string title)
    {
        var g = new VerticalStackLayout { Spacing = 4 };
        g.Add(Heading(title, Theme.FontBody));
        return g;
    }

    private void AddLanguage(VerticalStackLayout host, AppLanguage value, string label) =>
        host.Add(new OptionRow(label, AppState.Settings.Language == value, () =>
        {
            if (AppState.Settings.Language == value) return;
            AppState.Settings.Language = value;
            AppState.SaveSettings();
            L.Apply(value);
            Nav.ResetRoot(HomeTab.Settings);
        }));

    private void AddUnit(VerticalStackLayout host, DrawingUnit value, string label) =>
        host.Add(new OptionRow(label, AppState.Settings.DisplayUnit == value, () =>
        {
            AppState.Settings.DisplayUnit = value; AppState.SaveSettings(); Show(HomeTab.Settings);
        }));
}
