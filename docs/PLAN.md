# KEYSTONE PLAN — what we work on (updated 2026-10-03)

## Rule
One idea → a 5-line SPEC CARD written back by Claude → user confirms → build → user runs + exports → Claude gives a
GO / NO-GO verdict with numbers. No new feature until the open verdicts are done.

## Track A — PROP: pass evaluations fast (MNQ + MGC rotation) • tool: ROTATION TESTER
- MNQ + MGC opened together, same direction, same minute; N contracts each.
- Per rotation: combined target / stop / 3 lock tiers; pause; next account in turn.
- Per account: daily target / daily stop / 2 losses → done; each account is a $120 evaluation ($3K target, $2K EOD DD).
- Question: which target, stop, contracts, window, direction rule, pause gives the CHEAPEST PASSED EVALUATION, every year?
- Answer = cost per pass + days to pass, per year. Status: built (10-03f/g) • waiting for the user's run + export.

## Track B — PROP / LIVE: FIRST 5M FVG STUDY (MNQ + MGC) • tool: MOVE STUDY → "FIRST 5M FVG STUDY" (build 10-03h)
SPEC (confirmed by the user): per instrument and day the FIRST bullish 5M FVG after the start (MNQ 09:30, MGC 08:00).
Entry variations, each its own set: TOUCH the gap top • 25% dip • 50% dip • GREEN CLOSE + BREAK of its high •
PRIOR untouched FVG (formed 18:00 → start) that the opening candles dip into (touch, 50%). No BH. Entries identified and
measured to the close first (for / against / before best / close, per year, vs buying at the start); contract sizes later.
Data: lab Step 1 → PROP BRACKET (1-minute MNQ + MGC), BOTH, dates, START → MOVE STUDY opens on the study.

## (old Track B notes) gold 5M FVG retest • tool: GOLDEN + MOVE STUDY
- Entry: first bullish 5M FVG after 08:00 ET, RETEST + BREAK (back into the gap, green close, break of its high);
  aggression recorded, not required. MNQ from 09:30 for comparison.
- Measured to the close: for us / against us / against before best / close; per year; vs buying at 08:00 (no setup).
- Question: does it beat no setup, every year? Then target / stop in points and $ per year.
- Status: built (10-03e/g) • waiting for the user's run + export.

## Frozen (kept, not developed): BH, ASIAN 75, HELIX, RECOIL, PROP BRACKET sweet spot, old Step 3 tabs.
## Later (only after a GO): funded payouts after passing in the ROTATION TESTER; PROP PLANNER for account count / budget ($10K).
