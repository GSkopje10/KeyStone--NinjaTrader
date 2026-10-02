# PROP PLANNER — the business math of prop firms

Open it from the **PROP PLANNER** button in the lab header, or from **PROP PLANNER WITH THESE DAYS** in the RECOIL Step 3 view.
It is a separate window; nothing in the lab changes.

## The idea
Evaluations are the expense, payouts are the income. The planner buys thousands of simulated evaluations and follows each one
through its whole life: evaluation → pass (or blow) → funded → payouts → lost or retired. The output is the one number that decides
the business: **VALUE OF ONE EVALUATION** = average cash received − average cost. Above zero you make money in the long run no matter
how many evaluations fail; below zero more accounts only lose faster.

## Inputs
1. **Firm rules** (presets for 50K at 50 / 40 / 35% consistency and a 25K): evaluation cost, activation fee, profit target, max drawdown,
   drawdown type (EOD / INTRADAY / STATIC) and where the floor stops trailing (+$100), consistency %, minimum days, daily loss limit,
   payout after N good days, good day = at least $X, payout % of profit, payout cap, your split, max payouts.
2. **How you trade (the days)**
   - **COIN FLIP** (no strategy): each day ends at +target or −stop with the fair chance stop ÷ (target + stop) — pure prop math. On the
     evaluation's last day you aim only for what is left; a stop never goes past the room left. Optional edge in % points; costs per day.
   - **RECOIL DAYS** (MNQ / MGC / BOTH, after a RECOIL run): every loaded day of the run, 0 on days without a ladder; the worst moment of a
     day is the sum of the ladders' worst open P/L (an account that dips through its floor is lost even if the day recovers). Size ×
     and an optional day stop for the evaluation and for the funded account.
3. **Your program**: accounts at once, months, simulated evaluations.

## Outputs (tabs)
- **THIS PLAN**: verdict, pass rate, days to pass, what a funded account pays, value of one evaluation, share of losing evaluations,
  fails in a row (worst 1 in 20) and the money that streak costs. With RECOIL days, also the same rules with pure luck for comparison.
- **SEPARATE • COPY • ROTATION**: the same number of accounts run three ways — evaluations bought, passed, payouts, spent, received,
  typical / bad (1 in 10) / good (1 in 10) result, chance of losing money, money needed (worst 1 in 20), cash by month.
- **HISTORY BY YEAR** (RECOIL days): one account slot walking the real days in order, per year (bought, passed, payouts, funded lost, net).
- **SWEET SPOT**: many evaluation × funded plans under the same rules, ranked by value per evaluation. Click a row (or USE BEST PLAN)
  to load and run it.
- **EXPORT REPORT (HTML)** → `Documents\KeystoneArc5MResearch\KeystoneArc_PropPlanner_*.html`.

## Rules of the model (checked in tests/PropPlannerTests.cs)
- Pure luck, 2 days of +1,500 / −2,000 = (2000/3500)² = **32.65%**; the simulator reproduces it.
- EOD floor trails the best close and stops at +$100; an intraday dip through the floor loses the account.
- Consistency: the best day must be ≤ the % of the total profit when the target is reached (otherwise keep trading).
- Payouts: N days ≥ the good-day amount → payout % of the profit, capped, × your split; the counter restarts.
- COPY = exactly N × one account (same days on every copy) — same value per evaluation, N× the swings and the money needed.
- Limits: an evaluation is dropped after 150 trading days, a funded account retired after 250.

## REAL BRACKET — a real rule instead of a coin flip (build 10-03c)
**Where the dates come from:** the lab's Step 1. Choose **PROP BRACKET • ONE TRADE A DAY** as the strategy, pick the dates
(2 years or more) and MNQ / MGC / BOTH, press START. The lab loads real 1-minute bars 07:00–16:00 New York and opens the planner
with **REAL BRACKET • MNQ** (or MGC) as the day source. (Any 1-minute load — e.g. RECOIL — also offers REAL BRACKET.)

**The rule:** one trade a day. At the ENTRY TIME (New York) trade in the DIRECTION rule's way at the next minute's open, with a
target and a stop. Close at the CLOSE time if neither is hit. Sizes are dollars + contracts → points (MNQ $2/pt, MGC $10/pt,
rounded to the tick). Costs: commission $0.62 per side + 1 tick per contract.
- Directions: FOLLOW 5M CANDLE (the 5 minutes before the entry; green = buy), FADE 5M CANDLE, FOLLOW / FADE LAST 30 MIN, ALWAYS BUY, ALWAYS SELL.
- Evaluation's last day: aim only for what is left to the target (when the consistency rule allows). The stop never goes past the floor.
- A minute that touches both the target and the stop counts as the STOP. A day without the exact entry minute or the direction
  candle is a no-trade day (no price is invented).

**THIS PLAN** shows THE RULE TO FOLLOW as numbered steps, the dates it was tested on, how the days ended (target / stop / close %)
for the evaluation and the funded size, and the same plan with pure luck for comparison. **HISTORY BY YEAR** walks the real days in order.

**FIND SWEET SPOT (REAL BRACKET):** (1) every entry time × direction with your sizes; (2) for the best 4 rules every evaluation
size (contracts × $1,000/$1,500 target × $1,000/$1,500/$2,000 stop) and funded size (contracts × $200/$300 × $300/$600/$1,000);
(3) the best 25 are re-run on each year's days alone. ✓ = positive in every year (a candidate), ✗ = needed a lucky year.
USE BEST PLAN takes the best ✓ rule. Fewer than 120 loaded trading days → a warning: the result cannot be trusted.

## PROOF TEST + max-payout pace (build 10-03d)
- Every REAL BRACKET **FIND SWEET SPOT** saves its best 25 rules to `Documents\KeystoneArc5MResearch\PropPlanner_SavedRules.txt`
  (per instrument, with the dates they were found on).
- **PROOF TEST** re-runs those saved rules on the data loaded now and shows FOUND value vs NOW value, per year, HOLDS? YES / PARTLY / NO.
  If the dates overlap the dates the rules were found on, it says so: that is not a proof.
- The sweet spot also tries funded days of $500 and **$800** (the max-payout pace: $2,000 cap = 50% of $4,000 in 5 days) with
  up to 4 MNQ / 2 MGC, so the data decides between small safe days and the max payout.
- THIS PLAN shows **WHILE FUNDED, PER WEEK** (cash per funded account per 5 trading days; max $1,800).
- EXPORT writes the full report (every sweet spot row + the proof table) and `..._days.csv` with every day of the rule.
- The planner keeps your last rule and firm rules when it reopens; a sweet spot from other dates is no longer shown.
