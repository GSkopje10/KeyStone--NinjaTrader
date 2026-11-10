// SpasiX — 🚀 TREND STACK (09e, the user's request: "detect the short-term trend so we can stack up higher lots" to get out of a stuck basket).
// EXPERIMENT: only on simulated accounts (🎮 DEMO, 🏁 RACE, 🧪 BACKTEST) — HedgeAuto.Check refuses it on any MT5 account (REAL / PROP / MT5 demo).
// The detector reads FINISHED candles only (M1 + M5 built from the M1): price vs EMA 9 / 21 with both sloping the same way, higher highs + higher
// lows (or lower), a close beyond the last M1 swing, the VWAP side, the ATR(14) not collapsing → UP / DOWN / NONE and a strength 0–100.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class TrendState
    {
        public int Dir; public int Strength, ScoreUp, ScoreDown; public string Why = "";
        public double SwingHi = double.NaN, SwingLo = double.NaN, Atr1 = double.NaN, Atr5 = double.NaN, Close = double.NaN;
        public bool CrossUp, CrossDown; public DateTime Bar;
        public string Text { get { return Dir == 0 ? "➖ NO TREND " + Strength + (Why != "" ? " · " + Why : "") : (Dir > 0 ? "📈 TREND UP " : "📉 TREND DOWN ") + Strength + " · " + Why; } }
    }

    public sealed class RecoveryTicket { public int Dir; public double Lots, Entry, Protect = double.NaN, RiskLeg, BasketAtProtect, BePx = double.NaN, TpPx = double.NaN, GainAtMove, Move, NetAfter; public string Text = ""; }

    public static class TrendDetect
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static double[] Ema(IList<double> v, int n)
        {
            var e = new double[v.Count]; if (v.Count == 0) return e; double k = 2.0 / (n + 1); e[0] = v[0];
            for (int i = 1; i < v.Count; i++) e[i] = v[i] * k + e[i - 1] * (1 - k);
            return e;
        }
        public static double Atr(IList<Mt5Bar> b, int end, int n)
        {
            if (end < 1) return double.NaN; double s = 0; int c = 0;
            for (int i = Math.Max(1, end - n + 1); i <= end; i++) { s += Math.Max(b[i].H, b[i - 1].C) - Math.Min(b[i].L, b[i - 1].C); c++; }
            return c > 0 ? s / c : double.NaN;
        }
        // M5 candles from finished M1 candles (oldest first); only complete 5-minute buckets
        public static List<Mt5Bar> ToM5(IList<Mt5Bar> m1)
        {
            var res = new List<Mt5Bar>();
            foreach (var g in m1.GroupBy(x => new DateTime(x.Time.Ticks - x.Time.Ticks % (TimeSpan.TicksPerMinute * 5))))
            {
                var l = g.OrderBy(x => x.Time).ToList(); if (l.Count < 5) continue;
                res.Add(new Mt5Bar { Time = g.Key, O = l[0].O, C = l[l.Count - 1].C, H = l.Max(x => x.H), L = l.Min(x => x.L), V = l.Sum(x => x.V) });
            }
            return res.OrderBy(x => x.Time).ToList();
        }
        // m1 = finished M1 candles, OLDEST first (60+ for a reading; 125+ for the M5 part); vwap = the day's VWAP (NaN = not used)
        public static TrendState Read(IList<Mt5Bar> m1, double vwap)
        {
            var t = new TrendState(); if (m1 == null || m1.Count < 30) { t.Why = "not enough candles"; return t; }
            int i = m1.Count - 1; t.Bar = m1[i].Time; var c = m1.Select(x => x.C).ToList(); var e9 = Ema(c, 9); var e21 = Ema(c, 21); t.Close = c[i];
            double a1 = Atr(m1, i, 14), gap = 0.1 * (double.IsNaN(a1) ? 0 : a1), slope = 0.05 * (double.IsNaN(a1) ? 0 : a1);   // a real separation / slope, not noise (a flat chop never reads as a trend)
            bool up1 = c[i] > e9[i] && e9[i] - e21[i] > gap && e9[i] - e9[i - 3] > slope && e21[i] > e21[i - 3], dn1 = c[i] < e9[i] && e21[i] - e9[i] > gap && e9[i - 3] - e9[i] > slope && e21[i] < e21[i - 3];
            t.CrossUp = e9[i] > e21[i] && e9[i - 1] <= e21[i - 1]; t.CrossDown = e9[i] < e21[i] && e9[i - 1] >= e21[i - 1];
            var m5 = ToM5(m1); bool up5 = false, dn5 = false, have5 = m5.Count >= 25;
            if (have5)
            {
                int j = m5.Count - 1; var c5 = m5.Select(x => x.C).ToList(); var f9 = Ema(c5, 9); var f21 = Ema(c5, 21);
                t.Atr5 = Atr(m5, j, 14); double g5 = 0.1 * (double.IsNaN(t.Atr5) ? 0 : t.Atr5);
                up5 = c5[j] > f9[j] && f9[j] - f21[j] > g5 && f9[j] > f9[j - 2] && f21[j] >= f21[j - 2]; dn5 = c5[j] < f9[j] && f21[j] - f9[j] > g5 && f9[j] < f9[j - 2] && f21[j] <= f21[j - 2];
            }
            int hh = 0, ll = 0; for (int k = i; k >= 1 && m1[k].H > m1[k - 1].H && m1[k].L > m1[k - 1].L; k--) hh++; for (int k = i; k >= 1 && m1[k].H < m1[k - 1].H && m1[k].L < m1[k - 1].L; k--) ll++;
            // the last M1 swing high / low (a candle higher / lower than the 2 on each side) BEFORE the newest two candles
            for (int k = i - 2; k >= Math.Max(2, i - 40) && double.IsNaN(t.SwingHi); k--) if (m1[k].H > m1[k - 1].H && m1[k].H > m1[k - 2].H && m1[k].H >= m1[k + 1].H && m1[k].H >= m1[k + 2].H) t.SwingHi = m1[k].H;
            for (int k = i - 2; k >= Math.Max(2, i - 40) && double.IsNaN(t.SwingLo); k--) if (m1[k].L < m1[k - 1].L && m1[k].L < m1[k - 2].L && m1[k].L <= m1[k + 1].L && m1[k].L <= m1[k + 2].L) t.SwingLo = m1[k].L;
            bool brkUp = !double.IsNaN(t.SwingHi) && c[i] > t.SwingHi, brkDn = !double.IsNaN(t.SwingLo) && c[i] < t.SwingLo;
            bool vUp = !double.IsNaN(vwap) && c[i] > vwap, vDn = !double.IsNaN(vwap) && c[i] < vwap;
            t.Atr1 = Atr(m1, i, 14); double atrOld = Atr(m1, Math.Max(15, i - 20), 14); bool atrOk = !double.IsNaN(t.Atr1) && (double.IsNaN(atrOld) || atrOld <= 0 || t.Atr1 >= 0.7 * atrOld);
            if (double.IsNaN(t.Atr5) && !double.IsNaN(t.Atr1)) t.Atr5 = t.Atr1 * Math.Sqrt(5);
            Func<bool, bool, int, bool, bool, int> score = (e1, e5, run, brk, vw) => (e1 ? 25 : 0) + (e5 ? 25 : 0) + (run >= 2 ? Math.Min(25, 15 + 5 * (run - 2)) : 0) + (brk ? 15 : 0) + (vw ? 10 : 0);
            t.ScoreUp = Math.Min(100, score(up1, up5, hh, brkUp, vUp) + (atrOk && up1 ? 10 : 0)); t.ScoreDown = Math.Min(100, score(dn1, dn5, ll, brkDn, vDn) + (atrOk && dn1 ? 10 : 0));
            if (up1 && t.ScoreUp >= 50 && t.ScoreUp > t.ScoreDown) t.Dir = 1; else if (dn1 && t.ScoreDown >= 50 && t.ScoreDown > t.ScoreUp) t.Dir = -1;
            t.Strength = t.Dir > 0 ? t.ScoreUp : t.Dir < 0 ? t.ScoreDown : Math.Max(t.ScoreUp, t.ScoreDown);
            var w = new List<string>(); bool u = t.Dir > 0 || (t.Dir == 0 && t.ScoreUp >= t.ScoreDown);
            if (u ? up1 : dn1) w.Add("EMA9" + (u ? ">" : "<") + "21 M1" + ((u ? up5 : dn5) ? "+M5" : "")); else if (u ? up5 : dn5) w.Add("EMA9" + (u ? ">" : "<") + "21 M5");
            int run2 = u ? hh : ll; if (run2 >= 2) w.Add(run2 + (u ? " HH/HL" : " LH/LL"));
            if (u ? brkUp : brkDn) w.Add("broke " + (u ? t.SwingHi : t.SwingLo).ToString("0.00", IC));
            if (u ? vUp : vDn) w.Add((u ? "above" : "below") + " VWAP");
            if (!atrOk) w.Add("ATR collapsing");
            t.Why = w.Count > 0 ? string.Join(", ", w) : "no alignment";
            return t;
        }
        // the day's VWAP from finished M1 candles (typical price × tick volume)
        public static double DayVwap(IList<Mt5Bar> m1, DateTime day)
        {
            double pv = 0, v = 0; foreach (var b in m1) if (b.Time.Date == day.Date) { double w = Math.Max(1, b.V); pv += (b.H + b.L + b.C) / 3 * w; v += w; }
            return v > 0 ? pv / v : double.NaN;
        }
        public const string RecoveryHonest = "The six-year test of automatic stacking LOST (09e); this is a suggestion with its risk shown, not a proven edge.";
        // 🧭 RECOVERY ASSIST (09f): a recovery trade for a basket in trouble — side = the confirmed short-term trend, size = the stack formula
        // (capped), the protection = the last swing against it (else 1 ATR M5), the exit = the BASKET's breakeven / target (the 🎯 EXIT GUARD closes all there)
        public static RecoveryTicket Recovery(double total, double target, double net, double px, double perPrice, double balance, double capPer1k, double k, double marginLevel, double minMargin, TrendState tr, double step = 0.01)
        {
            var r = new RecoveryTicket();
            if (tr == null || tr.Dir == 0) { r.Text = "💡 RECOVERY: no trade — no clear trend" + (tr != null ? " (" + tr.Text + ")" : ""); return r; }
            if (marginLevel > 0 && marginLevel < minMargin) { r.Text = "💡 RECOVERY: no trade — margin " + marginLevel.ToString("0", IC) + " % < " + minMargin.ToString("0", IC) + " %"; return r; }
            int dir = tr.Dir; double cap = Math.Max(step, Math.Round(Math.Max(1, balance) / 1000 * capPer1k, 2)); r.Move = k * (double.IsNaN(tr.Atr5) ? 0 : tr.Atr5); string why;
            double lots = StackLots(total, target, r.Move, perPrice, net * dir, step, cap, out why); if (lots < step - 1e-9) { r.Text = "💡 RECOVERY: no trade — " + why; return r; }
            r.Dir = dir; r.Lots = lots; r.Entry = px; double after = Math.Round(net + dir * lots, 2); r.NetAfter = after;
            r.BePx = Math.Abs(after) < 0.005 || perPrice <= 0 ? double.NaN : px + (0 - total) / (after * perPrice); r.TpPx = Math.Abs(after) < 0.005 || perPrice <= 0 ? double.NaN : px + (target - total) / (after * perPrice);
            double sw = dir < 0 ? tr.SwingHi : tr.SwingLo; r.Protect = !double.IsNaN(sw) && (dir < 0 ? sw > px : sw < px) ? sw : px - dir * (double.IsNaN(tr.Atr5) ? 0 : tr.Atr5);
            r.RiskLeg = Math.Abs(r.Protect - px) * lots * perPrice; r.BasketAtProtect = total + (r.Protect - px) * after * perPrice; r.GainAtMove = total + after * perPrice * dir * r.Move;
            r.Text = "💡 RECOVERY: " + (dir > 0 ? "BUY " : "SELL ") + lots.ToString("0.##", IC) + " now @ " + px.ToString("0.00", IC) + " — " + tr.Text + " · the basket " + Usd(total) + " → breakeven " + r.BePx.ToString("0.00", IC) + ", +$" + target.ToString("0.##", IC) + " at " + r.TpPx.ToString("0.00", IC) + " (the 🎯 EXIT GUARD closes everything there)"
                + " · protection " + r.Protect.ToString("0.00", IC) + " (" + (!double.IsNaN(sw) && r.Protect == sw ? "the last swing " + (dir < 0 ? "high" : "low") : "1 ATR") + ") = −$" + r.RiskLeg.ToString("0", IC) + " on this leg, the basket " + Usd(r.BasketAtProtect) + " there"
                + " · ≈ " + Usd(r.GainAtMove) + " total if the price moves 1.5 ATR ($" + r.Move.ToString("0.00", IC) + ") your way. " + RecoveryHonest;
            return r;
        }
        static string Usd(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", IC); }
        // 🚀 the stack size: add = clamp((debt + target) ÷ (expected move × $ per 1.0 per lot) − the net already with the trend, step, cap)
        // debt = how far the basket total is under 0 (≥ 0); cap = the most net lots the trend side may hold; "" = sized, else why not
        public static double StackLots(double total, double target, double expMove, double perPrice, double netWithTrend, double step, double cap, out string why)
        {
            why = ""; if (expMove <= 0 || perPrice <= 0) { why = "no expected move (ATR)"; return 0; }
            double need = Math.Max(0, target - total) / (expMove * perPrice);   // = (the debt under $0 + the target) when the total is negative
            double add = need - netWithTrend; double room = cap - netWithTrend;
            if (room < step - 1e-9) { why = "the net cap " + cap.ToString("0.##", IC) + " is reached"; return 0; }
            add = Math.Max(step, Math.Ceiling(add / step - 1e-9) * step); add = Math.Min(add, Math.Floor(room / step + 1e-9) * step);
            return Math.Round(add, 2);
        }
    }
}
