// 09l: the OPEN iteration's headline is its TOTAL (open + this iteration's closed legs) — the ITERATIONS row, the 🛡 HEDGE banner, the chart
// strip's pill; 🦅 RUNNER's target 3× → 1×; the 🏁 RACE lane DEF (+ the compare set when MY SETTINGS are RUNNER); ⚙ SETTINGS' 💰 size preview
using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using KeystoneMT5;
public static class NineLTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 1e-9; }

    public static int Main14()
    {
        // ---------------------------------------------------------------- 1. the TOTAL is the headline (closed −90, open +20)
        {
            double open = 20, closed = -90, total = open + closed;
            var row = HedgeAuto.OpenRowResult(total, open, closed);
            Check(row.Item1 == "(−70.00)" && Eq(row.Item3, -70) && row.Item2.StartsWith("if closed now: open +20.00 + closed −90.00"),
                "🧮 ITERATIONS open row: RESULT (NET) = (−70.00) — the TOTAL, not the open +20; tooltip \"" + row.Item2 + "\"", row.Item1 + " | " + row.Item2);
            var hl = HedgeAuto.BannerHeadline(total, open, closed, 10);
            Check(hl.Item2.StartsWith("−$70.00") && hl.Item2.Contains("+$10") && Eq(hl.Item4, -70) && hl.Item1.StartsWith("TOTAL IF CLOSED NOW") && hl.Item3 == "open +$20.00  •  closed −$90.00",
                "🛡 HEDGE banner: the big number = TOTAL IF CLOSED NOW " + hl.Item2 + ", small \"" + hl.Item3 + "\"", hl.Item1 + " | " + hl.Item2 + " | " + hl.Item3);
            var pill = HedgeAuto.PillHeadline("XAUUSD", true, total, open, closed, 22.47, true);
            Check(pill.Item2 == "−$70.00" && Eq(pill.Item3, -70) && pill.Item1.Contains("TOTAL IF CLOSED NOW") && pill.Item1.Contains("open +$20.00") && pill.Item1.Contains("closed −$90.00"),
                "📈 strip pill: the big number = the TOTAL −$70.00, the open part small in the caption (\"" + pill.Item1 + "\")", pill.Item1 + " | " + pill.Item2);
            var pill0 = HedgeAuto.PillHeadline("XAUUSD", true, 22.4, 22.4, 0, 22.47, true); var pillOther = HedgeAuto.PillHeadline("EURUSD", false, double.NaN, 0, 0, 5, true); var pillNone = HedgeAuto.PillHeadline("XAUUSD", false, double.NaN, 0, 0, 0, false);
            Check(pill0.Item1 == "XAUUSD • P/L of this instrument" && pill0.Item2 == "+$22.47" && pillOther.Item2 == "+$5.00" && pillNone.Item2 == "no ticket",
                "📈 strip pill: no closed legs / another symbol → the instrument's P/L as before; nothing open → \"no ticket\"", pill0.Item1 + " " + pill0.Item2 + " | " + pillOther.Item2 + " | " + pillNone.Item2);
        }
        {   // the engine's own numbers: running → (BasketTotal, Basket, Banked); stopped → the 🎯 guard's count; no open iteration → NaN
            var au = new HedgeAuto(); var nan = au.LiveTotal();
            au.Current = new AutoIteration { N = 7, Start = new DateTime(2026, 11, 10, 9, 0, 0) }; au.Basket = 20; au.Banked = -90; au.BasketTotal = -70; au.GuardTotal = double.NaN;
            var st = au.LiveTotal(); au.GuardTotal = -71.5; au.GuardOpen = 18.5; var gd = au.LiveTotal(); au.Current.End = au.Current.Start.AddHours(1); var dn = au.LiveTotal();
            Check(double.IsNaN(nan.Item1) && Eq(st.Item1, -70) && Eq(st.Item2, 20) && Eq(st.Item3, -90) && Eq(gd.Item1, -71.5) && Eq(gd.Item2, 18.5) && Eq(gd.Item3, -90) && double.IsNaN(dn.Item1)
                && HedgeAuto.OpenRowResult(st.Item1, st.Item2, st.Item3).Item1 == "(−70.00)",
                "🧮 HedgeAuto.LiveTotal: the open iteration's TOTAL / open / closed (−70 = +20 − 90), stopped → the guard's count (−71.50 = 18.50 − 90), done / none → NaN", st + " " + gd + " " + dn);
        }
        // ---------------------------------------------------------------- 2. 🦅 RUNNER: the target 1× (TargetPerLot 200)
        {
            var b0 = new AutoSettings { Symbol = "XAUUSD" }; var r8 = AutoSettings.Model("RUNNER", b0, 8000); var r4 = AutoSettings.Model("RUNNER", b0, 431);
            Check(AutoSettings.RunnerTargetMult == 1 && Eq(r8.TargetPerLot, 200) && Eq(r8.EntryLots, 0.05) && Eq(r8.Target, 10) && Eq(r4.EntryLots, 0.01) && Eq(r4.Target, 2) && Eq(r8.AddLots, 0.10) && r8.CompressMode == "ALWAYS" && r8.GuardMode == "LOCK",
                "🦅 RUNNER 1×: $8,000 → 0.05 lots / $10 target, $431 → 0.01 / $2 (TargetPerLot 200), compress + the chop LOCK kept", r8.EntryLots + " " + r8.Target + " " + r4.EntryLots + " " + r4.Target + " " + r8.TargetPerLot);
            Check(AutoSettings.RunnerText.Contains("small entries 0.006 lot per $1,000") && AutoSettings.RunnerText.Contains("COMPRESS") && AutoSettings.RunnerText.Contains("chop LOCK") && AutoSettings.RunnerText.Contains("real costs") && AutoSettings.RunnerText.Contains("never blew the account") && AutoSettings.RunnerText.Contains("near breakeven") && !AutoSettings.RunnerText.Contains("3×"),
                "🦅 RUNNER's description: the 6-year replays with the brokers' real costs (most stable, never blew, near breakeven), no 3× any more", AutoSettings.RunnerText);
        }
        // ---------------------------------------------------------------- 3. 🏁 RACE: DEF
        {
            var mine = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000); mine.EntryLots = 0.33; mine.Target = 99;   // MY SETTINGS changed by hand
            var def = RaceLanes.For("DEF", mine, 5000); var ref5 = AutoSettings.Model("DEFENDER", mine, 5000);
            Check(def.Preset == "DEFENDER" && Eq(def.EntryLots, ref5.EntryLots) && !Eq(def.EntryLots, 0.33) && Eq(def.Target, ref5.Target) && def.Symbol == "XAUUSD" && !def.TrackManual && !RaceLanes.IsExperimental("DEF") && RaceLanes.Tip("DEF").Contains("🛡 DEFENDER, the default, on its own simulated account"),
                "🏁 RACE lane DEF = 🛡 DEFENDER sized from the race balance ($5,000 → " + def.EntryLots + " / $" + def.Target + "), not from MY SETTINGS (0.33 / $99)", def.Preset + " " + def.EntryLots + " " + def.Target);
            var run = AutoSettings.Model("RUNNER", mine, 8000);
            var cDef = RaceLanes.CompareFor(mine); var cRun = RaceLanes.CompareFor(run); var cNull = RaceLanes.CompareFor(null);
            Check(string.Join(",", cDef) == "MINE,RUN,REC,AOP,TAH" && string.Join(",", cRun) == "MINE,DEF,REC,AOP,TAH" && !cRun.Contains("RUN") && string.Join(",", cNull) == "MINE,RUN,REC,AOP,TAH" && RaceLanes.Keys.Contains("DEF") && RaceLanes.Keys.Contains("RUN"),
                "🏁 ▶ COMPARE WITH MY SETTINGS: MINE, RUN, REC, AOP, TAH — and MINE, DEF, REC, AOP, TAH when MY SETTINGS are 🦅 RUNNER (never RUNNER twice); DEF in the lane list", string.Join(",", cDef) + " | " + string.Join(",", cRun));
        }
        // ---------------------------------------------------------------- 4. ⚙ SETTINGS: the 💰 size preview
        {
            string p8 = HedgeAuto.SizePreview(0.006, 200, 8208, true), pf = HedgeAuto.SizePreview(0.006, 200, 8208, false), p0 = HedgeAuto.SizePreview(0, 200, 8208, true), pt = HedgeAuto.SizePreview(0.006, 600, 8208, true);
            string p4 = HedgeAuto.SizePreview(0.006, 200, 431, true), pn = HedgeAuto.SizePreview(0.01, 200, 0, false);
            var big = new Mt5Symbol { Name = "NAS100", VolMin = 0.1, VolStep = 0.1 }; string pb = HedgeAuto.SizePreview(0.01, 200, 8208, false, big);
            Check(p8 == "→ 0.05 lots now at $8,208 · target $10" && pf.StartsWith("→ 0.04 lots now at $8,208 · target $8") && p0 == "0 = the boxes (ENTRY LOTS / TARGET used as typed)" && pt == "→ 0.05 lots now at $8,208 · target $30",
                "💰 preview: \"" + p8 + "\" (RUNNER = the nearest step; the others round down: " + pf + "); TARGET $ PER LOT 600 → $30; 0 = the boxes", p8 + " | " + pf + " | " + p0 + " | " + pt);
            Check(p4.StartsWith("→ 0.01 lots now at $431 · target $2") && p4.Contains("⚠") && p4.Contains("minimum") && !p8.Contains("⚠") && pn.Contains("no balance") && pb.StartsWith("→ 0.10 lots") && pb.Contains("⚠"),
                "💰 preview: 0.01 above the plan → the ⚠ minimum-lot warning (\"" + p4.Replace("\n", " / ") + "\"); the symbol's own lot step / minimum", p4 + " | " + pn + " | " + pb);
        }
        Console.WriteLine(fails == 0 ? "ALL 09l TESTS PASSED" : fails + " 09l TEST(S) FAILED");
        return fails;
    }
}
