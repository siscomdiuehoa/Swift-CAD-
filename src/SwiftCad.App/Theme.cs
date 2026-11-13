namespace SwiftCad.App;

/// <summary>Màu, kích thước và bóng dùng chung theo đặc tả Neumorphism V2 trong Issue #1.</summary>
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb("#E8F1F8");
    public static readonly Color Surface = Color.FromArgb("#EEF5FB");
    public static readonly Color SurfaceEdge = Color.FromArgb("#FFFFFF");
    public static readonly Color ShadowDark = Color.FromArgb("#9FB4C9");
    public static readonly Color Text = Color.FromArgb("#10213B");
    public static readonly Color TextSub = Color.FromArgb("#4B607A");
    public static readonly Color Primary = Color.FromArgb("#0878FF");
    public static readonly Color OnPrimary = Colors.White;
    public static readonly Color Selected = Color.FromArgb("#D6E9FF");
    public static readonly Color Danger = Color.FromArgb("#B3261E");
    public static readonly Color DemoBg = Color.FromArgb("#FFE7B0");
    public static readonly Color DemoText = Color.FromArgb("#5C3D00");
    public static readonly Color Canvas = Color.FromArgb("#111C25");
    public static readonly Color CanvasBlack = Color.FromArgb("#000000");

    public const double Margin = 20;
    public const double Gap = 16;
    public const double RowMin = 72;
    public const double Touch = 48;
    public const double RadiusBlock = 18;
    public const double RadiusButton = 16;

    public const double FontTitle = 26;
    public const double FontFile = 16;
    public const double FontBody = 15;
    public const double FontSub = 14;

    /// <summary>Một bóng mềm phía dưới phải; viền sáng thay cho bóng sáng phía trên trái.</summary>
    public static Shadow SoftShadow() => new()
    {
        Brush = new SolidColorBrush(ShadowDark),
        Offset = new Point(5, 6),
        Radius = 14,
        Opacity = 0.55f,
    };
}

/// <summary>Biểu tượng nét do dự án tự vẽ trên lưới 24 x 24. Không dùng bộ biểu tượng bên ngoài.</summary>
public static class Icons
{
    public const string Search = "M10.5 4 A6.5 6.5 0 1 0 10.5 17 A6.5 6.5 0 1 0 10.5 4 M15.5 15.5 L20 20";
    public const string Measure = "M3 16 L16 3 L21 8 L8 21 Z M7 12 L9 14 M10 9 L12.5 11.5 M13 6 L15 8";
    public const string Layers = "M12 3 L21 8 L12 13 L3 8 Z M3 12 L12 17 L21 12 M3 16 L12 21 L21 16";
    public const string Note = "M4 4 H20 V16 H13 L8 21 V16 H4 Z M8 9 H16 M8 12 H13";
    public const string Back = "M15 5 L8 12 L15 19";
    public const string More = "M12 5 A1 1 0 1 0 12 7 A1 1 0 1 0 12 5 M12 11 A1 1 0 1 0 12 13 A1 1 0 1 0 12 11 M12 17 A1 1 0 1 0 12 19 A1 1 0 1 0 12 17";
    public const string Settings = "M4 7 H14 M18 7 H20 M4 17 H6 M10 17 H20 M14 5 V9 H18 V5 Z M6 15 V19 H10 V15 Z";
    public const string Fit = "M4 9 V4 H9 M15 4 H20 V9 M20 15 V20 H15 M9 20 H4 V15";
    public const string Fullscreen = "M4 4 L9 9 M4 8 V4 H8 M20 4 L15 9 M16 4 H20 V8 M4 20 L9 15 M4 16 V20 H8 M20 20 L15 15 M16 20 H20 V16";
    public const string ExitFullscreen = "M9 4 V9 H4 M15 4 V9 H20 M9 20 V15 H4 M15 20 V15 H20";
    public const string File = "M6 3 H14 L19 8 V21 H6 Z M14 3 V8 H19";
    public const string Cloud = "M7 18 H17 A4 4 0 0 0 17.5 10 A6 6 0 0 0 6 9.5 A4.3 4.3 0 0 0 7 18 Z";
    public const string Bookmark = "M7 3 H17 V21 L12 17 L7 21 Z";
    public const string Check = "M5 12 L10 17 L19 7";
    public const string Up = "M6 15 L12 9 L18 15";
    public const string Down = "M6 9 L12 15 L18 9";
    public const string Trash = "M5 7 H19 M9 7 V4 H15 V7 M7 7 L8 20 H16 L17 7";
    public const string Person = "M12 4 A4 4 0 1 0 12 12 A4 4 0 1 0 12 4 M4 21 A8 8 0 0 1 20 21";
    public const string TextTool = "M5 6 V4 H19 V6 M12 4 V20 M9 20 H15";
    public const string Arrow = "M5 19 L19 5 M10 5 H19 V14";
    public const string Region = "M6 8 A2 2 0 0 1 10 6 A2 2 0 0 1 14 6 A2 2 0 0 1 18 8 A2 2 0 0 1 18 12 A2 2 0 0 1 18 16 A2 2 0 0 1 14 18 A2 2 0 0 1 10 18 A2 2 0 0 1 6 16 A2 2 0 0 1 6 12 A2 2 0 0 1 6 8 Z";
    public const string Info = "M12 3 A9 9 0 1 0 12 21 A9 9 0 1 0 12 3 M12 11 V16 M12 8 V8.5";
    public const string LogoBody = "M5 2 H15 L21 8 V22 H5 Z";
    public const string LogoMark = "M8 18 L11 12 L14 16 L16 13 L18 18 Z";
}
