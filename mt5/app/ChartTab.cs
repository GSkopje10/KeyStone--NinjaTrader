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
        sealed class Pane { public ChartView V; public string Sym = ""; public int Tf = 60; public Border Host; public TextBlock Head; }
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
            zoneIdx = Math.Max(0, Math.Min(Zones.Length - 1, IntOf(Get("zone", "1"), 1))); themeIdx = Math.Max(0, Math.Min(ChartTheme.All.Length - 1, IntOf(Get("theme", "0"), 0)));
            drawColor = Get("drawcolor", "#FFC94D"); magnet = Get("magnet", "0") == "1"; ticketMode = Get("tickets", "AUTO"); oneClickOn = Get("oneclick", "0") == "1";
            // ---- toolbar: symbols (cyan) • timeframes (gold) • split • indicators • theme • zone • reset / full / panel
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) }; Grid.SetRow(bar, 0); Grid.SetColumnSpan(bar, 3); g.Children.Add(bar);
            symChips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }; bar.Children.Add(symChips);
            if (!Sim) { var addSym = Pill("+", Tint(Cyan, 60)); addSym.ToolTip = "add an instrument (every symbol of your broker)"; addSym.Click += delegate { PickSymbol(); }; bar.Children.Add(addSym); }
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
            themeBtn = Sx.Drop("🎨 " + ChartTheme.All[themeIdx].Name, Card, ThemeMenu); bar.Children.Add(themeBtn);
            zoneBtn = Sx.Drop("🕘 " + Zones[zoneIdx].Item1, Card, ZoneMenu); zoneBtn.ToolTip = "the time on the chart (MT5 keeps its server time inside)"; bar.Children.Add(zoneBtn);
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
            // ---- the replay bar (backtest)
            if (Sim) { var rb = BuildReplayBar(); Grid.SetRow(rb, 1); Grid.SetColumnSpan(rb, 3); g.Children.Add(rb); }
            // ---- the drawing rail
            var rail = BuildRail(); Grid.SetRow(rail, 2); Grid.SetColumn(rail, 0); g.Children.Add(rail);
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
            foreach (var p in panes) { p.V.Theme = ChartTheme.All[themeIdx]; p.V.DrawColor = drawColor; p.V.Magnet = magnet; p.V.TicketMode = ticketMode; p.V.Sim = Sim; foreach (var t in ChartView.Indicators) if (Get(ModeKey + "ind." + t.Key, "0") == "1") p.V.Ind.Add(t.Key); }
            SetSplit(IntOf(Get(pre + "split", "1"), 1)); PaintBar(); PaintRail();
            return g;
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
            var l = new List<Mi> { Mi.H("indicators on every chart") };
            foreach (var t in ChartView.Indicators) { var key = t.Key; l.Add(new Mi { Text = t.Name, Hint = t.Key == "ORB" ? "under H1" : t.Key == "SESS" ? "H1 and under" : "", Check = v.Ind.Contains(key), Dot = Sx.B(t.Color), Do = () => ToggleInd(key) }); }
            l.Add(Mi.S()); l.Add(new Mi("⊘", "Turn every indicator off", () => { foreach (var t in ChartView.Indicators) { foreach (var p in panes) p.V.Ind.Remove(t.Key); Put(ModeKey + "ind." + t.Key, "0"); } PaintBar(); }));
            return l;
        }
        List<Mi> ThemeMenu() { var l = new List<Mi> { Mi.H("chart theme") }; for (int i = 0; i < ChartTheme.All.Length; i++) { int ii = i; var th = ChartTheme.All[i]; l.Add(new Mi { Text = th.Name, Check = themeIdx == i, Radio = true, Do = () => SetTheme(ii) }); } return l; }
        List<Mi> ZoneMenu() { var l = new List<Mi> { Mi.H("the time on the chart") }; for (int i = 0; i < Zones.Length; i++) { int ii = i; l.Add(new Mi { Text = Zones[i].Item1, Check = zoneIdx == i, Radio = true, Do = () => SetZone(ii) }); } return l; }
        void SetTheme(int i) { themeIdx = i; Put("theme", i.ToString(IC)); foreach (var p in panes) p.V.Theme = ChartTheme.All[i]; themeBtn.Content = "🎨 " + ChartTheme.All[i].Name + "  ▾"; }
        void SetZone(int i) { zoneIdx = i; Put("zone", i.ToString(IC)); zoneBtn.Content = "🕘 " + Zones[i].Item1 + "  ▾"; newsSig = ""; }

        // ---- the drawing rail
        FrameworkElement BuildRail()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 2, 6, 2) };
            foreach (var t in new[] { Tuple.Create("", "↖", "cursor (Esc)"), Tuple.Create("HLINE", "—", "horizontal line"), Tuple.Create("HRAY", "→", "horizontal ray"), Tuple.Create("VLINE", "│", "vertical line"), Tuple.Create("TREND", "╱", "trend line"), Tuple.Create("RAY", "↗", "trend ray (extends)"),
                Tuple.Create("RECT", "▭", "rectangle / zone"), Tuple.Create("FIB", "≋", "Fibonacci retracement (drag from the start of the move to its end)"), Tuple.Create("TEXT", "T", "text note"), Tuple.Create("LONG", "▲", "long position: risk / reward with your lots"), Tuple.Create("SHORT", "▼", "short position: risk / reward with your lots"), Tuple.Create("MEASURE", "📏", "measure: price, %, points, candles, time, $") })
            {
                var key = t.Item1; var b = RailBtn(t.Item2, t.Item3); b.Foreground = key == "LONG" ? Green : key == "SHORT" ? Red : Text;
                b.Click += delegate { Act.V.Tool = Act.V.Tool == key ? "" : key; if (key == "") Act.V.Deselect(); Act.V.Focus(); PaintRail(); Act.V.InvalidateVisual(); };
                sp.Children.Add(b); toolBtns.Add(Tuple.Create(key, b));
            }
            sp.Children.Add(new Border { Height = 1, Background = Line, Margin = new Thickness(4, 4, 4, 4) });
            magnetBtn = RailBtn("🧲", "magnet: points snap to the open / high / low / close"); magnetBtn.Click += delegate { magnet = !magnet; Put("magnet", magnet ? "1" : "0"); foreach (var p in panes) p.V.Magnet = magnet; PaintRail(); }; sp.Children.Add(magnetBtn);
            colorBtn = RailBtn("●", "the colour of new drawings"); colorBtn.Click += delegate { SxMenu.Show(colorBtn, new Point(colorBtn.ActualWidth + 4, 0), ColorMenu(drawColor, c => { drawColor = c; Put("drawcolor", c); foreach (var p in panes) p.V.DrawColor = c; PaintRail(); })); }; sp.Children.Add(colorBtn);
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
            v.RightClick = (pt, px, t, kind, tk, dr) => ChartMenu(p, pt, px, kind, tk, dr);
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
        void ToggleInd(string key)
        {
            bool on = !Act.V.Ind.Contains(key); foreach (var p in panes) { if (on) p.V.Ind.Add(key); else p.V.Ind.Remove(key); p.V.InvalidateVisual(); } Put(ModeKey + "ind." + key, on ? "1" : "0"); PaintBar();
            var ind = ChartView.Indicators.First(x => x.Key == key); Toast((on ? "☑ " : "☐ ") + ind.Name + (on ? " on — " + ind.Tip : " off"));
        }
        void PaintBar()
        {
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
            p.Sym = sym; p.Tf = tf; int k = panes.IndexOf(p); if (!Sim) Put("sym" + k, sym); Put((Sim ? "bt." : "") + "tf" + k, tf.ToString(IC)); if (!same) { p.V.ResetView(); p.V.Drawings = DrawingsOf(sym); }
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
        void FillChart(Mt5State s)
        {
            // the symbols: yours (tickets), the ones you added, then Market Watch (a backtest: its symbol)
            var mine = s.Positions.Select(p => p.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var added = Sim ? new List<string>() : Get("watch", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var syms = Sim ? s.Symbols.Keys.ToList() : mine.Concat(added).Concat(s.Quotes.Keys.OrderBy(k => k).Take(8)).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToList();
            foreach (var p in panes.Take(split)) if (p.Sym == "" || !s.Quotes.ContainsKey(p.Sym)) { var first = mine.FirstOrDefault() ?? s.Quotes.Keys.FirstOrDefault(k => k.StartsWith("XAU", StringComparison.OrdinalIgnoreCase)) ?? syms.FirstOrDefault(); if (first != null && s.Quotes.ContainsKey(first) && p.Sym != first) SetPane(p, first, p.Tf); }
            Func<string, double> plOf = sy => s.Positions.Where(x => string.Equals(x.Symbol, sy, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Profit + x.Swap);
            string sig = string.Join(",", syms) + "|" + Act.Sym + "|" + string.Join(",", mine) + "|" + string.Join(",", mine.Select(m => Math.Round(plOf(m)).ToString(IC)));
            if (sig != symSig)
            {
                symSig = sig; symChips.Children.Clear();
                foreach (var sy in syms)
                {
                    var s0 = sy; bool on = string.Equals(sy, Act.Sym, StringComparison.OrdinalIgnoreCase); bool hasPos = mine.Contains(sy, StringComparer.OrdinalIgnoreCase);
                    double cpl = hasPos ? plOf(sy) : 0; var b = Pill((hasPos ? "● " : "") + sy + (hasPos ? "  " + (cpl >= 0 ? "+" : "−") + "$" + Math.Abs(cpl).ToString("N0", IC) : ""), on ? (Brush)Tint(Cyan, 200) : Card); b.Padding = new Thickness(10, 0, 10, 0); b.Foreground = on ? Bg : Cyan; b.BorderBrush = on ? Cyan : Tint(Cyan, 90); b.Click += delegate { SetPane(Act, s0, Act.Tf); };
                    b.ToolTip = hasPos ? "you have tickets on " + sy + " • P/L " + Signed(cpl, s.Account.Currency) : sy; if (hasPos && !on) b.Foreground = cpl >= 0 ? Green : Red; symChips.Children.Add(b);
                }
            }
            string zn; var conv = ZoneConv(s, out zn); int nyOff = NyOffset(s);
            // every timeframe's countdown (the selected chart's symbol)
            { Mt5Quote aq; s.Quotes.TryGetValue(Act.Sym, out aq); var sn = Sim ? s.ServerNow : aq != null && aq.Time > s.ServerNow ? aq.Time : s.ServerNow; bool closed = !Sim && aq != null && (s.ServerNow - aq.Time).TotalMinutes > 3;
              foreach (var kv in tfCountdown) { if (sn == DateTime.MinValue || closed) { kv.Value.Text = closed ? "closed" : ""; continue; } var left = Tf.Start(sn, kv.Key).AddMinutes(kv.Key >= 10080 ? 10080 : kv.Key) - sn; if (left.TotalSeconds < 0) left = TimeSpan.Zero;
                kv.Value.Text = left.TotalDays >= 1 ? ((int)left.TotalDays) + "d " + left.Hours + "h" : left.TotalHours >= 1 ? ((int)left.TotalHours) + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00") : left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00"); } }
            var now = DateTime.Now; bool trade = Virtual || (armed && s.CanTrade);
            for (int k = 0; k < split; k++)
            {
                var p = panes[k]; if (p.Sym == "") continue; var v = p.V;
                var se = s.SeriesOf(p.Sym, p.Tf);
                if (!Sim)
                {
                    if (s.BridgeVersion < 2) v.Status = "the chart needs the new bridge — put SpasiXBridge.mq5 in MT5 (⚙ SETUP)";
                    else if (!se.Loaded) { v.Status = "loading " + p.Sym + " " + Tf.Name(p.Tf) + " from MT5 …"; if ((now - se.AskedAt).TotalSeconds > 3) { se.AskedAt = now; broker.Send("BARS|" + p.Sym + "|" + p.Tf + "|" + BarsWanted(p.Sym, p.Tf)); } }
                    else if ((now - se.AskedAt).TotalSeconds > 20) { se.AskedAt = now; broker.Send("BARS|" + p.Sym + "|" + p.Tf + "|3"); }
                }
                Mt5Quote q; s.Quotes.TryGetValue(p.Sym, out q); var sym = s.Symbols.ContainsKey(p.Sym) ? s.Symbols[p.Sym] : null;
                v.Series = se.Loaded ? se : null; v.Symbol = p.Sym; v.Digits = sym != null ? sym.Digits : 2; v.Point = sym != null && sym.Point > 0 ? sym.Point : 0.01; v.TickValue = sym != null && sym.TickValue > 0 ? sym.TickValue : 1; v.TickSize = sym != null && sym.TickSize > 0 ? sym.TickSize : v.Point;
                v.Bid = q != null ? q.Bid : 0; v.Ask = q != null ? q.Ask : 0; v.QuoteTime = q != null ? q.Time : DateTime.MinValue; v.Positions = s.Positions; v.Orders = s.Orders; v.Currency = s.Account.Currency; v.Gate = s.Gate; v.Lots = Num(lotsBox);
                v.ToView = conv; v.ZoneName = zn; v.ServerNow = Sim ? s.ServerNow : q != null && q.Time > s.ServerNow ? q.Time : s.ServerNow; v.NyOffsetHours = nyOff; v.TradeOn = trade;
                var dl = DrawingsOf(p.Sym); if (!ReferenceEquals(v.Drawings, dl)) v.Drawings = dl;   // the symbol's own drawings (saved)
                if (Sim) { v.News = ""; v.NewsHot = false; } else { var nw = NewsFor(p.Sym); v.News = nw.Item1; v.NewsHot = nw.Item2; }
                string f = "F" + v.Digits; var ps = s.Positions.Where(x => string.Equals(x.Symbol, p.Sym, StringComparison.OrdinalIgnoreCase)).ToList(); double pl = ps.Sum(x => x.Profit + x.Swap);
                p.Head.Text = (split > 1 && k == activePane ? "▶ " : "") + p.Sym + " • " + Tf.Name(p.Tf) + (q != null ? "   " + q.Bid.ToString(f, IC) + " / " + q.Ask.ToString(f, IC) + "   spread " + q.Spread(v.Point).ToString("0", IC) : "") + (sym != null && sym.Description != "" ? "   " + sym.Description : "") + (ps.Count > 0 ? "   •   " + ps.Count + " ticket" + (ps.Count == 1 ? "" : "s") + " " + Signed(pl, s.Account.Currency) : "");
                p.Head.Foreground = k == activePane ? Gold : Muted;
                v.InvalidateVisual();
            }
            FillSymPl(s); FillSpreads(s);
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
                l.Add(Mi.Group("🎨", "Colour", ColorMenu(dr.Color, c => { dr.Color = c; SaveDrawings(); v.InvalidateVisual(); })));
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
                l.Add(Mi.S()); l.Add(TicketsMenu());
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
            }
            var tfl = new List<Mi>(); foreach (var m in Tf.All) { int mm = m; tfl.Add(new Mi { Text = Tf.Name(m), Check = m == p.Tf, Radio = true, Do = () => SetPane(p, p.Sym, mm) }); } l.Add(Mi.Group("⏱", "Timeframe", tfl));
            var drl = new List<Mi> { Mi.H("drawing tools") }; foreach (var t in toolBtns.Where(x => x.Item1 != "")) { var key = t.Item1; drl.Add(new Mi(t.Item2.Content as string, ChartView.ToolName(key).Substring(0, 1) + ChartView.ToolName(key).Substring(1).ToLowerInvariant(), () => { v.Tool = key; v.Focus(); PaintRail(); v.InvalidateVisual(); })); }
            drl.Add(Mi.S()); drl.Add(new Mi { Text = "Magnet (snap to the candles)", Check = magnet, Do = () => { magnet = !magnet; Put("magnet", magnet ? "1" : "0"); foreach (var q in panes) q.V.Magnet = magnet; PaintRail(); } });
            drl.Add(Mi.Group("●", "Colour of new drawings", ColorMenu(drawColor, c => { drawColor = c; Put("drawcolor", c); foreach (var q in panes) q.V.DrawColor = c; PaintRail(); })));
            drl.Add(new Mi { Icon = "🗑", Text = "Remove all drawings on " + p.Sym, Danger = true, Do = () => { if (v.Drawings.Count > 0 && Sx.Ask(this, "Remove the drawings", "Remove every drawing on " + p.Sym + "?", "REMOVE", "KEEP", Red)) { v.Drawings.Clear(); SaveDrawings(); v.InvalidateVisual(); } } });
            l.Add(Mi.Group("✏", "Draw", drl));
            l.Add(Mi.Group("📈", "Indicators", IndMenu(v)));
            l.Add(Mi.Group("🎨", "Chart theme", ThemeMenu())); l.Add(Mi.Group("🕘", "Time zone", ZoneMenu()));
            l.Add(TicketsMenu());
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
        Mi TicketsMenu()
        {
            var l = new List<Mi>();
            foreach (var m in new[] { Tuple.Create("AUTO", "a label for every ticket (lines only above 10 on a side)"), Tuple.Create("EACH", "a label for every ticket, always"), Tuple.Create("LINES", "lines only (point at one for its card)") })
            { var key = m.Item1; l.Add(new Mi { Text = m.Item2, Check = ticketMode == key, Radio = true, Do = () => { ticketMode = key; Put("tickets", key); foreach (var p in panes) { p.V.TicketMode = key; p.V.InvalidateVisual(); } } }); }
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
                    double w, p3; if (a.Length > 7 && double.TryParse(a[7], NumberStyles.Float, IC, out w)) d.Width = w; if (a.Length > 8) d.Dash = a[8] == "1"; if (a.Length > 9 && double.TryParse(a[9], NumberStyles.Float, IC, out p3)) d.P3 = p3; if (a.Length > 10) d.Text = a[10].Replace("¦", "|");
                    DrawingsOf(a[0]).Add(d);
                }
            }
            catch { }
        }
        void SaveDrawings()
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllLines(DrawFileNow, drawingsBySym.SelectMany(kv => kv.Value.Select(d => kv.Key + "|" + d.Kind + "|" + d.T1.Ticks + "|" + d.P1.ToString("R", IC) + "|" + d.T2.Ticks + "|" + d.P2.ToString("R", IC) + "|" + d.Color + "|" + d.Width.ToString("R", IC) + "|" + (d.Dash ? "1" : "0") + "|" + d.P3.ToString("R", IC) + "|" + (d.Text ?? "").Replace("|", "¦")))); } catch { }
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
                var added = Get("watch", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); if (!added.Contains(sy)) { added.Add(sy); Put("watch", string.Join(",", added)); }
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
            // close right under BUY / SELL (each asks first): this symbol's sells / all / buys
            var cr = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 2, 0, 2) };
            foreach (var m in new[] { Tuple.Create("✕ SELLS", "SELL", Red), Tuple.Create("✕ ALL", "ALL", Gold), Tuple.Create("✕ BUYS", "BUY", Green) })
            { var which = m.Item2; var b = Pill(m.Item1, Tint(m.Item3, 40)); b.Foreground = m.Item3; b.BorderBrush = Tint(m.Item3, 140); b.Height = 30; b.FontSize = 11; b.Padding = new Thickness(2, 0, 2, 0); b.Margin = new Thickness(0, 0, m.Item2 == "BUY" ? 0 : 4, 0); b.ToolTip = "close " + (which == "ALL" ? "every ticket" : "the " + which.ToLowerInvariant() + "s") + " of this symbol (asks first)"; b.Click += delegate { CloseAll(Act.Sym, which); }; cr.Children.Add(b); }
            t.Children.Add(cr);
            t.Children.Add(new TextBlock { Text = "orders go in at once • the ✕ on the chart closes at once • close-all buttons ask", Foreground = Muted, FontSize = 9.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
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
            mc.Children.Add(new TextBlock { Text = "🧹 MANAGE THIS SYMBOL", Foreground = Violet, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            var mg = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 }; mc.Children.Add(mg);
            foreach (var m in new[] { Tuple.Create("ALL", "ALL", Red, "close every ticket of this symbol"), Tuple.Create("BUYS", "BUY", Green, "close the buys"), Tuple.Create("SELLS", "SELL", Red, "close the sells"), Tuple.Create("WINNERS", "PROFIT", Green, "close the tickets in profit"), Tuple.Create("LOSERS", "LOSS", Red, "close the tickets in loss") })
            { var which = m.Item2; var b = Pill(m.Item1, Card); b.Foreground = m.Item3; b.Height = 28; b.FontSize = 10.5; b.Padding = new Thickness(2, 0, 2, 0); b.Margin = new Thickness(0, 0, 4, 4); b.ToolTip = m.Item4; b.Click += delegate { CloseAll(Act.Sym, which); }; mg.Children.Add(b); }
            var cancel = Pill("ORDERS", Card); cancel.Foreground = Amber; cancel.Height = 28; cancel.FontSize = 10.5; cancel.Padding = new Thickness(2, 0, 2, 0); cancel.Margin = new Thickness(0, 0, 4, 4); cancel.ToolTip = "cancel the pending orders of this symbol"; cancel.Click += delegate { if (Ask("Cancel every pending order on " + Act.Sym + "?")) Cmd("cancel the orders on " + Act.Sym, "CANCELALL", Act.Sym); }; mg.Children.Add(cancel);
            mc.Children.Add(new TextBlock { Text = "close …  (each asks first)", Foreground = Muted, FontSize = 9.5, Margin = new Thickness(0, 0, 0, 4) });
            var everything = Pill("⛔ CLOSE EVERYTHING", Tint(Red, 220)); everything.Foreground = Sx.White; everything.Margin = new Thickness(0, 8, 0, 0); everything.Height = 34; everything.ToolTip = "every ticket on every symbol (asks first)"; everything.Click += delegate { CloseAll("", "ALL"); }; mc.Children.Add(everything);
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
            if (!Sx.Ask(this, sym == "" ? "⛔ Close everything" : "Close", what.Substring(0, 1).ToUpperInvariant() + what.Substring(1) + "?", "CLOSE", "KEEP", Red, "CLOSE " + n + " TICKET" + (n == 1 ? "" : "S"))) return;
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
