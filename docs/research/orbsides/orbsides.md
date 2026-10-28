# MNQ: ORB + VWAP — BUYS vs SELLS, and the risk management

The rule: a **5-minute close above the opening range high while above the VWAP = BUY**, **below the low while below the VWAP = SELL**, market at the next minute. 1 MNQ contract, $ after costs ($1.74 + 1 tick on stops). Every rule is picked on **2020–2023** and checked on **2024–2026** (never seen while picking). R = the trade's result ÷ its own stop distance.

## 1. Buys vs sells — the rule with no management

Stop at the other side of the range, held to 15:55, one trade a day.

| ORB | side | period | trades | win | net $ | PF | max DD $ | avg R |
|---|---|---|---|---|---|---|---|---|
| 15 | ▲ BUY | 2020–23 | 433 | 50% | 9,579 | 1.29 | 2,950 | 0.12 |
| 15 | ▲ BUY | 2024–26 | 359 | 49% | 5,664 | 1.15 | 3,024 | 0.06 |
| 15 | ▼ SELL | 2020–23 | 392 | 43% | 3,797 | 1.11 | 2,215 | 0.05 |
| 15 | ▼ SELL | 2024–26 | 342 | 41% | 573 | 1.01 | 8,268 | 0.04 |
| 30 | ▲ BUY | 2020–23 | 416 | 55% | 7,570 | 1.25 | 2,537 | 0.07 |
| 30 | ▲ BUY | 2024–26 | 371 | 53% | 4,473 | 1.12 | 3,702 | 0.05 |
| 30 | ▼ SELL | 2020–23 | 384 | 47% | 2,612 | 1.08 | 2,470 | 0.03 |
| 30 | ▼ SELL | 2024–26 | 317 | 44% | -182 | 1.00 | 5,734 | 0.03 |

## 2a. ▲ BUYS only — the best risk management

608 of 1120 rules made money in 2020–23; of the best 10, **10 also made money in 2024–26** (their average there 6,221 $). Of ALL rules, 700 of 1120 made money in 2024–26.

| rule | 20–23 trades | win | net $ | PF | DD $ | avg R | 24–26 trades | win | net $ | PF | DD $ | avg R |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ORB 15 + VWAP side • stop range other side • target 15:55 • BE at +1R • entries until 12:00 • 1 trade a day | 428 | 46% | 11,120 | 1.38 | 2,252 | 0.14 | 353 | 45% | 4,562 | 1.14 | 2,621 | 0.09 |
| ORB 15 (no VWAP filter) • stop range other side • target 15:55 • BE at +1R • entries until 12:00 • 1 trade a day | 428 | 46% | 11,120 | 1.38 | 2,252 | 0.14 | 353 | 45% | 4,562 | 1.14 | 2,621 | 0.09 |
| ORB 15 + VWAP side • stop range other side • target 3R • BE at +1R • entries until 12:00 • 1 trade a day | 428 | 46% | 11,105 | 1.38 | 2,252 | 0.14 | 353 | 45% | 4,611 | 1.14 | 2,621 | 0.09 |
| ORB 15 (no VWAP filter) • stop range other side • target 3R • BE at +1R • entries until 12:00 • 1 trade a day | 428 | 46% | 11,105 | 1.38 | 2,252 | 0.14 | 353 | 45% | 4,611 | 1.14 | 2,621 | 0.09 |
| ORB 15 + VWAP side • stop range other side • target 15:55 • BE at +1R • entries until 15:00 • 1 trade a day | 433 | 46% | 11,102 | 1.37 | 2,252 | 0.14 | 359 | 45% | 8,111 | 1.25 | 2,621 | 0.10 |
| ORB 15 (no VWAP filter) • stop range other side • target 15:55 • BE at +1R • entries until 15:00 • 1 trade a day | 433 | 46% | 11,102 | 1.37 | 2,252 | 0.14 | 359 | 45% | 8,111 | 1.25 | 2,621 | 0.10 |
| ORB 15 + VWAP side • stop range other side • target 3R • BE at +1R • entries until 15:00 • 1 trade a day | 433 | 46% | 11,087 | 1.37 | 2,252 | 0.14 | 359 | 45% | 7,920 | 1.25 | 2,621 | 0.10 |
| ORB 15 (no VWAP filter) • stop range other side • target 3R • BE at +1R • entries until 15:00 • 1 trade a day | 433 | 46% | 11,087 | 1.37 | 2,252 | 0.14 | 359 | 45% | 7,920 | 1.25 | 2,621 | 0.10 |
| ORB 30 + VWAP side • stop range other side • target 3R • BE at +1R + trail 5-min candles • entries until 15:00 • 1 trade a day | 416 | 58% | 8,408 | 1.29 | 1,833 | 0.08 | 371 | 56% | 5,899 | 1.17 | 3,702 | 0.06 |
| ORB 30 (no VWAP filter) • stop range other side • target 3R • BE at +1R + trail 5-min candles • entries until 15:00 • 1 trade a day | 416 | 58% | 8,408 | 1.29 | 1,833 | 0.08 | 371 | 56% | 5,899 | 1.17 | 3,702 | 0.06 |

