// Compile-only stand-ins for the WPF and NinjaTrader types src/KeyStone.cs uses, so the WHOLE
// file (engine + lab window + chart) can be compile-checked with mono mcs on Linux.
// Names and signatures mirror the real APIs for the members the file uses. Nothing here runs;
// the real compile is still the user's F5 in NinjaTrader.
using System;
using System.Collections;
using System.Collections.Generic;

namespace System.Windows
{
    public enum Visibility { Visible, Hidden, Collapsed }
    public enum VerticalAlignment { Top, Center, Bottom, Stretch }
    public enum HorizontalAlignment { Left, Center, Right, Stretch }
    public enum ResizeMode { NoResize, CanMinimize, CanResize, CanResizeWithGrip }
    public enum WindowStartupLocation { Manual, CenterScreen, CenterOwner }
    public enum WindowState { Normal, Minimized, Maximized }
    public enum SizeToContent { Manual, Width, Height, WidthAndHeight }
    public enum TextWrapping { WrapWithOverflow, NoWrap, Wrap }
    public enum TextAlignment { Left, Right, Center, Justify }
    public enum TextTrimming { None, CharacterEllipsis, WordEllipsis }
    public enum GridUnitType { Auto, Pixel, Star }
    public enum MessageBoxButton { OK, OKCancel, YesNoCancel, YesNo }
    public enum MessageBoxImage { None, Hand, Question, Exclamation, Asterisk, Stop, Error, Warning, Information }
    public enum MessageBoxResult { None, OK, Cancel, Yes, No }
    public enum FlowDirection { LeftToRight, RightToLeft }
    public struct Thickness
    {
        public Thickness(double u) { Left = Top = Right = Bottom = u; }
        public Thickness(double l, double t, double r, double b) { Left = l; Top = t; Right = r; Bottom = b; }
        public double Left, Top, Right, Bottom;
    }
    public struct CornerRadius { public CornerRadius(double u) { } public CornerRadius(double a, double b, double c, double d) { } }
    public struct GridLength
    {
        public GridLength(double v) { } public GridLength(double v, GridUnitType t) { }
        public static GridLength Auto { get { return new GridLength(); } }
        public double Value { get { return 0; } }
    }
    public struct FontWeight { }
    public static class FontWeights { public static FontWeight Bold { get { return new FontWeight(); } } public static FontWeight Normal { get { return new FontWeight(); } } public static FontWeight SemiBold { get { return new FontWeight(); } } public static FontWeight Light { get { return new FontWeight(); } } public static FontWeight Black { get { return new FontWeight(); } } public static FontWeight Medium { get { return new FontWeight(); } } public static FontWeight ExtraBold { get { return new FontWeight(); } } }
    public struct FontStyle { }
    public static class FontStyles { public static FontStyle Italic { get { return new FontStyle(); } } public static FontStyle Normal { get { return new FontStyle(); } } }
    public struct Point { public Point(double x, double y) { X = x; Y = y; } public double X; public double Y; }
    public struct Size { public Size(double w, double h) { Width = w; Height = h; } public double Width; public double Height; }
    public struct Rect { public Rect(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; } public double X, Y, Width, Height; }
    public class DependencyProperty { }
    public class RoutedEvent { }
    public static class LogicalTreeHelper { public static System.Collections.IEnumerable GetChildren(DependencyObject d) { return new object[0]; } public static DependencyObject GetParent(DependencyObject d) { return null; } }
    public class WindowCollection : List<Window> { }
    public class DependencyObject { public System.Windows.Threading.Dispatcher Dispatcher { get { return null; } } public bool CheckAccess() { return true; } public object GetValue(DependencyProperty p) { return null; } public void SetValue(DependencyProperty p, object v) { } }
    public class RoutedEventArgs : EventArgs { public bool Handled { get; set; } public object Source { get; set; } public object OriginalSource { get; set; } }
    public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);
    public class SizeChangedEventArgs : RoutedEventArgs { public Size NewSize { get; set; } public Size PreviousSize { get; set; } public bool WidthChanged { get; set; } public bool HeightChanged { get; set; } }
    public delegate void SizeChangedEventHandler(object sender, SizeChangedEventArgs e);
    public class Setter { public Setter() { } public Setter(DependencyProperty p, object v) { } }
    public class Style { public Style() { } public Style(Type t) { } public List<Setter> Setters = new List<Setter>(); }
    public class ResourceDictionary : Dictionary<object, object> { }
    public class UIElement : DependencyObject
    {
        // Stand-in for WPF's single logical parent rule: adding an element that already has a parent throws, as WPF does.
        internal object StubParent;
        internal static void Adopt(object parent, object child) { var e = child as UIElement; if (e == null) return; if (e.StubParent != null && !ReferenceEquals(e.StubParent, parent)) throw new InvalidOperationException("Specified element is already the logical child of another element. Disconnect it first. (" + e.GetType().Name + ")"); e.StubParent = parent; }
        internal static void Release(object child) { var e = child as UIElement; if (e != null) e.StubParent = null; }
        public Visibility Visibility { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get { return true; } }
        public double Opacity { get; set; }
        public bool IsHitTestVisible { get; set; }
        public bool Focusable { get; set; }
        public bool IsMouseOver { get { return false; } }
        public bool ClipToBounds { get; set; }
        public Size RenderSize { get; set; }
        public System.Windows.Media.Transform RenderTransform { get; set; }
        public Point RenderTransformOrigin { get; set; }
        public System.Windows.Media.Effects.Effect Effect { get; set; }
        public bool Focus() { return true; }
        public void AddHandler(RoutedEvent e, Delegate h, bool handledEventsToo) { }
        public void AddHandler(RoutedEvent e, Delegate h) { }
        public void RemoveHandler(RoutedEvent e, Delegate h) { }
        public bool CaptureMouse() { return true; }
        public void ReleaseMouseCapture() { }
        public void InvalidateVisual() { }
        public void UpdateLayout() { }
        public void BeginAnimation(DependencyProperty p, System.Windows.Media.Animation.AnimationTimeline a) { }
        public static readonly DependencyProperty OpacityProperty = new DependencyProperty();
        public event System.Windows.Input.MouseButtonEventHandler MouseLeftButtonDown, MouseLeftButtonUp, MouseRightButtonDown, MouseRightButtonUp, MouseDown, MouseUp, PreviewMouseLeftButtonDown, PreviewMouseDown;
        public event System.Windows.Input.MouseEventHandler MouseMove, MouseEnter, MouseLeave, PreviewMouseMove, LostMouseCapture;
        public event System.Windows.Input.MouseWheelEventHandler MouseWheel, PreviewMouseWheel;
        public event System.Windows.Input.KeyEventHandler KeyDown, KeyUp, PreviewKeyDown;
        public event RoutedEventHandler GotFocus, LostFocus;
    }
    public class FrameworkElement : UIElement
    {
        public Thickness Margin { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double MinWidth { get; set; }
        public double MinHeight { get; set; }
        public double MaxWidth { get; set; }
        public double MaxHeight { get; set; }
        public double ActualWidth { get { return 0; } }
        public double ActualHeight { get { return 0; } }
        public object ToolTip { get; set; }
        public object Tag { get; set; }
        public string Name { get; set; }
        public object DataContext { get; set; }
        public Style Style { get; set; }
        public ResourceDictionary Resources { get; set; }
        public System.Windows.Input.Cursor Cursor { get; set; }
        public VerticalAlignment VerticalAlignment { get; set; }
        public HorizontalAlignment HorizontalAlignment { get; set; }
        public DependencyObject Parent { get { return null; } }
        public bool IsLoaded { get { return true; } }
        public System.Windows.Media.Transform LayoutTransform { get; set; }
        public FlowDirection FlowDirection { get; set; }
        public event SizeChangedEventHandler SizeChanged;
        public event RoutedEventHandler Loaded, Unloaded;
        public static readonly DependencyProperty WidthProperty = new DependencyProperty();
        public static readonly DependencyProperty HeightProperty = new DependencyProperty();
        public static readonly DependencyProperty MarginProperty = new DependencyProperty();
    }
    public class Window : System.Windows.Controls.ContentControl
    {
        public string Title { get; set; }
        public ResizeMode ResizeMode { get; set; }
        public WindowStartupLocation WindowStartupLocation { get; set; }
        public WindowState WindowState { get; set; }
        public SizeToContent SizeToContent { get; set; }
        public bool ShowInTaskbar { get; set; }
        public bool Topmost { get; set; }
        public Window Owner { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public System.Windows.Media.ImageSource Icon { get; set; }
        public event EventHandler Closed, Activated, Deactivated, ContentRendered;
        public bool Activate() { return true; }
        public event System.ComponentModel.CancelEventHandler Closing;
        public void Show() { }
        public bool? ShowDialog() { return true; }
        public void Close() { }
        public void Hide() { }
    }
    public static class MessageBox
    {
        public static MessageBoxResult Show(string text) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(string text, string caption) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(string text, string caption, MessageBoxButton b) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(string text, string caption, MessageBoxButton b, MessageBoxImage i) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(Window owner, string text, string caption, MessageBoxButton b, MessageBoxImage i) { return MessageBoxResult.OK; }
    }
    public static class SystemParameters { public static double PrimaryScreenWidth { get { return 1920; } } public static double PrimaryScreenHeight { get { return 1080; } } public static Rect WorkArea { get { return new Rect(); } } public static double MinimumHorizontalDragDistance { get { return 4; } } public static double MinimumVerticalDragDistance { get { return 4; } } }
    public static class Clipboard { public static void SetText(string s) { } }
    public class Application { public WindowCollection Windows { get { return new WindowCollection(); } } public static Application Current { get { return null; } } public System.Windows.Threading.Dispatcher Dispatcher { get { return null; } } public Window MainWindow { get; set; } public object TryFindResource(object key) { return null; } public object FindResource(object key) { return null; } }
}

namespace System.Windows.Media.Effects { public class Effect { } public class DropShadowEffect : Effect { public double BlurRadius { get; set; } public double ShadowDepth { get; set; } public double Opacity { get; set; } public System.Windows.Media.Color Color { get; set; } public double Direction { get; set; } } }

namespace System.Windows.Media
{
    public struct Color
    {
        public byte A, R, G, B;
        public static Color FromRgb(byte r, byte g, byte b) { return new Color { A = 255, R = r, G = g, B = b }; }
        public static Color FromArgb(byte a, byte r, byte g, byte b) { return new Color { A = a, R = r, G = g, B = b }; }
    }
    public static class Colors { public static Color Transparent { get { return new Color(); } } public static Color White { get { return new Color(); } } public static Color Black { get { return new Color(); } } }
    public class Freezable : System.Windows.DependencyObject { public void Freeze() { } public bool IsFrozen { get { return false; } } public bool CanFreeze { get { return true; } } }
    public abstract class Brush : Freezable { public double Opacity { get; set; } public Brush Clone() { return this; } }
    public class SolidColorBrush : Brush { public SolidColorBrush() { } public SolidColorBrush(Color c) { Color = c; } public Color Color { get; set; } public new SolidColorBrush Clone() { return this; } }
    public class GradientStop { public GradientStop() { } public GradientStop(Color c, double o) { } }
    public class GradientStopCollection : List<GradientStop> { }
    public class LinearGradientBrush : Brush { public LinearGradientBrush() { } public LinearGradientBrush(Color a, Color b, double angle) { } public LinearGradientBrush(Color a, Color b, System.Windows.Point s, System.Windows.Point e) { } public GradientStopCollection GradientStops { get; set; } public System.Windows.Point StartPoint { get; set; } public System.Windows.Point EndPoint { get; set; } }
    public static class Brushes
    {
        public static SolidColorBrush Transparent, White, Black, Gray, DimGray, DarkGray, LightGray, Silver, Red, Green, Lime, LimeGreen, Blue, DodgerBlue, DeepSkyBlue, Cyan, Aqua, Turquoise, Gold, Orange, OrangeRed, Yellow, Magenta, MediumOrchid, Orchid, Purple, SlateGray, Crimson, SeaGreen, MediumSeaGreen, SteelBlue, CornflowerBlue, Khaki, Goldenrod, Tomato, SkyBlue, LightSkyBlue, Violet, HotPink, Teal, Navy;
    }
    public class FontFamily { public FontFamily(string n) { } }
    public abstract class Transform : Freezable { }
    public class TranslateTransform : Transform { public TranslateTransform() { } public TranslateTransform(double x, double y) { X = x; Y = y; } public double X { get; set; } public double Y { get; set; } public static readonly System.Windows.DependencyProperty XProperty = new System.Windows.DependencyProperty(); public static readonly System.Windows.DependencyProperty YProperty = new System.Windows.DependencyProperty(); public void BeginAnimation(System.Windows.DependencyProperty p, System.Windows.Media.Animation.AnimationTimeline a) { } }
    public class ScaleTransform : Transform { public ScaleTransform() { } public ScaleTransform(double x, double y) { ScaleX = x; ScaleY = y; } public ScaleTransform(double x, double y, double cx, double cy) { } public double ScaleX { get; set; } public double ScaleY { get; set; } public double CenterX { get; set; } public double CenterY { get; set; } public static readonly System.Windows.DependencyProperty ScaleXProperty = new System.Windows.DependencyProperty(); public static readonly System.Windows.DependencyProperty ScaleYProperty = new System.Windows.DependencyProperty(); public void BeginAnimation(System.Windows.DependencyProperty p, System.Windows.Media.Animation.AnimationTimeline a) { } }
    public class RotateTransform : Transform { public RotateTransform() { } public RotateTransform(double a) { } public double Angle { get; set; } }
    public class TransformGroup : Transform { public List<Transform> Children = new List<Transform>(); }
    public class DoubleCollection : List<double> { public DoubleCollection() { } public DoubleCollection(IEnumerable<double> v) : base(v) { } }
    public enum PenLineCap { Flat, Square, Round, Triangle }
    public enum PenLineJoin { Miter, Bevel, Round }
    public enum Stretch { None, Fill, Uniform, UniformToFill }
    public abstract class Geometry : Freezable { public static Geometry Parse(string s) { return null; } }
    public class PointCollection : List<System.Windows.Point> { }
    public class ImageSource { }
    public static class CompositionTarget { public static event EventHandler Rendering; }
    public static class VisualTreeHelper { public static System.Windows.DependencyObject GetParent(System.Windows.DependencyObject d) { return null; } public static int GetChildrenCount(System.Windows.DependencyObject d) { return 0; } public static System.Windows.DependencyObject GetChild(System.Windows.DependencyObject d, int i) { return null; } }
}

namespace System.Windows.Media.Animation
{
    public enum EasingMode { EaseIn, EaseOut, EaseInOut }
    public interface IEasingFunction { }
    public abstract class EasingFunctionBase : IEasingFunction { public EasingMode EasingMode { get; set; } }
    public class BackEase : EasingFunctionBase { public double Amplitude { get; set; } }
    public class QuadraticEase : EasingFunctionBase { }
    public class CubicEase : EasingFunctionBase { }
    public class SineEase : EasingFunctionBase { }
    public class ElasticEase : EasingFunctionBase { }
    public struct Duration { public Duration(TimeSpan t) { } public static implicit operator Duration(TimeSpan t) { return new Duration(t); } }
    public abstract class AnimationTimeline : System.Windows.Media.Freezable { public Duration Duration { get; set; } public TimeSpan? BeginTime { get; set; } public bool AutoReverse { get; set; } public event EventHandler Completed; }
    public class DoubleAnimation : AnimationTimeline
    {
        public DoubleAnimation() { }
        public DoubleAnimation(double to, Duration d) { }
        public DoubleAnimation(double from, double to, Duration d) { }
        public double? From { get; set; } public double? To { get; set; } public IEasingFunction EasingFunction { get; set; }
    }
}

namespace System.Windows.Input
{
    public enum MouseButtonState { Released, Pressed }
    public enum MouseButton { Left, Middle, Right, XButton1, XButton2 }
    public enum Key { None, Left, Right, Up, Down, Space, Home, End, Escape, Enter, Return, PageUp, PageDown, Add, Subtract, OemPlus, OemMinus, B, C, S }
    [Flags] public enum ModifierKeys { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }
    public class Cursor { }
    public static class Cursors { public static Cursor Arrow, Hand, SizeAll, SizeWE, SizeNS, Cross, IBeam, Wait, ScrollAll, ScrollWE, ScrollNS, None; }
    public class InputEventArgs : System.Windows.RoutedEventArgs { public int Timestamp { get; set; } }
    public class MouseEventArgs : InputEventArgs
    {
        public System.Windows.Point GetPosition(System.Windows.IInputElementCompat relativeTo) { return new System.Windows.Point(); }
        public MouseButtonState LeftButton { get { return MouseButtonState.Released; } }
        public MouseButtonState RightButton { get { return MouseButtonState.Released; } }
        public MouseButtonState MiddleButton { get { return MouseButtonState.Released; } }
    }
    public class MouseButtonEventArgs : MouseEventArgs { public int ClickCount { get { return 1; } } public MouseButton ChangedButton { get { return MouseButton.Left; } } public MouseButtonState ButtonState { get { return MouseButtonState.Pressed; } } }
    public class MouseWheelEventArgs : MouseEventArgs { public int Delta { get { return 0; } } }
    public class KeyEventArgs : InputEventArgs { public Key Key { get { return Key.None; } } public bool IsRepeat { get { return false; } } }
    public delegate void MouseEventHandler(object sender, MouseEventArgs e);
    public delegate void MouseButtonEventHandler(object sender, MouseButtonEventArgs e);
    public delegate void MouseWheelEventHandler(object sender, MouseWheelEventArgs e);
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public static class Mouse { public static readonly System.Windows.RoutedEvent PreviewMouseWheelEvent = new System.Windows.RoutedEvent(); public static readonly System.Windows.RoutedEvent MouseWheelEvent = new System.Windows.RoutedEvent(); public static MouseButtonState LeftButton { get { return MouseButtonState.Released; } } public static MouseButtonState RightButton { get { return MouseButtonState.Released; } } public static System.Windows.Point GetPosition(System.Windows.IInputElementCompat e) { return new System.Windows.Point(); } public static bool Capture(System.Windows.IInputElementCompat e) { return true; } public static System.Windows.IInputElementCompat Captured { get { return null; } } public static void OverrideCursor(Cursor c) { } }
    public static class Keyboard { public static ModifierKeys Modifiers { get { return ModifierKeys.None; } } public static bool IsKeyDown(Key k) { return false; } }
}

namespace System.Windows
{
    // GetPosition takes an IInputElement; UIElement implements it. Modelled with an implicit conversion.
    public class IInputElementCompat { public static implicit operator IInputElementCompat(UIElement e) { return new IInputElementCompat(); } }
}

namespace System.Windows.Threading
{
    public enum DispatcherPriority { Inactive, SystemIdle, ApplicationIdle, ContextIdle, Background, Input, Loaded, Render, DataBind, Normal, Send }
    public class DispatcherOperation { }
    public class Dispatcher
    {
        public bool CheckAccess() { return true; }
        public DispatcherOperation BeginInvoke(Delegate method, params object[] args) { return null; }
        public DispatcherOperation BeginInvoke(DispatcherPriority p, Delegate method) { return null; }
        public DispatcherOperation BeginInvoke(Action a) { return null; }
        public DispatcherOperation BeginInvoke(Action a, DispatcherPriority p) { return null; }
        public void Invoke(Action a) { a(); }
        public object Invoke(Delegate method, params object[] args) { return null; }
        public void InvokeAsync(Action a) { }
        public void InvokeAsync(Action a, DispatcherPriority p) { }
    }
    public class DispatcherTimer
    {
        public DispatcherTimer() { }
        public DispatcherTimer(DispatcherPriority p) { }
        public DispatcherTimer(DispatcherPriority p, Dispatcher d) { }
        public TimeSpan Interval { get; set; } public bool IsEnabled { get; set; } public object Tag { get; set; }
        public event EventHandler Tick; public void Start() { } public void Stop() { }
    }
}

namespace System.Windows.Documents
{
    public abstract class Inline : System.Windows.DependencyObject { public System.Windows.Media.Brush Foreground { get; set; } public System.Windows.Media.Brush Background { get; set; } public System.Windows.FontWeight FontWeight { get; set; } public double FontSize { get; set; } public System.Windows.Media.FontFamily FontFamily { get; set; } public System.Windows.FontStyle FontStyle { get; set; } }
    public class Run : Inline { public Run() { } public Run(string t) { Text = t; } public string Text { get; set; } }
    public class LineBreak : Inline { }
    public class Span : Inline { public List<Inline> Inlines = new List<Inline>(); }
    public class Bold : Span { public Bold() { } public Bold(Inline i) { } }
    public class InlineCollection : List<Inline> { public void Add(string s) { base.Add(new Run(s)); } }
}

namespace System.Windows.Controls
{
    using System.Windows; using System.Windows.Media;
    public enum ScrollBarVisibility { Disabled, Auto, Hidden, Visible }
    public enum Dock { Left, Top, Right, Bottom }
    public enum Orientation { Horizontal, Vertical }
    public enum SelectionMode { Single, Multiple, Extended }
    public class UIElementCollection : List<UIElement>
    {
        internal object Owner;
        public new int Add(UIElement e) { UIElement.Adopt(Owner, e); base.Add(e); return Count - 1; }
        public new void Insert(int i, UIElement e) { UIElement.Adopt(Owner, e); base.Insert(i, e); }
        public new bool Remove(UIElement e) { UIElement.Release(e); return base.Remove(e); }
        public new void RemoveAt(int i) { UIElement.Release(this[i]); base.RemoveAt(i); }
        public new void Clear() { foreach (var e in this) UIElement.Release(e); base.Clear(); }
    }
    public class ItemCollection : List<object> { public new int Add(object o) { base.Add(o); return Count - 1; } }
    public class Panel : FrameworkElement
    {
        private readonly UIElementCollection children = new UIElementCollection();
        public UIElementCollection Children { get { children.Owner = this; return children; } }
        public Brush Background { get; set; }
        public static void SetZIndex(UIElement e, int z) { }
    }
    public class RowDefinition { public GridLength Height { get; set; } public double MinHeight { get; set; } public double MaxHeight { get; set; } public double ActualHeight { get { return 0; } } }
    public class ColumnDefinition { public GridLength Width { get; set; } public double MinWidth { get; set; } public double MaxWidth { get; set; } public double ActualWidth { get { return 0; } } }
    public class RowDefinitionCollection : List<RowDefinition> { }
    public class ColumnDefinitionCollection : List<ColumnDefinition> { }
    public class Grid : Panel
    {
        private readonly RowDefinitionCollection rows = new RowDefinitionCollection();
        private readonly ColumnDefinitionCollection cols = new ColumnDefinitionCollection();
        public RowDefinitionCollection RowDefinitions { get { return rows; } }
        public ColumnDefinitionCollection ColumnDefinitions { get { return cols; } }
        public static void SetRow(UIElement e, int r) { } public static void SetColumn(UIElement e, int c) { } public static void SetRowSpan(UIElement e, int r) { } public static void SetColumnSpan(UIElement e, int c) { }
        public static int GetRow(UIElement e) { return 0; } public static int GetColumn(UIElement e) { return 0; }
    }
    public class StackPanel : Panel { public Orientation Orientation { get; set; } }
    public class WrapPanel : Panel { public Orientation Orientation { get; set; } public double ItemWidth { get; set; } public double ItemHeight { get; set; } }
    public class DockPanel : Panel { public bool LastChildFill { get; set; } public static void SetDock(UIElement e, Dock d) { } }
    public class Canvas : Panel { public static void SetLeft(UIElement e, double v) { } public static void SetTop(UIElement e, double v) { } public static void SetRight(UIElement e, double v) { } public static void SetBottom(UIElement e, double v) { } public static double GetLeft(UIElement e) { return 0; } public static double GetTop(UIElement e) { return 0; } }
    public class Control : FrameworkElement
    {
        public Brush Background { get; set; } public Brush Foreground { get; set; } public Brush BorderBrush { get; set; }
        public Thickness BorderThickness { get; set; } public Thickness Padding { get; set; }
        public FontFamily FontFamily { get; set; } public double FontSize { get; set; } public FontWeight FontWeight { get; set; } public FontStyle FontStyle { get; set; }
        public HorizontalAlignment HorizontalContentAlignment { get; set; } public VerticalAlignment VerticalContentAlignment { get; set; }
        public int TabIndex { get; set; } public bool IsTabStop { get; set; }
        public event MouseButtonEventHandlerCompat MouseDoubleClick;
    }
    public delegate void MouseButtonEventHandlerCompat(object sender, System.Windows.Input.MouseButtonEventArgs e);
    public class ContentControl : Control { private object content; public object Content { get { return content; } set { if (ReferenceEquals(content, value)) return; UIElement.Release(content); UIElement.Adopt(this, value); content = value; } } }
    public class UserControl : ContentControl { }
    public class Label : ContentControl { }
    public class ToolTip : ContentControl { }
    public class HeaderedContentControl : ContentControl { public object Header { get; set; } }
    public class GroupBox : HeaderedContentControl { }
    public class Expander : HeaderedContentControl { public bool IsExpanded { get; set; } }
    public class TabItem : HeaderedContentControl { public bool IsSelected { get; set; } }
    public class ScrollChangedEventArgs : RoutedEventArgs { public double VerticalOffset { get; set; } public double HorizontalOffset { get; set; } public double VerticalChange { get; set; } public double HorizontalChange { get; set; } public double ExtentHeight { get; set; } public double ViewportHeight { get; set; } }
    public delegate void ScrollChangedEventHandler(object sender, ScrollChangedEventArgs e);
    public class ScrollViewer : ContentControl
    {
        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
        public double VerticalOffset { get { return 0; } } public double HorizontalOffset { get { return 0; } }
        public double ExtentHeight { get { return 0; } } public double ExtentWidth { get { return 0; } } public double ViewportHeight { get { return 0; } } public double ViewportWidth { get { return 0; } }
        public double ScrollableHeight { get { return 0; } } public double ScrollableWidth { get { return 0; } }
        public bool CanContentScroll { get; set; }
        public void ScrollToVerticalOffset(double o) { } public void ScrollToHorizontalOffset(double o) { } public void ScrollToTop() { } public void ScrollToBottom() { } public void ScrollToHome() { } public void ScrollToEnd() { } public void LineUp() { } public void LineDown() { } public void PageUp() { } public void PageDown() { }
        public event ScrollChangedEventHandler ScrollChanged;
        public static void SetVerticalScrollBarVisibility(DependencyObject d, ScrollBarVisibility v) { } public static void SetHorizontalScrollBarVisibility(DependencyObject d, ScrollBarVisibility v) { }
        public static void SetCanContentScroll(DependencyObject d, bool v) { }
    }
    public class ButtonBase : ContentControl { public event RoutedEventHandler Click; }
    public class Button : ButtonBase { public bool IsDefault { get; set; } public bool IsCancel { get; set; } }
    public class CheckBox : System.Windows.Controls.Primitives.ToggleButton { }
    public class RadioButton : System.Windows.Controls.Primitives.ToggleButton { public string GroupName { get; set; } }
    public class TextChangedEventArgs : RoutedEventArgs { }
    public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);
    public class TextBox : Control
    {
        public string Text { get; set; } public bool IsReadOnly { get; set; } public TextWrapping TextWrapping { get; set; } public TextAlignment TextAlignment { get; set; }
        public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
        public bool AcceptsReturn { get; set; } public int MaxLength { get; set; } public Brush CaretBrush { get; set; } public Brush SelectionBrush { get; set; }
        public int CaretIndex { get; set; } public void SelectAll() { } public void ScrollToEnd() { } public void ScrollToHome() { } public void ScrollToLine(int l) { }
        public event TextChangedEventHandler TextChanged;
    }
    public class TextBlock : FrameworkElement
    {
        public TextBlock() { } public TextBlock(System.Windows.Documents.Inline i) { }
        public string Text { get; set; } public Brush Foreground { get; set; } public Brush Background { get; set; }
        public double FontSize { get; set; } public FontWeight FontWeight { get; set; } public FontFamily FontFamily { get; set; } public FontStyle FontStyle { get; set; }
        public TextWrapping TextWrapping { get; set; } public TextAlignment TextAlignment { get; set; } public TextTrimming TextTrimming { get; set; } public Thickness Padding { get; set; } public double LineHeight { get; set; }
        private readonly System.Windows.Documents.InlineCollection inlines = new System.Windows.Documents.InlineCollection();
        public System.Windows.Documents.InlineCollection Inlines { get { return inlines; } }
    }
    public class SelectionChangedEventArgs : RoutedEventArgs { public IList AddedItems { get { return new List<object>(); } } public IList RemovedItems { get { return new List<object>(); } } }
    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);
    public class ItemsControl : Control
    {
        private readonly ItemCollection items = new ItemCollection();
        public ItemCollection Items { get { return items; } }
        public IEnumerable ItemsSource { get; set; }
        public Style ItemContainerStyle { get; set; }
        public string DisplayMemberPath { get; set; }
    }
    public class Selector : ItemsControl
    {
        public object SelectedItem { get; set; } public int SelectedIndex { get; set; } public object SelectedValue { get; set; }
        public event SelectionChangedEventHandler SelectionChanged;
    }
    public class ComboBox : Selector { public bool IsEditable { get; set; } public bool IsDropDownOpen { get; set; } public string Text { get; set; } public double MaxDropDownHeight { get; set; } }
    public class ComboBoxItem : ContentControl { public bool IsSelected { get; set; } }
    public class ListBox : Selector { public SelectionMode SelectionMode { get; set; } public void ScrollIntoView(object o) { } public IList SelectedItems { get { return new List<object>(); } } }
    public class ListBoxItem : ContentControl { public bool IsSelected { get; set; } }
    public class TabControl : Selector { public Dock TabStripPlacement { get; set; } }
    public class Menu : ItemsControl { }
    public class MenuItem : HeaderedItemsControlCompat { public event RoutedEventHandler Click; public object Icon { get; set; } }
    public class HeaderedItemsControlCompat : ItemsControl { public object Header { get; set; } }
    public class Separator : Control { }
    public class Border : FrameworkElement
    {
        public Brush Background { get; set; } public Brush BorderBrush { get; set; } public Thickness BorderThickness { get; set; }
        public CornerRadius CornerRadius { get; set; } public Thickness Padding { get; set; }
        private UIElement child; public UIElement Child { get { return child; } set { if (ReferenceEquals(child, value)) return; UIElement.Release(child); UIElement.Adopt(this, value); child = value; } }
    }
    public class Viewbox : FrameworkElement { public UIElement Child { get; set; } public Stretch Stretch { get; set; } }
    public class Image : FrameworkElement { public ImageSource Source { get; set; } public Stretch Stretch { get; set; } }
    public class ProgressBar : System.Windows.Controls.Primitives.RangeBase { public bool IsIndeterminate { get; set; } }
    public class Slider : System.Windows.Controls.Primitives.RangeBase { public double TickFrequency { get; set; } public bool IsSnapToTickEnabled { get; set; } }
    public static class ToolTipService { public static void SetShowDuration(DependencyObject d, int v) { } public static void SetInitialShowDelay(DependencyObject d, int v) { } }
}

