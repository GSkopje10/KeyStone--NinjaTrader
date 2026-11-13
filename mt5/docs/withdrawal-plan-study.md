# 💸 The withdrawal plan: $5k → withdraw at $10k → back to $5k, blow-up → a new $5k (6 years)

Research only. No app code changed, nothing built or shipped.

> **ESTIMATE — futures 1-minute candles with synthetic ticks, not the broker's XAUUSD ticks.**
> The bench runs on `data/MGC` (Micro Gold futures, 1-minute candles, Jun 2020 – 2 Oct 2026, Aug–Sep 2020 missing) used as XAUUSD, with ticks made up inside each candle.
> Costs: a fixed spread of 0.25, the swap assumption of the earlier studies (long −62.4 / short +28.1 points per lot per night, triple on Wednesday), no commission.
> The bench is chaotic: one different close shifts every later basket. Trust the counts (blow-ups, hard stops) and the sign, not the exact dollars.

> **⚠ Numbers produced before the 13q fix are INVALID.** Before SpasiX 13q (commit 8878b4a), a 🔐 BREAKEVEN PROFIT lock at net 0 with LOCK / RISK GUARD off was never
> unlocked: baskets froze for months and the bench looked far better than the real engine (e.g. iter23-study.md's "as-is −$110k / 14 blow-ups"). Every number below
> comes from the fixed engine (HEAD after 8878b4a). A first run of this study on the old engine was thrown away.

## The plan, as simulated

* Start $5,000. Settings: the user's exported DEFENDER (`exp1113/01-settings.txt`), as they now run after 13q:
  * fixed size as exported (`sz1k=0`): entry 0.1 lot, add-on 0.14, STEPLEAD hedges, target $50, scale out, compress ALWAYS, exit guard, rescue, danger exit,
    HARD STOP at −60 % of the balance at the iteration's start, HARD MARGIN 60 %;
  * **(A) 🔐 BREAKEVEN PROFIT = CLOSE ALL, 🛡 RISK GUARD off** (`beclose=1`, `rg=0`) — the user's settings now;
  * **(B) the same + 🛡 RISK GUARD on** (LOCK, lead ≤ 3 — the 13p recommendation);
  * **(C) reference: 🔐 BREAKEVEN PROFIT = LOCK** (the old choice, `beclose=0`), RISK GUARD off.
* **Withdrawal:** the first time the **balance ≥ $10,000 with no position open**, everything above $5,000 is withdrawn (a balance deal) and the account goes on at $5,000.
  Variant **+50 %:** the same at $7,500. (The "withdraw with a basket open" variant was started but not finished — see the end.)
* **Blow-up:** the app's 💥 AUTO RESTART rule — the automation stopped for no money / margin / a stop-out, or flat with the balance under 10 % of $5,000 (= $500).
  The money left in the blown account goes back to the user, a fresh $5,000 is deposited.
  In practice a blow-up is **3–4 HARD STOPs in a row**: $5,000 → ≈ $2,000 → ≈ $800 → ≈ $330 (each HARD STOP takes 60 % of what is there).
* NET CASH = withdrawn + the money recovered from blown accounts − deposited. "+ account" = NET CASH + the equity still in the account at the end (2 Oct 2026).
* Harness: `mt5/study/WithdrawBench.cs.txt` (the app's own `HedgeAuto` on the BACKTEST `SimBroker`, `Step` + `GuardStep` + `ProtectStep` per tick, as `Iter23Bench`),
  aggregation `mt5/study/WithdrawAgg.py.txt`. One continuous run per line (not one fresh account per year as in the earlier studies).

## Headline (Jun 2020 – Oct 2026, withdraw at $10k)

| | (A) user's settings: CLOSE ALL, RG off | (B) CLOSE ALL + 🛡 RISK GUARD | (C) LOCK, RG off (reference) ¹ |
|---|---|---|---|
| deposits ($5k each) | **86** = $430,000 (1 + **85 blow-ups**) | **10** = $50,000 (1 + 9 blow-ups) | 92 = $460,000 (1 + 91 blow-ups) |
| withdrawals | **15** = $75,316 | 5 = $25,096 | 9 = $45,273 |
| recovered from blown accounts | $29,673 | $2,934 | $28,866 |
| **NET CASH** | **−$325,011** | **−$21,970** | −$385,861 |
| in the account at the end | $1,313 | $2,417 (a basket open) | ≈ $3,391 |
| NET CASH + account | **−$323,698** | **−$19,553** | ≈ −$382,470 |
| cycles ending in a withdrawal vs a blow-up | 15 vs 85 | 5 vs 9 | 9 vs 91 |
| HARD STOPs / iterations | 310 / 10,717 | 31 / 1,335 | 321 / — |
| time to reach $10k (withdrawal cycles): median / worst | 11 d / 102 d | 57 d / 79 d | — |
| time to blow (blow-up cycles): median / shortest | 6 d / < 1 d (7 blew within a day) | 203 d / 8 d | — |
| longest stretch without a withdrawal | 1,062 days (Feb 2022 → Jan 2025) | 1,969 days (Jun 2020 → Oct 2025) | — |
| first withdrawal | 15 Jan 2021 | 22 Oct 2025 | — |

¹ (C) stopped at 1 Oct 2026 (the data ends 2 Oct) when the run was cut for budget; it is complete in all but the last day.

**In plain words:** with the user's settings the plan withdraws $75k over six years but needs **85 new $5k deposits** to do it. For every withdrawal there are about
**6 blow-ups**. A cycle that ends in a withdrawal doubles fast (median 11 days), but most cycles never get there: the median blown account lasts 6 days.

### Worst drawdown inside a cycle (% of the $5,000 start)

* Every blow-up cycle, by definition, ends ≥ 90 % down. With (A) some went below $0 (equity −$700 at the worst, "114 %").
* **Even the cycles that DID reach $10k went deep first:** (A) the worst one fell to $1,724 (−66 % of $5k) and had a peak-to-trough of $7,050 (141 % of $5k) before it made it.
  (B) the worst withdrawal cycle bottomed at $2,051 (−59 %), peak-to-trough 76 % of $5k.

## Per year — (A) the user's settings (CLOSE ALL, RG off), withdraw at $10k

| year | withdrawals | blow-ups (= new deposits) | NET CASH that year | HARD STOPs | iterations | equity at year end |
|---|---|---|---|---|---|---|
| 2020 (Jun–Dec) | 0 | 1 | −$9,593 | 0 | 132 | $7,777 |
| 2021 | 1 ($5,030) | 5 | −$18,047 | 19 | 471 | $3,110 |
| 2022 | 1 ($5,041) | 4 | −$13,355 | 15 | 563 | $654 |
| 2023 | 0 | 6 | −$27,574 | 18 | 454 | $1,349 |
| 2024 | 0 | 8 | −$38,001 | 25 | 970 | $6,007 |
| 2025 | 6 ($30,115) | 21 | −$67,652 | 79 | 3,006 | $6,486 |
| 2026 (to 2 Oct) | 7 ($35,130) | 40 | −$150,789 | 154 | 5,121 | $1,313 |

(2020's first deposit is counted in 2020's NET CASH.)

## Per year — (B) CLOSE ALL + 🛡 RISK GUARD, withdraw at $10k

| year | withdrawals | blow-ups | NET CASH that year | HARD STOPs | iterations | equity at year end |
|---|---|---|---|---|---|---|
| 2020 | 0 | 1 | −$9,663 | 3 | 13 | $4,175 |
| 2021 | 0 | 1 | −$4,628 | 3 | 37 | $4,485 |
| 2022 | 0 | 0 | $0 | 0 | 14 | $3,439 |
| 2023 | 0 | 1 | −$4,721 | 4 | 58 | $4,658 |
| 2024 | 0 | 1 | −$4,703 | 3 | 115 | $5,503 |
| 2025 | 1 ($5,023) | 2 | −$4,258 | 7 | 387 | $3,913 |
| 2026 | 4 ($20,074) | 3 | **+$6,003** | 11 | 711 | $2,417 |

RISK GUARD trades ~8 × less (1,335 iterations vs 10,717): its LOCKs park baskets for weeks / months, so it blows ~9 × less often but also rarely reaches $10k
(no withdrawal at all for the first 5 years). 2026 is its only positive year.

## Sensitivity

### Withdraw at +50 % ($7.5k → back to $5k) vs +100 %

| | withdrawals | blow-ups | withdrawn | NET CASH | + account |
|---|---|---|---|---|---|
| (A) +100 % | 15 | 85 | $75,316 | −$325,011 | −$323,698 |
| (A) +50 % | 36 | 96 | $94,081 | −$356,518 | −$355,209 |
| (B) RG +100 % | 5 | 9 | $25,096 | −$21,970 | −$19,553 |
| (B) RG +50 % | 11 | 11 | $27,733 | −$28,685 | −$26,067 |

Taking money out earlier gives more, smaller withdrawals but does not change the outcome: the blow-ups keep coming, and the account restarted at $5k after
each withdrawal is as exposed as before. +50 % is slightly worse in both setups (within the noise).

### Start date (luck) — withdraw at $10k, each run to Oct 2026

| start | (A) CLOSE ALL, RG off: deposits / withdrawals / NET CASH | (B) + RISK GUARD: deposits / withdrawals / NET CASH (+ account) |
|---|---|---|
| Jun 2020 | 86 / 15 / −$325,011 | 10 / 5 / −$21,970 (−$19,553) |
| Jan 2024 | 67 / 12 / −$252,313 | 7 / 2 / −$22,830 (−$20,043) |
| Apr 2025 | 58 / 10 / −$219,037 | 4 / 4 / **+$1,165 (+$4,069)** |
| Jan 2021, Jul 2022 | not finished (budget cut) | not finished (budget cut) |

* (A) loses heavily from every start date. Even starting in Apr 2025 (the strongest gold trend in the data) it needs 58 deposits in 18 months.
* (B) is luck-dependent: started in Apr 2025 it ends slightly positive (+$1.2k cash, +$4.1k with the account); started in 2020 or 2024 it is −$20k.
  One lucky start is not an edge.

### Not completed (budget cut — no numbers)

* withdraw with a basket open (only the balance above $5k leaves; equity and balance ≥ $10k) — the harness models it (`OPEN`), the run was stopped in 2021;
* start dates Jan 2021 and Jul 2022 (both setups);
* (C) LOCK with +50 %; 💰 sizing per $1,000 (`P1K`, 0.01 lot per $1k — the DEFENDER preset's sizing; the export has fixed 0.1 lot);
* a stricter "blown = under $2,500" rule (`BL50`).

## The user's live DEMO so far (context only — separate from the bench)

* IC Markets DEMO 5298760: started at $5,000 on 5 Nov 2026 (export of 5 Nov). Since then the settings changed several times (entry 0.25 → 0.5 → 0.1 lot,
  several builds), there were manual interventions (the WHAT IF says closing #83 by hand saved ≈ $4,700 that the automation would have lost at the HARD STOP),
  and the exports show balance jumps that are not trading (5,322 ↔ 7,679 between exports of 9 Nov: balance set / histories merged). So there is no clean
  "$5k with these settings" path to compare with.
* The latest export (BUILD 13o): 11 Nov 23:37 **$6,722** → 13 Nov 03:22 **$7,175** (+$453 in 23 iterations, 22 won, worst basket −$1,460 = 20 % of the balance).
* 8 days of a demo with manual help say nothing about the tail the bench shows: in the bench the losses come from the rare deep baskets
  (3–4 HARD STOPs per blow-up), and on the demo one of them (#83) was stopped by hand.

## Verdict (plain words)

* **The plan does not turn a profit in this bench with the user's settings.** Withdrawing at $10k works as a mechanism — it happened 15 times — but the account
  blows ~6 times for every withdrawal, so over 6 years about $430k is deposited to take out $75k (net ≈ −$325k).
* Withdrawing earlier (+50 %) does not help.
* **🛡 RISK GUARD is the only thing that changes the picture**: ≈ −$20k instead of −$325k, 9 blow-ups instead of 85 — but it is still a loss over 6 years, it
  waits years for its first withdrawal, and its one positive start (Apr 2025) is luck.
* With a fixed 0.1 lot on a $5k account the HARD STOP (−60 %) is the real blow-up mechanism: each deep basket takes 60 % of what is left.
  Smaller size per $1,000 (the DEFENDER preset's 0.01 lot per $1k = 0.05 lot at $5k) was not measured here (budget cut).
* Again: these are estimates on futures candles with synthetic ticks, not the broker's XAUUSD ticks; the counts are the robust part.
