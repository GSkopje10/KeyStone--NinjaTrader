// OPENING FVG • MNQ — 6 years. The app's own engine (KeystoneOpenFvg.Run, the same code the studio and the prop sim trade):
// from 09:30 the first 5-minute FVG sets the side, FVGs in a row the same way join one zone, a limit at Pct of the zone, stop −$ /
// target +$ for 10 MNQ, no fill by 15:55 = no trade, flat at 15:55. Costs: $1.74 a contract (commission both sides + 1 tick).
// Prop math on LUCID FLEX 50K (target $3,000, $2,000 EOD trailing drawdown that stops at +$100, 50% consistency, 2 days min;
// funded: same drawdown, payout after 5 days of ≥ $150: 50% of the profit up to $2,000, you keep 90%, 5 payouts then retired).
// Four ways: EVAL + FUNDED (pass, then trade the funded account; a blown one is replaced by a new evaluation) • EVAL ONLY (how many
// pass) • FUNDED ONLY (a funded account from day one, no evaluation) • LIVE ACCOUNT (no firm rules, own money).
// "Every start day": the account is started on every day of the 6 years and run to its end — the spread of outcomes, not one lucky path.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class OpenFvgStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public sealed class T { public DateTime Day; public KeystoneOpenFvgDay D; }

    public static List<KeystoneArcBar> BarsOf(RDay x) { return Bars(x); }
    static List<KeystoneArcBar> Bars(RDay x)
    {
        var l = new List<KeystoneArcBar>(); var t0 = x.Day.Date.AddDays(-1).AddHours(18);
        for (int k = 0; k < R.Slots; k++) if (x.Has(k) && !float.IsNaN(x.O[k])) l.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(k + 1), Open = x.O[k], High = x.H[k], Low = x.L[k], Close = x.C[k], Volume = x.V[k] });
        return l;
    }

    public static List<T> Trades(List<List<KeystoneArcBar>> days, List<DateTime> dates, KeystoneOrbRule r)
    {
        var o = new List<T>();
        for (int i = 0; i < days.Count; i++) { var d = KeystoneOpenFvg.Run(days[i], r, "MNQ"); if (d != null) o.Add(new T { Day = dates[i], D = d }); }
        return o;
    }

    // ---- the prop account (one trade a day at most, the day's P/L = that trade)
    public sealed class Acct
    {
        public int Id; public bool Funded; public double Bal, Hwm, Best; public int Days, QualDays, Payouts; public double Kept;
        public string End = "";   // PASSED / BLOWN / RETIRED / "" (still open)
    }
    // returns false when the account ended that day
    public static bool Step(Acct a, double pnl, KeystoneSimRules r)
    {
        double dd = a.Funded ? r.FundDrawdown : r.EvalDrawdown, lockAt = a.Funded ? r.FundLockAt : r.EvalLockAt;
        double floor = Math.Min(a.Hwm - dd, lockAt);
        a.Bal += pnl; a.Days++;
        if (a.Bal <= floor) { a.End = "BLOWN"; return false; }
        a.Hwm = Math.Max(a.Hwm, a.Bal); a.Best = Math.Max(a.Best, pnl);
        if (!a.Funded)
        {
            if (a.Bal >= r.Target && a.Days >= r.EvalMinDays && (r.EvalConsistency <= 0 || a.Best <= r.EvalConsistency / 100.0 * a.Bal)) { a.End = "PASSED"; return false; }
            return true;
        }
        if (pnl >= r.QualifyingDay) a.QualDays++;
        if (a.QualDays >= r.PayoutDays && a.Bal > 0)
        {
            double g = Math.Min(r.PayoutCap > 0 ? r.PayoutCap : double.MaxValue, a.Bal * r.PayoutPercent / 100.0);
            if (g >= Math.Max(1, r.PayoutMin)) { a.Bal -= g; a.Kept += g * r.Split / 100.0; a.Payouts++; a.QualDays = 0; if (r.MaxPayouts > 0 && a.Payouts >= r.MaxPayouts) { a.End = "RETIRED"; return false; } }
        }
        return true;
    }

    // day P/L per trading day (0 when no trade), in date order
    static double[] DayPnl(List<T> ts) { return ts.Select(t => t.D.Filled ? t.D.Pnl : 0).ToArray(); }

    public sealed class Single { public int Starts, Passed, Blown, Open; public double AvgDaysPass, AvgDaysBlow; public int FBlown, FRetired, FOpen; public double AvgKept, AvgPayouts, AvgFundDays; }
    // EVAL ONLY / FUNDED ONLY from every start day
    public static Single EveryStart(double[] p, KeystoneSimRules r, bool funded)
    {
        var s = new Single(); var dp = new List<int>(); var db = new List<int>(); var kept = new List<double>(); var pay = new List<int>(); var fd = new List<int>();
        for (int st = 0; st < p.Length; st++)
        {
            var a = new Acct { Funded = funded }; int i = st;
            for (; i < p.Length; i++) { if (p[i] == 0) continue; if (!Step(a, p[i], r)) break; }
            s.Starts++;
            if (!funded) { if (a.End == "PASSED") { s.Passed++; dp.Add(a.Days); } else if (a.End == "BLOWN") { s.Blown++; db.Add(a.Days); } else s.Open++; }
            else
            {
                if (a.End == "") { s.FOpen++; continue; }   // still running at the end of the data: left out of the averages
                if (a.End == "BLOWN") s.FBlown++; else s.FRetired++; kept.Add(a.Kept); pay.Add(a.Payouts); fd.Add(a.Days);
            }
        }
        s.AvgDaysPass = dp.Count == 0 ? 0 : dp.Average(); s.AvgDaysBlow = db.Count == 0 ? 0 : db.Average();
        s.AvgKept = kept.Count == 0 ? 0 : kept.Average(); s.AvgPayouts = pay.Count == 0 ? 0 : pay.Average(); s.AvgFundDays = fd.Count == 0 ? 0 : fd.Average();
        return s;
    }

    public sealed class Chain { public int Evals, Passed, Blown, FBlown, FRetired, Payouts; public double Spent, Kept; public List<double[]> Curve = new List<double[]>(); public Dictionary<int, double[]> Year = new Dictionary<int, double[]>(); }
    // one lane, 6 years in a row: evaluation → funded → (blown / retired) → a new evaluation … (funded only: a new funded account)
    public static Chain RunChain(List<T> ts, KeystoneSimRules r, bool evalOnly, bool fundedOnly, double fundCost = 0)
    {
        var c = new Chain(); Acct a = null; double net = 0;
        foreach (var t in ts)
        {
            int y = t.Day.Year; if (!c.Year.ContainsKey(y)) c.Year[y] = new double[6];   // spent, kept, evals, passed, payouts, blown(all)
            if (a == null)
            {
                a = new Acct { Funded = fundedOnly };
                if (!fundedOnly) { c.Evals++; c.Spent += r.EvalCost; net -= r.EvalCost; c.Year[y][0] += r.EvalCost; c.Year[y][2]++; }
                else { c.Evals++; c.Spent += fundCost; net -= fundCost; c.Year[y][0] += fundCost; }
            }
            if (!t.D.Filled) { c.Curve.Add(new[] { t.Day.ToOADate(), net }); continue; }
            double kept0 = a.Kept; bool alive = Step(a, t.D.Pnl, r);
            if (a.Kept > kept0) { net += a.Kept - kept0; c.Kept += a.Kept - kept0; c.Year[y][1] += a.Kept - kept0; c.Payouts++; c.Year[y][4]++; }
            if (!alive)
            {
                if (a.End == "PASSED") { c.Passed++; c.Year[y][3]++; if (evalOnly) a = null; else { a = new Acct { Funded = true }; net -= r.Activation; c.Spent += r.Activation; } }
                else if (a.End == "BLOWN") { if (a.Funded) c.FBlown++; else c.Blown++; c.Year[y][5]++; a = null; }
                else { c.FRetired++; a = null; }
            }
            c.Curve.Add(new[] { t.Day.ToOADate(), net });
        }
        return c;
    }

    public sealed class Stats { public int Days, Setups, Fills, Wins, Losses, Time; public double Pnl, MaxDd, Gross, GrossLoss; public double Pf { get { return GrossLoss == 0 ? 0 : Gross / GrossLoss; } } public double WinPct { get { return Fills == 0 ? 0 : 100.0 * Wins / Fills; } } public double FillPct { get { return Setups == 0 ? 0 : 100.0 * Fills / Setups; } } }
    public static Stats Of(IEnumerable<T> ts)
    {
        var s = new Stats(); double eq = 0, pk = 0;
        foreach (var t in ts)
        {
            s.Days++; if (t.D.Dir != 0) s.Setups++; if (!t.D.Filled) continue;
            s.Fills++; s.Pnl += t.D.Pnl; if (t.D.Pnl > 0) { s.Wins++; s.Gross += t.D.Pnl; } else { s.Losses++; s.GrossLoss -= t.D.Pnl; }
            if (t.D.Reason != "TARGET" && t.D.Reason != "STOP") s.Time++;
            eq += t.D.Pnl; pk = Math.Max(pk, eq); s.MaxDd = Math.Max(s.MaxDd, pk - eq);
        }
        return s;
    }

    public static void Run(List<RDay> mnq, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = m => { Console.WriteLine(m); log.AppendLine(m); };
        mnq = mnq.OrderBy(d => d.Day).ToList();
        var bars = mnq.Select(Bars).ToList(); var dates = mnq.Select(d => d.Day.Date).ToList();
        var rules = KeystoneSimRules.Presets()[1].Copy();
        var baseR = KeystoneOpeningRange.Presets().First(x => x.Mode == "FVG");
        say("OPENING FVG • MNQ • " + dates[0].ToString("yyyy-MM-dd") + " → " + dates[dates.Count - 1].ToString("yyyy-MM-dd") + " • " + dates.Count + " days • " + baseR.Describe());
        var main = Trades(bars, dates, baseR); var ms = Of(main);
        say("DEFAULT: setups " + ms.Setups + " • filled " + ms.Fills + " (" + ms.FillPct.ToString("0.0") + "%) • won " + ms.Wins + " (" + ms.WinPct.ToString("0.0") + "%) • time exits " + ms.Time + " • net $" + ms.Pnl.ToString("N0", IC) + " • PF " + ms.Pf.ToString("0.00") + " • max DD $" + ms.MaxDd.ToString("N0", IC));

        // the % of the zone
        var pcts = new double[] { 10, 15, 25, 33, 40, 50, 60, 75 };
        var pctRows = new List<object[]>();
        foreach (var pc in pcts)
        {
            var r = baseR.Copy(); r.FvgPct = pc; var ts = pc == 50 ? main : Trades(bars, dates, r); var st = Of(ts); var p = DayPnl(ts);
            var ev = EveryStart(p, rules, false); var fu = EveryStart(p, rules, true); var ch = RunChain(ts, rules, false, false);
            pctRows.Add(new object[] { pc, st, ev, fu, ch });
            say("  " + pc + "%: fills " + st.Fills + " won " + st.WinPct.ToString("0.0") + "% net $" + st.Pnl.ToString("N0", IC) + " • eval pass " + (100.0 * ev.Passed / Math.Max(1, ev.Passed + ev.Blown)).ToString("0.0") + "% • funded kept avg $" + fu.AvgKept.ToString("N0", IC) + " • eval+funded net $" + (ch.Kept - ch.Spent).ToString("N0", IC));
        }
        // stop × target at 50%
        var stops = new double[] { 1000, 1500, 2000 }; var tgts = new double[] { 500, 1000, 1500, 2000 }; var brRows = new List<object[]>();
        foreach (var sv in stops) foreach (var tv in tgts)
        {
            var r = baseR.Copy(); r.StopUsd = sv; r.TargetUsd = tv; var ts = sv == 2000 && tv == 1000 ? main : Trades(bars, dates, r); var st = Of(ts); var p = DayPnl(ts);
            var ev = EveryStart(p, rules, false); var fu = EveryStart(p, rules, true); var ch = RunChain(ts, rules, false, false);
            brRows.Add(new object[] { sv, tv, st, ev, fu, ch });
        }
        // the default in the four ways
        var pm = DayPnl(main);
        var evS = EveryStart(pm, rules, false); var fuS = EveryStart(pm, rules, true);
        var chEF = RunChain(main, rules, false, false); var chE = RunChain(main, rules, true, false);
        double perPass = rules.EvalCost * (evS.Passed + evS.Blown) / Math.Max(1, evS.Passed); var chF = RunChain(main, rules, false, true, perPass);
        say("EVAL + FUNDED (one lane): evals " + chEF.Evals + " passed " + chEF.Passed + " blown " + chEF.Blown + " • funded blown " + chEF.FBlown + " retired " + chEF.FRetired + " • payouts " + chEF.Payouts + " • spent $" + chEF.Spent.ToString("N0", IC) + " kept $" + chEF.Kept.ToString("N0", IC));
        say("EVAL ONLY: from every start day " + evS.Passed + " passed / " + evS.Blown + " blown (" + (100.0 * evS.Passed / Math.Max(1, evS.Passed + evS.Blown)).ToString("0.0") + "%) • one lane: " + chE.Evals + " evals, " + chE.Passed + " passed");
        say("FUNDED ONLY (each funded account costs $" + perPass.ToString("N0", IC) + " = the evaluations it takes to pass one): from every start day avg kept $" + fuS.AvgKept.ToString("N0", IC) + " • payouts " + fuS.AvgPayouts.ToString("0.00") + " • blown " + fuS.FBlown + " retired " + fuS.FRetired + " • one lane: kept $" + chF.Kept.ToString("N0", IC) + ", " + chF.FBlown + " blown");

        // tables by year / side / stack / exit / weekday
        Func<Func<T, string>, List<Tuple<string, Stats>>> by = key => main.Where(t => t.D.Dir != 0).GroupBy(key).OrderBy(g => g.Key).Select(g => Tuple.Create(g.Key, Of(g))).ToList();
        var byYear = by(t => t.Day.Year.ToString()); var bySide = by(t => t.D.Dir > 0 ? "BUY (bullish FVG)" : "SELL (bearish FVG)");
        var byStack = by(t => t.D.Stack >= 3 ? "3+ FVGs" : t.D.Stack + " FVG" + (t.D.Stack > 1 ? "s" : "")); var byDow = by(t => ((int)t.Day.DayOfWeek) + " " + t.Day.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant());
        var bySetup = by(t => { int m = (int)(t.D.SetupTime - t.Day.AddHours(9).AddMinutes(30)).TotalMinutes; return m <= 15 ? "a 09:45" : m <= 30 ? "b 09:46–10:00" : m <= 60 ? "c 10:01–10:30" : "d after 10:30"; });
        var reasons = main.Where(t => t.D.Filled).GroupBy(t => t.D.Reason).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count()).ToList();
        int noSetup = main.Count(t => t.D.Dir == 0), noFill = main.Count(t => t.D.Dir != 0 && !t.D.Filled);

        // LIVE ACCOUNT: own money, 10 MNQ, every trade
        var liveCurve = new List<double[]>(); double eqv = 0; foreach (var t in main) { if (t.D.Filled) eqv += t.D.Pnl; liveCurve.Add(new[] { t.Day.ToOADate(), eqv }); }
        int maxLossStreak = 0, cur = 0; foreach (var t in main.Where(t => t.D.Filled)) { if (t.D.Pnl <= 0) { cur++; maxLossStreak = Math.Max(maxLossStreak, cur); } else cur = 0; }
        double worstMonth = main.Where(t => t.D.Filled).GroupBy(t => t.Day.Year * 100 + t.Day.Month).Select(g => g.Sum(t => t.D.Pnl)).DefaultIfEmpty(0).Min();
        int months = main.Select(t => t.Day.Year * 100 + t.Day.Month).Distinct().Count(), upMonths = main.Where(t => t.D.Filled).GroupBy(t => t.Day.Year * 100 + t.Day.Month).Count(g => g.Sum(t => t.D.Pnl) > 0);

        Directory.CreateDirectory(outDir);
        var csv = new StringBuilder("day,side,stack,zone_lo,zone_hi,entry,setup,fill,exit_time,reason,points,pnl\n");
        foreach (var t in main.Where(t => t.D.Dir != 0)) csv.AppendLine(string.Join(",", t.Day.ToString("yyyy-MM-dd"), t.D.Dir > 0 ? "BUY" : "SELL", t.D.Stack, F2(t.D.ZoneLo), F2(t.D.ZoneHi), F2(t.D.Entry), t.D.SetupTime.ToString("HH:mm"), t.D.Filled ? t.D.FillTime.ToString("HH:mm") : "", t.D.Filled ? t.D.ExitTime.ToString("HH:mm") : "", t.D.Filled ? t.D.Reason : "NO FILL", t.D.Filled ? F2(t.D.Points) : "", t.D.Filled ? t.D.Pnl.ToString("0", IC) : ""));
        File.WriteAllText(Path.Combine(outDir, "open_fvg_trades.csv"), csv.ToString());

        // ---- HTML
        var h = new StringBuilder();
        Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC);
        Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> pct = v => v.ToString("0.0", IC) + "%";
        Func<Single, double> passPct = s => 100.0 * s.Passed / Math.Max(1, s.Passed + s.Blown);
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Opening FVG MNQ</title><style>");
        h.Append(":root{--bg:#0d1117;--card:#161b22;--line:#30363d;--text:#e6edf3;--mute:#8b949e;--pos:#3fb950;--neg:#f85149;--acc:#d29922;--blue:#58a6ff}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f6f8fa;--card:#fff;--line:#d0d7de;--text:#1f2328;--mute:#59636e;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--blue:#0969da}}");
        h.Append("body{background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:24px 16px}main{max-width:1100px;margin:0 auto}h1{font-size:24px;margin:0 0 4px}h2{font-size:17px;margin:28px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}");
        h.Append(".sub{color:var(--mute)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:10px;margin:14px 0}.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:12px}.card b{display:block;font-size:20px}.card small{color:var(--mute)}");
        h.Append(".wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;background:var(--card);border:1px solid var(--line);border-radius:8px;font-variant-numeric:tabular-nums}th,td{padding:6px 9px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}th{color:var(--mute);font-weight:600;font-size:12px}");
        h.Append(".pos{color:var(--pos)}.neg{color:var(--neg)}.w{white-space:normal!important;text-align:left!important;min-width:280px}.best{background:rgba(210,153,34,.14)}.note{background:var(--card);border-left:3px solid var(--acc);padding:10px 14px;border-radius:6px;margin:10px 0}svg{width:100%;height:auto;background:var(--card);border:1px solid var(--line);border-radius:8px}</style></head><body><main>");
        h.Append("<h1>★ OPENING FVG • MNQ — 6 years</h1><div class='sub'>" + dates[0].ToString("MMM d, yyyy", IC) + " → " + dates[dates.Count - 1].ToString("MMM d, yyyy", IC) + " • " + dates.Count + " trading days • 10 MNQ • stop −$2,000 • target +$1,000 • entry at 50% of the zone • the app's own engine (KeystoneOpenFvg)</div>");
        h.Append("<div class='note'><b>The rule.</b> From 09:30 New York, 5-minute candles. The first FVG (candle 3's low above candle 1's high = bullish, candle 3's high below candle 1's low = bearish) decides the side. If the next candle makes another FVG the same way, it joins the zone (from the lowest to the highest edge) and the order moves to 50% of the whole zone. A BUY limit (bullish) or SELL limit (bearish) waits there; stop −$2,000 (100 pts) and target +$1,000 (50 pts) on 10 MNQ. Not filled by 15:55 = no trade; open at 15:55 = flat. Fills: the limit price (or the open when a minute opens through it); the fill minute can only stop out; after that the stop is checked before the target. Costs $1.74 a contract.</div>");
        h.Append("<div class='cards'>");
        Action<string, string, string, string> card = (k, v, sub, c) => h.Append("<div class='card'><small>" + k + "</small><b class='" + c + "'>" + v + "</b><small>" + sub + "</small></div>");
        card("TRADES", ms.Fills.ToString("N0", IC), ms.Setups + " setups • " + pct(ms.FillPct) + " filled • " + noSetup + " days no FVG", "");
        card("WON", pct(ms.WinPct), ms.Wins + " won • " + ms.Losses + " lost (" + ms.Time + " time exits)", "");
        card("NET • LIVE 10 MNQ", usd(ms.Pnl), "PF " + ms.Pf.ToString("0.00") + " • max drawdown " + usd(ms.MaxDd), cls(ms.Pnl));
        card("EVAL PASS RATE", pct(passPct(evS)), evS.Passed + " passed / " + evS.Blown + " blown, started on every day", "");
        card("FUNDED ACCOUNT KEEPS", usd(fuS.AvgKept), "on average, " + fuS.AvgPayouts.ToString("0.0") + " payouts before it ends", cls(fuS.AvgKept));
        card("EVAL + FUNDED • ONE LANE", usd(chEF.Kept - chEF.Spent), chEF.Evals + " evals bought, " + chEF.Payouts + " payouts", cls(chEF.Kept - chEF.Spent));
        h.Append("</div>");
        // breakeven win rate
        double avgWin = ms.Wins == 0 ? 0 : ms.Gross / ms.Wins, avgLoss = ms.Losses == 0 ? 0 : ms.GrossLoss / ms.Losses;
        h.Append("<div class='note'><b>What the numbers mean.</b> With −$2,000 / +$1,000 a trade must win about " + pct(100.0 * avgLoss / Math.Max(1, avgWin + avgLoss)) + " of the time to break even (average win " + usd(avgWin) + ", average loss " + usd(avgLoss) + "). It won " + pct(ms.WinPct) + ". On a $50K evaluation with a $2,000 drawdown, the first loss on a fresh account is the blow-up; an account that is up first has room for one loss.</div>");

        h.Append("<h2>1 • The four ways (default rule)</h2><div class='wrap'><table><tr><th>WAY</th><th>WHAT HAPPENS</th><th>MONEY IN</th><th>MONEY OUT (KEPT)</th><th>NET</th></tr>");
        h.Append("<tr><td><b>EVAL + FUNDED</b></td><td>" + chEF.Evals + " evaluations • " + chEF.Passed + " passed • " + chEF.Blown + " blown • funded: " + chEF.FBlown + " blown, " + chEF.FRetired + " retired (5 payouts) • " + chEF.Payouts + " payouts</td><td>" + usd(chEF.Spent) + "</td><td class='pos'>" + usd(chEF.Kept) + "</td><td class='" + cls(chEF.Kept - chEF.Spent) + "'>" + usd(chEF.Kept - chEF.Spent) + "</td></tr>");
        h.Append("<tr><td><b>EVAL ONLY</b></td><td>one lane: " + chE.Evals + " evaluations, " + chE.Passed + " passed (" + pct(100.0 * chE.Passed / Math.Max(1, chE.Evals)) + ") • from every start day: " + pct(passPct(evS)) + " pass, in " + evS.AvgDaysPass.ToString("0.0") + " trade days (blown after " + evS.AvgDaysBlow.ToString("0.0") + ") • cost per pass " + usd(rules.EvalCost * (evS.Passed + evS.Blown) / Math.Max(1, evS.Passed)) + "</td><td>" + usd(chE.Spent) + "</td><td>–</td><td>" + chE.Passed + " passes</td></tr>");
        h.Append("<tr><td><b>FUNDED ONLY</b></td><td>a funded account from its first day, each one priced at " + usd(perPass) + " (the evaluations it takes to pass one at this pass rate) • one lane: " + chF.Evals + " accounts, " + chF.FBlown + " blown, " + chF.FRetired + " retired, " + chF.Payouts + " payouts • from every start day: one account keeps " + usd(fuS.AvgKept) + " on average over " + fuS.AvgFundDays.ToString("0") + " trade days (" + fuS.FBlown + " ended blown, " + fuS.FRetired + " retired)</td><td>" + usd(chF.Spent) + "</td><td class='pos'>" + usd(chF.Kept) + "</td><td class='" + cls(chF.Kept - chF.Spent) + "'>" + usd(chF.Kept - chF.Spent) + "</td></tr>");
        h.Append("<tr><td><b>LIVE ACCOUNT</b></td><td>own money, 10 MNQ, every trade • max drawdown " + usd(ms.MaxDd) + " • worst month " + usd(worstMonth) + " • " + upMonths + " of " + months + " months up • longest losing run " + maxLossStreak + "</td><td>margin + " + usd(ms.MaxDd) + " of room</td><td>–</td><td class='" + cls(ms.Pnl) + "'>" + usd(ms.Pnl) + "</td></tr></table></div>");
        h.Append("<p class='sub'>One lane = one account at a time over the 6 years (the next one starts the day after the last ended). More accounts trading the same signal multiply money in and out alike. LUCID FLEX 50K rules as in the app's prop sim.</p>");

        h.Append("<h2>2 • Live account equity (10 MNQ)</h2>" + Svg(liveCurve, "var(--blue)") + "<h2>3 • Eval + funded lane, net after costs</h2>" + Svg(chEF.Curve, "var(--acc)"));

        h.Append("<h2>4 • Year by year</h2><div class='wrap'><table><tr><th>YEAR</th><th>SETUPS</th><th>FILLED</th><th>WON</th><th>NET 10 MNQ</th><th>MAX DD</th><th>EVALS</th><th>PASSED</th><th>PAYOUTS</th><th>EVAL+FUNDED NET</th></tr>");
        foreach (var y in byYear) { int yy = int.Parse(y.Item1); var yr = chEF.Year.ContainsKey(yy) ? chEF.Year[yy] : new double[6]; h.Append("<tr><td>" + y.Item1 + "</td><td>" + y.Item2.Setups + "</td><td>" + y.Item2.Fills + "</td><td>" + pct(y.Item2.WinPct) + "</td><td class='" + cls(y.Item2.Pnl) + "'>" + usd(y.Item2.Pnl) + "</td><td>" + usd(y.Item2.MaxDd) + "</td><td>" + yr[2] + "</td><td>" + yr[3] + "</td><td>" + yr[4] + "</td><td class='" + cls(yr[1] - yr[0]) + "'>" + usd(yr[1] - yr[0]) + "</td></tr>"); }
        h.Append("</table></div>");

        Action<string, List<Tuple<string, Stats>>, bool> tbl = (title, rows, strip) =>
        {
            h.Append("<h2>" + title + "</h2><div class='wrap'><table><tr><th></th><th>SETUPS</th><th>FILLED</th><th>WON</th><th>NET 10 MNQ</th><th>PF</th></tr>");
            foreach (var x in rows) h.Append("<tr><td>" + (strip ? x.Item1.Substring(2) : x.Item1) + "</td><td>" + x.Item2.Setups + "</td><td>" + x.Item2.Fills + " (" + pct(x.Item2.FillPct) + ")</td><td>" + pct(x.Item2.WinPct) + "</td><td class='" + cls(x.Item2.Pnl) + "'>" + usd(x.Item2.Pnl) + "</td><td>" + x.Item2.Pf.ToString("0.00") + "</td></tr>");
            h.Append("</table></div>");
        };
        tbl("5 • Side", bySide, false); tbl("6 • FVGs in a row (one zone)", byStack, false); tbl("7 • When the zone was set", bySetup, true); tbl("8 • Weekday", byDow, true);
        h.Append("<p class='sub'>Filled trades ended: " + string.Join(" • ", reasons) + " • setups never filled: " + noFill + ".</p>");

        h.Append("<h2>9 • Entry at % of the zone</h2><div class='wrap'><table><tr><th>ENTRY %</th><th>FILLED</th><th>WON</th><th>NET 10 MNQ</th><th>MAX DD</th><th>EVAL PASS</th><th>FUNDED KEEPS</th><th>EVAL+FUNDED NET</th></tr>");
        double bestPct = pctRows.Max(x => ((Chain)x[4]).Kept - ((Chain)x[4]).Spent);
        foreach (var x in pctRows) { var st = (Stats)x[1]; var ev = (Single)x[2]; var fu = (Single)x[3]; var ch = (Chain)x[4]; double n = ch.Kept - ch.Spent; h.Append("<tr class='" + (Math.Abs(n - bestPct) < 0.01 ? "best" : "") + "'><td>" + (double)x[0] + "%" + ((double)x[0] == 50 ? " (default)" : "") + "</td><td>" + st.Fills + " (" + pct(st.FillPct) + ")</td><td>" + pct(st.WinPct) + "</td><td class='" + cls(st.Pnl) + "'>" + usd(st.Pnl) + "</td><td>" + usd(st.MaxDd) + "</td><td>" + pct(passPct(ev)) + "</td><td>" + usd(fu.AvgKept) + "</td><td class='" + cls(n) + "'>" + usd(n) + "</td></tr>"); }
        h.Append("</table></div><p class='sub'>Bullish: the % is measured down from the top of the zone (10% = a shallow pullback, fills more often; 75% = deep, fills less). Bearish: up from the bottom.</p>");

        h.Append("<h2>10 • Stop × target (entry 50%)</h2><div class='wrap'><table><tr><th>STOP / TARGET</th><th>WON</th><th>NET 10 MNQ</th><th>MAX DD</th><th>EVAL PASS</th><th>FUNDED KEEPS</th><th>EVAL+FUNDED NET</th></tr>");
        double bestBr = brRows.Max(x => ((Chain)x[5]).Kept - ((Chain)x[5]).Spent);
        foreach (var x in brRows) { var st = (Stats)x[2]; var ev = (Single)x[3]; var fu = (Single)x[4]; var ch = (Chain)x[5]; double n = ch.Kept - ch.Spent; bool def = (double)x[0] == 2000 && (double)x[1] == 1000; h.Append("<tr class='" + (Math.Abs(n - bestBr) < 0.01 ? "best" : "") + "'><td>−" + usd((double)x[0]) + " / +" + usd((double)x[1]) + (def ? " (default)" : "") + "</td><td>" + pct(st.WinPct) + "</td><td class='" + cls(st.Pnl) + "'>" + usd(st.Pnl) + "</td><td>" + usd(st.MaxDd) + "</td><td>" + pct(passPct(ev)) + "</td><td>" + usd(fu.AvgKept) + "</td><td class='" + cls(n) + "'>" + usd(n) + "</td></tr>"); }
        h.Append("</table></div><p class='sub'>Highlighted = the best eval + funded lane in each table. One 6-year path per row: a row that is best by a small margin can be luck; the year table shows whether the edge holds every year.</p>");
        h.Append("<p class='sub'>Every trade: open_fvg_trades.csv • built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + "</p></main></body></html>");
        File.WriteAllText(Path.Combine(outDir, "open_fvg_study.html"), h.ToString().Replace("</b></td><td>", "</b></td><td class='w'>"));
        File.WriteAllText(Path.Combine(outDir, "open_fvg_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }

    static string F2(double v) { return v.ToString("0.##", IC); }

    static string Svg(List<double[]> pts, string color)
    {
        if (pts.Count < 2) return "";
        double x0 = pts[0][0], x1 = pts[pts.Count - 1][0], lo = Math.Min(0, pts.Min(p => p[1])), hi = Math.Max(0, pts.Max(p => p[1])); if (hi - lo < 1) hi = lo + 1;
        int W = 1000, H = 260, pad = 40; Func<double, double> X = x => pad + (x - x0) / (x1 - x0) * (W - pad - 10); Func<double, double> Y = y => 10 + (hi - y) / (hi - lo) * (H - 40);
        var sb = new StringBuilder("<svg viewBox='0 0 " + W + " " + H + "' role='img'>");
        sb.Append("<line x1='" + pad + "' x2='" + (W - 10) + "' y1='" + Y(0).ToString("0.#", IC) + "' y2='" + Y(0).ToString("0.#", IC) + "' stroke='var(--mute)' stroke-dasharray='4 4'/>");
        for (int y = DateTime.FromOADate(x0).Year + 1; y <= DateTime.FromOADate(x1).Year; y++) { double xx = X(new DateTime(y, 1, 1).ToOADate()); sb.Append("<line x1='" + xx.ToString("0.#", IC) + "' x2='" + xx.ToString("0.#", IC) + "' y1='10' y2='" + (H - 30) + "' stroke='var(--line)'/><text x='" + (xx + 3).ToString("0.#", IC) + "' y='" + (H - 12) + "' fill='var(--mute)' font-size='12'>" + y + "</text>"); }
        sb.Append("<text x='4' y='" + (Y(hi) + 12).ToString("0.#", IC) + "' fill='var(--mute)' font-size='11'>$" + (hi / 1000).ToString("0", IC) + "K</text><text x='4' y='" + (Y(lo) - 2).ToString("0.#", IC) + "' fill='var(--mute)' font-size='11'>" + (lo < 0 ? "−$" : "$") + (Math.Abs(lo) / 1000).ToString("0", IC) + "K</text>");
        sb.Append("<polyline fill='none' stroke='" + color + "' stroke-width='1.6' points='");
        int step = Math.Max(1, pts.Count / 1500); for (int i = 0; i < pts.Count; i += step) sb.Append(X(pts[i][0]).ToString("0.#", IC) + "," + Y(pts[i][1]).ToString("0.#", IC) + " ");
        sb.Append(X(pts[pts.Count - 1][0]).ToString("0.#", IC) + "," + Y(pts[pts.Count - 1][1]).ToString("0.#", IC) + "'/></svg>");
        return sb.ToString();
    }
}
