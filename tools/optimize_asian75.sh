#!/usr/bin/env bash
# Asian 75 parameter optimizer. Run with --help for options.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
rm -f "$here/.build/optimizer.exe"
"$here/compile_engine.sh" "$here/.build/optimizer.exe" "$here/Asian75Optimizer.cs"
exec mono --server "$here/.build/optimizer.exe" "$@"
