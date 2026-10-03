// PROP GAME OPTIMIZER: the row with your current settings = the normal pool; parallel = sequential; ranked by net.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class GameOptimizerTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    public static int Main()
    {
        var ev = LifecycleSnapshot.BhEvents(3); var cfg = LifecycleSnapshot.Cfg("BH", 1, 0, 0);
        var basePool = KeystoneArcEngine.SimulatePool(ev.Select(e => e.CopyForPool()).ToList(), cfg); var bx = KeystoneArcPoolInsights.Build(basePool, null, cfg);
        var dates = ev.Select(e => e.EntryTime.Date).Distinct().OrderBy(d => d).ToList(); var split = dates[(int)(dates.Count * 0.7)];
        var one = KeystoneArcGameOptimizer.One(ev, cfg, "BOTH", "ALL", null, 1, cfg.DailyGoal, cfg.DailyLoss, cfg.PoolSize, split);
        Check(Math.Abs(one.Net - bx.Net) < 0.01 && one.Payouts == bx.Payouts, "current settings through the optimizer = the normal pool", one.Net + " vs " + bx.Net);
        var g = new KeystoneArcGameGrid { Instruments = new List<string> { "BOTH", "MNQ" }, Sizes = new List<double> { 1, 2 }, DayProfits = new List<double> { 1000, 2000 }, DayLosses = new List<double> { 500, 1000 }, Accounts = new List<int> { 5, 10 } };
        var rows = KeystoneArcGameOptimizer.Run(ev, cfg, g, null, null);
        var again = KeystoneArcGameOptimizer.Run(ev, cfg, g, null, null);
        Check(rows.Count > 0 && rows.Count == again.Count && rows.Zip(again, (a, b) => a.Label == b.Label && Math.Abs(a.Net - b.Net) < 0.01).All(x => x), "parallel runs give the same table twice", rows.Count.ToString());
        Check(rows.Zip(rows.Skip(1), (a, b) => a.Net >= b.Net).All(x => x), "ranked by net cash");
        var x2 = rows.First(r => r.Size == 2 && r.Instrument == "BOTH" && r.Hours == "ALL");
        Check(x2.Setups == ev.Count, "size 2× keeps every setup");
        var adv = KeystoneArcGameOptimizer.Advice(rows, one);
        Check(adv.Any(l => l.StartsWith("MOST CASH")) && adv.Any(l => l.StartsWith("SIZE")), "advice lines", string.Join(" | ", adv.Take(2)));
        Console.WriteLine(failures == 0 ? "ALL GAME OPTIMIZER TESTS PASSED" : failures + " GAME OPTIMIZER TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
