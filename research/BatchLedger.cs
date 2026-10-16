// BATCH LEDGER — the frequent pick (FREQUENT ENTRIES study) played as the user will: buy 50 evaluations (one batch), trade them
// until every one passed or blew, then the passed ones funded until every one blew or retired, then buy the next 50.
// Every batch: dates, cost, passed, funded paid, payouts, net. The bank: losing streaks of batches, time under water,
// money in / out per month and per year. Same fills, rules and costs as FreqStudy (LUCID FLEX 50K, 10 MNQ, $1.74 a contract).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class BatchLedger
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    public sealed class Batch
    {
        public int No; public DateTime Bought, EvalEnd, End, FirstPay, BreakEven; public int DaysToPay = -1, DaysToEven = -1; public int Days, EvalDays, Passed, Paid, Payouts; public double Cost, Kept; public bool Open;
        public double Net { get { return Kept - Cost; } }
    }
    public sealed class Flow { public DateTime Day; public double Amt; }   // − = fees, + = payouts kept

    public static List<Batch> Play(List<Tuple<DateTime, List<FreqStudy.Tr>>> days, int maxDay, KeystoneSimRules r, int n, List<Flow> flows)
    {
        var o = new List<Batch>(); Batch cur = null; List<OpenFvgStudy.Acct> evals = null, funded = null, passed = null; int last = -1; var paid = new HashSet<OpenFvgStudy.Acct>();
        foreach (var dd in days)
        {
            if (evals == null && funded == null)
            {
                evals = Enumerable.Range(0, n).Select(k => new OpenFvgStudy.Acct { Id = k }).ToList(); passed = new List<OpenFvgStudy.Acct>(); last = -1; paid.Clear();
                cur = new Batch { No = o.Count + 1, Bought = dd.Item1, Cost = n * r.EvalCost, Open = true }; o.Add(cur); flows.Add(new Flow { Day = dd.Item1, Amt = -cur.Cost });
            }
            cur.Days++; if (evals != null) cur.EvalDays++;
            var pool = evals ?? funded; var used = new HashSet<OpenFvgStudy.Acct>(); int taken = 0;
            foreach (var t in dd.Item2)
            {
                if (taken >= maxDay) break;
                var live = pool.Where(a => a.End == "" && !used.Contains(a)).OrderBy(a => a.Id).ToList(); if (live.Count == 0) break;
                var a0 = live.FirstOrDefault(a => a.Id > last) ?? live[0]; last = a0.Id; used.Add(a0); taken++;
                double k0 = a0.Kept; OpenFvgStudy.Step(a0, t.Pnl, r);
                if (a0.Kept > k0) { cur.Kept += a0.Kept - k0; cur.Payouts++; if (cur.DaysToPay < 0) { cur.DaysToPay = cur.Days; cur.FirstPay = dd.Item1; } if (cur.DaysToEven < 0 && cur.Kept >= cur.Cost) { cur.DaysToEven = cur.Days; cur.BreakEven = dd.Item1; } flows.Add(new Flow { Day = dd.Item1, Amt = a0.Kept - k0 }); if (paid.Add(a0)) cur.Paid++; }
                if (a0.End == "PASSED") { cur.Passed++; passed.Add(new OpenFvgStudy.Acct { Id = a0.Id, Funded = true }); }
            }
            if (evals != null && evals.All(a => a.End != "")) { evals = null; cur.EvalEnd = dd.Item1; funded = passed.Count > 0 ? passed : null; last = -1; if (funded == null) { cur.End = dd.Item1; cur.Open = false; } }
            else if (funded != null && funded.All(a => a.End != "")) { funded = null; cur.End = dd.Item1; cur.Open = false; }
        }
        if (cur != null && cur.Open) cur.End = days[days.Count - 1].Item1;
        return o;
    }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var days = mnq.OrderBy(d => d.Day).ToList(); var rules = KeystoneSimRules.Presets()[1].Copy();
        Func<List<FreqStudy.Tr>, List<FreqStudy.Tr>> solo = l => { var o2 = new List<FreqStudy.Tr>(); int busy = -1; foreach (var t in l) { if (t.Slot <= busy) continue; o2.Add(t); busy = t.Exit; } return o2; };
        // the pick: FVG RETRACE 10% (model 0) • NY OPEN 09:30–11:30 (window 0) • +$1,500 / −$1,500 (bracket 6)
        var runs = new List<Tuple<string, string, List<Tuple<DateTime, List<FreqStudy.Tr>>>, int>>
        {
            Tuple.Create("APP", "★ FVG RETRACE 10% • 09:30–15:00 • ±$1,500 • exactly as the app trades it (the studio default)", days.Select(d => Tuple.Create(d.Day.Date, FreqStudy.AppDay(d, 1, 0, 6, 0.10))).ToList(), 99),
            Tuple.Create("APP1130", "FVG RETRACE 10% • 09:30–11:30 • ±$1,500 • as the app trades it (the earlier default)", days.Select(d => Tuple.Create(d.Day.Date, FreqStudy.AppDay(d, 0, 0, 6, 0.10))).ToList(), 99),
                        Tuple.Create("RANDOM", "RANDOM SIDE every 15 min (control) • 09:30–15:00, ±$1,500, one at a time", days.Select(d => Tuple.Create(d.Day.Date, solo(FreqStudy.DayTrades(d, 4, 1, 6)))).ToList(), 99),
        };
        var html = new StringBuilder();
        Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC);
        Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<DateTime, string> dt = d => d.ToString("yyyy-MM-dd", IC);
        html.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Batch Ledger</title><style>");
        html.Append(":root{--bg:#0d1117;--card:#161b22;--line:#30363d;--text:#e6edf3;--mute:#8b949e;--pos:#3fb950;--neg:#f85149;--acc:#d29922}");
        html.Append("@media (prefers-color-scheme: light){:root{--bg:#f6f8fa;--card:#fff;--line:#d0d7de;--text:#1f2328;--mute:#59636e;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700}}");
        html.Append("body{background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:24px 16px}main{max-width:1280px;margin:0 auto}h1{font-size:24px;margin:0 0 4px}h2{font-size:18px;margin:34px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}h3{font-size:14px;margin:18px 0 6px;color:var(--acc)}");
        html.Append(".mute{color:var(--mute)}.pos{color:var(--pos)}.neg{color:var(--neg)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:10px;margin:10px 0}.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:10px 12px}.card b{display:block;font-size:20px}.card span{color:var(--mute);font-size:12px}");
        html.Append(".wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums;background:var(--card)}th,td{border:1px solid var(--line);padding:4px 8px;text-align:right;white-space:nowrap}th{color:var(--mute);font-weight:600}td:first-child,th:first-child{text-align:left}details{margin:8px 0}summary{cursor:pointer;color:var(--acc)}</style></head><body><main>");
        html.Append("<h1>Batch ledger • 50 evaluations at a time</h1><p class='mute'>MNQ " + dt(days[0].Day) + " → " + dt(days[days.Count - 1].Day) + " • LUCID FLEX 50K ($" + rules.EvalCost + " an eval, payouts after 5 qualifying days, 50% up to $2,000, 90% kept, retired after 5 payouts) • 10 MNQ, $1.74 a contract in costs • a batch = buy 50 evals → trade them until all passed or blew → the passed ones funded until all blew or retired → buy the next 50. Only one batch at a time; money in = payouts you keep, money out = eval fees (no activation fee on this firm).</p>");
        var csv = new StringBuilder("run,batch,bought,evals_done,ended,days,eval_days,days_to_first_payout,days_to_break_even,cost,passed,funded_paid,payouts,kept,net,bank_after\n");

        foreach (var run in runs)
        {
            var flows = new List<Flow>(); var bs = Play(run.Item3, run.Item4, rules, 50, flows);
            var done = bs.Where(b => !b.Open).ToList();
            // bank (by day, from the flows)
            double bank = 0, peak = 0, low = 0; DateTime peakDay = days[0].Day, lowDay = days[0].Day, uwStart = days[0].Day; double longestUw = 0; DateTime uwA = days[0].Day, uwB = days[0].Day; DateTime? firstPositive = null, lastNegative = null;
            var byDay = flows.GroupBy(f => f.Day).OrderBy(g => g.Key).ToList(); bool under = false;
            foreach (var g in byDay)
            {
                bank += g.Sum(f => f.Amt);
                if (bank < low) { low = bank; lowDay = g.Key; }
                if (bank > 0 && firstPositive == null) firstPositive = g.Key;
                if (bank < 0) lastNegative = g.Key;
                if (bank >= peak) { if (under) { double len = (g.Key - uwStart).TotalDays; if (len > longestUw) { longestUw = len; uwA = uwStart; uwB = g.Key; } } peak = bank; peakDay = g.Key; under = false; }
                else if (!under) { under = true; uwStart = peakDay; }
            }
            bool stillUnder = under; double openUw = under ? (days[days.Count - 1].Day - uwStart).TotalDays : 0;
            // losing streaks of batches (net < 0)
            var streaks = new List<Tuple<int, int, double>>(); int s0 = -1; double sAmt = 0;
            for (int i = 0; i < done.Count; i++)
            {
                if (done[i].Net < 0) { if (s0 < 0) { s0 = i; sAmt = 0; } sAmt += done[i].Net; }
                else if (s0 >= 0) { streaks.Add(Tuple.Create(s0, i - 1, sAmt)); s0 = -1; }
            }
            if (s0 >= 0) streaks.Add(Tuple.Create(s0, done.Count - 1, sAmt));
            var longest = streaks.OrderByDescending(s => s.Item2 - s.Item1).ThenBy(s => s.Item3).FirstOrDefault();
            var costliest = streaks.OrderBy(s => s.Item3).FirstOrDefault();
            var shortest = streaks.OrderBy(s => s.Item2 - s.Item1).ThenByDescending(s => s.Item3).FirstOrDefault();
            int wins = done.Count(b => b.Net > 0); double spent = bs.Sum(b => b.Cost), kept = bs.Sum(b => b.Kept);
            say(run.Item1 + " • batches " + bs.Count + " (" + done.Count + " finished) • won " + wins + " • spent " + usd(spent) + " kept " + usd(kept) + " net " + usd(kept - spent) + " • ROI " + (spent == 0 ? 0 : kept / spent).ToString("0.00") + "× • lowest bank " + usd(low) + " on " + dt(lowDay) + " • first in profit " + (firstPositive.HasValue ? dt(firstPositive.Value) : "never") + " • longest under water " + longestUw.ToString("0") + " days (" + dt(uwA) + " → " + dt(uwB) + ")" + (stillUnder ? " • now under water for " + openUw.ToString("0") + " days" : ""));
            if (longest != null) say("   longest losing streak " + (longest.Item2 - longest.Item1 + 1) + " batches (#" + done[longest.Item1].No + "–#" + done[longest.Item2].No + ", " + dt(done[longest.Item1].Bought) + " → " + dt(done[longest.Item2].End) + ") " + usd(longest.Item3));
            if (costliest != null) say("   costliest losing streak " + (costliest.Item2 - costliest.Item1 + 1) + " batches " + usd(costliest.Item3));

            // ---- html for this run
            html.Append("<h2>" + run.Item2 + "</h2><div class='cards'>");
            Action<string, string, string> card = (k, v, c) => html.Append("<div class='card'><b class='" + c + "'>" + v + "</b><span>" + k + "</span></div>");
            card("batches of 50 bought (" + done.Count + " finished)", bs.Count.ToString(), "");
            card("batches that made money", wins + " of " + done.Count + " (" + (done.Count == 0 ? 0 : 100.0 * wins / done.Count).ToString("0") + "%)", "");
            card("evals passed (of " + (bs.Count * 50) + ")", bs.Sum(b => b.Passed) + " (" + (100.0 * bs.Sum(b => b.Passed) / (bs.Count * 50.0)).ToString("0") + "%)", "");
            card("funded accounts paid at least once", bs.Sum(b => b.Paid) + " of " + bs.Sum(b => b.Passed), "");
            card("payouts • kept", bs.Sum(b => b.Payouts) + " • " + usd(kept), "pos");
            card("eval fees", usd(-spent), "neg");
            card("net in the bank • ROI", usd(kept - spent) + " • " + (spent == 0 ? 0 : kept / spent).ToString("0.00") + "×", cls(kept - spent));
            card("lowest the bank went", usd(low) + " • " + dt(lowDay), cls(low));
            card("first day in profit", firstPositive.HasValue ? dt(firstPositive.Value) : "never", "");
            card("longest time under water", longestUw.ToString("0") + " days • " + dt(uwA) + " → " + dt(uwB), "");
            if (stillUnder) card("now under water since", dt(uwStart) + " (" + openUw.ToString("0") + " days)", "neg");
            if (longest != null) card("longest losing streak", (longest.Item2 - longest.Item1 + 1) + " batches • " + usd(longest.Item3) + " • " + dt(done[longest.Item1].Bought) + " → " + dt(done[longest.Item2].End), "neg");
            if (costliest != null) card("costliest losing streak", (costliest.Item2 - costliest.Item1 + 1) + " batches • " + usd(costliest.Item3) + " • " + dt(done[costliest.Item1].Bought) + " → " + dt(done[costliest.Item2].End), "neg");
            if (shortest != null) card("shortest losing streak", (shortest.Item2 - shortest.Item1 + 1) + " batch • " + usd(shortest.Item3), "neg");
            card("a batch lasts (median)", done.Count == 0 ? "—" : done.Select(b => b.Days).OrderBy(x => x).ElementAt(done.Count / 2) + " trading days (eval part " + done.Select(b => b.EvalDays).OrderBy(x => x).ElementAt(done.Count / 2) + ")", "");
            var ev = done.Where(b => b.DaysToEven >= 0).ToList(); var fp = done.Where(b => b.DaysToPay >= 0).ToList();
            if (ev.Count > 0)
            {
                var fast = ev.OrderBy(b => b.DaysToEven).First(); var slow = ev.OrderByDescending(b => b.DaysToEven).First();
                card("fastest batch back to even", fast.DaysToEven + " trading days • #" + fast.No + " " + dt(fast.Bought) + " → " + dt(fast.BreakEven), "pos");
                card("slowest batch back to even", slow.DaysToEven + " trading days • #" + slow.No + " " + dt(slow.Bought) + " → " + dt(slow.BreakEven), "");
                say("   back to even: fastest " + fast.DaysToEven + " d (#" + fast.No + ") slowest " + slow.DaysToEven + " d (#" + slow.No + ") • first payout after " + fp.Min(b => b.DaysToPay) + "–" + fp.Max(b => b.DaysToPay) + " d");
            }
            if (fp.Count > 0) card("first payout after (fastest – slowest)", fp.Min(b => b.DaysToPay) + " – " + fp.Max(b => b.DaysToPay) + " trading days", "");
            html.Append("</div>");

            // losing streaks list
            html.Append("<h3>Every losing streak (batches in a row that lost money)</h3><div class='wrap'><table><tr><th>BATCHES</th><th>FROM</th><th>TO</th><th>LOST</th></tr>");
            foreach (var s in streaks) html.Append("<tr><td>#" + done[s.Item1].No + (s.Item2 > s.Item1 ? "–#" + done[s.Item2].No : "") + " (" + (s.Item2 - s.Item1 + 1) + ")</td><td>" + dt(done[s.Item1].Bought) + "</td><td>" + dt(done[s.Item2].End) + "</td><td class='neg'>" + usd(s.Item3) + "</td></tr>");
            html.Append("</table></div>");

            // yearly
            html.Append("<h3>Per year (by the day the money moved)</h3><div class='wrap'><table><tr><th>YEAR</th><th>BATCHES BOUGHT</th><th>EVAL FEES</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th>BANK AT YEAR END</th></tr>");
            double run0 = 0;
            foreach (var y in flows.GroupBy(f => f.Day.Year).OrderBy(g => g.Key))
            {
                double fee = y.Where(f => f.Amt < 0).Sum(f => f.Amt), pay = y.Where(f => f.Amt > 0).Sum(f => f.Amt); run0 += fee + pay;
                html.Append("<tr><td>" + y.Key + "</td><td>" + bs.Count(b => b.Bought.Year == y.Key) + "</td><td class='neg'>" + usd(fee) + "</td><td>" + y.Count(f => f.Amt > 0) + "</td><td class='pos'>" + usd(pay) + "</td><td class='" + cls(fee + pay) + "'>" + usd(fee + pay) + "</td><td class='" + cls(run0) + "'>" + usd(run0) + "</td></tr>");
            }
            html.Append("</table></div>");
            // monthly
            html.Append("<details><summary>Per month (" + flows.Select(f => f.Day.ToString("yyyy-MM")).Distinct().Count() + " months with money moving)</summary><div class='wrap'><table><tr><th>MONTH</th><th>BATCHES BOUGHT</th><th>EVAL FEES</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th>BANK</th></tr>");
            run0 = 0; int posM = 0, negM = 0;
            var allMonths = days.Select(d => new DateTime(d.Day.Year, d.Day.Month, 1)).Distinct().OrderBy(m => m).ToList();
            foreach (var m in allMonths)
            {
                var mf = flows.Where(f => f.Day.Year == m.Year && f.Day.Month == m.Month).ToList();
                double fee = mf.Where(f => f.Amt < 0).Sum(f => f.Amt), pay = mf.Where(f => f.Amt > 0).Sum(f => f.Amt); run0 += fee + pay; if (fee + pay > 0) posM++; else if (fee + pay < 0) negM++;
                html.Append("<tr><td>" + m.ToString("yyyy-MM", IC) + "</td><td>" + bs.Count(b => b.Bought.Year == m.Year && b.Bought.Month == m.Month) + "</td><td class='neg'>" + (fee == 0 ? "" : usd(fee)) + "</td><td>" + mf.Count(f => f.Amt > 0) + "</td><td class='pos'>" + (pay == 0 ? "" : usd(pay)) + "</td><td class='" + cls(fee + pay) + "'>" + usd(fee + pay) + "</td><td class='" + cls(run0) + "'>" + usd(run0) + "</td></tr>");
            }
            html.Append("</table></div><p class='mute'>" + posM + " months up, " + negM + " months down, " + (allMonths.Count - posM - negM) + " months with no money moving.</p></details>");
            say("   months up " + posM + " • down " + negM + " • flat " + (allMonths.Count - posM - negM));
            // every batch
            html.Append("<details" + (run.Item1 == "APP" ? " open" : "") + "><summary>Every batch (" + bs.Count + ")</summary><div class='wrap'><table><tr><th>#</th><th>BOUGHT</th><th>EVALS DONE</th><th>BATCH ENDED</th><th>DAYS</th><th>1ST PAYOUT</th><th>BACK TO EVEN</th><th>FEES</th><th>PASSED / 50</th><th>FUNDED PAID</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th>BANK AFTER</th></tr>");
            double bank2 = 0;
            foreach (var b in bs)
            {
                bank2 += b.Net;
                html.Append("<tr><td>" + b.No + (b.Open ? " (open)" : "") + "</td><td>" + dt(b.Bought) + "</td><td>" + (b.EvalEnd == default(DateTime) ? "—" : dt(b.EvalEnd)) + "</td><td>" + dt(b.End) + "</td><td>" + b.Days + "</td><td>" + (b.DaysToPay < 0 ? "—" : dt(b.FirstPay) + " (" + b.DaysToPay + " d)") + "</td><td>" + (b.DaysToEven < 0 ? "never" : dt(b.BreakEven) + " (" + b.DaysToEven + " d)") + "</td><td class='neg'>" + usd(-b.Cost) + "</td><td>" + b.Passed + "</td><td>" + b.Paid + "</td><td>" + b.Payouts + "</td><td class='pos'>" + usd(b.Kept) + "</td><td class='" + cls(b.Net) + "'>" + usd(b.Net) + "</td><td class='" + cls(bank2) + "'>" + usd(bank2) + "</td></tr>");
                csv.Append(run.Item1 + "," + b.No + "," + dt(b.Bought) + "," + (b.EvalEnd == default(DateTime) ? "" : dt(b.EvalEnd)) + "," + dt(b.End) + "," + b.Days + "," + b.EvalDays + "," + b.DaysToPay + "," + b.DaysToEven + "," + b.Cost.ToString(IC) + "," + b.Passed + "," + b.Paid + "," + b.Payouts + "," + b.Kept.ToString("0.##", IC) + "," + b.Net.ToString("0.##", IC) + "," + bank2.ToString("0.##", IC) + "\n");
            }
            html.Append("</table></div></details>");
        }
        html.Append("<p class='mute'>Historical simulation only — no orders. A blown evaluation costs its fee; payouts are what the firm's rules would have paid and you kept (90%). Real fills, slippage on fast opens and payout denials can make it worse.</p></main></body></html>");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "batch_ledger.html"), html.ToString());
        File.WriteAllText(Path.Combine(outDir, "batch_ledger.csv"), csv.ToString());
        File.WriteAllText(Path.Combine(outDir, "batch_ledger_log.txt"), log.ToString());
    }
}
