# Notes — "If I had $1,000 and had to start over with prop firms" (YouTube, a trader with $2M+ in payouts)

Saved 2026-10-14. His method, point by point, and where Keystone Arc stands.

## His method
1. **Buy nothing first.** Prove the plan in replay: simulate **50 complete evaluations**, then the funded accounts that pass
   (e.g. 15 funded → see how many payouts they really give).
2. **Follow every firm rule exactly** — trail the drawdown at the end of the day (that is how firms make money), consistency
   rule, winning-days rule, daily loss limit (DLL), payout caps ("you made $2,000 but can only withdraw $1,000 — then what?").
3. **The only score: payouts − evaluation fees (+ resets, activations, every cost).** Win rate does not matter. Pass rate only
   makes funded accounts cheaper. Unpaid funded balance counts for nothing. A positive live equity curve means nothing.
4. **Track:** pass rate, fails, payout rate, payouts per funded account ("15 funded, $15K paid → each new funded ≈ $1,000"),
   net money.
5. **If it does not make money:** change risk-to-reward first (one R:R is optimal for a firm's rule set), then risk
   management, then the strategy. **Optimise evaluation and funded separately** (eval goal = pass rate, funded goal = payouts;
   different R:R for each). Re-run the 50.
6. **Risk of ruin:** P(payout per eval bought) = pass rate × funded payout rate. P(no payout in N evals) = (1 − p)^N.
   Example: 30% × 33% ≈ 0.10 → 20 evals: 0.9^20 ≈ 12% ruin. Expected value = payout size × probability; smaller payouts =
   higher probability = lower ruin at similar EV.
7. **Start:** $1,000 → **Lucid Flex 25K** (≈ $50 with the DLL on) = 20 evaluations. Never trade with less than $1,000.
   His stats there: **37% pass, 35% payout rate, 4.2× ROI.**
8. **Request every payout the moment it is allowed.** Payouts matter, not account balance.
9. **Scale with the bankroll:** $3K → 50K accounts (Lucid, Topstep, Tradeify, FundedNext), $15K → 150K accounts.
   **Re-simulate the 50 for each firm/size** — target vs max loss changes the optimal R:R
   (Lucid Flex 25K ≈ 1 : 1.25, 50K 1 : 1.5, 150K ≈ $9K target / $4.5K loss = 1 : 2, as he states it).
10. **Trade exactly what was tested**, log any rule break, compare real results with the backtest.

## Where Keystone Arc is (build 2026-10-13x)
| His point | In the app |
|---|---|
| 50 evaluations, then the funded ones | ✔ PROP SIM: N accounts (firms × evaluations), PASS FIRST, funded stage, payouts, 📦 BATCHES |
| EOD trailing drawdown, consistency, min days, DLL, payout caps / %, max payouts | ✔ per firm (EOD / INTRADAY / STATIC, lock, consistency, min days, daily loss, payout %, cap, split) |
| Net = payouts − fees, all costs | ✔ NET, SPENT, ROI, EXPENSES, breakeven; unpaid balance not counted |
| Pass rate, fails, payouts | ✔ OVERVIEW, EVAL TRACKER, BATCHES, LOSERS, PAYOUT CYCLES |
| 6-year tests, R:R grid, halves | ✔ docs/research/openfvg (target × stop × entry %, 10–50 evals, rotate vs copy) |
| Payout requested as soon as eligible | ✔ (the engine pays out at the first eligible close) |
| **Payout rate (funded accounts with ≥ 1 payout) and $ per funded account** | ✘ not shown as one number — to add |
| **Risk of ruin for a budget** ((1 − pass × payout rate)^N) | ✘ to add |
| **Separate stop / target for funded** on studio strategies (e.g. OPENING FVG) | ✘ only the rotation has it — to add |
| **Lucid Flex 25K / 150K presets** (check the firm's current rules first) | ✘ only 50K-class presets — to add |
| Bankroll ladder ($1K / $3K / $15K) | ✘ to add as a planner view |
| Real results vs backtest | partly: LIVE journal + MY TRADES; no side-by-side yet |

## Our numbers so far, against his
OPENING FVG • MNQ, +$2,000 / −$1,500 at 10% of the zone, 10 evaluations rotating, LUCID FLEX 50K rules, 6 years:
**33.9% pass** (his 37%), +$53K net on $21.6K spent (≈ 2.5× return; his 4.2×). The default −$2,000 / +$1,000 at 50% loses
(a +$983 day lifts the trailing floor, so the next −$2,017 loss blows the account).
