// 🧠 ENTRY ANALYZER (BUILD 2026-11-11l): feature extraction (exact), no lookahead, the outcome walk, walk-forward split, calibration with a known edge / no edge, the score text.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class EntryAnalyzerTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b, double t = 1e-9) { return Math.Abs(a - b) <= t; }
    public static int Main()
    {
        // 70 bars: a gentle rise of +1 a minute (open = prev close, high = close + 1, low = open − 1, volume 100), then bar 65 is special
        var t0 = new DateTime(2026, 3, 10, 9, 30, 0); var b = new List<KeystoneArcBar>(); double px = 100;
        for (int i = 0; i < 70; i++) { double o = px, c = px + 1; b.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(i + 1), Open = o, High = c + 1, Low = o - 1, Close = c, Volume = 100 }); px = c; }
        b[65] = new KeystoneArcBar { Symbol = "MNQ", Time = b[65].Time, Open = 165, High = 170, Low = 164, Close = 168, Volume = 300 };   /* body 3 of range 6, upper wick 2, lower 1, 3× volume, a gap above bar 63's high (166) → no: low 164 < 166 */
        var all = KeystoneEntryFeatures.All(b); var f = all[65];
        Check(all[59] == null && all[60] != null && all.Count == 70, "warm-up: no features before bar 60");
        Check(f[0] == 1 && Eq(f[1], 0.5) && Eq(f[2], 2.0 / 6) && Eq(f[3], 1.0 / 6) && Eq(f[5], 3.0), "bar 65: green, body 50 %, upper wick 33.3 %, lower 16.7 %, volume 3× the 20-bar average", string.Join(" ", f.Take(6).Select(v => v.ToString("0.###"))));
        double atr = (13 * 3.0 + Math.Max(6, Math.Max(Math.Abs(170 - 165), Math.Abs(164 - 165)))) / 14;   /* each earlier bar: high − low = 3 (true range 3) • bar 65: 6 */
        Check(Eq(f[4], 6 / atr) && Eq(f[9], (168 - b[50].Close) / atr) && Eq(f[10], (168 - b[5].Close) / atr), "range ÷ ATR(14), the 15- and 60-minute moves in ATR", f[4] + " " + f[9] + " " + f[10]);
        Check(f[15] == 66 && f[16] == 0 && Eq(f[17], 10 + 36 / 60.0) && f[18] == 2 && f[19] == 0, "streak 66 green, no FVG, 10:36 NY, New York session, no news", f[15] + " " + f[16] + " " + f[17]);
        var b2 = b.Select(x => new KeystoneArcBar { Symbol = x.Symbol, Time = x.Time, Open = x.Open, High = x.High, Low = x.Low, Close = x.Close, Volume = x.Volume }).ToList(); b2[66] = new KeystoneArcBar { Symbol = "MNQ", Time = b2[66].Time, Open = 175, High = 190, Low = 174, Close = 189, Volume = 100 };
        var all2 = KeystoneEntryFeatures.All(b2); Check(all2[66][16] == 1 && all2[65].SequenceEqual(f), "a gap above bar 64's high at bar 66 = a fresh bullish FVG — and changing bar 66 never changes bar 65's features (no lookahead)");
        Check(Enumerable.Range(60, 10).All(i => KeystoneEntryFeatures.At(b, i).SequenceEqual(all[i])), "the features of bar i from bars ≤ i only = the full pass's");
        double mfe, mae; int w1 = KeystoneEntryFeatures.Outcome(b, 60, 1, 3, 10, out mfe, out mae); int w2 = KeystoneEntryFeatures.Outcome(b, 60, -1, 3, 10, out mfe, out mae);
        Check(w1 == 1 && w2 == 0 && KeystoneEntryFeatures.Outcome(b, 60, 1, 50, 5, out mfe, out mae) == -1, "the outcome walk: a long +3 first, a short −3 first, nothing within 5 bars = unknown");

        // walk-forward + calibration on a synthetic set with a KNOWN edge (the chance rises with feature 4), then one with NO edge
        var rnd = new Random(11); var rows = new List<KeystoneEntryRow>(); var rowsN = new List<KeystoneEntryRow>();
        for (int i = 0; i < 40000; i++)
        {
            var fz = new double[KeystoneEntryFeatures.N]; for (int k = 0; k < fz.Length; k++) fz[k] = rnd.NextDouble() * 2 - 1; double pTrue = 1 / (1 + Math.Exp(-(-0.5 + 1.6 * fz[4])));
            int sd = i % 2 == 0 ? 1 : -1; rows.Add(new KeystoneEntryRow { Time = t0.AddMinutes(i), Side = sd, Win = rnd.NextDouble() < pTrue ? 1 : 0, F = fz });
            rowsN.Add(new KeystoneEntryRow { Time = t0.AddMinutes(i), Side = sd, Win = rnd.NextDouble() < 0.38 ? 1 : 0, F = fz });
        }
        var m = KeystoneEntryAnalyzer.Fit(rows, 16.75, "MNQ");
        Check(m.Train == 28000 && m.Test == 12000 && m.SplitAt == t0.AddMinutes(28000), "walk-forward: trained on the earlier 70 % (28,000 rows), judged on the later 30 %", m.Train + " / " + m.Test + " · " + m.SplitAt);
        var cal = m.Calib.Where(c => c[0] == 1).ToList(); double worst = cal.Max(c => Math.Abs(c[2] - c[3]));
        Check(cal.Count == 10 && worst < 0.06 && m.TopEdge > 0.15 && !m.Verdict.StartsWith("⚠"), "known edge: calibrated (predicted vs realised within 6 points per decile), the top decile beats the base by > 15 points", "worst " + worst.ToString("0.000") + " · edge " + m.TopEdge.ToString("0.000") + " · " + m.Verdict);
        var mn = KeystoneEntryAnalyzer.Fit(rowsN, 16.75, "MNQ"); Check(mn.Verdict.StartsWith("⚠ NO REAL EDGE") && Math.Abs(mn.Base[0] - 0.38) < 0.02, "no edge: the model says so (top decile ≈ the base rate 38 %)", mn.Verdict);
        int strong; var hi = new double[KeystoneEntryFeatures.N]; hi[4] = 0.95; string sc = m.Score(hi, out strong);
        Check(System.Text.RegularExpressions.Regex.IsMatch(sc, @"^LONG \d+ % · SHORT \d+ % · base \d+ % · n = [\d,]+ similar$") && strong != 0, "the 🧠 ENTRY SCORE text + coloured when clearly above the base with enough samples", sc);
        string scN = mn.Score(hi, out strong); Check(scN.EndsWith("⚠ no real edge found") && strong == 0, "no edge: the score says so and is never coloured", scN);
        Check(m.Score(null, out strong).Contains("not enough bars"), "no features yet → it says so");
        var back = KeystoneEntryModel.FromJson(m.ToJson()); int st2; Check(back != null && back.Score(hi, out st2) == sc && back.Calib.Count == m.Calib.Count && back.Cells.Count == m.Cells.Count, "model.json round trip", back == null ? "null" : back.Score(hi, out st2));
        Check(KeystoneEntryAnalyzer.Extremes(m, true, 3).Count == 3 && KeystoneEntryAnalyzer.Extremes(m, true, 1)[0][0] == KeystoneEntryFeatures.Names[4], "best conditions: the bins of the feature that carries the edge come first", KeystoneEntryAnalyzer.Extremes(m, true, 1)[0][0]);
        var line = KeystoneEntryAnalyzer.TradeLine(t0, "MNQ", -1, 60, "💀 blown", -2010, 3.5, 16.75, f); var tr = KeystoneEntryAnalyzer.LoadTrades(KeystoneEntryAnalyzer.TradesHeader + "\n" + line);
        Check(tr.Count == 1 && tr[0].Side == -1 && tr[0].Win == 0 && tr[0].Mine && Eq(tr[0].F[5], 3.0, 1e-4), "your trades: saved with their entry context and read back");
        var boot = KeystoneEntryAnalyzer.Bootstrap(b, 3, 5); Check(boot.Count > 0 && boot.All(r => r.F != null && (r.Win == 0 || r.Win == 1)), "bootstrap: every minute after the warm-up, both sides, known outcomes only", boot.Count.ToString());
        Console.WriteLine(failures == 0 ? "ALL ENTRY ANALYZER TESTS PASSED" : "ENTRY ANALYZER TESTS: " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
