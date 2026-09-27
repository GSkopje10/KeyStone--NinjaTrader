// 123 ENGULFING strategy tests on hand-made 1-minute candles.
// Run: tools/compile_engine.sh tests/.build/eng.exe tests/EngulfingTests.cs && mono tests/.build/eng.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class EngulfingTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }
    static readonly DateTime T0 = new DateTime(2026, 3, 10, 9, 0, 0);
    static List<KeystoneArcBar> Bars(IEnumerable<double[]> ohlc)
    {
        var list = new List<KeystoneArcBar>(); int i = 0;
        foreach (var x in ohlc) list.Add(new KeystoneArcBar { Symbol = "MGC", Time = T0.AddMinutes(i++), Open = x[0], High = x[1], Low = x[2], Close = x[3] });
        return list;
    }
    static double[] C(double o, double h, double l, double c) { return new[] { o, h, l, c }; }
    static KeystoneArcRunConfig Cfg()
    {
        return new KeystoneArcRunConfig
        {
            StrategyCode = "ENG", Scope = "MGC", SetupMinutes = 1, SessionMode = "CUSTOM", CustomStart = 800, EndTime = 1655,
            Start = T0.Date, End = T0.Date.AddDays(1), TargetDollars = 100, StopDollars = 50, Quantity = 1, StopMode = "STANDARD", OutcomeModelEnabled = 1,
            EngStopMode = "FIXED"
        };
    }
    static List<KeystoneArcEvent> Run(List<double[]> c, KeystoneArcRunConfig cfg) { var b = Bars(c); return KeystoneArcEngine.DetectAndResolve(b, b, cfg); }
    // Flat filler so the outcome has minutes to run on.
    static IEnumerable<double[]> Flat(double p, int n) { for (int i = 0; i < n; i++) yield return C(p, p + 0.2, p - 0.2, p); }

    public static int Main()
    {
        // 1. Two red candles, then a green candle closing above the 2nd one's body (not its wick) → BUY, BODY.
        {
            var c = new List<double[]> { C(100, 100.2, 99.8, 100), C(100, 100.1, 98, 98.5), C(98.5, 99.0, 97, 97.2), C(97.2, 98.9, 97.1, 98.7) };
            c.AddRange(Flat(98.7, 5));
            var ev = Run(c, Cfg());
            Check(ev.Count == 1 && ev[0].Direction == "LONG" && ev[0].StrengthTag == "BODY" && Math.Abs(ev[0].Entry - 98.7) < 1e-9, "red, red, green closing above the 2nd body → BUY at the close (BODY)", ev.Count + " " + (ev.Count > 0 ? ev[0].StrengthTag : ""));
            Check(ev.Count == 1 && ev[0].EntryTime == T0.AddMinutes(4), "market entry on the next 1-minute bar after the signal close", ev.Count > 0 ? ev[0].EntryTime.ToString("HH:mm") : "");
        }
        // 2. Green closes inside the 2nd body (below its open) → no setup.
        {
            var c = new List<double[]> { C(100, 100.2, 99.8, 100), C(100, 100.1, 98, 98.5), C(98.5, 98.6, 97, 97.2), C(97.2, 98.4, 97.1, 98.3) };
            c.AddRange(Flat(98.3, 3));
            Check(Run(c, Cfg()).Count == 0, "close not above the 2nd candle's body → no setup");
        }
        // 3. Long run (5 red), sweep of the last 2 lows and close above the last wick → SWEEP+WICK, DOUBLE TROUBLE.
        {
            var c = new List<double[]> { C(106, 106.2, 104.9, 105), C(105, 105.1, 103.9, 104), C(104, 104.1, 102.9, 103), C(103, 103.1, 101.9, 102), C(102, 102.1, 100.9, 101), C(101, 102.5, 100.2, 102.4) };
            c.AddRange(Flat(102.4, 3));
            var ev = Run(c, Cfg());
            Check(ev.Count == 1 && ev[0].StrengthTag == "SWEEP+WICK" && ev[0].QualityTier == "DT" && ev[0].FvgRedRun == 5, "5 red, sweep below the last 2 lows, close above the last wick → SWEEP+WICK, DOUBLE TROUBLE", ev.Count > 0 ? ev[0].StrengthTag + " " + ev[0].QualityTier + " run " + ev[0].FvgRedRun : "none");
        }
        // 4. SELL mirror resolves as a short: price falls to the target → WIN, positive P/L.
        {
            var c = new List<double[]> { C(100, 100.2, 99.8, 100), C(100, 101.5, 99.9, 101.4), C(101.4, 102.6, 101.3, 102.5), C(102.5, 102.9, 100.9, 101.0) };
            c.Add(C(101.0, 101.1, 99.5, 99.8)); c.AddRange(Flat(99.8, 3));
            var sc = Cfg(); sc.TargetDollars = 10; sc.StopDollars = 20;
            var ev = Run(c, sc);
            Check(ev.Count == 1 && ev[0].Direction == "SHORT" && ev[0].Outcome == "WIN" && ev[0].GrossPnl > 0 && ev[0].Stop > ev[0].Entry && ev[0].Target < ev[0].Entry, "green, green, red closing below the 2nd body → SELL; price falls → WIN", ev.Count > 0 ? ev[0].Direction + " " + ev[0].Outcome + " " + ev[0].GrossPnl : "none");
        }
        // 5. Direction filter and strength filter.
        {
            var sell = new List<double[]> { C(100, 100.2, 99.8, 100), C(100, 101.5, 99.9, 101.4), C(101.4, 102.6, 101.3, 102.5), C(102.5, 102.9, 100.9, 101.0) }; sell.AddRange(Flat(101, 3));
            var cfg = Cfg(); cfg.EngDirection = "BUY";
            Check(Run(sell, cfg).Count == 0, "BUY ONLY ignores sell setups");
            var body = new List<double[]> { C(100, 100.2, 99.8, 100), C(100, 100.1, 98, 98.5), C(98.5, 98.6, 97, 97.2), C(97.2, 98.9, 97.1, 98.7) }; body.AddRange(Flat(98.7, 3));
            cfg = Cfg(); cfg.EngStrength = "SWEEP";
            Check(Run(body, cfg).Count == 0, "SWEEP ONLY ignores a plain body engulf");
            cfg = Cfg(); cfg.EngMinRun = 3;
            Check(Run(body, cfg).Count == 0, "MIN RUN 3 ignores a 2-candle run");
        }
        // 6. The signal candle's own range never decides the outcome; CANDLE stop sizes contracts.
        {
            var c = new List<double[]> { C(100, 100.2, 99.8, 100), C(100, 100.1, 98, 98.5), C(98.5, 98.6, 97, 97.2), C(97.2, 98.9, 96.0, 98.7) };
            c.AddRange(Flat(98.7, 3));
            var cfg = Cfg(); cfg.EngStopMode = "CANDLE"; cfg.StopDollars = 100; cfg.EngTargetMode = "R_BY_QUALITY";
            var ev = Run(c, cfg);
            // MGC $10/point: stop 96.0, distance 2.7 → floor(100 / 27) = 3 contracts.
            Check(ev.Count == 1 && Math.Abs(ev[0].Stop - 96.0) < 1e-9 && Math.Abs(ev[0].Quantity - 3) < 1e-9 && ev[0].RiskModel.StartsWith("ENG "), "CANDLE stop below the engulfing wick, auto size", ev.Count > 0 ? ev[0].Stop + " x" + ev[0].Quantity + " " + ev[0].RiskModel : "none");
            Check(ev.Count == 1 && ev[0].Outcome == "SESSION EXIT", "no stop/target inside the flat minutes after entry → session exit (signal candle low not counted)", ev.Count > 0 ? ev[0].Outcome : "");
        }
        Console.WriteLine(failures == 0 ? "\nALL ENGULFING TESTS PASSED" : "\n" + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
