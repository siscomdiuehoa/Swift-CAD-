using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCad.Core;

public sealed class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.Device;
    public bool OnboardingDone { get; set; }
    /// <summary>Unknown = hiển thị theo đơn vị của bản vẽ.</summary>
    public DrawingUnit DisplayUnit { get; set; } = DrawingUnit.Unknown;
    public bool BlackCanvas { get; set; }
}

public enum FileSource { Demo = 0, Device }

public sealed class RecentFile
{
    public string Id { get; set; } = "";
    /// <summary>Tên tệp gốc, giữ nguyên, không dịch.</summary>
    public string DisplayName { get; set; } = "";
    public FileSource Source { get; set; }
    /// <summary>Đường dẫn bản sao trong vùng dữ liệu của ứng dụng. Rỗng với tệp Demo.</summary>
    public string LocalPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset LastOpenedUtc { get; set; }
    /// <summary>Đánh dấu "Đã lưu" là danh sách đánh dấu, không có nghĩa đã tải ngoại tuyến.</summary>
    public bool Bookmarked { get; set; }
    /// <summary>Khóa nội dung (SHA-256) gắn ghi chú với đúng phiên bản tệp.</summary>
    public string FileKey { get; set; } = "";
}

/// <summary>
/// Loại ghi chú. Pin: ghim đánh số (bản cũ). Text: chữ hiện ngay trên bản vẽ.
/// Arrow: mũi tên từ (X, Y) chỉ tới (X2, Y2), chữ đặt ở đầu (X, Y). Cloud: khoanh vùng hình chữ nhật dạng đám mây.
/// </summary>
public enum NoteKind { Pin = 0, Text, Arrow, Cloud }

public sealed class Note
{
    public string Id { get; set; } = "";
    /// <summary>Thiếu trong tệp ghi chú cũ thì mặc định Pin.</summary>
    public NoteKind Kind { get; set; } = NoteKind.Pin;
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Điểm thứ hai: đầu mũi tên hoặc góc đối của vùng khoanh. Bằng (X, Y) với Pin và Text.</summary>
    public double X2 { get; set; }
    public double Y2 { get; set; }
    /// <summary>Bắt buộc với Pin và Text; có thể rỗng với Arrow và Cloud.</summary>
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }

    [JsonIgnore]
    public bool HasSecondPoint => Kind is NoteKind.Arrow or NoteKind.Cloud;

    /// <summary>Điểm đại diện để đưa ghi chú vào giữa khung nhìn.</summary>
    [JsonIgnore]
    public Point2 Anchor => HasSecondPoint ? new Point2((X + X2) / 2, (Y + Y2) / 2) : new Point2(X, Y);
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<RecentFile>))]
[JsonSerializable(typeof(List<Note>))]
internal sealed partial class StorageJson : JsonSerializerContext;

internal static class JsonFile
{
    public static T? Read<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var s = File.OpenRead(path);
            return JsonSerializer.Deserialize(s, info);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Tệp hỏng hoặc không đọc được: coi như chưa có dữ liệu, không làm sập ứng dụng.
            return null;
        }
    }

    /// <summary>Ghi qua tệp tạm rồi thay thế, tránh mất dữ liệu khi ứng dụng bị tắt giữa chừng.</summary>
    public static void Write<T>(string path, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var s = File.Create(tmp)) JsonSerializer.Serialize(s, value, info);
        File.Move(tmp, path, overwrite: true);
    }
}

public sealed class SettingsStore(string path)
{
    public AppSettings Load() => JsonFile.Read(path, StorageJson.Default.AppSettings) ?? new AppSettings();
    public void Save(AppSettings s) => JsonFile.Write(path, s, StorageJson.Default.AppSettings);
}

public sealed class RecentStore(string path)
{
    public List<RecentFile> Load() =>
        (JsonFile.Read(path, StorageJson.Default.ListRecentFile) ?? [])
            .OrderByDescending(r => r.LastOpenedUtc).ToList();

    private void Save(List<RecentFile> items) => JsonFile.Write(path, items, StorageJson.Default.ListRecentFile);

