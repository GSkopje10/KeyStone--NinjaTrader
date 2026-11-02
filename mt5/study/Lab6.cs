// part 6: the BIG search — 14 entry signals (trend + reversal + a random coin-flip control) × 4 timeframes × 3 filters × 5 target / stop
// shapes × 3 sessions × 3 frequencies, on gold and Nasdaq 1-minute futures (New York time), every trade risking 1R = $100.
// Chosen on 2025, checked on 2026 and on 2023-24 (never seen). Stage 2: the best get the zone-recovery hedge instead of a stop.
// Output: docs/research/hedge/lab6.json → the HTML report (docs/research/hedge/report.html).
using System; using System.Collections.Generic; using System.Linq; using System.Text; using System.Globalization; using System.Threading.Tasks; using KeystoneMT5;
public static class Lab6
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public sealed class M1 { public string Name; public int N; public float[] O, H, L, C; public double[] V; public int[] Min, Day; public DateTime[] T; public double Cost; public sbyte[] H1Trend; }
    sealed class Ser { public List<float> O = new List<float>(), H = new List<float>(), L = new List<float>(), C = new List<float>(); public List<double> V = new List<double>(); public List<int> End = new List<int>(); }
    public struct Sig { public int At; public sbyte Dir; public float Atr; public sbyte Trend; public bool AtrHi; public float Px, Vw, Lim, Stop; public int Valid; }
    public static readonly string[] Sigs = { "BRK5", "DON20", "EMA_PULLBACK", "EMA_CROSS", "FVG", "ORB", "INSIDE_BAR", "VWAP_CROSS", "RSI_REVERSAL", "BB_REVERSAL", "VWAP_STRETCH", "ENGULF_EXTREME", "AGGRESSION_REV", "RANDOM" };
    public static string Family(string s) { return s == "RANDOM" ? "CONTROL" : Array.IndexOf(Sigs, s) >= 8 ? "REVERSAL" : "TREND"; }
    public static readonly int[] Tfs = { 1, 5, 15, 60 };
    public static readonly string[] Filters = { "NONE", "WITH_H1_TREND", "HIGH_VOLATILITY" };
    public static readonly double[][] Exits = { new[] { 0.5, 1.0 }, new[] { 1.0, 2.0 }, new[] { 1.0, 1.0 }, new[] { 2.0, 1.0 }, new[] { 3.0, 1.5 } };   // target, stop in ATR of the signal timeframe
    public static readonly (string n, int f, int t)[] Sessions = { ("LONDON 03:00-06:00", 180, 360), ("NY 08:30-11:30", 510, 690), ("DAY 03:00-15:30", 180, 930) };
    public static readonly int[] Freqs = { 1, 3, 99 };
    public const int Flat = 955;   // everything closes at 15:55 New York

    public static M1 Prep(string name, List<Mt5Bar> b, double cost)
    {
        var m = new M1 { Name = name, N = b.Count, Cost = cost, O = new float[b.Count], H = new float[b.Count], L = new float[b.Count], C = new float[b.Count], V = new double[b.Count], Min = new int[b.Count], Day = new int[b.Count], T = new DateTime[b.Count] };
        for (int i = 0; i < b.Count; i++) { var ny = b[i].Time.AddHours(-7); m.T[i] = ny; m.O[i] = (float)b[i].O; m.H[i] = (float)b[i].H; m.L[i] = (float)b[i].L; m.C[i] = (float)b[i].C; m.V[i] = b[i].V; m.Min[i] = ny.Hour * 60 + ny.Minute; m.Day[i] = (int)(ny.Date - new DateTime(2000, 1, 1)).TotalDays; }
        var h1 = Agg(m, 60); var e50 = Ema(h1.C, 50); m.H1Trend = new sbyte[m.N]; int k = 0;
        for (int j = 0; j < h1.End.Count; j++) { sbyte tr = (sbyte)(h1.C[j] > e50[j] ? 1 : h1.C[j] < e50[j] ? -1 : 0); int to = j + 1 < h1.End.Count ? h1.End[j + 1] : m.N - 1; for (k = h1.End[j] + 1; k <= to && k < m.N; k++) m.H1Trend[k] = tr; }
        return m;
    }
    static Ser Agg(M1 m, int tf)
    {
        var s = new Ser(); long key = -1;
        for (int i = 0; i < m.N; i++)
        {
            long kk = (long)m.Day[i] * 1440 + m.Min[i] - m.Min[i] % tf;
            if (kk != key) { key = kk; s.O.Add(m.O[i]); s.H.Add(m.H[i]); s.L.Add(m.L[i]); s.C.Add(m.C[i]); s.V.Add(m.V[i]); s.End.Add(i); }
            else { int j = s.C.Count - 1; s.H[j] = Math.Max(s.H[j], m.H[i]); s.L[j] = Math.Min(s.L[j], m.L[i]); s.C[j] = m.C[i]; s.V[j] += m.V[i]; s.End[j] = i; }
        }
        return s;
    }
    static double[] Ema(List<float> x, int n) { var r = new double[x.Count]; double a = 2.0 / (n + 1); for (int i = 0; i < x.Count; i++) r[i] = i == 0 ? x[0] : r[i - 1] + a * (x[i] - r[i - 1]); return r; }

    public static List<Sig> Signals(M1 m, int tf, string sig)
    {
        var s = Agg(m, tf); int n = s.C.Count; var C = s.C; var H = s.H; var L = s.L; var O = s.O; var V = s.V;
        var atr = new double[n]; var rsi = new double[n]; double ag = 0, al = 0;
        for (int i = 0; i < n; i++)
        {
            double tr = i == 0 ? H[i] - L[i] : Math.Max(H[i], C[i - 1]) - Math.Min(L[i], C[i - 1]); atr[i] = i == 0 ? tr : atr[i - 1] + (tr - atr[i - 1]) / 14;
            double ch = i == 0 ? 0 : C[i] - C[i - 1]; ag += ((ch > 0 ? ch : 0) - ag) / 14; al += ((ch < 0 ? -ch : 0) - al) / 14; rsi[i] = al == 0 ? 100 : 100 - 100 / (1 + ag / al);
        }
        var e9 = Ema(C, 9); var e21 = Ema(C, 21); var e20 = Ema(C, 20); var e50 = Ema(C, 50);
        var atrAvg = new double[n]; for (int i = 0; i < n; i++) atrAvg[i] = i == 0 ? atr[0] : atrAvg[i - 1] + (atr[i] - atrAvg[i - 1]) / 100;
        // the session VWAP (the futures day starts at 18:00 New York) sampled at each candle's close
        var vw = new double[n]; { double pv = 0, vv = 0; int lastKey = int.MinValue, j = 0; for (int i = 0; i < m.N && j < n; i++) { int key = m.Min[i] >= 1080 ? m.Day[i] + 1 : m.Day[i]; if (key != lastKey) { pv = 0; vv = 0; lastKey = key; } double tp = (m.H[i] + m.L[i] + m.C[i]) / 3.0, v = Math.Max(1, m.V[i]); pv += tp * v; vv += v; while (j < n && s.End[j] == i) { vw[j] = pv / vv; j++; } } }
        var rng = new Random(tf * 7919 + 17); var res = new List<Sig>(); int lastDir = 0; int orbDay = -1; double orbH = 0, orbL = 0; bool orbDone = false;
        int orbFrom = m.Name == "GOLD" ? 500 : 570, orbTo = orbFrom + 15;
        for (int i = 21; i < n; i++)
        {
            int d = 0; double lim = double.NaN, stp = double.NaN; double avgV = 0, avgR = 0; for (int k = i - 20; k < i; k++) { avgV += V[k]; avgR += H[k] - L[k]; } avgV /= 20; avgR /= 20;
            switch (sig)
            {
                case "BRK5": case "DON20":
                    { int lb = sig == "BRK5" ? 5 : 20; double hh = double.MinValue, ll = double.MaxValue; for (int k = i - lb; k < i; k++) { hh = Math.Max(hh, H[k]); ll = Math.Min(ll, L[k]); } d = C[i] > hh ? 1 : C[i] < ll ? -1 : 0; break; }
                case "EMA_PULLBACK": d = e20[i] > e50[i] && L[i] <= e20[i] && C[i] > e20[i] && C[i] > O[i] ? 1 : e20[i] < e50[i] && H[i] >= e20[i] && C[i] < e20[i] && C[i] < O[i] ? -1 : 0; break;
                case "EMA_CROSS": d = e9[i - 1] <= e21[i - 1] && e9[i] > e21[i] ? 1 : e9[i - 1] >= e21[i - 1] && e9[i] < e21[i] ? -1 : 0; break;
                case "FVG": d = L[i] > H[i - 2] && C[i - 1] - O[i - 1] > avgR ? 1 : H[i] < L[i - 2] && O[i - 1] - C[i - 1] > avgR ? -1 : 0; break;
                case "INSIDE_BAR": d = H[i - 1] < H[i - 2] && L[i - 1] > L[i - 2] ? (C[i] > H[i - 2] ? 1 : C[i] < L[i - 2] ? -1 : 0) : 0; break;
                case "VWAP_CROSS": d = C[i - 1] < vw[i - 1] && C[i] > vw[i] ? 1 : C[i - 1] > vw[i - 1] && C[i] < vw[i] ? -1 : 0; break;
                case "RSI_REVERSAL": d = rsi[i - 1] < 30 && rsi[i] >= 30 ? 1 : rsi[i - 1] > 70 && rsi[i] <= 70 ? -1 : 0; break;
                case "BB_REVERSAL":
                    { double mu = 0, sd = 0, mu1 = 0, sd1 = 0; for (int k = i - 19; k <= i; k++) mu += C[k]; mu /= 20; for (int k = i - 19; k <= i; k++) sd += (C[k] - mu) * (C[k] - mu); sd = Math.Sqrt(sd / 20);
                      for (int k = i - 20; k < i; k++) mu1 += C[k]; mu1 /= 20; for (int k = i - 20; k < i; k++) sd1 += (C[k] - mu1) * (C[k] - mu1); sd1 = Math.Sqrt(sd1 / 20);
                      d = C[i - 1] < mu1 - 2 * sd1 && C[i] > mu - 2 * sd ? 1 : C[i - 1] > mu1 + 2 * sd1 && C[i] < mu + 2 * sd ? -1 : 0; break; }
                case "VWAP_STRETCH": d = vw[i - 1] - C[i - 1] > 1.5 * atr[i - 1] && C[i] > H[i - 1] ? 1 : C[i - 1] - vw[i - 1] > 1.5 * atr[i - 1] && C[i] < L[i - 1] ? -1 : 0; break;
                case "ENGULF_EXTREME":
                    { double ll = double.MaxValue, hh = double.MinValue; for (int k = i - 21; k < i - 1; k++) { ll = Math.Min(ll, L[k]); hh = Math.Max(hh, H[k]); }
                      bool bull = C[i - 1] < O[i - 1] && C[i] > O[i] && C[i] >= O[i - 1] && O[i] <= C[i - 1], bear = C[i - 1] > O[i - 1] && C[i] < O[i] && C[i] <= O[i - 1] && O[i] >= C[i - 1];
                      d = bull && Math.Min(L[i], L[i - 1]) <= ll ? 1 : bear && Math.Max(H[i], H[i - 1]) >= hh ? -1 : 0; break; }
                case "AGGRESSION_REV":
                    { double r = H[i - 1] - L[i - 1]; bool agg = V[i - 1] >= 2 * avgV && r >= avgR && r > 0;
                      d = agg && C[i - 1] <= L[i - 1] + 0.25 * r && C[i] > H[i - 1] ? 1 : agg && C[i - 1] >= H[i - 1] - 0.25 * r && C[i] < L[i - 1] ? -1 : 0; break; }
                case "ORB":
                    { int e = s.End[i], dd = m.Day[e]; if (dd != orbDay) { orbDay = dd; orbH = double.MinValue; orbL = double.MaxValue; orbDone = false; }
                      // the opening range is read straight from the 1-minute candles
                      if (m.Min[e] >= orbTo && !orbDone && orbH == double.MinValue) { for (int k = e; k >= 0 && m.Day[k] == dd; k--) if (m.Min[k] >= orbFrom && m.Min[k] < orbTo) { orbH = Math.Max(orbH, m.H[k]); orbL = Math.Min(orbL, m.L[k]); } }
                      if (!orbDone && orbH > double.MinValue && m.Min[e] >= orbTo) { d = C[i] > orbH ? 1 : C[i] < orbL ? -1 : 0; if (d != 0) orbDone = true; }
                      break; }
                case "FVG50": d = L[i] > H[i - 2] && C[i - 1] - O[i - 1] > avgR ? 1 : H[i] < L[i - 2] && O[i - 1] - C[i - 1] > avgR ? -1 : 0; lim = d > 0 ? (L[i] + H[i - 2]) / 2 : d < 0 ? (H[i] + L[i - 2]) / 2 : double.NaN; break;
                case "SWEEP":   // a stop hunt: the candle takes the last 20 candles' low (high) and closes back inside
                    { double ll = double.MaxValue, hh = double.MinValue; for (int k = i - 20; k < i; k++) { ll = Math.Min(ll, L[k]); hh = Math.Max(hh, H[k]); }
                      d = L[i] < ll && C[i] > ll ? 1 : H[i] > hh && C[i] < hh ? -1 : 0; break; }
                case "VWAP_REJECT":   // above the VWAP for the last 6 candles, this one dips to it and closes back above (mirror for sells)
                    { bool above = true, below = true; for (int k = i - 6; k < i; k++) { if (L[k] <= vw[k]) above = false; if (H[k] >= vw[k]) below = false; }
                      d = above && L[i] <= vw[i] && C[i] > vw[i] ? 1 : below && H[i] >= vw[i] && C[i] < vw[i] ? -1 : 0; break; }
                case "AGG_BREAK3": case "AGG_BREAK4": case "AGG_BREAK5":
                    {   // the user's aggression reversal: N+ candles of one colour, the FIRST opposite close = the signal candle; the entry = the break of its high (buy) / low (sell) during the next candle
                        int need = sig[9] - '0'; int run = 0; bool green = C[i] > O[i], red = C[i] < O[i];
                        for (int k = i - 1; k >= 0 && run < 30; k--) { if (green ? C[k] < O[k] : red ? C[k] > O[k] : false) run++; else break; }
                        if (run >= need && (green || red)) { d = green ? 1 : -1; stp = green ? H[i] : L[i]; }
                        break;
                    }
                case "RANDOM": d = rng.NextDouble() < 0.08 ? (rng.Next(2) == 0 ? 1 : -1) : 0; break;
            }
            bool fresh = sig == "RANDOM" || sig == "ORB" || sig == "SWEEP" || sig.StartsWith("AGG_BREAK") || d != lastDir; lastDir = d;
            if (d == 0 || !fresh) continue;
            int at = s.End[i] + 1; if (at >= m.N || m.Day[at] != m.Day[s.End[i]]) continue;
            res.Add(new Sig { At = at, Dir = (sbyte)d, Atr = (float)atr[i], Trend = m.H1Trend[s.End[i]], AtrHi = atr[i] > atrAvg[i], Px = C[i], Vw = (float)vw[i], Lim = (float)lim, Stop = (float)stp, Valid = tf });
        }
        return res;
    }

    public sealed class Cfg { public string Inst, Sig, Filter, Session, Hedge = "NONE"; public int Tf, Freq, SessF, SessT; public double Tgt, Stp; public string Key { get { return string.Join("|", Inst, Sig, "M" + Tf, Filter, Session, Tgt.ToString(IC) + "/" + Stp.ToString(IC), Freq == 99 ? "all" : Freq + "/day", Hedge); } } }
    public sealed class Trade { public int Day; public double R; public int Hold; public int Legs; }
    public sealed class Stat { public int N, Wins; public double NetR, GrossW, GrossL, MaxDdR, AvgHold, MaxLegs; public int Days; public double Pf { get { return GrossL > 0 ? GrossW / GrossL : GrossW > 0 ? 99 : 0; } } public double Wr { get { return N > 0 ? (double)Wins / N : 0; } } public double ExpR { get { return N > 0 ? NetR / N : 0; } } }
    public static int Period(int day) { var y = new DateTime(2000, 1, 1).AddDays(day).Year; return y == 2025 ? 0 : y == 2026 ? 1 : y >= 2023 && y <= 2024 ? 2 : -1; }

    // one position at a time; the legs of a hedged basket all count against the same 1R
    public static List<Trade> Sim(M1 m, List<Sig> sigs, Cfg c)
    {
        var res = new List<Trade>(); int busyTo = -1, day = -1, today = 0;
        foreach (var g in sigs)
        {
            if (g.At <= busyTo) continue; int mn = m.Min[g.At]; if (mn < c.SessF || mn >= c.SessT) continue;
            if (c.Filter == "WITH_H1_TREND" && g.Trend != g.Dir) continue; if (c.Filter == "HIGH_VOLATILITY" && !g.AtrHi) continue;
            if (m.Day[g.At] != day) { day = m.Day[g.At]; today = 0; } if (today >= c.Freq) continue; today++;
            double stop = c.Stp * g.Atr, tgt = c.Tgt * g.Atr; if (stop <= 0) continue;
            int end; var t = c.Hedge == "NONE" ? One(m, g, stop, tgt, out end) : Basket(m, g, stop, tgt, c.Hedge, out end);
            t.Day = day; t.Hold = end - g.At + 1; res.Add(t); busyTo = end;
        }
        return res;
    }
    static Trade One(M1 m, Sig g, double stop, double tgt, out int end)
    {
        double e = m.O[g.At], d = g.Dir, sl = e - d * stop, tp = e + d * tgt, exit = e; int j = g.At;
        for (; j < m.N; j++)
        {
            if (m.Day[j] != m.Day[g.At]) { exit = m.C[j - 1]; j--; break; }
            if (m.Min[j] >= Flat) { exit = m.O[j]; break; }
            if (d > 0 ? m.L[j] <= sl : m.H[j] >= sl) { exit = sl; break; }   // the stop first when both are inside one candle
            if (d > 0 ? m.H[j] >= tp : m.L[j] <= tp) { exit = tp; break; }
        }
        if (j >= m.N) { j = m.N - 1; exit = m.C[j]; }
        end = j; return new Trade { R = ((exit - e) * d - m.Cost) / stop, Legs = 1 };
    }
    // the zone recovery: the stop becomes an opposite leg (×2), then ×4 back at the entry (ZR2); the whole basket closes at +1 target
    // (in base-lot money) or at −2.5 R, or at 15:55
    static Trade Basket(M1 m, Sig g, double stop, double tgt, string mode, out int end)
    {
        int maxLegs = mode == "ZR1" ? 2 : 3; double e = m.O[g.At], d = g.Dir; var px = new List<double> { e }; var lot = new List<double> { d };
        double up = d > 0 ? e : e + stop, dn = d > 0 ? e - stop : e;   // the two zone lines
        double cost = m.Cost; Func<double, double> pl = p => { double s = 0; for (int k = 0; k < px.Count; k++) s += (p - px[k]) * lot[k] - Math.Abs(lot[k]) * cost; return s; };
        double win = tgt, lose = -2.5 * stop, result = 0; int j = g.At; bool done = false;
        for (; j < m.N && !done; j++)
        {
            if (m.Day[j] != m.Day[g.At]) { result = pl(m.C[j - 1]); j--; break; }
            if (m.Min[j] >= Flat) { result = pl(m.O[j]); break; }
            double net = lot.Sum(); bool added = false;
            if (px.Count < maxLegs) { double next = -Math.Sign(net) * Math.Abs(lot[lot.Count - 1]) * 2, lvl = net > 0 ? dn : up; if (net > 0 ? m.L[j] <= lvl : m.H[j] >= lvl) { px.Add(lvl); lot.Add(next); added = true; net = lot.Sum(); } }
            double worst = net > 0 ? m.L[j] : m.H[j], best = net > 0 ? m.H[j] : m.L[j];
            if (pl(worst) <= lose) { result = lose; break; }   // (the basket stop is a price level: filled there)
            if (!added && pl(best) >= win) { result = win; break; }
        }
        if (j >= m.N) { j = m.N - 1; result = pl(m.C[j]); }
        end = Math.Min(j, m.N - 1); return new Trade { R = result / stop, Legs = px.Count };
    }
    public static Stat[] Stats(List<Trade> ts)
    {
        var r = new Stat[4]; for (int p = 0; p < 4; p++) r[p] = new Stat();
        foreach (var grp in ts.GroupBy(t => Period(t.Day) < 0 ? 3 : Period(t.Day)))
        {
            var s = r[grp.Key]; double eq = 0, pk = 0; var days = new HashSet<int>();
            foreach (var t in grp) { s.N++; if (t.R > 0) { s.Wins++; s.GrossW += t.R; } else s.GrossL -= t.R; s.NetR += t.R; eq += t.R; pk = Math.Max(pk, eq); s.MaxDdR = Math.Max(s.MaxDdR, pk - eq); s.AvgHold += t.Hold; s.MaxLegs = Math.Max(s.MaxLegs, t.Legs); days.Add(t.Day); }
            if (s.N > 0) s.AvgHold /= s.N; s.Days = days.Count;
        }
        return r;
    }

    public static void Run(Dictionary<string, M1> inst, string outJson, Dictionary<string, M1> noGaps)
    {
        var cfgs = new List<Cfg>();
        foreach (var kv in inst) foreach (var sg in Sigs) foreach (var tf in Tfs) foreach (var fl in Filters) foreach (var ex in Exits) foreach (var se in Sessions) foreach (var fq in Freqs)
            cfgs.Add(new Cfg { Inst = kv.Key, Sig = sg, Tf = tf, Filter = fl, Tgt = ex[0], Stp = ex[1], Session = se.n, SessF = se.f, SessT = se.t, Freq = fq });
        var sigCache = new System.Collections.Concurrent.ConcurrentDictionary<string, List<Sig>>();
        Parallel.ForEach(inst.Keys.SelectMany(k => Sigs.SelectMany(sg => Tfs.Select(tf => (k, sg, tf)))), x => sigCache[x.k + x.sg + x.tf] = Signals(inst[x.k], x.tf, x.sg));
        Console.WriteLine("signals ready: " + sigCache.Count + " lists • " + cfgs.Count + " settings"); var t0 = DateTime.Now;
        var stats = new Stat[cfgs.Count][];
        Parallel.For(0, cfgs.Count, i => { var c = cfgs[i]; stats[i] = Stats(Sim(inst[c.Inst], sigCache[c.Inst + c.Sig + c.Tf], c)); });
        Console.WriteLine("stage 1 done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        // robust = profitable in all three periods with 40+ trades in each; score = the WORST period's expectancy
        Func<Stat[], bool> robust = s => s[0].N >= 40 && s[1].N >= 30 && s[2].N >= 60 && s[0].NetR > 0 && s[1].NetR > 0 && s[2].NetR > 0;
        Func<Stat[], double> score = s => Math.Min(s[0].ExpR, Math.Min(s[1].ExpR, s[2].ExpR)) * Math.Sqrt(Math.Min(s[0].N, Math.Min(s[1].N, s[2].N / 2.0)));
        var order = Enumerable.Range(0, cfgs.Count).Where(i => robust(stats[i])).OrderByDescending(i => score(stats[i])).ToList();
        Console.WriteLine("robust (all three periods): " + order.Count + " of " + cfgs.Count + " • of them RANDOM control: " + order.Count(i => cfgs[i].Sig == "RANDOM") + " of " + cfgs.Count(c => c.Sig == "RANDOM"));
        // stage 2: the best 25 per instrument (no RANDOM), each with the zone-recovery hedge instead of the stop
        var top = new List<int>(); foreach (var k in inst.Keys) top.AddRange(order.Where(i => cfgs[i].Inst == k && cfgs[i].Sig != "RANDOM").Take(25));
        var hed = new List<Cfg>(); foreach (var i in top) foreach (var h in new[] { "ZR1", "ZR2" }) { var c = cfgs[i]; hed.Add(new Cfg { Inst = c.Inst, Sig = c.Sig, Tf = c.Tf, Filter = c.Filter, Tgt = c.Tgt, Stp = c.Stp, Session = c.Session, SessF = c.SessF, SessT = c.SessT, Freq = c.Freq, Hedge = h }); }
        var hstats = new Stat[hed.Count][]; Parallel.For(0, hed.Count, i => { var c = hed[i]; hstats[i] = Stats(Sim(inst[c.Inst], sigCache[c.Inst + c.Sig + c.Tf], c)); });
        // the curves (R by month) + the trade lists for the Monte Carlo of the best 12 per instrument
        var curveIdx = new List<int>(); foreach (var k in inst.Keys) curveIdx.AddRange(order.Where(i => cfgs[i].Inst == k && cfgs[i].Sig != "RANDOM").Take(12));
        var sb = new StringBuilder(); sb.Append("{\"generated\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + "\",\"rows\":[");
        Func<Stat, string> S = s => "[" + s.N + "," + F(s.Wr, 3) + "," + F(s.Pf, 2) + "," + F(s.ExpR, 3) + "," + F(s.NetR, 1) + "," + F(s.MaxDdR, 1) + "," + F(s.AvgHold, 0) + "," + s.Days + "," + s.MaxLegs + "]";
        Action<Cfg, Stat[], bool> row = (c, s, last) => sb.Append("[\"" + c.Inst + "\",\"" + c.Sig + "\"," + c.Tf + ",\"" + c.Filter + "\",\"" + c.Session + "\"," + F(c.Tgt, 1) + "," + F(c.Stp, 1) + "," + c.Freq + ",\"" + c.Hedge + "\"," + S(s[0]) + "," + S(s[1]) + "," + S(s[2]) + "]" + (last ? "" : ","));
        for (int i = 0; i < cfgs.Count; i++) row(cfgs[i], stats[i], i == cfgs.Count - 1);
        sb.Append("],\"hedged\":["); for (int i = 0; i < hed.Count; i++) row(hed[i], hstats[i], i == hed.Count - 1);
        sb.Append("],\"curves\":["); bool first = true;
        foreach (var i in curveIdx)
        {
            var c = cfgs[i]; var tr = Sim(inst[c.Inst], sigCache[c.Inst + c.Sig + c.Tf], c).Where(t => Period(t.Day) >= 0).OrderBy(t => t.Day).ToList();
            var months = tr.GroupBy(t => new DateTime(2000, 1, 1).AddDays(t.Day).ToString("yyyy-MM", IC)).Select(g => "[\"" + g.Key + "\"," + F(g.Sum(x => x.R), 1) + "," + g.Count() + "]");
            sb.Append((first ? "" : ",") + "{\"key\":\"" + c.Key + "\",\"months\":[" + string.Join(",", months) + "],\"r\":[" + string.Join(",", tr.Select(t => F(t.R, 2))) + "]}"); first = false;
        }
        Console.WriteLine("-- box, futures data as is:"); var raw = BoxJson(inst); Console.WriteLine("-- box, overnight / roll gaps closed:"); sb.Append("],\"boxRaw\":" + raw + ",\"box\":" + BoxJson(noGaps) + "}"); System.IO.File.WriteAllText(outJson, sb.ToString());
        Console.WriteLine("best robust:"); foreach (var i in order.Take(30)) { var s = stats[i]; Console.WriteLine(string.Format(IC, "  {0,-90} exp {1:0.000}/{2:0.000}/{3:0.000} R  n {4}/{5}/{6}  PF {7:0.00}/{8:0.00}/{9:0.00}", cfgs[i].Key, s[0].ExpR, s[1].ExpR, s[2].ExpR, s[0].N, s[1].N, s[2].N, s[0].Pf, s[1].Pf, s[2].Pf)); }
        Console.WriteLine("hedged vs plain (sum of expectancy over the 3 periods): plain " + F(top.Average(i => stats[i].Take(3).Sum(x => x.ExpR)), 3) + " • ZR1 " + F(Enumerable.Range(0, hed.Count).Where(i => hed[i].Hedge == "ZR1").Average(i => hstats[i].Take(3).Sum(x => x.ExpR)), 3) + " • ZR2 " + F(Enumerable.Range(0, hed.Count).Where(i => hed[i].Hedge == "ZR2").Average(i => hstats[i].Take(3).Sum(x => x.ExpR)), 3));
    }

    // the "hedgingmastermind" idea: never take a stop; when price breaks the CONSOLIDATION box (the high / low of the last K hours) the net
    // position flips to that side (open 2 units against 1, so the net stays ±1 unit), inside the box nothing happens ("stop hedging"),
    // the basket closes as soon as everything together is +target. Runs for days; every opened unit pays the round-turn cost and every
    // unit held overnight pays the swap. Equity = realized + floating, measured each minute.
    public sealed class BoxRes { public string Inst; public int K; public double Buf, Tgt; public bool SwapFree; public double[] Net = new double[3], Dd = new double[3], Worst = new double[3], Swap = new double[3]; public int[] Baskets = new int[3]; public double[] AvgDays = new double[3], MaxDays = new double[3], MaxGross = new double[3]; }
    public static BoxRes Box(M1 m, int K, double buf, double tgtAtr, bool swapFree, double usd, double swapPerUnit)
    {
        var h1 = Agg(m, 60); int n1 = h1.C.Count; var atr = new double[n1];
        for (int j = 0; j < n1; j++) { double tr = j == 0 ? h1.H[j] - h1.L[j] : Math.Max(h1.H[j], h1.C[j - 1]) - Math.Min(h1.L[j], h1.C[j - 1]); atr[j] = j == 0 ? tr : atr[j - 1] + (tr - atr[j - 1]) / 14; }
        var r = new BoxRes { K = K, Buf = buf, Tgt = tgtAtr, SwapFree = swapFree, Inst = m.Name };
        double real = 0, locked = 0, ePx = 0, T = 0, gross = 0; int net = 0, startDay = 0; double[] pkEq = new double[3], startEq = new double[3]; int lastP = -1; double eqNow = 0;
        var hold = new List<double>[3]; for (int p = 0; p < 3; p++) { hold[p] = new List<double>(); pkEq[p] = double.MinValue; }
        int j1 = K; int prevMin = -1;
        for (int i = h1.End[K] + 1; i < m.N; i++)
        {
            while (j1 + 1 < n1 && h1.End[j1 + 1] < i) j1++;   // the last completed H1 candle
            double hi = double.MinValue, lo = double.MaxValue; for (int k = j1 - K + 1; k <= j1; k++) { hi = Math.Max(hi, h1.H[k]); lo = Math.Min(lo, h1.L[k]); }
            double a = atr[j1], b = buf * a; int p = Period(m.Day[i]);
            // overnight swap at 17:00 New York on every unit held (both sides)
            if (!swapFree && net != 0 && prevMin >= 0 && prevMin < 1020 && m.Min[i] >= 1020) { double sw = gross * swapPerUnit * (m.T[i].DayOfWeek == DayOfWeek.Wednesday ? 3 : 1); locked -= sw; if (p >= 0) r.Swap[p] += sw * usd; }
            prevMin = m.Min[i];
            if (net == 0)
            {
                if (m.H[i] > hi + b) { net = 1; ePx = hi + b; } else if (m.L[i] < lo - b) { net = -1; ePx = lo - b; }
                if (net != 0) { locked = -m.Cost; gross = 1; T = tgtAtr * a; startDay = m.Day[i]; }
            }
            else
            {
                bool flipped = false;
                if (net > 0 && m.L[i] < lo - b) { double lv = Math.Min(m.O[i], lo - b); locked += lv - ePx - 2 * m.Cost; ePx = lv; net = -1; gross += 2; flipped = true; }
                else if (net < 0 && m.H[i] > hi + b) { double lv = Math.Max(m.O[i], hi + b); locked += ePx - lv - 2 * m.Cost; ePx = lv; net = 1; gross += 2; flipped = true; }
                double best = net > 0 ? m.H[i] : m.L[i];
                if (!flipped && locked + (best - ePx) * net >= T)
                {
                    real += T; if (p >= 0) { r.Baskets[p]++; double d = m.Day[i] - startDay + 1; hold[p].Add(d); r.MaxGross[p] = Math.Max(r.MaxGross[p], gross); }
                    net = 0; locked = 0; gross = 0;
                }
                else if (p >= 0) r.MaxGross[p] = Math.Max(r.MaxGross[p], gross);
            }
            eqNow = real + (net != 0 ? locked + (m.C[i] - ePx) * net : 0);
            if (p != lastP) { if (lastP >= 0) r.Net[lastP] = (eqNow - startEq[lastP]) * usd; if (p >= 0) { startEq[p] = eqNow; pkEq[p] = eqNow; } lastP = p; }
            if (p >= 0) { pkEq[p] = Math.Max(pkEq[p], eqNow); r.Dd[p] = Math.Max(r.Dd[p], (pkEq[p] - eqNow) * usd); if (net != 0) r.Worst[p] = Math.Min(r.Worst[p], (locked + (m.C[i] - ePx) * net) * usd); r.Net[p] = (eqNow - startEq[p]) * usd; }
        }
        for (int p = 0; p < 3; p++) { r.AvgDays[p] = hold[p].Count > 0 ? hold[p].Average() : 0; r.MaxDays[p] = hold[p].Count > 0 ? hold[p].Max() : 0; }
        return r;
    }
    public static string BoxJson(Dictionary<string, M1> inst)
    {
        var jobs = new List<(M1 m, int K, double buf, double tgt, bool sf)>();
        foreach (var m in inst.Values) foreach (var K in new[] { 3, 6, 12, 24, 48, 96 }) foreach (var buf in new[] { 0.0, 0.5 }) foreach (var tg in new[] { 1.0, 2, 4, 8 }) foreach (var sf in new[] { false, true }) jobs.Add((m, K, buf, tg, sf));
        var res = new BoxRes[jobs.Count];
        Parallel.For(0, jobs.Count, i => { var j = jobs[i]; bool g = j.m.Name == "GOLD"; res[i] = Box(j.m, j.K, j.buf, j.tgt, j.sf, g ? 10 : 2, g ? 0.25 : 3.0); });
        foreach (var r in res.Where(x => x.Net.All(v => v > 0)).OrderByDescending(x => x.Net.Min()).Take(10)) Console.WriteLine(string.Format(IC, "BOX {0} K{1} buf{2} tgt{3} swapfree {4}: net {5:N0}/{6:N0}/{7:N0} dd {8:N0}/{9:N0}/{10:N0} worst basket {11:N0} gross {12}", r.Inst, r.K, r.Buf, r.Tgt, r.SwapFree, r.Net[0], r.Net[1], r.Net[2], r.Dd[0], r.Dd[1], r.Dd[2], r.Worst.Min(), r.MaxGross.Max()));
        Console.WriteLine("BOX profitable in all 3 periods: " + res.Count(x => x.Net.All(v => v > 0)) + " of " + res.Length + " (swap paid: " + res.Count(x => !x.SwapFree && x.Net.All(v => v > 0)) + ")");
        Func<double[], string> A = v => "[" + string.Join(",", v.Select(x => F(x, 0))) + "]";
        return "[" + string.Join(",", res.Select(r => "[\"" + r.Inst + "\"," + r.K + "," + F(r.Buf, 1) + "," + F(r.Tgt, 0) + "," + (r.SwapFree ? 1 : 0) + "," + A(r.Net) + "," + A(r.Dd) + "," + A(r.Worst) + "," + A(r.Swap) + ",[" + string.Join(",", r.Baskets) + "]," + "[" + string.Join(",", r.AvgDays.Select(x => F(x, 1))) + "]," + A(r.MaxDays) + "," + A(r.MaxGross) + "]")) + "]";
    }

    // ===== part 7: the user's plan on gold — $5,000, 0.1 lot, +$20 a trade, one trade at a time all day, NO stop (or an emergency stop),
    // which entry gets to +$20 most often with the smallest heat against it?
    public sealed class S7 { public double MaxLots = 0.1; public int N, Hits; public double Net, Win, Loss, Dd, Worst, MinEq = 5000; public bool Blown; public List<double> Mae = new List<double>(); public List<int> Mins = new List<int>(); public HashSet<int> Days = new HashSet<int>(); public Dictionary<int, double> DayPl = new Dictionary<int, double>(); }
    public static double Cost7 = 0.27, Spread7 = 0.20;   // a round turn on 1 unit (spread + commission) and the spread alone, in price
    public static S7[] Run7(M1 m, List<Sig> sigs, string filter, string exit, int sf, int st, double tgtUsd)
    {
        var r = new S7[3]; double[] eq = new double[3], pk = new double[3]; for (int p = 0; p < 3; p++) { r[p] = new S7(); eq[p] = pk[p] = 5000; }
        int busyTo = -1; double tgtMove = tgtUsd / 10 + Spread7, cost = Cost7, stopMove = exit.StartsWith("STOP") ? double.Parse(exit.Substring(4), IC) / 10 : double.MaxValue;
        foreach (var g in sigs)
        {
            if (g.At <= busyTo) continue; int mn = m.Min[g.At]; if (mn < sf || mn >= st) continue;
            if (filter == "H1_TREND" && g.Trend != g.Dir) continue; if (filter == "H1_AGAINST" && g.Trend != -g.Dir) continue;
            if (filter == "VWAP_DISCOUNT" && (g.Dir > 0 ? g.Px >= g.Vw : g.Px <= g.Vw)) continue;   // buy under the VWAP, sell over it
            if (filter == "VWAP_FLOW" && (g.Dir > 0 ? g.Px <= g.Vw : g.Px >= g.Vw)) continue;       // buy over the VWAP, sell under it
            int p = Period(m.Day[g.At]); if (p < 0 || r[p].Blown) continue;
            // a limit fills only when price trades THROUGH it (a buy: the ask = the bid + 0.20 spread)
            int start = g.At; double e = m.O[g.At], d = g.Dir;
            if (!float.IsNaN(g.Lim)) { start = -1; for (int j = g.At; j < Math.Min(m.N, g.At + 12) && m.Day[j] == m.Day[g.At]; j++) if (d > 0 ? m.L[j] <= g.Lim - Spread7 - 0.05 : m.H[j] >= g.Lim + 0.05) { start = j; e = g.Lim; break; } if (start < 0) continue; }
            if (!float.IsNaN(g.Stop)) { start = -1; for (int j = g.At; j < Math.Min(m.N, g.At + Math.Max(1, g.Valid)) && m.Day[j] == m.Day[g.At]; j++) if (d > 0 ? m.H[j] > g.Stop : m.L[j] < g.Stop) { start = j; e = g.Stop + d * 0.05; break; } if (start < 0) continue; }   // a stop entry: the break of the signal candle, filled 5 points worse
            if (exit.StartsWith("ZR"))
            {   // the exact zone recovery: each new leg is sized so the WHOLE basket is +$20 net when price runs T past the leg's line
                var zp = exit.Substring(2).Split('x'); double Z = double.Parse(zp[0], IC) / 10, T = tgtUsd / 10; int maxL = int.Parse(zp[1], IC);
                var px = new List<double> { e }; var lt = new List<double> { d }; double up = d > 0 ? e : e + Z, dn = d > 0 ? e - Z : e, bmae = 0, res7 = 0, maxLots = 1; int jj = start, end7 = start; bool ok = false;
                Func<double, double> PL = q => { double t = 0; for (int k = 0; k < px.Count; k++) t += (q - px[k]) * lt[k] - Math.Abs(lt[k]) * cost; return t; };
                for (; jj < m.N; jj++)
                {
                    if (m.Day[jj] != m.Day[start]) { res7 = PL(m.C[jj - 1]); end7 = jj - 1; break; }
                    if (m.Min[jj] >= 1015) { res7 = PL(m.O[jj]); end7 = jj; break; }
                    double net = lt.Sum(), line = net > 0 ? dn : up; bool added = false, fail = false;
                    if (net > 0 ? m.L[jj] <= line : m.H[jj] >= line)
                    {
                        if (px.Count >= maxL) fail = true;
                        else { double nd = -Math.Sign(net), aim = line + nd * T; double size = Math.Ceiling((T - PL(aim)) / (T - cost) * 10) / 10; px.Add(line); lt.Add(nd * size); maxLots = Math.Max(maxLots, lt.Sum(x => Math.Abs(x))); added = true; net = lt.Sum(); }
                    }
                    if (fail) { res7 = PL(line); end7 = jj; break; }
                    double worst = net > 0 ? m.L[jj] : m.H[jj], best = net > 0 ? m.H[jj] : m.L[jj];
                    bmae = Math.Max(bmae, -PL(worst));
                    if (!added && PL(best) >= T) { res7 = T; ok = true; end7 = jj; break; }
                }
                if (jj >= m.N) { end7 = m.N - 1; res7 = PL(m.C[end7]); }
                double pz = res7 * 10; var sz = r[p]; sz.N++; if (ok) { sz.Hits++; sz.Mins.Add(end7 - start + 1); } if (pz > 0) sz.Win += pz; else sz.Loss -= pz; sz.Net += pz; sz.Worst = Math.Min(sz.Worst, pz); sz.Mae.Add(bmae * 10); sz.Days.Add(m.Day[start]);
                sz.DayPl[m.Day[start]] = (sz.DayPl.TryGetValue(m.Day[start], out var dz) ? dz : 0) + pz; sz.MaxLots = Math.Max(sz.MaxLots, maxLots / 10);
                sz.MinEq = Math.Min(sz.MinEq, eq[p] - bmae * 10); sz.Dd = Math.Max(sz.Dd, pk[p] - (eq[p] - bmae * 10)); eq[p] += pz; pk[p] = Math.Max(pk[p], eq[p]); if (eq[p] - bmae * 10 <= 0) sz.Blown = true;
                busyTo = end7; continue;
            }
            double mae = 0, exitPx = e, swap = 0; bool hit = false; int j2 = start, endI = start, prev = m.Min[start], days = 0;
            for (; j2 < m.N; j2++)
            {
                if (exit != "HOLD") { if (m.Day[j2] != m.Day[start]) { exitPx = m.C[j2 - 1]; endI = j2 - 1; break; } if (m.Min[j2] >= 1015) { exitPx = m.O[j2]; endI = j2; break; } }
                else { if (prev < 1020 && m.Min[j2] >= 1020) { swap += 0.25; days++; } prev = m.Min[j2]; if (days > 30) { exitPx = m.O[j2]; endI = j2; break; } }
                double adv = d > 0 ? e - m.L[j2] : m.H[j2] - e, fav = d > 0 ? m.H[j2] - e : e - m.L[j2];
                if (adv >= stopMove) { mae = stopMove; exitPx = e - d * stopMove; endI = j2; break; }
                mae = Math.Max(mae, adv);
                if (eq[p] - mae * 10 <= 0) { r[p].Blown = true; exitPx = e - d * mae; endI = j2; break; }
                if (fav >= tgtMove) { hit = true; exitPx = e + d * tgtMove; endI = j2; break; }
            }
            if (j2 >= m.N) { endI = m.N - 1; exitPx = m.C[endI]; }
            double pl = ((exitPx - e) * d - cost - swap) * 10; var s = r[p];
            s.N++; if (hit) { s.Hits++; s.Mins.Add(endI - start + 1); } if (pl > 0) s.Win += pl; else s.Loss -= pl; s.Net += pl; s.Worst = Math.Min(s.Worst, pl); s.Mae.Add(mae * 10); s.Days.Add(m.Day[start]);
            s.DayPl[m.Day[start]] = (s.DayPl.TryGetValue(m.Day[start], out var dp) ? dp : 0) + pl;
            s.MinEq = Math.Min(s.MinEq, eq[p] - mae * 10); s.Dd = Math.Max(s.Dd, pk[p] - (eq[p] - mae * 10)); eq[p] += pl; pk[p] = Math.Max(pk[p], eq[p]); if (eq[p] <= 0) s.Blown = true;
            busyTo = endI;
        }
        return r;
    }
    public static void Scalp20(M1 m, string outJson, double cost = 0.27, double spread = 0.20)
    {
        Cost7 = cost; Spread7 = spread; var sigs = new[] { "AGG_BREAK3", "AGG_BREAK4", "AGG_BREAK5", "RANDOM", "BRK5", "FVG", "FVG50", "VWAP_CROSS", "VWAP_REJECT", "VWAP_STRETCH", "SWEEP", "AGGRESSION_REV", "EMA_PULLBACK", "RSI_REVERSAL", "BB_REVERSAL", "ENGULF_EXTREME", "ORB", "INSIDE_BAR" };
        var tfs = new[] { 1, 5, 15 }; var filters = new[] { "NONE", "H1_TREND", "H1_AGAINST", "VWAP_DISCOUNT", "VWAP_FLOW" }; var exits = new[] { "EOD", "HOLD", "STOP100", "STOP200", "STOP300", "ZR20x3", "ZR20x4", "ZR20x5", "ZR40x3", "ZR40x4", "ZR40x5", "ZR60x3", "ZR60x4" };
        var sess = new[] { ("ALL DAY 03:00-15:30", 180, 930), ("NY 08:30-11:30", 510, 690), ("LONDON 03:00-06:00", 180, 360) };
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<string, List<Sig>>();
        Parallel.ForEach(sigs.SelectMany(s => tfs.Select(t => (s, t))), x => cache[x.s + x.t] = Signals(m, x.t, x.s));
        var jobs = new List<(string sig, int tf, string f, string ex, (string, int, int) se)>();
        foreach (var sg in sigs) foreach (var tf in tfs) foreach (var f in filters) foreach (var ex in exits) foreach (var se in sess) jobs.Add((sg, tf, f, ex, se));
        var res = new S7[jobs.Count][]; Parallel.For(0, jobs.Count, i => { var j = jobs[i]; res[i] = Run7(m, cache[j.sig + j.tf], j.f, j.ex, j.se.Item2, j.se.Item3, 20); });
        Func<List<double>, double, double> Q = (l, q) => { if (l.Count == 0) return 0; var o = l.OrderBy(x => x).ToList(); return o[(int)Math.Min(o.Count - 1, Math.Floor(q * (o.Count - 1)))]; };
        Func<S7, string> St = s => "[" + s.N + "," + s.Hits + "," + F(s.Net, 0) + "," + F(s.Win, 0) + "," + F(s.Loss, 0) + "," + F(s.Dd, 0) + "," + F(s.Worst, 0) + "," + (s.Blown ? 1 : 0) + "," + F(Q(s.Mae, .5), 0) + "," + F(Q(s.Mae, .9), 0) + "," + F(Q(s.Mae, .99), 0) + "," + F(s.Mae.Count > 0 ? s.Mae.Max() : 0, 0) + "," + F(Q(s.Mins.Select(x => (double)x).ToList(), .5), 0) + "," + s.Days.Count + "," + s.DayPl.Count(x => x.Value > 0) + "," + F(s.DayPl.Count > 0 ? s.DayPl.Values.Min() : 0, 0) + "," + F(s.MinEq, 0) + "," + F(s.MaxLots, 1) + "]";
        var sb = new StringBuilder("{\"generated\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + "\",\"rows\":[");
        for (int i = 0; i < jobs.Count; i++) { var j = jobs[i]; sb.Append((i > 0 ? "," : "") + "[\"" + j.sig + "\"," + j.tf + ",\"" + j.f + "\",\"" + j.ex + "\",\"" + j.se.Item1 + "\"," + St(res[i][0]) + "," + St(res[i][1]) + "," + St(res[i][2]) + "]"); }
        sb.Append("]}"); System.IO.File.WriteAllText(outJson, sb.ToString());
        Func<S7[], double> worst = r => r.Any(x => x.Blown || x.N < 30) ? -1e9 : r.Min(x => x.Net);
        var ord = Enumerable.Range(0, jobs.Count).OrderByDescending(i => worst(res[i])).ToList();
        Console.WriteLine("profitable in all 3 periods, never blown, 30+ trades each: " + ord.Count(i => worst(res[i]) > 0) + " of " + jobs.Count + " (RANDOM: " + ord.Count(i => worst(res[i]) > 0 && jobs[i].sig == "RANDOM") + ")");
        foreach (var ex in exits) Console.WriteLine(string.Format(IC, "  exit {0,-8} profitable all 3: {1,4} / {2}   blown in some period: {3}", ex, ord.Count(i => jobs[i].ex == ex && worst(res[i]) > 0), jobs.Count(j => j.ex == ex), Enumerable.Range(0, jobs.Count).Count(i => jobs[i].ex == ex && res[i].Any(x => x.Blown))));
        foreach (var i in ord.Take(25)) { var j = jobs[i]; var r = res[i]; Console.WriteLine(string.Format(IC, "  {0}|M{1}|{2}|{3}|{4}: net {5:N0}/{6:N0}/{7:N0} hit {8:0.0}% n {9}/{10}/{11} DD {12:N0} worst {13:N0} mae99 {14:N0}", j.sig, j.tf, j.f, j.ex, j.se.Item1, r[0].Net, r[1].Net, r[2].Net, 100.0 * r.Sum(x => x.Hits) / Math.Max(1, r.Sum(x => x.N)), r[0].N, r[1].N, r[2].N, r.Max(x => x.Dd), r.Min(x => x.Worst), Q(r.SelectMany(x => x.Mae).ToList(), .99))); }
    }

    // ===== part 8: 0.01 lot, +$2 a basket, all day — ADD TO THE WINNER and HEDGE THE LOSER combinations on the researched entries.
    // A unit = 0.01 lot = $1 a $1 move. Every leg pays 0.27 a unit (spread 0.20 + commission). Equity is checked at the worst price of every
    // minute for a $5,000 account at 0.01 AND at 0.1 (the same trades × 10).
    public sealed class Mix { public string Entry, Name; public double T, AddEvery, HedgeAt, HedgeMult, Stop; public int MaxAdds, MaxHedges; }
    public sealed class MixRes { public double[] Net = new double[3], Dd = new double[3]; public int[] N = new int[3], Wins = new int[3]; public bool Blown1, Blown10; public double Worst, MaxUnits, MinEq1 = 5000, MinEq10 = 5000; }
    static MixRes RunMix(M1 m, List<Sig> sigs, Mix x, string filter, int sf, int st)
    {
        var r = new MixRes(); double eq = 0, pk = 0; int busyTo = -1; const double cost = 0.27;
        foreach (var g in sigs)
        {
            if (g.At <= busyTo) continue; int mn = m.Min[g.At]; if (mn < sf || mn >= st) continue;
            if (filter == "VWAP_DISCOUNT" && (g.Dir > 0 ? g.Px >= g.Vw : g.Px <= g.Vw)) continue;
            if (filter == "VWAP_FLOW" && (g.Dir > 0 ? g.Px <= g.Vw : g.Px >= g.Vw)) continue;
            int p = Period(m.Day[g.At]); if (p < 0) continue;
            int start = g.At; double e = m.O[g.At];
            if (!float.IsNaN(g.Lim)) { start = -1; for (int j = g.At; j < Math.Min(m.N, g.At + 12) && m.Day[j] == m.Day[g.At]; j++) if (g.Dir > 0 ? m.L[j] <= g.Lim - 0.25 : m.H[j] >= g.Lim + 0.05) { start = j; e = g.Lim; break; } if (start < 0) continue; }
            var px = new List<double> { e }; var lt = new List<double> { (double)g.Dir }; double last = e; int adds = 0, hedges = 0, end = start; double res = 0, worstFl = 0, maxU = 1;
            Func<double, double> PL = q => { double t = 0; for (int k = 0; k < px.Count; k++) t += (q - px[k]) * lt[k] - Math.Abs(lt[k]) * cost; return t; };
            int jj = start;
            for (; jj < m.N; jj++)
            {
                if (m.Day[jj] != m.Day[start]) { res = PL(m.C[jj - 1]); end = jj - 1; break; }
                if (m.Min[jj] >= 1015) { res = PL(m.O[jj]); end = jj; break; }
                double net = lt.Sum(); int nd = Math.Sign(net); bool added = false;
                // HEDGE THE LOSER: the price ran HedgeAt against the newest leg → HedgeMult × the net the other way
                if (x.HedgeAt > 0 && hedges < x.MaxHedges && nd != 0 && (nd > 0 ? m.L[jj] <= last - x.HedgeAt : m.H[jj] >= last + x.HedgeAt))
                { last = last - nd * x.HedgeAt; px.Add(last); lt.Add(-nd * Math.Abs(net) * x.HedgeMult); hedges++; added = true; }
                // ADD TO THE WINNER: the price ran AddEvery in favour of the newest leg (before any hedge) → one more unit that way
                else if (x.AddEvery > 0 && adds < x.MaxAdds && hedges == 0 && (nd > 0 ? m.H[jj] >= last + x.AddEvery : m.L[jj] <= last - x.AddEvery))
                { last = last + nd * x.AddEvery; px.Add(last); lt.Add(nd); adds++; added = true; }
                maxU = Math.Max(maxU, lt.Sum(v => Math.Abs(v))); net = lt.Sum();
                double worst = net >= 0 ? m.L[jj] : m.H[jj], best = net >= 0 ? m.H[jj] : m.L[jj];
                worstFl = Math.Min(worstFl, PL(worst));
                if (x.Stop > 0 && PL(worst) <= -x.Stop) { res = -x.Stop; end = jj; break; }
                if (!added && PL(best) >= x.T) { res = x.T; end = jj; break; }
            }
            if (jj >= m.N) { end = m.N - 1; res = PL(m.C[end]); }
            r.N[p]++; if (res > 0) r.Wins[p]++; r.Net[p] += res; r.Worst = Math.Min(r.Worst, res); r.MaxUnits = Math.Max(r.MaxUnits, maxU);
            r.MinEq1 = Math.Min(r.MinEq1, 5000 + eq + worstFl); r.MinEq10 = Math.Min(r.MinEq10, 5000 + 10 * (eq + worstFl));
            r.Dd[p] = Math.Max(r.Dd[p], pk - (eq + worstFl)); eq += res; pk = Math.Max(pk, eq); busyTo = end;
        }
        r.Blown1 = r.MinEq1 <= 0; r.Blown10 = r.MinEq10 <= 0; return r;
    }
    public static void Mixes(M1 m)
    {
        var entries = new[] { ("A London engulfing", "ENGULF_EXTREME", 5, "VWAP_DISCOUNT", 180, 360), ("B NY FVG 50%", "FVG50", 5, "VWAP_FLOW", 510, 690), ("C all-day VWAP rejection", "VWAP_REJECT", 15, "NONE", 180, 930), ("your break, all day", "BRK5", 1, "NONE", 180, 930), ("RANDOM all day", "RANDOM", 1, "NONE", 180, 930) };
        var mixes = new List<Mix>();
        foreach (var t in new[] { 2.0, 3, 5 })
        foreach (var add in new[] { (0.0, 0, "no adds"), (1.0, 2, "add +1 unit every $1 (max 2)"), (2.0, 3, "add every $2 (max 3)") })
        foreach (var h in new[] { (0.0, 0.0, 0, "no hedge"), (2.0, 2.0, 1, "1 hedge 2x at -$2"), (2.0, 2.0, 3, "3 hedges 2x every $2"), (5.0, 2.0, 2, "2 hedges 2x every $5") })
        foreach (var stop in new[] { 0.0, 20, 50 })
            mixes.Add(new Mix { T = t, AddEvery = add.Item1, MaxAdds = add.Item2, HedgeAt = h.Item1, HedgeMult = h.Item2, MaxHedges = h.Item3, Stop = stop, Name = "+$" + t + " | " + add.Item3 + " | " + h.Item4 + " | " + (stop > 0 ? "basket stop -$" + stop : "no stop") + " (0.01 lot)" });
        var jobs = new List<(int e, Mix x)>(); for (int i = 0; i < entries.Length; i++) foreach (var x in mixes) jobs.Add((i, x));
        var sig = entries.Select(en => Signals(m, en.Item3, en.Item2)).ToList();
        var res = new MixRes[jobs.Count]; Parallel.For(0, jobs.Count, i => { var en = entries[jobs[i].e]; res[i] = RunMix(m, sig[jobs[i].e], jobs[i].x, en.Item4, en.Item5, en.Item6); });
        Func<MixRes, bool> ok = r => r.N.All(n => n >= 30) && r.Net.All(v => v > 0) && !r.Blown1;
        var sb = new StringBuilder("entry,setting,net_2025,net_2026,net_2023_24,trades,win_pct,worst_basket,max_units,min_equity_001,min_equity_01,blown_001,blown_01\n");
        for (int i = 0; i < jobs.Count; i++) { var r = res[i]; sb.Append("\"" + entries[jobs[i].e].Item1 + "\",\"" + jobs[i].x.Name + "\"," + F(r.Net[0], 0) + "," + F(r.Net[1], 0) + "," + F(r.Net[2], 0) + "," + r.N.Sum() + "," + F(100.0 * r.Wins.Sum() / Math.Max(1, r.N.Sum()), 1) + "," + F(r.Worst, 0) + "," + F(r.MaxUnits, 0) + "," + F(r.MinEq1, 0) + "," + F(r.MinEq10, 0) + "," + (r.Blown1 ? 1 : 0) + "," + (r.Blown10 ? 1 : 0) + "\n"); }
        System.IO.File.WriteAllText("../../docs/research/hedge/mix001.csv", sb.ToString());
        foreach (var grp in new[] { "no adds | no hedge", "add", "hedge" })
        {
            Func<Mix, bool> inG = x => grp == "no adds | no hedge" ? x.AddEvery == 0 && x.HedgeAt == 0 : grp == "add" ? x.AddEvery > 0 && x.HedgeAt == 0 : x.HedgeAt > 0;
            var idx = Enumerable.Range(0, jobs.Count).Where(i => inG(jobs[i].x) && jobs[i].e < 4).ToList();
            Console.WriteLine(string.Format(IC, "{0,-22} real entries: profitable all 3 periods {1,3}/{2,-3}  blown at 0.01: {3,3}  blown at 0.1: {4,3}  median net/yr ${5:N0}", grp == "add" ? "ADD TO WINNER only" : grp == "hedge" ? "with HEDGES" : "PLAIN (no add/hedge)", idx.Count(i => ok(res[i])), idx.Count, idx.Count(i => res[i].Blown1), idx.Count(i => res[i].Blown10), idx.Select(i => res[i].Net.Sum() / 3.75).OrderBy(v => v).ElementAt(idx.Count / 2)));
        }
        var rnd = Enumerable.Range(0, jobs.Count).Where(i => jobs[i].e == 4).ToList(); Console.WriteLine("RANDOM control: profitable all 3: " + rnd.Count(i => ok(res[i])) + "/" + rnd.Count);
        foreach (var i in Enumerable.Range(0, jobs.Count).Where(i => ok(res[i]) && jobs[i].e < 4).OrderByDescending(i => res[i].Net.Min() - 0 * res[i].Dd.Max()).Take(25))
        { var r = res[i]; Console.WriteLine(string.Format(IC, "  {0,-26} {1,-80} net {2:N0}/{3:N0}/{4:N0}  win {5:0.0}%  trades {6}  worst {7:N0}  DD {8:N0}  max units {9}  min eq 0.01 ${10:N0}  at 0.1 ${11:N0}{12}", entries[jobs[i].e].Item1, jobs[i].x.Name, r.Net[0], r.Net[1], r.Net[2], 100.0 * r.Wins.Sum() / r.N.Sum(), r.N.Sum(), r.Worst, r.Dd.Max(), r.MaxUnits, r.MinEq1, r.MinEq10, r.Blown10 ? " BLOWN at 0.1" : "")); }
    }

    // ===== part 9: SIGNAL HEDGING — the hedge opens only when an OPPOSITE signal fires while the trade is losing Z or more; each leg its own
    // target (LEGS) or the basket closes at breakeven (BE) / at +T (BT); a basket stop S; everything closes at 16:55 NY. Price units:
    // 1.0 = $10 on 0.1 lot. Every leg pays 0.27 a unit (spread + commission); a target = T + 0.20 on the bid.
    public sealed class SH { public string Pri, Hs, Mode; public double Z, M, S, T; public int MaxH; public double[] Net = new double[3]; public int[] N = new int[3], W = new int[3]; public int Hedged; public double Worst, MinEq = 5000, MaxLots; public bool Blown; public double[] Dd = new double[3]; }
    static void RunSH(M1 m, List<Sig> pri, sbyte[] hsig, SH x, string filter, int sf, int st)
    {
        double eq = 0, pk = 0; int busyTo = -1; const double cost = 0.27;
        foreach (var g in pri)
        {
            if (g.At <= busyTo) continue; int mn = m.Min[g.At]; if (mn < sf || mn >= st) continue;
            if (filter == "VWAP_DISCOUNT" && (g.Dir > 0 ? g.Px >= g.Vw : g.Px <= g.Vw)) continue;
            if (filter == "VWAP_FLOW" && (g.Dir > 0 ? g.Px <= g.Vw : g.Px >= g.Vw)) continue;
            int p = Period(m.Day[g.At]); if (p < 0) continue;
            int start = g.At; double e = m.O[g.At];
            if (!float.IsNaN(g.Lim)) { start = -1; for (int j = g.At; j < Math.Min(m.N, g.At + 12) && m.Day[j] == m.Day[g.At]; j++) if (g.Dir > 0 ? m.L[j] <= g.Lim - 0.25 : m.H[j] >= g.Lim + 0.05) { start = j; e = g.Lim; break; } if (start < 0) continue; }
            var px = new List<double> { e }; var lt = new List<double> { (double)g.Dir }; var open = new List<bool> { true }; double realized = 0, worstFl = 0, maxLots = 1; int hedges = 0, end = start, j2 = start; bool done = false;
            Func<double, double> Fl = q => { double t = 0; for (int k = 0; k < px.Count; k++) if (open[k]) t += (q - px[k]) * lt[k] - Math.Abs(lt[k]) * cost; return t; };
            Action<double> CloseAll = q => { realized += Fl(q); for (int k = 0; k < open.Count; k++) open[k] = false; };
            for (; j2 < m.N && !done; j2++)
            {
                if (m.Day[j2] != m.Day[start]) { CloseAll(m.C[j2 - 1]); end = j2 - 1; break; }
                if (m.Min[j2] >= 1015) { CloseAll(m.O[j2]); end = j2; break; }
                double net = 0; for (int k = 0; k < lt.Count; k++) if (open[k]) net += lt[k];
                // the HEDGE: an opposite signal opens at this minute's open while the newest open leg is losing Z or more
                if (x.Hs != "NONE" && hedges < x.MaxH && hsig[j2] != 0 && Math.Sign(hsig[j2]) == -Math.Sign(net) && net != 0)
                {
                    int lastK = -1; for (int k = lt.Count - 1; k >= 0; k--) if (open[k]) { lastK = k; break; }
                    if (lastK >= 0 && (m.O[j2] - px[lastK]) * Math.Sign(lt[lastK]) <= -x.Z) { px.Add(m.O[j2]); lt.Add(hsig[j2] * Math.Abs(net) * x.M); open.Add(true); hedges++; maxLots = Math.Max(maxLots, lt.Where((v, k) => open[k]).Sum(v => Math.Abs(v))); net = 0; for (int k = 0; k < lt.Count; k++) if (open[k]) net += lt[k]; }
                }
                double worst = net >= 0 ? m.L[j2] : m.H[j2], best = net >= 0 ? m.H[j2] : m.L[j2];
                worstFl = Math.Min(worstFl, realized + Fl(worst));
                if (realized + Fl(worst) <= -x.S) { realized = -x.S; for (int k = 0; k < open.Count; k++) open[k] = false; end = j2; break; }
                if (x.Mode == "LEGS" || hedges == 0)
                {   // every open leg at its own target
                    for (int k = 0; k < px.Count; k++) if (open[k] && (lt[k] > 0 ? m.H[j2] >= px[k] + x.T + 0.2 : m.L[j2] <= px[k] - x.T - 0.2)) { realized += (x.T + 0.2 - cost) * Math.Abs(lt[k]); open[k] = false; }
                }
                else
                {   // the basket closes at breakeven (BE) or at +T (BT)
                    double goal = x.Mode == "BE" ? 0.05 : x.T; if (realized + Fl(best) >= goal) { realized = goal; for (int k = 0; k < open.Count; k++) open[k] = false; }
                }
                if (!open.Any(o => o)) { end = j2; break; }
            }
            if (j2 >= m.N) { CloseAll(m.C[m.N - 1]); end = m.N - 1; }
            double usd = realized * 10; x.N[p]++; if (usd > 0) x.W[p]++; x.Net[p] += usd; x.Worst = Math.Min(x.Worst, usd); if (hedges > 0) x.Hedged++; x.MaxLots = Math.Max(x.MaxLots, maxLots / 10);
            x.MinEq = Math.Min(x.MinEq, 5000 + eq + worstFl * 10); x.Dd[p] = Math.Max(x.Dd[p], pk - (eq + worstFl * 10)); eq += usd; pk = Math.Max(pk, eq); busyTo = end;
            if (5000 + eq <= 0) { x.Blown = true; return; }
        }
        x.Blown = x.Blown || x.MinEq <= 0;
    }
    public static void SigHedge(M1 m)
    {
        var pris = new[] { ("A London engulfing", "ENGULF_EXTREME", 5, "VWAP_DISCOUNT", 180, 360), ("B NY FVG 50%", "FVG50", 5, "VWAP_FLOW", 510, 690), ("C all-day VWAP rejection", "VWAP_REJECT", 15, "NONE", 180, 930), ("your break M1 all day", "BRK5", 1, "NONE", 180, 930) };
        var hss = new[] { ("NONE", "", 0), ("VWAP rejection M5", "VWAP_REJECT", 5), ("engulfing M5", "ENGULF_EXTREME", 5), ("stop-hunt sweep M5", "SWEEP", 5), ("break of 5 M5", "BRK5", 5), ("RANDOM M5 (control)", "RANDOM", 5) };
        var hmap = new Dictionary<string, sbyte[]>();
        foreach (var h in hss.Skip(1)) { var a = new sbyte[m.N]; foreach (var g in Signals(m, h.Item3, h.Item2)) a[g.At] = g.Dir; hmap[h.Item1] = a; }
        var priSig = pris.Select(pp => Signals(m, pp.Item3, pp.Item2)).ToList();
        var jobs = new List<(int pi, SH x)>();
        for (int pi = 0; pi < pris.Length; pi++) foreach (var T in new[] { 2.0, 3.0 }) foreach (var S in new[] { 20.0, 40.0 })
        {
            jobs.Add((pi, new SH { Pri = pris[pi].Item1, Hs = "NONE", Mode = "-", T = T, S = S }));
            foreach (var h in hss.Skip(1)) foreach (var Z in new[] { 0.5, 2.0, 5.0 }) foreach (var M in new[] { 1.0, 2.0 }) foreach (var mode in new[] { "LEGS", "BE", "BT" }) foreach (var mh in new[] { 1, 2 })
                jobs.Add((pi, new SH { Pri = pris[pi].Item1, Hs = h.Item1, Mode = mode, T = T, S = S, Z = Z, M = M, MaxH = mh }));
        }
        Parallel.For(0, jobs.Count, i => { var pp = pris[jobs[i].pi]; var x = jobs[i].x; RunSH(m, priSig[jobs[i].pi], x.Hs == "NONE" ? new sbyte[m.N] : hmap[x.Hs], x, pp.Item4, pp.Item5, pp.Item6); });
        Func<SH, bool> ok = x => !x.Blown && x.N.All(n => n >= 30) && x.Net.All(v => v > 0);
        Func<SH, string> nm = x => x.Hs == "NONE" ? "NO HEDGE | +$" + x.T * 10 + " | stop -$" + x.S * 10 : "hedge on " + x.Hs + " after -$" + x.Z * 10 + " | " + x.M + "x | " + x.MaxH + " max | " + (x.Mode == "LEGS" ? "each leg its own +$" + x.T * 10 : x.Mode == "BE" ? "basket out at breakeven" : "basket at +$" + x.T * 10) + " | stop -$" + x.S * 10;
        var all = jobs.Select(j => j.x).ToList();
        Console.WriteLine("SIGNAL HEDGING on 0.1 lot, $5,000 — profitable in 2025, 2026 AND 2023-24, never blown:");
        foreach (var h in hss) { var g = all.Where(x => x.Hs == h.Item1).ToList(); Console.WriteLine(string.Format(IC, "  {0,-22} {1,4}/{2,-4}  blown {3,4}  median net (all periods) ${4:N0}", h.Item1, g.Count(ok), g.Count, g.Count(x => x.Blown), g.Select(x => x.Net.Sum()).OrderBy(v => v).ElementAt(g.Count / 2))); }
        // hedge vs the same entry with no hedge (same T and S)
        int better = 0, tot = 0; foreach (var x in all.Where(x => x.Hs != "NONE" && !x.Hs.StartsWith("RANDOM"))) { var b = all.First(y => y.Pri == x.Pri && y.Hs == "NONE" && y.T == x.T && y.S == x.S); tot++; if (x.Net.Sum() > b.Net.Sum() && x.Net.Min() > b.Net.Min()) better++; }
        Console.WriteLine("  hedged settings better than the same entry without a hedge (total AND worst period): " + better + " / " + tot);
        var sb = new StringBuilder("primary,setting,net_2025,net_2026,net_2023_24,trades,win_pct,hedged,worst,max_lots,min_equity,blown\n");
        foreach (var x in all) sb.Append("\"" + x.Pri + "\",\"" + nm(x) + "\"," + F(x.Net[0], 0) + "," + F(x.Net[1], 0) + "," + F(x.Net[2], 0) + "," + x.N.Sum() + "," + F(100.0 * x.W.Sum() / Math.Max(1, x.N.Sum()), 1) + "," + x.Hedged + "," + F(x.Worst, 0) + "," + F(x.MaxLots, 1) + "," + F(x.MinEq, 0) + "," + (x.Blown ? 1 : 0) + "\n");
        System.IO.File.WriteAllText("../../docs/research/hedge/sighedge.csv", sb.ToString());
        Console.WriteLine("BEST hedged (by the worst period):");
        foreach (var x in all.Where(x => ok(x) && x.Hs != "NONE").OrderByDescending(x => x.Net.Min()).Take(20))
        { var b = all.First(y => y.Pri == x.Pri && y.Hs == "NONE" && y.T == x.T && y.S == x.S); Console.WriteLine(string.Format(IC, "  {0,-24} {1,-95} {2:N0}/{3:N0}/{4:N0} won {5:0}% hedged {6} worst {7:N0} DD {8:N0} lots {9}   | no hedge: {10:N0}/{11:N0}/{12:N0}", x.Pri, nm(x), x.Net[0], x.Net[1], x.Net[2], 100.0 * x.W.Sum() / x.N.Sum(), x.Hedged, x.Worst, x.Dd.Max(), x.MaxLots, b.Net[0], b.Net[1], b.Net[2])); }
    }
    static string F(double v, int d) { if (double.IsNaN(v) || double.IsInfinity(v)) v = 0; return v.ToString("F" + d, IC); }
}
