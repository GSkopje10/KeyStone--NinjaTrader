// the wide search: every lot / lookback / target / distance, a fresh account in each 4-week window (every Monday), ranked by the median result
using System; using System.Collections.Generic; using System.Linq; using System.Globalization; using System.Threading.Tasks; using KeystoneMT5;
public static class Sweep
{
    public static void Run(List<Mt5Bar> bars, DateTime from, DateTime to, Func<string, double, double, double, int, HedgeSettings> mk, double bal, double[] lots, double daily)
    {
        var IC = CultureInfo.InvariantCulture; var sets = new List<HedgeSettings>();
        foreach (var lot in lots) foreach (var look in new[] { 5, 10, 20, 30, 60, 120, 240 }) foreach (var tgt in new[] { 10.0, 20, 40, 80 }) foreach (var md in new[] { 0.0, 3, 8 })
        { var s = mk("", bal, lot, Math.Round(tgt * lot / 0.1, 2), look); s.MinDist = md; s.DailyTarget = daily; s.Name = string.Format(IC, "${0} lot {1} look {2} tgt ${3} min${4}", bal, lot, look, s.Target, md); sets.Add(s); }
        var rows = new (HedgeSettings s, double blown, double prof, double med, double mean)[sets.Count];
        Parallel.For(0, sets.Count, i =>
        {
            var res = new List<HedgeResult>(); for (var st = from; st.AddDays(28) <= to; st = st.AddDays(7)) res.Add(HedgeLab.Run(bars, sets[i], st, st.AddDays(27)));
            var nets = res.Select(x => x.Net).OrderBy(x => x).ToList();
            rows[i] = (sets[i], 100.0 * res.Count(x => x.Blown) / res.Count, 100.0 * res.Count(x => !x.Blown && x.Net > 0) / res.Count, nets[nets.Count / 2], nets.Average());
        });
        Console.WriteLine("  " + rows.Count(r => r.mean > 0) + " of " + rows.Length + " settings made money on average over the 4-week windows; the best 12 by average:");
        foreach (var r in rows.OrderByDescending(r => r.mean).Take(12)) Console.WriteLine(string.Format(IC, "  {0,-44} blown {1,3:0}%  profitable {2,3:0}%  median {3,7:N0}  average {4,7:N0}", r.s.Name, r.blown, r.prof, r.med, r.mean));
    }
}
