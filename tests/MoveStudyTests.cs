// MOVE STUDY: every entry followed to the close (for us / against us / heat before each level), target / stop chosen
// from the measured moves, baseline, ranking. Run: tools/compile_engine.sh tests/.build/move.exe tests/MoveStudyTests.cs && mono tests/.build/move.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class MoveStudyTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.005; }
    static KeystoneArcBar B(string s, DateTime t, double o, double h, double l, double c) { return new KeystoneArcBar { Symbol = s, Time = t, Open = o, High = h, Low = l, Close = c }; }

    public static int Main()
    {
        var day = new DateTime(2025, 6, 3);
        // MGC: entry 2000 at 08:10; −3 at 08:12, +6 at 08:20 (best), −5 at 09:00, close 15:55 at +2
        var bars = new List<KeystoneArcBar>();
        for (int m = 8 * 60 + 1; m <= 16 * 60; m++)
        {
            var t = day.AddMinutes(m); double p = 2000;
            if (m == 8 * 60 + 12) bars.Add(B("MGC", t, p, p + 0.5, p - 3, p));
            else if (m == 8 * 60 + 20) bars.Add(B("MGC", t, p, p + 6, p - 0.5, p));
            else if (m == 9 * 60) bars.Add(B("MGC", t, p, p + 0.5, p - 5, p));
            else if (m == 15 * 60 + 55) bars.Add(B("MGC", t, p, p + 2.5, p, p + 2));
            else bars.Add(B("MGC", t, p, p + 0.2, p - 0.2, p));
        }
        var e = new KeystoneArcEvent { Symbol = "MGC", Direction = "LONG", EntryTime = day.AddHours(8).AddMinutes(10), Entry = 2000, SetupClass = "FVG", StrengthTag = "AGGR" };
        Check(KeystoneMoveStudy.MeasureAll(new List<KeystoneArcEvent> { e }, bars, 1555) == 1, "one entry measured");
        Check(Eq(e.MoveMfe, 6) && Eq(e.MoveMae, 5) && Eq(e.MoveMaeBeforeMfe, 3) && Eq(e.MoveClose, 2), "for us +6, against −5, against before the best −3, close +2", e.MoveMfe + " " + e.MoveMae + " " + e.MoveMaeBeforeMfe + " " + e.MoveClose);
        var lv = KeystoneMoveStudy.Levels("MGC");
        Check(Eq(e.MoveHeat[Array.IndexOf(lv, 5.0)], 3) && double.IsNaN(e.MoveHeat[Array.IndexOf(lv, 8.0)]), "+5 was reached after −3 against; +8 never");
        var row = KeystoneMoveStudy.Row("FIRST_FVG", e);
        var rows = new List<KeystoneMoveRow> { row };
        var b1 = KeystoneMoveStudy.Bracket(rows, "MGC", 5, 4);   // reached +5 with only −3 against → win
        var b2 = KeystoneMoveStudy.Bracket(rows, "MGC", 5, 3);   // −3 came first → loss
        var b3 = KeystoneMoveStudy.Bracket(rows, "MGC", 8, 6);   // never +8, never −6 → closed +2
        Check(Eq(b1.PerTradePts, 5) && Eq(b2.PerTradePts, -3) && Eq(b3.PerTradePts, 2), "target / stop from the measured move: +5/−4 win, +5/−3 loss, +8/−6 closed +2", b1.PerTradePts + " " + b2.PerTradePts + " " + b3.PerTradePts);
        Check(Eq(b1.Dollars, 5 * 10 - KeystoneMoveStudy.Cost("MGC")), "dollars per contract after costs", b1.Dollars.ToString());
        // SHORT measured the other way
        var s = new KeystoneArcEvent { Symbol = "MGC", Direction = "SHORT", EntryTime = day.AddHours(8).AddMinutes(10), Entry = 2000 };
        KeystoneMoveStudy.MeasureAll(new List<KeystoneArcEvent> { s }, bars, 1555);
        Check(Eq(s.MoveMfe, 5) && Eq(s.MoveMae, 6) && Eq(s.MoveClose, -2), "short: for us +5 (the 09:00 drop), against −6, close −2", s.MoveMfe + " " + s.MoveMae + " " + s.MoveClose);
        // baseline: buy at 08:00 every day
        var bl = KeystoneMoveStudy.BaselineRows(bars, "MGC", 800, 1555);
        Check(bl.Count == 1 && Eq(bl[0].Entry, 2000) && Eq(bl[0].Mfe, 6), "baseline: buy the 08:01 open, same measures");
        // two years, setup entries drift up, baseline flat → ranking puts the setup first, every year ✓
        var rng = new Random(5); var sets = new Dictionary<string, List<KeystoneArcEvent>> { { "FIRST_FVG", new List<KeystoneArcEvent>() } };
        var allBars = new List<KeystoneArcBar>();
        for (var d = new DateTime(2024, 1, 2); d < new DateTime(2025, 12, 31); d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
            double p = 2000; var entryT = d.AddHours(8).AddMinutes(30); double entry = 0;
            for (int m = 8 * 60 + 1; m <= 16 * 60; m++) { var t = d.AddMinutes(m); double o = p; p += (rng.NextDouble() - 0.5) * 0.8 + (t > entryT ? 0.03 : 0); allBars.Add(B("MGC", t, o, Math.Max(o, p) + 0.1, Math.Min(o, p) - 0.1, p)); if (t == entryT) entry = p; }
            sets["FIRST_FVG"].Add(new KeystoneArcEvent { Symbol = "MGC", Direction = "LONG", EntryTime = entryT, Entry = entry, SetupClass = rng.NextDouble() < 0.5 ? "FVG" : "BH", StrengthTag = rng.NextDouble() < 0.4 ? "AGGR" : "BASE", FvgGap = rng.NextDouble() * 3, FvgDrop = rng.NextDouble() * 10 });
        }
        KeystoneMoveStudy.MeasureAll(sets["FIRST_FVG"], allBars, 1555);
        var groups = KeystoneMoveStudy.Run(sets, KeystoneMoveStudy.BaselineRows(allBars, "MGC", 800, 1555));
        var top = groups[0];
        Console.WriteLine("      " + KeystoneMoveStudy.Verdict(groups));
        Check(groups.Any(g => g.Set == KeystoneMoveStudy.Baseline) && groups.Any(g => g.Set == "FIRST_FVG+AGGR"), "baseline and aggression-only groups are built");
        Check(top.Set.StartsWith("FIRST_FVG") && top.EveryYear && top.Best.Dollars > 0 && top.Years.Count == 2 && top.BestByYear.Count == 2, "the drifting setup ranks first, positive in each year", top.Name + " " + top.Best.Dollars);
        Check(top.All.UpPct > 50 && top.All.MfeP50 > top.All.MaeP50, "ended up more often; for us > against", top.All.UpPct + " " + top.All.MfeP50 + " " + top.All.MaeP50);
        var why = KeystoneMoveStudy.Why(top);
        Check(why.Any(w => w.Factor.StartsWith("AGGRESSION")) && why.Any(w => w.Factor == "WEEKDAY") && why.Any(w => w.Factor.StartsWith("FVG GAP")), "why: setup, aggression, hour, weekday, gap, push down, year");
        string html = KeystoneMoveStudy.Html(groups, "test"), csv = KeystoneMoveStudy.Csv(groups);
        Check(html.Contains("RANKING") && html.Contains("HOW FAR DID IT GO") && html.Contains("WHY") && csv.Split('\n').Length > 500, "report + every-entry CSV");
        Console.WriteLine(failures == 0 ? "ALL MOVE STUDY TESTS PASSED" : failures + " MOVE STUDY TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
