// VWAP + 5-MINUTE FVG for a LIVE account (no prop rules): every combination of session, side, VWAP filter, entry depth in the FVG, how long
// the FVG waits, stop type and target, traded with a FIXED $ RISK per trade (500 / 1,000 / 1,500 / 2,000): the contracts = risk ÷ (stop × $ per point),
// rounded down, so a tight stop trades many contracts and a wide one few.
// Honest fills: a limit fills only when price trades 1 tick THROUGH it; a stop costs 1 tick of slippage; the entry minute counts only against us; a minute
// that touches both the stop and the target = the stop; the FVG dies when a 1-minute candle closes beyond its far edge; flat at 16:55 New York.
// One trade at a time per variant. Costs per contract: MNQ $1.74, MGC $2.50. Output: vwapfvg_live.json (every variant) for the HTML report.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class VwapFvgLive
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class Fvg { public int End, Dir; public double Top, Bot, Vw; public bool UnderVwap, OverVwap; }
    sealed class DayData { public RDay D; public double[] Vw; public List<Fvg> F = new List<Fvg>(); public List<int> Ends = new List<int>(); public int Month, Year; }
    sealed class V { public bool Random; public double LimitOff; public int Dir, S0, S1, Expire, StopKind, TgtKind; public string Session, StopName, TgtName; public bool UseVwap; public double Depth, StopPts, Tgt; }
    static readonly double[] Risks = { 500, 1000, 1500, 2000 };

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var sessions = new[] { Tuple.Create("ASIA 18:00-02:00", R.S(18, 0), R.S(2, 0)), Tuple.Create("LONDON 02:00-08:00", R.S(2, 0), R.S(8, 0)), Tuple.Create("NY OPEN 09:30-11:00", R.S(9, 30), R.S(11, 0)),
            Tuple.Create("NY AM 09:30-12:00", R.S(9, 30), R.S(12, 0)), Tuple.Create("NY PM 12:00-16:00", R.S(12, 0), R.S(16, 0)), Tuple.Create("NY 09:30-16:00", R.S(9, 30), R.S(16, 0)), Tuple.Create("ALL 18:00-16:00", 0, R.S(16, 0)) };
        var sb = new StringBuilder("{\"risks\":[500,1000,1500,2000],\"rows\":["); bool firstRow = true; var curves = new StringBuilder(); bool firstCurve = true;
        foreach (var sym in new[] { "MNQ", "MGC" })
        {
            double pv = sym == "MNQ" ? 2 : 10, cost = sym == "MNQ" ? 1.74 : 2.5, tick = sym == "MNQ" ? 0.25 : 0.1, buf = sym == "MNQ" ? 2 : 0.5; int cap = 200;
            var fixedStops = sym == "MNQ" ? new double[] { 10, 20, 30 } : new double[] { 2, 4, 6 };
            var tgtPts = sym == "MNQ" ? new double[] { 10, 15, 20, 30, 40 } : new double[] { 3, 5, 10, 15, 20 };
            var days = R.Load(folder, sym, true).OrderBy(d => d.Day).ToList();
            var dd = days.Select(Prep).ToList();
            var months = dd.Select(x => x.Year * 100 + x.Month).Distinct().OrderBy(m => m).ToList(); var mIx = months.Select((m, i) => Tuple.Create(m, i)).ToDictionary(t => t.Item1, t => t.Item2);
            var years = dd.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
            var vs = new List<V>();
            foreach (var dir in new[] { -1, 1 }) foreach (var se in sessions) foreach (var uv in new[] { true, false }) foreach (var dp in new[] { 0.0, 0.25, 0.5 }) foreach (var ex in new[] { 12, 36 })
            {
                var stops = new List<Tuple<int, string, double>> { Tuple.Create(0, "VWAP", 0.0), Tuple.Create(1, "FVG edge", 0.0) }; foreach (var f in fixedStops) stops.Add(Tuple.Create(2, f + " pts", f));
                var tg = new List<Tuple<int, string, double>>(); foreach (var t in tgtPts) tg.Add(Tuple.Create(0, t + " pts", t)); foreach (var r in new[] { 1.0, 1.5, 2, 3 }) tg.Add(Tuple.Create(1, r + "R", r));
                foreach (var st in stops) foreach (var t in tg)
                {
                    if (!uv && st.Item1 == 0) continue;   // a VWAP stop needs the VWAP side rule
                    vs.Add(new V { Dir = dir, Session = se.Item1, S0 = se.Item2, S1 = se.Item3, UseVwap = uv, Depth = dp, Expire = ex, StopKind = st.Item1, StopName = st.Item2, StopPts = st.Item3, TgtKind = t.Item1, TgtName = t.Item2, Tgt = t.Item3 });
                }
            }
            foreach (var dir in new[] { -1, 1 }) foreach (var se in sessions) foreach (var uv in new[] { true, false })
            {
                var stops = new List<Tuple<int, string, double>>(); if (uv) stops.Add(Tuple.Create(0, "VWAP", 0.0)); foreach (var f in fixedStops) stops.Add(Tuple.Create(2, f + " pts", f));
                var tg = new List<Tuple<int, string, double>>(); foreach (var t in tgtPts) tg.Add(Tuple.Create(0, t + " pts", t)); foreach (var r in new[] { 1.0, 1.5, 2, 3 }) tg.Add(Tuple.Create(1, r + "R", r));
                foreach (var st in stops) foreach (var t in tg) foreach (var lo in new[] { 0.0, sym == "MNQ" ? 10.0 : 2.0 }) vs.Add(new V { Random = true, LimitOff = lo, Dir = dir, Session = se.Item1, S0 = se.Item2, S1 = se.Item3, UseVwap = uv, Depth = -1, Expire = 0, StopKind = st.Item1, StopName = st.Item2, StopPts = st.Item3, TgtKind = t.Item1, TgtName = t.Item2, Tgt = t.Item3 });
            }
            Console.WriteLine(sym + ": " + vs.Count + " variants over " + dd.Count + " days");
            var rows = new string[vs.Count]; var mon = new double[vs.Count][]; var score = new double[vs.Count]; int done = 0;
            Parallel.For(0, vs.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var v = vs[i]; int n = 0, wins = 0, skipped = 0; double sumR = 0, gw = 0, gl = 0; int streak = 0, worstStreak = 0;
                var net = new double[Risks.Length]; var peak = new double[Risks.Length]; var eq = new double[Risks.Length]; var mdd = new double[Risks.Length]; var cts = new double[Risks.Length];
                var yr = new Dictionary<int, double[]>(); var mR = new double[months.Count]; var m1k = new double[months.Count]; double riskPtsSum = 0;
                foreach (var d in dd)
                {
                    foreach (var t in Trades(d, v, buf, tick))
                    {
                        n++; sumR += t.Item1; riskPtsSum += t.Item3; if (t.Item1 > 0) { wins++; streak = 0; } else { streak++; worstStreak = Math.Max(worstStreak, streak); }
                        double[] y; if (!yr.TryGetValue(d.Year, out y)) { y = new double[2 + Risks.Length]; yr[d.Year] = y; } y[0]++; if (t.Item1 > 0) y[1]++;
                        mR[mIx[d.Year * 100 + d.Month]] += t.Item1;
                        for (int k = 0; k < Risks.Length; k++)
                        {
                            int c = (int)Math.Min(cap, Math.Floor(Risks[k] / (t.Item3 * pv))); if (c <= 0) { if (k == 1) skipped++; continue; }
                            double usd = c * (t.Item2 * pv - cost); net[k] += usd; eq[k] += usd; peak[k] = Math.Max(peak[k], eq[k]); mdd[k] = Math.Max(mdd[k], peak[k] - eq[k]); cts[k] += c; y[2 + k] += usd;
                            if (k == 1) { m1k[mIx[d.Year * 100 + d.Month]] += usd; if (usd > 0) gw += usd; else gl -= usd; }
                        }
                    }
                }
                mon[i] = m1k; score[i] = v.Random ? double.MinValue : net[1];
                int posYears = yr.Count(kv => kv.Value[3] > 0), posMonths = m1k.Count(x => x > 0), anyMonths = m1k.Count(x => x != 0);
                var s = new StringBuilder();
                s.Append("{\"sig\":\"" + (v.Random ? (v.LimitOff > 0 ? "RANDOM LIMIT" : "RANDOM") : "FVG") + "\",\"sym\":\"" + sym + "\",\"side\":\"" + (v.Dir < 0 ? "SELL" : "BUY") + "\",\"ses\":\"" + v.Session + "\",\"vwap\":" + (v.UseVwap ? 1 : 0) + ",\"depth\":" + v.Depth.ToString(IC) + ",\"exp\":" + v.Expire + ",\"stop\":\"" + v.StopName + "\",\"tgt\":\"" + v.TgtName + "\"");
                s.Append(",\"n\":" + n + ",\"win\":" + (n == 0 ? 0 : Math.Round(100.0 * wins / n, 1)).ToString(IC) + ",\"avgR\":" + (n == 0 ? 0 : Math.Round(sumR / n, 3)).ToString(IC) + ",\"pf\":" + (gl > 0 ? Math.Round(gw / gl, 2) : 0).ToString(IC) + ",\"risk\":" + (n == 0 ? 0 : Math.Round(riskPtsSum / n, 2)).ToString(IC));
                s.Append(",\"streak\":" + worstStreak + ",\"posYears\":" + posYears + ",\"nYears\":" + yr.Count + ",\"posMonths\":" + posMonths + ",\"nMonths\":" + anyMonths + ",\"skip\":" + skipped + ",\"perMonth\":" + (months.Count == 0 ? 0 : Math.Round((double)n / months.Count, 1)).ToString(IC));
                s.Append(",\"net\":[" + string.Join(",", net.Select(x => Math.Round(x).ToString(IC))) + "],\"dd\":[" + string.Join(",", mdd.Select(x => Math.Round(x).ToString(IC))) + "],\"cts\":[" + string.Join(",", cts.Select(x => (n == 0 ? 0 : Math.Round(x / n, 1)).ToString(IC))) + "]");
                s.Append(",\"years\":{" + string.Join(",", yr.OrderBy(kv => kv.Key).Select(kv => "\"" + kv.Key + "\":[" + kv.Value[0] + "," + (kv.Value[0] == 0 ? 0 : Math.Round(100 * kv.Value[1] / kv.Value[0], 1)).ToString(IC) + "," + string.Join(",", kv.Value.Skip(2).Select(x => Math.Round(x).ToString(IC))) + "]")) + "}}");
                rows[i] = s.ToString();
                int dn = System.Threading.Interlocked.Increment(ref done); if (dn % 2000 == 0) Console.WriteLine("  " + dn + " / " + vs.Count);
            });
            // monthly curves ($1,000 risk) for the best 600 variants of this instrument + every variant's index into them
            var best = Enumerable.Range(0, vs.Count).OrderByDescending(i => score[i]).Take(600).ToList(); var curveOf = new Dictionary<int, int>();
            foreach (var i in best) { if (!firstCurve) curves.Append(","); firstCurve = false; curveOf[i] = curveCount++; curves.Append("[" + string.Join(",", mon[i].Select(x => Math.Round(x).ToString(IC))) + "]"); }
            monthsBy[sym] = months;
            for (int i = 0; i < vs.Count; i++) { if (!firstRow) sb.Append(","); firstRow = false; sb.Append(rows[i].Substring(0, rows[i].Length - 1) + ",\"curve\":" + (curveOf.ContainsKey(i) ? curveOf[i] : -1) + "}"); }
        }
        sb.Append("],\"months\":{" + string.Join(",", monthsBy.Select(kv => "\"" + kv.Key + "\":[" + string.Join(",", kv.Value.Select(m => "\"" + (m / 100) + "-" + (m % 100).ToString("00") + "\"")) + "]")) + "},\"curves\":[" + curves + "]}");
        File.WriteAllText(Path.Combine(outDir, "vwapfvg_live.json"), sb.ToString());
        Console.WriteLine("done");
    }
    static int curveCount; static readonly Dictionary<string, List<int>> monthsBy = new Dictionary<string, List<int>>();

    static DayData Prep(RDay d)
    {
        var x = new DayData { D = d, Year = d.Day.Year, Month = d.Day.Month, Vw = new double[R.Slots] }; double pv = 0, vol = 0;
        for (int k = 0; k < R.Slots; k++) { if (d.Has(k)) { double w = Math.Max(1, d.V[k]); pv += (d.H[k] + d.L[k] + d.C[k]) / 3.0 * w; vol += w; } x.Vw[k] = vol > 0 ? pv / vol : double.NaN; }
        int nb = R.Slots / 5; var bh = new double[nb]; var bl = new double[nb]; var bc = new double[nb]; var ok = new bool[nb];
        for (int b = 0; b < nb; b++) { double hi = double.NaN, lo = double.NaN, c = double.NaN; for (int k = b * 5; k < b * 5 + 5; k++) if (d.Has(k)) { hi = double.IsNaN(hi) ? d.H[k] : Math.Max(hi, d.H[k]); lo = double.IsNaN(lo) ? d.L[k] : Math.Min(lo, d.L[k]); c = d.C[k]; } bh[b] = hi; bl[b] = lo; bc[b] = c; ok[b] = !double.IsNaN(c); }
        for (int b = 2; b < nb; b++)
        {
            if (!ok[b] || !ok[b - 1] || !ok[b - 2]) continue; int end = b * 5 + 4; double v0 = x.Vw[end]; if (double.IsNaN(v0)) continue; if (d.Has(end)) x.Ends.Add(end);
            if (bl[b - 2] > bh[b]) x.F.Add(new Fvg { End = end, Dir = -1, Top = bl[b - 2], Bot = bh[b], Vw = v0, UnderVwap = bc[b] < v0 && bl[b - 2] < v0 });
            if (bh[b - 2] < bl[b]) x.F.Add(new Fvg { End = end, Dir = 1, Bot = bh[b - 2], Top = bl[b], Vw = v0, OverVwap = bc[b] > v0 && bh[b - 2] > v0 });
        }
        return x;
    }

    // one day of one variant: (R, points, risk in points) per trade
    static IEnumerable<Tuple<double, double, double>> Trades(DayData x, V v, double buf, double tick)
    {
        var d = x.D; int flat = R.S(16, 55); int busy = -1;
        if (v.Random)
        {
            foreach (var end in x.Ends)
            {
                if (end < v.S0 || end >= v.S1 || end <= busy) continue; double e = d.C[end], v0 = x.Vw[end]; int dir = v.Dir;
                if (v.UseVwap && !(dir < 0 ? e < v0 : e > v0)) continue;
                if (v.LimitOff > 0)
                {
                    // a limit LimitOff points better than the 5-min close, waiting 3 hours: the same kind of entry as the FVG's, at random times
                    double le = e - dir * v.LimitOff; int lf = -1;
                    for (int k = end + 1; k <= Math.Min(v.S1, end + 180) && k < R.Slots; k++) { if (!d.Has(k)) continue; if (dir < 0 ? d.H[k] >= le + tick : d.L[k] <= le - tick) { lf = k; break; } }
                    if (lf < 0) continue;
                    double ls = v.StopKind == 0 ? (dir < 0 ? Math.Max(x.Vw[lf], v0) + buf : Math.Min(x.Vw[lf], v0) - buf) : le - dir * v.StopPts; double lr = (ls - le) * -dir; if (lr < tick * 2) continue;
                    double ltp = v.TgtKind == 0 ? v.Tgt : v.Tgt * lr, ltg = le + dir * ltp; double lp = double.NaN; int lo = flat;
                    if (dir < 0 ? d.H[lf] >= ls : d.L[lf] <= ls) { lp = -lr - tick; lo = lf; }
                    else for (int k = lf + 1; k <= flat && k < R.Slots; k++)
                    {
                        if (!d.Has(k)) continue;
                        if (dir < 0 ? d.H[k] >= ls : d.L[k] <= ls) { lp = -lr - tick; lo = k; break; }
                        if (dir < 0 ? d.L[k] <= ltg : d.H[k] >= ltg) { lp = ltp; lo = k; break; }
                    }
                    if (double.IsNaN(lp)) lp = (d.CloseAt(flat) - le) * dir;
                    busy = lo; yield return Tuple.Create(lp / lr, lp, lr); continue;
                }
                double stop = v.StopKind == 0 ? v0 + (dir < 0 ? buf : -buf) : e - dir * v.StopPts; double risk = (stop - e) * -dir; if (risk < tick * 2) continue;
                double tp = v.TgtKind == 0 ? v.Tgt : v.Tgt * risk, tgt = e + dir * tp; double pts = double.NaN; int outK = flat;
                for (int k = end + 1; k <= flat && k < R.Slots; k++)
                {
                    if (!d.Has(k)) continue;
                    if (dir < 0 ? d.H[k] >= stop : d.L[k] <= stop) { pts = -risk - tick; outK = k; break; }
                    if (dir < 0 ? d.L[k] <= tgt : d.H[k] >= tgt) { pts = tp; outK = k; break; }
                }
                if (double.IsNaN(pts)) pts = (d.CloseAt(flat) - e) * dir;
                busy = outK; yield return Tuple.Create(pts / risk, pts, risk);
            }
            yield break;
        }
        foreach (var f in x.F)
        {
            if (f.Dir != v.Dir || f.End < v.S0 || f.End >= v.S1 || f.End <= busy) continue;
            if (v.UseVwap && !(v.Dir < 0 ? f.UnderVwap : f.OverVwap)) continue;
            int dir = v.Dir; double entry = dir < 0 ? f.Bot + v.Depth * (f.Top - f.Bot) : f.Top - v.Depth * (f.Top - f.Bot);
            int fill = -1, last = Math.Min(v.S1, f.End + v.Expire * 5);
            for (int k = f.End + 1; k <= last && k < R.Slots; k++)
            {
                if (!d.Has(k)) continue;
                if (dir < 0 ? d.H[k] >= entry + tick : d.L[k] <= entry - tick) { fill = k; break; }
                if (dir < 0 ? d.C[k] > f.Top : d.C[k] < f.Bot) break;   // closed through the gap: it is gone
            }
            if (fill < 0) continue;
            double stop;
            if (v.StopKind == 0) { double vf = x.Vw[fill]; stop = dir < 0 ? Math.Max(vf, f.Vw) + buf : Math.Min(vf, f.Vw) - buf; }
            else if (v.StopKind == 1) stop = dir < 0 ? f.Top + buf : f.Bot - buf;
            else stop = entry - dir * v.StopPts;
            double risk = (stop - entry) * -dir; if (risk < tick * 2) continue;
            double tp = v.TgtKind == 0 ? v.Tgt : v.Tgt * risk; double tgt = entry + dir * tp;
            double pts; int outK;
            if (dir < 0 ? d.H[fill] >= stop : d.L[fill] <= stop) { pts = -risk - tick; outK = fill; }
            else
            {
                pts = double.NaN; outK = flat;
                for (int k = fill + 1; k <= flat && k < R.Slots; k++)
                {
                    if (!d.Has(k)) continue;
                    if (dir < 0 ? d.H[k] >= stop : d.L[k] <= stop) { pts = -risk - tick; outK = k; break; }
                    if (dir < 0 ? d.L[k] <= tgt : d.H[k] >= tgt) { pts = tp; outK = k; break; }
                }
                if (double.IsNaN(pts)) { double c = d.CloseAt(flat); pts = (c - entry) * dir; }
            }
            busy = outK;
            yield return Tuple.Create(pts / risk, pts, risk);
        }
    }
}
