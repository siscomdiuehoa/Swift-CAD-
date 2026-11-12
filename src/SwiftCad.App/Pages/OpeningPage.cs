using SwiftCad.Cad;
using SwiftCad.Core;

namespace SwiftCad.App.Pages;

/// <summary>Đọc tệp DWG/DXF ở luồng nền, rồi thay bằng màn hình xem. Đọc lỗi thì màn hình xem hiện trạng thái lỗi.</summary>
public class OpeningPage : ContentPage
{
    private readonly RecentFile _file;
    private bool _started;

    public OpeningPage(RecentFile file)
    {
        _file = file;
        BackgroundColor = Theme.Canvas;
        SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);
        var box = new VerticalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.Center, Padding = 24 };
        box.Add(new ActivityIndicator { IsRunning = true, Color = Colors.White, HorizontalOptions = LayoutOptions.Center });
        var label = Ui.Text(L.F("loading_file", file.DisplayName), Theme.FontBody, Colors.White);
        label.HorizontalTextAlignment = TextAlignment.Center;
        box.Add(label);
        Content = box;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        ImportReport? report = null;
        try
        {
            report = await Task.Run(() =>
            {
                using var s = File.OpenRead(_file.LocalPath);
                return CadImporter.Read(s, _file.DisplayName);
            });
        }
        catch (Exception)
        {
            report = null; // ViewerPage hiện trạng thái lỗi.
        }
        var viewer = new ViewerPage(_file, report);
        Navigation.InsertPageBefore(viewer, this);
        await Navigation.PopAsync(false);
#if SWIFTCAD_SHOTS
        ShotMode.AfterOpen?.Invoke(viewer);
#endif
    }
}
