// SpasiX — the loading screen and the START SCREEN: choose LIVE (your MT5 account), PROP (a CFD prop-firm account with its rules), DEMO (live prices,
// a simulated account) or BACKTEST
// (MT5's history replayed). Each opens its own window; ⌂ MODES in the header comes back here.
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
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed class Launcher : Window
    {
        public event Action<string> Chosen;
        readonly Mt5Bridge bridge; readonly DispatcherTimer tm; TextBlock status; Border statusDot;
        WrapPanel accts; string acctSig = "";
        Border resume; StackPanel resumeBox, resumeBtns; TextBlock resumeTitle, resumeText; string resumeSig = ""; readonly DateTime openedAt = DateTime.Now;
        public Launcher(Mt5Bridge b, bool stream)
        {
            bridge = b; Title = "SpasiX"; Width = 1440; Height = 860; MinWidth = 900; MinHeight = 560; WindowState = WindowState.Maximized; Background = Sx.Bg;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = Sx.Face; var lg = MainWindow.Logo(); if (lg != null) Icon = lg;
            var ts = Environment.GetEnvironmentVariable("SPASIX_TEST_SIZE"); if (!string.IsNullOrEmpty(ts) && ts.Contains("x")) { WindowState = WindowState.Normal; WindowStartupLocation = WindowStartupLocation.Manual; Left = 0; Top = 0; Width = double.Parse(ts.Split('x')[0], CultureInfo.InvariantCulture); Height = double.Parse(ts.Split('x')[1], CultureInfo.InvariantCulture); }
            var root = new Grid();
            var frame = new Border { CornerRadius = new CornerRadius(26), Margin = new Thickness(20), BorderThickness = new Thickness(1.4), BorderBrush = new LinearGradientBrush(Sx.C("#FFC94D"), Sx.C("#4DD8FF"), 30),
                Background = new RadialGradientBrush(Sx.C("#16203A"), Sx.C("#070A12")) { GradientOrigin = new Point(0.5, 0.0), Center = new Point(0.5, 0.1), RadiusX = 1.1, RadiusY = 1.2 },
                Effect = new DropShadowEffect { Color = Sx.C("#4DD8FF"), BlurRadius = 30, ShadowDepth = 0, Opacity = 0.35 } };
            root.Children.Add(frame);
            var g = new Grid { Margin = new Thickness(46, 30, 46, 26) }; frame.Child = g;
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // top: the logo, the name, the question, MT5's state, close
            var top = new DockPanel();
            var close = Sx.Pill("✕", Brushes.Transparent); close.BorderBrush = Brushes.Transparent; close.Width = 36; close.Padding = new Thickness(0); close.FontSize = 14; close.ToolTip = "close SpasiX"; close.Click += delegate { Close(); };
            
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
            head.Children.Add(tx);
            // RESUME: the trades still open (your MT5's, the saved DEMO) found at start — one click opens them again
            resumeBox = new StackPanel(); resume = new Border { Child = resumeBox, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 470,
                Background = new LinearGradientBrush(Sx.C("#16263A"), Sx.C("#0C1422"), 90), BorderThickness = new Thickness(1.4), BorderBrush = new LinearGradientBrush(Sx.C("#2EE59D"), Sx.C("#4DD8FF"), 0), Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect { Color = Sx.C("#2EE59D"), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.45 } };
            var rr = new StackPanel { Orientation = Orientation.Horizontal };
            var spin = new Grid { Width = 34, Height = 34, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            var arc = new System.Windows.Shapes.Ellipse { Width = 34, Height = 34, StrokeThickness = 3, Stroke = new LinearGradientBrush(Sx.C("#2EE59D"), Sx.C("#4DD8FF"), 45), StrokeDashArray = new DoubleCollection { 7, 4 } };
            var rot2 = new RotateTransform(0, 17, 17); arc.RenderTransform = rot2; rot2.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(3)) { RepeatBehavior = RepeatBehavior.Forever });
            spin.Children.Add(arc); spin.Children.Add(new TextBlock { Text = "⟳", Foreground = Sx.Green, FontSize = 15, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            rr.Children.Add(spin); var rt = new StackPanel(); rr.Children.Add(rt);
            resumeTitle = new TextBlock { Foreground = Sx.Green, FontSize = 12.5, FontWeight = FontWeights.Bold }; resumeText = new TextBlock { Foreground = Sx.Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxWidth = 380, Margin = new Thickness(0, 2, 0, 6) };
            rt.Children.Add(resumeTitle); rt.Children.Add(resumeText); resumeBtns = new StackPanel { Orientation = Orientation.Horizontal }; rt.Children.Add(resumeBtns); resumeBox.Children.Add(rr);
            resume.Loaded += delegate { resume.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.82, TimeSpan.FromMilliseconds(1100)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }); };
            DockPanel.SetDock(resume, Dock.Right); top.Children.Add(resume);
            top.Children.Add(head); g.Children.Add(top);
            // the three modes
            var row = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(0, 26, 0, 10) }; Grid.SetRow(row, 1); g.Children.Add(row);
            row.Children.Add(Card("LIVE", "📈", "LIVE TRADING", "your MT5 account in real time", Sx.Green, new[] { "charts with your tickets, stops and targets", "lots, hedging, limit / stop, one-click", "close all / buys / sells / winners / losers", "tracker, market watch, ForexFactory news" }, "1"));
            row.Children.Add(Card("PROP", "🏆", "PROP ACCOUNT", "a CFD prop-firm account on MT5", Sx.Violet, new[] { "the firm's rules always on screen", "daily loss, max loss, profit target, days", "new entries blocked at the limit", "the same chart, panel and tracker as live" }, "2"));
            row.Children.Add(Card("DEMO", "🎮", "DEMO ACCOUNT", "live prices, a simulated account", Sx.Orange, new[] { "your MT5's real prices and candles", "your own balance, RAW or STANDARD", "trade like live — nothing reaches MT5", "kept on this PC, RESET any time" }, "3"));
            row.Children.Add(Card("BACKTEST", "🧪", "BACKTEST STUDIO", "MT5's own history, replayed", Sx.Cyan, new[] { "gold, NAS100, … together, real ticks", "play / speed / ◀ ▶ candle / day / jump", "RAW or STANDARD, MT5's swap and margin", "sessions saved to a pool + HTML tracker" }, "4"));
            // YOUR MT5 ACCOUNTS: every terminal with the SpasiXBridge (same port) — each opens as its own LIVE / PROP tab
            var ab = new DockPanel { Margin = new Thickness(10, 0, 10, 8) }; Grid.SetRow(ab, 2); g.Children.Add(ab);
            var kill = Sx.Pill("⛔ CLOSE ALL • every armed account", Sx.Tint(Sx.Red, 200)); kill.Foreground = Sx.White; kill.Height = 34; kill.VerticalAlignment = VerticalAlignment.Top; kill.ToolTip = "every MT5 account whose tab is ARMED: its positions close at the market, its 🤖 AUTO stops (asks first)"; kill.Click += delegate { CloseEverything(); }; DockPanel.SetDock(kill, Dock.Right); ab.Children.Add(kill);
            var al = new StackPanel(); al.Children.Add(new TextBlock { Text = "YOUR MT5 ACCOUNTS", Foreground = Sx.Gold, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            accts = new WrapPanel(); al.Children.Add(accts); ab.Children.Add(al);
            var foot = new DockPanel { Margin = new Thickness(4, 4, 4, 0) }; Grid.SetRow(foot, 3); g.Children.Add(foot);
            var bl = new TextBlock { Text = MainWindow.Build, Foreground = Sx.Muted, FontSize = 11 }; DockPanel.SetDock(bl, Dock.Right); foot.Children.Add(bl);
            foot.Children.Add(new TextBlock { Text = "1 / 2 / 3 / 4 = open a mode (several can be open: they become tabs at the top) • LIVE, PROP and DEMO need MetaTrader 5 with the SpasiXBridge on a chart; BACKTEST works offline", Foreground = Sx.Muted, FontSize = 11 });
            // full screen: the whole start screen scales to the window, every button always visible
            root.Width = 1360; root.Height = 800;
            var host = new Grid { Background = new RadialGradientBrush(Sx.C("#111A2E"), Sx.C("#05070D")) { RadiusX = 0.9, RadiusY = 0.9 } };
            host.Children.Add(new Viewbox { Child = root, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Margin = new Thickness(18, 10, 18, 14) });
            Content = Shell.Chrome(this, "HOME", host); SxMenu.Attach(this, host);
            Closing += (s, e) =>
            {
                if (Shell.Quitting) return; int n = Shell.Modes.Count;
                if (n > 0 && Sx.Choice(this, "Close SpasiX?", n + " mode" + (n == 1 ? " is" : "s are") + " open (" + string.Join(", ", Shell.Modes.Select(m => Shell.TitleOf(m))) + "). Your MT5 positions stay open at the broker.", null, new[] { "⏻  CLOSE SPASIX", "STAY" }, new[] { Sx.Red, (SolidColorBrush)null }) != 0) { e.Cancel = true; return; }
                e.Cancel = true; Dispatcher.BeginInvoke(new Action(Shell.Quit));
            };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.R && resume.Visibility == Visibility.Visible && resumeBtns.Children.Count > 0) Pick(resumeTitle.Text.StartsWith("YOUR DEMO") ? "DEMO" : "LIVE"); else if (e.Key == Key.D1 || e.Key == Key.NumPad1) Pick("LIVE"); else if (e.Key == Key.D2 || e.Key == Key.NumPad2) Pick("PROP"); else if (e.Key == Key.D3 || e.Key == Key.NumPad3) Pick("DEMO"); else if (e.Key == Key.D4 || e.Key == Key.NumPad4) Pick("BACKTEST"); };
            tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            tm.Tick += delegate
            {
                var s = bridge.State; lock (s.Gate)
                {
                    if (bridge.Problem != "") { status.Text = bridge.Problem; statusDot.Background = Sx.Red; }
                    else if (s.Alive) { var a = s.Account; status.Text = "MT5 connected • " + (stream ? "•••••" : a.Login.ToString(CultureInfo.InvariantCulture)) + " • " + a.Server + " • " + a.Kind + " • bridge v" + s.BridgeVersion; statusDot.Background = a.Kind == "REAL" ? Sx.Green : Sx.Cyan; }
                    else { status.Text = "MT5 not connected yet — start MetaTrader 5 with the SpasiXBridge on a chart (BACKTEST works without it)"; statusDot.Background = Sx.Gold; }
                }
                PaintResume(); PaintAccounts();
            };
            tm.Start(); Closed += delegate { tm.Stop(); };
            Loaded += delegate { frame.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))); };
        }
        // the RESUME card: MT5's open positions (live / prop) and the saved DEMO account's tickets
        void PaintResume()
        {
            int n = 0, ords = 0; double pl = 0; string by = "", kind = ""; bool alive;
            var s = bridge.State; lock (s.Gate) { alive = s.Alive; if (alive) { n = s.Positions.Count; ords = s.Orders.Count; pl = s.Positions.Sum(p => p.Profit + p.Swap); kind = s.Account.Kind; by = string.Join("  •  ", s.Positions.GroupBy(p => p.Symbol).Select(gp => gp.Key + (gp.Any(p => p.Dir > 0) ? " ▲" + gp.Count(p => p.Dir > 0) : "") + (gp.Any(p => p.Dir < 0) ? " ▼" + gp.Count(p => p.Dir < 0) : ""))); } }
            int demo = 0; try { if (System.IO.File.Exists(PaperBroker.FilePath)) demo = System.IO.File.ReadAllLines(PaperBroker.FilePath).Count(l => l.StartsWith("P|")); } catch { }
            bool searching = !alive && (DateTime.Now - openedAt).TotalSeconds < 12;
            string sig = n + "|" + ords + "|" + Math.Round(pl) + "|" + by + "|" + demo + "|" + searching + "|" + alive;
            if (sig == resumeSig) return; resumeSig = sig;
            if (n == 0 && demo == 0 && !searching) { resume.Visibility = Visibility.Collapsed; return; }
            resume.Visibility = Visibility.Visible; resumeBtns.Children.Clear();
            if (n == 0 && searching && demo == 0) { resumeTitle.Text = "LOOKING FOR YOUR OPEN TRADES …"; resumeTitle.Foreground = Sx.Cyan; resumeText.Text = "waiting for MT5 — the trades you had open come back here, with their stops and targets"; return; }
            resumeTitle.Foreground = Sx.Green;
            if (n > 0)
            {
                resumeTitle.Text = "WELCOME BACK • " + n + " TRADE" + (n == 1 ? "" : "S") + " STILL OPEN";
                resumeText.Text = by + (ords > 0 ? "  •  ⏳ " + ords + " order" + (ords == 1 ? "" : "s") : "") + "\nopen P/L " + (pl >= 0 ? "+$" : "−$") + Math.Abs(pl).ToString("N2", CultureInfo.InvariantCulture) + " on your " + kind + " account — safe at the broker, nothing changed";
                var b = Sx.Pill("⟳ RESUME LIVE  ›", Sx.Green); b.Foreground = Sx.Bg; b.Height = 30; b.ToolTip = "open LIVE on the chart of your trades (key R) — trading stays disarmed until you ARM"; b.Click += delegate { Pick("LIVE"); }; resumeBtns.Children.Add(b);
                var bp = Sx.Pill("PROP  ›", Sx.Card); bp.Height = 30; bp.Foreground = Sx.Violet; bp.Click += delegate { Pick("PROP"); }; resumeBtns.Children.Add(bp);
            }
            else resumeTitle.Text = "YOUR DEMO IS WAITING";
            if (demo > 0) { var bd = Sx.Pill("🎮 RESUME DEMO (" + demo + ")  ›", Sx.Tint(Sx.Orange, 200)); bd.Foreground = Sx.Bg; bd.Height = 30; bd.Click += delegate { Pick("DEMO"); }; resumeBtns.Children.Add(bd); if (n == 0) resumeText.Text = demo + " simulated ticket" + (demo == 1 ? "" : "s") + " saved on this PC — they follow the live prices again"; }
        }
        // one card per MT5 account: connected or not, its balance, open trades, and LIVE / PROP / ✎ (the tab opens, or comes forward)
        void PaintAccounts()
        {
            var links = Shell.Hub != null ? Shell.Hub.Links.Where(l => l == bridge || l.Key != "").ToList() : new List<Mt5Bridge> { bridge };
            var rows = links.Select(l => { lock (l.State.Gate) { var s = l.State; return new { L = l, On = s.Alive, A = s.Account, N = s.Positions.Count, Pl = s.Positions.Sum(p => p.Profit + p.Swap), Name = MainWindow.AccountLabel(l) }; } }).ToList();
            var sig = string.Join("|", rows.Select(r => r.L.Key + r.On + Math.Round(r.A.Balance) + r.N + Math.Round(r.Pl) + r.Name + Shell.Modes.Count(m => m.Link == r.L)));
            if (sig == acctSig) return; acctSig = sig; accts.Children.Clear();
            foreach (var r in rows)
            {
                var l = r.L; var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = r.On ? (r.A.Kind == "REAL" ? Sx.Green : Sx.Cyan) : Sx.Muted, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
                var tx = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
                tx.Children.Add(new TextBlock { Text = r.Name != "" ? r.Name : "waiting for MT5 …", Foreground = Sx.Text, FontSize = 13, FontWeight = FontWeights.Bold });
                tx.Children.Add(new TextBlock { Text = l.Key == "" ? "start MetaTrader 5 with the SpasiXBridge on a chart" : (r.On ? r.A.Kind + " • $" + r.A.Balance.ToString("N2", CultureInfo.InvariantCulture) + (r.N > 0 ? " • " + r.N + " open " + (r.Pl >= 0 ? "+$" : "−$") + Math.Abs(r.Pl).ToString("N2", CultureInfo.InvariantCulture) : " • nothing open") : "MT5 closed / disconnected"), Foreground = Sx.Muted, FontSize = 11 });
                sp.Children.Add(tx);
                Func<string, string, SolidColorBrush, System.Windows.Controls.Button> btn = (txt, mode, col) => { var b = Sx.Pill(txt, Sx.Tint(col, Shell.Modes.Any(m => m.Mode == mode && m.Link == l) ? (byte)90 : (byte)40)); b.Height = 28; b.Margin = new Thickness(0, 0, 4, 0); b.ToolTip = (Shell.Modes.Any(m => m.Mode == mode && m.Link == l) ? "show its " : "open its ") + mode + " tab"; b.Click += delegate { Shell.Open(mode, l, true); }; return b; };
                sp.Children.Add(btn("● LIVE", "LIVE", Sx.Green)); sp.Children.Add(btn("🏆 PROP", "PROP", Sx.Violet));
                if (l.Key != "") { var rn = Sx.Pill("✎", Sx.Tint(Sx.Card2, 220)); rn.Height = 28; rn.ToolTip = "name this account"; rn.Click += delegate { MainWindow.Rename(this, l); acctSig = ""; }; sp.Children.Add(rn); }
                accts.Children.Add(new Border { Child = sp, CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 6, 8, 6), Margin = new Thickness(0, 0, 8, 6), Background = new LinearGradientBrush(Sx.C("#162034"), Sx.C("#0C121E"), 90), BorderBrush = r.On ? Sx.Tint(r.A.Kind == "REAL" ? Sx.Green : Sx.Cyan, 140) : Sx.Line, BorderThickness = new Thickness(1.2) });
            }
        }
        // ⛔ every ARMED account's positions close (each through its own tab and terminal); the others are listed, not touched
        void CloseEverything()
        {
            var wins = Shell.Modes.Where(m => Shell.PerAccount(m.Mode)).GroupBy(m => m.Link).Select(g => g.First()).ToList();
            var noTab = Shell.Hub != null ? Shell.Hub.Links.Where(l => l.Key != "" && !wins.Any(w => w.Link == l)).Where(l => { lock (l.State.Gate) return l.State.Positions.Count > 0; }).ToList() : new List<Mt5Bridge>();
            if (wins.Count == 0 && noTab.Count == 0) { Sx.Info(this, "⛔ CLOSE ALL", "No MT5 account tab is open.", Sx.Cyan); return; }
            if (!Sx.Ask(this, "⛔ CLOSE ALL — every armed account", "Every position on every MT5 account whose tab is ARMED closes at the market and its 🤖 AUTO stops:\n\n" + string.Join("\n", wins.Select(w => "• " + Shell.TitleOf(w))) + "\n\nAccounts that are not armed are NOT touched (they are listed after).", "⛔ CLOSE ALL", "CANCEL", Sx.Red)) return;
            var lines = new List<string>(); foreach (var w in wins) { string l; w.EmergencyClose(out l); lines.Add(l); }
            foreach (var l in noTab) lines.Add(MainWindow.AccountLabel(l) + ": NOT closed — its tab is not open (open it and ARM)");
            Sx.Info(this, "⛔ CLOSE ALL", string.Join("\n", lines), Sx.Red);
        }
        void Pick(string mode) { var h = Chosen; if (h != null) h(mode); else Shell.Open(mode); }
        FrameworkElement Card(string mode, string icon, string title, string tag, SolidColorBrush col, string[] lines, string key)
        {
            var sp = new StackPanel { Margin = new Thickness(22, 22, 22, 20) };
            var ic = new Border { Width = 66, Height = 66, CornerRadius = new CornerRadius(33), Background = Sx.Tint(col, 40), BorderBrush = col, BorderThickness = new Thickness(1.6), HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = icon, FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, Effect = new DropShadowEffect { Color = col.Color, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.6 } };
            sp.Children.Add(ic);
            sp.Children.Add(new TextBlock { Text = title, Foreground = col, FontSize = 21, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 14, 0, 0) });
            sp.Children.Add(new TextBlock { Text = tag, Foreground = Sx.Text, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 12), TextWrapping = TextWrapping.Wrap, MaxWidth = 240 });
            foreach (var l in lines) { var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) }; r.Children.Add(new TextBlock { Text = "◆", Foreground = col, FontSize = 9, Margin = new Thickness(0, 3, 9, 0) }); r.Children.Add(new TextBlock { Text = l, Foreground = Sx.Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxWidth = 212 }); sp.Children.Add(r); }
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
        [STAThread]
        public static void Main(string[] args)
        {
            int port = 47123; string direct = null;   // SpasiX.exe [port] [--mode LIVE|PROP|DEMO|BACKTEST] (a shortcut can open a mode directly)
            for (int i = 0; i < args.Length; i++) { int pp; if (args[i] == "--mode" && i + 1 < args.Length) direct = args[++i].ToUpperInvariant(); else if (int.TryParse(args[i], out pp)) port = pp; }
            bridge = new Mt5Bridge(port); bridge.Start();
            app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Sx.ModernScrollbars(app);
            app.DispatcherUnhandledException += (s, e) => { e.Handled = true; try { Sx.Info(app.MainWindow, "SpasiX — something went wrong", e.Exception.Message + "\n\n" + (e.Exception.StackTrace ?? "").Split('\n')[0].Trim(), Sx.Red); } catch { } };
            var splash = Splash(); splash.Show();
            var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
            tm.Tick += delegate
            {
                tm.Stop(); DataDir.Init();   // the data folder (F: when there is one) — moves the old data there once
                Shell.Bridge = bridge; Shell.App = app; Shell.Home = new Launcher(bridge, StreamOn());
                // every other MT5 account that connects (same port, its own terminal) gets its own tab
                bridge.Hub.Added += l => app.Dispatcher.BeginInvoke(new Action(() => Shell.AccountAdded(l)));
                foreach (var l in bridge.Hub.Links) if (l != bridge) Shell.AccountAdded(l);   // the ones that connected while SpasiX was starting
                if (direct == "LIVE" || direct == "PROP" || direct == "DEMO" || direct == "BACKTEST") { Shell.Switch(Shell.Home); Shell.Open(direct); } else Shell.ShowHome();
                splash.Close();
            };
            tm.Start(); app.Run();
        }
    }
}