## 2b. ▼ SELLS only — the best risk management

438 of 1120 rules made money in 2020–23; of the best 10, **8 also made money in 2024–26** (their average there 1,891 $). Of ALL rules, 280 of 1120 made money in 2024–26.

| rule | 20–23 trades | win | net $ | PF | DD $ | avg R | 24–26 trades | win | net $ | PF | DD $ | avg R |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ORB 15 + VWAP side • stop range other side • target 3R • BE at +1R • entries until 15:00 • 1 trade a day | 392 | 38% | 5,211 | 1.17 | 1,836 | 0.08 | 342 | 34% | 479 | 1.01 | 7,239 | 0.03 |
| ORB 15 (no VWAP filter) • stop range other side • target 3R • BE at +1R • entries until 15:00 • 1 trade a day | 392 | 38% | 5,211 | 1.17 | 1,836 | 0.08 | 342 | 34% | 479 | 1.01 | 7,239 | 0.03 |
| ORB 15 + VWAP side • stop range other side • target 2R • none • entries until 15:00 • 2nd trade the other way | 533 | 47% | 7,033 | 1.16 | 2,537 | 0.09 | 448 | 44% | 5,256 | 1.11 | 5,120 | 0.07 |
| ORB 15 (no VWAP filter) • stop range other side • target 2R • none • entries until 15:00 • 2nd trade the other way | 533 | 47% | 7,033 | 1.16 | 2,537 | 0.09 | 448 | 44% | 5,256 | 1.11 | 5,120 | 0.07 |
| ORB 15 + VWAP side • stop range middle • target 15:55 • none • entries until 15:00 • 1 trade a day | 392 | 33% | 5,472 | 1.20 | 2,007 | 0.09 | 342 | 28% | -1,154 | 0.97 | 7,240 | 0.02 |
| ORB 15 (no VWAP filter) • stop range middle • target 15:55 • none • entries until 15:00 • 1 trade a day | 392 | 33% | 5,472 | 1.20 | 2,007 | 0.09 | 342 | 28% | -1,154 | 0.97 | 7,240 | 0.02 |
| ORB 15 + VWAP side • stop range other side • target 3R • BE at +1R • entries until 12:00 • 1 trade a day | 391 | 37% | 4,698 | 1.15 | 1,836 | 0.07 | 340 | 34% | 839 | 1.02 | 7,239 | 0.04 |
| ORB 15 (no VWAP filter) • stop range other side • target 3R • BE at +1R • entries until 12:00 • 1 trade a day | 391 | 37% | 4,698 | 1.15 | 1,836 | 0.07 | 340 | 34% | 839 | 1.02 | 7,239 | 0.04 |
| ORB 15 + VWAP side • stop range other side • target 3R • none • entries until 15:00 • 2nd trade the other way | 533 | 45% | 6,495 | 1.14 | 2,614 | 0.08 | 448 | 42% | 4,035 | 1.08 | 6,436 | 0.05 |
| ORB 15 (no VWAP filter) • stop range other side • target 3R • none • entries until 15:00 • 2nd trade the other way | 533 | 45% | 6,495 | 1.14 | 2,614 | 0.08 | 448 | 42% | 4,035 | 1.08 | 6,436 | 0.05 |

