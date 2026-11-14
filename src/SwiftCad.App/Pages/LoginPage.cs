using SwiftCad.Core;

namespace SwiftCad.App.Pages;

/// <summary>
/// Màn đăng nhập bắt buộc: chỉ đăng nhập bằng Apple hoặc Google (Firebase). Không có chế độ khách.
/// Ngôn ngữ chọn được ngay trên màn này.
/// </summary>
public class LoginPage : ContentPage
{
    public LoginPage()
    {
        BackgroundColor = Theme.Bg;
        SafeAreaEdges = SafeAreaEdges.All;
        Build();
    }

    private void Build()
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(Theme.Margin, 32), Spacing = Theme.Gap };

        var logo = Ui.Logo(88); logo.HorizontalOptions = LayoutOptions.Center; logo.Margin = new Thickness(0, 24, 0, 8);
        stack.Add(logo);
        // "Swift CAD" là tên riêng, không dịch.
        var title = Ui.Text("Swift CAD", Theme.FontTitle, bold: true); title.HorizontalTextAlignment = TextAlignment.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        stack.Add(title);
        var sub = Ui.Text(L.T("login_intro"), Theme.FontBody, Theme.TextSub); sub.HorizontalTextAlignment = TextAlignment.Center;
        stack.Add(sub);

        if (Auth.AuthService.CanUseApple)
            stack.Add(new PrimaryButton(L.T("auth_apple"), () => _ = SignIn(Auth.AuthService.SignInWithAppleAsync)));
        if (Auth.AuthService.CanUseGoogle)
            stack.Add(new PrimaryButton(L.T("auth_google"), () => _ = SignIn(Auth.AuthService.SignInWithGoogleAsync), secondary: true));
        if (!Auth.AuthService.CanUseApple && !Auth.AuthService.CanUseGoogle)
            stack.Add(Ui.Text(L.T("auth_not_configured"), Theme.FontBody, Theme.Danger));

        var langs = new VerticalStackLayout { Spacing = 4 };
        langs.Add(Ui.Text(L.T("settings_language"), Theme.FontSub, Theme.TextSub, bold: true));
        AddLanguage(langs, AppLanguage.Device, L.T("lang_device"));
        // Tên ngôn ngữ viết bằng chính ngôn ngữ đó để người dùng luôn nhận ra (không dịch).
        foreach (var (value, name) in LanguageResolver.Languages) AddLanguage(langs, value, name);
        stack.Add(new SoftSurface(langs));

        Content = new ScrollView { Content = stack };
    }

    private void AddLanguage(VerticalStackLayout host, AppLanguage value, string label) =>
        host.Add(new OptionRow(label, AppState.Settings.Language == value, () =>
        {
            AppState.Settings.Language = value;
            AppState.SaveSettings();
            L.Apply(value);
            Nav.ResetRoot();
        }));

    private async Task SignIn(Func<Task> signIn)
    {
        try { await signIn(); }
        catch (OperationCanceledException) { return; }
        catch (Exception)
        {
            await DisplayAlertAsync(L.T("auth_failed_title"), L.T("auth_failed_message"), L.T("ok"));
            return;
        }
        Nav.ResetRoot();
    }
}
