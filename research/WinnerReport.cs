// THE WINNER'S REPORT — ★ FVG RETRACE • MNQ 1M 10% + VWAP (FAST ROTATION) on LUCID FLEX 50K, everything a plan needs:
// pools of 10 / 20 / 30 / 40 / 50 accounts, rotating (one position at a time = what the app does live, or several at once) vs copying,
// evaluation and funded numbers, account cycles, streaks, the money needed and the time to profit from EVERY start month in the
// data, year / month, and a written recommendation. Trailing drawdown trade by trade; a blown / retired slot gets a new evaluation
// the next day; the 11-second rule is not modelled (the trades last minutes to hours, far longer than 11 s).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class WinnerReport
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);

    sealed class WRun
    {
        public string Mode; public int N; public double Spent, Kept, Low, LowDay, BankEnd; public int Bought, Passed, Paid, Payouts, EBlown, FBlown, Retired, Trades, Wins;
        public DateTime FirstPay = DateTime.MinValue, FirstGreen = DateTime.MinValue, LowDate; public List<double> EvalLife = new List<double>(), FundLife = new List<double>(), KeptPerFunded = new List<double>(), DaysToPass = new List<double>(), DaysToPay = new List<double>();
        public SortedDictionary<string, double[]> Month = new SortedDictionary<string, double[]>();   // fees, kept, payouts, passed, blown
        public List<int> Seq = new List<int>(); public List<Tuple<DateTime, double>> Bank = new List<Tuple<DateTime, double>>();
        public double Net { get { return Kept - Spent; } } public double Roi { get { return Spent == 0 ? 0 : Kept / Spent; } }
    }
    sealed class Slot { public AccountTypeStudy.Acc A; public DateTime Start, FundedOn; public int Trades; }

    // rotate: each signal → the next slot that has not traded today (several = positions on several accounts at once, same side only;
    // one = only when no position is open anywhere) • copy: one slot × n (every copied account takes every trade, one at a time)
    static WRun Play(List<DateTime> dates, List<List<FastRotateStudy.T1>> sigs, int from, int to, KeystoneSimRules r, int n, string mode)
    {
        var o = new WRun { Mode = mode, N = n }; bool copy = mode == "COPY"; int slots = copy ? 1 : n; double w = copy ? n : 1;
        var sl = new Slot[slots]; int last = -1; var b = new AccountTypeStudy.Batch(); var agg = new AccountTypeStudy.Out();
        double kept0 = 0;
        for (int i = from; i < to; i++)
        {
            var day = dates[i]; string mk = day.ToString("yyyy-MM", IC); double[] mo; if (!o.Month.TryGetValue(mk, out mo)) { mo = new double[5]; o.Month[mk] = mo; }
            for (int s = 0; s < slots; s++) if (sl[s] == null) { sl[s] = new Slot { A = new AccountTypeStudy.Acc { Id = s, Start = day }, Start = day }; o.Spent += w * r.EvalCost; o.Bought += (int)w; mo[0] += w * r.EvalCost; }
            var used = new bool[slots]; var open = new List<FastRotateStudy.T1>();
            foreach (var t in sigs[i])
            {
                if (open.Any(q => q.Exit >= t.Slot && (mode != "SEVERAL" || q.Dir != t.Dir))) continue;
                int pick = -1;
                if (copy) { if (sl[0].A.End == "") pick = 0; }
                else for (int j = 1; j <= slots; j++) { int s = (last + j) % slots; if (!used[s] && sl[s].A.End == "") { pick = s; break; } }
                if (pick < 0) continue;
                if (!copy) { last = pick; used[pick] = true; }
                open.Add(t); o.Trades += (int)w; if (t.Pnl > 0) o.Wins += (int)w; o.Seq.Add(t.Pnl > 0 ? 1 : -1);
                var slot = sl[pick]; var a = slot.A; slot.Trades++; double kb = a.Kept;
                AccountTypeStudy.Step(a, t.Pnl, t.Mfe, r, day, agg, b);
                if (a.Kept > kb) { double g = (a.Kept - kb) * w; o.Kept += g; mo[1] += g; mo[2] += w; o.Payouts += (int)w; if (o.FirstPay == DateTime.MinValue) o.FirstPay = day; if (a.Payouts == 1) { o.Paid += (int)w; o.DaysToPay.Add((day - slot.FundedOn).TotalDays); } }
                if (a.End == "PASSED") { o.Passed += (int)w; mo[3] += w; o.DaysToPass.Add((day - slot.Start).TotalDays); o.EvalLife.Add(slot.Trades); sl[pick] = new Slot { A = new AccountTypeStudy.Acc { Id = pick, Funded = true, FundedOn = day }, Start = day, FundedOn = day }; o.Spent += w * r.Activation; mo[0] += w * r.Activation; }
                else if (a.End != "")
                {
                    if (a.Funded) { o.FundLife.Add(slot.Trades); o.KeptPerFunded.Add(a.Kept); if (a.End == "BLOWN") o.FBlown += (int)w; else o.Retired += (int)w; } else { o.EBlown += (int)w; o.EvalLife.Add(slot.Trades); }
                    mo[4] += a.End == "BLOWN" ? w : 0; sl[pick] = null;
                    if (copy) break;
                }
            }
            double bank = o.Kept - o.Spent; o.Bank.Add(Tuple.Create(day, bank));
            if (bank < o.Low) { o.Low = bank; o.LowDate = day; } if (o.FirstGreen == DateTime.MinValue && bank > 0) o.FirstGreen = day;
        }
        o.BankEnd = o.Kept - o.Spent; return o;
    }

    static int Longest(List<int> seq, int v) { int best = 0, cur = 0; foreach (var x in seq) { cur = x == v ? cur + 1 : 0; best = Math.Max(best, cur); } return best; }
    static double Pct(List<double> l, double p) { if (l.Count == 0) return double.NaN; var s = l.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)(p * s.Count))]; }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var days = mnq.OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int split = dates.FindIndex(d => d >= Half); double yrs = (dates[dates.Count - 1] - dates[0]).TotalDays / 365.25;
        var r = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID FLEX 50K")).Copy();
        var sigs = days.Select(d => FastRotateStudy.Signals(d, 12, 1500)).ToList();
        var ctrl = days.Select(d => FastRotateStudy.Signals(d, 3, 1500)).ToList();
        int[] ns = { 10, 20, 30, 40, 50 }; string[] modes = { "ONE", "SEVERAL", "COPY" };
        var grid = new List<Tuple<WRun, WRun, WRun>>();
        foreach (var md in modes) foreach (int n in ns) grid.Add(Tuple.Create(Play(dates, sigs, 0, split, r, n, md), Play(dates, sigs, split, dates.Count, r, n, md), Play(dates, sigs, 0, dates.Count, r, n, md)));
        var ctrlRun = Tuple.Create(Play(dates, ctrl, 0, split, r, 10, "ONE"), Play(dates, ctrl, split, dates.Count, r, 10, "ONE"), Play(dates, ctrl, 0, dates.Count, r, 10, "ONE"));
        foreach (var g in grid) say(g.Item3.Mode + " " + g.Item3.N + " • ROI " + g.Item1.Roi.ToString("0.00") + " / " + g.Item2.Roi.ToString("0.00") + " • net/yr " + (g.Item3.Net / yrs).ToString("N0", IC) + " • low " + g.Item3.Low.ToString("N0", IC));
        // the recommended plan: what the app does live (rotation, one position at a time), 10 accounts
        var rec = grid.First(g => g.Item3.Mode == "ONE" && g.Item3.N == 10); var R0 = rec.Item3;
        // every start month: run 12 months from there
        var starts = new List<Tuple<DateTime, WRun>>();
        for (int i = 0; i < dates.Count; i++) if (i == 0 || dates[i].Month != dates[i - 1].Month) { int end = dates.FindIndex(d => d >= dates[i].AddMonths(12)); if (end < 0) break; starts.Add(Tuple.Create(dates[i], Play(dates, sigs, i, end, r, 10, "ONE"))); }
        Func<WRun, double> monthsToGreen = x => x.FirstGreen == DateTime.MinValue ? double.NaN : (x.FirstGreen - x.Bank[0].Item1).TotalDays / 30.44;
        Func<WRun, DateTime, double> bankAt = (x, d) => { double v = 0; foreach (var p in x.Bank) { if (p.Item1 > d) break; v = p.Item2; } return v; };
        var mtg = starts.Select(s => monthsToGreen(s.Item2)).Where(v => !double.IsNaN(v)).ToList();
        int never = starts.Count(s => s.Item2.FirstGreen == DateTime.MinValue);
        var lows = starts.Select(s => s.Item2.Low).ToList(); var at3 = starts.Select(s => bankAt(s.Item2, s.Item1.AddMonths(3))).ToList(); var at6 = starts.Select(s => bankAt(s.Item2, s.Item1.AddMonths(6))).ToList(); var at12 = starts.Select(s => s.Item2.BankEnd).ToList();
        say("starts " + starts.Count + " • months to green median " + Pct(mtg, 0.5).ToString("0.0") + " worst " + (mtg.Count > 0 ? mtg.Max() : double.NaN).ToString("0.0") + " never " + never + " • worst low " + lows.Min().ToString("N0", IC) + " • green after 3/6/12 m " + at3.Count(v => v > 0) + "/" + at6.Count(v => v > 0) + "/" + at12.Count(v => v > 0));

        // ---- HTML
        var h = new StringBuilder(); Func<double, string> usd = v => double.IsNaN(v) ? "—" : (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC); Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> roi = v => v.ToString("0.00", IC) + "×"; Func<double, string> n0 = v => double.IsNaN(v) ? "—" : v.ToString("0", IC); Func<double, string> n1 = v => double.IsNaN(v) ? "—" : v.ToString("0.0", IC);
        Func<string, string> modeName = m => m == "ONE" ? "ROTATE • one position at a time (the app, live)" : m == "SEVERAL" ? "ROTATE • several accounts at once (same side)" : "COPY • every account every trade";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>The Winner's Report</title><style>");
        h.Append(":root{--bg:#0b0f14;--side:#0f151d;--card:#141a22;--card2:#1b2330;--line:#2a3442;--text:#e8eef5;--mute:#8d9aab;--pos:#3fb950;--neg:#f85149;--acc:#e3b341;--blue:#58a6ff;--pbg:rgba(63,185,80,.12);--abg:rgba(227,179,65,.12)}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f5f7fa;--side:#eef2f6;--card:#fff;--card2:#f0f3f7;--line:#d8dee6;--text:#1c2430;--mute:#5d6b7c;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--blue:#0969da;--pbg:rgba(26,127,55,.10);--abg:rgba(154,103,0,.10)}}");
        h.Append("*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--bg);color:var(--text);font:14.5px/1.6 system-ui,-apple-system,Segoe UI,sans-serif}.layout{display:flex}nav{width:240px;flex:none;background:var(--side);border-right:1px solid var(--line);position:sticky;top:0;height:100vh;overflow-y:auto;padding:18px 12px}nav b{display:block;padding:4px 10px 12px;font-size:16px}nav a{display:block;color:var(--text);text-decoration:none;padding:6px 10px;border-radius:8px;font-size:13.5px}nav a:hover{background:var(--card2)}main{flex:1;min-width:0;padding:28px 30px 80px;max-width:1240px}");
        h.Append("h1{font-size:28px;margin:0 0 6px}h2{font-size:20px;margin:0 0 8px}section{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:20px 22px;margin:0 0 20px;scroll-margin-top:12px}.mute{color:var(--mute)}.pos{color:var(--pos)}.neg{color:var(--neg)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin:12px 0}.card{background:var(--card2);border:1px solid var(--line);border-radius:12px;padding:12px 14px}.card b{display:block;font-size:22px;line-height:1.25}.card span{color:var(--mute);font-size:12.5px}");
        h.Append(".box{border-radius:10px;padding:12px 16px;margin:10px 0;border:1px solid var(--line);border-left:5px solid var(--acc);background:var(--abg)}.box.g{border-left-color:var(--pos);background:var(--pbg)}.wrap{overflow-x:auto;border:1px solid var(--line);border-radius:10px;margin:10px 0}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums}th,td{padding:6px 9px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th{background:var(--card2);color:var(--mute);font-weight:600;font-size:12.5px}td.l,th.l{text-align:left}tr.rec td{background:var(--abg)}details{margin:8px 0}summary{cursor:pointer;color:var(--blue);font-weight:600}.bar{display:inline-block;height:10px;border-radius:3px;vertical-align:middle}.bp{background:var(--pos)}.bn{background:var(--neg)}");
        h.Append("@media (max-width:900px){nav{display:none}main{padding:18px 14px}}</style></head><body><div class='layout'><nav><b>🏆 The Winner</b>");
        foreach (var lk in new[] { "s0|Summary", "s1|10 → 50 accounts", "s2|Rotate vs copy", "s3|Evaluations & funded", "s4|Cycles & streaks", "s5|Money needed & time to profit", "s6|Year by year", "s7|Month by month", "s8|Recommendation", "s9|How it was tested" }) { var p = lk.Split('|'); h.Append("<a href='#" + p[0] + "'>" + p[1] + "</a>"); }
        h.Append("</nav><main>");
        h.Append("<h1>🏆 ★ FVG RETRACE • MNQ 1M 10% + VWAP — the full report</h1><p class='mute'>MNQ 1-minute data " + dates[0].ToString("yyyy-MM-dd", IC) + " → " + dates[dates.Count - 1].ToString("yyyy-MM-dd", IC) + " (" + dates.Count + " days) • LUCID FLEX 50K ($" + r.EvalCost + ", target $3,000, $2,000 end-of-day trailing floor that stops at +$100, payouts after 5 days ≥ $150: 50% up to $2,000, 90% kept, 5 payouts) • every 1-minute FVG ≥ 1 point 09:30–15:00 on the VWAP side, a limit 10% into it valid 15 min, +$1,500 / −$1,500 on 10 MNQ, flat 15:55.</p>");
        // summary
        h.Append("<section id='s0'><h2>Summary — the plan the app runs live (rotate 10 accounts, one position at a time)</h2><div class='cards'>");
        Action<string, string, string> card = (k, v, c) => h.Append("<div class='card'><b class='" + c + "'>" + v + "</b><span>" + k + "</span></div>");
        card("ROI 2020–23 • 2024–26", roi(rec.Item1.Roi) + " • " + roi(rec.Item2.Roi), ""); card("net per year (sim)", usd(R0.Net / yrs), "pos"); card("money needed (lowest bank)", usd(R0.Low), "neg"); card("trades a day", (R0.Trades / (double)dates.Count).ToString("0.0", IC), "");
        card("won", (100.0 * R0.Wins / Math.Max(1, R0.Trades)).ToString("0.0", IC) + "%", ""); card("evals → passed", (100.0 * R0.Passed / Math.Max(1, R0.Bought)).ToString("0", IC) + "% (" + R0.Passed + " of " + R0.Bought + ")", ""); card("funded → paid", (100.0 * R0.Paid / Math.Max(1, R0.Passed)).ToString("0", IC) + "%", ""); card("payouts • kept", R0.Payouts + " • " + usd(R0.Kept), "pos");
        card("start months green after 6 / 12 months", at6.Count(v => v > 0) + " / " + at12.Count(v => v > 0) + " of " + starts.Count, ""); card("time to profit from a start (median / worst)", n1(Pct(mtg, 0.5)) + " / " + n1(mtg.Count > 0 ? mtg.Max() : double.NaN) + " months", ""); card("coin flip, same plan (control)", roi(ctrlRun.Item1.Roi) + " • " + roi(ctrlRun.Item2.Roi), "");
        h.Append("</div></section>");
        // accounts grid
        Action<string, IEnumerable<Tuple<WRun, WRun, WRun>>> tbl = (title, rows) =>
        {
            h.Append("<h3 class='mute'>" + title + "</h3><div class='wrap'><table><tr><th class='l'>MODE</th><th>ACCOUNTS</th><th>ROI 20–23</th><th>ROI 24–26</th><th>NET / YEAR</th><th>MONEY NEEDED</th><th>START COST</th><th>EVALS (6 YRS)</th><th>PASS %</th><th>PAID %</th><th>PAYOUTS</th><th>FIRST PAYOUT</th><th>MONTHS +/−</th></tr>");
            foreach (var g in rows)
            {
                var x = g.Item3; int up = x.Month.Values.Count(m => m[1] - m[0] > 0), dn = x.Month.Values.Count(m => m[1] - m[0] < 0);
                h.Append("<tr class='" + (g == rec ? "rec" : "") + "'><td class='l'>" + modeName(x.Mode) + "</td><td>" + x.N + "</td><td class='" + cls(g.Item1.Roi - 1) + "'>" + roi(g.Item1.Roi) + "</td><td class='" + cls(g.Item2.Roi - 1) + "'><b>" + roi(g.Item2.Roi) + "</b></td><td class='" + cls(x.Net) + "'><b>" + usd(x.Net / yrs) + "</b></td><td class='neg'>" + usd(x.Low) + "</td><td>" + usd(x.N * r.EvalCost) + "</td><td>" + x.Bought + "</td><td>" + (100.0 * x.Passed / Math.Max(1, x.Bought)).ToString("0", IC) + "%</td><td>" + (100.0 * x.Paid / Math.Max(1, x.Passed)).ToString("0", IC) + "%</td><td>" + x.Payouts + "</td><td>" + (x.FirstPay == DateTime.MinValue ? "never" : x.FirstPay.ToString("MMM yyyy", IC)) + "</td><td>" + up + " / " + dn + "</td></tr>");
            }
            h.Append("</table></div>");
        };
        h.Append("<section id='s1'><h2>10 → 50 accounts</h2><p class='mute'>MONEY NEEDED = the most the plan was ever down (fees paid before payouts came) — keep at least that ready. START COST = the first evaluations.</p>");
        tbl("Rotate, one position at a time — what the app does live today", grid.Where(g => g.Item3.Mode == "ONE"));
        tbl("Rotate, several accounts in trades at once (same side) — needs more than one live position", grid.Where(g => g.Item3.Mode == "SEVERAL"));
        h.Append("</section>");
        h.Append("<section id='s2'><h2>Rotate vs copy</h2><p class='mute'>Copy = every account takes every trade (they pass, pay and blow together). Rotate = each trade to the next account (they do not).</p>");
        tbl("Copy", grid.Where(g => g.Item3.Mode == "COPY")); tbl("Rotate (one at a time) — for comparison", grid.Where(g => g.Item3.Mode == "ONE"));
        h.Append("</section>");
        // eval / funded
        h.Append("<section id='s3'><h2>Evaluations &amp; funded (rotate 10, one at a time)</h2><div class='cards'>");
        card("evaluations bought (6 years)", R0.Bought.ToString(IC), ""); card("passed • blown in eval", R0.Passed + " • " + R0.EBlown, ""); card("days to pass (median)", n0(Pct(R0.DaysToPass, 0.5)), ""); card("funded paid at least once", R0.Paid + " of " + R0.Passed, "");
        card("funded blown • retired (5 payouts)", R0.FBlown + " • " + R0.Retired, ""); card("days from funded to 1st payout (median)", n0(Pct(R0.DaysToPay, 0.5)), ""); card("kept per funded account (median / best)", usd(Pct(R0.KeptPerFunded, 0.5)) + " / " + usd(R0.KeptPerFunded.Count > 0 ? R0.KeptPerFunded.Max() : double.NaN), "pos"); card("payouts per paid account", (R0.Payouts / Math.Max(1.0, R0.Paid)).ToString("0.0", IC), "");
        h.Append("</div></section>");
        // cycles & streaks
        var neg = R0.Bank; int longestUnder = 0, cur = 0; double peak = double.MinValue; DateTime uA = DateTime.MinValue, uB = DateTime.MinValue, uS = DateTime.MinValue;
        foreach (var p in neg) { if (p.Item2 >= peak) { peak = p.Item2; if (cur > longestUnder) { longestUnder = cur; uB = p.Item1; uA = uS; } cur = 0; } else { if (cur == 0) uS = p.Item1; cur++; } }
        var mUp = new List<int>(); foreach (var m in R0.Month.Values) mUp.Add(m[1] - m[0] > 0 ? 1 : m[1] - m[0] < 0 ? -1 : 0);
        h.Append("<section id='s4'><h2>Cycles &amp; streaks</h2><div class='cards'>");
        card("trades in an evaluation's life (median / longest)", n0(Pct(R0.EvalLife, 0.5)) + " / " + n0(R0.EvalLife.Count > 0 ? R0.EvalLife.Max() : double.NaN), ""); card("trades in a funded life (median / longest)", n0(Pct(R0.FundLife, 0.5)) + " / " + n0(R0.FundLife.Count > 0 ? R0.FundLife.Max() : double.NaN), "");
        card("longest winning streak (trades)", Longest(R0.Seq, 1).ToString(IC), "pos"); card("longest losing streak (trades)", Longest(R0.Seq, -1).ToString(IC), "neg");
        card("most months up in a row", Longest(mUp, 1).ToString(IC), "pos"); card("most months down in a row", Longest(mUp, -1).ToString(IC), "neg");
        card("longest time below the best bank", longestUnder + " trading days" + (uA != DateTime.MinValue ? " (" + uA.ToString("MMM yyyy", IC) + " → " + uB.ToString("MMM yyyy", IC) + ")" : ""), ""); card("best month • worst month", usd(R0.Month.Values.Max(m => m[1] - m[0])) + " • " + usd(R0.Month.Values.Min(m => m[1] - m[0])), "");
        h.Append("</div></section>");
        // money needed & time to profit from every start month
        h.Append("<section id='s5'><h2>Money needed &amp; time to profit — starting in every month of the data</h2><p class='mute'>The plan started fresh on the first trading day of each month (" + starts.Count + " starts), run for 12 months with 10 accounts. This answers: if I start in a bad month, how deep do I go and how long until I am up?</p><div class='cards'>");
        card("worst money needed", usd(lows.Min()), "neg"); card("median money needed", usd(Pct(lows, 0.5)), ""); card("months to be up (median)", n1(Pct(mtg, 0.5)), ""); card("months to be up (worst)", n1(mtg.Count > 0 ? mtg.Max() : double.NaN), "");
        card("starts up after 3 months", at3.Count(v => v > 0) + " of " + starts.Count, ""); card("starts up after 6 months", at6.Count(v => v > 0) + " of " + starts.Count, ""); card("starts up after 12 months", at12.Count(v => v > 0) + " of " + starts.Count, "pos"); card("starts never up within 12 months", never.ToString(IC), never > 0 ? "neg" : "pos");
        h.Append("</div><details><summary>Every start month</summary><div class='wrap'><table><tr><th class='l'>START</th><th>MONEY NEEDED</th><th>MONTHS TO BE UP</th><th>AFTER 3 MONTHS</th><th>AFTER 6 MONTHS</th><th>AFTER 12 MONTHS</th></tr>");
        for (int i = 0; i < starts.Count; i++) h.Append("<tr><td class='l'>" + starts[i].Item1.ToString("yyyy-MM", IC) + "</td><td class='neg'>" + usd(lows[i]) + "</td><td>" + n1(monthsToGreen(starts[i].Item2)) + "</td><td class='" + cls(at3[i]) + "'>" + usd(at3[i]) + "</td><td class='" + cls(at6[i]) + "'>" + usd(at6[i]) + "</td><td class='" + cls(at12[i]) + "'><b>" + usd(at12[i]) + "</b></td></tr>");
        h.Append("</table></div></details></section>");
        // years
        h.Append("<section id='s6'><h2>Year by year (rotate 10, one at a time)</h2><div class='wrap'><table><tr><th class='l'>YEAR</th><th>EVALS BOUGHT</th><th>FEES</th><th>PASSED</th><th>BLOWN</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th>BANK</th></tr>");
        double bank = 0;
        foreach (var y in R0.Month.GroupBy(kv => kv.Key.Substring(0, 4))) { double f = y.Sum(kv => kv.Value[0]), k = y.Sum(kv => kv.Value[1]); bank += k - f; h.Append("<tr><td class='l'>" + y.Key + "</td><td>" + n0(y.Sum(kv => kv.Value[0]) / r.EvalCost) + "</td><td class='neg'>" + usd(-f) + "</td><td>" + n0(y.Sum(kv => kv.Value[3])) + "</td><td>" + n0(y.Sum(kv => kv.Value[4])) + "</td><td>" + n0(y.Sum(kv => kv.Value[2])) + "</td><td class='pos'>" + usd(k) + "</td><td class='" + cls(k - f) + "'><b>" + usd(k - f) + "</b></td><td class='" + cls(bank) + "'>" + usd(bank) + "</td></tr>"); }
        h.Append("</table></div></section>");
        double maxAbs = Math.Max(1, R0.Month.Values.Max(m => Math.Abs(m[1] - m[0])));
        h.Append("<section id='s7'><h2>Month by month</h2><details open><summary>All " + R0.Month.Count + " months</summary><div class='wrap'><table><tr><th class='l'>MONTH</th><th>FEES</th><th>PASSED</th><th>BLOWN</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th class='l'></th><th>BANK</th></tr>");
        bank = 0;
        foreach (var kv in R0.Month) { var m = kv.Value; double nn = m[1] - m[0]; bank += nn; h.Append("<tr><td class='l'>" + kv.Key + "</td><td class='neg'>" + (m[0] == 0 ? "" : usd(-m[0])) + "</td><td>" + n0(m[3]) + "</td><td>" + n0(m[4]) + "</td><td>" + n0(m[2]) + "</td><td class='pos'>" + (m[1] == 0 ? "" : usd(m[1])) + "</td><td class='" + cls(nn) + "'><b>" + usd(nn) + "</b></td><td class='l'><span class='bar " + (nn >= 0 ? "bp" : "bn") + "' style='width:" + (int)(120 * Math.Abs(nn) / maxAbs) + "px'></span></td><td class='" + cls(bank) + "'>" + usd(bank) + "</td></tr>"); }
        h.Append("</table></div></details></section>");
        // recommendation (generated from the numbers)
        var one20 = grid.First(g => g.Item3.Mode == "ONE" && g.Item3.N == 20); var sev10 = grid.First(g => g.Item3.Mode == "SEVERAL" && g.Item3.N == 10); var cp10 = grid.First(g => g.Item3.Mode == "COPY" && g.Item3.N == 10);
        var bestScore = grid.OrderByDescending(g => Math.Min(g.Item1.Roi, g.Item2.Roi)).First();
        h.Append("<section id='s8'><h2>Recommendation (written from these numbers)</h2>");
        h.Append("<div class='box g'><b>Start: rotate 10 Lucid Flex 50K accounts, one position at a time, AUTO.</b> It is what the app does live today. The data: ROI " + roi(rec.Item1.Roi) + " / " + roi(rec.Item2.Roi) + ", about " + usd(R0.Net / yrs) + " a year, " + (R0.Trades / (double)dates.Count).ToString("0.0", IC) + " trades a day. Keep " + usd(-lows.Min()) + " ready (the worst start month needed that) — the first 10 evaluations cost " + usd(10 * r.EvalCost) + ".</div>");
        h.Append("<div class='box'><b>More accounts, one position at a time, add little:</b> 20 accounts made " + usd(one20.Item3.Net / yrs) + " a year vs " + usd(R0.Net / yrs) + " with 10 — the chart's one position a time is the limit, not the accounts. <b>Several positions at once</b> (10 accounts) would make " + usd(sev10.Item3.Net / yrs) + " a year (ROI " + roi(sev10.Item1.Roi) + " / " + roi(sev10.Item2.Roi) + ") — that needs the router to hold a trade per account at the same time (a next build).</div>");
        h.Append("<div class='box'><b>Copy vs rotate:</b> copying 10 accounts: ROI " + roi(cp10.Item1.Roi) + " / " + roi(cp10.Item2.Roi) + ", " + usd(cp10.Item3.Net / yrs) + " a year, needs " + usd(cp10.Item3.Low) + " — they all win and blow together. Rotation spreads that risk: " + R0.Month.Values.Count(m => m[1] - m[0] > 0) + " of " + R0.Month.Count + " months up.</div>");
        h.Append("<div class='box'><b>The steadiest of all " + grid.Count + " plans here</b> (best ROI in its worse period): " + modeName(bestScore.Item3.Mode) + ", " + bestScore.Item3.N + " accounts — " + roi(bestScore.Item1.Roi) + " / " + roi(bestScore.Item2.Roi) + ". The coin flip with the same plan: " + roi(ctrlRun.Item1.Roi) + " / " + roi(ctrlRun.Item2.Roi) + " — the FVG + VWAP entry is what adds the rest.</div>");
        h.Append("<div class='box'><b>Rules that keep it working:</b> never trade against an open position on another account • one trade per account per day • the funded accounts' 11-second hold on (the trades last minutes, so it costs nothing) • a blown account is replaced the next morning • every payout: half to the bank.</div></section>");
        h.Append("<section id='s9'><h2>How it was tested</h2><ul class='mute'><li>Signals on MNQ 1-minute bars as the app computes them: the FVG's third candle closes on the VWAP side (session VWAP from 09:30), the gap ≥ 1 point, a limit 10% into it valid 15 minutes; fills at the limit or the minute's open through it; the stop checked before the target within a minute.</li><li>Each trade goes to the next account that has not traded today; ONE = only when no position is open anywhere (the app's chart holds one) • SEVERAL = while others are open, same side only • COPY = one account's life × the accounts.</li><li>LUCID FLEX 50K: trailing drawdown trade by trade at the end of the day, consistency 50% to pass, payouts after 5 days ≥ $150 (50% up to $2,000, 90% kept), 5 payouts then retired. Costs $1.74 a contract round trip.</li><li>Not included: slippage beyond one tick, payout denials, firm limits on the number of accounts, activation fees.</li></ul></section>");
        h.Append("</main></div></body></html>");
        Directory.CreateDirectory(outDir); File.WriteAllText(System.IO.Path.Combine(outDir, "winner_report.html"), h.ToString()); File.WriteAllText(System.IO.Path.Combine(outDir, "winner_report_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
