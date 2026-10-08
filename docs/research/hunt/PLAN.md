# THE PLAN • 50 evaluations, aggressive, built on the firms' math (2026-10-08)

Sources: `hunt.md` (27 entry families × 8 stops × 8 targets × 3 firms × 7 sizes, MNQ + MGC, picked on 2021–2024 and
tested on 2025–2026) and `zero_edge.md` (the firm rules alone, a fair coin, 20,000 evaluations per setting).

## What the data says (short)
1. **No entry rule survived.** Plans picked on 2021–2024 lost in 2025 and 2026; the 5-plan portfolio from $5K lost
   −$6.2K (2025) and −$5.0K (2026). Treat every entry as a coin flip.
2. **The rules can still be beaten with no edge — barely.** With a fair coin, the best sizing makes:

   | firm | eval sizing | funded sizing | pass % | **net per eval** |
   |---|---|---|---|---|
   | Tradeify Select 50K ($99) | 3R target, risk $400, 1 trade/day (2nd only after a loss) | same 3R • $400 | 24% | **+$67** |
   | Tradeify Select 50K | 5R target, risk $250 | 2R • $600 | 23% | +$52 |
   | Lucid Flex 50K ($120) | 2R target, risk $750 | 2R • $400 | 23% | +$41 |
   | Lucid Flex 50K | 3R target, risk $400 | same | 24% | +$39 |
   | Take Profit Trader 50K ($170 + $130) | any | any | – | **negative everywhere** (best −$34) |

3. Small targets (0.5R, "high win rate") are the **worst** setting: −$120 per eval. The firms are built to farm
   high-win-rate scalpers — the consistency rule plus the trailing drawdown eat them.

## THE RULES
- **Firms:** Tradeify Select 50K first, Lucid Flex 50K second. Skip Take Profit Trader at full price.
  Buy only on discount codes — at half price every row above gains half the price (Tradeify +$50, Lucid +$60 per eval).
- **Eval trade:** risk **$400** (MNQ / MGC micros = $400 ÷ stop in $), target **3R = $1,200**. One trade a day.
  A second trade only if the first lost. Win → done for the day. Pass ≈ 3 winning days.
- **Funded trade:** same $400 → $1,200, one a day, request the payout the first day it is allowed.
- **Rotation:** 5 new evals a week, never all 50 at once. Different accounts take different setups / instruments /
  sessions so they do not all win or lose on the same candle (copying one signal to all = all-or-nothing).
- **Bankroll stop:** if 25 evals ($2.5K) are gone with zero payouts, stop and re-check — with only ~1 payout per 9 evals
  that can happen by luck, but it is also the first sign that fees, slippage or the rules are worse than the model.

## What to expect (honest)
Per 50 Tradeify evals ($4,950): about 12 passes, about 5–6 payouts, net expectation about **+$3,300**
— but it rests on 5–6 payouts, so the spread is wide: a few payouts more or less swings the result by thousands. The plan beats the firms' math only by a thin
margin; the margin comes from the payout rules, not from trading. Every result depends on the real eval price,
the real payout rules and costs — re-run `zeroedge` whenever a firm changes them.
