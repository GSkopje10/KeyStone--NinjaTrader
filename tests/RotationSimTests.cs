// 🔄 ROTATION SIM (BUILD 2026-11-11k): non-stop bracket trades on a TPT PRO trailing floor — exact maths on hand-made paths (simulation only, nothing is sent).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class RotationSimTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b, double tol = 1e-6) { return Math.Abs(a - b) <= tol; }
    // a hand-made path: continuous (the minute path's inner moves) or jumps (ticks)
    static KeystoneRSimDay Day(DateTime day, double[] px, bool gaps, string src = "MINUTE", double medRange = 0)
    {
        var t0 = day.Date.AddHours(9).AddMinutes(30);
        return new KeystoneRSimDay { Day = day.Date, Source = src, Px = px, T = px.Select((p, i) => t0.AddSeconds(20 * i).Ticks).ToArray(), Gap = px.Select((p, i) => gaps && i > 0).ToArray(), BarT = new long[0], BarO = new double[0], BarC = new double[0], MedRange = medRange };
    }
    static KeystoneArcBar B(DateTime t, double o, double h, double l, double c) { return new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = o, High = h, Low = l, Close = c, Volume = 10 }; }

    public static int Main()
    {
        double c = KeystoneMoveStudy.Cost("MNQ"); var d0 = new DateTime(2025, 3, 10);
        Check(Eq(KeystoneMoveStudy.PointValue("MNQ"), 2) && c > 1 && c < 3, "MNQ: $2 a point, costs " + c.ToString("0.00") + " a contract a round trip");

        // 1. 1 MNQ ±10 pts, pattern BSBS: B 100→110 TP, S 110→100 TP, B 100→90 SL, S 90→100 SL
        var s = new KeystoneRSimSettings { Qty = 1, TpPts = 10, SlPts = 10, Dir = "BSBS", MaxDays = 1 };
        var r = KeystoneRotationSim.Run(s, new List<KeystoneRSimDay> { Day(d0, new double[] { 100, 110, 100, 90, 100, 100.5 }, false) }, 0);
        Check(r.Trades >= 4 && string.Join("", r.Log.Take(4).Select(t => t.Side > 0 ? "B" : "S")) == "BSBS", "pattern BSBS: the sides by trade number", string.Join(",", r.Log.Select(t => (t.Side > 0 ? "B" : "S") + t.How)));
        Check(r.Log.Take(4).Select(t => t.How).SequenceEqual(new[] { "TP", "TP", "SL", "SL" }), "TP, TP, SL, SL", string.Join(",", r.Log.Select(t => t.How)));
        Check(Eq(r.Log[0].Net, 20 - c) && Eq(r.Log[1].Net, 20 - c) && Eq(r.Log[2].Net, -20 - c) && Eq(r.Log[3].Net, -20 - c), "1 MNQ ±10 pts: a win nets $20 − costs, a loss −$20 − costs", string.Join(" ", r.Log.Select(t => t.Net.ToString("0.00"))));
        Check(Eq(r.Log[1].EntryPx, 110) && Eq(r.Log[2].EntryPx, 100) && Eq(r.Log[3].EntryPx, 90), "each trade enters at the previous one's exit price");
        Check(r.Log.Last().How == "SESSION END" && r.Result == "OPEN" && Eq(r.Final, r.Log.Sum(t => t.Net)), "the 5th trade is closed at the session end, the account stays open", r.Log.Last().How + " " + r.Result);
        Check(Eq(s.WinNet, 20 - c) && Eq(s.LossNet, -20 - c) && s.CostLine.Contains("TP − costs"), "settings: win / loss net + the cost line", s.CostLine);

        // 2. ⇄ switch on target / stay on stop: B TP → S; S SL → stay S; S TP → B
        var s2 = new KeystoneRSimSettings { Qty = 1, TpPts = 10, SlPts = 10, Dir = "RULE:SWITCH_TP", MaxDays = 1 };
        var r2 = KeystoneRotationSim.Run(s2, new List<KeystoneRSimDay> { Day(d0, new double[] { 100, 110, 120, 110, 109 }, false) }, 0);
        Check(string.Join("", r2.Log.Take(3).Select(t => (t.Side > 0 ? "B" : "S") + t.How + ";")) == "BTP;SSL;STP;", "switch on target / stay on stop: B TP → S SL → S TP", string.Join(",", r2.Log.Select(t => (t.Side > 0 ? "B" : "S") + t.How)));
        var s2b = s2.Copy(); s2b.Dir = "RULE:STAY_TP"; var r2b = KeystoneRotationSim.Run(s2b, new List<KeystoneRSimDay> { Day(d0, new double[] { 100, 110, 120, 110, 109 }, false) }, 0);
        Check(string.Join("", r2b.Log.Take(3).Select(t => (t.Side > 0 ? "B" : "S") + t.How + ";")) == "BTP;BTP;BSL;", "stay on target / switch on stop: B TP → B TP → B SL", string.Join(",", r2b.Log.Select(t => (t.Side > 0 ? "B" : "S") + t.How)));

        // 3. real ticks (jumps): a target fills at its limit, a stop at the jump's price, the next entry at the market
        var s3 = new KeystoneRSimSettings { Qty = 1, TpPts = 10, SlPts = 10, Dir = "B", MaxDays = 1 };
        var r3 = KeystoneRotationSim.Run(s3, new List<KeystoneRSimDay> { Day(d0, new double[] { 100, 101, 103, 111, 108, 99 }, true, "TICKS") }, 0);
        Check(r3.Trades == 2 && Eq(r3.Log[0].ExitPx, 110) && r3.Log[0].How == "TP" && Eq(r3.Log[1].EntryPx, 111) && Eq(r3.Log[1].ExitPx, 99) && r3.Log[1].How == "SL" && Eq(r3.Log[1].Net, -24 - c),
            "tick path: TP at the limit 110, the next entry at 111, the stop jumps to 99 (−12 pts)", string.Join(" | ", r3.Log.Select(t => t.How + " " + t.EntryPx + "→" + t.ExitPx + " " + t.Net.ToString("0.00"))));
        Check(r3.TickDays == 1 && r3.Reliable, "a real-tick day is reliable whatever the bracket");

        // 4. 🔒 the lock on OPEN profit: 10 MNQ, TP = the lock, SL = the floor → the lock price 1100.87 → the first tick 1101
        var s4 = new KeystoneRSimSettings { Qty = 10, TpPts = 0, SlPts = 0, Dir = "B", MaxDays = 5 };
        var r4 = KeystoneRotationSim.Run(s4, new List<KeystoneRSimDay> { Day(d0, new double[] { 1000, 1110, 1050 }, false) }, 0);
        Check(r4.Result == "LOCKED" && r4.LockDay == 1 && r4.Trades == 1 && r4.Log[0].How == "LOCK" && Eq(r4.Log[0].ExitPx, 1101) && Eq(r4.Final, 20 * 101 - 10 * c), "🔒 LOCKED on the open profit at 1101 (+$2,000 incl. costs), day 1, 1 trade", r4.Result + " " + (r4.Log.Count > 0 ? r4.Log[0].How + " " + r4.Log[0].ExitPx : "") + " " + r4.Final);
        var s4b = s4.Copy(); s4b.TpPts = 150; var r4b = KeystoneRotationSim.Run(s4b, new List<KeystoneRSimDay> { Day(d0, new double[] { 1000, 1110, 1050 }, false) }, 0);
        Check(r4b.Result == "LOCKED" && r4b.Log[0].How == "LOCK", "a TP beyond the lock: the lock comes first (the account stops there)");

        // 5. 💀 blow at the floor: 10 MNQ short from 1000, SL = the floor → the floor price 1099.13 → the first tick 1099.25
        var s5 = new KeystoneRSimSettings { Qty = 10, TpPts = 0, SlPts = 0, Dir = "S", MaxDays = 5 };
        var r5 = KeystoneRotationSim.Run(s5, new List<KeystoneRSimDay> { Day(d0, new double[] { 1000, 1200 }, false) }, 0);
        Check(r5.Result == "BLOWN" && r5.Log[0].How == "FLOOR" && Eq(r5.Log[0].ExitPx, 1099.25) && Eq(r5.Final, -20 * 99.25 - 10 * c) && r5.Final <= -2000, "💀 BLOWN at the first tick beyond the floor (1099.25)", r5.Result + " " + (r5.Log.Count > 0 ? r5.Log[0].ExitPx.ToString() : "") + " " + r5.Final);
        // the stop = the TIGHTER of the SL and the floor: SL 200 pts (= $4,000) is looser → the floor
        var s5b = s5.Copy(); s5b.SlPts = 200; var r5b = KeystoneRotationSim.Run(s5b, new List<KeystoneRSimDay> { Day(d0, new double[] { 1000, 1200 }, false) }, 0);
        Check(r5b.Result == "BLOWN" && Eq(r5b.Log[0].ExitPx, 1099.25) && r5b.Log[0].How == "FLOOR", "SL wider than the room → the real stop is the floor");

        // 6. a STOP IN PROFIT: 10 MNQ, trade 1 loses 75 pts (−$1,517.40), trade 2 runs +115 pts (peak +$765.20 → floor −$1,234.80 = price 940, above its entry 925) → stopped
        //    at 940 with a +$282.60 trade — the real exit is booked and the account is blown (equity = the floor)
        var s6 = new KeystoneRSimSettings { Qty = 10, TpPts = 150, SlPts = 75, Dir = "B", MaxDays = 5 };
        var r6 = KeystoneRotationSim.Run(s6, new List<KeystoneRSimDay> { Day(d0, new double[] { 1000, 925, 1040, 900 }, false) }, 0);
        Check(r6.Trades == 2 && Eq(r6.Log[0].Net, -1500 - 10 * c) && Eq(r6.Log[1].EntryPx, 925) && Eq(r6.Log[1].ExitPx, 940) && r6.Log[1].Net > 0 && Eq(r6.Log[1].Net, 300 - 10 * c) && r6.Log[1].How == "FLOOR" && r6.Result == "BLOWN" && Eq(r6.Final, r6.Log[1].Floor, 1e-6),
            "a stop in profit: the floor (940) above the entry (925) books +$282.60 and the account is blown at the floor", string.Join(" | ", r6.Log.Select(t => t.How + " " + t.EntryPx + "→" + t.ExitPx + " " + t.Net.ToString("0.00") + " floor " + t.Floor.ToString("0.00"))) + " " + r6.Result);
        Check(Eq(r6.Peak, 20 * 115 - 10 * c - 1500 - 10 * c) && Eq(r6.GiveBack, r6.Peak - r6.Final), "the peak includes the open profit (+$765.20), give-back = peak − final", r6.Peak + " " + r6.GiveBack);

        // 7. no room for the costs: the next entry is refused as a blow
        var s7 = new KeystoneRSimSettings { Qty = 1, TpPts = 10, SlPts = 10, Dir = "B", MaxDays = 1, Dd = 21 };
        var r7 = KeystoneRotationSim.Run(s7, new List<KeystoneRSimDay> { Day(d0, new double[] { 100, 90, 95 }, false) }, 0);
        Check(r7.Result == "BLOWN", "DD $21: one $20 loss + costs → blown", r7.Result + " " + r7.Final);

        // 8. several days: the session end flattens, the next day goes on, MaxDays stops it (OPEN)
        var days8 = Enumerable.Range(0, 6).Select(i => Day(d0.AddDays(i), new double[] { 100, 104, 98, 103 }, false)).ToList();
        var s8 = new KeystoneRSimSettings { Qty = 1, TpPts = 10, SlPts = 10, Dir = "B", MaxDays = 4 };
        var r8 = KeystoneRotationSim.Run(s8, days8, 1);
        Check(r8.Result == "OPEN" && r8.Days == 4 && r8.Trades == 4 && r8.Log.All(t => t.How == "SESSION END") && r8.Start == d0.AddDays(1) && r8.End == d0.AddDays(4), "4 days of SESSION END trades, then ⏳ open (max days)", r8.Result + " days " + r8.Days + " trades " + r8.Trades);

        // 9. the minute path from bars: a green candle visits its low first, a red one its high first; the rules read the bars
        var bars = new List<KeystoneArcBar>(); var t9 = d0.AddHours(9).AddMinutes(30); double p9 = 100;
        bars.Add(B(t9, 99, 100, 99, 100));
        for (int i = 1; i <= 20; i++) { double o = p9, cl = i % 2 == 1 ? p9 + 2 : p9 - 1; bars.Add(B(t9.AddMinutes(i), o, Math.Max(o, cl) + 1, Math.Min(o, cl) - 1, cl)); p9 = cl; }
        var day9 = KeystoneRSimDay.Build(d0, bars, null, "", 930, 950);
        Check(day9 != null && day9.Source == "MINUTE" && day9.Px[0] == 100 && day9.Px[1] == 99 && day9.Px[2] == 103 && day9.Px[3] == 102, "minute path: 9:30 close 100 → (open 100) → low 99 → high 103 → close 102 (green)", day9 == null ? "null" : string.Join(" ", day9.Px.Take(8)));
        Check(day9.Px[4] == 103 && day9.Px[5] == 100 && day9.Px[6] == 101, "red candle: high 103 → low 100 → close 101", string.Join(" ", day9.Px.Take(8)));
        Check(Eq(day9.MedRange, 3.5), "the median 1-minute range = 3.5 pts (green 4, red 3)", day9.MedRange.ToString());
        var s9 = new KeystoneRSimSettings { Qty = 1, TpPts = 2, SlPts = 2, Dir = "RULE:CANDLE", MaxDays = 1 }; var r9 = KeystoneRotationSim.Run(s9, new List<KeystoneRSimDay> { day9 }, 0);
        Check(!r9.Reliable && r9.Why.Contains("NOT reliable") && r9.Trades > 0, "a 2-pt bracket on a 3.5-pt minute path is flagged NOT reliable", r9.Why);
        Check(r9.Log[0].Side == 1, "🕯 the first trade follows the 9:30 candle (green → B)");
        var s9b = s9.Copy(); s9b.TpPts = 6; s9b.SlPts = 6; Check(KeystoneRotationSim.Run(s9b, new List<KeystoneRSimDay> { day9 }, 0).Reliable, "a 6-pt bracket (≥ 1.5 × 3.5) is reliable");
        var s9c = s9.Copy(); s9c.Dir = "RULE:ANTI"; Check(KeystoneRotationSim.Run(s9c, new List<KeystoneRSimDay> { day9 }, 0).Log[0].Side == -1, "↩ against the last candle → S");
        var s9d = s9.Copy(); s9d.Dir = "RULE:TREND60"; s9d.FirstSide = 'S'; Check(KeystoneRotationSim.Run(s9d, new List<KeystoneRSimDay> { day9 }, 0).Log[0].Side == 1, "📈 60-min trend (the context bars rise) → B");
        var s9e = s9.Copy(); s9e.Dir = "RULE:COIN"; var ca = KeystoneRotationSim.Run(s9e, new List<KeystoneRSimDay> { day9 }, 0); var cb = KeystoneRotationSim.Run(s9e, new List<KeystoneRSimDay> { day9 }, 0);
        Check(ca.Log.Select(t => t.Side).SequenceEqual(cb.Log.Select(t => t.Side)) && ca.Log.Select(t => t.Side).Distinct().Count() == 2, "🎲 the coin is seeded (same run = same sides, both sides appear)");

        // 10. the no-edge maths: lock vs floor in one trade = 50 % without costs; $20 / $20 on 1 MNQ with costs ≈ 0 %
        double ne = KeystoneRotationSim.NoEdge(new KeystoneRSimSettings { Qty = 10, TpPts = 0, SlPts = 0 }, false, 40000, 10);
        Check(ne > 35 && ne < 39.5, "no-edge: one trade to the lock vs the TRAILING floor, no costs ≈ e^−1 = 36.8 % (the no-edge ceiling)", ne.ToString("0.0"));
        var sF = new KeystoneRSimSettings { Qty = 10, TpPts = 100, SlPts = 100, Dd = 4000 }; double neF = KeystoneRotationSim.NoEdge(sF, false, 40000, 1);
        Check(neF > 48 && neF < 52 && KeystoneRotationSim.NoEdge(sF, false, 40000, 1) == neF, "no-edge: one equal $2,000 / $2,000 bracket the floor never reaches = 50 / 50 (seeded, repeatable)", neF.ToString("0.0"));
        double ne2 = KeystoneRotationSim.NoEdge(new KeystoneRSimSettings { Qty = 1, TpPts = 10, SlPts = 10 }, true, 2000, 5000);
        double ne3 = KeystoneRotationSim.NoEdge(new KeystoneRSimSettings { Qty = 1, TpPts = 10, SlPts = 10 }, false, 2000, 5000);
        Check(ne2 < 3 && ne3 > ne2 + 10, "no-edge 1 MNQ $20 / $20: with costs ≈ 0 % (the costs eat the $2,000), without costs much higher", ne2.ToString("0.0") + " vs " + ne3.ToString("0.0"));

        // 11. pool + analyzer + optimizer (the unreliable row is never the best) + save / reopen
        var pdays = new List<KeystoneRSimDay>(); var rnd = new Random(3);
        for (int i = 0; i < 60; i++)
        {
            var dd = d0.AddDays(i); var px = new List<double> { 20000 }; for (int k = 0; k < 400; k++) px.Add(Math.Round((px[px.Count - 1] + (rnd.NextDouble() - 0.5) * 16) * 4) / 4);
            var cl = new List<double>(); foreach (var x in px) if (cl.Count == 0 || cl[cl.Count - 1] != x) cl.Add(x);
            pdays.Add(Day(dd, cl.ToArray(), false, "MINUTE", 6));
        }
        var starts = KeystoneRotationSim.Starts(pdays, d0, d0.AddDays(59), 3, 100);
        Check(starts.Count == 20 && starts[1] == 3, "pool starts: every 3rd day → 20 accounts", starts.Count.ToString());
        var ps = new KeystoneRSimSettings { Qty = 5, TpPts = 40, SlPts = 40, Dir = "BS", MaxDays = 10 };
        var pool = KeystoneRotationSim.Pool(ps, pdays, starts); var st = KeystoneRotationSim.Analyze(ps, pool, 2000);
        Check(st.N == 20 && st.Locked + st.Blown + st.Open == 20 && st.Hist.Sum(h => h[2] + h[3] + h[4]) == 20 && st.ByYear.Values.Sum(v => v[0]) == 20, "analyzer: every account counted once (result, histogram, per year)", "L" + st.Locked + " B" + st.Blown + " O" + st.Open);
        Check(Eq(st.ExpPerTrade, pool.Sum(x => x.Log.Sum(t => t.Net)) / Math.Max(1, st.Trades)) && st.NoEdgePct >= 0 && KeystoneRotationSim.NoEdgeLine(ps, st).Contains("NO-EDGE"), "analyzer: expectancy per trade after costs + the no-edge line", KeystoneRotationSim.NoEdgeLine(ps, st));
        var opt = KeystoneRotationSim.Optimize(ps, pdays, starts, new[] { 1, 5 }, new List<double[]> { new double[] { 20, 20 }, new double[] { 400, 400 } }, new[] { "BS", "RULE:STAY_TP" }, null, null, 300);
        var best = KeystoneRotationSim.Best(opt);
        Check(opt.Count == 8 && opt.Where(o => !o.All.Reliable).All(o => o.Rank == 0) && (best == null || best.All.Reliable) && opt.Any(o => !o.All.Reliable), "🏆 optimizer: 2 sizes × 2 brackets × 2 directions; unreliable rows (1 MNQ $20 = 10 pts < 1.5 × 6) never ranked", string.Join(" | ", opt.Select(o => o.Rank + " " + o.S.Short + " " + o.All.LockPct.ToString("0") + "% " + (o.All.Reliable ? "" : "unrel"))));
        Check(opt.All(o => o.HoldsText.StartsWith("✓") || o.HoldsText.StartsWith("✗")) && opt.All(o => o.H1.N + o.H2.N == o.All.N), "both halves re-checked on every row", opt[0].HoldsText);
        string tmp = Path.Combine(Path.GetTempPath(), "ks-rsim-" + Guid.NewGuid().ToString("N"));
        try
        {
            string f = KeystoneRotationSim.SaveRun(tmp, ps, pool, st, "test BS", new DateTime(2026, 11, 9, 10, 0, 0));
            KeystoneRSimSettings back; var again = KeystoneRotationSim.LoadRun(f, out back); var runs = KeystoneRotationSim.ListRuns(tmp);
            Check(File.Exists(f) && f.EndsWith("2026-11-09-100000-test-BS.csv") && again.Count == pool.Count && again.Sum(x => x.Log.Count) == pool.Sum(x => x.Log.Count) && back.Qty == 5 && Eq(back.TpPts, 40) && back.Dir == "BS" && runs.Count == 1 && runs[0][1] == "test BS",
                "💾 saved (trades CSV + runs.csv) and reopened: same accounts, trades and settings", f);
            Check(again.Select(x => x.Result).SequenceEqual(pool.Select(x => x.Result)) && Eq(again[0].Log.Sum(t => t.Net), pool[0].Log.Sum(t => t.Net), 0.05), "reopened results match");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Check(KeystoneRSimDir.Clean("bs b-s") == "BSBS" && KeystoneRSimDir.Clean("") == "B" && KeystoneRSimDir.Clean("RULE:COIN") == "RULE:COIN" && KeystoneRSimSettings.PtsOf(20, 1, 2, 0.25) == 10 && KeystoneRSimSettings.PtsOf(20, 60, 2, 0.25) == 0.25, "direction text cleaned, $ → points on the tick");
        var kv = KeystoneRSimSettings.FromKv(new KeystoneRSimSettings { Qty = 7, TpPts = 12.5, SlPts = 0, Dir = "RULE:TREND60", StartHhmm = 1000, FlatHhmm = 1530, MaxDays = 12, Dd = 2500, FirstSide = 'S' }.ToKv());
        Check(kv.Qty == 7 && Eq(kv.TpPts, 12.5) && kv.SlPts == 0 && kv.Dir == "RULE:TREND60" && kv.StartHhmm == 1000 && kv.FlatHhmm == 1530 && kv.MaxDays == 12 && Eq(kv.Dd, 2500) && kv.FirstSide == 'S', "settings round trip");

        Console.WriteLine(failures == 0 ? "ALL ROTATION SIM TESTS PASSED" : "ROTATION SIM TESTS: " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
