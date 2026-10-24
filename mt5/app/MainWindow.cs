// SpasiX — the window: the header (logo, account with STREAM MODE blur, account cards, connection), the views, the tab bar at the bottom,
// the settings file and the 200 ms refresh. The views live in ChartTab.cs, PositionsTab.cs and MarketNews.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed partial class MainWindow : Window
    {
        public const string Build = "SpasiX • BUILD 2026-10-24c";
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static SolidColorBrush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
        static readonly SolidColorBrush Bg = B("#0A0E16"), Panel = B("#0F1522"), Card = B("#161E2E"), Line = B("#22304A"), Text = B("#E8ECF4"), Muted = B("#7D879A"),
            Gold = B("#FFC94D"), Green = B("#00E676"), Red = B("#FF3D71"), Cyan = B("#4DD8FF"), Violet = B("#B388FF"), Orange = B("#FF9F43");
        static SolidColorBrush Tint(SolidColorBrush b, byte a) { var c = b.Color; var t = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); t.Freeze(); return t; }
        static string Money(double v, string cur) { return (v < 0 ? "−" : "") + (cur == "USD" ? "$" : "") + Math.Abs(v).ToString("N2", IC) + (cur == "USD" ? "" : " " + cur); }
        static string Signed(double v, string cur) { return (v > 0 ? "+" : "") + Money(v, cur); }
        public static ImageSource Logo() { try { return BitmapFrame.Create(new Uri("pack://application:,,,/assets/spasix.png")); } catch { return null; } }

        readonly Mt5Bridge bridge; readonly DispatcherTimer ui;
        string view = "SETUP"; bool everLinked, streamMode, fullChart;
        readonly Dictionary<string, FrameworkElement> views = new Dictionary<string, FrameworkElement>();
        readonly List<Tuple<string, Border, TextBlock, Border>> tabs = new List<Tuple<string, Border, TextBlock, Border>>();
        FrameworkElement headerEl, tabBarEl;
        TextBlock connText, acctText; Border connPill, acctBox; readonly Dictionary<string, Tuple<TextBlock, TextBlock, Border>> cards = new Dictionary<string, Tuple<TextBlock, TextBlock, Border>>();
        Border eqFill; TextBlock eqText; StackPanel setupSteps; TextBlock setupStatus;

        // the app's own settings (%AppData%\SpasiX\MT5\settings.txt) and drawings (drawings.txt)
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpasiX", "MT5");
        static readonly string SetFile = Path.Combine(Dir, "settings.txt"), DrawFile = Path.Combine(Dir, "drawings.txt");
        readonly Dictionary<string, string> set = new Dictionary<string, string>();
        void LoadSet() { try { foreach (var l in File.ReadAllLines(SetFile)) { int i = l.IndexOf('='); if (i > 0) set[l.Substring(0, i)] = l.Substring(i + 1); } } catch { } }
        void SaveSet() { try { Directory.CreateDirectory(Dir); File.WriteAllLines(SetFile, set.Select(kv => kv.Key + "=" + kv.Value)); } catch { } }
        string Get(string k, string d) { string v; return set.TryGetValue(k, out v) ? v : d; }
        void Put(string k, string v) { set[k] = v; SaveSet(); }

        public MainWindow(Mt5Bridge b)
        {
            bridge = b;
            Title = "SpasiX"; Width = 1440; Height = 900; MinWidth = 1000; MinHeight = 620; Background = Bg; Foreground = Text;
            FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterScreen; WindowState = WindowState.Maximized;
            var lg = Logo(); if (lg != null) Icon = lg;
            LoadSet(); LoadDrawings();
            var root = new Grid(); Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headerEl = BuildHeader(); Grid.SetRow(headerEl, 0); root.Children.Add(headerEl);
            var host = new Grid { Margin = new Thickness(10, 6, 10, 6) }; Grid.SetRow(host, 1); root.Children.Add(host);
            views["CHART"] = BuildChartTab(); views["POSITIONS"] = BuildPositions(); views["MARKET"] = BuildMarket(); views["NEWS"] = BuildNews();
            views["TRACKER"] = Soon("📊 TRACKER", "Next: every day of this account from MT5's history — the trades, P/L after commission and swap, win rate, the best / worst day, by symbol, by hour, and the equity curve.");
            views["BACKTEST"] = Soon("🧪 BACKTEST", "Next: load any day from MT5 (candles and real ticks), replay it in real time or faster with lots, hedging, the real spread, commission and swap — the same chart, panel and tabs as live."); views["SETUP"] = BuildSetup();
            foreach (var v in views.Values) { v.Visibility = Visibility.Collapsed; host.Children.Add(v); }
            tabBarEl = BuildTabBar(); Grid.SetRow(tabBarEl, 2); root.Children.Add(tabBarEl);
            Show("SETUP");
            ui = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) }; ui.Tick += delegate { Refresh(); }; ui.Start();
            PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.F11 || (e.Key == System.Windows.Input.Key.Escape && fullChart)) { SetFullChart(!fullChart); e.Handled = true; } };
            // leaving: asks first (your positions stay open in MT5)
            Closing += (s, e) =>
            {
                int open; lock (bridge.State.Gate) open = bridge.State.Positions.Count;
                var msg = "Close SpasiX?" + (open > 0 ? "\n\nYour " + open + " open position" + (open == 1 ? "" : "s") + " stay open in MetaTrader 5 (only this window closes)." : "") + (armed ? "\n\nTrading is ARMED — it disarms when SpasiX closes." : "");
                if (MessageBox.Show(this, msg, "SpasiX", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
            };
            Closed += delegate { ui.Stop(); SaveDrawings(); bridge.Dispose(); };
            StartNews();
        }

        // ---------------------------------------------------------------- header
        FrameworkElement BuildHeader()
        {
            var dp = new DockPanel { LastChildFill = true, Margin = new Thickness(14, 10, 14, 8) };
            var logo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
            var lgI = Logo(); if (lgI != null) logo.Children.Add(new Image { Source = lgI, Width = 42, Height = 42, Margin = new Thickness(0, 0, 10, 0) });
            logo.Children.Add(new TextBlock { Text = "Spasi", Foreground = Gold, FontSize = 27, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            logo.Children.Add(new TextBlock { Text = "X", Foreground = Cyan, FontSize = 27, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(logo, Dock.Left); dp.Children.Add(logo);
            connText = new TextBlock { Text = "● WAITING FOR MT5", Foreground = Bg, FontSize = 12, FontWeight = FontWeights.Bold };
            connPill = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 4, 12, 4), Background = Gold, Child = connText, VerticalAlignment = VerticalAlignment.Center };
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var streamBtn = Pill("👁 STREAM MODE", Card); streamBtn.ToolTip = "hide the account name and number (blurred) — for streaming / screenshots"; streamBtn.Click += delegate { SetStream(!streamMode); streamBtn.Background = streamMode ? Tint(Violet, 160) : (Brush)Card; };
            right.Children.Add(streamBtn); right.Children.Add(connPill);
            right.Children.Add(new TextBlock { Text = Build, Foreground = Muted, FontSize = 11, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(right, Dock.Right); dp.Children.Add(right);
            var mid = new StackPanel(); dp.Children.Add(mid);
            acctText = new TextBlock { Text = "start MetaTrader 5 with the SpasiXBridge EA on a chart (⚙ SETUP)", Foreground = Muted, FontSize = 12.5, FontWeight = FontWeights.SemiBold };
            acctBox = new Border { Child = acctText, CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1, 6, 2), Margin = new Thickness(-6, 0, 0, 5), HorizontalAlignment = HorizontalAlignment.Left, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "click = hide / show the name and number (STREAM MODE)" };
            acctBox.MouseLeftButtonUp += delegate { SetStream(!streamMode); streamBtn.Background = streamMode ? Tint(Violet, 160) : (Brush)Card; };
            mid.Children.Add(acctBox);
            var row = new WrapPanel(); mid.Children.Add(row);
            foreach (var k in new[] { "BALANCE", "EQUITY", "OPEN P/L", "MARGIN LEVEL", "FREE MARGIN", "MARGIN USED" })
            {
                var cap = new TextBlock { Text = k, Foreground = Muted, FontSize = 10, FontWeight = FontWeights.Bold };
                var val = new TextBlock { Text = "—", Foreground = Text, FontSize = 19, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas") };
                var sp = new StackPanel(); sp.Children.Add(cap); sp.Children.Add(val);
                var bd = new Border { Background = Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 6, 14, 7), Margin = new Thickness(0, 0, 8, 4), MinWidth = 150, BorderBrush = Line, BorderThickness = new Thickness(1), Child = sp };
                row.Children.Add(bd); cards[k] = Tuple.Create(cap, val, bd);
            }
            var eqHost = new Grid { Width = 230, Height = 46, Margin = new Thickness(4, 0, 0, 4), VerticalAlignment = VerticalAlignment.Center };
            eqHost.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1) });
            eqFill = new Border { CornerRadius = new CornerRadius(8), Background = Tint(Green, 90), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; eqHost.Children.Add(eqFill);
            eqText = new TextBlock { Text = "EQUITY / BALANCE —", Foreground = Text, FontSize = 11.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; eqHost.Children.Add(eqText);
            row.Children.Add(eqHost);
            return new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = dp };
        }
        // STREAM MODE: the name and number blurred under a violet veil
        void SetStream(bool on)
        {
            streamMode = on; acctText.Effect = on ? new BlurEffect { Radius = 9 } : null; acctBox.Background = on ? Tint(Violet, 70) : Brushes.Transparent;
            acctBox.ToolTip = on ? "STREAM MODE: hidden • click = show" : "click = hide / show the name and number (STREAM MODE)"; Put("stream", on ? "1" : "0");
        }
        // the chart over the whole window (F11 / Esc): only its own bar stays
        void SetFullChart(bool on)
        {
            fullChart = on; if (on) Show("CHART");
            headerEl.Visibility = on ? Visibility.Collapsed : Visibility.Visible; tabBarEl.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            WindowStyle = on ? WindowStyle.None : WindowStyle.SingleBorderWindow; if (on) { WindowState = WindowState.Normal; WindowState = WindowState.Maximized; }
            if (fullBtn != null) fullBtn.Content = on ? "⛶ EXIT FULL (Esc)" : "⛶ FULL";
        }

        // ---------------------------------------------------------------- tab bar
        FrameworkElement BuildTabBar()
        {
            var rowP = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var t in new[] { Tuple.Create("CHART", "📈  CHART"), Tuple.Create("POSITIONS", "💼  POSITIONS"), Tuple.Create("MARKET", "👁  MARKET"), Tuple.Create("NEWS", "📰  NEWS"), Tuple.Create("TRACKER", "📊  TRACKER"), Tuple.Create("BACKTEST", "🧪  BACKTEST"), Tuple.Create("SETUP", "⚙  SETUP") })
            {
                var key = t.Item1; var txt = new TextBlock { Text = t.Item2, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center };
                var line = new Border { Height = 3, CornerRadius = new CornerRadius(2), Background = Gold, Margin = new Thickness(6, 4, 6, 0), Opacity = 0 };
                var st = new StackPanel(); st.Children.Add(txt); st.Children.Add(line);
                var cell = new Border { Padding = new Thickness(18, 7, 18, 5), Margin = new Thickness(3, 0, 3, 0), CornerRadius = new CornerRadius(9), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Child = st };
                cell.MouseLeftButtonUp += delegate { Show(key); };
                cell.MouseEnter += delegate { if (view != key) cell.Background = Card; }; cell.MouseLeave += delegate { if (view != key) cell.Background = Brushes.Transparent; };
                rowP.Children.Add(cell); tabs.Add(Tuple.Create(key, cell, txt, line));
            }
            return new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(6, 6, 6, 5), Child = rowP };
        }
        void Show(string key)
        {
            view = key;
            foreach (var kv in views)
            {
                bool on = kv.Key == key; if (on && kv.Value.Visibility != Visibility.Visible) { kv.Value.Opacity = 0; kv.Value.Visibility = Visibility.Visible; kv.Value.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))); }
                else if (!on) kv.Value.Visibility = Visibility.Collapsed;
            }
            foreach (var t in tabs)
            {
                bool on = t.Item1 == key; t.Item3.Foreground = on ? Gold : Muted; t.Item2.Background = on ? Tint(Gold, 30) : Brushes.Transparent;
                if (on) { t.Item4.Opacity = 1; t.Item4.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(0, 60, TimeSpan.FromMilliseconds(220))); } else t.Item4.Opacity = 0;
            }
        }
        FrameworkElement Soon(string title, string what)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 680 };
            sp.Children.Add(new TextBlock { Text = title, Foreground = Gold, FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = "COMING NEXT", Foreground = Cyan, FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 10) });
            sp.Children.Add(new TextBlock { Text = what, Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            return sp;
        }
        public Button Pill(string t, Brush bg)
        {
            var b = new Button { Content = t, Foreground = Text, Background = bg, BorderBrush = Line, BorderThickness = new Thickness(1), Height = 30, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Bold, FontSize = 12, Cursor = System.Windows.Input.Cursors.Hand };
            var tpl = new ControlTemplate(typeof(Button)); var bd = new FrameworkElementFactory(typeof(Border)); bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent }); bd.SetValue(Border.BorderThicknessProperty, new Thickness(1)); bd.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); bd.AppendChild(cp); tpl.VisualTree = bd;
            b.Template = tpl; var keep = bg; b.MouseEnter += delegate { b.Tag = b.BorderBrush; b.BorderBrush = Gold; }; b.MouseLeave += delegate { b.BorderBrush = b.Tag as Brush ?? Line; };
            b.IsEnabledChanged += delegate { b.Opacity = b.IsEnabled ? 1 : 0.45; }; return b;
        }

        // ---------------------------------------------------------------- SETUP
        FrameworkElement BuildSetup()
        {
            var sp = new StackPanel { MaxWidth = 880, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            sp.Children.Add(new TextBlock { Text = "⚙ CONNECT METATRADER 5", Foreground = Gold, FontSize = 22, FontWeight = FontWeights.Bold });
            setupStatus = new TextBlock { Text = "", Foreground = Cyan, FontSize = 13.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 12), TextWrapping = TextWrapping.Wrap }; sp.Children.Add(setupStatus);
            setupSteps = new StackPanel(); sp.Children.Add(setupSteps);
            var steps = new[] {
                Tuple.Create("1", "Put the bridge in MT5", "MT5 ▸ File ▸ Open Data Folder ▸ MQL5 ▸ Experts → copy SpasiXBridge.mq5 there (replace the old one). Restart MT5: it compiles it."),
                Tuple.Create("2", "Allow the local link (once)", "MT5 ▸ Tools ▸ Options ▸ Expert Advisors: tick 'Allow WebRequest for listed URL' and add  http://127.0.0.1"),
                Tuple.Create("3", "Start it", "Drag SpasiXBridge onto ONE chart → OK. The chart's corner says 'SpasiX: connected to the app' and this window turns green."),
                Tuple.Create("4", "To TRADE from SpasiX (only when you want it)", "On the bridge's chart: right-click ▸ Expert list ▸ SpasiXBridge ▸ Properties: Common ▸ tick 'Allow Algo Trading'; Inputs ▸ AllowTrading = true (MaxLotsPerOrder = your limit). Turn ON 'Algo Trading' in the MT5 toolbar. Then press 🔓 ARM in SpasiX's trade panel."),
                Tuple.Create("✓", "Safe by default", "Without step 4 SpasiX only reads. Trading needs BOTH the bridge's AllowTrading AND the ARM button (each time SpasiX starts). Every order is answered with MT5's own result.") };
            foreach (var s0 in steps)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 8) }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) }); g.ColumnDefinitions.Add(new ColumnDefinition());
                var num = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = s0.Item1 == "✓" ? Tint(Green, 60) : Tint(Gold, 50), VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = s0.Item1, Foreground = s0.Item1 == "✓" ? Green : Gold, FontSize = 15, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                g.Children.Add(num);
                var tx = new StackPanel(); tx.Children.Add(new TextBlock { Text = s0.Item2, Foreground = Text, FontSize = 15, FontWeight = FontWeights.Bold }); tx.Children.Add(new TextBlock { Text = s0.Item3, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(tx, 1); g.Children.Add(tx);
                setupSteps.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), BorderBrush = Line, BorderThickness = new Thickness(1), Child = g, Margin = new Thickness(0, 0, 0, 8) });
            }
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }

        // ---------------------------------------------------------------- every 200 ms
        void Refresh()
        {
            var s = bridge.State;
            lock (s.Gate)
            {
                bool alive = s.Alive; var a = s.Account; string cur = a.Currency;
                if (bridge.Problem != "") { connText.Text = "● " + bridge.Problem; connPill.Background = Red; }
                else if (!alive) { connText.Text = s.Linked ? "● MT5 IS QUIET — is it frozen?" : everLinked ? "● MT5 DISCONNECTED" : "● WAITING FOR MT5"; connPill.Background = everLinked ? Red : Gold; }
                else { connText.Text = "● LIVE • " + a.Kind + (a.Connected ? "" : " • MT5 offline from the broker"); connPill.Background = a.Kind == "REAL" ? (a.Connected ? Green : Red) : Cyan; }
                setupStatus.Text = bridge.Problem != "" ? "⚠ " + bridge.Problem : alive ? "✓ CONNECTED to " + a.Server + " • " + a.Kind + " • " + s.Quotes.Count + " symbols • bridge v" + s.BridgeVersion + (s.BridgeVersion < 3 ? " — update to SpasiXBridge v3 (step 1) for trading" : s.CanTrade ? " • TRADING ALLOWED by the bridge" : " • read only (step 4 allows trading)") : "Listening on 127.0.0.1:" + bridge.Port + " — waiting for the SpasiXBridge EA …";
                setupStatus.Foreground = alive ? Green : bridge.Problem != "" ? Red : Cyan;
                if (!alive && !s.Linked) { if (armed) SetArmed(false, "MT5 disconnected"); return; }
                if (!everLinked && alive) { everLinked = true; Show("CHART"); if (Get("stream", "0") == "1") SetStream(true); }
                acctText.Text = a.Login + " • " + a.Name + " • " + a.Server + " • " + a.Kind + " • " + (a.Mode == "HEDGE" ? "HEDGING" : "NETTING") + " • 1:" + a.Leverage + (s.Company != "" ? " • " + s.Company : "");
                acctText.Foreground = Text;
                Action<string, string, Brush> card = (k, v, fg) => { cards[k].Item2.Text = v; cards[k].Item2.Foreground = fg; };
                card("BALANCE", Money(a.Balance, cur), Text); card("EQUITY", Money(a.Equity, cur), a.Equity >= a.Balance ? Green : Text);
                double fl = a.Floating; card("OPEN P/L", Signed(fl, cur), fl >= 0 ? Green : Red); cards["OPEN P/L"].Item3.BorderBrush = fl >= 0 ? Tint(Green, 120) : Tint(Red, 160);
                card("MARGIN LEVEL", a.Margin > 0 ? a.MarginLevel.ToString("N0", IC) + " %" : "—", a.Margin <= 0 ? Muted : a.MarginLevel < 200 ? Red : a.MarginLevel < 500 ? Gold : Green);
                card("FREE MARGIN", Money(a.FreeMargin, cur), Text); card("MARGIN USED", Money(a.Margin, cur), Muted);
                double ratio = a.Balance > 0 ? Math.Max(0, Math.Min(1, a.Equity / a.Balance)) : 0; eqFill.Width = 230 * ratio; eqFill.Background = ratio >= 0.9 ? Tint(Green, 90) : ratio >= 0.6 ? Tint(Gold, 110) : Tint(Red, 130);
                eqText.Text = "EQUITY = " + (100 * (a.Balance > 0 ? a.Equity / a.Balance : 0)).ToString("0", IC) + " % OF THE BALANCE";
                ReadResults(s);
                if (view == "CHART") FillChart(s); if (view == "POSITIONS") FillPositions(s); if (view == "MARKET") FillMarket(s); if (view == "NEWS") FillNews();
            }
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
            sp.Children.Add(new TextBlock { Text = "connecting to MetaTrader 5 …", Foreground = br("#7D879A"), FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center });
            w.Content = new Border { CornerRadius = new CornerRadius(22), Background = new LinearGradientBrush(br("#141C2C").Color, br("#070A12").Color, 90), BorderBrush = br("#22304A"), BorderThickness = new Thickness(1), Child = sp };
            return w;
        }
        [STAThread]
        public static void Main(string[] args)
        {
            int port = 47123; if (args.Length > 0) int.TryParse(args[0], out port);
            var bridge = new Mt5Bridge(port); bridge.Start();
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (s, e) => { MessageBox.Show(e.Exception.Message, "SpasiX"); e.Handled = true; };
            var splash = Splash(); splash.Show(); var t0 = DateTime.Now;
            var main = new MainWindow(bridge); app.MainWindow = main;
            var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(50, 1400 - (DateTime.Now - t0).TotalMilliseconds)) };
            tm.Tick += delegate { tm.Stop(); main.Show(); main.Activate(); splash.Close(); };
            tm.Start(); app.Run();
        }
    }
}
