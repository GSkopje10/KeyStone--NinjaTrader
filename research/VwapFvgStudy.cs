// VWAP + 5-MINUTE FVG (user's idea): below the VWAP a bearish 5-minute FVG (low of bar 1 above the high of bar 3) is a SELL zone;
// when price comes back up into it (10 % … 50 % of its height, or just touching it) we sell, stop above the VWAP, fixed target in points.
// The mirror (above the VWAP, a bullish FVG, BUY, stop under the VWAP) is tested the same way.
// Every year of the 1-minute data, both instruments (MNQ; MGC = gold like XAUUSD). Outcomes on 1-minute bars: a minute that touches
// both the stop and the target counts as the STOP (conservative); the entry minute itself only counts against us; flat at 16:55 NY.
// One trade at a time per variant. Costs per contract: MNQ $1.74, MGC $2.50 (commission + a tick).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class VwapFvgStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public sealed class V { public string Sym, Side, Anchor, Hours, StopAt; public double Pct, Tp; public int Expire; public string Key { get { return Sym + " " + Side + " vwap@" + Anchor + " " + Hours + " in " + (Pct * 100) + "% stop " + StopAt + " tp " + Tp + " exp " + Expire + "x5m"; } } }
    sealed class Tr { public DateTime Day; public int Year; public bool Win; public double Pts, Risk; public string Why; }

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir); var json = new StringBuilder("{\"rows\":["); bool first = true; var md = new StringBuilder();
        md.AppendLine("# VWAP + 5-minute FVG — every year, MNQ and MGC (gold)\n");
        md.AppendLine("Sell: price under the VWAP, a bearish 5-min FVG forms, price comes back into it → sell; stop above the VWAP; fixed target. Buy = the mirror.");
        md.AppendLine("1-minute outcomes, same-minute stop+target = stop, flat 16:55 NY, one trade at a time, costs per contract (MNQ $1.74, MGC $2.50).\n");
        foreach (var sym in new[] { "MNQ", "MGC" })
        {
            var days = R.Load(folder, sym, true).OrderBy(d => d.Day).ToList();
            double pv = sym == "MNQ" ? 2 : 10, cost = sym == "MNQ" ? 1.74 : 2.5, buf = sym == "MNQ" ? 2 : 0.5;
            var tps = sym == "MNQ" ? new double[] { 10, 15, 20, 30, 40 } : new double[] { 3, 5, 10, 15, 20 };
            var vs = new List<V>();
            foreach (var side in new[] { "SELL", "BUY" }) foreach (var an in new[] { "SESSION", "RTH" }) foreach (var hrs in new[] { "ALL", "LDN+NY", "NY" }) foreach (var pct in new[] { 0.0, 0.1, 0.25, 0.5 })
                foreach (var st in new[] { "VWAP", "FVG" }) foreach (var tp in tps) foreach (var ex in new[] { 12, 36 })
                    vs.Add(new V { Sym = sym, Side = side, Anchor = an, Hours = hrs, Pct = pct, StopAt = st, Tp = tp, Expire = ex });
            var res = new List<Tr>[vs.Count];
            Parallel.For(0, vs.Count, i => { var l = new List<Tr>(); foreach (var d in days) l.AddRange(Day(d, vs[i], buf)); res[i] = l; });
            var years = days.Select(d => d.Day.Year).Distinct().OrderBy(y => y).ToList();
            var rows = new List<Tuple<V, List<Tr>, double>>();
            for (int i = 0; i < vs.Count; i++) { var l = res[i]; double net = l.Sum(t => t.Pts * pv - cost); rows.Add(Tuple.Create(vs[i], l, net)); }
            foreach (var r in rows)
            {
                var l = r.Item2; if (!first) json.Append(","); first = false;
                json.Append("{\"key\":\"" + r.Item1.Key + "\",\"sym\":\"" + sym + "\",\"side\":\"" + r.Item1.Side + "\",\"anchor\":\"" + r.Item1.Anchor + "\",\"hours\":\"" + r.Item1.Hours + "\",\"pct\":" + r.Item1.Pct.ToString(IC) + ",\"stop\":\"" + r.Item1.StopAt + "\",\"tp\":" + r.Item1.Tp.ToString(IC) + ",\"exp\":" + r.Item1.Expire
                    + ",\"n\":" + l.Count + ",\"win\":" + Pct(l) + ",\"net\":" + Math.Round(r.Item3) + ",\"avgRisk\":" + (l.Count == 0 ? 0 : Math.Round(l.Average(t => t.Risk), 2)).ToString(IC) + ",\"years\":{"
                    + string.Join(",", years.Select(y => { var ly = l.Where(t => t.Year == y).ToList(); return "\"" + y + "\":{\"n\":" + ly.Count + ",\"win\":" + Pct(ly) + ",\"net\":" + Math.Round(ly.Sum(t => t.Pts * pv - cost)) + "}"; })) + "}}");
            }
            foreach (var side in new[] { "SELL", "BUY" })
            {
                md.AppendLine("## " + sym + " • " + side + (side == "SELL" ? " (under the VWAP, bearish FVG)" : " (above the VWAP, bullish FVG)") + "\n");
                // the user's version first: 15-point target (MNQ) / the matching gold one, stop above the VWAP, every entry depth
                md.AppendLine("**Your rule (stop above the VWAP, session VWAP, all hours, FVG valid 1 hour):**\n");
                md.AppendLine("| entry in the FVG | target | trades | win % | net $ (1 contract) | avg risk (pts) | " + string.Join(" | ", years) + " |"); md.AppendLine("|---|---|---|---|---|---|" + string.Join("", years.Select(y => "---|")));
                foreach (var r in rows.Where(x => x.Item1.Side == side && x.Item1.Anchor == "SESSION" && x.Item1.Hours == "ALL" && x.Item1.StopAt == "VWAP" && x.Item1.Expire == 12))
                    md.AppendLine("| " + (r.Item1.Pct == 0 ? "touch" : (r.Item1.Pct * 100) + " %") + " | " + r.Item1.Tp + " | " + r.Item2.Count + " | " + Pct(r.Item2) + " | " + Math.Round(r.Item3).ToString("N0", IC) + " | " + (r.Item2.Count == 0 ? 0 : r.Item2.Average(t => t.Risk)).ToString("0.0", IC) + " | "
                        + string.Join(" | ", years.Select(y => { var ly = r.Item2.Where(t => t.Year == y).ToList(); return Pct(ly) + "% / " + Math.Round(ly.Sum(t => t.Pts * pv - cost)).ToString("N0", IC); })) + " |");
                md.AppendLine("\n**Best 12 of every combination (≥ 150 trades, ranked by net $; per year: win % / net $):**\n");
                md.AppendLine("| variant | trades | win % | net $ | " + string.Join(" | ", years) + " |"); md.AppendLine("|---|---|---|---|" + string.Join("", years.Select(y => "---|")));
                foreach (var r in rows.Where(x => x.Item1.Side == side && x.Item2.Count >= 150).OrderByDescending(x => x.Item3).Take(12))
                    md.AppendLine("| " + r.Item1.Key + " | " + r.Item2.Count + " | " + Pct(r.Item2) + " | " + Math.Round(r.Item3).ToString("N0", IC) + " | "
                        + string.Join(" | ", years.Select(y => { var ly = r.Item2.Where(t => t.Year == y).ToList(); return Pct(ly) + "% / " + Math.Round(ly.Sum(t => t.Pts * pv - cost)).ToString("N0", IC); })) + " |");
                int up = rows.Count(x => x.Item1.Side == side && x.Item2.Count >= 150 && x.Item3 > 0), all = rows.Count(x => x.Item1.Side == side && x.Item2.Count >= 150);
                md.AppendLine("\n" + up + " of " + all + " combinations made money over all the years.\n");
            }
            Console.WriteLine(sym + " done");
        }
        json.Append("]}"); File.WriteAllText(Path.Combine(outDir, "vwapfvg.json"), json.ToString()); File.WriteAllText(Path.Combine(outDir, "vwapfvg.md"), md.ToString());
    }
    static string Pct(List<Tr> l) { return l.Count == 0 ? "0" : (100.0 * l.Count(t => t.Win) / l.Count).ToString("0.0", IC); }

    static IEnumerable<Tr> Day(RDay d, V v, double buf)
    {
        int dir = v.Side == "SELL" ? -1 : 1; int a0 = v.Anchor == "SESSION" ? 0 : R.S(9, 30);
        int h0 = v.Hours == "ALL" ? 0 : v.Hours == "LDN+NY" ? R.S(3, 0) : R.S(9, 30), h1 = R.S(16, 0), flat = R.S(16, 55);
        // VWAP per minute from the anchor (typical price × volume); NaN before the anchor
        var vw = new double[R.Slots]; double pv = 0, vol = 0;
        for (int k = 0; k < R.Slots; k++) { if (k >= a0 && d.Has(k)) { double w = Math.Max(1, d.V[k]); pv += (d.H[k] + d.L[k] + d.C[k]) / 3.0 * w; vol += w; } vw[k] = vol > 0 && k >= a0 ? pv / vol : double.NaN; }
        // 5-minute bars on the grid: bar b = slots 5b … 5b+4
        int nb = R.Slots / 5; var bh = new double[nb]; var bl = new double[nb]; var bc = new double[nb]; var ok = new bool[nb];
        for (int b = 0; b < nb; b++) { double hi = double.NaN, lo = double.NaN, c = double.NaN; for (int k = b * 5; k < b * 5 + 5; k++) if (d.Has(k)) { hi = double.IsNaN(hi) ? d.H[k] : Math.Max(hi, d.H[k]); lo = double.IsNaN(lo) ? d.L[k] : Math.Min(lo, d.L[k]); c = d.C[k]; } bh[b] = hi; bl[b] = lo; bc[b] = c; ok[b] = !double.IsNaN(c); }
        int busyUntil = -1;
        for (int b = 2; b < nb; b++)
        {
            if (!ok[b] || !ok[b - 1] || !ok[b - 2]) continue; int endK = b * 5 + 4; if (endK < h0 || endK >= h1 || endK <= busyUntil) continue;
            double v0 = vw[endK]; if (double.IsNaN(v0)) continue;
            double top, bot;   // the zone: for a SELL the gap between bar 1's low (top) and bar 3's high (bottom)
            if (dir < 0) { if (!(bl[b - 2] > bh[b])) continue; top = bl[b - 2]; bot = bh[b]; if (!(bc[b] < v0) || !(top < v0)) continue; }
            else { if (!(bh[b - 2] < bl[b])) continue; bot = bh[b - 2]; top = bl[b]; if (!(bc[b] > v0) || !(bot > v0)) continue; }
            double entry = dir < 0 ? bot + v.Pct * (top - bot) : top - v.Pct * (top - bot);
            // wait for price to come back into the zone (from the next minute, up to Expire 5-min bars); the zone dies when price goes through it
            int fill = -1, lastK = Math.Min(h1, endK + v.Expire * 5);
            for (int k = endK + 1; k <= lastK && k < R.Slots; k++)
            {
                if (!d.Has(k)) continue;
                if (dir < 0 ? d.H[k] >= entry : d.L[k] <= entry) { fill = k; break; }
            }
            if (fill < 0) continue;
            double vf = vw[fill]; double stop = v.StopAt == "VWAP" ? (dir < 0 ? Math.Max(vf, v0) + buf : Math.Min(vf, v0) - buf) : (dir < 0 ? top + buf : bot - buf);
            double risk = (stop - entry) * -dir * -1; risk = Math.Abs(stop - entry); if (dir < 0 ? stop <= entry : stop >= entry) continue;
            double tgt = entry + dir * v.Tp; bool win = false; double pts = 0; string why = "flat"; int outK = flat;
            // the entry minute: only the stop counts (we don't know the order inside it)
            if (dir < 0 ? d.H[fill] >= stop : d.L[fill] <= stop) { pts = -risk; why = "stop"; outK = fill; }
            else
            {
                for (int k = fill + 1; k <= flat && k < R.Slots; k++)
                {
                    if (!d.Has(k)) continue;
                    bool hs = dir < 0 ? d.H[k] >= stop : d.L[k] <= stop, ht = dir < 0 ? d.L[k] <= tgt : d.H[k] >= tgt;
                    if (hs) { pts = -risk; why = "stop"; outK = k; break; }
                    if (ht) { pts = v.Tp; win = true; why = "target"; outK = k; break; }
                    if (k == flat || k == R.Slots - 1) { pts = (d.C[k] - entry) * dir; why = "flat"; outK = k; win = pts > 0; break; }
                }
                if (why == "flat" && pts == 0) { double c = d.CloseAt(flat); pts = (c - entry) * dir; win = pts > 0; }
            }
            busyUntil = outK;
            yield return new Tr { Day = d.Day, Year = d.Day.Year, Win = win && why == "target", Pts = pts, Risk = risk, Why = why };
        }
    }
}
