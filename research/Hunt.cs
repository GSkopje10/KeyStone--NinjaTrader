// THE HUNT: every entry family × stop × target × window × side on MNQ + MGC 1-minute bars, scored first per trade
// (2021–2024 only), then the best run through full prop lifecycles (evaluation → funded → payouts) for Lucid Flex, Tradeify
// Select (Flex payouts) and Take Profit Trader PRO at several risk sizes; the winners are judged on 2025–2026, untouched.
// The goal is not a trading edge: it is the plan (entry + R:R + size + firm) whose payouts beat the evaluation fees.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Hunt
{
    static readonly double[] Targets = { 0.5, 0.75, 1, 1.5, 2, 3, 4, 5 };
    static readonly string[] Windows = { "ALL", "ASIA 18–02", "LONDON 02–08", "NY AM 08–11", "NY MID 11–14", "NY PM 14–15:30" };
    static int WindowOf(int slot) { int m = (slot + 18 * 60) % 1440, h = m / 60; if (h >= 18 || h < 2) return 1; if (h < 8) return 2; if (h < 11) return 3; if (h < 14) return 4; return 5; }
    static readonly string[] Sides = { "BOTH", "LONG", "SHORT" };
    const double Comm = 1.24;   // $ a micro, round trip

    sealed class Sig { public int Day, Slot, Dir; public float Stop, Mfe, Close; public bool StopHit; public float[] Mae = new float[8]; }   // R units, stop in points
    sealed class Fam { public string Name; public string Sym; public int StopVar; public List<Sig> Sigs = new List<Sig>(); }

    public static void Run(Dictionary<string, List<RDay>> bySym, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder();
        Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        DateTime isEnd = new DateTime(2025, 1, 1), isStart = new DateTime(2021, 1, 1);
        // a common calendar index across symbols
        var allDays = bySym.Values.SelectMany(l => l.Select(d => d.Day)).Distinct().OrderBy(d => d).ToList(); var dayIx = new Dictionary<DateTime, int>(); for (int i = 0; i < allDays.Count; i++) dayIx[allDays[i]] = i;
        var fams = new List<Fam>();
        foreach (var sym in bySym.Keys)
        {
            var sp = Spec.Of(sym); double tk = sp.Tick, pv = sp.PointValue;
            double[] fixedStops = sym == "MGC" ? new[] { 1.0, 2, 4, 8 } : new[] { 5.0, 10, 20, 40 };
            double[] atrFr = { 0.05, 0.1, 0.2 };
            // raw signals per family: (day, slot, dir, natural stop level)
            var raw = new Dictionary<string, List<Tuple<RDay, int, int, double>>>();
            Action<string, RDay, int, int, double> add = (f, d, k, dir, lvl) => { List<Tuple<RDay, int, int, double>> l; if (!raw.TryGetValue(f, out l)) { l = new List<Tuple<RDay, int, int, double>>(); raw[f] = l; } l.Add(Tuple.Create(d, k, dir, lvl)); };
            int lastEntry = R.S(15, 30);
            foreach (var d in bySym[sym])
            {
                var last = new Dictionary<string, int>();
                Func<string, int, bool> spaced = (key, k) => { int p; if (last.TryGetValue(key, out p) && k - p < 10) return false; last[key] = k; return true; };
                // VWAP + EMA 20
                var vwap = new double[R.Slots]; var ema = new double[R.Slots]; double pvs = 0, vv = 0, e = double.NaN;
                for (int k = 0; k < R.Slots; k++) { if (!d.Has(k)) { vwap[k] = k > 0 ? vwap[k - 1] : double.NaN; ema[k] = e; continue; } double tp = (d.H[k] + d.L[k] + d.C[k]) / 3.0, v = Math.Max(1, d.V[k]); pvs += tp * v; vv += v; vwap[k] = pvs / vv; e = double.IsNaN(e) ? d.C[k] : e + (d.C[k] - e) * (2.0 / 21); ema[k] = e; }
                for (int k = 6; k <= lastEntry; k++)
                {
                    if (!d.Has(k) || !d.Has(k - 1)) continue;
                    double c = d.C[k], lo5 = d.Lo(k - 4, k), hi5 = d.Hi(k - 4, k);
                    foreach (int n in new[] { 15, 30, 60 })
                    {
                        if (k < n + 1) continue; double hh = d.Hi(k - n, k - 1), ll = d.Lo(k - n, k - 1);
                        if (c > hh && d.C[k - 1] <= hh && spaced("B" + n + "L", k)) add("BREAK " + n + "m", d, k, 1, lo5);
                        if (c < ll && d.C[k - 1] >= ll && spaced("B" + n + "S", k)) add("BREAK " + n + "m", d, k, -1, hi5);
                        if (d.L[k] < ll && c > d.O[k] && c > d.H[k - 1] && spaced("S" + n + "L", k)) add("SWEEP " + n + "m", d, k, 1, d.L[k]);
                        if (d.H[k] > hh && c < d.O[k] && c < d.L[k - 1] && spaced("S" + n + "S", k)) add("SWEEP " + n + "m", d, k, -1, d.H[k]);
                    }
                    if (k >= 10 && !double.IsNaN(ema[k - 5]))
                    {
                        double slope = ema[k] - ema[k - 5];
                        if (d.C[k - 1] < vwap[k - 1] && c > vwap[k] && slope > 0 && spaced("VL", k)) add("VWAP CROSS", d, k, 1, lo5);
                        if (d.C[k - 1] > vwap[k - 1] && c < vwap[k] && slope < 0 && spaced("VS", k)) add("VWAP CROSS", d, k, -1, hi5);
                        if (slope > 0 && d.L[k] <= ema[k] && c > ema[k] && c > d.O[k] && spaced("PL", k)) add("EMA PULLBACK", d, k, 1, d.L[k]);
                        if (slope < 0 && d.H[k] >= ema[k] && c < ema[k] && c < d.O[k] && spaced("PS", k)) add("EMA PULLBACK", d, k, -1, d.H[k]);
                    }
                }
                // BH / BL on 5-minute candles
                for (int k = 0; k + 15 <= lastEntry; k += 5)
                {
                    double rO = d.OpenAt(k), rC = d.CloseAt(k + 4), gO = d.OpenAt(k + 5), gC = d.CloseAt(k + 9); if (double.IsNaN(rO) || double.IsNaN(gC)) continue;
                    double h1 = d.Hi(k, k + 4), l1 = d.Lo(k, k + 4), h2 = d.Hi(k + 5, k + 9), l2 = d.Lo(k + 5, k + 9);
                    if (rC < rO && gC > gO) for (int j = k + 10; j < k + 20 && j <= lastEntry; j++) if (d.Has(j) && d.C[j] > h2) { add("BH / BL 5m", d, j, 1, Math.Min(l1, l2)); break; }
                    if (rC > rO && gC < gO) for (int j = k + 10; j < k + 20 && j <= lastEntry; j++) if (d.Has(j) && d.C[j] < l2) { add("BH / BL 5m", d, j, -1, Math.Max(h1, h2)); break; }
                }
                // opening range breaks + opening drive candles at the three session opens
                foreach (var op in new[] { Tuple.Create("ASIA", R.S(18, 0)), Tuple.Create("LONDON", R.S(3, 0)), Tuple.Create("NY", R.S(9, 30)) })
                foreach (int m in new[] { 5, 15, 30 })
                {
                    int a = op.Item2, b = a + m - 1; if (b + 1 > lastEntry) continue; double hi = d.Hi(a, b), lo = d.Lo(a, b), o0 = d.OpenAt(a), c0 = d.CloseAt(b); if (double.IsNaN(hi) || double.IsNaN(c0)) continue;
                    for (int j = b + 1; j <= Math.Min(lastEntry, b + 120); j++) { if (!d.Has(j)) continue; if (d.C[j] > hi) { add("ORB " + m + "m " + op.Item1, d, j, 1, lo); break; } if (d.C[j] < lo) { add("ORB " + m + "m " + op.Item1, d, j, -1, hi); break; } }
                    int mm = m == 5 ? 1 : m == 15 ? 5 : 15; int b2 = a + mm - 1; double o2 = d.OpenAt(a), c2 = d.CloseAt(b2);
                    if (!double.IsNaN(o2) && !double.IsNaN(c2) && Math.Abs(c2 - o2) >= tk && d.Has(b2)) add("DRIVE " + mm + "m " + op.Item1, d, b2, Math.Sign(c2 - o2), c2 > o2 ? d.Lo(a, b2) : d.Hi(a, b2));
                }
            }
            // outcomes for every stop variant
            foreach (var kv in raw)
            for (int sv = 0; sv < 8; sv++)
            {
                var f = new Fam { Name = kv.Key, Sym = sym, StopVar = sv };
                foreach (var s in kv.Value)
                {
                    var d = s.Item1; int k = s.Item2, dir = s.Item3; double entry = d.C[k] + dir * tk;
                    double stop = sv == 0 ? Math.Abs(entry - s.Item4) + tk : sv <= 4 ? fixedStops[sv - 1] : atrFr[sv - 5] * d.RthAtr;
                    stop = Math.Round(stop / tk) * tk;
                    if (stop < 8 * tk || stop * pv > 2000 || (d.RthAtr > 0 && stop > 0.6 * d.RthAtr)) continue;
                    var g = new Sig { Day = dayIx[d.Day], Slot = k, Dir = dir, Stop = (float)stop };
                    double mfe = 0, mae = 0; int exitK = R.S(15, 55); bool hit = false; var tHit = new bool[8]; double closePx = double.NaN;
                    for (int j = k + 1; j <= exitK; j++)
                    {
                        if (!d.Has(j)) continue;
                        double adv = dir > 0 ? entry - d.L[j] : d.H[j] - entry, fav = dir > 0 ? d.H[j] - entry : entry - d.L[j];
                        if (adv >= stop) { hit = true; mae = Math.Max(mae, adv); break; }   // stop first inside a bar
                        mae = Math.Max(mae, adv); mfe = Math.Max(mfe, fav); closePx = d.C[j];
                        for (int t = 0; t < 8; t++) if (!tHit[t] && mfe >= Targets[t] * stop) { tHit[t] = true; g.Mae[t] = (float)(mae / stop); }
                    }
                    g.StopHit = hit; g.Mfe = (float)(mfe / stop); g.Close = double.IsNaN(closePx) ? 0f : (float)(dir * (closePx - entry) / stop);
                    for (int t = 0; t < 8; t++) if (!tHit[t]) g.Mae[t] = (float)Math.Min(1.0, mae / stop);
                    f.Sigs.Add(g);
                }
                if (f.Sigs.Count >= 100) fams.Add(f);
            }
        }
        say("signals ready: " + fams.Count + " family × symbol × stop sets, " + fams.Sum(f => f.Sigs.Count).ToString("N0") + " trades (" + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s)");
        Func<Fam, Sig, int, double> rOf = (f, g, t) =>
        {
            var sp = Spec.Of(f.Sym); double cost = (Comm + (g.StopHit && g.Mfe < Targets[t] ? sp.Tick * sp.PointValue : 0)) / (g.Stop * sp.PointValue);
            double r = g.Mfe >= Targets[t] ? Targets[t] : g.StopHit ? -1 : Math.Min(g.Close, Targets[t]);
            return r - cost;
        };
        // ---- stage 1: per trade, the first signal of each day (one trade a day per account), 2021–2024
        int isA = allDays.FindIndex(x => x >= isStart), isB = allDays.FindIndex(x => x >= isEnd);
        var combos = new List<Tuple<Fam, int, int, int, double, double, int>>();   // fam, window, side, target, EV R, win %, days
        var lockObj = new object(); long tested = 0;
        Parallel.ForEach(fams, f =>
        {
            for (int w = 0; w < Windows.Length; w++)
            for (int sd = 0; sd < 3; sd++)
            {
                var first = new Dictionary<int, Sig>();
                foreach (var g in f.Sigs) { if (g.Day < isA || g.Day >= isB) continue; if (w > 0 && WindowOf(g.Slot) != w) continue; if (sd == 1 && g.Dir < 0 || sd == 2 && g.Dir > 0) continue; if (!first.ContainsKey(g.Day)) first[g.Day] = g; }
                if (first.Count < 150) continue;
                for (int t = 0; t < 8; t++)
                {
                    double sum = 0; int win = 0; foreach (var g in first.Values) { double r = rOf(f, g, t); sum += r; if (r > 0) win++; }
                    var c = Tuple.Create(f, w, sd, t, sum / first.Count, 100.0 * win / first.Count, first.Count);
                    lock (lockObj) { combos.Add(c); tested++; }
                }
            }
        });
        say("stage 1: " + tested.ToString("N0") + " setups scored on 2021–2024 (" + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s)");
        var pick = combos.OrderByDescending(c => c.Item5).Take(1500).Concat(combos.Where(c => c.Item5 > -0.08 && Targets[c.Item4] >= 0.75).OrderByDescending(c => c.Item6).Take(500)).Distinct().ToList();
        // ---- stage 2: prop lifecycles
        var firms = new List<Tuple<PRules, double>>();   // rules, activation fee
        { var r = PropMain.Flex(50); r.Name = "LUCID FLEX 50K"; r.Cost = 120; firms.Add(Tuple.Create(r, 0.0)); }
        { var r = PropMain.Flex(50); r.Name = "TRADEIFY SELECT 50K (Flex payouts)"; r.Cost = 99; r.EvalConsistency = 40; r.MinEvalDays = 3; r.PayCap = 2500; r.Split = 90; firms.Add(Tuple.Create(r, 0.0)); }
        { var r = PropMain.Flex(50); r.Name = "TAKE PROFIT TRADER 50K → PRO"; r.Cost = 170; r.EvalConsistency = 50; r.MinEvalDays = 5; r.PayMode = "DAILY"; r.FundIntraday = true; r.LockAt = 0; r.PayCap = 0; r.PayMin = 250; r.Split = 80; r.MaxPayouts = 0; firms.Add(Tuple.Create(r, 130.0)); }
        double[] risks = { 250, 400, 600, 800, 1000, 1500, 2000 };
        int maxMicros = 40;
        var results = new List<Res>(); long sims = 0;
        Parallel.ForEach(pick, c =>
        {
            var f = c.Item1; var sp = Spec.Of(f.Sym);
            // day streams: ONE trade a day, or TWO (a second one after a losing first)
            var byDay = f.Sigs.Where(g => (c.Item2 == 0 || WindowOf(g.Slot) == c.Item2) && !(c.Item3 == 1 && g.Dir < 0) && !(c.Item3 == 2 && g.Dir > 0)).GroupBy(g => g.Day).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Slot).ToList());
            foreach (int mode in new[] { 1, 2 })
            foreach (double risk in risks)
            foreach (double fundShare in new[] { 1.0, 0.5 })
            {
                var stream = new List<PDay>(); bool any = false;
                for (int di = 0; di < allDays.Count; di++)
                {
                    List<Sig> l; if (!byDay.TryGetValue(di, out l)) { stream.Add(new PDay { Day = allDays[di] }); continue; }
                    double pnl = 0, worst = 0, best = 0; int taken = 0, lastOut = -1;
                    foreach (var g in l)
                    {
                        if (taken >= mode || g.Slot <= lastOut) continue;
                        double qty = Math.Min(maxMicros, Math.Floor(risk / (g.Stop * sp.PointValue))); if (qty < 1) continue;
                        double usd = qty * g.Stop * sp.PointValue;   // the risk actually taken
                        double r = rOf(f, g, c.Item4); double mae = -g.Mae[c.Item4] * usd - qty * Comm;
                        worst = Math.Min(worst, pnl + Math.Min(mae, r * usd)); pnl += r * usd; best = Math.Max(best, pnl); taken++; any = true;
                        lastOut = g.Slot + 30;   // a second trade only after the first is out (approx.)
                        if (r > 0) break;   // a winning day ends
                    }
                    stream.Add(new PDay { Day = allDays[di], Pnl = pnl, Worst = worst, Best = best, Traded = taken > 0 });
                }
                if (!any) continue;
                foreach (var fm in firms)
                {
                    var rr = fm.Item1;
                    var a = Each(stream, isStart, isEnd, rr, 1, fundShare, fm.Item2);
                    var res = new Res { C = c, Mode = mode, Risk = risk, FundShare = fundShare, Firm = rr.Name, Rules = rr, Act = fm.Item2, Is = a };
                    lock (lockObj) { results.Add(res); sims++; }
                }
            }
        });
        say("stage 2: " + sims.ToString("N0") + " prop lifecycles on 2021–2024 (" + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s)");
        // ---- stage 3: the best 300 on 2025–2026 (never used to choose)
        var top = results.Where(r => r.Is.N >= 100 && r.Is.Net > 0).GroupBy(r => r.C.Item1.Name + "|" + r.C.Item1.Sym + "|" + r.C.Item1.StopVar + "|" + r.C.Item3 + "|" + r.C.Item4 + "|" + r.Firm + "|" + r.Risk + "|" + r.FundShare)
            .Select(g => g.OrderByDescending(x => x.Is.Net).First()).OrderByDescending(r => r.Is.Net).Take(1500).ToList();
        Parallel.ForEach(top, r =>
        {
            var f = r.C.Item1; var sp = Spec.Of(f.Sym);
            var byDay = f.Sigs.Where(g => (r.C.Item2 == 0 || WindowOf(g.Slot) == r.C.Item2) && !(r.C.Item3 == 1 && g.Dir < 0) && !(r.C.Item3 == 2 && g.Dir > 0)).GroupBy(g => g.Day).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Slot).ToList());
            var stream = Stream(allDays, byDay, f, r.C.Item4, r.Mode, r.Risk, maxMicros, rOf);
            r.Oos = Each(stream, isEnd, new DateTime(2026, 12, 31), r.Rules, 1, r.FundShare, r.Act);
            r.Y2026 = Each(stream, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), r.Rules, 1, r.FundShare, r.Act);
            r.Years = new double[6]; for (int y = 0; y < 6; y++) r.Years[y] = Each(stream, new DateTime(2021 + y, 1, 1), new DateTime(2022 + y, 1, 1), r.Rules, 1, r.FundShare, r.Act).Net;
            r.Stream = stream;
        });
        // ranked ONLY on 2021–2024 (years 0–3): most money-making years, then the worst of them, then the 2021–24 total — 2025 and 2026 stay an honest test
        var ranked = top.Where(r => r.Oos != null && r.Oos.N >= 30).OrderByDescending(r => r.Years.Take(4).Count(v => v > 0)).ThenByDescending(r => r.Years.Take(4).Min()).ThenByDescending(r => r.Is.Net).ToList();
        // ---- report
        var sb = new StringBuilder();
        sb.AppendLine("# THE HUNT • prop-firm plans from " + tested.ToString("N0") + " setups and " + sims.ToString("N0") + " prop lifecycles");
        sb.AppendLine();
        sb.AppendLine("Data: MNQ + MGC 1-minute bars " + allDays.First().ToString("yyyy-MM-dd") + " .. " + allDays.Last().ToString("yyyy-MM-dd") + ". Chosen on **2021–2024** only, judged on **2025–2026** (never used to choose).");
        sb.AppendLine("Entry at the signal bar's close + 1 tick; stop and target are brackets (stop: +1 tick slippage; a bar touching the stop counts it first); out at 15:55 NY. $" + Comm + " a micro round trip. Max 40 micros. One trade per account per day (or a second after a losing first).");
        sb.AppendLine("Firm models (editable, check before buying): Lucid Flex 50K $120 • Tradeify Select 50K $99, 40% consistency, Flex payouts capped $2,500 • Take Profit Trader 50K $170 + $130 PRO activation, intraday trailing when funded, payouts above the buffer. Net per eval = payouts − eval fee − activation (each eval started on every 3rd trading day of the period, run to its end).");
        sb.AppendLine();
        sb.AppendLine("## The best plans — ranked on 2021–2024 ONLY (most years with money made per eval, then the worst year); 2025 and 2026 are the honest test");
        sb.AppendLine("| # | entry | symbol | stop | window | side | target | trades / day | firm | risk / trade | funded size | net/eval 2021 | 2022 | 2023 | 2024 | 2025 (test) | 2026 (test) | years + 21–24 • test | pass % 25–26 | days to pass |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        string[] stopNames = { "NATURAL (pattern)", "FIXED A", "FIXED B", "FIXED C", "FIXED D", "0.05 × ATR", "0.10 × ATR", "0.20 × ATR" };
        Func<Fam, int, string> stopName = (f, sv) => sv >= 1 && sv <= 4 ? (f.Sym == "MGC" ? new[] { "1", "2", "4", "8" } : new[] { "5", "10", "20", "40" })[sv - 1] + " pts" : stopNames[sv];
        int ix = 0;
        foreach (var r in ranked.Take(30))
        {
            var f = r.C.Item1;
            sb.AppendLine("| " + (++ix) + " | " + f.Name + " | " + f.Sym + " | " + stopName(f, f.StopVar) + " | " + Windows[r.C.Item2] + " | " + Sides[r.C.Item3] + " | " + Targets[r.C.Item4] + "R | " + (r.Mode == 1 ? "1" : "≤2") + " | " + r.Firm + " | $" + r.Risk.ToString("0") + " | " + (r.FundShare == 1 ? "same" : "half") + " | " + string.Join(" | ", r.Years.Select(v => (v >= 0 ? "$" : "−$") + Math.Abs(v).ToString("0"))) + " | **" + r.Years.Take(4).Count(v => v > 0) + "/4 • " + r.Years.Skip(4).Count(v => v > 0) + "/2** | " + r.Oos.Pass.ToString("0") + "% | " + r.Oos.AvgEvalDays.ToString("0") + " |");
        }
        sb.AppendLine();
        int positiveBoth = top.Count(r => r.Oos != null && r.Is.Net > 0 && r.Oos.Net > 0);
        sb.AppendLine(positiveBoth + " of the " + top.Count + " plans that made money on 2021–24 also made money per eval in 2025–26; " + top.Count(r => r.Years != null && r.Years.All(v => v > 0)) + " made money in every single year 2021–2026; " + top.Count(r => r.Years != null && r.Years.Count(v => v > 0) >= 5) + " in at least 5 of the 6.");
        sb.AppendLine();
        // the winner's month-by-month program in 2026 with 50 evaluations
        // ---- PORTFOLIO: the best distinct plans (different entry / instrument / window), evaluations shared out in turn, each year from $5K
        var distinct = new List<Res>(); var seen = new HashSet<string>();
        foreach (var r in ranked) { string key = r.C.Item1.Name + "|" + r.C.Item1.Sym + "|" + r.C.Item2; if (seen.Add(key)) distinct.Add(r); if (distinct.Count == 5) break; }
        sb.AppendLine("## THE PORTFOLIO • the 5 best DIFFERENT plans (picked on 2021–2024 only), your evaluations shared out among them in turn");
        ix = 0; foreach (var r in distinct) { var f = r.C.Item1; sb.AppendLine((++ix) + ". " + f.Name + " • " + f.Sym + " • stop " + stopName(f, f.StopVar) + " • " + Windows[r.C.Item2] + " • " + Sides[r.C.Item3] + " • target " + Targets[r.C.Item4] + "R • risk $" + r.Risk.ToString("0") + " a trade (funded " + (r.FundShare == 1 ? "same" : "half") + ") • " + (r.Mode == 1 ? "1 trade a day" : "up to 2 a day") + " • " + r.Firm); }
        sb.AppendLine();
        sb.AppendLine("Each year starts fresh with $5,000: 5 new evaluations every week from the cash in hand (plan 1, 2, 3, 4, 5, 1, …), at most 50 evaluations and 15 funded at a time. Compared with putting everything on plan #1 alone (all accounts copying the same signals).");
        sb.AppendLine("2021–2024 = the years the plans were picked on; **2025 and 2026 = the honest test**.");
        sb.AppendLine("| year | PORTFOLIO: evals bought | passed | payouts | spent | paid out | **net** | lowest cash | PLAN #1 ALONE: net | lowest cash |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        for (int y = 2021; y <= 2026; y++)
        {
            var pf = Portfolio(distinct.Select(r => Tuple.Create(r.Stream, r.Rules, r.FundShare, r.Act)).ToList(), new DateTime(y, 1, 1), new DateTime(y, 12, 31), 5000);
            var one = Portfolio(new[] { Tuple.Create(ranked[0].Stream, ranked[0].Rules, ranked[0].FundShare, ranked[0].Act) }.ToList(), new DateTime(y, 1, 1), new DateTime(y, 12, 31), 5000);
            sb.AppendLine("| " + y + " | " + pf[0].ToString("0") + " | " + pf[1].ToString("0") + " | " + pf[2].ToString("0") + " | $" + pf[3].ToString("N0") + " | $" + pf[4].ToString("N0") + " | **" + (pf[4] - pf[3] >= 0 ? "+$" : "−$") + Math.Abs(pf[4] - pf[3]).ToString("N0") + "** | $" + pf[5].ToString("N0") + " | " + (one[4] - one[3] >= 0 ? "+$" : "−$") + Math.Abs(one[4] - one[3]).ToString("N0") + " | $" + one[5].ToString("N0") + " |");
        }
        sb.AppendLine();
        for (int pi = 0; pi < Math.Min(1, ranked.Count); pi++)
        {
            var best = ranked[pi]; var bf = best.C.Item1;
            foreach (var yr in new[] { 2025, 2026 })
            {
                sb.AppendLine("## PLAN #" + (pi + 1) + " (" + bf.Name + " " + bf.Sym + " • " + Targets[best.C.Item4] + "R • $" + best.Risk.ToString("0") + " • " + best.Firm + ") — " + yr + " with your $5K");
                sb.AppendLine(Program(best.Stream, new DateTime(yr, 1, 1), new DateTime(yr, 12, 31), best.Rules, best.FundShare, best.Act, 5000));
            }
        }
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "hunt.md"), sb.ToString());
        var csv = new StringBuilder("rank,entry,symbol,stop,window,side,target_r,trades_day,firm,risk,funded_size,is_net_per_eval,oos_net_per_eval,net_2026,oos_pass_pct,oos_payouts,is_trades_win_pct,is_ev_r,y2021,y2022,y2023,y2024,y2025,y2026\n");
        ix = 0; foreach (var r in ranked.Take(300)) { var f = r.C.Item1; csv.AppendLine(string.Join(",", ++ix, f.Name, f.Sym, stopName(f, f.StopVar), Windows[r.C.Item2], Sides[r.C.Item3], Targets[r.C.Item4], r.Mode, r.Firm.Replace(",", " "), r.Risk, r.FundShare, r.Is.Net.ToString("0"), r.Oos.Net.ToString("0"), r.Y2026.Net.ToString("0"), r.Oos.Pass.ToString("0"), r.Oos.Payouts.ToString("0.00"), r.C.Item6.ToString("0.0"), r.C.Item5.ToString("0.000"), string.Join(",", r.Years.Select(v => v.ToString("0"))))); }
        File.WriteAllText(Path.Combine(outDir, "hunt_top300.csv"), csv.ToString());
        File.WriteAllText(Path.Combine(outDir, "hunt_log.txt"), log.ToString());
        Console.Write(sb.ToString());
    }

    sealed class Res { public Tuple<Fam, int, int, int, double, double, int> C; public int Mode; public double Risk, FundShare, Act; public string Firm; public PRules Rules; public Prop.Summary Is, Oos, Y2026; public List<PDay> Stream; public double[] Years; }

    static List<PDay> Stream(List<DateTime> allDays, Dictionary<int, List<Sig>> byDay, Fam f, int t, int mode, double risk, int maxMicros, Func<Fam, Sig, int, double> rOf)
    {
        var sp = Spec.Of(f.Sym); var stream = new List<PDay>();
        for (int di = 0; di < allDays.Count; di++)
        {
            List<Sig> l; if (!byDay.TryGetValue(di, out l)) { stream.Add(new PDay { Day = allDays[di] }); continue; }
            double pnl = 0, worst = 0, best = 0; int taken = 0, lastOut = -1;
            foreach (var g in l)
            {
                if (taken >= mode || g.Slot <= lastOut) continue;
                double qty = Math.Min(maxMicros, Math.Floor(risk / (g.Stop * sp.PointValue))); if (qty < 1) continue;
                double usd = qty * g.Stop * sp.PointValue, r = rOf(f, g, t), mae = -g.Mae[t] * usd - qty * Comm;
                worst = Math.Min(worst, pnl + Math.Min(mae, r * usd)); pnl += r * usd; best = Math.Max(best, pnl); taken++; lastOut = g.Slot + 30;
                if (r > 0) break;
            }
            stream.Add(new PDay { Day = allDays[di], Pnl = pnl, Worst = worst, Best = best, Traded = taken > 0 });
        }
        return stream;
    }

    // an evaluation started on every 3rd trading day of the period, run to its end (or 300 days); net = cash − fee − activation
    static Prop.Summary Each(List<PDay> days, DateTime from, DateTime to, PRules r, double es, double fs, double act)
    {
        var s = new Prop.Summary(); var passDays = new List<int>(); int ix = 0;
        for (int i = 0; i < days.Count; i++)
        {
            if (days[i].Day < from || days[i].Day >= to || !days[i].Traded) continue; if (ix++ % 3 != 0) continue;
            var a = new PAcct(r, es, fs);
            for (int k = 0; k < 300 && !a.Dead && i + k < days.Count; k++) a.Step(days[i + k]);
            s.N++; if (a.Passed) { s.Pass++; passDays.Add(a.EvalDays); }
            s.Payouts += a.Payouts; s.Cash += a.Cash; s.Net += a.Cash - r.Cost - (a.Passed ? act : 0);
        }
        if (s.N == 0) return s;
        s.Pass = 100 * s.Pass / s.N; s.AvgEvalDays = passDays.Count > 0 ? passDays.Average() : 0; s.Payouts /= s.N; s.Cash /= s.N; s.Net /= s.N;
        return s;
    }

    // several plans: evaluations bought 5 a week, assigned in turn; → bought, passed, payouts, spent, paid, lowest cash
    static double[] Portfolio(List<Tuple<List<PDay>, PRules, double, double>> plans, DateTime from, DateTime to, double budget)
    {
        double cash = budget, spent = 0, paid = 0, low = budget; int bought = 0, passed = 0, payouts = 0, turn = 0; var accts = new List<Tuple<PAcct, int>>();
        var maps = plans.Select(p => p.Item1.Where(x => x.Day >= from && x.Day < to).ToDictionary(x => x.Day, x => x)).ToList();
        var cal = plans.SelectMany(p => p.Item1.Select(x => x.Day)).Where(d => d >= from && d < to).Distinct().OrderBy(d => d).ToList(); int lastWeek = -1;
        foreach (var day in cal)
        {
            int week = (int)(day - from).TotalDays / 7;
            if (week != lastWeek)
            {
                lastWeek = week;
                for (int b = 0; b < 5 && accts.Count(a => !a.Item1.Funded) < 50 && accts.Count(a => a.Item1.Funded) < 15; b++)
                { var pl = plans[turn % plans.Count]; if (cash < pl.Item2.Cost) break; cash -= pl.Item2.Cost; spent += pl.Item2.Cost; bought++; accts.Add(Tuple.Create(new PAcct(pl.Item2, 1, pl.Item3), turn % plans.Count)); turn++; }
            }
            foreach (var a in accts)
            {
                PDay pd; if (!maps[a.Item2].TryGetValue(day, out pd)) continue;
                bool was = a.Item1.Funded; double c = a.Item1.Step(pd);
                if (!was && a.Item1.Funded) { passed++; double act = plans[a.Item2].Item4; cash -= act; spent += act; }
                if (c > 0) { payouts++; paid += c; cash += c; }
            }
            accts.RemoveAll(a => a.Item1.Dead); low = Math.Min(low, cash);
        }
        return new double[] { bought, passed, payouts, spent, paid, low };
    }

    // a real program: $budget of evaluations, 50 running at a time, cash paid back is spent on new ones; month by month
    static string Program(List<PDay> days, DateTime from, DateTime to, PRules r, double fs, double act, double budget)
    {
        var sb = new StringBuilder(); double cash = budget, spent = 0, paid = 0; int bought = 0, passed = 0, payouts = 0; var accts = new List<PAcct>();
        Func<bool> buy = () => { if (cash < r.Cost) return false; cash -= r.Cost; spent += r.Cost; bought++; accts.Add(new PAcct(r, 1, fs)); return true; };
        var month = DateTime.MinValue; var rows = new List<string>(); int lastWeek = -1;
        foreach (var d in days.Where(x => x.Day >= from && x.Day < to))
        {
            var m = new DateTime(d.Day.Year, d.Day.Month, 1);
            if (m != month) { if (month != DateTime.MinValue) rows.Add("| " + month.ToString("MMM", CultureInfo.InvariantCulture) + " | " + bought + " | " + passed + " | " + payouts + " | $" + spent.ToString("N0") + " | $" + paid.ToString("N0") + " | $" + cash.ToString("N0") + " |"); month = m; }
            for (int s = 0; s < accts.Count; s++)
            {
                var a = accts[s]; bool was = a.Funded; double c = a.Step(d); if (!was && a.Funded) { passed++; cash -= act; spent += act; }
                if (c > 0) { payouts++; paid += c; cash += c; }
            }
            accts.RemoveAll(a => a.Dead);
            int week = (int)(d.Day - from).TotalDays / 7;
            if (week != lastWeek) { lastWeek = week; for (int b = 0; b < 5 && accts.Count(a => !a.Funded) < 50 && accts.Count(a => a.Funded) < 15; b++) if (!buy()) break; }   // 5 new evals a week; 15 funded at most (≈ 3 firms × 5 per person)
        }
        rows.Add("| " + month.ToString("MMM", CultureInfo.InvariantCulture) + " | " + bought + " | " + passed + " | " + payouts + " | $" + spent.ToString("N0") + " | $" + paid.ToString("N0") + " | $" + cash.ToString("N0") + " |");
        sb.AppendLine("Start with $" + budget.ToString("N0") + "; 5 new evaluations bought every week from the cash in hand (at most 50 evaluations and 15 funded accounts at a time ≈ 3 firms × 5 per person). All accounts copy the same signals, so accounts started in different weeks are at different stages. Cumulative:");
        sb.AppendLine("| month | evals bought | passed | payouts | spent (fees + activations) | paid out | cash in hand |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var x in rows) sb.AppendLine(x);
        sb.AppendLine();
        sb.AppendLine("End: spent $" + spent.ToString("N0") + ", paid out $" + paid.ToString("N0") + " → **net $" + (paid - spent).ToString("N0") + "** (cash in hand $" + cash.ToString("N0") + ", plus " + accts.Count(a => a.Funded && !a.Dead) + " funded accounts still running).");
        return sb.ToString();
    }
}
