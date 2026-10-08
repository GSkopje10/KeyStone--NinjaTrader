# EVAL ROTATION 2026-01-01 → 2026-12-31 • 50 evaluations • BH setups • risk $500 → target $1,500 a trade

Every BH trade: entry one tick over the green reference candle's high (5-minute candles), stop one tick under the red + green low, target 3R; out by 15:55 if neither. Micros = $500 ÷ (stop × point value), capped (25K-style 20, 50K-style 40); a stop too wide for 1 micro = skipped. Costs: $1.24 a micro round trip.
ROTATE 50: 50 evaluations running, each setup goes to the next one; a passed / blown one is replaced at once. FOCUS: one evaluation trades every setup it gets until it passes or blows, then the next one starts. FIXED 50: exactly 50 bought, no replacement (open = never finished by now). Pass % = passed ÷ (passed + blown); passes per 50 evals = pass % × 50 (a $5K budget at $100 an eval).

## MNQ • 188 sessions

### MNQ • 25K-style (pass +$1,500 • blow −$1,000 EOD)
| setups | session | trades | win % | losing streak | ROTATE 50: passed / blown (pass %) | FOCUS 1 at a time: passed / blown (pass %) | FIXED 50 bought: passed / blown / open | passes per 50 evals ($5K) rotate • focus |
|---|---|---|---|---|---|---|---|---|
| ONE A DAY | NY 09:30–15:00 | 187 | 28% | 12 | 11 / 25 (31%) | 10 / 37 (21%) | 9 / 34 / 7 | **15** • **11** |
| ONE A DAY | NY OPEN 09:30–11:30 | 187 | 28% | 12 | 11 / 25 (31%) | 10 / 37 (21%) | 9 / 34 / 7 | **15** • **11** |
| ONE A DAY | LONDON 02:00–09:30 | 188 | 30% | 9 | 15 / 29 (34%) | 18 / 43 (30%) | 15 / 35 / 0 | **17** • **15** |
| ONE A DAY | ASIA 18:00–02:00 | 187 | 26% | 11 | 10 / 29 (26%) | 8 / 42 (16%) | 11 / 39 / 0 | **13** • **8** |
| ONE A DAY | ALL 18:00–15:00 | 187 | 26% | 11 | 10 / 29 (26%) | 8 / 42 (16%) | 11 / 39 / 0 | **13** • **8** |
| EVERY SETUP | NY 09:30–15:00 | 619 | 29% | 28 | 33 / 120 (22%) | 36 / 114 (24%) | 9 / 41 / 0 | **11** • **12** |
|  | ↳ ROTATE 50 by month: Jan 1 passed 2 blown • Feb 4 passed 6 blown • Mar 4 passed 28 blown • Apr 2 passed 12 blown • May 7 passed 11 blown • Jun 1 passed 16 blown • Jul 6 passed 15 blown • Aug 2 passed 15 blown • Sep 6 passed 13 blown • Oct 0 passed 2 blown | | | | | | | |
| EVERY SETUP | NY OPEN 09:30–11:30 | 332 | 30% | 13 | 19 / 51 (27%) | 18 / 70 (20%) | 15 / 35 / 0 | **14** • **10** |
| EVERY SETUP | LONDON 02:00–09:30 | 1097 | 26% | 19 | 68 / 258 (21%) | 88 / 225 (28%) | 10 / 40 / 0 | **10** • **14** |
| EVERY SETUP | ASIA 18:00–02:00 | 967 | 26% | 23 | 74 / 211 (26%) | 79 / 212 (27%) | 11 / 39 / 0 | **13** • **14** |
| EVERY SETUP | ALL 18:00–15:00 | 2450 | 27% | 21 | 155 / 572 (21%) | 184 / 459 (29%) | 13 / 37 / 0 | **11** • **14** |

