// part 4: looking for ONE hedging scalper that holds up — chosen on 2025, checked on 2026 and on 2023-24 (never seen), gold and Nasdaq
using System; using System.Collections.Generic; using System.Linq; using System.Globalization; using System.Threading.Tasks; using KeystoneMT5;
public static class Grid4
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    // gold: 100 oz a lot, 0.20 spread, $7 / lot, 5 points slip • Nasdaq (USTEC): $1 a point a lot, 1.0 point spread, no commission, 0.2 point slip
    static HedgeSettings Inst(string inst, double lot)
    {
        if (inst == "GOLD") return new HedgeSettings { Balance = 10000, BaseLot = lot, Contract = 100, Point = 0.01, CommissionPerLot = 7, FixedSpreadPts = 20, SlipPts = 5, Leverage = 500, HedgedRatio = 0.5, StopOutPct = 50, SwapLong = -25, SwapShort = -25 };
        return new HedgeSettings { Balance = 10000, BaseLot = lot, Contract = 1, Point = 0.01, CommissionPerLot = 0, FixedSpreadPts = 100, SlipPts = 20, Leverage = 100, HedgedRatio = 0.5, StopOutPct = 50, SwapLong = -5, SwapShort = -2 };
    }
    public static void Run(string inst, List<Mt5Bar> bars)
    {
        double lot = inst == "GOLD" ? 0.1 : 1.0, perMove = inst == "GOLD" ? 10 : 1;   // $ per 1.0 of price at the base lot
        var moves = inst == "GOLD" ? new[] { 3.0, 5, 8 } : new[] { 15.0, 25, 40 };
        var sessions = new[] { Tuple.Create("NY open 08:30-10:30", 510, 630), Tuple.Create("NY 08:00-12:00", 480, 720), Tuple.Create("all day", -1, -1) };
        var hedges = new[] { "NONE", "1x2", "ZR2x2", "FLAT3" };
        var jobs = new List<HedgeSettings>();
        foreach (var se in sessions) foreach (var look in new[] { 5 }) foreach (var mv in moves.Take(2)) foreach (var h in hedges) foreach (var zf in h == "NONE" ? new[] { 0.0 } : new[] { 0.0, 1.0 })
        foreach (var sx in new[] { 2.0 }) foreach (var tries in new[] { 1, 0 }) foreach (var be in h == "NONE" ? new[] { 0 } : new[] { 0, 1 })
        {
            var s = Inst(inst, lot); s.Target = mv * perMove; s.Lookback = look; s.SessionFrom = se.Item2; s.SessionTo = se.Item3; s.ReEnter = false; s.OneTargetADay = tries == 1; s.MaxTriesADay = tries;
            s.BasketStop = sx * s.Target; s.LockDayOnStop = tries == 1;
            if (h == "NONE") s.NoHedge = true;
            else
            {
                s.Zone = zf * mv;   // 0 = the recent low / high (look-back); else a fixed move from the leg
                if (h == "1x2") { s.HedgeLot = 2 * lot; s.MaxLegs = 2; } else if (h == "1x3") { s.HedgeLot = 3 * lot; s.MaxLegs = 2; }
                else if (h == "ZR2x2") { s.HedgeLot = 2 * lot; s.Multiplier = 2; s.MaxLegs = 3; } else if (h == "ZR3x1.5") { s.HedgeLot = 2 * lot; s.Multiplier = 1.5; s.MaxLegs = 4; }
                else { s.HedgeLot = 2 * lot; s.MaxLegs = 4; }
                if (be == 1) { s.BreakevenAfter = 1; s.BeTarget = 0.5 * perMove * 0.2; }
            }
            s.Name = string.Format(IC, "{0}|look {1}|move {2}|{3}|zone {4}|stop {5}x|{6}|{7}", se.Item1, look, mv, h, zf == 0 ? "swing" : zf + "x", sx, tries == 1 ? "1 try/day" : "all day", be == 1 ? "BE after 1 hedge" : "full target");
            jobs.Add(s);
        }
        DateTime a0 = new DateTime(2025, 1, 6), a1 = new DateTime(2025, 12, 31), b0 = new DateTime(2026, 1, 5), b1 = new DateTime(2026, 10, 2), c0 = new DateTime(2023, 1, 2), c1 = new DateTime(2024, 12, 31);
        var rows = new (HedgeSettings s, HedgeResult a, HedgeResult b, HedgeResult c)[jobs.Count];
        Parallel.For(0, jobs.Count, i => rows[i] = (jobs[i], HedgeLab.Run(bars, jobs[i], a0, a1), HedgeLab.Run(bars, jobs[i], b0, b1), HedgeLab.Run(bars, jobs[i], c0, c1)));
        Func<HedgeResult, bool> ok = r => !r.Blown && r.Net > 0;
        var good = rows.Where(r => ok(r.a) && ok(r.b) && ok(r.c)).ToList();
        Console.WriteLine("\n==== " + inst + ": " + rows.Length + " settings • profitable + not blown in 2025: " + rows.Count(r => ok(r.a)) + " • ALSO in 2026 and 2023-24: " + good.Count);
        foreach (var h in hedges) { var g = rows.Where(r => r.s.Name.Contains("|" + h + "|")).ToList(); Console.WriteLine(string.Format(IC, "  {0,-8} all three periods profitable: {1,3}/{2,-4}  median 2025 / 2026 / 2023-24: {3,8:N0} {4,8:N0} {5,8:N0}", h, g.Count(r => ok(r.a) && ok(r.b) && ok(r.c)), g.Count, Med(g.Select(r => r.a.Net)), Med(g.Select(r => r.b.Net)), Med(g.Select(r => r.c.Net)))); }
        Func<HedgeResult, string> L = r => string.Format(IC, "{0,8:N0} DD {1,6:N0} baskets {2,4} lost {3,3} PF {4,4:0.00}", r.Net, r.MaxDd, r.Cycles.Count, r.Losses, Pf(r));
        Console.WriteLine("  the best by the WORST of the three periods:");
        foreach (var r in rows.OrderByDescending(r => Math.Min(r.a.Blown ? -1e9 : r.a.Net, Math.Min(r.b.Blown ? -1e9 : r.b.Net, r.c.Blown ? -1e9 : r.c.Net))).Take(10))
            Console.WriteLine("   " + r.s.Name + "\n      2025 " + L(r.a) + " | 2026 " + L(r.b) + " | 2023-24 " + L(r.c));
        var csv = new List<string> { "setting,net_2025,dd_2025,blown_2025,net_2026,dd_2026,blown_2026,net_2023_24,dd_2023_24,blown_2023_24,baskets_2025,pf_2025" };
        foreach (var r in rows) csv.Add("\"" + r.s.Name + "\"," + string.Join(",", r.a.Net.ToString("0", IC), r.a.MaxDd.ToString("0", IC), r.a.Blown ? 1 : 0, r.b.Net.ToString("0", IC), r.b.MaxDd.ToString("0", IC), r.b.Blown ? 1 : 0, r.c.Net.ToString("0", IC), r.c.MaxDd.ToString("0", IC), r.c.Blown ? 1 : 0, r.a.Cycles.Count, Pf(r.a).ToString("0.00", IC)));
        System.IO.File.WriteAllLines("../../docs/research/hedge/grid4_" + inst.ToLowerInvariant() + ".csv", csv);
    }
    // no hedge, small targets, the REVERSAL after aggression (fade / confirm), many trades a day
    public static void Rev(string inst, List<Mt5Bar> bars)
    {
        double lot = inst == "GOLD" ? 0.1 : 1.0; var jobs = new List<HedgeSettings>();
        var sessions = new[] { Tuple.Create("NY open 08:30-10:30", 510, 630), Tuple.Create("NY 08:00-12:00", 480, 720), Tuple.Create("all day", -1, -1) };
        foreach (var se in sessions) foreach (var rm in new[] { 1, 2 }) foreach (var av in new[] { 1.5, 2.5 }) foreach (var tg in new[] { 20.0, 25, 30, 50 }) foreach (var sx in new[] { 1.0, 1.5, 2.0 })
        {
            var s = Inst(inst, lot); s.NoHedge = true; s.RevMode = rm; s.AggVol = av; s.Target = tg; s.BasketStop = sx * tg; s.ReEnter = false; s.SessionFrom = se.Item2; s.SessionTo = se.Item3; s.Lookback = 1;
            s.Name = string.Format(IC, "{0}|{1}|vol {2}x|target ${3}|stop ${4}", se.Item1, rm == 1 ? "FADE" : "CONFIRM", av, tg, sx * tg); jobs.Add(s);
        }
        DateTime a0 = new DateTime(2025, 1, 6), a1 = new DateTime(2025, 12, 31), b0 = new DateTime(2026, 1, 5), b1 = new DateTime(2026, 10, 2), c0 = new DateTime(2023, 1, 2), c1 = new DateTime(2024, 12, 31);
        var rows = new (HedgeSettings s, HedgeResult a, HedgeResult b, HedgeResult c)[jobs.Count];
        Parallel.For(0, jobs.Count, i => rows[i] = (jobs[i], HedgeLab.Run(bars, jobs[i], a0, a1), HedgeLab.Run(bars, jobs[i], b0, b1), HedgeLab.Run(bars, jobs[i], c0, c1)));
        Func<HedgeResult, bool> ok = r => !r.Blown && r.Net > 0;
        Console.WriteLine("\n==== " + inst + " REVERSAL, no hedge: " + rows.Length + " settings • profitable in 2025: " + rows.Count(r => ok(r.a)) + " • ALSO 2026 and 2023-24: " + rows.Count(r => ok(r.a) && ok(r.b) && ok(r.c)));
        Func<HedgeResult, string> L = r => string.Format(IC, "{0,8:N0} DD {1,6:N0} trades {2,5} won {3,4:0}% PF {4,4:0.00} /day {5,4:0.0}", r.Net, r.MaxDd, r.Cycles.Count, r.Cycles.Count == 0 ? 0 : 100.0 * r.Wins / r.Cycles.Count, Pf(r), r.Cycles.Count / (double)Math.Max(1, r.Days.Count(d => d.Cycles > 0)));
        foreach (var r in rows.OrderByDescending(r => Math.Min(r.a.Blown ? -1e9 : r.a.Net, Math.Min(r.b.Blown ? -1e9 : r.b.Net, r.c.Blown ? -1e9 : r.c.Net))).Take(8))
            Console.WriteLine("   " + r.s.Name + "\n      2025 " + L(r.a) + "\n      2026 " + L(r.b) + "\n      23-24 " + L(r.c));
        var csv = new List<string> { "setting,net_2025,trades_2025,pf_2025,net_2026,trades_2026,pf_2026,net_2023_24,trades_2023_24,pf_2023_24" };
        foreach (var r in rows) csv.Add("\"" + r.s.Name + "\"," + string.Join(",", r.a.Net.ToString("0", IC), r.a.Cycles.Count, Pf(r.a).ToString("0.00", IC), r.b.Net.ToString("0", IC), r.b.Cycles.Count, Pf(r.b).ToString("0.00", IC), r.c.Net.ToString("0", IC), r.c.Cycles.Count, Pf(r.c).ToString("0.00", IC)));
        System.IO.File.WriteAllLines("../../docs/research/hedge/grid5_reversal_" + inst.ToLowerInvariant() + ".csv", csv);
        Console.Out.Flush();
    }
    static double Med(IEnumerable<double> v) { var l = v.OrderBy(x => x).ToList(); return l[l.Count / 2]; }
    static double Pf(HedgeResult r) { double w = r.Cycles.Where(c => c.Profit > 0).Sum(c => c.Profit), l = -r.Cycles.Where(c => c.Profit < 0).Sum(c => c.Profit); return l <= 0 ? (w > 0 ? 99 : 0) : w / l; }
}
