// TAKE PROFIT TRADER 50K — which strategy beats the $2,000 trailing drawdown on ONE account, all the way to money in the bank.
// Every strategy family of the fast-rotation search (FastRotateStudy.Signals, 1-minute bars, 10 micros) × stop $ × target R ×
// session window × day plan (max trades, stop for the day at +$ / −$), traded one position at a time:
//   EVALUATION ($170 a month): +$3,000, END-OF-DAY trailing $2,000 (locks at +$100), at least 5 trading days, best day ≤ 50%
//   PRO ($130 activation): INTRADAY trailing $2,000 (the best open profit counts) until it reaches the start balance, then fixed;
//   from +$2,000 (the buffer) everything above is withdrawn at the close (≥ $250), 80% to you; followed up to 120 trading days.
// Chosen on evaluations started 2020-07 → 2025-09 (in-sample), judged on the evaluations started in the LAST 6 MONTHS (unseen).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class TptStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly string[] WinName = { "OPEN 09:30–10:30", "MORNING 09:30–11:30", "MIDDAY 11:30–13:30", "AFTERNOON 13:30–15:00", "NY DAY 09:30–15:00" };
    static readonly int[][] WinHm = { new[] { 930, 1030 }, new[] { 930, 1130 }, new[] { 1130, 1330 }, new[] { 1330, 1500 }, new[] { 930, 1500 } };
    static readonly double[] Stops = { 250, 500, 750, 1000 };
    static readonly double[] Rs = { 1, 2 };
    static readonly int[] MaxTr = { 1, 2, 3, 99 };
    static readonly double[] DayTgt = { 0, 1000, 1500 };
    static readonly bool[] DayLossOn = { false, true };   // stop for the day at −2 × the stop
    const int FundDays = 120;

    public sealed class Ev { public int Start, PassIdx = -1, BufIdx = -1, EndIdx = -1; public string End = ""; public double Fees, Cash; public int EvalDays; }
    sealed class Res
    {
        public string Sym; public int Sig, Win, Mt; public double Stop, R, Dt; public bool Dl;
        public double InNet, InPass, InBuf, OutNet, OutPass, OutBuf, OutCash, OutFees, InDaysToPass; public int OutN, InN; public Dictionary<int, double> YearNet = new Dictionary<int, double>();
        public string Name { get { return Sym + " • " + FastRotateStudy.SigName[Sig] + " • " + WinName[Win] + " • stop $" + Stop.ToString("N0", IC) + " / target " + R.ToString("0", IC) + "R ($" + (Stop * R).ToString("N0", IC) + ") • " + (Mt >= 99 ? "any number of trades" : "≤ " + Mt + " a day") + (Dt > 0 ? " • stop the day at +$" + Dt.ToString("N0", IC) : "") + (Dl ? " • stop the day at −$" + (2 * Stop).ToString("N0", IC) : ""); } }
    }

    // the day's trades for one account: one position at a time, inside the window, the day plan applied → (pnl, mfe) pairs
    static double[][] Day(List<FastRotateStudy.T1> sig, int win, int mt, double dt, double dl)
    {
        int a = R.S(WinHm[win][0] / 100, WinHm[win][0] % 100), b = R.S(WinHm[win][1] / 100, WinHm[win][1] % 100);
        var o = new List<double[]>(); int busy = -1; double p = 0;
        foreach (var t in sig)
        {
            if (t.Slot < a || t.Slot >= b || t.Slot <= busy) continue;
            if (o.Count >= mt || (dt > 0 && p >= dt) || (dl > 0 && p <= -dl)) break;
            o.Add(new[] { t.Pnl, t.Mfe }); p += t.Pnl; busy = t.Exit;
        }
        return o.ToArray();
    }

    // one evaluation bought on day s → pass? → PRO → buffer → withdrawals (up to FundDays) • days[i] = the day's trades
    public static Ev Run(double[][][] days, int s, int last)
    {
        var e = new Ev { Start = s, Fees = 170 }; double bal = 0, peak = 0, best = 0; int traded = 0, sinceFee = 0, i = s;
        for (; i <= last; i++)
        {
            if (++sinceFee > 21) { e.Fees += 170; sinceFee = 1; }   // the monthly subscription (about 21 trading days)
            var d = days[i]; if (d.Length == 0) continue; double dp = 0; foreach (var t in d) dp += t[0];
            bal += dp; traded++; best = Math.Max(best, dp);
            if (bal <= Math.Min(peak - 2000, 100) + 1e-9) { e.End = "EVAL BLOWN"; e.EndIdx = i; e.EvalDays = traded; return e; }
            peak = Math.Max(peak, bal);
            if (bal >= 3000 && traded >= 5 && best <= 0.5 * bal + 1e-9) { e.PassIdx = i; break; }
        }
        e.EvalDays = traded; if (e.PassIdx < 0) { e.End = "STILL IN EVAL"; e.EndIdx = last; return e; }
        e.Fees += 130; bal = 0; double pk = 0; int f = 0;
        for (i = e.PassIdx + 1; i <= last && f < FundDays; i++, f++)
        {
            foreach (var t in days[i])
            {
                pk = Math.Max(pk, bal + Math.Max(0, t[1])); double floor = Math.Min(pk - 2000, 0);
                bal += t[0]; if (bal <= floor + 1e-9) { e.End = e.BufIdx < 0 ? "PRO BLOWN BEFORE THE BUFFER" : "PRO BLOWN AFTER PAYOUTS"; e.EndIdx = i; return e; }
                pk = Math.Max(pk, bal);
            }
            if (bal >= 2000 && e.BufIdx < 0) e.BufIdx = i;
            if (bal - 2000 >= 250) { e.Cash += 0.8 * (bal - 2000); bal = 2000; }
        }
        e.End = e.BufIdx >= 0 ? "PRO PAYING" : "PRO RUNNING"; e.EndIdx = Math.Min(i, last); return e;
    }

    public static void Run(string data, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var res = new List<Res>();
        foreach (string sym in new[] { "MNQ", "MGC" })
        {
            FreqStudy.Sym = sym; FreqStudy.PV = sym == "MGC" ? 10 : 2;
            var days = R.Load(data, sym).OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int last = dates.Count - 1;
            DateTime oosFrom = dates[last].AddMonths(-6), inTo = oosFrom.AddMonths(-7);
            var inStarts = Enumerable.Range(0, dates.Count).Where(i => dates[i] >= new DateTime(2020, 7, 1) && dates[i] < inTo && i % 3 == 0).ToList();
            var outStarts = Enumerable.Range(0, dates.Count).Where(i => dates[i] >= oosFrom).ToList(); int inLast = outStarts[0] - 1;   // in-sample evaluations never see the last 6 months
            say(sym + ": " + dates.Count + " days " + dates[0].ToString("yyyy-MM-dd") + " → " + dates[last].ToString("yyyy-MM-dd") + " • in-sample starts " + inStarts.Count + " • last 6 months starts " + outStarts.Count + " (from " + oosFrom.ToString("yyyy-MM-dd") + ")");
            for (int sg = 0; sg < FastRotateStudy.SigName.Length; sg++) foreach (double stop in Stops) foreach (double rr in Rs)
            {
                FastRotateStudy.TgtR = rr; var sigs = days.Select(d => FastRotateStudy.Signals(d, sg, stop)).ToList(); FastRotateStudy.TgtR = 1;
                for (int w = 0; w < WinName.Length; w++) foreach (int mt in MaxTr) foreach (double dt in DayTgt) foreach (bool dl in DayLossOn)
                {
                    if (mt == 1 && (dt > 0 || dl)) continue;   // one trade a day: the day plan changes nothing
                    var dd = sigs.Select(l => Day(l, w, mt, dt, dl ? 2 * stop : 0)).ToArray();
                    var x = new Res { Sym = sym, Sig = sg, Win = w, Mt = mt, Stop = stop, R = rr, Dt = dt, Dl = dl };
                    var ins = inStarts.Select(s => Run(dd, s, Math.Min(inLast, s + 200))).ToList(); var outs = outStarts.Select(s => Run(dd, s, last)).ToList();
                    x.InN = ins.Count; x.InNet = ins.Average(v => v.Cash - v.Fees); x.InPass = 100.0 * ins.Count(v => v.PassIdx >= 0) / ins.Count; x.InBuf = 100.0 * ins.Count(v => v.BufIdx >= 0) / ins.Count;
                    x.InDaysToPass = ins.Where(v => v.PassIdx >= 0).Select(v => (double)(v.PassIdx - v.Start + 1)).DefaultIfEmpty(double.NaN).Average();
                    x.OutN = outs.Count; x.OutNet = outs.Average(v => v.Cash - v.Fees); x.OutPass = 100.0 * outs.Count(v => v.PassIdx >= 0) / outs.Count; x.OutBuf = 100.0 * outs.Count(v => v.BufIdx >= 0) / outs.Count; x.OutCash = outs.Average(v => v.Cash); x.OutFees = outs.Average(v => v.Fees);
                    foreach (var g in ins.GroupBy(v => dates[v.Start].Year)) x.YearNet[g.Key] = g.Average(v => v.Cash - v.Fees);
                    res.Add(x);
                }
            }
            say(sym + " done • " + res.Count + " plans so far • " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        }
        // the choice: best in-sample net per evaluation with at least 30% reaching the buffer and every in-sample year positive
        var ranked = res.OrderByDescending(x => x.InNet).ToList();
        var robust = ranked.Where(x => x.YearNet.Values.All(v => v > 0) && x.InBuf >= 20).ToList();
        say("\nTOP 25 IN-SAMPLE (net per evaluation) → the LAST 6 MONTHS");
        foreach (var x in ranked.Take(25)) say(Line(x));
        say("\nTOP 25 ROBUST (every year positive) → the LAST 6 MONTHS");
        foreach (var x in robust.Take(25)) say(Line(x));
        say("\nCONTROL (random side, the best plan for it)");
        foreach (var x in res.Where(x => x.Sig == 3).OrderByDescending(x => x.InNet).Take(3)) say(Line(x));
        // sessions and families: the best plan of each (in-sample), with its last-6-months result
        say("\nBEST PER FAMILY");
        foreach (var g in res.GroupBy(x => x.Sym + x.Sig)) say(Line(g.OrderByDescending(x => x.InNet).First()));
        say("\nBEST PER SESSION");
        foreach (var g in res.GroupBy(x => x.Sym + x.Win)) say(Line(g.OrderByDescending(x => x.InNet).First()));
        Directory.CreateDirectory(outDir);
        File.WriteAllText(System.IO.Path.Combine(outDir, "tpt_log.txt"), log.ToString());
        using (var w = new StreamWriter(System.IO.Path.Combine(outDir, "tpt_all.csv")))
        {
            w.WriteLine("sym,signal,window,stop,r,max_trades,day_target,day_loss,in_net,in_pass,in_buffer,in_days_to_pass,out_net,out_pass,out_buffer,out_cash,out_fees," + string.Join(",", Enumerable.Range(2020, 7).Select(y => "y" + y)));
            foreach (var x in ranked) w.WriteLine(string.Join(",", x.Sym, FastRotateStudy.SigName[x.Sig].Replace(",", ";"), WinName[x.Win], x.Stop, x.R, x.Mt, x.Dt, x.Dl ? 2 * x.Stop : 0, x.InNet.ToString("0", IC), x.InPass.ToString("0.0", IC), x.InBuf.ToString("0.0", IC), x.InDaysToPass.ToString("0.0", IC), x.OutNet.ToString("0", IC), x.OutPass.ToString("0.0", IC), x.OutBuf.ToString("0.0", IC), x.OutCash.ToString("0", IC), x.OutFees.ToString("0", IC), string.Join(",", Enumerable.Range(2020, 7).Select(y => x.YearNet.ContainsKey(y) ? x.YearNet[y].ToString("0", IC) : ""))));
        }
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
    // ===================== TPT ROTATION (the way you scale: each setup → the next account, never copied) =====================
    // n accounts (TAKE PROFIT TRADER 50K). Evaluation trades: stop $S / target $1,520 (≈ +$1,500 net: your two +$1,500 days); an evaluation at
    // +$3,000 with its best day ≤ 50% only "counts" days (one open-and-close trade, $0) until it has the minimum days → PRO.
    // PRO trades its own bracket; intraday trailing $2,000 that stops at the start; everything above the $2,000 buffer is
    // withdrawn (≥ $250, 80% to you). A blown account (eval or PRO) is replaced by a new evaluation the next day. The $170
    // subscription repeats every 21 trading days while an evaluation runs. No position against an open one on another account.
    // AFTER THE BUFFER (TPT PRO: the floor is locked at the start, everything above the buffer is yours, withdrawn the same day up to
    // $10,000): the account may trade AfterMult × the contracts, up to AfterTrades trades a day, until +AfterDayGoal for the day (0 = none);
    // at the close everything above the buffer is withdrawn (80%). Defaults = the plain plan (1×, one trade a day).
    public static double AfterMult = 1, AfterDayGoal = 0; public static int AfterTrades = 1;
    public static bool CheckInside = true;   // the floor checked inside each trade (its worst open $) — false = only at the exit (the old way)
    public sealed class RotOut { public AccountTypeStudy.Out O; public int Passed, BufferHit, Counted, BlownAfterBuffer; public double Cash, CashAfterBuffer; public int StillEval; public int EvalBlown { get { return O.Bought - Passed - StillEval; } } }   // evaluations: bought = passed + blown + still running
    public static RotOut Rotate(List<DateTime> dates, List<List<FastRotateStudy.T1>> evalSig, List<List<FastRotateStudy.T1>> proSig, int from, int to, KeystoneSimRules r, int n, int minDays, bool solo)
    {
        r = r.Copy(); r.EvalMinDays = minDays;   // TPT: the minimum trading days to test (3 = your plan, 5 = the preset)
        var res = new RotOut(); var o = new AccountTypeStudy.Out(); res.O = o; var b = new AccountTypeStudy.Batch();
        var acc = new AccountTypeStudy.Acc[n]; var born = new int[n]; var bufHit = new bool[n]; int last = -1;
        for (int i = from; i < to; i++)
        {
            var day = dates[i]; string mk = day.ToString("yyyy-MM", IC); if (!o.Month.ContainsKey(mk)) o.Month[mk] = new double[4];
            for (int s = 0; s < n; s++)
            {
                if (acc[s] == null) { acc[s] = new AccountTypeStudy.Acc { Id = s, Start = day }; born[s] = i; bufHit[s] = false; o.Spent += r.EvalCost; o.Bought++; o.Month[mk][0] += r.EvalCost; }
                else if (!acc[s].Funded && (i - born[s]) > 0 && (i - born[s]) % 21 == 0) { o.Spent += r.EvalCost; o.Month[mk][0] += r.EvalCost; }   // next month's subscription
            }
            var used = new bool[n]; var open = new List<FastRotateStudy.T1>(); var dayN = new int[n]; var dayP = new double[n]; var busy = new int[n]; for (int s = 0; s < n; s++) busy[s] = -1;
            Func<int, bool> free = s => acc[s] != null && acc[s].Funded && acc[s].PeakI >= r.FundDrawdown - 1e-9;   // the buffer is made: the floor is locked at the start
            // the floor right now (evaluation: end-of-day trailing • PRO: live trailing) — touched INSIDE a trade = blown at the floor
            Func<AccountTypeStudy.Acc, double> floorOf = x => x.Funded ? (r.FundDdType == "INTRADAY" ? Math.Min(x.PeakI - r.FundDrawdown, r.FundLockAt) : Math.Min(x.PeakE - r.FundDrawdown, r.FundLockAt)) : Math.Min(x.PeakE - r.EvalDrawdown, r.EvalLockAt);
            // evaluations already at the target: today only an open-and-close trade to count the day
            for (int s = 0; s < n; s++)
            {
                var a = acc[s]; if (a.Funded || a.Bal < r.Target || a.Best > r.EvalConsistency / 100.0 * a.Bal + 1e-9) continue;
                used[s] = true; res.Counted++; AccountTypeStudy.Step(a, -KeystoneMoveStudy.Cost("MNQ"), 0, r, day, o, b);   // one micro opened and closed (commission + 1 tick)
                if (a.End == "PASSED") { res.Passed++; o.Passed++; o.Month[mk][3]++; acc[s] = new AccountTypeStudy.Acc { Id = s, Funded = true, FundedOn = day }; o.Spent += r.Activation; o.Month[mk][0] += r.Activation; }
            }
            var es = evalSig[i]; var ps = proSig[i];
            for (int k = 0; k < es.Count; k++)
            {
                var t = es[k]; if (open.Any(q => q.Exit >= t.Slot && (solo || q.Dir != t.Dir))) continue;
                int pick = -1; for (int j = 1; j <= n; j++) { int s = (last + j) % n; if (!used[s] && busy[s] < t.Slot) { pick = s; break; } }
                if (pick < 0) continue;
                last = pick; var a = acc[pick]; var tt = a.Funded && k < ps.Count ? ps[k] : t; open.Add(tt);
                if (free(pick))
                {
                    // past the buffer: its own size and number of trades; blown only at the start balance
                    if (!bufHit[pick]) { bufHit[pick] = true; res.BufferHit++; }
                    double pnl = CheckInside && a.Bal + tt.Mae * AfterMult <= 1e-9 ? -a.Bal : tt.Pnl * AfterMult;   // liquidated at the start balance inside the trade
                    a.Bal += pnl; dayP[pick] += pnl; dayN[pick]++; busy[pick] = tt.Exit;
                    if (a.Bal <= 1e-9) { res.BlownAfterBuffer++; o.FBlown++; acc[pick] = null; used[pick] = true; continue; }
                    if (dayN[pick] >= AfterTrades || (AfterDayGoal > 0 && dayP[pick] >= AfterDayGoal - 1e-9)) used[pick] = true;
                    continue;
                }
                used[pick] = true;
                bool hit = CheckInside && a.Bal + tt.Mae <= floorOf(a) + 1e-9; double fl = floorOf(a), tp = hit ? fl - a.Bal : tt.Pnl, tm = hit ? 0 : tt.Mfe;   // the floor touched inside the trade
                double before = a.Kept; AccountTypeStudy.Step(a, tp, tm, r, day, o, b); res.Cash += a.Kept - before;
                if (a.Funded && a.End == "" && !bufHit[pick] && (a.PeakI >= r.FundDrawdown - 1e-9 || a.Payouts > 0)) { bufHit[pick] = true; res.BufferHit++; }
                if (a.End == "PASSED") { res.Passed++; o.Passed++; o.Month[mk][3]++; acc[pick] = new AccountTypeStudy.Acc { Id = pick, Funded = true, FundedOn = day }; o.Spent += r.Activation; o.Month[mk][0] += r.Activation; }
                else if (a.End != "") acc[pick] = null;
            }
            // the close: every account past its buffer withdraws everything above it (≥ $250, at most the daily cap), 80% to you
            for (int s = 0; s < n; s++)
            {
                if (!free(s)) continue; var a = acc[s]; double g = a.Bal - r.PayoutBuffer; if (r.PayoutCap > 0) g = Math.Min(g, r.PayoutCap); g = Math.Floor(g);
                if (g < Math.Max(1, r.PayoutMin)) continue; double cash = g * r.Split / 100.0; a.Bal -= g; a.Kept += cash; a.Payouts++; a.Paid = true; o.Kept += cash; o.Payouts++; o.Month[mk][1] += cash; o.Month[mk][2]++; res.Cash += cash; res.CashAfterBuffer += cash;
            }
            if (o.Kept - o.Spent < o.Low) { o.Low = o.Kept - o.Spent; o.LowDay = day; }
        }
        foreach (var m in o.Month.Values) { double v = m[1] - m[0]; if (v > 0) o.MonthsUp++; else if (v < 0) o.MonthsDown++; }
        res.StillEval = acc.Count(a => a != null && !a.Funded);
        return res;
    }

    public static void RunRotation(string data, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var r = KeystoneSimRules.Presets().First(x => x.Name == "TAKE PROFIT TRADER 50K").Copy();
        FreqStudy.Sym = "MNQ"; FreqStudy.PV = 2;
        var days = R.Load(data, "MNQ").OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int last = dates.Count - 1;
        int oos = dates.FindIndex(d => d >= dates[last].AddMonths(-6)); double yrsIn = (dates[oos - 1] - dates[0]).TotalDays / 365.25, moOut = (dates[last] - dates[oos]).TotalDays / 30.44;
        say("TPT 50K ROTATION • MNQ " + dates[0].ToString("yyyy-MM-dd") + " → " + dates[last].ToString("yyyy-MM-dd") + " • chosen on → " + dates[oos - 1].ToString("yyyy-MM-dd") + " • judged on the LAST 6 MONTHS " + dates[oos].ToString("yyyy-MM-dd") + " → (a fresh pool)");
        var rows = new List<string[]>();
        int[] sigsTried = { 0, 12, 11, 13, 2, 4, 10, 3 };
        double[] evalStops = { 500, 750, 1000, 1500 };   // the evaluation target is always $1,520 a trade (≈ $1,503 net of costs: two wins + the count day ≥ $3,000)
        double[][] proBr = { new[] { 500.0, 500 }, new[] { 750.0, 750 }, new[] { 1000.0, 1000 }, new[] { 500.0, 1000 }, new[] { 1000.0, 1500 }, new[] { 1500.0, 1500 } };
        foreach (int sg in sigsTried) foreach (double es in evalStops)
        {
            FastRotateStudy.TgtR = 1520 / es; var eSig = days.Select(d => FastRotateStudy.Signals(d, sg, es)).ToList();
            foreach (var pb in proBr)
            {
                FastRotateStudy.TgtR = pb[1] / pb[0]; var pSig = days.Select(d => FastRotateStudy.Signals(d, sg, pb[0])).ToList(); FastRotateStudy.TgtR = 1;
                foreach (int md in new[] { 3, 5 }) foreach (bool solo in new[] { true, false }) foreach (int n in new[] { 10, 20, 30, 50 })
                {
                    var a = Rotate(dates, eSig, pSig, 0, oos, r, n, md, solo); var bo = Rotate(dates, eSig, pSig, oos, dates.Count, r, n, md, solo);
                    var yr = new Dictionary<int, double>(); foreach (int y in Enumerable.Range(2021, 5)) { int f0 = dates.FindIndex(d => d.Year == y), t1 = dates.FindLastIndex(d => d.Year == y) + 1; if (f0 >= 0 && t1 <= oos) yr[y] = Rotate(dates, eSig, pSig, f0, t1, r, n, md, solo).O.Net; }
                    rows.Add(new[] { FastRotateStudy.SigName[sg], "$" + es + " / $1,520", "$" + pb[0] + " / $" + pb[1], md.ToString(), solo ? "one at a time" : "several", n.ToString(),
                        (a.O.Net / yrsIn / 12).ToString("0", IC), a.O.Roi.ToString("0.00", IC), (-a.O.Low).ToString("0", IC), yr.Values.Count(v => v > 0) + "/" + yr.Count, string.Join(" ", yr.Select(kv => kv.Key + ":" + kv.Value.ToString("0", IC))),
                        bo.O.Net.ToString("0", IC), (bo.O.Net / moOut).ToString("0", IC), bo.O.Roi.ToString("0.00", IC), (-bo.O.Low).ToString("0", IC), bo.O.Bought.ToString(), bo.Passed.ToString(), bo.BufferHit.ToString(), bo.O.Payouts.ToString(), bo.Cash.ToString("0", IC), bo.O.Spent.ToString("0", IC), bo.O.MonthsUp + "/" + bo.O.MonthsDown,
                        (100.0 * a.Passed / Math.Max(1, a.O.Bought)).ToString("0", IC), (100.0 * a.BufferHit / Math.Max(1, a.Passed)).ToString("0", IC) });
                }
            }
            say(FastRotateStudy.SigName[sg] + " eval stop $" + es + " done • " + rows.Count + " plans • " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        }
        Directory.CreateDirectory(outDir);
        string[] head = { "signal", "eval_trade", "pro_trade", "min_days", "positions", "accounts", "in_net_per_month", "in_roi", "in_max_budget", "in_years_up", "in_years", "out_net", "out_net_per_month", "out_roi", "out_max_budget", "out_evals", "out_passed", "out_buffer", "out_withdrawals", "out_cash", "out_spent", "out_months", "in_pass_pct", "in_buffer_pct_of_passed" };
        using (var w = new StreamWriter(System.IO.Path.Combine(outDir, "tpt_rotation.csv"))) { w.WriteLine(string.Join(",", head)); foreach (var x in rows) w.WriteLine(string.Join(",", x.Select(c => c.Replace(",", ";")))); }
        File.WriteAllText(System.IO.Path.Combine(outDir, "tpt_rotation_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }

    // ===================== TPT 25K ROTATION: target $1,500 = two +$750 days (+ a day-count trade), PRO buffer $1,500 =====================
    // 5 MNQ (the same points as 10 MNQ at 50K): evaluation trades −stop / +$760 (≈ +$750 net), PRO its own bracket. Signals: the
    // 1-minute FVG, random side and ALWAYS BUY every 5 min (each trade to the next account at once). The PRO drawdown is tested two
    // ways: INTRADAY (TPT's real rule — the best open profit lifts the floor at once) and EOD (what it would be at the close only).
    public static void RunRotation25(string data, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var r = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("TAKE PROFIT TRADER 25K")).Copy(); var rEod = r.Copy(); rEod.FundDdType = "EOD";
        FreqStudy.Sym = "MNQ"; FreqStudy.PV = 2; FreqStudy.Q = 5;
        var days = R.Load(data, "MNQ").OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int last = dates.Count - 1;
        int oos = dates.FindIndex(d => d >= dates[last].AddMonths(-6)); double yrsIn = (dates[oos - 1] - dates[0]).TotalDays / 365.25, moOut = (dates[last] - dates[oos]).TotalDays / 30.44;
        say("TPT 25K ROTATION • 5 MNQ • eval target $1,500 (two +$760 trades), PRO buffer $1,500 • " + r.Describe());
        var rows = new List<string[]>();
        int[] sigsTried = { 0, 12, 3, 14 }; double[] evalStops = { 375, 500, 750 };
        double[][] proBr = { new[] { 375.0, 375 }, new[] { 500.0, 500 }, new[] { 750.0, 750 }, new[] { 750.0, 1000 } };
        Func<RotOut, string> evalTxt = x => x.O.Bought + " bought • " + x.Passed + " passed (" + (100.0 * x.Passed / Math.Max(1, x.Passed + x.EvalBlown)).ToString("0", IC) + "% of the finished) • " + x.EvalBlown + " blown";
        Func<RotOut, string> proTxt = x => x.Passed + " PRO • " + x.BufferHit + " reached the buffer (" + (100.0 * x.BufferHit / Math.Max(1, x.Passed)).ToString("0", IC) + "%) • " + x.O.FBlownBeforePay + " blown before it (" + (100.0 * x.O.FBlownBeforePay / Math.Max(1, x.Passed)).ToString("0", IC) + "%)";
        foreach (int sg in sigsTried) foreach (double es in evalStops)
        {
            FastRotateStudy.TgtR = 760 / es; var eSig = days.Select(d => FastRotateStudy.Signals(d, sg, es)).ToList();
            foreach (var pb in proBr)
            {
                FastRotateStudy.TgtR = pb[1] / pb[0]; var pSig = days.Select(d => FastRotateStudy.Signals(d, sg, pb[0])).ToList(); FastRotateStudy.TgtR = 1;
                foreach (int md in new[] { 3, 5 }) foreach (bool solo in new[] { true, false }) foreach (int n in new[] { 10, 25, 50 })
                {
                    var a = Rotate(dates, eSig, pSig, 0, oos, r, n, md, solo); var bo = Rotate(dates, eSig, pSig, oos, dates.Count, r, n, md, solo);
                    var aE = Rotate(dates, eSig, pSig, 0, oos, rEod, n, md, solo);
                    var yr = new Dictionary<int, double>(); foreach (int y in Enumerable.Range(2021, 5)) { int f0 = dates.FindIndex(d => d.Year == y), t1 = dates.FindLastIndex(d => d.Year == y) + 1; if (f0 >= 0 && t1 <= oos) yr[y] = Rotate(dates, eSig, pSig, f0, t1, r, n, md, solo).O.Net; }
                    rows.Add(new[] { FastRotateStudy.SigName[sg], "$" + es + " / $760", "$" + pb[0] + " / $" + pb[1], md.ToString(), solo ? "one at a time" : "several", n.ToString(),
                        (a.O.Net / yrsIn / 12).ToString("0", IC), a.O.Roi.ToString("0.00", IC), (-a.O.Low).ToString("0", IC), yr.Values.Count(v => v > 0) + "/" + yr.Count, string.Join(" ", yr.Select(kv => kv.Key + ":" + kv.Value.ToString("0", IC))),
                        (100.0 * a.Passed / Math.Max(1, a.Passed + a.EvalBlown)).ToString("0", IC), (100.0 * a.BufferHit / Math.Max(1, a.Passed)).ToString("0", IC), (100.0 * a.O.FBlownBeforePay / Math.Max(1, a.Passed)).ToString("0", IC),
                        (100.0 * aE.BufferHit / Math.Max(1, aE.Passed)).ToString("0", IC), (aE.O.Net / yrsIn / 12).ToString("0", IC),
                        bo.O.Net.ToString("0", IC), (bo.O.Net / moOut).ToString("0", IC), bo.O.Roi.ToString("0.00", IC), (-bo.O.Low).ToString("0", IC), evalTxt(bo), proTxt(bo), bo.O.Payouts.ToString(), bo.Cash.ToString("0", IC), bo.O.Spent.ToString("0", IC), bo.O.MonthsUp + "/" + bo.O.MonthsDown });
                }
            }
            say(FastRotateStudy.SigName[sg] + " eval stop $" + es + " done • " + rows.Count + " plans • " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        }
        FreqStudy.Q = 10;
        Directory.CreateDirectory(outDir);
        string[] head = { "signal", "eval_trade", "pro_trade", "min_days", "positions", "accounts", "in_net_per_month", "in_roi", "in_max_budget", "in_years_up", "in_years", "in_eval_pass_pct", "in_pro_buffer_pct", "in_pro_blown_before_buffer_pct", "in_pro_buffer_pct_if_EOD", "in_net_per_month_if_EOD", "out_net", "out_net_per_month", "out_roi", "out_max_budget", "out_evals", "out_pro", "out_withdrawals", "out_cash", "out_spent", "out_months" };
        using (var w = new StreamWriter(System.IO.Path.Combine(outDir, "tpt25_rotation.csv"))) { w.WriteLine(string.Join(",", head)); foreach (var x in rows) w.WriteLine(string.Join(",", x.Select(c => c.Replace(",", ";")))); }
        File.WriteAllText(System.IO.Path.Combine(outDir, "tpt25_rotation_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }

    // ===================== TPT 50K • AFTER THE BUFFER: how hard to push the PRO accounts once their floor is locked =====================
    public static void RunAfterBuffer(string data, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var r = KeystoneSimRules.Presets().First(x => x.Name == "TAKE PROFIT TRADER 50K").Copy();
        FreqStudy.Sym = "MNQ"; FreqStudy.PV = 2; FreqStudy.Q = 10;
        var days = R.Load(data, "MNQ").OrderBy(d => d.Day).ToList(); var dates = days.Select(d => d.Day.Date).ToList(); int last = dates.Count - 1;
        int oos = dates.FindIndex(d => d >= dates[last].AddMonths(-6)); double yrsIn = (dates[oos - 1] - dates[0]).TotalDays / 365.25, moOut = (dates[last] - dates[oos]).TotalDays / 30.44;
        say("TPT 50K • AFTER THE BUFFER • the rotation plan (1-min FVG 10%, eval −1,500 / +1,520, PRO −1,500 / +1,500, 3 days, several at once) • " + r.Describe());
        FastRotateStudy.TgtR = 1520 / 1500.0; var eSig = days.Select(d => FastRotateStudy.Signals(d, 0, 1500)).ToList(); FastRotateStudy.TgtR = 1; var pSig = days.Select(d => FastRotateStudy.Signals(d, 0, 1500)).ToList();
        var rows = new List<string[]>();
        foreach (int n in new[] { 10, 30 }) foreach (double m in new[] { 1.0, 2, 3, 6 }) foreach (int kt in new[] { 1, 2, 3, 99 }) foreach (double dg in new[] { 0.0, 3000, 5000, 10000 })
        {
            if (kt == 1 && dg > 0) continue;
            AfterMult = m; AfterTrades = kt; AfterDayGoal = dg;
            var a = Rotate(dates, eSig, pSig, 0, oos, r, n, 3, false); var bo = Rotate(dates, eSig, pSig, oos, dates.Count, r, n, 3, false);
            var yr = new Dictionary<int, double>(); foreach (int y in Enumerable.Range(2021, 5)) { int f0 = dates.FindIndex(d => d.Year == y), t1 = dates.FindLastIndex(d => d.Year == y) + 1; if (f0 >= 0 && t1 <= oos) yr[y] = Rotate(dates, eSig, pSig, f0, t1, r, n, 3, false).O.Net; }
            rows.Add(new[] { n.ToString(), (m * 10).ToString("0") + " MNQ", kt >= 99 ? "any" : kt.ToString(), dg > 0 ? "+$" + dg.ToString("N0", IC) : "none",
                (a.O.Net / yrsIn / 12).ToString("0", IC), (-a.O.Low).ToString("0", IC), yr.Values.Count(v => v > 0) + "/" + yr.Count, string.Join(" ", yr.Select(kv => kv.Key + ":" + kv.Value.ToString("0", IC))),
                a.BufferHit.ToString(), a.BlownAfterBuffer.ToString(), (a.CashAfterBuffer / Math.Max(1, a.BufferHit)).ToString("0", IC),
                bo.O.Net.ToString("0", IC), (bo.O.Net / moOut).ToString("0", IC), (-bo.O.Low).ToString("0", IC), bo.Passed.ToString(), bo.BufferHit.ToString(), bo.BlownAfterBuffer.ToString(), bo.O.Payouts.ToString(), bo.Cash.ToString("0", IC), (bo.CashAfterBuffer / Math.Max(1, bo.BufferHit)).ToString("0", IC), bo.O.Spent.ToString("0", IC), bo.O.MonthsUp + "/" + bo.O.MonthsDown });
            say(string.Join(" | ", rows[rows.Count - 1]));
        }
        AfterMult = 1; AfterTrades = 1; AfterDayGoal = 0;
        Directory.CreateDirectory(outDir);
        string[] head = { "accounts", "after_buffer_size", "after_buffer_trades_a_day", "after_buffer_day_goal", "in_net_per_month", "in_max_budget", "in_years_up", "in_years", "in_buffer_made", "in_blown_after_buffer", "in_cash_per_buffer_account", "out_net", "out_net_per_month", "out_max_budget", "out_passed", "out_buffer_made", "out_blown_after_buffer", "out_withdrawals", "out_cash", "out_cash_per_buffer_account", "out_spent", "out_months" };
        using (var w = new StreamWriter(System.IO.Path.Combine(outDir, "tpt_after_buffer.csv"))) { w.WriteLine(string.Join(",", head)); foreach (var x in rows) w.WriteLine(string.Join(",", x.Select(c => c.Replace(",", ";")))); }
        File.WriteAllText(System.IO.Path.Combine(outDir, "tpt_after_buffer_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }

    static string Line(Res x)
    {
        return x.Name + "\n      IN: net/eval " + x.InNet.ToString("N0", IC) + " • pass " + x.InPass.ToString("0") + "% • buffer " + x.InBuf.ToString("0") + "% • " + x.InDaysToPass.ToString("0") + " days to pass • years " + string.Join(" ", x.YearNet.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value.ToString("0", IC)))
            + "\n      LAST 6 MONTHS (" + x.OutN + " evals): net/eval " + x.OutNet.ToString("N0", IC) + " • pass " + x.OutPass.ToString("0") + "% • buffer " + x.OutBuf.ToString("0") + "% • cash " + x.OutCash.ToString("N0", IC) + " • fees " + x.OutFees.ToString("N0", IC);
    }
}
