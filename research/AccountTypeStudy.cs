// ACCOUNT TYPES — the same strategy (★ FVG RETRACE, exactly as the app trades it: 09:30–15:00, the 09:30 candle's FVG included,
// a setup during a trade waits for its exit, each trade to the next account) on four kinds of prop account, with each firm's
// trailing drawdown done trade by trade: END OF DAY (the floor follows the best closing balance) or INTRADAY (it follows the best
// open profit too — a trade that was +$600 before stopping out has already lifted the floor), locking at the firm's level, payout
// buffers, daily payouts. 50 accounts a batch: evaluations until every one passed or blew, the funded stage, then the next batch.
// 50K accounts trade 10 MNQ (+$1,500 / −$1,500); 25K accounts 5 MNQ (+$750 / −$750) so one loss never breaks a $1,250–1,500 drawdown.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class AccountTypeStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);

    sealed class Acc { public int Id; public bool Funded; public double Bal, PeakE, PeakI, Best, Kept; public int Days, Qual, Payouts; public string End = ""; public DateTime Start, FundedOn; public bool Paid; }
    sealed class Batch { public int No, Bought, Passed, Paid, Payouts, FBlown; public DateTime From, EvalsDone, To; public double Fees, Kept; public bool Open = true; public double Net { get { return Kept - Fees; } } }
    sealed class Out
    {
        public string Name, Note; public double Spent, Kept, Low; public int Bought, Passed, FundedPaid, Payouts, FBlownBeforePay, FBlown, Batches; public DateTime FirstPay = DateTime.MinValue, LowDay;
        public double PassDays, PayDays; public int PassN, PayN; public List<Batch> B = new List<Batch>(); public SortedDictionary<string, double[]> Month = new SortedDictionary<string, double[]>();   // fees, kept, payouts, passed
        public double Net { get { return Kept - Spent; } } public double Roi { get { return Spent == 0 ? 0 : Kept / Spent; } }
        public int MonthsUp, MonthsDown;
    }

    // one trade on one account: the firm's floor (EOD or INTRADAY trailing, lock), target / consistency, payouts
    static void Step(Acc a, double pnl, double mfe, KeystoneSimRules r, DateTime day, Out o, Batch b)
    {
        bool f = a.Funded; string type = f ? r.FundDdType : r.EvalDdType; double dd = f ? r.FundDrawdown : r.EvalDrawdown, lk = f ? r.FundLockAt : r.EvalLockAt;
        if (type == "INTRADAY") a.PeakI = Math.Max(a.PeakI, a.Bal + Math.Max(0, mfe));
        double peak = type == "INTRADAY" ? a.PeakI : a.PeakE, floor = type == "STATIC" ? -dd : Math.Min(peak - dd, lk);
        a.Bal += pnl; a.Days++;
        if (a.Bal <= floor + 1e-9) { a.End = "BLOWN"; if (f) { b.FBlown++; o.FBlown++; if (!a.Paid) o.FBlownBeforePay++; } return; }
        a.PeakE = Math.Max(a.PeakE, a.Bal); a.PeakI = Math.Max(a.PeakI, a.Bal); a.Best = Math.Max(a.Best, pnl);
        if (!f)
        {
            if (a.Bal >= r.Target && a.Days >= Math.Max(1, r.EvalMinDays) && (r.EvalConsistency <= 0 || a.Best <= r.EvalConsistency / 100.0 * a.Bal + 1e-9)) a.End = "PASSED";
            return;
        }
        if ((pnl >= r.QualifyingDay - 1e-9 && pnl > 0) || r.QualifyingDay <= 0) a.Qual++;
        double above = a.Bal - r.PayoutBuffer;
        if (a.Qual >= Math.Max(1, r.PayoutDays) && above > 0)
        {
            double g = above * r.PayoutPercent / 100.0; if (r.PayoutCap > 0) g = Math.Min(g, r.PayoutCap); g = Math.Floor(g);
            if (g >= Math.Max(1, r.PayoutMin))
            {
                double cash = g * r.Split / 100.0; a.Bal -= g; a.Kept += cash; a.Payouts++; a.Qual = 0; o.Kept += cash; o.Payouts++; b.Kept += cash; b.Payouts++;
                if (!a.Paid) { a.Paid = true; o.FundedPaid++; b.Paid++; o.PayDays += (day - a.FundedOn).TotalDays; o.PayN++; }
                if (o.FirstPay == DateTime.MinValue) o.FirstPay = day; string mk = day.ToString("yyyy-MM", IC); o.Month[mk][1] += cash; o.Month[mk][2]++;
                if (r.MaxPayouts > 0 && a.Payouts >= r.MaxPayouts) a.End = "RETIRED";
            }
        }
    }

    static Out Play(List<Tuple<DateTime, List<FreqStudy.Tr>>> days, KeystoneSimRules r, double scale, int n, string name, string note, double fScale = -1)
    {
        var o = new Out { Name = name, Note = note }; List<Acc> evals = null, funded = null; Batch cur = null; int last = -1; double net = 0;
        foreach (var dd in days)
        {
            string mk = dd.Item1.ToString("yyyy-MM", IC); if (!o.Month.ContainsKey(mk)) o.Month[mk] = new double[4];
            if (evals == null && funded == null)
            {
                evals = Enumerable.Range(0, n).Select(k => new Acc { Id = k, Start = dd.Item1 }).ToList(); last = -1;
                cur = new Batch { No = o.B.Count + 1, From = dd.Item1, Bought = n, Fees = n * r.EvalCost }; o.B.Add(cur); o.Batches++; o.Bought += n; o.Spent += n * r.EvalCost; o.Month[mk][0] += n * r.EvalCost;
            }
            var pool = evals ?? funded; var used = new HashSet<Acc>();
            foreach (var t in dd.Item2)
            {
                var live = pool.Where(a => a.End == "" && !used.Contains(a)).OrderBy(a => a.Id).ToList(); if (live.Count == 0) break;
                var a0 = live.FirstOrDefault(a => a.Id > last) ?? live[0]; last = a0.Id; used.Add(a0);
                double sc = a0.Funded && fScale > 0 ? fScale : scale; Step(a0, t.Pnl * sc, t.Mfe * sc, r, dd.Item1, o, cur);
                if (a0.End == "PASSED") { cur.Passed++; o.Passed++; o.PassDays += (dd.Item1 - a0.Start).TotalDays; o.PassN++; o.Month[mk][3]++; double act = r.Activation; o.Spent += act; cur.Fees += act; o.Month[mk][0] += act; }
            }
            if (evals != null && evals.All(a => a.End != ""))
            {
                funded = evals.Where(a => a.End == "PASSED").Select(a => new Acc { Id = a.Id, Funded = true, FundedOn = dd.Item1 }).ToList(); if (funded.Count == 0) funded = null; cur.EvalsDone = dd.Item1; evals = null; last = -1;
                if (funded == null) { cur.To = dd.Item1; cur.Open = false; }
            }
            else if (funded != null && funded.All(a => a.End != "")) { funded = null; cur.To = dd.Item1; cur.Open = false; }
            net = o.Kept - o.Spent; if (net < o.Low) { o.Low = net; o.LowDay = dd.Item1; }
        }
        if (cur != null && cur.Open) cur.To = days[days.Count - 1].Item1;
        foreach (var m in o.Month.Values) { double v = m[1] - m[0]; if (v > 0) o.MonthsUp++; else if (v < 0) o.MonthsDown++; }
        return o;
    }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var days = mnq.OrderBy(d => d.Day).Select(d => Tuple.Create(d.Day.Date, FreqStudy.AppDay(d, 1, 1, 6, 0.10))).ToList();
        var A = days.Where(x => x.Item1 < Half).ToList(); var B = days.Where(x => x.Item1 >= Half).ToList();
        var pre = KeystoneSimRules.Presets(); Func<string, KeystoneSimRules> R0 = nm => pre.First(x => x.Name.StartsWith(nm)).Copy();
        var types = new List<Tuple<string, KeystoneSimRules, double, string>>
        {
            Tuple.Create("LUCID FLEX 50K", R0("LUCID FLEX 50K"), 1.0, "$120 • target $3,000 • $2,000 trailing at the END OF THE DAY, stops at +$100 • payout after 5 days ≥ $150: 50% up to $2,000, 90% kept, 5 payouts • 10 MNQ ±$1,500"),
            Tuple.Create("25K (your numbers)", R0("25K"), 0.5, "$90 • target $1,500, best day ≤ 50% ($750) • $1,250 trailing at the end of the day (ASSUMED — check), stops at +$100 • payout after 5 days ≥ $100: 50% up to $1,500, 90% kept, 5 payouts • 5 MNQ ±$750"),
            Tuple.Create("TAKE PROFIT TRADER 50K", R0("TAKE PROFIT TRADER 50K"), 1.0, "$170 + $130 activation • target $3,000, 5 days minimum, best day ≤ 50% • funded: $2,000 trailing INTRADAY (follows open profit) until it reaches the start balance • daily payouts of everything above the $2,000 buffer (min $250), 80% kept • 10 MNQ ±$1,500 • NO AUTOMATION allowed: you click"),
            Tuple.Create("TAKE PROFIT TRADER 25K", R0("TAKE PROFIT TRADER 25K"), 0.5, "$150 + $130 activation (check) • target $1,500, 5 days • funded: $1,500 trailing INTRADAY until the start balance • daily payouts above the $1,500 buffer, 80% kept • 5 MNQ ±$750 • NO AUTOMATION allowed"),
        };
        var res = types.Select(t => new { T = t, A = Play(A, t.Item2, t.Item3, 50, t.Item1, t.Item4), B = Play(B, t.Item2, t.Item3, 50, t.Item1, t.Item4), All = Play(days, t.Item2, t.Item3, 50, t.Item1, t.Item4) }).ToList();
        foreach (var fs in new[] { 0.5, 0.3 }) foreach (var t in types.Take(2)) { double f2 = t.Item3 * fs / 0.5 * 0.5; var a2 = Play(A, t.Item2, t.Item3, 50, t.Item1, "", t.Item3 * fs); var b2 = Play(B, t.Item2, t.Item3, 50, t.Item1, "", t.Item3 * fs); var c2 = Play(days, t.Item2, t.Item3, 50, t.Item1, "", t.Item3 * fs); say("  FUNDED AT " + (fs * 100).ToString("0") + "% SIZE • " + t.Item1 + " • ROI A " + a2.Roi.ToString("0.00") + " B " + b2.Roi.ToString("0.00") + " • funded paid " + c2.FundedPaid + " of " + c2.Passed + " • payouts " + c2.Payouts + " • net " + c2.Net.ToString("N0", IC) + " • low " + c2.Low.ToString("N0", IC)); }
        foreach (var x in res) say(x.T.Item1 + " • ROI A " + x.A.Roi.ToString("0.00") + " B " + x.B.Roi.ToString("0.00") + " • batches " + x.All.Batches + " • evals " + x.All.Bought + " passed " + x.All.Passed + " (" + (100.0 * x.All.Passed / x.All.Bought).ToString("0") + "%) • funded paid " + x.All.FundedPaid + " of " + x.All.Passed + " • blown before 1st payout " + x.All.FBlownBeforePay + " • payouts " + x.All.Payouts + " kept " + x.All.Kept.ToString("N0", IC) + " spent " + x.All.Spent.ToString("N0", IC) + " net " + x.All.Net.ToString("N0", IC) + " low " + x.All.Low.ToString("N0", IC));

        var h = new StringBuilder(); Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC); Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> pct = v => v.ToString("0", IC) + "%";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Account Types Compared</title><style>");
        h.Append(":root{--bg:#0b0f14;--card:#141a22;--card2:#1b2330;--line:#2a3442;--text:#e8eef5;--mute:#8d9aab;--pos:#3fb950;--neg:#f85149;--acc:#e3b341;--blue:#58a6ff}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f5f7fa;--card:#fff;--card2:#f0f3f7;--line:#d8dee6;--text:#1c2430;--mute:#5d6b7c;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--blue:#0969da}}");
        h.Append("*{box-sizing:border-box}body{background:var(--bg);color:var(--text);font:14px/1.55 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:28px 16px}main{max-width:1240px;margin:0 auto}h1{font-size:27px;margin:0 0 6px}h2{font-size:19px;margin:38px 0 10px}h3{font-size:15px;margin:20px 0 8px;color:var(--acc)}.mute{color:var(--mute)}.pos{color:var(--pos)}.neg{color:var(--neg)}");
        h.Append(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:14px}.acct{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px}.acct h3{margin:0 0 4px;color:var(--text);font-size:17px}.acct .big{font-size:26px;font-weight:700}.kv{display:flex;justify-content:space-between;border-bottom:1px dashed var(--line);padding:3px 0}.kv:last-child{border:0}");
        h.Append(".box{background:var(--card);border:1px solid var(--line);border-left:4px solid var(--acc);border-radius:10px;padding:14px 18px;margin:14px 0}.wrap{overflow-x:auto;border-radius:10px;border:1px solid var(--line)}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums;background:var(--card)}th,td{padding:6px 9px;text-align:right;white-space:nowrap;border-bottom:1px solid var(--line)}th{color:var(--mute);font-weight:600;background:var(--card2)}td:first-child,th:first-child{text-align:left}details{margin:10px 0}summary{cursor:pointer;color:var(--blue);font-weight:600}</style></head><body><main>");
        h.Append("<h1>Which prop account? Same strategy, four account types</h1><p class='mute'>★ FVG RETRACE as the app trades it, MNQ " + days[0].Item1.ToString("yyyy-MM-dd", IC) + " → " + days[days.Count - 1].Item1.ToString("yyyy-MM-dd", IC) + " • 50 accounts a batch: the evaluations (each trade to the next account), then the funded ones, then the next batch • trailing drawdown done trade by trade (INTRADAY: the floor also follows each trade's best open profit) • ROI = what you keep ÷ evaluation fees + activations. Historical simulation; firm numbers marked “check” are assumptions.</p>");
        h.Append("<div class='grid'>");
        foreach (var x in res)
        {
            var o = x.All;
            h.Append("<div class='acct'><h3>" + x.T.Item1 + "</h3><div class='mute' style='font-size:12.5px;margin-bottom:8px'>" + x.T.Item4 + "</div><div class='big " + cls(o.Net) + "'>" + usd(o.Net) + "</div><div class='mute' style='margin-bottom:8px'>net over 6 years, 50 accounts a batch</div>");
            Action<string, string> kv = (k, v) => h.Append("<div class='kv'><span class='mute'>" + k + "</span><b>" + v + "</b></div>");
            kv("ROI 2020–23 • 2024–26", x.A.Roi.ToString("0.00", IC) + "× • " + x.B.Roi.ToString("0.00", IC) + "×");
            kv("eval → funded (pass rate)", pct(100.0 * o.Passed / Math.Max(1, o.Bought)) + " (" + o.Passed + " of " + o.Bought + ")");
            kv("funded → paid at least once", pct(100.0 * o.FundedPaid / Math.Max(1, o.Passed)) + " (" + o.FundedPaid + " of " + o.Passed + ")");
            kv("funded blown before any payout", o.FBlownBeforePay.ToString(IC));
            kv("payouts • kept", o.Payouts + " • " + usd(o.Kept)); kv("fees + activations", usd(-o.Spent));
            kv("kept per funded account", usd(o.Passed == 0 ? 0 : o.Kept / o.Passed)); kv("days to pass • to 1st payout", (o.PassN == 0 ? "—" : (o.PassDays / o.PassN).ToString("0", IC)) + " • " + (o.PayN == 0 ? "—" : (o.PayDays / o.PayN).ToString("0", IC)));
            kv("lowest bank (money needed)", usd(o.Low)); kv("first payout", o.FirstPay == DateTime.MinValue ? "never" : o.FirstPay.ToString("MMM yyyy", IC)); kv("months up / down", o.MonthsUp + " / " + o.MonthsDown);
            h.Append("</div>");
        }
        h.Append("</div>");
        h.Append("<div class='box'><b>How to read the trailing drawdown here.</b> END OF DAY (Lucid, the 25K): only the closing balance moves the floor, so a trade that was +$1,000 open and closed at +$200 lifts the floor by $200. INTRADAY (Take Profit Trader funded): the best open profit moves it, so the same trade lifts it by $1,000 — a winner that gives back can leave you closer to the floor than the balance shows. Both stop trailing at the lock level (the start balance or +$100); payouts only come from above the buffer.</div>");
        foreach (var x in res)
        {
            var o = x.All;
            h.Append("<h2>" + x.T.Item1 + " — batch by batch</h2><div class='wrap'><table><tr><th>BATCH</th><th>BOUGHT</th><th>EVALS DONE</th><th>ENDED</th><th>PASSED / 50</th><th>FUNDED PAID</th><th>FUNDED BLOWN</th><th>PAYOUTS</th><th>FEES</th><th>KEPT</th><th>NET</th></tr>");
            foreach (var b in o.B) h.Append("<tr><td>#" + b.No + (b.Open ? " (open)" : "") + "</td><td>" + b.From.ToString("yyyy-MM-dd", IC) + "</td><td>" + (b.EvalsDone == default(DateTime) ? "—" : b.EvalsDone.ToString("yyyy-MM-dd", IC)) + "</td><td>" + b.To.ToString("yyyy-MM-dd", IC) + "</td><td>" + b.Passed + "</td><td>" + b.Paid + "</td><td>" + b.FBlown + "</td><td>" + b.Payouts + "</td><td class='neg'>" + usd(-b.Fees) + "</td><td class='pos'>" + usd(b.Kept) + "</td><td class='" + cls(b.Net) + "'><b>" + usd(b.Net) + "</b></td></tr>");
            h.Append("</table></div><details><summary>Year by year</summary><div class='wrap'><table><tr><th>YEAR</th><th>FEES</th><th>PASSED</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th>BANK</th></tr>"); double bank = 0;
            foreach (var y in o.Month.GroupBy(kv => kv.Key.Substring(0, 4))) { double f = y.Sum(kv => kv.Value[0]), k = y.Sum(kv => kv.Value[1]); bank += k - f; h.Append("<tr><td>" + y.Key + "</td><td class='neg'>" + usd(-f) + "</td><td>" + y.Sum(kv => kv.Value[3]).ToString("0", IC) + "</td><td>" + y.Sum(kv => kv.Value[2]).ToString("0", IC) + "</td><td class='pos'>" + usd(k) + "</td><td class='" + cls(k - f) + "'>" + usd(k - f) + "</td><td class='" + cls(bank) + "'>" + usd(bank) + "</td></tr>"); }
            h.Append("</table></div></details>");
        }
        h.Append("</main></body></html>");
        Directory.CreateDirectory(outDir); File.WriteAllText(System.IO.Path.Combine(outDir, "account_types.html"), h.ToString()); File.WriteAllText(System.IO.Path.Combine(outDir, "account_types_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
