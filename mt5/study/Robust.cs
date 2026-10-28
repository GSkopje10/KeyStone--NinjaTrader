// the sessions study's winner was a plain trade (the basket stop fires before any hedge): one basket a day, +$target, −$stop. Is it a plateau
// or a spike? target move × stop × session × entry, with NO hedge vs WITH hedges inside the stop; 2025 and 2026 apart, month by month
using System; using System.Collections.Generic; using System.Linq; using System.Globalization; using System.Threading.Tasks; using KeystoneMT5;
public static class Robust
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static void Run(List<Mt5Bar> bars, Func<string, double, double, double, int, HedgeSettings> raw)
    {
        DateTime a0 = new DateTime(2025, 1, 6), a1 = new DateTime(2025, 12, 31), b0 = new DateTime(2026, 1, 5), b1 = new DateTime(2026, 10, 2);
        var sess = Sessions.All.Where(x => x.name.StartsWith("NY") || x.name.StartsWith("all") || x.name.StartsWith("overlap") || x.name.StartsWith("London 03")).ToList();
        var rows = new List<(string name, string hedge, double tm, double sx, HedgeResult r5, HedgeResult r6)>(); var gate = new object();
        var jobs = new List<(string se, int from, int to, int look, double tm, double sx, string hedge)>();
        foreach (var se in sess) foreach (var look in new[] { 5, 10, 20, 30 }) foreach (var tm in new[] { 2.0, 3, 4, 5, 6 }) foreach (var sx in new[] { 1.0, 1.5, 2, 3 }) foreach (var hedge in new[] { "no hedge", "hedge at half the stop" })
            jobs.Add((se.name, se.from, se.to, look, tm, sx, hedge));
        Parallel.ForEach(jobs, j =>
        {
            var s = raw("", 5000, 0.25, j.tm * 0.25 * 100, j.look); s.OneTargetADay = true; s.ReEnter = false; s.SessionFrom = j.from; s.SessionTo = j.to; s.BasketStop = j.sx * s.Target;
            s.NoHedge = j.hedge == "no hedge"; s.Zone = j.tm * j.sx / 2;
            s.Name = string.Format(IC, "{0,-22} entry recent {1,2}  target ${2} move (${3,3:0})  stop {4}x", j.se, j.look, j.tm, s.Target, j.sx);
            var r5 = HedgeLab.Run(bars, s, a0, a1); var r6 = HedgeLab.Run(bars, s, b0, b1);
            lock (gate) rows.Add((s.Name, j.hedge, j.tm, j.sx, r5, r6));
        });
        foreach (var h in new[] { "no hedge", "hedge at half the stop" })
        {
            var hr = rows.Where(x => x.hedge == h).ToList();
            Console.WriteLine(string.Format(IC, "\n== B $5,000 0.25 lot, one target a day, {0}: {1} settings — profitable in 2025: {2}, in 2026: {3}, in both: {4}, blown in either: {5}",
                h.ToUpperInvariant(), hr.Count, hr.Count(x => !x.r5.Blown && x.r5.Net > 0), hr.Count(x => !x.r6.Blown && x.r6.Net > 0), hr.Count(x => !x.r5.Blown && !x.r6.Blown && x.r5.Net > 0 && x.r6.Net > 0), hr.Count(x => x.r5.Blown || x.r6.Blown)));
            Console.WriteLine("   by stop (median net 2025 / 2026):  " + string.Join("   ", new[] { 1.0, 1.5, 2, 3 }.Select(sx => { var g = hr.Where(x => x.sx == sx).ToList(); return sx + "x " + Med(g.Select(x => x.r5.Net)) + " / " + Med(g.Select(x => x.r6.Net)); })));
            Console.WriteLine("   by target move (median net 2025 / 2026):  " + string.Join("   ", new[] { 2.0, 3, 4, 5, 6 }.Select(tm => { var g = hr.Where(x => x.tm == tm).ToList(); return "$" + tm + " " + Med(g.Select(x => x.r5.Net)) + " / " + Med(g.Select(x => x.r6.Net)); })));
            Console.WriteLine("   by session (median net 2025 / 2026):  " + string.Join("   ", sess.Select(se => { var g = hr.Where(x => x.name.StartsWith(se.name)).ToList(); return se.name.Split(' ')[0] + (se.name.Contains("open") ? " open" : "") + " " + Med(g.Select(x => x.r5.Net)) + " / " + Med(g.Select(x => x.r6.Net)); })));
            Console.WriteLine("   best by the WORSE of the two years:");
            foreach (var x in hr.Where(x => !x.r5.Blown && !x.r6.Blown).OrderByDescending(x => Math.Min(x.r5.Net, x.r6.Net)).Take(8))
                Console.WriteLine(string.Format(IC, "     {0}   2025 {1,7:N0} (DD {2,5:N0})   2026 {3,7:N0} (DD {4,5:N0})   win days {5}/{6} + {7}/{8}", x.name, x.r5.Net, x.r5.MaxDd, x.r6.Net, x.r6.MaxDd, x.r5.Days.Count(d => d.Won), x.r5.Days.Count, x.r6.Days.Count(d => d.Won), x.r6.Days.Count));
        }
    }
    static string Med(IEnumerable<double> v) { var l = v.OrderBy(x => x).ToList(); return l[l.Count / 2].ToString("+#,0;−#,0;0", IC); }

    // one setting in detail: month by month, trades a day, and the $350 version at 0.02 / 0.05 lots
    public static void Detail(List<Mt5Bar> bars, Func<string, double, double, double, int, HedgeSettings> raw, string name, int from, int to, int look, double tm, double sx, int tries = 0, bool months = true)
    {
        DateTime a0 = new DateTime(2023, 1, 2), b1 = new DateTime(2026, 10, 2);
        foreach (var acct in new[] { (5000.0, 0.25), (350.0, 0.02), (350.0, 0.05), (350.0, 0.1) })
        {
            var s = raw("", acct.Item1, acct.Item2, tm * acct.Item2 * 100, look); s.OneTargetADay = true; s.ReEnter = false; s.SessionFrom = from; s.SessionTo = to; s.BasketStop = sx * s.Target; s.NoHedge = true; s.MaxTriesADay = tries;
            var r = HedgeLab.Run(bars, s, a0, b1); var traded = r.Days.Where(d => d.Cycles > 0).ToList();
            Console.WriteLine(string.Format(IC, "\n{0}  ${1:N0} lot {2}: target ${3:0} stop ${4:0} — net {5:N0} ({6}), max DD {7:N0} ({8:0}%), days traded {9}, won {10}, lost-only days {11}, trades {12} (won {13}, stopped {14}), worst day {15:N0}, most losses in a row {16}",
                name + (tries > 0 ? ", max " + tries + " tr" + (tries == 1 ? "y" : "ies") + " a day" : ", tries until a win"), acct.Item1, acct.Item2, s.Target, s.BasketStop, r.Net, r.Blown ? "BLOWN " + r.BlownAt.ToString("dd MMM yy", IC) : "survived", r.MaxDd, r.MaxDdPct, traded.Count, traded.Count(d => d.Won), traded.Count(d => !d.Won), r.Cycles.Count, r.Wins, r.Losses, r.Days.Min(d => d.EndEquity - d.StartEquity), Streak(r)));
            foreach (var y in new[] { 2023, 2024, 2025, 2026 }) { var yr = HedgeLab.Run(bars, s, new DateTime(y, 1, 1), y == 2026 ? b1 : new DateTime(y, 12, 31)); Console.WriteLine(string.Format(IC, "   {0} alone: net {1,8:N0}  {2,-17} max DD {3,6:N0}  trades {4} (won {5})", y, yr.Net, yr.Blown ? "BLOWN " + yr.BlownAt.ToString("dd MMM", IC) : "ok", yr.MaxDd, yr.Cycles.Count, yr.Wins)); }
            if (months) Console.WriteLine("   by month: " + string.Join("  ", r.Days.GroupBy(d => d.Day.ToString("yy-MM", IC)).Select(g => g.Key + " " + (g.Last().EndEquity - g.First().StartEquity).ToString("+#,0;−#,0;0", IC))));
        }
    }
    static int Streak(HedgeResult r) { int best = 0, cur = 0; foreach (var c in r.Cycles) { cur = c.Reason == "TARGET" ? 0 : cur + 1; best = Math.Max(best, cur); } return best; }
}
