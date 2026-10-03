// 123 ENGULFING MATH LAB: every timeframe × BUY/SELL/BOTH × strength × run × target × stop × contracts, prop + walk-forward.
// Run: tools/compile_engine.sh tests/.build/elab.exe tests/EngLabTests.cs && mono tests/.build/elab.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class EngLabTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.01; }

    public static int Main()
    {
        var t0 = new DateTime(2025, 3, 3, 10, 0, 0); var close = new DateTime(2025, 3, 3, 15, 55, 0);
        var sh = new List<KeystoneArcBar> { new KeystoneArcBar { Symbol = "MNQ", Time = t0, Open = 20000, High = 20010, Low = 19940, Close = 19950 }, new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(1), Open = 19950, High = 20060, Low = 19945, Close = 20055 } };
        var ts = KeystoneLab.Bracket(sh, 0, 20000, -1, 50, 50, close, "MNQ", true);
        Check(ts.Outcome == "TARGET" && Eq(ts.Exit, 19950) && Eq(ts.Net, 100 - KeystoneMoveStudy.Cost("MNQ")), "SELL at 20000, target 50 → 19950 = +$100 less costs (market fill: the fill minute counts)", ts.Outcome + " " + ts.Exit);
        var ts2 = KeystoneLab.Bracket(sh, 1, 19950, -1, 50, 50, close, "MNQ", true);
        Check(ts2.Outcome == "STOP" && Eq(ts2.Exit, 20000), "SELL stop above: a minute through both target and stop = the stop", ts2.Outcome + " " + ts2.Exit);

        var rng = new Random(5); var bars = new List<KeystoneArcBar>(); double pn = 20000, pg = 2000;
        for (var day = new DateTime(2024, 1, 2); day < new DateTime(2025, 12, 20); day = day.AddDays(1))
        {
            if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
            for (var t = day.AddHours(7).AddMinutes(1); t <= day.AddHours(16); t = t.AddMinutes(1))
            {
                double o1 = pn, o2 = pg; pn += (rng.NextDouble() - 0.5) * 9; pg += (rng.NextDouble() - 0.5) * 1.1;
                bars.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = o1, High = Math.Max(o1, pn) + rng.NextDouble() * 3, Low = Math.Min(o1, pn) - rng.NextDouble() * 3, Close = pn });
                bars.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = o2, High = Math.Max(o2, pg) + rng.NextDouble() * 0.4, Low = Math.Min(o2, pg) - rng.NextDouble() * 0.4, Close = pg });
            }
        }
        // the lab's signals = the lab detector's 123 ENGULFING triggers on the same 5-minute candles
        var mnq = bars.Where(b => b.Symbol == "MNQ").ToList(); var five = KeystoneEngLab.Candles(mnq, 5);
        var cfg = new KeystoneArcRunConfig { StrategyCode = "ENG", Scope = "MNQ", SessionMode = "INSTRUMENT_DEFAULT", MnqStart = 930, MgcStart = 800, EndTime = 1555, SetupMinutes = 5, OutcomeModelEnabled = 1, EngDirection = "BOTH", EngStrength = "ALL", EngMinRun = 1, EngStopMode = "POINTS", Start = new DateTime(2024, 1, 1), End = new DateTime(2024, 3, 1) };
        var detector = KeystoneArcEngine.DetectAndResolve(mnq, five, cfg).Where(e => e.TriggerTime.Hour * 100 + e.TriggerTime.Minute < 1555).Select(e => e.TriggerTime + (e.Direction == "LONG" ? "B" : "S")).ToList();
        var lab = KeystoneEngLab.Signals(five, "MNQ", 0, 930, 1555).Where(s => s.Time >= cfg.Start && s.Time <= cfg.End).Select(s => s.Time + (s.Buy ? "B" : "S")).ToList();
        Check(detector.Count > 20 && detector.SequenceEqual(lab), "the math lab finds exactly the lab detector's engulfing signals (5M, Jan–Feb 2024)", detector.Count + " vs " + lab.Count + " • first diff " + detector.Except(lab).Concat(lab.Except(detector)).FirstOrDefault());
        var c15 = KeystoneEngLab.Candles(mnq.Where(b => b.Time.Date == new DateTime(2024, 1, 2)).ToList(), 15);
        Check(c15.All(c => c.Time.Minute % 15 == 0) && c15.Count == 36, "15-minute candles close-stamped on :00 :15 :30 :45 (07:00 → 16:00 = 36)", c15.Count.ToString());

        var g = new KeystoneEngLabGrid(); var rules = new KeystonePropRules();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = KeystoneEngLab.Run(bars, g, rules, null, null);
        Console.WriteLine("      " + res.Rows.Count + " rows in " + sw.ElapsedMilliseconds + " ms • signals " + string.Join(", ", res.Signals.Select(kv => kv.Key + " " + kv.Value)));
        Check(res.Rows.Count == 2 * 5 * 3 * 2 * 1 * 3 * 2 * 1, "rows: MNQ/MGC × 5 timeframes × BUY/SELL/BOTH × 2 strengths × 3 targets × 2 stops", res.Rows.Count.ToString());
        var row = res.Rows.First(r => r.Get("INSTRUMENT") == "MNQ" && r.Get("TIMEFRAME") == "15M" && r.Get("DIRECTION") == "BOTH" && r.Get("SIGNAL") == "ALL" && r.Get("TARGET") == "TP 50" && r.Get("STOP") == "SL 25");
        bool noOverlap = true; for (int i = 1; i < row.Trades.Count; i++) if (row.Trades[i].EntryTime <= row.Trades[i - 1].ExitTime) noOverlap = false;
        Check(noOverlap && row.Trades.All(t => t.Outcome != "TARGET" || Eq(t.Points, 50)) && row.Trades.All(t => t.Outcome != "STOP" || t.Points <= -25 + 1e-9), "one position at a time; targets +50, stops ≥ −25 points");
        var buy = res.Rows.First(r => r.Get("INSTRUMENT") == "MNQ" && r.Get("TIMEFRAME") == "15M" && r.Get("DIRECTION") == "BUY" && r.Get("SIGNAL") == "ALL" && r.Get("TARGET") == "TP 50" && r.Get("STOP") == "SL 25");
        var sell = res.Rows.First(r => r.Get("INSTRUMENT") == "MNQ" && r.Get("TIMEFRAME") == "15M" && r.Get("DIRECTION") == "SELL" && r.Get("SIGNAL") == "ALL" && r.Get("TARGET") == "TP 50" && r.Get("STOP") == "SL 25");
        Check(buy.Trades.Count > 0 && sell.Trades.Count > 0 && row.Trades.Count <= buy.Trades.Count + sell.Trades.Count, "BUY and SELL rows separate; BOTH takes from both (fewer when positions overlap)", buy.Trades.Count + " + " + sell.Trades.Count + " ≥ " + row.Trades.Count);
        var sw2 = res.Rows.First(r => r.Get("INSTRUMENT") == "MNQ" && r.Get("TIMEFRAME") == "15M" && r.Get("DIRECTION") == "BOTH" && r.Get("SIGNAL") == "SWEEP+WICK" && r.Get("TARGET") == "TP 50" && r.Get("STOP") == "SL 25");
        Check(sw2.Trades.Count < row.Trades.Count, "SWEEP+WICK is a stricter filter than ALL", sw2.Trades.Count + " < " + row.Trades.Count);
        Check(Eq(KeystonePropPlanner.History(rules, new KeystonePropPlan { Source = "REAL" }, row.P.Days).Sum(y => y.Net), row.P.PropNet), "prop result = the shared prop history");
        var w = KeystoneLab.WalkForward(res.Rows, rules, 1, r => r.Get("INSTRUMENT") == "MGC");
        Check(w.Years.Count == 1 && w.Years[0].Chosen.Get("INSTRUMENT") == "MGC", "walk-forward per instrument");
        var adv = KeystoneEngLab.Advice(res); foreach (var l in adv.Take(7)) Console.WriteLine("      " + l);
        Check(adv.Count(l => l.Contains(" • BUY:") || l.Contains(" • SELL:") || l.Contains(" • BOTH:")) == 6 && adv.Any(l => l.Contains("TIMEFRAMES")), "advice compares BUY / SELL / BOTH and the timeframes per instrument");
        Console.WriteLine(failures == 0 ? "ALL ENGULFING LAB TESTS PASSED" : failures + " ENGULFING LAB TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
