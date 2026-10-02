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

    static KeystoneArcBar B(DateTime t, double o, double h, double l, double c) { return new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = o, High = h, Low = l, Close = c }; }
    // A NY day of close-stamped 1-minute bars 07:01 → 16:00, flat at 20000 unless a minute is overridden.
    static List<KeystoneArcBar> FlatDay(DateTime day, Dictionary<int, double[]> overrides, double tail = 20000)
    {
        var list = new List<KeystoneArcBar>();
        for (int m = 7 * 60 + 1; m <= 16 * 60; m++)
        {
            double[] v; var t = day.Date.AddMinutes(m);
            if (overrides.TryGetValue(m, out v)) list.Add(B(t, v[0], v[1], v[2], v[3])); else list.Add(B(t, tail, tail + 1, tail - 1, tail));
        }
        return list;
    }
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
        // 10. REAL BRACKET on hand-made bars
        {
            var day = new DateTime(2025, 3, 4);
            // 09:31-09:35 green candle (19990 → 20000); 09:36 opens 20000 (entry), later 09:50 reaches +150
            var o = new Dictionary<int, double[]> { { 9 * 60 + 31, new[] { 19990.0, 19995, 19989, 19994 } }, { 9 * 60 + 35, new[] { 19998.0, 20001, 19997, 20000 } }, { 9 * 60 + 50, new[] { 20000.0, 20151, 19999, 20140 } } };
            var days = KeystoneBracket.Days(FlatDay(day, o), "MNQ");
            var p = KeystoneBracket.Path(days[0], 935, 1555, "FOLLOW5");
            Check(p.Ok && p.Dir == 1 && Eq(p.Entry, 20000), "bracket: green 09:30-09:35 candle → BUY at the 09:36 open 20000", p.Why + " dir " + p.Dir + " entry " + p.Entry);
            double pts, worst, best; string how;
            KeystoneBracket.Resolve(p, 150, 200, out pts, out worst, out best, out how);
            Check(how == "TARGET" && Eq(pts, 150), "bracket: +150 reached at 09:50 → TARGET", how + " " + pts);
            var fade = KeystoneBracket.Path(days[0], 935, 1555, "FADE5");
            KeystoneBracket.Resolve(fade, 150, 200, out pts, out worst, out best, out how);
            Check(fade.Dir == -1 && how == "CLOSE" && Eq(pts, 0) && Eq(worst, -151), "bracket: FADE sells 20000; the spike is 151 against (< 200 stop) → closes at 15:55 at 20000 = 0, worst −151", how + " " + pts + " " + worst);
            // both in one minute → stop first
            var o2 = new Dictionary<int, double[]>(o); o2[9 * 60 + 50] = new[] { 20000.0, 20160, 19790, 20000 };
            var p2 = KeystoneBracket.Path(KeystoneBracket.Days(FlatDay(day, o2), "MNQ")[0], 935, 1555, "LONG");
            KeystoneBracket.Resolve(p2, 150, 200, out pts, out worst, out best, out how);
            Check(how == "STOP" && Eq(pts, -200), "bracket: a minute touching +160 and −210 counts as the STOP", how + " " + pts);
            // missing entry minute → no trade
            var gap = FlatDay(day, o).Where(b => b.Time != day.Date.AddMinutes(9 * 60 + 36)).ToList();
            Check(!KeystoneBracket.Path(KeystoneBracket.Days(gap, "MNQ")[0], 935, 1555, "LONG").Ok, "bracket: no 09:36 bar → no trade (no invented price)");
            // dollars: evaluation 5 MNQ, $1,500 / $2,000 = 150 / 200 points; costs 5 × (1.24 + 0.50)
            var bplan = new KeystonePropPlan { Source = "BRACKET", Bracket = new KeystoneBracketSet { Symbol = "MNQ", Days = new List<KeystoneBracketPath> { p } }, EvalContracts = 5, EvalTarget = 1500, EvalStop = 2000, FundContracts = 1, FundTarget = 200, FundStop = 600 };
            var acct = KeystonePropPlanner.NewEval(r);
            var d1 = KeystoneBracket.Day(p, r, bplan, acct);
            Check(Eq(d1.Pnl, 1500 - 5 * 1.74) && d1.Traded, "bracket: evaluation day = 5 × 150 pts × $2 − costs = $1,491.30", d1.Pnl.ToString("0.00"));
            acct.Bal = 2900; acct.Best = 1450; acct.Days = 2;
            var d2 = KeystoneBracket.Day(p, r, bplan, acct);
            Check(d2.Pnl > 100 && d2.Pnl < 120, "bracket: $100 left → aim only for 11 points (+$110 − costs)", d2.Pnl.ToString("0.00"));
            var funded = new KeystonePropPlanner.Account { Funded = true };
            Check(Eq(KeystoneBracket.Day(p, r, bplan, funded).Pnl, 200 - 1.74), "bracket: funded 1 × 100 pts = $200 − costs");
            var close = new KeystonePropPlanner.Account { Bal = -1500, Hwm = 0 };   // room 500 → the stop shrinks to fit
            var d3 = KeystoneBracket.Day(p2, r, bplan, close);
            Check(d3.Pnl < 0 && d3.Pnl > -500 && d3.Worst > -500, "bracket: with $500 of room the stop shrinks so the account survives", d3.Pnl.ToString("0.00"));
            Check(KeystoneBracket.RuleSteps(bplan).Count == 6 && KeystoneBracket.PlanText(bplan).Contains("150"), "bracket: the rule as steps + bplan text", KeystoneBracket.PlanText(bplan));
            // random-walk years with an up-drift at 10:00 → the sweet spot runs, ranks and checks every year
            var rng = new Random(9); var all = new List<KeystoneArcBar>();
            for (var dd = new DateTime(2024, 1, 2); dd < new DateTime(2025, 12, 31); dd = dd.AddDays(1))
            {
                if (dd.DayOfWeek == DayOfWeek.Saturday || dd.DayOfWeek == DayOfWeek.Sunday) continue;
                double px = 20000;
                for (int m = 7 * 60 + 1; m <= 16 * 60; m++) { double o3 = px; px += (rng.NextDouble() - 0.5) * 12 + (m > 600 ? 0.4 : 0); all.Add(B(dd.Date.AddMinutes(m), o3, Math.Max(o3, px) + 1, Math.Min(o3, px) - 1, px)); }
            }
            Check(KeystoneBracket.IsOneMinute(all), "bracket: 1-minute series detected");
            var md = KeystoneBracket.Days(all, "MNQ");
            var set = KeystoneBracket.Build(md, "MNQ", 1000, 1555, "LONG");
            var lp = bplan.Copy(); lp.Bracket = set;
            var bev = KeystonePropPlanner.Evaluate(r, lp, null, 1000, 3);
            var hist = KeystonePropPlanner.History(r, lp, null);
            Console.WriteLine("      " + set.Traded + " days • pass " + bev.PassRate.ToString("0") + "% • value " + bev.ValuePerEval.ToString("0")); Check(set.Traded > 400 && bev.Evaluations == 1000 && hist.Count == 2, "bracket: 2 years replayed, evaluated and walked year by year", set.Traded + " days • pass " + bev.PassRate.ToString("0") + "% • value " + bev.ValuePerEval.ToString("0") + " • " + string.Join(" | ", hist.Select(h => h.Year + " " + h.Net.ToString("0"))));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sweet = KeystoneBracket.SweetSpot(md, "MNQ", 1555, r, bplan, 800, 5, null); Console.WriteLine("      " + sweet.Count + " plans in " + sw.ElapsedMilliseconds + " ms • #1 " + sweet[0].Label + " • " + sweet[0].YearText);
            Check(sweet.Count > 100 && sweet[0].YearText.Contains("2024") && sweet[0].YearText.Contains("2025") && sweet[0].Plan.Bracket.Direction == "LONG", "bracket sweet spot finds the up-drift (ALWAYS BUY) and checks each year", sweet.Count + " plans in " + sw.ElapsedMilliseconds + " ms • #1 " + sweet[0].Label + " • " + sweet[0].YearText);
            Check(sweet.Any(x => Eq(x.Plan.FundTarget, 800) && x.Plan.FundContracts == 4), "bracket sweet spot also tries the $800-a-day max-payout pace with 4 contracts");
            string html = KeystonePropPlanner.Html(sweet[0], new List<KeystonePropProgram>(), hist, sweet, "REAL BRACKET • MNQ");
            // PROOF TEST: save the top rules, re-run them on 2025 only, round-trip through the text format
            var saved = sweet.Take(5).Select((x, i) => KeystoneBracket.SavedRule.From(x, KeystoneBracket.RangeText(md.Where(d => d.Day.Year == 2024).ToList()), i + 1)).ToList();
            var back = saved.Select(x => KeystoneBracket.SavedRule.FromLine(x.ToLine())).ToList();
            Check(back.All(x => x != null) && back[0].Entry == saved[0].Entry && back[0].Direction == saved[0].Direction && Eq(back[0].ES, saved[0].ES) && back[0].FoundRange == saved[0].FoundRange, "proof: saved rules survive the file format", saved[0].ToLine());
            var newer = md.Where(d => d.Day.Year == 2025).ToList();
            var proof = KeystoneBracket.ProofTest(newer, back, r, bplan, 400, 7);
            Check(proof.Count == 5 && proof.All(t => t.Item2.Plan.Bracket.Traded > 200) && proof[0].Item2.ValuePerEval > 0, "proof: the up-drift rule still works on the other year", proof[0].Item2.Label + " → " + proof[0].Item2.ValuePerEval.ToString("0"));
            Check(!KeystoneBracket.Overlaps(back[0].FoundRange, KeystoneBracket.RangeText(newer)) && KeystoneBracket.Overlaps("2024-01-01 → 2024-12-31", "2024-06-01 → 2025-01-31"), "proof: overlap check", back[0].FoundRange + " vs " + KeystoneBracket.RangeText(newer));
            string csv = KeystoneBracket.DayCsv(proof[0].Item2.Plan);
            Check(csv.Split('\n').Length > 250 && csv.Contains("TARGET") && csv.StartsWith("date,traded"), "proof: one CSV row per day with both sizes' results");
            string ph = KeystonePropPlanner.Html(proof[0].Item2, new List<KeystonePropProgram>(), null, sweet, "REAL BRACKET • MNQ", proof, KeystoneBracket.RangeText(newer), "2024");
            Check(ph.Contains("PROOF TEST") && ph.Contains("new data the rules never saw") && ph.Contains("<td>" + sweet.Count + "</td>") && ph.Contains("all " + sweet.Count + " plans"), "proof: report has the proof table and every sweet spot row");
            Check(html.Contains("THE RULE TO FOLLOW") && html.Contains("BY YEAR"), "bracket report shows the rule and the year check");
        }
        Console.WriteLine(failures == 0 ? "ALL PROP PLANNER TESTS PASSED" : failures + " PROP PLANNER TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
