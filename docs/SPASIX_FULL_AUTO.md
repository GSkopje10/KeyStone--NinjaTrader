# SpasiX 🤖 FULL AUTO — the hands-off strategy (design)

Goal, in this order: **1. protect the capital, 2. grow it.**
FULL AUTO is one switch. Once it is on, you set no rules: the engine decides every entry, size, hedge, lock,
target, trail, pause and restart from the price, the account and what its own history taught it.
Every decision is drawn and explained on the chart and saved, so it can be checked and learned from.

It runs on the existing safe path only: the same `AutoSend` as today (MT5 CanTrade + ARM + PROP limits;
DEMO / BACKTEST trade their simulated account). The hard stops can never be switched off by the engine.

---

## 0. The only inputs (read, never asked)

| Input | Where it comes from |
|---|---|
| Balance, equity, margin, free margin, leverage | the account (MT5 / SimAccount) |
| Contract size, tick value, swap, spread, min / step lot | the symbol spec |
| Candles M1 … H4, ticks, spread history | live feed / history |
| News calendar (high impact) | the existing news feed |
| The firm's limits (PROP) | the PROP settings |
| Every past iteration, signal, post-mortem, race lane | `DataDir` (iterations, signals-*.csv, post-mortems, race\) |

The **one optional input is the risk dial**: SAFE / NORMAL / BOLD. It caps the risk budget below and nothing else.
Default = SAFE.

---

## 1. Risk budget: the ceiling everything else sits under

Computed again on every tick from the equity, **E**:

| Limit | SAFE | NORMAL | BOLD |
|---|---|---|---|
| Max basket drawdown (then the 🚨 exit ladder, §6) | 4 % E | 6 % E | 8 % E |
| Hard stop (closes everything, the automation stops for the day) | 8 % E | 12 % E | 15 % E |
| Day loss (no new basket until the NY day turns) | 5 % E | 8 % E | 10 % E |
| Max gross lots | 0.6 / $5k | 1.0 / $5k | 1.5 / $5k |
| Margin level floor (no new leg under it) | 800 % | 500 % | 350 % |
| Max legs in one basket | 8 | 10 | 12 |

- Margin is always the broker's real formula (contract × lots × price / leverage, the hedged-margin rule of the account type).
- The engine never opens a leg it could not survive at the hard stop.

---

## 2. Reading the market (every closed M1, plus every tick for the levels)

| Measure | How | Used for |
|---|---|---|
| **Regime** | ADX(14) M5 + EMA 50 / 200 slope + range of the last 60 min vs ATR | TREND ↑ / TREND ↓ / RANGE / CHOP |
| **Volatility** | ATR(14) M1 and M5, the spread in ATR units | distances, sizes, pauses |
| **Extension** | distance from VWAP and from EMA 50, in ATR; bars since the last pullback ≥ 0.5 ATR | “don't buy the top” filter |
| **Depth of the swings** | the average swing size of the last 2 h vs ATR | one-direction mode (§3.3) |
| **Key levels** | repeated swings (2+ touches), VWAP, EMA 50 / 200, day H / L, Asia H / L, round $5 | entries, hedge placement, targets |
| **Flow** | volume vs its 20-bar mean, the candle body share, aggression runs | confirmation |
| **Session** | Asia / London / NY / overlap / the last hour | size factor, which setups are allowed |
| **News** | minutes to the next high-impact event | pause / flatten (§7) |

---

## 3. Entries: several setups, each scored before it is taken

### 3.1 Setup library (all exist in code or research already; each one is scored separately)

| Setup | Trigger |
|---|---|
| **BRK** M1 break | the close beyond the swing high / low (the current DEFENDER entry) |
| **ENG** London engulfing | an engulfing at a key level, London |
| **FVG** 50 % fill | a retrace into the 50 % of a fresh (stacked) gap, NY |
| **VWR** VWAP rejection | a wick through VWAP with the close back on the trend side |
| **AGR** aggression reversal | an aggression run that stalls + a reversal break |
| **PBK** trend pullback | TREND only: a pullback to EMA 20/50 or VWAP + a resumption candle |
| **RNG** range fade | RANGE only: a limit order at the range edge, the target at the middle / other edge |

