# Keystone Arc — working rules for Claude sessions

Read `docs/` first: the Manus takeover handoff (.docx), the Claude session handoff, and
`docs/ASIAN75.md` (Asian 75 rules, optimizer, data conventions).

## Delivering code to the user

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

- Historical research only: no live orders, broker/account access, or Apex/Nexus coupling.
- Do not change BH detection, BH outcome resolution, or the MNQ loader without explicit scope.
  MGC loader changes stay MGC-only. Asian work stays inside the Asian code paths.
- Never fix a zero result by relaxing the 1-minute outcome gate or inventing prices.
- This file shadows WPF names with brush fields (`Panel`, `Card`, `Text`, …): fully qualify WPF
  classes such as `System.Windows.Controls.Panel`.

## Verification available here (no NinjaTrader)

- `tests/build_engine.sh tests/Asian75EngineTests.cs` — compiles the engine block of
  `src/KeyStone.cs` with mono and runs the Asian/optimizer/MGC-roll tests.
- `mcs --parse src/KeyStone.cs` — syntax check of the whole file.
- UI code cannot be fully compiled here (no WPF/NinjaTrader assemblies); compile new UI methods
  against small WPF stand-ins before pushing, and state that the real compile is the user's F5.
