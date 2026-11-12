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
    private static readonly FilePickerFileType CadTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        // Android thường không có MIME riêng cho DWG nên cho chọn mọi loại rồi kiểm tra phần mở rộng.
        [DevicePlatform.Android] = ["*/*"],
        [DevicePlatform.iOS] = ["com.autodesk.dwg", "com.autodesk.dxf", "public.data"],
    });

    /// <summary>
    /// Mở trình chọn tệp hệ thống, sao chép tệp đã chọn vào vùng dữ liệu của ứng dụng để mở lại được
    /// khi mất quyền truy cập nguồn, rồi ghi vào danh sách Gần đây. Hủy thì không thay đổi gì.
    /// </summary>
    public static async Task<ImportResult> PickAndImportAsync()
    {
        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = L.T("picker_title"), FileTypes = CadTypes });
        }
        catch (Exception)
        {
            return new ImportResult(ImportStatus.Failed, null);
        }
        if (picked is null) return new ImportResult(ImportStatus.Cancelled, null);
        if (!FileIdentity.IsSupportedExtension(picked.FileName)) return new ImportResult(ImportStatus.Unsupported, null);

        try
        {
            Directory.CreateDirectory(AppState.DrawingsDir);
            var temp = Path.Combine(AppState.DrawingsDir, Guid.NewGuid().ToString("N") + ".tmp");
            await using (var src = await picked.OpenReadAsync())
            await using (var dst = File.Create(temp))
                await src.CopyToAsync(dst);

            string key;
            await using (var s = File.OpenRead(temp)) key = FileIdentity.ComputeKey(s);
            var final = Path.Combine(AppState.DrawingsDir, key + Path.GetExtension(picked.FileName).ToLowerInvariant());
            File.Move(temp, final, overwrite: true);

            var entry = AppState.Recents.Touch(new RecentFile
            {
                DisplayName = picked.FileName,
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
    }

    public static async Task ShareCopyAsync(RecentFile file)
    {
        if (!File.Exists(file.LocalPath)) return;
        await Share.Default.RequestAsync(new ShareFileRequest { Title = file.DisplayName, File = new ShareFile(file.LocalPath) { FileName = file.DisplayName } });
    }
}
