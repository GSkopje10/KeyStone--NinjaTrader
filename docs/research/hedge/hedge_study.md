# Hedge instead of a stop: study (2026-10-28)

## The strategy as tested

- Open **0.1** (or **0.25**) on the break of the last N one-minute candles' high (BUY) or low (SELL).
- When the **whole basket** is up **+$20** (net of commission, spread and swap), close everything and open the same direction again at once.
- **No stop.** When the price breaks the recent **low** after a buy, open the other side with **0.2** (0.5). When it then breaks the recent **high**, open **0.2** the other way again, and so on, until the basket is +$20.
- **A:** $350, 0.1 / 0.2, $20 a basket, all day, every day.
- **B:** $5,000, 0.25 / 0.5, $20 a basket, stop for the day at +$100 banked.
- **Variations:** recent high / low of 5, 10, 20, 30, 60 (sweep: up to 240) candles; target $10 to $200; minimum hedge distance $0 to $8; max legs 3 or 5 (then the basket closes at a loss); doubling hedges (martingale); waiting for a new break instead of re-entering at once; smaller lots.

## Data and costs

- **Data:** `data/MGC` 1-minute gold futures as a stand-in for MT5's **XAUUSD**, 6 Jan 2025 to 2 Oct 2026 (612,306 candles). That is $1 per 0.01 per 1 lot, the same as XAUUSD's 100 oz.
- **Costs and margin (IC Markets Raw-like):**
  - 0.20 spread, $7 a lot round turn, 5 points of slippage on every entry and hedge
  - swap −$25 a lot a night
  - 1:500 leverage, hedged margin 50 %, stop-out at 50 % margin level
  - MT5 refuses a hedge the free margin cannot carry
- Every setting was also run with every gap at a session open closed (futures rolls and weekend gaps removed). The results hardly change.

## Results: every variation blows the account

Straight through from 6 Jan 2025:

| setting | result | blown |
|---|---|---|
| A, recent 5 / 10 / 30 / 60 candles | −$245 to −$312 | **6–7 Jan 2025 (day 1–2)** |
| A, recent 20 | −$119 | 5 Feb 2025 (13 legs, 2.5 lots open) |
| A, max 3 legs | −$324 | 15 Jan 2025 (10 baskets closed at a loss) |
| A, martingale ×2 | −$220 | 6 Jan 2025 |
| A, 0.01 / 0.02, $2 | −$301 | 25 Feb 2025 |
| B, recent 5 / 10 / 20 / 30 | −$3,644 to −$4,092 | 23 Jan – 27 Feb 2025 (up to 20 legs, 9.75 lots open) |
| B, recent 60 | −$3,856 | 4 Apr 2025 (27 of 70 days hit +$100 first) |
| B, max 3 / 5 legs | −$4,877 / −$4,755 | Jan 2025 |

A fresh account every Monday for 4 weeks (87 windows):

- **A:** blown in **94–100 %** of the windows; the median result is −$235.
- **B:** blown in **32–82 %**; the median is −$400 to −$4,800.

The wide sweep covered 336 settings for A and 252 for B, with lots from 0.01 to 0.25, recent 5 to 240 candles, target $10 to $200 and minimum distance $0 to $8. The best ones on paper failed when run straight through:

- **B, 0.25, recent 240, $200, min $8:** blown 13 Mar 2025, and again on 1 Feb 2026 when run on 2026 alone.
- **B, 0.25, recent 20, $50, min $8:** +$2,282 in Jan–Feb 2025, then blown 7 Apr 2025. On 2026 alone, blown 30 Jul 2026.
- **A, 0.01, recent 120, $8:** survived 2026 (+$2,491, 59 % drawdown), but blown 13 Mar 2025.

## Why it cannot work

- Buy 0.1 → sell 0.2 → buy 0.2 → sell 0.2 … leaves you net **±0.1 the whole time**. In P&L terms, every hedge is exactly "stop out, then reverse". The loss of each whipsaw is locked in, not avoided.
- Compared with an honest stop-and-reverse, every flip pays spread and commission on **0.2 instead of 0.1**. Swap and margin grow with every leg: 0.1 + 0.2 + 0.2 + … is 0.7, 1.1, 1.5 lots open.
- The basket only gets back to +$20 if price later travels (all the locked losses + $20) ÷ $10 per $1 at 0.1 net. In a choppy hour that can be $30 to $100 of gold. Meanwhile the margin of the growing gross blocks the next hedge ("no money"). The basket is then left open and runs to the stop-out.
- **$350 is the hardest case:** 4 legs (0.7 lots) already use about $210 of margin at 1:500. One fast hour (5 to 6 whipsaws of 10 candles) ends the account. On gold in 2025–26 that happens on most days.

## What would have to change

The data says the "+$20 a basket" part works almost all the time. The problem is the rare basket that never comes back. Any version worth building needs:

1. **A real stop on the basket** in $ that is small next to the account (a "BASKET STOP $" of 2–5 % of balance), and
2. **An entry with an edge**, so the stopped baskets are paid for by the winners.

The HEDGE LAB in SpasiX has both brakes (MAX LEGS, BASKET STOP $). Any new idea can be tested there on the broker's own XAUUSD history before it is automated.

