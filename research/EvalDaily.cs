// EVAL DAILY 2026: 50 evaluations (50K: $3,000 target, $2,000 end-of-day trailing drawdown, 50% consistency, min trading days)
// rotated over BH setups with ONE trade per account per day: risk $300 → target $900 (a win = the account's +$900 day, done).
// Each day's setups (in time order) go to the next account that has not traded today. When an account reaches the target
// before its minimum days it only places a tiny open/close trade on the next days (≈ fees) until the days count — then PASSES.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class EvalDaily
{
    sealed class Acct { public double Bal, PeakEod; public int Days, Trades; public bool Passed, Blown, TradedToday, AtTarget; public DateTime Start, End; public double BestDay, Today; }

    public static void Run(Dictionary<string, List<RDay>> bySym, DateTime from, DateTime to, string outDir)
    {
        double target = 3000, dd = 2000, fillerCost = 3; int maxMicros = 40;
        var sb = new StringBuilder();
        sb.AppendLine("# EVAL DAILY " + from.ToString("yyyy-MM-dd") + " → " + to.ToString("yyyy-MM-dd") + " • 50 evaluations • ONE BH trade a day per account (risk $300 → $900, and $500 → $1,500)");
        sb.AppendLine();
        sb.AppendLine("Evaluation: 50K • target +$3,000 • end-of-day trailing drawdown $2,000 (follows the best close, stops at the start + $100) • best day ≤ 50% of the profit (automatic here: a day is at most +3R ≤ $1,500) • minimum 3 trading days (extra days = a tiny open/close trade ≈ $" + fillerCost + "; 2 or 5 days gave the same passes).");
        sb.AppendLine("Trade: BH (5-minute red → green → break of the green high, stop under the pattern, target 3R, out by 15:55). Micros = risk ÷ (stop × point value), max 40; too wide for 1 micro = skipped. Costs in.");
        sb.AppendLine("FIXED 50 = your $5K: exactly 50 evaluations bought, no replacement. Each day the setups go to accounts that have not traded yet today (rotation).");
        sb.AppendLine();
        var sources = new List<Tuple<string, string[], int, int>> {
            Tuple.Create("MNQ • NY 09:30–15:00", new[] { "MNQ" }, R.S(9, 30), R.S(15, 0)),
            Tuple.Create("MNQ • NY OPEN 09:30–11:30", new[] { "MNQ" }, R.S(9, 30), R.S(11, 30)),
            Tuple.Create("MNQ • ALL 18:00–15:00", new[] { "MNQ" }, R.S(18, 0), R.S(15, 0)),
            Tuple.Create("MGC • ALL 18:00–15:00", new[] { "MGC" }, R.S(18, 0), R.S(15, 0)),
            Tuple.Create("MNQ + MGC • NY 09:30–15:00", new[] { "MNQ", "MGC" }, R.S(9, 30), R.S(15, 0)),
            Tuple.Create("MNQ + MGC • ALL 18:00–15:00", new[] { "MNQ", "MGC" }, R.S(18, 0), R.S(15, 0)) };
        int minDays = 3;   // 2 / 3 / 5 gave the same passes: the target takes longer than the minimum anyway
        foreach (double risk in new[] { 300.0, 500.0 })
        {
            sb.AppendLine("## Risk $" + risk.ToString("0") + " → target $" + (3 * risk).ToString("0") + " a day (one trade per account per day) • minimum 3 trading days");
            sb.AppendLine("| setups from | setups | win % | PASSED | blown | still running | pass % (of finished) | median days to pass | first pass | passes by month (cumulative) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var src in sources)
            {
                var trades = new List<RTrade>(); var cfg = new BhCfg { Start = src.Item3, LastEntry = src.Item4, MaxTrades = 20, Stop = "LOW", TargetR = 3 };
                foreach (var s0 in src.Item2) foreach (var d in bySym[s0].Where(d => d.Day >= from && d.Day <= to)) trades.AddRange(Bh.Day(d, cfg));
                trades = trades.OrderBy(t => t.Day).ThenBy(t => t.InSlot).ToList();
                var res = new List<Tuple<RTrade, double, double>>();
                foreach (var t in trades)
                {
                    var sp = Spec.Of(t.Sym); double stopD = Math.Abs(t.Entry - t.Stop); if (stopD <= 0) continue;
                    int q = Math.Min(maxMicros, (int)Math.Floor(risk / (stopD * sp.PointValue))); if (q < 1) continue;
                    double pnl = q * ((t.Exit - t.Entry) * t.Dir * sp.PointValue - sp.CommissionRt), worst = -q * (t.MaePts * sp.PointValue + sp.CommissionRt);
                    res.Add(Tuple.Create(t, pnl, Math.Min(pnl, worst)));
                }
                var accts = Enumerable.Range(0, 50).Select(i => new Acct { Start = from }).ToList(); int next = 0;
                foreach (var dayGroup in res.GroupBy(r => r.Item1.Day).OrderBy(g => g.Key))
                {
                    var day = dayGroup.Key;
                    foreach (var a in accts) { a.TradedToday = false; a.Today = 0; }
                    foreach (var r in dayGroup)
                    {
                        // the next account still trading that has not traded today
                        int tries = 0; while (tries < 50 && (accts[next].Passed || accts[next].Blown || accts[next].TradedToday || accts[next].AtTarget)) { next = (next + 1) % 50; tries++; }
                        if (tries >= 50) break;
                        var a = accts[next]; a.TradedToday = true; a.Trades++;
                        double floor = Math.Min(a.PeakEod - dd, 100);
                        if (a.Bal + r.Item3 <= floor) { a.Blown = true; a.End = day; a.Bal += Math.Max(r.Item3, floor - a.Bal); }
                        else { a.Bal += r.Item2; a.Today += r.Item2; if (a.Bal <= floor) { a.Blown = true; a.End = day; } }
                        next = (next + 1) % 50;
                    }
                    // end of day: days counted, trailing floor moves, target / minimum days, filler days
                    foreach (var a in accts)
                    {
                        if (a.Passed || a.Blown) continue;
                        if (a.AtTarget) { a.Days++; a.Bal -= fillerCost; }   // the tiny open / close trade that makes the day count
                        else if (a.TradedToday) { a.Days++; a.BestDay = Math.Max(a.BestDay, a.Today); }
                        a.PeakEod = Math.Max(a.PeakEod, a.Bal);
                        if (a.Bal >= target) { a.AtTarget = true; if (a.Days >= minDays && a.BestDay <= 0.5 * a.Bal) { a.Passed = true; a.End = day; } }
                    }
                }
                int passed = accts.Count(a => a.Passed), blown = accts.Count(a => a.Blown), open = 50 - passed - blown;
                var passDays = accts.Where(a => a.Passed).Select(a => (double)a.Days).OrderBy(x => x).ToList();
                var firstPass = accts.Where(a => a.Passed).Select(a => a.End).DefaultIfEmpty(DateTime.MinValue).Min();
                var byMonth = new List<string>(); int cum = 0;
                for (int mo = from.Month; mo <= Math.Min(12, to.Month); mo++) { cum += accts.Count(a => a.Passed && a.End.Month == mo && a.End.Year == from.Year); byMonth.Add(new DateTime(2000, mo, 1).ToString("MMM", CultureInfo.InvariantCulture) + " " + cum); }
                int wins = res.Count(r => r.Item2 > 0);
                sb.AppendLine("| " + src.Item1 + " | " + res.Count + " | " + (100.0 * wins / Math.Max(1, res.Count)).ToString("0") + "% | **" + passed + "** | " + blown + " | " + open + " | " + (100.0 * passed / Math.Max(1, passed + blown)).ToString("0") + "% | " + (passDays.Count > 0 ? passDays[passDays.Count / 2].ToString("0") : "–") + " | " + (firstPass == DateTime.MinValue ? "–" : firstPass.ToString("MMM d", CultureInfo.InvariantCulture)) + " | " + string.Join(" • ", byMonth) + " |");
            }
            sb.AppendLine();
        }
        sb.AppendLine("Reading it: at $300 → $900 a pass needs about four winning days net of the losing ones (3,000 ÷ 900 = 3.3); at $500 → $1,500 two winning days, but a loss costs $500 (four in a row and the $2,000 drawdown is gone). 'Still running' = bought, neither passed nor blown by the last day in the data.");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "eval_daily_2026.md"), sb.ToString());
        Console.Write(sb.ToString());
    }
}
