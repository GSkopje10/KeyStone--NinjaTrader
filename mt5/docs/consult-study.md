# 🧭 HISTORY CONSULT — the 6-year bench (13s)

**Short answer: acting on the consult's advice did not help. It made the 6-year result worse.** The consult stays **ADVISE** by default. Its 🏁 RACE lane CNS and its ACT modes are there so it can keep being checked on real ticks. Nothing suggests switching them on.

## What was tested

* **Data:** `data/MGC` 1-minute **gold futures** candles used as XAUUSD, with **synthetic ticks** inside each candle. Jun 2020 – Oct 2026, a $8,000 account, 1:500, stop-out 50 %, 25-point spread, swap. A blow-up restarts the account at $8,000, like the DEMO's 💥 AUTO RESTART.
  * The user's caveat applies in full. These are futures candles, not his broker's XAUUSD ticks. His STEPLEAD settings (hedge distance 5) are sensitive to the exact price path, so the whole bench is **an estimate**. The app says so wherever the library is used ("estimate — futures candles, synthetic ticks").
* **Engine:** the 13q engine (the breakeven-lock bug fixed), run by the app's own `HedgeAuto` on the BACKTEST `SimBroker` (Step + the 🎯 EXIT GUARD). The bench code is in the scratch bench `Cb.cs`, modes `points`, `measure`, `bench` and `track`.
* **Settings:** the user's DEFENDER export (`his.txt`), with 🔐 BREAKEVEN PROFIT = CLOSE ALL and RISK GUARD off (variant **BCL**).
* **Library:** every hedge decision of the BCL run (13,988 decisions). Every 2nd one was measured, giving **6,996**.
  * Each measured decision was replayed 5 times from the same basket in an in-memory SimBroker: 🛡 hedge now (the engine's own rules), ⏳ wait for the next swing, 🔐 lock at net 0, ➕ add with the trend (2 × the hedge lots), ✖ close now.
  * After the option, the engine's own rules take over.
  * The replay runs until the basket closes, or for up to 3 days. A basket still open after 3 days is valued at its open total (marked to market).
* **Features of a decision:** finished candles before the decision only (tested: no lookahead).
  * The hedge number, the depth (× target), the basket's range (× ATR) and the minutes in the basket.
  * The EMA 50/200 trend and the 60-minute move, both measured against the net side.
  * The NY hour (sin / cos), the distance to VWAP (× ATR), the spread and the triple-swap day.
* **Advice:** the 40 nearest past decisions whose outcome was **already known** at that moment (walk-forward).
  * Each option gets a mean and a 95 % interval.
  * The consult recommends something only when n ≥ 30 **and** the best option beats "hedge now" on the same neighbours with a 95 % interval above $0. Otherwise it says "no clear answer — your rules".

## The library (what each option did, averaged over the 6,996 decisions)

| year | decisions | hedge now | wait | lock | add with trend | close now |
|---|---|---|---|---|---|---|
| 2020 | 137 | −$245 (73 % recovered) | −$245 | −$245 | −$191 | −$281 (9 %) |
| 2021 | 423 | −$289 (80 %) | −$293 | −$308 | −$309 | −$242 |
| 2022 | 425 | −$327 (80 %) | −$315 | −$328 | −$357 | −$304 |
| 2023 | 365 | −$388 (77 %) | −$358 | −$341 | −$437 | −$281 |
| 2024 | 837 | −$311 (81 %) | −$300 | −$293 | −$306 | −$254 |
| 2025 | 1,813 | −$309 (90 %) | −$313 | −$309 | −$318 | −$317 |
| 2026 | 2,996 | −$410 (88 %) | −$402 | −$403 | −$452 | −$355 |
| **all** | **6,996** | **−$355 (86 %)** | −$350 | −$349 | −$380 | −$318 (9 %) |

"Recovered" means the basket ended at or above $0.

* The means are dominated by a few huge baskets (−100 × the target and worse).
* "Close now" has the best *mean* only because it cuts that tail. It recovers 9 % of the baskets, against 86 % for the engine's hedge.
* No option is better everywhere.

**Walk-forward track record on the library:** each decision was advised only from the decisions known before it.

* 268 clear calls in 6,859 decisions: 230 add with the trend, 21 wait, 13 close now, 4 lock.
* Together they made **+$1,423** against the engine's hedge, and were better in 56 % of the calls.
* That is inside the noise.

## The bench: CONSULT acting (simulated) vs your settings vs RISK GUARD

Net = the closed iterations' results. The year runs are independent (each starts at $8,000).

| variant | iterations | net 6 y | 💥 blow-ups | HARD STOPs | worst iteration |
|---|---|---|---|---|---|
| **your settings** (BCL: 🔐 breakeven profit = CLOSE, RISK GUARD off) | 8,598 | **−$257,875** | **38** | 144 | −$40,342 |
| **+ 🧭 CONSULT ACTING** (BCL + CNS, library walk-forward) | 8,770 | **−$356,617** | **47** | 168 | −$21,874 |
| **+ 🛡 RISK GUARD** (RG + BCL) | 1,465 | **−$25,714** | **4** | 25 | −$8,641 |

Per year, consult acting vs your settings:

| year | your settings | consult acting |
|---|---|---|
| 2020 | +$2,755 | +$2,516 |
| 2021 | −$20,892 | −$26,704 |
| 2022 | −$9,670 | −$9,653 |
| 2023 | −$28,944 | −$29,615 |
| 2024 | −$59,055 | −$52,023 |
| 2025 | −$27,615 | −$18,044 |
| 2026 | −$114,454 | −$223,094 |

* The consult acted 576 times. 32 of those were "close now" closes, worth −$55,060 between them. The rest were mostly "add with the trend", then wait and lock.
* It helped in 2024 and 2025 and was neutral in 2020, 2022 and 2023. **2026 was much worse**: 30 blow-ups against 18, and −$223k against −$114k. Adding with the trend in 2026's fast, two-way market fed the tail instead of cutting it.
* The bench is chaotic (iter23-study). One different close shifts every later iteration, so single-year differences are noise. The total and the blow-up count still point the same way: **worse**.
* **🛡 RISK GUARD remains the only rule that clearly changes the tail**, as in iter23-study: −$26k and 4 blow-ups instead of −$258k and 38.

## What the app does with this

* **🧭 CONSULT MODE = ADVISE by default** everywhere. The advice is shown in the 🛡 HEDGE banner and in the 🧭 CONSULT view: the neighbours, the option table, its own track record. ADVISE never sends anything.
* **Memory order:** the user's own **real-tick** decisions come first. These are his live / DEMO baskets, measured after they close, plus 🧭 BUILD FROM MY TICKS on the XAUUSD ticks he downloaded in 📥 DATA STUDIO.
  * With ≥ 30 of them known, only they are used.
  * Otherwise the 6-year library fills in after them, labelled "estimate — futures candles, synthetic ticks".
* **Acting:** ACT ON SIMULATED and ACT ON THIS ACCOUNT exist, the latter after the typed CONSULT, every session. So does the 🏁 lane CNS. All of them are labelled with this result.
* **To revisit:** when enough real-tick decisions exist, the CNS lane and the consult's own track record (its view) will show whether real ticks change this answer.
