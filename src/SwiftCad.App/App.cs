namespace SwiftCad.App;

public class App : Application
{
    public App()
    {
        // Giao diện sáng cố định theo thiết kế Neumorphism V2.
        UserAppTheme = AppTheme.Light;
        AppState.Init();
        // Cấu hình và phiên đăng nhập đọc rất nhanh (tệp nhỏ, kho bảo mật); chờ để màn đầu tiên đã biết trạng thái.
        // Chạy trên luồng nền để không khóa luồng giao diện (tránh treo khi chờ kho bảo mật).
        Task.Run(Auth.AuthService.InitAsync).GetAwaiter().GetResult();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if SWIFTCAD_SHOTS
        // Chỉ có trong bản dựng chụp ảnh (-p:SwiftCadShots=true), không có trong bản phát hành.
        if (ShotMode.Build() is { } shot) return new Window(shot);
#endif
        return new Window(Nav.BuildRoot());
    }
}

public static class Nav
{
    public static Page BuildRoot(HomeTab tab = HomeTab.Drawings)
    {
        if (!AppState.Settings.OnboardingDone) return new Pages.WelcomePage();
        var nav = new NavigationPage(new Pages.HomePage(tab)) { BarBackgroundColor = Theme.Bg, BarTextColor = Theme.Text };
        return nav;
    }

    /// <summary>Dựng lại toàn bộ giao diện, dùng sau khi đổi ngôn ngữ hoặc xong màn hình chào.</summary>
    public static void ResetRoot(HomeTab tab = HomeTab.Drawings)
    {
        var windows = Application.Current?.Windows;
        if (windows is { Count: > 0 }) windows[0].Page = BuildRoot(tab);
    }
}

public enum HomeTab { Drawings, Saved, Settings }
