# MICRO A DAY

**Rule:** at the 18:00 New York session open, buy or sell 1 micro (MNQ, MGC or both). There is no stop, no adding and no management. The trade runs through every session until it reaches the take-profit (if one is set) or the 16:59 close.

## How to run
1. Step 1: choose **MICRO A DAY • 1 MICRO AT THE 18:00 OPEN, HELD ALL DAY**.
2. Set the direction (BUY / SELL), the MNQ and MGC take-profit in points (0 = hold to the close), the number of micros, and the open/close times.
3. Set the dates, ending yesterday, then press START.
4. Step 3 shows every day in the virtual pool and on the evidence chart. Each day's trade starts at the 18:00 bar's open; its exit is either WIN (take-profit) or SESSION EXIT (16:59 close).
5. The **COMPARE** window opens on its own; the COMPARE button reopens it. It lists 36 versions:
   BUY/SELL × MNQ/MGC/BOTH × take-profit (MNQ 0/25/50/75/100/150 pts, MGC 0/5/10/15/20/30 pts).
   - Each version shows net P&L after costs on a plain account, max drawdown, and the longest time underwater.
   - It also runs the prop simulation (Prop Planner firm rules): evaluations bought, passed, payouts, funded accounts lost, money spent and cash kept.
   - Versions are ranked by prop net. The EACH YEAR tab splits the results by year.

## Rules
- A day needs a bar within 15 minutes of the open; otherwise there is no trade that day.
- Prices come only from the 1-minute bars. Nothing is made up.

## Results window (build 10-03o)
Bar at the top:
- firm rules preset, evaluation $ and activation $
- **MICROS: EVALUATION / FUNDED**: how many micros to trade in each phase (the plain account stays at 1)
- RE-RUN, **EXPORT HTML + CSV** (the HTML report opens in your browser), **GO TO LAB STEP 3** (the pool, first return, payout cycles and the chart with replay for the run you started)

Tabs:
- **RANKING**: the best version for prop, for the plain account, for MNQ only, MGC only and BOTH. Filter by ALL / MNQ / MGC / BOTH / BUY / SELL and sort by prop, plain, value per evaluation or pass %. Click a row to select that version in every other tab.
- **CHARTS**: plain account and drawdown, prop cash − spent, copy ×1/×5/×10, and every day's result.
- **EVALS & FUNDED**:
  - money: bought, passed, failed, funded lost, payouts, spent, cash, money needed
  - first payout: the date, days and sessions it took, money spent and evaluations bought before it
  - payouts in a row, failed evaluations in a row, months in profit
  - the random-order pass chance
  - every account
- **SIZE & SPEED**: 7 evaluation sizes × 4 funded sizes, sorted by the fastest first payout, with what each costs.
- **COPY TRADING**: COPY (the same trade on N accounts) and STAGGERED (each account starts 5 sessions later), for 1–20 accounts, with whether it fits the $10K budget.
- **MONTHS**: trading P&L, bought, passed, payouts, lost, spent, cash in, net and running total per month and per year.
- **BEST TAKE PROFIT**: each instrument and direction alone, 31 take profits each, per year; ★ marks the best.
- **SESSIONS & HOURS**: Asia / London / New York and every hour (average points, up days), plus when the take profit was hit.
- **DAYS**: by weekday, and every trade. Click a trade to open the chart on that session.
- **EACH YEAR**: the selected version, and every version's prop result per year.
