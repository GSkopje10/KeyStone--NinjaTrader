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
        // 14. Investment answer: initial money, first payout, profitable date, spending after.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 0, 1, 0); cfg.PoolSize = 2;
            var acc = KeystoneArcEngine.SimulatePool(Nights(Repeat(900, 8).Concat(Repeat(-900, 4)).ToArray()), cfg);
            string ans = KeystoneArcPoolInsights.InvestmentAnswer(KeystoneArcEngine.BuildCapitalPolicySummary(acc, cfg), KeystoneArcPoolInsights.Build(acc, null, cfg));
            Console.WriteLine(ans);
            Check(ans.Contains("start $240") && ans.Contains("WAS ENOUGH") && ans.Contains("PROFITABLE FROM") && ans.Contains("FINAL NET"), "answer: $240 start was enough, profitable date, final net", ans);
        }
        // 15. Automatic analysis: front-loaded profits → EDGE FADED; one losing instrument → trade the other.
        {
            var cfg = LifecycleSnapshot.Cfg("ASIAN75", 0, 1, 0); cfg.PoolSize = 2;
            var pnl = Repeat(350, 60).Concat(Repeat(-400, 60)).ToArray();
            var ev = Nights(pnl);
            var acc = KeystoneArcEngine.SimulatePool(ev, cfg);
            var found = KeystoneArcAnalyst.Analyze(acc, ev, cfg, KeystoneArcEngine.BuildCapitalPolicySummary(acc, cfg));
            foreach (var f in found) Console.WriteLine("  [" + f.Level + "] " + f.Title + ": " + f.Text);
            Check(found.Any(f => f.Title == "EDGE FADED"), "analysis flags profits that only came early", string.Join(", ", found.Select(f => f.Title)));
            Check(found.Any(f => f.Title == "STRATEGY EDGE") && found.Any(f => f.Title == "NET RESULT"), "analysis states the strategy edge and the net result", "");
            var bh = LifecycleSnapshot.BhEvents(4);
            foreach (var e in bh.Where(e => e.Symbol == "MGC")) { e.Outcome = "LOSS"; e.GrossPnl = -500; }
            var bcfg = LifecycleSnapshot.Cfg("BH", 1, 0, 0);
            var found2 = KeystoneArcAnalyst.Analyze(new List<KeystoneArcVirtualAccount>(), bh, bcfg, null);
            Check(found2.Any(f => f.Title == "TRADE MNQ ONLY?"), "analysis suggests the winning instrument when the other loses", string.Join(", ", found2.Select(f => f.Title)));
        }
        // 16. Setup grades: walk-forward only, learn which buckets win; stage filters in the pool.
        {
            var list = new List<KeystoneArcEvent>(); DateTime d0 = new DateTime(2025, 2, 3);
            for (int i = 0; i < 160; i++)
            {
                DateTime day = d0.AddDays(i); bool good = i % 2 == 0;
                DateTime t = day.AddHours(good ? 10 : 14);
                list.Add(new KeystoneArcEvent { Id = "Q" + i, Symbol = "MNQ", SetupClass = "BH", TriggerTime = t, EntryTime = t, ExitTime = t.AddMinutes(30), Outcome = good ? "WIN" : "LOSS", GrossPnl = good ? 300 : -100, StopDistance = 50, Quantity = 1, StrengthTag = "BASE", SessionOrder = 1, ReviewState = "ACCEPTED" });
            }
            // a later event whose exit happens after the next entry must not be known at that entry
            KeystoneArcQualityLearner.GradeAll(list, e => 2.0);
            Check(list.Take(30).All(e => e.QualityTier == "N"), "first 30 setups are N (not enough earlier setups)", string.Join("", list.Take(30).Select(e => e.QualityTier)));
            var late = list.Skip(80).ToList();
            double goodA = late.Where(e => e.TriggerTime.Hour == 10).Count(e => e.QualityTier == "A"), badA = late.Where(e => e.TriggerTime.Hour == 14).Count(e => e.QualityTier == "A");
            Check(goodA > badA * 3, "grades learn: the winning 10:00 setups get A far more often than the losing 14:00 ones", goodA + " vs " + badA);
            var leak = new List<KeystoneArcEvent>(); DateTime t0 = new DateTime(2025, 6, 2, 10, 0, 0);
            for (int i = 0; i < 40; i++) leak.Add(new KeystoneArcEvent { Id = "L" + i, Symbol = "MNQ", SetupClass = "BH", TriggerTime = t0.AddMinutes(i), EntryTime = t0.AddMinutes(i), ExitTime = t0.AddDays(1), Outcome = "WIN", GrossPnl = 100, StopDistance = 50, Quantity = 1, StrengthTag = "BASE" });
            KeystoneArcQualityLearner.GradeAll(leak, e => 2.0);
            Check(leak.All(e => e.QualityTier == "N"), "no look-ahead: setups still open at the next entry are not used to grade it", string.Join("", leak.Select(e => e.QualityTier)));
            // Pool: funded accounts take only grade A.
            var cfg = LifecycleSnapshot.Cfg("BH", 0, 0, 0); cfg.PoolSize = 2; cfg.FundedTierFilter = "A";
            var evs = LifecycleSnapshot.BhEvents(5).Take(40).ToList();
            for (int i = 0; i < evs.Count; i++) evs[i].QualityTier = i % 4 == 0 ? "A" : "C";
            KeystoneArcEngine.SimulatePool(evs, cfg);
            Check(evs.Where(e => !string.IsNullOrEmpty(e.AssignedVirtualAccount)).All(e => e.QualityTier == "A") && evs.Any(e => !string.IsNullOrEmpty(e.AssignedVirtualAccount)), "FUNDED TRADES = A ONLY: funded accounts take only grade A setups", evs.Count(e => !string.IsNullOrEmpty(e.AssignedVirtualAccount)) + " assigned");
            Check(evs.Where(e => e.QualityTier == "C").All(e => (e.SkipReason ?? "").StartsWith("QUALITY FILTER")), "skipped setups say why (quality filter)", evs.First(e => e.QualityTier == "C").SkipReason);
        }
        // 17. Copy to groups, account-count comparison, payout diagnosis.
        {
            var cfg = LifecycleSnapshot.Cfg("BH", 0, 0, 0); cfg.PoolSize = 6; cfg.CopyGroupSize = 3; cfg.AllowMultipleSetupsPerDay = 1;
            var ev = LifecycleSnapshot.BhEvents(8).Take(30).ToList();
            var acc = KeystoneArcEngine.SimulatePool(ev, cfg);
            var groupsUsed = ev.Where(e => (e.AssignedVirtualAccount ?? "").StartsWith("GROUP")).Select(e => e.AssignedVirtualAccount.Substring(0, 7)).Distinct().ToList();
            Check(groupsUsed.Count == 2 && acc.Take(3).All(a => a.Trades == acc[0].Trades) && acc.Skip(3).All(a => a.Trades == acc[3].Trades), "copy to groups: 2 groups of 3 take turns, members of a group trade the same setups", string.Join(",", acc.Select(a => a.Trades)));
            var rows = KeystoneArcPoolCompare.Run(LifecycleSnapshot.BhEvents(8), LifecycleSnapshot.Cfg("BH", 1, 0, 0));
            Console.WriteLine(KeystoneArcPoolCompare.Table(rows));
            Check(rows.Count >= 10 && rows.Count(r => r.Best) <= 1 && rows.First(r => r.Accounts == 1 && !r.Copy).TradesPerAccount > rows.First(r => r.Accounts == 20 && r.GroupSize == 0).TradesPerAccount, "comparison covers account counts and groups; fewer accounts → more trades each", rows.Count.ToString());
            var few = LifecycleSnapshot.Cfg("BH", 0, 0, 0); few.PoolSize = 20;
            var accFew = KeystoneArcEngine.SimulatePool(LifecycleSnapshot.BhEvents(8).Take(40).ToList(), few);
            string why = KeystoneArcPoolCompare.PayoutDiagnosis(accFew, few);
            Console.WriteLine(why);
            Check(why.StartsWith("WHY") && why.Contains("FEWER ACCOUNTS"), "payout diagnosis explains too few trades per account", why);
        }
        // 18. Strategy comparison (BH vs FVG) summary, winner, same-entry agreement, day line.
        {
            var cfg = LifecycleSnapshot.Cfg("BH", 1, 0, 0);
            var bh = LifecycleSnapshot.BhEvents(8).Take(60).ToList();
            var fvg = bh.Select(e => e.CopyForPool()).ToList();
            for (int i = 0; i < fvg.Count; i++) { fvg[i].SetupClass = "FVG"; if (i % 3 == 0) { fvg[i].Outcome = "LOSS"; fvg[i].GrossPnl = -Math.Abs(fvg[i].GrossPnl == 0 ? 300 : fvg[i].GrossPnl); } if (i % 2 == 1) fvg[i].EntryTime = fvg[i].EntryTime.AddMinutes(45); }
            var rows = new List<KeystoneArcStrategyRow> {
                KeystoneArcStrategyCompare.Summarize("BH", bh, KeystoneArcEngine.SimulatePool(bh.Select(e => e.CopyForPool()).ToList(), cfg), cfg),
                KeystoneArcStrategyCompare.Summarize("FVG", fvg, KeystoneArcEngine.SimulatePool(fvg.Select(e => e.CopyForPool()).ToList(), cfg), cfg) };
            KeystoneArcStrategyCompare.MarkBest(rows);
            var agree = KeystoneArcStrategyCompare.SameEntries(fvg, bh, 5);
            Console.WriteLine(KeystoneArcStrategyCompare.Table(rows));
            string verdict = KeystoneArcStrategyCompare.Verdict(rows, agree.Count, agree.Count(e => e.Outcome == "WIN"), agree.Sum(e => e.GrossPnl));
            Console.WriteLine(verdict);
            Check(rows[0].Wins + rows[0].Losses + rows[0].Exits == rows[0].Resolved && rows[0].TradePnl > rows[1].TradePnl, "strategy rows count W/L/EXIT and trade P/L", rows[0].TradePnl + " vs " + rows[1].TradePnl);
            Check(rows.Count(r => r.Best) <= 1 && (rows.All(r => !r.Best) || rows.First(r => r.Best).Net >= rows.Where(r => !r.Best).Max(r => r.Net)), "winner = best net after costs", string.Join(",", rows.Select(r => r.Strategy + " " + r.Net)));
            Check(agree.Count == fvg.Count(e => bh.Any(b => b.Symbol == e.Symbol && Math.Abs((b.EntryTime - e.EntryTime).TotalMinutes) <= 5)) && agree.Count > 0 && agree.Count < fvg.Count, "same-entry agreement matches entries within the tolerance only", agree.Count + " of " + fvg.Count);
            Check(verdict.Contains("BOTH AGREE") && (verdict.StartsWith("WINNER • BH") || verdict.StartsWith("WINNER • FVG") || verdict.StartsWith("WINNER • none")), "verdict names the winner and the agreement", verdict);
            var day = bh[0].EntryTime.Date;
            string line = KeystoneArcStrategyCompare.DayLine(day.ToString("yyyy-MM-dd"), new[] { Tuple.Create("BH", bh.Where(e => e.EntryTime.Date == day).ToList()), Tuple.Create("FVG", fvg.Where(e => e.EntryTime.Date == day).ToList()) });
            Console.WriteLine(line);
            Check(line.StartsWith("DAY COMPARE") && line.Contains("BH ") && line.Contains("FVG "), "day line lists each strategy", line);
        }
        // 19. OPTIMAL BEST ENTRIES FOR PROP: eval / funded filter picks, hours, stop / target from price moves.
        {
            var cfg = LifecycleSnapshot.Cfg("BH", 1, 0, 0); cfg.StrategyCode = "FVG";
            var ev = LifecycleSnapshot.BhEvents(8).Take(120).ToList();
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i]; e.QualityTier = i % 10 == 0 ? "DT" : (i % 4 == 0 ? "A" : (i % 4 == 1 ? "B" : "C"));
                if (e.QualityTier == "C") { e.Outcome = "LOSS"; e.GrossPnl = -500; }
                if (e.QualityTier == "A" || e.QualityTier == "DT") { e.Outcome = "WIN"; e.GrossPnl = 800; }
                e.Entry = 2000; e.PeakAfterEntry = e.Outcome == "WIN" ? 2015 : 2004; e.TroughAfterEntry = e.Outcome == "WIN" ? 1994 : 1989; e.Symbol = "MGC"; e.Quantity = 10;
            }
            var plan = KeystoneArcAnalyst.BestEntries(ev, cfg, null, null);
            Console.WriteLine(plan.Headline); foreach (var c in plan.Cards) Console.WriteLine("[" + c.Level + "] " + c.Title + ": " + c.Text);
            Check(plan.Filters.Count >= 3 && (plan.FundedFilter == "A" || plan.FundedFilter == "DT"), "funded pick = the strongest grades when they earn most per setup", plan.FundedFilter);
            Check(plan.EvalFilter != "ALL", "evaluation pick avoids the losing C setups", plan.EvalFilter);
            Check(plan.Cards.Any(c => c.Title.StartsWith("MGC STOP / TARGET") && c.Text.Contains("TARGET ≈ 15")), "stop / target suggested from real price moves (winners moved +15)", string.Join(" | ", plan.Cards.Select(c => c.Title)));
            Check(plan.Cards.Any(c => c.Title == "DOUBLE TROUBLE"), "double trouble stats included");
            Check(plan.Headline.StartsWith("PROP PLAN"), "headline states the plan", plan.Headline);
        }
        // 20. Funded episodes: funded but never paid, how close to the payout balance.
        {
            foreach (int ds in new[] { 5, 8, 11, -5, -8 })
            {
                var cfg = LifecycleSnapshot.Cfg("BH", 1, 0, 0); cfg.PoolSize = 5;
                if (ds < 0) { cfg.FundedFailure = 700; cfg.PayoutThreshold = 6000; }
                var acc = KeystoneArcEngine.SimulatePool(LifecycleSnapshot.BhEvents(Math.Abs(ds)), cfg);
                var f = KeystoneArcFundedEpisodes.Build(acc, cfg);
                Console.WriteLine(f.Line(cfg) + "   | passes " + acc.Sum(a => a.EvaluationPasses) + " funded blowups " + acc.Sum(a => a.FailedFunded) + " payouts " + acc.Sum(a => a.Payouts) + " funded now " + acc.Count(a => a.Funded && !a.Blown));
                Check(f.Episodes == acc.Sum(a => a.EvaluationPasses) && f.Paid + f.NeverPaid + f.StillOpen >= f.Episodes - f.Paid && f.StillOpen == acc.Count(a => a.Funded && !a.Blown) && f.NeverPaid <= acc.Sum(a => a.FailedFunded),
                    "funded episodes = evaluation passes; never-paid ≤ funded blowups; open = funded now (seed " + ds + ")");
            }
        }
        // 21. DIRECT FUNDED: own account cost; a blowup is funded again after N days at the replacement cost, no evaluation stage.
        {
            var cfg = LifecycleSnapshot.Cfg("BH", 0, 0, 0); cfg.PoolSize = 3; cfg.FundedFailure = 700;
            cfg.DirectFundedCost = 500; cfg.DirectReplacementMode = 1; cfg.DirectRefundDays = 3; cfg.DirectReplacementCost = 150;
            var acc = KeystoneArcEngine.SimulatePool(LifecycleSnapshot.BhEvents(8), cfg);
            int blowups = acc.Sum(a => a.FailedFunded), purchases = acc.Sum(a => a.EvaluationPurchases);
            double cost = acc.Sum(a => a.EvaluationCost);
            Console.WriteLine("direct refund • blowups " + blowups + " • purchases " + purchases + " • cost " + cost + " • eval fails " + acc.Sum(a => a.FailedEvaluations) + " • passes " + acc.Sum(a => a.EvaluationPasses));
            Check(blowups > 0 && acc.Sum(a => a.FailedEvaluations) == 0, "direct funded never trades an evaluation after a blowup", blowups + " blowups");
            Check(Math.Abs(cost - (3 * 500 + (purchases - 3) * 150)) < 0.01, "first accounts at the direct-funded cost, replacements at the replacement cost", cost.ToString());
            bool gapOk = true;
            foreach (var a in acc)
            {
                var hist = a.DayHistory.OrderBy(d => d.Day).ToList();
                for (int i = 0; i < hist.Count; i++)
                    if (hist[i].BlownAfter && (i == 0 || !hist[i - 1].BlownAfter))
                    {
                        var next = hist.Skip(i + 1).FirstOrDefault(d => d.TradesAfter > 0);
                        if (next != null && (next.Day - hist[i].Day).TotalDays < 4) gapOk = false;
                    }
            }
            Check(gapOk, "after a blowup the next trade is at least 3 days + 1 later");
            var legacy = LifecycleSnapshot.Cfg("BH", 0, 0, 0); legacy.PoolSize = 3; legacy.FundedFailure = 700;
            var accLegacy = KeystoneArcEngine.SimulatePool(LifecycleSnapshot.BhEvents(8), legacy);
            Check(accLegacy.Sum(a => a.EvaluationPasses) + accLegacy.Sum(a => a.FailedEvaluations) > 0 || accLegacy.Sum(a => a.FailedFunded) == 0, "engine default keeps the original direct-funded replacement (new evaluation)");
        }
        // 22. NO HEDGING: never opposite directions on the same instrument across the pool (other instrument is fine).
        {
            DateTime d = new DateTime(2025, 2, 3, 10, 0, 0);
            Func<string, string, string, int, int, KeystoneArcEvent> mk = (id, sym, dir, start, len) => new KeystoneArcEvent { Id = id, Symbol = sym, Direction = dir, SetupClass = "ENG", TriggerTime = d.AddMinutes(start), EntryTime = d.AddMinutes(start), ExitTime = d.AddMinutes(start + len), Outcome = "WIN", GrossPnl = 100, Quantity = 1, ReviewState = "ACCEPTED" };
            var evs = new List<KeystoneArcEvent> { mk("A", "MNQ", "LONG", 0, 30), mk("B", "MNQ", "SHORT", 10, 10), mk("C", "MGC", "SHORT", 12, 10), mk("D", "MNQ", "LONG", 15, 5), mk("E", "MNQ", "SHORT", 40, 5) };
            foreach (bool copy in new[] { false, true })
            {
                var cfg = LifecycleSnapshot.Cfg("BH", 0, 0, 0); cfg.PoolSize = 4; cfg.AllowMultipleSetupsPerDay = 1; cfg.CopyGroupSize = copy ? 2 : 0;
                var list = evs.Select(e => e.CopyForPool()).ToList();
                KeystoneArcEngine.SimulatePool(list, cfg);
                Func<string, KeystoneArcEvent> get = id => list.First(e => e.Id == id);
                Console.WriteLine((copy ? "groups" : "rotation") + ": " + string.Join(" | ", list.Select(e => e.Id + " " + e.Symbol + " " + e.Direction + " → " + (string.IsNullOrEmpty(e.AssignedVirtualAccount) ? "SKIP " + e.SkipReason : e.AssignedVirtualAccount))));
                Check(!string.IsNullOrEmpty(get("A").AssignedVirtualAccount) && string.IsNullOrEmpty(get("B").AssignedVirtualAccount) && (get("B").SkipReason ?? "").StartsWith("NO HEDGING"), (copy ? "groups" : "rotation") + ": MNQ SHORT blocked while an MNQ LONG is open");
                Check(!string.IsNullOrEmpty(get("C").AssignedVirtualAccount) && (copy || !string.IsNullOrEmpty(get("D").AssignedVirtualAccount)) && !(get("D").SkipReason ?? "").StartsWith("NO HEDGING") && !string.IsNullOrEmpty(get("E").AssignedVirtualAccount), (copy ? "groups" : "rotation") + ": MGC opposite direction, same-direction MNQ and a later MNQ short (after the long closed) are allowed");
            }
        }
        // 23. MONTHS / WEEKS and SESSION FINDER.
        {
            var cfg = LifecycleSnapshot.Cfg("BH", 1, 0, 0); cfg.PoolSize = 5;
            var ev = LifecycleSnapshot.BhEvents(8).ToList();
            var acc = KeystoneArcEngine.SimulatePool(ev.Select(e => e.CopyForPool()).ToList(), cfg);
            DateTime from = ev.Min(e => e.TriggerTime).Date, to = ev.Max(e => e.TriggerTime).Date;
            var months = KeystoneArcPeriods.Build(ev, acc, cfg, "MONTH", true, from, to);
            Console.WriteLine(KeystoneArcPeriods.Table(months, "MONTHS"));
            Check(months.Count >= 2 && months.Sum(m => m.Setups) == ev.Count && Math.Abs(months.Sum(m => m.TradePnl) - ev.Where(KeystoneArcStrategyCompare.Resolved).Sum(e => e.GrossPnl)) < 0.01, "months add up to the whole range (setups and trade P/L)");
            Check(months.All(m => m.FreshRan) && months.Count(m => m.Best) <= 1 && months.Count(m => m.Worst) <= 1, "each month also runs alone with new accounts; one best / one worst");
            double poolNet = acc.Sum(a => a.PayoutCash - a.EvaluationCost);
            Check(Math.Abs(months.Sum(m => m.Net) - poolNet) < 0.01, "continuous pool net per month adds up to the pool total", months.Sum(m => m.Net) + " vs " + poolNet);
            var weeks = KeystoneArcPeriods.Build(ev, acc, cfg, "WEEK", false, months[0].Start, months[0].End);
            Check(weeks.Count >= 3 && weeks.Sum(w => w.Setups) == months[0].Setups, "weeks of a month add up to the month", weeks.Count + " weeks");
            var shifted = ev.Select((e, i) => { var c = e.CopyForPool(); int h = new[] { 3, 9, 10, 13 }[i % 4]; c.TriggerTime = c.TriggerTime.Date.AddHours(h).AddMinutes(5); c.EntryTime = c.TriggerTime; c.ExitTime = c.TriggerTime.AddMinutes(20); if (h == 10 && c.GrossPnl < 0) { c.GrossPnl = 800; c.Outcome = "WIN"; } return c; }).ToList();
            var sess = KeystoneArcSessions.Build(shifted, cfg, true);
            Console.WriteLine(KeystoneArcSessions.Table(sess));
            var best = sess.Windows.FirstOrDefault(w => w.Best);
            Check(best != null && best.Label.StartsWith("NY OPEN"), "session finder finds the window that really wins (10:00 setups)", best == null ? "none" : best.Label);
            Check(sess.Verdict.Contains("HELD UP") && sess.Hours.Count == 4, "picked on the first part, confirmed on the later part; hour table has only loaded hours", sess.Verdict);
        }
        Console.WriteLine(failures == 0 ? "\nALL LIFECYCLE TESTS PASSED" : "\n" + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
