// 🎯 BUILD 2026-11-13a — the REVERSAL SCORE (confluence, closed candles only, no lookahead, the ≥ 75 % gate, the bands, the journal) + ⚡ the order-flow
// engine gate (nothing runs unless the 📊 ORDER FLOW switch shows its tab or the chart's 📊 OF chip is on; the ⚡ ENGINE OFF kill wins) + the incremental
// helpers (event measure, session flow) = the same numbers as a full walk.
using System; using System.Collections.Generic; using System.Linq; using System.IO;
using NinjaTrader.NinjaScript;
public static class ReversalScoreTests
{
    static int fails, passes;
    static void Check(bool ok, string what, string why = "") { if (ok) { passes++; Console.WriteLine("PASS  " + what); } else { fails++; Console.WriteLine("FAIL  " + what + (why != "" ? "  → " + why : "")); } }
    static readonly DateTime T0 = new DateTime(2026, 11, 10, 10, 0, 0);
    static KeystoneFootprint Fp(int i, double o, double h, double l, double c, long buy, long sell, long cvd)
    {
        var f = new KeystoneFootprint { Start = T0.AddMinutes(i), End = T0.AddMinutes(i + 1), Open = o, High = h, Low = l, Close = c, Buy = buy, Sell = sell, Cvd = cvd, Trades = 10 };
        // rows: the volume spread evenly over the candle's range in 0.25 steps
        int n = Math.Max(1, (int)Math.Round((h - l) / 0.25)) + 1; for (int k = 0; k < n; k++) f.Rows[l + k * 0.25] = new KeystoneOfRow { Buy = buy / n, Sell = sell / n };
        return f;
    }
    // a down-move into a low at bar 14 (12.00 under the previous swing at bar 8, the CVD higher there), bar 15 = a bullish engulfing with the delta turned
    static List<KeystoneFootprint> Bull(bool strong)
    {
        var l = new List<KeystoneFootprint>(); double px = 21100; long cvd = 0;
        for (int i = 0; i < 14; i++)
        {
            double o = px, c = px - 2; if (i == 8) { c = px - 6; } if (i == 9 || i == 10) { c = px + 1; }
            long b = 200, s = 300; cvd += b - s; if (i == 8) cvd -= 800;
            l.Add(Fp(i, o, Math.Max(o, c) + 0.5, Math.Min(o, c) - 0.5, c, b, s, cvd)); px = c;
        }
        // bar 14: the low — small volume (dried up), sellers hitting the bottom rows, closes in its upper half
        { double o = px, lo = px - 4, c = px - 1; var f = new KeystoneFootprint { Start = T0.AddMinutes(14), End = T0.AddMinutes(15), Open = o, High = o + 0.25, Low = lo, Close = c, Buy = strong ? 60 : 200, Sell = strong ? 90 : 300, Trades = 9 };
          cvd += f.Buy - f.Sell; f.Cvd = strong ? -500 : cvd; f.Rows[lo] = new KeystoneOfRow { Sell = strong ? 40 : 10, Buy = 2 }; f.Rows[lo + 0.25] = new KeystoneOfRow { Sell = strong ? 20 : 10, Buy = 3 }; f.Rows[o] = new KeystoneOfRow { Buy = 50, Sell = 30 }; l.Add(f); px = c; }
        // bar 15: the reversal candle
        { var p = l[14]; double o = p.Close - 0.25, c = strong ? p.Open + 2 : p.Close + 0.5; l.Add(Fp(15, o, Math.Max(o, c) + 0.25, o - 0.25, c, strong ? 500 : 150, strong ? 200 : 140, l[14].Cvd + 300)); }
        // later candles (bar 16 forming / future)
        for (int i = 16; i < 40; i++) { var p = l[i - 1]; double c = p.Close + 1; l.Add(Fp(i, p.Close, c + 0.5, p.Close - 0.25, c, 300, 200, p.Cvd + 100)); }
        return l;
    }
    static KeystoneFootprint Mirror(KeystoneFootprint f)
    {
        var m = new KeystoneFootprint { Start = f.Start, End = f.End, Open = -f.Open, High = -f.Low, Low = -f.High, Close = -f.Close, Buy = f.Sell, Sell = f.Buy, Cvd = -f.Cvd, Trades = f.Trades };
        foreach (var kv in f.Rows) m.Rows[-kv.Key] = new KeystoneOfRow { Buy = kv.Value.Sell, Sell = kv.Value.Buy }; return m;
    }
    public static int Main()
    {
        const double tk = 0.25;
        var bull = Bull(true); double ext = bull[14].Low;
        var lv = new[] { new KeystoneRevLevel("VAL", ext + 0.5, ext + 0.5) };
        var ev = new List<KeystoneOfEvent> { new KeystoneOfEvent { Time = bull[14].End.AddSeconds(-20), Type = "TRAPPED", Side = 1, Price = ext + 0.25, Symbol = "MNQ" } };
        // 1. each confluence
        var r = KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", lv, ev, null);
        Check(r != null && r.Dir == 1, "a swing low + a bullish close back = a reversal candidate");
        Func<string, bool> ok = k => r.Items.First(x => x.Key == k).Ok;
        foreach (var k in new[] { "LOC", "DIV", "VOL", "ABS", "TRAP", "FLOW", "CANDLE" }) Check(ok(k), "confluence " + k + " present", string.Join(" / ", r.Items.Select(x => (x.Ok ? "+" : "-") + x.Key + " " + x.Text)));
        Check(r.Score == 100 && r.Band == "95–100" && KeystoneReversalScore.ColourOf(r.Score, 1) == "GOLD", "all seven = 100 % (gold band)", r.Score.ToString());
        Check(r.Items.First(x => x.Key == "LOC").Text.StartsWith("at VAL"), "the location line names the level: " + r.Items.First(x => x.Key == "LOC").Text);
        Check(r.Items.First(x => x.Key == "ABS").Text.Contains("sellers absorbed"), "absorption line: " + r.Items.First(x => x.Key == "ABS").Text);
        // weights add up and each missing item takes its weight away
        Check(KeystoneReversalScore.WLocation + KeystoneReversalScore.WDivergence + KeystoneReversalScore.WVolume + KeystoneReversalScore.WAbsorb + KeystoneReversalScore.WTrapped + KeystoneReversalScore.WFlow + KeystoneReversalScore.WCandle == 100, "the weights sum to 100");
        var rNoLv = KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", new KeystoneRevLevel[0], ev, null);
        Check(rNoLv.Score == 75 && !rNoLv.Items.First(x => x.Key == "LOC").Ok && KeystoneReversalScore.ColourOf(75, 1) == "AMBER", "no level → 75 % (amber, still shown)", rNoLv.Score.ToString());
        var rNoTrap = KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", new KeystoneRevLevel[0], null, null);
        Check(rNoTrap.Score == 65 && rNoTrap.Score < KeystoneReversalScore.MinShow, "no level + no trapped traders → 65 % = under the ≥ 75 % gate (not shown)");
        Check(KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", lv, null, null).Score == 90 && KeystoneReversalScore.ColourOf(90, 1) == "TEAL" && KeystoneReversalScore.ColourOf(90, -1) == "CORAL", "90 % = the teal (buy) / coral (sell) band");
        Check(KeystoneReversalScore.Band(74) == "<75" && KeystoneReversalScore.Band(75) == "75–84" && KeystoneReversalScore.Band(84) == "75–84" && KeystoneReversalScore.Band(85) == "85–94" && KeystoneReversalScore.Band(94) == "85–94" && KeystoneReversalScore.Band(95) == "95–100", "the band edges 75 / 85 / 95");
        // a weak version: no divergence / absorption / drying
        var weak = KeystoneReversalScore.Score(Bull(false), 16, 15, 1, tk, "MNQ", lv, null, null);
        Check(weak != null && weak.Score < 75, "a plain bounce (no exhaustion, no absorption) stays under 75 %", weak == null ? "null" : weak.Score + " " + string.Join(" ", weak.Items.Select(x => (x.Ok ? "+" : "-") + x.Key)));
        // 2. closed candles only / no lookahead
        Check(KeystoneReversalScore.Score(bull, 15, 15, 1, tk, "MNQ", lv, ev, null) == null, "the forming candle (j ≥ closed count) is never scored");
        var cut = bull.Take(16).ToList(); var rCut = KeystoneReversalScore.Score(cut, 16, 15, 1, tk, "MNQ", lv, ev, null);
        Check(rCut != null && rCut.Score == r.Score && string.Join("|", rCut.Items.Select(x => x.Ok + x.Text)) == string.Join("|", r.Items.Select(x => x.Ok + x.Text)), "the later candles change nothing (same score, same lines)");
        var evLate = new List<KeystoneOfEvent> { new KeystoneOfEvent { Time = bull[16].End, Type = "TRAPPED", Side = 1, Price = ext, Symbol = "MNQ" } };
        Check(!KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", lv, evLate, null).Items.First(x => x.Key == "TRAP").Ok, "an event after the candle is not counted");
        var evSeenLate = new List<KeystoneOfEvent> { new KeystoneOfEvent { Time = bull[15].Start, Seen = bull[15].End.AddMinutes(3), Type = "TRAPPED", Side = 1, Price = ext, Symbol = "MNQ" } };
        Check(!KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", lv, evSeenLate, null).Items.First(x => x.Key == "TRAP").Ok, "an event the app only knew later is not counted");
        var lvLate = new[] { new KeystoneRevLevel("ZONE", ext, ext) { Known = bull[15].End.AddMinutes(1) } };
        Check(!KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", lvLate, ev, null).Items.First(x => x.Key == "LOC").Ok, "a zone marked after the candle is not a location");
        var sl = KeystoneReversalScore.SessionLevels(bull, 0, 15, tk);
        Check(sl.Length == 3 && sl.All(x => x.Hi <= bull.Take(15).Max(f => f.High) + 1e-9), "the session levels use the candles BEFORE it only");
        // 3. the swing rule + the mirror (sell side)
        Check(KeystoneReversalScore.Score(bull, 16, 12, 1, tk, "MNQ", lv, ev, null) == null, "a candle that is not at the swing low is not a candidate");
        var bear = bull.Select(Mirror).ToList(); var rb = KeystoneReversalScore.Score(bear, 16, 15, -1, tk, "MNQ", new[] { new KeystoneRevLevel("VAH", -(ext + 0.5), -(ext + 0.5)) }, new List<KeystoneOfEvent> { new KeystoneOfEvent { Time = ev[0].Time, Type = "TRAPPED", Side = -1, Price = -(ext + 0.25) } }, null);
        Check(rb != null && rb.Dir == -1 && rb.Score == 100 && rb.Items.First(x => x.Key == "ABS").Text.StartsWith("buyers absorbed"), "the mirror image = a 100 % SELL reversal", rb == null ? "null" : rb.Score + " " + string.Join(" ", rb.Items.Select(x => (x.Ok ? "+" : "-") + x.Key)));
        // 4. the idea: stop beyond the extreme, the scalp target clamped (MNQ 10–20 pts)
        Check(Math.Abs(r.Stop - (ext - 2 * tk)) < 1e-9 && r.Entry == bull[15].Close && r.TargetPts >= 10 - 1e-9 && r.TargetPts <= 20 + 1e-9 && Math.Abs(r.Target - (r.Entry + r.TargetPts)) < 1e-9, "entry = the close, stop 2 ticks beyond the low, target clamped to 10–20 pts", r.Entry + " " + r.Stop + " " + r.Target);
        // 5. the outcome (after the candle only) + the hit rate bands + the journal
        KeystoneReversalScore.Resolve(r, bull, 40); Check(r.Done && r.Result == 1 && r.Exit == "TARGET", "the rally after it reaches the target first", r.Exit + " " + r.Result);
        var rs = KeystoneReversalScore.Score(bull, 16, 15, 1, tk, "MNQ", lv, ev, null); var down = bull.Take(16).ToList(); down.Add(Fp(16, rs.Entry, rs.Entry + 1, rs.Stop - 1, rs.Stop - 0.5, 10, 10, 0)); down[16].High = rs.Target + 5;
        KeystoneReversalScore.Resolve(rs, down, 17); Check(rs.Done && rs.Result == -1, "a candle touching both = the stop first (conservative)");
        var rows = new List<KeystoneRevResult>(); for (int i = 0; i < 25; i++) rows.Add(new KeystoneRevResult { Symbol = "MNQ", Time = T0.AddMinutes(i), Dir = 1, Score = 88, Done = true, Result = i < 15 ? 1 : -1 });
        for (int i = 0; i < 5; i++) rows.Add(new KeystoneRevResult { Symbol = "MNQ", Time = T0.AddHours(1).AddMinutes(i), Dir = 1, Score = 96, Done = true, Result = 1 });
        Check(KeystoneReversalScore.HitText(rows, "85–94").StartsWith("60 % hit (15 / 25"), "band 85–94 with n = 25 → the measured %: " + KeystoneReversalScore.HitText(rows, "85–94"));
        Check(KeystoneReversalScore.HitText(rows, "95–100") == "learning (5)" && KeystoneReversalScore.HitText(rows, "75–84") == "learning (0)", "n < 20 → learning (n)");
        string dir = Path.Combine(Path.GetTempPath(), "ks-rev-" + Guid.NewGuid().ToString("N")); r.Symbol = "MNQ";
        KeystoneRevJournal.Save(dir, "MNQ", new[] { r, rNoTrap }, "test"); KeystoneRevJournal.Save(dir, "MNQ", new[] { r }, "test");
        var back = KeystoneRevJournal.Load(dir, "MNQ");
        Check(back.Count == 1 && back[0].Score == 100 && back[0].Result == 1 && back[0].Done && back[0].Exit == "TARGET" && File.ReadAllLines(KeystoneRevJournal.FileOf(dir, "MNQ"))[0] == KeystoneRevJournal.Header, "the journal keeps ≥ 75 % only, one line per reversal (saved twice = once), with its outcome", back.Count.ToString());
        Check(KeystoneRevJournal.Analyze(rows).Contains("85–94") && KeystoneRevJournal.Analyze(rows).Contains("not a win probability"), "📊 ANALYZE line by band");
        var box = KeystoneReversalScore.Box(r, "learning (3)");
        Check(box[0].StartsWith("🎯 REVERSAL ▲ 100 %") && box[0].Contains("confluence score") && box.Count(x => x.StartsWith("✔ ")) == 7 && box.Any(x => x.Contains("never sends")), "the click box: title, 7 ✔ lines, the idea, the measured line, display only");
        // 6. Scan finds it and only from closed candles
        var sc = KeystoneReversalScore.Scan(bull, 16, 0, tk, "MNQ", j => lv, ev, null);
        Check(sc.Any(x => x.Bar == 15 && x.Dir == 1 && x.Score == 100) && sc.All(x => x.Bar < 16), "Scan: finds the reversal, never the forming candle");
        {   // 📜 the history replay of a day: only ≥ 75 %, each resolved after its own candle
            var rnd = new Random(5); var tks = new List<KeystoneOfTick>(); double px = 21000; var d0 = new DateTime(2026, 11, 9, 9, 30, 0);
            for (int i = 0; i < 40000; i++) { double drift = Math.Sin(i / 2500.0) * 0.6; px += (rnd.NextDouble() - 0.5 + drift * 0.3) > 0 ? 0.25 : -0.25; tks.Add(new KeystoneOfTick { Time = d0.AddMilliseconds(i * 600), Price = px, Size = 1 + rnd.Next(rnd.NextDouble() < 0.03 ? 40 : 3), Side = rnd.Next(2) == 0 ? 1 : -1 }); }
            double h, lo; var rd = KeystoneReversalScore.ReplayDay(tks, "MNQ", 0.25, 21010, 20980, out h, out lo);
            Check(rd.All(x => x.Score >= 75) && rd.All(x => x.Time <= tks[tks.Count - 1].Time) && !double.IsNaN(h) && h >= lo, "📜 ReplayDay: " + rd.Count + " reversals, all ≥ 75 %, day high / low returned");
        }
        // ---- ⚡ the engine gate
        Check(!KeystoneOfGate.Want(false, false, false, false, false, true), "default (switch off, chip off) → the engine does not run");
        Check(!KeystoneOfGate.Want(false, true, false, false, false, true), "switch on but its tab not visible → no engine");
        Check(KeystoneOfGate.Want(false, true, true, false, false, true), "switch on + its tab visible → the engine runs");
        Check(KeystoneOfGate.Want(false, false, false, true, false, true) && !KeystoneOfGate.Want(false, false, false, true, false, false), "the chart chip → the engine for the chart on screen only");
        Check(!KeystoneOfGate.Want(false, false, false, false, true, true) && KeystoneOfGate.Want(false, true, false, false, true, true), "an older CHART chip counts only with the switch on");
        Check(!KeystoneOfGate.Want(true, true, true, true, true, true) && !KeystoneOfGate.TabShown(true, true) && !KeystoneOfGate.TabShown(false, false) && KeystoneOfGate.TabShown(false, true), "⚡ ENGINE OFF kills everything; the tab shows only with the switch on");
        // ---- incremental = full walk
        {
            var rnd = new Random(3); var ticks = new List<KeystoneOfTick>(); double px = 100;
            for (int i = 0; i < 6000; i++) { px += (rnd.Next(3) - 1) * 0.25; ticks.Add(new KeystoneOfTick { Time = T0.AddSeconds(i * 0.5), Price = px, Size = 1 + rnd.Next(3), Side = rnd.Next(2) == 0 ? 1 : -1 }); }
            var e1 = new KeystoneOfEvent { Time = T0.AddMinutes(5), Price = ticks[600].Price, Side = 1 }; var e2 = new KeystoneOfEvent { Time = e1.Time, Price = e1.Price, Side = 1 };
            for (int n = 300; n <= 6000; n += 97) { var nowN = ticks[n - 1].Time; KeystoneOfEvents.Measure(e1, ticks, n, nowN, 0.25); }
            var fresh = new KeystoneOfEvent { Time = e1.Time, Price = e1.Price, Side = 1 }; KeystoneOfEvents.Measure(fresh, ticks, 6000, ticks[5999].Time, 0.25);
            Check(e1.Result == fresh.Result && e1.Mfe == fresh.Mfe && e1.Mae == fresh.Mae && Equals(e1.M5, fresh.M5) && Equals(e1.M15, fresh.M15) && Equals(e1.M30, fresh.M30) && e1.Done == fresh.Done, "event Measure in pieces = one full walk", e1.Mfe + "/" + fresh.Mfe);
            var rf = new KeystoneOfRunningFlow(); long[] last = null; for (int n = 100; n <= 6000; n += 333) last = rf.Get(ticks, n, T0.AddMinutes(2), ticks[n - 1].Time); last = rf.Get(ticks, 6000, T0.AddMinutes(2), ticks[5999].Time);
            var full = KeystoneOrderFlow.Flow(ticks, 6000, ticks[5999].Time, (ticks[5999].Time - T0.AddMinutes(2)).TotalSeconds);
            Check(last[0] == full[0] && last[1] == full[1] && last[2] == full[2], "the running session flow = Flow over the whole window", last[0] + "/" + full[0]);
            var back2 = rf.Get(ticks, 3000, T0.AddMinutes(2), ticks[2999].Time); var full2 = KeystoneOrderFlow.Flow(ticks, 3000, ticks[2999].Time, (ticks[2999].Time - T0.AddMinutes(2)).TotalSeconds);
            Check(back2[0] == full2[0] && back2[2] == full2[2], "the clock going back (replay) restarts it correctly");
        }
        Console.WriteLine(fails == 0 ? "REVERSAL SCORE + ENGINE GATE TESTS PASSED (" + passes + ")" : "REVERSAL SCORE TESTS FAILED " + fails + " of " + (passes + fails));
        return fails == 0 ? 0 : 1;
    }
}
