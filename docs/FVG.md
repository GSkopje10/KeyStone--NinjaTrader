# FVG retest long — rules as explained by the user (2026-09-26)

Status: BUILT (build 26f). Engine `KeystoneArcEngine.FvgScan` / `DetectFvgRetest`, tests in
`tests/FvgTests.cs`, Step 1 option "FVG • RETEST + BREAK LONG". BH and Asian code paths are not
touched (guarded by `tests/bh_detection_baseline.txt` and `tests/lifecycle_baseline.txt`).

## Idea

After an aggressive bearish move ("scary manipulation"), price reverses up and leaves a bullish
fair value gap (FVG). Later price comes back into the gap, holds it, prints a green candle, and
the next candle breaks that green candle's high → long entry (same entry mechanic as BH).
Aggression before the FVG is preferred but not required (aggressive ones are better setups).

## Definitions

- **Bullish FVG** (3 candles on the selected timeframe): candle 3 low > candle 1 high.
  Zone = candle 1 high (bottom) → candle 3 low (top). Gap size = top − bottom.
  Bigger gaps are usually better; gap size is recorded on every setup so the lab can show which
  sizes win or lose.
- **Invalidation**: any candle **closes below the zone bottom** → zone is dead, no more entries.
- **Retest (touch)**: a later candle's low goes into the zone. Not a mere touch — it should go
  deeper, ideally to the midpoint. Parameter: minimum penetration % of the zone
  (0 = any touch, 50 = midpoint).
- After the retest, 0, 1, 2 … candles may follow that are not green, as long as none closes
  below the zone.
- **Green confirmation**: the first candle that **closes green** after the retest. Its high is
  the reference.
- **Entry**: the next candle trades above the green candle's high → long at that high.
- **More entries**: the same zone can give another entry later (screenshot 2: the first entry
  candle closed red, then another green candle + break gave a second valid entry).
- **Overlapping zones**: a retest can create a new FVG at the same time (screenshot 3); each
  zone is tracked on its own and can give its own entries.

## Scope

- Instruments: MNQ and MGC. User expects MGC 08:00 ET → close to work best.
- Timeframes: 1, 5, 15, 30, 60, 240 minutes — all testable and all on the chart.
- Risk (stop / target / size): parameters, decided later and optimized.
- Accounts: rotation assigns setups across accounts; copy / personal take fewer setups. The
  existing pool, lifecycle, payout, report and optimizer apply unchanged.
- Chart: draw each FVG box from formation until invalidated / expired, mark retest, green
  confirmation, entry and exit; replay with live data box, same style as BH/Asian.

## Parameters planned

| Parameter | Default (proposal) |
|---|---|
| Timeframe | 5 |
| Instruments | MGC (MNQ selectable) |
| Session window | 08:00 ET → close |
| Minimum gap size (points / ticks) | 0 (record, learn later) |
| Minimum retest depth % of zone | 25 (0 = touch, 50 = midpoint) |
| Bearish aggression before FVG | TAG ONLY (options: OFF / TAG ONLY / REQUIRED); N candles + minimum drop |
| Entry must break on the very next candle | YES; otherwise wait for the next green close |
| Max entries per zone | unlimited (parameter) |
| Zone lifetime | until invalidated or session end (parameter: N candles / same day / multi-day) |
| Stop | zone bottom / green-candle low / fixed $ (parameter) |
| Target | fixed $ / R multiple (parameter) |

## Answers from the user (2026-09-26)

1. **Timeframe**: the timeframe chosen at the start is used for everything — FVG detection,
   retest, green confirmation and entry. No mixing.
2. **Green dip candle counts**: the first green candle that dips into the zone is itself the
   confirmation.
3. **No break on the next candle**: option. Default = wait for another dip / touch into the zone,
   then a new green close.
4. **Zone reuse**: a zone is fresh until used. Once it has been touched and price moved away
   (entry or not), it is not traded again by default. Parameter: how many times a zone may be
   reused (default 1 = one use).
5. **After an entry**: wait for the next **new** FVG; same rules.
6. **Time zone**: always New York time for every strategy (user lives in NM, 2 hours behind,
   but everything is NY). CME Globex closes 17:00 ET (daily halt 17:00–18:00); the last 5-minute
   candle is 16:55–17:00. Reference times: pre-NY session 08:00, NY stock open 09:30.
   Session window start/end are parameters (default 08:00 → 16:55 ET last entry, flat by close).
7. **Aggression**: examples coming. Two parameter types: N bearish candles before the reversal,
   or a downside move of ≥ $X / points before the reversal. Plus OFF / TAG ONLY / REQUIRED.
8. **Candle colors**: any colors — only the gap matters (candle 3 low > candle 1 high).

## Resulting default logic

```
for each bar on the selected timeframe (NY time, inside the session window):
  new bullish FVG (bar[i].Low > bar[i-2].High)      -> fresh zone [bar[i-2].High, bar[i].Low]
  zone: any close < bottom                          -> dead
  zone fresh, bar low inside zone deep enough       -> retest started (zone now "used")
     bar closes green                               -> confirmation (reference = bar high)
     else wait; later bar closes green (no close below bottom) -> confirmation
  next bar high > reference                         -> ENTRY long at reference; zone finished
  next bar does not break                           -> default: need another dip into zone, then
                                                       a new green close (option: any green close)
  price runs above top + run-away distance          -> zone retired (0 = never; close below always kills)
  price stays near the zone                         -> every new dip can start a new retest
  after an entry                                    -> only a NEW FVG can give the next entry
```

## Still open

- Aggression (example received 2026-09-26: a run of mostly red candles, accelerating, the last
  ones large, straight into the reversal that forms the FVG). Plan: reuse the BH "STRONGER"
  measures so both strategies speak the same language — **red candles in a row** before the
  FVG's first candle (MNQ / MGC separately) and **drop size** (MNQ points, MGC $) from the
  top of that run to its low; combine ANY / ALL; mode OFF / TAG ONLY / REQUIRED. Extra option:
  allow 1 small green candle inside the run (so a pause does not reset the count). Every setup
  records its red-count and drop so the results can show whether stronger aggression wins.
  More examples welcome.
- Risk defaults (stop / target / size) — to decide and optimize.
- (answered) "Moved away": gold often revisits a box several times. While price stays
  relatively close above the box, every new dip can still give a setup. Only a close below the
  box kills it. If price runs far above the box, the box is retired by default.
  Parameters: **RUN-AWAY DISTANCE** = how far above the box top price may go before the box is
  retired (multiple of box height or points; 0 = never retire, only a close below kills it) and
  **MAX ENTRIES PER BOX** (default 1). Both get optimized; the lab records every revisit so the
  results show whether 2nd/3rd visits win.

## Fix 2026-09-27 (build 26-09-27b)
Run-away now means the WHOLE candle (its low too) trades above the run-away distance. Before,
a candle that dipped into a small box but spiked high retired the box, so the user's MGC
2026-09-23 10:55 setup was missed. Regression test: tests/FvgTests.cs #6b.
