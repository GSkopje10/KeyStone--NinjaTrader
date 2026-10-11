// THE FIRST 5 MINUTES (MNQ, 09:30 New York): the 09:30–09:35 candle closed bearish / bullish — how often did the price keep
// going that way, and how far, versus turning the other way? Then: is any trade on it worth taking (follow / fade, stop,
// target, time out), chosen on 2020–23 and judged on 2024–26, against a label-shuffle null; then the chosen trades through
// the studio's own prop engine (KeystonePropSim, firm presets) as ROTATION / COPY / single account, and as a live account.
// Prices are the real 1-minute bars only: a stop and a target inside the same minute count as the stop.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;

public static class FirstFive
{
    // ---------- the day's first 5-minute candle and what came after ----------
    public sealed class Row
    {
        public RDay D; public double O5, H5, L5, C5, Body, Range, BodyAtr, ClosePos; public int Dir;   // Dir: 1 bull, −1 bear, 0 flat (close = open)
        public double Gap, Overnight; public int PrevDay;
        public Dictionary<string, double> Move = new Dictionary<string, double>();   // horizon → close − C5, signed in the candle's direction (raw for flat)
        public Dictionary<string, double> Raw = new Dictionary<string, double>();    // horizon → close − C5 (unsigned)
        public Dictionary<int, int> Race = new Dictionary<int, int>();             // X pts → +1 continued first, −1 opposite first, 2 both in one minute, 0 neither by 16:00
        public Dictionary<int, int> RaceMin = new Dictionary<int, int>();          // X pts → minutes until the first touch
        public int BreakFirst;   // +1 the candle's extreme in its direction broke first, −1 the other one, 2 same minute, 0 neither
        public double Mfe12, Mae12, Mfe16, Mae16;   // from C5, in the candle's direction (points)
    }
    public static readonly string[] Hz = { "09:40", "09:45", "10:00", "10:30", "11:00", "12:00", "16:00" };
    static readonly int[] HzSlot = { R.S(9, 39), R.S(9, 44), R.S(9, 59), R.S(10, 29), R.S(10, 59), R.S(11, 59), R.S(15, 59) };
    public static readonly int[] RaceX = { 10, 20, 30, 50, 75, 100 };
    static int O { get { return R.S(9, 30); } }

    public static List<Row> Rows(List<RDay> days)
    {
        var rows = new List<Row>(); int o = O, end = R.S(15, 59);
        foreach (var d in days)
        {
            if (!d.Has(o) || !d.Has(o + 4) || d.Prev == null || d.RthAtr <= 0) continue;
            var r = new Row { D = d, O5 = d.O[o], H5 = d.Hi(o, o + 4), L5 = d.Lo(o, o + 4), C5 = d.C[o + 4] };
            r.Body = r.C5 - r.O5; r.Range = r.H5 - r.L5; r.BodyAtr = Math.Abs(r.Body) / d.RthAtr; r.Dir = Math.Sign(r.Body);
            r.ClosePos = r.Range > 0 ? (r.C5 - r.L5) / r.Range : 0.5;
            int sg = r.Dir == 0 ? 1 : r.Dir;
            for (int i = 0; i < Hz.Length; i++) { double c = d.CloseAt(HzSlot[i]); r.Raw[Hz[i]] = c - r.C5; r.Move[Hz[i]] = sg * (c - r.C5); }
            foreach (int x in RaceX)
            {
                int res = 0, mins = 0;
                for (int k = o + 5; k <= end; k++)
                {
                    if (!d.Has(k)) continue;
                    bool up = d.H[k] >= r.C5 + x, dn = d.L[k] <= r.C5 - x;
                    if (up || dn) { res = up && dn ? 2 : (up ? 1 : -1) * sg; mins = k - (o + 5) + 1; break; }
                }
                r.Race[x] = res; r.RaceMin[x] = mins;
            }
            for (int k = o + 5; k <= end; k++)
            {
                if (!d.Has(k)) continue;
                bool up = d.H[k] > r.H5, dn = d.L[k] < r.L5;
                if (up || dn) { r.BreakFirst = up && dn ? 2 : (up ? 1 : -1) * sg; break; }
            }
            double h12 = d.Hi(o + 5, R.S(11, 59)), l12 = d.Lo(o + 5, R.S(11, 59)), h16 = d.Hi(o + 5, end), l16 = d.Lo(o + 5, end);
            r.Mfe12 = sg > 0 ? h12 - r.C5 : r.C5 - l12; r.Mae12 = sg > 0 ? r.C5 - l12 : h12 - r.C5;
            r.Mfe16 = sg > 0 ? h16 - r.C5 : r.C5 - l16; r.Mae16 = sg > 0 ? r.C5 - l16 : h16 - r.C5;
            r.Gap = r.O5 - d.Prev.CloseAt(R.S(15, 59)); r.Overnight = d.CloseAt(o - 1) - d.OpenAt(0);
            r.PrevDay = Math.Sign(d.Prev.CloseAt(R.S(15, 59)) - d.Prev.OpenAt(o));
            rows.Add(r);
        }
        return rows;
    }

