// SETUP COACH: walk an entry to its stop / best move, learn the best target in R, contracts for a $ risk.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class CoachTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static KeystoneArcBar B(int m, double h, double l, double c) { return new KeystoneArcBar { Symbol = "MNQ", Time = new DateTime(2026, 3, 10, 9, 30, 0).AddMinutes(m), Open = c, High = h, Low = l, Close = c }; }
    static KeystoneReplaySignal Buy(int m) { return new KeystoneReplaySignal { Time = new DateTime(2026, 3, 10, 9, 30, 0).AddMinutes(m), Dir = 1, Entry = 100, Stop = 90 }; }

    public static int Main()
    {
        // entry minute 1 (bar 2 is the entry bar and is skipped); then +15 (1.5R), then the stop
        var bars = new List<KeystoneArcBar> { B(1, 101, 99, 100), B(2, 130, 80, 100), B(3, 115, 98, 110), B(4, 112, 89, 92) };
        var wk = KeystoneSetupCoach.Walk(Buy(1), bars);
        Check(wk != null && Math.Abs(wk.Item1 - 1.5) < 1e-9 && wk.Item3, "best +1.5R before the stop; the entry minute is not counted", wk == null ? "null" : wk.Item1 + " " + wk.Item3);
        Check(KeystoneSetupCoach.ResultR(wk, 1) == 1 && KeystoneSetupCoach.ResultR(wk, 2) == -1, "target 1R = +1, target 2R = stopped −1");
        var both = new List<KeystoneArcBar> { B(3, 125, 85, 100) };
        var wb = KeystoneSetupCoach.Walk(Buy(1), both);
        Check(wb.Item3 && wb.Item1 == 0, "a minute touching target and stop counts as the stop");
        var open = new List<KeystoneArcBar> { B(3, 104, 98, 103) };
        var wo = KeystoneSetupCoach.Walk(Buy(1), open);
        Check(!wo.Item3 && Math.Abs(wo.Item2 - 0.3) < 1e-9 && KeystoneSetupCoach.ResultR(wo, 1) == wo.Item2, "never stopped, target missed → the session-end R (+0.3)");
        // ten days: 6 reach 2R, 4 stop at once → 1R: 0.6−0.4 = +0.2 ; 2R: 1.2−0.4 = +0.8 ; 3R: −1 for all → best 2R
        var days = new List<Tuple<List<KeystoneReplaySignal>, List<KeystoneArcBar>>>();
        for (int i = 0; i < 10; i++) days.Add(Tuple.Create(new List<KeystoneReplaySignal> { Buy(1) }, i < 6 ? new List<KeystoneArcBar> { B(3, 121, 95, 120), B(4, 119, 89, 90) } : new List<KeystoneArcBar> { B(3, 101, 89, 90) }));
        var rep = KeystoneSetupCoach.Learn(days);
        Check(rep.Ready && rep.BestR == 2 && Math.Abs(rep.Expectancy - 0.8) < 1e-9 && Math.Abs(rep.HitRate - 0.6) < 1e-9, "learns the best target: 2R, 60% reached first, +0.8R a trade", rep.BestR + " " + rep.Expectancy + " " + rep.HitRate);
        Check(!KeystoneSetupCoach.Learn(days.Take(3)).Ready, "fewer than " + KeystoneSetupCoach.MinSignals + " entries = not ready");
        Check(KeystoneSetupCoach.Contracts(200, 10, 2, 20) == 10 && KeystoneSetupCoach.Contracts(200, 10, 2, 5) == 5 && KeystoneSetupCoach.Contracts(5, 10, 2, 20) == 1, "contracts = $ risk ÷ (stop pts × point value), within 1 … max");
        Console.WriteLine(failures == 0 ? "ALL COACH TESTS PASSED" : failures + " COACH TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
