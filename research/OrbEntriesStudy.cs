// MNQ ENTRY COMPARISON (user's request): which confirmation is worth waiting for, buys and sells apart, with the same risk management grid.
// Entries: ORB break only • ORB break + VWAP side • VWAP cross only • 5-min FVG only • ORB break + FVG • ORB + VWAP + FVG • VWAP + FVG
// • a RANDOM side at the ORB break's time (the benchmark: does the direction matter?). A break / cross = a 5-minute close; market entries at
// the next minute's open + 1 tick; FVG entries = a limit at the gap's near edge that fills only 1 tick through, cancelled on a close beyond
// its far edge. Stops: the range's other side / its middle / the signal candle's (or the FVG's) far end / the VWAP / 0.25 % / 0.5 % of price.
// Targets 1 / 1.5 / 2 / 3R or 15:55; management none / break-even at +1R / out on a 5-minute close through the VWAP / break-even + trail.
// One trade a day, entries until 12:00 or 15:00. Costs $1.74 + 1 tick on stops; a minute touching stop and target = stop.
// Picked on 2020–2023 (IS), shown on 2024–2026 (OOS). Writes orbentries.json (read by the HTML report).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class OrbEntriesStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class V { public int E, Orb, Stop, Tgt, Mg, End; }
    sealed class Tr { public DateTime Day; public int Dir; public double R, Usd; }
    sealed class Day { public RDay D; public double[] Vw; }
    sealed class Sig { public int Dir, Fill, From; public double Entry, Lo, Hi, SigLo, SigHi, Vw; public bool Limit; public double Cancel; }
    const double Tick = 0.25, Pv = 2, Cost = 1.74;

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var days = new List<Day>(); int a0 = R.S(9, 30), a1 = R.S(16, 0);
        foreach (var d in R.Load(folder, "MNQ", true).OrderBy(d => d.Day))
        {
            var x = new Day { D = d, Vw = new double[R.Slots] }; double pvs = 0, vol = 0;
            for (int k = 0; k < R.Slots; k++) { if (k >= a0 && k < a1 && d.Has(k)) { double w = Math.Max(1, d.V[k]); pvs += (d.H[k] + d.L[k] + d.C[k]) / 3 * w; vol += w; } x.Vw[k] = vol > 0 ? pvs / vol : double.NaN; }
            days.Add(x);
        }
        var vs = new List<V>();
        for (int e = 0; e < 8; e++) foreach (var orb in new[] { 15, 30 }) for (int st = 0; st < 6; st++) for (int tg = 0; tg < 5; tg++) for (int mg = 0; mg < 4; mg++) foreach (var end in new[] { 12, 15 })
            vs.Add(new V { E = e, Orb = orb, Stop = st, Tgt = tg, Mg = mg, End = end });
        var res = new List<Tr>[vs.Count];
        Parallel.For(0, vs.Count, i => { var l = new List<Tr>(); foreach (var d in days) { var t = Trade(d, vs[i]); if (t != null) l.Add(t); } res[i] = l; });

        // [trades, win %, net $, PF, max DD $, avg R]
        Func<IEnumerable<Tr>, double[]> stat = src =>
        {
            var s = src.ToList(); if (s.Count == 0) return new double[6];
            double gw = s.Where(t => t.Usd > 0).Sum(t => t.Usd), gl = -s.Where(t => t.Usd < 0).Sum(t => t.Usd), eq = 0, pk = 0, dd = 0;
            foreach (var t in s) { eq += t.Usd; pk = Math.Max(pk, eq); dd = Math.Max(dd, pk - eq); }
            return new[] { s.Count, 100.0 * s.Count(t => t.Usd > 0) / s.Count, s.Sum(t => t.Usd), gl > 0 ? gw / gl : 0, dd, s.Average(t => t.R) };
        };
        Func<double, string> n2 = v => Math.Round(v, 2).ToString(IC);
        Func<double[], string> arr = a => "[" + string.Join(",", a.Select(n2)) + "]";
        var years = Enumerable.Range(2020, 7).ToArray();
        var js = new StringBuilder("{\"years\":[" + string.Join(",", years) + "],\"rules\":[");
        for (int i = 0; i < vs.Count; i++)
        {
            var v = vs[i]; var l = res[i]; if (i > 0) js.Append(',');
            js.Append("{\"r\":[" + v.E + "," + v.Orb + "," + v.Stop + "," + v.Tgt + "," + v.Mg + "," + v.End + "]");
            foreach (var sd in new[] { 1, -1, 0 })
            {
                string nm = sd > 0 ? "b" : sd < 0 ? "s" : "a"; var ls = l.Where(t => sd == 0 || t.Dir == sd).ToList();
                js.Append(",\"" + nm + "\":{\"is\":" + arr(stat(ls.Where(t => t.Day.Year <= 2023))) + ",\"oos\":" + arr(stat(ls.Where(t => t.Day.Year >= 2024))) + ",\"y\":[" + string.Join(",", years.Select(y => Math.Round(ls.Where(t => t.Day.Year == y).Sum(t => t.Usd)).ToString(IC))) + "]}");
            }
            js.Append('}');
        }
        js.Append("],\"curves\":[");
        // equity curves: for every entry and side, the best rule of 2020–23 (net ÷ drawdown, ≥ 150 trades), one point a week
        bool firstC = true;
        for (int e = 0; e < 8; e++) foreach (var sd in new[] { 1, -1, 0 })
        {
            int best = -1; double bestS = double.NegativeInfinity;
            for (int i = 0; i < vs.Count; i++) { if (vs[i].E != e) continue; var s = stat(res[i].Where(t => t.Day.Year <= 2023 && (sd == 0 || t.Dir == sd))); if (s[0] < (sd == 0 ? 250 : 150)) continue; double sc = s[2] / Math.Max(500, s[4]); if (sc > bestS) { bestS = sc; best = i; } }
            if (best < 0) continue; var v = vs[best]; double eq = 0; var pts = new List<string>(); DateTime wk = DateTime.MinValue;
            foreach (var t in res[best].Where(t => sd == 0 || t.Dir == sd)) { eq += t.Usd; var w = t.Day.AddDays(-(int)t.Day.DayOfWeek); if (w != wk) { pts.Add("[\"" + t.Day.ToString("yyyy-MM-dd", IC) + "\"," + Math.Round(eq) + "]"); wk = w; } else pts[pts.Count - 1] = "[\"" + t.Day.ToString("yyyy-MM-dd", IC) + "\"," + Math.Round(eq) + "]"; }
            js.Append((firstC ? "" : ",") + "{\"e\":" + e + ",\"side\":\"" + (sd > 0 ? "b" : sd < 0 ? "s" : "a") + "\",\"rule\":" + best + ",\"pts\":[" + string.Join(",", pts) + "]}"); firstC = false;
        }
        js.Append("]}");
        File.WriteAllText(Path.Combine(outDir, "orbentries.json"), js.ToString());
        Console.WriteLine("done " + vs.Count + " rules");
    }

    static double[] Bar(RDay d, int b0) { double h = double.NaN, l = double.NaN, c = double.NaN; for (int k = b0; k < b0 + 5; k++) if (d.Has(k)) { h = double.IsNaN(h) ? d.H[k] : Math.Max(h, d.H[k]); l = double.IsNaN(l) ? d.L[k] : Math.Min(l, d.L[k]); c = d.C[k]; } return new[] { h, l, c }; }

    static Sig Signal(Day x, V v)
    {
        var d = x.D; int o0 = R.S(9, 30), o1 = o0 + v.Orb, end = R.S(v.End, 0);
        double hi = d.Hi(o0, o1 - 1), lo = d.Lo(o0, o1 - 1); if (double.IsNaN(hi) || double.IsNaN(lo) || hi - lo < 2) return null;
        var s = new Sig { Hi = hi, Lo = lo }; int brk = -1;
        bool needBreak = v.E <= 1 || v.E == 4 || v.E == 5 || v.E == 7, vwBreak = v.E == 1 || v.E == 5;
        if (needBreak)
        {
            for (int b0 = o1; b0 + 4 < end; b0 += 5)
            {
                var b = Bar(d, b0); if (double.IsNaN(b[2])) continue; double vw = x.Vw[b0 + 4];
                if (b[2] > hi && (!vwBreak || b[2] > vw)) { s.Dir = 1; brk = b0 + 4; s.SigLo = b[1]; s.SigHi = b[0]; break; }
                if (b[2] < lo && (!vwBreak || b[2] < vw)) { s.Dir = -1; brk = b0 + 4; s.SigLo = b[1]; s.SigHi = b[0]; break; }
            }
            if (brk < 0) return null;
            if (v.E == 7) s.Dir = ((uint)(d.Day.GetHashCode() * 2654435761u) >> 7) % 2 == 0 ? 1 : -1;   // the benchmark: a coin flip at the break's time
        }
        else if (v.E == 2)
        {
            // the first 5-minute close across the VWAP after the range (the close before on the other side)
            double pc = double.NaN, pv = double.NaN;
            for (int b0 = o0; b0 + 4 < end; b0 += 5)
            {
                var b = Bar(d, b0); if (double.IsNaN(b[2])) continue; double vw = x.Vw[b0 + 4];
                if (b0 >= o1 && !double.IsNaN(pc) && !double.IsNaN(vw))
                {
                    if (pc <= pv && b[2] > vw) { s.Dir = 1; brk = b0 + 4; s.SigLo = b[1]; s.SigHi = b[0]; break; }
                    if (pc >= pv && b[2] < vw) { s.Dir = -1; brk = b0 + 4; s.SigLo = b[1]; s.SigHi = b[0]; break; }
                }
                pc = b[2]; pv = vw;
            }
            if (brk < 0) return null;
        }
        if (v.E >= 3 && v.E <= 6)
        {
            // the first 5-minute FVG (candle 1 vs candle 3), its 3rd candle closing after the range (and after the break when there is one)
            bool fvVw = v.E == 5 || v.E == 6; int found = -1; double fTop = 0, fBot = 0, after = brk >= 0 ? brk : o1 - 1;
            for (int b0 = o0 + 10; b0 + 4 < end; b0 += 5)
            {
                if (b0 + 4 <= after) continue; var c1 = Bar(d, b0 - 10); var c3 = Bar(d, b0); if (double.IsNaN(c1[2]) || double.IsNaN(c3[2])) continue;
                int dir; if (c3[1] > c1[0]) { dir = 1; fTop = c3[1]; fBot = c1[0]; } else if (c3[0] < c1[1]) { dir = -1; fTop = c1[1]; fBot = c3[0]; } else continue;
                if (brk >= 0 && dir != s.Dir) continue;
                double vw = x.Vw[b0 + 4]; if (fvVw && (double.IsNaN(vw) || (dir > 0 ? fBot <= vw : fTop >= vw))) continue;
                s.Dir = dir; found = b0 + 4; break;
            }
            if (found < 0) return null;
            s.Limit = true; s.From = found + 1; s.Entry = s.Dir > 0 ? fTop : fBot; s.Cancel = s.Dir > 0 ? fBot : fTop; s.SigLo = fBot; s.SigHi = fTop; brk = found;
            s.Fill = -1;
            for (int k = s.From; k < end; k++) { if (!d.Has(k)) continue; if (s.Dir > 0 ? d.L[k] <= s.Entry - Tick : d.H[k] >= s.Entry + Tick) { s.Fill = k; break; } if (s.Dir > 0 ? d.C[k] < s.Cancel : d.C[k] > s.Cancel) return null; }
            if (s.Fill < 0) return null;
        }
        else
        {
            s.Fill = brk + 1; while (s.Fill < end && !d.Has(s.Fill)) s.Fill++; if (s.Fill >= end) return null;
            s.Entry = d.O[s.Fill] + s.Dir * Tick;
        }
        s.Vw = x.Vw[brk];
        return s;
    }

    static Tr Trade(Day x, V v)
    {
        var s = Signal(x, v); if (s == null) return null; var d = x.D; int dir = s.Dir, o0 = R.S(9, 30), flat = R.S(15, 55); double entry = s.Entry;
        double stop = v.Stop == 0 ? (dir > 0 ? s.Lo : s.Hi) : v.Stop == 1 ? (s.Hi + s.Lo) / 2 : v.Stop == 2 ? (dir > 0 ? s.SigLo - Tick : s.SigHi + Tick) : v.Stop == 3 ? s.Vw - dir * Tick : entry - dir * entry * (v.Stop == 4 ? 0.0025 : 0.005);
        double risk = (entry - stop) * dir; if (double.IsNaN(risk) || risk < 2) return null;
        double tR = new[] { 0, 1, 1.5, 2, 3 }[v.Tgt], tgt = tR > 0 ? entry + dir * tR * risk : double.NaN, pts = double.NaN, mfe = 0, cur = stop; bool be = false;
        for (int k = s.Fill; k <= flat; k++)
        {
            if (!d.Has(k)) continue;
            if (dir > 0 ? d.L[k] <= cur : d.H[k] >= cur) { pts = (cur - entry) * dir - Tick; break; }
            if (!double.IsNaN(tgt) && (dir > 0 ? d.H[k] >= tgt : d.L[k] <= tgt)) { pts = tR * risk; break; }
            mfe = Math.Max(mfe, ((dir > 0 ? d.H[k] : d.L[k]) - entry) * dir / risk);
            if ((v.Mg == 1 || v.Mg == 3) && !be && mfe >= 1) { be = true; cur = entry + dir * Tick; }
            if ((k - o0) % 5 == 4 && k > s.Fill)
            {
                if (v.Mg == 2 && !double.IsNaN(x.Vw[k]) && (dir > 0 ? d.C[k] < x.Vw[k] : d.C[k] > x.Vw[k])) { pts = (d.C[k] - entry) * dir - Tick; break; }
                if (v.Mg == 3 && be) { var pb = Bar(d, k - 4); double ts = dir > 0 ? pb[1] - Tick : pb[0] + Tick; if (!double.IsNaN(ts) && (dir > 0 ? ts > cur : ts < cur)) cur = ts; }
            }
        }
        if (double.IsNaN(pts)) pts = (d.CloseAt(flat) - entry) * dir;
        double usd = pts * Pv - Cost; return new Tr { Day = d.Day, Dir = dir, R = usd / (risk * Pv), Usd = usd };
    }
}
