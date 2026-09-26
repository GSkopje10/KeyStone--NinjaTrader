// Asian 75 reversal-cycle engine tests. Run with:  tests/build_engine.sh tests/Asian75EngineTests.cs
// These use synthetic 1-minute bars, so they prove the cycle rules, not NinjaTrader data coverage.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class Asian75EngineTests
{
    static int failures;
    static readonly DateTime Session = new DateTime(2025, 3, 10); // Monday; cycle runs Mon 18:00 → Tue 15:55

    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }

    static KeystoneArcRunConfig Cfg(string scope)
    {
        return new KeystoneArcRunConfig
        {
            StrategyCode = "ASIAN75", SessionMode = "ASIAN75", Scope = scope, SetupMinutes = 1,
            Start = Session.AddHours(18), End = Session.AddDays(1).AddHours(15).AddMinutes(55),
            AsianStartHhmm = 1800, AsianEndHhmm = 1555, AsianRiskMode = "CASH", AsianReversalLossDollars = 75,
            AsianCycleTargetDollars = 350, AsianCombinedStopLossDollars = 600, AsianDailyLossLimitDollars = 2000,
            AsianInstrumentStopLossDollars = 0, AsianMnqInstrumentStopLossDollars = 0, AsianMgcInstrumentStopLossDollars = 0,
            AsianStartingQuantity = 1, AsianMnqMaxReversals = 3, AsianMgcMaxReversals = 3,
            AsianMnqInitialDirection = "LONG", AsianMgcInitialDirection = "LONG"
        };
    }

    // Builds one bar per minute across the cycle. path(minuteIndex) returns (open, high, low, close)
    // for the minute that STARTS at 18:00 + minuteIndex. closeStamped = NinjaTrader convention: the bar
    // covering 18:00–18:01 is stamped 18:01. skip lists minute indexes with no trades (no bar).
    static List<KeystoneArcBar> Bars(string symbol, Func<int, double[]> path, bool closeStamped = true, int[] skip = null)
    {
        var list = new List<KeystoneArcBar>();
        DateTime open = Session.AddHours(18);
        int minutes = (int)(Session.AddDays(1).AddHours(15).AddMinutes(55) - open).TotalMinutes;
        for (int m = 0; m < minutes; m++)
        {
            if (skip != null && skip.Contains(m)) continue;
            double[] p = path(m);
            list.Add(new KeystoneArcBar { Symbol = symbol, Time = open.AddMinutes(m + (closeStamped ? 1 : 0)), Open = p[0], High = p[1], Low = p[2], Close = p[3], Volume = 100 });
        }
        return list;
    }

    static double[] Flat(double price) { return new[] { price, price + 0.25, price - 0.25, price }; }

    static List<KeystoneArcEvent> Run(KeystoneArcRunConfig cfg, List<KeystoneArcBar> bars)
    {
        return KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
    }

    static string Dump(List<KeystoneArcEvent> ev)
    {
        return string.Join(" | ", ev.Select(e => e.Symbol + " L" + e.Quantity + " " + e.Direction + " " + e.EntryTime.ToString("HH:mm") + "@" + e.Entry + "→" + e.ExitTime.ToString("HH:mm") + "@" + e.ExitPrice + " " + e.Outcome + " " + e.GrossPnl.ToString("0.##")));
    }

    public static int Main()
    {
        // 1. NinjaTrader close-stamped bars: the first bar after the 18:00 open is stamped 18:01.
        {
            var ev = Run(Cfg("MNQ"), Bars("MNQ", m => Flat(20000)));
            Check(ev.Count == 1 && ev[0].Outcome == "SESSION EXIT", "close-stamped data produces a cycle (was zero cycles)", Dump(ev));
            if (ev.Count > 0) Check(ev[0].Entry == 20000 && ev[0].EntryTime == Session.AddHours(18).AddMinutes(1), "entry is the 18:00 opening price, marked on the first (18:01-stamped) bar", Dump(ev));
        }
        // 2. Open-stamped data still works and is not shifted a minute late.
        {
            var ev = Run(Cfg("MNQ"), Bars("MNQ", m => Flat(20000 + m * 0.01), closeStamped: false));
            Check(ev.Count == 1 && ev[0].EntryTime == Session.AddHours(18) && ev[0].Entry == 20000, "open-stamped data enters on the 18:00 bar", Dump(ev));
        }
        // 3. Full reversal to target: long x1 stopped ($75 = 37.5 pts), reverse short x2, target $350 combined.
        {
            Func<int, double[]> path = m =>
            {
                if (m < 5) return Flat(20000);
                if (m == 5) return new[] { 20000, 20000.25, 19960, 19962 };          // long x1 stop at 19962.5
                return Flat(19962 - (m - 5) * 2.0);                                   // steady decline favours the short
            };
            var ev = Run(Cfg("MNQ"), Bars("MNQ", path));
            var loss = ev.FirstOrDefault(e => e.Outcome == "LOSS");
            var win = ev.FirstOrDefault(e => e.Outcome == "WIN");
            Check(loss != null && Math.Abs(loss.GrossPnl + 75) < 0.01 && loss.ExitPrice == 19962.5, "leg 1 stops at exactly -$75", Dump(ev));
            Check(win != null && win.Direction == "SHORT" && win.Quantity == 2, "reversal enters SHORT x2 and reaches the combined target", Dump(ev));
            if (win != null) Check(win.EntryTime == loss.ExitTime.AddMinutes(1), "reversal enters on the next 1M bar open", Dump(ev));
            Check(ev.Sum(e => e.GrossPnl) >= 350, "cycle P/L is at least the $350 target", ev.Sum(e => e.GrossPnl).ToString());
        }
        // 4. A stop followed by a minute with no trades must still reverse at the next available bar.
        {
            Func<int, double[]> path = m =>
            {
                if (m < 5) return Flat(20000);
                if (m == 5) return new[] { 20000, 20000.25, 19960, 19962 };
                return Flat(19962 - (m - 5) * 2.0);
            };
            var ev = Run(Cfg("MNQ"), Bars("MNQ", path, skip: new[] { 6 }));
            Check(ev.Any(e => e.Direction == "SHORT" && e.Quantity == 2), "reversal still happens when the next minute has no bar (was stuck forever)", Dump(ev));
        }
        // 5. Stop hit inside the entry bar itself (entry is at that bar's open, so its range is after entry).
        {
            Func<int, double[]> path = m => m == 0 ? new[] { 20000, 20000.25, 19960, 19961 } : Flat(19961);
            var ev = Run(Cfg("MNQ"), Bars("MNQ", path));
            Check(ev.Count > 0 && ev[0].Outcome == "LOSS" && ev[0].ExitTime == Session.AddHours(18).AddMinutes(1), "stop inside the entry bar is detected", Dump(ev));
        }
        // 6. A bar that opens beyond the stop fills at that open (gap), not at the better stop price.
        {
            Func<int, double[]> path = m => m < 5 ? Flat(20000) : (m == 5 ? new[] { 19950.0, 19951, 19940, 19945 } : Flat(19945));
            var ev = Run(Cfg("MNQ"), Bars("MNQ", path));
            var loss = ev.FirstOrDefault(e => e.Outcome == "LOSS");
            Check(loss != null && loss.ExitPrice == 19950 && Math.Abs(loss.GrossPnl + 100) < 0.01, "gap through stop fills at the bar open (-$100, not -$75)", Dump(ev));
        }
        // 7. Max reversals: 3 reversals = 4 legs, then MNQ stops for the day.
        {
            // Alternating whipsaw: every minute swings 60 pts, stopping each leg.
            Func<int, double[]> path = m => new double[] { 20000, 20060, 19940, 20000 };
            var ev = Run(Cfg("MNQ"), Bars("MNQ", path));
            var losses = ev.Where(e => e.Outcome == "LOSS").ToList();
            Check(losses.Count == 4 && losses.Select(e => (int)e.Quantity).SequenceEqual(new[] { 1, 2, 3, 4 }), "4 legs x1→x4 then halt", Dump(ev));
            Check(Math.Abs(ev.Sum(e => e.GrossPnl) + 300) < 0.01, "max-leg day loses exactly 4 × $75 = $300", ev.Sum(e => e.GrossPnl).ToString());
        }
        // 8. BOTH: one shared cycle; target is combined MNQ + MGC.
        {
            var mnq = Bars("MNQ", m => Flat(20000 + m * 0.5));   // +$1/min at x1
            var mgc = Bars("MGC", m => Flat(2900 + m * 0.1));    // +$1/min at x1
            var ev = Run(Cfg("BOTH"), mnq.Concat(mgc).ToList());
            Check(ev.Count == 2 && ev.All(e => e.Outcome == "WIN") && ev.Select(e => e.ExitTime).Distinct().Count() == 1, "BOTH closes together on the combined target", Dump(ev));
        }
        // 9. Readiness receipt agrees with the engine on close-stamped data.
        {
            string receipt = KeystoneArcEngine.AsianCycleReadinessReceipt(Bars("MNQ", m => Flat(20000)), Cfg("MNQ"));
            Check(receipt.Contains("CYCLE READY") && !receipt.Contains("NO CYCLE"), "readiness receipt reports READY for close-stamped data", receipt);
        }
        // 10. Optimizer core: runs the same engine, counts combos, honours the lab's auto daily loss.
        {
            var mnq = Bars("MNQ", m => m < 5 ? Flat(20000) : (m == 5 ? new[] { 20000, 20000.25, 19960, 19962 } : Flat(19962 - (m - 5) * 2.0)));
            var grid = new KeystoneArcAsianGrid { Scopes = new List<string> { "MNQ" }, MnqDirections = new List<string> { "LONG", "SHORT" }, LegLosses = new List<double> { 75, 100 }, Reversals = new List<int> { 1, 3 }, Targets = new List<double> { 350 }, OutOfSampleFraction = 0, MinimumNights = 1, CostPerContract = 0 };
            var o = KeystoneArcAsianOptimizer.Run(mnq, grid, null, null);
            Check(o.Combinations == 8 && o.Ranked.Count == 8, "optimizer tests every combination (2 dirs x 2 losses x 2 reversals)", o.Combinations + " / " + o.Ranked.Count);
            var match = KeystoneArcAsianOptimizer.FindMatch(o, new KeystoneArcAsianCombo { Scope = "MNQ", MnqDirection = "LONG", MgcDirection = "LONG", RiskMode = "CASH", LegLoss = 75, Reversals = 3, Target = 350, StartHhmm = 1800 });
            var direct = Run(Cfg("MNQ"), mnq);
            Check(match != null && Math.Abs(match.All.Net - direct.Sum(e => e.GrossPnl)) < 0.01, "optimizer result equals the normal backtest for the same settings", match == null ? "no match" : match.All.Net + " vs " + direct.Sum(e => e.GrossPnl));
            var cfg = new KeystoneArcAsianCombo { Scope = "BOTH", MnqDirection = "LONG", MgcDirection = "LONG", RiskMode = "CASH", LegLoss = 75, Reversals = 4, Target = 350, StartHhmm = 1800, EndHhmm = 1555, StartingQuantity = 1 }.Apply(new KeystoneArcRunConfig());
            Check(cfg.AsianDailyLossLimitDollars == 600, "auto daily loss = leg x reversals x instruments (75 x 4 x 2 = 600), same as the lab", cfg.AsianDailyLossLimitDollars.ToString());
            var linked = KeystoneArcAsianOptimizer.BuildCombos(new KeystoneArcAsianGrid { Scopes = new List<string> { "BOTH" }, LinkDirections = true, LegLosses = new List<double> { 75 }, Reversals = new List<int> { 3 }, Targets = new List<double> { 350 } }, true, true);
            Check(linked.Count == 2 && linked.All(c => c.MnqDirection == c.MgcDirection), "linked directions give L/L and S/S only for BOTH", linked.Count.ToString());
        }
        // 11. MGC liquid-contract roll schedule (gold data loader).
        {
            var seg = KeystoneArcEngine.MgcLiquidRollSchedule(new DateTime(2026, 6, 24, 18, 0, 0), new DateTime(2026, 9, 25, 15, 55, 0));
            string got = string.Join(" | ", seg.Select(x => x.ContractMonth.ToString("MM-yy") + " " + x.Start.ToString("MM-dd HH:mm") + "→" + x.End.ToString("MM-dd HH:mm")));
            Check(got == "08-26 06-24 18:00→07-24 17:00 | 12-26 07-24 17:01→09-25 15:55", "Jun–Sep 2026 uses Aug then Dec, switching in the 17:00 halt (was Jun/Aug to expiry, then nothing)", got);
            var year = KeystoneArcEngine.MgcLiquidRollSchedule(new DateTime(2021, 1, 1), new DateTime(2022, 1, 1));
            string months = string.Join(",", year.Select(x => x.ContractMonth.ToString("MM-yy")));
            Check(months == "02-21,04-21,06-21,08-21,12-21,02-22", "a full year walks Feb, Apr, Jun, Aug, Dec (Oct skipped)", months);
            Check(year.Zip(year.Skip(1), (a, b) => (b.Start - a.End).TotalMinutes == 1).All(x => x), "segments are gapless", "");
        }
        // 12. Night-by-night summary groups legs per session and shows how the night ended.
        {
            var ev = Run(Cfg("MNQ"), Bars("MNQ", m => m < 5 ? Flat(20000) : (m == 5 ? new[] { 20000, 20000.25, 19960, 19962 } : Flat(19962 - (m - 5) * 2.0))));
            string text = KeystoneArcAsianOptimizer.NightlySummary(ev, 0);
            Console.WriteLine(text);
            Check(text.Contains("2 (Lx1 Sx2)") && text.Contains("TARGET") && text.Contains("NIGHTS 1 • PROFITABLE 1"), "nightly summary: legs, target ending, one profitable night", text);
        }
        // 13. User's 2026-09-13 pattern: MGC whipsaws through all legs, MNQ's first leg stops,
        //     its SHORT x2 then has to recover every loss; the night closes at the COMBINED target.
        {
            var mgc = Bars("MGC", m => new double[] { 2900, 2910, 2890, 2900 });                  // every leg stopped
            var mnq = Bars("MNQ", m => m == 0 ? new double[] { 20000, 20000.25, 19960, 19965 } : Flat(19965 - m * 1.0));
            var ev = Run(Cfg("BOTH"), mnq.Concat(mgc).ToList());
            double mgcPnl = ev.Where(e => e.Symbol == "MGC").Sum(e => e.GrossPnl), mnqPnl = ev.Where(e => e.Symbol == "MNQ").Sum(e => e.GrossPnl);
            var mnqWin = ev.FirstOrDefault(e => e.Symbol == "MNQ" && e.Outcome == "WIN");
            Check(Math.Abs(mgcPnl + 300) < 0.01 && ev.Count(e => e.Symbol == "MGC") == 4, "MGC stops out all 4 legs (-$300)", Dump(ev));
            Check(mnqWin != null && mnqWin.Direction == "SHORT" && mnqWin.Quantity == 2 && mnqWin.GrossPnl >= 725, "MNQ SHORT x2 wins at least $725 (= $350 + $75 + $300 recovered)", Dump(ev));
            Check(mgcPnl + mnqPnl >= 350 && mgcPnl + mnqPnl < 360, "night closes on the COMBINED +$350 (MNQ + MGC), not per instrument", (mgcPnl + mnqPnl).ToString());
            Check(KeystoneArcAsianOptimizer.NightEnding(ev).StartsWith("TARGET"), "night ending reads TARGET", KeystoneArcAsianOptimizer.NightEnding(ev));
            Check(ev.All(e => !double.IsNaN(e.AsianCyclePnlAtExit) && e.AsianLegNumber >= 1), "every leg records its number and the combined cycle P/L at its exit", "");
            Check(mnqWin != null && Math.Abs(mnqWin.AsianCyclePnlAtExit - (mgcPnl + mnqPnl)) < 0.01, "closing leg's cycle P/L equals the night result", mnqWin == null ? "" : mnqWin.AsianCyclePnlAtExit.ToString());
            Check(mnqWin != null && mnqWin.AsianCycleWorstAtExit < -300 && mnqWin.AsianCycleWorstAtExit <= ev.Min(e => e.AsianCyclePnlAtExit), "night's worst combined drawdown is recorded and is the lowest point of the night", mnqWin == null ? "" : mnqWin.AsianCycleWorstAtExit.ToString());
        }
        Console.WriteLine(failures == 0 ? "\nALL ASIAN 75 TESTS PASSED" : "\n" + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
