// SpasiX — the ⇅ HEDGE LAB window (the title bar's ⇅ HEDGE LAB button, on every tab): the "hedge instead of a stop" strategy (HedgeLab.cs)
// replayed on the MT5 history this PC already has (the BACKTEST's downloads, HistoryStore). RESEARCH ONLY: a simulated account in memory —
// nothing here sends an order to MT5. ▶ RUN = one test with its days / cycles / trades; ▶ VARIATIONS = lookbacks × targets, each run straight
// through AND from a fresh account every Monday for 4 weeks (how often it blows).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KeystoneMT5
{
    public sealed class HedgeLabWindow : Window
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static HedgeLabWindow open;
        public static void ShowLab(Window owner)
        {
            if (open != null) { if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal; open.Activate(); return; }
            open = new HedgeLabWindow(); open.Closed += delegate { open = null; };
            if (owner != null && owner.IsVisible) open.Owner = owner; open.Show();
        }

        readonly Dictionary<string, TextBox> f = new Dictionary<string, TextBox>();
        readonly CheckBox reEnter = new CheckBox { Content = "after a win, the same direction again at once", IsChecked = true, Foreground = Sx.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
        readonly CheckBox oneTarget = new CheckBox { Content = "one target a day (done after the first win)", Foreground = Sx.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
        readonly CheckBox noHedge = new CheckBox { Content = "NO HEDGE (only the target and the basket stop)", Foreground = Sx.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
        readonly CheckBox flatEnd = new CheckBox { Content = "close the basket at the session's end", Foreground = Sx.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
        readonly CheckBox realSpread = new CheckBox { Content = "MT5's own spread (each candle's)", IsChecked = true, Foreground = Sx.Text, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock status = Sx.Label("", 12.5, Sx.Muted), symInfo = Sx.Label("", 12, Sx.Muted);
        readonly ContentControl tiles = new ContentControl();
        readonly Canvas curve = new Canvas { Height = 170, ClipToBounds = true, Background = Sx.Panel };
        readonly TextBox table = new TextBox { IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 12.5, Background = Sx.Panel, Foreground = Sx.Text, BorderBrush = Sx.Line, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, Padding = new Thickness(8) };
        readonly StackPanel views = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 6) };
        Button symBtn, runBtn, varBtn, sesBtn; string sym = ""; HedgeResult last; string varText = ""; string view = "DAYS"; bool busy;

        public HedgeLabWindow()
        {
            Title = "SpasiX — ⇅ HEDGE LAB"; Width = 1180; Height = 860; MinWidth = 900; MinHeight = 600; Background = Sx.Bg; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var lg = MainWindow.Logo(); if (lg != null) Icon = lg; Sx.DarkTitle(this);
            var root = new DockPanel { Margin = new Thickness(16, 12, 16, 12), LastChildFill = true };

            // the banner
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(head, Dock.Top);
            head.Children.Add(Sx.Label("⇅ HEDGE LAB — hedge instead of a stop", 20, Sx.Gold, true));
            head.Children.Add(new TextBlock { Text = "Open a lot, bank +$target on the whole basket and open the same way again. No stop: when the price breaks the recent low (after a buy) or high (after a sell) of the last N one-minute candles, the other side opens with the hedge lot, and each new break the other way adds another hedge lot (× the multiplier) until the basket reaches the target. Tested on the MT5 history on this PC — research only, NOTHING is sent to MT5.", Foreground = Sx.Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            root.Children.Add(head);

            // the settings
            var card = new Border { Background = Sx.CardGrad, BorderBrush = Sx.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(card, Dock.Top);
            var cs = new StackPanel(); card.Child = cs;
            var r0 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            symBtn = Sx.Pill("SYMBOL  ▾", Sx.Tint(Sx.Cyan, 40)); symBtn.Click += delegate { PickSymbol(); }; r0.Children.Add(symBtn);
            r0.Children.Add(Field("from", "FROM", DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd", IC), 110, "")); r0.Children.Add(Field("to", "TO", DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", IC), 110, ""));
            var pa = Sx.Pill("A: $350 • 0.1 / 0.2 • $20 a trade • all day", Sx.Tint(Sx.Gold, 34)); pa.Click += delegate { Preset(350, 0.1, 20, 0); }; r0.Children.Add(pa);
            var pb = Sx.Pill("B: $5,000 • 0.25 / 0.5 • $20 a trade • $100 a day", Sx.Tint(Sx.Violet, 40)); pb.Click += delegate { Preset(5000, 0.25, 20, 100); }; r0.Children.Add(pb);
            var pc = Sx.Pill("C: best found • NY open • one $150 target a day • stop $300 • no hedge", Sx.Tint(Sx.Green, 40)); pc.ToolTip = "the study's best: $5,000, 0.25 lot, NY 08:30-10:30, entry = break of the last 5 candles, +$150 / −$300, 1 try a day (docs/research/hedge)";
            pc.Click += delegate { Preset(5000, 0.25, 150, 0); f["look"].Text = "5"; f["bstop"].Text = "300"; f["sfrom"].Text = "08:30"; f["sto"].Text = "10:30"; f["tries"].Text = "1"; oneTarget.IsChecked = true; noHedge.IsChecked = true; reEnter.IsChecked = false; };
            r0.Children.Add(pc); r0.Children.Add(symInfo); cs.Children.Add(r0);
            var r1 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            r1.Children.Add(Field("bal", "BALANCE $", "350", 80, "")); r1.Children.Add(Field("lot", "LOT", "0.1", 60, "")); r1.Children.Add(Field("hedge", "HEDGE LOT", "0.2", 60, "")); r1.Children.Add(Field("mult", "× EACH NEXT", "1", 50, "1 = every hedge the same lot, 2 = doubling (martingale)"));
            r1.Children.Add(Field("target", "TARGET $", "20", 60, "the whole basket's profit (commission, spread, swap included) that closes it")); r1.Children.Add(Field("daily", "DAY TARGET $", "0", 60, "0 = trade all day; else no new cycle once the day banked this"));
            r1.Children.Add(Field("look", "RECENT HIGH / LOW", "10", 50, "one-minute candles")); cs.Children.Add(r1);
            var r2 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            r2.Children.Add(Field("mind", "MIN DISTANCE", "0", 60, "price: the hedge level at least this far from the fill")); r2.Children.Add(Field("maxlegs", "MAX LEGS", "0", 50, "0 = no limit; else the basket closes at a loss instead"));
            r2.Children.Add(Field("bstop", "BASKET STOP $", "0", 60, "0 = none")); r2.Children.Add(Field("comm", "COMMISSION $/LOT", "7", 50, "round turn")); r2.Children.Add(Field("spread", "SPREAD PTS", "20", 50, "when MT5's own spread is off")); r2.Children.Add(Field("slip", "SLIPPAGE PTS", "5", 50, "on every hedge / entry"));
            r2.Children.Add(Field("lev", "LEVERAGE 1:", "500", 60, "")); r2.Children.Add(Field("so", "STOP-OUT %", "50", 50, "")); cs.Children.Add(r2);
            var r25 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            r25.Children.Add(Field("sfrom", "SESSION FROM (NY)", "", 70, "New York time hh:mm — new baskets only inside the session (empty = all day)")); r25.Children.Add(Field("sto", "SESSION TO (NY)", "", 70, "New York time hh:mm"));
            r25.Children.Add(Field("nyoff", "NY = SERVER +", "-7", 50, "hours from MT5's server clock to New York (IC Markets: −7)")); r25.Children.Add(Field("tries", "MAX TRIES A DAY", "0", 50, "0 = no limit; else no new basket once this many closed today (win or loss)"));
            r25.Children.Add(Field("zone", "ZONE $", "0", 60, "> 0: the hedge line this far (price) from the fill instead of the recent high / low")); cs.Children.Add(r25);
            var r3 = new WrapPanel(); r3.Children.Add(reEnter); r3.Children.Add(oneTarget); r3.Children.Add(noHedge); r3.Children.Add(flatEnd); r3.Children.Add(realSpread); cs.Children.Add(r3);
            root.Children.Add(card);

            // the buttons
            var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(bar, Dock.Top);
            runBtn = Sx.Pill("▶  RUN", Sx.Tint(Sx.Green, 60)); runBtn.Click += delegate { RunOne(); }; DockPanel.SetDock(runBtn, Dock.Left); bar.Children.Add(runBtn);
            varBtn = Sx.Pill("▶  VARIATIONS (recent high / low × target)", Sx.Tint(Sx.Cyan, 50)); varBtn.Click += delegate { RunVariations(); }; DockPanel.SetDock(varBtn, Dock.Left); bar.Children.Add(varBtn);
            sesBtn = Sx.Pill("▶  SESSIONS (one target a day, hedge vs no hedge)", Sx.Tint(Sx.Gold, 40)); sesBtn.Click += delegate { RunSessions(); }; DockPanel.SetDock(sesBtn, Dock.Left); bar.Children.Add(sesBtn);
            status.Margin = new Thickness(10, 0, 0, 0); bar.Children.Add(status); root.Children.Add(bar);

            // the results
            var res = new DockPanel();
            DockPanel.SetDock(tiles, Dock.Top); res.Children.Add(tiles);
            var cv = new Border { BorderBrush = Sx.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = curve, Margin = new Thickness(0, 8, 0, 0) }; DockPanel.SetDock(cv, Dock.Top); res.Children.Add(cv);
            curve.SizeChanged += delegate { DrawCurve(); };
            foreach (var v in new[] { "DAYS", "CYCLES", "TRADES", "VARIATIONS" }) { var vv = v; var b = Sx.Pill(v, Sx.Tint(Sx.Card2, 220)); b.Height = 26; b.Click += delegate { view = vv; ShowView(); }; views.Children.Add(b); }
            DockPanel.SetDock(views, Dock.Top); res.Children.Add(views); res.Children.Add(table);
            root.Children.Add(res);
            Content = root;

            var syms = SafeSymbols(); sym = syms.FirstOrDefault(s => s.ToUpperInvariant().StartsWith("XAU")) ?? syms.FirstOrDefault() ?? "";
            PaintSymbol(); ShowView();
            status.Text = syms.Count == 0 ? "No MT5 history on this PC yet: open 🧪 BACKTEST, pick a symbol and its days and download them (HIST) — then come back here." : "Choose the days, a preset or your own numbers, then ▶ RUN.";
        }

        FrameworkElement Field(string key, string label, string val, double w, string tip)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Sx.Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            var tb = Sx.Input(val, w); if (tip != "") tb.ToolTip = tip; f[key] = tb; sp.Children.Add(tb); return sp;
        }
        double N(string key, double def) { double x; return double.TryParse((f[key].Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, IC, out x) ? x : def; }
        DateTime D(string key, DateTime def) { DateTime x; return DateTime.TryParseExact((f[key].Text ?? "").Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out x) ? x : def; }
        int Hm(string key) { TimeSpan x; var v = (f[key].Text ?? "").Trim(); return v != "" && TimeSpan.TryParseExact(v, new[] { @"h\:mm", @"hh\:mm" }, IC, out x) ? (int)x.TotalMinutes % 1440 : -1; }
        void Preset(double bal, double lot, double target, double daily)
        {
            f["bal"].Text = bal.ToString(IC); f["lot"].Text = lot.ToString(IC); f["hedge"].Text = (lot * 2).ToString(IC); f["mult"].Text = "1"; f["target"].Text = target.ToString(IC); f["daily"].Text = daily.ToString(IC);
            f["maxlegs"].Text = "0"; f["bstop"].Text = "0"; f["look"].Text = "10"; f["sfrom"].Text = ""; f["sto"].Text = ""; f["tries"].Text = "0"; f["zone"].Text = "0";
            reEnter.IsChecked = true; oneTarget.IsChecked = false; noHedge.IsChecked = false; flatEnd.IsChecked = false;
        }
        static List<string> SafeSymbols() { try { return HistoryStore.Symbols(); } catch { return new List<string>(); } }
        void PickSymbol()
        {
            var items = SafeSymbols().Select(s => { var ss = s; return new Mi { Text = ss + "   (" + HistoryStore.Days(ss).Count + " days)", Do = () => { sym = ss; PaintSymbol(); } }; }).ToList();
            if (items.Count == 0) items.Add(new Mi { Text = "no history yet — download it in 🧪 BACKTEST", Do = () => { } });
            SxMenu.Show(symBtn, new Point(0, symBtn.ActualHeight + 4), items);
        }
        void PaintSymbol()
        {
            symBtn.Content = (sym == "" ? "SYMBOL" : sym) + "  ▾";
            if (sym == "") { symInfo.Text = ""; return; }
            var days = HistoryStore.Days(sym); var sp = HistoryStore.LoadSpec(sym);
            symInfo.Text = days.Count == 0 ? "no candles saved" : days.Count + " days saved: " + days.First().ToString("dd MMM yyyy", IC) + " → " + days.Last().ToString("dd MMM yyyy", IC) + (sp != null && sp.TickSize > 0 ? "  •  $" + (sp.TickValue / sp.TickSize).ToString("0.##", IC) + " per 1.0 move per lot" : "");
            if (days.Count > 0) { var to = days.Last(); f["to"].Text = to.ToString("yyyy-MM-dd", IC); var from = days.Where(d => d >= to.AddDays(-30)).First(); f["from"].Text = from.ToString("yyyy-MM-dd", IC); }
        }

        HedgeSettings Settings(out string err, out List<Mt5Bar> bars, out DateTime from, out DateTime to)
        {
            err = ""; bars = null; from = D("from", DateTime.Today.AddDays(-30)); to = D("to", DateTime.Today);
            if (sym == "") { err = "Choose a symbol (its history comes from 🧪 BACKTEST's downloads)."; return null; }
            if (to < from) { err = "TO is before FROM."; return null; }
            var s = new HedgeSettings { Balance = Math.Max(10, N("bal", 350)), BaseLot = Math.Max(0.01, N("lot", 0.1)), HedgeLot = Math.Max(0.01, N("hedge", 0.2)), Multiplier = Math.Max(1, N("mult", 1)), Target = Math.Max(0.5, N("target", 20)), DailyTarget = Math.Max(0, N("daily", 0)),
                Lookback = (int)Math.Max(1, Math.Min(1440, N("look", 10))), MinDist = Math.Max(0, N("mind", 0)), MaxLegs = (int)Math.Max(0, N("maxlegs", 0)), BasketStop = Math.Max(0, N("bstop", 0)), CommissionPerLot = Math.Max(0, N("comm", 7)),
                FixedSpreadPts = realSpread.IsChecked == true ? -1 : (int)Math.Max(0, N("spread", 20)), SlipPts = (int)Math.Max(0, N("slip", 5)), Leverage = (int)Math.Max(1, N("lev", 500)), StopOutPct = Math.Max(0, N("so", 50)), ReEnter = reEnter.IsChecked == true,
                OneTargetADay = oneTarget.IsChecked == true, NoHedge = noHedge.IsChecked == true, FlattenAtSessionEnd = flatEnd.IsChecked == true, MaxTriesADay = (int)Math.Max(0, N("tries", 0)), Zone = Math.Max(0, N("zone", 0)),
                SessionFrom = Hm("sfrom"), SessionTo = Hm("sto"), NyOffsetHours = (int)N("nyoff", -7) };
            if ((s.SessionFrom < 0) != (s.SessionTo < 0)) { err = "Give the session both FROM and TO (hh:mm, New York), or leave both empty for all day."; return null; }
            bars = HistoryStore.LoadRange(sym, from.AddDays(-3), to);
            if (bars.Count == 0) { err = "No " + sym + " candles saved for these days — download them in 🧪 BACKTEST first."; return null; }
            HedgeLab.FromSpec(s, HistoryStore.LoadSpec(sym), bars[bars.Count - 1].C);
            return s;
        }

        void Busy(bool on, string text) { busy = on; runBtn.IsEnabled = varBtn.IsEnabled = sesBtn.IsEnabled = !on; status.Text = text; status.Foreground = on ? Sx.Cyan : Sx.Muted; }

        void RunOne()
        {
            if (busy) return; string err; List<Mt5Bar> bars; DateTime from, to; var s = Settings(out err, out bars, out from, out to);
            if (s == null) { status.Text = err; status.Foreground = Sx.Red; return; }
            s.Trace = true; s.Name = sym; Busy(true, "running " + sym + " " + from.ToString("dd MMM", IC) + " → " + to.ToString("dd MMM yyyy", IC) + " …");
            Task.Run(() => HedgeLab.Run(bars, s, from, to)).ContinueWith(t => Dispatcher.Invoke(() =>
            {
                Busy(false, t.Exception != null ? "the test stopped: " + t.Exception.GetBaseException().Message : "done — " + bars.Count.ToString("N0", IC) + " one-minute candles");
                if (t.Exception != null) return; last = t.Result; PaintResult(); if (view == "VARIATIONS") view = "DAYS"; ShowView();
            }));
        }

        void RunVariations()
        {
            if (busy) return; string err; List<Mt5Bar> bars; DateTime from, to; var s0 = Settings(out err, out bars, out from, out to);
            if (s0 == null) { status.Text = err; status.Foreground = Sx.Red; return; }
            Busy(true, "running the variations …");
            Task.Run(() =>
            {
                var looks = new[] { 5, 10, 20, 30, 60, 120 }; var tgts = new[] { s0.Target / 2, s0.Target, s0.Target * 2 }; var sets = new List<HedgeSettings>();
                foreach (var lk in looks) foreach (var tg in tgts) { var s = s0.Copy(); s.Lookback = lk; s.Target = Math.Round(tg, 2); s.Name = "recent " + lk + " • target $" + s.Target.ToString("0.##", IC); sets.Add(s); }
                var rows = new string[sets.Count];
                Parallel.For(0, sets.Count, i =>
                {
                    var r = HedgeLab.Run(bars, sets[i], from, to); var w = new List<HedgeResult>();
                    for (var st = from; st.AddDays(28) <= to.AddDays(1); st = st.AddDays(7)) w.Add(HedgeLab.Run(bars, sets[i], st, st.AddDays(27)));
                    string win = w.Count == 0 ? "   (fewer than 4 weeks: no windows)" : string.Format(IC, "   4-wk windows {0,3}: blown {1,3:0}%  profitable {2,3:0}%  median {3,9:N0}", w.Count, 100.0 * w.Count(x => x.Blown) / w.Count, 100.0 * w.Count(x => !x.Blown && x.Net > 0) / w.Count, w.Select(x => x.Net).OrderBy(x => x).ElementAt(w.Count / 2));
                    rows[i] = string.Format(IC, "{0,-26} net {1,9:N0}  {2,-24} cycles {3,5}  max DD {4,8:N0} ({5,3:0}%)  legs ≤ {6,3}  lots ≤ {7,6:0.00}", sets[i].Name, r.Net, r.Blown ? "BLOWN " + r.BlownAt.ToString("dd MMM yy HH:mm", IC) : "survived", r.Cycles.Count, r.MaxDd, r.MaxDdPct, r.MaxLegs, r.MaxGross) + win;
                });
                return sym + "  " + from.ToString("dd MMM yyyy", IC) + " → " + to.ToString("dd MMM yyyy", IC) + "  •  start $" + s0.Balance.ToString("N0", IC) + "  •  lot " + s0.BaseLot.ToString("0.##", IC) + " / hedge " + s0.HedgeLot.ToString("0.##", IC) + (s0.DailyTarget > 0 ? "  •  $" + s0.DailyTarget.ToString("N0", IC) + " a day" : "  •  all day") +
                    "\nstraight through from the first day, then a fresh account every Monday for 4 weeks\n\n" + string.Join("\n", rows);
            }).ContinueWith(t => Dispatcher.Invoke(() =>
            {
                Busy(false, t.Exception != null ? "the variations stopped: " + t.Exception.GetBaseException().Message : "variations done");
                if (t.Exception != null) return; varText = t.Result; view = "VARIATIONS"; ShowView();
            }));
        }

        // every session (New York time) with the settings above, one target a day, WITH the hedges and with NO HEDGE (the basket stop only)
        static readonly Tuple<string, int, int>[] SessionList = { Tuple.Create("all day", -1, -1), Tuple.Create("Asia 19:00-23:00", 1140, 1380), Tuple.Create("London open 03:00-05:00", 180, 300), Tuple.Create("London 03:00-08:00", 180, 480),
            Tuple.Create("NY open 08:30-10:30", 510, 630), Tuple.Create("NY 09:30-11:30", 570, 690), Tuple.Create("overlap 08:00-12:00", 480, 720), Tuple.Create("NY pm 13:00-16:00", 780, 960) };
        void RunSessions()
        {
            if (busy) return; string err; List<Mt5Bar> bars; DateTime from, to; var s0 = Settings(out err, out bars, out from, out to);
            if (s0 == null) { status.Text = err; status.Foreground = Sx.Red; return; }
            Busy(true, "running every session …");
            Task.Run(() =>
            {
                var sets = new List<HedgeSettings>();
                foreach (var se in SessionList) foreach (var nh in new[] { false, true })
                { var s = s0.Copy(); s.OneTargetADay = true; s.ReEnter = false; s.SessionFrom = se.Item2; s.SessionTo = se.Item3; s.NoHedge = nh; s.Name = se.Item1.PadRight(24) + (nh ? "no hedge" : "hedges"); sets.Add(s); }
                var rows = new string[sets.Count];
                Parallel.For(0, sets.Count, i =>
                {
                    var r = HedgeLab.Run(bars, sets[i], from, to); var traded = r.Days.Where(d => d.Cycles > 0).ToList();
                    rows[i] = string.Format(IC, "{0,-34} net {1,9:N0}  {2,-24} days traded {3,4}  won {4,4}  worst day {5,8:N0}  max DD {6,8:N0} ({7,3:0}%)  legs ≤ {8,3}", sets[i].Name, r.Net, r.Blown ? "BLOWN " + r.BlownAt.ToString("dd MMM yy HH:mm", IC) : "survived", traded.Count, traded.Count(d => d.Won), r.Days.Count == 0 ? 0 : r.Days.Min(d => d.EndEquity - d.StartEquity), r.MaxDd, r.MaxDdPct, r.MaxLegs);
                });
                return sym + "  " + from.ToString("dd MMM yyyy", IC) + " → " + to.ToString("dd MMM yyyy", IC) + "  •  start $" + s0.Balance.ToString("N0", IC) + "  •  lot " + s0.BaseLot.ToString("0.##", IC) + " / hedge " + s0.HedgeLot.ToString("0.##", IC) + "  •  one $" + s0.Target.ToString("0.##", IC) + " target a day" + (s0.BasketStop > 0 ? "  •  basket stop $" + s0.BasketStop.ToString("0.##", IC) : "  •  NO basket stop") + (s0.MaxTriesADay > 0 ? "  •  max " + s0.MaxTriesADay + (s0.MaxTriesADay == 1 ? " try" : " tries") + " a day" : "") +
                    "\nsessions in New York time; each with the hedges and with NO HEDGE (only the target and the basket stop)\n\n" + string.Join("\n", rows);
            }).ContinueWith(t => Dispatcher.Invoke(() =>
            {
                Busy(false, t.Exception != null ? "the sessions stopped: " + t.Exception.GetBaseException().Message : "sessions done");
                if (t.Exception != null) return; varText = t.Result; view = "VARIATIONS"; ShowView();
            }));
        }

        void PaintResult()
        {
            var r = last; if (r == null) { tiles.Content = null; return; }
            Func<double, string> m = x => (x < 0 ? "−$" : "$") + Math.Abs(x).ToString("N0", IC);
            tiles.Content = Sx.Tiles(
                Tuple.Create("NET", (r.Net >= 0 ? "+" : "") + m(r.Net), r.Net >= 0 ? Sx.Green : Sx.Red),
                Tuple.Create("END EQUITY", m(r.EndEquity), Sx.Text),
                Tuple.Create(r.Blown ? "BLOWN ON" : "ACCOUNT", r.Blown ? r.BlownAt.ToString("dd MMM HH:mm", IC) : "survived", r.Blown ? Sx.Red : Sx.Green),
                Tuple.Create("CYCLES WON / LOST", r.Wins + " / " + r.Losses, Sx.Text),
                Tuple.Create("MAX DRAWDOWN", m(r.MaxDd) + " (" + r.MaxDdPct.ToString("0", IC) + "%)", r.MaxDdPct > 50 ? Sx.Red : Sx.Amber),
                Tuple.Create("LEGS / LOTS (MOST)", r.MaxLegs + " / " + r.MaxGross.ToString("0.00", IC), Sx.Violet),
                Tuple.Create("DAYS AT TARGET", r.Set.DailyTarget > 0 || r.Set.OneTargetADay ? r.TargetDays + " / " + r.Days.Count : "—", Sx.Cyan),
                Tuple.Create("COMM. + SWAP", m(r.Commission - r.Swap), Sx.Muted));
            DrawCurve();
        }

        // the equity every 15 minutes (line) and each day's lowest equity (red dots); the start balance dashed
        void DrawCurve()
        {
            curve.Children.Clear(); var r = last; double w = curve.ActualWidth, h = curve.ActualHeight; if (r == null || r.Curve.Count == 0 || w < 20 || h < 20) return;
            var pts = r.Curve; DateTime t0 = pts[0].Item1, t1 = pts[pts.Count - 1].Item1; double span = Math.Max(1, (t1 - t0).TotalMinutes);
            double lo = Math.Min(0, Math.Min(pts.Min(p => p.Item2), r.Days.Count > 0 ? r.Days.Min(d => d.LowEquity) : 0)), hi = Math.Max(r.Set.Balance, pts.Max(p => p.Item2)) * 1.05; if (hi - lo < 1) hi = lo + 1;
            Func<DateTime, double> X = t => 8 + (w - 16) * Math.Max(0, Math.Min(1, (t - t0).TotalMinutes / span)); Func<double, double> Y = v => 18 + (h - 26) * (1 - (v - lo) / (hi - lo));
            curve.Children.Add(new Line { X1 = 0, X2 = w, Y1 = Y(r.Set.Balance), Y2 = Y(r.Set.Balance), Stroke = Sx.Tint(Sx.Gold, 120), StrokeDashArray = new DoubleCollection { 4, 4 } });
            var pl = new Polyline { Stroke = r.Net >= 0 ? Sx.Green : Sx.Red, StrokeThickness = 2 }; int step = Math.Max(1, pts.Count / 2000);
            for (int i = 0; i < pts.Count; i += step) pl.Points.Add(new Point(X(pts[i].Item1), Y(pts[i].Item2)));
            pl.Points.Add(new Point(X(t1), Y(pts[pts.Count - 1].Item2))); curve.Children.Add(pl);
            foreach (var d in r.Days) { var dot = new Ellipse { Width = 4, Height = 4, Fill = Sx.Tint(Sx.Red, 170) }; Canvas.SetLeft(dot, X(d.Day.AddHours(12)) - 2); Canvas.SetTop(dot, Y(d.LowEquity) - 2); curve.Children.Add(dot); }
            var lab = Sx.Label("equity every 15 minutes  •  red dots = each day's lowest equity  •  dashed = the start balance  •  " + t0.ToString("dd MMM", IC) + " → " + t1.ToString("dd MMM yyyy", IC), 11, Sx.Muted); Canvas.SetLeft(lab, 10); Canvas.SetTop(lab, 2); curve.Children.Add(lab);
        }

        void ShowView()
        {
            foreach (Button b in views.Children) b.Background = (string)b.Content == view ? Sx.Tint(Sx.Cyan, 70) : Sx.Tint(Sx.Card2, 220);
            var r = last; var lines = new List<string>();
            if (view == "VARIATIONS") lines.Add(varText == "" ? "▶ VARIATIONS runs every recent high / low (5 … 120 candles) × half / the same / double target on these days." : varText);
            else if (r == null) lines.Add("▶ RUN to test the settings above.");
            else if (view == "DAYS")
            {
                lines.Add(string.Format("{0,-12} {1,10} {2,10} {3,10} {4,10} {5,7}  {6}", "DAY", "START", "END", "LOWEST", "BANKED", "CYCLES", ""));
                foreach (var d in r.Days) lines.Add(string.Format(IC, "{0,-12} {1,10:N2} {2,10:N2} {3,10:N2} {4,10:N2} {5,7}  {6}", d.Day.ToString("ddd dd MMM", IC), d.StartEquity, d.EndEquity, d.LowEquity, d.Banked, d.Cycles, d.HitTarget ? "✓ day target" : ""));
            }
            else if (view == "CYCLES")
            {
                lines.Add(string.Format("{0,-17} {1,-17} {2,-5} {3,5} {4,10} {5,7}  {6}", "OPENED", "CLOSED", "SIDE", "LEGS", "RESULT", "LOTS", "HOW"));
                foreach (var c in r.Cycles) lines.Add(string.Format(IC, "{0,-17} {1,-17} {2,-5} {3,5} {4,10:N2} {5,7:0.00}  {6}", c.Open.ToString("dd MMM HH:mm", IC), c.Close.ToString("dd MMM HH:mm", IC), c.Dir > 0 ? "BUY" : "SELL", c.Legs, c.Profit, c.MaxGross, c.Reason));
                if (r.OpenAtEnd != 0) lines.Add("still open at the end: " + r.OpenAtEnd.ToString("+0.00;−0.00", IC));
            }
            else { lines.AddRange(r.Log.Count > 20000 ? r.Log.Take(20000).Concat(new[] { "… (the first 20,000 lines)" }) : r.Log); }
            table.Text = string.Join("\n", lines);
        }
    }
}
