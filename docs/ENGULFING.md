# 123 ENGULFING (buy and sell) — rules from the user (2026-09-27)

Status: BUILT (build 27i). Engine `KeystoneArcEngine.DetectEngulfing`, tests `tests/EngulfingTests.cs`,
Step 1 option "123 ENGULFING • BUY / SELL".

## Setup
- **BUY:** a run of `MIN RUN` (default 2) or more **red** candles (10 in a row also counts), then a **green**
  candle whose close is **above the last red candle's body** (its open).
- **SELL:** the mirror: a run of green candles, then a red candle closing **below the last green body**.
- **Stronger (WICK):** the close is also beyond the last run candle's **wick** (high for buys, low for sells).
- **Strongest (SWEEP):** the engulfing candle's wick first **takes the lows** (buys) / **highs** (sells) of the
  last 2 run candles, then it closes engulfing. SWEEP + WICK after a run of `DT RUN` (default 3) = DOUBLE TROUBLE.
- **Entry:** at the close of the engulfing candle (market, next 1-minute bar; the signal candle's own minutes
  never decide the outcome).
- Every timeframe (1/5/15/30/60/240), MNQ and MGC, NY time, inside the chosen session window.

## Parameters
Direction BOTH / BUY / SELL • which setups ALL / STRONG (wick or sweep) / SWEEP / SWEEP + WICK • min / max run
• min engulfing body per instrument • stop: beyond the engulfing wick (auto size to Step 2 risk), fixed $, or
points by grade • target: R by grade (DT 3R, A 3R, B 2R, C 1.5R) or fixed $ • stop buffer • max contracts.

## Prop rule: NO HEDGING (all strategies)
Prop pools (rotation, copy to all, copy to groups) never hold the same instrument long and short at the same
time. While any account holds MNQ long, an MNQ sell setup is skipped ("NO HEDGING • …") until the long is
closed. MNQ and MGC may be in opposite directions. The personal / live account model may hedge.

## Use
Strong setups (sweep / wick, often around pre-NY 08:00 and the 09:30 open) for funded or live accounts with
bigger targets; all setups (even 1-minute) to pass evaluations. The analysis shows BUY vs SELL, BODY / WICK /
SWEEP and pre-NY vs NY-open results so the data decides.
