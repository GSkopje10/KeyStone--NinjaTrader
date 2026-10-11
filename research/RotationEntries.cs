// COMBINED ROTATION ENTRIES (MNQ + MGC at the same time): for every session and entry time, which sides (BUY-BUY, SELL-SELL,
// BUY MNQ + SELL MGC, SELL MNQ + BUY MGC) and which confirmation known at the entry reach a $ target before a $ stop — the
// prop day targets $1,500 (pass in 2 days) and $900 (3+ days) against their stops — compared with MNQ alone and MGC alone.
// Real minute prices only: the stop is checked first with both legs at their worst of the minute (conservative), a target
// needs the combined P/L at a minute's close (filled at the target), costs per contract (commission + 1 tick per side).
// Rules are chosen on Oct 2020–2023 and judged on 2024–26, against a shuffle null; the best go through the prop engine.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;

public static class RotationEntries
{
    public sealed class Session { public string Name; public int Start, End; public int[] Offsets; }
    public static readonly Session[] Sessions =
    {
        new Session { Name = "ASIA 20:00–00:00", Start = R.S(20, 0), End = R.S(23, 59), Offsets = new[] { 0, 30, 60, 120 } },
        new Session { Name = "LONDON 03:00–07:00", Start = R.S(3, 0), End = R.S(6, 59), Offsets = new[] { 0, 30, 60, 120 } },
        new Session { Name = "NY PRE 08:00–09:29", Start = R.S(8, 0), End = R.S(9, 29), Offsets = new[] { 0, 15, 30, 45 } },
        new Session { Name = "NY OPEN 09:30–11:00", Start = R.S(9, 30), End = R.S(10, 59), Offsets = new[] { 0, 5, 15, 30 } },
        new Session { Name = "NY MID 11:00–13:30", Start = R.S(11, 0), End = R.S(13, 29), Offsets = new[] { 0, 30, 60, 90 } },
        new Session { Name = "NY CLOSE 13:30–15:55", Start = R.S(13, 30), End = R.S(15, 54), Offsets = new[] { 0, 30, 60, 90 } },
    };
    // $ target / $ stop pairs: the prop day targets 1,500 and 900 with tight / medium / even stops
    public static readonly double[][] Brackets = { new double[] { 1500, 500 }, new double[] { 1500, 750 }, new double[] { 1500, 1500 }, new double[] { 900, 300 }, new double[] { 900, 450 }, new double[] { 900, 900 } };
    // the instruments: both (two sizes), MNQ alone, MGC alone — contracts
    public static readonly string[] Sets = { "BOTH 10+10", "BOTH 5+5", "MNQ 20", "MGC 10" };
    static readonly int[,] SetQty = { { 10, 10 }, { 5, 5 }, { 20, 0 }, { 0, 10 } };
    public static readonly string[] Pairs = { "BB", "SS", "BS", "SB" };   // MNQ side first
    static readonly int[,] PairDir = { { 1, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 } };

    public sealed class Day
    {
        public RDay M, G; public KeystoneRotDay R; public DateTime Date { get { return M.Day; } }
        public static Day Of(RDay m, RDay g)
        {
            var r = new KeystoneRotDay { Day = m.Day }; Array.Copy(m.O, r.MO, KeystoneRotDay.Slots); Array.Copy(m.H, r.MH, KeystoneRotDay.Slots); Array.Copy(m.L, r.ML, KeystoneRotDay.Slots); Array.Copy(m.C, r.MC, KeystoneRotDay.Slots);
            Array.Copy(g.O, r.GO, KeystoneRotDay.Slots); Array.Copy(g.H, r.GH, KeystoneRotDay.Slots); Array.Copy(g.L, r.GL, KeystoneRotDay.Slots); Array.Copy(g.C, r.GC, KeystoneRotDay.Slots); return new Day { M = m, G = g, R = r };
        }
    }
    public sealed class Entry { public Day D; public int S, Off, K; }   // session, offset index, entry slot
    public sealed class PRes { public int Days, Trades, Wins, Bought, Passed, BlownEval, BlownFunded, Payouts, AcctDays, AtTarget; public double PassRate, Cash, Spent, Net, Worst, AvgPassDays; }

    // one combined trade from slot k (the engine's: stop first with both legs at their worst, target at a minute close, costs)
    public static double Trade(Day d, int k, int end, int dM, int dG, int qM, int qG, double target, double stop, out int how, out int exitSlot)
    {
        double pk, ws; return KeystoneRotationLab.Trade(d.R, k, end, dM, dG, qM, qG, target, stop, 0, null, out how, out exitSlot, out pk, out ws);
    }

