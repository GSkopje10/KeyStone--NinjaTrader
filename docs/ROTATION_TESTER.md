# ROTATION TESTER (build 10-03f)

Header button **ROTATION TESTER**. The live MNQ + MGC rotation replayed on real 1-minute history.

**Data:** lab Step 1 → PROP BRACKET, BOTH, your dates (end yesterday or earlier), START. Any load with both instruments on
1-minute bars works.

**Rules simulated:** both instruments opened together on the minute's open (BUY BUY, SELL SELL, RANDOM, CONFIRM = both moved the
same way over the last 5 minutes → follow, or FADE). One combined target / stop per rotation, the 3-tier profit lock
(peak : lock), the pause, then the next account in turn. Per account: daily target, daily stop, N losses → done for the day.
Every account is an evaluation (cost, target, EOD trailing drawdown that stops at +$100, consistency, min days); passed or
blown accounts are replaced the next day. Inside a minute the worst combined price counts first. Costs: commission per
contract per side + slippage ticks on stop / lock exits.

**Tabs:** RESULT (cost of 1 passed evaluation, pass rate, days to pass, rotations, per rotation after costs, exits by reason,
year by year) • EVERY DAY • MNQ + MGC TOGETHER (per 30-minute slot: % of 5-minute windows where both moved the same way and
the average combined $ move) • OPTIMIZER (2,025 combinations: direction × window × contracts × target / stop × pause, ranked
by cost per pass, with each year) • HOW IT WORKS. EXPORT CSV writes every rotation and the optimizer table.

**Limits:** 1-minute bars cannot see seconds (25 vs 45 s pause, 11 s hold); RANDOM uses one fixed seed.
