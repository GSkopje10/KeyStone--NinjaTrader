#!/usr/bin/env bash
# Runs every engine test suite (no NinjaTrader needed) and a syntax check of the whole file.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/.."
mcs --parse "$root/src/KeyStone.cs" >/dev/null && echo "SYNTAX OK  src/KeyStone.cs"
"$here/fullcompile/compile.sh" 2>&1 | tail -1
"$here/build_engine.sh" "$here/Asian75EngineTests.cs" | tail -1
rm -f "$here/.build/life.exe"
"$root/tools/compile_engine.sh" "$here/.build/life.exe" "$here/LifecycleSnapshot.cs" "$here/LifecycleTests.cs"
mono "$here/.build/life.exe" | tail -1