## Reproduce

```
cd mt5/study && dotnet run -c Release            # the table above (+ study_results.csv)
dotnet run -c Release -- sweep                   # the wide search + margin sensitivity
dotnet run -c Release -- best                    # the sweep's best, straight through and by year
dotnet run -c Release -- trace                   # every open / close of setting A on 6 Jan 2025
```

---

# Part 2: one target a day, the best session (2026-10-28)

## What was tested

One basket a day:
- open on the break of the last N one-minute candles inside a **New York session**,
- close it at the target, then **done for the day**.

The grid had 640 settings (`dotnet run -c Release -- sessions`, results in `sessions_results.csv`):

| | options |
|---|---|
| **Sessions (NY time)** | all day • Asia 19:00–23:00 • London open 03:00–05:00 • London 03:00–08:00 • NY open 08:30–10:30 • NY 09:30–11:30 • overlap 08:00–12:00 • NY pm 13:00–16:00 |
| **Hedge style** | recent 5 / 10 / 20 / 30 / 60 candles • fixed zone $2 / $4 / $8 |
| **Exit** | hold to target • close at the session end • basket stop 2× / 5× target • close at session end + stop 2× |
| **Accounts** | A: $350, 0.1 / 0.2, $20 • B: $5,000, 0.25 / 0.5, $100 |

Settings were chosen on **2025** and checked on **2026**. The final picks were then checked on **2023 and 2024**, a calmer gold market none of this was chosen on.

## Findings

1. **One target a day still blows up when there is no stop.** "Hold to target" survived 0 of 64 settings in 2025 for both A and B. Closing at the session end alone did not save it either (A 0/64, B 14/64).
2. **A ($350, 0.1 lot) never survived 2025 in any of the 320 settings.** With 0.1 lots, two to five losing baskets are the whole account.
3. **Every B setting that held up in both years was one where the hedge never fired.** The basket stop is hit before the hedge line. For example, "NY open | zone $8 | stop 2×" (legs ≤ 1). The best "hedge strategy" is a plain trade with a stop.
4. **Adding real hedges inside the stop made it worse:**

| B, one target a day, 480 settings each | profitable in both years | blown in either year |
|---|---|---|
| no hedge | 67 | 183 |
| hedge at half the stop | 42 | 296 |

5. **The best session is the NY open, 08:30–10:30 New York** (15:30–17:30 MT5 server time at IC). In the robustness grid it is the only session whose median setting made money in 2025 (+$3,040). Its 2026 median is about break-even (−$132), but almost all of the top settings by the worse of the two years are NY open.
6. **Bigger target, wider stop.** A $5–6 move (B: $125–150) with the stop at 2–3× the target beats small targets. The $2 targets are eaten by spread, commission and slippage.

## The best found (no hedge)

- NY open 08:30–10:30 (NY time), entry on the break of the last **5** one-minute candles' high or low.
- Target a **$6 move**, stop **2×** ($12 move).
- One win and you're done; **max 1 try a day**.

| account | 2023 | 2024 | 2025 | 2026 (to 2 Oct) | max drawdown | worst losing run |
|---|---|---|---|---|---|---|
| $5,000, 0.25 lot (+$150 / −$300) | −$75 | +$1,385 | +$6,002 | +$5,449 | $4,650 (59 %, in 2023) | 5 |
| same, max 2 tries a day | −$312 | +$4,265 | +$8,552 | +$6,049 | $4,260 | 6 |
| $350, 0.02 lot (+$12 / −$24) | −$6 | +$111 | +$480 | +$436 | $372 | 5 |
| $350, 0.05 lot (+$30 / −$60) | **blown** Nov 2023 | +$277 | +$1,200 | +$1,090 | 99 % | 5 |

How to read this:

- **It only works when gold moves.** 2023 is flat. Most of the money is from 2025–26, when gold's one-minute range was 2 to 4 times larger.
- **0.25 lot on $5,000 is too big.** The 2023 drawdown was 59 % of the account. 0.1 lot on $5,000 (about 37 % worst drawdown) is the most this supports.
- **Some neighbouring settings fail where this one held.** "Recent 10, stop 3×" lost $2,107 in 2024 at 1 try and was blown in Oct 2024 at 2 tries. The edge is thin. Treat this as a candidate to forward-test on a demo, not as a proven system.
- **$20 a day on $350 is 5.7 % a day.** None of the 1,200+ variations did that without blowing the account. At $350 the version that survived 2023–26 is 0.02 lot, about $1–2 a day on average.

## Reproduce

```
cd mt5/study && dotnet run -c Release -- sessions   # 640 settings, 2025 → 2026
dotnet run -c Release -- robust                     # NY-open plateau: target × stop × session × entry, hedge vs no hedge
dotnet run -c Release -- detail                     # the picks 2023 → 2026, by year and month, $5,000 and $350, 1 / 2 / unlimited tries
```

In SpasiX: **⇅ HEDGE LAB → C: best found**, then **▶ RUN**. Use **▶ SESSIONS** to compare every session with and without hedges on your own MT5 XAUUSD history.
