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
