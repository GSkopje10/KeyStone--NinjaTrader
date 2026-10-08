# MGC TEN-R STUDY • every session • stop 1 / target 10 pts • 10 micros

1521 sessions 2020-06-29 .. 2026-10-02 (every session with an hour of bars or more) • 23,259 separate moves drawn in the report
Win = +$988 • loss = $-122 (1 tick slippage on the stop + fees) • break-even win rate 11.0%
IS = 2020–2023 (where rules are chosen) • OOS = 2024–2026 (never used to choose — the honest test)

## 1. Each feature alone (both sides, all hours)
| feature | IS entries | IS win % | IS $/entry | OOS entries | OOS win % | OOS $/entry |
|---|---|---|---|---|---|---|
| ANY MINUTE (baseline) | 2,171,278 | 6.60% | -25.59 | 1,894,898 | 7.90% | -23.76 |
| WITH VWAP (buy above / sell below the session VWAP) | 1,085,626 | 6.57% | -26.90 | 947,443 | 8.17% | -21.14 |
| BREAK 30 (close beyond the last 30-minute high / low) | 87,619 | 6.56% | -31.20 | 77,477 | 7.40% | -31.81 |
| BREAK 60 (close beyond the last 60-minute high / low) | 55,891 | 6.62% | -32.56 | 50,608 | 7.35% | -33.55 |
| MOMENTUM 5 (≥ 2 pts in the trade's direction over 5 minutes) | 55,704 | 7.76% | -25.41 | 269,303 | 8.38% | -26.91 |
| BIG BAR (range ≥ 2× the last 20 bars, closing the trade's way) | 93,633 | 6.53% | -30.41 | 46,563 | 7.54% | -28.48 |
| NEW SESSION HIGH / LOW (the bar breaks the session's extreme) | 37,680 | 7.43% | -33.81 | 37,438 | 7.77% | -33.96 |
| EMA TREND (with EMA 20 and EMA rising / falling) | 916,973 | 6.60% | -27.62 | 801,142 | 7.85% | -25.15 |
| PULLBACK IN TREND (EMA trend, last bar against, this bar with) | 158,434 | 6.61% | -28.84 | 178,794 | 7.97% | -25.09 |
| QUIET (average 1-minute range ≤ 0.6 pts) | 1,648,834 | 6.34% | -26.68 | 441,584 | 6.76% | -22.00 |
| WILD (average 1-minute range ≥ 1.5 pts) | 46,694 | 8.59% | -22.10 | 731,550 | 8.82% | -23.37 |
| SWEEP + RECLAIM (took the 30-minute low / high, closed back the trade's way) | 21,153 | 6.48% | -29.17 | 27,201 | 8.08% | -24.00 |
| STRETCHED (≥ 3 average ranges away from VWAP AGAINST the trade = snap-back) | 801,274 | 6.23% | -24.59 | 664,691 | 7.35% | -26.64 |

## 2. Time of day alone
| window | IS win % | IS $/entry | OOS win % | OOS $/entry |
|---|---|---|---|---|
| 18:00–22:00 (Asia open) | 8.32% | -28.28 | 8.94% | -22.92 |
| 22:00–02:00 | 8.48% | -26.23 | 9.40% | -17.73 |
| 02:00–06:00 (London) | 8.52% | -25.12 | 8.58% | -26.60 |
| 06:00–10:00 (NY open) | 8.28% | -23.92 | 8.71% | -24.53 |
| 10:00–14:00 | 3.78% | -25.55 | 6.90% | -29.26 |
| 14:00–17:00 | 0.54% | -24.19 | 3.75% | -20.62 |

## 3. Best rules — ranked on IS only (1,160 rules tested), then their untouched OOS
| # | rule | window | IS entries | IS win % | IS $/entry | OOS entries | OOS win % | OOS $/entry | holds up? |
|---|---|---|---|---|---|---|---|---|---|
| 1 | WILD + STRETCHED | 18:00–22:00 (Asia open) | 403 | 16.6% | 62.14 | 22,784 | 9.1% | -21.45 | no |
| 2 | WITH VWAP + MOMENTUM 5 + WILD | 14:00–17:00 | 628 | 11.1% | 13.84 | 8,938 | 8.5% | -20.15 | no |
| 3 | MOMENTUM 5 + EMA TREND + WILD | 14:00–17:00 | 772 | 10.6% | 7.99 | 12,099 | 8.1% | -24.71 | no |
| 4 | MOMENTUM 5 + WILD | 14:00–17:00 | 1,034 | 9.5% | 0.80 | 16,238 | 7.9% | -26.70 | no |
| 5 | BREAK 60 + BIG BAR + STRETCHED | 22:00–02:00 | 414 | 10.9% | 0.35 | 187 | 7.5% | -39.30 | no |
| 6 | PULLBACK IN TREND + QUIET + STRETCHED | 18:00–22:00 (Asia open) | 961 | 10.6% | -3.79 | 208 | 10.6% | -1.34 | no |
| 7 | WITH VWAP + MOMENTUM 5 + EMA TREND | 14:00–17:00 | 1,666 | 6.9% | -3.95 | 10,983 | 7.4% | -22.92 | no |
| 8 | EMA TREND + PULLBACK IN TREND + WILD | 14:00–17:00 | 447 | 8.5% | -4.84 | 6,549 | 7.9% | -23.82 | no |
| 9 | PULLBACK IN TREND + WILD | 14:00–17:00 | 447 | 8.5% | -4.84 | 6,549 | 7.9% | -23.82 | no |
| 10 | EMA TREND + WILD | 14:00–17:00 | 1,828 | 8.8% | -5.06 | 26,104 | 7.9% | -23.93 | no |
| 11 | WITH VWAP + MOMENTUM 5 | 14:00–17:00 | 1,823 | 6.9% | -5.21 | 13,204 | 7.5% | -22.63 | no |
| 12 | BREAK 30 + MOMENTUM 5 + BIG BAR | 14:00–17:00 | 410 | 4.9% | -6.11 | 1,299 | 5.5% | -33.82 | no |
| 13 | WILD | 18:00–22:00 (Asia open) | 4,650 | 10.5% | -6.26 | 126,080 | 9.2% | -20.04 | no |
| 14 | WITH VWAP + PULLBACK IN TREND + WILD | 06:00–10:00 (NY open) | 1,720 | 10.4% | -6.38 | 10,135 | 9.2% | -20.13 | no |
| 15 | MOMENTUM 5 + BIG BAR + NEW SESSION HIGH / LOW | 18:00–22:00 (Asia open) | 577 | 10.4% | -6.98 | 1,519 | 8.0% | -33.25 | no |
| 16 | MOMENTUM 5 + PULLBACK IN TREND + STRETCHED | 06:00–10:00 (NY open) | 422 | 10.2% | -7.16 | 2,199 | 8.5% | -27.50 | no |
| 17 | MOMENTUM 5 + EMA TREND + PULLBACK IN TREND | 18:00–22:00 (Asia open) | 523 | 10.3% | -7.79 | 7,131 | 8.6% | -26.42 | no |
| 18 | MOMENTUM 5 + PULLBACK IN TREND | 18:00–22:00 (Asia open) | 523 | 10.3% | -7.79 | 7,131 | 8.6% | -26.42 | no |
| 19 | EMA TREND + WILD | 18:00–22:00 (Asia open) | 1,337 | 10.3% | -7.83 | 52,241 | 9.3% | -18.74 | no |
| 20 | BIG BAR + PULLBACK IN TREND + STRETCHED | 22:00–02:00 | 675 | 10.1% | -7.97 | 320 | 7.8% | -35.68 | no |
| 21 | WITH VWAP + WILD | 14:00–17:00 | 2,093 | 8.4% | -8.28 | 30,472 | 8.6% | -14.79 | no |
| 22 | MOMENTUM 5 + BIG BAR + NEW SESSION HIGH / LOW | 10:00–14:00 | 550 | 8.7% | -8.51 | 570 | 7.7% | -28.24 | no |
| 23 | WITH VWAP + EMA TREND + WILD | 02:00–06:00 (London) | 639 | 10.2% | -9.49 | 31,730 | 8.3% | -30.43 | no |
| 24 | WITH VWAP + EMA TREND + WILD | 06:00–10:00 (NY open) | 7,225 | 10.1% | -9.64 | 41,425 | 9.1% | -21.14 | no |
| 25 | WITH VWAP + WILD | 06:00–10:00 (NY open) | 10,816 | 10.1% | -9.66 | 76,693 | 9.2% | -20.36 | no |

5 rules made money per entry on IS; 0 of them also on OOS.

## 4. Traded one at a time (a signal is taken only when flat; first signal of either side)
| rule | window | trades | trades/yr | win % | net $ | IS net | OOS net | worst drawdown | longest losing streak | years up |
|---|---|---|---|---|---|---|---|---|---|---|
| WILD + STRETCHED | 18:00–22:00 (Asia open) | 6,916 | 1105 | 9.3% | -133,738 | 5,292 | -139,030 | 141,976 | 68 | 3/7 |
| WITH VWAP + MOMENTUM 5 + WILD | 14:00–17:00 | 3,498 | 559 | 9.0% | -51,155 | 934 | -52,089 | 56,561 | 83 | 3/7 |
| MOMENTUM 5 + EMA TREND + WILD | 14:00–17:00 | 4,619 | 738 | 8.4% | -102,335 | -154 | -102,181 | 105,709 | 86 | 3/7 |
| MOMENTUM 5 + WILD | 14:00–17:00 | 5,631 | 900 | 8.1% | -142,614 | -856 | -141,758 | 144,027 | 102 | 0/7 |
| BREAK 60 + BIG BAR + STRETCHED | 22:00–02:00 | 454 | 73 | 9.3% | -8,300 | -2,403 | -5,897 | 11,482 | 55 | 2/7 |

Reading it: at 1 : 10 a rule only needs to win a bit more than 1 in 10 — but it must hold on the OOS years it was not chosen on, and the losing streaks (column) are what an account has to survive: with 10 micros every loss is $122.