### MNQ • 50K-style (pass +$1,500 • blow −$2,000 EOD)
| setups | session | trades | win % | losing streak | ROTATE 50: passed / blown (pass %) | FOCUS 1 at a time: passed / blown (pass %) | FIXED 50 bought: passed / blown / open | passes per 50 evals ($5K) rotate • focus |
|---|---|---|---|---|---|---|---|---|
| ONE A DAY | NY 09:30–15:00 | 187 | 28% | 12 | 11 / 0 (100%) | 8 / 15 (35%) | 9 / 0 / 41 | **50** • **17** |
| ONE A DAY | NY OPEN 09:30–11:30 | 187 | 28% | 12 | 11 / 0 (100%) | 8 / 15 (35%) | 9 / 0 / 41 | **50** • **17** |
| ONE A DAY | LONDON 02:00–09:30 | 188 | 30% | 9 | 16 / 5 (76%) | 17 / 17 (50%) | 17 / 7 / 26 | **38** • **25** |
| ONE A DAY | ASIA 18:00–02:00 | 187 | 26% | 11 | 11 / 4 (73%) | 12 / 19 (39%) | 14 / 7 / 29 | **37** • **19** |
| ONE A DAY | ALL 18:00–15:00 | 187 | 26% | 11 | 11 / 4 (73%) | 12 / 19 (39%) | 14 / 7 / 29 | **37** • **19** |
| EVERY SETUP | NY 09:30–15:00 | 619 | 29% | 28 | 26 / 46 (36%) | 28 / 45 (38%) | 18 / 32 / 0 | **18** • **19** |
|  | ↳ ROTATE 50 by month: Jan 1 passed 0 blown • Feb 4 passed 0 blown • Mar 4 passed 2 blown • Apr 2 passed 12 blown • May 5 passed 5 blown • Jun 2 passed 8 blown • Jul 3 passed 7 blown • Aug 2 passed 4 blown • Sep 3 passed 6 blown • Oct 0 passed 2 blown | | | | | | | |
| EVERY SETUP | NY OPEN 09:30–11:30 | 332 | 30% | 13 | 18 / 14 (56%) | 16 / 25 (39%) | 18 / 20 / 12 | **28** • **20** |
| EVERY SETUP | LONDON 02:00–09:30 | 1097 | 26% | 19 | 64 / 96 (40%) | 74 / 97 (43%) | 14 / 36 / 0 | **20** • **22** |
| EVERY SETUP | ASIA 18:00–02:00 | 967 | 26% | 23 | 68 / 90 (43%) | 70 / 85 (45%) | 18 / 32 / 0 | **22** • **23** |
| EVERY SETUP | ALL 18:00–15:00 | 2450 | 27% | 21 | 164 / 237 (41%) | 161 / 194 (45%) | 19 / 31 / 0 | **20** • **23** |

## MGC • 183 sessions

### MGC • 25K-style (pass +$1,500 • blow −$1,000 EOD)
| setups | session | trades | win % | losing streak | ROTATE 50: passed / blown (pass %) | FOCUS 1 at a time: passed / blown (pass %) | FIXED 50 bought: passed / blown / open | passes per 50 evals ($5K) rotate • focus |
|---|---|---|---|---|---|---|---|---|
| ONE A DAY | NY 09:30–15:00 | 181 | 27% | 14 | 8 / 26 (24%) | 11 / 35 (24%) | 11 / 39 / 0 | **12** • **12** |
| ONE A DAY | NY OPEN 09:30–11:30 | 181 | 27% | 14 | 8 / 26 (24%) | 11 / 35 (24%) | 11 / 39 / 0 | **12** • **12** |
| ONE A DAY | LONDON 02:00–09:30 | 183 | 22% | 16 | 6 / 37 (14%) | 10 / 47 (18%) | 11 / 39 / 0 | **7** • **9** |
| ONE A DAY | ASIA 18:00–02:00 | 182 | 25% | 19 | 12 / 32 (27%) | 14 / 47 (23%) | 14 / 36 / 0 | **14** • **11** |
| ONE A DAY | ALL 18:00–15:00 | 182 | 25% | 19 | 12 / 32 (27%) | 14 / 47 (23%) | 14 / 36 / 0 | **14** • **11** |
| EVERY SETUP | NY 09:30–15:00 | 613 | 27% | 16 | 22 / 124 (15%) | 31 / 127 (20%) | 9 / 41 / 0 | **8** • **10** |
|  | ↳ ROTATE 50 by month: Feb 2 passed 0 blown • Mar 4 passed 17 blown • Apr 0 passed 12 blown • May 5 passed 14 blown • Jun 1 passed 12 blown • Jul 2 passed 25 blown • Aug 3 passed 22 blown • Sep 4 passed 19 blown • Oct 1 passed 3 blown | | | | | | | |
| EVERY SETUP | NY OPEN 09:30–11:30 | 327 | 26% | 13 | 10 / 62 (14%) | 13 / 74 (15%) | 8 / 42 / 0 | **7** • **7** |
| EVERY SETUP | LONDON 02:00–09:30 | 989 | 21% | 33 | 50 / 254 (16%) | 55 / 240 (19%) | 9 / 41 / 0 | **8** • **9** |
| EVERY SETUP | ASIA 18:00–02:00 | 1017 | 24% | 21 | 72 / 248 (23%) | 75 / 227 (25%) | 12 / 38 / 0 | **11** • **12** |
| EVERY SETUP | ALL 18:00–15:00 | 2364 | 24% | 24 | 139 / 567 (20%) | 151 / 487 (24%) | 12 / 38 / 0 | **10** • **12** |

