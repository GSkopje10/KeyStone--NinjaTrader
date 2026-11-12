// SpasiX — 🪜 2ND-SWING HEDGE (09o, the user's rule after his DEMO BUY 0.60 @ 3881.09 that reversed): "once the 2nd swing low is broken in
// negative start hedging, and then if it doesn't continue down and comes back, hedge at the swing high".
// A hedge TRIGGER (AutoSettings.HedgeTrigger = SWING2; LEAD = the fixed-lead break hedge, the default). Pure: the swings counted on the
// strategy's finished candles (the same SwingK pivot as the BREAK levels), the level, the break test. HedgeAuto sends through Ask → AutoSend.
//   • the newest leg is NOT one of the engine's hedges (the entry, a manual trade, an add): the hedge waits for the break of the N-th
//     confirmed swing low (net long) / high (net short) formed AFTER that leg (N = SwN, 1–3, default 2; broken swings still count)
//   • the newest leg IS a hedge: the flip back goes at the break of the most recent confirmed swing high / low not yet traded through
//   • a swing counts only when it is ≥ max(SwMinTicks ticks, SwMinAtr × the average candle of the last 14) deep — micro-wiggles are skipped
//   • HedgeAuto adds: only while the basket is in drawdown (total < 0), a cool-down of SwCool minutes after a hedge, TOUCH / a CLOSED candle,
//     and the MAX HEDGE DISTANCE from the newest leg as the safety when no N-th swing has formed yet (a straight fall)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class SwingPt { public DateTime Time; public double Price, Size; public bool High, Used; public int N; }
    public sealed class SwingPick { public double Level = double.NaN; public string Why = "", Tag = ""; public bool FlipBack; public int Have, Need; public List<SwingPt> Counted = new List<SwingPt>(); }

    public static class SwingHedge
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const string Text = "🪜 2ND-SWING: the hedge waits for the break of the 2nd swing low (net long) / high (net short) formed after the entry while the basket is in drawdown, then flips back at the most recent swing high / low — structure breaks instead of a fixed distance.";
        public static string Ord(int n) { return n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th"; }
        public static string Circled(int n) { return n >= 1 && n <= 10 ? ((char)(0x2460 + n - 1)).ToString() : n.ToString(IC); }
        // the smallest swing that counts (price)
        public static double MinSize(IList<Mt5Bar> fin, double minAtr, double minTicks, double tick)
        {
            double atr = fin != null && fin.Count > 0 ? fin.Take(14).Average(x => x.H - x.L) : 0;
            return Math.Max(Math.Max(0, minAtr) * atr, Math.Max(0, minTicks) * (tick > 0 ? tick : 0.01));
        }
        // the confirmed swings (k finished candles on each side) whose candle started at or after `after`, OLDEST first, numbered 1, 2, …
        // fin = finished candles NEWEST first (index 0 = the last finished candle)
        public static List<SwingPt> Swings(IList<Mt5Bar> fin, int k, bool high, double minSize, DateTime after)
        {
            var res = new List<SwingPt>(); k = Math.Max(1, k); if (fin == null) return res;
            for (int j = k; j + k < fin.Count; j++)
            {
                var c = fin[j]; if (c.Time < after) break;
                bool ok = true; double far = high ? c.H : c.L;
                for (int d = 1; d <= k && ok; d++)
                {
                    ok = high ? c.H > fin[j - d].H && c.H > fin[j + d].H : c.L < fin[j - d].L && c.L < fin[j + d].L;
                    if (high) far = Math.Min(far, Math.Min(fin[j - d].L, fin[j + d].L)); else far = Math.Max(far, Math.Max(fin[j - d].H, fin[j + d].H));
                }
                if (!ok) continue;
                double size = high ? c.H - Math.Min(far, c.L) : Math.Max(far, c.H) - c.L; if (size < minSize - 1e-9) continue;
                bool used = false; for (int i = 0; i < j && !used; i++) used = high ? fin[i].H > c.H : fin[i].L < c.L;   // traded through since it formed
                res.Add(new SwingPt { Time = c.Time, Price = high ? c.H : c.L, Size = size, High = high, Used = used });
            }
            res.Reverse(); for (int i = 0; i < res.Count; i++) res[i].N = i + 1; return res;
        }
        // the level for the hedge against a leg of side `dir` (+1 = the newest leg is a BUY → a SELL hedge below)
        public static SwingPick Pick(IList<Mt5Bar> fin, int k, int dir, DateTime anchor, int n, bool flipBack, double minSize)
        {
            n = Math.Max(1, Math.Min(3, n)); bool high = dir < 0; string side = high ? "high" : "low"; var pk = new SwingPick { FlipBack = flipBack, Need = flipBack ? 1 : n };
            if (flipBack)
            {
                var all = Swings(fin, k, high, minSize, DateTime.MinValue); var last = all.LastOrDefault(x => !x.Used); pk.Have = last != null ? 1 : 0;
                if (last == null) { pk.Why = "🪜 the flip back waits for a confirmed swing " + side + " (none yet)"; pk.Tag = "the next swing " + side; return pk; }
                last.N = 1; pk.Counted.Add(last); pk.Level = last.Price; pk.Tag = "the swing " + side;
                pk.Why = "🪜 FLIP BACK at the most recent swing " + side.ToUpperInvariant() + " " + last.Price.ToString("0.00", IC) + " (" + last.Time.ToString("HH:mm", IC) + ") — the hedge did not continue";
                return pk;
            }
            var sw = Swings(fin, k, high, minSize, anchor); pk.Have = sw.Count; pk.Counted = sw;
            if (sw.Count < n) { pk.Why = "🪜 counting swing " + side + "s since the leg (" + anchor.ToString("HH:mm", IC) + "): " + sw.Count + " of " + n + (sw.Count > 0 ? " (" + string.Join(", ", sw.Select(x => Circled(x.N) + " " + x.Price.ToString("0.00", IC))) + ")" : ""); pk.Tag = "the " + Ord(n) + " swing " + side; return pk; }
            // the N-th swing; when it was already traded through without a hedge (in profit then), the newest swing from the N-th on still standing
            var cand = sw.Skip(n - 1).ToList(); var p = cand.LastOrDefault(x => !x.Used) ?? cand.Last();
            pk.Level = p.Price; pk.Tag = "the " + Ord(p.N) + " swing " + side;
            pk.Why = "🪜 the " + Ord(p.N) + " swing " + side.ToUpperInvariant() + " since the leg: " + p.Price.ToString("0.00", IC) + " (" + p.Time.ToString("HH:mm", IC) + "; counted " + string.Join(", ", sw.Take(p.N).Select(x => Circled(x.N) + " " + x.Price.ToString("0.00", IC))) + ")";
            return pk;
        }
        // broken? TOUCH = the bid at / past the level; CLOSE = the last finished candle closed past it
        public static bool Broken(int dir, double level, double bid, double lastClose, string mode)
        {
            if (double.IsNaN(level)) return false;
            if (mode == "CLOSE") return !double.IsNaN(lastClose) && (dir > 0 ? lastClose < level : lastClose > level);
            return !double.IsNaN(bid) && (dir > 0 ? bid <= level : bid >= level);
        }
        // why a broken level does NOT hedge yet ("" = hedge): in profit, the cool-down after a hedge, waiting for a closed candle
        public static string Gate(double total, bool capped, string mode, bool closedBeyond, double minutesSinceHedge, int cool)
        {
            if (total >= 0) return "🪜 past the line but the basket is not in drawdown (" + (total >= 0 ? "+" : "−") + "$" + Math.Abs(total).ToString("0.00", IC) + ") → no hedge while in profit";
            if (capped) return "";   // 🛡 the max distance is the safety: never delayed
            if (cool > 0 && minutesSinceHedge >= 0 && minutesSinceHedge < cool) return "🪜 cool-down: the last hedge was " + minutesSinceHedge.ToString("0.0", IC) + " min ago (< " + cool + " min) — no flip in a chop";
            if (mode == "CLOSE" && !closedBeyond) return "🪜 waiting for a CLOSED candle past the line";
            return "";
        }
    }
}
