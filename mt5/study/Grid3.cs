// part 3: the live run's logic (entry = the break of the previous M1 candle, hedges on the break of the leg's previous candle, the next
// basket on the next break) × target × lots × hedge sizing (flat 2× / STEP) × hard stop (+ day lock) × session, $5,000 and $25,000
using System; using System.Collections.Generic; using System.Linq; using System.Globalization; using System.Threading.Tasks; using KeystoneMT5;
public static class Grid3
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static void Run(List<Mt5Bar> bars, Func<string, double, double, double, int, HedgeSettings> raw)
    {
        DateTime f = new DateTime(2025, 1, 6), e = new DateTime(2026, 10, 2), y1 = new DateTime(2025, 12, 31), y2 = new DateTime(2026, 1, 5);
        var jobs = new List<HedgeSettings>();
        foreach (var bal in new[] { 5000.0, 25000 }) foreach (var lot in new[] { 0.1, 0.3, 0.5 }) foreach (var tgt in new[] { 20.0, 50, 100 }) foreach (var hs in new[] { "FLAT", "STEP" })
        foreach (var stopPct in new[] { 0.0, 0.04, 0.10 }) foreach (var ses in new[] { "ALL", "NY 08-12" }) foreach (var look in new[] { 1, 5 })
        {
            var s = raw("", bal, lot, tgt, look); s.ReEnter = false; s.HedgeLot = lot * 2;
            if (hs == "STEP") { s.StepLoss = 100 * lot / 0.1; s.StepLots = lot; }
            if (stopPct > 0) { s.BasketStop = bal * stopPct; s.LockDayOnStop = true; }
            if (ses != "ALL") { s.SessionFrom = 480; s.SessionTo = 720; }
            s.Name = string.Format(IC, "${0:N0}|lot {1}|tgt ${2}|{3}|stop {4}|{5}|look {6}", bal, lot, tgt, hs, stopPct > 0 ? (stopPct * 100) + "%" : "none", ses, look);
            jobs.Add(s);
        }
        var rows = new (HedgeSettings s, HedgeResult all, HedgeResult a, HedgeResult b)[jobs.Count];
        Parallel.For(0, jobs.Count, i => rows[i] = (jobs[i], HedgeLab.Run(bars, jobs[i], f, e), HedgeLab.Run(bars, jobs[i], f, y1), HedgeLab.Run(bars, jobs[i], y2, e)));
        var csv = new List<string> { "balance,lot,target,hedging,stop,session,look,net,blown,blown_at,max_dd,max_dd_pct,cycles,lost,cycles_per_day,worst_day,net_2025,blown_2025,net_2026,blown_2026" };
        foreach (var r in rows) { var p = r.s.Name.Split('|'); int days = Math.Max(1, r.all.Days.Count(d => d.Cycles > 0)); csv.Add(string.Join(",", p[0].Replace(",", ""), p[1], p[2], p[3], p[4], p[5], p[6], r.all.Net.ToString("0", IC), r.all.Blown ? 1 : 0, r.all.Blown ? r.all.BlownAt.ToString("yyyy-MM-dd", IC) : "", r.all.MaxDd.ToString("0", IC), r.all.MaxDdPct.ToString("0", IC), r.all.Cycles.Count, r.all.Losses, (r.all.Cycles.Count / (double)days).ToString("0.0", IC), r.all.Days.Count == 0 ? "0" : r.all.Days.Min(d => d.EndEquity - d.StartEquity).ToString("0", IC), r.a.Net.ToString("0", IC), r.a.Blown ? 1 : 0, r.b.Net.ToString("0", IC), r.b.Blown ? 1 : 0)); }
        System.IO.File.WriteAllLines("../../docs/research/hedge/grid3_results.csv", csv);
        Func<HedgeResult, string> L = r => string.Format(IC, "net {0,9:N0} {1,-16} DD {2,7:N0} ({3,3:0}%) baskets {4,5} lost {5,3} /day {6,4:0.0} worst day {7,7:N0}", r.Net, r.Blown ? "BLOWN " + r.BlownAt.ToString("dd MMM yy", IC) : "ok", r.MaxDd, r.MaxDdPct, r.Cycles.Count, r.Losses, r.Cycles.Count / (double)Math.Max(1, r.Days.Count(d => d.Cycles > 0)), r.Days.Count == 0 ? 0 : r.Days.Min(d => d.EndEquity - d.StartEquity));
        Console.WriteLine(rows.Length + " settings • survived the whole period: " + rows.Count(r => !r.all.Blown) + " • survived + profitable in 2025 AND 2026: " + rows.Count(r => !r.a.Blown && !r.b.Blown && r.a.Net > 0 && r.b.Net > 0));
        foreach (var grp in new[] { "FLAT", "STEP" }) foreach (var st in new[] { "none", "4%", "10%" })
        { var g = rows.Where(r => r.s.Name.Contains("|" + grp + "|") && r.s.Name.Contains("|stop " + st + "|")).ToList(); Console.WriteLine(string.Format(IC, "  {0,-4} stop {1,-4}: survived {2,3}/{3}  profitable both years {4,3}  median net {5,8:N0}", grp, st, g.Count(r => !r.all.Blown), g.Count, g.Count(r => !r.a.Blown && !r.b.Blown && r.a.Net > 0 && r.b.Net > 0), g.Select(r => r.all.Net).OrderBy(x => x).ElementAt(g.Count / 2))); }
        Console.WriteLine("\nBEST 15 by the WORSE year (2025 vs 2026), not blown:");
        foreach (var r in rows.Where(r => !r.all.Blown && !r.a.Blown && !r.b.Blown).OrderByDescending(r => Math.Min(r.a.Net, r.b.Net)).Take(15))
            Console.WriteLine("  " + r.s.Name.PadRight(62) + "\n     all  " + L(r.all) + "\n     2025 " + L(r.a) + "\n     2026 " + L(r.b));
    }
}