### 3.2 The score: 0 to 100, and an entry needs 60 or more

```
score = 40 × history edge      (the setup's win rate × payoff in the same regime / session / extension bucket,
                                from the iterations + signal journal; < 20 samples → neutral 0.5)
      + 20 × regime fit         (PBK in a trend, RNG in a range, BRK when the range is compressing …)
      + 15 × room               (distance to the next key level against the trade, in ATR, capped at 2)
      + 15 × confirmation       (flow + body + the higher timeframe agreeing)
      + 10 × timing             (session quality, no news within 15 min)
      − penalties: over-extended (no pullback ≥ 0.5 ATR in the last 10 bars and > 1.5 ATR from VWAP / EMA50) −30,
                   at the day H / L without a retest −15, a spread > 0.25 ATR −20,
                   the same setup lost twice in a row today −15
```

- Every scored signal is written to `signals-*.csv`, whether it is taken or not, together with what the price did over the next 15 / 30 / 60 min.
- That file is how the edge term learns.
- Your rule is built in: **a break at the very high or low with no retracement before it is not taken**. That is the over-extension penalty.
- Its “what if I had entered” result is still recorded.

### 3.3 One-direction mode (from the depth of the market's swings)

- **Deep swings** (swing ≥ 2 × ATR, the price returning through the middle) → the market mean-reverts:
  - only **limit** entries at the swing edges, in the bounce direction;
  - **no stop orders** beyond them (a break is likely to fail).
- **A clean trend** (regime TREND, shallow pullbacks) → only the trend side:
  - entries are PBK / BRK in the trend direction;
  - the opposite side is used only as a hedge.
- **Chop** (ATR rising, no direction, 3+ failed breaks in the hour) → **no new basket**, and the chart says so.

### 3.4 Size per entry: dynamic, not fixed 0.01

```
base  = E / 1000 × 0.01                              (0.05 on $5k)
size  = base × k(score) × k(volatility) × k(account state)
k(score)          = 0.5 at 60 … 1.0 at 75 … up to 5 × at 90+ (a very strong scalp: up to the setup's max, e.g. 0.5 lot on $5k)
k(volatility)     = clamp(normal ATR / ATR now, 0.5, 1.5)
k(account state)  = 1.0; 0.5 after a losing day or when the equity is under its 5-day high by > 3 %; 0 under the day-loss line
```

- The size is also capped so that the planned hedge path (§4) stays under the risk budget.
- If it would not, the size is cut. It is never the budget that gets raised.

---

## 4. Managing the basket (the hedge engine, made adaptive)

### 4.1 Where the hedge goes

- **Start:** beyond the protective level of the setup (the swing / gap / candle the entry relies on), at least 0.6 ATR away.
- **Key level** just before it → moved past that level.
- **📉 The deeper the basket, the closer the next hedge.** This shipped in 05v:
  - for every 2 % of the balance the basket is down, the hedge comes 10 % closer to the newest leg;
  - never closer than 50 % of the normal distance, and never inside half a candle.
  - 2 % was the best setting in the 2025 + 2026 replays; 1 % hedged too early.
- **A trend WITH the basket** → the hedge waits deeper (TrendLevel).
- **A trend AGAINST it** → the hedge comes closer still.

### 4.2 How big the hedge is

- **Fixed lead**: the new side leads by the lead lots.
- The lead grows with the drawdown, so that a deep basket gets back faster:
  - −1 % → +0.05;
  - −2 % → +0.10;
  - … capped at the gross-lots limit.
- The engine tests 2 or 3 leads per basket in the background (shadow lanes) and moves toward the one that recovers in the fewest minutes for the risk taken.

### 4.3 LOCK instead of hedging (both sides equal, frozen)

The basket locks instead of adding a hedge when **any** of these is true:
- the next hedge would pass the gross-lots limit;
- the margin level is under the floor;
- 3 hedges came in the last hour (chop);
- the 🚨 danger score is 70 or more;
- a high-impact news event is less than 5 minutes away.

