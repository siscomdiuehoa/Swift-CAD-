using Microsoft.Maui.Controls.Shapes;
using SwiftCad.Core;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace SwiftCad.App;

public static class Ui
{
    private static readonly PathGeometryConverter GeometryConverter = new();

    public static Label Text(string text, double size = Theme.FontBody, Color? color = null, bool bold = false, int maxLines = -1)
    {
        var l = new Label
        {
            Text = text,
            FontSize = size,
            TextColor = color ?? Theme.Text,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalTextAlignment = TextAlignment.Center,
        };
        if (maxLines > 0) { l.MaxLines = maxLines; l.LineBreakMode = LineBreakMode.TailTruncation; }
        return l;
    }

    public static Path Icon(string data, Color? color = null, bool filled = false)
    {
        var c = new SolidColorBrush(color ?? Theme.Text);
        return new Path
        {
            Data = (Geometry)GeometryConverter.ConvertFromInvariantString(data)!,
            Stroke = filled ? null : c,
            Fill = filled ? c : null,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Aspect = Stretch.None,
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
    }

    public static T Tap<T>(this T view, Action action) where T : View
    {
        var g = new TapGestureRecognizer();
        g.Tapped += (_, _) => action();
        view.GestureRecognizers.Add(g);
        return view;
    }

    /// <summary>Nhãn trợ năng cho trình đọc màn hình. Luôn lấy từ tài nguyên dịch.</summary>
    public static T A11y<T>(this T view, string description) where T : View
    {
        SemanticProperties.SetDescription(view, description);
        return view;
    }

    /// <summary>Nút chỉ có biểu tượng, vùng chạm tối thiểu 48.</summary>
    public static View IconButton(string icon, string description, Action action, Color? color = null)
    {
        var g = new Grid { MinimumWidthRequest = Theme.Touch, MinimumHeightRequest = Theme.Touch, BackgroundColor = Colors.Transparent };
        g.Add(Icon(icon, color));
        return g.Tap(action).A11y(description);
    }

    public static View DemoBadge() => new Border
    {
        BackgroundColor = Theme.DemoBg,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 8 },
        Padding = new Thickness(8, 2),
        VerticalOptions = LayoutOptions.Center,
        HorizontalOptions = LayoutOptions.Start,
        Content = Text(L.T("demo_badge"), 12, Theme.DemoText, bold: true),
    };

    public static View Logo(double size = 36)
    {
        var g = new Grid { WidthRequest = size, HeightRequest = size, InputTransparent = true };
        var scale = size / 24.0;
        var body = Icon(Icons.LogoBody, Theme.Primary, filled: true);
        var mark = Icon(Icons.LogoMark, Colors.White);
        body.Scale = scale; mark.Scale = scale;
        g.Add(body); g.Add(mark);
        return g;
    }

    public static string FileSubtitle(RecentFile f) =>
        FileIdentity.FormatSize(f.SizeBytes, L.FormatCulture) + " · " +
        L.F("opened_at", f.LastOpenedUtc.ToLocalTime().ToString("g", L.FormatCulture));
}

/// <summary>Khối nổi mềm: nền sáng, viền sáng, một bóng mềm phía dưới phải.</summary>
public class SoftSurface : Border
{
    public SoftSurface(View? content = null, double radius = Theme.RadiusBlock, Thickness? padding = null)
    {
        Content = content;
        BackgroundColor = Theme.Surface;
        Stroke = new SolidColorBrush(Theme.SurfaceEdge);
        StrokeThickness = 1.5;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) };
        Padding = padding ?? new Thickness(16);
        Shadow = Theme.SoftShadow();
    }

    /// <summary>Trạng thái chọn thể hiện bằng nền xanh nhạt và viền xanh, không chỉ bằng bóng.</summary>
    public void SetSelected(bool selected)
    {
        BackgroundColor = selected ? Theme.Selected : Theme.Surface;
        Stroke = new SolidColorBrush(selected ? Theme.Primary : Theme.SurfaceEdge);
    }
}

public class PrimaryButton : Button
{
    public PrimaryButton(string text, Action action, bool secondary = false)
    {
        Text = text;
        FontSize = 16;
        FontAttributes = FontAttributes.Bold;
        CornerRadius = (int)Theme.RadiusButton;
        MinimumHeightRequest = 52;
        Padding = new Thickness(16, 12);
        LineBreakMode = LineBreakMode.WordWrap;
        if (secondary)
        {
            BackgroundColor = Theme.Surface;
            TextColor = Theme.Primary;
            BorderColor = Theme.Primary;
            BorderWidth = 1.5;
        }
        else
        {
            BackgroundColor = Theme.Primary;
            TextColor = Theme.OnPrimary;
            Shadow = Theme.SoftShadow();
        }
        Clicked += (_, _) => action();
    }
}

public class SearchField : SoftSurface
{
    public Entry Entry { get; }

