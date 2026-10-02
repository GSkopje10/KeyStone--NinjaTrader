# MOVE STUDY — one test for every strategy (build 10-03e)

Header button **MOVE STUDY**. It measures the entries of the last run — any strategy — on real 1-minute bars.

1. In the lab, Step 1: choose the strategy, instruments, dates (e.g. 5 years), start time; press START (as always).
   - **GOLDEN SETUP** is the richest: it detects FIRST SETUP, FIRST BH, FIRST 5M FVG and EVERY 5M FVG at once and the move
     study compares them all, plus "aggression only" versions.
   - Any other strategy (BH, FVG, ASIAN, RECOIL…) is measured as one set.
2. Open **MOVE STUDY** (it measures right away; MEASURE THE LAST RUN repeats it after a new run).

**What is measured for every entry (no target, no stop), from the fill minute to FOLLOW UNTIL (default the GOLDEN close):**
- FOR US = the best point above the entry (long) • AGAINST = the worst point below it
- AGAINST BEFORE BEST = how far it went against us before its best point (the stop needed to stay in)
- CLOSE = where it ended vs the entry • ENDED UP = closed in our favour
- for every move level (MNQ 10…300 pts, MGC 1…30 pts): was it reached, and how far against us first

**What you get:**
- **RANKING** of every entry set × instrument against **NO SETUP** (buy at the start time every day): ended up %, median for /
  against, the **best target / stop picked from these moves** (win if the target came first, loss if the stop came first —
  the same minute counts as the stop — else closed at the close), $ per trade (1 contract, after costs), $ per year, and the same
  target / stop on each year alone (✓ = positive every year).
- **DETAILS** for the clicked set: year by year (for / against at 50 / 75 / 90%, best target / stop THAT year, its $),
  HOW FAR DID IT GO (% reaching each level, per year), and **WHY** (setup type, aggression, entry hour, weekday, FVG gap size,
  push down before, year — each with the $ per trade).
- **EXPORT**: HTML report + a CSV with every entry and every measure — attach both to Claude.