**Unlock**:
- only on a confirmed break of the lock range (a close beyond it plus a 0.5 ATR follow-through);
- the losing side is trimmed first (pair-off), then the basket runs with the break.

### 4.4 Taking profit, protecting it, letting it run

| Situation | Action |
|---|---|
| The basket at +target (the target is set by the setup's expected move: 0.6 × the measured payoff, minimum $ = fees × 3) | close |
| +50 % of the target reached | break-even lock: the basket's stop moves to +fees |
| +100 % with momentum (flow + a close beyond the last key level) | **don't close**: trail at the last M1 swing, and add to the winner (≤ 50 % of the leading side, max 2 adds) |
| Scale-out on the way | close the worst losing leg each time the basket gains 1.5 ATR |
| A deep basket returns near 0 (it was −2 × target or worse) | take break-even / a small profit; don't wait for the full target |

---

## 5. 🚨 The danger meter (0–100, on the chart)

```
danger = 30 × (basket DD / max basket DD) + 20 × (margin used / margin floor)
       + 15 × (hedges in the last hour / 3) + 15 × (time in trade / 3 h)
       + 20 × (share of similar baskets in the history that did NOT recover)
```

| Danger | What it does |
|---|---|
| < 40 | normal |
| 40–60 | no adds to winners; the target drops to 50 % |
| 60–80 | LOCK instead of the next hedge; the target drops to 25 % (take any profit) |
| ≥ 80 or the basket at max DD | the exit ladder (§6) |

---

## 6. The exit ladder (protect the capital, step by step)

1. Close the worst leg that is losing more than its share, if the remaining basket is still a hedge.
2. Lock (§4.3).
3. Close half of the gross, worst legs first.
4. Hard stop: close everything at the risk budget's hard stop. The automation then pauses until the next NY day.

---

## 6b. 🛟 DRAWDOWN RECOVERY: getting back to break-even as fast as possible

Once a basket is in danger, **the goal stops being the target. It becomes break-even, as soon as possible.**

**Shipped in 05v: 🛟 RESCUE TO BREAKEVEN**
- It starts once the basket has been 3 % of the balance deep, or once the danger meter reaches its level (with DANGER EXIT off).
- From then on the TP line *is* the break-even price (+ the closing fees).
- The first touch of that price closes everything.

**Next: the recovery planner.** It runs every second while rescue is on.

1. **Read the moment.**
   - Break of structure on M1 / M5: the last swing high / low taken or held.
   - Which side is winning the last 5 candles; the distance to VWAP / EMA 50 / the key levels.
   - Momentum: ATR now vs normal, bodies, volume.
2. **Find the similar moments in the history.**
   - Each saved basket and each post-mortem is a vector: drawdown % of the balance, net lots and side, legs, time in the trade, regime, BOS direction vs the basket, distance to VWAP in ATR, session.
   - The 30 nearest neighbours are taken, weighted by distance.
   - For each one, the planner reads what was done and how long it took to reach $0.
3. **Score the recovery moves**, each replayed on the neighbours' candles in an in-memory SimBroker (never MT5):

   | Move | What it does |
   |---|---|
   | **HOLD** | wait for break-even with the current hedge |
   | **LEAN WITH BOS** | add the lead on the side of the new break of structure (sized so the BE price is ≤ 1 ATR away) |
   | **LOCK & WAIT** | buys = sells until the next BOS, then lift the losing side |
   | **TRIM** | close the worst leg when the basket is net in profit on the other side |
   | **CUT HALF** | close half the gross, the worst legs first (only when the neighbours say recovery is unlikely) |

   The score is the expected minutes to $0 × the risk of a deeper drawdown, and the move with the lowest score wins.
4. **Show it.**
   - A banner on the chart: “🛟 RECOVERY: LEAN WITH BOS ↑ — 23 similar baskets, 78 % got back to $0 in a median of 41 min (HOLD: 55 %, 96 min)”.
   - The BE line blinks.
5. **Do it.**
   - DEMO / BACKTEST / RACE: it acts on its own.
   - A real account: it acts on its own only with FULL AUTO on. Otherwise it is a 💡 suggestion with one click.
   - Every choice and its result is saved, which makes the next search better.

---

## 7. Pause and resume, automatically

| Pause when | Resume when |
|---|---|
| High-impact news (−15 / +15 min, the basket locked or flat) | 15 min after, the spread back under 0.25 ATR |
| Chop (§3.3) | 2 clean M5 bars with a regime |
| 2 losing baskets in a row | 30 min later, at half size |
| The day loss hit | the next NY day |
| A connection / bridge drop | the heartbeat back + the positions re-read (adopted, never doubled) |
| The spread > 0.4 ATR | the spread back to normal |

Every pause shows **why** and **until when** on the chart.

---

## 8. What the chart shows (all live, the engine's own words)

- **Entry**:
  - a flashing arrow;
  - a label “PBK long 78 — pullback to VWAP in an up-trend, 0.15 lot”.
- **Hedge line**:
  - its price, its size and why it sits there (key level, drawdown-closer, trend);
  - it blinks when the price is within 1 ATR of it.
- **Target / break-even / trail lines**:
  - each line with its $ amount.
- **Danger meter**:
  - the meter itself, and its biggest cause in words.
- **Pause banner**:
  - the reason and the resume condition.
- **The state header**:
  - REGIME • SETUP SCORES NOW • BASKET • DANGER • NEXT ACTION.

---

## 9. Recording and learning (the core)

**Saved every second while a basket is open, and on every event:**
- the time;
- the bid / ask / spread;
- every leg;
- the basket $;
- the margin;
- danger and its parts;
- the regime;
- the hedge line and why it sits there;
- the action taken and its reason.

Files: `full-auto-YYYY-MM-DD.csv` plus the existing `iterations`, `signals`, `blowups` and post-mortems.

**How it learns** (all of these exist as building blocks: SMART, POST-MORTEM, RACE):

1. **Post-mortem** of every closed basket: replayed with the alternatives (closer hedge, bigger lead, lock, early BE, no entry).
2. **Shadow race**: 6–8 variants of the live decisions run in the background on the same prices (in-memory brokers, never MT5).
3. **The edge table**: setup × regime × session × extension bucket → the win rate, payoff and recovery time. It updates after every result.
4. **Self-tuning**:
   - On DEMO / BACKTEST, the distances, leads and targets drift toward the best shadow variant, at most 10 % per day.
   - On a REAL account, a change is only **proposed** (💡). You accept it.

---

## 10. The build plan (in order, each shipped and tested on its own)

| Step | What | Already there |
|---|---|---|
| 1 | Drawdown-closer hedge + drawdown lead + 🛟 rescue to breakeven | ✅ 05u / 05v |
| 1b | The recovery planner (§6b): BOS + the nearest similar baskets → the fastest way to $0 | — |
| 2 | Regime + extension + swing-depth readers; the state header on the chart | partly (TrendNow, Vwap, EMAs, Danger) |
| 3 | The setup scorer + the over-extension filter + one-direction mode (limits in deep swings) | signal journal ✅, setups ✅ |
| 4 | Dynamic size from the score / volatility / account state | — |
| 5 | Adaptive lead + lock decision from the danger meter; the exit ladder | Risk Guard / Danger ✅ (static) |
| 6 | Trail + add to the winner under momentum | scale-out ✅, add-to-winner (manual setting) ✅ |
| 7 | Auto pause / resume (news, chop, losing streak, spread) | news ✅, Danger ✅ |
| 8 | Per-second recording + the shadow race of the live decisions + self-tuning | POST-MORTEM ✅, RACE ✅, SMART ✅ |
| 9 | The 🤖 FULL AUTO switch + the risk dial; FULL AUTO = these rules, no other fields | — |

- Each step is checked on the saved gold history (2025 + 2026) against DEFENDER before it becomes the default.
- A step that is worse in the replay ships **off** and is said so, as RecoverExit and SMART were.
