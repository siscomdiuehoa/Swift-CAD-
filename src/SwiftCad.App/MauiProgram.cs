using Microsoft.Maui.LifecycleEvents;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace SwiftCad.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseSkiaSharp();
        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            // Mở bằng / Chia sẻ tệp DWG/DXF từ ứng dụng khác; kết quả chọn thư mục Tải xuống › Zalo.
            events.AddAndroid(android => android
                .OnCreate((activity, saved) => IncomingIntent.Handle(activity.Intent, restored: saved is not null))
                .OnNewIntent((activity, intent) => IncomingIntent.Handle(intent))
                .OnActivityResult((activity, requestCode, resultCode, data) => DeviceCadFiles.OnActivityResult(requestCode, resultCode, data)));
#elif IOS
            events.AddiOS(ios => ios
                .SceneWillConnect((scene, session, options) => IncomingUrls.Handle(options.UrlContexts))
                // SceneOpenUrl phải trả bool: true = ứng dụng đã nhận xử lý URL.
                .SceneOpenUrl((scene, contexts) => { IncomingUrls.Handle(contexts); return true; }));
#endif
        });
        return builder.Build();
    }
}
