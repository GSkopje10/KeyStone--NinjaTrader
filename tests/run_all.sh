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
rm -f "$here/.build/fvg.exe"
"$root/tools/compile_engine.sh" "$here/.build/fvg.exe" "$here/FvgTests.cs"
mono "$here/.build/fvg.exe" | tail -1
rm -f "$here/.build/eng.exe"
"$root/tools/compile_engine.sh" "$here/.build/eng.exe" "$here/EngulfingTests.cs"
mono "$here/.build/eng.exe" | tail -1
rm -f "$here/.build/snap.exe"
"$root/tools/compile_engine.sh" "$here/.build/snap.exe" "$here/LifecycleSnapshot.cs" "$here/SnapshotRunner.cs"
mono "$here/.build/snap.exe" "$here/.build/snapshot.txt"
cmp -s "$here/.build/snapshot.txt" "$here/lifecycle_baseline.txt" && echo "LIFECYCLE SNAPSHOT IDENTICAL TO BASELINE (BH + Asian results unchanged)" || { echo "LIFECYCLE SNAPSHOT CHANGED — diff tests/.build/snapshot.txt tests/lifecycle_baseline.txt"; exit 1; }
rm -f "$here/.build/bhsnap.exe"
"$root/tools/compile_engine.sh" "$here/.build/bhsnap.exe" "$here/BhDetectionSnapshot.cs"
mono "$here/.build/bhsnap.exe" "$here/.build/bh_detection.txt"
cmp -s "$here/.build/bh_detection.txt" "$here/bh_detection_baseline.txt" && echo "BH DETECTION SNAPSHOT IDENTICAL TO BASELINE ($(grep -c '|' "$here/bh_detection_baseline.txt") setups)" || { echo "BH DETECTION CHANGED — diff tests/.build/bh_detection.txt tests/bh_detection_baseline.txt"; exit 1; }
( cd "$here/fullcompile" && mcs -langversion:6 -nowarn:67,169,414,649,618,219,168,162,1998,429,108,114 -r:System.Core.dll -out:.build/saved.exe Stubs.cs NinjaStubs.cs ../../src/KeyStone.cs SavedDataTest.cs >/dev/null && mono .build/saved.exe | tail -1 )
( cd "$here/fullcompile" && mcs -langversion:6 -nowarn:67,169,414,649,618,219,168,162,1998,429,108,114 -r:System.Core.dll -out:.build/report.exe Stubs.cs NinjaStubs.cs ../../src/KeyStone.cs ReportPreview.cs >/dev/null && for s in BH ASIAN75 FVG ENG; do mono .build/report.exe .build/report_$s.html $s | sed "s/^/$s /"; done )
( cd "$here/fullcompile" && mcs -langversion:6 -nowarn:67,169,414,649,618,219,168,162,1998,429,108,114 -r:System.Core.dll -out:.build/ui.exe Stubs.cs NinjaStubs.cs ../../src/KeyStone.cs UiSmokeTest.cs >/dev/null && mono .build/ui.exe | tail -1 )
