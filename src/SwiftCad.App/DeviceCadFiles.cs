using SwiftCad.Core;
#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
using AUri = Android.Net.Uri;
#endif

namespace SwiftCad.App;

/// <summary>
/// Danh sách tệp DWG/DXF trong các thư mục người dùng cho phép (ví dụ Tải xuống › Zalo), mới tải về đứng đầu.
/// Android: quyền đọc thư mục qua trình chọn thư mục hệ thống (Storage Access Framework), giữ qua các lần mở ứng dụng;
/// không xin quyền đọc toàn bộ bộ nhớ. Chỉ đọc, không sửa hay xóa tệp trong thư mục.
/// iOS: không có thư mục Tải xuống dùng chung; tệp đến từ Zalo qua mục Mở bằng (xem IncomingFiles).
/// </summary>
public static class DeviceCadFiles
{
    /// <summary>Tối đa số tệp hiện trên màn hình chính.</summary>
    public const int MaxShown = 30;

#if ANDROID
    public static bool IsSupported => true;

    private const int RequestTree = 0x5C1D;
    private const int MaxDepth = 3;
    private const int MaxEntries = 5000;
    private const string ExternalStorageAuthority = "com.android.externalstorage.documents";
    private static TaskCompletionSource<AUri?>? _pick;

    private static ContentResolver Resolver => Android.App.Application.Context.ContentResolver!;

    /// <summary>Các thư mục đã được cấp quyền đọc (giữ qua các lần mở ứng dụng).</summary>
    public static IReadOnlyList<(string Uri, string Name)> Folders() =>
        Resolver.PersistedUriPermissions
            .Where(p => p.IsReadPermission && p.Uri is not null && DocumentsContract.IsTreeUri(p.Uri))
            .Select(p => (p.Uri!.ToString()!, FolderName(p.Uri!)))
            .ToList();

    private static string FolderName(AUri tree)
    {
        var id = DocumentsContract.GetTreeDocumentId(tree) ?? "";
        var path = id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id;
        var name = path.TrimEnd('/');
        name = name.Contains('/') ? name[(name.LastIndexOf('/') + 1)..] : name;
        return name.Length > 0 ? name : id;
    }

    /// <summary>
    /// Mở trình chọn thư mục hệ thống, gợi ý sẵn Tải xuống › Zalo. Trả true khi người dùng cấp quyền.
    /// Android 11 trở lên không cho chọn thẳng thư mục gốc Tải xuống, nên hướng dẫn chọn thư mục con Zalo.
    /// </summary>
    public static async Task<bool> AddFolderAsync()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null) return false;
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        intent.PutExtra(DocumentsContract.ExtraInitialUri, DocumentsContract.BuildDocumentUri(ExternalStorageAuthority, "primary:Download/Zalo"));
        _pick?.TrySetResult(null);
        _pick = new TaskCompletionSource<AUri?>();
        activity.StartActivityForResult(intent, RequestTree);
        var uri = await _pick.Task;
        if (uri is null) return false;
        try
        {
            Resolver.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
            return true;
        }
        catch (Java.Lang.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Gọi từ MainActivity.OnActivityResult.</summary>
    public static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestTree) return;
        _pick?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
    }

    public static void RemoveFolder(string uri)
    {
        try { Resolver.ReleasePersistableUriPermission(AUri.Parse(uri)!, ActivityFlags.GrantReadUriPermission); }
        catch (Java.Lang.SecurityException) { }
    }

    /// <summary>Quét các thư mục đã cấp quyền (kể cả thư mục con tới 3 cấp). Chạy ở luồng nền.</summary>
    public static Task<List<DeviceCadFile>> ScanAsync() => Task.Run(() =>
    {
        var found = new List<DeviceCadFile>();
        var budget = MaxEntries;
        foreach (var (uriText, name) in Folders())
        {
            try
            {
                var tree = AUri.Parse(uriText)!;
                Walk(tree, DocumentsContract.GetTreeDocumentId(tree)!, name, 0, found, ref budget);
            }
            catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
            {
                // Thư mục đã bị xóa hoặc quyền bị thu hồi: bỏ qua, các thư mục khác vẫn hiện.
            }
        }
        return CadFiles.NewestFirst(found, MaxShown);
    });

    private static void Walk(AUri tree, string docId, string folder, int depth, List<DeviceCadFile> found, ref int budget)
    {
        var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, docId)!;
        string[] cols =
        [
            DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName,
            DocumentsContract.Document.ColumnMimeType, DocumentsContract.Document.ColumnSize,
            DocumentsContract.Document.ColumnLastModified,
        ];
        var subdirs = new List<(string Id, string Name)>();
        using (var c = Resolver.Query(children, cols, null, null, null))
        {
            if (c is null) return;
            while (c.MoveToNext() && budget-- > 0)
            {
                var id = c.GetString(0) ?? "";
                var name = c.GetString(1) ?? "";
                var mime = c.GetString(2) ?? "";
                if (mime == DocumentsContract.Document.MimeTypeDir)
                {
                    if (depth < MaxDepth && id.Length > 0) subdirs.Add((id, name));
                    continue;
                }
                if (!FileIdentity.IsSupportedExtension(name)) continue;
                var size = c.IsNull(3) ? 0 : c.GetLong(3);
                var modified = c.IsNull(4) ? DateTimeOffset.MinValue : DateTimeOffset.FromUnixTimeMilliseconds(c.GetLong(4));
                var uri = DocumentsContract.BuildDocumentUriUsingTree(tree, id)!.ToString()!;
                found.Add(new DeviceCadFile(name, size, modified, uri, folder));
            }
        }
        foreach (var (id, name) in subdirs)
            if (budget > 0) Walk(tree, id, name, depth + 1, found, ref budget);
    }

    public static Stream? Open(DeviceCadFile file) => Resolver.OpenInputStream(AUri.Parse(file.Uri)!);
#else
    public static bool IsSupported => false;
    public static IReadOnlyList<(string Uri, string Name)> Folders() => [];
    public static Task<bool> AddFolderAsync() => Task.FromResult(false);
    public static void RemoveFolder(string uri) { }
    public static Task<List<DeviceCadFile>> ScanAsync() => Task.FromResult(new List<DeviceCadFile>());
    public static Stream? Open(DeviceCadFile file) => null;
#endif
}