    // ---- confirmations known at the entry (closes up to k − 1): each returns (MNQ side, MGC side), (0, 0) = no trade
    public sealed class Rule { public string Name, Family; public Func<Day, int, int, Tuple<int, int>> Pick; }
    static double Mom(RDay x, int k, int n) { double a = x.CloseAt(k - 1), b = x.CloseAt(k - 1 - n); return double.IsNaN(a) || double.IsNaN(b) ? 0 : a - b; }
    static int Sg(double v) { return v > 0 ? 1 : v < 0 ? -1 : 0; }
    public static List<Rule> Rules()
    {
        var l = new List<Rule>(); Func<int, int, Tuple<int, int>> T = (a, b) => Tuple.Create(a, b);
        for (int p = 0; p < 4; p++) { int pp = p; l.Add(new Rule { Name = "ALWAYS " + Pairs[p], Family = "STATIC", Pick = (d, k, s0) => T(PairDir[pp, 0], PairDir[pp, 1]) }); }
        foreach (int n in new[] { 5, 15, 30, 60, 120 })
        {
            int nn = n;
            l.Add(new Rule { Name = "FOLLOW " + n + "m • each its own", Family = "MOMENTUM", Pick = (d, k, s0) => T(Sg(Mom(d.M, k, nn)), Sg(Mom(d.G, k, nn))) });
            l.Add(new Rule { Name = "FADE " + n + "m • each its own", Family = "MOMENTUM", Pick = (d, k, s0) => T(-Sg(Mom(d.M, k, nn)), -Sg(Mom(d.G, k, nn))) });
            l.Add(new Rule { Name = "FOLLOW " + n + "m • only when both agree", Family = "MOMENTUM", Pick = (d, k, s0) => { int a = Sg(Mom(d.M, k, nn)), b = Sg(Mom(d.G, k, nn)); return a != 0 && a == b ? T(a, b) : T(0, 0); } });
            l.Add(new Rule { Name = "FADE " + n + "m • only when both agree", Family = "MOMENTUM", Pick = (d, k, s0) => { int a = Sg(Mom(d.M, k, nn)), b = Sg(Mom(d.G, k, nn)); return a != 0 && a == b ? T(-a, -b) : T(0, 0); } });
            l.Add(new Rule { Name = "NQ LEADS " + n + "m (both follow MNQ)", Family = "LEAD", Pick = (d, k, s0) => { int a = Sg(Mom(d.M, k, nn)); return T(a, a); } });
            l.Add(new Rule { Name = "GOLD LEADS " + n + "m (both follow MGC)", Family = "LEAD", Pick = (d, k, s0) => { int b = Sg(Mom(d.G, k, nn)); return T(b, b); } });
        }
        foreach (int n in new[] { 30, 60, 120 })
        {
            int nn = n;
            // relative strength (moves in units of each one's typical day): buy the stronger, sell the weaker — or the reverse
            l.Add(new Rule { Name = "STRONGER vs WEAKER " + n + "m (buy strong, sell weak)", Family = "PAIR", Pick = (d, k, s0) => { double a = Mom(d.M, k, nn) / Math.Max(1e-9, d.M.RthAtr), b = Mom(d.G, k, nn) / Math.Max(1e-9, d.G.RthAtr); return a == b ? T(0, 0) : a > b ? T(1, -1) : T(-1, 1); } });
            l.Add(new Rule { Name = "WEAKER vs STRONGER " + n + "m (buy weak, sell strong)", Family = "PAIR", Pick = (d, k, s0) => { double a = Mom(d.M, k, nn) / Math.Max(1e-9, d.M.RthAtr), b = Mom(d.G, k, nn) / Math.Max(1e-9, d.G.RthAtr); return a == b ? T(0, 0) : a > b ? T(-1, 1) : T(1, -1); } });
        }
        l.Add(new Rule { Name = "VWAP side • each follows", Family = "VWAP", Pick = (d, k, s0) => T(Sg(d.M.CloseAt(k - 1) - R.Vwap(d.M, 0, k - 1)), Sg(d.G.CloseAt(k - 1) - R.Vwap(d.G, 0, k - 1))) });
        l.Add(new Rule { Name = "VWAP side • each fades", Family = "VWAP", Pick = (d, k, s0) => T(-Sg(d.M.CloseAt(k - 1) - R.Vwap(d.M, 0, k - 1)), -Sg(d.G.CloseAt(k - 1) - R.Vwap(d.G, 0, k - 1))) });
        l.Add(new Rule { Name = "OVERNIGHT (since 18:00) • each follows", Family = "DAY", Pick = (d, k, s0) => T(Sg(d.M.CloseAt(k - 1) - d.M.OpenAt(0)), Sg(d.G.CloseAt(k - 1) - d.G.OpenAt(0))) });
        l.Add(new Rule { Name = "OVERNIGHT (since 18:00) • each fades", Family = "DAY", Pick = (d, k, s0) => T(-Sg(d.M.CloseAt(k - 1) - d.M.OpenAt(0)), -Sg(d.G.CloseAt(k - 1) - d.G.OpenAt(0))) });
        Func<RDay, int> prevDir = x => x.Prev == null ? 0 : Sg(x.Prev.CloseAt(R.S(15, 59)) - x.Prev.OpenAt(R.S(9, 30)));
        l.Add(new Rule { Name = "YESTERDAY • each follows", Family = "DAY", Pick = (d, k, s0) => T(prevDir(d.M), prevDir(d.G)) });
        l.Add(new Rule { Name = "YESTERDAY • each fades", Family = "DAY", Pick = (d, k, s0) => T(-prevDir(d.M), -prevDir(d.G)) });
        l.Add(new Rule { Name = "SESSION SO FAR • each follows", Family = "SESSION", Pick = (d, k, s0) => k <= s0 ? T(0, 0) : T(Sg(d.M.CloseAt(k - 1) - d.M.OpenAt(s0)), Sg(d.G.CloseAt(k - 1) - d.G.OpenAt(s0))) });
        l.Add(new Rule { Name = "SESSION SO FAR • each fades", Family = "SESSION", Pick = (d, k, s0) => k <= s0 ? T(0, 0) : T(-Sg(d.M.CloseAt(k - 1) - d.M.OpenAt(s0)), -Sg(d.G.CloseAt(k - 1) - d.G.OpenAt(s0))) });
        return l;
    }

