// SpasiX — the 📅 DAY tab: DayLab.cs on this window's candles (MT5's / the BACKTEST's): which entry setup would have made the most on a day with the
// 🤖 AUTO's settings, ranked, saved per day (DataDir\auto\day-setups-SYMBOL.csv), and every saved day with its winner. LIVE / PROP / DEMO: the
// day that just ended is analysed by itself at the server's midnight.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        TextBox dyDate, dyTable; TextBlock dyHead; ContentControl dyTiles; DispatcherTimer dyTimer; DateTime dyLastDay = DateTime.MinValue;

        FrameworkElement BuildDay()
        {
            var root = new DockPanel { LastChildFill = true };
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(head, Dock.Top);
            head.Children.Add(new TextBlock { Text = "📅 DAY — which entry setup would have made the most today", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Gold });
            head.Children.Add(new TextBlock { Text = "The day is replayed four times through the BACKTEST's broker — BREAK, FVG, VWAP and ORDER FLOW entries — each with the 🤖 AUTO tab's other settings (symbol, timeframe, lots, hedge trigger, target, drawdown, trading times, news, add to the winner). Ranked by the day's net and saved; the history below shows which setup won each day. Simulated from the minute candles: a guide, not a promise.", Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
            var bar = new WrapPanel();
            bar.Children.Add(new TextBlock { Text = "SERVER DAY", Foreground = Muted, FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            dyDate = Sx.Input(DateTime.Today.ToString("yyyy-MM-dd", IC), 120); bar.Children.Add(dyDate);
            var prev = Pill("◀", Tint(Sx.Card2, 220)); prev.Margin = new Thickness(6, 0, 4, 0); prev.Click += delegate { DayShift(-1); }; bar.Children.Add(prev);
            var next = Pill("▶", Tint(Sx.Card2, 220)); next.Click += delegate { DayShift(1); }; bar.Children.Add(next);
            var run = Pill("▶  ANALYZE THIS DAY", Tint(Green, 80)); run.Margin = new Thickness(10, 0, 6, 0); run.Click += delegate { DayRun(DayPicked(), true); }; bar.Children.Add(run);
            dyHead = new TextBlock { Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), TextWrapping = TextWrapping.Wrap }; bar.Children.Add(dyHead);
            head.Children.Add(bar); root.Children.Add(head);
            dyTiles = new ContentControl { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(dyTiles, Dock.Top); root.Children.Add(dyTiles);
            dyTable = new TextBox { IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 12.5, Background = Panel, Foreground = Text, BorderBrush = Line, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, Padding = new Thickness(8) };
            root.Children.Add(dyTable);
            // live: the day that just ended, by itself, once (server midnight)
            dyTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(20) };
            dyTimer.Tick += delegate
            {
                if (Sim) return; DateTime now; lock (broker.State.Gate) now = broker.State.ServerNow; if (now == DateTime.MinValue) return;
                if (dyLastDay == DateTime.MinValue) { dyLastDay = now.Date; return; }
                if (now.Date > dyLastDay) { var ended = dyLastDay; dyLastDay = now.Date; if (ended.DayOfWeek != DayOfWeek.Saturday && ended.DayOfWeek != DayOfWeek.Sunday) DayRun(ended, false); }
            };
            dyTimer.Start(); Closed += delegate { dyTimer.Stop(); };
            // the day shown: this window's server day (the BACKTEST's replay day), until you change it
            bool touched = false; dyDate.TextChanged += delegate { if (dyDate.IsKeyboardFocusWithin) touched = true; };
            root.IsVisibleChanged += delegate { if (!root.IsVisible || touched) return; DateTime now; lock (broker.State.Gate) now = broker.State.ServerNow; if (now > DateTime.MinValue) { dyDate.Text = now.ToString("yyyy-MM-dd", IC); DayShift(0); } };
            DayPaint(null, ""); return root;
        }
        DateTime DayPicked() { DateTime d; return DateTime.TryParseExact((dyDate.Text ?? "").Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out d) ? d : DateTime.Today; }
        void DayShift(int n) { var d = DayPicked().AddDays(n); while (n != 0 && d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(n); dyDate.Text = d.ToString("yyyy-MM-dd", IC); var f = DayLab.FileOf(Dir, AutoRead().Symbol); DayPaint(DayLab.Load(f).Where(x => x.Day == d).OrderByDescending(x => x.Net).ToList(), ""); }

        void DayRun(DateTime day, bool asked)
        {
            var s = AutoRead(); SimSpec spec = null; List<Mt5Bar> m1; int spread = -1; double bal;
            lock (broker.State.Gate)
            {
                var st = broker.State; Mt5Symbol ms; if (Sim) spec = sim.Specs.FirstOrDefault(x => string.Equals(x.Name, s.Symbol, StringComparison.OrdinalIgnoreCase));
                if (spec == null && st.Symbols.TryGetValue(s.Symbol, out ms)) spec = SimSpec.From(ms, st.ServerOffsetSec, s.NyOffsetHours, st.Account.Currency);
                var se = st.SeriesOf(s.Symbol, 1); m1 = se.Bars.Where(b => b.Time >= day.AddDays(-3) && b.Time < day.AddDays(1)).Select(b => b.Copy()).ToList();
                if (!Sim && (m1.Count(b => b.Time.Date == day) < 60 || !se.Loaded)) broker.Send("BARS|" + s.Symbol + "|1|5000");
                Mt5Quote q; if (m1.All(b => b.Spread <= 0) && st.Quotes.TryGetValue(s.Symbol, out q) && spec != null && spec.Point > 0) spread = (int)Math.Round((q.Ask - q.Bid) / spec.Point);
                bal = st.Account.Balance > 0 ? st.Account.Balance : 10000;
            }
            if (spec == null) { if (asked) Sx.Info(this, "📅 DAY", s.Symbol + " is not known yet (the 🤖 AUTO tab's symbol).", Amber); return; }
            if (m1.Count(b => b.Time.Date == day) < 30) { if (asked) Sx.Info(this, "📅 DAY", "Not enough one-minute candles of " + s.Symbol + " on " + day.ToString("ddd dd MMM yyyy", IC) + " here yet" + (Sim ? " — the backtest has not replayed that day." : " — they were asked from MT5, try again in a few seconds."), Amber); return; }
            var res = DayLab.Run(spec, m1, day, s, bal, spread, AutoNews);
            DayLab.Save(DayLab.FileOf(Dir, spec.Name), res); dyDate.Text = day.ToString("yyyy-MM-dd", IC);
            DayPaint(res, (asked ? "" : "the day that ended, analysed by itself • ") + "saved to " + DayLab.FileOf(Dir, spec.Name));
        }

        void DayPaint(List<DayResult> res, string note)
        {
            if (dyTable == null) return; var s = AutoRead(); var all = DayLab.Load(DayLab.FileOf(Dir, s.Symbol)); var L = new List<string>();
            dyHead.Text = note != "" ? note : s.Symbol + " • " + all.Select(x => x.Day).Distinct().Count() + " days saved";
            if (res != null && res.Count > 0)
            {
                var w = res[0];
                dyTiles.Content = Sx.Tiles(Tuple.Create("BEST SETUP " + w.Day.ToString("ddd dd MMM", IC).ToUpperInvariant(), DayLab.ModeName(w.Mode), w.Net > 0 ? Green : Red), Tuple.Create("ITS NET", Signed(w.Net, "USD"), w.Net >= 0 ? Green : Red), Tuple.Create("ITERATIONS WON / LOST", w.Won + " / " + w.Lost, Text), Tuple.Create("WORST DRAWDOWN", w.WorstDd.ToString("0.00", IC), Red));
                L.Add(res[0].Day.ToString("dddd dd MMMM yyyy", IC) + "  •  " + s.Symbol + "  •  the 🤖 AUTO settings with each entry setup");
                L.Add(string.Format("{0,-20} {1,6} {2,5} {3,5} {4,8} {5,6} {6,12} {7,11} {8,10} {9,10}  {10}", "SETUP", "ITER.", "WON", "LOST", "ADD-ONS", "LEGS", "NET", "WORST DD", "BEST", "WORST", ""));
                foreach (var r in res) L.Add(string.Format(IC, "{0,-20} {1,6} {2,5} {3,5} {4,8} {5,6} {6,12} {7,11:N2} {8,10:N2} {9,10:N2}  {10}", DayLab.ModeName(r.Mode), r.Iterations, r.Won, r.Lost, r.MaxAddOns, r.Legs, Signed(r.Net, "USD"), r.WorstDd, r.Best, r.Worst, r.Note));
                L.Add("");
            }
            L.Add("EVERY SAVED DAY — the winner of each day");
            L.Add(string.Format("{0,-16} {1,-20} {2,12}   {3}", "DAY", "WINNER", "ITS NET", "THE OTHERS"));
            foreach (var g in all.GroupBy(x => x.Day).OrderByDescending(g => g.Key)) { var o = g.OrderByDescending(x => x.Net).ToList(); L.Add(string.Format(IC, "{0,-16} {1,-20} {2,12}   {3}", g.Key.ToString("ddd dd MMM yy", IC), DayLab.ModeName(o[0].Mode), Signed(o[0].Net, "USD"), string.Join("  ", o.Skip(1).Select(x => x.Mode + " " + x.Net.ToString("+0;−0;0", IC))))); }
            if (all.Count > 0)
            {
                L.Add(""); L.Add("OVER ALL SAVED DAYS");
                foreach (var g in all.GroupBy(x => x.Mode).OrderByDescending(g => g.Sum(x => x.Net))) { int days = all.Select(x => x.Day).Distinct().Count(), won = all.GroupBy(x => x.Day).Count(d => d.OrderByDescending(x => x.Net).First().Mode == g.Key); L.Add(string.Format(IC, "  {0,-20} best on {1,3} of {2,3} days   net {3,12}   iterations {4,5} (won {5})   worst drawdown {6,10:N2}", DayLab.ModeName(g.Key), won, days, Signed(g.Sum(x => x.Net), "USD"), g.Sum(x => x.Iterations), g.Sum(x => x.Won), g.Min(x => x.WorstDd))); }
            }
            dyTable.Text = string.Join("\n", L);
        }
    }
}
