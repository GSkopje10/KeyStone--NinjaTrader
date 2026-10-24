#!/bin/bash
# Screenshots of the real SpasiX app under Wine + Xvfb with a fake MT5 (mt5/tests/fakemt5.py): catches start-up crashes and layout bugs
# before the user's PC. Needs: wine64, xvfb, imagemagick, xdotool, python3-fonttools (winefonts.py renames Linux fonts to Segoe UI / Consolas / Arial).
# usage: mt5/tests/screens.sh <out-dir> <LIVE|PROP|BACKTEST> [seconds] [actions.sh]   (actions.sh: xdotool clicks + `import -window root x.png`)
set -e; OUT=$1; MODE=$2; SECS=${3:-40}; ACT=$4; HERE=$(cd "$(dirname "$0")" && pwd)
export WINEPREFIX=$OUT/wp WINEDEBUG=-all SPASIX_TEST_SIZE=1600x1000
mkdir -p $OUT; (cd $HERE/../app && ~/.dotnet/dotnet publish -c Release -r win-x64 --self-contained true -o $OUT/sc >/dev/null)
FD=$WINEPREFIX/drive_c/windows/Fonts; if [ ! -f $FD/segoeui.ttf ]; then mkdir -p $FD; L=/usr/share/fonts/truetype
  python3 -I $HERE/winefonts.py $L/dejavu/DejaVuSans.ttf $FD/segoeui.ttf Segoe_UI Regular; python3 -I $HERE/winefonts.py $L/dejavu/DejaVuSans-Bold.ttf $FD/segoeuib.ttf Segoe_UI Bold
  python3 -I $HERE/winefonts.py $L/dejavu/DejaVuSansMono.ttf $FD/consola.ttf Consolas Regular; python3 -I $HERE/winefonts.py $L/dejavu/DejaVuSansMono-Bold.ttf $FD/consolab.ttf Consolas Bold
  python3 -I $HERE/winefonts.py $L/liberation/LiberationSans-Regular.ttf $FD/arial.ttf Arial Regular
  K='HKLM\Software\Microsoft\Windows NT\CurrentVersion\Fonts'; for p in "Segoe UI (TrueType)=segoeui.ttf" "Segoe UI Bold (TrueType)=segoeuib.ttf" "Consolas (TrueType)=consola.ttf" "Consolas Bold (TrueType)=consolab.ttf" "Arial (TrueType)=arial.ttf"; do /usr/lib/wine/wine64 reg add "$K" /v "${p%%=*}" /d "${p#*=}" /f >/dev/null 2>&1; done
  /usr/lib/wine/wineserver -k; fi
xvfb-run -a -s "-screen 0 1600x1000x24" bash -c "/usr/lib/wine/wine64 $OUT/sc/SpasiX.exe --mode $MODE > $OUT/run.log 2>&1 & python3 -I $HERE/fakemt5.py DEMO >/dev/null 2>&1 & sleep $SECS; [ -n '$ACT' ] && bash '$ACT'; import -window root $OUT/$MODE.png; /usr/lib/wine/wineserver -k"
grep -v '^   at' $OUT/run.log | head -5 || true
