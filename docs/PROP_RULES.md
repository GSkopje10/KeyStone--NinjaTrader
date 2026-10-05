# Prop firm rules used by the research (read 2026-10-04)

Source: support.lucidtrading.com (Lucid help centre) + the uploaded Lucid FAQ (`Lucid FAQ/`).
Other firms (MFFU, Tradeify, Take Profit Trader, FundedSeat) run close variants of these models; the
research tests the models as parameter sets, so a firm = one row of numbers.

## Lucid — all plans
- Flat by 16:45 ET, no overnight, no hedging (also across accounts), copiers allowed, news allowed
  (except LucidDaily funded: flat 1 min before → 1 min after USD high-impact news, hard breach).
- Max 10 evaluations / 5 funded per household (10 combined). Split 90/10. Min payout $500.
- MLL trails up to Initial Trail Balance, then locks at start + $100.

| Size | Target | MLL | Trail locks at | Locked MLL | Max size |
|---|---|---|---|---|---|
| 25K | 1,250 | 1,000 | 26,100 | 25,100 | 2 mini / 20 micro |
| 50K | 3,000 | 2,000 | 52,100 | 50,100 | 4 / 40 |
| 100K | 6,000 | 3,000 | 103,100 | 100,100 | 6 / 60 |
| 150K | 9,000 | 4,500 | 154,600 | 150,100 | 10 / 100 |

### LucidFlex (EOD drawdown)
- Eval: 50% consistency (pass in 2 days), no DLL unless chosen.
- Funded: no consistency, no buffer, optional DLL, scaling plan (25K: 1 mini/10 micro until +$1,000;
  50K: 2/20 → 3/30 at +1,000 → 4/40 at +2,000).
- Payout: 5 days ≥ $100 / $150 / $200 / $250 (25/50/100/150K) + net positive cycle; 50% of profit up to
  $1,000 / $2,000 / $2,500 / $3,000; max 5 payouts, then moved live. After a payout the MLL locks at start + $100.

### LucidPro (EOD drawdown)
- Eval: DLL 50K $1,200 / 100K $1,800 / 150K $2,700 (25K none); can pass in 1 day.
- Funded: optional fixed DLL (600/1,200/1,800/2,700) or scaling DLL 60% of peak EOD; no scaling plan.
- Payout: min profit goal 250/500/750/1,000 per cycle, best day ≤ 40% of cycle profit, profit above buffer (MLL + $100).

### LucidDirect (no evaluation)
- MLL 1,000 / 2,000 / 3,500 / 5,000; DLL none / 1,200 / 2,100 / 3,000.
- Payout: best day ≤ 20%; profit goal 1: 1,500/3,000/6,000/9,000, goal 2+: 1,250/2,500/3,500/4,500;
  caps payouts 1–3: 1,000/2,000/2,500/3,000, 4–5: 1,000/2,500/3,000/3,500.

### LucidDaily (daily payouts)
- Eval: like Flex (50% consistency), EOD or intraday drawdown (intraday = cheaper).
- Funded: INTRADAY trailing drawdown, no consistency, payouts any day once above buffer (start + MLL + 100)
  and net positive since the last payout; max sim profit a day 6K/8K/10K/12K (then moved live). News rule above.

Prices are not on the help centre (pricing page blocked by Cloudflare): the research uses an editable
evaluation price and reports results for a range of prices.

## Daily close — when each firm closes open positions (checked 2026-10-05)

All times New York (ET). Every firm opens again at 18:00 ET (Globex). No firm here allows overnight or weekend holds.
On **holiday early closes** the firms below do NOT auto-close: be flat yourself before the early close or the account is breached.

| Firm | Flat by (ET) | What happens to an open position | Source |
|---|---|---|---|
| MyFundedFutures (all plans) | **16:10** | Auto-closed at 16:10 on normal days. Holiday early close: not auto-closed → breach. Do not keep re-sending orders after 16:10 (a fill can disqualify the account). | [MFFU help: Permitted Times to Trade](https://help.myfundedfutures.com/en/articles/9558251-permitted-times-to-trade) |
| Lucid (Pro / Flex / Daily / Direct) | **16:45** | Auto-flattened at 16:45, not a breach (you get whatever fill is there). | [tradetanto: Lucid rules](https://tradetanto.com/learn/lucid-trading-rules-explained-every-plan-rule-and-limit), [propvator: Lucid hours](https://propvator.com/blog/lucid-trading-trading-hours/) |
| Take Profit Trader | **16:55** (day ends 17:00) | Auto-closed at 16:55. Holiday sessions: not auto-closed. A product that closes before 17:00 must be flat before its own close or the account is liquidated. | [TPT Rule 4: Approved Products, Approved Hours](https://takeprofittraderhelp.zendesk.com/hc/en-us/articles/15170347090461-Rule-4-Trade-Approved-Products-During-Approved-Hours) |
| Topstep | **16:10** (15:10 CT) or the product's close if sooner | Must be flat (third-party summary — verify on Topstep before trading it). | [h2tfunding: Topstep close time](https://h2tfunding.com/topstep-trades-closed-by-what-time/) |
| Apex Trader Funding | **16:59** | Your responsibility to be flat (third-party summary — verify). | [Apex: Futures Trading Times](https://apextraderfunding.com/help-center/getting-started/futures-trading-times/) |
| Tradeify | **16:59** | Hard stop; exit by ~16:55 with a market order (third-party summary — verify). | [Tradeify: end of day guide](https://tradeify.co/post/end-of-day-trading-strategy-guide-futures-prop-firm-traders) |

In the app: the firm presets carry `FlatBy` (MFFU 16:10, Lucid 16:45, TPT 16:55). LIVE TRADING shows
"FLAT BY hh:mm NY • n min left" in the TODAY panel from 20 minutes before, plays a sound and shows the rule at
5 minutes when a position is open. It is a warning only — nothing is closed by the app.
