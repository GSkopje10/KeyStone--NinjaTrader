# ❓ HOW TO USE the labs (🧪 Eval Lab • 💰 Funded Lab)

The same guide is the **❓ HOW TO USE** tab, first in the left rail of both labs (`LabHowTo` in `src/KeyStone.cs`).
Everything in the labs is simulation on the minutes saved on this PC: nothing sends an order.

## 🧪 EVAL LAB

### THE 5 STEPS (🧭 START HERE does them with you)

- 1 • ACCOUNTS — 🧭 START HERE ▸ ① how many evaluations and which firms (or pick a pool from 👥 POOLS) ▸ ✓ NEXT. They rotate firm by firm: Firm1·acc1, Firm2·acc1 …
- 2 • THE PLAN — ② keep ★ (the tested fixed model) or ⚙ customise (instrument, contracts, take profit $, floor / stop $, side, start time) ▸ ✓ NEXT. Your own plans live in 🎯 PLANS.
- 3 • THE DAYS — ③ FROM / TO (yyyy-mm-dd, at least 2 saved days; 🗂 DATA ▸ ⤓ UPDATE / DOWNLOAD gets them) ▸ ✓ NEXT.
- 4 • CHART OR MATH — ④ ▶ watch it minute by minute on the chart (replay) or ⚡ the totals at once ▸ ✓ NEXT.
- 5 • START — ⑤ check the summary ▸ ▶ START. ← BACK / ✎ CHANGE fix a step; ⚙ advanced shows every screen.

### READING THE RESULTS

- 6 • 🔄 ROTATE ▸ 📈 TRADING = the chart + the pool on the right (🟢 ACTIVE / ✅ PASSED / 💀 BLOWN, ⤢ BIG CARDS); with ⚡ AUTO off the next account's BUY / SELL flashes — nothing happens until you press.
- 7 • ⚡ CALCULATE ranks every plan on the days (progress bar + %, ■ STOP ends it); 🔮 HINDSIGHT shows what the perfect sides would have made.
- 8 • 📅 START-DAY TEST (in ⚡ CALCULATE) starts a batch on every day of the range (progress bar + %, ■ STOP).
- 9 • 📊 STATS = pass rate, days to pass, fees per funded account by firm / plan / month; 🧠 ADVISOR = the likelihood of the target before the floor.

### NEXT DAY • FUNDED • EXPORTS

- 10 • In the replay ▶▶ NEXT DAY opens the next saved day; a passed account stops trading, a blown one drops out of the round robin.
- 11 • 🔁 PIPELINE: eval → funded → payouts in one run — ▶ ONE BATCH / 📅 A BATCH EVERY WEEKDAY / 🔁 BUSINESS LOOP (progress bar + %, ■ STOP).
- 12 • ⇢ SEND THE PASSED TO 💰 FUNDED LAB moves the passed accounts there; their eval history stays here.
- 13 • ⤓ CSV / ⤓ HTML REPORT in each screen and 🗂 DATA (saved runs) export everything; 📜 EVENT LOG ▸ ⤓ EXPORT LOG saves every event.
- 14 • 🗄 ARCHIVE / ↺ RESTORE and ✕ DELETE ask first (YES / NO; ✕ DELETE POOL: click twice).

## 💰 FUNDED LAB

### THE 5 STEPS (🧭 START HERE does them with you)

- 1 • ACCOUNTS — 🧭 START HERE ▸ ① how many funded accounts per firm (Lucid / Tradeify / TPT) or a pool from 👥 POOLS ▸ ✓ NEXT.
- 2 • THE PLAN — ② ★ MAX IN 5 • 🧩 SPREAD is the default: each account trades its own time (2:00 / 8:30 / 9:30 / 10:00 / 11:00) and a firm never holds opposite positions; PUSH MNQ 6, then QUALIFY MNQ 2 (+$200 / −$600, up to 3 tries). ‘SAME SIDE FOR ALL’ is the all-or-nothing option ▸ ✓ NEXT.
- 3 • THE DAYS — ③ FROM / TO (saved days on this PC) ▸ ✓ NEXT.
- 4 • CHART OR MATH — ④ ▶ the replay on the chart or ⚡ the payout math at once ▸ ✓ NEXT.
- 5 • START — ⑤ ▶ START. In ▶ REPLAY: FROM → TO ▸ ⇩ LOAD ▸ ▶ PLAY (SPEED 1× … 30×) or ▶▶ NEXT DAY.

