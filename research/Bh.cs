// BREAK OF HIGH (the user's setup, 5-minute candles): red candle → green reference candle → the NEXT candle trades
// above the reference high (buy stop one tick above it). Stop below the pattern low (or fixed points), target in R or
// points, flat 15:55. Compared: looking for the setup from 09:30 vs from 10:00.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public sealed class BhCfg
{
    public int Min = 5;   // candle size in minutes (5 = your chart; 1 = scalps)
    public int Start = R.S(9, 30), LastEntry = R.S(11, 30), MaxTrades = 1, MinReds = 1;
    public string Stop = "LOW";       // LOW = one tick under the low of the red + reference candles • P10 / P20 / P30 = fixed points
    public double TargetR = 2, TargetPts = 0;   // TargetPts > 0 overrides R; both 0 = hold to 15:55
    public bool BelowOpen = false;     // the pattern low must be under the 09:30 open (a real pullback after the open)
    public double Drop = 0;            // the push down: (highest high since the start − pattern low) ≥ Drop × 14-day 09:30–16:00 range (0 = off)
    public bool Sweep = false;         // the pattern low takes out the overnight low (18:00 – 09:29)
    public double BigRed = 0;          // the red candle's range ≥ BigRed × 14-day range (0 = off)
    public string Name()
    {
        return "BH " + Min + "M from " + R.T(Start) + " entries until " + R.T(LastEntry) + " • " + (MaxTrades == 1 ? "first setup only" : "up to " + MaxTrades + " a day") + (MinReds > 1 ? " • " + MinReds + "+ reds" : "")
            + " • stop " + (Stop == "LOW" ? "pattern low" : Stop.Substring(1) + " pts") + " • target " + (TargetPts > 0 ? TargetPts + " pts" : TargetR > 0 ? TargetR + "R" : "15:55") + (BelowOpen ? " • pullback under the open" : "") + (Drop > 0 ? " • drop ≥ " + Drop + "×range" : "") + (Sweep ? " • sweeps the overnight low" : "") + (BigRed > 0 ? " • red candle ≥ " + BigRed + "×range" : "");
    }
}

public static class Bh
{
    public static List<RTrade> Day(RDay d, BhCfg c)
    {
        var res = new List<RTrade>(); double tk = Spec.Of(d.Sym).Tick; int from = c.Start; double open930 = d.OpenAt(R.S(9, 30));
        int exitSlot = R.S(15, 55);
        while (res.Count < c.MaxTrades)
        {
            RTrade got = null;
            // 5-minute candles aligned to :00/:05 … starting at `from`
            int M = c.Min; int first = from + ((M - (from % M)) % M);   // slots are minutes since 18:00, which is on a 5-minute boundary
            for (int k = first; k + 3 * M <= c.LastEntry + M && got == null; k += M)
            {
                int red = k, refc = k + M, brk = k + 2 * M; if (refc + M > c.LastEntry + M) break;
                double rO = d.OpenAt(red), rC = d.CloseAt(red + M - 1), gO = d.OpenAt(refc), gC = d.CloseAt(refc + M - 1);
                if (double.IsNaN(rO) || double.IsNaN(gC) || !(rC < rO) || !(gC > gO)) continue;
                if (c.MinReds > 1) { bool ok = true; for (int j = 1; j < c.MinReds; j++) { int pk = red - M * j; if (pk < from) { ok = false; break; } if (!(d.CloseAt(pk + M - 1) < d.OpenAt(pk))) { ok = false; break; } } if (!ok) continue; }
                double refHigh = d.Hi(refc, refc + M - 1), patLow = Math.Min(d.Lo(red, red + M - 1), d.Lo(refc, refc + M - 1));
                if (c.MinReds > 1) for (int j = 1; j < c.MinReds; j++) patLow = Math.Min(patLow, d.Lo(red - M * j, red - M * j + M - 1));
                if (c.BelowOpen && !(patLow < open930)) continue;
                if (c.Drop > 0 && !(d.Hi(c.Start, refc + M - 1) - patLow >= c.Drop * d.RthAtr)) continue;
                if (c.Sweep && !(patLow < d.Lo(0, R.S(9, 29)))) continue;
                if (c.BigRed > 0 && !(d.Hi(red, red + M - 1) - d.Lo(red, red + M - 1) >= c.BigRed * d.RthAtr)) continue;
                double entry = refHigh + tk;
                double stopD = c.Stop == "LOW" ? entry - (patLow - tk) : double.Parse(c.Stop.Substring(1), CultureInfo.InvariantCulture);
                double tgt = c.TargetPts > 0 ? c.TargetPts : c.TargetR > 0 ? c.TargetR * stopD : 0;
                var t = R.Trade(d, 1, "STOP", refHigh, brk, brk + M - 1, stopD, tgt, exitSlot, "BH");
                if (t != null) got = t;
            }
            if (got == null) break;
            res.Add(got); from = got.OutSlot + 1; if (from >= c.LastEntry) break;
        }
        return res;
    }

