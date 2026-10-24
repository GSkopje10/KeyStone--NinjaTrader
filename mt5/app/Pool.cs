// SpasiX — 🗂 POOL (the backtest): every finished day of a test is saved on this PC with all its numbers and trades (%AppData%\SpasiX\MT5\
// backtest-pool\TEST\days.txt + trades.txt) — by itself when a day ends in the replay, or with 💾 SAVE THE DAY NOW. A TEST is a folder: NEW TEST keeps
// the old ones, CLEAR empties the chosen one (asks). The tab shows the totals and the days / weeks / months / years; 🌐 HTML TRACKER writes the
// same as a web page (calendar, equity curve, tables, trades) and opens it in the browser.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        int poolDaysSeen; string poolBy = "DAYS", poolSig = ""; StackPanel poolRows; WrapPanel poolCards; TextBlock poolHead; Button poolTestBtn; readonly List<Tuple<string, Button>> poolByBtns = new List<Tuple<string, Button>>();
        string PoolTest { get { return Get("bt.test", "Test 1"); } }

        // a day ended in the replay → saved in the test (only days with trades)
        void PoolWatch()
        {
            var cd = sim.ClosedDays; if (poolDaysSeen > cd.Count) poolDaysSeen = 0;
            for (; poolDaysSeen < cd.Count; poolDaysSeen++) { if (PoolSaveDay(cd[poolDaysSeen])) Toast("🗂 " + cd[poolDaysSeen].ToString("ddd dd MMM", IC) + " saved in the POOL (" + PoolTest + ")"); }
        }
        bool PoolSaveDay(DateTime day)
        {
            Tuple<PoolDay, List<PoolTrade>> d; lock (sim.State.Gate) d = BtPool.Build(sim.State.Deals.ToList(), day, sim.Set != null ? sim.Set.Kind : "");
            if (d == null) return false; try { BtPool.Save(PoolTest, d.Item1, d.Item2); poolSig = ""; return true; } catch { return false; }
        }
        void PoolSaveAll() { List<DateTime> ds; lock (sim.State.Gate) ds = sim.State.Deals.Where(x => x.IsTrade).Select(x => x.Time.Date).Distinct().ToList(); foreach (var d in ds) PoolSaveDay(d); }

        FrameworkElement BuildPool()
        {
            var sp = new StackPanel { MaxWidth = 1240, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(4, 4, 4, 10) };
            var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            top.Children.Add(new TextBlock { Text = "🗂 POOL", Foreground = Gold, FontSize = 22, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
            poolTestBtn = Sx.Drop("TEST: " + PoolTest, Tint(Gold, 60), PoolMenu); poolTestBtn.Foreground = Gold; poolTestBtn.Height = 34; top.Children.Add(poolTestBtn);
            var nw = Pill("＋ NEW TEST", Card); nw.Height = 34; nw.ToolTip = "a new, empty test — the old ones stay"; nw.Click += delegate { NewPoolTest(); }; top.Children.Add(nw);
            var save = Pill("💾 SAVE THE DAY NOW", Card); save.Height = 34; save.ToolTip = "save the replay's current day as it is now (the finished days are saved by themselves)"; save.Click += delegate { if (sim.Spec == null) { Toast("start a backtest first"); return; } Toast(PoolSaveDay(sim.Clock.Date) ? "🗂 " + sim.Clock.ToString("ddd dd MMM", IC) + " saved" : "no trade today yet"); }; top.Children.Add(save);
            var html = Pill("🌐 HTML TRACKER", Tint(Cyan, 200)); html.Foreground = Bg; html.Height = 34; html.ToolTip = "the test as a web page: calendar, equity curve, weeks / months / years, every trade — opens in the browser"; html.Click += delegate { OpenPoolHtml(); }; top.Children.Add(html);
            var clr = Pill("🧹 CLEAR THIS TEST", Card); clr.Foreground = Red; clr.Height = 34; clr.Click += delegate { if (Sx.Ask(this, "🧹 Clear the test", "Remove every saved day of '" + PoolTest + "'? The test stays (empty).", "CLEAR", "KEEP", Red)) { BtPool.Clear(PoolTest); poolSig = ""; } }; top.Children.Add(clr);
            sp.Children.Add(top);
            poolHead = new TextBlock { Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 8) }; sp.Children.Add(poolHead);
            poolCards = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) }; sp.Children.Add(poolCards);
            var byRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var b in new[] { "DAYS", "WEEKS", "MONTHS", "YEARS" }) { var bb = b; var p = Pill(b, Card); p.Click += delegate { poolBy = bb; poolSig = ""; }; byRow.Children.Add(p); poolByBtns.Add(Tuple.Create(b, p)); }
            sp.Children.Add(byRow);
            poolRows = new StackPanel(); sp.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 12, 10), Child = poolRows });
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        List<Mi> PoolMenu()
        {
            var l = new List<Mi> { Mi.H("tests on this PC") }; var ts = BtPool.Tests(); if (!ts.Contains(PoolTest)) ts.Insert(0, PoolTest);
            foreach (var t in ts) { var tt = t; l.Add(new Mi { Text = t, Check = t == PoolTest, Radio = true, Hint = BtPool.Days(t).Count + " days", Do = () => { Put("bt.test", tt); poolSig = ""; poolTestBtn.Content = "TEST: " + tt + "  ▾"; } }); }
            l.Add(Mi.S()); l.Add(new Mi("＋", "New test …", NewPoolTest));
            l.Add(new Mi { Icon = "🗑", Text = "Delete '" + PoolTest + "' …", Danger = true, Do = () => { if (Sx.Ask(this, "🗑 Delete the test", "Delete the test '" + PoolTest + "' with all its days?", "DELETE", "KEEP", Red)) { BtPool.Delete(PoolTest); Put("bt.test", BtPool.Tests().FirstOrDefault() ?? "Test 1"); poolTestBtn.Content = "TEST: " + PoolTest + "  ▾"; poolSig = ""; } } });
            return l;
        }
        void NewPoolTest()
        {
            var n = Sx.Prompt(this, "＋ New test", "A name for the new test (the old tests stay in the list):", "Test " + (BtPool.Tests().Count + 1)); if (string.IsNullOrWhiteSpace(n)) return;
            n = n.Trim(); BtPool.Create(n); Put("bt.test", n); poolTestBtn.Content = "TEST: " + n + "  ▾"; poolSig = ""; poolDaysSeen = sim.ClosedDays.Count; Toast("🗂 new test '" + n + "' — the next finished days are saved there");
        }
        void OpenPoolHtml()
        {
            try
            {
                var days = BtPool.Days(PoolTest); if (days.Count == 0) { Toast("no saved day in '" + PoolTest + "' yet"); return; }
                var path = Path.Combine(BtPool.DirOf(PoolTest), "tracker.html"); File.WriteAllText(path, BtPool.Html(PoolTest, days, BtPool.Trades(PoolTest)), new UTF8Encoding(false));
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); Toast("🌐 the tracker opens in your browser");
            }
            catch (Exception ex) { Toast("✖ " + ex.Message); }
        }
        void FillPool()
        {
            if (poolRows == null) return; var days = BtPool.Days(PoolTest);
            string sig = PoolTest + "|" + poolBy + "|" + days.Count + "|" + days.Sum(d => d.Net).ToString("0.00", IC) + "|" + days.Select(d => d.SavedAt).DefaultIfEmpty().Max().Ticks; if (sig == poolSig) return; poolSig = sig;
            foreach (var t in poolByBtns) { bool on = t.Item1 == poolBy; t.Item2.Background = on ? Gold : (Brush)Card; t.Item2.Foreground = on ? Bg : Text; }
            if (poolTestBtn != null) poolTestBtn.Content = "TEST: " + PoolTest + "  ▾";
            double net = days.Sum(d => d.Net); int tr = days.Sum(d => d.Trades), w = days.Sum(d => d.Wins);
            poolHead.Text = days.Count == 0 ? "No day saved in '" + PoolTest + "' yet — play a backtest: every finished day with trades is saved here by itself (or 💾 SAVE THE DAY NOW)." : "'" + PoolTest + "' • " + days.Count + " day(s) • " + days.Min(d => d.Day).ToString("dd MMM yyyy", IC) + " → " + days.Max(d => d.Day).ToString("dd MMM yyyy", IC) + " • " + string.Join(", ", days.SelectMany(d => d.Syms.Split('+')).Where(x => x != "").Distinct());
            poolCards.Children.Clear();
            Action<string, string, Brush> card = (k, v, fg) => { var s0 = new StackPanel(); s0.Children.Add(new TextBlock { Text = k, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold }); s0.Children.Add(new TextBlock { Text = v, Foreground = fg, FontSize = 17, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono }); poolCards.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(11, 5, 12, 6), Margin = new Thickness(0, 0, 7, 7), MinWidth = 120, BorderBrush = Line, BorderThickness = new Thickness(1), Child = s0 }); };
            card("NET", Signed(net, "USD"), net > 0 ? Green : net < 0 ? Red : Text); card("DAYS", days.Count + " (" + days.Count(d => d.Net > 0) + " green)", Text); card("TRADES", tr.ToString(IC), Text); card("WIN RATE", tr > 0 ? (100.0 * w / tr).ToString("0", IC) + " %" : "—", Text);
            card("AVG DAY", days.Count > 0 ? Signed(net / days.Count, "USD") : "—", net >= 0 ? Green : Red); card("BEST DAY", days.Count > 0 ? Signed(days.Max(d => d.Net), "USD") : "—", days.Count > 0 && days.Max(d => d.Net) < 0 ? Red : Green); card("WORST DAY", days.Count > 0 ? Signed(days.Min(d => d.Net), "USD") : "—", days.Count > 0 && days.Min(d => d.Net) >= 0 ? Green : Red); card("COMMISSION", Money(days.Sum(d => d.Comm), "USD"), Red);
            poolRows.Children.Clear();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var h in new[] { Tuple.Create(poolBy.TrimEnd('S'), 220.0), Tuple.Create("DAYS", 60.0), Tuple.Create("TRADES", 70.0), Tuple.Create("WIN %", 60.0), Tuple.Create("COMMISSION", 100.0), Tuple.Create("SWAP", 90.0), Tuple.Create("NET", 110.0) }) head.Children.Add(Cell(h.Item1, Muted, h.Item2, h.Item1 == "NET"));
            poolRows.Children.Add(head);
            var gs = days.GroupBy(d => BtPool.PeriodKey(d.Day, poolBy)).OrderByDescending(g => g.Key).ToList(); double mx = gs.Count > 0 ? Math.Max(1, gs.Max(g => Math.Abs(g.Sum(x => x.Net)))) : 1;
            foreach (var g in gs.Take(400))
            {
                double n = g.Sum(x => x.Net); int t = g.Sum(x => x.Trades), ww = g.Sum(x => x.Wins);
                var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                r.Children.Add(Cell(BtPool.PeriodName(g.Key, poolBy), Text, 220, false, true)); r.Children.Add(Cell(g.Count().ToString(IC), Muted, 60)); r.Children.Add(Cell(t.ToString(IC), Text, 70)); r.Children.Add(Cell(t > 0 ? (100.0 * ww / t).ToString("0", IC) : "—", Text, 60));
                r.Children.Add(Cell(Money(g.Sum(x => x.Comm), "USD"), Red, 100)); r.Children.Add(Cell(Signed(g.Sum(x => x.Swap), "USD"), Muted, 90)); r.Children.Add(Cell(Signed(n, "USD"), n >= 0 ? Green : Red, 110, true, true));
                r.Children.Add(new Border { Width = Math.Max(2, 200 * Math.Abs(n) / mx), Height = 8, CornerRadius = new CornerRadius(4), Background = n >= 0 ? Green : Red, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 });
                poolRows.Children.Add(r);
            }
            if (gs.Count == 0) poolRows.Children.Add(new TextBlock { Text = "—", Foreground = Muted });
        }
    }
}
