// SpasiX — the SHELL: one app with TABS. HOME (the start screen) + every open mode (LIVE, PROP, DEMO, BACKTEST — several at once, each in its
// own window that keeps running when you switch). Every window has our own TITLE BAR (no grey Windows frame): the logo, the tabs, and
// minimize / maximize / close in our colours. Closing a mode goes back to HOME (or the next open tab); ⏻ closes SpasiX.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace KeystoneMT5
{
    public static class Shell
    {
        public static Mt5Bridge Bridge; public static Application App; public static Launcher Home; public static Window Active;
        public static readonly List<MainWindow> Modes = new List<MainWindow>();
        public static bool Quitting;
        static readonly List<Tuple<Window, StackPanel>> strips = new List<Tuple<Window, StackPanel>>(); static readonly Dictionary<Window, ContentControl> centers = new Dictionary<Window, ContentControl>();
        // the middle of the title bar: the mode's banner (LIVE / DEMO …), always in view
        public static void SetCenter(Window w, FrameworkElement el) { ContentControl c; if (centers.TryGetValue(w, out c)) c.Content = el; }
        static readonly string[] Order = { "LIVE", "PROP", "DEMO", "BACKTEST" };
        public static SolidColorBrush ColorOf(string mode) { return mode == "BACKTEST" ? Sx.Cyan : mode == "PROP" ? Sx.Violet : mode == "DEMO" ? Sx.Orange : mode == "HOME" ? Sx.Gold : Sx.Green; }
        public static string NameOf(string mode) { return mode == "BACKTEST" ? "🧪 BACKTEST" : mode == "PROP" ? "🏆 PROP" : mode == "DEMO" ? "🎮 DEMO" : mode == "HOME" ? "⌂ HOME" : "● LIVE"; }

        // a mode: its tab if it is open, else a new window
        public static void Open(string mode)
        {
            var w = Modes.FirstOrDefault(x => x.Mode == mode);
            if (w == null)
            {
                try { w = new MainWindow(Bridge, mode); }
                catch (Exception ex) { Sx.Info(Active, "SpasiX", "The " + mode + " window could not open: " + ex.Message, Sx.Red); return; }
                Modes.Add(w); Modes.Sort((a, b) => Array.IndexOf(Order, a.Mode).CompareTo(Array.IndexOf(Order, b.Mode)));
                w.Closed += delegate { Removed(w); };
            }
            Switch(w);
        }
        public static void ShowHome() { Switch(Home); }
        // show one window where the last one was, hide the others (they keep running)
        public static void Switch(Window w)
        {
            if (w == null) return; var cur = Active;
            if (cur != null && cur != w && cur.IsVisible)
            {
                w.WindowState = cur.WindowState == WindowState.Minimized ? WindowState.Maximized : cur.WindowState;
                if (cur.WindowState == WindowState.Normal) { w.Left = cur.Left; w.Top = cur.Top; w.Width = cur.Width; w.Height = cur.Height; }
            }
            Active = w; if (App != null) App.MainWindow = w;
            // SMOOTH: the new tab is shown OVER the old one (same place, no Windows animation), its content fades in, and the old one is hidden
            // only once the new one is painted — never a blank window in between
            bool change = cur != null && cur != w; if (w.Background == null || w.Background == Brushes.Transparent) w.Background = Sx.Bg;
            w.Show(); if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Maximized; w.Activate();
            var root = w.Content as UIElement; if (change && root != null) root.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(170)) { EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
            var others = Modes.Cast<Window>().Concat(new Window[] { Home }).Where(o => o != null && o != w && o.IsVisible).ToList();
            w.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { foreach (var o in others) if (o != Active && o.IsVisible) o.Hide(); }));
            Repaint();
        }
        static void Removed(MainWindow m)
        {
            Modes.Remove(m); strips.RemoveAll(s => s.Item1 == m);
            if (Quitting) return;
            if (Active == m) { Active = null; Switch((Window)Modes.LastOrDefault() ?? Home); } else Repaint();
        }
        // Windows' own show / hide animation (the zoom) off: switching tabs is instant
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        static void NoWindowsAnimation(Window w) { try { var h = new System.Windows.Interop.WindowInteropHelper(w).Handle; int on = 1; DwmSetWindowAttribute(h, 3, ref on, 4); } catch { } }   // DWMWA_TRANSITIONS_FORCEDISABLED
        // ⏻ close SpasiX: every mode closes (their own saving runs), then the app
        public static void Quit()
        {
            Quitting = true;
            foreach (var w in Modes.ToList()) { try { w.Close(); } catch { } }
            try { if (Home != null) Home.Close(); } catch { }
            try { Bridge.Dispose(); } catch { }
            App.Shutdown();
        }

        // ---- the chrome: our title bar instead of the Windows frame
        public static FrameworkElement Chrome(Window w, string mode, FrameworkElement content)
        {
            w.Background = Sx.Bg; w.SourceInitialized += delegate { NoWindowsAnimation(w); };
            WindowChrome.SetWindowChrome(w, new WindowChrome { CaptionHeight = 38, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
            var dp = new DockPanel { LastChildFill = true };
            var bar = new Grid { Height = 38, Background = Sx.HeadGrad };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 14, 0) };
            var lg = MainWindow.Logo(); if (lg != null) left.Children.Add(new Image { Source = lg, Width = 24, Height = 24, Margin = new Thickness(0, 0, 7, 0) });
            left.Children.Add(new TextBlock { Text = "Spasi", Foreground = Sx.Gold, FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            left.Children.Add(new TextBlock { Text = "X", Foreground = Sx.Cyan, FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            bar.Children.Add(left);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left }; Grid.SetColumn(tabs, 1); bar.Children.Add(tabs);
            var center = new ContentControl { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Focusable = false }; Grid.SetColumn(center, 1); bar.Children.Add(center); centers[w] = center;
            strips.Add(Tuple.Create(w, tabs));
            var btns = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(btns, 2); bar.Children.Add(btns);
            Func<string, string, bool, Action, Button> cap = (glyph, tip, danger, act) =>
            {
                var b = Sx.Pill(glyph, Brushes.Transparent); b.Width = 46; b.Height = 38; b.Margin = new Thickness(0); b.Padding = new Thickness(0); b.FontSize = 13; b.BorderBrush = Brushes.Transparent; b.Template = CapTemplate(danger); b.ToolTip = tip;
                WindowChrome.SetIsHitTestVisibleInChrome(b, true); b.Click += delegate { act(); }; btns.Children.Add(b); return b;
            };
            // ⇅ HEDGE LAB: the hedge-instead-of-a-stop strategy tested on the saved MT5 history (research only, sends nothing)
            var lab = Sx.Pill("⇅ HEDGE LAB", Sx.Tint(Sx.Violet, 46)); lab.Height = 26; lab.Margin = new Thickness(0, 6, 10, 6); lab.FontSize = 11.5; lab.ToolTip = "test the hedge-instead-of-a-stop strategy on your MT5 history (research only — nothing is sent to MT5)";
            WindowChrome.SetIsHitTestVisibleInChrome(lab, true); lab.Click += delegate { HedgeLabWindow.ShowLab(w); }; btns.Children.Add(lab);
            cap("─", "minimize", false, () => w.WindowState = WindowState.Minimized);
            var maxB = cap("▢", "maximize / restore", false, () => w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
            cap("✕", mode == "HOME" ? "close SpasiX" : "close this mode (back to HOME)", true, () => w.Close());
            bar.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) { w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; e.Handled = true; } };
            var edge = new Border { Height = 1, Background = new LinearGradientBrush(Sx.Tint(Sx.Gold, 160).Color, Sx.Tint(Sx.Cyan, 120).Color, 0) };
            var top = new StackPanel(); top.Children.Add(bar); top.Children.Add(edge); DockPanel.SetDock(top, Dock.Top); dp.Children.Add(top); dp.Children.Add(content);
            var outer = new Border { Child = dp, Background = Sx.Bg, BorderBrush = Sx.Tint(Sx.Cyan, 70), BorderThickness = new Thickness(1) };
            w.StateChanged += delegate { outer.Margin = w.WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0); outer.BorderThickness = new Thickness(w.WindowState == WindowState.Maximized ? 0 : 1); maxB.Content = w.WindowState == WindowState.Maximized ? "❐" : "▢"; };
            w.Loaded += delegate { if (w.WindowState == WindowState.Maximized) { outer.Margin = new Thickness(7); outer.BorderThickness = new Thickness(0); maxB.Content = "❐"; } };
            w.Closed += delegate { strips.RemoveAll(s => s.Item1 == w); centers.Remove(w); };
            PaintStrip(w, tabs);
            return outer;
        }
        static ControlTemplate capT, capDangerT;
        static ControlTemplate CapTemplate(bool danger)
        {
            if (danger && capDangerT != null) return capDangerT; if (!danger && capT != null) return capT;
            var bd = new FrameworkElementFactory(typeof(Border), "bd"); bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); bd.AppendChild(cp);
            var t = new ControlTemplate(typeof(Button)) { VisualTree = bd };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; over.Setters.Add(new Setter(Border.BackgroundProperty, danger ? (Brush)Sx.Red.CloneCurrentValue() : Sx.Tint(Sx.Cyan, 50), "bd")); t.Triggers.Add(over);
            var press = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true }; press.Setters.Add(new Setter(Border.BackgroundProperty, danger ? Sx.Tint(Sx.Red, 170) : Sx.Tint(Sx.Cyan, 90), "bd")); t.Triggers.Add(press);
            if (danger) capDangerT = t; else capT = t; return t;
        }
        static void Repaint() { foreach (var s in strips.ToList()) PaintStrip(s.Item1, s.Item2); }
        // the tabs: HOME + the open modes (the active one lit, its colour underneath; ✕ closes that mode)
        static void PaintStrip(Window owner, StackPanel tabs)
        {
            tabs.Children.Clear();
            var items = new List<Tuple<string, Window>> { Tuple.Create("HOME", (Window)Home) }; items.AddRange(Modes.Select(m => Tuple.Create(m.Mode, (Window)m)));
            foreach (var it in items)
            {
                var mode = it.Item1; var win = it.Item2; bool on = win == owner; var col = ColorOf(mode);
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = NameOf(mode), Foreground = on ? col : Sx.Muted, FontSize = 12.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                var tab = new Border { Child = sp, Padding = new Thickness(14, 0, mode == "HOME" ? 14 : 6, 0), Height = 32, Margin = new Thickness(0, 6, 3, 0), CornerRadius = new CornerRadius(9, 9, 0, 0), Cursor = Cursors.Hand,
                    Background = on ? (Brush)Sx.Tint(col, 38) : Sx.Tint(Sx.Card, 200), BorderBrush = on ? col : Sx.Line, BorderThickness = new Thickness(1, 1, 1, on ? 0 : 1), ToolTip = mode == "HOME" ? "the start screen" : NameOf(mode) + " — click = show it (it keeps running in the background)" };
                if (mode != "HOME")
                {
                    var x = new TextBlock { Text = "✕", Foreground = Sx.Muted, FontSize = 11, Margin = new Thickness(9, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "close " + NameOf(mode) };
                    x.MouseEnter += delegate { x.Foreground = Sx.Red; }; x.MouseLeave += delegate { x.Foreground = Sx.Muted; };
                    x.MouseLeftButtonUp += (s, e) => { e.Handled = true; win.Close(); }; sp.Children.Add(x);
                }
                tab.MouseEnter += delegate { if (!on) tab.Background = Sx.Tint(col, 26); }; tab.MouseLeave += delegate { if (!on) tab.Background = Sx.Tint(Sx.Card, 200); };
                tab.MouseLeftButtonUp += delegate { if (!on) { if (win == Home) ShowHome(); else Switch(win); } };
                WindowChrome.SetIsHitTestVisibleInChrome(tab, true); tabs.Children.Add(tab);
            }
            // + a new mode (the ones not open yet)
            var closed = Order.Where(m => !Modes.Any(x => x.Mode == m)).ToList();
            if (closed.Count > 0)
            {
                var plus = new Border { Child = new TextBlock { Text = "+", Foreground = Sx.Gold, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }, Padding = new Thickness(10, 0, 10, 0), Height = 28, Margin = new Thickness(4, 8, 0, 0), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, ToolTip = "open another mode", Background = Sx.Tint(Sx.Gold, 26) };
                plus.MouseLeftButtonUp += delegate { var fe = plus; SxMenu.Show(fe, new Point(0, fe.ActualHeight + 4), closed.Select(m => { var mm = m; return new Mi { Text = NameOf(mm), Fg = ColorOf(mm), Do = () => Open(mm) }; }).ToList()); };
                WindowChrome.SetIsHitTestVisibleInChrome(plus, true); tabs.Children.Add(plus);
            }
        }
    }
}
