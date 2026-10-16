// FREQUENT ENTRIES FOR PROP ROTATION — the score is ROI = payouts ÷ evaluation fees (a blown evaluation costs its $120, nothing more).
// Many setups a day, each trade to the NEXT account in turn (one trade per account per day), 50 evaluations of LUCID FLEX 50K:
// pass first, then the funded accounts, then the next batch. MNQ, 10 contracts, $ target / stop, flat 2 h after the window (≤ 15:55).
//   FVG RETRACE  every 5-min FVG in the window → a limit at 10% / 50% of it (valid 60 min)
//   MOMENTUM     every 15 min: the side of the last 15 min, at the close
//   FADE         every 15 min: against the last 15 min, at the close
// Fills: the limit at its price (or the open through it), the entry minute can only stop out; then the stop before the target on the
// minute's range; flat at the time exit. Costs $1.74 a contract. CHOSEN on 2020–23 (ROI), JUDGED on 2024–26.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class FreqStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);
    public static readonly string[] Models = { "FVG RETRACE 10%", "FVG RETRACE 50%", "MOMENTUM 15M", "FADE 15M", "RANDOM SIDE 15M (control)" };
    public static readonly string[] Wins = { "NY OPEN 09:30–11:30", "NY DAY 09:30–15:00", "LONDON 03:00–08:00", "ASIA 20:00–00:00", "WHOLE SESSION 18:00–15:00" };
    static readonly int[][] WinHm = { new[] { 930, 1130 }, new[] { 930, 1500 }, new[] { 300, 800 }, new[] { 2000, 2359 }, new[] { 1800, 1500 } };
    static readonly double[][] Br = { new[] { 500.0, 500 }, new[] { 750.0, 500 }, new[] { 1000.0, 500 }, new[] { 1000.0, 1000 }, new[] { 1500.0, 1000 }, new[] { 2000.0, 1000 }, new[] { 1500.0, 1500 } };
    static readonly int[] MaxDay = { 1, 2, 3, 5, 10 };
    const int Q = 10; const double PV = 2;

    public sealed class Tr { public int Slot, Exit; public double Pnl; }
    public sealed class Res
    {
        public int M, W, B, X; public int Trades; public double WinPct, Live; public OpenFvgEdge.Odds A, Bo, All; public double FirstPayDays;
        public string Name { get { return Models[M] + " • " + Wins[W] + " • +$" + Br[B][0].ToString("N0", IC) + " / −$" + Br[B][1].ToString("N0", IC) + " • ≤ " + MaxDay[X] + " a day"; } }
    }

    static int SlotOf(int hhmm) { return R.S(hhmm / 100, hhmm % 100); }

    // one trade from entry slot k0 at price e, side d → $ (net of costs)
    static int lastExit;
    static double Walk(RDay x, int k0, double e, int d, double tgtPts, double stpPts, int flat, bool limitBar)
    {
        lastExit = k0;
        double stop = e - d * stpPts, tgt = e + d * tgtPts, cost = KeystoneMoveStudy.Cost("MNQ") * Q;
        if (limitBar && x.Has(k0) && (d > 0 ? x.L[k0] <= stop : x.H[k0] >= stop)) return -stpPts * PV * Q - cost;
        double last = e;
        for (int k = k0 + 1; k <= flat; k++)
        {
            if (!x.Has(k)) continue; lastExit = k;
            if (d > 0 ? x.L[k] <= stop : x.H[k] >= stop) return -stpPts * PV * Q - cost;
            if (d > 0 ? x.H[k] >= tgt : x.L[k] <= tgt) return tgtPts * PV * Q - cost;
            last = x.C[k];
        }
        lastExit = flat; return d * (last - e) * PV * Q - cost;
    }

    // every trade of a day for a model / window / bracket (in entry order; overlapping trades allowed — they go to different accounts)
    public static List<Tr> DayTrades(RDay x, int model, int w, int b)
    {
        var o = new List<Tr>(); int ws = SlotOf(WinHm[w][0]), we = SlotOf(WinHm[w][1]); if (we < ws) we = R.Slots - 1;
        int flat = Math.Min(we + 120, SlotOf(1555)); if (flat <= ws) flat = SlotOf(1555);
        double tgt = Br[b][0] / (PV * Q), stp = Br[b][1] / (PV * Q);
        if (model <= 1)
        {
            double pct = model == 0 ? 0.10 : 0.50;
            for (int c3 = ws + 10; c3 + 5 < we; c3 += 5)   // 5-min candles from the window start (09:30 candle = first c1, as the app); c3 = the third candle's first slot
            {
                int c1 = c3 - 10, c2 = c3 - 5; double h1 = x.Hi(c1, c1 + 4), l1 = x.Lo(c1, c1 + 4), h3 = x.Hi(c3, c3 + 4), l3 = x.Lo(c3, c3 + 4);
                if (double.IsNaN(h1) || double.IsNaN(h3)) continue;
                int d = l3 > h1 ? 1 : h3 < l1 ? -1 : 0; if (d == 0) continue;
                double lo = d > 0 ? h1 : h3, hi = d > 0 ? l3 : l1; double lv = d > 0 ? hi - pct * (hi - lo) : lo + pct * (hi - lo); lv = Math.Round(lv / 0.25) * 0.25;
                int until = Math.Min(c3 + 5 + 60, we);
                for (int k = c3 + 5; k <= until; k++)
                {
                    if (!x.Has(k)) continue;
                    if (d > 0 ? x.L[k] <= lv : x.H[k] >= lv) { double f = d > 0 ? Math.Min(lv, x.O[k]) : Math.Max(lv, x.O[k]); double pnl0 = Walk(x, k, f, d, tgt, stp, flat, true); o.Add(new Tr { Slot = k, Exit = lastExit, Pnl = pnl0 }); break; }
                }
            }
        }
        else
        {
            for (int k = ws + 15; k <= we; k += 15)
            {
                double c0 = x.CloseAt(k - 15), c1 = x.CloseAt(k); if (double.IsNaN(c0) || double.IsNaN(c1) || c1 == c0 || !x.Has(k)) continue;
                int d = Math.Sign(c1 - c0); if (model == 3) d = -d; if (model == 4) d = ((x.Day.DayOfYear * 31 + k * 7 + x.Day.Year) * 2654435761L % 1000) < 500 ? 1 : -1;   // the control: a coin flip (fixed per day + time)
                double pnl1 = Walk(x, k, c1, d, tgt, stp, flat, false); o.Add(new Tr { Slot = k, Exit = lastExit, Pnl = pnl1 });
            }
        }
        return o.OrderBy(t => t.Slot).ToList();
    }

    // FVG RETRACE exactly as the app's chart trades it (KeystoneFvgRetrace + AUTO): one position; a setup that forms while a position is
    // open is skipped; a newer setup replaces a limit still waiting. from 1 = the candle before the window start counts (the 09:30 candle
    // may be an FVG's middle).
    public static List<Tr> AppDay(RDay x, int w, int from, int b, double pct, bool defer = true)
    {
        var o = new List<Tr>(); int ws = SlotOf(WinHm[w][0]), we = SlotOf(WinHm[w][1]); if (we < ws) we = R.Slots - 1;
        int flat = Math.Min(we + 120, SlotOf(1555)); if (flat <= ws) flat = SlotOf(1555); double tgt = Br[b][0] / (PV * Q), stp = Br[b][1] / (PV * Q);
        var cands = new List<double[]>();   // start slot, side, level, last slot
        for (int c3 = ws + 10 - 5 * from; c3 + 5 < we; c3 += 5)
        {
            int c1 = c3 - 10; double h1 = x.Hi(c1, c1 + 4), l1 = x.Lo(c1, c1 + 4), h3 = x.Hi(c3, c3 + 4), l3 = x.Lo(c3, c3 + 4);
            if (double.IsNaN(h1) || double.IsNaN(h3)) continue; int d = l3 > h1 ? 1 : h3 < l1 ? -1 : 0; if (d == 0) continue;
            double lo = d > 0 ? h1 : h3, hi = d > 0 ? l3 : l1, lv = d > 0 ? hi - pct * (hi - lo) : lo + pct * (hi - lo); lv = Math.Round(lv / 0.25) * 0.25;
            cands.Add(new double[] { c3 + 5, d, lv, Math.Min(c3 + 5 + 60, we) });
        }
        int busy = -1; double[] pend = null;
        Action<int> run = upto =>
        {
            if (pend == null) return; int st = (int)pend[0], un = (int)pend[3], d = (int)pend[1]; double lv = pend[2];
            for (int k = st; k <= Math.Min(un, upto); k++)
            {
                if (!x.Has(k)) continue;
                if (d > 0 ? x.L[k] <= lv : x.H[k] >= lv) { double f = d > 0 ? Math.Min(lv, x.O[k]) : Math.Max(lv, x.O[k]); double p = Walk(x, k, f, d, tgt, stp, flat, true); o.Add(new Tr { Slot = k, Exit = lastExit, Pnl = p }); busy = lastExit; pend = null; return; }
            }
            if (un <= upto) pend = null;
        };
        // defer: a setup that forms while a trade is open waits for the exit (if still valid then, the newest one)
        foreach (var cd in cands) { run((int)cd[0] - 1); if (cd[0] <= busy) { if (defer && cd[3] > busy) { var c2 = (double[])cd.Clone(); c2[0] = busy + 1; pend = c2; } continue; } pend = cd; }
        run(int.MaxValue);
        return o;
    }

    // 50 evaluations, pass first, then funded, then the next batch; each trade to the next account that has not traded today
    public static OpenFvgEdge.Odds Prop(List<Tuple<DateTime, List<Tr>>> days, int maxDay, KeystoneSimRules r, int n, out double firstPayDays)
    {
        var o = new OpenFvgEdge.Odds(); List<OpenFvgStudy.Acct> evals = null, funded = null, passed = null; int last = -1; double net = 0; var paid = new HashSet<OpenFvgStudy.Acct>(); firstPayDays = -1; int dayNo = 0;
        foreach (var dd in days)
        {
            dayNo++;
            if (evals == null && funded == null) { evals = Enumerable.Range(0, n).Select(k => new OpenFvgStudy.Acct { Id = k }).ToList(); passed = new List<OpenFvgStudy.Acct>(); last = -1; o.Batches++; o.Evals += n; o.Spent += n * r.EvalCost; net -= n * r.EvalCost; }
            var pool = evals ?? funded; var used = new HashSet<OpenFvgStudy.Acct>(); int taken = 0;
            foreach (var t in dd.Item2)
            {
                if (taken >= maxDay) break;
                var live = pool.Where(a => a.End == "" && !used.Contains(a)).OrderBy(a => a.Id).ToList(); if (live.Count == 0) break;
                var a0 = live.FirstOrDefault(a => a.Id > last) ?? live[0]; last = a0.Id; used.Add(a0); taken++;
                double k0 = a0.Kept; OpenFvgStudy.Step(a0, t.Pnl, r);
                if (a0.Kept > k0) { o.Kept += a0.Kept - k0; net += a0.Kept - k0; o.Payouts++; paid.Add(a0); if (firstPayDays < 0) firstPayDays = dayNo; }
                if (a0.End == "PASSED") { o.Passed++; o.Funded++; passed.Add(new OpenFvgStudy.Acct { Id = a0.Id, Funded = true }); }
            }
            o.Low = Math.Min(o.Low, net);
            if (evals != null && evals.All(a => a.End != "")) { evals = null; funded = passed.Count > 0 ? passed : null; last = -1; }
            else if (funded != null && funded.All(a => a.End != "")) funded = null;
        }
        o.FundedPaid = paid.Count(a => a.Funded);
        return o;
    }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var days = mnq.OrderBy(d => d.Day).ToList(); var rules = KeystoneSimRules.Presets()[1].Copy();
        say("FREQUENT ENTRIES • " + days[0].Day.ToString("yyyy-MM-dd") + " → " + days[days.Count - 1].Day.ToString("yyyy-MM-dd") + " • " + days.Count + " days");
        var res = new List<Res>(); double perYear = days.Count / 6.0; var soloRows = new List<Tuple<string, OpenFvgEdge.Odds, OpenFvgEdge.Odds, double, double>>();
        for (int m = 0; m < Models.Length; m++) for (int w = 0; w < Wins.Length; w++) for (int b = 0; b < Br.Length; b++)
        {
            var all = days.Select(d => Tuple.Create(d.Day.Date, DayTrades(d, m, w, b))).ToList();
            var A = all.Where(x => x.Item1 < Half).ToList(); var B = all.Where(x => x.Item1 >= Half).ToList();
            for (int xi = 0; xi < MaxDay.Length; xi++)
            {
                int mx = MaxDay[xi]; double fp, fpA, fpB;
                var r = new Res { M = m, W = w, B = b, X = xi };
                var taken = all.SelectMany(d => d.Item2.Take(mx)).ToList(); r.Trades = taken.Count; r.WinPct = taken.Count == 0 ? 0 : 100.0 * taken.Count(t => t.Pnl > 0) / taken.Count; r.Live = taken.Sum(t => t.Pnl);
                r.A = Prop(A, mx, rules, 50, out fpA); r.Bo = Prop(B, mx, rules, 50, out fpB); r.All = Prop(all, mx, rules, 50, out fp); r.FirstPayDays = fp;
                res.Add(r);
            }
        }
        say(res.Count + " combinations in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        // the baseline: OPENING FVG (one a day) from the edge study's best, run through the same prop
        var picks = res.Where(x => x.A.Spent > 0 && x.Trades >= 300).OrderByDescending(x => x.A.Roi).Take(30).ToList();
        foreach (var x in picks.Take(12)) say("  " + x.Name + " • A ROI " + x.A.Roi.ToString("0.00") + "× net " + x.A.Net.ToString("N0", IC) + " → B ROI " + x.Bo.Roi.ToString("0.00") + "× net " + x.Bo.Net.ToString("N0", IC) + " (pass " + (100 * x.Bo.PassRate).ToString("0") + "%, paid " + (100 * x.Bo.PayoutRate).ToString("0") + "%, evals " + x.Bo.Evals + ")");
        var held = picks.Where(x => x.Bo.Roi > 1 && x.A.Roi > 1).ToList();
        var winner = held.OrderByDescending(x => Math.Min(x.A.Roi, x.Bo.Roi)).FirstOrDefault() ?? picks[0];   // the steadiest: best ROI in its WORSE period
        // ONE AT A TIME (as the chart trades it: one position per instrument) — the pick's settings, overlapping trades dropped
        {
            Func<List<Tr>, List<Tr>> solo = l => { var o2 = new List<Tr>(); int busy = -1; foreach (var t in l) { if (t.Slot <= busy) continue; o2.Add(t); busy = t.Exit; } return o2; };
            foreach (var mm in new[] { 0, 2, 4 })
            {
                var all1 = days.Select(d => Tuple.Create(d.Day.Date, solo(DayTrades(d, mm, winner.W, winner.B)))).ToList(); double f1;
                var oa = Prop(all1.Where(x => x.Item1 < Half).ToList(), MaxDay[winner.X], rules, 50, out f1); var ob = Prop(all1.Where(x => x.Item1 >= Half).ToList(), MaxDay[winner.X], rules, 50, out f1);
                var tk = all1.SelectMany(d => d.Item2.Take(MaxDay[winner.X])).ToList();
                soloRows.Add(Tuple.Create(Models[mm], oa, ob, tk.Count / (double)days.Count, tk.Sum(t => t.Pnl)));
                say("  ONE AT A TIME • " + Models[mm] + " • ROI A " + oa.Roi.ToString("0.00") + " B " + ob.Roi.ToString("0.00") + " • net B " + ob.Net.ToString("N0", IC) + " • " + (tk.Count / (double)days.Count).ToString("0.0") + " trades/day • live " + tk.Sum(t => t.Pnl).ToString("N0", IC));
            }
        }
        // AS THE APP TRADES IT: FVG RETRACE 10% • window × the 09:30 candle as first / middle × bracket
        var appRows = new List<Tuple<string, OpenFvgEdge.Odds, OpenFvgEdge.Odds, double, double, double>>();
        foreach (int w in new[] { 0, 1 }) foreach (int f in new[] { 0, 1 }) foreach (int b in new[] { 6, 4, 3 })
        {
            var all2 = days.Select(d => Tuple.Create(d.Day.Date, AppDay(d, w, f, b, 0.10))).ToList(); double f2;
            var oa = Prop(all2.Where(x => x.Item1 < Half).ToList(), 99, rules, 50, out f2); var ob = Prop(all2.Where(x => x.Item1 >= Half).ToList(), 99, rules, 50, out f2);
            var tk = all2.SelectMany(d => d.Item2).ToList(); string nm = "FVG RETRACE 10% • " + Wins[w] + " • 09:30 candle " + (f == 0 ? "FIRST only" : "may be the MIDDLE") + " • +$" + Br[b][0].ToString("N0", IC) + " / −$" + Br[b][1].ToString("N0", IC);
            appRows.Add(Tuple.Create(nm, oa, ob, tk.Count / (double)days.Count, tk.Sum(t => t.Pnl), tk.Count == 0 ? 0 : 100.0 * tk.Count(t => t.Pnl > 0) / tk.Count));
            say("  AS THE APP TRADES IT • " + nm + " • ROI A " + oa.Roi.ToString("0.00") + " B " + ob.Roi.ToString("0.00") + " • net 6 yrs " + (oa.Net + ob.Net).ToString("N0", IC) + " • " + (tk.Count / (double)days.Count).ToString("0.0") + "/day • live " + tk.Sum(t => t.Pnl).ToString("N0", IC));
        }
        var ctrl = res.FirstOrDefault(x => x.M == 4 && x.W == winner.W && x.B == winner.B && x.X == winner.X);
        foreach (var mm in new[] { 0, 1, 2, 3, 4 }) { var cx = res.FirstOrDefault(x => x.M == mm && x.W == winner.W && x.B == winner.B && x.X == winner.X); if (cx != null) say("  SAME SETTINGS • " + Models[mm] + " • ROI A " + cx.A.Roi.ToString("0.00") + " B " + cx.Bo.Roi.ToString("0.00") + " • net B " + cx.Bo.Net.ToString("N0", IC) + " • won " + cx.WinPct.ToString("0.0") + "% • live " + cx.Live.ToString("N0", IC)); }
        say("PICK: " + winner.Name + " • B ROI " + winner.Bo.Roi.ToString("0.00") + "× • held " + held.Count + " of " + picks.Count);

        // ---- HTML
        var h = new StringBuilder();
        Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC);
        Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> pct = v => v.ToString("0.0", IC) + "%";
        Func<double, string> roi = v => v.ToString("0.00", IC) + "×";
        Func<double, string> rcls = v => v >= 1 ? "pos" : "neg";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Frequent Entries ROI</title><style>");
        h.Append(":root{--bg:#0d1117;--card:#161b22;--line:#30363d;--text:#e6edf3;--mute:#8b949e;--pos:#3fb950;--neg:#f85149;--acc:#d29922;--pbg:rgba(63,185,80,.16);--nbg:rgba(248,81,73,.16)}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f6f8fa;--card:#fff;--line:#d0d7de;--text:#1f2328;--mute:#59636e;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--pbg:rgba(26,127,55,.12);--nbg:rgba(207,34,46,.12)}}");
        h.Append("body{background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:24px 16px}main{max-width:1280px;margin:0 auto}h1{font-size:24px;margin:0 0 4px}h2{font-size:17px;margin:30px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}h3{font-size:14px;margin:16px 0 6px;color:var(--acc)}");
        h.Append(".sub{color:var(--mute)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:10px;margin:14px 0}.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:12px}.card b{display:block;font-size:17px}.card small{color:var(--mute)}");
        h.Append(".wrap{overflow-x:auto;margin-bottom:6px}table{border-collapse:collapse;width:100%;background:var(--card);border:1px solid var(--line);font-variant-numeric:tabular-nums}th,td{padding:5px 8px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}th{color:var(--mute);font-weight:600;font-size:12px}");
        h.Append(".pos{color:var(--pos)}.neg{color:var(--neg)}.hp{background:var(--pbg)}.hn{background:var(--nbg)}.best{outline:2px solid var(--acc);outline-offset:-2px}.note{background:var(--card);border-left:3px solid var(--acc);padding:10px 14px;border-radius:6px;margin:10px 0}td.w{white-space:normal;min-width:330px;text-align:left}</style></head><body><main>");
        h.Append("<h1>Frequent entries for prop rotation — the best ROI</h1><div class='sub'>" + days[0].Day.ToString("MMM d, yyyy", IC) + " → " + days[days.Count - 1].Day.ToString("MMM d, yyyy", IC) + " • " + res.Count + " combinations • MNQ 10 contracts • each trade to the next account in turn (one trade per account per day) • 50 evaluations of LUCID FLEX 50K ($120 each) • ROI = payouts ÷ fees (1.00× = break even) • chosen on 2020–23, judged on 2024–26</div>");
        Action<string, Res> card = (k, x) => h.Append("<div class='card'><small>" + k + "</small><b>" + x.Name + "</b><small>ROI 2020–23 <span class='" + rcls(x.A.Roi) + "'>" + roi(x.A.Roi) + "</span> → 2024–26 <span class='" + rcls(x.Bo.Roi) + "'><b>" + roi(x.Bo.Roi) + "</b></span><br>2024–26: " + x.Bo.Evals + " evaluations, pass " + pct(100 * x.Bo.PassRate) + ", funded paid " + pct(100 * x.Bo.PayoutRate) + ", " + x.Bo.Payouts + " payouts, net " + usd(x.Bo.Net) + "<br>6 years: " + (x.Trades / (days.Count / 1.0)).ToString("0.0", IC) + " trades a day, won " + pct(x.WinPct) + ", first payout after " + (x.FirstPayDays < 0 ? "–" : x.FirstPayDays + " days") + ", most $ down " + usd(x.All.Low) + "</small></div>");
        h.Append("<div class='cards'>"); card("THE PICK (best ROI in its worse period: 2020–23 vs 2024–26)", winner); h.Append("</div>");
        h.Append("<div class='note'><b>Why frequent can win here.</b> A blown evaluation costs its fee, a passed one can pay $1,000–2,000 several times. So the plan does not need a high win rate — it needs a good chance per evaluation of reaching the payouts, and enough setups to give every account its turn. The score below is only that: payouts ÷ fees. " + res.Count + " combinations were tried, so the best one is partly luck — the pick is chosen on 2020–23 and shown on 2024–26 it never saw.</div>");
        h.Append("<h2>0 • The pick's settings with every entry model — is it the entry or the structure?</h2><div class='wrap'><table><tr><th>ENTRY MODEL (same window, bracket, trades a day)</th><th>WON</th><th>ROI 2020–23</th><th>ROI 2024–26</th><th>NET 2024–26</th><th>PASS 24–26</th><th>FUNDED PAID 24–26</th><th>LIVE 6 YRS (own money)</th></tr>");
        foreach (var mm in new[] { 0, 1, 2, 3, 4 }) { var cx = res.FirstOrDefault(x => x.M == mm && x.W == winner.W && x.B == winner.B && x.X == winner.X); if (cx == null) continue; h.Append("<tr class='" + (cx == winner ? "best" : "") + "'><td>" + Models[mm] + "</td><td>" + pct(cx.WinPct) + "</td><td class='" + rcls(cx.A.Roi) + "'>" + roi(cx.A.Roi) + "</td><td class='" + rcls(cx.Bo.Roi) + "'><b>" + roi(cx.Bo.Roi) + "</b></td><td class='" + cls(cx.Bo.Net) + "'>" + usd(cx.Bo.Net) + "</td><td>" + pct(100 * cx.Bo.PassRate) + "</td><td>" + pct(100 * cx.Bo.PayoutRate) + "</td><td class='" + cls(cx.Live) + "'>" + usd(cx.Live) + "</td></tr>"); }
        h.Append("</table></div><h3>The same, ONE TRADE AT A TIME (as the chart holds one position per instrument)</h3><div class='wrap'><table><tr><th>ENTRY MODEL</th><th>TRADES/DAY</th><th>ROI 2020–23</th><th>ROI 2024–26</th><th>NET 2024–26</th><th>PASS 24–26</th><th>FUNDED PAID 24–26</th><th>LIVE 6 YRS</th></tr>");
        foreach (var x in soloRows) h.Append("<tr><td>" + x.Item1 + "</td><td>" + x.Item4.ToString("0.0", IC) + "</td><td class='" + rcls(x.Item2.Roi) + "'>" + roi(x.Item2.Roi) + "</td><td class='" + rcls(x.Item3.Roi) + "'><b>" + roi(x.Item3.Roi) + "</b></td><td class='" + cls(x.Item3.Net) + "'>" + usd(x.Item3.Net) + "</td><td>" + pct(100 * x.Item3.PassRate) + "</td><td>" + pct(100 * x.Item3.PayoutRate) + "</td><td class='" + cls(x.Item5) + "'>" + usd(x.Item5) + "</td></tr>");
        h.Append("</table></div><div class='note'>RANDOM SIDE is the control: a coin flip every 15 minutes. What it earns comes from the prop structure alone (a blown evaluation costs only its fee, payouts are taken as soon as allowed). An entry model is worth something only by what it earns ABOVE the coin flip.</div>");
        h.Append("<h2>★ As the app trades it (the default in the studio)</h2><div class='note'>The chart holds one position: a setup that forms while a trade is open is skipped, and a newer setup replaces a limit that is still waiting. Every trade goes to the next account; trades a day are limited only by the accounts.</div><div class='wrap'><table><tr><th>SETTINGS</th><th>TRADES/DAY</th><th>WON</th><th>ROI 2020–23</th><th>ROI 2024–26</th><th>NET 6 YRS</th><th>PASS 24–26</th><th>FUNDED PAID 24–26</th><th>LIVE 6 YRS</th></tr>");
        foreach (var x in appRows) h.Append("<tr class='" + (x.Item1.Contains("15:00") && x.Item1.Contains("MIDDLE") && x.Item1.Contains("1,500 / −$1,500") ? "best" : "") + "'><td class='w'>" + x.Item1 + "</td><td>" + x.Item4.ToString("0.0", IC) + "</td><td>" + pct(x.Item6) + "</td><td class='" + rcls(x.Item2.Roi) + "'>" + roi(x.Item2.Roi) + "</td><td class='" + rcls(x.Item3.Roi) + "'><b>" + roi(x.Item3.Roi) + "</b></td><td class='" + cls(x.Item2.Net + x.Item3.Net) + "'>" + usd(x.Item2.Net + x.Item3.Net) + "</td><td>" + pct(100 * x.Item3.PassRate) + "</td><td>" + pct(100 * x.Item3.PayoutRate) + "</td><td class='" + cls(x.Item5) + "'>" + usd(x.Item5) + "</td></tr>");
        h.Append("</table></div>");
        h.Append("<h2>1 • The 30 best ROI on 2020–23 — and on 2024–26</h2><div class='wrap'><table><tr><th>ENTRY MODEL</th><th>TRADES/DAY</th><th>WON</th><th>ROI 20–23</th><th>ROI 24–26</th><th>EVALS 24–26</th><th>PASS</th><th>FUNDED PAID</th><th>PAYOUTS</th><th>NET 24–26</th><th>NET 6 YRS</th><th>MOST $ DOWN</th><th>LIVE 6 YRS (own money)</th></tr>");
        foreach (var x in picks) h.Append("<tr class='" + (x == winner ? "best" : "") + "'><td class='w'>" + x.Name + "</td><td>" + (x.Trades / (double)days.Count).ToString("0.0", IC) + "</td><td>" + pct(x.WinPct) + "</td><td class='" + rcls(x.A.Roi) + "'>" + roi(x.A.Roi) + "</td><td class='" + rcls(x.Bo.Roi) + " " + (x.Bo.Roi >= 1 ? "hp" : "hn") + "'><b>" + roi(x.Bo.Roi) + "</b></td><td>" + x.Bo.Evals + "</td><td>" + pct(100 * x.Bo.PassRate) + "</td><td>" + pct(100 * x.Bo.PayoutRate) + "</td><td>" + x.Bo.Payouts + "</td><td class='" + cls(x.Bo.Net) + "'>" + usd(x.Bo.Net) + "</td><td class='" + cls(x.All.Net) + "'>" + usd(x.All.Net) + "</td><td class='neg'>" + usd(x.All.Low) + "</td><td class='" + cls(x.Live) + "'>" + usd(x.Live) + "</td></tr>");
        h.Append("</table></div>");
        Action<string, Func<Res, int>, string[]> by = (title, key, names) =>
        {
            h.Append("<h3>" + title + " — average ROI over all the other settings</h3><div class='wrap'><table><tr><th></th><th>COMBINATIONS</th><th>AVG ROI 2020–23</th><th>AVG ROI 2024–26</th><th>ABOVE 1× IN BOTH</th><th>AVG TRADES/DAY</th></tr>");
            for (int i = 0; i < names.Length; i++) { var g = res.Where(x => key(x) == i).ToList(); if (g.Count == 0) continue; h.Append("<tr><td>" + names[i] + "</td><td>" + g.Count + "</td><td class='" + rcls(g.Average(x => x.A.Roi)) + "'>" + roi(g.Average(x => x.A.Roi)) + "</td><td class='" + rcls(g.Average(x => x.Bo.Roi)) + "'>" + roi(g.Average(x => x.Bo.Roi)) + "</td><td>" + g.Count(x => x.A.Roi > 1 && x.Bo.Roi > 1) + " of " + g.Count + "</td><td>" + g.Average(x => x.Trades / (double)days.Count).ToString("0.0", IC) + "</td></tr>"); }
            h.Append("</table></div>");
        };
        h.Append("<h2>2 • What matters</h2>");
        by("ENTRY MODEL", x => x.M, Models); by("TIME WINDOW", x => x.W, Wins); by("TARGET / STOP", x => x.B, Br.Select(b => "+$" + b[0].ToString("N0", IC) + " / −$" + b[1].ToString("N0", IC)).ToArray()); by("TRADES A DAY (MAX)", x => x.X, MaxDay.Select(m => "≤ " + m).ToArray());
        h.Append("<p class='sub'>Built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + " • research/FreqStudy.cs</p></main></body></html>");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "freq_study.html"), h.ToString());
        File.WriteAllText(Path.Combine(outDir, "freq_study_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
