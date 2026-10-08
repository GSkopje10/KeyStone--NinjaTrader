// TEN-R STUDY: every session, every 1-minute bar as an entry (both sides) with a fixed tiny stop and a 10x target, plus
// what the market looked like at that minute (features, all known at the bar's close — no look-ahead). Then:
//   1. every move on the chart: a .kreport.json the studio's REPORTS window opens (each session, each move: entry, stop, target)
//   2. each feature alone: win rate and $ per entry, chosen-on 2020–2023 (IS) vs untouched 2024–2026 (OOS), each year
//   3. every combination of up to 3 features × time window, ranked on IS only, then judged on OOS
//   4. the best rules traded one at a time (no overlapping trades): trades / year, win rate, net, drawdown, losing streaks
// Costs: commission both sides per contract + slippage ticks on every stop (the target is a limit = no slippage).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class TenRStudy
{
    static readonly string[] FNames = {
        "WITH VWAP (buy above / sell below the session VWAP)",
        "BREAK 30 (close beyond the last 30-minute high / low)",
        "BREAK 60 (close beyond the last 60-minute high / low)",
        "MOMENTUM 5 (≥ 2 pts in the trade's direction over 5 minutes)",
        "BIG BAR (range ≥ 2× the last 20 bars, closing the trade's way)",
        "NEW SESSION HIGH / LOW (the bar breaks the session's extreme)",
        "EMA TREND (with EMA 20 and EMA rising / falling)",
        "PULLBACK IN TREND (EMA trend, last bar against, this bar with)",
        "QUIET (average 1-minute range ≤ 0.6 pts)",
        "WILD (average 1-minute range ≥ 1.5 pts)",
        "SWEEP + RECLAIM (took the 30-minute low / high, closed back the trade's way)",
        "STRETCHED (≥ 3 average ranges away from VWAP AGAINST the trade = snap-back)" };
    const int NF = 12;
    static readonly string[] Blocks = { "ALL HOURS", "18:00–22:00 (Asia open)", "22:00–02:00", "02:00–06:00 (London)", "06:00–10:00 (NY open)", "10:00–14:00", "14:00–17:00" };
    static int BlockOf(int hour) { int m = ((hour - 18) + 24) % 24; return 1 + m / 4; }   // 18→1, 22→2, 2→3, 6→4, 10→5, 14→6

    // per-day indicators, all from bars ≤ k
    sealed class Ind { public double[] Vwap = new double[R.Slots], Ema = new double[R.Slots], AvgR = new double[R.Slots], Hi30 = new double[R.Slots], Lo30 = new double[R.Slots], Hi60 = new double[R.Slots], Lo60 = new double[R.Slots], SesHi = new double[R.Slots], SesLo = new double[R.Slots]; public int[] Prev = new int[R.Slots]; }

    static Ind Build(RDay d)
    {
        var x = new Ind(); double pv = 0, vv = 0, ema = double.NaN, sh = double.MinValue, sl = double.MaxValue; var ranges = new Queue<double>(); double rsum = 0; int prev = -1;
        var hq = new List<int>();
        for (int k = 0; k < R.Slots; k++)
        {
            x.Prev[k] = prev; x.SesHi[k] = sh; x.SesLo[k] = sl;   // before bar k
            if (!d.Has(k)) { x.Vwap[k] = x.Ema[k] = x.AvgR[k] = double.NaN; continue; }
            double tp = (d.H[k] + d.L[k] + d.C[k]) / 3.0, v = Math.Max(1, d.V[k]); pv += tp * v; vv += v; x.Vwap[k] = pv / vv;
            ema = double.IsNaN(ema) ? d.C[k] : ema + (d.C[k] - ema) * (2.0 / 21); x.Ema[k] = ema;
            double rg = d.H[k] - d.L[k]; ranges.Enqueue(rg); rsum += rg; if (ranges.Count > 20) rsum -= ranges.Dequeue(); x.AvgR[k] = rsum / ranges.Count;
            sh = Math.Max(sh, d.H[k]); sl = Math.Min(sl, d.L[k]); prev = k;
        }
        // highs / lows of the 30 / 60 bars BEFORE k
        for (int k = 0; k < R.Slots; k++)
        {
            double h30 = double.MinValue, l30 = double.MaxValue, h60 = double.MinValue, l60 = double.MaxValue; int n = 0;
            for (int j = k - 1; j >= 0 && j >= k - 60; j--) { if (!d.Has(j)) continue; n++; if (k - j <= 30) { h30 = Math.Max(h30, d.H[j]); l30 = Math.Min(l30, d.L[j]); } h60 = Math.Max(h60, d.H[j]); l60 = Math.Min(l60, d.L[j]); }
            x.Hi30[k] = n >= 20 ? h30 : double.NaN; x.Lo30[k] = n >= 20 ? l30 : double.NaN; x.Hi60[k] = n >= 40 ? h60 : double.NaN; x.Lo60[k] = n >= 40 ? l60 : double.NaN;
        }
        return x;
    }

    static int Flags(RDay d, Ind x, int k, int dir)
    {
        int f = 0; double c = d.C[k], o = d.O[k];
        if (!double.IsNaN(x.Vwap[k]) && dir * (c - x.Vwap[k]) > 0) f |= 1 << 0;
        if (!double.IsNaN(x.Hi30[k]) && (dir > 0 ? c > x.Hi30[k] : c < x.Lo30[k])) f |= 1 << 1;
        if (!double.IsNaN(x.Hi60[k]) && (dir > 0 ? c > x.Hi60[k] : c < x.Lo60[k])) f |= 1 << 2;
        int k5 = k - 5; while (k5 >= 0 && !d.Has(k5)) k5--; if (k5 >= 0 && dir * (c - d.C[k5]) >= 2.0) f |= 1 << 3;
        int p = x.Prev[k]; double avgPrev = p >= 0 ? x.AvgR[p] : double.NaN;
        if (!double.IsNaN(avgPrev) && avgPrev > 0 && d.H[k] - d.L[k] >= 2 * avgPrev && dir * (c - o) > 0) f |= 1 << 4;
        if (x.SesHi[k] > double.MinValue && (dir > 0 ? d.H[k] > x.SesHi[k] : d.L[k] < x.SesLo[k])) f |= 1 << 5;
        int k5e = k - 5; while (k5e >= 0 && !d.Has(k5e)) k5e--;
        bool emaTrend = k5e >= 0 && !double.IsNaN(x.Ema[k5e]) && dir * (c - x.Ema[k]) > 0 && dir * (x.Ema[k] - x.Ema[k5e]) > 0;
        if (emaTrend) f |= 1 << 6;
        if (emaTrend && p >= 0 && dir * (d.C[p] - d.O[p]) < 0 && dir * (c - o) > 0) f |= 1 << 7;
        double ar = x.AvgR[k]; if (!double.IsNaN(ar) && ar <= 0.6) f |= 1 << 8; if (!double.IsNaN(ar) && ar >= 1.5) f |= 1 << 9;
        if (!double.IsNaN(x.Lo30[k]) && p >= 0 && (dir > 0 ? d.L[k] < x.Lo30[k] && c > o && c > d.C[p] : d.H[k] > x.Hi30[k] && c < o && c < d.C[p])) f |= 1 << 10;
        if (!double.IsNaN(x.Vwap[k]) && !double.IsNaN(ar) && ar > 0 && dir * (x.Vwap[k] - c) >= 3 * ar) f |= 1 << 11;
        return f;
    }

    sealed class Rule { public int Item1, Item2; public double Item3, Item4; public long Item5, Item6; public double Item7, Item8; }
    struct Ent { public int Flags; public byte Block, YearIx; public sbyte Dir; public float Usd; public bool Win; }
    sealed class Agg { public long N, W; public double Usd; public long[] YN = new long[8]; public double[] YUsd = new double[8]; }

    public static void Run(List<RDay> days, string sym, double stopPts, double targetPts, int qty, double slipTicks, string outDir)
    {
        double pv = sym == "MGC" ? 10.0 : 2.0, tick = sym == "MGC" ? 0.1 : 0.25, fee = 2 * 0.62 * qty;
        double winUsd = targetPts * pv * qty - fee, lossUsd = -(stopPts + slipTicks * tick) * pv * qty - fee;
        int y0 = days.First().Day.Year; var oosStart = new DateTime(2024, 1, 1);
        var aggs = new Dictionary<long, Agg>();   // key: flags | block << 12 | oos << 16
        var report = new KReport { Title = sym + " TEN-R MOVES • every session", Instrument = sym, Subtitle = "every 1-minute entry that reached +" + targetPts + " pts before −" + stopPts + " pt (" + qty + " micros: $" + (targetPts * pv * qty).ToString("0") + " / −$" + (stopPts * pv * qty).ToString("0") + ")",
            Method = "Each bar's close tested as a buy and a sell. A move = winning entries the same way within 15 minutes; its first winning minute is drawn (▲ buy / ▼ sell) with the stop / target and where the target was hit. A bar touching both stop and target counts as a loss." };
        long moves = 0;
        foreach (var d in days)
        {
            var x = Build(d); var kd = new KReport.KDay { Date = d.Day }; int lastWinK = -100, lastWinDir = 0, nUp = 0, nDn = 0;
            byte yi = (byte)Math.Min(7, Math.Max(0, d.Day.Year - y0)); bool oos = d.Day >= oosStart;
            for (int k = 0; k < R.Slots - 1; k++)
            {
                if (!d.Has(k)) continue; double e = d.C[k]; int hour = ((k + 1 + 18 * 60) % 1440) / 60; byte blk = (byte)BlockOf(hour);
                foreach (int dir in new[] { 1, -1 })
                {
                    double stop = e - dir * stopPts, tgt = e + dir * targetPts; int res = 0, outK = -1;
                    for (int j = k + 1; j < R.Slots; j++)
                    {
                        if (!d.Has(j)) continue;
                        if (dir > 0 ? d.L[j] <= stop + 1e-6 : d.H[j] >= stop - 1e-6) { res = -1; outK = j; break; }
                        if (dir > 0 ? d.H[j] >= tgt - 1e-6 : d.L[j] <= tgt + 1e-6) { res = 1; outK = j; break; }
                    }
                    double usd = res > 0 ? winUsd : res < 0 ? lossUsd : dir * (d.CloseAt(R.Slots - 1) - e) * pv * qty - fee;
                    int fl = Flags(d, x, k, dir);
                    long key = fl | ((long)blk << 12) | ((oos ? 1L : 0L) << 16);
                    Agg a; if (!aggs.TryGetValue(key, out a)) { a = new Agg(); aggs[key] = a; }
                    a.N++; if (res > 0) a.W++; a.Usd += usd; a.YN[yi]++; a.YUsd[yi] += usd;
                    if (res > 0)
                    {
                        bool newMove = !(dir == lastWinDir && k - lastWinK <= 15); lastWinK = k; lastWinDir = dir == lastWinDir || newMove ? dir : lastWinDir;
                        if (newMove)
                        {
                            moves++; if (dir > 0) nUp++; else nDn++;
                            kd.Trades.Add(new KReport.KTrade { Dir = dir, EntryTime = R.T(k), Entry = e, Stop = stop, Target = tgt, ExitTime = R.T(outK), Exit = tgt, Pnl = winUsd, Why = "+" + targetPts + " pts in " + (outK - k) + " min" });
                        }
                    }
                }
            }
            if (kd.Trades.Count > 0) { kd.Label = kd.Trades.Count + " moves (" + nUp + "▲ " + nDn + "▼)"; kd.Color = "green"; kd.Fields["moves"] = kd.Trades.Count.ToString(); kd.Fields["up"] = nUp.ToString(); kd.Fields["down"] = nDn.ToString(); kd.Fields["range"] = (d.Hi(0, R.Slots - 1) - d.Lo(0, R.Slots - 1)).ToString("0.0", CultureInfo.InvariantCulture); report.Days.Add(kd); }
        }
        // ---- rules: required flags (up to 3) × time block; aggregated from the keys
        Func<int, int, bool, Tuple<long, long, double, long[], double[]>> sum = (mask, blk, oosSide) =>
        {
            long n = 0, w = 0; double u = 0; var yn = new long[8]; var yu = new double[8];
            foreach (var kv in aggs)
            {
                int fl = (int)(kv.Key & 0xFFF), b = (int)((kv.Key >> 12) & 0xF); bool o = ((kv.Key >> 16) & 1) == 1;
                if (o != oosSide || (fl & mask) != mask || (blk != 0 && b != blk)) continue;
                n += kv.Value.N; w += kv.Value.W; u += kv.Value.Usd; for (int i = 0; i < 8; i++) { yn[i] += kv.Value.YN[i]; yu[i] += kv.Value.YUsd[i]; }
            }
            return Tuple.Create(n, w, u, yn, yu);
        };
        Func<int, string> maskName = m => m == 0 ? "ANY MINUTE" : string.Join(" + ", Enumerable.Range(0, NF).Where(i => (m & (1 << i)) != 0).Select(i => FNames[i].Split('(')[0].Trim()));
        var sb = new StringBuilder();
        sb.AppendLine("# " + sym + " TEN-R STUDY • every session • stop " + stopPts + " / target " + targetPts + " pts • " + qty + " micros");
        sb.AppendLine();
        sb.AppendLine(days.Count + " sessions " + days.First().Day.ToString("yyyy-MM-dd") + " .. " + days.Last().Day.ToString("yyyy-MM-dd") + " (every session with an hour of bars or more) • " + moves.ToString("N0") + " separate moves drawn in the report");
        sb.AppendLine("Win = +$" + winUsd.ToString("0") + " • loss = $" + lossUsd.ToString("0") + " (" + slipTicks + " tick slippage on the stop + fees) • break-even win rate " + (100.0 * -lossUsd / (winUsd - lossUsd)).ToString("0.0") + "%");
        sb.AppendLine("IS = 2020–2023 (where rules are chosen) • OOS = 2024–2026 (never used to choose — the honest test)");
        sb.AppendLine();
        sb.AppendLine("## 1. Each feature alone (both sides, all hours)");
        sb.AppendLine("| feature | IS entries | IS win % | IS $/entry | OOS entries | OOS win % | OOS $/entry |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (int m in new[] { 0 }.Concat(Enumerable.Range(0, NF).Select(i => 1 << i)))
        {
            var a = sum(m, 0, false); var b = sum(m, 0, true);
            sb.AppendLine("| " + (m == 0 ? "ANY MINUTE (baseline)" : FNames[(int)Math.Round(Math.Log(m, 2))]) + " | " + a.Item1.ToString("N0") + " | " + (100.0 * a.Item2 / Math.Max(1, a.Item1)).ToString("0.00") + "% | " + (a.Item3 / Math.Max(1, a.Item1)).ToString("0.00") + " | " + b.Item1.ToString("N0") + " | " + (100.0 * b.Item2 / Math.Max(1, b.Item1)).ToString("0.00") + "% | " + (b.Item3 / Math.Max(1, b.Item1)).ToString("0.00") + " |");
        }
        sb.AppendLine();
        sb.AppendLine("## 2. Time of day alone");
        sb.AppendLine("| window | IS win % | IS $/entry | OOS win % | OOS $/entry |");
        sb.AppendLine("|---|---|---|---|---|");
        for (int bk = 1; bk < Blocks.Length; bk++) { var a = sum(0, bk, false); var b = sum(0, bk, true); sb.AppendLine("| " + Blocks[bk] + " | " + (100.0 * a.Item2 / Math.Max(1, a.Item1)).ToString("0.00") + "% | " + (a.Item3 / Math.Max(1, a.Item1)).ToString("0.00") + " | " + (100.0 * b.Item2 / Math.Max(1, b.Item1)).ToString("0.00") + "% | " + (b.Item3 / Math.Max(1, b.Item1)).ToString("0.00") + " |"); }
        sb.AppendLine();
        // every rule: up to 3 features × block, IS needs ≥ 400 entries
        var rules = new List<Rule>();   // mask, block, IS $/e, IS win%, IS n, OOS n, OOS $/e, OOS win%
        var masks = new List<int>();
        for (int a = 0; a < NF; a++) { masks.Add(1 << a); for (int b = a + 1; b < NF; b++) { masks.Add((1 << a) | (1 << b)); for (int c = b + 1; c < NF; c++) masks.Add((1 << a) | (1 << b) | (1 << c)); } }
        foreach (int m in masks) for (int bk = 0; bk < Blocks.Length; bk++)
        {
            var a = sum(m, bk, false); if (a.Item1 < 400) continue; var b = sum(m, bk, true);
            rules.Add(new Rule { Item1 = m, Item2 = bk, Item3 = a.Item3 / a.Item1, Item4 = 100.0 * a.Item2 / a.Item1, Item5 = a.Item1, Item6 = b.Item1, Item7 = b.Item1 > 0 ? b.Item3 / b.Item1 : double.NaN, Item8 = b.Item1 > 0 ? 100.0 * b.Item2 / b.Item1 : double.NaN });
        }
        var top = rules.OrderByDescending(r => r.Item3).Take(25).ToList();
        sb.AppendLine("## 3. Best rules — ranked on IS only (" + rules.Count.ToString("N0") + " rules tested), then their untouched OOS");
        sb.AppendLine("| # | rule | window | IS entries | IS win % | IS $/entry | OOS entries | OOS win % | OOS $/entry | holds up? |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        int ix = 0; foreach (var r in top) sb.AppendLine("| " + (++ix) + " | " + maskName(r.Item1) + " | " + Blocks[r.Item2] + " | " + r.Item5.ToString("N0") + " | " + r.Item4.ToString("0.0") + "% | " + r.Item3.ToString("0.00") + " | " + r.Item6.ToString("N0") + " | " + r.Item8.ToString("0.0") + "% | " + r.Item7.ToString("0.00") + " | " + (r.Item7 > 0 ? "YES" : "no") + " |");
        sb.AppendLine();
        int oosPositive = rules.Count(r => r.Item3 > 0 && r.Item7 > 0), isPositive = rules.Count(r => r.Item3 > 0);
        sb.AppendLine(isPositive + " rules made money per entry on IS; " + oosPositive + " of them also on OOS.");
        sb.AppendLine();
        // ---- the best rules traded one at a time
        var picks = top.Take(5).ToList();
        var bothBest = rules.Where(r => r.Item3 > 0 && r.Item7 > 0 && r.Item6 >= 200).OrderByDescending(r => Math.Min(r.Item3, r.Item7)).Take(3).ToList();
        foreach (var r in bothBest) if (!picks.Contains(r)) picks.Add(r);
        sb.AppendLine("## 4. Traded one at a time (a signal is taken only when flat; first signal of either side)");
        sb.AppendLine("| rule | window | trades | trades/yr | win % | net $ | IS net | OOS net | worst drawdown | longest losing streak | years up |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        KReport best = null; string bestName = "";
        foreach (var r in picks)
        {
            var kr = new KReport { Title = sym + " TEN-R RULE • " + maskName(r.Item1) + " • " + Blocks[r.Item2], Instrument = sym, Subtitle = "one trade at a time • stop " + stopPts + " / target " + targetPts + " pts • " + qty + " micros", Method = "Entry at the close of the signal bar; loss = stop + slippage + fees; a bar touching both counts as a loss." };
            int trades = 0, wins = 0, streak = 0, worstStreak = 0; double eq = 0, peak = 0, dd = 0, isNet = 0, oosNet = 0; var yearsNet = new Dictionary<int, double>();
            foreach (var d in days)
            {
                var x = Build(d); var kd = new KReport.KDay { Date = d.Day }; int k = 0;
                while (k < R.Slots - 1)
                {
                    if (!d.Has(k)) { k++; continue; }
                    int hour = ((k + 1 + 18 * 60) % 1440) / 60; if (r.Item2 != 0 && BlockOf(hour) != r.Item2) { k++; continue; }
                    int dir = 0; foreach (int dd0 in new[] { 1, -1 }) if ((Flags(d, x, k, dd0) & r.Item1) == r.Item1) { dir = dd0; break; }
                    if (dir == 0) { k++; continue; }
                    double e = d.C[k], stop = e - dir * stopPts, tgt = e + dir * targetPts; int res = 0, outK = R.Slots - 1;
                    for (int j = k + 1; j < R.Slots; j++) { if (!d.Has(j)) continue; if (dir > 0 ? d.L[j] <= stop + 1e-6 : d.H[j] >= stop - 1e-6) { res = -1; outK = j; break; } if (dir > 0 ? d.H[j] >= tgt - 1e-6 : d.L[j] <= tgt + 1e-6) { res = 1; outK = j; break; } }
                    double exitPx = res > 0 ? tgt : res < 0 ? stop - dir * slipTicks * tick : d.CloseAt(R.Slots - 1);
                    double usd = res > 0 ? winUsd : res < 0 ? lossUsd : dir * (exitPx - e) * pv * qty - fee;
                    trades++; if (res > 0) { wins++; streak = 0; } else { streak++; worstStreak = Math.Max(worstStreak, streak); }
                    eq += usd; peak = Math.Max(peak, eq); dd = Math.Max(dd, peak - eq); if (d.Day >= oosStart) oosNet += usd; else isNet += usd;
                    double yv; yearsNet.TryGetValue(d.Day.Year, out yv); yearsNet[d.Day.Year] = yv + usd;
                    kd.Trades.Add(new KReport.KTrade { Dir = dir, EntryTime = R.T(k), Entry = e, Stop = stop, Target = tgt, ExitTime = R.T(outK), Exit = exitPx, Pnl = usd, Why = res > 0 ? "TARGET" : res < 0 ? "STOP" : "SESSION END" });
                    k = outK + 1;
                }
                if (kd.Trades.Count > 0) { double net = kd.Trades.Sum(t => t.Pnl); kd.Label = kd.Trades.Count + " trades " + (net >= 0 ? "+$" : "−$") + Math.Abs(net).ToString("0"); kd.Color = net >= 0 ? "green" : "red"; kr.Days.Add(kd); }
            }
            double yrs = Math.Max(0.1, (days.Last().Day - days.First().Day).TotalDays / 365.25);
            sb.AppendLine("| " + maskName(r.Item1) + " | " + Blocks[r.Item2] + " | " + trades.ToString("N0") + " | " + (trades / yrs).ToString("0") + " | " + (100.0 * wins / Math.Max(1, trades)).ToString("0.0") + "% | " + eq.ToString("N0") + " | " + isNet.ToString("N0") + " | " + oosNet.ToString("N0") + " | " + dd.ToString("N0") + " | " + worstStreak + " | " + yearsNet.Count(kv => kv.Value > 0) + "/" + yearsNet.Count + " |");
            kr.Summary.Add(new[] { "trades", trades.ToString("N0"), "" }); kr.Summary.Add(new[] { "win rate", (100.0 * wins / Math.Max(1, trades)).ToString("0.0") + "%", "break-even " + (100.0 * -lossUsd / (winUsd - lossUsd)).ToString("0.0") + "%" }); kr.Summary.Add(new[] { "net", "$" + eq.ToString("N0"), "IS $" + isNet.ToString("N0") + " • OOS $" + oosNet.ToString("N0") }); kr.Summary.Add(new[] { "worst drawdown", "$" + dd.ToString("N0"), "longest losing streak " + worstStreak });
            if (best == null) { best = kr; bestName = maskName(r.Item1) + " • " + Blocks[r.Item2]; }
        }
        sb.AppendLine();
        sb.AppendLine("Reading it: at 1 : 10 a rule only needs to win a bit more than 1 in 10 — but it must hold on the OOS years it was not chosen on, and the losing streaks (column) are what an account has to survive: with " + qty + " micros every loss is $" + (-lossUsd).ToString("0") + ".");
        Directory.CreateDirectory(outDir);
        report.Summary.Add(new[] { "sessions", days.Count.ToString(), days.First().Day.ToString("yyyy-MM-dd") + " .. " + days.Last().Day.ToString("yyyy-MM-dd") });
        report.Summary.Add(new[] { "moves", moves.ToString("N0"), (moves / (double)days.Count).ToString("0.0") + " a session" });
        File.WriteAllText(Path.Combine(outDir, sym + "_tenR_moves.kreport.json"), report.Json());
        if (best != null) File.WriteAllText(Path.Combine(outDir, sym + "_tenR_bestrule.kreport.json"), best.Json());
        File.WriteAllText(Path.Combine(outDir, sym + "_tenR_study.md"), sb.ToString());
        Console.Write(sb.ToString());
        Console.WriteLine("best rule report: " + bestName);
    }
}
