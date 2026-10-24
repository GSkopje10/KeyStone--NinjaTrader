// SpasiX — the loading screen and the START SCREEN: choose LIVE (your MT5 account), PROP (a CFD prop-firm account with its rules) or BACKTEST
// (MT5's history replayed). Each opens its own window; ⌂ MODES in the header comes back here.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed class Launcher : Window
    {
        public event Action<string> Chosen; public bool Picked;
        readonly Mt5Bridge bridge; readonly DispatcherTimer tm; TextBlock status; Border statusDot;
        public Launcher(Mt5Bridge b, bool stream)
        {
            bridge = b; Title = "SpasiX"; Width = 1180; Height = 660; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = Sx.Face; var lg = MainWindow.Logo(); if (lg != null) Icon = lg;
            var root = new Grid();
            var frame = new Border { CornerRadius = new CornerRadius(26), Margin = new Thickness(20), BorderThickness = new Thickness(1.4), BorderBrush = new LinearGradientBrush(Sx.C("#FFC94D"), Sx.C("#4DD8FF"), 30),
                Background = new RadialGradientBrush(Sx.C("#16203A"), Sx.C("#070A12")) { GradientOrigin = new Point(0.5, 0.0), Center = new Point(0.5, 0.1), RadiusX = 1.1, RadiusY = 1.2 },
                Effect = new DropShadowEffect { Color = Sx.C("#4DD8FF"), BlurRadius = 30, ShadowDepth = 0, Opacity = 0.35 } };
            root.Children.Add(frame);
            var g = new Grid { Margin = new Thickness(46, 30, 46, 26) }; frame.Child = g;
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            frame.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 1) try { DragMove(); } catch { } };
            // top: the logo, the name, the question, MT5's state, close
            var top = new DockPanel();
            var close = Sx.Pill("✕", Brushes.Transparent); close.BorderBrush = Brushes.Transparent; close.Width = 36; close.Padding = new Thickness(0); close.FontSize = 14; close.ToolTip = "close SpasiX"; close.Click += delegate { Close(); };
            DockPanel.SetDock(close, Dock.Right); close.VerticalAlignment = VerticalAlignment.Top; top.Children.Add(close);
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            var lgG = new Grid { Width = 74, Height = 74, Margin = new Thickness(0, 0, 16, 0) };
            var ring = new System.Windows.Shapes.Ellipse { Width = 74, Height = 74, StrokeThickness = 3, Stroke = new LinearGradientBrush(Sx.C("#FFC94D"), Sx.C("#4DD8FF"), 45), StrokeDashArray = new DoubleCollection { 5, 3 } };
            var rot = new RotateTransform(0, 37, 37); ring.RenderTransform = rot; rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(9)) { RepeatBehavior = RepeatBehavior.Forever });
            lgG.Children.Add(ring); if (lg != null) lgG.Children.Add(new Image { Source = lg, Width = 54, Height = 54 });
            head.Children.Add(lgG);
            var tx = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nm = new StackPanel { Orientation = Orientation.Horizontal }; nm.Children.Add(new TextBlock { Text = "Spasi", Foreground = Sx.Gold, FontSize = 38, FontWeight = FontWeights.Bold }); nm.Children.Add(new TextBlock { Text = "X", Foreground = Sx.Cyan, FontSize = 38, FontWeight = FontWeights.Bold }); tx.Children.Add(nm);
            tx.Children.Add(new TextBlock { Text = "How do you trade today?", Foreground = Sx.Text, FontSize = 16, FontWeight = FontWeights.SemiBold });
            var st = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            statusDot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = Sx.Gold, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            status = new TextBlock { Foreground = Sx.Muted, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            statusDot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            st.Children.Add(statusDot); st.Children.Add(status); tx.Children.Add(st);
            head.Children.Add(tx); top.Children.Add(head); g.Children.Add(top);
            // the three modes
            var row = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 26, 0, 10) }; Grid.SetRow(row, 1); g.Children.Add(row);
            row.Children.Add(Card("LIVE", "📈", "LIVE TRADING", "your MT5 account in real time", Sx.Green, new[] { "charts with your tickets, stops and targets", "lots, hedging, limit / stop, one-click", "close all / buys / sells / winners / losers", "tracker, market watch, ForexFactory news" }, "1"));
            row.Children.Add(Card("PROP", "🏆", "PROP ACCOUNT", "a CFD prop-firm account on MT5", Sx.Violet, new[] { "the firm's rules always on screen", "daily loss, max loss, profit target, days", "new entries blocked at the limit", "the same chart, panel and tracker as live" }, "2"));
            row.Children.Add(Card("BACKTEST", "🧪", "BACKTEST STUDIO", "MT5's own history, replayed", Sx.Cyan, new[] { "pick a symbol and the days, download once", "play / pause / speed / one candle / jump", "the real spread, commission, swap, stop-out", "stats per day, win rate, equity curve" }, "3"));
            var foot = new DockPanel { Margin = new Thickness(4, 4, 4, 0) }; Grid.SetRow(foot, 2); g.Children.Add(foot);
            var bl = new TextBlock { Text = MainWindow.Build, Foreground = Sx.Muted, FontSize = 11 }; DockPanel.SetDock(bl, Dock.Right); foot.Children.Add(bl);
            foot.Children.Add(new TextBlock { Text = "1 / 2 / 3 = choose • Esc = close • LIVE and PROP need MetaTrader 5 with the SpasiXBridge on a chart; BACKTEST works offline with the days on this PC", Foreground = Sx.Muted, FontSize = 11 });
            Content = root;
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key == Key.D1 || e.Key == Key.NumPad1) Pick("LIVE"); else if (e.Key == Key.D2 || e.Key == Key.NumPad2) Pick("PROP"); else if (e.Key == Key.D3 || e.Key == Key.NumPad3) Pick("BACKTEST"); };
            tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            tm.Tick += delegate
            {
                var s = bridge.State; lock (s.Gate)
                {
                    if (bridge.Problem != "") { status.Text = bridge.Problem; statusDot.Background = Sx.Red; }
                    else if (s.Alive) { var a = s.Account; status.Text = "MT5 connected • " + (stream ? "•••••" : a.Login.ToString(CultureInfo.InvariantCulture)) + " • " + a.Server + " • " + a.Kind + " • bridge v" + s.BridgeVersion; statusDot.Background = a.Kind == "REAL" ? Sx.Green : Sx.Cyan; }
                    else { status.Text = "MT5 not connected yet — start MetaTrader 5 with the SpasiXBridge on a chart (BACKTEST works without it)"; statusDot.Background = Sx.Gold; }
                }
            };
            tm.Start(); Closed += delegate { tm.Stop(); };
            Loaded += delegate { frame.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))); };
        }
        void Pick(string mode) { if (Picked) return; Picked = true; var h = Chosen; if (h != null) h(mode); }
        FrameworkElement Card(string mode, string icon, string title, string tag, SolidColorBrush col, string[] lines, string key)
        {
            var sp = new StackPanel { Margin = new Thickness(22, 22, 22, 20) };
            var ic = new Border { Width = 66, Height = 66, CornerRadius = new CornerRadius(33), Background = Sx.Tint(col, 40), BorderBrush = col, BorderThickness = new Thickness(1.6), HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = icon, FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, Effect = new DropShadowEffect { Color = col.Color, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.6 } };
            sp.Children.Add(ic);
            sp.Children.Add(new TextBlock { Text = title, Foreground = col, FontSize = 21, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 14, 0, 0) });
            sp.Children.Add(new TextBlock { Text = tag, Foreground = Sx.Text, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 12) });
            foreach (var l in lines) { var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) }; r.Children.Add(new TextBlock { Text = "◆", Foreground = col, FontSize = 9, Margin = new Thickness(0, 3, 9, 0) }); r.Children.Add(new TextBlock { Text = l, Foreground = Sx.Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxWidth = 260 }); sp.Children.Add(r); }
            var go = Sx.Pill("OPEN " + mode + "  ›", col); go.Foreground = Sx.Bg; go.Height = 40; go.FontSize = 13.5; go.Margin = new Thickness(0, 18, 0, 0); go.HorizontalAlignment = HorizontalAlignment.Stretch; go.Click += delegate { Pick(mode); }; sp.Children.Add(go);
            sp.Children.Add(new TextBlock { Text = "key " + key, Foreground = Sx.Muted, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            var sc = new ScaleTransform(1, 1);
            var glow = new DropShadowEffect { Color = col.Color, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.25 };
            var bd = new Border { CornerRadius = new CornerRadius(20), Margin = new Thickness(10, 6, 10, 6), Background = new LinearGradientBrush(Sx.C("#162034"), Sx.C("#0C121E"), 90), BorderBrush = Sx.Tint(col, 120), BorderThickness = new Thickness(1.3), Child = sp, Cursor = Cursors.Hand, RenderTransform = sc, RenderTransformOrigin = new Point(0.5, 0.5), Effect = glow };
            bd.MouseEnter += delegate { var an = new DoubleAnimation(1.035, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }; sc.BeginAnimation(ScaleTransform.ScaleXProperty, an); sc.BeginAnimation(ScaleTransform.ScaleYProperty, an); glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(28, TimeSpan.FromMilliseconds(200))); glow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.6, TimeSpan.FromMilliseconds(200))); bd.BorderBrush = col; };
            bd.MouseLeave += delegate { var an = new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)); sc.BeginAnimation(ScaleTransform.ScaleXProperty, an); sc.BeginAnimation(ScaleTransform.ScaleYProperty, an); glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(10, TimeSpan.FromMilliseconds(200))); glow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.25, TimeSpan.FromMilliseconds(200))); bd.BorderBrush = Sx.Tint(col, 120); };
            bd.MouseLeftButtonUp += delegate { Pick(mode); };
            return bd;
        }
    }

    public static class Program
    {
        static Window Splash()
        {
            Func<string, SolidColorBrush> br = h => (SolidColorBrush)new BrushConverter().ConvertFromString(h);
            var w = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, Width = 440, Height = 300, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true, Topmost = true, Title = "SpasiX" };
            var lg = MainWindow.Logo(); if (lg != null) w.Icon = lg;
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var ring = new System.Windows.Shapes.Ellipse { Width = 132, Height = 132, StrokeThickness = 4, Stroke = new LinearGradientBrush(br("#FFC94D").Color, br("#4DD8FF").Color, 45), StrokeDashArray = new DoubleCollection { 6, 3 } };
            var rot = new RotateTransform(0, 66, 66); ring.RenderTransform = rot; rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });
            var logoG = new Grid { Width = 132, Height = 132, HorizontalAlignment = HorizontalAlignment.Center }; logoG.Children.Add(ring);
            if (lg != null) logoG.Children.Add(new Image { Source = lg, Width = 92, Height = 92, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(logoG);
            var name = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 2) };
            name.Children.Add(new TextBlock { Text = "Spasi", Foreground = br("#FFC94D"), FontSize = 32, FontWeight = FontWeights.Bold }); name.Children.Add(new TextBlock { Text = "X", Foreground = br("#4DD8FF"), FontSize = 32, FontWeight = FontWeights.Bold });
            sp.Children.Add(name);
            sp.Children.Add(new TextBlock { Text = "loading …", Foreground = br("#7D879A"), FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center });
            w.Content = new Border { CornerRadius = new CornerRadius(22), Background = new LinearGradientBrush(br("#141C2C").Color, br("#070A12").Color, 90), BorderBrush = br("#22304A"), BorderThickness = new Thickness(1), Child = sp };
            return w;
        }
        static Application app; static Mt5Bridge bridge;
        static bool StreamOn() { try { var f = System.IO.Path.Combine(MainWindow.Dir, "settings.txt"); return System.IO.File.Exists(f) && Array.IndexOf(System.IO.File.ReadAllLines(f), "stream=1") >= 0; } catch { return false; } }
        static void ShowLauncher()
        {
            var l = new Launcher(bridge, StreamOn());
            l.Chosen += mode =>
            {
                MainWindow main;
                try { main = new MainWindow(bridge, mode); }
                catch (Exception ex) { l.Picked = false; Sx.Info(l, "SpasiX", "The " + mode + " window could not open: " + ex.Message, Sx.Red); return; }
                app.MainWindow = main;
                main.Closed += delegate { if (main.BackToLauncher) ShowLauncher(); else { bridge.Dispose(); app.Shutdown(); } };
                main.Show(); main.Activate(); l.Close();
            };
            l.Closed += delegate { if (!l.Picked) { bridge.Dispose(); app.Shutdown(); } };
            app.MainWindow = l; l.Show(); l.Activate();
        }
        [STAThread]
        public static void Main(string[] args)
        {
            int port = 47123; string direct = null;   // SpasiX.exe [port] [--mode LIVE|PROP|BACKTEST] (a shortcut can open a mode directly)
            for (int i = 0; i < args.Length; i++) { int pp; if (args[i] == "--mode" && i + 1 < args.Length) direct = args[++i].ToUpperInvariant(); else if (int.TryParse(args[i], out pp)) port = pp; }
            bridge = new Mt5Bridge(port); bridge.Start();
            app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (s, e) => { e.Handled = true; try { Sx.Info(app.MainWindow, "SpasiX — something went wrong", e.Exception.Message + "\n\n" + (e.Exception.StackTrace ?? "").Split('\n')[0].Trim(), Sx.Red); } catch { } };
            var splash = Splash(); splash.Show();
            var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
            tm.Tick += delegate
            {
                tm.Stop();
                if (direct == "LIVE" || direct == "PROP" || direct == "BACKTEST") { var main = new MainWindow(bridge, direct); app.MainWindow = main; main.Closed += delegate { if (main.BackToLauncher) ShowLauncher(); else { bridge.Dispose(); app.Shutdown(); } }; main.Show(); }
                else ShowLauncher();
                splash.Close();
            };
            tm.Start(); app.Run();
        }
    }
}
