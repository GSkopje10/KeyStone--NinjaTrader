# Keystone Playbook — Claude's own strategy idea and approach (2026-09-27)

This is my own proposal, not a summary of the user's plan. Nothing here is proven yet: every
rule below is meant to be built into the lab and must pass the gates in section 3 before any
money is risked.

## 1. New strategy: OPENING RANGE SWEEP & RECLAIM (ORSR)

**Idea.** The first minutes after a cash open set a range where many orders sit just outside
(stops of the early traders, breakout orders). Price very often pokes through one side, takes
those orders, fails, and comes back inside. That failed break is a trapped-trader move with a
natural stop (beyond the sweep) and natural targets (the middle and the other side of the range).
It is time-anchored (one decision window a day), so it is quiet, repeatable and easy to automate.

**Rules (both directions, one trade per instrument per day).**
1. Opening range (OR): MNQ 09:30–09:45 ET; MGC 08:20–08:35 ET (COMEX gold open).
2. Decision window: from the OR end until 11:00 ET. Flat by 11:30 ET (time stop).
3. SHORT: price trades **above the OR high by at least the sweep size** (default 15% of the OR
   height), then a 5-minute candle **closes back inside the range** (below the OR high).
   Enter at that close. LONG is the mirror (sweep below the OR low, close back inside).
4. Stop: beyond the sweep extreme + a small buffer (auto size: risk $ ÷ stop distance).
5. Targets: **T1 = OR midpoint** (take half, move the stop to entry), **T2 = the opposite OR edge**.
6. Grade A+: the sweep also took a liquidity level — overnight high/low (18:00–09:30) or the prior
   day's high/low. Those are the ones for funded and live accounts.
7. Skip days: OR height above 2× its 20-day median (news / chaos) or below 0.3× (dead day).

**Why it suits prop accounts.** One trade a day, a hard stop that is known before entry, a first
target inside the range (high hit rate for passing evaluations), no overnight risk, and it never
needs a second instrument to hedge. It also matches the "sweep then reverse" behaviour the user
sees in engulfing and double-trouble setups, but anchored to time and real liquidity levels.

**Parameters to optimise in the lab.** OR length (5 / 15 / 30 min), sweep size, reclaim candle
timeframe (1 / 5 min), decision-window end, T1/T2 split, A+ only vs all, skip-day thresholds.

## 1b. Two more brand-new strategies (BUILT in build 27l — Step 1 strategy list)

### LAST-HOUR RELAY (intraday momentum) — strategy code RLY
How the day starts tends to show how it ends: when the market moves strongly from the 18:00 Globex
open to 10:00 ET, the last half hour tends to continue that direction (documented "intraday momentum"
in index futures — late-day rebalancing and hedging follow the day's move). Rules: signal = move from
the Globex open to 10:00 ÷ the 20-day average daily range; above +0.25 → BUY at 15:25, below −0.25 →
SELL; out at 15:55. Protective stop 0.15 × average range, auto size to the Step 2 STOP $. Option: the
afternoon (10:00 → 15:25) must agree. One decision a day, ~30 minutes in the market, uses 1-minute bars
(load FULL GLOBEX). Not related to candle patterns, so it diversifies BH / FVG / engulfing.

### VWAP SNAP-BACK (midday mean reversion) — strategy code VWP
Between 10:30 and 14:30, price stretched far from the day's VWAP usually snaps partway back once the
push fails. Rules: session VWAP from 09:30 with σ bands; a candle closes beyond 2.5σ; the next candle
makes no new extreme and closes back toward VWAP → enter against the stretch at its close. Stop beyond
the extreme (auto size), target the 1σ band (or VWAP). Skip trend days (first hour wider than 1.8× its
20-day average). Max 2 trades a day. Works in the quiet middle of the day when the other strategies
are idle; high win rate with small targets suits evaluations.

Both run through the same pool, payouts, MONTHS & SESSIONS, LIVE ACCOUNT, COMPARE STRATEGIES and the
report. They are untested on real data — they must pass the gates below like everything else.

## 2. How I would run the two baskets

### Prop firms (the money is replaceable; the process is not)
1. **Gate first.** Only a system that passes section 3 goes on paid accounts.
2. **Evaluation = pass fast, cheaply.** Size so the profit target is 3–4 average winning days.
   1–2 setups a day (all grades). Daily loss stop at 25% of the evaluation drawdown. After the day
   reaches its daily target, stop (consistency rules and give-back).
3. **Funded = protect, then withdraw.** Grade A / DOUBLE TROUBLE / ORSR A+ only. Half size until
   the balance has a cushion equal to the drawdown room, then normal size. Withdraw the minimum
   payout every time it is available — profit left in the account is not yours yet.
4. **Fleet.** Copy groups of 3–5 accounts (never one copy of everything). Never more than 20% of
   the bankroll in open evaluations at once. Stop buying evaluations when the pass rate of the last
   10 falls below 20% — something changed; go back to the lab.
5. **Firms.** Prefer end-of-day drawdown over intraday trailing, loose or no consistency rule,
   fast payouts, and buy only on discount. Enter each firm's real cost and rules in Pool Settings.

### Live account (real money, slow and boring on purpose)
1. Separate bankroll (e.g. $2,000 of the $10,000). **0.5% risk per trade** for the first 100 live
   trades, then 1% if live results match the lab (win rate within 10 points, average R within 0.2R).
2. Grade A / A+ only, **one trade a day**, daily stop 1.5%, monthly stop 6% (LIVE ACCOUNT tab).
3. Withdraw 50% of every green month. Never add money after a losing month.
4. **No hedging, no averaging down, no martingale.** A full hedge is the stop loss paid later with
   spread and swap on top. If a stop is hit, the trade is over.
5. **Drift check every month:** compare the live month with the lab's same month. If they diverge
   for 30 trades, stop live trading and find out why before continuing.

## 3. Gates a system must pass before real money (all in the lab)
- At least 200 trades over at least 9 months, **after commission and slippage** (Step 1).
- Profitable in at least 70% of months (MONTHS & SESSIONS).
- Profit factor ≥ 1.3 and the best session **held up on the last third** it was not picked on.
- Worst drawdown at the chosen size below 60% of the account's drawdown limit.
- Prop: positive net after costs per $1 spent in COMPARE ACCOUNTS; first payout inside ~45 days.
- Live: LIVE ACCOUNT worst drawdown under 15% at the chosen risk %.

## 4. Order of work
1. Export real data (EXPORT FOR CLAUDE) → run the search over BH / FVG / ENGULFING / sessions.
2. Build ORSR as the 5th strategy and run it through the same gates.
3. Whatever passes: small live account + 3–5 discounted evaluations. Everything else stays in the lab.
