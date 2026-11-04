// SpasiX — the 🏁 RACE view inside 🤖 AUTO: every strategy at once in the background (Race.cs), one simulated account each, never MT5.
// 🏆 TODAY = the leaderboard of the day • one tab per strategy (its iterations, trades, story) • 📅 HISTORY = days / weeks / months from
// the saved files, ranked by consistency • SHOW ON 📈 CHART = whose levels, signals and trades the chart draws (one at a time).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        public static readonly string[] RaceKeys = { "MINE", "RNG", "LKL", "LKF", "A", "AH", "B", "C", "D", "F", "BOX" };   // RNG / LKL / LKF = MY SETTINGS + the anti-chop options
        static string RaceName(string k) { return k == "MINE" ? "★ MY SETTINGS" : k == "RNG" ? "★ MINE + range-only hedge" : k == "LKL" ? "★ MINE + 🔒 lock → lead" : k == "LKF" ? "★ MINE + 🔒 lock → lift" : k == "A" ? "A · London engulfing" : k == "AH" ? "A + signal hedge" : k == "B" ? "B · NY FVG 50%" : k == "C" ? "C · all-day VWAP rejection" : k == "D" ? "D · M1 aggression break" : k == "F" ? "F · FVG 50% + FVG hedge" : k == "BOX" ? "HEDGE BOX" : k; }
        readonly List<RaceLane> lanes = new List<RaceLane>(); bool raceOn; string raceShow = "", raceView = "TODAY", racePeriod = "7"; DateTime raceDay = DateTime.MinValue, raceAskAt = DateTime.MinValue;
        DockPanel aRaceHost; StackPanel raceBody, raceTabs; TextBlock raceStatus; Button raceStart; TextBox raceBal, raceComm; ComboBox raceShowBox; readonly Dictionary<string, CheckBox> raceChecks = new Dictionary<string, CheckBox>();

        bool RaceDura() { string sv; lock (broker.State.Gate) sv = broker.State.Account.Server ?? ""; return sv.IndexOf("dura", StringComparison.OrdinalIgnoreCase) >= 0; }   // Duramarkets: 0.001 % of each deal
        string RaceFolder() { var id = Sim ? "backtest" : AutoId(); return Path.Combine(Dir, "race", Mode + (id != "" ? "-" + id : "")); }
        // whose levels / signals / trades the chart draws: a lane of the race, or the 🤖 AUTO itself
        StackPanel raceChips; string raceChipsSig = "";
        // the chart's strategy tabs (only while the race runs): 🤖 MINE + every racing strategy — click = that one on the chart
        void RaceChipsPaint()
        {
            if (raceChips == null) return; bool on = raceOn && lanes.Count > 0; raceChips.Visibility = on ? Visibility.Visible : Visibility.Collapsed; if (!on) { raceChipsSig = ""; return; }
            var sig = raceShow + "|" + string.Join(",", lanes.Select(l => l.Key + (l.Eng.Current != null ? "*" : ""))); if (sig == raceChipsSig) return; raceChipsSig = sig; raceChips.Children.Clear();
            raceChips.Children.Add(new TextBlock { Text = "🏁 SHOW", Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            Action<string, string, bool> chip = (key, label, inTrade) =>
            {
                bool sel = raceShow == key; var b = Pill((inTrade ? "● " : "") + label, sel ? Tint(Cyan, 120) : Tint(Sx.Card2, 220)); b.Height = 26; b.FontSize = 11; b.Margin = new Thickness(0, 0, 4, 0); if (sel) b.Foreground = Bg;
                b.ToolTip = key == "" ? "the real automation (its levels and trades)" : RaceName(key) + " — its own simulated account" + (inTrade ? " • in a trade now" : "");
                b.Click += delegate { int ix = key == "" ? 0 : Array.IndexOf(RaceKeys, key) + 1; if (raceShowBox != null && ix >= 0) raceShowBox.SelectedIndex = ix; else { raceShow = key; foreach (var pq in panes) pq.V.InvalidateVisual(); } raceChipsSig = ""; RaceChipsPaint(); };
                raceChips.Children.Add(b);
            };
            chip("", "🤖 MINE", auto.Current != null);
            foreach (var l in lanes) chip(l.Key, l.Key == "MINE" ? "★ MY SETTINGS" : l.Key, l.Eng.Current != null);
        }
        HedgeAuto ShowEngine() { if (raceShow != "") { var l = lanes.FirstOrDefault(x => x.Key == raceShow); if (l != null) return l.Eng; } return auto; }

        FrameworkElement BuildRace()
        {
            aRaceHost = new DockPanel { Visibility = Visibility.Collapsed, Background = Panel };
            var bar = new WrapPanel { Margin = new Thickness(6, 6, 6, 4) }; DockPanel.SetDock(bar, Dock.Top);
            raceStart = Pill("▶  START RACE", Tint(Green, 90)); raceStart.Height = 34; raceStart.Click += delegate { if (raceOn) RaceStop(); else RaceStartAll(); }; bar.Children.Add(raceStart);
            Func<string, FrameworkElement, StackPanel> fld = (lab, el) => { var sp = new StackPanel { Margin = new Thickness(10, 0, 4, 0) }; sp.Children.Add(new TextBlock { Text = lab, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold }); sp.Children.Add(el); return sp; };
            raceBal = Sx.Input(Get("race.bal", "5000"), 80); raceComm = Sx.Input(Get("race.comm", "0"), 60);   // extra $/lot on top; the DEMO's own % commission is copied to every lane by itself
            bar.Children.Add(fld("BALANCE EACH $", raceBal)); bar.Children.Add(fld("COMMISSION $ / LOT", raceComm));
            var checks = new WrapPanel { Margin = new Thickness(10, 12, 0, 0) };
            foreach (var k in RaceKeys) { var kk = k; var cb = new CheckBox { Content = RaceName(k), Foreground = Text, Margin = new Thickness(0, 0, 12, 0), IsChecked = Get("race.on." + k, "1") == "1" }; cb.Click += delegate { Put("race.on." + kk, cb.IsChecked == true ? "1" : "0"); }; raceChecks[k] = cb; checks.Children.Add(cb); }
            bar.Children.Add(checks);
            raceShowBox = new ComboBox { Width = 230, Margin = new Thickness(0, 0, 0, 0) };
            raceShowBox.Items.Add("🤖 MY AUTO (the real one)"); foreach (var k in RaceKeys) raceShowBox.Items.Add(RaceName(k)); raceShowBox.SelectedIndex = 0;
            raceShowBox.SelectionChanged += delegate { int i = raceShowBox.SelectedIndex; raceShow = i <= 0 ? "" : RaceKeys[i - 1]; foreach (var pq in panes) pq.V.InvalidateVisual(); RacePaint(); };
            bar.Children.Add(fld("SHOW ON 📈 CHART (one at a time)", raceShowBox));
            aRaceHost.Children.Add(bar);
            raceStatus = new TextBlock { Foreground = Muted, FontSize = 12, Margin = new Thickness(8, 0, 8, 4), TextWrapping = TextWrapping.Wrap, Text = "Every checked strategy trades its OWN simulated account on the same prices, in the background — nothing reaches MT5. Each finished iteration is saved for 📅 HISTORY." };
            DockPanel.SetDock(raceStatus, Dock.Top); aRaceHost.Children.Add(raceStatus);
            raceTabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 6, 4) }; var tabsSv = new ScrollViewer { Content = raceTabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; DockPanel.SetDock(tabsSv, Dock.Top); aRaceHost.Children.Add(tabsSv);
            raceBody = new StackPanel(); aRaceHost.Children.Add(new ScrollViewer { Content = raceBody, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
            return aRaceHost;
        }

        // 🏁 the race survives a restart (user's request): every lane's simulated account is saved each minute; after a restart the race starts
        // again by itself as soon as the prices are back, each lane with its own balance and open positions (taken over)
        string RaceStateDir() { return Path.Combine(RaceFolder(), "state"); }
        DateTime raceSavedAt = DateTime.MinValue; bool raceResumePending; DateTime raceResumeAt;
        void RaceSaveState()
        {
            try { var d = RaceStateDir(); Directory.CreateDirectory(d); foreach (var l in lanes) { List<string> lines; lock (l.Br.State.Gate) lines = l.Br.Acct.Save(); File.WriteAllLines(Path.Combine(d, l.Key + ".txt"), lines); } } catch { }
        }
        void RaceTryResume()
        {
            if (raceOn) { raceResumePending = false; return; } if ((DateTime.Now - raceResumeAt).TotalMinutes > 5) { raceResumePending = false; return; }
            string sym = aset.Symbol; bool ready; lock (broker.State.Gate) { Mt5Quote q; ready = broker.State.Quotes.TryGetValue(sym, out q) && q.Bid > 0 && broker.State.SeriesOf(sym, 1).Bars.Count >= 20; }
            if (!ready || (Paper && !paper.Ready)) return; raceResumePending = false; RaceStartAll(true);
        }
        void RaceStartAll() { RaceStartAll(false); }
        void RaceStartAll(bool restore)
        {
            Put("race.bal", raceBal.Text); Put("race.comm", raceComm.Text); double bal, comm; if (!double.TryParse(raceBal.Text.Trim(), NumberStyles.Float, IC, out bal) || bal <= 0) bal = 5000; double.TryParse(raceComm.Text.Trim(), NumberStyles.Float, IC, out comm);
            var mine = AutoRead(); lanes.Clear();
            var acc = Paper ? new SimAccountSettings { Balance = bal, CommissionPerLot = Math.Max(0, comm), Leverage = paper.Acct.Set.Leverage, Kind = paper.Acct.Set.Kind, MarkupPts = paper.Acct.Set.MarkupPts, Currency = paper.Acct.Set.Currency, StopOutPct = paper.Acct.Set.StopOutPct, CommissionPct = paper.Acct.Set.CommissionPct }
                : new SimAccountSettings { Balance = bal, CommissionPerLot = Math.Max(0, comm), Kind = comm > 0 ? "STANDARD" : "RAW", MarkupPts = 0, CommissionPct = RaceDura() ? 0.001 : 0 };
            var market = Sim ? sim.State : live.State; Func<string, bool> send = Sim ? (Func<string, bool>)(l => false) : live.Send;
            foreach (var k in RaceKeys)
            {
                if (raceChecks[k].IsChecked != true) continue;
                var set = k == "MINE" || k == "RNG" || k == "LKL" || k == "LKF" ? mine.Copy() : AutoSettings.Model(k, mine);
                if (k == "RNG") { set.RangeOnly = true; set.LockOn = false; } else if (k == "LKL") { set.LockOn = true; set.Unlock = "LEAD"; set.RangeOnly = true; } else if (k == "LKF") { set.LockOn = true; set.Unlock = "LIFT"; set.RangeOnly = true; } set.Symbol = mine.Symbol; set.TrackManual = false;   // a lane never adopts anything: its account is its own
                var l = new RaceLane { Key = k, Name = RaceName(k), Set = set, Br = new ShadowBroker(market, send, new SimAccountSettings { Balance = acc.Balance, CommissionPerLot = acc.CommissionPerLot, Leverage = acc.Leverage, Kind = acc.Kind, MarkupPts = acc.MarkupPts, Currency = acc.Currency, StopOutPct = acc.StopOutPct, CommissionPct = acc.CommissionPct }) };
                l.StartBalance = l.PeakEq = bal; l.Eng.Send = (c, a, lab) => l.Br.Command(c, a);
                bool had = false;
                if (restore) { try { var sf = Path.Combine(RaceStateDir(), k + ".txt"); if (File.Exists(sf)) { var lines = File.ReadAllLines(sf); lock (l.Br.State.Gate) had = l.Br.Acct.Load(lines); if (had) { lock (l.Br.State.Gate) l.StartBalance = l.PeakEq = l.Br.Acct.Balance; } } } catch { had = false; } }
                var err = l.Eng.Check(l.Br, set); bool open; lock (l.Br.State.Gate) open = l.Br.State.Positions.Count > 0;
                if (err != "") l.Problem = err; else l.Eng.Start(l.Br, set, had && open);
                lanes.Add(l);
            }
            raceOn = true; raceDay = DateTime.MinValue; Put("race.run", "1"); Log(restore ? "🏁 race RESUMED after the restart: " + lanes.Count + " strategies, each with its own account and open positions" : "🏁 race started: " + lanes.Count + " strategies on their own simulated accounts", Green); RacePaint();
        }
        void RaceStop() { foreach (var l in lanes) if (l.Eng.Running) l.Eng.Stop("race stopped"); raceOn = false; Put("race.run", "0"); try { if (Directory.Exists(RaceStateDir())) Directory.Delete(RaceStateDir(), true); } catch { } Log("🏁 race stopped", Amber); RacePaint(); }

        // every step (each backtest tick / every 200 ms live): prices → each lane's account → each lane's engine; finished iterations saved
        void RaceStep()
        {
            if (!raceOn) return;
            if ((DateTime.Now - raceSavedAt).TotalSeconds >= 60) { raceSavedAt = DateTime.Now; RaceSaveState(); }
            foreach (var l in lanes)
            {
                try { l.Step(); } catch (Exception ex) { l.Problem = ex.Message; if (l.Eng.Running) l.Eng.Stop("error: " + ex.Message); }
                var done = l.Eng.Iterations.Where(x => x.Done).ToList(); while (l.Saved < done.Count) { RaceStore.Save(RaceFolder(), l.Key, done[l.Saved], l.Eng.Trades); l.Saved++; }
            }
        }
        // live: the candles every lane needs, asked once from MT5 (the bridge keeps them updated)
        void RaceBars()
        {
            if (!raceOn || Sim || (DateTime.Now - raceAskAt).TotalSeconds < 3) return; raceAskAt = DateTime.Now;
            var need = lanes.SelectMany(l => new[] { Tuple.Create(l.Set.Symbol, l.Set.Tf), Tuple.Create(l.Set.Symbol, l.Set.FvgTf) }).Distinct().ToList();
            lock (live.State.Gate) foreach (var n in need) { var se = live.State.SeriesOf(n.Item1, n.Item2); if (!se.Loaded && (DateTime.Now - se.AskedAt).TotalSeconds > 5) { se.AskedAt = DateTime.Now; live.Send("BARS|" + n.Item1 + "|" + n.Item2 + "|300"); } }
        }

        // ---- the screen
        static string Money(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture); }
        void RacePaint()
        {
            if (raceBody == null || aRaceHost.Visibility != Visibility.Visible) return;
            raceStart.Content = raceOn ? "■  STOP RACE" : "▶  START RACE"; raceStart.Background = raceOn ? Tint(Red, 120) : Tint(Green, 90);
            // the sub-tabs: TODAY, one per strategy, HISTORY
            var tabs = new List<Tuple<string, string>> { Tuple.Create("TODAY", "🏆 TODAY") }; tabs.AddRange(lanes.Select(l => Tuple.Create(l.Key, l.Name))); tabs.Add(Tuple.Create("HISTORY", "📅 HISTORY"));
            if (raceTabs.Children.Count != tabs.Count || raceTabs.Children.Cast<Button>().Select(b => (string)b.Tag).Where((t, i) => t != tabs[i].Item1).Any())
            {
                raceTabs.Children.Clear();
                foreach (var t in tabs) { var tt = t; var b = Pill(t.Item2, Tint(Sx.Card2, 220)); b.Height = 28; b.Tag = t.Item1; b.Click += delegate { raceView = tt.Item1; RacePaint(); }; raceTabs.Children.Add(b); }
            }
            foreach (Button b in raceTabs.Children) b.Background = (string)b.Tag == raceView ? Tint(Cyan, 80) : Tint(Sx.Card2, 220);
            raceBody.Children.Clear();
            if (raceView == "HISTORY") { RaceHistory(); return; }
            var lane = lanes.FirstOrDefault(x => x.Key == raceView);
            if (lane != null) { RaceLaneView(lane); return; }
            // 🏆 TODAY: the leaderboard of the server day
            if (lanes.Count == 0) { raceBody.Children.Add(new TextBlock { Text = "Tick the strategies above and press ▶ START RACE. Each one gets its own simulated $" + raceBal.Text + " account on the live prices" + (Sim ? " (here: the backtest's replay — press ▶ play on the 📈 CHART)" : "") + ".", Foreground = Muted, FontSize = 13, Margin = new Thickness(10) }); return; }
            var today = (lanes[0].Eng.Now > DateTime.MinValue ? lanes[0].Eng.Now : DateTime.Now).Date; int rank = 0;
            foreach (var l in lanes.OrderByDescending(x => x.Equity))
            {
                rank++; var it = l.Eng.Iterations.Where(x => x.Done && x.End.Date == today).ToList(); double net = l.Equity - l.StartBalance; var col = net > 0.005 ? Green : net < -0.005 ? Red : Muted;
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(Cell(rank == 1 ? "🥇" : rank == 2 ? "🥈" : rank == 3 ? "🥉" : "#" + rank, 44, Text, true, 16));
                var nm = new StackPanel { Width = 260 }; nm.Children.Add(new TextBlock { Text = l.Name, Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 14 }); nm.Children.Add(new TextBlock { Text = l.Problem != "" ? "⚠ " + l.Problem : (l.Eng.Running ? l.Eng.Phase : "stopped") + (l.Eng.Current != null ? "  •  basket " + Money(l.Eng.Basket) : ""), Foreground = l.Problem != "" ? Red : Muted, FontSize = 11 }); sp.Children.Add(nm);
                sp.Children.Add(Cell(Money(net), 130, col, true, 18));
                sp.Children.Add(Cell(it.Count + " iterations", 120, Text)); sp.Children.Add(Cell(it.Count(x => x.Won) + " won / " + it.Count(x => !x.Won) + " lost", 140, it.Any(x => !x.Won) ? Amber : Green));
                sp.Children.Add(Cell("worst basket " + (it.Count > 0 ? it.Min(x => x.MaxDd).ToString("0", IC) : "0"), 140, Red)); sp.Children.Add(Cell("account DD −$" + l.MaxDdEq.ToString("0", IC), 140, l.MaxDdEq > 0 ? Red : Muted));
                sp.Children.Add(Cell("max lots " + (it.Count > 0 ? it.Max(x => x.MaxGross).ToString("0.##", IC) : "0"), 100, Text)); sp.Children.Add(Cell("⏱ " + (it.Count > 0 ? Dur(TimeSpan.FromSeconds(it.Average(x => (x.End - x.Start).TotalSeconds))) : "—"), 90, Violet, true, 12.5, Violet));
                var card = new Border { Child = sp, Background = Sx.CardGrad, BorderBrush = l.Key == raceShow ? Cyan : col, BorderThickness = new Thickness(5, 1, 1, 1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(6, 3, 6, 3), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "click: this strategy's own tab" };
                var key = l.Key; card.MouseLeftButtonUp += delegate { raceView = key; RacePaint(); }; raceBody.Children.Add(card);
            }
        }
        void RaceLaneView(RaceLane l)
        {
            double net = l.Equity - l.StartBalance;
            raceBody.Children.Add(new TextBlock { Text = l.Name + "  •  " + (l.Eng.Running ? l.Eng.Phase : "stopped") + "  •  account $" + l.Equity.ToString("N2", IC) + " (" + Money(net) + ")  •  deepest account drop −$" + l.MaxDdEq.ToString("0", IC) + "  •  " + l.Eng.WaitingFor(), Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(8, 6, 8, 2), TextWrapping = TextWrapping.Wrap });
            var show = Pill(raceShow == l.Key ? "👁 ON THE 📈 CHART NOW" : "👁 SHOW THIS ONE ON THE 📈 CHART", raceShow == l.Key ? Tint(Cyan, 90) : Tint(Sx.Card2, 220)); show.HorizontalAlignment = HorizontalAlignment.Left; show.Margin = new Thickness(8, 2, 8, 6);
            show.Click += delegate { raceShowBox.SelectedIndex = Array.IndexOf(RaceKeys, l.Key) + 1; }; raceBody.Children.Add(show);
            var rows = new StackPanel(); AutoRowsOf(l.Eng, rows, "ITERATIONS"); raceBody.Children.Add(rows);
            raceBody.Children.Add(new TextBlock { Text = "TRADES", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 10, 8, 2) });
            var tr = new StackPanel(); AutoRowsOf(l.Eng, tr, "TRADES"); raceBody.Children.Add(tr);
            raceBody.Children.Add(new TextBlock { Text = "STORY (newest first)", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 10, 8, 2) });
            raceBody.Children.Add(new TextBlock { Text = string.Join("\n", l.Eng.Story.AsEnumerable().Reverse().Take(60).Select(x => "#" + x.Item1 + "  " + x.Item2)), Foreground = Text, FontFamily = Sx.Mono, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 8, 10) });
        }
        void RaceHistory()
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 6) };
            foreach (var p in new[] { Tuple.Create("1", "TODAY"), Tuple.Create("7", "7 DAYS"), Tuple.Create("30", "30 DAYS"), Tuple.Create("90", "3 MONTHS"), Tuple.Create("3650", "ALL") }) { var pp = p; var b = Pill(p.Item2, p.Item1 == racePeriod ? Tint(Cyan, 80) : Tint(Sx.Card2, 220)); b.Height = 26; b.Click += delegate { racePeriod = pp.Item1; RacePaint(); }; bar.Children.Add(b); }
            raceBody.Children.Add(bar);
            var now = (lanes.Count > 0 && lanes[0].Eng.Now > DateTime.MinValue ? lanes[0].Eng.Now : DateTime.Now).Date; var rows = RaceStore.Load(RaceFolder(), now.AddDays(-(int.Parse(racePeriod, IC) - 1)));
            if (rows.Count == 0) { raceBody.Children.Add(new TextBlock { Text = "nothing saved for this period yet — run the race; every finished iteration lands in " + RaceFolder(), Foreground = Muted, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap }); return; }
            raceBody.Children.Add(new TextBlock { Text = "THE MOST CONSISTENT FIRST — score = the average day ÷ how much the days swing", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 2, 8, 4) });
            raceBody.Children.Add(Row(Line, Cell("#", 36, Muted, true, 11), Cell("STRATEGY", 230, Muted, true, 11), Cell("NET", 110, Muted, true, 11), Cell("PER DAY", 100, Muted, true, 11), Cell("GREEN DAYS", 110, Muted, true, 11), Cell("WORST DAY", 100, Muted, true, 11), Cell("BEST DAY", 100, Muted, true, 11), Cell("ITERATIONS", 100, Muted, true, 11), Cell("WON", 70, Muted, true, 11), Cell("WORST BASKET", 110, Muted, true, 11), Cell("MAX LOTS", 80, Muted, true, 11), Cell("⏱ AVG", 80, Muted, true, 11), Cell("SCORE", 70, Muted, true, 11)));
            int r = 0;
            foreach (var s in RaceStore.Stats(rows))
            {
                r++; var col = s.Net > 0 ? Green : Red;
                raceBody.Children.Add(Row(col, Cell(r == 1 ? "🥇" : r == 2 ? "🥈" : r == 3 ? "🥉" : "#" + r, 36, Text, true), Cell(RaceName(s.Key), 230, Text, true), Cell(Money(s.Net), 110, col, true, 13.5), Cell(Money(s.PerDay), 100, s.PerDay >= 0 ? Green : Red),
                    Cell(s.GreenDays + " / " + s.Days, 110, s.GreenDays * 2 >= s.Days ? Green : Amber, true, 12, s.GreenDays * 2 >= s.Days ? Green : Amber), Cell(Money(s.WorstDay), 100, s.WorstDay < 0 ? Red : Green), Cell(Money(s.BestDay), 100, Green),
                    Cell(s.Iterations.ToString(IC), 100, Text), Cell((100.0 * s.Wins / Math.Max(1, s.Iterations)).ToString("0", IC) + "%", 70, Text), Cell(s.WorstDd.ToString("0", IC), 110, Red), Cell(s.MaxLots.ToString("0.##", IC), 80, Text), Cell(Dur(TimeSpan.FromSeconds(s.AvgSecs)), 80, Violet), Cell(s.Score.ToString("0.00", IC), 70, Cyan, true)));
            }
            raceBody.Children.Add(new TextBlock { Text = "EVERY DAY", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 12, 8, 4) });
            var keys = rows.Select(x => x.Key).Distinct().ToList();
            raceBody.Children.Add(Row(Line, new[] { Cell("DAY", 110, Muted, true, 11) }.Concat(keys.Select(k => Cell(RaceName(k), 150, Muted, true, 11))).ToArray()));
            foreach (var d in rows.GroupBy(x => x.Day).OrderByDescending(g => g.Key))
                raceBody.Children.Add(Row(Line, new[] { Cell(d.Key.ToString("ddd dd MMM", IC), 110, Text, true) }.Concat(keys.Select(k => { var v = d.Where(x => x.Key == k).ToList(); double n = v.Sum(x => x.Result); return Cell(v.Count == 0 ? "—" : Money(n) + " (" + v.Count + ")", 150, v.Count == 0 ? Muted : n >= 0 ? Green : Red, v.Count > 0); })).ToArray()));
        }
    }
}
