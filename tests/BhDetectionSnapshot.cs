// Prints every BH setup detected and resolved on seeded random-walk bars, for several BH
// settings. Compared with tests/bh_detection_baseline.txt to prove BH detection and outcomes
// stay byte-for-byte unchanged when other strategies are added.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class BhDetectionSnapshot
{
    static List<KeystoneArcBar> Walk(string symbol, int seed, double start, double step)
    {
        var rng = new Random(seed); var list = new List<KeystoneArcBar>(); double price = start;
        DateTime t = new DateTime(2025, 3, 2, 18, 1, 0);
        for (int i = 0; i < 60 * 23 * 20; i++, t = t.AddMinutes(1))
        {
            if (t.Hour == 17) continue; // daily halt
            if (t.DayOfWeek == DayOfWeek.Saturday || (t.DayOfWeek == DayOfWeek.Friday && t.Hour >= 17) || (t.DayOfWeek == DayOfWeek.Sunday && t.Hour < 18)) continue;
            double o = price, c = Math.Round((price + (rng.NextDouble() - 0.5) * step * 4) / (step / 4)) * (step / 4);
            double h = Math.Max(o, c) + Math.Round(rng.NextDouble() * step * 2 / (step / 4)) * (step / 4), l = Math.Min(o, c) - Math.Round(rng.NextDouble() * step * 2 / (step / 4)) * (step / 4);
            list.Add(new KeystoneArcBar { Symbol = symbol, Time = t, Open = o, High = h, Low = l, Close = c }); price = c;
        }
        return list;
    }

    static string Run(string title, List<KeystoneArcBar> raw, KeystoneArcRunConfig cfg)
    {
        var sb = new StringBuilder("## " + title + "\n");
        foreach (var e in KeystoneArcEngine.DetectAndResolve(raw, cfg))
            sb.AppendLine(string.Join(" | ", new[] { e.Id, e.SetupClass, e.StrengthTag, e.Outcome, e.GrossPnl.ToString("0.##", CultureInfo.InvariantCulture), e.Entry.ToString("0.##", CultureInfo.InvariantCulture), e.Stop.ToString("0.##", CultureInfo.InvariantCulture), e.Target.ToString("0.##", CultureInfo.InvariantCulture), e.Quantity.ToString(CultureInfo.InvariantCulture), e.EntryTime.ToString("MM-dd HH:mm"), e.ExitTime.ToString("MM-dd HH:mm"), e.RiskModel }));
        return sb.ToString();
    }

    public static int Main(string[] args)
    {
        var raw = Walk("MNQ", 7, 20000, 4).Concat(Walk("MGC", 9, 2900, 0.8)).ToList();
        Func<KeystoneArcRunConfig> baseCfg = () => new KeystoneArcRunConfig { StrategyCode = "BH", Scope = "BOTH", EnableBh = 1, EnableFvg = 0, SetupMinutes = 5, SessionMode = "NY_OPEN", CustomStart = 930, EndTime = 1555, Start = new DateTime(2025, 3, 3), End = new DateTime(2025, 3, 22), TargetDollars = 1500, StopDollars = 500, Quantity = 10, StopMode = "STANDARD", OutcomeModelEnabled = 1 };
        var sb = new StringBuilder();
        sb.Append(Run("BH 5M NY OPEN STANDARD", raw, baseCfg()));
        var c2 = baseCfg(); c2.SessionMode = "FULL_GLOBEX"; sb.Append(Run("BH 5M FULL GLOBEX", raw, c2));
        var c3 = baseCfg(); c3.BhAggressionFilter = "STRONGER"; sb.Append(Run("BH 5M STRONGER", raw, c3));
        var c4 = baseCfg(); c4.StopMode = "BELOW_SETUP_LOW"; sb.Append(Run("BH 5M BELOW SETUP LOW", raw, c4));
        var c5 = baseCfg(); c5.SetupMinutes = 1; c5.SessionMode = "NY_EARLY"; sb.Append(Run("BH 1M NY EARLY", raw, c5));
        if (args.Length > 0) System.IO.File.WriteAllText(args[0], sb.ToString()); else Console.Write(sb.ToString());
        return 0;
    }
}
