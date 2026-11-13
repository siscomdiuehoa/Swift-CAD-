using System.Globalization;

namespace SwiftCad.Core;

/// <summary>Giá trị lưu trong cài đặt: không đổi số của mục đã có để giữ lựa chọn của người dùng cũ.</summary>
public enum AppLanguage { Device = 0, English, Vietnamese, Spanish, Indonesian, ChineseSimplified }

public static class LanguageResolver
{
    /// <summary>
    /// Mã ngôn ngữ có tài nguyên dịch (tên tệp AppResources.&lt;mã&gt;.resx). Thêm ngôn ngữ mới: thêm tệp .resx,
    /// thêm mã vào đây, thêm giá trị vào AppLanguage và một dòng trong Languages.
    /// </summary>
    public static readonly IReadOnlyList<string> Supported = ["en", "vi", "es", "id", "zh-Hans"];
    public const string Fallback = "en";

    /// <summary>Danh sách hiển thị trong Cài đặt: tên ngôn ngữ viết bằng chính ngôn ngữ đó, không dịch.</summary>
    public static readonly IReadOnlyList<(AppLanguage Value, string NativeName)> Languages =
    [
        (AppLanguage.English, "English"),
        (AppLanguage.Vietnamese, "Tiếng Việt"),
        (AppLanguage.Spanish, "Español"),
        (AppLanguage.Indonesian, "Bahasa Indonesia"),
        (AppLanguage.ChineseSimplified, "简体中文"),
    ];

    /// <summary>Trả mã ngôn ngữ giao diện. Ngôn ngữ thiết bị chưa hỗ trợ thì dùng tiếng Anh.</summary>
    public static string Resolve(AppLanguage preference, string? deviceLanguageTag) => preference switch
    {
        AppLanguage.English => "en",
        AppLanguage.Vietnamese => "vi",
        AppLanguage.Spanish => "es",
        AppLanguage.Indonesian => "id",
        AppLanguage.ChineseSimplified => "zh-Hans",
        _ => FromDevice(deviceLanguageTag),
    };

    private static string FromDevice(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return Fallback;
        var parts = tag.Replace('_', '-').Split('-');
        var lang = parts[0].ToLowerInvariant();
        if (lang == "zh")
        {
            // Chỉ có chữ giản thể. Chữ phồn thể (Đài Loan, Hồng Kông, Ma Cao, hoặc ghi rõ Hant) dùng tiếng Anh thay vì giản thể.
            var rest = parts.Skip(1).Select(p => p.ToUpperInvariant()).ToList();
            if (rest.Contains("HANT") || rest.Contains("TW") || rest.Contains("HK") || rest.Contains("MO")) return Fallback;
            return "zh-Hans";
        }
        // "in" là mã cũ của tiếng Indonesia (một số máy Android vẫn dùng).
        if (lang == "in") lang = "id";
        return Supported.Contains(lang) ? lang : Fallback;
    }

    private static string BaseLanguage(string tag) => tag.Split('-')[0].ToLowerInvariant();

    /// <summary>
    /// Văn hóa dùng để định dạng số và ngày: giữ vùng của thiết bị khi cùng ngôn ngữ với giao diện
    /// (ví dụ en-GB, es-MX), ngược lại dùng văn hóa mặc định của ngôn ngữ giao diện.
    /// </summary>
    public static CultureInfo FormatCulture(string uiLanguage, CultureInfo device)
    {
        var ui = BaseLanguage(uiLanguage);
        var dev = device.TwoLetterISOLanguageName == "in" ? "id" : device.TwoLetterISOLanguageName;
        var traditional = device.Name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
            || device.Name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) || device.Name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(dev, ui, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(device.Name) && !(ui == "zh" && traditional))
            return device;
        return CultureInfo.GetCultureInfo(ui switch
        {
            "vi" => "vi-VN",
            "es" => "es-ES",
            "id" => "id-ID",
            "zh" => "zh-CN",
            _ => "en-US",
        });
    }
}
