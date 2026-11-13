using SwiftCad.Core;

namespace SwiftCad.App;

/// <summary>Trạng thái và kho dữ liệu dùng chung. Mọi thứ lưu trong vùng dữ liệu riêng của ứng dụng.</summary>
public static class AppState
{
    public static AppSettings Settings { get; private set; } = new();
    public static RecentStore Recents { get; private set; } = null!;
    public static NoteStore Notes { get; private set; } = null!;
    private static SettingsStore _settingsStore = null!;

    public static string DrawingsDir => Path.Combine(FileSystem.AppDataDirectory, "drawings");

    public static void Init()
    {
        var dir = FileSystem.AppDataDirectory;
        _settingsStore = new SettingsStore(Path.Combine(dir, "settings.json"));
        Recents = new RecentStore(Path.Combine(dir, "recent.json"));
        Notes = new NoteStore(Path.Combine(dir, "notes"));
        Settings = _settingsStore.Load();
        L.Apply(Settings.Language);
    }

    public static void SaveSettings() => _settingsStore.Save(Settings);
}

public enum ImportStatus { Opened, Cancelled, Unsupported, Failed }

public readonly record struct ImportResult(ImportStatus Status, RecentFile? File);

public static class Files
{
    /// <summary>
    /// Trình chọn tệp chỉ hiện tệp AutoCAD. Android: lọc theo các kiểu MIME của DWG/DXF (kèm kiểu octet-stream cho máy
    /// chưa biết đuôi .dwg; PDF, Word, ảnh, APK bị ẩn). iOS: kiểu tài liệu DWG/DXF khai báo trong Info.plist.
    /// </summary>
    private static readonly FilePickerFileType CadTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = CadFiles.AndroidMimeTypes,
        [DevicePlatform.iOS] = ["com.autodesk.dwg", "com.autodesk.dxf"],
    });

    /// <summary>Phương án dự phòng khi máy gán kiểu lạ cho tệp DWG: hiện mọi tệp, vẫn kiểm tra nội dung trước khi mở.</summary>
    private static readonly FilePickerFileType AnyTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = ["*/*"],
        [DevicePlatform.iOS] = ["public.data"],
    });

    /// <summary>
    /// Mở trình chọn tệp hệ thống, sao chép tệp đã chọn vào vùng dữ liệu của ứng dụng để mở lại được
    /// khi mất quyền truy cập nguồn, rồi ghi vào danh sách Gần đây. Hủy thì không thay đổi gì.
    /// </summary>
    public static async Task<ImportResult> PickAndImportAsync(bool anyType = false)
    {
        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = L.T("picker_title"), FileTypes = anyType ? AnyTypes : CadTypes });
        }
        catch (Exception)
        {
            return new ImportResult(ImportStatus.Failed, null);
        }
        if (picked is null) return new ImportResult(ImportStatus.Cancelled, null);
        try
        {
            await using var src = await picked.OpenReadAsync();
            return await ImportAsync(src, picked.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ImportResult(ImportStatus.Failed, null);
        }
    }

    /// <summary>
    /// Sao chép một luồng tệp (từ trình chọn tệp, thư mục đã cấp quyền, hoặc ứng dụng khác gửi sang) vào vùng dữ liệu
    /// của ứng dụng. Nhận diện theo nội dung: tệp không có đuôi nhưng đúng DWG/DXF vẫn mở được; tệp khác bị từ chối.
    /// Tệp gốc không bị sửa.
    /// </summary>
    public static async Task<ImportResult> ImportAsync(Stream src, string? suggestedName)
    {
        string? temp = null;
        try
        {
            Directory.CreateDirectory(AppState.DrawingsDir);
            temp = Path.Combine(AppState.DrawingsDir, Guid.NewGuid().ToString("N") + ".tmp");
            await using (var dst = File.Create(temp))
                await src.CopyToAsync(dst);

            var head = new byte[CadFiles.HeadLength];
            int read;
            await using (var s = File.OpenRead(temp)) read = await s.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
            var name = CadFiles.ResolveName(suggestedName, head.AsSpan(0, read));
            if (name is null)
            {
                File.Delete(temp);
                return new ImportResult(ImportStatus.Unsupported, null);
            }

            string key;
            await using (var s = File.OpenRead(temp)) key = FileIdentity.ComputeKey(s);
            var final = Path.Combine(AppState.DrawingsDir, key + Path.GetExtension(name).ToLowerInvariant());
            File.Move(temp, final, overwrite: true);
            temp = null;

            var entry = AppState.Recents.Touch(new RecentFile
            {
                DisplayName = name,
                Source = FileSource.Device,
                LocalPath = final,
                SizeBytes = new FileInfo(final).Length,
                FileKey = key,
            }, DateTimeOffset.UtcNow);
            return new ImportResult(ImportStatus.Opened, entry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ImportResult(ImportStatus.Failed, null);
        }
        finally
        {
            if (temp is not null) try { File.Delete(temp); } catch (IOException) { }
        }
    }

    public static async Task ShareCopyAsync(RecentFile file)
    {
        if (!File.Exists(file.LocalPath)) return;
        await Share.Default.RequestAsync(new ShareFileRequest { Title = file.DisplayName, File = new ShareFile(file.LocalPath) { FileName = file.DisplayName } });
    }
}
