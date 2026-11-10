// SpasiX — ⚠ DANGER RADAR (09h, the user's request: "exception rules to prevent these scenarios before they happen" — the honest version).
// At each new hedge of a basket: a CALIBRATED probability that it goes below −10 × its target, from the basket's own state (the features of the
// 6-year research, danger-model.txt), the median time such baskets stayed stuck, and the 5 most similar past baskets. Display + alert only:
// the radar itself never sends anything (the optional actions are separate settings, OFF by default, and lost to COMPRESS ALWAYS in the tests).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class RadarBin { public double Lo, Hi, Pred, Obs, StuckH, Depth, Cal; public int N; }
    public sealed class RadarNeighbour { public string Date = "", Reason = ""; public int Hedges; public double Depth, Hours, Result = double.NaN, Dist; public bool Mine; public string Text { get { return Date + " · hedge " + Hedges + " · went " + Depth.ToString("0.0", CultureInfo.InvariantCulture) + "× the target deep · " + (Hours >= 24 ? (Hours / 24).ToString("0.0", CultureInfo.InvariantCulture) + " d" : Hours.ToString("0.0", CultureInfo.InvariantCulture) + " h") + " · " + (Reason == "" ? "still open" : Reason) + (double.IsNaN(Result) ? "" : " " + (Result >= 0 ? "+" : "−") + Math.Abs(Result).ToString("0.0", CultureInfo.InvariantCulture) + "× target") + (Mine ? " (yours)" : ""); } } }
    public sealed class RadarModel
    {
        public string[] Names = new string[0]; public double[] Mu, Sd, W; public double B; public double Auc; public int Samples, Positives;
        public readonly Dictionary<int, Tuple<double[], double[], double[], double>> WalkForward = new Dictionary<int, Tuple<double[], double[], double[], double>>();
        public readonly List<RadarBin> Bins = new List<RadarBin>(); public readonly List<Tuple<RadarNeighbour, double[]>> Library = new List<Tuple<RadarNeighbour, double[]>>();
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static double[] V(string s) { return s.Split(',').Select(x => double.Parse(x, IC)).ToArray(); }
        public static RadarModel Parse(string text)
        {
            var m = new RadarModel();
            foreach (var raw in text.Split('\n'))
            {
                var l = raw.TrimEnd('\r'); if (l == "" || l.StartsWith("#")) continue; var a = l.Split('|');
                if (a[0] == "FEAT") m.Names = a.Skip(1).ToArray();
                else if (a[0] == "FINAL") { m.Mu = V(a[1]); m.Sd = V(a[2]); m.W = V(a[3]); m.B = double.Parse(a[4], IC); }
                else if (a[0] == "WF") m.WalkForward[int.Parse(a[1], IC)] = Tuple.Create(V(a[2]), V(a[3]), V(a[4]), double.Parse(a[5], IC));
                else if (a[0] == "AUC") { m.Auc = double.Parse(a[1], IC); m.Samples = int.Parse(a[2], IC); m.Positives = int.Parse(a[3], IC); }
                else if (a[0] == "CAL") m.Bins.Add(new RadarBin { Lo = double.Parse(a[1], IC), Hi = double.Parse(a[2], IC), Pred = double.Parse(a[3], IC), Obs = double.Parse(a[4], IC), N = int.Parse(a[5], IC), StuckH = double.Parse(a[6], IC), Depth = double.Parse(a[7], IC), Cal = double.Parse(a[8], IC) });
                else if (a[0] == "NN") m.Library.Add(Tuple.Create(new RadarNeighbour { Date = a[1], Hedges = int.Parse(a[2], IC), Depth = double.Parse(a[3], IC), Hours = double.Parse(a[4], IC), Reason = a[5], Result = a[6] == "" ? double.NaN : double.Parse(a[6], IC) }, V(a[7])));
            }
            return m;
        }
        static RadarModel def;
        public static RadarModel Default
        {
            get
            {
                if (def != null) return def;
                try { using (var st = typeof(RadarModel).Assembly.GetManifestResourceStream("KeystoneMT5.danger-model.txt")) if (st != null) using (var r = new StreamReader(st)) return def = Parse(r.ReadToEnd()); } catch { }
                return def = new RadarModel();
            }
            set { def = value; }
        }
        public bool Ready { get { return Mu != null && W != null && Bins.Count > 0; } }
        public static double Sig(double z) { return 1 / (1 + Math.Exp(-z)); }
        public double Raw(double[] x, int wfYear = 0)
        {
            double[] mu = Mu, sd = Sd, w = W; double b = B; Tuple<double[], double[], double[], double> wf;
            if (wfYear > 0) { if (!WalkForward.TryGetValue(wfYear, out wf)) return double.NaN; mu = wf.Item1; sd = wf.Item2; w = wf.Item3; b = wf.Item4; }
            double z = b; for (int i = 0; i < w.Length; i++) z += (x[i] - mu[i]) / sd[i] * w[i]; return Sig(z);
        }
        public RadarBin BinOf(double p) { if (Bins.Count == 0 || double.IsNaN(p)) return null; foreach (var b in Bins) if (p < b.Hi) return b; return Bins[Bins.Count - 1]; }
        public double[] Z(double[] x) { var z = new double[x.Length]; for (int i = 0; i < x.Length; i++) z[i] = (x[i] - Mu[i]) / Sd[i]; return z; }
        // the 5 nearest past baskets (6-year runs + your own, extra) by the same standardized features — deterministic (distance, then date)
        public List<RadarNeighbour> Nearest(double[] x, IEnumerable<Tuple<RadarNeighbour, double[]>> extra, int k = 5)
        {
            var z = Z(x); var all = Library.Concat(extra ?? Enumerable.Empty<Tuple<RadarNeighbour, double[]>>());
            return all.Select(t => { double d = 0; for (int i = 0; i < z.Length && i < t.Item2.Length; i++) d += (z[i] - t.Item2[i]) * (z[i] - t.Item2[i]); var n = t.Item1; return new RadarNeighbour { Date = n.Date, Hedges = n.Hedges, Depth = n.Depth, Hours = n.Hours, Reason = n.Reason, Result = n.Result, Mine = n.Mine, Dist = Math.Sqrt(d) }; })
                .OrderBy(n => n.Dist).ThenBy(n => n.Date, StringComparer.Ordinal).Take(k).ToList();
        }
    }
    public sealed class RadarReading { public double Raw = double.NaN, P = double.NaN, StuckH, Depth; public int Hedges; public double[] X; public RadarBin Bin; public List<RadarNeighbour> Near = new List<RadarNeighbour>(); public string Text = ""; public DateTime At; }

    public static class DangerRadar
    {
        // the 6-year results of the optional actions (09h bench: his settings + COMPRESS ALWAYS, $8,000 a year 2020-06…2026-10, both replay speeds; walk-forward radar)
        // 6 years 2020–26 (XAUUSD, $8,000, DEFENDER + COMPRESS ALWAYS, the radar's walk-forward model), two replay speeds: every option LOST to COMPRESS alone
        public static string BenchCmp = "−$20,695 (fast) / −$12,849 (1-min), 1 wipe-out / 0, 8 / 1 hard stops — the reference";
        public static string BenchNoAdd = "−$25,098 / −$26,348, 2 wipe-outs / 2, 19 / 14 hard stops — WORSE than COMPRESS alone";
        public static string BenchLock = "−$10,750 / −$26,714, 1 / 2 wipe-outs, 8 / 17 hard stops — mixed (better fast, much worse 1-min)";
        public static string BenchBe = "−$20,767 / −$14,284, 1 / 0 wipe-outs, 7 / 1 hard stops — about the same or worse";
        public static string BenchTah = "−$13,862 / −$2,190, 0 / 0 wipe-outs, 3 / 0 hard stops — better, but the opposite rule did about as well — unproven";
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        // the research's features (edge/py/mk.py + radar.py) at minute m = the hedge's minute: bar i = the last M1 candle that OPENED before m
        public static double[] Features(IList<Mt5Bar> m1, DateTime t0, DateTime basketStart, double total, double target, double net, int hedges)
        {
            var m = new DateTime(t0.Ticks - t0.Ticks % TimeSpan.TicksPerMinute); int i = -1;
            for (int k = m1.Count - 1; k >= 0; k--) if (m1[k].Time < m) { i = k; break; }
            if (i < 200 || target <= 0) return null;
            Func<int, Tuple<double, double, double, double>> agg = mins =>
            {   // completed buckets of `mins` minutes (server time) up to bar i: Wilder ATR(14), EMA 9 / 21 of the closes
                long span = TimeSpan.TicksPerMinute * mins; double s = double.NaN, e9 = double.NaN, e21 = double.NaN, pc = double.NaN; double o = 0, h = 0, l = 0, c = 0; long key = long.MinValue; bool any = false;
                double ra9 = 2.0 / 10, ra21 = 2.0 / 22;
                Action close = () => { double tr = double.IsNaN(pc) ? h - l : Math.Max(h, pc) - Math.Min(l, pc); if (double.IsNaN(pc)) tr = Math.Max(h, c) - Math.Min(l, c); s = double.IsNaN(s) ? tr : s + (tr - s) / 14; e9 = double.IsNaN(e9) ? c : e9 + ra9 * (c - e9); e21 = double.IsNaN(e21) ? c : e21 + ra21 * (c - e21); pc = c; };
                for (int k = 0; k <= i; k++)
                {
                    var b = m1[k]; long kk = b.Time.Ticks / span;
                    if (kk != key) { if (any) close(); key = kk; o = b.O; h = b.H; l = b.L; c = b.C; any = true; } else { h = Math.Max(h, b.H); l = Math.Min(l, b.L); c = b.C; }
                }
                bool lastDone = i + 1 >= m1.Count ? true : m1[i + 1].Time.Ticks / span != key;   // bar i ends its bucket (the next bar is in another one)
                if (any && lastDone) close();
                return Tuple.Create(s, e9, e21, c);
            };
            var h1 = agg(60); var m5 = agg(5); double atrH = h1.Item1, atrM5 = m5.Item1;
            // the Wilder ATR of the research starts at the first bucket's true range = high − low (pc = c of itself): same as above
            var day = m1[i].Time.Date; double pv = 0, vv = 0; for (int k = i; k >= 0 && m1[k].Time.Date == day; k--) { double w = Math.Max(1, m1[k].V); pv += (m1[k].H + m1[k].L + m1[k].C) / 3 * w; vv += w; }
            double vwap = vv > 0 ? pv / vv : m1[i].C, hi = double.MinValue, lo = double.MaxValue; for (int k = i - 59; k <= i; k++) { hi = Math.Max(hi, m1[k].H); lo = Math.Min(lo, m1[k].L); }
            double ny = ((m1[i].Time.AddHours(-7) - m1[i].Time.AddHours(-7).Date).TotalHours), H = Math.Max(atrH, 1e-6);
            var bs = new DateTime(basketStart.Ticks - basketStart.Ticks % TimeSpan.TicksPerMinute);
            return new[] { Math.Log(1 + Math.Max(0, (m - bs).TotalMinutes)), total / target, atrH / Math.Max(atrM5, 1e-6), (hi - lo) / H, (m1[i].C - vwap) / H, Math.Sin(2 * Math.PI * ny / 24), Math.Cos(2 * Math.PI * ny / 24),
                Math.Sign(net) * Math.Sign(h1.Item2 - h1.Item3), Math.Abs(m1[i].C - m1[i - 60].C) / H, Math.Log(1 + hedges) };
        }
        public static RadarReading Read(RadarModel model, IList<Mt5Bar> m1, DateTime t0, DateTime basketStart, double total, double target, double net, int hedges, IEnumerable<Tuple<RadarNeighbour, double[]>> mine = null, int wfYear = 0)
        {
            var r = new RadarReading { Hedges = hedges, At = t0 }; if (model == null || !model.Ready) { r.Text = "⚠ DANGER RADAR: no model"; return r; }
            r.X = Features(m1, t0, basketStart, total, target, net, hedges); if (r.X == null) { r.Text = "⚠ DANGER RADAR: not enough candles yet (200+ before the hedge)"; return r; }
            r.Raw = model.Raw(r.X, wfYear); r.Bin = model.BinOf(r.Raw); if (r.Bin == null) { r.Text = "⚠ DANGER RADAR: no reading"; return r; }
            r.P = r.Bin.Cal; r.StuckH = r.Bin.StuckH; r.Depth = r.Bin.Depth; if (wfYear == 0) r.Near = model.Nearest(r.X, mine);
            r.Text = "⚠ DANGER " + (100 * r.P).ToString("0", IC) + " % (in 2021–26, baskets like this went below −$" + (10 * target).ToString("0", IC) + " " + (100 * r.P).ToString("0", IC) + " % of the time, median stuck " + (r.StuckH >= 1 ? r.StuckH.ToString("0.#", IC) + " h" : (r.StuckH * 60).ToString("0", IC) + " min") + ") · hedge " + hedges;
            return r;
        }
        // the calibration, said honestly (the ANALYSIS view)
        public static List<string> CalibrationText(RadarModel m)
        {
            var l = new List<string> { "⚠ DANGER RADAR — how honest it is: walk-forward (each year predicted by a model trained on the years before) AUC " + m.Auc.ToString("0.00", IC) + " on " + m.Samples + " hedges (" + m.Positives + " went below −10 × the target)",
                "   the raw score             → what really happened (the % shown)   baskets   median stuck   median depth" };
            foreach (var b in m.Bins) l.Add(string.Format(IC, "   {0,5:0.00} – {1,5:0.00} (said {2,4:0}%) → {3,4:0}% observed → shown {4,4:0}%   {5,7}   {6,8:0.0} h   {7,8:0.0}× target", b.Lo, b.Hi, 100 * b.Pred, 100 * b.Obs, 100 * b.Cal, b.N, b.StuckH, b.Depth));
            l.Add("   the raw model is badly calibrated (high scores over-promise): the % shown is the OBSERVED rate of its score band (monotone), not the raw score. A ranking aid, not a forecast.");
            return l;
        }
        // 🧪 TAH (RACE lane only): the H1 EMA 20 of completed hours, its change over the last 3 hours (+ = up)
        public static double H1Ema20Slope3(IList<Mt5Bar> m1, DateTime now)
        {
            var closes = new List<double>(); long span = TimeSpan.TicksPerHour, key = long.MinValue; double c = double.NaN;
            foreach (var b in m1) { if (b.Time.AddMinutes(1) > now) break; long kk = b.Time.Ticks / span; if (kk != key) { if (key != long.MinValue) closes.Add(c); key = kk; } c = b.C; }
            if (key != long.MinValue && now.Ticks / span != key) closes.Add(c);
            if (closes.Count < 25) return double.NaN; var e = TrendDetect.Ema(closes, 20); return e[e.Length - 1] - e[e.Length - 4];
        }
    }
}
