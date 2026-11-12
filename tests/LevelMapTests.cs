// 🗺 NEXT LEVELS + ➕ ADD suggestions (BUILD 2026-11-13d) — the level finder, the reach statistics (no lookahead, the n gate), the favour / against words, both add rules.
// Run: tools/compile_engine.sh tests/.build/lvl.exe tests/LevelMapTests.cs && mono tests/.build/lvl.exe
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class LevelMapTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static void Flat(List<KeystoneArcBar> l, DateTime firstEnd, int n, double lo, double hi, long vol = 100) { for (int i = 0; i < n; i++) l.Add(new KeystoneArcBar { Symbol = "MNQ", Time = firstEnd.AddMinutes(i), Open = (lo + hi) / 2, High = hi, Low = lo, Close = (lo + hi) / 2, Volume = vol }); }
    static KeystoneArcBar B(DateTime t, double o, double h, double l, double c, long v = 100) { return new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = o, High = h, Low = l, Close = c, Volume = v }; }
    static KeystoneLevel Find(List<KeystoneLevel> s, string type, string tf = "") { return s.FirstOrDefault(x => x.Type == type && x.Tf == tf); }

    public static int Main()
    {
        var IC = CultureInfo.InvariantCulture;
        // ---- 1. prior day (RTH high / low / close), overnight, today, VWAP, round numbers, a 5-minute FVG and how it fills
        {
            var st = new KeystoneLevelState("MNQ"); var bars = new List<KeystoneArcBar>();
            var d1 = new DateTime(2026, 3, 9, 18, 1, 0);                    // session 2026-03-10
            Flat(bars, d1, 60, 19998, 20002);                                // the evening
            bars.Add(B(new DateTime(2026, 3, 10, 10, 0, 0), 20000, 20050, 19999, 20020));
            bars.Add(B(new DateTime(2026, 3, 10, 11, 0, 0), 20020, 20021, 19950, 19990));
            bars.Add(B(new DateTime(2026, 3, 10, 16, 0, 0), 19990, 20012, 19988, 20010));
            bars.Add(B(new DateTime(2026, 3, 10, 16, 30, 0), 20010, 20070, 20000, 20005));  // after the RTH close: not the prior day's high
            var d2 = new DateTime(2026, 3, 10, 18, 1, 0);                    // session 2026-03-11
            Flat(bars, d2, 30, 19995, 20005); bars.Add(B(new DateTime(2026, 3, 11, 3, 0, 0), 20000, 20030, 19990, 20000));
            Flat(bars, new DateTime(2026, 3, 11, 9, 31, 0), 5, 20000, 20004);   // 5m c1 (9:35)
            Flat(bars, new DateTime(2026, 3, 11, 9, 36, 0), 5, 20004, 20030);   // c2 (9:40)
            Flat(bars, new DateTime(2026, 3, 11, 9, 41, 0), 5, 20012, 20035);   // c3 (9:45) → a bullish 5m gap 20004–20012
            foreach (var b in bars) st.Feed(b);
            var s = st.Snapshot(20030);
            Check(Math.Abs(st.Pdh - 20050) < 1e-9 && Math.Abs(st.Pdl - 19950) < 1e-9 && Math.Abs(st.Pdc - 20010) < 1e-9, "prior day = the RTH high / low / close (the 16:30 spike excluded)", st.Pdh + " / " + st.Pdl + " / " + st.Pdc);
            var onh = Find(s, "ONH"); var onl = Find(s, "ONL");
            Check(onh != null && Math.Abs(onh.Price - 20030) < 1e-9 && onl != null && Math.Abs(onl.Price - 19990) < 1e-9, "overnight high / low (after 9:30)");
            var g5 = Find(s, "FVG", "5");
            Check(g5 != null && Math.Abs(g5.Lo - 20004) < 1e-9 && Math.Abs(g5.Hi - 20012) < 1e-9 && Math.Abs(g5.Price - 20012) < 1e-9 && Math.Abs(g5.Mid - 20008) < 1e-9 && g5.Dir == 1, "5m FVG found with its edges + 50 % (near edge = the top below the price)", g5 == null ? "none" : g5.Lo + "–" + g5.Hi + " @" + g5.Price);
            Check(Find(s, "HOD") != null && Math.Abs(Find(s, "HOD").Price - 20035) < 1e-9, "today's high");
            Check(Find(s, "VWAP") != null && Find(s, "VWAPU") != null, "VWAP + its 1σ bands");
            Check(s.Any(x => x.Type == "ROUND" && Math.Abs(x.Price - 20100) < 1e-9) && s.Any(x => x.Type == "ROUND" && Math.Abs(x.Price - 20000) < 1e-9), "round numbers every 100 pts");
            st.Feed(B(new DateTime(2026, 3, 11, 9, 46, 0), 20020, 20022, 20009, 20015));
            var g5b = Find(st.Snapshot(20015), "FVG", "5");
            Check(g5b != null && Math.Abs(g5b.Hi - 20009) < 1e-9 && g5b.Touched && Math.Abs(g5b.Mid - 20008) < 1e-9, "a partly filled FVG keeps its unfilled part (top 20009), still shows its 50 %");
            st.Feed(B(new DateTime(2026, 3, 11, 9, 47, 0), 20015, 20016, 20003, 20006));
            Check(Find(st.Snapshot(20006), "FVG", "5") == null, "a fully traded-through FVG is gone");
        }
        // ---- 2. a 30-minute FVG, kept into the next session as "yesterday's"; a 5m swing high (fractal) taken out
        {
            var st = new KeystoneLevelState("MNQ"); var bars = new List<KeystoneArcBar>();
            Flat(bars, new DateTime(2026, 3, 10, 10, 1, 0), 30, 20000, 20010); Flat(bars, new DateTime(2026, 3, 10, 10, 31, 0), 30, 20010, 20060); Flat(bars, new DateTime(2026, 3, 10, 11, 1, 0), 30, 20030, 20070);
            foreach (var b in bars) st.Feed(b);
            var g30 = Find(st.Snapshot(20050), "FVG", "30");
            Check(g30 != null && Math.Abs(g30.Lo - 20010) < 1e-9 && Math.Abs(g30.Hi - 20030) < 1e-9, "30m FVG 20010–20030", g30 == null ? "none" : g30.Lo + "–" + g30.Hi);
            Flat(bars, new DateTime(2026, 3, 10, 18, 1, 0), 20, 20040, 20050); foreach (var b in bars.Skip(90)) st.Feed(b);
            var g30y = Find(st.Snapshot(20045), "FVG", "30");
            Check(g30y != null && g30y.Name.StartsWith("yesterday's"), "the previous session's unfilled 30m FVG stays (\"yesterday's 30m FVG\")", g30y == null ? "none" : g30y.Name);
            var sw = new KeystoneLevelState("MNQ"); var sb = new List<KeystoneArcBar>(); var t0 = new DateTime(2026, 3, 10, 10, 1, 0); double[] his = { 10, 12, 15, 12, 10 };
            for (int k = 0; k < 5; k++) Flat(sb, t0.AddMinutes(5 * k), 5, 20000 + his[k] - 5, 20000 + his[k]);
            foreach (var b in sb) sw.Feed(b);
            var swh = Find(sw.Snapshot(20005), "SWH", "5");
            Check(swh != null && Math.Abs(swh.Price - 20015) < 1e-9, "5m fractal swing high 20015");
            sw.Feed(B(t0.AddMinutes(25), 20010, 20016, 20009, 20012));
            Check(Find(sw.Snapshot(20012), "SWH", "5") == null, "a swing high taken out is gone");
        }
        // ---- 3. POC / value area
        {
            var p = new Dictionary<long, double> { { 100, 10 }, { 101, 50 }, { 102, 100 }, { 103, 40 }, { 104, 5 } };
            var va = KeystoneLevelState.ValueArea(p, 1);
            Check(Math.Abs(va.Item1 - 102.5) < 1e-9 && Math.Abs(va.Item2 - 103) < 1e-9 && Math.Abs(va.Item3 - 101) < 1e-9, "POC = the busiest row, VA = 70 % around it", va.Item1 + " " + va.Item2 + " " + va.Item3);
        }
        // ---- 4. Side: nearest first, levels within the merge distance = ONE level named by the strongest
        {
            var ls = new List<KeystoneLevel> { new KeystoneLevel { Type = "VWAP", Name = "VWAP", Price = 20050, Strength = 6 }, new KeystoneLevel { Type = "FVG", Tf = "30", Name = "30m FVG", Price = 20050.5, Strength = 8.5 }, new KeystoneLevel { Type = "PDH", Name = "PDH", Price = 20100, Strength = 8 }, new KeystoneLevel { Type = "VAL", Name = "VAL", Price = 19980, Strength = 6.5 } };
            var up = KeystoneLevelMap.Side(ls, 20000, 1, 3, 0.25, 1);
            Check(up.Count == 2 && up[0].Name == "30m FVG" && up[0].Also == "VWAP" && up[1].Name == "PDH", "above: the 30m FVG + VWAP merged, then PDH", string.Join(" | ", up.Select(x => x.Label)));
            var dn = KeystoneLevelMap.Side(ls, 20000, -1, 3, 0.25, 1); Check(dn.Count == 1 && dn[0].Name == "VAL", "below: VAL");
            KeystoneLevelMap.MarkDefended(dn, new[] { Tuple.Create(new DateTime(2026, 3, 10, 10, 42, 0), 19981.0, 1, "ABSORPTION"), Tuple.Create(new DateTime(2026, 3, 10, 10, 50, 0), 19980.0, -1, "BIG PRINT") }, 0.25);
            Check(dn[0].DefendedText == "buyers defended it 10:42", "order flow only adds WORDS: a defend event at the level = \"buyers defended it 10:42\" (a push event ignored)", dn[0].DefendedText);
        }
        // ---- 5. the reach statistics: no lookahead (a later session never changes an earlier count), the n gate, the hold edge, the CSV round trip
        {
            var rnd = new Random(7); var all = new List<KeystoneArcBar>(); double px = 20000;
            for (int d = 0; d < 8; d++)
            {
                var s0 = new DateTime(2026, 3, 2, 18, 1, 0).AddDays(d); if (s0.DayOfWeek == DayOfWeek.Friday || s0.DayOfWeek == DayOfWeek.Saturday) continue;
                for (int m = 0; m < 1380; m++) { double o = px; px += (rnd.NextDouble() - 0.5) * 6; px = Math.Round(px * 4) / 4; double h = Math.Max(o, px) + rnd.Next(0, 4) * 0.25, l = Math.Min(o, px) - rnd.Next(0, 4) * 0.25; all.Add(B(s0.AddMinutes(m), o, h, l, px, 50 + rnd.Next(100))); }
            }
            var sess = all.Select(b => KeystoneStudioStore.SessionOf(b.Time)).Distinct().OrderBy(x => x).ToList(); var cutS = sess[sess.Count - 2];
            var tFull = KeystoneLevelReach.Study(new[] { all }, "MNQ", cutS, sess[2]);
            var tCut = KeystoneLevelReach.Study(new[] { all.Where(b => KeystoneStudioStore.SessionOf(b.Time) < cutS).ToList() }, "MNQ", cutS, sess[2]);
            var mutated = all.Select(b => KeystoneStudioStore.SessionOf(b.Time) >= cutS ? B(b.Time, b.Open + 500, b.High + 500, b.Low + 500, b.Close + 500, b.Volume) : b).ToList();
            var tMut = KeystoneLevelReach.Study(new[] { mutated }, "MNQ", cutS, sess[2]);
            Func<KeystoneReachTable, string> body = tb => string.Join("\n", tb.ToCsv().Split('\n').Skip(1));
            Check(tFull.Samples > 50 && body(tFull) == body(tCut) && body(tFull) == body(tMut), "no lookahead: the sessions from the cut on (missing or changed) never change the earlier counts", tFull.Samples + " samples");
            Check(tFull.Through < cutS && tFull.Cells.ContainsKey("R:ANY|2"), "only sessions before the cut are measured");
            var rt = KeystoneReachTable.Parse(tFull.ToCsv().Split('\n'));
            Check(body(rt) == body(tFull) && rt.Samples == tFull.Samples && rt.Through == tFull.Through, "the CSV round trip (Studio\\Levels\\reach-<SYM>.csv)");
            var t = new KeystoneReachTable(); var lv = new KeystoneLevel { Type = "FVG", Tf = "30", Name = "30m FVG", Price = 20040 };
            for (int i = 0; i < 49; i++) t.Add("R:FVG|30|2", i % 3 > 0, 0.5, true);
            Check(t.FirstText(lv, 40, 40).StartsWith("learning (n = 49)"), "n < 50 → learning", t.FirstText(lv, 40, 40));
            t.Add("R:FVG|30|2", true, 0.5, false); var ft = t.FirstText(lv, 40, 40);
            Check(ft.StartsWith("reached first 66 % (n = 50") && ft.Contains("distances alone ≈ 50 %"), "n ≥ 50 → \"reached first 66 % (n = 50; distances alone ≈ 50 %)\"", ft);
            for (int i = 0; i < 100; i++) t.Add("H:FVG|30", i < 62, 0.5, true); var hd = t.Hold("FVG|30");
            Check(hd.Item4 && hd.Item3.Contains("= an edge"), "a hold rate 62 % of 100 (Wilson low > 50 %) = an edge", hd.Item3);
            for (int i = 0; i < 100; i++) t.Add("H:PDH", i < 52, 0.5, true); Check(!t.Hold("PDH").Item4, "52 % of 100 = no edge");
            Check(!t.Hold("VAH").Item4 && t.Hold("VAH").Item3.StartsWith("learning"), "no history = learning, no edge");
        }
        // ---- 6. the words, told from the position's side
        {
            var above = new List<KeystoneLevel> { new KeystoneLevel { Type = "PDH", Name = "PDH", Price = 20050 } }; var below = new List<KeystoneLevel> { new KeystoneLevel { Type = "VWAP", Name = "VWAP", Price = 19980 } };
            var lg = KeystoneLevelMap.InTrade(1, 2, 20000, 20010, 19970, 20080, double.NaN, above, below, 2, null);
            Check(lg.Contains("in your favour: PDH 20,050 (+$200 for 2) → your TP 20,080 (+$320)") && lg.Contains("against you: VWAP 19,980 (−$80 for 2) → your stop 19,970 (−$120)"), "LONG: favour = above → your TP, against = below → your stop", lg);
            var sh = KeystoneLevelMap.InTrade(-1, 1, 20020, 20010, 20060, 19950, 20040, above, below, 2, null);
            Check(sh.Contains("in your favour: VWAP 19,980 (+$80 for 1) → your TP 19,950 (+$140)") && sh.Contains("against you: the floor ≈ 20,040") && !sh.Contains("PDH"), "SHORT: favour = below, against = above — the floor before PDH / the stop cuts the list", sh);
            var ns = KeystoneLevelMap.InTrade(-1, 1, 20020, 20010, 0, 0, double.NaN, above, below, 2, null);
            Check(ns.Contains("NO STOP SET"), "no stop → said");
            var fl = KeystoneLevelMap.Flat(above, below, 20010, l => l.Type == "VWAP" ? "reached first 61 % (n = 1,240)" : "");
            Check(fl.Contains("▲ above: PDH 20,050 (+40 pts)") && fl.Contains("▼ below: VWAP 19,980 (−30 pts) • reached first 61 % (n = 1,240)"), "FLAT: above / below with pts + the measured %", fl);
            Check(KeystoneLevelMap.Strong(new KeystoneLevel { Type = "FVG", Tf = "30" }, -1) && !KeystoneLevelMap.Strong(new KeystoneLevel { Type = "FVG", Tf = "5" }, -1) && KeystoneLevelMap.Strong(new KeystoneLevel { Type = "PDH" }, -1) && !KeystoneLevelMap.Strong(new KeystoneLevel { Type = "PDH" }, 1), "strong levels: higher-TF FVG, PDH for a short (not for a long)");
        }
        // ---- 7. ➕ ADD TO THE WINNER
        {
            var held = new KeystoneLevel { Type = "VWAP", Name = "VWAP", Price = 20020 };
            var w = KeystoneAddAdvice.Winner(1, 1, 20000, 20030, 19980, held, 20019, 20031, 20025, false, 60, 10, double.NaN, 2, 0.25, 4);
            Check(w.Ok && w.Add == 3 && w.Total == 4 && Math.Abs(w.Stop - 20016) < 1e-9 && Math.Abs(w.NewAvg - 20022.5) < 1e-9 && w.RiskUsd <= 60 && w.Text.Contains("a suggestion — you decide"), "winner: +3 (the risk setting $60 binds), stop for ALL beyond the level 20016, average 20022.5", w.Text);
            var wMax = KeystoneAddAdvice.Winner(1, 1, 20000, 20030, 19980, held, 20019, 20031, 20025, false, 60, 3, double.NaN, 2, 0.25, 4);
            Check(wMax.Ok && wMax.Add == 2 && wMax.Text.Contains("max contracts"), "winner: max contracts 3 → +2", wMax.Text);
            var wFl = KeystoneAddAdvice.Winner(1, 1, 20000, 20030, 19980, held, 20019, 20031, 20025, false, 600, 10, 100, 2, 0.25, 4);
            Check(wFl.Ok && wFl.Add == 1 && wFl.Text.Contains("floor"), "winner: the floor (room $100 ≥ 1.5 × the loss at the stop) → +1", wFl.Text);
            Check(!KeystoneAddAdvice.Winner(1, 1, 20000, 19995, 19980, held, 19990, 20000, 19995, false, 60, 10, double.NaN, 2, 0.25, 4).Ok, "winner: not in profit → no");
            Check(!KeystoneAddAdvice.Winner(1, 1, 20000, 20030, 19980, null, 20019, 20031, 20025, false, 60, 10, double.NaN, 2, 0.25, 4).Ok, "winner: no held pullback → no");
            Check(!KeystoneAddAdvice.Winner(1, 1, 20000, 20030, 19980, held, 20024, 20031, 20028, false, 60, 10, double.NaN, 2, 0.25, 4).Ok, "winner: the last closed candle did not test the level → no");
            var wd = KeystoneAddAdvice.Winner(1, 1, 20000, 20030, 19980, held, 20019, 20031, 20025, true, 60, 10, double.NaN, 2, 0.25, 4);
            Check(!wd.Ok && wd.Text.Contains("defended"), "winner: the other side defended there (order flow) → not here", wd.Text);
            var ws = KeystoneAddAdvice.Winner(-1, 2, 20040, 20010, 20060, new KeystoneLevel { Type = "VWAP", Name = "VWAP", Price = 20020 }, 20005, 20021, 20012, false, 200, 10, double.NaN, 2, 0.25, 4);
            Check(ws.Ok && Math.Abs(ws.Stop - 20024) < 1e-9, "winner SHORT: the stop above the held level (20024)", ws.Text);
        }
        // ---- 8. ➕ ADD TO THE LOSER (scale in)
        {
            var lvl = new KeystoneLevel { Type = "FVG", Tf = "30", Name = "30m FVG", Price = 20050 }; var edge = Tuple.Create(60, 100, "turned the price 60 % (n = 100) = an edge", true); var none = Tuple.Create(47, 3296, "turned the price 47 % (n = 3,296) = no edge", false);
            var a = KeystoneAddAdvice.Loser(-1, 1, 20000, 20020, 20080, lvl, edge, 500, 10, double.NaN, 2, 0.25, 1.74, 4);
            Check(a.Ok && a.Add == 1 && Math.Abs(a.NewAvg - 20025) < 1e-9 && Math.Abs(a.BreakEven - (20025 - 0.87)) < 1e-9 && Math.Abs(a.RiskUsd - 220) < 1e-9 && a.Text.Contains("new average 20,025") && a.Text.Contains("a suggestion — you decide"), "loser: +1 at the 30m FVG 20050 → average 20025, break-even after costs 20024.13, total at the stop −$220", a.Text);
            var b1 = KeystoneAddAdvice.Loser(-1, 1, 20000, 20020, 20040, lvl, edge, 500, 10, double.NaN, 2, 0.25, 1.74, 4);
            Check(!b1.Ok && b1.Text.Contains("don't add") && b1.Text.Contains("BEFORE"), "loser: the stop before the level → don't add", b1.Text);
            var b2 = KeystoneAddAdvice.Loser(-1, 1, 20000, 20020, 20080, lvl, none, 500, 10, double.NaN, 2, 0.25, 1.74, 4);
            Check(!b2.Ok && b2.Text.Contains("no edge"), "loser: no measured edge → don't add", b2.Text);
            var b3 = KeystoneAddAdvice.Loser(-1, 1, 20000, 20020, 20080, lvl, edge, 500, 10, 200, 2, 0.25, 1.74, 4);
            Check(!b3.Ok && b3.Text.Contains("floor"), "loser: the room to the floor < 1.5 × the loss at the stop → don't add", b3.Text);
            var b4 = KeystoneAddAdvice.Loser(-1, 1, 20000, 20020, 20080, lvl, edge, 500, 1, double.NaN, 2, 0.25, 1.74, 4);
            Check(!b4.Ok && b4.Text.Contains("max contracts"), "loser: max contracts → don't add", b4.Text);
            var b5 = KeystoneAddAdvice.Loser(-1, 1, 20000, 20020, 20080, lvl, edge, 100, 10, double.NaN, 2, 0.25, 1.74, 4);
            Check(!b5.Ok && b5.Text.Contains("risk setting"), "loser: over the risk setting → don't add", b5.Text);
            var b6 = KeystoneAddAdvice.Loser(-1, 1, 20000, 20020, 20080, null, edge, 500, 10, double.NaN, 2, 0.25, 1.74, 4);
            Check(!b6.Ok && b6.Text.Contains("no strong level"), "loser: no strong level → don't add");
            var lg = KeystoneAddAdvice.Loser(1, 2, 20000, 19990, 0, new KeystoneLevel { Type = "PDL", Name = "PDL", Price = 19970 }, edge, 1000, 10, double.NaN, 2, 0.25, 1.74, 4);
            Check(lg.Ok && lg.Add == 2 && Math.Abs(lg.NewAvg - 19985) < 1e-9 && Math.Abs(lg.Stop - 19966) < 1e-9 && lg.Text.Contains("set it"), "loser LONG with no stop: +2 at PDL (never more than doubling), a stop beyond it suggested", lg.Text);
        }
        // ---- 9. ● BIG PRINTS on the chart: closed minutes only, the session history (cap, numbering), the age fade, beside the body, the latest marker, the card
        {
            var now = new DateTime(2026, 3, 11, 10, 30, 20); var hist = new List<KeystoneBigMark>();
            var found = new List<KeystoneFlowBigPrint> { new KeystoneFlowBigPrint { Time = new DateTime(2026, 3, 11, 10, 28, 10), Side = 1, Size = 40, Price = 20000, After = "held" },
                new KeystoneFlowBigPrint { Time = new DateTime(2026, 3, 11, 10, 29, 50), Side = -1, Size = 60, Price = 20004, After = "failed" },
                new KeystoneFlowBigPrint { Time = new DateTime(2026, 3, 11, 10, 30, 5), Side = 1, Size = 90, Price = 20006, After = "just now" } };
            KeystoneBigPrints.Merge(hist, found, now);
            Check(hist.Count == 2 && hist.All(h => h.Print.Time.Minute < 30), "only prints of CLOSED minutes get a circle (10:30's forming minute excluded)");
            var fm = KeystoneBigPrints.Forming(found, now); Check(fm != null && fm.Size == 90, "the forming minute's biggest print = the small 'forming' dot");
            var latest = KeystoneBigPrints.Latest(hist); Check(latest != null && latest.Seq == 2 && KeystoneBigPrints.IsNew(latest, now) && !KeystoneBigPrints.IsNew(latest, now.AddSeconds(70)), "the latest = #2, NEW for 60 s after its minute closed");
            KeystoneBigPrints.Merge(hist, found, now.AddMinutes(1)); Check(hist.Count == 3 && hist[2].Seq == 3 && hist[0].Seq == 1, "the next minute: 10:30's print joins as #3, the older ones stay");
            for (int i = 0; i < 60; i++) KeystoneBigPrints.Merge(hist, new[] { new KeystoneFlowBigPrint { Time = new DateTime(2026, 3, 11, 11, 0, 0).AddMinutes(i), Side = 1, Size = 30, Price = 20010 } }, new DateTime(2026, 3, 11, 12, 30, 0));
            Check(hist.Count == KeystoneBigPrints.Cap && hist[0].Seq > 3 && hist.Last().Seq == 63, "cap 40: the oldest removed first, numbering continues", hist.Count + " #" + hist[0].Seq + "…#" + hist.Last().Seq);
            Check(Math.Abs(KeystoneBigPrints.Opacity(0) - 0.85) < 1e-9 && Math.Abs(KeystoneBigPrints.Opacity(60) - 0.55) < 1e-9 && Math.Abs(KeystoneBigPrints.Opacity(500) - 0.25) < 1e-9, "faded by age: 0.85 → 0.25 over 2 h");
            double d = KeystoneBigPrints.Diameter(90, 90), cxW = KeystoneBigPrints.CenterX(100, 80, 30, d), cxN = KeystoneBigPrints.CenterX(100, 9, 6.48, d);
            Check(d <= KeystoneBigPrints.MaxD && KeystoneBigPrints.Diameter(10, 90) < d, "size by contracts (capped)");
            Check(cxW - d / 2 >= 100 + 15 && cxW + d / 2 <= 180 - 15, "zoomed in: the circle sits fully in the gap right of its body", cxW.ToString());
            Check(cxN > 100 + 3.24 && cxN < 109 - 3.24, "zoomed out: the centre in the gap between the bodies (never inside one; drawn behind them)", cxN.ToString());
            string card = KeystoneBigPrints.Card(hist[1], 20020, 0.25, null, new[] { "▲ BUY ZONE 20,000–20,004" });
            Check(card.Contains("#") && card.Contains("of today") && card.Contains("contracts @") && (card.Contains("if the price comes BACK") || card.Contains("back into")) && card.Contains("BUY ZONE"), "the card: #n of today, contracts @ price, the IF / THEN", card);
            var hy = new List<KeystoneBigMark>(); KeystoneBigPrints.Merge(hy, found, now); KeystoneBigPrints.Merge(hy, new[] { new KeystoneFlowBigPrint { Time = new DateTime(2026, 3, 11, 18, 5, 0), Side = 1, Size = 50, Price = 20100 } }, new DateTime(2026, 3, 11, 18, 10, 0));
            Check(hy.Count == 1 && hy[0].Seq == 1, "a new session starts its own history");
            Check(KeystoneBigPrints.Role(found[1]) == "FAILED" && KeystoneBigPrints.Role(found[0]) == "BUY", "colour role: FAILED (amber ring) / BUY / SELL");
        }
        // ---- 10. the in-trade box follows ADDS (user: SHORT 2 after an add read "@ 28197.00 −$998" while the real average was 28362.38 −$302)
        {
            var t0 = new DateTime(2026, 11, 12, 10, 0, 0); var e1 = Tuple.Create(-1, t0, 28197.0);
            var e2 = KeystoneAddAdvice.FollowPosition(e1, -2, 28362.38);
            Check(e2 != e1 && e2.Item1 == -2 && Math.Abs(e2.Item3 - 28362.38) < 1e-9 && e2.Item2 == t0, "after an ADD: the current size (2) + the account's average (28362.38), the start time kept");
            Check(ReferenceEquals(KeystoneAddAdvice.FollowPosition(e2, -2, 28362.38), e2), "nothing changed → the same trade");
            Check(ReferenceEquals(KeystoneAddAdvice.FollowPosition(e2, 1, 28400), e2), "a flip to the other side is a NEW trade (not followed here)");
            var e3 = KeystoneAddAdvice.FollowPosition(e2, -1, 28362.38); Check(e3.Item1 == -1, "a partial exit: the size follows (1)");
        }
        Console.WriteLine(failures == 0 ? "LEVEL MAP TESTS: ALL PASS" : "LEVEL MAP TESTS: " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
