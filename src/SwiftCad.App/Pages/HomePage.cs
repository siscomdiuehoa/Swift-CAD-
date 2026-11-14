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
    private string _filter = "";
    private bool _showAll;
    private const int RecentPreview = 5;

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

    /// <summary>
    /// Chưa có tích hợp trực tiếp: hướng dẫn chọn nhà cung cấp trong trình chọn tệp hệ thống.
    /// Không ghi trạng thái kết nối giả, không thu mật khẩu.
    /// </summary>
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

        // Tài khoản (tùy chọn): đăng nhập Google/Apple qua Firebase. Khách vẫn dùng đầy đủ tính năng xem bản vẽ.
        col.Add(new SoftSurface(BuildAccount()));

        var lang = Group(L.T("settings_language"));
        AddLanguage(lang, AppLanguage.Device, L.T("lang_device"));
        // Tên ngôn ngữ viết bằng chính ngôn ngữ đó để người dùng luôn nhận ra (không dịch).
        foreach (var (value, name) in LanguageResolver.Languages) AddLanguage(lang, value, name);
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
        // Logo và tên nhà phát triển là tên riêng (pháp nhân), không dịch.
        var devLogo = new Image { Source = "siscom_logo.png", HeightRequest = 40, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(0, 4, 0, 2) };
        SemanticProperties.SetDescription(devLogo, AppInfoExtra.DeveloperName);
        about.Add(devLogo);
        about.Add(Ui.Text(L.F("about_developer", AppInfoExtra.DeveloperName), Theme.FontSub, Theme.Text, bold: true));
        // Trang web nhà phát triển: chạm để mở trình duyệt. Địa chỉ web không dịch.
        var site = Ui.Text(AppInfoExtra.Website.Replace("https://", ""), Theme.FontSub, Theme.Primary, bold: true);
        site.MinimumHeightRequest = Theme.Touch;
        site.Tap(() => _ = Launcher.Default.OpenAsync(new Uri(AppInfoExtra.Website))).A11y(L.F("a11y_open_website", AppInfoExtra.Website));
        about.Add(site);
        about.Add(Ui.Text(L.F("about_version", AppInfo.Current.VersionString), Theme.FontSub, Theme.TextSub));
        about.Add(new OptionRow(L.T("settings_licenses"), false, () => _ = Navigation.PushAsync(new LicensesPage())));
        col.Add(new SoftSurface(about));
        return col;
    }

    private View BuildAccount()
    {
        var account = new VerticalStackLayout { Spacing = 8 };
        var accHead = new HorizontalStackLayout { Spacing = 8 };
        accHead.Add(Ui.Icon(Icons.Person, Theme.TextSub));
        accHead.Add(Ui.Text(L.T("settings_account"), Theme.FontBody, bold: true));
        account.Add(accHead);
        var me = Auth.AuthService.Current;
        if (me is not null)
        {
            account.Add(Ui.Text(L.F("auth_signed_in_as", me.Email ?? me.DisplayName ?? me.UserId), Theme.FontSub, Theme.Text, bold: true));
            account.Add(new PrimaryButton(L.T("account_sign_out"), async () => { await Auth.AuthService.SignOutAsync(); Show(HomeTab.Settings); }, secondary: true));
            var del = Ui.Text(L.T("auth_delete"), Theme.FontSub, Theme.Danger, bold: true);
            del.MinimumHeightRequest = Theme.Touch;
            del.Tap(() => _ = DeleteAccountAsync()).A11y(L.T("auth_delete"));
            account.Add(del);
            return account;
        }
        if (!Auth.AuthService.IsConfigured)
        {
            account.Add(Ui.Text(L.T("auth_not_configured"), Theme.FontSub, Theme.TextSub));
            return account;
        }
        if (Auth.AuthService.CanUseApple)
            account.Add(new PrimaryButton(L.T("auth_apple"), () => _ = SignInAsync(Auth.AuthService.SignInWithAppleAsync)));
        if (Auth.AuthService.CanUseGoogle)
            account.Add(new PrimaryButton(L.T("auth_google"), () => _ = SignInAsync(Auth.AuthService.SignInWithGoogleAsync), secondary: true));
        return account;
    }

    private async Task SignInAsync(Func<Task> signIn)
    {
        try { await signIn(); }
        catch (TaskCanceledException) { return; } // người dùng đóng cửa sổ đăng nhập
        catch (OperationCanceledException) { return; }
        catch (Exception)
        {
            await DisplayAlertAsync(L.T("auth_failed_title"), L.T("auth_failed_message"), L.T("ok"));
        }
        Show(HomeTab.Settings);
    }

    private async Task DeleteAccountAsync()
    {
        if (!await DisplayAlertAsync(L.T("auth_delete_confirm_title"), L.T("auth_delete_confirm_message"), L.T("delete"), L.T("cancel"))) return;
        try
        {
            await Auth.AuthService.DeleteAccountAsync();
            await DisplayAlertAsync(L.T("auth_delete"), L.T("auth_deleted"), L.T("ok"));
        }
        catch (Exception)
        {
            await DisplayAlertAsync(L.T("auth_failed_title"), L.T("auth_failed_message"), L.T("ok"));
        }
        Show(HomeTab.Settings);
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
