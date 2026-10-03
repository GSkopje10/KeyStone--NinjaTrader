# MATH LABS (build 10-03s)

Open them with the **MATH LABS** header button. A lab also opens and runs by itself after its strategy's Step 1 run. Every lab uses the 1-minute bars already loaded and downloads nothing.

| Lab | Load in Step 1 | One row = |
|---|---|---|
| ASIAN 75 | ASIAN 75 REVERSAL, BOTH | instruments × direction (or TREND) × micros × leg loss × reversals × take profit × max loss |
| 5M FVG | FIRST 5M FVG STUDY, BOTH | MNQ (09:30) or MGC (08:00) × entry style (touch, 25%, 50%, green close + break, prior FVG touch / 50%, first BH) × target × stop × contracts |
| 123 ENGULFING | 123 ENGULFING, BOTH | MNQ or MGC × timeframe (1/5/15/30/60…) × BUY / SELL / BOTH × signal (ALL, WICK, SWEEP, SWEEP+WICK) × min run × target × stop × contracts |

**How trades are filled (5M FVG and 123 ENGULFING):**
- 1-minute bars only.
- A minute that touches both the target and the stop counts as the stop.
- A gap through the stop fills at the open.
- A limit fill's own minute cannot reach the target.
- Engulfing enters at the next minute's open after the signal candle closes, and holds one position at a time.
- Everything is out at the close time.
- Costs: commission both sides + 1 tick per contract.

**Tabs (same in every lab):**
- **ADVICE:** written conclusions (best for prop, best plain, BUY vs SELL vs BOTH, timeframes, your targets), plus "which value of each setting wins" (median plain / prop, accounts lost).
- **ADAPTS EACH YEAR?:** walk-forward. Each year's settings are chosen only from the earlier years. This is the honest test.
- **RANKING:** sort by prop, plain, win %, first payout, unseen 30% or smallest drawdown.
- **GRID:**
  - FVG: stop × target per entry style
  - Engulfing: timeframe × direction
  - Asian: reversals × take profit
- **CHARTS, EVALS & FUNDED** (first payout, money spent before it, payouts in a row, every account), **COPY TRADING**, **MONTHS** (profit vs expenses).
- **YEARS:** plus weekday and hour of entry.
- **TRADES:** click a trade to open the chart on that day.
- **EXPORT HTML + CSV:** send both files to Claude.

**What counts as proven:** plain profit in the early 70% AND in the unseen last 30%, prop positive in every year, AND the walk-forward positive in most years. The advice calls this **STRONGEST**. If nothing qualifies, the lab says "no proven edge yet".
