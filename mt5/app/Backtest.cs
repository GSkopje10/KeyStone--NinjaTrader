// SpasiX — the BACKTEST STUDIO: 📂 DATA (the symbol, the days, the start time, the account: balance, leverage, commission, spread, slippage, swap,
// stop-out; the days on this PC; download from MT5; ▶ START) and the REPLAY BAR on the chart (restart, play / pause, one candle, the speed, jump to
// London / the New York open / the next day, the clock in New York time, the progress). The replay itself is SimBroker (Sim.cs).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        TextBox btSym, btFrom, btTo, btWarm, btBal, btLev, btComm, btSpread, btSlip, btSwapL, btSwapS, btStopOut; TextBlock btStatus, btDays, btSpec; Border btBar, btBarFill; StackPanel btRuns;
        readonly List<Tuple<double, Button>> btStartBtns = new List<Tuple<double, Button>>(); double btStartNy = double.NaN; bool btRealSpread = true; Button btSpreadReal, btSpreadFixed;
        HistoryDownloader dl; DispatcherTimer dlTimer; bool dlThenStart;
        Button playBtn; TextBlock clockText, clockDay; Border progFill, progTrack; readonly List<Tuple<double, Button>> speedBtns = new List<Tuple<double, Button>>();
        static string RunsFile { get { return Path.Combine(Dir, "backtest-runs.txt"); } }

        FrameworkElement BuildBacktestData()
        {
            var sp = new StackPanel { MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 10) };
            var title = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 2) };
            title.Children.Add(new TextBlock { Text = "🧪 BACKTEST STUDIO", Foreground = Cyan, FontSize = 22, FontWeight = FontWeights.Bold });
            sp.Children.Add(title);
            sp.Children.Add(new TextBlock { Text = "MT5's own 1-minute candles with their spread, saved on this PC and replayed tick by tick through the same chart, panel and tabs as live. Nothing reaches your MT5 account.", Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 12) });
            var cols = new Grid(); cols.ColumnDefinitions.Add(new ColumnDefinition()); cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); cols.ColumnDefinitions.Add(new ColumnDefinition()); sp.Children.Add(cols);
            Func<string, SolidColorBrush, StackPanel> card = (h, col) => { var c = new StackPanel(); c.Children.Add(new TextBlock { Text = h, Foreground = col, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) }); return c; };
            Func<StackPanel, SolidColorBrush, Border> wrap = (c, col) => new Border { Background = Card, BorderBrush = Tint(col, 110), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12, 16, 14), Child = c, Margin = new Thickness(0, 0, 0, 12) };
            Func<string, string, FrameworkElement, FrameworkElement> field = (cap, tip, el) => { var r = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; var t = new TextBlock { Text = cap, Foreground = Text, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Width = 150, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip }; r.Children.Add(t); r.Children.Add(el); return r; };
            // 1 — what to test
            var c1 = card("1 • WHAT TO TEST", Cyan);
            var symRow = new StackPanel { Orientation = Orientation.Horizontal }; btSym = Sx.Input(Get("bt.sym", "XAUUSD"), 150); btSym.LostFocus += delegate { Put("bt.sym", btSym.Text.Trim()); PaintDays(); };
            var pickS = Sx.Drop("CHOOSE", Card, SymMenu); pickS.Margin = new Thickness(6, 0, 0, 0); symRow.Children.Add(btSym); symRow.Children.Add(pickS);
            c1.Children.Add(field("SYMBOL", "the MT5 symbol (as in Market Watch)", symRow));
            var dRow = new StackPanel { Orientation = Orientation.Horizontal }; var today = DateTime.Today;
            btFrom = Sx.Input(Get("bt.from", today.AddDays(-7).ToString("yyyy-MM-dd", IC)), 120); btTo = Sx.Input(Get("bt.to", today.AddDays(-1).ToString("yyyy-MM-dd", IC)), 120);
            dRow.Children.Add(btFrom); dRow.Children.Add(new TextBlock { Text = "  →  ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); dRow.Children.Add(btTo);
            btFrom.LostFocus += delegate { Put("bt.from", btFrom.Text.Trim()); PaintDays(); }; btTo.LostFocus += delegate { Put("bt.to", btTo.Text.Trim()); PaintDays(); };
            c1.Children.Add(field("DAYS (server dates)", "the first and the last day to replay (yyyy-mm-dd)", dRow));
            var quick = new WrapPanel { Margin = new Thickness(150, -2, 0, 8) };
            foreach (var q in new[] { Tuple.Create("LAST 5 DAYS", 5), Tuple.Create("LAST 10", 10), Tuple.Create("LAST 20", 20), Tuple.Create("THIS WEEK", -1), Tuple.Create("LAST WEEK", -2), Tuple.Create("LAST MONTH", -3) })
            {
                var qq = q; var b = Pill(q.Item1, Card); b.Height = 24; b.FontSize = 10.5; b.Padding = new Thickness(8, 0, 8, 0); b.Margin = new Thickness(0, 0, 4, 4);
                b.Click += delegate
                {
                    DateTime f, t; var d = DateTime.Today;
                    if (qq.Item2 > 0) { t = d.AddDays(-1); f = d; int n = 0; while (n < qq.Item2) { f = f.AddDays(-1); if (f.DayOfWeek != DayOfWeek.Saturday && f.DayOfWeek != DayOfWeek.Sunday) n++; } }
                    else if (qq.Item2 == -1) { f = d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); t = d; }
                    else if (qq.Item2 == -2) { var mon = d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); f = mon.AddDays(-7); t = mon.AddDays(-1); }
                    else { var first = new DateTime(d.Year, d.Month, 1); f = first.AddMonths(-1); t = first.AddDays(-1); }
                    btFrom.Text = f.ToString("yyyy-MM-dd", IC); btTo.Text = t.ToString("yyyy-MM-dd", IC); Put("bt.from", btFrom.Text); Put("bt.to", btTo.Text); PaintDays();
                };
                quick.Children.Add(b);
            }
            c1.Children.Add(quick);
            btWarm = Sx.Input(Get("bt.warm", "3"), 60); btWarm.LostFocus += delegate { Put("bt.warm", btWarm.Text); };
            var wr = new StackPanel { Orientation = Orientation.Horizontal }; wr.Children.Add(btWarm); wr.Children.Add(new TextBlock { Text = "  days before (drawn at once, for the context)", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            c1.Children.Add(field("WARM-UP", "candles shown before the replay starts", wr));
            var stR = new WrapPanel(); double sv; btStartNy = double.TryParse(Get("bt.start", "NaN"), NumberStyles.Float, IC, out sv) ? sv : double.NaN;
            foreach (var o in new[] { Tuple.Create(double.NaN, "SERVER 00:00 (NY 17:00)"), Tuple.Create(2.0, "LONDON (NY 02:00)"), Tuple.Create(8.0, "NY 08:00"), Tuple.Create(9.25, "NY 09:15") })
            { var oo = o; var b = Pill(o.Item2, Card); b.Height = 26; b.FontSize = 10.5; b.Padding = new Thickness(8, 0, 8, 0); b.Margin = new Thickness(0, 0, 4, 4); b.Click += delegate { btStartNy = oo.Item1; Put("bt.start", double.IsNaN(oo.Item1) ? "NaN" : oo.Item1.ToString("R", IC)); PaintStart(); }; stR.Children.Add(b); btStartBtns.Add(Tuple.Create(o.Item1, b)); }
            c1.Children.Add(field("START AT", "where the first day starts playing", stR)); PaintStart();
            btSpec = new TextBlock { Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }; c1.Children.Add(btSpec);
            var w1 = wrap(c1, Cyan); Grid.SetColumn(w1, 0); cols.Children.Add(w1);
            // 2 — the account
            var c2 = card("2 • THE ACCOUNT (simulated, hedging)", Gold);
            btBal = Sx.Input(Get("bt.bal", "10000"), 120); btLev = Sx.Input(Get("bt.lev", "500"), 80); btComm = Sx.Input(Get("bt.comm", "7"), 80); btSlip = Sx.Input(Get("bt.slip", "0"), 80);
            btSwapL = Sx.Input(Get("bt.swapl", "0"), 80); btSwapS = Sx.Input(Get("bt.swaps", "0"), 80); btStopOut = Sx.Input(Get("bt.so", "50"), 80); btSpread = Sx.Input(Get("bt.spread", "20"), 70);
            foreach (var t in new[] { Tuple.Create(btBal, "bt.bal"), Tuple.Create(btLev, "bt.lev"), Tuple.Create(btComm, "bt.comm"), Tuple.Create(btSlip, "bt.slip"), Tuple.Create(btSwapL, "bt.swapl"), Tuple.Create(btSwapS, "bt.swaps"), Tuple.Create(btStopOut, "bt.so"), Tuple.Create(btSpread, "bt.spread") }) { var tt = t; tt.Item1.LostFocus += delegate { Put(tt.Item2, tt.Item1.Text.Trim()); }; }
            Func<TextBox, string, FrameworkElement> withUnit = (b, u) => { var r = new StackPanel { Orientation = Orientation.Horizontal }; r.Children.Add(b); r.Children.Add(new TextBlock { Text = "  " + u, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }); return r; };
            c2.Children.Add(field("START BALANCE", "", withUnit(btBal, "USD")));
            c2.Children.Add(field("LEVERAGE", "", withUnit(btLev, "→ 1:500 (the margin and the stop-out)")));
            c2.Children.Add(field("COMMISSION", "per lot, round turn (IC Markets Raw: about $7)", withUnit(btComm, "$ per lot (half at the entry, half at the exit)")));
            var spR = new StackPanel { Orientation = Orientation.Horizontal }; btRealSpread = Get("bt.realspread", "1") == "1";
            btSpreadReal = Pill("REAL (MT5's candles)", Card); btSpreadReal.Height = 26; btSpreadReal.FontSize = 10.5; btSpreadFixed = Pill("FIXED", Card); btSpreadFixed.Height = 26; btSpreadFixed.FontSize = 10.5;
            btSpreadReal.Click += delegate { btRealSpread = true; Put("bt.realspread", "1"); PaintSpread(); }; btSpreadFixed.Click += delegate { btRealSpread = false; Put("bt.realspread", "0"); PaintSpread(); };
            spR.Children.Add(btSpreadReal); spR.Children.Add(btSpreadFixed); spR.Children.Add(btSpread); spR.Children.Add(new TextBlock { Text = "  points", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            c2.Children.Add(field("SPREAD", "REAL = the spread MT5 recorded on every 1-minute candle", spR)); PaintSpread();
            c2.Children.Add(field("SLIPPAGE", "added against you on market orders and stops", withUnit(btSlip, "points")));
            var swR = new StackPanel { Orientation = Orientation.Horizontal }; swR.Children.Add(new TextBlock { Text = "buy ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); swR.Children.Add(btSwapL); swR.Children.Add(new TextBlock { Text = "  sell ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); swR.Children.Add(btSwapS); swR.Children.Add(new TextBlock { Text = "  $ per lot per night (×3 Wed)", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            c2.Children.Add(field("SWAP", "charged at the server's midnight on positions held over it", swR));
            c2.Children.Add(field("STOP OUT", "", withUnit(btStopOut, "% margin level (the worst ticket closes)")));
            var w2 = wrap(c2, Gold); Grid.SetColumn(w2, 2); cols.Children.Add(w2);
            // 3 — the data and the start
            var c3 = card("3 • THE DATA AND THE START", Green);
            btDays = new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) }; c3.Children.Add(btDays);
            btBar = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Line, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
            btBarFill = new Border { CornerRadius = new CornerRadius(4), Background = new LinearGradientBrush(Cyan.Color, Green.Color, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; btBar.Child = btBarFill; c3.Children.Add(btBar);
            btStatus = new TextBlock { Foreground = Cyan, FontSize = 12.5, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) }; c3.Children.Add(btStatus);
            var br = new StackPanel { Orientation = Orientation.Horizontal };
            var down = Pill("⬇ DOWNLOAD FROM MT5", Card); down.Height = 38; down.Foreground = Cyan; down.ToolTip = "the missing days of the range (+ the warm-up) from MT5, saved on this PC"; down.Click += delegate { StartDownload(false); };
            var start = Pill("▶ START THE BACKTEST", Green); start.Height = 38; start.FontSize = 13.5; start.Foreground = Bg; start.Click += delegate { StartBacktest(); };
            br.Children.Add(start); br.Children.Add(down); c3.Children.Add(br);
            c3.Children.Add(new TextBlock { Text = "On the chart: SPACE = play / pause • → = one candle • the replay bar has the speed and the jumps. Trade with the panel, the chart's right-click and the lines — exactly as live.", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            sp.Children.Add(wrap(c3, Green));
            var c4 = card("🗂 PAST RUNS", Violet); btRuns = new StackPanel(); c4.Children.Add(btRuns); sp.Children.Add(wrap(c4, Violet));
            PaintDays(); PaintRuns();
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        List<Mi> SymMenu()
        {
            var l = new List<Mi>(); var saved = HistoryStore.Symbols();
            if (saved.Count > 0) { l.Add(Mi.H("on this PC")); foreach (var s in saved) { var ss = s; l.Add(new Mi("💾", s, () => { btSym.Text = ss; Put("bt.sym", ss); PaintDays(); }) { Hint = HistoryStore.Days(s).Count + " days" }); } }
            List<string> mt; lock (live.State.Gate) mt = live.State.Quotes.Keys.OrderBy(x => x).ToList();
            if (mt.Count > 0) { l.Add(Mi.H("MT5 Market Watch")); foreach (var s in mt.Where(x => !saved.Contains(x, StringComparer.OrdinalIgnoreCase)).Take(30)) { var ss = s; l.Add(new Mi("📈", s, () => { btSym.Text = ss; Put("bt.sym", ss); PaintDays(); })); } }
            if (l.Count == 0) l.Add(new Mi { Text = "connect MT5 (LIVE needs nothing else) to list the symbols", Off = true });
            return l;
        }
        void PaintStart() { foreach (var t in btStartBtns) { bool on = (double.IsNaN(t.Item1) && double.IsNaN(btStartNy)) || t.Item1 == btStartNy; t.Item2.Background = on ? Tint(Cyan, 190) : (Brush)Card; t.Item2.Foreground = on ? Bg : Text; } }
        void PaintSpread() { btSpreadReal.Background = btRealSpread ? Tint(Cyan, 190) : (Brush)Card; btSpreadReal.Foreground = btRealSpread ? Bg : Text; btSpreadFixed.Background = !btRealSpread ? Tint(Cyan, 190) : (Brush)Card; btSpreadFixed.Foreground = !btRealSpread ? Bg : Text; btSpread.IsReadOnly = btRealSpread; btSpread.Opacity = btRealSpread ? 0.35 : 1; }
        bool Range(out DateTime f, out DateTime t)
        {
            bool a = DateTime.TryParseExact(btFrom.Text.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out f), b = DateTime.TryParseExact(btTo.Text.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out t);
            if (!a || !b || t < f) return false; return true;
        }
        int Warm() { int w = IntOf(btWarm.Text, 3); return Math.Max(0, Math.Min(30, w)); }
        void PaintDays()
        {
            if (btDays == null) return; var sym = btSym.Text.Trim(); DateTime f, t;
            var have = HistoryStore.Days(sym); var spec = HistoryStore.LoadSpec(sym);
            if (!Range(out f, out t)) { btDays.Text = "type the days as yyyy-mm-dd (FROM ≤ TO)"; btDays.Foreground = Red; return; }
            int need = 0, got = 0; for (var d = f.AddDays(-Warm()); d <= t; d = d.AddDays(1)) { if (d.DayOfWeek == DayOfWeek.Saturday) continue; need++; if (HistoryStore.HasDay(sym, d)) got++; }
            btDays.Text = "💾 " + sym + ": " + have.Count + " day(s) on this PC" + (have.Count > 0 ? " (" + have.First().ToString("dd MMM yyyy", IC) + " → " + have.Last().ToString("dd MMM yyyy", IC) + ")" : "") + "   •   this test needs " + need + " (with the warm-up): " + got + " here" + (got < need ? ", " + (need - got) + " to download" : " ✓");
            btDays.Foreground = got >= need ? Green : Text;
            btSpec.Text = spec != null ? "spec: " + spec.Name + " • " + spec.Digits + " digits • contract " + spec.Contract.ToString("0.##", IC) + " • $" + (spec.TickValue / spec.TickSize * spec.Point).ToString("0.####", IC) + " per point per lot • lots " + spec.VolMin.ToString("0.##", IC) + "–" + spec.VolMax.ToString("0.##", IC) + " • NY = server " + spec.NyOffsetHours.ToString("+0;−0", IC) + " h" : "the symbol's spec (contract, digits, $ per point) comes from MT5 with the first download";
        }
        SimSpec SpecNow(string sym)
        {
            Mt5Symbol ms = null; int off = 10800, ny = -7; string cur = "USD";
            lock (live.State.Gate) { if (live.State.Alive) live.State.Symbols.TryGetValue(sym, out ms); off = live.State.ServerOffsetSec; cur = live.State.Account.Currency; }
            if (ms != null)
            {
                try { var et = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); ny = (int)Math.Round(et.GetUtcOffset(DateTime.UtcNow).TotalHours - off / 3600.0); } catch { }
                var sp = SimSpec.From(ms, off, ny, cur); HistoryStore.SaveSpec(sp); return sp;
            }
            return HistoryStore.LoadSpec(sym);
        }
        void StartDownload(bool thenStart)
        {
            var sym = btSym.Text.Trim(); DateTime f, t; if (sym == "" || !Range(out f, out t)) { btStatus.Text = "✖ choose a symbol and the days first"; btStatus.Foreground = Red; return; }
            bool alive; lock (live.State.Gate) alive = live.State.Alive && live.State.BridgeVersion >= 4;
            if (!alive) { btStatus.Text = "✖ MT5 with the SpasiXBridge v4 is needed to download (⌂ MODES ▸ LIVE ▸ SETUP shows how) — the days already on this PC still work"; btStatus.Foreground = Red; if (thenStart) LoadAndStart(); return; }
            lock (live.State.Gate) if (!live.State.Symbols.ContainsKey(sym)) live.Send("WATCH|" + sym);
            SpecNow(sym);
            dl = new HistoryDownloader(live, sym, f.AddDays(-Warm()), t); dlThenStart = thenStart; btBar.Visibility = Visibility.Visible;
            if (dlTimer == null) { dlTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) }; dlTimer.Tick += delegate { DownloadTick(); }; }
            dlTimer.Start();
        }
        void DownloadTick()
        {
            if (dl == null) { dlTimer.Stop(); return; }
            dl.Step(DateTime.Now); btStatus.Text = dl.Status; btStatus.Foreground = dl.Failed ? Red : dl.Done ? Green : Cyan; btBarFill.Width = Math.Max(0, btBar.ActualWidth * dl.Progress);
            if (dl.Done) { dlTimer.Stop(); PaintDays(); if (SpecNow(btSym.Text.Trim()) != null) PaintDays(); bool go = dlThenStart; dl = null; if (go) LoadAndStart(); }
        }
        void StartBacktest()
        {
            var sym = btSym.Text.Trim(); DateTime f, t; if (sym == "" || !Range(out f, out t)) { btStatus.Text = "✖ choose a symbol and the days first"; btStatus.Foreground = Red; return; }
            int missing = 0; for (var d = f.AddDays(-Warm()); d <= t; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && !HistoryStore.HasDay(sym, d)) missing++;
            bool alive; lock (live.State.Gate) alive = live.State.Alive && live.State.BridgeVersion >= 4;
            if (missing > 0 && alive) { StartDownload(true); return; }
            LoadAndStart();
        }
        void LoadAndStart()
        {
            var sym = btSym.Text.Trim(); DateTime f, t; if (!Range(out f, out t)) return;
            var spec = SpecNow(sym); if (spec == null) { btStatus.Text = "✖ no spec for " + sym + " — connect MT5 once and press ⬇ DOWNLOAD"; btStatus.Foreground = Red; return; }
            var m1 = HistoryStore.LoadRange(sym, f.AddDays(-Warm()), t);
            if (m1.Count == 0) { btStatus.Text = "✖ no candles of " + sym + " for these days on this PC — ⬇ DOWNLOAD FROM MT5 first"; btStatus.Foreground = Red; return; }
            if (sim.Spec != null && sim.State.Deals.Count(d => d.IsExit) > 0) SaveRun();
            Func<TextBox, double, double> num = (b, d) => { double v; return double.TryParse(b.Text.Replace(',', '.'), NumberStyles.Float, IC, out v) ? v : d; };
            var st = new SimSettings { Symbol = sym, From = f, To = t, WarmDays = Warm(), StartNyHour = btStartNy, Balance = Math.Max(100, num(btBal, 10000)), Leverage = (int)Math.Max(1, num(btLev, 500)), CommissionPerLot = Math.Max(0, num(btComm, 7)),
                FixedSpread = btRealSpread ? -1 : (int)Math.Max(0, num(btSpread, 20)), SlipPts = (int)Math.Max(0, num(btSlip, 0)), SwapLong = num(btSwapL, 0), SwapShort = num(btSwapS, 0), StopOutPct = Math.Max(0, num(btStopOut, 50)), Currency = spec.Currency };
            string err; lock (sim.State.Gate) err = sim.Load(st, spec, m1);
            if (err != "") { btStatus.Text = "✖ " + err; btStatus.Foreground = Red; return; }
            resSeen = 0; notesSeen = 0; logPanel.Children.Clear();
            foreach (var p in panes) { p.Sym = ""; }
            foreach (var p in panes) SetPane(p, sym, p.Tf); symSig = "";
            btStatus.Text = "✓ loaded " + m1.Count.ToString("N0", IC) + " one-minute candles • " + sim.DaysTotal + " day(s) to replay from " + sim.StartTime.ToString("ddd dd MMM HH:mm", IC) + " server"; btStatus.Foreground = Green;
            Show("CHART"); Toast("🧪 " + sym + " loaded — ▶ PLAY (Space) • → one candle • trade with the panel"); trackSig = "";
        }
        void SaveRun()
        {
            try
            {
                var s = sim.State; var ex = s.Deals.Where(d => d.IsExit).ToList(); if (ex.Count == 0) return; double net = s.Deals.Where(d => d.IsTrade).Sum(d => d.Net); int w = ex.Count(d => d.Net > 0);
                Directory.CreateDirectory(Dir);
                File.AppendAllLines(RunsFile, new[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + "|" + sim.Spec.Name + "|" + sim.Set.From.ToString("yyyy-MM-dd", IC) + "|" + sim.Set.To.ToString("yyyy-MM-dd", IC) + "|" + ex.Count + "|" + net.ToString("0.00", IC) + "|" + (100.0 * w / ex.Count).ToString("0", IC) + "|" + sim.Set.Balance.ToString("0", IC) });
                PaintRuns();
            }
            catch { }
        }
        void PaintRuns()
        {
            if (btRuns == null) return; btRuns.Children.Clear(); string[] lines = new string[0]; try { if (File.Exists(RunsFile)) lines = File.ReadAllLines(RunsFile); } catch { }
            if (lines.Length == 0) { btRuns.Children.Add(new TextBlock { Text = "no run yet — a run is saved here when you start another one or leave the backtest", Foreground = Muted, FontSize = 12 }); return; }
            foreach (var ln in lines.Reverse().Take(12))
            {
                var a = ln.Split('|'); if (a.Length < 8) continue; double net; double.TryParse(a[5], NumberStyles.Float, IC, out net);
                var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                r.Children.Add(Cell(a[0], Muted, 130)); r.Children.Add(Cell(a[1], Cyan, 90, false, true)); r.Children.Add(Cell(a[2] + " → " + a[3], Text, 200)); r.Children.Add(Cell(a[4] + " trades", Text, 90)); r.Children.Add(Cell(a[6] + " % won", Text, 90)); r.Children.Add(Cell(Signed(net, "USD"), net >= 0 ? Green : Red, 110, true, true)); r.Children.Add(Cell("on $" + a[7], Muted, 90));
                btRuns.Children.Add(r);
            }
        }

        // ---------------------------------------------------------------- the replay bar
        FrameworkElement BuildReplayBar()
        {
            var w = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            var restart = Pill("⏮", Card); restart.ToolTip = "restart the backtest from its first candle (the trades are cleared)"; restart.Click += delegate { RestartSim(); }; w.Children.Add(restart);
            playBtn = Pill("▶ PLAY", Green); playBtn.Foreground = Bg; playBtn.MinWidth = 96; playBtn.ToolTip = "play / pause (Space)"; playBtn.Click += delegate { TogglePlay(); }; w.Children.Add(playBtn);
            var step = Pill("⏭ CANDLE", Card); step.ToolTip = "one candle of the selected chart's timeframe further (→)"; step.Click += delegate { StepCandle(); }; w.Children.Add(step);
            w.Children.Add(Sep());
            double cur; double.TryParse(Get("bt.speed", "10"), NumberStyles.Float, IC, out cur); if (cur <= 0) cur = 10; sim.Speed = cur;
            foreach (var sp in new[] { 1.0, 2, 5, 10, 30, 60, 120, 300, 1000, SimBroker.MaxSpeed })
            {
                double v = sp; var b = Pill(v >= SimBroker.MaxSpeed ? "MAX" : v + "×", Card); b.Padding = new Thickness(7, 0, 7, 0); b.MinWidth = 34; b.ToolTip = v >= SimBroker.MaxSpeed ? "as fast as possible" : v == 1 ? "real time" : v + " × real time";
                b.Click += delegate { sim.Speed = v; Put("bt.speed", v.ToString("R", IC)); PaintSpeed(); }; w.Children.Add(b); speedBtns.Add(Tuple.Create(v, b));
            }
            w.Children.Add(Sep());
            var lon = Pill("⏩ LONDON", Card); lon.ToolTip = "jump to the next London open (NY 03:00)"; lon.Click += delegate { Jump(sim.NextNy(3)); }; w.Children.Add(lon);
            var nyo = Pill("⏩ NY OPEN", Card); nyo.ToolTip = "jump to the next New York open (09:30) minus 5 minutes"; nyo.Click += delegate { Jump(sim.NextNy(9.5 - 5 / 60.0)); }; w.Children.Add(nyo);
            var nd = Pill("⏩ NEXT DAY", Card); nd.ToolTip = "jump to the next server day (NY 17:00)"; nd.Click += delegate { Jump(sim.NextDayStart()); }; w.Children.Add(nd);
            w.Children.Add(Sep());
            var clk = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 0) };
            clockText = new TextBlock { Text = "📂 load a backtest in the DATA tab", Foreground = Gold, FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono }; clk.Children.Add(clockText);
            clockDay = new TextBlock { Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.SemiBold }; clk.Children.Add(clockDay); w.Children.Add(clk);
            progTrack = new Border { Width = 220, Height = 8, CornerRadius = new CornerRadius(4), Background = Line, VerticalAlignment = VerticalAlignment.Center, ToolTip = "how far the replay is" };
            progFill = new Border { CornerRadius = new CornerRadius(4), Background = new LinearGradientBrush(Cyan.Color, Gold.Color, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; progTrack.Child = progFill; w.Children.Add(progTrack);
            PaintSpeed();
            return new Border { Background = new LinearGradientBrush(Sx.C("#102033"), Sx.C("#0D1524"), 0), BorderBrush = Tint(Cyan, 110), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 6), Child = w };
        }
        void PaintSpeed() { foreach (var t in speedBtns) { bool on = Math.Abs(t.Item1 - sim.Speed) < 1e-9; t.Item2.Background = on ? Tint(Cyan, 200) : (Brush)Card; t.Item2.Foreground = on ? Bg : Text; } }
        bool SimReady() { if (sim.Spec == null) { Toast("📂 load a backtest in the DATA tab first"); return false; } return true; }
        void TogglePlay() { if (!SimReady()) return; if (sim.Ended) { Toast("the end of the data — ⏮ restarts it"); return; } sim.Playing = !sim.Playing; simLast = DateTime.Now; }
        void StepCandle() { if (!SimReady()) return; sim.Playing = false; sim.StepBar(Act.Tf); foreach (var p in panes.Take(split)) p.V.InvalidateVisual(); }
        void Jump(DateTime t) { if (!SimReady()) return; bool was = sim.Playing; sim.Playing = false; sim.JumpTo(t); sim.Playing = was; Toast("⏩ " + t.AddHours(sim.Spec.NyOffsetHours).ToString("ddd dd MMM HH:mm", IC) + " NY"); }
        void RestartSim()
        {
            if (!SimReady()) return; int n; lock (sim.State.Gate) n = sim.State.Deals.Count(d => d.Entry == "IN");
            if (n > 0 && !Sx.Ask(this, "⏮ Restart", "Restart from the first candle? The " + n + " trade" + (n == 1 ? "" : "s") + " of this run are saved in PAST RUNS and cleared.", "RESTART", "NO")) return;
            if (n > 0) SaveRun(); string err; lock (sim.State.Gate) err = sim.Restart(); resSeen = 0; notesSeen = 0; logPanel.Children.Clear(); trackSig = "";
            if (err != "") Toast("✖ " + err); else Toast("⏮ back to " + sim.StartTime.ToString("ddd dd MMM HH:mm", IC) + " server");
        }
        void RefreshSimHeader(Mt5State s)
        {
            if (sim.Spec == null) { connText.Text = "🧪 NO DATA YET — 📂 DATA"; connPill.Background = Gold; acLogin.Text = "BACKTEST"; acName.Text = ""; acRest.Text = "choose a symbol and the days in 📂 DATA, then ▶ START"; foreach (var c in cards.Values) c.Item2.Text = "—"; return; }
            connText.Text = (sim.Ended ? "■ ENDED" : sim.Playing ? "▶ PLAYING " + (sim.Speed >= SimBroker.MaxSpeed ? "MAX" : sim.Speed + "×") : "⏸ PAUSED") + " • " + sim.Spec.Name; connPill.Background = sim.Playing ? Green : sim.Ended ? Red : Cyan;
            acLogin.Text = "BACKTEST"; acName.Text = sim.Spec.Name;
            acRest.Text = "•  " + sim.Set.From.ToString("dd MMM", IC) + " → " + sim.Set.To.ToString("dd MMM yyyy", IC) + "  •  HEDGING  •  1:" + sim.Set.Leverage + "  •  $" + sim.Set.CommissionPerLot.ToString("0.##", IC) + " / lot  •  spread " + (sim.Set.FixedSpread >= 0 ? sim.Set.FixedSpread + " pts" : "REAL") + "  •  start $" + sim.Set.Balance.ToString("N0", IC);
        }
        void FillSim(Mt5State s)
        {
            if (playBtn == null) return;
            playBtn.Content = sim.Playing ? "⏸ PAUSE" : sim.Ended ? "■ ENDED" : "▶ PLAY"; playBtn.Background = sim.Playing ? (Brush)Tint(Gold, 220) : sim.Ended ? Card : Green; playBtn.Foreground = sim.Ended ? Muted : Bg;
            if (sim.Spec == null) return;
            var ny = sim.Clock.AddHours(sim.Spec.NyOffsetHours);
            clockText.Text = ny.ToString("ddd dd MMM yyyy  HH:mm:ss", IC) + " NY";
            clockDay.Text = "day " + Math.Max(1, sim.DayNumber) + " / " + sim.DaysTotal + "  •  server " + sim.Clock.ToString("HH:mm", IC) + "  •  " + (100 * sim.Progress).ToString("0", IC) + " %";
            progFill.Width = Math.Max(0, (progTrack.ActualWidth > 0 ? progTrack.ActualWidth : 220) * sim.Progress);
        }
    }
}
