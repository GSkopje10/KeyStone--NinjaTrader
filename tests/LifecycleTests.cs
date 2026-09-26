// Account lifecycle tests (evaluation, funded, payouts, replacement, costs, firm cap).
// Run with: tools/compile_engine.sh tests/.build/life.exe tests/LifecycleSnapshot.cs tests/LifecycleTests.cs && mono tests/.build/life.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class LifecycleTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }

    static List<KeystoneArcEvent> Nights(params double[] pnl)
    {
        var list = new List<KeystoneArcEvent>(); DateTime day = new DateTime(2025, 1, 5); int i = 0;
        for (int d = 0; i < pnl.Length; d++)
        {
            DateTime s = day.AddDays(d);
            if (s.DayOfWeek == DayOfWeek.Friday || s.DayOfWeek == DayOfWeek.Saturday) continue;
            DateTime t = s.AddHours(18).AddMinutes(1);
            list.Add(new KeystoneArcEvent { Id = "N" + i, Symbol = "MNQ", SetupClass = "ASIA75", ReferenceTime = s.AddHours(18), TriggerTime = t, EntryTime = t, ExitTime = t.AddHours(2), Outcome = pnl[i] > 0 ? "WIN" : "LOSS", GrossPnl = pnl[i], Quantity = 1, ReviewState = "ACCEPTED" });
            i++;
        }
        return list;
    }

    static double[] Repeat(double v, int n) { return Enumerable.Repeat(v, n).ToArray(); }

    public static int Main()
    {
        // 1. Asian evaluation: $350 nights must be able to pass ($3,000 target, 2 qualifying days ≥ $150).
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 2;
            var acc = KeystoneArcEngine.SimulatePool(Nights(Repeat(350, 12)), cfg);
            Check(acc.All(a => a.EvaluationPasses == 1 && a.FirstEvaluationPassDate != DateTime.MinValue), "Asian evaluation passes on $350 nights (was blocked by the BH $1,500 day rule)", acc[0].LastState);
            var pass = acc[0].DayHistory.FirstOrDefault(d => d.EvaluationPassesAfter == 1);
            Check(pass != null && pass.EvaluationBalanceAfter >= 3000, "pass happens once the $3,000 evaluation target is reached", pass == null ? "none" : pass.EvaluationBalanceAfter.ToString());
            Check(acc[0].DayHistory.All(d => (d.StateAfterClose ?? "").IndexOf("DAILY PROFIT LOCK", StringComparison.OrdinalIgnoreCase) < 0 && (d.StateAfterClose ?? "").IndexOf("DAILY LOSS LOCK", StringComparison.OrdinalIgnoreCase) < 0), "Asian days are not labelled with BH daily profit/loss locks", "");
        }
        // 2. Evaluation → funded → payout with the payout rules ($4,000 balance, 5 qualifying days, withdraw $2,000, 80% share).
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 1; cfg.PayoutProfitSharePercent = 80;
            var acc = KeystoneArcEngine.SimulatePool(Nights(Repeat(400, 40)), cfg);
            var a = acc[0];
            Check(a.Payouts >= 1 && a.PayoutGrossWithdrawn == a.Payouts * 2000 && Math.Abs(a.PayoutCash - a.PayoutGrossWithdrawn * 0.8) < 0.01, "funded account pays $2,000 per payout, 80% share as cash", a.Payouts + " payouts, gross " + a.PayoutGrossWithdrawn + ", cash " + a.PayoutCash);
            Check(a.FirstPayoutDate > a.FirstEvaluationPassDate, "first payout comes after the evaluation pass", a.FirstEvaluationPassDate.ToShortDateString() + " → " + a.FirstPayoutDate.ToShortDateString());
        }
        // 3. Direct-funded start costs the account price.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 0, 1, 0); cfg.PoolSize = 10;
            var acc = KeystoneArcEngine.SimulatePool(Nights(Repeat(350, 5)), cfg);
            Check(Math.Abs(acc.Sum(a => a.EvaluationCost) - 1200) < 0.01, "10 direct-funded accounts cost 10 × $120 = $1,200 (was $0)", acc.Sum(a => a.EvaluationCost).ToString());
        }
        // 4. Direct-funded blowout → replaced by a NEW EVALUATION that must pass before payouts.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 0, 1, 0); cfg.PoolSize = 1;
            var pnl = new List<double>(); pnl.AddRange(Repeat(-600, 4)); pnl.AddRange(Repeat(400, 30));
            var a = KeystoneArcEngine.SimulatePool(Nights(pnl.ToArray()), cfg)[0];
            Check(a.FailedFunded == 1 && a.EvaluationPurchases == 2 && Math.Abs(a.EvaluationCost - 240) < 0.01, "blown direct-funded account buys a replacement evaluation (+$120)", "fails " + a.FailedFunded + ", buys " + a.EvaluationPurchases + ", cost " + a.EvaluationCost);
            Check(a.EvaluationPasses == 1, "the replacement evaluation has to pass (1 pass recorded)", a.EvaluationPasses.ToString());
            var blowDay = a.LastBlowoutDate; var passDay = a.FirstEvaluationPassDate;
            Check(passDay > blowDay && a.DayHistory.Where(d => d.Day > blowDay && d.Day < passDay).All(d => !d.FundedAfter), "between blowout and pass the slot is an evaluation, not funded", blowDay.ToShortDateString() + " / " + passDay.ToShortDateString());
        }
        // 5. Replacement OFF: blown slots end and nothing more is bought or traded.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 3; cfg.BlownAccountReplacement = 0;
            var pnl = new List<double>(); pnl.AddRange(Repeat(-600, 4)); pnl.AddRange(Repeat(400, 10));
            var acc = KeystoneArcEngine.SimulatePool(Nights(pnl.ToArray()), cfg);
            Check(acc.All(a => a.Blown && !a.ReplacementPending && a.EvaluationPurchases == 1 && a.Trades == 4), "no replacement: every slot ends after its blowout, cost stays 3 × $120", string.Join("; ", acc.Select(a => a.Trades + " trades, " + a.EvaluationPurchases + " buys")));
            // default (replacement on) keeps trading
            cfg.BlownAccountReplacement = 1;
            var acc2 = KeystoneArcEngine.SimulatePool(Nights(pnl.ToArray()), cfg);
            Check(acc2.All(a => a.EvaluationPurchases == 2 && a.Trades > 4), "replacement on: a new evaluation is bought and keeps trading", string.Join("; ", acc2.Select(a => a.Trades + " trades, " + a.EvaluationPurchases + " buys")));
        }
        // 6. Firm cap: 10 direct-funded accounts with max 5 funded per firm → two firms of 5.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 0, 1, 1); cfg.PoolSize = 10; cfg.MaxFundedPerFirm = 5; cfg.EvaluationSlotsPerFirm = 10;
            var acc = KeystoneArcEngine.SimulatePool(Nights(Repeat(350, 5)), cfg);
            Check(acc.GroupBy(a => a.PropFirmCode).Select(g => g.Count()).SequenceEqual(new[] { 5, 5 }) && acc.All(a => a.Funded), "direct-funded accounts fill firms up to the funded cap (P1 ×5, P2 ×5), all funded", string.Join(",", acc.Select(a => a.Name)));
            // evaluation-first: 10 evals in one firm, only 5 may be funded; the rest wait
            var cfgE = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 1); cfgE.PoolSize = 10; cfgE.MaxFundedPerFirm = 5; cfgE.EvaluationSlotsPerFirm = 10;
            var accE = KeystoneArcEngine.SimulatePool(Nights(Repeat(350, 12)), cfgE);
            Check(accE.Count(a => a.Funded) == 5 && accE.Count(a => a.FundedCapPending) == 5, "evaluation-first: 5 funded, 5 passed accounts wait at the firm cap", accE.Count(a => a.Funded) + " funded / " + accE.Count(a => a.FundedCapPending) + " waiting");
        }
        // 7. Non-consecutive qualifying days option.
        {
            var pnl = new double[] { 2000, -100, 2000, -100, 400 };
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 1; cfg.EvaluationDailyCreditCap = 5000;
            var strict = KeystoneArcEngine.SimulatePool(Nights(pnl), cfg)[0];
            cfg.EvalQualifyingDaysConsecutive = 0;
            var loose = KeystoneArcEngine.SimulatePool(Nights(pnl), cfg)[0];
            Check(strict.EvaluationPasses == 0 && loose.EvaluationPasses == 1, "consecutive rule on: no pass; off: pass once 2 qualifying days and $3,000 are reached", strict.EvaluationPasses + " / " + loose.EvaluationPasses);
        }
        // 8. Asian evaluation-stage settings: evaluation accounts use the evaluation cycle ledger,
        //    funded accounts the main one.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 1;
            var funded = Nights(Repeat(350, 20));
            var evalLedger = Nights(Repeat(1000, 20));
            var a = KeystoneArcEngine.SimulatePool(funded, cfg, evalLedger)[0];
            int evalDays = a.DayHistory.Count(d => !d.FundedAfter) + 1; // the pass day itself traded the evaluation ledger
            double expected = 0; int k = 0;
            foreach (var d in a.DayHistory) { expected += k < evalDays ? 1000 : 350; k++; }
            Check(a.EvaluationPasses == 1 && Math.Abs(a.TotalPnl - expected) < 0.01, "evaluation nights take the evaluation-stage cycle ($1,000), funded nights the main cycle ($350)", "total " + a.TotalPnl + " vs " + expected + ", eval days " + evalDays);
            var main = new KeystoneArcRunConfig { StrategyCode = "ASIAN75", Scope = "BOTH", AsianCycleTargetDollars = 350, AsianReversalLossDollars = 75, AsianEvalStageEnabled = 1, AsianEvalCycleTargetDollars = 600, AsianEvalReversalLossDollars = 100, AsianEvalMaxReversals = 2 };
            var ev = KeystoneArcEngine.AsianEvaluationStageConfig(main);
            Check(ev != null && ev.AsianCycleTargetDollars == 600 && ev.AsianReversalLossDollars == 100 && ev.AsianMnqMaxReversals == 2 && ev.AsianDailyLossLimitDollars == 400 && main.AsianCycleTargetDollars == 350,
                "evaluation-stage config: target $600, $100 legs, 2 reversals, auto loss 100×2×2=$400; main config unchanged", ev == null ? "null" : ev.AsianDailyLossLimitDollars.ToString());
        }
        // 9. Daily records show that day's trades / wins / losses, starting balance and purchases.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 1;
            var a = KeystoneArcEngine.SimulatePool(Nights(350, -600, -600, -600, -600, 350), cfg)[0];
            var d0 = a.DayHistory[0]; var d1 = a.DayHistory[1];
            Check(d0.TradesAfter == 1 && d0.WinsAfter == 1 && d1.LossesAfter == 1, "daily records count that day's trades and wins/losses (was always 0)", d0.TradesAfter + "/" + d0.WinsAfter + "/" + d1.LossesAfter);
            Check(Math.Abs(d0.CostDelta - 120) < 0.01 && Math.Abs(d1.BalanceBefore - 350) < 0.01, "day one shows the $120 purchase; day two starts from the day-one balance", d0.CostDelta + " / " + d1.BalanceBefore);
            var replacementDay = a.DayHistory.FirstOrDefault(d => d.EvaluationPurchaseDelta == 1 && d.Day > d0.Day);
            Check(replacementDay != null && Math.Abs(replacementDay.CostDelta - 120) < 0.01 && Math.Abs(replacementDay.BalanceBefore) < 0.01, "replacement day shows +$120 and starts at a $0 balance", replacementDay == null ? "none" : replacementDay.CostDelta + " / " + replacementDay.BalanceBefore);
        }
        // 10. Setups-only scoreboard: days, win days, month-by-month.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0);
            var ev = Nights(350, -600, 400, 350, -600, -600, 380);
            var days = KeystoneArcScoreboard.Days(ev, cfg);
            string rep = KeystoneArcScoreboard.Report(days, cfg);
            Console.WriteLine(rep.Substring(0, Math.Min(rep.Length, 900)));
            Check(days.Count == 7 && days.Count(d => d.Pnl > 0) == 4 && Math.Abs(days.Sum(d => d.Pnl) - ev.Sum(e => e.GrossPnl)) < 0.01, "scoreboard: one row per night, 4 win days, totals match", days.Count.ToString());
            Check(rep.Contains("MONTH BY MONTH") && rep.Contains("LONGEST LOSING STREAK 2 days"), "report has month-by-month and the longest losing streak (2 days)", "");
        }
        // 11. Minimum trading days: target met on day 2, minimal trades on days 3-4, pass on day 4.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 1; cfg.EvaluationDailyCreditCap = 5000; cfg.EvaluationMinTradingDays = 4;
            var a = KeystoneArcEngine.SimulatePool(Nights(Repeat(1600, 8)), cfg)[0];
            var passDay = a.DayHistory.FindIndex(d => d.EvaluationPassesAfter == 1);
            Check(passDay == 3, "passes on trading day 4 (target met day 2, then 2 minimal-trade days)", "pass index " + passDay);
            Check(a.DayHistory.Count > 3 && a.DayHistory[2].DayPnl == 0 && a.DayHistory[3].DayPnl == 0 && Math.Abs(a.DayHistory[3].EvaluationBalanceAfter - 3200) < 0.01, "minimal-trade days carry no strategy P/L (balance stays $3,200)", a.DayHistory.Count > 3 ? a.DayHistory[2].DayPnl + " / " + a.DayHistory[3].EvaluationBalanceAfter : "");
            cfg.EvaluationMinTradingDays = 0;
            var b = KeystoneArcEngine.SimulatePool(Nights(Repeat(1600, 8)), cfg)[0];
            Check(b.DayHistory.FindIndex(d => d.EvaluationPassesAfter == 1) == 1, "without the minimum it passes on day 2", "");
        }
        // 12. Pool insights: real blowup count, last payout, per-instrument split.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 0, 1, 0); cfg.PoolSize = 3;
            var ev = Nights(Repeat(900, 6).Concat(Repeat(-900, 6)).Concat(Repeat(900, 3)).ToArray());
            var acc = KeystoneArcEngine.SimulatePool(ev, cfg);
            var ins = KeystoneArcPoolInsights.Build(acc, ev, cfg);
            Check(ins.BlowupEvents == acc.Sum(a => a.FailedEvaluations + a.FailedFunded) && ins.BlowupEvents > 0, "blowup box counts every blowup event, not only ended slots", ins.BlowupEvents + " events, ended " + ins.SlotsEnded);
            Check(ins.MaxBlowupsPerAccount == ins.MinBlowupsPerAccount, "copy trading: every account has the same blowup count", ins.MinBlowupsPerAccount + "-" + ins.MaxBlowupsPerAccount);
            Check(Math.Abs(ins.Net - (acc.Sum(a => a.PayoutCash) - acc.Sum(a => a.EvaluationCost))) < 0.01, "net = cash after share − all costs", ins.Net.ToString());
            Check(ins.LastPayoutDate >= ins.FirstPayoutDate, "last payout date is on/after the first", ins.FirstPayoutDate.ToShortDateString() + " / " + ins.LastPayoutDate.ToShortDateString());
            var bh = LifecycleSnapshot.BhEvents(3);
            var stats = KeystoneArcPoolInsights.InstrumentStats(bh, LifecycleSnapshot.Cfg("BH", 1, 0, 0));
            Check(stats.Count == 2 && stats.Sum(x => x.Setups) == bh.Count && Math.Abs(stats.Sum(x => x.Pnl) - bh.Sum(e => e.GrossPnl)) < 0.01, "per-instrument split: MNQ + MGC setups and P/L add up to the total", string.Join(", ", stats.Select(x => x.Symbol + " " + x.Setups + " " + x.Pnl)));
        }
        // 13. Replacement delay: a blown slot waits N calendar days before the new evaluation trades.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 1, 1, 0); cfg.PoolSize = 1;
            var ev = Nights(-2100, 350, 350, 350, 350, 350, 350);
            var now = KeystoneArcEngine.SimulatePool(ev, cfg)[0];
            cfg.ReplacementDelayDays = 2;
            var later = KeystoneArcEngine.SimulatePool(Nights(-2100, 350, 350, 350, 350, 350, 350), cfg)[0];
            DateTime blow = later.DayHistory[0].Day;
            var firstTrade = later.DayHistory.Skip(1).FirstOrDefault(d => d.TradesAfter > 0);
            Check(now.DayHistory.Count > 1 && now.DayHistory[1].TradesAfter > 0, "delay 0: replacement trades the next session", "");
            Check(firstTrade != null && firstTrade.Day >= blow.AddDays(3) && later.Trades < now.Trades, "delay 2: blown on day 1, new evaluation trades 3+ calendar days later", firstTrade == null ? "none" : blow.ToShortDateString() + " → " + firstTrade.Day.ToShortDateString());
        }
        Console.WriteLine(failures == 0 ? "\nALL LIFECYCLE TESTS PASSED" : "\n" + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
