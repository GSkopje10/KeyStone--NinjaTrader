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
        var lv = KeystoneReplayTrader.LiveStart(bars.Take(2).ToList(), "MNQ", bars[1].Time);
        Check(lv.Live && lv.Price == 105 && lv.Candles(1).Count == 2, "LIVE: starts at the last bar's close with every bar so far on the chart");
        lv.Buy(1); lv.StopPts = 5; lv.TargetPts = 20;
        lv.LiveUpdate(new KeystoneArcBar { Symbol = "MNQ", Time = bars[2].Time, Open = 105, High = 108, Low = 104, Close = 107 }, bars[2].Time.AddSeconds(-30));
        Check(lv.Position == 1 && lv.Bars.Count == 3 && lv.Price == 107, "LIVE: a new forming bar is added, the position stays");
        lv.LiveUpdate(new KeystoneArcBar { Symbol = "MNQ", Time = bars[2].Time, Open = 105, High = 108, Low = 99, Close = 99.5 }, bars[2].Time.AddSeconds(-5));
        Check(lv.Position == 0 && lv.Fills.Count == 1 && lv.Fills[0].Reason == "STOP" && lv.Fills[0].Exit == 100 && lv.Bars.Count == 3, "LIVE: the forming bar updated in place; price through the stop closes at the stop (100)", lv.Fills.Count > 0 ? lv.Fills[0].Reason + " " + lv.Fills[0].Exit : "");
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
        {   // 📐 BUILD 11j — a STOP MOVED INTO PROFIT, counted exactly (the user's screenshot): TPT trailing $2,000, start $0, costs $2 / contract round trip;
            // long 8 MNQ @ 27,249.75, sell STP 27,251.50 (in profit), sell LMT 27,265.25; up to 27,261.00 (target not hit), then down through 27,251.50
            // our cost model books the WHOLE round trip in the open equity (open = 8·2·(px − entry) − 8·$2): peak = $180 − $16 = $164 (the user's gross $180), floor −$1,836
            double c2 = 2, pv2 = 2, e2 = 27249.75; int q2 = 8; var tpS = KeystonePractice.Presets().First(x => x.Name.StartsWith("TPT PRO 50K funded")).Copy(); tpS.ApplyTyped(); tpS.CloseAtGoal = false;
            Func<double, double> op2 = x => q2 * pv2 * (x - e2) - c2 * q2; double realized2 = 0; bool open2 = true, stopIn = false; double exitPx = double.NaN;
            var stopWalk = new List<double>(); for (double x = e2; x <= 27261.0 + 1e-9; x += 0.25) stopWalk.Add(Math.Round(x, 2)); for (double x = 27260.75; x >= 27250.0 - 1e-9; x -= 0.25) stopWalk.Add(Math.Round(x, 2));
            foreach (var x in stopWalk)
            {
                if (x >= 27255.0) stopIn = true;   /* the stop is dragged to 27,251.50 once the price is above it */
                if (open2 && stopIn && x <= 27251.50 + 1e-9) { exitPx = 27251.50; realized2 = q2 * pv2 * (exitPx - e2) - c2 * q2; open2 = false; }   /* the stop fills AT its price (the practice trader does the same, below) */
                tpS.Mark(realized2, open2 ? op2(x) : 0, "");
            }
            var afterX = KeystonePracticeTrail.Of(tpS, tpS.Closed, q2, 27250.0, 27250.0, pv2, 0);
            Console.WriteLine("      📐 stop in profit: peak +$" + tpS.Peak.ToString("0.00") + " (gross $180 − $16 costs) · floor $" + tpS.FloorNow(0).ToString("0.00") + " · realized +$" + realized2.ToString("0.00") + " · room $" + afterX.Room.ToString("0.00") + " · given back $" + afterX.GivenBack.ToString("0.00") + " · to lock $" + afterX.ToLock.ToString("0.00"));
            Check(Math.Abs(realized2 - 12.0) < 1e-9 && Math.Abs(tpS.Peak - 164) < 1e-9 && Math.Abs(tpS.FloorNow(0) + 1836) < 1e-9 && Math.Abs(afterX.Equity - 12) < 1e-9 && Math.Abs(afterX.Room - 1848) < 1e-9 && Math.Abs(afterX.GivenBack - 152) < 1e-9 && Math.Abs(afterX.ToLock - 1988) < 1e-9 && !tpS.Blown,
                "📐 a stop moved into profit: realized +$12.00 (8 × $2 × 1.75 − $16), the peak keeps +$164 (= $180 gross − $16 costs), floor −$1,836, ROOM $1,848, GIVEN BACK $152, TO LOCK $1,988", tpS.Peak + " / " + afterX.Room + " / " + afterX.GivenBack + " / " + afterX.ToLock);
            Check(Math.Abs((afterX.Room) - (2000 - afterX.GivenBack)) < 1e-9, "📐 ROOM LEFT shrinks by exactly what was given back from the peak (room = $2,000 − given back)", afterX.Room + " vs " + afterX.GivenBack);
            // the practice trader itself: the profit stop fills AT 27,251.50, the floor kept the trade's best
            var lt = KeystoneReplayTrader.LiveStart(new List<KeystoneArcBar> { new KeystoneArcBar { Symbol = "MNQ", Time = new DateTime(2026, 3, 4, 10, 0, 0), Open = e2, High = e2, Low = e2, Close = e2 } }, "MNQ", new DateTime(2026, 3, 4, 10, 0, 0));
            lt.Buy(q2); lt.TargetPts = 15.5; double cT = KeystoneMoveStudy.Cost("MNQ"); var pr2 = KeystonePractice.Presets().First(x => x.Name.StartsWith("TPT PRO 50K funded")).Copy(); pr2.ApplyTyped(); pr2.CloseAtGoal = false;
            var tB = new DateTime(2026, 3, 4, 10, 1, 0); double hiT = e2;
            foreach (var x in stopWalk) { hiT = Math.Max(hiT, x); if (x >= 27255.0 && lt.Position != 0) lt.StopPts = -1.75; /* dragged into profit */ lt.LiveUpdate(new KeystoneArcBar { Symbol = "MNQ", Time = tB, Open = e2, High = hiT, Low = Math.Min(e2, x), Close = x }, tB); pr2.Mark(lt.Realized, lt.Position == 0 ? 0 : lt.Unrealized - cT * Math.Abs(lt.Position), ""); }
            var fl2 = lt.Fills.LastOrDefault();
            Check(lt.Position == 0 && fl2 != null && fl2.Reason == "PROFIT STOP" && Math.Abs(lt.Realized - (q2 * pv2 * 1.75 - cT * q2)) < 1e-6 && Math.Abs(pr2.Peak - (q2 * pv2 * 11.25 - cT * q2)) < 1e-6 && Math.Abs(pr2.FloorNow(0) - (pr2.Peak - 2000)) < 1e-9,
                "📐 the studio's practice trader: the profit stop fills AT 27,251.50 (realized 8 × $2 × 1.75 − costs), the peak keeps the trade's best (27,261.00), the floor never drops back", (fl2 == null ? "no fill" : fl2.Reason) + " / " + lt.Realized + " / " + pr2.Peak);
        }
        {   // 📐 BUILD 11j — the user's scenario (TPT PRO 50K funded): 60 MNQ long @ 27,246.50, goingUp to 27,262.00 tick by tick, then back towards 27,240.00
            var tp = KeystonePractice.Presets().First(x => x.Name.StartsWith("TPT PRO 50K funded")).Copy(); tp.ApplyTyped(); tp.CloseAtGoal = false;
            double pv = 2, c = KeystoneMoveStudy.Cost("MNQ"), avg = 27246.50; int q = 60; Func<double, double> opn = x9 => q * pv * (x9 - avg) - c * q;
            var tickPath = new List<double>(); for (double pq = avg; pq <= 27262.0 + 1e-9; pq += 0.25) tickPath.Add(Math.Round(pq, 2)); for (double pq = 27261.75; pq >= 27240.0 - 1e-9; pq -= 0.25) tickPath.Add(Math.Round(pq, 2));
            var at0 = KeystonePracticeTrail.Of(tp, 0, q, avg, avg, pv, c);
            KeystonePracticeTrail top = null, mid = null; double blownAt = double.NaN; bool goingUp = true;
            foreach (var pq in tickPath)
            {
                if (pq >= 27262.0 - 1e-9) goingUp = false; var hit = tp.Mark(0, opn(pq), "");
                if (Math.Abs(pq - 27262.0) < 1e-9 && top == null) top = KeystonePracticeTrail.Of(tp, 0, q, avg, pq, pv, c);
                if (!goingUp && Math.Abs(pq - 27250.0) < 1e-9) mid = KeystonePracticeTrail.Of(tp, 0, q, avg, pq, pv, c);
                if (hit == "BLOWN" && double.IsNaN(blownAt)) blownAt = pq;
            }
            double peakX = 120 * (27262.0 - avg) - 60 * c, floorX = peakX - 2000, floorPxX = avg + (floorX + c * q) / (q * pv);
            Console.WriteLine("      📐 60 MNQ long @ 27,246.50 (costs $" + c.ToString("0.00") + " / contract round trip): at the start floor @ " + at0.FloorPx.ToString("0.00") + " (−$2,000), lock @ " + at0.LockPx.ToString("0.00")
                + " • at 27,262: peak +$" + top.Peak.ToString("0.00") + ", floor $" + top.Floor.ToString("0.00") + " @ " + top.FloorPx.ToString("0.0000") + " (dies at " + KeystonePracticeTrail.DeathTick(top.FloorPx, q, 0.25).ToString("0.00") + "), room $" + top.Room.ToString("0.00") + " = " + top.RoomPts.ToString("0.000") + " pts, to the lock $" + top.ToLock.ToString("0.00")
                + " • back at 27,250: room $" + mid.Room.ToString("0.00") + ", given back $" + mid.GivenBack.ToString("0.00") + " • 💀 blown at " + blownAt.ToString("0.00"));
            Check(Math.Abs(top.Peak - peakX) < 1e-6 && Math.Abs(top.Floor - floorX) < 1e-6 && Math.Abs(top.FloorPx - floorPxX) < 1e-9 && Math.Abs(top.FloorPx - (27262.0 - 2000.0 / 120)) < 1e-9 && Math.Abs(top.Room - 2000) < 1e-6 && Math.Abs(top.RoomPts - 2000.0 / 120) < 1e-9 && Math.Abs(top.GivenBack) < 1e-9,
                "📐 at 27,262: the peak (incl. open, net of costs) = +$" + peakX.ToString("0.00") + ", the floor = peak − $2,000, its price 27,245.3333 (costs cancel), room $2,000 = 16.667 pts", top.Peak + " / " + top.Floor + " / " + top.FloorPx + " / " + top.Room);
            Check(Math.Abs(top.ToLock - (2000 - peakX)) < 1e-6 && Math.Abs(top.LockPx - (avg + (2000 + c * q) / (q * pv))) < 1e-9 && Math.Abs(at0.FloorPx - (avg + (-2000 + c * q) / (q * pv))) < 1e-9 && !top.Locked,
                "📐 the LOCK = equity +$2,000 net of costs (@ " + (avg + (2000 + c * q) / (q * pv)).ToString("0.0000") + "), the start floor @ " + at0.FloorPx.ToString("0.0000"), top.ToLock + " / " + top.LockPx);
            Check(mid != null && Math.Abs(mid.Equity - (120 * 3.5 - 60 * c)) < 1e-6 && Math.Abs(mid.Room - 560) < 1e-6 && Math.Abs(mid.GivenBack - 1440) < 1e-6 && Math.Abs(mid.RoomPts - 560.0 / 120) < 1e-9 && Math.Abs(mid.Floor - floorX) < 1e-6,
                "📐 back at 27,250: the floor did not move down, room $560 (4.667 pts), given back $1,440", mid == null ? "null" : mid.Room + " / " + mid.GivenBack);
            Check(Math.Abs(blownAt - 27245.25) < 1e-9 && KeystonePracticeTrail.DeathTick(top.FloorPx, q, 0.25) == 27245.25 && tp.Blown && !tp.Locked, "📐 the reversal to 27,240 blows it at the first tick through the floor: 27,245.25", blownAt.ToString());
            var race = KeystonePractice.Presets().First(x => x.Race).Copy();
            Check(race.Name == KeystonePractice.RaceName && race.RaceQty == 10 && race.RaceTp == 2000 && race.Kind == "TPT" && race.DdType == "INTRADAY" && race.Dd == 2000 && KeystonePractice.RaceSizes.SequenceEqual(new[] { 5, 10, 15, 20, 30, 40, 60 }) && KeystonePractice.RaceNote.Contains("MNQ 10 @9:30 trend ≈ 40% lock (36% day 1); MNQ 5 ≈ 39%; MNQ 60 ≈ 31%. No edge ceiling ≈ 37%."),
                "🎯 LOCK RACE preset: MNQ 10 by default (5 / 10 / 15 / 20 / 30 / 40 / 60), +$2,000 lock, stop = the trailing floor (TPT PRO rules) + the research note");
            Check(KeystonePractice.RaceTps.SequenceEqual(new double[] { 2000, 1000 }) && KeystonePractice.RaceSls.SequenceEqual(new double[] { 0, 1000, 500, 250 })
                && KeystonePractice.RaceNextTp(2000, 0, 2000) == 2000 && KeystonePractice.RaceNextTp(2000, 1250, 2000) == 750 && KeystonePractice.RaceNextTp(1000, 1250, 2000) == 750 && KeystonePractice.RaceNextTp(1000, -300, 2000) == 1000
                && KeystonePractice.RaceStopUsd(0, 1700) == 1700 && KeystonePractice.RaceStopUsd(500, 1700) == 500 && KeystonePractice.RaceStopUsd(1000, 640) == 640
                && KeystonePractice.BeTrigger(1000) == 750 && Math.Abs(KeystonePractice.BeStopPts(1.74, 2, 0.25) - 1.0) < 1e-9 && Math.Abs(KeystonePractice.BeStopPts(2.24, 10, 0.1) - 0.3) < 1e-9
                && KeystonePractice.RaceNote.Contains("9:30 trend MNQ 5, TP $2K, SL $500 ≈ 41–43%") && KeystonePractice.RaceNote.Contains("4:00 trend MNQ 5, TP $1K, SL $250, BE 75% ≈ 45%"),
                "🎯 LOCK RACE choices: TP $2,000 / $1,000 (the next trade aims for what is still missing), SL floor / $1,000 / $500 / $250 (the tighter of it and the floor), BE at 75 % → entry + costs");
            {   // the 9:30 side = the last 60 minutes' close vs open
                var d0 = new DateTime(2026, 3, 4); var upB = new List<KeystoneArcBar>(); var dnB = new List<KeystoneArcBar>();
                for (int i = 1; i <= 120; i++) { var t = d0.AddHours(8).AddMinutes(i); upB.Add(new KeystoneArcBar { Time = t, Open = 100 + i * 0.25, Close = 100 + i * 0.25 + 0.25, High = 101 + i, Low = 99 }); dnB.Add(new KeystoneArcBar { Time = t, Open = 200 - i * 0.25, Close = 200 - i * 0.25 - 0.25, High = 201, Low = 100 }); }
                Check(KeystonePractice.RaceSide(upB, d0) == 1 && KeystonePractice.RaceSide(dnB, d0) == -1 && KeystonePractice.RaceSide(upB.Take(40).ToList(), d0) == 0, "🎯 LOCK RACE side at 9:30: the last 60 minutes upB → LONG, down → SHORT, too few bars → none");
            }
        }
        {   // 🎯 BUILD 11f — the PRACTICE ACCOUNT: the peak (open profit included), the floor never wentDown, the TPT funded phases, the goal close, the withdrawals, the status
            Func<string, KeystonePractice> pre = nm => KeystonePractice.Presets().First(p => p.Name.StartsWith(nm)).Copy();
            var ev = new KeystonePractice { Name = "t", DdType = "INTRADAY", Size = 50000, Dd = 2000, Target = 3000, MaxQty = 40 };
            ev.Mark(0, 1500, ""); ev.Mark(200, 0, "");   /* a long: +$1,500 open, closed at +$200 */
            Check(Math.Abs(ev.Peak - 1500) < 1e-9 && Math.Abs(ev.FloorNow(0) + 500) < 1e-9 && Math.Abs(ev.Room(0) - 700) < 1e-9 && ev.Status(0).Contains("peak +$1,500 · floor (peak − $2,000) −$500 · room $700"), "🎯 a long +$1,500 open → closed +$200: peak 1,500 · floor −500 · room 700 (the status says so)", ev.Status(0));
            var sh = new KeystonePractice { Name = "t", DdType = "INTRADAY", Size = 50000, Dd = 2000, Target = 3000 };
            double avgS = 20000, lowS = 19925, pv = 2; int qS = -10; sh.SeeBest(0, qS * (lowS - avgS) * pv); sh.Mark(200, 0, "");   /* a short of 10 MNQ: the bar's LOW gave +$1,500 */
            Check(Math.Abs(sh.Peak - 1500) < 1e-9 && Math.Abs(sh.FloorNow(0) + 500) < 1e-9 && Math.Abs(sh.Room(0) - 700) < 1e-9, "🎯 the same as a short, the peak from the bar's low", sh.Peak + " / " + sh.FloorNow(0));
            var mono = new KeystonePractice { Name = "t", DdType = "INTRADAY", Size = 50000, Dd = 2000, Target = 1e9 }; var rnd = new Random(5); double prevF = double.MinValue, cl = 0; bool wentDown = false;
            for (int i = 0; i < 2000 && !mono.Blown; i++) { double op = (rnd.NextDouble() - 0.45) * 900; if (i % 7 == 0) { cl += op; op = 0; } mono.Mark(cl, op, ""); double fM = mono.FloorNow(op); if (fM < prevF - 1e-9) wentDown = true; prevF = Math.Max(prevF, fM); }
            var eod = new KeystonePractice { Name = "t", DdType = "EOD", Size = 50000, Dd = 2000, Target = 1e9 }; eod.Mark(0, 1800, ""); double fE0 = eod.FloorNow(1800); eod.EndOfDay(600);
            Check(!wentDown && Math.Abs(fE0 + 2000) < 1e-9 && Math.Abs(eod.FloorNow(0) + 1400) < 1e-9 && double.IsNaN(new KeystonePractice { DdType = "STATIC" }.PeakNow(0)), "🎯 the floor never moves wentDown (2,000 random looks); EOD = the best END-OF-DAY balance (an open +$1,800 doesn't move it); STATIC has no peak");
            var tpt = pre("TPT PRO 50K funded"); tpt.ApplyTyped();
            Check(tpt.Kind == "TPT" && tpt.Funded && tpt.TargetLabel == "TRAILING TO BEAT $" && tpt.Target == 2000 && tpt.LockAt == 0 && tpt.GoalName == "beat the trailing" && Math.Abs(tpt.Goal - 2000) < 1e-9 && tpt.Status(0).Contains("BEAT THE TRAILING DRAWDOWN: $2,000 to go") && !tpt.Line().Contains("3,000"),
                "🎯 TPT PRO funded phase 1: BEAT THE TRAILING DRAWDOWN (+$2,000 best equity, open profit included) — no '+$3,000 target'", tpt.Status(0));
            string goal = tpt.GoalHit(2100); string hitL = tpt.Mark(0, 2100, ""); double fL = tpt.FloorNow(2100); tpt.Mark(0, 300, ""); tpt.Mark(1800, 0, "");
            Check(goal == "LOCK" && hitL == "LOCKED" && tpt.Locked && Math.Abs(fL) < 1e-9 && Math.Abs(tpt.FloorNow(0)) < 1e-9 && !tpt.Passed && tpt.How.StartsWith("🔒 TRAILING BEATEN: the floor is locked at the start ($50,000)"),
                "🎯 the lock at peak ≥ +$2,000: 🔒 TRAILING BEATEN, the floor stays at the start afterwards (a funded account never 'passes'); ✅ CLOSE EVERYTHING sees it", tpt.How);
            tpt.Mark(2900, 0, ""); double can = tpt.CanWithdraw(0); double got = tpt.Withdraw(0, 300); double can2 = tpt.CanWithdraw(0);
            Check(Math.Abs(can - 900) < 1e-9 && Math.Abs(got - 300) < 1e-9 && Math.Abs(tpt.ToYou - 240) < 1e-9 && Math.Abs(can2 - 600) < 1e-9 && tpt.Status(0).Contains("💰 WITHDRAWABLE NOW $600 (balance − the $2,000 buffer)") && tpt.Status(0).Contains("withdrawn so far $300 (you $240)") && tpt.CanWithdraw(50) == 0,
                "🎯 TPT phase 2: WITHDRAWABLE NOW = balance − the $2,000 buffer, any amount up to it (80 % to you), withdrawn so far; flat only", tpt.Status(0));
            var e2 = pre("Lucid 50K eval"); e2.Mark(0, 2990, ""); string gh0 = e2.GoalHit(2990); e2.Mark(0, 3010, ""); string gh1 = e2.GoalHit(3010); string ps = e2.Mark(3010, 0, "");
            var luc = pre("Lucid 50K funded"); for (int d = 0; d < 5; d++) { luc.Mark(160 * (d + 1), 0, ""); luc.EndOfDay(160 * (d + 1)); }
            Check(gh0 == "" && gh1 == "TARGET" && ps == "PASSED" && e2.CloseAtGoal && !luc.CloseAtGoal && luc.Kind == "PAYOUT" && luc.Qual == 5 && Math.Abs(luc.CanWithdraw(0) - 400) < 1e-9 && luc.TargetLabel == "PAYOUT NEED $" && pre("MFF Studio 002 • funded").TargetLabel == "WITHDRAW ≥ $" && pre("Lucid 50K eval").TargetLabel == "TARGET $",
                "🎯 the eval target closes everything + passes; Lucid funded: 5 qualifying days (≥ $150) + $500 profit → a payout (50 %); the TARGET field's label per kind");
            Check(KeystonePractice.Tally(5, 3, 2, 4) == "attempts 5 · passed 3 · blown 2 · pass rate 60 % · avg days to pass 1.3", "🔁 the attempts' tally", KeystonePractice.Tally(5, 3, 2, 4));
            var tile = pre("TPT PRO 50K funded"); tile.ApplyTyped(); tile.Mark(0, 1500, ""); var tm = tile.Tile(200);
            Check(Math.Abs(tm.Peak - 1500) < 1e-9 && tm.TopLabel == "beat the trailing" && Math.Abs(tm.Top - 2000) < 1e-9 && tm.Kind == "INTRADAY" && tm.PeakFrac > tm.BalFrac, "🎯 the animated bar's model: the PEAK marker above the balance (the given-back segment), the goal = beat the trailing +$2,000");
        }
        {   // 📜 / 🧪 / 📦 BUILD 11f — the trades log (CSV round trip, totals, streaks, filters), the probe's decisions, the ticks export
            var d0 = new DateTime(2026, 3, 10, 9, 31, 5); var rows = new List<KeystonePracticeTrade>();
            double[] nets = { 120, -40, 60, 30, -50, -20, -10, 200 };
            for (int i = 0; i < nets.Length; i++) rows.Add(new KeystonePracticeTrade { Entry = d0.AddMinutes(i * 3).AddDays(i >= 6 ? 1 : 0), Exit = d0.AddMinutes(i * 3).AddSeconds(41).AddDays(i >= 6 ? 1 : 0), Side = i % 2 == 0 ? "BUY" : "SELL", Qty = 25, Adds = i == 0 ? 1 : 0, EntryPx = 29789.5, ExitPx = 29791.75, Net = nets[i], Points = 2.25, Mfe = Math.Max(nets[i], 80), Mae = Math.Min(nets[i], -30), How = nets[i] > 0 ? "TARGET" : "STOP", Attempt = i < 4 ? 1 : 2, Preset = "TPT PRO 50K funded", Note = i == 0 ? "🧪 probe → ADD 24 at 11 s (+3 ticks ≥ +2)" : "" });
            var back = KeystonePracticeTrades.Load(KeystonePracticeTrades.Header + "\n" + string.Join("\n", rows.Select(KeystonePracticeTrades.Line)));
            var tot = KeystonePracticeTrades.Totals(rows); Func<string, string> T = k => tot.First(x => x[0] == k)[1];
            Check(back.Count == 8 && back[0].Seconds == 41 && back[0].Adds == 1 && back[0].Note.StartsWith("🧪 probe → ADD 24") && Math.Abs(back[7].Net - 200) < 1e-9 && back[3].Attempt == 1 && back[0].Entry.ToString("HH:mm:ss") == "09:31:05",
                "📜 a trade row keeps its seconds, size + adds, prices, $, MFE / MAE, how it ended, the attempt, the preset and the probe's branch (CSV round trip)");
            Check(T("trades") == "8" && T("win %").StartsWith("50.0 %") && T("expectancy per trade") == "+$36.25" && T("best / worst streak") == "2 wins / 3 losses" && T("avg win / avg loss") == "+$102.50 / −$30.00" && tot.Count(x => x[0].StartsWith("P/L ")) == 2 && T("avg duration") == "41 s",
                "📜 the totals: win %, avg win / loss, expectancy, best / worst streak, P/L per day", string.Join(" | ", tot.Skip(1).Select(x => x[0] + "=" + x[1])));
            Check(KeystonePracticeTrades.Filter(rows, "TODAY", d0.Date, 1).Count == 6 && KeystonePracticeTrades.Filter(rows, "ATTEMPT", d0.Date, 2).Count == 4 && KeystonePracticeTrades.Filter(rows, "ALL", d0.Date, 1).Count == 8, "📜 the filters today / attempt / all");
            Check(KeystoneProbe.AfterWait(2, 2, "CLOSE") == "ADD" && KeystoneProbe.AfterWait(1.5, 2, "CLOSE") == "CLOSE" && KeystoneProbe.AfterWait(-3, 2, "WAIT") == "WAIT"
                && KeystoneProbe.OnCandle(1, 100, 101, d0, d0.AddMinutes(3)) == "ADD" && KeystoneProbe.OnCandle(-1, 100, 101, d0, d0.AddMinutes(3)) == "" && KeystoneProbe.OnCandle(-1, 100, 101, d0.AddMinutes(3), d0.AddMinutes(3)) == "CLOSE" && KeystoneProbe.OnCandle(-1, 101, 100, d0, d0.AddMinutes(3)) == "ADD",
                "🧪 the probe: after N s ≥ +X ticks → ADD, else (a) CLOSE or (b) WAIT for a 1-minute candle closing the trade's way within M minutes, then ADD / CLOSE");
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ks-ticks-" + Guid.NewGuid().ToString("N")); var dd = new DateTime(2026, 3, 9);
            KeystoneTickCache.Save(dir, "MNQ", dd, new List<KeystoneTick> { new KeystoneTick { Time = dd.AddHours(9.5).AddMilliseconds(7), Price = 20000.25, Volume = 3 }, new KeystoneTick { Time = dd.AddHours(9.5).AddSeconds(11), Price = 20001, Volume = 2 } }, "TEST");
            string outF = System.IO.Path.Combine(dir, "exports", "t.csv.gz"); int nT = KeystoneTickCache.ExportCsvGz(dir, new[] { "MNQ", "MGC" }, dd.AddDays(-1), dd.AddDays(1), outF); string txt;
            using (var fs = System.IO.File.OpenRead(outF)) using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress)) using (var sr = new System.IO.StreamReader(gz)) txt = sr.ReadToEnd();
            Check(nT == 2 && txt.Split('\n').Length == 4 && txt.Contains("MNQ,2026-03-09 09:30:00.007,20000.25,3,") && txt.Contains("MNQ,2026-03-09 09:30:11.000,20001.00,2,"), "📦 EXPORT TICKS: the cached ticks of the range → one CSV.gz (time to the ms, price, size)", txt);
            try { System.IO.Directory.Delete(dir, true); } catch { }
        }
        {   // ✋ BUILD 2026-11-11h: dragging the TP / SL over the entry (the user's short 1 @ 27016): the drag owns the mouse, the entry is never grabbed,
            // the live label reads points + $ after costs, Esc restores, the order is modified once — on release (DragChart = a stand-in of the chart's handlers)
            var ch = new DragChart { Entry = 27016, Side = -1, Qty = 1, Last = 27010, StopPts = 20, TargetPts = 40 };
            ch.Down("TARGET");
            foreach (var dpx in new[] { 26980.0, 27000.10, 27015.9, 27016.0, 27016.13, 27030.0, 27016.0, 26990.0, 26960.0 }) ch.MoveTo(dpx);
            Check(ch.Drag.Kind == "TARGET" && ch.Grabbed.All(g => g == "TARGET") && !ch.Grabbed.Contains("ENTRY") && ch.Entry == 27016 && ch.EntryHandleY == 27016 && ch.EntrySelected == false && !ch.Drag.EntryInteractive && Math.Abs(ch.Drag.EntryOpacity - 0.3) < 1e-9,
                "✋ a TP drag that crosses the entry never grabs / moves / selects the entry (it fades to 30 %, the drag keeps its kind)", string.Join(",", ch.Grabbed));
            Check(ch.Seen.SequenceEqual(new[] { 26980.0, 27000.0, 27016.0, 27016.0, 27016.25, 27030.0, 27016.0, 26990.0, 26960.0 }), "✋ the dragged line follows the mouse rounded to the tick only (no snapping to the entry)", string.Join(" ", ch.Seen));
            Check(ch.Modifies == 0 && ch.TargetPts == 40, "✋ nothing is sent while dragging (the target stays at 40 pts until the release)");
            ch.Up();
            Check(ch.Modifies == 1 && Math.Abs(ch.TargetPts - 56) < 1e-9 && ch.Drag.Commits == 1 && !ch.Drag.Active && ch.Drag.EntryInteractive && ch.Drag.EntryOpacity == 1, "✋ the order is modified ONCE, on release (TP 56 pts = 26960), then the entry is back to normal", ch.Modifies + " " + ch.TargetPts);
            ch.Up(); Check(ch.Modifies == 1, "✋ a second release sends nothing");
            ch.Down("STOP"); ch.MoveTo(27030); ch.MoveTo(27045.6);
            Check(ch.Drag.Price == 27045.5 && ch.Modifies == 1, "✋ the stop previews at 27045.50");
            ch.Esc(); ch.Up();
            Check(ch.Modifies == 1 && ch.StopPts == 20 && ch.LastCancel == 27036 && !ch.Drag.Active, "✋ Esc cancels the drag: the stop stays at 27036 (20 pts), nothing is sent, the release after it does nothing", ch.LastCancel + " " + ch.StopPts);
            ch.Down("STOP"); ch.MoveTo(27000); ch.MoveTo(27005);
            Check(ch.Drag.Price == 27010.5 && ch.Drag.Clamped, "✋ a short's stop can't be dragged through the price (27010) into an instant fill: held 2 ticks above it (27010.50) + a note", ch.Drag.Price.ToString());
            ch.MoveTo(27020); Check(ch.Drag.Price == 27020 && !ch.Drag.Clamped, "✋ back on the loss side: free again");
            ch.Up(); Check(ch.Modifies == 2 && Math.Abs(ch.StopPts - 4) < 1e-9, "✋ released: one modification, stop 4 pts (27020)");
            double c = 1.74;
            Check(KeystoneBracketDrag.Label("TARGET", -1, 27016, 26895.25, 2, 1, c) == "TP 120.75 pts · +$240", "✋ the live label: TP 120.75 pts on 1 MNQ = +$241.50 − $1.74 costs → \"TP 120.75 pts · +$240\"", KeystoneBracketDrag.Label("TARGET", -1, 27016, 26895.25, 2, 1, c));
            Check(KeystoneBracketDrag.Label("TARGET", 1, 100, 221, 2, 1, 0) == "TP 121.00 pts · +$242" && KeystoneBracketDrag.Label("STOP", -1, 27016, 27028.25, 2, 1, c) == "SL 12.25 pts · −$26.24"
                && KeystoneBracketDrag.Label("STOP", -1, 27016, 27000, 2, 1, c) == "SL 🔒 16.00 pts · +$30.26" && KeystoneBracketDrag.Label("TARGET", 1, 20000, 20010, 2, 3, c) == "TP 10.00 pts · +$54.78",
                "✋ the label: points from the entry and $ for the size after costs (stop = a loss incl. costs, a profit stop 🔒 = locked $, 3 contracts)", KeystoneBracketDrag.Label("STOP", -1, 27016, 27028.25, 2, 1, c));
            // the label sits on the line, never under the entry's label (entry label band 186..214, the chart 0..500, a label 21 px tall)
            double a1 = KeystoneBracketDrag.LabelTop(300, 21, 186, 214, 0, 500), a2 = KeystoneBracketDrag.LabelTop(220, 21, 186, 214, 0, 500), a3 = KeystoneBracketDrag.LabelTop(200, 21, 186, 214, 0, 500), a4 = KeystoneBracketDrag.LabelTop(180, 21, 186, 214, 0, 500);
            Func<double, bool> clear = t => !KeystoneBracketDrag.Overlaps(t, t + 21, 186, 214);
            Check(a1 == 276 && a2 == 223 && clear(a2) && clear(a3) && clear(a4) && a4 == 156 && KeystoneBracketDrag.LabelTop(5, 21, 186, 214, 0, 500) == 0 && a3 == 163,
                "✋ the live label: above its line, flipped below near the entry's label, past it when the line is on the entry — never under the entry label", a1 + " " + a2 + " " + a3 + " " + a4);
        }
        Console.WriteLine(failures == 0 ? "ALL REPLAY TRADER TESTS PASSED" : failures + " REPLAY TRADER TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}

// ✋ 11h: a stand-in of the chart's TP / SL mouse handlers (down → move → up / Esc) around the real KeystoneBracketDrag; Modifies counts the order changes
public sealed class DragChart
{
    public double Entry, Last, StopPts, TargetPts, Tick = 0.25, EntryHandleY, LastCancel = double.NaN; public int Side, Qty, Modifies; public bool EntrySelected;
    public string DragKind; public KeystoneBracketDrag Drag = new KeystoneBracketDrag(); public List<string> Grabbed = new List<string>(); public List<double> Seen = new List<double>();
    public void Down(string kind) { if (Drag.Active) Drag.Cancel(); DragKind = kind; EntryHandleY = Entry; }
    public void MoveTo(double price)
    {
        if (DragKind == null) return;
        if (!Drag.Active) Drag.Begin(DragKind, DragKind == "STOP" ? Entry - Side * StopPts : Entry + Side * TargetPts);
        // what the chart would hit under the mouse: the entry when the mouse is on it — the drag must keep its own kind
        string under = Math.Abs(price - Entry) < 1 ? "ENTRY" : DragKind; string g = Drag.Grab(under); Grabbed.Add(g); if (g == "ENTRY") EntrySelected = true;
        DragKind = g; Seen.Add(Drag.Move(price, Tick, Side, Last));
    }
    public void Esc() { if (Drag.Active) { LastCancel = Drag.Cancel(); DragKind = null; } }
    public void Up()
    {
        string was = DragKind; DragKind = null; if (was == null || !Drag.Active) return;
        double p = Drag.Release(); if (double.IsNaN(p)) return;
        if (was == "STOP") StopPts = -Side * (p - Entry); else TargetPts = Side * (p - Entry);
        Modifies++;
    }
}

