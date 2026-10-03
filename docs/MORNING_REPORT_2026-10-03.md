# Morning report • 2026-10-03

## 1. What your Asian results file says (6.75 years, 1,733 nights)
- **MNQ data was missing.** MNQ had 56 nights (Jan–Mar 2020 = the 03-20 contract only), against 1,733 for MGC. The MNQ contract rollover failed during the load, so every MNQ and BOTH row was measured on about 2 months. Ignore those rows. The math lab now shows a DATA COVERAGE table and warns when this happens.
- **Gold (MGC), real data:**
  - **Plain account:** only 39 of 192 settings made money. SHORT lost in 95 of 96 settings; LONG's median was −$4,330.
  - **Prop results:** positive in 106 of 192 settings, but only because a failed evaluation costs $120 while payouts are kept. The account's day-to-day results were mostly negative; the firm's structure turned them into profit.
  - **Years:** most of the profit came in 2024–2026, when gold went from about $2,000 to $4,000. LONG gold worked because gold went up.
  - **Best direction by year:** SHORT in 2020–21, LONG in 2022–25, SHORT in 2026.
  - **Walk-forward (re-chosen each year from the year before):** +$9,782 over 2021–2026, positive in 4 of 6 years.
  - **Strongest row:** MGC LONG, 1 micro, leg $100, 6 reversals, TP $200 (prop positive in all 7 years, plain +$7,789, 30 payouts). But it is long-only gold during a gold bull market.
- **Verdict: Asian 75 is NOT proven.** It is mostly "long gold", wrapped in a reversal ladder.
- **New idea, now built:** TREND rows choose each night's direction from gold's trend (above or below the last N nights' average). If the trend version holds up in the walk-forward, the strategy adapts by itself.

## 2. Built overnight (builds 10-03p → 10-03t)
- **ASIAN MATH LAB:**
  - ADAPTS EACH YEAR? (walk-forward), TREND direction rows, data coverage check.
  - Your Step 1 setup is marked "YOUR SETUP • rank X of N".
  - REVERSALS × TAKE PROFIT grid, and APPLY TO LAB with no reload.
- **5M FVG MATH LAB:** the first 5M FVG entries (touch, 25%, 50%, green close + break, prior FVG, first BH) × targets × stops × contracts. MNQ 09:30 and MGC 08:00 tested separately. Your targets (MNQ 100, gold 10) are always included.
- **123 ENGULFING MATH LAB:** every timeframe, BUY / SELL / BOTH, signal strength, run length, targets × stops × contracts.
- **FLIP (LIVE ACCOUNT) tab** in the FVG and Engulfing labs: START → GOAL, risk % or fixed micros, a replay of all years, and the odds of reaching the goal before busting.
- **Fixes:**
  - MNQ / MGC / BOTH views no longer change with the click order.
  - The evidence HTML export works.
  - The chart shows a night box (legs, peak, low, combined).
  - Settings that don't apply to Asian are hidden.

## 3. What to run (in this order), then send me the exports
1. **Fix MNQ first:** Step 1 → ASIAN 75, instruments **MNQ**, 2020-01-01 → yesterday, START.
   - In the math lab, ADVICE → DATA COVERAGE should show about 250 nights per year.
   - If it shows only 2020, the MNQ rollover is broken in NinjaTrader. Check Tools → Instruments → MNQ → Rollovers, or open a MNQ chart and click REFRESH OPEN CHART INSTRUMENTS. Send me a screenshot of the status line.
2. **ASIAN 75**, BOTH, 2020 → yesterday. The math lab runs; check ADAPTS EACH YEAR?, then EXPORT.
3. **FIRST 5M FVG STUDY**, BOTH, 2020 → yesterday. The 5M FVG MATH LAB runs; EXPORT.
4. **123 ENGULFING**, BOTH, 2020 → yesterday. The ENGULFING MATH LAB runs; EXPORT.

**What counts as proven:** plain profit in the early 70% AND the unseen 30%, prop positive in every year, AND the walk-forward positive in most years. That is the rule before any money moves.

## 4. Your $17K (my recommendation; you decide)
Nothing is proven yet, so no trading money moves until a lab shows a STRONGEST row and a positive walk-forward on the full 2020 → today data.

- **Keep $10K+ untouched** as the reserve until something is proven.
- **Prop:** once a strategy is proven, start with a fixed evaluation budget of about $1,000–1,500. Spend only from that budget and add to it only from payouts. The PROP PLANNER / EVALS tabs show how many failed evaluations in a row to expect.
- **Live flip:** only with a row whose FLIP tab shows more reaches than busts across all years. Start with $500, and never top up more than 2–3 times.
- **Agency with your brother** (local websites + marketing at about $290/month): the lowest-risk use of money and time. It needs effort more than capital; $500–1,000 covers tools and outreach. Ten clients would be about $2,900 a month of steady income, which also funds the trading.
- **Dropshipping with FB ads:** high risk. If you try it, cap it at about $1,000 with a hard stop rule: no profitable product after that budget → stop.

## 5. Automation
- **Prop firms that ban bots** treat software clicking the buttons as automation too. It risks the accounts and the payouts, so I won't place or click trades on them.
- **What I can do:** analyze live positions against history, size risk, and alert you. You place the trades yourself.
