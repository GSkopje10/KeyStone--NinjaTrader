# ❓ HOW TO USE the labs (🧪 Eval Lab • 💰 Funded Lab)

The same guide is the **❓ HOW TO USE** tab, first in the left rail of both labs (`LabHowTo` in `src/KeyStone.cs`).
Everything in the labs is simulation on the minutes saved on this PC: nothing sends an order.

## 🧪 EVAL LAB

### THE 5 STEPS (🧭 START HERE does them with you)

- 1 • ACCOUNTS — 🧭 START HERE ▸ ① how many evaluations and which firms (or pick a pool from 👥 POOLS) ▸ ✓ NEXT. They rotate firm by firm: Firm1·acc1, Firm2·acc1 …
- 2 • THE PLAN — ② keep ★ (the tested fixed model) or ⚙ customise (instrument, contracts, take profit $, floor / stop $, side, start time) ▸ ✓ NEXT. Your own plans live in 🎯 PLANS.
- 3 • THE DAYS — ③ FROM / TO (yyyy-mm-dd, at least 2 saved days; BACKTEST STUDIO ▸ DATA LIBRARY downloads them) ▸ ✓ NEXT.
- 4 • CHART OR MATH — ④ ▶ watch it minute by minute on the chart (replay) or ⚡ the totals at once ▸ ✓ NEXT.
- 5 • START — ⑤ check the summary ▸ ▶ START. ← BACK / ✎ CHANGE fix a step; ⚙ advanced shows every screen.

### READING THE RESULTS

- 6 • 🔄 ROTATE shows the account tiles (floor / buffer / target bars) and the run's totals; in the replay the next account's BUY / SELL flashes when MANUAL is on — nothing happens until you press.
- 7 • ⚡ CALCULATE ranks every plan on the days (progress bar + %, ■ STOP ends it); 🔮 HINDSIGHT shows what the perfect sides would have made.
- 8 • 📅 START-DAY TEST (in ⚡ CALCULATE) starts a batch on every day of the range (progress bar + %, ■ STOP).
- 9 • 📊 STATS = pass rate, days to pass, fees per funded account by firm / plan / month; 🧠 ADVISOR = the likelihood of the target before the floor.

### NEXT DAY • FUNDED • EXPORTS

- 10 • In the replay ▶ NEXT DAY opens the next saved day; a passed account stops trading, a blown one drops out of the round robin.
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
- 5 • START — ⑤ ▶ START. In the replay: ① START THE REPLAY ▸ ▶ PLAY (SPEED 1× … ) or ▶ NEXT DAY.

### READING THE RESULTS

- 6 • Each tile = one funded account: the gauge runs from the floor (red) to the payout (green); 🔒 LOCKED = the floor stopped at the start; ✋ = the firm wants MANUAL (the button flashes).
- 7 • 💵 PAYOUTS ▸ ▶ RUN THE PAYOUT CYCLES = the payouts tracker: qualifying days, each payout, what reaches you (progress + ■ STOP).
- 8 • TPT: phase 1 = the one-shot lock (9:30, MNQ 10, +$2,250, intraday trailing incl. open profit); phase 2 = withdraw $2,000 while the $2,000 buffer stays (80 % → $1,600), never above $10,000 profit a day.
- 9 • 🪑 BENCH: at most 5 active funded accounts per firm; the extra passed ones wait and replace a blown / finished one (first in, first out).

### NEXT DAY • EXPORTS • LIVE

- 10 • ▶ NEXT DAY closes the day (qualifying day counted, payout checked) and opens the next one.
- 11 • ⤓ CSV / ⤓ HTML REPORT export the runs; 📜 EVENT LOG ▾ shows every event, ⤓ EXPORT LOG saves it.
- 12 • ✕ DELETE (a pool, a plan) asks first (YES / NO).

## ● LIVE

### 🏢 FIRMS & RULES • ⚡ FAST CASH • 🎯 TPT +$10K • 💰 BUDGET

- 🏢 FIRMS & RULES (left rail, both labs): one card per firm — evaluation (size, price, discount, target, drawdown, min days, best day %), funded (drawdown, lock, qualifying days, payout %, cap, split, min payout), AFTER PAYOUTS (live after N, or a cooldown with no cap — Lucid: 14 days), max funded at once and automation. 💾 SAVE; every simulation uses them.
- ＋ ADD FIRM: a code, a name and a colour → the common 50K template; check each box against the firm's fine print, then 💾 SAVE. The pipeline and the round robin include it.
- Presets: ⚡ FAST CASH (funded MNQ 6, +$1,500 / −$500, one trade a day, the first eligible payout) and 🎯 TPT +$10K (lock MNQ 15 London side, then MNQ 10 +$4,000, withdraw at +$12,000; manual).
- 🔁 PIPELINE ▸ 💰 BUDGET: your starting money + the price of one evaluation → how many to buy now, the reserve, when to scale, when to add a firm.

### WHAT LIVE WILL BE (stage 3 — disabled today)

- ● LIVE will trade the real accounts in the same firm round robin ONLY through each account's armed link (ARM + its limits + confirmations).
- AUTO only where the firm allows automation; otherwise MANUAL with flashing buttons. Today nothing in this window sends an order: it is simulation on your saved minutes.

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
