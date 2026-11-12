# 🚑 STEP IN — what the saves teach (SpasiX 09n)

Source: every SpasiX export in the session scratchpad (13 exports, 04 Nov – 12 Nov 2026, DEMO accounts
512502@Duramarkets and 5298760@ICMarketsSC, SAFE / DEFENDER / FULLAUTO on XAUUSD M1): the STORY files
(`✋ MANUAL ADD`, `✋ PART CLOSED BY HAND`, `✋ CLOSED BY HAND`), `14/15/16-manual-*` (every leg of the
account, flat to flat) and the 🤖 WHAT IF cases (#57, #83). The trend at each step-in was re-read from the
exported M1 candles (EMA 9 / 21 + the last 60 minutes). Script: scratchpad `stepin/study.py`.

**Small n: 10 iterations with a manual step-in, 6 of them deep (≥ 5 × the target). Nothing below is a
statistic — it is a description of what one trader did on one week of gold.**

## 1. Every step-in

| # | when (server) | depth at the first action | × target | % balance | minutes open | trend then | what he did | size vs the basket | outcome | automation alone |
|---|---|---|---|---|---|---|---|---|---|---|
| A | 05 Nov 07:07 (#12, $50 target) | −$374 | 7.5× | 7.7 % | 110 | DOWN | BUY 0.6 (against), 48 s later SELL 1.4 (flip with the trend) + SELL 0.3 + SELL 0.3 (stack) | 0.8× the gross per add, gross 1.7 → 3.7 | **+$22.72** (rescue, green), worst −$559 (11×) | — (before 09g) |
| B | 05 Nov 09:12 (#2, $50) | −$81 | 1.6× | 1.6 % | 24 | flat | SELL 0.2 with the net | 0.3× | +$53.74 target | — |
| C | 05 Nov 10:27 (#5, $30) | −$153 | 5.1× | 2.9 % | 18 | **flat** | SELL 0.7, BUY 1.4, SELL 1.4, SELL 1.4 in 4 min (flip-flop) | gross 1.2 → 6.1 (5×) | **−$212.25** (loss), worst −$644 (21×) | — |
| D | 05 Nov 21:52 (#15, $10) | −$82 | 8.2× | 1.6 % | 66 | UP (fell −13 in the next hour) | BUY 0.5 with the trend, then SELL 1.0 (flip) + SELL 0.35, closed 0.35 | gross 0.85 → 2.18 | +$1.52 (breakeven), worst −$417 (42×) | — |
| E | 06 Nov 13:18 (#1, $10) | −$17 | 1.7× | 0.3 % | 4 | down → flat | 3 × BUY 0.1 | small | +$4.50 closed by hand | — |
| F | 06 Nov 22:22 (#18, $10) | −$235 | 23.5× | 4.4 % | 245 | DOWN | 4 × SELL 0.65 in 11 s, all WITH the trend (net +0.12 → −2.48) | 2.6 lots = 4.5× the gross 0.58 | +$1.75 (breakeven), worst −$497 | — |
| G | 06 Nov 22:30 (#19, $10) | −$31 | 3.1× | 0.6 % | 3 | flat / up | 2 × SELL 0.65 | 2.7× | +$22.44 target | — |
| H | 09 Nov 17:21 (#35, $16) | −$354 | 22× | 4.5 % | 140 | flat (−12 in 60 min, EMA flat) | CLOSED 3 × SELL 0.16 (cut one side; net +0.08 → +0.40) | — (reduction) | +$1.37 (breakeven), worst −$472 | — |
| I | 11 Nov 02:19 (WHAT IF #57, ≈ $10) | ≈ −$20 | ≈ 2× | 0.3 % | 388 | UP (+6.7 in 60 min) | BUY 0.5 with the trend, closed 1 min later | 4× the gross 0.13 | +$0.97 | **+$10.45** (target; "would have done it too") |
| J | 11 Nov 23:16 (WHAT IF #83, ≈ $50) | **−$1,666** | **33×** | ≈ 20 % | 118 | **flat chop** (ATR M1 $0.65, 60-min move −0.13; 7 flips of 0.44 → 2.2 lots inside $4) | CLOSED EVERYTHING (cut, −$566 of the open legs) | — | **−$1,676.82** | **HARD STOP −$4,707.85** 5 h later → he saved **$3,031** |

## 2. What the numbers say

**When he stepped in (the 6 deep ones, A C D F H J):** depth 5.1× – 33× the target, **median ≈ 15×**;
the EARLIEST deep step-ins came at **5–8×** (A 7.5×, C 5.1×, D 8.2×); 1.6 % – 20 % of the balance
(median 4.4 %); after **18 – 245 minutes open (median ≈ 114 min)**.

**What he did and how it ended:**

* WITH the trend in size (A, F; D first) → A green (+$22.72), F breakeven (+$1.75). His adds were big:
  0.8× – 4.5× the basket's gross at once.
* FLIP-FLOP in a market with no trend (C) → the only real loss (−$212, the basket went 21× deep).
* CUT in a chop (H: one side closed, J: everything closed) → H breakeven, J −$1,677 instead of the
  automation's −$4,708: the biggest save of the week ($3,031) was a CUT, not a stack.
* Shallow step-ins (B, E, G, I; ≤ 3×) all ended ≥ $0 — the automation alone would mostly have done the
  same (I: the WHAT IF says +$10.45 alone vs his +$0.97).

Outcomes of the 6 deep ones: 1 green, 3 breakeven, 2 losses (one of them the save of the week).

## 3. → the defaults of 🚑 STEP IN

| setting | default | from |
|---|---|---|
| trigger | **−8 × the target** (TOTAL = open + this iteration's closed legs) | the earliest deep step-ins came at 5–8× (A, C, D) |
| …or stuck | **120 min at ≥ 3 × the target** | median ≈ 114 min open at the step-in |
| direction | **only WITH a confirmed trend** (TrendDetect ≥ 60 + a NEW finished swing break), one order flips a net that is against it | A / F worked, C (no trend) lost |
| no trend | **HOLD = net 0 once**, nothing until a trend shows | C's flip-flop and #83's 7 flips in $4 |
| size | enough to reach the goal in 2 × ATR(M5), capped by the risk budget (the equity above the floor over 3 × ATR M5) and **≤ 20 % of it at the stack's own stop** (1.5 × ATR M5) | his adds were 0.8×–4.5× the gross; the 20 % cap came from the backtest below (without it the account blew in 31 % of the step-ins) |
| bank | **TRIM** 33 % of the stack every 1 × ATR M5 in its favour, **TRAIL** 1.5 × ATR M5 behind its best once 1 ATR in profit, **CUT PAIR** a counter-trend loser with a with-trend winner only when the pair is ≥ $0 (the net and the total do not change) | H (cut one side), his quick closes after a move (D, I) |
| exit | **CLOSE ALL at breakeven + the target** (option: breakeven); the 🎯 EXIT GUARD watches that goal | the user's request |
| risk | simulated: MAX RISK 100 %, floor $0 (it may blow — counted); MT5: opt-in, MAX RISK 50 %, neutralise at the floor, never a loss close | the user's request + his live decision |

## 4. Backtest (the bench: scratchpad `stepbench/`, `stepbench3/`)

Engine = the app's own `HedgeAuto` + `StepIn.cs` on the 🧪 BACKTEST's `SimBroker` (never MT5): his DEFENDER settings of the
12 Nov export, $8,000, leverage 1:500, stop-out 50 %, spread 35 points, $7 a lot, the broker's swap; 💥 AUTO RESTART as the DEMO does
it (a blown account is reset to $8,000 and counted). Data: the repo's 1-minute gold (data/MGC, Jun 2020 – Oct 2026, futures as a
stand-in for XAUUSD, ticks synthesised inside each candle) + the exports' own XAUUSD M1 candles of 4 – 12 Nov 2026. The 🎯 EXIT GUARD
(a tab feature) is not in the bench. "The automation alone" = `WhatIf.Replay` of the basket at the trigger, the same settings without
STEP IN, up to 10 days (3 days for the November week). STEP IN defaults as shipped (trigger 8× / 120 min at 3×, MAX RISK 100 %,
floor $0, M1 breaks, ≤ 20 % of the equity per stack).

### 4a. The six years, per year (net = Σ iteration results; blow-ups = resets)

| year | DEFENDER alone: net / blow-ups / hard stops | DEFENDER + 🚑 STEP IN: net / blow-ups / step-ins |
|---|---|---|
| 2020 (Jun–Dec) | +$2,584 / 0 / 0 | −$53,104 / 7 / 20 |
| 2021 | −$4,231 / 1 / 2 | −$59,143 / 8 / 41 |
| 2022 | −$7,119 / 0 / 3 | −$64,442 / 9 / 52 |
| 2023 | −$7,232 / 1 / 3 | −$69,976 / 10 / 50 |
| 2024 | −$6,200 / 1 / 2 | −$99,332 / 12 / 75 |
| 2025 | −$13,278 / 1 / 5 | −$131,548 / 18 / 189 |
| 2026 (Jan–Oct) | −$48,467 / 6 / 23 | −$106,906 / 13 / 232 |
| **all** | **−$83,943 / 10 blow-ups / 38 hard stops** | **−$584,451 / 77 blow-ups / 659 step-ins** |

(DEFENDER itself loses in this bench — without 🧹 COMPRESS its swap and long baskets cost it, as the 09h replays already showed.)

### 4b. The 659 step-ins

* recovered GREEN (closed at breakeven + the target): **581 = 88.2 %**, average **+$97.90**
* 💥 BLOWN (a stop-out → the account reset): **77 = 11.7 %**, average **−$9,525**; 1 other loss
* back to breakeven only: 0 (the goal is breakeven + the target; nothing else ends an episode)
* depth at the trigger: average −5.9× the target (most fired on the 120-minute rule at ≥ 3×); the worst depth AFTER the step-in:
  average −46×, the worst −447×; median time to recover 141 min
* $ net of all step-ins **−$679,101** vs CLOSE AT THE TRIGGER **−$195,852** vs THE AUTOMATION ALONE (10-day replays) **−$262,416**
  (566 of 659 would have closed green alone, 38 at the hard stop)
* per trigger depth: < 5× 290 step-ins, 41 blown, −$329,182 • 5–10× 367, 36 blown, −$350,115 • 10–20× 2, 0 blown, +$196
* per year the blow-up rate fell from 35 % (2020) to 6 % (2026): wider ATR in later years puts the stops further from the spread

### 4c. Variants (the same six years)

| variant | step-ins | green | blown | net of all iterations |
|---|---|---|---|---|
| default (MAX RISK 100 %, ≤ 20 % a stack) | 659 | 88.2 % | 11.7 % (77) | −$584,451 |
| ≤ 5 % a stack (smaller stacks) | 354 | 77.1 % | 21.8 % (77) | −$618,476 — WORSE: small stacks rarely reach the goal, the episode drags on |
| MAX RISK 50 % (neutralise at the floor, the live default) | 22 | 86.4 % | 13.6 % (3) | −$20,783 — the account is parked at the floor for months (2024: 2 iterations, 2026: 1) |
| an earlier draft without the per-stack cap / spread floor / no stacks against an open stack | 246 (2020–24) | 69.1 % | 30.9 % (76) | −$604,185 (2020–24 only) |

### 4d. His own week, 4 – 12 Nov 2026 (the exports' XAUUSD candles — 8 days, small)

DEFENDER alone **−$6,667** (1 blow-up, 4 hard stops) • DEFENDER + STEP IN **+$3,120**: 16 step-ins, **16 green, 0 blown**, the
episodes +$1,197 vs the automation alone on the same 16 baskets +$441 (3-day replays) vs closing at the trigger −$4,921.

## 5. Honest summary

* STEP IN does what it was asked: it recovers most deep baskets (88 % green over six years, 16 / 16 in his own week) — and it blows
  the account in about 1 step-in of 8 (77 blow-ups in 659 over six years). One blow-up (−$9,525 on average) wipes out ~97 recoveries
  (+$98 each), so over six years it lost far more than closing at the trigger, and more than the automation alone.
* The losses come from what makes it "on steroids": stacks sized to reach the goal in 2 × ATR, re-stacked after every failed stack,
  until the goal or the account is gone. Smaller stacks (5 %) were worse; a 50 % floor turns blow-ups into baskets parked for months.
* His own saves were mostly CUTS (#83, #35) and one trend add; the bench says the cut ("close at the trigger") is the cheapest
  of the three paths over six years. The statistics panel (🛡 HEDGE ▸ ANALYSIS / LEARN, the 🏁 RACE lanes STP / DEF+STP) will now
  count the same thing on his own DEMO.
* The study n is tiny (10 step-ins, 6 deep); the backtest is futures 1-minute data with synthetic ticks, without the 🎯 EXIT GUARD.
  On an MT5 account STEP IN stays an opt-in (typed STEP IN each session, MAX RISK 50 % by default) — the six-year numbers above are
  the reason to keep it that way.
