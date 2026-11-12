using SwiftCad.Cad;
using SwiftCad.Core;

namespace SwiftCad.App.Pages;

/// <summary>
/// Đọc tệp DWG/DXF ở luồng nền, rồi thay chính trang này bằng màn hình xem. Đọc lỗi thì màn hình xem hiện trạng thái lỗi.
/// Nếu người dùng rời trang trong lúc đọc (Back, vuốt quay lại), kết quả bị bỏ và không điều hướng gì thêm.
/// </summary>
public class OpeningPage : ContentPage
{
    private readonly RecentFile _file;
    private bool _started;

#if SWIFTCAD_SHOTS
    /// <summary>Chỉ trong bản dựng kiểm thử: làm chậm việc đọc để kiểm tra bấm Back giữa chừng.</summary>
    public static int TestReadDelayMs;
#endif

    public OpeningPage(RecentFile file)
    {
        _file = file;
        BackgroundColor = Theme.Canvas;
        SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);
        var box = new VerticalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.Center, Padding = 24 };
        box.Add(new ActivityIndicator { IsRunning = true, Color = Colors.White, HorizontalOptions = LayoutOptions.Center });
        var label = Ui.Text(L.F("loading_file", file.DisplayName), Theme.FontBody, Colors.White);
        label.HorizontalTextAlignment = TextAlignment.Center;
        box.Add(label);
        Content = box;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        ImportReport? report = null;
        try
        {
#if SWIFTCAD_SHOTS
            if (TestReadDelayMs > 0) await Task.Delay(TestReadDelayMs);
#endif
            report = await Task.Run(() =>
            {
                using var s = File.OpenRead(_file.LocalPath);
                return CadImporter.Read(s, _file.DisplayName);
            });
        }
        catch (Exception)
        {
            report = null; // ViewerPage hiện trạng thái lỗi.
        }

        try
        {
            // Chỉ thay khi trang này vẫn ở đỉnh ngăn điều hướng (người dùng chưa Back/vuốt quay lại); ngược lại bỏ kết quả.
            // Không dựa vào OnDisappearing vì ứng dụng xuống nền cũng có thể gọi nó trong khi trang vẫn còn.
            var stack = Navigation.NavigationStack;
            if (stack.Count == 0 || !ReferenceEquals(stack[^1], this)) return;
            var viewer = new ViewerPage(_file, report);
            Navigation.InsertPageBefore(viewer, this);
            await Navigation.PopAsync(false);
#if SWIFTCAD_SHOTS
            ShotMode.AfterOpen?.Invoke(viewer);
#endif
        }
        catch (Exception)
        {
            // Điều hướng thất bại (trang đã bị tháo giữa chừng): không làm sập ứng dụng.
        }
    }
}
