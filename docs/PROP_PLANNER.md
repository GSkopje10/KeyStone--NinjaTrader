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
