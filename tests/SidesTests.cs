// SELL setups = the BUY detector on the mirrored price: golden sells on a series must be the golden buys on its mirror
// (same times, same P/L, mirrored prices, SHORT), and BUY results must not change when SELL is added.
// Run: tools/compile_engine.sh tests/.build/sides.exe tests/SidesTests.cs && mono tests/.build/sides.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class SidesTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    // A random walk of 1-minute bars over several sessions (deterministic).
    static List<KeystoneArcBar> Walk(string sym, double start, double step)
    {
        var rng = new Random(11); var l = new List<KeystoneArcBar>(); double p = start;
        for (var d = new DateTime(2026, 3, 9); d < new DateTime(2026, 3, 14); d = d.AddDays(1))
            for (var t = d.AddHours(8); t <= d.AddHours(16); t = t.AddMinutes(1))
            {
                double o = p, c = Math.Round((p + (rng.NextDouble() - 0.5) * step * 8) / 0.25) * 0.25;
                double h = Math.Max(o, c) + Math.Round(rng.NextDouble() * step * 2 / 0.25) * 0.25, lo = Math.Min(o, c) - Math.Round(rng.NextDouble() * step * 2 / 0.25) * 0.25;
                l.Add(new KeystoneArcBar { Symbol = sym, Time = t, Open = o, High = h, Low = lo, Close = c }); p = c;
            }
        return l;
    }

    static KeystoneArcRunConfig Cfg(string side, string fvg)
    {
        return new KeystoneArcRunConfig
        {
            StrategyCode = "GLD", TradeSide = side, GoldenFvgMode = fvg, GoldenUseBh = 1, GoldenTakeAll = 1, GoldenMaxTradesPerDay = 5, GoldenMnqMinGap = 1, GoldenSkipStopOverTarget = 0, GoldenHold = 0,
            Scope = "MNQ", SetupMinutes = 1, SessionMode = "INSTRUMENT_DEFAULT", EndTime = 1555, MnqStart = 930, MgcStart = 800, GoldenMnqStart = 930, GoldenLastEntry = 1500, GoldenClose = 1555,
            Start = new DateTime(2026, 3, 9), End = new DateTime(2026, 3, 14), OutcomeModelEnabled = 1, Quantity = 1, TargetDollars = 1500, StopDollars = 500
        };
    }

    public static int Main()
    {
        var bars = Walk("MNQ", 20000, 2.5);
        foreach (string fvg in new[] { "BREAK", "CLOSE", "DIP50", "OFF" })
        {
            var buy = KeystoneArcEngine.DetectAndResolve(bars, bars, Cfg("BUY", fvg));
            var sell = KeystoneArcEngine.DetectAndResolve(bars, bars, Cfg("SELL", fvg));
            var both = KeystoneArcEngine.DetectAndResolve(bars, bars, Cfg("BOTH", fvg));
            double k = 50000; var mirror = KeystoneArcEngine.Mirror(bars, k);
            var buyOnMirror = KeystoneArcEngine.DetectAndResolve(mirror, mirror, Cfg("BUY", fvg));
            Check(buy.Count > 0 && sell.Count > 0, fvg + ": buys and sells found on a random walk", buy.Count + " buys / " + sell.Count + " sells");
            Check(sell.All(e => e.Direction == "SHORT") && buy.All(e => e.Direction == "LONG"), fvg + ": sells are SHORT, buys LONG");
            Check(both.Count == buy.Count + sell.Count && both.Where(e => e.Direction == "LONG").Select(e => e.Id + e.GrossPnl).SequenceEqual(buy.Select(e => e.Id + e.GrossPnl)), fvg + ": BOTH = the same buys + the sells (buys unchanged)");
            bool same = sell.Count == buyOnMirror.Count && sell.Zip(buyOnMirror, (a, b) => a.EntryTime == b.EntryTime && Math.Abs(a.GrossPnl - b.GrossPnl) < 1e-6 && a.Outcome == b.Outcome).All(x => x);
            Check(same, fvg + ": a sell = the buy on the mirrored chart (same time, outcome, P/L)", sell.Count + " vs " + buyOnMirror.Count);
            bool sane = sell.Where(e => e.Outcome == "WIN").All(e => e.Target < e.Entry && e.Stop > e.Entry && e.ExitPrice <= e.Entry) && sell.All(e => double.IsNaN(e.Stop) || e.Stop > e.Entry);
            Check(sane, fvg + ": sell prices are real prices (stop above, target below the entry)");
        }
        // BH: the same mirror at the top of the engine
        Func<string, KeystoneArcRunConfig> bh = side => new KeystoneArcRunConfig { StrategyCode = "BH", TradeSide = side, EnableBh = 1, EnableFvg = 0, Scope = "MNQ", SetupMinutes = 1, SessionMode = "INSTRUMENT_DEFAULT", EndTime = 1555, MnqStart = 930, MgcStart = 800, Start = new DateTime(2026, 3, 9), End = new DateTime(2026, 3, 14), OutcomeModelEnabled = 1, Quantity = 1, TargetDollars = 500, StopDollars = 250 };
        var bBuy = KeystoneArcEngine.DetectAndResolve(bars, bars, bh("BUY")); var bSell = KeystoneArcEngine.DetectAndResolve(bars, bars, bh("SELL")); var bBoth = KeystoneArcEngine.DetectAndResolve(bars, bars, bh("BOTH"));
        Check(bBuy.Count > 0 && bSell.Count > 0 && bSell.All(e => e.Direction == "SHORT") && bBuy.All(e => e.Direction == "LONG"), "BH: buys and SELLS (break of a low)", bBuy.Count + " / " + bSell.Count);
        Check(bBoth.Count == bBuy.Count + bSell.Count && bBoth.Where(e => e.Direction == "LONG").Select(e => e.Id + e.GrossPnl).SequenceEqual(bBuy.Select(e => e.Id + e.GrossPnl)), "BH BOTH = the unchanged buys + the sells");
        Check(bSell.Where(e => e.Outcome == "WIN").All(e => e.Target < e.Entry) && bSell.Where(e => !double.IsNaN(e.Stop)).All(e => e.Stop > e.Entry), "BH sell prices: stop above, target below");
        Console.WriteLine(failures == 0 ? "ALL SIDES TESTS PASSED" : failures + " SIDES TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
