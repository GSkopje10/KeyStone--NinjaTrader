// SpasiX — ⇅ HEDGE LAB: the "hedge instead of a stop" strategy tested on 1-minute candles. RESEARCH ONLY: it trades a simulated hedging
// account in memory and never sends an order to MT5.
// One CYCLE: open BaseLot in a direction; the whole basket closes at +Target $ (commission, spread and swap included) and the same direction
// opens again at once (ReEnter). There is no stop: when the price breaks the low (a buy was last) / the high (a sell was last) of the last
// Lookback candles, the other side opens with HedgeLot (0.1 buy → 0.2 sell), and every new break the other way opens another HedgeLot
// (× Multiplier each time) the other way, until the basket reaches the target. Optional brakes: MaxLegs / BasketStop close the basket at a loss.
// With no cycle open, the first entry is the break of the last Lookback candles' high (BUY) or low (SELL). DailyTarget: once the day's banked
// profit reaches it, no new cycle opens until the next server day.
// Inside a candle the price walks open → low → high → close (a rising candle) or open → high → low → close (a falling one), like the BACKTEST;
// the candles are BID, a buy fills at the ask (bid + spread). Gaps (a candle opening past a level) fill at the open. Margin like MT5's hedging
// account (covered lots at HedgedRatio), stop-out at StopOutPct: the run ends there (BLOWN).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class HedgeSettings
    {
        public string Name = "";
        public double Balance = 350, BaseLot = 0.1, HedgeLot = 0.2, Multiplier = 1, Target = 20, DailyTarget = 0;
        public int Lookback = 10;              // candles for the recent high / low (1-minute)
        public double MinDist = 0;             // the hedge level at least this far (price) from the fill
        public int MaxLegs = 0;                // 0 = no limit; else the basket closes at a loss instead of opening leg MaxLegs + 1
        public double BasketStop = 0;          // 0 = none; else the basket closes at −BasketStop $
        public bool ReEnter = true;            // after a win: the same direction again at once (false = wait for the next break)
        public double Contract = 100, Point = 0.01, CommissionPerLot = 7;   // Contract = $ per 1.0 of price per lot; commission = round turn per lot (half in, half out)
        public double MarginPerLot = 0, MarginRefPrice = 0;                 // MT5's own margin of 1 lot at a price (0 = contract × price ÷ leverage)
        public int FixedSpreadPts = -1, SlipPts = 0;                        // −1 = the candles' own spread
        public int Leverage = 500; public double HedgedRatio = 0.5, StopOutPct = 50;
        public double SwapLong = 0, SwapShort = 0; public DayOfWeek Swap3Day = DayOfWeek.Wednesday;   // $ per lot per night
        public double Zone = 0;                // > 0: the hedge line is this far ($ of price) from the leg's fill instead of the recent high / low
        public bool NoHedge;                   // no hedge at all: only the target and the BasketStop (a plain trade)
        public bool OneTargetADay;             // after the first basket closed at the target, done for the day
        public int MaxTriesADay = 0;           // 0 = no limit; else no new basket once this many closed today (win or loss)
        public int SessionFrom = -1, SessionTo = -1, NyOffsetHours = -7;   // new baskets only inside this New York window (minutes of the day; −1 = all day)
        public bool FlattenAtSessionEnd;       // the basket still open when the window ends closes at the market
        public bool Trace;                     // keep every open / close in HedgeResult.Log
        public HedgeSettings Copy() { return (HedgeSettings)MemberwiseClone(); }
    }

    public sealed class HedgeLeg { public int Dir; public double Lots, Price; public DateTime Time; }
    public sealed class HedgeCycle { public DateTime Open, Close; public int Dir, Legs; public double Profit, Worst, MaxGross; public string Reason = ""; }
    public sealed class HedgeDay { public DateTime Day; public double StartEquity, EndEquity, Banked, LowEquity; public int Cycles; public bool HitTarget, Won; }

    public sealed class HedgeResult
    {
        public HedgeSettings Set; public DateTime From, To, BlownAt; public bool Blown;
        public double EndBalance, EndEquity, MaxDd, MaxDdPct, MinMarginLevel = double.MaxValue, MaxGross, Commission, Swap, OpenAtEnd;
        public int Legs, MaxLegs, Rejected;
        public readonly List<HedgeCycle> Cycles = new List<HedgeCycle>();
        public readonly List<HedgeDay> Days = new List<HedgeDay>();
        public readonly List<string> Log = new List<string>();
        public readonly List<Tuple<DateTime, double>> Curve = new List<Tuple<DateTime, double>>();   // the equity every 15 minutes (and at the end)
        public int Wins { get { return Cycles.Count(c => c.Reason == "TARGET"); } }
        public int Losses { get { return Cycles.Count(c => c.Reason != "TARGET"); } }
        public double Net { get { return EndEquity - Set.Balance; } }
        public int TargetDays { get { return Days.Count(d => d.HitTarget); } }
    }

    public static class HedgeLab
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // bars: 1-minute BID candles (server time), sorted; the ones before `from` only warm up the recent high / low
        public static HedgeResult Run(IList<Mt5Bar> bars, HedgeSettings s, DateTime from, DateTime to)
        {
            var r = new HedgeResult { Set = s, From = from, To = to }; var e = new Engine(s, r);
            int first = 0; while (first < bars.Count && bars[first].Time < from) first++;
            for (int i = first; i < bars.Count && !r.Blown; i++) { if (bars[i].Time >= to.Date.AddDays(1)) break; e.Bar(bars, i); }
            e.Finish(); return r;
        }

        sealed class Engine
        {
            readonly HedgeSettings s; readonly HedgeResult r; readonly List<HedgeLeg> legs = new List<HedgeLeg>();
            double balance, swapAcc, commAcc, peak, sp, lastBid; int lastDir; double hedgeLevel = double.NaN; HedgeCycle cyc; HedgeDay day; DateTime now;
            IList<Mt5Bar> bars; int bi;
            public Engine(HedgeSettings s, HedgeResult r) { this.s = s; this.r = r; balance = peak = s.Balance; }

            double Lots(int dir) { double x = 0; foreach (var l in legs) if (l.Dir == dir) x += l.Lots; return x; }
            double Gross { get { double x = 0; foreach (var l in legs) x += l.Lots; return x; } }
            // the basket's P&L if closed at this bid: longs at the bid, shorts at the ask; the closing commission, open commission and swap included
            double CyclePl(double b) { double pl = 0; foreach (var l in legs) pl += l.Dir > 0 ? (b - l.Price) * l.Lots * s.Contract : (l.Price - (b + sp)) * l.Lots * s.Contract; return pl - Gross * s.CommissionPerLot / 2 + commAcc + swapAcc; }
            double Equity(double b) { return legs.Count == 0 ? balance : balance + CyclePl(b); }   // the closing commission counted already (worst case)
            double Margin(double b) { double per = s.MarginPerLot > 0 && s.MarginRefPrice > 0 ? s.MarginPerLot * b / s.MarginRefPrice : s.Contract * b / Math.Max(1, s.Leverage), bl = Lots(1), sl = Lots(-1); return Math.Abs(bl - sl) * per + 2 * Math.Min(bl, sl) * per * s.HedgedRatio; }
            // the price where the basket's P&L reaches `value` (A·bid + B = value), NaN when it no longer moves with the price
            double LevelFor(double value, out int side)
            {
                double a = (Lots(1) - Lots(-1)) * s.Contract, b0 = CyclePl(0); side = Math.Sign(a);
                if (Math.Abs(a) < 1e-9) return double.NaN; return (value - b0) / a;
            }
            double RecentHigh(int i) { double h = double.MinValue; for (int k = Math.Max(0, i - s.Lookback); k < i; k++) h = Math.Max(h, bars[k].H); return h == double.MinValue ? double.NaN : h; }
            double RecentLow(int i) { double l = double.MaxValue; for (int k = Math.Max(0, i - s.Lookback); k < i; k++) l = Math.Min(l, bars[k].L); return l == double.MaxValue ? double.NaN : l; }
            bool CanTrade { get { return !(s.DailyTarget > 0 && day != null && day.Banked >= s.DailyTarget) && !(s.OneTargetADay && day != null && day.Won) && !(s.MaxTriesADay > 0 && day != null && day.Cycles >= s.MaxTriesADay) && InSession(now); } }
            bool HasSession { get { return s.SessionFrom >= 0 && s.SessionTo >= 0 && s.SessionFrom != s.SessionTo; } }
            bool InSession(DateTime t)
            {
                if (!HasSession) return true; var ny = t.AddHours(s.NyOffsetHours); int m = ny.Hour * 60 + ny.Minute;
                return s.SessionFrom < s.SessionTo ? m >= s.SessionFrom && m < s.SessionTo : m >= s.SessionFrom || m < s.SessionTo;
            }

            public void Bar(IList<Mt5Bar> all, int i)
            {
                bars = all; bi = i; var b = all[i]; now = b.Time; sp = (s.FixedSpreadPts >= 0 ? s.FixedSpreadPts : b.Spread) * s.Point;
                NewDay(b.Time.Date, b.O);
                if (s.FlattenAtSessionEnd && HasSession && legs.Count > 0 && !InSession(now)) CloseAll(b.O, "SESSION END");
                // the gap from the last close to this open: nothing trades in between, whatever is past a level fills at the open
                Walk(b.O, b.O);
                if (b.C >= b.O) { Walk(b.O, b.L); Walk(b.L, b.H); Walk(b.H, b.C); } else { Walk(b.O, b.H); Walk(b.H, b.L); Walk(b.L, b.C); }
                lastBid = b.C; if (day != null) day.EndEquity = Equity(b.C);
                if (b.Time.Minute % 15 == 0 || r.Blown) r.Curve.Add(Tuple.Create(b.Time, r.Blown ? balance : Equity(b.C)));
            }

            void NewDay(DateTime d, double open)
            {
                if (day != null && day.Day == d) return;
                if (day != null)
                {
                    // swap at the server's midnight for every night held (none into Sunday / Monday: the Wednesday night counts ×3)
                    for (var x = day.Day.AddDays(1); x <= d; x = x.AddDays(1))
                    {
                        var prev = x.AddDays(-1); if (prev.DayOfWeek == DayOfWeek.Saturday || prev.DayOfWeek == DayOfWeek.Sunday || legs.Count == 0) continue;
                        int mult = prev.DayOfWeek == s.Swap3Day ? 3 : 1; double sw = (Lots(1) * s.SwapLong + Lots(-1) * s.SwapShort) * mult; swapAcc += sw; r.Swap += sw;
                    }
                }
                day = new HedgeDay { Day = d, StartEquity = Equity(lastBid > 0 ? lastBid : open) }; day.LowEquity = day.StartEquity; day.EndEquity = day.StartEquity; r.Days.Add(day);
            }

            // the price walks from p0 to p1: the first level crossed on the way fires (at its price), then the rest of the way is checked again
            void Walk(double p0, double p1)
            {
                double cur = p0; int up = p1 > p0 ? 1 : p1 < p0 ? -1 : 0;
                for (int guard = 0; guard < 50 && !r.Blown; guard++)
                {
                    Check(cur); if (r.Blown) return;
                    // candidates: (price, kind); a level already passed at `cur` fires at `cur`
                    double best = double.NaN; string kind = null; int entryDir = 0;
                    Action<double, int, string, int> cand = (lvl, side, k, d) =>
                    {
                        if (double.IsNaN(lvl)) return; double at;
                        if (side > 0 ? cur >= lvl : cur <= lvl) at = cur;            // already there
                        else if (up == side && (side > 0 ? p1 >= lvl : p1 <= lvl)) at = lvl;   // reached on this walk
                        else return;
                        if (double.IsNaN(best) || Math.Abs(at - cur) < Math.Abs(best - cur)) { best = at; kind = k; entryDir = d; }
                    };
                    if (legs.Count > 0)
                    {
                        int ts; double tl = LevelFor(s.Target, out ts); if (ts != 0) cand(tl, ts, "TARGET", 0); else if (CyclePl(cur) >= s.Target) cand(cur, up >= 0 ? 1 : -1, "TARGET", 0);
                        if (s.BasketStop > 0) { int ss; double sl = LevelFor(-s.BasketStop, out ss); if (ss != 0) cand(sl, -ss, "BASKET STOP", 0); }
                        if (!double.IsNaN(hedgeLevel)) cand(hedgeLevel, -lastDir, "HEDGE", -lastDir);
                    }
                    else if (CanTrade && bi >= s.Lookback) { cand(RecentHigh(bi), 1, "ENTRY", 1); cand(RecentLow(bi), -1, "ENTRY", -1); }
                    if (kind == null) { Check(p1); return; }
                    cur = best; Fire(kind, entryDir, cur);
                }
            }

            void Check(double b)
            {
                if (legs.Count == 0) return; double eq = Equity(b), m = Margin(b);
                if (day != null) day.LowEquity = Math.Min(day.LowEquity, eq);
                peak = Math.Max(peak, eq); double dd = peak - eq; if (dd > r.MaxDd) { r.MaxDd = dd; r.MaxDdPct = peak > 0 ? 100 * dd / peak : 0; }
                if (m > 0) { double lvl = 100 * eq / m; r.MinMarginLevel = Math.Min(r.MinMarginLevel, lvl); if (lvl < s.StopOutPct || eq <= 0) { CloseAll(b, "STOP-OUT"); r.Blown = true; r.BlownAt = now; } }
            }

            void Fire(string kind, int dir, double b)
            {
                if (kind == "TARGET") { int d = Math.Sign(Lots(1) - Lots(-1)); if (d == 0) d = lastDir; CloseAll(b, "TARGET"); if (s.ReEnter && CanTrade) Open(d, s.BaseLot, b, true); return; }   // (one target a day: CanTrade is false now)
                if (kind == "BASKET STOP") { CloseAll(b, "BASKET STOP"); return; }
                if (kind == "ENTRY") { Open(dir, s.BaseLot, b, true); return; }
                // HEDGE: the next leg the other way (HedgeLot, then × Multiplier for every leg after it)
                if (s.MaxLegs > 0 && legs.Count >= s.MaxLegs) { CloseAll(b, "MAX LEGS"); return; }
                int hedges = legs.Count - 1; double lots = Math.Round(s.HedgeLot * Math.Pow(s.Multiplier, hedges), 2);
                Open(dir, lots, b, false);
            }

            void Open(int dir, double lots, double b, bool newCycle)
            {
                double slip = s.SlipPts * s.Point, fill = dir > 0 ? b + sp + slip : b - slip;
                // MT5 refuses an order the free margin cannot carry ("no money")
                legs.Add(new HedgeLeg { Dir = dir, Lots = lots, Price = fill, Time = now });
                if (Equity(b) - Margin(b) < 0 && legs.Count > 1) { legs.RemoveAt(legs.Count - 1); r.Rejected++; hedgeLevel = double.NaN; return; }
                if (legs.Count == 1) { cyc = new HedgeCycle { Open = now, Dir = dir }; commAcc = 0; swapAcc = 0; }
                double c = lots * s.CommissionPerLot / 2; commAcc -= c; r.Commission += c; r.Legs++;
                if (s.Trace) r.Log.Add(now.ToString("yyyy-MM-dd HH:mm", IC) + "  " + (dir > 0 ? "BUY " : "SELL") + " " + lots.ToString("0.00", IC) + " @ " + fill.ToString("0.00", IC) + "   basket " + Lots(1).ToString("0.00", IC) + " buy / " + Lots(-1).ToString("0.00", IC) + " sell");
                lastDir = dir; cyc.Legs = legs.Count; cyc.MaxGross = Math.Max(cyc.MaxGross, Gross); r.MaxGross = Math.Max(r.MaxGross, Gross); r.MaxLegs = Math.Max(r.MaxLegs, legs.Count);
                // the hedge level: the recent low under a buy / high over a sell, at least MinDist (and the spread) away
                double gap = Math.Max(s.MinDist, sp);
                hedgeLevel = s.Zone > 0 ? (dir > 0 ? b - Math.Max(s.Zone, sp) : b + Math.Max(s.Zone, sp)) : dir > 0 ? Math.Min(RecentLow(bi), b - gap) : Math.Max(RecentHigh(bi), b + gap);
                if (double.IsNaN(hedgeLevel)) hedgeLevel = dir > 0 ? b - gap : b + gap;
                if (s.NoHedge) hedgeLevel = double.NaN;
                if (s.Trace) r.Log.Add("                  next hedge if the bid " + (dir > 0 ? "≤ " : "≥ ") + hedgeLevel.ToString("0.00", IC) + ",  equity " + Equity(b).ToString("0.00", IC) + ",  margin " + Margin(b).ToString("0.00", IC));
            }

            void CloseAll(double b, string reason)
            {
                if (legs.Count == 0) return; double pl = CyclePl(b);
                r.Commission += Gross * s.CommissionPerLot / 2; balance += pl;
                if (s.Trace) r.Log.Add(now.ToString("yyyy-MM-dd HH:mm", IC) + "  CLOSE ALL (" + reason + ") at bid " + b.ToString("0.00", IC) + "  basket " + pl.ToString("+0.00;-0.00", IC) + "  balance " + (balance).ToString("0.00", IC));
                cyc.Close = now; cyc.Profit = pl; cyc.Reason = reason; r.Cycles.Add(cyc);
                if (day != null) { day.Banked += pl; day.Cycles++; if (s.DailyTarget > 0 && day.Banked >= s.DailyTarget) day.HitTarget = true; if (reason == "TARGET") { day.Won = true; if (s.OneTargetADay) day.HitTarget = true; } }
                legs.Clear(); commAcc = 0; swapAcc = 0; hedgeLevel = double.NaN; cyc = null;
            }

            public void Finish()
            {
                r.EndBalance = balance; r.EndEquity = legs.Count > 0 ? Equity(lastBid) : balance; r.OpenAtEnd = legs.Count > 0 ? CyclePl(lastBid) : 0;
                if (r.MinMarginLevel == double.MaxValue) r.MinMarginLevel = 0;
            }
        }

        // the settings from MT5's symbol (saved with the BACKTEST's history): $ per price move, MT5's margin, hedged margin, swap, commission
        public static void FromSpec(HedgeSettings s, SimSpec sp, double price)
        {
            if (sp == null) return; s.Point = sp.Point > 0 ? sp.Point : s.Point;
            if (sp.TickSize > 0 && sp.TickValue > 0) s.Contract = sp.TickValue / sp.TickSize;
            if (sp.MarginPerLot > 0 && sp.MarginRefPrice > 0 && (sp.MarginLeverage <= 0 || sp.MarginLeverage == s.Leverage)) { s.MarginPerLot = sp.MarginPerLot; s.MarginRefPrice = sp.MarginRefPrice; } else { s.MarginPerLot = 0; s.MarginRefPrice = 0; }
            if (sp.HedgedRatio >= 0) s.HedgedRatio = sp.HedgedRatio; if (sp.MarginUseLeg) s.HedgedRatio = 0.5;   // "larger leg": the covered pair costs one side = half each
            s.Swap3Day = sp.Swap3Day; s.SwapLong = SwapPerLot(sp, 1, price); s.SwapShort = SwapPerLot(sp, -1, price);
        }
        // MT5's swap of 1 lot for one night in money (the same modes as the BACKTEST's account)
        public static double SwapPerLot(SimSpec s, int dir, double price)
        {
            double v = dir > 0 ? s.SwapLong : s.SwapShort; double pts = s.TickSize > 0 ? s.TickValue * (s.Point / s.TickSize) : 0;
            switch (s.SwapMode) { case 1: case 7: case 8: return v * pts; case 2: case 3: case 4: return v; case 5: case 6: return price * s.Contract * v / 100.0 / 360.0; }
            return 0;
        }

        // the worst-case summary line used by the study and the window
        public static string Line(HedgeResult r)
        {
            return string.Format(IC, "{0,-34} net {1,9:N0}  end {2,9:N0}  {3,-22} cycles {4,5}  lost {5,3}  maxDD {6,8:N0} ({7,5:0}%)  legs≤{8,3}  gross≤{9,5:0.00}  days@target {10,3}/{11,-3}",
                r.Set.Name, r.Net, r.EndEquity, r.Blown ? "BLOWN " + r.BlownAt.ToString("dd MMM yy HH:mm", IC) : "survived", r.Cycles.Count, r.Losses, r.MaxDd, r.MaxDdPct, r.MaxLegs, r.MaxGross, r.TargetDays, r.Days.Count);
        }
    }
}
