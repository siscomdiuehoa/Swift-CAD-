using Foundation;
using UIKit;

namespace SwiftCad.App;

/// <summary>
/// iOS: tệp DWG/DXF mở từ Zalo, Tệp, Mail qua mục Mở bằng / Sao chép vào Swift CAD.
/// Kiểu tài liệu khai báo trong Info.plist (CFBundleDocumentTypes). Hệ thống chép tệp vào hộp thư đến của ứng dụng
/// hoặc cấp quyền truy cập tạm; ứng dụng chép tiếp vào vùng dữ liệu riêng rồi mở.
/// </summary>
public static class IncomingUrls
{
    public static void Handle(NSSet<UIOpenUrlContext>? contexts)
    {
        var url = contexts?.ToArray().FirstOrDefault()?.Url;
        if (url is null || !url.IsFileUrl || url.Path is null) return;
        var path = url.Path;
        IncomingFiles.Enqueue(() =>
        {
            var scoped = url.StartAccessingSecurityScopedResource();
            try
            {
                // Đọc hết vào bộ nhớ trước khi trả quyền truy cập tạm.
                var ms = new MemoryStream();
                using (var fs = File.OpenRead(path)) fs.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
            finally
            {
                if (scoped) url.StopAccessingSecurityScopedResource();
            }
        }, url.LastPathComponent);
    }
}
