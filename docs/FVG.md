# FVG retest long — rules as explained by the user (2026-09-26)

Status: specification only, not built yet. Open questions at the bottom must be answered (or a
default chosen) before implementation. BH and Asian 75 code paths are not touched.

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

## Open questions for the user

1. Timeframes: is the FVG found on the **same** timeframe as the entry candles (1M FVG → 1M
   entries), or always on the **5-minute** chart with entries on the chosen timeframe? (Lab can
   offer both.)
2. Can the candle that goes into the zone be the green confirmation itself if it closes green?
3. If the candle after the green one does **not** break its high, does the setup wait for the
   next green close (new reference), or is that retest finished?
4. How long does a zone stay valid — same session only, or can yesterday's zone be traded today?
5. After an entry from a zone, must price retest again before the next entry, or can the next
   green candle + break (without a new retest) be another entry?
6. "Market close" for the window: 15:55 ET (as BH/Asian) or 17:00 ET?
7. What counts as "aggression": e.g. X red candles in a row, or a drop of ≥ N points within
   M candles before the FVG?
8. Must the FVG's middle candle be green (displacement candle), or any 3 candles with a gap?
