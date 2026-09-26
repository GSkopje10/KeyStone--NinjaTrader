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
- [ ] Chart is the main, biggest element: replay controls / info move into a compact side
      panel or overlay; nothing pushes the chart down
- [ ] TradingView-style navigation: drag to pan, wheel zoom, drag the time axis to zoom
      horizontally, drag the price axis to scale vertically, time axis always visible,
      double-click to reset
- [ ] Bar-by-bar replay: candles appear one minute at a time (future hidden), speed choice,
      play / pause / step bar / jump to next event; not just event-to-event jumps
- [ ] Live replay dashboard box: open legs, unrealized + realized P/L per instrument, combined
      P/L vs target and loss limit (progress bars), worst so far — updating every candle
- [ ] Clear chart labels: instrument exit ("MNQ exit +$92") separate from the cycle result
      ("CYCLE +$367 ≥ TARGET $350"); no overlapping text
- [ ] Use the BH 5M chart (small W/L badges, clean axes, easy zoom/drag) as the style template
      for the Asian chart; details go in the side panel, not on the candles
- [ ] Later / optional: a replay for BH setups
- [ ] Session-end timestamp for every Asian cycle (which session it ended in) in report

## B. Pool settings — fast testing
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
- [ ] Firm cap optional, OFF by default: max evaluations per firm (10), max funded per firm (5),
      extras benched until a funded account blows; separate eval / funded account names
- [x] Account cost charged for direct-funded accounts too (26d)
- [ ] Replacement delay: a blown account's new evaluation starts trading after N days
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
- [ ] Total investment (initial + replacements), gross payouts, cash after split, net
- [ ] First payout date + account, days / sessions to first payout, money spent until then
- [ ] Was the initial investment enough to get paid without extra money? date, account
- [ ] First profitable date (payout cash ≥ all money spent)
- [ ] Longest losing streak (nights, $), max drawdown, best / worst day, longest-lasting
      accounts, biggest payout streaks
- [ ] Asian: accounts copied, P/L per account, reversal legs per instrument, highest profit peak,
      deepest drawdown
- [ ] Daily / weekly / monthly browsing with filters, numbered cards (date visible when scrolling)
- [ ] Lifecycle walkthrough that actually walks through each day / week / month per account
- [ ] Consolidate tabs; no duplicate boxes; nothing cut off on one-day tests
- [ ] Prop firm / account colors and names for easy reading
- [x] Fix: daily records showed TRADES 0 • W/L 0/0, start balance and cost wrong (26e)
- [ ] BLOWN box = real blowup count (per copied account and total events), not "terminal slots"
- [ ] Pool dashboard account card: first AND latest payout, payout count, current status
      (still funded / eval / blown), current balance and carry-over balance after payout + split
- [ ] Remove the small yellow/blue/white text blocks at the bottom of tabs; everything in
      colored boxes
- [ ] Performance periods per day/week/month: profit after split per account, number of
      accounts, total to bank, cost to date, net after all costs, carry-over balances
- [ ] Clear answer box: "spent $X initial → first payout date → profitable date → extra money
      needed after that (replacements) and whether it was ever paid back"
- [ ] Research findings specific to the strategy (no BH boilerplate on Asian); win rate per
      night, not per leg

## D. Report
- [ ] Web-app style report: professional design, filters, all data (everything that is too busy
      for the lab screens)
- [ ] AI Analyze section: summary of what matters, suggestions for better parameters
- [ ] Deeper automatic back-testing ideas (parameters, entries, risk, payout cycles)

## E. BH
- [ ] Per day: which session and timeframe would have been best (comparison filter)
- [ ] Best timeframe across the range for strategies where timeframe applies

## F. Optimizer
- [x] Asian optimizer on loaded data (26a)
- [ ] Run on the full history and review results together