    public static void Run(List<RDay> days, string outDir)
    {
        var cfgs = new List<BhCfg>();
        foreach (int st in new[] { R.S(9, 30), R.S(10, 0) })
        foreach (int last in new[] { R.S(11, 30), R.S(15, 0) })
        foreach (int mt in new[] { 1, 3 })
        foreach (bool below in new[] { false, true })
        foreach (double drop in new[] { 0.0, 0.1, 0.2, 0.3 })
        foreach (bool sweep in new[] { false, true })
        foreach (double big in new[] { 0.0, 0.05 })
        foreach (string stop in new[] { "LOW", "P40" })
        foreach (var tg in new[] { Tuple.Create(1.0, 0.0), Tuple.Create(2.0, 0.0), Tuple.Create(3.0, 0.0), Tuple.Create(0.0, 50.0), Tuple.Create(0.0, 100.0), Tuple.Create(0.0, 0.0) })
            cfgs.Add(new BhCfg { Start = st, LastEntry = last, MaxTrades = mt, BelowOpen = below, Drop = drop, Sweep = sweep, BigRed = big, Stop = stop, TargetR = tg.Item1, TargetPts = tg.Item2 });
        var one = new List<BhCfg>();
        foreach (int st in new[] { R.S(9, 30), R.S(10, 0) })
        foreach (int last in new[] { R.S(11, 30), R.S(15, 0) })
        foreach (int mt in new[] { 1, 3, 10 })
        foreach (double drop in new[] { 0.0, 0.1, 0.2 })
        foreach (bool sweep in new[] { false, true })
        foreach (string stop in new[] { "LOW", "P10", "P20" })
        foreach (var tg in new[] { Tuple.Create(1.0, 0.0), Tuple.Create(2.0, 0.0), Tuple.Create(3.0, 0.0), Tuple.Create(0.0, 10.0), Tuple.Create(0.0, 20.0), Tuple.Create(0.0, 40.0) })
            one.Add(new BhCfg { Min = 1, Start = st, LastEntry = last, MaxTrades = mt, Drop = drop, Sweep = sweep, Stop = stop, TargetR = tg.Item1, TargetPts = tg.Item2 });
        var results = new List<Tuple<BhCfg, List<RTrade>, Stats, Stats, Stats>>();
        var arr = new Tuple<BhCfg, List<RTrade>, Stats, Stats, Stats>[cfgs.Count];
        System.Threading.Tasks.Parallel.For(0, cfgs.Count, i =>
        {
            var tr = new List<RTrade>(); foreach (var d in days) tr.AddRange(Day(d, cfgs[i]));
            arr[i] = Tuple.Create(cfgs[i], tr, Stats.Of(tr), Stats.Of(tr.Where(t => t.Day < R.OosStart)), Stats.Of(tr.Where(t => t.Day >= R.OosStart)));
        });
        results.AddRange(arr);
        var arr1 = new Tuple<BhCfg, List<RTrade>, Stats, Stats, Stats>[one.Count];
        System.Threading.Tasks.Parallel.For(0, one.Count, i =>
        {
            var tr = new List<RTrade>(); foreach (var d in days) tr.AddRange(Day(d, one[i]));
            arr1[i] = Tuple.Create(one[i], tr, Stats.Of(tr), Stats.Of(tr.Where(t => t.Day < R.OosStart)), Stats.Of(tr.Where(t => t.Day >= R.OosStart)));
        });
        var ones = arr1.ToList();
        using (var w = new StreamWriter(Path.Combine(outDir, "bh.txt")))
        using (var csv = new StreamWriter(Path.Combine(outDir, "bh_variants.csv")))
        {
            csv.WriteLine("start,last_entry,max_trades,min_reds,below_open,stop,target_r,target_pts,n,win,avg,total,pf,t,maxdd,is_avg,is_t,oos_avg,oos_t,y2020,y2021,y2022,y2023,y2024,y2025,y2026");
            foreach (var r in results)
            {
                Func<int, string> y = yr => { double v; return r.Item3.Years.TryGetValue(yr, out v) ? v.ToString("0") : ""; };
                var c = r.Item1; csv.WriteLine(string.Join(",", R.T(c.Start), R.T(c.LastEntry), c.MaxTrades, c.MinReds, c.BelowOpen, c.Stop, c.TargetR, c.TargetPts, r.Item3.N, r.Item3.Win.ToString("0.0"), r.Item3.Avg.ToString("0.00"), r.Item3.Total.ToString("0"), r.Item3.Pf.ToString("0.00"), r.Item3.T.ToString("0.00"), r.Item3.MaxDd.ToString("0"), r.Item4.Avg.ToString("0.00"), r.Item4.T.ToString("0.00"), r.Item5.Avg.ToString("0.00"), r.Item5.T.ToString("0.00"), y(2020), y(2021), y(2022), y(2023), y(2024), y(2025), y(2026)));
            }
            Action<string, IEnumerable<Tuple<BhCfg, List<RTrade>, Stats, Stats, Stats>>> block = (title, rows) =>
            {
                w.WriteLine("\n== " + title);
                foreach (var r in rows)
                    w.WriteLine(string.Format("  n {0,5} win {1,3:0}% avg ${2,6:0.0} pf {3,4:0.00} maxDD ${4,6:0} | 2020–23 ${5,6:0.0} (t {6,4:0.0}) | 2024–26 ${7,6:0.0} (t {8,4:0.0}) | {9} | {10}", r.Item3.N, r.Item3.Win, r.Item3.Avg, r.Item3.Pf, r.Item3.MaxDd, r.Item4.Avg, r.Item4.T, r.Item5.Avg, r.Item5.T,
                        string.Join(" ", Enumerable.Range(2021, 6).Select(yy => { double v; return yy + ":" + (r.Item3.Years.TryGetValue(yy, out v) ? v.ToString("0") : "-"); })), r.Item1.Name()));
            };
            // the head-to-head: identical rules, only the start time differs
            Func<BhCfg, BhCfg, bool> same = (a, b) => a.LastEntry == b.LastEntry && a.MaxTrades == b.MaxTrades && a.MinReds == b.MinReds && a.BelowOpen == b.BelowOpen && a.Stop == b.Stop && a.TargetR == b.TargetR && a.TargetPts == b.TargetPts && a.Drop == b.Drop && a.Sweep == b.Sweep && a.BigRed == b.BigRed;
            var at930 = results.Where(r => r.Item1.Start == R.S(9, 30)).ToList(); var at10 = results.Where(r => r.Item1.Start == R.S(10, 0)).ToList();
            int win930 = 0, win10 = 0; double sum930 = 0, sum10 = 0;
            foreach (var a in at930) { var b = at10.First(x => same(x.Item1, a.Item1)); if (a.Item3.Total > b.Item3.Total) win930++; else win10++; sum930 += a.Item3.Total; sum10 += b.Item3.Total; }
            w.WriteLine("HEAD TO HEAD • the same " + at930.Count + " rule versions, only the start differs: 09:30 better in " + win930 + ", 10:00 better in " + win10 + " • average total per version: 09:30 $" + (sum930 / at930.Count).ToString("0") + " vs 10:00 $" + (sum10 / at10.Count).ToString("0") + " (per MNQ micro, Oct 2020 – Oct 2026)");
            var basic930 = results.First(r => r.Item1.Start == R.S(9, 30) && r.Item1.LastEntry == R.S(11, 30) && r.Item1.MaxTrades == 1 && r.Item1.MinReds == 1 && !r.Item1.BelowOpen && r.Item1.Stop == "LOW" && r.Item1.TargetR == 2 && r.Item1.Drop == 0 && !r.Item1.Sweep && r.Item1.BigRed == 0);
            var basic10 = results.First(r => same(r.Item1, basic930.Item1) && r.Item1.Start == R.S(10, 0));
            block("THE BASIC RULE (first setup, stop under the pattern, 2R): 09:30 vs 10:00", new[] { basic930, basic10 });
            block("BEST 15 STARTING 09:30 (ranked on 2020–2023 only)", at930.Where(r => r.Item4.N >= 100).OrderByDescending(r => r.Item4.T).Take(15));
            block("BEST 15 STARTING 10:00 (ranked on 2020–2023 only)", at10.Where(r => r.Item4.N >= 100).OrderByDescending(r => r.Item4.T).Take(15));
            int pos930 = at930.Count(r => r.Item5.Total > 0), pos10 = at10.Count(r => r.Item5.Total > 0);
            w.WriteLine("\nVERSIONS POSITIVE ON 2024–26: from 09:30 " + pos930 + " / " + at930.Count + " • from 10:00 " + pos10 + " / " + at10.Count);
            w.WriteLine("VERSIONS POSITIVE ON 2020–23: from 09:30 " + at930.Count(r => r.Item4.Total > 0) + " / " + at930.Count + " • from 10:00 " + at10.Count(r => r.Item4.Total > 0) + " / " + at10.Count);
            foreach (var g in new[] { "LOW", "P40" }) w.WriteLine("  stop " + g + ": avg $/trade 09:30 " + at930.Where(r => r.Item1.Stop == g).Average(r => r.Item3.Avg).ToString("0.0") + " • 10:00 " + at10.Where(r => r.Item1.Stop == g).Average(r => r.Item3.Avg).ToString("0.0"));
            foreach (bool b in new[] { false, true }) w.WriteLine("  pullback under the open " + b + ": avg $/trade 09:30 " + at930.Where(r => r.Item1.BelowOpen == b).Average(r => r.Item3.Avg).ToString("0.0") + " • 10:00 " + at10.Where(r => r.Item1.BelowOpen == b).Average(r => r.Item3.Avg).ToString("0.0"));
            foreach (double dr in new[] { 0.0, 0.1, 0.2, 0.3 }) w.WriteLine("  drop ≥ " + dr + "×range: avg $/trade 09:30 " + at930.Where(r => r.Item1.Drop == dr).Average(r => r.Item3.Avg).ToString("0.0") + " (2024–26 " + at930.Where(r => r.Item1.Drop == dr).Average(r => r.Item5.Avg).ToString("0.0") + ") • 10:00 " + at10.Where(r => r.Item1.Drop == dr).Average(r => r.Item3.Avg).ToString("0.0") + " (2024–26 " + at10.Where(r => r.Item1.Drop == dr).Average(r => r.Item5.Avg).ToString("0.0") + ") • trades/version " + at930.Where(r => r.Item1.Drop == dr).Average(r => r.Item3.N).ToString("0"));
            foreach (bool sw in new[] { false, true }) w.WriteLine("  sweeps the overnight low " + sw + ": avg $/trade 09:30 " + at930.Where(r => r.Item1.Sweep == sw).Average(r => r.Item3.Avg).ToString("0.0") + " (2024–26 " + at930.Where(r => r.Item1.Sweep == sw).Average(r => r.Item5.Avg).ToString("0.0") + ") • 10:00 " + at10.Where(r => r.Item1.Sweep == sw).Average(r => r.Item3.Avg).ToString("0.0") + " • trades/version " + at930.Where(r => r.Item1.Sweep == sw).Average(r => r.Item3.N).ToString("0"));
            foreach (double bg in new[] { 0.0, 0.05 }) w.WriteLine("  big red candle " + bg + ": avg $/trade 09:30 " + at930.Where(r => r.Item1.BigRed == bg).Average(r => r.Item3.Avg).ToString("0.0") + " (2024–26 " + at930.Where(r => r.Item1.BigRed == bg).Average(r => r.Item5.Avg).ToString("0.0") + ") • 10:00 " + at10.Where(r => r.Item1.BigRed == bg).Average(r => r.Item3.Avg).ToString("0.0"));
            block("1-MINUTE BH (SCALPS) • BEST 15 FROM 09:30 (ranked on 2020–2023 only)", ones.Where(r => r.Item1.Start == R.S(9, 30) && r.Item4.N >= 100).OrderByDescending(r => r.Item4.T).Take(15));
            block("1-MINUTE BH (SCALPS) • BEST 10 FROM 10:00 (ranked on 2020–2023 only)", ones.Where(r => r.Item1.Start == R.S(10, 0) && r.Item4.N >= 100).OrderByDescending(r => r.Item4.T).Take(10));
            w.WriteLine("1-MINUTE VERSIONS POSITIVE ON 2024–26: from 09:30 " + ones.Count(r => r.Item1.Start == R.S(9, 30) && r.Item5.Total > 0) + " / " + ones.Count(r => r.Item1.Start == R.S(9, 30)) + " • from 10:00 " + ones.Count(r => r.Item1.Start == R.S(10, 0) && r.Item5.Total > 0) + " / " + ones.Count(r => r.Item1.Start == R.S(10, 0)) + " • average $/trade 09:30 " + ones.Where(r => r.Item1.Start == R.S(9, 30)).Average(r => r.Item3.Avg).ToString("0.0") + " vs 10:00 " + ones.Where(r => r.Item1.Start == R.S(10, 0)).Average(r => r.Item3.Avg).ToString("0.0"));
            foreach (int m in new[] { 1, 3 }) w.WriteLine("  trades a day " + m + ": avg $/trade 09:30 " + at930.Where(r => r.Item1.MaxTrades == m).Average(r => r.Item3.Avg).ToString("0.0") + " • 10:00 " + at10.Where(r => r.Item1.MaxTrades == m).Average(r => r.Item3.Avg).ToString("0.0"));
        }
    }
}
