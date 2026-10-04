// REPLAY TRADER: tick path inside a minute, buy / sell / reverse, stop and target, session-end close, journal line.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class ReplayTraderTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static KeystoneArcBar B(int m, double o, double h, double l, double c) { return new KeystoneArcBar { Symbol = "MNQ", Time = new DateTime(2026, 3, 10, 9, 30, 0).AddMinutes(m), Open = o, High = h, Low = l, Close = c }; }

    public static int Main()
    {
        var down = B(1, 100, 104, 90, 92);
        var path = Enumerable.Range(0, 13).Select(k => KeystoneReplayTrader.PathPrice(down, k, 12, 0.25)).ToList();
        Check(path[0] == 100 && path[12] == 92 && path.IndexOf(104) >= 0 && path.IndexOf(104) < path.IndexOf(90), "a red minute walks open → high → low → close", string.Join(" ", path));
        var up = B(1, 100, 110, 96, 108);
        var p2 = Enumerable.Range(0, 13).Select(k => KeystoneReplayTrader.PathPrice(up, k, 12, 0.25)).ToList();
        Check(p2.IndexOf(96) >= 0 && p2.IndexOf(96) < p2.IndexOf(110), "a green minute visits its low first");

        var bars = new List<KeystoneArcBar> { B(1, 100, 101, 99, 100), B(2, 100, 106, 99.5, 105), B(3, 105, 115, 104, 114), B(4, 114, 114, 100, 101) };
        var r = new KeystoneReplayTrader(bars, "MNQ", 12);
        for (int i = 0; i < 12; i++) r.Advance();                       // through minute 1
        r.Buy(2); r.TargetPts = 10;
        Check(r.Position == 2 && r.AvgPrice == 100, "BUY 2 at the current price 100", r.AvgPrice.ToString());
        while (r.Position != 0 && r.Advance()) { }
        var f = r.Fills.Last();
        Check(f.Reason == "TARGET" && f.Exit == 110 && Math.Abs(f.Net - (10 * 2 * 2 - 2 * KeystoneMoveStudy.Cost("MNQ"))) < 1e-9, "target +10 on 2 MNQ = $40 less costs", f.Reason + " " + f.Exit + " " + f.Net);
        var r2 = new KeystoneReplayTrader(bars, "MNQ", 12);
        for (int i = 0; i < 36; i++) r2.Advance();                      // end of minute 3 at 114
        r2.Sell(1); r2.StopPts = 0;
        Check(r2.Position == -1 && r2.AvgPrice == 114, "SELL 1 at 114");
        r2.Buy(2);
        Check(r2.Position == 1 && r2.Fills.Count == 1 && r2.Fills[0].Side == "SELL", "BUY 2 while short 1 = close the short and go long 1");
        while (r2.Advance()) { }
        Check(r2.Position == 0 && r2.Fills.Last().Reason == "SESSION END" && r2.Fills.Last().Exit == 101, "an open trade is closed at the session end (101)", r2.Fills.Last().Reason + " " + r2.Fills.Last().Exit);
        var pst = new KeystoneReplayTrader(bars, "MNQ", 12);
        for (int i = 0; i < 12; i++) pst.Advance(); pst.Buy(1);         // long at 100
        for (int i = 0; i < 24; i++) pst.Advance();                      // price up to 114
        pst.StopPts = -8;                                                // a stop past the entry: 108 locks +8
        while (pst.Position != 0 && pst.Advance()) { }
        Check(pst.Fills.Count == 1 && pst.Fills[0].Reason == "PROFIT STOP" && pst.Fills[0].Exit == 108, "a stop moved into profit (108) closes the long at +8 on the way down", pst.Fills.Count > 0 ? pst.Fills[0].Reason + " " + pst.Fills[0].Exit : "no fill");
        var c5 = new KeystoneReplayTrader(bars, "MNQ", 12); for (int i = 0; i < 18; i++) c5.Advance();
        var cs = c5.Candles(1);
        Check(cs.Count == 2 && cs[1].Close == c5.Price && cs[1].High <= 106, "the forming candle closes at the current price", cs.Count + " " + cs[1].Close + "/" + c5.Price);
        string line = KeystoneReplayTrader.JournalLine(new DateTime(2026, 3, 10), f);
        Check(line.StartsWith("2026-03-10,MNQ,BUY,2,") && line.EndsWith(",TARGET") && line.Split(',').Length == KeystoneReplayTrader.JournalHeader.Split(',').Length, "journal line", line);
        // working orders: buy limit below, sell stop below
        var r3 = new KeystoneReplayTrader(bars, "MNQ", 12); for (int i = 0; i < 12; i++) r3.Advance();   // at 100
        r3.Place("LIMIT", 1, 1, 99.75); r3.Place("STOP", 1, 1, 112);
        while (r3.Orders.Count > 0 && r3.Advance()) { }
        Check(r3.Position == 2 && Math.Abs(r3.AvgPrice - (99.75 + 112) / 2) < 1e-9, "BUY LIMIT 99.75 filled on the dip, BUY STOP 112 filled on the rally (avg " + r3.AvgPrice + ")");
        var r4 = new KeystoneReplayTrader(bars, "MNQ", 12); for (int i = 0; i < 12; i++) r4.Advance();
        r4.Place("LIMIT", -1, 1, 200); r4.CancelOrders(); while (r4.Advance()) { }
        Check(r4.Fills.Count == 0 && r4.Orders.Count == 0, "a cancelled order never fills");
        var day = new KeystoneReplayTrader(bars, "MNQ", 12); for (int i = 0; i < 30; i++) day.Advance();
        var dc = day.Candles(1440); Check(dc.Count == 1 && dc[0].Open == 100 && dc[0].Close == day.Price, "daily = one candle of the session so far");
        // strategy signals on a random walk session: buys and sells, inside the session, with stop + target
        var rng = new Random(5); var walk = new List<KeystoneArcBar>(); double px = 20000;
        for (var tm = new DateTime(2026, 3, 9, 18, 1, 0); tm <= new DateTime(2026, 3, 10, 16, 59, 0); tm = tm.AddMinutes(1)) { double o = px, c = Math.Round((px + (rng.NextDouble() - 0.5) * 20) / 0.25) * 0.25; walk.Add(new KeystoneArcBar { Symbol = "MNQ", Time = tm, Open = o, Close = c, High = Math.Max(o, c) + 1.5, Low = Math.Min(o, c) - 1.5 }); px = c; }
        foreach (int kind in new[] { 1, 2, 5 })
        {
            var sg = KeystoneReplaySignals.Build(kind, walk, "MNQ", new KeystoneArcRunConfig(), 0);
            Check(sg.Count > 0 && sg.Any(x => x.Dir > 0) && sg.Any(x => x.Dir < 0) && sg.All(x => x.Time >= walk[0].Time.AddMinutes(-1) && x.Time <= walk[walk.Count - 1].Time), KeystoneReplaySignals.Names[kind] + ": buys + sells inside the session", sg.Count.ToString());
        }
        Check(KeystoneReplaySignals.Build(0, walk, "MNQ", null, 0).Count == 0, "MANUAL = no signals");
        // the realistic tick path: 5,000 random minutes — always inside the real high / low, touching both, exact open and close
        var rq = new Random(9); bool inside = true, touches = true, ends = true;
        for (int n = 0; n < 5000; n++)
        {
            double o = 100 + rq.Next(-40, 40) * 0.25, c = 100 + rq.Next(-40, 40) * 0.25, h = Math.Max(o, c) + rq.Next(0, 30) * 0.25, lo = Math.Min(o, c) - rq.Next(0, 30) * 0.25;
            var bar = new KeystoneArcBar { Symbol = "MNQ", Time = new DateTime(2026, 1, 5, 9, 31, 0).AddMinutes(n), Open = o, High = h, Low = lo, Close = c };
            var pth = Enumerable.Range(0, 61).Select(k => KeystoneReplayTrader.PathPrice(bar, k, 60, 0.25)).ToList();
            if (pth.Any(v => v > h + 1e-9 || v < lo - 1e-9)) inside = false;
            if (Math.Abs(pth.Max() - h) > 1e-9 || Math.Abs(pth.Min() - lo) > 1e-9) touches = false;
            if (pth[0] != o || pth[60] != c) ends = false;
        }
        Check(inside && touches && ends, "realistic path: inside the real high / low, touches both, exact open + close (5,000 minutes)", inside + " " + touches + " " + ends);
        Console.WriteLine(failures == 0 ? "ALL REPLAY TRADER TESTS PASSED" : failures + " REPLAY TRADER TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
