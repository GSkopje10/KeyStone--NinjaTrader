// EVAL ROTATION 2026: BH setups (break of the green reference candle's high after a red one) rotated across 50 evaluation
// accounts. Every trade risks a fixed $500 (micros = $500 ÷ stop distance, capped at the firm's max) for a 3R = $1,500 target.
// An account PASSES at +target, BLOWS at its end-of-day trailing drawdown (checked with the trade's worst point), and is
// replaced by a new evaluation (FIXED-50: no replacement — what happens to exactly 50 bought evaluations).
// Modes: ONE A DAY (the day's first setup only) vs EVERY SETUP (each setup → the next account) × session window × instrument.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class EvalRotation
{
    sealed class Acct { public double Bal, PeakEod, LastEodDay; public int Trades; public bool Done; public string End = ""; public DateTime Bought; }
    sealed class Model { public string Name = ""; public double Target, Dd, Cost = 100; public int MaxMicros; }

    public static void Run(Dictionary<string, List<RDay>> bySym, DateTime from, DateTime to, string outDir)
    {
        var models = new[] {
            new Model { Name = "25K-style (pass +$1,500 • blow −$1,000 EOD)", Target = 1500, Dd = 1000, MaxMicros = 20 },
            new Model { Name = "50K-style (pass +$1,500 • blow −$2,000 EOD)", Target = 1500, Dd = 2000, MaxMicros = 40 } };
        var sessions = new[] {
            Tuple.Create("NY 09:30–15:00", R.S(9, 30), R.S(15, 0)), Tuple.Create("NY OPEN 09:30–11:30", R.S(9, 30), R.S(11, 30)),
            Tuple.Create("LONDON 02:00–09:30", R.S(2, 0), R.S(9, 29)), Tuple.Create("ASIA 18:00–02:00", R.S(18, 0), R.S(1, 59)), Tuple.Create("ALL 18:00–15:00", R.S(18, 0), R.S(15, 0)) };
        var sb = new StringBuilder();
        sb.AppendLine("# EVAL ROTATION " + from.ToString("yyyy-MM-dd") + " → " + to.ToString("yyyy-MM-dd") + " • 50 evaluations • BH setups • risk $500 → target $1,500 a trade");
        sb.AppendLine();
        sb.AppendLine("Every BH trade: entry one tick over the green reference candle's high (5-minute candles), stop one tick under the red + green low, target 3R; out by 15:55 if neither. Micros = $500 ÷ (stop × point value), capped (25K-style 20, 50K-style 40); a stop too wide for 1 micro = skipped. Costs: $1.24 a micro round trip.");
        sb.AppendLine("ROTATE 50: 50 evaluations running, each setup goes to the next one; a passed / blown one is replaced at once. FOCUS: one evaluation trades every setup it gets until it passes or blows, then the next one starts. FIXED 50: exactly 50 bought, no replacement (open = never finished by now). Pass % = passed ÷ (passed + blown); passes per 50 evals = pass % × 50 (a $5K budget at $100 an eval).");
        sb.AppendLine();
        var csv = new StringBuilder("symbol,model,mode,session,trades,win_pct,evals_bought,passed,blown,open_at_end,pass_rate,cost,longest_loss_streak,longest_win_streak,fixed50_passed,fixed50_blown,fixed50_open,focus_passed,focus_blown,focus_pass_pct\n");
        foreach (var sym in bySym.Keys)
        {
            var days = bySym[sym].Where(d => d.Day >= from && d.Day <= to).ToList(); var sp = Spec.Of(sym);
            sb.AppendLine("## " + sym + " • " + days.Count + " sessions");
            foreach (var m in models)
            {
                sb.AppendLine();
                sb.AppendLine("### " + sym + " • " + m.Name);
                sb.AppendLine("| setups | session | trades | win % | losing streak | ROTATE 50: passed / blown (pass %) | FOCUS 1 at a time: passed / blown (pass %) | FIXED 50 bought: passed / blown / open | passes per 50 evals ($5K) rotate • focus |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
                foreach (bool oneADay in new[] { true, false })
                foreach (var ses in sessions)
                {
                    var cfg = new BhCfg { Start = ses.Item2, LastEntry = ses.Item3, MaxTrades = oneADay ? 1 : 20, Stop = "LOW", TargetR = 3 };
                    var trades = new List<RTrade>(); foreach (var d in days) trades.AddRange(Bh.Day(d, cfg));
                    // $ result of each trade at $500 risk
                    var res = new List<Tuple<RTrade, double, double>>();   // trade, pnl, worst
                    foreach (var t in trades)
                    {
                        double stopD = Math.Abs(t.Entry - t.Stop); if (stopD <= 0) continue;
                        int q = (int)Math.Floor(500.0 / (stopD * sp.PointValue)); q = Math.Min(q, m.MaxMicros); if (q < 1) continue;
                        double pnl = q * ((t.Exit - t.Entry) * t.Dir * sp.PointValue - sp.CommissionRt), worst = -q * (t.MaePts * sp.PointValue + sp.CommissionRt);
                        res.Add(Tuple.Create(t, pnl, Math.Min(pnl, worst)));
                    }
                    int wins = res.Count(r => r.Item2 > 0), ls = 0, ws = 0, curL = 0, curW = 0;
                    foreach (var r in res) { if (r.Item2 > 0) { curW++; curL = 0; } else { curL++; curW = 0; } ls = Math.Max(ls, curL); ws = Math.Max(ws, curW); }
                    var rot = Simulate(res, m, true, 50); var fix = Simulate(res, m, false, 50); var foc = Simulate(res, m, true, 1);
                    Func<Tuple<int, int, int, int, Dictionary<int, Tuple<int, int>>>, double> pr = x => 100.0 * x.Item2 / Math.Max(1, x.Item2 + x.Item3);
                    sb.AppendLine("| " + (oneADay ? "ONE A DAY" : "EVERY SETUP") + " | " + ses.Item1 + " | " + res.Count + " | " + (100.0 * wins / Math.Max(1, res.Count)).ToString("0") + "% | " + ls + " | " + rot.Item2 + " / " + rot.Item3 + " (" + pr(rot).ToString("0") + "%) | " + foc.Item2 + " / " + foc.Item3 + " (" + pr(foc).ToString("0") + "%) | " + fix.Item2 + " / " + fix.Item3 + " / " + fix.Item4 + " | **" + (pr(rot) / 2).ToString("0") + "** • **" + (pr(foc) / 2).ToString("0") + "** |");
                    csv.AppendLine(string.Join(",", sym, m.Name.Split('(')[0].Trim(), oneADay ? "ONE A DAY" : "EVERY SETUP", ses.Item1, res.Count, (100.0 * wins / Math.Max(1, res.Count)).ToString("0.0", CultureInfo.InvariantCulture), rot.Item1, rot.Item2, rot.Item3, rot.Item4, pr(rot).ToString("0.0", CultureInfo.InvariantCulture), rot.Item1 * m.Cost, ls, ws, fix.Item2, fix.Item3, fix.Item4, foc.Item2, foc.Item3, pr(foc).ToString("0.0", CultureInfo.InvariantCulture)));
                    if (oneADay == false && ses.Item1.StartsWith("NY 09:30–15:00"))
                    {
                        // month by month for the main rotation
                        sb.AppendLine("|  | ↳ ROTATE 50 by month: " + string.Join(" • ", rot.Item5.OrderBy(kv => kv.Key).Select(kv => new DateTime(2000, kv.Key, 1).ToString("MMM", CultureInfo.InvariantCulture) + " " + kv.Value.Item1 + " passed " + kv.Value.Item2 + " blown")) + " | | | | | | | |");
                    }
                }
            }
            sb.AppendLine();
        }
        sb.AppendLine("Reading it: a pass needs ONE 3R win (+$1,500) before the account's losses reach its drawdown (25K-style: two losses; 50K-style: four). Pass % = passed ÷ (passed + blown). Each pass is an evaluation passed — not money yet (funded rules, activation fees and payouts come after).");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "eval_rotation_2026.md"), sb.ToString());
        File.WriteAllText(Path.Combine(outDir, "eval_rotation_2026.csv"), csv.ToString());
        Console.Write(sb.ToString());
    }

    // → bought, passed, blown, open, month → (passed, blown)
    static Tuple<int, int, int, int, Dictionary<int, Tuple<int, int>>> Simulate(List<Tuple<RTrade, double, double>> res, Model m, bool replace, int size)
    {
        var pool = new List<Acct>(); int bought = 0, passed = 0, blown = 0; var months = new Dictionary<int, Tuple<int, int>>();
        Func<Acct> buy = () => { bought++; return new Acct { Bal = 0, PeakEod = 0, LastEodDay = -1 }; };
        for (int i = 0; i < size; i++) pool.Add(buy());
        int next = 0;
        foreach (var r in res)
        {
            // the next account still running (FIXED-50: stop when all are done)
            int tries = 0; while (pool[next].Done && tries < pool.Count) { next = (next + 1) % pool.Count; tries++; }
            if (pool[next].Done) break;
            var a = pool[next]; double day = r.Item1.Day.ToOADate();
            if (a.LastEodDay >= 0 && day > a.LastEodDay) a.PeakEod = Math.Max(a.PeakEod, a.Bal);   // the trailing floor moves at the close
            a.LastEodDay = day;
            double floor = Math.Min(Math.Max(0, a.PeakEod) - m.Dd, 0);   // end-of-day trailing: follows the best close, stops at the starting balance
            a.Trades++;
            Tuple<int, int> mo; months.TryGetValue(r.Item1.Day.Month, out mo); if (mo == null) mo = Tuple.Create(0, 0);
            if (a.Bal + r.Item3 <= floor) { a.Done = true; a.End = "BLOWN"; blown++; months[r.Item1.Day.Month] = Tuple.Create(mo.Item1, mo.Item2 + 1); }
            else
            {
                a.Bal += r.Item2;
                if (a.Bal >= m.Target) { a.Done = true; a.End = "PASSED"; passed++; months[r.Item1.Day.Month] = Tuple.Create(mo.Item1 + 1, mo.Item2); }
                else if (a.Bal <= floor) { a.Done = true; a.End = "BLOWN"; blown++; months[r.Item1.Day.Month] = Tuple.Create(mo.Item1, mo.Item2 + 1); }
            }
            if (a.Done && replace) pool[next] = buy();
            next = (next + 1) % pool.Count;
        }
        int open = pool.Count(x => !x.Done);
        return Tuple.Create(bought, passed, blown, open, months);
    }
}
