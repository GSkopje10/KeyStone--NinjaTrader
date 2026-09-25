#!/usr/bin/env bash
# Compiles the engine block of src/KeyStone.cs with a test file and runs it.
# Usage: tests/build_engine.sh tests/Asian75EngineTests.cs
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
rm -f "$here/.build/tests.exe"
"$here/../tools/compile_engine.sh" "$here/.build/tests.exe" "$@"
mono "$here/.build/tests.exe"
