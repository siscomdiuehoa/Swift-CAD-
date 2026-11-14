using System.Globalization;
using System.Text;

namespace SwiftCad.Core;

/// <summary>Tên tệp gọn khi chia sẻ: không dấu, không khoảng trắng, không ký tự lạ.</summary>
public static class FileNames
{
    /// <summary>"MẶT BẰNG TẦNG 6-3.dwg" + "ghi-chu" -> "MAT-BANG-TANG-6-3-ghi-chu.dwg".</summary>
    public static string ForShare(string displayName, string suffix = "")
    {
        var ext = Path.GetExtension(displayName);
        var baseName = Path.GetFileNameWithoutExtension(displayName);
        var core = Slug(baseName);
        if (core.Length == 0) core = "ban-ve";
        if (suffix.Length > 0) core += "-" + suffix;
        return core + ext.ToLowerInvariant();
    }

    public static string Slug(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = ch switch { 'đ' => 'd', 'Đ' => 'D', _ => ch };
            if (char.IsAsciiLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-' or '_' or '.' or '(' or ')') { if (sb.Length > 0 && sb[^1] != '-') sb.Append('-'); }
        }
        return sb.ToString().Trim('-');
    }
}
