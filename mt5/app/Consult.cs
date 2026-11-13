// SpasiX — 🧭 HISTORY CONSULT (09r, the user's request: "the hedging algo should always, in the background, compare setups / patterns from the history
// data it has been saving, especially for managing hedging — consult in the background for better real-time hedging decisions for certain setups").
// MEMORY: every hedge decision point of every saved basket (this account's own baskets, measured in the background after each basket closes, + a
// 6-year library from the bench, consult-library.txt.gz) with its features at that moment (finished candles only — no lookahead) and what happened next
// for each option, measured by replaying the SAME basket from that point in an in-memory SimBroker (never MT5): 🛡 HEDGE NOW (what the engine did),
// ⏳ WAIT FOR THE NEXT SWING, 🔐 LOCK (net 0 until the break), ➕ ADD WITH THE TREND, ✖ CLOSE NOW — each followed by the engine's own rules.
// CONSULT: at a live hedge decision the k nearest past points (standardised features, only points whose outcome was KNOWN before now = walk-forward)
// → per option mean + 95 % interval; it RECOMMENDS only when n ≥ MIN N and the best option beats HEDGE NOW on the same neighbours by an interval
// that excludes 0 — else "no clear answer — your rules". Acting: ADVISE (default, sends nothing) / ACT ON SIMULATED (DEMO / BACKTEST / RACE) /
// ACT ON THIS ACCOUNT (MT5 only after the typed CONSULT, every session — ConsultGate). Every action goes through HedgeAuto's Ask → AutoSend.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace KeystoneMT5
{
    public static class ConsultOpt
    {
        public const string HEDGE = "HEDGE", WAIT = "WAIT", LOCK = "LOCK", ADD = "ADD", CLOSE = "CLOSE";
        public static readonly string[] All = { HEDGE, WAIT, LOCK, ADD, CLOSE };
        public static string Name(string o)
        {
            return o == HEDGE ? "🛡 hedge now" : o == WAIT ? "⏳ wait for the next swing" : o == LOCK ? "🔐 lock (net 0)" : o == ADD ? "➕ add with the trend" : o == CLOSE ? "✖ close now" : o;
        }
        public static string Short(string o) { return o == HEDGE ? "hedge now" : o == WAIT ? "wait for the swing" : o == LOCK ? "lock" : o == ADD ? "add with the trend" : o == CLOSE ? "close now" : o; }
    }

    // where a memory point comes from: LIB = the built-in 6-year library (an ESTIMATE — futures candles, synthetic ticks: weighted below the rest),
    // MINE = this account's own live / DEMO baskets, TICKS = built from the user's downloaded / recorded real XAUUSD ticks (🧭 BUILD FROM MY TICKS)
    public static class ConsultSrc
    {
        public const string LIB = "LIB", MINE = "MINE", TICKS = "TICKS";
        public const string LibLabel = "estimate — futures candles, synthetic ticks";
        public static bool Real(string src) { return src != LIB; }
    }

    // what happened next for one option: the basket's result (× its target), the worst total after the point (× target, ≤ 0), minutes to the exit
    public sealed class ConsultOutcome { public double ResultX = double.NaN, WorstX = double.NaN, Minutes = double.NaN; public bool Closed; public string Reason = ""; }

    // the basket's state at a decision (filled by HedgeAuto; tests by hand)
    public sealed class ConsultState
    {
        public DateTime At, IterStart; public int Side, HedgeN, NyOffset = -7; public double Total, Target, LegHi = double.NaN, LegLo = double.NaN, Spread; public string Setup = "";
    }

    public sealed class ConsultPoint
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public DateTime At, EndAt; public string Src = "LIB", Setup = ""; public int Iter; public double Target; public double[] X;
        public readonly Dictionary<string, ConsultOutcome> Out = new Dictionary<string, ConsultOutcome>();
        public bool Complete { get { return X != null && ConsultOpt.All.All(o => Out.ContainsKey(o) && !double.IsNaN(Out[o].ResultX)); } }
        public double R(string o) { ConsultOutcome c; return Out.TryGetValue(o, out c) ? c.ResultX : double.NaN; }
        public string Line()
        {
            return string.Join("|", "P", At.ToString("yyyyMMddHHmm", IC), EndAt.ToString("yyyyMMddHHmm", IC), Src, (Setup ?? "").Replace("|", "/"), Iter.ToString(IC), Target.ToString("0.##", IC),
                string.Join(",", X.Select(v => v.ToString("0.###", IC))),
                string.Join(";", ConsultOpt.All.Where(o => Out.ContainsKey(o)).Select(o => o + ":" + Out[o].ResultX.ToString("0.###", IC) + ":" + Out[o].WorstX.ToString("0.##", IC) + ":" + Out[o].Minutes.ToString("0", IC) + ":" + (Out[o].Closed ? 1 : 0))));
        }
        public static ConsultPoint Parse(string l)
        {
            try
            {
                var a = l.Split('|'); if (a.Length < 9 || a[0] != "P") return null;
                var p = new ConsultPoint { At = DateTime.ParseExact(a[1], "yyyyMMddHHmm", IC), EndAt = DateTime.ParseExact(a[2], "yyyyMMddHHmm", IC), Src = a[3], Setup = a[4], Iter = int.Parse(a[5], IC), Target = double.Parse(a[6], IC),
                    X = a[7].Split(',').Select(v => double.Parse(v, IC)).ToArray() };
                foreach (var kv in a[8].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                { var q = kv.Split(':'); p.Out[q[0]] = new ConsultOutcome { ResultX = double.Parse(q[1], IC), WorstX = double.Parse(q[2], IC), Minutes = double.Parse(q[3], IC), Closed = q.Length > 4 && q[4] == "1" }; }
                return p.X.Length == ConsultFeatures.Names.Length ? p : null;
            }
            catch { return null; }
        }
    }

    // the features of a decision point — ONLY finished 1-minute candles before `At` are read (bars after it never change the answer)
    public static class ConsultFeatures
    {
        public static readonly string[] Names = { "hedge #", "depth × target", "range × ATR", "minutes in basket (log)", "EMA 50/200 vs the net", "60-min move × ATR vs the net", "hour (sin)", "hour (cos)", "VWAP distance × ATR vs the net", "spread × ATR", "triple swap tonight" };
        // the distance scale of each feature (fixed, so a point's neighbours never depend on later data)
        public static readonly double[] Scale = { 2, 4, 3, 1, 1, 2, 0.7, 0.7, 2, 0.3, 2 };
        // the index of the last finished candle at `at` (Time + 1 min ≤ at); bars oldest first
        public static int LastFinished(IList<Mt5Bar> m1, DateTime at)
        {
            int lo = 0, hi = m1.Count - 1, r = -1; var lim = at.AddMinutes(-1);
            while (lo <= hi) { int mid = (lo + hi) / 2; if (m1[mid].Time <= lim) { r = mid; lo = mid + 1; } else hi = mid - 1; }
            return r;
        }
        public static double[] Compute(IList<Mt5Bar> m1, ConsultState s)
        {
            var x = new double[Names.Length]; int last = m1 == null ? -1 : LastFinished(m1, s.At); int side = s.Side >= 0 ? 1 : -1;
            double atr = double.NaN; if (last >= 0) { int n = 0; double sum = 0; for (int i = last; i >= 0 && n < 30; i--, n++) sum += m1[i].H - m1[i].L; atr = n > 0 ? Math.Max(1e-6, sum / n) : double.NaN; }
            if (double.IsNaN(atr) || atr <= 0) atr = 1;
            x[0] = Math.Min(12, s.HedgeN);
            x[1] = s.Target > 0 ? Math.Max(-40, Math.Min(5, s.Total / s.Target)) : 0;
            x[2] = !double.IsNaN(s.LegHi) && !double.IsNaN(s.LegLo) ? Math.Min(40, (s.LegHi - s.LegLo) / atr) : 0;
            x[3] = Math.Log(1 + Math.Max(0, (s.At - s.IterStart).TotalMinutes));
            if (last >= 200)
            {
                int first = Math.Max(0, last - 1200); double e5 = double.NaN, e2 = double.NaN;
                for (int i = first; i <= last; i++) { double c = m1[i].C; e5 = double.IsNaN(e5) ? c : c * 2 / 51.0 + e5 * (1 - 2 / 51.0); e2 = double.IsNaN(e2) ? c : c * 2 / 201.0 + e2 * (1 - 2 / 201.0); }
                double cl = m1[last].C; int tr = e5 > e2 && cl > e2 ? 1 : e5 < e2 && cl < e2 ? -1 : 0; x[4] = tr * side;
            }
            if (last >= 0)
            {
                double c0 = m1[last].C; int j = last; while (j > 0 && m1[j].Time > m1[last].Time.AddMinutes(-60)) j--;
                x[5] = Math.Max(-15, Math.Min(15, (c0 - m1[j].C) / atr)) * side;
                double pv = 0, vv = 0; var day = m1[last].Time.Date; for (int i = last; i >= 0 && m1[i].Time.Date == day; i--) { double v = Math.Max(1, m1[i].V); pv += (m1[i].H + m1[i].L + m1[i].C) / 3 * v; vv += v; }
                x[8] = vv > 0 ? Math.Max(-15, Math.Min(15, (c0 - pv / vv) / atr)) * side : 0;
            }
            var ny = s.At.AddHours(s.NyOffset); double h = ny.Hour + ny.Minute / 60.0; x[6] = Math.Sin(2 * Math.PI * h / 24); x[7] = Math.Cos(2 * Math.PI * h / 24);
            x[9] = Math.Min(3, Math.Max(0, s.Spread) / atr);
            x[10] = s.At.DayOfWeek == DayOfWeek.Wednesday ? 1 : 0;
            for (int i = 0; i < x.Length; i++) x[i] = Math.Round(x[i], 3);
            return x;
        }
        public static double Dist(double[] a, double[] b, string sa, string sb)
        {
            double d = 0; for (int i = 0; i < a.Length; i++) { double z = (a[i] - b[i]) / Scale[i]; d += z * z; }
            if (!string.Equals(sa ?? "", sb ?? "", StringComparison.OrdinalIgnoreCase)) d += 1;   // another entry setup = one scale step further
            return Math.Sqrt(d);
        }
    }

    public sealed class ConsultRow { public string Opt = ""; public int N; public double Mean, Lo, Hi, Recov, Worst, Minutes, Diff, DiffLo, DiffHi; }
    public sealed class ConsultAdvice
    {
        public int N, Library, Mine; public double Target; public string Says = "", Best = "", Why = "", Line = ""; public DateTime At; public string Key = "";
        public List<ConsultRow> Rows = new List<ConsultRow>(); public List<ConsultPoint> Neighbours = new List<ConsultPoint>();
        public bool Clear { get { return Says != ""; } }
        // where the neighbours came from — the library is always labelled as an estimate
        public string SourceNote { get { return N == 0 ? "" : Library == 0 ? " [your real ticks: " + Mine + "]" : Mine == 0 ? " [library — " + ConsultSrc.LibLabel + "]" : " [" + Mine + " yours + " + Library + " library — " + ConsultSrc.LibLabel + "]"; } }
        public ConsultRow Row(string o) { return Rows.FirstOrDefault(r => r.Opt == o); }
    }

    public static class ConsultAdvisor
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const int DefaultK = 40, DefaultMinN = 30;
        static double Sd(List<double> v) { if (v.Count < 2) return 0; double m = v.Average(); return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Count - 1)); }
        // the k nearest COMPLETE points whose outcome was known before `now` (walk-forward: EndAt ≤ now) — deterministic (distance, then time).
        // The user's REAL-tick points come first: when at least `realFirst` of them are known, ONLY real-tick points are used; else the library
        // (an estimate — futures candles, synthetic ticks) fills in AFTER all the real ones (weighted below them).
        public static List<ConsultPoint> Nearest(IEnumerable<ConsultPoint> mem, double[] x, string setup, DateTime now, int k, int realFirst = DefaultMinN)
        {
            var real = new List<Tuple<double, ConsultPoint>>(); var lib = new List<Tuple<double, ConsultPoint>>();
            foreach (var p in mem)
            {
                if (p == null || p.EndAt > now || p.At >= now || !p.Complete) continue;
                double d = ConsultFeatures.Dist(x, p.X, setup, p.Setup);
                if (ConsultSrc.Real(p.Src)) real.Add(Tuple.Create(d, p)); else lib.Add(Tuple.Create(d, p));
            }
            Comparison<Tuple<double, ConsultPoint>> cmp = (a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.At.CompareTo(b.Item2.At);
            real.Sort(cmp); if (real.Count >= Math.Max(1, Math.Min(k, realFirst))) return real.Take(k).Select(t => t.Item2).ToList();
            lib.Sort(cmp); return real.Take(k).Concat(lib.Take(Math.Max(0, k - real.Count))).Select(t => t.Item2).ToList();   // all of yours first, the library only fills the rest
        }
        // the honesty gate: a recommendation only with n ≥ minN AND the best option beats HEDGE NOW on the same neighbours with a 95 % interval above `margin` (× target)
        public static ConsultAdvice Advise(IEnumerable<ConsultPoint> mem, double[] x, string setup, DateTime now, double target, int k = DefaultK, int minN = DefaultMinN, double margin = 0)
        {
            var a = new ConsultAdvice { At = now, Target = target }; var nb = Nearest(mem, x, setup, now, Math.Max(1, k), minN); a.Neighbours = nb; a.N = nb.Count; a.Mine = nb.Count(p => ConsultSrc.Real(p.Src)); a.Library = a.N - a.Mine;
            double tg = target > 0 ? target : 1;
            foreach (var o in ConsultOpt.All)
            {
                var v = nb.Select(p => p.R(o)).ToList(); var w = nb.Select(p => p.Out[o].WorstX).ToList(); var d = nb.Select(p => p.R(o) - p.R(ConsultOpt.HEDGE)).ToList();
                var r = new ConsultRow { Opt = o, N = v.Count };
                if (v.Count > 0)
                {
                    r.Mean = v.Average() * tg; double se = Sd(v) / Math.Sqrt(v.Count) * tg; r.Lo = r.Mean - 1.96 * se; r.Hi = r.Mean + 1.96 * se;
                    r.Recov = 100.0 * v.Count(z => z >= -1e-9) / v.Count; r.Worst = w.Average() * tg; r.Minutes = nb.Average(p => double.IsNaN(p.Out[o].Minutes) ? 0 : p.Out[o].Minutes);
                    r.Diff = d.Average() * tg; double sd = Sd(d) / Math.Sqrt(d.Count) * tg; r.DiffLo = r.Diff - 1.96 * sd; r.DiffHi = r.Diff + 1.96 * sd;
                }
                a.Rows.Add(r);
            }
            var h = a.Row(ConsultOpt.HEDGE); var bestRow = a.Rows.Where(r => r.Opt != ConsultOpt.HEDGE).OrderByDescending(r => r.Diff).ThenBy(r => Array.IndexOf(ConsultOpt.All, r.Opt)).FirstOrDefault();
            a.Best = bestRow != null && a.N > 0 && bestRow.Diff > 0 ? bestRow.Opt : ConsultOpt.HEDGE;
            if (a.N == 0) { a.Why = "no similar decision in the memory yet"; a.Line = "🧭 CONSULT: no similar decision in the memory yet — your rules"; return a; }
            if (a.N < minN) a.Why = "only " + a.N + " similar (needs " + minN + ")";
            else if (bestRow == null || bestRow.DiffLo <= margin * tg) a.Why = "no option beats hedge now clearly (the 95 % interval includes " + (margin > 0 ? "the margin" : "0") + ")";
            else { a.Says = bestRow.Opt; a.Why = ConsultOpt.Short(bestRow.Opt) + " beat hedge now by " + Usd(bestRow.Diff) + " a basket (95 %: " + Usd(bestRow.DiffLo) + " … " + Usd(bestRow.DiffHi) + ")"; }
            var shown = a.Rows.Where(r => r.Opt != ConsultOpt.HEDGE).OrderByDescending(r => r.Diff).First();
            a.Line = "🧭 " + a.N + " similar: " + ConsultOpt.Short(shown.Opt) + " recovered " + shown.Recov.ToString("0", IC) + " % (" + Usd(shown.Mean) + " avg) vs hedge now " + h.Recov.ToString("0", IC) + " % (" + Usd(h.Mean) + ")"
                + (a.Clear ? " — CONSULT says " + a.Says : " — no clear answer, your rules (" + a.Why + ")") + a.SourceNote;
            return a;
        }
        public static string Usd(double v) { return (v >= 0 ? "+$" : "−$") + Math.Abs(v).ToString(Math.Abs(v) >= 100 ? "N0" : "0.00", IC); }

        // its own track record: each point (in time order) advised from the points known BEFORE it; when it said something else than HEDGE NOW,
        // what that did vs the engine's hedge (× target → $ at each point's own target)
        public sealed class Track { public int Points, Calls, Better; public double Gain, GainCalls; public Dictionary<string, int> ByOpt = new Dictionary<string, int>(); public string Text = ""; }
        public static Track TrackOf(IList<ConsultPoint> mem, IEnumerable<ConsultPoint> judge, int k = DefaultK, int minN = DefaultMinN)
        {
            var t = new Track();
            foreach (var p in judge.Where(z => z.Complete).OrderBy(z => z.At))
            {
                t.Points++; var a = Advise(mem, p.X, p.Setup, p.At, p.Target, k, minN);
                if (!a.Clear) continue; t.Calls++; int c; t.ByOpt.TryGetValue(a.Says, out c); t.ByOpt[a.Says] = c + 1;
                double g = (p.R(a.Says) - p.R(ConsultOpt.HEDGE)) * p.Target; t.Gain += g; if (g > 0) t.Better++;
            }
            t.Text = t.Points == 0 ? "no measured decision yet" : t.Calls == 0 ? t.Points + " decisions judged: it never had a clear answer (it stayed with your rules)" :
                t.Points + " decisions judged, " + t.Calls + " clear calls (" + string.Join(", ", t.ByOpt.OrderByDescending(z => z.Value).Select(z => ConsultOpt.Short(z.Key) + " " + z.Value)) + "): " + Usd(t.Gain) + " vs the engine's hedge, better in " + (100.0 * t.Better / t.Calls).ToString("0", IC) + " %";
            return t;
        }
    }

    // the memory: the 6-year library (embedded, read once) + this account's own measured points (…-consult.csv); thread-safe snapshot for the engine
    public sealed class ConsultMemory
    {
        readonly object gate = new object(); List<ConsultPoint> all = new List<ConsultPoint>(); public string LibraryNote = ""; public string TrackLine = "";
        public IList<ConsultPoint> Points { get { lock (gate) return all; } }
        public int Count { get { lock (gate) return all.Count; } }
        public int MineCount { get { lock (gate) return all.Count(p => ConsultSrc.Real(p.Src)); } }
        public void Add(IEnumerable<ConsultPoint> pts) { lock (gate) { var n = new List<ConsultPoint>(all); var seen = new HashSet<string>(n.Select(q => q.Src + "|" + q.At.Ticks + "|" + q.Iter)); foreach (var p in pts) if (p != null && p.X != null && seen.Add(p.Src + "|" + p.At.Ticks + "|" + p.Iter)) n.Add(p); all = n; } }
        public static List<ConsultPoint> ParseText(string text, out string note, out string track)
        {
            var r = new List<ConsultPoint>(); note = ""; track = "";
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var l = raw.TrimEnd('\r'); if (l.StartsWith("NOTE|")) note = l.Substring(5); else if (l.StartsWith("TRACK|")) track = l.Substring(6);
                else if (l.StartsWith("P|")) { var p = ConsultPoint.Parse(l); if (p != null) r.Add(p); }
            }
            return r;
        }
        static ConsultMemory lib; static readonly object libGate = new object();
        // the embedded 6-year library (consult-library.txt.gz) — a fresh memory each call (shares the library's points)
        public static ConsultMemory WithLibrary()
        {
            var m = new ConsultMemory();
            lock (libGate)
            {
                if (lib == null)
                {
                    lib = new ConsultMemory();
                    try
                    {
                        using (var st = typeof(ConsultMemory).Assembly.GetManifestResourceStream("KeystoneMT5.consult-library.txt.gz"))
                            if (st != null) using (var gz = new GZipStream(st, CompressionMode.Decompress)) using (var rd = new StreamReader(gz, Encoding.UTF8)) { string n, t; lib.all = ParseText(rd.ReadToEnd(), out n, out t); lib.LibraryNote = n; lib.TrackLine = t; }
                    }
                    catch { }
                }
                m.all = new List<ConsultPoint>(lib.all); m.LibraryNote = lib.LibraryNote; m.TrackLine = lib.TrackLine;
            }
            return m;
        }
        public static void SetLibrary(string text) { lock (libGate) { lib = new ConsultMemory(); string n, t; lib.all = ParseText(text, out n, out t); lib.LibraryNote = n; lib.TrackLine = t; } }   // tests / the bench
        public static List<ConsultPoint> LoadFile(string file) { try { if (File.Exists(file)) { string n, t; return ParseText(File.ReadAllText(file), out n, out t); } } catch { } return new List<ConsultPoint>(); }
        public static void SaveFile(string file, IEnumerable<ConsultPoint> pts) { try { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllLines(file, pts.Select(p => p.Line())); } catch { } }
    }

    // a decision point waiting for its measurement: the basket just before the decision (legs, what it had banked) + the features
    public sealed class ConsultCase
    {
        public ConsultPoint Point = new ConsultPoint(); public WhatIfCase Snap = new WhatIfCase(); public string Engine = ConsultOpt.HEDGE;
        public bool Measured;
    }

    // the replays: the same basket from the decision, each option once, then the engine's own rules — in an in-memory SimBroker only (never MT5)
    public static class ConsultSim
    {
        public const int HorizonMin = 3 * 1440;   // an option still open after 3 days counts at its total then (marked to market)
        public static ConsultOutcome Run(WhatIfCase c, string opt, List<Mt5Bar> m1, SimSpec spec, int horizonMin = HorizonMin, List<SimTick> ticks = null)
        {
            var o = new ConsultOutcome(); double tg = c.Target > 0 ? c.Target : 1;
            var end = c.SnapAt.AddMinutes(horizonMin); var win = m1.Where(b => b.Time >= c.SnapAt.AddMinutes(-1300) && b.Time <= end).ToList();
            if (win.Count(b => b.Time >= c.SnapAt) < 2) { o.Reason = "no candles after the point"; return o; }
            var sb = new SimBroker(); var from = c.SnapAt.Date;
            var t0 = win[0].Time; var rtk = ticks != null ? ticks.Where(t => t.T >= t0 && t.T <= end.AddMinutes(1)).ToList() : null; if (rtk != null && rtk.Count(t => t.T >= c.SnapAt) < 10) rtk = null;   // 🎞 the real ticks where they exist
            var err = sb.Load(new SimSettings { Symbol = c.Symbol, From = from, To = win[win.Count - 1].Time.Date, Balance = c.Balance, CommissionPerLot = c.Commission, Leverage = c.Leverage, Kind = c.Kind, FixedSpread = rtk != null ? -1 : c.Spread, StopOutPct = 50, StartNyHour = (c.SnapAt - from).TotalHours + spec.NyOffsetHours }, new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> { Tuple.Create(spec, win, rtk) });
            if (err != "") { o.Reason = "no replay: " + err; return o; }
            sb.Playing = true; sb.Speed = 60; sb.Advance(0.02);
            lock (sb.State.Gate)
            {
                var lines = sb.Account.Save().Where(l => !l.StartsWith("P|")).ToList();
                foreach (var l in c.Legs) lines.Add("P|" + l.Ticket + "|" + c.Symbol + "|" + l.Side + "|" + l.Lots.ToString("R", CultureInfo.InvariantCulture) + "|" + l.Open.ToString("R", CultureInfo.InvariantCulture) + "|0|0|" + l.Time.Ticks + "|0|0");
                sb.Account.Load(lines);
            }
            var s = AutoSettings.Load(c.Settings); s.Symbol = c.Symbol; s.TrackManual = false; s.OnManual = "ADOPT"; s.TrendStack = false; s.StepIn = false; s.ConsultMode = "ADVISE"; s.MaxIterations = 1; s.NextStart = "PAUSE"; s.PauseSec = 999999;
            var au = new HedgeAuto(); au.Send = (cmd, a, lab) => sb.Command(cmd, a); au.ConsultForce = opt; au.StopOutPct = 50;
            var tk = new HashSet<long>(c.Legs.Select(l => l.Ticket)); double balStart; lock (sb.State.Gate) balStart = sb.State.Account.Balance;
            au.RestoreOpen = () => new HedgeAuto.OpenSave { It = new AutoIteration { N = c.N, Start = c.Start, FirstDir = c.Legs.Count > 0 ? c.Legs.OrderBy(l => l.Time).First().Side : "BUY", BalanceStart = balStart - c.Banked, MaxDd = c.MaxDd }, Tickets = tk, SavedAt = c.SnapAt };
            double worst = 0; int guardIt = 0; double lastWorst = 0;
            sb.OnTickHook = () =>
            {
                au.Step(sb); if (au.Current == null) return;
                double tot = au.LiveTotal().Item1; if (tot < worst) worst = tot;
                if (guardIt == 0 || tot >= au.Set.GuardKeep - 0.01 || tot < lastWorst - 5) { guardIt = 1; if (tot < lastWorst) lastWorst = tot; au.GuardStep(sb, au.Set); }
            };
            if (au.Check(sb, s) != "") { o.Reason = "no replay: " + au.Check(sb, s); return o; }
            au.Start(sb, s, true);
            for (int k = 0; k < 4000000 && !sb.Ended; k++)
            {
                sb.Advance(0.5); var it = au.Iterations.FirstOrDefault(x => x.N == c.N);
                if (it != null && it.Done)
                {
                    double bal; lock (sb.State.Gate) bal = sb.State.Account.Balance;
                    o.ResultX = Math.Round((bal - balStart + c.Banked) / tg, 3); o.WorstX = Math.Round(Math.Min(0, Math.Min(worst, c.MaxDd)) / tg, 2); o.Minutes = Math.Max(0, (it.End - c.SnapAt).TotalMinutes); o.Closed = true; o.Reason = it.Reason; return o;
                }
                if (!au.Running && it == null) break;
            }
            // still open at the horizon (or the data's end): marked to market
            double eq, b0; lock (sb.State.Gate) { eq = sb.State.Account.Equity; b0 = sb.State.Account.Balance; }
            o.ResultX = Math.Round((eq - balStart + c.Banked) / tg, 3); o.WorstX = Math.Round(Math.Min(0, Math.Min(worst, c.MaxDd)) / tg, 2); o.Minutes = Math.Max(0, (sb.State.ServerNow - c.SnapAt).TotalMinutes); o.Closed = false; o.Reason = "still open (marked to market)";
            return o;
        }
        // all five options of one case → the point's outcomes (EndAt = when the slowest option was known)
        public static void Measure(ConsultCase cc, List<Mt5Bar> m1, SimSpec spec, int horizonMin = HorizonMin, List<SimTick> ticks = null)
        {
            var p = cc.Point; DateTime endAt = p.At;
            foreach (var o in ConsultOpt.All) { var r = Run(cc.Snap, o, m1, spec, horizonMin, ticks); p.Out[o] = r; if (!double.IsNaN(r.Minutes)) { var e = p.At.AddMinutes(r.Minutes); if (e > endAt) endAt = e; } }
            p.EndAt = endAt; cc.Measured = true;
        }
    }

    // 🧭 BUILD FROM MY TICKS: the user's settings replayed on the REAL ticks he downloaded (🧪 BACKTEST ⬇ DOWNLOAD REAL TICKS) or recorded live, every hedge
    // decision kept and its five options measured on the same real ticks — points with Src = TICKS (they outrank the library). In-memory SimBroker only.
    public static class ConsultBuilder
    {
        // 1-minute candles from ticks (bid) — for days whose candles are not on this PC
        public static List<Mt5Bar> BarsFromTicks(IEnumerable<SimTick> ticks)
        {
            var r = new List<Mt5Bar>(); Mt5Bar cur = null;
            foreach (var t in ticks)
            {
                var m = new DateTime(t.T.Ticks - t.T.Ticks % TimeSpan.TicksPerMinute);
                if (cur == null || cur.Time != m) { cur = new Mt5Bar { Time = m, O = t.Bid, H = t.Bid, L = t.Bid, C = t.Bid, V = 0, Spread = 0 }; r.Add(cur); }
                if (t.Bid > cur.H) cur.H = t.Bid; if (t.Bid < cur.L) cur.L = t.Bid; cur.C = t.Bid; cur.V++;
            }
            return r;
        }
        // the runs of consecutive tick days (a gap of more than 3 calendar days starts a new run; at most `maxDays` days a run)
        public static List<List<DateTime>> Runs(IEnumerable<DateTime> days, int maxDays = 20)
        {
            var res = new List<List<DateTime>>(); List<DateTime> cur = null;
            foreach (var d in days.Select(x => x.Date).Distinct().OrderBy(x => x)) { if (cur == null || (d - cur[cur.Count - 1]).TotalDays > 3 || cur.Count >= maxDays) { cur = new List<DateTime>(); res.Add(cur); } cur.Add(d); }
            return res;
        }
        // one run: the settings on the real ticks, every decision → its options measured (horizon = the run's end, marked to market there)
        public static List<ConsultPoint> FromRun(AutoSettings set, SimSpec spec, List<Mt5Bar> bars, List<SimTick> ticks, double balance, Func<bool> cancel = null, Action<string> progress = null)
        {
            var pts = new List<ConsultPoint>(); if (ticks == null || ticks.Count < 100) return pts;
            if (bars == null || bars.Count == 0) bars = BarsFromTicks(ticks);
            var s = set.Copy(); s.TrackManual = false; s.OnManual = "ADOPT"; s.StepIn = false; s.TrendStack = false; s.ConsultMode = "ADVISE"; s.Windows = s.Windows ?? "";
            var first = ticks[0].T.Date; var last = ticks[ticks.Count - 1].T;
            var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = s.Symbol, From = first, To = last.Date, Balance = balance, Leverage = 500, Kind = "STANDARD", FixedSpread = -1, StopOutPct = 50 }, new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> { Tuple.Create(spec, bars, ticks) });
            if (err != "") { if (progress != null) progress("✖ " + err); return pts; }
            var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); au.StopOutPct = 50; var cases = new List<ConsultCase>(); au.OnConsultPoint += cc => cases.Add(cc);
            sb.OnTickHook = () => { au.Step(sb); if (au.Current != null) au.GuardStep(sb, au.Set); };
            if (au.Check(sb, s) != "") { if (progress != null) progress("✖ " + au.Check(sb, s)); return pts; }
            au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
            for (long k = 0; k < 50000000 && !sb.Ended; k++)
            {
                sb.Advance(0.5); if ((k & 1023) == 0 && cancel != null && cancel()) return pts;
                if (!au.Running) { double b1; int open; lock (sb.State.Gate) { b1 = sb.State.Account.Balance; open = sb.State.Positions.Count; } if (open > 0) { sb.Command("CLOSEALL", s.Symbol, "ALL"); continue; } if (b1 < 0.1 * balance) lock (sb.State.Gate) sb.Account.SetBalance(balance, sb.State.ServerNow); if (au.Check(sb, s) == "") au.Start(sb, s, true); else break; }
            }
            int i = 0; int hz = (int)Math.Max(10, (last - (cases.Count > 0 ? cases[0].Point.At : last)).TotalMinutes + 1);
            foreach (var cc in cases)
            {
                if (cancel != null && cancel()) break; i++; if (progress != null) progress("🧭 measuring decision " + i + " / " + cases.Count + " (" + cc.Point.At.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture) + ")");
                cc.Snap.Kind = "STANDARD"; cc.Snap.Leverage = 500; cc.Snap.Spread = -1;
                ConsultSim.Measure(cc, bars, spec, Math.Min(ConsultSim.HorizonMin, (int)Math.Max(10, (last - cc.Point.At).TotalMinutes)), ticks); cc.Point.Src = ConsultSrc.TICKS;
                if (cc.Point.Complete) pts.Add(cc.Point);
            }
            return pts;
        }
    }

    // ✋ ACT ON THIS ACCOUNT on an MT5 account: the typed CONSULT each session (in memory: again after every SpasiX restart and after DISARM)
    public static class ConsultGate
    {
        static readonly HashSet<string> ok = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public const string Word = "CONSULT";
        public static bool Confirm(string key, string typed) { if (string.IsNullOrEmpty(key) || !StepInGate.Typed(typed, Word)) return false; lock (ok) ok.Add(key); return true; }
        public static bool Ok(string key) { lock (ok) return !string.IsNullOrEmpty(key) && ok.Contains(key); }
        public static void Revoke(string key) { lock (ok) ok.Remove(key ?? ""); }
        public static void RevokeAll() { lock (ok) ok.Clear(); }
        // may the engine ACT on the advice here? ADVISE never; SIM only on a simulated broker; ACT on a simulated broker, or an MT5 account after the typed CONSULT
        public static bool MayAct(string mode, bool isSim, string accKey)
        {
            if (mode == "SIM") return isSim;
            if (mode == "ACT") return isSim || Ok(accKey);
            return false;
        }
        public const string NotConfirmed = "🧭 CONSULT MODE is ACT ON THIS ACCOUNT but it was not confirmed in this session — it only ADVISES (type CONSULT in ⚙ SETTINGS ▸ 🧭 CONSULT, again after every SpasiX restart and after DISARM).";
        public static string Dialog()
        {
            return "🧭 CONSULT — ACT ON THIS ACCOUNT (REAL MONEY / MT5).\n\nAt each hedge decision CONSULT compares the basket with the similar past decisions (your own baskets + the 6-year library).\n"
                + "When — and only when — it has a CLEAR answer (≥ 30 similar and the best option beats the engine's hedge with a 95 % interval above $0), the automation does that instead of the hedge:\n"
                + "⏳ wait for the next swing, 🔐 lock at net 0, ➕ add with the trend (2 × the hedge lots) or ✖ close the basket now.\n"
                + "Everything still goes through ARM, PROP's limits, STOP ON MANUAL and the 🎯 EXIT GUARD. Without a clear answer your rules decide.\n\n"
                + "This lasts until DISARM or until SpasiX closes.\nType CONSULT to switch it on.";
        }
    }
}
