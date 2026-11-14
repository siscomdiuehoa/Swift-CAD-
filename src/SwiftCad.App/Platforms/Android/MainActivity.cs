using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace SwiftCad.App;

// SingleTask: mở tệp từ Zalo dùng lại cửa sổ Swift CAD đang chạy (nhận qua OnNewIntent), không tạo thêm bản thứ hai trong Zalo.
[Activity(Name = "com.swiftcad.app.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Hiện Swift CAD trong mục Mở bằng / Chia sẻ khi bấm vào tệp DWG/DXF (Zalo gửi kiểu image/vnd.dwg; ứng dụng khác có thể gửi
// kiểu chung application/octet-stream). Nội dung được kiểm tra trước khi mở; tệp không phải CAD bị từ chối.
[IntentFilter(new[] { Intent.ActionView, Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault },
    DataMimeTypes = new[] {"image/vnd.dwg", "image/x-dwg", "application/acad", "application/x-acad", "application/autocad_dwg",
        "application/dwg", "application/x-dwg", "application/x-autocad", "drawing/x-dwg",
        "image/vnd.dxf", "image/x-dxf", "application/dxf", "application/x-dxf", "drawing/x-dxf", "application/octet-stream" })]
public class MainActivity : MauiAppCompatActivity
{
#if SWIFTCAD_SHOTS
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // adb shell am start -n com.swiftcad.app/com.swiftcad.app.MainActivity --es swiftcad_shot viewer-find
        ShotMode.Requested = Intent?.GetStringExtra("swiftcad_shot");
        ShotMode.TestSession = Intent?.GetStringExtra("swiftcad_session") == "test";
        // adb shell am start ... --ei swiftcad_read_delay_ms 8000 : làm chậm việc đọc tệp để kiểm tra Back giữa chừng.
        SwiftCad.App.Pages.OpeningPage.TestReadDelayMs = Intent?.GetIntExtra("swiftcad_read_delay_ms", 0) ?? 0;
        base.OnCreate(savedInstanceState);
    }
#endif
}
