using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AUri = Android.Net.Uri;

namespace SwiftCad.App;

/// <summary>
/// Nhận tệp khi người dùng bấm vào DWG/DXF trong Zalo hoặc trình quản lý tệp rồi chọn Swift CAD ở mục Mở bằng
/// (ACTION_VIEW), hoặc chia sẻ tệp sang Swift CAD (ACTION_SEND). Bộ lọc khai báo trên MainActivity.
/// </summary>
public static class IncomingIntent
{
    public static void Handle(Intent? intent, bool restored = false)
    {
        if (intent is null || restored) return;
        // Mở lại từ danh sách ứng dụng gần đây: Android gửi lại intent cũ, không mở lại tệp lần nữa.
        if (intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory)) return;

        AUri? uri = intent.Action switch
        {
            Intent.ActionView => intent.Data,
            Intent.ActionSend => StreamExtra(intent),
            _ => null,
        };
        if (uri is null) return;
        // Đánh dấu đã xử lý để xoay máy hay tạo lại activity không mở lại tệp.
        intent.SetAction(Intent.ActionMain);

        var resolver = Android.App.Application.Context.ContentResolver!;
        IncomingFiles.Enqueue(() => resolver.OpenInputStream(uri), DisplayName(resolver, uri));
    }

    private static AUri? StreamExtra(Intent intent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            return intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(AUri))) as AUri;
#pragma warning disable CA1422 // API cũ cho Android 12 trở xuống
        return intent.GetParcelableExtra(Intent.ExtraStream) as AUri;
#pragma warning restore CA1422
    }

    /// <summary>Tên tệp gốc do ứng dụng gửi cung cấp; không có thì lấy đoạn cuối đường dẫn.</summary>
    private static string? DisplayName(ContentResolver resolver, AUri uri)
    {
        try
        {
            using var c = resolver.Query(uri, [OpenableColumns.DisplayName], null, null, null);
            if (c is not null && c.MoveToFirst() && !c.IsNull(0)) return c.GetString(0);
        }
        catch (Java.Lang.Exception) { }
        return uri.LastPathSegment;
    }
}
