using SkiaSharp;
using SwiftCad.Core;

namespace SwiftCad.App.Pages;

public enum Tool { None, Find, Measure, Layers, Notes }

/// <summary>
/// Màn hình xem. Với tệp mẫu: hình học Demo do ứng dụng tự tạo, các công cụ Tìm/Đo/Layer/Ghi chú chạy thật trên hình học đó.
/// Với tệp DWG/DXF của người dùng: bản này CHƯA đọc nội dung, chỉ hiện trạng thái trung thực.
/// </summary>
public partial class ViewerPage : ContentPage
{
    private readonly RecentFile? _file;
    private readonly SwiftCad.Cad.ImportReport? _report;
    private readonly DrawingModel? _model;
    private readonly string _fileKey;
    private readonly string _name;

    private readonly CadCanvas _canvas = new();
    private readonly Grid _root = new();
    private readonly View _topBar;
    private readonly SoftSurface _toolbar;
    private readonly SoftSurface _panel = new(null, Theme.RadiusBlock, new Thickness(12)) { IsVisible = false, MaximumHeightRequest = 264 };
    private readonly Dictionary<Tool, ToolButton> _toolButtons = new();
    private readonly Grid _fullscreenHost = new();
    private Tool _tool = Tool.None;
    private bool _fullscreen;
    private bool _landscape;

    private string _query = "";
    private Label? _findCount;
    private Label? _measureResult, _measureHint;
    private DrawingUnit _sourceUnit;
    private string _layerFilter = "";

    /// <summary>Mở tệp mẫu Demo.</summary>
    public ViewerPage() : this(null) { }

    /// <summary>Tệp của người dùng: report là kết quả đọc; null khi đọc lỗi (hiện trạng thái lỗi, không vẽ hình thay thế).</summary>
    public ViewerPage(RecentFile? file, SwiftCad.Cad.ImportReport? report = null)
    {
        _file = file;
        _report = report;
        _model = file is null ? DemoDrawing.Create() : report?.Model;
        _fileKey = file?.FileKey ?? DemoDrawing.FileKey;
        _name = file?.DisplayName ?? DemoDrawing.FileName;
        _sourceUnit = _model?.Unit ?? DrawingUnit.Unknown;

        BackgroundColor = Theme.Bg;
        SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);