    /// <summary>Thêm mới hoặc cập nhật lần mở. Trùng khi cùng nguồn và cùng khóa nội dung.</summary>
    public RecentFile Touch(RecentFile file, DateTimeOffset now)
    {
        var items = Load();
        var existing = items.FirstOrDefault(r => r.Source == file.Source && r.FileKey == file.FileKey && r.FileKey.Length > 0);
        if (existing is null)
        {
            if (file.Id.Length == 0) file.Id = Guid.NewGuid().ToString("N");
            file.LastOpenedUtc = now;
            items.Add(file);
            existing = file;
        }
        else
        {
            existing.LastOpenedUtc = now;
            existing.DisplayName = file.DisplayName;
            if (file.LocalPath.Length > 0) existing.LocalPath = file.LocalPath;
            if (file.SizeBytes > 0) existing.SizeBytes = file.SizeBytes;
        }
        Save(items);
        return existing;
    }

    public void Remove(string id)
    {
        var items = Load();
        if (items.RemoveAll(r => r.Id == id) > 0) Save(items);
    }

    public bool ToggleBookmark(string id)
    {
        var items = Load();
        var it = items.FirstOrDefault(r => r.Id == id);
        if (it is null) return false;
        it.Bookmarked = !it.Bookmarked;
        Save(items);
        return it.Bookmarked;
    }

    public static IEnumerable<RecentFile> Filter(IEnumerable<RecentFile> items, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return items;
        var q = TextSearch.Fold(query);
        return items.Where(r => TextSearch.Fold(r.DisplayName).Contains(q, StringComparison.Ordinal));
    }
}

/// <summary>Ghi chú lưu thành tệp JSON riêng theo khóa nội dung của bản vẽ. Không bao giờ ghi vào tệp gốc.</summary>
public sealed class NoteStore(string directory)
{
    private string PathFor(string fileKey)
    {
        foreach (var c in fileKey)
            if (!(char.IsAsciiLetterOrDigit(c) || c == '-')) throw new ArgumentException("Khóa tệp không hợp lệ.", nameof(fileKey));
        return Path.Combine(directory, fileKey + ".notes.json");
    }

    public List<Note> Load(string fileKey) => JsonFile.Read(PathFor(fileKey), StorageJson.Default.ListNote) ?? [];

    /// <summary>Ghim đánh số tại một điểm (kiểu ghi chú của bản đầu).</summary>
    public Note Add(string fileKey, Point2 at, string text, DateTimeOffset now) =>
        Add(fileKey, NoteKind.Pin, at, at, text, now);

    /// <summary>
    /// Thêm ghi chú. Chữ, Ghim cần có nội dung; mũi tên và vùng khoanh được để trống chữ.
    /// Mũi tên hoặc vùng khoanh có hai điểm trùng nhau bị từ chối (không có hình để vẽ).
    /// </summary>
    public Note Add(string fileKey, NoteKind kind, Point2 a, Point2 b, string? text, DateTimeOffset now)
    {
        var t = (text ?? "").Trim();
        if (kind is NoteKind.Pin or NoteKind.Text && t.Length == 0) throw new ArgumentException("Ghi chú chữ cần có nội dung.", nameof(text));
        if (kind is NoteKind.Arrow or NoteKind.Cloud && a.DistanceTo(b) <= 0) throw new ArgumentException("Cần hai điểm khác nhau.", nameof(b));
        if (kind is NoteKind.Pin or NoteKind.Text) b = a;
        var notes = Load(fileKey);
        var n = new Note { Id = Guid.NewGuid().ToString("N"), Kind = kind, X = a.X, Y = a.Y, X2 = b.X, Y2 = b.Y, Text = t, CreatedUtc = now };
        notes.Add(n);
        JsonFile.Write(PathFor(fileKey), notes, StorageJson.Default.ListNote);
        return n;
    }

    public void Remove(string fileKey, string noteId)
    {
        var notes = Load(fileKey);
        if (notes.RemoveAll(n => n.Id == noteId) > 0)
            JsonFile.Write(PathFor(fileKey), notes, StorageJson.Default.ListNote);
    }
}

public static class FileIdentity
{
    /// <summary>Khóa nội dung SHA-256 dạng hex thường. Tệp thay đổi nội dung thì khóa đổi theo.</summary>
    public static string ComputeKey(Stream content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexStringLower(hash);
    }

    public static bool IsSupportedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext.Equals(".dwg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dxf", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatSize(long bytes, System.Globalization.CultureInfo culture)
    {
        if (bytes < 1024) return bytes.ToString("N0", culture) + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("N0", culture) + " KB";
        return (bytes / (1024.0 * 1024.0)).ToString("N1", culture) + " MB";
    }
}
