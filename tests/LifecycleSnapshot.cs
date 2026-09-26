// Prints a deterministic summary of the virtual-account lifecycle for fixed synthetic events.
// Used to prove that lifecycle changes leave the selected scenarios (e.g. BH evaluation-first)
// byte-for-byte unchanged: run before and after a change and diff the output.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class LifecycleSnapshot
{
    public static List<KeystoneArcEvent> BhEvents(int seed)
    {
        var rng = new Random(seed); var list = new List<KeystoneArcEvent>();
        DateTime day = new DateTime(2025, 1, 6);
        for (int d = 0; d < 180; d++)
        {
            DateTime session = day.AddDays(d);
            if (session.DayOfWeek == DayOfWeek.Saturday || session.DayOfWeek == DayOfWeek.Sunday) continue;
            int setups = rng.Next(0, 5);
            for (int k = 0; k < setups; k++)
            {
                DateTime t = session.AddHours(9).AddMinutes(35 + k * 25);
                bool win = rng.NextDouble() < 0.42;
                list.Add(new KeystoneArcEvent { Id = "E" + d + "_" + k, Symbol = k % 2 == 0 ? "MNQ" : "MGC", SetupClass = "BH", Direction = "LONG", TriggerTime = t, EntryTime = t, ReferenceTime = t,
                    ExitTime = t.AddMinutes(20), Outcome = win ? "WIN" : "LOSS", GrossPnl = win ? 1500 : -500, Quantity = 10, ReviewState = "ACCEPTED" });
            }
        }
        return list;
    }

    public static List<KeystoneArcEvent> AsianNights(int seed, int nights, double winPnl, double lossPnl, double winRate)
    {
        var rng = new Random(seed); var list = new List<KeystoneArcEvent>();
        DateTime day = new DateTime(2025, 1, 5);
        int made = 0;
        for (int d = 0; made < nights; d++)
        {
            DateTime session = day.AddDays(d);
            if (session.DayOfWeek == DayOfWeek.Friday || session.DayOfWeek == DayOfWeek.Saturday) continue;
            made++;
            bool win = rng.NextDouble() < winRate;
            DateTime t = session.AddHours(18).AddMinutes(1);
            list.Add(new KeystoneArcEvent { Id = "A" + d, Symbol = "MNQ", SetupClass = "ASIA75", Direction = "LONG", ReferenceTime = session.AddHours(18), TriggerTime = t, EntryTime = t,
                ExitTime = t.AddHours(3), Outcome = win ? "WIN" : "LOSS", GrossPnl = win ? winPnl : lossPnl, Quantity = 1, ReviewState = "ACCEPTED" });
        }
        return list;
    }

    public static KeystoneArcRunConfig Cfg(string strategy, int evaluationEnabled, int copy, int firmCap)
    {
        bool asian = strategy == "ASIAN75";
        return new KeystoneArcRunConfig
        {
            StrategyCode = strategy, SessionMode = asian ? "ASIAN75" : "NY_AFTER_0930", Scope = "BOTH",
            Start = asian ? new DateTime(2025, 1, 5, 18, 0, 0) : new DateTime(2025, 1, 6, 9, 30, 0), End = new DateTime(2025, 12, 31),
            PoolSize = 10, CopyTradingPool = copy, EvaluationEnabled = evaluationEnabled, FirmFundedCapEnabled = firmCap,
            DailyGoal = 1500, DailyLoss = 500, TargetDollars = 1500, StopDollars = 500,
            AsianCycleTargetDollars = 350, AsianDailyLossLimitDollars = 600
        };
    }

    public static string Summarize(string title, List<KeystoneArcVirtualAccount> accounts)
    {
        var sb = new StringBuilder("## " + title + "\n");
        foreach (var a in accounts)
            sb.AppendLine(string.Join(" | ", new[] { a.Name, "buy " + a.EvaluationPurchases, "cost " + a.EvaluationCost, "pass " + a.EvaluationPasses, "payouts " + a.Payouts, "gross " + a.PayoutGrossWithdrawn, "cash " + a.PayoutCash,
                "failE " + a.FailedEvaluations, "failF " + a.FailedFunded, "blown " + a.Blown, "funded " + a.Funded, "trades " + a.Trades, "pnl " + a.TotalPnl, "days " + a.DayHistory.Count, a.LastState }));
        return sb.ToString();
    }

    public static string All()
    {
        var sb = new StringBuilder();
        var bh = BhEvents(11);
        Func<List<KeystoneArcEvent>, List<KeystoneArcEvent>> copy = src => src.Select(e => new KeystoneArcEvent { Id = e.Id, Symbol = e.Symbol, SetupClass = e.SetupClass, Direction = e.Direction, TriggerTime = e.TriggerTime, EntryTime = e.EntryTime, ReferenceTime = e.ReferenceTime, ExitTime = e.ExitTime, Outcome = e.Outcome, GrossPnl = e.GrossPnl, Quantity = e.Quantity, ReviewState = e.ReviewState }).ToList();
        sb.Append(Summarize("BH EVAL-FIRST ROTATION", KeystoneArcEngine.SimulatePool(copy(bh), Cfg("BH", 1, 0, 0))));
        sb.Append(Summarize("BH EVAL-FIRST COPY", KeystoneArcEngine.SimulatePool(copy(bh), Cfg("BH", 1, 1, 0))));
        sb.Append(Summarize("BH EVAL-FIRST ROTATION FIRM CAP", KeystoneArcEngine.SimulatePool(copy(bh), Cfg("BH", 1, 0, 1))));
        sb.Append(Summarize("BH DIRECT-FUNDED ROTATION", KeystoneArcEngine.SimulatePool(copy(bh), Cfg("BH", 0, 0, 0))));
        sb.Append(Summarize("BH ONE-DAY", KeystoneArcEngine.SimulatePool(copy(bh.Take(4).ToList()), Cfg("BH", -1, 0, 0))));
        var asian = AsianNights(5, 190, 360, -600, 0.8);
        sb.Append(Summarize("ASIAN EVAL-FIRST COPY", KeystoneArcEngine.SimulatePool(copy(asian), Cfg("ASIAN75", 1, 1, 0))));
        sb.Append(Summarize("ASIAN DIRECT-FUNDED COPY", KeystoneArcEngine.SimulatePool(copy(asian), Cfg("ASIAN75", 0, 1, 0))));
        return sb.ToString();
    }

    public static int SnapshotMain(string[] args)
    {
        string text = All();
        if (args.Length > 0) System.IO.File.WriteAllText(args[0], text); else Console.Write(text);
        return 0;
    }
}