## 2c. BOTH sides — the best risk management

476 of 1120 rules made money in 2020–23; of the best 10, **10 also made money in 2024–26** (their average there 7,747 $). Of ALL rules, 586 of 1120 made money in 2024–26.

| rule | 20–23 trades | win | net $ | PF | DD $ | avg R | 24–26 trades | win | net $ | PF | DD $ | avg R |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ORB 15 + VWAP side • stop range other side • target 2R • BE at +1R • entries until 15:00 • 1 trade a day | 825 | 43% | 14,723 | 1.24 | 2,198 | 0.09 | 701 | 42% | 9,508 | 1.14 | 6,878 | 0.08 |
| ORB 15 (no VWAP filter) • stop range other side • target 2R • BE at +1R • entries until 15:00 • 1 trade a day | 825 | 43% | 14,723 | 1.24 | 2,198 | 0.09 | 701 | 42% | 9,508 | 1.14 | 6,878 | 0.08 |
| ORB 15 + VWAP side • stop range other side • target 2R • BE at +1R • entries until 12:00 • 1 trade a day | 819 | 43% | 14,226 | 1.24 | 2,198 | 0.09 | 693 | 42% | 7,556 | 1.11 | 6,878 | 0.07 |
| ORB 15 (no VWAP filter) • stop range other side • target 2R • BE at +1R • entries until 12:00 • 1 trade a day | 819 | 43% | 14,226 | 1.24 | 2,198 | 0.09 | 693 | 42% | 7,556 | 1.11 | 6,878 | 0.07 |
| ORB 15 + VWAP side • stop range other side • target 3R • BE at +1R • entries until 15:00 • 1 trade a day | 825 | 42% | 16,299 | 1.27 | 2,642 | 0.11 | 701 | 40% | 8,399 | 1.13 | 7,436 | 0.07 |
| ORB 15 (no VWAP filter) • stop range other side • target 3R • BE at +1R • entries until 15:00 • 1 trade a day | 825 | 42% | 16,299 | 1.27 | 2,642 | 0.11 | 701 | 40% | 8,399 | 1.13 | 7,436 | 0.07 |
| ORB 15 + VWAP side • stop range other side • target 3R • BE at +1R • entries until 12:00 • 1 trade a day | 819 | 42% | 15,802 | 1.26 | 2,642 | 0.11 | 693 | 40% | 5,450 | 1.08 | 7,436 | 0.06 |
| ORB 15 (no VWAP filter) • stop range other side • target 3R • BE at +1R • entries until 12:00 • 1 trade a day | 819 | 42% | 15,802 | 1.26 | 2,642 | 0.11 | 693 | 40% | 5,450 | 1.08 | 7,436 | 0.06 |
| ORB 15 + VWAP side • stop range other side • target 15:55 • BE at +1R • entries until 15:00 • 1 trade a day | 825 | 42% | 15,865 | 1.26 | 2,659 | 0.10 | 701 | 39% | 7,821 | 1.12 | 7,373 | 0.07 |
| ORB 15 (no VWAP filter) • stop range other side • target 15:55 • BE at +1R • entries until 15:00 • 1 trade a day | 825 | 42% | 15,865 | 1.26 | 2,659 | 0.10 | 701 | 39% | 7,821 | 1.12 | 7,373 | 0.07 |

