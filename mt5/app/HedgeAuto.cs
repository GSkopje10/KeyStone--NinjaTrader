// SpasiX — 🤖 AUTO: the hedge AUTOMATION (the user's strategy), the same code on the BACKTEST, the DEMO account and MT5 (demo or real).
// One ITERATION: open the ENTRY lots (BUY / SELL typed by you for the first one, or the automation's: the break of the recent high = BUY,
// the recent low = SELL). No stop: when the price breaks the recent LOW (the last leg was a buy) / HIGH (the last leg was a sell) of the last
// Lookback finished candles of the chosen timeframe, the ADD-ON lots open the other way (0.5 buy → 1.0 sell → 1.0 buy …), until the whole
// basket (every position on the symbol: profit + swap + commission) reaches +TARGET $ → everything closes. MAX DRAWDOWN $ (0 = none, until
// the account blows) closes the basket at −$X, then STOP or the NEXT iteration. The next iteration: after a PAUSE (seconds of the market's clock,
// then the last basket's direction at once) or at the very next BREAK of the recent high / low.
// Orders go through the caller's Send (the window's ARM / Algo Trading / PROP checks), one at a time: the next decision waits for MT5's answer
// and for the position to show up, so a slow bridge never doubles an order. The automation owns EVERY position on its symbol.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class AutoSettings
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public string Symbol = "XAUUSD"; public int Tf = 1, Lookback = 1;   // the recent high / low = the last Lookback FINISHED candles of Tf minutes
        public double EntryLots = 0.1, AddLots = 0.2, Target = 20, MaxDrawdown = 0, CommissionPerLot = 0;   // MaxDrawdown 0 = none; CommissionPerLot = round turn, taken off the basket when MT5 does not show it
        public string FirstDir = "AUTO";       // AUTO = the first break; BUY / SELL = at once
        public string NextStart = "BREAK";     // BREAK = the very next break of the recent high / low; PAUSE = PauseSec, then the last basket's direction at once
        public int PauseSec = 60; public string AfterDrawdown = "STOP";   // STOP / NEXT
        public bool Rolling = true;            // BREAK: the add-on level follows each new candle in real time — the newest swing low / high (user's request, the default); off = fixed when the leg opens
        public string HedgeMode = "BREAK";     // when the add-on opens: BREAK = the recent low / high; PRICE = HedgePrice of price against the last leg; MONEY = −HedgeMoney $ since the last leg
        public double HedgePrice = 5, HedgeMoney = 100;
        public int MaxIterations = 0;          // 0 = no limit
        public string EntryMode = "BREAK";     // the entry setup: BREAK (recent high / low) • FVG (the retest of a fair value gap) • VWAP (a cross of the day's VWAP) • FLOW (a tick-volume surge candle, broken)
        public double FlowVol = 1.5, FlowShare = 0.7;   // FLOW: the candle's tick volume ≥ FlowVol × the last 20, buyers (est.) ≥ FlowShare (sellers for a SELL)
        public int PerDay = 0;                 // iterations a server day (0 = no limit)
        public string Windows = "";            // New York times new iterations may start, e.g. "08:30-11:00, 13:00-16:00" ("" = all the time)
        public int NyOffsetHours = -7;         // New York = the MT5 server's clock + this (IC Markets: −7)
        public string NewsMode = "IGNORE";     // IGNORE • AVOID (no new iteration NewsBefore … NewsAfter minutes around high-impact news) • ONLY (only then)
        public int NewsBefore = 15, NewsAfter = 15;
        public bool Swing = true; public int SwingK = 2;   // the levels: the most recent SWING high / low (a candle higher / lower than SwingK candles on each side); off = the last Lookback candles
        public int FvgTf = 5;                  // the FVG setups' own timeframe (5 = M5, like the futures interface)
        public double WinnerLotsSell = 0.1;    // ADD TO THE WINNER: WinnerLots on a BUY side, WinnerLotsSell on a SELL side
        public bool AddToWinner; public double WinnerLots = 0.1; public int MaxWinnerAdds = 0;   // ADD TO THE WINNER: on each new signal of the setup in the leading side's direction (0 = no limit)
        public string Preset = "";             // A = London engulfing, B = NY FVG 50%, BOX = consolidation flip, "" = your own
        public string VwapFilter = "NONE";     // NONE • DISCOUNT (buy under the day's VWAP, sell over it) • FLOW (buy over it, sell under it)
        // ANTI-CHOP (user's request, each a checkbox): RangeOnly = a hedge only BEYOND the basket's range (its highest / lowest leg), never inside it;
        // LockOn = after LockAfter hedges inside LockRange $ of price, LOCK the basket (buys = sells, net 0) and wait for the break of that range,
        // then UNLOCK: LEAD = add NetAdvantage lots the break's way, LIFT = close the side against the break
        public int BeAfter;   // RESCUE EXIT: a basket with this many hedges (or more) closes as soon as it is back at breakeven ($0 after every cost) instead of waiting for the target — no endless baskets (0 = off)
        public bool RangeOnly, LockOn; public int LockAfter = 2; public double LockRange = 3; public string Unlock = "LEAD";
        // ⚡ QUICK HEDGE (user's request): once the price ran QuickAfter beyond the basket's best leg, the hedge trails the LAST FINISHED CANDLE's
        // low (long basket) / high (short) — the first candle that breaks it hedges at once (near the top, not back at the old swing)
        public bool QuickHedge; public double QuickAfter = 3;
        // 🕯 HEDGE AT THE ENTRY CANDLE (user's request: "it should have hedged on that bullish candle's low wick, not wait for the swing far below"):
        // the hedge sits just beyond the low (buy) / high (sell) of the candle the newest leg filled in — the candle before it while that one is forming
        public bool CandleHedge;
        // 🧠 SMART (user's request: "its own intuition from the previous data"): before every new basket it looks at how the baskets that started in
        // the SAME conditions (session, hour, VWAP side, order flow, FVG, trend) ended — when those mostly got stuck (≥ 3 hedges, a loss, or deep)
        // far more often than the rest, it skips the entry. Only with enough history (SmartMin baskets in that condition, 20 in all).
        public bool Smart; public int SmartMin = 10;
        // 🏆 ONLY THE BEST SETUPS (with SMART, once 100+ baskets are learned): a new basket only where at least one of its conditions gave CLEAN
        // baskets clearly more often than usual (+15 points) — the algo targets what worked best
        public bool SmartBest;
        // 🔐 PROTECT (user's request): a basket that reached ProtectAt % of its target and falls back to ProtectKeep % of it closes there — a near
        // miss of the take profit never turns into a long hedge (0 = off)
        public double ProtectAt = 0, ProtectKeep = 10;
        // 🛡 RISK GUARD (user's request): your setup unchanged while things look normal; when a basket turns dangerous it stops the snowball —
        // TOO BIG (the next hedge would pass GuardLots per $5,000 of balance), MARGIN (margin level under GuardMargin %), CHOP (GuardChop hedges in
        // the last hour) → instead of the next hedge it LOCKS (buys = sells, frozen) and waits for the real break; a basket that is still too big
        // when it unlocks LIFTS (closes the side against the break) instead of adding more. The STEP LEAD never grows past GuardMaxLead × the lead,
        // the hedge distance widens with the market (≥ 1.5 × the average candle) — and at an emergency margin (≤ GuardMargin / 2) it locks at once
        public string GuardMode = "LOCK"; public bool RiskGuard, GuardLift;   // GuardMode: LOCK = freeze + trim at the break; HOLD = no more hedges at the cap (the small lead rides)
        public double GuardLots = 1.0, GuardMargin = 500; public int GuardChop = 3, GuardMaxLead = 3;
        // 🛟 DANGER RECOVERY (with the guard): a locked basket waits for an OBVIOUS trend — the break of its range by GuardBreak × the average candle
        // AND the EMA 50 / 200 the same way — then PAIRS OFF (the best winning leg + the worst losing leg closed together when they add up to ≥ $0,
        // the basket shrinks for free) before it leans with the trend. HARD STOPS (user's): the floating loss ≥ HardFloat % of the balance, or the
        // margin level ≤ HardMargin % → CLOSE EVERYTHING ("it is what it is"); 0 = off
        public double GuardBreak = 1.0, HardFloat = 60, HardMargin = 60; public bool PairOff = true;
        // 🚨 DANGER EXIT (user's request: detect danger → smaller profits): once the live DANGER meter (loss vs balance, margin level, chop,
        // time in the trade, how similar baskets of the history ended) reaches DangerAt, the basket closes at DangerKeep % of the target
        public bool DangerExit; public double DangerAt = 60, DangerKeep = 25;
        // ⛔ LOSS LIMITS (user's request): MaxDdPct = the iteration closes at −X % of its starting balance (open + realised), then the next trade as
        // AFTER MAX DRAWDOWN says • DayLoss $ / DayLossPct % = today's (New York day) closed + open loss reaches it → close, NO new iteration until tomorrow
        public double MaxDdPct = 0, DayLoss = 0, DayLossPct = 0;
        // 🔄 RECOVERED EXIT (user's request: "it came back from −$200 to −$3 and fell again — protect it, keep the drawdown low"): a basket that
        // went at least RecDeep × the target deep and came back to within RecKeep % of the target from breakeven closes there (a tiny loss or
        // better) — the account is free for the next setup instead of riding the same basket down again
        public bool RecoverExit; public double RecDeep = 2, RecKeep = 20;
        // 🔐 BREAKEVEN LOCK (user's request: "put the hedge at breakeven once the profit is at 80 %"): once the basket reached BeLockAt % of the
        // target, an EQUAL hedge waits at the price where the basket would be back at BeLockKeep % of the target — if the price turns there it
        // LOCKS (buys = sells, net 0: the small profit kept) and waits for the next break (the lock's own unlock), never a win turned into a drawdown
        public bool BeLock; public double BeLockAt = 80, BeLockKeep = 10;
        // 🧭 TREND-AWARE HEDGE (user's request): the trend (EMA 50 / 200) agrees with the basket's net side → a hedge against it only beyond the basket's
        // lowest / highest leg AND at least TrendHedgeMult × the normal distance — gold's deeper pullbacks in a trend never trigger it
        public bool TrendHedge; public double TrendHedgeMult = 2;
        // 📉 SCALE OUT (user's request): while the basket holds more than its lead, every ScaleStep of price in its favour closes ScaleLots of the
        // most profitable leg on the winning side — smaller as it wins, the slices banked (they count for the target)
        public bool ScaleOut; public double ScaleLots = 0.05, ScaleStep = 1.5;   // GuardLift: still too big at the break → close the side against it (realises a loss; the replay bled with it — off by default)
        // 🛡 MAX HEDGE DISTANCE (user's request): the hedge is NEVER further than this (price) from the losing side — from the newest leg of
        // the side that loses (from the basket's own high / low with range-only); no swing (a straight trend) → it sits there; 0 = off
        public double HedgeCap = 5;
        // 🪜 HM LADDER (draft from the Hedging Mastermind day 1, DEMO / BACKTEST only): EntryMode STRADDLE = a buy stop + a sell stop LadderStep/2
        // around the price; HedgeMode LADDER = a new BUY only above the basket's HIGHEST leg + LadderStep, a new SELL only below its LOWEST leg −
        // LadderStep, every leg ENTRY LOTS, at most LadderMax legs (the backtest without a cap blew every account); the basket closes at the target
        public double LadderStep = 2.5; public int LadderMax = 10;
        // ✅ TREND CONFIRMATION (user's request): EMA = a new basket only in the trend's direction (BUY: EMA 50 over EMA 200 and the close over the
        // EMA 200 of the strategy's timeframe; SELL the mirror); no trend = no entry. LADDER: a leg against the trend only when it REDUCES the net (a hedge)
        public string TrendFilter = "NONE";
        public string EodLoss = "HOLD";       // at the close time with the basket in a LOSS: HOLD = stays open and managed to its target (hedging never closes at a loss — the user's rule, the default); CLOSE = close anyway
        public string CloseAtNy = "";          // "16:55" = what is open closes at that New York time, no new iteration after it ("" = hold until the target)
        public bool TrackManual = true;        // a trade you open by hand on the symbol becomes an iteration the automation manages and records (✋)
        public int FvgAge = 0;                 // FVG: a gap is only good this many minutes after it formed (0 = 60 candles)
        public bool FvgStrict; public bool FvgStack = true;   // FvgStack: gaps formed one after another the same way = ONE zone (the 50% of all of them)
        public int AggRun = 4;
        public string HedgeSize = "ADVANTAGE"; public double RecoverMove = 3, HedgeMaxLots = 1, NetAdvantage = 0.2;   // ADVANTAGE (the default): every hedge = the net open the other way + NetAdvantage, so after it the net is always NetAdvantage lots the hedge's way   // RECOVER: each hedge sized so a move of RecoverMove its way takes the WHOLE basket to the target (capped at HedgeMaxLots)                 // AGGRESSION BREAK: at least this many candles of one colour before the first opposite close
        public double HedgeMult = 2; public int MaxHedges = 1;   // SIGNAL hedge: the hedge = HedgeMult × the net lots the other way, at most MaxHedges an iteration (HedgeMoney = how much the newest leg must be losing)
        public string ExitMode = "BASKET";     // BASKET = everything closes at +Target; LEGS = each position closes at its own +Target × its lots / the entry lots                 // FVG: the middle candle's body must be bigger than the average range of the last 20 candles (the research's gap)
        public AutoSettings Copy() { return (AutoSettings)MemberwiseClone(); }
        // the researched models (docs/research/hedge/scalp20.html, report.html)
        public static AutoSettings Model(string name, AutoSettings b) { return Model(name, b, 5000); }
        public static AutoSettings Model(string name, AutoSettings b, double bal)
        {
            var s = b.Copy(); s.Preset = name; s.ExitMode = "BASKET"; s.FirstDir = "AUTO"; s.NextStart = "BREAK"; s.AddToWinner = false; s.PerDay = 0; s.MaxIterations = 0; s.NewsMode = "IGNORE"; s.FvgAge = 0; s.FvgStrict = false;
            // ★ SAFE HEDGE (the replay of 5 months of 1-minute gold, user's own break strategy): range-only hedges + the rescue exit after 3
            // hedges, small size for the balance (0.01 lot and +$2 per $5,000) so the deepest basket stays survivable; never a loss close
            if (name == "PRO") { s.Tf = 1; s.EntryMode = "BREAK"; s.Swing = true; s.SwingK = 2; s.VwapFilter = "NONE"; s.HedgeMode = "BREAK"; s.Rolling = true; s.HedgeSize = "STEPLEAD"; s.EntryLots = 0.02; s.NetAdvantage = 0.02; s.AddLots = 0.02; s.Target = 10; s.ScaleOut = true; s.ScaleLots = 0.01; s.ScaleStep = 1.5; s.MaxDrawdown = 0; s.AfterDrawdown = "NEXT"; s.Windows = ""; s.CloseAtNy = ""; s.EodLoss = "HOLD"; s.RangeOnly = true; s.BeAfter = 3; s.QuickHedge = true; s.QuickAfter = 3; s.HedgeCap = 3; s.LockOn = false; s.TrendFilter = "NONE"; s.RiskGuard = true; s.GuardMode = "LOCK"; s.GuardLots = 1.0; s.GuardMargin = 500; s.GuardChop = 3; s.GuardMaxLead = 3; s.GuardBreak = 1.0; s.PairOff = true; s.HardFloat = 60; s.HardMargin = 60; s.ProtectAt = 0; s.TrendHedge = true; s.TrendHedgeMult = 2; }
            if (name == "LADDER") { s.Tf = 1; s.EntryMode = "STRADDLE"; s.HedgeMode = "LADDER"; s.HedgeSize = "FIXED"; s.LadderStep = 4; s.LadderMax = 10; s.EntryLots = 0.05; s.AddLots = 0.05; s.Target = 17; s.TrendFilter = "EMA"; s.BeAfter = 6; s.MaxDrawdown = 0; s.VwapFilter = "NONE"; s.Windows = ""; s.CloseAtNy = ""; s.EodLoss = "HOLD"; s.RangeOnly = false; s.QuickHedge = false; s.LockOn = false; s.BeAfter = 0; s.Rolling = false; }
            if (name == "DEFENDER")
            {   // 🛡 DEFENDER (the default, user's request: capital first, small profits, a calm hedging flow): the SAFE break hedge (a FIXED lead,
                // never a growing step) at 0.01 lot per $1,000 + the RISK GUARD (LOCK instead of a hedge when too big / low margin / chop, the
                // size cap 0.6 lot per $5,000, unlock only on an obvious trend, pair-off) + scale out + the rescue after 3 hedges + the 🚨 DANGER EXIT
                double k = Math.Max(0.01, Math.Round(Math.Max(1000, bal) / 1000 * 0.01, 2));
                s.Tf = 1; s.EntryMode = "BREAK"; s.Swing = true; s.SwingK = 2; s.VwapFilter = "NONE"; s.HedgeMode = "BREAK"; s.Rolling = true; s.HedgeSize = "ADVANTAGE"; s.EntryLots = k; s.NetAdvantage = k; s.AddLots = Math.Round(k * 2, 2); s.Target = Math.Round(k * 200, 2); s.MaxDrawdown = 0; s.AfterDrawdown = "NEXT"; s.Windows = ""; s.CloseAtNy = ""; s.EodLoss = "HOLD"; s.RangeOnly = true; s.BeAfter = 3; s.LockOn = false; s.TrendFilter = "NONE"; s.QuickHedge = false; s.HedgeCap = 5;
                s.RiskGuard = true; s.GuardMode = "LOCK"; s.GuardLots = 0.6; s.GuardMargin = 500; s.GuardChop = 3; s.GuardMaxLead = 3; s.GuardBreak = 1.0; s.GuardLift = false; s.PairOff = true; s.ScaleOut = true; s.ScaleLots = 0.01; s.ScaleStep = 1.5; s.TrendHedge = false; s.ProtectAt = 0;
                s.HardFloat = 60; s.HardMargin = 60; s.DangerExit = true; s.DangerAt = 60; s.DangerKeep = 25; s.Smart = false; s.RecoverExit = false; s.RecDeep = 2; s.RecKeep = 20;   // 🔄 off: the replay with it was far worse (closing at a small loss led the next baskets into the hard stop)   // 🧠 SMART off by default: the 5-month replay was lower with it (too little history to learn from) — tick it to learn on DEMO
            }
            if (name == "SAFE") { s.Tf = 1; s.EntryMode = "BREAK"; s.Swing = true; s.SwingK = 2; s.VwapFilter = "NONE"; s.HedgeMode = "BREAK"; s.Rolling = true; s.HedgeSize = "ADVANTAGE"; s.EntryLots = 0.01; s.NetAdvantage = 0.01; s.AddLots = 0.02; s.Target = 2; s.MaxDrawdown = 0; s.AfterDrawdown = "NEXT"; s.Windows = ""; s.CloseAtNy = ""; s.EodLoss = "HOLD"; s.RangeOnly = true; s.BeAfter = 3; s.LockOn = false; }
            else if (name == "A") { s.Tf = 5; s.EntryMode = "ENGULF"; s.VwapFilter = "DISCOUNT"; s.HedgeMode = "NONE"; s.EntryLots = 0.1; s.AddLots = 0.2; s.Target = 20; s.MaxDrawdown = 300; s.AfterDrawdown = "NEXT"; s.Windows = "03:00-06:00"; s.CloseAtNy = "16:55"; }
            else if (name == "B") { s.Tf = 5; s.FvgTf = 5; s.EntryMode = "FVG50"; s.FvgAge = 12; s.FvgStrict = true; s.VwapFilter = "FLOW"; s.HedgeMode = "NONE"; s.EntryLots = 0.1; s.AddLots = 0.2; s.Target = 20; s.MaxDrawdown = 0; s.AfterDrawdown = "NEXT"; s.Windows = "08:30-11:30"; s.CloseAtNy = ""; }
            else if (name == "AH") { s.Tf = 5; s.EntryMode = "ENGULF"; s.VwapFilter = "DISCOUNT"; s.HedgeMode = "SIGNAL"; s.HedgeMult = 2; s.MaxHedges = 1; s.HedgeMoney = 5; s.ExitMode = "LEGS"; s.EntryLots = 0.1; s.AddLots = 0.2; s.Target = 20; s.MaxDrawdown = 400; s.AfterDrawdown = "NEXT"; s.Windows = "03:00-06:00"; s.CloseAtNy = "16:55"; }
            else if (name == "D") { s.Tf = 1; s.EntryMode = "AGGBRK"; s.AggRun = 4; s.VwapFilter = "FLOW"; s.HedgeMode = "NONE"; s.EntryLots = 0.1; s.AddLots = 0.2; s.Target = 20; s.MaxDrawdown = 300; s.AfterDrawdown = "NEXT"; s.Windows = "03:00-15:30"; s.CloseAtNy = "16:55"; }
            else if (name == "F") { s.Tf = 1; s.FvgTf = 5; s.EntryMode = "FVG50"; s.FvgAge = 0; s.FvgStrict = false; s.VwapFilter = "NONE"; s.HedgeMode = "FVG"; s.Swing = true; s.SwingK = 2; s.Rolling = false; s.EntryLots = 0.1; s.AddLots = 0.2; s.Target = 20; s.MaxDrawdown = 0; s.AfterDrawdown = "NEXT"; s.Windows = ""; s.CloseAtNy = ""; }
            else if (name == "C") { s.Tf = 15; s.Swing = false; s.Lookback = 100; s.EntryMode = "VWAPREJ"; s.VwapFilter = "NONE"; s.HedgeMode = "NONE"; s.EntryLots = 0.1; s.AddLots = 0.2; s.Target = 20; s.MaxDrawdown = 200; s.AfterDrawdown = "NEXT"; s.Windows = "03:00-15:30"; s.CloseAtNy = "16:55"; }
            else if (name == "BOX") { s.HedgeSize = "FIXED"; s.Tf = 60; s.Swing = false; s.Lookback = 96; s.EntryMode = "BREAK"; s.VwapFilter = "NONE"; s.HedgeMode = "BREAK"; s.Rolling = true; s.EntryLots = 0.1; s.AddLots = 0.2; s.Target = 150; s.MaxDrawdown = 0; s.AfterDrawdown = "STOP"; s.Windows = ""; s.CloseAtNy = ""; }
            return s;
        }
        // the New York windows ("08:30-11:00, 13:00-16:00") as minutes of the day; null = all the time; a bad window = an empty list
        public List<Tuple<int, int>> WindowList()
        {
            if (string.IsNullOrWhiteSpace(Windows)) return null; var res = new List<Tuple<int, int>>();
            foreach (var part in Windows.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var ab = part.Trim().Split('-'); TimeSpan a, b; if (ab.Length != 2 || !TimeSpan.TryParse(ab[0].Trim(), IC, out a) || !TimeSpan.TryParse(ab[1].Trim(), IC, out b)) return new List<Tuple<int, int>>();
                res.Add(Tuple.Create((int)a.TotalMinutes, (int)b.TotalMinutes));
            }
            return res;
        }
        public string Save() { return string.Join(";", "sym=" + Symbol, "tf=" + Tf, "look=" + Lookback, "entry=" + EntryLots.ToString(IC), "add=" + AddLots.ToString(IC), "target=" + Target.ToString(IC), "dd=" + MaxDrawdown.ToString(IC), "comm=" + CommissionPerLot.ToString(IC), "first=" + FirstDir, "next=" + NextStart, "pause=" + PauseSec, "afterdd=" + AfterDrawdown, "rolling=" + (Rolling ? 1 : 0), "maxit=" + MaxIterations, "hmode=" + HedgeMode, "hprice=" + HedgePrice.ToString(IC), "hmoney=" + HedgeMoney.ToString(IC), "emode=" + EntryMode, "fvol=" + FlowVol.ToString(IC), "fshare=" + FlowShare.ToString(IC), "perday=" + PerDay, "win=" + Windows.Replace(";", ","), "nyoff=" + NyOffsetHours, "news=" + NewsMode, "nb=" + NewsBefore, "na=" + NewsAfter, "win2=" + (AddToWinner ? 1 : 0), "wlots=" + WinnerLots.ToString(IC), "wmax=" + MaxWinnerAdds, "swing=" + (Swing ? 1 : 0), "swk=" + SwingK, "fvgtf=" + FvgTf, "wlots2=" + WinnerLotsSell.ToString(IC), "preset=" + Preset, "vf=" + VwapFilter, "eod=" + CloseAtNy, "man=" + (TrackManual ? 1 : 0), "fage=" + FvgAge, "fstrict=" + (FvgStrict ? 1 : 0), "hmult=" + HedgeMult.ToString(IC), "maxh=" + MaxHedges, "exit=" + ExitMode, "aggrun=" + AggRun, "fstack=" + (FvgStack ? 1 : 0), "hsize=" + HedgeSize, "hs2=1", "roll2=1", "eodl=" + EodLoss, "ronly=" + (RangeOnly ? 1 : 0), "qh=" + (QuickHedge ? 1 : 0), "chedge=" + (CandleHedge ? 1 : 0), "smart=" + (Smart ? 1 : 0), "sbest=" + (SmartBest ? 1 : 0), "smartn=" + SmartMin, "qafter=" + QuickAfter.ToString(IC), "pat=" + ProtectAt.ToString(IC), "rg=" + (RiskGuard ? 1 : 0), "glots=" + GuardLots.ToString(IC), "gmargin=" + GuardMargin.ToString(IC), "gchop=" + GuardChop, "glead=" + GuardMaxLead, "glift=" + (GuardLift ? 1 : 0), "gmode=" + GuardMode, "gbreak=" + GuardBreak.ToString(IC), "hfloat=" + HardFloat.ToString(IC), "hmargin=" + HardMargin.ToString(IC), "pair=" + (PairOff ? 1 : 0), "thedge=" + (TrendHedge ? 1 : 0), "scale=" + (ScaleOut ? 1 : 0), "slots=" + ScaleLots.ToString(IC), "sstep=" + ScaleStep.ToString(IC), "thmult=" + TrendHedgeMult.ToString(IC), "pkeep=" + ProtectKeep.ToString(IC), "hcap=" + HedgeCap.ToString(IC), "lstep=" + LadderStep.ToString(IC), "lmax=" + LadderMax.ToString(IC), "trend=" + TrendFilter, "beafter=" + BeAfter, "lock=" + (LockOn ? 1 : 0), "lafter=" + LockAfter, "lrange=" + LockRange.ToString(IC), "unlock=" + Unlock, "nadv=" + NetAdvantage.ToString(IC), "rmove=" + RecoverMove.ToString(IC), "hmax=" + HedgeMaxLots.ToString(IC), "dexit=" + (DangerExit ? 1 : 0), "dat=" + DangerAt.ToString(IC), "dkeep=" + DangerKeep.ToString(IC), "ddpct=" + MaxDdPct.ToString(IC), "rexit=" + (RecoverExit ? 1 : 0), "belock=" + (BeLock ? 1 : 0), "belat=" + BeLockAt.ToString(IC), "belkeep=" + BeLockKeep.ToString(IC), "rdeep=" + RecDeep.ToString(IC), "rkeep=" + RecKeep.ToString(IC), "dayloss=" + DayLoss.ToString(IC), "daypct=" + DayLossPct.ToString(IC)); }
        public static AutoSettings Load(string line)
        {
            var s = new AutoSettings(); if (string.IsNullOrEmpty(line)) return s; var d = new Dictionary<string, string>();
            foreach (var kv in line.Split(';')) { int i = kv.IndexOf('='); if (i > 0) d[kv.Substring(0, i)] = kv.Substring(i + 1); }
            Func<string, double, double> n = (k, def) => { string v; double x; return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, IC, out x) ? x : def; };
            Func<string, string, string> t = (k, def) => { string v; return d.TryGetValue(k, out v) && v != "" ? v : def; };
            s.Symbol = t("sym", s.Symbol); s.Tf = (int)n("tf", 1); s.Lookback = (int)n("look", 1); s.EntryLots = n("entry", 0.1); s.AddLots = n("add", 0.2); s.Target = n("target", 20); s.MaxDrawdown = n("dd", 0); s.CommissionPerLot = n("comm", 0);
            s.FirstDir = t("first", "AUTO"); s.NextStart = t("next", "BREAK"); s.PauseSec = (int)n("pause", 60); s.AfterDrawdown = t("afterdd", "STOP"); s.Rolling = !d.ContainsKey("roll2") ? true : n("rolling", 1) == 1;   /* older saved settings (fixed by default then) → FOLLOWS */ s.MaxIterations = (int)n("maxit", 0); s.HedgeMode = t("hmode", "BREAK"); s.HedgePrice = n("hprice", 5); s.HedgeMoney = n("hmoney", 100);
            s.EntryMode = t("emode", "BREAK"); s.FlowVol = n("fvol", 1.5); s.FlowShare = n("fshare", 0.7); s.PerDay = (int)n("perday", 0); s.Windows = t("win", ""); s.NyOffsetHours = (int)n("nyoff", -7); s.NewsMode = t("news", "IGNORE"); s.NewsBefore = (int)n("nb", 15); s.NewsAfter = (int)n("na", 15); s.AddToWinner = n("win2", 0) == 1; s.WinnerLots = n("wlots", 0.1); s.MaxWinnerAdds = (int)n("wmax", 0); s.Swing = n("swing", 1) == 1; s.SwingK = (int)Math.Max(1, n("swk", 2)); s.FvgTf = (int)n("fvgtf", 5); s.WinnerLotsSell = n("wlots2", s.WinnerLots); s.Preset = t("preset", ""); s.VwapFilter = t("vf", "NONE"); s.CloseAtNy = d.ContainsKey("eod") ? d["eod"] : ""; s.TrackManual = n("man", 1) == 1; s.FvgAge = (int)n("fage", 0); s.FvgStrict = n("fstrict", 0) == 1; s.HedgeMult = n("hmult", 2); s.MaxHedges = (int)n("maxh", 1); s.ExitMode = t("exit", "BASKET"); s.AggRun = (int)Math.Max(1, n("aggrun", 4)); s.FvgStack = n("fstack", 1) == 1; s.EodLoss = t("eodl", "HOLD"); s.RangeOnly = n("ronly", 0) == 1; s.QuickHedge = n("qh", 0) == 1; s.CandleHedge = n("chedge", 0) == 1; s.Smart = n("smart", 0) == 1; s.SmartBest = n("sbest", 0) == 1; s.SmartMin = (int)Math.Max(3, n("smartn", 10)); s.QuickAfter = Math.Max(0.1, n("qafter", 3)); s.ProtectAt = Math.Max(0, Math.Min(99, n("pat", 0))); s.RiskGuard = n("rg", 0) == 1; s.GuardLots = Math.Max(0.01, n("glots", 1.0)); s.GuardMargin = Math.Max(0, n("gmargin", 500)); s.GuardChop = (int)Math.Max(0, n("gchop", 3)); s.GuardMaxLead = (int)Math.Max(1, n("glead", 3)); s.GuardLift = n("glift", 0) == 1; s.GuardMode = t("gmode", "LOCK"); s.GuardBreak = Math.Max(0, n("gbreak", 1)); s.HardFloat = Math.Max(0, n("hfloat", 0)); s.HardMargin = Math.Max(0, n("hmargin", 0)); s.PairOff = n("pair", 1) == 1; s.TrendHedge = n("thedge", 0) == 1; s.ScaleOut = n("scale", 0) == 1; s.ScaleLots = Math.Max(0.01, n("slots", 0.05)); s.ScaleStep = Math.Max(0.1, n("sstep", 1.5)); s.TrendHedgeMult = Math.Max(1, n("thmult", 2)); s.ProtectKeep = Math.Max(0, Math.Min(95, n("pkeep", 10))); s.HedgeCap = Math.Max(0, n("hcap", 5)); s.LadderStep = Math.Max(0.1, n("lstep", 2.5)); s.LadderMax = (int)Math.Max(0, n("lmax", 10)); s.TrendFilter = t("trend", "NONE"); s.BeAfter = (int)Math.Max(0, n("beafter", 0)); s.LockOn = n("lock", 0) == 1; s.LockAfter = (int)Math.Max(1, n("lafter", 2)); s.LockRange = Math.Max(0.01, n("lrange", 3)); s.Unlock = t("unlock", "LEAD"); s.HedgeSize = t("hsize", "ADVANTAGE"); s.NetAdvantage = n("nadv", 0.2); if (!d.ContainsKey("hs2") && s.HedgeSize == "FIXED" && s.Preset != "BOX") s.HedgeSize = "ADVANTAGE";   /* the user's rule: the new side always LEADS BY the lots (older saved settings were FIXED by default) */ s.RecoverMove = n("rmove", 3); s.HedgeMaxLots = n("hmax", 1); s.DangerExit = n("dexit", 0) == 1; s.DangerAt = Math.Max(10, Math.Min(100, n("dat", 60))); s.DangerKeep = Math.Max(0, Math.Min(100, n("dkeep", 25))); s.MaxDdPct = Math.Max(0, Math.Min(100, n("ddpct", 0))); s.RecoverExit = n("rexit", 0) == 1; s.BeLock = n("belock", 0) == 1; s.BeLockAt = Math.Max(10, Math.Min(99, n("belat", 80))); s.BeLockKeep = Math.Max(0, Math.Min(90, n("belkeep", 10))); s.RecDeep = Math.Max(0.5, n("rdeep", 2)); s.RecKeep = Math.Max(0, Math.Min(100, n("rkeep", 20))); s.DayLoss = Math.Max(0, n("dayloss", 0)); s.DayLossPct = Math.Max(0, Math.Min(100, n("daypct", 0)));
            return s;
        }
    }

    // every hedge / lock / unlock with the market at that moment and what the price did in the next 30 minutes (for LEARN: which hedges were whipsaws)
    public sealed class HedgeEvent
    {
        public int Iter; public DateTime Time; public string Kind = "HEDGE"; public int Dir; public double Price, Lots, RangeLo, RangeHi, Atr, Basket, Best, Worst; public int Before; public bool Done; public string Ctx = "";
        public double RangeAtr { get { return Atr > 0 ? (RangeHi - RangeLo) / Atr : 0; } }
        public bool Whipsaw { get { return Done && -Worst > Best; } }   // it went further against the hedge than for it
    }
    public sealed class AutoTrade { public int Iter; public DateTime Time; public string Side = "", Why = ""; public double Lots, Price, Level, Basket; }
    public sealed class AutoIteration
    {
        public bool ProtectArmed, BeLockArmed;   // 🔐 the basket reached the protect level once
        public int N; public DateTime Start, End; public string FirstDir = "", Reason = ""; public int Buys, Sells, AddOns, WinnerAdds; public string Ctx = "";   // Ctx: the market when it started (setup, VWAP side, FVG, order flow, session …) for LEARN
        public bool Manual; public int ManualAdds;
        public double Fees; public readonly Dictionary<long, double> FeeByTicket = new Dictionary<long, double>();   // COMMISSIONS of the iteration: each position's open commission + the same again for its close (in / out deals)
        public double BuyLots, SellLots, MaxGross, MaxDd, Result, BalanceStart, BalanceEnd, Estimate, Peak, CloseBid; public bool Adopted;
        // AFTER THE TARGET (user's request, to train the algo): a TARGET basket is followed as if it were still open — its best P/L afterwards
        // and when, when it fell back to the target and to breakeven (watched up to 8 hours). AfterMax = NaN: not followed.
        public double AfterMax = double.NaN, AfterMaxMin = -1, AfterBackTargetMin = -1, AfterBreakevenMin = -1, AfterMove; public bool AfterDone;
        public bool Done { get { return End > DateTime.MinValue; } }
        public bool Won { get { return Done && Result > 0; } }
    }

    public sealed class HedgeAuto
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public AutoSettings Set = new AutoSettings();
        // ✏ EDIT WHILE RUNNING (user's request): new settings wait here and take over between two iterations — the open one keeps its own
        public AutoSettings Next;
        public readonly List<HedgeEvent> Hedges = new List<HedgeEvent>(); public Action<HedgeEvent> OnHedgeDone; public Action<AutoIteration> OnAfterDone;   // OnAfterDone: an iteration's after-target follow-up is finished • the recording (saved by the window when each one is 30 minutes old)
        public double Ema50 = double.NaN, Ema200 = double.NaN; public int TrendNow; DateTime emaAt = DateTime.MinValue; string trendSkip = "";
        // ✋ YOUR hedge (user's request): dragged / typed on the chart, or removed — wins over the automation's level until the next leg fills
        public double ManualHedge = double.NaN; public bool ManualNoHedge; public double NextHedgeLots = double.NaN;   // the REAL size of the next hedge (for the chart)
        double scaleRef = double.NaN;
        public void SetManualHedge(double px) { ManualHedge = px; ManualNoHedge = false; ApplyManual(); Note("✋ YOUR HEDGE LEVEL " + P(px) + " — the automation hedges there (until the next leg fills)"); Version++; }
        public void RemoveHedge() { ManualNoHedge = true; ManualHedge = double.NaN; ApplyManual(); Note("✋ NO HEDGE for this leg (you removed it) — ↺ BACK TO AUTO on the line's menu brings it back"); Version++; }
        public void AutoHedgeAgain() { ManualHedge = double.NaN; ManualNoHedge = false; manualReset = true; Note("↺ the hedge is the automation's again"); Version++; }
        bool manualReset;
        public double AutoLevelShadow = double.NaN;   // the automation's level while you removed the hedge (drawn faint, to bring it back)
        void ApplyManual() { if (ManualNoHedge) { if (!double.IsNaN(HedgeLevel)) AutoLevelShadow = HedgeLevel; HedgeLevel = double.NaN; HedgeWhy = "✋ no hedge (you removed it)"; } else if (!double.IsNaN(ManualHedge)) { HedgeLevel = ManualHedge; HedgeWhy = "✋ YOUR level " + P(ManualHedge); } }
        long pairNext;   // the pair-off's second leg
        double quickBest = double.NaN; DateTime guardQuiet = DateTime.MinValue, lockAt = DateTime.MinValue; string guardHeld = "";   // the quick hedge's closest level so far (per leg)
        public double StraddleRef = double.NaN, LadderHi = double.NaN, LadderLo = double.NaN; string ladderFull = "";
        public bool Locked; public double LockHi = double.NaN, LockLo = double.NaN; string pendSide = ""; int liftDir, lockFrom;   // lockFrom: the add-ons count after the last unlock — a new lock needs LockAfter NEW hedges after it
        // ⚡ APPLY NOW: the new settings take over at once, the open basket included (its positions stay; the next hedge / exit uses the new rules)
        public void ApplyNow(AutoSettings s) { if (s == null) return; Next = s.Copy(); ApplyNext(); if (Current != null) Tell("⚡ the new settings apply to the OPEN iteration #" + Current.N + " too (its positions stay)."); }
        public void Queue(AutoSettings s) { Next = s == null ? null : s.Copy(); Version++; if (Next != null && Current == null && Running) ApplyNext(); }
        void ApplyNext()
        {
            var n = Next; Next = null; if (n == null) return; Set = n; vwapLast = DateTime.MinValue; vwapDay = DateTime.MinValue; cumPV = 0; cumV = 0; Version++;
            Tell("⚙ NEW SETTINGS from this iteration on: entry " + Set.EntryLots.ToString("0.##", IC) + " lots • target +$" + Set.Target.ToString("0.##", IC) + " • " + Set.EntryMode + " on " + Tf.Name(Set.Tf) + " • hedge " + Set.HedgeMode + (Set.HedgeSize == "STEPLEAD" ? " (lead +" + Set.NetAdvantage.ToString("0.##", IC) + " per hedge)" : Set.HedgeSize == "ADVANTAGE" ? " (lead by " + Set.NetAdvantage.ToString("0.##", IC) + ")" : "") + (Set.Rolling ? " • highs / lows in real time" : ""));
        }
        public bool Running { get; private set; }
        public string Phase { get; private set; } = "OFF";   // OFF / WAIT BREAK / PAUSE / IN ITERATION / CLOSING
        public string StopReason = "";
        public readonly List<AutoIteration> Iterations = new List<AutoIteration>();
        public readonly List<AutoTrade> Trades = new List<AutoTrade>();
        public readonly List<string> Log = new List<string>();
        public readonly List<Tuple<int, string>> Story = new List<Tuple<int, string>>();   // (iteration, what happened and WHY — the candle, the level, the prices, the $)
        public AutoIteration Current;
        public int Version;                                      // changes when anything above changes (the window repaints)
        public double Basket, HedgeLevel = double.NaN, BuyLevel = double.NaN, SellLevel = double.NaN;   // live numbers for the window
        public DateTime Now, PauseUntil = DateTime.MinValue;
        public double NetLots, PerPrice, TargetPx = double.NaN, StopPx = double.NaN; public int LastDir;   // for the chart's guides while a trade is open
        // the order path: (command, args, label) → the command's id, or null with the reason in `refused`
        public Func<string, object[], string, string> Send;
        public string Refused = "";
        // ✋ MANUAL MANAGING (user's request): AUTO keeps reading, tracking and planning, but SENDS NOTHING — every order / close it would send
        // becomes a 💡 SUGGESTION on the chart and in the story (you place it yourself; a trade you open by hand is adopted as always).
        // Only the ⛔ HARD STOPS (whole-basket loss / margin level, the user's own "it is what it is" rule) still close by themselves.
        public bool ManualManage; public string Suggestion = ""; public DateTime SuggestAt = DateTime.MinValue; bool sugg, hardNow;
        string Ask(string cmd, object[] args, string label)
        {
            sugg = false;
            if (ManualManage && !hardNow)
            {
                sugg = true; Refused = "✋ MANUAL MANAGING — suggested, not sent";
                string t = label.Replace("(🤖", "(");
                if (t != Suggestion || (DateTime.Now - SuggestAt).TotalSeconds > 300) { Suggestion = t; SuggestAt = DateTime.Now; Version++; Note("💡 SUGGEST (✋ manual managing, nothing sent): " + t); }
                return null;
            }
            return Send == null ? null : Send(cmd, args, label);
        }
        public void SetManualManage(bool on) { if (ManualManage == on) return; ManualManage = on; Suggestion = ""; Version++; TellNow(on ? "✋ MANUAL MANAGING ON — AUTO only SUGGESTS from now on (nothing is sent by itself except the ⛔ hard stops). Your own trades are adopted and tracked." : "🤖 MANUAL MANAGING OFF — AUTO sends its orders again (the same checks: ARM, MT5, the limits)."); }
        // the news: (server time, minutes before, minutes after) → the high-impact event near that moment, or null (the window gives it)
        public Func<DateTime, int, int, string> NewsNear;
        public double Vwap = double.NaN; public string SignalText = "", HedgeWhy = "", Blocked = "";

        int lastDir; string blockedKey = ""; double prevBid = double.NaN, prevFormH = double.NaN, prevFormL = double.NaN, knownGross; DateTime prevFormT; int engDir; DateTime engBar; string engStory = "", vwapSkip = ""; int hEngDir; DateTime hEngBar; public int AggDir; public double AggLevel = double.NaN; public DateTime AggBar; string hEngStory = ""; long pendTicket; bool closedByUs;
        string pendId, pendKind; DateTime pendSent, pendOk; double pendGross; int pendDirAfter; double pendLots; string pendWhy; double pendLevel;
        DateTime closeSeen;
        // the entry setups, worked out once per finished candle
        DateTime sigFvg = DateTime.MinValue, sigBar = DateTime.MinValue, vwapDay = DateTime.MinValue, vwapLast = DateTime.MinValue; double cumPV, cumV;
        sealed class Fvg { public int Dir, Count = 1; public double Top, Bottom, First; public DateTime Time, From; }   // First = the low (bullish) / high (bearish) of the FIRST candle of the gap — the FVG hedge level; Count = gaps stacked in a row
        Fvg entryFvg; public List<Tuple<int, double, double, DateTime, int>> FvgZones { get { return fvgs.Select(f => Tuple.Create(f.Dir, f.Bottom, f.Top, f.From, f.Count)).ToList(); } }
        readonly List<Fvg> fvgs = new List<Fvg>(); int flowDir; double flowLevel = double.NaN; DateTime flowBar, flowUntil;
        static double BuyShare(Mt5Bar b) { double r = b.H - b.L; if (r <= 0) return 0.5; double pos = (b.C - b.L) / r, body = (b.C - b.O) / r; return Math.Max(0, Math.Min(1, 0.5 * pos + 0.5 * (0.5 + 0.5 * body))); }   // the chart's estimate (ChartFlow)

        public Action<int, string> OnTell, OnNote;
        // 📉 THE BASKET'S PATH (for learning): once a minute while an iteration is open — the time, bid, basket $, net lots, legs, gross lots
        public Action<string> OnPath; DateTime pathMin = DateTime.MinValue;
        // 💾 THE OPEN ITERATION survives a restart: saved every minute (and at STOP), read back when the same positions are adopted again
        public sealed class OpenSave { public AutoIteration It; public HashSet<long> Tickets = new HashSet<long>(); public DateTime SavedAt; }
        public Action<string> OnOpenState; public Func<OpenSave> RestoreOpen; DateTime openSavedAt = DateTime.MinValue;
        public static string Dur(TimeSpan t) { return t.TotalDays >= 1 ? ((int)t.TotalDays) + "d " + t.Hours + "h " + t.Minutes.ToString("00") + "m" : t.TotalHours >= 1 ? ((int)t.TotalHours) + "h " + t.Minutes.ToString("00") + "m" : t.Minutes + "m " + t.Seconds.ToString("00") + "s"; }
        public string OpenText(IEnumerable<long> tickets)
        {
            var x = Current; if (x == null) return "";
            return string.Join("|", x.N.ToString(IC), x.Start.Ticks.ToString(IC), x.FirstDir, x.Buys.ToString(IC), x.Sells.ToString(IC), x.AddOns.ToString(IC), x.BuyLots.ToString("R", IC), x.SellLots.ToString("R", IC), x.MaxGross.ToString("R", IC), x.MaxDd.ToString("R", IC), x.BalanceStart.ToString("R", IC), x.Peak.ToString("R", IC), (x.Ctx ?? "").Replace("|", " "), x.Manual ? "1" : "0", string.Join(";", tickets.Select(t => t.ToString(IC))), Now.Ticks.ToString(IC), string.Join(";", x.FeeByTicket.Select(kv => kv.Key.ToString(IC) + "=" + kv.Value.ToString("R", IC))));
        }
        public static OpenSave ParseOpen(string line)
        {
            try
            {
                var a = (line ?? "").Split('|'); if (a.Length < 16) return null; Func<int, double> d = i => double.Parse(a[i], IC);
                var it = new AutoIteration { N = int.Parse(a[0], IC), Start = new DateTime(long.Parse(a[1], IC)), FirstDir = a[2], Buys = int.Parse(a[3], IC), Sells = int.Parse(a[4], IC), AddOns = int.Parse(a[5], IC), BuyLots = d(6), SellLots = d(7), MaxGross = d(8), MaxDd = d(9), BalanceStart = d(10), Peak = d(11), Ctx = a[12], Manual = a[13] == "1" };
                var o = new OpenSave { It = it, SavedAt = new DateTime(long.Parse(a[15], IC)) }; foreach (var t in a[14].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) o.Tickets.Add(long.Parse(t, IC));
                if (a.Length > 16) foreach (var kv in a[16].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { var q = kv.Split('='); if (q.Length == 2) it.FeeByTicket[long.Parse(q[0], IC)] = double.Parse(q[1], IC); }
                return o;
            }
            catch { return null; }
        }   // every story / log line as it happens (the window writes them to its files)
        string mmKey = ""; DateTime mmAt = DateTime.MinValue;
        void Tell(string s)
        {   // ✋ manual managing: a decision that only became a suggestion is told once (not on every tick)
            if (ManualManage) { int wi = s.IndexOf("WHY", StringComparison.Ordinal); string k = wi > 0 ? s.Substring(0, wi) : s; if (k == mmKey && (DateTime.Now - mmAt).TotalSeconds < 300) return; mmKey = k; mmAt = DateTime.Now; }
            TellNow(s);
        }
        void TellNow(string s) { var line = Now.ToString("dd MMM yy HH:mm:ss", IC) + "  " + s; if (OnTell != null) try { OnTell(Current != null ? Current.N : 0, line); } catch { } Story.Add(Tuple.Create(Current != null ? Current.N : 0, line)); if (Story.Count > 5000) Story.RemoveRange(0, 1000); Version++; }
        string P(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.#####", IC); }
        string M(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", IC); }
        // the candle(s) behind the recent high / low, in words
        string Candles(Snap sn, bool high)
        {
            if (Set.Swing) { var pv = Pivot(sn, high); return pv == null ? "(no swing yet)" : "the SWING " + (high ? "HIGH" : "LOW") + " = the " + Tf.Name(Set.Tf) + " candle " + pv.Time.ToString("HH:mm", IC) + " (O " + P(pv.O) + " H " + P(pv.H) + " L " + P(pv.L) + " C " + P(pv.C) + ", " + (high ? "higher" : "lower") + " than " + Set.SwingK + " candles on each side)"; }
            if (sn.Done.Count == 0) return "(no finished candles yet)"; var best = high ? sn.Done.OrderByDescending(x => x.H).First() : sn.Done.OrderBy(x => x.L).First();
            string one = Tf.Name(Set.Tf) + " candle " + best.Time.ToString("HH:mm", IC) + " (O " + P(best.O) + " H " + P(best.H) + " L " + P(best.L) + " C " + P(best.C) + ")";
            return sn.Done.Count == 1 ? "the previous " + one : "the " + (high ? "highest" : "lowest") + " of the last " + sn.Done.Count + " " + Tf.Name(Set.Tf) + " candles (" + sn.Done.Last().Time.ToString("HH:mm", IC) + "–" + sn.Done.First().Time.ToString("HH:mm", IC) + ") — the " + one;
        }
        // where the bid has to go for the basket to reach the target (net lots × $ per price), and the next add-on
        string Plan(Snap sn)
        {
            double net = sn.Pos.Sum(p => p.Dir * p.Lots), basket = sn.Pos.Count > 0 ? BasketOf(sn) : 0, tgt = sn.PerPrice > 0 && Math.Abs(net) > 1e-9 ? sn.Bid + (Set.Target - basket) / (net * sn.PerPrice) : double.NaN;
            string hm = Set.HedgeMode == "PRICE" ? Set.HedgePrice.ToString("0.#####", IC) + " against the last leg" : Set.HedgeMode == "MONEY" ? "−$" + Set.HedgeMoney.ToString("0.##", IC) + " since the last leg" : "the break of " + (lastDir > 0 ? "the recent low" : "the recent high");
            return "Now " + sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots).ToString("0.##", IC) + " bought / " + sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots).ToString("0.##", IC) + " sold (net " + (net >= 0 ? "+" : "") + net.ToString("0.##", IC) + "), basket " + M(basket)
                + ". TAKE PROFIT: the basket closes at " + M(Set.Target) + (double.IsNaN(tgt) ? "" : " — at a bid of ≈ " + P(tgt)) + (Set.HedgeMode == "NONE" ? ". NO HEDGE (no add-ons)" + (Set.MaxDrawdown > 0 ? ". EMERGENCY STOP: closes at −$" + Set.MaxDrawdown.ToString("0.##", IC) : "") + "." : "") + (Set.HedgeMode == "SIGNAL" ? ". SIGNAL HEDGE: " + (Set.HedgeMult).ToString("0.#", IC) + "× the net the other way on an opposite " + Tf.Name(Set.Tf) + " engulfing at a 20-candle extreme while the newest leg is down $" + Set.HedgeMoney.ToString("0.##", IC) + "+ (at most " + Set.MaxHedges + ")" + (Set.ExitMode == "LEGS" ? "; each leg closes at its own +$" + Set.Target.ToString("0.##", IC) + " per " + Set.EntryLots.ToString("0.##", IC) + " lot" : "") + (Set.MaxDrawdown > 0 ? "; basket stop −$" + Set.MaxDrawdown.ToString("0.##", IC) : "") + "." : "") + (Set.HedgeMode == "NONE" || Set.HedgeMode == "SIGNAL" ? "" : ". NEXT ADD-ON " + (lastDir > 0 ? "SELL " : "BUY ") + HedgeLots(sn).ToString("0.##", IC) + (Set.HedgeSize == "ADVANTAGE" ? " (" + (lastDir > 0 ? "sells" : "buys") + " then lead by " + Set.NetAdvantage.ToString("0.##", IC) + ")" : "") + " if the bid " + (lastDir > 0 ? "falls to " : "rises to ") + P(HedgeLevel) + " (" + hm + ")"
                + (Set.MaxDrawdown > 0 ? ". MAX DRAWDOWN: closes at −$" + Set.MaxDrawdown.ToString("0.##", IC) : "") + ".");
        }
        public void Note(string s) { var line = (Now > DateTime.MinValue ? Now : DateTime.Now).ToString("dd MMM HH:mm:ss", IC) + "  " + s; if (OnNote != null) try { OnNote(0, line); } catch { } Log.Add(line); if (Log.Count > 3000) Log.RemoveRange(0, 1000); Version++; }

        // ---- start / stop
        // a problem that stops the start ("" = fine); adopt = the positions already open on the symbol become the current iteration
        public string Check(IBroker b, AutoSettings s)
        {
            if ((s.HedgeMode == "LADDER" || s.EntryMode == "STRADDLE") && b != null && !b.IsSim) return "🪜 HM LADDER is a DRAFT (decoded from one day of his stream; the 12-month backtest was thin and can get stuck) — DEMO / BACKTEST only for now";
            if (string.IsNullOrWhiteSpace(s.Symbol)) return "choose a symbol";
            if (s.EntryLots <= 0 || (s.AddLots <= 0 && s.HedgeMode != "NONE" && s.HedgeMode != "SIGNAL")) return "the entry and add-on lots must be above 0";
            int cm; if (s.CloseAtNy != "" && !NyMin(s.CloseAtNy, out cm)) return "the close time \"" + s.CloseAtNy + "\" cannot be read (New York, like 16:55)";
            if (s.Target <= 0) return "the target per iteration must be above $0";
            if (!Tf.All.Contains(s.Tf)) return "the timeframe must be one of M1 … W1";
            if (s.Lookback < 1) return "the recent high / low needs at least 1 candle";
            if (s.HedgeMode == "PRICE" && s.HedgePrice <= 0) return "the add-on price distance must be above 0";
            if (s.HedgeMode == "MONEY" && s.HedgeMoney <= 0) return "the add-on $ amount must be above 0";
            if (s.AddToWinner && s.WinnerLots <= 0) return "the add-to-the-winner lots must be above 0";
            lock (b.State.Gate)
            {
                var st = b.State; if (!st.Symbols.ContainsKey(s.Symbol)) return s.Symbol + " is not in MT5's symbols (Market Watch)";
                if (!b.IsSim && st.Account.Mode == "NET") return "this MT5 account is NETTING — the strategy needs a HEDGING account (buys and sells side by side)";
                if (st.MaxLots > 0 && Math.Max(s.EntryLots, s.HedgeMode == "NONE" ? 0 : s.AddLots) > st.MaxLots + 1e-9) return "the bridge allows at most " + st.MaxLots.ToString("0.##", IC) + " lots an order (SpasiXBridge input MaxLotsPerOrder) — raise it in MT5 or use smaller lots";
            }
            return "";
        }
        public void Start(IBroker b, AutoSettings s, bool adopt)
        {
            Set = s.Copy(); Running = true; StraddleRef = double.NaN; LadderHi = LadderLo = double.NaN; Locked = false; lockFrom = 0; StopReason = ""; blockedKey = "~"; lastDir = 0; engDir = 0; vwapSkip = ""; AggDir = 0; AggLevel = double.NaN; prevBid = double.NaN; pendId = null; PauseUntil = DateTime.MinValue; Current = null;
            Snap sn = Read(b); Now = sn.Now;
            Note("▶ START " + Set.Symbol + " " + Tf.Name(Set.Tf) + " • entry " + Set.EntryLots.ToString("0.##", IC) + " • add-on " + Set.AddLots.ToString("0.##", IC) + " • target $" + Set.Target.ToString("0.##", IC) + " • max drawdown " + (Set.MaxDrawdown > 0 ? "$" + Set.MaxDrawdown.ToString("0.##", IC) : "none") + " • first " + Set.FirstDir + " • next " + (Set.NextStart == "PAUSE" ? "pause " + Set.PauseSec + " s" : "the next break"));
            if (sn.Pos.Count > 0 && adopt)
            {
                var last = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).Last(); lastDir = last.Dir;
                Current = new AutoIteration { N = Iterations.Count + 1, Start = Now, FirstDir = sn.Pos.OrderBy(p => p.Time).First().Side, BalanceStart = sn.Balance, Adopted = true, Buys = sn.Pos.Count(p => p.Dir > 0), Sells = sn.Pos.Count(p => p.Dir < 0), BuyLots = sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots), SellLots = sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots) };
                Current.AddOns = Math.Max(0, sn.Pos.Count - 1);
                // the SAME basket as before the restart (its positions match the saved open iteration): it keeps its number, its start, its numbers
                var sv = RestoreOpen != null ? RestoreOpen() : null; bool same = false;
                if (sv != null && sv.Tickets.Count > 0 && sn.Pos.All(p => sv.Tickets.Contains(p.Ticket) || p.Time >= sv.SavedAt))
                {
                    var o = sv.It; Current.N = o.N; Current.Start = o.Start; Current.FirstDir = o.FirstDir; Current.BalanceStart = o.BalanceStart; Current.MaxDd = o.MaxDd; Current.MaxGross = Math.Max(o.MaxGross, Gross(sn.Pos)); Current.Peak = o.Peak; Current.Ctx = o.Ctx;
                    Current.Buys = Math.Max(Current.Buys, o.Buys); Current.Sells = Math.Max(Current.Sells, o.Sells); Current.AddOns = Math.Max(Current.AddOns, o.AddOns); Current.Adopted = false; Current.Manual = o.Manual; same = true;
                    foreach (var kv in o.FeeByTicket) Current.FeeByTicket[kv.Key] = kv.Value;
                }
                if (!same)
                {   // no saved open iteration (an older build): its trades in the history still tell when it began
                    var trs = Trades.Where(t => t.Iter == Current.N && t.Side != "CLOSE").OrderBy(t => t.Time).ToList();
                    if (trs.Count > 0 && trs[0].Time < Now) { Current.Start = trs[0].Time; Current.FirstDir = trs[0].Side; Current.Buys = Math.Max(Current.Buys, trs.Count(t => t.Side == "BUY")); Current.Sells = Math.Max(Current.Sells, trs.Count(t => t.Side == "SELL")); Current.AddOns = Math.Max(Current.AddOns, trs.Count - 1); Current.MaxDd = Math.Min(0, trs.Min(t => t.Basket)); Current.Adopted = false; same = true; }
                }
                Iterations.RemoveAll(x => !x.Done && x.N == Current.N); Iterations.Add(Current); Phase = "IN ITERATION"; SetHedgeLevel(sn, lastDir); knownGross = Gross(sn.Pos);
                Note(same ? "⤴ iteration #" + Current.N + " continues after the restart — open since " + Current.Start.ToString("dd MMM HH:mm", IC) + " (" + sn.Pos.Count + " positions)" : "⤴ adopted the " + sn.Pos.Count + " position(s) open on " + Set.Symbol + " as iteration #" + Current.N);
                if (same && Current.Start.Date < Now.Date) Tell("↪ CARRIED OVER: iteration #" + Current.N + " has been open since " + Current.Start.ToString("ddd dd MMM HH:mm", IC) + " (" + Dur(Now - Current.Start) + ") — " + sn.Pos.Count + " positions, basket " + M(BasketOf(sn)) + ".");
            }
            else Wait(sn, Set.FirstDir == "BUY" || Set.FirstDir == "SELL" ? "FIRST" : "BREAK");
            Version++;
        }
        // a stop that means the (simulated) account is blown: no money / margin for the next order
        public static bool IsBlowReason(string why) { var w = (why ?? "").ToLowerInvariant(); return w.Contains("not enough money") || w.Contains("no money") || w.Contains("not enough margin") || w.Contains("stop out") || w.Contains("stopout"); }
        public void Stop(string why) { if (!Running) return; Running = false; Phase = "OFF"; StopReason = why; pendId = null; Note("■ STOP — " + why); }

        // ---- one look at the market (every tick of the BACKTEST, every refresh live)
        sealed class Snap { public readonly Dictionary<long, double> InComm = new Dictionary<long, double>(); /* each open position's opening commission from MT5's deals (a real account books it on the deal, not on the position) */ public DateTime Now; public double Bid = double.NaN, Ask = double.NaN, Balance, MarginLevel, Equity; public List<Mt5Position> Pos = new List<Mt5Position>(); public List<Mt5Bar> Done = new List<Mt5Bar>(), Fin = new List<Mt5Bar>(), FinFvg = new List<Mt5Bar>(); public bool Loaded; public Tuple<bool, string> Res; public double PerPrice; public double FormH = double.NaN, FormL = double.NaN; public DateTime FormT; public double DayHi = double.NaN, DayLo = double.NaN; }   // Form* = the forming candle (its high / low catch a break between two price updates)   // PerPrice = $ per 1.0 of price per lot
        Snap Read(IBroker b)
        {
            var sn = new Snap();
            lock (b.State.Gate)
            {
                var st = b.State; sn.Now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; Mt5Quote q; if (st.Quotes.TryGetValue(Set.Symbol, out q)) { sn.Bid = q.Bid; sn.Ask = q.Ask; }
                sn.Balance = st.Account.Balance; sn.MarginLevel = st.Account.MarginLevel; sn.Equity = st.Account.Equity; Mt5Symbol ms; if (st.Symbols.TryGetValue(Set.Symbol, out ms) && ms.TickSize > 0) sn.PerPrice = ms.TickValue / ms.TickSize;
                foreach (var p in st.Positions) if (string.Equals(p.Symbol, Set.Symbol, StringComparison.OrdinalIgnoreCase)) sn.Pos.Add(p);
                if (sn.Pos.Count > 0) { var tks = new HashSet<long>(sn.Pos.Select(p => p.Ticket)); foreach (var d in st.Deals) if (d.Entry == "IN" && tks.Contains(d.Position)) { double c0; sn.InComm.TryGetValue(d.Position, out c0); sn.InComm[d.Position] = c0 + Math.Abs(d.Commission); } }
                var se = st.SeriesOf(Set.Symbol, Set.Tf); sn.Loaded = se.Bars.Count > 0;
                for (int i = se.Bars.Count - 1; i >= 0 && sn.Fin.Count < Math.Max(60, Set.Lookback); i--) if (se.Bars[i].Time.AddMinutes(Set.Tf) <= sn.Now) sn.Fin.Add(se.Bars[i]);
                sn.Done.AddRange(sn.Fin.Take(Set.Lookback));
                if (se.Bars.Count > 0) { var lb = se.Bars[se.Bars.Count - 1]; if (lb.Time.AddMinutes(Set.Tf) > sn.Now) { sn.FormH = lb.H; sn.FormL = lb.L; sn.FormT = lb.Time; } }
                {   // the day so far (server day): its high / low — where the entry sits in the day's range (the learning's context)
                    double dh = double.MinValue, dl = double.MaxValue; for (int i = se.Bars.Count - 1; i >= 0 && se.Bars[i].Time.Date == sn.Now.Date; i--) { if (se.Bars[i].H > dh) dh = se.Bars[i].H; if (se.Bars[i].L < dl) dl = se.Bars[i].L; }
                    if (dh > double.MinValue) { sn.DayHi = dh; sn.DayLo = dl; }
                }
                if ((Set.TrendFilter == "EMA" || Set.RiskGuard || Set.TrendHedge) && sn.Fin.Count > 0 && sn.Fin[0].Time != emaAt)
                {   // the EMA 50 / 200 of the finished candles (up to 1,200 of them), once per new candle
                    emaAt = sn.Fin[0].Time; int last = se.Bars.Count - 1; while (last >= 0 && se.Bars[last].Time.AddMinutes(Set.Tf) > sn.Now) last--;
                    int first = Math.Max(0, last - 1200); double e5 = double.NaN, e2 = double.NaN;
                    for (int i = first; i <= last; i++) { double c = se.Bars[i].C; e5 = double.IsNaN(e5) ? c : c * 2 / 51.0 + e5 * (1 - 2 / 51.0); e2 = double.IsNaN(e2) ? c : c * 2 / 201.0 + e2 * (1 - 2 / 201.0); }
                    if (last - first >= 200) { Ema50 = e5; Ema200 = e2; double cl = se.Bars[last].C; TrendNow = e5 > e2 && cl > e2 ? 1 : e5 < e2 && cl < e2 ? -1 : 0; } else { Ema50 = Ema200 = double.NaN; TrendNow = 0; }
                }
                if (Set.EntryMode.StartsWith("FVG"))
                {
                    var sf = st.SeriesOf(Set.Symbol, Set.FvgTf); for (int i = sf.Bars.Count - 1; i >= 0 && sn.FinFvg.Count < 60; i--) if (sf.Bars[i].Time.AddMinutes(Set.FvgTf) <= sn.Now) sn.FinFvg.Add(sf.Bars[i]);
                    if (!b.IsSim && !sf.Loaded && (DateTime.Now - sf.AskedAt).TotalSeconds > 5) { sf.AskedAt = DateTime.Now; b.Send("BARS|" + Set.Symbol + "|" + Set.FvgTf + "|300"); }
                }
                // VWAP of the server day: the finished candles not counted yet (typical price × tick volume)
                if (sn.Fin.Count > 0 && sn.Fin[0].Time > vwapLast)   // always: it is in every iteration's LEARN context
                {
                    var add = new List<Mt5Bar>(); for (int i = se.Bars.Count - 1; i >= 0; i--) { var x = se.Bars[i]; if (x.Time <= vwapLast) break; if (x.Time.AddMinutes(Set.Tf) <= sn.Now) add.Add(x); }
                    for (int i = add.Count - 1; i >= 0; i--) { var x = add[i]; if (x.Time.Date != vwapDay) { vwapDay = x.Time.Date; cumPV = 0; cumV = 0; } double v = Math.Max(1, x.V); cumPV += (x.H + x.L + x.C) / 3 * v; cumV += v; vwapLast = x.Time; }
                    Vwap = cumV > 0 && vwapDay == sn.Now.Date ? cumPV / cumV : double.NaN;
                }
                if (!b.IsSim && !se.Loaded && (DateTime.Now - se.AskedAt).TotalSeconds > 5) { se.AskedAt = DateTime.Now; b.Send("BARS|" + Set.Symbol + "|" + Set.Tf + "|300"); }
                if (pendId != null) for (int i = st.Results.Count - 1; i >= 0; i--) if (st.Results[i].Item1 == pendId) { sn.Res = Tuple.Create(st.Results[i].Item2, st.Results[i].Item3); break; }
            }
            return sn;
        }
        // the level: the most recent SWING high / low (a finished candle above / below SwingK candles on each side), or the last Lookback candles
        Mt5Bar Pivot(Snap sn, bool high)
        {
            int k = Math.Max(1, Set.SwingK); var f = sn.Fin;
            for (int j = k; j + k < f.Count; j++)
            {
                bool ok = true; for (int d = 1; d <= k && ok; d++) ok = high ? f[j].H > f[j - d].H && f[j].H > f[j + d].H : f[j].L < f[j - d].L && f[j].L < f[j + d].L;
                // a swing the price has already traded through since it formed is USED UP — never a break again (the user's bug: an old low re-used after a retrace)
                for (int i = 0; i < j && ok; i++) ok = high ? f[i].H <= f[j].H : f[i].L >= f[j].L;
                if (ok) return f[j];
            }
            return null;
        }
        double High(Snap sn) { if (Set.Swing) { var pv = Pivot(sn, true); return pv != null ? pv.H : double.NaN; } return sn.Done.Count < Set.Lookback ? double.NaN : sn.Done.Max(x => x.H); }
        double Low(Snap sn) { if (Set.Swing) { var pv = Pivot(sn, false); return pv != null ? pv.L : double.NaN; } return sn.Done.Count < Set.Lookback ? double.NaN : sn.Done.Min(x => x.L); }
        static double Gross(List<Mt5Position> p) { return p.Sum(x => x.Lots); }
        // the basket = what closing everything now would give: profit + swap + the commission paid at the open AND the same again for the close
        // (brokers that charge "in / out deals", like Duramarkets' 0.001 %) — so +$20 is +$20 after every commission
        double OpenComm(Snap sn, Mt5Position p) { double c; return Math.Max(Math.Abs(p.Commission), sn.InComm.TryGetValue(p.Ticket, out c) ? c : 0); }
        double BasketOf(Snap sn) { return sn.Pos.Sum(p => p.Profit + p.Swap - 2 * OpenComm(sn, p)) - (Set.CommissionPerLot > 0 ? Gross(sn.Pos) * Set.CommissionPerLot : 0); }

        // AFTER THE TARGET: followed on every step, also when the automation is stopped (a target reached just before STOP is still measured)
        void TrackAfter(IBroker b)
        {
            if (!Iterations.Any(x => x.Done && x.Reason == "TARGET" && !x.AfterDone && !double.IsNaN(x.AfterMax))) return;
            double bid = double.NaN, pp = 0; DateTime now;
            lock (b.State.Gate) { var st = b.State; now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; Mt5Quote q; if (st.Quotes.TryGetValue(Set.Symbol, out q)) bid = q.Bid; Mt5Symbol ms; if (st.Symbols.TryGetValue(Set.Symbol, out ms) && ms.TickSize > 0) pp = ms.TickValue / ms.TickSize; }
            if (double.IsNaN(bid) || bid <= 0 || pp <= 0) return; if (now > Now) Now = now;
            // AFTER THE TARGET: what each closed TARGET basket would be worth now (the same net lots, from the bid it closed at)
            foreach (var ia in Iterations.Skip(Math.Max(0, Iterations.Count - 50))) if (ia.Done && ia.Reason == "TARGET" && !ia.AfterDone && !double.IsNaN(ia.AfterMax) && ia.CloseBid > 0 && pp > 0)
            {
                double netA = ia.BuyLots - ia.SellLots, ghost = ia.Result + netA * (bid - ia.CloseBid) * pp, mins = (Now - ia.End).TotalMinutes;
                if (ghost > ia.AfterMax) { ia.AfterMax = ghost; ia.AfterMaxMin = mins; ia.AfterMove = Math.Abs(bid - ia.CloseBid); ia.AfterBackTargetMin = -1; }   // "back to the target" counts from its best
                if (ia.AfterBackTargetMin < 0 && ia.AfterMax > ia.Result + 1 && ghost <= ia.Result) ia.AfterBackTargetMin = mins;
                if (ghost <= 0 || mins >= 480) { if (ghost <= 0) ia.AfterBreakevenMin = mins; ia.AfterDone = true; Version++; if (OnAfterDone != null) OnAfterDone(ia); }
            }
        }
        public void Step(IBroker b)
        {
            TrackAfter(b);
            if (!Running) return;
            if (Next != null && Current == null) ApplyNext();   // between iterations: the saved changes take over
            var sn = Read(b); if (sn.Now < (b.IsSim ? Now : Now.AddMinutes(-1))) { Stop("the clock went back (the backtest was rewound or restarted)"); return; }
            Now = sn.Now; if (double.IsNaN(sn.Bid) || sn.Bid <= 0) return;
            Basket = sn.Pos.Count > 0 ? BasketOf(sn) : 0;
            foreach (var he in Hedges) if (!he.Done)
            {   // what the price did after each hedge (in $ of gold per unit): its best and worst within 30 minutes
                double mv = he.Dir * (sn.Bid - he.Price); if (mv > he.Best) he.Best = mv; if (mv < he.Worst) he.Worst = mv;
                if (Now >= he.Time.AddMinutes(30)) { he.Done = true; if (OnHedgeDone != null) OnHedgeDone(he); }
            }
            NetLots = sn.Pos.Sum(p => p.Dir * p.Lots); PerPrice = sn.PerPrice; LastDir = lastDir;
            double move = Math.Abs(NetLots) > 1e-9 && sn.PerPrice > 0 ? NetLots * sn.PerPrice : 0;   // $ per 1.0 of price on what is open
            TargetPx = Current != null && move != 0 ? sn.Bid + (Set.Target - Basket) / move : double.NaN;
            StopPx = Current != null && move != 0 && Set.MaxDrawdown > 0 ? sn.Bid + (-Set.MaxDrawdown - Basket - (Set.ExitMode == "LEGS" ? sn.Balance - Current.BalanceStart : 0)) / move : double.NaN;
            BuyLevel = High(sn); SellLevel = Low(sn);
            try { Decide(b, sn); } finally { prevBid = sn.Bid; prevFormH = sn.FormH; prevFormL = sn.FormL; prevFormT = sn.FormT; }
        }

        void Decide(IBroker b, Snap sn)
        {
            // an order on its way: wait for MT5's answer, then for the position list to show it
            if (pendId != null)
            {
                if (sn.Res == null) { if ((DateTime.Now - pendSent).TotalSeconds > 20 && !b.IsSim) Stop("MT5 did not answer the " + pendKind + " in 20 s — check the bridge / Algo Trading"); return; }
                if (!sn.Res.Item1)
                {
                    var no = sn.Res.Item2; pendId = null;
                    if (pendKind == "CLOSE1" || pendKind == "TRIM" || pendKind == "PAIR" || pendKind == "PAIR2" || pendKind == "SCALE") { if (pendKind == "TRIM" || pendKind == "PAIR" || pendKind == "PAIR2") Locked = true; Note("⚠ close of #" + pendTicket + " refused: " + no + " — trying again"); return; }
                    if (pendKind == "CLOSESIDE") { Note("⚠ lift (close the " + pendSide + "s) refused: " + no + " — trying again"); Locked = true; return; }
                    if (pendKind == "CLOSE") { Note("⚠ close refused: " + no + " — trying again"); closeSeen = DateTime.MinValue; return; }
                    if (pendKind == "ORDER" && sn.Pos.Count > 0 && IsBlowReason(no))
                    {   // ⚠ the hedge / add-on / lock was refused for margin while a basket is open: HOLD and keep managing (never stop the automation)
                        marginWait = Now.AddMinutes(2); var mk = "MARGIN" + Now.ToString("yyyyMMddHH", IC); if (mk != marginNote) { marginNote = mk; Tell("⚠ NOT ENOUGH MARGIN — MT5 refused the " + pendWhy + " (" + no + ") → the automation HOLDS and keeps managing the basket (targets, rescue, locks, hard stops); new orders are tried again in 2 minutes."); }
                        return;
                    }
                    Stop("MT5 refused the " + pendWhy + ": " + no); return;
                }
                if (pendOk == DateTime.MinValue) pendOk = DateTime.Now;
                bool shown = pendKind == "CLOSE" ? sn.Pos.Count == 0 : pendKind == "CLOSE1" || pendKind == "TRIM" || pendKind == "PAIR" || pendKind == "PAIR2" ? !sn.Pos.Any(p => p.Ticket == pendTicket) : pendKind == "SCALE" ? Gross(sn.Pos) < pendGross - 1e-6 : pendKind == "CLOSESIDE" ? !sn.Pos.Any(p => pendSide == "BUY" ? p.Dir > 0 : p.Dir < 0) : Math.Abs(Gross(sn.Pos) - pendGross) < 1e-6;
                if (!shown && (DateTime.Now - pendOk).TotalSeconds < 6) return;
                pendId = null;
                if (pendKind == "ORDER") Opened(sn);
                if (pendKind == "CLOSE") entryFvg = null;
                if (pendKind == "CLOSE1") { knownGross = Gross(sn.Pos); closedByUs = true; }
                if (pendKind == "PAIR")
                {   // the losing leg is closed: now its winning partner
                    knownGross = Gross(sn.Pos); closedByUs = true; var w2 = sn.Pos.FirstOrDefault(p => p.Ticket == pairNext);
                    if (w2 != null) { Refused = ""; var id2 = Ask("CLOSE", new object[] { w2.Ticket, 0.0 }, "CLOSE #" + w2.Ticket + " " + w2.Side + " " + w2.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + Current.N + " 🛟 pair-off)"); if (id2 != null) { pendId = id2; pendKind = "PAIR2"; pendTicket = w2.Ticket; pendSent = DateTime.Now; pendOk = DateTime.MinValue; return; } }
                }
                if (pendKind == "SCALE") { knownGross = Gross(sn.Pos); closedByUs = true; Version++; }
                if (pendKind == "PAIR2") { knownGross = Gross(sn.Pos); closedByUs = true; Tell("🛟 PAIRED OFF — the basket is now " + Gross(sn.Pos).ToString("0.##", IC) + " lots (" + sn.Pos.Count + " legs), " + M(Basket) + " open. " + Plan(sn)); Version++; }
                if (pendKind == "TRIM") { knownGross = Gross(sn.Pos); closedByUs = true; lastDir = liftDir; quickBest = double.NaN; SetHedgeLevel(sn, liftDir); Tell("🛡 TRIMMED — the basket now leans " + (liftDir > 0 ? "LONG" : "SHORT") + " with the break, " + Gross(sn.Pos).ToString("0.##", IC) + " lots. " + Plan(sn)); Version++; }
                if (pendKind == "CLOSESIDE") { knownGross = Gross(sn.Pos); lastDir = liftDir; SetHedgeLevel(sn, liftDir); Tell("🔓 LIFTED — the " + pendSide + "s are closed; the " + (liftDir > 0 ? "BUYS" : "SELLS") + " run with the break. " + Plan(sn)); Version++; }
            }

            if (Phase == "CLOSING")
            {
                if (sn.Pos.Count > 0) { if (pendId == null && (DateTime.Now - closeSeen).TotalSeconds > 2) CloseAll(sn, Current.Reason, true); return; }
                Finish(sn); return;
            }

            if (Phase == "IN ITERATION")
            {
                var it = Current;
                if (sn.Pos.Count == 0 && closedByUs) { it.Reason = "LEG TARGETS"; Tell("ALL LEGS AT THEIR TARGETS — iteration #" + it.N + " done."); Finish(sn); return; }
                if (sn.Pos.Count == 0 && (it.Manual || Set.TrackManual)) { it.Reason = "CLOSED BY HAND"; Tell("✋ CLOSED BY HAND — the positions on " + Set.Symbol + " were closed outside the automation; the iteration is recorded and the automation goes on."); Finish(sn); return; }
                if (sn.Pos.Count == 0) { it.Reason = "CLOSED OUTSIDE"; Note("⚠ the positions on " + Set.Symbol + " were closed outside the automation (by hand or a stop-out)"); Finish(sn); return; }
                if (Math.Abs(Gross(sn.Pos) - knownGross) > 1e-6) Manual(sn, it);
                if (EndOfDay())
                {
                    if (Basket < 0 && Set.EodLoss != "CLOSE")
                    {   // hedging never closes at a loss: the basket stays open and keeps being managed (hedges, the target); only new iterations wait for tomorrow
                        if (eodHeld != it.N + "|" + Now.Date.Ticks) { eodHeld = it.N + "|" + Now.Date.Ticks; Tell("END OF DAY " + Set.CloseAtNy + " New York: the basket is " + M(Basket) + " → HELD OPEN (a hedge never closes at a loss). It is still managed — the hedges and the +" + M(Set.Target).TrimStart('+') + " target — and no new iteration starts until tomorrow."); }
                    }
                    else { it.Reason = "END OF DAY"; Tell("END OF DAY. WHY: it is " + Set.CloseAtNy + " New York (the close time) → CLOSE ALL at " + M(Basket) + "."); CloseAll(sn, "END OF DAY", false); return; }
                }
                double total = Basket + (Set.ExitMode == "LEGS" ? sn.Balance - it.BalanceStart : 0);   // LEGS: what the closed legs made counts too
                foreach (var p in sn.Pos) it.FeeByTicket[p.Ticket] = OpenComm(sn, p) * 2 + (Set.CommissionPerLot > 0 ? p.Lots * Set.CommissionPerLot : 0);   // the commissions so far (live)
                it.Fees = it.FeeByTicket.Values.Sum();
                if (OnOpenState != null && (DateTime.Now - openSavedAt).TotalSeconds >= 30) { openSavedAt = DateTime.Now; OnOpenState(OpenText(sn.Pos.Select(p => p.Ticket))); }
                { var pm = new DateTime(Now.Year, Now.Month, Now.Day, Now.Hour, Now.Minute, 0); if (pm != pathMin && OnPath != null) { pathMin = pm; OnPath(string.Join(",", it.N.ToString(IC), Now.Ticks.ToString(IC), sn.Bid.ToString("R", IC), Basket.ToString("0.00", IC), sn.Pos.Sum(p => p.Dir * p.Lots).ToString("0.##", IC), sn.Pos.Count.ToString(IC), Gross(sn.Pos).ToString("0.##", IC), Locked ? "LOCKED" : "")); } }
                it.MaxDd = Math.Min(it.MaxDd, total); it.Peak = Math.Max(it.Peak, Basket); it.MaxGross = Math.Max(it.MaxGross, Gross(sn.Pos));
                if (sn.Pos.Count > 0 && ((Set.HardFloat > 0 && Basket + sn.Balance - it.BalanceStart <= -Set.HardFloat / 100 * Math.Max(1, it.BalanceStart)) || (Set.HardMargin > 0 && sn.MarginLevel > 0 && sn.MarginLevel <= Set.HardMargin)))
                {   // ⛔ HARD STOP (user's): it is what it is — close everything before the broker does it worse
                    bool byF = Set.HardFloat > 0 && Basket + sn.Balance - it.BalanceStart <= -Set.HardFloat / 100 * Math.Max(1, it.BalanceStart); it.Reason = "HARD STOP";
                    Tell("⛔ HARD STOP. WHY: " + (byF ? "the iteration's loss " + M(Basket + sn.Balance - it.BalanceStart) + " (open + realised) reached " + Set.HardFloat.ToString("0", IC) + " % of the balance $" + it.BalanceStart.ToString("N0", IC) : "the margin level " + sn.MarginLevel.ToString("0", IC) + " % is at / under " + Set.HardMargin.ToString("0", IC) + " %") + " → CLOSE EVERYTHING (" + sn.Pos.Count + " positions).");
                    hardNow = true; try { CloseAll(sn, "HARD STOP", false); } finally { hardNow = false; } return;
                }
                DangerNow(sn, it);
                if (Set.BeLock && !Locked && Set.ExitMode != "LEGS" && Set.Target > 0 && sn.PerPrice > 0 && !double.IsNaN(sn.Bid))
                {   // 🔐 BREAKEVEN LOCK
                    double totB = Basket + sn.Balance - it.BalanceStart, netB = sn.Pos.Sum(p => p.Dir * p.Lots);
                    if (!it.BeLockArmed && totB >= Set.Target * Set.BeLockAt / 100) { it.BeLockArmed = true; Tell("🔐 BREAKEVEN LOCK ARMED: the basket reached " + M(totB) + " (≥ " + Set.BeLockAt.ToString("0", IC) + " % of the target " + M(Set.Target) + ") — if the price turns back to where it would be " + M(Set.Target * Set.BeLockKeep / 100) + ", an equal hedge locks it (net 0)."); }
                    if (it.BeLockArmed && Math.Abs(netB) > 0.005)
                    {
                        double keep = Set.Target * Set.BeLockKeep / 100, px = netB > 0 ? sn.Bid : sn.Ask; if (double.IsNaN(px)) px = sn.Bid;
                        BeLockPx = px - (totB - keep) / (netB * sn.PerPrice);   // the price where the basket = keep (long: below the price, short: above)
                        if (netB > 0 ? px <= BeLockPx : px >= BeLockPx) { it.BeLockArmed = false; BeLockPx = double.NaN; GuardLock(sn, it, "🔐 BREAKEVEN LOCK — the basket came back from ≥ " + Set.BeLockAt.ToString("0", IC) + " % of the target to " + M(totB) + ": the profit is kept, no drawdown"); return; }
                    }
                }
                else BeLockPx = double.NaN;
                if (Set.DangerExit && Set.ExitMode != "LEGS" && DangerPeak >= Set.DangerAt)
                {
                    double tot = Basket + sn.Balance - it.BalanceStart, keep = Math.Max(0.5, Set.Target * Set.DangerKeep / 100);
                    if (tot >= keep) { it.Reason = "DANGER EXIT"; Tell("🚨 DANGER EXIT (a smaller profit). WHY: this basket's danger meter reached " + DangerPeak + " (≥ " + Set.DangerAt.ToString("0", IC) + ": " + DangerPeakWhy + ") and the basket is back at " + M(tot) + " (≥ " + M(keep) + " = " + Set.DangerKeep.ToString("0", IC) + " % of the target) → take it, CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "DANGER EXIT", false); return; }
                }
                if (Set.ExitMode == "LEGS")
                {
                    foreach (var p in sn.Pos) { double own = p.Profit + p.Swap + p.Commission - (Set.CommissionPerLot > 0 ? p.Lots * Set.CommissionPerLot : 0), goal = Set.Target * p.Lots / Set.EntryLots;
                        if (own >= goal) { Tell("LEG TARGET. WHY: " + p.Side + " " + p.Lots.ToString("0.##", IC) + " @ " + P(p.Open) + " (#" + p.Ticket + ") is " + M(own) + " ≥ its own target " + M(goal) + " (+$" + Set.Target.ToString("0.##", IC) + " per " + Set.EntryLots.ToString("0.##", IC) + " lot) → close it."); CloseOne(p); return; } }
                }
                else if ((Set.RiskGuard || Set.ScaleOut ? Basket + sn.Balance - it.BalanceStart : Basket) >= Set.Target) { it.Reason = "TARGET"; Tell("TAKE PROFIT. WHY: the basket reached " + M(Set.RiskGuard || Set.ScaleOut ? Basket + sn.Balance - it.BalanceStart : Basket) + (Set.RiskGuard || Set.ScaleOut ? " (open " + M(Basket) + " + banked " + M(sn.Balance - it.BalanceStart) + ")" : "") + " ≥ the target " + M(Set.Target) + " at a bid of " + P(sn.Bid) + " → CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "TARGET", false); return; }
                if (Set.ProtectAt > 0 && Set.ExitMode != "LEGS")
                {   // 🔐 PROTECT: armed once the basket reached ProtectAt % of the target; it closes when it falls back to ProtectKeep % of the target
                    if (!it.ProtectArmed && Basket >= Set.Target * Set.ProtectAt / 100) { it.ProtectArmed = true; Tell("🔐 PROTECT ARMED: the basket reached " + M(Basket) + " (≥ " + Set.ProtectAt.ToString("0", IC) + " % of the target " + M(Set.Target) + ") — if it falls back to " + M(Set.Target * Set.ProtectKeep / 100) + " it closes there"); }
                    if (it.ProtectArmed && Basket <= Set.Target * Set.ProtectKeep / 100) { it.Reason = "PROTECT"; Tell("🔐 PROTECT EXIT. WHY: the basket was near the target (" + Set.ProtectAt.ToString("0", IC) + " % reached) and came back to " + M(Basket) + " ≤ " + M(Set.Target * Set.ProtectKeep / 100) + " → CLOSE ALL " + sn.Pos.Count + " positions (a near miss never turns into a long hedge)."); CloseAll(sn, "PROTECT", false); return; }
                }
                if (Set.BeAfter > 0 && it.AddOns >= Set.BeAfter && (Set.RiskGuard ? Basket + sn.Balance - it.BalanceStart : Basket) >= 0 && Set.ExitMode != "LEGS") { it.Reason = "RESCUE"; Tell("🛟 RESCUE EXIT. WHY: the basket has " + it.AddOns + " hedges (≥ " + Set.BeAfter + ") and is back at " + M(Basket) + " (breakeven or better) → CLOSE ALL " + sn.Pos.Count + " positions — no endless basket."); CloseAll(sn, "RESCUE", false); return; }
                {
                    double totR = Basket + sn.Balance - it.BalanceStart;
                    if (Set.MaxDdPct > 0 && totR <= -Set.MaxDdPct / 100 * Math.Max(1, it.BalanceStart)) { it.Reason = "MAX DRAWDOWN"; Tell("⛔ MAX DRAWDOWN %. WHY: the iteration is at " + M(totR) + " (open + realised) ≤ −" + Set.MaxDdPct.ToString("0.##", IC) + " % of its starting balance $" + it.BalanceStart.ToString("N2", IC) + " → CLOSE ALL " + sn.Pos.Count + " positions" + (Set.AfterDrawdown == "STOP" ? " and stop." : ", then the next trade.")); CloseAll(sn, "MAX DRAWDOWN", false); return; }
                    if (Set.RecoverExit && Set.ExitMode != "LEGS" && Set.Target > 0 && it.MaxDd <= -Set.RecDeep * Set.Target && totR >= -Set.Target * Set.RecKeep / 100)
                    { it.Reason = "RECOVERED"; Tell("🔄 RECOVERED EXIT. WHY: the basket went " + M(it.MaxDd) + " deep (≥ " + Set.RecDeep.ToString("0.#", IC) + " × the target) and came back to " + M(totR) + " (within " + Set.RecKeep.ToString("0", IC) + " % of the target from breakeven) → CLOSE ALL " + sn.Pos.Count + " positions: the drawdown is gone, the account waits for the next setup."); CloseAll(sn, "RECOVERED", false); return; }
                    string dl = DayLossHit(sn, totR);
                    if (dl != "") { it.Reason = "DAY LOSS"; dayBlocked = NyDay(); Tell("⛔ DAILY LOSS. WHY: " + dl + " → CLOSE ALL " + sn.Pos.Count + " positions — no new iteration until tomorrow (New York day)."); CloseAll(sn, "DAY LOSS", false); return; }
                }
                if (Set.MaxDrawdown > 0 && total <= -Set.MaxDrawdown) { it.Reason = "MAX DRAWDOWN"; Tell("MAX DRAWDOWN. WHY: the basket fell to " + M(Basket) + " ≤ −$" + Set.MaxDrawdown.ToString("0.##", IC) + " at a bid of " + P(sn.Bid) + " → CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "MAX DRAWDOWN", false); return; }
                if (Set.Rolling && Set.HedgeMode == "BREAK") SetHedgeLevel(sn, lastDir); else if ((Set.QuickHedge || Set.CandleHedge) && Set.HedgeMode != "NONE" && Set.HedgeMode != "SIGNAL" && Set.HedgeMode != "LADDER") { double spQ = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0; QuickLevel(sn, lastDir, spQ); CandleLevel(sn, lastDir, spQ); TrendLevel(sn, lastDir, spQ); }
                if (manualReset) { manualReset = false; SetHedgeLevel(sn, lastDir); } ApplyManual();
                if ((Set.LockOn || Set.RiskGuard) && Set.HedgeMode != "NONE" && Set.HedgeMode != "LADDER")
                {
                    double spL = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0, netL = sn.Pos.Sum(p => p.Dir * p.Lots);
                    if (Locked)
                    {   // 🔒 LOCKED: nothing happens inside the range; the break of its high / low unlocks
                        int bd = sn.Bid > LockHi + spL ? 1 : sn.Bid < LockLo - spL ? -1 : 0; if (bd == 0) return;
                        if (Set.RiskGuard)
                        {   // 🛟 only an OBVIOUS trend unlocks: far enough beyond the range AND the EMA 50 / 200 the same way
                            double avgR = sn.Fin.Count >= 5 ? sn.Fin.Take(14).Average(x => x.H - x.L) : 0;
                            bool far = bd > 0 ? sn.Bid >= LockHi + Set.GuardBreak * avgR : sn.Bid <= LockLo - Set.GuardBreak * avgR;
                            if (!far || (!double.IsNaN(Ema200) && TrendNow != bd)) return;
                            if (Set.PairOff && sn.Pos.Count > 2)
                            {   // PAIR-OFF: the best leg with the trend + the worst leg against it, closed together when they add up to ≥ $0
                                Func<Mt5Position, double> plP = p => p.Profit + p.Swap + p.Commission;
                                var win = sn.Pos.Where(p => p.Dir == bd).OrderByDescending(plP).FirstOrDefault(); var los = sn.Pos.Where(p => p.Dir == -bd).OrderBy(plP).FirstOrDefault();
                                if (win != null && los != null && plP(win) + plP(los) >= 0)
                                {
                                    Tell("🛟 RECOVERY — PAIR-OFF. WHY: the trend is " + (bd > 0 ? "UP" : "DOWN") + " (the lock range broken by " + Math.Abs(bd > 0 ? sn.Bid - LockHi : LockLo - sn.Bid).ToString("0.00", IC) + ", EMA 50 / 200 agree) → close " + win.Side + " " + win.Lots.ToString("0.##", IC) + " (" + M(plP(win)) + ") + " + los.Side + " " + los.Lots.ToString("0.##", IC) + " (" + M(plP(los)) + ") = " + M(plP(win) + plP(los)) + ": the basket gets smaller for free");
                                    Refused = ""; var idP = Ask("CLOSE", new object[] { los.Ticket, 0.0 }, "CLOSE #" + los.Ticket + " " + los.Side + " " + los.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + it.N + " 🛟 pair-off)");
                                    if (idP == null) { if (sugg) return; Note("⚠ pair-off not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
                                    pendId = idP; pendKind = "PAIR"; pendTicket = los.Ticket; pairNext = win.Ticket; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
                                    Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = los.Lots + win.Lots, Price = sn.Bid, Level = bd > 0 ? LockHi : LockLo, Why = "🛟 PAIR-OFF #" + los.Ticket + " + #" + win.Ticket, Basket = Basket });
                                    return;
                                }
                            }
                        }
                        bool tooBig = Set.RiskGuard && Set.GuardLift && GuardWhy(sn, it, Math.Abs(netL) + Math.Max(0.01, Set.NetAdvantage)) != "";   // 🛡 still dangerous: never add more on the way out — lift instead
                        Locked = false; lockFrom = it.AddOns + (Set.Unlock == "LIFT" || tooBig ? 0 : 1);
                        if (Set.Unlock == "LIFT" || tooBig)
                        {
                            string side = bd > 0 ? "SELL" : "BUY"; liftDir = bd;
                            Tell("🔓 UNLOCK — LIFT. WHY: the bid " + P(sn.Bid) + " broke the lock range " + (bd > 0 ? "HIGH " + P(LockHi) : "LOW " + P(LockLo)) + " → close every " + side + " (against the break); the " + (bd > 0 ? "BUYS" : "SELLS") + " keep running. The basket was " + M(Basket) + ".");
                            Record(sn, "UNLOCK", bd, 0); var idL = Ask("CLOSEALL", new object[] { Set.Symbol, side }, "CLOSE the " + side + "S " + Set.Symbol + " (🤖 #" + it.N + " unlock: lift)");
                            if (idL == null) { Locked = true; if (sugg) return; Note("⚠ lift not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
                            pendId = idL; pendKind = "CLOSESIDE"; pendSide = side; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
                            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = sn.Pos.Where(p => side == "BUY" ? p.Dir > 0 : p.Dir < 0).Sum(p => p.Lots), Price = sn.Bid, Level = bd > 0 ? LockHi : LockLo, Why = "🔓 UNLOCK — LIFT the " + side + "S", Basket = Basket });
                            return;
                        }
                        double ul = Math.Max(0.01, Math.Round(Math.Abs(netL) + Math.Max(0.01, Set.NetAdvantage), 2));
                        if (Set.RiskGuard && GuardWhy(sn, it, ul) != "")
                        {   // 🛡 TRIM: at the cap a LEAD unlock would make it bigger — close the SMALLEST leg against the break instead: the basket leans
                            // with the break AND gets smaller (one small realised loss, never a pile of new lots)
                            var leg = sn.Pos.Where(p => p.Dir == -bd && p.Time < lockAt.AddSeconds(-2)).OrderByDescending(p => p.Profit + p.Swap + p.Commission).FirstOrDefault() ?? sn.Pos.Where(p => p.Dir == -bd).OrderByDescending(p => p.Profit + p.Swap + p.Commission).FirstOrDefault();   // the CHEAPEST leg to close (its loss the smallest) — never the lock's own fresh leg   // never the lock's own fresh leg
                            if (leg != null)
                            {
                                Tell("🛡 UNLOCK — TRIM. WHY: the bid " + P(sn.Bid) + " broke the lock range " + (bd > 0 ? "HIGH " + P(LockHi) : "LOW " + P(LockLo)) + " and the basket is at the RISK GUARD cap → close the smallest " + leg.Side + " " + leg.Lots.ToString("0.##", IC) + " @ " + P(leg.Open) + " (#" + leg.Ticket + ", " + M(leg.Profit + leg.Swap + leg.Commission) + ") instead of adding — the " + (bd > 0 ? "buys" : "sells") + " lead, the basket gets smaller.");
                                Refused = ""; var idT = Ask("CLOSE", new object[] { leg.Ticket, 0.0 }, "CLOSE #" + leg.Ticket + " " + leg.Side + " " + leg.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + it.N + " 🛡 trim)");
                                if (idT == null) { Locked = true; if (sugg) return; Note("⚠ trim not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
                                guardQuiet = Now.AddMinutes(30); pendId = idT; pendKind = "TRIM"; pendTicket = leg.Ticket; liftDir = bd; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
                                Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = leg.Lots, Price = sn.Bid, Level = bd > 0 ? LockHi : LockLo, Why = "🛡 TRIM #" + leg.Ticket + " " + leg.Side, Basket = Basket });
                                return;
                            }
                        }
                        Tell("🔓 UNLOCK — LEAD. WHY: the bid " + P(sn.Bid) + " broke the lock range " + (bd > 0 ? "HIGH " + P(LockHi) : "LOW " + P(LockLo)) + " → " + (bd > 0 ? "BUY " : "SELL ") + ul.ToString("0.##", IC) + " so the " + (bd > 0 ? "buys" : "sells") + " lead by " + Set.NetAdvantage.ToString("0.##", IC) + ". The basket was " + M(Basket) + ".");
                        Order(sn, bd, ul, "🔓 UNLOCK " + (bd > 0 ? "BUY" : "SELL") + " — the break of the lock range " + (bd > 0 ? "high " + P(LockHi) : "low " + P(LockLo)), bd > 0 ? LockHi : LockLo); return;
                    }
                    if (Set.RiskGuard && Set.GuardMargin > 0 && sn.MarginLevel > 0 && sn.MarginLevel <= Set.GuardMargin / 2 && sn.Pos.Any(p => p.Dir > 0) && sn.Pos.Any(p => p.Dir < 0) && Math.Abs(netL) > 0.005) { GuardLock(sn, it, "EMERGENCY — the margin level is " + sn.MarginLevel.ToString("0", IC) + " % (≤ " + (Set.GuardMargin / 2).ToString("0", IC) + " %)"); return; }
                    if (Set.LockOn && it.AddOns - lockFrom >= Set.LockAfter && sn.Pos.Any(p => p.Dir > 0) && sn.Pos.Any(p => p.Dir < 0) && Math.Abs(netL) > 0.005)
                    {
                        double hiL = sn.Pos.Max(p => p.Open), loL = sn.Pos.Min(p => p.Open);
                        if (hiL - loL <= Set.LockRange)
                        {   // 🔒 CHOP: the hedges keep coming inside a small range → make buys = sells and wait for the real break
                            Locked = true; LockHi = Math.Max(hiL, sn.Ask); LockLo = Math.Min(loL, sn.Bid); int ld = netL > 0 ? -1 : 1; double ll = Math.Round(Math.Abs(netL), 2);
                            Tell("🔒 LOCK. WHY: " + it.AddOns + " hedges inside " + P(loL) + "–" + P(hiL) + " ($" + (hiL - loL).ToString("0.00", IC) + " ≤ $" + Set.LockRange.ToString("0.##", IC) + ") = chop → " + (ld > 0 ? "BUY " : "SELL ") + ll.ToString("0.##", IC) + " so buys = sells (net 0): the basket is frozen at about " + M(Basket) + ". It waits for the break of " + P(LockLo) + " / " + P(LockHi) + " (then " + (Set.Unlock == "LIFT" ? "the side against the break closes" : "the break's side gets " + Set.NetAdvantage.ToString("0.##", IC) + " more") + ").");
                            Order(sn, ld, ll, "🔒 LOCK " + (ld > 0 ? "BUY" : "SELL") + " — net 0 in the chop " + P(loL) + "–" + P(hiL), sn.Bid); return;
                        }
                    }
                }
                if (Set.HedgeMode == "SIGNAL" && it.AddOns < Set.MaxHedges)
                {   // SIGNAL HEDGE: an opposite engulfing at a 20-candle extreme on the last finished candle, the newest leg losing HedgeMoney or more
                    Signals(sn); double net = sn.Pos.Sum(p => p.Dir * p.Lots); int nd = Math.Sign(net); var newest = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault();
                    if (nd != 0 && hEngDir == -nd && sn.Fin.Count > 0 && hEngBar == sn.Fin[0].Time && newest != null && newest.Profit + newest.Swap + newest.Commission <= -Set.HedgeMoney)
                    {
                        double lots = Math.Round(Math.Abs(net) * Set.HedgeMult, 2); hEngDir = 0;
                        Tell("SIGNAL HEDGE " + (nd > 0 ? "SELL " : "BUY ") + lots.ToString("0.##", IC) + ". WHY: " + hEngStory + "; the newest leg " + newest.Side + " " + newest.Lots.ToString("0.##", IC) + " @ " + P(newest.Open) + " is " + M(newest.Profit + newest.Swap + newest.Commission) + " (≤ −$" + Set.HedgeMoney.ToString("0.##", IC) + "); net " + net.ToString("+0.##;−0.##", IC) + " lots × " + Set.HedgeMult.ToString("0.#", IC) + ".");
                        Order(sn, -nd, lots, "SIGNAL HEDGE " + (nd > 0 ? "SELL" : "BUY") + " — opposite engulfing", sn.Bid); return;
                    }
                }
                // ADD TO THE WINNER: the setup signals again in the leading side's direction (the last leg's)
                if (Set.AddToWinner && (Set.MaxWinnerAdds == 0 || it.WinnerAdds < Set.MaxWinnerAdds))
                {
                    Signals(sn); int wd; double wl; string ww, ws;
                    if (Signal(sn, lastDir, out wd, out wl, out ww, out ws) && wd == lastDir)
                    {
                        double wlots = wd > 0 ? Set.WinnerLots : Set.WinnerLotsSell; if (wlots <= 0) return;
                        Tell("ADD TO THE WINNER " + (wd > 0 ? "BUY " : "SELL ") + wlots.ToString("0.##", IC) + ". WHY: the leading side is " + (wd > 0 ? "BUY" : "SELL") + " and the setup " + Set.EntryMode + " signalled that way again: " + ws + ". The basket was " + M(Basket) + ".");
                        Order(sn, wd, wlots, "ADD TO THE WINNER — " + ww, wl); return;
                    }
                }
                NextHedgeLots = Set.HedgeMode == "NONE" || Set.HedgeMode == "LADDER" ? double.NaN : HedgeLots(sn);
                if (Set.ScaleOut && sn.Pos.Count > 0 && !Locked)
                {   // 📉 SCALE OUT: the net side won ScaleStep more → bank ScaleLots of its best leg (never below the lead)
                    double netS = sn.Pos.Sum(p => p.Dir * p.Lots); int nd = Math.Sign(Math.Round(netS, 6)); double px = nd > 0 ? sn.Bid : sn.Ask;
                    if (nd != 0 && double.IsNaN(scaleRef)) scaleRef = px;
                    if (nd != 0 && Math.Abs(netS) - Set.ScaleLots >= Math.Max(0.01, Set.NetAdvantage) - 1e-9 && nd * (px - scaleRef) >= Set.ScaleStep)
                    {
                        var best = sn.Pos.Where(p => p.Dir == nd).OrderByDescending(p => p.Profit + p.Swap + p.Commission).FirstOrDefault();
                        if (best != null && best.Profit > 0)
                        {
                            double cl = Math.Min(best.Lots, Set.ScaleLots); scaleRef = px;
                            Tell("📉 SCALE OUT " + cl.ToString("0.##", IC) + ". WHY: the " + (nd > 0 ? "buys" : "sells") + " lead by " + Math.Abs(netS).ToString("0.##", IC) + " and the price went " + Set.ScaleStep.ToString("0.##", IC) + " further their way → close " + cl.ToString("0.##", IC) + " of " + best.Side + " " + best.Lots.ToString("0.##", IC) + " @ " + P(best.Open) + " (" + M(best.Profit) + "): banked, the basket smaller as it wins");
                            double grossBefore = Gross(sn.Pos);   // BEFORE sending (a simulated close happens at once, in place)
                            Refused = ""; var idS = Ask("CLOSE", new object[] { best.Ticket, cl }, "CLOSE " + cl.ToString("0.##", IC) + " of #" + best.Ticket + " " + best.Side + " " + Set.Symbol + " (🤖 #" + it.N + " 📉 scale out)");
                            if (idS == null) { if (sugg) return; Note("⚠ scale out not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return; }
                            pendId = idS; pendKind = "SCALE"; pendTicket = best.Ticket; pendGross = grossBefore; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
                            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = cl, Price = px, Level = scaleRef, Why = "📉 SCALE OUT #" + best.Ticket, Basket = Basket });
                            return;
                        }
                    }
                }
                if (Set.HedgeMode == "LADDER")
                {   // 🪜 a new BUY only above the highest leg + step, a new SELL only below the lowest leg − step; nothing inside the range
                    if (sn.Pos.Count == 0) return;
                    double top = sn.Pos.Max(p => p.Open), bot = sn.Pos.Min(p => p.Open); LadderHi = top + Set.LadderStep; LadderLo = bot - Set.LadderStep; HedgeLevel = double.NaN;
                    if (Set.LadderMax > 0 && sn.Pos.Count >= Set.LadderMax) { if (ladderFull != it.N + "") { ladderFull = it.N + ""; Tell("🪜 LADDER FULL: " + sn.Pos.Count + " legs (max " + Set.LadderMax + ") — no more legs; the basket waits for its target " + M(Set.Target)); } return; }
                    double ask = !double.IsNaN(sn.Ask) ? sn.Ask : sn.Bid;
                    int netL = Math.Sign(Math.Round(sn.Pos.Sum(p => p.Dir * p.Lots), 6)); bool okB = Set.TrendFilter != "EMA" || TrendNow >= 0 || netL < 0, okS = Set.TrendFilter != "EMA" || TrendNow <= 0 || netL > 0;
                    if (ask >= LadderHi && okB) { Tell("🪜 LADDER BUY " + Set.EntryLots.ToString("0.##", IC) + ". WHY: the price " + P(ask) + " is above the basket's highest leg " + P(top) + " + " + Set.LadderStep.ToString("0.##", IC) + " = " + P(LadderHi) + "; basket " + M(Basket) + ", " + sn.Pos.Count + " legs."); Order(sn, 1, Set.EntryLots, "🪜 LADDER BUY — above the highest leg " + P(top) + " + " + Set.LadderStep.ToString("0.##", IC), LadderHi); return; }
                    if (sn.Bid <= LadderLo && okS) { Tell("🪜 LADDER SELL " + Set.EntryLots.ToString("0.##", IC) + ". WHY: the price " + P(sn.Bid) + " is below the basket's lowest leg " + P(bot) + " − " + Set.LadderStep.ToString("0.##", IC) + " = " + P(LadderLo) + "; basket " + M(Basket) + ", " + sn.Pos.Count + " legs."); Order(sn, -1, Set.EntryLots, "🪜 LADDER SELL — below the lowest leg " + P(bot) + " − " + Set.LadderStep.ToString("0.##", IC), LadderLo); return; }
                    return;
                }
                if (Set.HedgeMode != "NONE" && !double.IsNaN(HedgeLevel) && (lastDir > 0 ? sn.Bid <= HedgeLevel : sn.Bid >= HedgeLevel))
                {
                    if (Set.RiskGuard && !Locked) { var gw = GuardWhy(sn, it, HedgeLots(sn)); if (gw != "") { if (Set.GuardMode == "HOLD" || Now < guardQuiet) { if (gw != guardHeld) { guardHeld = gw; Tell("🛡 RISK GUARD → HOLD. WHY: " + gw + " → no more hedges; the basket keeps its small lead (" + sn.Pos.Sum(p => p.Dir * p.Lots).ToString("+0.##;−0.##", IC) + " lots) and waits for the target / rescue"); } return; } GuardLock(sn, it, gw); return; } }
                    var leg = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault();
                    double hl = HedgeLots(sn);
                    Tell("ADD-ON " + (lastDir > 0 ? "SELL " : "BUY ") + hl.ToString("0.##", IC) + ". WHY: the last leg was a " + (lastDir > 0 ? "BUY" : "SELL") + (leg != null ? " " + leg.Lots.ToString("0.##", IC) + " @ " + P(leg.Open) + " (" + leg.Time.ToString("HH:mm:ss", IC) + ")" : "") + "; the bid " + (lastDir > 0 ? "fell to " : "rose to ") + P(sn.Bid) + ", past the add-on line " + P(HedgeLevel)
                        + (Set.HedgeMode == "PRICE" ? " = its fill " + (lastDir > 0 ? "− " : "+ ") + Set.HedgePrice.ToString("0.#####", IC) : Set.HedgeMode == "MONEY" ? " = where the basket lost $" + Set.HedgeMoney.ToString("0.##", IC) + " since that leg" : " = " + HedgeWhy + (Set.Rolling ? " (following the newest candles)" : " (fixed when the leg opened)"))
                        + ". The basket was " + M(Basket) + " (worst so far " + M(Current.MaxDd) + ") with " + (Current.Buys + Current.Sells) + " leg(s) open.");
                    Order(sn, -lastDir, hl, (lastDir > 0 ? "ADD-ON SELL — " : "ADD-ON BUY — ") + (Set.HedgeMode == "PRICE" ? "the price moved " + Set.HedgePrice.ToString("0.#####", IC) + " against the last leg (" + HedgeLevel.ToString("0.#####", IC) + ")" : Set.HedgeMode == "MONEY" ? "−$" + Set.HedgeMoney.ToString("0.##", IC) + " since the last leg (" + HedgeLevel.ToString("0.#####", IC) + ")" : (Set.HedgeMode == "FVG" && Current != null && Current.AddOns == 0 ? HedgeWhy + " broke" : (lastDir > 0 ? "the recent low " : "the recent high ") + HedgeLevel.ToString("0.#####", IC) + " broke")), HedgeLevel);
                }
                return;
            }

            // waiting for the next iteration
            if (Set.MaxIterations > 0 && Iterations.Count(x => x.Done) >= Set.MaxIterations) { Stop("the " + Set.MaxIterations + " iterations are done"); return; }
            if (Phase == "PAUSE") { if (Now < PauseUntil) return; Phase = lastDir != 0 ? "AGAIN" : "WAIT BREAK"; }
            if (sn.Pos.Count > 0 && Set.TrackManual) { Adopt(sn); return; }
            if (sn.Pos.Count > 0) { Note("⚠ positions on " + Set.Symbol + " that are not the automation's — waiting until they are closed"); return; }
            Signals(sn);
            var gate = Gate(); int gcut = gate.IndexOf(" (New York "); string gkey = gcut > 0 ? gate.Substring(0, gcut) : gate; Blocked = gate; if (gkey != blockedKey) { blockedKey = gkey; Blocked = gate; if (gate != "") Tell("NOT TRADING NOW: " + gate + "."); else Tell("TRADING ALLOWED again" + (Set.Windows != "" ? " (inside " + Set.Windows + " New York)" : "") + "."); }
            if (gate != "") return;
            int dir = 0; string why = ""; double lvl = double.NaN; string story = "";
            if (Phase == "FIRST") { dir = Set.FirstDir == "BUY" ? 1 : -1; why = "FIRST " + Set.FirstDir + " (yours)"; story = "you chose " + Set.FirstDir + " as the first direction"; }
            else if (Phase == "AGAIN" && Set.NextStart == "WINNER") { dir = lastDir; why = "AFTER A WIN — the same direction again"; story = "the last iteration closed in profit " + (lastDir > 0 ? "long" : "short") + ", so " + (dir > 0 ? "BUY" : "SELL") + " again at once (NEXT ITERATION = after a win, the same direction)"; }
            else if (Phase == "AGAIN") { dir = lastDir; why = "AFTER THE PAUSE — the last basket's direction"; story = "the " + Set.PauseSec + " s pause after the last iteration ended; the last basket was " + (dir > 0 ? "long" : "short") + ", so " + (dir > 0 ? "BUY" : "SELL") + " again"; }
            else if (!Signal(sn, 0, out dir, out lvl, out why, out story)) return;
            if (Set.Smart && Phase != "FIRST")
            {   // 🧠 SMART: the same conditions in the history mostly got stuck → no new basket here
                var sk = SmartSkip(Context(sn, dir));
                if (sk != "")
                {
                    string ck = sk.Substring(0, Math.Max(0, sk.IndexOf(',')));
                    int seen; smartSeen.TryGetValue(ck, out seen); smartSeen[ck] = ++seen;
                    if (seen % 5 != 0) { var k2 = Now.ToString("yyyyMMddHH", IC) + dir + ck; if (k2 != smartSkipKey) { smartSkipKey = k2; Note("🧠 SMART: " + why + " skipped — " + sk); SmartSkips++; Version++; } return; }
                    Tell("🧠 SMART — EXPLORING: " + sk + ", but every 5th signal there still trades (so it can learn when that condition gets better).");
                }
            }
            if (Set.TrendFilter == "EMA" && Phase != "FIRST" && TrendNow != dir)
            {   // ✅ no confirmation: the trend is the other way (or none) — no new basket
                var k = Now.ToString("yyyyMMddHHmm", IC) + dir; if (k != trendSkip) { trendSkip = k; Note("✅ TREND CONFIRMATION: " + why + " skipped — the trend is " + (TrendNow > 0 ? "UP" : TrendNow < 0 ? "DOWN" : "NONE") + " (EMA 50 " + P(Ema50) + " / EMA 200 " + P(Ema200) + ")"); }
                return;
            }
            if (Phase != "FIRST" && Phase != "AGAIN" && !VwapOk(sn, dir)) { var sk = why + " — skipped: the VWAP filter " + Set.VwapFilter + " wants " + (Set.VwapFilter == "DISCOUNT" ? (dir > 0 ? "a buy UNDER" : "a sell OVER") : (dir > 0 ? "a buy OVER" : "a sell UNDER")) + " the VWAP " + P(Vwap) + " (bid " + P(sn.Bid) + ")"; if (sk != vwapSkip) { vwapSkip = sk; Tell("NO ENTRY: " + sk + "."); } return; }
            if (ManualManage) { Ask("ORDER", null, (dir > 0 ? "BUY " : "SELL ") + Set.EntryLots.ToString("0.##", IC) + " " + Set.Symbol + " (ENTRY — " + why + ")"); return; }
            closedByUs = false; Current = new AutoIteration { N = Iterations.Count + 1, Start = Now, FirstDir = dir > 0 ? "BUY" : "SELL", BalanceStart = sn.Balance }; Iterations.Add(Current); Phase = "IN ITERATION";
            Current.Ctx = Context(sn, dir); Tell("CONTEXT: " + Current.Ctx.Replace(";", " • "));
            Tell("ITERATION #" + Current.N + " — ENTRY " + (dir > 0 ? "BUY " : "SELL ") + Set.EntryLots.ToString("0.##", IC) + " (setup " + Set.EntryMode + "). WHY: " + story + ". Bid " + P(sn.Bid) + " / ask " + P(sn.Ask) + ", balance $" + sn.Balance.ToString("N2", IC) + ".");
            Order(sn, dir, Set.EntryLots, "ENTRY " + (dir > 0 ? "BUY" : "SELL") + " — " + why, lvl);
        }

        // the entry setup's signal now (only = 0 any side, +1 / −1 that side only — ADD TO THE WINNER): its direction, level, why and the story
        bool Signal(Snap sn, int only, out int dir, out double lvl, out string why, out string story)
        {
            dir = 0; lvl = double.NaN; why = ""; story = "";
            if (Set.EntryMode == "STRADDLE")
            {
                if (only != 0) return false; double hs = Math.Max(0.01, Set.LadderStep / 2);
                if (double.IsNaN(StraddleRef)) { StraddleRef = sn.Bid; Note("🪜 STRADDLE: a BUY above " + P(StraddleRef + hs) + " / a SELL below " + P(StraddleRef - hs) + " (" + hs.ToString("0.##", IC) + " each side of " + P(StraddleRef) + ")"); }
                if (sn.Bid >= StraddleRef + hs) dir = 1; else if (sn.Bid <= StraddleRef - hs) dir = -1; else return false;
                lvl = dir > 0 ? StraddleRef + hs : StraddleRef - hs; why = "🪜 STRADDLE " + (dir > 0 ? "BUY above " : "SELL below ") + P(lvl); story = "the straddle around " + P(StraddleRef) + ": the price went " + (dir > 0 ? "up" : "down") + " through " + P(lvl) + " first"; StraddleRef = double.NaN; return true;
            }
            if (Set.EntryMode == "FVG")
            {
                var z = fvgs.FirstOrDefault(f => (only == 0 || f.Dir == only) && (f.Dir > 0 ? sn.Bid <= f.Top && sn.Bid >= f.Bottom : sn.Bid >= f.Bottom && sn.Bid <= f.Top)); if (z == null) return false;
                fvgs.Remove(z); dir = z.Dir; lvl = z.Dir > 0 ? z.Top : z.Bottom; why = (dir > 0 ? "FVG BUY — the retest of the bullish gap " : "FVG SELL — the retest of the bearish gap ") + P(z.Bottom) + "–" + P(z.Top);
                story = "a " + (dir > 0 ? "bullish" : "bearish") + " FAIR VALUE GAP " + P(z.Bottom) + "–" + P(z.Top) + " formed by the three " + Tf.Name(Set.FvgTf) + " candles ending " + z.Time.ToString("HH:mm", IC) + " (" + (dir > 0 ? "the 3rd candle's low above the 1st candle's high" : "the 3rd candle's high under the 1st candle's low") + ") on " + Tf.Name(Set.FvgTf) + "; the bid came back into it (" + P(prevBid) + " → " + P(sn.Bid) + ")";
            }
            else if (Set.EntryMode == "AGGBRK")
            {
                if (AggDir == 0 || double.IsNaN(AggLevel) || double.IsNaN(prevBid) || (only != 0 && AggDir != only)) return false;
                if (Now >= AggBar.AddMinutes(2 * Set.Tf)) { AggDir = 0; AggLevel = double.NaN; return false; }   // only during the candle after the signal candle
                if (AggDir > 0 ? !(prevBid <= AggLevel && sn.Bid > AggLevel) : !(prevBid >= AggLevel && sn.Bid < AggLevel)) return false;
                dir = AggDir; lvl = AggLevel; why = "AGGRESSION BREAK " + (dir > 0 ? "BUY above " : "SELL below ") + P(lvl); story = SignalText + "; the bid broke it (" + P(prevBid) + " → " + P(sn.Bid) + ")"; AggDir = 0; AggLevel = double.NaN;
            }
            else if (Set.EntryMode == "ENGULF" || Set.EntryMode == "VWAPREJ")
            {
                if (engDir == 0 || (only != 0 && engDir != only) || sn.Fin.Count == 0 || sn.Fin[0].Time != engBar) return false;
                dir = engDir; engDir = 0; lvl = sn.Bid; why = Set.EntryMode == "VWAPREJ" ? "VWAP REJECTION " + (dir > 0 ? "BUY" : "SELL") : "ENGULFING " + (dir > 0 ? "BUY at a 20-candle low" : "SELL at a 20-candle high"); story = engStory;
            }
            else if (Set.EntryMode == "FVG50")
            {
                var z = fvgs.FirstOrDefault(f => (only == 0 || f.Dir == only) && !double.IsNaN(prevBid) && (f.Dir > 0 ? prevBid > (f.Top + f.Bottom) / 2 && sn.Bid <= (f.Top + f.Bottom) / 2 : prevBid < (f.Top + f.Bottom) / 2 && sn.Bid >= (f.Top + f.Bottom) / 2)); if (z == null) return false;
                fvgs.Remove(z); entryFvg = z; dir = z.Dir; lvl = (z.Top + z.Bottom) / 2; why = "FVG 50% " + (dir > 0 ? "BUY" : "SELL") + " — the middle " + P(lvl) + " of the gap " + P(z.Bottom) + "–" + P(z.Top);
                story = "a " + (dir > 0 ? "bullish" : "bearish") + " FAIR VALUE GAP " + P(z.Bottom) + "–" + P(z.Top) + " (" + Tf.Name(Set.FvgTf) + ", the three candles ending " + z.Time.ToString("HH:mm", IC) + "); the bid came back to its 50% " + P(lvl) + " (" + P(prevBid) + " → " + P(sn.Bid) + ")";
            }
            else if (Set.EntryMode == "FVGBREAK")
            {
                double hi = BuyLevel, lo = SellLevel; var lf = fvgs.LastOrDefault(); if (lf == null || double.IsNaN(prevBid)) return false;
                if (!double.IsNaN(hi) && lf.Dir > 0 && prevBid <= hi && sn.Bid > hi && only >= 0) { dir = 1; lvl = hi; } else if (!double.IsNaN(lo) && lf.Dir < 0 && prevBid >= lo && sn.Bid < lo && only <= 0) { dir = -1; lvl = lo; } else return false;
                why = "FVG + BREAK " + (dir > 0 ? "BUY — the high " : "SELL — the low ") + P(lvl) + " broke with a " + (dir > 0 ? "bullish" : "bearish") + " FVG";
                story = "the bid broke " + Candles(sn, dir > 0) + " (" + P(prevBid) + " → " + P(sn.Bid) + ") and the latest " + Tf.Name(Set.FvgTf) + " FAIR VALUE GAP is " + (dir > 0 ? "bullish " : "bearish ") + P(lf.Bottom) + "–" + P(lf.Top) + " (" + lf.Time.ToString("HH:mm", IC) + ") — the two agree";
            }
            else if (Set.EntryMode == "VWAP")
            {
                double v = Vwap; if (double.IsNaN(v) || double.IsNaN(prevBid)) return false;
                if (prevBid <= v && sn.Bid > v && only >= 0) dir = 1; else if (prevBid >= v && sn.Bid < v && only <= 0) dir = -1; else return false;
                lvl = v; why = "VWAP " + (dir > 0 ? "cross up " : "cross down ") + P(v);
                story = "the bid crossed the day's VWAP " + P(v) + " (" + Tf.Name(Set.Tf) + " typical price × tick volume since the server's midnight) " + (dir > 0 ? "upward" : "downward") + ": " + P(prevBid) + " → " + P(sn.Bid);
            }
            else if (Set.EntryMode == "FLOW")
            {
                if (flowDir == 0 || double.IsNaN(prevBid) || Now > flowUntil || (only != 0 && flowDir != only)) return false;
                if (flowDir > 0 && prevBid <= flowLevel && sn.Bid > flowLevel) dir = 1; else if (flowDir < 0 && prevBid >= flowLevel && sn.Bid < flowLevel) dir = -1; else return false;
                lvl = flowLevel; why = "ORDER FLOW " + (dir > 0 ? "BUY" : "SELL") + " — the surge candle " + flowBar.ToString("HH:mm", IC) + " broke"; story = SignalText + "; the bid broke its " + (dir > 0 ? "high " : "low ") + P(flowLevel) + " (" + P(prevBid) + " → " + P(sn.Bid) + ")"; flowDir = 0;
            }
            else
            {
                double hi = BuyLevel, lo = SellLevel; if (double.IsNaN(prevBid)) return false;   // each side on its own: no swing high must not block a sell
                // the user's bug (the entry "moved" to an older swing): a quick spike through the level BETWEEN two price updates — MT5's forming candle
                // has the high / low, our bid never showed it — used the swing up without an entry. The forming candle's own high / low passing it counts too.
                bool sameBar = !double.IsNaN(sn.FormH) && sn.FormT == prevFormT;
                bool upWick = !double.IsNaN(hi) && !double.IsNaN(sn.FormH) && sn.FormH > hi && prevBid <= hi && (sameBar ? prevFormH <= hi : sn.FormT > prevFormT);
                bool dnWick = !double.IsNaN(lo) && !double.IsNaN(sn.FormL) && sn.FormL < lo && prevBid >= lo && (sameBar ? prevFormL >= lo : sn.FormT > prevFormT);
                bool viaWick = false;
                if (!double.IsNaN(hi) && prevBid <= hi && (sn.Bid > hi || upWick) && only >= 0) { dir = 1; lvl = hi; viaWick = sn.Bid <= hi; why = "BREAK of the " + (Set.Swing ? "swing" : "recent") + " high " + hi.ToString("0.#####", IC); }
                else if (!double.IsNaN(lo) && prevBid >= lo && (sn.Bid < lo || dnWick) && only <= 0) { dir = -1; lvl = lo; viaWick = sn.Bid >= lo; why = "BREAK of the " + (Set.Swing ? "swing" : "recent") + " low " + lo.ToString("0.#####", IC); }
                if (dir == 0) return false;
                story = viaWick ? "the candle's " + (dir > 0 ? "high reached " + P(sn.FormH) + ", above " : "low reached " + P(sn.FormL) + ", below ") + P(lvl) + " = " + Candles(sn, dir > 0) + " between two price updates (bid now " + P(sn.Bid) + ") → the " + (dir > 0 ? "high" : "low") + " broke"
                    : "the bid moved from " + P(prevBid) + " to " + P(sn.Bid) + ", " + (dir > 0 ? "above " : "below ") + P(lvl) + " = " + Candles(sn, dir > 0) + " → the " + (dir > 0 ? "high" : "low") + " broke";
            }
            return true;
        }

        // the market when an iteration starts — saved with its result, so LEARN can tell which conditions win and which lose
        // 🧠 more of the market at the entry (user's request: "every single information — candles, timeframe, session, trend, lows, highs"):
        // the trend of the last 60 candles, where the price sits in the day's range, the day of the week, the last 3 candles' colours,
        // the signal candle's body, volatility, the EMA trend (when computed) and news nearby — the learning (🧠 SMART, LEARN) uses them
        string MoreContext(Snap sn, int dir)
        {
            var f = sn.Fin; var ny = Now.AddHours(Set.NyOffsetHours); var l = new List<string>();
            if (f.Count >= 30) { int n = Math.Min(60, f.Count); double ch = f[0].C - f[n - 1].C, rg = f.Take(n).Max(x => x.H) - f.Take(n).Min(x => x.L); string t = rg <= 0 ? "flat" : ch > 0.35 * rg ? "up" : ch < -0.35 * rg ? "down" : "flat"; l.Add("trend60=" + t); l.Add("withTrend=" + (t == "flat" ? "flat" : (t == "up") == (dir > 0) ? "yes" : "no")); }
            if (!double.IsNaN(sn.DayHi) && sn.DayHi > sn.DayLo && !double.IsNaN(sn.Bid)) { double pos = (sn.Bid - sn.DayLo) / (sn.DayHi - sn.DayLo); l.Add("dayPos=" + (pos >= 0.8 ? "top" : pos <= 0.2 ? "bottom" : "middle")); l.Add("dayRange=" + (sn.DayHi - sn.DayLo).ToString("0.##", IC)); }
            l.Add("dow=" + ny.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant());
            if (f.Count >= 3) l.Add("last3=" + string.Concat(f.Take(3).Reverse().Select(x => x.C > x.O ? "G" : x.C < x.O ? "R" : "D")));
            if (f.Count >= 15) { double atr = f.Take(14).Average(x => x.H - x.L), body = Math.Abs(f[0].C - f[0].O); l.Add("vol=" + (f[0].H - f[0].L > 1.5 * atr ? "high" : f[0].H - f[0].L < 0.6 * atr ? "low" : "normal")); l.Add("body=" + (body > 0.7 * (f[0].H - f[0].L) ? "strong" : body < 0.3 * (f[0].H - f[0].L) ? "weak" : "normal")); }
            if (!double.IsNaN(Ema50) && !double.IsNaN(Ema200)) l.Add("ema=" + (Ema50 > Ema200 ? "up" : "down"));
            if (NewsNear != null) { try { l.Add("news=" + (NewsNear(Now, 30, 30) != null ? "near" : "none")); } catch { } }
            l.Add("tf=" + Tf.Name(Set.Tf));
            return string.Join(";", l);
        }
        string Context(Snap sn, int dir)
        {
            var ny = Now.AddHours(Set.NyOffsetHours); int m = ny.Hour * 60 + ny.Minute; string ses = m >= 1140 || m < 180 ? "ASIA" : m < 480 ? "LONDON" : m < 720 ? "NY AM" : m < 960 ? "NY PM" : "LATE";
            var lf = fvgs.LastOrDefault(); double sh = sn.Fin.Count > 0 ? BuyShare(sn.Fin[0]) : 0.5; double rng = sn.Fin.Count >= 5 ? sn.Fin.Take(20).Average(x => x.H - x.L) : double.NaN;
            double hi = High(sn), lo = Low(sn), stopD = dir > 0 ? sn.Bid - lo : hi - sn.Bid;
            return string.Join(";", "setup=" + Set.EntryMode, "side=" + (dir > 0 ? "BUY" : "SELL"), "session=" + ses, "hour=" + ny.Hour.ToString("00", IC),
                "vwap=" + (double.IsNaN(Vwap) ? "none" : sn.Bid > Vwap ? "above" : "below"), "withVwap=" + (double.IsNaN(Vwap) ? "?" : ((sn.Bid > Vwap) == (dir > 0) ? "yes" : "no")),
                "fvg=" + (lf == null ? "none" : lf.Dir > 0 ? "bull" : "bear"), "withFvg=" + (lf == null ? "?" : (lf.Dir == dir ? "yes" : "no")),
                "flow=" + (sh >= 0.6 ? "buyers" : sh <= 0.4 ? "sellers" : "even"), "withFlow=" + ((dir > 0 ? sh >= 0.6 : sh <= 0.4) ? "yes" : "no"),
                "range=" + (double.IsNaN(rng) ? "?" : rng.ToString("0.###", IC)), "spread=" + (double.IsNaN(sn.Ask) ? "?" : (sn.Ask - sn.Bid).ToString("0.#####", IC)), "toOtherSide=" + (double.IsNaN(stopD) ? "?" : stopD.ToString("0.###", IC)), MoreContext(sn, dir));
        }
        // FVG zones and the FLOW surge candle: once per new finished candle
        void Signals(Snap sn)
        {
            if (Set.EntryMode.StartsWith("FVG") && sn.FinFvg.Count >= 3 && sn.FinFvg[0].Time != sigFvg)
            {
                sigFvg = sn.FinFvg[0].Time; Mt5Bar c1 = sn.FinFvg[2], c3 = sn.FinFvg[0];
                bool big = true; if (Set.FvgStrict) { var c2 = sn.FinFvg[1]; double avg = sn.FinFvg.Count >= 23 ? sn.FinFvg.Skip(2).Take(20).Average(x => x.H - x.L) : double.NaN; big = !double.IsNaN(avg) && Math.Abs(c2.C - c2.O) > avg && (c3.L > c1.H ? c2.C > c2.O : c2.C < c2.O); }
                if (!big) { }
                else if (c3.L > c1.H) { AddFvg(1, c1.H, c3.L, c1.L, c1.Time, c3.Time); SignalText = "bullish FVG " + P(c1.H) + "–" + P(c3.L) + " (" + c3.Time.ToString("HH:mm", IC) + ")"; }
                else if (c3.H < c1.L) { AddFvg(-1, c3.H, c1.L, c1.H, c1.Time, c3.Time); SignalText = "bearish FVG " + P(c3.H) + "–" + P(c1.L) + " (" + c3.Time.ToString("HH:mm", IC) + ")"; }
                fvgs.RemoveAll(f => (f.Dir > 0 ? c3.C < f.Bottom : c3.C > f.Top) || f.Time < c3.Time.AddMinutes(-(Set.FvgAge > 0 ? Set.FvgAge : 60 * Set.FvgTf))); while (fvgs.Count > 10) fvgs.RemoveAt(0);
            }
            if (Set.EntryMode.StartsWith("FVG") && Set.FvgAge > 0) fvgs.RemoveAll(f => f.Time.AddMinutes(Set.FvgTf + Set.FvgAge) < Now);   // the gap's 3rd candle closes FvgTf after its time
            if (sn.Fin.Count == 0 || sn.Fin[0].Time == sigBar) return; sigBar = sn.Fin[0].Time;
            if (Set.EntryMode == "VWAPREJ")
            {   // the day's candles oldest first, the VWAP at each one: 6 candles fully above it, then this one dips to it and closes back above → BUY (mirror → SELL)
                var day = sn.Fin.Where(x => x.Time.Date == sn.Fin[0].Time.Date).Reverse().ToList(); engDir = 0; engBar = sn.Fin[0].Time;
                if (day.Count >= 7)
                {
                    var vw = new double[day.Count]; double pv = 0, vv = 0; for (int i = 0; i < day.Count; i++) { double w = Math.Max(1, day[i].V); pv += (day[i].H + day[i].L + day[i].C) / 3 * w; vv += w; vw[i] = pv / vv; }
                    int n = day.Count - 1; bool above = true, below = true; for (int k = n - 6; k < n; k++) { if (day[k].L <= vw[k]) above = false; if (day[k].H >= vw[k]) below = false; }
                    var c = day[n]; engDir = above && c.L <= vw[n] && c.C > vw[n] ? 1 : below && c.H >= vw[n] && c.C < vw[n] ? -1 : 0;
                    if (engDir != 0) { engStory = "the " + Tf.Name(Set.Tf) + " candle " + c.Time.ToString("HH:mm", IC) + " " + (engDir > 0 ? "dipped to the day's VWAP " + P(vw[n]) + " (low " + P(c.L) + ") and closed back ABOVE it (" + P(c.C) + ") after 6 candles fully above it" : "rose to the day's VWAP " + P(vw[n]) + " (high " + P(c.H) + ") and closed back UNDER it (" + P(c.C) + ") after 6 candles fully under it") + " — a VWAP rejection"; SignalText = engStory; }
                }
            }
            if (Set.HedgeMode == "SIGNAL" && sn.Fin.Count >= 22)
            {
                var c = sn.Fin[0]; var pr = sn.Fin[1]; double ll = sn.Fin.Skip(2).Take(20).Min(x => x.L), hh = sn.Fin.Skip(2).Take(20).Max(x => x.H);
                bool bull = pr.C < pr.O && c.C > c.O && c.C >= pr.O && c.O <= pr.C, bear = pr.C > pr.O && c.C < c.O && c.C <= pr.O && c.O >= pr.C;
                hEngDir = bull && Math.Min(c.L, pr.L) <= ll ? 1 : bear && Math.Max(c.H, pr.H) >= hh ? -1 : 0; hEngBar = c.Time;
                if (hEngDir != 0) hEngStory = "the " + Tf.Name(Set.Tf) + " candle " + c.Time.ToString("HH:mm", IC) + " is a " + (hEngDir > 0 ? "BULLISH engulfing at the 20-candle low " + P(Math.Min(c.L, pr.L)) : "BEARISH engulfing at the 20-candle high " + P(Math.Max(c.H, pr.H)));
            }
            if (Set.EntryMode == "AGGBRK" && sn.Fin.Count >= Set.AggRun + 2)
            {   // AGGRESSION BREAK: AggRun+ candles of one colour, then the FIRST opposite close = the signal candle; the entry = its high (buy) / low (sell) broken during the next candle
                var c = sn.Fin[0]; bool green = c.C > c.O, red = c.C < c.O; int run = 0;
                for (int k = 1; k < sn.Fin.Count && run < 50; k++) { var x = sn.Fin[k]; if (green ? x.C < x.O : red ? x.C > x.O : false) run++; else break; }
                if (run >= Set.AggRun && (green || red)) { AggDir = green ? 1 : -1; AggLevel = green ? c.H : c.L; AggBar = c.Time; SignalText = run + " " + (green ? "red" : "green") + " " + Tf.Name(Set.Tf) + " candles, then the " + c.Time.ToString("HH:mm", IC) + " candle closed " + (green ? "GREEN" : "RED") + " (O " + P(c.O) + " C " + P(c.C) + ") — " + (green ? "BUY above its high " : "SELL below its low ") + P(AggLevel) + " during the next candle"; }
                else if (AggDir != 0 && Now >= AggBar.AddMinutes(2 * Set.Tf)) { AggDir = 0; AggLevel = double.NaN; }
            }
            if (Set.EntryMode == "ENGULF" && sn.Fin.Count >= 22)
            {
                var c = sn.Fin[0]; var pr = sn.Fin[1]; double ll = sn.Fin.Skip(2).Take(20).Min(x => x.L), hh = sn.Fin.Skip(2).Take(20).Max(x => x.H);
                bool bull = pr.C < pr.O && c.C > c.O && c.C >= pr.O && c.O <= pr.C, bear = pr.C > pr.O && c.C < c.O && c.C <= pr.O && c.O >= pr.C;
                engDir = bull && Math.Min(c.L, pr.L) <= ll ? 1 : bear && Math.Max(c.H, pr.H) >= hh ? -1 : 0; engBar = c.Time;
                if (engDir != 0) { engStory = "the " + Tf.Name(Set.Tf) + " candle " + c.Time.ToString("HH:mm", IC) + " (O " + P(c.O) + " C " + P(c.C) + ") ENGULFED the one before (O " + P(pr.O) + " C " + P(pr.C) + ") at the " + (engDir > 0 ? "lowest low " + P(Math.Min(c.L, pr.L)) + " of the last 20 candles (" + P(ll) + ")" : "highest high " + P(Math.Max(c.H, pr.H)) + " of the last 20 candles (" + P(hh) + ")") + " — a reversal"; SignalText = engStory; }
            }
            if (Set.EntryMode == "FLOW" && sn.Fin.Count >= 21)
            {
                var c = sn.Fin[0]; double avg = sn.Fin.Skip(1).Take(20).Average(x => (double)x.V), sh = BuyShare(c);
                if (avg > 0 && c.V >= Set.FlowVol * avg && (sh >= Set.FlowShare || sh <= 1 - Set.FlowShare))
                {
                    flowDir = sh >= Set.FlowShare ? 1 : -1; flowLevel = flowDir > 0 ? c.H : c.L; flowBar = c.Time; flowUntil = c.Time.AddMinutes(Set.Tf * 6);
                    SignalText = "the " + Tf.Name(Set.Tf) + " candle " + c.Time.ToString("HH:mm", IC) + " had " + (c.V / avg).ToString("0.0", IC) + "× the tick volume of the last 20 and " + (100 * (flowDir > 0 ? sh : 1 - sh)).ToString("0", IC) + "% " + (flowDir > 0 ? "buyers" : "sellers") + " (estimated from where it closed in its range — MT5 CFDs have no real order book)";
                }
            }
        }
        // why no new iteration may start now ("" = it may): the iterations a day, the New York windows, the news
        DateTime dayBlocked = DateTime.MinValue;
        DateTime NyDay() { return Now.AddHours(Set.NyOffsetHours).Date; }
        // today's (New York day) closed iterations + the open one vs the daily loss limit: the reason, or ""
        string DayLossHit(Snap sn, double openNow)
        {
            if (Set.DayLoss <= 0 && Set.DayLossPct <= 0) return "";
            var d = NyDay(); var today = Iterations.Where(x => x.Done && x.End.AddHours(Set.NyOffsetHours).Date == d).ToList();
            double closed = today.Sum(x => x.Result), dayNow = closed + openNow;
            double startBal = today.Count > 0 ? today.OrderBy(x => x.Start).First().BalanceStart : Current != null ? Current.BalanceStart : sn.Balance;
            double lim = Math.Min(Set.DayLoss > 0 ? Set.DayLoss : double.MaxValue, Set.DayLossPct > 0 ? Set.DayLossPct / 100 * Math.Max(1, startBal) : double.MaxValue);
            return dayNow <= -lim ? "today (New York " + d.ToString("ddd dd MMM", IC) + ") is at " + M(dayNow) + " (" + M(closed) + " closed + " + M(openNow) + " open) ≤ the daily loss limit −$" + lim.ToString("N2", IC) : "";
        }
        string smartSkipKey = ""; readonly Dictionary<string, int> smartSeen = new Dictionary<string, int>(); public int SmartSkips; public string SmartState = "";
        // a basket that went wrong: 3+ hedges, a loss, or deeper than 2 % of its balance
        // a CLEAN basket (user's request: "the best iterations: lowest drawdown, a fast take profit, clean price action"): it won, no hedge,
        // within 30 minutes and never deeper than 0.5 % of its balance
        public static bool Clean(AutoIteration x) { return x.Done && x.Result > 0 && x.AddOns == 0 && (x.End - x.Start).TotalMinutes <= 30 && (x.BalanceStart <= 0 || -x.MaxDd <= 0.005 * x.BalanceStart); }
        // 🏆 the conditions that give CLEAN baskets most often (n ≥ min): (condition, baskets, clean %, bad %, avg minutes)
        public List<Tuple<string, int, double, double, double>> BestSetups(int min)
        {
            var hist = Iterations.Where(x => x.Done && !string.IsNullOrEmpty(x.Ctx) && !x.Manual).ToList(); var res = new List<Tuple<string, int, double, double, double>>();
            foreach (var g in hist.SelectMany(x => x.Ctx.Split(';').Where(z => z.Contains("=") && !z.StartsWith("range=") && !z.StartsWith("spread=") && !z.StartsWith("toOtherSide=") && !z.StartsWith("dayRange=") && !z.StartsWith("setup=")).Select(z => Tuple.Create(z, x))).GroupBy(t => t.Item1))
            {
                var xs = g.Select(t => t.Item2).ToList(); if (xs.Count < min) continue;
                res.Add(Tuple.Create(g.Key.Replace("=", " = "), xs.Count, xs.Count(Clean) * 100.0 / xs.Count, xs.Count(Bad) * 100.0 / xs.Count, xs.Average(x => (x.End - x.Start).TotalMinutes)));
            }
            return res.OrderByDescending(t => t.Item3 - t.Item4).ToList();
        }
        static bool Bad(AutoIteration x) { return x.AddOns >= 3 || x.Result < 0 || (x.BalanceStart > 0 && -x.MaxDd > 0.02 * x.BalanceStart); }
        public string SmartSkip(string ctx)
        {
            var hist = Iterations.Where(x => x.Done && !string.IsNullOrEmpty(x.Ctx) && !x.Manual).ToList();
            if (hist.Count < 20) { SmartState = "🧠 learning: " + hist.Count + " / 20 baskets with their conditions"; return ""; }
            double all = hist.Count(Bad) / (double)hist.Count;
            Func<string, Dictionary<string, string>> kv = c => c.Split(';').Select(z => z.Split('=')).Where(z => z.Length == 2).GroupBy(z => z[0]).ToDictionary(g => g.Key, g => g.First()[1]);
            var now = kv(ctx); string worst = ""; double worstRate = 0; int worstN = 0, worstBad = 0;
            foreach (var key in new[] { "session", "hour", "withVwap", "withFlow", "withFvg", "side", "withTrend", "dayPos", "dow", "last3", "vol", "body", "news" })
            {
                string v; if (!now.TryGetValue(key, out v) || v == "?") continue;
                var same = hist.Where(x => { var d = kv(x.Ctx); string w; return d.TryGetValue(key, out w) && w == v; }).ToList();
                if (same.Count < SetSmartMin()) continue;
                int bad = same.Count(Bad); double rate = bad / (double)same.Count;
                if (rate >= 0.5 && rate >= all + 0.2 && rate > worstRate) { worstRate = rate; worst = key + " = " + v; worstN = same.Count; worstBad = bad; }
            }
            SmartState = "🧠 SMART: " + hist.Count + " baskets learned, " + (all * 100).ToString("0", IC) + " % went wrong overall";
            if (worst == "" && Set != null && Set.SmartBest && hist.Count >= 100)
            {   // 🏆 only where a condition of this entry gave clean baskets clearly more often than usual
                double cleanAll = hist.Count(Clean) / (double)hist.Count; string best = "";
                foreach (var key in now.Keys.Where(k => k != "range" && k != "spread" && k != "toOtherSide" && k != "dayRange" && k != "setup"))
                {
                    var same = hist.Where(x => { var d = kv(x.Ctx); string w; return d.TryGetValue(key, out w) && w == now[key]; }).ToList();
                    if (same.Count >= SetSmartMin() && same.Count(Clean) / (double)same.Count >= cleanAll + 0.15) { best = key + " = " + now[key] + " (" + (same.Count(Clean) * 100 / same.Count) + " % clean of " + same.Count + ")"; break; }
                }
                SmartState += best != "" ? " • 🏆 a best setup: " + best : "";
                if (best == "") return "🏆 ONLY THE BEST SETUPS: none of this entry's conditions gave clean baskets more often than the usual " + (cleanAll * 100).ToString("0", IC) + " %";
            }
            return worst == "" ? "" : "when " + worst + ", " + worstBad + " of " + worstN + " baskets went wrong (3+ hedges, a loss or deep) — " + (worstRate * 100).ToString("0", IC) + " % vs " + (all * 100).ToString("0", IC) + " % overall";
        }
        int SetSmartMin() { return Set != null ? Math.Max(3, Set.SmartMin) : 10; }
        string Gate()
        {
            if (dayBlocked == NyDay()) return "the DAILY LOSS limit was reached today (New York day) — no new iteration until tomorrow";
            if ((Set.DayLoss > 0 || Set.DayLossPct > 0) && Current == null) { var d0 = NyDay(); var closedT = Iterations.Where(x => x.Done && x.End.AddHours(Set.NyOffsetHours).Date == d0).ToList(); if (closedT.Count > 0) { double cl = closedT.Sum(x => x.Result), sb0 = closedT.OrderBy(x => x.Start).First().BalanceStart; double lim = Math.Min(Set.DayLoss > 0 ? Set.DayLoss : double.MaxValue, Set.DayLossPct > 0 ? Set.DayLossPct / 100 * Math.Max(1, sb0) : double.MaxValue); if (cl <= -lim) return "the DAILY LOSS limit −$" + lim.ToString("N2", IC) + " is reached today (" + M(cl) + " closed) — no new iteration until tomorrow (New York day)"; } }
            if (EndOfDay()) return "after the close time " + Set.CloseAtNy + " New York (no new iteration until the next day)";
            if (Set.PerDay > 0) { int today = Iterations.Count(x => x.Start.Date == Now.Date); if (today >= Set.PerDay) return "the " + Set.PerDay + " iteration" + (Set.PerDay == 1 ? "" : "s") + " of today are done (server day " + Now.ToString("dd MMM", IC) + ")"; }
            var w = Set.WindowList();
            if (w != null)
            {
                if (w.Count == 0) return "the trading times \"" + Set.Windows + "\" cannot be read (use 08:30-11:00, 13:00-16:00)";
                var ny = Now.AddHours(Set.NyOffsetHours); int m = ny.Hour * 60 + ny.Minute;
                if (!w.Any(x => x.Item1 <= x.Item2 ? m >= x.Item1 && m < x.Item2 : m >= x.Item1 || m < x.Item2)) return "outside the trading times " + Set.Windows + " (New York " + ny.ToString("HH:mm", IC) + ")";
            }
            if (Set.NewsMode != "IGNORE" && NewsNear != null)
            {
                var n = NewsNear(Now, Set.NewsBefore, Set.NewsAfter);
                if (Set.NewsMode == "AVOID" && n != null) return "high-impact news " + n + " (" + Set.NewsBefore + " min before … " + Set.NewsAfter + " min after)";
                if (Set.NewsMode == "ONLY" && n == null) return "no high-impact news within " + Set.NewsBefore + " min before … " + Set.NewsAfter + " min after (news-only mode)";
            }
            return "";
        }

        // the recording: one hedge / lock / unlock with the market at that moment
        void Record(Snap sn, string kind, int dir, double lots)
        {
            if (ManualManage) return;   // ✋ only suggested: nothing happened
            double atr = sn.Fin.Count > 0 ? sn.Fin.Take(14).Average(b => b.H - b.L) : 0;
            Hedges.Add(new HedgeEvent { Iter = Current != null ? Current.N : 0, Time = Now, Kind = kind, Dir = dir, Price = dir > 0 ? sn.Ask : sn.Bid, Lots = lots, RangeLo = sn.Pos.Count > 0 ? sn.Pos.Min(p => p.Open) : sn.Bid, RangeHi = sn.Pos.Count > 0 ? sn.Pos.Max(p => p.Open) : sn.Bid, Atr = atr, Basket = Basket, Before = Current != null ? Current.AddOns : 0, Ctx = Context(sn, dir) });
            if (Hedges.Count > 3000) Hedges.RemoveRange(0, 500);
        }
        DateTime marginWait = DateTime.MinValue; string marginNote = "";
        void Order(Snap sn, int dir, double lots, string why, double level)
        {
            if (sn.Pos.Count > 0 && Now < marginWait) return;   // ⚠ holding after a margin refusal
            var label = (dir > 0 ? "BUY " : "SELL ") + lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + Current.N + " " + why + ")";
            Refused = ""; var id = Ask("ORDER", new object[] { Set.Symbol, dir > 0 ? "BUY" : "SELL", lots, 0.0, 0.0, 0.0 }, label);
            if (id == null && sugg) return;
            if (id == null && sn.Pos.Count > 0 && IsBlowReason(Refused))
            {   // ⚠ not enough margin for THIS order while a basket is open (user's catch: "the whole automation stopped"): never stop — hold,
                // keep managing the basket (targets, rescue, locks, hard stops), try orders again in 2 minutes
                marginWait = Now.AddMinutes(2); var mk = "MARGIN" + Now.ToString("yyyyMMddHH", IC); if (mk != marginNote) { marginNote = mk; Tell("⚠ NOT ENOUGH MARGIN for " + label + " (" + Refused + ") → the automation HOLDS and keeps managing the basket (targets, rescue, locks, hard stops); new orders are tried again in 2 minutes."); }
                return;
            }
            if (id == null) { Stop("the order was not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return; }
            pendId = id; pendKind = "ORDER"; pendSent = DateTime.Now; pendOk = DateTime.MinValue; pendGross = Gross(sn.Pos) + lots; pendDirAfter = dir; pendLots = lots; pendWhy = why; pendLevel = level;
            Note("→ " + label);
        }
        // the order is in: count it, the next add-on level
        void Opened(Snap sn)
        {
            var it = Current; int dir = pendDirAfter; var px = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(p => p.Dir == dir);
            if (dir > 0) { it.Buys++; it.BuyLots += pendLots; } else { it.Sells++; it.SellLots += pendLots; }
            if (pendWhy.StartsWith("ADD TO THE WINNER")) it.WinnerAdds++; else if (it.Buys + it.Sells > 1) { it.AddOns++; Record(sn, pendWhy.StartsWith("🔒") ? "LOCK" : pendWhy.StartsWith("🔓") ? "UNLOCK" : "HEDGE", dir, pendLots); }
            it.MaxGross = Math.Max(it.MaxGross, Gross(sn.Pos));
            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = dir > 0 ? "BUY" : "SELL", Lots = pendLots, Price = px != null ? px.Open : dir > 0 ? sn.Ask : sn.Bid, Level = pendLevel, Why = pendWhy, Basket = Basket });
            lastDir = dir; knownGross = Gross(sn.Pos); quickBest = double.NaN; scaleRef = double.NaN; ManualHedge = double.NaN; ManualNoHedge = false; SetHedgeLevel(sn, dir);
            Tell("FILLED " + (dir > 0 ? "BUY " : "SELL ") + pendLots.ToString("0.##", IC) + " @ " + P(px != null ? px.Open : dir > 0 ? sn.Ask : sn.Bid) + ". " + Plan(sn)); Version++;
        }
        // the add-on line: the recent low under a buy / the recent high over a sell (at least the spread away from the price)
        void SetHedgeLevel(Snap sn, int dir)
        {
            if (Set.HedgeMode == "NONE") { HedgeLevel = double.NaN; HedgeWhy = "no add-ons (no hedge)"; return; }
            if (Set.HedgeMode == "LADDER") { HedgeLevel = double.NaN; HedgeWhy = "🪜 the ladder: a BUY above the highest leg + " + Set.LadderStep.ToString("0.##", IC) + ", a SELL below the lowest − " + Set.LadderStep.ToString("0.##", IC); return; }
            if (Set.HedgeMode == "FVG" && entryFvg != null && Current != null && Current.AddOns == 0 && entryFvg.Dir == dir)
            { HedgeLevel = entryFvg.First; HedgeWhy = "the " + (dir > 0 ? "LOW" : "HIGH") + " " + P(entryFvg.First) + " of the first candle of the " + (entryFvg.Count > 1 ? entryFvg.Count + " stacked gaps" : "gap") + " (" + entryFvg.From.ToString("HH:mm", IC) + ")"; return; }
            if (Set.HedgeMode == "SIGNAL") { HedgeLevel = double.NaN; HedgeWhy = "an opposite engulfing while the newest leg is down $" + Set.HedgeMoney.ToString("0.##", IC); return; }
            double sp = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0;
            // PRICE: a fixed move from the last leg's fill; MONEY: the move that loses HedgeMoney $ on what the basket holds now (net lots × $ per price)
            if (Set.HedgeMode == "PRICE" || Set.HedgeMode == "MONEY")
            {
                var last = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); double from = last != null ? (dir > 0 ? last.Open - sp : last.Open + sp) : sn.Bid;   // the bid when the leg opened
                double net = Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)); double dist = Set.HedgeMode == "PRICE" ? Set.HedgePrice : sn.PerPrice > 0 && net > 1e-9 ? Set.HedgeMoney / (net * sn.PerPrice) : double.NaN;
                HedgeLevel = double.IsNaN(dist) ? double.NaN : dir > 0 ? from - Math.Max(dist, sp) : from + Math.Max(dist, sp); RangeOnlyLevel(sn, dir, sp); CapLevel(sn, dir, sp); QuickLevel(sn, dir, sp); CandleLevel(sn, dir, sp); TrendLevel(sn, dir, sp); ApplyManual(); return;
            }
            double lv = dir > 0 ? Low(sn) : High(sn);
            if (double.IsNaN(lv) && Set.HedgeCap > 0 && sn.Pos.Count > 0) { HedgeLevel = double.NaN; HedgeWhy = ""; CapLevel(sn, dir, sp); QuickLevel(sn, dir, sp); CandleLevel(sn, dir, sp); return; }   // no swing (a straight trend): the max distance is the hedge
            if (double.IsNaN(lv)) { HedgeLevel = double.NaN; HedgeWhy = ""; return; }
            HedgeLevel = Set.Rolling ? lv : dir > 0 ? Math.Min(lv, sn.Bid - sp) : Math.Max(lv, sn.Bid + sp);
            HedgeWhy = Candles(sn, dir < 0) + (Math.Abs(HedgeLevel - lv) > 1e-9 ? ", moved to " + P(HedgeLevel) + " (at least the spread from the price)" : "");
            RangeOnlyLevel(sn, dir, sp); CapLevel(sn, dir, sp); QuickLevel(sn, dir, sp); CandleLevel(sn, dir, sp); TrendLevel(sn, dir, sp); ApplyManual();
        }
        void TrendLevel(Snap sn, int dir, double sp)
        {
            if (!Set.TrendHedge || double.IsNaN(HedgeLevel) || sn.Pos.Count == 0 || double.IsNaN(Ema200)) return;
            double net = sn.Pos.Sum(p => p.Dir * p.Lots); if (Math.Sign(Math.Round(net, 6)) != dir || TrendNow != dir) return;   // the trend is WITH the basket's net side
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); if (leg == null) return;
            double normal = Math.Abs(leg.Open - HedgeLevel), edge = dir > 0 ? sn.Pos.Min(p => p.Open) - sp : sn.Pos.Max(p => p.Open) + sp, deep = dir > 0 ? leg.Open - Set.TrendHedgeMult * normal : leg.Open + Set.TrendHedgeMult * normal;
            double was = HedgeLevel; HedgeLevel = dir > 0 ? Math.Min(HedgeLevel, Math.Min(edge, deep)) : Math.Max(HedgeLevel, Math.Max(edge, deep));
            if (Math.Abs(HedgeLevel - was) > 1e-9) HedgeWhy += " → 🧭 the trend is " + (dir > 0 ? "UP" : "DOWN") + " with the basket: the hedge waits deeper, at " + P(HedgeLevel) + " (beyond the basket's " + (dir > 0 ? "lowest" : "highest") + " leg, ≥ " + Set.TrendHedgeMult.ToString("0.##", IC) + "× the normal distance)";
        }
        void CapLevel(Snap sn, int dir, double sp)
        {
            if (Set.HedgeCap <= 0 || sn.Pos.Count == 0) return;
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault();
            double from = Set.RangeOnly ? (dir > 0 ? sn.Pos.Min(p => p.Open) : sn.Pos.Max(p => p.Open)) : leg != null ? leg.Open : (dir > 0 ? sn.Pos.Min(p => p.Open) : sn.Pos.Max(p => p.Open));
            double capD = Set.HedgeCap; if (Set.RiskGuard && sn.Fin.Count >= 5) capD = Math.Max(capD, 1.5 * sn.Fin.Take(14).Average(x => x.H - x.L));   // 🛡 wider when the market swings more
            double cap = dir > 0 ? from - capD : from + capD;
            if (double.IsNaN(HedgeLevel) || (dir > 0 ? HedgeLevel < cap : HedgeLevel > cap))
            {
                bool none = double.IsNaN(HedgeLevel); HedgeLevel = cap;
                HedgeWhy = (none ? "no swing " + (dir > 0 ? "low" : "high") + " to hedge at (a straight move)" : "the swing was too far") + " → 🛡 MAX HEDGE DISTANCE: " + Set.HedgeCap.ToString("0.##", IC) + " from " + P(from) + " = " + P(cap) + ((dir > 0 ? sn.Bid <= cap : sn.Bid >= cap) ? " — the price is already past it: hedge NOW" : "");
            }
        }
        // 🛡 why the next hedge is dangerous ("" = it is fine): too big, low margin, chop
        // 🚨 THE DANGER METER (0 calm … 100 critical), every step of an open basket: the loss vs the balance (open + realised), the margin
        // level, the chop (hedges in the last hour), the time in the trade, the size vs the guard's cap — and the history: how the baskets
        // that went at least this deep ended (few recovered = more danger). Shown on the chart; the DANGER EXIT uses it.
        public double BeLockPx = double.NaN; public int Danger, DangerPeak; public string DangerWhy = "", DangerPeakWhy = "", Similar = "";   // the peak = the worst of this basket (it stays after the basket recovers)
        void DangerNow(Snap sn, AutoIteration it)
        {
            double bal = Math.Max(1, it.BalanceStart), loss = -(Basket + sn.Balance - it.BalanceStart), ddPct = Math.Max(0, loss) / bal * 100;
            double sDd = Math.Min(40, ddPct * 4), sMl = sn.MarginLevel > 0 ? Math.Max(0, Math.Min(1, (2000 - sn.MarginLevel) / 1600)) * 20 : 0;
            int chop = Hedges.Count(h => h.Iter == it.N && (Now - h.Time).TotalMinutes <= 60); double sCh = Math.Min(15, chop * 5);
            double hrs = Math.Max(0, (Now - it.Start).TotalHours), sT = Math.Min(10, hrs * 2);
            double cap = (Set.GuardLots > 0 ? Set.GuardLots : 1.0) * Math.Max(1, sn.Balance) / 5000.0, gross = sn.Pos.Sum(p => p.Lots), sSz = Math.Min(10, gross / Math.Max(0.01, cap) * 10);
            double sH = 0; Similar = "";
            if (loss > 0)
            {
                var sim = Iterations.Where(x => x.Done && x != it && x.BalanceStart > 0 && -x.MaxDd / x.BalanceStart * 100 >= ddPct * 0.9).ToList();
                if (sim.Count > 0)
                {
                    int ok = sim.Count(x => x.Result >= 0); double mins = sim.Average(x => (x.End - x.Start).TotalMinutes);
                    Similar = ok + " of " + sim.Count + " similar baskets (≥ " + ddPct.ToString("0.0", IC) + " % down) recovered, in " + Dur(TimeSpan.FromMinutes(mins)) + " on average";
                    if (sim.Count >= 3) sH = (1 - ok / (double)sim.Count) * 25;
                }
                else Similar = "no basket in the history went this deep (" + ddPct.ToString("0.0", IC) + " %)";
                if (sim.Count < 3 && ddPct >= 3) sH = 10;   // deeper than anything seen: unknown ground
            }
            Danger = (int)Math.Round(Math.Min(100, sDd + sMl + sCh + sT + sSz + sH));
            var w = new List<string>();
            if (sDd >= 8) w.Add("down " + ddPct.ToString("0.0", IC) + " % of the balance"); if (sMl >= 5) w.Add("margin " + sn.MarginLevel.ToString("0", IC) + " %");
            if (chop > 0) w.Add(chop + " hedge" + (chop == 1 ? "" : "s") + " in the last hour"); if (hrs >= 1) w.Add(hrs.ToString("0.0", IC) + " h in the trade");
            if (sSz >= 5) w.Add(gross.ToString("0.##", IC) + " lots of the " + cap.ToString("0.##", IC) + " cap"); if (sH >= 8) w.Add("similar baskets often did not recover");
            DangerWhy = w.Count == 0 ? "calm" : string.Join(", ", w);
            if (Danger > DangerPeak) { DangerPeak = Danger; DangerPeakWhy = DangerWhy; }
        }
        string GuardWhy(Snap sn, AutoIteration it, double nextLots)
        {
            double gross = sn.Pos.Sum(p => p.Lots), cap = Set.GuardLots * Math.Max(1, sn.Balance) / 5000.0;
            if (gross + nextLots > cap + 1e-9) return "TOO BIG — " + (gross + nextLots).ToString("0.##", IC) + " lots would pass the cap " + cap.ToString("0.##", IC) + " (" + Set.GuardLots.ToString("0.##", IC) + " per $5,000)";
            if (Set.GuardMargin > 0 && sn.MarginLevel > 0 && sn.MarginLevel < Set.GuardMargin) return "MARGIN — the margin level is " + sn.MarginLevel.ToString("0", IC) + " % (< " + Set.GuardMargin.ToString("0", IC) + " %)";
            if (Set.GuardChop > 0 && Hedges.Count(h => h.Iter == it.N && (Now - h.Time).TotalMinutes <= 60) >= Set.GuardChop) return "CHOP — " + Set.GuardChop + " hedges in the last hour";
            return "";
        }
        // 🛡 instead of the hedge: buys = sells (net 0), frozen until the range breaks (the LOCK's own unlock)
        void GuardLock(Snap sn, AutoIteration it, string why)
        {
            double netL = sn.Pos.Sum(p => p.Dir * p.Lots); if (Math.Abs(netL) < 0.005) { Locked = true; LockHi = Math.Max(sn.Pos.Max(p => p.Open), sn.Ask); LockLo = Math.Min(sn.Pos.Min(p => p.Open), sn.Bid); return; }
            Locked = true; lockAt = Now; LockHi = Math.Max(sn.Pos.Max(p => p.Open), sn.Ask); LockLo = Math.Min(sn.Pos.Min(p => p.Open), sn.Bid); int ld = netL > 0 ? -1 : 1; double ll = Math.Round(Math.Abs(netL), 2);
            Tell("🛡 RISK GUARD → LOCK. WHY: " + why + " → " + (ld > 0 ? "BUY " : "SELL ") + ll.ToString("0.##", IC) + " so buys = sells (net 0): the basket is frozen at about " + M(Basket) + " and waits for the break of " + P(LockLo) + "–" + P(LockHi) + " (no more hedges inside)");
            Order(sn, ld, ll, "🛡 RISK GUARD LOCK " + (ld > 0 ? "BUY" : "SELL") + " — " + why.Split('—')[0].Trim(), sn.Bid);
        }
        void CandleLevel(Snap sn, int dir, double sp)
        {
            if (!Set.CandleHedge || sn.Fin.Count == 0 || sn.Pos.Count == 0 || double.IsNaN(sn.Bid) || Set.HedgeMode == "NONE" || Set.HedgeMode == "SIGNAL" || Set.HedgeMode == "LADDER") return;
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); if (leg == null) return;
            var c = sn.Fin.FirstOrDefault(x => x.Time <= leg.Time && x.Time.AddMinutes(Set.Tf) > leg.Time);   // the entry candle, once it has finished
            bool done = c != null; if (c == null) c = sn.Fin[0];                                                  // still forming: the candle before it (the breakout candle)
            double q = dir > 0 ? c.L - sp : c.H + sp;
            if (Set.RangeOnly && sn.Pos.Count >= 2) { double edgeR = dir > 0 ? sn.Pos.Min(p => p.Open) - sp : sn.Pos.Max(p => p.Open) + sp; q = dir > 0 ? Math.Min(q, edgeR) : Math.Max(q, edgeR); }   // never inside the basket's range
            if (dir > 0 ? q >= leg.Open : q <= leg.Open) return;                                                  // never on the wrong side of the leg itself
            if (!double.IsNaN(HedgeLevel) && (dir > 0 ? q <= HedgeLevel : q >= HedgeLevel)) return;               // only ever CLOSER to the price
            HedgeLevel = q; HedgeWhy = "🕯 ENTRY CANDLE: the hedge sits beyond the " + (dir > 0 ? "LOW " + P(c.L) : "HIGH " + P(c.H)) + " of the " + (done ? "candle the " + (dir > 0 ? "buy" : "sell") + " filled in" : "breakout candle before the entry") + " (" + c.Time.ToString("HH:mm", IC) + ") — not the far swing";
        }
        void QuickLevel(Snap sn, int dir, double sp)
        {
            if (!Set.QuickHedge || sn.Fin.Count == 0 || sn.Pos.Count == 0 || double.IsNaN(sn.Bid)) return;
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); if (leg == null) return;
            var since = sn.Fin.Where(x => x.Time.AddMinutes(Set.Tf) > leg.Time).ToList();
            double ext = dir > 0 ? Math.Max(sn.Bid, since.Count > 0 ? since.Max(x => x.H) : sn.Bid) : Math.Min(sn.Bid, since.Count > 0 ? since.Min(x => x.L) : sn.Bid);
            double edge = dir > 0 ? sn.Pos.Max(p => p.Open) : sn.Pos.Min(p => p.Open), run = dir > 0 ? ext - edge : edge - ext;
            if (run < Set.QuickAfter) return;
            var last = sn.Fin[0]; double q = dir > 0 ? Math.Min(last.L, sn.Bid - sp) : Math.Max(last.H, sn.Bid + sp);
            if (!double.IsNaN(quickBest)) q = dir > 0 ? Math.Max(q, quickBest) : Math.Min(q, quickBest);   // LOCKED IN: once it trailed up / down it never moves back away from the price (user's catch)
            quickBest = q;
            if (Set.RangeOnly && sn.Pos.Count >= 2) { double edgeR = dir > 0 ? sn.Pos.Min(p => p.Open) - sp : sn.Pos.Max(p => p.Open) + sp; q = dir > 0 ? Math.Min(q, edgeR) : Math.Max(q, edgeR); }   // never INSIDE the basket's range (range-only wins: no hedging in its own chop)
            if (!double.IsNaN(HedgeLevel) && (dir > 0 ? q <= HedgeLevel : q >= HedgeLevel)) return;   // only ever CLOSER to the price
            HedgeLevel = q; HedgeWhy = "⚡ QUICK HEDGE: the price ran " + run.ToString("0.00", IC) + " beyond the basket's best leg " + P(edge) + " (≥ " + Set.QuickAfter.ToString("0.##", IC) + ") → the hedge sits at the last candle's " + (dir > 0 ? "LOW " : "HIGH ") + P(q) + " (" + last.Time.ToString("HH:mm", IC) + "): the first candle that breaks it hedges";
        }
        void RangeOnlyLevel(Snap sn, int dir, double sp)
        {
            if (!Set.RangeOnly || sn.Pos.Count < 2 || double.IsNaN(HedgeLevel)) return;
            // ANTI-CHOP: never inside the basket's own range — a hedge SELL only under its lowest leg, a hedge BUY only over its highest
            double lo = sn.Pos.Min(p => p.Open), hi = sn.Pos.Max(p => p.Open), was = HedgeLevel;
            HedgeLevel = dir > 0 ? Math.Min(HedgeLevel, lo - sp) : Math.Max(HedgeLevel, hi + sp);
            if (Math.Abs(HedgeLevel - was) > 1e-9) HedgeWhy += " → moved BEYOND the basket's range " + P(lo) + "–" + P(hi) + " to " + P(HedgeLevel) + " (range-only hedging)";
        }
        void CloseOne(Mt5Position p)
        {
            Refused = ""; var id = Ask("CLOSE", new object[] { p.Ticket, 0.0 }, "CLOSE #" + p.Ticket + " " + p.Side + " " + p.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + Current.N + " leg target)");
            if (id == null) { if (sugg) return; Note("⚠ close of #" + p.Ticket + " not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
            pendId = id; pendKind = "CLOSE1"; pendTicket = p.Ticket; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
            Trades.Add(new AutoTrade { Iter = Current.N, Time = Now, Side = "CLOSE", Lots = p.Lots, Price = p.Current, Level = double.NaN, Why = "LEG TARGET #" + p.Ticket + " " + p.Side, Basket = Basket });
        }
        void CloseAll(Snap sn, string reason, bool again)
        {
            Current.Estimate = Basket; Phase = "CLOSING"; closeSeen = DateTime.Now;
            Refused = ""; var id = Ask("CLOSEALL", new object[] { Set.Symbol, "ALL" }, "CLOSE ALL " + Set.Symbol + " (🤖 #" + Current.N + " " + reason + ", basket " + Basket.ToString("+0.00;−0.00", IC) + ")");
            if (id == null) { if (sugg) return; Note("⚠ close all not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
            pendId = id; pendKind = "CLOSE"; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
            if (!again) Note("■ " + reason + " — closing the basket at " + Basket.ToString("+0.00;−0.00", IC));
        }
        void Finish(Snap sn)
        {
            var it = Current; it.End = Now; it.BalanceEnd = sn.Balance; it.CloseBid = sn.Bid; it.Result = Math.Abs(sn.Balance - it.BalanceStart) > 1e-9 || it.Estimate == 0 ? sn.Balance - it.BalanceStart : it.Estimate;
            if (it.Reason == "") it.Reason = "CLOSED";
            Tell("CLOSED — iteration #" + it.N + " " + it.Reason + " " + M(it.Result) + " (balance $" + it.BalanceStart.ToString("N2", IC) + " → $" + sn.Balance.ToString("N2", IC) + "), " + it.Buys + " buys / " + it.Sells + " sells, " + it.AddOns + " add-ons, most lots open " + it.MaxGross.ToString("0.##", IC) + ", worst drawdown " + M(it.MaxDd) + ", " + (it.End - it.Start).ToString(@"hh\:mm\:ss", IC) + ".");
            Note((it.Result >= 0 ? "✔" : "✖") + " iteration #" + it.N + " " + it.Reason + " " + it.Result.ToString("+0.00;−0.00", IC) + " • " + it.Buys + " buys / " + it.Sells + " sells • " + it.AddOns + " add-ons • max drawdown " + it.MaxDd.ToString("0.00", IC));
            int dir = it.BuyLots > it.SellLots + 1e-9 ? 1 : it.SellLots > it.BuyLots + 1e-9 ? -1 : lastDir; lastDir = dir;
            if (it.Reason == "TARGET" && Math.Abs(it.BuyLots - it.SellLots) > 1e-9) { it.AfterMax = it.Result; it.AfterDone = false; }   // follow it after the target
            BeLockPx = double.NaN; Current = null; Danger = 0; DangerPeak = 0; DangerPeakWhy = ""; DangerWhy = ""; Similar = ""; HedgeLevel = double.NaN; quickBest = double.NaN; scaleRef = double.NaN; NextHedgeLots = double.NaN; ManualHedge = double.NaN; ManualNoHedge = false; guardQuiet = DateTime.MinValue; guardHeld = ""; Locked = false; lockFrom = 0; Version++; if (OnOpenState != null) OnOpenState("");   // closed: nothing to carry over
            if (Next != null) ApplyNext();
            if (it.Reason == "DAY LOSS") dayBlocked = NyDay();
            if (it.Reason == "MAX DRAWDOWN" && Set.AfterDrawdown == "STOP") { Stop("the max drawdown $" + Set.MaxDrawdown.ToString("0.##", IC) + " was hit"); return; }
            if (it.Reason == "CLOSED OUTSIDE") { Stop("the basket was closed outside the automation"); return; }
            if (Set.MaxIterations > 0 && Iterations.Count(x => x.Done) >= Set.MaxIterations) { Stop("the " + Set.MaxIterations + " iterations are done"); return; }
            Wait(sn, Set.NextStart == "PAUSE" ? "PAUSE" : Set.NextStart == "WINNER" && it.Result > 0 && lastDir != 0 ? "AGAIN" : "BREAK");
        }
        void Wait(Snap sn, string how)
        {
            if (how == "FIRST") { Phase = "FIRST"; return; }
            if (how == "AGAIN") { Phase = "AGAIN"; Note("↻ the last iteration WON — the same direction (" + (lastDir > 0 ? "BUY" : "SELL") + ") again at once"); return; }
            if (how == "PAUSE") { Phase = "PAUSE"; PauseUntil = Now.AddSeconds(Math.Max(0, Set.PauseSec)); Note("⏸ next iteration after " + Set.PauseSec + " s (" + PauseUntil.ToString("HH:mm:ss", IC) + ")"); return; }
            Phase = "WAIT BREAK"; Note("⏳ waiting for the next " + WaitingFor());
        }

        // a new gap the same way as the gap of the candle before = STACKED: one zone from the first gap's far side to the newest gap's
        // near side (the 50% entry is the middle of all of them), the hedge level stays the FIRST gap's first candle
        void AddFvg(int dir, double bottom, double top, double first, DateTime from, DateTime time)
        {
            var last = fvgs.LastOrDefault();
            if (Set.FvgStack && last != null && last.Dir == dir && time <= last.Time.AddMinutes(Set.FvgTf * 2))
            { if (dir > 0) last.Top = Math.Max(last.Top, top); else last.Bottom = Math.Min(last.Bottom, bottom); last.Time = time; last.Count++; SignalText = ""; return; }
            fvgs.Add(new Fvg { Dir = dir, Bottom = bottom, Top = top, First = first, From = from, Time = time });
        }
        // the hedge's lots: FIXED = the add-on lots; RECOVER = enough that RecoverMove of price the hedge's way brings the whole basket to the
        // target (net after = (target − basket) / ($ per price × the move)), never above HedgeMaxLots, rounded up to 0.01
        double HedgeLots(Snap sn)
        {
            if (Set.HedgeSize == "ADVANTAGE") { double nnet = Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)); return Math.Max(0.01, Math.Round(nnet + Math.Max(0.01, Set.NetAdvantage), 2)); }
            // STEP LEAD (user's request): the lead GROWS by LEAD BY with every hedge — the 1st hedge leads by 0.1, the 2nd by 0.2, the 3rd by 0.3 …
            if (Set.HedgeSize == "STEPLEAD") { double nnet = Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)); int k = (Current != null ? Current.AddOns : 0) + 1; if (Set.RiskGuard) k = Math.Min(k, Math.Max(1, Set.GuardMaxLead)); return Math.Max(0.01, Math.Round(nnet + Math.Max(0.01, Set.NetAdvantage) * k, 2)); }
            if (Set.HedgeSize != "RECOVER" || sn.PerPrice <= 0 || Set.RecoverMove <= 0) return Set.AddLots;
            double net = Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)), total = Basket + (Set.ExitMode == "LEGS" && Current != null ? sn.Balance - Current.BalanceStart : 0);
            double need = net + Math.Max(0, Set.Target - total) / (sn.PerPrice * Set.RecoverMove);
            return Math.Max(0.01, Math.Min(Set.HedgeMaxLots > 0 ? Set.HedgeMaxLots : 100, Math.Ceiling(need * 100 - 1e-6) / 100));
        }
        // what the next entry needs, in words (the 🤖 card and the log)
        public string WaitingFor()
        {
            string tf = Tf.Name(Set.Tf), f = Set.VwapFilter == "DISCOUNT" ? " — buys only UNDER the VWAP, sells only OVER it" : Set.VwapFilter == "FLOW" ? " — buys only OVER the VWAP, sells only UNDER it" : "";
            string w = Set.EntryMode == "AGGBRK" ? "AGGRESSION BREAK on " + tf + ": " + Set.AggRun + "+ candles of one colour, the first opposite close, then the break of its high (BUY) / low (SELL)"
                : Set.EntryMode == "VWAPREJ" ? "VWAP REJECTION on " + tf + ": 6 candles fully above (under) the day's VWAP, then one dips to it and closes back above (under)"
                : Set.EntryMode == "ENGULF" ? "ENGULFING on " + tf + " at the lowest low (BUY) / highest high (SELL) of the last 20 candles"
                : Set.EntryMode == "FVG50" ? "FVG 50%: the price back to the middle of a fresh " + Tf.Name(Set.FvgTf) + " fair value gap"
                : Set.EntryMode == "FVG" ? "FVG: the price back into a fresh " + Tf.Name(Set.FvgTf) + " fair value gap"
                : Set.EntryMode == "FVGBREAK" ? "break of the " + (Set.Swing ? "swing" : "recent") + " high / low with a " + Tf.Name(Set.FvgTf) + " FVG the same way"
                : Set.EntryMode == "VWAP" ? "VWAP cross on " + tf
                : Set.EntryMode == "FLOW" ? "ORDER FLOW surge candle on " + tf + ", then its break"
                : "break of the " + (Set.Swing ? "swing" : "recent") + " high (BUY) / low (SELL) on " + tf + (Set.Swing ? "" : " (" + Set.Lookback + " candle" + (Set.Lookback == 1 ? "" : "s") + ")");
            return w + f + (Set.Windows != "" ? " • only " + Set.Windows + " New York" : "");
        }
        public static bool NyMin(string hhmm, out int m) { TimeSpan t; m = 0; if (!TimeSpan.TryParse((hhmm ?? "").Trim(), IC, out t)) return false; m = (int)t.TotalMinutes; return m >= 0 && m < 1440; }
        // the close time reached: from it until the futures day ends (17:00 → 18:00 New York is the daily break)
        string eodHeld = "";
        bool EndOfDay() { int cm; if (Set.CloseAtNy == "" || !NyMin(Set.CloseAtNy, out cm)) return false; var ny = Now.AddHours(Set.NyOffsetHours); int m = ny.Hour * 60 + ny.Minute; return m >= cm && m < Math.Max(cm + 5, 18 * 60); }
        bool VwapOk(Snap sn, int dir) { if (Set.VwapFilter == "NONE") return true; if (double.IsNaN(Vwap)) return false; bool above = sn.Bid > Vwap; return Set.VwapFilter == "DISCOUNT" ? (dir > 0 ? !above : above) : (dir > 0 ? above : !above); }
        // ✋ a trade you opened by hand: it becomes an iteration the automation manages (target, max drawdown, end of day) and records
        void Adopt(Snap sn)
        {
            var ps = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).ToList(); var last = ps.Last(); lastDir = last.Dir;
            Current = new AutoIteration { N = Iterations.Count + 1, Start = Now, FirstDir = ps[0].Side, BalanceStart = sn.Balance, Adopted = true, Manual = true, Buys = ps.Count(p => p.Dir > 0), Sells = ps.Count(p => p.Dir < 0), BuyLots = ps.Where(p => p.Dir > 0).Sum(p => p.Lots), SellLots = ps.Where(p => p.Dir < 0).Sum(p => p.Lots) };
            closedByUs = false; Current.Ctx = Context(sn, lastDir) + ";manual=yes"; Iterations.Add(Current); Phase = "IN ITERATION"; knownGross = Gross(sn.Pos); Current.MaxGross = knownGross;
            foreach (var p in ps) Trades.Add(new AutoTrade { Iter = Current.N, Time = p.Time > DateTime.MinValue ? p.Time : Now, Side = p.Side, Lots = p.Lots, Price = p.Open, Level = double.NaN, Why = "✋ MANUAL " + p.Side, Basket = BasketOf(sn) });
            SetHedgeLevel(sn, lastDir);
            Tell("✋ MANUAL TRADE — iteration #" + Current.N + ": " + string.Join(", ", ps.Select(p => p.Side + " " + p.Lots.ToString("0.##", IC) + " @ " + P(p.Open))) + " opened by hand. The automation now manages it: " + Plan(sn) + (Set.CloseAtNy != "" ? " Closes at " + Set.CloseAtNy + " New York if still open." : ""));
        }
        // ✋ positions added or closed by hand during an iteration: recorded, the add-on level follows the newest leg
        void Manual(Snap sn, AutoIteration it)
        {
            double g = Gross(sn.Pos);
            if (g > knownGross + 1e-6)
            {
                var p = sn.Pos.OrderBy(x => x.Time).ThenBy(x => x.Ticket).Last(); it.ManualAdds++; if (p.Dir > 0) { it.Buys++; it.BuyLots += g - knownGross; } else { it.Sells++; it.SellLots += g - knownGross; }
                it.MaxGross = Math.Max(it.MaxGross, g); if (!it.Ctx.Contains("manual=yes")) it.Ctx += ";manual=yes";
                Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = p.Side, Lots = g - knownGross, Price = p.Open, Level = double.NaN, Why = "✋ MANUAL ADD " + p.Side, Basket = Basket });
                lastDir = p.Dir; quickBest = double.NaN; SetHedgeLevel(sn, lastDir); Tell("✋ MANUAL ADD " + p.Side + " " + (g - knownGross).ToString("0.##", IC) + " @ " + P(p.Open) + " — added by hand; " + Plan(sn));
            }
            else Tell("✋ PART CLOSED BY HAND — " + (knownGross - g).ToString("0.##", IC) + " lots closed outside the automation; " + Plan(sn));
            knownGross = g; Version++;
        }

        // ---- the numbers for the ANALYSIS
        public sealed class Stats { public int Done, Wins, Losses, Streak, BestStreak, WorstStreak, Buys, Sells, AddOns, MostAddOns; public double Net, Avg, WorstDd, BestIt, WorstIt; }
        public Stats Calc(IEnumerable<AutoIteration> list)
        {
            var d = list.Where(x => x.Done).ToList(); var s = new Stats { Done = d.Count, Wins = d.Count(x => x.Won), Losses = d.Count(x => !x.Won), Net = d.Sum(x => x.Result), Buys = d.Sum(x => x.Buys), Sells = d.Sum(x => x.Sells), AddOns = d.Sum(x => x.AddOns) };
            s.Avg = d.Count > 0 ? s.Net / d.Count : 0; s.WorstDd = d.Count > 0 ? d.Min(x => x.MaxDd) : 0; s.MostAddOns = d.Count > 0 ? d.Max(x => x.AddOns) : 0; s.BestIt = d.Count > 0 ? d.Max(x => x.Result) : 0; s.WorstIt = d.Count > 0 ? d.Min(x => x.Result) : 0;
            int w = 0, l = 0; foreach (var x in d) { if (x.Won) { w++; l = 0; } else { l++; w = 0; } s.BestStreak = Math.Max(s.BestStreak, w); s.WorstStreak = Math.Max(s.WorstStreak, l); }
            s.Streak = w > 0 ? w : -l; return s;
        }
    }
}
