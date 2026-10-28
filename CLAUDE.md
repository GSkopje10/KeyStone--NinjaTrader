# Keystone Arc — working rules for Claude sessions

Read `docs/` first: the Manus takeover handoff (.docx), the Claude session handoff, and
`docs/ASIAN75.md` (Asian 75 rules, optimizer, data conventions).

## Delivering code to the user

- Every reply that ships code: first explain everything that changed (per product), THEN at the very end
  hand over every file the user needs: ONLY `mt5/dist/SpasiX.exe` (one file, sent as a file — NO zip, user's
  request) whenever `mt5/` changed (send `SpasiXBridge.mq5` on its own only when the bridge itself changed, and say so), and the KeyStone.cs link whenever `src/` changed.
- The user installs one file: `src/KeyStone.cs` → `Documents\NinjaTrader 8\bin\Custom\AddOns\`,
  then compiles with F5 in the NinjaScript Editor.
- **After every code change: commit, push, bump `KeystoneBuild` in `src/KeyStone.cs`, and end the
  reply with the copy-paste link pinned to the pushed commit:**
  `https://github.com/GSkopje10/KeyStone--NinjaTrader/raw/<full commit sha>/src/KeyStone.cs`
  (the github.com form works for the private repo when the user is logged in)
  plus the build label the header should show, so the user can confirm the new code is running.
- If a user screenshot shows an older build label, the new file was not compiled/installed;
  say so before debugging anything else. NinjaTrader keeps running the last successful compile
  when F5 fails, and any error in any file under `bin\Custom` blocks the whole compile.

## Guardrails

- Real orders exist ONLY through the ● LIVE TRADING window's REAL ACCOUNT bar (`KeystoneAccountLink` + `NinjaBroker`; the live chart shows the linked account,
  orders placed in NinjaTrader included), approved by the
  user: nothing is sent unless the user ARMS it and confirms (ARM once per session, user's request: switching the chart account
  re-arms the new account with its own limits until DISARM); keep its limits (max contracts, day loss, day profit cap,
  runaway guard = 8 entry orders a minute, stops / targets / closes counted apart at 30, reject → disarm) and its tests (`tests/AccountLinkTests.cs`). No other code path may place orders.
  Exceptions the user asked for, active on a linked account even unarmed: ONE CANCELS OTHER (cancels only) and LOCK FOR
  TODAY with flatten (confirmed when locking). ⛔ CLOSE ALL POSITIONS (Live Trading strip, `FlattenEveryAccount`) flattens every
  connected account after one confirmation (closes / cancels only, stops the rotations and the copier). The COPIER (named groups of prop accounts, one leader + followers, an account in one group only, `KeystoneCopyGroups`) sends orders to follower
  accounts only after the group's switch is turned on and confirmed (that one confirmation also arms the group's chart account with the
  ACCOUNT LIMITS; each new entry while a group is on asks again). Other groups can be ON at the same time, traded from their card in ⇉ COPIER
  (`KeystoneGroupDesk`: a virtual leader per instrument; BUY / SELL / CLOSE buttons, each asks first; each account its own armed link). The chart has ONE tab per ON group (bottom strip, next to the instruments); the selected tab is the only group its BUY / SELL / CLOSE trade (`chartDesks`, `chartOwnOff`): another group is never touched; each entry asks first, an opposite-side group warns first. The ✋ click-to-trade signal window sends nothing by itself (its button = a BUY / SELL click). The 📊 ORDERFLOW tab is analysis only (`tradeOkOf` is false there); its dock's BUY / SELL asks first, switches back to the 📈 CHART tab and presses the chart's own BUY / SELL (same armed link, same limits).
  While any account is in a trade, settings, DISARM and copy-group OFF are locked (`inTradeNow`): close the positions first; switching the chart account asks YES / NO (`okSwitchInTrade`, the position stays at the broker). An account in a trade cannot be added to a copy group. A group switches ON only after CONFIRM GROUP (`KeystoneCopyGroup.Confirmed`). At start-up ONE RESUME window (`ResumeWindow`, user's request) lists every group that was ON / still in a trade and the ARM state; its RESUME EVERYTHING is the confirmation for all of them (`resumeSilent`); a group switched ON mid-trade takes the real position over and sends nothing (`KeystoneGroupDesk.TakeOver`, tested).
  Firm rules (`KeystoneFirmPolicies`): automation (evaluation / funded) and copying are separate; AUTO is refused where automation is off.
  The ROTATION (Live Trading ▸ 🔄 ROTATE, `KeystoneRotator`) sends the chart strategy's orders to ONE pool account at a time
  only after START ROTATION is confirmed (each account gets its own armed `KeystoneAccountLink` with its limits; MANUAL mode
  sends nothing until ENTER is pressed); its tests are in `tests/AccountLinkTests.cs`.
  The MNQ+MGC ROTATION (Live Trading ▸ 🎲, `KeystoneDuoRotation`) trades both instruments on ONE pool account at a time only
  after START ROTATION is confirmed (each account: two armed links with their limits + a hard stop at the broker per leg;
  MANUAL sends nothing until a BUY / SELL button; ROTATE COPY GROUPS: a seat's `Mates` take the same sides); its tests are in `tests/AccountLinkTests.cs`.
  Research / lab / backtests stay historical; no Apex/Nexus coupling.
- SpasiX for MT5 (`mt5/`: the bridge EA `SpasiXBridge.mq5` v5 + the Windows app `mt5/app`, built with `dotnet publish` here, shipped in `mt5/dist`;
  a full-screen HOME picks LIVE / PROP / DEMO / BACKTEST; several can be open at once as tabs in our own title bar, `Shell.cs`, each LIVE / PROP
  window with its own ARM; data lives in `DataDir` — F:\SpasiX\MT5 when there is an F: drive): real orders exist only when the bridge input `EnableTrading` (default true, false = read only)
  + MT5's Algo Trading + the EA's Allow Algo Trading are on AND the app's ARM (per session, ONE click, user's request; it never turns itself off — only DISARM or closing the window ends it, while MT5 blocks trading orders are refused but ARM stays) is on; `MaxLotsPerOrder`
  is enforced in the bridge. Armed, market BUY / SELL and a ticket's ✕ act at once (user's request); a right-click limit / stop asks its lots
  first; every close-all / bulk button, breakeven and the BASKET (close all at +$X / −$Y, `Protect.cs`) ask first. PROP mode refuses new
  entries at the firm's limits (closes never blocked). The DEMO (`Paper.cs`, `PaperBroker`) and the BACKTEST (`Sim.cs`, `SimBroker`) trade a
  `SimAccount` only — they never send an order to MT5 (DEMO reads live prices; the backtest only downloads history: HIST / HISTT ticks). The ⇅ HEDGE LAB (title-bar button, `HedgeLab.cs` engine + `HedgeLabWindow.cs`) is research only: a simulated account in memory on the saved history, it never sends an order. Tests: `cd mt5/tests && dotnet run` (bridge vs a fake MT5 + the backtest engine + the hedge engine).
  Screens: `mt5/tests/screens.sh` runs the real app under Wine + Xvfb with `fakemt5.py` and screenshots it — use it before shipping UI.
