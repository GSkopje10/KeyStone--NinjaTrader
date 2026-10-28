# ORB + VWAP + volume profile — MNQ and MGC, 1-minute bars

Ranked on 2020–2023 (in-sample), shown on 2024–2026 (out-of-sample, never used to pick). R = the stop distance; results after costs.

## MNQ (1 contract, $ after costs)

792 of 1050 rules made money in-sample; of the 20 best in-sample, 6 also made money out-of-sample.

**The 15 best in-sample (ranked by net ÷ drawdown, ≥ 150 trades), and what they did after:**

| rule | IS trades | IS win | IS net | IS PF | IS DD | OOS trades | OOS win | OOS net | OOS PF | OOS DD |
|---|---|---|---|---|---|---|---|---|---|---|
| BREAK +slope • ORB 15 • stop middle • target 15:55 • until 12:00 | 680 | 30% | 11,650 | 1.27 | 1,973 | 592 | 28% | -3,463 | 0.93 | 8,361 |
| BREAK +VWAP +slope • ORB 15 • stop middle • target 15:55 • until 12:00 | 680 | 30% | 11,650 | 1.27 | 1,973 | 592 | 28% | -3,463 | 0.93 | 8,361 |
| BREAK +slope • ORB 15 • stop middle • target 15:55 • until 15:00 | 681 | 30% | 11,618 | 1.27 | 1,973 | 596 | 28% | -3,687 | 0.93 | 8,588 |
| BREAK +VWAP +slope • ORB 15 • stop middle • target 15:55 • until 15:00 | 681 | 30% | 11,618 | 1.27 | 1,973 | 596 | 28% | -3,687 | 0.93 | 8,588 |
| BREAK +VWAP +slope • ORB 15 • stop VWAP • target 15:55 • until 12:00 | 680 | 26% | 11,457 | 1.30 | 2,037 | 592 | 24% | -4,757 | 0.90 | 9,175 |
| BREAK +VWAP +slope • ORB 15 • stop VWAP • target 15:55 • until 15:00 | 681 | 26% | 11,425 | 1.30 | 2,037 | 596 | 24% | -4,909 | 0.89 | 9,361 |
| BREAK +volume • ORB 30 • stop middle • target 1.5R • until 15:00 | 205 | 49% | 4,811 | 1.46 | 875 | 201 | 34% | -5,276 | 0.71 | 5,323 |
| BREAK +VWAP +volume • ORB 30 • stop middle • target 1.5R • until 15:00 | 205 | 49% | 4,811 | 1.46 | 875 | 201 | 34% | -5,276 | 0.71 | 5,323 |
| BREAK +volume • ORB 30 • stop middle • target 1.5R • until 12:00 | 204 | 49% | 4,706 | 1.45 | 875 | 200 | 34% | -5,145 | 0.72 | 5,323 |
| BREAK +VWAP +volume • ORB 30 • stop middle • target 1.5R • until 12:00 | 204 | 49% | 4,706 | 1.45 | 875 | 200 | 34% | -5,145 | 0.72 | 5,323 |
| BREAK +slope +volume • ORB 30 • stop middle • target 1.5R • until 15:00 | 191 | 48% | 4,225 | 1.42 | 810 | 185 | 34% | -4,934 | 0.71 | 5,057 |
| BREAK +VWAP +slope +volume • ORB 30 • stop middle • target 1.5R • until 15:00 | 191 | 48% | 4,225 | 1.42 | 810 | 185 | 34% | -4,934 | 0.71 | 5,057 |
| BREAK +slope +volume • ORB 30 • stop middle • target 1.5R • until 12:00 | 190 | 48% | 4,121 | 1.41 | 810 | 184 | 34% | -4,803 | 0.71 | 5,057 |
| BREAK +VWAP +slope +volume • ORB 30 • stop middle • target 1.5R • until 12:00 | 190 | 48% | 4,121 | 1.41 | 810 | 184 | 34% | -4,803 | 0.71 | 5,057 |
| BREAK +slope • ORB 30 • stop other side • target 3R • until 15:00 | 780 | 50% | 13,332 | 1.21 | 2,683 | 675 | 48% | 2,068 | 1.03 | 9,690 |

**What each confirmation adds (average over every rule with it on vs off, in-sample → out-of-sample net $):**

