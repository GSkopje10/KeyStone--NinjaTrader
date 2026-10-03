# IDEAS — test a strategy written in words

MATH LABS → **IDEAS • TYPE A STRATEGY IN WORDS**. The window uses the 1-minute bars already loaded in Step 1.
To load them: Step 1 → MICRO A DAY → BOTH → DATE RANGE 2020-01-01 → yesterday → confirm → REQUEST → BUILD.
Any strategy that loads 1-minute bars works.

Type one line and press RUN. Each comma list is tested in every combination. The rows are ranked by
money: the plain account and prop (evaluations, payouts, first payout, copy trading, months, years,
walk-forward, FLIP). In TRADES, click a trade to open the chart on that day with that row's trades
(buys and sells); ▶ PLAY replays them.

## Words

| word | meaning |
|---|---|
| `MNQ` `MGC` `BOTH` | instruments; BOTH also adds the combined MNQ + MGC rows |
| `AT 0930[,1000]` | entry time (New York) |
| `BUY` `SELL` `EITHER` | sides; EITHER (FVG only) takes both sides but never against an open trade |
| `ORB n` / `FADE n` | first break of the n-minute range after the time / the opposite side of that break |
| `FVG n` | every n-minute fair value gap (bullish: candle 3's low above candle 1's high; bearish: the mirror) |
| `FILL CLOSE,50` | FVG: in when candle 3 closes, or a limit at the gap's middle (waits 60 minutes) |
| `GAP pts` / `MGCGAP pts` | minimum gap size (stronger vs weaker gaps) |
| `SESSION ASIA,LONDON,NY,ALL` | FVG windows 18:00–03:00, 03:00–09:30, 09:30–15:55, 18:00–15:55 |
| `FROM 0930 TO 1100` | your own window |
| `ACCOUNTS n` | FVG: each new setup goes to the next free account (round robin); every account is its own evaluation → funded life |
| `TP pts` | target (MNQ points; gold = ÷ 10 unless `MGCTP`; MGC alone = gold points) |
| `SL pts` / `NONE` / `C1` | stop; C1 = beyond the FVG's first candle |
| `EXIT 1555` | out at this time (FVG default: the session end) |
| `QTY 1,2` | contracts |

## Fill rules (no invented prices)

- A minute that touches both the stop and the target counts as the stop.
- A gap through the stop fills at the open.
- A limit fill's minute cannot reach the target.
- Costs come from `KeystoneMoveStudy.Cost` (commission both sides + 1 tick).
- A trade from 18:00 on belongs to the next date's prop day.

## Ready-made ideas (the dropdown)

1. 09:30 open, SELL vs BUY, targets 25 → 200 (your example).
2. Rotation: every 1-minute FVG, sells too, 1 / 3 / 5 accounts.
3. Rotation by session: Asia, London, New York.
4. Strong vs weak gaps; candle-3 close vs 50% tap.
5. First 90 minutes, 1-minute FVG scalps, 3 / 5 accounts.
6. Opening range breakout 5 / 15 / 30.
7. Fade the first break of the opening range.
8. Gold COMEX open 08:20, 10-minute breakout.
9. 08:30 news candle break, out by 09:30.
10. 10:00 / 10:30 / 11:00 scalps.
11. 18:00 Asia open, both instruments, out at 03:00.

None of these is proven. Each is a question for the data, and the ADVICE tab says which rows made money
in every year.
