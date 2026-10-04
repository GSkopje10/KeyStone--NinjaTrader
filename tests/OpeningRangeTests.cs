// TESTED OPENING-RANGE RULES (studio kinds 8–10): the engine copy of the research rules.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class OpeningRangeTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    // a full session (18:00 → 17:00) flat at `px`, the cash session 09:30–16:00 spanning `rth` points; `drive` shapes 09:30–09:40
    static List<KeystoneArcBar> Day(DateTime day, double px, double rth, double drive)
    {
        var bars = new List<KeystoneArcBar>(); var t = day.AddDays(-1).AddHours(18);
        for (int k = 0; k < 1380; k++)
        {
            var open = t.AddMinutes(k); double o = px, c = px, h = px + 0.5, l = px - 0.5;
            if (open.Hour == 9 && open.Minute >= 30 && open.Minute < 40) { o = px + drive * (open.Minute - 30) / 10.0; c = px + drive * (open.Minute - 29) / 10.0; h = Math.Max(o, c) + 0.25; l = Math.Min(o, c) - 0.25; }
            if (open.Hour == 12 && open.Minute == 0) { h = px + rth / 2; l = px - rth / 2; }
            bars.Add(new KeystoneArcBar { Symbol = "MNQ", Time = open.AddMinutes(1), Open = o, High = h, Low = l, Close = c, Volume = 10 });
        }
        return bars;
    }

    public static int Main()
    {
        var day = new DateTime(2024, 5, 2); var prior = new List<KeystoneArcBar>();
        for (var d = day.AddDays(-30); d < day; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) prior.AddRange(Day(d, 18000, 100, 0));
        double atr = KeystoneOpeningRange.RthAtr(prior, day);
        Check(Math.Abs(atr - 100) < 1e-6, "14-day 09:30–16:00 range from the days before", atr.ToString());
        var rules = KeystoneOpeningRange.Presets();

        var up = KeystoneOpeningRange.Signals(rules[0], Day(day, 18000, 100, 20), prior, "MNQ");
        Check(up.Count == 1 && up[0].Dir == 1, "opening drive: the 09:30–09:40 candle closed up → one buy");
        Check(up.Count == 1 && up[0].Time == day.AddHours(9).AddMinutes(40) && up[0].Order == "MARKET", "opening drive: decided at 09:40, market order");
        Check(up.Count == 1 && Math.Abs(up[0].StopDist - 25) < 1e-6 && Math.Abs(up[0].TargetDist - 100) < 1e-6, "opening drive: stop 0.25 × range = 25 pts, target 4R = 100 pts");
        Check(up.Count == 1 && up[0].Exit == day.AddHours(15).AddMinutes(56) && up[0].Tier == "T", "opening drive: flat 15:55, tested tier");
        var dn = KeystoneOpeningRange.Signals(rules[0], Day(day, 18000, 100, -20), prior, "MNQ");
        Check(dn.Count == 1 && dn[0].Dir == -1, "opening drive: the candle closed down → one sell");
        var fomc = new DateTime(2024, 5, 1);
        var p2 = new List<KeystoneArcBar>(); for (var d = fomc.AddDays(-30); d < fomc; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) p2.AddRange(Day(d, 18000, 100, 0));
        Check(KeystoneOpeningRange.Signals(rules[0], Day(fomc, 18000, 100, 20), p2, "MNQ").Count == 0, "no trade on an FOMC day (2024-05-01)");
        Check(KeystoneOpeningRange.Signals(rules[0], Day(day, 18000, 100, 20), new List<KeystoneArcBar>(), "MNQ").Count == 0, "no 14-day history → no trade (never a guessed stop)");

        var br = KeystoneOpeningRange.Signals(rules[1], Day(day, 18000, 100, 20), prior, "MNQ");
        Check(br.Count == 2 && br.All(x => x.Order == "STOP") && br[0].Group == br[1].Group, "range break: a buy stop and a sell stop (one cancels the other)");
        Check(br.Count == 2 && br.Any(x => x.Dir == 1 && Math.Abs(x.Entry - (18020 + 0.25 + 0.25)) < 1e-6), "range break: buy stop one tick above the 15-minute high");
        Check(br.Count == 2 && br.All(x => x.CancelAt == day.AddHours(9).AddMinutes(45).AddMinutes(150)), "range break: orders cancelled 150 min after the range");
        var b30 = KeystoneOpeningRange.Signals(rules[2], Day(day, 18000, 100, 20), prior, "MNQ");
        Check(b30.Count == 2 && b30.All(x => Math.Abs(x.StopDist - x.TargetDist) < 1e-9 && x.StopDist > 0), "30M 1R: stop = the range, target = 1R");

        var sig = KeystoneReplaySignals.Build(8, Day(day, 18000, 100, 20), "MNQ", null, 0, prior);
        Check(sig.Count == 1 && sig[0].Dir == 1, "studio strategy list: kind 8 = OPENING DRIVE");
        Console.WriteLine(failures == 0 ? "ALL OPENING RANGE TESTS PASSED" : failures + " OPENING RANGE TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
