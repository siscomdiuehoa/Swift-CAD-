using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using SwiftCad.Core;
using TextEntity = ACadSharp.Entities.TextEntity;

namespace SwiftCad.Cad;

/// <summary>
/// Xuất BẢN SAO của bản vẽ gốc có kèm ghi chú của ứng dụng, thành các thực thể CAD trên layer SWIFTCAD_NOTES.
/// Tệp gốc không bị sửa. Bản sao được ghi lại bằng ACadSharp, nên cần kiểm tra lại trên AutoCAD trước khi phát hành.
/// </summary>
public static class DrawingNoteExporter
{
    public const string NotesLayer = "SWIFTCAD_NOTES";

    /// <summary>Đọc tệp gốc (DWG hoặc DXF), thêm ghi chú, ghi ra đường dẫn đích (DWG).</summary>
    public static ExportResult Export(string sourcePath, IReadOnlyList<Note> notes, string targetPath)
    {
        // Thử DWG trước; nếu bản vẽ có thực thể mà bộ ghi DWG chưa hỗ trợ thì ghi DXF (AutoCAD mở được).
        var asDwg = Path.ChangeExtension(targetPath, ".dwg");
        try { return ExportTo(sourcePath, notes, asDwg, dxf: false); }
        catch (Exception)
        {
            if (File.Exists(asDwg)) try { File.Delete(asDwg); } catch (IOException) { }
            var asDxf = Path.ChangeExtension(targetPath, ".dxf");
            return ExportTo(sourcePath, notes, asDxf, dxf: true);
        }
    }

    private static ExportResult ExportTo(string sourcePath, IReadOnlyList<Note> notes, string targetPath, bool dxf)
    {
        if (notes.Count == 0) return new ExportResult(0, "Không có ghi chú để xuất.");
        var doc = ReadDocument(sourcePath);
        var layer = doc.Layers.FirstOrDefault(l => l.Name == NotesLayer);
        if (layer is null)
        {
            layer = new Layer(NotesLayer) { Color = new ACadSharp.Color(1) }; // đỏ
            doc.Layers.Add(layer);
        }
        var h = TextHeight(doc.Header.InsUnits);
        var added = 0;
        foreach (var n in notes)
        {
            foreach (var e in Build(n, h)) { e.Layer = layer; doc.Entities.Add(e); added++; }
        }
        if (dxf) DxfWriter.Write(targetPath, doc);
        else DwgWriter.Write(targetPath, doc);
        return new ExportResult(added, null);
    }

    /// <summary>Đọc lại bản đã xuất để kiểm tra: số thực thể trên layer ghi chú.</summary>
    public static int CountNoteEntities(string path)
    {
        var doc = ReadDocument(path);
        return doc.Entities.Count(e => e.Layer?.Name == NotesLayer);
    }

    private static CadDocument ReadDocument(string path)
    {
        using var fs = File.OpenRead(path);
        var head = new byte[6];
        fs.ReadExactly(head, 0, 6);
        fs.Position = 0;
        var isDwg = head[0] == 'A' && head[1] == 'C';
        return isDwg ? DwgReader.Read(fs) : DxfReader.Read(fs);
    }

    /// <summary>Chiều cao chữ theo đơn vị bản vẽ (khoảng 2,5 mm ở tỷ lệ 1:100 trên giấy).</summary>
    public static double TextHeight(UnitsType units) => units switch
    {
        UnitsType.Meters => 0.25,
        UnitsType.Centimeters => 25,
        UnitsType.Inches => 10,
        UnitsType.Feet => 1,
        _ => 250,
    };

    private static double Dist(XYZ p, XYZ q) => Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));

    public static IEnumerable<ACadSharp.Entities.Entity> Build(Note n, double h)
    {
        var a = new XYZ(n.X, n.Y, 0);
        switch (n.Kind)
        {
            case NoteKind.Pin:
            case NoteKind.Text:
                yield return new TextEntity { Value = n.Text, InsertPoint = a, Height = h };
                break;
            case NoteKind.Arrow:
                {
                    var b = new XYZ(n.X2, n.Y2, 0);
                    yield return new Line { StartPoint = a, EndPoint = b };
                    // Đầu mũi tên: hai nhánh 25° lùi về phía đuôi, dài bằng 0,8 chiều cao chữ.
                    var len = Math.Max(h * 0.8, Dist(a, b) * 0.1);
                    var dir = Math.Atan2(b.Y - a.Y, b.X - a.X);
                    foreach (var s in new[] { 25.0, -25.0 })
                    {
                        var t = dir + Math.PI - s * Math.PI / 180;
                        yield return new Line { StartPoint = b, EndPoint = new XYZ(b.X + len * Math.Cos(t), b.Y + len * Math.Sin(t), 0) };
                    }
                    if (n.Text.Length > 0) yield return new TextEntity { Value = n.Text, InsertPoint = a, Height = h };
                    break;
                }
            case NoteKind.Cloud:
                {
                    // Khung khoanh vùng: đa giác bốn đỉnh theo chiều ngược kim đồng hồ, cạnh là cung lồi (bulge 1) để giống đám mây.
                    var x1 = Math.Min(n.X, n.X2); var x2 = Math.Max(n.X, n.X2);
                    var y1 = Math.Min(n.Y, n.Y2); var y2 = Math.Max(n.Y, n.Y2);
                    var pl = new LwPolyline(
                        new LwPolyline.Vertex(x1, y1) { Bulge = 1 },
                        new LwPolyline.Vertex(x2, y1) { Bulge = 1 },
                        new LwPolyline.Vertex(x2, y2) { Bulge = 1 },
                        new LwPolyline.Vertex(x1, y2) { Bulge = 1 })
                    { IsClosed = true };
                    yield return pl;
                    if (n.Text.Length > 0) yield return new TextEntity { Value = n.Text, InsertPoint = new XYZ(x1, y2 + h * 0.3, 0), Height = h };
                    break;
                }
        }
    }
}

public sealed record ExportResult(int EntitiesAdded, string? Error);
