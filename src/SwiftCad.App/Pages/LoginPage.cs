using Microsoft.Maui.Controls.Shapes;
using SwiftCad.Core;

namespace SwiftCad.App.Pages;

/// <summary>
/// Màn đăng nhập bắt buộc (theo mẫu Figma "Login & Signup Screen"): nền xanh nhạt, các hình tròn xanh đậm ở góc,
/// nút bo tròn. Chỉ có Apple và Google, không có tên người dùng/mật khẩu.
/// </summary>
public class LoginPage : ContentPage
{
    // Màu lấy từ mẫu Figma: nền sáng, xanh đậm, xanh vừa.
    private static readonly Color Background = Color.FromArgb("#E6F3FF");
    private static readonly Color DeepBlue = Color.FromArgb("#00508C");
    private static readonly Color MidBlue = Color.FromArgb("#1A6399");

    public LoginPage()
    {
        BackgroundColor = Background;
        SafeAreaEdges = SafeAreaEdges.All;
        Build();
    }

    private void Build()
    {
        var root = new Grid();

        // Hình trang trí (không nhận thao tác chạm).
        root.Add(Circle(340, MidBlue, HorizontalOptions: LayoutOptions.End, VerticalOptions: LayoutOptions.Start, tx: 130, ty: -120));
        root.Add(Circle(250, DeepBlue, HorizontalOptions: LayoutOptions.End, VerticalOptions: LayoutOptions.Start, tx: 40, ty: -150));
        root.Add(Circle(300, DeepBlue, HorizontalOptions: LayoutOptions.Start, VerticalOptions: LayoutOptions.End, tx: -120, ty: 120));
        root.Add(Circle(72, MidBlue, HorizontalOptions: LayoutOptions.Center, VerticalOptions: LayoutOptions.End, tx: 30, ty: -70));

        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(Theme.Margin, 24),
            Spacing = 14,
            VerticalOptions = LayoutOptions.Center,
        };

        var logo = Ui.Logo(96);
        logo.HorizontalOptions = LayoutOptions.Center;
        stack.Add(logo);

        // "Swift CAD" là tên riêng, không dịch.
        var title = Ui.Text("Swift CAD", 28, DeepBlue, bold: true);
        title.HorizontalTextAlignment = TextAlignment.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        stack.Add(title);

        var intro = Ui.Text(L.T("login_intro"), Theme.FontBody, Color.FromArgb("#2F4A63"));
        intro.HorizontalTextAlignment = TextAlignment.Center;
        intro.Margin = new Thickness(8, 0, 8, 18);
        stack.Add(intro);

        if (Auth.AuthService.CanUseApple)
            stack.Add(Pill(L.T("auth_apple"), Color.FromArgb("#000000"), Colors.White, null, () => _ = SignIn(Auth.AuthService.SignInWithAppleAsync)));
        if (Auth.AuthService.CanUseGoogle)
            stack.Add(Pill(L.T("auth_google"), Colors.White, DeepBlue, DeepBlue, () => _ = SignIn(Auth.AuthService.SignInWithGoogleAsync)));
        if (!Auth.AuthService.CanUseApple && !Auth.AuthService.CanUseGoogle)
        {
            var none = Ui.Text(L.T("auth_not_configured"), Theme.FontBody, Theme.Danger);
            none.HorizontalTextAlignment = TextAlignment.Center;
            stack.Add(none);
        }

        var langs = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Center, Margin = new Thickness(0, 26, 0, 0) };
        AddLanguageChip(langs, AppLanguage.Device, L.T("lang_device"));
        foreach (var (value, name) in LanguageResolver.Languages) AddLanguageChip(langs, value, name);
        stack.Add(langs);

        root.Add(new ScrollView { Content = stack });
        Content = root;
    }

    private static Shape Circle(double size, Color fill, LayoutOptions HorizontalOptions, LayoutOptions VerticalOptions, double tx, double ty) =>
        new Ellipse
        {
            Fill = new SolidColorBrush(fill), WidthRequest = size, HeightRequest = size,
            HorizontalOptions = HorizontalOptions, VerticalOptions = VerticalOptions,
            TranslationX = tx, TranslationY = ty, InputTransparent = true,
        };

    /// <summary>Nút bo tròn kiểu viên thuốc, cùng khổ với mẫu Figma (cao 52).</summary>
    private static View Pill(string text, Color fill, Color textColor, Color? border, Action onTap)
    {
        var b = new Border
        {
            BackgroundColor = fill,
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Stroke = new SolidColorBrush(border ?? Colors.Transparent),
            StrokeThickness = border is null ? 0 : 1.5,
            HeightRequest = 52,
            Padding = new Thickness(16, 0),
            Shadow = border is null ? Theme.SoftShadow() : null,
            Content = new Label
            {
                Text = text, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = textColor,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            },
        };
        return b.Tap(onTap).A11y(text);
    }

    private void AddLanguageChip(FlexLayout host, AppLanguage value, string label)
    {
        var selected = AppState.Settings.Language == value;
        var chip = new Border
        {
            BackgroundColor = selected ? DeepBlue : Colors.Transparent,
            Stroke = new SolidColorBrush(DeepBlue), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 6), Margin = new Thickness(4),
            Content = new Label { Text = label, FontSize = 13, TextColor = selected ? Colors.White : DeepBlue, FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None },
        };
        chip.Tap(() =>
        {
            AppState.Settings.Language = value;
            AppState.SaveSettings();
            L.Apply(value);
            Nav.ResetRoot();
        }).A11y(label);
        host.Add(chip);
    }

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
