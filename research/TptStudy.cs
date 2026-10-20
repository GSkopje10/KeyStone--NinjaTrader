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
    static string Line(Res x)
    {
        return x.Name + "\n      IN: net/eval " + x.InNet.ToString("N0", IC) + " • pass " + x.InPass.ToString("0") + "% • buffer " + x.InBuf.ToString("0") + "% • " + x.InDaysToPass.ToString("0") + " days to pass • years " + string.Join(" ", x.YearNet.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value.ToString("0", IC)))
            + "\n      LAST 6 MONTHS (" + x.OutN + " evals): net/eval " + x.OutNet.ToString("N0", IC) + " • pass " + x.OutPass.ToString("0") + "% • buffer " + x.OutBuf.ToString("0") + "% • cash " + x.OutCash.ToString("N0", IC) + " • fees " + x.OutFees.ToString("N0", IC);
    }
}
