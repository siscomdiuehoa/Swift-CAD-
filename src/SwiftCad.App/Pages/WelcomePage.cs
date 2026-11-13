using SwiftCad.Core;

namespace SwiftCad.App.Pages;

/// <summary>Chào lần đầu: chọn ngôn ngữ, dùng không cần tài khoản hoặc đăng nhập mẫu. Lựa chọn được lưu.</summary>
public class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        BackgroundColor = Theme.Bg;
        SafeAreaEdges = SafeAreaEdges.All;
        Build();
    }

    private void Build()
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(Theme.Margin, 32), Spacing = Theme.Gap };

        var logo = Ui.Logo(72); logo.HorizontalOptions = LayoutOptions.Center; logo.Margin = new Thickness(0, 24, 0, 8);
        stack.Add(logo);
        // "Swift CAD" là tên riêng, không dịch.
        var title = Ui.Text("Swift CAD", Theme.FontTitle, bold: true); title.HorizontalTextAlignment = TextAlignment.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        stack.Add(title);
        var sub = Ui.Text(L.T("welcome_subtitle"), Theme.FontBody, Theme.TextSub); sub.HorizontalTextAlignment = TextAlignment.Center;
        stack.Add(sub);

        var langs = new VerticalStackLayout { Spacing = 4 };
        langs.Add(Ui.Text(L.T("settings_language"), Theme.FontSub, Theme.TextSub, bold: true));
        AddLanguage(langs, AppLanguage.Device, L.T("lang_device"));
        // Tên ngôn ngữ viết bằng chính ngôn ngữ đó để người dùng luôn nhận ra (không dịch).
        foreach (var (value, name) in LanguageResolver.Languages) AddLanguage(langs, value, name);
        stack.Add(new SoftSurface(langs));

        stack.Add(new PrimaryButton(L.T("welcome_guest"), () => Finish(signIn: false)));
        stack.Add(new PrimaryButton(L.T("welcome_sign_in_demo"), () => Finish(signIn: true), secondary: true));
        stack.Add(Ui.Text(L.T("welcome_note"), Theme.FontSub, Theme.TextSub));

        Content = new ScrollView { Content = stack };
    }

    private void AddLanguage(VerticalStackLayout host, AppLanguage value, string label) =>
        host.Add(new OptionRow(label, AppState.Settings.Language == value, () =>
        {
            AppState.Settings.Language = value;
            AppState.SaveSettings();
            L.Apply(value);
            Build();
        }));

    private async void Finish(bool signIn)
    {
        if (signIn)
            await DisplayAlertAsync(L.T("account_demo_title"), L.T("account_demo_message"), L.T("ok"));
        AppState.Settings.DemoSignedIn = signIn;
        AppState.Settings.OnboardingDone = true;
        AppState.SaveSettings();
        Nav.ResetRoot();
    }
}
