// 📥 BUILD 2026-11-11s — the TICK LIBRARY (format v2 read / write + the old v1, the calendar, merge with the live recording, the download plan, the export for
// Claude), the ORDER-FLOW SOURCE BADGE + the guardrail (no order flow without real trades — never from candles), and the 💡 3 SETUPS + OVERNIGHT + SKIP WILD DAYS
// filters. Files only: nothing here sends an order.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class TickLibraryTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "ksticklib-" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(d); return d; }
    static List<KeystoneOfTick> Day(DateTime day, int n, double quotedShare, int seed)
    {
        var r = new Random(seed); var o = new List<KeystoneOfTick>(); var t = KeystoneTickLib.SessionStart(day).AddSeconds(5); double px = 20000;
        for (int i = 0; i < n; i++) { t = t.AddMilliseconds(1 + r.Next(40000)); px += (r.Next(3) - 1) * 0.25; bool q = r.NextDouble() < quotedShare; o.Add(new KeystoneOfTick { Time = t, Price = px, Size = 1 + r.Next(9), Side = r.Next(2) == 0 ? 1 : -1, Quoted = q }); }
        return o;
    }
    static string Gunzip(byte[] gz) { using (var ms = new MemoryStream(gz)) using (var z = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress)) using (var sr = new StreamReader(z)) return sr.ReadToEnd(); }

    public static int Main()
    {
        var root = Tmp(); var mon = new DateTime(2026, 3, 9);   // a Monday
        // ---- 1. the session calendar
        Check(KeystoneTickLib.IsSessionDay(mon) && !KeystoneTickLib.IsSessionDay(mon.AddDays(5)) && !KeystoneTickLib.IsSessionDay(mon.AddDays(6)), "session days: Monday yes, Saturday / Sunday no");
        Check(!KeystoneTickLib.IsSessionDay(new DateTime(2026, 4, 3)) && !KeystoneTickLib.IsSessionDay(new DateTime(2026, 12, 25)) && !KeystoneTickLib.IsSessionDay(new DateTime(2026, 1, 1)) && !KeystoneTickLib.IsSessionDay(new DateTime(2022, 12, 26)) && KeystoneTickLib.IsSessionDay(new DateTime(2026, 4, 6)),
            "holidays: Good Friday 2026-04-03, Christmas, New Year, Christmas on a Sunday → Monday 2022-12-26 off; Easter Monday trades");
        Check(KeystoneTickLib.SessionOfTick(new DateTime(2026, 3, 8, 18, 0, 1)) == mon && KeystoneTickLib.SessionOfTick(new DateTime(2026, 3, 9, 16, 59, 0)) == mon && KeystoneTickLib.SessionStart(mon) == new DateTime(2026, 3, 8, 18, 0, 0), "a session day runs from 18:00 the evening before to 17:00");

        // ---- 2. v2 round trip: time to the 100 ns, price, size, side, bid / ask flag; compressed
        var t1 = Day(mon, 20000, 0.97, 1); KeystoneTickLib.Save(root, "MNQ", mon, t1, "TICKS", true);
        KeystoneTickDay m1; var back = KeystoneTickLib.ReadFile(KeystoneTickLib.FileOf(root, "MNQ", mon), false, out m1);
        bool same = back != null && back.Count == t1.Count && Enumerable.Range(0, t1.Count).All(i => back[i].Time == t1[i].Time && back[i].Price == t1[i].Price && back[i].Size == t1[i].Size && back[i].Side == t1[i].Side && back[i].Quoted == t1[i].Quoted);
        Check(same && m1.Version == 2 && m1.Complete && m1.Count == 20000 && m1.Quoted == t1.Count(x => x.Quoted), "v2: every trade back exactly (time, price, size, side, bid / ask flag) + the header counts", back == null ? "null" : back.Count + " v" + m1.Version);
        long bytes = new FileInfo(KeystoneTickLib.FileOf(root, "MNQ", mon)).Length; Check(bytes < 20000L * 14, "v2 is compressed (" + bytes + " bytes for 20 000 trades, < 14 bytes a trade)");
        var i1 = KeystoneTickLib.Info(root, "MNQ", mon); Check(i1.Status == "COMPLETE" && i1.Count == 20000 && i1.QuotedPct > 90 && i1.Bytes == bytes, "the calendar: COMPLETE, 20 000 trades, bid / ask > 90 %, its MB", i1.Status + " " + i1.Count + " " + i1.QuotedPct);

        // ---- 3. the old v1 cache is still read (side from the tick rule, never "quoted")
        var tue = mon.AddDays(1); var f1 = KeystoneTickLib.FileOf(root, "MNQ", tue); Directory.CreateDirectory(Path.GetDirectoryName(f1));
        using (var bw = new BinaryWriter(File.Create(f1))) { bw.Write("KTK1"); bw.Write(3); bw.Write("TICKS"); int sec = 0; foreach (var p in new[] { 100.0, 100.25, 100.0 }) { bw.Write(KeystoneTickLib.SessionStart(tue).AddHours(16).AddSeconds(++sec).Ticks); bw.Write(p); bw.Write(2); } }
        KeystoneTickDay mv1; var v1 = KeystoneTickLib.OrderFlowTicks(root, "MNQ", tue, out mv1);
        Check(v1 != null && v1.Count == 3 && v1[1].Side == 1 && v1[2].Side == -1 && v1.All(x => !x.Quoted) && mv1.Status == "PARTIAL" && mv1.Version == 1, "v1 (old cache): read, side by the tick rule, not quoted, shown PARTIAL (↻ REPAIR re-downloads it)", mv1.Status);
        string src; var rp = KeystoneTickCache.Load(root, "MNQ", tue, out src); Check(rp != null && rp.Count == 3 && src == "TICKS", "the replay's KeystoneTickCache.Load goes through the library (v1 still works)");

        // ---- 4. 1-second bars: stored apart, flagged, NEVER order flow (the guardrail); the replay may walk them (source SECONDS)
        var wed = mon.AddDays(2); var secs = new List<KeystoneArcBar>(); for (int k = 1; k <= 120; k++) secs.Add(new KeystoneArcBar { Time = wed.AddHours(10).AddSeconds(k), Open = 100, High = 100.5, Low = 99.75, Close = 100.25, Volume = 8 });
        KeystoneTickLib.SaveSeconds(root, "MNQ", wed, secs); var iw = KeystoneTickLib.Info(root, "MNQ", wed); KeystoneTickDay mw;
        Check(iw.Status == "SECONDS" && !iw.RealTicks && KeystoneTickLib.OrderFlowTicks(root, "MNQ", wed, out mw) == null, "1-second day: status SECONDS, not real ticks, NO order flow from it");
        string srcW; var rw = KeystoneTickLib.LoadForReplay(root, "MNQ", wed, out srcW); Check(rw != null && rw.Count == 480 && srcW == "SECONDS", "… the replay walks the 1-second path (source SECONDS)");

        // ---- 5. the live recording + merge
        var thu = mon.AddDays(3); var dl = Day(thu, 500, 1, 3); var live = new List<KeystoneOfTick>();
        live.Add(new KeystoneOfTick { Time = dl[0].Time.AddSeconds(-2), Price = 1, Size = 1, Side = 1, Quoted = true }); live.AddRange(dl.Skip(100).Take(50)); live.Add(new KeystoneOfTick { Time = dl[dl.Count - 1].Time.AddSeconds(9), Price = 2, Size = 3, Side = -1, Quoted = true });
        Check(KeystoneTickLib.AppendLive(root, "MNQ", live) == 52 && KeystoneTickLib.Info(root, "MNQ", thu).Status == "PARTIAL" && KeystoneTickLib.Info(root, "MNQ", thu).Live, "● LIVE recording: 52 trades appended, the day PARTIAL (live)");
        KeystoneTickLib.Save(root, "MNQ", thu, dl, "TICKS", true); KeystoneTickDay mt; var mg = KeystoneTickLib.OrderFlowTicks(root, "MNQ", thu, out mt);
        Check(mg.Count == 502 && mg[0].Price == 1 && mg[mg.Count - 1].Price == 2 && mt.Status == "COMPLETE", "merge: the download wins where it covers (no duplicates), the live trades before / after it are kept", mg.Count + " " + mt.Status);

        // ---- 6. the download plan
        var fri = mon.AddDays(4);
        var plan = KeystoneTickDownload.Plan("MNQ", mon, mon.AddDays(13), d => KeystoneTickLib.Info(root, "MNQ", d), false, new DateTime(2026, 6, 1));
        Check(plan.Days.Count == 7 && !plan.Days.Contains(mon) && !plan.Days.Contains(thu) && !plan.Days.Contains(wed) && plan.Days.Contains(tue) && plan.Days.All(KeystoneTickLib.IsSessionDay), "plan: the 10 session days minus the 2 complete ones and the 1-second one = 7 (weekends never asked; ↻ REPAIR re-asks the 1-second day)", string.Join(" ", plan.Days.Select(x => x.ToString("MM-dd"))));
        var rep = KeystoneTickDownload.Plan("MNQ", mon, mon.AddDays(13), d => KeystoneTickLib.Info(root, "MNQ", d), true, new DateTime(2026, 6, 1));
        Check(rep.Days.Count == 2 && rep.Days.Contains(tue) && rep.Days.Contains(wed), "↻ REPAIR: only the partial / old / 1-second days (tue v1, wed seconds)", string.Join(" ", rep.Days.Select(x => x.ToString("MM-dd"))));
        var p2 = plan; var d0 = p2.Next().Value;
        Check(p2.OnResult(d0, 0, 0, "Timeout") == "RETRY" && p2.Next().Value == d0 && p2.OnResult(d0, 1000, 980, "") == "SAVE" && p2.Saved == 1, "a failed day is asked once more, then saved");
        var d1 = p2.Next().Value; Check(p2.OnResult(d1, 0, 0, "err") == "RETRY" && p2.OnResult(d1, 0, 0, "err") == "FAILED" && p2.Failed == 1 && p2.Next().Value != d1, "twice failed → FAILED, the plan moves on");
        var d2 = p2.Next().Value; Check(p2.OnResult(d2, 0, 0, "") == "NOSESSION" && p2.NoSession == 1, "no trades, no error = no session (skipped quietly)");
        p2.Paused = true; Check(p2.Next() == null && p2.Progress().StartsWith("⏸ paused"), "⏸ PAUSE: no next day"); p2.Paused = false;
        var d3 = p2.Next().Value; p2.OnResult(d3, 2100000, 2058000, "");
        string pg = p2.Progress(); Check(pg.Contains("day 5 / 7") && pg.Contains("2.1 M ticks") && pg.Contains("bid/ask 98 %"), "progress: \"day 5 / 7 • 2.1 M ticks • bid/ask 98 %\"", pg);
        Check(p2.OnResult(d3, 1, 1, "") == "IGNORED", "a late answer for a day already done is ignored");

        // ---- 7. 📦 EXPORT FOR CLAUDE: one zip per month, README + per-day CSV.gz (time ms, price, size, side, quoted)
        var outDir = Path.Combine(root, "exports"); var zips = KeystoneTickLib.ExportForClaude(root, "MNQ", mon.AddDays(-20), mon.AddDays(30), outDir);
        Check(zips.Count == 1 && zips[0].EndsWith("ticks-MNQ-2026-03.zip"), "one zip for March (no ticks in Feb / Apr)", string.Join(";", zips));
        var z = KeystoneZip.Read(zips[0]);
        Check(z.ContainsKey("README.txt") && z.ContainsKey("MNQ/2026-03-09.csv.gz") && z.ContainsKey("MNQ/2026-03-10.csv.gz") && z.ContainsKey("MNQ/2026-03-12.csv.gz") && !z.ContainsKey("MNQ/2026-03-11.csv.gz"), "zip: README + the real-tick days, NOT the 1-second day", string.Join(",", z.Keys));
        var csv = Gunzip(z["MNQ/2026-03-09.csv.gz"]).Split('\n'); var c1 = csv[1].Split(',');
        Check(csv[0] == KeystoneTickLib.CsvHeader && csv.Length == 20002 && c1.Length == 6 && c1[0].Length == 23 && (c1[4] == "1" || c1[4] == "-1") && (c1[5] == "1" || c1[5] == "0"), "day CSV: header + 20 000 rows, time to the ms, side ±1, quoted 1 / 0", csv[0] + " | " + csv[1]);
        Check(Encoding.UTF8.GetString(z["README.txt"]).Contains("bidask_quoted"), "README explains the columns");
        if (File.Exists("/usr/bin/unzip")) { var pi = new System.Diagnostics.ProcessStartInfo("/usr/bin/unzip", "-tq \"" + zips[0] + "\"") { RedirectStandardOutput = true, UseShellExecute = false }; var pr = System.Diagnostics.Process.Start(pi); string uo = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); Check(pr.ExitCode == 0, "unzip -t accepts the zip", uo); }

        // ---- 8. the ORDER-FLOW SOURCE BADGE + the guardrail
        Check(KeystoneOfSource.Badge(1000, 980) == "● REAL TICKS • bid/ask 98 %" && KeystoneOfSource.Level(1000, 980) == 2, "badge green: ● REAL TICKS • bid/ask 98 %");
        Check(KeystoneOfSource.Badge(1000, 10) == "● REAL TICKS • tick rule" && KeystoneOfSource.Level(1000, 10) == 1, "badge amber: ● REAL TICKS • tick rule");
        Check(KeystoneOfSource.Badge(0, 0) == "NO ORDER FLOW — download ticks (📥)" && KeystoneOfSource.Level(0, 0) == 0, "badge grey: NO ORDER FLOW — download ticks (📥)");
        var wins = new List<Tuple<DateTime, DateTime>> { Tuple.Create(mon.AddHours(9), mon.AddHours(9).AddMinutes(1)) };
        Check(KeystoneOfSource.FootprintsOrNone(new List<KeystoneOfTick>(), wins, 0.25, new KeystoneOfSettings()) == null && KeystoneOfSource.FootprintsOrNone(null, wins, 0.25, new KeystoneOfSettings()) == null, "GUARDRAIL: no trades → NO footprint at all (no delta, no buyers %)");
        var fpE = KeystoneOrderFlow.Footprints(new List<KeystoneOfTick>(), wins, 0.25, new KeystoneOfSettings());
        Check(fpE.Count == 1 && fpE[0].Empty && fpE[0].Delta == 0 && fpE[0].Volume == 0 && fpE[0].Rows.Count == 0, "GUARDRAIL: the footprint engine itself gives an EMPTY candle without trades (it never reads OHLC)");
        Check(KeystoneTickLib.CoverageText(3, 5) == "real ticks ✓ 3 days of 5 (the rest: 1-minute path)" && KeystoneTickLib.CoverageText(5, 5) == "real ticks ✓ 5 days of 5" && KeystoneTickLib.CoverageText(0, 5).StartsWith("no real ticks"), "coverage line: real ticks ✓ n days of m");
        Check(KeystoneTickLib.RealDays(root, "MNQ", new[] { mon, tue, wed, thu, fri }) == 3, "RealDays counts mon / tue / thu (wed = 1-second, fri = missing)");

        // ---- 9. 💡 3 SETUPS + OVERNIGHT + SKIP WILD DAYS
        var ps = KeystoneTriSettings.OvernightSkipWild();
        Check(ps.Overnight && Math.Abs(ps.AtrSkipTop - 1.0 / 3) < 1e-9 && ps.Setups == "FVG,AGG" && ps.Qty == 5 && ps.StopUsd == 500 && ps.TargetUsd == 1050 && ps.EOneTrade && ps.FOneTrade && ps.PayoutHold == 0 && ps.Window == "NY", "preset: FVG + AGG, overnight, skip top third, SMALL 1-A-DAY (5 MNQ −$500 / +$1,050, one a day, payout ASAP)");
        Check(ps.Describe().Contains("OVERNIGHT side only") && ps.Describe().Contains("SKIP WILD") && KeystoneTriSettings.OvernightStudy.Contains("+$4,606") && KeystoneTriSettings.OvernightStudy.Contains("+$6,987") && KeystoneTriSettings.OvernightStudy.Contains("49.9 %"), "the description and the research numbers");
        // a synthetic set of sessions: night up on even days, down on odd days; each day a 10:00 FVG-like buy and sell candidate made by hand
        var bars = new List<KeystoneArcBar>(); var dayList = new List<DateTime>(); var dd = new DateTime(2025, 1, 6);
        for (int k = 0; dayList.Count < 30; k++, dd = dd.AddDays(1))
        {
            if (!KeystoneTickLib.IsSessionDay(dd)) continue; dayList.Add(dd); bool up = dayList.Count % 2 == 0; double wild = dayList.Count == 30 ? 8 : 1;   // the last day is wild
            double px = 100; for (var t = dd.AddDays(-1).AddHours(18).AddMinutes(1); t <= dd.AddHours(16); t = t.AddMinutes(1))
            {
                if (t <= dd.AddHours(9).AddMinutes(30)) px += up ? 0.01 : -0.01; double r0 = (t >= dd.AddHours(9).AddMinutes(30) ? wild : 1) * 0.5;
                bars.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = px, High = px + r0, Low = px - r0, Close = px, Volume = 5 });
            }
        }
        var data = KeystoneTriData.From(bars); var on = KeystoneTriSetup.OvernightDirs(data);
        Check(on.Count == 30 && on[dayList[1]] == 1 && on[dayList[0]] == -1, "overnight move per session: 18:00 open → 9:30 close (up / down)", on.Count + "");
        var cands = new List<KeystoneTriCandidate>();
        foreach (var day in dayList) foreach (var dir in new[] { 1, -1 }) foreach (var hm in new[] { 9 * 60 + 15, 10 * 60, 12 * 60 })
                { var tt = day.AddMinutes(hm); int idx = data.LastAtOrBefore(tt.Ticks); cands.Add(new KeystoneTriCandidate { Setup = "FVG", Dir = dir, Day = day, Signal = tt, Entry = tt, EntryIdx = idx, EntryPx = 100 }); }
        var onlyOn = KeystoneTriSetup.Filter(data, new KeystoneTriSettings { Overnight = true }.ApplyWindow(), cands);
        Check(onlyOn.Count == 60 && onlyOn.All(c => c.Dir == on[c.Day] && c.Entry.Hour >= 10), "OVERNIGHT: only the night's side, nothing before 9:30", onlyOn.Count + "");
        var sAtr = new KeystoneTriSettings { AtrSkipTop = 1.0 / 3 }.ApplyWindow(); var atrK = KeystoneTriSetup.Filter(data, sAtr, cands.Where(c => c.Entry.Hour == 12).ToList());
        Check(atrK.Count == 58 && atrK.All(c => c.Day != dayList[29]), "SKIP WILD: the wild last day (ATR in the top third of the previous 20 sessions) is skipped; normal days kept", atrK.Count + "");
        Check(KeystoneTriSetup.Filter(data, sAtr, cands.Where(c => c.Day <= dayList[9] && c.Entry.Hour == 12).ToList()).Count == 20, "SKIP WILD needs 10 earlier sessions (no lookahead, never uses the day itself or later days)");
        var full = KeystoneTriSetup.Candidates(data, ps); Check(full.All(c => c.Setup != "ENG" && c.Dir == on[c.Day]), "Candidates() applies the preset's filters (FVG / AGG only, the night's side)", full.Count + "");

        // ---- 10. a minute touching both the stop and the target: the ticks decide (3-SETUP ROTATION with the library)
        var sb = new KeystoneTriSettings { Window = "NY", Qty = 5, StopUsd = 100, TargetUsd = 100 }.ApplyWindow(); var bb = new List<KeystoneArcBar>(); var dz = new DateTime(2025, 2, 3);
        for (var t = dz.AddHours(9).AddMinutes(31); t <= dz.AddHours(10); t = t.AddMinutes(1)) bb.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = 100, High = 100.25, Low = 99.75, Close = 100, Volume = 5 });
        bb[5] = new KeystoneArcBar { Symbol = "MNQ", Time = bb[5].Time, Open = 100, High = 120, Low = 80, Close = 100, Volume = 50 };
        var dz1 = KeystoneTriData.From(bb); var cz = new KeystoneTriCandidate { Setup = "AGG", Dir = 1, Day = dz, Signal = bb[2].Time, Entry = bb[2].Time, EntryIdx = 2, EntryPx = 100, StopPx = 100 - sb.StopPts, TargetPx = 100 + sb.TargetPts };
        var noT = KeystoneTriSetup.Resolve(dz1, sb, cz, double.PositiveInfinity);
        var tk = new List<KeystoneTick> { new KeystoneTick { Time = bb[5].Time.AddSeconds(-50), Price = 115, Volume = 1 }, new KeystoneTick { Time = bb[5].Time.AddSeconds(-20), Price = 85, Volume = 1 } };
        sb.TicksOf = d => tk; var withT = KeystoneTriSetup.Resolve(dz1, sb, cz, double.PositiveInfinity);
        Check(noT.Reason == "STOP (BOTH)" && withT.Reason == "TARGET (TICKS)" && withT.TickDecided && withT.Pnl > 0, "both touched in one minute: without ticks the stop is assumed; the real ticks show the target came first", noT.Reason + " / " + withT.Reason);
        tk.Reverse(); tk[0] = new KeystoneTick { Time = bb[5].Time.AddSeconds(-50), Price = 85, Volume = 1 }; tk[1] = new KeystoneTick { Time = bb[5].Time.AddSeconds(-20), Price = 115, Volume = 1 };
        Check(KeystoneTriSetup.Resolve(dz1, sb, cz, double.PositiveInfinity).Reason == "STOP (TICKS)", "… and the stop when it came first");


        // ---- 11. 🎯 the ORDERFLOW desk's SIGNAL card: stop beyond the defended level, target at the next volume node ≥ 1 R, size from the $ risk
        {
            var f0 = new KeystoneFootprint { Open = 100, High = 101, Low = 99, Close = 100.5, Trades = 10 }; f0.Stacks.Add(new KeystoneOfStack { Lo = 99.5, Hi = 100, Side = 1, Count = 3 });
            var fps = new List<KeystoneFootprint> { new KeystoneFootprint(), new KeystoneFootprint(), f0 };
            var prof = new KeystoneProfile { Poc = 101.5, Vah = 103, Val = 98 }; prof.Hvn.Add(100.75); prof.Hvn.Add(102.5);
            var dplan = KeystoneOfDesk.Plan(new KeystoneOfSignal { Bar = 2, Dir = 1, Kind = "STACKED" }, fps, prof, 100.5, 0.25, 2, 100, 40, 2);
            Check(dplan != null && Math.Abs(dplan.Stop - 99.0) < 1e-9 && dplan.Level.Contains("stacked buying zone 99.50") && Math.Abs(dplan.Target - 102.5) < 1e-9 && dplan.TargetWhy.Contains("HVN") && dplan.Qty == 33, "BUY: stop 2 ticks under the stacked zone (99.00), target the next HVN ≥ 1 R (102.50, not 100.75), 33 micros for $100 at 1.5 pts", dplan == null ? "null" : dplan.Stop + " " + dplan.Target + " " + dplan.Qty + " " + dplan.Level);
            var g1 = new KeystoneFootprint { Open = 100, High = 100.75, Low = 99.25, Close = 99.5, Trades = 5 }; var fps1 = new List<KeystoneFootprint> { g1 };
            var p1 = KeystoneOfDesk.Plan(new KeystoneOfSignal { Bar = 0, Dir = -1, Kind = "ABSORB" }, fps1, new KeystoneProfile(), 99.5, 0.25, 2, 100, 40, 2);
            Check(p1 != null && Math.Abs(p1.Stop - 101.25) < 1e-9 && Math.Abs(p1.Target - 89.5) < 1e-9 && p1.TargetWhy.Contains("plan's bracket") && p1.Level.Contains("high"), "SELL: stop above the signal candle's high + 2 ticks, no node → the plan's bracket (40 ticks)", p1 == null ? "null" : p1.Stop + " " + p1.Target);
            Check(KeystoneOfDesk.Plan(new KeystoneOfSignal { Bar = 0, Dir = 1 }, fps1, null, 99.0, 0.25, 2, 100, 40, 2) == null, "price already through the defended level → no plan (never a made-up stop)");
        }
        try { Directory.Delete(root, true); } catch { }
        Console.WriteLine(failures == 0 ? "ALL TICK LIBRARY TESTS PASSED" : failures + " TICK LIBRARY TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
