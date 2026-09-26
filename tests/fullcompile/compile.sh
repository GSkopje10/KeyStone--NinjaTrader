#!/usr/bin/env bash
# Compile-checks the WHOLE src/KeyStone.cs (engine + WPF lab + chart) against stand-in stubs.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$here/.build"
mcs -langversion:6 -target:library -nowarn:67,169,414,649,618,219,168,162,1998,429,108,114 \
  -r:System.Core.dll \
  -out:"$here/.build/KeyStoneFull.dll" "$here/Stubs.cs" "$here/NinjaStubs.cs" "$here/../../src/KeyStone.cs"
echo "FULL FILE COMPILES"
