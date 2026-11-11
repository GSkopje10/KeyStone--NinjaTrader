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
        // ⏰ 9:30 STRADDLE (docs/reports/nas100-930-hedge-study.html, the user's request): at the New York open BUY + SELL the same lots;
        // BREAKOUT (the tested winner) = at ±X cut the LOSING leg, the winner gets a stop at its entry and a target +StradTargetX × X (or a
        // trail X behind its best, never below the entry); HOLD = the user's first idea (bank the winner at +X, hold the loser for a reversal —
        // tested: lost money 2020–26). X = POINTS or RANGE (StradRangeK × the previous day's 9:30–16:00 range); lots = StradRisk ÷ (X × $ per point)
        public string StradMode = "BREAKOUT", StradXMode = "RANGE", StradHoldClose = "HALF", StradLoserExit = "ENTRY", StradOpen = "09:30", StradClose = "16:00";
        public double StradX = 100, StradRangeK = 0.5, StradRisk = 100, StradMaxLots = 1, StradTargetX = 3; public bool StradTrail, StradBankHalf;
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
        // 🔑 KEY-LEVEL HEDGE (user's request: "avoid hedging where the price might bounce — support / resistance, FVG, EMA, VWAP"): when the hedge
        // would sit just past a key level (support for a long basket's SELL hedge, resistance for a short's BUY), it moves BEYOND that level by
        // KeyBuffer × the average candle — a bounce no longer fills it, a real break still does
        public bool KeyHedge; public double KeyBuffer = 0.5;
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
        // 📈 DRAWDOWN LEAD (fixed-lead hedging): +DdLeadAdd lots of lead for every DdLeadStep $ of drawdown, up to DdLeadMax (0 = off)
        public double DdLeadStep = 0, DdLeadAdd = 0.05, DdLeadMax = 0.3;
        // 📉 DRAWDOWN-CLOSER HEDGE: for every DdTightEvery % of the balance the basket is down, the next hedge comes DdTightBy % closer to the
        // newest leg (never under DdTightFloor % of its normal distance, never inside half a candle / 3 spreads) — a deep basket is protected sooner
        public double DdTightEvery = 2, DdTightBy = 10, DdTightFloor = 50;   // 2 %: the best in the 2025 + 2026 replays (1 % hedged too early)
        // 🛟 RESCUE TO BREAKEVEN: once a basket was RescuePct % of the balance deep (or its danger meter reached DangerAt), the goal is no longer
        // the target: the first time it is back at break-even (+ the closing fees) everything closes — the capital first
        public bool Rescue = true; public double RescuePct = 3; public int RescueMin = 0;   /* OFF by default: the replays (2025 + 2026) were much worse with 60 min (closing at $0 gave up baskets that later paid, and new ones went deeper) */
        // 🪓 DE-RISK (while 🛟 RESCUE is on): part of the best winning leg + a matching part of the worst losing leg are closed together whenever
        // the pair adds up to ≥ $0 (after the fees) — the total stays, the lots (the risk) shrink; the net lead toward the winners is kept
        public bool DeRisk = false;
        // 🛡 at most TrimMax RISK GUARD trims per iteration (0 = no limit): a trim books the loss of the leg against the break; in a chop the break
        // fails, the basket locks again and the next trim books another loss (06 Nov #22: 3 trims = −$570 booked, the basket closed at −$424)
        public int TrimMax = 0;
        // 🤖 FULL AUTO (step 1 + 2 of docs/SPASIX_FULL_AUTO.md): every entry signal is SCORED from the market reading (regime, extension, pullback,
        // run, day edge, spread); under AutoScoreMin it is skipped (no chasing a break at the very top / bottom); the size follows the score
        public bool FullAuto; public int AutoScoreMin = 50;
        // 🧯 HEDGE STOP: once the basket (open + closed legs) is HedgeStopPct % of the balance down, no more hedges / locks (no new lots) —
        // it waits for the target / the rescue, and MaxDdPct (⛔) is the hard floor that closes everything
        public double HedgeStopPct = 0; public string RiskDial = "SAFE";   /* 0 by default: DEFENDER was much worse with 1 (it stayed locked for weeks); with YOUR 06 Nov settings 1 saved the account (2025: $48 → $5,050) */   /* OFF by default: in the replays it left baskets too small to recover (2026 on your settings: the account almost blown) */   // ⏱ also after RescueMin minutes in the trade (0 = no time rule)
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
        public string OnManual = "STOP";       // 09d (user's rule): STOP = anything you do by hand on the symbol (SpasiX or MT5) stops the automation at once, positions untouched; ADOPT = the old way (adopt and go on)
        public int FvgAge = 0;                 // FVG: a gap is only good this many minutes after it formed (0 = 60 candles)
        public bool FvgStrict; public bool FvgStack = true;   // FvgStack: gaps formed one after another the same way = ONE zone (the 50% of all of them)
        public int AggRun = 1;
        public bool BreakClose;
        public bool ProfitUnlock;
        public bool BeClose;   // 🔐 BREAKEVEN: false = LOCK at the kept profit (the 6-year replays: +$1,723), true = CLOSE everything there (−$4,172)
        public double LockMaxMin;   // ⏱ a lock IN PROFIT that has waited this many minutes closes at that profit (0 = waits for the break)
        // 🧯 STUCK EXIT: a basket still StuckPct % of the balance down (open + closed legs) after StuckHours hours is closed at that loss (0 = off)
        // — the 6 years of replays: a $300+ deep basket takes a median 5 days, 1 in 10 over 2 months, some never come back
        public double StuckPct = 0, StuckHours = 0;
        public bool TrendBreak;   // 📈 TREND BREAK: in a clear EMA 50 / 200 trend, the break of the LAST candle's high (up) / low (down) is an entry too (no swing needed)   // 🔓 a lock IN PROFIT unlocks on any clear break, without the EMA trend (option, OFF: 6 years of replays went from +$1,700 to −$4,600 with it)
        public double FadeAtr = 3;
        // 🎚 PER-SETUP RISK (FULL AUTO / MULTI): each entry setup gets its own MAX HEDGE DISTANCE and TARGET (× the balance's base target),
        // taken from the replays of 2025 + 2026 — applied when that setup starts the iteration, back to the base when it closes
        public bool SetupRisk = true;   // 🔄 BREAK FADE: the broken swing must be ≥ FadeAtr × ATR from the other end of the last 20 candles (a long run into it) → enter the OTHER way   // ⚡ BREAK + CLOSE (option): the swing break counts only when a candle CLOSES beyond the swing high (BUY) / low (SELL) — no wick entries   // user's rule (the futures BH): ONE red candle, a green close, the next candle breaks the green's high = a BUY (mirrored for a SELL); a longer run = a stronger setup (scored)
        public string HedgeSize = "ADVANTAGE"; public double RecoverMove = 3, HedgeMaxLots = 1, NetAdvantage = 0.2;   // ADVANTAGE (the default): every hedge = the net open the other way + NetAdvantage, so after it the net is always NetAdvantage lots the hedge's way   // RECOVER: each hedge sized so a move of RecoverMove its way takes the WHOLE basket to the target (capped at HedgeMaxLots)                 // AGGRESSION BREAK: at least this many candles of one colour before the first opposite close
        public double HedgeMult = 2; public int MaxHedges = 1;   // SIGNAL hedge: the hedge = HedgeMult × the net lots the other way, at most MaxHedges an iteration (HedgeMoney = how much the newest leg must be losing)
        // 🧹 COMPRESS HEDGES (user's request: "prevent longer drawdowns"; 6 years of 1-minute gold replayed with the broker's swap): a hedged
        // basket's matched BUY / SELL lots are closed in pairs (the oldest legs first, partial lots allowed) — the NET (the market exposure) stays
        // exactly the same, the realised part stays in the iteration (the TP / breakeven / rescue / DANGER all count open + closed), only the swap
        // and the margin on the matched lots stop. ROLLOVER = down to the net CompressBefore minutes before the server's midnight swap (only when
        // the pairs pay swap); ALWAYS = after every hedge, whenever gross > 2 × |net| + the entry lots (at most one matched pair stays). Never when the pair EARNS swap, or the basket is frozen in a RISK GUARD lock — also in DANGER (a basket in danger is the one that sits overnight: the replays with a danger gate kept paying)
        public double SizePer1k = 0, TargetPerLot = 200; public string CompressMode = "OFF"; public int CompressBefore = 15; public bool Compress { get { return CompressMode == "ROLLOVER" || CompressMode == "ALWAYS"; } }
        // 📦 NO FLIPS IN A BOX (🏁 RACE lane NFB only — experimental, not a live option): while the price is still inside the high–low of the
        // last BoxBars finished candles, no hedge — the hedge line moves just beyond that box (0 = off; never saved with the live settings)
        public int BoxBars = 0;
        // 🚀 TREND STACK (09e, EXPERIMENT — only on simulated accounts: 🎮 DEMO, 🏁 RACE, 🧪 BACKTEST; refused on every MT5 account): while a basket
        // is open and the short-term trend (TrendDetect) agrees with a side, each new swing break in the trend's way adds lots on that side, sized
        // to reach the target in StackK × ATR(M5): add = clamp((target − total) ÷ (StackK × ATR5 × $ per 1.0 per lot) − the net already with the
        // trend, 0.01, the net cap StackCapPer1k per $1,000) — at most StackMax stacks, never against the trend, never under StackMargin % margin.
        // A trend flip (an opposite trend or an EMA 9/21 cross) closes the stacks at once; a strong trend (≥ BankAt) banks the profitable legs
        // of the other side. Everything through Ask → AutoSend (✋ MANUAL MANAGE → 💡)
        // 🎯 TOTAL EXIT GUARD (09f, the user's HARD RULE: "even if I trade manually, once the price comes to breakeven or target, ALL positions close"):
        // the basket's TOTAL (open + this iteration's closed legs) is watched on every tick — also while the automation is stopped by hand, paused or
        // off — and at the exit level everything on the symbol closes (close-only). Exit = the TARGET normally; BREAKEVEN (+GuardKeep $) once the basket
        // is in trouble: its worst total ≤ −GuardTroubleX × the target, or open > GuardMinutes, or a manual hedge was added. GuardManual = pure manual baskets too
        // ⚠ DANGER RADAR options (09h, OFF by default — each LOST to COMPRESS ALWAYS in the 6-year test): above RadarAt % → no more add-ons /
        // lock at net 0 (the lock's own unlock on the break) / close at the next breakeven (+GuardKeep); RadarAlert % = the toast; SlopeFilter = the TAH lane only
        public bool RadarNoAdd, RadarLock, RadarBe, SlopeFilter; public double RadarAt = 50, RadarAlert = 35;
        public bool ExitGuard = true, GuardManual = true; public double GuardKeep = 0.5, GuardTroubleX = 3, GuardMinutes = 60;
        // ➕ ADD ON BREAK (09h, OFF by default; every account kind once he switches it on — it sends through AutoSend like any engine order): one add (AobMult × the
        // entry) in the net's direction while the basket loses, on a trend ≥ AobMin that way + a finished M1 close beyond the last swing; its stop at the last leg (LEG) / the swing (SWING)
        // 🛡 PROTECT THE ADD (09h): AUTO = on for simulated accounts, OFF on MT5 accounts until ticked ON (it OPENS the equalizing lots); ATR = entry − k × ATR(M1), SWING = the last swing
        public string ProtectAdd = "AUTO", ProtectMode = "ATR"; public double ProtectK = 1, ProtectBigX = 2; public int ProtectCool = 15;
        public bool AddOnBreak; public double AobMult = 1; public string AobStop = "LEG"; public int AobMax = 1, AobMin = 70;
        public bool TrendStack; public double StackK = 1.5, StackCapPer1k = 0.07, StackMargin = 300; public int StackMax = 3, StackMin = 60, BankAt = 70;
        public static bool TrendStackAllowed(bool simulated) { return simulated; }
        public const string TrendStackNo = "🚀 TREND STACK is an EXPERIMENT: only on a simulated account (🎮 DEMO, 🏁 RACE, 🧪 BACKTEST) — never on an MT5 account (REAL, PROP or MT5 demo). Turn it off in ⚙ ADVANCED to start here.";
        // 🦅 RUNNER (09k, EXPERIMENT — only on simulated accounts: 🎮 DEMO, 🏁 RACE, 🧪 BACKTEST; refused on every MT5 account): DEFENDER for the balance,
        // then small entries (RunnerPer1k lot per $1,000 of the LIVE balance at each iteration start, to the nearest lot step, min the broker's minimum),
        // the hedge lead = the entry, add-ons 2×, a target RunnerTargetMult × DEFENDER's (+$2 per 0.01 lot × RunnerTargetMult), 🧹 COMPRESS ALWAYS and the
        // RISK GUARD in LOCK mode (the "chop lock": 0.6 lot per $5,000, margin 500 %, 3 hedges an hour → lock instead of hedging, pair-off)
        public const double RunnerTargetMult = 1;   // ← the ONE number to change: the target = entry lots × 200 × this ($2 per 0.01 lot at 1; 09l: 3 → 1, the 3× target blew the account in one 6-year model)
        public const double RunnerPer1k = 0.006;
        public static bool RunnerAllowed(bool simulated) { return simulated; }
        public const string RunnerNo = "🦅 RUNNER is an EXPERIMENT: only on a simulated account (🎮 DEMO, 🏁 RACE, 🧪 BACKTEST) — never on an MT5 account (REAL, PROP or MT5 demo). Pick another STRATEGY (🛡 DEFENDER is the default) to start here.";
        public const string RunnerText = "🦅 RUNNER (experiment, simulated accounts only): small entries 0.006 lot per $1,000, COMPRESS, and the chop LOCK (no more hedges while the price whipsaws — it waits for a clean break). 6-year replays with your brokers' real costs: the most stable version (never blew the account, worst year about −$2,000), near breakeven overall — test it on DEMO / RACE.";
        public bool IsRunner { get { return Preset == "RUNNER"; } }
        // the smallest lot is the broker's minimum (0.01): when RUNNER's formula asks for less ($431 → 0.0026), 0.01 trades and this says how much bigger the risk is ("" = no warning)
        public static string RunnerSizeWarning(double bal, double minLot = 0.01)
        {
            if (!(bal > 0)) return ""; double want = bal / 1000 * RunnerPer1k; if (Math.Round(want / minLot, MidpointRounding.AwayFromZero) >= 1) return "";
            return "⚠ at this balance ($" + bal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + ") " + minLot.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " lot (the smallest lot) is " + (minLot / want).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "× the planned risk (" + RunnerPer1k.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " × " + (bal / 1000).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " = " + want.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + " lot) — the planned size needs ≥ $" + Math.Ceiling(minLot / 2 / RunnerPer1k * 1000).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
        public static void ApplyRunner(AutoSettings s, double bal)
        {
            s.SizePer1k = RunnerPer1k; s.TargetPerLot = Math.Round(200 * RunnerTargetMult, 2);   // 💰 re-sized from the live balance at every iteration start (HedgeAuto.SizeFor, nearest step)
            var sz = HedgeAuto.SizeFor(bal, bal, false, null, s.SizePer1k, s.TargetPerLot, true); s.EntryLots = sz.Item1; s.NetAdvantage = sz.Item1; s.AddLots = Math.Round(2 * sz.Item1, 2); s.Target = sz.Item2; s.ScaleLots = 0.01;
            s.HedgeSize = "ADVANTAGE"; s.CompressMode = "ALWAYS";
            s.RiskGuard = true; s.GuardMode = "LOCK"; s.GuardLots = 0.6; s.GuardMargin = 500; s.GuardChop = 3; s.PairOff = true;   // the chop LOCK
        }
        public string ExitMode = "BASKET";     // BASKET = everything closes at +Target; LEGS = each position closes at its own +Target × its lots / the entry lots                 // FVG: the middle candle's body must be bigger than the average range of the last 20 candles (the research's gap)
        public AutoSettings Copy() { return (AutoSettings)MemberwiseClone(); }
        // ⚡ what changes between two settings, in words ("🧹 COMPRESS HEDGES: OFF → ALWAYS"), for the APPLY NOW question
        static readonly Dictionary<string, string> DiffNames = new Dictionary<string, string> { { "cmp", "🧹 COMPRESS HEDGES" }, { "target", "TARGET $" }, { "entry", "ENTRY LOTS" }, { "nadv", "LEAD (net lots)" }, { "add", "ADD-ON LOTS" }, { "dd", "MAX DRAWDOWN $" }, { "preset", "STRATEGY" }, { "hcap", "MAX HEDGE DISTANCE" }, { "scale", "📉 SCALE OUT" }, { "belock", "🔐 BREAKEVEN LOCK" }, { "dexit", "🚨 DANGER EXIT" }, { "resc", "🛟 RESCUE" }, { "rg", "🛡 RISK GUARD" }, { "ronly", "RANGE-ONLY HEDGE" }, { "hsize", "HEDGE SIZE" }, { "emode", "ENTRY SETUP" }, { "hmode", "HEDGE MODE" } };
        public static List<string> Diff(AutoSettings a, AutoSettings b)
        {
            Func<AutoSettings, Dictionary<string, string>> kv = x => x.Save().Split(';').Select(p => p.Split(new[] { '=' }, 2)).Where(p => p.Length == 2).GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);
            var da = kv(a); var db = kv(b); var res = new List<string>();
            foreach (var k in db.Keys) { string va; da.TryGetValue(k, out va); if (va != db[k]) { string nm; if (!DiffNames.TryGetValue(k, out nm)) nm = k; res.Add(nm + ": " + (va ?? "—") + " → " + db[k]); } }
            return res;
        }
        // the researched models (docs/research/hedge/scalp20.html, report.html)
        public static AutoSettings Model(string name, AutoSettings b) { return Model(name, b, 5000); }
        public static AutoSettings Model(string name, AutoSettings b, double bal)
        {
            var s = b.Copy(); s.Preset = name; s.ExitMode = "BASKET"; s.FirstDir = "AUTO"; s.NextStart = "BREAK"; s.AddToWinner = false; s.PerDay = 0; s.MaxIterations = 0; s.NewsMode = "IGNORE"; s.FvgAge = 0; s.FvgStrict = false;
            // ★ SAFE HEDGE (the replay of 5 months of 1-minute gold, user's own break strategy): range-only hedges + the rescue exit after 3
            // hedges, small size for the balance (0.01 lot and +$2 per $5,000) so the deepest basket stays survivable; never a loss close
            if (name == "PRO") { s.Tf = 1; s.EntryMode = "BREAK"; s.Swing = true; s.SwingK = 2; s.VwapFilter = "NONE"; s.HedgeMode = "BREAK"; s.Rolling = true; s.HedgeSize = "STEPLEAD"; s.EntryLots = 0.02; s.NetAdvantage = 0.02; s.AddLots = 0.02; s.Target = 10; s.ScaleOut = true; s.ScaleLots = 0.01; s.ScaleStep = 1.5; s.MaxDrawdown = 0; s.AfterDrawdown = "NEXT"; s.Windows = ""; s.CloseAtNy = ""; s.EodLoss = "HOLD"; s.RangeOnly = true; s.BeAfter = 3; s.QuickHedge = true; s.QuickAfter = 3; s.HedgeCap = 3; s.LockOn = false; s.TrendFilter = "NONE"; s.RiskGuard = true; s.GuardMode = "LOCK"; s.GuardLots = 1.0; s.GuardMargin = 500; s.GuardChop = 3; s.GuardMaxLead = 3; s.GuardBreak = 1.0; s.PairOff = true; s.HardFloat = 60; s.HardMargin = 60; s.ProtectAt = 0; s.TrendHedge = true; s.TrendHedgeMult = 2; }
            if (name == "LADDER") { s.Tf = 1; s.EntryMode = "STRADDLE"; s.HedgeMode = "LADDER"; s.HedgeSize = "FIXED"; s.LadderStep = 4; s.LadderMax = 10; s.EntryLots = 0.05; s.AddLots = 0.05; s.Target = 17; s.TrendFilter = "EMA"; s.BeAfter = 6; s.MaxDrawdown = 0; s.VwapFilter = "NONE"; s.Windows = ""; s.CloseAtNy = ""; s.EodLoss = "HOLD"; s.RangeOnly = false; s.QuickHedge = false; s.LockOn = false; s.BeAfter = 0; s.Rolling = false; }
            if (name == "STRADDLE930")
            {   // ⏰ 9:30 STRADDLE → BREAKOUT (rule set 1 of the study: +9.96 points a day per lot, both halves positive, PF 1.33)
                s.Tf = 1; s.HedgeMode = "NONE"; s.FirstDir = "AUTO"; s.CloseAtNy = ""; s.Windows = ""; s.FullAuto = false; s.Smart = false; s.RiskGuard = false; s.BeLock = false; s.DangerExit = false; s.Rescue = false; s.ScaleOut = false;
                s.StradMode = "BREAKOUT"; s.StradXMode = "RANGE"; s.StradRangeK = 0.5; s.StradTargetX = 3; s.StradTrail = false; s.StradBankHalf = false; s.StradOpen = "09:30"; s.StradClose = "16:00";
                if (s.StradRisk <= 0) s.StradRisk = 100; if (s.StradMaxLots <= 0) s.StradMaxLots = 1;
                return s;
            }
            if (name == "RUNNER")
            {   // 🦅 RUNNER (09k, experiment — simulated accounts only): DEFENDER for the balance + small entries, compress, the chop LOCK (09l: target 1×)
                var d = Model("DEFENDER", b, bal); d.Preset = "RUNNER"; ApplyRunner(d, bal); return d;
            }
            if (name == "SAFELIVE")
            {   // 🔒 SAFE LIVE (small accounts, 09d): the setup that survived the small-account sweeps — NOT the default (DEFENDER stays)
                var d = Model("DEFENDER", b, bal); d.Preset = "SAFELIVE"; ApplySafeLive(d, bal); return d;
            }
            if (name == "FULLAUTO")
            {   // 🤖 FULL AUTO = DEFENDER's protections + the scored entries (no chasing, size from the score) + the drawdown tools
                var d = Model("DEFENDER", b, bal); d.Preset = "FULLAUTO"; d.FullAuto = true;   /* entry: the swing BREAK — 🎯 MULTI was worse in the 2025 replay ($6,326 vs $7,402, deepest basket −$1,405 vs −$648): an option */ d.AutoScoreMin = 50;   /* 50 = the best in the 2025 + 2026 replays (60 skipped too much in 2026) */ d.DdTightEvery = 2; d.DdTightBy = 10; d.DdTightFloor = 50; d.Rescue = true; d.RescuePct = 3; d.RescueMin = 0; d.DeRisk = false; d.TrimMax = 0;
                return d;
            }
            if (name == "DEFENDER")
            {   // 🛡 DEFENDER (the default, user's request: capital first, small profits, a calm hedging flow): the SAFE break hedge (a FIXED lead,
                // never a growing step) at 0.01 lot per $1,000 + the RISK GUARD (LOCK instead of a hedge when too big / low margin / chop, the
                // size cap 0.6 lot per $5,000, unlock only on an obvious trend, pair-off) + scale out + the rescue after 3 hedges + the 🚨 DANGER EXIT
                double k = Math.Max(0.01, Math.Round(Math.Max(1000, bal) / 1000 * 0.01, 2));
                s.Tf = 1; s.EntryMode = "BREAK"; s.Swing = true; s.SwingK = 2; s.VwapFilter = "NONE"; s.HedgeMode = "BREAK"; s.Rolling = true; s.HedgeSize = "ADVANTAGE"; s.EntryLots = k; s.NetAdvantage = k; s.AddLots = Math.Round(k * 2, 2); s.Target = Math.Round(k * 200, 2); s.MaxDrawdown = 0; s.AfterDrawdown = "NEXT"; s.Windows = ""; s.CloseAtNy = ""; s.EodLoss = "HOLD"; s.RangeOnly = true; s.BeAfter = 3; s.LockOn = false; s.TrendFilter = "NONE"; s.QuickHedge = false; s.HedgeCap = 5;
                s.RiskGuard = false; /* 🛡 OFF (the 6-year replays: +$2,258 without its freezing locks vs +$1,723 with them) */ s.BeLock = true; s.BeLockAt = 80; s.BeLockKeep = 45; s.BeClose = false; s.LockOn = false; s.TrimMax = 0; s.GuardMode = "LOCK"; s.GuardLots = 0.6; s.GuardMargin = 500; s.GuardChop = 3; s.GuardMaxLead = 3; s.GuardBreak = 1.0; s.GuardLift = false; s.PairOff = true; s.ScaleOut = true; s.ScaleLots = 0.01; s.ScaleStep = 1.5; s.TrendHedge = false; s.ProtectAt = 0;
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
        // 🔒 SAFE LIVE's values (every one stays an ordinary, editable box after picking it)
        public static void ApplySafeLive(AutoSettings s, double bal)
        {
            s.SizePer1k = 0.01; s.TargetPerLot = 200;   // 💰 0.01 lot per $1,000 of the live balance (the broker minimum + a warning below $1,000), +$2 per 0.01 lot
            var sz = HedgeAuto.SizeFor(bal, bal, false, null, s.SizePer1k, s.TargetPerLot); s.EntryLots = sz.Item1; s.NetAdvantage = sz.Item1; s.AddLots = sz.Item1; s.Target = sz.Item2;
            s.HedgeSize = "SAME";                       // every hedge = the entry lots (not net + lead)
            s.CompressMode = "ALWAYS";                  // 🧹 compress after every hedge
            s.MaxDdPct = 10; s.AfterDrawdown = "NEXT"; // ⛔ −10 % of the iteration's starting balance → the basket closes, the next iteration
            s.DayLossPct = 5;                           // ⛔ −5 % on the day → closed, no new iteration until tomorrow
            s.BeLock = true; s.BeClose = true;          // 🔐 AT THE BREAKEVEN LINE = CLOSE ALL
            s.DangerExit = true;                        // 🚨 DANGER EXIT
            s.ScaleOut = false;                         // (a same-size basket never holds more than its lead: nothing to scale out)
        }
        // the evidence, in words (the STRATEGY list, its tooltip and the line under it) — the sweeps of 09c (scratchpad small/: HedgeLab, 1-minute
        // gold 2020–26, a 90-day run from every 20th day, spread 35 points, $7 a lot, the broker's swap; "blown" = under half the start)
        public const string SafeLiveEvidence = "Tested (1-minute gold 2020–26, a 90-day run from every 20th trading day, 74 runs per balance, same-size hedges + compress + a 10 % basket stop): $431 → 3 % (2020–22) and 5 % (2023–26) of the runs fell under half the balance within 90 days, the median run ended at $396 / $415; $1,000 → 0 % and 0 %, median $972 / $989. It PROTECTS the account — it did NOT grow it in the tests. The 5 % daily loss, the breakeven close and the DANGER EXIT were not part of that sweep.";
        // ⚠ the START check on a REAL account: no basket stop or no daily loss limit (both 0) → a warning ("" = fine)
        public static string LiveLimitsWarning(string kind, AutoSettings s)
        {
            if (kind != "REAL" || s == null) return "";
            bool noBasket = s.MaxDrawdown <= 0 && s.MaxDdPct <= 0, noDay = s.DayLoss <= 0 && s.DayLossPct <= 0; if (!noBasket && !noDay) return "";
            return "⚠ " + (noBasket && noDay ? "NO BASKET STOP and NO DAILY LOSS LIMIT" : noBasket ? "NO BASKET STOP (MAX DRAWDOWN $ and % are 0)" : "NO DAILY LOSS LIMIT (DAILY LOSS $ and % are 0)") + " on a REAL account — the tests blew small accounts without one. Use 🔒 SAFE LIVE's limits (−10 % per iteration, −5 % a day)?";
        }
        // the one-click "apply SAFE LIVE limits": only the missing ones, nothing else changes
        public static void ApplySafeLiveLimits(AutoSettings s)
        {
            if (s.MaxDrawdown <= 0 && s.MaxDdPct <= 0) { s.MaxDdPct = 10; s.AfterDrawdown = "NEXT"; }
            if (s.DayLoss <= 0 && s.DayLossPct <= 0) s.DayLossPct = 5;
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
        public string Save() { return string.Join(";", "sym=" + Symbol, "tf=" + Tf, "look=" + Lookback, "entry=" + EntryLots.ToString(IC), "add=" + AddLots.ToString(IC), "target=" + Target.ToString(IC), "dd=" + MaxDrawdown.ToString(IC), "comm=" + CommissionPerLot.ToString(IC), "first=" + FirstDir, "next=" + NextStart, "pause=" + PauseSec, "afterdd=" + AfterDrawdown, "rolling=" + (Rolling ? 1 : 0), "maxit=" + MaxIterations, "hmode=" + HedgeMode, "hprice=" + HedgePrice.ToString(IC), "hmoney=" + HedgeMoney.ToString(IC), "emode=" + EntryMode, "fvol=" + FlowVol.ToString(IC), "fshare=" + FlowShare.ToString(IC), "perday=" + PerDay, "win=" + Windows.Replace(";", ","), "nyoff=" + NyOffsetHours, "news=" + NewsMode, "nb=" + NewsBefore, "na=" + NewsAfter, "win2=" + (AddToWinner ? 1 : 0), "wlots=" + WinnerLots.ToString(IC), "wmax=" + MaxWinnerAdds, "swing=" + (Swing ? 1 : 0), "swk=" + SwingK, "fvgtf=" + FvgTf, "wlots2=" + WinnerLotsSell.ToString(IC), "preset=" + Preset, "vf=" + VwapFilter, "eod=" + CloseAtNy, "man=" + (TrackManual ? 1 : 0), "onman=" + OnManual, "fage=" + FvgAge, "fstrict=" + (FvgStrict ? 1 : 0), "hmult=" + HedgeMult.ToString(IC), "maxh=" + MaxHedges, "exit=" + ExitMode, "aggrun=" + AggRun, "fstack=" + (FvgStack ? 1 : 0), "hsize=" + HedgeSize, "hs2=1", "roll2=1", "eodl=" + EodLoss, "ronly=" + (RangeOnly ? 1 : 0), "qh=" + (QuickHedge ? 1 : 0), "chedge=" + (CandleHedge ? 1 : 0), "khedge=" + (KeyHedge ? 1 : 0), "kbuf=" + KeyBuffer.ToString(IC), "smart=" + (Smart ? 1 : 0), "sbest=" + (SmartBest ? 1 : 0), "smartn=" + SmartMin, "qafter=" + QuickAfter.ToString(IC), "pat=" + ProtectAt.ToString(IC), "rg=" + (RiskGuard ? 1 : 0), "glots=" + GuardLots.ToString(IC), "gmargin=" + GuardMargin.ToString(IC), "gchop=" + GuardChop, "glead=" + GuardMaxLead, "glift=" + (GuardLift ? 1 : 0), "gmode=" + GuardMode, "gbreak=" + GuardBreak.ToString(IC), "hfloat=" + HardFloat.ToString(IC), "hmargin=" + HardMargin.ToString(IC), "pair=" + (PairOff ? 1 : 0), "thedge=" + (TrendHedge ? 1 : 0), "scale=" + (ScaleOut ? 1 : 0), "slots=" + ScaleLots.ToString(IC), "sstep=" + ScaleStep.ToString(IC), "thmult=" + TrendHedgeMult.ToString(IC), "pkeep=" + ProtectKeep.ToString(IC), "hcap=" + HedgeCap.ToString(IC), "lstep=" + LadderStep.ToString(IC), "lmax=" + LadderMax.ToString(IC), "trend=" + TrendFilter, "beafter=" + BeAfter, "lock=" + (LockOn ? 1 : 0), "lafter=" + LockAfter, "lrange=" + LockRange.ToString(IC), "unlock=" + Unlock, "nadv=" + NetAdvantage.ToString(IC), "rmove=" + RecoverMove.ToString(IC), "hmax=" + HedgeMaxLots.ToString(IC), "dexit=" + (DangerExit ? 1 : 0), "dat=" + DangerAt.ToString(IC), "dkeep=" + DangerKeep.ToString(IC), "ddpct=" + MaxDdPct.ToString(IC), "rexit=" + (RecoverExit ? 1 : 0), "belock=" + (BeLock ? 1 : 0), "ddlstep=" + DdLeadStep.ToString(IC), "ddladd=" + DdLeadAdd.ToString(IC), "ddlmax=" + DdLeadMax.ToString(IC), "dtev=" + DdTightEvery.ToString(IC), "dtby=" + DdTightBy.ToString(IC), "dtfl=" + DdTightFloor.ToString(IC), "resc=" + (Rescue ? 1 : 0), "rpct=" + RescuePct.ToString(IC), "rmin=" + RescueMin, "derisk=" + (DeRisk ? 1 : 0), "trimmax=" + TrimMax, "fauto=" + (FullAuto ? 1 : 0), "bclose=" + (BreakClose ? 1 : 0), "punlock=" + (ProfitUnlock ? 1 : 0), "beclose=" + (BeClose ? 1 : 0), "lockmax=" + LockMaxMin.ToString(IC), "stpct=" + StuckPct.ToString(IC), "sthrs=" + StuckHours.ToString(IC), "tbreak=" + (TrendBreak ? 1 : 0), "fadeatr=" + FadeAtr.ToString(IC), "srisk=" + (SetupRisk ? 1 : 0), "hstop=" + HedgeStopPct.ToString(IC), "ascore=" + AutoScoreMin, "rdial=" + RiskDial, "belat=" + BeLockAt.ToString(IC), "belkeep=" + BeLockKeep.ToString(IC), "rdeep=" + RecDeep.ToString(IC), "rkeep=" + RecKeep.ToString(IC), "dayloss=" + DayLoss.ToString(IC), "daypct=" + DayLossPct.ToString(IC), "cmp=" + CompressMode, "tstack=" + (TrendStack ? 1 : 0), "padd=" + ProtectAdd, "pmode=" + ProtectMode, "pk=" + ProtectK.ToString(IC), "pbig=" + ProtectBigX.ToString(IC), "pcool=" + ProtectCool, "aob=" + (AddOnBreak ? 1 : 0), "aobx=" + AobMult.ToString(IC), "aobs=" + AobStop, "aobm=" + AobMax, "aobmin=" + AobMin, "rnoadd=" + (RadarNoAdd ? 1 : 0), "rlock=" + (RadarLock ? 1 : 0), "rbe=" + (RadarBe ? 1 : 0), "rat=" + RadarAt.ToString(IC), "ralert=" + RadarAlert.ToString(IC), "xg=" + (ExitGuard ? 1 : 0), "xgman=" + (GuardManual ? 1 : 0), "xgkeep=" + GuardKeep.ToString(IC), "xgx=" + GuardTroubleX.ToString(IC), "xgmin=" + GuardMinutes.ToString(IC), "stk=" + StackK.ToString(IC), "stcap=" + StackCapPer1k.ToString(IC), "stml=" + StackMargin.ToString(IC), "stmax=" + StackMax, "stmin=" + StackMin, "stbank=" + BankAt, "sz1k=" + SizePer1k.ToString(IC), "tpl=" + TargetPerLot.ToString(IC), "cmpb=" + CompressBefore.ToString(IC), "smode=" + StradMode, "sxm=" + StradXMode, "sx=" + StradX.ToString(IC), "srk=" + StradRangeK.ToString(IC), "srsk=" + StradRisk.ToString(IC), "smax=" + StradMaxLots.ToString(IC), "stx=" + StradTargetX.ToString(IC), "strail=" + (StradTrail ? 1 : 0), "sbank=" + (StradBankHalf ? 1 : 0), "shold=" + StradHoldClose, "slex=" + StradLoserExit, "sopen=" + StradOpen, "sclose=" + StradClose); }
        public static AutoSettings Load(string line)
        {
            var s = new AutoSettings(); if (string.IsNullOrEmpty(line)) return s; var d = new Dictionary<string, string>();
            foreach (var kv in line.Split(';')) { int i = kv.IndexOf('='); if (i > 0) d[kv.Substring(0, i)] = kv.Substring(i + 1); }
            Func<string, double, double> n = (k, def) => { string v; double x; return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, IC, out x) ? x : def; };
            Func<string, string, string> t = (k, def) => { string v; return d.TryGetValue(k, out v) && v != "" ? v : def; };
            s.Symbol = t("sym", s.Symbol); s.Tf = (int)n("tf", 1); s.Lookback = (int)n("look", 1); s.EntryLots = n("entry", 0.1); s.AddLots = n("add", 0.2); s.Target = n("target", 20); s.MaxDrawdown = n("dd", 0); s.CommissionPerLot = n("comm", 0);
            s.FirstDir = t("first", "AUTO"); s.NextStart = t("next", "BREAK"); s.PauseSec = (int)n("pause", 60); s.AfterDrawdown = t("afterdd", "STOP"); s.Rolling = !d.ContainsKey("roll2") ? true : n("rolling", 1) == 1;   /* older saved settings (fixed by default then) → FOLLOWS */ s.MaxIterations = (int)n("maxit", 0); s.HedgeMode = t("hmode", "BREAK"); s.HedgePrice = n("hprice", 5); s.HedgeMoney = n("hmoney", 100);
            s.EntryMode = t("emode", "BREAK"); s.FlowVol = n("fvol", 1.5); s.FlowShare = n("fshare", 0.7); s.PerDay = (int)n("perday", 0); s.Windows = t("win", ""); s.NyOffsetHours = (int)n("nyoff", -7); s.NewsMode = t("news", "IGNORE"); s.NewsBefore = (int)n("nb", 15); s.NewsAfter = (int)n("na", 15); s.AddToWinner = n("win2", 0) == 1; s.WinnerLots = n("wlots", 0.1); s.MaxWinnerAdds = (int)n("wmax", 0); s.Swing = n("swing", 1) == 1; s.SwingK = (int)Math.Max(1, n("swk", 2)); s.FvgTf = (int)n("fvgtf", 5); s.WinnerLotsSell = n("wlots2", s.WinnerLots); s.Preset = t("preset", ""); s.VwapFilter = t("vf", "NONE"); s.CloseAtNy = d.ContainsKey("eod") ? d["eod"] : ""; s.TrackManual = n("man", 1) == 1; s.OnManual = t("onman", "STOP") == "ADOPT" ? "ADOPT" : "STOP"; s.FvgAge = (int)n("fage", 0); s.FvgStrict = n("fstrict", 0) == 1; s.HedgeMult = n("hmult", 2); s.MaxHedges = (int)n("maxh", 1); s.ExitMode = t("exit", "BASKET"); s.AggRun = (int)Math.Max(1, n("aggrun", 1)); s.FvgStack = n("fstack", 1) == 1; s.EodLoss = t("eodl", "HOLD"); s.RangeOnly = n("ronly", 0) == 1; s.QuickHedge = n("qh", 0) == 1; s.CandleHedge = n("chedge", 0) == 1; s.KeyHedge = n("khedge", 0) == 1; s.KeyBuffer = Math.Max(0.1, Math.Min(3, n("kbuf", 0.5))); s.Smart = n("smart", 0) == 1; s.SmartBest = n("sbest", 0) == 1; s.SmartMin = (int)Math.Max(3, n("smartn", 10)); s.QuickAfter = Math.Max(0.1, n("qafter", 3)); s.ProtectAt = Math.Max(0, Math.Min(99, n("pat", 0))); s.RiskGuard = n("rg", 0) == 1; s.GuardLots = Math.Max(0.01, n("glots", 1.0)); s.GuardMargin = Math.Max(0, n("gmargin", 500)); s.GuardChop = (int)Math.Max(0, n("gchop", 3)); s.GuardMaxLead = (int)Math.Max(1, n("glead", 3)); s.GuardLift = n("glift", 0) == 1; s.GuardMode = t("gmode", "LOCK"); s.GuardBreak = Math.Max(0, n("gbreak", 1)); s.HardFloat = Math.Max(0, n("hfloat", 0)); s.HardMargin = Math.Max(0, n("hmargin", 0)); s.PairOff = n("pair", 1) == 1; s.TrendHedge = n("thedge", 0) == 1; s.ScaleOut = n("scale", 0) == 1; s.ScaleLots = Math.Max(0.01, n("slots", 0.05)); s.ScaleStep = Math.Max(0.1, n("sstep", 1.5)); s.TrendHedgeMult = Math.Max(1, n("thmult", 2)); s.ProtectKeep = Math.Max(0, Math.Min(95, n("pkeep", 10))); s.HedgeCap = Math.Max(0, n("hcap", 5)); s.LadderStep = Math.Max(0.1, n("lstep", 2.5)); s.LadderMax = (int)Math.Max(0, n("lmax", 10)); s.TrendFilter = t("trend", "NONE"); s.BeAfter = (int)Math.Max(0, n("beafter", 0)); s.LockOn = n("lock", 0) == 1; s.LockAfter = (int)Math.Max(1, n("lafter", 2)); s.LockRange = Math.Max(0.01, n("lrange", 3)); s.Unlock = t("unlock", "LEAD"); s.HedgeSize = t("hsize", "ADVANTAGE"); s.NetAdvantage = n("nadv", 0.2); if (!d.ContainsKey("hs2") && s.HedgeSize == "FIXED" && s.Preset != "BOX") s.HedgeSize = "ADVANTAGE";   /* the user's rule: the new side always LEADS BY the lots (older saved settings were FIXED by default) */ s.RecoverMove = n("rmove", 3); s.HedgeMaxLots = n("hmax", 1); s.DangerExit = n("dexit", 0) == 1; s.DangerAt = Math.Max(10, Math.Min(100, n("dat", 60))); s.DangerKeep = Math.Max(0, Math.Min(100, n("dkeep", 25))); s.MaxDdPct = Math.Max(0, Math.Min(100, n("ddpct", 0))); s.RecoverExit = n("rexit", 0) == 1; s.BeLock = n("belock", 0) == 1; s.DdLeadStep = Math.Max(0, n("ddlstep", 0)); s.DdLeadAdd = Math.Max(0.01, n("ddladd", 0.05)); s.DdLeadMax = Math.Max(0, n("ddlmax", 0.3)); s.DdTightEvery = Math.Max(0, n("dtev", 2)); s.DdTightBy = Math.Max(0, Math.Min(50, n("dtby", 10))); s.DdTightFloor = Math.Max(10, Math.Min(100, n("dtfl", 50))); s.Rescue = n("resc", 1) == 1; s.RescuePct = Math.Max(0.5, Math.Min(50, n("rpct", 3))); s.RescueMin = (int)Math.Max(0, Math.Min(1440, n("rmin", 0))); s.DeRisk = n("derisk", 0) == 1; s.TrimMax = (int)Math.Max(0, Math.Min(20, n("trimmax", 0))); s.FullAuto = n("fauto", 0) == 1; s.BreakClose = n("bclose", 0) == 1; s.ProfitUnlock = n("punlock", 0) == 1; s.BeClose = n("beclose", 0) == 1; s.LockMaxMin = Math.Max(0, n("lockmax", 0)); s.StuckPct = Math.Max(0, Math.Min(90, n("stpct", 0))); s.StuckHours = Math.Max(0, n("sthrs", 0)); s.TrendBreak = n("tbreak", 0) == 1; s.FadeAtr = Math.Max(0, Math.Min(20, n("fadeatr", 3))); s.SetupRisk = n("srisk", 1) == 1; s.HedgeStopPct = Math.Max(0, Math.Min(50, n("hstop", 0))); s.AutoScoreMin = (int)Math.Max(0, Math.Min(100, n("ascore", 50))); s.RiskDial = t("rdial", "SAFE"); s.BeLockAt = Math.Max(10, Math.Min(99, n("belat", 80))); s.BeLockKeep = Math.Max(0, Math.Min(90, n("belkeep", 10))); s.RecDeep = Math.Max(0.5, n("rdeep", 2)); s.RecKeep = Math.Max(0, Math.Min(100, n("rkeep", 20))); s.DayLoss = Math.Max(0, n("dayloss", 0)); s.DayLossPct = Math.Max(0, Math.Min(100, n("daypct", 0))); s.SizePer1k = Math.Max(0, n("sz1k", 0)); s.TargetPerLot = Math.Max(1, n("tpl", 200)); s.CompressMode = t("cmp", "OFF"); if (s.CompressMode != "ROLLOVER" && s.CompressMode != "ALWAYS") s.CompressMode = "OFF"; s.CompressBefore = (int)Math.Max(1, Math.Min(120, n("cmpb", 15))); s.TrendStack = n("tstack", 0) == 1; s.ProtectAdd = t("padd", "AUTO"); if (s.ProtectAdd != "ON" && s.ProtectAdd != "OFF") s.ProtectAdd = "AUTO"; s.ProtectMode = t("pmode", "ATR") == "SWING" ? "SWING" : "ATR"; s.ProtectK = Math.Max(0.2, Math.Min(10, n("pk", 1))); s.ProtectBigX = Math.Max(1, Math.Min(10, n("pbig", 2))); s.ProtectCool = (int)Math.Max(1, Math.Min(240, n("pcool", 15))); s.AddOnBreak = n("aob", 0) == 1; s.AobMult = n("aobx", 1) >= 1.5 ? 2 : 1; s.AobStop = t("aobs", "LEG") == "SWING" ? "SWING" : "LEG"; s.AobMax = (int)Math.Max(1, Math.Min(2, n("aobm", 1))); s.AobMin = (int)Math.Max(50, Math.Min(100, n("aobmin", 70))); s.RadarNoAdd = n("rnoadd", 0) == 1; s.RadarLock = n("rlock", 0) == 1; s.RadarBe = n("rbe", 0) == 1; s.RadarAt = Math.Max(5, Math.Min(95, n("rat", 50))); s.RadarAlert = Math.Max(5, Math.Min(95, n("ralert", 35))); s.ExitGuard = n("xg", 1) == 1; s.GuardManual = n("xgman", 1) == 1; s.GuardKeep = Math.Max(0, n("xgkeep", 0.5)); s.GuardTroubleX = Math.Max(0.5, n("xgx", 3)); s.GuardMinutes = Math.Max(1, n("xgmin", 60)); s.StackK = Math.Max(0.2, Math.Min(10, n("stk", 1.5))); s.StackCapPer1k = Math.Max(0.01, Math.Min(1, n("stcap", 0.07))); s.StackMargin = Math.Max(0, n("stml", 300)); s.StackMax = (int)Math.Max(1, Math.Min(10, n("stmax", 3))); s.StackMin = (int)Math.Max(30, Math.Min(100, n("stmin", 60))); s.BankAt = (int)Math.Max(30, Math.Min(101, n("stbank", 70)));
            s.StradMode = t("smode", "BREAKOUT"); s.StradXMode = t("sxm", "RANGE"); s.StradX = n("sx", 100); s.StradRangeK = n("srk", 0.5); s.StradRisk = n("srsk", 100); s.StradMaxLots = n("smax", 1); s.StradTargetX = n("stx", 3);
            s.StradTrail = n("strail", 0) == 1; s.StradBankHalf = n("sbank", 0) == 1; s.StradHoldClose = t("shold", "HALF"); s.StradLoserExit = t("slex", "ENTRY"); s.StradOpen = t("sopen", "09:30"); s.StradClose = t("sclose", "16:00");
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
    // 📡 one entry signal (taken or not) and what the price did after it — the learning's raw data (user's request: learn which entries were a
    // bad idea, e.g. a break at the very high with no pullback). Mfe = the best move for it, Mae = the worst against it ($ of gold per unit).
    public sealed class SignalRec
    {
        public DateTime Time; public int Dir; public double Price, TargetMove; public string Why = "", Ctx = ""; public bool Taken, Done;
        public double Mfe15, Mae15, Mfe30, Mae30, Mfe60, Mae60; public double MinToTarget = -1, MinToAgainst = -1;
        static readonly System.Globalization.CultureInfo IC = System.Globalization.CultureInfo.InvariantCulture;
        public const string Header = "time,side,price,taken,why,best15,worst15,best30,worst30,best60,worst60,target_move,min_to_target,min_to_same_against,context";
        public string Csv() { return string.Join(",", Time.ToString("yyyy-MM-dd HH:mm:ss", IC), Dir > 0 ? "BUY" : "SELL", Price.ToString("0.00", IC), Taken ? "yes" : "no", "\"" + Why.Replace("\"", "'").Replace(",", " ") + "\"", Mfe15.ToString("0.00", IC), Mae15.ToString("0.00", IC), Mfe30.ToString("0.00", IC), Mae30.ToString("0.00", IC), Mfe60.ToString("0.00", IC), Mae60.ToString("0.00", IC), TargetMove.ToString("0.00", IC), MinToTarget.ToString("0", IC), MinToAgainst.ToString("0", IC), "\"" + Ctx + "\""); }
    }
    public sealed class AutoIteration
    {
        public bool ProtectArmed, BeLockArmed;   // 🔐 the basket reached the protect level once
        public int N; public DateTime Start, End; public string FirstDir = "", Reason = ""; public int Buys, Sells, AddOns, WinnerAdds; public string Ctx = "";   // Ctx: the market when it started (setup, VWAP side, FVG, order flow, session …) for LEARN
        public bool Manual; public int ManualAdds;
        // ✋ the lots added by hand (kept in Ctx so it survives every save): "manlots=1.00"
        public double ManualLots
        {
            get { foreach (var kv in (Ctx ?? "").Split(';')) if (kv.StartsWith("manlots=")) { double v; if (double.TryParse(kv.Substring(8), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v; } return 0; }
            set { var l = (Ctx ?? "").Split(';').Where(x => x != "" && !x.StartsWith("manlots=")).ToList(); l.Add("manlots=" + Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture)); Ctx = string.Join(";", l); }
        }
        public double Fees; public readonly Dictionary<long, double> FeeByTicket = new Dictionary<long, double>();   // COMMISSIONS of the iteration: each position's open commission + the same again for its close (in / out deals)
        public double BuyLots, SellLots, MaxGross, MaxDd, Result, BalanceStart, BalanceEnd, Estimate, Peak, CloseBid; public bool Adopted;
        public double AdoptPl;   // 09d: the open P/L the positions ALREADY had when an unknown basket was taken over — not earned by this iteration
        // AFTER THE TARGET (user's request, to train the algo): a TARGET basket is followed as if it were still open — its best P/L afterwards
        // and when, when it fell back to the target and to breakeven (watched up to 8 hours). AfterMax = NaN: not followed.
        public double AfterMax = double.NaN, AfterMaxMin = -1, AfterBackTargetMin = -1, AfterBreakevenMin = -1, AfterMove; public bool AfterDone;
        public bool Done { get { return End > DateTime.MinValue; } }
        public bool Won { get { return Done && Result > 0; } }
        public bool Unverified { get { return (Reason ?? "").Contains("unverified"); } }   // 09f: listed (grey) but never counted in NET / TODAY / win stats
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
        public bool Locked; public double LockHi = double.NaN, LockLo = double.NaN; public double LockFarHi = double.NaN, LockFarLo = double.NaN;   /* 09m: the REAL unlock prices (RISK GUARD: one average M1 candle × GuardBreak past the range; otherwise the range + the spread) — the chart labels show these */ string pendSide = ""; int liftDir, lockFrom;   // lockFrom: the add-ons count after the last unlock — a new lock needs LockAfter NEW hedges after it
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
        public double BasketTotal, Banked;   // BasketTotal = Basket (open) + Banked (closed legs of this iteration, scale-outs / trims)
        // 🧮 09l (user's confusion): the OPEN iteration's numbers = (TOTAL if closed now, open, closed legs) — the headline everywhere is the TOTAL;
        // running → the engine's own; stopped (⏸ STOP ON MANUAL) → the 🎯 guard's count of the iteration's deals; NaN total = no open iteration
        // 09m (user's catch: "it says it unlocks above / below, the price gets there and it stays locked"): what the unlock ALSO needs besides the price.
        // RISK GUARD: the EMA 50 / 200 trend the same way as the break — unless PROFIT LOCK (ProfitUnlock) and the basket is ≥ $0; "" = the price alone unlocks
        public static string UnlockNeeds(int dir, bool riskGuard, bool profitUnlock, double total, int trendNow, bool haveEma)
        {
            if (!riskGuard || !haveEma || (profitUnlock && total >= 0)) return "";
            string want = dir > 0 ? "UP" : "DOWN", now = trendNow > 0 ? "UP" : trendNow < 0 ? "DOWN" : "flat";
            return trendNow == dir ? " + trend " + want + " ✔" : " + the trend " + want + " (EMA 50/200 now " + now + ")";
        }
        public Tuple<double, double, double> LiveTotal()
        {
            if (Current == null || Current.Done) return Tuple.Create(double.NaN, 0.0, 0.0);
            if (!Running && !double.IsNaN(GuardTotal)) return Tuple.Create(GuardTotal, GuardOpen, Math.Round(GuardTotal - GuardOpen, 2));
            return Tuple.Create(BasketTotal, Basket, Banked);
        }
        static string Sg(double v) { return v.ToString("+0.00;−0.00;0.00", IC); }
        public static string Usd(double v) { return (v > 0 ? "+" : v < 0 ? "−" : "") + "$" + Math.Abs(v).ToString("N2", IC); }   // = the window's Signed(v, "USD")
        // the ITERATIONS table's RESULT (NET) for the OPEN row: (the text "(−70.74)", its tooltip, the value that colours it)
        public static Tuple<string, string, double> OpenRowResult(double total, double open, double closed)
        {
            return Tuple.Create("(" + Sg(total) + ")", "if closed now: open " + Sg(open) + " + closed " + Sg(closed) + " = " + Sg(total) + " (this iteration, commissions and swap included)", total);
        }
        // the 🛡 HEDGE banner's big number: (caption, the big text, the small "open • closed" line, the value that colours it)
        public static Tuple<string, string, string, double> BannerHeadline(double total, double open, double closed, double target, Func<double, string> fmt = null)
        {
            fmt = fmt ?? Usd;
            return Tuple.Create("TOTAL IF CLOSED NOW (open + this iteration's closed legs)", fmt(total) + "  /  +$" + target.ToString("0.##", IC), "open " + fmt(open) + "  •  closed " + fmt(closed), total);
        }
        // the 📈 chart strip's pill for the symbol: (caption, the big text, the value that colours it); closed legs → the big number is the TOTAL, the open part small
        public static Tuple<string, string, double> PillHeadline(string sym, bool mine, double total, double open, double closed, double openPl, bool any, Func<double, string> fmt = null)
        {
            fmt = fmt ?? Usd;
            if (mine && !double.IsNaN(total) && Math.Abs(closed) >= 0.005) return Tuple.Create(sym + " • TOTAL IF CLOSED NOW  (open " + fmt(open) + " • closed " + fmt(closed) + ")", fmt(total), total);
            return Tuple.Create(sym + " • P/L of this instrument", any ? fmt(openPl) : "no ticket", openPl);
        }
        // ⚙ SETTINGS: the live preview next to 💰 LOTS PER $1,000 / TARGET $ PER LOT (the engine's own SizeFor, RUNNER = the nearest step)
        public static string SizePreview(double per1k, double targetPerLot, double balance, bool runner, Mt5Symbol sym = null)
        {
            if (!(per1k > 0)) return "0 = the boxes (ENTRY LOTS / TARGET used as typed)";
            if (!(balance > 0)) return "→ the lots follow the balance (no balance yet)";
            var sz = SizeFor(balance, balance, false, sym, per1k, Math.Max(1, targetPerLot), runner);
            return "→ " + sz.Item1.ToString("0.00", IC) + " lots now at $" + balance.ToString("N0", IC) + " · target $" + sz.Item2.ToString("0.##", IC) + (sz.Item3 != "" ? "\n" + sz.Item3 : "");
        }
        public double NetLots, PerPrice, TargetPx = double.NaN, StopPx = double.NaN; public int LastDir;   // for the chart's guides while a trade is open
        // the order path: (command, args, label) → the command's id, or null with the reason in `refused`
        public Func<string, object[], string, string> Send;
        public string Refused = "";
        // ✋ MANUAL MANAGING (user's request): AUTO keeps reading, tracking and planning, but SENDS NOTHING — every order / close it would send
        // becomes a 💡 SUGGESTION on the chart and in the story (you place it yourself; a trade you open by hand is adopted as always).
        // Only the ⛔ HARD STOPS (whole-basket loss / margin level, the user's own "it is what it is" rule) still close by themselves.
        // ⏸ STOP ON MANUAL (09d): anything done by hand on the symbol (SpasiX chart / tickets / lines, or MT5 desktop / mobile / web) stops the
        // automation AT ONCE, without a question — nothing is closed or sent by the stop; ▶ RESUME AUTOMATION takes the basket over again
        public bool HandStopped; public string HandWhat = ""; public DateTime HandAt; public event Action<string> OnHandStop;
        DateTime handFrom = DateTime.MaxValue; readonly HashSet<long> handSeen = new HashSet<long>(); readonly Dictionary<long, Tuple<double, double>> handSlTp = new Dictionary<long, Tuple<double, double>>(); readonly HashSet<long> handOrders = new HashSet<long>(), handStartPos = new HashSet<long>();
        DateTime handWait = DateTime.MinValue; IBroker lastBroker;
        static bool BrokerClosed(Snap sn, DateTime since) { return sn.Deals.Any(d => d.Time >= since && d.Entry == "OUT" && (d.Reason == "SL" || d.Reason == "TP" || d.Reason == "SO" || d.Reason == "STOPOUT")); }   // the broker closed a leg (not you)
        public bool StopsOnHand { get { return Running && Set != null && Set.OnManual == "STOP" && !ManualManage; } }
        public void HandStop(string what)
        {
            if (!StopsOnHand) return;
            stopKnown = new HashSet<long>(engineSeen); ManualAction(what); HandStopped = true; HandWhat = what; HandAt = DateTime.Now; Running = false; Phase = "OFF"; pendId = null;
            StopReason = "⏸ you traded by hand: " + what;
            TellNow("⏸ AUTOMATION STOPPED — you traded by hand (" + what + "). Your positions are untouched (nothing was closed or sent). To continue: 🛡 HEDGE ▸ ▶ RESUME AUTOMATION.");
            Version++; if (OnHandStop != null) try { OnHandStop(what); } catch { }
        }
        void HandBaseline(Snap sn)
        {
            handFrom = sn.Now; handSeen.Clear(); foreach (var d in sn.Deals) handSeen.Add(d.Ticket);
            handSlTp.Clear(); foreach (var p in sn.Pos) handSlTp[p.Ticket] = Tuple.Create(p.Sl, p.Tp); handOrders.Clear(); foreach (var o in sn.Orders) handOrders.Add(o.Ticket);
        }
        // true = it stopped (the caller returns): a deal from MT5 by hand, an SL / TP changed, a pending order placed — the automation never does these
        bool HandCheck(Snap sn, bool real)
        {
            if (!StopsOnHand) { HandBaseline(sn); return false; }
            foreach (var d in sn.Deals) if (handSeen.Add(d.Ticket) && real && d.Time >= handFrom.AddSeconds(-2) && IsHandDeal(d)) { HandStop(HandDeal(d)); return true; }
            foreach (var p in sn.Pos) { Tuple<double, double> was; if (handSlTp.TryGetValue(p.Ticket, out was) && (Math.Abs(was.Item1 - p.Sl) > 1e-9 || Math.Abs(was.Item2 - p.Tp) > 1e-9)) { HandStop("SL / TP of #" + p.Ticket + " " + p.Side + " " + p.Lots.ToString("0.##", IC) + " changed to " + (p.Sl > 0 ? "SL " + P(p.Sl) : "no SL") + " / " + (p.Tp > 0 ? "TP " + P(p.Tp) : "no TP") + ", " + Now.ToString("HH:mm:ss", IC)); return true; } handSlTp[p.Ticket] = Tuple.Create(p.Sl, p.Tp); }
            foreach (var o in sn.Orders) if (!handOrders.Contains(o.Ticket)) { HandStop("a pending " + o.Type + " " + o.Lots.ToString("0.##", IC) + " @ " + P(o.Price) + " placed, " + Now.ToString("HH:mm:ss", IC)); return true; }
            handOrders.Clear(); foreach (var o in sn.Orders) handOrders.Add(o.Ticket);
            return false;
        }
        string HandGross(Snap sn)
        {
            double g = Gross(sn.Pos);
            if (g > knownGross + 1e-6) { var p = sn.Pos.OrderBy(x => x.Time).ThenBy(x => x.Ticket).Last(); return p.Side + " " + (g - knownGross).ToString("0.##", IC) + " @ " + P(p.Open) + " opened outside the automation, " + Now.ToString("HH:mm:ss", IC); }
            return (knownGross - g).ToString("0.##", IC) + " lots closed outside the automation, " + Now.ToString("HH:mm:ss", IC);
        }
        public bool NoCatchUp; bool catchNoted;
        // ⚠ DANGER RADAR (09h): read once the minute of each new hedge has finished (as the research sampled it); OnRadar = the tab's banner / toast
        public RadarReading Radar; public event Action<RadarReading> OnRadar; public Func<string, List<Mt5Bar>> RadarBars; public int RadarWfYear; public Func<IEnumerable<Tuple<RadarNeighbour, double[]>>> RadarMine;
        DateTime radarDue = DateTime.MinValue; int radarIter = -1; bool radarBeArmed; string radarSkipNote = "";
        void RadarStep(IBroker b, Snap sn, AutoIteration it)
        {
            if (radarIter != it.N) { radarIter = it.N; Radar = null; radarBeArmed = false; radarSkipNote = ""; }
            if (radarDue == DateTime.MinValue || Now < radarDue) return; var due = radarDue; radarDue = DateTime.MinValue;
            List<Mt5Bar> bars = null; try { bars = RadarBars != null ? RadarBars(Set.Symbol) : null; } catch { }
            if (bars == null) lock (b.State.Gate) { var all = b.State.SeriesOf(Set.Symbol, 1).Bars; int j = all.Count; while (j > 0 && all[j - 1].Time >= due.AddDays(-6)) j--; bars = all.Skip(j).Where(x => x.Time < due.AddMinutes(1)).ToList(); }   // 6 days are enough (the ATR / EMA settle)
            var r = DangerRadar.Read(RadarModel.Default, bars, due, it.Start, Tot(sn, it), Set.Target, sn.Pos.Sum(p => p.Dir * p.Lots), it.AddOns, RadarMine != null ? RadarMine() : null, RadarWfYear > 0 ? Math.Max(2021, Math.Min(2026, Now.Year)) : 0);
            if (RadarWfYear > 0 && Now.Year < 2021) r.P = double.NaN;   // the bench: no walk-forward model before 2021 → no action
            Radar = r; Version++;
            if (!double.IsNaN(r.P) && Set.RadarBe && r.P * 100 >= Set.RadarAt && !radarBeArmed) { radarBeArmed = true; Tell("⚠ RADAR → CLOSE AT THE NEXT BREAKEVEN (your option). WHY: " + r.Text + " ≥ " + Set.RadarAt.ToString("0", IC) + " % → everything closes as soon as the total is back at +$" + Set.GuardKeep.ToString("0.##", IC) + "."); }
            if (OnRadar != null) try { OnRadar(r); } catch { }
        }
        bool RadarHigh { get { return Radar != null && !double.IsNaN(Radar.P) && Radar.P * 100 >= Set.RadarAt; } }
        // 🤖 WHAT IF (09g): the basket after the last step with nothing done by hand — at the FIRST manual action of an iteration it is handed out once
        public event Action<AutoIteration, List<WhatIfLeg>, double, DateTime, string> OnManualAction;
        List<WhatIfLeg> cleanLegs; double cleanBanked; DateTime cleanAt; int wiIter = -1;
        void CleanSnap(Snap sn) { cleanLegs = sn.Pos.Select(p => new WhatIfLeg { Ticket = p.Ticket, Side = p.Side, Lots = p.Lots, Open = p.Open, Time = p.Time }).ToList(); cleanBanked = Current != null ? Math.Round(BasketTotal - Basket, 2) : 0; cleanAt = Now; }
        void ManualAction(string what)
        {
            var it = Current; if (it == null || it.Done || wiIter == it.N || OnManualAction == null) return; wiIter = it.N;
            try { OnManualAction(it, cleanLegs != null ? cleanLegs.ToList() : new List<WhatIfLeg>(), cleanBanked, cleanAt > DateTime.MinValue ? cleanAt : Now, what); } catch { }
        }   // ▶ RESUME without the catch-up order (09f): a hedge line already passed at the start is not acted on
        // ▶ what RESUME / START would do AT ONCE (09f): a dry run on a throw-away engine — nothing is sent (its orders are only written down)
        public static List<string> PreviewStart(IBroker b, AutoSettings s, Func<OpenSave> restore, IEnumerable<AutoIteration> history)
        {
            var said = new List<string>(); var p = new HedgeAuto(); p.Send = (c, a, l) => { said.Add(l); return null; }; p.RestoreOpen = restore; if (history != null) p.Iterations.AddRange(history.Where(x => x.Done));
            try { p.Start(b, s.Copy(), true); p.Step(b); } catch { }
            return said;
        }
        public static string PreviewText(List<string> said)
        {
            if (said == null || said.Count == 0) return "";
            return "⚠ on " + "resume it will IMMEDIATELY: " + string.Join(" • ", said.Select(x => { int c = x.IndexOf("(🤖"); var what = c > 0 ? x.Substring(0, c).Trim() : x; var why = c > 0 ? x.Substring(c).Trim('(', ')', ' ') : ""; int dash = why.IndexOf(" — "); return what + (dash > 0 ? " — " + why.Substring(dash + 3) : ""); })) + " (the price is already past its line). Pick \"WITHOUT THE CATCH-UP ORDER\" to wait for the next signal instead.";
        }
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

        int lastDir; string blockedKey = ""; double prevBid = double.NaN, prevBuyLvl = double.NaN, prevSellLvl = double.NaN, prevFormH = double.NaN, prevFormL = double.NaN, knownGross; DateTime prevFormT; int engDir; DateTime engBar; string engStory = "", vwapSkip = ""; int hEngDir; DateTime hEngBar; public int AggDir; public double AggLevel = double.NaN; public DateTime AggBar; string hEngStory = ""; long pendTicket; bool closedByUs;
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
            return string.Join("|", x.N.ToString(IC), x.Start.Ticks.ToString(IC), x.FirstDir, x.Buys.ToString(IC), x.Sells.ToString(IC), x.AddOns.ToString(IC), x.BuyLots.ToString("R", IC), x.SellLots.ToString("R", IC), x.MaxGross.ToString("R", IC), x.MaxDd.ToString("R", IC), x.BalanceStart.ToString("R", IC), x.Peak.ToString("R", IC), (x.Ctx ?? "").Replace("|", " "), x.Manual ? "1" : "0", string.Join(";", tickets.Select(t => t.ToString(IC))), Now.Ticks.ToString(IC), string.Join(";", x.FeeByTicket.Select(kv => kv.Key.ToString(IC) + "=" + kv.Value.ToString("R", IC))), x.ManualAdds.ToString(IC));
        }
        public static OpenSave ParseOpen(string line)
        {
            try
            {
                var a = (line ?? "").Split('|'); if (a.Length < 16) return null; Func<int, double> d = i => double.Parse(a[i], IC);
                var it = new AutoIteration { N = int.Parse(a[0], IC), Start = new DateTime(long.Parse(a[1], IC)), FirstDir = a[2], Buys = int.Parse(a[3], IC), Sells = int.Parse(a[4], IC), AddOns = int.Parse(a[5], IC), BuyLots = d(6), SellLots = d(7), MaxGross = d(8), MaxDd = d(9), BalanceStart = d(10), Peak = d(11), Ctx = a[12], Manual = a[13] == "1" };
                var o = new OpenSave { It = it, SavedAt = new DateTime(long.Parse(a[15], IC)) }; foreach (var t in a[14].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) o.Tickets.Add(long.Parse(t, IC));
                if (a.Length > 16) foreach (var kv in a[16].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { var q = kv.Split('='); if (q.Length == 2) it.FeeByTicket[long.Parse(q[0], IC)] = double.Parse(q[1], IC); }
                if (a.Length > 17) it.ManualAdds = int.Parse(a[17], IC);   /* ✋ hand-hedged legs survive a restart */
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
        // 09d: the next iteration number continues the history (#40 after #39), whatever was cleared or loaded
        int NextN() { return Iterations.Count == 0 ? 1 : Iterations.Max(x => x.N) + 1; }
        public void Note(string s) { var line = (Now > DateTime.MinValue ? Now : DateTime.Now).ToString("dd MMM HH:mm:ss", IC) + "  " + s; if (OnNote != null) try { OnNote(0, line); } catch { } Log.Add(line); if (Log.Count > 3000) Log.RemoveRange(0, 1000); Version++; }

        // ---- start / stop
        // a problem that stops the start ("" = fine); adopt = the positions already open on the symbol become the current iteration
        public string Check(IBroker b, AutoSettings s)
        {
            if (s.TrendStack && (b == null || !AutoSettings.TrendStackAllowed(b.IsSim))) return AutoSettings.TrendStackNo;   // 🚀 09e: never on an MT5 account
            if (s.IsRunner && (b == null || !AutoSettings.RunnerAllowed(b.IsSim))) return AutoSettings.RunnerNo;   // 🦅 09k: never on an MT5 account
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
            var oldIt = Current;   // 09h: an iteration left open by a stop
            Set = s.Copy(); Running = true; StraddleRef = double.NaN; LadderHi = LadderLo = double.NaN; Locked = false; lockFrom = 0; StopReason = ""; blockedKey = "~"; lastDir = 0; engDir = 0; vwapSkip = ""; AggDir = 0; AggLevel = double.NaN; prevBid = double.NaN; pendId = null; PauseUntil = DateTime.MinValue; Current = null;
            HandStopped = false; HandWhat = ""; handFrom = DateTime.MinValue; NoCatchUp = false; catchNoted = false; Snap sn = Read(b); Now = sn.Now; HandBaseline(sn); stopKnown = null; engineSeen.Clear(); foreach (var p in sn.Pos) engineSeen.Add(p.Ticket);
            if (oldIt != null && !oldIt.Done && sn.Pos.Count == 0) CloseStopped(oldIt, sn.Deals, s.Symbol, sn.Now, "");   // 09h: RESUME on a flat account — the old iteration closes first (never "open (+0.00)" forever) handStartPos.Clear(); foreach (var p in sn.Pos) handStartPos.Add(p.Ticket);
            Note("▶ START " + Set.Symbol + " " + Tf.Name(Set.Tf) + " • entry " + Set.EntryLots.ToString("0.##", IC) + " • add-on " + Set.AddLots.ToString("0.##", IC) + " • target $" + Set.Target.ToString("0.##", IC) + " • max drawdown " + (Set.MaxDrawdown > 0 ? "$" + Set.MaxDrawdown.ToString("0.##", IC) : "none") + " • first " + Set.FirstDir + " • next " + (Set.NextStart == "PAUSE" ? "pause " + Set.PauseSec + " s" : "the next break"));
            if (sn.Pos.Count > 0 && adopt)
            {
                var last = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).Last(); lastDir = NetSide(sn.Pos, last.Dir);
                Current = new AutoIteration { N = NextN(), Start = Now, FirstDir = sn.Pos.OrderBy(p => p.Time).First().Side, BalanceStart = sn.Balance, Adopted = true, Buys = sn.Pos.Count(p => p.Dir > 0), Sells = sn.Pos.Count(p => p.Dir < 0), BuyLots = sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots), SellLots = sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots) };
                Current.AddOns = Math.Max(0, sn.Pos.Count - 1);
                // the SAME basket as before the restart (its positions match the saved open iteration): it keeps its number, its start, its numbers
                var sv = RestoreOpen != null ? RestoreOpen() : null; bool same = false;
                if (sv != null && sv.Tickets.Count > 0 && sn.Pos.Any(p => sv.Tickets.Contains(p.Ticket)) && sn.Pos.All(p => sv.Tickets.Contains(p.Ticket) || p.Time >= sv.SavedAt))   /* 09d: at least one ticket of the saved basket must still be open — a basket of ANOTHER account (all positions newer than the save) is not "the same" */
                {
                    var o = sv.It; Current.N = o.N; Current.Start = o.Start; Current.FirstDir = o.FirstDir; Current.BalanceStart = o.BalanceStart; Current.MaxDd = o.MaxDd; Current.MaxGross = Math.Max(o.MaxGross, Gross(sn.Pos)); Current.Peak = o.Peak; Current.Ctx = o.Ctx;
                    Current.Buys = Math.Max(Current.Buys, o.Buys); Current.Sells = Math.Max(Current.Sells, o.Sells); Current.AddOns = Math.Max(Current.AddOns, o.AddOns); Current.Adopted = false; Current.Manual = o.Manual; Current.ManualAdds = Math.Max(Current.ManualAdds, o.ManualAdds); same = true;
                    foreach (var kv in o.FeeByTicket) Current.FeeByTicket[kv.Key] = kv.Value;
                }
                if (!same)
                {   // no saved open iteration (an older build): its trades in the history still tell when it began
                    var trs = Trades.Where(t => t.Iter == Current.N && t.Side != "CLOSE").OrderBy(t => t.Time).ToList();
                    if (trs.Count > 0 && trs[0].Time < Now && sn.Pos.All(p => trs.Any(t => t.Side == p.Side && Math.Abs((t.Time - p.Time).TotalSeconds) <= 5))) {   /* 09d: the history's trades must BE these positions (an unfinished #N of an earlier run had moved the start back, and its old deals counted as banked) */ Current.Start = trs[0].Time; Current.FirstDir = trs[0].Side; Current.Buys = Math.Max(Current.Buys, trs.Count(t => t.Side == "BUY")); Current.Sells = Math.Max(Current.Sells, trs.Count(t => t.Side == "SELL")); Current.AddOns = Math.Max(Current.AddOns, trs.Count - 1); Current.MaxDd = Math.Min(0, trs.Min(t => t.Basket)); Current.Adopted = false; same = true; }
                }
                if (!same) { Current.AdoptPl = Math.Round(BasketOf(sn), 2); if (Math.Abs(Current.AdoptPl) >= 0.01) Note("⤴ taken over with " + M(Current.AdoptPl) + " already open — the iteration's result counts only what it makes from here"); }
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
        sealed class Snap { public readonly Dictionary<long, double> InComm = new Dictionary<long, double>(); /* each open position's opening commission from MT5's deals (a real account books it on the deal, not on the position) */ public DateTime Now; public double Bid = double.NaN, Ask = double.NaN, Balance, MarginLevel, Equity; public List<Mt5Position> Pos = new List<Mt5Position>(); public List<Mt5Bar> Done = new List<Mt5Bar>(), Fin = new List<Mt5Bar>(), FinFvg = new List<Mt5Bar>(); public bool Loaded; public Tuple<bool, string> Res; public double PerPrice; public double FormH = double.NaN, FormL = double.NaN; public DateTime FormT; public Mt5Symbol Sym; public List<Mt5Deal> Deals = new List<Mt5Deal>(); public List<Mt5Bar> M1 = new List<Mt5Bar>(); public List<Mt5Order> Orders = new List<Mt5Order>(); public double DayHi = double.NaN, DayLo = double.NaN; public int PendingOrders; }   // Form* = the forming candle (its high / low catch a break between two price updates)   // PerPrice = $ per 1.0 of price per lot
        Snap Read(IBroker b)
        {
            var sn = new Snap();
            lock (b.State.Gate)
            {
                var st = b.State; sn.Now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; Mt5Quote q; if (st.Quotes.TryGetValue(Set.Symbol, out q)) { sn.Bid = q.Bid; sn.Ask = q.Ask; }
                sn.Balance = st.Account.Balance; sn.MarginLevel = st.Account.MarginLevel; sn.Equity = st.Account.Equity; Mt5Symbol ms; if (st.Symbols.TryGetValue(Set.Symbol, out ms) && ms.TickSize > 0) sn.PerPrice = ms.TickValue / ms.TickSize; if (ms != null) sn.Sym = ms;
                foreach (var p in st.Positions) if (string.Equals(p.Symbol, Set.Symbol, StringComparison.OrdinalIgnoreCase)) sn.Pos.Add(p);
                if (sn.Pos.Count > 0) { var tks = new HashSet<long>(sn.Pos.Select(p => p.Ticket)); foreach (var d in st.Deals) if (d.Entry == "IN" && tks.Contains(d.Position)) { double c0; sn.InComm.TryGetValue(d.Position, out c0); sn.InComm[d.Position] = c0 + Math.Abs(d.Commission); } }
                var se = st.SeriesOf(Set.Symbol, Set.Tf); sn.Loaded = se.Bars.Count > 0;
                sn.PendingOrders = st.Orders.Count(o => string.Equals(o.Symbol, Set.Symbol, StringComparison.OrdinalIgnoreCase)); sn.Orders = st.Orders.Where(o => string.Equals(o.Symbol, Set.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
                {   // 09d: the deals since the run / the iteration began (the iteration's own result, the ✋ stop-on-manual check)
                    var from = Current != null && Current.Start < handFrom ? Current.Start : handFrom; from = new DateTime(from.Ticks - from.Ticks % TimeSpan.TicksPerSecond);
                    for (int i = st.Deals.Count - 1; i >= 0; i--) { var d = st.Deals[i]; if (d.Time < from) { if (st.Deals.Count - i > 400) break; continue; } if (d.Type == "BALANCE" || string.Equals(d.Symbol, Set.Symbol, StringComparison.OrdinalIgnoreCase)) sn.Deals.Add(d); }
                }
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
                if (Set.TrendStack || Set.AddOnBreak || AddProtector.On(Set.ProtectAdd, b.IsSim))
                {   // 🚀 the trend detector: up to 200 finished M1 candles, oldest first
                    var s1 = Set.Tf == 1 ? se : st.SeriesOf(Set.Symbol, 1); int from1 = Math.Max(0, s1.Bars.Count - 220);
                    for (int i = from1; i < s1.Bars.Count; i++) if (s1.Bars[i].Time.AddMinutes(1) <= sn.Now) sn.M1.Add(s1.Bars[i]);
                    if (!b.IsSim && !s1.Loaded && (DateTime.Now - s1.AskedAt).TotalSeconds > 5) { s1.AskedAt = DateTime.Now; b.Send("BARS|" + Set.Symbol + "|1|300"); }
                }
                if (Set.EntryMode.StartsWith("FVG") || Set.EntryMode == "MULTI")
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
        // 🐞 audit: the iteration's total = the open basket (profit + swap − its opening AND closing commission) + what the balance moved since the
        // start — the balance already holds the opening commission of the legs still open (booked on the IN deal), so it is added back once
        double Tot(Snap sn, AutoIteration it) { return it == null ? Basket : Basket + sn.Balance - it.BalanceStart + sn.Pos.Sum(p => OpenComm(sn, p)); }
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
            TrackAfter(b); lastBroker = b;
            if (!Running) return;
            if (Set.TrendStack && !b.IsSim) { Stop(AutoSettings.TrendStackNo); return; }   // 🚀 defence in depth: an experiment never runs on MT5
            if (Next != null && Current == null) ApplyNext();   // between iterations: the saved changes take over
            if (Set.IsRunner && !b.IsSim) { Stop(AutoSettings.RunnerNo); return; }   // 🦅 defence in depth: the experiment never runs on MT5
            var sn = Read(b); if (sn.Now < (b.IsSim ? Now : Now.AddMinutes(-1))) { Stop("the clock went back (the backtest was rewound or restarted)"); return; }
            Now = sn.Now; if (pendId == null && HandCheck(sn, !b.IsSim)) return;
            if (double.IsNaN(sn.Bid) || sn.Bid <= 0) return;
            if (!b.IsSim && (sn.Bid != tapeBid || sn.Ask != tapeAsk))
            {   // 🎞 the REAL ticks as they came (user's request: the post-mortem must replay what really happened, not a guess inside each candle)
                tapeBid = sn.Bid; tapeAsk = sn.Ask; TickTape.Add(new SimTick { T = Now, Bid = sn.Bid, Ask = double.IsNaN(sn.Ask) ? sn.Bid : sn.Ask });
                if (TickTape.Count > 250000) TickTape.RemoveRange(0, 50000);
            }
            Basket = sn.Pos.Count > 0 ? BasketOf(sn) : 0;
            if ((Set.TrendStack || Set.AddOnBreak) && sn.M1.Count > 0 && sn.M1[sn.M1.Count - 1].Time != trendBar) { trendBar = sn.M1[sn.M1.Count - 1].Time; Trend = TrendDetect.Read(sn.M1, Vwap); Version++; }   // 🚀 the trend on every new finished M1 candle (the banner shows it even while waiting)
            foreach (var sg in Journal) if (!sg.Done)
            {   // 📡 the signal journal: what the price did after every signal, taken or not
                double px = sg.Dir > 0 ? sn.Bid : (double.IsNaN(sn.Ask) ? sn.Bid : sn.Ask), mv = sg.Dir * (px - sg.Price), mins = (Now - sg.Time).TotalMinutes;
                if (mins <= 15) { sg.Mfe15 = Math.Max(sg.Mfe15, mv); sg.Mae15 = Math.Min(sg.Mae15, mv); }
                if (mins <= 30) { sg.Mfe30 = Math.Max(sg.Mfe30, mv); sg.Mae30 = Math.Min(sg.Mae30, mv); }
                sg.Mfe60 = Math.Max(sg.Mfe60, mv); sg.Mae60 = Math.Min(sg.Mae60, mv);
                if (sg.TargetMove > 0 && sg.MinToTarget < 0 && mv >= sg.TargetMove) sg.MinToTarget = mins; if (sg.TargetMove > 0 && sg.MinToAgainst < 0 && mv <= -sg.TargetMove) sg.MinToAgainst = mins;
                if (mins >= 60) { sg.Done = true; if (OnSignalDone != null) try { OnSignalDone(sg); } catch { } }
            }
            if (Journal.Count > 400) Journal.RemoveAll(x => x.Done);
            foreach (var he in Hedges) if (!he.Done)
            {   // what the price did after each hedge (in $ of gold per unit): its best and worst within 30 minutes
                double mv = he.Dir * (sn.Bid - he.Price); if (mv > he.Best) he.Best = mv; if (mv < he.Worst) he.Worst = mv;
                if (Now >= he.Time.AddMinutes(30)) { he.Done = true; if (OnHedgeDone != null) OnHedgeDone(he); }
            }
            NetLots = sn.Pos.Sum(p => p.Dir * p.Lots); PerPrice = sn.PerPrice; LastDir = lastDir; SwapFigures(sn);
            double move = Math.Abs(NetLots) > 1e-9 && sn.PerPrice > 0 ? NetLots * sn.PerPrice : 0;   // $ per 1.0 of price on what is open
            BasketTotal = Current != null ? Tot(sn, Current) : Basket; Banked = Current != null ? BasketTotal - Basket : 0;   // what the exits count: open + the legs already closed in this iteration
            TargetPx = Current != null && move != 0 ? sn.Bid + (Set.Target - (Set.Compress || Set.ScaleOut || Set.RiskGuard ? BasketTotal : Basket)) / move : double.NaN;   // 🐞 audit: the TP line counts what the exit counts
            if (Current != null && move != 0 && RescueActive(Current)) TargetPx = sn.Bid + (RescueKeep(sn) - (Tot(sn, Current))) / move;   // 🛟 the line = break-even
            StopPx = Current != null && move != 0 && Set.MaxDrawdown > 0 ? sn.Bid + (-Set.MaxDrawdown - Basket - (Set.ExitMode == "LEGS" ? sn.Balance - Current.BalanceStart : 0)) / move : double.NaN;
            prevBuyLvl = BuyLevel; prevSellLvl = SellLevel; BuyLevel = High(sn); SellLevel = Low(sn);
            if (Set.FullAuto && sn.Fin.Count > 0 && sn.Fin[0].Time != readAt) { readAt = sn.Fin[0].Time; double a0, p0; int u0, d0; ReadMarket(sn, out a0, out p0, out u0, out d0); }   // 🤖 the market reading, once a closed candle
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
                if (pendKind == "CLOSE1") { knownGross = Gross(sn.Pos); closedByUs = true; SyncSideToNet(sn); }
                if (pendKind == "PAIR")
                {   // the losing leg is closed: now its winning partner
                    knownGross = Gross(sn.Pos); closedByUs = true; var w2 = sn.Pos.FirstOrDefault(p => p.Ticket == pairNext);
                    if (w2 != null) { Refused = ""; var id2 = Ask("CLOSE", new object[] { w2.Ticket, 0.0 }, "CLOSE #" + w2.Ticket + " " + w2.Side + " " + w2.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + Current.N + " 🛟 pair-off)"); if (id2 != null) { pendId = id2; pendKind = "PAIR2"; pendTicket = w2.Ticket; pendSent = DateTime.Now; pendOk = DateTime.MinValue; return; } }
                }
                if (pendKind == "SCALE") { knownGross = Gross(sn.Pos); closedByUs = true; SyncSideToNet(sn); Version++; }
                if (pendKind == "PAIR2") { knownGross = Gross(sn.Pos); closedByUs = true; SyncSideToNet(sn); Tell("🛟 PAIRED OFF — the basket is now " + Gross(sn.Pos).ToString("0.##", IC) + " lots (" + sn.Pos.Count + " legs), " + M(Basket) + " open. " + Plan(sn)); Version++; }
                if (pendKind == "TRIM") { knownGross = Gross(sn.Pos); closedByUs = true; lastDir = liftDir; quickBest = double.NaN; SetHedgeLevel(sn, liftDir); Tell("🛡 TRIMMED — the basket now leans " + (liftDir > 0 ? "LONG" : "SHORT") + " with the break, " + Gross(sn.Pos).ToString("0.##", IC) + " lots. " + Plan(sn)); Version++; }
                if (pendKind == "CLOSESIDE") { knownGross = Gross(sn.Pos); lastDir = liftDir; SetHedgeLevel(sn, liftDir); Tell("🔓 LIFTED — the " + pendSide + "s are closed; the " + (liftDir > 0 ? "BUYS" : "SELLS") + " run with the break. " + Plan(sn)); Version++; }
            }

            if (Phase == "CLOSING")
            {
                if (Set.Preset == "STRADDLE930" && sn.Pos.Count == 0) { stStage = 0; }
                if (sn.Pos.Count > 0) { if (pendId == null && (DateTime.Now - closeSeen).TotalSeconds > 2) CloseAll(sn, Current.Reason, true); return; }
                Finish(sn); return;
            }
            if (Set.Preset == "STRADDLE930") { Straddle(b, sn); return; }   // ⏰ its own flow (below): nothing else opens or closes

            if (Phase == "IN ITERATION")
            {
                var it = Current;
                if (sn.Pos.Count == 0 && closedByUs && Set.ExitMode == "LEGS") { /* only the LEGS exit closes a basket leg by leg; after a scale-out / trim the last close can still be YOURS (06 Nov #22 was closed by hand) */ it.Reason = "LEG TARGETS"; Tell("ALL LEGS AT THEIR TARGETS — iteration #" + it.N + " done."); Finish(sn); return; }
                if (sn.Pos.Count == 0 && StopsOnHand && pendId == null && !sn.Deals.Any(d => d.Time >= it.Start && d.Entry == "OUT" && (d.Reason == "SL" || d.Reason == "TP" || d.Reason == "SO" || d.Reason == "STOPOUT"))) { it.Reason = "CLOSED BY HAND"; Finish(sn); HandStop("every position on " + Set.Symbol + " closed outside the automation, " + Now.ToString("HH:mm:ss", IC)); return; }
                if (sn.Pos.Count == 0 && (it.Manual || Set.TrackManual)) { it.Reason = "CLOSED BY HAND"; Tell("✋ CLOSED BY HAND — the positions on " + Set.Symbol + " were closed outside the automation; the iteration is recorded and the automation goes on."); Finish(sn); return; }
                if (sn.Pos.Count == 0) { it.Reason = "CLOSED OUTSIDE"; Note("⚠ the positions on " + Set.Symbol + " were closed outside the automation (by hand or a stop-out)"); Finish(sn); return; }
                if (Math.Abs(Gross(sn.Pos) - knownGross) > 1e-6)
                {   // ⏸ 09e fix: a broker SL / TP / stop-out on a leg is NOT a hand trade; on MT5 the deal can come a moment after the position changed → wait ≤ 10 s for it
                    if (StopsOnHand && pendId == null && !BrokerClosed(sn, Now.AddMinutes(-2)))
                    {
                        if (!b.IsSim && !sn.Deals.Any(d => d.Time >= Now.AddSeconds(-60) && IsHandDeal(d))) { if (handWait == DateTime.MinValue) handWait = DateTime.Now; if ((DateTime.Now - handWait).TotalSeconds < 10) return; }
                        handWait = DateTime.MinValue; HandStop(HandGross(sn)); return;
                    }
                    handWait = DateTime.MinValue; ManualAction(HandGross(sn)); Manual(sn, it);
                }
                else if (pendId == null) CleanSnap(sn);
                foreach (var p in sn.Pos) engineSeen.Add(p.Ticket);   // ✋ 09h: the legs the automation knows (a leg not in here when it stops = yours)
                if (pendId == null) { RadarStep(b, sn, it); if (radarBeArmed && Set.RadarBe && sn.Pos.Count > 0 && Tot(sn, it) >= Set.GuardKeep) { it.Reason = "⚠ RADAR BE"; Tell("⚠ RADAR BREAKEVEN — the total " + M(Tot(sn, it)) + " is back at breakeven → CLOSE ALL (your option: close at the next breakeven above DANGER " + Set.RadarAt.ToString("0", IC) + " %)."); CloseAll(sn, "⚠ RADAR BE", false); return; } }
                if (EndOfDay())
                {
                    if (Basket < 0 && Set.EodLoss != "CLOSE")
                    {   // hedging never closes at a loss: the basket stays open and keeps being managed (hedges, the target); only new iterations wait for tomorrow
                        if (eodHeld != it.N + "|" + Now.Date.Ticks) { eodHeld = it.N + "|" + Now.Date.Ticks; Tell("END OF DAY " + Set.CloseAtNy + " New York: the basket is " + M(Basket) + " → HELD OPEN (a hedge never closes at a loss). It is still managed — the hedges and the +" + M(Set.Target).TrimStart('+') + " target — and no new iteration starts until tomorrow."); }
                    }
                    else { it.Reason = "END OF DAY"; Tell("END OF DAY. WHY: it is " + Set.CloseAtNy + " New York (the close time) → CLOSE ALL at " + M(Basket) + "."); CloseAll(sn, "END OF DAY", false); return; }
                }
                double total = Set.ExitMode == "LEGS" || Set.Compress || Set.TrendStack || Set.AddOnBreak ? Tot(sn, it) : Basket;   // LEGS / 🧹 COMPRESS: what the closed legs made counts too (the worst drawdown, the rescue)
                foreach (var p in sn.Pos) it.FeeByTicket[p.Ticket] = OpenComm(sn, p) * 2 + (Set.CommissionPerLot > 0 ? p.Lots * Set.CommissionPerLot : 0);   // the commissions so far (live)
                it.Fees = it.FeeByTicket.Values.Sum();
                if (OnOpenState != null && (DateTime.Now - openSavedAt).TotalSeconds >= 30) { openSavedAt = DateTime.Now; OnOpenState(OpenText(sn.Pos.Select(p => p.Ticket))); }
                { var pm = new DateTime(Now.Year, Now.Month, Now.Day, Now.Hour, Now.Minute, 0); if (pm != pathMin && OnPath != null) { pathMin = pm; OnPath(string.Join(",", it.N.ToString(IC), Now.Ticks.ToString(IC), sn.Bid.ToString("R", IC), Basket.ToString("0.00", IC), sn.Pos.Sum(p => p.Dir * p.Lots).ToString("0.##", IC), sn.Pos.Count.ToString(IC), Gross(sn.Pos).ToString("0.##", IC), Locked ? "LOCKED" : "")); } }
                it.MaxDd = Math.Min(it.MaxDd, total); it.Peak = Math.Max(it.Peak, Basket); it.MaxGross = Math.Max(it.MaxGross, Gross(sn.Pos));
                if (sn.Pos.Count > 0 && ((Set.HardFloat > 0 && Tot(sn, it) <= -Set.HardFloat / 100 * Math.Max(1, it.BalanceStart)) || (Set.HardMargin > 0 && sn.MarginLevel > 0 && sn.MarginLevel <= Set.HardMargin)))
                {   // ⛔ HARD STOP (user's): it is what it is — close everything before the broker does it worse
                    bool byF = Set.HardFloat > 0 && Tot(sn, it) <= -Set.HardFloat / 100 * Math.Max(1, it.BalanceStart); it.Reason = "HARD STOP";
                    Tell("⛔ HARD STOP. WHY: " + (byF ? "the iteration's loss " + M(Tot(sn, it)) + " (open + realised) reached " + Set.HardFloat.ToString("0", IC) + " % of the balance $" + it.BalanceStart.ToString("N0", IC) : "the margin level " + sn.MarginLevel.ToString("0", IC) + " % is at / under " + Set.HardMargin.ToString("0", IC) + " %") + " → CLOSE EVERYTHING (" + sn.Pos.Count + " positions).");
                    hardNow = true; try { CloseAll(sn, "HARD STOP", false); } finally { hardNow = false; } return;
                }
                DangerNow(sn, it);
                if (Set.BeLock && !Locked && Set.ExitMode != "LEGS" && Set.Target > 0 && sn.PerPrice > 0 && !double.IsNaN(sn.Bid))
                {   // 🔐 BREAKEVEN LOCK
                    double totB = Tot(sn, it), netB = sn.Pos.Sum(p => p.Dir * p.Lots);
                    if (!it.BeLockArmed && totB >= Set.Target * Set.BeLockAt / 100) { it.BeLockArmed = true; Tell("🔐 BREAKEVEN PROFIT ARMED: the basket reached " + M(totB) + " (≥ " + Set.BeLockAt.ToString("0", IC) + " % of the target " + M(Set.Target) + ") — if the price turns back to where it would be " + M(Set.Target * Set.BeLockKeep / 100) + ", " + (Set.BeClose ? "everything CLOSES there at the market (the profit is kept, the iteration ends)." : "the basket LOCKS there (buys = sells: the profit is frozen, it does NOT close — 🔐 AT THE BREAKEVEN LINE = LOCK; choose CLOSE ALL to bank it instead).")); }
                    if (it.BeLockArmed && Math.Abs(netB) > 0.005)
                    {
                        double keep = Set.Target * Set.BeLockKeep / 100, px = netB > 0 ? sn.Bid : sn.Ask; if (double.IsNaN(px)) px = sn.Bid;
                        BeLockPx = px - (totB - keep) / (netB * sn.PerPrice);   // the price where the basket = keep (long: below the price, short: above)
                        if (netB > 0 ? px <= BeLockPx : px >= BeLockPx) { it.BeLockArmed = false; BeLockPx = double.NaN; if (!Set.BeClose) { GuardLock(sn, it, "🔐 BREAKEVEN LOCK — the basket came back from ≥ " + Set.BeLockAt.ToString("0", IC) + " % of the target to " + M(totB) + ": the profit is kept, no drawdown"); return; }
                            it.Reason = "BE PROFIT"; Tell("🔐 BREAKEVEN PROFIT — the basket came back from ≥ " + Set.BeLockAt.ToString("0", IC) + " % of the target to " + M(totB) + " → CLOSE ALL " + sn.Pos.Count + " positions at that profit and keep trading"); CloseAll(sn, "BE PROFIT", false); return; }
                    }
                }
                else BeLockPx = double.NaN;
                if (Set.DangerExit && Set.ExitMode != "LEGS" && DangerPeak >= Set.DangerAt)
                {
                    double tot = Tot(sn, it), keep = Math.Max(0.5, Set.Target * Set.DangerKeep / 100);
                    if (tot >= keep) { it.Reason = "DANGER EXIT"; Tell("🚨 DANGER EXIT (a smaller profit). WHY: this basket's danger meter reached " + DangerPeak + " (≥ " + Set.DangerAt.ToString("0", IC) + ": " + DangerPeakWhy + ") and the basket is back at " + M(tot) + " (≥ " + M(keep) + " = " + Set.DangerKeep.ToString("0", IC) + " % of the target) → take it, CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "DANGER EXIT", false); return; }
                }
                if (Set.Rescue && Set.ExitMode != "LEGS" && RescueActive(it))
                {
                    double tot = Tot(sn, it), keep = RescueKeep(sn);
                    if (tot >= keep) { it.Reason = "RESCUE BE"; Tell("🛟 RESCUED TO BREAKEVEN. WHY: this basket went " + M(it.MaxDd) + " deep (" + (it.BalanceStart > 0 ? (100 * -it.MaxDd / it.BalanceStart).ToString("0.0", IC) + " % of the balance" : "") + (DangerPeak >= Set.DangerAt ? ", danger " + DangerPeak : "") + ")" + (Set.RescueMin > 0 && (Now - it.Start).TotalMinutes >= Set.RescueMin ? " • " + ((int)(Now - it.Start).TotalMinutes) + " min in the trade (≥ " + Set.RescueMin + ")" : "") + " — the goal became getting out without a loss, and it is back at " + M(tot) + " (≥ " + M(keep) + " incl. the fees) → CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "RESCUE BE", false); return; }
                }
                if (Set.DeRisk && Set.Rescue && Set.ExitMode != "LEGS" && RescueActive(it) && !Locked && DeRiskStep(sn, it)) return;
                if (Set.ExitMode == "LEGS")
                {
                    foreach (var p in sn.Pos) { double own = p.Profit + p.Swap + p.Commission - (Set.CommissionPerLot > 0 ? p.Lots * Set.CommissionPerLot : 0), goal = Set.Target * p.Lots / Set.EntryLots;
                        if (own >= goal) { Tell("LEG TARGET. WHY: " + p.Side + " " + p.Lots.ToString("0.##", IC) + " @ " + P(p.Open) + " (#" + p.Ticket + ") is " + M(own) + " ≥ its own target " + M(goal) + " (+$" + Set.Target.ToString("0.##", IC) + " per " + Set.EntryLots.ToString("0.##", IC) + " lot) → close it."); CloseOne(p); return; } }
                }
                else if ((Set.RiskGuard || Set.ScaleOut || Set.Compress || Set.TrendStack || Set.AddOnBreak ? Tot(sn, it) : Basket) >= Set.Target) { it.Reason = "TARGET"; Tell("TAKE PROFIT. WHY: the basket reached " + M(Set.RiskGuard || Set.ScaleOut || Set.Compress || Set.TrendStack || Set.AddOnBreak ? Tot(sn, it) : Basket) + (Set.RiskGuard || Set.ScaleOut || Set.Compress ? " (open " + M(Basket) + " + banked " + M(sn.Balance - it.BalanceStart) + ")" : "") + " ≥ the target " + M(Set.Target) + " at a bid of " + P(sn.Bid) + " → CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "TARGET", false); return; }
                if (Set.ProtectAt > 0 && Set.ExitMode != "LEGS")
                {   // 🔐 PROTECT: armed once the basket reached ProtectAt % of the target; it closes when it falls back to ProtectKeep % of the target
                    if (!it.ProtectArmed && Basket >= Set.Target * Set.ProtectAt / 100) { it.ProtectArmed = true; Tell("🔐 PROTECT ARMED: the basket reached " + M(Basket) + " (≥ " + Set.ProtectAt.ToString("0", IC) + " % of the target " + M(Set.Target) + ") — if it falls back to " + M(Set.Target * Set.ProtectKeep / 100) + " it closes there"); }
                    if (it.ProtectArmed && Basket <= Set.Target * Set.ProtectKeep / 100) { it.Reason = "PROTECT"; Tell("🔐 PROTECT EXIT. WHY: the basket was near the target (" + Set.ProtectAt.ToString("0", IC) + " % reached) and came back to " + M(Basket) + " ≤ " + M(Set.Target * Set.ProtectKeep / 100) + " → CLOSE ALL " + sn.Pos.Count + " positions (a near miss never turns into a long hedge)."); CloseAll(sn, "PROTECT", false); return; }
                }
                if (Set.BeAfter > 0 && it.AddOns >= Set.BeAfter && (Set.RiskGuard || Set.Compress ? Tot(sn, it) : Basket) >= 0 && Set.ExitMode != "LEGS") { it.Reason = "RESCUE"; Tell("🛟 RESCUE EXIT. WHY: the basket has " + it.AddOns + " hedges (≥ " + Set.BeAfter + ") and is back at " + M(Basket) + " (breakeven or better) → CLOSE ALL " + sn.Pos.Count + " positions — no endless basket."); CloseAll(sn, "RESCUE", false); return; }
                {
                    double totR = Tot(sn, it);
                    if (Set.MaxDdPct > 0 && totR <= -Set.MaxDdPct / 100 * Math.Max(1, it.BalanceStart)) { it.Reason = "MAX DRAWDOWN"; Tell("⛔ MAX DRAWDOWN %. WHY: the iteration is at " + M(totR) + " (open + realised) ≤ −" + Set.MaxDdPct.ToString("0.##", IC) + " % of its starting balance $" + it.BalanceStart.ToString("N2", IC) + " → CLOSE ALL " + sn.Pos.Count + " positions" + (Set.AfterDrawdown == "STOP" ? " and stop." : ", then the next trade.")); CloseAll(sn, "MAX DRAWDOWN", false); return; }
                    if (Set.RecoverExit && Set.ExitMode != "LEGS" && Set.Target > 0 && it.MaxDd <= -Set.RecDeep * Set.Target && totR >= -Set.Target * Set.RecKeep / 100)
                    { it.Reason = "RECOVERED"; Tell("🔄 RECOVERED EXIT. WHY: the basket went " + M(it.MaxDd) + " deep (≥ " + Set.RecDeep.ToString("0.#", IC) + " × the target) and came back to " + M(totR) + " (within " + Set.RecKeep.ToString("0", IC) + " % of the target from breakeven) → CLOSE ALL " + sn.Pos.Count + " positions: the drawdown is gone, the account waits for the next setup."); CloseAll(sn, "RECOVERED", false); return; }
                    string dl = DayLossHit(sn, totR);
                    if (dl != "") { it.Reason = "DAY LOSS"; dayBlocked = NyDay(); Tell("⛔ DAILY LOSS. WHY: " + dl + " → CLOSE ALL " + sn.Pos.Count + " positions — no new iteration until tomorrow (New York day)."); CloseAll(sn, "DAY LOSS", false); return; }
                }
                if (Set.StuckPct > 0 && Set.StuckHours > 0 && it.BalanceStart > 0 && (Now - it.Start).TotalHours >= Set.StuckHours && Tot(sn, it) <= -Set.StuckPct / 100 * it.BalanceStart)
                { it.Reason = "STUCK EXIT"; Tell("🧯 STUCK EXIT. WHY: the basket is " + M(Tot(sn, it)) + " after " + ((int)(Now - it.Start).TotalHours) + " h (≥ " + Set.StuckHours.ToString("0", IC) + " h and ≥ " + Set.StuckPct.ToString("0.#", IC) + " % of the balance down) — in 6 years of replays baskets like this took weeks or never came back → CLOSE ALL " + sn.Pos.Count + " positions and start fresh."); CloseAll(sn, "STUCK EXIT", false); return; }
                if (Set.MaxDrawdown > 0 && total <= -Set.MaxDrawdown) { it.Reason = "MAX DRAWDOWN"; Tell("MAX DRAWDOWN. WHY: the basket fell to " + M(Basket) + " ≤ −$" + Set.MaxDrawdown.ToString("0.##", IC) + " at a bid of " + P(sn.Bid) + " → CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "MAX DRAWDOWN", false); return; }
                if (pendId == null && ProtectEngine(b, sn, it)) return;   // 🛡 PROTECT THE ADD: lock / resume small
                if (Prot.Blocks) return;   // 🛡 locked: the cool-down / the break — no hedges, no adds
                if (Set.Rolling && Set.HedgeMode == "BREAK") SetHedgeLevel(sn, lastDir); else if ((Set.QuickHedge || Set.CandleHedge || Set.KeyHedge) && Set.HedgeMode != "NONE" && Set.HedgeMode != "SIGNAL" && Set.HedgeMode != "LADDER") { double spQ = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0; QuickLevel(sn, lastDir, spQ); CandleLevel(sn, lastDir, spQ); KeyLevel(sn, lastDir, spQ); TrendLevel(sn, lastDir, spQ); }
                if (!(Set.Rolling && Set.HedgeMode == "BREAK") && Set.DdTightEvery > 0) { double spT = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0; DdTightLevel(sn, lastDir, spT, false); }   // 📉 the basket got deeper: the hedge comes closer
                if (manualReset) { manualReset = false; SetHedgeLevel(sn, lastDir); } ApplyManual();
                if (Locked && !Set.LockOn && !Set.RiskGuard && !Set.RadarLock && Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)) > 0.005)
                {   // 🐞 user's catch (#39: "LOCKED" on the banner while 0.08 bought / 0.16 sold): without the lock rules (LOCK / RISK GUARD off) nothing manages a
                    // lock — the 🔐 breakeven lock's leg is followed by normal hedges; once the basket is no longer even the state is cleared (the banner shows the real net)
                    Locked = false; LockHi = double.NaN; LockLo = double.NaN; Note("🔓 the basket is no longer even (net " + sn.Pos.Sum(p => p.Dir * p.Lots).ToString("+0.##;−0.##", IC) + ") → not locked any more; the normal hedge rules go on");
                }
                if ((Set.LockOn || Set.RiskGuard || Set.RadarLock) && Set.HedgeMode != "NONE" && Set.HedgeMode != "LADDER")
                {
                    double spL = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0, netL = sn.Pos.Sum(p => p.Dir * p.Lots);
                    if (Locked && Set.LockMaxMin > 0 && (Now - lockAt).TotalMinutes >= Set.LockMaxMin && Tot(sn, it) >= Math.Max(0.5, Set.CommissionPerLot * Gross(sn.Pos)))
                    { it.Reason = "LOCK TIMEOUT"; Tell("⏱ LOCK TIMEOUT. WHY: locked " + ((int)(Now - lockAt).TotalMinutes) + " min (≥ " + Set.LockMaxMin.ToString("0", IC) + ") and the basket is in profit " + M(Tot(sn, it)) + " → CLOSE ALL and trade again"); CloseAll(sn, "LOCK TIMEOUT", false); return; }
                    if (Locked)
                    {   // 🔒 LOCKED: nothing happens inside the range; the break of its high / low unlocks
                        {   // 09m: keep the real unlock prices for the chart labels (the same rule as below)
                            double avgR0 = Set.RiskGuard && sn.Fin.Count >= 5 ? sn.Fin.Take(14).Average(x => x.H - x.L) : 0;
                            LockFarHi = Set.RiskGuard ? Math.Max(LockHi + spL, LockHi + Set.GuardBreak * avgR0) : LockHi + spL; LockFarLo = Set.RiskGuard ? Math.Min(LockLo - spL, LockLo - Set.GuardBreak * avgR0) : LockLo - spL;
                        }
                        int bd = sn.Bid > LockHi + spL ? 1 : sn.Bid < LockLo - spL ? -1 : 0; if (bd == 0) return;
                        if (Set.RiskGuard)
                        {   // 🛟 only an OBVIOUS trend unlocks: far enough beyond the range AND the EMA 50 / 200 the same way
                            double avgR = sn.Fin.Count >= 5 ? sn.Fin.Take(14).Average(x => x.H - x.L) : 0;
                            bool far = bd > 0 ? sn.Bid >= LockHi + Set.GuardBreak * avgR : sn.Bid <= LockLo - Set.GuardBreak * avgR;
                            bool inProfit = Set.ProfitUnlock && Tot(sn, it) >= 0;   // a BREAKEVEN lock (in profit): any clear break goes for the target — the trend check only guards a LOSING lock
                            if (!far || (!inProfit && !double.IsNaN(Ema200) && TrendNow != bd)) return;
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
                        if (Set.RiskGuard && GuardWhy(sn, it, ul) != "" && Set.TrimMax > 0 && trimCount >= Set.TrimMax)
                        {   // 🛡 the trims of this iteration are used up: no new realised loss in a chop — it STAYS LOCKED (frozen, net 0) and waits for a
                            // pair-off (a winner + a loser that add up to ≥ $0) or the rescue
                            Locked = true; if (trimNoted != it.N) { trimNoted = it.N; Tell("🛡 STAYS LOCKED. WHY: the bid " + P(sn.Bid) + " broke the lock range, but this iteration already TRIMMED " + trimCount + "× (each trim books a loss; in a chop the break fails and the next trim books another) → no trim, no new lots: frozen at " + M(Basket) + " until a pair-off ≥ $0 or the rescue."); }
                            return;
                        }
                        if (Set.RiskGuard && GuardWhy(sn, it, ul) != "")
                        {   // 🛡 TRIM: at the cap a LEAD unlock would make it bigger — close the SMALLEST leg against the break instead: the basket leans
                            // with the break AND gets smaller (one small realised loss, never a pile of new lots)
                            var leg = sn.Pos.Where(p => p.Dir == -bd && p.Time < lockAt.AddSeconds(-2)).OrderByDescending(p => p.Profit + p.Swap + p.Commission).FirstOrDefault() ?? sn.Pos.Where(p => p.Dir == -bd).OrderByDescending(p => p.Profit + p.Swap + p.Commission).FirstOrDefault();   // the CHEAPEST leg to close (its loss the smallest) — never the lock's own fresh leg   // never the lock's own fresh leg
                            if (leg != null)
                            {
                                Tell("🛡 UNLOCK — TRIM. WHY: the bid " + P(sn.Bid) + " broke the lock range " + (bd > 0 ? "HIGH " + P(LockHi) : "LOW " + P(LockLo)) + " and the basket is at the RISK GUARD cap → close the smallest " + leg.Side + " " + leg.Lots.ToString("0.##", IC) + " @ " + P(leg.Open) + " (#" + leg.Ticket + ", " + M(leg.Profit + leg.Swap + leg.Commission) + ") instead of adding — the " + (bd > 0 ? "buys" : "sells") + " lead, the basket gets smaller.");
                                Refused = ""; var idT = Ask("CLOSE", new object[] { leg.Ticket, 0.0 }, "CLOSE #" + leg.Ticket + " " + leg.Side + " " + leg.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + it.N + " 🛡 trim)");
                                if (idT == null) { Locked = true; if (sugg) return; Note("⚠ trim not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
                                guardQuiet = Now.AddMinutes(30); trimCount++; pendId = idT; pendKind = "TRIM"; pendTicket = leg.Ticket; liftDir = bd; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
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
                        double hiL = PHi(sn), loL = PLo(sn);
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
                if (CompressStep(sn, it)) return;
                if (Set.AddOnBreak && AobStep(sn, it)) return;   // ➕ 09h: one add on a confirmed break (OFF by default)
                if (Set.TrendStack && TrendStackStep(sn, it)) return;   // 🚀 09e (simulated accounts only)
                // ADD TO THE WINNER: the setup signals again in the leading side's direction (the last leg's)
                if (Set.AddToWinner && (Set.MaxWinnerAdds == 0 || it.WinnerAdds < Set.MaxWinnerAdds) && Tot(sn, it) > Math.Max(1, Set.Target * 0.2))   // only to a REAL winner: the basket already in profit (≥ 20 % of the target)
                {
                    Signals(sn); int wd; double wl; string ww, ws;
                    if (AnySignal(sn, lastDir, out wd, out wl, out ww, out ws) && wd == lastDir)
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
                    double top = PHi(sn), bot = PLo(sn); LadderHi = top + Set.LadderStep; LadderLo = bot - Set.LadderStep; HedgeLevel = double.NaN;
                    if (Set.LadderMax > 0 && sn.Pos.Count >= Set.LadderMax) { if (ladderFull != it.N + "") { ladderFull = it.N + ""; Tell("🪜 LADDER FULL: " + sn.Pos.Count + " legs (max " + Set.LadderMax + ") — no more legs; the basket waits for its target " + M(Set.Target)); } return; }
                    double ask = !double.IsNaN(sn.Ask) ? sn.Ask : sn.Bid;
                    int netL = Math.Sign(Math.Round(sn.Pos.Sum(p => p.Dir * p.Lots), 6)); bool okB = Set.TrendFilter != "EMA" || TrendNow >= 0 || netL < 0, okS = Set.TrendFilter != "EMA" || TrendNow <= 0 || netL > 0;
                    if (ask >= LadderHi && okB) { Tell("🪜 LADDER BUY " + Set.EntryLots.ToString("0.##", IC) + ". WHY: the price " + P(ask) + " is above the basket's highest leg " + P(top) + " + " + Set.LadderStep.ToString("0.##", IC) + " = " + P(LadderHi) + "; basket " + M(Basket) + ", " + sn.Pos.Count + " legs."); Order(sn, 1, Set.EntryLots, "🪜 LADDER BUY — above the highest leg " + P(top) + " + " + Set.LadderStep.ToString("0.##", IC), LadderHi); return; }
                    if (sn.Bid <= LadderLo && okS) { Tell("🪜 LADDER SELL " + Set.EntryLots.ToString("0.##", IC) + ". WHY: the price " + P(sn.Bid) + " is below the basket's lowest leg " + P(bot) + " − " + Set.LadderStep.ToString("0.##", IC) + " = " + P(LadderLo) + "; basket " + M(Basket) + ", " + sn.Pos.Count + " legs."); Order(sn, -1, Set.EntryLots, "🪜 LADDER SELL — below the lowest leg " + P(bot) + " − " + Set.LadderStep.ToString("0.##", IC), LadderLo); return; }
                    return;
                }
                bool beyondNow = Set.HedgeMode != "NONE" && !double.IsNaN(HedgeLevel) && (lastDir > 0 ? sn.Bid <= HedgeLevel : sn.Bid >= HedgeLevel);
                if (NoCatchUp && !beyondNow) { NoCatchUp = false; Note("▶ the price is back inside the hedge line — the normal hedging goes on"); }
                if (NoCatchUp && beyondNow && !catchNoted) { catchNoted = true; Tell("⏸ NO CATCH-UP (your choice at RESUME): the price " + P(sn.Bid) + " is already past the hedge line " + P(HedgeLevel) + " → no " + (lastDir > 0 ? "SELL" : "BUY") + " now; it waits for the next signal (a new line beyond the price)."); }
                if (beyondNow && !NoCatchUp && RadarHigh && (Set.RadarNoAdd || Set.RadarLock))
                {   // ⚠ your radar option: no more add-ons / lock at net 0 above DANGER x %
                    if (Set.RadarLock && !Locked) { GuardLock(sn, it, "⚠ DANGER RADAR " + (100 * Radar.P).ToString("0", IC) + " % ≥ " + Set.RadarAt.ToString("0", IC) + " %"); return; }
                    var k = it.N + "|" + it.AddOns; if (radarSkipNote != k) { radarSkipNote = k; Tell("⚠ RADAR → NO ADD-ON (your option). WHY: " + Radar.Text + " ≥ " + Set.RadarAt.ToString("0", IC) + " % → the " + (lastDir > 0 ? "SELL" : "BUY") + " at " + P(HedgeLevel) + " is not placed; the basket waits for its target / breakeven / your limits."); }
                    if (!Locked) beyondNow = false;
                }
                if (beyondNow && !NoCatchUp && !(Locked && Set.RadarLock && !Set.LockOn && !Set.RiskGuard))
                {
                    if (Set.HedgeStopPct > 0 && it.BalanceStart > 0 && Tot(sn, it) <= -Set.HedgeStopPct / 100 * it.BalanceStart)
                    {   // 🧯 deep enough: no new lots — the basket waits (target / 🛟 rescue / ⛔ the hard floor)
                        if (hstopNoted != it.N) { hstopNoted = it.N; Tell("🧯 HEDGE STOP. WHY: the basket is " + M(Tot(sn, it)) + " (≥ " + Set.HedgeStopPct.ToString("0.#", IC) + " % of $" + it.BalanceStart.ToString("N0", IC) + ") → no more hedges or locks in this iteration: no new lots; it waits for the target / the rescue" + (Set.MaxDdPct > 0 ? ", and closes everything at −" + Set.MaxDdPct.ToString("0.#", IC) + " %" : "") + "."); }
                        return;
                    }
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
            if (sn.Pos.Count > 0 && StopsOnHand && pendId == null && sn.Pos.Any(x => !handStartPos.Contains(x.Ticket))) { var p0 = sn.Pos.Where(x => !handStartPos.Contains(x.Ticket)).OrderBy(x => x.Time).ThenBy(x => x.Ticket).Last(); HandStop(p0.Side + " " + p0.Lots.ToString("0.##", IC) + " @ " + P(p0.Open) + " opened by hand, " + Now.ToString("HH:mm:ss", IC)); return; }
            if (sn.Pos.Count > 0 && Set.TrackManual) { Adopt(sn); return; }
            if (sn.Pos.Count > 0) { Note("⚠ positions on " + Set.Symbol + " that are not the automation's — waiting until they are closed"); return; }
            Signals(sn);
            var gate = Gate(); int gcut = gate.IndexOf(" (New York "); string gkey = gcut > 0 ? gate.Substring(0, gcut) : gate; Blocked = gate; if (gkey != blockedKey) { blockedKey = gkey; Blocked = gate; if (gate != "") Tell("NOT TRADING NOW: " + gate + "."); else Tell("TRADING ALLOWED again" + (Set.Windows != "" ? " (inside " + Set.Windows + " New York)" : "") + "."); }
            if (gate != "") return;
            int dir = 0; string why = ""; double lvl = double.NaN; string story = "";
            if (Phase == "FIRST") { dir = Set.FirstDir == "BUY" ? 1 : -1; why = "FIRST " + Set.FirstDir + " (yours)"; story = "you chose " + Set.FirstDir + " as the first direction"; }
            else if (Phase == "AGAIN" && Set.NextStart == "WINNER") { dir = lastDir; why = "AFTER A WIN — the same direction again"; story = "the last iteration closed in profit " + (lastDir > 0 ? "long" : "short") + ", so " + (dir > 0 ? "BUY" : "SELL") + " again at once (NEXT ITERATION = after a win, the same direction)"; }
            else if (Phase == "AGAIN") { dir = lastDir; why = "AFTER THE PAUSE — the last basket's direction"; story = "the " + Set.PauseSec + " s pause after the last iteration ended; the last basket was " + (dir > 0 ? "long" : "short") + ", so " + (dir > 0 ? "BUY" : "SELL") + " again"; }
            else if (!AnySignal(sn, 0, out dir, out lvl, out why, out story)) return;
            JournalAdd(sn, dir, why);
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
            if (Set.SlopeFilter && Phase != "FIRST")
            {   // 🧪 TAH (RACE lane): a new iteration only with the H1 EMA 20's 3-hour slope
                List<Mt5Bar> sb1 = null; try { sb1 = RadarBars != null ? RadarBars(Set.Symbol) : null; } catch { } if (sb1 == null && lastBroker != null) lock (lastBroker.State.Gate) sb1 = lastBroker.State.SeriesOf(Set.Symbol, 1).Bars.ToList();
                double sl = sb1 == null ? double.NaN : DangerRadar.H1Ema20Slope3(sb1, Now); if (double.IsNaN(sl) || Math.Sign(sl) != dir) { Blocked = "🧪 TAH: the H1 EMA 20 slope (3 h) " + (double.IsNaN(sl) ? "unknown" : sl.ToString("+0.00;−0.00", IC)) + " is not " + (dir > 0 ? "up" : "down"); return; }
            }
            if (Phase != "FIRST" && Phase != "AGAIN" && !VwapOk(sn, dir)) { var sk = why + " — skipped: the VWAP filter " + Set.VwapFilter + " wants " + (Set.VwapFilter == "DISCOUNT" ? (dir > 0 ? "a buy UNDER" : "a sell OVER") : (dir > 0 ? "a buy OVER" : "a sell UNDER")) + " the VWAP " + P(Vwap) + " (bid " + P(sn.Bid) + ")"; if (sk != vwapSkip) { vwapSkip = sk; Tell("NO ENTRY: " + sk + "."); } return; }
            if (Set.SizePer1k > 0) { var sz = SizeFor(sn.Balance, sn.Equity, sn.Pos.Count == 0 && OtherOpen(sn), sn.Sym, Set.SizePer1k, Set.TargetPerLot, Set.IsRunner); Set.EntryLots = sz.Item1; Set.NetAdvantage = sz.Item1; Set.AddLots = Set.HedgeSize == "SAME" ? sz.Item1 : Math.Round(2 * sz.Item1, 2); Set.Target = sz.Item2; if (sz.Item3 != "") Note(sz.Item3); }
            double eLots = Set.EntryLots;
            if (Set.FullAuto && Phase != "FIRST")
            {   // 🤖 the score decides: skip a weak / chasing entry, size a strong one up
                string scWhy; int sc = Score(sn, dir, out scWhy); LastScore = sc; LastScoreWhy = scWhy;
                if (sc < Set.AutoScoreMin) { SkipLevel = double.IsNaN(lvl) ? sn.Bid : lvl; SkipAt = Now; SkipDir = dir; var k3 = Now.ToString("yyyyMMddHHmm", IC) + dir; if (k3 != scoreSkip) { scoreSkip = k3; ScoreSkips++; Note("🤖 FULL AUTO: " + why + " skipped — score " + sc + " < " + Set.AutoScoreMin + " (" + scWhy + ")"); Version++; } return; }
                double kS = sc >= 85 ? 1.5 : sc >= 75 ? 1.25 : 1.0; if (Set.RiskDial == "NORMAL") kS *= 1.25; else if (Set.RiskDial == "BOLD") kS *= 1.5;
                eLots = Math.Max(0.01, Math.Round(Set.EntryLots * kS, 2));
                why += " • 🤖 score " + sc + (eLots > Set.EntryLots + 1e-9 ? " → " + eLots.ToString("0.##", IC) + " lot" : "");
            }
            if (ManualManage) { Ask("ORDER", null, (dir > 0 ? "BUY " : "SELL ") + eLots.ToString("0.##", IC) + " " + Set.Symbol + " (ENTRY — " + why + ")"); return; }
            closedByUs = false; trimCount = 0; Current = new AutoIteration { N = NextN(), Start = Now, FirstDir = dir > 0 ? "BUY" : "SELL", BalanceStart = sn.Balance }; Iterations.Add(Current); Phase = "IN ITERATION";
            if (lastSig != null && (Now - lastSig.Time).TotalSeconds < 5 && lastSig.Dir == dir) lastSig.Taken = true;
            ApplySetupRisk(LastSetup);
            Current.Ctx = Context(sn, dir); Tell("CONTEXT: " + Current.Ctx.Replace(";", " • "));
            Tell("ITERATION #" + Current.N + " — ENTRY " + (dir > 0 ? "BUY " : "SELL ") + eLots.ToString("0.##", IC) + " (setup " + (Set.EntryMode == "MULTI" ? "🎯 " + SetupName(LastSetup) : Set.EntryMode) + "). WHY: " + story + (Set.FullAuto ? " • 🤖 SCORE " + LastScore + ": " + LastScoreWhy : "") + ". Bid " + P(sn.Bid) + " / ask " + P(sn.Ask) + ", balance $" + sn.Balance.ToString("N2", IC) + ".");
            Order(sn, dir, eLots, "ENTRY " + (dir > 0 ? "BUY" : "SELL") + " — " + why, lvl);
        }

        // 🤖 THE MARKET READING (every closed candle): regime, ATR, extension from VWAP / EMA 50, the pullback before the break, the run, the day edge
        DateTime readAt = DateTime.MinValue; public int AggRunNow; int e123Dir; DateTime e123Bar = DateTime.MinValue; string e123Story = ""; int rtDir; double rtLevel; DateTime rtBar = DateTime.MinValue; string rtStory = ""; readonly List<Tuple<double, int, DateTime, string>> rtBroken = new List<Tuple<double, int, DateTime, string>>(); DateTime bcBar = DateTime.MinValue; double bcHi = double.NaN, bcLo = double.NaN;
        public double SkipLevel = double.NaN; public DateTime SkipAt = DateTime.MinValue; public int SkipDir;   // ⛔ the last SKIPPED break (shown on the chart)
        public string Regime = ""; public string MarketNow = ""; public int LastScore = -1; public string LastScoreWhy = ""; public int ScoreSkips; string scoreSkip = "";
        void ReadMarket(Snap sn, out double atr, out double pull, out int runUp, out int runDn)
        {
            var f = sn.Fin; atr = f.Count >= 5 ? Math.Max(0.01, f.Take(14).Average(x => x.H - x.L)) : 0; pull = 0; runUp = 0; runDn = 0; Regime = "";
            if (f.Count < 20 || atr <= 0) return;
            foreach (var x in f) { if (x.C > x.O) runUp++; else break; } foreach (var x in f) { if (x.C < x.O) runDn++; else break; }
            var w = f.Take(60).ToList(); double rng = w.Max(x => x.H) - w.Min(x => x.L);
            int flips = 0; if (!double.IsNaN(Vwap)) for (int i = 1; i < Math.Min(30, f.Count); i++) if (Math.Sign(f[i].C - Vwap) != Math.Sign(f[i - 1].C - Vwap)) flips++;
            Regime = rng < 4 * atr ? "RANGE" : flips >= 6 ? "CHOP" : TrendNow > 0 ? "TREND UP" : TrendNow < 0 ? "TREND DOWN" : "MIXED";
            MarketNow = Regime + " • ATR " + atr.ToString("0.00", IC) + " • 60-min range " + (rng / atr).ToString("0.0", IC) + " ATR" + (!double.IsNaN(Vwap) ? " • " + ((sn.Bid - Vwap) / atr).ToString("+0.0;−0.0", IC) + " ATR from VWAP" : "");
        }
        // the deepest move AGAINST dir in the last n closed candles (from a running high for a buy / low for a sell) — "was there a retracement?"
        static double PullbackOf(List<Mt5Bar> f, int dir, int n)
        {
            double best = 0, run = double.NaN; for (int i = Math.Min(n, f.Count) - 1; i >= 0; i--) { var x = f[i];
                // the high so far BEFORE this candle vs its low (a candle's own range is not a pullback)
                if (dir > 0) { if (!double.IsNaN(run)) best = Math.Max(best, run - x.L); run = double.IsNaN(run) ? x.H : Math.Max(run, x.H); } else { if (!double.IsNaN(run)) best = Math.Max(best, x.H - run); run = double.IsNaN(run) ? x.L : Math.Min(run, x.L); } }
            return best;
        }
        int Score(Snap sn, int dir, out string why)
        {
            double atr, pull; int ru, rd; ReadMarket(sn, out atr, out pull, out ru, out rd); var parts = new List<string>(); int sc = 50;
            if (atr <= 0) { why = "not enough candles"; return 50; }
            Action<int, string> add = (v, t) => { sc += v; parts.Add((v >= 0 ? "+" : "") + v + " " + t); };
            if (Regime == "TREND UP" || Regime == "TREND DOWN") { int td = Regime == "TREND UP" ? 1 : -1; if (td == dir) add(15, "with the trend"); else add(-20, "against the trend"); }
            else if (Regime == "RANGE") add(5, "a break out of a tight range");
            else if (Regime == "CHOP") add(-15, "chop (VWAP crossed again and again)");
            if (Set.EntryMode == "AGGBRK" && AggRunNow > 1) add(Math.Min(15, 5 * (AggRunNow - 1)), AggRunNow + " candles of aggression before the turn (a stronger reversal)");
            double pb = PullbackOf(sn.Fin, dir, 10); var f10 = sn.Fin.Take(10).ToList(); double mv10 = f10.Count >= 5 ? dir * (f10[0].C - f10[f10.Count - 1].O) : 0;
            if (pb < 0.5 * atr && mv10 >= 3 * atr) add(-25, "NO PULLBACK: " + (mv10 / atr).ToString("0.0", IC) + " ATR " + (dir > 0 ? "up" : "down") + " in 10 candles, the deepest dip only " + (pb / atr).ToString("0.0", IC) + " ATR — chasing the top / bottom");
            else if (pb >= 1.0 * atr) add(10, "a real pullback before the break (" + (pb / atr).ToString("0.0", IC) + " ATR)");
            if (!double.IsNaN(Vwap)) { double e = dir * (sn.Bid - Vwap) / atr; if (e > 2.5) add(-20, "stretched " + e.ToString("0.0", IC) + " ATR from VWAP"); else if (e > 1.5) add(-10, e.ToString("0.0", IC) + " ATR from VWAP"); }
            if (!double.IsNaN(sn.DayHi) && !double.IsNaN(sn.DayLo) && pb < 0.5 * atr) { if (dir > 0 && sn.Bid >= sn.DayHi - 0.2 * atr) add(-10, "at the day's high"); if (dir < 0 && sn.Bid <= sn.DayLo + 0.2 * atr) add(-10, "at the day's low"); }
            double sp = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0; if (sp > 0.25 * atr) add(-15, "wide spread");
            sc = Math.Max(0, Math.Min(100, sc)); why = parts.Count == 0 ? "nothing special" : string.Join(", ", parts); return sc;
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
            else if (Set.EntryMode == "RETEST")
            {
                if (rtDir == 0 || (only != 0 && rtDir != only) || sn.Fin.Count == 0 || sn.Fin[0].Time != rtBar) return false;
                dir = rtDir; lvl = rtLevel; why = "🧱 S/R RETEST " + (dir > 0 ? "BUY" : "SELL") + " at " + P(rtLevel); story = rtStory; rtDir = 0;
            }
            else if (Set.EntryMode == "ENG123")
            {
                if (e123Dir == 0 || (only != 0 && e123Dir != only) || sn.Fin.Count == 0 || sn.Fin[0].Time != e123Bar) return false;
                dir = e123Dir; lvl = sn.Fin[0].C; why = "1-2-3 ENGULFING " + (dir > 0 ? "BUY" : "SELL"); story = e123Story; e123Dir = 0;   // once per candle
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
                if (Set.BreakClose && (Set.EntryMode == "BREAK" || Set.EntryMode == "FADE"))
                {   // ⚡ BREAK + CLOSE: the first closed candle beyond the swing level (the level before that candle), checked once per candle
                    if (sn.Fin.Count < 2 || sn.Fin[0].Time == bcBar) return false;
                    var cc = sn.Fin[0]; var cp = sn.Fin[1]; bcBar = cc.Time;
                    double bl = !double.IsNaN(bcHi) ? bcHi : BuyLevel, sl = !double.IsNaN(bcLo) ? bcLo : SellLevel; bcHi = BuyLevel; bcLo = SellLevel;
                    if (only >= 0 && !double.IsNaN(bl) && cc.C > bl && cp.C <= bl) { dir = 1; lvl = bl; }
                    else if (only <= 0 && !double.IsNaN(sl) && cc.C < sl && cp.C >= sl) { dir = -1; lvl = sl; }
                    else return false;
                    why = "BREAK + CLOSE " + (dir > 0 ? "above the swing high " : "below the swing low ") + P(lvl);
                    story = "the " + Tf.Name(Set.Tf) + " candle " + cc.Time.ToString("HH:mm", IC) + " CLOSED at " + P(cc.C) + (dir > 0 ? ", above the swing HIGH " : ", below the swing LOW ") + P(lvl) + " (the candle before closed " + (dir > 0 ? "under" : "over") + " it) → a confirmed break";
                    return Set.EntryMode != "FADE" || FadeFlip(sn, ref dir, lvl, ref why, ref story);
                }
                double hi = BuyLevel, lo = SellLevel; if (double.IsNaN(prevBid)) return false;   // each side on its own: no swing high must not block a sell
                // the user's bug (the entry "moved" to an older swing): a quick spike through the level BETWEEN two price updates — MT5's forming candle
                // has the high / low, our bid never showed it — used the swing up without an entry. The forming candle's own high / low passing it counts too.
                bool sameBar = !double.IsNaN(sn.FormH) && sn.FormT == prevFormT;
                bool upWick = !double.IsNaN(hi) && !double.IsNaN(sn.FormH) && sn.FormH > hi && prevBid <= hi && (sameBar ? prevFormH <= hi : sn.FormT > prevFormT);
                bool dnWick = !double.IsNaN(lo) && !double.IsNaN(sn.FormL) && sn.FormL < lo && prevBid >= lo && (sameBar ? prevFormL >= lo : sn.FormT > prevFormT);
                bool viaWick = false;
                // the user's 2nd case (the entry "moved back" to an older swing): the candle that just CLOSED went through the level between two price
                // updates (the DEMO's forming candle is refreshed every few seconds) — the level is used up the moment it closes; that close IS the break,
                // taken while the price is still near the level (never a chase)
                if (sn.Fin.Count > 0)
                {
                    double atrC = sn.Fin.Count >= 5 ? Math.Max(0.05, sn.Fin.Take(14).Average(x => x.H - x.L)) : 0.5, cH = sn.Fin[0].H, cL = sn.Fin[0].L;
                    if (only >= 0 && !double.IsNaN(prevBuyLvl) && (double.IsNaN(hi) || Math.Abs(hi - prevBuyLvl) > 1e-9) && cH > prevBuyLvl && prevBid <= prevBuyLvl + 1e-9 && sn.Bid >= prevBuyLvl - 0.5 * atrC && sn.Bid <= prevBuyLvl + atrC)
                    { dir = 1; lvl = prevBuyLvl; why = "BREAK of the " + (Set.Swing ? "swing" : "recent") + " high " + prevBuyLvl.ToString("0.#####", IC); story = "the " + Tf.Name(Set.Tf) + " candle " + sn.Fin[0].Time.ToString("HH:mm", IC) + " closed with its high " + P(cH) + " above " + P(prevBuyLvl) + " (it went through between two price updates; bid now " + P(sn.Bid) + ", still at the level) → the high broke"; return true; }
                    if (only <= 0 && !double.IsNaN(prevSellLvl) && (double.IsNaN(lo) || Math.Abs(lo - prevSellLvl) > 1e-9) && cL < prevSellLvl && prevBid >= prevSellLvl - 1e-9 && sn.Bid <= prevSellLvl + 0.5 * atrC && sn.Bid >= prevSellLvl - atrC)
                    { dir = -1; lvl = prevSellLvl; why = "BREAK of the " + (Set.Swing ? "swing" : "recent") + " low " + prevSellLvl.ToString("0.#####", IC); story = "the " + Tf.Name(Set.Tf) + " candle " + sn.Fin[0].Time.ToString("HH:mm", IC) + " closed with its low " + P(cL) + " below " + P(prevSellLvl) + " (it went through between two price updates; bid now " + P(sn.Bid) + ", still at the level) → the low broke"; return true; }
                }
                if (!double.IsNaN(hi) && prevBid <= hi && (sn.Bid > hi || upWick) && only >= 0) { dir = 1; lvl = hi; viaWick = sn.Bid <= hi; why = "BREAK of the " + (Set.Swing ? "swing" : "recent") + " high " + hi.ToString("0.#####", IC); }
                else if (!double.IsNaN(lo) && prevBid >= lo && (sn.Bid < lo || dnWick) && only <= 0) { dir = -1; lvl = lo; viaWick = sn.Bid >= lo; why = "BREAK of the " + (Set.Swing ? "swing" : "recent") + " low " + lo.ToString("0.#####", IC); }
                if (dir == 0 && Set.TrendBreak && Set.EntryMode == "BREAK" && TrendNow != 0 && sn.Fin.Count > 0 && !double.IsNaN(prevBid))
                {   // 📈 a clear trend: the last closed candle's high (up) / low (down) broken = an entry the trend's way, no swing needed
                    double tl = TrendNow > 0 ? sn.Fin[0].H : sn.Fin[0].L;
                    if ((TrendNow > 0 && only >= 0 && prevBid <= tl && sn.Bid > tl) || (TrendNow < 0 && only <= 0 && prevBid >= tl && sn.Bid < tl))
                    { dir = TrendNow; lvl = tl; why = "📈 TREND BREAK of the last candle's " + (dir > 0 ? "high " : "low ") + P(tl); story = "a clear " + (dir > 0 ? "UP" : "DOWN") + " trend (EMA 50 " + P(Ema50) + (dir > 0 ? " > " : " < ") + "EMA 200 " + P(Ema200) + ") and the bid broke the " + sn.Fin[0].Time.ToString("HH:mm", IC) + " candle's " + (dir > 0 ? "high" : "low") + " " + P(tl) + " → with the trend"; return true; }
                }
                if (dir == 0) return false;
                story = viaWick ? "the candle's " + (dir > 0 ? "high reached " + P(sn.FormH) + ", above " : "low reached " + P(sn.FormL) + ", below ") + P(lvl) + " = " + Candles(sn, dir > 0) + " between two price updates (bid now " + P(sn.Bid) + ") → the " + (dir > 0 ? "high" : "low") + " broke"
                    : "the bid moved from " + P(prevBid) + " to " + P(sn.Bid) + ", " + (dir > 0 ? "above " : "below ") + P(lvl) + " = " + Candles(sn, dir > 0) + " → the " + (dir > 0 ? "high" : "low") + " broke";
                if (Set.EntryMode == "FADE" && !FadeFlip(sn, ref dir, lvl, ref why, ref story)) return false;
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
            return string.Join(";", "setup=" + (Set.EntryMode == "MULTI" && LastSetup != "" ? LastSetup : Set.EntryMode), "side=" + (dir > 0 ? "BUY" : "SELL"), "session=" + ses, "hour=" + ny.Hour.ToString("00", IC),
                "vwap=" + (double.IsNaN(Vwap) ? "none" : sn.Bid > Vwap ? "above" : "below"), "withVwap=" + (double.IsNaN(Vwap) ? "?" : ((sn.Bid > Vwap) == (dir > 0) ? "yes" : "no")),
                "fvg=" + (lf == null ? "none" : lf.Dir > 0 ? "bull" : "bear"), "withFvg=" + (lf == null ? "?" : (lf.Dir == dir ? "yes" : "no")),
                "flow=" + (sh >= 0.6 ? "buyers" : sh <= 0.4 ? "sellers" : "even"), "withFlow=" + ((dir > 0 ? sh >= 0.6 : sh <= 0.4) ? "yes" : "no"),
                "range=" + (double.IsNaN(rng) ? "?" : rng.ToString("0.###", IC)), "spread=" + (double.IsNaN(sn.Ask) ? "?" : (sn.Ask - sn.Bid).ToString("0.#####", IC)), "toOtherSide=" + (double.IsNaN(stopD) ? "?" : stopD.ToString("0.###", IC)), MoreContext(sn, dir));
        }
        // FVG zones and the FLOW surge candle: once per new finished candle
        // 🎯 MULTI (FULL AUTO): the setups the engine watches together — each signal is scored, the best one starts the iteration
        static readonly string[] MultiModes = { "BREAK", "RETEST", "FADE", "ENG123", "ENGULF", "VWAPREJ", "AGGBRK", "FVG50", "FLOW" };
        public string LastSetup = "";
        void Signals(Snap sn)
        {
            if (Set.EntryMode == "MULTI") { string keep = Set.EntryMode; try { foreach (var m in MultiModes) { Set.EntryMode = m; SignalsOne(sn); } } finally { Set.EntryMode = keep; } return; }
            SignalsOne(sn);
        }
        // 🔄 BREAK FADE: only a break at the end of a LONG run (the swing ≥ FadeAtr × ATR from the other end of the last 20 candles) → the other way
        bool FadeFlip(Snap sn, ref int dir, double lvl, ref string why, ref string story)
        {
            if (sn.Fin.Count < 20) return false; double atr = Math.Max(0.05, sn.Fin.Take(14).Average(x => x.H - x.L));
            double far = dir > 0 ? lvl - sn.Fin.Take(20).Min(x => x.L) : sn.Fin.Take(20).Max(x => x.H) - lvl;
            if (far < Set.FadeAtr * atr) return false;
            story += " — 🔄 FADE: the run into it was " + (far / atr).ToString("0.0", IC) + " ATR (≥ " + Set.FadeAtr.ToString("0.#", IC) + ") → " + (dir > 0 ? "SELL" : "BUY") + " the exhaustion";
            why = "🔄 FADE " + (dir > 0 ? "SELL" : "BUY") + " — " + why; dir = -dir; return true;
        }
        // setup → (max hedge distance in price, target × the base target); filled from the per-setup replay sweep
        public static readonly Dictionary<string, Tuple<double, double>> RiskProfiles = new Dictionary<string, Tuple<double, double>>
        {
            { "BREAK", Tuple.Create(5.0, 1.0) }, { "AGGBRK", Tuple.Create(5.0, 1.0) }, { "ENG123", Tuple.Create(5.0, 1.0) }, { "FADE", Tuple.Create(5.0, 1.0) },
        };
        double baseCap = double.NaN, baseTarget = double.NaN;
        void ApplySetupRisk(string setup)
        {
            if (!Set.SetupRisk || Set.EntryMode != "MULTI") return; Tuple<double, double> rp; if (!RiskProfiles.TryGetValue(setup, out rp)) return;
            if (double.IsNaN(baseCap)) { baseCap = Set.HedgeCap; baseTarget = Set.Target; }
            Set.HedgeCap = rp.Item1; Set.Target = Math.Round(baseTarget * rp.Item2, 2);
            Tell("🎚 RISK FOR " + SetupName(setup) + ": max hedge distance " + rp.Item1.ToString("0.##", IC) + ", target +$" + Set.Target.ToString("0.##", IC) + " (the replays' best for this setup)");
        }
        void RestoreSetupRisk() { if (double.IsNaN(baseCap)) return; Set.HedgeCap = baseCap; Set.Target = baseTarget; baseCap = baseTarget = double.NaN; }
        static string SetupName(string m) { return m == "BREAK" ? "⚡ BREAK" : m == "ENGULF" ? "🕯 ENGULFING" : m == "ENG123" ? "🕯 1-2-3 ENGULFING" : m == "FADE" ? "🔄 BREAK FADE" : m == "RETEST" ? "🧱 S/R RETEST" : m == "VWAPREJ" ? "〽 VWAP REJECTION" : m == "AGGBRK" ? "💥 AGGRESSION BREAK" : m == "FVG50" ? "🧲 FVG 50 %" : m == "FLOW" ? "🌊 ORDER FLOW" : m; }
        // every setup's signal now; with MULTI the best SCORED one (the others are named in the why)
        bool AnySignal(Snap sn, int only, out int dir, out double lvl, out string why, out string story)
        {
            if (Set.EntryMode != "MULTI") { LastSetup = Set.EntryMode; return Signal(sn, only, out dir, out lvl, out why, out story); }
            dir = 0; lvl = double.NaN; why = ""; story = ""; int best = -1; var seen = new List<string>(); string keep = Set.EntryMode;
            try
            {
                foreach (var m in MultiModes)
                {
                    Set.EntryMode = m; int d; double l; string w, st;
                    if (!Signal(sn, only, out d, out l, out w, out st) || d == 0) continue;
                    string scW; int sc = Score(sn, d, out scW); if (m == "ENGULF" || m == "ENG123" || m == "VWAPREJ" || m == "FVG50" || m == "RETEST") sc += 5;   // a retrace / rejection entry: not a chase by design
                    seen.Add(SetupName(m) + " " + (d > 0 ? "BUY" : "SELL") + " " + sc);
                    if (sc > best) { best = sc; dir = d; lvl = l; why = SetupName(m) + " — " + w; story = st; LastSetup = m; }
                }
            }
            finally { Set.EntryMode = keep; }
            if (dir == 0) return false;
            if (seen.Count > 1) story += " (🎯 " + seen.Count + " setups fired: " + string.Join(", ", seen) + " → the best one)";
            return true;
        }
        void SignalsOne(Snap sn)
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
                if (run >= Set.AggRun && (green || red)) { AggRunNow = run; AggDir = green ? 1 : -1; AggLevel = green ? c.H : c.L; AggBar = c.Time; SignalText = run + " " + (green ? "red" : "green") + " " + Tf.Name(Set.Tf) + " candles, then the " + c.Time.ToString("HH:mm", IC) + " candle closed " + (green ? "GREEN" : "RED") + " (O " + P(c.O) + " C " + P(c.C) + ") — " + (green ? "BUY above its high " : "SELL below its low ") + P(AggLevel) + " during the next candle"; }
                else if (AggDir != 0 && Now >= AggBar.AddMinutes(2 * Set.Tf)) { AggDir = 0; AggLevel = double.NaN; }
            }
            if (Set.EntryMode == "RETEST" && sn.Fin.Count >= 22 && sn.Fin[0].Time != rtBar)
            {   // 🧱 S/R RETEST: a key level broken by a CLOSE, then within 30 candles the price comes back to it and is REJECTED (a candle touches it and
                // closes back on the break side) → enter the break's way: broken support = new resistance (SELL), broken resistance = new support (BUY)
                rtBar = sn.Fin[0].Time; rtDir = 0; var c = sn.Fin[0]; var p1 = sn.Fin[1]; double atr = Math.Max(0.05, sn.Fin.Take(14).Average(x => x.H - x.L));
                foreach (var k in KeyLevels(sn))
                {   // a fresh break of this level by the candle that just closed
                    if (p1.C >= k.Item1 && c.C < k.Item1 - 0.2 * atr && !rtBroken.Any(b => Math.Abs(b.Item1 - k.Item1) < 0.3 * atr)) rtBroken.Add(Tuple.Create(k.Item1, -1, c.Time, k.Item2));
                    if (p1.C <= k.Item1 && c.C > k.Item1 + 0.2 * atr && !rtBroken.Any(b => Math.Abs(b.Item1 - k.Item1) < 0.3 * atr)) rtBroken.Add(Tuple.Create(k.Item1, 1, c.Time, k.Item2));
                }
                rtBroken.RemoveAll(b => (c.Time - b.Item3).TotalMinutes > 30 * Set.Tf);
                foreach (var b in rtBroken.ToList())
                {
                    if (b.Item3 == c.Time) continue;   // the break candle itself is not the retest
                    bool rej = b.Item2 < 0 ? c.H >= b.Item1 - 0.15 * atr && c.C < b.Item1 && c.C < c.O : c.L <= b.Item1 + 0.15 * atr && c.C > b.Item1 && c.C > c.O;
                    bool gone = b.Item2 < 0 ? c.C > b.Item1 + 0.3 * atr : c.C < b.Item1 - 0.3 * atr;   // closed back through it: the break failed
                    if (gone) { rtBroken.Remove(b); continue; }
                    if (rej) { rtDir = b.Item2; rtLevel = b.Item1; rtStory = "🧱 RETEST: " + b.Item4 + " " + P(b.Item1) + " was broken " + (b.Item2 < 0 ? "DOWN" : "UP") + " at " + b.Item3.ToString("HH:mm", IC) + "; the " + c.Time.ToString("HH:mm", IC) + " candle came back to it (" + (b.Item2 < 0 ? "high " + P(c.H) : "low " + P(c.L)) + ") and closed " + (b.Item2 < 0 ? "back under it at " : "back over it at ") + P(c.C) + " → " + (b.Item2 < 0 ? "broken support = resistance → SELL" : "broken resistance = support → BUY"); rtBroken.Remove(b); SignalText = rtStory; break; }
                }
            }
            if (Set.EntryMode == "ENG123" && sn.Fin.Count >= 3 && sn.Fin[0].Time != e123Bar)
            {   // 1-2-3 ENGULFING (user's rule): candles 1 and 2 the same colour, candle 3 the other colour ENGULFS candle 2's BODY and closes there = the entry
                var c3 = sn.Fin[0]; var c2 = sn.Fin[1]; var c1 = sn.Fin[2]; e123Bar = c3.Time;
                bool bull = c1.C < c1.O && c2.C < c2.O && c3.C > c3.O && c3.C > c2.O && c3.L <= c2.C;   // two red, a green closing above the 2nd red's body
                bool bear = c1.C > c1.O && c2.C > c2.O && c3.C < c3.O && c3.C < c2.O && c3.H >= c2.C;   // two green, a red closing below the 2nd green's body
                e123Dir = bull ? 1 : bear ? -1 : 0;
                if (e123Dir != 0) { e123Story = "1-2-3 ENGULFING: " + c1.Time.ToString("HH:mm", IC) + " and " + c2.Time.ToString("HH:mm", IC) + " closed " + (bull ? "RED" : "GREEN") + ", then the " + c3.Time.ToString("HH:mm", IC) + " candle closed " + (bull ? "GREEN at " + P(c3.C) + ", above" : "RED at " + P(c3.C) + ", below") + " the 2nd candle's body (O " + P(c2.O) + " C " + P(c2.C) + ") → " + (bull ? "BUY" : "SELL"); SignalText = e123Story; }
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
            Hedges.Add(new HedgeEvent { Iter = Current != null ? Current.N : 0, Time = Now, Kind = kind, Dir = dir, Price = dir > 0 ? sn.Ask : sn.Bid, Lots = lots, RangeLo = sn.Pos.Count > 0 ? PLo(sn) : sn.Bid, RangeHi = sn.Pos.Count > 0 ? PHi(sn) : sn.Bid, Atr = atr, Basket = Basket, Before = Current != null ? Current.AddOns : 0, Ctx = Context(sn, dir) });
            if (Hedges.Count > 3000) Hedges.RemoveRange(0, 500);
        }
        DateTime marginWait = DateTime.MinValue; string marginNote = "";
        public readonly List<SimTick> TickTape = new List<SimTick>(); double tapeBid = double.NaN, tapeAsk = double.NaN;
        public readonly List<SignalRec> Journal = new List<SignalRec>(); public Action<SignalRec> OnSignalDone; SignalRec lastSig;
        void JournalAdd(Snap sn, int dir, string why)
        {
            if (lastSig != null && lastSig.Dir == dir && (Now - lastSig.Time).TotalMinutes < Math.Max(1, Set.Tf)) return;   // one per candle and side
            double px = dir > 0 ? (double.IsNaN(sn.Ask) ? sn.Bid : sn.Ask) : sn.Bid;
            double tm = Set.Target > 0 && Set.EntryLots > 0 && sn.PerPrice > 0 ? Set.Target / (Set.EntryLots * sn.PerPrice) : 0;
            lastSig = new SignalRec { Time = Now, Dir = dir, Price = px, Why = why, TargetMove = tm, Ctx = Context(sn, dir) + ";" + Extension(sn, dir) }; Journal.Add(lastSig);
        }
        // how EXTENDED the move is at the signal (user's example: a break at the very high with no pullback before it)
        string Extension(Snap sn, int dir)
        {
            var f = sn.Fin; if (f.Count < 21 || double.IsNaN(sn.Bid)) return "ext=?";
            double atr = Math.Max(0.05, f.Take(14).Average(x => x.H - x.L)), mean = f.Take(20).Average(x => x.C), hi = f.Take(20).Max(x => x.H), lo = f.Take(20).Min(x => x.L), rg = Math.Max(1e-9, hi - lo);
            double ext = dir * (sn.Bid - mean) / atr; int run = 0; for (int i = 0; i < f.Count && i < 30; i++) { if (dir > 0 ? f[i].C > f[i].O : f[i].C < f[i].O) run++; else break; }
            double pb = dir > 0 ? (hi - f.Take(6).Min(x => x.L)) / rg : (f.Take(6).Max(x => x.H) - lo) / rg;   // how deep the last 6 candles pulled back from the extreme
            var l = new List<string> { "ext=" + (ext >= 2.5 ? "very" : ext >= 1.2 ? "extended" : ext <= -0.5 ? "against" : "normal"), "extAtr=" + ext.ToString("0.0", IC), "run=" + (run >= 5 ? "5+" : run.ToString(IC)), "pullback=" + (pb >= 0.4 ? "deep" : pb >= 0.15 ? "some" : "none") };
            if (!double.IsNaN(sn.DayHi)) l.Add("toDayEdge=" + ((dir > 0 ? sn.DayHi - sn.Bid : sn.Bid - sn.DayLo) / atr <= 0.5 ? "at" : "room"));
            return string.Join(";", l);
        }
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
            if (pendWhy.StartsWith("🚀 STACK") && px != null) { stackTickets.Add(px.Ticket); stackDir = dir; }
            bool aobFill = pendWhy.StartsWith("➕ ADD ON BREAK"); double aobKeepLv = HedgeLevel; string aobKeepWhy = HedgeWhy;
            if (aobFill) { aobAdds++; AobAddsTotal++; aobTicket = px != null ? px.Ticket : 0; aobStop = aobPendStop; if (px != null) Prot.Flag(px.Ticket); } else if (px != null) Prot.Engine(px.Ticket);   // 🛡 the engine's own legs are never "adds"
            if (it.Buys + it.Sells >= 1 && !pendWhy.StartsWith("ENTRY")) radarDue = new DateTime(Now.Ticks - Now.Ticks % TimeSpan.TicksPerMinute).AddMinutes(1);   // ⚠ the radar once this minute has finished
            if (dir > 0) { it.Buys++; it.BuyLots += pendLots; } else { it.Sells++; it.SellLots += pendLots; }
            if (pendWhy.StartsWith("ADD TO THE WINNER")) it.WinnerAdds++; else if (aobFill) Record(sn, "ADD", dir, pendLots); else if (pendWhy.StartsWith("🛡")) Record(sn, pendWhy.StartsWith("🛡 RESUME") ? "RESUME" : "LOCK", dir, pendLots); else if (it.Buys + it.Sells > 1) { it.AddOns++; Record(sn, pendWhy.StartsWith("🔒") ? "LOCK" : pendWhy.StartsWith("🔓") ? "UNLOCK" : "HEDGE", dir, pendLots); }
            it.MaxGross = Math.Max(it.MaxGross, Gross(sn.Pos));
            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = dir > 0 ? "BUY" : "SELL", Lots = pendLots, Price = px != null ? px.Open : dir > 0 ? sn.Ask : sn.Bid, Level = pendLevel, Why = pendWhy, Basket = Basket });
            lastDir = dir; knownGross = Gross(sn.Pos); quickBest = double.NaN; scaleRef = double.NaN; ManualHedge = double.NaN; ManualNoHedge = false; SetHedgeLevel(sn, dir); if (aobFill) { HedgeLevel = aobKeepLv; HedgeWhy = aobKeepWhy; }   /* ➕ the add keeps the basket's next hedge where it was */
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
                HedgeLevel = double.IsNaN(dist) ? double.NaN : dir > 0 ? from - Math.Max(dist, sp) : from + Math.Max(dist, sp); RangeOnlyLevel(sn, dir, sp); CapLevel(sn, dir, sp); QuickLevel(sn, dir, sp); CandleLevel(sn, dir, sp); KeyLevel(sn, dir, sp); TrendLevel(sn, dir, sp); DdTightLevel(sn, dir, sp); BoxLevel(sn, dir, sp); ApplyManual(); return;
            }
            double lv = dir > 0 ? Low(sn) : High(sn);
            if (double.IsNaN(lv) && Set.HedgeCap > 0 && sn.Pos.Count > 0) { HedgeLevel = double.NaN; HedgeWhy = ""; CapLevel(sn, dir, sp); QuickLevel(sn, dir, sp); CandleLevel(sn, dir, sp); KeyLevel(sn, dir, sp); DdTightLevel(sn, dir, sp); BoxLevel(sn, dir, sp); return; }   // no swing (a straight trend): the max distance is the hedge
            if (double.IsNaN(lv)) { HedgeLevel = double.NaN; HedgeWhy = ""; return; }
            HedgeLevel = Set.Rolling ? lv : dir > 0 ? Math.Min(lv, sn.Bid - sp) : Math.Max(lv, sn.Bid + sp);
            HedgeWhy = Candles(sn, dir < 0) + (Math.Abs(HedgeLevel - lv) > 1e-9 ? ", moved to " + P(HedgeLevel) + " (at least the spread from the price)" : "");
            RangeOnlyLevel(sn, dir, sp); CapLevel(sn, dir, sp); QuickLevel(sn, dir, sp); CandleLevel(sn, dir, sp); KeyLevel(sn, dir, sp); TrendLevel(sn, dir, sp); DdTightLevel(sn, dir, sp); BoxLevel(sn, dir, sp); ApplyManual();
        }
        // 📦 NO FLIPS IN A BOX (RACE lane only): the hedge waits beyond the high / low of the last BoxBars finished candles
        void BoxLevel(Snap sn, int dir, double sp)
        {
            if (Set.BoxBars <= 0 || double.IsNaN(HedgeLevel) || sn.Fin.Count == 0) return;
            var box = sn.Fin.Take(Set.BoxBars).ToList(); double lo = box.Min(x => x.L), hi = box.Max(x => x.H), was = HedgeLevel;
            HedgeLevel = dir > 0 ? Math.Min(HedgeLevel, lo - sp) : Math.Max(HedgeLevel, hi + sp);
            if (Math.Abs(HedgeLevel - was) > 1e-9) HedgeWhy += " → 📦 no flip inside the " + box.Count + "-candle box " + P(lo) + "–" + P(hi) + ": moved to " + P(HedgeLevel);
        }
        // the key levels near the price: repeated swing lows / highs (2+ touches), VWAP, EMA 50 / 200, the day's high / low, round $5 numbers
        List<Tuple<double, string>> KeyLevels(Snap sn)
        {
            var res = new List<Tuple<double, string>>(); var f = sn.Fin; if (f.Count < 10) return res;
            double atr = Math.Max(0.05, f.Take(14).Average(x => x.H - x.L)), tol = 0.35 * atr;
            var lows = new List<double>(); var highs = new List<double>();
            for (int j = 2; j + 2 < f.Count; j++)
            {
                if (f[j].L < f[j - 1].L && f[j].L < f[j - 2].L && f[j].L < f[j + 1].L && f[j].L < f[j + 2].L) lows.Add(f[j].L);
                if (f[j].H > f[j - 1].H && f[j].H > f[j - 2].H && f[j].H > f[j + 1].H && f[j].H > f[j + 2].H) highs.Add(f[j].H);
            }
            foreach (var grp in new[] { Tuple.Create(lows, "swing lows"), Tuple.Create(highs, "swing highs") })
                foreach (var v in grp.Item1) { int touches = grp.Item1.Count(w => Math.Abs(w - v) <= tol) + f.Count(b => Math.Abs((grp.Item2 == "swing lows" ? b.L : b.H) - v) <= tol / 2) - 1; if (touches >= 2 && !res.Any(r => Math.Abs(r.Item1 - v) <= tol)) res.Add(Tuple.Create(v, grp.Item2 + " ×" + touches)); }
            if (!double.IsNaN(Vwap)) res.Add(Tuple.Create(Vwap, "VWAP"));
            if (!double.IsNaN(Ema50)) res.Add(Tuple.Create(Ema50, "EMA 50")); if (!double.IsNaN(Ema200)) res.Add(Tuple.Create(Ema200, "EMA 200"));
            if (!double.IsNaN(sn.DayHi)) { res.Add(Tuple.Create(sn.DayHi, "the day's high")); res.Add(Tuple.Create(sn.DayLo, "the day's low")); }
            if (!double.IsNaN(sn.Bid) && sn.Bid > 100) { double r5 = Math.Round(sn.Bid / 5) * 5; for (int k = -2; k <= 2; k++) res.Add(Tuple.Create(r5 + 5 * k, "round " + (r5 + 5 * k).ToString("0", IC))); }
            return res;
        }
        void KeyLevel(Snap sn, int dir, double sp)
        {
            if (!Set.KeyHedge || double.IsNaN(HedgeLevel) || sn.Fin.Count < 10 || Set.HedgeMode == "NONE" || Set.HedgeMode == "SIGNAL" || Set.HedgeMode == "LADDER") return;
            double atr = Math.Max(0.05, sn.Fin.Take(14).Average(x => x.H - x.L)), zone = Math.Max(0.6 * atr, 3 * sp), buf = Math.Max(Set.KeyBuffer * atr, 2 * sp);
            var keys = KeyLevels(sn); double h = HedgeLevel, moved = h; string why = "";
            for (int pass = 0; pass < 3; pass++)
            {   // a long basket's SELL hedge below: a key level just ABOVE it (between it and the price) = support → below that support; short: mirrored
                var k = keys.Where(x => dir > 0 ? x.Item1 >= moved - 1e-9 && x.Item1 <= moved + zone && x.Item1 < sn.Bid : x.Item1 <= moved + 1e-9 && x.Item1 >= moved - zone && x.Item1 > sn.Bid).OrderBy(x => Math.Abs(x.Item1 - moved)).FirstOrDefault();
                if (k == null) break; double nh = dir > 0 ? k.Item1 - buf : k.Item1 + buf; if (dir > 0 ? nh >= moved : nh <= moved) break; moved = nh; why = k.Item2 + " " + P(k.Item1);
            }
            if (why == "") return;
            if (Set.HedgeCap > 0 && sn.Pos.Count > 0) { double edge = dir > 0 ? PLo(sn) : PHi(sn), lim = Set.HedgeCap + atr; if (Math.Abs(edge - moved) > lim) moved = dir > 0 ? edge - lim : edge + lim; }
            if (Math.Abs(moved - h) < 1e-9) return;
            HedgeLevel = moved; HedgeWhy += " → 🔑 KEY LEVEL: " + why + " was right " + (dir > 0 ? "above" : "below") + " the hedge (the price may bounce there) → moved " + (dir > 0 ? "below" : "above") + " it to " + P(moved);
        }
        // 📉 the deeper the basket, the CLOSER the next hedge (measured from the newest leg, so it never chases the price)
        // 🛟 a basket that went RescuePct % of the balance deep (or reached the danger level) only wants break-even now
        public bool RescueActive(AutoIteration it) { return Set.Rescue && it != null && it.BalanceStart > 0 && Set.ExitMode != "LEGS" && (it.MaxDd <= -Set.RescuePct / 100 * it.BalanceStart || (Set.RescueMin > 0 && (Now - it.Start).TotalMinutes >= Set.RescueMin && it.MaxDd < -RescueKeepMin) || (!Set.DangerExit && Set.DangerAt > 0 && DangerPeak >= Set.DangerAt)); }   // DANGER EXIT on: it takes its own smaller profit there
        const double RescueKeepMin = 1;   // the time rule only for a basket that was ever below $0 (a calm winner keeps its target)
        double RescueKeep(Snap sn) { return Math.Max(0.5, Set.CommissionPerLot * sn.Pos.Sum(p => p.Lots)); }
        public double DdTightFactor = 1; double ddNormal = double.NaN;   // ddNormal: the hedge before it came closer (each tick re-measures from it)
        void DdTightLevel(Snap sn, int dir, double sp, bool fresh = true)
        {
            if (fresh) { ddNormal = HedgeLevel; DdTightFactor = 1; }
            else if (double.IsNaN(ddNormal) || double.IsNaN(HedgeLevel)) return;
            double cur = HedgeLevel; HedgeLevel = ddNormal; string whyWas = HedgeWhy;
            DdTightCore(sn, dir, sp);
            if (!fresh) { bool closer = dir > 0 ? HedgeLevel > cur + 1e-9 : HedgeLevel < cur - 1e-9; if (!closer) { HedgeLevel = cur; HedgeWhy = whyWas; } else Version++; }
        }
        void DdTightCore(Snap sn, int dir, double sp)
        {
            if (Set.DdTightEvery <= 0 || Set.DdTightBy <= 0 || double.IsNaN(HedgeLevel) || Current == null || sn.Pos.Count == 0 || Current.BalanceStart <= 0) return;
            if (Set.HedgeMode == "NONE" || Set.HedgeMode == "SIGNAL" || Set.HedgeMode == "LADDER") return;
            double dd = Math.Max(0, -(Tot(sn, Current))), ddPct = 100 * dd / Current.BalanceStart; int steps = (int)Math.Floor(ddPct / Set.DdTightEvery); if (steps <= 0) return;
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); if (leg == null) return;
            double normal = dir > 0 ? leg.Open - HedgeLevel : HedgeLevel - leg.Open; if (normal <= 0) return;
            double f = Math.Max(Set.DdTightFloor / 100.0, 1 - steps * Set.DdTightBy / 100.0);
            double atr = sn.Fin.Count >= 5 ? sn.Fin.Take(14).Average(x => x.H - x.L) : 0, minD = Math.Max(0.5 * atr, 3 * sp), d2 = Math.Max(minD, normal * f);
            if (Set.RangeOnly) { double edge = dir > 0 ? leg.Open - (PLo(sn) - sp) : (PHi(sn) + sp) - leg.Open; d2 = Math.Max(d2, edge); }   // never inside the basket's range
            if (d2 >= normal - 1e-9) return;
            double was = HedgeLevel; HedgeLevel = dir > 0 ? leg.Open - d2 : leg.Open + d2; DdTightFactor = d2 / normal;
            HedgeWhy += " → 📉 DRAWDOWN " + ddPct.ToString("0.0", IC) + " % of the balance: the hedge comes " + (100 - 100 * d2 / normal).ToString("0", IC) + " % closer (" + P(was) + " → " + P(HedgeLevel) + ", " + d2.ToString("0.00", IC) + " from the newest leg " + P(leg.Open) + ")" + ((dir > 0 ? sn.Bid <= HedgeLevel : sn.Bid >= HedgeLevel) ? " — the price is already past it: hedge NOW" : "");
        }
        void TrendLevel(Snap sn, int dir, double sp)
        {
            if (!Set.TrendHedge || double.IsNaN(HedgeLevel) || sn.Pos.Count == 0 || double.IsNaN(Ema200)) return;
            double net = sn.Pos.Sum(p => p.Dir * p.Lots); if (Math.Sign(Math.Round(net, 6)) != dir || TrendNow != dir) return;   // the trend is WITH the basket's net side
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); if (leg == null) return;
            double normal = Math.Abs(leg.Open - HedgeLevel), edge = dir > 0 ? PLo(sn) - sp : PHi(sn) + sp, deep = dir > 0 ? leg.Open - Set.TrendHedgeMult * normal : leg.Open + Set.TrendHedgeMult * normal;
            double was = HedgeLevel; HedgeLevel = dir > 0 ? Math.Min(HedgeLevel, Math.Min(edge, deep)) : Math.Max(HedgeLevel, Math.Max(edge, deep));
            if (Math.Abs(HedgeLevel - was) > 1e-9) HedgeWhy += " → 🧭 the trend is " + (dir > 0 ? "UP" : "DOWN") + " with the basket: the hedge waits deeper, at " + P(HedgeLevel) + " (beyond the basket's " + (dir > 0 ? "lowest" : "highest") + " leg, ≥ " + Set.TrendHedgeMult.ToString("0.##", IC) + "× the normal distance)";
        }
        void CapLevel(Snap sn, int dir, double sp)
        {
            if (Set.HedgeCap <= 0 || sn.Pos.Count == 0) return;
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault();
            double from = Set.RangeOnly ? (dir > 0 ? PLo(sn) : PHi(sn)) : leg != null ? leg.Open : (dir > 0 ? PLo(sn) : PHi(sn));
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
            double bal = Math.Max(1, it.BalanceStart), loss = -(Tot(sn, it)), ddPct = Math.Max(0, loss) / bal * 100;
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
            double netL = sn.Pos.Sum(p => p.Dir * p.Lots); if (Math.Abs(netL) < 0.005) { Locked = true; LockHi = Math.Max(PHi(sn), sn.Ask); LockLo = Math.Min(PLo(sn), sn.Bid); return; }
            Locked = true; lockAt = Now; LockHi = Math.Max(PHi(sn), sn.Ask); LockLo = Math.Min(PLo(sn), sn.Bid); int ld = netL > 0 ? -1 : 1; double ll = Math.Round(Math.Abs(netL), 2);
            Tell("🛡 RISK GUARD → LOCK. WHY: " + why + " → " + (ld > 0 ? "BUY " : "SELL ") + ll.ToString("0.##", IC) + " so buys = sells (net 0): the basket is frozen at about " + M(Basket) + (Set.LockOn || Set.RiskGuard ? " and waits for the break of " + P(LockLo) + "–" + P(LockHi) + " (no more hedges inside)" : " — LOCK / RISK GUARD are off, so nothing waits for a break: the normal hedge rules go on from here (the lock ends as soon as a hedge makes the basket uneven)"));
            Order(sn, ld, ll, "🛡 RISK GUARD LOCK " + (ld > 0 ? "BUY" : "SELL") + " — " + why.Split('—')[0].Trim(), sn.Bid);
        }
        void CandleLevel(Snap sn, int dir, double sp)
        {
            if (!Set.CandleHedge || sn.Fin.Count == 0 || sn.Pos.Count == 0 || double.IsNaN(sn.Bid) || Set.HedgeMode == "NONE" || Set.HedgeMode == "SIGNAL" || Set.HedgeMode == "LADDER") return;
            var leg = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); if (leg == null) return;
            var c = sn.Fin.FirstOrDefault(x => x.Time <= leg.Time && x.Time.AddMinutes(Set.Tf) > leg.Time);   // the entry candle, once it has finished
            bool done = c != null; if (c == null) c = sn.Fin[0];                                                  // still forming: the candle before it (the breakout candle)
            double q = dir > 0 ? c.L - sp : c.H + sp;
            if (Set.RangeOnly && sn.Pos.Count >= 2) { double edgeR = dir > 0 ? PLo(sn) - sp : PHi(sn) + sp; q = dir > 0 ? Math.Min(q, edgeR) : Math.Max(q, edgeR); }   // never inside the basket's range
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
            double edge = dir > 0 ? PHi(sn) : PLo(sn), run = dir > 0 ? ext - edge : edge - ext;
            if (run < Set.QuickAfter) return;
            var last = sn.Fin[0]; double q = dir > 0 ? Math.Min(last.L, sn.Bid - sp) : Math.Max(last.H, sn.Bid + sp);
            if (!double.IsNaN(quickBest)) q = dir > 0 ? Math.Max(q, quickBest) : Math.Min(q, quickBest);   // LOCKED IN: once it trailed up / down it never moves back away from the price (user's catch)
            quickBest = q;
            if (Set.RangeOnly && sn.Pos.Count >= 2) { double edgeR = dir > 0 ? PLo(sn) - sp : PHi(sn) + sp; q = dir > 0 ? Math.Min(q, edgeR) : Math.Max(q, edgeR); }   // never INSIDE the basket's range (range-only wins: no hedging in its own chop)
            if (!double.IsNaN(HedgeLevel) && (dir > 0 ? q <= HedgeLevel : q >= HedgeLevel)) return;   // only ever CLOSER to the price
            HedgeLevel = q; HedgeWhy = "⚡ QUICK HEDGE: the price ran " + run.ToString("0.00", IC) + " beyond the basket's best leg " + P(edge) + " (≥ " + Set.QuickAfter.ToString("0.##", IC) + ") → the hedge sits at the last candle's " + (dir > 0 ? "LOW " : "HIGH ") + P(q) + " (" + last.Time.ToString("HH:mm", IC) + "): the first candle that breaks it hedges";
        }
        void RangeOnlyLevel(Snap sn, int dir, double sp)
        {
            if (!Set.RangeOnly || (sn.Pos.Count < 2 && double.IsNaN(cmpHi)) || double.IsNaN(HedgeLevel)) return;
            // ANTI-CHOP: never inside the basket's own range — a hedge SELL only under its lowest leg, a hedge BUY only over its highest
            double lo = PLo(sn), hi = PHi(sn), was = HedgeLevel;
            HedgeLevel = dir > 0 ? Math.Min(HedgeLevel, lo - sp) : Math.Max(HedgeLevel, hi + sp);
            if (Math.Abs(HedgeLevel - was) > 1e-9) HedgeWhy += " → moved BEYOND the basket's range " + P(lo) + "–" + P(hi) + " to " + P(HedgeLevel) + " (range-only hedging)";
        }
        // 🪓 one de-risk step: 1) close y lots of the best winner (books a profit), 2) next tick close x lots of the worst loser, x sized so the
        // pair's loss never exceeds what step 1 booked; the net side toward the winners stays ≥ the lead; at most one pair per closed candle
        int trimCount, trimNoted = -1, hstopNoted = -1;
        long deLoser; double deLoserLots, deBooked; DateTime deCandle = DateTime.MinValue;
        bool DeRiskStep(Snap sn, AutoIteration it)
        {
            Func<Mt5Position, double> pl = p => p.Profit + p.Swap + p.Commission;
            double fee = Set.CommissionPerLot;
            if (deLoser != 0)
            {   // step 2: the loser's part
                var lp = sn.Pos.FirstOrDefault(p => p.Ticket == deLoser); long tk = deLoser; deLoser = 0; if (lp == null || lp.Lots <= 0) return false;
                double perLot = -pl(lp) / lp.Lots + fee; double x2 = perLot > 0 ? Math.Floor((deBooked / perLot) * 100 + 1e-6) / 100 : lp.Lots; x2 = Math.Min(Math.Min(x2, deLoserLots), lp.Lots); if (x2 < 0.01) return false;
                Tell("🪓 DE-RISK (2/2): close " + x2.ToString("0.##", IC) + " of the losing " + lp.Side + " #" + tk + " (" + M(-perLot * x2) + ") with the " + M(deBooked) + " just booked → the total stays, " + x2.ToString("0.##", IC) + " lot less risk.");
                double gb = Gross(sn.Pos); Refused = ""; var id2 = Ask("CLOSE", new object[] { tk, x2 }, "CLOSE " + x2.ToString("0.##", IC) + " of #" + tk + " " + lp.Side + " " + Set.Symbol + " (🤖 #" + it.N + " 🪓 de-risk)");
                if (id2 == null) { if (sugg) return true; Note("⚠ de-risk not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return true; }
                pendId = id2; pendKind = "SCALE"; pendTicket = tk; pendGross = gb; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
                Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = x2, Price = lp.Current, Level = double.NaN, Why = "🪓 DE-RISK loser #" + tk, Basket = Basket });
                return true;
            }
            if (sn.Pos.Count < 2 || sn.Fin.Count == 0 || sn.Fin[0].Time == deCandle) return false;
            double net = sn.Pos.Sum(p => p.Dir * p.Lots); int wd = Math.Sign(Math.Round(net, 6)); if (wd == 0) return false;
            var win = sn.Pos.Where(p => p.Dir == wd && pl(p) > 0).OrderByDescending(p => pl(p) / p.Lots).FirstOrDefault();
            var los = sn.Pos.Where(p => p.Dir == -wd && pl(p) < 0).OrderBy(p => pl(p) / p.Lots).FirstOrDefault(); if (win == null || los == null) return false;
            double wpl = pl(win) / win.Lots - fee, lpl = -pl(los) / los.Lots + fee; if (wpl <= 0 || lpl <= 0) return false;
            // y of the winner and x = y·wpl/lpl of the loser: the net (winner side) changes by −(y − x) and must stay ≥ the lead
            double lead = Math.Max(0.01, Set.NetAdvantage), room = Math.Abs(net) - lead, k = 1 - wpl / lpl;   // net drop per lot of y — the lead toward the winners stays (a 0.01 lean could not recover: the chop replay)
            double y = k > 1e-9 ? Math.Min(win.Lots, room / k) : win.Lots; y = Math.Floor(y * 100 + 1e-6) / 100;
            double x = Math.Floor(y * wpl / lpl * 100 + 1e-6) / 100; x = Math.Min(x, los.Lots);
            if (y < 0.01 || x < 0.01) return false;
            deCandle = sn.Fin[0].Time; deLoser = los.Ticket; deLoserLots = x; deBooked = y * wpl;
            Tell("🪓 DE-RISK (1/2). WHY: 🛟 rescue is on and the " + win.Side + "S are paying → close " + y.ToString("0.##", IC) + " of " + win.Side + " #" + win.Ticket + " (+" + M(deBooked) + "), then " + x.ToString("0.##", IC) + " of the losing " + los.Side + " #" + los.Ticket + " with it: the total stays, the basket gets " + (x + y).ToString("0.##", IC) + " lots smaller, the " + (wd > 0 ? "buys" : "sells") + " keep leading by ≥ " + lead.ToString("0.##", IC) + ".");
            double gb1 = Gross(sn.Pos); Refused = ""; var id = Ask("CLOSE", new object[] { win.Ticket, y }, "CLOSE " + y.ToString("0.##", IC) + " of #" + win.Ticket + " " + win.Side + " " + Set.Symbol + " (🤖 #" + it.N + " 🪓 de-risk)");
            if (id == null) { deLoser = 0; if (sugg) return true; Note("⚠ de-risk not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return true; }
            pendId = id; pendKind = "SCALE"; pendTicket = win.Ticket; pendGross = gb1; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = y, Price = win.Current, Level = double.NaN, Why = "🪓 DE-RISK winner #" + win.Ticket, Basket = Basket });
            return true;
        }

        // ================= ⏰ 9:30 STRADDLE =================
        string stDay = ""; int stStage, stWin; double stRef = double.NaN, stX, stLots, stBest = double.NaN, stWinEntry; bool stHalf;
        public string StradInfo = "";   // the chart / story line: X, the triggers, the winner's stop / target
        // the previous New York day's 9:30–16:00 range (high − low) from the M1 candles; NaN = none
        public double StradRange(IBroker b, DateTime nyToday)
        {
            lock (b.State.Gate)
            {
                var se = b.State.SeriesOf(Set.Symbol, 1); DateTime day = DateTime.MinValue; double hi = double.MinValue, lo = double.MaxValue;
                for (int i = se.Bars.Count - 1; i >= 0; i--)
                {
                    var ny = se.Bars[i].Time.AddHours(Set.NyOffsetHours); if (ny.Date >= nyToday.Date) continue; int m = ny.Hour * 60 + ny.Minute; if (m < 570 || m >= 960) continue;
                    if (day == DateTime.MinValue) day = ny.Date; else if (ny.Date != day) break;
                    hi = Math.Max(hi, se.Bars[i].H); lo = Math.Min(lo, se.Bars[i].L);
                }
                return day == DateTime.MinValue ? double.NaN : hi - lo;
            }
        }
        static double Down01(double v) { return Math.Floor(v * 100 + 1e-6) / 100; }
        void StradClose(Mt5Position p, double lots, string why)
        {
            bool all = lots <= 0 || lots >= p.Lots - 1e-9; double gb = Gross(lastSnapPos);   /* before the close: the broker may change the position in place */
            Refused = ""; var id = Ask("CLOSE", new object[] { p.Ticket, all ? 0.0 : lots }, "CLOSE " + (all ? "" : lots.ToString("0.##", IC) + " of ") + "#" + p.Ticket + " " + p.Side + " " + Set.Symbol + " (🤖 #" + Current.N + " " + why + ")");
            if (id == null) { if (sugg) return; Note("⚠ close of #" + p.Ticket + " not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
            pendId = id; pendKind = all ? "CLOSE1" : "SCALE"; pendTicket = p.Ticket; pendGross = gb; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
            Trades.Add(new AutoTrade { Iter = Current.N, Time = Now, Side = "CLOSE", Lots = all ? p.Lots : lots, Price = p.Current, Level = double.NaN, Why = "⏰ " + why + " #" + p.Ticket + " " + p.Side, Basket = Basket });
        }
        List<Mt5Position> lastSnapPos = new List<Mt5Position>();
        void Straddle(IBroker b, Snap sn)
        {
            lastSnapPos = sn.Pos;
            var ny = Now.AddHours(Set.NyOffsetHours); string today = ny.ToString("yyyy-MM-dd", IC); int nm = ny.Hour * 60 + ny.Minute;
            int om, cm; if (!NyMin(Set.StradOpen, out om)) om = 570; if (!NyMin(Set.StradClose, out cm)) cm = 960;
            double bid = sn.Bid, ask = double.IsNaN(sn.Ask) ? sn.Bid : sn.Ask, mid = (bid + ask) / 2;
            if (Current == null)
            {
                stStage = 0; TargetPx = double.NaN; StopPx = double.NaN;
                if (sn.Pos.Count > 0 || stDay == today || ny.DayOfWeek == DayOfWeek.Saturday || ny.DayOfWeek == DayOfWeek.Sunday) return;
                if (nm < om || nm >= om + 5) { StradInfo = "⏰ waiting for " + Set.StradOpen + " New York"; return; }   // only the open's first 5 minutes
                stDay = today;
                double x = Set.StradXMode == "POINTS" ? Set.StradX : Set.StradRangeK * StradRange(b, ny.Date);
                if (double.IsNaN(x) || x <= 0) { Tell("⏰ 9:30 STRADDLE — no candles of the previous day's 9:30–16:00 to size X (load M1 history) → no setup today"); return; }
                double per = sn.PerPrice > 0 ? sn.PerPrice : 1, lots = Math.Min(Set.StradMaxLots, Down01(Set.StradRisk / (x * per)));
                if (lots < 0.01) { Tell("⏰ 9:30 STRADDLE — the risk $" + Set.StradRisk.ToString("0", IC) + " is too small for X = " + x.ToString("0.##", IC) + " (0.01 lot risks " + M(0.01 * x * per) + ") → no setup today"); return; }
                stX = x; stLots = lots; stRef = mid; stWin = 0; stHalf = false; stBest = double.NaN;
                closedByUs = false; Current = new AutoIteration { N = NextN(), Start = Now, FirstDir = "BUY", BalanceStart = sn.Balance }; Iterations.Add(Current); Phase = "IN ITERATION";
                Current.Ctx = "setup=STRADDLE930;mode=" + Set.StradMode + ";x=" + x.ToString("0.##", IC);
                Tell("ITERATION #" + Current.N + " — ⏰ 9:30 STRADDLE: BUY " + lots.ToString("0.##", IC) + " + SELL " + lots.ToString("0.##", IC) + " " + Set.Symbol + ". X = " + x.ToString("0.##", IC) + (Set.StradXMode == "RANGE" ? " (" + Set.StradRangeK.ToString("0.##", IC) + " × yesterday's 9:30–16:00 range)" : " points") + ". " +
                    (Set.StradMode == "HOLD" ? "At +X the winner is banked (" + Set.StradHoldClose + "), the loser waits for a reversal (tested: lost money 2020–26)." : "At ±X the LOSING leg is cut; the winner gets a stop at its entry and " + (Set.StradTrail ? "a trail of X behind its best." : "a target of +" + Set.StradTargetX.ToString("0.#", IC) + "X.")) + " Everything closes at " + Set.StradClose + " New York.");
                stStage = 1; Order(sn, 1, lots, "⏰ 9:30 STRADDLE • BUY leg", double.NaN); return;
            }
            var it = Current; it.MaxDd = Math.Min(it.MaxDd, BasketTotal); it.Peak = Math.Max(it.Peak, BasketTotal); it.MaxGross = Math.Max(it.MaxGross, Gross(sn.Pos));
            if (stStage == 0) { if (today != stDay || nm >= cm) { it.Reason = "CLOSE TIME"; CloseAll(sn, "CLOSE TIME", false); } return; }   // an adopted basket (after a restart): flat at the close time
            if (stStage == 1) { if (sn.Pos.Any(p => p.Dir > 0)) { stStage = 2; Order(sn, -1, stLots, "⏰ 9:30 STRADDLE • SELL leg", double.NaN); } return; }
            if (stStage == 2)
            {
                var bL = sn.Pos.FirstOrDefault(p => p.Dir > 0); var sL = sn.Pos.FirstOrDefault(p => p.Dir < 0); if (bL == null || sL == null) return;
                stRef = (bL.Open + sL.Open) / 2; stStage = 3; StradInfo = "⏰ X " + stX.ToString("0.##", IC) + " • ▲ " + P(stRef + stX) + " / ▼ " + P(stRef - stX);
                Tell("⏰ both legs open around " + P(stRef) + " → triggers ▲ " + P(stRef + stX) + " / ▼ " + P(stRef - stX) + "."); Version++; return;
            }
            if (sn.Pos.Count == 0) { it.Reason = "CLOSED BY HAND"; Tell("✋ the straddle's positions were closed outside the automation."); Finish(sn); return; }
            if (today != stDay || nm >= cm) { it.Reason = "CLOSE TIME"; Tell("⏰ " + Set.StradClose + " New York → CLOSE ALL at " + M(BasketTotal) + "."); CloseAll(sn, "CLOSE TIME", false); return; }
            if (Set.StradRisk > 0 && BasketTotal <= -3 * Set.StradRisk) { it.Reason = "SAFETY STOP"; Tell("⛔ SAFETY STOP — the straddle is at " + M(BasketTotal) + " (≤ −3 × the risk $" + Set.StradRisk.ToString("0", IC) + ") → CLOSE ALL."); CloseAll(sn, "SAFETY STOP", false); return; }
            if (stStage == 3)
            {
                int dir = mid >= stRef + stX ? 1 : mid <= stRef - stX ? -1 : 0; TargetPx = stRef + stX; StopPx = stRef - stX; if (dir == 0) return;
                var win = sn.Pos.FirstOrDefault(p => p.Dir == dir); var los = sn.Pos.FirstOrDefault(p => p.Dir == -dir); if (win == null || los == null) return;
                stWin = dir;
                if (Set.StradMode == "HOLD")
                {
                    stStage = 5; double cl = Set.StradHoldClose == "ALL" ? win.Lots : Math.Max(0.01, Down01(win.Lots / 2));
                    Tell("⏰ +X reached " + (dir > 0 ? "UP" : "DOWN") + " → BANK " + cl.ToString("0.##", IC) + " of the winning " + win.Side + "; the losing " + los.Side + " waits for a reversal (back to " + (Set.StradLoserExit == "HALFX" ? "X/2" : "the entry") + "), +2X = close all.");
                    StradClose(win, cl, "bank the winner at +X"); return;
                }
                stStage = 4; stWinEntry = win.Open; stBest = dir > 0 ? bid : ask;
                Tell("⏰ ±X reached " + (dir > 0 ? "UP" : "DOWN") + " → CUT the losing " + los.Side + " (" + M(los.Profit + los.Swap + los.Commission) + "); the " + win.Side + " rides: stop at its entry " + P(stWinEntry) + (Set.StradTrail ? ", trailing X behind its best." : ", target " + P(stWinEntry + dir * Set.StradTargetX * stX) + "."));
                StradClose(los, 0, "cut the losing leg at ±X"); return;
            }
            if (stStage == 4)
            {
                var win = sn.Pos.FirstOrDefault(p => p.Dir == stWin); if (win == null || sn.Pos.Count > 1) return;
                if (Set.StradBankHalf && !stHalf && win.Lots >= 0.02) { stHalf = true; Tell("⏰ BANK HALF of the winner at +X."); StradClose(win, Down01(win.Lots / 2), "bank half at +X"); return; }
                double px = stWin > 0 ? bid : ask; stBest = stWin > 0 ? Math.Max(stBest, px) : Math.Min(stBest, px);
                double stop = stWinEntry; if (Set.StradTrail) { double tr = stBest - stWin * stX; stop = stWin > 0 ? Math.Max(stop, tr) : Math.Min(stop, tr); }
                double tgt = stWinEntry + stWin * Set.StradTargetX * stX; StopPx = stop; TargetPx = Set.StradTrail ? double.NaN : tgt;
                StradInfo = "⏰ " + win.Side + " rides • stop " + P(stop) + (Set.StradTrail ? " (trail)" : " • target " + P(tgt));
                if (!Set.StradTrail && (stWin > 0 ? px >= tgt : px <= tgt)) { it.Reason = "TARGET +" + Set.StradTargetX.ToString("0.#", IC) + "X"; Tell("⏰ TARGET +" + Set.StradTargetX.ToString("0.#", IC) + "X reached at " + P(px) + " → CLOSE ALL " + M(BasketTotal) + "."); CloseAll(sn, it.Reason, false); return; }
                if (stWin > 0 ? px <= stop : px >= stop) { it.Reason = Set.StradTrail && Math.Abs(stop - stWinEntry) > 1e-9 ? "TRAIL STOP" : "BREAKEVEN STOP"; Tell("⏰ " + it.Reason + " at " + P(px) + " → CLOSE ALL " + M(BasketTotal) + "."); CloseAll(sn, it.Reason, false); return; }
                return;
            }
            if (stStage == 5)
            {   // HOLD LOSER: +2X = it continued → close all; back to the loser's exit level = it reversed → close all
                double cont = stRef + stWin * 2 * stX, back = Set.StradLoserExit == "HALFX" ? stRef + stWin * stX / 2 : stRef; TargetPx = cont; StopPx = back;
                if (stWin > 0 ? mid >= cont : mid <= cont) { it.Reason = "CONTINUED +2X"; Tell("⏰ the move continued to +2X → CLOSE ALL " + M(BasketTotal) + "."); CloseAll(sn, it.Reason, false); return; }
                if (stWin > 0 ? mid <= back : mid >= back) { it.Reason = "REVERSED"; Tell("⏰ the price came back to " + P(back) + " → CLOSE ALL " + M(BasketTotal) + "."); CloseAll(sn, it.Reason, false); return; }
            }
        }
        // ===== 🧹 COMPRESS HEDGES BEFORE THE ROLLOVER =====
        // the swap of ONE lot for one night in money ($ for XAUUSD): MT5's own swap mode (1 points, 2–4 money, 5–6 annual %, 7–8 reopen ≈ points)
        public static double SwapPerLotNight(Mt5Symbol s, int dir, double price)
        {
            if (s == null) return 0; double v = dir > 0 ? s.SwapLong : s.SwapShort; double pts = s.TickSize > 0 ? s.TickValue * (s.Point / s.TickSize) : 0;
            switch (s.SwapMode) { case 1: case 7: case 8: return v * pts; case 2: case 3: case 4: return v; case 5: case 6: return price * s.Contract * v / 100.0 / 360.0; }
            return 0;
        }
        // the swap a basket pays (−) / earns (+) at the next rollover, the triple day counted; the night after `t` (server time)
        public static double SwapTonight(Mt5Symbol s, double buyLots, double sellLots, double price, DateTime t)
        {
            if (s == null) return 0; var d = t.DayOfWeek; if (d == DayOfWeek.Saturday || d == DayOfWeek.Sunday) return 0;
            int mult = (int)d == s.Swap3Day ? 3 : 1;
            return Math.Round((buyLots * SwapPerLotNight(s, 1, price) + sellLots * SwapPerLotNight(s, -1, price)) * mult, 2);
        }
        // the pairs a compress closes: min(buy, sell) of each side; what stays = the net (the same side, the same lots)
        public static double CompressLots(double buyLots, double sellLots) { return Math.Round(Math.Min(buyLots, sellLots), 2); }
        public double SwapNight, CompressSave, CompressGross, CompressNet; public DateTime CompressAt = DateTime.MinValue; public string CompressWhy = ""; public int Compressions;
        int cmpIter = -1, cmpOweDir; double cmpHi = double.NaN, cmpLo = double.NaN, cmpOwe;
        // the basket's highest / lowest leg ever in this iteration (a compress closes legs, the range-only hedge keeps the range it had)
        double PHi(Snap sn) { double h = sn.Pos.Count > 0 ? sn.Pos.Max(p => p.Open) : double.NaN; if (Set.Compress && !double.IsNaN(cmpHi)) h = double.IsNaN(h) ? cmpHi : Math.Max(h, cmpHi); return h; }
        double PLo(Snap sn) { double l = sn.Pos.Count > 0 ? sn.Pos.Min(p => p.Open) : double.NaN; if (Set.Compress && !double.IsNaN(cmpLo)) l = double.IsNaN(l) ? cmpLo : Math.Min(l, cmpLo); return l; }
        void SwapFigures(Snap sn)
        {
            double bl = sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots), sl = sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots), px = double.IsNaN(sn.Bid) ? 0 : sn.Bid, m = CompressLots(bl, sl);
            SwapNight = sn.Pos.Count == 0 ? 0 : SwapTonight(sn.Sym, bl, sl, px, sn.Now);
            CompressGross = Math.Round(bl + sl, 2); CompressNet = Math.Round(bl - sl, 2);
            CompressSave = m < 0.01 ? 0 : -SwapTonight(sn.Sym, m, m, px, sn.Now);   // what the matched pairs would pay tonight (+ = saved by compressing)
            CompressAt = sn.Now.Date.AddDays(1).AddMinutes(-Math.Max(1, Set.CompressBefore));
        }
        // why it compresses now ("" = not now): the rollover window, a hedged pair that PAYS swap, no frozen lock, the danger meter under its level
        string CompressNow(Snap sn)
        {
            if (!Set.Compress || sn.Pos.Count == 0 || Current == null) return "";
            double bl = sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots), sl = sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots), m = CompressLots(bl, sl);
            if (m < 0.01 || Math.Abs(bl - sl) < 0.005) { CompressWhy = m < 0.01 ? "" : "net 0 (a lock) — nothing to compress to"; return ""; }
            var d = sn.Now.DayOfWeek; int mins = sn.Now.Hour * 60 + sn.Now.Minute; double net = Math.Abs(bl - sl);
            if (Set.CompressMode == "ALWAYS")
            {
                if (Locked && (Set.LockOn || Set.RiskGuard)) { CompressWhy = "not now: the basket is LOCKED by the risk guard"; return ""; }
                double over = bl + sl - 2 * net - Set.EntryLots; if (over <= 1e-9) { CompressWhy = "compact: gross " + (bl + sl).ToString("0.##", IC) + " ≤ 2 × net " + net.ToString("0.##", IC) + " + " + Set.EntryLots.ToString("0.##", IC); return ""; }
                return "gross " + (bl + sl).ToString("0.##", IC) + " > 2 × net " + net.ToString("0.##", IC) + " + the entry " + Set.EntryLots.ToString("0.##", IC) + " → matched pairs closed (net " + (bl > sl ? "BUY " : "SELL ") + net.ToString("0.##", IC) + " unchanged), swap / margin saved on them";
            }
            if (d == DayOfWeek.Saturday || d == DayOfWeek.Sunday || mins < 24 * 60 - Math.Max(1, Set.CompressBefore)) { CompressWhy = "waits for " + CompressAt.ToString("HH:mm", IC) + " (" + Set.CompressBefore + " min before the rollover)"; return ""; }
            if (Locked && (Set.LockOn || Set.RiskGuard)) { CompressWhy = "not now: the basket is LOCKED by the risk guard"; return ""; }
            double save = -SwapTonight(sn.Sym, m, m, double.IsNaN(sn.Bid) ? 0 : sn.Bid, sn.Now);
            if (save <= 0) { CompressWhy = "not needed: the matched pairs " + (save < 0 ? "EARN" : "pay no") + " swap tonight"; return ""; }
            return "gross " + (bl + sl).ToString("0.##", IC) + " → " + Math.Abs(bl - sl).ToString("0.##", IC) + " lot (net " + (bl > sl ? "BUY " : "SELL ") + Math.Abs(bl - sl).ToString("0.##", IC) + " unchanged), " + m.ToString("0.##", IC) + " BUY + " + m.ToString("0.##", IC) + " SELL closed, swap saved tonight ≈ $" + save.ToString("0.00", IC) + ((int)d == (sn.Sym != null ? sn.Sym.Swap3Day : 3) ? " (×3 triple day)" : "");
        }
        bool CompressStep(Snap sn, AutoIteration it)
        {
            if (!Set.Compress || sn.Pos.Count == 0) return false;
            if (cmpIter != it.N) { cmpIter = it.N; cmpHi = double.NaN; cmpLo = double.NaN; cmpOwe = 0; }
            foreach (var p in sn.Pos) { cmpHi = double.IsNaN(cmpHi) ? p.Open : Math.Max(cmpHi, p.Open); cmpLo = double.IsNaN(cmpLo) ? p.Open : Math.Min(cmpLo, p.Open); }
            Func<int, Mt5Position> oldest = dd => sn.Pos.Where(p => p.Dir == dd).OrderBy(p => p.Time).ThenBy(p => p.Ticket).FirstOrDefault();
            if (cmpOwe >= 0.005)
            {   // the other half of the pair: the same lots on the other side (the net is back to what it was)
                var q = oldest(cmpOweDir); if (q == null) { cmpOwe = 0; return false; }
                double cl = Math.Round(Math.Min(q.Lots, cmpOwe), 2); if (CompressClose(sn, it, q, cl, "")) { cmpOwe = Math.Round(cmpOwe - cl, 2); return true; } return false;
            }
            string why = CompressNow(sn); if (why == "") return false;
            double bl = sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots), sl = sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots), m = CompressLots(bl, sl);
            var b = oldest(1); var s = oldest(-1); var first = b.Time < s.Time || (b.Time == s.Time && b.Ticket < s.Ticket) ? b : s;
            double lots = Math.Round(Math.Min(first.Lots, m), 2);
            if (Set.CompressMode == "ALWAYS") lots = Math.Max(0.01, Math.Min(m, Math.Round(Math.Min(lots, (bl + sl - 2 * Math.Abs(bl - sl) - Set.EntryLots) / 2 + 0.0099), 2)));
            if (!CompressClose(sn, it, first, lots, why)) return false;
            cmpOwe = lots; cmpOweDir = -first.Dir; return true;
        }
        bool CompressClose(Snap sn, AutoIteration it, Mt5Position p, double lots, string why)
        {
            double pl = (p.Profit + p.Swap + p.Commission) * lots / Math.Max(0.01, p.Lots), grossBefore = Gross(sn.Pos);
            Refused = ""; var id = Ask("CLOSE", new object[] { p.Ticket, lots >= p.Lots - 1e-9 ? 0.0 : lots }, "CLOSE " + lots.ToString("0.##", IC) + " of #" + p.Ticket + " " + p.Side + " " + Set.Symbol + " (🤖 #" + it.N + " 🧹 compress)");
            if (id == null) { if (!sugg) Note("⚠ compress not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return false; }
            if (why != "") { Compressions++; Tell((Set.CompressMode == "ALWAYS" ? "🧹 COMPRESS. WHY: " : "🧹 COMPRESS BEFORE THE ROLLOVER. WHY: ") + why + ". The realised part stays in this iteration (open + closed = " + M(BasketTotal) + " total, the TP / breakeven / rescue count the same total), the market exposure is the same."); }
            pendId = id; pendKind = "SCALE"; pendTicket = p.Ticket; pendGross = grossBefore; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = lots, Price = p.Dir > 0 ? sn.Bid : sn.Ask, Level = double.NaN, Why = "🧹 COMPRESS #" + p.Ticket + " " + p.Side + " (" + M(pl) + " realised, kept in the iteration)", Basket = Basket });
            return true;
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
            {   // 09d (#41): the result = the iteration's OWN deals (profit + commission + swap) — never a balance difference; a deposit, SET BALANCE,
                // AUTO RESTART or an account swap is not trading P/L
                var own = IterationResult(sn.Deals, Set.Symbol, it.Start, DateTime.MaxValue, sn.Pos);
                double jump = sn.Deals.Where(d => d.Type == "BALANCE" && d.Time >= it.Start.AddSeconds(-1)).Sum(d => d.Profit);
                if (!double.IsNaN(own)) { if (Math.Abs(own - it.Result) > 0.5) Note("🧮 iteration #" + it.N + ": result from its deals " + M(own) + " (the balance moved " + M(sn.Balance - it.BalanceStart) + (Math.Abs(jump) > 0.005 ? ", of which " + M(jump) + " deposits / balance changes" : "") + " — not counted)"); it.Result = Math.Round(own, 2); }
                else if (Math.Abs(jump) > 0.005) it.Result = Math.Round(it.Result - jump, 2);
            }
            if (it.Reason == "") it.Reason = "CLOSED";
            if (Math.Abs(it.AdoptPl) >= 0.01) { it.Result = Math.Round(it.Result - it.AdoptPl, 2); it.Reason += " (taken over at " + (it.AdoptPl < 0 ? "-$" : "+$") + Math.Abs(it.AdoptPl).ToString("0.00", IC) + " open - not counted)"; }   // 09d: #41's "+$204 in 4 s"
            if (sn.PendingOrders > 0)
            {   // the user's request: when an iteration ends, the pending limit / stop orders placed for it (by hand too) are cancelled and leave the chart
                Refused = ""; Ask("CANCELALL", new object[] { Set.Symbol }, "CANCEL the " + sn.PendingOrders + " pending order" + (sn.PendingOrders == 1 ? "" : "s") + " on " + Set.Symbol + " (🤖 #" + it.N + " closed)");
                Tell("🧹 the iteration is closed → its " + sn.PendingOrders + " pending order" + (sn.PendingOrders == 1 ? "" : "s") + " on " + Set.Symbol + " (limits / stops, yours included) cancelled.");
            }
            Tell("CLOSED — iteration #" + it.N + " " + it.Reason + " " + M(it.Result) + " (balance $" + it.BalanceStart.ToString("N2", IC) + " → $" + sn.Balance.ToString("N2", IC) + "), " + it.Buys + " buys / " + it.Sells + " sells, " + it.AddOns + " add-ons, most lots open " + it.MaxGross.ToString("0.##", IC) + ", worst drawdown " + M(it.MaxDd) + ", " + (it.End - it.Start).ToString(@"hh\:mm\:ss", IC) + ".");
            Note((it.Result >= 0 ? "✔" : "✖") + " iteration #" + it.N + " " + it.Reason + " " + it.Result.ToString("+0.00;−0.00", IC) + " • " + it.Buys + " buys / " + it.Sells + " sells • " + it.AddOns + " add-ons • max drawdown " + it.MaxDd.ToString("0.00", IC));
            int dir = it.BuyLots > it.SellLots + 1e-9 ? 1 : it.SellLots > it.BuyLots + 1e-9 ? -1 : lastDir; lastDir = dir;
            if (it.Reason == "TARGET" && Math.Abs(it.BuyLots - it.SellLots) > 1e-9) { it.AfterMax = it.Result; it.AfterDone = false; }   // follow it after the target
            BeLockPx = double.NaN; trimCount = 0; cmpIter = -1; cmpHi = double.NaN; cmpLo = double.NaN; cmpOwe = 0; RestoreSetupRisk(); Current = null; Danger = 0; DangerPeak = 0; DangerPeakWhy = ""; DangerWhy = ""; Similar = ""; HedgeLevel = double.NaN; quickBest = double.NaN; scaleRef = double.NaN; NextHedgeLots = double.NaN; ManualHedge = double.NaN; ManualNoHedge = false; guardQuiet = DateTime.MinValue; guardHeld = ""; Locked = false; lockFrom = 0; Version++; if (OnOpenState != null) OnOpenState("");   // closed: nothing to carry over
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
        // 📈 DRAWDOWN LEAD (user's request: "0.05 lead is too small to get out of −$50 … −50 to −100 → 0.1, −100 to −200 → 0.15"): the fixed lead
        // grows by DdLeadAdd for every DdLeadStep $ of the basket's drawdown (open + banked), at most DdLeadMax — a deeper basket gets back faster
        double DdLead(Snap sn)
        {
            double lead = Math.Max(0.01, Set.NetAdvantage); if (Set.DdLeadStep <= 0 || Set.DdLeadAdd <= 0 || Current == null) return lead;
            double dd = Math.Max(0, -(Tot(sn, Current))); int steps = (int)Math.Floor(dd / Set.DdLeadStep);
            double l2 = lead + steps * Set.DdLeadAdd; if (Set.DdLeadMax > 0) l2 = Math.Min(l2, Set.DdLeadMax); return Math.Round(Math.Max(lead, l2), 2);
        }
        // ---- 🛡 PROTECT THE ADD (09h): the engine's path while it runs (Ask → AutoSend), ProtectStep (the tab) while it is stopped
        public readonly AddProtector Prot = new AddProtector(); public string ProtText = ""; public Func<string, string, double, string, string> ProtectSend; bool protWarned;
        bool ProtectEngine(IBroker b, Snap sn, AutoIteration it)
        {
            if (!AddProtector.On(Set.ProtectAdd, b.IsSim) || double.IsNaN(sn.Bid)) { ProtText = ""; return false; }
            double ask = double.IsNaN(sn.Ask) ? sn.Bid : sn.Ask;
            var a = Prot.Tick(sn.Pos, sn.Bid, ask, Now, sn.M1, Tot(sn, it), sn.PerPrice, Set.CommissionPerLot, Set.ProtectMode, Set.ProtectK, Set.ProtectBigX, Set.ProtectCool);
            ProtText = Prot.Text(Now); if (a == null) return false;
            var before = pendId; Tell(a.Why + (a.Kind == "LOCK" ? " — the loss stops growing (the 🎯 exit guard still watches the total); a " + Set.ProtectCool + "-minute cool-down follows, then restart " + Prot.EntryLots.ToString("0.##", IC) + "." : "."));
            Order(sn, a.Dir, a.Lots, (a.Kind == "LOCK" ? "🛡 PROTECT LOCK " : "🛡 RESUME SMALL ") + (a.Dir > 0 ? "BUY" : "SELL"), a.Dir > 0 ? ask : sn.Bid);
            if (pendId != null && pendId != before) { Prot.Sent(a, Now, Tot(sn, it)); ProtText = Prot.Text(Now); }
            return true;
        }
        // the automation stopped (⏸ STOP ON MANUAL / never started): the tab calls this on every tick, like the 🎯 exit guard — it sends through ProtectSend
        // (the tab's AutoSend: MT5 CanTrade + ARM + PROP limits; on an MT5 account only when PROTECT THE ADD is ticked ON)
        public void ProtectStep(IBroker b, AutoSettings s)
        {
            if (b == null || s == null || (Running && !HandStopped)) return;
            if (!AddProtector.On(s.ProtectAdd, b.IsSim)) { ProtText = ""; return; }
            string sym = s.Symbol; List<Mt5Position> pos; DateTime now; double per = 0, bid = double.NaN, ask = double.NaN; var m1 = new List<Mt5Bar>();
            lock (b.State.Gate)
            {
                var st = b.State; pos = st.Positions.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now;
                Mt5Symbol ms; if (st.Symbols.TryGetValue(sym, out ms) && ms.TickSize > 0) per = ms.TickValue / ms.TickSize; Mt5Quote q; if (st.Quotes.TryGetValue(sym, out q)) { bid = q.Bid; ask = q.Ask; }
                var se = st.SeriesOf(sym, 1); int from = Math.Max(0, se.Bars.Count - 220); for (int i = from; i < se.Bars.Count; i++) if (se.Bars[i].Time.AddMinutes(1) <= now) m1.Add(se.Bars[i]);
            }
            if (double.IsNaN(bid)) return; if (double.IsNaN(ask)) ask = bid;
            double total = !double.IsNaN(GuardTotal) && pos.Count > 0 ? GuardTotal : OpenPl(pos, sym);
            var a = Prot.Tick(pos, bid, ask, now, m1, total, per, s.CommissionPerLot, s.ProtectMode, s.ProtectK, s.ProtectBigX, s.ProtectCool);
            ProtText = Prot.Text(now); if (a == null) { protWarned = false; return; }
            var id = ProtectSend != null ? ProtectSend(sym, a.Dir > 0 ? "BUY" : "SELL", a.Lots, a.Why) : null;
            if (id == null) { if (!protWarned) { protWarned = true; Note("⚠ " + a.Why + " — but NOT sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + ". Act yourself!"); if (OnGuard != null) try { OnGuard("⚠ 🛡 PROTECT NOT SENT — " + a.Why); } catch { } } return; }
            Prot.Sent(a, now, total); ProtText = Prot.Text(now); var line = a.Why + (a.Kind == "LOCK" ? " (the automation is stopped: the protection still acts) — cool-down " + s.ProtectCool + " min, then restart " + Prot.EntryLots.ToString("0.##", IC) : "");
            if (Current != null && !Current.Done) TellNow(line); else Note(line); Version++;
            if (OnGuard != null) try { OnGuard(line); } catch { }
        }
        // ---- ➕ ADD ON BREAK (09h): the add's stop first, then at most AobMax adds a basket — one action per step through Ask → AutoSend
        public string AobText = ""; public int AobAddsTotal, AobStops; public AddBreakTicket AobLast;
        int aobIter = -1, aobAdds; long aobTicket; double aobStop = double.NaN, aobPendStop = double.NaN, aobSwing = double.NaN;
        bool AobStep(Snap sn, AutoIteration it)
        {
            if (pendId != null || sn.Pos.Count == 0 || double.IsNaN(sn.Bid) || sn.PerPrice <= 0) return false;
            if (aobIter != it.N) { aobIter = it.N; aobAdds = 0; aobTicket = 0; aobStop = double.NaN; aobSwing = double.NaN; AobText = ""; }
            if (aobTicket != 0)
            {
                var a = sn.Pos.FirstOrDefault(p => p.Ticket == aobTicket);
                if (a == null) aobTicket = 0;
                else if (!double.IsNaN(aobStop) && (a.Dir > 0 ? sn.Bid <= aobStop : sn.Ask >= aobStop))
                {   // the add's protective stop: only the add closes, the basket goes on (its result stays in the total)
                    double pl = a.Profit + a.Swap + a.Commission, grossBefore = Gross(sn.Pos); Refused = "";
                    var id = Ask("CLOSE", new object[] { a.Ticket, 0.0 }, "CLOSE #" + a.Ticket + " " + a.Side + " " + a.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + it.N + " ➕ the add's stop)");
                    if (id == null) { if (!sugg) Note("⚠ not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return false; }
                    AobText = "➕ STOP — the add " + a.Side + " " + a.Lots.ToString("0.##", IC) + " closed at its stop " + P(aobStop) + " (" + M(pl) + "), the basket goes on";
                    Tell(AobText + ". WHY: the " + (a.Dir > 0 ? "bid " + P(sn.Bid) + " ≤ " : "ask " + P(sn.Ask) + " ≥ ") + P(aobStop) + " (the add failed); its loss stays in the iteration's total.");
                    pendId = id; pendKind = "SCALE"; pendTicket = a.Ticket; pendGross = grossBefore; pendSent = DateTime.Now; pendOk = DateTime.MinValue; aobTicket = 0; AobStops++;
                    Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = a.Lots, Price = a.Dir > 0 ? sn.Bid : sn.Ask, Level = aobStop, Why = "➕ ADD STOP #" + a.Ticket + " " + a.Side + " (" + M(pl) + " realised, kept in the iteration)", Basket = Basket });
                    return true;
                }
            }
            if (Trend == null || sn.M1.Count == 0 || Trend.Bar != sn.M1[sn.M1.Count - 1].Time) return false;   // only on the newest finished M1 candle
            var t = TrendDetect.AddOnBreak(sn.Pos, Tot(sn, it), Set.Target, sn.Bid, sn.Ask, sn.PerPrice, Set.CommissionPerLot, HedgeLevel, Trend, Set.AobMin, Set.AobMult, Set.AobStop, aobAdds, Set.AobMax, aobSwing, sn.Sym != null && sn.Sym.VolStep > 0 ? sn.Sym.VolStep : 0.01);
            AobLast = t; if (!t.Ok) { AobText = t.Text; return false; }
            aobSwing = t.Swing; aobPendStop = t.Stop; AobText = t.Text; Tell(t.Text);
            Order(sn, t.Dir, t.Lots, "➕ ADD ON BREAK " + (aobAdds + 1) + "/" + Set.AobMax + " (stop " + P(t.Stop) + ", exit " + P(t.ExitPx) + ")", t.Dir > 0 ? sn.Ask : sn.Bid); return true;
        }
        // ---- 🚀 TREND STACK (09e): the trend, the stacks, the flip and the bank — one action per step, through Ask → AutoSend
        public TrendState Trend; public string StackText = ""; public int Stacks; public int StackFlips, StackBanks, StacksTotal;
        readonly List<long> stackTickets = new List<long>(); int stackDir, stackIter = -1; double stackLevel = double.NaN; DateTime trendBar = DateTime.MinValue;
        bool TrendStackStep(Snap sn, AutoIteration it)
        {
            if (pendId != null || sn.Pos.Count == 0 || double.IsNaN(sn.Bid) || sn.PerPrice <= 0) return false;
            if (stackIter != it.N) { stackIter = it.N; Stacks = 0; stackTickets.Clear(); stackDir = 0; stackLevel = double.NaN; }
            stackTickets.RemoveAll(tk => !sn.Pos.Any(p => p.Ticket == tk)); if (stackTickets.Count == 0) stackDir = 0;
            var tr = Trend; if (tr == null) return false;
            double net = sn.Pos.Sum(p => p.Dir * p.Lots), total = Tot(sn, it), bal = Math.Max(1, sn.Balance), cap = Math.Max(0.01, Math.Round(bal / 1000 * Set.StackCapPer1k, 2));
            double step = sn.Sym != null && sn.Sym.VolStep > 0 ? sn.Sym.VolStep : 0.01;
            // 1. FLIP: the trend turned against the stacks (an opposite trend or an EMA 9/21 cross) → close the stacked part at once
            if (stackDir != 0 && stackTickets.Count > 0 && ((tr.Dir == -stackDir) || (stackDir > 0 ? tr.CrossDown : tr.CrossUp)))
            {
                if (sn.Pos.All(x => stackTickets.Contains(x.Ticket)))
                {   // only stacks are left: they ARE the basket → the iteration closes here (its total is what it is)
                    it.Reason = "🚀 FLIP"; StackFlips++; Tell("🚀 FLIP — " + tr.Text + " → only the stacks were left: the basket closes at " + M(total) + " so a failed stack cannot run."); CloseAll(sn, "🚀 FLIP", false); return true;
                }
                var p = sn.Pos.Where(x => stackTickets.Contains(x.Ticket)).OrderByDescending(x => x.Lots).First();
                StackText = "🚀 FLIP — " + tr.Text + " → the stack " + p.Side + " " + p.Lots.ToString("0.##", IC) + " closes (" + M(p.Profit + p.Swap + p.Commission) + ")";
                if (StackClose(sn, it, p, "🚀 FLIP: the trend turned (" + tr.Text + ") → stack #" + p.Ticket + " " + p.Side + " " + p.Lots.ToString("0.##", IC) + " closed so a failed stack cannot run")) { StackFlips++; return true; }   /* the stacks used stay counted: at most StackMax per basket */
                return false;
            }
            // 2. BANK: a strong trend → the profitable legs of the other side close (they count in the total), the stacks' side keeps the lead
            if (tr.Dir != 0 && tr.Strength >= Set.BankAt)
            {
                var g = sn.Pos.Where(x => x.Dir == -tr.Dir && x.Profit + x.Swap + x.Commission > 0.5).OrderByDescending(x => x.Profit + x.Swap + x.Commission).FirstOrDefault();
                if (g != null && Math.Abs(net + g.Dir * -1 * g.Lots) <= cap + 1e-9 && sn.Pos.Any(x => x.Dir == tr.Dir))   /* a leg on the trend's side stays open: banking never empties the basket */
                {
                    StackText = "🏦 BANK — " + tr.Text + " → the " + g.Side + " " + g.Lots.ToString("0.##", IC) + " in profit closes (" + M(g.Profit + g.Swap + g.Commission) + ", kept in the total)";
                    if (StackClose(sn, it, g, "🏦 BANK: " + tr.Text + " → " + g.Side + " " + g.Lots.ToString("0.##", IC) + " #" + g.Ticket + " in profit closed (" + M(g.Profit + g.Swap + g.Commission) + "), counted in the total")) { StackBanks++; return true; }
                }
            }
            // 3. STACK: the trend agrees, a NEW swing break in its way (a confirmed continuation), room under the caps
            if (tr.Dir == 0 || tr.Strength < Set.StackMin) { StackText = tr.Text + " — no stack (needs a trend ≥ " + Set.StackMin + ")"; return false; }
            if (Stacks >= Set.StackMax) { StackText = tr.Text + " — " + Stacks + "/" + Set.StackMax + " stacks used"; return false; }
            double lvl = tr.Dir > 0 ? tr.SwingHi : tr.SwingLo; double px = tr.Dir > 0 ? sn.Ask : sn.Bid;
            if (double.IsNaN(lvl) || !(tr.Dir > 0 ? tr.Close > lvl : tr.Close < lvl) || (!double.IsNaN(stackLevel) && Math.Abs(lvl - stackLevel) < 1e-9)) { StackText = tr.Text + " — waits for a new swing break " + (tr.Dir > 0 ? "up" : "down"); return false; }
            if (sn.MarginLevel > 0 && sn.MarginLevel < Set.StackMargin) { StackText = tr.Text + " — no stack: margin " + sn.MarginLevel.ToString("0", IC) + " % < " + Set.StackMargin.ToString("0", IC) + " %"; return false; }
            double move = Set.StackK * (double.IsNaN(tr.Atr5) ? 0 : tr.Atr5), withTrend = net * tr.Dir; string why;
            double lots = TrendDetect.StackLots(total, Set.Target, move, sn.PerPrice, withTrend, step, cap, out why);
            if (lots < step - 1e-9) { StackText = tr.Text + " — no stack: " + why; stackLevel = lvl; return false; }
            double after = withTrend + lots;
            string lab = "🚀 STACK " + (Stacks + 1) + "/" + Set.StackMax + ": " + (tr.Dir > 0 ? "BUY " : "SELL ") + lots.ToString("0.##", IC) + " — trend " + (tr.Dir > 0 ? "UP " : "DOWN ") + tr.Strength + ", needs " + M(Set.Target - total) + " to target, risk −$" + (after * sn.PerPrice).ToString("0", IC) + "/$1, cap " + cap.ToString("0.##", IC) + " net";
            StackText = lab; stackLevel = lvl; Stacks++; StacksTotal++;
            Tell(lab + ". WHY: " + tr.Text + "; the close " + P(tr.Close) + " broke the swing " + P(lvl) + "; expected move " + StackK(move) + " (" + Set.StackK.ToString("0.#", IC) + " × ATR M5).");
            Order(sn, tr.Dir, lots, lab, px); return true;
        }
        string StackK(double move) { return "$" + move.ToString("0.00", IC); }
        bool StackClose(Snap sn, AutoIteration it, Mt5Position p, string why)
        {
            double pl = p.Profit + p.Swap + p.Commission, grossBefore = Gross(sn.Pos);
            Refused = ""; var id = Ask("CLOSE", new object[] { p.Ticket, 0.0 }, "CLOSE #" + p.Ticket + " " + p.Side + " " + p.Lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + it.N + " " + (why.StartsWith("🏦") ? "🏦 bank" : "🚀 flip") + ")");
            if (id == null) { if (!sugg) Note("⚠ not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return false; }
            Tell(why + ".");
            pendId = id; pendKind = "SCALE"; pendTicket = p.Ticket; pendGross = grossBefore; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = "CLOSE", Lots = p.Lots, Price = p.Dir > 0 ? sn.Bid : sn.Ask, Level = double.NaN, Why = (why.StartsWith("🏦") ? "🏦 BANK #" : "🚀 FLIP #") + p.Ticket + " " + p.Side + " (" + M(pl) + " realised, kept in the iteration)", Basket = Basket });
            return true;
        }
        // ---- 🎯 TOTAL EXIT GUARD (09f): runs on every tick, the automation running or not; never opens anything
        public Func<string, string> GuardClose;   // the tab's close-only path (CLOSEALL through the armed link; null = not armed / not allowed)
        public string GuardText = "", GuardExit = ""; public bool GuardAlert; public double GuardLevel = double.NaN, GuardTotal = double.NaN, GuardBePx = double.NaN, GuardOpen; public event Action<string> OnGuard;
        double guardWorst; DateTime guardFrom = DateTime.MinValue, guardClosedAt = DateTime.MinValue; string guardKey = ""; bool guardWarned;
        // which exit: (kind, the $ level of the total, why) — BREAKEVEN once the basket is in trouble, else the TARGET
        public static Tuple<string, double, string> ExitLevel(double target, double keep, double worst, double minutesOpen, bool manualHedge, double troubleX, double troubleMin)
        {
            var why = new List<string>();
            if (target > 0 && worst <= -troubleX * target) why.Add("the basket was " + (worst < 0 ? "−$" : "$") + Math.Abs(worst).ToString("0", IC));
            if (minutesOpen >= troubleMin) why.Add("open " + (minutesOpen >= 120 ? (minutesOpen / 60).ToString("0.#", IC) + " h" : minutesOpen.ToString("0", IC) + " min"));
            if (manualHedge) why.Add("a manual trade was added");
            return why.Count > 0 || target <= 0 ? Tuple.Create("BREAKEVEN", keep, string.Join(", ", why)) : Tuple.Create("TARGET", target, "a fresh basket");
        }
        // ✋ 09h (user's DEMO #47): while the automation is STOPPED with an iteration open, every leg you add is counted (BUYS / SELLS / MAX LOTS, ✋ n manual legs, a STORY
        // line with its source), and when the basket goes flat — by hand, by the 🎯 guard or the broker's TP / SL — the iteration CLOSES with ALL its deals since its start
        readonly HashSet<long> engineSeen = new HashSet<long>(); HashSet<long> stopKnown;
        public void StoppedTrack(IBroker b, AutoSettings s)
        {
            var it = Current; if (b == null || Running || it == null || it.Done || Set == null) return;
            string sym = Set.Symbol; List<Mt5Position> pos; List<Mt5Deal> deals; DateTime now;
            lock (b.State.Gate)
            {
                var st = b.State; pos = st.Positions.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now;
                deals = st.Deals.Where(d => string.Equals(d.Symbol, sym, StringComparison.OrdinalIgnoreCase) && d.Time >= it.Start.AddSeconds(-1)).ToList();
            }
            if (stopKnown == null) { stopKnown = new HashSet<long>(engineSeen); if (stopKnown.Count == 0) foreach (var p in pos) stopKnown.Add(p.Ticket); }
            foreach (var p in pos.OrderBy(x => x.Time).ThenBy(x => x.Ticket)) if (stopKnown.Add(p.Ticket))
            {
                var din = deals.FirstOrDefault(d => d.Position == p.Ticket && d.Entry == "IN");
                string src = din == null ? (p.Magic == ManualTracker.SpasixMagic ? "SpasiX" : "MT5") : IsHandDeal(din) ? (din.Reason == "MOBILE" ? "MT5 mobile" : din.Reason == "WEB" ? "MT5 web" : "MT5 (desktop)") : din.Magic == ManualTracker.SpasixMagic || p.Magic == ManualTracker.SpasixMagic ? "SpasiX" : "MT5 " + (din.Reason != "" ? din.Reason.ToLowerInvariant() : "");
                it.ManualAdds++; it.ManualLots = it.ManualLots + p.Lots; it.Manual = true; if (p.Dir > 0) { it.Buys++; it.BuyLots += p.Lots; } else { it.Sells++; it.SellLots += p.Lots; }
                if (!it.Ctx.Contains("manual=yes")) it.Ctx += ";manual=yes";
                Trades.Add(new AutoTrade { Iter = it.N, Time = p.Time > DateTime.MinValue ? p.Time : now, Side = p.Side, Lots = p.Lots, Price = p.Open, Level = double.NaN, Why = "✋ MANUAL LEG " + p.Side + " (" + src + ")", Basket = OpenPl(pos, sym) });
                TellNow("✋ MANUAL LEG " + p.Side + " " + p.Lots.ToString("0.##", IC) + " @ " + P(p.Open) + " — " + src + ", " + (p.Time > DateTime.MinValue ? p.Time : now).ToString("HH:mm:ss", IC) + " (the automation is stopped: counted in iteration #" + it.N + ")"); Version++;
            }
            it.MaxGross = Math.Max(it.MaxGross, Gross(pos));
            if (pos.Count == 0) CloseStopped(it, deals, sym, now, guardClosedAt > DateTime.MinValue ? "🎯 EXIT GUARD " + GuardExit : "");
        }
        // the stopped iteration's end: its result = every deal of the symbol since its start (automation + yours + the broker's), commissions / swap included
        void CloseStopped(AutoIteration it, List<Mt5Deal> deals, string sym, DateTime now, string how)
        {
            if (it == null || it.Done) return;
            var r = IterationResult(deals ?? new List<Mt5Deal>(), sym, it.Start, DateTime.MaxValue, new List<Mt5Position>());
            var outs = (deals ?? new List<Mt5Deal>()).Where(d => d.Entry == "OUT" && d.Time >= it.Start).OrderBy(d => d.Time).ToList(); var lastOut = outs.LastOrDefault();
            if (how == "") how = lastOut == null ? "✋ CLOSED BY HAND" : lastOut.Reason == "TP" ? "broker TP" : lastOut.Reason == "SL" ? "broker SL" : lastOut.Reason == "SO" || lastOut.Reason == "STOPOUT" ? "STOP OUT" : "✋ CLOSED BY HAND";
            it.End = now; if (!double.IsNaN(r)) it.Result = Math.Round(r, 2); it.Reason = how + (double.IsNaN(r) ? " (unverified)" : ""); if (Current == it) Current = null; Phase = "OFF"; guardClosedAt = DateTime.MinValue;
            TellNow("■ ITERATION #" + it.N + " CLOSED — " + how + " · result " + (double.IsNaN(r) ? "not verifiable (no closing deal in the history)" : M(it.Result)) + " (all its deals since " + it.Start.ToString("dd MMM HH:mm:ss", IC) + ": the automation's, yours and the broker's)" + (it.ManualAdds > 0 ? " · ✋ " + it.ManualAdds + " manual leg" + (it.ManualAdds == 1 ? "" : "s") + " (" + it.ManualLots.ToString("0.##", IC) + " lots)" : "")); Version++;
        }
        public void GuardStep(IBroker b, AutoSettings s)
        {
            try { StoppedTrack(b, s); } catch { }
            if (b == null || s == null || !s.ExitGuard) { GuardText = ""; GuardAlert = false; return; }
            if (Running && pendId != null) return;   // the automation is in the middle of an order / close: wait
            string sym = Running && Set != null ? Set.Symbol : s.Symbol; List<Mt5Position> pos; List<Mt5Deal> deals; DateTime now; double per = 0, bid = double.NaN, ask = double.NaN;
            lock (b.State.Gate)
            {
                var st = b.State; pos = st.Positions.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now;
                Mt5Symbol ms; if (st.Symbols.TryGetValue(sym, out ms) && ms.TickSize > 0) per = ms.TickValue / ms.TickSize; Mt5Quote q; if (st.Quotes.TryGetValue(sym, out q)) { bid = q.Bid; ask = q.Ask; }
                deals = pos.Count > 0 || Current != null ? st.Deals.Where(d => string.Equals(d.Symbol, sym, StringComparison.OrdinalIgnoreCase) && d.Time >= (Current != null && !Current.Done ? Current.Start : DateTime.MinValue).AddSeconds(-1)).ToList() : new List<Mt5Deal>();
            }
            var it = Current != null && !Current.Done ? Current : null;
            if (pos.Count == 0)
            {   // flat: an iteration the guard closed while the automation was not running is recorded here
                if (it != null && !Running && guardClosedAt > DateTime.MinValue) { it.End = now; var r = IterationResult(deals, sym, it.Start, DateTime.MaxValue, pos); if (!double.IsNaN(r)) it.Result = Math.Round(r, 2); it.Reason = "🎯 EXIT GUARD " + GuardExit; Current = null; Phase = "OFF"; Version++; }
                guardKey = ""; guardClosedAt = DateTime.MinValue; GuardText = ""; GuardAlert = false; GuardTotal = double.NaN; return;
            }
            if (it == null && !s.GuardManual) { GuardText = ""; return; }
            DateTime from = it != null ? it.Start : pos.Min(p => p.Time); string key = (it != null ? "#" + it.N : "M") + "|" + from.Ticks;
            if (key != guardKey) { guardKey = key; guardWorst = it != null ? Math.Min(0, it.MaxDd) : 0; guardFrom = from; guardWarned = false; GuardAlert = false; }
            double total = OpenPl(pos, sym) + ClosedSince(deals.Where(d => d.Time >= from.AddSeconds(-1)), pos, sym, from); guardWorst = Math.Min(guardWorst, total); GuardTotal = total; GuardOpen = OpenPl(pos, sym);
            bool manualHedge = it != null ? (it.ManualAdds > 0 || HandStopped || it.Manual) : (pos.Any(p => p.Dir > 0) && pos.Any(p => p.Dir < 0));
            double target = Running && Set != null ? Set.Target : s.Target;
            var ex = ExitLevel(target, s.GuardKeep, guardWorst, (now - from).TotalMinutes, manualHedge, s.GuardTroubleX, s.GuardMinutes); GuardExit = ex.Item1; GuardLevel = ex.Item2;
            double net = pos.Sum(p => p.Dir * p.Lots), px = net > 0 ? bid : ask; GuardBePx = PriceForTotal(total, ex.Item2, net, per, px);
            GuardText = "🎯 EXIT AT " + ex.Item1 + " " + (ex.Item1 == "TARGET" ? "+$" + target.ToString("0.##", IC) : "(+$" + ex.Item2.ToString("0.##", IC) + ")") + (double.IsNaN(GuardBePx) ? "" : " " + GuardBePx.ToString("0.00", IC) + " (" + (GuardBePx - px).ToString("+0.0;−0.0", IC) + ")") + " — " + ex.Item3 + " · total now " + M(total);
            if (total < ex.Item2) { if (GuardAlert && total < ex.Item2 - Math.Max(1, 0.25 * Math.Abs(target))) GuardAlert = false; return; }
            if (guardClosedAt > DateTime.MinValue && (DateTime.Now - guardClosedAt).TotalSeconds < 10) return;   // a close is on its way
            string what = "the TOTAL " + M(total) + " (open " + M(OpenPl(pos, sym)) + " + closed " + M(total - OpenPl(pos, sym)) + ") reached the " + ex.Item1 + " level " + M(ex.Item2) + " (" + ex.Item3 + ")";
            if (ManualManage)
            {   // ✋ MANUAL MANAGE: never closes by itself — a loud alert with a one-click CLOSE ALL
                if (!GuardAlert) { GuardAlert = true; Note("🎯 EXIT GUARD ALERT (✋ MANUAL MANAGE): " + what + " → press ⛔ CLOSE ALL NOW"); if (OnGuard != null) try { OnGuard("🎯 " + ex.Item1 + " REACHED — " + what + ". ✋ MANUAL MANAGE: press ⛔ CLOSE ALL NOW"); } catch { } Version++; }
                return;
            }
            if (Running && it != null && !HandStopped && Set != null)
            {   // the automation runs: its own close path finishes the iteration
                var sn = Read(b); it.Reason = "🎯 EXIT GUARD " + ex.Item1; guardClosedAt = DateTime.Now; Tell("🎯 TOTAL EXIT GUARD — " + what + " → CLOSE ALL."); CloseAll(sn, "🎯 EXIT GUARD " + ex.Item1, false);
                if (OnGuard != null) try { OnGuard("🎯 CLOSED AT " + ex.Item1 + " — " + what); } catch { } return;
            }
            var id = GuardClose != null ? GuardClose(sym) : null;
            if (id == null) { if (!guardWarned) { guardWarned = true; Note("⚠ 🎯 EXIT GUARD: " + what + " — but nothing could be closed (trading is not ARMED / allowed). Close it yourself!"); if (OnGuard != null) try { OnGuard("⚠ 🎯 " + ex.Item1 + " REACHED but NOT closed — trading is not armed. " + what); } catch { } } return; }
            guardClosedAt = DateTime.Now; var line = "🎯 TOTAL EXIT GUARD — " + what + " → CLOSE ALL " + sym + " (" + pos.Count + " position" + (pos.Count == 1 ? "" : "s") + ")" + (HandStopped ? " — the automation stays stopped" : "");
            if (it != null) TellNow(line); else Note(line); Version++;
            if (OnGuard != null) try { OnGuard("🎯 CLOSED AT " + ex.Item1 + " — final total ≈ " + M(total) + " (" + pos.Count + " positions on " + sym + ")"); } catch { }
        }
        double HedgeLots(Snap sn)
        {
            if (Set.HedgeSize == "SAME") return Math.Max(0.01, Math.Round(Set.EntryLots, 2));   // 🔒 SAME SIZE: every hedge = the entry lots
            if (Set.HedgeSize == "ADVANTAGE") { double nnet = Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)); return Math.Max(0.01, Math.Round(nnet + DdLead(sn), 2)); }
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
                : Set.EntryMode == "ENG123" ? "1-2-3 ENGULFING on " + tf + ": 2 candles of one colour, the 3rd engulfs the 2nd candle's body and closes"
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
            var ps = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).ToList(); var last = ps.Last(); lastDir = NetSide(sn.Pos, last.Dir);
            Current = new AutoIteration { N = NextN(), Start = Now, FirstDir = ps[0].Side, BalanceStart = sn.Balance, Adopted = true, Manual = true, Buys = ps.Count(p => p.Dir > 0), Sells = ps.Count(p => p.Dir < 0), BuyLots = ps.Where(p => p.Dir > 0).Sum(p => p.Lots), SellLots = ps.Where(p => p.Dir < 0).Sum(p => p.Lots) };
            closedByUs = false; Current.Ctx = Context(sn, lastDir) + ";manual=yes"; Iterations.Add(Current); Phase = "IN ITERATION"; knownGross = Gross(sn.Pos); Current.MaxGross = knownGross;
            foreach (var p in ps) Trades.Add(new AutoTrade { Iter = Current.N, Time = p.Time > DateTime.MinValue ? p.Time : Now, Side = p.Side, Lots = p.Lots, Price = p.Open, Level = double.NaN, Why = "✋ MANUAL " + p.Side, Basket = BasketOf(sn) });
            SetHedgeLevel(sn, lastDir);
            Tell("✋ MANUAL TRADE — iteration #" + Current.N + ": " + string.Join(", ", ps.Select(p => p.Side + " " + p.Lots.ToString("0.##", IC) + " @ " + P(p.Open))) + " opened by hand. The automation now manages it: " + Plan(sn) + (Set.CloseAtNy != "" ? " Closes at " + Set.CloseAtNy + " New York if still open." : ""));
        }
        // ✋ positions added or closed by hand during an iteration: recorded, the add-on level follows the newest leg
        // 💰 SIZE FROM THE LIVE ACCOUNT (user's request): at every iteration start, lots = SizePer1k per $1,000 of the balance (the equity when a basket
        // of another symbol is open), rounded DOWN to the lot step, never under the minimum (then a warning: the minimum is more than the tested risk);
        // the target = lots × TargetPerLot ($2 per 0.01 lot in DEFENDER)
        // nearest = to the NEAREST lot step (🦅 RUNNER: 0.048 → 0.05 at $8,000) instead of down
        public static Tuple<double, double, string> SizeFor(double balance, double equity, bool otherOpen, Mt5Symbol sym, double per1k, double targetPerLot, bool nearest = false)
        {
            double basis = otherOpen ? Math.Min(balance, equity) : balance, step = sym != null && sym.VolStep > 0 ? sym.VolStep : 0.01, min = sym != null && sym.VolMin > 0 ? sym.VolMin : 0.01;
            double want = basis / 1000 * per1k, lots = (nearest ? Math.Round(want / step + 1e-9, MidpointRounding.AwayFromZero) : Math.Floor(want / step + 1e-9)) * step; string warn = "";
            if (lots < min) { lots = min; warn = "⚠ SIZE: " + min.ToString("0.##", IC) + " lot (the broker's minimum) = " + (want > 0 ? (min / want).ToString("0.0", IC) : "∞") + "× the tested risk at $" + basis.ToString("N0", IC) + " (" + per1k.ToString("0.###", IC) + " lot per $1,000)"; }
            lots = Math.Round(lots, 2); return Tuple.Create(lots, Math.Round(lots * targetPerLot, 2), warn);
        }
        bool OtherOpen(Snap sn) { return sn.Equity > 0 && Math.Abs(sn.Equity - sn.Balance) > 0.005; }   // (no position on this symbol) the account still floats = a basket on another symbol
        // the side the basket LEANS (its net lots); flat → the given side (the newest leg's)
        // the chart shows the automation's preview (the "it would enter" chip, the break / hedge lines) only while it runs, can be resumed, or a 🏁 RACE engine is shown
        // 🧮 ITERATION TOTAL IF CLOSED NOW (user's request): the open tickets + what this set already closed (the same function everywhere)
        // the strip's TOTAL tooltip (09h fix: it kept the old basket's numbers): built every refresh from the same total / open / closed as the boxes; null = no basket
        public static string TotalTip(bool basket, double total, double open, double closed, Func<double, string> fmt)
        {
            if (!basket || double.IsNaN(total)) return null;
            return "TOTAL NOW " + fmt(total) + " = open " + fmt(open) + " + closed legs " + fmt(double.IsNaN(closed) ? 0 : closed) + " (this iteration, commissions and swap included)";
        }
        public static double OpenPl(IEnumerable<Mt5Position> pos, string sym) { return pos.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).Sum(p => p.Profit + p.Swap + p.Commission); }
        // the closed part since `from` on the symbol: every OUT deal (profit, swap, commission) + the IN commission of the positions no longer open
        public static double ClosedSince(IEnumerable<Mt5Deal> deals, IEnumerable<Mt5Position> open, string sym, DateTime from)
        {
            var still = new HashSet<long>(open.Select(p => p.Ticket)); double x = 0;
            foreach (var d in deals) if (string.Equals(d.Symbol, sym, StringComparison.OrdinalIgnoreCase) && d.Time >= from && (d.Type == "BUY" || d.Type == "SELL") && (d.Entry == "OUT" || (d.Entry == "IN" && !still.Contains(d.Position)))) x += d.Profit + d.Swap + d.Commission;
            return x;
        }
        // ✔ the legs closed since `from` on the symbol: (the side of the CLOSED position, lots, its open price, the close price, $ with swap + commission)
        public static List<Tuple<string, double, double, double, double>> ClosedLegs(IEnumerable<Mt5Deal> deals, string sym, DateTime from)
        {
            var all = deals.Where(d => string.Equals(d.Symbol, sym, StringComparison.OrdinalIgnoreCase) && (d.Type == "BUY" || d.Type == "SELL")).ToList(); var res = new List<Tuple<string, double, double, double, double>>();
            foreach (var d in all.Where(d => d.Entry == "OUT" && d.Time >= from).OrderBy(d => d.Time))
            {
                var inn = all.FirstOrDefault(x => x.Entry == "IN" && x.Position == d.Position);
                res.Add(Tuple.Create(d.Type == "BUY" ? "SELL" : "BUY", d.Lots, inn != null ? inn.Price : double.NaN, d.Price, d.Profit + d.Swap + d.Commission + (inn != null && inn.Lots > 0 ? inn.Commission * d.Lots / inn.Lots : 0)));
            }
            return res;
        }
        // 🎯 the price where the iteration's TOTAL is $0 / the target with the current net (NaN when flat): px = the closing price now (bid for a long net)
        public static double PriceForTotal(double total, double want, double net, double perPrice, double px) { return Math.Abs(net) < 0.005 || perPrice <= 0 || double.IsNaN(px) ? double.NaN : px + (want - total) / (net * perPrice); }
        // what a close button would do now: "SELL" / "BUY" = that side, "ALL" = everything; total = the iteration's open + closed
        public static string ClosePreview(IEnumerable<Mt5Position> pos, string sym, string which, double total, double target, double perPrice)
        {
            var ps = pos.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); if (ps.Count == 0) return "nothing open on " + sym;
            Func<double, string> M = v => (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("0.00", IC);
            if (which == "ALL") return "close all now: the iteration ends at " + M(total);
            int d = which == "BUY" ? 1 : -1; var side = ps.Where(p => p.Dir == d).ToList(); if (side.Count == 0) return "no " + which.ToLowerInvariant() + "s open";
            double bank = side.Sum(p => p.Profit + p.Swap + p.Commission), netAfter = Math.Round(ps.Sum(p => p.Dir * p.Lots) - d * side.Sum(p => p.Lots), 2);
            string tail = Math.Abs(netAfter) < 0.005 ? ", nothing left open the other way" : ", net then " + (netAfter > 0 ? "LONG " : "SHORT ") + Math.Abs(netAfter).ToString("0.##", IC) + (target > 0 && perPrice > 0 ? (total >= target ? " (the target is already reached)" : " (needs a $" + ((target - total) / (Math.Abs(netAfter) * perPrice)).ToString("0.00", IC) + " move " + (netAfter > 0 ? "UP" : "DOWN") + " to the +$" + target.ToString("0.##", IC) + " target)") : "");
            return "close the " + which.ToLowerInvariant() + "s now: " + M(bank) + " banked → total stays " + M(total) + tail;
        }
        // ● the HEDGE tab's account badge: REAL / MT5 demo / 🎮 DEMO (simulated) / 🏆 PROP / 🧪 BACKTEST + the broker and login
        // 🧮 an iteration's result from its own deals on the symbol between from and to (the OUT deals + the opening commission of the legs it
        // opened and closed); NaN = no closing deal there (nothing to count from — the caller keeps what it has)
        public static double IterationResult(IEnumerable<Mt5Deal> deals, string sym, DateTime from, DateTime to, IEnumerable<Mt5Position> open)
        {
            from = new DateTime(from.Ticks - from.Ticks % TimeSpan.TicksPerSecond);
            var own = deals.Where(d => string.Equals(d.Symbol, sym, StringComparison.OrdinalIgnoreCase) && (d.Type == "BUY" || d.Type == "SELL") && d.Time >= from && d.Time <= to).ToList();
            if (!own.Any(d => d.Entry == "OUT")) return double.NaN;
            return ClosedSince(own, open ?? Enumerable.Empty<Mt5Position>(), sym, from);
        }
        // ⏸ STOP ON MANUAL (09d, user's rule): what you did by hand, in words ("SELL 0.05 @ 4101.97 in MT5 (desktop), 19:53:28")
        public static string HandDeal(Mt5Deal d)
        {
            string where = d.Reason == "MOBILE" ? "MT5 mobile" : d.Reason == "WEB" ? "MT5 web" : "MT5" + (d.Reason == "DESKTOP" ? " (desktop)" : "");
            return (d.Entry == "OUT" ? "CLOSE " + (d.Type == "BUY" ? "SELL" : "BUY") : d.Type) + " " + d.Lots.ToString("0.##", IC) + " @ " + d.Price.ToString("0.#####", IC) + " in " + where + ", " + d.Time.ToString("HH:mm:ss", IC);
        }
        public static bool IsHandDeal(Mt5Deal d) { return d.Magic != ManualTracker.SpasixMagic && (d.Reason == "DESKTOP" || d.Reason == "MOBILE" || d.Reason == "WEB" || d.Reason == "CLIENT") && (d.Type == "BUY" || d.Type == "SELL"); }
        public static string BadgeText(string mode, bool sim, bool paper, string kind, string server, long login)
        {
            string who = ((server ?? "") != "" ? server.Split('-')[0] + " " : "") + (login > 0 ? login.ToString(IC) : "");
            if (sim) return "🧪 BACKTEST · history replay — nothing is real";
            if (paper) return "🎮 DEMO · simulated · " + who;
            if (mode == "PROP") return "🏆 PROP · " + who + (kind != "" ? " · " + kind : "");
            return kind == "REAL" ? "● REAL MONEY · " + who : "● MT5 " + (kind != "" ? kind : "ACCOUNT") + " · " + who;
        }
        // 🔐 armed breakeven: the price meets the BE line before the hedge (long basket: BE above the hedge; short: below)
        public static bool BeComesFirst(int lastDir, double hedge, double be) { return !double.IsNaN(hedge) && !double.IsNaN(be) && (lastDir > 0 ? be > hedge : be < hedge); }
        public static bool ShowPreview(bool running, bool raceLane, bool resumable) { return running || raceLane || resumable; }
        public static int NetSide(IEnumerable<Mt5Position> pos, int flat) { double n = pos.Sum(p => p.Dir * p.Lots); return n > 0.005 ? 1 : n < -0.005 ? -1 : flat; }
        // 🐞 user's catch (#35: all SELLs closed by hand, net LONG 0.40, yet the next hedge was a BUY above): after ANY close (by hand, scale out,
        // compress, a leg target, a pair-off) the next hedge follows the CURRENT net — net long → a SELL below, net short → a BUY above
        void SyncSideToNet(Snap sn)
        {
            if (sn.Pos.Count == 0 || Set.HedgeMode == "NONE" || Set.HedgeMode == "LADDER") return;
            int side = NetSide(sn.Pos, lastDir); if (side == lastDir) return;
            lastDir = side; quickBest = double.NaN; scaleRef = double.NaN; ManualHedge = double.NaN; ManualNoHedge = false; SetHedgeLevel(sn, lastDir); Version++;
            Note("↻ the basket now leans " + (side > 0 ? "LONG" : "SHORT") + " " + Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)).ToString("0.##", IC) + " lot → the next hedge is a " + (side > 0 ? "SELL below" : "BUY above"));
        }
        void Manual(Snap sn, AutoIteration it)
        {
            double g = Gross(sn.Pos);
            if (g > knownGross + 1e-6)
            {
                var p = sn.Pos.OrderBy(x => x.Time).ThenBy(x => x.Ticket).Last(); it.ManualAdds++; it.ManualLots = it.ManualLots + (g - knownGross); if (p.Dir > 0) { it.Buys++; it.BuyLots += g - knownGross; } else { it.Sells++; it.SellLots += g - knownGross; }
                it.MaxGross = Math.Max(it.MaxGross, g); if (!it.Ctx.Contains("manual=yes")) it.Ctx += ";manual=yes";
                Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = p.Side, Lots = g - knownGross, Price = p.Open, Level = double.NaN, Why = "✋ MANUAL ADD " + p.Side, Basket = Basket });
                lastDir = p.Dir; quickBest = double.NaN; SetHedgeLevel(sn, lastDir); Tell("✋ MANUAL ADD " + p.Side + " " + (g - knownGross).ToString("0.##", IC) + " @ " + P(p.Open) + " — added by hand; " + Plan(sn));
            }
            else { SyncSideToNet(sn); Tell("✋ PART CLOSED BY HAND — " + (knownGross - g).ToString("0.##", IC) + " lots closed outside the automation; " + Plan(sn)); }
            knownGross = g; Version++;
        }

        // ---- the numbers for the ANALYSIS
        public sealed class Stats { public int Done, Wins, Losses, Streak, BestStreak, WorstStreak, Buys, Sells, AddOns, MostAddOns; public double Net, Avg, WorstDd, BestIt, WorstIt; }
        public Stats Calc(IEnumerable<AutoIteration> list)
        {
            var d = list.Where(x => x.Done && !x.Unverified).ToList(); var s = new Stats { Done = d.Count, Wins = d.Count(x => x.Won), Losses = d.Count(x => !x.Won), Net = d.Sum(x => x.Result), Buys = d.Sum(x => x.Buys), Sells = d.Sum(x => x.Sells), AddOns = d.Sum(x => x.AddOns) };
            s.Avg = d.Count > 0 ? s.Net / d.Count : 0; s.WorstDd = d.Count > 0 ? d.Min(x => x.MaxDd) : 0; s.MostAddOns = d.Count > 0 ? d.Max(x => x.AddOns) : 0; s.BestIt = d.Count > 0 ? d.Max(x => x.Result) : 0; s.WorstIt = d.Count > 0 ? d.Min(x => x.Result) : 0;
            int w = 0, l = 0; foreach (var x in d) { if (x.Won) { w++; l = 0; } else { l++; w = 0; } s.BestStreak = Math.Max(s.BestStreak, w); s.WorstStreak = Math.Max(s.WorstStreak, l); }
            s.Streak = w > 0 ? w : -l; return s;
        }
    }
}

namespace KeystoneMT5
{
    // 09j (the user's 1920 px screen: two long 💡 buttons in the button row squeezed the banner to ~60 px and pushed the tab body out): the 🛡 HEDGE
    // top part's limits — the banner never below BannerMin wide, the button row at most what is left (it wraps), the top part at most TopShare of the height
    public static class HedgeLayout
    {
        public const double BannerMin = 520, ButtonsMin = 260, TopShare = 0.45, TopMinPx = 160;
        public static double ButtonsMax(double topWidth) { return Math.Max(ButtonsMin, topWidth - BannerMin - 20); }
        public static double TopMax(double rootHeight) { return Math.Max(TopMinPx, rootHeight * TopShare); }
        public const int InfoLines = 4;
        public static bool IsLong(string t) { return (t ?? "").Split('\n').Length > InfoLines || (t ?? "").Length > 90 * InfoLines; }
        // a long banner line → its first part (before " — "), the rest goes to the tooltip
        public static string Short(string t) { t = t ?? ""; int i = t.IndexOf(" — ", StringComparison.Ordinal); var s = i > 0 ? t.Substring(0, i) : t; int k = t.LastIndexOf(" · total now ", StringComparison.Ordinal); if (i > 0 && k > i) s += t.Substring(k); return s.Length > 140 ? s.Substring(0, 137) + "…" : s; }
    }
}
