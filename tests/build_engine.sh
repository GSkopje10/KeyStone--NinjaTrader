#!/usr/bin/env bash
# Compiles only the model + engine part of src/KeyStone.cs (the first namespace block,
# which has no WPF/NinjaTrader UI dependencies) together with tiny stubs and a test file.
# Usage: tests/build_engine.sh tests/Asian75EngineTests.cs
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"; out="$here/.build"; mkdir -p "$out"
src="$here/../src/KeyStone.cs"
end=$(grep -n '^namespace NinjaTrader.NinjaScript.Indicators' "$src" | cut -d: -f1)
{ echo 'using System; using System.Collections; using System.ComponentModel; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text;'
  sed -n "/^namespace NinjaTrader.NinjaScript$/,$((end-1))p" "$src"; } > "$out/Engine.cs"
mcs -langversion:6 -r:System.Core -out:"$out/tests.exe" "$here/EngineStubs.cs" "$out/Engine.cs" "$@"
mono "$out/tests.exe"