### READING THE RESULTS

- 6 • ▶ REPLAY = the chart + the pool on the right (🟢 ACTIVE / 💰 PAID / 💀 BLOWN, ⤢ BIG CARDS): each row = one funded account — the bar runs from the floor to what the payout needs, qualifying days n / 5, the next payout date once known; 🔒 LOCKED = the floor stopped at the start; ✋ = the firm wants MANUAL (BUY / SELL flash).
- 7 • 💵 PAYOUTS ▸ ▶ RUN THE PAYOUT CYCLES = the payouts tracker: qualifying days, each payout, what reaches you (progress + ■ STOP).
- 8 • TPT: phase 1 = the one-shot lock (9:30, MNQ 10, +$2,250, intraday trailing incl. open profit); phase 2 = withdraw $2,000 while the $2,000 buffer stays (80 % → $1,600), never above $10,000 profit a day.
- 9 • 🪑 BENCH: at most 5 active funded accounts per firm; the extra passed ones wait and replace a blown / finished one (first in, first out).

### NEXT DAY • EXPORTS • LIVE

- 10 • ▶▶ NEXT DAY closes the day (qualifying day counted, payout checked) and opens the next one.
- 11 • ⤓ CSV / ⤓ HTML REPORT export the runs; ▶ REPLAY ▸ 📜 TRADES lists every replay trade.
- 12 • ✕ DELETE (a pool, a plan) asks first (YES / NO).
- 13 • ● LIVE (🧪 EVAL LAB rail, eval + funded) only reads your NinjaTrader prop accounts and hands them to ● LIVE TRADING ▸ 🔄 ROTATE; the labs send no order.

## ● LIVE

### 🏢 FIRMS & RULES • ⚡ FAST CASH • 🎯 TPT +$10K • 💰 BUDGET

- 🏢 FIRMS & RULES (left rail, both labs): one card per firm — evaluation (size, price, discount, target, drawdown, min days, best day %), funded (drawdown, lock, qualifying days, payout %, cap, split, min payout), AFTER PAYOUTS (live after N, or a cooldown with no cap — Lucid: 14 days), max funded at once and automation. 💾 SAVE; every simulation uses them.
- ＋ ADD FIRM: a code, a name and a colour → the common 50K template; check each box against the firm's fine print, then 💾 SAVE. The pipeline and the round robin include it.
- Presets: ⚡ FAST CASH (funded MNQ 6, +$1,500 / −$500, one trade a day, the first eligible payout) and 🎯 TPT +$10K (lock MNQ 15 London side, then MNQ 10 +$4,000, withdraw at +$12,000; manual).
- 🔁 PIPELINE ▸ 💰 BUDGET: your starting money + the price of one evaluation → how many to buy now, the reserve, when to scale, when to add a firm.

### ● LIVE (🧪 EVAL LAB rail — read only)

- ● LIVE lights up when NinjaTrader is connected to a prop account: it detects the firm and the stage (EVAL / FUNDED), one pool per firm per stage, the daily tracking.
- ✓ CONFIRM hands the ticked accounts (firm by firm, MANUAL) to ● LIVE TRADING ▸ 🔄 ROTATE — real orders exist only there, through each account's armed link (ARM + its limits + confirmations). The labs themselves send no order.

## BUILD 2026-11-10a — the trading screen

- **Tabs:** 📈 TRADING (top bar + chart + pool) · ⚙ SETTINGS (+ the instant results) · 📊 RESULTS · 📜 LOG · ✅ PASSED · 💀 BLOWN.
- **Accounts per firm:** each firm has its own count box (default 5, 1–50), and the total is shown ("15 = 5 TPT + 5 Lucid + 5 Tradeify"). The same boxes appear in wizard step ① and in ⚙ SETTINGS.
- **Firm by firm:**
  - Every day the active accounts are dealt in turn (TPT 1, Lucid 1, Tradeify 1, TPT 2 …).
  - The entry times are given out in blocks, so the next account always comes from another firm, both within one entry time and from one time to the next.
