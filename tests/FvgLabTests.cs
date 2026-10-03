// 5M FVG MATH LAB: first 5-minute FVG entries × target × stop × contracts, MNQ and MGC separately, prop + walk-forward.
// Run: tools/compile_engine.sh tests/.build/flab.exe tests/FvgLabTests.cs && mono tests/.build/flab.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class FvgLabTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.01; }
    static List<KeystoneArcBar> Path(string sym, DateTime t0, double[][] ohlc) { var l = new List<KeystoneArcBar>(); for (int i = 0; i < ohlc.Length; i++) l.Add(new KeystoneArcBar { Symbol = sym, Time = t0.AddMinutes(i), Open = ohlc[i][0], High = ohlc[i][1], Low = ohlc[i][2], Close = ohlc[i][3] }); return l; }

    public static int Main()
    {
        var t0 = new DateTime(2025, 3, 3, 10, 0, 0); var close = new DateTime(2025, 3, 3, 15, 55, 0);
        // target: entry 20000, +100 at minute 3
        var a = Path("MNQ", t0, new[] { new[] { 20000.0, 20150, 19990, 20010 }, new[] { 20010.0, 20050, 19980, 20040 }, new[] { 20040.0, 20080, 20020, 20070 }, new[] { 20070.0, 20105, 20060, 20100 } });
        var ta = KeystoneFvgLab.Trade(a, 0, 20000, 100, 50, close, "MNQ");
        Check(ta.Outcome == "TARGET" && Eq(ta.Exit, 20100) && ta.ExitTime == t0.AddMinutes(3) && Eq(ta.Net, 200 - KeystoneMoveStudy.Cost("MNQ")), "target +100 MNQ = +$200 less costs; the fill minute's high (20150) does not count", ta.Outcome + " " + ta.Exit + " " + ta.ExitTime);
        var b = Path("MNQ", t0, new[] { new[] { 20000.0, 20010, 19990, 20000 }, new[] { 20000.0, 20120, 19940, 20000 } });
        var tb = KeystoneFvgLab.Trade(b, 0, 20000, 100, 50, close, "MNQ");
        Check(tb.Outcome == "STOP" && Eq(tb.Exit, 19950), "a minute touching both the target and the stop counts as the stop", tb.Outcome + " " + tb.Exit);
        var c = Path("MGC", t0, new[] { new[] { 3000.0, 3002, 2999, 3001 }, new[] { 2990.0, 2991, 2985, 2988 } });
        var tc = KeystoneFvgLab.Trade(c, 0, 3000, 10, 5, close, "MGC");
        Check(tc.Outcome == "STOP" && Eq(tc.Exit, 2990) && Eq(tc.Net, -100 - KeystoneMoveStudy.Cost("MGC")), "gold gap through the stop fills at the open (2990, −$100), never better than reality", tc.Outcome + " " + tc.Exit);
        var d = Path("MGC", close.AddMinutes(-2), new[] { new[] { 3000.0, 3004, 2998, 3003 }, new[] { 3003.0, 3006, 3001, 3005 }, new[] { 3005.0, 3009, 3004, 3008 }, new[] { 3008.0, 3020, 3007, 3019 } });
        var td = KeystoneFvgLab.Trade(d, 0, 3000, 10, 5, close, "MGC");
        Check(td.Outcome == "CLOSE" && Eq(td.Exit, 3008) && td.ExitTime == close, "no target / stop by 15:55 → out at the 15:55 close (later bars ignored)", td.Outcome + " " + td.Exit + " " + td.ExitTime);

        // two years of random 1-minute bars 07:00 → 16:00 plus the evening before (prior FVGs)
        var rng = new Random(21); var bars = new List<KeystoneArcBar>(); double pn = 20000, pg = 2000;
        for (var day = new DateTime(2024, 1, 2); day < new DateTime(2025, 12, 20); day = day.AddDays(1))
        {
            if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
            for (var t = day.AddHours(-6).AddMinutes(1); t <= day.AddHours(16); t = t.AddMinutes(1))
            {
                if (t.Hour == 17 && t.Date < day) continue;
                double o1 = pn, o2 = pg; pn += (rng.NextDouble() - 0.5) * 9; pg += (rng.NextDouble() - 0.5) * 1.1;
                bars.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = o1, High = Math.Max(o1, pn) + rng.NextDouble() * 3, Low = Math.Min(o1, pn) - rng.NextDouble() * 3, Close = pn });
                bars.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = o2, High = Math.Max(o2, pg) + rng.NextDouble() * 0.4, Low = Math.Min(o2, pg) - rng.NextDouble() * 0.4, Close = pg });
            }
        }
        var g = new KeystoneFvgLabGrid(); var rules = new KeystonePropRules();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = KeystoneFvgLab.Run(bars, g, rules, null, null);
        Console.WriteLine("      " + res.Rows.Count + " rows in " + sw.ElapsedMilliseconds + " ms • entries: " + string.Join(", ", res.Entries.Select(kv => kv.Key + " " + kv.Value)));
        Check(res.Rows.Count > 500 && res.Rows.All(r => r.Trades.Count > 0), "rows: entry style × MNQ/MGC × 5 targets × 4 stops × 3 contract sizes", res.Rows.Count.ToString());
        var study = KeystoneFvgEntryStudy.Run(bars.OrderBy(x => x.Time).ToList(), g.Study);
        var r1 = res.Rows.First(r => r.Get("INSTRUMENT") == "MGC" && r.Get("ENTRY") == "50% DIP" && r.Get("TARGET") == "TP 10" && r.Get("STOP") == "SL 5" && r.Get("CONTRACTS") == "1x");
        var r3 = res.Rows.First(r => r.Get("INSTRUMENT") == "MGC" && r.Get("ENTRY") == "50% DIP" && r.Get("TARGET") == "TP 10" && r.Get("STOP") == "SL 5" && r.Get("CONTRACTS") == "3x");
        Check(r1.Trades.Count == study[KeystoneFvgEntryStudy.Dip50].Count(e => e.Symbol == "MGC"), "every FIRST 5M FVG 50% entry of gold is traded once", r1.Trades.Count + " vs " + study[KeystoneFvgEntryStudy.Dip50].Count(e => e.Symbol == "MGC"));
        Check(Eq(r3.P.Net, 3 * r1.P.Net) && r1.Wins + r1.Losses + r1.Closes == r1.Trades.Count, "3 contracts = 3 × one contract (costs too); every trade ends at target, stop or close");
        Check(r1.Trades.All(t => t.Outcome != "TARGET" || Eq(t.Points, 10)) && r1.Trades.All(t => t.Outcome != "STOP" || t.Points <= -5 + 1e-9), "targets pay exactly +10 gold points, stops lose at least 5");
        var hist = KeystonePropPlanner.History(rules, new KeystonePropPlan { Source = "REAL" }, r1.P.Days);
        Check(Eq(hist.Sum(y => y.Net), r1.P.PropNet), "prop result = the shared prop history");
        Check(Eq(r1.InSampleNet + r1.OutSampleNet, r1.P.Net), "early 70% + unseen 30% = whole range");
        var w = KeystoneLab.WalkForward(res.Rows, rules, 1, x => x.Get("INSTRUMENT") == "MNQ");
        Console.WriteLine("      " + KeystoneLab.WalkVerdict(w));
        Check(w.Years.Count == 1 && w.Years[0].Year == 2025 && w.Years.All(y => y.Chosen.Get("INSTRUMENT") == "MNQ"), "walk-forward per instrument: 2025 chosen from 2024 MNQ rows only");
        var adv = KeystoneFvgLab.Advice(res); foreach (var l in adv.Take(6)) Console.WriteLine("      " + l);
        Check(adv.Count >= 9 && adv.Any(l => l.StartsWith("MNQ • YOUR TARGET")) && adv.Any(l => l.StartsWith("MGC • YOUR TARGET")), "advice per instrument including your targets (MNQ 100, gold 10)");
        var imp = KeystoneLab.Impacts(res.Rows.Where(r => r.Get("INSTRUMENT") == "MGC").ToList());
        Check(imp.Select(x => x.Setting).Distinct().Count() == 4, "which ENTRY / TARGET / STOP / CONTRACTS value wins", string.Join(",", imp.Select(x => x.Setting).Distinct()));
        // GOLDEN → PROP: an overnight winner becomes a 15:55 close when the account must be flat
        var gEv = new KeystoneArcEvent { Symbol = "MNQ", Direction = "LONG", SetupClass = "FVG", EntryTime = new DateTime(2025, 3, 3, 10, 0, 0), TriggerTime = new DateTime(2025, 3, 3, 10, 0, 0), Entry = 20000, ExitPrice = 20100, ExitTime = new DateTime(2025, 3, 4, 3, 0, 0), Outcome = "WIN", PeakAfterEntry = 20100, TroughAfterEntry = 19970, Quantity = 1 };
        var asIs = KeystoneGoldenLab.Unit(gEv, false, 1555, (sym, tt) => 19990);
        var flatT = KeystoneGoldenLab.Unit(gEv, true, 1555, (sym, tt) => tt == new DateTime(2025, 3, 3, 15, 55, 0) ? 19990 : (double?)null);
        Check(asIs != null && asIs.Outcome == "TARGET" && Eq(asIs.Points, 100) && asIs.Day == new DateTime(2025, 3, 4), "golden as tested: the overnight target counts on its exit day (+100)");
        Check(flatT != null && flatT.Outcome == "CLOSE" && Eq(flatT.Points, -10) && flatT.ExitTime == new DateTime(2025, 3, 3, 15, 55, 0) && Eq(flatT.Worst, -60), "golden flat by 15:55: the same trade is closed at the 15:55 close (−10 pts), worst point kept (−30 pts = −$60)", flatT == null ? "null" : flatT.Outcome + " " + flatT.Points + " " + flatT.Worst);
        // FVG MAP: every bullish 5M gap on the chart with its reason
        var mb = new List<KeystoneArcBar>(); var d0 = new DateTime(2025, 3, 3, 9, 0, 0); double[][] ohlc = {
            new[] { 100.0, 102, 99, 101 }, new[] { 101.0, 104, 100, 103 }, new[] { 103.0, 110, 108, 109 },   // 09:05-09:15: gap 102→108 before 09:30
            new[] { 109.0, 110, 107, 108 }, new[] { 108.0, 109, 106, 107 }, new[] { 107.0, 108, 105, 106 },
            new[] { 106.0, 107, 104, 105 }, new[] { 105.0, 112, 104, 111 }, new[] { 111.0, 120, 110, 119 },   // 09:35 c1 (H107) … 09:45 c3 (L110): gap 3 (< 5)
            new[] { 119.0, 121, 118, 120 }, new[] { 120.0, 130, 128, 129 } };                               // 09:50 c1 H121 → 10:00 c3 L128: gap 7
        for (int i = 0; i < ohlc.Length; i++) mb.Add(new KeystoneArcBar { Symbol = "MNQ", Time = d0.AddMinutes(5 * (i + 1)), Open = ohlc[i][0], High = ohlc[i][1], Low = ohlc[i][2], Close = ohlc[i][3] });
        var mapCfg = new KeystoneArcRunConfig { GoldenMnqStart = 930, GoldenMnqMinGap = 5, GoldenLastEntry = 1555, GoldenFvgMode = "BREAK", GoldenMaxTradesPerDay = 1 };
        var map = KeystoneGoldenFvgMap.Scan(mb, "MNQ", mapCfg, new List<KeystoneArcEvent>());
        Console.WriteLine("      FVG map: " + string.Join(" | ", map.Select(z => mb[z.C3].Time.ToString("HH:mm") + " " + z.Low + "-" + z.High + " " + z.Status)));
        Check(map.Any(z => z.Status.StartsWith("STARTED BEFORE")) && map.Any(z => z.Status.StartsWith("GAP 3 < MIN 5")) && map.Any(z => z.Status.StartsWith("NO RETEST")), "FVG map labels every gap: before the start, too small, or no retest/break");
        var takenEv = new KeystoneArcEvent { Symbol = "MNQ", SetupClass = "FVG", EntryTime = d0.AddMinutes(70), FvgFormedTime = d0.AddMinutes(55), RiskModel = "GLD FVG" };
        var map2 = KeystoneGoldenFvgMap.Scan(mb, "MNQ", mapCfg, new List<KeystoneArcEvent> { takenEv });
        Check(map2.Count(z => z.Taken) == 1 && map2.First(z => z.Taken).Formed == d0.AddMinutes(55), "the traded gap is marked TAKEN");
        // GOLDEN PROP GRID: one entry at 10:00, target reached only the next morning → HOLD = TARGET, SAME DAY = out at 15:55
        var gr = new List<KeystoneArcBar>(); var g0 = new DateTime(2025, 3, 3, 10, 0, 0);
        for (int i = 0; i < 1500; i++) { var tt = g0.AddMinutes(i); double px = i < 1200 ? 20000 + (i % 7) : 20000 + (i - 1200) * 0.5; gr.Add(new KeystoneArcBar { Symbol = "MNQ", Time = tt, Open = px, High = px + 1, Low = px - 1, Close = px }); }
        var gs = new KeystoneGoldenStudyResult(); gs.Kept.Add(new KeystoneArcEvent { Symbol = "MNQ", Direction = "LONG", SetupClass = "FVG", EntryTime = g0, TriggerTime = g0, Entry = 20000, Stop = 19950, Outcome = "WIN" });
        DateTime gf, gl, gsp; int gd;
        var grows = KeystoneGoldenLab.Rows(new Dictionary<string, List<KeystoneArcEvent>> { { "YOUR STUDY", gs.Kept } }, new KeystonePropRules(), new KeystoneGoldenLab.Grid { MnqTargets = new List<double> { 100 }, MnqStops = new List<double> { 0 }, Contracts = new List<int> { 1 } }, new Dictionary<string, List<KeystoneArcBar>> { { "MNQ", gr } }, null, null, out gf, out gl, out gsp, out gd);
        var same = grows.First(x => x.Get("EXIT") == "SAME DAY").Trades[0]; var hold = grows.First(x => x.Get("EXIT") == "HOLD").Trades[0];
        Check(same.Outcome == "CLOSE" && same.ExitTime == new DateTime(2025, 3, 3, 15, 55, 0) && hold.Outcome == "TARGET" && hold.ExitTime.Date == new DateTime(2025, 3, 4) && grows.All(x => x.Get("STOP") == "SL PATTERN"), "golden grid: SAME DAY closes at 15:55, HOLD reaches the target the next day; PATTERN = the setup's own stop", same.Outcome + " " + same.ExitTime + " / " + hold.Outcome + " " + hold.ExitTime);
        // TRADES A DAY: 10:00 entry loses, 10:30 entry wins, 11:00 entry → MAX 1 takes only the first; MAX 3 takes two (a win ends the day)
        var pr = new List<KeystoneArcBar>(); var p0 = new DateTime(2025, 3, 5, 9, 31, 0);
        for (int i = 0; i < 400; i++) { var tt = p0.AddMinutes(i); double px = tt < new DateTime(2025, 3, 5, 10, 20, 0) ? 20000 - (i * 2) : 19902 + (tt - new DateTime(2025, 3, 5, 10, 20, 0)).TotalMinutes * 3; pr.Add(new KeystoneArcBar { Symbol = "MNQ", Time = tt, Open = px, High = px + 1, Low = px - 1, Close = px }); }
        Func<int, double, KeystoneArcEvent> ev = (min, px) => new KeystoneArcEvent { Symbol = "MNQ", Direction = "LONG", SetupClass = "FVG", EntryTime = p0.AddMinutes(min), TriggerTime = p0.AddMinutes(min), Entry = px, Stop = px - 20 };
        var day3 = new List<KeystoneArcEvent> { ev(29, pr[29].Close), ev(59, pr[59].Close), ev(89, pr[89].Close) };
        DateTime pf, pl, psp; int pdd;
        var prow = KeystoneGoldenLab.Rows(new Dictionary<string, List<KeystoneArcEvent>> { { "FVG", day3 } }, new KeystonePropRules(), new KeystoneGoldenLab.Grid { Exits = new List<string> { "SAME DAY" }, MnqTargets = new List<double> { 40 }, MnqStops = new List<double> { 0 }, Contracts = new List<int> { 1 }, PerDay = new List<int> { 1, 3 } }, new Dictionary<string, List<KeystoneArcBar>> { { "MNQ", pr } }, null, null, out pf, out pl, out psp, out pdd);
        var one = prow.First(x => x.Get("TRADES A DAY") == "MAX 1/DAY"); var three = prow.First(x => x.Get("TRADES A DAY") == "MAX 3/DAY");
        Check(one.Trades.Count == 1 && one.Trades[0].Outcome == "STOP" && three.Trades.Count == 2 && three.Trades[1].Outcome == "TARGET", "TRADES A DAY: max 1 = the first (a loss); max 3 = the loss, then the next setup wins and ends the day", one.Trades.Count + " / " + three.Trades.Count + " " + string.Join(",", three.Trades.Select(t => t.Outcome)));
        Console.WriteLine(failures == 0 ? "ALL FVG LAB TESTS PASSED" : failures + " FVG LAB TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
