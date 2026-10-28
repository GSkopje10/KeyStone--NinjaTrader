// ORB + VWAP on MNQ, BUYS vs SELLS (user's manual rule): a 5-minute close ABOVE the opening range high while above the VWAP = BUY,
// a 5-minute close BELOW the low while below the VWAP = SELL; market entry at the next minute's open (+1 tick slippage).
// Risk management grid: stop (other side of the range / its middle / the VWAP / the break candle's far end), target (1 / 1.5 / 2 / 3R or
// the 15:55 close), management (none / break-even at +1R / out on a 5-minute close back through the VWAP / break-even at +1R then trail
// under the last 5-minute candle), entries until 12:00 or 15:00, one trade a day or a second one the other way after the first is out.
// Costs $1.74 a contract + 1 tick on stops; a minute that touches stop and target = stop. Ranked on 2020–2023, shown on 2024–2026.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class OrbSidesStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class V { public int Orb, Stop, Tgt, Mg, End; public bool FV, Two; public string Key; }
    sealed class Tr { public DateTime Day; public int Dir; public double R, Usd, Risk, Mfe; }
    sealed class Day { public RDay D; public double[] Vw; }
    const double Tick = 0.25, Pv = 2, Cost = 1.74;
    static readonly string[] StopN = { "range other side", "range middle", "VWAP", "break candle end" }, TgtN = { "15:55", "1R", "1.5R", "2R", "3R" }, MgN = { "none", "BE at +1R", "out on a close through VWAP", "BE at +1R + trail 5-min candles" };

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
        foreach (var orb in new[] { 15, 30 }) foreach (var fv in new[] { true, false }) foreach (var st in new[] { 0, 1, 2, 3 }) foreach (var tg in new[] { 1, 2, 3, 4, 0 }) foreach (var mg in new[] { 0, 1, 2, 3 }) foreach (var end in new[] { 12, 15 }) foreach (var two in new[] { false, true })
        { if (st == 2 && !fv) continue; vs.Add(new V { Orb = orb, FV = fv, Stop = st, Tgt = tg, Mg = mg, End = end, Two = two }); }
        foreach (var v in vs) v.Key = "ORB " + v.Orb + (v.FV ? " + VWAP side" : " (no VWAP filter)") + " • stop " + StopN[v.Stop] + " • target " + TgtN[v.Tgt] + " • " + MgN[v.Mg] + " • entries until " + v.End + ":00" + (v.Two ? " • 2nd trade the other way" : " • 1 trade a day");
        var res = new List<Tr>[vs.Count];
        Parallel.For(0, vs.Count, i => { var l = new List<Tr>(); foreach (var d in days) l.AddRange(Trades(d, vs[i])); res[i] = l; });

        // stats: trades, win %, net $, PF, max DD $, avg R, avg win R, avg loss R, longest losing run
        Func<IEnumerable<Tr>, double[]> stat = src =>
        {
            var s = src.ToList(); if (s.Count == 0) return new double[9];
            double gw = s.Where(t => t.Usd > 0).Sum(t => t.Usd), gl = -s.Where(t => t.Usd < 0).Sum(t => t.Usd), eq = 0, pk = 0, dd = 0; int run = 0, worst = 0;
            foreach (var t in s) { eq += t.Usd; pk = Math.Max(pk, eq); dd = Math.Max(dd, pk - eq); run = t.Usd < 0 ? run + 1 : 0; worst = Math.Max(worst, run); }
            var w = s.Where(t => t.R > 0).ToList(); var lo = s.Where(t => t.R <= 0).ToList();
            return new double[] { s.Count, 100.0 * w.Count / s.Count, s.Sum(t => t.Usd), gl > 0 ? gw / gl : 0, dd, s.Average(t => t.R), w.Count > 0 ? w.Average(t => t.R) : 0, lo.Count > 0 ? lo.Average(t => t.R) : 0, worst };
        };
        Func<Tr, bool> isF = t => t.Day.Year <= 2023, oosF = t => t.Day.Year >= 2024;
        var rows = Enumerable.Range(0, vs.Count).Select(i => new { V = vs[i], L = res[i], IS = stat(res[i].Where(isF)), OOS = stat(res[i].Where(oosF)) }).ToList();
        var md = new StringBuilder("# MNQ: ORB + VWAP — BUYS vs SELLS, and the risk management\n\n");
        md.AppendLine("The rule: a **5-minute close above the opening range high while above the VWAP = BUY**, **below the low while below the VWAP = SELL**, market at the next minute. 1 MNQ contract, $ after costs ($1.74 + 1 tick on stops). Every rule is picked on **2020–2023** and checked on **2024–2026** (never seen while picking). R = the trade's result ÷ its own stop distance.\n");
        Action<string[]> head = h => { md.AppendLine("| " + string.Join(" | ", h) + " |"); md.AppendLine("|" + string.Join("|", h.Select(_ => "---")) + "|"); };
        Func<double[], string> cells = s => s[0] + " | " + s[1].ToString("0", IC) + "% | " + s[2].ToString("N0", IC) + " | " + s[3].ToString("0.00", IC) + " | " + s[4].ToString("N0", IC) + " | " + s[5].ToString("0.00", IC);

        // 1. the plain rule, sides apart (range other side stop, hold to 15:55, no management, 1 a day)
        md.AppendLine("## 1. Buys vs sells — the rule with no management\n\nStop at the other side of the range, held to 15:55, one trade a day.\n");
        head(new[] { "ORB", "side", "period", "trades", "win", "net $", "PF", "max DD $", "avg R" });
        foreach (var orb in new[] { 15, 30 })
        {
            var b = rows.First(r => r.V.Orb == orb && r.V.FV && r.V.Stop == 0 && r.V.Tgt == 0 && r.V.Mg == 0 && r.V.End == 15 && !r.V.Two);
            foreach (var sd in new[] { 1, -1 }) foreach (var per in new[] { "2020–23", "2024–26" })
                md.AppendLine("| " + orb + " | " + (sd > 0 ? "▲ BUY" : "▼ SELL") + " | " + per + " | " + cells(stat(b.L.Where(t => t.Dir == sd && (per == "2020–23" ? isF(t) : oosF(t))))) + " |");
        }
        md.AppendLine();

        // 2. every side × risk management: the best 10 picked on 2020–23 (net ÷ drawdown, at least 150 trades), then their 2024–26
        foreach (var sd in new[] { 1, -1, 0 })
        {
            string nm = sd > 0 ? "▲ BUYS only" : sd < 0 ? "▼ SELLS only" : "BOTH sides";
            var c = rows.Select(r => new { r.V, IS = stat(r.L.Where(t => isF(t) && (sd == 0 || t.Dir == sd))), OOS = stat(r.L.Where(t => oosF(t) && (sd == 0 || t.Dir == sd))) })
                .Where(r => r.IS[0] >= (sd == 0 ? 300 : 150)).OrderByDescending(r => r.IS[2] / Math.Max(500, r.IS[4])).ToList();
            md.AppendLine("## 2" + (sd > 0 ? "a" : sd < 0 ? "b" : "c") + ". " + nm + " — the best risk management\n");
            md.AppendLine(c.Count(r => r.IS[2] > 0) + " of " + c.Count + " rules made money in 2020–23; of the best 10, **" + c.Take(10).Count(r => r.OOS[2] > 0) + " also made money in 2024–26** (their average there " + (c.Count > 0 ? c.Take(10).Average(r => r.OOS[2]) : 0).ToString("N0", IC) + " $). Of ALL rules, " + c.Count(r => r.OOS[2] > 0) + " of " + c.Count + " made money in 2024–26.\n");
            head(new[] { "rule", "20–23 trades", "win", "net $", "PF", "DD $", "avg R", "24–26 trades", "win", "net $", "PF", "DD $", "avg R" });
            foreach (var r in c.Take(10)) md.AppendLine("| " + r.V.Key + " | " + cells(r.IS) + " | " + cells(r.OOS) + " |");
            md.AppendLine();
        }

        // 3. which choice helps, on average over the whole grid (out-of-sample avg R), by side
        md.AppendLine("## 3. What each choice does (average over every rule that has it, 2024–26 avg R per trade)\n");
        head(new[] { "choice", "▲ buys 20–23", "▲ buys 24–26", "▼ sells 20–23", "▼ sells 24–26" });
        Action<string, Func<V, bool>> eff = (n, f) =>
        {
            var sel = rows.Where(r => f(r.V)).ToList(); Func<int, Func<Tr, bool>, string> av = (sd, pf) => sel.Average(r => stat(r.L.Where(t => t.Dir == sd && pf(t)))[5]).ToString("0.000", IC);
            md.AppendLine("| " + n + " | " + av(1, isF) + " | " + av(1, oosF) + " | " + av(-1, isF) + " | " + av(-1, oosF) + " |");
        };
        foreach (var o in new[] { 15, 30 }) eff("ORB " + o, v => v.Orb == o);
        eff("VWAP side filter ON", v => v.FV); eff("VWAP side filter OFF", v => !v.FV);
        for (int q = 0; q < StopN.Length; q++) { int qq = q; eff("stop: " + StopN[q], v => v.Stop == qq); }
        for (int q = 0; q < TgtN.Length; q++) { int qq = q; eff("target: " + TgtN[q], v => v.Tgt == qq); }
        for (int q = 0; q < MgN.Length; q++) { int qq = q; eff("management: " + MgN[q], v => v.Mg == qq); }
        eff("entries until 12:00", v => v.End == 12); eff("entries until 15:00", v => v.End == 15);
        md.AppendLine();

        // 4. the most stable rule: good in BOTH periods (the lower of the two avg R), then sized by $ risk
        // the rule section 2c ranks first (both sides, picked on 2020–23 only) — its 2024–26 is out of sample
        var best = rows.Where(r => r.IS[0] >= 300).OrderByDescending(r => r.IS[2] / Math.Max(500, r.IS[4])).First();
        md.AppendLine("## 4. The pick: the best both-sides rule of 2020–23, year by year\n\n**" + best.V.Key + "** — chosen on 2020–23 only, so 2024–26 is a real test.\n");
        head(new[] { "year", "trades", "win", "net $", "PF", "DD $", "avg R" });
        foreach (var g in best.L.GroupBy(t => t.Day.Year).OrderBy(g => g.Key)) md.AppendLine("| " + g.Key + " | " + cells(stat(g)) + " |");
        foreach (var sd in new[] { 1, -1 }) md.AppendLine("| " + (sd > 0 ? "▲ buys" : "▼ sells") + " all years | " + cells(stat(best.L.Where(t => t.Dir == sd))) + " |");
        var bs = stat(best.L); md.AppendLine("\nAverage win " + bs[6].ToString("0.00", IC) + "R, average loss " + bs[7].ToString("0.00", IC) + "R, longest losing run " + bs[8] + " trades. Median stop " + Median(best.L.Select(t => t.Risk)).ToString("0.0", IC) + " points ($" + (Median(best.L.Select(t => t.Risk)) * Pv).ToString("0", IC) + " a micro). Median best move in the trade's favour " + Median(best.L.Select(t => t.Mfe)).ToString("0.00", IC) + "R.\n");
        md.AppendLine("### Sizing: contracts = $ risk ÷ (stop points × $2), at least 1\n");
        head(new[] { "risk a trade", "net $ 2020–26", "max DD $", "worst day $", "worst month $", "avg contracts" });
        foreach (var rb in new[] { 100, 250, 500, 1000 })
        {
            double eq = 0, pk = 0, dd = 0; var sized = best.L.Select(t => { int n = Math.Max(1, (int)Math.Floor(rb / (t.Risk * Pv))); return new { t.Day, Usd = t.Usd * n, N = n }; }).ToList();
            foreach (var t in sized) { eq += t.Usd; pk = Math.Max(pk, eq); dd = Math.Max(dd, pk - eq); }
            md.AppendLine("| $" + rb + " | " + eq.ToString("N0", IC) + " | " + dd.ToString("N0", IC) + " | " + sized.GroupBy(t => t.Day).Min(g => g.Sum(t => t.Usd)).ToString("N0", IC) + " | " + sized.GroupBy(t => t.Day.Year * 100 + t.Day.Month).Min(g => g.Sum(t => t.Usd)).ToString("N0", IC) + " | " + sized.Average(t => t.N).ToString("0.0", IC) + " |");
        }
        File.WriteAllText(Path.Combine(outDir, "orbsides.md"), md.ToString());
        File.WriteAllText(Path.Combine(outDir, "orbsides.json"), "{\"rows\":[" + string.Join(",", rows.Select(r => "{\"key\":\"" + r.V.Key + "\",\"is\":[" + string.Join(",", r.IS.Select(x => Math.Round(x, 3).ToString(IC))) + "],\"oos\":[" + string.Join(",", r.OOS.Select(x => Math.Round(x, 3).ToString(IC))) + "]}")) + "]}");
        Console.WriteLine("done");
    }

    static double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToList(); return a.Count == 0 ? 0 : a[a.Count / 2]; }

    static List<Tr> Trades(Day x, V v)
    {
        var d = x.D; var res = new List<Tr>(); int o0 = R.S(9, 30), o1 = o0 + v.Orb, end = R.S(v.End, 0), flat = R.S(15, 55);
        double hi = d.Hi(o0, o1 - 1), lo = d.Lo(o0, o1 - 1); if (double.IsNaN(hi) || double.IsNaN(lo) || hi - lo < 2) return res;
        Func<int, double[]> bar = b0 => { double h = double.NaN, l = double.NaN, c = double.NaN; for (int k = b0; k < b0 + 5; k++) if (d.Has(k)) { h = double.IsNaN(h) ? d.H[k] : Math.Max(h, d.H[k]); l = double.IsNaN(l) ? d.L[k] : Math.Min(l, d.L[k]); c = d.C[k]; } return new[] { h, l, c }; };
        int from = o1, lastDir = 0;
        while (res.Count < (v.Two ? 2 : 1))
        {
            int dir = 0, brk = -1; double[] bb = null;
            for (int b0 = from; b0 + 4 < end; b0 += 5)
            {
                if ((b0 - o0) % 5 != 0) continue; var b = bar(b0); if (double.IsNaN(b[2])) continue; double vw = x.Vw[b0 + 4];
                bool up = b[2] > hi && (!v.FV || b[2] > vw), dn = b[2] < lo && (!v.FV || b[2] < vw);
                if (up && lastDir != 1) { dir = 1; brk = b0 + 4; bb = b; break; }
                if (dn && lastDir != -1) { dir = -1; brk = b0 + 4; bb = b; break; }
            }
            if (brk < 0) break;
            int fill = brk + 1; while (fill < end && !d.Has(fill)) fill++; if (fill >= end) break;
            double entry = d.O[fill] + dir * Tick;
            double stop = v.Stop == 0 ? (dir > 0 ? lo : hi) : v.Stop == 1 ? (hi + lo) / 2 : v.Stop == 2 ? x.Vw[brk] - dir * Tick : (dir > 0 ? bb[1] : bb[0]) - dir * Tick;
            double risk = (entry - stop) * dir; if (double.IsNaN(risk) || risk < 2) break;
            double tR = new[] { 0, 1, 1.5, 2, 3 }[v.Tgt], tgt = tR > 0 ? entry + dir * tR * risk : double.NaN, pts = double.NaN, mfe = 0, curStop = stop; bool be = false; int exitK = flat;
            for (int k = fill; k <= flat; k++)
            {
                if (!d.Has(k)) continue;
                if (dir > 0 ? d.L[k] <= curStop : d.H[k] >= curStop) { pts = (curStop - entry) * dir - Tick; exitK = k; break; }
                if (!double.IsNaN(tgt) && (dir > 0 ? d.H[k] >= tgt : d.L[k] <= tgt)) { pts = tR * risk; exitK = k; break; }
                mfe = Math.Max(mfe, ((dir > 0 ? d.H[k] : d.L[k]) - entry) * dir / risk);
                // management acts at the minute's close, for the next minute on
                if ((v.Mg == 1 || v.Mg == 3) && !be && mfe >= 1) { be = true; curStop = entry + dir * Tick; }
                if ((k - o0) % 5 == 4 && k > fill)
                {
                    if (v.Mg == 2 && !double.IsNaN(x.Vw[k]) && (dir > 0 ? d.C[k] < x.Vw[k] : d.C[k] > x.Vw[k])) { pts = (d.C[k] - entry) * dir - Tick; exitK = k; break; }
                    if (v.Mg == 3 && be) { var pb = bar(k - 4); double ts = dir > 0 ? pb[1] - Tick : pb[0] + Tick; if (!double.IsNaN(ts) && (dir > 0 ? ts > curStop : ts < curStop)) curStop = ts; }
                }
            }
            if (double.IsNaN(pts)) pts = (d.CloseAt(flat) - entry) * dir;
            double usd = pts * Pv - Cost; res.Add(new Tr { Day = d.Day, Dir = dir, R = usd / (risk * Pv), Usd = usd, Risk = risk, Mfe = mfe });
            lastDir = dir; from = o0 + ((exitK - o0) / 5 + 1) * 5;   // look for the other way from the next 5-minute candle
            if (from + 4 >= end) break;
        }
        return res;
    }
}