    public SearchField(string placeholder, Action<string> onChanged) : base(null, Theme.RadiusButton, new Thickness(12, 0))
    {
        Entry = new Entry
        {
            Placeholder = placeholder,
            PlaceholderColor = Theme.TextSub,
            TextColor = Theme.Text,
            FontSize = Theme.FontBody,
            BackgroundColor = Colors.Transparent,
            MinimumHeightRequest = Theme.Touch,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            ReturnType = ReturnType.Search,
        };
        SemanticProperties.SetDescription(Entry, placeholder);
        Entry.TextChanged += (_, e) => onChanged(e.NewTextValue ?? "");
        var g = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
        g.Add(Ui.Icon(Icons.Search, Theme.TextSub), 0);
        g.Add(Entry, 1);
        Content = g;
    }
}

public class SourceTile : SoftSurface
{
    public SourceTile(string icon, string title, string status, string description, Action action) : base(null, Theme.RadiusBlock, new Thickness(12))
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var ic = Ui.Icon(icon, Theme.Primary); ic.HorizontalOptions = LayoutOptions.Start;
        stack.Add(ic);
        stack.Add(Ui.Text(title, Theme.FontBody, bold: true));
        stack.Add(Ui.Text(status, Theme.FontSub, Theme.Primary, bold: true));
        Content = stack;
        MinimumHeightRequest = 96;
        this.Tap(action).A11y(description);
    }
}

public class DrawingRow : SoftSurface
{
    public DrawingRow(string name, string subtitle, bool demo, string openDescription, Action open, string? menuDescription = null, Action? menu = null)
        : base(null, Theme.RadiusBlock, new Thickness(12, 10))
    {
        var g = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 12,
            MinimumHeightRequest = Theme.RowMin - 20,
        };
        var thumb = new Border
        {
            WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0,
            BackgroundColor = Theme.Selected,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = Ui.Icon(Icons.File, Theme.Primary),
            VerticalOptions = LayoutOptions.Center,
        };
        var text = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        // Tên tệp giữ nguyên như tệp gốc, không dịch.
        text.Add(Ui.Text(name, Theme.FontFile, bold: true, maxLines: 2));
        text.Add(Ui.Text(subtitle, Theme.FontSub, Theme.TextSub));
        if (demo) text.Add(Ui.DemoBadge());
        g.Add(thumb, 0);
        g.Add(text, 1);
        if (menu is not null) g.Add(Ui.IconButton(Icons.More, menuDescription ?? name, menu, Theme.TextSub), 2);
        Content = g;
        this.Tap(open).A11y(openDescription);
    }
}

/// <summary>Nút công cụ luôn có nhãn. Khi chọn: nền xanh nhạt, viền xanh, biểu tượng và nhãn xanh đậm.</summary>
public class ToolButton : Border
{
    private readonly string _icon;
    private readonly Label _label;
    private readonly Grid _iconHost = new() { HeightRequest = 24 };

    public ToolButton(string icon, string label, Action action)
    {
        _icon = icon;
        _label = Ui.Text(label, 13, Theme.TextSub);
        _label.HorizontalTextAlignment = TextAlignment.Center;
        _label.LineBreakMode = LineBreakMode.WordWrap;
        StrokeShape = new RoundRectangle { CornerRadius = 14 };
        StrokeThickness = 1.5;
        Padding = new Thickness(4, 8);
        MinimumHeightRequest = 56;
        var stack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        stack.Add(_iconHost);
        stack.Add(_label);
        Content = stack;
        SetSelected(false);
        this.Tap(action).A11y(label);
    }

    public void SetSelected(bool selected)
    {
        BackgroundColor = selected ? Theme.Selected : Colors.Transparent;
        Stroke = new SolidColorBrush(selected ? Theme.Primary : Colors.Transparent);
        _label.TextColor = selected ? Theme.Primary : Theme.TextSub;
        _label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
        _iconHost.Clear();
        _iconHost.Add(Ui.Icon(_icon, selected ? Theme.Primary : Theme.TextSub));
    }
}

/// <summary>Hàng lựa chọn một trong nhiều: mục đang chọn có dấu kiểm, chữ đậm và nền xanh nhạt.</summary>
public class OptionRow : Border
{
    public OptionRow(string text, bool selected, Action action)
    {
        StrokeShape = new RoundRectangle { CornerRadius = 12 };
        StrokeThickness = 1.5;
        Stroke = new SolidColorBrush(selected ? Theme.Primary : Colors.Transparent);
        BackgroundColor = selected ? Theme.Selected : Colors.Transparent;
        Padding = new Thickness(12, 4);
        MinimumHeightRequest = Theme.Touch;
        var g = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        g.Add(Ui.Text(text, Theme.FontBody, selected ? Theme.Primary : Theme.Text, bold: selected), 0);
        if (selected) g.Add(Ui.Icon(Icons.Check, Theme.Primary), 1);
        Content = g;
        this.Tap(action).A11y(selected ? L.F("a11y_selected", text) : text);
    }
}
