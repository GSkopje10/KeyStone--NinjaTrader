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
        Console.WriteLine(failures == 0 ? "\nALL LIFECYCLE TESTS PASSED" : "\n" + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
