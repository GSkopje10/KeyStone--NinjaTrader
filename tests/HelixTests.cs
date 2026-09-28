// HELIX ROTATION engine tests on synthetic MNQ + MGC 1-minute sessions (close-stamped, NY time).
using System; using System.Collections.Generic; using System.Linq; using NinjaTrader.NinjaScript;
public static class HelixTests
{
    static int failed;
    static void Check(bool ok, string name) { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name); if (!ok) failed++; }

    // One session 09:00–16:00 of 1M bars. step(i) gives the price change of minute i (0 = 09:01).
    static void AddDay(List<KeystoneArcBar> mnq, List<KeystoneArcBar> mgc, DateTime day, Func<int, double> mnqStep, Func<int, double> mgcStep, double wick = 0)
    {
        double pm = mnq.Count == 0 ? 20000 : mnq[mnq.Count - 1].Close, pg = mgc.Count == 0 ? 2000 : mgc[mgc.Count - 1].Close;
        for (int i = 0; i < 420; i++)
        {
            DateTime t = day.Date.AddHours(9).AddMinutes(i + 1);
            double om = pm, og = pg; pm += mnqStep(i); pg += mgcStep(i);
            mnq.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = om, Close = pm, High = Math.Max(om, pm) + wick, Low = Math.Min(om, pm) - wick });
            mgc.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = og, Close = pg, High = Math.Max(og, pg) + wick * 0.1, Low = Math.Min(og, pg) - wick * 0.1 });
        }
    }

    static KeystoneHelixConfig Plain(int accounts)
    {
        var c = KeystoneHelix.ManusPreset(new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), accounts);
        c.PauseMinutes = 2; return c;
    }

    static List<DateTime> Weekdays(DateTime from, int n) { var l = new List<DateTime>(); for (var d = from; l.Count < n; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) l.Add(d); return l; }

    public static int Main()
    {
        // 1. alignment
        var am = new List<KeystoneArcBar> { new KeystoneArcBar { Time = new DateTime(2025, 1, 2, 9, 31, 0), Open = 1, High = 2, Low = 0.5, Close = 1.5 }, new KeystoneArcBar { Time = new DateTime(2025, 1, 2, 9, 33, 0), Open = 1.5, High = 2, Low = 1, Close = 1.8 } };
        var ag = new List<KeystoneArcBar> { new KeystoneArcBar { Time = new DateTime(2025, 1, 2, 9, 31, 0), Open = 10, High = 11, Low = 9, Close = 10.5 }, new KeystoneArcBar { Time = new DateTime(2025, 1, 2, 9, 32, 0), Open = 10.5, High = 11, Low = 10, Close = 10.7 } };
        var al = KeystoneHelix.Align(am, ag);
        Check(al.Count == 3 && al[1].HasMnq == false && al[1].MnqC == 1.5 && al[1].HasMgc && al[2].HasMgc == false && al[2].MgcO == 10.7, "align: one timeline, a missing leg carries its last close and is flagged");

        // 2. steady uptrend, BUY + BUY, first win locks: every account wins once a day at exactly +1000
        var mnq = new List<KeystoneArcBar>(); var mgc = new List<KeystoneArcBar>();
        var days = Weekdays(new DateTime(2025, 1, 6), 5);
        foreach (var d in days) AddDay(mnq, mgc, d, i => 1.0, i => 0.1);
        var minutes = KeystoneHelix.Align(mnq, mgc);
        var cfg = Plain(3);
        var r = KeystoneHelix.Run(minutes, cfg);
        Check(r.Days.Count == 5, "5 sessions found in the 09:30–16:00 window (" + r.Days.Count + ")");
        Check(r.Rotations.Count == 15 && r.Rotations.All(x => x.Reason == "TARGET" && Math.Abs(x.Net - 1000) < 0.01), "uptrend: 3 accounts × 5 days, each basket closes at exactly +$1,000 (" + r.Rotations.Count + ")");
        Check(r.Rotations.All(x => x.After == "LOCKED • FIRST WIN") && r.Days.All(d => d.EndReason == "ALL ACCOUNTS DONE"), "first win locks the account; the day ends when every account is locked");
        var first = r.Rotations[0];
        Check(first.EntryTime == days[0].AddHours(9).AddMinutes(30) && Math.Abs(first.MnqEntry - (20000 + 30)) < 1e-9, "first basket enters at 09:30 at the open of the 09:31 bar");
        Check(Math.Abs(first.MnqPnl + first.MgcPnl - first.Gross) < 0.01, "MNQ leg + MGC leg = basket gross");
        var second = r.Rotations[1];
        Check(second.EntryTime >= first.ExitTime.AddMinutes(2), "pause: the next account enters 2 minutes after the previous basket closed");
        Check(second.Account.StartsWith("A02") && r.Rotations[2].Account.StartsWith("A03") && r.Rotations[3].Account.StartsWith("A01"), "rotation order A01 → A02 → A03, and every day starts again at A01");
        // payouts: 5 × 1000 = 5,000 profit at the 5th session → 50% of (5000 + 2000 cushion) capped at 2,000 → 1,600 cash
        Check(r.Payouts.Count == 3 && r.Payouts.All(p => p.Gross == 2000 && p.Cash == 1600 && p.BalanceAfter == 3000), "payout review every 5 sessions: $2,000 gross, $1,600 cash, $3,000 kept (" + r.Payouts.Count + ")");
        Check(Math.Abs(r.ExpenseTotal - 1500) < 0.01 && Math.Abs(r.NetCash - (4800 - 1500)) < 0.01, "3 direct accounts × $500; net cash = payouts − purchases");

        // 3. steady downtrend, BUY + BUY: 4 stops of −500 liquidate a $2,000 static cushion; replaced at the next review
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        days = Weekdays(new DateTime(2025, 2, 3), 10);
        foreach (var d in days) AddDay(mnq, mgc, d, i => -1.0, i => -0.1);
        minutes = KeystoneHelix.Align(mnq, mgc);
        cfg = Plain(2);
        r = KeystoneHelix.Run(minutes, cfg);
        var firstLife = r.Lives.First(l => l.Id == "A01-I01");
        Check(firstLife.Status == "BLOWN" && firstLife.Losses == 4 && firstLife.EndDate == days[0], "downtrend: A01 is liquidated after 4 × −$500 on day 1 (" + firstLife.Losses + ")");
        Check(r.Rotations.Where(x => x.Day == days[0]).All(x => x.Reason == "STOP" || x.Reason == "LIQUIDATED") && r.Rotations.Count(x => x.Day == days[0]) == 8, "day 1: 2 accounts × 4 stops");
        Check(r.Lives.Count(l => l.Slot == 1) == 3 && r.Lives.Where(l => l.Slot == 1 && l.Instance == 2).All(l => l.Bought == days[4]), "replacement bought at the next review (session 5) and traded from session 6");
        Check(!r.Rotations.Any(x => x.Day > days[0] && x.Day <= days[4]), "no trading while every account waits for its replacement");

        // 4. costs: commission $0.62/side/contract and 1 tick slippage per side
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        days = Weekdays(new DateTime(2025, 3, 3), 1);
        AddDay(mnq, mgc, days[0], i => 1.0, i => 0.1);
        cfg = Plain(1); cfg.CommissionPerSide = 0.62; cfg.SlippageTicks = 1;
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        var c0 = r.Rotations[0];
        Check(Math.Abs(c0.Commission - 24.8) < 1e-6 && Math.Abs(c0.Gross - (1000 - 15)) < 1e-6 && Math.Abs(c0.Net - (1000 - 15 - 24.8)) < 1e-6, "costs: commission $24.80, exit slippage $15, entry slippage inside the fill (" + c0.Gross.ToString("0.00") + ")");
        Check(Math.Abs(c0.MnqEntry - (20030 + 0.25)) < 1e-9 && Math.Abs(c0.MgcEntry - (2003 + 0.1)) < 1e-9, "entry slippage: 1 tick worse on each leg");

        // 5. strict fills: one minute that touches both target and stop counts as the stop
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        days = Weekdays(new DateTime(2025, 3, 10), 1);
        AddDay(mnq, mgc, days[0], i => i == 40 ? 1.0 : 0.0, i => 0.0, 0);
        var spike = mnq[40]; spike.High = spike.Open + 60; spike.Low = spike.Open - 60;   // ±$1,200 on 10 MNQ
        cfg = Plain(1); cfg.UseMgc = 0; cfg.Funded.MnqContracts = 10;
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        Check(r.Rotations[0].Reason == "STOP" && r.Rotations[0].Ambiguous && Math.Abs(r.Rotations[0].Net + 500) < 1e-6, "STRICT: a minute touching both counts as the stop (flagged ambiguous)");
        cfg.ExitModel = "NEUTRAL";
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        Check(r.Rotations[0].Reason != "STOP" || r.Rotations[0].ExitTime > days[0].AddHours(9).AddMinutes(41), "NEUTRAL: exits are judged on minute closes, not on wicks");

        // 6. session end closes an open basket at the 16:00 close
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        days = Weekdays(new DateTime(2025, 3, 17), 1);
        AddDay(mnq, mgc, days[0], i => 0.0, i => 0.0);
        cfg = Plain(1);
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        Check(r.Rotations.Count == 1 && r.Rotations[0].Reason == "SESSION END" && r.Rotations[0].ExitTime == days[0].AddHours(16), "flat market: the basket is closed at 16:00");

        // 7. evaluation: target 3,000 with 2 days minimum and 50% consistency, then funded
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        days = Weekdays(new DateTime(2025, 4, 7), 8);
        foreach (var d in days) AddDay(mnq, mgc, d, i => 1.0, i => 0.1);
        cfg = Plain(1); cfg.StartMode = "EVAL"; cfg.EvalCost = 100; cfg.ActivationCost = 80; cfg.EvalTarget = 3000; cfg.EvalMinDays = 2; cfg.EvalConsistency = 50;
        cfg.Eval.LockMode = "DAILY_TARGET"; cfg.Eval.DailyTarget = 1500; cfg.Eval.RotationTarget = 1500;
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        var ev = r.Lives[0];
        Check(ev.PassDate == days[1] && ev.FundedDate == days[2], "evaluation: +1,500 × 2 days passes on day 2 (consistency 50%), funded from day 3");
        Check(r.Expenses.Count == 2 && r.Expenses.Sum(e => e.Amount) == 180 && r.Rotations.Where(x => x.Day >= days[2]).All(x => x.Stage == "FUNDED"), "evaluation + activation costs recorded; funded rules used after the pass");

        // 8. trailing end-of-day drawdown moves up with the balance and stops at start + 100
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        days = Weekdays(new DateTime(2025, 5, 5), 4);
        AddDay(mnq, mgc, days[0], i => 1.0, i => 0.1); AddDay(mnq, mgc, days[1], i => 1.0, i => 0.1); AddDay(mnq, mgc, days[2], i => 1.0, i => 0.1); AddDay(mnq, mgc, days[3], i => -1.0, i => -0.1);
        cfg = Plain(1); cfg.FundedDrawdown = "TRAILING_EOD"; cfg.TrailStopAt = 100; cfg.PayoutEverySessions = 50;
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        var lastDay = r.Rotations.Where(x => x.Day == days[3]).ToList();
        Check(lastDay.Count == 6 && lastDay.Last().Reason == "LIQUIDATED" && Math.Abs(lastDay.Last().BalanceAfter - 100) < 0.01, "trailing EOD: +3,000 then losses liquidate at start + $100 (" + lastDay.Count + " baskets)");

        // 8b. a day where MGC is missing is not traded; the weekday filter skips days
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        days = Weekdays(new DateTime(2025, 5, 12), 5);
        foreach (var d in days) AddDay(mnq, mgc, d, i => 1.0, i => 0.1);
        mgc.RemoveAll(b => b.Time.Date == days[2] && b.Time.Hour >= 10);           // Wednesday: MGC stops at 10:00
        cfg = Plain(2);
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        Check(r.Days.Count == 4 && !r.Rotations.Any(x => x.Day == days[2]) && r.SkippedDays.Count == 1 && r.SkippedDays[0].Contains("MGC"), "a day with missing MGC bars is listed and not traded (" + string.Join(" | ", r.SkippedDays) + ")");
        cfg.Weekdays = "135";
        r = KeystoneHelix.Run(KeystoneHelix.Align(mnq, mgc), cfg);
        Check(r.Days.All(d => d.Day.DayOfWeek == DayOfWeek.Monday || d.Day.DayOfWeek == DayOfWeek.Friday) && r.Days.Count == 2, "weekday filter: only Monday and Friday traded (Wednesday has no MGC)");

        // 9. proof tests and the Manus reference
        mnq = new List<KeystoneArcBar>(); mgc = new List<KeystoneArcBar>();
        var rnd = new Random(3);
        days = Weekdays(new DateTime(2025, 6, 2), 30);
        foreach (var d in days) AddDay(mnq, mgc, d, i => (rnd.NextDouble() - 0.5) * 8, i => (rnd.NextDouble() - 0.5) * 0.8, 1);
        minutes = KeystoneHelix.Align(mnq, mgc);
        cfg = Plain(10); cfg.CommissionPerSide = 0.62; cfg.SlippageTicks = 1;
        var baseRun = KeystoneHelix.Run(minutes, cfg);
        var proof = KeystoneHelix.Proof(minutes, cfg, baseRun, 5, null);
        Check(proof.Count(p => p.IsBase) == 1 && proof.Any(p => p.Group == "RANDOM" && p.Label.StartsWith("RANDOM DIRECTIONS")) && proof.Any(p => p.Label == "SELL MNQ + SELL MGC") && proof.Any(p => p.Group == "START TIME") && proof.Any(p => p.Group == "PAUSE"), "proof tests: base, directions, random baseline, start times, pauses (" + proof.Count + " rows)");
        var buckets = KeystoneHelix.Buckets(baseRun);
        Check(buckets.Any(b => b.Group == "ENTRY HOUR") && buckets.All(b => b.Count == b.EarlyCount + b.LateCount), "setup finder buckets split into first 2/3 and last 1/3");
        var months = KeystoneHelix.Months(baseRun);
        Check(months.Sum(m => m.Purchases) == baseRun.Lives.Count && Math.Abs(months.Sum(m => m.TradingNet) - baseRun.TradingNet) < 0.01 && Math.Abs(months.Sum(m => m.NetCash) - baseRun.NetCash) < 0.01, "months add up to the totals");
        var manus = KeystoneHelix.ParseManus(KeystoneHelixManusReference.Pool10);
        Check(manus.Count == 564 && manus[0].Day == new DateTime(2024, 1, 2) && manus[0].Pnl == -2565, "Manus 10-account workbook: 564 sessions parsed");
        Check(Math.Abs(manus.Sum(m => m.Pnl) - 638355) < 1, "Manus total gross trading P/L = $638,355 (as in the workbook)");
        double reach, pays; double ev0 = KeystoneHelix.ZeroEdgeValuePerAccount(KeystoneHelix.ManusPreset(DateTime.MinValue, DateTime.MaxValue, 10), out reach, out pays);
        Check(Math.Abs(reach - 0.5) < 1e-9 && Math.Abs(ev0 - (1 * 2000 * 0.8 - 500)) < 1e-6, "zero-edge math: 50% reach payout first, ≈ +$1,100 per $500 account");

        Console.WriteLine(failed == 0 ? "ALL HELIX TESTS PASSED" : failed + " HELIX TEST(S) FAILED");
        return failed == 0 ? 0 : 1;
    }
}
