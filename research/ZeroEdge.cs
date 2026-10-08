// ZERO-EDGE PROP MATH: no trading edge at all — every trade wins +T R with probability 1/(1+T) (a fair bet), minus costs —
// pushed through each firm's full lifecycle (evaluation → funded → payouts). If the net per evaluation is positive with NO edge,
// the firm's rules themselves are beatable with the right target, size and trades per day. Monte Carlo, many evaluations each.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class ZeroEdge
{
    sealed class Row { public string Item1; public double Item2, Item3, Item4; public int Item5; public double Item6, Item7, Item8, Cash; }
    public static void Run(string outDir)
    {
        var firms = new List<Tuple<PRules, double>>();
        { var r = PropMain.Flex(50); r.Name = "LUCID FLEX 50K ($120)"; r.Cost = 120; firms.Add(Tuple.Create(r, 0.0)); }
        { var r = PropMain.Flex(50); r.Name = "TRADEIFY SELECT 50K ($99, 40%, Flex cap $2,500)"; r.Cost = 99; r.EvalConsistency = 40; r.MinEvalDays = 3; r.PayCap = 2500; r.Split = 90; firms.Add(Tuple.Create(r, 0.0)); }
        { var r = PropMain.Flex(50); r.Name = "TAKE PROFIT TRADER 50K ($170 + $130 PRO)"; r.Cost = 170; r.EvalConsistency = 50; r.MinEvalDays = 5; r.PayMode = "DAILY"; r.FundIntraday = true; r.LockAt = 0; r.PayCap = 0; r.PayMin = 250; r.Split = 80; r.MaxPayouts = 0; firms.Add(Tuple.Create(r, 130.0)); }
        double[] targets = { 0.5, 1, 1.5, 2, 3, 4, 5 }, risks = { 250, 400, 600, 800, 1000, 1500, 2000 }, fundShares = { 1, 0.5, 0.25 };
        double costR = 0.03;   // commission + slippage ≈ 3% of the risk on a typical stop
        int n = 20000; var rows = new List<Row>(); var lk = new object();
        Parallel.ForEach(firms, fm =>
        {
            foreach (double T in targets) foreach (double risk in risks) foreach (double fs in fundShares) foreach (int perDay in new[] { 1, 2 })
            {
                var rng = new Random(12345 + (int)(T * 100) + (int)risk + perDay); double net = 0, pass = 0, pays = 0, cash = 0; double p = 1.0 / (1 + T);
                for (int i = 0; i < n; i++)
                {
                    var a = new PAcct(fm.Item1, 1, fs); int day = 0;
                    while (!a.Dead && day < 400)
                    {
                        double pnl = 0, worst = 0;
                        for (int k = 0; k < perDay; k++) { bool win = rng.NextDouble() < p; double r = (win ? T : -1) - costR; worst = Math.Min(worst, pnl + Math.Min(0, -risk * (win ? 0.5 : 1))); pnl += r * risk; if (win) break; }
                        a.Step(new PDay { Day = DateTime.MinValue.AddDays(day), Pnl = pnl, Worst = worst, Best = Math.Max(0, pnl), Traded = true }); day++;
                    }
                    if (a.Passed) pass++; pays += a.Payouts; cash += a.Cash; net += a.Cash - fm.Item1.Cost - (a.Passed ? fm.Item2 : 0);
                }
                lock (lk) rows.Add(new Row { Item1 = fm.Item1.Name, Item2 = T, Item3 = risk, Item4 = fs, Item5 = perDay, Item6 = net / n, Item7 = 100 * pass / n, Item8 = pays / n, Cash = cash / n });
            }
        });
        // ---- EVAL ONE WAY, FUNDED ANOTHER: big targets to pass, then small high-win-rate targets to collect payouts
        var rows2 = new List<Row>(); var evalSets = new[] { Tuple.Create(3.0, 400.0), Tuple.Create(5.0, 250.0), Tuple.Create(1.0, 1000.0), Tuple.Create(2.0, 750.0) };
        double[] fT = { 0.25, 0.5, 1, 2 }, fR = { 150, 250, 400, 600, 800 };
        Parallel.ForEach(firms, fm =>
        {
            foreach (var es in evalSets) foreach (double ft in fT) foreach (double fr in fR)
            {
                var rng = new Random(777 + (int)(ft * 100) + (int)fr + (int)es.Item2); double net = 0, pass = 0, pays = 0, cash = 0;
                for (int i = 0; i < n; i++)
                {
                    var a = new PAcct(fm.Item1, 1, 1); int day = 0;
                    while (!a.Dead && day < 400)
                    {
                        double T = a.Funded ? ft : es.Item1, risk = a.Funded ? fr : es.Item2, p = 1.0 / (1 + T);
                        bool win = rng.NextDouble() < p; double pnl = ((win ? T : -1) - costR) * risk, worst = Math.Min(0, -risk * (win ? 0.5 : 1));
                        a.Step(new PDay { Day = DateTime.MinValue.AddDays(day), Pnl = pnl, Worst = worst, Best = Math.Max(0, pnl), Traded = true }); day++;
                    }
                    if (a.Passed) pass++; pays += a.Payouts; cash += a.Cash; net += a.Cash - fm.Item1.Cost - (a.Passed ? fm.Item2 : 0);
                }
                lock (lk) rows2.Add(new Row { Item1 = fm.Item1.Name, Item2 = es.Item1, Item3 = es.Item2, Item4 = ft, Item5 = (int)fr, Item6 = net / n, Item7 = 100 * pass / n, Item8 = pays / n, Cash = cash / n });
            }
        });
        var sb = new StringBuilder();
        sb.AppendLine("# ZERO-EDGE PROP MATH • can the firm's rules alone pay more than the evaluations cost?");
        sb.AppendLine();
        sb.AppendLine("Every trade is a fair coin for its target: wins +T R with probability 1 ÷ (1 + T), else −1 R, minus 3% of R in costs — NO edge. One trade a day (or a second after a losing first). " + n.ToString("N0") + " simulated evaluations per row, each run through evaluation → funded → payouts with the firm's rules (" + string.Join(" • ", firms.Select(f => f.Item1.Name)) + ").");
        sb.AppendLine();
        foreach (var fm in firms)
        {
            sb.AppendLine("## " + fm.Item1.Name + " — the 12 best settings");
            sb.AppendLine("| target | risk / trade (eval) | funded size | trades / day | pass % | payouts per eval | cash per eval | **net per eval** |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var r in rows.Where(x => x.Item1 == fm.Item1.Name).OrderByDescending(x => x.Item6).Take(12))
                sb.AppendLine("| " + r.Item2 + "R | $" + r.Item3.ToString("0") + " | " + (r.Item4 == 1 ? "same" : r.Item4 == 0.5 ? "half" : "quarter") + " | " + (r.Item5 == 1 ? "1" : "≤2") + " | " + r.Item7.ToString("0.0") + "% | " + r.Item8.ToString("0.00") + " | $" + r.Cash.ToString("0") + " | **" + (r.Item6 >= 0 ? "+$" : "−$") + Math.Abs(r.Item6).ToString("0") + "** |");
            var worst = rows.Where(x => x.Item1 == fm.Item1.Name).OrderBy(x => x.Item6).First();
            sb.AppendLine("Worst setting: " + worst.Item2 + "R at $" + worst.Item3 + " → " + (worst.Item6 >= 0 ? "+$" : "−$") + Math.Abs(worst.Item6).ToString("0") + " per eval. Settings with a positive net: " + rows.Count(x => x.Item1 == fm.Item1.Name && x.Item6 > 0) + " of " + rows.Count(x => x.Item1 == fm.Item1.Name) + ".");
            sb.AppendLine();
        }
        sb.AppendLine("# EVAL ONE WAY, FUNDED ANOTHER (still zero edge)");
        sb.AppendLine("Pass with a big target, then trade the funded account with a small target (high win rate) and its own size, to collect the payout days. One trade a day.");
        sb.AppendLine();
        foreach (var fm in firms)
        {
            sb.AppendLine("## " + fm.Item1.Name + " — the 10 best");
            sb.AppendLine("| EVAL: target • risk | FUNDED: target • risk | pass % | payouts per eval | cash per eval | **net per eval** |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in rows2.Where(x => x.Item1 == fm.Item1.Name).OrderByDescending(x => x.Item6).Take(10))
                sb.AppendLine("| " + r.Item2 + "R • $" + r.Item3.ToString("0") + " | " + r.Item4 + "R • $" + r.Item5 + " | " + r.Item7.ToString("0.0") + "% | " + r.Item8.ToString("0.00") + " | $" + r.Cash.ToString("0") + " | **" + (r.Item6 >= 0 ? "+$" : "−$") + Math.Abs(r.Item6).ToString("0") + "** |");
            sb.AppendLine();
        }
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "zero_edge.md"), sb.ToString());
        Console.Write(sb.ToString());
    }
}
