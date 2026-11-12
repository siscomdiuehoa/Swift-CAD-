#if SWIFTCAD_SHOTS
using SwiftCad.Core;

namespace SwiftCad.App.Pages;

public partial class ViewerPage
{
    /// <summary>Đưa màn hình xem về trạng thái xác định cho ảnh chụp. Chỉ dùng hình học Demo.</summary>
    internal void ShotSetup(string tool)
    {
        if (_model is null) return;
        switch (tool)
        {
            case "find":
                _query = L.Lang == "vi" ? "phòng" : "phong";
                SelectTool(Tool.Find);
                RunFind(resetIndex: true);
                break;
            case "none":
                break;
            case "measure":
                SelectTool(Tool.Measure);
                _canvas.Fit();
                _canvas.PlaceMeasurePoint(new SnapResult(new Point2(0, 0), SnapKind.Endpoint));
                _canvas.PlaceMeasurePoint(new SnapResult(new Point2(12000, 0), SnapKind.Endpoint));
                break;
            case "layers":
                if (_model.IsDemo) _canvas.Layers.Set(DemoDrawing.LDuct, false);
                SelectTool(Tool.Layers);
                break;
            case "notes":
                foreach (var n in AppState.Notes.Load(_fileKey)) AppState.Notes.Remove(_fileKey, n.Id);
                AppState.Notes.Add(_fileKey, new Point2(3000, 2000), L.Lang == "vi" ? "Kiểm tra van DN100" : "Check valve DN100", DateTimeOffset.UtcNow);
                AppState.Notes.Add(_fileKey, new Point2(10000, 4100), L.Lang == "vi" ? "Miệng gió lệch trục 3" : "Diffuser off grid 3", DateTimeOffset.UtcNow);
                ReloadNotes();
                SelectTool(Tool.Notes);
                break;
            case "fullscreen":
                ToggleFullscreen();
                break;
        }
    }
}
#endif
