# Iteration #23 (12–13 Nov, DEMO XAUUSD, DEFENDER): what happened, what-ifs, scenarios, 6-year check

Research only. No app code changed, nothing built or shipped. Sources:

* the user's export `exp1113` (DEMO 5298760@ICMarketsSC-MT5, BUILD 2026-11-13o), real recorded ticks of 12 and 13 Nov;
* the app's own `HedgeAuto` on the BACKTEST `SimBroker`. On every tick the harness calls `Step` + `GuardStep` + `ProtectStep`, the same calls the 🛡 HEDGE tab makes. The earlier benches called only `Step`, so they had no 🎯 EXIT GUARD;
* the 6-year bench: `data/MGC` 1-minute gold (Jun 2020 – Oct 2026) used as XAUUSD, with synthetic ticks inside each candle, $8,000, 1:500, stop-out 50 %, 💥 AUTO RESTART like the DEMO.

Harness: `mt5/study/Iter23Bench.cs.txt`. It compiles against the same `mt5/app` files as `Swing2Bench`. Analysis scripts: `Iter23Agg.py.txt`, `Iter23Scenarios.py.txt`, `Iter23Pool.py.txt`.

**Costs (the user's correction): spread only + swap, no commission.**

* #23 replays use the real tick spread (≈ 0.17–0.20) and no markup.
* The bench uses a fixed 25-point spread (0.25).
* The export has no swap figures, so the swap is the value the repo's tests use for XAUUSD: long −62.4 points / short +28.1 points per lot per night (≈ −$62.40 / +$28.10 per lot), triple on Wednesday. **This is an assumption.** Check it against MT5's symbol specification on the account.

## 0. Was 🪜 SWING2 in play?

**No.** `01-settings.txt` has `htrig=LEAD`. Every hedge of #23 in `03-trades` / `04-story` was the fixed-lead break hedge: "the recent low / high broke", the 🛡 MAX HEDGE DISTANCE 5 and RANGE-ONLY. None was a 🪜 swing hedge.

The exit was the 🎯 EXIT GUARD at BREAKEVEN. It was in "trouble" mode because the basket had been down to −$1,452 and had been open more than 60 minutes, so its level was +$0.50 and not the +$50 target.

The 🏁 RACE does not show what SW2 would have done on #23 either. The app restarted at 01:09 and the race lanes started fresh baskets then. The SW2 lane's only basket (01:23–01:47) was a different, short one.

## 1. Replay fidelity

To check the replay against the real run, I used the cost the DEMO actually charged: 0.001 % per deal ≈ $7.8 per lot round turn (see § 6). The recorder has no tick between 18:16:10.9 and 18:16:32.6. The one quote of the real entry that the STORY gives (bid 3877.42 / ask 3877.59 at 18:16:20) was added, and the BUY 0.1 was forced there, as `Swing2Case` did.

| | real (#23) | replay |
|---|---|---|
| entry | BUY 0.1 @ 3877.61 18:16:20 | BUY 0.1 @ 3877.59 |
| hedges (lots @ price, time) | S 0.2 @ 3872.50 18:22 • B 0.3 @ 3878.86 18:51 • S 0.48 @ 3872.29 19:21 • B 0.69 @ 3881.09 20:08 • S 0.87 @ 3872.12 23:13 • B 1.1 @ 3881.54 01:46 | S 0.2 @ 3872.61 18:21 • B 0.3 @ 3878.65 18:51 • S 0.48 @ 3872.29 19:21 • B 0.7 @ 3881.07 20:08 • S 0.87 @ 3872.12 23:13 • B 1.1 @ 3881.52 01:45 |
| most lots open | 2.18 | 2.18 |
| worst total | −$1,459.51 | −$1,460 (02:00) |
| exit | 🎯 EXIT GUARD BREAKEVEN 03:22:24, **−$2.97**, 9 h 06 m | 🎯 EXIT GUARD BREAKEVEN 03:21, **−$2.09**, 9 h 05 m |

All 6 hedges match, with the same sizes (0.69 vs 0.70 is one scale-out of 0.01) and prices within ≈ 0.2. The worst total and the exit match to the minute and to the dollar. After #23 the replay drifts: its next entry was a BUY at 03:30, the real one a SELL at 03:34. The "until data end" column below is therefore only indicative. The data ends at 04:38 server.

Spread only (no commission), the same replay ends at **+$1.17 (RESCUE BE, 03:21)**, worst −$1,428. That is the baseline for the what-ifs.

## 2. What happened (in words)

* Gold sat in a **$9 box (3872–3881) for 7.5 hours**. ATR(M5) was ≈ $2.3–5.5, so the box was only 1.5–4 × ATR(M5).
* STEPLEAD makes each hedge bigger than the last: 0.2 → 0.3 → 0.48 → 0.69 → 0.87 → 1.1. Every whipsaw across the box cost more than the one before.
* A false breakout at 21:42–22:44 went up to ~3885 and came back. At 23:13 the 5th hedge sold 0.87 at the bottom of the box.
* The basket carried net −0.5 over the 00:00 rollover. The swap on it was small (about +$4 with the rates assumed above).
* The real breakout started at 01:46 (BUY 1.1 @ 3881.54, the basket at −$1,336 total). It ran to ≈ 3914–3916 by 03:24–03:30.
* SCALE OUT banked 0.01 per $1.5 and COMPRESS closed the matched pairs, which does not change the total.
* The guard closed everything at breakeven at ≈ 3906. That was **8–10 points before the top**, with net 0.45 long, i.e. ≈ $360–450 left on the table.

## 3. What-if table: #23 on the real ticks (spread + swap, no commission)

"Until data end" = the account's change from 18:16 to 04:38 with the automation running on (open baskets marked to market).

| variant | hedges | max lots | worst $ | worst % bal | exit | result #23 | how it closed | until data end |
|---|---|---|---|---|---|---|---|---|
| **as-is (LEAD)** | 6 | 2.18 | −1,428 | 19.9 % | 03:21 (9 h 05) | **+1.17** | RESCUE BE | −121.55 |
| 🪜 SWING2 N=1 / 2 / 3, TOUCH | 2 | 0.48 | −123 | 1.7 % | 18:51 (35 min) | +52.12 | TARGET | N1 −166.53 • N2 −59.43 • N3 −52.55 |
| 🪜 SWING2 N=1 / 2 / 3, CLOSED candle | 2 | 0.54 | −220 | 3.1 % | 20:13 (1 h 56) | +1.24 | RESCUE BE | N1 −6.11 • N2 / N3 −131.31 |
| 🚑 STEP IN (sim: MAX RISK 100 %) | 8 | 1.69 | −437 | 6.1 % | 20:36 (2 h 19) | +53.95 | 🚑 STEP IN GOAL | +78.36 |
| 🚑 STEP IN, MAX RISK 50 % | 8 | 1.69 | −437 | 6.1 % | 20:36 | +53.95 | 🚑 GOAL (the 50 % floor was never touched) | +78.36 |
| 🛡 RISK GUARD / LOCK on (`rg=1`) | 5 | 0.70 | −504 | 7.0 % | 03:03 (8 h 46) | +0.79 | RESCUE BE (locked 19:21, "TOO BIG") | −66.46 |
| hedge size ADVANTAGE (fixed lead 0.1) | 6 | 0.50 | −476 | 6.6 % | — | open −133.22 | still open at the data end | −133.22 |
| hedge size SAME | 6 | 0.50 | −276 | 3.8 % | 03:22 | +1.74 | RESCUE BE | −56.96 |
| max hedge distance 8 / 10 | 6 | 2.18 | −1,428 | 19.9 % | 03:21 | +1.17 | RESCUE BE (the cap never bound here) | −121.55 |
| exit: hold to the +$50 target (guard / rescue / danger exit off) | 6 | 2.18 | −1,428 | 19.9 % | 03:22 | +66.86 | TARGET | −55.86 |
| no SCALE OUT at all | 6 | 2.18 | −1,500 | 20.9 % | 03:20 | +14.32 | DANGER EXIT | −108.40 |
| no SCALE OUT while a breakout runs | 6 | 2.18 | −1,475 | 20.6 % | 03:20 | +6.10 | RESCUE BE | −116.62 |
| TRAIL the breakout, 1 × ATR(M5) | 6 | 2.18 | −1,475 | 20.6 % | 03:24 | **+385.42** | trailing stop 3911.11 (best 3916.66) | +262.70 |
| TRAIL the breakout, 2 × / 3 × ATR(M5) | 6 | 2.18 | −1,475 | 20.6 % | — | open +516.50 at 04:38 | not stopped yet | +516.50 |
| range-break re-entry (BE close in a breakout → the next one WITH it) | 6 | 2.18 | −1,428 | 19.9 % | 03:21 | +1.17 | RESCUE BE, then BUY +50.62 at once | −67.85 |
| TIGHT RANGE → hedge size ADVANTAGE | 6 | 1.08 | −703 | 9.8 % | — | open −361.19 | still open | −361.19 |
| TIGHT RANGE → RISK GUARD | 6 | 0.98 | −798 | 11.1 % | — | open −633.97 | still open | −633.97 |
| ⏰ NO NEW ITERATION in the last 45 min (16:15–17:00 NY) | 6 | 2.18 | −1,428 | 19.9 % | 03:21 | +1.17 | RESCUE BE | −121.55 |
| ⏰ close at the close ONLY IF ≥ breakeven (16:50 NY), else keep | 6 | 2.18 | −1,428 | 19.9 % | 03:21 | +1.17 | (at 16:50 NY the basket was −$1,100 → KEPT) | −121.55 |
| both ⏰ rules | 6 | 2.18 | −1,428 | 19.9 % | 03:21 | +1.17 | as above | −121.55 |
| 🛡 RISK GUARD + both ⏰ rules | 5 | 0.70 | −504 | 7.0 % | 03:03 | +0.79 | RESCUE BE | −66.46 |
| hold to target + both ⏰ rules | 6 | 2.18 | −1,428 | 19.9 % | 03:22 | +66.86 | TARGET | −55.86 |
| TRAIL 1 × ATR + both ⏰ rules | 6 | 2.18 | −1,475 | 20.6 % | 03:24 | +385.42 | trailing stop | +262.70 |

The two ⏰ rules cannot change #23:

* it started at 11:16 NY, not in the last 45 minutes;
* at the close (16:50 NY = 23:50 server) it was −$1,100, so the "only if ≥ breakeven" rule keeps it over the rollover, as asked.

For comparison, the rejected "close everything before the rollover" would have realised **−$1,094**.

## 4. Scenarios: what the data say

### 4a. The live exports (13 exports, 4–12 Nov, 114 iterations, 130 hedges)

* Iterations by add-ons: 0 → 52, 1 → 20, 2 → 20, 3 → 8, 4 → 4, 5 → 4, 6+ → 3.
* **All 11 deep ones (≥ 4 add-ons) except one ended at ≈ breakeven** (+$0.2 to +$49, after 2–9 hours, worst −$108 to −$2,988).
* The one exception is −$424 in LEG TARGETS mode.
* The real danger is not visible in these 9 days: the WHAT IF #83 would have hit the HARD STOP at −$4,712.
* Whipsaw (the hedge reversed within 30 min) is ≈ 50 % at every hedge number (1st 50 %, 2nd 55 %, 3rd 53 %, 4th 64 %, 5th+ 36 %). It does not depend on the range in ATR either (< 2: 52 %, 2–4: 55 %, 4–6: 46 %, ≥ 6: 33 %).
* LATE NY (16:00–19:00) hedges whipsawed 76 % (n = 17) against 41–59 % in the other sessions. That is suggestive but n is small.

### 4b. The same questions on 6 years (as-is LEAD, spread only)

| group | iterations | net | losers | worst (p10) | HARD STOPs |
|---|---|---|---|---|---|
| 0–2 add-ons | 1,479 | **+$69,426** | 0 | −$146 | 0 |
| ≥ 3 add-ons | 769 | **−$169,246** | 54 | −$2,193 | 53 |
| ≥ 5 add-ons | 230 | −$173,360 | 49 | −$4,961 | 48 |
| closed by HARD STOP | 53 | −$182,623 | 53 | | |

The whole 6-year loss is in the ≥ 3-add-on baskets, and 99 % of it is in the HARD STOPs. They come at the 5th+ STEPLEAD hedge, when the lots are ≥ 2 and a range finally breaks against the net, or the basket has no lots left to fight with.

#### Scenario A: TIGHT RANGE

Definition: ≥ 3 hedges whose legs lie inside < 4 × ATR(M5).

* It **describes 93 % of all ≥ 3-hedge baskets** (718 of 769). It is not a rare scenario that can be singled out: it is how a basket gets to 3 hedges in the first place.
* The "not tight" ones (51) lost too (−$6,180).
* Acting on the flag inside an iteration does not help on #23: "→ ADVANTAGE size" left it open at −$361; "→ RISK GUARD" left it open at −$634.

#### Scenario B: BREAKOUT from the basket's range WITH the net

Definition: the price ≥ 1 × ATR(M5) beyond every leg's price, ≥ 2 add-ons.

* 862 baskets closed (breakeven / rescue) while such a breakout ran.
* In the next 60 minutes the price went on its way **+7.7 points on average** (+14.4 within 4 hours).
* 63 % went ≥ 1 ATR(M5) further, 39 % ≥ 2 ATR(M5).
* So "money left on the table" is real and frequent, the #23 pattern.
* Whether harvesting it pays is in § 5: TRAIL and hold-to-target.

#### Scenario C: TIME OF DAY

By the start session (6 years, LEAD, spread only):

| start session | iterations | net |
|---|---|---|
| ASIA | 523 | −$10k |
| LONDON | 302 | −$28k |
| NY AM | 999 | −$42k |
| NY PM | 310 | −$23k |
| LATE (16:00–19:00 NY) | 114 | +$3k |

The ⏰ "no new iteration in the last 45 min before the close" is in § 5.

## 5. The 6-year bench

Same harness and cost model (spread 0.25, swap as above, no commission). Each period starts with a fresh $8,000. "net" = closed iterations + the basket still open at the end of each period, marked to market.

Main run: spread 0.25, no commission, swap as above. The last column is a first run with **$7 / lot + spread 0.35** (the previous studies' costs, the earlier binary) as a sensitivity check. In it, "—" = not run with those costs.

| variant | net 6 y (closed + open) | closed only | 💥 blow-ups | HARD STOPs | iterations | per year (closed + open) | $7 / lot run: net • blow-ups |
|---|---|---|---|---|---|---|---|
| **as-is (LEAD)** | **−$109,668** | −$99,821 | 14 | 53 | 2,248 | 20 +1.6k • 21 −1.1k • 22 −22.3k • 23 −29.8k • 24 −10.0k • 25 −3.2k • 26 −44.8k | −$118,961 • 14 |
| **🛡 RISK GUARD / LOCK (`rg=1`)** | **−$4,661** | **+$9,825** | **2** | **5** | 695 | 20 −5.6k • 21 −8.8k • 22 −0.6k • 23 −10.1k • 24 −0.2k • 25 +10.4k • 26 +10.2k | **+$4,134 • 0** |
| 🛡 RG + ⏰ both rules (NO NEW ITERATION in the last 45 min + close at the close only if ≥ breakeven) | −$5,691 | +$4,786 | 2 | 8 | 897 | 20 −5.6k • 21 −6.2k • 22 −0.8k • 23 −12.2k • 24 −0.9k • 25 +10.4k • 26 +9.6k | — |
| 🛡 RG + ⏰ NO NEW ITERATION in the last 45 min | −$9,886 | +$3,585 | 2 | 7 | 827 | 20 −5.6k • 21 −8.8k • 22 −0.6k • 23 −10.1k • 24 −4.9k • 25 +10.1k • 26 +10.0k | — |
| 🛡 RG + hold to the +$50 target | −$23,439 | −$9,621 | 2 | 15 | 864 | | — |
| 🪜 SWING2 N = 2, CLOSED candle | −$54,249 | −$47,695 | 4 | 23 | 850 | | (09o bench without the guard: −$15,479) |
| ⏰ NO NEW ITERATION in the last 45 min (alone) | −$136,221 | −$125,625 | 16 | 64 | 2,335 | 20 +1.6k • 21 −1.1k • 22 −29.2k • 23 −29.9k • 24 −10.0k • 25 −3.2k • 26 −64.4k | — |
| hold to the +$50 target (no BE exits) | −$145,764 | −$144,975 | 18 | 78 | 2,426 | | −$69,715 • 7 |
| TRAIL the breakout 1 × ATR(M5) | −$237,081 | −$236,805 | 29 | 123 | 4,729 | | −$30,097 • 7 |
| ⏰ close at the close only if ≥ breakeven (alone) | −$285,308 | −$282,093 | 38 | 140 | 7,046 | | — |
| ⏰ both rules (alone) | −$323,116 | −$321,028 | 42 | 164 | 7,461 | | — |
| hedge size ADVANTAGE | — | | | | | | −$27,579 • 2 |
| range-break re-entry | — | | | | | | −$67,384 • 8 (6 years) |
| no SCALE OUT in a breakout | — | | | | | | −$131,934 • 18 |
| TRAIL 2 × ATR(M5) | — | | | | | | −$140,148 • 19 |
| 🚑 STEP IN (sim, MAX RISK 100 %) | — | | | | | | −$497,902 • 60 (6 years) |

How to read it, honestly:

* **DEFENDER as-is loses over 6 years in this bench (−$110k, 14 resets of the $8k account).** All of the loss is in the ≥ 3-add-on baskets that end at a HARD STOP (§ 4b). The 0–2-add-on baskets make +$69k.
* **The bench is chaotic.** One different close shifts every later iteration, a basket can sit open for a whole year (LEAD 2021: 5 iterations, one basket open from 6 Jan; 2025: 18), and blow-ups land at random. The same settings moved −$119k → −$110k (LEAD) and +$4.1k → −$4.7k (RG) between the two cost models. **Differences under ≈ $30k between LEAD-like variants are noise.**
* What is robust is **the count of blow-ups / HARD STOPs**: RG 0–2 vs LEAD 14, in both runs.
* **🛡 RISK GUARD is the only rule that clearly changes the tail.** It caps the STEPLEAD growth (lead ≤ 3 steps) and LOCKs net 0 when the next hedge would be too big. 6 years: −$4.7k instead of −$110k, 2 blow-ups instead of 14.
  * Its cost: baskets freeze. The average basket lasts 45 h. Locked baskets were left open at the end of a period for months: 2026 since 3 Feb (−$4.7k), 2023 since 29 Nov, 2022 since 16 Mar.
  * 2020, 2021 and 2023 were still −$5k to −$10k years.
  * It is not an edge. It is a much smaller tail.
* **⏰ The close-time rules:**
  * NO NEW ITERATION in the last 45 min is ≈ neutral, inside the noise: alone −$136k vs −$110k, with RG −$9.9k vs −$4.7k. It does no visible harm and has no proven benefit. It matches the live LATE-session whipsaw hint (76 %, n = 17), but the bench cannot confirm that.
  * "Close at the close only if ≥ breakeven" is **harmful alone** (−$285k, 38 blow-ups). Closing baskets early at ≈ $0 starts 3× more iterations (7,046 vs 2,248), and every iteration is another ticket into the deep-basket tail. With RG it is neutral (−$5.7k).
  * The rejected "close everything before the rollover" would have realised −$1,094 on #23 alone. Not benched further.
* **Exits that let breakouts run** (hold to target, TRAIL) win #23 (+$67 / +$385) but **lose on 6 years**. Spread-only: hold −$146k, TRAIL 1 × −$237k. The $7 run had TRAIL 1 × at −$30k, which shows how unstable it is.
  * In this bench the breakouts the basket is "with" reverse often enough that keeping the big net exposure (no breakeven exit) feeds the next round of STEPLEAD hedges.
  * § 4b's "money left on the table" (+7.7 points in 60 min after a breakout BE close) is real on average, but harvesting it costs more in the cases that turn.
* 🪜 SWING2 + CLOSED candle: −$54k with 4 blow-ups. Better than LEAD, not as good as RG. 🚑 STEP IN with the simulated MAX RISK 100 % is far worse on 6 years (60 resets), as in the step-in study.

## 6. The DEMO's commission (the −$87.50) and the account type

* SpasiX has an account-type setting for the 🎮 DEMO / 🧪 BACKTEST (`SimAccountSettings.Kind`, ⚙ DEMO ACCOUNT dialog in `Protect.cs`). Its naming is SpasiX's own, the user's definition, and it is the opposite of the brokers' marketing names:
  * **RAW** = NO commission per lot, the spread widened by `MarkupPts`;
  * **STANDARD** = commission $ / lot (round turn) + MT5's raw spread (`SimAccountSettings.Commission`: `Kind == "RAW" ? 0 : CommissionPerLot`).
* There is also a separate **COMMISSION %** field (`CommissionPct`, % of each deal's value, charged in AND out). It exists for Duramarkets (0.001 %).
* This account's DEMO header says **"simulated RAW (+2 pts spread)"**: no per-lot commission, +2 points markup.
* The commission it charges comes from **COMMISSION % = 0.001**: 0.001 % × 3,877 × 100 × 0.1 lot = $0.39 a side = **$0.78 per 0.1 lot round turn**. That is exactly the `commission` column of `02-iterations.csv` (0.48 per 0.06 lot, 0.78 per 0.1 lot, $29.00 on #23).
* Why it is set: `demo.pct` / `demo.kind` / `demo.mk` are saved **globally**, not per account (`Protect.cs` 151 / 162; `AutoTab.AutoDemoCommission` writes `demo.pct = 0.001` for a Duramarkets demo). The IC Markets demo inherited the Duramarkets 0.001 %.
* `Protect.cs` 139 would have started a new IC Markets demo "like the real account" (STANDARD, $0 / lot, markup 0, % 0). But that only happens when `demo.kind` was never saved, and here it had been.
* To match the real IC Markets account (spread only), set in ⚙ DEMO ACCOUNT ▸ ✔ APPLY: **COMMISSION % = 0 and EXTRA SPREAD = 0** (RAW, or STANDARD with $0 / lot). The balance and positions stay.
* A code fix (out of scope here, no app change): keep `demo.pct` / `demo.kind` per account key.

## 7. Recommendations

Ranked. Expected effects are from the 6-year bench (spread only + swap), and the risks are named.

1. **Turn 🛡 RISK GUARD ON (`rg=1`, LOCK, glead 3, the existing settings).**
   * Expected: the 6-year loss goes from −$110k to about −$5k, and blow-ups from 14 to 2. On #23 the worst would have been −$504 (7 % of the balance) instead of −$1,428 (20 %), still closing at breakeven at 03:03.
   * Risk: baskets freeze in a LOCK for days, sometimes months (capital tied up, swap paid on both sides, few new iterations). 2020 / 2021 / 2023 were still losing years. It is risk control, not a profit edge.
2. **Fix the DEMO's cost: ⚙ DEMO ACCOUNT ▸ COMMISSION % = 0, EXTRA SPREAD = 0 (✔ APPLY keeps the balance and positions).**
   * Expected: the DEMO matches the real IC Markets account (spread only). It stops paying ≈ $7.8 per lot round turn (−$87.50 so far, $29 on #23 alone).
   * Risk: none to the strategy. Only the DEMO's numbers change.
3. **Optional: ⏰ NO NEW ITERATION in the last 45 min before the 17:00 NY close** (`win=18:00-16:15`, the existing TRADING TIMES).
   * Expected: no measurable change on 6 years (inside the noise). It keeps fresh baskets from being born into the illiquid close and the rollover.
   * Risk: slightly fewer iterations.
   * **Do not add "close at the close if ≥ breakeven" on its own**: it was clearly harmful (−$285k, 38 blow-ups). It is only acceptable together with RISK GUARD, where it was neutral.

Not recommended as defaults, even though they won #23:

* hold to the +$50 target / TRAIL the breakout: #23 +$67 / +$385, but 6 years −$146k / −$237k;
* SWING2 TOUCH: #23 +$52 in 35 min, the 09o bench already showed it is not an edge.

"TIGHT RANGE" as a detector is not useful: it describes 93 % of all ≥ 3-hedge baskets. The real scenario to recognise is **"the 3rd+ STEPLEAD hedge inside the box"**, and RISK GUARD's lead cap / LOCK is the rule that acts on it.

**Recommended DEFAULT for this account** (spread only, swap): DEFENDER as exported + **RISK GUARD ON** + **NO NEW ITERATION 16:15–17:00 NY** (+ optionally close at the close only if ≥ breakeven). 6 years: **−$9.9k** (RG + 45-min rule) / **−$5.7k** (+ the breakeven close) vs −$110k as-is, 2 blow-ups vs 14. None of the variants is a proven positive edge in this bench.
