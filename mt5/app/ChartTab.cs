// SpasiX — the 📈 CHART tab: 1 / 2 / 4 charts (each its own symbol and timeframe; the toolbar works on the selected one), the toolbar (symbols + add,
// timeframes, split, time zone, theme, indicators, full screen), the right-click menu, the TRADE PANEL (ARM, lots, BUY / SELL, limit / stop at a
// price picked on the chart, close all / buys / sells / profit / loss, breakeven, cancel orders, close everything) and the trading commands.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        sealed class Pane { public ChartView V; public string Sym = ""; public int Tf = 60; public Border Host; public TextBlock Head; }
        readonly List<Pane> panes = new List<Pane>(); int activePane; int split = 1; Grid chartsGrid;
        WrapPanel symChips; readonly List<Tuple<int, Button>> tfBtns = new List<Tuple<int, Button>>(); readonly List<Tuple<string, Button, SolidColorBrush>> indBtns = new List<Tuple<string, Button, SolidColorBrush>>();
        readonly List<Tuple<int, Button>> splitBtns = new List<Tuple<int, Button>>(); ComboBox zoneBox, themeBox; Button fullBtn, panelBtn; string symSig = "";
        readonly Dictionary<string, List<Drawing>> drawingsBySym = new Dictionary<string, List<Drawing>>(StringComparer.OrdinalIgnoreCase);
        static readonly Tuple<string, string>[] Zones = { Tuple.Create("SERVER (MT5)", ""), Tuple.Create("NEW YORK", "Eastern Standard Time"), Tuple.Create("LONDON", "GMT Standard Time"), Tuple.Create("CENTRAL EUROPE", "Central Europe Standard Time"),
            Tuple.Create("FRANKFURT", "W. Europe Standard Time"), Tuple.Create("TOKYO", "Tokyo Standard Time"), Tuple.Create("SYDNEY", "AUS Eastern Standard Time"), Tuple.Create("UTC", "UTC"), Tuple.Create("MY PC", "LOCAL") };
        // trade panel
        bool armed; Button armBtn, buyBtn, sellBtn; TextBlock armText, panelSym, panelExpo, toastText; TextBox lotsBox, slBox, tpBox, pendPrice, beOff; ComboBox pendType; CheckBox oneClick; StackPanel logPanel; Border panelHost, toastBox;
        int resSeen; readonly Dictionary<string, string> sent = new Dictionary<string, string>();

        Pane Act { get { return panes[Math.Max(0, Math.Min(panes.Count - 1, activePane))]; } }

        FrameworkElement BuildChartTab()
        {
            var g = new Grid(); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // ---- toolbar: symbols (cyan) • timeframes (gold) • split • zone • theme • indicators (their colours) • reset / full / panel
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) }; Grid.SetRow(bar, 0); Grid.SetColumnSpan(bar, 2); g.Children.Add(bar);
            symChips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }; bar.Children.Add(symChips);
            var addSym = Pill("＋", Tint(Cyan, 60)); addSym.ToolTip = "add an instrument (every symbol of your broker)"; addSym.Click += delegate { PickSymbol(); }; bar.Children.Add(addSym);
            bar.Children.Add(Sep());
            foreach (var m in Tf.All) { int mm = m; var b = Pill(Tf.Name(m), Card); b.MinWidth = 42; b.BorderBrush = Tint(Gold, 90); b.Click += delegate { SetPane(Act, Act.Sym, mm); }; bar.Children.Add(b); tfBtns.Add(Tuple.Create(m, b)); }
            bar.Children.Add(Sep());
            foreach (var sp in new[] { Tuple.Create(1, "▣ 1"), Tuple.Create(2, "◫ 2"), Tuple.Create(4, "⊞ 4") }) { int n = sp.Item1; var b = Pill(sp.Item2, Card); b.ToolTip = n == 1 ? "one chart" : n + " charts side by side (each its own symbol / timeframe — click one to select it)"; b.Click += delegate { SetSplit(n); }; bar.Children.Add(b); splitBtns.Add(Tuple.Create(n, b)); }
            bar.Children.Add(Sep());
            zoneBox = Combo(Zones.Select(z => "🕘 " + z.Item1).ToArray(), 150); zoneBox.SelectedIndex = Math.Max(0, Math.Min(Zones.Length - 1, int.Parse(Get("zone", "1"), IC))); zoneBox.ToolTip = "the time on the chart (MT5 keeps its server time inside)";
            zoneBox.SelectionChanged += delegate { Put("zone", zoneBox.SelectedIndex.ToString(IC)); }; bar.Children.Add(zoneBox);
            themeBox = Combo(ChartTheme.All.Select(t => "🎨 " + t.Name).ToArray(), 150); themeBox.SelectedIndex = Math.Max(0, Math.Min(ChartTheme.All.Length - 1, int.Parse(Get("theme", "0"), IC)));
            themeBox.SelectionChanged += delegate { Put("theme", themeBox.SelectedIndex.ToString(IC)); foreach (var p in panes) p.V.Theme = ChartTheme.All[themeBox.SelectedIndex]; }; bar.Children.Add(themeBox);
            bar.Children.Add(Sep());
            foreach (var ind in new[] { Tuple.Create("EMA9", "EMA 9", "#FFC94D"), Tuple.Create("EMA21", "EMA 21", "#4DD8FF"), Tuple.Create("EMA50", "EMA 50", "#B388FF"), Tuple.Create("EMA200", "EMA 200", "#FF9F43"), Tuple.Create("VWAP", "VWAP", "#FFC94D"), Tuple.Create("FVG", "FVG", "#00E676"), Tuple.Create("ORB", "NY ORB", "#B388FF"), Tuple.Create("PDHL", "PDH / PDL", "#42A5F5") })
            {
                var key = ind.Item1; var col = B(ind.Item3); var b = Pill("", Card); b.Click += delegate { ToggleInd(key); }; bar.Children.Add(b); indBtns.Add(Tuple.Create(key, b, col));
                b.Tag = ind.Item2;
            }
            bar.Children.Add(Sep());
            var reset = Pill("⟲ NOW", Card); reset.ToolTip = "back to the latest candles, automatic scale (or double-click the chart)"; reset.Click += delegate { Act.V.ResetView(); }; bar.Children.Add(reset);
            fullBtn = Pill("⛶ FULL", Card); fullBtn.ToolTip = "the chart over the whole screen (F11 / Esc)"; fullBtn.Click += delegate { SetFullChart(!fullChart); }; bar.Children.Add(fullBtn);
            panelBtn = Pill("◧ PANEL", Tint(Gold, 60)); panelBtn.ToolTip = "show / hide the trade panel"; panelBtn.Click += delegate { panelHost.Visibility = panelHost.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; Put("panel", panelHost.Visibility == Visibility.Visible ? "1" : "0"); }; bar.Children.Add(panelBtn);
            // ---- the charts
            chartsGrid = new Grid(); Grid.SetRow(chartsGrid, 1); g.Children.Add(chartsGrid);
            for (int k = 0; k < 4; k++) panes.Add(MakePane(k));
            for (int k = 0; k < 4; k++) { int tf0; panes[k].Sym = Get("sym" + k, k == 0 ? Get("sym", "") : ""); panes[k].Tf = int.TryParse(Get("tf" + k, k == 0 ? Get("tf", "60") : k == 1 ? "15" : k == 2 ? "5" : "240"), out tf0) && Tf.All.Contains(tf0) ? tf0 : 60; }
            // ---- the trade panel
            panelHost = BuildTradePanel(); Grid.SetRow(panelHost, 1); Grid.SetColumn(panelHost, 1); g.Children.Add(panelHost); if (Get("panel", "1") == "0") panelHost.Visibility = Visibility.Collapsed;
            // ---- the toast (answers from MT5)
            toastText = new TextBlock { Foreground = Text, FontSize = 13, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            toastBox = new Border { Child = toastText, Background = Tint(Panel, 245), BorderBrush = Gold, BorderThickness = new Thickness(1.4), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 8, 14, 9), Margin = new Thickness(0, 0, 0, 18), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed, MaxWidth = 700, IsHitTestVisible = false };
            Grid.SetRow(toastBox, 1); g.Children.Add(toastBox);
            foreach (var k in new[] { "EMA21", "VWAP" }) if (Get("ind." + k, "") == "") Put("ind." + k, "1");
            foreach (var p in panes) { p.V.Theme = ChartTheme.All[themeBox.SelectedIndex]; foreach (var t in indBtns) if (Get("ind." + t.Item1, "0") == "1") p.V.Ind.Add(t.Item1); }
            int sp0; SetSplit(int.TryParse(Get("split", "1"), out sp0) ? sp0 : 1); PaintBar();
            return g;
        }
        FrameworkElement Sep() { return new Border { Width = 1, Height = 24, Background = Line, Margin = new Thickness(4, 3, 10, 3), VerticalAlignment = VerticalAlignment.Center }; }
        ComboBox Combo(string[] items, double w) { var c = new ComboBox { Width = w, Height = 30, Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Bold, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center }; foreach (var i in items) c.Items.Add(i); return c; }

        Pane MakePane(int k)
        {
            var p = new Pane(); var v = new ChartView(); p.V = v;
            p.Head = new TextBlock { Foreground = Muted, FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 2, 0, 2) };
            var dock = new DockPanel(); DockPanel.SetDock(p.Head, Dock.Top); dock.Children.Add(p.Head); dock.Children.Add(v);
            p.Host = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Line, BorderThickness = new Thickness(1.4), Child = dock, Margin = new Thickness(2) };
            p.Host.PreviewMouseLeftButtonDown += delegate { if (activePane != k) { activePane = k; PaintBar(); } };
            p.Host.PreviewMouseRightButtonDown += delegate { if (activePane != k) { activePane = k; PaintBar(); } };
            v.CloseTicket = (tk, lots) => Cmd("close #" + tk, "CLOSE", tk, lots);
            v.ModifyTicket = (tk, sl, tp) => Cmd("S/L " + (sl > 0 ? sl.ToString("0.#####", IC) : "—") + " • T/P " + (tp > 0 ? tp.ToString("0.#####", IC) : "—") + " on #" + tk, "MODIFY", tk, sl, tp);
            v.ModifyOrder = (tk, px, sl, tp) => Cmd("order #" + tk + " → " + px.ToString("0.#####", IC), "MODORD", tk, px, sl, tp);
            v.CancelOrder = tk => Cmd("cancel order #" + tk, "CANCEL", tk);
            v.PricePicked = px => { pendPrice.Text = px.ToString("F" + v.Digits, IC); };
            v.Toast = Toast; v.DrawingsChanged = SaveDrawings;
            v.RightClick = (pt, px, t) => ChartMenu(p, px);
            return p;
        }
        void SetSplit(int n)
        {
            split = n == 2 || n == 4 ? n : 1; Put("split", split.ToString(IC)); chartsGrid.Children.Clear(); chartsGrid.RowDefinitions.Clear(); chartsGrid.ColumnDefinitions.Clear();
            int cols = split == 1 ? 1 : 2, rows = split == 4 ? 2 : 1;
            for (int c = 0; c < cols; c++) chartsGrid.ColumnDefinitions.Add(new ColumnDefinition()); for (int r = 0; r < rows; r++) chartsGrid.RowDefinitions.Add(new RowDefinition());
            for (int k = 0; k < split; k++) { var h = panes[k].Host; Grid.SetColumn(h, k % cols); Grid.SetRow(h, k / cols); chartsGrid.Children.Add(h); }
            if (activePane >= split) activePane = 0; PaintBar();
        }
        void ToggleInd(string key)
        {
            bool on = !Act.V.Ind.Contains(key); foreach (var p in panes) { if (on) p.V.Ind.Add(key); else p.V.Ind.Remove(key); } Put("ind." + key, on ? "1" : "0"); PaintBar();
        }
        void PaintBar()
        {
            foreach (var t in tfBtns) { bool on = t.Item1 == Act.Tf; t.Item2.Background = on ? Gold : (Brush)Card; t.Item2.Foreground = on ? Bg : Gold; }
            foreach (var t in splitBtns) { bool on = t.Item1 == split; t.Item2.Background = on ? Tint(Violet, 170) : (Brush)Card; }
            foreach (var t in indBtns) { bool on = Act.V.Ind.Contains(t.Item1); t.Item2.Content = (on ? "☑ " : "☐ ") + (t.Item2.Tag as string); t.Item2.Foreground = on ? t.Item3 : Muted; t.Item2.BorderBrush = on ? t.Item3 : Line; t.Item2.Background = on ? Tint(t.Item3, 40) : (Brush)Card; }
            for (int k = 0; k < panes.Count; k++) { panes[k].Host.BorderBrush = split > 1 && k == activePane ? Gold : Line; }
            symSig = "";
        }
        void SetPane(Pane p, string sym, int tf)
        {
            if (string.IsNullOrEmpty(sym)) return; bool same = string.Equals(sym, p.Sym, StringComparison.OrdinalIgnoreCase) && tf == p.Tf;
            if (!same) SaveDrawings();
            p.Sym = sym; p.Tf = tf; int k = panes.IndexOf(p); Put("sym" + k, sym); Put("tf" + k, tf.ToString(IC)); if (!same) { p.V.ResetView(); p.V.Drawings = DrawingsOf(sym); }
            PaintBar();
        }
        Func<DateTime, DateTime> ZoneConv(Mt5State s, out string name)
        {
            var z = Zones[Math.Max(0, zoneBox.SelectedIndex)]; name = z.Item1.Split(' ')[0]; int off = s.ServerOffsetSec;
            if (z.Item2 == "") return t => t;
            TimeZoneInfo tz = null; try { tz = z.Item2 == "LOCAL" ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(z.Item2); } catch { }
            if (tz == null) return t => t.AddSeconds(-off);
            return t => { try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t.AddSeconds(-off), DateTimeKind.Utc), tz); } catch { return t; } };
        }

        // ---------------------------------------------------------------- every refresh: the charts
        void FillChart(Mt5State s)
        {
            // the symbols: yours (tickets), the ones you added, then Market Watch
            var mine = s.Positions.Select(p => p.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var added = Get("watch", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var syms = mine.Concat(added).Concat(s.Quotes.Keys.OrderBy(k => k).Take(8)).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToList();
            foreach (var p in panes.Take(split)) if (p.Sym == "" || !s.Quotes.ContainsKey(p.Sym)) { var first = mine.FirstOrDefault() ?? s.Quotes.Keys.FirstOrDefault(k => k.StartsWith("XAU", StringComparison.OrdinalIgnoreCase)) ?? syms.FirstOrDefault(); if (first != null && s.Quotes.ContainsKey(first) && p.Sym != first) SetPane(p, first, p.Tf); }
            string sig = string.Join(",", syms) + "|" + Act.Sym;
            if (sig != symSig)
            {
                symSig = sig; symChips.Children.Clear();
                foreach (var sy in syms)
                {
                    var s0 = sy; bool on = string.Equals(sy, Act.Sym, StringComparison.OrdinalIgnoreCase); bool hasPos = mine.Contains(sy, StringComparer.OrdinalIgnoreCase);
                    var b = Pill((hasPos ? "● " : "") + sy, on ? (Brush)Tint(Cyan, 90) : Card); b.Foreground = on ? Bg : Cyan; b.BorderBrush = on ? Cyan : Tint(Cyan, 90); b.Click += delegate { SetPane(Act, s0, Act.Tf); };
                    symChips.Children.Add(b);
                }
            }
            string zn; var conv = ZoneConv(s, out zn);
            double nyOff = -7; try { var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); nyOff = Math.Round(ny.GetUtcOffset(DateTime.UtcNow).TotalHours - s.ServerOffsetSec / 3600.0); } catch { }
            var now = DateTime.Now; bool trade = armed && s.CanTrade;
            for (int k = 0; k < split; k++)
            {
                var p = panes[k]; if (p.Sym == "") continue; var v = p.V;
                var se = s.SeriesOf(p.Sym, p.Tf);
                if (s.BridgeVersion < 2) v.Status = "the chart needs the new bridge — put SpasiXBridge.mq5 in MT5 (⚙ SETUP)";
                else if (!se.Loaded) { v.Status = "loading " + p.Sym + " " + Tf.Name(p.Tf) + " from MT5 …"; if ((now - se.AskedAt).TotalSeconds > 3) { se.AskedAt = now; bridge.Send("BARS|" + p.Sym + "|" + p.Tf + "|2000"); } }
                else if ((now - se.AskedAt).TotalSeconds > 20) { se.AskedAt = now; bridge.Send("BARS|" + p.Sym + "|" + p.Tf + "|3"); }
                Mt5Quote q; s.Quotes.TryGetValue(p.Sym, out q); var sym = s.Symbols.ContainsKey(p.Sym) ? s.Symbols[p.Sym] : null;
                v.Series = se.Loaded ? se : null; v.Symbol = p.Sym; v.Digits = sym != null ? sym.Digits : 2; v.Point = sym != null && sym.Point > 0 ? sym.Point : 0.01;
                v.Bid = q != null ? q.Bid : 0; v.Ask = q != null ? q.Ask : 0; v.Positions = s.Positions; v.Orders = s.Orders; v.Currency = s.Account.Currency; v.Gate = s.Gate;
                v.ToView = conv; v.ZoneName = zn; v.ServerNow = q != null && q.Time > s.ServerNow ? q.Time : s.ServerNow; v.NyOffsetHours = (int)nyOff; v.TradeOn = trade;
                var dl = DrawingsOf(p.Sym); if (!ReferenceEquals(v.Drawings, dl)) v.Drawings = dl;   // the symbol's own drawings (saved)
                var nw = NewsFor(p.Sym); v.News = nw.Item1; v.NewsHot = nw.Item2;
                string f = "F" + v.Digits; var ps = s.Positions.Where(x => string.Equals(x.Symbol, p.Sym, StringComparison.OrdinalIgnoreCase)).ToList(); double pl = ps.Sum(x => x.Profit + x.Swap);
                p.Head.Text = (split > 1 && k == activePane ? "▶ " : "") + p.Sym + " • " + Tf.Name(p.Tf) + (q != null ? "   " + q.Bid.ToString(f, IC) + " / " + q.Ask.ToString(f, IC) + "   spread " + q.Spread(v.Point).ToString("0", IC) : "") + (sym != null ? "   " + sym.Description : "") + (ps.Count > 0 ? "   •   " + ps.Count + " tickets " + Signed(pl, s.Account.Currency) : "");
                p.Head.Foreground = k == activePane ? Gold : Muted;
                v.InvalidateVisual();
            }
            FillPanel(s);
        }

        // ---------------------------------------------------------------- the right-click menu
        void ChartMenu(Pane p, double price)
        {
            var v = p.V; var cm = new ContextMenu { Background = Panel, Foreground = Text, BorderBrush = Gold, BorderThickness = new Thickness(1) };
            Func<string, Action, MenuItem> mi = (h, a) => { var m = new MenuItem { Header = h, Foreground = Text }; if (a != null) m.Click += delegate { a(); }; return m; };
            string f = "F" + v.Digits; var head = mi(p.Sym + " • " + Tf.Name(p.Tf) + " • " + price.ToString(f, IC), null); head.IsEnabled = false; cm.Items.Add(head);
            if (armed && v.Bid > 0)
            {
                bool below = price < v.Bid;
                cm.Items.Add(mi((below ? "🟢 BUY LIMIT" : "🔴 SELL LIMIT") + " here @ " + price.ToString(f, IC), () => PlacePending(p.Sym, below ? "BUY LIMIT" : "SELL LIMIT", price)));
                cm.Items.Add(mi((below ? "🔴 SELL STOP" : "🟢 BUY STOP") + " here @ " + price.ToString(f, IC), () => PlacePending(p.Sym, below ? "SELL STOP" : "BUY STOP", price)));
                cm.Items.Add(new Separator());
            }
            var tfM = mi("⏱ Timeframe", null); foreach (var m in Tf.All) { int mm = m; tfM.Items.Add(mi((m == p.Tf ? "◉  " : "○  ") + Tf.Name(m), () => SetPane(p, p.Sym, mm))); } cm.Items.Add(tfM);
            var drM = mi("✏ Draw", null);
            foreach (var d in new[] { Tuple.Create("HLINE", "— Horizontal line"), Tuple.Create("VLINE", "| Vertical line"), Tuple.Create("TREND", "／ Trend line"), Tuple.Create("RECT", "▭ Rectangle") }) { var dk = d.Item1; drM.Items.Add(mi(d.Item2, () => { v.Tool = dk; v.Focus(); v.InvalidateVisual(); })); }
            drM.Items.Add(new Separator()); drM.Items.Add(mi("Delete the selected drawing (Del)", () => v.DeleteSelected()));
            drM.Items.Add(mi("Remove all drawings on " + p.Sym, () => { if (MessageBox.Show(this, "Remove every drawing on " + p.Sym + "?", "SpasiX", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { v.Drawings.Clear(); SaveDrawings(); v.InvalidateVisual(); } }));
            cm.Items.Add(drM);
            var inM = mi("📈 Indicators", null); foreach (var t in indBtns) { var key = t.Item1; inM.Items.Add(mi((v.Ind.Contains(key) ? "☑  " : "☐  ") + (t.Item2.Tag as string), () => ToggleInd(key))); } cm.Items.Add(inM);
            var thM = mi("🎨 Chart theme", null); for (int i = 0; i < ChartTheme.All.Length; i++) { int ii = i; thM.Items.Add(mi((themeBox.SelectedIndex == i ? "◉  " : "○  ") + ChartTheme.All[i].Name, () => themeBox.SelectedIndex = ii)); } cm.Items.Add(thM);
            var zM = mi("🕘 Time zone", null); for (int i = 0; i < Zones.Length; i++) { int ii = i; zM.Items.Add(mi((zoneBox.SelectedIndex == i ? "◉  " : "○  ") + Zones[i].Item1, () => zoneBox.SelectedIndex = ii)); } cm.Items.Add(zM);
            var spM = mi("⊞ Split screen", null); foreach (var n in new[] { 1, 2, 4 }) { int nn = n; spM.Items.Add(mi((split == n ? "◉  " : "○  ") + n + (n == 1 ? " chart" : " charts"), () => SetSplit(nn))); } cm.Items.Add(spM);
            cm.Items.Add(new Separator());
            cm.Items.Add(mi("⟲ Back to now • automatic scale", () => v.ResetView()));
            cm.Items.Add(mi(fullChart ? "⛶ Exit full screen (Esc)" : "⛶ Full screen chart (F11)", () => SetFullChart(!fullChart)));
            cm.PlacementTarget = v; cm.IsOpen = true;
        }

        // ---------------------------------------------------------------- drawings, saved per symbol
        List<Drawing> DrawingsOf(string sym) { List<Drawing> l; if (!drawingsBySym.TryGetValue(sym, out l)) drawingsBySym[sym] = l = new List<Drawing>(); return l; }
        void LoadDrawings()
        {
            try
            {
                foreach (var ln in File.ReadAllLines(DrawFile))
                {
                    var a = ln.Split('|'); if (a.Length < 7) continue; long t1, t2; double p1, p2;
                    if (!long.TryParse(a[2], out t1) || !long.TryParse(a[4], out t2) || !double.TryParse(a[3], NumberStyles.Float, IC, out p1) || !double.TryParse(a[5], NumberStyles.Float, IC, out p2)) continue;
                    DrawingsOf(a[0]).Add(new Drawing { Kind = a[1], T1 = new DateTime(t1), P1 = p1, T2 = new DateTime(t2), P2 = p2, Color = a[6] });
                }
            }
            catch { }
        }
        void SaveDrawings()
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllLines(DrawFile, drawingsBySym.SelectMany(kv => kv.Value.Select(d => kv.Key + "|" + d.Kind + "|" + d.T1.Ticks + "|" + d.P1.ToString("R", IC) + "|" + d.T2.Ticks + "|" + d.P2.ToString("R", IC) + "|" + d.Color))); } catch { }
        }

        // ---------------------------------------------------------------- + an instrument
        void PickSymbol()
        {
            bridge.Send("ALLSYMS");
            var w = new Window { Title = "SpasiX • add an instrument", Width = 420, Height = 560, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Bg, Foreground = Text };
            var sp = new DockPanel { Margin = new Thickness(14) }; var box = new TextBox { FontSize = 15, Height = 32, Margin = new Thickness(0, 0, 0, 8), Background = Card, Foreground = Text, BorderBrush = Gold, CaretBrush = Gold };
            DockPanel.SetDock(box, Dock.Top); sp.Children.Add(box);
            var hint = new TextBlock { Text = "type to search • double-click = add it and open its chart", Foreground = Muted, FontSize = 11.5, Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(hint, Dock.Top); sp.Children.Add(hint);
            var list = new ListBox { Background = Card, Foreground = Text, BorderBrush = Line, FontSize = 14 }; sp.Children.Add(list); w.Content = sp;
            Action fill = () => { List<string> all; lock (bridge.State.Gate) all = bridge.State.AllSymbols.Count > 0 ? bridge.State.AllSymbols.ToList() : bridge.State.Quotes.Keys.ToList(); var q = box.Text.Trim(); list.Items.Clear(); foreach (var x in all.Where(x => q == "" || x.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(400)) list.Items.Add(x); };
            box.TextChanged += delegate { fill(); }; var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) }; int tries = 0; tm.Tick += delegate { fill(); if (++tries > 6) tm.Stop(); }; tm.Start(); fill();
            list.MouseDoubleClick += delegate
            {
                var sy = list.SelectedItem as string; if (sy == null) return; bridge.Send("WATCH|" + sy);
                var added = Get("watch", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); if (!added.Contains(sy)) { added.Add(sy); Put("watch", string.Join(",", added)); }
                SetPane(Act, sy, Act.Tf); w.Close();
            };
            w.Closed += delegate { tm.Stop(); }; box.Focus(); w.ShowDialog();
        }

        // ---------------------------------------------------------------- the TRADE PANEL
        Border BuildTradePanel()
        {
            var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 10) };
            Func<string, SolidColorBrush, Border> card = (h, edge) => { var c = new Border { Background = Card, BorderBrush = Tint(edge, 120), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 7, 10, 9), Margin = new Thickness(0, 0, 0, 8) }; return c; };
            Func<string, TextBlock> cap = t => new TextBlock { Text = t, Foreground = Muted, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2) };
            Func<string, double, TextBox> input = (t, w) => new TextBox { Text = t, Width = w, Height = 28, FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Background = Bg, Foreground = Text, BorderBrush = Line, CaretBrush = Gold, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 6, 0) };
            // ARM
            var a = new StackPanel(); var ac = card("", Gold); ac.Child = a; sp.Children.Add(ac);
            armText = new TextBlock { Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) }; a.Children.Add(armText);
            armBtn = Pill("🔓 ARM TRADING", Tint(Orange, 200)); armBtn.Height = 34; armBtn.Margin = new Thickness(0); armBtn.Click += delegate { SetArmed(!armed, null); }; a.Children.Add(armBtn);
            // the symbol + exposure
            var t = new StackPanel(); var tc = card("", Cyan); tc.Child = t; sp.Children.Add(tc);
            panelSym = new TextBlock { Foreground = Cyan, FontSize = 16, FontWeight = FontWeights.Bold }; t.Children.Add(panelSym);
            panelExpo = new TextBlock { Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) }; t.Children.Add(panelExpo);
            // lots
            t.Children.Add(cap("LOTS"));
            var lr = new StackPanel { Orientation = Orientation.Horizontal }; var lm = Pill("−", Card); lm.Width = 32; lotsBox = input(Get("lots", "0.10"), 80); lotsBox.TextAlignment = TextAlignment.Center; var lp = Pill("+", Card); lp.Width = 32; lp.Margin = new Thickness(6, 0, 0, 0);
            lm.Click += delegate { StepLots(-1); }; lp.Click += delegate { StepLots(1); }; lotsBox.LostFocus += delegate { Put("lots", lotsBox.Text); };
            lr.Children.Add(lm); lr.Children.Add(lotsBox); lr.Children.Add(lp); t.Children.Add(lr);
            var pr = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) }; foreach (var v in new[] { "0.01", "0.05", "0.10", "0.50", "1.00" }) { var vv = v; var b = Pill(v, Card); b.Height = 24; b.FontSize = 10.5; b.Padding = new Thickness(7, 0, 7, 0); b.Margin = new Thickness(0, 0, 4, 4); b.Click += delegate { lotsBox.Text = vv; Put("lots", vv); }; pr.Children.Add(b); } t.Children.Add(pr);
            // stop / target distance
            var st = new Grid(); st.ColumnDefinitions.Add(new ColumnDefinition()); st.ColumnDefinitions.Add(new ColumnDefinition());
            var s1 = new StackPanel(); s1.Children.Add(cap("STOP (price distance)")); slBox = input(Get("sl", "0"), 100); s1.Children.Add(slBox);
            var s2 = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; s2.Children.Add(cap("TARGET (price distance)")); tpBox = input(Get("tp", "0"), 100); s2.Children.Add(tpBox); Grid.SetColumn(s2, 1);
            st.Children.Add(s1); st.Children.Add(s2); t.Children.Add(st); slBox.LostFocus += delegate { Put("sl", slBox.Text); }; tpBox.LostFocus += delegate { Put("tp", tpBox.Text); };
            t.Children.Add(new TextBlock { Text = "0 = none • gold: 5 = $5 move • drag the lines on the chart later", Foreground = Muted, FontSize = 9.5, Margin = new Thickness(0, 2, 0, 6) });
            // SELL / BUY
            var bs = new Grid { Margin = new Thickness(0, 2, 0, 4) }; bs.ColumnDefinitions.Add(new ColumnDefinition()); bs.ColumnDefinitions.Add(new ColumnDefinition());
            sellBtn = Pill("SELL", Red); sellBtn.Height = 54; sellBtn.FontSize = 14; sellBtn.Margin = new Thickness(0, 0, 4, 0); buyBtn = Pill("BUY", Tint(Green, 230)); buyBtn.Height = 54; buyBtn.FontSize = 14; buyBtn.Foreground = Bg; buyBtn.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(buyBtn, 1); bs.Children.Add(sellBtn); bs.Children.Add(buyBtn); t.Children.Add(bs);
            sellBtn.Click += delegate { Market(-1); }; buyBtn.Click += delegate { Market(1); };
            oneClick = new CheckBox { Content = "one-click (no question before market orders)", Foreground = Muted, FontSize = 10.5, IsChecked = Get("oneclick", "0") == "1" }; oneClick.Click += delegate { Put("oneclick", oneClick.IsChecked == true ? "1" : "0"); }; t.Children.Add(oneClick);
            // pending
            var pc = new StackPanel(); var pcc = card("", Orange); pcc.Child = pc; sp.Children.Add(pcc);
            pc.Children.Add(new TextBlock { Text = "⏳ LIMIT / STOP ORDER", Foreground = Orange, FontSize = 12, FontWeight = FontWeights.Bold });
            pendType = Combo(new[] { "BUY LIMIT", "SELL LIMIT", "BUY STOP", "SELL STOP" }, 240); pendType.Margin = new Thickness(0, 4, 0, 4); pendType.SelectedIndex = 0; pc.Children.Add(pendType);
            var ppr = new StackPanel { Orientation = Orientation.Horizontal }; pendPrice = input("", 120); var pick = Pill("🎯 PICK", Card); pick.Margin = new Thickness(6, 0, 0, 0); pick.ToolTip = "click the price on the chart";
            pick.Click += delegate { var v = Act.V; v.PickKind = pendType.SelectedItem as string; v.PickPrice = double.NaN; Toast("click the price on the chart for the " + v.PickKind); v.Focus(); };
            ppr.Children.Add(pendPrice); ppr.Children.Add(pick); pc.Children.Add(ppr);
            var place = Pill("PLACE ORDER", Tint(Orange, 200)); place.Margin = new Thickness(0, 6, 0, 0); place.Foreground = Bg; place.Click += delegate { double px; if (!double.TryParse(pendPrice.Text.Replace(',', '.'), NumberStyles.Float, IC, out px) || px <= 0) { Toast("type or 🎯 pick a price first"); return; } PlacePending(Act.Sym, pendType.SelectedItem as string, px); }; pc.Children.Add(place);
            // manage
            var mc = new StackPanel(); var mcc = card("", Violet); mcc.Child = mc; sp.Children.Add(mcc);
            mc.Children.Add(new TextBlock { Text = "🧹 MANAGE THIS SYMBOL", Foreground = Violet, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            var mg = new UniformGrid2(2); mc.Children.Add(mg.Panel);
            foreach (var m in new[] { Tuple.Create("CLOSE ALL", "ALL", Red), Tuple.Create("CLOSE BUYS", "BUY", Green), Tuple.Create("CLOSE SELLS", "SELL", Red), Tuple.Create("CLOSE PROFIT", "PROFIT", Green), Tuple.Create("CLOSE LOSS", "LOSS", Red) })
            { var which = m.Item2; var b = Pill(m.Item1, Card); b.Foreground = m.Item3; b.Height = 30; b.Margin = new Thickness(0, 0, 4, 4); b.Click += delegate { CloseAll(Act.Sym, which); }; mg.Add(b); }
            var cancel = Pill("CANCEL ORDERS", Card); cancel.Foreground = Cyan; cancel.Height = 30; cancel.Margin = new Thickness(0, 0, 4, 4); cancel.Click += delegate { if (Ask("Cancel every pending order on " + Act.Sym + "?")) Cmd("cancel the orders on " + Act.Sym, "CANCELALL", Act.Sym); }; mg.Add(cancel);
            var ber = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) }; var be = Pill("⚖ BREAKEVEN", Tint(Gold, 170)); be.Foreground = Bg; be.ToolTip = "every ticket of this symbol in profit: the stop to its entry (+ the points on the right)";
            beOff = input(Get("beoff", "10"), 60); beOff.ToolTip = "points beyond the entry (covers the spread / commission)"; beOff.LostFocus += delegate { Put("beoff", beOff.Text); };
            be.Click += delegate { int off; int.TryParse(beOff.Text, out off); Cmd("breakeven on " + Act.Sym, "BE", Act.Sym, "ALL", off); };
            ber.Children.Add(be); ber.Children.Add(new TextBlock { Text = " + ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); ber.Children.Add(beOff); ber.Children.Add(new TextBlock { Text = " pts", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); mc.Children.Add(ber);
            var everything = Pill("⛔ CLOSE EVERYTHING (all symbols)", Tint(Red, 210)); everything.Margin = new Thickness(0, 8, 0, 0); everything.Height = 34; everything.Click += delegate { CloseAll("", "ALL"); }; mc.Children.Add(everything);
            // the answers from MT5
            var lc = new StackPanel(); var lcc = card("", Line); lcc.Child = lc; sp.Children.Add(lcc);
            lc.Children.Add(new TextBlock { Text = "🧾 MT5 ANSWERS", Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.Bold });
            logPanel = new StackPanel(); lc.Children.Add(logPanel);
            return new Border { Width = 286, Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(1, 0, 0, 0), CornerRadius = new CornerRadius(10), Margin = new Thickness(6, 2, 0, 2), Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp } };
        }
        sealed class UniformGrid2 { public readonly System.Windows.Controls.Primitives.UniformGrid Panel; public UniformGrid2(int cols) { Panel = new System.Windows.Controls.Primitives.UniformGrid { Columns = cols }; } public void Add(UIElement e) { Panel.Children.Add(e); } }
        void StepLots(int dir)
        {
            double v; double.TryParse(lotsBox.Text.Replace(',', '.'), NumberStyles.Float, IC, out v); double step = 0.01;
            lock (bridge.State.Gate) { Mt5Symbol sy; if (bridge.State.Symbols.TryGetValue(Act.Sym, out sy) && sy.VolStep > 0) step = sy.VolStep; }
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
            string why = s.BridgeVersion < 3 ? "update the bridge to SpasiXBridge v3 (⚙ SETUP step 1)" : !s.AllowInput ? "the bridge's AllowTrading is false (⚙ SETUP step 4)" : !s.AlgoTerminal ? "turn ON 'Algo Trading' in the MT5 toolbar" : !s.AlgoEa ? "tick 'Allow Algo Trading' in the bridge's properties" : "";
            if (why != "" && armed) SetArmed(false, why);
            armText.Text = why != "" ? "🔒 READ ONLY — " + why : armed ? "🟢 ARMED • " + a.Kind + " account • max " + s.MaxLots.ToString("0.##", IC) + " lots / order (the bridge's limit)" : "🔒 not armed — ARM to trade from SpasiX (" + a.Kind + " account)";
            armText.Foreground = why != "" ? Gold : armed ? Green : Muted;
            armBtn.IsEnabled = why == "" || armed; armBtn.Content = armed ? "🔒 DISARM" : "🔓 ARM TRADING"; armBtn.Background = armed ? Tint(Green, 200) : (Brush)Tint(Orange, 200); armBtn.Foreground = Bg;
            foreach (var b in new[] { buyBtn, sellBtn }) b.Opacity = armed ? 1 : 0.5;
        }
        void SetArmed(bool on, string why)
        {
            if (on)
            {
                Mt5Account a; lock (bridge.State.Gate) a = bridge.State.Account;
                if (MessageBox.Show(this, "ARM trading on account " + a.Login + " (" + a.Kind + ")?\n\nFrom now on SpasiX's BUY / SELL, orders, closes, stops and targets go to MetaTrader 5" + (a.Kind == "REAL" ? " — REAL MONEY" : "") + ".\nIt stays armed until you DISARM or close SpasiX.", "SpasiX • ARM", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            armed = on; Toast(on ? "🟢 ARMED — trading from SpasiX is on" : "🔒 DISARMED" + (why != null ? " — " + why : ""));
        }
        bool Ask(string msg) { return MessageBox.Show(this, msg, "SpasiX", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes; }
        void Toast(string msg)
        {
            toastText.Text = msg; toastBox.Visibility = Visibility.Visible; toastBox.Opacity = 1;
            var an = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromSeconds(3.5) }; an.Completed += delegate { if (toastBox.Opacity < 0.05) toastBox.Visibility = Visibility.Collapsed; }; toastBox.BeginAnimation(UIElement.OpacityProperty, an);
        }

        // ---------------------------------------------------------------- the trading commands (only armed, only through the bridge)
        bool Cmd(string label, string cmd, params object[] args)
        {
            bool can; lock (bridge.State.Gate) can = bridge.State.CanTrade;
            if (!armed || !can) { Toast("🔒 " + (!can ? "trading is not allowed by the bridge yet (⚙ SETUP step 4)" : "press 🔓 ARM TRADING in the panel first")); return false; }
            var id = bridge.Command(cmd, args); if (id == null) { Toast("⚠ MT5 is not connected"); return false; }
            sent[id] = label; Log("→ " + label, Muted); return true;
        }
        double Num(TextBox b) { double v; return double.TryParse(b.Text.Replace(',', '.'), NumberStyles.Float, IC, out v) ? v : 0; }
        void Market(int dir)
        {
            var p = Act; double lots = Num(lotsBox); if (lots <= 0) { Toast("type the lots first"); return; }
            Mt5Quote q; lock (bridge.State.Gate) bridge.State.Quotes.TryGetValue(p.Sym, out q); if (q == null) { Toast("no price for " + p.Sym + " yet"); return; }
            double px = dir > 0 ? q.Ask : q.Bid, sd = Num(slBox), td = Num(tpBox); double sl = sd > 0 ? px - dir * sd : 0, tp = td > 0 ? px + dir * td : 0; string f = "F" + p.V.Digits;
            string what = (dir > 0 ? "BUY " : "SELL ") + lots.ToString("0.00", IC) + " " + p.Sym + " at market ≈ " + px.ToString(f, IC) + (sl > 0 ? " • S/L " + sl.ToString(f, IC) : "") + (tp > 0 ? " • T/P " + tp.ToString(f, IC) : "");
            if ((oneClick.IsChecked != true || lots > 2) && !Ask(what + "?")) return;
            Cmd(what, "ORDER", p.Sym, dir > 0 ? "BUY" : "SELL", lots, 0.0, sl, tp);
        }
        void PlacePending(string sym, string kind, double price)
        {
            double lots = Num(lotsBox); if (lots <= 0 || string.IsNullOrEmpty(kind)) { Toast("type the lots first"); return; }
            int dir = kind.StartsWith("BUY") ? 1 : -1; double sd = Num(slBox), td = Num(tpBox); double sl = sd > 0 ? price - dir * sd : 0, tp = td > 0 ? price + dir * td : 0; string f = "F" + Act.V.Digits;
            string what = kind + " " + lots.ToString("0.00", IC) + " " + sym + " @ " + price.ToString(f, IC) + (sl > 0 ? " • S/L " + sl.ToString(f, IC) : "") + (tp > 0 ? " • T/P " + tp.ToString(f, IC) : "");
            if (!Ask(what + "?")) return;
            if (Cmd(what, "ORDER", sym, kind, lots, price, sl, tp)) { foreach (var pn in panes) { pn.V.PickKind = ""; pn.V.PickPrice = double.NaN; } }
        }
        void CloseAll(string sym, string which)
        {
            int n; double pl; lock (bridge.State.Gate) { var ps = bridge.State.Positions.Where(x => (sym == "" || string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)) && (which == "ALL" || (which == "BUY" && x.Dir > 0) || (which == "SELL" && x.Dir < 0) || (which == "PROFIT" && x.Profit + x.Swap > 0) || (which == "LOSS" && x.Profit + x.Swap < 0))).ToList(); n = ps.Count; pl = ps.Sum(x => x.Profit + x.Swap); }
            if (n == 0) { Toast("nothing to close"); return; }
            string what = "close " + n + " ticket" + (n == 1 ? "" : "s") + (which == "ALL" ? "" : " (" + which.ToLowerInvariant() + ")") + (sym == "" ? " on EVERY symbol" : " on " + sym) + " — P/L " + Signed(pl, "USD");
            if (!Ask(what.Substring(0, 1).ToUpperInvariant() + what.Substring(1) + "?")) return;
            Cmd(what, "CLOSEALL", sym, which);
        }
        void Log(string msg, Brush fg)
        {
            logPanel.Children.Insert(0, new TextBlock { Text = DateTime.Now.ToString("HH:mm:ss", IC) + "  " + msg, Foreground = fg, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) });
            while (logPanel.Children.Count > 12) logPanel.Children.RemoveAt(logPanel.Children.Count - 1);
        }
        void ReadResults(Mt5State s)
        {
            for (; resSeen < s.Results.Count; resSeen++)
            {
                var r = s.Results[resSeen]; string label; sent.TryGetValue(r.Item1, out label);
                Log((r.Item2 ? "✓ " : "✖ ") + r.Item3, r.Item2 ? Green : Red); Toast((r.Item2 ? "✓ " : "✖ ") + r.Item3);
            }
            if (resSeen > s.Results.Count) resSeen = s.Results.Count;
        }
    }
}
