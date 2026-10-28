// ORB + VWAP + 5-MINUTE FVG on MNQ (user's idea): the opening range (09:30–09:45 / 10:00 New York) breaks (a 5-minute close beyond it);
// then the first 5-minute FVG in the break's direction (a gap between candle 1 and candle 3) is the setup; a limit at its edge or its middle
// (fills only when price trades 1 tick through), stop beyond the FVG's far edge / the range's middle / the VWAP, target in R or the 15:55 close.
// Filters (on / off): the price on the VWAP's side at the break and the FVG beyond the VWAP; the previous day's value area (buy above its high,
// sell below its low). One trade a day. Costs $1.74 a contract + 1 tick on stops; a minute that touches stop and target = stop.
// Honesty: ranked on 2020–2023, shown on 2024–2026; benchmarks on the same days: the plain ORB break (market) and a RANDOM LIMIT (a random
// 5-minute close after the break, a limit 8 points better, the same stop / target rule).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class OrbFvgStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class V { public int Orb, Wait, Stop, Tgt, End, Kind; public bool FV, FP; public double Depth; public string Key; }   // Kind 0 = ORB+FVG, 1 = plain break, 2 = random limit
    sealed class Tr { public int Year; public double R, Usd; }
    sealed class Day { public RDay D; public double[] Vw; public double Vah = double.NaN, Val = double.NaN; public double Vah0 = double.NaN, Val0 = double.NaN; }
    const double Tick = 0.25, Pv = 2, Cost = 1.74;

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var raw = R.Load(folder, "MNQ", true).OrderBy(d => d.Day).ToList(); var days = new List<Day>(); Day prev = null; int a0 = R.S(9, 30), a1 = R.S(16, 0);
        foreach (var d in raw)
        {
            var x = new Day { D = d, Vw = new double[R.Slots] }; double pvs = 0, vol = 0;
            for (int k = 0; k < R.Slots; k++) { if (k >= a0 && k < a1 && d.Has(k)) { double w = Math.Max(1, d.V[k]); pvs += (d.H[k] + d.L[k] + d.C[k]) / 3 * w; vol += w; } x.Vw[k] = vol > 0 ? pvs / vol : double.NaN; }
            if (prev != null) { x.Vah = prev.Vah0; x.Val = prev.Val0; }
            var prof = new Dictionary<long, double>(); for (int k = a0; k < a1; k++) if (d.Has(k)) { long b = (long)Math.Floor(d.C[k] / 2.0); double cur; prof.TryGetValue(b, out cur); prof[b] = cur + Math.Max(1, d.V[k]); }
            if (prof.Count > 5)
            {
                var keys = prof.Keys.OrderBy(b => b).ToList(); long poc = prof.OrderByDescending(kv => kv.Value).First().Key; double tot = prof.Values.Sum(), inVa = prof[poc]; int lo = keys.IndexOf(poc), hi = lo;
                while (inVa < tot * 0.7 && (lo > 0 || hi < keys.Count - 1)) { double up = hi < keys.Count - 1 ? prof[keys[hi + 1]] : -1, dn = lo > 0 ? prof[keys[lo - 1]] : -1; if (up >= dn) { hi++; inVa += up; } else { lo--; inVa += dn; } }
                x.Vah0 = (keys[hi] + 1) * 2.0; x.Val0 = keys[lo] * 2.0;
            }
            days.Add(x); prev = x;
        }
        var vs = new List<V>();
        foreach (var orb in new[] { 15, 30 }) foreach (var fv in new[] { false, true }) foreach (var fp in new[] { false, true }) foreach (var wait in new[] { 12, 36 }) foreach (var dp in new[] { 0.0, 0.5 })
            foreach (var st in new[] { 0, 1, 2 }) foreach (var tg in new[] { 1, 2, 3, 4, 0 }) foreach (var end in new[] { 12, 15 })
            { if (st == 2 && !fv) continue; vs.Add(new V { Kind = 0, Orb = orb, FV = fv, FP = fp, Wait = wait, Depth = dp, Stop = st, Tgt = tg, End = end }); }
        foreach (var orb in new[] { 15, 30 }) foreach (var fv in new[] { false, true }) foreach (var fp in new[] { false, true }) foreach (var st in new[] { 1, 2 }) foreach (var tg in new[] { 1, 2, 3, 4, 0 }) foreach (var end in new[] { 12, 15 })
            { if (st == 2 && !fv) continue; vs.Add(new V { Kind = 1, Orb = orb, FV = fv, FP = fp, Stop = st, Tgt = tg, End = end }); vs.Add(new V { Kind = 2, Orb = orb, FV = fv, FP = fp, Stop = st, Tgt = tg, End = end, Wait = 36 }); }
        foreach (var v in vs) v.Key = new[] { "ORB+FVG", "PLAIN BREAK", "RANDOM LIMIT" }[v.Kind] + (v.FV ? " +VWAP" : "") + (v.FP ? " +profile" : "") + " • ORB " + v.Orb + (v.Kind == 0 ? " • FVG within " + (v.Wait == 12 ? "1h" : "3h") + " • entry " + (v.Depth == 0 ? "edge" : "50%") : "")
            + " • stop " + new[] { "FVG edge", "ORB middle", "VWAP" }[v.Stop] + " • target " + new[] { "15:55", "1R", "1.5R", "2R", "3R" }[v.Tgt] + " • until " + v.End + ":00";
        var res = new List<Tr>[vs.Count];
        Parallel.For(0, vs.Count, i => { var l = new List<Tr>(); foreach (var d in days) { var t = Trade(d, vs[i]); if (t != null) l.Add(t); } res[i] = l; });
        Func<List<Tr>, Func<Tr, bool>, double[]> stat = (l, f) => { var s = l.Where(f).ToList(); if (s.Count == 0) return new double[6]; double net = s.Sum(t => t.Usd), gw = s.Where(t => t.Usd > 0).Sum(t => t.Usd), gl = -s.Where(t => t.Usd < 0).Sum(t => t.Usd); double eq = 0, pk = 0, dd = 0; foreach (var t in s) { eq += t.Usd; pk = Math.Max(pk, eq); dd = Math.Max(dd, pk - eq); } return new double[] { s.Count, 100.0 * s.Count(t => t.R > 0) / s.Count, net, gl > 0 ? gw / gl : 0, dd, s.Average(t => t.R) }; };
        var rows = Enumerable.Range(0, vs.Count).Select(i => Tuple.Create(vs[i], stat(res[i], t => t.Year <= 2023), stat(res[i], t => t.Year >= 2024), res[i])).ToList();
        var md = new StringBuilder("# MNQ: ORB break + VWAP + 5-minute FVG (and the volume profile)\n\nPicked on 2020–2023 (IS), checked on 2024–2026 (OOS). 1 MNQ contract, $ after costs. avg R = per trade after costs.\n\n");
        Action<string, Func<V, bool>> table = (title, f) =>
        {
            var c = rows.Where(r => f(r.Item1) && r.Item2[0] >= 100).OrderByDescending(r => r.Item2[2] / Math.Max(1, r.Item2[4])).ToList();
            md.AppendLine("## " + title + "\n\n" + c.Count(r => r.Item2[2] > 0) + " of " + c.Count + " made money in-sample; of the best 20 in-sample, " + c.Take(20).Count(r => r.Item3[2] > 0) + " also made money out-of-sample. Average OOS of the best 20: " + (c.Count == 0 ? 0 : c.Take(20).Average(r => r.Item3[2])).ToString("N0", IC) + " $.\n");
            md.AppendLine("| rule | IS trades | IS win | IS avg R | IS net | IS DD | OOS trades | OOS win | OOS avg R | OOS net | OOS DD |"); md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in c.Take(12)) md.AppendLine("| " + r.Item1.Key + " | " + r.Item2[0] + " | " + r.Item2[1].ToString("0", IC) + "% | " + r.Item2[5].ToString("0.00", IC) + " | " + r.Item2[2].ToString("N0", IC) + " | " + r.Item2[4].ToString("N0", IC) + " | " + r.Item3[0] + " | " + r.Item3[1].ToString("0", IC) + "% | " + r.Item3[5].ToString("0.00", IC) + " | " + r.Item3[2].ToString("N0", IC) + " | " + r.Item3[4].ToString("N0", IC) + " |");
            md.AppendLine();
        };
        table("ORB + FVG (the setup)", v => v.Kind == 0); table("Benchmark: plain ORB break (market)", v => v.Kind == 1); table("Benchmark: random limit after the break", v => v.Kind == 2);
        // the setup vs the random limit with the same filters / stop / target (ORB middle and VWAP stops exist in both)
        var pairs = rows.Where(r => r.Item1.Kind == 0 && r.Item1.Stop > 0 && r.Item2[0] >= 100).Select(r => Tuple.Create(r, rows.First(q => q.Item1.Kind == 2 && q.Item1.Orb == r.Item1.Orb && q.Item1.FV == r.Item1.FV && q.Item1.FP == r.Item1.FP && q.Item1.Stop == r.Item1.Stop && q.Item1.Tgt == r.Item1.Tgt && q.Item1.End == r.Item1.End))).ToList();
        md.AppendLine("## Does the FVG add anything?\n\nSame filters, stop and target: the FVG entry beat the random limit in " + pairs.Count(p => p.Item1.Item3[5] > p.Item2.Item3[5]) + " of " + pairs.Count + " pairs out-of-sample (avg R " + pairs.Average(p => p.Item1.Item3[5]).ToString("0.000", IC) + " vs " + pairs.Average(p => p.Item2.Item3[5]).ToString("0.000", IC) + "), in-sample " + pairs.Count(p => p.Item1.Item2[5] > p.Item2.Item2[5]) + " of " + pairs.Count + ".\n");
        foreach (var f in new[] { "VWAP", "profile" })
        {
            var a = rows.Where(r => r.Item1.Kind == 0 && r.Item2[0] >= 100 && (f == "VWAP" ? r.Item1.FV : r.Item1.FP)).ToList(); var b = rows.Where(r => r.Item1.Kind == 0 && r.Item2[0] >= 100 && !(f == "VWAP" ? r.Item1.FV : r.Item1.FP)).ToList();
            md.AppendLine("- **" + f + " filter**: on avg R " + a.Average(r => r.Item2[5]).ToString("0.000", IC) + " → OOS " + a.Average(r => r.Item3[5]).ToString("0.000", IC) + " • off " + b.Average(r => r.Item2[5]).ToString("0.000", IC) + " → OOS " + b.Average(r => r.Item3[5]).ToString("0.000", IC));
        }
        File.WriteAllText(Path.Combine(outDir, "orbfvg.md"), md.ToString());
        File.WriteAllText(Path.Combine(outDir, "orbfvg.json"), "{\"rows\":[" + string.Join(",", rows.Select(r => "{\"key\":\"" + r.Item1.Key + "\",\"kind\":" + r.Item1.Kind + ",\"is\":[" + string.Join(",", r.Item2.Select(x => Math.Round(x, 3).ToString(IC))) + "],\"oos\":[" + string.Join(",", r.Item3.Select(x => Math.Round(x, 3).ToString(IC))) + "],\"years\":{" + string.Join(",", r.Item4.GroupBy(t => t.Year).OrderBy(g => g.Key).Select(g => "\"" + g.Key + "\":" + Math.Round(g.Sum(t => t.Usd)))) + "}}")) + "]}");
        Console.WriteLine("done");
    }

    static Tr Trade(Day x, V v)
    {
        var d = x.D; int o0 = R.S(9, 30), o1 = o0 + v.Orb, end = R.S(v.End, 0), flat = R.S(15, 55);
        double hi = d.Hi(o0, o1 - 1), lo = d.Lo(o0, o1 - 1); if (double.IsNaN(hi) || double.IsNaN(lo) || hi - lo < 2) return null;
        // 5-minute candles from 09:30 (slot 930 is a multiple of 5 on the grid)
        Func<int, double[]> bar = b0 => { double h = double.NaN, l = double.NaN, c = double.NaN; for (int k = b0; k < b0 + 5; k++) if (d.Has(k)) { h = double.IsNaN(h) ? d.H[k] : Math.Max(h, d.H[k]); l = double.IsNaN(l) ? d.L[k] : Math.Min(l, d.L[k]); c = d.C[k]; } return new[] { h, l, c }; };
        int dir = 0, brk = -1;
        for (int b0 = o1; b0 + 4 < end; b0 += 5) { var b = bar(b0); if (double.IsNaN(b[2])) continue; if (b[2] > hi) { dir = 1; brk = b0 + 4; break; } if (b[2] < lo) { dir = -1; brk = b0 + 4; break; } }
        if (brk < 0) return null;
        double vwB = x.Vw[brk], cB = d.C[brk];
        if (v.FV && (double.IsNaN(vwB) || (dir > 0 ? cB <= vwB : cB >= vwB))) return null;
        if (v.FP && (double.IsNaN(x.Vah) || (dir > 0 ? cB <= x.Vah : cB >= x.Val))) return null;
        double entry; int fill = -1; double fTop = 0, fBot = 0;
        if (v.Kind == 1) { fill = brk + 1; while (fill < end && !d.Has(fill)) fill++; if (fill >= end) return null; entry = d.O[fill] + dir * Tick; }
        else
        {
            int from; double lim;
            if (v.Kind == 0)
            {
                // the first FVG in the break's direction, its 3rd candle ending after the break, within Wait 5-min candles
                int found = -1; int lastB = Math.Min(end - 5, brk + 1 + v.Wait * 5);
                for (int b0 = o0 + 10; b0 <= lastB; b0 += 5)
                {
                    if (b0 + 4 <= brk) continue; var c1 = bar(b0 - 10); var c3 = bar(b0); if (double.IsNaN(c1[2]) || double.IsNaN(c3[2])) continue;
                    if (dir > 0 && c3[1] > c1[0]) { fTop = c3[1]; fBot = c1[0]; } else if (dir < 0 && c3[0] < c1[1]) { fTop = c1[1]; fBot = c3[0]; } else continue;
                    if (v.FV && !double.IsNaN(x.Vw[b0 + 4]) && (dir > 0 ? fBot <= x.Vw[b0 + 4] : fTop >= x.Vw[b0 + 4])) continue;
                    found = b0 + 4; break;
                }
                if (found < 0) return null; from = found + 1; lim = dir > 0 ? fTop - v.Depth * (fTop - fBot) : fBot + v.Depth * (fTop - fBot);
            }
            else { int span = Math.Max(1, (end - brk - 5) / 5); int k5 = brk + 1 + 5 * (int)((uint)(d.Day.GetHashCode() * 2654435761u) % (uint)span); if (k5 >= end || !d.Has(k5)) return null; from = k5 + 1; lim = d.C[k5] - dir * 8; }
            for (int k = from; k < end; k++) { if (!d.Has(k)) continue; if (dir > 0 ? d.L[k] <= lim - Tick : d.H[k] >= lim + Tick) { fill = k; break; } if (v.Kind == 0 && (dir > 0 ? d.C[k] < fBot : d.C[k] > fTop)) return null; }
            if (fill < 0) return null; entry = lim;
        }
        double stop = v.Stop == 0 ? (dir > 0 ? fBot : fTop) - dir * Tick : v.Stop == 1 ? (hi + lo) / 2 : x.Vw[fill] - dir * Tick;
        double risk = (entry - stop) * dir; if (double.IsNaN(risk) || risk < 1) return null;
        double tR = new[] { 0, 1, 1.5, 2, 3 }[v.Tgt]; double tgt = tR > 0 ? entry + dir * tR * risk : double.NaN; double pts = double.NaN;
        if (v.Kind != 1 && (dir > 0 ? d.L[fill] <= stop : d.H[fill] >= stop)) pts = -risk - Tick;
        for (int k = v.Kind == 1 ? fill : fill + 1; double.IsNaN(pts) && k <= flat; k++)
        {
            if (!d.Has(k)) continue;
            if (dir > 0 ? d.L[k] <= stop : d.H[k] >= stop) { pts = -risk - Tick; break; }
            if (!double.IsNaN(tgt) && (dir > 0 ? d.H[k] >= tgt : d.L[k] <= tgt)) { pts = tR * risk; break; }
        }
        if (double.IsNaN(pts)) pts = (d.CloseAt(flat) - entry) * dir;
        double usd = pts * Pv - Cost; return new Tr { Year = d.Day.Year, R = usd / (risk * Pv), Usd = usd };
    }
}