    static string J(string s) { var b = new StringBuilder("\""); foreach (char ch in s ?? "") { if (ch == '"' || ch == '\\') b.Append('\\').Append(ch); else if (ch < 32) b.Append(' '); else b.Append(ch); } return b.Append('"').ToString(); }
    static string Jn(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 3).ToString(CultureInfo.InvariantCulture); }
    static string F(double v, string f = "0.0") { return v.ToString(f, CultureInfo.InvariantCulture); }

    public static void Run(List<RDay> mnq, List<RDay> mgc, string outDir)
    {
        Directory.CreateDirectory(outDir); var log = new StringBuilder(); Action<string> say = s0 => { Console.WriteLine(s0); log.AppendLine(s0); };
        var gByDay = mgc.ToDictionary(x => x.Day, x => x);
        var days = mnq.Where(x => gByDay.ContainsKey(x.Day)).Select(x => Day.Of(x, gByDay[x.Day])).OrderBy(x => x.Date).ToList();
        say("ROTATION ENTRIES • MNQ + MGC • " + days.Count + " days with both • " + days[0].Date.ToString("yyyy-MM-dd") + " → " + days[days.Count - 1].Date.ToString("yyyy-MM-dd"));
        var entries = new List<Entry>();
        foreach (var d in days) for (int s = 0; s < Sessions.Length; s++) for (int o = 0; o < Sessions[s].Offsets.Length; o++) { int k = Sessions[s].Start + Sessions[s].Offsets[o]; if (k < Sessions[s].End - 10 && d.M.Has(k) && d.G.Has(k)) entries.Add(new Entry { D = d, S = s, Off = o, K = k }); }
        say("  entry points: " + entries.Count);
        // outcomes: [entry][set][pair][bracket] → net $ and how (+1 target, −1 stop, 0 time)
        int E = entries.Count, NS = Sets.Length, NB = Brackets.Length; var t0 = DateTime.Now;
        var net = new float[E, NS, 4, NB]; var how = new sbyte[E, NS, 4, NB];
        Parallel.For(0, E, e =>
        {
            var en = entries[e]; int end = Sessions[en.S].End;
            for (int st = 0; st < NS; st++) for (int p = 0; p < 4; p++)
            {
                int qM = SetQty[st, 0], qG = SetQty[st, 1];
                // a single instrument has only 2 sides: pairs 0 (BUY) and 1 (SELL); 2 / 3 repeat them for the lookups
                int dM = PairDir[p, 0], dG = PairDir[p, 1]; if (qG == 0) { dM = p == 0 || p == 2 ? 1 : -1; dG = 0; } if (qM == 0) { dG = p == 0 || p == 3 ? 1 : -1; dM = 0; }
                for (int b = 0; b < NB; b++) { int hw, xs; net[e, st, p, b] = (float)Trade(en.D, en.K, end, dM, dG, qM, qG, Brackets[b][0], Brackets[b][1], out hw, out xs); how[e, st, p, b] = (sbyte)hw; }
            }
        });
        say("  outcomes: " + E + " × " + NS + " × 4 × " + NB + " in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        var isMask = entries.Select(x => x.D.Date < R.OosStart).ToArray();
        // the pair index of a rule's sides for a set (single instruments: its own leg's side)
        Func<int, int, int, int> pairOf = (st, dM, dG) =>
        {
            if (SetQty[st, 1] == 0) return dM == 0 ? -1 : dM > 0 ? 0 : 1;
            if (SetQty[st, 0] == 0) return dG == 0 ? -1 : dG > 0 ? 0 : 1;
            if (dM == 0 || dG == 0) return -1; return dM > 0 ? (dG > 0 ? 0 : 2) : (dG > 0 ? 3 : 1);
        };
        var rules = Rules(); int NR = rules.Count;
        var picks = new Tuple<int, int>[NR, E];
        Parallel.For(0, NR, r => { for (int e = 0; e < E; e++) picks[r, e] = rules[r].Pick(entries[e].D, entries[e].K, Sessions[entries[e].S].Start); });
        // ---- every config: rule × session × offset × set × bracket
        var cfgs = new List<int[]>();   // rule, session, offset, set, bracket
        for (int r = 0; r < NR; r++) for (int s = 0; s < Sessions.Length; s++) for (int o = 0; o < Sessions[s].Offsets.Length; o++) for (int st = 0; st < NS; st++) for (int b = 0; b < NB; b++) cfgs.Add(new[] { r, s, o, st, b });
        var byCell = new Dictionary<int, List<int>>(); for (int e = 0; e < E; e++) { int key = entries[e].S * 10 + entries[e].Off; List<int> l; if (!byCell.TryGetValue(key, out l)) byCell[key] = l = new List<int>(); l.Add(e); }
        Func<int[], bool?, int[], double[]> stat = (c, isOnly, perm) =>
        {
            // n, wins (target), stops, avg $, t, total, n time exits
            var cell = byCell[c[1] * 10 + c[2]]; int n = 0, w = 0, st0 = 0; double sum = 0, ss = 0;
            for (int ii = 0; ii < cell.Count; ii++)
            {
                int e = cell[ii]; if (isOnly.HasValue && isMask[e] != isOnly.Value) continue;
                var pk = picks[c[0], perm == null ? e : cell[perm[ii]]]; int p = pairOf(c[3], pk.Item1, pk.Item2); if (p < 0) continue;
                double v = net[e, c[3], p, c[4]]; int h = how[e, c[3], p, c[4]]; if (h == -9) continue;
                n++; if (h == 1) w++; else if (h == -1) st0++; sum += v; ss += v * v;
            }
            if (n < 2) return new double[] { n, w, st0, 0, 0, sum };
            double avg = sum / n, sd = Math.Sqrt(Math.Max(0, (ss - n * avg * avg) / (n - 1)));
            return new double[] { n, w, st0, avg, sd > 0 ? avg / sd * Math.Sqrt(n) : 0, sum };
        };
        t0 = DateTime.Now;
        var isS = new double[cfgs.Count][]; var oosS = new double[cfgs.Count][];
        Parallel.For(0, cfgs.Count, i => { isS[i] = stat(cfgs[i], true, null); oosS[i] = stat(cfgs[i], false, null); });
        say("  configs: " + cfgs.Count + " in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        Func<int, string> cname = i => rules[cfgs[i][0]].Name + " • " + Sessions[cfgs[i][1]].Name + " +" + Sessions[cfgs[i][1]].Offsets[cfgs[i][2]] + "m • " + Sets[cfgs[i][3]] + " • +" + Brackets[cfgs[i][4]][0] + " / −" + Brackets[cfgs[i][4]][1];
        var json = new StringBuilder("{"); Action<string, string> put = (k, v) => { if (json.Length > 1) json.Append(','); json.Append(J(k)).Append(':').Append(v); };
        put("meta", "{\"days\":" + days.Count + ",\"from\":" + J(days[0].Date.ToString("yyyy-MM-dd")) + ",\"to\":" + J(days[days.Count - 1].Date.ToString("yyyy-MM-dd")) + ",\"entries\":" + E + ",\"configs\":" + cfgs.Count
            + ",\"sessions\":[" + string.Join(",", Sessions.Select(x => "{\"name\":" + J(x.Name) + ",\"offsets\":[" + string.Join(",", x.Offsets) + "]}")) + "],\"sets\":[" + string.Join(",", Sets.Select(J)) + "],\"brackets\":[" + string.Join(",", Brackets.Select(b => "[" + b[0] + "," + b[1] + "]")) + "]"
            + ",\"rules\":[" + string.Join(",", rules.Select(r => "{\"name\":" + J(r.Name) + ",\"family\":" + J(r.Family) + "}")) + "],\"generated\":" + J(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")) + "}");

        // ---- 1. the static sides on every session (all years): hit rate of the target, $ per trade — the baseline map
        var map = new List<string>();
        for (int s = 0; s < Sessions.Length; s++) for (int st = 0; st < NS; st++) for (int b = 0; b < NB; b++) for (int p = 0; p < 4; p++)
        {
            if ((SetQty[st, 0] == 0 || SetQty[st, 1] == 0) && p >= 2) continue;
            int n = 0, w = 0, sp = 0; double sum = 0; var yr = new Dictionary<int, double>();
            for (int e = 0; e < E; e++) { if (entries[e].S != s) continue; int h = how[e, st, p, b]; if (h == -9) continue; n++; if (h == 1) w++; else if (h == -1) sp++; sum += net[e, st, p, b]; double y; yr.TryGetValue(entries[e].D.Date.Year, out y); yr[entries[e].D.Date.Year] = y + net[e, st, p, b]; }
            string side = SetQty[st, 1] == 0 ? (p == 0 ? "BUY MNQ" : "SELL MNQ") : SetQty[st, 0] == 0 ? (p == 0 ? "BUY MGC" : "SELL MGC") : Pairs[p];
            map.Add("{\"s\":" + s + ",\"set\":" + st + ",\"b\":" + b + ",\"side\":" + J(side) + ",\"n\":" + n + ",\"hit\":" + Jn(n == 0 ? 0 : 100.0 * w / n) + ",\"stop\":" + Jn(n == 0 ? 0 : 100.0 * sp / n) + ",\"avg\":" + Jn(n == 0 ? 0 : sum / n) + ",\"years\":{" + string.Join(",", yr.OrderBy(x => x.Key).Select(x => J(x.Key.ToString()) + ":" + Jn(x.Value))) + "}}");
        }
        put("staticMap", "[" + string.Join(",", map) + "]");
        // the break-even hit rate of each bracket (time exits aside): stop / (target + stop)
        put("breakEven", "[" + string.Join(",", Brackets.Select(b => Jn(100.0 * b[1] / (b[0] + b[1])))) + "]");

        // ---- 2. the search: best on 2020–23 (n ≥ 120 there), judged on 2024–26
        const int MinN = 120;
        var ranked = Enumerable.Range(0, cfgs.Count).Where(i => isS[i][0] >= MinN && rules[cfgs[i][0]].Family != "STATIC").OrderByDescending(i => isS[i][4]).ToList();
        int pos = ranked.Count(i => isS[i][3] > 0), posBoth = ranked.Count(i => isS[i][3] > 0 && oosS[i][3] > 0);
        say("  rule configs with ≥ " + MinN + " trades 2020–23: " + ranked.Count + " • positive then: " + pos + " • positive 2024–26 too: " + posBoth);
        Func<int, string> cj = i =>
        {
            var c = cfgs[i]; var a = isS[i]; var o = oosS[i]; var all = stat(c, null, null);
            return "{\"i\":" + i + ",\"name\":" + J(cname(i)) + ",\"rule\":" + c[0] + ",\"s\":" + c[1] + ",\"off\":" + Sessions[c[1]].Offsets[c[2]] + ",\"set\":" + c[3] + ",\"b\":" + c[4]
                + ",\"is\":{\"n\":" + a[0] + ",\"hit\":" + Jn(a[0] == 0 ? 0 : 100 * a[1] / a[0]) + ",\"avg\":" + Jn(a[3]) + ",\"t\":" + Jn(a[4]) + "},\"oos\":{\"n\":" + o[0] + ",\"hit\":" + Jn(o[0] == 0 ? 0 : 100 * o[1] / o[0]) + ",\"avg\":" + Jn(o[3]) + ",\"t\":" + Jn(o[4]) + ",\"total\":" + Jn(o[5]) + "}"
                + ",\"all\":{\"n\":" + all[0] + ",\"hit\":" + Jn(all[0] == 0 ? 0 : 100 * all[1] / all[0]) + ",\"stop\":" + Jn(all[0] == 0 ? 0 : 100 * all[2] / all[0]) + ",\"avg\":" + Jn(all[3]) + ",\"t\":" + Jn(all[4]) + "}}";
        };
        put("top", "[" + string.Join(",", ranked.Take(40).Select(cj)) + "]");
        foreach (var i in ranked.Take(12)) say(string.Format("  IS t {0,5:0.00} hit {1,4:0}% avg {2,6:0} | OOS t {3,5:0.00} hit {4,4:0}% avg {5,6:0} n {6,4} | {7}", isS[i][4], 100 * isS[i][1] / isS[i][0], isS[i][3], oosS[i][4], oosS[i][0] == 0 ? 0 : 100 * oosS[i][1] / oosS[i][0], oosS[i][3], oosS[i][0], cname(i)));
        // the best per session and bracket (the table the indicator would use)
        var perSession = new List<string>();
        for (int s = 0; s < Sessions.Length; s++) for (int b = 0; b < NB; b++)
        {
            var best = ranked.Where(i => cfgs[i][1] == s && cfgs[i][4] == b).Take(1).ToList(); if (best.Count == 0) continue;
            perSession.Add(cj(best[0]));
        }
        put("bestPerSession", "[" + string.Join(",", perSession) + "]");
        // the families: how the best of each family does
        var fam = new List<string>();
        foreach (var f in rules.Select(r => r.Family).Distinct().Where(f => f != "STATIC"))
        {
            var l = ranked.Where(i => rules[cfgs[i][0]].Family == f).ToList(); if (l.Count == 0) continue;
            fam.Add("{\"family\":" + J(f) + ",\"configs\":" + l.Count + ",\"posIs\":" + l.Count(i => isS[i][3] > 0) + ",\"posBoth\":" + l.Count(i => isS[i][3] > 0 && oosS[i][3] > 0) + ",\"oosAvgOfPos\":" + Jn(l.Where(i => isS[i][3] > 0).Select(i => oosS[i][3]).DefaultIfEmpty(0).Average()) + ",\"best\":" + cj(l[0]) + "}");
        }
        put("families", "[" + string.Join(",", fam) + "]");

        // ---- 3. luck: shuffle which day's confirmation goes with which day's outcome (inside each session / offset, 2020–23), redo the search
        int perms = 60; var nullMax = new double[perms]; var seeds = new Random(20261011).Let(rg => Enumerable.Range(0, perms).Select(x => rg.Next()).ToArray());
        var ruleCfgs = Enumerable.Range(0, cfgs.Count).Where(i => rules[cfgs[i][0]].Family != "STATIC").ToList(); t0 = DateTime.Now;
        Parallel.For(0, perms, pi =>
        {
            var rng = new Random(seeds[pi]); var permOf = new Dictionary<int, int[]>();
            foreach (var kv in byCell) { int n = kv.Value.Count; var pm = Enumerable.Range(0, n).ToArray(); for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); int tmp = pm[i]; pm[i] = pm[j]; pm[j] = tmp; } permOf[kv.Key] = pm; }
            double mx = double.MinValue;
            foreach (int i in ruleCfgs) { var st = stat(cfgs[i], true, permOf[cfgs[i][1] * 10 + cfgs[i][2]]); if (st[0] >= MinN && st[4] > mx) mx = st[4]; }
            nullMax[pi] = mx;
        });
        Array.Sort(nullMax); double bestT = ranked.Count > 0 ? isS[ranked[0]][4] : 0; double pNull = (1.0 + nullMax.Count(v => v >= bestT)) / (perms + 1);
        say("  luck (" + perms + " shuffles, " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s): best t by luck median " + F(nullMax[perms / 2], "0.00") + " • 95th " + F(nullMax[(int)(perms * 0.95)], "0.00") + " • real " + F(bestT, "0.00") + " → p " + F(pNull, "0.000"));
        put("luck", "{\"perms\":" + perms + ",\"bestT\":" + Jn(bestT) + ",\"med\":" + Jn(nullMax[perms / 2]) + ",\"p95\":" + Jn(nullMax[(int)(perms * 0.95)]) + ",\"p\":" + Jn(pNull) + ",\"dist\":[" + string.Join(",", nullMax.Select(Jn)) + "]}");

        // ---- 4. walk-forward: each year, the config with the best t on the years before, traded that year
        var years = days.Select(x => x.Date.Year).Distinct().OrderBy(y => y).ToList(); var wf = new List<string>(); double wfTot = 0; int wfN = 0, wfW = 0;
        foreach (int y in years.Where(y => y >= 2022))
        {
            int best = -1; double bt = double.MinValue;
            foreach (int i in ruleCfgs)
            {
                var c = cfgs[i]; var cell = byCell[c[1] * 10 + c[2]]; int n = 0; double sum = 0, ss = 0;
                foreach (int e in cell) { if (entries[e].D.Date.Year >= y) continue; var pk = picks[c[0], e]; int p = pairOf(c[3], pk.Item1, pk.Item2); if (p < 0 || how[e, c[3], p, c[4]] == -9) continue; double v = net[e, c[3], p, c[4]]; n++; sum += v; ss += v * v; }
                if (n < MinN) continue; double avg = sum / n, sd = Math.Sqrt(Math.Max(0, (ss - n * avg * avg) / (n - 1))), t = sd > 0 ? avg / sd * Math.Sqrt(n) : 0; if (t > bt) { bt = t; best = i; }
            }
            if (best < 0) continue; var cb = cfgs[best]; double tot = 0; int nn = 0, ww = 0;
            foreach (int e in byCell[cb[1] * 10 + cb[2]]) { if (entries[e].D.Date.Year != y) continue; var pk = picks[cb[0], e]; int p = pairOf(cb[3], pk.Item1, pk.Item2); if (p < 0 || how[e, cb[3], p, cb[4]] == -9) continue; tot += net[e, cb[3], p, cb[4]]; nn++; if (how[e, cb[3], p, cb[4]] == 1) ww++; }
            wfTot += tot; wfN += nn; wfW += ww;
            wf.Add("{\"year\":" + y + ",\"name\":" + J(cname(best)) + ",\"t\":" + Jn(bt) + ",\"n\":" + nn + ",\"hit\":" + Jn(nn == 0 ? 0 : 100.0 * ww / nn) + ",\"usd\":" + Jn(tot) + "}");
            say("  walk-forward " + y + ": " + F(tot, "0") + " $ (" + nn + " trades, hit " + (nn == 0 ? 0 : 100 * ww / nn) + "%) • chosen on < " + y + " (t " + F(bt, "0.0") + "): " + cname(best));
        }
        put("walkForward", "{\"total\":" + Jn(wfTot) + ",\"n\":" + wfN + ",\"hit\":" + Jn(wfN == 0 ? 0 : 100.0 * wfW / wfN) + ",\"years\":[" + string.Join(",", wf) + "]}");

        // ---- 5. prop: the rotation through the studio's engine — each day in the chosen session the account in turn takes the
        // confirmed trade, ONE WIN A DAY per account, the next account trades 2 minutes later; GENERIC 50K rules, 5 accounts
        var candidates = ranked.Take(6).ToList();
        var propRows = new List<string>(); var rules0 = KeystoneSimRules.Presets()[0];
        Func<Func<Day, int, int, Tuple<int, int>>, int, int, int, bool, PRes> propRun = (pick, s, st, b, oos) =>
        {
            var set = new KeystoneSimSettings { Strategy = "ROTATION", Accounts = 5, ReplacePassed = false, DayPlan = "ONEWIN", MaxLossesPerDay = 0, Rules = rules0.Copy(), RotTarget = Brackets[b][0], RotStop = Brackets[b][1], FundRotTarget = Brackets[b][0], FundRotStop = Brackets[b][1] };
            var sim = new KeystonePropSim(set); var res = new PRes(); double low = 0;
            foreach (var d in days)
            {
                if ((d.Date >= R.OosStart) != oos) continue;
                sim.BeginDay(d.Date); int k = Sessions[s].Start, end = Sessions[s].End;
                while (k < end - 5 && sim.Active != null)
                {
                    var pk = pick(d, k, Sessions[s].Start); int qM = SetQty[st, 0], qG = SetQty[st, 1]; int dM = pk.Item1, dG = pk.Item2;
                    if (qG == 0) dG = 0; if (qM == 0) dM = 0; if ((qM > 0 && dM == 0 && qG == 0) || (qG > 0 && dG == 0 && qM == 0) || (qM > 0 && qG > 0 && (dM == 0 || dG == 0))) { k += 1; continue; }
                    int hw, xs; double v = Trade(d, k, end, dM, dG, qM, qG, Brackets[b][0], Brackets[b][1], out hw, out xs); if (hw == -9) { k++; continue; }
                    var a = sim.Active; res.Trades++; if (hw == 1) res.Wins++;
                    sim.AddFill(a, v); sim.EndTrade(a, new KeystoneSimTrade { Entry = d.Date.AddMinutes(18 * 60 + k - 1440), Exit = d.Date.AddMinutes(18 * 60 + xs - 1440), Dir = dM != 0 ? dM : dG, Legs = Pairs[Math.Max(0, pairOf(st, dM, dG))], Pnl = v, Reason = hw == 1 ? "TARGET" : hw == -1 ? "STOP" : "TIME", Source = "RE" });
                    k = xs + 2;
                }
                var dd = sim.EndDay(); low = Math.Min(low, dd.CumCash - dd.CumSpent);
            }
            res.Days = sim.Days.Count; res.Bought = sim.Bought; res.Passed = sim.PassedCount; res.PassRate = sim.PassRate; res.BlownEval = sim.BlownEval; res.BlownFunded = sim.BlownFunded; res.Payouts = sim.PayoutCount; res.Cash = sim.Cash; res.Spent = sim.Spent; res.Net = sim.Net; res.Worst = low;
            res.AvgPassDays = sim.Accounts.Where(x => x.PassedOn != DateTime.MinValue).Select(x => (double)x.EvalDays).DefaultIfEmpty(0).Average();
            res.AcctDays = sim.Trades.Select(t => t.Day.ToString("yyyyMMdd") + "|" + t.Account).Distinct().Count(); res.AtTarget = KeystoneRotationLab.DaysAtTarget(sim);
            return res;
        };
        Func<PRes, string, string, string> propJ = (x, label, period) => "{\"label\":" + J(label) + ",\"period\":" + J(period) + ",\"days\":" + x.Days + ",\"trades\":" + x.Trades + ",\"hit\":" + Jn(x.Trades == 0 ? 0 : 100.0 * x.Wins / x.Trades) + ",\"bought\":" + x.Bought + ",\"passed\":" + x.Passed + ",\"passRate\":" + Jn(x.PassRate) + ",\"blownEval\":" + x.BlownEval + ",\"blownFunded\":" + x.BlownFunded + ",\"payouts\":" + x.Payouts + ",\"cash\":" + Jn(x.Cash) + ",\"spent\":" + Jn(x.Spent) + ",\"net\":" + Jn(x.Net) + ",\"worst\":" + Jn(x.Worst) + ",\"avgPassDays\":" + Jn(x.AvgPassDays) + ",\"acctDays\":" + x.AcctDays + ",\"atTarget\":" + x.AtTarget + "}";
        Func<int, Func<Day, int, int, Tuple<int, int>>> ruleFn = ri => (d, k, s0) => rules[ri].Pick(d, k, s0);
        // the same entry times as the rule, the sides by a coin (BB / SS when the rule wants both the same way, BS / SB when hedged)
        Func<int, int, Func<Day, int, int, Tuple<int, int>>> coinOf = (ri, seed) => { var rg = new Random(seed); return (d, k, s0) => { var r0 = rules[ri].Pick(d, k, s0); if (r0.Item1 == 0 && r0.Item2 == 0) return r0; int a = rg.Next(2) == 0 ? 1 : -1; bool same = r0.Item1 == 0 || r0.Item2 == 0 || r0.Item1 == r0.Item2; return Tuple.Create(r0.Item1 == 0 ? 0 : a, r0.Item2 == 0 ? 0 : same ? a : -a); }; };
        const int Coins = 40; var luckRows = new List<string>();
        foreach (int i in candidates)
        {
            var c = cfgs[i];
            foreach (bool oos in new[] { false, true })
            {
                var real = propRun(ruleFn(c[0]), c[1], c[3], c[4], oos); propRows.Add(propJ(real, cname(i), oos ? "2024–26" : "2020–23"));
                var coin = new PRes[Coins]; Parallel.For(0, Coins, ci => { coin[ci] = propRun(coinOf(c[0], 7000 + ci), c[1], c[3], c[4], oos); });
                var pr = coin.Select(x => x.PassRate).OrderBy(x => x).ToArray(); var nt = coin.Select(x => x.Net).OrderBy(x => x).ToArray();
                double pPass = (1.0 + coin.Count(x => x.PassRate >= real.PassRate)) / (Coins + 1), pNet = (1.0 + coin.Count(x => x.Net >= real.Net)) / (Coins + 1);
                luckRows.Add("{\"label\":" + J(cname(i)) + ",\"period\":" + J(oos ? "2024–26" : "2020–23") + ",\"realPass\":" + Jn(real.PassRate) + ",\"realNet\":" + Jn(real.Net) + ",\"coinPassMed\":" + Jn(pr[Coins / 2]) + ",\"coinPass5\":" + Jn(pr[(int)(Coins * 0.05)]) + ",\"coinPass95\":" + Jn(pr[(int)(Coins * 0.95)]) + ",\"coinNetMed\":" + Jn(nt[Coins / 2]) + ",\"coinNet95\":" + Jn(nt[(int)(Coins * 0.95)]) + ",\"pPass\":" + Jn(pPass) + ",\"pNet\":" + Jn(pNet) + "}");
                say(string.Format("  prop {0} {1}: pass {2:0}% (coin {3:0}% [{4:0}–{5:0}] p {6:0.00}) net {7:0} (coin {8:0}, p {9:0.00}) | {10}", oos ? "OOS" : "IS ", "", real.PassRate, pr[Coins / 2], pr[(int)(Coins * 0.05)], pr[(int)(Coins * 0.95)], pPass, real.Net, nt[Coins / 2], pNet, cname(i)));
            }
        }
        foreach (int r in new[] { 0, 3 }) foreach (int b in new[] { 0, 3 }) foreach (bool oos in new[] { false, true }) propRows.Add(propJ(propRun(ruleFn(r), 3, 0, b, oos), rules[r].Name + " • " + Sessions[3].Name + " • BOTH 10+10 • +" + Brackets[b][0] + " / −" + Brackets[b][1] + " (baseline)", oos ? "2024–26" : "2020–23"));
        put("prop", "[" + string.Join(",", propRows) + "]");
        put("propLuck", "[" + string.Join(",", luckRows) + "]");
        say("  prop runs: " + propRows.Count + " • coin-flip bands: " + luckRows.Count);

        // ---- 6. the plain rotation (the studio's RANDOM ×4 sides, a new trade 2 minutes after the last) in every session × size ×
        // $ target / stop: pass rate, money, account-days at the day target — the median of 20 coin seeds, 2020–23 and 2024–26
        const int Seeds = 20; var gridRows = new List<string>(); var t1 = DateTime.Now; var gl = new object();
        var combos = new List<int[]>(); for (int s = 0; s < Sessions.Length; s++) foreach (int st in new[] { 0, 1 }) for (int b = 0; b < NB; b++) foreach (int oo in new[] { 0, 1 }) combos.Add(new[] { s, st, b, oo });
        Parallel.For(0, combos.Count, ci =>
        {
            var cb = combos[ci]; var rs = new PRes[Seeds];
            for (int sd = 0; sd < Seeds; sd++) { var rg = new Random(9100 + sd * 31 + ci); rs[sd] = propRun((d, k, s0) => { int pp = rg.Next(4); return Tuple.Create(PairDir[pp, 0], PairDir[pp, 1]); }, cb[0], cb[1], cb[2], cb[3] == 1); }
            Func<Func<PRes, double>, double> med = f => rs.Select(f).OrderBy(x => x).ElementAt(Seeds / 2);
            string row = "{\"s\":" + cb[0] + ",\"set\":" + cb[1] + ",\"b\":" + cb[2] + ",\"period\":" + J(cb[3] == 1 ? "2024–26" : "2020–23") + ",\"pass\":" + Jn(med(x => x.PassRate)) + ",\"passLo\":" + Jn(rs.Select(x => x.PassRate).Min()) + ",\"passHi\":" + Jn(rs.Select(x => x.PassRate).Max()) + ",\"net\":" + Jn(med(x => x.Net)) + ",\"bought\":" + Jn(med(x => x.Bought)) + ",\"payouts\":" + Jn(med(x => x.Payouts)) + ",\"atTarget\":" + Jn(med(x => x.AcctDays == 0 ? 0 : 100.0 * x.AtTarget / x.AcctDays)) + ",\"days2pass\":" + Jn(med(x => x.AvgPassDays)) + ",\"hit\":" + Jn(med(x => x.Trades == 0 ? 0 : 100.0 * x.Wins / x.Trades)) + "}";
            lock (gl) gridRows.Add(row);
        });
        put("rotationGrid", "[" + string.Join(",", gridRows) + "]");
        say("  plain rotation grid: " + gridRows.Count + " cells × " + Seeds + " seeds in " + (DateTime.Now - t1).TotalSeconds.ToString("0") + " s");
        json.Append('}');
        File.WriteAllText(Path.Combine(outDir, "rotation_entries.json"), json.ToString());
        File.WriteAllText(Path.Combine(outDir, "rotation_entries_log.txt"), log.ToString());
    }
    static T2 Let<T1, T2>(this T1 x, Func<T1, T2> f) { return f(x); }
}
