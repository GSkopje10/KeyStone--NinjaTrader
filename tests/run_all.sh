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
rm -f "$here/.build/new.exe"
"$root/tools/compile_engine.sh" "$here/.build/new.exe" "$here/NewStrategyTests.cs"
mono "$here/.build/new.exe" | tail -1
rm -f "$here/.build/golden.exe"
"$root/tools/compile_engine.sh" "$here/.build/golden.exe" "$here/GoldenTests.cs"
mono "$here/.build/golden.exe" | tail -1
rm -f "$here/.build/helix.exe"
"$root/tools/compile_engine.sh" "$here/.build/helix.exe" "$here/HelixTests.cs"
mono "$here/.build/helix.exe" | tail -1
rm -f "$here/.build/recoil.exe"
"$root/tools/compile_engine.sh" "$here/.build/recoil.exe" "$here/RecoilTests.cs"
mono "$here/.build/recoil.exe" | tail -1
rm -f "$here/.build/fes.exe"
"$root/tools/compile_engine.sh" "$here/.build/fes.exe" "$here/FvgEntryStudyTests.cs"
mono "$here/.build/fes.exe" | tail -1
rm -f "$here/.build/move.exe"
"$root/tools/compile_engine.sh" "$here/.build/move.exe" "$here/MoveStudyTests.cs"
mono "$here/.build/move.exe" | tail -1
rm -f "$here/.build/rot.exe"
"$root/tools/compile_engine.sh" "$here/.build/rot.exe" "$here/RotationTests.cs"
mono "$here/.build/rot.exe" | tail -1
rm -f "$here/.build/flip.exe"
"$root/tools/compile_engine.sh" "$here/.build/flip.exe" "$here/FlipTests.cs"
mono "$here/.build/flip.exe" | tail -1
rm -f "$here/.build/flab.exe"
"$root/tools/compile_engine.sh" "$here/.build/flab.exe" "$here/FvgLabTests.cs"
mono "$here/.build/flab.exe" | tail -1
rm -f "$here/.build/elab.exe"
"$root/tools/compile_engine.sh" "$here/.build/elab.exe" "$here/EngLabTests.cs"
mono "$here/.build/elab.exe" | tail -1
rm -f "$here/.build/alab.exe"
"$root/tools/compile_engine.sh" "$here/.build/alab.exe" "$here/AsianLabTests.cs"
mono "$here/.build/alab.exe" | tail -1
rm -f "$here/.build/mad.exe"
"$root/tools/compile_engine.sh" "$here/.build/mad.exe" "$here/MicroADayTests.cs"
mono "$here/.build/mad.exe" | tail -1
rm -f "$here/.build/ideas.exe"
"$root/tools/compile_engine.sh" "$here/.build/ideas.exe" "$here/IdeasTests.cs"
mono "$here/.build/ideas.exe" | tail -1
rm -f "$here/.build/sides.exe"
"$root/tools/compile_engine.sh" "$here/.build/sides.exe" "$here/SidesTests.cs"
mono "$here/.build/sides.exe" | tail -1
rm -f "$here/.build/rtrade.exe"
"$root/tools/compile_engine.sh" "$here/.build/rtrade.exe" "$here/ReplayTraderTests.cs"
mono "$here/.build/rtrade.exe" | tail -1
rm -f "$here/.build/studio.exe"
"$root/tools/compile_engine.sh" "$here/.build/studio.exe" "$here/StudioStoreTests.cs"
mono "$here/.build/studio.exe" | tail -1
rm -f "$here/.build/orb.exe"
"$root/tools/compile_engine.sh" "$here/.build/orb.exe" "$here/OpeningRangeTests.cs"
mono "$here/.build/orb.exe" | tail -1
rm -f "$here/.build/news.exe"
"$root/tools/compile_engine.sh" "$here/.build/news.exe" "$here/NewsTests.cs"
mono "$here/.build/news.exe" | tail -1
rm -f "$here/.build/coach.exe"
"$root/tools/compile_engine.sh" "$here/.build/coach.exe" "$here/CoachTests.cs"
mono "$here/.build/coach.exe" | tail -1
rm -f "$here/.build/analog.exe"
"$root/tools/compile_engine.sh" "$here/.build/analog.exe" "$here/AnalogTests.cs"
mono "$here/.build/analog.exe" | tail -1
rm -f "$here/.build/game.exe"
"$root/tools/compile_engine.sh" "$here/.build/game.exe" "$here/LifecycleSnapshot.cs" "$here/GameOptimizerTests.cs"
mono "$here/.build/game.exe" | tail -1
rm -f "$here/.build/prop.exe"
"$root/tools/compile_engine.sh" "$here/.build/prop.exe" "$here/PropPlannerTests.cs"
mono "$here/.build/prop.exe" | tail -1
rm -f "$here/.build/snap.exe"
"$root/tools/compile_engine.sh" "$here/.build/snap.exe" "$here/LifecycleSnapshot.cs" "$here/SnapshotRunner.cs"
mono "$here/.build/snap.exe" "$here/.build/snapshot.txt"
cmp -s "$here/.build/snapshot.txt" "$here/lifecycle_baseline.txt" && echo "LIFECYCLE SNAPSHOT IDENTICAL TO BASELINE (BH + Asian results unchanged)" || { echo "LIFECYCLE SNAPSHOT CHANGED — diff tests/.build/snapshot.txt tests/lifecycle_baseline.txt"; exit 1; }
rm -f "$here/.build/bhsnap.exe"
"$root/tools/compile_engine.sh" "$here/.build/bhsnap.exe" "$here/BhDetectionSnapshot.cs"
mono "$here/.build/bhsnap.exe" "$here/.build/bh_detection.txt"
cmp -s "$here/.build/bh_detection.txt" "$here/bh_detection_baseline.txt" && echo "BH DETECTION SNAPSHOT IDENTICAL TO BASELINE ($(grep -c '|' "$here/bh_detection_baseline.txt") setups)" || { echo "BH DETECTION CHANGED — diff tests/.build/bh_detection.txt tests/bh_detection_baseline.txt"; exit 1; }
( cd "$here/fullcompile" && mcs -langversion:6 -nowarn:67,169,414,649,618,219,168,162,1998,429,108,114 -r:System.Core.dll -out:.build/saved.exe Stubs.cs NinjaStubs.cs ../../src/KeyStone.cs SavedDataTest.cs >/dev/null && mono .build/saved.exe | tail -1 )
( cd "$here/fullcompile" && mcs -langversion:6 -nowarn:67,169,414,649,618,219,168,162,1998,429,108,114 -r:System.Core.dll -out:.build/report.exe Stubs.cs NinjaStubs.cs ../../src/KeyStone.cs ReportPreview.cs >/dev/null && for s in BH ASIAN75 FVG ENG RLY VWP GLD; do mono .build/report.exe .build/report_$s.html $s | sed "s/^/$s /"; done )
( cd "$here/fullcompile" && mcs -langversion:6 -nowarn:67,169,414,649,618,219,168,162,1998,429,108,114 -r:System.Core.dll -out:.build/ui.exe Stubs.cs NinjaStubs.cs ../../src/KeyStone.cs UiSmokeTest.cs >/dev/null && mono .build/ui.exe | tail -1 )
