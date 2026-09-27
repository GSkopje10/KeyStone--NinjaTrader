# Keystone — requested features checklist

Every request from the user, in one place. `[x]` = shipped (build label noted), `[ ]` = open.
Future strategies must get the same data, screens and reports; only entries, risk management
and chart labels differ per strategy.

## A. Evidence chart — see exactly what happened
- [x] Leg labels, stop lines, entry→exit lines, combined night banner (26c)
- [x] **Replay**: START / PREV / NEXT / END + PLAY / PAUSE through the night (26e)
- [x] Replay box at every step: what happened (entry / stop + reversal / target / loss limit /
      session end), contracts, prices, BOTH instruments' state at that moment, combined P/L,
      worst drawdown so far (26e)
- [x] Cursor + highlight on the chart where each event happened; MNQ and MGC tabs (26e)
- [x] Compact labels on multi-leg nights; full detail in the replay box (26e) — review in NinjaTrader
- [x] Chart is the main, biggest element: replay controls / info move into a compact side
      panel or overlay; nothing pushes the chart down
- [~] TradingView-style navigation (drag / wheel / axis drag existed; axis no longer cut off; double-click reset still to add): drag to pan, wheel zoom, drag the time axis to zoom
      horizontally, drag the price axis to scale vertically, time axis always visible,
      double-click to reset
- [x] Bar-by-bar replay: candles appear one minute at a time (future hidden), speed choice,
      play / pause / step bar / jump to next event; not just event-to-event jumps
- [x] Live replay dashboard box: open legs, unrealized + realized P/L per instrument, combined
      P/L vs target and loss limit (progress bars), worst so far — updating every candle
- [x] Clear chart labels: instrument exit ("MNQ exit +$92") separate from the cycle result
      ("CYCLE +$367 ≥ TARGET $350"); no overlapping text
- [ ] Use the BH 5M chart (small W/L badges, clean axes, easy zoom/drag) as the style template
      for the Asian chart; details go in the side panel, not on the candles
- [ ] Later / optional: a replay for BH setups
- [ ] Session-end timestamp for every Asian cycle (which session it ended in) in report

## B. Pool settings — fast testing
- [x] INSTRUMENT switch on the results screen (MNQ / MGC / BOTH) for every strategy: re-runs
      the pool with the same rules without going back to Configure; day stepping, charts and
      boxes show the selected instrument(s). BOTH on BH: each setup goes to the next available
      account (MNQ and MGC setups share the rotation). Both instruments are loaded once so
      switching is instant; if an instrument was never loaded the lab loads it first
- [x] PREV / NEXT day buttons in Pool Settings (26e): instantly show that day's result without going
      back to Configure; OPEN CHART jumps to that day
- [x] "Setups only" checkbox: win/loss per day, totals, streaks, month-by-month (26e)
- [x] Parameter names readable (wider rows, wrapping checkbox text, clearer names) (26e)
- [x] Asian evaluation uses Step 1 Asian settings unless ASIAN EVAL CYCLE is ticked; BH-only
      boxes hidden for Asian (visibility bug fixed) (26e)
- [ ] One clear investment policy: only the initial investment until the first payout, then
      replacements from payout cash; always report whether the initial investment alone was
      enough or how much extra money was needed
- [x] Prop-firm style evaluation: consistency %, MIN TRADING DAYS with $0-risk minimal-trade
      days after the target (26e)
- [x] Firm cap optional, OFF by default: max evaluations per firm (10), max funded per firm (5),
      extras benched until a funded account blows; separate eval / funded account names
- [x] Account cost charged for direct-funded accounts too (26d)
- [x] Replacement delay: a blown account's new evaluation starts trading after N days
      (default 2) instead of the next session
- [ ] Option: evaluation passed with another strategy / assumed passed (cost + days), funded
      stage traded with the selected strategy
