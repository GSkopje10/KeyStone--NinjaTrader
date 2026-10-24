// SpasiX — the look: the colours, the buttons (a soft glow under the mouse), our own dialogs (never the grey system box), and our own MENU
// (right-click and the ▾ buttons): dark glass, rounded, a cyan → violet edge, check marks / colour dots, sub-menus that open on hover.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace KeystoneMT5
{
    public static class Sx
    {
        public static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static SolidColorBrush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
        public static SolidColorBrush Tint(SolidColorBrush b, byte a) { var c = b.Color; var t = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); t.Freeze(); return t; }
        public static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        public static readonly SolidColorBrush Bg = B("#080C14"), Panel = B("#0D131F"), Card = B("#131B2A"), Card2 = B("#19233A"), Line = B("#22304A"), Text = B("#E8ECF4"), Muted = B("#8590A6"),
            Gold = B("#FFC94D"), Green = B("#2EE59D"), Red = B("#FF4D6D"), Cyan = B("#4DD8FF"), Violet = B("#A98BFF"), Orange = B("#FF9F43"), Amber = B("#FFB020"), White = B("#FFFFFF");
        public static readonly FontFamily Mono = new FontFamily("Consolas"), Face = new FontFamily("Segoe UI");

        // modern scrollbars everywhere: thin, rounded, no arrows; they widen and glow under the mouse
        public static void ModernScrollbars(Application app)
        {
            const string x = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='SxPage' TargetType='RepeatButton'><Setter Property='Focusable' Value='False'/><Setter Property='IsTabStop' Value='False'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></Setter.Value></Setter></Style>
  <Style x:Key='SxThumb' TargetType='Thumb'><Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Thumb'>
      <Border x:Name='t' CornerRadius='5' Background='#3A4B6E' Margin='1'/>
      <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='t' Property='Background' Value='#4DD8FF'/></Trigger>
        <Trigger Property='IsDragging' Value='True'><Setter TargetName='t' Property='Background' Value='#FFC94D'/></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter></Style>
  <Style TargetType='ScrollBar'><Setter Property='Background' Value='Transparent'/><Setter Property='Width' Value='10'/><Setter Property='MinWidth' Value='10'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>
      <Border x:Name='bg' Background='#0F1828' CornerRadius='5' Opacity='0.85'>
        <Track x:Name='PART_Track' IsDirectionReversed='True'>
          <Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource SxPage}' Command='ScrollBar.PageUpCommand'/></Track.DecreaseRepeatButton>
          <Track.Thumb><Thumb Style='{StaticResource SxThumb}' MinHeight='28'/></Track.Thumb>
          <Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource SxPage}' Command='ScrollBar.PageDownCommand'/></Track.IncreaseRepeatButton>
        </Track></Border>
      <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bg' Property='Background' Value='#16223A'/></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
    <Style.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='MinWidth' Value='0'/><Setter Property='Height' Value='10'/><Setter Property='MinHeight' Value='10'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>
        <Border x:Name='bg' Background='#0F1828' CornerRadius='5' Opacity='0.85'>
          <Track x:Name='PART_Track' IsDirectionReversed='False'>
            <Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource SxPage}' Command='ScrollBar.PageLeftCommand'/></Track.DecreaseRepeatButton>
            <Track.Thumb><Thumb Style='{StaticResource SxThumb}' MinWidth='28'/></Track.Thumb>
            <Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource SxPage}' Command='ScrollBar.PageRightCommand'/></Track.IncreaseRepeatButton>
          </Track></Border>
      </ControlTemplate></Setter.Value></Setter></Trigger></Style.Triggers></Style>
