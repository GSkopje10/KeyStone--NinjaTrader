# SpasiX MT5

A modern Windows platform for MetaTrader 5 (IC Markets): live trading with lots and hedging, and backtesting.

- `dist/SpasiX.exe` — the app (needs the free **.NET 8 Desktop Runtime**; Windows offers it on the first start).
- `dist/SpasiXBridge.mq5` — the bridge EA in MT5: it streams the account, prices, positions and orders to the app on 127.0.0.1.
- `app/` — the app's source (WPF, .NET 8). `tests/` — the bridge tested against a fake MT5 (`dotnet run` in `tests/`).

## Steps (where we are)

1. **Connect — read only** ✅ the account, the Market Watch prices, every open ticket and pending order, live. No trading code at all.
2. **Chart** ✅ (candles, tickets, orders; indicators and drawings next) — the Keystone chart fed by MT5 candles, timeframes, drawing tools, VWAP / ORB / FVG, tickets / stops / targets on it.
3. **Trading** — lots, market / limit / stop, S/L / T/P per ticket (drag), hedging, close a ticket / buys / sells / all, partial close;
   ARM + limits (max lots, day loss, equity stop) — on a DEMO account first.
4. **Backtesting** — MT5 history (candles + real ticks), replay with lots, hedging, spread, commission, swaps.
5. **Polish** — positions / history / journal tabs, P/L dashboard, settings.

## Building (developer)

`dotnet publish app -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true` (works on Linux with `EnableWindowsTargeting`).
