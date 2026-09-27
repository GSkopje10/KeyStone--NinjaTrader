// LAST-HOUR RELAY and VWAP SNAP-BACK tests on synthetic 1-minute data with a known answer.
// Run: tools/compile_engine.sh tests/.build/new.exe tests/NewStrategyTests.cs && mono tests/.build/new.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class NewStrategyTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }

    // Weekday sessions 18:00 (evening before) → 16:00, 1-minute bars. drift(day, minuteOfSession) adds a trend.
    static List<KeystoneArcBar> Sessions(DateTime firstDay, int days, Func<DateTime, DateTime, double> drift, int seed)
    {
        var r = new Random(seed); var list = new List<KeystoneArcBar>(); double p = 20000;
        for (DateTime d = firstDay; list.Count == 0 || d < firstDay.AddDays(days); d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
            for (DateTime t = d.AddDays(-1).AddHours(18).AddMinutes(1); t <= d.AddHours(16); t = t.AddMinutes(1))
            {
                double o = p; p += (r.NextDouble() - 0.5) * 2 + drift(d, t);
                list.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = o, Close = p, High = Math.Max(o, p) + r.NextDouble(), Low = Math.Min(o, p) - r.NextDouble(), Volume = 10 });
            }
        }
        return list;
    }

    static KeystoneArcRunConfig Cfg(string code, DateTime start, DateTime end)
    {
        return new KeystoneArcRunConfig { StrategyCode = code, Scope = "MNQ", SetupMinutes = 5, SessionMode = "FULL_GLOBEX", CustomStart = 1800, EndTime = 1655, Start = start, End = end, TargetDollars = 400, StopDollars = 200, Quantity = 1, StopMode = "STANDARD", OutcomeModelEnabled = 1 };
    }

    public static int Main()
    {
        DateTime d0 = new DateTime(2025, 3, 3);
        // 1. RELAY: on the test day the market rises from the Globex open to 10:00 and keeps rising into the close.
        {
            DateTime trend = d0.AddDays(35); while (trend.DayOfWeek == DayOfWeek.Saturday || trend.DayOfWeek == DayOfWeek.Sunday) trend = trend.AddDays(1);
            var bars = Sessions(d0, 40, (d, t) => d == trend ? 0.6 : 0, 1);
            var cfg = Cfg("RLY", d0, d0.AddDays(45));
            var ev = KeystoneArcEngine.DetectAndResolve(bars, null, cfg);
            var e = ev.FirstOrDefault(x => x.TriggerTime.Date == trend);
            Check(e != null && e.Direction == "LONG" && e.EntryTime == trend.AddHours(15).AddMinutes(25) && e.GrossPnl > 0 && e.ExitTime <= trend.AddHours(15).AddMinutes(55), "strong morning up-move → BUY at 15:25, out by 15:55 with a profit", e == null ? "none (" + ev.Count + " setups)" : e.Direction + " " + e.EntryTime.ToString("HH:mm") + " → " + e.ExitTime.ToString("HH:mm") + " " + e.Outcome + " " + e.GrossPnl);
            Check(ev.Count <= 32 && ev.GroupBy(x => x.TriggerTime.Date).All(g => g.Count() == 1) && ev.All(x => x.EntryTime.Hour == 15 && x.EntryTime.Minute == 25), "at most one decision a day, always at 15:25", ev.Count + " setups");
            var sellOnly = Cfg("RLY", d0, d0.AddDays(45)); sellOnly.RelayDirection = "SELL";
            Check(KeystoneArcEngine.DetectAndResolve(bars, null, sellOnly).All(x => x.Direction == "SHORT"), "SELL ONLY never buys");
        }
        // 2. VWAP SNAP-BACK: a quiet day with one spike at 11:00 that fails → SELL back toward VWAP → WIN.
        {
            DateTime day = d0.AddDays(35); while (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) day = day.AddDays(1);
            var one = Sessions(d0, 40, (d, t) => d == day && t >= day.AddHours(10).AddMinutes(56) && t <= day.AddHours(11) ? 9 : (d == day && t > day.AddHours(11).AddMinutes(5) && t <= day.AddHours(11).AddMinutes(40) ? -1.2 : 0), 3);
            // A clean failed spike: the minutes after 11:00 stay below the spike's high.
            double spikeHigh = one.Where(b => b.Time.Date == day && b.Time <= day.AddHours(11)).Max(b => b.High);
            foreach (var b in one.Where(b => b.Time > day.AddHours(11) && b.Time <= day.AddHours(11).AddMinutes(10))) { b.High = Math.Min(b.High, spikeHigh - 0.5); b.Open = Math.Min(b.Open, b.High); b.Close = Math.Min(b.Close, b.High); b.Low = Math.Min(b.Low, Math.Min(b.Open, b.Close)); }
            var five = KeystoneArcEngine.ToSetupBars(one, "MNQ", 5);
            var cfg = Cfg("VWP", d0, d0.AddDays(45));
            var ev = KeystoneArcEngine.DetectAndResolve(one, five, cfg);
            var e = ev.FirstOrDefault(x => x.TriggerTime.Date == day);
            Check(e != null && e.Direction == "SHORT" && e.TriggerTime.Hour == 11 && e.Outcome == "WIN" && e.Target < e.Entry && e.Stop > e.Entry, "spike above the 2.5σ band fails at 11:00 → SELL back to the 1σ band → WIN", e == null ? "none (" + ev.Count + ")" : e.Direction + " " + e.TriggerTime.ToString("HH:mm") + " " + e.Outcome + " " + e.GrossPnl);
            Check(ev.All(x => x.TriggerTime.Hour * 100 + x.TriggerTime.Minute > 1030 && x.TriggerTime.Hour * 100 + x.TriggerTime.Minute <= 1430) && ev.GroupBy(x => x.TriggerTime.Date).All(g => g.Count() <= 2), "only 10:30–14:30, at most 2 a day", ev.Count + " setups");
            // Trend-day filter: a first hour 3× wider than usual keeps the day out.
            var wild = Sessions(d0, 40, (d, t) => d == day && t.Hour * 100 + t.Minute > 930 && t.Hour * 100 + t.Minute <= 1030 ? (t.Minute % 20 < 10 ? 3 : -3) : (d == day && t >= day.AddHours(10).AddMinutes(56) && t <= day.AddHours(11) ? 9 : 0), 3);
            var evWild = KeystoneArcEngine.DetectAndResolve(wild, KeystoneArcEngine.ToSetupBars(wild, "MNQ", 5), cfg);
            Check(!evWild.Any(x => x.TriggerTime.Date == day), "trend-day filter: a very wide first hour → no snap-back trades that day");
        }
        Console.WriteLine(failures == 0 ? "\nALL NEW STRATEGY TESTS PASSED" : "\n" + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
