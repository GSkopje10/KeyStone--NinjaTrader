// Research harness — strategy families. Every variant is one rule with fixed numbers, at most one trade a day,
// decided only from bars that are already closed (no look-ahead), flat by the exit time.
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Variant { public string Family, Name, Sym; public Func<RDay, RTrade> Run; }

public static partial class R
{
    static string D(int dir) { return dir > 0 ? "LONG" : dir < 0 ? "SHORT" : "BOTH"; }

    public static List<Variant> Variants(string sym)
    {
        var v = new List<Variant>();
        int s1555 = S(15, 55), s1644 = S(16, 44);
        var opens = sym == "MGC" ? new[] { S(8, 20), S(9, 30) } : new[] { S(9, 30) };

        // F1 OPENING RANGE BREAKOUT
        foreach (int o in opens)
        foreach (int n in new[] { 5, 15, 30, 60 })
        foreach (int dir in new[] { 1, -1, 0 })
        foreach (string stopMode in new[] { "OPP", "MID" })
        foreach (double rr in new[] { 1, 1.5, 2, 3, 0 })
        foreach (int win in new[] { 90, 240 })
        {
            int oo = o, nn = n, dd = dir, ww = win; string sm = stopMode; double r2 = rr;
            v.Add(new Variant { Family = "ORB", Sym = sym, Name = "ORB " + T(o) + " " + n + "m " + D(dir) + " stop " + sm + " " + (rr > 0 ? rr + "R" : "to 15:55") + " entries " + win + "m", Run = d =>
            {
                double hi = d.Hi(oo, oo + nn - 1), lo = d.Lo(oo, oo + nn - 1); if (double.IsNaN(hi) || hi - lo < 4 * Spec.Of(d.Sym).Tick) return null;
                double w = hi - lo, stopD = sm == "OPP" ? w : w / 2;
                RTrade best = null;
                foreach (int side in dd == 0 ? new[] { 1, -1 } : new[] { dd })
                {
                    var t = Trade(d, side, "STOP", side > 0 ? hi : lo, oo + nn, oo + nn + ww, stopD, r2 > 0 ? r2 * stopD : 0, s1555, "ORB");
                    if (t != null && (best == null || t.InSlot < best.InSlot)) best = t;
                }
                return best;
            }});
        }

        // F2 FAILED BREAKOUT (fade back inside the opening range)
        foreach (int o in opens)
        foreach (int n in new[] { 15, 30, 60 })
        foreach (int dir in new[] { 1, -1, 0 })
        foreach (string tm in new[] { "OPP", "1R", "2R" })
        {
            int oo = o, nn = n, dd = dir; string t2 = tm;
            v.Add(new Variant { Family = "FADE", Sym = sym, Name = "FAILED BREAK " + T(o) + " " + n + "m " + D(dir) + " target " + tm, Run = d =>
            {
                double hi = d.Hi(oo, oo + nn - 1), lo = d.Lo(oo, oo + nn - 1); if (double.IsNaN(hi)) return null;
                bool brokeUp = false, brokeDn = false; double ext = double.NaN;
                for (int i = oo + nn; i < oo + nn + 180 && i < s1555; i++)
                {
                    if (!d.Has(i)) continue;
                    if (!brokeUp && !brokeDn) { if (d.H[i] > hi) { brokeUp = true; ext = d.H[i]; } else if (d.L[i] < lo) { brokeDn = true; ext = d.L[i]; } }
                    if (brokeUp) { ext = Math.Max(ext, d.H[i]); if (d.C[i] < hi && (dd <= 0)) { double sd = Math.Min(ext - d.C[i], 1.5 * (hi - lo)) + Spec.Of(d.Sym).Tick; double tg = t2 == "OPP" ? d.C[i] - lo : t2 == "1R" ? sd : 2 * sd; return tg <= 0 ? null : Trade(d, -1, "MKT", 0, i + 1, i + 1, sd, tg, s1555, "FADE"); } if (d.C[i] < hi) return null; }
                    if (brokeDn) { ext = Math.Min(ext, d.L[i]); if (d.C[i] > lo && (dd >= 0)) { double sd = Math.Min(d.C[i] - ext, 1.5 * (hi - lo)) + Spec.Of(d.Sym).Tick; double tg = t2 == "OPP" ? hi - d.C[i] : t2 == "1R" ? sd : 2 * sd; return tg <= 0 ? null : Trade(d, 1, "MKT", 0, i + 1, i + 1, sd, tg, s1555, "FADE"); } if (d.C[i] > lo) return null; }
                }
                return null;
            }});
        }

        // F3 TIME WINDOWS (hold from a to b) — the "when does it drift" map
        var starts = new[] { S(18, 0), S(20, 0), S(0, 0), S(3, 0), S(8, 0), S(9, 30), S(10, 0), S(11, 0), S(12, 0), S(13, 0), S(14, 0), S(15, 0), S(15, 30) };
        var ends = new[] { S(3, 0), S(8, 0), S(9, 29), S(10, 0), S(11, 0), S(12, 0), S(13, 0), S(14, 0), S(15, 0), S(15, 55), S(16, 44) };
        foreach (int a in starts) foreach (int b in ends) if (b > a) foreach (int dir in new[] { 1, -1 }) foreach (double sk in new[] { 0.0, 0.5 })
        {
            int aa = a, bb = b, dd = dir; double kk = sk;
            v.Add(new Variant { Family = "TIME", Sym = sym, Name = "HOLD " + T(a) + "→" + T(b) + " " + D(dir) + (sk > 0 ? " stop " + sk + "×ATR" : " no stop"), Run = d => Trade(d, dd, "MKT", 0, aa, aa + 5, kk > 0 ? kk * d.Atr : 0, 0, bb - 1, "TIME") });
        }

        // F4 INTRADAY MOMENTUM: the day's move so far → the last half hour (Gao, Han, Li & Zhou 2018)
        foreach (int sig in new[] { S(10, 0), S(12, 0), S(15, 0), S(15, 30) })
        foreach (string basis in new[] { "PREVCLOSE", "OPEN" })
        foreach (int trade in new[] { S(15, 0), S(15, 30) })
        foreach (int sense in new[] { 1, -1 })
        foreach (double th in new[] { 0.0, 0.25 })
        {
            if (sig > trade) continue;
            int sg = sig, tr = trade, se = sense; string bs = basis; double thr = th;
            v.Add(new Variant { Family = "MOMENTUM", Sym = sym, Name = (sense > 0 ? "FOLLOW" : "REVERSE") + " move " + (basis == "OPEN" ? "09:30" : "prev close") + "→" + T(sig) + (th > 0 ? " (>" + th + "×ATR)" : "") + ", trade " + T(trade) + "→15:59", Run = d =>
            {
                double from = bs == "OPEN" ? d.OpenAt(S(9, 30)) : (d.Prev == null ? double.NaN : d.Prev.CloseAt(S(15, 59)));
                double now = d.CloseAt(sg - 1); if (double.IsNaN(from) || double.IsNaN(now)) return null;
                double mv = now - from; if (Math.Abs(mv) <= thr * d.RthAtr || mv == 0) return null;
                return Trade(d, se * Math.Sign(mv), "MKT", 0, tr, tr + 3, 0, 0, S(15, 59), "MOM");
            }});
        }

        // F5 OPENING GAP: fade toward yesterday's close, or go with it
        foreach (double g in new[] { 0.1, 0.2, 0.35, 0.5 })
        foreach (string mode in new[] { "FADE", "GO" })
        foreach (double k in new[] { 1.0, 2.0 })
        foreach (int exitAt in new[] { S(11, 0), S(15, 55) })
        foreach (int only in new[] { 0, 1, -1 })
        {
            double gg = g, kk = k; string md = mode; int ex = exitAt, on = only;
            v.Add(new Variant { Family = "GAP", Sym = sym, Name = "GAP " + mode + " >" + g + "×ATR " + (only == 0 ? "up+down gaps" : only > 0 ? "up gaps" : "down gaps") + " stop " + k + "×gap exit " + T(exitAt), Run = d =>
            {
                if (d.Prev == null) return null; double pc = d.Prev.CloseAt(S(15, 59)), op = d.OpenAt(S(9, 30)); double gap = op - pc;
                if (Math.Abs(gap) < gg * d.RthAtr) return null; if (on != 0 && Math.Sign(gap) != on) return null;
                int dir = md == "FADE" ? -Math.Sign(gap) : Math.Sign(gap);
                return Trade(d, dir, "MKT", 0, S(9, 30), S(9, 31), kk * Math.Abs(gap), md == "FADE" ? Math.Abs(gap) : kk * Math.Abs(gap), ex, "GAP");
            }});
        }

        // F6 VWAP STRETCH: fade (or follow) a stretch away from the 09:30 VWAP
        foreach (double dv in new[] { 0.25, 0.35, 0.5 })
        foreach (string mode in new[] { "FADE", "FOLLOW" })
        foreach (int dir in new[] { 1, -1, 0 })
        foreach (double sk in new[] { 0.5, 1.0 })
        {
            double dvv = dv, skk = sk; string md = mode; int dd = dir;
            v.Add(new Variant { Family = "VWAP", Sym = sym, Name = "VWAP " + mode + " stretch " + dv + "×ATR " + D(dir) + " stop " + sk + "×stretch", Run = d =>
            {
                int a = S(9, 30); double pv = 0, vv = 0;
                for (int i = a; i < S(15, 0); i++)
                {
                    if (!d.Has(i)) continue; double w = Math.Max(1, d.V[i]); pv += (d.H[i] + d.L[i] + d.C[i]) / 3 * w; vv += w;
                    if (i < S(10, 0)) continue;
                    double vw = pv / vv, dev = d.C[i] - vw;
                    if (Math.Abs(dev) < dvv * d.RthAtr) continue;
                    int side = md == "FADE" ? -Math.Sign(dev) : Math.Sign(dev); if (dd != 0 && side != dd) return null;
                    return Trade(d, side, "MKT", 0, i + 1, i + 1, skk * Math.Abs(dev), Math.Abs(dev), S(15, 55), "VWAP");
                }
                return null;
            }});
        }

        // F7 YESTERDAY'S HIGH / LOW: break or fade the first touch
        foreach (string lvl in new[] { "PDH", "PDL" })
        foreach (string mode in new[] { "BREAK", "FADE" })
        foreach (double x in new[] { 0.1, 0.2 })
        foreach (double rr in new[] { 1, 2, 3 })
        {
            string lv = lvl, md = mode; double xx = x, r2 = rr;
            v.Add(new Variant { Family = "PDHL", Sym = sym, Name = mode + " " + lvl + " stop " + x + "×ATR " + rr + "R", Run = d =>
            {
                if (d.Prev == null) return null; double level = lv == "PDH" ? d.Prev.Hi(S(9, 30), S(15, 59)) : d.Prev.Lo(S(9, 30), S(15, 59));
                double op = d.OpenAt(S(9, 30)); if (double.IsNaN(level) || double.IsNaN(op)) return null;
                if (lv == "PDH" ? op >= level : op <= level) return null;   // opened beyond the level: no first touch
                int dir = (lv == "PDH") == (md == "BREAK") ? 1 : -1;
                string et = md == "BREAK" ? "STOP" : "LIMIT"; double sd = xx * d.Atr;
                return Trade(d, dir, et, level, S(9, 30), S(15, 0), sd, r2 * sd, S(15, 55), md);
            }});
        }

        // F8 TREND DAY: above the 30-minute range and VWAP at 10:30 / 11:00 → go with it to the close
        foreach (int at in new[] { S(10, 30), S(11, 0) })
        foreach (int dir in new[] { 1, -1, 0 })
        foreach (string st in new[] { "VWAP", "ORMID" })
        foreach (double rr in new[] { 0, 2 })
        {
            int aa = at, dd = dir; string s2 = st; double r2 = rr;
            v.Add(new Variant { Family = "TREND", Sym = sym, Name = "TREND " + T(at) + " " + D(dir) + " stop " + st + (rr > 0 ? " 2R" : " to 15:55"), Run = d =>
            {
                int o = S(9, 30); double hi = d.Hi(o, o + 29), lo = d.Lo(o, o + 29), c = d.CloseAt(aa - 1), vw = Vwap(d, o, aa - 1);
                if (double.IsNaN(hi) || double.IsNaN(c)) return null;
                int side = c > hi && c > vw ? 1 : c < lo && c < vw ? -1 : 0; if (side == 0 || (dd != 0 && side != dd)) return null;
                double stopPx = s2 == "VWAP" ? vw : (hi + lo) / 2; double sd = Math.Abs(c - stopPx) + 0.05 * d.Atr;
                return Trade(d, side, "MKT", 0, aa, aa + 2, sd, r2 * sd, S(15, 55), "TREND");
            }});
        }

        // F9 ASIA RANGE → LONDON BREAKOUT
        foreach (int rangeEnd in new[] { S(0, 0), S(2, 0) })
        foreach (int dir in new[] { 1, -1, 0 })
        foreach (string sm in new[] { "OPP", "MID" })
        foreach (double rr in new[] { 1, 2, 0 })
        foreach (int exitAt in new[] { S(9, 25), S(11, 0) })
        {
            int re = rangeEnd, dd = dir, ex = exitAt; string s2 = sm; double r2 = rr;
            v.Add(new Variant { Family = "ASIA", Sym = sym, Name = "ASIA 18:00→" + T(re) + " break " + D(dir) + " stop " + sm + " " + (rr > 0 ? rr + "R" : "time") + " exit " + T(exitAt), Run = d =>
            {
                double hi = d.Hi(0, re - 1), lo = d.Lo(0, re - 1); if (double.IsNaN(hi) || hi - lo < 4 * Spec.Of(d.Sym).Tick) return null;
                double sd = s2 == "OPP" ? hi - lo : (hi - lo) / 2; RTrade best = null;
                foreach (int side in dd == 0 ? new[] { 1, -1 } : new[] { dd })
                {
                    var t = Trade(d, side, "STOP", side > 0 ? hi : lo, re, S(5, 0), sd, r2 * sd, ex, "ASIA");
                    if (t != null && (best == null || t.InSlot < best.InSlot)) best = t;
                }
                return best;
            }});
        }
        return v;
    }
}
