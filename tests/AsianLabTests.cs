// ASIAN MATH LAB: every combination on the loaded 1-minute bars, prop accounts, night summary for the chart.
// Run: tools/compile_engine.sh tests/.build/alab.exe tests/AsianLabTests.cs && mono tests/.build/alab.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class AsianLabTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.01; }

    static List<KeystoneArcBar> Data()
    {
        var rng = new Random(11); var all = new List<KeystoneArcBar>(); double pn = 20000, pg = 2000;
        for (var d = new DateTime(2024, 1, 7); d < new DateTime(2025, 12, 1); d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Friday || d.DayOfWeek == DayOfWeek.Saturday) continue;
            for (var t = d.AddHours(18).AddMinutes(1); t <= d.AddDays(1).AddHours(15).AddMinutes(55); t = t.AddMinutes(1))
            {
                double a = pn, b = pg; pn += (rng.NextDouble() - 0.5) * 8; pg += (rng.NextDouble() - 0.5) * 1.2;
                all.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = a, High = Math.Max(a, pn) + 1, Low = Math.Min(a, pn) - 1, Close = pn });
                all.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = b, High = Math.Max(b, pg) + 0.2, Low = Math.Min(b, pg) - 0.2, Close = pg });
            }
        }
        return all;
    }

    public static int Main()
    {
        var bars = Data(); var rules = new KeystonePropRules();
        var g = new KeystoneAsianLabGrid { Quantities = new List<int> { 1, 2 }, LegLosses = new List<double> { 75, 150 }, Reversals = new List<int> { 2, 3 }, Targets = new List<double> { 200, 400 }, MixedBoth = true };
        var combos = KeystoneAsianLab.Combos(g, true, true);
        Check(combos.Count == (2 + 2 + 4) * 2 * 2 * 2 * 2, "combinations: MNQ/MGC × LONG/SHORT + BOTH × 4 directions, × micros × leg loss × reversals × take profit", combos.Count.ToString());
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = KeystoneAsianLab.Run(bars, g, rules, null, null);
        Console.WriteLine("      " + res.Rows.Count + " combinations × " + res.Nights + " nights in " + sw.ElapsedMilliseconds + " ms • #1 " + res.Rows[0].Label + " • prop " + res.Rows[0].P.PropNet.ToString("0") + " • plain " + res.Rows[0].P.Net.ToString("0"));
        Check(res.Rows.Count == combos.Count && res.Nights > 400, "every combination measured on every night", res.Rows.Count + " / " + res.Nights);
        // the lab's night = the engine's own legs
        var pick = res.Rows.First(r => r.Combo.Scope == "BOTH" && r.Combo.MnqDirection == "LONG" && r.Combo.MgcDirection == "LONG" && r.Combo.StartingQuantity == 1 && r.Combo.LegLoss == 75 && r.Combo.Reversals == 3 && r.Combo.Target == 400);
        var cfg = pick.Combo.Apply(new KeystoneArcRunConfig()); cfg.Start = new DateTime(2024, 1, 1); cfg.End = new DateTime(2025, 12, 2);
        var ev = KeystoneArcEngine.SimulateAsian75Sessions(KeystoneArcEngine.PrepareAsian75Sessions(bars, cfg), cfg);
        double engineNet = ev.Sum(e => e.GrossPnl - e.Quantity * g.CostPerContract);
        Check(Eq(engineNet, pick.P.Net) && pick.Nights.Count == ev.Select(e => e.ReferenceTime.Date).Distinct().Count(), "lab result = the Asian engine's legs (net after $1/contract)", engineNet.ToString("0.00") + " vs " + pick.P.Net.ToString("0.00"));
        Check(pick.Nights.All(n => Eq(n.Mnq + n.Mgc, n.Net)) && Eq(pick.MnqNet + pick.MgcNet, pick.P.Net), "MNQ + MGC = combined, every night");
        Check(pick.Nights.All(n => n.Worst <= n.Net + 0.01 && n.Worst <= 0), "night worst point ≤ its result (feeds the prop drawdown)");
        Check(pick.EndLegs.Sum() == pick.Nights.Count && pick.TargetNights + pick.LossNights + pick.AllStoppedNights + pick.SessionEndNights + pick.Nights.Count(n => n.End == "BREAKEVEN") == pick.Nights.Count, "every night counted once by legs and by how it ended",
            string.Join(",", pick.EndLegs) + " • TP " + pick.TargetNights + " loss " + pick.LossNights + " stopped " + pick.AllStoppedNights + " end " + pick.SessionEndNights);
        var hist = KeystonePropPlanner.History(rules, new KeystonePropPlan { Source = "REAL" }, pick.P.Days);
        Check(Eq(hist.Sum(y => y.Net), pick.P.PropNet) && hist.Sum(y => y.Bought) == pick.P.Bought, "prop result = the shared prop history (same rules as the planner)");
        Check(Eq(pick.InSampleNet + pick.OutSampleNet, pick.P.Net), "early 70% + unseen last 30% = whole range");
        double before = pick.P.PropNet; KeystoneAsianLab.Detail(pick, rules, res.SplitAt);
        Check(Eq(before, pick.P.PropNet) && pick.P.Accounts.Count == pick.P.Bought && pick.P.Ledger.Count == pick.Nights.Count, "selected row in full = its ranking row (accounts, ledger)", before + " vs " + pick.P.PropNet);
        // quantity really changes the cycle: 2 micros at a $75 leg loss = a tighter stop, not 2× the P/L
        var q1 = res.Rows.First(r => r.Combo.Scope == "MNQ" && r.Combo.MnqDirection == "LONG" && r.Combo.StartingQuantity == 1 && r.Combo.LegLoss == 75 && r.Combo.Reversals == 2 && r.Combo.Target == 200);
        var q2 = res.Rows.First(r => r.Combo.Scope == "MNQ" && r.Combo.MnqDirection == "LONG" && r.Combo.StartingQuantity == 2 && r.Combo.LegLoss == 75 && r.Combo.Reversals == 2 && r.Combo.Target == 200);
        Check(!Eq(q1.P.Net * 2, q2.P.Net) && q2.Nights.Sum(n => n.Contracts) > q1.Nights.Sum(n => n.Contracts), "starting micros: 2 micros trade more contracts with a tighter stop (simulated, not multiplied)", q1.P.Net.ToString("0") + " / " + q2.P.Net.ToString("0"));
        // determinism: same input → same numbers
        var again = KeystoneAsianLab.Run(bars, g, rules, null, null);
        Check(again.Rows.Count == res.Rows.Count && again.Rows.Zip(res.Rows, (a, b) => a.Label == b.Label && Eq(a.P.PropNet, b.P.PropNet) && Eq(a.P.Net, b.P.Net)).All(x => x), "two runs give identical results");
        // night summary for the chart
        var night = ev.GroupBy(e => e.ReferenceTime.Date).First(x => x.Select(e => e.Symbol).Distinct().Count() == 2 && x.Count() >= 3).ToList();
        var mn = bars.Where(b => b.Symbol == "MNQ").ToList(); var mg = bars.Where(b => b.Symbol == "MGC").ToList();
        Func<string, DateTime, double?> closeAt = (sym, t) => { var l = sym == "MGC" ? mg : mn; int lo = 0, hi = l.Count - 1, f = -1; while (lo <= hi) { int mid = (lo + hi) / 2; if (l[mid].Time <= t) { f = mid; lo = mid + 1; } else hi = mid - 1; } return f < 0 ? (double?)null : l[f].Close; };
        var sum = KeystoneAsianLab.NightSummary(night, closeAt, mn.Select(b => b.Time), DateTime.MaxValue);
        double gross = night.Sum(e => e.GrossPnl);
        Console.WriteLine("      night " + night[0].ReferenceTime.ToString("yyyy-MM-dd") + ": " + string.Join(" | ", sum.Sides.Select(x => x.Symbol + " " + x.Legs + " legs " + x.Pnl.ToString("0") + " peak " + x.Peak.ToString("0") + " low " + x.Low.ToString("0"))) + " | combined " + sum.Combined.Pnl.ToString("0") + " peak " + sum.Combined.Peak.ToString("0") + " low " + sum.Combined.Low.ToString("0") + " • " + sum.End);
        Check(Eq(sum.Combined.Pnl, gross) && Eq(sum.Sides.Sum(x => x.Pnl), sum.Combined.Pnl), "night summary: final combined = the legs' P/L = MNQ + MGC", sum.Combined.Pnl + " vs " + gross);
        Check(sum.Combined.Peak >= sum.Combined.Pnl - 0.01 && sum.Combined.Low <= sum.Combined.Pnl + 0.01 && sum.Sides.All(x => x.Peak >= x.Low) && sum.End != "RUNNING" && sum.Sides.All(x => x.Legs == night.Where(e => e.Symbol == x.Symbol).Max(e => e.AsianLegNumber)), "highest peak / lowest point per instrument and combined, legs, how it ended");
        var mid2 = KeystoneAsianLab.NightSummary(night, closeAt, mn.Select(b => b.Time), night.Min(e => e.EntryTime).AddMinutes(1));
        Check(mid2.End == "RUNNING", "during replay the summary only shows the night up to the cursor");
        var advice = KeystoneAsianLab.Advice(res); var imp = KeystoneAsianLab.Impacts(res.Rows);
        foreach (var a in advice.Take(4)) Console.WriteLine("      " + a);
        Check(advice.Count >= 8 && imp.Select(x => x.Setting).Distinct().Count() >= 6, "advice + which value of each setting wins", advice.Count + " lines, " + imp.Select(x => x.Setting).Distinct().Count() + " settings");
        Console.WriteLine(failures == 0 ? "ALL ASIAN LAB TESTS PASSED" : failures + " ASIAN LAB TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
