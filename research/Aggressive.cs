// Aggressive search: the limit is account SLOTS (a firm caps funded accounts), not money. For each rule × plan × size × payout
// timing, one slot runs on the real calendar (a dead account is replaced the next day): net $ per slot per month, both periods.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class Aggressive
{
    public static void Run(Dictionary<string, List<PDay>> strategies, TextWriter w)
    {
        var plans = new List<Func<double, PRules>> { pw => PropMain.Flex(150, pw), pw => PropMain.Flex(50, pw), pw => { var r = PropMain.Daily(150); r.PayWhenBal = pw; return r; }, pw => { var r = PropMain.Pro(150); r.PayWhenBal = pw; return r; } };
        var rows = new List<Tuple<double, string>>();
        DateTime a0 = new DateTime(2021, 1, 1), a1 = new DateTime(2024, 1, 1), b1 = new DateTime(2026, 10, 3);
        foreach (var s in strategies)
        foreach (var mk in plans)
        foreach (double pw in new[] { 0.0, 3000, 6000, 9000 })
        foreach (double es in new[] { 2, 4, 6, 8, 10, 15, 20 })
        foreach (double fs in new[] { 1, 2, 3, 4, 6, 8, 10, 15, 20 })
        {
            var r = mk(pw); if (r.Target < 5000 && (es > 10 || fs > 10)) continue;
            var p1 = Prop.Run(s.Value, a0, a1, r, es, fs, 1); var p2 = Prop.Run(s.Value, a1, b1, r, es, fs, 1);
            double worse = Math.Min(p1.PerMonth, p2.PerMonth);
            rows.Add(Tuple.Create(worse, string.Format("{0,-3} {1,-42} eval {2,2} fund {3,2} payout from ${4,5:0} | 2021–23 ${5,5:0}/mo (evals {6}, payouts {7}) | 2024–26 ${8,5:0}/mo (evals {9}, payouts {10}) | worse ${11,5:0}/mo",
                s.Key, r.Name.Split('(')[0].Trim(), es, fs, pw, p1.PerMonth, p1.Bought, p1.Payouts, p2.PerMonth, p2.Bought, p2.Payouts, worse)));
        }
        w.WriteLine("######## PER ACCOUNT SLOT • net $ a month (fees $150 50K / $350 150K / $455 150K daily) • ranked by the worse period");
        foreach (var r in rows.OrderByDescending(x => x.Item1).Take(40)) w.WriteLine(r.Item2);
    }
}
