# Asian 75 reversal cycle — rules and optimizer

Confirmed with the user on 2026-09-25. Code: `KeystoneArcEngine.PrepareAsian75Sessions` /
`SimulateAsian75Sessions` in `src/KeyStone.cs`.

## Rules

1. At the entry time (default 18:00 ET) open MNQ and/or MGC in the start direction
   (default LONG), 1 micro each, at that minute's opening price.
2. Each instrument has its own stop. Default (FIXED CASH) mode: every leg loses the same
   dollar amount (default $75), so the stop tightens as size grows
   (MNQ x1 = 37.5 pts, x2 = 18.75 pts; MGC x1 = 7.5 pts).
3. When a stop is hit, that leg closes and the instrument reverses on the next available
   1-minute bar, adding one contract (x1 → x2 → x3 …).
4. "Max reversals" counts reversals **after** x1: 3 means x1→x4 (four legs). After the last
   leg is stopped, that instrument is done for the night; the other keeps going.
5. The whole cycle closes at the first of: combined target (default $350, checked on each
   1-minute close), optional combined cycle stop, breakeven guard, the daily loss limit
   (lab default AUTO = leg loss × max reversals × instruments), or the session end (15:55 ET).
6. Copy trading: every virtual account receives the same completed cycle.

## Data conventions

- NinjaTrader stamps 1-minute bars with their **closing** time: the 18:00–18:01 bar reads
  18:01. The engine detects this from the data (a close-stamped series resumes at 18:01 after
  the daily halt) and enters on the bar that opens at the entry minute.
- A stop is checked on the entry bar too (entry is at its open). A bar that opens beyond the
  stop fills at that open (labelled GAP).
- A session named D runs D 18:00 → D+1 15:55. Friday-named sessions have no data.

## Optimizer

In the lab: select ASIAN 75, START RESEARCH, load the data, then **OPTIMIZE ASIAN 75** on the
data tab. It uses the bars already loaded — nothing is exported or re-requested. Results are
saved to `Documents/KeystoneArc5MResearch/AsianOptimizer/`. **APPLY RANK** copies a row back
into the Asian settings so it can be run and inspected on the evidence chart.

How to read it:
- Ranking uses only the IN-SAMPLE (earlier) sessions. OUT-OF-SAMPLE columns show the later
  sessions the ranking never saw. On random data the top in-sample rows look excellent and
  then fail out-of-sample — trust settings that stay good out-of-sample and in most years
  (`YRS+`), not the single best in-sample row.
- Net figures include the per-contract round-trip cost (default $1.00); every leg pays it.
- High win % usually comes with rare large losses (more legs, bigger size). Compare max
  drawdown and worst year against the prop account's drawdown limit.

Developer tooling (Linux/mono, no NinjaTrader needed):
- `tests/build_engine.sh tests/Asian75EngineTests.cs` — engine + optimizer tests.
- `tools/optimize_asian75.sh --help` — the same optimizer from the command line on NinjaTrader
  export files or `--synthetic` random-walk data.

## ASIAN MATH LAB (build 10-03p)

Open it with the **ASIAN MATH LAB** button in the header. It also opens and runs by itself after every Asian run. It uses the bars already loaded and downloads nothing.

**Inputs.** Type one value or a list in each box:
- instruments (MNQ, MGC, BOTH)
- directions, and optionally BOTH with opposite directions
- starting micros
- leg loss $, reversals, take profit $
- max combined loss (0 = AUTO)
- start / end time, and cost per contract
- firm rules, evaluation $ and activation $

Every combination runs through the same `SimulateAsian75Sessions` engine the lab uses. Then it goes through the prop rules, using the same code as the PROP PLANNER and MICRO A DAY.

**Tabs:**
- **ADVICE:**
  - written conclusions, including whether the best row also worked on the unseen last 30% and in every year
  - which value of each setting wins (median prop result, accounts lost, full-loss nights)
  - a **REVERSALS × TAKE PROFIT** grid: more legs win more nights but lose more accounts, and the grid shows that trade-off directly
- **RANKING:** best cards; filter by MNQ / MGC / BOTH / LONG / SHORT; sort by prop, plain, first payout, pass %, unseen 30% or smallest drawdown.
- **CHARTS:** combined vs MNQ vs MGC night by night, plus the prop and copy curves.
- **EVALS & FUNDED:** first payout (the days it took and the money spent before it), payouts in a row, failed evaluations in a row, and every account.
- **SIZE & SPEED:** every starting size × leg loss that was tested (real simulations, not multiplied), sorted by the fastest first payout.
- **COPY TRADING**, **LEGS** (how nights ended, highest leg, each instrument), **MONTHS** (profit vs expenses).
- **YEARS & PERIODS:** each year, the early 70% vs the unseen 30%, and Period A vs Period B on any dates.
- **NIGHTS:** every night. Click one to open the chart on it.

**Buttons:**
- **APPLY SELECTED TO LAB:** writes the row into the Asian settings, re-runs the cycle on the loaded bars and recalculates the pool. The chart and replay then show it. There's no reload.
- **EXPORT HTML + CSV.**

**Chart:** the Asian side panel has a **THIS NIGHT** box. It shows legs, max size, P/L, highest peak and lowest point for MNQ, MGC and combined, and follows the replay cursor.

**Instrument views:** MNQ / MGC / BOTH give the same numbers whatever the click order. The max combined loss is your typed number, or the automatic value for that view.
