// the 🤖 AUTO automation against the BACKTEST broker (the same commands as MT5): entry on the break / typed, the add-on on the break of the
// recent low / a fixed price move / a fixed $ loss, the target closes the basket, max drawdown, pause vs the next break, a rewind stops it
using System; using System.Collections.Generic; using System.Linq; using KeystoneMT5;
public static class AutoTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static readonly DateTime Day0 = new DateTime(2026, 10, 20), Day1 = new DateTime(2026, 10, 21);
    // one-minute candles from a list of closes (each candle: open = the last close, high / low 0.10 beyond)
    static List<Mt5Bar> Path(DateTime start, double first, IEnumerable<double> closes)
    {
        var res = new List<Mt5Bar>(); double o = first; int i = 0;
        foreach (var c in closes) { res.Add(new Mt5Bar { Time = start.AddMinutes(i++), O = o, C = c, H = Math.Round(Math.Max(o, c) + 0.1, 2), L = Math.Round(Math.Min(o, c) - 0.1, 2), V = 100, Spread = 10 }); o = c; }
        return res;
    }
    static IEnumerable<double> Line(double from, double step, int n) { for (int i = 1; i <= n; i++) yield return Math.Round(from + step * i, 2); }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static Tuple<SimBroker, HedgeAuto> Run(List<Mt5Bar> test, AutoSettings s, Action<SimBroker, HedgeAuto> mid = null, Action<HedgeAuto> prep = null)
    {
        var warm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30));
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm.Concat(test).ToList()); if (err != "") throw new Exception(err);
        var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); if (prep != null) prep(au);
        var chk = au.Check(sb, s); if (chk != "") throw new Exception(chk);
        au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
        for (int k = 0; k < 4000 && !sb.Ended; k++) { sb.Advance(0.25); if (mid != null && k == 3) mid(sb, au); }
        return Tuple.Create(sb, au);
    }
    static AutoSettings S() { return new AutoSettings { Symbol = "XAUUSD", Tf = 1, Lookback = 1, EntryLots = 0.1, AddLots = 0.2, Target = 20, FirstDir = "AUTO", NextStart = "BREAK" }; }

    public static int Main4()
    {
        // 1. AUTO: the climb breaks the last candle's high → BUY 0.1; +$20 (a $2 move + the spread) closes it; the next break opens #2
        var r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), S()); var au = r.Item2;
        var it = au.Iterations.FirstOrDefault();
        Check(it != null && it.FirstDir == "BUY" && it.Reason == "TARGET" && it.Result >= 20 && it.Result < 26, "auto: the break of the high buys, the basket closes at the $20 target", it == null ? "none" : it.FirstDir + " " + it.Reason + " " + it.Result);
        Check(au.Iterations.Count(x => x.Done) >= 2 && au.Trades.All(t => t.Side == "BUY" && Math.Abs(t.Lots - 0.1) < 1e-9), "next = the very next break: more iterations, every one a 0.10 BUY", au.Iterations.Count + " / " + string.Join(",", au.Trades.Select(t => t.Side + t.Lots)));

        // 2. typed SELL in a climb, add-on on a fixed $2 price move: SELL 0.10 → BUY 0.20 (net long 0.10) → the target
        var s2 = S(); s2.FirstDir = "SELL"; s2.HedgeMode = "PRICE"; s2.HedgePrice = 2; s2.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, 0.25, 80)), s2); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(au.Trades.Count == 2 && au.Trades[0].Side == "SELL" && au.Trades[1].Side == "BUY" && Math.Abs(au.Trades[1].Lots - 0.2) < 1e-9, "typed SELL first, the add-on BUY 0.20 after a $2 move against it", string.Join(" | ", au.Trades.Select(t => t.Side + " " + t.Lots + " @" + t.Price)));
        Check(it != null && it.Reason == "TARGET" && it.Sells == 1 && it.Buys == 1 && it.AddOns == 1 && it.MaxDd < -15 && !au.Running, "the iteration: 1 sell, 1 buy, 1 add-on, its drawdown kept, max 1 iteration → stop", it == null ? "none" : it.Reason + " dd " + it.MaxDd + " running " + au.Running);
        Check(Math.Abs(au.Trades[1].Price - (4002.0 + 0.1)) < 0.3, "the add-on opened ≈ $2 above the sell's fill", au.Trades[1].Price + "");
        Check(au.Story.Count >= 5 && au.Story.Any(x => x.Item2.Contains("ADD-ON BUY 0.2. WHY")) && au.Story.Any(x => x.Item2.Contains("TAKE PROFIT. WHY")), "story: the entry, the add-on and the take profit are explained", au.Story.Count + "");
        if (Environment.GetEnvironmentVariable("SHOW_STORY") == "1") AutoStoryPeek.Print(au);

        // 3. max drawdown $30, no add-on reachable (price mode $1000): BUY then a slide → closed at −$30, STOP
        var s3 = S(); s3.FirstDir = "BUY"; s3.HedgeMode = "PRICE"; s3.HedgePrice = 1000; s3.MaxDrawdown = 30;
        r = Run(Path(Day1, 4000, Line(4000, -0.5, 30)), s3); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(it != null && it.Reason == "MAX DRAWDOWN" && it.Result <= -30 && it.Result > -40 && !au.Running && r.Item1.State.Positions.Count == 0, "max drawdown: the basket closes at −$30 and the automation stops", it == null ? "none" : it.Reason + " " + it.Result);

        // 4. MONEY: BUY 0.10 ($10 a $1) and −$20 → the add-on SELL 0.20 ≈ $2 under the buy
        var s4 = S(); s4.FirstDir = "BUY"; s4.HedgeMode = "MONEY"; s4.HedgeMoney = 20; s4.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, -0.25, 20).Concat(Line(3995, 0.5, 40))), s4); au = r.Item2;
        Check(au.Trades.Count >= 2 && au.Trades[1].Side == "SELL" && Math.Abs(au.Trades[0].Price - 0.1 - au.Trades[1].Price - 2) < 0.35, "money: −$20 on 0.10 lots = the add-on SELL ≈ $2 below", string.Join(" | ", au.Trades.Select(t => t.Side + " " + t.Lots + " @" + t.Price)));

        // 5. PAUSE 120 s: after the win the same direction again 2 minutes later (market time)
        var s5 = S(); s5.FirstDir = "BUY"; s5.NextStart = "PAUSE"; s5.PauseSec = 120; s5.MaxIterations = 2;
        r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s5); au = r.Item2;
        var a1 = au.Iterations.ElementAtOrDefault(0); var a2 = au.Iterations.ElementAtOrDefault(1);
        Check(a1 != null && a2 != null && a2.FirstDir == "BUY" && (a2.Start - a1.End).TotalSeconds >= 120 && (a2.Start - a1.End).TotalSeconds < 200, "pause: iteration #2 opens the same way 2 minutes after #1", a2 == null ? "none" : (a2.Start - a1.End).TotalSeconds + " s");

        // 6. BREAK of the recent low as the add-on: BUY, then a drop under the last candle's low → SELL 0.20
        var s6 = S(); s6.FirstDir = "BUY"; s6.MaxIterations = 1;
        r = Run(Path(Day1, 4000, new[] { 4000.5, 4001.0, 3999.0, 3998.0, 4000.0, 4004.0, 4008.0 }.Concat(Line(4008, 0.5, 20))), s6); au = r.Item2;
        Check(au.Trades.Count >= 2 && au.Trades[1].Side == "SELL" && au.Trades[1].Why.Contains("recent low"), "break: the add-on SELL on the break of the recent low", string.Join(" | ", au.Trades.Select(t => t.Side + " " + t.Why)));
        if (Environment.GetEnvironmentVariable("SHOW_STORY") == "1") AutoStoryPeek.Print(au);

        // 7. a rewind of the backtest stops the automation (its orders are not replayed twice)
        r = Run(Path(Day1, 4000, Line(4000, 0.5, 60)), S(), (sb, a) => { sb.RewindTo(sb.StartTime); sb.Playing = true; });
        Check(!r.Item2.Running && r.Item2.StopReason.Contains("clock went back"), "rewind: the automation stops", r.Item2.StopReason);
        // 8. FVG: a bullish gap (the 3rd candle's low 4002.5 above the 1st candle's high 4000.3) → BUY on the retest into it
        Func<int, double, double, double, double, long, Mt5Bar> B = (i, o, h, l, c, v) => new Mt5Bar { Time = Day1.AddMinutes(i), O = o, H = h, L = l, C = c, V = v, Spread = 10 };
        var fv = new List<Mt5Bar> { B(0, 4000, 4000.3, 3999.8, 4000.2, 100), B(1, 4000.2, 4003, 4000.2, 4002.8, 100), B(2, 4002.8, 4004, 4002.5, 4003.8, 100), B(3, 4003.8, 4003.9, 4002.0, 4002.2, 100) };
        fv.AddRange(Path(Day1.AddMinutes(4), 4002.2, Line(4002.2, 0.5, 20)));
        var s8 = S(); s8.EntryMode = "FVG"; s8.MaxIterations = 1; r = Run(fv, s8); au = r.Item2;
        Check(au.Trades.Count >= 1 && au.Trades[0].Side == "BUY" && au.Trades[0].Why.StartsWith("ENTRY BUY — FVG") && au.Trades[0].Time >= Day1.AddMinutes(3) && au.Trades[0].Time < Day1.AddMinutes(4), "FVG: the retest of the bullish gap buys (in the 4th candle)", string.Join(" | ", au.Trades.Select(x => x.Time.ToString("HH:mm:ss") + " " + x.Why)));
        Check(au.Story.Any(x => x.Item2.Contains("FAIR VALUE GAP 4000.3–4002.5")), "FVG: the story names the gap and its candles", au.Story.FirstOrDefault(x => x.Item2.Contains("ENTRY"))?.Item2 ?? "none");

        // 9. VWAP: under the day's VWAP, then a cross up → BUY
        var s9 = S(); s9.EntryMode = "VWAP"; s9.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, -0.2, 10).Concat(Line(3998, 0.4, 30))), s9); au = r.Item2;
        Check(au.Trades.Count >= 1 && au.Trades[0].Side == "BUY" && au.Trades[0].Why.Contains("VWAP cross up"), "VWAP: the cross up buys", string.Join(" | ", au.Trades.Select(x => x.Why)));

        // 10. FLOW: a candle with 4× the tick volume closing at its high → BUY when its high breaks
        var fl = new List<Mt5Bar> { B(0, 4000, 4000.2, 3999.9, 4000.1, 100), B(1, 4000.1, 4002, 4000.0, 4001.95, 400), B(2, 4001.9, 4002.4, 4001.8, 4002.3, 100) };
        fl.AddRange(Path(Day1.AddMinutes(3), 4002.3, Line(4002.3, 0.5, 20)));
        var s10 = S(); s10.EntryMode = "FLOW"; s10.MaxIterations = 1; r = Run(fl, s10); au = r.Item2;
        Check(au.Trades.Count >= 1 && au.Trades[0].Side == "BUY" && au.Trades[0].Why.Contains("ORDER FLOW BUY") && au.Story.Any(x => x.Item2.Contains("× the tick volume")), "FLOW: the volume-surge candle's high breaks → BUY, explained", string.Join(" | ", au.Trades.Select(x => x.Why)));

        // 11. ADD TO THE WINNER: a climb, target $60 → BUY 0.1 + winner adds on new highs before the target
        var s11 = S(); s11.FirstDir = "BUY"; s11.Target = 60; s11.AddToWinner = true; s11.WinnerLots = 0.1; s11.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s11); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(it != null && it.WinnerAdds >= 1 && it.AddOns == 0 && au.Trades.All(x => x.Side == "BUY") && it.Reason == "TARGET", "add to the winner: more BUYs on new highs, no add-on, the target closes it", it == null ? "none" : it.WinnerAdds + " winner adds, " + it.AddOns + " add-ons, " + it.Reason);

        // 12. the gates: trading times, iterations a day, the news
        var s12 = S(); s12.Windows = "20:00-21:00"; r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s12); au = r.Item2;
        Check(au.Iterations.Count == 0 && au.Blocked.Contains("outside the trading times"), "times: nothing starts outside 20:00-21:00 New York (it is 17:00 there)", au.Blocked);
        s12.Windows = "16:30-18:00"; r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s12);
        Check(r.Item2.Iterations.Count > 0, "times: inside 16:30-18:00 New York it trades", r.Item2.Iterations.Count + "");
        var s13 = S(); s13.PerDay = 1; r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s13);
        Check(r.Item2.Iterations.Count == 1 && r.Item2.Blocked.Contains("of today are done"), "iterations a day: 1, then it waits for tomorrow", r.Item2.Iterations.Count + " " + r.Item2.Blocked);
        var s14 = S(); s14.NewsMode = "AVOID";
        r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s14, null, a => a.NewsNear = (tt, b0, a0) => "USD Non-Farm Payrolls 08:30");
        Check(r.Item2.Iterations.Count == 0 && r.Item2.Blocked.Contains("Non-Farm"), "news AVOID: no iteration around the event", r.Item2.Blocked);
        s14.NewsMode = "ONLY"; r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s14, null, a => a.NewsNear = (tt, b0, a0) => null);
        Check(r.Item2.Iterations.Count == 0 && r.Item2.Blocked.Contains("news-only"), "news ONLY: nothing without news", r.Item2.Blocked);

        // 13. DECODE learns a bot from what it does: our own automation (add-on = a fixed $2 move, ×2 lots, +$20 on the basket) in a choppy market
        var zz = new List<double>(); for (int i = 1; i <= 900; i++) zz.Add(Math.Round(4000 + 6 * Math.Sin(i / 9.0) + 2.5 * Math.Sin(i / 2.3), 2));
        var s15 = S(); s15.FirstDir = "BUY"; s15.HedgeMode = "PRICE"; s15.HedgePrice = 2; s15.NextStart = "PAUSE"; s15.PauseSec = 60;
        var w = new TradeWatch();
        var sb15 = new SimBroker(); var warm15 = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30));
        sb15.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm15.Concat(Path(Day1, 4000, zz)).ToList());
        var au15 = new HedgeAuto(); au15.Send = (c, a, l) => sb15.Command(c, a); sb15.OnTickHook = () => { au15.Step(sb15); w.Step(sb15); };
        au15.Start(sb15, s15, false); sb15.Playing = true; sb15.Speed = 60; for (int k = 0; k < 20000 && !sb15.Ended; k++) sb15.Advance(0.25);
        var key = w.Baskets.Select(x => x.Key).FirstOrDefault() ?? ""; var rules = string.Join("\n", w.Rules(key));
        if (Environment.GetEnvironmentVariable("SHOW_STORY") == "1") { Console.WriteLine(rules); foreach (var e in w.Events.Take(8)) Console.WriteLine("   " + e.Story); }
        Check(au15.Iterations.Count(x => x.Done) >= 3 && w.Baskets.Count(x => x.Done) >= 3, "decode: it saw the automation's baskets", au15.Iterations.Count + " / " + w.Baskets.Count);
        Check(rules.Contains("magic 0") || rules.Contains("magic"), "decode: the bot is named by symbol + magic", key);
        Check(rules.Contains("most likely a FIXED PRICE STEP of ≈ 2"), "decode: the hedge rule found = a fixed $2 price step", rules);
        Check(rules.Contains("TAKE PROFIT ≈ +$2"), "decode: the take profit found ≈ +$20 on the whole basket", rules);
        Check(rules.Contains("× the first: median 2"), "decode: the hedge lots = 2 × the first leg", rules);

        // 13b. DECODE learns a BREAK bot: add-ons on the break of the previous M1 candle's low / high (fixed when the leg opened)
        var s16 = S(); s16.FirstDir = "BUY"; s16.NextStart = "PAUSE"; s16.PauseSec = 60; var w2 = new TradeWatch();
        var sb16 = new SimBroker(); sb16.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 100000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm15.Concat(Path(Day1, 4000, zz)).ToList());
        var au16 = new HedgeAuto(); au16.Send = (c, a, l) => sb16.Command(c, a); sb16.OnTickHook = () => { au16.Step(sb16); w2.Step(sb16); };
        au16.Start(sb16, s16, false); sb16.Playing = true; sb16.Speed = 60; for (int k = 0; k < 20000 && !sb16.Ended; k++) sb16.Advance(0.25);
        var rules2 = string.Join("\n", w2.Rules(w2.Baskets.Select(x => x.Key).FirstOrDefault() ?? ""));
        if (Environment.GetEnvironmentVariable("SHOW_STORY") == "1") Console.WriteLine(rules2);
        Check(rules2.Contains("most likely the BREAK of the HIGH (buy) / LOW (sell) of the last 1 M1 candle when the last leg opened"), "decode: the hedge rule found = the break of the previous M1 candle (when the last leg opened)", rules2);

        // 14. DAY: the day replayed with every entry setup, ranked, saved and read back
        var day = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)).Concat(Path(Day1, 4000, zz.Take(600))).ToList();
        var ds = S(); ds.HedgeMode = "PRICE"; ds.HedgePrice = 2; ds.Target = 20;
        var dr = DayLab.Run(Gold(), day, Day1, ds, 10000, 10, null);
        Check(dr.Count == 4 && dr.Select(x => x.Mode).Distinct().Count() == 4 && dr.Zip(dr.Skip(1), (a, b) => a.Net >= b.Net).All(x => x) && dr.Sum(x => x.Iterations) > 0, "day: 4 setups replayed and ranked by net", string.Join(" | ", dr.Select(x => x.Mode + " " + x.Iterations + " it " + x.Net.ToString("0") + " " + x.Note)));
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-day-" + Guid.NewGuid().ToString("N") + ".csv"); DayLab.Save(tmp, dr); DayLab.Save(tmp, dr); var back = DayLab.Load(tmp); System.IO.File.Delete(tmp);
        Check(back.Count == 4 && Math.Abs(back.Sum(x => x.Net) - dr.Sum(x => x.Net)) < 0.05, "day: saved per day (saving again replaces it)", back.Count + "");
        if (Environment.GetEnvironmentVariable("SHOW_STORY") == "1") foreach (var x in dr) Console.WriteLine("   " + x.Csv() + " " + x.Note);
        return fails;
    }
}
public static class AutoStoryPeek { public static void Print(HedgeAuto a) { foreach (var s in a.Story) Console.WriteLine("   " + s.Item2); } }
