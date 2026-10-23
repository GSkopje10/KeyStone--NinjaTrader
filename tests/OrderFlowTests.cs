// ORDER FLOW: aggressor classification, footprints, delta / cumulative delta, volume profile (POC, value area), imbalances + stacks,
// the three signals, and their hit rates without look-ahead.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class OrderFlowTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static KeystoneOfTick T(DateTime t, double p, long sz, int side) { return new KeystoneOfTick { Time = t, Price = p, Size = sz, Side = side, Quoted = true }; }
    public static int Main()
    {
        // 1. who was the aggressor
        Check(KeystoneOrderFlow.Classify(100.25, 100.00, 100.25, 0, 0) == 1 && KeystoneOrderFlow.Classify(100.00, 100.00, 100.25, 0, 0) == -1, "a trade at the ask = buyer, at the bid = seller");
        Check(KeystoneOrderFlow.Classify(100.50, 0, 0, 100.25, -1) == 1 && KeystoneOrderFlow.Classify(100.00, 0, 0, 100.25, 1) == -1 && KeystoneOrderFlow.Classify(100.25, 0, 0, 100.25, -1) == -1, "no quote: the tick rule (up = buy, down = sell, same price = the side before)");
        // 2. one candle's footprint
        var t0 = new DateTime(2026, 10, 22, 9, 30, 0); double tk = 0.25; var s = new KeystoneOfSettings { ImbRatio = 3, ImbMinVol = 5, StackMin = 3 };
        var ticks = new List<KeystoneOfTick> {
            T(t0.AddSeconds(5), 100.00, 2, -1), T(t0.AddSeconds(6), 100.25, 10, 1), T(t0.AddSeconds(7), 100.50, 12, 1), T(t0.AddSeconds(8), 100.75, 15, 1),
            T(t0.AddSeconds(9), 101.00, 20, 1), T(t0.AddSeconds(10), 100.75, 3, -1), T(t0.AddSeconds(20), 100.50, 1, -1), T(t0.AddSeconds(30), 101.00, 4, 1) };
        var win = new List<Tuple<DateTime, DateTime>> { Tuple.Create(t0, t0.AddMinutes(1)) };
        var fp = KeystoneOrderFlow.Footprints(ticks, win, tk, s);
        var f = fp[0];
        Check(f.Buy == 61 && f.Sell == 6 && f.Delta == 55 && f.Cvd == 55 && f.Open == 100.00 && f.High == 101.00 && f.Low == 100.00 && f.Close == 101.00, "footprint: buy 61 / sell 6 → delta +55, OHLC from the trades", f.Buy + "/" + f.Sell);
        Check(f.Poc == 101.00 && f.Rows[100.75].Buy == 15 && f.Rows[100.75].Sell == 3, "POC = the most traded price (101.00, 24 lots); rows keep buy / sell apart", f.Poc.ToString());
        Check(f.MaxDelta == 55 && f.MinDelta == -2, "the delta's high / low inside the candle", f.MaxDelta + " / " + f.MinDelta);
        // 3. diagonal imbalances + a stack: buy 10 @100.25 vs sell 2 @100.00 = 5×, buy 12 @100.50 vs 0 = 12×, buy 15 @100.75 vs sell 1 @100.50, buy 24 @101 vs 3 @100.75
        Check(f.Imb.Count(x => x.Side > 0) == 4 && f.Stacks.Any(x => x.Side > 0 && x.Count == 4 && x.Lo == 100.25), "4 buying imbalances in a row 100.25 → 101.00 = a stack", string.Join(",", f.Imb.Select(x => x.Price + ":" + x.Side)));
        // 4. the volume profile + value area
        var prof = KeystoneOrderFlow.Profile(fp, tk, new KeystoneOfSettings { ValueAreaPct = 70 });
        Check(prof.Poc == 101.00 && prof.Volume == 67 && prof.Val <= 100.75 && prof.Vah >= 101.25 && prof.Val >= 100.25, "profile: POC 101.00, 67 lots, the value area around it (" + prof.Val + "–" + prof.Vah + ")");
        // 5. signals on a built series: a selling run then a candle at a new low that is bought hard (absorption + divergence)
        var series = new List<KeystoneOfTick>(); var wins = new List<Tuple<DateTime, DateTime>>(); double px = 200;
        for (int i = 0; i < 12; i++)
        {
            var a = t0.AddMinutes(i); wins.Add(Tuple.Create(a, a.AddMinutes(1)));
            if (i < 10) { series.Add(T(a.AddSeconds(5), px, 5, -1)); px -= 0.5; series.Add(T(a.AddSeconds(30), px, 20, -1)); }
            else if (i == 10) { series.Add(T(a.AddSeconds(5), px - 0.25, 40, -1)); series.Add(T(a.AddSeconds(10), px - 0.5, 30, -1)); series.Add(T(a.AddSeconds(30), px + 0.5, 10, 1)); series.Add(T(a.AddSeconds(50), px + 1.0, 80, 1)); }
            else { series.Add(T(a.AddSeconds(30), px + 20, 5, 1)); }
        }
        var fps = KeystoneOrderFlow.Footprints(series, wins, tk, new KeystoneOfSettings());
        var sig = KeystoneOrderFlow.Signals(fps, tk, new KeystoneOfSettings { LowLookback = 5, DivLookback = 5 });
        Check(sig.Any(g => g.Kind == "ABSORB" && g.Dir == 1 && g.Bar == 10), "absorption: a new low where most of the volume was sold at the bottom, closing high → BUY", string.Join(" | ", sig.Select(g => g.Kind + g.Dir + "@" + g.Bar)));
        Check(sig.Any(g => g.Kind == "DIVERGE" && g.Dir == 1 && g.Bar == 10), "delta divergence: a new low while the cumulative delta is higher and this candle's delta is positive → BUY");
        var sc = KeystoneOrderFlow.Score(sig, fps, tk, new KeystoneOfSettings { TargetTicks = 8, StopTicks = 8, HorizonBars = 5 });
        var ab = sig.First(g => g.Kind == "ABSORB");
        Check(ab.Outcome == 1 && double.IsNaN(ab.Prob) && ab.N == 0 && sc["ABSORB|1"].Item1 == 1 && sc["ABSORB|1"].Item2 == 1, "scored: it reached +8 ticks first (a win); its own probability uses only EARLIER signals (none → unknown)", ab.Outcome + " " + ab.Prob + " " + ab.N);
        // 5b. live: only the last candles are rebuilt (from the cached ones' cumulative delta) and match a full rebuild exactly
        var tail = KeystoneOrderFlow.Footprints(series, wins, tk, new KeystoneOfSettings(), 9, fps[8].Cvd, series.Count);
        Check(tail.Count == 3 && Enumerable.Range(0, 3).All(k => tail[k].Cvd == fps[9 + k].Cvd && tail[k].Delta == fps[9 + k].Delta && tail[k].Poc == fps[9 + k].Poc && tail[k].Rows.Count == fps[9 + k].Rows.Count && tail[k].Start == fps[9 + k].Start),
            "the forming candle alone is rebuilt live: same footprint, delta and cumulative delta as a full rebuild");
        var cut = KeystoneOrderFlow.Footprints(series, wins, tk, new KeystoneOfSettings(), 10, fps[9].Cvd, series.Count - 1);
        Check(cut[1].Trades == 0 && cut[0].Trades == fps[10].Trades, "only the trades counted when the rebuild started are used (the feed may be adding more)");
        // 6. probability = earlier hit rate: three earlier signals (2 wins) → the 4th shows 67 %
        var fake = new List<KeystoneOfSignal>(); var flat = new List<KeystoneFootprint>();
        for (int i = 0; i < 40; i++) flat.Add(new KeystoneFootprint { Open = 100, High = i == 2 || i == 12 ? 103 : i == 22 ? 100 : 100.5, Low = i == 22 ? 97 : 99.5, Close = 100, Trades = 1 });
        foreach (int b in new[] { 1, 11, 21, 31 }) fake.Add(new KeystoneOfSignal { Bar = b, Dir = 1, Kind = "STACKED", Price = 100 });
        KeystoneOrderFlow.Score(fake, flat, 0.25, new KeystoneOfSettings { TargetTicks = 8, StopTicks = 8, HorizonBars = 3 });
        Check(fake[0].Outcome == 1 && fake[1].Outcome == 1 && fake[2].Outcome == -1 && Math.Abs(fake[3].Prob - 200.0 / 3) < 1e-6 && fake[3].N == 3, "the 4th signal's probability = 2 wins of the 3 before it = 67 % (n = 3)", fake[3].Prob + " n=" + fake[3].N);
        Console.WriteLine(failures == 0 ? "ALL ORDER FLOW TESTS PASSED" : failures + " ORDER FLOW TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
