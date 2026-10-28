# SpasiX MT5

A modern Windows platform for MetaTrader 5 (IC Markets): live trading with lots and hedging, and backtesting.

- `dist/SpasiX.exe` — the app (needs the free **.NET 8 Desktop Runtime**; Windows offers it on the first start).
- `dist/SpasiXBridge.mq5` — the bridge EA in MT5: it streams the account, prices, positions and orders to the app on 127.0.0.1.
- `app/` — the app's source (WPF, .NET 8). `tests/` — the bridge tested against a fake MT5 (`dotnet run` in `tests/`).

## What it does (build 2026-10-25a)

- **Start screen**: LIVE (your MT5 account), PROP (a CFD prop-firm account with its rules), BACKTEST (MT5's history replayed).
- **Chart**: themes, time zones, split 1/2/4, indicators menu (EMA 9/21/50/200, VWAP, Bollinger, FVG, NY ORB, PDH/PDL, sessions, SpasiX setups with their record),
  drawing rail (lines, rays, trend, rectangle, Fibonacci, text, long/short risk-reward, measure, magnet, colours), our own right-click menus, tickets in their own colours
  (buys mint, sells red, pending amber) that light up under the mouse, drag stops / targets / orders, ✕ to close.
- **Trading**: ARM with a ✓/✖ checklist of every MT5 switch, lots, market / limit / stop, close all / buys / sells / winners / losers, breakeven, close everything — each asks first.
- **Backtest**: download days once (HIST), replay with play / pause / step / speed / jumps, hedging, real spread, commission, swap, stop-out, stats.
- **⇅ HEDGE LAB** (title bar, every tab): the hedge-instead-of-a-stop strategy (lot → +$target on the whole basket → the same way again; no stop: a break of the
  recent low / high opens the other side with the hedge lot) tested on the saved MT5 history — presets A ($350, 0.1 / 0.2, $20) and B ($5,000, 0.25 / 0.5, $20, $100 a day),
  preset C (the study's best: NY open 08:30-10:30, one $150 target a day, stop $300, no hedge), sessions in New York time, one target a day, max tries a day,
  NO HEDGE, close at the session end, a fixed zone; ▶ VARIATIONS (recent high / low × target, plus a fresh account every Monday for 4 weeks), ▶ SESSIONS (every session,
  hedges vs no hedge). Research only: nothing is sent to MT5. Study: `docs/research/hedge/`.
- **Tracker** (DEALS): calendar, stats, equity curve, by hour / weekday / symbol, journal. **Prop**: presets, meters, entry guard. **News**: ForexFactory + headlines.

## Verification

`cd tests && dotnet run` — the bridge against a fake MT5, the backtest engine and the HEDGE LAB engine. `cd study && dotnet run -c Release` — the hedge study on data/MGC. `tests/screens.sh` — the real app under Wine with `fakemt5.py`, screenshots.

## Building (developer)

`dotnet publish app -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true` (works on Linux with `EnableWindowsTargeting`).
