// SpasiX — the BACKTEST STUDIO: 📂 DATA = QUICK START (the symbols — gold, NAS100, bitcoin … together —, the days, RAW or STANDARD, the balance,
// ▶ START; MT5's spec of every symbol one click away; MORE SETTINGS folded) and the REPLAY BAR on the chart (restart, back / forward one minute,
// one candle, one day, play / pause, the speed, London / the New York open, 📅 GO TO a date and time or change the range, the clock in New York
// time, how much of the replay is MT5's real ticks, the progress). The replay itself is SimBroker (Sim.cs); the saved days are the POOL (Pool.cs).
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
        TextBox btStartBox; HistoryDownloader bgDl; readonly Queue<string> bgQueue = new Queue<string>(); string bgStatus = "";
        TextBox btFrom, btTo, btWarm, btBal, btLev, btComm, btMarkup, btSpread, btSlip, btSwapL, btSwapS, btStopOut; TextBlock btStatus, btDays, btSpec, btKindHint; Border btBar, btBarFill; StackPanel btRuns, btMore; WrapPanel btSymChips;
        readonly List<Tuple<double, Button>> btStartBtns = new List<Tuple<double, Button>>(); double btStartNy = double.NaN; bool btRealSpread = true, btTicks = true; Button btSpreadReal, btSpreadFixed, btRawBtn, btStdBtn, btTicksBtn, btMoreBtn; string btKind = "RAW";
        readonly List<string> btSyms = new List<string>(); readonly Queue<string> dlQueue = new Queue<string>(); int dlTotal;
        HistoryDownloader dl; DispatcherTimer dlTimer; bool dlThenStart;
        Button playBtn; TextBlock clockText, clockDay, realText; Border progFill, progTrack; readonly List<Tuple<double, Button>> speedBtns = new List<Tuple<double, Button>>();
        static string RunsFile { get { return Path.Combine(Dir, "backtest-runs.txt"); } }
        static readonly string[] BtSuggest = { "XAUUSD", "USTEC", "BTCUSD", "US30", "US500", "EURUSD", "GBPUSD", "XAGUSD" };

        FrameworkElement BuildBacktestData()
        {
            var sp = new StackPanel { MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 10) };
            sp.Children.Add(new TextBlock { Text = "🧪 BACKTEST STUDIO", Foreground = Cyan, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 0, 2) });
            sp.Children.Add(new TextBlock { Text = "Three steps: the symbols, the days, the account → ▶ START. MT5's own candles and REAL TICKS are downloaded once and kept on this PC; then everything replays on the same chart, panel and tabs as live. Nothing reaches your MT5 account.", Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 12) });
            Func<string, SolidColorBrush, StackPanel> card = (h, col) => { var c = new StackPanel(); c.Children.Add(new TextBlock { Text = h, Foreground = col, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) }); return c; };
            Func<StackPanel, SolidColorBrush, Border> wrap = (c, col) => new Border { Background = Card, BorderBrush = Tint(col, 110), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12, 16, 14), Child = c, Margin = new Thickness(0, 0, 0, 12) };
            Func<string, string, FrameworkElement, FrameworkElement> field = (cap, tip, el) => { var r = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; var t = new TextBlock { Text = cap, Foreground = Text, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Width = 150, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip }; r.Children.Add(t); r.Children.Add(el); return r; };
            Func<string, Button> chip = t => { var b = Pill(t, Card); b.Height = 28; b.FontSize = 11; b.Padding = new Thickness(10, 0, 10, 0); b.Margin = new Thickness(0, 0, 5, 5); return b; };
            // 1 — the symbols (several at once)
            var c1 = card("1 • THE SYMBOLS  (tick one or more — they replay together on one account)", Cyan);
            btSyms.AddRange(Get("bt.syms", Get("bt.sym", "XAUUSD")).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x != "").Distinct(StringComparer.OrdinalIgnoreCase));
            btSymChips = new WrapPanel(); c1.Children.Add(btSymChips);
            var symTools = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            var addS = chip("+ ANY SYMBOL"); addS.Foreground = Cyan; addS.Click += delegate { var s = Sx.Prompt(this, "+ a symbol", "The MT5 symbol (as in Market Watch), e.g. XAUUSD, USTEC, BTCUSD:", ""); if (!string.IsNullOrWhiteSpace(s)) { s = s.Trim().ToUpperInvariant(); if (!btSyms.Contains(s, StringComparer.OrdinalIgnoreCase)) btSyms.Add(s); SaveSyms(); } }; symTools.Children.Add(addS);
            var specB = chip("ⓘ MT5 SPEC — contract, swap, margin, commission"); specB.Foreground = Gold; specB.Click += delegate { SxMenu.Show(specB, new Point(0, specB.ActualHeight + 4), btSyms.Select(x => { var xx = x; return new Mi("ⓘ", xx, () => ShowSpec(xx)); }).DefaultIfEmpty(new Mi { Text = "tick a symbol first", Off = true }).ToList()); }; symTools.Children.Add(specB);
            c1.Children.Add(symTools);
            btSpec = new TextBlock { Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }; c1.Children.Add(btSpec);
            sp.Children.Add(wrap(c1, Cyan));
            // 2 — the days
            var c2 = card("2 • THE DAYS", Violet);
            var quick = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var q in new[] { Tuple.Create("YESTERDAY", 1), Tuple.Create("LAST 5 DAYS", 5), Tuple.Create("LAST 10", 10), Tuple.Create("LAST 20", 20), Tuple.Create("THIS WEEK", -1), Tuple.Create("LAST WEEK", -2), Tuple.Create("LAST MONTH", -3) })
            {
                var qq = q; var b = chip(q.Item1);
                b.Click += delegate
                {
                    DateTime f, t; var d = DateTime.Today;
                    if (qq.Item2 > 0) { t = d.AddDays(-1); while (t.DayOfWeek == DayOfWeek.Saturday || t.DayOfWeek == DayOfWeek.Sunday) t = t.AddDays(-1); f = t; int n = 1; while (n < qq.Item2) { f = f.AddDays(-1); if (f.DayOfWeek != DayOfWeek.Saturday && f.DayOfWeek != DayOfWeek.Sunday) n++; } }
                    else if (qq.Item2 == -1) { f = d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); t = d; }
                    else if (qq.Item2 == -2) { var mon = d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); f = mon.AddDays(-7); t = mon.AddDays(-1); }
                    else { var first = new DateTime(d.Year, d.Month, 1); f = first.AddMonths(-1); t = first.AddDays(-1); }
                    SetRange(f, t);
                };
                quick.Children.Add(b);
            }
            c2.Children.Add(quick);
            var dRow = new StackPanel { Orientation = Orientation.Horizontal }; var today = DateTime.Today;
            btFrom = Sx.Input(Get("bt.from", today.AddDays(-7).ToString("yyyy-MM-dd", IC)), 120); btTo = Sx.Input(Get("bt.to", today.AddDays(-1).ToString("yyyy-MM-dd", IC)), 120);
            dRow.Children.Add(btFrom); dRow.Children.Add(new TextBlock { Text = "  →  ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); dRow.Children.Add(btTo);
            btFrom.LostFocus += delegate { Put("bt.from", btFrom.Text.Trim()); PaintDays(); }; btTo.LostFocus += delegate { Put("bt.to", btTo.Text.Trim()); PaintDays(); };
            c2.Children.Add(field("FROM → TO", "the first and the last server day (yyyy-mm-dd) — also changeable later from the chart (📅)", dRow));
            var stR = new WrapPanel(); double sv; btStartNy = double.TryParse(Get("bt.start", "NaN"), NumberStyles.Float, IC, out sv) ? sv : double.NaN;
            foreach (var o in new[] { Tuple.Create(double.NaN, "THE DAY'S START"), Tuple.Create(2.0, "LONDON (NY 02:00)"), Tuple.Create(8.0, "NY 08:00"), Tuple.Create(9.25, "NY 09:15") })
            { var oo = o; var b = chip(o.Item2); b.Click += delegate { btStartNy = oo.Item1; Put("bt.start", double.IsNaN(oo.Item1) ? "NaN" : oo.Item1.ToString("R", IC)); PaintStart(); }; stR.Children.Add(b); btStartBtns.Add(Tuple.Create(o.Item1, b)); }
            btStartBox = Sx.Input(double.IsNaN(btStartNy) ? "" : TimeOfNy(btStartNy), 74); btStartBox.ToolTip = "your own start time, New York (HH:mm) — Enter / leave the box"; btStartBox.Margin = new Thickness(6, 0, 0, 4); btStartBox.Height = 26; btStartBox.FontSize = 12.5;
            Action setCustom = () => { TimeSpan ts; var tx = btStartBox.Text.Trim(); if (tx == "") return; if (!TimeSpan.TryParse(tx.Contains(":") ? tx : tx + ":00", IC, out ts) || ts.TotalHours >= 24) { btStatus.Text = "✖ the start time is HH:mm (New York), e.g. 07:45"; btStatus.Foreground = Red; return; } btStartNy = ts.TotalHours; Put("bt.start", btStartNy.ToString("R", IC)); PaintStart(); };
            btStartBox.LostFocus += delegate { setCustom(); }; btStartBox.KeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Enter) setCustom(); };
            var cst = new StackPanel { Orientation = Orientation.Horizontal }; cst.Children.Add(new TextBlock { Text = "  or NY", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 4) }); cst.Children.Add(btStartBox); stR.Children.Add(cst);
            c2.Children.Add(field("START PLAYING AT", "where the first day starts playing (the hours before are drawn at once)", stR)); PaintStart();
            btDays = new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }; c2.Children.Add(btDays);
            sp.Children.Add(wrap(c2, Violet));
            // 3 — the account: RAW / STANDARD + the balance (the rest folded under MORE)
            var c3 = card("3 • THE ACCOUNT  (simulated, hedging)", Gold);
            btKind = Get("bt.kind", "RAW") == "STANDARD" ? "STANDARD" : "RAW";
            var kr = new WrapPanel(); btRawBtn = chip("RAW — no commission, wider spread"); btStdBtn = chip("STANDARD — commission, spread near 0"); btRawBtn.Height = btStdBtn.Height = 32;
            btRawBtn.Click += delegate { btKind = "RAW"; Put("bt.kind", "RAW"); PaintKind(); }; btStdBtn.Click += delegate { btKind = "STANDARD"; Put("bt.kind", "STANDARD"); PaintKind(); };
            kr.Children.Add(btRawBtn); kr.Children.Add(btStdBtn); c3.Children.Add(field("ACCOUNT TYPE", "like IC Markets' Raw Spread and Standard accounts", kr));
            btComm = Sx.Input(Get("bt.comm", "7"), 80); btMarkup = Sx.Input(Get("bt.markup", "10"), 80); btBal = Sx.Input(Get("bt.bal", "10000"), 120);
            Func<TextBox, string, FrameworkElement> withUnit = (b, u) => { var r = new StackPanel { Orientation = Orientation.Horizontal }; r.Children.Add(b); r.Children.Add(new TextBlock { Text = "  " + u, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }); return r; };
            var commRow = field("COMMISSION", "STANDARD: per lot, round turn", withUnit(btComm, "$ per lot round turn (half in, half out) — STANDARD only")); var markRow = field("EXTRA SPREAD", "RAW: added to MT5's spread", withUnit(btMarkup, "points added to the spread — RAW only (RAW has no commission)"));
            c3.Children.Add(commRow); c3.Children.Add(markRow);
            btKindHint = new TextBlock { Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(150, -4, 0, 8) }; c3.Children.Add(btKindHint);
            c3.Children.Add(field("START BALANCE", "", withUnit(btBal, "USD")));
            btMoreBtn = chip("▸ MORE SETTINGS (leverage, spread, slippage, swap, stop-out, warm-up, real ticks)"); btMoreBtn.Foreground = Muted; c3.Children.Add(btMoreBtn);
            btMore = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) }; c3.Children.Add(btMore);
            btMoreBtn.Click += delegate { bool on = btMore.Visibility != Visibility.Visible; btMore.Visibility = on ? Visibility.Visible : Visibility.Collapsed; btMoreBtn.Content = (on ? "▾" : "▸") + " MORE SETTINGS (leverage, spread, slippage, swap, stop-out, warm-up, real ticks)"; };
            btLev = Sx.Input(Get("bt.lev", "500"), 80); btSlip = Sx.Input(Get("bt.slip", "0"), 80); btSwapL = Sx.Input(Get("bt.swapl", "0"), 80); btSwapS = Sx.Input(Get("bt.swaps", "0"), 80); btStopOut = Sx.Input(Get("bt.so", "50"), 80); btSpread = Sx.Input(Get("bt.spread", "20"), 70); btWarm = Sx.Input(Get("bt.warm", "3"), 60);
            foreach (var t in new[] { Tuple.Create(btBal, "bt.bal"), Tuple.Create(btLev, "bt.lev"), Tuple.Create(btComm, "bt.comm"), Tuple.Create(btMarkup, "bt.markup"), Tuple.Create(btSlip, "bt.slip"), Tuple.Create(btSwapL, "bt.swapl"), Tuple.Create(btSwapS, "bt.swaps"), Tuple.Create(btStopOut, "bt.so"), Tuple.Create(btSpread, "bt.spread"), Tuple.Create(btWarm, "bt.warm") }) { var tt = t; tt.Item1.LostFocus += delegate { Put(tt.Item2, tt.Item1.Text.Trim()); PaintDays(); }; }
            btMore.Children.Add(field("LEVERAGE  1:", "the margin and the stop-out (MT5's own margin per symbol is used when known)", withUnit(btLev, "")));
            var spR = new StackPanel { Orientation = Orientation.Horizontal }; btRealSpread = Get("bt.realspread", "1") == "1";
            btSpreadReal = chip("REAL (MT5's)"); btSpreadFixed = chip("FIXED");
            btSpreadReal.Click += delegate { btRealSpread = true; Put("bt.realspread", "1"); PaintSpread(); }; btSpreadFixed.Click += delegate { btRealSpread = false; Put("bt.realspread", "0"); PaintSpread(); };
            spR.Children.Add(btSpreadReal); spR.Children.Add(btSpreadFixed); spR.Children.Add(btSpread); spR.Children.Add(new TextBlock { Text = "  points", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            btMore.Children.Add(field("SPREAD", "REAL = the spread of every real tick (or of every 1-minute candle without ticks)", spR)); PaintSpread();
            btTicks = Get("bt.ticks", "1") == "1"; btTicksBtn = chip(""); btTicksBtn.Click += delegate { btTicks = !btTicks; Put("bt.ticks", btTicks ? "1" : "0"); PaintTicks(); PaintDays(); };
            btMore.Children.Add(field("PRICE MOVEMENT", "REAL TICKS = every price MT5 recorded (bid, ask, time to the millisecond); off = the candle's open → low / high → close", btTicksBtn)); PaintTicks();
            btMore.Children.Add(field("SLIPPAGE", "added against you on market orders and stops", withUnit(btSlip, "points")));
            var swR = new StackPanel { Orientation = Orientation.Horizontal }; swR.Children.Add(new TextBlock { Text = "buy ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); swR.Children.Add(btSwapL); swR.Children.Add(new TextBlock { Text = "  sell ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); swR.Children.Add(btSwapS); swR.Children.Add(new TextBlock { Text = "  0 = MT5's own swap (ⓘ MT5 SPEC) • else $ per lot per night", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            btMore.Children.Add(field("SWAP", "charged at the server's midnight, the triple day from MT5", swR));
            btMore.Children.Add(field("STOP OUT", "", withUnit(btStopOut, "% margin level (the worst ticket closes)")));
            btMore.Children.Add(field("WARM-UP", "candles drawn before the first day, for the context", withUnit(btWarm, "days before (drawn at once)")));
            PaintKind();
            sp.Children.Add(wrap(c3, Gold));
            // START
            var c4 = new StackPanel();
            btBar = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Line, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
            btBarFill = new Border { CornerRadius = new CornerRadius(4), Background = new LinearGradientBrush(Cyan.Color, Green.Color, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; btBar.Child = btBarFill;
            var br = new StackPanel { Orientation = Orientation.Horizontal };
            var start = Pill("▶ START THE BACKTEST", Green); start.Height = 46; start.FontSize = 15; start.Padding = new Thickness(26, 0, 26, 0); start.Foreground = Bg; start.Click += delegate { StartBacktest(); };
            var down = Pill("⬇ ONLY DOWNLOAD", Card); down.Height = 46; down.Foreground = Cyan; down.ToolTip = "download the missing days (+ the warm-up) from MT5 without starting"; down.Click += delegate { StartDownload(false); };
            var years = Sx.Drop("⬇ DOWNLOAD YEARS", Card, () => new[] { Tuple.Create("1 month", 31), Tuple.Create("3 months", 92), Tuple.Create("6 months", 183), Tuple.Create("1 year", 365), Tuple.Create("2 years", 730), Tuple.Create("5 years", 1826) }
                .Select(x => { var xx = x; return new Mi("⬇", "the last " + xx.Item1 + " of 1-minute candles (" + string.Join(" + ", btSyms) + ")", () => StartDownload(false, xx.Item2)) { Hint = "saved on this PC" }; }).Concat(new[] { Mi.S(), new Mi { Text = "kept in " + HistoryStore.Root + " — every later load is instant; the real ticks come for the days you test", Off = true } }).ToList());
            years.Height = 46; years.Foreground = Gold; years.ToolTip = "download months / years of 1-minute candles once (in the background); later backtests load from this PC at once";
            br.Children.Add(start); br.Children.Add(down); br.Children.Add(years); c4.Children.Add(br);
            btStatus = new TextBlock { Foreground = Cyan, FontSize = 12.5, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 6) }; c4.Children.Add(btStatus); c4.Children.Add(btBar);
            c4.Children.Add(new TextBlock { Text = "On the chart: SPACE = play / pause • ← → = one candle back / forward • Shift + ← → = one minute • Page Up / Down = one day • 📅 = go to a date and time or change the days. Trade with the panel, the right-click and the lines — exactly as live. Every finished day is saved in 🗂 POOL.", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(wrap(c4, Green));
            var c5 = card("🧾 PAST RUNS", Muted); btRuns = new StackPanel(); c5.Children.Add(btRuns); sp.Children.Add(wrap(c5, Line));
            PaintSymChips(); PaintDays(); PaintRuns();
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        void SetRange(DateTime f, DateTime t) { btFrom.Text = f.ToString("yyyy-MM-dd", IC); btTo.Text = t.ToString("yyyy-MM-dd", IC); Put("bt.from", btFrom.Text); Put("bt.to", btTo.Text); PaintDays(); }
        void SaveSyms() { Put("bt.syms", string.Join(",", btSyms)); PaintSymChips(); PaintDays(); }
        void PaintSymChips()
        {
            if (btSymChips == null) return; btSymChips.Children.Clear();
            List<string> mt; lock (live.State.Gate) mt = live.State.Quotes.Keys.ToList();
            var saved = HistoryStore.Symbols();
            var all = btSyms.Concat(BtSuggest.Where(x => mt.Count == 0 || mt.Contains(x, StringComparer.OrdinalIgnoreCase) || saved.Contains(x, StringComparer.OrdinalIgnoreCase))).Concat(saved).Concat(mt.OrderBy(x => x).Take(10)).Distinct(StringComparer.OrdinalIgnoreCase).Take(22).ToList();
            foreach (var s in all)
            {
                var ss = s; bool on = btSyms.Contains(s, StringComparer.OrdinalIgnoreCase); int days = HistoryStore.Days(s).Count;
                var b = Pill((on ? "✓ " : "") + s + (days > 0 ? "  💾" + days : ""), on ? (Brush)Tint(Cyan, 200) : Card); b.Height = 32; b.FontSize = 12; b.Padding = new Thickness(12, 0, 12, 0); b.Margin = new Thickness(0, 0, 6, 6);
                b.Foreground = on ? Bg : Cyan; b.BorderBrush = on ? Cyan : Tint(Cyan, 80); b.ToolTip = (days > 0 ? days + " day(s) of " + s + " on this PC" : "not downloaded yet") + " • click = " + (on ? "remove" : "add") + " • right-click = MT5 spec";
                b.Click += delegate { if (btSyms.Contains(ss, StringComparer.OrdinalIgnoreCase)) btSyms.RemoveAll(x => string.Equals(x, ss, StringComparison.OrdinalIgnoreCase)); else btSyms.Add(ss); SaveSyms(); };
                b.MouseRightButtonUp += (o, e) => { e.Handled = true; ShowSpec(ss); };
                btSymChips.Children.Add(b);
            }
        }
        void PaintStart()
        {
            bool any = false; foreach (var t in btStartBtns) { bool on = (double.IsNaN(t.Item1) && double.IsNaN(btStartNy)) || t.Item1 == btStartNy; any |= on; t.Item2.Background = on ? Tint(Violet, 190) : (Brush)Card; t.Item2.Foreground = on ? Bg : Text; }
            if (btStartBox != null) { btStartBox.BorderBrush = any ? Line : Violet; btStartBox.Background = any ? Bg : Tint(Violet, 60); if (!any && !btStartBox.IsKeyboardFocused) btStartBox.Text = TimeOfNy(btStartNy); else if (any && !btStartBox.IsKeyboardFocused) btStartBox.Text = ""; }
        }
        static string TimeOfNy(double h) { if (double.IsNaN(h)) return ""; var ts = TimeSpan.FromHours(((h % 24) + 24) % 24); return ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00"); }
        void PaintSpread() { btSpreadReal.Background = btRealSpread ? Tint(Cyan, 190) : (Brush)Card; btSpreadReal.Foreground = btRealSpread ? Bg : Text; btSpreadFixed.Background = !btRealSpread ? Tint(Cyan, 190) : (Brush)Card; btSpreadFixed.Foreground = !btRealSpread ? Bg : Text; btSpread.IsReadOnly = btRealSpread; btSpread.Opacity = btRealSpread ? 0.35 : 1; }
        void PaintTicks() { btTicksBtn.Content = btTicks ? "✓ MT5's REAL TICKS (recommended)" : "✖ candles only (open → low / high → close)"; btTicksBtn.Background = btTicks ? Tint(Green, 190) : (Brush)Card; btTicksBtn.Foreground = btTicks ? Bg : Gold; }
        void PaintKind()
        {
            bool raw = btKind == "RAW"; btRawBtn.Background = raw ? Tint(Cyan, 200) : (Brush)Card; btRawBtn.Foreground = raw ? Bg : Text; btStdBtn.Background = !raw ? Tint(Violet, 200) : (Brush)Card; btStdBtn.Foreground = !raw ? Bg : Text;
            btComm.IsReadOnly = raw; btComm.Opacity = raw ? 0.35 : 1; btMarkup.IsReadOnly = !raw; btMarkup.Opacity = raw ? 1 : 0.35;
            btKindHint.Text = raw ? "RAW: no commission — every ask is wider by the extra points (that is your cost)." : "STANDARD: MT5's spread (near 0) + the commission per lot.";
        }
        bool Range(out DateTime f, out DateTime t)
        {
            bool a = DateTime.TryParseExact(btFrom.Text.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out f), b = DateTime.TryParseExact(btTo.Text.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out t);
            return a && b && t >= f;
        }
        int Warm() { int w = IntOf(btWarm.Text, 3); return Math.Max(0, Math.Min(30, w)); }
        void PaintDays()
        {
            if (btDays == null) return; DateTime f, t;
            if (btSyms.Count == 0) { btDays.Text = "tick at least one symbol in 1"; btDays.Foreground = Gold; btSpec.Text = ""; return; }
            if (!Range(out f, out t)) { btDays.Text = "type the days as yyyy-mm-dd (FROM ≤ TO) or press a chip"; btDays.Foreground = Red; return; }
            int wd = 0; for (var d = f; d <= t; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) wd++;
            var parts = new List<string>(); int missing = 0, tickMiss = 0;
            foreach (var sym in btSyms)
            {
                int need = 0, got = 0, tk = 0; for (var d = f.AddDays(-Warm()); d <= t; d = d.AddDays(1)) { if (d.DayOfWeek == DayOfWeek.Saturday) continue; need++; if (HistoryStore.HasDay(sym, d)) got++; if (d >= f && HistoryStore.HasTicks(sym, d)) tk++; }
                missing += need - got; tickMiss += Math.Max(0, wd - tk); parts.Add(sym + " " + got + "/" + need + (btTicks ? " (ticks " + tk + ")" : ""));
            }
            btDays.Text = "📅 " + f.ToString("ddd dd MMM", IC) + " → " + t.ToString("ddd dd MMM yyyy", IC) + " • " + wd + " trading day(s)   •   💾 on this PC: " + string.Join("  •  ", parts) + (missing > 0 ? "   →  ▶ START downloads the rest from MT5 once" : btTicks && tickMiss > 0 ? "   →  ▶ START adds MT5's real ticks (with MT5 connected)" : "   ✓ ready");
            btDays.Foreground = missing == 0 ? Green : Text;
            btSpec.Text = string.Join("   •   ", btSyms.Select(s => { var sp = HistoryStore.LoadSpec(s); return sp == null ? s + ": spec with the first download" : s + ": $" + sp.PointValue.ToString("0.####", IC) + " / point / lot, lots " + sp.VolMin.ToString("0.##", IC) + "–" + sp.VolMax.ToString("0.##", IC); }));
        }
        SimSpec SpecNow(string sym)
        {
            Mt5Symbol ms = null; int off = 10800, ny = -7; string cur = "USD";
            lock (live.State.Gate) { if (live.State.Alive) live.State.Symbols.TryGetValue(sym, out ms); off = live.State.ServerOffsetSec; cur = live.State.Account.Currency; }
            if (ms != null)
            {
                try { var et = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); ny = (int)Math.Round(et.GetUtcOffset(DateTime.UtcNow).TotalHours - off / 3600.0); } catch { }
                var sp = SimSpec.From(ms, off, ny, cur); double c = CommissionSeen(sym); if (c >= 0) sp.CommissionPerLot = c; else { var old = HistoryStore.LoadSpec(sym); if (old != null) sp.CommissionPerLot = old.CommissionPerLot; }
                HistoryStore.SaveSpec(sp); return sp;
            }
            return HistoryStore.LoadSpec(sym);
        }
        // the commission you really pay on this symbol: from your MT5 deals (round turn per lot), −1 = not seen
        double CommissionSeen(string sym)
        {
            lock (live.State.Gate)
            {
                var ds = live.State.Deals.Where(d => d.IsTrade && string.Equals(d.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); double lots = ds.Where(d => d.Entry == "IN").Sum(d => d.Lots);
                if (lots <= 0 || ds.Count < 2) return -1; return Math.Round(Math.Abs(ds.Sum(d => d.Commission)) / lots, 2);
            }
        }
        // ⓘ MT5's spec of a symbol, read from the server (or the copy on this PC)
        void ShowSpec(string sym)
        {
            var sp = SpecNow(sym); bool live0; lock (live.State.Gate) live0 = live.State.Alive;
            if (sp == null) { lock (live.State.Gate) if (live0) live.Send("WATCH|" + sym); Sx.Info(this, "ⓘ " + sym, live0 ? "Asking MT5 for " + sym + " … press ⓘ again in a second." : "No spec of " + sym + " on this PC yet — connect MT5 (SpasiXBridge v5) once and it is read from the server."); return; }
            string[] modes = { "disabled", "points", "money (base currency)", "money (margin currency)", "money (deposit currency)", "interest % (current price)", "interest % (open price)", "points (reopen at close)", "points (reopen at bid)" };
            string swU = sp.SwapMode == 1 || sp.SwapMode >= 7 ? " points" : sp.SwapMode == 5 || sp.SwapMode == 6 ? " % a year" : sp.SwapMode >= 2 ? " $" : "";
            var lastDay = HistoryStore.Days(sym).LastOrDefault(); double avgSp = 0, lastPx = sp.MarginRefPrice; if (lastDay != default(DateTime)) { var bars = HistoryStore.LoadDay(sym, lastDay); if (bars.Count > 0) { avgSp = bars.Average(b => (double)b.Spread); lastPx = bars.Last().C; } }
            lock (live.State.Gate) { Mt5Quote q; if (live.State.Quotes.TryGetValue(sym, out q) && q.Bid > 0) lastPx = q.Bid; }
            double swapB = SimSwap(sp, 1, lastPx), swapS = SimSwap(sp, -1, lastPx);
            string txt = sp.Name + (sp.Description != "" ? " — " + sp.Description : "") + "\n\n"
                + "CONTRACT  " + sp.Contract.ToString("0.####", IC) + " • " + sp.Digits + " digits • point " + sp.Point.ToString("0.#####", IC) + "\n"
                + "VALUE  $" + sp.PointValue.ToString("0.####", IC) + " per point per lot ($" + (sp.TickValue).ToString("0.####", IC) + " per tick of " + sp.TickSize.ToString("0.#####", IC) + ")\n"
                + "LOTS  " + sp.VolMin.ToString("0.##", IC) + " … " + sp.VolMax.ToString("0.##", IC) + " step " + sp.VolStep.ToString("0.##", IC) + "\n"
                + "SWAP  buy " + sp.SwapLong.ToString("0.####", IC) + swU + " • sell " + sp.SwapShort.ToString("0.####", IC) + swU + " • " + (sp.SwapMode >= 0 && sp.SwapMode < modes.Length ? modes[sp.SwapMode] : "mode " + sp.SwapMode) + " • ×3 on " + sp.Swap3Day + (Math.Abs(swapB) + Math.Abs(swapS) > 0 ? "\n          ≈ " + Signed(swapB, "USD") + " a night per buy lot • " + Signed(swapS, "USD") + " per sell lot (at today's price)" : "") + "\n"
                + "MARGIN  " + (sp.MarginPerLot > 0 ? "$" + sp.MarginPerLot.ToString("N2", IC) + " per lot (MT5, at " + sp.MarginRefPrice.ToString("0.##", IC) + ")" : "contract × price ÷ leverage") + " • hedged: " + (sp.MarginUseLeg ? "only the larger side" : sp.HedgedRatio < 0 ? "unknown (half)" : sp.HedgedRatio < 0.001 ? "FREE — an opposite trade releases the margin" : (100 * sp.HedgedRatio).ToString("0", IC) + " % of the normal margin") + "\n"
                + "COMMISSION  " + (sp.CommissionPerLot >= 0 ? "$" + sp.CommissionPerLot.ToString("0.##", IC) + " per lot round turn (seen in your MT5 deals)" : "not seen in your deals yet — RAW uses the number in 3") + "\n"
                + "SPREAD  " + (avgSp > 0 ? "≈ " + avgSp.ToString("0", IC) + " points on average (" + lastDay.ToString("dd MMM", IC) + ")" : "comes with the download") + "\n"
                + "TIME  New York = server " + sp.NyOffsetHours.ToString("+0;−0", IC) + " h";
            Sx.Info(this, "ⓘ MT5 SPEC • " + sym, txt, Gold);
        }
        static double SimSwap(SimSpec sp, int dir, double price)
        {
            try { if (price <= 0) return 0; var a = new SimAccount(new Mt5State()); a.Specs[sp.Name] = sp; return a.SwapPerLot(sp, dir, price); } catch { return 0; }
        }
        // ⬇ the missing candles (a week per request) + MT5's real ticks of the FIRST day; the other days' ticks come in the background after ▶ START
        void StartDownload(bool thenStart, int yearsDays = 0)
        {
            DateTime f, t; if (btSyms.Count == 0 || (yearsDays == 0 && !Range(out f, out t))) { btStatus.Text = "✖ tick a symbol and choose the days first"; btStatus.Foreground = Red; return; }
            bool alive; lock (live.State.Gate) alive = live.State.Alive && live.State.BridgeVersion >= 4;
            if (!alive) { btStatus.Text = "✖ MT5 with the SpasiXBridge is needed to download — the days already on this PC still work"; btStatus.Foreground = Red; if (thenStart) LoadAndStart(); return; }
            dlQueue.Clear(); foreach (var s in btSyms) dlQueue.Enqueue(s); dlTotal = dlQueue.Count; dlThenStart = thenStart; dlYears = yearsDays; btBar.Visibility = Visibility.Visible;
            lock (live.State.Gate) foreach (var s in btSyms) if (!live.State.Symbols.ContainsKey(s)) live.Send("WATCH|" + s);
            NextDownload(); EnsureDlTimer();
        }
        int dlYears;
        void EnsureDlTimer() { if (dlTimer == null) { dlTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) }; dlTimer.Tick += delegate { DownloadTick(); }; } dlTimer.Start(); }
        DateTime FirstWeekday(DateTime d) { while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1); return d; }
        void NextDownload()
        {
            var sym = dlQueue.Dequeue(); SpecNow(sym);
            if (dlYears > 0) { var y = DateTime.Today.AddDays(-1); dl = new HistoryDownloader(live, sym, y.AddDays(-dlYears), y, y, y, false); return; }   // ⬇ YEARS: candles only
            DateTime f, t; Range(out f, out t); var d1 = FirstWeekday(f);
            dl = new HistoryDownloader(live, sym, f.AddDays(-Warm()), t, d1, d1, btTicks);
        }
        void DownloadTick()
        {
            var now = DateTime.Now;
            if (dl != null)
            {
                for (int k = 0; k < 4 && dl.Step(now); k++) { }   // several steps per tick: the answers are already here
                int doneSyms = dlTotal - dlQueue.Count - 1;
                btStatus.Text = (dlTotal > 1 ? "[" + (doneSyms + 1) + "/" + dlTotal + "] " : "") + dl.Status; btStatus.Foreground = dl.Failed ? Red : Cyan;
                btBarFill.Width = Math.Max(0, btBar.ActualWidth * (doneSyms + dl.Progress) / Math.Max(1, dlTotal));
                if (dl.Done)
                {
                    if (dl.Failed) { dl = null; PaintDays(); if (dlThenStart) LoadAndStart(); }
                    else if (dlQueue.Count > 0) NextDownload();
                    else { foreach (var s in btSyms) SpecNow(s); PaintDays(); PaintSymChips(); bool go = dlThenStart; btStatus.Foreground = Green; dl = null; if (dlYears > 0) btStatus.Text = "✓ " + string.Join(" + ", btSyms) + ": the last " + (dlYears >= 365 ? (dlYears / 365) + " year(s)" : Math.Round(dlYears / 30.4) + " month(s)") + " of 1-minute candles are on this PC — every backtest in that range loads at once"; if (go) { LoadAndStart(); StartBackgroundTicks(); } }
                }
            }
            if (bgDl != null)
            {
                for (int k = 0; k < 4 && bgDl.Step(now); k++) { }
                foreach (var d in bgDl.NewTickDays) { var tk = HistoryStore.LoadTicks(bgDl.Symbol, d); if (sim.Spec != null) sim.AddTicks(bgDl.Symbol, tk); } bgDl.NewTickDays.Clear();
                bgStatus = bgDl.Done ? "" : "⬇ real ticks " + bgDl.Symbol + " " + bgDl.TickDaysDone + "/" + bgDl.TickDaysTotal;
                if (bgDl.Done) { bgDl = bgQueue.Count > 0 ? NewBgDl(bgQueue.Dequeue()) : null; }
            }
            if (dl == null && bgDl == null) dlTimer.Stop();
        }
        // after the start: the rest of the days' real ticks, symbol by symbol, fed into the running replay as they arrive
        void StartBackgroundTicks()
        {
            bgQueue.Clear(); if (!btTicks) return; bool alive; lock (live.State.Gate) alive = live.State.Alive && live.State.BridgeVersion >= 5; if (!alive) return;
            foreach (var s in btSyms) bgQueue.Enqueue(s); bgDl = bgQueue.Count > 0 ? NewBgDl(bgQueue.Dequeue()) : null; if (bgDl != null) EnsureDlTimer();
        }
        HistoryDownloader NewBgDl(string sym) { DateTime f, t; Range(out f, out t); return new HistoryDownloader(live, sym, f, t, f, t, true); }
        void StartBacktest()
        {
            DateTime f, t; if (btSyms.Count == 0 || !Range(out f, out t)) { btStatus.Text = "✖ tick a symbol and choose the days first"; btStatus.Foreground = Red; return; }
            int missing = 0; foreach (var sym in btSyms) for (var d = f.AddDays(-Warm()); d <= t; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && (!HistoryStore.HasDay(sym, d) || (btTicks && d >= f && d.DayOfWeek != DayOfWeek.Sunday && !HistoryStore.HasTicks(sym, d) && HistoryStore.LoadDay(sym, d).Count > 0))) missing++;
            bool alive; lock (live.State.Gate) alive = live.State.Alive && live.State.BridgeVersion >= 4;
            if (missing > 0 && alive) { StartDownload(true); return; }
            LoadAndStart(); if (alive) StartBackgroundTicks();
        }
        void LoadAndStart()
        {
            DateTime f, t; if (!Range(out f, out t)) return;
            var input = new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>>(); var miss = new List<string>(); long ticks = 0; int bars = 0;
            foreach (var sym in btSyms)
            {
                var spec = SpecNow(sym); if (spec == null) { miss.Add(sym + " (no spec — connect MT5 once)"); continue; }
                var m1 = HistoryStore.LoadRange(sym, f.AddDays(-Warm()), t); if (m1.Count == 0) { miss.Add(sym + " (no candles on this PC)"); continue; }
                var tk = btTicks ? HistoryStore.LoadTickRange(sym, f, t) : null; if (tk != null) ticks += tk.Count; bars += m1.Count;
                input.Add(Tuple.Create(spec, m1, tk));
            }
            if (input.Count == 0) { btStatus.Text = "✖ nothing to replay: " + string.Join(", ", miss) + " — connect MT5 and press ▶ START (it downloads)"; btStatus.Foreground = Red; return; }
            if (sim.Spec != null && sim.State.Deals.Count(d => d.IsExit) > 0) { SaveRun(); PoolSaveAll(); }
            Func<TextBox, double, double> num = (b, d) => { double v; return double.TryParse(b.Text.Replace(',', '.'), NumberStyles.Float, IC, out v) ? v : d; };
            var st = new SimSettings { Symbol = string.Join(",", input.Select(x => x.Item1.Name)), From = f, To = t, WarmDays = Warm(), StartNyHour = btStartNy, Balance = Math.Max(100, num(btBal, 10000)), Leverage = (int)Math.Max(1, num(btLev, 500)), CommissionPerLot = Math.Max(0, num(btComm, 7)),
                FixedSpread = btRealSpread ? -1 : (int)Math.Max(0, num(btSpread, 20)), SlipPts = (int)Math.Max(0, num(btSlip, 0)), SwapLong = num(btSwapL, 0), SwapShort = num(btSwapS, 0), StopOutPct = Math.Max(0, num(btStopOut, 50)), Currency = input[0].Item1.Currency, Kind = btKind, MarkupPts = Math.Max(0, num(btMarkup, 10)) };
            string err; lock (sim.State.Gate) err = sim.Load(st, input);
            if (err != "") { btStatus.Text = "✖ " + err; btStatus.Foreground = Red; return; }
            resSeen = 0; notesSeen = 0; poolDaysSeen = 0; logPanel.Children.Clear();
            var names = input.Select(x => x.Item1.Name).ToList();
            for (int k = 0; k < panes.Count; k++) { panes[k].Sym = ""; SetPane(panes[k], names[k % names.Count], panes[k].Tf); } symSig = "";
            btStatus.Text = "✓ " + string.Join(" + ", names) + " • " + bars.ToString("N0", IC) + " one-minute candles" + (btTicks ? " • " + ticks.ToString("N0", IC) + " real ticks" + (ticks == 0 ? " (none on this PC — the candles' path is used)" : "") : "") + " • " + sim.DaysTotal + " day(s) from " + sim.StartTime.ToString("ddd dd MMM HH:mm", IC) + " server" + (miss.Count > 0 ? " • skipped: " + string.Join(", ", miss) : "");
            btStatus.Foreground = miss.Count > 0 ? Gold : Green;
            Show("CHART"); Toast("🧪 " + string.Join(" + ", names) + " loaded — ▶ PLAY (Space) • ← → candle • trade with the panel"); trackSig = "";
        }
        void SaveRun()
        {
            try
            {
                var s = sim.State; var ex = s.Deals.Where(d => d.IsExit).ToList(); if (ex.Count == 0) return; double net = s.Deals.Where(d => d.IsTrade).Sum(d => d.Net); int w = ex.Count(d => d.Net > 0);
                Directory.CreateDirectory(Dir);
                File.AppendAllLines(RunsFile, new[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + "|" + string.Join("+", sim.Specs.Select(x => x.Name)) + "|" + sim.Set.From.ToString("yyyy-MM-dd", IC) + "|" + sim.Set.To.ToString("yyyy-MM-dd", IC) + "|" + ex.Count + "|" + net.ToString("0.00", IC) + "|" + (100.0 * w / ex.Count).ToString("0", IC) + "|" + sim.Set.Balance.ToString("0", IC) });
                PaintRuns();
            }
            catch { }
        }
        void PaintRuns()
        {
            if (btRuns == null) return; btRuns.Children.Clear(); string[] lines = new string[0]; try { if (File.Exists(RunsFile)) lines = File.ReadAllLines(RunsFile); } catch { }
            if (lines.Length == 0) { btRuns.Children.Add(new TextBlock { Text = "no run yet — a run is listed here when you start another one or leave the backtest (the days themselves are in 🗂 POOL)", Foreground = Muted, FontSize = 12 }); return; }
            foreach (var ln in lines.Reverse().Take(8))
            {
                var a = ln.Split('|'); if (a.Length < 8) continue; double net; double.TryParse(a[5], NumberStyles.Float, IC, out net);
                var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                r.Children.Add(Cell(a[0], Muted, 130)); r.Children.Add(Cell(a[1], Cyan, 160, false, true)); r.Children.Add(Cell(a[2] + " → " + a[3], Text, 200)); r.Children.Add(Cell(a[4] + " trades", Text, 90)); r.Children.Add(Cell(a[6] + " % won", Text, 90)); r.Children.Add(Cell(Signed(net, "USD"), net >= 0 ? Green : Red, 110, true, true)); r.Children.Add(Cell("on $" + a[7], Muted, 90));
                btRuns.Children.Add(r);
            }
        }

        // ---------------------------------------------------------------- the replay bar
        FrameworkElement BuildReplayBar()
        {
            var w = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            Func<string, string, Action, Button> nav = (t, tip, a) => { var b = Pill(t, Card); b.Padding = new Thickness(8, 0, 8, 0); b.ToolTip = tip; b.Click += delegate { a(); }; w.Children.Add(b); return b; };
            nav("⏮", "restart from the first candle (this run's trades are saved, then cleared)", RestartSim);
            nav("◀◀ DAY", "back to the start of the day — or the day before (Page Up)", () => Back(sim.PrevDayStart(), "the day's start"));
            nav("◀ CANDLE", "one candle of the selected chart back (←)", () => Back(sim.PrevBar(Act.Tf), "one " + Tf.Name(Act.Tf) + " candle"));
            nav("◀ 1m", "one minute back (Shift + ←)", () => Back(sim.PrevBar(1), "one minute"));
            playBtn = Pill("▶ PLAY", Green); playBtn.Foreground = Bg; playBtn.MinWidth = 96; playBtn.ToolTip = "play / pause (Space)"; playBtn.Click += delegate { TogglePlay(); }; w.Children.Add(playBtn);
            nav("1m ▶", "one minute forward (Shift + →)", () => StepCandle(1));
            nav("CANDLE ▶", "one candle of the selected chart forward (→)", () => StepCandle(Act.Tf));
            nav("DAY ▶▶", "to the next day's start (Page Down)", () => Jump(sim.NextDayStart()));
            w.Children.Add(Sep());
            double cur; double.TryParse(Get("bt.speed", "10"), NumberStyles.Float, IC, out cur); if (cur <= 0) cur = 10; sim.Speed = cur;
            foreach (var sp in new[] { 1.0, 2, 5, 10, 30, 60, 120, 300, 1000, SimBroker.MaxSpeed })
            {
                double v = sp; var b = Pill(v >= SimBroker.MaxSpeed ? "MAX" : v + "×", Card); b.Padding = new Thickness(6, 0, 6, 0); b.MinWidth = 32; b.ToolTip = v >= SimBroker.MaxSpeed ? "as fast as possible" : v == 1 ? "real time (with real ticks: exactly the market's pace)" : v + " × real time";
                b.Click += delegate { sim.Speed = v; Put("bt.speed", v.ToString("R", IC)); PaintSpeed(); }; w.Children.Add(b); speedBtns.Add(Tuple.Create(v, b));
            }
            w.Children.Add(Sep());
            nav("⏩ LONDON", "to the next London open (NY 03:00)", () => Jump(sim.NextNy(3)));
            nav("⏩ NY OPEN", "to the next New York open (09:30) minus 5 minutes", () => Jump(sim.NextNy(9.5 - 5 / 60.0)));
            // pick a day and a time right here: 📆 the day ▾ + HH:mm (New York) + GO — back or forward
            pickDayBtn = Sx.Drop("📆 DAY", Card, () => sim.Spec == null ? new List<Mi> { new Mi { Text = "load a backtest first", Off = true } } : sim.Days.Where(d => d.DayOfWeek != DayOfWeek.Saturday).Select(d => { var dd = d; return new Mi { Text = d.ToString("dddd dd MMM yyyy", IC), Check = pickDay == dd, Radio = true, Do = () => { pickDay = dd; pickDayBtn.Content = "📆 " + dd.ToString("ddd dd MMM", IC) + "  ▾"; GoPicked(); } }; }).ToList());
            pickDayBtn.ToolTip = "the day to go to (the replay goes there at the time in the box)"; w.Children.Add(pickDayBtn);
            pickTime = Sx.Input(Get("bt.picktime", "08:00"), 62); pickTime.Height = 30; pickTime.FontSize = 12.5; pickTime.Margin = new Thickness(0, 0, 4, 0); pickTime.ToolTip = "the New York time (HH:mm) — Enter = go"; pickTime.KeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Enter) GoPicked(); }; w.Children.Add(pickTime);
            var gob = Pill("GO", Tint(Gold, 190)); gob.Foreground = Bg; gob.Padding = new Thickness(10, 0, 10, 0); gob.ToolTip = "go to that day and time"; gob.Click += delegate { GoPicked(); }; w.Children.Add(gob);
            var go = nav("📅", "go to any date and time — or change the days of the test", GoToDialog); go.Foreground = Gold;
            w.Children.Add(Sep());
            var clk = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 0) };
            clockText = new TextBlock { Text = "📂 load a backtest in the DATA tab", Foreground = Gold, FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono }; clk.Children.Add(clockText);
            clockDay = new TextBlock { Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.SemiBold }; clk.Children.Add(clockDay); w.Children.Add(clk);
            var pr = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            progTrack = new Border { Width = 200, Height = 8, CornerRadius = new CornerRadius(4), Background = Line, ToolTip = "how far the replay is" };
            progFill = new Border { CornerRadius = new CornerRadius(4), Background = new LinearGradientBrush(Cyan.Color, Gold.Color, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; progTrack.Child = progFill; pr.Children.Add(progTrack);
            realText = new TextBlock { FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 3, 0, 0) }; pr.Children.Add(realText); w.Children.Add(pr);
            PaintSpeed();
            return new Border { Background = new LinearGradientBrush(Sx.C("#102033"), Sx.C("#0D1524"), 0), BorderBrush = Tint(Cyan, 110), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 6), Child = w };
        }
        void PaintSpeed() { foreach (var t in speedBtns) { bool on = Math.Abs(t.Item1 - sim.Speed) < 1e-9; t.Item2.Background = on ? Tint(Cyan, 200) : (Brush)Card; t.Item2.Foreground = on ? Bg : Text; } }
        bool SimReady() { if (sim.Spec == null) { Toast("📂 load a backtest in the DATA tab first"); return false; } return true; }
        void TogglePlay() { if (!SimReady()) return; if (sim.Ended) { Toast("the end of the data — ◀ back, 📅 GO TO or ⏮ restart"); return; } sim.Playing = !sim.Playing; simLast = DateTime.Now; }
        void StepCandle(int minutes) { if (!SimReady()) return; sim.Playing = false; sim.StepBar(minutes); foreach (var p in panes.Take(split)) p.V.InvalidateVisual(); }
        void StepCandle() { StepCandle(Act.Tf); }
        void Jump(DateTime t) { if (!SimReady()) return; bool was = sim.Playing; sim.Playing = false; sim.JumpTo(t); sim.Playing = was; Toast("⏩ " + t.AddHours(sim.Spec.NyOffsetHours).ToString("ddd dd MMM HH:mm", IC) + " NY"); }
        // back in time: the replay runs again to that moment, your trades before it stay, the ones after it are undone (asks if any)
        void Back(DateTime t, string what)
        {
            if (!SimReady()) return; if (t >= sim.Clock) { Toast("already at the start"); return; }
            int after = sim.CommandsAfter(t);
            if (after > 0 && !Sx.Ask(this, "◀ Back in time", "Go back " + what + " to " + t.AddHours(sim.Spec.NyOffsetHours).ToString("ddd dd MMM HH:mm", IC) + " NY?\n\nYour " + after + " action" + (after == 1 ? "" : "s") + " after that moment (orders, closes, stops) are undone; everything before it stays exactly as it was.", "GO BACK", "STAY", Gold)) return;
            bool was = sim.Playing; string err; lock (sim.State.Gate) err = sim.RewindTo(t);
            resSeen = 0; notesSeen = 0; posSig = ""; trackSig = ""; logPanel.Children.Clear();
            if (err != "") { Toast("✖ " + err); return; }
            sim.Playing = was && !sim.Ended; simLast = DateTime.Now; foreach (var p in panes.Take(split)) p.V.InvalidateVisual();
            Toast("◀ " + sim.Clock.AddHours(sim.Spec.NyOffsetHours).ToString("ddd dd MMM HH:mm", IC) + " NY");
        }
        // 📅 GO TO: a date + a New York time (back or forward) — or new days for the test (reloads)
        void GoToDialog()
        {
            if (!SimReady()) return; var ny = sim.Clock.AddHours(sim.Spec.NyOffsetHours);
            var g = new Grid { Margin = new Thickness(0, 12, 0, 0) }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            int row = 0; Func<string, string, TextBox> fld = (cap, val) => { g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); var l = new TextBlock { Text = cap, Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }; var b = Sx.Input(val, 150); b.Margin = new Thickness(0, 4, 0, 4); Grid.SetRow(l, row); Grid.SetRow(b, row); Grid.SetColumn(b, 1); g.Children.Add(l); g.Children.Add(b); row++; return b; };
            var day = fld("DATE (yyyy-mm-dd)", ny.ToString("yyyy-MM-dd", IC)); var hm = fld("NEW YORK TIME", ny.ToString("HH:mm", IC));
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); var sep = new TextBlock { Text = "— or change the days of the test (reloads, downloads what is missing) —", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 10, 0, 4) }; Grid.SetRow(sep, row); Grid.SetColumnSpan(sep, 2); g.Children.Add(sep); row++;
            var f0 = fld("FROM", sim.Set.From.ToString("yyyy-MM-dd", IC)); var t0 = fld("TO", sim.Set.To.ToString("yyyy-MM-dd", IC));
            if (!Sx.Form(this, "📅 Go to", "Now: " + ny.ToString("ddd dd MMM yyyy HH:mm", IC) + " NY • the test: " + sim.Set.From.ToString("dd MMM", IC) + " → " + sim.Set.To.ToString("dd MMM yyyy", IC), g, "GO", "CANCEL", Gold)) return;
            DateTime nf, nt; bool rangeOk = DateTime.TryParseExact(f0.Text.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out nf) & DateTime.TryParseExact(t0.Text.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out nt);
            if (rangeOk && nt >= nf && (nf != sim.Set.From.Date || nt != sim.Set.To.Date)) { SetRange(nf, nt); StartBacktest(); return; }
            DateTime d; TimeSpan ts; if (!DateTime.TryParseExact(day.Text.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out d) || !TimeSpan.TryParse(hm.Text.Trim(), IC, out ts)) { Toast("type the date as yyyy-mm-dd and the time as HH:mm"); return; }
            var target = d.Add(ts).AddHours(-sim.Spec.NyOffsetHours);
            if (target < sim.StartTime || target > sim.EndTime) { Toast("outside the loaded days — change FROM / TO in 📅 to load them"); return; }
            if (target < sim.Clock) Back(target, "in time"); else Jump(target);
        }
        Button pickDayBtn; TextBox pickTime; DateTime pickDay = DateTime.MinValue;
        void GoPicked()
        {
            if (!SimReady()) return; TimeSpan ts; var tx = pickTime.Text.Trim();
            if (!TimeSpan.TryParse(tx.Contains(":") ? tx : tx + ":00", IC, out ts) || ts.TotalHours >= 24) { Toast("the time is HH:mm (New York), e.g. 09:25"); return; }
            Put("bt.picktime", tx); var ny0 = sim.Clock.AddHours(sim.Spec.NyOffsetHours); var day = pickDay == DateTime.MinValue ? ny0.Date : pickDay;
            // the day = a server day; its New York date: the server day runs NY 17:00 (the day before) → 17:00, so a NY time before 17:00 is that date
            var target = day.Add(ts).AddHours(-sim.Spec.NyOffsetHours); if (ts.TotalHours >= 24 + sim.Spec.NyOffsetHours) target = target.AddDays(-1);   // e.g. NY 18:00 = the evening before the server day
            if (target < sim.StartTime) target = sim.StartTime; if (target > sim.EndTime) { Toast("after the loaded days — 📅 changes them"); return; }
            if (target < sim.Clock) Back(target, "in time"); else Jump(target);
        }
        void RestartSim()
        {
            if (!SimReady()) return; int n; lock (sim.State.Gate) n = sim.State.Deals.Count(d => d.Entry == "IN");
            if (n > 0 && !Sx.Ask(this, "⏮ Restart", "Restart from the first candle? The " + n + " trade" + (n == 1 ? "" : "s") + " of this run are listed in PAST RUNS (their days stay in the POOL) and cleared.", "RESTART", "NO")) return;
            if (n > 0) { SaveRun(); PoolSaveAll(); } string err; lock (sim.State.Gate) err = sim.Restart(); resSeen = 0; notesSeen = 0; poolDaysSeen = 0; logPanel.Children.Clear(); trackSig = ""; posSig = "";
            if (err != "") Toast("✖ " + err); else Toast("⏮ back to " + sim.StartTime.ToString("ddd dd MMM HH:mm", IC) + " server");
        }
        void RefreshSimHeader(Mt5State s)
        {
            if (sim.Spec == null) { connText.Text = "🧪 NO DATA YET — 📂 DATA"; connPill.Background = Gold; acLogin.Text = "BACKTEST"; acName.Text = ""; acRest.Text = "tick the symbols and the days in 📂 DATA, then ▶ START"; foreach (var c in cards.Values) c.Item2.Text = "—"; return; }
            var names = string.Join(" + ", sim.Specs.Select(x => x.Name));
            connText.Text = (sim.Ended ? "■ ENDED" : sim.Playing ? "▶ PLAYING " + (sim.Speed >= SimBroker.MaxSpeed ? "MAX" : sim.Speed + "×") : "⏸ PAUSED") + " • " + names; connPill.Background = sim.Playing ? Green : sim.Ended ? Red : Cyan;
            acLogin.Text = "BACKTEST"; acName.Text = names;
            acRest.Text = "•  " + sim.Set.From.ToString("dd MMM", IC) + " → " + sim.Set.To.ToString("dd MMM yyyy", IC) + "  •  " + sim.Set.Kind + (sim.Set.Kind != "RAW" ? " $" + sim.Set.CommissionPerLot.ToString("0.##", IC) + " / lot" : " +" + sim.Set.MarkupPts.ToString("0", IC) + " pts") + "  •  HEDGING  •  1:" + sim.Set.Leverage + "  •  spread " + (sim.Set.FixedSpread >= 0 ? sim.Set.FixedSpread + " pts" : "REAL") + "  •  start $" + sim.Set.Balance.ToString("N0", IC) + "  •  🗂 " + PoolTest;
        }
        void FillSim(Mt5State s)
        {
            if (playBtn == null) return;
            playBtn.Content = sim.Playing ? "⏸ PAUSE" : sim.Ended ? "■ ENDED" : "▶ PLAY"; playBtn.Background = sim.Playing ? (Brush)Tint(Gold, 220) : sim.Ended ? Card : Green; playBtn.Foreground = sim.Ended ? Muted : Bg;
            if (sim.Spec == null) return;
            var ny = sim.Clock.AddHours(sim.Spec.NyOffsetHours);
            clockText.Text = ny.ToString("ddd dd MMM yyyy  HH:mm:ss", IC) + " NY";
            clockDay.Text = "day " + Math.Max(1, sim.DayNumber) + " / " + sim.DaysTotal + "  •  server " + sim.Clock.ToString("HH:mm", IC) + "  •  " + (100 * sim.Progress).ToString("0", IC) + " %";
            progFill.Width = Math.Max(0, (progTrack.ActualWidth > 0 ? progTrack.ActualWidth : 200) * sim.Progress);
            double rs = sim.RealShare; realText.Text = (sim.HasTicks ? "● REAL TICKS " + (100 * rs).ToString("0", IC) + " %" : "○ candle path (no ticks on this PC)") + (bgStatus != "" ? "   " + bgStatus : ""); realText.Foreground = sim.HasTicks && rs > 0.95 ? Green : sim.HasTicks ? Gold : Muted;
            realText.ToolTip = sim.HasTicks ? "this share of the replayed minutes moves exactly like MT5 recorded it (every tick); the rest follows the candle open → low / high → close" : "tick 'REAL TICKS' in 📂 DATA ▸ MORE and START again with MT5 connected to download them";
            PoolWatch();
        }
    }
}
