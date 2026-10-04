// 1-MINUTE BREAK OF HIGH with an AGGRESSION LEG before it: every setup of the day (one at a time), for rotating accounts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class Bh1m
{
    public static void Run(List<RDay> days, string outDir)
    {
        var cfgs = new List<BhCfg>();
        foreach (int st in new[] { R.S(9, 30), R.S(10, 0) })
        foreach (int last in new[] { R.S(11, 30), R.S(15, 0) })
        foreach (int lm in new[] { 5, 10, 15 })
        foreach (double la in new[] { 0.05, 0.1, 0.15, 0.2 })
        foreach (string stop in new[] { "LOW", "P10", "P20" })
        foreach (var tg in new[] { Tuple.Create(1.0, 0.0), Tuple.Create(2.0, 0.0), Tuple.Create(3.0, 0.0), Tuple.Create(0.0, 20.0), Tuple.Create(0.0, 40.0) })
            cfgs.Add(new BhCfg { Min = 1, Start = st, LastEntry = last, MaxTrades = 20, LegMins = lm, LegAtr = la, Stop = stop, TargetR = tg.Item1, TargetPts = tg.Item2 });
        var res = new Tuple<BhCfg, List<RTrade>, Stats, Stats, Stats>[cfgs.Count];
        System.Threading.Tasks.Parallel.For(0, cfgs.Count, i =>
        {
            var tr = new List<RTrade>(); foreach (var d in days) tr.AddRange(Bh.Day(d, cfgs[i]));
            res[i] = Tuple.Create(cfgs[i], tr, Stats.Of(tr), Stats.Of(tr.Where(t => t.Day < R.OosStart)), Stats.Of(tr.Where(t => t.Day >= R.OosStart)));
        });
        using (var w = new StreamWriter(Path.Combine(outDir, "bh1m.txt")))
        {
            w.WriteLine("1-MINUTE BH + AGGRESSION LEG • " + cfgs.Count + " versions • every setup of the day • per MNQ micro after costs");
            w.WriteLine("versions positive on 2020–23: " + res.Count(r => r.Item4.Total > 0) + " • on 2024–26: " + res.Count(r => r.Item5.Total > 0) + " • on both: " + res.Count(r => r.Item4.Total > 0 && r.Item5.Total > 0));
            foreach (double la in new[] { 0.05, 0.1, 0.15, 0.2 }) w.WriteLine("  leg ≥ " + la + "×range: avg $/trade " + res.Where(r => r.Item1.LegAtr == la).Average(r => r.Item3.Avg).ToString("0.0") + " • 2024–26 " + res.Where(r => r.Item1.LegAtr == la).Average(r => r.Item5.Avg).ToString("0.0") + " • trades/day " + res.Where(r => r.Item1.LegAtr == la).Average(r => r.Item3.N / (double)days.Count).ToString("0.0"));
            foreach (int lm in new[] { 5, 10, 15 }) w.WriteLine("  leg within " + lm + " min: avg $/trade " + res.Where(r => r.Item1.LegMins == lm).Average(r => r.Item3.Avg).ToString("0.0") + " • 2024–26 " + res.Where(r => r.Item1.LegMins == lm).Average(r => r.Item5.Avg).ToString("0.0"));
            w.WriteLine("\nBEST 20 ranked on 2020–2023 only (≥ 200 trades):");
            foreach (var r in res.Where(r => r.Item4.N >= 200).OrderByDescending(r => r.Item4.T).Take(20))
                w.WriteLine(string.Format("  n {0,5} ({1:0.0}/day) win {2,3:0}% ${3,5:0.0}/trade pf {4,4:0.00} maxDD ${5,6:0} | 2020–23 ${6,5:0.0} (t {7,4:0.0}) | 2024–26 ${8,5:0.0} (t {9,4:0.0}) | {10} | {11}", r.Item3.N, r.Item3.N / (double)days.Count, r.Item3.Win, r.Item3.Avg, r.Item3.Pf, r.Item3.MaxDd, r.Item4.Avg, r.Item4.T, r.Item5.Avg, r.Item5.T,
                    string.Join(" ", Enumerable.Range(2021, 6).Select(y => { double v; return y + ":" + (r.Item3.Years.TryGetValue(y, out v) ? v.ToString("0") : "-"); })), r.Item1.Name()));
        }
        var top = res.Where(r => r.Item4.N >= 200).OrderByDescending(r => r.Item4.T).Take(3).Select(r => r.Item1).ToArray();
        BhRotation.Run(days, outDir, "bh1m_rotation.txt", top);
    }
}
