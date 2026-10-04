using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class PropMain
{
    public static List<PDay> DaysOf(List<RDay> cal, List<RTrade> trades, double shift = 0)
    {
        var by = trades.ToDictionary(t => t.Day, t => t);
        return cal.Select(d =>
        {
            RTrade t; if (!by.TryGetValue(d.Day, out t)) return new PDay { Day = d.Day };
            var sp = Spec.Of(t.Sym); double worst = Math.Min(t.Usd, -t.MaePts * sp.PointValue - sp.CommissionRt);
            return new PDay { Day = d.Day, Pnl = t.Usd - shift, Worst = worst - shift, Best = Math.Max(t.Usd, t.MfePts * sp.PointValue - sp.CommissionRt) - shift, Traded = true };
        }).ToList();
    }

    // Lucid plans (help centre, 2026-10). Cost = an assumed price; results are shown as cash per evaluation, so any price can be subtracted.
    public static PRules Flex(int k, double payWhen = 0)
    {
        var r = new PRules { Name = k + "K FLEX" + (payWhen > 0 ? " (payout from $" + payWhen + ")" : ""), PayWhenBal = payWhen };
        if (k == 25) { r.Cost = 100; r.Target = 1250; r.Dd = 1000; r.PayDayMin = 100; r.PayCap = 1000; }
        if (k == 50) { r.Cost = 150; r.Target = 3000; r.Dd = 2000; r.PayDayMin = 150; r.PayCap = 2000; }
        if (k == 100) { r.Cost = 250; r.Target = 6000; r.Dd = 3000; r.PayDayMin = 200; r.PayCap = 2500; }
        if (k == 150) { r.Cost = 350; r.Target = 9000; r.Dd = 4500; r.PayDayMin = 250; r.PayCap = 3000; }
        return r;
    }
    public static PRules Daily(int k)
    {
        var r = Flex(k); r.Name = k + "K DAILY (daily payouts, intraday trailing when funded)"; r.PayMode = "DAILY"; r.FundIntraday = true; r.MaxPayouts = 0; r.LockAfterPayout = true; r.Cost *= 1.3; return r;
    }
    public static PRules Pro(int k)
    {
        var r = Flex(k); r.Name = k + "K PRO (40% consistency, profit goal, buffer)"; r.PayMode = "BUFFER"; r.MaxPayouts = 0; r.FundConsistency = 40; r.EvalConsistency = 0; r.MinEvalDays = 1;
        r.PayDayMin = k == 25 ? 250 : k == 50 ? 500 : k == 100 ? 750 : 1000; return r;
    }

    public static void Run(List<VResult> all, Dictionary<string, List<RDay>> cal, string outDir, TextWriter w)
    {
        var picks = new[]
        {
            Tuple.Create("A OPENING DRIVE", "ORB2 09:30 10m opening-candle direction BOTH stop 0.25×ATR 4R days NONEWS"),
            Tuple.Create("B ORB 15", "ORB2 09:30 15m break BOTH stop 0.25×ATR 4R days NONEWS"),
            Tuple.Create("C ORB 30 1R", "ORB 09:30 30m BOTH stop OPP 1R entries 90m"),
        };
        var strategies = new List<Tuple<string, List<PDay>>>();
        foreach (var pk in picks)
        {
            var v = all.First(r => r.V.Sym == "MNQ" && r.V.Name == pk.Item2);
            strategies.Add(Tuple.Create(pk.Item1, DaysOf(cal["MNQ"], v.Trades)));
            using (var tw = new StreamWriter(Path.Combine(outDir, "trades_" + pk.Item1.Substring(0, 1) + ".csv")))
            {
                tw.WriteLine("day,dir,entry_time,exit_time,entry,exit,stop,target,pts,usd,mae_pts,mfe_pts,how");
                foreach (var t in v.Trades) tw.WriteLine(string.Join(",", t.Day.ToString("yyyy-MM-dd"), t.Dir, R.T(t.InSlot), R.T(t.OutSlot + 1), t.Entry, t.Exit, t.Stop.ToString("0.00"), double.IsNaN(t.Target) ? "" : t.Target.ToString("0.00"), t.Pts.ToString("0.00"), t.Usd.ToString("0.00"), t.MaePts.ToString("0.00"), t.MfePts.ToString("0.00"), t.Why.Trim()));
            }
            if (pk.Item1.StartsWith("A")) strategies.Add(Tuple.Create("Z ZERO-EDGE TWIN of A", DaysOf(cal["MNQ"], v.Trades, v.Trades.Average(t => t.Usd))));
        }
        DateTime is0 = new DateTime(2020, 1, 1), oos0 = R.OosStart, end = new DateTime(2026, 4, 1);
        var plans = new List<PRules>();
        foreach (int k in new[] { 25, 50, 100, 150 }) { plans.Add(Flex(k)); plans.Add(Flex(k, k == 25 ? 1500 : k == 50 ? 3000 : k == 100 ? 4500 : 6000)); plans.Add(Daily(k)); plans.Add(Pro(k)); }
        var best = new List<Tuple<double, string, PRules, string, double, double>>();
        foreach (var rules in plans)
        {
            w.WriteLine("\n######## " + rules.Name + " (assumed eval $" + rules.Cost.ToString("0") + ", target " + rules.Target + ", drawdown " + rules.Dd + ")");
            foreach (var s in strategies)
            {
                var rows = new List<Tuple<double, string, double, double>>();
                foreach (double es in new[] { 1, 2, 3, 4, 5, 6, 8, 10 })
                foreach (double fs in new[] { 1, 2, 3, 4, 5, 6, 8, 10 })
                {
                    var o = Prop.Each(s.Item2, oos0, end, rules, es, fs); var i = Prop.Each(s.Item2, is0, oos0, rules, es, fs);
                    double score = Math.Min(o.Cash, i.Cash);   // must hold in BOTH periods
                    rows.Add(Tuple.Create(score, string.Format("  eval {0,2} fund {1,2} micros | 2024–26: pass {2,3:0}% in {3,3:0}d, payouts {4,4:0.00}, CASH/eval ${5,5:0}, no payout {6,3:0}% | 2020–23: pass {7,3:0}% CASH/eval ${8,5:0} | worse period ${9,5:0}", es, fs, o.Pass, o.AvgEvalDays, o.Payouts, o.Cash, o.P0Pay, i.Pass, i.Cash, score), es, fs));
                }
                w.WriteLine("== " + s.Item1);
                foreach (var r in rows.OrderByDescending(x => x.Item1).Take(4)) w.WriteLine(r.Item2);
                var top = rows.OrderByDescending(x => x.Item1).First();
                best.Add(Tuple.Create(top.Item1 - rules.Cost, s.Item1, rules, top.Item2, top.Item3, top.Item4));
            }
        }
        w.WriteLine("\n######## RANKING (worse-period cash per evaluation minus the assumed price)");
        foreach (var b in best.OrderByDescending(x => x.Item1).Take(25)) w.WriteLine(string.Format("{0,6:0}  {1} • {2} •{3}", b.Item1, b.Item2, b.Item3.Name, b.Item4));

        // FINAL PLANS: start in any month 2021-01 … 2025-09, run 12 months — what a year looked like from every start
        w.WriteLine("\n######## FINAL PLANS • 12 MONTHS FROM EVERY START MONTH (2021-01 … 2025-09)");
        Func<string, List<PDay>> S = n => strategies.First(x => x.Item1.StartsWith(n)).Item2;
        var finals = new[]
        {
            Tuple.Create("P1 BEST VALUE • 150K FLEX • ORB15 • eval 6 / funded 2 micros • payout from +$6,000", "B", Flex(150, 6000), 6.0, 2.0),
            Tuple.Create("P2 DAILY PAYOUTS • 150K DAILY • ORB15 • eval 6 / funded 2", "B", Daily(150), 6.0, 2.0),
            Tuple.Create("P3 LOW BUDGET • 50K PRO • ORB15 • eval 2 / funded 1", "B", Pro(50), 2.0, 1.0),
            Tuple.Create("P4 LOW BUDGET • 50K FLEX • ORB15 • eval 2 / funded 1 • payout from +$3,000", "B", Flex(50, 3000), 2.0, 1.0),
            Tuple.Create("P5 STEADY • 150K FLEX • OPENING DRIVE • eval 3 / funded 3 • payout from +$6,000", "A", Flex(150, 6000), 3.0, 3.0),
            Tuple.Create("P6 FASTEST PASS • 50K PRO • OPENING DRIVE • eval 4 / funded 1", "A", Pro(50), 4.0, 1.0),
        };
        foreach (var f in finals)
        foreach (int slots in new[] { 1, 5 })
        {
            var nets = new List<double>(); var holes = new List<double>(); var buys = new List<int>(); var pays = new List<int>();
            for (var m = new DateTime(2021, 1, 1); m <= new DateTime(2025, 9, 1); m = m.AddMonths(1))
            {
                var p = Prop.Run(S(f.Item2), m, m.AddMonths(12), f.Item3, f.Item4, f.Item5, slots);
                nets.Add(p.Net); holes.Add(-p.WorstNet); buys.Add(p.Bought); pays.Add(p.Payouts);
            }
            nets.Sort(); holes.Sort();
            Func<List<double>, double, double> q = (l, x) => l[Math.Min(l.Count - 1, (int)(x * (l.Count - 1)))];
            w.WriteLine(string.Format("{0} • {1} account(s): 12-month net worst ${2,7:0} • 25% ${3,7:0} • median ${4,7:0} • 75% ${5,7:0} • best ${6,7:0} • losing years {7}/{8} • evals bought avg {9:0.0} • payouts avg {10:0.0} • deepest hole median ${11:0} / worst ${12:0}",
                f.Item1, slots, nets[0], q(nets, 0.25), q(nets, 0.5), q(nets, 0.75), nets[nets.Count - 1], nets.Count(x => x < 0), nets.Count, buys.Average(), pays.Average(), q(holes, 0.5), holes[holes.Count - 1]));
        }

        // MIXES: each account runs its own rule (accounts are independent, so a mix = the sum of single accounts)
        w.WriteLine("\n######## MIXES • 5 ACCOUNTS, 12 MONTHS FROM EVERY START MONTH");
        var singles = new Dictionary<string, Tuple<string, PRules, double, double>>
        {
            { "B", Tuple.Create("B", Flex(150, 6000), 6.0, 2.0) }, { "C", Tuple.Create("C", Flex(150, 6000), 3.0, 2.0) }, { "A", Tuple.Create("A", Flex(150, 6000), 3.0, 3.0) },
            { "b", Tuple.Create("B", Flex(50, 3000), 2.0, 1.0) }, { "c", Tuple.Create("C", Flex(50, 3000), 1.0, 1.0) },
        };
        var starts = new List<DateTime>(); for (var m = new DateTime(2021, 1, 1); m <= new DateTime(2025, 9, 1); m = m.AddMonths(1)) starts.Add(m);
        var one = new Dictionary<string, double[]>();
        foreach (var kv in singles) one[kv.Key] = starts.Select(m => Prop.Run(S(kv.Value.Item1), m, m.AddMonths(12), kv.Value.Item2, kv.Value.Item3, kv.Value.Item4, 1).Net).ToArray();
        foreach (var mix in new[] { "BBBBB", "CCCCC", "BBBCC", "BBCCA", "BCCCC", "bbbbb", "bbbcc", "ccccc" })
        {
            var nets = starts.Select((m, i) => mix.Sum(ch => one[ch.ToString()][i])).OrderBy(x => x).ToList();
            w.WriteLine(string.Format("{0} (B = ORB15 150K, C = ORB30 150K, A = DRIVE 150K, b / c = 50K): 12-month net worst ${1,7:0} • 25% ${2,7:0} • median ${3,7:0} • 75% ${4,7:0} • best ${5,7:0} • losing years {6}/{7}",
                mix, nets[0], nets[nets.Count / 4], nets[nets.Count / 2], nets[3 * nets.Count / 4], nets[nets.Count - 1], nets.Count(x => x < 0), nets.Count));
        }

        // programs: N accounts copying the signal, replaced when they die
        w.WriteLine("\n######## PROGRAMS (all accounts copy the same trades; a dead account is replaced the next day)");
        foreach (var b in best.Where(x => !x.Item2.StartsWith("Z")).OrderByDescending(x => x.Item1).Take(8))
        {
            var s = strategies.First(x => x.Item1 == b.Item2);
            w.WriteLine("== " + b.Item2 + " • " + b.Item3.Name + " • eval " + b.Item5 + " / funded " + b.Item6 + " micros");
            foreach (int slots in new[] { 1, 2, 3, 5 })
            foreach (var per in new[] { Tuple.Create("2021–23", new DateTime(2021, 1, 1), oos0), Tuple.Create("2024–26", oos0, new DateTime(2026, 10, 3)) })
            {
                var p = Prop.Run(s.Item2, per.Item2, per.Item3, b.Item3, b.Item5, b.Item6, slots);
                var ms = p.Monthly; int neg = ms.Count(x => x < 0);
                w.WriteLine(string.Format("  {0} accounts {1}: bought {2,3}, passed {3,3}, payouts {4,3} | net ${5,7:0} = ${6,5:0}/month | deepest hole ${7,6:0} | losing months {8}/{9}", slots, per.Item1, p.Bought, p.Passed, p.Payouts, p.Net, p.PerMonth, -p.WorstNet, neg, ms.Count));
            }
        }
    }
}
