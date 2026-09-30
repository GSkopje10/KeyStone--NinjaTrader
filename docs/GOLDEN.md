# GOLDEN SETUP • first BH / FVG after the open (build 30q)

The user's "golden" idea, testable on real data: **one trade a day per instrument — the first bullish
setup after the start time.**

- MNQ from 09:30, MGC from 08:00 (editable). Every candle of the pattern must form after the start.
- Setups: **BH** (the unchanged BH rule: red → green reference → next candle breaks its high, entry at
  that high) and/or a **bullish 5-minute FVG** (candle 3 low above candle 1 high): entry at the
  **close of candle 3** (default) or **touch + break** like BH. Whichever comes first is the trade;
  both on the same candle = DT (BH fills first inside the candle).
- Target in points (MNQ +100, MGC +10). Stop **below the pattern's low** (BH: lower of the red and
  reference candles; FVG: lowest of the three candles) plus optional extra points, or fixed points;
  optional "skip if the stop is bigger than".
- **Push down (aggression)** before the setup, measured on every trade: points from the start
  price to the lowest low before the trigger, and red candles in a row after the start ending at the
  pattern. MEASURE + COMPARE (default) tags it (AGGR / BASE); REQUIRED trades only setups with it.
- Trades a day: 1 = first setup only. 2–3 = after a LOSS the next setup is taken; a win ends the day.
- Resolved on the verified 1-minute path (stop first when a minute touches both), then the usual
  pool, payouts, months & sessions, chart/replay and report. RESEARCH FINDINGS adds: with vs without
  the push down, BH vs FVG, stop sizes, first vs later tries.
- Data window: earliest start → 16:55, the same as the FVG default, so saved bars are reused.
- Tests: `tests/GoldenTests.cs`.
