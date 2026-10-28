// ⇅ HEDGE LAB study: the hedge-instead-of-a-stop variations on 1-minute gold (data/MGC as XAUUSD), Jan 2025 → the end of the data.
// dotnet run -c Release [data/MGC folder]  →  the table on screen + study_*.csv
using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Globalization; using KeystoneMT5;
public static class Program
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    // gaps closed: a candle opening after a pause of 50+ minutes starts where the last one closed (the rest of the history shifted with it)
    static List<Mt5Bar> NoGaps(List<Mt5Bar> src)
    {
        var res = new List<Mt5Bar>(src.Count); double shift = 0;
        for (int i = 0; i < src.Count; i++)
        {
            var b = src[i].Copy();
            if (i > 0 && (b.Time - src[i - 1].Time).TotalMinutes >= 50) shift += src[i - 1].C - b.O;
            b.O += shift; b.H += shift; b.L += shift; b.C += shift; res.Add(b);
        }
        return res;
    }
    static HedgeSettings Raw(string name, double bal, double lot, double target, int look)
    {   // IC Markets RAW-like XAUUSD: 100 oz a lot, $7 a lot round turn, 0.20 spread, 1:500, hedged margin 50 %, stop-out 50 %, swap −$25 a lot a night
        return new HedgeSettings { Name = name, Balance = bal, BaseLot = lot, HedgeLot = lot * 2, Target = target, Lookback = look, Contract = 100, Point = 0.01, CommissionPerLot = 7, FixedSpreadPts = 20, SlipPts = 5, Leverage = 500, HedgedRatio = 0.5, StopOutPct = 50, SwapLong = -25, SwapShort = -25 };
    }
    public static int Main(string[] args)
    {
        string data = args.Length > 0 && args[0] != "trace" && args[0] != "sweep" && args[0] != "best" && args[0] != "export" ? args[0] : "../../data/MGC";
        if (args.Length > 2 && args[0] == "export")
        {   // export <history root> <days>: the last days as SpasiX's own saved history (XAUUSD), for the screenshots
            HistoryStore.Root = args[1]; var b = Gold.Load("../../data/MGC", new DateTime(2026, 8, 1), new DateTime(2026, 10, 2));
            var days = b.GroupBy(x => x.Time.Date).OrderBy(g => g.Key).ToList(); days = days.Skip(Math.Max(0, days.Count - int.Parse(args[2]))).ToList();
            foreach (var g in days) { foreach (var x in g) x.Spread = 20; HistoryStore.SaveDay("XAUUSD", g.Key, g.ToList()); }
            HistoryStore.SaveSpec(new SimSpec { Name = "XAUUSD", Description = "Gold vs US Dollar", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, CommissionPerLot = 7, SwapLong = -25, SwapShort = -25, SwapMode = 4, HedgedRatio = 0.5 });
            Console.WriteLine("exported " + days.Count + " days"); return 0;
        }
        if (args.Length > 0 && args[0] == "best")
        {
            var b = Gold.Load(data, new DateTime(2024, 12, 28), new DateTime(2026, 10, 2));
            var picks = new List<HedgeSettings>();
            { var s = Raw("B 0.25 look 240 tgt $200 min$8", 5000, 0.25, 200, 240); s.MinDist = 8; s.DailyTarget = 100; picks.Add(s); }
            { var s = Raw("B 0.25 look 20 tgt $50 min$8", 5000, 0.25, 50, 20); s.MinDist = 8; s.DailyTarget = 100; picks.Add(s); }
            { var s = Raw("B 0.1 look 240 tgt $40", 5000, 0.1, 40, 240); s.DailyTarget = 100; picks.Add(s); }
            { var s = Raw("A 0.01 look 120 tgt $8", 350, 0.01, 8, 120); picks.Add(s); }
            foreach (var s in picks)
                foreach (var p in new[] { Tuple.Create(new DateTime(2025, 1, 6), new DateTime(2026, 10, 2)), Tuple.Create(new DateTime(2025, 1, 6), new DateTime(2025, 12, 31)), Tuple.Create(new DateTime(2026, 1, 5), new DateTime(2026, 10, 2)) })
                {
                    var r = HedgeLab.Run(b, s, p.Item1, p.Item2);
                    Console.WriteLine(p.Item1.ToString("MMM yy", IC) + "-" + p.Item2.ToString("MMM yy", IC) + "  " + HedgeLab.Line(r));
                    if (p.Item2.Year == 2026 && p.Item1.Year == 2025) Console.WriteLine("      by month: " + string.Join("  ", r.Days.GroupBy(d => d.Day.ToString("yy-MM", IC)).Select(g => g.Key + " " + (g.Last().EndEquity - g.First().StartEquity).ToString("+0;-0", IC))));
                }
            return 0;
        }
        if (args.Length > 0 && args[0] == "sweep")
        {
            DateTime f = new DateTime(2025, 1, 6), e = new DateTime(2026, 10, 2); var b = Gold.Load(data, f.AddDays(-5), e);
            Console.WriteLine("== $350, whole day =="); Sweep.Run(b, f, e, Raw, 350, new[] { 0.01, 0.02, 0.05, 0.1 }, 0);
            Console.WriteLine("== $5000, $100 a day =="); Sweep.Run(b, f, e, Raw, 5000, new[] { 0.05, 0.1, 0.25 }, 100);
            Console.WriteLine("== margin: $350 0.1 look 10 $20 with other brokers' margin =="); foreach (var lev in new[] { 200, 500, 1000 }) foreach (var hr in new[] { 0.0, 0.5, 1.0 }) { var s = Raw("lev 1:" + lev + " hedged " + hr * 100 + "%", 350, 0.1, 20, 10); s.Leverage = lev; s.HedgedRatio = hr; var w = new List<HedgeResult>(); for (var st = f; st.AddDays(28) <= e; st = st.AddDays(7)) w.Add(HedgeLab.Run(b, s, st, st.AddDays(27))); Console.WriteLine(string.Format(IC, "  {0,-24} blown {1,3:0}%  median {2,6:N0}", s.Name, 100.0 * w.Count(x => x.Blown) / w.Count, w.Select(x => x.Net).OrderBy(x => x).ElementAt(w.Count / 2))); }
            return 0;
        }
        if (args.Length > 0 && args[0] == "trace") { Trace.Run(data, new DateTime(2025, 1, 6), new DateTime(2025, 1, 6), Raw("A trace", 350, 0.1, 20, 10)); return 0; }
        DateTime from = new DateTime(2025, 1, 6), to = new DateTime(2026, 10, 2);
        var asIs = Gold.Load(data, from.AddDays(-5), to); var flat = NoGaps(asIs);
        Console.WriteLine("XAUUSD stand-in: " + asIs.Count + " one-minute candles, " + from.ToString("dd MMM yyyy", IC) + " → " + to.ToString("dd MMM yyyy", IC));

        var sets = new List<HedgeSettings>();
        foreach (var look in new[] { 5, 10, 20, 30, 60 }) sets.Add(Raw("A $350 0.1/0.2 $20 look " + look, 350, 0.1, 20, look));
        foreach (var md in new[] { 2.0, 4.0 }) { var x = Raw("A $350 0.1/0.2 $20 look 10 min$" + md, 350, 0.1, 20, 10); x.MinDist = md; sets.Add(x); }
        foreach (var t in new[] { 10.0, 40.0 }) sets.Add(Raw("A $350 0.1/0.2 $" + t + " look 10", 350, 0.1, t, 10));
        foreach (var cap in new[] { 3, 5 }) { var x = Raw("A $350 0.1/0.2 $20 look 10 cap " + cap, 350, 0.1, 20, 10); x.MaxLegs = cap; sets.Add(x); }
        { var x = Raw("A $350 0.01/0.02 $2 look 10", 350, 0.01, 2, 10); sets.Add(x); }
        { var x = Raw("A $350 0.1/0.2 $20 look 10 wait", 350, 0.1, 20, 10); x.ReEnter = false; sets.Add(x); }
        { var x = Raw("A $350 martingale x2 look 10", 350, 0.1, 20, 10); x.Multiplier = 2; sets.Add(x); }
        foreach (var look in new[] { 5, 10, 20, 30, 60 }) { var x = Raw("B $5000 0.25/0.5 $20 day$100 look " + look, 5000, 0.25, 20, look); x.DailyTarget = 100; sets.Add(x); }
        foreach (var t in new[] { 50.0, 100.0 }) { var x = Raw("B $5000 0.25/0.5 $" + t + " day$100 look 10", 5000, 0.25, t, 10); x.DailyTarget = 100; sets.Add(x); }
        foreach (var cap in new[] { 3, 5 }) { var x = Raw("B $5000 0.25/0.5 $20 day$100 look 10 cap " + cap, 5000, 0.25, 20, 10); x.DailyTarget = 100; x.MaxLegs = cap; sets.Add(x); }
        { var x = Raw("B $5000 0.25/0.5 $20 no day cap look 10", 5000, 0.25, 20, 10); sets.Add(x); }

        var csv = new List<string> { "data,name,net,end_equity,blown,blown_at,cycles,lost_cycles,max_dd,max_dd_pct,max_legs,max_gross,days,target_days,commission,swap,win_months,months,roll_windows,roll_blown,roll_profitable,roll_median" };
        foreach (var pass in new[] { Tuple.Create("as-is", asIs), Tuple.Create("gaps closed", flat) })
        {
            Console.WriteLine("\n==== " + pass.Item1 + " ====   (continuous run from the first day; then 4-week windows starting every Monday)");
            foreach (var s in sets)
            {
                var r = HedgeLab.Run(pass.Item2, s, from, to);
                // rolling 4-week windows: a fresh account every Monday
                var res = new List<HedgeResult>();
                for (var st = from; st.AddDays(28) <= to; st = st.AddDays(7)) res.Add(HedgeLab.Run(pass.Item2, s, st, st.AddDays(27)));
                int blown = res.Count(x => x.Blown), prof = res.Count(x => !x.Blown && x.Net > 0); var nets = res.Select(x => x.Net).OrderBy(x => x).ToList(); double med = nets[nets.Count / 2];
                Console.WriteLine(HedgeLab.Line(r) + string.Format(IC, "  | 4-wk windows {0}: blown {1,3:0}%  profitable {2,3:0}%  median {3,8:N0}", res.Count, 100.0 * blown / res.Count, 100.0 * prof / res.Count, med));
                csv.Add(string.Join(",", pass.Item1, "\"" + s.Name + "\"", r.Net.ToString("0.00", IC), r.EndEquity.ToString("0.00", IC), r.Blown ? 1 : 0, r.Blown ? r.BlownAt.ToString("yyyy-MM-dd HH:mm", IC) : "", r.Cycles.Count, r.Losses, r.MaxDd.ToString("0", IC), r.MaxDdPct.ToString("0.0", IC), r.MaxLegs, r.MaxGross.ToString("0.00", IC), r.Days.Count, r.TargetDays, r.Commission.ToString("0", IC), r.Swap.ToString("0", IC), 0, 0, res.Count, blown, prof, med.ToString("0", IC)));
            }
        }
        File.WriteAllLines("../../docs/research/hedge/study_results.csv", csv);
        return 0;
    }
}
