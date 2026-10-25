// EVAL PASS STUDY 2026: 50 evaluations in a batch (a new batch of 50 only when every one of them has passed or blown — evaluations
// only, the passed ones never trade funded), the MNQ + MGC rotation from 18:00 to the end of the day, the firm's rules (Lucid Flex
// 50K: +$3,000 target, $2,000 EOD drawdown, 50 % consistency = two days of +$1,500, 2 days minimum). The app's own engine
// (KeystoneRotationLab.Run = the PROP SIM's what-if), so every number is what the studio would show.
// Compared: the sides (BUY BOTH • RANDOM BB/SS • the 📡 entry indicator trained on 2025 only, AUTO), the stop of a trade ($1,000 =
// two tries, $500 = four), tries a day (until the +$1,500 day target vs ONE trade a day), the size (10 / 15 / 20 MNQ + the same MGC),
// a breakeven stop (off / at +$250 / +$500 / +$750 / +$1,000) and the session the rotation starts in.
// + every day: one trade at every full hour × every side, to see which hour / side would have reached the target that day.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;

public static class EvalPassStudy
{
    sealed class Cfg { public string Dir = "BUY"; public double Stop = 1000, Be; public int Qty = 10, Tries; public int Start = 1800, End = 1645; public int Seed = 7; public string Label = ""; public string Dd = "EOD"; }
    sealed class Res
    {
        public Cfg C; public KeystonePropSim Sim; public int Bought, Passed, Blown, Open, Trades, Wins, ZeroBatches, Batches, BestBatch, BestStreak, ZeroStreak; public double PassRate, AvgDays, Cost, CostPerPass; public int TwoDay;
    }
    static KeystoneSimSettings Base()
    {
        var r = KeystoneSimRules.Presets().First(x => x.Name == "LUCID FLEX 50K").Copy();
        r.Target = 3000; r.EvalDrawdown = 2000; r.EvalDdType = "EOD"; r.EvalConsistency = 50; r.EvalMinDays = 2; r.MaxMicros = 40; r.FlatByHhmm = 1645;
        return new KeystoneSimSettings { Strategy = "ROTATION", Accounts = 50, StartAs = "EVAL", FundedWhen = "ASK", BuyRule = "BATCH", Allocation = "ROTATE", ReplacePassed = true, DayPlan = "NONE", MaxTradesPerDay = 0,
            PauseSeconds = 60, Auto = true, AutoFallback = "WAIT", TrailStop = false, RotTarget = 1500, FundRotTarget = 1500, Rules = r, AcctDayTarget = 0, AcctDayStop = 0, MinRoomShare = 0.25 };   // an evaluation too close to its floor to carry a quarter stop is dead (counted as failed)
    }
    static KeystoneSimSettings Of(Cfg c, Dictionary<string, string> inds)
    {
        var s = Base(); s.Direction = c.Dir; s.RotStop = c.Stop; s.FundRotStop = c.Stop; s.BreakevenAt = c.Be; s.MaxTradesPerDay = c.Tries; s.MnqQty = s.MgcQty = s.FundMnqQty = s.FundMgcQty = c.Qty; s.Seed = c.Seed;
        s.Rules.EvalDdType = c.Dd; s.StartHhmm = c.Start; s.EndHhmm = c.End; if (c.Dir == "INDICATOR") s.Indicator = inds[c.Qty + "|" + c.Stop];
        return s;
    }
    static Res Measure(Cfg c, KeystonePropSim sim)
    {
        var r = new Res { C = c, Sim = sim, Bought = sim.Bought, Passed = sim.PassedCount, Blown = sim.BlownEval + sim.Accounts.Count(a => a.Phase == "RETIRED" && a.WasPhase == "EVAL"), Trades = sim.Trades.Count, Wins = sim.Trades.Count(t => t.Pnl > 0) };
        r.Open = sim.Accounts.Count(a => a.Phase == "EVAL"); r.PassRate = r.Passed + r.Blown == 0 ? 0 : 100.0 * r.Passed / (r.Passed + r.Blown);
        var passed = sim.Accounts.Where(a => a.PassedOn != DateTime.MinValue).ToList();
        r.AvgDays = passed.Count == 0 ? 0 : passed.Average(a => sim.Trades.Where(t => t.Account == a.Id).Select(t => t.Day).Distinct().Count());
        r.TwoDay = passed.Count(a => sim.Trades.Where(t => t.Account == a.Id).Select(t => t.Day).Distinct().Count() <= 2);
        r.Cost = sim.Spent; r.CostPerPass = r.Passed == 0 ? double.NaN : r.Cost / r.Passed;
        var b = sim.Batches; r.Batches = b.Count; r.ZeroBatches = b.Count(x => x.Passed == 0); r.BestBatch = b.Count == 0 ? 0 : b.Max(x => x.Passed);
        int cur = 0, cz = 0; foreach (var x in b) { if (x.Passed > 0) { cur++; cz = 0; } else { cz++; cur = 0; } r.BestStreak = Math.Max(r.BestStreak, cur); r.ZeroStreak = Math.Max(r.ZeroStreak, cz); }
        return r;
    }
    static string P(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.0", CultureInfo.InvariantCulture); }
    static string D(double v) { return double.IsNaN(v) ? "—" : "$" + v.ToString("N0", CultureInfo.InvariantCulture); }
    static string Sd(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N0", CultureInfo.InvariantCulture); }
    static string Name(Cfg c) { return (c.Dir == "BUY" ? "BUY BOTH" : c.Dir == "RANDOM" ? "RANDOM BB/SS" : c.Dir == "INDICATOR" ? "📡 INDICATOR" : c.Dir) + " • stop $" + c.Stop + " • " + (c.Tries == 1 ? "1 trade/day" : "until +$1,500/day") + " • " + c.Qty + "+" + c.Qty + (c.Be > 0 ? " • BE +$" + c.Be : "") + (c.Start != 1800 ? " • from " + KeystoneBracket.Hm(c.Start) : ""); }

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var all = KeystoneStudioStore.Sessions(folder, "MNQ").Keys.Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday).ToList();
        var test = KeystoneRotationLab.LoadDays(folder, all.Where(d => d >= new DateTime(2026, 1, 1)));
        var train = KeystoneRotationLab.LoadDays(folder, all.Where(d => d >= new DateTime(2025, 1, 1) && d < new DateTime(2026, 1, 1)));
        Console.WriteLine("test days " + test.Count + " (" + test.First().Day.ToString("yyyy-MM-dd") + " → " + test.Last().Day.ToString("yyyy-MM-dd") + "), indicator training days " + train.Count + " (2025)");
        // 📡 the entry indicator, built on 2025 only (never sees 2026) for every size × stop
        var inds = new Dictionary<string, string>(); var indDesc = new Dictionary<string, string>();
        Parallel.ForEach(new[] { 10, 15, 20 }.SelectMany(q => new[] { 1000.0, 500.0 }.Select(st => Tuple.Create(q, st))).ToList(), qs =>
        {
            var s = Base(); s.MnqQty = s.MgcQty = qs.Item1; s.RotStop = qs.Item2; s.StartHhmm = 1800; s.EndHhmm = 1645;
            var ind = KeystoneEntryIndicator.Build(s, train, 2, 30, 3, null); lock (inds) { inds[qs.Item1 + "|" + qs.Item2] = ind.ToText(); indDesc[qs.Item1 + "|" + qs.Item2] = ind.Describe(); }
        });
        Console.WriteLine("indicators built");
        if (Environment.GetEnvironmentVariable("EP_DEBUG") == "1")
        {
            var c = new Cfg { Dir = "BUY", Stop = 500, Tries = 0, Qty = 15 }; var sim = KeystoneRotationLab.Run(Of(c, inds), test, 1800, 1645, null);
            foreach (var a in sim.Accounts.Where(x => x.Phase == "EVAL"))
            {
                var tr = sim.Trades.Where(t => t.Account == a.Id).ToList();
                Console.WriteLine(a.Name + " bal " + a.Bal + " best " + a.BestDay + " days " + a.Days + " trades " + tr.Count + " last " + (tr.Count > 0 ? tr.Last().Day.ToString("yyyy-MM-dd") + " " + tr.Last().Pnl.ToString("0") : "") + " locked " + a.LockedToday + " note " + a.DayNote + " passesAtClose " + sim.PassesAtClose(a) + " floor " + sim.Floor(a));
                foreach (var t in tr) Console.WriteLine("   " + t.Day.ToString("yyyy-MM-dd") + " " + t.Legs + " " + t.Pnl.ToString("0") + " " + t.Reason);
            }
            foreach (var e in sim.Events.Where(e => e.Kind != "BOUGHT").Take(40)) Console.WriteLine("  ev " + e.Time.ToString("yyyy-MM-dd HH:mm") + " " + e.Kind + " " + e.Text);
            return;
        }
        // the grid
        var cfgs = new List<Cfg>();
        foreach (var dir in new[] { "BUY", "RANDOM", "INDICATOR" })
            foreach (var st in new[] { 1000.0, 500.0 })
                foreach (var tries in new[] { 0, 1 })
                    foreach (var q in new[] { 10, 15, 20 })
                        foreach (var be in new[] { 0.0, 250, 500, 750, 1000 })
                            foreach (var seed in dir == "RANDOM" ? new[] { 7, 11, 23, 41, 59 } : new[] { 7 })
                                cfgs.Add(new Cfg { Dir = dir, Stop = st, Tries = tries, Qty = q, Be = be, Seed = seed });
        var res = new Res[cfgs.Count]; int done = 0;
        Parallel.For(0, cfgs.Count, i => { var c = cfgs[i]; var sim = KeystoneRotationLab.Run(Of(c, inds), test, c.Start, c.End, null); res[i] = Measure(c, sim); int n = System.Threading.Interlocked.Increment(ref done); if (n % 50 == 0) Console.WriteLine("  " + n + " / " + cfgs.Count); });
        // RANDOM: the 5 coin seeds averaged into one row (the spread is reported)
        var rows = new List<Res>(); var spread = new Dictionary<Res, string>();
        foreach (var g in res.GroupBy(r => r.C.Dir + "|" + r.C.Stop + "|" + r.C.Tries + "|" + r.C.Qty + "|" + r.C.Be))
        {
            var l = g.ToList(); if (l.Count == 1) { rows.Add(l[0]); continue; }
            var m = new Res { C = l[0].C, Sim = l[0].Sim, Bought = (int)Math.Round(l.Average(x => x.Bought)), Passed = (int)Math.Round(l.Average(x => x.Passed)), Blown = (int)Math.Round(l.Average(x => x.Blown)), Open = (int)Math.Round(l.Average(x => x.Open)), Trades = (int)Math.Round(l.Average(x => x.Trades)), Wins = (int)Math.Round(l.Average(x => x.Wins)),
                PassRate = l.Average(x => x.PassRate), AvgDays = l.Average(x => x.AvgDays), Cost = l.Average(x => x.Cost), Batches = (int)Math.Round(l.Average(x => x.Batches)), ZeroBatches = (int)Math.Round(l.Average(x => x.ZeroBatches)), BestBatch = l.Max(x => x.BestBatch), BestStreak = l.Max(x => x.BestStreak), ZeroStreak = l.Max(x => x.ZeroStreak), TwoDay = (int)Math.Round(l.Average(x => x.TwoDay)) };
            m.CostPerPass = m.Passed == 0 ? double.NaN : m.Cost / m.Passed; rows.Add(m); spread[m] = l.Min(x => x.PassRate).ToString("0", CultureInfo.InvariantCulture) + "–" + l.Max(x => x.PassRate).ToString("0", CultureInfo.InvariantCulture) + "%";
        }
        // CSV of every row
        var csv = new StringBuilder("sides,stop,tries_per_day,contracts_each,breakeven_at,evals_bought,passed,blown,open_at_end,pass_rate,random_spread,avg_trading_days_to_pass,passed_in_2_days,batches,batches_with_zero_passes,best_batch_passed,longest_streak_batches_with_a_pass,longest_streak_zero_batches,trades,win_rate,eval_cost,cost_per_pass\n");
        foreach (var r in rows) { string sp; spread.TryGetValue(r, out sp); csv.AppendLine(string.Join(",", r.C.Dir, r.C.Stop, r.C.Tries == 1 ? "1" : "until_day_target", r.C.Qty, r.C.Be, r.Bought, r.Passed, r.Blown, r.Open, P(r.PassRate), sp ?? "", P(r.AvgDays), r.TwoDay, r.Batches, r.ZeroBatches, r.BestBatch, r.BestStreak, r.ZeroStreak, r.Trades, P(r.Trades == 0 ? 0 : 100.0 * r.Wins / r.Trades), r.Cost.ToString("0"), double.IsNaN(r.CostPerPass) ? "" : r.CostPerPass.ToString("0"))); }
        File.WriteAllText(Path.Combine(outDir, "grid.csv"), csv.ToString());

        var sb = new StringBuilder(); var ic = CultureInfo.InvariantCulture;
        sb.AppendLine("# EVAL PASS STUDY • " + test.First().Day.ToString("dd MMM yyyy", ic) + " → " + test.Last().Day.ToString("dd MMM yyyy", ic) + " (" + test.Count + " trading days)");
        sb.AppendLine();
        sb.AppendLine("**Set-up:** batches of 50 evaluations (Lucid Flex 50K rules: pass at +$3,000, $2,000 end-of-day drawdown, 50 % consistency = two days of +$1,500, 2 days minimum). Evaluations only — a passed account stops; a new batch of 50 is bought the day after the last one of the batch has passed or blown. The rotation trades MNQ + MGC together on one account at a time from 18:00 to 16:45 (New York), a trade = +$1,500 target (after the $1.24 / micro costs) or its stop, then the next account. Every price is a real 1-minute bar; the stop is checked with both legs at their worst first. The 📡 indicator was learned on 2025 only and never saw 2026.");
        sb.AppendLine();
        sb.AppendLine("Pass % = passed ÷ (passed + failed); failed = blown at the drawdown floor or left too close to it to carry another trade (a quarter of the stop). An account that makes +$1,500 in a day is locked until the next day (the most a day can add). Cost = $" + Base().Rules.EvalCost.ToString("0", ic) + " an evaluation.");
        sb.AppendLine();
        Func<IEnumerable<Res>, string, string> table = (list, title) =>
        {
            var t = new StringBuilder(); t.AppendLine("### " + title); t.AppendLine();
            t.AppendLine("| rule | evals | passed | blown | pass % | days to pass | batches (0 passed) | best batch | cost / pass |"); t.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in list) { string sp; spread.TryGetValue(r, out sp); t.AppendLine("| " + Name(r.C) + " | " + r.Bought + " | " + r.Passed + " | " + r.Blown + (r.Open > 0 ? " (+" + r.Open + " open)" : "") + " | **" + P(r.PassRate) + "**" + (sp != null ? " (" + sp + ")" : "") + " | " + P(r.AvgDays) + " | " + r.Batches + " (" + r.ZeroBatches + ") | " + r.BestBatch + "/50 | " + D(r.CostPerPass) + " |"); }
            t.AppendLine(); return t.ToString();
        };
        Func<Res, bool> noBe = r => r.C.Be == 0;
        // 1. the headline: sides × stop × tries, 10+10, no breakeven
        sb.AppendLine("## 1. The sides: BUY BOTH vs RANDOM vs the 📡 INDICATOR (10 + 10, no breakeven)"); sb.AppendLine();
        sb.Append(table(rows.Where(r => noBe(r) && r.C.Qty == 10).OrderBy(r => r.C.Stop == 1000 ? 0 : 1).ThenBy(r => r.C.Tries).ThenBy(r => r.C.Dir), "every combination"));
        // 2. size
        sb.AppendLine("## 2. The size: 10 / 15 / 20 MNQ + the same MGC (no breakeven)"); sb.AppendLine();
        foreach (var dir in new[] { "BUY", "RANDOM", "INDICATOR" }) sb.Append(table(rows.Where(r => noBe(r) && r.C.Dir == dir).OrderBy(r => r.C.Stop == 1000 ? 0 : 1).ThenBy(r => r.C.Tries).ThenBy(r => r.C.Qty), dir == "BUY" ? "BUY BOTH" : dir == "RANDOM" ? "RANDOM BB/SS (5 coin seeds averaged)" : "📡 INDICATOR"));
        // 3. breakeven
        sb.AppendLine("## 3. Breakeven: does moving the stop to +fees help? (pass % and blown)"); sb.AppendLine();
        sb.AppendLine("| rule | BE off | BE +$250 | BE +$500 | BE +$750 | BE +$1,000 |"); sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var g in rows.GroupBy(r => r.C.Dir + "|" + r.C.Stop + "|" + r.C.Tries + "|" + r.C.Qty).OrderBy(g => g.Key))
        {
            var l = g.OrderBy(r => r.C.Be).ToList(); var c0 = l[0].C;
            sb.AppendLine("| " + Name(new Cfg { Dir = c0.Dir, Stop = c0.Stop, Tries = c0.Tries, Qty = c0.Qty }) + " | " + string.Join(" | ", l.Select(r => P(r.PassRate) + "% • " + r.Passed + "✔ " + r.Blown + "✖")) + " |");
        }
        sb.AppendLine();
        // the winner
        var ranked = rows.Where(r => r.Passed + r.Blown >= 50).OrderByDescending(r => r.PassRate).ThenBy(r => r.CostPerPass).ToList();
        var best = ranked.First(); var bestBb = rows.Where(r => r.C.Dir == "BUY").OrderByDescending(r => r.PassRate).First();
        sb.AppendLine("## 4. The ranking (at least 50 evaluations finished)"); sb.AppendLine();
        sb.Append(table(ranked.Take(15), "the 15 best pass rates"));
        sb.Append(table(ranked.AsEnumerable().Reverse().Take(5), "the 5 worst"));
        // 5. sessions for the best rule + the BUY BOTH baseline
        sb.AppendLine("## 5. Which session to rotate in (the best rule and BUY BOTH, the same days)"); sb.AppendLine();
        var winBest = new List<string>();
        foreach (var pick in new[] { best, bestBb }.Distinct())
        {
            sb.AppendLine("**" + Name(pick.C) + "**"); sb.AppendLine(); sb.AppendLine("| session | evals | passed | blown | pass % | cost / pass |"); sb.AppendLine("|---|---|---|---|---|---|");
            var wins0 = new[] { Tuple.Create("ALL DAY 18:00–16:45", 1800, 1645), Tuple.Create("ASIA 18:00–02:00", 1800, 200), Tuple.Create("ASIA 20:00–00:00", 2000, 2359), Tuple.Create("LONDON 03:00–08:00", 300, 800), Tuple.Create("NY PRE 08:00–09:30", 800, 930), Tuple.Create("NY OPEN 09:30–11:00", 930, 1100), Tuple.Create("NY DAY 09:30–15:55", 930, 1555), Tuple.Create("NY AFTERNOON 13:30–15:55", 1330, 1555) };
            var sres = new Res[wins0.Length];
            Parallel.For(0, wins0.Length, i => { var c = new Cfg { Dir = pick.C.Dir, Stop = pick.C.Stop, Tries = pick.C.Tries, Qty = pick.C.Qty, Be = pick.C.Be, Start = wins0[i].Item2, End = wins0[i].Item3 }; sres[i] = Measure(c, KeystoneRotationLab.Run(Of(c, inds), test, c.Start, c.End, null)); });
            for (int i = 0; i < wins0.Length; i++) sb.AppendLine("| " + wins0[i].Item1 + " | " + sres[i].Bought + " | " + sres[i].Passed + " | " + sres[i].Blown + " | **" + P(sres[i].PassRate) + "** | " + D(sres[i].CostPerPass) + " |");
            sb.AppendLine();
        }
        // 6. the batches of the best rule
        sb.AppendLine("## 6. Every batch of 50 • " + Name(best.C)); sb.AppendLine();
        sb.AppendLine("Batches: " + best.Batches + " • with 0 passed: " + best.ZeroBatches + " • best batch " + best.BestBatch + "/50 • longest run of batches that each passed at least one: " + best.BestStreak + " • longest run of empty batches: " + best.ZeroStreak + " • passed within 2 trading days: " + best.TwoDay + " of " + best.Passed); sb.AppendLine();
        {
            // the accounts that banked +$1,500 on their FIRST trading day: how many passed, how many on day 2, how many failed after
            var sim0 = best.Sim; int good = 0, pass = 0, pass2 = 0, fail = 0;
            foreach (var a in sim0.Accounts)
            {
                var days0 = sim0.Trades.Where(t => t.Account == a.Id).GroupBy(t => t.Day).OrderBy(g => g.Key).ToList(); if (days0.Count == 0) continue;
                if (days0[0].Sum(t => t.Pnl) < 1350) continue; good++;
                if (a.PassedOn != DateTime.MinValue) { pass++; if (days0.Count <= 2) pass2++; } else if (!a.Alive) fail++;
            }
            sb.AppendLine("**After a +$1,500 first day:** " + good + " accounts banked it on their first day → " + pass + " passed (" + P(good == 0 ? 0 : 100.0 * pass / good) + " %), " + pass2 + " of them on day 2 • " + fail + " failed later • the rest still open. (Every account: " + P(best.PassRate) + " %.)"); sb.AppendLine();
            Console.WriteLine("after a good first day: " + good + " → " + pass + " passed (" + pass2 + " on day 2), " + fail + " failed");
        }
        sb.AppendLine("| batch | from → to | days | passed | blown | trades | won | eval P/L |"); sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var b in best.Sim.Batches) sb.AppendLine("| " + b.No + " | " + b.From.ToString("dd MMM", ic) + " → " + b.To.ToString("dd MMM", ic) + " | " + b.Days + " | " + b.Passed + " | " + b.Blown + " | " + b.Trades + " | " + b.Wins + " | " + Sd(b.Pnl) + " |");
        sb.AppendLine();
        sb.AppendLine("BUY BOTH baseline (" + Name(bestBb.C) + "): " + bestBb.Batches + " batches, " + bestBb.ZeroBatches + " with 0 passed, best " + bestBb.BestBatch + "/50, longest empty run " + bestBb.ZeroStreak + "."); sb.AppendLine();
        // 7. the pools: daily / weekly / monthly of the best rule
        Action<KeystonePropSim, string, string> pools = (sim, name, file) =>
        {
            var days = sim.Trades.Select(t => t.Day).Distinct().OrderBy(d => d).ToList(); var dcsv = new StringBuilder("day,trades,wins,pnl,passed,blown\n");
            Func<DateTime, int> passedOn = d => sim.Accounts.Count(a => a.PassedOn == d); Func<DateTime, int> blownOn = d => sim.Accounts.Count(a => a.Phase == "BLOWN" && a.Ended.Date == d);
            foreach (var d in days) { var tt = sim.Trades.Where(t => t.Day == d).ToList(); dcsv.AppendLine(d.ToString("yyyy-MM-dd") + "," + tt.Count + "," + tt.Count(t => t.Pnl > 0) + "," + tt.Sum(t => t.Pnl).ToString("0") + "," + passedOn(d) + "," + blownOn(d)); }
            File.WriteAllText(Path.Combine(outDir, file + "_days.csv"), dcsv.ToString());
            sb.AppendLine("### " + name); sb.AppendLine();
            sb.AppendLine("| month | trades | won % | passed | blown | pass % | eval P/L |"); sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var g in days.GroupBy(d => d.ToString("yyyy-MM")))
            {
                var tt = sim.Trades.Where(t => t.Day.ToString("yyyy-MM") == g.Key).ToList(); int pa = g.Sum(passedOn), bl = g.Sum(blownOn);
                sb.AppendLine("| " + g.Key + " | " + tt.Count + " | " + P(tt.Count == 0 ? 0 : 100.0 * tt.Count(t => t.Pnl > 0) / tt.Count) + " | " + pa + " | " + bl + " | " + P(pa + bl == 0 ? double.NaN : 100.0 * pa / (pa + bl)) + " | " + Sd(tt.Sum(t => t.Pnl)) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("| week (Mon) | trades | won % | passed | blown | eval P/L |"); sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var g in days.GroupBy(d => d.AddDays(-(((int)d.DayOfWeek + 6) % 7))))
            {
                var set = new HashSet<DateTime>(g); var tt = sim.Trades.Where(t => set.Contains(t.Day)).ToList();
                sb.AppendLine("| " + g.Key.ToString("dd MMM", ic) + " | " + tt.Count + " | " + P(tt.Count == 0 ? 0 : 100.0 * tt.Count(t => t.Pnl > 0) / tt.Count) + " | " + g.Sum(passedOn) + " | " + g.Sum(blownOn) + " | " + Sd(tt.Sum(t => t.Pnl)) + " |");
            }
            sb.AppendLine(); sb.AppendLine("Every day: `" + file + "_days.csv`."); sb.AppendLine();
        };
        sb.AppendLine("## 7. The pools (month / week; every day in the CSV)"); sb.AppendLine();
        pools(best.Sim, Name(best.C), "best"); if (bestBb != best) pools(bestBb.Sim, Name(bestBb.C), "buyboth");
        // 8. every day × every full hour × every side: one trade, did it reach +$1,500 before its stop?
        sb.AppendLine("## 8. The best hour and side, day by day (one trade at each full hour, 10 + 10, +$1,500 / −$1,000, out by 16:45)"); sb.AppendLine();
        int[] hours = { 1800, 1900, 2000, 2100, 2200, 2300, 0, 100, 200, 300, 400, 500, 600, 700, 800, 900, 930, 1000, 1100, 1200, 1300, 1400, 1500 };
        string[] sideN = { "BB", "SS", "BS", "SB" }; int[][] sides = KeystoneEntryIndicator.Sides;
        var win = new int[hours.Length, 4]; var cnt = new int[hours.Length, 4]; var hcsv = new StringBuilder("day," + string.Join(",", hours.SelectMany(h => sideN.Select(s => KeystoneBracket.Hm(h) + "_" + s))) + ",best\n");
        int e0 = KeystoneRotDay.Slot(1645); double fees = 10 * KeystoneRotationLab.CostMnq + 10 * KeystoneRotationLab.CostMgc; var bestOfDay = new Dictionary<string, int>(); int noneDays = 0;
        foreach (var d in test)
        {
            var cells = new List<string>(); string bestCell = ""; int bestExit = int.MaxValue;
            for (int hi = 0; hi < hours.Length; hi++)
            {
                int k = KeystoneRotDay.Slot(hours[hi]);
                for (int c = 0; c < 4; c++)
                {
                    int how, xs; double pk, wr; double v = KeystoneRotationLab.Trade(d, k, e0, sides[c][0], sides[c][1], 10, 10, 1500 + fees, 1000, 0, null, out how, out xs, out pk, out wr);
                    if (how == -9) { cells.Add(""); continue; } cnt[hi, c]++; if (how == 1) { win[hi, c]++; if (xs - k < bestExit) { bestExit = xs - k; bestCell = KeystoneBracket.Hm(hours[hi]) + " " + sideN[c]; } }
                    cells.Add(how == 1 ? "W" : how == -1 ? "L" : v.ToString("0", ic));
                }
            }
            if (bestCell == "") noneDays++; else { int n0; bestOfDay.TryGetValue(bestCell, out n0); bestOfDay[bestCell] = n0 + 1; }
            hcsv.AppendLine(d.Day.ToString("yyyy-MM-dd") + "," + string.Join(",", cells) + "," + bestCell);
        }
        File.WriteAllText(Path.Combine(outDir, "hours_days.csv"), hcsv.ToString());
        sb.AppendLine("Win % = the trade reached +$1,500 before −$1,000 (the rest = stopped or out at 16:45). W / L per day and hour in `hours_days.csv`."); sb.AppendLine();
        sb.AppendLine("| entry (NY) | BB | SS | BS | SB | best side |"); sb.AppendLine("|---|---|---|---|---|---|");
        for (int hi = 0; hi < hours.Length; hi++)
        {
            int bc = 0; for (int c = 1; c < 4; c++) if ((double)win[hi, c] / Math.Max(1, cnt[hi, c]) > (double)win[hi, bc] / Math.Max(1, cnt[hi, bc])) bc = c;
            sb.AppendLine("| " + KeystoneBracket.Hm(hours[hi]) + " | " + string.Join(" | ", Enumerable.Range(0, 4).Select(c => P(100.0 * win[hi, c] / Math.Max(1, cnt[hi, c])) + "%")) + " | " + sideN[bc] + " |");
        }
        sb.AppendLine();
        sb.AppendLine("**The fastest winning hour + side of each day** (the one that reached +$1,500 soonest) — how often each was the day's best: " + string.Join(" • ", bestOfDay.OrderByDescending(kv => kv.Value).Take(12).Select(kv => kv.Key + " " + kv.Value + "×")) + " • days with no winning trade at all: " + noneDays + " of " + test.Count + "."); sb.AppendLine();
        // 9. THE LOSERS: would another side have won from the same minute? + a FLIP rule learned on 2025 (avoid the states where BUY BOTH loses)
        sb.AppendLine("## 9. The losing trades: would another direction have won from the same minute?"); sb.AppendLine();
        foreach (var pick in new[] { best, bestBb }.Distinct())
        {
            var s0 = Of(pick.C, inds); double fe = pick.C.Qty * (KeystoneRotationLab.CostMnq + KeystoneRotationLab.CostMgc); var byDay = test.ToDictionary(x => x.Day);
            int losers = 0, rescued = 0; var bySide = new int[4]; var byHour = new SortedDictionary<int, int[]>();
            foreach (var t in pick.Sim.Trades.Where(t => t.Pnl < 0))
            {
                KeystoneRotDay d; if (!byDay.TryGetValue(t.Day, out d)) continue; int k = (int)Math.Round((t.Entry - d.Day).TotalMinutes) - 18 * 60 + 1440; if (k < 0 || k >= KeystoneRotDay.Slots) continue;
                int dM = t.Legs.Contains("SELL") && t.Legs.IndexOf("SELL") < t.Legs.IndexOf("MNQ") ? -1 : 1, dG = t.Legs.Contains("MGC") && t.Legs.Substring(Math.Max(0, t.Legs.IndexOf('+'))).Contains("SELL") ? -1 : 1;
                losers++; bool any = false; int hh = (18 * 60 + k) % 1440 / 60; int[] hv; if (!byHour.TryGetValue(hh, out hv)) byHour[hh] = hv = new int[2]; hv[0]++;
                for (int c = 0; c < 4; c++)
                {
                    if (sides[c][0] == dM && sides[c][1] == dG) continue;
                    int how, xs; double pk, wr; KeystoneRotationLab.Trade(d, k, e0, sides[c][0], sides[c][1], pick.C.Qty, pick.C.Qty, s0.RotTarget + fe, s0.RotStop, 0, null, out how, out xs, out pk, out wr);
                    if (how == 1) { bySide[c]++; any = true; }
                }
                if (any) { rescued++; hv[1]++; }
            }
            sb.AppendLine("**" + Name(pick.C) + "**: " + losers + " losing trades • " + rescued + " (" + P(losers == 0 ? 0 : 100.0 * rescued / losers) + " %) would have WON with another side from the same minute (same +$1,500 / stop) • by side: " + string.Join(" • ", Enumerable.Range(0, 4).Select(c => sideN[c] + " " + bySide[c])) + " • " + (losers - rescued) + " lost whatever the side (a whipsaw minute)."); sb.AppendLine();
            sb.AppendLine("| entry hour (NY) | losers | another side would have won |"); sb.AppendLine("|---|---|---|");
            foreach (var kv in byHour.OrderBy(kv => (kv.Key + 6) % 24)) sb.AppendLine("| " + kv.Key.ToString("00") + ":00 | " + kv.Value[0] + " | " + kv.Value[1] + " (" + P(100.0 * kv.Value[1] / Math.Max(1, kv.Value[0])) + " %) |");
            sb.AppendLine();
        }
        // the FLIP rule: learned on 2025 every 15 minutes — the market state (MNQ / MGC up or down over the last 15 minutes, the session) where BUY BOTH
        // wins less often than another side by 5+ points (40+ samples) → that side instead; everywhere else BUY BOTH
        {
            var cnt2 = new Dictionary<string, int[]>(); int qF = bestBb.C.Qty; double feF = qF * (KeystoneRotationLab.CostMnq + KeystoneRotationLab.CostMgc), tgF = 1500 + feF, stF = bestBb.C.Stop;
            Func<KeystoneRotDay, int, string> keyOf = (d, k) => { if (k < 16) return null; double m = d.CloseM(k - 1) - d.OpenM(k - 15), g = d.CloseG(k - 1) - d.OpenG(k - 15); if (double.IsNaN(m) || double.IsNaN(g)) return null; int mins = (18 * 60 + k) % 1440; return Math.Sign(m) + "," + Math.Sign(g) + "," + KeystoneEntryIndicator.SessionOf(mins / 60 * 100 + mins % 60); };
            foreach (var d in train) for (int k = 16; k < e0 - 30; k += 15)
            {
                var key = keyOf(d, k); if (key == null) continue; int[] v; if (!cnt2.TryGetValue(key, out v)) cnt2[key] = v = new int[5]; bool ok = true; var w4 = new bool[4];
                for (int c = 0; c < 4 && ok; c++) { int how, xs; double pk, wr; KeystoneRotationLab.Trade(d, k, e0, sides[c][0], sides[c][1], qF, qF, tgF, stF, 0, null, out how, out xs, out pk, out wr); if (how == -9) ok = false; w4[c] = how == 1; }
                if (!ok) continue; v[4]++; for (int c = 0; c < 4; c++) if (w4[c]) v[c]++;
            }
            var flip = new Dictionary<string, int>(); var flipRows = new List<string>();
            foreach (var kv in cnt2.OrderBy(k => k.Key)) { var v = kv.Value; if (v[4] < 40) continue; double bb = 100.0 * v[0] / v[4]; int bc = 0; for (int c = 1; c < 4; c++) if (v[c] > v[bc]) bc = c; double bw = 100.0 * v[bc] / v[4]; if (bc != 0 && bw >= bb + 5) { flip[kv.Key] = bc; flipRows.Add("| " + KeystoneEntryIndicator.StateName(kv.Key, 15) + " | " + v[4] + " | " + P(bb) + "% | → " + sideN[bc] + " " + P(bw) + "% |"); } }
            Func<KeystoneRotDay, int, Tuple<int, int>> sidesAt = (d, k) => { var key = keyOf(d, k); int c; if (key != null && flip.TryGetValue(key, out c)) return Tuple.Create(sides[c][0], sides[c][1]); return Tuple.Create(1, 1); };
            var fsim = KeystoneRotationLab.Run(Of(bestBb.C, inds), test, bestBb.C.Start, bestBb.C.End, null, sidesAt); var fr = Measure(new Cfg { Dir = "FLIP", Stop = bestBb.C.Stop, Tries = bestBb.C.Tries, Qty = qF, Be = bestBb.C.Be }, fsim);
            sb.AppendLine("## 10. The FLIP indicator (learned from where BUY BOTH lost in 2025, tested on 2026)"); sb.AppendLine();
            sb.AppendLine("Every 15 minutes of 2025: the state (MNQ and MGC up / down over the last 15 minutes + the session) and which of the 4 sides reached +$1,500 before −$" + stF.ToString("0", ic) + ". Where BUY BOTH won at least 5 points less often than another side (40+ samples) the rule takes that side; everywhere else BUY BOTH. " + flip.Count + " states flipped:"); sb.AppendLine();
            sb.AppendLine("| state | samples 2025 | BUY BOTH won | flip to |"); sb.AppendLine("|---|---|---|---|"); foreach (var l in flipRows) sb.AppendLine(l); sb.AppendLine();
            sb.AppendLine("| 2026 (never seen) | evals | passed | failed | pass % | cost / pass |"); sb.AppendLine("|---|---|---|---|---|---|");
            sb.AppendLine("| BUY BOTH (" + Name(bestBb.C) + ") | " + bestBb.Bought + " | " + bestBb.Passed + " | " + bestBb.Blown + " | **" + P(bestBb.PassRate) + "** | " + D(bestBb.CostPerPass) + " |");
            sb.AppendLine("| FLIP rule, same settings | " + fr.Bought + " | " + fr.Passed + " | " + fr.Blown + " | **" + P(fr.PassRate) + "** | " + D(fr.CostPerPass) + " |"); sb.AppendLine();
            Console.WriteLine("FLIP: " + P(fr.PassRate) + "% vs BUY BOTH " + P(bestBb.PassRate) + "%");
        }
        // 11. how many rotations (trades) a day happen from the 18:00 open until 08:00 (before the New York pre-market), and in each stretch of the day
        sb.AppendLine("## 11. Rotations a day: 18:00 open → 08:00 (before the NY pre-market) and the rest of the day"); sb.AppendLine();
        sb.AppendLine("| rule | days | rotations a day 18:00–08:00 | 08:00–09:30 | 09:30–16:45 | all day | days with 0 before 08:00 | won 18:00–08:00 |"); sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var pick in rows.Where(r => r.C.Be == 0 && r.C.Qty == 10 && r.C.Stop == 1000).OrderBy(r => r.C.Tries).ThenBy(r => r.C.Dir).Concat(new[] { best }).Distinct())
        {
            var dd2 = pick.Sim.Trades.Select(t => t.Day).Distinct().ToList(); int nd = Math.Max(1, test.Count);
            Func<KeystoneSimTrade, int> mm = t => { int m = (t.Entry.Hour * 60 + t.Entry.Minute - 18 * 60 + 1440) % 1440; return m; };   // minutes after 18:00
            var night = pick.Sim.Trades.Where(t => mm(t) < 14 * 60).ToList(); var pre = pick.Sim.Trades.Where(t => mm(t) >= 14 * 60 && mm(t) < 15 * 60 + 30).ToList(); var ny = pick.Sim.Trades.Where(t => mm(t) >= 15 * 60 + 30).ToList();
            int zero = test.Count(d => !night.Any(t => t.Day == d.Day));
            sb.AppendLine("| " + Name(pick.C) + " | " + test.Count + " | **" + ((double)night.Count / nd).ToString("0.0", ic) + "** | " + ((double)pre.Count / nd).ToString("0.0", ic) + " | " + ((double)ny.Count / nd).ToString("0.0", ic) + " | " + ((double)pick.Sim.Trades.Count / nd).ToString("0.0", ic) + " | " + zero + " | " + P(night.Count == 0 ? 0 : 100.0 * night.Count(t => t.Pnl > 0) / night.Count) + "% |");
        }
        sb.AppendLine(); sb.AppendLine("(a rotation = one trade on the account in turn: +$1,500 or its stop, then the next account after the pause; the days a new batch is bought count too)"); sb.AppendLine();
        // 12. the drawdown: END OF DAY vs INTRADAY TRAILING (the floor follows the best open P/L, live) vs STATIC — same days, same math
        sb.AppendLine("## 12. The drawdown: end of day (EOD) vs intraday trailing vs static — the same days and trades"); sb.AppendLine();
        sb.AppendLine("EOD = the floor moves up only with the best CLOSING balance; INTRADAY = it follows the best open profit while a trade runs (a +$1,200 trade that comes back to −$1,000 has lifted the floor $1,200); STATIC = the floor never moves ($2,000 under the start). All three lock at the start balance."); sb.AppendLine();
        sb.AppendLine("| rule | EOD pass % (passed / failed) | INTRADAY pass % | STATIC pass % | INTRADAY vs EOD |"); sb.AppendLine("|---|---|---|---|---|");
        var ddCfgs = new List<Cfg>(); foreach (var dir in new[] { "BUY", "RANDOM", "INDICATOR" }) foreach (var tries in new[] { 1, 0 }) foreach (var q in dir == "BUY" ? new[] { 10, 15, 20 } : new[] { 10 }) ddCfgs.Add(new Cfg { Dir = dir, Stop = 1000, Tries = tries, Qty = q });
        var ddRes = new Res[ddCfgs.Count, 3]; string[] ddTypes = { "EOD", "INTRADAY", "STATIC" };
        Parallel.For(0, ddCfgs.Count * 3, i => { var c0 = ddCfgs[i / 3]; var c = new Cfg { Dir = c0.Dir, Stop = c0.Stop, Tries = c0.Tries, Qty = c0.Qty, Dd = ddTypes[i % 3] }; ddRes[i / 3, i % 3] = Measure(c, KeystoneRotationLab.Run(Of(c, inds), test, c.Start, c.End, null)); });
        for (int i = 0; i < ddCfgs.Count; i++)
        {
            Func<Res, string> cell = r => P(r.PassRate) + "% (" + r.Passed + " / " + r.Blown + ")";
            double diff = ddRes[i, 1].PassRate - ddRes[i, 0].PassRate;
            sb.AppendLine("| " + Name(ddCfgs[i]) + " | " + cell(ddRes[i, 0]) + " | " + cell(ddRes[i, 1]) + " | " + cell(ddRes[i, 2]) + " | **" + (diff >= 0 ? "+" : "−") + Math.Abs(diff).ToString("0.0", ic) + " pts** |");
        }
        sb.AppendLine(); sb.AppendLine("(RANDOM = one coin seed here; the grid above averages 5)"); sb.AppendLine();
        sb.AppendLine("## The indicators used (trained on 2025)"); sb.AppendLine(); foreach (var kv in indDesc.OrderBy(k => k.Key)) sb.AppendLine("- " + kv.Key.Replace("|", " each • stop $") + ": " + kv.Value); sb.AppendLine();
        File.WriteAllText(Path.Combine(outDir, "report.md"), sb.ToString());
        Console.WriteLine("BEST: " + Name(best.C) + " pass " + P(best.PassRate) + "% (" + best.Passed + " / " + (best.Passed + best.Blown) + ")");
        Console.WriteLine("BUY BOTH best: " + Name(bestBb.C) + " pass " + P(bestBb.PassRate) + "%");
    }
}
