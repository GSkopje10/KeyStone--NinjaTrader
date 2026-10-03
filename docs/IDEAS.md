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
| `FILL CLOSE,25,40,50,BREAK` | FVG: in when candle 3 closes • a limit 25 / 40 / 50% into the gap (from its near edge, waits 60 minutes) • BREAK = price comes back into the gap, a candle closes our way, then the break of its high (low for a sell); cancelled beyond candle 1 |
| `STRICT` | limit fills only when price trades one tick through the level (no fill on a touch) |
| `BE pts` / `MGCBE` | breakeven: after +pts the stop moves to the entry (0 = off) |
| `PARTIAL pts` / `MGCPARTIAL` | half the position off at +pts, the rest to the target / stop |
| `DAYTARGET $` / `DAYSTOP $` | FVG: an account that banked +$ (or lost $) today takes no more setups that day |
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

## Results on the first real export (Apr 2 → Oct 2 2026, 6 months — NOT proof)

- 1-minute FVG at the candle-3 close in New York: 0 of 243 combinations made money (costs + noise).
- The same setups with a limit 25 / 40 / 50% into the gap: about +$7,800 per micro (TP 30–40, stop past
  candle 1, buys + sells, 1 account), 6 of 7 months up. With STRICT fills: +$5,843 — the edge is not only
  touch fills.
- A $1,000–1,500 day target per account kept most of the money with a smaller drawdown; breakeven and
  partials lowered the results on this data.
- Asia (18:00–03:00) MNQ 1-minute FVG, 3 accounts × 5 micros: positive but front-loaded.
- Gold 1-minute FVG: nothing made money.

## Replay trader

MATH LABS → REPLAY TRADER. It opens with the 1-minute bars loaded in Step 1, or with the bars saved on this PC by
earlier loads. Pick an instrument, a session (18:00 → 17:00 New York), a start time and LOAD DAY, then ▶ PLAY.

- Speed follows the wall clock: REAL TIME = one minute of chart per 60 seconds; 2× … 240× are exact multiples.
- The clock shows New York time and how long until the current candle closes (1M … 4H, D).
- Mouse wheel = zoom, drag = scroll back / forward, LIVE ▶| = back to the newest candle.
- Orders: MARKET, LIMIT or STOP (click the chart to set the price), contracts, stop / target in points. Drag the
  STOP, TARGET or a working order's line to move it. CANCEL ORDERS removes the working orders. Keys B / S / C,
  space = play / pause.
- Tools: horizontal lines and trend lines (two clicks), CLEAR DRAWINGS.
- One account: ACCOUNT START $ is the whole drawdown — at $0 the account is blown. The balance carries over from
  day to day (Documents\KeystoneArc5MResearch\ReplayAccount.txt) until NEW ACCOUNT. END DAY + SAVE closes the day:
  every trade is in ReplayJournal.csv and each day in ReplayDays.csv.
- Each minute's open, high, low and close are real; the order of the ticks inside a minute is simulated.
