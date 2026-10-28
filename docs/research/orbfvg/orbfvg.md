# MNQ: ORB break + VWAP + 5-minute FVG (and the volume profile)

Picked on 2020–2023 (IS), checked on 2024–2026 (OOS). 1 MNQ contract, $ after costs. avg R = per trade after costs.

## ORB + FVG (the setup)

233 of 800 made money in-sample; of the best 20 in-sample, 0 also made money out-of-sample. Average OOS of the best 20: -4,038 $.

| rule | IS trades | IS win | IS avg R | IS net | IS DD | OOS trades | OOS win | OOS avg R | OOS net | OOS DD |
|---|---|---|---|---|---|---|---|---|---|---|
| ORB+FVG +VWAP • ORB 15 • FVG within 1h • entry edge • stop ORB middle • target 15:55 • until 12:00 | 488 | 34% | 0.03 | 4,000 | 1,930 | 377 | 32% | -0.07 | -1,653 | 2,582 |
| ORB+FVG +VWAP • ORB 15 • FVG within 1h • entry 50% • stop ORB middle • target 15:55 • until 12:00 | 446 | 31% | 0.05 | 4,426 | 2,204 | 342 | 29% | -0.07 | -2,128 | 3,228 |
| ORB+FVG +VWAP • ORB 30 • FVG within 1h • entry edge • stop VWAP • target 15:55 • until 15:00 | 516 | 28% | -0.01 | 3,161 | 1,599 | 411 | 24% | -0.23 | -4,725 | 5,378 |
| ORB+FVG +VWAP • ORB 15 • FVG within 3h • entry 50% • stop ORB middle • target 15:55 • until 12:00 | 479 | 31% | 0.04 | 3,995 | 2,076 | 374 | 29% | -0.08 | -2,344 | 3,207 |
| ORB+FVG +VWAP • ORB 15 • FVG within 3h • entry 50% • stop ORB middle • target 15:55 • until 15:00 | 583 | 31% | 0.01 | 3,894 | 2,099 | 446 | 29% | -0.11 | -4,348 | 5,212 |
| ORB+FVG +VWAP • ORB 30 • FVG within 3h • entry 50% • stop VWAP • target 15:55 • until 15:00 | 504 | 22% | -0.01 | 2,684 | 1,474 | 425 | 21% | -0.20 | -4,031 | 4,802 |
| ORB+FVG +VWAP • ORB 15 • FVG within 1h • entry edge • stop ORB middle • target 15:55 • until 15:00 | 534 | 33% | 0.01 | 3,412 | 1,874 | 412 | 32% | -0.09 | -3,115 | 3,548 |
| ORB+FVG +VWAP • ORB 15 • FVG within 1h • entry 50% • stop ORB middle • target 15:55 • until 15:00 | 508 | 31% | 0.02 | 3,792 | 2,120 | 388 | 30% | -0.08 | -3,239 | 4,178 |
| ORB+FVG +VWAP • ORB 30 • FVG within 3h • entry edge • stop VWAP • target 15:55 • until 15:00 | 572 | 27% | -0.04 | 3,138 | 1,782 | 474 | 24% | -0.22 | -3,908 | 5,055 |
| ORB+FVG • ORB 30 • FVG within 3h • entry edge • stop ORB middle • target 3R • until 12:00 | 489 | 38% | 0.00 | 3,480 | 1,985 | 406 | 35% | -0.12 | -5,681 | 6,631 |
| ORB+FVG • ORB 30 • FVG within 1h • entry edge • stop ORB middle • target 3R • until 12:00 | 476 | 39% | 0.00 | 3,324 | 1,898 | 390 | 36% | -0.12 | -5,408 | 6,432 |
| ORB+FVG +VWAP • ORB 30 • FVG within 3h • entry edge • stop VWAP • target 15:55 • until 12:00 | 468 | 27% | 0.01 | 2,543 | 1,491 | 392 | 24% | -0.21 | -3,543 | 4,837 |

## Benchmark: plain ORB break (market)

72 of 120 made money in-sample; of the best 20 in-sample, 11 also made money out-of-sample. Average OOS of the best 20: -1,085 $.