### MGC • 50K-style (pass +$1,500 • blow −$2,000 EOD)
| setups | session | trades | win % | losing streak | ROTATE 50: passed / blown (pass %) | FOCUS 1 at a time: passed / blown (pass %) | FIXED 50 bought: passed / blown / open | passes per 50 evals ($5K) rotate • focus |
|---|---|---|---|---|---|---|---|---|
| ONE A DAY | NY 09:30–15:00 | 181 | 27% | 14 | 8 / 0 (100%) | 9 / 17 (35%) | 7 / 0 / 43 | **50** • **17** |
| ONE A DAY | NY OPEN 09:30–11:30 | 181 | 27% | 14 | 8 / 0 (100%) | 9 / 17 (35%) | 7 / 0 / 43 | **50** • **17** |
| ONE A DAY | LONDON 02:00–09:30 | 183 | 22% | 16 | 6 / 0 (100%) | 10 / 22 (31%) | 10 / 3 / 37 | **50** • **16** |
| ONE A DAY | ASIA 18:00–02:00 | 182 | 25% | 19 | 12 / 4 (75%) | 12 / 18 (40%) | 10 / 8 / 32 | **38** • **20** |
| ONE A DAY | ALL 18:00–15:00 | 182 | 25% | 19 | 12 / 4 (75%) | 12 / 18 (40%) | 10 / 8 / 32 | **38** • **20** |
| EVERY SETUP | NY 09:30–15:00 | 613 | 27% | 16 | 24 / 45 (35%) | 26 / 54 (33%) | 14 / 36 / 0 | **17** • **16** |
|  | ↳ ROTATE 50 by month: Feb 2 passed 0 blown • Mar 4 passed 0 blown • Apr 0 passed 9 blown • May 4 passed 3 blown • Jun 0 passed 11 blown • Jul 5 passed 5 blown • Aug 5 passed 12 blown • Sep 4 passed 5 blown | | | | | | | |
| EVERY SETUP | NY OPEN 09:30–11:30 | 327 | 26% | 13 | 12 / 19 (39%) | 14 / 31 (31%) | 10 / 26 / 14 | **19** • **16** |
| EVERY SETUP | LONDON 02:00–09:30 | 989 | 21% | 33 | 48 / 111 (30%) | 46 / 106 (30%) | 13 / 37 / 0 | **15** • **15** |
| EVERY SETUP | ASIA 18:00–02:00 | 1017 | 24% | 21 | 76 / 106 (42%) | 59 / 97 (38%) | 21 / 29 / 0 | **21** • **19** |
| EVERY SETUP | ALL 18:00–15:00 | 2364 | 24% | 24 | 122 / 237 (34%) | 129 / 225 (36%) | 24 / 26 / 0 | **17** • **18** |

Reading it: a pass needs ONE 3R win (+$1,500) before the account's losses reach its drawdown (25K-style: two losses; 50K-style: four). Pass % = passed ÷ (passed + blown). Each pass is an evaluation passed — not money yet (funded rules, activation fees and payouts come after).
