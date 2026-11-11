using System.Globalization;

namespace SwiftCad.Core;

public enum AppLanguage { Device = 0, English, Vietnamese }

public static class LanguageResolver
{
    /// <summary>Mã ngôn ngữ có tài nguyên dịch. Thêm ngôn ngữ mới: thêm tệp .resx và thêm mã vào đây.</summary>
    public static readonly IReadOnlyList<string> Supported = ["en", "vi"];
    public const string Fallback = "en";

    /// <summary>Trả mã ngôn ngữ giao diện. Ngôn ngữ thiết bị chưa hỗ trợ thì dùng tiếng Anh.</summary>
    public static string Resolve(AppLanguage preference, string? deviceLanguageTag) => preference switch
    {
        AppLanguage.English => "en",
        AppLanguage.Vietnamese => "vi",
        _ => FromDevice(deviceLanguageTag),
    };

    private static string FromDevice(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return Fallback;
        var two = tag.Split('-', '_')[0].ToLowerInvariant();
        return Supported.Contains(two) ? two : Fallback;
    }

    /// <summary>
    /// Văn hóa dùng để định dạng số và ngày: giữ vùng của thiết bị khi cùng ngôn ngữ với giao diện
    /// (ví dụ en-GB, en-AU), ngược lại dùng văn hóa mặc định của ngôn ngữ giao diện.
    /// </summary>
    public static CultureInfo FormatCulture(string uiLanguage, CultureInfo device)
    {
        if (string.Equals(device.TwoLetterISOLanguageName, uiLanguage, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(device.Name))
            return device;
        return CultureInfo.GetCultureInfo(uiLanguage == "vi" ? "vi-VN" : "en-US");
    }
}
