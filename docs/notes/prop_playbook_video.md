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

---

# Notes — the student interview (Ron: $2,500 → ~$257K payouts in ~7 months)

Saved 2026-10-15. For motivation; we build our own strategy, not his.

## What he says, and where we stand
| His point | Keystone Arc |
|---|---|
| **A blown evaluation costs its fee (~$100–120), not the drawdown.** Score = payouts − fees | ✔ every NET / ROI / ODDS / BATCHES number counts only fees and payouts |
| **≈ 1 in 10 evaluations reaches a payout** (10–15%); payout $2–3K per $100 → EV ≈ $300 per eval bought | ✔ ours (OPENING FVG +$2,000 / −$1,500 @ 10%, 50 rotating, 6 years): 34% pass × 28% paid ≈ **9.5% per evaluation** — the same 1 in 10. 🎲 ODDS shows P(payout per evaluation) and $ per evaluation bought |
| **First 25 evals: 23 blown, 2 paid** — losing many accounts is part of it; streaks of 25–30 evals with no payout happen | ✔ RISK OF RUIN table (5 … 100 evals, and your own number); 📦 BATCHES, LOSERS |
| **Don't put 6 accounts on one move** — he blew 5–10 accounts in an hour; spreading across moves cut the variance | ✔ ROTATE (one account a trade, the next one the next) vs 📋 COPY; our research: rotate first, copy multiplies the swings |
| **Take the payout the moment you are eligible**; smaller targets, don't chase the max | ✔ the sim pays out at the first eligible close; funded stop / target can be set separately |
| **Delayed gratification:** month 1 ≈ breakeven, month 2 small, month 3: $11K spent → $28K paid | ✔ EVAL TRACKER / EXPENSES day by day (spent so far vs received so far, breakeven date) |
| **Payouts denied:** 1 s under the micro-scalp rule, trading during news, a firm cancelling a plan (~$20–30K lost) | ✔ firm min-hold rule (Lucid 11 s) checked at pass / payout; news lock; ✘ a "denied payout %" haircut is not modelled yet |
| **Several firms** to reach volume; Lucid best ROI, also Tradeify, FundedNext, Topstep, MFFU; 100K accounts were worse EV than 50K | ✔ firms with their own rules in the set-up; re-simulate each size before buying |
| **Start with cheaper accounts** to measure pass rate, eval→payout rate and funded win rate on a big sample | ✔ that is what the 50-evaluation simulation measures, before any money |
| **ROI 2.5–3×** of total spent even with everything that went wrong ($78K spent → ~$257K paid) | ours: 54K net on the 50 × 50K plan ≈ 2.5–3× back on the evaluations bought — the same range |

## Ideas for us
- A "payout denied / firm risk" haircut (e.g. 10%) in the 🎲 ODDS view, so the numbers stay honest.
- Track eval → payout conversion per firm and per month (his main dashboard numbers).
- Spread rule: never more than N accounts on the same trade (between ROTATE and COPY).
