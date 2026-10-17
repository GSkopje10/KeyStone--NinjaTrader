// TWO-PHASE PROP SEARCH — a plan built for the prop rules, not around one entry: the EVALUATION and the FUNDED stage each get
// their own strategy (aggressive to pass fast, steadier to collect payouts), every pair tested, rotating (each trade to the next
// of 50 accounts, batch by batch) and copied (every account takes every trade). LUCID FLEX 50K rules, trailing drawdown trade by
// trade. Entry families: FVG RETRACE (as the app trades it), MOMENTUM (the side of the last 15 min), RANDOM (control); brackets
// from ±$750 to +$2,000 / −$1,000. Chosen on 2020–23, judged on 2024–26.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class TwoPhaseStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);
    sealed class Gen { public string Name; public List<List<FreqStudy.Tr>> Days; public double Scale = 1; }
    sealed class Res { public Gen E, F; public string Mode; public AccountTypeStudy.Out A, B, All; public double Score { get { return Math.Min(A.Roi, B.Roi); } } public double PerYear; }

    static AccountTypeStudy.Out Rotate(List<DateTime> dates, int from, int to, Gen ge, Gen gf, KeystoneSimRules r, int n)
    {
        var o = new AccountTypeStudy.Out(); List<AccountTypeStudy.Acc> evals = null, funded = null; var cur = new AccountTypeStudy.Batch(); int last = -1;
        for (int i = from; i < to; i++)
        {
            var day = dates[i]; string mk = day.ToString("yyyy-MM", IC); if (!o.Month.ContainsKey(mk)) o.Month[mk] = new double[4];
            if (evals == null && funded == null) { evals = Enumerable.Range(0, n).Select(k => new AccountTypeStudy.Acc { Id = k, Start = day }).ToList(); last = -1; o.Batches++; o.Bought += n; o.Spent += n * r.EvalCost; o.Month[mk][0] += n * r.EvalCost; cur = new AccountTypeStudy.Batch(); }
            var g = evals != null ? ge : gf; var pool = evals ?? funded; var used = new HashSet<AccountTypeStudy.Acc>();
            foreach (var t in g.Days[i])
            {
                var live = pool.Where(a => a.End == "" && !used.Contains(a)).OrderBy(a => a.Id).ToList(); if (live.Count == 0) break;
                var a0 = live.FirstOrDefault(a => a.Id > last) ?? live[0]; last = a0.Id; used.Add(a0);
                AccountTypeStudy.Step(a0, t.Pnl * g.Scale, t.Mfe * g.Scale, r, day, o, cur);
                if (a0.End == "PASSED") { o.Passed++; o.Month[mk][3]++; o.Spent += r.Activation; }
            }
            if (evals != null && evals.All(a => a.End != "")) { funded = evals.Where(a => a.End == "PASSED").Select(a => new AccountTypeStudy.Acc { Id = a.Id, Funded = true, FundedOn = day }).ToList(); if (funded.Count == 0) funded = null; evals = null; last = -1; }
            else if (funded != null && funded.All(a => a.End != "")) funded = null;
            if (o.Kept - o.Spent < o.Low) { o.Low = o.Kept - o.Spent; o.LowDay = day; }
        }
        foreach (var m in o.Month.Values) { double v = m[1] - m[0]; if (v > 0) o.MonthsUp++; else if (v < 0) o.MonthsDown++; }
        return o;
    }
    static AccountTypeStudy.Out Copy(List<DateTime> dates, int from, int to, Gen ge, Gen gf, KeystoneSimRules r)
    {
        var o = new AccountTypeStudy.Out(); var cur = new AccountTypeStudy.Batch(); AccountTypeStudy.Acc acc = null;
        for (int i = from; i < to; i++)
        {
            var day = dates[i]; string mk = day.ToString("yyyy-MM", IC); if (!o.Month.ContainsKey(mk)) o.Month[mk] = new double[4];
            if (acc == null) { acc = new AccountTypeStudy.Acc { Start = day }; o.Spent += r.EvalCost; o.Bought++; o.Month[mk][0] += r.EvalCost; }
            var g = acc.Funded ? gf : ge;
            foreach (var t in g.Days[i])
            {
                AccountTypeStudy.Step(acc, t.Pnl * g.Scale, t.Mfe * g.Scale, r, day, o, cur);
                if (acc.End == "PASSED") { o.Passed++; o.Month[mk][3]++; acc = new AccountTypeStudy.Acc { Funded = true, FundedOn = day }; o.Spent += r.Activation; break; }
                if (acc.End != "") { acc = null; break; }
            }
            if (o.Kept - o.Spent < o.Low) { o.Low = o.Kept - o.Spent; o.LowDay = day; }
        }
        foreach (var m in o.Month.Values) { double v = m[1] - m[0]; if (v > 0) o.MonthsUp++; else if (v < 0) o.MonthsDown++; }
        return o;
    }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var days = mnq.OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int split = dates.FindIndex(d => d >= Half);
        var r = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID FLEX 50K")).Copy();
        Func<List<FreqStudy.Tr>, List<FreqStudy.Tr>> solo = l => { var o2 = new List<FreqStudy.Tr>(); int busy = -1; foreach (var t in l) { if (t.Slot <= busy) continue; o2.Add(t); busy = t.Exit; } return o2; };
        string[] br = { "±$500", "+$750/−$500", "+$1,000/−$500", "±$1,000", "+$1,500/−$1,000", "+$2,000/−$1,000", "±$1,500" };
        var gens = new List<Gen>();
        foreach (int b in new[] { 6, 5, 4, 3 }) foreach (int w in new[] { 1, 0 }) gens.Add(new Gen { Name = "FVG RETRACE " + br[b] + (w == 0 ? " • 09:30–11:30" : " • 09:30–15:00"), Days = days.Select(d => FreqStudy.AppDay(d, w, 1, b, 0.10)).ToList() });
        gens.Add(new Gen { Name = "FVG RETRACE ±$750 (5 MNQ) • 09:30–15:00", Days = gens[0].Days, Scale = 0.5 });
        foreach (int b in new[] { 6, 4 }) foreach (int w in new[] { 1, 0 }) gens.Add(new Gen { Name = "MOMENTUM 15M " + br[b] + (w == 0 ? " • 09:30–11:30" : " • 09:30–15:00"), Days = days.Select(d => solo(FreqStudy.DayTrades(d, 2, w, b))).ToList() });
        gens.Add(new Gen { Name = "RANDOM 15M ±$1,500 • 09:30–15:00 (control)", Days = days.Select(d => solo(FreqStudy.DayTrades(d, 4, 1, 6))).ToList() });
        var res = new List<Res>();
        foreach (var ge in gens) foreach (var gf in gens)
        {
            res.Add(new Res { E = ge, F = gf, Mode = "ROTATE 50", A = Rotate(dates, 0, split, ge, gf, r, 50), B = Rotate(dates, split, dates.Count, ge, gf, r, 50), All = Rotate(dates, 0, dates.Count, ge, gf, r, 50) });
            res.Add(new Res { E = ge, F = gf, Mode = "COPY (per account)", A = Copy(dates, 0, split, ge, gf, r), B = Copy(dates, split, dates.Count, ge, gf, r), All = Copy(dates, 0, dates.Count, ge, gf, r) });
        }
        double yrs = (dates[dates.Count - 1] - dates[0]).TotalDays / 365.25; foreach (var x in res) x.PerYear = x.All.Net / yrs;
        say(res.Count + " plans in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        var baseRot = res.First(x => x.E == gens[0] && x.F == gens[0] && x.Mode.StartsWith("ROTATE")); var baseCopy = res.First(x => x.E == gens[0] && x.F == gens[0] && x.Mode.StartsWith("COPY"));
        foreach (var mode in new[] { "ROTATE", "COPY" })
        {
            say("== " + mode + " (best by the worse period)");
            foreach (var x in res.Where(x => x.Mode.StartsWith(mode)).OrderByDescending(x => x.Score).Take(10)) say("  EVAL " + x.E.Name + " → FUNDED " + x.F.Name + " • ROI " + x.A.Roi.ToString("0.00") + " / " + x.B.Roi.ToString("0.00") + " • net/yr " + x.PerYear.ToString("N0", IC) + " • low " + x.All.Low.ToString("N0", IC) + " • pass " + (100.0 * x.All.Passed / Math.Max(1, x.All.Bought)).ToString("0") + "% paid " + x.All.FundedPaid + "/" + x.All.Passed);
        }
        // HTML
        var h = new StringBuilder(); Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC); Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Two-Phase Prop Search</title><style>:root{--bg:#0b0f14;--card:#141a22;--card2:#1b2330;--line:#2a3442;--text:#e8eef5;--mute:#8d9aab;--pos:#3fb950;--neg:#f85149;--acc:#e3b341}@media (prefers-color-scheme: light){:root{--bg:#f5f7fa;--card:#fff;--card2:#f0f3f7;--line:#d8dee6;--text:#1c2430;--mute:#5d6b7c;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700}}*{box-sizing:border-box}body{background:var(--bg);color:var(--text);font:14px/1.55 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:28px 16px}main{max-width:1280px;margin:0 auto}h1{font-size:26px;margin:0 0 6px}h2{font-size:19px;margin:34px 0 8px}.mute{color:var(--mute)}.pos{color:var(--pos)}.neg{color:var(--neg)}.box{background:var(--card);border:1px solid var(--line);border-left:4px solid var(--acc);border-radius:10px;padding:14px 18px;margin:14px 0}.wrap{overflow-x:auto;border:1px solid var(--line);border-radius:10px}table{border-collapse:collapse;width:100%;background:var(--card);font-variant-numeric:tabular-nums}th,td{padding:6px 9px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th{background:var(--card2);color:var(--mute)}td.l,th.l{text-align:left}tr.base td{background:rgba(227,179,65,.12)}</style></head><body><main>");
        h.Append("<h1>Two-phase prop search • " + gens.Count + " × " + gens.Count + " strategy pairs • rotate and copy</h1><p class='mute'>MNQ " + dates[0].ToString("yyyy-MM-dd", IC) + " → " + dates[dates.Count - 1].ToString("yyyy-MM-dd", IC) + " • LUCID FLEX 50K • every evaluation strategy paired with every funded strategy • ROTATE 50 = each trade to the next of 50 accounts, batch by batch • COPY = per account slot, every account takes every trade (× the accounts you copy to) • chosen by the worse of 2020–23 / 2024–26.</p>");
        Action<string, IEnumerable<Res>> tbl = (t, rows) => { h.Append("<h2>" + t + "</h2><div class='wrap'><table><tr><th class='l'>EVALUATION STRATEGY</th><th class='l'>FUNDED STRATEGY</th><th>ROI 20–23</th><th>ROI 24–26</th><th>NET / YEAR</th><th>LOWEST</th><th>PASS %</th><th>FUNDED PAID</th><th>PAYOUTS</th><th>MONTHS +/−</th></tr>"); foreach (var x in rows) h.Append("<tr class='" + (x == baseRot || x == baseCopy ? "base" : "") + "'><td class='l'>" + x.E.Name + "</td><td class='l'>" + x.F.Name + "</td><td class='" + cls(x.A.Roi - 1) + "'>" + x.A.Roi.ToString("0.00", IC) + "×</td><td class='" + cls(x.B.Roi - 1) + "'><b>" + x.B.Roi.ToString("0.00", IC) + "×</b></td><td class='" + cls(x.PerYear) + "'><b>" + usd(x.PerYear) + "</b></td><td class='neg'>" + usd(x.All.Low) + "</td><td>" + (100.0 * x.All.Passed / Math.Max(1, x.All.Bought)).ToString("0", IC) + "%</td><td>" + x.All.FundedPaid + " / " + x.All.Passed + "</td><td>" + x.All.Payouts + "</td><td>" + x.All.MonthsUp + " / " + x.All.MonthsDown + "</td></tr>"); h.Append("</table></div>"); };
        h.Append("<div class='box'>Highlighted row = today's plan (FVG RETRACE ±$1,500, 09:30–15:00, both stages). Everything above it in a table did better in its worse period.</div>");
        tbl("COPY — the 20 steadiest pairs (per copied account)", res.Where(x => x.Mode.StartsWith("COPY")).OrderByDescending(x => x.Score).Take(20).Concat(new[] { baseCopy }).Distinct());
        tbl("ROTATE 50 — the 20 steadiest pairs (all 50 accounts)", res.Where(x => x.Mode.StartsWith("ROTATE")).OrderByDescending(x => x.Score).Take(20).Concat(new[] { baseRot }).Distinct());
        tbl("COPY — the 15 that made the most per year (aggressive)", res.Where(x => x.Mode.StartsWith("COPY") && x.Score > 1).OrderByDescending(x => x.PerYear).Take(15));
        h.Append("</main></body></html>");
        Directory.CreateDirectory(outDir); File.WriteAllText(System.IO.Path.Combine(outDir, "two_phase.html"), h.ToString()); File.WriteAllText(System.IO.Path.Combine(outDir, "two_phase_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
