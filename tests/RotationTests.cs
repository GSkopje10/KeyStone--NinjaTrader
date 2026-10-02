// ROTATION TESTER: MNQ + MGC together, combined target / stop / profit lock, accounts in turn, daily locks, evaluations.
// Run: tools/compile_engine.sh tests/.build/rot.exe tests/RotationTests.cs && mono tests/.build/rot.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class RotationTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.01; }
    static KeystoneHelixMinute M(DateTime t, double nq, double nqH, double nqL, double nqC, double gc, double gcH, double gcL, double gcC) { return new KeystoneHelixMinute { Time = t, MnqO = nq, MnqH = nqH, MnqL = nqL, MnqC = nqC, MgcO = gc, MgcH = gcH, MgcL = gcL, MgcC = gcC, HasMnq = true, HasMgc = true }; }
    static List<KeystoneHelixMinute> Flat(DateTime day, Dictionary<int, KeystoneHelixMinute> o)
    {
        var list = new List<KeystoneHelixMinute>();
        for (int m = 9 * 60 + 25; m <= 16 * 60; m++) { var t = day.AddMinutes(m); KeystoneHelixMinute x; list.Add(o.TryGetValue(m, out x) ? x : M(t, 20000, 20000.25, 19999.75, 20000, 3000, 3000.1, 2999.9, 3000)); }
        return list;
    }
    public static int Main()
    {
        var day = new DateTime(2025, 6, 3);
        // 20 MNQ ($40/pt) + 20 MGC ($200/pt). 09:31 bar: entry at the open, then MGC +5.5 pts → +$1,100 combined → TARGET +$1,000
        var o = new Dictionary<int, KeystoneHelixMinute>();
        o[9 * 60 + 33] = M(day.AddMinutes(9 * 60 + 33), 20000, 20001, 19999, 20000, 3000, 3005.5, 2999.9, 3005);
        var cfg = new KeystoneRotationConfig { Accounts = 1, LockTiers = "", CommissionPerSide = 0, SlippageTicks = 0, DailyTarget = 1000, MaxLossesPerDay = 2 };
        var r = KeystoneRotation.Run(Flat(day, o), cfg);
        Check(r.Rotations == 1 && Eq(r.Trades[0].Pnl, 1000) && r.Trades[0].Reason == "TARGET", "BUY BUY: MGC +5.5 → combined +$1,000 target; daily target reached → account locked for the day", r.Rotations + " " + (r.Trades.Count > 0 ? r.Trades[0].Reason + " " + r.Trades[0].Pnl : ""));
        // both legs down in one minute: MNQ −10 (−$400) + MGC −1 (−$200) → −$600 ≤ −$500 → STOP at −500 (stop before target)
        var o2 = new Dictionary<int, KeystoneHelixMinute>();
        o2[9 * 60 + 33] = M(day.AddMinutes(9 * 60 + 33), 20000, 20030, 19990, 20000, 3000, 3005, 2999, 3000);
        var r2 = KeystoneRotation.Run(Flat(day, o2), cfg);
        Check(r2.Trades.Count >= 1 && Eq(r2.Trades[0].Pnl, -500) && r2.Trades[0].Reason == "STOP", "a minute touching both −$600 and +$2,200 counts as the STOP (worst first)", r2.Trades.Count > 0 ? r2.Trades[0].Reason + " " + r2.Trades[0].Pnl : "none");
        Check(r2.Trades.Count == 2 && r2.Trades.All(t => t.Reason == "STOP" || t.Reason == "SESSION END"), "after 2 losses the account is locked (only 2 rotations)", r2.Trades.Count + " " + string.Join(",", r2.Trades.Select(t => t.Reason)));
        // profit lock: peak +$600 (tier 500:150), then back below +$150 → LOCK exit +$150
        var o3 = new Dictionary<int, KeystoneHelixMinute>();
        o3[9 * 60 + 33] = M(day.AddMinutes(9 * 60 + 33), 20000, 20000, 20000, 20000, 3000, 3003, 3000, 3003);    // +$600 close
        o3[9 * 60 + 34] = M(day.AddMinutes(9 * 60 + 34), 20000, 20000, 20000, 20000, 3003, 3003, 3000.5, 3000.5);  // back to +$100
        var lockCfg = cfg.Copy(); lockCfg.LockTiers = "500:150,700:350,900:750";
        var r3 = KeystoneRotation.Run(Flat(day, o3), lockCfg);
        Check(r3.Trades.Count >= 1 && r3.Trades[0].Reason == "LOCK" && Eq(r3.Trades[0].Pnl, 150), "profit lock: peak +$600 then back down → out at +$150", r3.Trades.Count > 0 ? r3.Trades[0].Reason + " " + r3.Trades[0].Pnl : "none");
        // SELL SELL is the mirror
        var sellCfg = cfg.Copy(); sellCfg.Direction = "SELL";
        var r4 = KeystoneRotation.Run(Flat(day, o), sellCfg);
        Check(r4.Trades.Count >= 1 && r4.Trades[0].Reason == "STOP" && Eq(r4.Trades[0].Pnl, -500), "SELL SELL on the same up move → STOP");
        // costs: 40 contracts × 2 × $0.62 = $49.60 per rotation; slippage 1 tick on stops = 20×$0.50 + 20×$1 = $30
        var costCfg = cfg.Copy(); costCfg.CommissionPerSide = 0.62; costCfg.SlippageTicks = 1;
        var r5 = KeystoneRotation.Run(Flat(day, o2), costCfg);
        Check(Eq(r5.Trades[0].Pnl, -500 - 30 - 49.6), "costs: stop −$500 − $30 slippage − $49.60 commission", r5.Trades[0].Pnl.ToString());
        // random walk 2 years, 5 accounts as evaluations: runs, buys, records years; the optimizer ranks
        var rng = new Random(3); var all = new List<KeystoneHelixMinute>();
        for (var d = new DateTime(2024, 1, 2); d < new DateTime(2025, 6, 30); d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
            double nq = 20000, gc = 3000;
            for (int m = 7 * 60 + 1; m <= 16 * 60; m++) { double a = nq, b = gc; double z = rng.NextDouble() - 0.5; nq += z * 12 + (rng.NextDouble() - 0.5) * 6; gc += z * 2 + (rng.NextDouble() - 0.5) * 1; all.Add(M(d.AddMinutes(m), a, Math.Max(a, nq) + 1, Math.Min(a, nq) - 1, nq, b, Math.Max(b, gc) + 0.2, Math.Min(b, gc) - 0.2, gc)); }
        }
        var big = KeystoneRotation.Run(all, new KeystoneRotationConfig { Accounts = 5, Direction = "RANDOM" });
        Console.WriteLine("      " + KeystoneRotation.Verdict(big));
        Check(big.Bought >= 5 && big.Rotations > 100 && big.ByYear.Count == 2 && (big.Passed + big.Blown) > 0, "2 years: accounts bought, rotations, passes or blowups, per year", big.Bought + " bought • " + big.Passed + " passed • " + big.Blown + " blown");
        var together = KeystoneRotation.Together(all, 20, 20);
        Check(together.Count >= 10 && together.All(t => t.Length == 5), "MNQ + MGC together table by 30-minute slot", together.Count.ToString());
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var opt = KeystoneRotation.Optimize(all.Where(x => x.Time.Year == 2024 && x.Time.Month <= 6).ToList(), new KeystoneRotationConfig { Accounts = 5 }, null);
        Console.WriteLine("      optimizer " + opt.Count + " configs in " + sw.ElapsedMilliseconds + " ms • #1 " + opt[0].Label + " • " + KeystoneRotation.Verdict(opt[0]));
        Check(opt.Count == 5 * 5 * 3 * 3 * 3 * 3, "optimizer tries 2,025 combinations");
        Console.WriteLine(failures == 0 ? "ALL ROTATION TESTS PASSED" : failures + " ROTATION TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