namespace System.Windows.Controls.Primitives
{
    using System.Windows; using System.Windows.Controls;
    public class UniformGrid : Panel { public int Columns { get; set; } public int Rows { get; set; } }
    public class ToggleButton : ButtonBase { public bool? IsChecked { get; set; } public bool IsThreeState { get; set; } public event RoutedEventHandler Checked, Unchecked; }
    public class RangeBase : Control { public double Minimum { get; set; } public double Maximum { get; set; } public double Value { get; set; } public double SmallChange { get; set; } public double LargeChange { get; set; } public event RoutedPropertyChangedEventHandlerDouble ValueChanged; }
    public delegate void RoutedPropertyChangedEventHandlerDouble(object sender, RoutedPropertyChangedEventArgsDouble e);
    public class RoutedPropertyChangedEventArgsDouble : RoutedEventArgs { public double NewValue { get; set; } public double OldValue { get; set; } }
    public class ScrollBar : RangeBase { public Orientation Orientation { get; set; } public double ViewportSize { get; set; } public event ScrollEventHandler Scroll; }
    public class ScrollEventArgs : RoutedEventArgs { public double NewValue { get; set; } }
    public delegate void ScrollEventHandler(object sender, ScrollEventArgs e);
    public class RepeatButton : ButtonBase { public int Delay { get; set; } public int Interval { get; set; } }
    public class Popup : FrameworkElement { public bool IsOpen { get; set; } public UIElement Child { get; set; } }
}

