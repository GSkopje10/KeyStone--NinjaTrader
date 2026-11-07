# 🧪 EVAL LAB — specification (user's request, 2026-11-07)

A separate KeyStone window for **evaluation accounts only**: backtesting, pools, trackers, control panels,
statistics, advice, and live trading of the real eval accounts. All in one place. A **FUNDED LAB** comes later.
Passed accounts move there automatically, and their eval history stays in the Eval Lab.

The Rotation Studio (BUILD 2026-11-07l…o) is its starting point. The Eval Lab grows out of it and replaces it.

## Navigation (left rail, one click each)
| Section | What it holds |
|---|---|
| 🏠 OVERVIEW | every pool at a glance: accounts per firm, passed / blown / in eval, today's P/L, next account in turn |
| 👥 POOLS | groups of accounts organised by prop firm; one-click rename, firm, size, colour, order; add / remove / archive |
| 🎯 PLANS | strategy templates (the tested best plans + your own): instrument, contracts, take profit $, floor / stop $, breakeven, side rule, session, colours |
| 🔄 ROTATE | the control panel: BACKTEST (instant or replay) or LIVE; AUTO or MANUAL (flashing buttons — nothing happens until you press) |
| 📊 STATS | pass rate, days to pass, fees per funded account, by firm / plan / month; batch history |
| 🧠 ADVISOR | likelihood of +target before the floor for the next trade, from history (similar time, size, conditions); what worked best; warnings |
| 🗂 DATA | date range pickers, saved runs, exports (CSV) for analysis |

## The firm round robin (a hard rule)
Accounts from several prop firms always rotate **firm by firm**: with 6 firms × 5 accounts the order is
Firm1·acc1, Firm2·acc1, … Firm6·acc1, Firm1·acc2, … so no firm sees high-frequency trading from us.
The same order applies in backtest and live. A firm whose accounts are all passed / blown drops out of the cycle.

## Tested defaults (docs/reports/rotation-batches-2020-2026.html)
- **Best plan (preset ★):** MNQ 20, one trade per account per day, rotation one account after another,
  first account BUY, next account keeps the same side (after a loss and a win), take profit +$1,500 **after costs**,
  no stop (ride to the account's floor), all day. ≈ 31 % passed, ≈ 9 of 30 per batch, ≈ 4.7 sessions per batch.
- Alternatives to offer as presets: MNQ 10 at 2:00 London; all at once (own random side) ≈ 31 % in ≈ 2.3 sessions;
  alternate MNQ / MGC (MNQ 25 / MGC 20).
- Confirmations (ORB 30 + 15 m trend, VWAP, short-term trend) shown as **indicators with a likelihood %**, not as gates.
  None beat timing by more than 1–3 points per trade.
- Breakeven / profit locks: available, but tested as neutral (shown with that note).

## Views
- **Simulator only:** account tiles + price targets moving (Manus style, animated floor / buffer / target bars).
- **With chart:** MNQ / MGC price chart with the target and floor lines, draggable.
- Colours: per firm (KeystoneFirmNames) and per state (in eval / day banked / passed / blown).

## Manual mode
When the plan's moment comes, the next account's BUY / SELL button flashes with the likelihood %. One press = the
order with its take profit and floor / stop (limit + stop at the broker in live). SKIP lets it go.
Controls always visible: contracts ±, take profit $, stop $, breakeven, close, close all, pause, next account, done for today.

## Live (real accounts)
Only through the existing armed `KeystoneAccountLink` per account (ARM + limits + confirmations, CLAUDE.md guardrails).
AUTO only where the firm allows automation (KeystoneFirmPolicies). Otherwise MANUAL with flashing buttons.

## Data
Every run, day, trade and account state is saved (Studio\EvalLab\…). Live accounts' days come from NinjaTrader's balances.
Moving to the Funded Lab copies the account's eval record. The Eval Lab keeps it.

## Stages
1. Window + POOLS (firm groups, round robin, one-click edit) + PLANS (presets/templates) + ROTATE backtest
   (instant + replay, AUTO / MANUAL with flashing buttons + likelihood %) + STATS + DATA. Simulator-only view.
2. Chart view in the replay (target / floor lines), ADVISOR panel (history comparison), colour templates.
3. LIVE: real eval accounts in firm round robin through armed links; AUTO / MANUAL; daily tracking from NinjaTrader.
4. FUNDED LAB (separate window), automatic move of passed accounts.
