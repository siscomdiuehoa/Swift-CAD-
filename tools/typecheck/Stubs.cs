// Stub tối thiểu của bề mặt API MAUI/SkiaSharp mà app dùng, chỉ để kiểm tra kiểu tại chỗ khi không có NuGet.
#nullable disable
global using Microsoft.Maui;
global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Graphics;
global using Microsoft.Maui.Storage;
global using Microsoft.Maui.ApplicationModel;
global using Microsoft.Maui.ApplicationModel.DataTransfer;
global using Microsoft.Maui.Devices;
global using Microsoft.Maui.Hosting;
using System.ComponentModel;

namespace Microsoft.Maui.Graphics {
  public class Color { public static Color FromArgb(string s) => null; public static Color FromRgba(int r, int g, int b, int a) => null; }
  public static class Colors { public static Color White, Black, Transparent; }
  public struct Point { public Point(double x, double y) {} }
  public struct Size { public double Width, Height; }
}
namespace Microsoft.Maui {
  public interface IView { Microsoft.Maui.Graphics.Size Measure(double w, double h); }
  public struct Thickness { public Thickness(double u) {} public Thickness(double h, double v) {} public Thickness(double l, double t, double r, double b) {} public static implicit operator Thickness(double d) => default; }
  public struct CornerRadius { public CornerRadius(double u) {} public static implicit operator CornerRadius(double d) => default; }
  public struct GridLength { public GridLength(double v) {} public static GridLength Auto, Star; }
  public enum SafeAreaRegions { None, All }
  public struct SafeAreaEdges { public static SafeAreaEdges All => default; }
  public enum TextAlignment { Start, Center, End }
  public enum LineBreakMode { WordWrap, TailTruncation, MiddleTruncation }
  public enum ReturnType { Default, Search }
  public enum ClearButtonVisibility { Never, WhileEditing }
  public enum SemanticHeadingLevel { None, Level1, Level2 }
  public interface IActivationState {}
  public enum AppTheme { Unspecified, Light, Dark }
}
namespace Microsoft.Maui.Hosting {
  public class MauiApp { public static MauiAppBuilder CreateBuilder() => null; }
  public class MauiAppBuilder { public MauiApp Build() => null; }
  public static class Ext { public static MauiAppBuilder UseMauiApp<T>(this MauiAppBuilder b) => b; }
}
namespace SkiaSharp.Views.Maui.Controls.Hosting { public static class E { public static MauiAppBuilder UseSkiaSharp(this MauiAppBuilder b) => b; } }
namespace Microsoft.Maui.Devices { public struct DevicePlatform { public static DevicePlatform Android, iOS; } }
namespace Microsoft.Maui.Storage {
  public static class FileSystem { public static string AppDataDirectory => ""; public static Task<Stream> OpenAppPackageFileAsync(string f) => null; }
  public class FileBase { public string FileName { get; set; } public Task<Stream> OpenReadAsync() => null; }
  public class FileResult : FileBase {}
  public class FilePickerFileType { public FilePickerFileType(IDictionary<DevicePlatform, IEnumerable<string>> d) {} }
  public class PickOptions { public string PickerTitle { get; set; } public FilePickerFileType FileTypes { get; set; } }
  public interface IFilePicker { Task<FileResult> PickAsync(PickOptions o = null); }
  public static class FilePicker { public static IFilePicker Default => null; }
}
namespace Microsoft.Maui.ApplicationModel { public interface IAppInfo { string VersionString { get; } } public static class AppInfo { public static IAppInfo Current => null; } }
namespace Microsoft.Maui.ApplicationModel.DataTransfer {
  public class ShareFile : Microsoft.Maui.Storage.FileBase { public ShareFile(string p) {} }
  public class ShareFileRequest { public string Title { get; set; } public ShareFile File { get; set; } }
  public interface IShare { Task RequestAsync(ShareFileRequest r); }
  public static class Share { public static IShare Default => null; }
}
namespace Microsoft.Maui.Controls {
  public class BindableObject {}
  public class Element : BindableObject {}
  public class Brush : Element { public static SolidColorBrush Transparent => null; }
  public class SolidColorBrush : Brush { public SolidColorBrush(Color c) {} }
  public class Shadow : Element { public Brush Brush { get; set; } public Point Offset { get; set; } public float Radius { get; set; } public float Opacity { get; set; } }
  public class LayoutOptions { public static LayoutOptions Start, Center, End, Fill; }
  public enum FontAttributes { None, Bold }
  public enum Stretch { None, Uniform }
  public class VisualElement : Element {
    public Color BackgroundColor { get; set; } public double WidthRequest { get; set; } public double HeightRequest { get; set; }
    public double MinimumWidthRequest { get; set; } public double MinimumHeightRequest { get; set; } public double MaximumHeightRequest { get; set; }
    public bool IsVisible { get; set; } public bool InputTransparent { get; set; } public double Scale { get; set; } public Shadow Shadow { get; set; }
    public double Width => 0; public double Height => 0; public event EventHandler SizeChanged;
  }
  public class GestureRecognizer : Element {}
  public class TapGestureRecognizer : GestureRecognizer { public event EventHandler Tapped; }
  public class View : VisualElement, IView { Microsoft.Maui.Graphics.Size IView.Measure(double w, double h) => default;
    public LayoutOptions HorizontalOptions { get; set; } public LayoutOptions VerticalOptions { get; set; } public Thickness Margin { get; set; }
    public IList<GestureRecognizer> GestureRecognizers => null;
  }
  public class Layout : View, System.Collections.IEnumerable { public Thickness Padding { get; set; } public IList<IView> Children => null; public void Add(IView v) {} public void Clear() {} public System.Collections.IEnumerator GetEnumerator() => null; }
  public class StackBase : Layout { public double Spacing { get; set; } }
  public class VerticalStackLayout : StackBase {}
  public class HorizontalStackLayout : StackBase {}
  public class RowDefinition { public RowDefinition(GridLength g) {} }
  public class ColumnDefinition { public ColumnDefinition(GridLength g) {} public GridLength Width { get; set; } }
  public class RowDefinitionCollection : List<RowDefinition> {}
  public class ColumnDefinitionCollection : List<ColumnDefinition> {}
  public class Grid : Layout { public static void SetRow(BindableObject b, int v) {} public static void SetColumn(BindableObject b, int v) {} public static void SetRowSpan(BindableObject b, int v) {} public static void SetColumnSpan(BindableObject b, int v) {} public RowDefinitionCollection RowDefinitions { get; set; } = new(); public ColumnDefinitionCollection ColumnDefinitions { get; set; } = new(); public double ColumnSpacing { get; set; } public double RowSpacing { get; set; } }
  public static class GridExtensions { public static void Add(this Grid grid, IView view, int column = 0, int row = 0) {} }
  public class ScrollView : View { public View Content { get; set; } }
  public class ContentView : View { public View Content { get; set; } }
  public class Border : View { public View Content { get; set; } public Brush Stroke { get; set; } public double StrokeThickness { get; set; } public Shapes.IShape StrokeShape { get; set; } public Thickness Padding { get; set; } }
  public class Label : View { public string Text { get; set; } public double FontSize { get; set; } public Color TextColor { get; set; } public FontAttributes FontAttributes { get; set; } public LineBreakMode LineBreakMode { get; set; } public int MaxLines { get; set; } public TextAlignment HorizontalTextAlignment { get; set; } public TextAlignment VerticalTextAlignment { get; set; } }
  public class Button : View { public string Text { get; set; } public double FontSize { get; set; } public Color TextColor { get; set; } public Color BorderColor { get; set; } public double BorderWidth { get; set; } public int CornerRadius { get; set; } public Thickness Padding { get; set; } public FontAttributes FontAttributes { get; set; } public LineBreakMode LineBreakMode { get; set; } public event EventHandler Clicked; }
  public class TextChangedEventArgs : EventArgs { public string NewTextValue => ""; }
  public class Entry : View { public string Text { get; set; } public string Placeholder { get; set; } public Color PlaceholderColor { get; set; } public Color TextColor { get; set; } public double FontSize { get; set; } public ClearButtonVisibility ClearButtonVisibility { get; set; } public ReturnType ReturnType { get; set; } public event EventHandler<TextChangedEventArgs> TextChanged; }
  public class ToggledEventArgs : EventArgs { public bool Value => false; }
  public class Switch : View { public bool IsToggled { get; set; } public Color OnColor { get; set; } public event EventHandler<ToggledEventArgs> Toggled; }
  public class ActivityIndicator : View { public bool IsRunning { get; set; } public Color Color { get; set; } }
  public class BoxView : View { public Color Color { get; set; } public CornerRadius CornerRadius { get; set; } }
  public static class SemanticProperties { public static void SetDescription(BindableObject b, string v) {} public static void SetHeadingLevel(BindableObject b, SemanticHeadingLevel v) {} }
  public interface INavigation { Task PushAsync(Page p); Task<Page> PopAsync(); Task<Page> PopAsync(bool animated); void InsertPageBefore(Page page, Page before); }
  public interface IDispatcher { bool Dispatch(Action a); }
  public class Page : VisualElement { public event EventHandler Loaded; public IDispatcher Dispatcher => null;
    public string Title { get; set; } public INavigation Navigation => null;
    public Task DisplayAlertAsync(string title, string message, string cancel) => null;
    public Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel) => null;
    public Task<string> DisplayActionSheetAsync(string title, string cancel, string destruction, params string[] buttons) => null;
    public Task<string> DisplayPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel", string placeholder = null, int maxLength = -1) => null;
    protected virtual void OnAppearing() {} protected virtual void OnSizeAllocated(double w, double h) {}
  }
  public class ContentPage : Page { public View Content { get; set; } public SafeAreaEdges SafeAreaEdges { get; set; } }
  public class NavigationPage : Page { public NavigationPage(Page root) {} public Task PushAsync(Page p, bool animated) => null; public Color BarBackgroundColor { get; set; } public Color BarTextColor { get; set; } public static void SetHasNavigationBar(BindableObject p, bool v) {} }
  public class Window { public Window(Page p) {} public Page Page { get; set; } }
  public class Application : Element { public static Application Current => null; public IReadOnlyList<Window> Windows => null; public AppTheme UserAppTheme { get; set; } protected virtual Window CreateWindow(IActivationState s) => null; }
}
namespace Microsoft.Maui.Controls.Shapes {
  public interface IShape {}
  public class Geometry : BindableObject {}
  public enum PenLineCap { Flat, Round } public enum PenLineJoin { Miter, Round }
  public class Shape : View, IShape { public Brush Fill { get; set; } public Brush Stroke { get; set; } public double StrokeThickness { get; set; } public PenLineCap StrokeLineCap { get; set; } public PenLineJoin StrokeLineJoin { get; set; } public Stretch Aspect { get; set; } }
  public sealed class Path : Shape { public Geometry Data { get; set; } }
  public sealed class RoundRectangle : Shape { public CornerRadius CornerRadius { get; set; } }
  public class PathGeometryConverter : TypeConverter {}
}
namespace SkiaSharp {
  public struct SKPoint { public SKPoint(float x, float y) {} public float X, Y; }
  public struct SKRect { public SKRect(float l, float t, float r, float b) {} }
  public struct SKColor { public SKColor(uint v) {} public SKColor(byte r, byte g, byte b) {} public SKColor(byte r, byte g, byte b, byte a) {} }
  public static class SKColors { public static SKColor White, Black; }
  public struct SKImageInfo { public int Width, Height; }
  public enum SKPaintStyle { Fill, Stroke } public enum SKTextAlign { Left, Center, Right }
  public class SKPaint : IDisposable { public bool IsAntialias { get; set; } public SKPaintStyle Style { get; set; } public float StrokeWidth { get; set; } public SKColor Color { get; set; } public void Dispose() {} }
  public class SKTypeface { public static SKTypeface Default => null; }
  public class SKFont : IDisposable { public SKFont(SKTypeface t, float size = 12) {} public float Size { get; set; } public float MeasureText(string text, SKPaint paint = null) => 0; public void Dispose() {} }
  public class SKPathBuilder : IDisposable { public void MoveTo(SKPoint p) {} public void MoveTo(float x, float y) {} public void LineTo(SKPoint p) {} public void LineTo(float x, float y) {} public void Close() {} public void AddArc(SKRect oval, float start, float sweep) {} public SKPath Detach() => null; public void Dispose() {} }
  public class SKPath : IDisposable { public void MoveTo(SKPoint p) {} public void MoveTo(float x, float y) {} public void LineTo(SKPoint p) {} public void LineTo(float x, float y) {} public void Close() {} public void AddArc(SKRect oval, float start, float sweep) {} public void Dispose() {} }
  public class SKCanvas { public void Clear(SKColor c) {} public void DrawLine(SKPoint a, SKPoint b, SKPaint p) {} public void DrawLine(float x0, float y0, float x1, float y1, SKPaint p) {} public void DrawPath(SKPath path, SKPaint p) {} public void DrawCircle(SKPoint c, float r, SKPaint p) {} public void DrawRect(SKRect r, SKPaint p) {} public void DrawRoundRect(SKRect r, float rx, float ry, SKPaint p) {} public void DrawText(string text, float x, float y, SKTextAlign a, SKFont f, SKPaint p) {} }
  public class SKSurface { public SKCanvas Canvas => null; }
}
namespace SkiaSharp.Views.Maui {
  using SkiaSharp;
  public class SKPaintSurfaceEventArgs : EventArgs { public SKSurface Surface => null; public SKImageInfo Info => default; }
  public enum SKTouchAction { Entered, Pressed, Moved, Released, Cancelled, Exited, WheelChanged }
  public class SKTouchEventArgs : EventArgs { public bool Handled { get; set; } public long Id => 0; public SKTouchAction ActionType => default; public SKPoint Location => default; }
}
namespace SkiaSharp.Views.Maui.Controls {
  public class SKCanvasView : View { public bool EnableTouchEvents { get; set; } public event EventHandler<SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs> PaintSurface; public event EventHandler<SkiaSharp.Views.Maui.SKTouchEventArgs> Touch; public void InvalidateSurface() {} }
}