    // ---------- statistics ----------
    static double Phi(double z) { double t = 1 / (1 + 0.2316419 * Math.Abs(z)), y = 0.3989423 * Math.Exp(-z * z / 2) * t * (0.3193815 + t * (-0.3565638 + t * (1.781478 + t * (-1.821256 + t * 1.330274)))); return z > 0 ? 1 - y : y; }
    public static double P2(double z) { return 2 * (1 - Phi(Math.Abs(z))); }
    // two-sided binomial p-value vs p0 (normal approximation with continuity correction; exact enough for n ≥ 30)
    public static double BinomP(int k, int n, double p0 = 0.5) { if (n == 0) return 1; double m = n * p0, sd = Math.Sqrt(n * p0 * (1 - p0)); double z = (Math.Abs(k - m) - 0.5) / sd; return Math.Min(1, P2(Math.Max(0, z))); }
    public static Tuple<double, double> Wilson(int k, int n) { if (n == 0) return Tuple.Create(0.0, 0.0); double z = 1.96, p = (double)k / n, den = 1 + z * z / n, c = (p + z * z / (2 * n)) / den, h = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n)) / den; return Tuple.Create(c - h, c + h); }
    // two-proportion z-test
    public static double TwoPropP(int k1, int n1, int k2, int n2) { if (n1 == 0 || n2 == 0) return 1; double p = (double)(k1 + k2) / (n1 + n2), se = Math.Sqrt(p * (1 - p) * (1.0 / n1 + 1.0 / n2)); return se == 0 ? 1 : P2(((double)k1 / n1 - (double)k2 / n2) / se); }
    public sealed class Mean { public int N; public double Avg, Sd, T, P, Lo, Hi, Median; }
    public static Mean MeanOf(IEnumerable<double> v)
    {
        var l = v.ToList(); var m = new Mean { N = l.Count }; if (l.Count < 2) return m;
        m.Avg = l.Average(); m.Sd = Math.Sqrt(l.Sum(x => (x - m.Avg) * (x - m.Avg)) / (l.Count - 1)); double se = m.Sd / Math.Sqrt(l.Count);
        m.T = se > 0 ? m.Avg / se : 0; m.P = P2(m.T); m.Lo = m.Avg - 1.96 * se; m.Hi = m.Avg + 1.96 * se; m.Median = Quantile(l, 0.5); return m;
    }
    // Welch t-test of two means
    public static double WelchP(IEnumerable<double> a, IEnumerable<double> b) { var x = MeanOf(a); var y = MeanOf(b); if (x.N < 2 || y.N < 2) return 1; double se = Math.Sqrt(x.Sd * x.Sd / x.N + y.Sd * y.Sd / y.N); return se == 0 ? 1 : P2((x.Avg - y.Avg) / se); }
    public static double Quantile(IEnumerable<double> v, double q) { var l = v.OrderBy(x => x).ToList(); return l.Count == 0 ? 0 : l[(int)Math.Min(l.Count - 1, Math.Round(q * (l.Count - 1)))]; }

    // ---------- the trade grid: every day × entry × stop × target × exit × side, $ per micro after costs ----------
    public static readonly string[] Entries = { "MKT", "BREAK" };              // MKT = market at 09:35:00 • BREAK = stop order beyond the candle's extreme (until 10:30)
    public static readonly double[] Stops = { 10, 15, 20, 30, 40, 50, 75, 100, -1 };   // −1 = the candle's other extreme
    public static readonly double[] Targets = { 10, 15, 20, 30, 40, 50, 75, 100, 150, 0 };   // 0 = none (time exit)
    public static readonly string[] Exits = { "10:00", "10:30", "11:00", "12:00", "15:55" };
    static readonly int[] ExitSlot = { R.S(9, 59), R.S(10, 29), R.S(10, 59), R.S(11, 59), R.S(15, 54) };
    public static int Configs { get { return Entries.Length * Stops.Length * Targets.Length * Exits.Length; } }
    public static string ConfigName(int c)
    {
        int e = c % Exits.Length; c /= Exits.Length; int t = c % Targets.Length; c /= Targets.Length; int s = c % Stops.Length; c /= Stops.Length;
        return (Entries[c] == "MKT" ? "market 09:35" : "break of the candle") + " • stop " + (Stops[s] < 0 ? "other side of the candle" : Stops[s].ToString("0") + " pts") + " • target " + (Targets[t] <= 0 ? "none" : Targets[t].ToString("0") + " pts") + " • out " + Exits[e];
    }
    public static RTrade TradeOf(Row r, int c, int dir)
    {
        int e = c % Exits.Length; c /= Exits.Length; int t = c % Targets.Length; c /= Targets.Length; int s = c % Stops.Length; c /= Stops.Length; string ent = Entries[c];
        var d = r.D; int o = O; double tk = 0.25, level = dir > 0 ? r.H5 : r.L5;
        double stop = Stops[s];
        if (stop < 0) { double from = ent == "MKT" ? r.C5 : level; stop = dir > 0 ? from - r.L5 + tk : r.H5 - from + tk; if (stop < 2) stop = 2; }
        return ent == "MKT" ? R.Trade(d, dir, "MKT", 0, o + 5, o + 5, stop, Targets[t], ExitSlot[e], "F5") : R.Trade(d, dir, "STOP", level, o + 5, R.S(10, 29), stop, Targets[t], ExitSlot[e], "F5");
    }

    public sealed class Strat { public string Side, Filter; public int Config; public string Name; public Stats All, Is, Oos; public List<RTrade> Trades; public double NullP; }
    public static readonly string[] Filters = { "all days", "bull candle only", "bear candle only", "big candle (top 25%)", "small candle (bottom 25%)", "gap agrees", "gap disagrees", "yesterday agrees", "yesterday disagrees", "strong close (outer 25%)", "weak close (wick back)" };
    static bool Pass(string f, Row r, int dir, double bigQ, double smallQ)
    {
        switch (f)
        {
            case "bull candle only": return dir > 0;
            case "bear candle only": return dir < 0;
            case "big candle (top 25%)": return r.BodyAtr >= bigQ;
            case "small candle (bottom 25%)": return r.BodyAtr <= smallQ;
            case "gap agrees": return Math.Sign(r.Gap) == dir;
            case "gap disagrees": return Math.Sign(r.Gap) == -dir;
            case "yesterday agrees": return r.PrevDay == dir;
            case "yesterday disagrees": return r.PrevDay == -dir;
            case "strong close (outer 25%)": return dir > 0 ? r.ClosePos >= 0.75 : r.ClosePos <= 0.25;
            case "weak close (wick back)": return dir > 0 ? r.ClosePos < 0.5 : r.ClosePos > 0.5;
            default: return true;
        }
    }

    static string P(double x) { return x.ToString("0.0", CultureInfo.InvariantCulture) + "%"; }
    static string F(double x, string f = "0.0") { return x.ToString(f, CultureInfo.InvariantCulture); }
    static string S(double x, string f = "0.0") { return (x >= 0 ? "+" : "") + x.ToString(f, CultureInfo.InvariantCulture); }
    static string Pv(double p) { return p < 0.0001 ? "<0.0001" : p.ToString(p < 0.01 ? "0.0000" : "0.000", CultureInfo.InvariantCulture); }
    static string J(string s) { var b = new StringBuilder("\""); foreach (char ch in s ?? "") { if (ch == '"' || ch == '\\') b.Append('\\').Append(ch); else if (ch == '\n') b.Append("\\n"); else if (ch < 32) b.Append(' '); else b.Append(ch); } return b.Append('"').ToString(); }
    static string Jn(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 4).ToString(CultureInfo.InvariantCulture); }

    public static void Run(List<RDay> days, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var rows = Rows(days); var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var json = new StringBuilder("{"); Action<string, string> put = (k, v) => { if (json.Length > 1) json.Append(','); json.Append(J(k)).Append(':').Append(v); };
        DateTime first = rows.First().D.Day, last = rows.Last().D.Day;
        int n = rows.Count, bull = rows.Count(r => r.Dir > 0), bear = rows.Count(r => r.Dir < 0), flat = n - bull - bear;
        say("FIRST 5 MINUTES • MNQ • " + n + " days " + first.ToString("yyyy-MM-dd") + " → " + last.ToString("yyyy-MM-dd") + " • bull " + bull + " • bear " + bear + " • flat " + flat);
        put("meta", "{\"days\":" + n + ",\"from\":" + J(first.ToString("yyyy-MM-dd")) + ",\"to\":" + J(last.ToString("yyyy-MM-dd")) + ",\"bull\":" + bull + ",\"bear\":" + bear + ",\"flat\":" + flat
            + ",\"medianRange\":" + Jn(Quantile(rows.Select(r => r.Range), 0.5)) + ",\"medianBody\":" + Jn(Quantile(rows.Select(r => Math.Abs(r.Body)), 0.5)) + ",\"medianRthAtr\":" + Jn(Quantile(rows.Select(r => r.D.RthAtr), 0.5))
            + ",\"bullP\":" + Jn(BinomP(bull, bull + bear)) + ",\"generated\":" + J(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")) + "}");

        // ===== 1. continuation vs opposite at each horizon =====
        var hzRows = new List<string>();
        foreach (string h in Hz)
        {
            Func<int, string> grp = dir =>
            {
                var l = rows.Where(r => r.Dir == dir).ToList(); int cont = l.Count(r => r.Move[h] > 0), opp = l.Count(r => r.Move[h] < 0), same = l.Count - cont - opp;
                var ci = Wilson(cont, l.Count); var all = MeanOf(l.Select(r => r.Move[h]));
                var mc = MeanOf(l.Where(r => r.Move[h] > 0).Select(r => r.Move[h])); var mo = MeanOf(l.Where(r => r.Move[h] < 0).Select(r => -r.Move[h]));
                // the same direction on every other day (the base rate): bear → share of non-bear days that closed lower, etc.
                var other = rows.Where(r => r.Dir != dir).ToList(); int otherSame = other.Count(r => dir * r.Raw[h] > 0);
                return "{\"n\":" + l.Count + ",\"cont\":" + cont + ",\"opp\":" + opp + ",\"flat\":" + same + ",\"contPct\":" + Jn(100.0 * cont / l.Count) + ",\"ciLo\":" + Jn(100 * ci.Item1) + ",\"ciHi\":" + Jn(100 * ci.Item2)
                    + ",\"p50\":" + Jn(BinomP(cont, cont + opp)) + ",\"avg\":" + Jn(all.Avg) + ",\"med\":" + Jn(all.Median) + ",\"t\":" + Jn(all.T) + ",\"pAvg\":" + Jn(all.P) + ",\"avgLo\":" + Jn(all.Lo) + ",\"avgHi\":" + Jn(all.Hi)
                    + ",\"avgCont\":" + Jn(mc.Avg) + ",\"medCont\":" + Jn(mc.Median) + ",\"avgOpp\":" + Jn(mo.Avg) + ",\"medOpp\":" + Jn(mo.Median)
                    + ",\"basePct\":" + Jn(other.Count == 0 ? 0 : 100.0 * otherSame / other.Count) + ",\"pBase\":" + Jn(TwoPropP(cont, l.Count, otherSame, other.Count)) + "}";
            };
            hzRows.Add("{\"h\":" + J(h) + ",\"bear\":" + grp(-1) + ",\"bull\":" + grp(1) + "}");
            var lb = rows.Where(r => r.Dir < 0).ToList(); var lu = rows.Where(r => r.Dir > 0).ToList();
            say(string.Format("  {0}: BEAR kept going down {1} ({2}/{3}) avg {4} pts • BULL kept going up {5} ({6}/{7}) avg {8} pts", h, P(100.0 * lb.Count(r => r.Move[h] > 0) / lb.Count), lb.Count(r => r.Move[h] > 0), lb.Count, S(lb.Average(r => r.Move[h])), P(100.0 * lu.Count(r => r.Move[h] > 0) / lu.Count), lu.Count(r => r.Move[h] > 0), lu.Count, S(lu.Average(r => r.Move[h]))));
        }
        put("horizons", "[" + string.Join(",", hzRows) + "]");

        // ===== 2. the race: +X in its direction or −X against, which came first (from the 09:35 close) =====
        var raceRows = new List<string>();
        foreach (int x in RaceX)
        {
            Func<int, string> grp = dir =>
            {
                var l = rows.Where(r => r.Dir == dir).ToList(); int c = l.Count(r => r.Race[x] == 1), op = l.Count(r => r.Race[x] == -1), both = l.Count(r => r.Race[x] == 2), none = l.Count(r => r.Race[x] == 0);
                var ci = Wilson(c, c + op); var mins = l.Where(r => r.Race[x] == 1 || r.Race[x] == -1).Select(r => (double)r.RaceMin[x]).ToList();
                return "{\"n\":" + l.Count + ",\"cont\":" + c + ",\"opp\":" + op + ",\"both\":" + both + ",\"none\":" + none + ",\"contPct\":" + Jn(c + op == 0 ? 0 : 100.0 * c / (c + op)) + ",\"ciLo\":" + Jn(100 * ci.Item1) + ",\"ciHi\":" + Jn(100 * ci.Item2) + ",\"p\":" + Jn(BinomP(c, c + op)) + ",\"medMin\":" + Jn(Quantile(mins, 0.5)) + "}";
            };
            raceRows.Add("{\"x\":" + x + ",\"bear\":" + grp(-1) + ",\"bull\":" + grp(1) + "}");
            var lb = rows.Where(r => r.Dir < 0).ToList(); var lu = rows.Where(r => r.Dir > 0).ToList();
            say(string.Format("  race ±{0}: BEAR down first {1} of decided • BULL up first {2}", x, P(100.0 * lb.Count(r => r.Race[x] == 1) / Math.Max(1, lb.Count(r => r.Race[x] == 1 || r.Race[x] == -1))), P(100.0 * lu.Count(r => r.Race[x] == 1) / Math.Max(1, lu.Count(r => r.Race[x] == 1 || r.Race[x] == -1)))));
        }
        put("race", "[" + string.Join(",", raceRows) + "]");

        // ===== 3. break of the candle's own extreme first (biased: the close sits nearer its own end) =====
        Func<int, string> brk = dir => { var l = rows.Where(r => r.Dir == dir).ToList(); int c = l.Count(r => r.BreakFirst == 1), op = l.Count(r => r.BreakFirst == -1), both = l.Count(r => r.BreakFirst == 2), none = l.Count(r => r.BreakFirst == 0); return "{\"n\":" + l.Count + ",\"cont\":" + c + ",\"opp\":" + op + ",\"both\":" + both + ",\"none\":" + none + ",\"contPct\":" + Jn(100.0 * c / Math.Max(1, c + op)) + ",\"closePos\":" + Jn(100 * l.Average(r => dir > 0 ? r.ClosePos : 1 - r.ClosePos)) + "}"; };
        put("breakFirst", "{\"bear\":" + brk(-1) + ",\"bull\":" + brk(1) + "}");

        // ===== 4. how far: excursions from the 09:35 close =====
        Func<int, string> exc = dir =>
        {
            var l = rows.Where(r => r.Dir == dir).ToList();
            Func<Func<Row, double>, string> q = f => "{\"avg\":" + Jn(l.Average(f)) + ",\"q25\":" + Jn(Quantile(l.Select(f), 0.25)) + ",\"med\":" + Jn(Quantile(l.Select(f), 0.5)) + ",\"q75\":" + Jn(Quantile(l.Select(f), 0.75)) + ",\"q90\":" + Jn(Quantile(l.Select(f), 0.9)) + "}";
            return "{\"mfe12\":" + q(r => r.Mfe12) + ",\"mae12\":" + q(r => r.Mae12) + ",\"mfe16\":" + q(r => r.Mfe16) + ",\"mae16\":" + q(r => r.Mae16) + ",\"pMfe12\":" + Jn(MeanOf(l.Select(r => r.Mfe12 - r.Mae12)).P) + ",\"pMfe16\":" + Jn(MeanOf(l.Select(r => r.Mfe16 - r.Mae16)).P) + "}";
        };
        put("excursion", "{\"bear\":" + exc(-1) + ",\"bull\":" + exc(1) + "}");

        // ===== 5. by year, by candle size, by context (11:00 and 16:00 horizons) =====
        Func<IEnumerable<Row>, string, string> cell = (src, h) => { var l = src.ToList(); int c = l.Count(r => r.Move[h] > 0), op = l.Count(r => r.Move[h] < 0); var m = MeanOf(l.Select(r => r.Move[h])); return "{\"n\":" + l.Count + ",\"contPct\":" + Jn(l.Count == 0 ? 0 : 100.0 * c / l.Count) + ",\"p\":" + Jn(BinomP(c, c + op)) + ",\"avg\":" + Jn(m.Avg) + ",\"pAvg\":" + Jn(m.P) + "}"; };
        Func<string, Func<Row, bool>, string> ctx = (label, f) => "{\"label\":" + J(label) + ",\"bear\":{" + string.Join(",", new[] { "10:00", "11:00", "16:00" }.Select(h => J(h) + ":" + cell(rows.Where(r => r.Dir < 0 && f(r)), h))) + "},\"bull\":{" + string.Join(",", new[] { "10:00", "11:00", "16:00" }.Select(h => J(h) + ":" + cell(rows.Where(r => r.Dir > 0 && f(r)), h))) + "}}";
        var years = rows.Select(r => r.D.Day.Year).Distinct().OrderBy(y => y).ToList();
        put("byYear", "[" + string.Join(",", years.Select(y => ctx(y.ToString(), r => r.D.Day.Year == y))) + "]");
        double q25 = Quantile(rows.Select(r => r.BodyAtr), 0.25), q50 = Quantile(rows.Select(r => r.BodyAtr), 0.5), q75 = Quantile(rows.Select(r => r.BodyAtr), 0.75);
        Func<double, double> pts = fr => Quantile(rows.Select(r => Math.Abs(r.Body)), fr);
        put("bySize", "[" + string.Join(",", new[] {
            ctx("tiny body (bottom 25%, ≤ " + F(pts(0.25), "0") + " pts typical)", r => r.BodyAtr <= q25), ctx("small (25–50%)", r => r.BodyAtr > q25 && r.BodyAtr <= q50),
            ctx("medium (50–75%)", r => r.BodyAtr > q50 && r.BodyAtr <= q75), ctx("big body (top 25%, ≥ " + F(pts(0.75), "0") + " pts typical)", r => r.BodyAtr > q75),
            ctx("strong close (outer 25% of the candle)", r => r.Dir > 0 ? r.ClosePos >= 0.75 : r.ClosePos <= 0.25), ctx("weak close (wick back past the middle)", r => r.Dir > 0 ? r.ClosePos < 0.5 : r.ClosePos > 0.5) }) + "]");
        put("byContext", "[" + string.Join(",", new[] {
            ctx("every day", r => true), ctx("gap agrees with the candle", r => Math.Sign(r.Gap) == r.Dir), ctx("gap against the candle", r => Math.Sign(r.Gap) == -r.Dir),
            ctx("overnight agrees", r => Math.Sign(r.Overnight) == r.Dir), ctx("overnight against", r => Math.Sign(r.Overnight) == -r.Dir),
            ctx("yesterday agrees", r => r.PrevDay == r.Dir), ctx("yesterday against", r => r.PrevDay == -r.Dir),
            ctx("Monday", r => r.D.Day.DayOfWeek == DayOfWeek.Monday), ctx("Tuesday", r => r.D.Day.DayOfWeek == DayOfWeek.Tuesday), ctx("Wednesday", r => r.D.Day.DayOfWeek == DayOfWeek.Wednesday), ctx("Thursday", r => r.D.Day.DayOfWeek == DayOfWeek.Thursday), ctx("Friday", r => r.D.Day.DayOfWeek == DayOfWeek.Friday),
            ctx("payrolls Friday", r => R.IsNfp(r.D.Day)), ctx("quiet market (typical range bottom third)", r => r.D.RthAtr <= Quantile(rows.Select(x => x.D.RthAtr), 1.0 / 3)), ctx("busy market (typical range top third)", r => r.D.RthAtr >= Quantile(rows.Select(x => x.D.RthAtr), 2.0 / 3)) }) + "]");

        // ===== 6. the trade grid =====
        int C = Configs; var t0 = DateTime.Now;
        var res = new float[C, 2][]; var cache = new RTrade[C, 2][];   // [config, side(0 = buy, 1 = sell)][row]
        Parallel.For(0, C, c =>
        {
            for (int s = 0; s < 2; s++)
            {
                var a = new float[rows.Count]; var tr = new RTrade[rows.Count];
                for (int i = 0; i < rows.Count; i++) { var t = TradeOf(rows[i], c, s == 0 ? 1 : -1); tr[i] = t; a[i] = t == null ? float.NaN : (float)t.Usd; }
                res[c, s] = a; cache[c, s] = tr;
            }
        });
        say("  trade grid: " + C + " brackets × 2 sides × " + rows.Count + " days in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        var isMask = rows.Select(r => r.D.Day < R.OosStart).ToArray();
        var isRows = rows.Where(r => r.D.Day < R.OosStart).ToList();
        double bigQ = Quantile(isRows.Select(r => r.BodyAtr), 0.75), smallQ = Quantile(isRows.Select(r => r.BodyAtr), 0.25);
        // per filter / side: which rows trade and which direction (from the candle labels; the null shuffles the labels)
        Func<int[], List<Tuple<string, string, bool[], int[]>>> plans = dirs =>
        {
            var l = new List<Tuple<string, string, bool[], int[]>>();
            foreach (string side in new[] { "FOLLOW", "FADE" })
                foreach (string f in Filters)
                {
                    var on = new bool[rows.Count]; var dd = new int[rows.Count];
                    for (int i = 0; i < rows.Count; i++) { int d0 = dirs[i]; if (d0 == 0) continue; on[i] = Pass(f, rows[i], d0, bigQ, smallQ); dd[i] = side == "FOLLOW" ? d0 : -d0; }
                    l.Add(Tuple.Create(side, f, on, dd));
                }
            return l;
        };
        Func<float[,][], Tuple<string, string, bool[], int[]>, int, bool, double[]> tstat = (rs, pl, c, isOnly) =>
        {
            int k = 0; double sum = 0, ss = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!pl.Item3[i] || (isOnly && !isMask[i])) continue; float v = rs[c, pl.Item4[i] > 0 ? 0 : 1][i]; if (float.IsNaN(v)) continue;
                k++; sum += v; ss += v * (double)v;
            }
            if (k < 2) return new double[] { k, 0, 0 };
            double avg = sum / k, sd = Math.Sqrt(Math.Max(0, (ss - k * avg * avg) / (k - 1))); return new double[] { k, avg, sd > 0 ? avg / sd * Math.Sqrt(k) : 0 };
        };
        var realDirs = rows.Select(r => r.Dir).ToArray(); var realPlans = plans(realDirs);
        var grid = new List<Tuple<int, int, double[]>>();   // plan index, config, IS (n, avg, t)
        for (int p = 0; p < realPlans.Count; p++) for (int c = 0; c < C; c++) grid.Add(Tuple.Create(p, c, tstat(res, realPlans[p], c, true)));
        const int MinIs = 120;
        double bestIsT = grid.Where(x => x.Item3[0] >= MinIs).Max(x => x.Item3[2]);
        Func<int, int, Strat> mk = (p, c) =>
        {
            var pl = realPlans[p]; var tr = new List<RTrade>();
            for (int i = 0; i < rows.Count; i++) { if (!pl.Item3[i]) continue; var t = cache[c, pl.Item4[i] > 0 ? 0 : 1][i]; if (t != null) tr.Add(t); }
            return new Strat { Side = pl.Item1, Filter = pl.Item2, Config = c, Name = pl.Item1 + " the 5-min candle • " + pl.Item2 + " • " + ConfigName(c), Trades = tr, All = Stats.Of(tr), Is = Stats.Of(tr.Where(t => t.Day < R.OosStart)), Oos = Stats.Of(tr.Where(t => t.Day >= R.OosStart)) };
        };
        int tested = grid.Count(x => x.Item3[0] >= MinIs);
        var top = grid.Where(x => x.Item3[0] >= MinIs).OrderByDescending(x => x.Item3[2]).Take(25).Select(x => mk(x.Item1, x.Item2)).ToList();
        int posIs = grid.Count(x => x.Item3[0] >= MinIs && x.Item3[1] > 0), posBoth = 0;
        // OOS of every strategy that was positive on 2020–23
        var oosOfPos = new List<double>();
        foreach (var x in grid.Where(x => x.Item3[0] >= MinIs && x.Item3[1] > 0))
        {
            var pl = realPlans[x.Item1]; int k = 0; double sum = 0;
            for (int i = 0; i < rows.Count; i++) { if (!pl.Item3[i] || isMask[i]) continue; float v = res[x.Item2, pl.Item4[i] > 0 ? 0 : 1][i]; if (float.IsNaN(v)) continue; k++; sum += v; }
            if (k > 0) { oosOfPos.Add(sum / k); if (sum > 0) posBoth++; }
        }
        say("  strategies tested " + tested + " (≥ " + MinIs + " trades 2020–23) • positive 2020–23: " + posIs + " • of those positive 2024–26 too: " + posBoth + " • best 2020–23 t " + F(bestIsT, "0.00"));

        // ===== 7. label-shuffle null: the best 2020–23 t-stat when the candle's colour is shuffled between days =====
        int perms = 200; var nullMax = new double[perms]; var rng0 = new Random(20261011); var seeds = Enumerable.Range(0, perms).Select(i => rng0.Next()).ToArray();
        var isIdx = Enumerable.Range(0, rows.Count).Where(i => isMask[i] && realDirs[i] != 0).ToArray();
        t0 = DateTime.Now;
        Parallel.For(0, perms, pi =>
        {
            var rng = new Random(seeds[pi]); var dirs = (int[])realDirs.Clone(); var lab = isIdx.Select(i => realDirs[i]).ToArray();
            for (int i = lab.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int tmp = lab[i]; lab[i] = lab[j]; lab[j] = tmp; }
            for (int i = 0; i < isIdx.Length; i++) dirs[isIdx[i]] = lab[i];
            var pls = plans(dirs); double mx = double.MinValue;
            foreach (var pl in pls) for (int c = 0; c < C; c++) { var st = tstat(res, pl, c, true); if (st[0] >= MinIs && st[2] > mx) mx = st[2]; }
            nullMax[pi] = mx;
        });
        Array.Sort(nullMax);
        double pNull = (1.0 + nullMax.Count(v => v >= bestIsT)) / (perms + 1);
        say("  label-shuffle null (" + perms + "×, " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s): best t by luck median " + F(nullMax[perms / 2], "0.00") + " • 95th pct " + F(nullMax[(int)(perms * 0.95)], "0.00") + " • real best " + F(bestIsT, "0.00") + " → p = " + F(pNull, "0.000"));
        put("search", "{\"tested\":" + tested + ",\"minIs\":" + MinIs + ",\"posIs\":" + posIs + ",\"posBoth\":" + posBoth + ",\"bestIsT\":" + Jn(bestIsT) + ",\"perms\":" + perms + ",\"nullMed\":" + Jn(nullMax[perms / 2]) + ",\"null95\":" + Jn(nullMax[(int)(perms * 0.95)]) + ",\"nullMax\":" + Jn(nullMax[perms - 1]) + ",\"pNull\":" + Jn(pNull)
            + ",\"oosOfPosAvg\":" + Jn(oosOfPos.Count == 0 ? 0 : oosOfPos.Average()) + ",\"nullDist\":[" + string.Join(",", nullMax.Select(Jn)) + "]}");

        // walk-forward: every year, trade the strategy with the best t-stat on all earlier years (no peeking); the same
        // procedure on shuffled candle colours (every year) tells what a walk-forward earns by luck
        int y0 = years.Min(), Y = years.Max() - y0 + 1; var yearOf = rows.Select(r => r.D.Day.Year - y0).ToArray(); var wfYears = years.Where(y => y >= 2022).ToList();
        Func<int[], Tuple<double, List<double>, List<Tuple<int, int, int, double>>>> walk = dirs =>
        {
            var pls = plans(dirs); int PC = pls.Count * C; var K = new int[PC, Y]; var Sm = new double[PC, Y]; var Sq = new double[PC, Y];
            for (int p = 0; p < pls.Count; p++) for (int c = 0; c < C; c++)
            {
                var pl = pls[p]; int id = p * C + c;
                for (int i = 0; i < rows.Count; i++) { if (!pl.Item3[i]) continue; float v = res[c, pl.Item4[i] > 0 ? 0 : 1][i]; if (float.IsNaN(v)) continue; int yi = yearOf[i]; K[id, yi]++; Sm[id, yi] += v; Sq[id, yi] += v * (double)v; }
            }
            var vals = new List<double>(); var picks = new List<Tuple<int, int, int, double>>(); double tot = 0;
            foreach (int y in wfYears)
            {
                int yi = y - y0; double bt = double.MinValue; int best = -1;
                for (int id = 0; id < PC; id++)
                {
                    int k = 0; double sum = 0, ss = 0; for (int j = 0; j < yi; j++) { k += K[id, j]; sum += Sm[id, j]; ss += Sq[id, j]; }
                    if (k < MinIs) continue; double avg = sum / k, sd = Math.Sqrt(Math.Max(0, (ss - k * avg * avg) / (k - 1))), t = sd > 0 ? avg / sd * Math.Sqrt(k) : 0;
                    if (t > bt) { bt = t; best = id; }
                }
                if (best < 0) continue; int bp = best / C, bc = best % C; var pl = pls[bp];
                for (int i = 0; i < rows.Count; i++) { if (!pl.Item3[i] || yearOf[i] != yi) continue; float v = res[bc, pl.Item4[i] > 0 ? 0 : 1][i]; if (float.IsNaN(v)) continue; vals.Add(v); tot += v; }
                picks.Add(Tuple.Create(y, bp, bc, bt));
            }
            return Tuple.Create(tot, vals, picks);
        };
        var wfReal = walk(realDirs); var wf = new List<string>(); double wfTot = wfReal.Item1; var wfM = MeanOf(wfReal.Item2);
        foreach (var pk in wfReal.Item3)
        {
            var s = mk(pk.Item2, pk.Item3); var yr = s.Trades.Where(t => t.Day.Year == pk.Item1).ToList(); double v0 = yr.Sum(t => t.Usd);
            wf.Add("{\"year\":" + pk.Item1 + ",\"name\":" + J(s.Name) + ",\"t\":" + Jn(pk.Item4) + ",\"n\":" + yr.Count + ",\"usd\":" + Jn(v0) + ",\"avg\":" + Jn(yr.Count == 0 ? 0 : v0 / yr.Count) + "}");
            say("  walk-forward " + pk.Item1 + ": " + S(v0, "0") + " $/micro (" + yr.Count + " trades) • chosen on < " + pk.Item1 + " (t " + F(pk.Item4, "0.0") + "): " + s.Name);
        }
        int wfPerms = 100; var wfNull = new double[wfPerms]; var allIdx = Enumerable.Range(0, rows.Count).Where(i => realDirs[i] != 0).ToArray(); t0 = DateTime.Now;
        Parallel.For(0, wfPerms, pi =>
        {
            var rng = new Random(seeds[pi] ^ 0x5f5f); var dirs = (int[])realDirs.Clone(); var lab = allIdx.Select(i => realDirs[i]).ToArray();
            for (int i = lab.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int tmp = lab[i]; lab[i] = lab[j]; lab[j] = tmp; }
            for (int i = 0; i < allIdx.Length; i++) dirs[allIdx[i]] = lab[i];
            wfNull[pi] = walk(dirs).Item1;
        });
        Array.Sort(wfNull); double wfP = (1.0 + wfNull.Count(v => v >= wfTot)) / (wfPerms + 1);
        say("  walk-forward total " + S(wfTot, "0") + " $/micro over " + wfM.N + " trades (avg " + S(wfM.Avg) + ", t " + F(wfM.T, "0.00") + ") • shuffled colours (" + wfPerms + "×, " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s): median " + S(wfNull[wfPerms / 2], "0") + " • 95th pct " + S(wfNull[(int)(wfPerms * 0.95)], "0") + " → p = " + F(wfP, "0.000"));
        put("walkForward", "{\"total\":" + Jn(wfTot) + ",\"n\":" + wfM.N + ",\"avg\":" + Jn(wfM.Avg) + ",\"t\":" + Jn(wfM.T) + ",\"perms\":" + wfPerms + ",\"nullMed\":" + Jn(wfNull[wfPerms / 2]) + ",\"null95\":" + Jn(wfNull[(int)(wfPerms * 0.95)]) + ",\"p\":" + Jn(wfP) + ",\"nullDist\":[" + string.Join(",", wfNull.Select(Jn)) + "],\"years\":[" + string.Join(",", wf) + "]}");

        // the simple versions everybody asks about (fixed, not searched)
        var simple = new List<Strat>();
        Func<string, string, double, double, string, Strat> named = (side, filter, stop, tgt, exit) =>
        {
            int c = 0; int ei = Array.IndexOf(Exits, exit), ti = Array.IndexOf(Targets, tgt), si = Array.IndexOf(Stops, stop); c = ((0 * Stops.Length + si) * Targets.Length + ti) * Exits.Length + ei;
            int p = realPlans.FindIndex(x => x.Item1 == side && x.Item2 == filter); return mk(p, c);
        };
        foreach (string side in new[] { "FOLLOW", "FADE" })
            foreach (var b in new[] { Tuple.Create(20.0, 20.0, "11:00"), Tuple.Create(20.0, 40.0, "11:00"), Tuple.Create(30.0, 30.0, "12:00"), Tuple.Create(-1.0, 0.0, "15:55"), Tuple.Create(40.0, 80.0, "15:55"), Tuple.Create(50.0, 50.0, "12:00") })
                foreach (string f in new[] { "all days", "bear candle only", "bull candle only" })
                    simple.Add(named(side, f, b.Item1, b.Item2, b.Item3));
        Func<Strat, string> sj = s =>
        {
            Func<Stats, string> st = x => "{\"n\":" + x.N + ",\"win\":" + Jn(x.Win) + ",\"avg\":" + Jn(x.Avg) + ",\"total\":" + Jn(x.Total) + ",\"pf\":" + Jn(x.Pf) + ",\"t\":" + Jn(x.T) + ",\"p\":" + Jn(P2(x.T)) + ",\"maxdd\":" + Jn(x.MaxDd) + ",\"avgWin\":" + Jn(x.AvgWin) + ",\"avgLoss\":" + Jn(x.AvgLoss) + "}";
            return "{\"name\":" + J(s.Name) + ",\"side\":" + J(s.Side) + ",\"filter\":" + J(s.Filter) + ",\"bracket\":" + J(ConfigName(s.Config)) + ",\"all\":" + st(s.All) + ",\"is\":" + st(s.Is) + ",\"oos\":" + st(s.Oos)
                + ",\"longs\":" + st(Stats.Of(s.Trades.Where(t => t.Dir > 0))) + ",\"shorts\":" + st(Stats.Of(s.Trades.Where(t => t.Dir < 0)))
                + ",\"years\":{" + string.Join(",", s.All.Years.OrderBy(k => k.Key).Select(k => J(k.Key.ToString()) + ":" + Jn(k.Value))) + "}}";
        };
        put("top", "[" + string.Join(",", top.Select(sj)) + "]");
        {
            var b0 = top[0]; var pl0 = realPlans.First(x => x.Item1 == b0.Side && x.Item2 == b0.Filter); var bl = new List<Strat>();
            foreach (int side in new[] { 1, -1 })
            {
                var tr = new List<RTrade>(); for (int i = 0; i < rows.Count; i++) { if (!pl0.Item3[i]) continue; var t = cache[b0.Config, side > 0 ? 0 : 1][i]; if (t != null) tr.Add(t); }
                bl.Add(new Strat { Side = side > 0 ? "ALWAYS LONG" : "ALWAYS SHORT", Filter = b0.Filter, Config = b0.Config, Name = (side > 0 ? "ALWAYS BUY" : "ALWAYS SELL") + " • " + b0.Filter + " • " + ConfigName(b0.Config), Trades = tr, All = Stats.Of(tr), Is = Stats.Of(tr.Where(t => t.Day < R.OosStart)), Oos = Stats.Of(tr.Where(t => t.Day >= R.OosStart)) });
            }
            put("baselines", "[" + string.Join(",", bl.Select(sj)) + "]");
            say("  drift check for the best: always buy on its days " + S(bl[0].All.Avg) + " $/trade (t " + F(bl[0].All.T, "0.00") + ") • always sell " + S(bl[1].All.Avg) + " • the strategy " + S(b0.All.Avg) + " (longs " + S(Stats.Of(b0.Trades.Where(t => t.Dir > 0)).Avg) + ", shorts " + S(Stats.Of(b0.Trades.Where(t => t.Dir < 0)).Avg) + ")");
        }
        put("simple", "[" + string.Join(",", simple.Select(sj)) + "]");

        // ===== 8. prop: the studio's engine, firm presets, ROTATION / COPY / ONE account, sizes, 2020–23 and 2024–26 separately =====
        var candidates = new List<Tuple<string, Strat>>();
        candidates.Add(Tuple.Create("BEST 2020–23 (searched)", top[0]));
        var bestFollow = top.FirstOrDefault(s => s.Side == "FOLLOW") ?? mk(grid.Where(x => x.Item3[0] >= MinIs && realPlans[x.Item1].Item1 == "FOLLOW").OrderByDescending(x => x.Item3[2]).Select(x => x.Item1).First(), grid.Where(x => x.Item3[0] >= MinIs && realPlans[x.Item1].Item1 == "FOLLOW").OrderByDescending(x => x.Item3[2]).Select(x => x.Item2).First());
        var bestFade = top.FirstOrDefault(s => s.Side == "FADE") ?? mk(grid.Where(x => x.Item3[0] >= MinIs && realPlans[x.Item1].Item1 == "FADE").OrderByDescending(x => x.Item3[2]).Select(x => x.Item1).First(), grid.Where(x => x.Item3[0] >= MinIs && realPlans[x.Item1].Item1 == "FADE").OrderByDescending(x => x.Item3[2]).Select(x => x.Item2).First());
        if (bestFollow != top[0]) candidates.Add(Tuple.Create("BEST FOLLOW 2020–23", bestFollow));
        if (bestFade != top[0]) candidates.Add(Tuple.Create("BEST FADE 2020–23", bestFade));
        candidates.Add(Tuple.Create("SIMPLE FOLLOW 20 / 40", simple.First(s => s.Side == "FOLLOW" && s.Filter == "all days" && s.Name.Contains("stop 20 pts • target 40"))));
        candidates.Add(Tuple.Create("SIMPLE FADE 20 / 40", simple.First(s => s.Side == "FADE" && s.Filter == "all days" && s.Name.Contains("stop 20 pts • target 40"))));
        // coin flip on the same bracket as the SIMPLE ones: what the prop rules pay for pure luck
        {
            var rng = new Random(7); var tr = new List<RTrade>(); int c = Array.IndexOf(Exits, "11:00") + Exits.Length * (Array.IndexOf(Targets, 40.0) + Targets.Length * Array.IndexOf(Stops, 20.0));
            for (int i = 0; i < rows.Count; i++) { var t = cache[c, rng.Next(2)][i]; if (t != null) tr.Add(t); }
            candidates.Add(Tuple.Create("COIN FLIP 20 / 40 (no edge, for comparison)", new Strat { Side = "RANDOM", Filter = "all days", Config = c, Name = "COIN FLIP • market 09:35 • stop 20 / target 40 • out 11:00", Trades = tr, All = Stats.Of(tr), Is = Stats.Of(tr.Where(t => t.Day < R.OosStart)), Oos = Stats.Of(tr.Where(t => t.Day >= R.OosStart)) }));
        }
        var presets = KeystoneSimRules.Presets().Where(p => p.Name != "CUSTOM").ToList();
        int[] sizes = { 1, 2, 3, 5, 8, 10, 15, 20 }; string[] modes = { "ROTATION 5", "COPY 5", "ONE ACCOUNT" };
        var propRows = new List<string>(); var propLines = new List<string>();
        var periods = new[] { Tuple.Create("2020–23", new DateTime(2000, 1, 1), R.OosStart.AddDays(-1)), Tuple.Create("2024–26", R.OosStart, new DateTime(2100, 1, 1)) };
        var jobs = new List<Tuple<int, int, int, int, int>>();   // candidate, preset, size, mode, period
        for (int ci = 0; ci < candidates.Count; ci++) for (int pi = 0; pi < presets.Count; pi++) foreach (int q in sizes) for (int mi = 0; mi < modes.Length; mi++) for (int per = 0; per < periods.Length; per++) jobs.Add(Tuple.Create(ci, pi, q, mi, per));
        var propOut = new string[jobs.Count]; t0 = DateTime.Now;
        Parallel.For(0, jobs.Count, ji =>
        {
            var jb = jobs[ji]; var cand = candidates[jb.Item1].Item2; var rules = presets[jb.Item2]; int q = jb.Item3; string mode = modes[jb.Item4]; var per = periods[jb.Item5];
            if (q > rules.MaxMicros) { propOut[ji] = null; return; }
            var pr = PropRun(rows, cand.Trades, rules, q, mode, per.Item2, per.Item3);
            propOut[ji] = "{\"cand\":" + jb.Item1 + ",\"firm\":" + J(rules.Name) + ",\"q\":" + q + ",\"mode\":" + J(mode) + ",\"period\":" + J(per.Item1) + "," + pr + "}";
        });
        say("  prop runs: " + jobs.Count + " in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        // luck band: the best candidate's days and bracket with a coin for the side, 200 times → where does the real result rank?
        {
            var b0 = candidates[0].Item2; var pl0 = realPlans.First(x => x.Item1 == b0.Side && x.Item2 == b0.Filter); int seedsN = 200; int[] qs = { 1, 2, 3, 5, 8 }; string[] lm = { "ONE ACCOUNT", "ROTATION 5" };
            var lj = new List<Tuple<int, int, int, int>>(); for (int pi = 0; pi < presets.Count; pi++) foreach (int q in qs) for (int mi = 0; mi < lm.Length; mi++) for (int per = 0; per < periods.Length; per++) lj.Add(Tuple.Create(pi, q, mi, per));
            var outL = new string[lj.Count]; t0 = DateTime.Now;
            var coinTrades = new List<RTrade>[seedsN];
            for (int sd = 0; sd < seedsN; sd++) { var rng = new Random(1000 + sd); var tr = new List<RTrade>(); for (int i = 0; i < rows.Count; i++) { if (!pl0.Item3[i]) continue; var t = cache[b0.Config, rng.Next(2)][i]; if (t != null) tr.Add(t); } coinTrades[sd] = tr; }
            Parallel.For(0, lj.Count, k =>
            {
                var jb = lj[k]; var rules = presets[jb.Item1]; int q = jb.Item2; string mode = lm[jb.Item3]; var per = periods[jb.Item4]; double real;
                PropRun(rows, b0.Trades, rules, q, mode, per.Item2, per.Item3, out real);
                var nets = new double[seedsN]; for (int sd = 0; sd < seedsN; sd++) PropRun(rows, coinTrades[sd], rules, q, mode, per.Item2, per.Item3, out nets[sd]);
                Array.Sort(nets); double pct = 100.0 * nets.Count(v => v < real) / seedsN;
                outL[k] = "{\"firm\":" + J(rules.Name) + ",\"q\":" + q + ",\"mode\":" + J(mode) + ",\"period\":" + J(per.Item1) + ",\"real\":" + Jn(real) + ",\"coinMed\":" + Jn(nets[seedsN / 2]) + ",\"coin10\":" + Jn(nets[seedsN / 10]) + ",\"coin90\":" + Jn(nets[seedsN * 9 / 10]) + ",\"coinPos\":" + Jn(100.0 * nets.Count(v => v > 0) / seedsN) + ",\"pct\":" + Jn(pct) + "}";
            });
            put("luck", "{\"cand\":" + J(b0.Name) + ",\"seeds\":" + seedsN + ",\"rows\":[" + string.Join(",", outL) + "]}");
            say("  luck band: " + lj.Count + " prop cells × " + seedsN + " coin runs in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        }
        put("candidates", "[" + string.Join(",", candidates.Select(c => "{\"label\":" + J(c.Item1) + ",\"strat\":" + sj(c.Item2) + "}")) + "]");
        put("prop", "[" + string.Join(",", propOut.Where(x => x != null)) + "]");
        put("firms", "[" + string.Join(",", presets.Select(p => "{\"name\":" + J(p.Name) + ",\"describe\":" + J(p.Describe()) + ",\"cost\":" + Jn(p.EvalCost + p.Activation) + ",\"maxMicros\":" + p.MaxMicros + "}")) + "]");

        // ===== 9. live: per micro, drawdowns, streaks, sizing for an account =====
        Func<Strat, string> live = s =>
        {
            var l = s.Trades.OrderBy(t => t.Day).ToList(); double eq = 0, peak = 0, dd = 0; int streak = 0, worstStreak = 0; double worst = 0;
            foreach (var t in l) { eq += t.Usd; peak = Math.Max(peak, eq); dd = Math.Max(dd, peak - eq); if (t.Usd <= 0) { streak++; worstStreak = Math.Max(worstStreak, streak); } else streak = 0; worst = Math.Min(worst, t.Usd); }
            var m = MeanOf(l.Select(t => t.Usd)); double var = m.Sd * m.Sd, kelly = var > 0 ? m.Avg / var : 0;   // Kelly: $ of capital per micro = var / mean
            double perYear = l.Count / Math.Max(0.5, (last - first).TotalDays / 365.25);
            var o = s.Oos; var oo = MeanOf(l.Where(t => t.Day >= R.OosStart).Select(t => t.Usd));
            return "{\"label\":" + J(s.Name) + ",\"n\":" + l.Count + ",\"perYear\":" + Jn(perYear) + ",\"avg\":" + Jn(m.Avg) + ",\"sd\":" + Jn(m.Sd) + ",\"maxdd\":" + Jn(dd) + ",\"worst\":" + Jn(worst) + ",\"streak\":" + worstStreak
                + ",\"kellyCapitalPerMicro\":" + Jn(kelly > 0 ? 1 / kelly : 0) + ",\"oosAvg\":" + Jn(oo.Avg) + ",\"oosT\":" + Jn(oo.T) + ",\"oosMaxdd\":" + Jn(o.MaxDd)
                + ",\"curve\":[" + string.Join(",", l.Select(t => "[" + J(t.Day.ToString("yyyy-MM-dd")) + "," + Jn(t.Usd) + "]")) + "]}";
        };
        put("live", "[" + string.Join(",", candidates.Select(c => live(c.Item2))) + "]");

        json.Append('}');
        File.WriteAllText(Path.Combine(outDir, "first5.json"), json.ToString());
        File.WriteAllText(Path.Combine(outDir, "first5_log.txt"), log.ToString());
        using (var w = new StreamWriter(Path.Combine(outDir, "first5_days.csv")))
        {
            w.WriteLine("date,weekday,candle,open,high,low,close,body_pts,range_pts,close_pos," + string.Join(",", Hz.Select(h => "move_" + h.Replace(":", ""))) + "," + string.Join(",", RaceX.Select(x => "race" + x)) + ",break_first,mfe_to_12,mae_to_12,mfe_to_16,mae_to_16,gap,overnight,prev_day");
            foreach (var r in rows)
                w.WriteLine(string.Join(",", r.D.Day.ToString("yyyy-MM-dd"), r.D.Day.DayOfWeek, r.Dir > 0 ? "BULL" : r.Dir < 0 ? "BEAR" : "FLAT", r.O5, r.H5, r.L5, r.C5, F(r.Body, "0.00"), F(r.Range, "0.00"), F(r.ClosePos, "0.00"),
                    string.Join(",", Hz.Select(h => F(r.Move[h], "0.00"))), string.Join(",", RaceX.Select(x => r.Race[x] == 1 ? "CONT" : r.Race[x] == -1 ? "OPP" : r.Race[x] == 2 ? "BOTH" : "NONE")),
                    r.BreakFirst == 1 ? "OWN" : r.BreakFirst == -1 ? "OTHER" : r.BreakFirst == 2 ? "BOTH" : "NONE", F(r.Mfe12, "0.00"), F(r.Mae12, "0.00"), F(r.Mfe16, "0.00"), F(r.Mae16, "0.00"), F(r.Gap, "0.00"), F(r.Overnight, "0.00"), r.PrevDay));
        }
        // the studio REPORTS file: every day labelled, the 5-minute candle marked, the best simple trade drawn
        var rep = new KReport { Title = "The First 5 Minutes", Instrument = "MNQ", Subtitle = "MNQ • the 09:30–09:35 candle on " + n + " days (" + first.ToString("MMM yyyy", CultureInfo.InvariantCulture) + " – " + last.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + ")",
            Method = "Candle = 09:30:00–09:35:00 (five 1-minute bars). BEAR = closed below its open, BULL = above. Moves from its close (09:35) to the close of each later minute, signed in the candle's direction. Trades: $1.50 commission + 1 tick slippage per side; a stop and a target in the same minute count as the stop." };
        rep.Summary.Add(new[] { "Days", n.ToString(), "bull " + bull + " • bear " + bear + " • flat " + flat });
        foreach (string h in new[] { "10:00", "11:00", "16:00" })
        {
            var lb = rows.Where(r => r.Dir < 0).ToList(); var lu = rows.Where(r => r.Dir > 0).ToList();
            rep.Summary.Add(new[] { "Kept going by " + h, "BEAR " + P(100.0 * lb.Count(r => r.Move[h] > 0) / lb.Count) + " • BULL " + P(100.0 * lu.Count(r => r.Move[h] > 0) / lu.Count), "average " + S(lb.Average(r => r.Move[h])) + " / " + S(lu.Average(r => r.Move[h])) + " pts in the candle's direction" });
        }
        var showT = candidates[0].Item2.Trades.ToDictionary(t => t.Day, t => t);
        foreach (var r in rows)
        {
            var kd = new KReport.KDay { Date = r.D.Day, Label = (r.Dir > 0 ? "BULL" : r.Dir < 0 ? "BEAR" : "FLAT") + " 5m " + S(r.Body, "0") + " pts → 11:00 " + S(r.Move["11:00"], "0") + " • 16:00 " + S(r.Move["16:00"], "0"), Color = r.Move["11:00"] > 0 ? "green" : r.Move["11:00"] < 0 ? "red" : "gray",
                Note = "race ±20: " + (r.Race[20] == 1 ? "kept going first" : r.Race[20] == -1 ? "turned first" : r.Race[20] == 2 ? "both in one minute" : "neither") + " • most in its direction by 12:00 " + F(r.Mfe12, "0") + " pts • most against " + F(r.Mae12, "0") + " pts" };
            kd.Fields["candle"] = r.Dir > 0 ? "BULL" : r.Dir < 0 ? "BEAR" : "FLAT"; foreach (string h in Hz) kd.Fields["move " + h] = F(r.Move[h], "0.00");
            kd.Markers.Add(new KReport.KMarker { Time = "09:30", Price = r.Dir >= 0 ? r.H5 : r.L5, Text = "5M " + kd.Fields["candle"] + " " + S(r.Body, "0") + " pts", Kind = r.Dir > 0 ? "buy" : r.Dir < 0 ? "sell" : "info" });
            kd.Markers.Add(new KReport.KMarker { Time = "11:00", Price = r.D.CloseAt(R.S(10, 59)), Text = "11:00 " + S(r.Move["11:00"], "0") + " pts", Kind = "info" });
            RTrade t; if (showT.TryGetValue(r.D.Day, out t)) kd.Trades.Add(new KReport.KTrade { Dir = t.Dir, EntryTime = R.T(t.InSlot), Entry = t.Entry, Stop = t.Stop, Target = t.Target, ExitTime = R.T(t.OutSlot + 1), Exit = t.Exit, Pnl = t.Usd, Why = candidates[0].Item1 + ": " + t.Why.Trim() });
            rep.Days.Add(kd);
        }
        File.WriteAllText(Path.Combine(outDir, "first5.kreport.json"), rep.Json());
    }

    // one prop run through the studio's engine: ROTATION n (one trade a day, the accounts take turns, n evaluations kept alive),
    // COPY n (every alive account takes the same trade), ONE ACCOUNT (a new one after a pass / blow-up)
    public static string PropRun(List<Row> rows, List<RTrade> trades, KeystoneSimRules rules, int q, string mode, DateTime from, DateTime to) { double net; return PropRun(rows, trades, rules, q, mode, from, to, out net); }
    public static string PropRun(List<Row> rows, List<RTrade> trades, KeystoneSimRules rules, int q, string mode, DateTime from, DateTime to, out double netOut)
    {
        int nAcc = mode == "ONE ACCOUNT" ? 1 : 5;
        var set = new KeystoneSimSettings { Strategy = mode.StartsWith("ROTATION") ? "ROTATION" : "MANUAL", Accounts = nAcc, ReplacePassed = false, RotateBy = mode.StartsWith("ROTATION") ? "TRADE" : "DAY", MaxLossesPerDay = 0, Rules = rules.Copy(), MnqQty = q, FundMnqQty = q };
        var sim = new KeystonePropSim(set); var byDay = trades.ToDictionary(t => t.Day, t => t); double pv = 2;
        double cumLow = 0; var passDays = new List<int>(); var yearNet = new SortedDictionary<int, double>();
        foreach (var r in rows)
        {
            var day = r.D.Day; if (day < from || day > to) continue;
            sim.BeginDay(day); RTrade t; byDay.TryGetValue(day, out t);
            if (t != null)
            {
                var takers = mode.StartsWith("COPY") ? sim.Accounts.Where(sim.CanTrade).ToList() : new List<KeystoneSimAccount> { sim.Active };
                foreach (var a in takers)
                {
                    if (a == null || !sim.CanTrade(a)) continue;
                    double cost = 0;   // costs are inside t.Usd (commission + slippage)
                    DateTime ent = day.AddMinutes(18 * 60 + t.InSlot - 1440), ex = day.AddMinutes(18 * 60 + t.OutSlot + 1 - 1440);
                    // the worst moment of the trade against the floor (favourable first = the conservative order for a trailing floor)
                    sim.CheckEquity(a, t.MfePts * pv * q);
                    string st = sim.CheckEquity(a, -t.MaePts * pv * q - cost);
                    double pnl = t.Usd * q; string why = t.Why.Trim();
                    if (st == "BLOWN") { pnl = Math.Min(pnl, sim.Floor(a) - (a.Bal + a.DayPnl)); why = "BLOWN at the floor"; }
                    else if (st == "DAILY LOSS") { pnl = Math.Min(pnl, -sim.DailyLoss(a) - a.DayPnl); why = "firm daily loss"; }
                    sim.AddFill(a, pnl);
                    sim.EndTrade(a, new KeystoneSimTrade { Entry = ent, Exit = ex, Dir = t.Dir, Legs = q + " MNQ", Pnl = pnl, Reason = why, Source = "F5" });
                    if (st == "BLOWN" && a.Alive) sim.Blow(a, ex, "equity touched the drawdown floor");
                }
            }
            var d = sim.EndDay(); double net = d.CumCash - d.CumSpent; cumLow = Math.Min(cumLow, net);
            double yv; yearNet.TryGetValue(day.Year, out yv); yearNet[day.Year] = yv + d.Cash - d.Spent;
        }
        var passed = sim.Accounts.Where(a => a.PassedOn != DateTime.MinValue).ToList();
        double avgPassDays = passed.Count == 0 ? 0 : passed.Average(a => (double)a.EvalDays);
        double daysN = Math.Max(1, sim.Days.Count); netOut = sim.Net;
        return "\"days\":" + sim.Days.Count + ",\"bought\":" + sim.Bought + ",\"passed\":" + sim.PassedCount + ",\"passRate\":" + Jn(sim.PassRate) + ",\"blownEval\":" + sim.BlownEval + ",\"blownFunded\":" + sim.BlownFunded
            + ",\"payouts\":" + sim.PayoutCount + ",\"gross\":" + Jn(sim.Accounts.Sum(a => a.Gross)) + ",\"cash\":" + Jn(sim.Cash) + ",\"spent\":" + Jn(sim.Spent) + ",\"net\":" + Jn(sim.Net) + ",\"netPerYear\":" + Jn(sim.Net / daysN * 252)
            + ",\"perEval\":" + Jn(sim.Bought == 0 ? 0 : sim.Net / sim.Bought) + ",\"worstNet\":" + Jn(cumLow) + ",\"avgPassDays\":" + Jn(avgPassDays) + ",\"trades\":" + sim.Trades.Count
            + ",\"years\":{" + string.Join(",", yearNet.Select(k => J(k.Key.ToString()) + ":" + Jn(k.Value))) + "}";
    }
}
