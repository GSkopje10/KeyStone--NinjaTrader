# RECOIL • ADD TO LOSERS (build 30w, part 1)

Strategy choice **RECOIL • ADD TO LOSERS** in Step 1.

## Rule
- From the start time (MNQ and MGC can start together or at their own times) the start price is the open of the
  first 1-minute bar after the start. When the price moves TRIGGER points away (MNQ 100, MGC 20 by default) we
  enter AGAINST the move: down → BUY, up → SELL.
- Every STEP points further against us one more add (ladder +1 each: 1 → 2 → 3 → 4 contracts; or growing
  1 → 3 → 6 → 10; or doubling 1 → 2 → 4 → 8), up to MAX ENTRIES.
- Exit at the profit target ($ for the whole ladder, or points from the average price), at the MAX DRAWDOWN
  (the blowup), or at the close time. After a win a new ladder can start from the exit price (ladders a day).
- BOTH: the two ladders run at the same time; the max drawdown is SHARED (both closed when together they reach
  it, including ladders already closed that day) or SPLIT (each instrument its own).

## Money math (exact)
Each contract keeps its fill price. P/L at a price = direction × Σ (price − fill) × contracts × $ per point.
Average = Σ fill × contracts ÷ Σ contracts. Example MNQ BUY 19900, adds 19800 / 19700 / 19600 → 4 contracts,
average 19750, −$1,200 at 19600, blowup −$2,000 at 19500, target $400 at 19800. Costs: commission per contract
per side; slippage ticks on the stop and close exits (market orders). Entries / adds / targets are limit orders.

## Fills on 1-minute bars (conservative)
- A gap through an add level fills at the open; a gap through the target or the blowup exits at the open.
- Inside a minute the blowup is checked before the target. A target reachable before any add counts with the
  current size; if adds fill in the same minute, the (new) target only counts when the minute CLOSES beyond it.
- The trigger minute: adds on the way to the minute's extreme fill; a target only on the close.

## Step 3
OVERVIEW (MNQ only / MGC only / BOTH with the same settings), STEPS & BOUNCES (how deep ladders went, the
bounce after the worst price, what the full ladder was worth there, every blowup and whether the price came back),
RISK GRID (add distance × $ target, all years and each year), ONE LIVE ACCOUNT (start balance, $ per point per
contract/lot), EVERY LADDER (click → chart), BREAKDOWNS. EXPORT writes the HTML report and a CSV of every ladder.

## Chart
Each ladder: start price and trigger, every fill with its size, the average (steps at each add), target and
blowup lines (they move with each add), next add level, the exit. PLAY replays it minute by minute; the live box
shows contracts, average, open P/L, distance to the target and the blowup, and the day's log.

## Next parts
2. Prop simulation: evaluation fee, funded, payouts, copy trading, rotation (each add on another account) and
   account groups by session. 3. Report and analyst verdict on the prop math.
Tests: `tests/RecoilTests.cs` (ladder math to the cent).

## Build 10-02y • STOP AND REVERSE
Step 1 → AFTER EVERY STEP POINTS AGAINST US: ADD TO THE LOSER (ladder) or STOP AND REVERSE.
REVERSE: the first trade (1 contract, against the trigger move) is closed after STEP points against it and the
next size (2, then 3, then 4) opens the OTHER way at that price. The target makes the whole cycle +TARGET $ (it
covers the closed steps), e.g. MNQ 100-pt steps, $400: step 2 needs +150 pts, step 3 +167, step 4 +200.
All four steps losing = −$2,000 (−200 −400 −600 −800). Every run also runs the other mode on the same days:
OVERVIEW shows ADD vs REVERSE side by side (and the export includes it).
Minute model (REVERSE): each 1-minute bar is walked open → the extreme nearer the open → the other extreme →
close; stops and targets are hit at their exact prices along that path; stops are market (1 tick slippage).
With BOTH instruments REVERSE uses each instrument's own drawdown.
