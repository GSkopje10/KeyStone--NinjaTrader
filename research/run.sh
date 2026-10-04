#!/usr/bin/env bash
# 6-year research run: unpacks data/<SYM>/*.bars.gz into a scratch store, compiles the engine + research/*.cs, runs.
# Usage: research/run.sh [out-dir] [extra-slippage-ticks]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"; work="${TMPDIR:-/tmp}/keystone-research"; out="${1:-$root/docs/research}"
mkdir -p "$work"
for s in MNQ MGC; do mkdir -p "$work/$s"; for f in "$root"/data/$s/*.bars.gz; do t="$work/$s/$(basename "$f" .gz)"; [ -f "$t" ] || zcat "$f" > "$t"; done; done
"$root/tools/compile_engine.sh" "$work/research.exe" "$root"/research/*.cs
mono "$work/research.exe" "$work" "$out" ${2:-} | tee "$out/log.txt"
