// PROP PLANNER: firm rules, account state machine, coin-flip math, programs (SEPARATE / COPY / ROTATION) and history.
// Run: tools/compile_engine.sh tests/.build/prop.exe tests/PropPlannerTests.cs && mono tests/.build/prop.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class PropPlannerTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.005; }
    static KeystonePropDay Day(double pnl, double worst = double.NaN) { return new KeystonePropDay { Pnl = pnl, Worst = double.IsNaN(worst) ? Math.Min(0, pnl) : worst, Best = Math.Max(0, pnl), Traded = true }; }

    public static int Main()
    {
        var r = new KeystonePropRules();   // 50K: target 3,000, EOD 2,000 locking at +100, 50% consistency, 5 × $150 → 50% up to 2,000, 90%
        // 1. The famous number: pass in 2 days with no edge = (2000 / 3500)^2 = 32.65%
        Check(Math.Abs(KeystonePropPlanner.CoinPassChance(3000, 2000, 2) - 0.32653) < 0.0001, "pure luck, 2 days of +1,500 vs −2,000 → 32.65%", KeystonePropPlanner.CoinPassChance(3000, 2000, 2).ToString("0.0000"));
        // 2. The simulator agrees with the formula (no day costs)
        var plan = new KeystonePropPlan { EvalTarget = 1500, EvalStop = 2000, FundTarget = 200, FundStop = 1000, DayCost = 0 };
        var ev = KeystonePropPlanner.Evaluate(r, plan, null, 20000, 7);
        Check(Math.Abs(ev.PassRate - 32.65) < 1.0, "simulated pass rate ≈ 32.65%", ev.PassRate.ToString("0.00"));
        Check(Math.Abs(ev.AvgDaysToPass - 2) < 0.01, "passing takes exactly 2 days with +1,500 days", ev.AvgDaysToPass.ToString("0.00"));
        // 3. Consistency: +2,000 then +1,000 = 3,000 but the best day is 67% → not passed; +1,000 more → 4,000, best 50% → passed
        {
            var a = KeystonePropPlanner.NewEval(r);
            KeystonePropPlanner.Step(a, r, Day(2000)); KeystonePropPlanner.Step(a, r, Day(1000));
            Check(!a.Funded && Eq(a.Bal, 3000), "consistency: 3,000 with a 2,000 day is NOT a pass at 50%");
            KeystonePropPlanner.Step(a, r, Day(1000));
            Check(a.Funded && a.PassedEval && Eq(a.Bal, 0), "consistency: 4,000 with a 2,000 day passes → funded at 0");
            var r35 = r.Copy(); r35.Consistency = 35; var b = KeystonePropPlanner.NewEval(r35);
            foreach (var p in new[] { 1000.0, 1000, 1000 }) KeystonePropPlanner.Step(b, r35, Day(p));
            Check(b.Funded, "35%: three 1,000 days → best day 33% ≤ 35% → pass", "funded " + b.Funded);
            var c = KeystonePropPlanner.NewEval(r35);
            foreach (var p in new[] { 1500.0, 1500 }) KeystonePropPlanner.Step(c, r35, Day(p));
            Check(!c.Funded, "35%: two 1,500 days (50% each) do not pass");
        }
        // 4. EOD drawdown trails the best close and stops at +100
        {
            var a = KeystonePropPlanner.NewEval(r);
            Check(Eq(a.Threshold(r), -2000), "start: you blow up at −2,000");
            KeystonePropPlanner.Step(a, r, Day(500));
            Check(Eq(a.Threshold(r), -1500) && Eq(a.Room(r), 2000), "after +500 the floor trails to −1,500 (room stays 2,000)");
            KeystonePropPlanner.Step(a, r, Day(1600));
            Check(Eq(a.Threshold(r), 100) && Eq(a.Room(r), 2000), "after +2,100 the floor locks at +100");
            KeystonePropPlanner.Step(a, r, Day(500));
            Check(Eq(a.Threshold(r), 100) && Eq(a.Room(r), 2500), "it stays at +100 after more gains (room 2,500)");
            KeystonePropPlanner.Step(a, r, Day(-1000, -2550));
            Check(a.Dead, "a day that dips 2,550 against 2,500 of room blows the account even if it closes −1,000");
        }
        // 5. Payouts: 5 days of ≥ $150 → 50% of the profit, capped at 2,000, you keep 90%
        {
            var a = new KeystonePropPlanner.Account { Funded = true };
            double got = 0; for (int i = 0; i < 5; i++) got += KeystonePropPlanner.Step(a, r, Day(200));
            Check(Eq(got, 450) && a.Payouts == 1 && Eq(a.Bal, 500), "5 × +200 → payout 500 → you get 450, 500 stays", got + " bal " + a.Bal);
            var b = new KeystonePropPlanner.Account { Funded = true }; double g2 = 0;
            for (int i = 0; i < 5; i++) g2 += KeystonePropPlanner.Step(b, r, Day(1500));
            Check(Eq(g2, 1800) && Eq(b.Bal, 5500), "5 × +1,500 → 50% = 3,750 capped at 2,000 → you get 1,800", g2 + " bal " + b.Bal);
            var c = new KeystonePropPlanner.Account { Funded = true }; double g3 = 0;
            foreach (var p in new[] { 200.0, 100, 200, 100, 200, 200, 200 }) g3 += KeystonePropPlanner.Step(c, r, Day(p));
            Check(c.Payouts == 1 && Eq(g3, 0.9 * 0.5 * 1200), "days under $150 do not count toward the 5", g3.ToString());
        }
        // 6. COPY = exactly N × one account (same days on every copy)
        {
            var one = KeystonePropPlanner.Program(r, plan, null, "SEPARATE", 1, 120, 300, 11);
            var five = KeystonePropPlanner.Program(r, plan, null, "COPY", 5, 120, 300, 11);
            Check(Eq(five.P50, 5 * one.P50) && Eq(five.P10, 5 * one.P10) && Eq(five.MoneyNeeded95, 5 * one.MoneyNeeded95), "COPY 5 = 5 × one account (median, bad case, money needed)", one.P50 + " vs " + five.P50);
            var sep = KeystonePropPlanner.Program(r, plan, null, "SEPARATE", 5, 120, 300, 11);
            Check(sep.P90 - sep.P10 < five.P90 - five.P10, "SEPARATE 5 is less swingy than COPY 5", (sep.P90 - sep.P10) + " vs " + (five.P90 - five.P10));
            var rot = KeystonePropPlanner.Program(r, plan, null, "ROTATION", 5, 120, 100, 11);
            Check(rot.AvgBought >= 5 && rot.MedianCurve.Count == 6, "ROTATION runs and reports 6 months", rot.AvgBought + " " + rot.MedianCurve.Count);
        }
        // 7. Real days: history by year, sizing, day stop
        {
            var days = new List<KeystonePropDay>();
            var start = new DateTime(2024, 1, 2); var rng = new Random(3);
            for (int i = 0; i < 520; i++) { double p = rng.NextDouble() < 0.6 ? 400 : -500; days.Add(new KeystonePropDay { Day = start.AddDays(i * 1.4), Pnl = p, Worst = Math.Min(0, p) - 100, Best = Math.Max(0, p), Traded = true }); }
            var rp = new KeystonePropPlan { Source = "REAL" };
            var hist = KeystonePropPlanner.History(r, rp, days);
            Check(hist.Count >= 2 && hist.All(h => Eq(h.Spent, h.Bought * r.EvalCost)), "history: one row per year, spent = evaluations × $120", string.Join(" | ", hist.Select(h => h.Year + " " + h.Bought + " " + h.Spent)));
            Check(hist.Sum(h => h.Passed) > 0 && hist.Sum(h => h.Payouts) > 0, "history: a 60% / +400 −500 strategy passes and gets paid", string.Join(" | ", hist.Select(h => h.Year + " pass " + h.Passed + " pay " + h.Payouts + " net " + h.Net.ToString("0"))));
            var sc = KeystonePropPlanner.Scale(new KeystonePropDay { Pnl = 300, Worst = -700, Best = 500, Traded = true }, 2, 1000);
            Check(Eq(sc.Pnl, -1000) && Eq(sc.Worst, -1000), "size ×2 with a $1,000 day stop: a day that dipped −1,400 is stopped at −1,000");
            var ev2 = KeystonePropPlanner.Evaluate(r, rp, days, 2000, 5);
            Check(ev2.PassRate > 40 && ev2.ValuePerEval > 0, "a real edge makes one evaluation worth more than its cost", ev2.PassRate.ToString("0") + "% " + ev2.ValuePerEval.ToString("0"));
            var sweet = KeystonePropPlanner.SweetSpot(r, rp, days, 300, 5);
            Check(sweet.Count == 90 && sweet[0].ValuePerEval >= sweet[sweet.Count - 1].ValuePerEval, "sweet spot: 90 plans ranked by value per evaluation", sweet.Count + " best " + sweet[0].Label);
            var coinSweet = KeystonePropPlanner.SweetSpot(r, new KeystonePropPlan(), null, 200, 5);
            Check(coinSweet.Count > 20, "coin sweet spot builds its grid from the rules", coinSweet.Count.ToString());
        }
        // 8. Days from a RECOIL run: no-ladder days are 0, two ladders on a day add up
        {
            var res = new KeystoneRecoilResult();
            var d1 = new DateTime(2025, 5, 1); var d2 = new DateTime(2025, 5, 2); var d3 = new DateTime(2025, 5, 5);
            res.Days.AddRange(new[] { d1, d2, d3, d1 });
            res.Cycles.Add(new KeystoneRecoilCycle { Day = d1, Symbol = "MNQ", Net = 395, MaeValue = -300, MfeValue = 400 });
            res.Cycles.Add(new KeystoneRecoilCycle { Day = d1, Symbol = "MGC", Net = 95, MaeValue = -200, MfeValue = 100 });
            res.Cycles.Add(new KeystoneRecoilCycle { Day = d3, Symbol = "MGC", Net = -1210, MaeValue = -1200, MfeValue = 50 });
            var both = KeystonePropPlanner.DaysFromRecoil(res, "BOTH");
            Check(both.Count == 3 && Eq(both[0].Pnl, 490) && Eq(both[0].Worst, -500) && !both[1].Traded && Eq(both[1].Pnl, 0) && Eq(both[2].Pnl, -1210), "BOTH: 3 calendar days, day 1 = 395 + 95, worst −500, day 2 no ladder");
            var mgc = KeystonePropPlanner.DaysFromRecoil(res, "MGC");
            Check(mgc.Count == 3 && Eq(mgc[0].Pnl, 95), "MGC only keeps the gold ladders");
        }
        // 9. Report and verdict
        {
            var x = KeystonePropPlanner.Evaluate(r, plan, null, 2000, 3);
            var progs = new List<KeystonePropProgram> { KeystonePropPlanner.Program(r, plan, null, "SEPARATE", 5, 60, 50, 3) };
            string html = KeystonePropPlanner.Html(x, progs, new List<KeystonePropYear> { new KeystonePropYear { Year = 2025, Bought = 3, Spent = 360 } }, KeystonePropPlanner.SweetSpot(r, plan, null, 100, 3), "COIN FLIP");
            Check(html.Contains("PROP PLANNER") && html.Contains("SEPARATE") && html.Contains("2025") && html.Contains("SWEET SPOT") && html.EndsWith("</html>"), "HTML report has the plan, programs, years and sweet spot");
            Check(KeystonePropPlanner.Verdict(x).Length > 40, "verdict text", KeystonePropPlanner.Verdict(x));
        }
        Console.WriteLine(failures == 0 ? "ALL PROP PLANNER TESTS PASSED" : failures + " PROP PLANNER TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