## 3. What each choice does (average over every rule that has it, 2024–26 avg R per trade)

| choice | ▲ buys 20–23 | ▲ buys 24–26 | ▼ sells 20–23 | ▼ sells 24–26 |
|---|---|---|---|---|
| ORB 15 | 0.009 | 0.062 | -0.019 | -0.002 |
| ORB 30 | 0.026 | -0.010 | -0.042 | -0.006 |
| VWAP side filter ON | 0.015 | 0.024 | -0.032 | -0.008 |
| VWAP side filter OFF | 0.021 | 0.029 | -0.027 | 0.001 |
| stop: range other side | 0.067 | 0.054 | 0.020 | 0.025 |
| stop: range middle | 0.005 | 0.028 | -0.012 | -0.029 |
| stop: VWAP | -0.005 | 0.009 | -0.049 | -0.037 |
| stop: break candle end | -0.010 | 0.004 | -0.088 | 0.008 |
| target: 15:55 | 0.050 | 0.039 | -0.016 | 0.032 |
| target: 1R | -0.009 | 0.035 | -0.030 | -0.019 |
| target: 1.5R | 0.004 | 0.021 | -0.032 | -0.023 |
| target: 2R | 0.008 | 0.022 | -0.040 | -0.011 |
| target: 3R | 0.034 | 0.012 | -0.033 | 0.000 |
| management: none | 0.039 | 0.031 | -0.012 | -0.005 |
| management: BE at +1R | 0.027 | 0.020 | -0.024 | 0.008 |
| management: out on a close through VWAP | 0.008 | 0.037 | -0.059 | -0.013 |
| management: BE at +1R + trail 5-min candles | -0.005 | 0.015 | -0.024 | -0.006 |
| entries until 12:00 | 0.012 | 0.030 | -0.036 | 0.003 |
| entries until 15:00 | 0.022 | 0.022 | -0.024 | -0.012 |

## 4. The pick: the best both-sides rule of 2020–23, year by year

**ORB 15 + VWAP side • stop range other side • target 2R • BE at +1R • entries until 15:00 • 1 trade a day** — chosen on 2020–23 only, so 2024–26 is a real test.

| year | trades | win | net $ | PF | DD $ | avg R |
|---|---|---|---|---|---|---|
| 2020 | 66 | 39% | -212 | 0.95 | 954 | -0.05 |
| 2021 | 254 | 48% | 5,492 | 1.35 | 1,642 | 0.14 |
| 2022 | 253 | 45% | 7,593 | 1.31 | 2,198 | 0.15 |
| 2023 | 252 | 36% | 1,850 | 1.11 | 1,345 | 0.03 |
| 2024 | 254 | 44% | 4,971 | 1.27 | 1,450 | 0.15 |
| 2025 | 253 | 41% | 6,909 | 1.30 | 3,374 | 0.08 |
| 2026 | 194 | 40% | -2,372 | 0.91 | 6,878 | -0.03 |
| ▲ buys all years | 792 | 46% | 18,021 | 1.29 | 2,370 | 0.11 |
| ▼ sells all years | 734 | 38% | 6,209 | 1.09 | 6,492 | 0.06 |

Average win 1.16R, average loss -0.70R, longest losing run 10 trades. Median stop 93.5 points ($187 a micro). Median best move in the trade's favour 0.80R.

### Sizing: contracts = $ risk ÷ (stop points × $2), at least 1

| risk a trade | net $ 2020–26 | max DD $ | worst day $ | worst month $ | avg contracts |
|---|---|---|---|---|---|
| $100 | 24,412 | 6,878 | -748 | -2,135 | 1.0 |
| $250 | 24,753 | 7,139 | -748 | -2,141 | 1.3 |
| $500 | 57,150 | 9,152 | -748 | -2,307 | 2.6 |
| $1000 | 117,519 | 19,028 | -1,046 | -5,821 | 5.7 |