</ResourceDictionary>";
            try { app.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(x)); } catch { }
        }
        // a dark title bar in our colours (Windows 11; Windows 10 gets the dark one; older: nothing changes)
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        public static void DarkTitle(Window w)
        {
            w.SourceInitialized += delegate
            {
                try
                {
                    var h = new System.Windows.Interop.WindowInteropHelper(w).Handle; int on = 1, cap = 0x001F130D, txt = 0x00F4ECE8, edge = 0x004A3022;
                    DwmSetWindowAttribute(h, 20, ref on, 4); DwmSetWindowAttribute(h, 35, ref cap, 4); DwmSetWindowAttribute(h, 36, ref txt, 4); DwmSetWindowAttribute(h, 34, ref edge, 4);
                }
                catch { }
            };
        }

        // ---- buttons
        static ControlTemplate pillT;
        static ControlTemplate PillTemplate()
        {
            if (pillT != null) return pillT;
            var bd = new FrameworkElementFactory(typeof(Border), "bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var g = new FrameworkElementFactory(typeof(Grid));
            var hv = new FrameworkElementFactory(typeof(Border), "hv"); hv.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); hv.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var sheen = new FrameworkElementFactory(typeof(Border)); sheen.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); sheen.SetValue(Border.BackgroundProperty, new LinearGradientBrush(Color.FromArgb(26, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90)); sheen.SetValue(UIElement.IsHitTestVisibleProperty, false);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            g.AppendChild(sheen); g.AppendChild(hv); g.AppendChild(cp); bd.AppendChild(g);
            var t = new ControlTemplate(typeof(Button)) { VisualTree = bd };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), "hv"));
            over.Setters.Add(new Setter(UIElement.EffectProperty, new DropShadowEffect { Color = C("#4DD8FF"), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 }, "bd"));
            var press = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true }; press.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), "hv"));
            var off = new Trigger { Property = UIElement.IsEnabledProperty, Value = false }; off.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "bd"));
            t.Triggers.Add(over); t.Triggers.Add(press); t.Triggers.Add(off);
            pillT = t; return t;
        }
        public static Button Pill(string text, Brush bg)
        {
            return new Button { Content = text, Foreground = Text, Background = bg, BorderBrush = Line, BorderThickness = new Thickness(1), Height = 30, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Bold, FontSize = 12, Cursor = Cursors.Hand, Template = PillTemplate(), Focusable = false };
        }
        // a button that opens a menu under itself
        public static Button Drop(string text, Brush bg, Func<List<Mi>> items)
        {
            var b = Pill(text + "  ▾", bg);
            b.Click += delegate { SxMenu.Show(b, new Point(0, b.ActualHeight + 4), items()); };
            return b;
        }
        public static TextBlock Label(string t, double size, Brush fg, bool bold = false) { return new TextBlock { Text = t, FontSize = size, Foreground = fg, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center }; }
        public static TextBox Input(string t, double w)
        {
            return new TextBox { Text = t, Width = w, Height = 28, FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = Mono, Background = Bg, Foreground = Text, BorderBrush = Line, CaretBrush = Gold, SelectionBrush = Tint(Cyan, 120), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 6, 0) };
        }

        // ---- dialogs (Enter = the main button, Esc = the other)
        public static bool Ask(Window owner, string title, string text, string yes = "YES", string no = "NO", SolidColorBrush tone = null, string big = null)
        {
            return Dialog(owner, title, text, yes, no, tone ?? Gold, big, null) == 1;
        }
        public static void Info(Window owner, string title, string text, SolidColorBrush tone = null) { Dialog(owner, title, text, "OK", null, tone ?? Cyan, null, null); }
        public static string Prompt(Window owner, string title, string text, string initial)
        {
            var box = new TextBox { Text = initial ?? "", FontSize = 14, Height = 32, Background = Bg, Foreground = Text, BorderBrush = Gold, CaretBrush = Gold, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 10, 0, 0), MinWidth = 340 };
            return Dialog(owner, title, text, "OK", "CANCEL", Gold, null, box) == 1 ? box.Text : null;
        }
        // a dialog with our own content under the text (a lots box, a small form …); true = the main button
        // a modern choice window: the logo, a title, a line, optional tiles, and several buttons (the first = Enter); returns the index, −1 = Esc
        public static int Choice(Window owner, string title, string text, FrameworkElement tiles, string[] buttons, SolidColorBrush[] tones)
        {
            int result = -1;
            var w = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, Title = "SpasiX", FontFamily = Face };
            try { if (owner != null && owner.IsLoaded) { w.Owner = owner; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; } else w.WindowStartupLocation = WindowStartupLocation.CenterScreen; } catch { w.WindowStartupLocation = WindowStartupLocation.CenterScreen; }
            var sp = new StackPanel { Width = 470 };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var lg = MainWindow.Logo(); if (lg != null) head.Children.Add(new Image { Source = lg, Width = 46, Height = 46, Margin = new Thickness(0, 0, 14, 0) });
            var ht = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nm = new StackPanel { Orientation = Orientation.Horizontal }; nm.Children.Add(new TextBlock { Text = "Spasi", Foreground = Gold, FontSize = 13, FontWeight = FontWeights.Bold }); nm.Children.Add(new TextBlock { Text = "X", Foreground = Cyan, FontSize = 13, FontWeight = FontWeights.Bold }); ht.Children.Add(nm);
            ht.Children.Add(new TextBlock { Text = title, Foreground = Text, FontSize = 20, FontWeight = FontWeights.Bold }); head.Children.Add(ht); sp.Children.Add(head);
            if (!string.IsNullOrEmpty(text)) sp.Children.Add(new TextBlock { Text = text, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, LineHeight = 19, Margin = new Thickness(0, 0, 0, 10) });
            if (tiles != null) sp.Children.Add(tiles);
            var bs = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            for (int i = 0; i < buttons.Length; i++)
            {
                int ii = i; var tone = tones != null && i < tones.Length && tones[i] != null ? tones[i] : Card; bool main = i == 0;
                var b = Pill(buttons[i], main ? (Brush)tone : Tint(tone, 46)); b.Height = main ? 44 : 38; b.FontSize = main ? 14.5 : 13; b.Margin = new Thickness(0, 0, 0, 7); b.HorizontalAlignment = HorizontalAlignment.Stretch;
                b.Foreground = main ? (tone == Red ? White : Bg) : tone == Card ? Text : tone; b.BorderBrush = Tint(tone, 160); b.Click += delegate { result = ii; w.Close(); }; bs.Children.Add(b);
            }
            sp.Children.Add(bs);
            sp.Children.Add(new TextBlock { Text = "Enter = " + buttons[0].Trim() + "   •   Esc = cancel", Foreground = Muted, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
            var edge = tones != null && tones.Length > 0 && tones[0] != null ? tones[0] : Gold;
            var card = new Border { CornerRadius = new CornerRadius(20), Background = new LinearGradientBrush(C("#17213A"), C("#090D17"), 90), BorderThickness = new Thickness(1.4), BorderBrush = new LinearGradientBrush(edge.Color, C("#4DD8FF"), 35), Padding = new Thickness(26, 22, 26, 18), Margin = new Thickness(24), Child = sp,
                Effect = new DropShadowEffect { Color = edge.Color, BlurRadius = 34, ShadowDepth = 0, Opacity = 0.5 }, RenderTransform = new ScaleTransform(0.96, 0.96), RenderTransformOrigin = new Point(0.5, 0.5) };
            card.MouseLeftButtonDown += delegate { try { w.DragMove(); } catch { } };
            w.Content = card;
            w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { result = -1; w.Close(); e.Handled = true; } else if (e.Key == Key.Enter) { result = 0; w.Close(); e.Handled = true; } };
            w.Loaded += delegate { card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))); var an = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }; ((ScaleTransform)card.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, an); ((ScaleTransform)card.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, an); };
            try { w.ShowDialog(); } catch { return MessageBox.Show(text, title, MessageBoxButton.YesNo) == MessageBoxResult.Yes ? 0 : -1; }
            return result;
        }
        // small tiles for Choice: caption + value, coloured
        public static FrameworkElement Tiles(params Tuple<string, string, SolidColorBrush>[] t)
        {
            var g = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1 };
            foreach (var x in t)
            {
                var st = new StackPanel(); st.Children.Add(new TextBlock { Text = x.Item1, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold }); st.Children.Add(new TextBlock { Text = x.Item2, Foreground = x.Item3, FontSize = 15, FontWeight = FontWeights.Bold, FontFamily = Mono, TextTrimming = TextTrimming.CharacterEllipsis });
                g.Children.Add(new Border { Child = st, Background = Tint(x.Item3, 22), BorderBrush = Tint(x.Item3, 110), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 5, 10, 6), Margin = new Thickness(0, 0, 6, 0) });
            }
            return g;
        }
        static TextBox FirstBox(DependencyObject d)
        {
            if (d == null) return null; var p = d as System.Windows.Controls.Panel; if (p != null) foreach (UIElement c in p.Children) { var t = c as TextBox ?? FirstBox(c); if (t != null) return t; }
            var b = d as Decorator; if (b != null) return b.Child as TextBox ?? FirstBox(b.Child); return null;
        }
        public static bool Form(Window owner, string title, string text, FrameworkElement content, string yes, string no, SolidColorBrush tone = null)
        {
            return Dialog(owner, title, text, yes, no, tone ?? Gold, null, content) == 1;
        }
        static int Dialog(Window owner, string title, string text, string yes, string no, SolidColorBrush tone, string big, FrameworkElement extra)
        {
            int result = 0;
            var w = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = owner == null, Topmost = owner == null, Title = "SpasiX", FontFamily = Face };
            try { if (owner != null && owner.IsLoaded) { w.Owner = owner; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; } else w.WindowStartupLocation = WindowStartupLocation.CenterScreen; } catch { w.WindowStartupLocation = WindowStartupLocation.CenterScreen; }
            var sp = new StackPanel { MaxWidth = extra != null && extra.MinWidth > 500 ? extra.MinWidth + 20 : 520 };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            head.Children.Add(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Tint(tone, 50), BorderBrush = tone, BorderThickness = new Thickness(1.4), Margin = new Thickness(0, 0, 10, 0), Child = new TextBlock { Text = "S", Foreground = tone, FontWeight = FontWeights.Bold, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            head.Children.Add(new TextBlock { Text = title, Foreground = tone, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 });
            sp.Children.Add(head);
            if (!string.IsNullOrEmpty(big))
            {
                var bt = new TextBlock { Text = big, Foreground = tone, FontSize = 19, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
                bt.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.55, TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }); sp.Children.Add(bt);
            }
            sp.Children.Add(new TextBlock { Text = text, Foreground = Text, FontSize = 13.5, TextWrapping = TextWrapping.Wrap, LineHeight = 20 });
            if (extra != null) sp.Children.Add(extra);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            if (no != null) { var nb = Pill(no, Card); nb.MinWidth = 96; nb.Height = 34; nb.Click += delegate { result = 0; w.Close(); }; btns.Children.Add(nb); }
            var yb = Pill(yes, tone); yb.MinWidth = 110; yb.Height = 34; yb.Foreground = Bg; yb.Margin = new Thickness(4, 0, 0, 0); yb.Click += delegate { result = 1; w.Close(); }; btns.Children.Add(yb);
            sp.Children.Add(btns);
            var card = new Border { CornerRadius = new CornerRadius(16), Background = new LinearGradientBrush(C("#141D2E"), C("#0A0F19"), 90), BorderBrush = tone, BorderThickness = new Thickness(1.4), Padding = new Thickness(22, 18, 22, 18), Margin = new Thickness(18), Child = sp,
                Effect = new DropShadowEffect { Color = tone.Color, BlurRadius = 26, ShadowDepth = 0, Opacity = 0.45 } };
            card.MouseLeftButtonDown += delegate { try { w.DragMove(); } catch { } };
            w.Content = card;
            w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { result = 0; w.Close(); e.Handled = true; } else if (e.Key == Key.Enter && !(extra is TextBox && false)) { result = 1; w.Close(); e.Handled = true; } };
            w.Loaded += delegate { card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))); var tb = extra as TextBox ?? FirstBox(extra); if (tb != null) { tb.Focus(); tb.SelectAll(); } };
            try { w.ShowDialog(); } catch { return MessageBox.Show(text, title, no != null ? MessageBoxButton.YesNo : MessageBoxButton.OK) == MessageBoxResult.Yes ? 1 : 0; }
            return result;
        }
    }

    // one line of a menu
    public sealed class Mi
    {
        public string Icon = "", Text = "", Hint = ""; public bool? Check; public bool Radio; public Brush Dot, Fg; public Action Do; public List<Mi> Sub; public bool Sep, Head, Off, Danger, KeepOpen;
        public Mi() { }
        public Mi(string icon, string text, Action a) { Icon = icon; Text = text; Do = a; }
        public static Mi H(string t) { return new Mi { Text = t, Head = true }; }
        public static Mi S() { return new Mi { Sep = true }; }
        public static Mi Group(string icon, string text, List<Mi> sub) { return new Mi { Icon = icon, Text = text, Sub = sub }; }
    }

    // the menu: drawn on a layer over the window (no system popups), closes on a click outside / Esc / when the window loses focus
    public static class SxMenu
    {
        static readonly Dictionary<Window, Canvas> hosts = new Dictionary<Window, Canvas>();
        static readonly Dictionary<Window, List<Border>> levels = new Dictionary<Window, List<Border>>();
        public static void Attach(Window w, Grid root)
        {
            var c = new Canvas { IsHitTestVisible = false, Background = null };
            Grid.SetRowSpan(c, 50); Grid.SetColumnSpan(c, 50); System.Windows.Controls.Panel.SetZIndex(c, 1000); root.Children.Add(c);
            hosts[w] = c; levels[w] = new List<Border>();
            c.MouseDown += (s, e) => { if (ReferenceEquals(e.OriginalSource, c)) { Close(w); e.Handled = true; } };
            w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && IsOpen(w)) { Close(w); e.Handled = true; } };
            w.Deactivated += delegate { Close(w); };
        }
        public static bool IsOpen(Window w) { List<Border> l; return w != null && levels.TryGetValue(w, out l) && l.Count > 0; }
        public static void Close(Window w)
        {
            Canvas c; if (w == null || !hosts.TryGetValue(w, out c)) return;
            c.Children.Clear(); levels[w].Clear(); c.Background = null; c.IsHitTestVisible = false;
        }
        public static void Show(FrameworkElement anchor, Point at, List<Mi> items)
        {
            var w = Window.GetWindow(anchor); Canvas c; if (w == null || !hosts.TryGetValue(w, out c) || items == null || items.Count == 0) return;
            Close(w); c.Background = Brushes.Transparent; c.IsHitTestVisible = true;
            Point p; try { p = anchor.TranslatePoint(at, c); } catch { p = at; }
            Open(w, c, items, p, 0, null);
        }
        static void Open(Window w, Canvas c, List<Mi> items, Point at, int level, Border parentRow)
        {
            var lv = levels[w]; while (lv.Count > level) { c.Children.Remove(lv[lv.Count - 1]); lv.RemoveAt(lv.Count - 1); }
            var sp = new StackPanel { MinWidth = 210 };
            foreach (var it in items) sp.Children.Add(Row(w, c, it, level));
            FrameworkElement content = sp;
            double maxH = Math.Max(200, c.ActualHeight - 24);
            var sv = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = maxH }; content = sv;
            var panel = new Border
            {
                CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromArgb(246, 12, 18, 30)), BorderThickness = new Thickness(1.3),
                BorderBrush = new LinearGradientBrush(Sx.C("#4DD8FF"), Sx.C("#A98BFF"), 0), Padding = new Thickness(5, 6, 5, 6), Child = content,
                Effect = new DropShadowEffect { Color = Sx.C("#4DD8FF"), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.32 }, RenderTransform = new TranslateTransform(0, 6)
            };
            TextOptions.SetTextFormattingMode(panel, TextFormattingMode.Display);
            c.Children.Add(panel); lv.Add(panel);
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); var sz = panel.DesiredSize;
            double x = at.X, y = at.Y;
            if (parentRow != null)
            {
                var pr = lv[level - 1]; Point rp; try { rp = parentRow.TranslatePoint(new Point(0, 0), c); } catch { rp = at; }
                x = Canvas.GetLeft(pr) + pr.ActualWidth - 6; y = rp.Y - 6;
                if (x + sz.Width > c.ActualWidth - 4) x = Canvas.GetLeft(pr) - sz.Width + 6;
            }
            if (x + sz.Width > c.ActualWidth - 4) x = Math.Max(4, c.ActualWidth - sz.Width - 4);
            if (y + sz.Height > c.ActualHeight - 4) y = Math.Max(4, c.ActualHeight - sz.Height - 4);
            Canvas.SetLeft(panel, Math.Max(4, x)); Canvas.SetTop(panel, Math.Max(4, y));
            panel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)));
            ((TranslateTransform)panel.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(130)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        static FrameworkElement Row(Window w, Canvas c, Mi it, int level)
        {
            if (it.Sep) return new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(60, 0x4D, 0xD8, 0xFF)), Margin = new Thickness(10, 5, 10, 5) };
            if (it.Head) return new TextBlock { Text = it.Text.ToUpperInvariant(), Foreground = Sx.Muted, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 6, 12, 3) };
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            FrameworkElement icon;
            if (it.Check.HasValue)
            {
                bool on = it.Check.Value;
                icon = it.Radio
                    ? (FrameworkElement)new Border { Width = 13, Height = 13, CornerRadius = new CornerRadius(7), BorderBrush = on ? Sx.Cyan : Sx.Muted, BorderThickness = new Thickness(1.5), Background = on ? Sx.Tint(Sx.Cyan, 60) : Brushes.Transparent, Child = on ? new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), Background = Sx.Cyan } : null, HorizontalAlignment = HorizontalAlignment.Left }
                    : new Border { Width = 15, Height = 15, CornerRadius = new CornerRadius(4), BorderBrush = on ? (it.Dot ?? Sx.Cyan) : Sx.Muted, BorderThickness = new Thickness(1.5), Background = on ? Sx.Tint((SolidColorBrush)(it.Dot ?? Sx.Cyan), 90) : Brushes.Transparent, Child = on ? new TextBlock { Text = "✓", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Sx.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -1, 0, 0) } : null, HorizontalAlignment = HorizontalAlignment.Left };
            }
            else if (it.Dot != null) icon = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = it.Dot, HorizontalAlignment = HorizontalAlignment.Left };
            else icon = new TextBlock { Text = it.Icon ?? "", FontSize = 13, Foreground = it.Fg ?? Sx.Text, VerticalAlignment = VerticalAlignment.Center };
            icon.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(icon);
            var txt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (it.Check.HasValue && it.Dot != null) txt.Children.Add(new Border { Width = 18, Height = 3, CornerRadius = new CornerRadius(2), Background = it.Dot, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            var tb = new TextBlock { Text = it.Text, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = it.Off ? Sx.Muted : it.Danger ? Sx.Red : it.Fg ?? Sx.Text, VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(tb); Grid.SetColumn(txt, 1); g.Children.Add(txt);
            var right = new TextBlock { Text = it.Sub != null ? "›" : it.Hint ?? "", FontSize = it.Sub != null ? 15 : 11, Foreground = Sx.Muted, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(right, 2); g.Children.Add(right);
            var row = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 6, 10, 6), Background = Brushes.Transparent, Child = g, Cursor = it.Off ? Cursors.Arrow : Cursors.Hand, Margin = new Thickness(0, 1, 0, 1) };
            row.MouseEnter += delegate
            {
                if (it.Off) return; row.Background = it.Danger ? Sx.Tint(Sx.Red, 46) : new SolidColorBrush(Color.FromArgb(44, 0x4D, 0xD8, 0xFF)); tb.Foreground = it.Danger ? Sx.Red : Sx.White;
                if (it.Sub != null) Open(w, c, it.Sub, new Point(), level + 1, row);
                else { var lv = levels[w]; while (lv.Count > level + 1) { c.Children.Remove(lv[lv.Count - 1]); lv.RemoveAt(lv.Count - 1); } }
            };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; tb.Foreground = it.Off ? Sx.Muted : it.Danger ? Sx.Red : it.Fg ?? Sx.Text; };
            row.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true; if (it.Off) return;
                if (it.Sub != null) { Open(w, c, it.Sub, new Point(), level + 1, row); return; }
                if (!it.KeepOpen) Close(w);
                var a = it.Do; if (a != null) w.Dispatcher.BeginInvoke(a);
            };
            return row;
        }
    }
}
