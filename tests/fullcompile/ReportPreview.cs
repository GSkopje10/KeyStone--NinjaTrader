// Builds the exported HTML report from synthetic data through the real lab code (reflection),
// so the report layout can be previewed in a browser without NinjaTrader.
// Usage: mono .build/report.exe <out.html> [BH|ASIAN75|FVG]
using System; using System.Collections.Generic; using System.Linq; using System.Reflection;
using NinjaTrader.NinjaScript; using NinjaTrader.NinjaScript.AddOns;
public static class ReportPreview
{
    public static int Main(string[] args)
    {
        string strategy = args.Length > 1 ? args[1] : "BH";
        var rng = new Random(3); var ev = new List<KeystoneArcEvent>(); DateTime day = new DateTime(2025, 1, 6);
        for (int d = 0; d < 260; d++)
        {
            DateTime s = day.AddDays(d); if (s.DayOfWeek == DayOfWeek.Saturday || s.DayOfWeek == DayOfWeek.Sunday) continue;
            int n = rng.Next(0, 4);
            for (int k = 0; k < n; k++)
            {
                DateTime t = s.AddHours(9).AddMinutes(35 + k * 40); bool win = rng.NextDouble() < (d < 120 ? 0.5 : 0.36);
                string sym = k % 2 == 0 ? "MNQ" : "MGC";
                ev.Add(new KeystoneArcEvent { Id = "E" + d + "_" + k, Symbol = sym, SetupClass = strategy == "FVG" ? "FVG" : "BH", Direction = "LONG", TriggerTime = t, EntryTime = t, ReferenceTime = t, ExitTime = t.AddMinutes(25), Outcome = win ? "WIN" : "LOSS", GrossPnl = win ? 1500 : -500, Quantity = 10, Entry = 20000, ReviewState = "ACCEPTED", StrengthTag = rng.NextDouble() < 0.4 ? "AGGR" : "BASE", FvgGap = 1 + rng.NextDouble() * 6, FvgVisit = 1 });
            }
        }
        var cfg = new KeystoneArcRunConfig { StrategyCode = strategy, SessionMode = "NY_OPEN", Scope = "BOTH", Start = day, End = day.AddDays(270), PoolSize = 20, EvaluationEnabled = 1, DailyGoal = 1500, DailyLoss = 500, TargetDollars = 1500, StopDollars = 500, OutcomeModelEnabled = 1, SetupMinutes = 5 };
        var accounts = KeystoneArcEngine.SimulatePool(ev, cfg);
        var labType = typeof(KeystoneArc5MResearchLab);
        var lab = (KeystoneArc5MResearchLab)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(labType);
        // run field initialisers that the report relies on
        foreach (var f in labType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (f.FieldType.IsGenericType && f.GetValue(lab) == null)
            {
                try { f.SetValue(lab, Activator.CreateInstance(f.FieldType)); } catch { }
            }
        }
        labType.GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab, cfg);
        labType.GetField("accounts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab, accounts);
        labType.GetField("events", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab, ev);
        labType.GetField("loadedEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab, ev);
        labType.GetField("loadedScope", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab, "BOTH");
        labType.GetField("lastInstrumentComparison", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab, "");
        string html = (string)labType.GetMethod("BuildHtmlReport", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lab, null);
        System.IO.File.WriteAllText(args[0], html);
        Console.WriteLine("REPORT WRITTEN " + html.Length + " chars");
        return 0;
    }
}
