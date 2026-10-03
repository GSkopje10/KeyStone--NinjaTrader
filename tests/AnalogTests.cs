// PATTERN ENGINE: fingerprints use only the past, outcomes, earlier-days-only queries, and the walk-forward test finds a planted
// pattern but not an edge in a pure random walk.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class AnalogTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    static List<KeystoneArcBar> Session(DateTime day, Random rnd, bool plant)
    {
        var list = new List<KeystoneArcBar>(); double p = 1000; var t = day.AddHours(9);
        int n = 360; var drift = new double[n];
        if (plant) for (int s = 40; s + 45 < n; s += 50) { for (int k = 0; k < 8; k++) drift[s + k] = -1.2; for (int k = 8; k < 40; k++) drift[s + k] = 0.9; }   // a dip, then a rise
        for (int i = 0; i < n; i++)
        {
            double o = p; p += drift[i] + (rnd.NextDouble() - 0.5) * 2.0;
            double h = Math.Max(o, p) + rnd.NextDouble() * 0.6, l = Math.Min(o, p) - rnd.NextDouble() * 0.6;
            list.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t.AddMinutes(i + 1), Open = o, High = h, Low = l, Close = p });
        }
        return list;
    }
    static KeystoneAnalogIndex Build(bool plant, int days, int seed)
    {
        var rnd = new Random(seed); var idx = new KeystoneAnalogIndex(); var d = new DateTime(2024, 1, 1);
        for (int k = 0; k < days; k++) { while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1); KeystoneAnalog.AddSession(idx, Session(d, rnd, plant), d); d = d.AddDays(1); }
        return idx;
    }

    public static int Main()
    {
        var rnd = new Random(1); var s = Session(new DateTime(2024, 3, 4), rnd, false);
        var f1 = KeystoneAnalog.Fingerprint(s, 100); var s2 = s.Take(101).ToList(); var f2 = KeystoneAnalog.Fingerprint(s2, 100);
        Check(f1 != null && f1.Length == KeystoneAnalog.Dim && f1.SequenceEqual(f2), "the fingerprint at a minute uses only bars up to that minute (cutting the future changes nothing)");
        Check(KeystoneAnalog.Fingerprint(s, 10) == null, "no fingerprint in the first 30 minutes");
        var up = new List<KeystoneArcBar>(); for (int i = 0; i < 40; i++) up.Add(new KeystoneArcBar { Time = new DateTime(2024, 1, 2, 10, 0, 0).AddMinutes(i), Open = 100 + i, High = 100.5 + i, Low = 99.5 + i, Close = 100 + i });
        double end; int o = KeystoneAnalog.Outcome(up, 5, 3, out end);
        Check(o == 1 && end == 1, "a rising market: +3 comes first");
        var both = new List<KeystoneArcBar> { up[0], new KeystoneArcBar { Time = up[0].Time.AddMinutes(1), Open = 100, High = 110, Low = 90, Close = 100 } };
        Check(KeystoneAnalog.Outcome(both, 0, 3, out end) == 0, "a minute touching both barriers counts as neither");

        var planted = Build(true, 140, 7);
        var q = KeystoneAnalog.Query(planted, KeystoneAnalog.Fingerprint(Session(new DateTime(2024, 9, 2), new Random(3), true), 48), new DateTime(2024, 9, 2, 9, 49, 0), KeystoneAnalog.DayKeyOf(new DateTime(2024, 9, 2)), 50, 30);
        Check(q.Neighbors == 50 && q.Pool > q.Neighbors, "a query finds the 50 nearest earlier moments at that time of day", q.Neighbors + " / pool " + q.Pool);
        var early = KeystoneAnalog.Query(planted, KeystoneAnalog.Fingerprint(Session(new DateTime(2024, 1, 2), new Random(3), true), 48), new DateTime(2024, 1, 2, 9, 49, 0), KeystoneAnalog.DayKeyOf(new DateTime(2024, 1, 2)), 50, 30);
        int key2 = KeystoneAnalog.DayKeyOf(new DateTime(2024, 1, 2)), tod2 = 9 * 60 + 49 + 360;
        int expect = Enumerable.Range(0, planted.Count).Count(i => planted.DayKey[i] < key2 && Math.Abs(planted.Tod[i] - tod2) <= 30);
        Check(early.Pool == expect && expect > 0 && planted.DayKey.Where((x, i) => planted.DayKey[i] < key2).All(x => x == KeystoneAnalog.DayKeyOf(new DateTime(2024, 1, 1))), "a query only sees days before its own (no look-ahead)", early.Pool + " vs " + expect);
        var rp = KeystoneAnalog.WalkForward(planted, 4, 0.6, 60, 30, 40, 0, 1440, null);
        Console.WriteLine("      planted: " + rp.Calls + " calls • hit " + (100 * rp.Hit).ToString("0.0") + "% vs baseline " + (100 * rp.BaseHit).ToString("0.0") + "% • " + (rp.R / Math.Max(1, rp.Calls)).ToString("+0.00;-0.00") + "R • " + rp.Verdict);
        Check(rp.Calls > 100 && rp.Hit > rp.BaseHit + 0.05 && rp.Verdict.StartsWith("PROMISING"), "walk forward finds a real (planted) pattern");
        var random = Build(false, 140, 11);
        var rr = KeystoneAnalog.WalkForward(random, 4, 0.6, 60, 30, 40, 0, 1440, null);
        Console.WriteLine("      random walk: " + rr.Calls + " calls • hit " + (100 * rr.Hit).ToString("0.0") + "% vs baseline " + (100 * rr.BaseHit).ToString("0.0") + "% • " + rr.Verdict);
        Check(!rr.Verdict.StartsWith("PROMISING") && Math.Abs(rr.Hit - rr.BaseHit) < 0.05, "no edge is claimed on a pure random walk");
        Console.WriteLine(failures == 0 ? "ALL PATTERN ENGINE TESTS PASSED" : failures + " PATTERN ENGINE TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