| rule | IS trades | IS win | IS avg R | IS net | IS DD | OOS trades | OOS win | OOS avg R | OOS net | OOS DD |
|---|---|---|---|---|---|---|---|---|---|---|
| PLAIN BREAK • ORB 15 • stop ORB middle • target 15:55 • until 15:00 | 825 | 33% | 0.08 | 9,057 | 2,863 | 701 | 32% | 0.04 | 3,045 | 7,727 |
| PLAIN BREAK +VWAP • ORB 15 • stop ORB middle • target 15:55 • until 15:00 | 825 | 33% | 0.08 | 9,057 | 2,863 | 701 | 32% | 0.04 | 3,045 | 7,727 |
| PLAIN BREAK • ORB 15 • stop ORB middle • target 15:55 • until 12:00 | 819 | 33% | 0.08 | 8,506 | 2,800 | 693 | 32% | 0.03 | 255 | 7,727 |
| PLAIN BREAK +VWAP • ORB 15 • stop ORB middle • target 15:55 • until 12:00 | 819 | 33% | 0.08 | 8,506 | 2,800 | 693 | 32% | 0.03 | 255 | 7,727 |
| PLAIN BREAK +VWAP • ORB 15 • stop VWAP • target 15:55 • until 15:00 | 825 | 28% | 0.09 | 8,422 | 2,881 | 701 | 27% | 0.05 | 2,129 | 8,058 |
| PLAIN BREAK +VWAP • ORB 15 • stop VWAP • target 15:55 • until 12:00 | 819 | 28% | 0.09 | 7,888 | 2,802 | 693 | 26% | 0.04 | -1,101 | 7,932 |
| PLAIN BREAK • ORB 30 • stop ORB middle • target 15:55 • until 15:00 | 800 | 40% | 0.06 | 8,119 | 3,155 | 688 | 36% | -0.01 | -2,800 | 7,011 |
| PLAIN BREAK +VWAP • ORB 30 • stop ORB middle • target 15:55 • until 15:00 | 800 | 40% | 0.06 | 8,119 | 3,155 | 688 | 36% | -0.01 | -2,800 | 7,011 |
| PLAIN BREAK • ORB 30 • stop ORB middle • target 15:55 • until 12:00 | 757 | 39% | 0.06 | 7,001 | 2,772 | 646 | 36% | -0.01 | -4,976 | 8,062 |
| PLAIN BREAK +VWAP • ORB 30 • stop ORB middle • target 15:55 • until 12:00 | 757 | 39% | 0.06 | 7,001 | 2,772 | 646 | 36% | -0.01 | -4,976 | 8,062 |
| PLAIN BREAK • ORB 15 • stop ORB middle • target 3R • until 15:00 | 825 | 34% | 0.05 | 6,061 | 2,857 | 701 | 34% | 0.03 | 2,889 | 7,514 |
| PLAIN BREAK +VWAP • ORB 15 • stop ORB middle • target 3R • until 15:00 | 825 | 34% | 0.05 | 6,061 | 2,857 | 701 | 34% | 0.03 | 2,889 | 7,514 |

## Benchmark: random limit after the break

76 of 120 made money in-sample; of the best 20 in-sample, 8 also made money out-of-sample. Average OOS of the best 20: -1,772 $.

| rule | IS trades | IS win | IS avg R | IS net | IS DD | OOS trades | OOS win | OOS avg R | OOS net | OOS DD |
|---|---|---|---|---|---|---|---|---|---|---|
| RANDOM LIMIT +VWAP • ORB 15 • stop VWAP • target 15:55 • until 12:00 | 431 | 30% | 0.13 | 7,296 | 972 | 399 | 26% | 0.28 | -592 | 4,360 |
| RANDOM LIMIT • ORB 15 • stop ORB middle • target 15:55 • until 12:00 | 520 | 39% | 0.09 | 8,371 | 1,955 | 479 | 38% | 0.03 | 2,977 | 2,615 |
| RANDOM LIMIT +VWAP • ORB 15 • stop ORB middle • target 15:55 • until 12:00 | 520 | 39% | 0.09 | 8,371 | 1,955 | 479 | 38% | 0.03 | 2,977 | 2,615 |
| RANDOM LIMIT • ORB 15 • stop ORB middle • target 3R • until 12:00 | 520 | 42% | 0.04 | 6,767 | 1,593 | 479 | 42% | 0.10 | 3,389 | 2,103 |
| RANDOM LIMIT +VWAP • ORB 15 • stop ORB middle • target 3R • until 12:00 | 520 | 42% | 0.04 | 6,767 | 1,593 | 479 | 42% | 0.10 | 3,389 | 2,103 |
| RANDOM LIMIT +VWAP • ORB 15 • stop VWAP • target 3R • until 12:00 | 431 | 37% | 0.06 | 4,149 | 1,008 | 399 | 31% | -0.08 | -659 | 3,577 |
| RANDOM LIMIT • ORB 30 • stop ORB middle • target 1R • until 15:00 | 524 | 54% | 0.01 | 6,155 | 1,579 | 461 | 46% | -0.04 | -4,902 | 5,678 |
| RANDOM LIMIT +VWAP • ORB 30 • stop ORB middle • target 1R • until 15:00 | 524 | 54% | 0.01 | 6,155 | 1,579 | 461 | 46% | -0.04 | -4,902 | 5,678 |
| RANDOM LIMIT • ORB 30 • stop ORB middle • target 3R • until 15:00 | 524 | 45% | -0.01 | 6,214 | 1,799 | 461 | 40% | -0.03 | -5,406 | 5,953 |
| RANDOM LIMIT +VWAP • ORB 30 • stop ORB middle • target 3R • until 15:00 | 524 | 45% | -0.01 | 6,214 | 1,799 | 461 | 40% | -0.03 | -5,406 | 5,953 |
| RANDOM LIMIT • ORB 30 • stop ORB middle • target 15:55 • until 15:00 | 524 | 44% | -0.02 | 6,237 | 1,812 | 461 | 38% | -0.08 | -6,101 | 6,789 |
| RANDOM LIMIT +VWAP • ORB 30 • stop ORB middle • target 15:55 • until 15:00 | 524 | 44% | -0.02 | 6,237 | 1,812 | 461 | 38% | -0.08 | -6,101 | 6,789 |

## Does the FVG add anything?

Same filters, stop and target: the FVG entry beat the random limit in 120 of 480 pairs out-of-sample (avg R -0.059 vs 0.029), in-sample 144 of 480.

- **VWAP filter**: on avg R -0.159 → OOS -0.151 • off -0.214 → OOS -0.184
- **profile filter**: on avg R -0.182 → OOS -0.136 • off -0.180 → OOS -0.193
