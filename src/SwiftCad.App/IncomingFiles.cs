using SwiftCad.App.Pages;

namespace SwiftCad.App;

/// <summary>
/// Tệp do ứng dụng khác gửi sang (Zalo, trình quản lý tệp, Mail…) qua mục Mở bằng hoặc Chia sẻ.
/// Nền tảng chỉ cần gọi Enqueue với cách mở luồng và tên gợi ý; lớp này chờ giao diện sẵn sàng, sao chép tệp vào
/// vùng dữ liệu của ứng dụng (tệp gốc không bị sửa) rồi mở màn hình xem.
/// </summary>
public static class IncomingFiles
{
    private static (Func<Stream?> Open, string? Name)? _pending;
    private static bool _delivering;

    public static void Enqueue(Func<Stream?> open, string? suggestedName)
    {
        _pending = (open, suggestedName);
        MainThread.BeginInvokeOnMainThread(() => _ = DeliverAsync());
    }

    /// <summary>Gọi lại an toàn nhiều lần (khi mở ứng dụng, khi màn hình chính hiện).</summary>
    public static async Task DeliverAsync()
    {
        if (_delivering || _pending is null) return;
        _delivering = true;
        try
        {
            // Chưa đăng nhập: giữ tệp chờ, sẽ mở sau khi đăng nhập xong (màn chính gọi lại).
            if (Auth.AuthService.Current is null) return;

            NavigationPage? nav = null;
            for (var i = 0; i < 60 && nav is null; i++)
            {
                nav = Application.Current?.Windows.LastOrDefault()?.Page as NavigationPage;
                if (nav is null) await Task.Delay(100);
            }
            if (nav is null || _pending is not { } item) return;
            _pending = null;

            ImportResult result;
            try
            {
                using var src = await Task.Run(item.Open);
                result = src is null ? new ImportResult(ImportStatus.Failed, null) : await Files.ImportAsync(src, item.Name);
            }
            catch (Exception)
            {
                result = new ImportResult(ImportStatus.Failed, null);
            }

            var page = nav.CurrentPage ?? nav;
            switch (result.Status)
            {
                case ImportStatus.Opened:
                    await nav.PopToRootAsync(false);
                    await nav.PushAsync(new OpeningPage(result.File!));
                    break;
                case ImportStatus.Unsupported:
                    await page.DisplayAlertAsync(L.T("error_unsupported_title"), L.T("error_unsupported_message"), L.T("ok"));
                    break;
                default:
                    await page.DisplayAlertAsync(L.T("error_open_title"), L.T("error_open_message"), L.T("ok"));
                    break;
            }
        }
        finally
        {
            _delivering = false;
        }
    }
}
