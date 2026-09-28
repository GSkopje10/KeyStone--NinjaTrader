# HELIX ROTATION • prop basket math (build 28n)

Strategy choice **HELIX ROTATION • PROP BASKET MATH** in Step 1. Reverse-engineers the Manus
"9:30 LL" random rotation so we can decide, on real NinjaTrader data, whether it is worth automating.

## Rules (engine `KeystoneHelix.Run`)
- One basket at a time: MNQ and MGC open together on one account (directions per leg: BUY, SELL,
  RANDOM, ALTERNATE, OFF). Entry at the open of the 1-minute bar after the entry minute.
- The basket closes on its COMBINED $ target or $ stop (or the force-close time). STRICT fills: a
  minute touching both counts as the stop; NEUTRAL: judged on minute closes.
- After the pause the next account in the rotation opens the next basket. Each day starts at
  account 1 (or continues).
- Profit lock per account and day: FIRST WIN (Manus), DAY POSITIVE, DAY TARGET, NONE; optional
  max losses per day and day stop; optional shrink of the target to the remaining day target.
- Full prop life per account: evaluation (target, max loss static/trailing, min days,
  consistency %) or direct funded → payouts (pool review every N sessions or per account, profit
  needed, % of balance above liquidation, cap, split, min amount, max payouts) → liquidation →
  replacement (at the next review like Manus, or after N sessions; optional purchase budget).
- Costs: commission per contract per side, slippage ticks per side per leg.
- Evaluation and funded accounts can use different basket rules.

## Manus workbook (NinjaTrader.xlsx, not confirmed)
Rules recovered from its daily rows: BUY/BUY at 09:30, **+$1,000 / −$500** basket (332 of 564 days
match exactly, the rest have a forced 16:00 close), lock after the first win, $2,000 cushion,
$500 per account, payout review every 5 sessions, balance ≥ $4,000 → 50% capped at $2,000,
80% to you, replacement at the next review, no commission or slippage. Workbook: 37.4% basket
win rate (break-even 33.3%), $638,355 trading P/L, $1,008,000 gross withdrawals (10 accounts).
The day rows are embedded (`KeystoneHelixManusReference`) and compared day by day in MANUS CHECK.

## Step 3 HELIX view
Overview (cash curve, trading curve, months) • Baskets • Days & times • Accounts & payouts •
Expenses & cash • The math • Proof tests (BUY/SELL/RANDOM directions, instruments, fills, costs,
halves, start times, pauses, account counts) • Setup finder (conditions found on 2/3 of the
dates and checked on the last 1/3) • Manus check. Chart: one band per basket with the account
and combined result; the pins are the instrument's leg. EXPORT HELIX REPORT writes the full HTML.

## What the math says
On a random market with no drift (synthetic 2.5 years), BUY/BUY wins ≈ 32% (fair 33.3%) and
the trades lose, yet the Manus account rules still end far positive: withdrawals are the
firm's money while the loss per account is capped at its price. Whether that survives real
firm rules (evaluation cost and pass rate, trailing drawdown, consistency, payout caps, account
limits) is exactly what to test on real data before automating.
