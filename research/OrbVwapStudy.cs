// ORB + VWAP + VOLUME PROFILE (user's idea), MNQ and MGC 1-minute bars, every RTH day 2020 → 2026.
// The opening range (09:30–09:45 or 09:30–10:00 New York); after it, the first 1-minute CLOSE beyond its high (buy) / low (sell) is the
// signal — or a RETEST: after that break, a limit at the range edge (fills only when price trades 1 tick through it).
// Confirmations (each on / off): VWAP side (RTH VWAP from 09:30: buy above, sell below) • VWAP slope (higher than 15 minutes ago for a buy)
// • VOLUME PROFILE (the previous RTH day's value area, 70 %: buy above its high, sell below its low) • VOLUME (the break candle ≥ 1.5×
// the opening range's average minute).
// Stops: the other side of the range • the range's middle • the VWAP. Targets 1 / 1.5 / 2 / 3 R or the 15:55 close. One trade a day.
// Costs a contract: MNQ $1.74, MGC $2.50, + 1 tick on market entries and on stops. A minute that touches stop and target = stop.
// Honesty: every rule is ranked on 2020–2023 only (IN-SAMPLE) and shown on 2024–2026 (OUT-OF-SAMPLE, never used to pick); a RANDOM
// benchmark enters at a random minute of the same window on the VWAP's side with the same stop / target.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class OrbVwapStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class V { public string Sym; public int Orb, Entry, Stop, Tgt, End; public bool FV, FS, FP, FL, Rnd; public string Key; }
    sealed class Tr { public int Year; public double R, Usd; }
    sealed class Day { public RDay D; public double[] Vw; public double Vah, Val, Poc, Vah0, Val0, Poc0; public bool HasVa, HasVa0; }

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir); var md = new StringBuilder(); var json = new StringBuilder("{\"rows\":["); bool first = true;
        md.AppendLine("# ORB + VWAP + volume profile — MNQ and MGC, 1-minute bars\n");
        md.AppendLine("Ranked on 2020–2023 (in-sample), shown on 2024–2026 (out-of-sample, never used to pick). R = the stop distance; results after costs.\n");
        foreach (var sym in new[] { "MNQ", "MGC" })
        {
            double pv = sym == "MNQ" ? 2 : 10, cost = sym == "MNQ" ? 1.74 : 2.5, tick = sym == "MNQ" ? 0.25 : 0.1, bucket = sym == "MNQ" ? 2 : 0.5;
            var raw = R.Load(folder, sym, true).OrderBy(d => d.Day).ToList(); var days = new List<Day>(); Day prev = null;
            foreach (var d in raw)
            {
                var x = new Day { D = d, Vw = new double[R.Slots] }; double pvs = 0, vol = 0; int a0 = R.S(9, 30), a1 = R.S(16, 0);
                for (int k = 0; k < R.Slots; k++) { if (k >= a0 && k < a1 && d.Has(k)) { double w = Math.Max(1, d.V[k]); pvs += (d.H[k] + d.L[k] + d.C[k]) / 3 * w; vol += w; } x.Vw[k] = vol > 0 ? pvs / vol : double.NaN; }
                if (prev != null) { x.Vah = prev.Vah0; x.Val = prev.Val0; x.Poc = prev.Poc0; x.HasVa = prev.HasVa0; }
                // this day's own value area (for tomorrow)
                var prof = new Dictionary<long, double>(); for (int k = a0; k < a1; k++) if (d.Has(k)) { long b = (long)Math.Floor(d.C[k] / bucket); double w = Math.Max(1, d.V[k]); double cur; prof.TryGetValue(b, out cur); prof[b] = cur + w; }
                if (prof.Count > 5)
                {
                    var keys = prof.Keys.OrderBy(b => b).ToList(); long poc = prof.OrderByDescending(kv => kv.Value).First().Key; double tot = prof.Values.Sum(), inVa = prof[poc]; int lo = keys.IndexOf(poc), hi = lo;
                    while (inVa < tot * 0.7 && (lo > 0 || hi < keys.Count - 1)) { double up = hi < keys.Count - 1 ? prof[keys[hi + 1]] : -1, dn = lo > 0 ? prof[keys[lo - 1]] : -1; if (up >= dn) { hi++; inVa += up; } else { lo--; inVa += dn; } }
                    x.Vah0 = (keys[hi] + 1) * bucket; x.Val0 = keys[lo] * bucket; x.Poc0 = (poc + 0.5) * bucket; x.HasVa0 = true;
                }
                days.Add(x); prev = x;
            }
            var vs = new List<V>();
            foreach (var orb in new[] { 15, 30 }) foreach (var en in new[] { 0, 1 }) foreach (var fv in new[] { false, true }) foreach (var fs in new[] { false, true }) foreach (var fp in new[] { false, true }) foreach (var fl in new[] { false, true })
                foreach (var stp in new[] { 0, 1, 2 }) foreach (var tg in new[] { 1, 2, 3, 4, 0 }) foreach (var end in new[] { 12, 15 })
                {
                    if (stp == 2 && !fv) continue;   // a VWAP stop needs the price on the VWAP's side
                    vs.Add(new V { Sym = sym, Orb = orb, Entry = en, FV = fv, FS = fs, FP = fp, FL = fl, Stop = stp, Tgt = tg, End = end });
                }
            foreach (var orb in new[] { 15, 30 }) foreach (var stp in new[] { 0, 1, 2 }) foreach (var tg in new[] { 1, 2, 3, 4, 0 }) foreach (var end in new[] { 12, 15 }) vs.Add(new V { Sym = sym, Orb = orb, Rnd = true, FV = true, Stop = stp, Tgt = tg, End = end });
            foreach (var v in vs) v.Key = (v.Rnd ? "RANDOM (VWAP side)" : (v.Entry == 0 ? "BREAK" : "RETEST") + (v.FV ? " +VWAP" : "") + (v.FS ? " +slope" : "") + (v.FP ? " +profile" : "") + (v.FL ? " +volume" : "")) + " • ORB " + v.Orb + " • stop " + new[] { "other side", "middle", "VWAP" }[v.Stop] + " • target " + new[] { "15:55", "1R", "1.5R", "2R", "3R" }[v.Tgt] + " • until " + v.End + ":00";
            var res = new List<Tr>[vs.Count];
            Parallel.For(0, vs.Count, i => { var l = new List<Tr>(); foreach (var d in days) { var t = Trade(d, vs[i], tick); if (t != null) { t.Usd = t.R * t.Usd - cost; l.Add(t); } } res[i] = l; });
            // per-trade $: Trade returns R and the risk in points (Usd field) → $ = R × risk × pv − cost (1 contract)
            Func<List<Tr>, Func<Tr, bool>, double[]> stat = (l, f) => { var s = l.Where(f).ToList(); if (s.Count == 0) return new double[] { 0, 0, 0, 0, 0 }; double net = s.Sum(t => t.Usd), gw = s.Where(t => t.Usd > 0).Sum(t => t.Usd), gl = -s.Where(t => t.Usd < 0).Sum(t => t.Usd); double eq = 0, pk = 0, dd = 0; foreach (var t in s) { eq += t.Usd; pk = Math.Max(pk, eq); dd = Math.Max(dd, pk - eq); } return new double[] { s.Count, 100.0 * s.Count(t => t.R > 0) / s.Count, net, gl > 0 ? gw / gl : 0, dd }; };
            var rows = new List<Tuple<V, double[], double[], List<Tr>>>();
            for (int i = 0; i < vs.Count; i++) { var l = res[i]; foreach (var t in l) t.Usd = t.Usd; rows.Add(Tuple.Create(vs[i], stat(l, t => t.Year <= 2023), stat(l, t => t.Year >= 2024), l)); }
            foreach (var r in rows)
            {
                if (!first) json.Append(","); first = false; var yrs = r.Item4.GroupBy(t => t.Year).OrderBy(g => g.Key);
                json.Append("{\"sym\":\"" + sym + "\",\"key\":\"" + r.Item1.Key + "\",\"rnd\":" + (r.Item1.Rnd ? "true" : "false") + ",\"is\":[" + string.Join(",", r.Item2.Select(x => Math.Round(x, 2).ToString(IC))) + "],\"oos\":[" + string.Join(",", r.Item3.Select(x => Math.Round(x, 2).ToString(IC))) + "],\"years\":{" + string.Join(",", yrs.Select(g => "\"" + g.Key + "\":" + Math.Round(g.Sum(t => t.Usd)))) + "}}");
            }
            md.AppendLine("## " + sym + " (1 contract, $ after costs)\n");
            var real = rows.Where(r => !r.Item1.Rnd && r.Item2[0] >= 150).ToList();
            md.AppendLine(real.Count(r => r.Item2[2] > 0) + " of " + real.Count + " rules made money in-sample; of the 20 best in-sample, " + real.OrderByDescending(r => r.Item2[2] / Math.Max(1, r.Item2[4])).Take(20).Count(r => r.Item3[2] > 0) + " also made money out-of-sample.\n");
            md.AppendLine("**The 15 best in-sample (ranked by net ÷ drawdown, ≥ 150 trades), and what they did after:**\n");
            md.AppendLine("| rule | IS trades | IS win | IS net | IS PF | IS DD | OOS trades | OOS win | OOS net | OOS PF | OOS DD |"); md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in real.OrderByDescending(r => r.Item2[2] / Math.Max(1, r.Item2[4])).Take(15))
                md.AppendLine("| " + r.Item1.Key + " | " + r.Item2[0] + " | " + r.Item2[1].ToString("0", IC) + "% | " + r.Item2[2].ToString("N0", IC) + " | " + r.Item2[3].ToString("0.00", IC) + " | " + r.Item2[4].ToString("N0", IC) + " | " + r.Item3[0] + " | " + r.Item3[1].ToString("0", IC) + "% | " + r.Item3[2].ToString("N0", IC) + " | " + r.Item3[3].ToString("0.00", IC) + " | " + r.Item3[4].ToString("N0", IC) + " |");
            md.AppendLine("\n**What each confirmation adds (average over every rule with it on vs off, in-sample → out-of-sample net $):**\n");
            foreach (var f in new[] { "VWAP", "slope", "profile", "volume" })
            {
                Func<V, bool> on = v => f == "VWAP" ? v.FV : f == "slope" ? v.FS : f == "profile" ? v.FP : v.FL;
                var a = real.Where(r => on(r.Item1)).ToList(); var b = real.Where(r => !on(r.Item1)).ToList();
                md.AppendLine("- **" + f + "**: on " + a.Average(r => r.Item2[2]).ToString("N0", IC) + " → " + a.Average(r => r.Item3[2]).ToString("N0", IC) + " • off " + b.Average(r => r.Item2[2]).ToString("N0", IC) + " → " + b.Average(r => r.Item3[2]).ToString("N0", IC));
            }
            var rnd = rows.Where(r => r.Item1.Rnd).ToList();
            md.AppendLine("\n**Random benchmark** (random minute, VWAP side, same stops / targets): " + rnd.Count(r => r.Item2[2] > 0) + " of " + rnd.Count + " made money in-sample, average " + rnd.Average(r => r.Item2[2]).ToString("N0", IC) + " → out-of-sample " + rnd.Average(r => r.Item3[2]).ToString("N0", IC) + ".\n");
            Console.WriteLine(sym + " done");
        }
        json.Append("]}"); File.WriteAllText(Path.Combine(outDir, "orbvwap.json"), json.ToString()); File.WriteAllText(Path.Combine(outDir, "orbvwap.md"), md.ToString());
    }

    // one day: returns (R, the risk in points in Usd for now) or null
    static Tr Trade(Day x, V v, double tick)
    {
        var d = x.D; int o0 = R.S(9, 30), o1 = o0 + v.Orb, end = R.S(v.End, 0), flat = R.S(15, 55);
        double hi = d.Hi(o0, o1 - 1), lo = d.Lo(o0, o1 - 1); if (double.IsNaN(hi) || double.IsNaN(lo) || hi - lo < tick * 4) return null;
        double avgV = 0; int nv = 0; for (int k = o0; k < o1; k++) if (d.Has(k)) { avgV += d.V[k]; nv++; } if (nv < v.Orb / 2) return null; avgV /= nv;
        int dir = 0, sig = -1;
        if (v.Rnd)
        {
            int span = end - o1; if (span < 10) return null; int k0 = o1 + (int)((uint)(d.Day.GetHashCode() * 2654435761u) % (uint)span);
            for (int k = k0; k < end; k++) if (d.Has(k) && !double.IsNaN(x.Vw[k])) { sig = k; dir = d.C[k] > x.Vw[k] ? 1 : -1; break; }
            if (sig < 0) return null;
        }
        else
        {
            for (int k = o1; k < end; k++)
            {
                if (!d.Has(k)) continue; int dd = d.C[k] > hi ? 1 : d.C[k] < lo ? -1 : 0; if (dd == 0) continue;
                double vw = x.Vw[k]; bool ok = true;
                if (v.FV) ok &= !double.IsNaN(vw) && (dd > 0 ? d.C[k] > vw : d.C[k] < vw);
                if (v.FS) ok &= k - 15 >= o0 && !double.IsNaN(x.Vw[k - 15]) && (dd > 0 ? vw > x.Vw[k - 15] : vw < x.Vw[k - 15]);
                if (v.FP) ok &= x.HasVa && (dd > 0 ? d.C[k] > x.Vah : d.C[k] < x.Val);
                if (v.FL) ok &= d.V[k] >= 1.5 * avgV;
                dir = dd; sig = k; if (!ok) return null; break;   // the FIRST break decides the day (filtered out = no trade)
            }
            if (sig < 0) return null;
        }
        double entry; int fill;
        if (v.Rnd || v.Entry == 0) { fill = sig + 1; while (fill < end && !d.Has(fill)) fill++; if (fill >= end) return null; entry = d.O[fill] + dir * tick; }   // market at the next minute's open + 1 tick
        else
        {
            double lim = dir > 0 ? hi : lo; fill = -1;
            for (int k = sig + 1; k < end; k++) { if (!d.Has(k)) continue; if (dir > 0 ? d.L[k] <= lim - tick : d.H[k] >= lim + tick) { fill = k; break; } }
            if (fill < 0) return null; entry = lim;
        }
        double stop = v.Stop == 0 ? (dir > 0 ? lo : hi) - dir * tick : v.Stop == 1 ? (hi + lo) / 2 : x.Vw[fill] - dir * tick;
        double risk = (entry - stop) * dir; if (double.IsNaN(risk) || risk < tick * 4) return null;
        double tgtR = new[] { 0, 1, 1.5, 2, 3 }[v.Tgt]; double tgt = tgtR > 0 ? entry + dir * tgtR * risk : double.NaN; double pts = double.NaN;
        bool entryMinuteStop = v.Entry == 1 && !v.Rnd && (dir > 0 ? d.L[fill] <= stop : d.H[fill] >= stop);   // a limit fill: the same minute can run to the stop
        if (entryMinuteStop) pts = -risk - tick;
        int k1 = v.Entry == 1 && !v.Rnd ? fill + 1 : fill;
        for (int k = k1; double.IsNaN(pts) && k <= flat; k++)
        {
            if (!d.Has(k)) continue;
            if (dir > 0 ? d.L[k] <= stop : d.H[k] >= stop) { pts = -risk - tick; break; }
            if (!double.IsNaN(tgt) && (dir > 0 ? d.H[k] >= tgt : d.L[k] <= tgt)) { pts = tgtR * risk; break; }
        }
        if (double.IsNaN(pts)) pts = (d.CloseAt(flat) - entry) * dir;
        return new Tr { Year = d.Day.Year, R = pts / risk, Usd = risk * (v.Sym == "MNQ" ? 2 : 10) };
    }
}