- **One time zone:** every time in the lab is New York time (the strategy's time) with AM / PM. The 🌐 SESSIONS box shows the same moment in New York, Chicago (CME), London and Tokyo, plus the day's entry times (✓ done / ▶ next).
- **AUTO / MANUAL:**
  - ⚡ AUTO trades Lucid / Tradeify by itself and writes a 💬 WHY sentence for every trade.
  - At a ✋ TPT account's minute the clock **stops** and a banner explains why. The press fills at that minute's price, then the replay resumes.
  - "let AUTO trade TPT too" works in the backtest only.
  - With AUTO unticked (MANUAL) nothing happens by itself: you pick any side, MNQ / MGC / both, your size, your TP and your stop.
- **⏩ Skip quiet time:** with AUTO on in the backtest, when nothing is open and nothing is due, the clock jumps straight to the next entry time (one step), and the jump is logged. ⏭ NEXT ACCOUNT'S TIME does the same by hand.
- **Chart:**
  - Timeframes 1m 2m 3m 5m 15m 30m 1h.
  - Wheel = zoom. Drag = pan. Drag the price axis = stretch. Double-click = fit.
  - Clicking a candle shows its O / H / L / C. A crosshair shows price / time.
  - Drawing tools: H-line, trend, rect, text, delete.
  - ⟲ NOW follows the newest candle again.
  - A 6 PM New York separator marks the day; the minutes before the session are shaded as history.
  - ⛶ FULL SCREEN keeps only the chart, with a small floating pool.
- **The pool:** only the active accounts. Blown and passed accounts leave at the close (💀 BLOWN / ✅ PASSED tabs). A TPT account at +$3,000 counts as PASSED; day 3 is only a tiny trade to activate it.
- **DAY DONE:** the box shows why accounts are still in evaluation. At the end of the batch you get 🏁 BATCH DONE and 🎉 ▶ START THE FUNDED STAGE, which opens the Funded Lab with step ① done.
- **⤓ EXPORT WHAT HAPPENED:** writes the events, trades, accounts, settings and days to `Studio\EvalLab\exports\<time>\`.

### LIVE (stage 3, not built yet) — design note
- Live runs in real time. Between entry times the lab sleeps, and it wakes 1 minute before each slot with a countdown.
- A ✋ TPT account's slot raises an alert and a sound, and its button flashes. Nothing is sent without the press, and every order still goes through the armed `KeystoneAccountLink` with its limits.
- No time skip in live: the skip is a backtest feature only.

## BUILD 2026-11-10b — ▶ START HERE (⚙ SETTINGS, at the top)
- **START DATE:** any saved day. The END is filled in automatically as that session plus the next one (2 trading days, the eval minimum), or 3 trading days when TPT is in the mix (its 3rd day is the tiny activation trade). Holidays are skipped using the saved days. The END stays editable.
- **SCOPE:** EVAL → FUNDED → PAYOUTS (both) · EVAL ONLY · FUNDED ONLY (N funded accounts per firm, taken from the per-firm boxes). Plus the FUNDED PLAN (★ MAX IN 5 spread by default).
- **⚡ CALCULATE EVAL ONLY:** no chart, instant. Opens 📊 RESULTS ▸ 🧪 EVAL RESULTS:
  - per firm: passed / blown / in eval, trades won / lost, win %;
  - every account's day 1 / day 2 / day 3 and its status;
  - the longest winning and losing streaks.
- **⚡ CALCULATE EVAL → FUNDED → PAYOUTS:** no chart. The passed accounts go funded under the chosen plan (bench rule applied) on the days after the eval, running to payouts or blown. Shows every tab plus spent / paid / net.
- **▶ REPLAY WITH CHART:** the 📈 TRADING screen. **⏩ TO THE END** plays every loaded day with AUTO.
- **Result tabs:** 🧪 EVAL RESULTS · 📅 DAYS (per date and firm) · 📜 TRADES (NY time AM / PM, instrument, side, contracts, entry, P/L after costs, how it ended) · 💀 BLOWN · 💰 PAYOUTS · 📊 STATS (by firm, by day number, by week, money). The period is in the header.
- **⤓ EXPORT:** writes the CSVs plus `summary.html` to `Studio\EvalLab\exports\<time>-<scope>\`.
- **One engine:** ⚡ CALCULATE and the ▶ replay share the same side source (`KeystoneLabFlow.PlanSide`) and the same trade rules. Played to the end, the replay gives the same passes, blows and trades (UI smoke test).

## BUILD 2026-11-10c — the chart-first TRADING screen
- **Top:** ONE toolbar (the New York clock, ▶ / speed / ▶▶ NEXT DAY / ⏭ NEXT ACCOUNT / 📅 / ⤓ EXPORT / ⛶ / ⋯), then ONE status line (ellipsis, the full text in its tooltip; 📰 news in a tooltip), then the latest 💬 WHY or ⏩ skip (all of them are in 📜 LOG).
- **Chart:** owns all the rest of the height. The timeframes are on its top edge and the drawing tools down its left edge.
- **Right:** the POOL, one row per account (firm colour bar, name, state chip, balance / today, a room bar from floor to target) with 🟢 ACTIVE / ✅ PASSED / 💀 BLOWN tabs and their counts. ⤢ BIG CARDS shows large cards in place of the chart.
- **Bottom:** the ORDER BAR, in one row: BUY / SELL / ✕ CLOSE · MNQ ± · MGC ± · TP $ ± · STOP $ ± · ⇢ BE · ⏭ SKIP ACCOUNT · ✕ CLOSE ALL · ■ DONE · ⚡ AUTO, plus the last message.
- **Moved to ⚙ SETTINGS ▸ 📈 REPLAY OPTIONS:** the plan line, the plan's ± sizes, 'let AUTO trade TPT too', ⏩ skip quiet time, the views and the AUTO explanation.
- **Left rail:** while TRADING is shown, the rail shrinks to icons and expands on hover.
- **BATCH DONE:** the totals come first, e.g. "15 bought (5 TPT · 5 Lucid · 5 Tradeify) · $1,800 spent · 6 passed (40 %) · 9 blown". A per-firm line follows. There is no NEXT DAY: the next step is 🎉 START THE FUNDED STAGE.
- **Funded from the eval:**
  - The days step fills itself, so there is no date to pick.
  - Each account goes funded the session after it passed; TPT goes after its day-3 tiny trade.
  - Each account runs to its payouts, or to the end of the saved data ("⏳ data ends … download more in 🗂 DATA").

## BUILD 2026-11-10d — ● LIVE in the labs (stage 3: detect, organise, track)
- **The ● LIVE button** (🧪 EVAL LAB rail) stays grey until NinjaTrader is connected to a prop account. Then it lights up and pulses with a count ("● LIVE · 1 TPT eval"). It re-checks every 5 s.
- **Detection:**
  - The firm comes from the account name or the connection. The stage (EVAL / FUNDED) comes from name patterns in `Studio\EvalLab\live\patterns.txt` (editable).
  - A one-click override per account is saved.
  - An eval shows its progress, e.g. "+$2,800 / $3,000 · $200 to go · days 4/3". At the target plus the minimum days it shows "✅ target reached: waiting for the firm to move it to funded".
  - A funded account that appears for that firm is linked to the eval that passed.
- **Pools:** one pool per firm per stage ("TPT · EVAL", "TPT · FUNDED", "LUCID · EVAL" …). Tick the accounts to rotate, then ✓ CONFIRM.
- **✓ CONFIRM:** hands the ticked accounts, firm by firm (the backtest's order) and MANUAL, to ● LIVE TRADING ▸ 🔄 ROTATE. There you ARM each account with its limits and confirm START ROTATION. The labs never send an order themselves. TPT is always manual.
- **Per-pool settings:**
  - Eval pools are locked to the winning plan (★ STAGGERED MNQ 20, +$1,500 riding the room); 🔓 UNLOCK asks first.
  - Funded pools default to 🛟 SAFE PAYOUT (MNQ 1, +$800 / −$300 at each account's own time and side; TPT PRO: MNQ 2 · 9:30 London · withdraw $1,000 at +$3,000).
  - "↺ back to the winning plan" restores the default.
- **Tracking tabs:** 📅 DAILY · 📈 PROFITABILITY · ✅ ELIGIBILITY · ⏳ TIMELINE · 💰 PAYOUTS. Daily rows are saved per account in `Studio\EvalLab\live\<account>.csv`. Payouts and fees are entered by hand, since NinjaTrader doesn't know them.
- **🛟 SAFE PAYOUT** is now the Funded Lab's default preset ("tested: about 6 in 10 funded accounts reach a payout"). ★ MAX IN 5 stays an option.

## BUILD 2026-11-10e
- **🏠 HOME:** three cards (● LIVE TRADING · 🧪 EVAL LAB · 💰 FUNDED LAB). The old studios sit under "🧰 Old tools (will be removed)", collapsed; their code stays until you confirm nothing is missed.
- **📅 2-DAY TEST** (⚙ SETTINGS ▸ ▶ START HERE): every weekday from the START DATE to the RANGE END starts a batch running 2 sessions (3 with TPT). Shows the pass % per start day, by weekday (best / worst) and the distribution. Instant, same engine as CALCULATE.
- **📁 REPORTS** (rail): every export. 🌐 OPEN SUMMARY; tick two to compare them side by side; 📥 IMPORT a .kreport.json.
- **🗂 DATA:** MNQ / MGC coverage, the missing weekdays and MNQ days without MGC, plus ⤓ UPDATE / DOWNLOAD (the DATA LIBRARY). A CALCULATE over a period with gaps says so.
- **🎯 PLANS:** 🏆 FIND BEST opens ⚡ CALCULATE, which ranks every plan on the period, with ▶ USE THIS PLAN.
- **🧠 The flashing buttons' %:** labelled "history, not a forecast".
- **● LIVE TRADING:** a 🗂 data chip (up to date or not). The 🧪 / 💰 shortcuts open the labs. No order path changed.
- **💰 FUNDED LAB result:** the phase strip (EVAL ✓ ▸ FUNDED ▸ PAYOUT 1 ▸ …) with "next: …" in words. 🛒 BUY THE NEXT BATCH shows money in vs spent and the rule (no new batch until the payouts cover the spend; keep 3 batches of budget). It starts a new Eval Lab batch with the same counts per firm.
- **⚡ ALL AT ONCE:** now held to 4:55 PM like STAGGERED. The old "30 of 30 blown" came from the take profit waiting for a close above it; it is a limit now (tested).

## BUILD 2026-11-10f
- **🏆 FIND BEST** (🎯 PLANS):
  - Every eval plan is ranked by pass % on FROM → TO, with both halves of the period shown for stability.
  - Every funded plan is ranked by "paid at least once %", then $ per account (5 Lucid + 5 Tradeify).
  - The top 5 of each come with ▶ USE THIS PLAN.
- **🧠 The % on the flashing buttons:**
  - It now comes from the analog fingerprint engine: the 30 most similar earlier moments of the loaded days (±60 min of the time of day), each replayed with this trade's bracket.
  - It shows as "similar past moments: X% reached +TP before the floor (n = …)".
  - With fewer than 10 similar moments it falls back to the same-time history %. Either way it is labelled "history, not a forecast".
- **Controls:** the timeframe buttons, pool tabs, ⤢ BIG CARDS, per-firm ± and "N EACH", REPORTS open buttons and the order-bar steppers are now ≥ 28 px.

## BUILD 2026-11-10g — the Funded Lab's ▶ REPLAY on the chart-first screen
- **Top:** ONE toolbar — FROM → TO on the left, the New York clock, then ▶ / speed (1× … 30×) / ▶▶ NEXT DAY / ⏭ NEXT ACCOUNT / ⇩ LOAD / 📜 TRADES / ⛶ / ⋯ (+1 MIN, +10 MIN, the stage indicators) — then ONE status line.
- **Chart:** owns the rest (the Eval Lab's chart: timeframes on top, drawing tools down the left, wheel / drag / double-click / click a candle), the ENTRY / TP / STOP / FLOOR lines in the right gutter.
- **Right:** the POOL (290 px) with 🟢 ACTIVE / 💰 PAID / 💀 BLOWN and their counts; each row: firm colour and name, balance, the floor → payout-need bar, qualifying days n / 5, profit vs what the payout needs, the next payout date once known, the payout taken, blown on. ⤢ BIG CARDS shows the same as large cards.
- **Bottom:** the ORDER BAR in one row: ▲ BUY / ▼ SELL (the plan's side flashes with its %) / ✕ CLOSE / ½ HALF · MNQ ± · TP $ ± · STOP $ ± · ⇢ BE · ✕ CLOSE ALL · ■ DONE · ⚡ AUTO.
- **Left rail:** shrinks to icons on ▶ REPLAY. The rail's ● LIVE opens the Eval Lab's ● LIVE (eval + funded accounts, read only).

## New in BUILD 2026-11-11c (phase C: screens and navigation)

- **❓ every tab says what it shows** in one line at the top, with buttons to jump: 🧭 START HERE · ⚙ SETTINGS · 📊 RESULTS · 🗂 DATA · 🏢 FIRMS & RULES (Funded Lab: SIMULATE / PAYOUTS / the Eval Lab's RESULTS).
- **📅 PICK A DAY opens inside the lab tab** (✕ or Esc closes it), from ▶ START HERE's 📅, the wizard's step ③ and the replay toolbar. Three ways to pick:
  1. QUICK: a day, its week, a month (pick from the list), a quarter, a year, the last 1 / 3 / 6 / 12 months, all 6 years.
  2. TICK SEVERAL: years, months, weeks (and days in the list), then ✓ USE THE TICKS. Ticks that touch become one range; ticks apart become separate periods.
  3. THE LIST: click a YEAR or MONTH header for all of it, click a day for that day, SHIFT-click a second one for the range between.
  Every choice fills START + END and the summary line ("2025 + 2026 · 483 trading days"). With separate periods, ⚡ CALCULATE runs each on its own; RESULTS shows a 📅 PERIODS table (each + Σ COMBINED). Click a period to see its full results.
- **🟩 Account tiles** (replay pool, the instant-run deck, Funded Lab tiles) show an animated buffer bar from the floor (red tick) through the balance (incl. open P/L) to the target or payout need. TPT: ⟳ the floor trails until the lock, then 🔒 and the $2,000 buffer band. Lucid / Tradeify: EOD trailing until +$100. MFF: ▬ static. Each tile also shows its days, "days to lock / pass: n so far / usually ≈ m", the qualifying dots, the last trade and the next trade time. The bar eases and flashes on pass / payout / blow.
- **🗂 Clicking a tile or a ✅ PASSED / 💀 BLOWN row opens the account card inside the tab.** The TPT card shades the $2,000 buffer above the locked floor.
- **🕒 TIMELINE**: click a row to open the chart replay at that minute (◀ ▶ one minute, ⏪ ⏩ 15, ▶ PLAY). 🗂 THE ACCOUNT CARD is one click from there.
- **🏆 BEST START DAYS**: click a column header to sort (click again to reverse). 📦 EXPORT FOR CLAUDE works here and in the Funded Lab's payout tracker / wizard results too.
- **＋ ADD A FIRM** (Eval wizard step ①, right side): a name, how many accounts, the rules "same as" Lucid / Tradeify / TPT / Apex / MFFU / Topstep / MFF Studio 002 / 001 / 003, or custom (target, drawdown $, type, price). It is saved to 🏢 FIRMS & RULES and ticked in ① at once.
- **👥 Pool dropdown**: each pool shows its breakdown ("5 TPT · 5 Lucid · 5 Tradeify"). Your real pools come first. The auto-saved mixes (🧭 wizard, 🎉 eval runs) are shown only when you tick "show the auto-saved mixes".
- **⚡ SPRINT**: one control (0 / 1 / 2 / 3 / ALL, default 2) with its research note, in ▶ START HERE, the Funded wizard ② and the ● LIVE funded pool settings.
- **Backtest Studio ▸ REGULAR BACKTESTING**:
  - 🎯 PRACTICE AS A PROP ACCOUNT (top bar): presets for MFF Studio 002 / 001 / 003, Lucid / Tradeify / TPT eval, and the funded versions, or custom rules (size, STATIC / EOD / INTRADAY drawdown, drawdown $, target, max contracts).
  - The gauge shows floor ▸ balance (incl. open) ▸ target. 💀 FLOOR and ✅ TARGET lines sit where the open position would hit them, with the zone between them shaded.
  - Orders above max contracts are refused. A 💀 BLOWN / ✅ PASSED overlay appears when the floor or target is hit. ↺ RESET ACCOUNT starts a new attempt. The last attempts are listed and saved to Studio\practice-attempts.csv.
  - 💵 AUTO BRACKET IN $: TP $ / SL $ (quick $250 / $500 / $1,000 / $2,000, STOP = THE ROOM TO THE FLOOR) placed at the price that gives exactly that $ after costs for the contracts chosen. It is recalculated when the size changes. In practice, the stop is clamped at the floor. The point brackets stay available (click to switch).
  - ⏱ REAL TICKS: the replay plays the day's real NinjaTrader ticks. They are cached in Studio\TickCache, with 1-second bars as the fallback. Each print lands at its real time (1× = a minute in 60 s). The forming candle builds from the ticks, and fills happen only at tick prices. Without ticks, a ⚠ note and a ⤓ GET TICKS button show on the chart.
  - 🏷 The trade labels (position, STOP, TARGET, FLOOR) sit in a right gutter. The candles end before it, and the TP ⇕ / SL ⇕ handles show on hover. A closed trade's tag reads "closed −$…", the realized result, not the open P/L.

## New in BUILD 2026-11-11d — ⚡ CALCULATE keeps buying until END

- **Continuous is the default.** ⚡ CALCULATE (EVAL → FUNDED → PAYOUTS and EVAL ONLY) runs from START to END (or the saved data's end). Every slot keeps its firm (LUCID 1, TPT 2 …). When its account blows (in eval or funded) or finishes (the goal, moved to live, the MFF cap), a new eval of the same firm is bought the next session. It then trades eval → funded → payouts again, over and over, until END. It runs on the 🔁 BUSINESS RUN engine, out of pocket: the price per firm comes from 🏢 FIRMS & RULES (else the FEE box), with max 5 funded per firm (the rest wait on the 🪑 bench).
- **Every generation is in the record.** "LUCID 1 · #3" is the third account bought for slot LUCID 1. ACCOUNTS, the cards, TIMELINE, PAYOUTS, STATS, FUNDED RESULTS and both exports cover the whole period.
- **The RESULTS header** reads: START → END · sessions · continuous: N evals bought ($ spent) · passed · went funded · payouts ($ to you) · net.
- **EVAL RESULTS** opens with a 📅 MONTH BY MONTH table: evals bought, $ spent, passed, funded alive at the month end, payouts n / $, net and cumulative net. The 🏢 per-firm totals come next.
- **EVAL ONLY is continuous too.** A blown or passed eval's slot buys a new eval. The header and STATS show the pass rate and the cost per passed account.
- **"one batch only (no replacements)"** (▶ START HERE, under SCOPE, off by default, remembered) gives the old behaviour. When that batch finishes before END, the header says so ("the batch ended … : all 30 accounts finished (… blown in eval · … blown funded · … reached the goal), nothing left to trade until …"). A 🔁 keep buying until END button reruns it continuously.
- **▶ REPLAY WITH CHART stays one batch.** Its results say so, with a ⚡ CALCULATE the whole period (continuous) button.
- **⚡ SPRINT badges** appear on the account rows, the cards and the Funded Lab tiles. FUNDED RESULTS splits each firm into ⚡ SPRINT and 🛟 SAFE. A sprint account rides MNQ 10 with no stop for its first 5 funded days, so one blown on its first funded day is the sprint's risk, not a bug. In a continuous run, the sprint count is N per batch-worth of evals bought (e.g. 2 more for every 30 evals).