- **VWAP**: on 2,426 → -1,330 • off 3,183 → -114
- **slope**: on 3,224 → -563 • off 2,278 → -1,098
- **profile**: on 1,328 → 552 • off 3,590 → -1,703
- **volume**: on 2,951 → -2,408 • off 2,659 → -355

**Random benchmark** (random minute, VWAP side, same stops / targets): 60 of 60 made money in-sample, average 8,844 → out-of-sample -4,984.

## MGC (1 contract, $ after costs)

351 of 1580 rules made money in-sample; of the 20 best in-sample, 0 also made money out-of-sample.

**The 15 best in-sample (ranked by net ÷ drawdown, ≥ 150 trades), and what they did after:**

| rule | IS trades | IS win | IS net | IS PF | IS DD | OOS trades | OOS win | OOS net | OOS PF | OOS DD |
|---|---|---|---|---|---|---|---|---|---|---|
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop VWAP • target 15:55 • until 15:00 | 227 | 39% | 1,896 | 1.37 | 426 | 198 | 28% | -1,347 | 0.89 | 3,016 |
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop VWAP • target 15:55 • until 12:00 | 223 | 39% | 1,898 | 1.37 | 481 | 198 | 28% | -1,347 | 0.89 | 3,016 |
| BREAK +slope +profile +volume • ORB 30 • stop middle • target 15:55 • until 15:00 | 227 | 41% | 1,790 | 1.32 | 480 | 198 | 33% | -2,279 | 0.83 | 4,089 |
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop middle • target 15:55 • until 15:00 | 227 | 41% | 1,790 | 1.32 | 480 | 198 | 33% | -2,279 | 0.83 | 4,089 |
| BREAK +slope +profile +volume • ORB 30 • stop other side • target 15:55 • until 15:00 | 227 | 48% | 1,570 | 1.24 | 446 | 198 | 45% | -1,653 | 0.90 | 4,893 |
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop other side • target 15:55 • until 15:00 | 227 | 48% | 1,570 | 1.24 | 446 | 198 | 45% | -1,653 | 0.90 | 4,893 |
| BREAK +slope +profile +volume • ORB 30 • stop other side • target 3R • until 15:00 | 227 | 48% | 1,542 | 1.24 | 446 | 198 | 45% | -1,724 | 0.89 | 4,893 |
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop other side • target 3R • until 15:00 | 227 | 48% | 1,542 | 1.24 | 446 | 198 | 45% | -1,724 | 0.89 | 4,893 |
| BREAK +VWAP +profile +volume • ORB 30 • stop VWAP • target 15:55 • until 15:00 | 242 | 38% | 1,637 | 1.29 | 475 | 212 | 29% | -30 | 1.00 | 3,193 |
| BREAK +slope +profile +volume • ORB 30 • stop middle • target 15:55 • until 12:00 | 223 | 41% | 1,812 | 1.33 | 534 | 198 | 33% | -2,279 | 0.83 | 4,089 |
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop middle • target 15:55 • until 12:00 | 223 | 41% | 1,812 | 1.33 | 534 | 198 | 33% | -2,279 | 0.83 | 4,089 |
| BREAK +slope +profile +volume • ORB 30 • stop other side • target 15:55 • until 12:00 | 223 | 48% | 1,563 | 1.24 | 463 | 198 | 45% | -1,653 | 0.90 | 4,893 |
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop other side • target 15:55 • until 12:00 | 223 | 48% | 1,563 | 1.24 | 463 | 198 | 45% | -1,653 | 0.90 | 4,893 |
| BREAK +slope +profile +volume • ORB 30 • stop other side • target 2R • until 15:00 | 227 | 48% | 1,482 | 1.23 | 446 | 198 | 46% | -1,464 | 0.91 | 4,520 |
| BREAK +VWAP +slope +profile +volume • ORB 30 • stop other side • target 2R • until 15:00 | 227 | 48% | 1,482 | 1.23 | 446 | 198 | 46% | -1,464 | 0.91 | 4,520 |

**What each confirmation adds (average over every rule with it on vs off, in-sample → out-of-sample net $):**

- **VWAP**: on -805 → -3,361 • off -690 → -3,589
- **slope**: on -661 → -3,678 • off -857 → -3,229
- **profile**: on -308 → -2,024 • off -1,198 → -4,846
- **volume**: on -257 → -3,125 • off -1,248 → -3,773

**Random benchmark** (random minute, VWAP side, same stops / targets): 2 of 60 made money in-sample, average -1,930 → out-of-sample -2,835.

