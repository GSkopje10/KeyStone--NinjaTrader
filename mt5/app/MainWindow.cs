// SpasiX — the window of one MODE (LIVE / PROP / BACKTEST, chosen on the start screen): the header (logo, the mode, the account — STREAM MODE blurs
// only the name and the number —, the account cards, the connection), the views, the tab bar at the bottom, the settings file and the refresh.
// The views live in ChartTab.cs, PositionsTab.cs, MarketNews.cs, Tracker.cs, Prop.cs and Backtest.cs; the start screen in Launcher.cs.
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
        public const string Build = "SpasiX • BUILD 2026-10-29d";
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static SolidColorBrush B(string hex) { return Sx.B(hex); }
        static readonly SolidColorBrush Bg = Sx.Bg, Panel = Sx.Panel, Card = Sx.Card, Line = Sx.Line, Text = Sx.Text, Muted = Sx.Muted,
            Gold = Sx.Gold, Green = Sx.Green, Red = Sx.Red, Cyan = Sx.Cyan, Violet = Sx.Violet, Orange = Sx.Orange, Amber = Sx.Amber;
        static SolidColorBrush Tint(SolidColorBrush b, byte a) { return Sx.Tint(b, a); }
        static string Money(double v, string cur) { return (v < 0 ? "−" : "") + (cur == "USD" ? "$" : "") + Math.Abs(v).ToString("N2", IC) + (cur == "USD" ? "" : " " + cur); }
        static string Signed(double v, string cur) { return (v > 0 ? "+" : "") + Money(v, cur); }
        public static ImageSource Logo() { try { return BitmapFrame.Create(new Uri("pack://application:,,,/assets/spasix.png")); } catch { return null; } }

        public readonly string Mode;
        readonly Mt5Bridge live; readonly IBroker broker; readonly SimBroker sim; readonly DispatcherTimer ui, simTimer;
        bool Sim { get { return sim != null; } }
        readonly PaperBroker paper; bool Paper { get { return paper != null; } } bool Virtual { get { return Sim || Paper; } }   // DEMO = live prices + a simulated account
        string view = ""; bool everLinked, streamMode, fullChart; DateTime simLast = DateTime.Now;
        readonly Dictionary<string, FrameworkElement> views = new Dictionary<string, FrameworkElement>();
        readonly List<Tuple<string, Border, TextBlock, Border>> tabs = new List<Tuple<string, Border, TextBlock, Border>>();
        FrameworkElement headerEl, tabBarEl; Grid rootGrid;
        TextBlock connText, acLogin, acName, acRest, modeText; Border connPill, acPrivate, modeBadge; readonly Dictionary<string, Tuple<TextBlock, TextBlock, Border>> cards = new Dictionary<string, Tuple<TextBlock, TextBlock, Border>>();
        StackPanel clockRow; readonly List<Tuple<string, TextBlock, TextBlock, Border, string>> clocks = new List<Tuple<string, TextBlock, TextBlock, Border, string>>();
        Border eqFill, modeBanner; TextBlock modeBannerText; DropShadowEffect plGlow, eqGlow; int plSign = 9, eqSign = 9; StackPanel setupSteps; TextBlock setupStatus; Button streamBtn;

        // the app's own settings (%AppData%\SpasiX\MT5\settings.txt) and drawings (drawings.txt)
        public static string Dir { get { return DataDir.Root; } }   // F:\SpasiX\MT5 when there is an F: drive (DataDir.cs)
        static string SetFile { get { return Path.Combine(Dir, "settings.txt"); } } static string DrawFile { get { return Path.Combine(Dir, "drawings.txt"); } }
        static readonly Dictionary<string, string> set = new Dictionary<string, string>(); static bool setLoaded;   // shared by every open mode (one settings file)
        void LoadSet() { if (setLoaded) return; setLoaded = true; try { foreach (var l in File.ReadAllLines(SetFile)) { int i = l.IndexOf('='); if (i > 0) set[l.Substring(0, i)] = l.Substring(i + 1); } } catch { } }
        void SaveSet() { try { Directory.CreateDirectory(Dir); File.WriteAllLines(SetFile, set.Select(kv => kv.Key + "=" + kv.Value)); } catch { } }
        string Get(string k, string d) { string v; return set.TryGetValue(k, out v) ? v : d; }
        void Put(string k, string v) { string o; if (set.TryGetValue(k, out o) && o == v) return; set[k] = v; SaveSet(); }
        public Button Pill(string t, Brush bg) { return Sx.Pill(t, bg); }

        public MainWindow(Mt5Bridge b, string mode)
        {
            live = b; Mode = mode; if (mode == "BACKTEST") { sim = new SimBroker(); broker = sim; } else if (mode == "DEMO") { paper = new PaperBroker(b); paper.LoadSaved(); broker = paper; } else broker = live;
            Title = "SpasiX • " + (mode == "BACKTEST" ? "BACKTEST" : mode == "PROP" ? "PROP" : mode == "DEMO" ? "DEMO" : "LIVE"); Width = 1440; Height = 900; MinWidth = 1080; MinHeight = 640; Background = Bg; Foreground = Text;
            FontFamily = Sx.Face; WindowStartupLocation = WindowStartupLocation.CenterScreen; WindowState = WindowState.Maximized;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            var lg = Logo(); if (lg != null) Icon = lg; Sx.DarkTitle(this);
            var ts = Environment.GetEnvironmentVariable("SPASIX_TEST_SIZE"); if (!string.IsNullOrEmpty(ts) && ts.Contains("x")) { WindowState = WindowState.Normal; WindowStartupLocation = WindowStartupLocation.Manual; Left = 0; Top = 0; Width = double.Parse(ts.Split('x')[0], IC); Height = double.Parse(ts.Split('x')[1], IC); }
            LoadSet(); LoadDrawings(); streamMode = Get("stream", "0") == "1";
            rootGrid = new Grid(); Content = Shell.Chrome(this, mode, rootGrid);
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headerEl = BuildHeader(); Grid.SetRow(headerEl, 0); rootGrid.Children.Add(headerEl);
            var host = new Grid { Margin = new Thickness(10, 6, 10, 6) }; Grid.SetRow(host, 1); rootGrid.Children.Add(host);
            var order = new List<Tuple<string, string>>();
            if (Sim) { views["DATA"] = BuildBacktestData(); order.Add(Tuple.Create("DATA", "📂  DATA")); }
            views["CHART"] = BuildChartTab(); order.Add(Tuple.Create("CHART", "📈  CHART"));
            views["ANALYSIS"] = BuildAnalysis(); order.Add(Tuple.Create("ANALYSIS", "📊  ANALYSIS"));
            views["POSITIONS"] = BuildPositions(); order.Add(Tuple.Create("POSITIONS", "💼  POSITIONS"));
            if (Mode == "PROP") { views["PROP"] = BuildProp(); order.Add(Tuple.Create("PROP", "🏆  PROP")); }
            views["TRACKER"] = BuildTracker(); order.Add(Tuple.Create("TRACKER", Sim ? "📊  STATS" : "📊  TRACKER"));
            if (Sim) { views["POOL"] = BuildPool(); order.Add(Tuple.Create("POOL", "🗂  POOL")); }
            if (!Sim) { views["MARKET"] = BuildMarket(); order.Add(Tuple.Create("MARKET", "👁  MARKET")); views["NEWS"] = BuildNews(); order.Add(Tuple.Create("NEWS", "📰  NEWS")); views["SETUP"] = BuildSetup(); order.Add(Tuple.Create("SETUP", "⚙  SETUP")); }
            foreach (var v in views.Values) { v.Visibility = Visibility.Collapsed; host.Children.Add(v); }
            tabBarEl = BuildTabBar(order); Grid.SetRow(tabBarEl, 2); rootGrid.Children.Add(tabBarEl);
            SxMenu.Attach(this, rootGrid);
            Show(Sim ? "DATA" : "SETUP");
            ui = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(Sim ? 100 : 200) }; ui.Tick += delegate { Refresh(); }; ui.Start();
            if (Sim)
            {
                simTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(30) };
                simTimer.Tick += delegate { var now = DateTime.Now; double dt = (now - simLast).TotalSeconds; simLast = now; bool was = sim.Playing; sim.Advance(dt); if (was && view == "CHART") foreach (var p in panes.Take(split)) p.V.InvalidateVisual(); };
                simTimer.Start();
            }
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.F10 || e.SystemKey == System.Windows.Input.Key.F10) { if (spreadPanel != null) { Show("CHART"); ToggleSpreads(); } e.Handled = true; return; }
                if (e.Key == System.Windows.Input.Key.F11 || (e.Key == System.Windows.Input.Key.Escape && fullChart && !SxMenu.IsOpen(this))) { SetFullChart(!fullChart); e.Handled = true; return; }
                if (Sim && view == "CHART" && !(System.Windows.Input.Keyboard.FocusedElement is TextBox))
                {
                    bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
                    if (e.Key == System.Windows.Input.Key.Space) { TogglePlay(); e.Handled = true; }
                    else if (e.Key == System.Windows.Input.Key.Right) { StepCandle(shift ? 1 : Act.Tf); e.Handled = true; }
                    else if (e.Key == System.Windows.Input.Key.Left && sim.Spec != null) { Back(sim.PrevBar(shift ? 1 : Act.Tf), shift ? "one minute" : "one candle"); e.Handled = true; }
                    else if (e.Key == System.Windows.Input.Key.PageDown && sim.Spec != null) { Jump(sim.NextDayStart()); e.Handled = true; }
                    else if (e.Key == System.Windows.Input.Key.PageUp && sim.Spec != null) { Back(sim.PrevDayStart(), "the day's start"); e.Handled = true; }
                }
            };
            // leaving: asks first (your positions stay open in MT5)
            Closing += (s, e) =>
            {
                if (Shell.Quitting) return;
                int open, ords; double pl; lock (broker.State.Gate) { open = broker.State.Positions.Count; ords = broker.State.Orders.Count; pl = broker.State.Positions.Sum(p => p.Profit + p.Swap); }
                string line = Sim ? "The backtest ends" + (open > 0 ? " with its " + open + " simulated position" + (open == 1 ? "" : "s") : "") + " — its finished days stay in the 🗂 POOL. Nothing is real."
                    : Paper ? "The DEMO account is saved on this PC with everything open; it follows the live prices again when you come back."
                    : (open > 0 ? "Your positions stay open in MetaTrader 5 with their stops and targets — only this window closes." : "Nothing is open.") + (armed ? " Trading disarms." : "");
                var tiles = Sx.Tiles(Tuple.Create(Sim ? "SIMULATED" : Paper ? "DEMO TICKETS" : "OPEN TICKETS", open.ToString(IC), open > 0 ? Cyan : Muted), Tuple.Create("OPEN P/L", open > 0 ? Signed(pl, "USD") : "—", pl > 0 ? Green : pl < 0 ? Red : Muted),
                    Tuple.Create("ORDERS", ords.ToString(IC), ords > 0 ? Amber : Muted), Tuple.Create(Virtual ? "MODE" : "TRADING", Virtual ? Mode : armed ? "ARMED" : "OFF", armed ? Red : Muted));
                int k = Sx.Choice(this, "Close " + Shell.NameOf(Mode) + "?", line, tiles, new[] { "✕  CLOSE " + Mode + "  →  HOME", "⏻  CLOSE SPASIX", "STAY" }, new[] { Shell.ColorOf(Mode), Red, (SolidColorBrush)null });
                if (k == 0) { armed = false; return; }
                if (k == 1) { armed = false; e.Cancel = true; Dispatcher.BeginInvoke(new Action(Shell.Quit)); return; }
                e.Cancel = true;
            };
            Closed += delegate { ui.Stop(); if (simTimer != null) simTimer.Stop(); SaveDrawings(); if (Paper) paper.Save(); if (Sim && sim.Spec != null) { SaveRun(); PoolSaveAll(); } };
            if (Paper) Loaded += delegate { Dispatcher.BeginInvoke(new Action(() => { if (!paper.Ready) DemoSetup(true); }), DispatcherPriority.Background); };
            if (!Sim) StartNews();
        }
        // back to the start screen (asks first while armed / in a backtest with trades)
        void GoLauncher() { Shell.ShowHome(); }   // HOME: this mode keeps running in its tab

        // ---------------------------------------------------------------- header
        FrameworkElement BuildHeader()
        {
            var dp = new DockPanel { LastChildFill = true, Margin = new Thickness(14, 8, 14, 8) };
            var logo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            var lgI = Logo(); if (lgI != null) logo.Children.Add(new Image { Source = lgI, Width = 38, Height = 38, Margin = new Thickness(0, 0, 8, 0) });
            var nm = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var word = new StackPanel { Orientation = Orientation.Horizontal };
            word.Children.Add(new TextBlock { Text = "Spasi", Foreground = Gold, FontSize = 24, FontWeight = FontWeights.Bold }); word.Children.Add(new TextBlock { Text = "X", Foreground = Cyan, FontSize = 24, FontWeight = FontWeights.Bold }); nm.Children.Add(word);
            var mc = Mode == "BACKTEST" ? Cyan : Mode == "PROP" ? Violet : Mode == "DEMO" ? Orange : Green;
            modeText = new TextBlock { Text = Mode == "BACKTEST" ? "🧪 BACKTEST" : Mode == "PROP" ? "🏆 PROP" : Mode == "DEMO" ? "🎮 DEMO" : "● LIVE", Foreground = mc, FontSize = 10, FontWeight = FontWeights.Bold };
            modeBadge = new Border { Child = modeText, Background = Tint(mc, 40), BorderBrush = Tint(mc, 150), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1, 6, 1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 1, 0, 0) };
            nm.Children.Add(modeBadge); logo.Children.Add(nm);
            DockPanel.SetDock(logo, Dock.Left); dp.Children.Add(logo);
            connText = new TextBlock { Text = "● WAITING FOR MT5", Foreground = Bg, FontSize = 11.5, FontWeight = FontWeights.Bold };
            connPill = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 4, 12, 4), Background = Gold, Child = connText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (!Sim)
            {
                streamBtn = Pill("👁 STREAM", Card); streamBtn.ToolTip = "STREAM MODE: blur your name and account number (everything else stays visible)"; streamBtn.Click += delegate { SetStream(!streamMode); };
                right.Children.Add(streamBtn);
            }
            right.Children.Add(connPill);
            var home = Pill("⌂ HOME", Card); home.ToolTip = "the start screen — this mode keeps running in its tab (top)"; home.Click += delegate { GoLauncher(); }; right.Children.Add(home);
            right.Children.Add(new TextBlock { Text = Build, Foreground = Muted, FontSize = 10.5, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            // the clocks: the time and the day in New York, London, Tokyo, MT5's server (+ the chart's zone) — in a backtest: the replay's time
            var rightAll = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; rightAll.Children.Add(right);
            clockRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) }; rightAll.Children.Add(clockRow);
            foreach (var z in new[] { Tuple.Create("NEW YORK", "Eastern Standard Time", Gold), Tuple.Create("LONDON", "GMT Standard Time", Cyan), Tuple.Create("TOKYO", "Tokyo Standard Time", Violet), Tuple.Create("SERVER", "", Green), Tuple.Create("CHART", "*", Orange) })
            {
                var cap = new TextBlock { Text = z.Item1, Foreground = z.Item3, FontSize = 9, FontWeight = FontWeights.Bold };
                var val = new TextBlock { Text = "--:--:--", Foreground = Text, FontSize = 13.5, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono };
                var st = new StackPanel(); st.Children.Add(cap); st.Children.Add(val);
                var bd = new Border { Child = st, Background = Tint(z.Item3, 26), BorderBrush = Tint(z.Item3, 120), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2, 9, 3), Margin = new Thickness(5, 0, 0, 0), MinWidth = 96 };
                clockRow.Children.Add(bd); clocks.Add(Tuple.Create(z.Item2, cap, val, bd, z.Item1));
            }
            if (!Sim) clockRow.Children.Insert(0, BuildNewsBadge());
            DockPanel.SetDock(rightAll, Dock.Right); dp.Children.Add(rightAll);
            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; dp.Children.Add(mid);
            // the account line: the name and the number apart (STREAM MODE blurs only them)
            var acc = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            acLogin = new TextBlock { Foreground = Text, FontSize = 12, FontWeight = FontWeights.Bold }; acName = new TextBlock { Foreground = Text, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0) };
            var priv = new StackPanel { Orientation = Orientation.Horizontal }; priv.Children.Add(acLogin); priv.Children.Add(acName);
            acPrivate = new Border { Child = priv, CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(-6, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "click = hide / show the name and number (STREAM MODE)" };
            if (!Sim) acPrivate.MouseLeftButtonUp += delegate { SetStream(!streamMode); };
            acRest = new TextBlock { Text = Sim ? "" : "start MetaTrader 5 with the SpasiXBridge EA on a chart (⚙ SETUP)", Foreground = Muted, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            acc.Children.Add(acPrivate); acc.Children.Add(acRest);
            // the mode, big and in the middle: LIVE (real money) / PROP / DEMO (the live account is not touched) / BACKTEST
            var bc = Mode == "BACKTEST" ? Cyan : Mode == "PROP" ? Violet : Mode == "DEMO" ? Orange : Green;
            modeBannerText = new TextBlock { Text = Mode == "BACKTEST" ? "🧪  BACKTEST  •  history replay — nothing is real" : Mode == "DEMO" ? "🎮  DEMO TRADING  •  your live account is not affected" : Mode == "PROP" ? "🏆  PROP ACCOUNT  •  LIVE TRADING" : "●  LIVE TRADING  •  REAL MONEY",
                Foreground = bc, FontSize = 14, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            modeBanner = new Border { Child = modeBannerText, Background = Tint(bc, 34), BorderBrush = bc, BorderThickness = new Thickness(1.6), CornerRadius = new CornerRadius(14), Padding = new Thickness(18, 3, 18, 4), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = bc.Color, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.55 } };
            modeBanner.Loaded += delegate { modeBanner.Effect.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.7, 0.15, TimeSpan.FromMilliseconds(1300)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }); };
            var accRow = new Grid { Margin = new Thickness(0, 0, 0, 4) }; accRow.Children.Add(acc); modeBanner.Padding = new Thickness(16, 2, 16, 3); modeBannerText.FontSize = 13; Shell.SetCenter(this, modeBanner); acc.Margin = new Thickness(0); acc.VerticalAlignment = VerticalAlignment.Center;
            mid.Children.Add(accRow);
            var row = new WrapPanel(); mid.Children.Add(row);
            foreach (var k in new[] { "OPEN P/L", "BALANCE", "EQUITY", "TODAY", "MARGIN LEVEL", "FREE MARGIN" })
            {
                bool big = k == "OPEN P/L", eq = k == "EQUITY";   // the two numbers that matter most stand out: a coloured, pulsing border
                var cap = new TextBlock { Text = k == "MARGIN LEVEL" ? "MARGIN" : k, Foreground = big || eq ? Gold : Muted, FontSize = big || eq ? 10.5 : 9.5, FontWeight = FontWeights.Bold };
                var val = new TextBlock { Text = "—", Foreground = Text, FontSize = big ? 27 : eq ? 22 : 17, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono };
                var sp = new StackPanel(); sp.Children.Add(cap); sp.Children.Add(val);
                if (k == "EQUITY") { var track = new Grid { Height = 4, Width = 108, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 3, 0, 0) }; track.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Line }); eqFill = new Border { CornerRadius = new CornerRadius(2), Background = Green, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; track.Children.Add(eqFill); sp.Children.Add(track); }
                var bd = new Border { Background = big || eq ? (Brush)Sx.CardGrad : Card, CornerRadius = new CornerRadius(big || eq ? 12 : 10), Padding = big || eq ? new Thickness(12, 3, 14, 4) : new Thickness(10, 4, 10, 5), Margin = new Thickness(0, 0, big || eq ? 10 : 7, 0), MinWidth = big ? 190 : eq ? 160 : 108, BorderBrush = eq ? Cyan : Line, BorderThickness = new Thickness(big || eq ? 2.4 : 1), Child = sp };
                row.Children.Add(bd); cards[k] = Tuple.Create(cap, val, bd);
                if (big) plGlow = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 16, Opacity = 0, Color = Sx.C("#2EE59D") };
                if (big) bd.Effect = plGlow;
                if (eq) { eqGlow = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 14, Opacity = 0.5, Color = Cyan.Color }; bd.Effect = eqGlow; }
            }
            cards["TODAY"].Item3.ToolTip = "today (the server's day): the closed trades after commission and swap + what is open now";
            if (Mode == "PROP") { propStrip = BuildPropStrip(); mid.Children.Add(propStrip); }
            if (streamMode && !Sim) ApplyStream();
            return new Border { Background = Sx.HeadGrad, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = dp };
        }
        // STREAM MODE: the name and number blurred under a violet veil — the rest of the line and every number stay visible
        void SetStream(bool on) { streamMode = on; Put("stream", on ? "1" : "0"); ApplyStream(); }
        void ApplyStream()
        {
            var fx = streamMode ? new BlurEffect { Radius = 8 } : null; acLogin.Effect = fx; acName.Effect = streamMode ? new BlurEffect { Radius = 8 } : null;
            acPrivate.Background = streamMode ? Tint(Violet, 70) : Brushes.Transparent; acPrivate.ToolTip = streamMode ? "STREAM MODE: the name and number are hidden • click = show" : "click = hide the name and number (STREAM MODE)";
            if (streamBtn != null) { streamBtn.Background = streamMode ? Tint(Violet, 170) : (Brush)Card; streamBtn.Content = streamMode ? "👁 STREAM ON" : "👁 STREAM"; }
        }
        // the chart over the whole window (F11 / Esc): header, tabs, toolbar, tools and panel go; one small strip of buttons stays
        void SetFullChart(bool on)
        {
            fullChart = on; if (on) Show("CHART");
            headerEl.Visibility = on ? Visibility.Collapsed : Visibility.Visible; tabBarEl.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            if (on) WindowState = WindowState.Maximized;
            if (fullBtn != null) fullBtn.Content = on ? "⛶ EXIT FULL (Esc)" : "⛶ FULL";
            ChartFull(on);
        }

        // ---------------------------------------------------------------- tab bar
        FrameworkElement BuildTabBar(List<Tuple<string, string>> order)
        {
            var rowP = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var t in order)
            {
                var key = t.Item1; var txt = new TextBlock { Text = t.Item2, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center };
                var line = new Border { Height = 3, CornerRadius = new CornerRadius(2), Background = new LinearGradientBrush(Gold.Color, Cyan.Color, 0), Margin = new Thickness(6, 4, 6, 0), Opacity = 0 };
                var st = new StackPanel(); st.Children.Add(txt); st.Children.Add(line);
                var cell = new Border { Padding = new Thickness(18, 7, 18, 5), Margin = new Thickness(3, 0, 3, 0), CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Child = st };
                cell.MouseLeftButtonUp += delegate { Show(key); };
                cell.MouseEnter += delegate { if (view != key) { cell.Background = Card; txt.Foreground = Text; } }; cell.MouseLeave += delegate { if (view != key) { cell.Background = Brushes.Transparent; txt.Foreground = Muted; } };
                rowP.Children.Add(cell); tabs.Add(Tuple.Create(key, cell, txt, line));
            }
            return new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(6, 6, 6, 5), Child = rowP };
        }
        void Show(string key)
        {
            if (!views.ContainsKey(key)) return; view = key;
            foreach (var kv in views)
            {
                bool on = kv.Key == key;
                if (on && kv.Value.Visibility != Visibility.Visible)
                {
                    kv.Value.Opacity = 0; kv.Value.Visibility = Visibility.Visible; kv.Value.RenderTransform = new TranslateTransform(0, 8);
                    kv.Value.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                    ((TranslateTransform)kv.Value.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                }
                else if (!on) kv.Value.Visibility = Visibility.Collapsed;
            }
            foreach (var t in tabs)
            {
                bool on = t.Item1 == key; t.Item3.Foreground = on ? Gold : Muted; t.Item2.Background = on ? Tint(Gold, 30) : Brushes.Transparent;
                if (on) { t.Item4.Opacity = 1; t.Item4.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(0, 60, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }); } else t.Item4.Opacity = 0;
            }
            if (key == "TRACKER") trackSig = "";
        }

        // where SpasiX keeps its data (history, ticks, pool, settings): F: by default when there is one; CHANGE moves it at the next start
        FrameworkElement DataFolderCard()
        {
            var st = new StackPanel(); string free = ""; try { var d = new DriveInfo(Path.GetPathRoot(Dir)); free = " • " + (d.AvailableFreeSpace / 1073741824.0).ToString("0.0", IC) + " GB free on " + d.Name; } catch { }
            st.Children.Add(new TextBlock { Text = "💾 THE DATA FOLDER", Foreground = Gold, FontSize = 15, FontWeight = FontWeights.Bold });
            st.Children.Add(new TextBlock { Text = Dir + free, Foreground = Text, FontSize = 13, FontFamily = Sx.Mono, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap });
            st.Children.Add(new TextBlock { Text = (DataDir.Moved != "" ? "✓ " + DataDir.Moved + "\n" : "") + "the downloaded candles and real ticks, the backtest POOL, the DEMO account, settings and drawings live here", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var b = Pill("📁 CHANGE THE FOLDER …", Card); b.Margin = new Thickness(0, 8, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Click += delegate
            {
                var n = Sx.Prompt(this, "📁 The data folder", "Where SpasiX keeps its data (e.g. F:\\SpasiX\\MT5). Everything moves there the next time SpasiX starts.", Dir); if (string.IsNullOrWhiteSpace(n)) return;
                try { Directory.CreateDirectory(n.Trim()); } catch (Exception ex) { Sx.Info(this, "📁 The data folder", "That folder cannot be used: " + ex.Message, Red); return; }
                DataDir.Change(n.Trim()); Sx.Info(this, "📁 The data folder", "Saved. Close SpasiX and start it again — your data moves to\n" + n.Trim(), Gold);
            };
            st.Children.Add(b);
            return new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 12), BorderBrush = Tint(Gold, 110), BorderThickness = new Thickness(1), Child = st, Margin = new Thickness(0, 6, 0, 8) };
        }

        // ---------------------------------------------------------------- SETUP
        FrameworkElement BuildSetup()
        {
            var sp = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            sp.Children.Add(new TextBlock { Text = "⚙ CONNECT METATRADER 5", Foreground = Gold, FontSize = 22, FontWeight = FontWeights.Bold });
            setupStatus = new TextBlock { Text = "", Foreground = Cyan, FontSize = 13.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 12), TextWrapping = TextWrapping.Wrap }; sp.Children.Add(setupStatus);
            setupSteps = new StackPanel(); sp.Children.Add(setupSteps);
            var steps = new[] {
                Tuple.Create("1", "Put the bridge in MT5 (v5)", "MT5 ▸ File ▸ Open Data Folder ▸ MQL5 ▸ Experts → copy SpasiXBridge.mq5 there (replace the old one). Restart MT5: it compiles it. Remove the old bridge from its chart and drag the new one on (so v5 and its inputs apply)."),
                Tuple.Create("2", "Allow the local link (once)", "MT5 ▸ Tools ▸ Options ▸ Expert Advisors: tick 'Allow WebRequest for listed URL' and add  http://127.0.0.1"),
                Tuple.Create("3", "Start it", "Drag SpasiXBridge onto ONE chart → in its window tick 'Allow Algo Trading' (Common tab) → OK. The chart's corner says 'SpasiX: connected to the app' and this window turns green."),
                Tuple.Create("4", "Trading from SpasiX", "Turn ON 'Algo Trading' in the MT5 toolbar (the button turns green) and keep 'Allow Algo Trading' ticked on the bridge. Then one click on 🔓 ARM in SpasiX's trade panel. The panel shows a ✓ / ✖ for every switch."),
                Tuple.Create("5", "History for the backtest", "MT5 ▸ Tools ▸ Options ▸ Charts ▸ Max bars in chart = Unlimited. SpasiX downloads MT5's 1-minute candles AND its real ticks for the days you choose and keeps them on this PC."),
                Tuple.Create("✓", "How it trades", "Nothing is sent until you ARM. Then BUY / SELL and the ✕ on a ticket act at once; every close-all, breakeven and the basket ask first. MaxLotsPerOrder in the bridge caps every order; every order is answered with MT5's own result. 🎮 DEMO and 🧪 BACKTEST never send anything to MT5.") };
            foreach (var s0 in steps)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 8) }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) }); g.ColumnDefinitions.Add(new ColumnDefinition());
                var num = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = s0.Item1 == "✓" ? Tint(Green, 60) : Tint(Gold, 50), VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = s0.Item1, Foreground = s0.Item1 == "✓" ? Green : Gold, FontSize = 15, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                g.Children.Add(num);
                var tx = new StackPanel(); tx.Children.Add(new TextBlock { Text = s0.Item2, Foreground = Text, FontSize = 15, FontWeight = FontWeights.Bold }); tx.Children.Add(new TextBlock { Text = s0.Item3, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(tx, 1); g.Children.Add(tx);
                setupSteps.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), BorderBrush = Line, BorderThickness = new Thickness(1), Child = g, Margin = new Thickness(0, 0, 0, 8) });
            }
            sp.Children.Add(DataFolderCard());
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }

        // the clocks: every zone's time and day, from MT5's server clock (or the replay's)
        void PaintClocks(Mt5State s)
        {
            if (clocks.Count == 0) return; int off; DateTime srv; lock (s.Gate) { off = s.ServerOffsetSec; srv = s.ServerNow; }
            DateTime utc = Sim ? (sim.Spec != null ? srv.AddSeconds(-off) : DateTime.MinValue) : DateTime.UtcNow;
            string chartZone = Zones[Math.Max(0, Math.Min(Zones.Length - 1, zoneIdx))].Item2, chartName = Zones[Math.Max(0, Math.Min(Zones.Length - 1, zoneIdx))].Item1;
            foreach (var c in clocks)
            {
                string id = c.Item1 == "*" ? chartZone : c.Item1; bool dup = c.Item1 == "*" && (chartZone == "" || clocks.Any(x => x.Item1 != "*" && x.Item1 == chartZone));
                c.Item4.Visibility = dup ? Visibility.Collapsed : Visibility.Visible; if (dup) continue;
                string label = c.Item1 == "*" ? chartName : c.Item5;
                if (utc == DateTime.MinValue) { c.Item3.Text = "--:--:--"; continue; }
                DateTime t;
                if (id == "") t = utc.AddSeconds(off);
                else { try { var tz = id == "LOCAL" ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(id); t = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz); } catch { t = utc; } }
                c.Item3.Text = t.ToString("HH:mm:ss", IC); c.Item2.Text = label + "  " + t.ToString("ddd dd MMM", IC).ToUpperInvariant();
                c.Item4.ToolTip = (c.Item1 == "*" ? "the chart's time zone" : c.Item5) + " • " + t.ToString("dddd dd MMMM yyyy HH:mm:ss", IC) + (Sim ? " (the replay's time)" : "");
            }
        }
        // the OPEN P/L card: big, its border pulses green / red while tickets are open
        // the EQUITY card: a bold border, green when the equity is at or above the balance, red under it, slowly pulsing
        void PaintEq(double equity, double balance)
        {
            int sg = equity >= balance - 1e-6 ? 1 : -1; if (sg == eqSign || eqGlow == null) return; eqSign = sg; var bd = cards["EQUITY"].Item3;
            var col = sg > 0 ? Green.Color : Red.Color; var br = new SolidColorBrush(col); bd.BorderBrush = br; eqGlow.Color = col;
            br.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(1200)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            eqGlow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.75, 0.2, TimeSpan.FromMilliseconds(1200)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        }
        void PaintPl(double fl, bool open)
        {
            int sg = !open ? 0 : fl >= 0 ? 1 : -1; var bd = cards["OPEN P/L"].Item3; if (sg == plSign) return; plSign = sg;
            if (sg == 0) { bd.BorderBrush = Line; plGlow.BeginAnimation(DropShadowEffect.OpacityProperty, null); plGlow.Opacity = 0; return; }
            var col = sg > 0 ? Green.Color : Red.Color; var br = new SolidColorBrush(col); bd.BorderBrush = br; plGlow.Color = col;
            var pulse = new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(750)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            br.BeginAnimation(Brush.OpacityProperty, pulse); plGlow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.85, 0.15, TimeSpan.FromMilliseconds(750)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        }
        // ---------------------------------------------------------------- the refresh
        void Refresh()
        {
            var s = broker.State; if (Paper) paper.Sync(); BasketWatch(s); EscapeWatch(s); PaintClocks(s);
            lock (s.Gate)
            {
                var a = s.Account; string cur = a.Currency;
                if (Sim) RefreshSimHeader(s);
                else
                {
                    bool alive = s.Alive;
                    if (live.Problem != "") { connText.Text = "● " + live.Problem; connPill.Background = Red; }
                    else if (!alive) { connText.Text = s.Linked ? "● MT5 IS QUIET — frozen?" : everLinked ? "● MT5 DISCONNECTED" : "● WAITING FOR MT5"; connPill.Background = everLinked ? Red : Gold; }
                    else if (Paper) { connText.Text = paper.Ready ? "● DEMO • LIVE PRICES" : "● DEMO • SET UP THE ACCOUNT"; connPill.Background = Orange; }
                    else { connText.Text = "● " + a.Kind + (a.Connected ? "" : " • MT5 OFFLINE FROM THE BROKER") + (armed ? " • ARMED" : ""); connPill.Background = a.Kind == "REAL" ? (a.Connected ? Green : Red) : Cyan; }
                    setupStatus.Text = live.Problem != "" ? "⚠ " + live.Problem : alive ? "✓ CONNECTED to " + a.Server + " • " + a.Kind + " • " + s.Quotes.Count + " symbols • bridge v" + s.BridgeVersion + (s.BridgeVersion < 5 ? " — put SpasiXBridge v5 in MT5 (step 1) for the real ticks, MT5's swap / margin of every symbol, backtest history and the tracker" : s.CanTrade ? " • trading allowed by MT5 (ARM in the panel)" : " • read only — see step 4") : "Listening on 127.0.0.1:" + live.Port + " — waiting for the SpasiXBridge EA …";
                    setupStatus.Foreground = alive ? Green : live.Problem != "" ? Red : Cyan;
                    if (!alive && !s.Linked) { if (armed) SetArmed(false, "MT5 disconnected"); return; }
                    if (!everLinked && alive) { everLinked = true; if (view == "SETUP") Show("CHART"); }
                    if (Paper) { var la = live.State.Account; var vs = paper.Acct.Set; acLogin.Text = "DEMO"; acName.Text = ""; acRest.Text = "•  simulated " + vs.Kind + (vs.Kind != "RAW" ? " ($" + vs.CommissionPerLot.ToString("0.##", IC) + " / lot)" : " (+" + vs.MarkupPts.ToString("0", IC) + " pts spread)") + "  •  1:" + vs.Leverage + "  •  HEDGING  •  prices from " + la.Server + "  •  nothing reaches MT5"; acRest.Foreground = Orange; }
                    else { acLogin.Text = a.Login.ToString(IC); acName.Text = a.Name;
                    acRest.Text = "•  " + a.Server + "  •  " + a.Kind + "  •  " + (a.Mode == "HEDGE" ? "HEDGING" : "NETTING") + "  •  1:" + a.Leverage + (s.Company != "" ? "  •  " + s.Company : "");
                    acRest.Foreground = Muted; }
                    if (!Paper) RequestDeals(false);
                }
                Action<string, string, Brush> card = (k, v, fg) => { cards[k].Item2.Text = v; cards[k].Item2.Foreground = fg; };
                bool any = Sim ? sim.Spec != null : true;
                if (any)
                {
                    card("BALANCE", Money(a.Balance, cur), Text); card("EQUITY", Money(a.Equity, cur), a.Equity >= a.Balance ? Green : Red); PaintEq(a.Equity, a.Balance);
                    double fl = a.Floating; card("OPEN P/L", Signed(fl, cur), fl >= 0 ? (fl > 0 ? Green : Text) : Red); PaintPl(fl, s.Positions.Count > 0);
                    card("FREE MARGIN", Money(a.FreeMargin, cur), a.FreeMargin < 0 ? Red : Text);
                    double today = TodayClosed(s) + fl; card("TODAY", Signed(today, cur), today > 0 ? Green : today < 0 ? Red : Text); cards["TODAY"].Item3.BorderBrush = today > 0 ? Tint(Green, 100) : today < 0 ? Tint(Red, 120) : Line;
                    card("MARGIN LEVEL", a.Margin > 0 ? a.MarginLevel.ToString("N0", IC) + " %" : "—", a.Margin <= 0 ? Muted : a.MarginLevel < 200 ? Red : a.MarginLevel < 500 ? Gold : Green);
                    cards["MARGIN LEVEL"].Item3.ToolTip = "margin used " + Money(a.Margin, cur) + " • free margin " + Money(a.FreeMargin, cur) + (a.Margin > 0 ? " • margin level " + a.MarginLevel.ToString("N0", IC) + " %" : "");
                    double ratio = a.Balance > 0 ? Math.Max(0, Math.Min(1, a.Equity / a.Balance)) : 0; eqFill.Width = 108 * ratio; eqFill.Background = ratio >= 0.9 ? Green : ratio >= 0.6 ? Gold : Red;
                    cards["EQUITY"].Item3.ToolTip = "equity = " + (100 * (a.Balance > 0 ? a.Equity / a.Balance : 0)).ToString("0", IC) + " % of the balance";
                }
                ReadResults(s);
                if (Mode == "PROP") FillPropStrip(s);
                if (view == "CHART") FillChart(s); if (view == "ANALYSIS") FillAnalysis(s); if (view == "POSITIONS") FillPositions(s); if (view == "TRACKER") FillTracker(s); if (view == "PROP") FillProp(s); if (view == "POOL") FillPool();
                if (!Sim) { if (view == "MARKET") FillMarket(s); if (view == "NEWS") FillNews(); PaintNewsBadge(); }
                if (Sim) FillSim(s);
            }
        }
    }
}
