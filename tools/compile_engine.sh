#!/usr/bin/env bash
# Compiles the model + engine block of src/KeyStone.cs (the first namespace, which has no
# WPF/NinjaTrader UI dependencies) with tests/EngineStubs.cs and the given extra sources.
# Usage: tools/compile_engine.sh <output.exe> <source.cs>...
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"; out="$1"; shift
src="${KEYSTONE_SRC:-$root/src/KeyStone.cs}"; mkdir -p "$(dirname "$out")"
end=$(grep -n '^namespace NinjaTrader.NinjaScript.Indicators' "$src" | cut -d: -f1)
engine="$(dirname "$out")/Engine.cs"
{ echo 'using System; using System.Collections; using System.ComponentModel; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text;'
  sed -n "/^namespace NinjaTrader.NinjaScript$/,$((end-1))p" "$src"; } > "$engine"
mcs -langversion:6 -optimize+ -nowarn:0162,0168,0219,0414,0649 -r:System.Core -out:"$out" "$root/tests/EngineStubs.cs" "$engine" "$@" | grep -v '^Compilation succeeded' || true
test -f "$out"