        _topBar = BuildTopBar();
        _toolbar = BuildToolbar();

        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _root.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0)));
        _root.Add(_topBar, 0, 0);
        _canvasArea = BuildCanvasArea();
        _root.Add(_canvasArea, 0, 1);
        _panel.Margin = new Thickness(12, 8, 12, 0);
        _root.Add(_panel, 0, 2);
        _root.Add(_toolbar, 0, 3);
        Content = _root;

        if (_model is not null)
        {
            _canvas.CanvasColor = AppState.Settings.BlackCanvas ? SKColors.Black : new SKColor(0x11, 0x1C, 0x25);
            _canvas.SetModel(_model);
            // Layer đang tắt hoặc đóng băng trong tệp gốc thì ẩn khi mở.
            foreach (var l in _model.Layers.Where(l => !l.VisibleByDefault)) _canvas.Layers.Set(l.Name, false);
            _canvas.Notes = AppState.Notes.Load(_fileKey);
            _canvas.MeasureChanged += UpdateMeasure;
            _canvas.AimChanged += UpdateAim;
            _canvas.NoteRequested += p => _ = AddNoteAsync(p);
            _canvas.ShapeDrawn += (kind, a, b) => _ = AddShapeAsync(kind, a, b);
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;
        var landscape = width > height;
        var rotated = landscape != _landscape;
        _landscape = landscape;
        // Bảng công cụ không chiếm quá khoảng một phần ba màn hình, để vùng CAD luôn lớn nhất.
        // Màn hình ngang còn thấp hơn. Camera do CadCanvas tự giữ.
        _panelMax = landscape ? Math.Max(120, height * 0.62) : Math.Min(280, height * 0.34);
        _panel.MaximumHeightRequest = _panelMax + 24;
        if (rotated) ApplyLayout(landscape);
        if (rotated && _tool != Tool.None) SelectTool(_tool);
    }

    private double _panelMax = 240;
    private View _canvasArea = null!;

    /// <summary>
    /// Dọc: thanh trên, vùng CAD, bảng công cụ, thanh công cụ xếp chồng.
    /// Ngang: vùng CAD chiếm toàn bộ bề ngang và chiều cao dưới thanh trên. Thanh công cụ thành cột dọc nổi bên phải,
    /// bảng công cụ nổi cạnh nó, nút Vừa khung/Toàn màn hình chuyển sang góc dưới trái (Owner 13/11: bản vẽ phải full màn).
    /// </summary>
    private void ApplyLayout(bool landscape)
    {
        _root.ColumnDefinitions[1].Width = new GridLength(0);
        Grid.SetColumnSpan(_topBar, 1);
        Grid.SetRow(_canvasArea, 1); Grid.SetColumn(_canvasArea, 0); Grid.SetRowSpan(_canvasArea, landscape ? 3 : 1);

        // Thanh công cụ: hàng ngang khi dọc, cột dọc khi ngang.
        _toolGrid.ColumnDefinitions.Clear(); _toolGrid.RowDefinitions.Clear();
        var i = 0;
        foreach (var b in _toolButtons.Values)
        {
            if (landscape) { _toolGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); Grid.SetRow(b, i); Grid.SetColumn(b, 0); }
            else { _toolGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); Grid.SetColumn(b, i); Grid.SetRow(b, 0); }
            i++;
        }
        if (landscape) _toolGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        else _toolGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _toolGrid.RowSpacing = landscape ? 2 : 0;

        Grid.SetRow(_toolbar, landscape ? 1 : 3); Grid.SetRowSpan(_toolbar, landscape ? 3 : 1); Grid.SetColumn(_toolbar, 0);
        _toolbar.HorizontalOptions = landscape ? LayoutOptions.End : LayoutOptions.Fill;
        _toolbar.VerticalOptions = landscape ? LayoutOptions.Center : LayoutOptions.Fill;
        _toolbar.WidthRequest = landscape ? ToolbarWidthLandscape : -1;
        _toolbar.Margin = landscape ? new Thickness(0, 8, 12, 8) : new Thickness(12, 8, 12, 12);

        Grid.SetRow(_panel, landscape ? 1 : 2); Grid.SetRowSpan(_panel, landscape ? 3 : 1); Grid.SetColumn(_panel, 0);
        _panel.HorizontalOptions = landscape ? LayoutOptions.End : LayoutOptions.Fill;
        _panel.VerticalOptions = landscape ? LayoutOptions.Start : LayoutOptions.Fill;
        _panel.WidthRequest = landscape ? 340 : -1;
        _panel.Margin = landscape ? new Thickness(12, 12, ToolbarWidthLandscape + 20, 12) : new Thickness(12, 8, 12, 0);

        if (_canvasButtons is not null)
            _canvasButtons.HorizontalOptions = landscape ? LayoutOptions.Start : LayoutOptions.End;
        Dispatcher.Dispatch(() => _canvas.InvalidateSurface());
    }

    private const double ToolbarWidthLandscape = 88;
    private Grid _toolGrid = null!;
    private HorizontalStackLayout? _canvasButtons;

    /// <summary>Cuộn khi nội dung dài; chiều cao bằng nội dung, không vượt quá giới hạn bảng.</summary>
    private View Scrollable(View content)
    {
        var sv = new ScrollView { Content = content };
        // Đo chiều cao nội dung khi không giới hạn (Height của nội dung trong ScrollView có thể bằng cả khung).
        void Fit()
        {
            var w = sv.Width > 0 ? sv.Width : _panel.Width - 24;
            if (w <= 0) return;
            var desired = ((IView)content).Measure(w, double.PositiveInfinity).Height;
            var target = Math.Min(desired, _panelMax);
            if (desired > 0 && Math.Abs(sv.HeightRequest - target) > 1) sv.HeightRequest = target;
        }
        content.SizeChanged += (_, _) => Fit();
        sv.SizeChanged += (_, _) => Fit();
        return sv;
    }

    // ---------- Khung ----------

    private View BuildTopBar()
    {
        var g = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 4, Padding = new Thickness(8, 0),
        };
        g.Add(Ui.IconButton(Icons.Back, L.T("back"), () => _ = Navigation.PopAsync()), 0);
        var name = Ui.Text(_name, Theme.FontFile, bold: true);
        name.LineBreakMode = LineBreakMode.MiddleTruncation; name.MaxLines = 1;
        g.Add(name, 1);
        if (_model?.IsDemo == true) g.Add(Ui.DemoBadge(), 2);
        g.Add(Ui.IconButton(Icons.More, L.T("menu"), () => _ = MenuAsync()), 3);
        return g;
    }

    private View BuildCanvasArea()
    {
        var area = new Grid { BackgroundColor = AppState.Settings.BlackCanvas ? Theme.CanvasBlack : Theme.Canvas };
        if (_model is null)
        {
            // Đọc tệp lỗi: báo trung thực, không vẽ hình minh họa thay thế.
            var box = new VerticalStackLayout { Spacing = 10, Padding = 24, VerticalOptions = LayoutOptions.Center };
            box.Add(Ui.Text(L.T("error_read_title"), 18, Colors.White, bold: true));
            box.Add(Ui.Text(L.T("error_read_message"), Theme.FontBody, Color.FromArgb("#C9D6E2")));
            if (_file is not null)
                box.Add(Ui.Text(_file.DisplayName + " · " + FileIdentity.FormatSize(_file.SizeBytes, L.FormatCulture), Theme.FontSub, Color.FromArgb("#C9D6E2")));
            area.Add(new ScrollView { Content = box });
            return area;
        }

        area.Add(_canvas);
        SemanticProperties.SetDescription(_canvas, L.T("a11y_canvas"));
        // Ghi chú mức độ hiển thị không đè lên bản vẽ nữa (Owner yêu cầu ẩn); xem trong menu ⋮ > Mức độ hiển thị.

        var overlay = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = 12 };
        _canvasButtons = overlay;
        overlay.Add(CanvasButton(Icons.Fit, L.T("fit"), () => _canvas.Fit()));
        overlay.Add(_fullscreenHost);
        RefreshFullscreenButton();
        area.Add(overlay);
        return area;
    }

    /// <summary>
    /// Mức độ hiển thị của tệp thật: đối tượng chưa vẽ, vẽ gần đúng, xref thiếu, đơn vị chưa rõ.
    /// Không hiện đè lên bản vẽ; người dùng mở từ menu khi cần. Null với tệp mẫu hoặc khi đọc lỗi.
    /// </summary>
    private string? DisplayInfo()
    {
        if (_report is null) return null;
        // Bản đầu luôn là hiển thị đơn giản hóa (nét liền, chưa có linetype/lineweight); chỉ nêu số đối tượng khi có đối tượng bị bỏ qua.
        var lines = new List<string> { _report.SkippedTotal > 0 ? L.F("notice_simplified", _report.SkippedTotal) : L.T("notice_simplified_basic") };
        if (_report.ApproximatedTotal > 0) lines.Add(L.F("notice_approximated", _report.ApproximatedTotal));
        if (_report.MissingXrefs.Count > 0) lines.Add(L.F("notice_xref", _report.MissingXrefs.Count));
        if (_report.Model.Unit == DrawingUnit.Unknown) lines.Add(L.T("notice_unit_unknown"));
        return string.Join("\n\n", lines);
    }

    private static View CanvasButton(string icon, string description, Action action) => new Border
    {
        BackgroundColor = Color.FromArgb("#22303C"),
        Stroke = new SolidColorBrush(Color.FromArgb("#52657A")),
        StrokeThickness = 1,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        WidthRequest = Theme.Touch, HeightRequest = Theme.Touch,
        Content = Ui.Icon(icon, Colors.White),
    }.Tap(action).A11y(description);

    private void RefreshFullscreenButton()
    {
        _fullscreenHost.Clear();
        _fullscreenHost.Add(_fullscreen
            ? CanvasButton(Icons.ExitFullscreen, L.T("exit_fullscreen"), ToggleFullscreen)
            : CanvasButton(Icons.Fullscreen, L.T("fullscreen"), ToggleFullscreen));
    }

    private void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        _topBar.IsVisible = !_fullscreen;
        _toolbar.IsVisible = !_fullscreen;
        _panel.IsVisible = !_fullscreen && _tool != Tool.None;
        RefreshFullscreenButton();
    }

    private SoftSurface BuildToolbar()
    {
        var g = new Grid { ColumnSpacing = 4 };
        _toolGrid = g;
        for (var i = 0; i < 4; i++) g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        AddTool(g, 0, Tool.Find, Icons.Search, L.T("tool_find"));
        AddTool(g, 1, Tool.Measure, Icons.Measure, L.T("tool_measure"));
        AddTool(g, 2, Tool.Layers, Icons.Layers, L.T("tool_layers"));
        AddTool(g, 3, Tool.Notes, Icons.Note, L.T("tool_notes"));
        return new SoftSurface(g, Theme.RadiusBlock, new Thickness(6)) { Margin = new Thickness(12, 8, 12, 12) };
    }

    private void AddTool(Grid g, int col, Tool tool, string icon, string label)
    {
        var b = new ToolButton(icon, label, () => SelectTool(_tool == tool ? Tool.None : tool));
        _toolButtons[tool] = b;
        g.Add(b, col);
    }

    /// <summary>Đổi công cụ. Camera không đổi; kết quả tìm, số đo và ghi chú được giữ.</summary>
    private void SelectTool(Tool tool)
    {
        if (_model is null)
        {
            _ = DisplayAlertAsync(L.T("error_read_title"), L.T("real_tools_disabled"), L.T("ok"));
            return;
        }
        _tool = tool;
        foreach (var (t, b) in _toolButtons) b.SetSelected(t == tool);
        _canvas.Mode = tool switch { Tool.Measure => CanvasMode.Measure, Tool.Notes => CanvasMode.Note, _ => CanvasMode.Pan };
        _panel.Content = tool switch
        {
            Tool.Find => BuildFind(),
            Tool.Measure => BuildMeasure(),
            Tool.Layers => BuildLayers(),
            Tool.Notes => BuildNotes(),
            _ => null,
        };
        _panel.IsVisible = tool != Tool.None;
        _canvas.InvalidateSurface();
    }

    private async Task MenuAsync()
    {
        var fit = L.T("fit"); var about = L.T("menu_about_demo"); var share = L.T("menu_share_copy"); var info = L.T("menu_display_info");
        var options = new List<string>();
        if (_model is not null) options.Add(fit);
        if (_file is not null) options.Add(share);
        if (DisplayInfo() is not null) options.Add(info);
        if (_model?.IsDemo == true) options.Add(about);
        var choice = await DisplayActionSheetAsync(_name, L.T("cancel"), null, options.ToArray());
        if (choice == fit) _canvas.Fit();
        else if (choice == share && _file is not null) await Files.ShareCopyAsync(_file);
        else if (choice == info && DisplayInfo() is { } text) await DisplayAlertAsync(info, text, L.T("ok"));
        else if (choice == about) await DisplayAlertAsync(L.T("menu_about_demo"), L.T("demo_explain"), L.T("ok"));
    }

    private static View PanelTitle(string text)
    {
        var l = Ui.Text(text, Theme.FontBody, bold: true);
        SemanticProperties.SetHeadingLevel(l, SemanticHeadingLevel.Level2);
        return l;
    }

    // ---------- Tìm ----------

    private View BuildFind()
    {
        var col = new VerticalStackLayout { Spacing = 8 };
        var field = new SearchField(L.T("find_placeholder"), q => { _query = q; RunFind(resetIndex: true); });
        field.Entry.Text = _query;
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 4 };
        _findCount = Ui.Text("", Theme.FontSub, Theme.TextSub);
        row.Add(_findCount, 0);
        row.Add(Ui.IconButton(Icons.Up, L.T("find_prev"), () => StepHit(-1)), 1);
        row.Add(Ui.IconButton(Icons.Down, L.T("find_next"), () => StepHit(1)), 2);
        col.Add(field);
        col.Add(row);
        RunFind(resetIndex: false);
        return col;
    }

    private void RunFind(bool resetIndex)
    {
        var hits = TextSearch.Find(_model!, _query, _canvas.Layers);
        _canvas.Hits = hits;
        if (resetIndex || _canvas.CurrentHit >= hits.Count) _canvas.CurrentHit = hits.Count > 0 ? 0 : -1;
        if (resetIndex && hits.Count > 0) _canvas.FocusOn(hits[0].Position, hits[0].Height);
        UpdateFindCount();
        _canvas.InvalidateSurface();
    }

    private void StepHit(int delta)
    {
        var n = _canvas.Hits.Count;
        if (n == 0) return;
        _canvas.CurrentHit = ((_canvas.CurrentHit + delta) % n + n) % n;
        var h = _canvas.Hits[_canvas.CurrentHit];
        _canvas.FocusOn(h.Position, h.Height);
        UpdateFindCount();
    }

    private void UpdateFindCount()
    {
        if (_findCount is null) return;
        var n = _canvas.Hits.Count;
        _findCount.Text = string.IsNullOrWhiteSpace(_query) ? L.T("find_hint")
            : n == 0 ? L.T("find_none")
            : L.F("find_count", _canvas.CurrentHit + 1, n);
    }

    // ---------- Đo ----------

    private View BuildMeasure()
    {
        var col = new VerticalStackLayout { Spacing = 6 };
        _measureResult = Ui.Text("", 20, bold: true);
        _measureHint = Ui.Text("", Theme.FontSub, Theme.TextSub);
        col.Add(_measureResult);
        col.Add(_measureHint);
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
        row.Add(new PrimaryButton(L.T("measure_clear"), () => _canvas.ClearMeasure(), secondary: true), 0);
        row.Add(new PrimaryButton(L.T("measure_set_unit"), () => _ = ConfirmUnitAsync(), secondary: true), 1);
        col.Add(row);
        UpdateMeasure();
        return col;
    }

    private void UpdateAim(SnapResult? aim)
    {
        if (_measureHint is null || aim is not { } a) { UpdateMeasure(); return; }
        _measureHint.Text = L.F("measure_snap", SnapName(a.Kind));
    }

    private static string SnapName(SnapKind k) => k switch
    {
        SnapKind.Endpoint => L.T("snap_endpoint"),
        SnapKind.Midpoint => L.T("snap_midpoint"),
        SnapKind.Center => L.T("snap_center"),
        _ => L.T("snap_free"),
    };

    /// <summary>Số đo luôn tính từ tọa độ bản vẽ. Chưa biết đơn vị nguồn thì không quy đổi mà yêu cầu xác nhận.</summary>
    private void UpdateMeasure()
    {
        if (_measureResult is null || _measureHint is null) return;
        var a = _canvas.MeasureA; var b = _canvas.MeasureB;
        if (a is null) { _measureResult.Text = L.T("measure_pick_first"); _measureHint.Text = L.T("measure_how"); _canvas.MeasureLabel = ""; return; }
        if (b is null) { _measureResult.Text = L.T("measure_pick_second"); _measureHint.Text = L.F("measure_snap", SnapName(a.Value.Kind)); _canvas.MeasureLabel = ""; return; }

        var m = new Measurement(a.Value.Point, b.Value.Point);
        var display = AppState.Settings.DisplayUnit;
        string text;
        if (_sourceUnit == DrawingUnit.Unknown)
        {
            text = L.F("measure_drawing_units", m.RawDistance.ToString("N3", L.FormatCulture));
            _measureHint.Text = L.T("measure_unit_unknown");
        }
        else
        {
            var unit = display == DrawingUnit.Unknown ? _sourceUnit : display;
            text = Units.Format(m.DistanceIn(_sourceUnit, unit)!.Value, unit, L.FormatCulture);
            _measureHint.Text = L.F("measure_units_line", Units.Symbol(_sourceUnit), Units.Symbol(unit),
                SnapName(a.Value.Kind), SnapName(b.Value.Kind));
        }
        _measureResult.Text = text;
        _canvas.MeasureLabel = text;
        _canvas.InvalidateSurface();
    }

    private async Task ConfirmUnitAsync()
    {
        var map = new Dictionary<string, DrawingUnit>
        {
            [L.T("unit_mm")] = DrawingUnit.Millimeter, [L.T("unit_cm")] = DrawingUnit.Centimeter, [L.T("unit_m")] = DrawingUnit.Meter,
            [L.T("unit_in")] = DrawingUnit.Inch, [L.T("unit_ft")] = DrawingUnit.Foot,
        };
        var choice = await DisplayActionSheetAsync(L.T("measure_set_unit_title"), L.T("cancel"), null, map.Keys.ToArray());
        if (choice is not null && map.TryGetValue(choice, out var u)) { _sourceUnit = u; UpdateMeasure(); }
    }

    // ---------- Layer ----------

    private View BuildLayers()
    {
        var col = new VerticalStackLayout { Spacing = 6 };
        var list = new VerticalStackLayout { Spacing = 2 };
        var top = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        var field = new SearchField(L.T("layer_search"), q => { _layerFilter = q; FillLayers(list); });
        field.Entry.Text = _layerFilter;
        top.Add(field, 0);
        var showAll = Ui.Text(L.T("layer_show_all"), Theme.FontSub, Theme.Primary, bold: true);
        showAll.MinimumHeightRequest = Theme.Touch;
        showAll.Tap(() => { _canvas.Layers.ShowAll(); FillLayers(list); RefreshAfterLayerChange(); });
        top.Add(showAll, 1);
        col.Add(top);
        col.Add(list);
        FillLayers(list);
        return Scrollable(col);
    }

    private void FillLayers(VerticalStackLayout list)
    {
        list.Clear();
        var layers = LayerState.Filter(_model!.Layers, _layerFilter).ToList();
        if (layers.Count == 0) { list.Add(Ui.Text(L.T("layer_none"), Theme.FontSub, Theme.TextSub)); return; }
        foreach (var layer in layers)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10, MinimumHeightRequest = Theme.Touch };
            row.Add(new BoxView { Color = Color.FromRgba((int)((layer.ColorArgb >> 16) & 0xFF), (int)((layer.ColorArgb >> 8) & 0xFF), (int)(layer.ColorArgb & 0xFF), 255), WidthRequest = 14, HeightRequest = 14, CornerRadius = 7, VerticalOptions = LayoutOptions.Center }, 0);
            // Tên layer là nội dung bản vẽ, giữ nguyên.
            row.Add(Ui.Text(layer.Name, Theme.FontBody), 1);
            var sw = new Switch { IsToggled = _canvas.Layers.IsVisible(layer.Name), OnColor = Theme.Primary, VerticalOptions = LayoutOptions.Center };
            SemanticProperties.SetDescription(sw, L.F("a11y_layer_toggle", layer.Name));
            sw.Toggled += (_, e) => { _canvas.Layers.Set(layer.Name, e.Value); RefreshAfterLayerChange(); };
            row.Add(sw, 2);
            list.Add(row);
        }
    }

    private void RefreshAfterLayerChange()
    {
        // Kết quả tìm chỉ tính trên layer đang hiện.
        _canvas.Hits = TextSearch.Find(_model!, _query, _canvas.Layers);
        if (_canvas.CurrentHit >= _canvas.Hits.Count) _canvas.CurrentHit = _canvas.Hits.Count > 0 ? 0 : -1;
        _canvas.InvalidateSurface();
    }

    // ---------- Ghi chú ----------

    /// <summary>
    /// Ba kiểu ghi chú: Chữ (chạm vào bản vẽ), Mũi tên và Khoanh vùng (kéo một ngón; hai ngón để di chuyển, thu phóng).
    /// Tất cả lưu riêng theo khóa nội dung tệp, không ghi vào tệp gốc.
    /// </summary>
    private View BuildNotes()
    {
        var col = new VerticalStackLayout { Spacing = 6 };
        var modes = new Grid { ColumnSpacing = 4 };
        for (var c = 0; c < 3; c++) modes.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        AddNoteMode(modes, 0, NoteKind.Text, Icons.TextTool, L.T("annot_text"));
        AddNoteMode(modes, 1, NoteKind.Arrow, Icons.Arrow, L.T("annot_arrow"));
        AddNoteMode(modes, 2, NoteKind.Cloud, Icons.Region, L.T("annot_cloud"));
        col.Add(modes);
        col.Add(PanelTitle(_canvas.NoteTool switch
        {
            NoteKind.Arrow => L.T("annot_hint_arrow"),
            NoteKind.Cloud => L.T("annot_hint_cloud"),
            _ => L.T("notes_hint"),
        }));
        col.Add(Ui.Text(L.T("notes_separate"), Theme.FontSub, Theme.TextSub));
        var notes = _canvas.Notes;
        for (var i = 0; i < notes.Count; i++)
        {
            var n = notes[i];
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8, MinimumHeightRequest = Theme.Touch };
            row.Add(Ui.Text((i + 1).ToString(L.FormatCulture), Theme.FontBody, Theme.Primary, bold: true), 0);
            row.Add(Ui.Icon(n.Kind switch { NoteKind.Arrow => Icons.Arrow, NoteKind.Cloud => Icons.Region, NoteKind.Text => Icons.TextTool, _ => Icons.Note }, Theme.TextSub), 1);
            var text = Ui.Text(n.Text.Length > 0 ? n.Text : L.T("annot_no_text"), Theme.FontBody, n.Text.Length > 0 ? Theme.Text : Theme.TextSub);
            text.Tap(() => _canvas.FocusOn(n.Anchor));
            row.Add(text, 2);
            row.Add(Ui.IconButton(Icons.Trash, L.F("a11y_delete_note", i + 1), () => _ = DeleteNoteAsync(n), Theme.Danger), 3);
            col.Add(row);
        }
        return Scrollable(col);
    }

    private void AddNoteMode(Grid g, int col, NoteKind kind, string icon, string label)
    {
        var b = new ToolButton(icon, label, () => { _canvas.NoteTool = kind; _panel.Content = BuildNotes(); });
        b.SetSelected(_canvas.NoteTool == kind);
        g.Add(b, col);
    }

    private async Task AddNoteAsync(Point2 at)
    {
        var text = await DisplayPromptAsync(L.T("notes_new_title"), L.T("notes_new_message"), L.T("save"), L.T("cancel"), maxLength: 500);
        if (string.IsNullOrWhiteSpace(text)) return;
        await SaveNoteAsync(NoteKind.Text, at, at, text);
    }

    /// <summary>Mũi tên hoặc vùng khoanh vừa vẽ: hỏi chữ kèm theo (có thể bỏ trống). Bấm Hủy thì bỏ hình.</summary>
    private async Task AddShapeAsync(NoteKind kind, Point2 a, Point2 b)
    {
        var text = await DisplayPromptAsync(kind == NoteKind.Arrow ? L.T("annot_arrow") : L.T("annot_cloud"), L.T("annot_text_optional"), L.T("save"), L.T("cancel"), maxLength: 500);
        if (text is null) { _canvas.InvalidateSurface(); return; }
        await SaveNoteAsync(kind, a, b, text);
    }

    private async Task SaveNoteAsync(NoteKind kind, Point2 a, Point2 b, string text)
    {
        try
        {
            AppState.Notes.Add(_fileKey, kind, a, b, text, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await DisplayAlertAsync(L.T("error_generic"), L.T("error_save_note"), L.T("ok"));
            return;
        }
        ReloadNotes();
    }

    private async Task DeleteNoteAsync(Note n)
    {
        if (!await DisplayAlertAsync(L.T("notes_delete_title"), n.Text, L.T("delete"), L.T("cancel"))) return;
        AppState.Notes.Remove(_fileKey, n.Id);
        ReloadNotes();
    }

    private void ReloadNotes()
    {
        _canvas.Notes = AppState.Notes.Load(_fileKey);
        _canvas.InvalidateSurface();
        if (_tool == Tool.Notes) _panel.Content = BuildNotes();
    }
}