- Do not change BH detection, BH outcome resolution, or the MNQ loader without explicit scope.
  MGC loader changes stay MGC-only. Asian work stays inside the Asian code paths.
- Never fix a zero result by relaxing the 1-minute outcome gate or inventing prices.
- This file shadows WPF names with brush fields (`Panel`, `Card`, `Text`, …): fully qualify WPF
  classes such as `System.Windows.Controls.Panel`.

## Verification available here (no NinjaTrader)

- `tests/run_all.sh` — runs every test suite (Asian engine, account lifecycle) + syntax check.
- `tests/build_engine.sh tests/Asian75EngineTests.cs` — compiles the engine block of
  `src/KeyStone.cs` with mono and runs the Asian/optimizer/MGC-roll tests.
- `mcs --parse src/KeyStone.cs` — syntax check of the whole file.
- `tests/fullcompile/compile.sh` — compiles the WHOLE file (engine + WPF lab + chart) against
  stand-in WPF / NinjaTrader stubs. Add a stub member when new code uses a new API; keep real
  names and signatures. Catches type/scope errors; the real compile is still the user's F5.
- `tests/fullcompile/UiSmokeTest.cs` (in run_all) builds the whole lab window and evidence
  chart with the stubs, which enforce WPF's one-logical-parent rule, and runs replay / render
  paths — catches runtime UI construction errors before the user opens the lab.
- `tests/fullcompile/ReportPreview.cs` writes the real HTML report from synthetic data
  (`tests/fullcompile/.build/report_*.html`); screenshot with
  `/opt/pw-browsers/chromium-1194/chrome-linux/chrome --headless --no-sandbox --screenshot=...`.
- Baselines: `tests/lifecycle_baseline.txt` and `tests/bh_detection_baseline.txt` must stay
  identical unless a change to BH / pool behaviour is intended and approved.
- UI code cannot be fully compiled here (no WPF/NinjaTrader assemblies); compile new UI methods
  against small WPF stand-ins before pushing, and state that the real compile is the user's F5.
