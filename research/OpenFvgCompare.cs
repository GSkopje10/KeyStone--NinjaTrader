// OPENING FVG • MNQ — THE COMPARISON. Same engine as the app (KeystoneOpenFvg.Run), 6 years of MNQ:
// • every target × stop ($ for 10 MNQ) at entries 10 / 25 / 50 % of the zone — whole period, 2020–23 vs 2024–26 (does it hold?)
// • years, every month, every week; the longest winning / losing day runs (and the best ones by $)
// • prop: 10 / 20 / 30 / 40 / 50 evaluations, ROTATE (one account a trade, the next one the next) vs COPY (every account every trade)
//   PASS FIRST: a batch of N evaluations trades until each one passed or blew; then the passed (funded) ones trade until each one
//   blew or retired (5 payouts); then the next batch of N. LUCID FLEX 50K rules (OpenFvgStudy.Step).
// • filters that might do better (FVGs in a row, the time the zone was set, buys / sells only)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class OpenFvgCompare
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);

    public sealed class Cfg { public double Pct, Tgt, Stop; public string Name { get { return "+$" + Tgt.ToString("N0", IC) + " / −$" + Stop.ToString("N0", IC) + " @ " + Pct + "%"; } } }
    public sealed class Row { public Cfg C; public string Filter = "ALL"; public List<OpenFvgStudy.T> Ts; public OpenFvgStudy.Stats All, A, B; public PR Prop; }

    // ---- prop batches: N evaluations, ROTATE or COPY, pass first, then funded, then the next batch
    public sealed class PR
    {
        public int N; public bool Copy; public int Batches, Evals, Passed, Blown, FBlown, FRetired, Payouts, Days; public double Spent, Kept, Low, MaxUp;
        public double Net { get { return Kept - Spent; } }
        public double PassPct { get { return Passed + Blown == 0 ? 0 : 100.0 * Passed / (Passed + Blown); } }
        public double Roi { get { return Spent == 0 ? 0 : 100.0 * Net / Spent; } }
    }
    public static PR Prop(List<OpenFvgStudy.T> ts, KeystoneSimRules r, int n, bool copy)
    {
        var p = new PR { N = n, Copy = copy }; List<OpenFvgStudy.Acct> evals = null, funded = null, passed = new List<OpenFvgStudy.Acct>(); int last = -1; double net = 0;
        foreach (var t in ts)
        {
            if (evals == null && funded == null)
            {
                evals = Enumerable.Range(0, n).Select(i => new OpenFvgStudy.Acct { Id = i }).ToList(); passed = new List<OpenFvgStudy.Acct>(); last = -1;
                p.Batches++; p.Evals += n; p.Spent += n * r.EvalCost; net -= n * r.EvalCost;
            }
            p.Days++;
            if (!t.D.Filled) continue;
            var pool = evals ?? funded; var live = pool.Where(a => a.End == "").OrderBy(a => a.Id).ToList();
            var takers = copy ? live : new List<OpenFvgStudy.Acct> { live.FirstOrDefault(a => a.Id > last) ?? live[0] };
            if (!copy) last = takers[0].Id;
            foreach (var a in takers)
            {
                double k0 = a.Kept; OpenFvgStudy.Step(a, t.D.Pnl, r);
                if (a.Kept > k0) { p.Kept += a.Kept - k0; net += a.Kept - k0; p.Payouts++; }
                if (a.End == "PASSED") { p.Passed++; passed.Add(new OpenFvgStudy.Acct { Id = a.Id, Funded = true }); net -= r.Activation; p.Spent += r.Activation; }
                else if (a.End == "BLOWN") { if (a.Funded) p.FBlown++; else p.Blown++; }
                else if (a.End == "RETIRED") p.FRetired++;
            }
            p.Low = Math.Min(p.Low, net); p.MaxUp = Math.Max(p.MaxUp, net);
            if (evals != null && evals.All(a => a.End != "")) { evals = null; funded = passed.Count > 0 ? passed : null; last = -1; }
            else if (funded != null && funded.All(a => a.End != "")) funded = null;
        }
        return p;
    }

    static OpenFvgStudy.Stats Of(IEnumerable<OpenFvgStudy.T> ts) { return OpenFvgStudy.Of(ts); }

    public sealed class Run1 { public DateTime From, To; public int Days; public double Pnl; }
    // runs of winning / losing TRADE DAYS (days without a trade are skipped, they don't break a run)
    public static List<Run1> Runs(List<OpenFvgStudy.T> ts, bool wins)
    {
        var o = new List<Run1>(); Run1 cur = null;
        foreach (var t in ts.Where(x => x.D.Filled))
        {
            bool w = t.D.Pnl > 0;
            if (w == wins) { if (cur == null) cur = new Run1 { From = t.Day }; cur.To = t.Day; cur.Days++; cur.Pnl += t.D.Pnl; }
            else if (cur != null) { o.Add(cur); cur = null; }
        }
        if (cur != null) o.Add(cur);
        return o;
    }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = m => { Console.WriteLine(m); log.AppendLine(m); };
        mnq = mnq.OrderBy(d => d.Day).ToList();
        var bars = mnq.Select(OpenFvgStudy.BarsOf).ToList(); var dates = mnq.Select(d => d.Day.Date).ToList();
        var rules = KeystoneSimRules.Presets()[1].Copy(); var baseR = KeystoneOpeningRange.Presets().First(x => x.Mode == "FVG");
        say("OPENING FVG COMPARISON • " + dates[0].ToString("yyyy-MM-dd") + " → " + dates[dates.Count - 1].ToString("yyyy-MM-dd") + " • " + dates.Count + " days");

        // 1) the grid: target × stop × entry %
        var tgts = new double[] { 500, 1000, 1500, 2000, 3000 }; var stops = new double[] { 500, 1000, 1500, 2000 }; var pcts = new double[] { 10, 25, 50 };
        var rows = new List<Row>();
        foreach (var pc in pcts) foreach (var sv in stops) foreach (var tv in tgts)
        {
            var r = baseR.Copy(); r.FvgPct = pc; r.StopUsd = sv; r.TargetUsd = tv;
            var ts = OpenFvgStudy.Trades(bars, dates, r);
            var row = new Row { C = new Cfg { Pct = pc, Tgt = tv, Stop = sv }, Ts = ts, All = Of(ts), A = Of(ts.Where(x => x.Day < Half)), B = Of(ts.Where(x => x.Day >= Half)) };
            row.Prop = Prop(ts, rules, 10, false); rows.Add(row);
        }
        foreach (var x in rows.OrderByDescending(x => x.All.Pnl).Take(10)) say("  " + x.C.Name + " • live $" + x.All.Pnl.ToString("N0", IC) + " (2020–23 $" + x.A.Pnl.ToString("N0", IC) + " • 2024–26 $" + x.B.Pnl.ToString("N0", IC) + ") • 10 evals rotate net $" + x.Prop.Net.ToString("N0", IC));
        Func<Row, bool> stable = x => x.A.Pnl > 0 && x.B.Pnl > 0;
        var def = rows.First(x => x.C.Pct == 50 && x.C.Tgt == 1000 && x.C.Stop == 2000);
        var bestLive = rows.Where(stable).OrderByDescending(x => x.All.Pnl).FirstOrDefault() ?? rows.OrderByDescending(x => x.All.Pnl).First();
        var bestProp = rows.OrderByDescending(x => x.Prop.Net).First();
        say("DEFAULT " + def.C.Name + " • BEST LIVE (both halves +) " + bestLive.C.Name + " • BEST PROP " + bestProp.C.Name);

        // 2) your brackets at 50% + the best ones: accounts 10–50, rotate vs copy
        var mine = new List<Row>();
        foreach (var b in new[] { new[] { 500.0, 1000 }, new[] { 1000.0, 1000 }, new[] { 2000.0, 2000 }, new[] { 1000.0, 2000 }, new[] { 1500.0, 1000 }, new[] { 1000.0, 500 } })
        { var x = rows.FirstOrDefault(y => y.C.Pct == 50 && y.C.Tgt == b[0] && y.C.Stop == b[1]); if (x != null && !mine.Contains(x)) mine.Add(x); }
        foreach (var x in new[] { bestLive, bestProp }) if (!mine.Contains(x)) mine.Add(x);
        var counts = new[] { 10, 20, 30, 40, 50 };
        var acc = mine.Select(x => Tuple.Create(x, counts.Select(n => Prop(x.Ts, rules, n, false)).ToList(), counts.Select(n => Prop(x.Ts, rules, n, true)).ToList())).ToList();

        // 3) filters on the default and the best live rule
        var filters = new List<Tuple<string, Func<OpenFvgStudy.T, bool>>>
        {
            Tuple.Create("every day (no filter)", (Func<OpenFvgStudy.T, bool>)(t => true)),
            Tuple.Create("only 2+ FVGs in a row", (Func<OpenFvgStudy.T, bool>)(t => t.D.Stack >= 2)),
            Tuple.Create("only 1 FVG (no stack)", (Func<OpenFvgStudy.T, bool>)(t => t.D.Stack == 1)),
            Tuple.Create("zone set by 10:00", (Func<OpenFvgStudy.T, bool>)(t => t.D.SetupTime <= t.Day.AddHours(10))),
            Tuple.Create("zone set after 09:45 (skip the first)", (Func<OpenFvgStudy.T, bool>)(t => t.D.SetupTime > t.Day.AddHours(9).AddMinutes(45))),
            Tuple.Create("buys only", (Func<OpenFvgStudy.T, bool>)(t => t.D.Dir > 0)),
            Tuple.Create("sells only", (Func<OpenFvgStudy.T, bool>)(t => t.D.Dir < 0)),
            Tuple.Create("filled by 11:00", (Func<OpenFvgStudy.T, bool>)(t => t.D.Filled && t.D.FillTime <= t.Day.AddHours(11))),
            Tuple.Create("no Monday", (Func<OpenFvgStudy.T, bool>)(t => t.Day.DayOfWeek != DayOfWeek.Monday)),
        };
        Func<Row, List<Tuple<string, OpenFvgStudy.Stats, OpenFvgStudy.Stats, OpenFvgStudy.Stats, PR>>> filt = x => filters.Select(f =>
        {
            var ts = x.Ts.Select(t => f.Item2(t) ? t : new OpenFvgStudy.T { Day = t.Day, D = new KeystoneOpenFvgDay { Day = t.Day } }).ToList();   // filtered out = no trade that day
            return Tuple.Create(f.Item1, Of(ts), Of(ts.Where(t => t.Day < Half)), Of(ts.Where(t => t.Day >= Half)), Prop(ts, rules, 10, false));
        }).ToList();
        var fDef = filt(def); var fBest = bestLive == def ? null : filt(bestLive);

        // ---- HTML
        var h = new StringBuilder();
        Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC);
        Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> pct = v => v.ToString("0.0", IC) + "%";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Opening FVG Compare</title><style>");
        h.Append(":root{--bg:#0d1117;--card:#161b22;--line:#30363d;--text:#e6edf3;--mute:#8b949e;--pos:#3fb950;--neg:#f85149;--acc:#d29922;--blue:#58a6ff;--pbg:rgba(63,185,80,.16);--nbg:rgba(248,81,73,.16)}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f6f8fa;--card:#fff;--line:#d0d7de;--text:#1f2328;--mute:#59636e;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--blue:#0969da;--pbg:rgba(26,127,55,.12);--nbg:rgba(207,34,46,.12)}}");
        h.Append("body{background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:24px 16px}main{max-width:1200px;margin:0 auto}h1{font-size:24px;margin:0 0 4px}h2{font-size:17px;margin:30px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}h3{font-size:14px;margin:16px 0 6px;color:var(--acc)}");
        h.Append(".sub{color:var(--mute)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:10px;margin:14px 0}.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:12px}.card b{display:block;font-size:19px}.card small{color:var(--mute)}");
        h.Append(".wrap{overflow-x:auto;margin-bottom:6px}table{border-collapse:collapse;width:100%;background:var(--card);border:1px solid var(--line);font-variant-numeric:tabular-nums}th,td{padding:5px 8px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}th{color:var(--mute);font-weight:600;font-size:12px}");
        h.Append(".pos{color:var(--pos)}.neg{color:var(--neg)}.hp{background:var(--pbg)}.hn{background:var(--nbg)}.best{outline:2px solid var(--acc);outline-offset:-2px}.note{background:var(--card);border-left:3px solid var(--acc);padding:10px 14px;border-radius:6px;margin:10px 0}.tag{font-size:11px;color:var(--acc);font-weight:600}</style></head><body><main>");
        h.Append("<h1>★ OPENING FVG • MNQ — the comparison</h1><div class='sub'>" + dates[0].ToString("MMM d, yyyy", IC) + " → " + dates[dates.Count - 1].ToString("MMM d, yyyy", IC) + " • " + dates.Count + " trading days • 10 MNQ • $1.74 a contract costs • the app's own engine • prop: LUCID FLEX 50K ($120 evaluation, $3,000 target, $2,000 EOD trailing drawdown, 50% consistency; funded payouts 50% up to $2,000, 90% kept, 5 payouts then retired)</div>");
        h.Append("<div class='cards'>");
        Action<string, Row, string> card = (k, x, why) => h.Append("<div class='card'><small>" + k + "</small><b>" + x.C.Name + "</b><small>live 10 MNQ <span class='" + cls(x.All.Pnl) + "'>" + usd(x.All.Pnl) + "</span> • won " + pct(x.All.WinPct) + " • max DD " + usd(x.All.MaxDd) + "<br>2020–23 <span class='" + cls(x.A.Pnl) + "'>" + usd(x.A.Pnl) + "</span> • 2024–26 <span class='" + cls(x.B.Pnl) + "'>" + usd(x.B.Pnl) + "</span><br>10 evals rotating: <span class='" + cls(x.Prop.Net) + "'>" + usd(x.Prop.Net) + "</span> (" + x.Prop.Passed + " passed)<br>" + why + "</small></div>");
        card("YOUR DEFAULT", def, "");
        card("BEST LIVE, UP IN BOTH HALVES", bestLive, "the highest 6-year total among the rules that made money in 2020–23 AND 2024–26");
        card("BEST FOR PROP (10 EVALS, ROTATING)", bestProp, "the most kept minus spent; check its halves before trusting it");
        h.Append("</div>");
        h.Append("<div class='note'><b>How to read this.</b> Every rule here was run on the same 6 years. A rule that is the best of 60 is partly the best because of luck — so the tables show 2020–23 and 2024–26 separately: a rule that is up in both halves is more likely real. Green cells = money made, red = lost.</div>");

        // GRID
        h.Append("<h2>1 • Target × stop × entry % (live 10 MNQ, 6 years)</h2>");
        foreach (var pc in pcts)
        {
            h.Append("<h3>Entry at " + pc + "% of the zone — net $ (won %), 2020–23 | 2024–26 underneath</h3><div class='wrap'><table><tr><th>STOP \\ TARGET</th>" + string.Concat(tgts.Select(t => "<th>+" + usd(t) + "</th>")) + "</tr>");
            foreach (var sv in stops)
            {
                h.Append("<tr><td>−" + usd(sv) + "</td>");
                foreach (var tv in tgts)
                {
                    var x = rows.First(y => y.C.Pct == pc && y.C.Stop == sv && y.C.Tgt == tv);
                    h.Append("<td class='" + (x.All.Pnl > 0 ? "hp" : "hn") + (x == bestLive || x == bestProp ? " best" : "") + "'><b class='" + cls(x.All.Pnl) + "'>" + usd(x.All.Pnl) + "</b> <span class='sub'>(" + x.All.WinPct.ToString("0") + "%)</span><br><small class='" + cls(x.A.Pnl) + "'>" + usd(x.A.Pnl) + "</small> | <small class='" + cls(x.B.Pnl) + "'>" + usd(x.B.Pnl) + "</small></td>");
                }
                h.Append("</tr>");
            }
            h.Append("</table></div>");
        }
        h.Append("<h3>The 15 best by 10-evaluation prop result (rotating)</h3><div class='wrap'><table><tr><th>RULE</th><th>WON</th><th>LIVE NET</th><th>2020–23</th><th>2024–26</th><th>MAX DD</th><th>EVALS</th><th>PASSED</th><th>PASS %</th><th>PAYOUTS</th><th>SPENT</th><th>KEPT</th><th>PROP NET</th><th>MOST $ DOWN</th></tr>");
        foreach (var x in rows.OrderByDescending(y => y.Prop.Net).Take(15))
            h.Append("<tr class='" + (x == def ? "best" : "") + "'><td>" + x.C.Name + (stable(x) ? " <span class='tag'>BOTH HALVES +</span>" : "") + "</td><td>" + pct(x.All.WinPct) + "</td><td class='" + cls(x.All.Pnl) + "'>" + usd(x.All.Pnl) + "</td><td class='" + cls(x.A.Pnl) + "'>" + usd(x.A.Pnl) + "</td><td class='" + cls(x.B.Pnl) + "'>" + usd(x.B.Pnl) + "</td><td>" + usd(x.All.MaxDd) + "</td><td>" + x.Prop.Evals + "</td><td>" + x.Prop.Passed + "</td><td>" + pct(x.Prop.PassPct) + "</td><td>" + x.Prop.Payouts + "</td><td>" + usd(x.Prop.Spent) + "</td><td>" + usd(x.Prop.Kept) + "</td><td class='" + cls(x.Prop.Net) + "'><b>" + usd(x.Prop.Net) + "</b></td><td class='neg'>" + usd(x.Prop.Low) + "</td></tr>");
        h.Append("</table></div>");

        // ACCOUNTS
        h.Append("<h2>2 • How many evaluations: 10 / 20 / 30 / 40 / 50 • 🔄 ROTATE vs 📋 COPY</h2><div class='note'>PASS FIRST: a batch of N evaluations trades until each one passed or blew; the passed ones then trade funded until each one blew or retired; then the next batch of N. <b>ROTATE</b>: the day's trade goes to the next account in turn (each account trades every N-th day). <b>COPY</b>: every account takes every trade — the batch passes or blows together, so N only multiplies the money in and out. MOST $ DOWN = the deepest the running total went (money you need to have).</div>");
        foreach (var a in acc)
        {
            var x = a.Item1;
            h.Append("<h3>" + x.C.Name + (x == def ? " (default)" : x == bestLive ? " (best live)" : x == bestProp ? " (best prop)" : "") + " — won " + pct(x.All.WinPct) + ", live " + usd(x.All.Pnl) + "</h3><div class='wrap'><table><tr><th>ACCOUNTS</th><th>HOW</th><th>BATCHES</th><th>EVALS</th><th>PASSED</th><th>PASS %</th><th>FUNDED BLOWN / RETIRED</th><th>PAYOUTS</th><th>SPENT</th><th>KEPT</th><th>NET</th><th>RETURN</th><th>MOST $ DOWN</th></tr>");
            for (int i = 0; i < counts.Length; i++)
                foreach (var pr in new[] { a.Item2[i], a.Item3[i] })
                    h.Append("<tr><td>" + pr.N + "</td><td>" + (pr.Copy ? "📋 COPY" : "🔄 ROTATE") + "</td><td>" + pr.Batches + "</td><td>" + pr.Evals + "</td><td>" + pr.Passed + "</td><td>" + pct(pr.PassPct) + "</td><td>" + pr.FBlown + " / " + pr.FRetired + "</td><td>" + pr.Payouts + "</td><td>" + usd(pr.Spent) + "</td><td>" + usd(pr.Kept) + "</td><td class='" + cls(pr.Net) + "'><b>" + usd(pr.Net) + "</b></td><td class='" + cls(pr.Roi) + "'>" + pr.Roi.ToString("0", IC) + "%</td><td class='neg'>" + usd(pr.Low) + "</td></tr>");
            h.Append("</table></div>");
        }

        // CALENDAR for the default and the best live rule
        foreach (var x in new[] { def, bestLive }.Distinct())
        {
            var f = x.Ts.Where(t => t.D.Filled).ToList();
            h.Append("<h2>" + (x == def ? "3" : "4") + " • " + x.C.Name + (x == def ? " (default)" : " (best live)") + " — years, months, weeks, streaks</h2>");
            h.Append("<h3>Years</h3><div class='wrap'><table><tr><th>YEAR</th><th>TRADES</th><th>WON</th><th>NET</th><th>MAX DD</th><th>GREEN MONTHS</th><th>GREEN WEEKS</th><th>BEST MONTH</th><th>WORST MONTH</th></tr>");
            foreach (var y in f.GroupBy(t => t.Day.Year).OrderBy(g => g.Key))
            {
                var st = Of(y); var ms = y.GroupBy(t => t.Day.Month).Select(g => g.Sum(t => t.D.Pnl)).ToList(); var ws = y.GroupBy(t => Wk(t.Day)).Select(g => g.Sum(t => t.D.Pnl)).ToList();
                h.Append("<tr><td>" + y.Key + "</td><td>" + st.Fills + "</td><td>" + pct(st.WinPct) + "</td><td class='" + cls(st.Pnl) + "'><b>" + usd(st.Pnl) + "</b></td><td>" + usd(st.MaxDd) + "</td><td>" + ms.Count(v => v > 0) + " / " + ms.Count + "</td><td>" + ws.Count(v => v > 0) + " / " + ws.Count + "</td><td class='pos'>" + usd(ms.Max()) + "</td><td class='neg'>" + usd(ms.Min()) + "</td></tr>");
            }
            h.Append("</table></div><h3>Every month (net $)</h3><div class='wrap'><table><tr><th>YEAR</th>" + string.Concat(Enumerable.Range(1, 12).Select(m => "<th>" + new DateTime(2000, m, 1).ToString("MMM", IC).ToUpperInvariant() + "</th>")) + "<th>YEAR</th></tr>");
            foreach (var y in f.GroupBy(t => t.Day.Year).OrderBy(g => g.Key))
            {
                h.Append("<tr><td>" + y.Key + "</td>");
                for (int m = 1; m <= 12; m++) { var mm = y.Where(t => t.Day.Month == m).ToList(); if (mm.Count == 0) { h.Append("<td class='sub'>–</td>"); continue; } double v = mm.Sum(t => t.D.Pnl); h.Append("<td class='" + (v > 0 ? "hp" : "hn") + "'><span class='" + cls(v) + "'>" + usd(v) + "</span><br><small class='sub'>" + mm.Count(t => t.D.Pnl > 0) + "/" + mm.Count + "</small></td>"); }
                double yv = y.Sum(t => t.D.Pnl); h.Append("<td class='" + cls(yv) + "'><b>" + usd(yv) + "</b></td></tr>");
            }
            h.Append("</table></div>");
            var weeks = f.GroupBy(t => Wk(t.Day)).Select(g => Tuple.Create(g.Key, g.Count(), g.Count(t => t.D.Pnl > 0), g.Sum(t => t.D.Pnl))).ToList();
            h.Append("<h3>Weeks: " + weeks.Count(w => w.Item4 > 0) + " green of " + weeks.Count + " (" + pct(100.0 * weeks.Count(w => w.Item4 > 0) / Math.Max(1, weeks.Count)) + ") • average week " + usd(weeks.Average(w => w.Item4)) + "</h3><div class='wrap'><table><tr><th>THE 10 BEST WEEKS</th><th>TRADES</th><th>WON</th><th>NET</th><th>THE 10 WORST WEEKS</th><th>TRADES</th><th>WON</th><th>NET</th></tr>");
            var bw = weeks.OrderByDescending(w => w.Item4).Take(10).ToList(); var ww = weeks.OrderBy(w => w.Item4).Take(10).ToList();
            for (int i = 0; i < Math.Min(bw.Count, ww.Count); i++) h.Append("<tr><td>week of " + bw[i].Item1.ToString("yyyy-MM-dd") + "</td><td>" + bw[i].Item2 + "</td><td>" + bw[i].Item3 + "</td><td class='pos'>" + usd(bw[i].Item4) + "</td><td>week of " + ww[i].Item1.ToString("yyyy-MM-dd") + "</td><td>" + ww[i].Item2 + "</td><td>" + ww[i].Item3 + "</td><td class='neg'>" + usd(ww[i].Item4) + "</td></tr>");
            h.Append("</table></div>");
            // weekly distribution
            var wb = new[] { -1e9, -5000, -2500, -1000, 0, 1000, 2500, 5000, 1e9 }; var wl = new[] { "under −$5K", "−$5K to −$2.5K", "−$2.5K to −$1K", "−$1K to $0", "$0 to $1K", "$1K to $2.5K", "$2.5K to $5K", "over $5K" };
            h.Append("<div class='wrap'><table><tr><th>A WEEK ENDS</th>" + string.Concat(wl.Select(l => "<th>" + l + "</th>")) + "</tr><tr><td>weeks</td>" + string.Concat(Enumerable.Range(0, wl.Length).Select(i => "<td>" + weeks.Count(w => w.Item4 > wb[i] && w.Item4 <= wb[i + 1]) + "</td>")) + "</tr></table></div>");
            // streaks
            var win = Runs(x.Ts, true); var los = Runs(x.Ts, false);
            Func<List<Run1>, int> longest = l => l.Count == 0 ? 0 : l.Max(r => r.Days);
            h.Append("<h3>Streaks of trade days: longest winning run " + longest(win) + " days • longest losing run " + longest(los) + " days • average winning run " + (win.Count == 0 ? 0 : win.Average(r => r.Days)).ToString("0.0", IC) + " • average losing run " + (los.Count == 0 ? 0 : los.Average(r => r.Days)).ToString("0.0", IC) + "</h3>");
            h.Append("<div class='wrap'><table><tr><th>LONGEST WINNING RUNS</th><th>DAYS</th><th>$</th><th>LONGEST LOSING RUNS</th><th>DAYS</th><th>$</th><th>BEST RUNS BY $</th><th>DAYS</th><th>$</th></tr>");
            var lw = win.OrderByDescending(r => r.Days).ThenByDescending(r => r.Pnl).Take(8).ToList(); var ll = los.OrderByDescending(r => r.Days).ThenBy(r => r.Pnl).Take(8).ToList(); var lb = win.OrderByDescending(r => r.Pnl).Take(8).ToList();
            Func<Run1, string> span = r => r.From.ToString("yyyy-MM-dd") + (r.To != r.From ? " → " + r.To.ToString("MM-dd") : "");
            for (int i = 0; i < Math.Min(lw.Count, Math.Min(ll.Count, lb.Count)); i++) h.Append("<tr><td>" + span(lw[i]) + "</td><td>" + lw[i].Days + "</td><td class='pos'>" + usd(lw[i].Pnl) + "</td><td>" + span(ll[i]) + "</td><td>" + ll[i].Days + "</td><td class='neg'>" + usd(ll[i].Pnl) + "</td><td>" + span(lb[i]) + "</td><td>" + lb[i].Days + "</td><td class='pos'>" + usd(lb[i].Pnl) + "</td></tr>");
            h.Append("</table></div>");
            // how often a run of length k happens
            h.Append("<div class='wrap'><table><tr><th>RUN LENGTH</th>" + string.Concat(Enumerable.Range(1, 8).Select(k => "<th>" + k + (k == 8 ? "+" : "") + "</th>")) + "</tr><tr><td>winning runs</td>" + string.Concat(Enumerable.Range(1, 8).Select(k => "<td>" + win.Count(r => k == 8 ? r.Days >= 8 : r.Days == k) + "</td>")) + "</tr><tr><td>losing runs</td>" + string.Concat(Enumerable.Range(1, 8).Select(k => "<td>" + los.Count(r => k == 8 ? r.Days >= 8 : r.Days == k) + "</td>")) + "</tr></table></div>");
        }

        // FILTERS
        foreach (var fx in new[] { Tuple.Create(def, fDef), Tuple.Create(bestLive, fBest) }.Where(z => z.Item2 != null))
        {
            h.Append("<h2>" + (fx.Item1 == def ? "5" : "6") + " • Filters on " + fx.Item1.C.Name + " — can a simple rule do better?</h2><div class='wrap'><table><tr><th>TAKE THE TRADE WHEN</th><th>TRADES</th><th>WON</th><th>NET</th><th>2020–23</th><th>2024–26</th><th>PF</th><th>MAX DD</th><th>10 EVALS ROTATING: PASSED</th><th>PROP NET</th></tr>");
            foreach (var r in fx.Item2) h.Append("<tr><td>" + r.Item1 + (r.Item3.Pnl > 0 && r.Item4.Pnl > 0 ? " <span class='tag'>BOTH HALVES +</span>" : "") + "</td><td>" + r.Item2.Fills + "</td><td>" + pct(r.Item2.WinPct) + "</td><td class='" + cls(r.Item2.Pnl) + "'><b>" + usd(r.Item2.Pnl) + "</b></td><td class='" + cls(r.Item3.Pnl) + "'>" + usd(r.Item3.Pnl) + "</td><td class='" + cls(r.Item4.Pnl) + "'>" + usd(r.Item4.Pnl) + "</td><td>" + r.Item2.Pf.ToString("0.00", IC) + "</td><td>" + usd(r.Item2.MaxDd) + "</td><td>" + r.Item5.Passed + " / " + r.Item5.Evals + "</td><td class='" + cls(r.Item5.Net) + "'>" + usd(r.Item5.Net) + "</td></tr>");
            h.Append("</table></div>");
        }
        h.Append("<p class='sub'>Built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + " • research/OpenFvgCompare.cs • every rule uses the same fills: the limit at its price (or the open through it), the fill minute only stops out, then the stop before the target, flat at 15:55.</p></main></body></html>");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "open_fvg_compare.html"), h.ToString());
        File.WriteAllText(Path.Combine(outDir, "open_fvg_compare_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }

    static DateTime Wk(DateTime d) { int k = ((int)d.DayOfWeek + 6) % 7; return d.Date.AddDays(-k); }
}
