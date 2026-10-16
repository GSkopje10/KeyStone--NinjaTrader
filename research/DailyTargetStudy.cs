// DAILY TARGET • COPY vs SPREAD — the user's plan: 50 evaluations (LUCID FLEX 50K), every day ONE trade per account at a session's
// open, 10 MNQ + 10 MGC, close at a small daily target ($150 … $350) or at the stop = the room to the floor (at most $2,000: "risk the
// full drawdown to make a little"), flat at 16:44 otherwise. Funded: payouts as the firm allows (5 days ≥ $150, 50% up to $2,000), or
// only once the balance is $4,000 (take $2,000, keep $2,000). A blown account is replaced at once by a new evaluation ($120).
// COPY = every account takes the same trade (they rise and fall together) • SPREAD = each account its own random sides • SPLIT = half
// BUY BOTH, half SELL BOTH. Also: pass the evaluation with FVG RETRACE (+$1,500 / −$1,500 on MNQ), then the daily target funded.
// Fills: market at the minute's open; within a minute the stop is checked before the target (both legs at their worst / best).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class DailyTargetStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);
    static readonly string[] SesName = { "OPEN 18:00", "ASIA 20:00", "LONDON 03:00", "NY 09:30" };
    static readonly int[] SesHm = { 1800, 2000, 300, 930 };
    static readonly string[] Models = { "RANDOM • SPREAD (each account its own sides)", "RANDOM • COPY (one coin for all)", "BUY BOTH", "SELL BOTH", "BUY MNQ • SELL MGC", "SELL MNQ • BUY MGC", "FOLLOW THE FIRST 5 MIN", "FADE THE FIRST 5 MIN", "SPLIT • half BUY BOTH, half SELL BOTH" };
    static readonly string[] ModelShort = { "RANDOM SPREAD", "RANDOM COPY", "BB", "SS", "BS", "SB", "FOLLOW 5", "FADE 5", "SPLIT BB/SS" };
    static readonly double[] Targets = { 150, 200, 250, 300, 350 };
    const double StopCap = 2000; const int N = 50;
    static readonly int[][] Combo = { new[] { 1, 1 }, new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 } };

    public sealed class Path
    {
        public List<int> HK = new List<int>(), LK = new List<int>(); public List<double> HV = new List<double>(), LV = new List<double>(); public double Close;
        // the day's result for target t and stop s: pnl, 1 = target / −1 = stop / 0 = flat at the end; mfe = the best before the stop
        public double Query(double t, double s, out int how, out double mfe)
        {
            int kT = int.MaxValue, kS = int.MaxValue;
            for (int i = 0; i < HV.Count; i++) if (HV[i] >= t) { kT = HK[i]; break; }
            for (int i = 0; i < LV.Count; i++) if (LV[i] <= -s) { kS = LK[i]; break; }
            mfe = 0;
            if (kS != int.MaxValue && kS <= kT) { how = -1; for (int i = 0; i < HV.Count && HK[i] < kS; i++) mfe = Math.Max(mfe, HV[i]); return -s; }
            if (kT != int.MaxValue) { how = 1; mfe = t; return t; }
            how = 0; for (int i = 0; i < HV.Count; i++) mfe = Math.Max(mfe, HV[i]); return Close;
        }
    }

    static Path Build(RDay m, RDay g, int s0, int flat, int dM, int dG, double cost)
    {
        double eM = m.OpenAt(s0), eG = g.OpenAt(s0); if (double.IsNaN(eM) || double.IsNaN(eG)) return null;
        double vM = KeystoneMoveStudy.PointValue("MNQ") * 10, vG = KeystoneMoveStudy.PointValue("MGC") * 10;
        var p = new Path(); double lm = eM, lg = eG, maxB = double.MinValue, minW = double.MaxValue;
        for (int k = s0; k <= flat; k++)
        {
            double hM = lm, lM = lm, hG = lg, lG = lg;
            if (m.Has(k)) { hM = m.H[k]; lM = m.L[k]; } if (g.Has(k)) { hG = g.H[k]; lG = g.L[k]; }
            double bM = dM > 0 ? (hM - eM) * vM : (eM - lM) * vM, wM = dM > 0 ? (lM - eM) * vM : (eM - hM) * vM;
            double bG = dG > 0 ? (hG - eG) * vG : (eG - lG) * vG, wG = dG > 0 ? (lG - eG) * vG : (eG - hG) * vG;
            double B = bM + bG - cost, W = wM + wG - cost;
            if (B > maxB) { maxB = B; p.HK.Add(k); p.HV.Add(B); }
            if (W < minW) { minW = W; p.LK.Add(k); p.LV.Add(W); }
            if (m.Has(k)) lm = m.C[k]; if (g.Has(k)) lg = g.C[k];
        }
        p.Close = dM * (lm - eM) * vM + dG * (lg - eG) * vG - cost;
        return p;
    }

    sealed class DayData { public DateTime Day; public Path[][] P; public int[] Follow; public double Fvgr; }   // P[session][combo 0-3 at the open, 4-7 at +5 min]; Follow[session] = combo of the first 5 min (−1 none)

    static bool PayStep(OpenFvgStudy.Acct a, double pnl, KeystoneSimRules r, double payFrom)
    {
        double dd = a.Funded ? r.FundDrawdown : r.EvalDrawdown, lockAt = a.Funded ? r.FundLockAt : r.EvalLockAt;
        double floor = Math.Min(a.Hwm - dd, lockAt);
        a.Bal += pnl; a.Days++;
        if (a.Bal <= floor + 1e-9) { a.End = "BLOWN"; return false; }
        a.Hwm = Math.Max(a.Hwm, a.Bal); a.Best = Math.Max(a.Best, pnl);
        if (!a.Funded)
        {
            if (a.Bal >= r.Target && a.Days >= r.EvalMinDays && (r.EvalConsistency <= 0 || a.Best <= r.EvalConsistency / 100.0 * a.Bal)) { a.End = "PASSED"; return false; }
            return true;
        }
        if (pnl >= r.QualifyingDay) a.QualDays++;
        if (a.QualDays >= r.PayoutDays && a.Bal > 0 && a.Bal >= payFrom)
        {
            double g = Math.Min(r.PayoutCap > 0 ? r.PayoutCap : double.MaxValue, a.Bal * r.PayoutPercent / 100.0);
            if (g >= Math.Max(1, r.PayoutMin)) { a.Bal -= g; a.Kept += g * r.Split / 100.0; a.Payouts++; a.QualDays = 0; if (r.MaxPayouts > 0 && a.Payouts >= r.MaxPayouts) { a.End = "RETIRED"; return false; } }
        }
        return true;
    }

    public sealed class Res
    {
        public int Ses, Model, T, Pay, EvalMode; public double Spent, Kept, Low; public int Evals, Passed, EBlown, FBlown, Retired, Payouts, Trades, Straight;
        public DateTime FirstPay = DateTime.MinValue, FirstGreen = DateTime.MinValue; public double PassDays, PayDays; public int PassN, PayN;
        public SortedDictionary<string, double[]> Month = new SortedDictionary<string, double[]>();   // fees, kept, payouts, passed, blown
        public double Net { get { return Kept - Spent; } }
        public double Roi { get { return Spent == 0 ? 0 : Kept / Spent; } }
    }

    static int Hash(int lane, int day) { unchecked { uint h = (uint)(lane * 7919 + day * 104729 + 12345); h *= 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; return (int)(h % 4); } }

    // 50 lanes from the first day: evaluation → funded → … a blown / retired account is replaced by a new evaluation the next day
    static Res Run(List<DayData> days, int ses, int model, double t, double payFrom, bool evalFvgr, KeystoneSimRules r)
    {
        var o = new Res(); int lanes = model == 0 ? N : model == 8 ? 2 : 1; double w = (double)N / lanes;
        var acc = new OpenFvgStudy.Acct[lanes]; var started = new DateTime[lanes]; var fundedOn = new DateTime[lanes];
        double net = 0; o.Low = 0;
        for (int di = 0; di < days.Count; di++)
        {
            var dd = days[di]; string mk = dd.Day.ToString("yyyy-MM", IC); double[] mo; if (!o.Month.TryGetValue(mk, out mo)) { mo = new double[5]; o.Month[mk] = mo; }
            for (int l = 0; l < lanes; l++)
            {
                if (acc[l] == null) { acc[l] = new OpenFvgStudy.Acct { Id = l }; o.Evals += (int)w; o.Spent += w * r.EvalCost; net -= w * r.EvalCost; mo[0] += w * r.EvalCost; started[l] = dd.Day; }
                var a = acc[l]; double pnl;
                if (!a.Funded && evalFvgr) { if (dd.Fvgr == 0) continue; pnl = dd.Fvgr; }
                else
                {
                    int c = model == 0 ? Hash(l, di) : model == 1 ? Hash(9999, di) : model >= 2 && model <= 5 ? model - 2 : model == 8 ? (l == 0 ? 0 : 1) : dd.Follow[ses];
                    if (model == 7 && c >= 0) c = c == 0 ? 1 : c == 1 ? 0 : c == 2 ? 3 : 2;
                    if (c < 0) continue;
                    var p = dd.P[ses][model >= 6 && model <= 7 ? 4 + c : c]; if (p == null) continue;
                    double dd0 = a.Funded ? r.FundDrawdown : r.EvalDrawdown, lk = a.Funded ? r.FundLockAt : r.EvalLockAt, floor = Math.Min(a.Hwm - dd0, lk);
                    double s = Math.Min(StopCap, a.Bal - floor); int how; double mfe; pnl = p.Query(t, s, out how, out mfe);
                    o.Trades += (int)w; if (how == -1 && mfe < 1) o.Straight += (int)w;
                }
                double k0 = a.Kept; bool alive = PayStep(a, pnl, r, payFrom);
                if (a.Kept > k0) { double g = (a.Kept - k0) * w; o.Kept += g; net += g; mo[1] += g; mo[2] += w; o.Payouts += (int)w; if (o.FirstPay == DateTime.MinValue) o.FirstPay = dd.Day; if (a.Payouts == 1) { o.PayDays += (dd.Day - fundedOn[l]).TotalDays * w; o.PayN += (int)w; } }
                if (!alive)
                {
                    if (a.End == "PASSED") { o.Passed += (int)w; mo[3] += w; o.PassDays += (dd.Day - started[l]).TotalDays * w; o.PassN += (int)w; acc[l] = new OpenFvgStudy.Acct { Id = l, Funded = true }; fundedOn[l] = dd.Day; }
                    else { if (a.End == "BLOWN") { if (a.Funded) o.FBlown += (int)w; else o.EBlown += (int)w; mo[4] += w; } else o.Retired += (int)w; acc[l] = null; }
                }
            }
            o.Low = Math.Min(o.Low, net); if (o.FirstGreen == DateTime.MinValue && net > 0) o.FirstGreen = dd.Day;
        }
        return o;
    }

    public static void Run(List<RDay> mnq, List<RDay> mgc, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var gBy = mgc.ToDictionary(d => d.Day.Date); var rules = KeystoneSimRules.Presets()[1].Copy();
        double cost = (KeystoneMoveStudy.Cost("MNQ") + KeystoneMoveStudy.Cost("MGC")) * 10; int flat = R.S(16, 44);
        var days = new List<DayData>();
        foreach (var m in mnq.OrderBy(d => d.Day))
        {
            RDay g; if (!gBy.TryGetValue(m.Day.Date, out g)) continue; if (m.Day.DayOfWeek == DayOfWeek.Saturday || m.Day.DayOfWeek == DayOfWeek.Sunday) continue;
            var dd = new DayData { Day = m.Day.Date, P = new Path[4][], Follow = new int[4] };
            for (int si = 0; si < 4; si++)
            {
                int s0 = R.S(SesHm[si] / 100, SesHm[si] % 100); dd.P[si] = new Path[8];
                for (int c = 0; c < 4; c++) { dd.P[si][c] = Build(m, g, s0, flat, Combo[c][0], Combo[c][1], cost); dd.P[si][4 + c] = Build(m, g, s0 + 5, flat, Combo[c][0], Combo[c][1], cost); }
                double om = m.OpenAt(s0), cm = m.CloseAt(s0 + 4), og = g.OpenAt(s0), cg = g.CloseAt(s0 + 4);
                bool okF = !double.IsNaN(om + cm + og + cg); int fM = okF ? Math.Sign(cm - om) : 0, fG = okF ? Math.Sign(cg - og) : 0;
                dd.Follow[si] = fM == 0 || fG == 0 ? -1 : Array.FindIndex(Combo, x => x[0] == fM && x[1] == fG);
            }
            dd.Fvgr = FreqStudy.AppDay(m, 1, 1, 6, 0.10).Sum(x => x.Pnl);
            days.Add(dd);
        }
        say("DAILY TARGET • COPY vs SPREAD • " + days[0].Day.ToString("yyyy-MM-dd") + " → " + days[days.Count - 1].Day.ToString("yyyy-MM-dd") + " • " + days.Count + " days with MNQ + MGC");

        // ---- 1. one trade: how often each target comes before −$2,000 (sides fixed: BB / SS / BS / SB, and their average = a coin)
        var tradeRows = new List<string[]>(); var mfeRows = new List<string[]>();
        double[] mfeCuts = { 1, 50, 100, 150, 200, 250, 300, 350, 500 };
        for (int si = 0; si < 4; si++)
            foreach (int ci in new[] { -1, 0, 1, 2, 3, 4, 5 })   // −1 = all four sides (a coin), 4 / 5 = follow / fade the first 5 min
            {
                var hit = new int[Targets.Length]; var stp = new int[Targets.Length]; var sum = new double[Targets.Length]; int n = 0; var mfes = new List<double>(); int straight = 0, stops2k = 0;
                foreach (var dd in days)
                {
                    var paths = new List<Path>();
                    if (ci == -1) paths.AddRange(dd.P[si].Take(4)); else if (ci < 4) paths.Add(dd.P[si][ci]);
                    else { int f = dd.Follow[si]; if (f >= 0) { if (ci == 5) f = f == 0 ? 1 : f == 1 ? 0 : f == 2 ? 3 : 2; paths.Add(dd.P[si][4 + f]); } }
                    foreach (var p in paths.Where(x => x != null))
                    {
                        n++; int how; double mfe;
                        for (int ti = 0; ti < Targets.Length; ti++) { double v = p.Query(Targets[ti], StopCap, out how, out mfe); sum[ti] += v; if (how == 1) hit[ti]++; if (how == -1) stp[ti]++; }
                        p.Query(1e9, StopCap, out how, out mfe); if (how == -1) { stops2k++; mfes.Add(mfe); if (mfe < 1) straight++; }
                    }
                }
                if (n == 0) continue;
                string nm = ci == -1 ? "RANDOM (any sides)" : ci < 4 ? ModelShort[2 + ci] : ci == 4 ? "FOLLOW 5" : "FADE 5";
                var row = new List<string> { SesName[si], nm, n.ToString(IC) };
                for (int ti = 0; ti < Targets.Length; ti++) row.Add((100.0 * hit[ti] / n).ToString("0.0", IC) + "% • " + (sum[ti] / n).ToString("+0;−0", IC));
                row.Add((100.0 * stops2k / n).ToString("0.0", IC) + "%"); row.Add((100.0 * straight / n).ToString("0.0", IC) + "%");
                tradeRows.Add(row.ToArray());
                var mr = new List<string> { SesName[si], nm, stops2k.ToString(IC) };
                foreach (var cut in mfeCuts) mr.Add(stops2k == 0 ? "—" : (100.0 * mfes.Count(x => x >= cut) / stops2k).ToString("0", IC) + "%");
                mr.Add(mfes.Count == 0 ? "—" : "$" + mfes.OrderBy(x => x).ElementAt(mfes.Count / 2).ToString("N0", IC));
                mfeRows.Add(mr.ToArray());
                say("  " + SesName[si] + " • " + nm + " • n " + n + " • " + string.Join(" | ", Enumerable.Range(0, Targets.Length).Select(ti => "$" + Targets[ti] + " " + (100.0 * hit[ti] / n).ToString("0") + "% avg " + (sum[ti] / n).ToString("0"))) + " • −$2K before any target " + (100.0 * stops2k / n).ToString("0.0") + "% (straight " + (100.0 * straight / n).ToString("0.0") + "%)");
            }

        // ---- 2. the prop: 50 lanes, every session × entry × target × payout rule × how the evaluation is passed
        var A = days.Where(d => d.Day < Half).ToList(); var B = days.Where(d => d.Day >= Half).ToList();
        var all = new List<Tuple<Res, Res, Res>>();
        foreach (int ev in new[] { 0, 1 }) for (int si = 0; si < 4; si++) for (int mi = 0; mi < Models.Length; mi++) for (int ti = 0; ti < Targets.Length; ti++) foreach (double pf in new[] { 0.0, 4000.0 })
        {
            Func<List<DayData>, Res> go = ds => { var x = Run(ds, si, mi, Targets[ti], pf, ev == 1, rules); x.Ses = si; x.Model = mi; x.T = ti; x.Pay = pf > 0 ? 1 : 0; x.EvalMode = ev; return x; };
            all.Add(Tuple.Create(go(A), go(B), go(days)));
        }
        say(all.Count + " prop runs in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        Func<Res, string> nameOf = x => SesName[x.Ses] + " • " + ModelShort[x.Model] + " • +$" + Targets[x.T] + " a day • " + (x.Pay == 1 ? "payout at $4K" : "firm payouts") + (x.EvalMode == 1 ? " • PASS WITH FVG RETRACE" : "");
        var ranked = all.OrderByDescending(x => Math.Min(x.Item1.Roi, x.Item2.Roi)).ToList();
        foreach (var x in ranked.Take(15)) say("  " + nameOf(x.Item3) + " • ROI A " + x.Item1.Roi.ToString("0.00") + " B " + x.Item2.Roi.ToString("0.00") + " • 6 yrs net " + x.Item3.Net.ToString("N0", IC) + " passed " + x.Item3.Passed + " payouts " + x.Item3.Payouts + " blown " + (x.Item3.EBlown + x.Item3.FBlown));
        var best = ranked[0];
        // the random baseline with the pick's session / target / payout rule
        Func<int, int, Tuple<Res, Res, Res>> same = (mi, ev) => all.FirstOrDefault(x => x.Item3.Ses == best.Item3.Ses && x.Item3.T == best.Item3.T && x.Item3.Pay == best.Item3.Pay && x.Item3.Model == mi && x.Item3.EvalMode == ev);

        // ---- HTML
        var h = new StringBuilder();
        Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC);
        Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> roi = v => v.ToString("0.00", IC) + "×";
        Func<double, string> rc = v => v >= 1 ? "pos" : "neg";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Daily Target Study</title><style>");
        h.Append(":root{--bg:#0d1117;--card:#161b22;--line:#30363d;--text:#e6edf3;--mute:#8b949e;--pos:#3fb950;--neg:#f85149;--acc:#d29922}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f6f8fa;--card:#fff;--line:#d0d7de;--text:#1f2328;--mute:#59636e;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700}}");
        h.Append("body{background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:24px 16px}main{max-width:1300px;margin:0 auto}h1{font-size:24px;margin:0 0 4px}h2{font-size:18px;margin:34px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}h3{font-size:14px;margin:16px 0 6px;color:var(--acc)}.mute{color:var(--mute)}.pos{color:var(--pos)}.neg{color:var(--neg)}");
        h.Append(".note{background:var(--card);border:1px solid var(--line);border-left:4px solid var(--acc);border-radius:6px;padding:10px 14px;margin:10px 0}.wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums;background:var(--card)}th,td{border:1px solid var(--line);padding:4px 7px;text-align:right;white-space:nowrap}th{color:var(--mute);font-weight:600}td:first-child,th:first-child,td.w{text-align:left}tr.best td{background:rgba(210,153,34,.14)}details{margin:8px 0}summary{cursor:pointer;color:var(--acc)}</style></head><body><main>");
        h.Append("<h1>Daily target • copy vs spread • 50 evaluations</h1><p class='mute'>" + days[0].Day.ToString("yyyy-MM-dd") + " → " + days[days.Count - 1].Day.ToString("yyyy-MM-dd") + " • " + days.Count + " days • LUCID FLEX 50K ($120, target $3,000, $2,000 end-of-day trailing floor that stops at +$100, payouts after 5 days ≥ $150: 50% up to $2,000, 90% kept, 5 payouts) • one trade a day per account: 10 MNQ + 10 MGC at the session's open, closed at the day's $ target or at the stop = the room to the floor (at most $2,000), else flat at 16:44 • costs $" + cost.ToString("0.0", IC) + " a round trip. Chosen on 2020–23, judged on 2024–26. Historical simulation only.</p>");
        var bx = best.Item3; var rnd = same(1, best.Item3.EvalMode); var spr = same(0, best.Item3.EvalMode);
        h.Append("<div class='note'><b>Answer in short.</b> The steadiest plan here: <b>" + nameOf(bx) + "</b> — ROI " + roi(best.Item1.Roi) + " on 2020–23 and " + roi(best.Item2.Roi) + " on 2024–26; over 6 years " + bx.Evals + " evaluations bought (" + usd(bx.Spent) + "), " + bx.Passed + " passed, " + bx.Payouts + " payouts kept " + usd(bx.Kept) + ", net <b class='" + cls(bx.Net) + "'>" + usd(bx.Net) + "</b>, the bank's lowest " + usd(bx.Low) + "."
            + (rnd != null ? " The same with one random coin for all (COPY): ROI " + roi(rnd.Item1.Roi) + " / " + roi(rnd.Item2.Roi) + ", net " + usd(rnd.Item3.Net) + "." : "") + (spr != null ? " Each account its own coin (SPREAD): ROI " + roi(spr.Item1.Roi) + " / " + roi(spr.Item2.Roi) + ", net " + usd(spr.Item3.Net) + "." : "") + "</div>");

        h.Append("<h2>1 • One trade: how often the day's target comes before −$2,000</h2><p class='mute'>Each cell: % of days the target was reached first • the average $ per trade (target, −$2,000 or flat at 16:44). Last two columns: % of trades that fell to −$2,000 when holding for no target, and % that went straight there without ever being $1 up.</p><div class='wrap'><table><tr><th>SESSION</th><th>SIDES</th><th>TRADES</th>");
        foreach (var t in Targets) h.Append("<th>+$" + t + "</th>"); h.Append("<th>−$2K (NO TARGET)</th><th>STRAIGHT −$2K</th></tr>");
        foreach (var rw in tradeRows) { h.Append("<tr>"); for (int i = 0; i < rw.Length; i++) h.Append("<td" + (i < 2 ? " class='w'" : "") + ">" + rw[i] + "</td>"); h.Append("</tr>"); }
        h.Append("</table></div>");
        h.Append("<h2>2 • Before the −$2,000: how much profit was there first?</h2><p class='mute'>Only the trades that ended at −$2,000 (no target). % of them that had reached at least that much profit before falling — the smallest daily target that most of the future blow-ups would have banked.</p><div class='wrap'><table><tr><th>SESSION</th><th>SIDES</th><th>−$2K TRADES</th>");
        foreach (var c in mfeCuts) h.Append("<th>≥ $" + c + "</th>"); h.Append("<th>MEDIAN BEST</th></tr>");
        foreach (var rw in mfeRows) { h.Append("<tr>"); for (int i = 0; i < rw.Length; i++) h.Append("<td" + (i < 2 ? " class='w'" : "") + ">" + rw[i] + "</td>"); h.Append("</tr>"); }
        h.Append("</table></div>");

        Action<string, IEnumerable<Tuple<Res, Res, Res>>> table = (title, rows) =>
        {
            h.Append("<h3>" + title + "</h3><div class='wrap'><table><tr><th>PLAN</th><th>ROI 20–23</th><th>ROI 24–26</th><th>EVALS (6 YRS)</th><th>PASSED</th><th>DAYS TO PASS</th><th>PAYOUTS</th><th>DAYS TO 1ST PAYOUT</th><th>EVAL BLOWN</th><th>FUNDED BLOWN</th><th>STRAIGHT −$2K</th><th>KEPT</th><th>NET 6 YRS</th><th>LOWEST BANK</th><th>FIRST PAYOUT</th></tr>");
            foreach (var x in rows)
            {
                var z = x.Item3;
                h.Append("<tr class='" + (x == best ? "best" : "") + "'><td class='w'>" + nameOf(z) + "</td><td class='" + rc(x.Item1.Roi) + "'>" + roi(x.Item1.Roi) + "</td><td class='" + rc(x.Item2.Roi) + "'><b>" + roi(x.Item2.Roi) + "</b></td><td>" + z.Evals + "</td><td>" + z.Passed + " (" + (z.Evals == 0 ? 0 : 100.0 * z.Passed / z.Evals).ToString("0", IC) + "%)</td><td>" + (z.PassN == 0 ? "—" : (z.PassDays / z.PassN).ToString("0", IC)) + "</td><td>" + z.Payouts + "</td><td>" + (z.PayN == 0 ? "—" : (z.PayDays / z.PayN).ToString("0", IC)) + "</td><td>" + z.EBlown + "</td><td>" + z.FBlown + "</td><td>" + z.Straight + "</td><td class='pos'>" + usd(z.Kept) + "</td><td class='" + cls(z.Net) + "'><b>" + usd(z.Net) + "</b></td><td class='neg'>" + usd(z.Low) + "</td><td>" + (z.FirstPay == DateTime.MinValue ? "never" : z.FirstPay.ToString("yyyy-MM-dd", IC)) + "</td></tr>");
            }
            h.Append("</table></div>");
        };
        h.Append("<h2>3 • The prop with 50 accounts</h2><p class='mute'>Days = calendar days. Evaluations bought include the replacements. STRAIGHT −$2K = trades that fell to the stop without ever being $1 up (all accounts).</p>");
        table("The 25 steadiest plans (best ROI in the worse of the two periods)", ranked.Take(25));
        table("The pick's session, target and payout rule with every entry — is it the entry or the structure?", Enumerable.Range(0, Models.Length).Select(mi => same(mi, best.Item3.EvalMode)).Where(x => x != null));
        table("COPY vs SPREAD at every session (the pick's target and payout rule, random sides)", Enumerable.Range(0, 4).SelectMany(si => new[] { 1, 0, 8 }.Select(mi => all.FirstOrDefault(x => x.Item3.Ses == si && x.Item3.Model == mi && x.Item3.T == best.Item3.T && x.Item3.Pay == best.Item3.Pay && x.Item3.EvalMode == best.Item3.EvalMode))).Where(x => x != null));
        table("Every daily target ($150 → $350), the pick's session and entry, both payout rules", Enumerable.Range(0, Targets.Length).SelectMany(ti => new[] { 0, 1 }.Select(pf => all.FirstOrDefault(x => x.Item3.Ses == best.Item3.Ses && x.Item3.Model == best.Item3.Model && x.Item3.T == ti && x.Item3.Pay == pf && x.Item3.EvalMode == best.Item3.EvalMode))).Where(x => x != null));
        table("Pass the evaluation with the daily target vs with FVG RETRACE (+$1,500 / −$1,500), then the daily target funded", new[] { 0, 1 }.Select(ev => all.FirstOrDefault(x => x.Item3.Ses == best.Item3.Ses && x.Item3.Model == best.Item3.Model && x.Item3.T == best.Item3.T && x.Item3.Pay == best.Item3.Pay && x.Item3.EvalMode == ev)).Where(x => x != null));

        // the pick's bank by year and month
        h.Append("<h2>4 • The pick, month by month (all 50 accounts)</h2>");
        var years = bx.Month.GroupBy(kv => kv.Key.Substring(0, 4));
        h.Append("<div class='wrap'><table><tr><th>YEAR</th><th>FEES</th><th>PAYOUTS</th><th>KEPT</th><th>PASSED</th><th>BLOWN</th><th>NET</th><th>BANK</th></tr>"); double bank = 0;
        foreach (var y in years) { double f = y.Sum(kv => kv.Value[0]), k = y.Sum(kv => kv.Value[1]); bank += k - f; h.Append("<tr><td>" + y.Key + "</td><td class='neg'>" + usd(-f) + "</td><td>" + y.Sum(kv => kv.Value[2]).ToString("0", IC) + "</td><td class='pos'>" + usd(k) + "</td><td>" + y.Sum(kv => kv.Value[3]).ToString("0", IC) + "</td><td>" + y.Sum(kv => kv.Value[4]).ToString("0", IC) + "</td><td class='" + cls(k - f) + "'>" + usd(k - f) + "</td><td class='" + cls(bank) + "'>" + usd(bank) + "</td></tr>"); }
        h.Append("</table></div><details><summary>Every month</summary><div class='wrap'><table><tr><th>MONTH</th><th>FEES</th><th>PAYOUTS</th><th>KEPT</th><th>PASSED</th><th>BLOWN</th><th>NET</th><th>BANK</th></tr>"); bank = 0; int up = 0, down = 0;
        foreach (var kv in bx.Month) { double f = kv.Value[0], k = kv.Value[1]; bank += k - f; if (k - f > 0) up++; else if (k - f < 0) down++; h.Append("<tr><td>" + kv.Key + "</td><td class='neg'>" + (f == 0 ? "" : usd(-f)) + "</td><td>" + kv.Value[2].ToString("0", IC) + "</td><td class='pos'>" + (k == 0 ? "" : usd(k)) + "</td><td>" + kv.Value[3].ToString("0", IC) + "</td><td>" + kv.Value[4].ToString("0", IC) + "</td><td class='" + cls(k - f) + "'>" + usd(k - f) + "</td><td class='" + cls(bank) + "'>" + usd(bank) + "</td></tr>"); }
        h.Append("</table></div><p class='mute'>" + up + " months up, " + down + " down.</p></details>");
        say("PICK: " + nameOf(bx) + " • months up " + up + " down " + down);
        h.Append("<h2>5 • How this was tested</h2><ul class='mute'><li>Entry: market at the session's open minute (FOLLOW / FADE: after the first 5 minutes, the side each instrument moved). Exit: the first minute the combined P/L reaches the target (at the target) or the stop (at the stop — checked first within a minute, both legs at their worst), else flat at 16:44.</li><li>The stop is the room left above the floor, at most $2,000 — the plan of risking the drawdown for a small daily gain. With less room left, a loss blows the account.</li><li>COPY: every account takes the same trade, so they pass and blow together. SPREAD: each account its own random sides each day (seeded). SPLIT: half the accounts buy both, half sell both.</li><li>PASS WITH FVG RETRACE: during the evaluation each account takes every FVG RETRACE trade of the day on 10 MNQ (09:30–15:00, +$1,500 / −$1,500, as the app trades it); funded, the daily target plan.</li><li>A blown or retired account is replaced by a new evaluation the next day ($120). No activation fee, no payout denials, no slippage beyond the costs — real results will be worse.</li></ul>");
        h.Append("</main></body></html>");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(System.IO.Path.Combine(outDir, "daily_target_study.html"), h.ToString());
        File.WriteAllText(System.IO.Path.Combine(outDir, "daily_target_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
