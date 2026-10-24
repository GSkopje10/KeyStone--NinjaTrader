// SpasiX MT5 — the window: header (connection + account cards), the views, the tab bar at the bottom.
// Read only so far: the CHART, POSITIONS and MARKET live from MetaTrader 5; trading and BACKTEST come next; SETUP explains the connection.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed class MainWindow : Window
    {
        public const string Build = "SpasiX MT5 • BUILD 2026-10-24b";
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static SolidColorBrush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
        static readonly SolidColorBrush Bg = B("#0A0E16"), Panel = B("#0F1522"), Card = B("#161E2E"), Line = B("#22304A"), Text = B("#E8ECF4"), Muted = B("#7D879A"),
            Gold = B("#FFC94D"), Green = B("#00E676"), Red = B("#FF3D71"), Cyan = B("#4DD8FF"), Violet = B("#B388FF");
        static Brush Tint(SolidColorBrush b, byte a) { var c = b.Color; var t = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); t.Freeze(); return t; }
        static string Money(double v, string cur) { return (v < 0 ? "−" : "") + (cur == "USD" ? "$" : "") + Math.Abs(v).ToString("N2", IC) + (cur == "USD" ? "" : " " + cur); }
        static string Signed(double v, string cur) { return (v > 0 ? "+" : "") + Money(v, cur); }

        readonly Mt5Bridge bridge; readonly DispatcherTimer ui;
        string view = "SETUP"; bool everLinked;
        readonly Dictionary<string, FrameworkElement> views = new Dictionary<string, FrameworkElement>();
        readonly List<Tuple<string, Border, TextBlock, Border>> tabs = new List<Tuple<string, Border, TextBlock, Border>>();
        // header
        TextBlock connText, acctText; Border connPill; readonly Dictionary<string, Tuple<TextBlock, TextBlock, Border>> cards = new Dictionary<string, Tuple<TextBlock, TextBlock, Border>>();
        Border eqFill; TextBlock eqText;
        // views
        WrapPanel marketPanel; readonly Dictionary<string, Tuple<Border, TextBlock, TextBlock, TextBlock, TextBlock>> marketCards = new Dictionary<string, Tuple<Border, TextBlock, TextBlock, TextBlock, TextBlock>>();
        StackPanel posPanel; TextBlock posSummary; StackPanel setupSteps; TextBlock setupStatus;
        string posSig = "";
        // the chart
        ChartView chart; WrapPanel symChips; readonly List<Tuple<int, Button>> tfBtns = new List<Tuple<int, Button>>(); TextBlock chartPx, chartInfo; string chartSym = ""; int chartTf = 60; string symSig = "";
        public static ImageSource Logo() { try { return BitmapFrame.Create(new Uri("pack://application:,,,/assets/spasix.png")); } catch { return null; } }
        // the app's own settings (%AppData%\SpasiX\MT5\settings.txt)
        static readonly string SetFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpasiX", "MT5", "settings.txt");
        readonly Dictionary<string, string> set = new Dictionary<string, string>();
        void LoadSet() { try { foreach (var l in File.ReadAllLines(SetFile)) { int i = l.IndexOf('='); if (i > 0) set[l.Substring(0, i)] = l.Substring(i + 1); } } catch { } }
        void SaveSet() { try { Directory.CreateDirectory(Path.GetDirectoryName(SetFile)); File.WriteAllLines(SetFile, set.Select(kv => kv.Key + "=" + kv.Value)); } catch { } }

        public MainWindow(Mt5Bridge b)
        {
            bridge = b;
            Title = "SpasiX MT5"; Width = 1420; Height = 880; MinWidth = 980; MinHeight = 600; Background = Bg; Foreground = Text;
            FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterScreen; WindowState = WindowState.Maximized;
            var lg = Logo(); if (lg != null) Icon = lg;
            LoadSet(); if (set.ContainsKey("sym")) chartSym = set["sym"]; int tf0; if (set.ContainsKey("tf") && int.TryParse(set["tf"], out tf0) && Tf.All.Contains(tf0)) chartTf = tf0;
            var root = new Grid(); Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var head = BuildHeader(); Grid.SetRow(head, 0); root.Children.Add(head);
            var host = new Grid { Margin = new Thickness(14, 6, 14, 6) }; Grid.SetRow(host, 1); root.Children.Add(host);
            views["POSITIONS"] = BuildPositions(); views["MARKET"] = BuildMarket(); views["CHART"] = BuildChart();
            views["BACKTEST"] = Soon("🧪 BACKTEST", "After the chart: MT5 history (candles and real ticks from IC Markets) replayed with lots, hedging, the real spread, IC Raw commission and swaps."); views["SETUP"] = BuildSetup();
            foreach (var v in views.Values) { v.Visibility = Visibility.Collapsed; host.Children.Add(v); }
            var bar = BuildTabBar(); Grid.SetRow(bar, 2); root.Children.Add(bar);
            Show("SETUP");
            bridge.Changed += () => { };
            ui = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) }; ui.Tick += delegate { Refresh(); }; ui.Start();
            Closed += delegate { ui.Stop(); bridge.Dispose(); };
        }

        // ---------------------------------------------------------------- header
        FrameworkElement BuildHeader()
        {
            var dp = new DockPanel { LastChildFill = true, Margin = new Thickness(14, 10, 14, 6) };
            var logo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
            var lgI = Logo(); if (lgI != null) logo.Children.Add(new Image { Source = lgI, Width = 40, Height = 40, Margin = new Thickness(0, 0, 10, 0) });
            logo.Children.Add(new TextBlock { Text = "SpasiX", Foreground = Gold, FontSize = 24, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            logo.Children.Add(new TextBlock { Text = " MT5", Foreground = Cyan, FontSize = 24, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(logo, Dock.Left); dp.Children.Add(logo);
            connText = new TextBlock { Text = "● WAITING FOR MT5", Foreground = Bg, FontSize = 12, FontWeight = FontWeights.Bold };
            connPill = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 4, 12, 4), Background = Gold, Child = connText, VerticalAlignment = VerticalAlignment.Center };
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; right.Children.Add(connPill);
            right.Children.Add(new TextBlock { Text = Build, Foreground = Muted, FontSize = 11, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(right, Dock.Right); dp.Children.Add(right);
            var mid = new StackPanel(); dp.Children.Add(mid);
            acctText = new TextBlock { Text = "start MetaTrader 5 with the SpasiXBridge EA on a chart (⚙ SETUP)", Foreground = Muted, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) }; mid.Children.Add(acctText);
            var row = new WrapPanel(); mid.Children.Add(row);
            foreach (var k in new[] { "BALANCE", "EQUITY", "OPEN P/L", "MARGIN LEVEL", "FREE MARGIN", "MARGIN USED" })
            {
                var cap = new TextBlock { Text = k, Foreground = Muted, FontSize = 10, FontWeight = FontWeights.Bold };
                var val = new TextBlock { Text = "—", Foreground = Text, FontSize = 19, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas") };
                var sp = new StackPanel(); sp.Children.Add(cap); sp.Children.Add(val);
                var bd = new Border { Background = Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 6, 14, 7), Margin = new Thickness(0, 0, 8, 4), MinWidth = 150, BorderBrush = Line, BorderThickness = new Thickness(1), Child = sp };
                row.Children.Add(bd); cards[k] = Tuple.Create(cap, val, bd);
            }
            // equity vs balance: how much of the balance is still there
            var eqHost = new Grid { Width = 230, Height = 46, Margin = new Thickness(4, 0, 0, 4), VerticalAlignment = VerticalAlignment.Center };
            var eqBack = new Border { CornerRadius = new CornerRadius(8), Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1) }; eqHost.Children.Add(eqBack);
            eqFill = new Border { CornerRadius = new CornerRadius(8), Background = Tint(Green, 90), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; eqHost.Children.Add(eqFill);
            eqText = new TextBlock { Text = "EQUITY / BALANCE —", Foreground = Text, FontSize = 11.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; eqHost.Children.Add(eqText);
            row.Children.Add(eqHost);
            return new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = dp };
        }

        // ---------------------------------------------------------------- tab bar
        FrameworkElement BuildTabBar()
        {
            var rowP = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var t in new[] { Tuple.Create("CHART", "📈  CHART"), Tuple.Create("POSITIONS", "💼  POSITIONS"), Tuple.Create("MARKET", "👁  MARKET"), Tuple.Create("BACKTEST", "🧪  BACKTEST"), Tuple.Create("SETUP", "⚙  SETUP") })
            {
                var key = t.Item1; var txt = new TextBlock { Text = t.Item2, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center };
                var line = new Border { Height = 3, CornerRadius = new CornerRadius(2), Background = Gold, Margin = new Thickness(6, 4, 6, 0), Opacity = 0 };
                var st = new StackPanel(); st.Children.Add(txt); st.Children.Add(line);
                var cell = new Border { Padding = new Thickness(20, 7, 20, 5), Margin = new Thickness(3, 0, 3, 0), CornerRadius = new CornerRadius(9), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Child = st };
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
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 640 };
            sp.Children.Add(new TextBlock { Text = title, Foreground = Gold, FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = "COMING NEXT", Foreground = Cyan, FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 10) });
            sp.Children.Add(new TextBlock { Text = what, Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            return sp;
        }

        // ---------------------------------------------------------------- SETUP: how to connect MT5
        FrameworkElement BuildSetup()
        {
            var sp = new StackPanel { MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            sp.Children.Add(new TextBlock { Text = "⚙ CONNECT METATRADER 5", Foreground = Gold, FontSize = 22, FontWeight = FontWeights.Bold });
            setupStatus = new TextBlock { Text = "", Foreground = Cyan, FontSize = 13.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 12), TextWrapping = TextWrapping.Wrap }; sp.Children.Add(setupStatus);
            setupSteps = new StackPanel(); sp.Children.Add(setupSteps);
            var steps = new[] {
                Tuple.Create("1", "Put the bridge in MT5", "MT5 ▸ File ▸ Open Data Folder ▸ MQL5 ▸ Experts → copy SpasiXBridge.mq5 there (next to the folders). Restart MT5: it compiles it and SpasiXBridge appears under Expert Advisors."),
                Tuple.Create("2", "Allow the local link", "MT5 ▸ Tools ▸ Options ▸ Expert Advisors: tick 'Allow WebRequest for listed URL' and add  http://127.0.0.1  (the link stays on this PC)."),
                Tuple.Create("3", "Start it", "Drag SpasiXBridge onto ONE chart → OK (leave Algo Trading off). The chart's corner says 'SpasiX: connected to the app' and this window turns green."),
                Tuple.Create("4", "Updating from the old KeystoneBridge", "Remove KeystoneBridge from its chart (right-click the chart ▸ Expert list ▸ Remove) and use SpasiXBridge instead — it also sends the candles for the 📈 CHART."),
                Tuple.Create("✓", "Read only for now", "This version only READS: the account, the prices, the candles, the positions and the orders. It contains no trading code — it cannot open, change or close anything.") };
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

        // ---------------------------------------------------------------- MARKET: every streamed symbol, bid / ask / spread + your exposure on it
        FrameworkElement BuildMarket()
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = "👁 MARKET • live from MT5 (the Market Watch symbols)", Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 0, 8) });
            marketPanel = new WrapPanel(); sp.Children.Add(marketPanel);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }

        // ---------------------------------------------------------------- POSITIONS: by symbol, buys and sells side by side (hedging), every ticket
        FrameworkElement BuildPositions()
        {
            var sp = new StackPanel();
            posSummary = new TextBlock { Text = "no positions", Foreground = Muted, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 0, 8), TextWrapping = TextWrapping.Wrap }; sp.Children.Add(posSummary);
            posPanel = new StackPanel(); sp.Children.Add(posPanel);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        static TextBlock Cell(string t, Brush fg, double w, bool right = false, bool bold = false) { return new TextBlock { Text = t, Foreground = fg, Width = w, FontSize = 12.5, FontFamily = new FontFamily("Consolas"), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextAlignment = right ? TextAlignment.Right : TextAlignment.Left, Margin = new Thickness(0, 0, 10, 0) }; }

        void FillPositions(Mt5State s)
        {
            var acc = s.Account; string cur = acc.Currency; var pos = s.Positions; var ords = s.Orders;
            double pl = pos.Sum(p => p.Profit + p.Swap);
            posSummary.Text = pos.Count == 0 && ords.Count == 0 ? "no open positions or orders" : pos.Count + " open ticket" + (pos.Count == 1 ? "" : "s") + " • " + pos.Where(p => p.Dir > 0).Sum(p => p.Lots).ToString("0.00", IC) + " lots bought • " + pos.Where(p => p.Dir < 0).Sum(p => p.Lots).ToString("0.00", IC) + " lots sold • open P/L " + Signed(pl, cur) + (ords.Count > 0 ? " • " + ords.Count + " pending order" + (ords.Count == 1 ? "" : "s") : "");
            posSummary.Foreground = pos.Count == 0 ? Muted : pl >= 0 ? Green : Red;
            // rebuild only when the tickets change; the numbers update every refresh
            string sig = string.Join(",", pos.Select(p => p.Ticket)) + "|" + string.Join(",", ords.Select(o => o.Ticket));
            if (sig != posSig)
            {
                posSig = sig; posPanel.Children.Clear();
                foreach (var g in pos.GroupBy(p => p.Symbol).OrderBy(x => x.Key))
                {
                    var card = new StackPanel(); var hdr = new TextBlock { Tag = "HDR:" + g.Key, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Text, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap }; card.Children.Add(hdr);
                    var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
                    foreach (var h in new[] { Tuple.Create("TICKET", 110.0), Tuple.Create("SIDE", 60.0), Tuple.Create("LOTS", 60.0), Tuple.Create("OPEN", 90.0), Tuple.Create("NOW", 90.0), Tuple.Create("S/L", 80.0), Tuple.Create("T/P", 80.0), Tuple.Create("SWAP", 70.0), Tuple.Create("P/L", 100.0), Tuple.Create("OPENED", 140.0) })
                        head.Children.Add(Cell(h.Item1, Muted, h.Item2, h.Item1 == "P/L" || h.Item1 == "SWAP"));
                    card.Children.Add(head);
                    foreach (var p in g.OrderBy(x => x.Time)) { var r = new StackPanel { Orientation = Orientation.Horizontal, Tag = "T:" + p.Ticket, Margin = new Thickness(0, 1, 0, 1) }; card.Children.Add(r); }
                    posPanel.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), BorderBrush = Line, BorderThickness = new Thickness(1), Child = card });
                }
                if (ords.Count > 0)
                {
                    var card = new StackPanel(); card.Children.Add(new TextBlock { Text = "⏳ PENDING ORDERS", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Cyan, Margin = new Thickness(0, 0, 0, 6) });
                    foreach (var o in ords)
                    {
                        var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) }; var sym = s.Symbols.ContainsKey(o.Symbol) ? s.Symbols[o.Symbol] : null; string f = "F" + (sym != null ? sym.Digits : 2);
                        r.Children.Add(Cell(o.Ticket.ToString(IC), Muted, 110)); r.Children.Add(Cell(o.Symbol, Text, 90, false, true)); r.Children.Add(Cell(o.Type, o.Type.StartsWith("BUY") ? Green : Red, 120, false, true));
                        r.Children.Add(Cell(o.Lots.ToString("0.##", IC) + " lots", Text, 90)); r.Children.Add(Cell("@ " + o.Price.ToString(f, IC), Text, 110)); r.Children.Add(Cell(o.Sl > 0 ? "S/L " + o.Sl.ToString(f, IC) : "", Red, 110)); r.Children.Add(Cell(o.Tp > 0 ? "T/P " + o.Tp.ToString(f, IC) : "", Green, 110));
                        card.Children.Add(r);
                    }
                    posPanel.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), BorderBrush = Tint(Cyan, 90), BorderThickness = new Thickness(1), Child = card });
                }
            }
            // the numbers
            var byTicket = pos.ToDictionary(p => p.Ticket);
            foreach (var cardB in posPanel.Children.OfType<Border>())
            {
                var card = cardB.Child as StackPanel; if (card == null) continue;
                foreach (var el in card.Children.OfType<FrameworkElement>())
                {
                    var tag = el.Tag as string; if (tag == null) continue;
                    if (tag.StartsWith("HDR:"))
                    {
                        string sy = tag.Substring(4); var ps = pos.Where(p => p.Symbol == sy).ToList(); double gpl = ps.Sum(p => p.Profit + p.Swap), bl = ps.Where(p => p.Dir > 0).Sum(p => p.Lots), sl = ps.Where(p => p.Dir < 0).Sum(p => p.Lots);
                        Mt5Quote q; s.Quotes.TryGetValue(sy, out q); var sym = s.Symbols.ContainsKey(sy) ? s.Symbols[sy] : null; string f = "F" + (sym != null ? sym.Digits : 2);
                        ((TextBlock)el).Text = sy + "   •   " + (bl > 0 ? "▲ " + bl.ToString("0.00", IC) + " lots bought (" + ps.Count(p => p.Dir > 0) + ")   " : "") + (sl > 0 ? "▼ " + sl.ToString("0.00", IC) + " lots sold (" + ps.Count(p => p.Dir < 0) + ")   " : "")
                            + "•   net " + (bl - sl >= 0 ? "+" : "") + (bl - sl).ToString("0.00", IC) + "   •   " + Signed(gpl, cur) + (q != null ? "   •   bid " + q.Bid.ToString(f, IC) + " / ask " + q.Ask.ToString(f, IC) : "");
                        ((TextBlock)el).Foreground = gpl >= 0 ? Green : Red; continue;
                    }
                    if (!tag.StartsWith("T:")) continue; long tk; if (!long.TryParse(tag.Substring(2), out tk)) continue; Mt5Position p0; if (!byTicket.TryGetValue(tk, out p0)) continue;
                    var row = (StackPanel)el; var symI = s.Symbols.ContainsKey(p0.Symbol) ? s.Symbols[p0.Symbol] : null; string fm = "F" + (symI != null ? symI.Digits : 2); double net = p0.Profit + p0.Swap;
                    row.Children.Clear();
                    row.Children.Add(Cell(p0.Ticket.ToString(IC), Muted, 110)); row.Children.Add(Cell(p0.Side, p0.Dir > 0 ? Green : Red, 60, false, true)); row.Children.Add(Cell(p0.Lots.ToString("0.00", IC), Text, 60));
                    row.Children.Add(Cell(p0.Open.ToString(fm, IC), Text, 90)); row.Children.Add(Cell(p0.Current.ToString(fm, IC), Cyan, 90)); row.Children.Add(Cell(p0.Sl > 0 ? p0.Sl.ToString(fm, IC) : "—", p0.Sl > 0 ? Red : Muted, 80)); row.Children.Add(Cell(p0.Tp > 0 ? p0.Tp.ToString(fm, IC) : "—", p0.Tp > 0 ? Green : Muted, 80));
                    row.Children.Add(Cell(Signed(p0.Swap, cur), p0.Swap >= 0 ? Muted : Red, 70, true)); row.Children.Add(Cell(Signed(net, cur), net >= 0 ? Green : Red, 100, true, true)); row.Children.Add(Cell(p0.Time == DateTime.MinValue ? "" : p0.Time.ToString("MM-dd HH:mm:ss", IC), Muted, 140));
                }
            }
        }

        void FillMarket(Mt5State s)
        {
            foreach (var q in s.Quotes.Values.OrderBy(x => x.Symbol))
            {
                Tuple<Border, TextBlock, TextBlock, TextBlock, TextBlock> c;
                if (!marketCards.TryGetValue(q.Symbol, out c))
                {
                    var name = new TextBlock { Text = q.Symbol, Foreground = Gold, FontSize = 16, FontWeight = FontWeights.Bold };
                    var px = new TextBlock { Foreground = Text, FontSize = 20, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 2, 0, 0) };
                    var spr = new TextBlock { Foreground = Muted, FontSize = 11.5 }; var expo = new TextBlock { Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
                    var sp = new StackPanel(); sp.Children.Add(name); sp.Children.Add(px); sp.Children.Add(spr); sp.Children.Add(expo);
                    var bd = new Border { Width = 250, Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 10, 10), BorderBrush = Line, BorderThickness = new Thickness(1), Child = sp };
                    var symQ = q.Symbol; bd.Cursor = System.Windows.Input.Cursors.Hand; bd.ToolTip = "open the 📈 CHART of " + symQ; bd.MouseLeftButtonUp += delegate { SetChart(symQ, chartTf); Show("CHART"); };
                    marketPanel.Children.Add(bd); c = Tuple.Create(bd, name, px, spr, expo); marketCards[q.Symbol] = c;
                }
                var sym = s.Symbols.ContainsKey(q.Symbol) ? s.Symbols[q.Symbol] : null; string f = "F" + (sym != null ? sym.Digits : 2);
                c.Item3.Text = q.Bid.ToString(f, IC) + "  /  " + q.Ask.ToString(f, IC);
                c.Item4.Text = "spread " + q.Spread(sym != null ? sym.Point : 0).ToString("0", IC) + " points" + (sym != null ? " • lots " + sym.VolMin.ToString("0.##", IC) + "–" + sym.VolMax.ToString("0.##", IC) + " step " + sym.VolStep.ToString("0.##", IC) : "") + " • " + q.Time.ToString("HH:mm:ss", IC) + " server";
                var ps = s.Positions.Where(p => p.Symbol == q.Symbol).ToList();
                if (ps.Count == 0) { c.Item5.Text = "no position"; c.Item5.Foreground = Muted; c.Item1.BorderBrush = Line; }
                else { double gpl = ps.Sum(p => p.Profit + p.Swap); c.Item5.Text = ps.Count + " ticket" + (ps.Count == 1 ? "" : "s") + " • net " + (ps.Sum(p => p.Dir * p.Lots) >= 0 ? "+" : "") + ps.Sum(p => p.Dir * p.Lots).ToString("0.##", IC) + " lots • " + Signed(gpl, s.Account.Currency); c.Item5.Foreground = gpl >= 0 ? Green : Red; c.Item1.BorderBrush = gpl >= 0 ? Tint(Green, 140) : Tint(Red, 140); }
            }
        }

        // ---------------------------------------------------------------- 📈 CHART: symbol chips, timeframes, the live price, the chart
        FrameworkElement BuildChart()
        {
            var g = new Grid(); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(right, Dock.Right);
            chartPx = new TextBlock { Foreground = Text, FontSize = 18, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 12, 0) };
            var reset = Pill("⟲ AUTO", Card); reset.ToolTip = "back to the latest candles, automatic price scale (or double-click the chart)"; reset.Click += delegate { if (chart != null) chart.ResetView(); };
            right.Children.Add(chartPx); right.Children.Add(reset); bar.Children.Add(right);
            var left = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            symChips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }; left.Children.Add(symChips);
            left.Children.Add(new Border { Width = 1, Height = 24, Background = Line, Margin = new Thickness(6, 0, 10, 0) });
            foreach (var m in Tf.All) { int mm = m; var b = Pill(Tf.Name(m), Card); b.MinWidth = 44; b.Click += delegate { SetChart(chartSym, mm); }; left.Children.Add(b); tfBtns.Add(Tuple.Create(m, b)); }
            chartInfo = new TextBlock { Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) }; left.Children.Add(chartInfo);
            bar.Children.Add(left); Grid.SetRow(bar, 0); g.Children.Add(bar);
            chart = new ChartView(); var host = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Line, BorderThickness = new Thickness(1), Child = chart }; Grid.SetRow(host, 1); g.Children.Add(host);
            PaintTf(); return g;
        }
        Button Pill(string t, Brush bg)
        {
            var b = new Button { Content = t, Foreground = Text, Background = bg, BorderBrush = Line, BorderThickness = new Thickness(1), Height = 30, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Bold, FontSize = 12, Cursor = System.Windows.Input.Cursors.Hand };
            var tpl = new ControlTemplate(typeof(Button)); var bd = new FrameworkElementFactory(typeof(Border)); bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent }); bd.SetValue(Border.BorderThicknessProperty, new Thickness(1)); bd.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); bd.AppendChild(cp); tpl.VisualTree = bd;
            b.Template = tpl; b.MouseEnter += delegate { b.BorderBrush = Gold; }; b.MouseLeave += delegate { b.BorderBrush = Line; }; return b;
        }
        void PaintTf() { foreach (var t in tfBtns) { bool on = t.Item1 == chartTf; t.Item2.Background = on ? Tint(Gold, 200) : Card; t.Item2.Foreground = on ? Bg : Text; } }
        void SetChart(string sym, int tf)
        {
            if (string.IsNullOrEmpty(sym)) return; bool same = string.Equals(sym, chartSym, StringComparison.OrdinalIgnoreCase) && tf == chartTf;
            chartSym = sym; chartTf = tf; set["sym"] = sym; set["tf"] = tf.ToString(IC); SaveSet(); PaintTf(); symSig = ""; if (!same && chart != null) chart.ResetView();
        }
        void FillChart(Mt5State s)
        {
            if (chartSym == "" || !s.Quotes.ContainsKey(chartSym))
            {
                var first = s.Positions.Select(p => p.Symbol).FirstOrDefault() ?? s.Quotes.Keys.FirstOrDefault(k => k.StartsWith("XAU", StringComparison.OrdinalIgnoreCase)) ?? s.Quotes.Keys.OrderBy(k => k).FirstOrDefault();
                if (first == null) return; if (chartSym == "" || s.Quotes.Count > 0) SetChart(first, chartTf);
            }
            // the symbol chips: the ones with tickets first, then Market Watch
            var syms = s.Positions.Select(p => p.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).Concat(s.Quotes.Keys.OrderBy(k => k)).Distinct(StringComparer.OrdinalIgnoreCase).Take(14).ToList();
            string sig = string.Join(",", syms) + "|" + chartSym;
            if (sig != symSig)
            {
                symSig = sig; symChips.Children.Clear();
                foreach (var sy in syms) { var s0 = sy; bool on = string.Equals(sy, chartSym, StringComparison.OrdinalIgnoreCase); var b = Pill(sy, on ? (Brush)Tint(Cyan, 70) : Card); if (on) b.BorderBrush = Cyan; b.Click += delegate { SetChart(s0, chartTf); }; symChips.Children.Add(b); }
            }
            var se = s.SeriesOf(chartSym, chartTf); var now = DateTime.Now;
            if (s.BridgeVersion < 2) chart.Status = "the chart needs the new bridge — put SpasiXBridge.mq5 in MT5 (⚙ SETUP, step 4)";
            else if (!se.Loaded) { chart.Status = "loading " + chartSym + " " + Tf.Name(chartTf) + " from MT5 …"; if ((now - se.AskedAt).TotalSeconds > 3) { se.AskedAt = now; bridge.Send("BARS|" + chartSym + "|" + chartTf + "|2000"); } }
            else if ((now - se.AskedAt).TotalSeconds > 20) { se.AskedAt = now; bridge.Send("BARS|" + chartSym + "|" + chartTf + "|3"); }   // MT5's own last candles now and then
            Mt5Quote q; s.Quotes.TryGetValue(chartSym, out q); var sym = s.Symbols.ContainsKey(chartSym) ? s.Symbols[chartSym] : null;
            chart.Series = se.Loaded ? se : null; chart.Symbol = chartSym; chart.Digits = sym != null ? sym.Digits : 2; chart.Point = sym != null && sym.Point > 0 ? sym.Point : 0.01;
            chart.Bid = q != null ? q.Bid : 0; chart.Ask = q != null ? q.Ask : 0; chart.Positions = s.Positions; chart.Orders = s.Orders; chart.Currency = s.Account.Currency; chart.Gate = s.Gate;
            string f = "F" + chart.Digits; chartPx.Text = q != null ? q.Bid.ToString(f, IC) + " / " + q.Ask.ToString(f, IC) : "";
            chartInfo.Text = (sym != null ? sym.Description + " • " : "") + (q != null ? "spread " + q.Spread(chart.Point).ToString("0", IC) + " • " + q.Time.ToString("HH:mm:ss", IC) + " server" : "") + (se.Loaded ? " • " + se.Bars.Count + " candles" : "");
            chart.InvalidateVisual();
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
                setupStatus.Text = bridge.Problem != "" ? "⚠ " + bridge.Problem : alive ? "✓ CONNECTED to " + a.Server + " • account " + a.Login + " (" + a.Name + ") • " + a.Kind + " • " + s.Quotes.Count + " symbols streaming" : "Listening on 127.0.0.1:" + bridge.Port + " — waiting for the SpasiXBridge EA …";
                setupStatus.Foreground = alive ? Green : bridge.Problem != "" ? Red : Cyan;
                if (!alive && !s.Linked) return;
                if (!everLinked && alive) { everLinked = true; Show("CHART"); }
                acctText.Text = a.Login + " • " + a.Name + " • " + a.Server + " • " + a.Kind + " • " + (a.Mode == "HEDGE" ? "HEDGING" : "NETTING") + " • 1:" + a.Leverage + (s.Company != "" ? " • " + s.Company : "");
                acctText.Foreground = Text;
                Action<string, string, Brush> card = (k, v, fg) => { cards[k].Item2.Text = v; cards[k].Item2.Foreground = fg; };
                card("BALANCE", Money(a.Balance, cur), Text); card("EQUITY", Money(a.Equity, cur), a.Equity >= a.Balance ? Green : Text);
                double fl = a.Floating; card("OPEN P/L", Signed(fl, cur), fl >= 0 ? Green : Red); cards["OPEN P/L"].Item3.BorderBrush = fl >= 0 ? Tint(Green, 120) : Tint(Red, 160);
                card("MARGIN LEVEL", a.Margin > 0 ? a.MarginLevel.ToString("N0", IC) + " %" : "—", a.Margin <= 0 ? Muted : a.MarginLevel < 200 ? Red : a.MarginLevel < 500 ? Gold : Green);
                card("FREE MARGIN", Money(a.FreeMargin, cur), Text); card("MARGIN USED", Money(a.Margin, cur), Muted);
                double ratio = a.Balance > 0 ? Math.Max(0, Math.Min(1, a.Equity / a.Balance)) : 0; eqFill.Width = 230 * ratio; eqFill.Background = ratio >= 0.9 ? Tint(Green, 90) : ratio >= 0.6 ? Tint(Gold, 110) : Tint(Red, 130);
                eqText.Text = "EQUITY = " + (100 * (a.Balance > 0 ? a.Equity / a.Balance : 0)).ToString("0", IC) + " % OF THE BALANCE";
                if (view == "POSITIONS") FillPositions(s); if (view == "MARKET") FillMarket(s); if (view == "CHART") FillChart(s);
            }
        }
    }

    public static class Program
    {
        static Window Splash()
        {
            Func<string, SolidColorBrush> br = h => (SolidColorBrush)new BrushConverter().ConvertFromString(h);
            var w = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, Width = 440, Height = 300, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true, Topmost = true, Title = "SpasiX MT5" };
            var lg = MainWindow.Logo(); if (lg != null) w.Icon = lg;
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var ring = new System.Windows.Shapes.Ellipse { Width = 132, Height = 132, StrokeThickness = 4, Stroke = new LinearGradientBrush(br("#FFC94D").Color, br("#4DD8FF").Color, 45), StrokeDashArray = new DoubleCollection { 6, 3 } };
            var rot = new RotateTransform(0, 66, 66); ring.RenderTransform = rot; rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });
            var logoG = new Grid { Width = 132, Height = 132, HorizontalAlignment = HorizontalAlignment.Center }; logoG.Children.Add(ring);
            if (lg != null) logoG.Children.Add(new Image { Source = lg, Width = 92, Height = 92, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(logoG);
            var name = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 2) };
            name.Children.Add(new TextBlock { Text = "SpasiX", Foreground = br("#FFC94D"), FontSize = 30, FontWeight = FontWeights.Bold }); name.Children.Add(new TextBlock { Text = " MT5", Foreground = br("#4DD8FF"), FontSize = 30, FontWeight = FontWeights.Bold });
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
            app.DispatcherUnhandledException += (s, e) => { MessageBox.Show(e.Exception.Message, "SpasiX MT5"); e.Handled = true; };
            // the branded loading screen first, then the platform (full screen)
            var splash = Splash(); splash.Show(); var t0 = DateTime.Now;
            var main = new MainWindow(bridge); app.MainWindow = main;
            var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(50, 1400 - (DateTime.Now - t0).TotalMilliseconds)) };
            tm.Tick += delegate { tm.Stop(); main.Show(); main.Activate(); splash.Close(); };
            tm.Start(); app.Run();
        }
    }
}
