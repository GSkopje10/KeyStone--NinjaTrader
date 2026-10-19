// FAST ROTATION — a frequent 1-minute strategy for a POOL of prop accounts: every setup goes to the next free account (one trade
// per account per day), so with enough setups every account trades every day — as fast as copying, but each account its own trade
// (they do not all blow together). Firm rule kept: never a position against one that is open on another account (no hedging).
// Signals 09:30–15:00 on MNQ 1-minute bars, 10 MNQ, flat 15:55: 1-MIN FVG retrace (limit at 10% / 50% of the gap, valid 15 min,
// gap ≥ 1 point) • 1-MIN MOMENTUM BURST (a candle ≥ 2× the last 30 minutes' average range → its side at the next open) • RANDOM
// every 5 min (control). Brackets ±$500 … ±$1,500. LUCID FLEX 50K rules, trailing drawdown trade by trade; a blown / retired slot
// gets a new evaluation the next day. Chosen on 2020–23, judged on 2024–26.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class FastRotateStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);
    public sealed class T1 { public int Slot, Exit, Dir; public double Pnl, Mfe; }
    static readonly string[] SigName = { "1-MIN FVG • limit 10%", "1-MIN FVG • limit 50%", "1-MIN MOMENTUM BURST", "RANDOM every 5 min (control)", "30-MIN BREAKOUT (follow)", "30-MIN BREAKOUT (fade)", "VWAP STRETCH (fade)", "VWAP CROSS (follow)", "EXHAUSTION: 5 candles one way (fade)", "BIG CANDLE (fade)", "PREVIOUS DAY HIGH / LOW BREAK (follow)", "1-MIN FVG 10% + WITH THE 30-MIN TREND", "1-MIN FVG 10% + ON THE VWAP SIDE", "1-MIN FVG 10% + TREND + VWAP" };
    static readonly double[] BrUsd = { 500, 750, 1000, 1500 };
    public static bool Invert;   // INVERSION: the same FVGs and prices, the opposite side (a stop order at the limit's level)

    public static List<T1> Signals(RDay x, int sig, double usd)
    {
        var o = new List<T1>(); int ws = R.S(9, 30), we = R.S(15, 0), flat = R.S(15, 55); double pts = usd / 20.0;
        Action<int, double, int, bool> take = (k, e, d, lim) => { double p = FreqStudy.Walk(x, k, e, d, pts, pts, flat, lim); o.Add(new T1 { Slot = k, Exit = FreqStudy.lastExit, Dir = d, Pnl = p, Mfe = Math.Max(p, FreqStudy.lastMfe) }); };
        if (sig <= 1 || sig >= 11)
        {
            double pct = sig == 1 ? 0.50 : 0.10;
            // filters (known at the FVG's third candle): the 30-min trend = the side of the last close beyond the prior 30-min range; VWAP side
            var trend = new int[R.Slots]; var vw = new double[R.Slots]; double pv = 0, vv = 0; int tr = 0;
            for (int k = ws; k < we; k++) { if (x.Has(k)) { double tp = (x.H[k] + x.L[k] + x.C[k]) / 3, vol = Math.Max(1, x.V[k]); pv += tp * vol; vv += vol; if (k - ws >= 30) { double hh = x.Hi(k - 30, k - 1), ll = x.Lo(k - 30, k - 1); if (x.C[k] > hh) tr = 1; else if (x.C[k] < ll) tr = -1; } } trend[k] = tr; vw[k] = vv > 0 ? pv / vv : double.NaN; }
            for (int c3 = ws + 2; c3 < we; c3++)
            {
                int c1 = c3 - 2; if (!x.Has(c1) || !x.Has(c3)) continue;
                int d = x.L[c3] > x.H[c1] + 1 - 1e-9 ? 1 : x.H[c3] < x.L[c1] - 1 + 1e-9 ? -1 : 0; if (d == 0) continue;
                if ((sig == 11 || sig == 13) && trend[c3] != d) continue;
                if ((sig == 12 || sig == 13) && (double.IsNaN(vw[c3]) || (d > 0 ? x.C[c3] < vw[c3] : x.C[c3] > vw[c3]))) continue;
                double lo = d > 0 ? x.H[c1] : x.H[c3], hi = d > 0 ? x.L[c3] : x.L[c1], lv = d > 0 ? hi - pct * (hi - lo) : lo + pct * (hi - lo); lv = Math.Round(lv / 0.25) * 0.25;
                for (int k = c3 + 1; k <= Math.Min(c3 + 15, we); k++) { if (!x.Has(k)) continue; if (d > 0 ? x.L[k] <= lv : x.H[k] >= lv) { take(k, d > 0 ? Math.Min(lv, x.O[k]) : Math.Max(lv, x.O[k]), Invert ? -d : d, true); break; } }
            }
        }
        else if (sig == 2)
        {
            for (int k = ws + 30; k < we; k++)
            {
                if (!x.Has(k) || !x.Has(k + 1)) continue; double avg = 0; int n = 0; for (int j = k - 30; j < k; j++) if (x.Has(j)) { avg += x.H[j] - x.L[j]; n++; }
                if (n < 20) continue; avg /= n; double rg = x.H[k] - x.L[k]; if (rg < 2 * avg || x.C[k] == x.O[k]) continue;
                take(k + 1, x.O[k + 1], x.C[k] > x.O[k] ? 1 : -1, false);
            }
        }
        else if (sig >= 4)
        {
            // market at the next minute's open; one signal per family every 10 minutes at most
            int cool = -999; double pv = 0, vv = 0; int run = 0;
            double pdh = double.NaN, pdl = double.NaN; bool upDone = false, dnDone = false;
            if (x.Prev != null) { pdh = x.Prev.Hi(R.S(9, 30), R.S(15, 59)); pdl = x.Prev.Lo(R.S(9, 30), R.S(15, 59)); }
            for (int k = ws; k < we; k++)
            {
                if (!x.Has(k)) continue; double tp = (x.H[k] + x.L[k] + x.C[k]) / 3, vol = Math.Max(1, x.V[k]); pv += tp * vol; vv += vol; double vwap = pv / vv;
                run = x.C[k] > x.O[k] ? (run > 0 ? run + 1 : 1) : x.C[k] < x.O[k] ? (run < 0 ? run - 1 : -1) : 0;
                if (k + 1 >= we || !x.Has(k + 1) || k - cool < 10) continue;
                double avg = 0; int n = 0; for (int j = Math.Max(ws, k - 30); j < k; j++) if (x.Has(j)) { avg += x.H[j] - x.L[j]; n++; } avg = n > 0 ? avg / n : 0;
                int d = 0;
                if (sig == 4 || sig == 5) { if (k - ws >= 30) { double hh = x.Hi(k - 30, k - 1), ll = x.Lo(k - 30, k - 1); d = x.C[k] > hh ? 1 : x.C[k] < ll ? -1 : 0; if (sig == 5) d = -d; } }
                else if (sig == 6) { if (n >= 10 && Math.Abs(x.C[k] - vwap) >= 8 * avg) d = x.C[k] > vwap ? -1 : 1; }
                else if (sig == 7) { if (k > ws + 10 && x.Has(k - 1)) { double pc = x.C[k - 1]; d = pc <= vwap && x.C[k] > vwap ? 1 : pc >= vwap && x.C[k] < vwap ? -1 : 0; } }
                else if (sig == 8) { if (Math.Abs(run) >= 5) d = run > 0 ? -1 : 1; }
                else if (sig == 9) { if (n >= 20 && x.H[k] - x.L[k] >= 2 * avg && x.C[k] != x.O[k]) d = x.C[k] > x.O[k] ? -1 : 1; }
                else if (sig == 10) { if (!double.IsNaN(pdh)) { if (!upDone && x.C[k] > pdh) { d = 1; upDone = true; } else if (!dnDone && x.C[k] < pdl) { d = -1; dnDone = true; } } }
                if (d == 0) continue; cool = k; take(k + 1, x.O[k + 1], d, false);
            }
        }
        else
        {
            for (int k = ws; k < we; k += 5) { if (!x.Has(k)) continue; int d = ((x.Day.DayOfYear * 31 + k * 7 + x.Day.Year) * 2654435761L % 1000) < 500 ? 1 : -1; take(k, x.O[k], d, false); }
        }
        return o.OrderBy(t => t.Slot).ToList();
    }

    sealed class Res { public int Sig, B, N; public AccountTypeStudy.Out A, Bo, All; public double PerDay, Skipped, PerYear; public bool Solo; public double Score { get { return Math.Min(A.Roi, Bo.Roi); } } }

    // a pool of n slots; each signal → the next slot that has not traded today; no position against an open one on another account
    public static AccountTypeStudy.Out Pool(List<DateTime> dates, List<List<T1>> sigs, int from, int to, KeystoneSimRules r, int n, out double taken, out double skipped, bool solo = false)
    {
        var o = new AccountTypeStudy.Out(); var b = new AccountTypeStudy.Batch(); var acc = new AccountTypeStudy.Acc[n]; int last = -1; taken = 0; skipped = 0;
        for (int i = from; i < to; i++)
        {
            var day = dates[i]; string mk = day.ToString("yyyy-MM", IC); if (!o.Month.ContainsKey(mk)) o.Month[mk] = new double[4];
            for (int s = 0; s < n; s++) if (acc[s] == null) { acc[s] = new AccountTypeStudy.Acc { Id = s, Start = day }; o.Spent += r.EvalCost; o.Bought++; o.Month[mk][0] += r.EvalCost; }
            var used = new bool[n]; var open = new List<T1>();
            foreach (var t in sigs[i])
            {
                if (open.Any(q => q.Exit >= t.Slot && (solo || q.Dir != t.Dir))) { skipped++; continue; }   // no hedging across accounts
                int pick = -1; for (int j = 1; j <= n; j++) { int s = (last + j) % n; if (!used[s] && acc[s] != null && acc[s].End == "") { pick = s; break; } }
                if (pick < 0) { skipped++; continue; }
                last = pick; used[pick] = true; open.Add(t); taken++;
                var a = acc[pick]; AccountTypeStudy.Step(a, t.Pnl, t.Mfe, r, day, o, b);
                if (a.End == "PASSED") { o.Passed++; o.Month[mk][3]++; acc[pick] = new AccountTypeStudy.Acc { Id = pick, Funded = true, FundedOn = day }; o.Spent += r.Activation; }
                else if (a.End != "") acc[pick] = null;
            }
            if (o.Kept - o.Spent < o.Low) { o.Low = o.Kept - o.Spent; o.LowDay = day; }
        }
        foreach (var m in o.Month.Values) { double v = m[1] - m[0]; if (v > 0) o.MonthsUp++; else if (v < 0) o.MonthsDown++; }
        return o;
    }

    // NORMAL vs INVERTED: the same FVG families (plain, VWAP side, trend, both), brackets and account counts, the side flipped
    public static void Inversion(List<RDay> mnq, string outDir)
    {
        var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var days = mnq.OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int split = dates.FindIndex(d => d >= Half); double yrs = (dates[dates.Count - 1] - dates[0]).TotalDays / 365.25;
        var r = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID FLEX 50K")).Copy();
        var rows = new List<string[]>();
        foreach (int sg in new[] { 0, 12, 11, 13 }) foreach (double br in new[] { 1000.0, 1500.0 }) foreach (bool inv in new[] { false, true })
        {
            Invert = inv; var sigs = days.Select(d => Signals(d, sg, br)).ToList(); Invert = false;
            var all = sigs.SelectMany(l => l).ToList(); double win = all.Count == 0 ? 0 : 100.0 * all.Count(t => t.Pnl > 0) / all.Count, avg = all.Count == 0 ? 0 : all.Average(t => t.Pnl);
            foreach (bool solo in new[] { true, false })
            {
                double tk, sk, tk2, sk2, tk3, sk3; int n = 10;
                var a = Pool(dates, sigs, 0, split, r, n, out tk, out sk, solo); var b = Pool(dates, sigs, split, dates.Count, r, n, out tk2, out sk2, solo); var o = Pool(dates, sigs, 0, dates.Count, r, n, out tk3, out sk3, solo);
                var row = new[] { SigName[sg], inv ? "INVERTED" : "normal", "±$" + br.ToString("N0", IC), solo ? "one at a time" : "several, same side", (tk3 / dates.Count).ToString("0.0", IC), win.ToString("0.0", IC) + "%", avg.ToString("0", IC), a.Roi.ToString("0.00", IC) + "×", b.Roi.ToString("0.00", IC) + "×", (o.Net / yrs).ToString("N0", IC), o.Low.ToString("N0", IC), o.Bought.ToString(IC), (100.0 * o.Passed / Math.Max(1, o.Bought)).ToString("0", IC) + "%", o.Payouts.ToString(IC), o.MonthsUp + " / " + o.MonthsDown };
                rows.Add(row); say(string.Join(" | ", row));
            }
        }
        var h = new StringBuilder();
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>FVG Inversion Test</title><style>:root{--bg:#0b0f14;--card:#141a22;--card2:#1b2330;--line:#2a3442;--text:#e8eef5;--mute:#8d9aab;--pos:#3fb950;--neg:#f85149;--acc:#e3b341}@media (prefers-color-scheme: light){:root{--bg:#f5f7fa;--card:#fff;--card2:#f0f3f7;--line:#d8dee6;--text:#1c2430;--mute:#5d6b7c;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700}}*{box-sizing:border-box}body{background:var(--bg);color:var(--text);font:14px/1.55 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:28px 16px}main{max-width:1280px;margin:0 auto}h1{font-size:26px;margin:0 0 6px}.mute{color:var(--mute)}.wrap{overflow-x:auto;border:1px solid var(--line);border-radius:10px}table{border-collapse:collapse;width:100%;background:var(--card);font-variant-numeric:tabular-nums}th,td{padding:6px 9px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th{background:var(--card2);color:var(--mute)}td.l,th.l{text-align:left}tr.inv td{color:var(--acc)}</style></head><body><main>");
        h.Append("<h1>FVG inversion • normal vs inverted</h1><p class='mute'>MNQ " + dates[0].ToString("yyyy-MM-dd", IC) + " → " + dates[dates.Count - 1].ToString("yyyy-MM-dd", IC) + " • the same 1-minute FVGs (gap ≥ 1 pt, 09:30–15:00), the same price (10% of the gap) and 15-minute validity — INVERTED trades the other side with a stop order at that price • 10 MNQ, flat 15:55 • 10 LUCID FLEX 50K accounts in rotation (one trade per account per day), trailing drawdown trade by trade • ROI = money back ÷ money spent.</p><div class='wrap'><table><tr><th class='l'>SIGNAL</th><th class='l'>SIDE</th><th>BRACKET</th><th class='l'>POSITIONS</th><th>TRADES / DAY</th><th>WIN %</th><th>AVG $ / TRADE</th><th>ROI 20–23</th><th>ROI 24–26</th><th>NET / YEAR</th><th>LOWEST</th><th>EVALS</th><th>PASS %</th><th>PAYOUTS</th><th>MONTHS +/−</th></tr>");
        foreach (var x in rows) { h.Append("<tr" + (x[1] == "INVERTED" ? " class='inv'" : "") + ">"); for (int i = 0; i < x.Length; i++) h.Append("<td" + (i < 2 || i == 3 ? " class='l'" : "") + ">" + System.Net.WebUtility.HtmlEncode(x[i]) + "</td>"); h.Append("</tr>"); }
        h.Append("</table></div></main></body></html>");
        Directory.CreateDirectory(outDir); File.WriteAllText(System.IO.Path.Combine(outDir, "fvg_inversion.html"), h.ToString()); File.WriteAllText(System.IO.Path.Combine(outDir, "fvg_inversion_log.txt"), log.ToString());
    }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var days = mnq.OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int split = dates.FindIndex(d => d >= Half); double yrs = (dates[dates.Count - 1] - dates[0]).TotalDays / 365.25;
        var r = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID FLEX 50K")).Copy();
        var res = new List<Res>();
        for (int sg = 0; sg < SigName.Length; sg++) for (int bi = 0; bi < BrUsd.Length; bi++)
        {
            var sigs = days.Select(d => Signals(d, sg, BrUsd[bi])).ToList(); double perDay = sigs.Average(l => l.Count);
            foreach (bool solo in new[] { false, true }) foreach (int n in new[] { 10, 15, 20, 30, 50 })
            {
                if (solo && n > 15) continue;
                double tk, sk, tk2, sk2, tk3, sk3;
                var x = new Res { Sig = sg, B = bi, N = n, A = Pool(dates, sigs, 0, split, r, n, out tk, out sk, solo), Bo = Pool(dates, sigs, split, dates.Count, r, n, out tk2, out sk2, solo), All = Pool(dates, sigs, 0, dates.Count, r, n, out tk3, out sk3, solo), Solo = solo };
                x.PerDay = tk3 / dates.Count; x.Skipped = sk3 / dates.Count; x.PerYear = x.All.Net / yrs; res.Add(x);
                say(SigName[sg] + " • ±$" + BrUsd[bi] + " • " + n + " accounts" + (solo ? " • ONE AT A TIME" : "") + " • signals/day " + perDay.ToString("0.0") + " taken/day " + x.PerDay.ToString("0.0") + " • ROI " + x.A.Roi.ToString("0.00") + " / " + x.Bo.Roi.ToString("0.00") + " • net/yr " + x.PerYear.ToString("N0", IC) + " • low " + x.All.Low.ToString("N0", IC) + " • pass " + (100.0 * x.All.Passed / Math.Max(1, x.All.Bought)).ToString("0") + "% • paid " + x.All.FundedPaid + "/" + x.All.Passed + " • months " + x.All.MonthsUp + "/" + x.All.MonthsDown);
            }
        }
        var h = new StringBuilder(); Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC); Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Fast Rotation Study</title><style>:root{--bg:#0b0f14;--card:#141a22;--card2:#1b2330;--line:#2a3442;--text:#e8eef5;--mute:#8d9aab;--pos:#3fb950;--neg:#f85149;--acc:#e3b341}@media (prefers-color-scheme: light){:root{--bg:#f5f7fa;--card:#fff;--card2:#f0f3f7;--line:#d8dee6;--text:#1c2430;--mute:#5d6b7c;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700}}*{box-sizing:border-box}body{background:var(--bg);color:var(--text);font:14px/1.55 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:28px 16px}main{max-width:1280px;margin:0 auto}h1{font-size:26px;margin:0 0 6px}h2{font-size:19px;margin:30px 0 8px}.mute{color:var(--mute)}.pos{color:var(--pos)}.neg{color:var(--neg)}.wrap{overflow-x:auto;border:1px solid var(--line);border-radius:10px}table{border-collapse:collapse;width:100%;background:var(--card);font-variant-numeric:tabular-nums}th,td{padding:6px 9px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th{background:var(--card2);color:var(--mute)}td.l,th.l{text-align:left}</style></head><body><main>");
        h.Append("<h1>Fast rotation • a 1-minute strategy for a pool of accounts</h1><p class='mute'>MNQ " + dates[0].ToString("yyyy-MM-dd", IC) + " → " + dates[dates.Count - 1].ToString("yyyy-MM-dd", IC) + " • 09:30–15:00 signals, 10 MNQ, flat 15:55 • each setup → the next account that has not traded today • no position against an open one on another account • LUCID FLEX 50K, trailing drawdown trade by trade • a blown slot gets a new evaluation the next day • chosen by the worse of 2020–23 / 2024–26.</p>");
        h.Append("<div class='wrap'><table><tr><th class='l'>SIGNAL</th><th>BRACKET</th><th>ACCOUNTS</th><th class='l'>POSITIONS</th><th>TRADES / DAY</th><th>SKIPPED / DAY</th><th>ROI 20–23</th><th>ROI 24–26</th><th>NET / YEAR</th><th>LOWEST</th><th>EVALS (6 YRS)</th><th>PASS %</th><th>FUNDED PAID</th><th>PAYOUTS</th><th>MONTHS +/−</th></tr>");
        foreach (var x in res.OrderByDescending(x => x.Score)) h.Append("<tr><td class='l'>" + SigName[x.Sig] + "</td><td>±$" + BrUsd[x.B].ToString("N0", IC) + "</td><td>" + x.N + "</td><td class='l'>" + (x.Solo ? "one at a time" : "several, same side") + "</td><td>" + x.PerDay.ToString("0.0", IC) + "</td><td>" + x.Skipped.ToString("0.0", IC) + "</td><td class='" + cls(x.A.Roi - 1) + "'>" + x.A.Roi.ToString("0.00", IC) + "×</td><td class='" + cls(x.Bo.Roi - 1) + "'><b>" + x.Bo.Roi.ToString("0.00", IC) + "×</b></td><td class='" + cls(x.PerYear) + "'><b>" + usd(x.PerYear) + "</b></td><td class='neg'>" + usd(x.All.Low) + "</td><td>" + x.All.Bought + "</td><td>" + (100.0 * x.All.Passed / Math.Max(1, x.All.Bought)).ToString("0", IC) + "%</td><td>" + x.All.FundedPaid + " / " + x.All.Passed + "</td><td>" + x.All.Payouts + "</td><td>" + x.All.MonthsUp + " / " + x.All.MonthsDown + "</td></tr>");
        h.Append("</table></div></main></body></html>");
        Directory.CreateDirectory(outDir); File.WriteAllText(System.IO.Path.Combine(outDir, "fast_rotation.html"), h.ToString()); File.WriteAllText(System.IO.Path.Combine(outDir, "fast_rotation_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