namespace System.Windows.Shapes
{
    using System.Windows; using System.Windows.Media;
    public abstract class Shape : FrameworkElement
    {
        public Brush Fill { get; set; } public Brush Stroke { get; set; } public double StrokeThickness { get; set; }
        public DoubleCollection StrokeDashArray { get; set; } public PenLineCap StrokeStartLineCap { get; set; } public PenLineCap StrokeEndLineCap { get; set; } public PenLineCap StrokeDashCap { get; set; } public PenLineJoin StrokeLineJoin { get; set; }
        public Stretch Stretch { get; set; }
    }
    public class Line : Shape { public double X1 { get; set; } public double Y1 { get; set; } public double X2 { get; set; } public double Y2 { get; set; } }
    public class Rectangle : Shape { public double RadiusX { get; set; } public double RadiusY { get; set; } }
    public class Ellipse : Shape { }
    public class Path : Shape { public Geometry Data { get; set; } }
    public class Polyline : Shape { public PointCollection Points { get; set; } }
    public class Polygon : Shape { public PointCollection Points { get; set; } }
}

namespace System.ComponentModel.DataAnnotations
{
    [AttributeUsage(AttributeTargets.All)] public sealed class RangeAttribute : Attribute { public RangeAttribute(int a, int b) { } public RangeAttribute(double a, double b) { } }
    [AttributeUsage(AttributeTargets.All)] public sealed class DisplayAttribute : Attribute { public string Name { get; set; } public string GroupName { get; set; } public int Order { get; set; } public string Description { get; set; } }
}
