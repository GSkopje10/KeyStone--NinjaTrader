// GOLDEN SETUP tests on hand-made candles: the first BH or bullish FVG after the open, one a day.
// Run: tools/compile_engine.sh tests/.build/golden.exe tests/GoldenTests.cs && mono tests/.build/golden.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class GoldenTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }
    static double[] C(double o, double h, double l, double c) { return new[] { o, h, l, c }; }

    // 1-minute candles (setup timeframe = 1M) starting at the given clock time, then flat drift.
    static List<KeystoneArcBar> Day(string symbol, DateTime first, List<double[]> candles, int tailMinutes, double tailStep)
    {
        var list = new List<KeystoneArcBar>();
        DateTime t = first;
        foreach (var c in candles) { list.Add(new KeystoneArcBar { Symbol = symbol, Time = t, Open = c[0], High = c[1], Low = c[2], Close = c[3] }); t = t.AddMinutes(1); }
        double p = candles[candles.Count - 1][3];
        for (int k = 0; k < tailMinutes; k++) { double o = p; p += tailStep; list.Add(new KeystoneArcBar { Symbol = symbol, Time = t, Open = o, Close = p, High = Math.Max(o, p) + 0.25, Low = Math.Min(o, p) - 0.25 }); t = t.AddMinutes(1); }
        return list;
    }

    static KeystoneArcRunConfig Cfg(string scope)
    {
        return new KeystoneArcRunConfig
        {
            StrategyCode = "GLD", GoldenMnqMinGap = 0, GoldenMgcMinGap = 0, GoldenSkipStopOverTarget = 0, Scope = scope, SetupMinutes = 1, SessionMode = "INSTRUMENT_DEFAULT", EndTime = 1555, MnqStart = 930, MgcStart = 800,
            Start = new DateTime(2026, 3, 10), End = new DateTime(2026, 3, 11), OutcomeModelEnabled = 1, Quantity = 1, TargetDollars = 1500, StopDollars = 500
        };
    }

    // MNQ after 09:30: two red candles push down 45 pts, then BH (red → green reference → break).
    static List<double[]> MnqBh()
    {
        return new List<double[]>
        {
            C(20010, 20012, 20000, 20000),   // 09:30 stamp = 09:29-09:30: pre-open
            C(20000, 20001, 19978, 19980),   // 09:31 red (push down)
            C(19980, 19981, 19958, 19960),   // 09:32 red
            C(19960, 19962, 19945, 19950),   // 09:33 red  (BH bearish, low 19945)
            C(19950, 19968, 19948, 19965),   // 09:34 green reference (high 19968)
            C(19965, 19992, 19960, 19990),   // 09:35 breaks 19968 → entry 19968
        };
    }

    public static int Main()
    {
        // 1. MNQ: first BH after the open, push down, +100 target reached → WIN
        {
            var cfg = Cfg("MNQ");
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 30, 0), MnqBh(), 120, 2.0);
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(ev.Count == 1, "MNQ: one trade a day (" + ev.Count + ")");
            var e = ev.FirstOrDefault();
            Check(e != null && e.SetupClass == "BH" && e.Entry == 19968 && e.Target == 20068 && e.Stop == 19945, "MNQ: BH entry 19968, target +100 = 20068, stop below the pattern 19945", e == null ? "" : e.SetupClass + " " + e.Entry + " " + e.Target + " " + e.Stop);
            Check(e != null && e.Outcome == "WIN" && Math.Abs(e.GrossPnl - 100 * 2 * 5) < 0.01, "MNQ: WIN = 100 pts × $2 × 5 contracts = $1,000", e == null ? "" : e.Outcome + " " + e.GrossPnl);
            Check(e != null && e.StrengthTag == "AGGR" && e.FvgDrop >= 50 && e.FvgRedRun == 3, "MNQ: push down measured from the 09:30 price (" + (e == null ? 0 : e.FvgDrop) + " pts, " + (e == null ? 0 : e.FvgRedRun) + " red) = AGGRESSION");
            Check(e != null && e.ReviewNote.StartsWith("GOLDEN • first setup after 09:30 • BH"), "MNQ: note explains the setup", e == null ? "" : e.ReviewNote);
        }
        // 2. A pattern whose candles end by 09:30 is not taken
        {
            var cfg = Cfg("MNQ");
            var c = new List<double[]> { C(19960, 19962, 19945, 19950), C(19950, 19968, 19948, 19965), C(19965, 19992, 19960, 19990) };
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 28, 0), c, 60, 0.0);
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(ev.Count == 0 || ev[0].TriggerTime > new DateTime(2026, 3, 10, 9, 33, 0), "a BH formed before the open is ignored", ev.Count == 0 ? "" : ev[0].TriggerTime.ToString("HH:mm"));
        }
        // 3. MGC from 08:00: a bullish FVG, entry at the close of candle 3, +10 target
        {
            var cfg = Cfg("MGC"); cfg.GoldenUseBh = 0;
            var c = new List<double[]>
            {
                C(2010, 2010.5, 2009.8, 2010),   // 08:00 pre
                C(2010, 2010.2, 2006, 2006.5),   // 08:01 red
                C(2006.5, 2006.8, 2003, 2003.4), // 08:02 red (push down 7)
                C(2003.4, 2004, 2002.8, 2003.9), // 08:03 candle 1 (high 2004)
                C(2003.9, 2007, 2003.8, 2006.8), // 08:04 candle 2
                C(2006.8, 2008, 2004.5, 2007.6), // 08:05 candle 3 (low 2004.5 > 2004) → enter at 2007.6
            };
            var bars = Day("MGC", new DateTime(2026, 3, 10, 8, 0, 0), c, 60, 0.3);
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            var e = ev.FirstOrDefault();
            Check(ev.Count == 1 && e.SetupClass == "FVG" && e.Entry == 2007.6 && Math.Abs(e.Target - 2017.6) < 1e-9 && Math.Abs(e.Stop - 2002.8) < 1e-9, "MGC: FVG entry at candle 3 close 2007.6, target 2017.6, stop 2002.8", e == null ? "none" : e.SetupClass + " " + e.Entry + " " + e.Target + " " + e.Stop);
            Check(e != null && e.Outcome == "WIN" && Math.Abs(e.GrossPnl - 10 * 10 * 10) < 0.01 && e.EntryTime == new DateTime(2026, 3, 10, 8, 5, 0), "MGC: WIN = 10 pts × $10 × 10 contracts = $1,000", e == null ? "" : e.Outcome + " " + e.GrossPnl);
        }
        // 4. REQUIRED aggression: the same BH without a push down is skipped
        {
            var cfg = Cfg("MNQ"); cfg.GoldenAggression = "REQUIRED"; cfg.GoldenMnqDropPoints = 80;
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 30, 0), MnqBh(), 120, 2.0);
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(ev.Count == 0, "REQUIRED aggression: a 55-pt push is not enough when 80 is required (" + ev.Count + ")");
            cfg.GoldenMnqDropPoints = 40;
            Check(KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).Count == 1, "REQUIRED aggression: taken when the push is big enough");
        }
        // 5. A loss, then a second try only when tries = 2
        {
            var cfg = Cfg("MNQ");
            var c = MnqBh();
            c.Add(C(19990, 19991, 19930, 19935)); // 09:36 crashes through the stop 19945 → loss
            c.Add(C(19935, 19936, 19920, 19925)); // 09:37 red
            c.Add(C(19925, 19940, 19923, 19938)); // 09:38 green reference (high 19940)
            c.Add(C(19938, 19950, 19930, 19948)); // 09:39 breaks 19940 → second BH
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 30, 0), c, 120, 2.0);
            var one = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(one.Count == 1 && one[0].Outcome.StartsWith("LOSS"), "tries = 1: only the first setup (a loss)", string.Join(",", one.Select(x => x.Outcome)));
            cfg.GoldenMaxTradesPerDay = 2;
            var two = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(two.Count == 2 && two[1].Entry == 19940 && two[1].Outcome == "WIN" && two[1].SessionOrder == 2, "tries = 2: after the loss the next setup is taken (and wins)", string.Join(",", two.Select(x => x.Entry + " " + x.Outcome)));
        }
        // 6. Fixed stop in points
        {
            var cfg = Cfg("MNQ"); cfg.GoldenStopMode = "FIXED"; cfg.GoldenMnqStopPoints = 40;
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 30, 0), MnqBh(), 120, 2.0);
            var e = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).First();
            Check(e.Stop == 19928, "FIXED stop: entry − 40 pts = 19928 (" + e.Stop + ")");
        }
        // 6b. minimum FVG gap and "skip if the stop is bigger than the target"
        {
            var cfg = Cfg("MGC"); cfg.GoldenUseBh = 0; cfg.GoldenMgcMinGap = 1.0;
            var c = new List<double[]>
            {
                C(2010, 2010.5, 2009.8, 2010), C(2010, 2010.2, 2006, 2006.5), C(2006.5, 2006.8, 2003, 2003.4),
                C(2003.4, 2004, 2002.8, 2003.9), C(2003.9, 2007, 2003.8, 2006.8), C(2006.8, 2008, 2004.5, 2007.6),   // gap 0.5 (2004 → 2004.5)
            };
            var bars = Day("MGC", new DateTime(2026, 3, 10, 8, 0, 0), c, 60, 0.3);
            Check(KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).Count == 0, "minimum gap 1.0: a 0.5-point FVG is ignored");
            cfg.GoldenMgcMinGap = 0.5;
            var e = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).FirstOrDefault();
            Check(e != null && e.GoldenStartTime == new DateTime(2026, 3, 10, 8, 0, 0) && e.GoldenLowPrice == 2002.8 && e.GoldenPatternTime == new DateTime(2026, 3, 10, 8, 3, 0), "chart labels: start 08:00, push-down low 2002.8, pattern candle 1 at 08:03", e == null ? "none" : e.GoldenStartTime.ToString("HH:mm") + " " + e.GoldenLowPrice + " " + e.GoldenPatternTime.ToString("HH:mm"));
            cfg.GoldenSkipStopOverTarget = 1; cfg.GoldenMgcTargetPoints = 3;   // stop 4.8 > target 3
            Check(KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).Count == 0, "a stop (4.8) bigger than the target (3) is skipped");
        }
        // 8. HOLD: a trade still open at the close carries to the next day (target hit the next morning)
        {
            var cfg = Cfg("MNQ"); cfg.End = new DateTime(2026, 3, 12);
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 30, 0), MnqBh(), 120, 0.0);
            bars.AddRange(Day("MNQ", new DateTime(2026, 3, 11, 9, 31, 0), new List<double[]> { C(19990, 19991, 19989, 19990) }, 70, 2.0));
            var held = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).First();
            Check(held.Outcome == "WIN" && held.ExitTime.Date == new DateTime(2026, 3, 11) && KeystoneGoldenStudy.HeldOvernight(held), "HOLD: still open at the close → WIN the next day", held.Outcome + " " + held.ExitTime.ToString("MM-dd HH:mm"));
            cfg.GoldenHold = 0;
            var closed = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).First();
            Check(closed.Outcome == "SESSION EXIT" && closed.ExitTime.Date == new DateTime(2026, 3, 10), "CLOSE AT SESSION END: the same trade is closed on day 1", closed.Outcome + " " + closed.ExitTime.ToString("MM-dd HH:mm"));
        }
        // 9. Strictly the first setup: a filtered first setup means no trade that day
        {
            var cfg = Cfg("MNQ"); cfg.GoldenAggression = "REQUIRED"; cfg.GoldenMnqDropPoints = 80; cfg.GoldenMaxTradesPerDay = 1;
            var c = MnqBh();
            c.Add(C(19990, 19991, 19930, 19935)); c.Add(C(19935, 19936, 19920, 19925)); c.Add(C(19925, 19940, 19923, 19938)); c.Add(C(19938, 19950, 19930, 19948));
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 30, 0), c, 120, 2.0);
            Check(KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).Count == 0, "STRICT FIRST: the first BH has too little push down → no trade that day");
            cfg.GoldenStrictFirst = 0;
            var next = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(next.Count == 1 && next[0].Entry == 19940, "TAKE THE NEXT: the second BH (push down 90) is taken", string.Join(",", next.Select(x => x.Entry)));
        }
        // 10. Evening start (18:00) belongs to the next trading day's session
        {
            var cfg = Cfg("MGC"); cfg.GoldenUseBh = 0; cfg.GoldenMgcStart = 1800; cfg.Start = new DateTime(2026, 3, 9);
            var c = new List<double[]> { C(2010, 2010.5, 2009.8, 2010), C(2010, 2010.2, 2006, 2006.5), C(2006.5, 2006.8, 2003, 2003.4), C(2003.4, 2004, 2002.8, 2003.9), C(2003.9, 2007, 2003.8, 2006.8), C(2006.8, 2008, 2004.5, 2007.6) };
            var bars = Day("MGC", new DateTime(2026, 3, 9, 18, 0, 0), c, 60, 0.3);
            var e = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg).FirstOrDefault();
            Check(e != null && e.EntryTime == new DateTime(2026, 3, 9, 18, 5, 0) && e.Outcome == "WIN" && KeystoneArcEngine.GoldenSessionDay(e.EntryTime) == new DateTime(2026, 3, 10), "EVENING START 18:00: FVG at 18:05, part of Tuesday's session", e == null ? "none" : e.EntryTime.ToString("MM-dd HH:mm") + " " + e.Outcome);
            // 11. run up before each stop level is recorded for the TARGET × STOP grid
            Check(e != null && e.GoldenRunBeforeStop != null && e.GoldenRunBeforeStop.Length == KeystoneGoldenStudy.StopLevels("MGC").Length + 1 && e.GoldenRunBeforeStop[0] >= 10 && !e.GoldenStopHit[0], "EXCURSION: run up ≥ 10 before a 1-point stop", e == null || e.GoldenRunBeforeStop == null ? "none" : e.GoldenRunBeforeStop[0] + " " + e.GoldenStopHit[0]);
        }
        // 12. The study: filters, comparison, grid, one account, export
        {
            var cfg = Cfg("MNQ"); cfg.GoldenMaxTradesPerDay = 2;
            var c = MnqBh();
            c.Add(C(19990, 19991, 19930, 19935)); c.Add(C(19935, 19936, 19920, 19925)); c.Add(C(19925, 19940, 19923, 19938)); c.Add(C(19938, 19950, 19930, 19948));
            var bars = Day("MNQ", new DateTime(2026, 3, 10, 9, 30, 0), c, 120, 2.0);
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            var f = KeystoneGoldenStudy.FromConfig(cfg);
            var r = KeystoneGoldenStudy.Run(ev, f, cfg);
            Check(r.Kept.Count == 2 && r.Mnq.Wins == 1 && r.Mnq.Losses == 1 && r.Both.Entries == 2, "STUDY: 2 entries, 1 W / 1 L", r.Kept.Count + " " + r.Mnq.Wins + "/" + r.Mnq.Losses);
            Check(Math.Abs(r.AccountMnq.End - (f.StartBalance + ev.Sum(x => x.GrossPnl))) < 0.01, "STUDY: one account = start + every trade", r.AccountMnq.End.ToString());
            Check(r.Grids.Count >= 2 && r.Grids[0].Yours != null && r.Grids[0].Yours.Wins + r.Grids[0].Yours.Losses == 2, "STUDY: the target × stop grid has your setting (pattern stop, +100)");
            Check(r.Grids[0].Yours.Wins == r.Mnq.Wins && r.Grids[0].Yours.Losses == r.Mnq.Losses, "STUDY: the grid's pattern-stop cell = the real outcomes", r.Grids[0].Yours.Wins + "/" + r.Grids[0].Yours.Losses);
            f.MaxTries = 1;
            Check(KeystoneGoldenStudy.Run(ev, f, cfg).Kept.Count == 1, "STUDY FILTER: 1 try a day keeps only the first");
            f.MaxTries = 5; f.Setup = "FVG";
            var none = KeystoneGoldenStudy.Run(ev, f, cfg);
            Check(none.Kept.Count == 0 && none.FilteredOut.ContainsKey("SETUP TYPE"), "STUDY FILTER: FVG only → the first BH is filtered and the day is used up");
            Check(KeystoneGoldenStudy.Html(r, KeystoneGoldenStudy.FromConfig(cfg), "test").Contains("GOLDEN ENTRY STUDY") && KeystoneGoldenStudy.Csv(r).Split('\n').Count(l => l.Trim().Length > 0) == ev.Count + 1, "STUDY: HTML report and CSV export");
        }
        // 13. Entry sets: first BH, first FVG, every 5M FVG — compared with the same filters
        {
            var cfg = Cfg("MGC");
            var c = new List<double[]> { C(2010, 2010.5, 2009.8, 2010), C(2010, 2010.2, 2006, 2006.5), C(2006.5, 2006.8, 2003, 2003.4), C(2003.4, 2004, 2002.8, 2003.9), C(2003.9, 2007, 2003.8, 2006.8), C(2006.8, 2008, 2004.5, 2007.6) };
            var bars = Day("MGC", new DateTime(2026, 3, 10, 8, 0, 0), c, 60, 0.3);
            var sets = new Dictionary<string, List<KeystoneArcEvent>>();
            foreach (string u in KeystoneGoldenStudy.Universes) sets[u] = KeystoneArcEngine.DetectAndResolve(bars, bars, KeystoneGoldenStudy.UniverseConfig(cfg, u));
            Check(sets["FIRST_FVG"].Count == 1 && sets["FIRST_FVG"][0].SetupClass == "FVG", "FIRST FVG OF THE DAY: one entry", sets["FIRST_FVG"].Count.ToString());
            Check(sets["EVERY_FVG"].Count >= 2 && sets["EVERY_FVG"].All(e => e.SetupClass == "FVG") && sets["EVERY_FVG"][0].Entry == 2007.6, "EVERY 5M FVG: every FVG of the day is an entry (" + sets["EVERY_FVG"].Count + ")");
            Check(sets["FIRST_BH"].All(e => e.SetupClass == "BH" || e.SetupClass == "DT"), "FIRST BH OF THE DAY: BH entries only");
            var f = KeystoneGoldenStudy.FromConfig(cfg);
            var cmp = KeystoneGoldenStudy.CompareUniverses(sets, f, cfg);
            var every = cmp.First(x => x.Universe == "EVERY_FVG");
            Check(cmp.Count == 4 && every.Kept.Count == sets["EVERY_FVG"].Count(KeystoneGoldenStudy.Traded), "COMPARE: four sets; every FVG kept (no 'day already used')", every.Kept.Count + " of " + sets["EVERY_FVG"].Count);
            f.OnePositionPerInstrument = true;
            Check(every.AccountBoth.SkippedOpen > 0 && every.AccountBoth.Trades < every.Kept.Count, "ONE LIVE ACCOUNT: FVGs while a trade is open are skipped (" + every.AccountBoth.SkippedOpen + ")");
            Check(KeystoneGoldenStudy.Html(every, f, "t", cmp).Contains("WHICH ENTRIES"), "REPORT: the comparison table is in the HTML");
        }
        // 7. Settings are part of the run key
        {
            var a = Cfg("MNQ"); var b = Cfg("MNQ"); b.GoldenMnqTargetPoints = 80;
            Check(a.Snapshot() != b.Snapshot(), "GOLDEN settings change the configuration key");
        }
        Console.WriteLine(failures == 0 ? "ALL GOLDEN TESTS PASSED" : failures + " GOLDEN TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
