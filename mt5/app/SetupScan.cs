// SpasiX — SETUPS NOW: how strong each researched setup is on the symbol right now (🤖 AUTO tab). It only reads candles; it never trades.
//   A  London engulfing reversal (M5): an engulfing candle at a 20-candle low / high, buy under the VWAP / sell over it, 03:00-06:00 New York
//   B  New York FVG 50% (M5): a strong fair value gap, buy over the VWAP / sell under it, the limit at the gap's middle, 08:30-11:30 New York
//   BOX the consolidation of the last 96 H1 candles: inside it = do NOT hedge (wait); a break of its high / low = the side to hedge / flip to
//   C  all day VWAP rejection (M15): 6 candles away from the VWAP, a candle back to it closing away again, 03:00-15:30 New York
// STRONG = every condition the research needs; WEAK = the signal is there but a condition is missing (named); WAIT = no signal.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class SetupState { public string Name = "", Level = "WAIT", Text = "", Key = ""; public int Dir; public double Price = double.NaN; public readonly List<SetupDraw> Draw = new List<SetupDraw>(); }
    // what a setup watches, for the chart: LINE (P1), ZONE (P1–P2 from a time), SIGNAL (a pulsing dot at P1 on the last candle)
    public sealed class SetupDraw { public string Kind = "LINE", Label = ""; public double P1, P2 = double.NaN; public DateTime From = DateTime.MinValue; public bool Dash; }

    public static class SetupScan
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string P(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.##", IC); }

        // the finished candles of a timeframe, newest first
        static List<Mt5Bar> Fin(Mt5State st, string sym, int tf, DateTime now, int n)
        {
            var se = st.SeriesOf(sym, tf); var res = new List<Mt5Bar>();
            for (int i = se.Bars.Count - 1; i >= 0 && res.Count < n; i--) if (se.Bars[i].Time.AddMinutes(tf) <= now) res.Add(se.Bars[i]);
            return res;
        }
        // the server day's VWAP from the M5 candles (typical price × tick volume)
        public static double Vwap(List<Mt5Bar> m5newestFirst, DateTime now)
        {
            double pv = 0, v = 0; foreach (var b in m5newestFirst) { if (b.Time.Date != now.Date) break; double w = Math.Max(1, b.V); pv += (b.H + b.L + b.C) / 3 * w; v += w; }
            return v > 0 ? pv / v : double.NaN;
        }
        static bool In(int m, int a, int b) { return m >= a && m < b; }
        public static double LastVwap = double.NaN;

        public static List<SetupState> Scan(IBroker br, string sym, int nyOffset)
        {
            var res = new List<SetupState>(); if (br == null || string.IsNullOrWhiteSpace(sym)) return res;
            List<Mt5Bar> m5, h1, m15; DateTime now; double bid = double.NaN;
            lock (br.State.Gate)
            {
                var st = br.State; now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; Mt5Quote q; if (st.Quotes.TryGetValue(sym, out q)) bid = q.Bid;
                m5 = Fin(st, sym, 5, now, 300); h1 = Fin(st, sym, 60, now, 100); m15 = Fin(st, sym, 15, now, 100);
                if (!br.IsSim) foreach (var tf in new[] { 5, 15, 60 }) { var se = st.SeriesOf(sym, tf); if (!se.Loaded && (DateTime.Now - se.AskedAt).TotalSeconds > 5) { se.AskedAt = DateTime.Now; br.Send("BARS|" + sym + "|" + tf + "|300"); } }
            }
            var ny = now.AddHours(nyOffset); int nm = ny.Hour * 60 + ny.Minute; double vw = Vwap(m5, now); LastVwap = vw;
            res.Add(A(m5, vw, bid, nm)); res.Add(B(m5, vw, bid, nm)); res.Add(C(m15, now, nm)); res.Add(Box(h1, bid));
            return res;
        }

        static SetupState A(List<Mt5Bar> f, double vw, double bid, int nm)
        {
            var s = new SetupState { Name = "A · London engulfing (M5)", Key = "A" };
            if (f.Count < 22) { s.Text = "loading M5 candles…"; return s; }
            var c = f[0]; var pr = f[1]; double ll = f.Skip(2).Take(20).Min(x => x.L), hh = f.Skip(2).Take(20).Max(x => x.H);
            bool bull = pr.C < pr.O && c.C > c.O && c.C >= pr.O && c.O <= pr.C, bear = pr.C > pr.O && c.C < c.O && c.C <= pr.O && c.O >= pr.C;
            int d = bull && Math.Min(c.L, pr.L) <= ll ? 1 : bear && Math.Max(c.H, pr.H) >= hh ? -1 : 0;
            bool win = In(nm, 180, 360); var from = f[Math.Min(f.Count - 1, 21)].Time;
            s.Draw.Add(new SetupDraw { P1 = hh, From = from, Dash = true, Label = "A · 20-candle HIGH " + P(hh) + " (bearish engulfing here = SELL)" });
            s.Draw.Add(new SetupDraw { P1 = ll, From = from, Dash = true, Label = "A · 20-candle LOW " + P(ll) + " (bullish engulfing here = BUY)" });
            if (d == 0)
            {
                bool nearLow = !double.IsNaN(bid) && bid - ll < (hh - ll) * 0.15, nearHigh = !double.IsNaN(bid) && hh - bid < (hh - ll) * 0.15;
                s.Text = (win ? "in the London window" : "outside 03:00-06:00 NY") + " • no engulfing at a 20-candle extreme" + (nearLow ? " • price near the 20-candle LOW " + P(ll) + ": watch for a bullish engulfing" : nearHigh ? " • price near the 20-candle HIGH " + P(hh) + ": watch for a bearish engulfing" : "");
                return s;
            }
            s.Dir = d; s.Price = bid; var miss = new List<string>();
            if (!win) miss.Add("outside 03:00-06:00 NY");
            bool disc = !double.IsNaN(vw) && (d > 0 ? bid < vw : bid > vw); if (!disc) miss.Add(d > 0 ? "price is OVER the VWAP " + P(vw) : "price is UNDER the VWAP " + P(vw));
            s.Level = miss.Count == 0 ? "STRONG" : "WEAK"; s.Draw.Add(new SetupDraw { Kind = "SIGNAL", P1 = c.C, Label = "A " + s.Level + (d > 0 ? " BUY ▲" : " SELL ▼") });
            s.Text = (d > 0 ? "BUY" : "SELL") + " — the " + c.Time.ToString("HH:mm", IC) + " candle engulfed at the 20-candle " + (d > 0 ? "low " + P(Math.Min(c.L, pr.L)) : "high " + P(Math.Max(c.H, pr.H))) + (miss.Count == 0 ? " • VWAP " + P(vw) + " agrees • target +$20 (0.1 lot), emergency stop −$300" : " • missing: " + string.Join(", ", miss));
            return s;
        }

        static SetupState B(List<Mt5Bar> f, double vw, double bid, int nm)
        {
            var s = new SetupState { Name = "B · NY FVG 50% (M5)", Key = "B" };
            if (f.Count < 26) { s.Text = "loading M5 candles…"; return s; }
            // the newest strong gap of the last 3 candles that the price has not reached its middle yet
            for (int i = 0; i < 3; i++)
            {
                var c3 = f[i]; var c2 = f[i + 1]; var c1 = f[i + 2]; double avg = f.Skip(i + 3).Take(20).Average(x => x.H - x.L);
                int d = c3.L > c1.H && c2.C - c2.O > avg ? 1 : c3.H < c1.L && c2.O - c2.C > avg ? -1 : 0; if (d == 0) continue;
                double mid = d > 0 ? (c3.L + c1.H) / 2 : (c3.H + c1.L) / 2; bool touched = f.Take(i).Any(x => d > 0 ? x.L <= mid : x.H >= mid);
                if (touched) continue;
                s.Dir = d; s.Price = mid; var miss = new List<string>();
                if (!In(nm, 510, 690)) miss.Add("outside 08:30-11:30 NY");
                bool flow = !double.IsNaN(vw) && (d > 0 ? bid > vw : bid < vw); if (!flow) miss.Add(d > 0 ? "price is UNDER the VWAP " + P(vw) : "price is OVER the VWAP " + P(vw));
                if (i * 5 > 12) miss.Add("the gap is older than 12 minutes");
                s.Level = miss.Count == 0 ? "STRONG" : "WEAK";
                s.Draw.Add(new SetupDraw { Kind = "ZONE", P1 = d > 0 ? c1.H : c3.H, P2 = d > 0 ? c3.L : c1.L, From = c1.Time, Label = "B · " + (d > 0 ? "bullish" : "bearish") + " FVG (" + s.Level + ")" });
                s.Draw.Add(new SetupDraw { P1 = mid, From = c1.Time, Dash = true, Label = "B · " + (d > 0 ? "BUY" : "SELL") + " LIMIT 50% " + P(mid) });
                s.Text = (d > 0 ? "BUY LIMIT " : "SELL LIMIT ") + P(mid) + " — the middle of the " + (d > 0 ? "bullish" : "bearish") + " gap " + P(d > 0 ? c1.H : c3.H) + "–" + P(d > 0 ? c3.L : c1.L) + " (" + c3.Time.ToString("HH:mm", IC) + ")" + (miss.Count == 0 ? " • VWAP " + P(vw) + " agrees • +$20, no stop" : " • missing: " + string.Join(", ", miss));
                return s;
            }
            s.Text = (In(nm, 510, 690) ? "in the NY window" : "outside 08:30-11:30 NY") + " • no fresh strong M5 gap";
            return s;
        }

        static SetupState C(List<Mt5Bar> f, DateTime now, int nm)
        {
            var s = new SetupState { Name = "C · all day VWAP rejection (M15)", Key = "C" };
            var day = f.Where(x => x.Time.Date == now.Date).Reverse().ToList(); bool win = In(nm, 180, 930);
            if (day.Count < 7) { s.Text = (win ? "in 03:00-15:30 NY" : "outside 03:00-15:30 NY") + " • fewer than 7 M15 candles today"; return s; }
            var vw = new double[day.Count]; double pv = 0, vv = 0; for (int i = 0; i < day.Count; i++) { double w = Math.Max(1, day[i].V); pv += (day[i].H + day[i].L + day[i].C) / 3 * w; vv += w; vw[i] = pv / vv; }
            int n = day.Count - 1; bool above = true, below = true; for (int k = n - 6; k < n; k++) { if (day[k].L <= vw[k]) above = false; if (day[k].H >= vw[k]) below = false; }
            var c = day[n]; int d = above && c.L <= vw[n] && c.C > vw[n] ? 1 : below && c.H >= vw[n] && c.C < vw[n] ? -1 : 0;
            if (d != 0) s.Draw.Add(new SetupDraw { Kind = "SIGNAL", P1 = vw[n], Label = "C " + (win ? "STRONG" : "WEAK") + (d > 0 ? " BUY ▲ VWAP rejected" : " SELL ▼ VWAP rejected") });
            if (d == 0) { s.Text = (win ? "in 03:00-15:30 NY" : "outside 03:00-15:30 NY") + " • VWAP " + P(vw[n]) + (above ? " • 6+ candles ABOVE it: watch for a dip that closes back above (buy)" : below ? " • 6+ candles UNDER it: watch for a rise that closes back under (sell)" : " • price is crossing the VWAP (no trend away from it)"); s.Level = above || below ? "WEAK" : "WAIT"; return s; }
            s.Dir = d; s.Price = c.C; s.Level = win ? "STRONG" : "WEAK";
            s.Text = (d > 0 ? "BUY" : "SELL") + " — the " + c.Time.ToString("HH:mm", IC) + " candle rejected the VWAP " + P(vw[n]) + (win ? " • +$20, emergency stop −$200, close 16:55 NY" : " • missing: outside 03:00-15:30 NY");
            return s;
        }

        static SetupState Box(List<Mt5Bar> f, double bid)
        {
            var s = new SetupState { Name = "HEDGE BOX · 96 H1 consolidation", Key = "BOX" };
            if (f.Count < 24 || double.IsNaN(bid)) { s.Text = "loading H1 candles…"; return s; }
            var w = f.Take(96).ToList(); double hi = w.Max(x => x.H), lo = w.Min(x => x.L), pos = hi > lo ? (bid - lo) / (hi - lo) : 0.5;
            // how choppy: how often the H1 candles crossed the box's middle
            s.Draw.Add(new SetupDraw { Kind = "ZONE", P1 = hi, P2 = lo, From = w.Last().Time, Label = "" });
            s.Draw.Add(new SetupDraw { P1 = hi, From = w.Last().Time, Label = "HEDGE BOX top " + P(hi) + " — hedge / flip BUY only above" });
            s.Draw.Add(new SetupDraw { P1 = lo, From = w.Last().Time, Label = "HEDGE BOX bottom " + P(lo) + " — hedge / flip SELL only below" });
            double mid = (hi + lo) / 2; int crosses = 0; for (int i = 1; i < w.Count; i++) if ((w[i].C - mid) * (w[i - 1].C - mid) < 0) crosses++;
            if (bid > hi || bid < lo) { s.Level = "STRONG"; s.Dir = bid > hi ? 1 : -1; s.Price = bid > hi ? hi : lo; s.Text = "BREAKOUT " + (bid > hi ? "ABOVE " + P(hi) + " → the hedge / flip side is BUY" : "BELOW " + P(lo) + " → the hedge / flip side is SELL") + " • box " + P(lo) + "–" + P(hi) + " ($" + P(hi - lo) + ")"; return s; }
            s.Level = pos > 0.85 || pos < 0.15 ? "WEAK" : "WAIT";
            s.Text = "INSIDE the consolidation " + P(lo) + "–" + P(hi) + " ($" + P(hi - lo) + ", price at " + (pos * 100).ToString("0", IC) + "%, " + crosses + " middle crossings) → do NOT hedge here; hedge only at a break of " + P(hi) + " (buy) or " + P(lo) + " (sell)" + (s.Level == "WEAK" ? " • price is close to the " + (pos > 0.5 ? "top" : "bottom") + " edge" : "");
            return s;
        }
    }
}
