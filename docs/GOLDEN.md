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

## Build 30r
- **Minimum FVG gap** (candle 3 low − candle 1 high): MNQ ≥ 5 pts, MGC ≥ 1 pt by default. Smaller gaps are ignored.
- **Skip a setup whose stop is bigger than its target** (on by default).
- **Chart labels** for every GOLDEN trade: gold START line at the start time; PUSH DOWN line from the start
  price to the low before the setup (red = aggression, grey dashed = none); FVG gold box with candles
  1 / 2 / 3, or BH RED / REF / BREAK with the reference-high line; ENTRY, TP (green) and SL (red) lines until
  the exit; result tag "GOLDEN FVG • WIN $1,000 10:12". In replay each part appears when it happened.
- **LIVE ACCOUNT • USE THE STRATEGY'S OWN CONTRACTS** (on by default for GOLDEN): one real account, the
  strategy's contracts (no % sizing), one position per instrument, so MNQ and MGC can be open together.

## Build 30s • GOLDEN ENTRY STUDY (no prop rules)
Goal: find the winning entries first; accounts (live, prop, rotation, copy) come later.
- **Hold until target or stop** (default): trades carry overnight and into the next days. The data window becomes the
  full trading day (18:00 → 17:00) so the overnight path is loaded. "Close at the time below" is still an option.
- **Strictly the first setup** (default): if a filter skips the day's first setup, no trade that day for that instrument.
  "Take the next setup" is the alternative. The FVG minimum gap defines what an FVG is (a sliver is not a setup).
- **Evening start**: a start time 1800 or later belongs to the next trading day's session (18:00 → 17:00).
- **Step 3 for GOLDEN** shows the study instead of the prop pool:
  - FILTERS (setup type, push down, red candles, FVG gap, max stop, stop > target, entry hours, weekdays, tries,
    account start, one trade open per instrument), applied live to the entries already found (no new detection);
  - MNQ • MGC • BOTH comparison and WHAT MAKES THE DIFFERENCE (groups whose win rate differs by 8+ points);
  - WHAT MAKES WINNERS: every condition grouped (setup, push down, red candles, gap, stop size, hour, minutes after
    the start, weekday, try, how it ended, year, month) for MNQ, MGC and BOTH;
  - TARGET × STOP: each entry replayed on its 1-minute path for fixed stops / the pattern stop and targets; the best
    cell and your setting, for all years and each year;
  - ONE ACCOUNT: start balance + each trade with the strategy's contracts, balance curve, months;
  - EVERY ENTRY: all first setups, filtered ones in grey with the reason; click opens the chart.
  - EXPORT STUDY writes an HTML report and a CSV of every entry to the KeystoneArc5MResearch folder.
- Strategy list now: BH, ASIAN 75, FVG, HELIX, GOLDEN (123 ENGULFING, LAST-HOUR RELAY, VWAP SNAP-BACK hidden; code kept).

## Build 30v • WHICH ENTRIES
Every GOLDEN run now also detects, on the same bars: the first BH of each day, the first 5M FVG of each day
(even when a BH came first) and EVERY 5M FVG of the day. Step 3 → ENTRIES picks the set for every tab; the
WHICH ENTRIES tab shows all four side by side with the same filters, the break-even win rate and one live
account (one trade open per instrument, or take all). The export includes the comparison.

## Build 10-03g
- FVG entry default is now **RETEST + BREAK**: the gap forms (candle 3 low above candle 1 high), price comes back into the gap without closing below it, a green candle closes (after a red touch the next candle must close green), the next candle breaks that green high → entry at that high. CANDLE 3 CLOSE (enter the moment the gap forms) is still selectable.
- MOVE STUDY opens automatically after every GOLDEN run.
