using System.Text;

namespace SwiftCad.Core;

/// <summary>Một tệp CAD tìm thấy trong thư mục trên máy (chưa sao chép vào ứng dụng).</summary>
public sealed record DeviceCadFile(string Name, long SizeBytes, DateTimeOffset ModifiedUtc, string Uri, string Folder);

/// <summary>
/// Nhận diện tệp AutoCAD theo phần mở rộng và theo nội dung đầu tệp.
/// Dùng khi ứng dụng khác (Zalo, trình quản lý tệp) gửi tệp không có đuôi hoặc kiểu MIME chung chung.
/// </summary>
public static class CadFiles
{
    /// <summary>
    /// Kiểu MIME mà Android và các ứng dụng thường gán cho DWG/DXF. Không có kiểu chuẩn duy nhất nên liệt kê đủ các biến thể.
    /// application/octet-stream: tệp không rõ kiểu (máy không biết đuôi .dwg); ứng dụng vẫn kiểm tra nội dung trước khi mở.
    /// </summary>
    public static readonly string[] AndroidMimeTypes =
    [
        "image/vnd.dwg", "image/x-dwg", "application/acad", "application/x-acad", "application/autocad_dwg",
        "application/dwg", "application/x-dwg", "application/x-autocad", "drawing/x-dwg",
        "image/vnd.dxf", "image/x-dxf", "application/dxf", "application/x-dxf", "drawing/x-dxf",
        "application/octet-stream",
    ];

    // Ghi chú (13/11/2026): đã thử bỏ application/octet-stream khỏi trình chọn để ẩn tệp không liên quan, nhưng Android
    // gán chính kiểu này cho tệp DWG/DXF trong Tải xuống nên bản vẽ bị khóa, không chọn được (phát hiện bằng kiểm tra luồng).
    // Trình chọn hệ thống vẫn làm mờ và không cho chọn tệp không khớp kiểu (ví dụ PDF); ứng dụng kiểm tra nội dung sau khi chọn.

    /// <summary>Số byte đầu tệp cần đọc để nhận diện.</summary>
    public const int HeadLength = 4096;

    /// <summary>
    /// Trả ".dwg" hoặc ".dxf" nếu nội dung đầu tệp đúng định dạng, null nếu không phải tệp CAD.
    /// DWG bắt đầu bằng mã phiên bản "AC10xx"/"AC1.xx"/"AC2.xx"; DXF nhị phân bắt đầu bằng "AutoCAD Binary DXF";
    /// DXF dạng chữ có nhóm mã 0 kèm SECTION ở phần đầu.
    /// </summary>
    public static string? DetectExtension(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 6 && head[0] == (byte)'A' && head[1] == (byte)'C'
            && (head[2] == (byte)'1' || head[2] == (byte)'2')
            && (IsDigit(head[3]) || head[3] == (byte)'.') && IsDigit(head[4]) && IsDigit(head[5]))
            return ".dwg";

        var binaryDxf = "AutoCAD Binary DXF"u8;
        if (head.StartsWith(binaryDxf)) return ".dxf";

        // DXF chữ: bỏ BOM, đọc vài dòng đầu (có thể có dòng chú thích 999).
        var text = Encoding.Latin1.GetString(head.Length > 1024 ? head[..1024] : head);
        if (text.StartsWith("ï»¿", StringComparison.Ordinal)) text = text[3..];
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(8).ToList();
        for (var i = 0; i + 1 < lines.Count; i++)
        {
            if (lines[i] == "999") { i++; continue; }
            if (lines[i] == "0" && lines[i + 1].Equals("SECTION", StringComparison.OrdinalIgnoreCase)) return ".dxf";
            break;
        }
        return null;
    }

    private static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';

    /// <summary>
    /// Đặt tên tệp khi nhận từ ứng dụng khác: giữ tên gốc nếu đã có đuôi DWG/DXF,
    /// thêm đuôi theo nội dung nếu thiếu. Trả null nếu nội dung không phải tệp CAD.
    /// </summary>
    public static string? ResolveName(string? suggestedName, ReadOnlySpan<byte> head)
    {
        var detected = DetectExtension(head);
        if (detected is null) return null;
        var name = string.IsNullOrWhiteSpace(suggestedName) ? "drawing" : Path.GetFileName(suggestedName.Trim());
        if (name.Length == 0) name = "drawing";
        return FileIdentity.IsSupportedExtension(name) ? name : name + detected;
    }

    /// <summary>Chỉ giữ tệp DWG/DXF, mới sửa đổi (mới tải về) đứng đầu; trùng thời điểm thì theo tên.</summary>
    public static List<DeviceCadFile> NewestFirst(IEnumerable<DeviceCadFile> files, int max = int.MaxValue) =>
        files.Where(f => FileIdentity.IsSupportedExtension(f.Name))
            .GroupBy(f => f.Uri, StringComparer.Ordinal).Select(g => g.First())
            .OrderByDescending(f => f.ModifiedUtc)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
}
