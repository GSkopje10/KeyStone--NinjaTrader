// Research harness — one-trade simulator on the 1-minute grid. Conservative: the stop is checked before the target on
// every bar, a target needs a trade THROUGH it (1 tick), stop / market / time exits pay 1 tick of slippage, commission per side.
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RTrade
{
    public DateTime Day; public string Sym; public int Dir; public int InSlot, OutSlot; public double Entry, Exit, Stop, Target;
    public double Pts;      // net points per contract after costs
    public double Usd;      // net $ per contract after costs
    public double MaePts, MfePts; public string Why = "";
}

public sealed class Spec
{
    public string Sym; public double PointValue, Tick, CommissionRt;
    public static readonly Spec MNQ = new Spec { Sym = "MNQ", PointValue = 2, Tick = 0.25, CommissionRt = 1.50 };
    public static readonly Spec MGC = new Spec { Sym = "MGC", PointValue = 10, Tick = 0.1, CommissionRt = 1.80 };
    public static Spec Of(string s) { return s == "MGC" ? MGC : MNQ; }
}

public static partial class R
{
    public static double ExtraTicks = 0;   // stress test: extra slippage per side
    // entry: "MKT" = open of inSlot; "STOP" = first bar in [inSlot, lastIn] trading through level; "LIMIT" = first bar trading through level (1 tick)
    // stopDist / tgtDist in points from the fill (tgtDist <= 0 = none); exit at the close of outSlot at the latest.
    public static RTrade Trade(RDay d, int dir, string entry, double level, int inSlot, int lastIn, double stopDist, double tgtDist, int outSlot, string why = "")
    {
        var sp = Spec.Of(d.Sym); double tk = sp.Tick;
        int k = -1; double fill = double.NaN;
        if (entry == "MKT")
        {
            for (int i = inSlot; i <= Math.Min(lastIn, outSlot); i++) if (d.Has(i)) { k = i; fill = d.O[i] + dir * tk; break; }
        }
        else
        {
            for (int i = inSlot; i <= Math.Min(lastIn, outSlot); i++)
            {
                if (!d.Has(i)) continue;
                if (entry == "STOP")
                {
                    if (dir > 0 && d.H[i] >= level + tk) { k = i; fill = Math.Max(level, d.O[i]) + tk; break; }
                    if (dir < 0 && d.L[i] <= level - tk) { k = i; fill = Math.Min(level, d.O[i]) - tk; break; }
                }
                else
                {
                    if (dir > 0 && d.L[i] <= level - tk) { k = i; fill = Math.Min(level, d.O[i]); break; }
                    if (dir < 0 && d.H[i] >= level + tk) { k = i; fill = Math.Max(level, d.O[i]); break; }
                }
            }
        }
        if (k < 0) return null;
        double stop = fill - dir * stopDist, tgt = tgtDist > 0 ? fill + dir * tgtDist : double.NaN;
        double exit = double.NaN; int ko = -1; string how = "TIME"; double mae = 0, mfe = 0;
        for (int i = k; i <= outSlot && i < Slots; i++)
        {
            if (!d.Has(i)) continue;
            double adverse = dir > 0 ? fill - d.L[i] : d.H[i] - fill, favour = dir > 0 ? d.H[i] - fill : fill - d.L[i];
            if (stopDist > 0 && ((dir > 0 && d.L[i] <= stop) || (dir < 0 && d.H[i] >= stop)))
            {
                double px = dir > 0 ? Math.Min(stop, d.O[i] < stop && i > k ? d.O[i] : stop) : Math.Max(stop, d.O[i] > stop && i > k ? d.O[i] : stop);   // gap through the stop fills at the open
                exit = px - dir * tk; ko = i; how = "STOP"; mae = Math.Max(mae, dir * (fill - exit)); break;
            }
            mae = Math.Max(mae, adverse);
            if (i > k && !double.IsNaN(tgt) && ((dir > 0 && d.H[i] >= tgt + tk) || (dir < 0 && d.L[i] <= tgt - tk))) { exit = tgt; ko = i; how = "TARGET"; mfe = Math.Max(mfe, dir * (tgt - fill)); break; }
            mfe = Math.Max(mfe, favour);
            ko = i; exit = d.C[i] - dir * tk;
        }
        if (ko < 0) return null;
        double pts = dir * (exit - fill) - sp.CommissionRt / sp.PointValue - 2 * ExtraTicks * tk;
        return new RTrade { Day = d.Day, Sym = d.Sym, Dir = dir, InSlot = k, OutSlot = ko, Entry = fill, Exit = exit, Stop = stop, Target = tgt, Pts = pts, Usd = pts * sp.PointValue, MaePts = mae, MfePts = mfe, Why = why + " " + how };
    }

    // session VWAP from slot a through slot b (typical price × volume)
    public static double Vwap(RDay d, int a, int b)
    {
        double pv = 0, v = 0;
        for (int i = a; i <= b; i++) if (d.Has(i)) { double w = Math.Max(1, d.V[i]); pv += (d.H[i] + d.L[i] + d.C[i]) / 3.0 * w; v += w; }
        return v > 0 ? pv / v : double.NaN;
    }
}

public sealed class Stats
{
    public int N; public double Total, Avg, Win, Pf, T, MaxDd, AvgWin, AvgLoss; public Dictionary<int, double> Years = new Dictionary<int, double>();
    public static Stats Of(IEnumerable<RTrade> ts)
    {
        var l = ts.ToList(); var s = new Stats { N = l.Count }; if (l.Count == 0) return s;
        double gw = 0, gl = 0, eq = 0, peak = 0, ss = 0; int w = 0;
        foreach (var t in l) { s.Total += t.Usd; if (t.Usd > 0) { gw += t.Usd; w++; } else gl -= t.Usd; eq += t.Usd; peak = Math.Max(peak, eq); s.MaxDd = Math.Max(s.MaxDd, peak - eq); double y; s.Years.TryGetValue(t.Day.Year, out y); s.Years[t.Day.Year] = y + t.Usd; }
        s.Avg = s.Total / l.Count; s.Win = 100.0 * w / l.Count; s.Pf = gl > 0 ? gw / gl : 99; s.AvgWin = w > 0 ? gw / w : 0; s.AvgLoss = l.Count - w > 0 ? gl / (l.Count - w) : 0;
        foreach (var t in l) ss += (t.Usd - s.Avg) * (t.Usd - s.Avg);
        double sd = Math.Sqrt(ss / Math.Max(1, l.Count - 1)); s.T = sd > 0 ? s.Avg / sd * Math.Sqrt(l.Count) : 0;
        return s;
    }
}
