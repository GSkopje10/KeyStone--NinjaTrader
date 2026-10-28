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

        // a mode: its tab if it is open, else a new window. LIVE / PROP belong to ONE MT5 account (link; null = the first account, as before);
        // DEMO / BACKTEST use the first account's prices and history. show = false: the tab is added without switching to it
        public static Mt5Hub Hub { get { return Bridge != null ? Bridge.Hub : null; } }
        public static bool PerAccount(string mode) { return mode == "LIVE" || mode == "PROP" || mode == "DEMO"; }   // each broker its own LIVE / PROP / DEMO
        public static void Open(string mode) { Open(mode, null, true); }
        public static MainWindow Open(string mode, Mt5Bridge link, bool show)
        {
            if (link == null || !PerAccount(mode)) link = Bridge;
            var w = Modes.FirstOrDefault(x => x.Mode == mode && (!PerAccount(mode) || x.Link == link));
            if (w == null)
            {
                try { w = new MainWindow(link, mode); }
                catch (Exception ex) { Sx.Info(Active, "SpasiX", "The " + mode + " window could not open: " + ex.Message, Sx.Red); return null; }
                Modes.Add(w); SortModes(); var ww = w;
                w.Closed += delegate { Removed(ww); };
                if (link.Key != "" && mode != "DEMO") MainWindow.SPut("acct.mode." + link.Key, mode);   // the mode it opens in when it connects again
            }
            if (show) Switch(w); else Repaint();
            return w;
        }
        static void SortModes()
        {
            var links = Hub != null ? Hub.Links : new List<Mt5Bridge>();
            Modes.Sort((a, b) => { int c = Array.IndexOf(Order, a.Mode).CompareTo(Array.IndexOf(Order, b.Mode)); return c != 0 ? c : links.IndexOf(a.Link).CompareTo(links.IndexOf(b.Link)); });
        }
        // a new MT5 account connected: its tab (LIVE, or PROP if it was PROP last time) is added at once — nothing is armed
        public static void AccountAdded(Mt5Bridge link)
        {
            if (Quitting || link == null || link == Bridge) return;
            Open(MainWindow.SGet("acct.mode." + link.Key, "LIVE"), link, false);
        }
        // the tab's name: LIVE / PROP = the account (its name on this PC, else the firm + login), the others = the mode
        public static string TitleOf(Window w)
        {
            var m = w as MainWindow; if (m == null || !PerAccount(m.Mode)) return NameOf(m != null ? m.Mode : "HOME");
            var a = MainWindow.AccountLabel(m.Link); return a == "" ? NameOf(m.Mode) : a + " • " + NameOf(m.Mode);
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
            Active = w; if (App != null) App.MainWindow = w; var mw0 = w as MainWindow; if (mw0 != null && PerAccount(mw0.Mode) && mw0.Link != null) lastOf[mw0.Link] = w;
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
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 14, 0) };
            var lg = MainWindow.Logo(); if (lg != null) left.Children.Add(new Image { Source = lg, Width = 24, Height = 24, Margin = new Thickness(0, 0, 7, 0) });
            left.Children.Add(new TextBlock { Text = "Spasi", Foreground = Sx.Gold, FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            left.Children.Add(new TextBlock { Text = "X", Foreground = Sx.Cyan, FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            bar.Children.Add(left);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left }; var tabsBox = new Border { Child = tabs, ClipToBounds = true }; Grid.SetColumn(tabsBox, 1); bar.Children.Add(tabsBox);
            var center = new ContentControl { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Focusable = false, Margin = new Thickness(8, 0, 12, 0) }; Grid.SetColumn(center, 2); bar.Children.Add(center); centers[w] = center;
            strips.Add(Tuple.Create(w, tabs));
            var btns = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(btns, 3); bar.Children.Add(btns);
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
        public static void Repaint() { foreach (var s in strips.ToList()) PaintStrip(s.Item1, s.Item2); }
        // the tabs: HOME + the open modes (the active one lit, its colour underneath; ✕ closes that mode)
        // the tabs: HOME, ONE TAB PER BROKER (its account) with its modes inside — ● LIVE / 🏆 PROP / 🎮 DEMO (lit = shown here, white = open,
        // dim = not open: a click opens it; ✕ closes the shown one) —, then 🧪 BACKTEST and +
        static readonly Dictionary<Mt5Bridge, Window> lastOf = new Dictionary<Mt5Bridge, Window>();
        static void PaintStrip(Window owner, StackPanel tabs)
        {
            tabs.Children.Clear();
            Action<string, Window, string, SolidColorBrush> plain = (label, win, tip, col) =>
            {
                bool on = win == owner; var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = label, Foreground = on ? col : Sx.Muted, FontSize = 12.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                var tab = new Border { Child = sp, Padding = new Thickness(14, 0, win == Home ? 14 : 6, 0), Height = 32, Margin = new Thickness(0, 6, 3, 0), CornerRadius = new CornerRadius(9, 9, 0, 0), Cursor = Cursors.Hand,
                    Background = on ? (Brush)Sx.Tint(col, 38) : Sx.Tint(Sx.Card, 200), BorderBrush = on ? col : Sx.Line, BorderThickness = new Thickness(1, 1, 1, on ? 0 : 1), ToolTip = tip };
                if (win != Home)
                {
                    var x = new TextBlock { Text = "✕", Foreground = Sx.Muted, FontSize = 11, Margin = new Thickness(9, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "close " + label };
                    x.MouseEnter += delegate { x.Foreground = Sx.Red; }; x.MouseLeave += delegate { x.Foreground = Sx.Muted; };
                    x.MouseLeftButtonUp += (s, e) => { e.Handled = true; win.Close(); }; sp.Children.Add(x);
                }
                tab.MouseEnter += delegate { if (!on) tab.Background = Sx.Tint(col, 26); }; tab.MouseLeave += delegate { if (!on) tab.Background = Sx.Tint(Sx.Card, 200); };
                tab.MouseLeftButtonUp += delegate { if (!on) { if (win == Home) ShowHome(); else Switch(win); } };
                WindowChrome.SetIsHitTestVisibleInChrome(tab, true); tabs.Children.Add(tab);
            };
            plain(NameOf("HOME"), Home, "the start screen", ColorOf("HOME"));
            // one tab per broker that has a window open
            var links = (Hub != null ? Hub.Links : new List<Mt5Bridge> { Bridge }).Where(l => Modes.Any(m => PerAccount(m.Mode) && m.Link == l)).ToList();
            foreach (var l in links)
            {
                var ll = l; var mine = Modes.Where(m => PerAccount(m.Mode) && m.Link == ll).ToList(); var shown = mine.FirstOrDefault(m => m == owner);
                var col = shown != null ? ColorOf(shown.Mode) : Sx.Cyan; bool on = shown != null;
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var name = MainWindow.AccountLabel(ll); var lab = new TextBlock { Text = name != "" ? name : "MT5", Foreground = on ? Sx.Text : Sx.Muted, FontSize = 12.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                sp.Children.Add(lab);
                foreach (var mode in new[] { "LIVE", "PROP", "DEMO" })
                {
                    var mm = mode; var w = mine.FirstOrDefault(m => m.Mode == mm); bool act = w != null && w == owner; var mc = ColorOf(mm);
                    var seg = new StackPanel { Orientation = Orientation.Horizontal };
                    seg.Children.Add(new TextBlock { Text = NameOf(mm), Foreground = act ? mc : w != null ? Sx.Text : Sx.Tint(Sx.Muted, 150), FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                    if (act) { var x = new TextBlock { Text = "✕", Foreground = Sx.Muted, FontSize = 10, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "close " + NameOf(mm) + " of " + name }; x.MouseEnter += delegate { x.Foreground = Sx.Red; }; x.MouseLeave += delegate { x.Foreground = Sx.Muted; }; var ww = w; x.MouseLeftButtonUp += (s, e) => { e.Handled = true; ww.Close(); }; seg.Children.Add(x); }
                    var sb = new Border { Child = seg, Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 3, 0), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                        Background = act ? (Brush)Sx.Tint(mc, 55) : w != null ? Sx.Tint(Sx.Card2, 230) : Brushes.Transparent, BorderBrush = act ? mc : w != null ? Sx.Line : Sx.Tint(Sx.Line, 120), BorderThickness = new Thickness(1),
                        ToolTip = w == null ? "open " + NameOf(mm) + " for " + name : act ? NameOf(mm) + " of " + name + " (shown)" : "show " + NameOf(mm) + " of " + name + " (it keeps running in the background)" };
                    sb.MouseLeftButtonUp += (s, e) => { e.Handled = true; if (!act) Open(mm, ll, true); };
                    WindowChrome.SetIsHitTestVisibleInChrome(sb, true); sp.Children.Add(sb);
                }
                var tab = new Border { Child = sp, Padding = new Thickness(12, 0, 6, 0), Height = 32, Margin = new Thickness(0, 6, 3, 0), CornerRadius = new CornerRadius(9, 9, 0, 0), Cursor = Cursors.Hand,
                    Background = on ? (Brush)Sx.Tint(col, 30) : Sx.Tint(Sx.Card, 200), BorderBrush = on ? col : Sx.Line, BorderThickness = new Thickness(1, 1, 1, on ? 0 : 1), ToolTip = (name != "" ? name : "MT5") + " — its LIVE / PROP / DEMO inside" };
                tab.MouseLeftButtonUp += delegate { if (on) return; Window last; Switch(lastOf.TryGetValue(ll, out last) && mine.Contains(last as MainWindow) ? last : mine.First()); };
                WindowChrome.SetIsHitTestVisibleInChrome(tab, true); tabs.Children.Add(tab);
            }
            foreach (var m in Modes.Where(m => !PerAccount(m.Mode))) plain(NameOf(m.Mode), m, NameOf(m.Mode) + " — click = show it (it keeps running in the background)", ColorOf(m.Mode));
            // + a new mode (the ones not open yet): LIVE / PROP for every MT5 account connected, DEMO, BACKTEST
            var menu = new List<Mi>(); var accts = Hub != null ? Hub.Links.Where(l => l == Bridge || l.Key != "").ToList() : new List<Mt5Bridge>();
            foreach (var m in Order)
            {
                if (!PerAccount(m)) { if (!Modes.Any(x => x.Mode == m)) { var mm = m; menu.Add(new Mi { Text = NameOf(mm), Fg = ColorOf(mm), Do = () => Open(mm) }); } continue; }
                foreach (var l in accts) if (!Modes.Any(x => x.Mode == m && x.Link == l)) { var mm = m; var ll = l; var a = MainWindow.AccountLabel(ll); menu.Add(new Mi { Text = (a != "" ? a + "  •  " : "") + NameOf(mm), Fg = ColorOf(mm), Do = () => Open(mm, ll, true) }); }
            }
            if (menu.Count > 0)
            {
                var plus = new Border { Child = new TextBlock { Text = "+", Foreground = Sx.Gold, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }, Padding = new Thickness(10, 0, 10, 0), Height = 28, Margin = new Thickness(4, 8, 0, 0), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, ToolTip = "open another mode / account", Background = Sx.Tint(Sx.Gold, 26) };
                plus.MouseLeftButtonUp += delegate { var fe = plus; SxMenu.Show(fe, new Point(0, fe.ActualHeight + 4), menu); };
                WindowChrome.SetIsHitTestVisibleInChrome(plus, true); tabs.Children.Add(plus);
            }
        }
    }
}
