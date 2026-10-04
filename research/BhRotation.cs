// EVERY BH setup, rotated across prop accounts: each new setup goes to the next account in turn (a dead account is
// replaced by a new evaluation). Compared with the same trades with their edge removed (zero-edge twin).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class BhRotation
{
    public static void Run(List<RDay> days, string outDir)
    {
        var w = new StreamWriter(Path.Combine(outDir, "bh_rotation.txt"));
        var cfgs = new[]
        {
            new BhCfg { Start = R.S(9, 30), LastEntry = R.S(15, 0), MaxTrades = 20, Stop = "LOW", TargetR = 2 },
            new BhCfg { Start = R.S(9, 30), LastEntry = R.S(15, 0), MaxTrades = 20, Stop = "LOW", TargetR = 1 },
            new BhCfg { Start = R.S(9, 30), LastEntry = R.S(15, 0), MaxTrades = 20, Stop = "LOW", TargetR = 3 },
            new BhCfg { Start = R.S(9, 30), LastEntry = R.S(11, 30), MaxTrades = 20, Stop = "LOW", TargetR = 2, Sweep = true },
            new BhCfg { Start = R.S(10, 0), LastEntry = R.S(15, 0), MaxTrades = 20, Stop = "LOW", TargetR = 2 },
        };
        foreach (var c in cfgs)
        {
            var trades = new List<RTrade>(); foreach (var d in days) trades.AddRange(Bh.Day(d, c));
            var st = Stats.Of(trades); double mean = st.Avg;
            w.WriteLine("\n== EVERY SETUP • " + c.Name() + " • " + trades.Count + " trades (" + (trades.Count / (double)days.Count).ToString("0.0") + " a day) • $" + st.Avg.ToString("0.0") + " a trade per micro • win " + st.Win.ToString("0") + "% • 2024–26 $" + Stats.Of(trades.Where(t => t.Day >= R.OosStart)).Avg.ToString("0.0") + " a trade");
            foreach (var shift in new[] { 0.0, mean })
            foreach (var mk in new Func<PRules>[] { () => PropMain.Flex(50, 0), () => PropMain.Flex(150, 6000) })
            foreach (int accounts in new[] { 3, 5, 10 })
            foreach (double size in new[] { 2, 5, 10 })
            {
                var r = mk(); if (r.Target < 5000 && size > 5) continue;
                foreach (var per in new[] { Tuple.Create("2021–23", new DateTime(2021, 1, 1), R.OosStart), Tuple.Create("2024–26", R.OosStart, new DateTime(2026, 10, 3)) })
                {
                    var pool = new List<PAcct>(); double net = 0, worst = 0; int bought = 0, payouts = 0;
                    Func<PAcct> buy = () => { bought++; net -= r.Cost; return new PAcct(r, size, Math.Max(1, size / 2)); };
                    for (int i = 0; i < accounts; i++) pool.Add(buy());
                    int next = 0;
                    foreach (var t in trades.Where(x => x.Day >= per.Item2 && x.Day < per.Item3))
                    {
                        var sp = Spec.Of(t.Sym);
                        var pd = new PDay { Day = t.Day, Traded = true, Pnl = t.Usd - shift, Worst = Math.Min(t.Usd, -t.MaePts * sp.PointValue - sp.CommissionRt) - shift, Best = Math.Max(t.Usd, t.MfePts * sp.PointValue) - shift };
                        var a = pool[next]; double cash = a.Step(pd); net += cash; if (cash > 0) payouts++;
                        if (a.Dead) pool[next] = buy();
                        next = (next + 1) % accounts; worst = Math.Min(worst, net);
                    }
                    int months = (int)Math.Round((per.Item3 - per.Item2).TotalDays / 30.44);
                    w.WriteLine(string.Format("  {0,-11} {1,-9} {2,2} accounts rotating • eval {3,2} / funded {4,2} micros • {5}: net ${6,8:0} = ${7,6:0}/month • evals bought {8,4} • payouts {9,3} • deepest hole ${10,7:0}",
                        shift == 0 ? "REAL" : "ZERO-EDGE", r.Name.Split('(')[0].Trim(), accounts, size, Math.Max(1, size / 2), per.Item1, net, net / months, bought, payouts, -worst));
                }
            }
        }
        w.Close();
    }
}
