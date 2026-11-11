using System.Globalization;
using System.Resources;
using SwiftCad.Core;

namespace SwiftCad.App;

/// <summary>
/// Tra chuỗi giao diện từ tài nguyên dịch (.resx). Thêm ngôn ngữ: thêm AppResources.xx.resx
/// và khai báo mã trong LanguageResolver.Supported.
/// </summary>
public static class L
{
    private static readonly ResourceManager Rm =
        new("SwiftCad.App.Resources.Localization.AppResources", typeof(L).Assembly);

    /// <summary>Ngôn ngữ và vùng của thiết bị, ghi lại trước khi ứng dụng đổi văn hóa.</summary>
    private static readonly CultureInfo DeviceCulture = CultureInfo.CurrentUICulture;

    public static string Lang { get; private set; } = LanguageResolver.Fallback;
    public static CultureInfo UiCulture { get; private set; } = CultureInfo.GetCultureInfo(LanguageResolver.Fallback);
    /// <summary>Văn hóa định dạng số và ngày theo vùng.</summary>
    public static CultureInfo FormatCulture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public static void Apply(AppLanguage preference)
    {
        Lang = LanguageResolver.Resolve(preference, DeviceCulture.Name);
        UiCulture = CultureInfo.GetCultureInfo(Lang);
        FormatCulture = LanguageResolver.FormatCulture(Lang, DeviceCulture);
        CultureInfo.DefaultThreadCurrentUICulture = UiCulture;
        CultureInfo.CurrentUICulture = UiCulture;
    }

    public static string T(string key) => Rm.GetString(key, UiCulture) ?? key;

    public static string F(string key, params object[] args) => string.Format(FormatCulture, T(key), args);
}
