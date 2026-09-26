# data/ — exports for offline optimisation

Put the files from the lab's **EXPORT FOR CLAUDE** button here (github.com → this folder →
Add file → Upload files). Each export is one folder name, e.g.
`ASIAN75_BOTH_20260101_20260925_20260927_091500`; upload its files and tell Claude the name.

Files: `*_1m.txt.gz` 1-minute bars (NinjaTrader export format, New York time),
`settings.txt`, `ledger.csv`, `summary.txt`. The command-line optimizer reads the `.txt.gz`
files directly: `tools/optimize_asian75.sh --mnq data/<folder>/MNQ_1m.txt.gz --mgc data/<folder>/MGC_1m.txt.gz`.
