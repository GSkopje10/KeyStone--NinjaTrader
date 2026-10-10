// FX HFT LAB: high-frequency flip / martingale / hedge strategies walked through every 1-minute bar of six years, futures
// prices standing in for the MetaTrader symbols (MGC → XAUUSD, MNQ → NAS100). Each day starts flat inside its session
// window and closes everything at the window's end. Path inside a bar = MetaTrader's "1 minute OHLC" tester model
// (bull bar O→L→H→C, bear bar O→H→L→C); the ADVERSE model (the extreme against the open exposure first) re-runs the
// detail rows to show how much of a result depends on the unknown order inside the minute.
// Daily target / loss limit overlays are exact: every new intraday equity high / low is recorded in time order, so the
// first of +target / −limit is known without re-running the day.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Fx
{
    public sealed class Inst { public string Name, Src, Unit, Lot; public double[] D; public double Cost; public List<RDay> Days; public List<float[]> Truth; }
    sealed class Ses { public string Name; public int K0, K1; }
    sealed class Cfg { public string Fam, Par; public double Res; public Func<double, St> Make; }

    // ---------------------------------------------------------------- strategies (units: base lot, price units)
    abstract class St
    {
        public double Real, NetLots, SumLE, Cost, MaxLot; public int Trades;
        public double Basket = double.NaN, BasketBase;
        public double Eq(double p) { return Real + NetLots * p - SumLE; }
        public abstract void Start(double p, int hint);
        public abstract double Next(double cur, bool up);   // next trigger strictly beyond cur in the direction of motion (NaN = none)
        public abstract void Fire(double p, bool up);       // the trigger last returned by Next, filled at p
        public virtual void BarClose(double o, double c) { }
        public virtual void CloseAll(double p) { Real += NetLots * p - SumLE; NetLots = 0; SumLE = 0; }
        protected void Open(int dir, double lots, double p) { NetLots += dir * lots; SumLE += dir * lots * p; Real -= Cost * lots; Trades++; if (lots > MaxLot) MaxLot = lots; }
        protected void Close(int dir, double lots, double e, double p) { Real += dir * lots * (p - e); NetLots -= dir * lots; SumLE -= dir * lots * e; }
        protected static bool Beyond(double lv, double cur, bool up) { return up ? lv > cur + 1e-9 : lv < cur - 1e-9; }
    }

    // FLIP: one position, TP / SL; a loss reopens the OTHER way (the user's rule); a win reopens the same way (SAME) or the
    // other way (FLIP). Martingale: after a loss the lot × m (step ≤ k, then back to 1); a win resets the lot.
    sealed class Flip : St
    {
        double tp, sl, m; int k; bool same; int dir, step; double e, lot;
        public Flip(double tp, double sl, bool same, double m, int k) { this.tp = tp; this.sl = sl; this.same = same; this.m = m; this.k = k; }
        public override void Start(double p, int hint) { dir = hint < 0 ? -1 : 1; step = 0; lot = 1; e = p; Open(dir, lot, p); }
        public override double Next(double cur, bool up) { double lv = up ? (dir > 0 ? e + tp : e + sl) : (dir > 0 ? e - sl : e - tp); return Beyond(lv, cur, up) ? lv : double.NaN; }
        public override void Fire(double p, bool up)
        {
            bool win = up == (dir > 0); Close(dir, lot, e, p);
            if (win) { step = 0; if (!same) dir = -dir; } else { dir = -dir; step++; if (step > k) step = 0; }
            lot = Math.Max(1, Math.Round(Math.Pow(m, step), MidpointRounding.AwayFromZero)); e = p; Open(dir, lot, p);
        }
        public override void CloseAll(double p) { base.CloseAll(p); lot = 0; }
    }

    // FOLLOW / FADE: flat until a 1-minute bar closes, then enter with (FOLLOW) or against (FADE) that bar; TP / SL; flat again.
    sealed class Sig : St
    {
        double tp, sl; bool follow; int dir; double e;
        public Sig(double tp, double sl, bool follow) { this.tp = tp; this.sl = sl; this.follow = follow; }
        public override void Start(double p, int hint) { dir = 0; }
        public override double Next(double cur, bool up) { if (dir == 0) return double.NaN; double lv = up ? (dir > 0 ? e + tp : e + sl) : (dir > 0 ? e - sl : e - tp); return Beyond(lv, cur, up) ? lv : double.NaN; }
        public override void Fire(double p, bool up) { Close(dir, 1, e, p); dir = 0; }
        public override void BarClose(double o, double c) { if (dir != 0 || c == o) return; dir = (c > o) == follow ? 1 : -1; e = c; Open(dir, 1, c); }
        public override void CloseAll(double p) { base.CloseAll(p); dir = 0; }
    }

    // ZONE RECOVERY (hedging): open 1 lot; if price runs z against it, open the other way with a bigger lot sized so the
    // whole basket makes +target at that side's exit (t beyond the zone); alternate inside the zone until one exit is hit.
    // After maxLv positions the next trigger closes the whole basket at a loss. A new cycle starts in the move's direction.
    sealed class Zone : St
    {
        double z, t, target; int maxLv; double P; int d0, nextDir, pending; double cycle0;
        readonly List<double[]> pos = new List<double[]>();   // dir, lots, entry
        public Zone(double z, double t, int maxLv) { this.z = z; this.t = t; this.maxLv = maxLv; target = t; }
        public override void Start(double p, int hint) { Cycle(p, hint < 0 ? -1 : 1); }
        void Cycle(double p, int d) { pos.Clear(); P = p; d0 = d; cycle0 = Real; Add(d, 1, p); nextDir = -d; }
        void Add(int d, double lots, double p) { Open(d, lots, p); pos.Add(new double[] { d, lots, p }); }
        double ExitA { get { return P + d0 * t; } }
        double ExitB { get { return P - d0 * (z + t); } }
        double Trig { get { return nextDir == -d0 ? P - d0 * z : P; } }
        public override double Next(double cur, bool up)
        {
            double best = double.NaN; pending = -1; double[] lv = { ExitA, ExitB, Trig };
            for (int i = 0; i < 3; i++) if (Beyond(lv[i], cur, up) && (double.IsNaN(best) || (up ? lv[i] < best : lv[i] > best))) { best = lv[i]; pending = i; }
            return best;
        }
        public override void Fire(double p, bool up)
        {
            if (pending < 2) { CloseAll(p); Cycle(p, up ? 1 : -1); return; }
            if (pos.Count >= maxLv) { CloseAll(p); Cycle(p, nextDir); return; }
            double X = nextDir == d0 ? ExitA : ExitB, net = Real - cycle0;
            foreach (var q in pos) net += q[0] * q[1] * (X - q[2]);
            double per = nextDir * (X - p) - Cost;
            if (per <= 0) { CloseAll(p); Cycle(p, nextDir); return; }
            Add(nextDir, Math.Max(1, Math.Ceiling((target - net) / per - 1e-9)), p); nextDir = -nextDir;
        }
        public override void CloseAll(double p) { base.CloseAll(p); pos.Clear(); }
    }

    // HEDGE GRID: a buy and a sell at every grid level (step g), each with TP = g, no stop. Losers float until price
    // comes back. Optional basket: everything closes when the cycle's equity reaches +basket, then a new grid starts.
    sealed class Grid : St
    {
        double g, bm; double P0; int lvl;
        readonly Dictionary<int, double[]> buys = new Dictionary<int, double[]>(), sells = new Dictionary<int, double[]>();   // level → {count, sum of entries}
        public Grid(double g, double bm) { this.g = g; this.bm = bm; }
        public override void Start(double p, int hint) { buys.Clear(); sells.Clear(); P0 = p; lvl = 0; BasketBase = Real; Basket = bm > 0 ? bm * g : double.NaN; Pair(0, p); }
        void Pair(int j, double p)
        {
            double[] b, s; if (!buys.TryGetValue(j, out b)) buys[j] = b = new double[2]; if (!sells.TryGetValue(j, out s)) sells[j] = s = new double[2];
            Open(1, 1, p); b[0]++; b[1] += p; Open(-1, 1, p); s[0]++; s[1] += p;
        }
        public override double Next(double cur, bool up) { double lv = P0 + (up ? lvl + 1 : lvl - 1) * g; return Beyond(lv, cur, up) ? lv : double.NaN; }
        public override void Fire(double p, bool up)
        {
            double[] x;
            if (up) { if (buys.TryGetValue(lvl, out x) && x[0] > 0) { Real += x[0] * p - x[1]; NetLots -= x[0]; SumLE -= x[1]; x[0] = 0; x[1] = 0; } lvl++; }
            else { if (sells.TryGetValue(lvl, out x) && x[0] > 0) { Real += x[1] - x[0] * p; NetLots += x[0]; SumLE += x[1]; x[0] = 0; x[1] = 0; } lvl--; }
            Pair(lvl, p);
        }
        public override void CloseAll(double p) { base.CloseAll(p); buys.Clear(); sells.Clear(); }
    }

    // ---------------------------------------------------------------- day walk + equity ladder
    sealed class Rec
    {
        public List<float> V = new List<float>(); public List<int> Tr = new List<int>(); double mx, mn; public double Final, Min; public int Trades;
        public void Reset() { V.Clear(); Tr.Clear(); mx = 0; mn = 0; }
        public void Mark(double v, int tr) { if (v > mx + 1e-9) { mx = v; V.Add((float)v); Tr.Add(tr); } else if (v < mn - 1e-9) { mn = v; V.Add((float)v); Tr.Add(tr); } }
        public void End(double v, int tr) { Mark(v, tr); Final = v; Min = mn; Trades = tr; }
    }

    static void Walk(St s, double a, double b, bool gap, Rec rec)
    {
        if (a == b) return; bool up = b > a; double cur = a;
        for (int guard = 0; guard < 100000; guard++)
        {
            double lv = s.Next(cur, up); bool hasLv = !double.IsNaN(lv) && (up ? lv <= b + 1e-9 : lv >= b - 1e-9);
            double stop = hasLv ? lv : b;
            if (!double.IsNaN(s.Basket) && Math.Abs(s.NetLots) > 1e-9)
            {
                double tgt = s.BasketBase + s.Basket, pStar = (tgt - s.Real + s.SumLE) / s.NetLots;
                bool inRange = up ? pStar > cur + 1e-9 && pStar <= stop + 1e-9 : pStar < cur - 1e-9 && pStar >= stop - 1e-9;
                if (s.Eq(cur) >= tgt - 1e-9) { pStar = cur; inRange = true; }
                if (inRange) { double fp = gap ? b : pStar; s.CloseAll(fp); rec.Mark(s.Eq(fp), s.Trades); s.Start(fp, up ? 1 : -1); rec.Mark(s.Eq(fp), s.Trades); cur = fp; if (gap) break; continue; }
            }
            if (!hasLv) break;
            double f = gap ? b : lv; s.Fire(f, up); rec.Mark(s.Eq(f), s.Trades); cur = f; if (gap) break;
        }
        rec.Mark(s.Eq(b), s.Trades);
    }

    // one config through every day: per day the ladder → overlays
    sealed class DayOut { public float Pnl, Worst; public int Trades; }
    static readonly double[] OvT = { 0, 5, 10, 20, 50, 100 }; static readonly double[] OvL = { 0.5, 1, 2 };
    static int NOv { get { return 1 + (OvT.Length - 1) * OvL.Length; } }
    static void Ov(int i, out double T, out double L) { if (i == 0) { T = 0; L = 0; return; } i--; T = OvT[1 + i / OvL.Length]; L = T * OvL[i % OvL.Length]; }

    // ---------------------------------------------------------------- the price path inside a minute
    // OHLC = MetaTrader's "1 minute OHLC": three straight lines O → first extreme → second extreme → C (bull bar low first).
    //   No wiggles at all, so any rule that follows small moves looks like a money machine (see SelfTest).
    // BRIDGE = the same four prices joined by random Brownian bridges (no edge inside the minute), volatility from the
    //   bar's own range (E[range] of Brownian motion = 1.6 σ), resolution a quarter of the rule's smallest distance.
    sealed class Rng
    {
        ulong x; bool has; double spare;
        public Rng(ulong seed) { x = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; if (x == 0) x = 1; }
        public double U() { x ^= x >> 12; x ^= x << 25; x ^= x >> 27; return ((x * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / 9007199254740992.0); }
        public double N() { if (has) { has = false; return spare; } double u, v, s; do { u = 2 * U() - 1; v = 2 * U() - 1; s = u * u + v * v; } while (s >= 1 || s == 0); double m = Math.Sqrt(-2 * Math.Log(s) / s); spare = v * m; has = true; return u * m; }
    }
    const int BridgeCap = 400;   // most sub-steps in one minute
    static void Bar(St s, Rec rec, double o, double h, double l, double c, int model, double res, Rng rng, double[] w, float[] truth, int k0)
    {
        if (model == 2) { double pv = o; for (int i = 1; i <= 60; i++) { double p = truth[k0 + i]; Walk(s, pv, p, false, rec); pv = p; } return; }
        bool lowFirst = c >= o; double x1 = lowFirst ? l : h, x2 = lowFirst ? h : l;
        if (model == 1 || h - l <= 1e-9) { Walk(s, o, x1, false, rec); Walk(s, x1, x2, false, rec); Walk(s, x2, c, false, rec); return; }
        double sigma = (h - l) / 1.6, pts = Math.Min(BridgeCap, Math.Ceiling(Math.Pow(4 * sigma / res, 2)));
        double l1 = Math.Abs(x1 - o), l2 = Math.Abs(x2 - x1), l3 = Math.Abs(c - x2), tot = l1 + l2 + l3 + 1e-12;
        double[] a = { o, x1, x2 }, b = { x1, x2, c }, len = { l1, l2, l3 };
        for (int leg = 0; leg < 3; leg++)
        {
            double tau = (len[leg] + 1e-6) / (tot + 3e-6); int n = Math.Max(1, (int)Math.Ceiling(tau * pts)); if (n > w.Length - 1) n = w.Length - 1;
            if (n == 1 || len[leg] < 1e-9 && tau * pts < 1) { Walk(s, a[leg], b[leg], false, rec); continue; }
            double sd = sigma * Math.Sqrt(tau / n); w[0] = 0; for (int k = 1; k <= n; k++) w[k] = w[k - 1] + sd * rng.N();
            double prev = a[leg];
            for (int k = 1; k <= n; k++)
            {
                double f = (double)k / n, p = k == n ? b[leg] : a[leg] + (b[leg] - a[leg]) * f + w[k] - f * w[n];
                if (p > h) p = h; if (p < l) p = l;
                Walk(s, prev, p, false, rec); prev = p;
            }
        }
    }

    // → [overlay][day]; maxLot out. model 0 = BRIDGE, 1 = OHLC
    static DayOut[][] RunCfg(Inst inst, Ses ses, Cfg cfg, double cost, int model, int nOv, out double maxLot)
    {
        var res = new DayOut[nOv][]; for (int o = 0; o < nOv; o++) res[o] = new DayOut[inst.Days.Count];
        var rec = new Rec(); maxLot = 0; var s = cfg.Make(cost); s.Cost = cost; var w = new double[BridgeCap + 2];
        for (int di = 0; di < inst.Days.Count; di++)
        {
            var d = inst.Days[di]; int first = -1, cnt = 0;
            for (int k = ses.K0; k <= ses.K1; k++) if (d.Has(k)) { if (first < 0) first = k; cnt++; }
            if (cnt < 30) continue;
            var rng = new Rng((ulong)(d.Day.Ticks / TimeSpan.TicksPerDay) * 31UL + (ulong)ses.K0);   // same micro-path for every rule on that day
            s.Real = 0; s.NetLots = 0; s.SumLE = 0; s.Trades = 0; s.Basket = double.NaN; rec.Reset();
            int hint = first > 0 && d.Has(first - 1) ? Math.Sign(d.C[first - 1] - d.O[first - 1]) : 1;
            s.Start(d.O[first], hint == 0 ? 1 : hint); rec.Mark(s.Eq(d.O[first]), s.Trades);
            double prevC = d.O[first]; int prevK = first - 1;
            for (int k = first; k <= ses.K1; k++)
            {
                if (!d.Has(k)) continue;
                double o = d.O[k], h = d.H[k], l = d.L[k], c = d.C[k];
                Walk(s, prevC, o, k != prevK + 1, rec);
                Bar(s, rec, o, h, l, c, model, cfg.Res, rng, w, model == 2 ? inst.Truth[di] : null, k * 61);
                s.BarClose(o, c); rec.Mark(s.Eq(c), s.Trades);
                prevC = c; prevK = k;
            }
            s.CloseAll(prevC); rec.End(s.Eq(prevC), s.Trades);
            if (s.MaxLot > maxLot) maxLot = s.MaxLot; s.MaxLot = 0;
            for (int oi = 0; oi < nOv; oi++)
            {
                double T, L; Ov(oi, out T, out L); var r = new DayOut();
                if (oi == 0) { r.Pnl = (float)rec.Final; r.Worst = (float)rec.Min; r.Trades = rec.Trades; }
                else
                {
                    double mn = 0; bool done = false;
                    for (int i = 0; i < rec.V.Count; i++)
                    {
                        double v = rec.V[i];
                        if (v >= T) { r.Pnl = (float)T; r.Worst = (float)mn; r.Trades = rec.Tr[i]; done = true; break; }
                        if (v <= -L) { r.Pnl = (float)-L; r.Worst = (float)-L; r.Trades = rec.Tr[i]; done = true; break; }
                        mn = Math.Min(mn, v);
                    }
                    if (!done) { r.Pnl = (float)rec.Final; r.Worst = (float)rec.Min; r.Trades = rec.Trades; }
                }
                res[oi][di] = r;
            }
        }
        return res;
    }

    // ---------------------------------------------------------------- stats
    public sealed class Row
    {
        public int Id; public string Inst, Fam, Par, Ses; public int Cost; public double T, L;
        public int N; public double Total, Avg, Win, Best, Worst, P5, MaxDd, Ruin, TrDay, Hit, Stop, MaxLot; public int Streak, YearsUp; public double[] Years = new double[7];
        public double AdvTotal = double.NaN; public double[] AdvYears;
        public float[] Pnl, WorstDay;
    }
    static Row Stats(DayOut[] days, Inst inst)
    {
        var r = new Row(); double cum = 0, peak = 0, minEq = 0, dd = 0; int streak = 0; var vals = new List<double>(); int yN0 = 2020; var yN = new int[7];
        for (int i = 0; i < days.Length; i++)
        {
            var d = days[i]; if (d == null) continue; r.N++;
            double low = cum + d.Worst; minEq = Math.Min(minEq, low); dd = Math.Max(dd, peak - low);
            cum += d.Pnl; peak = Math.Max(peak, cum); vals.Add(d.Pnl);
            if (d.Pnl > 0) { r.Win++; streak = 0; } else { streak++; r.Streak = Math.Max(r.Streak, streak); }
            r.TrDay += d.Trades; int y = inst.Days[i].Day.Year - yN0; if (y >= 0 && y < 7) { r.Years[y] += d.Pnl; yN[y]++; }
        }
        if (r.N == 0) return r;
        vals.Sort(); r.Total = cum; r.Avg = cum / r.N; r.Win = 100 * r.Win / r.N; r.Best = vals[vals.Count - 1]; r.Worst = vals[0]; r.P5 = vals[(int)(0.05 * (vals.Count - 1))];
        r.MaxDd = dd; r.Ruin = -minEq; r.TrDay /= r.N; for (int y = 0; y < 7; y++) if (yN[y] >= 50 && r.Years[y] > 0) r.YearsUp++;
        return r;
    }

    // ---------------------------------------------------------------- self-test: a computer-made random price with NO edge
    // 250 days of a random walk sampled every second; the 1-minute bars are built from it. Every rule runs three ways:
    // on the true second-by-second path, on MetaTrader's 1-minute OHLC path and on the bridge path. With no edge the true
    // result is about 0 (zero costs here), so whichever model stays near the truth is the one to trust.
    public sealed class Self { public string Fam, Par; public double True, Ohlc, Bridge, TrDay; }
    static List<Self> SelfTest(List<Cfg> cfgs)
    {
        var inst = new Inst { Name = "RANDOM", Days = new List<RDay>(), Truth = new List<float[]>(), Cost = 0 };
        var rng = new Rng(7); double p = 2000, sd = 0.12;   // 1-second σ $0.12 → a typical minute range ≈ $1.5 (gold-like)
        for (int di = 0; di < 250; di++)
        {
            var d = new RDay { Day = new DateTime(2023, 1, 2).AddDays(di), Sym = "RANDOM" }; var tr = new float[R.Slots * 61];
            for (int k = 0; k < R.Slots; k++)
            {
                double o = p, h = p, l = p; tr[k * 61] = (float)p;
                for (int i = 1; i <= 60; i++) { p += sd * rng.N(); tr[k * 61 + i] = (float)p; if (p > h) h = p; if (p < l) l = p; }
                d.O[k] = (float)o; d.H[k] = (float)Math.Max(h, tr[k * 61]); d.L[k] = (float)Math.Min(l, tr[k * 61]); d.C[k] = (float)p; d.Bars++;
                for (int i = 0; i <= 60; i++) { if (tr[k * 61 + i] > d.H[k]) d.H[k] = tr[k * 61 + i]; if (tr[k * 61 + i] < d.L[k]) d.L[k] = tr[k * 61 + i]; }
                p = tr[k * 61 + 60];
            }
            inst.Days.Add(d); inst.Truth.Add(tr);
        }
        var ses = new Ses { Name = "24H", K0 = R.S(18, 0), K1 = R.S(16, 54) }; var outp = new Self[cfgs.Count];
        Parallel.For(0, cfgs.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            double ml; var x = new Self { Fam = cfgs[i].Fam, Par = cfgs[i].Par };
            var a = Stats(RunCfg(inst, ses, cfgs[i], 0, 2, 1, out ml)[0], inst); x.True = a.Total; x.TrDay = a.TrDay;
            x.Ohlc = Stats(RunCfg(inst, ses, cfgs[i], 0, 1, 1, out ml)[0], inst).Total; x.Bridge = Stats(RunCfg(inst, ses, cfgs[i], 0, 0, 1, out ml)[0], inst).Total;
            outp[i] = x;
        });
        foreach (var x in outp) Console.WriteLine(string.Format("  SELFTEST true {0,10:0} ohlc {1,10:0} bridge {2,10:0} tr/d {3,7:0} | {4} | {5}", x.True, x.Ohlc, x.Bridge, x.TrDay, x.Fam, x.Par));
        return outp.ToList();
    }

    static List<Cfg> Configs(double[] D, double cost)
    {
        var cfgs = new List<Cfg>(); Func<double, string> f = x => x.ToString("0.##", CultureInfo.InvariantCulture);
        var ratios = new[] { Tuple.Create(1.0, 1.0), Tuple.Create(2.0, 1.0), Tuple.Create(1.0, 2.0), Tuple.Create(3.0, 1.0) };
        foreach (double d in D)
        {
            foreach (var rt in ratios) foreach (bool same in new[] { true, false })
            { double tp = d * rt.Item1, sl = d * rt.Item2; bool sm = same; cfgs.Add(new Cfg { Fam = "FLIP", Res = Math.Min(tp, sl), Par = "TP " + f(tp) + " • SL " + f(sl) + " • after a win " + (sm ? "SAME way" : "FLIP"), Make = c => new Flip(tp, sl, sm, 1, 0) }); }
            foreach (double m in new[] { 1.5, 2, 3 }) foreach (int k in new[] { 3, 5, 8 })
            { double dd = d, mm = m; int kk = k; cfgs.Add(new Cfg { Fam = "MARTINGALE FLIP", Res = dd, Par = "TP = SL " + f(dd) + " • lot ×" + f(mm) + " after a loss • max " + kk + " steps", Make = c => new Flip(dd, dd, true, mm, kk) }); }
            foreach (double tm in new[] { 1.0, 2.0 }) foreach (int mx in new[] { 4, 6, 8 })
            { double z = d, t = d * tm; int m2 = mx; if (t <= 1.5 * cost) continue; cfgs.Add(new Cfg { Fam = "ZONE RECOVERY (hedge)", Res = z, Par = "zone " + f(z) + " • exit " + f(t) + " • max " + m2 + " positions", Make = c => new Zone(z, t, m2) }); }
            foreach (double bm in new[] { 0.0, 2, 5 })
            { double g = d, b = bm; cfgs.Add(new Cfg { Fam = "HEDGE GRID", Res = g, Par = "step " + f(g) + " • buy + sell each level, TP " + f(g) + (b > 0 ? " • basket +" + f(b * g) : " • no basket"), Make = c => new Grid(g, b) }); }
            foreach (var rt in ratios.Take(3)) foreach (bool fol in new[] { true, false })
            { double tp = d * rt.Item1, sl = d * rt.Item2; bool fo = fol; cfgs.Add(new Cfg { Fam = fo ? "FOLLOW 1m bar" : "FADE 1m bar", Res = Math.Min(tp, sl), Par = "TP " + f(tp) + " • SL " + f(sl), Make = c => new Sig(tp, sl, fo) }); }
        }
        return cfgs;
    }

    // ---------------------------------------------------------------- main
    public static void Run(string data, string outDir)
    {
        var t0 = DateTime.Now;
        var self = SelfTest(Configs(new[] { 1.0, 2.0 }, 0.2));
        Console.WriteLine("self-test in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        var insts = new List<Inst> {
            new Inst { Name = "XAUUSD", Src = "MGC", Unit = "$ at 0.01 lot (1 oz: $1 per $1 move)", Lot = "0.01", D = new[] { 1.0, 2, 3, 5, 10 }, Cost = 0.20 },
            new Inst { Name = "NAS100", Src = "MNQ", Unit = "$ at 1.00 lot (contract size 1: $1 per point)", Lot = "1.00", D = new[] { 5.0, 10, 20, 40 }, Cost = 1.0 } };
        var sessions = new List<Ses> {
            new Ses { Name = "ASIA 18:00–03:00", K0 = R.S(18, 0), K1 = R.S(2, 59) }, new Ses { Name = "LONDON 03:00–08:00", K0 = R.S(3, 0), K1 = R.S(7, 59) },
            new Ses { Name = "NY AM 08:00–12:00", K0 = R.S(8, 0), K1 = R.S(11, 59) }, new Ses { Name = "NY 09:30–16:00", K0 = R.S(9, 30), K1 = R.S(15, 59) },
            new Ses { Name = "24H 18:00–16:55", K0 = R.S(18, 0), K1 = R.S(16, 54) } };
        var rows = new List<Row>(); var lk = new object();
        foreach (var inst in insts)
        {
            inst.Days = R.Load(data, inst.Src, true).Where(d => d.Day.Year >= 2020).ToList();
            Console.WriteLine(inst.Name + ": " + inst.Days.Count + " sessions " + inst.Days.First().Day.ToString("yyyy-MM-dd") + " → " + inst.Days.Last().Day.ToString("yyyy-MM-dd"));
            var cfgs = Configs(inst.D, inst.Cost);
            var jobs = new List<Tuple<Ses, Cfg, int>>(); foreach (var ses in sessions) foreach (var c in cfgs) foreach (int ci in new[] { 1, 0 }) jobs.Add(Tuple.Create(ses, c, ci));
            jobs = jobs.OrderByDescending(j => j.Item1.K1 - j.Item1.K0).ThenBy(j => j.Item2.Res).ToList();   // the long ones first
            Console.WriteLine(inst.Name + ": " + cfgs.Count + " strategies × " + sessions.Count + " sessions × 2 cost levels = " + jobs.Count + " runs");
            int done = 0;
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = 4 }, job =>
            {
                double ml, ml2; int nOv = job.Item3 == 1 ? NOv : 1; double cost = job.Item3 == 1 ? inst.Cost : 0;
                var res = RunCfg(inst, job.Item1, job.Item2, cost, 0, nOv, out ml);
                var ohlc = RunCfg(inst, job.Item1, job.Item2, cost, 1, nOv, out ml2);
                var local = new List<Row>();
                for (int oi = 0; oi < nOv; oi++)
                {
                    var r = Stats(res[oi], inst); double T, L; Ov(oi, out T, out L);
                    r.Inst = inst.Name; r.Fam = job.Item2.Fam; r.Par = job.Item2.Par; r.Ses = job.Item1.Name; r.Cost = job.Item3; r.T = T; r.L = L; r.MaxLot = ml;
                    if (oi > 0) { int h = 0, st = 0; foreach (var d in res[oi]) if (d != null) { if (d.Pnl >= T - 1e-6) h++; else if (d.Pnl <= -L + 1e-6) st++; } r.Hit = 100.0 * h / Math.Max(1, r.N); r.Stop = 100.0 * st / Math.Max(1, r.N); }
                    r.Pnl = res[oi].Select(x => x == null ? float.NaN : x.Pnl).ToArray(); r.WorstDay = res[oi].Select(x => x == null ? float.NaN : x.Worst).ToArray();
                    var o2 = Stats(ohlc[oi], inst); r.AdvTotal = o2.Total; r.AdvYears = o2.Years;
                    local.Add(r);
                }
                lock (lk)
                {
                    rows.AddRange(local);
                    done++; if (done % 100 == 0) Console.WriteLine("  " + inst.Name + " " + done + "/" + jobs.Count + " (" + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s)");
                }
            });
        }
        rows = rows.OrderBy(r => r.Inst).ThenBy(r => r.Fam).ThenBy(r => r.Par).ThenBy(r => r.Ses).ThenBy(r => r.Cost).ThenBy(r => r.T).ThenBy(r => r.L).ToList();
        for (int i = 0; i < rows.Count; i++) rows[i].Id = i;
        // detail rows (daily series): the best by several measures (realistic costs) + the user's own rule
        var real = rows.Where(r => r.Cost == 1 && r.N > 100).ToList(); var det = new HashSet<int>();
        foreach (var g in real.GroupBy(r => r.Inst))
        {
            foreach (var r in g.OrderByDescending(r => r.Total).Take(25)) det.Add(r.Id);
            foreach (var r in g.OrderByDescending(r => r.YearsUp).ThenByDescending(r => r.Total).Take(15)) det.Add(r.Id);
            foreach (var r in g.Where(r => r.Total > 0).OrderByDescending(r => r.Win).Take(10)) det.Add(r.Id);
            foreach (var r in g.Where(r => r.T > 0).OrderByDescending(r => r.Avg).Take(10)) det.Add(r.Id);
            foreach (var fg in g.GroupBy(r => r.Fam)) foreach (var r in fg.OrderByDescending(r => r.Total).Take(4)) det.Add(r.Id);
            foreach (var fg in g.GroupBy(r => r.Fam)) foreach (var r in fg.OrderByDescending(r => r.Win).Take(2)) det.Add(r.Id);
            foreach (var r in g.Where(r => r.T == 0).OrderBy(r => r.Total).Take(5)) det.Add(r.Id);
        }
        foreach (var r in real.Where(r => r.Fam == "FLIP" && r.Par.Contains("SAME"))) { var p = r.Par.Split('•'); if (p[0].Trim().Substring(3) == p[1].Trim().Substring(3) && (r.T == 0 || r.Ses.StartsWith("24H"))) det.Add(r.Id); }
        Console.WriteLine("detail rows: " + det.Count);
        Write(insts, rows, det, self, outDir);
        Console.WriteLine("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }

    static string N(double v, int dp = 1) { if (double.IsNaN(v) || double.IsInfinity(v)) return "null"; return Math.Round(v, dp).ToString(CultureInfo.InvariantCulture); }
    static string Q(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }

    static void Write(List<Inst> insts, List<Row> rows, HashSet<int> det, List<Self> self, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var fams = rows.Select(r => r.Fam).Distinct().ToList(); var pars = rows.Select(r => r.Par).Distinct().ToList(); var sess = rows.Select(r => r.Ses).Distinct().ToList();
        var fi = fams.Select((x, i) => new { x, i }).ToDictionary(a => a.x, a => a.i); var pi = pars.Select((x, i) => new { x, i }).ToDictionary(a => a.x, a => a.i); var si = sess.Select((x, i) => new { x, i }).ToDictionary(a => a.x, a => a.i);
        var sb = new StringBuilder();
        sb.Append("{\"generated\":" + Q(DateTime.Now.ToString("yyyy-MM-dd HH:mm")) + ",\"years\":[2020,2021,2022,2023,2024,2025,2026]");
        sb.Append(",\"fams\":[" + string.Join(",", fams.Select(Q)) + "],\"pars\":[" + string.Join(",", pars.Select(Q)) + "],\"sess\":[" + string.Join(",", sess.Select(Q)) + "]");
        sb.Append(",\"insts\":[" + string.Join(",", insts.Select(i => "{\"name\":" + Q(i.Name) + ",\"src\":" + Q(i.Src) + ",\"unit\":" + Q(i.Unit) + ",\"lot\":" + Q(i.Lot) + ",\"cost\":" + N(i.Cost, 2) + ",\"days\":[" + string.Join(",", i.Days.Select(d => Q(d.Day.ToString("yyyy-MM-dd")))) + "]}")) + "]");
        sb.Append(",\"cols\":[\"id\",\"inst\",\"fam\",\"par\",\"ses\",\"cost\",\"T\",\"L\",\"n\",\"total\",\"avg\",\"win\",\"best\",\"worst\",\"p5\",\"maxdd\",\"ruin\",\"trday\",\"hit\",\"stop\",\"maxlot\",\"streak\",\"yearsup\",\"y\",\"adv\",\"advy\"]");
        sb.Append(",\"rows\":[");
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; if (i > 0) sb.Append(',');
            sb.Append("[" + r.Id + "," + insts.FindIndex(x => x.Name == r.Inst) + "," + fi[r.Fam] + "," + pi[r.Par] + "," + si[r.Ses] + "," + r.Cost + "," + N(r.T, 0) + "," + N(r.L, 1) + "," + r.N + "," + N(r.Total, 0) + "," + N(r.Avg, 2) + "," + N(r.Win, 1) + "," + N(r.Best, 0) + "," + N(r.Worst, 0) + "," + N(r.P5, 1) + "," + N(r.MaxDd, 0) + "," + N(r.Ruin, 0) + "," + N(r.TrDay, 1) + "," + N(r.Hit, 1) + "," + N(r.Stop, 1) + "," + N(r.MaxLot, 0) + "," + r.Streak + "," + r.YearsUp + ",[" + string.Join(",", r.Years.Select(y => N(y, 0))) + "]," + N(r.AdvTotal, 0) + "," + (r.AdvYears == null ? "null" : "[" + string.Join(",", r.AdvYears.Select(y => N(y, 0))) + "]") + "]");
        }
        sb.Append("],\"selftest\":[" + string.Join(",", self.Select(x => "[" + Q(x.Fam) + "," + Q(x.Par) + "," + N(x.True, 0) + "," + N(x.Ohlc, 0) + "," + N(x.Bridge, 0) + "," + N(x.TrDay, 0) + "]")) + "]");
        sb.Append(",\"detail\":{");
        bool first = true;
        foreach (int id in det.OrderBy(x => x))
        {
            var r = rows[id]; if (!first) sb.Append(','); first = false;
            sb.Append("\"" + id + "\":{\"p\":[" + string.Join(",", r.Pnl.Select(v => N(v, 2))) + "],\"w\":[" + string.Join(",", r.WorstDay.Select(v => N(v, 1))) + "]}");
        }
        sb.Append("}}");
        File.WriteAllText(Path.Combine(outDir, "fx_hft.json"), sb.ToString());
        // a plain summary for reading here
        var txt = new StringBuilder();
        foreach (var g in rows.GroupBy(r => r.Inst + " cost=" + r.Cost))
        {
            txt.AppendLine("== " + g.Key + " rows " + g.Count() + " positive " + g.Count(r => r.Total > 0) + " all-years-up(≥6) " + g.Count(r => r.YearsUp >= 6));
            foreach (var r in g.OrderByDescending(r => r.Total).Take(25))
                txt.AppendLine(string.Format("  {0,9:0} avg {1,7:0.00} win {2,5:0.0}% worst {3,7:0} ruin {4,7:0} tr/d {5,7:0.0} up {6} hit {7,5:0.0}% adv {8,8:0} | {9} | {10} | {11} | T {12} L {13} | {14}", r.Total, r.Avg, r.Win, r.Worst, r.Ruin, r.TrDay, r.YearsUp, r.Hit, r.AdvTotal, r.Fam, r.Par, r.Ses, r.T, r.L, string.Join(" ", r.Years.Select(y => y.ToString("0")))));
        }
        foreach (var g in rows.Where(r => r.T == 0).GroupBy(r => r.Inst + " cost=" + r.Cost + " " + r.Fam))
            txt.AppendLine(string.Format("FAMILY {0}: rows {1} positive {2} median total {3:0} median trades/day {4:0.0}", g.Key, g.Count(), g.Count(r => r.Total > 0), g.Select(r => r.Total).OrderBy(x => x).ElementAt(g.Count() / 2), g.Select(r => r.TrDay).OrderBy(x => x).ElementAt(g.Count() / 2)));
        File.WriteAllText(Path.Combine(outDir, "fx_hft_summary.txt"), txt.ToString());
        Console.Write(txt.ToString());
    }
}
