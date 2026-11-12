# 🪜 2ND-SWING HEDGE (09o) — the bench and the user's case

**The rule (user, 12 Nov):** while a basket is in drawdown, hedge at the break of the 2nd swing low (net long) / high (net short)
formed after the entry; if the price does not continue and comes back, flip back at the swing high / low.
`AutoSettings.HedgeTrigger = SWING2` (LEAD = the fixed-lead break hedge stays the default). Pure logic in `mt5/app/SwingHedge.cs`,
wired in `HedgeAuto.Swing2Level` + the gate before the add-on order; every order still goes through `Ask → AutoSend`.

Options: N = 1 / 2 / 3 swings (default 2), MIN SWING × ATR (0.3) / ticks (0), COOL-DOWN (3 min), TOUCH / CLOSED candle,
RANGE-ONLY AFTER N HEDGES (the chop guard, default 2), the MAX HEDGE DISTANCE from the newest leg as the safety.

## Bench (`mt5/study/Swing2Bench.cs.txt`)

Engine = the app's own `HedgeAuto` on the BACKTEST's `SimBroker`; his DEFENDER settings of the 12 Nov export (STEPLEAD, range-only,
max distance 5, compress ALWAYS, …), $8,000, 1:500, stop-out 50 %, spread 35 points, $7 a lot, swap; 💥 AUTO RESTART like the DEMO.
Data: the repo's 1-minute gold (data/MGC, Jun 2020 – Oct 2026, futures as a stand-in for XAUUSD, ticks synthesised in each candle).
"net" = Σ closed iterations + the iteration still open at the end of each period (marked to market).

| variant | net (closed + open) | blow-ups | hard stops | iterations | hedges / iteration | avg iteration | worst iteration |
|---|---|---|---|---|---|---|---|
| **LEAD (today's default)** | **−$87,332** | 10 | 38 | 1,484 | 2.06 | 16 h | −$9,059 |
| 🪜 SWING2 N = 1 | −$32,365 | 3 | 14 | 624 | 2.29 | 41 h | −$9,284 |
| **🪜 SWING2 N = 2 (shipped)** | **−$42,567** | 6 | 16 | 695 | 2.32 | 35 h | −$6,627 |
| 🪜 SWING2 N = 3 | −$23,343 | 4 | 13 | 649 | 2.15 | 50 h | −$6,728 |
| 🪜 N = 2 + CLOSED candle | −$15,479 | 1 | 8 | 499 | 1.56 | 68 h | −$5,627 |
| 🪜 N = 2 + cool-down 15 min | −$31,470 | 5 | 19 | 846 | 2.14 | 40 h | −$5,935 |
| 🪜 N = 2 WITHOUT the chop guard (the literal rule) | **−$614,008** | **79** | 223 | 1,503 | 4.90 | 19 h | −$7,424 |

Per year, N = 2 vs LEAD (closed iterations; open at the end in brackets): 2020 −$4,661 (−523) vs +$2,584 (−257) • 2021 +$62 (−3,585: ONE basket
open from 5 Jan all year) vs −$4,231 • 2022 +$3,600 vs −$7,119 • 2023 −$6,635 vs −$7,232 • 2024 −$7,118 (−2,700) vs −$6,200 (−1,861) •
2025 −$29,310 (−1,178) vs −$13,278 (−980) • 2026 +$10,258 (−777) vs −$48,467 (−291).

### Honest reading

* **Everything loses in this bench** (DEFENDER itself does: −$87k over six years with 10 resets). SWING2 loses less and blew up
  fewer times, but mostly because it trades less: half the iterations, baskets ~2× longer, and one basket sat open for a year (2021).
  2025 was clearly worse with SWING2 (−$29k vs −$13k). It is not a proven edge.
* **The literal rule is dangerous**: flipping back at every most-recent swing inside a chop with STEPLEAD sizing grew the lots
  hedge after hedge (−$614k, 79 blow-ups). That is why the shipped version has the chop guard ON (after 2 hedges a hedge goes only
  beyond the basket's range) — the user's two steps (2nd-swing hedge → flip at the swing high) are kept, the third hedge on is range-only.
* The data are futures 1-minute candles with synthetic ticks; the 🏁 RACE lane SW2 (in ▶ COMPARE WITH MY SETTINGS) measures it on
  his own prices next to MINE.

## The user's case (12 Nov, DEMO XAUUSD, BUY 0.60 @ 3881.09)

The latest export (exp1112, candles + real ticks to 17:12 server) has 3881.09 traded once: 16:52:02 server (= 09:52 NY — not 15:30–16:00 NY).
The screenshot's moment (bid ≈ 3873.19, the pending LEAD hedge SELL 0.87 @ 3872.14 = the basket's lowest leg 3877.14 − the max distance 5)
fits 16:55–16:56 server. The rest of his basket (other legs, −$317.50 already closed) is not in the export, so the replay
(`mt5/study/Swing2Case.cs.txt`) opens a fresh BUY 0.60 at 16:52:02 on the real ticks and carries the −$317.50 as the iteration's closed part.

| | hedge | total at the worst | total at 17:12 (end of data) |
|---|---|---|---|
| LEAD (this reconstruction: no lower legs, so no range-only push) | SELL 0.68 @ 3879.79, 16:55:28 (the 16:51 swing low 3879.85 broke) | −$413.80 | −$197.56 (open) |
| 🪜 SWING2 N = 2 | SELL 0.68 @ 3874.83, 16:56:07 — the MAX DISTANCE 3876.09 (no 2nd swing low formed after the buy: the fall was straight) | −$701.46 | −$527.40 (open) |

With SwingK = 2 (his setting) no swing low completed between the buy and the 16:56 drop, so SWING2 could only hedge at the max distance.
What put his real hedge at 3872.14 was RANGE-ONLY (beyond the basket's lowest leg) + the max distance measured from that leg;
in the reconstruction without those lower legs, the existing break hedge (LEAD) already sat at the swing low he pointed at.