- [x] Blown account → buy new evaluation (default) or end slot; direct-funded replaced by eval (26d)
- [x] Asian evaluations can pass (qualifying day no longer uses the BH $1,500 lock) (26d)
- [x] Asian evaluation-stage cycle option (26d)
- [x] Pool Settings opens first (26d)
- [x] NEW TEST resets every setting on every tab (26e)
- [ ] Investment report: initial investment enough? extra money needed? (setting renamed "ONLY
      SPEND THE INITIAL INVESTMENT"; results summary still to do)

## C. Results — what the numbers must answer (main page, not buried)
- [x] Total investment (initial + replacements), gross payouts, cash after split, net
- [x] First payout date + account, days / sessions to first payout, money spent until then
- [x] Was the initial investment enough to get paid without extra money? date, account
- [x] First profitable date (payout cash ≥ all money spent)
- [x] Longest losing streak (nights, $), max drawdown, best / worst day, longest-lasting
      accounts, biggest payout streaks
- [x] Asian: accounts copied, P/L per account, reversal legs per instrument, highest profit peak,
      deepest drawdown
- [ ] Daily / weekly / monthly browsing with filters, numbered cards (date visible when scrolling)
- [ ] Lifecycle walkthrough that actually walks through each day / week / month per account
- [x] The 4 top boxes change with the selected results tab and show that tab's key numbers
      (e.g. First Return: paid accounts, earliest first payout, average days to first payout,
      cost through first return); the general summary lives only on the main tab
- [x] Payout Cycles tab redesign: the payout-account list and its detail are cramped (tiny
      nested scroll boxes). Full-height account list as colored cards, detail as boxes, one
      scroll per panel; same look and theme for every strategy
- [ ] Consolidate tabs; no duplicate boxes; nothing cut off on one-day tests
- [ ] Prop firm / account colors and names for easy reading
- [x] Fix: daily records showed TRADES 0 • W/L 0/0, start balance and cost wrong (26e)
- [x] BLOWN box = real blowup count (per copied account and total events), not "terminal slots"
- [x] Pool dashboard account card: first AND latest payout, payout count, current status
      (still funded / eval / blown), current balance and carry-over balance after payout + split
- [x] Remove the small yellow/blue/white text blocks at the bottom of tabs; everything in
      colored boxes
- [x] Performance periods per day/week/month: profit after split per account, number of
      accounts, total to bank, cost to date, net after all costs, carry-over balances
- [x] Clear answer box: "spent $X initial → first payout date → profitable date → extra money
      needed after that (replacements) and whether it was ever paid back"
- [ ] Research findings specific to the strategy (no BH boilerplate on Asian); win rate per
      night, not per leg

## D. Report
- [x] Web-app style report: professional design, filters, all data (everything that is too busy
      for the lab screens)
- [x] AI Analyze section: summary of what matters, suggestions for better parameters
- [ ] Deeper automatic back-testing ideas (parameters, entries, risk, payout cycles)

## E. BH
- [ ] Per day: which session and timeframe would have been best (comparison filter)
- [ ] Best timeframe across the range for strategies where timeframe applies

## F. Optimizer
- [x] Asian optimizer on loaded data (26a)
- [x] EXPORT FOR CLAUDE (all strategies): one compressed file with the loaded MNQ + MGC 1-minute bars
      and the current settings/results; user uploads it to the repo (github.com → Add file →
      Upload files, data/ folder); Claude runs the full optimizer and reports the best way to
      run the strategy (in-sample / out-of-sample, per year, per month, prop-rule fit)
- [ ] Run on the full history and review results together

## G. New strategies
- [x] FVG retest long (spec: `docs/FVG.md`) (26f; replay still to come) — all timeframes, MNQ + MGC, zone boxes on chart,
      replay, full pool / payout / report / optimizer
- [ ] 123 bullish engulfing — waiting for the user's explanation

## H. Build 26f notes (2026-09-27)
- [x] Loading: saved-data reuse, watchdog with elapsed time and time limit, deferred heavy tabs
- [x] Themes: OBSIDIAN GOLD (default), CLASSIC, DEEP OCEAN, GRAPHITE, ROYAL EMERALD
- [x] Tests: full-file compile, UI smoke test (WPF one-parent rule), report preview, BH and
      lifecycle baselines
- [ ] Next: BH best session / timeframe per day (E), 123 bullish engulfing (G), lifecycle
      walkthrough per day/week/month (C), prop firm colours / names (C)

## I. Build 27e (2026-09-27)
- [x] FVG: double trouble grade, stacked zones, why-no-entry log, points by grade
- [x] Pool: copy to groups of N, COMPARE ACCOUNTS (★ best), WHY NO PAYOUT
- [x] Pool Settings in EVALUATION / FUNDED / AFTER A BLOWUP blocks; Asian rows only for Asian
- [x] Period cards: TRADES P/L THIS DAY, NET SINCE START, PROFIT STILL IN ACCOUNTS, hover help, session exits
- [x] Colored account cards (stage band, balance, progress to target, chips)
- [x] APPLY THEME live (no restart)
- [x] Chart = ledger (same contract; other timeframes from loaded 1M)
- [x] Other strategies on the chart + day compare + COMPARE STRATEGIES
- [x] Report + AI: OPTIMAL BEST ENTRIES FOR PROP, strategy / account comparisons, payout diagnosis
- [ ] Next: BH best session / timeframe per day (E), 123 bullish engulfing (G), lifecycle
      walkthrough per day/week/month (C), prop firm colours / names (C)

## J. Build 27h (2026-09-27)
- [x] DIRECT FUNDED block (account cost, replacement cost, days until funded again); no evaluation
      stage in direct mode; evaluation settings only shown for EVALUATION FIRST
- [x] First Return redesign (summary, days-to-first-payout chart, sortable account cards)
- [x] Payout Cycles: WHAT MATTERS strip + one compact row per payout date
- [x] Asian: no grade / stop-target suggestions, strategy names fixed in findings and chart

## K. Build 27i (2026-09-27)
- [x] 123 ENGULFING buy / sell strategy (docs/ENGULFING.md) with all pool / payout / report / chart features
- [x] Prop NO HEDGING rule for every strategy (same instrument never long and short at once)
- [x] Chart: always-visible TIMEFRAME buttons; SHOW ENGULFING overlay; COMPARE STRATEGIES includes ENGULFING
