// SpasiX — the 📈 CHART tab: 1 / 2 / 4 charts (each its own symbol and timeframe; the toolbar works on the selected one), the toolbar (symbols + add,
// timeframes, split, indicators, theme, time zone, reset / full screen / panel), the DRAWING RAIL on the left, the right-click menus (the chart, a ticket,
// a stop / target, an order, a drawing), the REPLAY BAR in a backtest, the TRADE PANEL (ARM, lots, BUY / SELL, limit / stop at a price picked on the
// chart, close all / buys / sells / profit / loss, breakeven, cancel orders, close everything) and the trading commands (only armed, only through the broker).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        sealed class Pane { public string DrawSig = ""; public ChartView V; public string Sym = ""; public int Tf = 60; public Border Host; public TextBlock Head; }
        readonly List<Pane> panes = new List<Pane>(); int activePane; int split = 1; Grid chartsGrid;
        WrapPanel symChips; readonly List<Tuple<int, Button>> tfBtns = new List<Tuple<int, Button>>(); Button indBtn, themeBtn, zoneBtn;
        readonly Dictionary<int, TextBlock> tfCountdown = new Dictionary<int, TextBlock>();
        readonly List<Tuple<int, Button>> splitBtns = new List<Tuple<int, Button>>(); Button fullBtn, panelBtn; string symSig = ""; int zoneIdx, themeIdx;
        readonly Dictionary<string, List<Drawing>> drawingsBySym = new Dictionary<string, List<Drawing>>(StringComparer.OrdinalIgnoreCase);
        readonly List<Tuple<string, Button>> toolBtns = new List<Tuple<string, Button>>(); Button magnetBtn, colorBtn; string drawColor = "#FFC94D"; bool magnet;
        static readonly string[] DrawColors = { "#FFC94D", "#4DD8FF", "#A98BFF", "#2EE59D", "#FF4D6D", "#FF9F43", "#E879F9", "#E8ECF4" };
        static readonly Tuple<string, string>[] Zones = { Tuple.Create("SERVER (MT5)", ""), Tuple.Create("NEW YORK", "Eastern Standard Time"), Tuple.Create("LONDON", "GMT Standard Time"), Tuple.Create("CENTRAL EUROPE", "Central Europe Standard Time"),
            Tuple.Create("FRANKFURT", "W. Europe Standard Time"), Tuple.Create("TOKYO", "Tokyo Standard Time"), Tuple.Create("SYDNEY", "AUS Eastern Standard Time"), Tuple.Create("UTC", "UTC"), Tuple.Create("MY PC", "LOCAL") };
        // trade panel
        bool armed; Button armBtn, buyBtn, sellBtn, placeBtn; TextBlock armText, panelSym, panelExpo, toastText, riskText, spreadText; TextBox lotsBox, slBox, tpBox, pendPrice, beOff; bool oneClickOn; string pendKind = "BUY LIMIT";
        readonly List<Tuple<string, Button>> pendBtns = new List<Tuple<string, Button>>(); StackPanel logPanel, armChecks; Border panelHost, toastBox, armCard;
        int resSeen, notesSeen; readonly Dictionary<string, string> sent = new Dictionary<string, string>();
        Tuple<Border, TextBlock, TextBlock> symBuy, symSell, symHedge;
        string ticketMode = "AUTO"; Border symPlBox; TextBlock symPlCap, symPlVal, symPlInfo; int symPlSign = 9;

        Pane Act { get { return panes[Math.Max(0, Math.Min(panes.Count - 1, activePane))]; } }

        FrameworkElement BuildChartTab()
        {
            var g = new Grid(); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            zoneIdx = Math.Max(0, Math.Min(Zones.Length - 1, IntOf(Get("zone", "1"), 1))); LoadMyThemes(); themeIdx = Math.Max(0, Math.Min(ChartTheme.All.Length + myThemes.Count - 1, IntOf(Get("theme", "0"), 0)));
            { var tn = Get("themename", ""); if (tn != "") { int ix = ThemeIndexOf(tn); themeIdx = ix >= 0 ? ix : 0; } } LoadCandles();   // saved by name (new themes shift the numbers)
            showGrid = Get("grid", "0") == "1"; lookUi = Get("lookui", "1") == "1"; Sx.ApplyLook(lookUi ? CurTheme() : ChartTheme.All[0]);
            drawColor = Get("drawcolor", "#FFC94D"); drawFill = Get("drawfill", ""); magnet = Get("magnet", "0") == "1"; ticketMode = Get("tickets", "AUTO"); oneClickOn = Get("oneclick", "0") == "1";
            // ---- toolbar: symbols (cyan) • timeframes (gold) • split • indicators • theme • zone • reset / full / panel
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) }; Grid.SetRow(bar, 0); Grid.SetColumnSpan(bar, 3); g.Children.Add(bar);
            symChips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }; bar.Children.Add(symChips);
            if (!Sim) { var addSym = Pill("+", Tint(Cyan, 60)); addSym.ToolTip = "add an instrument (every symbol of your broker)"; addSym.Click += delegate { PickSymbol(); }; bar.Children.Add(addSym); }
            // the spread of the chart's instrument, live (points, like MT5's popup prices), next to the instruments: green = normal, gold = wider than usual, red = very wide
            spreadCells = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var spCap = new TextBlock { Text = "SPREAD", Foreground = Muted, FontSize = 9, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), LineHeight = 11 };
            var spRow = new StackPanel { Orientation = Orientation.Horizontal }; spRow.Children.Add(spCap); spRow.Children.Add(spreadCells);
            spreadStrip = new Border { Child = spRow, Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 8, 3), Margin = new Thickness(2, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "the spread now (ask − bid, in points) of the chart's instrument • green = at its usual, gold = 1.5× its usual, red = 2.5× its usual (wait before entering) • SPREADS opens the history" };
            bar.Children.Add(spreadStrip);

            bar.Children.Add(Sep());
            foreach (var m in Tf.All)
            {
                int mm = m; var nameT = new TextBlock { Text = Tf.Name(m), FontSize = 11.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center }; var cdT = new TextBlock { FontSize = 8.5, FontFamily = Sx.Mono, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.85 };
                var st0 = new StackPanel(); st0.Children.Add(nameT); st0.Children.Add(cdT);
                var b = Pill("", Card); b.Content = st0; b.MinWidth = 46; b.Height = 34; b.Padding = new Thickness(6, 0, 6, 0); b.BorderBrush = Tint(Gold, 90); b.ToolTip = "the " + Tf.Name(m) + " candle closes in …"; b.Click += delegate { SetPane(Act, Act.Sym, mm); }; bar.Children.Add(b); tfBtns.Add(Tuple.Create(m, b)); tfCountdown[m] = cdT;
            }
            bar.Children.Add(Sep());
            foreach (var sp in new[] { Tuple.Create(1, "▣ 1"), Tuple.Create(2, "◫ 2"), Tuple.Create(4, "⊞ 4") }) { int n = sp.Item1; var b = Pill(sp.Item2, Card); b.Padding = new Thickness(9, 0, 9, 0); b.ToolTip = n == 1 ? "one chart" : n + " charts side by side (each its own symbol / timeframe — click one to select it)"; b.Click += delegate { SetSplit(n); }; bar.Children.Add(b); splitBtns.Add(Tuple.Create(n, b)); }
            bar.Children.Add(Sep());
            indBtn = Sx.Drop("📈 INDICATORS", Card, () => IndMenu(Act.V)); indBtn.ToolTip = "tick the indicators (each has its colour)"; bar.Children.Add(indBtn);
            themeBtn = Sx.Drop("🎨 " + CurTheme().Name, Card, ThemeMenu); bar.Children.Add(themeBtn);
            zoneBtn = Sx.Drop("🕘 " + Zones[zoneIdx].Item1, Card, ZoneMenu); zoneBtn.ToolTip = "the time on the chart (MT5 keeps its server time inside)"; bar.Children.Add(zoneBtn);
            var cleanBtn = Pill("🧹 CLEAN", Card); cleanBtn.ToolTip = "clean chart: every drawing and indicator of this instrument off at once (right-click ▸ ↶ Undo the clean brings them back)"; cleanBtn.Click += delegate { CleanChart(Act); }; bar.Children.Add(cleanBtn);
            bar.Children.Add(LinesBtn(0));
            bar.Children.Add(Sep());
            var spb = Pill("📶 SPREADS", Card); spb.ToolTip = "every instrument's spread, live (F10)"; spb.Click += delegate { ToggleSpreads(); }; bar.Children.Add(spb);
            var more = Pill("⏪ MORE", Card); more.ToolTip = "load 30 more days of history on this chart (30 days are loaded by default)"; more.Click += delegate { MoreHistory(Act); }; bar.Children.Add(more);
            var reset = Pill("⟲ NOW", Card); reset.ToolTip = "back to the latest candles, automatic scale (or double-click the chart)"; reset.Click += delegate { Act.V.ResetView(); }; bar.Children.Add(reset);
            fullBtn = Pill("⛶ FULL", Card); fullBtn.ToolTip = "the chart over the whole screen (F11 / Esc)"; fullBtn.Click += delegate { SetFullChart(!fullChart); }; bar.Children.Add(fullBtn);
            panelBtn = Pill("◧ PANEL", Tint(Gold, 60)); panelBtn.ToolTip = "show / hide the trade panel"; panelBtn.Click += delegate { panelHost.Visibility = panelHost.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; Put("panel", panelHost.Visibility == Visibility.Visible ? "1" : "0"); }; bar.Children.Add(panelBtn);
            // the selected instrument's own P/L (its tickets only): buys / sells, lots, P/L — the border pulses green / red
            symPlCap = new TextBlock { Foreground = Cyan, FontSize = 9.5, FontWeight = FontWeights.Bold }; symPlVal = new TextBlock { Foreground = Muted, FontSize = 15, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono };
            Func<SolidColorBrush, Tuple<Border, TextBlock, TextBlock>> cell = col =>
            {
                var c1 = new TextBlock { Foreground = col, FontSize = 9.5, FontWeight = FontWeights.Bold }; var c2 = new TextBlock { Foreground = Text, FontSize = 12.5, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono };
                var st = new StackPanel(); st.Children.Add(c1); st.Children.Add(c2);
                return Tuple.Create(new Border { Child = st, Background = Tint(col, 30), BorderBrush = Tint(col, 120), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 9, 2), Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed }, c1, c2);
            };
            symBuy = cell(Green); symSell = cell(Red); symHedge = cell(Violet);
            symPlInfo = new TextBlock { Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var spl = new StackPanel(); spl.Children.Add(symPlCap); spl.Children.Add(symPlVal); var sph = new StackPanel { Orientation = Orientation.Horizontal }; sph.Children.Add(spl); sph.Children.Add(symBuy.Item1); sph.Children.Add(symSell.Item1); sph.Children.Add(symHedge.Item1); sph.Children.Add(symPlInfo);
            symPlBox = new Border { Child = sph, Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1.8), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 2, 12, 3), Margin = new Thickness(4, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, MinWidth = 260, ToolTip = "the P/L of this instrument only (its open tickets)" };
            bar.Children.Add(symPlBox);
            bar.Children.Add(CloseBtns(34));
            // ---- the replay bar (backtest)
            if (Sim) { var rb = BuildReplayBar(); Grid.SetRow(rb, 1); Grid.SetColumnSpan(rb, 3); g.Children.Add(rb); }
            // ---- the drawing rail
            var rail = BuildRail(); Grid.SetRow(rail, 2); Grid.SetColumn(rail, 0); g.Children.Add(rail);
            chartBar = bar; chartRail = rail; chartRoot = g; fullStrip = BuildFullStrip(); Grid.SetRow(fullStrip, 0); Grid.SetColumnSpan(fullStrip, 3); fullStrip.Visibility = Visibility.Collapsed; g.Children.Add(fullStrip);
            // ---- the charts
            chartsGrid = new Grid(); Grid.SetRow(chartsGrid, 2); Grid.SetColumn(chartsGrid, 1); g.Children.Add(chartsGrid);
            for (int k = 0; k < 4; k++) panes.Add(MakePane(k));
            string pre = Sim ? "bt." : "";
            for (int k = 0; k < 4; k++) { int tf0; panes[k].Sym = Sim ? "" : Get("sym" + k, k == 0 ? Get("sym", "") : ""); panes[k].Tf = int.TryParse(Get(pre + "tf" + k, k == 0 ? "5" : k == 1 ? "15" : k == 2 ? "1" : "60"), out tf0) && Tf.All.Contains(tf0) ? tf0 : 60; }
            // ---- the trade panel
            panelHost = BuildTradePanel(); Grid.SetRow(panelHost, 2); Grid.SetColumn(panelHost, 2); g.Children.Add(panelHost); if (Get("panel", "1") == "0") panelHost.Visibility = Visibility.Collapsed;
            // ---- the toast (answers from MT5)
            toastText = new TextBlock { Foreground = Text, FontSize = 13, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            toastBox = new Border { Child = toastText, Background = Tint(Panel, 245), BorderBrush = Gold, BorderThickness = new Thickness(1.4), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 8, 14, 9), Margin = new Thickness(0, 0, 0, 18), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed, MaxWidth = 720, IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Gold.Color, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.4 } };
            Grid.SetRow(toastBox, 2); Grid.SetColumn(toastBox, 1); g.Children.Add(toastBox);
            spreadPanel = BuildSpreadPanel(); Grid.SetRow(spreadPanel, 2); Grid.SetColumn(spreadPanel, 1); g.Children.Add(spreadPanel); if (Get("spreads", "0") != "1") spreadPanel.Visibility = Visibility.Collapsed;
            if (Get("v6.cleanind", "") == "") { foreach (var t in ChartView.Indicators) foreach (var pf in new[] { "", "bt.", "demo." }) Put(pf + "ind." + t.Key, "0"); Put("v6.cleanind", "1"); }   // once: the old default indicators off
            foreach (var p in panes) { p.V.Theme = CurTheme(); p.V.ShowGrid = showGrid; p.V.DrawColor = drawColor; p.V.DrawFill = drawFill; p.V.Magnet = magnet; p.V.TicketMode = ticketMode; p.V.Sim = Sim; LoadInd(p); }
            SetSplit(Sim ? 1 : IntOf(Get(pre + "split", "1"), 1));   // the backtest opens on ONE chart (the symbols = tabs); ⊞ splits PaintBar(); PaintRail();
            return g;
        }
        // 📊 the gap pool: every daily and weekly opening gap of the loaded history (MT5's D1 / W1 candles)
        void GapStatsDialog(Pane p)
        {
            if (p.Sym == "") return; List<Mt5Bar> d1, w1; double pt; int dg;
            lock (broker.State.Gate)
            {
                var s1 = broker.State.SeriesOf(p.Sym, 1440); var s2 = broker.State.SeriesOf(p.Sym, 10080); d1 = s1.Bars.ToList(); w1 = s2.Bars.ToList();
                Mt5Symbol sy; broker.State.Symbols.TryGetValue(p.Sym, out sy); pt = sy != null && sy.Point > 0 ? sy.Point : 0.01; dg = sy != null ? sy.Digits : 2;
                if (!Sim && (d1.Count < 300 || w1.Count < 100)) { broker.Send("BARS|" + p.Sym + "|1440|1500"); broker.Send("BARS|" + p.Sym + "|10080|400"); }
            }
            if (!Sim && d1.Count < 300) { Toast("📊 loading " + p.Sym + "'s daily and weekly history from MT5 … open the statistics again in a few seconds"); if (d1.Count < 20) return; }
            var txt = ChartView.GapStats(d1, pt, dg, "DAILY GAPS") + "\n\n" + ChartView.GapStats(w1, pt, dg, "WEEKLY GAPS") + "\n\na gap = the open of a day (week) minus the last close • filled = the price came back to that close in the same day (week) • tiny gaps under 3 points are left out";
            var box = new TextBox { Text = txt, IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 12.5, Background = Bg, Foreground = Text, BorderBrush = Line, Padding = new Thickness(10), Margin = new Thickness(0, 10, 0, 0), MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, MinWidth = 640 };
            Sx.Form(this, "📊 Gaps • " + p.Sym, Sim ? "from the backtest's loaded candles" : "from MT5's daily and weekly candles", box, "OK", null, Amber);
        }

        // ---- 📶 SPREADS (F10): every instrument's spread now, in points and in price, with today's low / average (seen here), coloured
        Border spreadPanel; StackPanel spreadRows; readonly Dictionary<string, List<int>> spreadSeen = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase); DateTime spreadAt = DateTime.MinValue;
        Border BuildSpreadPanel()
        {
            var sp = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) }; var x = Pill("✕", Brushes.Transparent); x.BorderBrush = Brushes.Transparent; x.Height = 22; x.Width = 24; x.Padding = new Thickness(0); x.Margin = new Thickness(0); x.Click += delegate { ToggleSpreads(); }; DockPanel.SetDock(x, Dock.Right); head.Children.Add(x);
            head.Children.Add(new TextBlock { Text = "📶 SPREADS • live", Foreground = Cyan, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }); sp.Children.Add(head);
            spreadRows = new StackPanel(); sp.Children.Add(spreadRows);
            sp.Children.Add(new TextBlock { Text = "points • price • low / avg since SpasiX started • F10", Foreground = Muted, FontSize = 9.5, Margin = new Thickness(0, 4, 0, 0) });
            var tt = new TranslateTransform(); var b = new Border { Child = sp, Background = Tint(Panel, 245), BorderBrush = Tint(Cyan, 140), BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 6, 10, 8), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 34, 96, 0), MinWidth = 250, RenderTransform = tt, Cursor = Cursors.SizeAll,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Cyan.Color, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.35 } };
            Point st0 = new Point(); bool dragging = false;
            b.MouseLeftButtonDown += (o, e) => { if (e.OriginalSource is TextBlock && ((TextBlock)e.OriginalSource).Text == "✕") return; dragging = true; st0 = e.GetPosition(this); st0.X -= tt.X; st0.Y -= tt.Y; b.CaptureMouse(); e.Handled = true; };
            b.MouseMove += (o, e) => { if (!dragging) return; var p = e.GetPosition(this); tt.X = p.X - st0.X; tt.Y = p.Y - st0.Y; };
            b.MouseLeftButtonUp += (o, e) => { if (dragging) { dragging = false; b.ReleaseMouseCapture(); } };
            return b;
        }
        void ToggleSpreads() { bool on = spreadPanel.Visibility != Visibility.Visible; spreadPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed; Put("spreads", on ? "1" : "0"); if (on) spreadAt = DateTime.MinValue; }
        // the instrument chips: their order (dragged) and the removed ones, saved
        string symDragSig = "";
        List<string> SymList(string key) { return Get(key, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
        void MoveSym(string from, string to, bool after)
        {
            var l = listSyms.ToList(); l.RemoveAll(x => string.Equals(x, from, StringComparison.OrdinalIgnoreCase)); int ix = l.FindIndex(x => string.Equals(x, to, StringComparison.OrdinalIgnoreCase)); if (ix < 0) return;
            l.Insert(after ? ix + 1 : ix, from); Put("symorder", string.Join(",", l)); symDragSig = string.Join(",", l); symSig = "";
        }
        void HideSym(string sym, bool hasPos)
        {
            if (hasPos && !Sx.Ask(this, "Remove " + sym, sym + " has open tickets. They stay open in MT5 (POSITIONS still shows them). Remove " + sym + " from the instrument list?", "REMOVE", "KEEP", Gold)) return;
            var h = SymList("symhide"); if (!h.Contains(sym, StringComparer.OrdinalIgnoreCase)) h.Add(sym); Put("symhide", string.Join(",", h));
            var w = SymList("watch"); w.RemoveAll(x => string.Equals(x, sym, StringComparison.OrdinalIgnoreCase)); Put("watch", string.Join(",", w));
            symDragSig = "hide:" + string.Join(",", h); symSig = ""; Toast("✕ " + sym + " removed from the list • + adds it back");
        }
        DateTime prefetchAt = DateTime.MinValue; readonly Dictionary<string, Tuple<TextBlock, Button>> chipLabs = new Dictionary<string, Tuple<TextBlock, Button>>(StringComparer.OrdinalIgnoreCase); readonly Dictionary<string, int> prefetchTries = new Dictionary<string, int>();
        Border spreadStrip; StackPanel spreadCells; string spreadStripSig = ""; List<string> listSyms = new List<string>();
        void FillSpreadStrip(Mt5State s)
        {
            if (spreadCells == null) return; var syms = new List<string> { Act.Sym }.Where(x => x != "" && s.Quotes.ContainsKey(x)).ToList();
            var cells = new List<Tuple<string, int, SolidColorBrush>>();
            foreach (var sym in syms)
            {
                var q = s.Quotes[sym]; Mt5Symbol sy; s.Symbols.TryGetValue(sym, out sy); double pt = sy != null && sy.Point > 0 ? sy.Point : 0.01; if (q.Bid <= 0 || q.Ask <= 0) continue;
                int sp = (int)Math.Round(q.Spread(pt)); List<int> l; double usual = sp; if (spreadSeen.TryGetValue(sym, out l) && l.Count >= 20) { var last = l.Skip(Math.Max(0, l.Count - 600)).OrderBy(x => x).ToList(); usual = last[last.Count / 2]; }   // its median of the last 10 minutes (a sample a second)
                var col = usual <= 0 ? Green : sp >= usual * 2.5 ? Red : sp >= usual * 1.5 ? Gold : Green; cells.Add(Tuple.Create(sym, sp, col));
            }
            string sig = string.Join(",", cells.Select(c => c.Item1 + ":" + c.Item2 + ":" + (c.Item3 == Red ? "r" : c.Item3 == Gold ? "g" : "n"))) + "|" + Act.Sym; if (sig == spreadStripSig) return; spreadStripSig = sig;
            spreadCells.Children.Clear(); spreadStrip.Visibility = cells.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            foreach (var c in cells)
            {
                bool on = string.Equals(c.Item1, Act.Sym, StringComparison.OrdinalIgnoreCase); var sp0 = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                sp0.Children.Add(new TextBlock { Text = c.Item1, Foreground = on ? Text : Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
                sp0.Children.Add(new TextBlock { Text = c.Item2.ToString("N0", IC), Foreground = c.Item3, FontSize = 13, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono, HorizontalAlignment = HorizontalAlignment.Center });
                spreadCells.Children.Add(new Border { Child = sp0, Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(0, 0, 4, 0), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), BorderBrush = on ? Tint(Cyan, 160) : Brushes.Transparent, Background = c.Item3 == Red ? Tint(Red, 40) : c.Item3 == Gold ? Tint(Gold, 30) : Brushes.Transparent });
            }
        }
        void FillSpreads(Mt5State s)
        {
            var now = DateTime.Now; bool sample = (now - spreadAt).TotalSeconds >= 1; if (!sample && spreadPanel.Visibility != Visibility.Visible) return;
            var rows = new List<Tuple<string, int, double, int, double, int>>();
            foreach (var q in s.Quotes.Values.OrderBy(x => x.Symbol))
            {
                Mt5Symbol sy; s.Symbols.TryGetValue(q.Symbol, out sy); double pt = sy != null && sy.Point > 0 ? sy.Point : 0.01; int d = sy != null ? sy.Digits : 2; int sp0 = (int)Math.Round(q.Spread(pt));
                List<int> l; if (!spreadSeen.TryGetValue(q.Symbol, out l)) spreadSeen[q.Symbol] = l = new List<int>(); if (sample && q.Bid > 0) { l.Add(sp0); if (l.Count > 30000) l.RemoveRange(0, 10000); }
                rows.Add(Tuple.Create(q.Symbol, sp0, sp0 * pt, l.Count > 0 ? l.Min() : sp0, l.Count > 0 ? l.Average() : sp0, d));
            }
            if (sample) spreadAt = now; if (spreadPanel.Visibility != Visibility.Visible) return;
            spreadRows.Children.Clear();
            foreach (var r in rows)
            {
                var g = new Grid { Margin = new Thickness(0, 1, 0, 1) }; foreach (var w in new[] { 82.0, 52, 74, 80 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
                double ratio = r.Item5 > 0 ? r.Item2 / r.Item5 : 1; var col = ratio > 2.2 ? Red : ratio > 1.4 ? Gold : Green;
                Action<string, int, Brush, bool> c = (t, i, fg, right) => { var tb = new TextBlock { Text = t, Foreground = fg, FontSize = 12, FontFamily = Sx.Mono, FontWeight = i <= 1 ? FontWeights.Bold : FontWeights.Normal, TextAlignment = right ? TextAlignment.Right : TextAlignment.Left }; Grid.SetColumn(tb, i); g.Children.Add(tb); };
                c(r.Item1, 0, r.Item1 == Act.Sym ? Gold : Text, false); c(r.Item2.ToString(IC), 1, col, true); c(r.Item3.ToString("F" + r.Item6, IC), 2, Muted, true); c(r.Item4 + " / " + r.Item5.ToString("0", IC), 3, Muted, true);
                g.ToolTip = r.Item1 + ": spread " + r.Item2 + " points = " + r.Item3.ToString("F" + r.Item6, IC) + " in price • lowest seen " + r.Item4 + " • average " + r.Item5.ToString("0.0", IC) + (ratio > 2.2 ? " • WIDE now (rollover / news / weekend)" : "");
                spreadRows.Children.Add(g);
            }
        }

        // the history on the chart: 30 days on every timeframe (at least 300 candles); ⏪ MORE HISTORY adds 30 days each time
        readonly Dictionary<string, int> moreDays = new Dictionary<string, int>();
        int BarsWanted(string sym, int tf) { int days; moreDays.TryGetValue(sym + "|" + tf, out days); days += 30; return Math.Min(100000, Math.Max(300, days * 1440 / Math.Max(1, tf) + 50)); }
        void MoreHistory(Pane p)
        {
            if (Sim) { Toast("the backtest shows the loaded days — 📅 GO TO changes them"); return; }
            string k = p.Sym + "|" + p.Tf; int d; moreDays.TryGetValue(k, out d); moreDays[k] = d + 30; lock (broker.State.Gate) { var se = broker.State.SeriesOf(p.Sym, p.Tf); se.AskedAt = DateTime.Now; }
            broker.Send("BARS|" + p.Sym + "|" + p.Tf + "|" + BarsWanted(p.Sym, p.Tf)); Toast("⏪ loading " + (d + 60) + " days of " + p.Sym + " " + Tf.Name(p.Tf) + " from MT5 …");
        }
        static int IntOf(string s, int d) { int v; return int.TryParse(s, NumberStyles.Integer, IC, out v) ? v : d; }
        FrameworkElement Sep() { return new Border { Width = 1, Height = 24, Background = Line, Margin = new Thickness(4, 3, 10, 3), VerticalAlignment = VerticalAlignment.Center }; }

        // ---- the menus of the toolbar
        List<Mi> IndMenu(ChartView v)
        {
            var l = new List<Mi> { Mi.H("indicators of " + (Act.Sym == "" ? "this chart" : Act.Sym) + " • tick several, click outside to close") };
            foreach (var t in ChartView.Indicators)
            {
                var key = t.Key; if (key == "SESS_SYD") { l.Add(Mi.S()); l.Add(Mi.H("market sessions • official hours, summer / winter time automatic")); } else if (key == "VOL") { l.Add(Mi.S()); l.Add(Mi.H("volume & order flow (estimated from MT5 tick volume)")); } else if (key == "SETUPS") { l.Add(Mi.S()); l.Add(Mi.H("signals")); }
                l.Add(new Mi { Text = t.Name, Hint = t.Key == "ORB" ? "under H1" : t.Key.StartsWith("SESS") ? "H1 and under" : "", Check = v.Ind.Contains(key), Dot = Sx.B(t.Color), Do = () => ToggleInd(key), KeepOpen = true });
                if (key == "FVG") { int fs = v.FvgSide; l.Add(new Mi { Text = "      FVG: bullish + bearish", Check = fs == 0, Radio = true, Do = () => SetFvgSide(0) }); l.Add(new Mi { Text = "      FVG: bullish only", Check = fs == 1, Radio = true, Fg = Green, Do = () => SetFvgSide(1) }); l.Add(new Mi { Text = "      FVG: bearish only", Check = fs == -1, Radio = true, Fg = Red, Do = () => SetFvgSide(-1) }); }
            }
            l.Add(new Mi("📊", "Gap statistics (daily / weekly) …", () => GapStatsDialog(Act)));
            l.Add(Mi.S()); l.Add(new Mi("🌐", "All sessions on", () => { foreach (var t in ChartView.Indicators.Where(x => x.Key.StartsWith("SESS"))) SetInd(Act.Sym, t.Key, true); PaintBar(); }));
            l.Add(Mi.S()); l.Add(new Mi("⊘", "Turn every indicator of " + Act.Sym + " off", () => { foreach (var t in ChartView.Indicators) SetInd(Act.Sym, t.Key, false); PaintBar(); }));
            return l;
        }
        // ---- the chart's look: the templates by group, my own saved charts, ⚙ the chart settings (every colour, like MT5's F8)
        readonly List<ChartTheme> myThemes = new List<ChartTheme>();
        void LoadMyThemes() { myThemes.Clear(); foreach (var t in Get("mythemes", "").Split('|')) { var th = ChartTheme.FromText(t); if (th != null) myThemes.Add(th); } }
        void SaveMyThemes() { Put("mythemes", string.Join("|", myThemes.Select(x => x.ToText()))); }
        ChartTheme ThemeAt(int i) { return i >= 0 && i < ChartTheme.All.Length ? ChartTheme.All[i] : i - ChartTheme.All.Length >= 0 && i - ChartTheme.All.Length < myThemes.Count ? myThemes[i - ChartTheme.All.Length] : ChartTheme.All[0]; }
        ChartTheme CurTheme() { return WithCandles(ThemeAt(themeIdx)); }
        // 🕯 the candle colours of a look, chosen per look (saved by the look's name): the look keeps its background, only the candles change
        static readonly Tuple<string, string, string, string, string, string>[] CandleSets = {   // key, name, up / down on dark, up / down on light
            Tuple.Create("", "the look's own", "", "", "", ""),
            Tuple.Create("MT5", "MT5 green / red", "#00C000", "#E00000", "#008A00", "#C00000"),
            Tuple.Create("JEWEL", "emerald / ruby", "#19C37D", "#E5484D", "#0E8A5A", "#B8323A"),
            Tuple.Create("TEAL", "teal / coral", "#26A69A", "#EF5350", "#0B7D72", "#C8372F"),
            Tuple.Create("GOLDV", "gold / violet", "#FFC94D", "#8E6BD8", "#A87400", "#5A3FB0"),
            Tuple.Create("GOLDS", "gold / silver", "#E8B84A", "#9AA3AE", "#9A6A00", "#5E6670"),
            Tuple.Create("NEON", "cyan / magenta", "#00E5FF", "#FF2E97", "#0088A8", "#C0156E"),
            Tuple.Create("ICE", "ice / fire", "#8FD3FF", "#FF6A3D", "#2A78B8", "#C2410C"),
            Tuple.Create("LIME", "lime / pink", "#B6FF3B", "#FF3BD4", "#4E8A00", "#B0158E"),
            Tuple.Create("MONO", "white / grey", "#F2F2F2", "#5C5F66", "#1B1B1B", "#9AA0A8"),
            Tuple.Create("BARS", "hollow bars (MT5 classic)", "#00FF00", "#00FF00", "#000000", "#000000") };
        Dictionary<string, string> candleOv = new Dictionary<string, string>(), candleSnap;
        ChartTheme candleCacheSrc, candleCacheOut; string candleCacheKey;
        // 🎯 the entry lines (buys / T/P, sells / S/L, pending orders) of a look: AUTO picks the set that stands apart from the candles AND the background, so every object is easy to spot
        static readonly string[][] LineSets = {   // key, name, buy / sell / pending on dark, the same on light
            new[] { "AUTO", "auto contrast", "", "", "", "", "", "" },
            new[] { "BLUEOR", "blue / orange", "#3D9BFF", "#FF8A1F", "#C77DFF", "#1C64D6", "#D9610A", "#7B3FC4" },
            new[] { "CYANMAG", "cyan / magenta", "#22D3EE", "#FF3DCB", "#FFC94D", "#0A84A8", "#B5157E", "#A86E00" },
            new[] { "SKYGOLD", "sky / gold", "#7CC4FF", "#FFC94D", "#FF6BD5", "#2563EB", "#A87400", "#B5157E" },
            new[] { "LIMEVIO", "lime / violet", "#A3FF12", "#B07CFF", "#FFB020", "#4E8A00", "#6A3FD6", "#B87400" },
            new[] { "WHITERED", "white / red", "#F2F5FA", "#FF3B3B", "#FFC94D", "#111418", "#D01F1F", "#A86E00" },
            new[] { "CLASSIC", "mint / coral (classic)", "#2EE59D", "#FF4D6D", "#FFB020", "#0F8A5F", "#D23048", "#B87400" } };
        static double Dist(Color a, Color b) { double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B; return Math.Sqrt(2 * dr * dr + 4 * dg * dg + 3 * db * db); }
        static string[] AutoLines(ChartTheme t, bool light)
        {
            string[] best = null; double bestScore = -1;
            foreach (var ls in LineSets.Skip(1))
            {
                var c3 = new[] { Sx.C(light ? ls[5] : ls[2]), Sx.C(light ? ls[6] : ls[3]) }; double sc = double.MaxValue;
                foreach (var c in c3) { sc = Math.Min(sc, Math.Min(Dist(c, t.Up), Dist(c, t.Down))); sc = Math.Min(sc, Dist(c, t.Plot) * 0.6); }
                if (sc > bestScore) { bestScore = sc; best = ls; }
            }
            return best;
        }
        ChartTheme WithCandles(ChartTheme t)
        {
            if (t == null) return t; string key, lk; candleOv.TryGetValue(t.Name, out key); candleOv.TryGetValue("L:" + t.Name, out lk); key = key ?? ""; lk = string.IsNullOrEmpty(lk) ? "AUTO" : lk;
            bool ownLines = t.Buy != ChartTheme.DefBuy || t.Sell != ChartTheme.DefSell || t.Pend != ChartTheme.DefPend;   // set in the chart settings: kept
            if (key == "" && ownLines) return t;
            if (ReferenceEquals(t, candleCacheSrc) && key + "|" + lk == candleCacheKey) return candleCacheOut;
            bool light = t.Light || Sx.Luma(t.Bg) > 0.55; var c = t.Copy();
            var cs = CandleSets.FirstOrDefault(x => x.Item1 == key);
            if (key != "" && cs != null)
            {
                var up = Sx.C(light ? cs.Item5 : cs.Item3); var dn = Sx.C(light ? cs.Item6 : cs.Item4);
                c.Up = up; c.Down = dn; c.UpWick = up; c.DownWick = dn; c.HollowUp = false; c.Bodies = false;
                if (key == "BARS") { c.Bodies = true; c.UpBody = t.Plot; c.DownBody = light ? Sx.C("#000000") : Sx.C("#FFFFFF"); c.Edge = up; }
            }
            if (!ownLines)
            {
                var ls = lk == "AUTO" ? AutoLines(c, light) : LineSets.FirstOrDefault(x => x[0] == lk) ?? AutoLines(c, light);
                c.Buy = Sx.C(light ? ls[5] : ls[2]); c.Sell = Sx.C(light ? ls[6] : ls[3]); c.Pend = Sx.C(light ? ls[7] : ls[4]);
            }
            candleCacheSrc = t; candleCacheKey = key + "|" + lk; candleCacheOut = c; return c;
        }
        void LoadCandles() { candleOv = new Dictionary<string, string>(); foreach (var kv in Get("candles", "").Split('|')) { int i = kv.LastIndexOf('='); if (i > 0) candleOv[kv.Substring(0, i)] = kv.Substring(i + 1); } }
        void SaveCandles() { Put("candles", string.Join("|", candleOv.Where(x => x.Value != "").Select(x => x.Key + "=" + x.Value))); }
        List<Mi> ThemeMenu()
        {
            var l = new List<Mi> { new Mi("✨", "Looks … (preview the whole platform in every theme)", () => LooksGallery()), new Mi("⚙", "Chart settings … (colours, candles, grid, entries)", () => ChartSettings()),
                new Mi { Text = "Grid squares in the background", Check = showGrid, KeepOpen = true, Do = () => SetGrid(!showGrid) }, new Mi { Text = "The whole interface follows the theme", Check = lookUi, KeepOpen = true, Do = () => SetLookUi(!lookUi) }, Mi.S() };
            if (myThemes.Count > 0) { var ml = new List<Mi> { Mi.H("saved from the chart settings") }; for (int k = 0; k < myThemes.Count; k++) { int ii = ChartTheme.All.Length + k; ml.Add(new Mi { Text = myThemes[k].Name, Dot = new SolidColorBrush(myThemes[k].Plot), Check = themeIdx == ii, Radio = true, KeepOpen = true, Do = () => SetTheme(ii) }); } l.Add(Mi.Group("★", "My charts", ml)); }
            foreach (var grp in ChartTheme.All.Select(x => x.Group).Distinct())
            {
                var gl = new List<Mi> { Mi.H(grp) }; bool cur = false;
                for (int i = 0; i < ChartTheme.All.Length; i++) { if (ChartTheme.All[i].Group != grp) continue; int ii = i; var th = ChartTheme.All[i]; if (themeIdx == i) cur = true; gl.Add(new Mi { Text = th.Name, Dot = new SolidColorBrush(th.Plot), Check = themeIdx == i, Radio = true, KeepOpen = true, Do = () => SetTheme(ii) }); }
                l.Add(Mi.Group(grp == "♛ Your picks" ? "♛" : grp == "Ⓜ MT5 Luxe" ? "Ⓜ" : grp == "✦ Neon" ? "✦" : grp == "☕ Paper & ink" ? "☕" : grp == "✧ Luxe colour" ? "✧" : grp == "★ Signature" ? "★" : grp == "Gray & light" ? "◐" : grp == "◐ Soft light" ? "◒" : grp == "Cool contrasts" ? "◑" : grp == "MetaTrader" ? "Ⓜ" : grp == "Easy on the eyes" ? "☾" : grp == "Unique" ? "✦" : "S", grp + (cur ? "  ✓" : ""), gl));
            }
            return l;
        }
        // a few candles + a buy / sell / pending line in a theme's colours (the template cards and the settings' preview)
        static void MiniChart(Canvas cv, ChartTheme th, int n, double step, double bw, double scale)
        {
            cv.Children.Clear(); var up = th.Bodies ? th.UpBody : th.Up; var dn = th.Bodies ? th.DownBody : th.Down; bool edge = th.Bodies && th.Edge.A > 0;
            if (scale > 1 && !th.NoGrid) for (int g = 1; g < 4; g++) { var gl = new Border { Width = n * step + 20, Height = 1, Background = new SolidColorBrush(th.Grid), Opacity = 0.9 }; Canvas.SetLeft(gl, 0); Canvas.SetTop(gl, g * 22 * scale / 1.6); cv.Children.Add(gl); }
            for (int k = 0; k < n; k++)
            {
                bool u = k % 3 != 1; double top = 10 + (k * 7 % 13), h = 14 + (k * 5 % 9); double x = 12 + k * step;
                if (scale > 1) { u = Math.Cos(k * 0.42) > -0.15 ? k % 4 != 2 : k % 4 == 1; h = 10 + (k * 7 % 17); top = 54 - 30 * Math.Sin(k * 0.42) - h / 2; }
                var w0 = new Border { Width = Math.Max(1, th.WickW) * (scale > 1 ? 1.2 : 1), Height = h + 10 * scale, Background = new SolidColorBrush(u ? th.UpWick : th.DownWick) }; Canvas.SetLeft(w0, x + bw / 2 - 0.6); Canvas.SetTop(w0, top - 5 * scale); cv.Children.Add(w0);
                bool hollow = u && th.HollowUp; var r0 = new Border { Width = bw * th.BodyW / 0.7, Height = h, Background = new SolidColorBrush(hollow ? th.Plot : u ? up : dn), BorderBrush = hollow ? new SolidColorBrush(th.Up) : edge ? new SolidColorBrush(th.Edge) : null, BorderThickness = new Thickness(hollow || edge ? 1 : 0) };
                Canvas.SetLeft(r0, x + bw / 2 - r0.Width / 2); Canvas.SetTop(r0, top); cv.Children.Add(r0);
            }
            if (scale <= 1) return;
            Action<Color, double, string> line = (c, y, t) => { var l0 = new Border { Width = n * step + 10, Height = 1.6, Background = new SolidColorBrush(c) }; Canvas.SetLeft(l0, 4); Canvas.SetTop(l0, y); cv.Children.Add(l0); var tb = new TextBlock { Text = t, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(c) }; Canvas.SetLeft(tb, n * step + 18); Canvas.SetTop(tb, y - 7); cv.Children.Add(tb); };
            line(th.Buy, 22, "BUY 0.10"); line(th.Price, 48, "BID"); line(th.Pend, 74, "BUY LIMIT"); line(th.Sell, 96, "S/L");
            var ax = new TextBlock { Text = "4003.35   12:35", FontSize = 10, Foreground = new SolidColorBrush(th.Axis) }; Canvas.SetLeft(ax, 12); Canvas.SetTop(ax, 104); cv.Children.Add(ax);
        }
        void ChartSettings()
        {
            var before = CurTheme(); var t = before.Editable(); int beforeIdx = themeIdx;
            var root = new StackPanel { MinWidth = 640 };
            var pv = new Canvas { Height = 122, ClipToBounds = true }; var pvBox = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.6), Padding = new Thickness(0, 4, 0, 0), Margin = new Thickness(0, 0, 0, 10), Child = pv }; root.Children.Add(pvBox);
            Action paintPv = () => { pvBox.Background = new SolidColorBrush(t.Plot); pvBox.BorderBrush = new SolidColorBrush(t.Bg.A > 0 && t.Bg != t.Plot ? t.Bg : t.Accent); MiniChart(pv, t, 22, 22, 12, 1.6); };
            Action live = () => { foreach (var p in panes) { p.V.Theme = t; p.V.InvalidateVisual(); } paintPv(); };
            paintPv();
            var tabsRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) }; root.Children.Add(tabsRow);
            var body = new Grid(); root.Children.Add(body);
            var pages = new List<Tuple<Button, FrameworkElement>>(); int curPage = 0;
            Action<int> showPage = k => { for (int q = 0; q < pages.Count; q++) { pages[q].Item2.Visibility = q == k ? Visibility.Visible : Visibility.Collapsed; pages[q].Item1.Background = q == k ? Tint(Gold, 70) : Card; } };
            Func<string, StackPanel> page = name => { var sp = new StackPanel(); var b = Pill(name, Card); int k = pages.Count; b.Click += delegate { curPage = k; showPage(k); }; tabsRow.Children.Add(b); pages.Add(Tuple.Create(b, (FrameworkElement)sp)); body.Children.Add(sp); return sp; };
            var palette = new[] { "#000000", "#0B0F15", "#131722", "#1E222D", "#2A2E39", "#363A45", "#4A4E59", "#5D606B", "#787B86", "#9598A1", "#B2B5BE", "#D1D4DC", "#E0E3EB", "#F0F3FA", "#FFFFFF",
                "#F23645", "#FF4D6D", "#FF7A59", "#FF9800", "#FFB020", "#FFC94D", "#FFEB3B", "#CDDC39", "#8BC34A", "#00FF00", "#2EE59D", "#089981", "#26A69A", "#00BCD4", "#4DD8FF",
                "#2962FF", "#3F51B5", "#673AB7", "#7A5CFF", "#A98BFF", "#9C27B0", "#E879F9", "#E91E63", "#795548", "#5C4033", "#0F1B33", "#0D3B3E", "#2A1638", "#2B211C", "#F1EEE6" };
            Func<StackPanel, string, string, FrameworkElement> colorRow = (host, label, key) =>
            {
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
                var line = new DockPanel(); row.Children.Add(line);
                var sw = new Border { Width = 34, Height = 22, CornerRadius = new CornerRadius(5), BorderBrush = Line, BorderThickness = new Thickness(1), Background = new SolidColorBrush(t.Get(key)), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 8, 0) };
                var hex = Sx.Input(t.Get(key).A == 0 ? "none" : t.Get(key).A == 255 ? "#" + t.Get(key).ToString().Substring(3) : t.Get(key).ToString(), 104); hex.Height = 24; hex.FontSize = 12;
                var lb = Sx.Label(label, 12.5, Text); DockPanel.SetDock(hex, Dock.Right); DockPanel.SetDock(sw, Dock.Right); line.Children.Add(hex); line.Children.Add(sw); line.Children.Add(lb);
                var pal = new WrapPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 2), MaxWidth = 600 }; row.Children.Add(pal);
                Action<Color> apply = c => { t.Set(key, c); sw.Background = new SolidColorBrush(c); live(); };
                foreach (var h in palette) { var hh = h; var b0 = new Border { Width = 22, Height = 18, Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(3), Background = Sx.B(h), BorderBrush = Line, BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = h }; b0.MouseLeftButtonUp += delegate { apply(Sx.C(hh)); hex.Text = hh; }; pal.Children.Add(b0); }
                if (key == "Edge") { var none = new Border { Height = 18, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(3), Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand, Child = Sx.Label("no outline", 10.5, Muted) }; none.MouseLeftButtonUp += delegate { apply(Colors.Transparent); hex.Text = "none"; }; pal.Children.Add(none); }
                sw.MouseLeftButtonUp += delegate { pal.Visibility = pal.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; };
                hex.TextChanged += delegate { try { var x = hex.Text.Trim(); if (x.Length == 7 || x.Length == 9) apply(Sx.C(x.StartsWith("#") ? x : "#" + x)); } catch { } };
                host.Children.Add(row); return row;
            };
            Func<StackPanel, string, Func<bool>, Action<bool>, CheckBox> tick = (host, label, get, set) => { var c = new CheckBox { Content = label, IsChecked = get(), Foreground = Text, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 6) }; c.Click += delegate { set(c.IsChecked == true); live(); }; host.Children.Add(c); return c; };
            Func<StackPanel, string, double, double, Func<double>, Action<double>, Slider> slide = (host, label, min, max, get, set) =>
            {
                var d = new DockPanel { Margin = new Thickness(0, 2, 0, 6) }; var sl = new Slider { Minimum = min, Maximum = max, Value = get(), Width = 240, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(sl, Dock.Right); d.Children.Add(sl); d.Children.Add(Sx.Label(label, 12.5, Text));
                sl.ValueChanged += delegate { set(sl.Value); live(); }; host.Children.Add(d); return sl;
            };
            Action build = null;
            build = () =>
            {
            tabsRow.Children.Clear(); body.Children.Clear(); pages.Clear();
            // 5 start from a template
            var ps = page("🎨 TEMPLATES");
            ps.Children.Add(Sx.Label("pick a template, then change any colour in the other tabs", 12, Muted));
            var tw = new WrapPanel { Margin = new Thickness(0, 8, 0, 0), MaxWidth = 640 }; var twScroll = new ScrollViewer { Content = tw, MaxHeight = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; ps.Children.Add(twScroll);
            foreach (var th0 in ChartTheme.All.Concat(myThemes))
            {
                var th1 = th0; var card = new Border { Width = 118, Height = 58, Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(th1.Plot), BorderBrush = new SolidColorBrush(th1.Grid), BorderThickness = new Thickness(1.4), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = th1.Group + " • " + th1.Name };
                var cv = new Canvas(); card.Child = cv; MiniChart(cv, th1, 6, 15, 8, 1);
                var nm = new TextBlock { Text = th1.Name, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(th1.Text), Margin = new Thickness(6, 0, 0, 3), VerticalAlignment = VerticalAlignment.Bottom }; Canvas.SetLeft(nm, 4); Canvas.SetTop(nm, 40); cv.Children.Add(nm);
                card.MouseLeftButtonUp += delegate { var e = th1.Editable(); foreach (var f in typeof(ChartTheme).GetFields()) if (!f.IsStatic && !f.IsLiteral) f.SetValue(t, f.GetValue(e)); live(); Toast("🎨 " + th1.Name + " — now change any colour"); curPage = 1; build(); };
                tw.Children.Add(card);
            }
            // 1 the candles
            var pc = page("🕯 CANDLES");
            colorRow(pc, "Bull candle (body)", "UpBody"); colorRow(pc, "Bear candle (body)", "DownBody"); colorRow(pc, "Bull wick", "UpWick"); colorRow(pc, "Bear wick", "DownWick"); colorRow(pc, "Candle outline (\"no outline\" in the colours)", "Edge");
            tick(pc, "Hollow bull candles (outline only)", () => t.HollowUp, v => t.HollowUp = v); tick(pc, "Glow around the candles", () => t.Glow, v => t.Glow = v);
            slide(pc, "Candle width", 0.3, 0.95, () => t.BodyW, v => t.BodyW = v); slide(pc, "Wick thickness", 1, 4, () => t.WickW, v => t.WickW = Math.Round(v * 2) / 2);
            // 2 the background
            var pb = page("🖼 BACKGROUND");
            colorRow(pb, "Window around the chart", "Bg"); colorRow(pb, "Chart area", "Plot"); colorRow(pb, "Chart frame", "Accent"); colorRow(pb, "Grid", "Grid");
            tick(pb, "Show the grid (squares in the background)", () => showGrid, v => SetGrid(v)); tick(pb, "Dotted grid (like MT5)", () => t.Dotted, v => t.Dotted = v); tick(pb, "Light background (dark labels on the tickets)", () => t.Light, v => t.Light = v);
            // 3 the text + the price
            var px = page("🔤 TEXT & PRICE");
            colorRow(px, "Price / time scale", "Axis"); colorRow(px, "Text", "Text"); colorRow(px, "Bid / ask lines + the price tag", "Price");
            // 4 the trades
            var pt = page("🎯 ENTRIES & ORDERS");
            colorRow(pt, "Buy entries, targets in profit, ▲", "Buy"); colorRow(pt, "Sell entries, stops, ▼", "Sell"); colorRow(pt, "Pending orders (limit / stop)", "Pend");
            showPage(curPage);
            };
            build();
            var nameBox = Sx.Input(before.Group == "Mine" ? before.Name : "My chart " + (myThemes.Count + 1), 220); nameBox.FontSize = 12.5;
            var nameRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(nameBox, Dock.Right); nameRow.Children.Add(nameBox); nameRow.Children.Add(Sx.Label("save as (★ My charts in the 🎨 menu)", 12.5, Muted)); root.Children.Add(nameRow);
            bool ok = Sx.Form(this, "⚙ Chart settings", "every colour of the chart, like MT5 (F8) • the chart changes as you pick • SAVE keeps it in ★ My charts", root, "SAVE", "CANCEL", Gold);
            if (!ok) { themeIdx = beforeIdx; foreach (var p in panes) { p.V.Theme = before; p.V.InvalidateVisual(); } return; }
            t.Group = "Mine"; t.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "My chart" : nameBox.Text.Trim().Replace("|", "/").Replace(";", ",");
            int at = myThemes.FindIndex(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase)); if (at >= 0) myThemes[at] = t; else { myThemes.Add(t); at = myThemes.Count - 1; }
            SaveMyThemes(); SetTheme(ChartTheme.All.Length + at); Toast("🎨 saved: " + t.Name);
        }
        List<Mi> ZoneMenu() { var l = new List<Mi> { Mi.H("the time on the chart") }; for (int i = 0; i < Zones.Length; i++) { int ii = i; l.Add(new Mi { Text = Zones[i].Item1, Check = zoneIdx == i, Radio = true, Do = () => SetZone(ii) }); } return l; }
        void SetTheme(int i) { SetTheme(i, true); }
        void SetTheme(int i, bool save)
        {
            themeIdx = i; if (save) { EndLookPreview(); Put("theme", i.ToString(IC)); Put("themename", CurTheme().Name); }
            foreach (var p in panes) { p.V.Theme = CurTheme(); p.V.InvalidateVisual(); } themeBtn.Content = "🎨 " + CurTheme().Name + "  ▾";
            Sx.ApplyLook(lookUi ? CurTheme() : ChartTheme.All[0]); symSig = ""; spreadStripSig = ""; PaintBar();   // the whole interface follows (chips repaint with the new colours)
        }
        bool showGrid, lookUi = true;
        int ThemeIndexOf(string name) { for (int k = 0; k < ChartTheme.All.Length; k++) if (ChartTheme.All[k].Name == name) return k; int m = myThemes.FindIndex(x => x.Name == name); return m >= 0 ? ChartTheme.All.Length + m : -1; }
        void SetGrid(bool on) { showGrid = on; Put("grid", on ? "1" : "0"); foreach (var p in panes) { p.V.ShowGrid = on; p.V.InvalidateVisual(); } }
        void SetLookUi(bool on) { lookUi = on; Put("lookui", on ? "1" : "0"); SetTheme(themeIdx, false); }
        // ✨ THE LOOKS GALLERY: every theme as a small picture of the whole platform; click one = the whole app takes it (pointing does nothing, user's request), SAVE keeps it
        static string LookGroup(ChartTheme t) { return (t.Group == "MetaTrader" ? "Ⓜ MT5" : t.Group == "Easy on the eyes" ? "☾ Easy on the eyes" : t.Group).ToUpperInvariant(); }
        // 👁 PREVIEW: the gallery folds into a small tab over the platform, so the whole interface is seen in the look; the tab opens the gallery again, ◀ ▶ step through the looks, CONFIRM keeps it and the tab goes
        int lookBefore = -1; Border lookTab; TextBlock lookTabName; string lookFilter = "ALL"; List<int> lookList = new List<int>();
        void CommitLook(int k) { EndLookPreview(); candleSnap = null; SaveCandles(); candleCacheSrc = null; SetTheme(k, true); Toast("🎨 " + CurTheme().Name); }
        void RevertLook(int k) { EndLookPreview(); if (candleSnap != null) candleOv = candleSnap; candleSnap = null; candleCacheSrc = null; SetTheme(k, false); }
        void LooksGallery()
        {
            int before = lookBefore >= 0 ? lookBefore : themeIdx, chosen = themeIdx, inUse = before; bool preview = false;
            if (candleSnap == null) candleSnap = new Dictionary<string, string>(candleOv);
            var groups = new List<string> { "ALL" }; groups.AddRange(ChartTheme.All.Select(x => x.Group).Distinct()); if (myThemes.Count > 0) groups.Add("Mine");
            Func<int, string> groupOf = ix => ix >= ChartTheme.All.Length ? "Mine" : ChartTheme.All[ix].Group;
            string filter = lookFilter; if (!groups.Contains(filter) || (filter != "ALL" && filter != groupOf(chosen))) filter = "ALL";   // opens on the look in use
            var root = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) }; root.Children.Add(top); var fbtns = new List<Button>();
            var note = new TextBlock { Foreground = Muted, FontSize = 11.5, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap, Text = "click a look: the whole platform takes it • 🕯 CANDLES and 🎯 ENTRY LINES below change its colours (lines: AUTO keeps them apart from the candles) • 👁 PREVIEW folds this window into a small tab so you see the whole platform • SAVE keeps it, CANCEL goes back" }; root.Children.Add(note);
            var wrap = new WrapPanel(); root.Children.Add(wrap);
            // 🕯 the candle colours of the chosen look
            var candleRow = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) }; root.Children.Add(candleRow);
            var lineRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) }; root.Children.Add(lineRow);
            var opts = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false }; root.Children.Add(opts);
            var gridChk = new CheckBox { Content = "Grid squares in the background", IsChecked = showGrid, Foreground = Text, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center }; gridChk.Click += delegate { SetGrid(gridChk.IsChecked == true); }; opts.Children.Add(gridChk);
            var uiChk = new CheckBox { Content = "The whole interface follows the look (not only the chart)", IsChecked = lookUi, Foreground = Text, VerticalAlignment = VerticalAlignment.Center }; uiChk.Click += delegate { SetLookUi(uiChk.IsChecked == true); }; opts.Children.Add(uiChk);
            Action goPreview = null;
            Action build = null; Action buildCandles = null; Border chosenCard = null;
            buildCandles = () =>
            {
                candleRow.Children.Clear(); var th = ThemeAt(chosen); string cur; candleOv.TryGetValue(th.Name, out cur); cur = cur ?? "";
                candleRow.Children.Add(new TextBlock { Text = "🕯 CANDLES of " + th.Name + ":", Foreground = Gold, FontWeight = FontWeights.Bold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                foreach (var cs in CandleSets)
                {
                    string key = cs.Item1; var saved = candleOv.ContainsKey(th.Name) ? candleOv[th.Name] : null; candleOv[th.Name] = key; candleCacheSrc = null; var look = WithCandles(th); if (saved == null) candleOv.Remove(th.Name); else candleOv[th.Name] = saved; candleCacheSrc = null;
                    var mc = new Canvas { Width = 40, Height = 30, ClipToBounds = true, Background = new SolidColorBrush(look.Plot) }; MiniChart(mc, look, 4, 9, 5, 1); foreach (UIElement e in mc.Children) { Canvas.SetLeft(e, Canvas.GetLeft(e) - 8); Canvas.SetTop(e, Canvas.GetTop(e) - 6); }
                    var st = new StackPanel { Orientation = Orientation.Horizontal }; st.Children.Add(new Border { Child = mc, CornerRadius = new CornerRadius(4), ClipToBounds = true, Margin = new Thickness(0, 0, 6, 0) }); st.Children.Add(new TextBlock { Text = cs.Item2, Foreground = Text, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
                    var b = Pill("", key == cur ? Tint(Gold, 90) : (Brush)Card); b.Content = st; b.Height = 38; b.Padding = new Thickness(4, 0, 10, 0); b.Margin = new Thickness(0, 0, 6, 6); if (key == cur) b.BorderBrush = Gold;
                    b.Click += delegate { if (key == "") candleOv.Remove(th.Name); else candleOv[th.Name] = key; candleCacheSrc = null; SetTheme(chosen, false); build(); };
                    candleRow.Children.Add(b);
                }
                lineRow.Children.Clear(); string lcur; candleOv.TryGetValue("L:" + th.Name, out lcur); lcur = string.IsNullOrEmpty(lcur) ? "AUTO" : lcur;
                lineRow.Children.Add(new TextBlock { Text = "🎯 ENTRY LINES:", Foreground = Gold, FontWeight = FontWeights.Bold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), ToolTip = "the colours of your buys / T/P, sells / S/L and pending orders on the chart" });
                bool lightT = th.Light || Sx.Luma(th.Bg) > 0.55;
                foreach (var ls in LineSets)
                {
                    string key = ls[0]; var show = key == "AUTO" ? AutoLines(WithCandles(th), lightT) : ls;
                    var st = new StackPanel { Orientation = Orientation.Horizontal };
                    var sw = new StackPanel { Background = new SolidColorBrush(WithCandles(th).Plot), Margin = new Thickness(0, 0, 6, 0), Width = 34 };
                    foreach (int q in new[] { 2, 3, 4 }) sw.Children.Add(new Border { Height = 2.2, Margin = new Thickness(3, 3, 3, 2), Background = new SolidColorBrush(Sx.C(lightT ? show[q + 3] : show[q])) });
                    st.Children.Add(new Border { Child = sw, CornerRadius = new CornerRadius(4), ClipToBounds = true }); st.Children.Add(new TextBlock { Text = ls[1], Foreground = Text, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
                    var b = Pill("", key == lcur ? Tint(Gold, 90) : (Brush)Card); b.Content = st; b.Height = 34; b.Padding = new Thickness(4, 0, 10, 0); b.Margin = new Thickness(0, 0, 6, 6); if (key == lcur) b.BorderBrush = Gold;
                    b.Click += delegate { if (key == "AUTO") candleOv.Remove("L:" + th.Name); else candleOv["L:" + th.Name] = key; candleCacheSrc = null; SetTheme(chosen, false); build(); };
                    lineRow.Children.Add(b);
                }
            };
            build = () =>
            {
                wrap.Children.Clear(); for (int q = 0; q < fbtns.Count; q++) { fbtns[q].Background = groups[q] == filter ? (Brush)Tint(Gold, 90) : Card; }
                lookList = new List<int>(); chosenCard = null;
                for (int ix = 0; ix < ChartTheme.All.Length + myThemes.Count; ix++)
                {
                    if (filter != "ALL" && groupOf(ix) != filter) continue;
                    lookList.Add(ix); var th = WithCandles(ThemeAt(ix)); var card = LookCard(th, ix == chosen); int ii = ix;
                    if (ix == inUse) ((Grid)card.Child).Children.Add(new Border { Background = Gold, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 6, 6, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = "✓ IN USE", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Bg } });
                    if (ix == chosen)
                    {
                        chosenCard = card;
                        var pb = Pill("👁 PREVIEW", Tint(Cyan, 200)); pb.Height = 26; pb.FontSize = 11; pb.Padding = new Thickness(10, 0, 10, 0); pb.HorizontalAlignment = HorizontalAlignment.Center; pb.VerticalAlignment = VerticalAlignment.Center; pb.ToolTip = "see the whole platform in " + th.Name;
                        pb.Click += delegate { goPreview(); }; ((Grid)card.Child).Children.Add(pb);
                    }
                    card.MouseLeftButtonUp += delegate { chosen = ii; SetTheme(ii, false); build(); };
                    wrap.Children.Add(card);
                }
                buildCandles();
            };
            for (int q = 0; q < groups.Count; q++) { var f0 = groups[q]; var b = Pill(f0 == "ALL" ? "ALL LOOKS" : f0 == "Mine" ? "★ MINE" : LookGroup(ChartTheme.All.First(x => x.Group == f0)), Card); b.Click += delegate { filter = f0; build(); }; top.Children.Add(b); fbtns.Add(b); }
            build();
            wrap.Loaded += delegate { Dispatcher.BeginInvoke(new Action(() => { if (chosenCard != null) chosenCard.BringIntoView(); }), System.Windows.Threading.DispatcherPriority.Background); };   // opens on the look in use
            // the LOOKS PANEL: docked on the right of the chart (the chart stays in view), its buttons always on top: PREVIEW • SAVE • CANCEL
            Action close = () => { lookFilter = filter; CloseLooksPanel(); };
            goPreview = () => { close(); lookBefore = before; SetTheme(chosen, false); ShowLookTab(); };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };
            Func<string, Brush, string, Action, Button> hb = (t0, bg, tip, act) => { var b = Pill(t0, bg); b.Height = 34; b.FontSize = 12.5; b.Padding = new Thickness(12, 0, 12, 0); b.Margin = new Thickness(4, 0, 0, 0); b.ToolTip = tip; b.Click += delegate { act(); }; DockPanel.SetDock(b, Dock.Right); return b; };
            var xB = hb("✕ CANCEL", Card, "back to the look you had (nothing is saved)", () => { close(); RevertLook(before); }); head.Children.Add(xB);
            var sB = hb("✓ SAVE", Gold, "keep this look, its candles and its entry lines", () => { close(); CommitLook(chosen); }); sB.Foreground = Bg; head.Children.Add(sB);
            var pB = hb("👁 PREVIEW", Tint(Cyan, 120), "fold this panel into a small tab: the whole platform in the look; the tab opens it again", () => goPreview()); head.Children.Add(pB);
            head.Children.Add(new TextBlock { Text = "✨ LOOKS", Foreground = Gold, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            var body = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var dock = new DockPanel(); DockPanel.SetDock(head, Dock.Top); dock.Children.Add(head); dock.Children.Add(body);
            OpenLooksPanel(new Border { Child = dock, Width = 536, Background = Panel, BorderBrush = Gold, BorderThickness = new Thickness(1.6), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 6, 10), Margin = new Thickness(6, 2, 0, 2) });
        }
        Border looksPanel; Grid chartRoot; bool panelBeforeLooks;
        void OpenLooksPanel(Border b)
        {
            if (looksPanel != null) chartRoot.Children.Remove(looksPanel); else panelBeforeLooks = panelHost.Visibility == Visibility.Visible;
            if (lookTab != null) lookTab.Visibility = Visibility.Collapsed;
            panelHost.Visibility = Visibility.Collapsed; looksPanel = b; Grid.SetRow(b, 2); Grid.SetColumn(b, 2); System.Windows.Controls.Panel.SetZIndex(b, 50); chartRoot.Children.Add(b);
        }
        void CloseLooksPanel() { if (looksPanel == null) return; chartRoot.Children.Remove(looksPanel); looksPanel = null; if (panelBeforeLooks && !fullChart) panelHost.Visibility = Visibility.Visible; }
        void ShowLookTab()
        {
            if (lookTab == null)
            {
                lookTabName = new TextBlock { Foreground = Text, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 150, TextAlignment = TextAlignment.Center };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                Func<string, Brush, string, Action, Button> btn = (t0, bg, tip, a) => { var b = Pill(t0, bg); b.Height = 30; b.Padding = new Thickness(10, 0, 10, 0); b.Margin = new Thickness(3, 0, 3, 0); b.ToolTip = tip; b.Click += delegate { a(); }; return b; };
                sp.Children.Add(new TextBlock { Text = "👁 PREVIEW", Foreground = Cyan, FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 0) });
                sp.Children.Add(btn("◀", Card, "the look before", () => StepLook(-1)));
                var nameBtn = btn("", Card, "open the looks again to preview another", () => LooksGallery()); nameBtn.Content = lookTabName; sp.Children.Add(nameBtn);
                sp.Children.Add(btn("▶", Card, "the next look", () => StepLook(1)));
                sp.Children.Add(btn("✨ LOOKS", Card, "open the looks again to preview another", () => LooksGallery()));
                var ok = btn("✓ CONFIRM", Gold, "keep this look and its candles (the tab goes)", () => CommitLook(themeIdx)); ok.Foreground = Bg; sp.Children.Add(ok);
                sp.Children.Add(btn("✕", Card, "cancel: back to the look you had", () => RevertLook(lookBefore)));
                lookTab = new Border { Child = sp, Background = Panel, BorderBrush = Cyan, BorderThickness = new Thickness(1.6), CornerRadius = new CornerRadius(14), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.45 } };
                System.Windows.Controls.Panel.SetZIndex(lookTab, 900); Grid.SetRow(lookTab, 1); rootGrid.Children.Add(lookTab);
            }
            lookTabName.Text = CurTheme().Name; lookTab.Visibility = Visibility.Visible;
        }
        void StepLook(int d)
        {
            if (lookList.Count == 0) return; int at = lookList.IndexOf(themeIdx); at = at < 0 ? 0 : (at + d + lookList.Count) % lookList.Count;
            SetTheme(lookList[at], false); if (lookTabName != null) lookTabName.Text = CurTheme().Name;
        }
        void EndLookPreview() { lookBefore = -1; if (lookTab != null) lookTab.Visibility = Visibility.Collapsed; }
        // a small picture of the platform in a theme: the header (P/L + equity), the instruments, the chart, the BUY / SELL panel
        Border LookCard(ChartTheme t, bool on)
        {
            Func<Color, SolidColorBrush> B0 = c => new SolidColorBrush(c); bool light = t.Light || Sx.Luma(t.Bg) > 0.55;
            Color panel = light ? t.Bg : Color.FromRgb((byte)(t.Bg.R * 0.88), (byte)(t.Bg.G * 0.88), (byte)(t.Bg.B * 0.88)), card = t.Plot, line = t.Grid, txt = t.Text;
            Color grn = light ? Sx.C("#0F8A5F") : Sx.C("#2EE59D"), red = light ? Sx.C("#D23048") : Sx.C("#FF4D6D"), cyan = light ? Sx.C("#0A78A8") : Sx.C("#4DD8FF");
            var g = new Grid { Width = 232, Height = 150 }; var cv = new Canvas(); g.Children.Add(cv);
            Action<double, double, double, double, Color, double, Color?> box = (x, y, w0, h0, c, r, bdc) => { var b = new Border { Width = w0, Height = h0, Background = B0(c), CornerRadius = new CornerRadius(r), BorderBrush = bdc.HasValue ? B0(bdc.Value) : null, BorderThickness = new Thickness(bdc.HasValue ? 1.4 : 0) }; Canvas.SetLeft(b, x); Canvas.SetTop(b, y); cv.Children.Add(b); };
            Action<double, double, string, Color, double, bool> label = (x, y, s0, c, fs, bold) => { var tb = new TextBlock { Text = s0, Foreground = B0(c), FontSize = fs, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontFamily = Sx.Mono }; Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y); cv.Children.Add(tb); };
            box(0, 0, 232, 150, panel, 10, null);
            box(6, 6, 58, 22, card, 5, grn); label(10, 10, "+$1,240", grn, 9.5, true);
            box(68, 6, 54, 22, card, 5, grn); label(72, 10, "$15,890", txt, 9, true);
            box(126, 6, 40, 22, card, 5, null); label(130, 11, "1:500", txt, 8, false);
            box(6, 32, 44, 13, Color.FromArgb(200, cyan.R, cyan.G, cyan.B), 4, null); label(9, 33, "XAUUSD", panel, 7.5, true);
            box(53, 32, 40, 13, card, 4, null); label(56, 33, "BTCUSD", cyan, 7.5, true); box(96, 32, 34, 13, card, 4, null); label(99, 33, "US30", cyan, 7.5, true);
            var chart = new Canvas { Width = 170, Height = 96, ClipToBounds = true }; var chb = new Border { Width = 170, Height = 96, Background = B0(t.Plot), CornerRadius = new CornerRadius(6), Child = chart, BorderBrush = B0(line), BorderThickness = new Thickness(1) }; Canvas.SetLeft(chb, 6); Canvas.SetTop(chb, 49); cv.Children.Add(chb);
            MiniChart(chart, t, 10, 15, 7, 1); foreach (UIElement e in chart.Children) Canvas.SetTop(e, Canvas.GetTop(e) + 22);
            box(180, 49, 46, 96, card, 6, null); box(184, 56, 38, 18, red, 4, null); label(188, 60, "SELL", Colors.White, 8, true); box(184, 78, 38, 18, grn, 4, null); label(190, 82, "BUY", light ? Colors.White : panel, 8, true);
            label(184, 104, "0.10", txt, 8, true);
            var name = new TextBlock { Text = t.Name, Foreground = B0(txt), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 0, 0, 6), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left };
            var tag = new Border { Background = B0(Color.FromArgb(200, panel.R, panel.G, panel.B)), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 6, 6), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Child = new TextBlock { Text = LookGroup(t), FontSize = 8, Foreground = B0(txt) } };
            g.Children.Add(name); g.Children.Add(tag);
            return new Border { Child = g, Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(on ? 3 : 1.4), BorderBrush = on ? Gold : B0(line), Cursor = Cursors.Hand, ToolTip = t.Group + " • " + t.Name };
        }
        void SetZone(int i) { zoneIdx = i; Put("zone", i.ToString(IC)); zoneBtn.Content = "🕘 " + Zones[i].Item1 + "  ▾"; newsSig = ""; }

        // ---- the drawing rail
        FrameworkElement BuildRail()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 2, 6, 2) };
            foreach (var t in new[] { Tuple.Create("", "↖", "cursor (Esc)"), Tuple.Create("HLINE", "—", "horizontal line"), Tuple.Create("HRAY", "→", "horizontal ray"), Tuple.Create("VLINE", "│", "vertical line"), Tuple.Create("TREND", "╱", "trend line"), Tuple.Create("RAY", "↗", "trend ray (extends)"),
                Tuple.Create("RECT", "▭", "rectangle / zone"), Tuple.Create("FVG", "▤", "fair-value gap: click a candle — the 3-candle gap around it is drawn as a zone until it is filled"), Tuple.Create("FIB", "≋", "Fibonacci retracement (drag from the start of the move to its end)"), Tuple.Create("TEXT", "T", "text note"), Tuple.Create("LONG", "▲", "long position: risk / reward with your lots"), Tuple.Create("SHORT", "▼", "short position: risk / reward with your lots"), Tuple.Create("MEASURE", "📏", "measure: price, %, points, candles, time, $") })
            {
                var key = t.Item1; var b = RailBtn(t.Item2, t.Item3); b.Foreground = key == "LONG" ? Green : key == "SHORT" ? Red : Text;
                b.Click += delegate { Act.V.Tool = Act.V.Tool == key ? "" : key; if (key == "") Act.V.Deselect(); Act.V.Focus(); PaintRail(); Act.V.InvalidateVisual(); };
                sp.Children.Add(b); toolBtns.Add(Tuple.Create(key, b));
            }
            sp.Children.Add(new Border { Height = 1, Background = Line, Margin = new Thickness(4, 4, 4, 4) });
            magnetBtn = RailBtn("🧲", "magnet: points snap to the open / high / low / close"); magnetBtn.Click += delegate { magnet = !magnet; Put("magnet", magnet ? "1" : "0"); foreach (var p in panes) p.V.Magnet = magnet; PaintRail(); }; sp.Children.Add(magnetBtn);
            colorBtn = RailBtn("●", "the colour of new drawings"); colorBtn.Click += delegate { SxMenu.Show(colorBtn, new Point(colorBtn.ActualWidth + 4, 0), ColorMenu(drawColor, c => SetDrawColor(c))); }; sp.Children.Add(colorBtn);
            var del = RailBtn("🗑", "remove every drawing on this symbol"); del.Click += delegate { var v = Act.V; if (v.Drawings.Count == 0) { Toast("no drawings on " + Act.Sym); return; } if (Sx.Ask(this, "Remove the drawings", "Remove all " + v.Drawings.Count + " drawings on " + Act.Sym + "?", "REMOVE", "KEEP", Red)) { v.Drawings.Clear(); SaveDrawings(); v.InvalidateVisual(); } }; sp.Children.Add(del);
            return new Border { Background = Panel, CornerRadius = new CornerRadius(10), BorderBrush = Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 6, 2), Padding = new Thickness(3), Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp } };
        }
        Button RailBtn(string glyph, string tip) { var b = Pill(glyph, Brushes.Transparent); b.Width = 34; b.Height = 32; b.Padding = new Thickness(0); b.Margin = new Thickness(0, 1, 0, 1); b.FontSize = 14; b.BorderBrush = Brushes.Transparent; b.ToolTip = tip; return b; }
        void PaintRail()
        {
            string tool = panes.Count > 0 ? Act.V.Tool : "";
            foreach (var t in toolBtns) { bool on = t.Item1 == tool; t.Item2.Background = on ? Tint(Gold, 70) : (Brush)Brushes.Transparent; t.Item2.BorderBrush = on ? Gold : (Brush)Brushes.Transparent; }
            if (magnetBtn != null) { magnetBtn.Background = magnet ? Tint(Cyan, 70) : (Brush)Brushes.Transparent; magnetBtn.BorderBrush = magnet ? Cyan : (Brush)Brushes.Transparent; }
            if (colorBtn != null) colorBtn.Foreground = Sx.B(drawColor);
        }
        // the colour of new drawings: the rail's ●, the Draw menu or a colour picked on any drawing — it stays as chosen (saved)
        void SetDrawColor(string c) { drawColor = c; Put("drawcolor", c); foreach (var q in panes) q.V.DrawColor = c; PaintRail(); }
        // a rectangle's / FVG's background: "" light, STRONG, NONE or a colour — the next rectangles get it too
        void SetFill(ChartView v, Drawing dr, string f) { dr.Fill = f; if (dr.Kind == "RECT") { drawFill = f; Put("drawfill", f); foreach (var q in panes) q.V.DrawFill = f; } SaveDrawings(); v.InvalidateVisual(); }
        string drawFill = "";
        List<Mi> ColorMenu(string cur, Action<string> pick)
        {
            var names = new[] { "gold", "cyan", "violet", "mint", "red", "orange", "pink", "white" }; var l = new List<Mi> { Mi.H("colour") };
            for (int i = 0; i < DrawColors.Length; i++) { var c = DrawColors[i]; l.Add(new Mi { Text = names[i], Dot = Sx.B(c), Hint = string.Equals(c, cur, StringComparison.OrdinalIgnoreCase) ? "✓" : "", Do = () => pick(c) }); }
            return l;
        }

        Pane MakePane(int k)
        {
            var p = new Pane(); var v = new ChartView(); p.V = v;
            p.Head = new TextBlock { Foreground = Muted, FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 3, 0, 3), TextTrimming = TextTrimming.CharacterEllipsis };
            var dock = new DockPanel(); DockPanel.SetDock(p.Head, Dock.Top); dock.Children.Add(p.Head); dock.Children.Add(v);
            p.Host = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Line, BorderThickness = new Thickness(1.4), Child = dock, Margin = new Thickness(2), Background = Panel };
            p.Host.PreviewMouseLeftButtonDown += delegate { if (activePane != k) { activePane = k; PaintBar(); PaintRail(); } };
            p.Host.PreviewMouseRightButtonDown += delegate { if (activePane != k) { activePane = k; PaintBar(); PaintRail(); } };
            v.CloseTicket = (tk, lots) => Cmd("close #" + tk, "CLOSE", tk, lots);
            v.ModifyTicket = (tk, sl, tp) => Cmd("S/L " + (sl > 0 ? sl.ToString("0.#####", IC) : "—") + " • T/P " + (tp > 0 ? tp.ToString("0.#####", IC) : "—") + " on #" + tk, "MODIFY", tk, sl, tp);
            v.ModifyOrder = (tk, px, sl, tp) => Cmd("order #" + tk + " → " + px.ToString("0.#####", IC), "MODORD", tk, px, sl, tp);
            v.CancelOrder = tk => Cmd("cancel order #" + tk, "CANCEL", tk);
            v.CloseSide = dir => CloseAll(p.Sym, dir > 0 ? "BUY" : "SELL");
            v.PricePicked = px => { pendPrice.Text = px.ToString("F" + v.Digits, IC); };
            v.Toast = Toast; v.DrawingsChanged = SaveDrawings; v.ToolDone = () => PaintRail();
            v.AskText = cur => Sx.Prompt(this, "✏ Text on the chart", "The note to show on the chart:", cur);
            v.RightClick = (pt, px, t, kind, tk, dr) => ChartMenu(p, pt, px, kind, tk, dr); v.IndOff = key => IndOff(key, p.Sym);
            return p;
        }
        void SetSplit(int n)
        {
            split = n == 2 || n == 4 ? n : 1; Put((Sim ? "bt." : "") + "split", split.ToString(IC)); chartsGrid.Children.Clear(); chartsGrid.RowDefinitions.Clear(); chartsGrid.ColumnDefinitions.Clear();
            int cols = split == 1 ? 1 : 2, rows = split == 4 ? 2 : 1;
            for (int c = 0; c < cols; c++) chartsGrid.ColumnDefinitions.Add(new ColumnDefinition()); for (int r = 0; r < rows; r++) chartsGrid.RowDefinitions.Add(new RowDefinition());
            for (int k = 0; k < split; k++) { var h = panes[k].Host; Grid.SetColumn(h, k % cols); Grid.SetRow(h, k / cols); chartsGrid.Children.Add(h); }
            if (activePane >= split) activePane = 0; PaintBar();
        }
        // the indicators belong to the INSTRUMENT (and the mode): gold keeps its own, NAS100 its own — every chart of that symbol shows them
        string IndKey(string sym, string key) { return ModeKey + "ind." + (sym ?? "").ToUpperInvariant() + "." + key; }
        bool IndOn(string sym, string key) { return Get(IndKey(sym, key), "0") == "1"; }
        void LoadInd(Pane p)
        {
            p.V.Ind.Clear(); if (p.Sym != "") foreach (var t in ChartView.Indicators) if (IndOn(p.Sym, t.Key)) p.V.Ind.Add(t.Key);
            int fs; p.V.FvgSide = int.TryParse(Get(IndKey(p.Sym, "FVGSIDE"), "0"), NumberStyles.Integer, IC, out fs) ? fs : 0; p.V.InvalidateVisual();
        }
        // 🧹 CLEAN CHART (the toolbar or a right-click): every drawing and indicator of the chart's instrument off at once, no question
        // asked; the last clean of each instrument can be undone from the right-click menu
        readonly Dictionary<string, Tuple<List<Drawing>, List<string>>> cleanUndo = new Dictionary<string, Tuple<List<Drawing>, List<string>>>(StringComparer.OrdinalIgnoreCase);
        void CleanChart(Pane p)
        {
            if (p == null || p.Sym == "") return; var v = p.V; var dr = v.Drawings.Select(d => d.Clone()).ToList(); var ind = v.Ind.ToList();
            if (dr.Count == 0 && ind.Count == 0) { Toast("🧹 " + p.Sym + " is already clean"); return; }
            cleanUndo[p.Sym] = Tuple.Create(dr, ind);
            v.Drawings.Clear(); v.Deselect(); SaveDrawings(); foreach (var k in ind) SetInd(p.Sym, k, false);
            foreach (var q in panes) q.V.InvalidateVisual(); PaintBar();
            Toast("🧹 " + p.Sym + " cleaned: " + dr.Count + " drawing" + (dr.Count == 1 ? "" : "s") + " + " + ind.Count + " indicator" + (ind.Count == 1 ? "" : "s") + " removed • right-click ▸ ↶ Undo the clean");
        }
        void UndoClean(string sym)
        {
            Tuple<List<Drawing>, List<string>> u; if (!cleanUndo.TryGetValue(sym, out u)) return; cleanUndo.Remove(sym);
            var p = panes.FirstOrDefault(x => string.Equals(x.Sym, sym, StringComparison.OrdinalIgnoreCase));
            if (p != null) { p.V.Drawings.AddRange(u.Item1); SaveDrawings(); } foreach (var k in u.Item2) SetInd(sym, k, true);
            foreach (var q in panes) q.V.InvalidateVisual(); PaintBar(); Toast("↶ " + sym + ": " + u.Item1.Count + " drawings + " + u.Item2.Count + " indicators back");
        }
        void SetInd(string sym, string key, bool on) { Put(IndKey(sym, key), on ? "1" : "0"); foreach (var p in panes) if (string.Equals(p.Sym, sym, StringComparison.OrdinalIgnoreCase)) { if (on) p.V.Ind.Add(key); else p.V.Ind.Remove(key); p.V.InvalidateVisual(); } }
        void IndOff(string key, string sym = null) { if (string.IsNullOrEmpty(sym)) sym = Act.Sym; SetInd(sym, key, false); PaintBar(); var ind = ChartView.Indicators.FirstOrDefault(x => x.Key == key); if (ind != null) Toast("✕ " + ind.Name + " removed from " + sym); }
        void SetFvgSide(int side) { Put(IndKey(Act.Sym, "FVGSIDE"), side.ToString(IC)); foreach (var p in panes) if (string.Equals(p.Sym, Act.Sym, StringComparison.OrdinalIgnoreCase)) { p.V.FvgSide = side; p.V.InvalidateVisual(); } if (!Act.V.Ind.Contains("FVG")) SetInd(Act.Sym, "FVG", true); PaintBar(); }
        void ToggleInd(string key)
        {
            bool on = !Act.V.Ind.Contains(key); SetInd(Act.Sym, key, on); PaintBar();
            var ind = ChartView.Indicators.First(x => x.Key == key); Toast((on ? "☑ " : "☐ ") + ind.Name + (on ? " on — " + ind.Tip : " off"));
        }
        // ⛶ FULL: the chart takes the whole window; only one small strip of buttons stays on top (instrument, timeframe, its P/L, SELL / BUY, the panel, exit)
        FrameworkElement chartBar, chartRail; Border fullStrip; Button fsSym, fsSell, fsBuy; TextBlock fsPl; readonly List<Tuple<int, Button>> fsTf = new List<Tuple<int, Button>>(); bool panelBeforeFull;
        // ✕ SELLS / ✕ ALL / ✕ BUYS of the chart's instrument on the toolbar: solid colours, easy to see on every look (each asks first)
        FrameworkElement CloseBtns(double h)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 4, 0) };
            foreach (var m in new[] { Tuple.Create("✕ SELLS", "SELL", Sx.C("#E5484D")), Tuple.Create("✕ ALL", "ALL", Sx.C("#F0B429")), Tuple.Create("✕ BUYS", "BUY", Sx.C("#12B886")) })
            {
                var which = m.Item2; var b = Pill(m.Item1, new SolidColorBrush(m.Item3)); b.Foreground = which == "SELL" ? Brushes.White : new SolidColorBrush(Sx.C("#101418")); b.BorderBrush = new SolidColorBrush(Sx.C("#FFFFFF")) { Opacity = 0.35 };
                b.Height = h; b.FontSize = 12; b.FontWeight = FontWeights.Bold; b.Padding = new Thickness(10, 0, 10, 0); b.Margin = new Thickness(0, 0, 4, 0);
                b.ToolTip = "close " + (which == "ALL" ? "every ticket" : "the " + which.ToLowerInvariant() + "s") + " of the chart's instrument (asks first)"; b.Click += delegate { CloseAll(Act.Sym, which); }; sp.Children.Add(b);
            }
            return sp;
        }
        Border BuildFullStrip()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Func<string, Brush, Button> small = (t0, bg) => { var b = Pill(t0, bg); b.Height = 28; b.FontSize = 11.5; b.Padding = new Thickness(9, 0, 9, 0); b.Margin = new Thickness(2, 0, 2, 0); return b; };
            fsSym = Sx.Drop("", Tint(Cyan, 60), () => listSyms.Select(x => new Mi { Text = x, Check = string.Equals(x, Act.Sym, StringComparison.OrdinalIgnoreCase), Radio = true, Do = () => SetPane(Act, x, Act.Tf) }).ToList());
            fsSym.Height = 28; fsSym.FontSize = 12; fsSym.Foreground = Cyan; fsSym.ToolTip = "the instrument"; sp.Children.Add(fsSym);
            sp.Children.Add(new Border { Width = 1, Height = 20, Background = Line, Margin = new Thickness(6, 0, 6, 0) });
            foreach (var m in Tf.All) { int mm = m; var b = small(Tf.Name(m), Card); b.MinWidth = 34; b.Padding = new Thickness(5, 0, 5, 0); b.Click += delegate { SetPane(Act, Act.Sym, mm); }; sp.Children.Add(b); fsTf.Add(Tuple.Create(m, b)); }
            sp.Children.Add(new Border { Width = 1, Height = 20, Background = Line, Margin = new Thickness(6, 0, 6, 0) });
            fsPl = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 0), Foreground = Muted }; sp.Children.Add(fsPl);
            fsSell = small("SELL", Red); fsSell.Foreground = Sx.White; fsSell.MinWidth = 110; fsSell.ToolTip = "SELL the panel's lots (like the panel's SELL: needs ARM)"; fsSell.Click += delegate { Market(-1); }; sp.Children.Add(fsSell);
            fsBuy = small("BUY", Green); fsBuy.Foreground = Bg; fsBuy.MinWidth = 110; fsBuy.ToolTip = "BUY the panel's lots (like the panel's BUY: needs ARM)"; fsBuy.Click += delegate { Market(1); }; sp.Children.Add(fsBuy);
            sp.Children.Add(CloseBtns(28));
            sp.Children.Add(new Border { Width = 1, Height = 20, Background = Line, Margin = new Thickness(6, 0, 6, 0) });
            var pb = small("◧ PANEL", Card); pb.ToolTip = "show / hide the trade panel on the right"; pb.Click += delegate { panelHost.Visibility = panelHost.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }; sp.Children.Add(pb);
            var tb = small("🧰 TOOLS", Card); tb.ToolTip = "show / hide the drawing tools on the left"; tb.Click += delegate { chartRail.Visibility = chartRail.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }; sp.Children.Add(tb);
            sp.Children.Add(LinesBtn(28));
            var ex = small("⛶ EXIT FULL (Esc)", Tint(Gold, 90)); ex.Click += delegate { SetFullChart(false); }; sp.Children.Add(ex);
            return new Border { Child = new ScrollViewer { Content = sp, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }, Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
        }
        void ChartFull(bool on)
        {
            if (chartBar == null) return;
            if (on && fullStrip.Visibility != Visibility.Visible) panelBeforeFull = panelHost.Visibility == Visibility.Visible;
            chartBar.Visibility = on ? Visibility.Collapsed : Visibility.Visible; fullStrip.Visibility = on ? Visibility.Visible : Visibility.Collapsed; chartRail.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            panelHost.Visibility = on ? Visibility.Collapsed : panelBeforeFull ? Visibility.Visible : Visibility.Collapsed;
            PaintBar();
        }
        void PaintBar()
        {
            foreach (var t in fsTf) { bool on = t.Item1 == Act.Tf; t.Item2.Background = on ? Gold : (Brush)Card; t.Item2.Foreground = on ? Bg : Gold; }
            if (fsSym != null) fsSym.Content = (string.IsNullOrEmpty(Act.Sym) ? "instrument" : Act.Sym) + "  ▾";
            foreach (var t in tfBtns) { bool on = t.Item1 == Act.Tf; t.Item2.Background = on ? Gold : (Brush)Card; t.Item2.Foreground = on ? Bg : Gold; }
            foreach (var t in splitBtns) { bool on = t.Item1 == split; t.Item2.Background = on ? Tint(Violet, 170) : (Brush)Card; }
            if (indBtn != null) { int n = Act.V.Ind.Count; indBtn.Content = "📈 INDICATORS" + (n > 0 ? " (" + n + ")" : "") + "  ▾"; indBtn.BorderBrush = n > 0 ? Tint(Cyan, 160) : Line; }
            for (int k = 0; k < panes.Count; k++) { panes[k].Host.BorderBrush = split > 1 && k == activePane ? Gold : Line; }
            symSig = "";
        }
        void SetPane(Pane p, string sym, int tf)
        {
            if (string.IsNullOrEmpty(sym)) return; bool same = string.Equals(sym, p.Sym, StringComparison.OrdinalIgnoreCase) && tf == p.Tf;
            if (!same) SaveDrawings();
            p.Sym = sym; p.Tf = tf; int k = panes.IndexOf(p); if (!Sim) Put("sym" + k, sym); Put((Sim ? "bt." : "") + "tf" + k, tf.ToString(IC)); if (!same) { p.V.ResetView(); p.V.Drawings = DrawingsOf(sym); } LoadInd(p);
            PaintBar();
        }
        Func<DateTime, DateTime> ZoneConv(Mt5State s, out string name)
        {
            var z = Zones[Math.Max(0, zoneIdx)]; name = new[] { "server", "NY", "London", "CET", "Frankfurt", "Tokyo", "Sydney", "UTC", "PC" }[Math.Max(0, Math.Min(8, zoneIdx))]; int off = s.ServerOffsetSec;
            if (z.Item2 == "") return t => t;
            TimeZoneInfo tz = null; try { tz = z.Item2 == "LOCAL" ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(z.Item2); } catch { }
            if (tz == null) return t => t.AddSeconds(-off);
            return t => { try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t.AddSeconds(-off), DateTimeKind.Utc), tz); } catch { return t; } };
        }
        int NyOffset(Mt5State s)
        {
            if (sim != null && sim.Spec != null) return sim.Spec.NyOffsetHours;
            try { var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); return (int)Math.Round(ny.GetUtcOffset(DateTime.UtcNow).TotalHours - s.ServerOffsetSec / 3600.0); } catch { return -7; }
        }

        // ---------------------------------------------------------------- every refresh: the charts
        // the MT5 server clock, running: the newest server time we were told (the bridge's clock or any instrument's last tick) moved on with this PC's clock,
        // so every countdown keeps going between ticks (it stood still — or at 0 — until the next price came)
        TextBlock manageTitle;   // the chart panel's "🧹 MANAGE <instrument> ONLY"
        DateTime srvSeen = DateTime.MinValue; TimeSpan srvOff = TimeSpan.Zero; bool srvKnown;
        DateTime LiveServerNow(Mt5State s)
        {
            if (Sim) return s.ServerNow;
            var best = s.ServerNow; foreach (var q in s.Quotes.Values) if (q != null && q.Time > best) best = q.Time;
            if (best == DateTime.MinValue) return best;
            var now = DateTime.Now; var off = best - now;
            if (!srvKnown || best != srvSeen && (off > srvOff || (srvOff - off).TotalMinutes > 2)) { srvOff = off; srvKnown = true; }
            srvSeen = best; return now + srvOff;
        }
        void FillChart(Mt5State s)
        {
            // the symbols: yours (tickets), the ones you added, then Market Watch (a backtest: its symbol)
            var mine = s.Positions.Select(p => p.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var added = Sim ? new List<string>() : Get("watch", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var syms = Sim ? s.Symbols.Keys.ToList() : mine.Concat(added).Concat(s.Quotes.Keys.OrderBy(k => k).Take(8)).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToList();
            foreach (var p in panes.Take(split)) if (p.Sym == "" || !s.Quotes.ContainsKey(p.Sym)) { var first = mine.FirstOrDefault() ?? s.Quotes.Keys.FirstOrDefault(k => k.StartsWith("XAU", StringComparison.OrdinalIgnoreCase)) ?? syms.FirstOrDefault(); if (first != null && s.Quotes.ContainsKey(first) && p.Sym != first) SetPane(p, first, p.Tf); }
            Func<string, double> plOf = sy => s.Positions.Where(x => string.Equals(x.Symbol, sy, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Profit + x.Swap);
            if (!Sim)
            {
                var hidden = SymList("symhide"); var order = SymList("symorder");
                syms = syms.Where(x => !hidden.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
                syms = syms.Select((x, k) => Tuple.Create(x, k)).OrderBy(t => { int ix = order.FindIndex(o => string.Equals(o, t.Item1, StringComparison.OrdinalIgnoreCase)); return ix < 0 ? 1000 + t.Item2 : ix; }).Select(t => t.Item1).ToList();
                foreach (var p in panes.Take(split)) if (p.Sym != "" && hidden.Contains(p.Sym, StringComparer.OrdinalIgnoreCase) && syms.Count > 0) SetPane(p, syms[0], p.Tf);
            }
            listSyms = syms;
            // preload: one series a moment (every instrument on the list × the charts' timeframes), so a click on a chip shows its chart at once
            if (!Sim && s.BridgeVersion >= 2 && (DateTime.Now - prefetchAt).TotalMilliseconds > 600)
            {
                prefetchAt = DateTime.Now; var tfs = panes.Take(split).Select(x => x.Tf).Distinct().ToList();
                foreach (var sy in syms) { bool sent = false; foreach (var tf in tfs) { var se0 = s.SeriesOf(sy, tf); int tries0; prefetchTries.TryGetValue(sy + "|" + tf, out tries0); if (!se0.Loaded && tries0 < 3 && (DateTime.Now - se0.AskedAt).TotalSeconds > 8) { prefetchTries[sy + "|" + tf] = tries0 + 1; se0.AskedAt = DateTime.Now; broker.Send("BARS|" + sy + "|" + tf + "|" + BarsWanted(sy, tf)); sent = true; break; } } if (sent) break; }
            }
            string sig = string.Join(",", syms) + "|" + Act.Sym + "|" + symDragSig + "|" + string.Join(",", mine) + "|" + string.Join(",", mine.Select(m => plOf(m) >= 0 ? "+" : "-"));   // the P/L numbers update in place (no rebuild on every tick)
            if (sig != symSig)
            {
                symSig = sig; symChips.Children.Clear(); chipLabs.Clear();
                foreach (var sy in syms)
                {
                    var s0 = sy; bool on = string.Equals(sy, Act.Sym, StringComparison.OrdinalIgnoreCase); bool hasPos = mine.Contains(sy, StringComparer.OrdinalIgnoreCase);
                    double cpl = hasPos ? plOf(sy) : 0; var b = Pill("", on ? (Brush)Tint(Cyan, 200) : Card); b.Padding = new Thickness(10, 0, 4, 0); b.Foreground = on ? Bg : Cyan; b.BorderBrush = on ? Cyan : Tint(Cyan, 90); b.Click += delegate { SetPane(Act, s0, Act.Tf); };
                    // drag a chip onto another to change the order • ✕ removes the instrument from the list (+ brings it back)
                    var lab = new TextBlock { Text = (hasPos ? "● " : "") + sy + (hasPos ? "  " + (cpl >= 0 ? "+" : "−") + "$" + Math.Abs(cpl).ToString("N0", IC) : ""), VerticalAlignment = VerticalAlignment.Center };
                    var xb = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Margin = new Thickness(7, 0, 0, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, ToolTip = "remove " + sy + " from the list (+ adds it back)", Child = new TextBlock { Text = "✕", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = on ? Bg : Muted } };
                    xb.MouseEnter += delegate { xb.Background = Tint(Red, 200); ((TextBlock)xb.Child).Foreground = Sx.White; }; xb.MouseLeave += delegate { xb.Background = Brushes.Transparent; ((TextBlock)xb.Child).Foreground = on ? Bg : Muted; };
                    xb.MouseLeftButtonDown += (o, e) => { e.Handled = true; HideSym(s0, hasPos); };
                    var cp = new StackPanel { Orientation = Orientation.Horizontal }; cp.Children.Add(lab); cp.Children.Add(xb); b.Content = cp; if (hasPos) chipLabs[sy] = Tuple.Create(lab, b);
                    Point? down = null; b.AllowDrop = true;
                    b.PreviewMouseLeftButtonDown += (o, e) => { down = e.GetPosition(symChips); };
                    b.PreviewMouseMove += (o, e) => { if (down == null || e.LeftButton != MouseButtonState.Pressed) return; var pt = e.GetPosition(symChips); if (Math.Abs(pt.X - down.Value.X) + Math.Abs(pt.Y - down.Value.Y) < 8) return; down = null; b.Opacity = 0.5; DragDrop.DoDragDrop(b, new DataObject("spx-sym", s0), DragDropEffects.Move); b.Opacity = 1; };
                    b.PreviewMouseLeftButtonUp += delegate { down = null; };
                    b.DragOver += (o, e) => { e.Effects = e.Data.GetDataPresent("spx-sym") ? DragDropEffects.Move : DragDropEffects.None; b.BorderBrush = Gold; e.Handled = true; };
                    b.DragLeave += delegate { b.BorderBrush = on ? Cyan : Tint(Cyan, 90); };
                    b.Drop += (o, e) => { var from = e.Data.GetData("spx-sym") as string; b.BorderBrush = on ? Cyan : Tint(Cyan, 90); if (from == null || from == s0) return; MoveSym(from, s0, e.GetPosition(b).X > b.ActualWidth / 2); e.Handled = true; };
                    b.ToolTip = hasPos ? "you have tickets on " + sy + " • P/L " + Signed(cpl, s.Account.Currency) : sy; if (hasPos && !on) b.Foreground = cpl >= 0 ? Green : Red; symChips.Children.Add(b);
                }
            }
            foreach (var kv in chipLabs) { double cpl = plOf(kv.Key); string t0 = "● " + kv.Key + "  " + (cpl >= 0 ? "+" : "−") + "$" + Math.Abs(cpl).ToString("N0", IC); if (kv.Value.Item1.Text != t0) { kv.Value.Item1.Text = t0; kv.Value.Item2.ToolTip = "you have tickets on " + kv.Key + " • P/L " + Signed(cpl, s.Account.Currency); } }
            string zn; var conv = ZoneConv(s, out zn); int nyOff = NyOffset(s);
            if (manageTitle != null) manageTitle.Text = "🧹 MANAGE " + (Act.Sym == "" ? "THIS SYMBOL" : Act.Sym + " ONLY");
            // every timeframe's countdown (the selected chart's symbol)
            { Mt5Quote aq; s.Quotes.TryGetValue(Act.Sym, out aq); var sn = LiveServerNow(s); bool closed = !Sim && aq != null && sn > DateTime.MinValue && (sn - aq.Time).TotalMinutes > 3;
              foreach (var kv in tfCountdown) { if (sn == DateTime.MinValue || closed) { kv.Value.Text = closed ? "closed" : ""; continue; } var left = Tf.Left(sn, kv.Key);
                kv.Value.Text = left.TotalDays >= 1 ? ((int)left.TotalDays) + "d " + left.Hours + "h" : left.TotalHours >= 1 ? ((int)left.TotalHours) + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00") : left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00"); } }
            var now = DateTime.Now; bool trade = Virtual || (armed && s.CanTrade);
            for (int k = 0; k < split; k++)
            {
                var p = panes[k]; if (p.Sym == "") { if (Sim) { p.V.Status = BtIdleText(); p.V.InvalidateVisual(); } continue; } var v = p.V;
                var se = s.SeriesOf(p.Sym, p.Tf);
                if (!Sim)
                {
                    if (s.BridgeVersion < 2) v.Status = "the chart needs the new bridge — put SpasiXBridge.mq5 in MT5 (⚙ SETUP)";
                    else if (!se.Loaded) { v.Status = p.Sym + " • " + Tf.Name(p.Tf) + "  ⟳"; if ((now - se.AskedAt).TotalSeconds > 3) { se.AskedAt = now; broker.Send("BARS|" + p.Sym + "|" + p.Tf + "|" + BarsWanted(p.Sym, p.Tf)); } }
                    else if ((now - se.AskedAt).TotalSeconds > 20) { se.AskedAt = now; broker.Send("BARS|" + p.Sym + "|" + p.Tf + "|3"); }
                }
                else if (!se.Loaded) v.Status = BtIdleText();
                Mt5Quote q; s.Quotes.TryGetValue(p.Sym, out q); var sym = s.Symbols.ContainsKey(p.Sym) ? s.Symbols[p.Sym] : null;
                v.Series = se.Loaded ? se : null; v.Symbol = p.Sym; v.Digits = sym != null ? sym.Digits : 2; v.Point = sym != null && sym.Point > 0 ? sym.Point : 0.01; v.TickValue = sym != null && sym.TickValue > 0 ? sym.TickValue : 1; v.TickSize = sym != null && sym.TickSize > 0 ? sym.TickSize : v.Point;
                v.Bid = q != null ? q.Bid : 0; v.Ask = q != null ? q.Ask : 0; v.QuoteTime = q != null ? q.Time : DateTime.MinValue; v.Positions = s.Positions; v.Orders = s.Orders; v.Currency = s.Account.Currency; v.Gate = s.Gate; v.Lots = Num(lotsBox);
                v.ToView = conv; v.ZoneName = zn; v.ServerNow = LiveServerNow(s); v.NyOffsetHours = nyOff; v.ServerOffsetSec = s.ServerOffsetSec; v.TradeOn = trade;
                { Tuple<double, int> ex; v.EscapePx = escapes.TryGetValue(p.Sym, out ex) ? ex.Item1 : double.NaN; v.VolStep = sym != null && sym.VolStep > 0 ? sym.VolStep : 0.01; }
                var dl = DrawingsOf(p.Sym); if (!ReferenceEquals(v.Drawings, dl)) v.Drawings = dl;   // the symbol's own drawings (saved)
                if (Sim) { v.News = ""; v.NewsHot = false; } else { var nw = NewsFor(p.Sym); v.News = nw.Item1; v.NewsHot = nw.Item2; }
                string f = "F" + v.Digits; var ps = s.Positions.Where(x => string.Equals(x.Symbol, p.Sym, StringComparison.OrdinalIgnoreCase)).ToList(); double pl = ps.Sum(x => x.Profit + x.Swap);
                p.Head.Text = (split > 1 && k == activePane ? "▶ " : "") + p.Sym + " • " + Tf.Name(p.Tf) + (q != null ? "   " + q.Bid.ToString(f, IC) + " / " + q.Ask.ToString(f, IC) + "   spread " + q.Spread(v.Point).ToString("0", IC) : "") + (sym != null && sym.Description != "" ? "   " + sym.Description : "") + (ps.Count > 0 ? "   •   " + ps.Count + " ticket" + (ps.Count == 1 ? "" : "s") + " " + Signed(pl, s.Account.Currency) : "");
                p.Head.Foreground = k == activePane ? Gold : Muted;
                // redraw only when something on it changed (a new price, a candle, a ticket, the clock second…): far less work for the screen
                var rs = new System.Text.StringBuilder(); rs.Append(se.Version).Append('|').Append(se.Bars.Count).Append('|').Append(v.Bid).Append('|').Append(v.Ask).Append('|').Append(v.Status).Append('|').Append(v.News).Append(v.NewsHot).Append(trade).Append('|').Append(v.Lots).Append('|').Append(DateTime.Now.Second).Append('|').Append(dl.Count).Append('|').Append(v.EscapePx);
                foreach (var x in ps) rs.Append(x.Ticket).Append(':').Append(x.Sl).Append(':').Append(x.Tp).Append(':').Append(Math.Round(x.Profit, 1)).Append(',');
                foreach (var o in s.Orders) if (string.Equals(o.Symbol, p.Sym, StringComparison.OrdinalIgnoreCase)) rs.Append(o.Ticket).Append(':').Append(o.Price).Append(':').Append(o.Sl).Append(':').Append(o.Tp).Append(',');
                string rsig = rs.ToString(); if (rsig != p.DrawSig) { p.DrawSig = rsig; v.InvalidateVisual(); }
            }
            FillSymPl(s); FillSpreads(s); FillSpreadStrip(s);
            FillPanel(s);
        }
        void FillSymPl(Mt5State s)
        {
            if (symPlBox == null) return; var sym = Act.Sym; var cur = s.Account.Currency;
            var ps = s.Positions.Where(x => string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); var os = s.Orders.Count(x => string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase));
            double pl = ps.Sum(x => x.Profit + x.Swap), bl = ps.Where(x => x.Dir > 0).Sum(x => x.Lots), sl = ps.Where(x => x.Dir < 0).Sum(x => x.Lots); int nb = ps.Count(x => x.Dir > 0), ns = ps.Count(x => x.Dir < 0);
            symPlCap.Text = sym + " • P/L of this instrument"; symPlVal.Text = ps.Count == 0 ? "no ticket" : Signed(pl, cur); symPlVal.Foreground = ps.Count == 0 ? Muted : pl > 0 ? Green : pl < 0 ? Red : Text;
            double bp = ps.Where(x => x.Dir > 0).Sum(x => x.Profit + x.Swap), spp = ps.Where(x => x.Dir < 0).Sum(x => x.Profit + x.Swap); bool both = nb > 0 && ns > 0;
            symBuy.Item1.Visibility = nb > 0 ? Visibility.Visible : Visibility.Collapsed; symBuy.Item2.Text = "▲ BUY  " + nb + " ticket" + (nb == 1 ? "" : "s") + (both && bl > sl ? "  ★" : ""); symBuy.Item3.Text = bl.ToString("0.00", IC) + " lots  " + Signed(bp, cur); symBuy.Item3.Foreground = bp >= 0 ? Green : Red;
            symSell.Item1.Visibility = ns > 0 ? Visibility.Visible : Visibility.Collapsed; symSell.Item2.Text = "▼ SELL  " + ns + " ticket" + (ns == 1 ? "" : "s") + (both && sl > bl ? "  ★" : ""); symSell.Item3.Text = sl.ToString("0.00", IC) + " lots  " + Signed(spp, cur); symSell.Item3.Foreground = spp >= 0 ? Green : Red;
            symHedge.Item1.Visibility = both ? Visibility.Visible : Visibility.Collapsed;
            if (both) { double net = bl - sl; symHedge.Item2.Text = "⇅ HEDGED"; symHedge.Item3.Text = Math.Abs(net) < 1e-9 ? "net 0 (fully)" : "net " + Math.Abs(net).ToString("0.00", IC) + " " + (net > 0 ? "BUY" : "SELL"); symHedge.Item1.ToolTip = "both directions open on " + sym + " • ★ = the side with more lots"; }
            symPlInfo.Text = os > 0 ? "⏳ " + os + " order" + (os == 1 ? "" : "s") : "";
            int sg = ps.Count == 0 ? 0 : pl >= 0 ? 1 : -1; if (sg == symPlSign) return; symPlSign = sg;
            if (sg == 0) { symPlBox.BorderBrush = Line; return; }
            var br = new SolidColorBrush(sg > 0 ? Green.Color : Red.Color); symPlBox.BorderBrush = br;
            br.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(750)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        }

        // ---------------------------------------------------------------- the right-click menus
        void ChartMenu(Pane p, Point pt, double price, string kind, long tk, Drawing dr)
        {
            var v = p.V; string f = "F" + v.Digits; var l = new List<Mi>(); bool trade = Virtual || armed;
            Mt5Position pos = null; Mt5Order ord = null; lock (broker.State.Gate) { pos = broker.State.Positions.FirstOrDefault(x => x.Ticket == tk); ord = broker.State.Orders.FirstOrDefault(x => x.Ticket == tk); }
            if (kind == "DRAW" && dr != null)
            {
                l.Add(Mi.H(ChartView.ToolName(dr.Kind).ToLowerInvariant()));
                l.Add(Mi.Group("🎨", "Colour", ColorMenu(dr.Color, c => { dr.Color = c; if (dr.Kind != "FVG") SetDrawColor(c); SaveDrawings(); v.InvalidateVisual(); })));
                if (dr.Kind == "RECT" || dr.Kind == "FVG")
                {
                    var fl = new List<Mi> { Mi.H("the zone's background") }; string curF = dr.Fill ?? "";
                    fl.Add(new Mi { Text = "light (the line's colour)", Check = curF == "", Radio = true, Do = () => SetFill(v, dr, "") });
                    fl.Add(new Mi { Text = "strong (the line's colour)", Check = curF == "STRONG", Radio = true, Do = () => SetFill(v, dr, "STRONG") });
                    fl.Add(new Mi { Text = "no background", Check = curF == "NONE", Radio = true, Do = () => SetFill(v, dr, "NONE") });
                    fl.Add(Mi.S()); var names = new[] { "gold", "cyan", "violet", "mint", "red", "orange", "pink", "white" };
                    for (int i = 0; i < DrawColors.Length; i++) { var c = DrawColors[i]; fl.Add(new Mi { Text = names[i], Dot = Sx.B(c), Check = string.Equals(curF, c, StringComparison.OrdinalIgnoreCase), Radio = true, Do = () => SetFill(v, dr, c) }); }
                    fl.Add(new Mi("✏", "Own colour (#RRGGBB) …", () => { var s = Sx.Prompt(this, "🎨 Background", "a colour like #3A6EA5:", curF.StartsWith("#") ? curF : "#3A6EA5"); if (s == null) return; s = s.Trim(); if (!s.StartsWith("#")) s = "#" + s; try { Sx.C(s); SetFill(v, dr, s.ToUpperInvariant()); } catch { Toast("🎨 not a colour: " + s); } }));
                    l.Add(Mi.Group("▧", "Background", fl));
                }
                var wl = new List<Mi>(); foreach (var w in new[] { 1.0, 1.6, 2.4, 3.4 }) { double ww = w; wl.Add(new Mi { Text = w == 1 ? "thin" : w == 1.6 ? "normal" : w == 2.4 ? "bold" : "extra bold", Check = Math.Abs(dr.Width - w) < 0.01, Radio = true, Do = () => { dr.Width = ww; SaveDrawings(); v.InvalidateVisual(); } }); }
                if (dr.Kind != "TEXT") l.Add(Mi.Group("━", "Width", wl)); else l.Add(Mi.Group("A", "Size", wl));
                if (dr.Kind != "TEXT" && dr.Kind != "LONG" && dr.Kind != "SHORT") l.Add(new Mi { Text = "Dashed", Check = dr.Dash, Do = () => { dr.Dash = !dr.Dash; SaveDrawings(); v.InvalidateVisual(); } });
                if (dr.Kind == "TEXT" || dr.Kind == "HLINE") l.Add(new Mi("✏", dr.Kind == "TEXT" ? "Edit the text" : "Add / edit a label", () => { var s = Sx.Prompt(this, "✏ Text", "The text:", dr.Text); if (s != null) { dr.Text = s; SaveDrawings(); v.InvalidateVisual(); } }));
                if (trade && (dr.Kind == "HLINE" || dr.Kind == "HRAY") && v.Bid > 0) { bool below = dr.P1 < v.Bid; l.Add(Mi.S()); l.Add(new Mi("⏳", (below ? "BUY LIMIT" : "SELL LIMIT") + " at this line @ " + dr.P1.ToString(f, IC) + " …", () => PendingAsk(p.Sym, below ? "BUY LIMIT" : "SELL LIMIT", Math.Round(dr.P1 / v.Point) * v.Point))); }
                if (trade && (dr.Kind == "LONG" || dr.Kind == "SHORT") && v.Bid > 0) { l.Add(Mi.S()); l.Add(new Mi("⚡", "Place this " + (dr.Kind == "LONG" ? "long" : "short") + " as a limit / stop with its stop + target", () => PlaceFromTool(p, dr))); }
                l.Add(new Mi("⧉", "Duplicate", () => { var c = dr.Clone(); double sh = (v.Point > 0 ? v.Point : 0.01) * 50; c.P1 += sh; c.P2 += sh; c.P3 += sh; v.Drawings.Add(c); SaveDrawings(); v.InvalidateVisual(); }));
                l.Add(Mi.S()); l.Add(new Mi { Icon = "🗑", Text = "Delete (Del)", Danger = true, Do = () => { v.Drawings.Remove(dr); v.Deselect(); SaveDrawings(); } });
                SxMenu.Show(v, pt, l); return;
            }
            if ((kind == "POS" || kind == "SL" || kind == "TP") && pos != null)
            {
                l.Add(Mi.H("#" + pos.Ticket + " " + pos.Side + " " + pos.Lots.ToString("0.00", IC) + " @ " + pos.Open.ToString(f, IC) + "  " + Signed(pos.Profit + pos.Swap, broker.State.Account.Currency)));
                if (!trade) l.Add(new Mi { Icon = "🔒", Text = "ARM in the panel to manage it from the chart", Off = true });
                else
                {
                    var p0 = pos; l.Add(new Mi { Icon = "✕", Text = "Close this ticket", Danger = true, Do = () => v.CloseTicket(p0.Ticket, 0) });
                    if (pos.Lots >= 0.02) l.Add(new Mi("½", "Close half (" + Math.Round(pos.Lots / 2, 2).ToString("0.00", IC) + " lots)", () => Cmd("close half of #" + p0.Ticket, "CLOSE", p0.Ticket, Math.Round(p0.Lots / 2, 2))));
                    l.Add(new Mi("⚖", "Breakeven (+ " + beOff.Text + " " + (beUnitUsd ? "$" : "points") + ")", () => { Cmd("breakeven #" + p0.Ticket, "MODIFY", p0.Ticket, BePrice(p0, v), p0.Tp); }));
                    bool good = pos.Dir > 0 ? price < v.Bid : price > v.Ask;
                    l.Add(new Mi("🛑", "Stop here @ " + price.ToString(f, IC), () => Cmd("S/L on #" + p0.Ticket, "MODIFY", p0.Ticket, Math.Round(price / v.Point) * v.Point, p0.Tp)) { Off = !good });
                    l.Add(new Mi("🎯", "Target here @ " + price.ToString(f, IC), () => Cmd("T/P on #" + p0.Ticket, "MODIFY", p0.Ticket, p0.Sl, Math.Round(price / v.Point) * v.Point)) { Off = good });
                    if (pos.Sl > 0) l.Add(new Mi("⊘", "Remove the stop", () => Cmd("remove S/L #" + p0.Ticket, "MODIFY", p0.Ticket, 0.0, p0.Tp)));
                    if (pos.Tp > 0) l.Add(new Mi("⊘", "Remove the target", () => Cmd("remove T/P #" + p0.Ticket, "MODIFY", p0.Ticket, p0.Sl, 0.0)));
                    l.Add(Mi.S()); l.Add(new Mi("⇊", "Close all " + (pos.Dir > 0 ? "buys" : "sells") + " on " + p.Sym, () => CloseAll(p.Sym, p0.Dir > 0 ? "BUY" : "SELL")));
                }
                l.Add(Mi.S()); l.Add(new Mi(ticketMode == "CLEAN" ? "👁" : "🫥", ticketMode == "CLEAN" ? "Show the position lines" : "Hide the position lines (only a small ✕ per ticket)", () => ToggleLines())); l.Add(TicketsMenu());
                SxMenu.Show(v, pt, l); return;
            }
            if (kind == "ORD" && ord != null)
            {
                var o0 = ord; l.Add(Mi.H("#" + ord.Ticket + " " + ord.Type + " " + ord.Lots.ToString("0.00", IC) + " @ " + ord.Price.ToString(f, IC)));
                if (!trade) l.Add(new Mi { Icon = "🔒", Text = "ARM in the panel to manage it", Off = true });
                else
                {
                    double pr = Math.Round(price / v.Point) * v.Point; int od = o0.Type.StartsWith("BUY") ? 1 : -1;
                    l.Add(new Mi("↕", "Move the order here @ " + price.ToString(f, IC), () => { double d = pr - o0.Price; Cmd("order #" + o0.Ticket + " → " + price.ToString(f, IC), "MODORD", o0.Ticket, pr, o0.Sl > 0 ? o0.Sl + d : 0.0, o0.Tp > 0 ? o0.Tp + d : 0.0); }));
                    l.Add(new Mi("🛑", "Its stop here @ " + price.ToString(f, IC), () => Cmd("order #" + o0.Ticket + " S/L", "MODORD", o0.Ticket, o0.Price, pr, o0.Tp)) { Off = od > 0 ? pr >= o0.Price : pr <= o0.Price });
                    l.Add(new Mi("🎯", "Its target here @ " + price.ToString(f, IC), () => Cmd("order #" + o0.Ticket + " T/P", "MODORD", o0.Ticket, o0.Price, o0.Sl, pr)) { Off = od > 0 ? pr <= o0.Price : pr >= o0.Price });
                    if (o0.Sl > 0) l.Add(new Mi("⊘", "Remove its stop", () => Cmd("order #" + o0.Ticket + " no S/L", "MODORD", o0.Ticket, o0.Price, 0.0, o0.Tp)));
                    if (o0.Tp > 0) l.Add(new Mi("⊘", "Remove its target", () => Cmd("order #" + o0.Ticket + " no T/P", "MODORD", o0.Ticket, o0.Price, o0.Sl, 0.0)));
                    l.Add(new Mi { Icon = "✕", Text = "Cancel the order", Danger = true, Do = () => v.CancelOrder(o0.Ticket) });
                }
                SxMenu.Show(v, pt, l); return;
            }
            // the chart itself
            l.Add(Mi.H(p.Sym + " • " + Tf.Name(p.Tf) + " • " + price.ToString(f, IC)));
            if (trade && v.Bid > 0)
            {
                bool below = price < v.Bid; double px = Math.Round(price / v.Point) * v.Point;
                l.Add(new Mi { Icon = below ? "▲" : "▼", Fg = below ? Green : Red, Text = (below ? "BUY LIMIT" : "SELL LIMIT") + " here @ " + px.ToString(f, IC) + " …", Do = () => PendingAsk(p.Sym, below ? "BUY LIMIT" : "SELL LIMIT", px) });
                l.Add(new Mi { Icon = below ? "▼" : "▲", Fg = below ? Red : Green, Text = (below ? "SELL STOP" : "BUY STOP") + " here @ " + px.ToString(f, IC) + " …", Do = () => PendingAsk(p.Sym, below ? "SELL STOP" : "BUY STOP", px) });
                l.Add(Mi.S());
                // this price as the stop or the target of every sell / buy of this symbol at once (asks first)
                var bulk = LevelForAll(p.Sym, px, v); if (bulk.Count > 0) { l.AddRange(bulk); l.Add(Mi.S()); }
                var esc = EscapeMenu(p.Sym, px, v); if (esc.Count > 0) { l.AddRange(esc); l.Add(Mi.S()); }
            }
            var tfl = new List<Mi>(); foreach (var m in Tf.All) { int mm = m; tfl.Add(new Mi { Text = Tf.Name(m), Check = m == p.Tf, Radio = true, Do = () => SetPane(p, p.Sym, mm) }); } l.Add(Mi.Group("⏱", "Timeframe", tfl));
            var drl = new List<Mi> { Mi.H("drawing tools") }; foreach (var t in toolBtns.Where(x => x.Item1 != "")) { var key = t.Item1; drl.Add(new Mi(t.Item2.Content as string, ChartView.ToolName(key).Substring(0, 1) + ChartView.ToolName(key).Substring(1).ToLowerInvariant(), () => { v.Tool = key; v.Focus(); PaintRail(); v.InvalidateVisual(); })); }
            drl.Add(Mi.S()); drl.Add(new Mi { Text = "Magnet (snap to the candles)", Check = magnet, Do = () => { magnet = !magnet; Put("magnet", magnet ? "1" : "0"); foreach (var q in panes) q.V.Magnet = magnet; PaintRail(); } });
            drl.Add(Mi.Group("●", "Colour of new drawings", ColorMenu(drawColor, c => SetDrawColor(c))));
            drl.Add(new Mi { Icon = "🗑", Text = "Remove all drawings on " + p.Sym, Danger = true, Do = () => { if (v.Drawings.Count > 0 && Sx.Ask(this, "Remove the drawings", "Remove every drawing on " + p.Sym + "?", "REMOVE", "KEEP", Red)) { v.Drawings.Clear(); SaveDrawings(); v.InvalidateVisual(); } } });
            l.Add(Mi.Group("✏", "Draw", drl));
            l.Add(new Mi { Icon = "🧹", Text = "Clean chart: remove every drawing + indicator of " + p.Sym, Hint = "at once", Do = () => CleanChart(p) });
            Tuple<List<Drawing>, List<string>> undo; if (cleanUndo.TryGetValue(p.Sym, out undo)) l.Add(new Mi { Icon = "↶", Text = "Undo the clean (" + undo.Item1.Count + " drawings, " + undo.Item2.Count + " indicators)", Do = () => UndoClean(p.Sym) });
            l.Add(Mi.Group("📈", "Indicators", IndMenu(v)));
            if (v.Ind.Count > 0) l.Add(Mi.Group("✕", "Remove an indicator", ChartView.Indicators.Where(x => v.Ind.Contains(x.Key)).Select(x => { var kk = x.Key; return new Mi { Text = x.Name, Dot = Sx.B(x.Color), Do = () => IndOff(kk) }; }).ToList()));
            l.Add(new Mi("✨", "Looks … (preview every theme on the whole platform)", () => LooksGallery())); l.Add(new Mi("⚙", "Chart settings … (colours, candles, entries)", () => ChartSettings())); l.Add(Mi.Group("🎨", "Chart theme", ThemeMenu())); l.Add(Mi.Group("🕘", "Time zone", ZoneMenu()));
            l.Add(new Mi(ticketMode == "CLEAN" ? "👁" : "🫥", ticketMode == "CLEAN" ? "Show the position lines" : "Hide the position lines (only a small ✕ per ticket)", () => ToggleLines())); l.Add(TicketsMenu());
            var spl = new List<Mi>(); foreach (var n in new[] { 1, 2, 4 }) { int nn = n; spl.Add(new Mi { Text = n + (n == 1 ? " chart" : " charts"), Check = split == n, Radio = true, Do = () => SetSplit(nn) }); } l.Add(Mi.Group("⊞", "Split screen", spl));
            l.Add(Mi.S());
            l.Add(new Mi("⟲", "Back to now • automatic scale", () => v.ResetView()) { Hint = "double-click" });
            if (!Sim) l.Add(new Mi("⏪", "Load 30 more days of history", () => MoreHistory(p)));
            l.Add(new Mi("⛶", fullChart ? "Exit full screen" : "Full screen chart", () => SetFullChart(!fullChart)) { Hint = fullChart ? "Esc" : "F11" });
            l.Add(new Mi("◧", panelHost.Visibility == Visibility.Visible ? "Hide the trade panel" : "Show the trade panel", () => { panelHost.Visibility = panelHost.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; Put("panel", panelHost.Visibility == Visibility.Visible ? "1" : "0"); }));
            SxMenu.Show(v, pt, l);
        }
        // a limit / stop from the right-click: it asks the lots (and shows the stop / target the panel will add), then places it
        void PendingAsk(string sym, string kind, double price)
        {
            var v = Act.V; string f = "F" + v.Digits; int dir = kind.StartsWith("BUY") ? 1 : -1; double sd = Num(slBox), td = Num(tpBox);
            var box = new TextBox { Text = Num(lotsBox).ToString("0.00", IC), FontSize = 18, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono, Height = 38, Background = Bg, Foreground = Text, BorderBrush = dir > 0 ? Green : Red, CaretBrush = Gold, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 10, 0, 0), MinWidth = 160 };
            string txt = kind + " " + sym + " @ " + price.ToString(f, IC) + (sd > 0 ? "\nS/L " + (price - dir * sd).ToString(f, IC) : "") + (td > 0 ? "\nT/P " + (price + dir * td).ToString(f, IC) : "") + "\n\nHow many lots?";
            if (!Sx.Form(this, "⏳ " + kind, txt, box, "PLACE ORDER", "CANCEL", dir > 0 ? Green : Red)) return;
            double lots; if (!double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, IC, out lots) || lots <= 0) { Toast("the lots must be a number above 0"); return; }
            lotsBox.Text = lots.ToString("0.00", IC); Put("lots", lotsBox.Text); PlacePending(sym, kind, price, lots);
        }
        // 👁 the position lines on / off: CLEAN hides them (a small ✕ per ticket stays on the right), again = back to the labels
        string ticketBeforeClean = "AUTO"; readonly List<Button> linesBtns = new List<Button>();
        void SetTicketMode(string key)
        {
            if (key == "CLEAN" && ticketMode != "CLEAN") ticketBeforeClean = ticketMode;
            ticketMode = key; Put("tickets", key); foreach (var p in panes) { p.V.TicketMode = key; p.V.InvalidateVisual(); }
            foreach (var b in linesBtns) { b.Content = key == "CLEAN" ? "👁 SHOW LINES" : "🫥 HIDE LINES"; b.Background = key == "CLEAN" ? Tint(Cyan, 90) : (Brush)Card; }
        }
        void ToggleLines() { SetTicketMode(ticketMode == "CLEAN" ? (ticketBeforeClean == "CLEAN" ? "AUTO" : ticketBeforeClean) : "CLEAN"); }
        Button LinesBtn(double h)
        {
            var b = Pill("", Card); if (h > 0) { b.Height = h; b.FontSize = 11.5; b.Padding = new Thickness(9, 0, 9, 0); b.Margin = new Thickness(2, 0, 2, 0); }
            b.ToolTip = "the position lines on the chart: hide them (only a small ✕ per ticket stays on the right, point at it for its card) / show them again"; b.Click += delegate { ToggleLines(); }; linesBtns.Add(b);
            b.Content = ticketMode == "CLEAN" ? "👁 SHOW LINES" : "🫥 HIDE LINES"; if (ticketMode == "CLEAN") b.Background = Tint(Cyan, 90); return b;
        }
        Mi TicketsMenu()
        {
            var l = new List<Mi>();
            foreach (var m in new[] { Tuple.Create("AUTO", "a label for every ticket (lines only above 10 on a side)"), Tuple.Create("EACH", "a label for every ticket, always"), Tuple.Create("LINES", "lines only (point at one for its card)"), Tuple.Create("CLEAN", "clean: no lines, only a small ✕ per ticket on the right") })
            { var key = m.Item1; l.Add(new Mi { Text = m.Item2, Check = ticketMode == key, Radio = true, Do = () => SetTicketMode(key) }); }
            return Mi.Group("🎟", "Tickets on the chart", l);
        }
        // the long / short drawing → a limit or stop order with its stop and target (asks first)
        void PlaceFromTool(Pane p, Drawing d)
        {
            var v = p.V; int dir = d.Kind == "LONG" ? 1 : -1; double px = Math.Round(d.P1 / v.Point) * v.Point, sl = Math.Round(d.P2 / v.Point) * v.Point, tp = Math.Round(d.P3 / v.Point) * v.Point;
            string kind = dir > 0 ? (px < v.Ask ? "BUY LIMIT" : "BUY STOP") : (px > v.Bid ? "SELL LIMIT" : "SELL STOP"); double lots = Num(lotsBox); string f = "F" + v.Digits;
            string what = kind + " " + lots.ToString("0.00", IC) + " " + p.Sym + " @ " + px.ToString(f, IC) + " • S/L " + sl.ToString(f, IC) + " • T/P " + tp.ToString(f, IC);
            if (!Sx.Ask(this, "⚡ Place the " + (dir > 0 ? "long" : "short"), what + "?", "PLACE", "NO", dir > 0 ? Green : Red, kind + " " + lots.ToString("0.00", IC) + " LOTS")) return;
            Cmd(what, "ORDER", p.Sym, kind, lots, px, sl, tp);
        }

        // ---------------------------------------------------------------- drawings, saved per symbol
        List<Drawing> DrawingsOf(string sym) { List<Drawing> l; if (!drawingsBySym.TryGetValue(sym, out l)) drawingsBySym[sym] = l = new List<Drawing>(); return l; }
        string DrawFileNow { get { return Sim ? DrawFile.Replace("drawings.txt", "drawings-backtest.txt") : Paper ? DrawFile.Replace("drawings.txt", "drawings-demo.txt") : DrawFile; } }   // every mode its own drawings
        string ModeKey { get { return Sim ? "bt." : Paper ? "demo." : ""; } }   // every mode its own indicators (none by default)
        void LoadDrawings()
        {
            try
            {
                if (!File.Exists(DrawFileNow)) return;
                foreach (var ln in File.ReadAllLines(DrawFileNow))
                {
                    var a = ln.Split('|'); if (a.Length < 7) continue; long t1, t2; double p1, p2;
                    if (!long.TryParse(a[2], out t1) || !long.TryParse(a[4], out t2) || !double.TryParse(a[3], NumberStyles.Float, IC, out p1) || !double.TryParse(a[5], NumberStyles.Float, IC, out p2)) continue;
                    var d = new Drawing { Kind = a[1], T1 = new DateTime(t1), P1 = p1, T2 = new DateTime(t2), P2 = p2, Color = a[6] };
                    double w, p3; if (a.Length > 7 && double.TryParse(a[7], NumberStyles.Float, IC, out w)) d.Width = w; if (a.Length > 8) d.Dash = a[8] == "1"; if (a.Length > 9 && double.TryParse(a[9], NumberStyles.Float, IC, out p3)) d.P3 = p3; if (a.Length > 10) d.Text = a[10].Replace("¦", "|"); if (a.Length > 11) d.Fill = a[11];
                    DrawingsOf(a[0]).Add(d);
                }
            }
            catch { }
        }
        void SaveDrawings()
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllLines(DrawFileNow, drawingsBySym.SelectMany(kv => kv.Value.Select(d => kv.Key + "|" + d.Kind + "|" + d.T1.Ticks + "|" + d.P1.ToString("R", IC) + "|" + d.T2.Ticks + "|" + d.P2.ToString("R", IC) + "|" + d.Color + "|" + d.Width.ToString("R", IC) + "|" + (d.Dash ? "1" : "0") + "|" + d.P3.ToString("R", IC) + "|" + (d.Text ?? "").Replace("|", "¦") + "|" + (d.Fill ?? "")))); } catch { }
        }

        // ---------------------------------------------------------------- + an instrument
        void PickSymbol()
        {
            broker.Send("ALLSYMS");
            var w = new Window { Title = "SpasiX • add an instrument", Width = 440, Height = 580, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Bg, Foreground = Text, FontFamily = Sx.Face };
            Sx.DarkTitle(w);
            var sp = new DockPanel { Margin = new Thickness(14) }; var box = new TextBox { FontSize = 15, Height = 34, Margin = new Thickness(0, 0, 0, 8), Background = Card, Foreground = Text, BorderBrush = Gold, CaretBrush = Gold, Padding = new Thickness(6, 4, 6, 4) };
            DockPanel.SetDock(box, Dock.Top); sp.Children.Add(box);
            var hint = new TextBlock { Text = "type to search • double-click (or Enter) = add it and open its chart", Foreground = Muted, FontSize = 11.5, Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(hint, Dock.Top); sp.Children.Add(hint);
            var list = new ListBox { Background = Card, Foreground = Text, BorderBrush = Line, FontSize = 14 }; sp.Children.Add(list); w.Content = sp;
            Action fill = () => { List<string> all; lock (broker.State.Gate) all = broker.State.AllSymbols.Count > 0 ? broker.State.AllSymbols.ToList() : broker.State.Quotes.Keys.ToList(); var q = box.Text.Trim(); list.Items.Clear(); foreach (var x in all.Where(x => q == "" || x.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(x => q != "" && !x.StartsWith(q, StringComparison.OrdinalIgnoreCase)).Take(400)) list.Items.Add(x); if (list.Items.Count > 0) list.SelectedIndex = 0; };
            box.TextChanged += delegate { fill(); }; var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) }; int tries = 0; tm.Tick += delegate { fill(); if (++tries > 6) tm.Stop(); }; tm.Start(); fill();
            Action add = () =>
            {
                var sy = list.SelectedItem as string; if (sy == null) return; broker.Send("WATCH|" + sy);
                var added = Get("watch", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); if (!added.Contains(sy)) { added.Add(sy); Put("watch", string.Join(",", added)); } var hid = SymList("symhide"); if (hid.RemoveAll(x => string.Equals(x, sy, StringComparison.OrdinalIgnoreCase)) > 0) { Put("symhide", string.Join(",", hid)); symDragSig = "show:" + sy; symSig = ""; }
                SetPane(Act, sy, Act.Tf); w.Close();
            };
            list.MouseDoubleClick += delegate { add(); }; box.KeyDown += (s, e) => { if (e.Key == Key.Enter) add(); else if (e.Key == Key.Down && list.Items.Count > 0) { list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1); list.ScrollIntoView(list.SelectedItem); } else if (e.Key == Key.Up && list.SelectedIndex > 0) { list.SelectedIndex--; list.ScrollIntoView(list.SelectedItem); } };
            w.Closed += delegate { tm.Stop(); }; box.Focus(); w.ShowDialog();
        }

        // ---------------------------------------------------------------- the TRADE PANEL
        Border BuildTradePanel()
        {
            var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 10) };
            Func<SolidColorBrush, Border> card = edge => new Border { Background = Card, BorderBrush = Tint(edge, 110), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 8, 11, 10), Margin = new Thickness(0, 0, 0, 8) };
            Func<string, TextBlock> cap = t => new TextBlock { Text = t, Foreground = Muted, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2) };
            // ARM (live / prop) or the simulated account (backtest)
            var a = new StackPanel(); armCard = card(Virtual ? Cyan : Gold); armCard.Child = a; sp.Children.Add(armCard);
            armText = new TextBlock { Foreground = Muted, FontSize = 11.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) }; a.Children.Add(armText);
            armChecks = new StackPanel { Margin = new Thickness(0, 0, 0, 6) }; a.Children.Add(armChecks);
            armBtn = Pill("🔓 ARM TRADING", Tint(Orange, 210)); armBtn.Height = 36; armBtn.Margin = new Thickness(0); armBtn.Foreground = Bg; armBtn.Click += delegate { SetArmed(!armed, null); }; if (!Virtual) a.Children.Add(armBtn);
            else if (Paper) { var db = Pill("🎮 DEMO ACCOUNT • NEW / RESET", Tint(Orange, 200)); db.Height = 32; db.Margin = new Thickness(0); db.Foreground = Bg; db.Click += delegate { DemoSetup(false); }; a.Children.Add(db); }
            // the symbol + exposure, lots, stop / target, SELL / BUY
            var t = new StackPanel(); var tc = card(Cyan); tc.Child = t; sp.Children.Add(tc);
            panelSym = new TextBlock { Foreground = Cyan, FontSize = 16, FontWeight = FontWeights.Bold }; t.Children.Add(panelSym);
            panelExpo = new TextBlock { Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) }; t.Children.Add(panelExpo);
            t.Children.Add(cap("LOTS"));
            var lr = new Grid(); lr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); lr.ColumnDefinitions.Add(new ColumnDefinition()); lr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            var lm = Pill("−", Card); lm.Margin = new Thickness(0); lotsBox = Sx.Input(Get("lots", "0.10"), double.NaN); lotsBox.TextAlignment = TextAlignment.Center; lotsBox.Margin = new Thickness(4, 0, 4, 0); var lp = Pill("+", Card); lp.Margin = new Thickness(0);
            Grid.SetColumn(lotsBox, 1); Grid.SetColumn(lp, 2); lr.Children.Add(lm); lr.Children.Add(lotsBox); lr.Children.Add(lp); t.Children.Add(lr);
            lm.Click += delegate { StepLots(-1); }; lp.Click += delegate { StepLots(1); }; lotsBox.LostFocus += delegate { Put("lots", lotsBox.Text); }; lotsBox.TextChanged += delegate { PaintRisk(); };
            var pr = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Margin = new Thickness(0, 5, 0, 0) }; foreach (var v in new[] { "0.01", "0.05", "0.10", "0.50", "1.00" }) { var vv = v; var b = Pill(v, Card); b.Height = 24; b.FontSize = 10.5; b.Padding = new Thickness(2, 0, 2, 0); b.Margin = new Thickness(0, 0, 3, 0); b.Click += delegate { lotsBox.Text = vv; Put("lots", vv); }; pr.Children.Add(b); } t.Children.Add(pr);
            var st = new Grid { Margin = new Thickness(0, 2, 0, 0) }; st.ColumnDefinitions.Add(new ColumnDefinition()); st.ColumnDefinitions.Add(new ColumnDefinition());
            var s1 = new StackPanel(); s1.Children.Add(cap("STOP (distance)")); slBox = Sx.Input(Get("sl", "0"), double.NaN); s1.Children.Add(slBox);
            var s2 = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; s2.Children.Add(cap("TARGET (distance)")); tpBox = Sx.Input(Get("tp", "0"), double.NaN); s2.Children.Add(tpBox); Grid.SetColumn(s2, 1);
            st.Children.Add(s1); st.Children.Add(s2); t.Children.Add(st); slBox.LostFocus += delegate { Put("sl", slBox.Text); }; tpBox.LostFocus += delegate { Put("tp", tpBox.Text); }; slBox.TextChanged += delegate { PaintRisk(); }; tpBox.TextChanged += delegate { PaintRisk(); };
            riskText = new TextBlock { Text = "0 = none • gold: 5 = a $5 move • drag the lines later", Foreground = Muted, FontSize = 10, Margin = new Thickness(0, 3, 0, 6), TextWrapping = TextWrapping.Wrap }; t.Children.Add(riskText);
            var bs = new Grid { Margin = new Thickness(0, 2, 0, 4) }; bs.ColumnDefinitions.Add(new ColumnDefinition()); bs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bs.ColumnDefinitions.Add(new ColumnDefinition());
            sellBtn = Pill("SELL", Red); sellBtn.Height = 56; sellBtn.FontSize = 14; sellBtn.Margin = new Thickness(0); sellBtn.Foreground = Sx.White;
            buyBtn = Pill("BUY", Green); buyBtn.Height = 56; buyBtn.FontSize = 14; buyBtn.Foreground = Bg; buyBtn.Margin = new Thickness(0);
            spreadText = new TextBlock { Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0), TextAlignment = TextAlignment.Center, Width = 34, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(spreadText, 1); Grid.SetColumn(buyBtn, 2); bs.Children.Add(sellBtn); bs.Children.Add(spreadText); bs.Children.Add(buyBtn); t.Children.Add(bs);
            sellBtn.Click += delegate { Market(-1); }; buyBtn.Click += delegate { Market(1); };
            // (✕ SELLS / ✕ ALL / ✕ BUYS moved to the chart toolbar, user's request: CloseBtns)
            t.Children.Add(new TextBlock { Text = "orders go in at once • the ✕ on the chart closes at once • ✕ SELLS / ALL / BUYS are on the chart toolbar (they ask)", Foreground = Muted, FontSize = 9.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            // pending
            var pc = new StackPanel(); var pcc = card(Amber); pcc.Child = pc; sp.Children.Add(pcc);
            pc.Children.Add(new TextBlock { Text = "⏳ LIMIT / STOP ORDER", Foreground = Amber, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            var pg = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            foreach (var k in new[] { "BUY LIMIT", "SELL LIMIT", "BUY STOP", "SELL STOP" }) { var kk = k; var b = Pill(k, Card); b.Height = 26; b.FontSize = 10.5; b.Margin = new Thickness(0, 0, 4, 4); b.Click += delegate { pendKind = kk; PaintPend(); }; pg.Children.Add(b); pendBtns.Add(Tuple.Create(k, b)); }
            pc.Children.Add(pg);
            var ppr = new Grid(); ppr.ColumnDefinitions.Add(new ColumnDefinition()); ppr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pendPrice = Sx.Input("", double.NaN); var pick = Pill("🎯 PICK", Card); pick.Margin = new Thickness(6, 0, 0, 0); pick.ToolTip = "click the price on the chart"; Grid.SetColumn(pick, 1); ppr.Children.Add(pendPrice); ppr.Children.Add(pick);
            pick.Click += delegate { var v = Act.V; v.PickKind = pendKind; v.PickPrice = double.NaN; Toast("click the price on the chart for the " + v.PickKind); v.Focus(); };
            pc.Children.Add(ppr);
            placeBtn = Pill("PLACE BUY LIMIT", Tint(Amber, 220)); placeBtn.Margin = new Thickness(0, 6, 0, 0); placeBtn.Height = 32; placeBtn.Foreground = Bg; placeBtn.Click += delegate { double px; if (!double.TryParse(pendPrice.Text.Replace(',', '.'), NumberStyles.Float, IC, out px) || px <= 0) { Toast("type or 🎯 pick a price first"); return; } PlacePending(Act.Sym, pendKind, px); }; pc.Children.Add(placeBtn);
            PaintPend();
            // manage
            var mc = new StackPanel(); var mcc = card(Violet); mcc.Child = mc; sp.Children.Add(mcc);
            manageTitle = new TextBlock { Text = "🧹 MANAGE THIS SYMBOL", Foreground = Violet, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5), TextWrapping = TextWrapping.Wrap }; mc.Children.Add(manageTitle);
            var mg = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 }; mc.Children.Add(mg);
            foreach (var m in new[] { Tuple.Create("ALL", "ALL", Red, "close every ticket of this symbol"), Tuple.Create("BUYS", "BUY", Green, "close the buys"), Tuple.Create("SELLS", "SELL", Red, "close the sells"), Tuple.Create("WINNERS", "PROFIT", Green, "close the tickets in profit"), Tuple.Create("LOSERS", "LOSS", Red, "close the tickets in loss") })
            { var which = m.Item2; var b = Pill(m.Item1, Card); b.Foreground = m.Item3; b.Height = 28; b.FontSize = 10.5; b.Padding = new Thickness(2, 0, 2, 0); b.Margin = new Thickness(0, 0, 4, 4); b.ToolTip = m.Item4; b.Click += delegate { CloseAll(Act.Sym, which); }; mg.Children.Add(b); }
            var cancel = Pill("ORDERS", Card); cancel.Foreground = Amber; cancel.Height = 28; cancel.FontSize = 10.5; cancel.Padding = new Thickness(2, 0, 2, 0); cancel.Margin = new Thickness(0, 0, 4, 4); cancel.ToolTip = "cancel the pending orders of this symbol"; cancel.Click += delegate { if (Ask("Cancel every pending order on " + Act.Sym + "?")) Cmd("cancel the orders on " + Act.Sym, "CANCELALL", Act.Sym); }; mg.Children.Add(cancel);
            var beC = Pill("⚖ BE", Card); beC.Foreground = Gold; beC.Height = 28; beC.FontSize = 10.5; beC.Padding = new Thickness(2, 0, 2, 0); beC.Margin = new Thickness(0, 0, 4, 4); beC.ToolTip = "breakeven: the stop of this symbol's tickets in profit to their entry"; beC.Click += delegate { BreakevenAll(Act.Sym); }; mg.Children.Add(beC);
            mc.Children.Add(new TextBlock { Text = "only this chart's instrument • each asks first • every instrument at once: ⛔ CLOSE EVERYTHING in POSITIONS", Foreground = Muted, FontSize = 9.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(BuildProtect(card));
            // the answers
            var lc = new StackPanel(); var lcc = card(Line); lcc.Child = lc; sp.Children.Add(lcc);
            lc.Children.Add(new TextBlock { Text = Sim ? "🧾 BACKTEST ANSWERS" : Paper ? "🧾 DEMO ANSWERS" : "🧾 MT5 ANSWERS", Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.Bold });
            logPanel = new StackPanel(); lc.Children.Add(logPanel);
            return new Border { Width = 318, Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(6, 2, 0, 2), Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp } };
        }
        void PaintPend()
        {
            foreach (var t in pendBtns) { bool on = t.Item1 == pendKind; var col = t.Item1.StartsWith("BUY") ? Green : Red; t.Item2.Background = on ? Tint(col, 200) : (Brush)Card; t.Item2.Foreground = on ? Bg : col; t.Item2.BorderBrush = on ? col : Line; }
            if (placeBtn != null) placeBtn.Content = "PLACE " + pendKind;
        }
        void PaintRisk()
        {
            if (riskText == null || panes.Count == 0) return; double lots = Num(lotsBox), sd = Num(slBox), td = Num(tpBox); var v = Act.V;
            if (sd <= 0 && td <= 0) { riskText.Text = "0 = none • gold: 5 = a $5 move • drag the lines later"; riskText.Foreground = Muted; return; }
            double pv = v.TickSize > 0 ? v.TickValue / v.TickSize : 0;
            riskText.Text = (sd > 0 ? "risk ≈ " + Money(sd * pv * lots, "USD") : "no stop") + "  •  " + (td > 0 ? "reward ≈ " + Money(td * pv * lots, "USD") : "no target") + (sd > 0 && td > 0 ? "  •  " + (td / sd).ToString("0.0#", IC) + " R" : "") + "  (" + lots.ToString("0.00", IC) + " lots)";
            riskText.Foreground = Text;
        }
        void StepLots(int dir)
        {
            double v; double.TryParse(lotsBox.Text.Replace(',', '.'), NumberStyles.Float, IC, out v); double step = 0.01;
            lock (broker.State.Gate) { Mt5Symbol sy; if (broker.State.Symbols.TryGetValue(Act.Sym, out sy) && sy.VolStep > 0) step = sy.VolStep; }
            v = Math.Max(step, Math.Round((v + dir * step) / step) * step); lotsBox.Text = v.ToString("0.00", IC); Put("lots", lotsBox.Text);
        }
        void FillPanel(Mt5State s)
        {
            var a = s.Account; var p = Act; Mt5Quote q; s.Quotes.TryGetValue(p.Sym, out q); string f = "F" + p.V.Digits;
            panelSym.Text = p.Sym + (q != null ? "   " + q.Bid.ToString(f, IC) : "");
            var ps = s.Positions.Where(x => string.Equals(x.Symbol, p.Sym, StringComparison.OrdinalIgnoreCase)).ToList();
            double bl = ps.Where(x => x.Dir > 0).Sum(x => x.Lots), sl = ps.Where(x => x.Dir < 0).Sum(x => x.Lots), pl = ps.Sum(x => x.Profit + x.Swap);
            panelExpo.Text = ps.Count == 0 ? "no position on " + p.Sym : "▲ " + bl.ToString("0.00", IC) + " lots bought • ▼ " + sl.ToString("0.00", IC) + " sold • net " + (bl - sl).ToString("+0.00;−0.00;0.00", IC) + " • " + Signed(pl, a.Currency);
            panelExpo.Foreground = ps.Count == 0 ? Muted : pl >= 0 ? Green : Red;
            sellBtn.Content = "SELL\n" + (q != null ? q.Bid.ToString(f, IC) : ""); buyBtn.Content = "BUY\n" + (q != null ? q.Ask.ToString(f, IC) : "");
            if (fsSell != null && fullStrip.Visibility == Visibility.Visible) { fsSell.Content = "SELL " + (q != null ? q.Bid.ToString(f, IC) : ""); fsBuy.Content = "BUY " + (q != null ? q.Ask.ToString(f, IC) : ""); fsPl.Text = ps.Count == 0 ? "" : Signed(pl, a.Currency); fsPl.Foreground = pl >= 0 ? Green : Red; }
            spreadText.Text = q != null ? q.Spread(p.V.Point).ToString("0", IC) + "\npts" : "";
            if (Virtual)
            {
                var vs = Sim ? (sim.Set != null ? sim.Set.Account() : null) : paper.Ready ? paper.Acct.Set : null;
                armText.Text = (Sim ? "🧪 BACKTEST ACCOUNT" : paper.Ready ? "🎮 DEMO ACCOUNT on live prices" : "🎮 DEMO — press the button below to set up your account") + (vs != null ? " • " + vs.Kind + (vs.Kind != "RAW" ? " $" + vs.CommissionPerLot.ToString("0.##", IC) + " / lot" : " +" + vs.MarkupPts.ToString("0", IC) + " pts spread") + " • 1:" + vs.Leverage : "") + " • hedging — nothing reaches MT5"; armText.Foreground = Cyan;
                if (armChecks.Children.Count > 0) armChecks.Children.Clear();
                foreach (var b in new[] { buyBtn, sellBtn }) b.Opacity = 1; return;
            }
            var checks = new[] {
                Tuple.Create(s.BridgeVersion >= 3, "bridge v" + s.BridgeVersion + (s.BridgeVersion >= 5 ? "" : " — put the new SpasiXBridge.mq5 (v5) in MT5 (⚙ SETUP)")),
                Tuple.Create(s.AllowInput, "EnableTrading = true (the bridge's Inputs)"),
                Tuple.Create(s.AlgoTerminal, "Algo Trading ON (the MT5 toolbar button)"),
                Tuple.Create(s.AlgoEa, "'Allow Algo Trading' ticked (the bridge's Common tab)"),
                Tuple.Create(s.AccountTrade && s.AccountExpert, "the account allows trading by experts") };
            string why = checks.Where(c => !c.Item1).Select(c => c.Item2).FirstOrDefault() ?? "";
            if (!s.CanKnown && s.BridgeVersion >= 3) why = "waiting for the bridge's switches …";
            if (why != "" && armed) SetArmed(false, why);
            string sig = string.Join("", checks.Select(c => c.Item1 ? "1" : "0")) + armed;
            if ((armChecks.Tag as string) != sig)
            {
                armChecks.Tag = sig; armChecks.Children.Clear();
                if (why != "" || !armed) foreach (var c in checks) armChecks.Children.Add(new TextBlock { Text = (c.Item1 ? "✓  " : "✖  ") + c.Item2, Foreground = c.Item1 ? Green : Red, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) });
            }
            armText.Text = why != "" ? "🔒 READ ONLY — fix the red line" + (checks.Count(c => !c.Item1) > 1 ? "s" : "") + " below" : armed ? "🟢 ARMED • " + a.Kind + " account • max " + s.MaxLots.ToString("0.##", IC) + " lots / order (the bridge's limit)" : "🔓 READY — press ARM to trade from SpasiX (" + a.Kind + " account)";
            armText.Foreground = why != "" ? Gold : armed ? Green : Text;
            armBtn.IsEnabled = why == "" || armed; armBtn.Content = armed ? "🔒 DISARM" : "🔓 ARM TRADING"; armBtn.Background = armed ? Tint(Green, 210) : (Brush)Tint(Orange, 210);
            armCard.BorderBrush = armed ? Tint(Green, 200) : why != "" ? Tint(Gold, 110) : Tint(Orange, 200);
            foreach (var b in new[] { buyBtn, sellBtn }) b.Opacity = armed ? 1 : 0.5;
        }
        void SetArmed(bool on, string why)
        {
            if (on)
            {
            }
            armed = on; Toast(on ? "🟢 ARMED with one click — BUY / SELL now go to MT5 at once (DISARM to stop)" : "🔒 DISARMED" + (why != null ? " — " + why : ""));
        }
        bool Ask(string msg) { return Sx.Ask(this, "SpasiX", msg); }
        // the right-click price as S/L or T/P for a whole side of one symbol: a stop above the ask for the sells / under the bid for the buys
        // (the ones opened beyond it lock a profit: breakeven or better), a target on the other side; MT5 refuses a level on the wrong side
        // ⛑ ESCAPE LEVEL: when price has run beyond EVERY entry of an instrument, a level between the price and the entries (a price it already passed):
        // if price comes back to it, every position of that instrument closes by itself (asks once when it is set; needs ARM, like the basket)
        readonly Dictionary<string, Tuple<double, int>> escapes = new Dictionary<string, Tuple<double, int>>(StringComparer.OrdinalIgnoreCase);
        List<Mi> EscapeMenu(string sym, double px, ChartView v)
        {
            var l = new List<Mi>(); List<Mt5Position> ps; double tv = 1, ts = v.Point > 0 ? v.Point : 0.01; string f = "F" + v.Digits;
            lock (broker.State.Gate) { ps = broker.State.Positions.Where(x => string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); Mt5Symbol sy; if (broker.State.Symbols.TryGetValue(sym, out sy)) { tv = sy.TickValue > 0 ? sy.TickValue : 1; ts = sy.TickSize > 0 ? sy.TickSize : ts; } }
            Tuple<double, int> cur; if (escapes.TryGetValue(sym, out cur)) l.Add(new Mi { Icon = "⛑", Fg = Gold, Text = "Remove the escape @ " + cur.Item1.ToString(f, IC), Do = () => { escapes.Remove(sym); Toast("⛑ escape on " + sym + " removed"); } });
            if (ps.Count == 0 || v.Bid <= 0) return l;
            double hi = ps.Max(x => x.Open), lo = ps.Min(x => x.Open);
            bool down = v.Bid < lo && px > v.Ask && px < lo, up = v.Ask > hi && px < v.Bid && px > hi;   // price ran below every entry (the level is above it) / above every entry (the level below it)
            if (down || up)
            {
                double pl = ps.Sum(x => (px - x.Open) * x.Dir / ts * tv * x.Lots + x.Swap);
                l.Add(new Mi { Icon = "⛑", Fg = Gold, Text = "Escape @ " + px.ToString(f, IC) + ": close all " + ps.Count + " " + sym + " if the price comes back here …", Hint = "≈ " + Signed(pl, "USD"), Do = () => EscapeAsk(sym, px, down ? 1 : -1, ps.Count, pl, f) });
            }
            else l.Add(new Mi { Icon = "⛑", Off = true, Text = "Escape level: right-click a price the market already passed, beyond every " + sym + " entry" });
            return l;
        }
        void EscapeAsk(string sym, double px, int dir, int n, double pl, string f)
        {
            if (!Sx.Ask(this, "⛑ Escape level on " + sym, "If the price comes back " + (dir > 0 ? "UP" : "DOWN") + " to " + px.ToString(f, IC) + ", ALL " + n + " " + sym + " positions close by themselves (≈ " + Signed(pl, "USD") + " now).\n\nIt needs trading ARMED. It stays until it fires, you remove it (right-click) or the positions are gone.", "SET THE ESCAPE", "CANCEL", Gold)) return;
            escapes[sym] = Tuple.Create(px, dir); Toast("⛑ escape set on " + sym + " @ " + px.ToString(f, IC));
        }
        void EscapeWatch(Mt5State s)
        {
            if (escapes.Count == 0) return;
            foreach (var kv in escapes.ToList())
            {
                int n; double bid; lock (s.Gate) { n = s.Positions.Count(x => string.Equals(x.Symbol, kv.Key, StringComparison.OrdinalIgnoreCase)); Mt5Quote q; bid = s.Quotes.TryGetValue(kv.Key, out q) ? q.Bid : 0; }
                if (n == 0) { escapes.Remove(kv.Key); continue; } if (bid <= 0) continue;
                bool hit = kv.Value.Item2 > 0 ? bid >= kv.Value.Item1 : bid <= kv.Value.Item1; if (!hit) continue;
                escapes.Remove(kv.Key);
                if (Cmd("⛑ escape " + kv.Key + " @ " + kv.Value.Item1.ToString(IC) + " — close all " + n, "CLOSEALL", kv.Key, "ALL")) Toast("⛑ ESCAPE reached on " + kv.Key + " — closing all " + n + " positions");
                else Toast("⚠ ESCAPE reached on " + kv.Key + " but NOTHING was closed — trading is not armed (the escape is off)");
            }
        }
        List<Mi> LevelForAll(string sym, double px, ChartView v)
        {
            var l = new List<Mi>(); List<Mt5Position> ps; double tv = 1, ts = v.Point > 0 ? v.Point : 0.01; string f = "F" + v.Digits;
            lock (broker.State.Gate) { ps = broker.State.Positions.Where(x => string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); Mt5Symbol sy; if (broker.State.Symbols.TryGetValue(sym, out sy)) { tv = sy.TickValue > 0 ? sy.TickValue : 1; ts = sy.TickSize > 0 ? sy.TickSize : ts; } }
            Func<Mt5Position, double> atPx = x => (px - x.Open) * x.Dir / ts * tv * x.Lots;   // what the ticket makes if closed at px
            foreach (var side in new[] { -1, 1 })
            {
                var all = ps.Where(x => x.Dir == side).ToList(); if (all.Count == 0) continue;
                string nm = side < 0 ? "sell" : "buy"; bool stopOk = side < 0 ? px > v.Ask : px < v.Bid, tpOk = side < 0 ? px < v.Bid : px > v.Ask;
                if (stopOk)
                {
                    var locked = all.Where(x => atPx(x) >= 0).ToList();
                    l.Add(new Mi { Icon = "🛡", Fg = side < 0 ? Red : Green, Text = "S/L for all " + all.Count + " " + nm + (all.Count == 1 ? "" : "s") + " @ " + px.ToString(f, IC) + " …", Hint = locked.Count + " in profit there", Do = () => LevelAsk(sym, all, px, true, atPx, f) });
                    if (locked.Count > 0 && locked.Count < all.Count)
                        l.Add(new Mi { Icon = "🔒", Fg = Gold, Text = "S/L @ " + px.ToString(f, IC) + " only for the " + locked.Count + " " + nm + (locked.Count == 1 ? "" : "s") + " it locks in profit …", Hint = "breakeven or better", Do = () => LevelAsk(sym, locked, px, true, atPx, f) });
                }
                if (tpOk) l.Add(new Mi { Icon = "🎯", Fg = side < 0 ? Red : Green, Text = "T/P for all " + all.Count + " " + nm + (all.Count == 1 ? "" : "s") + " @ " + px.ToString(f, IC) + " …", Do = () => LevelAsk(sym, all, px, false, atPx, f) });
            }
            return l;
        }
        void LevelAsk(string sym, List<Mt5Position> list, double px, bool stop, Func<Mt5Position, double> atPx, string f)
        {
            double sum = list.Sum(atPx); int inProfit = list.Count(x => atPx(x) >= 0); string side = list[0].Dir < 0 ? "sell" : "buy";
            string text = (stop ? "Set the STOP LOSS of " : "Set the TAKE PROFIT of ") + list.Count + " " + side + (list.Count == 1 ? "" : "s") + " on " + sym + " to " + px.ToString(f, IC) + "?\n\n"
                + (stop ? inProfit + " of them close in profit (or at breakeven) if it is hit, " + (list.Count - inProfit) + " at a loss.\n" : "")
                + "If every one closes there: " + Signed(sum, "USD") + ".\nTheir " + (stop ? "targets" : "stops") + " stay as they are.";
            if (!Sx.Ask(this, stop ? "🛡 Stop loss for every " + side : "🎯 Take profit for every " + side, text, "SET " + list.Count, "CANCEL", stop ? Gold : Green)) return;
            int n = 0; foreach (var x in list) if (Cmd((stop ? "S/L " : "T/P ") + px.ToString(f, IC) + " on #" + x.Ticket, "MODIFY", x.Ticket, stop ? px : x.Sl, stop ? x.Tp : px)) n++; else break;
            if (n > 0) Toast((stop ? "🛡 S/L " : "🎯 T/P ") + px.ToString(f, IC) + " sent for " + n + " " + side + (n == 1 ? "" : "s") + " on " + sym);
        }
        void Toast(string msg)
        {
            if (toastBox == null) return;
            toastText.Text = msg; toastBox.Visibility = Visibility.Visible; toastBox.Opacity = 1;
            var an = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromSeconds(3.5) }; an.Completed += delegate { if (toastBox.Opacity < 0.05) toastBox.Visibility = Visibility.Collapsed; }; toastBox.BeginAnimation(UIElement.OpacityProperty, an);
        }

        // ---------------------------------------------------------------- the trading commands (only armed, only through the broker)
        bool Cmd(string label, string cmd, params object[] args)
        {
            bool can; lock (broker.State.Gate) can = broker.State.CanTrade;
            if (!Virtual && (!armed || !can)) { Toast("🔒 " + (!can ? "trading is not allowed by the bridge yet (see the red lines in the panel)" : "press 🔓 ARM TRADING in the panel first")); return false; }
            string why; if (cmd == "ORDER" && !PropAllows(out why)) { Toast("🛑 " + why); Log("✖ " + why, Red); return false; }
            var id = broker.Command(cmd, args); if (id == null) { Toast("⚠ MT5 is not connected"); return false; }
            sent[id] = label; Log("→ " + label, Muted); return true;
        }
        double Num(TextBox b) { double v; return b != null && double.TryParse(b.Text.Replace(',', '.'), NumberStyles.Float, IC, out v) ? v : 0; }
        void Market(int dir)
        {
            var p = Act; double lots = Num(lotsBox); if (lots <= 0) { Toast("type the lots first"); return; }
            Mt5Quote q; lock (broker.State.Gate) broker.State.Quotes.TryGetValue(p.Sym, out q); if (q == null) { Toast("no price for " + p.Sym + " yet"); return; }
            double px = dir > 0 ? q.Ask : q.Bid, sd = Num(slBox), td = Num(tpBox); double sl = sd > 0 ? px - dir * sd : 0, tp = td > 0 ? px + dir * td : 0; string f = "F" + p.V.Digits;
            string what = (dir > 0 ? "BUY " : "SELL ") + lots.ToString("0.00", IC) + " " + p.Sym + " at market ≈ " + px.ToString(f, IC) + (sl > 0 ? " • S/L " + sl.ToString(f, IC) : "") + (tp > 0 ? " • T/P " + tp.ToString(f, IC) : "");
            if (!Virtual && !armed) { Toast("🔒 press 🔓 ARM TRADING in the panel first (one click)"); return; }
            Cmd(what, "ORDER", p.Sym, dir > 0 ? "BUY" : "SELL", lots, 0.0, sl, tp);
        }
        void PlacePending(string sym, string kind, double price, double lotsOverride = 0)
        {
            double lots = lotsOverride > 0 ? lotsOverride : Num(lotsBox); if (lots <= 0 || string.IsNullOrEmpty(kind)) { Toast("type the lots first"); return; }
            if (!Virtual && !armed) { Toast("🔒 press 🔓 ARM TRADING in the panel first (one click)"); return; }
            int dir = kind.StartsWith("BUY") ? 1 : -1; double sd = Num(slBox), td = Num(tpBox); double sl = sd > 0 ? price - dir * sd : 0, tp = td > 0 ? price + dir * td : 0; string f = "F" + Act.V.Digits;
            string what = kind + " " + lots.ToString("0.00", IC) + " " + sym + " @ " + price.ToString(f, IC) + (sl > 0 ? " • S/L " + sl.ToString(f, IC) : "") + (tp > 0 ? " • T/P " + tp.ToString(f, IC) : "");
            if (Cmd(what, "ORDER", sym, kind, lots, price, sl, tp)) { foreach (var pn in panes) { pn.V.PickKind = ""; pn.V.PickPrice = double.NaN; } }
        }
        void CloseAll(string sym, string which)
        {
            int n; double pl; lock (broker.State.Gate) { var ps = broker.State.Positions.Where(x => (sym == "" || string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)) && (which == "ALL" || (which == "BUY" && x.Dir > 0) || (which == "SELL" && x.Dir < 0) || (which == "PROFIT" && x.Profit + x.Swap > 0) || (which == "LOSS" && x.Profit + x.Swap < 0))).ToList(); n = ps.Count; pl = ps.Sum(x => x.Profit + x.Swap); }
            if (n == 0) { Toast("nothing to close"); return; }
            string what = "close " + n + " ticket" + (n == 1 ? "" : "s") + (which == "ALL" ? "" : " (" + (which == "PROFIT" ? "winners" : which == "LOSS" ? "losers" : which.ToLowerInvariant() + "s") + ")") + (sym == "" ? " on EVERY symbol" : " on " + sym) + " — P/L " + Signed(pl, "USD");
            if (sym == "" && which == "ALL")
            {
                string list; lock (broker.State.Gate) list = string.Join("\n", broker.State.Positions.GroupBy(x => x.Symbol).OrderBy(x => x.Key).Select(gq => "   • " + gq.Key + ": " + gq.Count() + " position" + (gq.Count() == 1 ? "" : "s") + ", " + gq.Sum(x => x.Lots).ToString("0.00", IC) + " lots, P/L " + Signed(gq.Sum(x => x.Profit + x.Swap), "USD")));
                what = "close EVERY position on EVERY instrument — " + n + " ticket" + (n == 1 ? "" : "s") + ", P/L " + Signed(pl, "USD") + ":\n" + list + "\n\n(pending orders stay — cancel them on each instrument's card)";
            }
            if (!Sx.Ask(this, sym == "" ? "⛔ Close everything — ALL instruments" : "Close on " + sym + " only", (what.Substring(0, 1).ToUpperInvariant() + what.Substring(1) + "?").Replace(":\n", "?\n").Replace("card)?", "card)"), "CLOSE", "KEEP", Red, "CLOSE " + n + " TICKET" + (n == 1 ? "" : "S"))) return;
            Cmd(what, "CLOSEALL", sym, which);
        }
        void Log(string msg, Brush fg)
        {
            if (logPanel == null) return;
            logPanel.Children.Insert(0, new TextBlock { Text = (Sim && sim != null ? sim.Clock.ToString("HH:mm:ss", IC) : DateTime.Now.ToString("HH:mm:ss", IC)) + "  " + msg, Foreground = fg, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) });
            while (logPanel.Children.Count > 14) logPanel.Children.RemoveAt(logPanel.Children.Count - 1);
        }
        void ReadResults(Mt5State s)
        {
            int total = s.ResultsDropped + s.Results.Count; if (resSeen > total) resSeen = total; if (resSeen < s.ResultsDropped) resSeen = s.ResultsDropped;
            for (; resSeen < total; resSeen++)
            {
                var r = s.Results[resSeen - s.ResultsDropped];
                Log((r.Item2 ? "✓ " : "✖ ") + r.Item3, r.Item2 ? Green : Red); Toast((r.Item2 ? "✓ " : "✖ ") + r.Item3);
            }
            if (sim != null) { if (notesSeen > sim.Notes.Count) notesSeen = 0; for (; notesSeen < sim.Notes.Count; notesSeen++) { var nt = sim.Notes[notesSeen]; Log(nt, nt.StartsWith("⚠") || nt.StartsWith("🛑") ? Red : nt.StartsWith("🎯") ? Green : Cyan); Toast(nt); } }
        }
    }
}
