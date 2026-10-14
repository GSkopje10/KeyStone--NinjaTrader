// OPENING FVG • MNQ — FINDING THE EDGE FOR 50 × 50K. The app's own engine, 6 years:
// 1) every trade with what was known at the entry (side, FVGs in a row, zone size vs the 14-day range, the time the zone was set,
//    the gap at the open, yesterday, the overnight move, the open inside the overnight range, weekday, the day's range regime)
//    → winners vs losers, each bucket in 2020–23 and 2024–26
// 2) filters CHOSEN on 2020–23 only (single conditions and pairs), then JUDGED on 2024–26 they never saw
// 3) EVALUATION vs FUNDED brackets: the evaluation trades one stop / target (goal: pass), the funded accounts another
//    (goal: payouts) — 50 evaluations of LUCID FLEX 50K, pass first, then the funded ones, then the next batch
// 4) the odds that matter: pass rate, payout rate (funded accounts that got paid), $ per funded account, $ per evaluation
//    bought, P(a payout per evaluation) and the risk of ruin of a budget
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class OpenFvgEdge
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);

    public sealed class F { public OpenFvgStudy.T T; public Dictionary<string, string> K = new Dictionary<string, string>(); }
    public sealed class Odds
    {
        public string Name = ""; public int Evals, Passed, Funded, FundedPaid, Payouts, Batches; public double Spent, Kept, Low;
        public double PassRate { get { return Evals == 0 ? 0 : (double)Passed / Evals; } }
        public double PayoutRate { get { return Funded == 0 ? 0 : (double)FundedPaid / Funded; } }
        public double PerFunded { get { return Funded == 0 ? 0 : Kept / Funded; } }
        public double PerEval { get { return Evals == 0 ? 0 : Kept / Evals; } }
        public double Net { get { return Kept - Spent; } }
        public double Roi { get { return Spent == 0 ? 0 : Kept / Spent; } }
        public double PPay { get { return PassRate * PayoutRate; } }
        public double Ruin(int n) { return Math.Pow(1 - PPay, n); }
    }

    // pass first: N evaluations (eval bracket) until each passed / blew → the passed ones funded (funded bracket) until each blew /
    // retired → the next batch. copy = every account every trade • rotate = one account a trade, the next one the next
    public static Odds Prop(string name, List<OpenFvgStudy.T> ev, List<OpenFvgStudy.T> fu, KeystoneSimRules r, int n, bool copy, Func<OpenFvgStudy.T, bool> take = null)
    {
        var o = new Odds { Name = name }; List<OpenFvgStudy.Acct> evals = null, funded = null, passed = null; int last = -1; double net = 0;
        var paid = new HashSet<OpenFvgStudy.Acct>();
        for (int i = 0; i < ev.Count; i++)
        {
            if (evals == null && funded == null) { evals = Enumerable.Range(0, n).Select(k => new OpenFvgStudy.Acct { Id = k }).ToList(); passed = new List<OpenFvgStudy.Acct>(); last = -1; o.Batches++; o.Evals += n; o.Spent += n * r.EvalCost; net -= n * r.EvalCost; }
            var t = evals != null ? ev[i] : fu[i];
            if (!t.D.Filled || (take != null && !take(ev[i]))) continue;
            var pool = evals ?? funded; var live = pool.Where(a => a.End == "").OrderBy(a => a.Id).ToList(); if (live.Count == 0) continue;
            var takers = copy ? live : new List<OpenFvgStudy.Acct> { live.FirstOrDefault(a => a.Id > last) ?? live[0] }; if (!copy) last = takers[0].Id;
            foreach (var a in takers)
            {
                double k0 = a.Kept; OpenFvgStudy.Step(a, t.D.Pnl, r);
                if (a.Kept > k0) { o.Kept += a.Kept - k0; net += a.Kept - k0; o.Payouts++; paid.Add(a); }
                if (a.End == "PASSED") { o.Passed++; o.Funded++; passed.Add(new OpenFvgStudy.Acct { Id = a.Id, Funded = true }); }
            }
            o.Low = Math.Min(o.Low, net);
            if (evals != null && evals.All(a => a.End != "")) { evals = null; funded = passed.Count > 0 ? passed : null; last = -1; }
            else if (funded != null && funded.All(a => a.End != "")) funded = null;
        }
        o.FundedPaid = paid.Count(a => a.Funded);
        return o;
    }

    static string Tercile(double v, double[] q) { return v <= q[0] ? "a low" : v <= q[1] ? "b mid" : "c high"; }
    static double[] Q(IEnumerable<double> xs) { var l = xs.OrderBy(x => x).ToList(); if (l.Count == 0) return new[] { 0.0, 0.0 }; return new[] { l[l.Count / 3], l[2 * l.Count / 3] }; }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = m => { Console.WriteLine(m); log.AppendLine(m); };
        mnq = mnq.OrderBy(d => d.Day).ToList();
        var bars = mnq.Select(OpenFvgStudy.BarsOf).ToList(); var dates = mnq.Select(d => d.Day.Date).ToList();
        var rules = KeystoneSimRules.Presets()[1].Copy(); var baseR = KeystoneOpeningRange.Presets().First(x => x.Mode == "FVG");
        Func<double, double, double, KeystoneOrbRule> rule = (pc, tg, st) => { var r = baseR.Copy(); r.FvgPct = pc; r.TargetUsd = tg; r.StopUsd = st; return r; };
        // the base rule for the edge search: the best one of the comparison (up in both halves)
        var main = OpenFvgStudy.Trades(bars, dates, rule(10, 2000, 1500));
        say("EDGE • " + dates[0].ToString("yyyy-MM-dd") + " → " + dates[dates.Count - 1].ToString("yyyy-MM-dd") + " • base +$2,000 / −$1,500 @ 10%");

        // ---- features known at the entry
        int s930 = R.S(9, 30);
        var feats = new List<F>();
        for (int i = 0; i < main.Count; i++)
        {
            var t = main[i]; if (!t.D.Filled) continue; var d = mnq[i]; int dir = t.D.Dir; var f = new F { T = t };
            double open = d.OpenAt(s930), onHi = d.Hi(0, s930 - 1), onLo = d.Lo(0, s930 - 1), onOpen = d.OpenAt(0);
            double prevClose = d.Prev != null ? d.Prev.CloseAt(R.Slots - 1) : double.NaN;
            double prevMove = d.Prev != null ? d.Prev.CloseAt(R.Slots - 1) - d.Prev.OpenAt(s930) : double.NaN;
            f.K["SIDE"] = dir > 0 ? "BUY" : "SELL";
            f.K["FVGS IN A ROW"] = t.D.Stack >= 3 ? "3+" : t.D.Stack.ToString();
            f.K["ZONE SIZE / 14-DAY RANGE"] = ((t.D.ZoneHi - t.D.ZoneLo) / Math.Max(1, d.RthAtr)).ToString("R", IC);   // bucketed below
            int sm = (int)(t.D.SetupTime - t.Day.AddHours(9).AddMinutes(30)).TotalMinutes;
            f.K["ZONE SET"] = sm <= 15 ? "a by 09:45" : sm <= 30 ? "b 09:46–10:00" : sm <= 60 ? "c 10:01–10:30" : "d after 10:30";
            int fm = (int)(t.D.FillTime - t.D.SetupTime).TotalMinutes;
            f.K["FILLED AFTER THE ZONE"] = fm <= 5 ? "a ≤ 5 min" : fm <= 15 ? "b 6–15 min" : fm <= 45 ? "c 16–45 min" : "d later";
            if (!double.IsNaN(prevClose)) f.K["GAP AT 09:30 VS THE SIDE"] = Math.Abs(open - prevClose) < 0.1 * d.RthAtr ? "b small gap" : Math.Sign(open - prevClose) == dir ? "a gap WITH the side" : "c gap AGAINST the side";
            if (!double.IsNaN(prevMove)) f.K["YESTERDAY (CASH SESSION)"] = Math.Sign(prevMove) == dir ? "a WITH the side" : "b AGAINST the side";
            f.K["OVERNIGHT 18:00→09:30"] = Math.Abs(open - onOpen) < 0.1 * d.RthAtr ? "b flat" : Math.Sign(open - onOpen) == dir ? "a WITH the side" : "c AGAINST the side";
            double pos = onHi > onLo ? (open - onLo) / (onHi - onLo) : 0.5; if (dir < 0) pos = 1 - pos;
            f.K["09:30 IN THE OVERNIGHT RANGE (side-relative)"] = pos <= 0.33 ? "a near the side's start (room to run)" : pos <= 0.66 ? "b middle" : "c near the side's end (stretched)";
            f.K["WEEKDAY"] = ((int)t.Day.DayOfWeek) + " " + t.Day.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant();
            f.K["14-DAY RANGE"] = d.RthAtr.ToString("R", IC);
            f.K["ENTRY VS 09:30 OPEN (side-relative)"] = (((t.D.Entry - open) * dir) / Math.Max(1, d.RthAtr)).ToString("R", IC);
            feats.Add(f);
        }
        // terciles for the numeric ones (from 2020–23 only, then used everywhere)
        foreach (var key in new[] { "ZONE SIZE / 14-DAY RANGE", "14-DAY RANGE", "ENTRY VS 09:30 OPEN (side-relative)" })
        {
            var q = Q(feats.Where(f => f.T.Day < Half).Select(f => double.Parse(f.K[key], IC)));
            foreach (var f in feats) f.K[key] = Tercile(double.Parse(f.K[key], IC), q);
        }
        var keys = feats.SelectMany(f => f.K.Keys).Distinct().ToList();

        // ---- 2) filters chosen on 2020–23, judged on 2024–26
        var conds = new List<Tuple<string, Func<F, bool>>>();
        foreach (var k in keys) foreach (var v in feats.Where(f => f.K.ContainsKey(k)).Select(f => f.K[k]).Distinct())
        { string kk = k, vv = v; conds.Add(Tuple.Create(kk + " = " + (vv.Length > 2 && vv[1] == ' ' ? vv.Substring(2) : vv), (Func<F, bool>)(f => f.K.ContainsKey(kk) && f.K[kk] == vv))); }
        var A = feats.Where(f => f.T.Day < Half).ToList(); var B = feats.Where(f => f.T.Day >= Half).ToList();
        Func<IEnumerable<F>, double[]> stat = xs => { var l = xs.ToList(); return new double[] { l.Count, l.Count == 0 ? 0 : 100.0 * l.Count(f => f.T.D.Pnl > 0) / l.Count, l.Sum(f => f.T.D.Pnl), l.Count == 0 ? 0 : l.Average(f => f.T.D.Pnl) }; };
        var cands = new List<Tuple<string, Func<F, bool>, double[], double[]>>();
        foreach (var c in conds) { var sa = stat(A.Where(c.Item2)); if (sa[0] >= 120) cands.Add(Tuple.Create(c.Item1, c.Item2, sa, stat(B.Where(c.Item2)))); }
        for (int i = 0; i < conds.Count; i++) for (int j = i + 1; j < conds.Count; j++)
        {
            if (conds[i].Item1.Split('=')[0] == conds[j].Item1.Split('=')[0]) continue;
            var c1 = conds[i].Item2; var c2 = conds[j].Item2; Func<F, bool> cc = f => c1(f) && c2(f);
            var sa = stat(A.Where(cc)); if (sa[0] >= 120) cands.Add(Tuple.Create(conds[i].Item1 + "  AND  " + conds[j].Item1, cc, sa, stat(B.Where(cc))));
        }
        var baseA = stat(A); var baseB = stat(B);
        var picks = cands.OrderByDescending(c => c.Item3[3]).Take(15).ToList();   // ranked by $ per trade in 2020–23 ONLY
        say("base 2020–23 $" + baseA[3].ToString("0") + "/trade, 2024–26 $" + baseB[3].ToString("0") + "/trade");
        foreach (var p in picks.Take(8)) say("  " + p.Item1 + " • A $" + p.Item3[3].ToString("0") + "/t (" + p.Item3[0] + ") → B $" + p.Item4[3].ToString("0") + "/t (" + p.Item4[0] + ")");

        // prop on 2024–26 only, 50 evaluations copy + rotate, base vs the top filters chosen on 2020–23
        var mainB = main.Where(t => t.Day >= Half).ToList(); var fmap = feats.ToDictionary(f => f.T, f => f);
        var filterProps = new List<Tuple<string, Odds, Odds>>();
        filterProps.Add(Tuple.Create("no filter", Prop("", mainB, mainB, rules, 50, false), Prop("", mainB, mainB, rules, 50, true)));
        foreach (var p in picks.Take(6)) { var pf = p.Item2; Func<OpenFvgStudy.T, bool> tk = t => fmap.ContainsKey(t) && pf(fmap[t]); filterProps.Add(Tuple.Create(p.Item1, Prop("", mainB, mainB, rules, 50, false, tk), Prop("", mainB, mainB, rules, 50, true, tk))); }

        // ---- 3) EVALUATION vs FUNDED brackets (6 years, 50 evaluations, rotate + copy)
        var evalB = new[] { new[] { 10.0, 2000, 1500 }, new[] { 10.0, 1500, 1500 }, new[] { 25.0, 2000, 1500 }, new[] { 10.0, 2000, 1000 }, new[] { 50.0, 1000, 2000 } };
        var fundB = new[] { new[] { 10.0, 2000, 1500 }, new[] { 10.0, 1000, 1000 }, new[] { 10.0, 500, 1000 }, new[] { 10.0, 1000, 1500 }, new[] { 10.0, 500, 1500 }, new[] { 25.0, 750, 1000 }, new[] { 10.0, 1500, 1000 } };
        var cache = new Dictionary<string, List<OpenFvgStudy.T>>();
        Func<double[], List<OpenFvgStudy.T>> ts = b => { string k = b[0] + "|" + b[1] + "|" + b[2]; List<OpenFvgStudy.T> l; if (!cache.TryGetValue(k, out l)) { l = OpenFvgStudy.Trades(bars, dates, rule(b[0], b[1], b[2])); cache[k] = l; } return l; };
        Func<double[], string> bn = b => "+$" + b[1].ToString("N0", IC) + " / −$" + b[2].ToString("N0", IC) + " @ " + b[0] + "%";
        var split = new List<Tuple<double[], double[], Odds, Odds>>();
        foreach (var e in evalB) foreach (var f in fundB) split.Add(Tuple.Create(e, f, Prop(bn(e) + " → " + bn(f), ts(e), ts(f), rules, 50, false), Prop("", ts(e), ts(f), rules, 50, true)));
        var bestSplit = split.OrderByDescending(x => x.Item3.Net).First();
        say("BEST SPLIT (50 rotating): eval " + bn(bestSplit.Item1) + " • funded " + bn(bestSplit.Item2) + " • net $" + bestSplit.Item3.Net.ToString("N0", IC) + " • pass " + (100 * bestSplit.Item3.PassRate).ToString("0.0") + "% • payout rate " + (100 * bestSplit.Item3.PayoutRate).ToString("0.0") + "%");
        // the same split, judged by halves
        Func<List<OpenFvgStudy.T>, bool, List<OpenFvgStudy.T>> half = (l, second) => l.Where(t => second ? t.Day >= Half : t.Day < Half).ToList();

        // ---- HTML
        var h = new StringBuilder();
        Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC);
        Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> pct = v => v.ToString("0.0", IC) + "%";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Opening FVG Edge</title><style>");
        h.Append(":root{--bg:#0d1117;--card:#161b22;--line:#30363d;--text:#e6edf3;--mute:#8b949e;--pos:#3fb950;--neg:#f85149;--acc:#d29922;--blue:#58a6ff;--pbg:rgba(63,185,80,.16);--nbg:rgba(248,81,73,.16)}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f6f8fa;--card:#fff;--line:#d0d7de;--text:#1f2328;--mute:#59636e;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--blue:#0969da;--pbg:rgba(26,127,55,.12);--nbg:rgba(207,34,46,.12)}}");
        h.Append("body{background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:24px 16px}main{max-width:1200px;margin:0 auto}h1{font-size:24px;margin:0 0 4px}h2{font-size:17px;margin:30px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}h3{font-size:14px;margin:16px 0 6px;color:var(--acc)}");
        h.Append(".sub{color:var(--mute)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:10px;margin:14px 0}.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:12px}.card b{display:block;font-size:19px}.card small{color:var(--mute)}");
        h.Append(".wrap{overflow-x:auto;margin-bottom:6px}table{border-collapse:collapse;width:100%;background:var(--card);border:1px solid var(--line);font-variant-numeric:tabular-nums}th,td{padding:5px 8px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}th{color:var(--mute);font-weight:600;font-size:12px}");
        h.Append(".pos{color:var(--pos)}.neg{color:var(--neg)}.hp{background:var(--pbg)}.hn{background:var(--nbg)}.best{outline:2px solid var(--acc);outline-offset:-2px}.note{background:var(--card);border-left:3px solid var(--acc);padding:10px 14px;border-radius:6px;margin:10px 0}.tag{font-size:11px;color:var(--acc);font-weight:600}td.w{white-space:normal;min-width:260px}</style></head><body><main>");
        h.Append("<h1>★ OPENING FVG • MNQ — finding the edge for 50 × 50K</h1><div class='sub'>" + dates[0].ToString("MMM d, yyyy", IC) + " → " + dates[dates.Count - 1].ToString("MMM d, yyyy", IC) + " • the app's own engine • LUCID FLEX 50K ($120, $3,000 target, $2,000 EOD trailing, 50% consistency; funded payouts 50% up to $2,000, 90% kept, 5 then retired) • score = payouts − evaluation fees (unpaid balances count for nothing)</div>");

        var bs = bestSplit.Item3; var bc = bestSplit.Item4;
        var bsA = Prop("", half(ts(bestSplit.Item1), false), half(ts(bestSplit.Item2), false), rules, 50, false); var bsB = Prop("", half(ts(bestSplit.Item1), true), half(ts(bestSplit.Item2), true), rules, 50, false);
        h.Append("<div class='cards'>");
        h.Append("<div class='card'><small>THE PLAN THAT PAID MOST (50 evaluations, rotating)</small><b>EVAL " + bn(bestSplit.Item1) + "</b><b>FUNDED " + bn(bestSplit.Item2) + "</b><small>net <span class='" + cls(bs.Net) + "'>" + usd(bs.Net) + "</span> • ROI " + bs.Roi.ToString("0.0", IC) + "× • 2020–23 " + usd(bsA.Net) + " • 2024–26 " + usd(bsB.Net) + "</small></div>");
        h.Append("<div class='card'><small>ITS ODDS</small><b>" + pct(100 * bs.PassRate) + " pass • " + pct(100 * bs.PayoutRate) + " paid</b><small>a funded account brings " + usd(bs.PerFunded) + " • each evaluation bought " + usd(bs.PerEval) + " • P(payout per evaluation) " + pct(100 * bs.PPay) + "</small></div>");
        h.Append("<div class='card'><small>RISK OF RUIN (no payout at all)</small><b>" + pct(100 * bs.Ruin(20)) + " with 20 • " + pct(100 * bs.Ruin(50)) + " with 50</b><small>(1 − pass × payout rate)^N — for independent accounts (ROTATE). With COPY the 50 move together: the risk is that of ONE account per batch.</small></div>");
        h.Append("<div class='card'><small>HIS BENCHMARK (the video)</small><b>37% pass • 35% paid • 4.2×</b><small>Lucid Flex 25K • ours on 50K: " + pct(100 * bs.PassRate) + " • " + pct(100 * bs.PayoutRate) + " • " + bs.Roi.ToString("0.0", IC) + "×</small></div>");
        h.Append("</div>");

        // section: eval vs funded
        h.Append("<h2>1 • Evaluation bracket × funded bracket (50 evaluations, 6 years)</h2><div class='note'>The evaluation's only goal is to pass; the funded account's only goal is payouts. So each gets its own stop / target. Rows = what the evaluations trade, columns = what the funded accounts trade. Cell: net $ (payouts − fees) with 50 rotating • the payout rate underneath.</div>");
        h.Append("<div class='wrap'><table><tr><th>EVAL \\ FUNDED</th>" + string.Concat(fundB.Select(f => "<th>" + bn(f) + "</th>")) + "</tr>");
        double maxN = split.Max(x => x.Item3.Net);
        foreach (var e in evalB)
        {
            h.Append("<tr><td>" + bn(e) + "</td>");
            foreach (var f in fundB) { var x = split.First(y => y.Item1 == e && y.Item2 == f).Item3; h.Append("<td class='" + (x.Net > 0 ? "hp" : "hn") + (Math.Abs(x.Net - maxN) < 0.5 ? " best" : "") + "'><b class='" + cls(x.Net) + "'>" + usd(x.Net) + "</b><br><small class='sub'>pass " + (100 * x.PassRate).ToString("0") + "% • paid " + (100 * x.PayoutRate).ToString("0") + "%</small></td>"); }
            h.Append("</tr>");
        }
        h.Append("</table></div>");
        h.Append("<h3>The 10 best plans — all the odds</h3><div class='wrap'><table><tr><th>EVAL → FUNDED</th><th>EVALS</th><th>PASS</th><th>FUNDED PAID</th><th>$ / FUNDED</th><th>$ / EVAL</th><th>P(PAYOUT / EVAL)</th><th>RUIN 20</th><th>RUIN 50</th><th>SPENT</th><th>PAYOUTS</th><th>NET ROTATE</th><th>ROI</th><th>NET COPY</th><th>MOST $ DOWN</th></tr>");
        foreach (var x in split.OrderByDescending(y => y.Item3.Net).Take(10))
        {
            var o = x.Item3;
            h.Append("<tr><td>" + o.Name + "</td><td>" + o.Evals + "</td><td>" + pct(100 * o.PassRate) + "</td><td>" + o.FundedPaid + " / " + o.Funded + " (" + pct(100 * o.PayoutRate) + ")</td><td>" + usd(o.PerFunded) + "</td><td>" + usd(o.PerEval) + "</td><td>" + pct(100 * o.PPay) + "</td><td>" + pct(100 * o.Ruin(20)) + "</td><td>" + pct(100 * o.Ruin(50)) + "</td><td>" + usd(o.Spent) + "</td><td>" + usd(o.Kept) + "</td><td class='" + cls(o.Net) + "'><b>" + usd(o.Net) + "</b></td><td>" + o.Roi.ToString("0.0", IC) + "×</td><td class='" + cls(x.Item4.Net) + "'>" + usd(x.Item4.Net) + "</td><td class='neg'>" + usd(o.Low) + "</td></tr>");
        }
        h.Append("</table></div>");

        // section: winners vs losers
        h.Append("<h2>2 • Winners vs losers — what was known at the entry</h2><div class='note'>Base rule +$2,000 / −$1,500 @ 10%: " + (int)(baseA[0] + baseB[0]) + " trades, won " + pct(100.0 * feats.Count(f => f.T.D.Pnl > 0) / Math.Max(1, feats.Count)) + ". Each bucket: trades, won %, $ per trade — 2020–23 and 2024–26. A bucket that is better in BOTH is worth testing; one good half only is likely luck. Size buckets are thirds of 2020–23.</div>");
        foreach (var k in keys.OrderBy(x => x))
        {
            h.Append("<h3>" + k + "</h3><div class='wrap'><table><tr><th></th><th>2020–23 TRADES</th><th>WON</th><th>$ / TRADE</th><th>2024–26 TRADES</th><th>WON</th><th>$ / TRADE</th><th></th></tr>");
            foreach (var v in feats.Where(f => f.K.ContainsKey(k)).Select(f => f.K[k]).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            {
                var a = stat(A.Where(f => f.K.ContainsKey(k) && f.K[k] == v)); var b = stat(B.Where(f => f.K.ContainsKey(k) && f.K[k] == v));
                bool both = a[3] > baseA[3] && b[3] > baseB[3] && a[0] >= 40 && b[0] >= 25;
                h.Append("<tr><td>" + (v.Length > 2 && v[1] == ' ' ? v.Substring(2) : v) + "</td><td>" + a[0] + "</td><td>" + pct(a[1]) + "</td><td class='" + cls(a[3]) + "'>" + usd(a[3]) + "</td><td>" + b[0] + "</td><td>" + pct(b[1]) + "</td><td class='" + cls(b[3]) + "'>" + usd(b[3]) + "</td><td>" + (both ? "<span class='tag'>BETTER IN BOTH</span>" : "") + "</td></tr>");
            }
            h.Append("</table></div>");
        }

        // section: filters
        h.Append("<h2>3 • Filters chosen on 2020–23, judged on 2024–26 (days they never saw)</h2><div class='note'>Every single condition and every pair (with at least 120 trades in 2020–23) was ranked by $ per trade in 2020–23 only. Then the same filter on 2024–26. Base: 2020–23 " + usd(baseA[3]) + " a trade, 2024–26 " + usd(baseB[3]) + " a trade.</div>");
        h.Append("<div class='wrap'><table><tr><th>TAKE THE TRADE ONLY WHEN</th><th>2020–23 TRADES</th><th>WON</th><th>$ / TRADE</th><th>2024–26 TRADES</th><th>WON</th><th>$ / TRADE</th><th>HELD UP?</th></tr>");
        foreach (var p in picks) { bool held = p.Item4[3] > baseB[3] && p.Item4[0] >= 30; h.Append("<tr><td class='w'>" + p.Item1 + "</td><td>" + p.Item3[0] + "</td><td>" + pct(p.Item3[1]) + "</td><td class='pos'>" + usd(p.Item3[3]) + "</td><td>" + p.Item4[0] + "</td><td>" + pct(p.Item4[1]) + "</td><td class='" + cls(p.Item4[3]) + "'>" + usd(p.Item4[3]) + "</td><td>" + (held ? "<span class='tag'>✔ BETTER THAN BASE</span>" : "<span class='sub'>no</span>") + "</td></tr>"); }
        h.Append("</table></div><h3>The top filters in prop terms, 2024–26 only — 50 evaluations</h3><div class='wrap'><table><tr><th>FILTER (chosen on 2020–23)</th><th>PASS</th><th>PAID</th><th>NET ROTATE</th><th>NET COPY</th><th>RUIN 50</th></tr>");
        foreach (var x in filterProps) h.Append("<tr><td class='w'>" + x.Item1 + "</td><td>" + pct(100 * x.Item2.PassRate) + "</td><td>" + pct(100 * x.Item2.PayoutRate) + "</td><td class='" + cls(x.Item2.Net) + "'><b>" + usd(x.Item2.Net) + "</b></td><td class='" + cls(x.Item3.Net) + "'>" + usd(x.Item3.Net) + "</td><td>" + pct(100 * x.Item2.Ruin(50)) + "</td></tr>");
        h.Append("</table></div>");

        h.Append("<h2>4 • What this means for the first 50 × 50K</h2><div class='note'><ul>");
        h.Append("<li><b>Trade the evaluation and the funded account differently.</b> Best found: evaluation " + bn(bestSplit.Item1) + ", funded " + bn(bestSplit.Item2) + " — " + pct(100 * bs.PassRate) + " pass, " + pct(100 * bs.PayoutRate) + " of funded accounts paid, " + usd(bs.PerFunded) + " per funded account.</li>");
        h.Append("<li><b>ROTATE before COPY for the first money.</b> Rotating spreads the 50 accounts over different days: the risk of no payout at all is " + pct(100 * bs.Ruin(50)) + ". COPY puts all 50 on the same trade — the whole $6,000 rides on one account's luck per batch (net " + usd(bc.Net) + " over 6 years, but deeper drawdowns: " + usd(bc.Low) + ").</li>");
        h.Append("<li><b>Filters:</b> keep only those marked ✔ in section 3 (better on days they never saw); one good half is luck.</li>");
        h.Append("<li><b>Request every payout the first day it is allowed</b> (the simulation does) and score only payouts − fees.</li>");
        h.Append("</ul></div><p class='sub'>Built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + " • research/OpenFvgEdge.cs</p></main></body></html>");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "open_fvg_edge.html"), h.ToString());
        File.WriteAllText(Path.Combine(outDir, "open_fvg_edge_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
