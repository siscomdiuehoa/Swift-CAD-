using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SwiftCad.App;

[Activity(Name = "com.swiftcad.app.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
#if SWIFTCAD_SHOTS
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // adb shell am start -n com.swiftcad.app/com.swiftcad.app.MainActivity --es swiftcad_shot viewer-find
        ShotMode.Requested = Intent?.GetStringExtra("swiftcad_shot");
        // adb shell am start ... --ei swiftcad_read_delay_ms 8000 : làm chậm việc đọc tệp để kiểm tra Back giữa chừng.
        SwiftCad.App.Pages.OpeningPage.TestReadDelayMs = Intent?.GetIntExtra("swiftcad_read_delay_ms", 0) ?? 0;
        base.OnCreate(savedInstanceState);
    }
#endif
}
