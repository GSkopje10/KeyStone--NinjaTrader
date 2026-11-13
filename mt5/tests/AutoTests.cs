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
    static Tuple<SimBroker, HedgeAuto> Run(List<Mt5Bar> test, AutoSettings s, Action<SimBroker, HedgeAuto> mid = null, Action<HedgeAuto> prep = null, Action<SimBroker> watch = null, int steps = 4000)
    {
        var warm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30));
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm.Concat(test).ToList()); if (err != "") throw new Exception(err);
        var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); if (prep != null) prep(au);
        var chk = au.Check(sb, s); if (chk != "") throw new Exception(chk);
        if (watch != null) watch(sb);
        au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
        for (int k = 0; k < steps && !sb.Ended; k++) { sb.Advance(0.25); if (mid != null && k == 3) mid(sb, au); }
        return Tuple.Create(sb, au);
    }
    static AutoSettings S() { return new AutoSettings { OnManual = "ADOPT", HedgeSize = "FIXED", Rolling = false, Swing = false, FvgTf = 1, Symbol = "XAUUSD", Tf = 1, Lookback = 1, EntryLots = 0.1, AddLots = 0.2, Target = 20, FirstDir = "AUTO", NextStart = "BREAK" }; }

    // ⏰ 9:30 STRADDLE: a NAS100-like symbol ($1 a point a lot), yesterday 9:30–16:00 NY ranged 200 points (X = 100 at 0.5 ×)
    static SimSpec Nas() { return new SimSpec { Name = "NAS100", Digits = 2, Point = 0.01, Contract = 1, TickValue = 0.01, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static Tuple<SimBroker, HedgeAuto> RunStrad(IEnumerable<double> afterOpen, Action<AutoSettings> tune, DateTime? openAt = null)
    {
        var bars = new List<Mt5Bar>();
        bars.AddRange(Path(Day0.AddHours(16).AddMinutes(30), 20000, Line(20000, 200.0 / 390, 390)));   // yesterday 9:30–16:00 NY: 20000 → 20200
        var start = openAt ?? Day1.AddHours(16).AddMinutes(30);
        if (openAt == null) bars.AddRange(Path(Day1.AddHours(15), 20000, Enumerable.Repeat(20000.0, (int)(start - Day1.AddHours(15)).TotalMinutes)));   /* no candles before a late start */
        var rest = afterOpen.ToList(); while (rest.Count < 400) rest.Add(rest.Count > 0 ? rest[rest.Count - 1] : 20000);
        bars.AddRange(Path(start, 20000, rest));
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "NAS100", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 100, Kind = "STANDARD", FixedSpread = 10 }, Nas(), bars); if (err != "") throw new Exception(err);
        var s = AutoSettings.Model("STRADDLE930", new AutoSettings { Symbol = "NAS100", NyOffsetHours = -7, StradRisk = 100, StradMaxLots = 5 }); if (tune != null) tune(s);
        var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb);
        var chk = au.Check(sb, s); if (chk != "") throw new Exception(chk);
        au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
        for (int k = 0; k < 40000 && !sb.Ended; k++) sb.Advance(0.25);
        return Tuple.Create(sb, au);
    }
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
        var s8 = S(); s8.FvgStack = false; s8.EntryMode = "FVG"; s8.MaxIterations = 1; r = Run(fv, s8); au = r.Item2;
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

        // 15. SWING levels: the swing high 4003 (higher than 2 candles each side) breaks → BUY; a lower candle high later is NOT the level
        var sw = new List<Mt5Bar> { B(0, 4000, 4001, 3999, 4000.5, 100), B(1, 4000.5, 4002, 4000, 4001.5, 100), B(2, 4001.5, 4003, 4001, 4002, 100), B(3, 4002, 4002.4, 4000.5, 4001, 100), B(4, 4001, 4001.8, 4000.2, 4001.2, 100), B(5, 4001.2, 4001.6, 4000.8, 4001.4, 100) };
        sw.AddRange(Path(Day1.AddMinutes(6), 4001.4, Line(4001.4, 0.4, 30)));
        var s17 = S(); s17.Swing = true; s17.SwingK = 2; s17.MaxIterations = 1; r = Run(sw, s17); au = r.Item2;
        Check(au.Trades.Count >= 1 && au.Trades[0].Side == "BUY" && Math.Abs(au.Trades[0].Level - 4003) < 1e-9 && au.Trades[0].Why.Contains("swing high"), "swing: the BUY waits for the swing high 4003 (not the last candle's 4001.6)", string.Join(" | ", au.Trades.Select(x => x.Why + " " + x.Level)));
        Check(au.Story.Any(x => x.Item2.Contains("the SWING HIGH = the M1 candle")) && au.Iterations[0].Ctx.Contains("setup=BREAK") && au.Iterations[0].Ctx.Contains("withVwap="), "swing: the story names the swing candle; the context is saved", au.Iterations.Count > 0 ? au.Iterations[0].Ctx : "none");
        // 16. FVG 50%: the gap 4000.3–4002.5 → BUY when the bid comes back to 4001.4 (its middle), not at its top
        var f5 = new List<Mt5Bar> { B(0, 4000, 4000.3, 3999.8, 4000.2, 100), B(1, 4000.2, 4003, 4000.2, 4002.8, 100), B(2, 4002.8, 4004, 4002.5, 4003.8, 100), B(3, 4003.8, 4003.9, 4002.3, 4002.4, 100), B(4, 4002.4, 4002.5, 4001.2, 4001.3, 100) };
        f5.AddRange(Path(Day1.AddMinutes(5), 4001.3, Line(4001.3, 0.5, 20)));
        var s18 = S(); s18.FvgStack = false; s18.EntryMode = "FVG50"; s18.MaxIterations = 1; r = Run(f5, s18); au = r.Item2;
        Check(au.Trades.Count >= 1 && au.Trades[0].Side == "BUY" && Math.Abs(au.Trades[0].Level - 4001.4) < 1e-9 && au.Trades[0].Time >= Day1.AddMinutes(4), "FVG 50%: the BUY at the gap's middle 4001.4 (candle 5), not at its top in candle 4", string.Join(" | ", au.Trades.Select(x => x.Time.ToString("HH:mm:ss") + " " + x.Level)));
        // 17. FVG + BREAK: the break of the high counts only with a bullish FVG; no FVG = no trade
        var s19 = S(); s19.EntryMode = "FVGBREAK"; s19.MaxIterations = 1; r = Run(Path(Day1, 4000, Line(4000, 0.05, 60)), s19);   // a slow climb: highs break, no gap is left
        Check(r.Item2.Trades.Count == 0, "FVG + BREAK: breaks without an FVG → nothing", r.Item2.Trades.Count + "");
        var fb = new List<Mt5Bar> { B(0, 4000, 4000.3, 3999.8, 4000.2, 100), B(1, 4000.2, 4003, 4000.2, 4002.8, 100), B(2, 4002.8, 4004, 4002.5, 4003.8, 100) };
        fb.AddRange(Path(Day1.AddMinutes(3), 4003.8, Line(4003.8, 0.5, 30)));
        r = Run(fb, s19); au = r.Item2;
        Check(au.Trades.Count >= 1 && au.Trades[0].Side == "BUY" && au.Trades[0].Why.StartsWith("ENTRY BUY — FVG + BREAK"), "FVG + BREAK: the high breaks with a bullish FVG → BUY", string.Join(" | ", au.Trades.Select(x => x.Why)));
        // 18. winner lots per side: a climb, buys add 0.3 each, sells would add 0
        var s20 = S(); s20.FirstDir = "BUY"; s20.Target = 80; s20.AddToWinner = true; s20.WinnerLots = 0.3; s20.WinnerLotsSell = 0; s20.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), s20); au = r.Item2;
        Check(au.Trades.Skip(1).Any() && au.Trades.Skip(1).All(x => x.Side == "BUY" && Math.Abs(x.Lots - 0.3) < 1e-9), "winner lots per side: each add on the BUY side = 0.30", string.Join(",", au.Trades.Select(x => x.Side + x.Lots)));

        // 19. model A pieces: ENGULFING at a 20-candle low → BUY, no hedge, +$20 (VWAP DISCOUNT: the buy is under the day's VWAP)
        Func<DateTime, List<Mt5Bar>> eng = t0 => { var l = Path(t0, 4000, Line(4000, -0.2, 25)); l.Add(new Mt5Bar { Time = t0.AddMinutes(25), O = 3995, H = 3995.1, L = 3994.4, C = 3994.5, V = 100, Spread = 10 }); l.Add(new Mt5Bar { Time = t0.AddMinutes(26), O = 3994.5, H = 3995.6, L = 3994.3, C = 3995.5, V = 100, Spread = 10 }); l.AddRange(Path(t0.AddMinutes(27), 3995.5, Line(3995.5, 0.5, 30))); return l; };
        var sA = AutoSettings.Model("A", S()); sA.Tf = 1; sA.Windows = ""; sA.MaxIterations = 1;
        r = Run(eng(Day1.AddHours(10)), sA); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(au.Trades.Count == 1 && au.Trades[0].Side == "BUY" && au.Trades[0].Why.Contains("ENGULFING") && it != null && it.Reason == "TARGET" && it.Result >= 20 && it.Result < 26, "model A: engulfing at the 20-candle low → BUY 0.10, closed at +$20, no add-on", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Why)) + " / " + (it == null ? "none" : it.Reason + " " + it.Result));
        var sF = sA.Copy(); sF.VwapFilter = "FLOW"; r = Run(eng(Day1.AddHours(10)), sF); au = r.Item2;
        Check(au.Trades.Count == 0 && au.Story.Any(x => x.Item2.Contains("NO ENTRY") && x.Item2.Contains("VWAP")), "VWAP filter FLOW: the same buy under the VWAP is skipped and said why", au.Trades.Count + "");
        // 20. no hedge + emergency stop −$50 (AFTER = NEXT): one BUY, no SELL ever, closed at −$50
        var sN = S(); sN.FirstDir = "BUY"; sN.HedgeMode = "NONE"; sN.MaxDrawdown = 50; sN.AfterDrawdown = "NEXT"; sN.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, -0.5, 40)), sN); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(au.Trades.Count == 1 && au.Trades[0].Side == "BUY" && it != null && it.Reason == "MAX DRAWDOWN" && it.Result <= -50 && it.Result > -60, "no hedge: no add-on, the emergency stop closes at −$50", string.Join(",", au.Trades.Select(x => x.Side)) + " " + (it == null ? "" : it.Reason + " " + it.Result));
        // 21. close time 03:10 New York (server 10:10): the open BUY closes, no new iteration after it
        var sE = S(); sE.FirstDir = "BUY"; sE.HedgeMode = "NONE"; sE.CloseAtNy = "03:10"; sE.Target = 500;
        r = Run(Path(Day1.AddHours(10), 4000, Line(4000, 0.05, 40)), sE); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(au.Iterations.Count == 1 && it.Reason == "END OF DAY" && it.End >= Day1.AddHours(10).AddMinutes(10) && r.Item1.State.Positions.Count == 0, "close time: closed at 03:10 New York, nothing new after it", au.Iterations.Count + " " + (it == null ? "" : it.Reason + " " + it.End.ToString("HH:mm")));
        {   // 21b. close time with the basket in a LOSS: HOLD OPEN by default (hedging never closes at a loss); CLOSE only when chosen
            var sHo = S(); sHo.FirstDir = "BUY"; sHo.HedgeMode = "NONE"; sHo.CloseAtNy = "03:10"; sHo.Target = 500;
            var rHo = Run(Path(Day1.AddHours(10), 4000, Line(4000, -0.05, 40)), sHo); var itHo = rHo.Item2.Iterations.FirstOrDefault();
            var sCl = S(); sCl.FirstDir = "BUY"; sCl.HedgeMode = "NONE"; sCl.CloseAtNy = "03:10"; sCl.Target = 500; sCl.EodLoss = "CLOSE";
            var rCl = Run(Path(Day1.AddHours(10), 4000, Line(4000, -0.05, 40)), sCl); var itCl = rCl.Item2.Iterations.FirstOrDefault();
            Check(sHo.EodLoss == "HOLD" && itHo != null && !itHo.Done && rHo.Item1.State.Positions.Count == 1 && rHo.Item2.Story.Any(x => x.Item2.Contains("HELD OPEN")) && itCl != null && itCl.Reason == "END OF DAY" && rCl.Item1.State.Positions.Count == 0 && AutoSettings.Load("eod=16:55").EodLoss == "HOLD",
                "close time in a loss: HELD OPEN by default (never closed at a loss); CLOSE only when chosen", (itHo == null ? "none" : itHo.Reason + " " + rHo.Item1.State.Positions.Count) + " / " + (itCl == null ? "none" : itCl.Reason));
        }
        // 22. ✋ a BUY by hand: the automation adopts it, takes it to +$20, records it as manual
        var sM = S(); sM.EntryMode = "ENGULF"; sM.HedgeMode = "NONE"; sM.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Enumerable.Repeat(4000.0, 10).Concat(Line(4000, 0.5, 30))), sM, (sb, a) => sb.Command("ORDER", "XAUUSD", "BUY", 0.1, 0.0, 0.0, 0.0)); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(it != null && it.Manual && it.Reason == "TARGET" && it.Result >= 20 && au.Trades.Any(x => x.Why.StartsWith("✋ MANUAL")) && it.Ctx.Contains("manual=yes"), "manual: a BUY by hand is adopted, managed to +$20, flagged ✋", it == null ? "none" : it.Reason + " " + it.Manual);
        // 23. 💰 set balance: 10 000 → 5 000 = a −5 000 BALANCE deal, the account follows
        var sbb = r.Item1; double diff; lock (sbb.State.Gate) diff = sbb.Account.SetBalance(5000, sbb.Account.Now);
        Check(Math.Abs(diff + (10000 + au.Iterations.Sum(x => x.Result) - 5000)) < 0.6 && Math.Abs(sbb.Account.Balance - 5000) < 1e-9 && sbb.State.Deals.Last().Type == "BALANCE" && Math.Abs(sbb.State.Account.Balance - 5000) < 0.01, "set balance: 5 000, booked as a balance deal", diff + " " + sbb.Account.Balance);
        // 24. the setups scanner reads the candles without trading (A / B / BOX states)
        var scan = SetupScan.Scan(sbb, "XAUUSD", -7);
        Check(scan.Count == 4 && scan.All(x => x.Level == "STRONG" || x.Level == "WEAK" || x.Level == "WAIT") && sbb.State.Positions.Count == 0, "setups scanner: four cards, no order", string.Join(" | ", scan.Select(x => x.Name + " " + x.Level)));

        // 25. model C: 10 candles climbing above the day's VWAP, one dips to it and closes back above → BUY, no hedge, +$20
        var vr = Path(Day1.AddHours(10), 4000, Line(4000, 0.5, 10)); vr.Add(new Mt5Bar { Time = Day1.AddHours(10).AddMinutes(10), O = 4005, H = 4005.3, L = 4001.5, C = 4005.2, V = 100, Spread = 10 }); vr.AddRange(Path(Day1.AddHours(10).AddMinutes(11), 4005.2, Line(4005.2, 0.5, 20)));
        var sC = AutoSettings.Model("C", S()); sC.Tf = 1; sC.Windows = ""; sC.MaxIterations = 1;
        r = Run(vr, sC); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(au.Trades.Count == 1 && au.Trades[0].Side == "BUY" && au.Trades[0].Why.Contains("VWAP REJECTION") && it != null && it.Reason == "TARGET", "model C: a dip to the VWAP closing back above → BUY, +$20, no add-on", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Why)) + " / " + (it == null ? "none" : it.Reason));

        // 26. A + SIGNAL HEDGE: BUY 0.1; down $10 a BEARISH engulfing at the 20-candle high → SELL 0.2; the sell closes at its own +$40, the buy at its own +$20
        var sh = Path(Day1, 4000, Enumerable.Repeat(4000.0, 25)); var t1 = Day1.AddMinutes(25);
        sh.Add(new Mt5Bar { Time = t1, O = 4000, H = 4000.05, L = 3998.9, C = 3999.0, V = 100, Spread = 10 });
        sh.Add(new Mt5Bar { Time = t1.AddMinutes(1), O = 3999.0, H = 4000.3, L = 3998.95, C = 3999.6, V = 100, Spread = 10 });
        sh.Add(new Mt5Bar { Time = t1.AddMinutes(2), O = 3999.7, H = 4000.4, L = 3998.7, C = 3998.8, V = 100, Spread = 10 });
        sh.AddRange(Path(t1.AddMinutes(3), 3998.8, Line(3998.8, -0.5, 8))); sh.AddRange(Path(t1.AddMinutes(11), 3994.8, Line(3994.8, 0.5, 30)));
        var sH = AutoSettings.Model("AH", S()); sH.Tf = 1; sH.Windows = ""; sH.CloseAtNy = ""; sH.FirstDir = "BUY"; sH.MaxIterations = 1;
        r = Run(sh, sH); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(au.Trades.Count >= 2 && au.Trades[0].Side == "BUY" && au.Trades[1].Side == "SELL" && Math.Abs(au.Trades[1].Lots - 0.2) < 1e-9 && au.Trades[1].Why.StartsWith("SIGNAL HEDGE"), "signal hedge: an opposite engulfing while losing → SELL 0.20 (2× the net)", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Lots + " " + x.Why)));
        Check(it != null && it.Reason == "LEG TARGETS" && it.Result > 50 && it.Result < 70 && au.Trades.Count(x => x.Side == "CLOSE") == 2 && r.Item1.State.Positions.Count == 0, "each leg at its own target: +$40 (0.2) and +$20 (0.1) → the iteration ≈ +$60", it == null ? "none" : it.Reason + " " + it.Result.ToString("0.00"));

        // 27. NEXT = after a WIN the same direction at once: a climb, #1 BUY wins → #2 BUY right away (no new break needed)
        var sW = S(); sW.FirstDir = "SELL"; sW.HedgeMode = "NONE"; sW.MaxDrawdown = 0; sW.NextStart = "WINNER"; sW.MaxIterations = 2;
        r = Run(Path(Day1, 4000, Line(4000, -0.5, 60)), sW); au = r.Item2;
        Check(au.Iterations.Count(x => x.Done) == 2 && au.Trades.Count >= 2 && au.Trades.All(x => x.Side == "SELL") && au.Trades[1].Why.Contains("AFTER A WIN") && au.Trades[1].Time - au.Iterations[0].End < TimeSpan.FromMinutes(1), "after a win: a winning SELL → SELL again at once", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Time.ToString("HH:mm:ss") + " " + x.Why)));

        // 28. a swing low already broken is used up: the first drop through the 3998 swing low sells; after the rally back above it, a second
        // drop through 3998 must NOT sell again (the newest unbroken swing low is now the 3995 bottom)
        var lows = new double[] { 3999.8, 3999.4, 3998.8, 3998.0, 3998.6, 3999.2, 3999.6, 3999.0, 3998.2, 3997.4, 3996.4, 3995.0, 3995.6, 3996.6, 3997.6, 3998.6, 3999.0, 3998.8, 3998.4, 3997.8, 3997.3, 3997.1, 3997.2, 3997.15 };
        var swb = new List<Mt5Bar>(); double pc = 4000;
        for (int i = 0; i < lows.Length; i++) { double c = lows[i] + 0.3; swb.Add(new Mt5Bar { Time = Day1.AddMinutes(i), O = pc, C = c, H = Math.Max(pc, c) + 0.1, L = Math.Min(lows[i], Math.Min(pc, c)), V = 100, Spread = 10 }); pc = c; }
        var sS = S(); sS.Swing = true; sS.SwingK = 2; sS.HedgeMode = "NONE"; sS.MaxIterations = 2;
        r = Run(swb, sS); au = r.Item2;
        Check(au.Trades.Count(x => x.Side == "SELL") == 1, "swing: a broken swing low is used up — no second SELL on the old level after a retrace", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Price.ToString("0.00") + " " + x.Time.ToString("HH:mm"))) + " LV " + au.BuyLevel + " / " + au.SellLevel + " LOG " + string.Join(" / ", au.Log.Take(6)) + " STORY " + string.Join(" / ", au.Story.Take(4).Select(x => x.Item2)));

        // 29. AGGRESSION BREAK: 4 red candles, a green close (the signal), the next candle breaks its high → BUY
        var ag = new List<Mt5Bar>(); double ap = 4005;
        for (int i = 0; i < 4; i++) { ag.Add(new Mt5Bar { Time = Day1.AddMinutes(i), O = ap, C = ap - 1, H = ap + 0.1, L = ap - 1.1, V = 100, Spread = 10 }); ap -= 1; }
        ag.Add(new Mt5Bar { Time = Day1.AddMinutes(4), O = ap, C = ap + 0.6, H = ap + 0.7, L = ap - 0.1, V = 100, Spread = 10 });
        ag.AddRange(Path(Day1.AddMinutes(5), ap + 0.6, Line(ap + 0.6, 0.5, 20)));
        var sA2 = AutoSettings.Model("D", S()); sA2.Windows = ""; sA2.CloseAtNy = ""; sA2.VwapFilter = "NONE"; sA2.MaxIterations = 1;
        r = Run(ag, sA2); au = r.Item2; it = au.Iterations.FirstOrDefault();
        Check(au.Trades.Count >= 1 && au.Trades[0].Side == "BUY" && au.Trades[0].Why.Contains("AGGRESSION BREAK") && Math.Abs(au.Trades[0].Level - (ap + 0.7)) < 1e-9 && au.Trades[0].Time >= Day1.AddMinutes(5) && it != null && it.Reason == "TARGET", "aggression break: 4 red, a green close, its high broken → BUY, +$20", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Level + " " + x.Time.ToString("HH:mm") + " " + x.Why)));

        // 30. FVG 50% of STACKED gaps + the FVG hedge at the first gap candle's low
        var fz = new List<Mt5Bar> { B(0, 4000, 4000.3, 3999.8, 4000.2, 100), B(1, 4000.2, 4003, 4000.2, 4002.8, 100), B(2, 4002.8, 4004, 4002.5, 4003.8, 100), B(3, 4003.8, 4006.2, 4003.8, 4006, 100), B(4, 4006, 4007.2, 4005.5, 4007, 100) };
        fz.AddRange(Path(Day1.AddMinutes(5), 4007, Line(4007, -0.5, 30)));
        var sFv = AutoSettings.Model("F", S()); sFv.FvgTf = 1; sFv.Windows = ""; sFv.MaxIterations = 1;
        r = Run(fz, sFv); au = r.Item2;
        Check(au.Trades.Count >= 2 && au.Trades[0].Side == "BUY" && Math.Abs(au.Trades[0].Level - 4003.25) < 1e-6 && au.Trades[1].Side == "SELL" && Math.Abs(au.Trades[1].Level - 3999.9) < 1e-6 && au.Trades[1].Why.Contains("first candle"), "FVG: 5 gaps in a row = one zone 4000.1–4006.4 → BUY at its 50% 4003.25; the hedge SELL at the first gap's first candle low 3999.9", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Lots + " lvl " + x.Level + " " + x.Why)));

        // 31. RECOVER sizing: BUY 0.1, $2 against it (basket ≈ −$21) → the SELL hedge = 0.1 + 41 / ($100 × 2) ≈ 0.31 lots, not the fixed 0.2
        var sR = S(); sR.FirstDir = "BUY"; sR.HedgeMode = "PRICE"; sR.HedgePrice = 2; sR.HedgeSize = "RECOVER"; sR.RecoverMove = 2; sR.HedgeMaxLots = 1; sR.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, -0.25, 60)), sR); au = r.Item2;
        Check(au.Trades.Count >= 2 && au.Trades[1].Side == "SELL" && au.Trades[1].Lots > 0.27 && au.Trades[1].Lots < 0.36, "recover sizing: the hedge is sized to get the basket to +$20 in a $2 move (≈ 0.31 lots)", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Lots)));

        // 32. 🏁 RACE: a lane trades its OWN shadow account on the backtest's prices — the main account is never touched; its iterations are saved and ranked
        {
            var warmR = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var sbR = new SimBroker();
            var errR = sbR.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warmR.Concat(Path(Day1, 4000, Line(4000, 0.5, 40))).ToList());
            var lane = new RaceLane { Key = "T", Set = S() }; lane.Set.FirstDir = "BUY"; lane.Set.HedgeMode = "NONE"; lane.Set.MaxIterations = 1;
            lane.Br = new ShadowBroker(sbR.State, null, new SimAccountSettings { Balance = 5000, CommissionPerLot = 0, Kind = "RAW" }); lane.StartBalance = lane.PeakEq = 5000; lane.Eng.Send = (c, a2, l2) => lane.Br.Command(c, a2);
            var ck = lane.Eng.Check(lane.Br, lane.Set); lane.Eng.Start(lane.Br, lane.Set, false); sbR.OnTickHook = () => lane.Step(); sbR.Playing = true; sbR.Speed = 60;
            for (int k = 0; k < 4000 && !sbR.Ended; k++) sbR.Advance(0.25);
            var li = lane.Eng.Iterations.FirstOrDefault(); int mainPos; lock (sbR.State.Gate) mainPos = sbR.State.Positions.Count + sbR.State.Deals.Count(dl => dl.Type != "BALANCE");
            Check(errR == "" && ck == "" && li != null && li.Reason == "TARGET" && li.Result >= 20 && mainPos == 0 && Math.Abs(lane.Equity - 5000 - li.Result) < 0.5, "race: a lane wins +$20 on its own $5,000 shadow account; the main account has no trade", errR + ck + " " + (li == null ? "none" : li.Reason + " " + li.Result) + " main " + mainPos + " eq " + lane.Equity);
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-race-" + Guid.NewGuid().ToString("N"));
            RaceStore.Save(dir, "T", li, lane.Eng.Trades); RaceStore.Save(dir, "U", new AutoIteration { N = 1, Start = li.Start, End = li.End.AddMinutes(5), FirstDir = "SELL", Result = -50, MaxDd = -80, Reason = "MAX DRAWDOWN" }, new AutoTrade[0]);
            var rback = RaceStore.Load(dir, Day1.AddDays(-1)); var st = RaceStore.Stats(rback);
            Check(rback.Count == 2 && st.Count == 2 && st[0].Key == "T" && Math.Abs(st[0].Net - li.Result) < 0.01 && st[1].WorstDay < 0, "race history: saved per day, loaded back, the winner ranked first", rback.Count + " " + string.Join(",", st.Select(x => x.Key + " " + x.Net)));
        }

        // 33. NET ADVANTAGE 0.2: BUY 0.1 → $2 down: SELL 0.3 (net −0.2) → $2 up: BUY 0.4 (net +0.2), never back to 0.1
        var sAd = S(); sAd.FirstDir = "BUY"; sAd.HedgeMode = "PRICE"; sAd.HedgePrice = 2; sAd.HedgeSize = "ADVANTAGE"; sAd.NetAdvantage = 0.2; sAd.Target = 500; sAd.MaxIterations = 1;
        r = Run(Path(Day1, 4000, Line(4000, -0.25, 12).Concat(Line(3997, 0.25, 30))), sAd); au = r.Item2;
        Check(au.Trades.Count >= 3 && Math.Abs(au.Trades[1].Lots - 0.3) < 1e-9 && au.Trades[1].Side == "SELL" && Math.Abs(au.Trades[2].Lots - 0.4) < 1e-9 && au.Trades[2].Side == "BUY", "net advantage 0.2: the hedges are 0.3 then 0.4 — the net is always 0.2 the hedge's way", string.Join(" | ", au.Trades.Select(x => x.Side + " " + x.Lots)));
        {   // old saved settings (FIXED was the default then, no hs2) → LEAD BY; a FIXED chosen in this build stays FIXED; the BOX preset keeps FIXED
            var oldS = AutoSettings.Load("sym=XAUUSD;add=0.3;hsize=FIXED;nadv=0.2"); var fx = S(); fx.HedgeSize = "FIXED"; var newS = AutoSettings.Load(fx.Save()); var box = AutoSettings.Load("hsize=FIXED;preset=BOX");
            Check(oldS.HedgeSize == "ADVANTAGE" && Math.Abs(oldS.NetAdvantage - 0.2) < 1e-9 && newS.HedgeSize == "FIXED" && box.HedgeSize == "FIXED", "lead by: older saved FIXED settings now lead by 0.2; a FIXED picked now stays", oldS.HedgeSize + " " + newS.HedgeSize + " " + box.HedgeSize);
        }
        {   // ROLLING (the default): the price went up and came back down → the NEWEST swing low (4000.0) is the hedge level, not the old 3996
            var rb = new List<Mt5Bar> { B(0, 4000, 4000.5, 3999.5, 4000.2, 100), B(1, 4000.2, 4000.6, 3998, 3998.5, 100), B(2, 3998.5, 3999, 3996, 3996.5, 100), B(3, 3996.5, 3998.5, 3997, 3998, 100), B(4, 3998, 4001, 3997.5, 4000.8, 100),
                B(5, 4000.8, 4003, 4000.5, 4002.5, 100), B(6, 4002.5, 4003.2, 4001.5, 4002, 100), B(7, 4002, 4002.2, 4000.0, 4001, 100), B(8, 4001, 4002.5, 4000.8, 4002.2, 100), B(9, 4002.2, 4003, 4001.6, 4002.6, 100) };
            rb.AddRange(Path(Day1.AddMinutes(10), 4002.6, Line(4002.6, -0.5, 14)));
            var sRoll = S(); sRoll.Rolling = new AutoSettings().Rolling; sRoll.Swing = true; sRoll.SwingK = 2; sRoll.FirstDir = "BUY"; sRoll.HedgeMode = "BREAK"; sRoll.Target = 500; sRoll.MaxIterations = 1;
            var rr = Run(rb, sRoll).Item2; var hs = rr.Trades.FirstOrDefault(t => t.Side == "SELL");
            Check(sRoll.Rolling && hs != null && Math.Abs(hs.Level - 4000.0) < 1e-9 && hs.Price > 3998, "rolling (default): up and back down → the hedge SELL at the NEWEST swing low 4000.0, not the old 3996", string.Join(" | ", rr.Trades.Select(x => x.Side + " " + x.Price + " lvl " + x.Level)));
            var oldR = AutoSettings.Load("sym=XAUUSD;rolling=0;hsize=FIXED"); var keepF = S(); keepF.Rolling = false; var keepB = AutoSettings.Load(keepF.Save());
            Check(oldR.Rolling && !keepB.Rolling, "rolling: older saved settings follow the newest swing; FIXED picked now stays", oldR.Rolling + " " + keepB.Rolling);
        }
        {   // ✏ EDIT WHILE RUNNING: saved changes wait for the NEXT iteration — the open one keeps its lots, the next one uses the new ones
            var sQ = S(); sQ.FirstDir = "BUY"; sQ.HedgeMode = "NONE"; sQ.Target = 20; sQ.MaxIterations = 2;
            var rQ = Run(Path(Day1, 4000, Line(4000, 0.25, 80)), sQ, (sb, au) => { var n2 = au.Set.Copy(); n2.EntryLots = 0.2; au.Queue(n2); });
            var eQ = rQ.Item2.Trades.Where(t => t.Why.StartsWith("ENTRY")).ToList();
            Check(eQ.Count >= 2 && Math.Abs(eQ[0].Lots - 0.1) < 1e-9 && Math.Abs(eQ[1].Lots - 0.2) < 1e-9 && rQ.Item2.Story.Any(x => x.Item2.Contains("NEW SETTINGS")),
                "edit while running: the open iteration keeps 0.1 lot, the next one uses the saved 0.2", string.Join(" | ", eQ.Select(x => x.Iter + " " + x.Lots)));
        }
        {   // 🧱 ANTI-CHOP: down $1.2, back up, chop inside $0.4, then up $8
            Func<List<Mt5Bar>> chop = () => Path(Day1, 4000, Line(4000, -0.1, 12).Concat(Line(3998.8, 0.1, 12)).Concat(Enumerable.Range(0, 20).Select(i => i % 2 == 0 ? 3999.6 : 4000.0)).Concat(Line(4000, 0.2, 40)));
            Func<AutoSettings> baseS = () => { var x = S(); x.FirstDir = "BUY"; x.HedgeMode = "PRICE"; x.HedgePrice = 1; x.HedgeSize = "ADVANTAGE"; x.NetAdvantage = 0.2; x.Target = 500; x.MaxIterations = 1; return x; };
            // LOCK → LEAD: after 2 hedges inside $3 the basket goes net 0, nothing in the chop, the break up adds BUY 0.2 (buys lead by 0.2)
            var sLk = baseS(); sLk.LockOn = true; sLk.LockAfter = 2; sLk.LockRange = 3; sLk.Unlock = "LEAD";
            var rLk = Run(chop(), sLk); var tLk = rLk.Item2.Trades.Where(t => t.Side != "CLOSE").ToList(); int iLock = tLk.FindIndex(t => t.Why.StartsWith("🔒 LOCK"));
            double netAfterLock = iLock < 0 ? 99 : tLk.Take(iLock + 1).Sum(t => (t.Side == "BUY" ? 1 : -1) * t.Lots);
            var un = iLock >= 0 && iLock + 1 < tLk.Count ? tLk[iLock + 1] : null;
            Check(iLock == 3 && tLk.Count(t => t.Why.StartsWith("🔒 LOCK")) == 1 && Math.Abs(netAfterLock) < 1e-9 && un != null && un.Why.StartsWith("🔓 UNLOCK") && un.Side == "BUY" && Math.Abs(un.Lots - 0.2) < 1e-9 && rLk.Item2.Hedges.Any(h => h.Kind == "LOCK") && rLk.Item2.Hedges.Any(h => h.Kind == "UNLOCK"),
                "lock: 2 hedges in a $1 range → LOCK to net 0, no hedge in the chop, the break up = UNLOCK BUY 0.2 (lead); all recorded", string.Join(" | ", tLk.Select(t => t.Side + " " + t.Lots + " " + t.Why.Split('—')[0])));
            // LOCK → LIFT: the break up closes every SELL; only buys stay
            var sLf = baseS(); sLf.LockOn = true; sLf.LockAfter = 2; sLf.LockRange = 3; sLf.Unlock = "LIFT"; sLf.RescueMin = 0;   // the lift itself (the ⏱ breakeven rule would close the basket first)
            var rLf = Run(chop(), sLf); var posLf = rLf.Item1.State.Positions;
            Check(rLf.Item2.Trades.Any(t => t.Why.StartsWith("🔓 UNLOCK — LIFT")) && posLf.Count > 0 && posLf.All(p => p.Dir > 0), "lock → lift: the break up closes every SELL, the buys keep running", string.Join(",", posLf.Select(p => p.Side + p.Lots)));
            // RANGE-ONLY: the 2nd hedge (a BUY) waits for a break ABOVE the basket's highest leg, not just +$1 from the last one
            var sRo = baseS(); sRo.RangeOnly = true; var rRo = Run(chop(), sRo); var tRo = rRo.Item2.Trades.Where(t => t.Side != "CLOSE").ToList();
            var sNo = baseS(); var rNo = Run(chop(), sNo); var tNo = rNo.Item2.Trades.Where(t => t.Side != "CLOSE").ToList();
            Check(tRo.Count >= 3 && tNo.Count >= 3 && tRo[2].Side == "BUY" && tRo[2].Price > tRo[0].Price + 0.05 && tRo[2].Time > tNo[2].Time, "range-only: the hedge BUY waits for a break over the highest leg (later than without it)", tRo.Count + " " + (tRo.Count > 2 ? tRo[2].Price + " vs first " + tRo[0].Price : ""));
            {   // ⚡ QUICK HEDGE: up $8, then down — the hedge SELL near the top (the first break of a candle low), not $6 under the entry
                Func<List<Mt5Bar>> upDown = () => Path(Day1, 4000, Line(4000, 0.5, 16).Concat(Line(4008, -0.5, 30)));
                Func<AutoSettings> qS = () => { var x = S(); x.FirstDir = "BUY"; x.HedgeMode = "PRICE"; x.HedgePrice = 6; x.HedgeSize = "ADVANTAGE"; x.NetAdvantage = 0.1; x.Target = 500; x.MaxIterations = 1; x.HedgeCap = 0; return x; };
                var sQh = qS(); sQh.QuickHedge = true; sQh.QuickAfter = 3; var hQ = Run(upDown(), sQh).Item2.Trades.FirstOrDefault(t => t.Side == "SELL");
                var hN = Run(upDown(), qS()).Item2.Trades.FirstOrDefault(t => t.Side == "SELL");
                Check(hQ != null && hN != null && hQ.Price > 4005 && hN.Price < 3995 && AutoSettings.Load(sQh.Save()).QuickHedge && !AutoSettings.Load("sym=XAUUSD").QuickHedge,
                    "quick hedge: after a $3 run the hedge SELL comes at the first candle-low break near the top (> 4005), without it $6 under the entry; saved, OFF by default", (hQ != null ? hQ.Price.ToString() : "none") + " vs " + (hN != null ? hN.Price.ToString() : "none"));
            }
            {   // 🕵 DECODE HEDGING: an observer on the account (as if the trades were copied) records the first leg, the hedge (its size and distance), the cycle; it never sends
                HedgeDecode.RootOverride = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-dec-" + Guid.NewGuid().ToString("N")); var dec = new HedgeDecode(); dec.Symbol = "XAU"; int sent = 0;
                var sD = S(); sD.FirstDir = "BUY"; sD.HedgeMode = "PRICE"; sD.HedgePrice = 2; sD.HedgeSize = "FIXED"; sD.Target = 20; sD.MaxIterations = 1;
                Run(Path(Day1, 4000, Line(4000, -0.25, 12).Concat(Line(3997, -0.25, 60))), sD, null, au => { var hook = au.Send; au.Send = (c, a, l) => hook(c, a, l); }, (sb2) => { var obs = new CountingBroker(sb2, () => sent++); dec.Step(obs, "TEST@Dura"); var prev = sb2.OnTickHook; sb2.OnTickHook = () => { prev(); dec.Step(obs, "TEST@Dura"); }; });
                var legsD = dec.Events.Where(e => e.Kind == "FIRST LEG" || e.Kind == "HEDGE").ToList(); var hD = legsD.FirstOrDefault(e => e.Kind == "HEDGE");
                bool saved = System.IO.Directory.Exists(HedgeDecode.Folder) && System.IO.Directory.GetFiles(HedgeDecode.Folder, "events-*.csv", System.IO.SearchOption.AllDirectories).Length == 1;
                var dec2 = new HedgeDecode(); dec2.Load();
                Check(legsD.Count >= 2 && legsD[0].Kind == "FIRST LEG" && legsD[0].Side == "BUY" && hD != null && hD.Side == "SELL" && Math.Abs(hD.RatioPrev - 2) < 1e-9 && hD.FromLastOpp > 1.9 && dec.Events.Any(e => e.Kind == "CYCLE FLAT") && saved && dec2.Events.Count == dec.Events.Count && dec.Hints("").Any(h => h.Item1.Contains("HEDGE SIZE")) && sent == 0,
                    "decode hedging: the copied first leg, the hedge SELL (2× the last leg, $2 away), the cycle — saved per day, read back, HINTS; it sent nothing", string.Join(" | ", dec.Events.Select(e => e.Kind + " " + e.Side + " " + e.Lots)) + " sent " + sent);
                HedgeDecode.RootOverride = null;
            }
            {   // STEP LEAD: the lead grows by 0.1 with every hedge — SELL 0.2 (leads 0.1), BUY 0.3 (leads 0.2), SELL 0.5 (leads 0.3)
                var sSt = S(); sSt.FirstDir = "BUY"; sSt.HedgeMode = "PRICE"; sSt.HedgePrice = 2; sSt.HedgeSize = "STEPLEAD"; sSt.NetAdvantage = 0.1; sSt.Target = 5000; sSt.MaxIterations = 1;
                var zzS = Path(Day1, 4000, Line(4000, -0.5, 8).Concat(Line(3996, 0.5, 14)).Concat(Line(4003, -0.5, 18)).Concat(Line(3994, 0, 5)));
                var tSt = Run(zzS, sSt).Item2.Trades.Where(t => t.Side != "CLOSE").ToList(); double net = 0; var leads = new List<double>(); foreach (var t in tSt) { net += (t.Side == "BUY" ? 1 : -1) * t.Lots; leads.Add(Math.Round(Math.Abs(net), 2)); }
                Check(tSt.Count >= 4 && Math.Abs(tSt[1].Lots - 0.2) < 1e-9 && Math.Abs(tSt[2].Lots - 0.3) < 1e-9 && Math.Abs(tSt[3].Lots - 0.5) < 1e-9 && AutoSettings.Load(sSt.Save()).HedgeSize == "STEPLEAD",
                    "step lead: the hedges lead by 0.1, then 0.2, then 0.3 (lots 0.2 / 0.3 / 0.5); saved", string.Join(" | ", tSt.Select(t => t.Side + " " + t.Lots)) + " leads " + string.Join(",", leads));
            }
            {   // 🛡 MAX HEDGE DISTANCE: a SELL basket, then a straight rally (no swing high forms) → the hedge BUY comes ≤ 5 above, not never
                var sCap = S(); sCap.FirstDir = "SELL"; sCap.HedgeMode = "BREAK"; sCap.Swing = true; sCap.SwingK = 2; sCap.Rolling = true; sCap.HedgeSize = "ADVANTAGE"; sCap.NetAdvantage = 0.1; sCap.Target = 5000; sCap.MaxIterations = 1; sCap.HedgeCap = 5;
                var up = Path(Day1, 4000, Line(4000, 0.5, 40));
                var hC = Run(up, sCap).Item2.Trades.FirstOrDefault(t => t.Side == "BUY"); var s0 = sCap.Copy(); s0.HedgeCap = 0; var h0 = Run(up, s0).Item2.Trades.FirstOrDefault(t => t.Side == "BUY");
                Check(hC != null && hC.Price <= 4006.5 && h0 == null && AutoSettings.Load("sym=XAUUSD").HedgeCap == 5, "max hedge distance: a straight rally against a SELL → the hedge BUY within $5 (without it: no hedge at all); default 5", (hC != null ? hC.Price.ToString() : "none") + " / " + (h0 != null ? h0.Price.ToString() : "none"));
            }
            {   // 🪜 HM LADDER: straddle → BUY above 4001.25; up to 4010: a BUY every 2.5 above the highest leg; down to 3985: SELLs only below the LOWEST leg − 2.5; at most 6 legs
                var sL = AutoSettings.Model("LADDER", S()); sL.MaxIterations = 1; sL.Target = 5000; sL.LadderMax = 6; sL.EntryLots = 0.05; sL.LadderStep = 2.5; sL.TrendFilter = "NONE"; sL.BeAfter = 0;
                var pL = Path(Day1, 4000, Line(4000, 0.25, 40).Concat(Line(4010, -0.25, 100)));
                var tL = Run(pL, sL).Item2.Trades.Where(t => t.Side != "CLOSE").ToList();
                var buys = tL.Where(t => t.Side == "BUY").ToList(); var sells = tL.Where(t => t.Side == "SELL").ToList();
                bool steps = buys.Count >= 2 && buys.Skip(1).Select((b, i) => b.Price - buys[i].Price).All(d => d > 2.3 && d < 3.2);
                bool below = sells.Count > 0 && sells[0].Price < buys[0].Price - 2.3;
                Check(tL.Count == 6 && tL[0].Side == "BUY" && tL[0].Price > 4001 && tL[0].Price < 4002 && steps && below && tL.All(t => Math.Abs(t.Lots - 0.05) < 1e-9),
                    "hm ladder: the straddle buys above 4001.25, a BUY every 2.5 above the highest leg, a SELL only below the lowest leg − 2.5, all 0.05, max 6 legs", string.Join(" | ", tL.Select(t => t.Side + " " + t.Price)));
                var real = new HedgeAuto(); string why = real.Check(new RealBroker(), sL);
                Check(why.Contains("DEMO") && AutoSettings.Load(sL.Save()).LadderMax == 6 && AutoSettings.Load(sL.Save()).HedgeMode == "LADDER", "hm ladder: refused on a real account (DEMO / BACKTEST only); saved", why);
            }
            {   // ✅ TREND CONFIRMATION: a long fall (EMA 50 under EMA 200), then a 4-candle bounce that breaks highs → no BUY with EMA; a BUY without
                var pT = Path(Day1, 4000, Line(4000, -0.4, 190).Concat(Line(3924, 0.6, 6)).Concat(Line(3927.6, -0.6, 10)));
                Func<string, List<AutoTrade>> runT = tf => { var x = S(); x.HedgeMode = "NONE"; x.Target = 5; x.MaxDrawdown = 2; x.MaxIterations = 0; x.TrendFilter = tf; return Run(pT, x, null, null, null, 12000).Item2.Trades.Where(t => t.Why.StartsWith("ENTRY")).ToList(); };
                var tOn = runT("EMA"); var tOff = runT("NONE");
                Check(!tOn.Any(t => t.Side == "BUY") && tOn.Any(t => t.Side == "SELL") && tOff.Any(t => t.Side == "BUY") && AutoSettings.Load("sym=XAUUSD;trend=EMA").TrendFilter == "EMA" && AutoSettings.Load("sym=XAUUSD").TrendFilter == "NONE",
                    "trend confirmation: in a downtrend the bounce's break-up BUY is skipped (only SELLs); without it the BUY opens; saved, OFF by default", "on: " + tOn.Count + " • off: " + string.Join(",", tOff.Skip(Math.Max(0, tOff.Count - 6)).Select(t => t.Side + "@" + t.Time.ToString("HH:mm") + "/" + t.Price)));
            }
            {   // 🔐 PROTECT: BUY 0.1, up $4 (80 % of a $5 target... here target $50 = 5.0 move), back down → closes at +10 % ($5) instead of hedging
                var sP = S(); sP.FirstDir = "BUY"; sP.HedgeMode = "PRICE"; sP.HedgePrice = 6; sP.HedgeSize = "FIXED"; sP.Target = 50; sP.MaxIterations = 1; sP.ProtectAt = 70; sP.ProtectKeep = 10;
                var pP = Path(Day1, 4000, Line(4000, 0.4, 10).Concat(Line(4004, -0.4, 30)));
                var rP = Run(pP, sP).Item2; var iP = rP.Iterations.FirstOrDefault();
                Check(iP != null && iP.Done && iP.Reason == "PROTECT" && iP.Result > 0 && iP.Result < 15 && !rP.Trades.Any(t => t.Side == "SELL") && AutoSettings.Load(sP.Save()).ProtectAt == 70 && AutoSettings.Load("sym=XAUUSD").ProtectAt == 0,
                    "protect: the basket reached $40 of $50, came back → closed at ≈ +$5 (no hedge); saved, OFF by default", iP == null ? "none" : iP.Reason + " " + iP.Result);
            }
            {   // ⚡ QUICK HEDGE is LOCKED IN: after trailing up to a candle's low it never moves back down when a lower candle follows
                var sQ2 = S(); sQ2.FirstDir = "BUY"; sQ2.HedgeMode = "PRICE"; sQ2.HedgePrice = 8; sQ2.HedgeSize = "ADVANTAGE"; sQ2.NetAdvantage = 0.1; sQ2.Target = 500; sQ2.MaxIterations = 1; sQ2.QuickHedge = true; sQ2.QuickAfter = 3; sQ2.HedgeCap = 0;
                var lv = new List<double>(); var pQ2 = Path(Day1, 4000, Line(4000, 0.5, 10).Concat(new[] { 4004.8, 4004.6, 4004.9, 4004.7, 4004.8, 4004.6 }));
                HedgeAuto capQ = null; var rQ2 = Run(pQ2, sQ2, null, a => capQ = a, sb => { var prev = sb.OnTickHook; sb.OnTickHook = () => { prev(); if (capQ != null && capQ.Current != null && !double.IsNaN(capQ.HedgeLevel)) lv.Add(capQ.HedgeLevel); }; });
                bool up = lv.Count > 5; for (int k = 1; k < lv.Count; k++) if (lv[k] < lv[k - 1] - 1e-9) up = false;
                Check(up && lv.Max() > 4003, "quick hedge locked: once it trails up it never moves back down (" + lv.Count + " ticks, " + lv.First().ToString("0.00") + " → " + lv.Last().ToString("0.00") + ")", string.Join(",", lv.Distinct().Select(x => x.ToString("0.00"))));
            }
            {   // 🛡 RISK GUARD: chop that would pile up hedges (lead 0.2) → the guard locks before the basket passes 1 lot per $5,000 ($10,000 = 2 lots)
                Func<List<Mt5Bar>> chopG = () => Path(Day1, 4000, Enumerable.Range(0, 120).Select(i => 4000 + ((i / 6) % 2 == 0 ? 1.5 : -1.5) + (i % 6) * 0.05));
                Func<bool, AutoSettings> gS = on => { var x = S(); x.FirstDir = "BUY"; x.HedgeMode = "PRICE"; x.HedgePrice = 1; x.HedgeSize = "ADVANTAGE"; x.NetAdvantage = 0.2; x.Target = 5000; x.MaxIterations = 1; x.HedgeCap = 0; x.RiskGuard = on; x.GuardLots = 1.0; x.GuardChop = 0; x.GuardMargin = 0; return x; };
                double maxOn = 0, maxOff = 0; HedgeAuto gOn = null;
                Run(chopG(), gS(true), null, a => gOn = a, sb => { var prev = sb.OnTickHook; sb.OnTickHook = () => { prev(); maxOn = Math.Max(maxOn, sb.State.Positions.Sum(p => p.Lots)); }; });
                Run(chopG(), gS(false), null, null, sb => { var prev = sb.OnTickHook; sb.OnTickHook = () => { prev(); maxOff = Math.Max(maxOff, sb.State.Positions.Sum(p => p.Lots)); }; });
                Check(maxOn <= 2.0 + 0.2 + 1e-9 && maxOff > 4.0 && gOn.Trades.Any(t => t.Why.StartsWith("🛡 RISK GUARD LOCK")) && AutoSettings.Load(gS(true).Save()).RiskGuard && !AutoSettings.Load("sym=XAUUSD").RiskGuard,
                    "risk guard: in chop the basket stays at the cap (2 lots on $10,000 + at most the lead to lock) — it LOCKS instead of the next hedge; without it " + maxOff.ToString("0.##") + " lots; saved, OFF by default", "on " + maxOn + " / off " + maxOff);
            }
            {   // 🤖 FULL AUTO: a break after 12 candles straight up with no pullback is SKIPPED (chasing); the same break after a real pullback is TAKEN
                var flatF = Enumerable.Repeat(4000.0, 40).ToList();
                var chaseF = flatF.Concat(Line(4000, 0.7, 20)).ToList();   // straight up: after the first quick target the next breaks are chasing
                var dipF = flatF.Concat(Line(4000, 0.7, 7)).Concat(Line(4004.9, -0.6, 5)).Concat(Line(4001.9, 0.7, 8)).ToList();   // up, a real pullback, then the break
                Func<AutoSettings> fS = () => { var x = AutoSettings.Model("FULLAUTO", S(), 10000); x.Symbol = S().Symbol; x.FirstDir = "AUTO"; x.Swing = false; x.Lookback = 1; x.MaxIterations = 0; x.Target = 10; x.RiskGuard = false; x.HedgeMode = "NONE"; x.AutoScoreMin = 45; return x; };   // the mechanics (the real threshold is set by the replays)
                var aC = Run(Path(Day1, 4000, chaseF), fS()).Item2; var aP = Run(Path(Day1, 4000, dipF), fS()).Item2;
                bool skipped = aC.ScoreSkips > 0; bool taken = aP.Trades.Any(t => t.Why.StartsWith("ENTRY BUY") && t.Why.Contains("🤖 score") && t.Time >= Day1.AddMinutes(52));
                var faL = AutoSettings.Load(fS().Save());
                Check(skipped && taken && faL.FullAuto && faL.Preset == "FULLAUTO" && !AutoSettings.Load("sym=XAUUSD").FullAuto, "🤖 full auto: a break with no pullback (chasing the top) is skipped; a break after a real pullback is taken with its score; saved; off by default",
                    "chaseF skips " + aC.ScoreSkips + " entries " + string.Join(",", aC.Trades.Select(t => t.Why.Split('—')[0])) + " | dipF " + string.Join(" ; ", aP.Trades.Select(t => t.Why)) + " | " + aC.LastScoreWhy + " / " + aP.LastScoreWhy + " | LOG " + string.Join(" ¦ ", aP.Log.Take(12)));
            }
            {   // 🎯 MULTI: the iterations start from different setupsMu (not only the swing break); the setup is named in the entry and the context
                var pMu = Enumerable.Range(0, 900).Select(i => Math.Round(4000 + 8 * Math.Sin(i / 23.0) + 3 * Math.Sin(i / 5.0) + ((i * 7919) % 13 - 6) * 0.15, 2)).ToList();
                var sMu = AutoSettings.Model("FULLAUTO", S(), 10000); sMu.Symbol = S().Symbol; sMu.EntryMode = "MULTI"; sMu.FvgTf = 1; sMu.MaxIterations = 0; sMu.Target = 3; sMu.AutoScoreMin = 0; sMu.RiskGuard = false; sMu.HedgeMode = "NONE"; sMu.Swing = false; sMu.Lookback = 5;
                var aMu = Run(Path(Day1, 4000, pMu), sMu, steps: 20000).Item2;
                var setupsMu = aMu.Trades.Where(t => t.Why.StartsWith("ENTRY")).Select(t => t.Why.Contains("🕯") ? "ENG" : t.Why.Contains("〽") ? "VWR" : t.Why.Contains("💥") ? "AGG" : t.Why.Contains("🧲") ? "FVG" : t.Why.Contains("🌊") ? "FLOW" : t.Why.Contains("🔄") ? "FADE" : t.Why.Contains("🧱") ? "RETEST" : t.Why.Contains("⚡") ? "BRK" : "?").Distinct().ToList();
                Check(sMu.EntryMode == "MULTI" && setupsMu.Count >= 2 && !setupsMu.Contains("?"), "🎯 multi: FULL AUTO watches every setup — the iterations start from " + string.Join(" / ", setupsMu), "iters " + aMu.Iterations.Count + " | " + string.Join(" ¦ ", aMu.Log.Where(l => l.Contains("FULL AUTO") || l.Contains("ENTRY") || l.Contains("CLOSED")).Take(14)));
            }
            {   // 💥 AGGRESSION (the futures BH rule): ONE red candle, a green close, the next candle breaks the green's high → BUY
                var bhAg = Enumerable.Repeat(4000.0, 30).Concat(new[] { 3999.0, 3999.8, 4001.0, 4001.5 }).ToList();   // red 4000→3999, green 3999→3999.8 (H 3999.9), then up through 3999.9
                var sAg = S(); sAg.EntryMode = "AGGBRK"; sAg.MaxIterations = 1; sAg.Target = 5000; sAg.HedgeMode = "NONE";
                var aAg = Run(Path(Day1, 4000, bhAg), sAg).Item2; var eAg = aAg.Trades.FirstOrDefault(t => t.Why.StartsWith("ENTRY"));
                Check(eAg != null && eAg.Side == "BUY" && AutoSettings.Load("sym=XAUUSD").AggRun == 1, "💥 aggression: one red, a green close, the break of the green's high = BUY (1 candle is enough, the default)", eAg == null ? string.Join(" ¦ ", aAg.Log.Take(5)) : eAg.Why);
            }
            {   // 🕯 1-2-3 ENGULFING: red, red, then a green closing above the 2nd red's body → BUY at that close; mirrored → SELL
                var e123a = Enumerable.Repeat(4000.0, 30).Concat(new[] { 3999.0, 3998.0, 3999.5, 3999.6 }).ToList();
                var e123b = Enumerable.Repeat(4000.0, 30).Concat(new[] { 4001.0, 4002.0, 4000.5, 4000.4 }).ToList();
                var sE123 = S(); sE123.EntryMode = "ENG123"; sE123.MaxIterations = 1; sE123.Target = 5000; sE123.HedgeMode = "NONE";
                var t123B = Run(Path(Day1, 4000, e123a), sE123).Item2.Trades.FirstOrDefault(t => t.Why.StartsWith("ENTRY")); var t123S = Run(Path(Day1, 4000, e123b), sE123).Item2.Trades.FirstOrDefault(t => t.Why.StartsWith("ENTRY"));
                Check(t123B != null && t123B.Side == "BUY" && t123S != null && t123S.Side == "SELL" && t123B.Why.Contains("1-2-3"), "🕯 1-2-3 engulfing: 2 red + a green over the 2nd body = BUY, 2 green + a red under it = SELL", (t123B == null ? "none" : t123B.Why) + " / " + (t123S == null ? "none" : t123S.Why));
            }
            {   // ⚡ BREAK + CLOSE: a wick through the high is NOT an entry; the first candle that CLOSES above it is
                var w0 = Enumerable.Repeat(4000.0, 30).ToList();
                var sBc = S(); sBc.Swing = false; sBc.Lookback = 5; sBc.MaxIterations = 1; sBc.Target = 5000; sBc.HedgeMode = "NONE"; sBc.FirstDir = "AUTO"; sBc.BreakClose = true;
                var pBc = Path(Day1, 4000, w0.Concat(new[] { 4000.0, 4000.0, 4001.0, 4001.2 })); pBc[30].H = 4000.6;   // candle 30: a wick to 4000.6, closes at 4000 → no entry
                var aBc = Run(pBc, sBc).Item2; var eBc = aBc.Trades.FirstOrDefault(t => t.Why.StartsWith("ENTRY"));
                Check(eBc != null && eBc.Side == "BUY" && eBc.Why.Contains("BREAK + CLOSE") && eBc.Time >= Day1.AddMinutes(33) && AutoSettings.Load(sBc.Save()).BreakClose && !AutoSettings.Load("sym=XAUUSD").BreakClose, "⚡ break + close: the wick through the high does not enter; the first CLOSE above it does (option, off by default)", eBc == null ? string.Join(" ¦ ", aBc.Log.Take(4)) : eBc.Time + " " + eBc.Why);
            }
            {   // 🔄 BREAK FADE: a long run up breaks the recent high → SELL (the other way); a small break after a flat stretch → nothing
                var fu = Enumerable.Repeat(4000.0, 30).Concat(Line(4000, 0.5, 12)).ToList();                // a long run: +6 in 12 candles
                var sFd = S(); sFd.EntryMode = "FADE"; sFd.Swing = false; sFd.Lookback = 5; sFd.FadeAtr = 3; sFd.MaxIterations = 1; sFd.Target = 5000; sFd.HedgeMode = "NONE"; sFd.FirstDir = "AUTO";
                var fd = Run(Path(Day1, 4000, fu), sFd).Item2.Trades.FirstOrDefault(t => t.Why.StartsWith("ENTRY"));
                var fl2 = Run(Path(Day1, 4000, Enumerable.Repeat(4000.0, 30).Concat(new[] { 4000.3, 4000.4 })), sFd).Item2.Trades.FirstOrDefault(t => t.Why.StartsWith("ENTRY"));
                Check(fd != null && fd.Side == "SELL" && fd.Why.Contains("FADE") && fl2 == null && AutoSettings.Load(sFd.Save()).FadeAtr == 3, "🔄 break fade: the high broken after a long run → SELL (the other way); a small break → no entry", (fd == null ? "none" : fd.Why) + " / " + (fl2 == null ? "none" : fl2.Why));
            }
            {   // ✋ a close BY HAND after a scale-out is saved as "CLOSED BY HAND" (it was "LEG TARGETS": 06 Nov #22)
                var sHd = S(); sHd.FirstDir = "SELL"; sHd.MaxIterations = 1; sHd.Target = 5000; sHd.HedgeMode = "NONE"; sHd.ScaleOut = true; sHd.ScaleLots = 0.01; sHd.ScaleStep = 0.5; sHd.NetAdvantage = 0.01; sHd.TrackManual = true;
                bool shut = false; var rHd = Run(Path(Day1, 4000, Line(4000, -0.3, 40)), sHd, null, null, sb => { var prev = sb.OnTickHook; sb.OnTickHook = () => { prev(); if (!shut && sb.Clock >= Day1.AddMinutes(25) && sb.State.Positions.Count > 0) { shut = true; sb.Command("CLOSEALL", new object[] { "XAUUSD", "ALL" }); } }; });
                var iHd = rHd.Item2.Iterations.FirstOrDefault(); bool scaled = rHd.Item2.Trades.Any(t => t.Why.StartsWith("📉 SCALE OUT"));
                Check(scaled && iHd != null && iHd.Done && iHd.Reason == "CLOSED BY HAND", "✋ closed by hand after a scale-out → saved as CLOSED BY HAND (not LEG TARGETS)", (iHd == null ? "none" : iHd.Reason) + " scaled " + scaled);
            }
            {   // 🧱 S/R RETEST: the round $4000 level breaks DOWN by a close, the price comes back up to it, a red candle rejects it → SELL
                var rtPath = Enumerable.Range(0, 30).Select(i => 4003.0 + (i % 2 == 0 ? 0.3 : -0.3)).Concat(new[] { 4002.0, 4000.8, 3999.0, 3998.5, 3998.9, 3999.6, 3999.2, 3998.8, 3998.4 }).ToList();
                var sRt = S(); sRt.EntryMode = "RETEST"; sRt.MaxIterations = 1; sRt.Target = 5000; sRt.HedgeMode = "NONE";
                var pRt = Path(Day1, 4003, rtPath); pRt[36].H = 4000.1;   // the 36th candle opens 3999.6, spikes to the level 4000.1 and closes red at 3999.2 (under it) = the rejection
                var aRt = Run(pRt, sRt).Item2; var eRt = aRt.Trades.FirstOrDefault(t => t.Why.StartsWith("ENTRY"));
                Check(eRt != null && eRt.Side == "SELL" && eRt.Why.Contains("RETEST"), "🧱 S/R retest: the $4000 level broken down, retested and rejected → SELL", eRt == null ? string.Join(" ¦ ", aRt.Log.Take(4)) : eRt.Why);
            }
            {   // 🛡 TRIM LIMIT (06 Nov #22): a chop that breaks the lock range up, down, up … → at most 1 trim (each one books a loss); no limit = several
                Func<List<Mt5Bar>> chopT = () => Path(Day1, 4000, Enumerable.Range(0, 400).Select(i => Math.Round(4000 + 6 * Math.Sin(i / 9.0), 2)));
                Func<int, AutoSettings> tS = mx => { var x = S(); x.FirstDir = "BUY"; x.HedgeMode = "PRICE"; x.HedgePrice = 1.5; x.HedgeSize = "ADVANTAGE"; x.NetAdvantage = 0.2; x.Target = 5000; x.MaxIterations = 1; x.HedgeCap = 0; x.RiskGuard = true; x.GuardMode = "LOCK"; x.GuardLots = 0.5; x.GuardChop = 0; x.GuardMargin = 0; x.GuardBreak = 0.2; x.PairOff = false; x.Rescue = false; x.DdTightEvery = 0; x.TrimMax = mx; return x; };
                var tTr1 = Run(chopT(), tS(1)).Item2; var tTr0 = Run(chopT(), tS(0)).Item2;
                int n1 = tTr1.Trades.Count(t => t.Why.StartsWith("🛡 TRIM")), n0 = tTr0.Trades.Count(t => t.Why.StartsWith("🛡 TRIM"));
                Check(n1 <= 1 && n0 >= 2 && AutoSettings.Load("sym=XAUUSD").TrimMax == 0 && AutoSettings.Load(tS(3).Save()).TrimMax == 3, "trim limit: in a chop at most 1 RISK GUARD trim an iteration (no limit: " + n0 + " trims, each a booked loss); then it stays locked", "max1 " + n1 + " / none " + n0);
            }
            {   // 📉 SCALE OUT: BUY 0.3 (lead 0.1) climbing → slices of 0.05 banked every $1.5, never below the lead 0.1
                var sSo = S(); sSo.FirstDir = "BUY"; sSo.EntryLots = 0.3; sSo.NetAdvantage = 0.1; sSo.HedgeMode = "NONE"; sSo.Target = 5000; sSo.MaxIterations = 1; sSo.ScaleOut = true; sSo.ScaleLots = 0.05; sSo.ScaleStep = 1.5;
                double minLots = 99; HedgeAuto capS = null;
                Run(Path(Day1, 4000, Line(4000, 0.25, 60)), sSo, null, a => capS = a, sb => { var prev = sb.OnTickHook; sb.OnTickHook = () => { prev(); var gl = sb.State.Positions.Sum(p => p.Lots); if (gl > 0) minLots = Math.Min(minLots, gl); }; });
                int slices = capS.Trades.Count(t => t.Why.StartsWith("📉 SCALE OUT"));
                Check(slices == 4 && Math.Abs(minLots - 0.1) < 1e-9 && AutoSettings.Load(sSo.Save()).ScaleOut && !AutoSettings.Load("sym=XAUUSD").ScaleOut, "scale out: 0.3 → 4 slices of 0.05 on the way up, it stops at the lead 0.1; saved, OFF by default", slices + " slices, min " + minLots);
            }
            {   // ✋ YOUR HEDGE: moved by hand it wins over the automation's level; removed = no hedge; ↺ = the automation's again
                var sMh = S(); sMh.FirstDir = "BUY"; sMh.HedgeMode = "PRICE"; sMh.HedgePrice = 2; sMh.HedgeSize = "FIXED"; sMh.Target = 5000; sMh.MaxIterations = 1;
                HedgeAuto am = null; var lvM = new List<double>();
                Run(Path(Day1, 4000, Line(4000, 0, 10).Concat(Line(4000, -0.25, 30))), sMh, (sb, au) => { au.SetManualHedge(3995); }, a => am = a);
                var hM = am.Trades.FirstOrDefault(t => t.Side == "SELL");
                Check(hM != null && hM.Price < 3995.3 && hM.Price > 3994, "your hedge: moved by hand to 3995 → the hedge SELL comes there (not at the automation's 3998)", hM == null ? "none" : hM.Price.ToString());
                HedgeAuto an = null; Run(Path(Day1, 4000, Line(4000, 0, 10).Concat(Line(4000, -0.25, 30))), sMh, (sb, au) => { au.RemoveHedge(); }, a => an = a);
                Check(!an.Trades.Any(t => t.Side == "SELL"), "your hedge: removed → no hedge for that leg", string.Join(",", an.Trades.Select(t => t.Side)));
            }
            {   // ⚡ + range-only: the quick hedge never trails INSIDE the basket's own range (the user's #12: SELL hedge 1.6 away inside a 6.7 box)
                var sQr = S(); sQr.FirstDir = "BUY"; sQr.HedgeMode = "PRICE"; sQr.HedgePrice = 6; sQr.HedgeSize = "ADVANTAGE"; sQr.NetAdvantage = 0.1; sQr.Target = 5000; sQr.MaxIterations = 1; sQr.QuickHedge = true; sQr.QuickAfter = 3; sQr.HedgeCap = 0; sQr.RangeOnly = true;
                HedgeAuto cq = null; var bad = new List<string>(); int samples = 0;
                var pq = Path(Day1, 4000, Line(4000, -0.5, 14).Concat(Line(3993, 0.6, 25)).Concat(Line(4008, -0.2, 20)));
                Run(pq, sQr, null, a => cq = a, sb => { var prev = sb.OnTickHook; sb.OnTickHook = () => { prev(); var ps = sb.State.Positions; if (cq == null || cq.Current == null || ps.Count < 2 || double.IsNaN(cq.HedgeLevel)) return; var nw = ps.OrderBy(p => p.Time).ThenBy(p => p.Ticket).Last(); if ((sb.State.ServerNow - nw.Time).TotalSeconds < 10) return; samples++;
                    bool okR = nw.Dir > 0 ? cq.HedgeLevel <= ps.Min(p => p.Open) + 1e-9 : cq.HedgeLevel >= ps.Max(p => p.Open) - 1e-9; if (!okR && bad.Count < 3) bad.Add(nw.Side + " newest, level " + cq.HedgeLevel); }; });
                Check(samples > 0 && bad.Count == 0, "quick hedge + range-only: the hedge never sits inside the basket's range (" + samples + " ticks)", string.Join(" | ", bad));
            }
            var rt = AutoSettings.Load(sLf.Save()); Check(rt.LockOn && rt.Unlock == "LIFT" && rt.LockAfter == 2 && !AutoSettings.Load("sym=XAUUSD").LockOn && !AutoSettings.Load("sym=XAUUSD").RangeOnly, "anti-chop: saved and read back; OFF by default", rt.LockOn + " " + rt.Unlock);
        }
        {   // 📈 AFTER THE TARGET: BUY 0.1 closes at +$20; gold keeps rising $3 more (≈ +$30 more), then falls $6 → back to $20, then breakeven
            var sAf = S(); sAf.FirstDir = "BUY"; sAf.HedgeMode = "NONE"; sAf.Target = 20; sAf.MaxIterations = 1;
            var rAf = Run(Path(Day1, 4000, Line(4000, 0.1, 60).Concat(Line(4006, -0.1, 60))), sAf); var iAf = rAf.Item2.Iterations.FirstOrDefault();
            Check(iAf != null && iAf.Reason == "TARGET" && iAf.AfterMax > iAf.Result + 25 && iAf.AfterMaxMin > 0 && iAf.AfterBackTargetMin > iAf.AfterMaxMin && iAf.AfterBreakevenMin > iAf.AfterBackTargetMin && iAf.AfterDone,
                "after the target: the basket is followed — best ≈ +$50 later, then back to $20, then breakeven (all with their minutes)", iAf == null ? "none" : iAf.AfterMax + " @" + iAf.AfterMaxMin + " back " + iAf.AfterBackTargetMin + " be " + iAf.AfterBreakevenMin);
        }
        {   // 🛟 RESCUE EXIT: after 2 hedges the basket closes as soon as it is back at breakeven (not at the far +$500 target)
            var sRs = S(); sRs.FirstDir = "BUY"; sRs.HedgeMode = "PRICE"; sRs.HedgePrice = 1; sRs.HedgeSize = "ADVANTAGE"; sRs.NetAdvantage = 0.2; sRs.Target = 500; sRs.MaxIterations = 1; sRs.BeAfter = 2;
            var rRs = Run(Path(Day1, 4000, Line(4000, -0.1, 12).Concat(Line(3998.8, 0.1, 12)).Concat(Line(4000, 0.2, 40))), sRs); var iRs = rRs.Item2.Iterations.FirstOrDefault();
            Check(iRs != null && iRs.Reason == "RESCUE" && iRs.AddOns >= 2 && iRs.Result > -1 && iRs.Result < 20 && AutoSettings.Load(sRs.Save()).BeAfter == 2 && AutoSettings.Load("sym=XAUUSD").BeAfter == 0,
                "rescue exit: 2 hedges, back at breakeven → closed (not waiting for +$500); saved; OFF by default", iRs == null ? "none" : iRs.Reason + " " + iRs.Result + " " + iRs.AddOns);
            // the hedged pair's margin: MT5's value, half, or both legs in full
            var acct = new SimAccount(new Mt5State()); var spx = Gold(); spx.HedgedRatio = 0;
            double m0 = acct.MarginOfSymbol(spx, 1, 1, 4000); acct.Set.HedgedPct = 100; double m100 = acct.MarginOfSymbol(spx, 1, 1, 4000); acct.Set.HedgedPct = 50; double m50 = acct.MarginOfSymbol(spx, 1, 1, 4000);
            Check(Math.Abs(m0) < 1e-9 && Math.Abs(m100 - 2 * 4000 * 100 / 500.0) < 1e-6 && Math.Abs(m50 - 4000 * 100 / 500.0) < 1e-6, "hedged margin: MT5's 0 % → 0; 100 % → both legs in full ($1,600); 50 % → $800", m0 + " " + m100 + " " + m50);
        }
        {   // 💾 CARRY OVER: an open basket saved, the automation restarted → the SAME iteration (its number, its start) continues
            var sCo = S(); sCo.FirstDir = "BUY"; sCo.HedgeMode = "PRICE"; sCo.HedgePrice = 1; sCo.Target = 500;
            var warmC = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var sbC = new SimBroker();
            sbC.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warmC.Concat(Path(Day1, 4000, Line(4000, -0.1, 30).Concat(Line(3997, 0.05, 60)))).ToList());
            var aCo1 = new HedgeAuto(); aCo1.Send = (c, x, l) => sbC.Command(c, x); string saved = ""; aCo1.OnOpenState = t => { if (t != "") saved = t; }; sbC.OnTickHook = () => aCo1.Step(sbC);
            aCo1.Start(sbC, sCo, false); sbC.Playing = true; sbC.Speed = 60; for (int k2 = 0; k2 < 120 && !sbC.Ended; k2++) sbC.Advance(0.25);
            var it1 = aCo1.Current; aCo1.Stop("the window closed"); sbC.OnTickHook = null;
            var aCo2 = new HedgeAuto(); aCo2.Send = (c, x, l) => sbC.Command(c, x); aCo2.RestoreOpen = () => HedgeAuto.ParseOpen(saved); aCo2.Start(sbC, sCo, true);
            Check(it1 != null && saved != "" && aCo2.Current != null && aCo2.Current.N == it1.N && aCo2.Current.Start == it1.Start && !aCo2.Current.Adopted, "carry over: after a restart the open basket is the SAME iteration (number, start kept)", (it1 == null ? "none" : it1.N + " " + it1.Start) + " vs " + (aCo2.Current == null ? "none" : aCo2.Current.N + " " + aCo2.Current.Start));
        }
        {   // ✋ the user's catch (iteration #18 hand-hedged, not shown after a restart): the hand-added legs count survives the open save
            var aMh = new HedgeAuto(); aMh.Current = new AutoIteration { N = 18, Start = Day1, FirstDir = "SELL", ManualAdds = 3 };
            var bk = HedgeAuto.ParseOpen(aMh.OpenText(new long[] { 1, 2 }));
            Check(bk != null && bk.It.ManualAdds == 3, "✋ HAND-HEDGED ×3 is saved with the open iteration and read back", bk == null ? "null" : bk.It.ManualAdds + "");
        }
        {   // ⏰ 9:30 STRADDLE (docs/reports/nas100-930-hedge-study.html)
            Func<HedgeAuto, string> sum = a => string.Join(" | ", a.Iterations.Select(x => "#" + x.N + " " + x.Reason + " " + x.Result.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " b" + x.Buys + "/s" + x.Sells + " " + x.Ctx));
            // BREAKOUT up to the +3X target: X = 0.5 × yesterday's 200-point range = 100, 1 lot (risk $100 ÷ 100 points × $1)
            var up = Line(20000, 5, 90).ToList(); var rA = RunStrad(up, null); var iA = rA.Item2.Iterations;
            Check(iA.Count == 1 && iA[0].Reason == "TARGET +3X" && iA[0].Buys == 1 && iA[0].Sells == 1 && iA[0].Ctx.Contains("x=100") && iA[0].Result > 150 && iA[0].Result < 220, "⏰ straddle breakout: BUY + SELL 1 lot at 9:30, X from yesterday's range (100), the SELL cut at −X, the BUY to +3X (≈ +$200)", sum(rA.Item2));
            // down −X then back to the entry: the SELL's stop at its entry → about −X (the cut BUY) in all
            var dn = Line(20000, -5, 30).Concat(Line(19850, 5, 40)).ToList(); var rB = RunStrad(dn, null); var iB = rB.Item2.Iterations;
            Check(iB.Count == 1 && iB[0].Reason == "BREAKEVEN STOP" && iB[0].Result < -90 && iB[0].Result > -125, "⏰ straddle breakout down: the BUY cut at −X, the SELL stopped at its entry on the way back (≈ −$100)", sum(rB.Item2));
            // TRAIL: up +250 then back: the stop trails X behind the best → out near +150
            var tr = Line(20000, 5, 50).Concat(Line(20250, -5, 40)).ToList(); var rC = RunStrad(tr, x => x.StradTrail = true); var iC = rC.Item2.Iterations;
            Check(iC.Count == 1 && iC[0].Reason == "TRAIL STOP" && iC[0].Result > 25 && iC[0].Result < 75, "⏰ straddle trail: the winner trails X behind its best (+250 → out ≈ +150, −100 for the cut leg = ≈ +$50)", sum(rC.Item2));
            // HOLD LOSER (the user's first idea): +X banks half the winner, back to the entry closes all (≈ +$50 − spreads)
            var hl = Line(20000, 5, 22).Concat(Line(20110, -5, 30)).ToList(); var rD = RunStrad(hl, x => x.StradMode = "HOLD"); var iD = rD.Item2.Iterations;
            Check(iD.Count == 1 && iD[0].Reason == "REVERSED" && iD[0].Result > 20 && iD[0].Result < 60, "⏰ straddle HOLD LOSER: half the winner banked at +X, the reversal back to the entry closes the rest", sum(rD.Item2));
            Check(rA.Item2.Trades.Count(t => t.Side == "CLOSE") >= 1 && rA.Item2.Iterations.Count == 1, "⏰ one setup a day: after the target nothing else opens the same day", sum(rA.Item2));
            // nothing outside the 9:30 window: the data starts at 10:00 New York → no setup
            var rE = RunStrad(up, null, Day1.AddHours(17)); Check(rE.Item2.Iterations.Count == 0 && rE.Item1.State.Deals.All(d => d.Type == "BALANCE"), "⏰ straddle: nothing is sent outside the 9:30–9:35 window", sum(rE.Item2));
            // POINTS mode: X fixed = 50 → 2 lots for $100 of risk
            var rF = RunStrad(up, x => { x.StradXMode = "POINTS"; x.StradX = 50; }); Check(rF.Item2.Iterations.Count == 1 && rF.Item2.Iterations[0].Ctx.Contains("x=50") && rF.Item2.Iterations[0].BuyLots > 1.99 && rF.Item2.Iterations[0].BuyLots < 2.01, "⏰ straddle POINTS: X = 50 → 2 lots for $100 risk", sum(rF.Item2));
            var sStr = AutoSettings.Load(AutoSettings.Model("STRADDLE930", new AutoSettings { StradTrail = true }).Save()); Check(sStr.Preset == "STRADDLE930" && sStr.StradMode == "BREAKOUT" && sStr.StradXMode == "RANGE" && Math.Abs(sStr.StradRangeK - 0.5) < 1e-9 && Math.Abs(sStr.StradTargetX - 3) < 1e-9, "⏰ the straddle settings are saved and read back");
        }
        // the old tests that expect FIXED add-on lots keep FIXED
        // 14. DAY: the day replayed with every entry setup, ranked, saved and read back
        var day = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)).Concat(Path(Day1, 4000, zz.Take(600))).ToList();
        var ds = S(); ds.HedgeMode = "PRICE"; ds.HedgePrice = 2; ds.Target = 20;
        var dr = DayLab.Run(Gold(), day, Day1, ds, 10000, 10, null);
        Check(dr.Count == DayLab.Modes.Length && dr.Select(x => x.Mode).Distinct().Count() == DayLab.Modes.Length && dr.Zip(dr.Skip(1), (a, b) => a.Net >= b.Net).All(x => x) && dr.Sum(x => x.Iterations) > 0, "day: every setup replayed and ranked by net", string.Join(" | ", dr.Select(x => x.Mode + " " + x.Iterations + " it " + x.Net.ToString("0") + " " + x.Note)));
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-day-" + Guid.NewGuid().ToString("N") + ".csv"); DayLab.Save(tmp, dr); DayLab.Save(tmp, dr); var back = DayLab.Load(tmp); System.IO.File.Delete(tmp);
        Check(back.Count == DayLab.Modes.Length && Math.Abs(back.Sum(x => x.Net) - dr.Sum(x => x.Net)) < 0.05, "day: saved per day (saving again replaces it)", back.Count + "");
        {   // ✋ MANUAL MANAGE: the climb that buys in test 1 now only SUGGESTS — nothing sent, no iteration, the suggestion names the BUY
            var rM = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), S(), prep: a0 => a0.SetManualManage(true)); var aM = rM.Item2; int posM; lock (rM.Item1.State.Gate) posM = rM.Item1.State.Positions.Count;
            Check(posM == 0 && aM.Trades.Count == 0 && aM.Iterations.Count == 0 && aM.Suggestion.StartsWith("BUY 0.1") && aM.Log.Count(x => x.Contains("💡 SUGGEST")) <= 45, "manual manage: AUTO sends nothing, it suggests the BUY (once per new level, not every tick)", posM + " pos / " + aM.Trades.Count + " trades / '" + aM.Suggestion + "' / " + aM.Log.Count(x => x.Contains("💡 SUGGEST")));
            // OFF again mid-run: AUTO trades by itself again
            var rM2 = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), S(), mid: (b0, a0) => a0.SetManualManage(false), prep: a0 => a0.SetManualManage(true));
            Check(rM2.Item2.Iterations.Count(x => x.Done) >= 1, "manual manage OFF: AUTO sends its orders again", rM2.Item2.Iterations.Count + "");
            // a basket in manual managing: the hedge is only suggested (SELL first in a climb, a $2 price add-on)
            var sMh = S(); sMh.FirstDir = "SELL"; sMh.HedgeMode = "PRICE"; sMh.HedgePrice = 2; sMh.MaxIterations = 1;
            var rMh = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), sMh, mid: (b0, a0) => a0.SetManualManage(true)); int sellsMh, buysMh; lock (rMh.Item1.State.Gate) { sellsMh = rMh.Item1.State.Positions.Count(p => p.Dir < 0); buysMh = rMh.Item1.State.Positions.Count(p => p.Dir > 0); }
            Check(sellsMh == 1 && buysMh == 0 && rMh.Item2.Suggestion.Contains("BUY"), "manual manage: the open basket's hedge is suggested, not sent", sellsMh + "/" + buysMh + " '" + rMh.Item2.Suggestion + "'");
        }
        {   // 🛡 DEFENDER: sized from the balance, the guard + the danger exit on, saved and read back
            var dfn = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 10000); var dfb = AutoSettings.Load(dfn.Save());
            Check(Math.Abs(dfn.EntryLots - 0.1) < 1e-9 && Math.Abs(dfn.Target - 20) < 1e-9 && dfn.HedgeSize == "ADVANTAGE" && dfn.RiskGuard && dfn.CloseGap && dfn.CloseGapMin == 45 && dfb.RiskGuard && dfb.CloseGap && dfn.BeLock && dfn.BeClose /* 09q: CLOSE ALL is the default */ && dfn.DangerExit && dfb.DangerExit && dfb.DangerAt == 60 && dfb.DangerKeep == 25 && Math.Abs(AutoSettings.Model("DEFENDER", new AutoSettings(), 5000).EntryLots - 0.05) < 1e-9 && !AutoSettings.Load("sym=XAUUSD").DangerExit,
                "defender: 0.01 lot per $1,000, fixed lead, ⭐ 13p: 🛡 RISK GUARD ON + ⏰ no new trades 45 min before the close, the breakeven lock, danger exit (saved and read back; old settings: off)", dfn.EntryLots + " " + dfn.Target + " " + dfb.DangerAt);
            // 🚨 the danger meter: a SELL in a $20 climb ($200 down on $10k) → the meter rises, names the loss; the peak stays
            var sDg = S(); sDg.FirstDir = "SELL"; sDg.HedgeMode = "NONE"; sDg.MaxIterations = 1;
            var rDg = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), sDg); var aDg = rDg.Item2;
            Check(aDg.Current != null && aDg.Danger >= 8 && aDg.DangerPeak >= aDg.Danger && aDg.DangerWhy.Contains("down"), "danger meter: a losing basket rises on the meter and names why", aDg.Danger + " '" + aDg.DangerWhy + "'");
            // 🚨 DANGER EXIT: down $100, then back → closes at +25 % of the $40 target ($10) instead of waiting for +$40
            var sDx = S(); sDx.FirstDir = "SELL"; sDx.HedgeMode = "NONE"; sDx.MaxIterations = 1; sDx.Target = 40; sDx.DangerExit = true; sDx.DangerAt = 4; sDx.DangerKeep = 25;
            var rDx = Run(Path(Day1, 4000, Line(4000, 0.5, 20).Concat(Line(4010, -0.5, 40))), sDx); var iDx = rDx.Item2.Iterations.FirstOrDefault();
            Check(iDx != null && iDx.Done && iDx.Reason == "DANGER EXIT" && iDx.Result >= 10 && iDx.Result < 30, "danger exit: after the danger a smaller profit is taken (25 % of the target)", iDx == null ? "none" : iDx.Reason + " " + iDx.Result);
        }
        {   // 🧠 POST-MORTEM: a STEP LEAD basket in a chop, replayed on its own candles with the protections — YOURS replays to the same result
            var zig = new List<double>(); double pz = 4000; foreach (var leg in new[] { 3.0, -3.5, 3.5, -3.5, 3.5, -3.5, 25.0 }) { int n = 12; for (int i = 0; i < n; i++) { pz += leg / n; zig.Add(Math.Round(pz, 2)); } }
            var test = Path(Day1, 4000, zig); var warm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var all = warm.Concat(test).ToList();
            var sP = S(); sP.FirstDir = "SELL"; sP.HedgeMode = "PRICE"; sP.HedgePrice = 2; sP.HedgeSize = "STEPLEAD"; sP.NetAdvantage = 0.1; sP.MaxIterations = 1; sP.Target = 20; sP.DeRisk = false;
            var rP = Run(test, sP); var itP = rP.Item2.Iterations.FirstOrDefault(x => x.Done);
            if (itP == null) Check(false, "post-mortem: the chop basket closes", rP.Item2.Iterations.Count + " " + rP.Item2.Basket.ToString("0.00") + " " + string.Join(",", rP.Item2.Trades.Select(t => t.Side + t.Lots + "@" + t.Price)));
            else
            {
                var pm = PostMortem.Run(Gold(), all, itP, sP, 10000, 10, 0); var mine = pm.Alts.FirstOrDefault(a => a.Name.StartsWith("YOURS"));
                Check(pm.Alts.Count >= 6 && mine != null && Math.Abs(mine.Result - itP.Result) < 3 && Math.Abs(mine.Dd - itP.MaxDd) < 15 && pm.Lesson.StartsWith("#" + itP.N), "post-mortem: the protections replayed on the iteration's own candles (YOURS = the real result), a lesson written", pm.Alts.Count + " alts, yours " + (mine == null ? "none" : mine.Result.ToString("0.00") + " / " + mine.Dd.ToString("0.00")) + " vs real " + itP.Result.ToString("0.00") + " / " + itP.MaxDd.ToString("0.00") + " • " + pm.Lesson);
                var tmpP = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-pm-" + Guid.NewGuid().ToString("N")); var fP = PostMortem.FileOf(tmpP, "auto.DEMO.x"); PostMortem.Save(fP, pm); PostMortem.Save(fP, pm); var backP = PostMortem.Load(fP);
                Check(backP.Count == 1 && backP[0].Alts.Count == pm.Alts.Count && backP[0].Lesson == pm.Lesson && PostMortem.Tally(backP) != "", "post-mortem: saved per account (the same iteration once), read back, tallied", backP.Count + " • " + PostMortem.Tally(backP));
                if (Environment.GetEnvironmentVariable("SHOW_STORY") == "1") { Console.WriteLine("   " + pm.Lesson); foreach (var a0 in pm.Alts) Console.WriteLine("     " + a0.Name + " " + a0.Result.ToString("0.00") + " dd " + a0.Dd.ToString("0.00") + " lots " + a0.MaxLots + " closed " + a0.Closed); }
            }
        }
        {   // ⛔ LOSS LIMITS: a SELL in a climb — MAX DRAWDOWN % closes it and trades on; DAILY LOSS closes it and nothing more today
            var sL = S(); sL.FirstDir = "SELL"; sL.HedgeMode = "NONE"; sL.MaxDdPct = 0.5; sL.AfterDrawdown = "NEXT"; var rL = Run(Path(Day1, 4000, Line(4000, 0.5, 60)), sL); var iL = rL.Item2.Iterations.FirstOrDefault();
            Check(iL != null && iL.Done && iL.Reason == "MAX DRAWDOWN" && iL.Result <= -50 && iL.Result > -80 && rL.Item2.Iterations.Count >= 2 && AutoSettings.Load(sL.Save()).MaxDdPct == 0.5, "max drawdown %: −0.5 % of $10k closes the iteration (≈ −$50), the next one trades", iL == null ? "none" : iL.Reason + " " + iL.Result + " / " + rL.Item2.Iterations.Count);
            var sD = S(); sD.FirstDir = "SELL"; sD.HedgeMode = "NONE"; sD.DayLoss = 50; var rD = Run(Path(Day1, 4000, Line(4000, 0.5, 60)), sD); var iD = rD.Item2.Iterations.FirstOrDefault();
            var sD2 = AutoSettings.Load(sD.Save());
            Check(iD != null && iD.Done && iD.Reason == "DAY LOSS" && iD.Result <= -50 && rD.Item2.Iterations.Count == 1 && rD.Item2.Blocked.Contains("DAILY LOSS") && sD2.DayLoss == 50 && AutoSettings.Load("sym=XAUUSD").DayLoss == 0, "daily loss: −$50 today closes everything, no new iteration until tomorrow", iD == null ? "none" : iD.Reason + " " + iD.Result + " / " + rD.Item2.Iterations.Count + " '" + rD.Item2.Blocked + "'");
        }
        {   // 🕯 HEDGE AT THE ENTRY CANDLE: a buy on the break, then a $2 dip — a $5 price hedge waits, the entry-candle hedge sells at the candle's low
            var pathC = Path(Day1, 4000, Line(4000, 0.5, 10).Concat(Line(4005, -0.5, 12)).Concat(Enumerable.Repeat(3999.0, 20)));
            var sC0 = S(); sC0.HedgeMode = "PRICE"; sC0.HedgePrice = 5; sC0.MaxIterations = 1; sC0.Target = 200;
            var sC1 = sC0.Copy(); sC1.CandleHedge = true;
            var rC0 = Run(pathC, sC0); var rC1 = Run(pathC, sC1);
            var sellC = rC1.Item2.Trades.FirstOrDefault(t => t.Side == "SELL"); var buyC = rC1.Item2.Trades.FirstOrDefault(t => t.Side == "BUY");
            Check(!rC0.Item2.Trades.Any(t => t.Side == "SELL") && sellC != null && buyC != null && sellC.Price < buyC.Price && buyC.Price - sellC.Price < 2.5 && AutoSettings.Load(sC1.Save()).CandleHedge && !AutoSettings.Load("sym=XAUUSD").CandleHedge,
                "entry-candle hedge: the hedge sells at the entry candle's low (not 5 below), saved; off by default", rC0.Item2.Trades.Count + " / " + string.Join(",", rC1.Item2.Trades.Select(t => t.Side + t.Lots + "@" + t.Price.ToString("0.00"))));
        }
        {   // 🧠 SMART: 12 earlier baskets in the LATE session got stuck (4 hedges), 12 in ASIA were clean → a LATE break is skipped; OFF = it trades
            Action<HedgeAuto> hist = a0 => { for (int i = 0; i < 24; i++) a0.Iterations.Add(new AutoIteration { N = i + 1, Start = Day0.AddHours(i % 12), End = Day0.AddHours(i % 12).AddMinutes(30), BalanceStart = 10000, Result = 20, AddOns = i < 12 ? 4 : 0, MaxDd = i < 12 ? -300 : -10, Ctx = "setup=BREAK;side=BUY;session=" + (i < 12 ? "LATE" : "ASIA") + ";hour=" + (i < 12 ? "17" : "01") }); };
            var sS0 = S(); sS0.MaxIterations = 0; var sS1 = sS0.Copy(); sS1.Smart = true;
            var rS0 = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), sS0, prep: hist); var rS1 = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), sS1, prep: hist);
            Check(rS0.Item2.Trades.Count > 0 && rS1.Item2.SmartSkips >= 1 && rS1.Item2.Log.Any(x => x.Contains("🧠 SMART") && x.Contains("LATE")) && rS1.Item2.Trades.Count < rS0.Item2.Trades.Count && AutoSettings.Load(sS1.Save()).Smart && !AutoSettings.Model("DEFENDER", new AutoSettings(), 5000).Smart,
                "smart: the conditions that mostly got stuck before are skipped (every 5th still explores); saved; off in DEFENDER (the replay)", rS0.Item2.Trades.Count + " vs " + rS1.Item2.Trades.Count + " trades, " + rS1.Item2.SmartSkips + " skips • " + rS1.Item2.SmartState);
            var aS = new HedgeAuto(); hist(aS); Check(aS.SmartSkip("session=ASIA;hour=01") == "" && aS.SmartSkip("session=LATE;hour=17").Contains("12 of 12"), "smart: a clean condition trades, a stuck one is named with its numbers", aS.SmartSkip("session=LATE;hour=17"));
        }
        {   // 🔄 RECOVERED EXIT: a SELL goes −$60 deep in a climb, the price comes back → it closes near breakeven (within −$4) instead of waiting for +$20
            var pRx = Path(Day1, 4000, Line(4000, 0.5, 12).Concat(Line(4006, -0.25, 40)));
            var sRx = S(); sRx.FirstDir = "SELL"; sRx.HedgeMode = "NONE"; sRx.MaxIterations = 1; sRx.Target = 20; sRx.RecoverExit = true; sRx.RecDeep = 2; sRx.RecKeep = 20;
            var iRx = Run(pRx, sRx).Item2.Iterations.FirstOrDefault(); var sRx0 = sRx.Copy(); sRx0.RecoverExit = false; var iRx0 = Run(pRx, sRx0).Item2.Iterations.FirstOrDefault();
            Check(iRx != null && iRx.Done && iRx.Reason == "RECOVERED" && iRx.MaxDd <= -40 && iRx.Result >= -6 && iRx.Result < 5 && iRx0 != null && iRx0.Reason != "RECOVERED" && AutoSettings.Load(sRx.Save()).RecoverExit && !AutoSettings.Model("DEFENDER", new AutoSettings(), 5000).RecoverExit && !AutoSettings.Load("sym=XAUUSD").RecoverExit,
                "recovered exit: −$60 deep, back near breakeven → closed at a tiny loss (off: it rides on); saved; off in DEFENDER (the replay)", iRx == null ? "none" : iRx.Reason + " " + iRx.Result.ToString("0.00") + " dd " + iRx.MaxDd.ToString("0.00") + " / off: " + (iRx0 == null ? "none" : iRx0.Reason + " " + iRx0.Result.ToString("0.00")));
        }
        {   // 💥 a blown simulated account (the entry needs more margin than there is): the stop reads as a blow-up (the DEMO / race restart it)
            var sB = S(); sB.EntryLots = 30; var rB = Run(Path(Day1, 4000, Line(4000, 0.5, 20)), sB);
            Check(!rB.Item2.Running && HedgeAuto.IsBlowReason(rB.Item2.StopReason) && !HedgeAuto.IsBlowReason("stopped by you"), "blow-up: no money for the order = a blow-up (the auto restart's trigger); a normal stop is not", rB.Item2.StopReason);
        }
        {   // 🏆 the learning's context and the best setups: an entry saves trend / day range / day / last candles / volatility; clean baskets rank first
            var rCx = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), S()); var cx = rCx.Item2.Iterations.FirstOrDefault();
            Check(cx != null && cx.Ctx.Contains("trend60=") && cx.Ctx.Contains("dayPos=") && cx.Ctx.Contains("dow=") && cx.Ctx.Contains("last3=") && cx.Ctx.Contains("tf=M1"), "context: the trend, the day range position, the day, the last 3 candles, the timeframe are saved with every basket", cx == null ? "none" : cx.Ctx);
            var aB = new HedgeAuto(); for (int i = 0; i < 30; i++) aB.Iterations.Add(new AutoIteration { N = i + 1, Start = Day0.AddMinutes(i * 40), End = Day0.AddMinutes(i * 40 + (i < 15 ? 10 : 200)), BalanceStart = 10000, Result = 20, AddOns = i < 15 ? 0 : 4, MaxDd = i < 15 ? -10 : -400, Ctx = "side=BUY;session=" + (i < 15 ? "LONDON" : "ASIA") + ";withTrend=" + (i % 2 == 0 ? "yes" : "no") });
            var bsT = aB.BestSetups(5);
            Check(bsT.Count >= 4 && bsT[0].Item1 == "session = LONDON" && bsT[0].Item3 == 100 && bsT[bsT.Count - 1].Item1 == "session = ASIA" && HedgeAuto.Clean(aB.Iterations[0]) && !HedgeAuto.Clean(aB.Iterations[20]), "best setups: the condition with the clean baskets ranks first, the stuck one last", string.Join(" | ", bsT.Select(t => t.Item1 + " " + t.Item3.ToString("0"))));
        }
        {   // 🔐 BREAKEVEN LOCK: a BUY runs to +$17 of a $20 target (85 %), then falls — an equal SELL locks it near +$2 (10 %), no drawdown
            var pBe = Path(Day1, 4000, Line(4000, 0.5, 4).Concat(Line(4002, -0.25, 40)));
            var sBe = S(); sBe.BeClose = true; sBe.HedgeMode = "PRICE"; sBe.HedgePrice = 8; sBe.MaxIterations = 1; sBe.Target = 20; sBe.BeLock = true; sBe.BeLockAt = 80; sBe.BeLockKeep = 10;
            var rBe = Run(pBe, sBe); var aBe = rBe.Item2; var sellBe = aBe.Trades.FirstOrDefault(t => t.Side == "SELL");
            var sBe0 = sBe.Copy(); sBe0.BeLock = false; var aBe0 = Run(pBe, sBe0).Item2;
            var sBeL = sBe.Copy(); sBeL.BeClose = false; var aBeL = Run(pBe, sBeL).Item2;
            Check(aBeL.Trades.Any(t => t.Why.Contains("🔐 BREAKEVEN LOCK")) && aBeL.Trades.Any(t => t.Why.StartsWith("🔓 UNLOCK SELL")) && AutoSettings.Load("sym=XAUUSD").BeClose /* 09q: the default is CLOSE ALL */, "breakeven LOCK (the default again, the best in 6 years): the turn back locks the basket at its profit; 09q: the fall through the lock range then UNLOCKS it (SELL lead) — the price alone, LOCK / RISK GUARD off", "locked " + aBeL.Locked + " " + string.Join(" / ", aBeL.Trades.Select(t => t.Side + " " + t.Why)));
            var itBe = aBe.Iterations.FirstOrDefault();
            Check(itBe != null && itBe.Done && itBe.Reason == "BE PROFIT" && itBe.Result > 0 && aBe.Story.Any(x => x.Item2.Contains("BREAKEVEN PROFIT ARMED")) && aBe0.Current != null && AutoSettings.Load(sBe.Save()).BeLock && !AutoSettings.Load("sym=XAUUSD").BeLock,
                "breakeven profit: after 80 % of the target, the turn back closes everything AT the small profit (no frozen lock; off: it falls into a drawdown); saved; off by default", (sellBe == null ? "no sell" : sellBe.Lots + "@" + sellBe.Price) + " locked " + aBe.Locked + " dd " + (aBe.Current == null ? "-" : aBe.Current.MaxDd.ToString("0.00")) + " vs off " + (aBe0.Current == null ? "-" : aBe0.Current.MaxDd.ToString("0.00")));
        }
        {   // ⚠ a hedge the margin cannot afford while a basket is open: the automation HOLDS and keeps managing (it used to stop completely)
            var sMg = S(); sMg.FirstDir = "BUY"; sMg.HedgeMode = "PRICE"; sMg.HedgePrice = 2; sMg.AddLots = 50; sMg.MaxIterations = 1;
            var rMg = Run(Path(Day1, 4000, Line(4000, -0.25, 30)), sMg); var aMg = rMg.Item2; int posMg; lock (rMg.Item1.State.Gate) posMg = rMg.Item1.State.Positions.Count;
            Check(aMg.Running && posMg == 1 && aMg.Story.Any(x => x.Item2.Contains("NOT ENOUGH MARGIN")) && aMg.Story.Count(x => x.Item2.Contains("NOT ENOUGH MARGIN")) <= 2, "margin refusal in a basket: it holds and keeps running (told once an hour), the basket stays managed", aMg.Running + " " + aMg.StopReason + " / " + posMg);
        }
        {   // 📡 the signal journal: the break is saved with its context + extension, measured for an hour, handed over when done
            var gotSig = new List<SignalRec>(); var rJ = Run(Path(Day1, 4000, Line(4000, 0.5, 10).Concat(Enumerable.Repeat(4005.0, 80))), S(), prep: a0 => a0.OnSignalDone = r0 => gotSig.Add(r0));
            var j0 = gotSig.FirstOrDefault();
            Check(j0 != null && j0.Taken && j0.Dir > 0 && j0.Mfe60 > 1 && j0.Ctx.Contains("ext=") && j0.Ctx.Contains("pullback=") && j0.Csv().Split(',').Length >= 15, "signal journal: every signal saved with its context and extension, the next hour measured (best / worst), the CSV row complete", j0 == null ? "none (" + rJ.Item2.Journal.Count + " open)" : j0.Csv());
            // 🔑 key-level hedge: a long basket whose $2.65 price hedge would sit just under the round 4000 → moved below it
            var sK = S(); sK.FirstDir = "BUY"; sK.HedgeMode = "PRICE"; sK.HedgePrice = 2.65; sK.MaxIterations = 1; sK.KeyHedge = true; sK.KeyBuffer = 0.5;
            var rK = Run(Path(Day1, 4002.5, Enumerable.Repeat(4002.5, 20)), sK); var aK = rK.Item2;
            Check(aK.Current != null && aK.HedgeWhy.Contains("KEY LEVEL") && aK.HedgeLevel < 3999.9 && AutoSettings.Load(sK.Save()).KeyHedge && !AutoSettings.Load("sym=XAUUSD").KeyHedge, "key-level hedge: a hedge just past a key level (round 4000) moves beyond it; saved; off by default", aK.HedgeLevel.ToString("0.00") + " • " + aK.HedgeWhy);
        }
        {   // 🎞 the post-mortem on REAL ticks: the candles are flat, the real ticks ran +$3 → with the ticks the replay hits the +$20 target, without them it cannot
            var warmT = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var flat = Path(Day1, 4000, Enumerable.Repeat(4000.0, 20)); var allT = warmT.Concat(flat).ToList();
            var tks = new List<SimTick>(); for (int i = 0; i < 240; i++) { double bd = 4000 + Math.Min(3.0, i * 0.02); tks.Add(new SimTick { T = Day1.AddSeconds(30 + i), Bid = Math.Round(bd, 2), Ask = Math.Round(bd + 0.1, 2) }); }
            var itT = new AutoIteration { N = 1, Start = Day1.AddSeconds(31), End = Day1.AddMinutes(5), FirstDir = "BUY", BalanceStart = 10000, Result = 20 };
            var sT = S(); sT.HedgeMode = "NONE"; sT.Target = 20;
            var withT = PostMortem.Replay(Gold(), allT, itT, sT, 10000, 10, 0, "YOURS", 1, tks); var noT = PostMortem.Replay(Gold(), allT, itT, sT, 10000, 10, 0, "YOURS", 1, null);
            Check(withT != null && withT.Closed && withT.Result >= 20 && noT != null && !noT.Closed, "post-mortem on real ticks: it follows the ticks as they came (flat candles, +$3 ticks → the target is hit); without ticks it cannot", (withT == null ? "null" : withT.Closed + " " + withT.Result.ToString("0.00")) + " / " + (noT == null ? "null" : noT.Closed + " " + noT.Result.ToString("0.00")));
        }
        {   // the break seen only in the CLOSED candle (its high went through the swing between two price updates; no tick showed it): it still enters
            var warmB = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)).ToList();
            Func<int, double, double, double, double, Mt5Bar> mk = (i, o, h, l, c) => new Mt5Bar { Time = Day1.AddMinutes(i), O = o, H = h, L = l, C = c, V = 100, Spread = 10 };
            var dayB = new List<Mt5Bar> { mk(0, 4000, 4000.5, 3999.5, 4000), mk(1, 4000, 4002.2, 3999.9, 4002), mk(2, 4002, 4005.1, 4001.9, 4003), mk(3, 4003, 4003.1, 4001.5, 4002), mk(4, 4002, 4002.1, 4001, 4002), mk(5, 4002, 4005.4, 4001.9, 4004.8), mk(6, 4004.8, 4005.0, 4004.6, 4004.9) };
            for (int i = 7; i < 30; i++) dayB.Add(mk(i, 4004.9, 4005.0, 4004.8, 4004.9));
            var tkB = new List<SimTick>(); for (int i = 0; i < 59; i++) tkB.Add(new SimTick { T = Day1.AddMinutes(5).AddSeconds(i), Bid = Math.Round(4002 + 2.8 * i / 58.0, 2), Ask = Math.Round(4002.1 + 2.8 * i / 58.0, 2) });
            for (int m = 6; m < 30; m++) for (int i = 0; i < 59; i += 5) tkB.Add(new SimTick { T = Day1.AddMinutes(m).AddSeconds(i), Bid = 4004.9, Ask = 4005.0 });
            var sbB = new SimBroker(); var errB = sbB.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD" }, new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> { Tuple.Create(Gold(), warmB.Concat(dayB).ToList(), tkB) });
            var sBk = S(); sBk.Swing = true; sBk.SwingK = 2; sBk.MaxIterations = 1; sBk.Target = 200; sBk.HedgeMode = "NONE";
            var aBk = new HedgeAuto(); aBk.Send = (c, x, l) => sbB.Command(c, x); sbB.OnTickHook = () => aBk.Step(sbB); aBk.Start(sbB, sBk, false); sbB.Playing = true; sbB.Speed = 60;
            for (int k = 0; k < 3000 && !sbB.Ended; k++)
            {   // MT5's own candle 00:05 has the wick (4005.4) that the sampled prices never showed — put it into the series like the bridge does
                sbB.Advance(0.25); lock (sbB.State.Gate) { var bx = sbB.State.SeriesOf("XAUUSD", 1).Bars.FirstOrDefault(b => b.Time == Day1.AddMinutes(5)); if (bx != null && sbB.State.ServerNow >= Day1.AddMinutes(6)) bx.H = Math.Max(bx.H, 4005.4); }
            }
            var itBk = aBk.Iterations.FirstOrDefault();
            Check(errB == "" && itBk != null && itBk.FirstDir == "BUY" && aBk.Story.Any(x => x.Item2.Contains("closed with its high")), "a break seen only in the closed candle (no tick showed it) still enters at the level — the entry no longer moves back to an older swing", errB + " / " + (itBk == null ? "none: " + string.Join(" | ", aBk.Log.Take(5)) : itBk.FirstDir + " " + itBk.Start.ToString("HH:mm:ss")));
        }
        {   // 📈 drawdown lead: a SELL 0.1 goes −$20 → the hedge leads by 0.10 (0.05 + one $20 step) instead of 0.05
            var sDl = S(); sDl.FirstDir = "SELL"; sDl.HedgeMode = "PRICE"; sDl.HedgePrice = 2; sDl.HedgeSize = "ADVANTAGE"; sDl.NetAdvantage = 0.05; sDl.MaxIterations = 1; sDl.DdLeadStep = 20; sDl.DdLeadAdd = 0.05; sDl.DdLeadMax = 0.3;
            var bDl = Run(Path(Day1, 4000, Line(4000, 0.25, 12)), sDl).Item2.Trades.FirstOrDefault(t => t.Side == "BUY"); var sDl0 = sDl.Copy(); sDl0.DdLeadStep = 0; var bDl0 = Run(Path(Day1, 4000, Line(4000, 0.25, 12)), sDl0).Item2.Trades.FirstOrDefault(t => t.Side == "BUY");
            Check(bDl != null && bDl0 != null && Math.Abs(bDl0.Lots - 0.15) < 1e-9 && bDl.Lots > bDl0.Lots + 0.04 && AutoSettings.Load(sDl.Save()).DdLeadStep == 20 && AutoSettings.Load("sym=XAUUSD").DdLeadStep == 0, "drawdown lead: the deeper the basket, the bigger the lead (off: the fixed 0.05)", (bDl == null ? "none" : bDl.Lots.ToString()) + " vs " + (bDl0 == null ? "none" : bDl0.Lots.ToString()));
            var sDt = sDl.Copy(); sDt.DdLeadStep = 0; sDt.HedgePrice = 4; sDt.DdTightEvery = 0.05; sDt.DdTightBy = 20; sDt.DdTightFloor = 40; var sDt0 = sDt.Copy(); sDt0.DdTightEvery = 0;
            var bDt = Run(Path(Day1, 4000, Line(4000, 0.25, 30)), sDt).Item2.Trades.FirstOrDefault(t => t.Side == "BUY"); var bDt0 = Run(Path(Day1, 4000, Line(4000, 0.25, 30)), sDt0).Item2.Trades.FirstOrDefault(t => t.Side == "BUY");
            Check(bDt != null && bDt0 != null && bDt.Price < bDt0.Price - 0.4 && AutoSettings.Load(sDt.Save()).DdTightEvery == 0.05 && AutoSettings.Load("sym=XAUUSD").DdTightEvery == 2, "drawdown-closer hedge: the deeper the basket, the closer the hedge (off: the normal distance)", (bDt == null ? "none" : bDt.Price.ToString()) + " vs " + (bDt0 == null ? "none" : bDt0.Price.ToString()));
            {   // 🛟 rescue: a SELL basket that goes 3 % deep and comes back to break-even closes there (off: it waits for the target)
                var sRz = S(); sRz.FirstDir = "SELL"; sRz.HedgeMode = "NONE"; sRz.MaxIterations = 1; sRz.Target = 400; sRz.MaxDrawdown = 0; sRz.EntryLots = 0.5; sRz.Rescue = true; sRz.RescuePct = 1; sRz.DangerAt = 100; var sRz0 = sRz.Copy(); sRz0.Rescue = false;
                var pathR = Path(Day1, 4000, Line(4000, 0.25, 20).Concat(Line(4005, -0.25, 24)).ToArray());
                var rR = Run(pathR, sRz).Item2; var rR0 = Run(Path(Day1, 4000, Line(4000, 0.25, 20).Concat(Line(4005, -0.25, 24)).ToArray()), sRz0).Item2;
                var iR = rR.Iterations.FirstOrDefault(); var iR0 = rR0.Iterations.FirstOrDefault();
                Check(iR != null && iR.Reason == "RESCUE BE" && (iR0 == null || iR0.Reason != "RESCUE BE") && AutoSettings.Load("sym=XAUUSD").Rescue && AutoSettings.Load(sRz0.Save()).Rescue == false, "rescue to breakeven: a deep basket that comes back to $0 closes there (off: it does not)", (iR == null ? "none" : iR.Reason) + " / " + (iR0 == null ? "none" : iR0.Reason));
            }
            {   // ⏱ the time rule: a small basket (never 3 % deep) that was in a loss for over an hour closes at breakeven
                var sT = S(); sT.FirstDir = "SELL"; sT.HedgeMode = "NONE"; sT.MaxIterations = 1; sT.Target = 400; sT.MaxDrawdown = 0; sT.EntryLots = 0.05; sT.RescuePct = 50; sT.DangerAt = 100; sT.RescueMin = 60; var sT0 = sT.Copy(); sT0.RescueMin = 0;
                Func<List<Mt5Bar>> pT = () => Path(Day1, 4000, Line(4000, 0.05, 40).Concat(Enumerable.Repeat(4002.0, 40)).Concat(Line(4002, -0.1, 30)).ToArray());
                var iT = Run(pT(), sT).Item2.Iterations.FirstOrDefault(); var iT0 = Run(pT(), sT0).Item2.Iterations.FirstOrDefault();
                Check(iT != null && iT.Reason == "RESCUE BE" && (iT0 == null || iT0.Reason != "RESCUE BE") && AutoSettings.Load("sym=XAUUSD").RescueMin == 0, "⏱ breakeven after an hour: a basket in a loss for 60+ min closes at $0 (off: it waits for the target)", (iT == null ? "none" : iT.Reason) + " / " + (iT0 == null ? "none" : iT0.Reason));
            }
            {   // 🪓 de-risk: SELL 0.1 first, a BUY hedge 0.3 above, the price falls far → in rescue the winning SELL pays for part of the losing BUY
                var sD = S(); sD.FirstDir = "BUY"; sD.HedgeMode = "PRICE"; sD.HedgePrice = 2; sD.HedgeSize = "FIXED"; sD.AddLots = 0.6; sD.NetAdvantage = 0.05; sD.MaxHedges = 1; sD.MaxIterations = 1; sD.Target = 2000; sD.MaxDrawdown = 0; sD.EntryLots = 0.3; sD.RescuePct = 0.5; sD.DangerAt = 100; sD.RescueMin = 0; sD.DdTightEvery = 0; sD.DeRisk = true; var sD0 = sD.Copy(); sD0.DeRisk = false;
                Func<List<Mt5Bar>> pD = () => Path(Day1, 4000, Line(4000, -0.25, 60).ToArray());
                var rD = Run(pD(), sD); var rD0 = Run(pD(), sD0);
                bool did = rD.Item2.Trades.Any(t => t.Why.StartsWith("🪓 DE-RISK winner")) && rD.Item2.Trades.Any(t => t.Why.StartsWith("🪓 DE-RISK loser")); double gD = rD.Item1.State.Positions.Sum(p => p.Lots), gD0 = rD0.Item1.State.Positions.Sum(p => p.Lots);
                double nD = rD.Item1.State.Positions.Sum(p => (p.Dir) * p.Lots);
                Check(did && !rD0.Item2.Trades.Any(t => t.Why.StartsWith("🪓")) && AutoSettings.Load(sD0.Save()).DeRisk == false, "🪓 de-risk: in rescue part of the winner + part of the loser are closed together (off: nothing)", "did " + did + " gross " + gD + " vs " + gD0 + " net " + nD + " | " + string.Join(" ; ", rD.Item2.Trades.Select(t => t.Side + " " + t.Lots + " " + t.Why)));
            }
            {   // 🧹 COMPRESS HEDGES: the swap math (MT5's points mode, the triple day), the pairs, and the engine on the screenshot's basket
                var zgs = new Mt5Symbol { Name = "XAUUSD", Point = 0.01, TickSize = 0.01, TickValue = 1, Contract = 100, SwapLong = -62.4, SwapShort = 28.1, SwapMode = 1, Swap3Day = 3 };
                double zsl1 = HedgeAuto.SwapPerLotNight(zgs, 1, 4125), zss1 = HedgeAuto.SwapPerLotNight(zgs, -1, 4125);
                double zmon = HedgeAuto.SwapTonight(zgs, 0.32, 0.40, 4125, new DateTime(2026, 11, 9, 20, 0, 0)), zwed = HedgeAuto.SwapTonight(zgs, 0.32, 0.40, 4125, new DateTime(2026, 11, 11, 20, 0, 0)), zsat = HedgeAuto.SwapTonight(zgs, 0.32, 0.40, 4125, new DateTime(2026, 11, 14, 20, 0, 0));
                Check(Math.Abs(zsl1 + 62.4) < 1e-9 && Math.Abs(zss1 - 28.1) < 1e-9 && Math.Abs(zmon + 8.73) < 1e-9 && Math.Abs(zwed + 26.18) < 1e-9 && zsat == 0 && Math.Abs(HedgeAuto.CompressLots(0.32, 0.40) - 0.32) < 1e-9 && Math.Abs(-HedgeAuto.SwapTonight(zgs, 0.32, 0.32, 4125, new DateTime(2026, 11, 9, 20, 0, 0)) - 10.98) < 1e-9,
                    "🧹 swap math: 0.32 B / 0.40 S = −$8.73 a night (−62.4 / +28.1 a lot), Wednesday ×3 = −$26.18, none on Saturday; the pairs = 0.32 (they pay $10.98 a night)", zsl1 + " " + zss1 + " " + zmon + " " + zwed + " " + zsat);
                var zgspec = Gold(); zgspec.SwapLong = -62.4; zgspec.SwapShort = 28.1; zgspec.SwapMode = 1; zgspec.Swap3Day = DayOfWeek.Wednesday;
                Action<SimBroker, HedgeAuto> zmid = null; Func<string, DateTime, Action<HedgeAuto>, Action<AutoSettings>, Tuple<SimBroker, HedgeAuto, double[]>> zcmpRun = (mode, start, prep, tune) =>
                {   // the screenshot's basket opened by hand (adopted): SELL 0.08, BUY 0.16, SELL 0.16, BUY 0.16, SELL 0.16 — then a flat price
                    var warm = Path(start.AddHours(-2), 4125, Enumerable.Repeat(4125.0, 60)); var test = Path(start, 4125, Enumerable.Repeat(4125.0, 40));
                    var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = start.Date, To = start.Date, Balance = 8000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10, StartNyHour = start.Hour + start.Minute / 60.0 - 7 }, zgspec, warm.Concat(test).ToList()); if (err != "") throw new Exception(err);
                    var s0 = S(); s0.HedgeMode = "NONE"; s0.Target = 16; s0.EntryLots = 0.08; s0.TrackManual = true; s0.CompressMode = mode; s0.Rescue = false; s0.FirstDir = "AUTO"; if (tune != null) tune(s0);
                    var ha = new HedgeAuto(); ha.Send = (c, a, l) => sb.Command(c, a); if (prep != null) prep(ha);
                    double[] seen = { double.NaN, double.NaN, double.NaN, double.NaN };   // the total and the TP distance before the first compress / at the end
                    sb.OnTickHook = () => { ha.Step(sb); if (ha.Current != null && !double.IsNaN(ha.TargetPx) && ha.Compressions == 0) { seen[0] = ha.BasketTotal; seen[1] = ha.TargetPx - sb.State.Quotes["XAUUSD"].Bid; } if (ha.Current != null && !double.IsNaN(ha.TargetPx) && sb.State.ServerNow < start.Date.AddDays(1)) { seen[2] = ha.BasketTotal; seen[3] = ha.TargetPx - sb.State.Quotes["XAUUSD"].Bid; } };
                    var chk = ha.Check(sb, s0); if (chk != "") throw new Exception(chk);
                    sb.Playing = true; sb.Speed = 60; sb.Advance(0.25);
                    foreach (var o in new[] { Tuple.Create("SELL", 0.08), Tuple.Create("BUY", 0.16), Tuple.Create("SELL", 0.16), Tuple.Create("BUY", 0.16), Tuple.Create("SELL", 0.16) }) { sb.Command("ORDER", "XAUUSD", o.Item1, o.Item2, 0.0, 0.0, 0.0); sb.Advance(0.05); }
                    ha.Start(sb, s0, false);
                    for (int k = 0; k < 3000 && !sb.Ended; k++) { sb.Advance(0.25); if (k == 40 && zmid != null) zmid(sb, ha); }
                    return Tuple.Create(sb, ha, seen);
                };
                Func<SimBroker, double[]> zbook = sb => { lock (sb.State.Gate) return new[] { sb.State.Positions.Where(p => p.Dir > 0).Sum(p => p.Lots), sb.State.Positions.Where(p => p.Dir < 0).Sum(p => p.Lots), sb.State.Positions.Count }; };
                var zwedNight = new DateTime(2026, 10, 21, 23, 30, 0);   // a Wednesday (triple swap), 15 min before the window
                var zcR = zcmpRun("ROLLOVER", zwedNight, null, null); var zbR = zbook(zcR.Item1); var zsR = zcR.Item3;
                Check(Math.Abs(zbR[0]) < 1e-9 && Math.Abs(zbR[1] - 0.08) < 1e-9 && zcR.Item2.Compressions == 4 && zcR.Item2.Trades.Count(t => t.Why.StartsWith("🧹 COMPRESS")) == 8 && zcR.Item2.Story.Any(x => x.Item2.Contains("🧹 COMPRESS BEFORE THE ROLLOVER") && x.Item2.Contains("×3")),
                    "🧹 rollover compress (Wednesday 23:45): 0.32 B / 0.40 S → SELL 0.08 only (net −0.08 unchanged), 4 pairs, the oldest legs first with partial lots (8 closes), the story shows the math ×3", string.Join(" ", zbR) + " / " + zcR.Item2.Compressions + " / " + zcR.Item2.Trades.Count(t => t.Why.StartsWith("🧹")));
                Check(!double.IsNaN(zsR[0]) && Math.Abs(zsR[2] - zsR[0]) < 0.6 && Math.Abs(zsR[3] - zsR[1]) < 0.1 && zcR.Item2.Current != null && zcR.Item2.Banked < -6 && zcR.Item2.Banked > -7.5,
                    "🧹 the ledger: the realised part stays in the iteration — the total (open + closed) and the TP distance are the same after the compress (until the midnight swap: on the net only)", "total " + zsR[0] + " → " + zsR[2] + ", TP distance " + zsR[1] + " → " + zsR[3] + ", banked " + zcR.Item2.Banked);
                var zcE = zcmpRun("ROLLOVER", new DateTime(2026, 10, 21, 22, 0, 0), null, null); var zbE = zbook(zcE.Item1);
                Check(Math.Abs(zbE[0] - 0.32) < 1e-9 && Math.Abs(zbE[1] - 0.40) < 1e-9 && zcE.Item2.Compressions == 0 && zcE.Item2.CompressWhy.StartsWith("waits for 23:45"), "🧹 rollover compress: nothing before the window (22:00–22:40): it waits for 23:45", string.Join(" ", zbE) + " " + zcE.Item2.CompressWhy);
                var zcA = zcmpRun("ALWAYS", new DateTime(2026, 10, 21, 10, 0, 0), null, null); var zbA = zbook(zcA.Item1);
                Check(Math.Abs(zbA[0] + zbA[1] - 0.24) < 1e-9 && Math.Abs(zbA[1] - zbA[0] - 0.08) < 1e-9 && zcA.Item2.Compressions >= 1, "🧹 always compress: gross 0.72 → 0.24 (≤ 2 × net 0.08 + the entry 0.08), the net SELL 0.08 unchanged", string.Join(" ", zbA));
                var zcM = zcmpRun("ROLLOVER", zwedNight, h => h.SetManualManage(true), null); var zbM = zbook(zcM.Item1);
                Check(Math.Abs(zbM[0] - 0.32) < 1e-9 && Math.Abs(zbM[1] - 0.40) < 1e-9 && zcM.Item2.Suggestion.Contains("🧹 compress"), "🧹 ✋ MANUAL MANAGE: the compress is only a 💡 suggestion, nothing is closed", string.Join(" ", zbM) + " / " + zcM.Item2.Suggestion);
                var zcO = zcmpRun("OFF", zwedNight, null, null); var zbO = zbook(zcO.Item1);
                Check(Math.Abs(zbO[0] - 0.32) < 1e-9 && Math.Abs(zbO[1] - 0.40) < 1e-9 && zcO.Item2.Compressions == 0 && AutoSettings.Load("sym=XAUUSD").CompressMode == "OFF" && AutoSettings.Load(new AutoSettings { CompressMode = "ALWAYS" }.Save()).CompressMode == "ALWAYS" && AutoSettings.Model("DEFENDER", new AutoSettings(), 8000).CompressMode == "OFF",
                    "🧹 OFF (the default, DEFENDER too): the basket keeps every leg; the mode is saved and read back", string.Join(" ", zbO));
                // ⚡ APPLY NOW on an open basket: OFF → ALWAYS at once — the same iteration, the net and the legs' total kept, only the compress pairs closed
                int zn0 = 0; double ztot0 = double.NaN, zg0 = 0, zstep = double.NaN; var zns = new AutoSettings();
                zmid = (sb, ha) => { zn0 = ha.Current != null ? ha.Current.N : -1; ztot0 = ha.BasketTotal; lock (sb.State.Gate) zg0 = sb.State.Positions.Sum(p => p.Lots); zns = ha.Set.Copy(); zns.CompressMode = "ALWAYS"; ha.ApplyNow(zns); };
                var zap = zcmpRun("OFF", new DateTime(2026, 10, 21, 10, 0, 0), null, null); var zbap = zbook(zap.Item1); zmid = null;
                Check(zn0 == 1 && Math.Abs(zg0 - 0.72) < 1e-9 && zap.Item2.Current != null && zap.Item2.Current.N == 1 && zap.Item2.Set.CompressMode == "ALWAYS" && Math.Abs(zbap[0] + zbap[1] - 0.24) < 1e-9 && Math.Abs(zbap[1] - zbap[0] - 0.08) < 1e-9 && Math.Abs(zap.Item2.BasketTotal - ztot0) < 2.5 && zap.Item2.Trades.All(t => t.Side == "CLOSE" || t.Why.StartsWith("✋")) && AutoSettings.Diff(new AutoSettings(), zns).Any(x => x.StartsWith("🧹 COMPRESS HEDGES: OFF → ALWAYS")),
                    "⚡ APPLY NOW in an open iteration: COMPRESS OFF → ALWAYS acts at once on the same basket (#1 still open, 0.72 → 0.24 lot, net SELL 0.08, the total kept); nothing opened; the question lists the change", zn0 + " " + zg0 + " → " + string.Join(" ", zbap) + " total " + ztot0 + " → " + zap.Item2.BasketTotal);
                zmid = (sb, ha) => { ha.SetManualManage(true); var x = ha.Set.Copy(); x.CompressMode = "ALWAYS"; ha.ApplyNow(x); };
                var zapm = zcmpRun("OFF", new DateTime(2026, 10, 21, 10, 0, 0), null, null); var zbapm = zbook(zapm.Item1); zmid = null;
                Check(Math.Abs(zbapm[0] - 0.32) < 1e-9 && Math.Abs(zbapm[1] - 0.40) < 1e-9 && zapm.Item2.Suggestion.Contains("🧹 compress"), "⚡ APPLY NOW + ✋ MANUAL MANAGE: the new compress only suggests", string.Join(" ", zbapm) + " / " + zapm.Item2.Suggestion);
                // 🐞 #35: every SELL closed by hand → the basket leans LONG → the next hedge is a SELL below (net + lead), not a BUY above
                zmid = (sb, ha) => { List<long> sells; lock (sb.State.Gate) sells = sb.State.Positions.Where(p => p.Dir < 0).Select(p => p.Ticket).ToList(); foreach (var t in sells) sb.Command("CLOSE", t, 0.0); };
                var zsd = zcmpRun("OFF", new DateTime(2026, 10, 21, 10, 0, 0), null, x => { x.HedgeMode = "BREAK"; x.HedgeSize = "ADVANTAGE"; x.NetAdvantage = 0.08; x.HedgeCap = 5; x.Swing = true; x.Rolling = true; });
                double zbid; lock (zsd.Item1.State.Gate) zbid = zsd.Item1.State.Quotes["XAUUSD"].Bid; var zbs = zbook(zsd.Item1);
                Check(Math.Abs(zbs[1]) < 1e-9 && Math.Abs(zbs[0] - 0.32) < 1e-9 && zsd.Item2.LastDir == 1 && !double.IsNaN(zsd.Item2.HedgeLevel) && zsd.Item2.HedgeLevel < zbid && Math.Abs(zsd.Item2.NextHedgeLots - 0.40) < 1e-9,
                    "🐞 the sells closed by hand → net LONG 0.32: the next hedge is SELL 0.40 (net + lead) BELOW the price, not a BUY above", "lastDir " + zsd.Item2.LastDir + " hedge " + zsd.Item2.HedgeLevel + " vs bid " + zbid + " lots " + zsd.Item2.NextHedgeLots);
                Check(!HedgeAuto.ShowPreview(false, false, false) && HedgeAuto.ShowPreview(true, false, false) && HedgeAuto.ShowPreview(false, true, false) && HedgeAuto.ShowPreview(false, false, true),
                    "📈 AUTO OFF (not running, not resumable) → no \"it would enter\" chip / break lines on the chart; running, resumable or a 🏁 RACE lane → shown");
                Check(HedgeAuto.BeComesFirst(1, 4115.46, 4119.54) && !HedgeAuto.BeComesFirst(1, 4115.46, double.NaN) && HedgeAuto.BeComesFirst(-1, 4125, 4121) && !HedgeAuto.BeComesFirst(-1, 4120, 4121),
                    "🔐 BE armed (BUY 0.01 @4118.52, BE 4119.54, hedge SELL @4115.46): the BE line comes first → the hedge line is drawn faded \"not reachable first\"");
                {   // 🧮 ITERATION TOTAL IF CLOSED NOW = open + the closed legs (the sells closed by hand above): the same number as the engine's total
                    double zop, zcl, zbt; lock (zsd.Item1.State.Gate) { var pp = zsd.Item1.State.Positions.ToList(); zop = HedgeAuto.OpenPl(pp, "XAUUSD"); zcl = HedgeAuto.ClosedSince(zsd.Item1.State.Deals, pp, "XAUUSD", zsd.Item2.Current.Start.AddMinutes(-1)); } zbt = zsd.Item2.BasketTotal;
                    var zpp = new List<Mt5Position> { new Mt5Position { Ticket = 1, Symbol = "XAUUSD", Side = "BUY", Lots = 0.40, Profit = -250 }, new Mt5Position { Ticket = 2, Symbol = "XAUUSD", Side = "SELL", Lots = 0.48, Profit = 51.84 } };
                    string zpv = HedgeAuto.ClosePreview(zpp, "XAUUSD", "SELL", -270.64, 16, 100), zpa = HedgeAuto.ClosePreview(zpp, "XAUUSD", "ALL", -270.64, 16, 100);
                    Check(Math.Abs(zop + zcl - zbt) < 0.02 && zcl < -1 && zpv.Contains("+$51.84 banked") && zpv.Contains("total stays −$270.64") && zpv.Contains("LONG 0.4") && zpv.Contains("$7.17 move UP") && zpa == "close all now: the iteration ends at −$270.64",
                        "🧮 the iteration total = open + the closed legs (the same as the engine's); ✕ SELLS preview: +$51.84 banked, total stays −$270.64, net LONG 0.40 needs $7.17 UP to +$16; ✕ ALL: ends at −$270.64", zop + " + " + zcl + " vs " + zbt + " | " + zpv);
                }
                {   // ✔ the closed legs of the iteration (the 3 sells closed by hand) + 🎯 the breakeven / target prices with the current net
                    List<Tuple<string, double, double, double, double>> zlg; lock (zsd.Item1.State.Gate) zlg = HedgeAuto.ClosedLegs(zsd.Item1.State.Deals, "XAUUSD", zsd.Item2.Current.Start.AddMinutes(-1));
                    double zbe = HedgeAuto.PriceForTotal(-270.64, 0, 0.40, 100, 4117.70), ztp = HedgeAuto.PriceForTotal(-270.64, 16, 0.40, 100, 4117.70);
                    Check(zlg.Count == 3 && zlg.All(x => x.Item1 == "SELL") && Math.Abs(zlg.Sum(x => x.Item2) - 0.40) < 1e-9 && zlg.All(x => !double.IsNaN(x.Item3)) && Math.Abs(zbe - 4124.466) < 1e-6 && Math.Abs(ztp - 4124.866) < 1e-6 && double.IsNaN(HedgeAuto.PriceForTotal(-10, 0, 0, 100, 4117)),
                        "✔ closed this iteration: 3 sells · 0.40 lots (open → close prices kept); 🎯 net LONG 0.40 at −$270.64: $0 at 4124.47, +$16 at 4124.87; flat → no price", zlg.Count + " legs " + zbe + " " + ztp);
                }
                Check(HedgeAuto.BadgeText("LIVE", false, false, "REAL", "Duramarkets-Server", 512563) == "● REAL MONEY · Duramarkets 512563" && HedgeAuto.BadgeText("DEMO", false, true, "", "ICMarketsSC-MT5-2", 5298760) == "🎮 DEMO · simulated · ICMarketsSC 5298760" && HedgeAuto.BadgeText("BACKTEST", true, false, "", "", 0).StartsWith("🧪 BACKTEST") && HedgeAuto.BadgeText("PROP", false, false, "CONTEST", "FTMO-Demo", 7).StartsWith("🏆 PROP · FTMO 7"),
                    "● the HEDGE tab names its account: REAL MONEY · Duramarkets 512563 / 🎮 DEMO · simulated · ICMarketsSC 5298760 / 🏆 PROP / 🧪 BACKTEST");
                {   // 💰 SIZE FROM THE LIVE ACCOUNT: 0.01 lot per $1,000 at every iteration start, rounded down to the step, never under the minimum (+ a warning)
                    var zsy = new Mt5Symbol { VolMin = 0.01, VolStep = 0.01 };
                    var z1 = HedgeAuto.SizeFor(431, 431, false, zsy, 0.01, 200); var z2 = HedgeAuto.SizeFor(8000, 8000, false, zsy, 0.01, 200); var z3 = HedgeAuto.SizeFor(12345, 12345, false, zsy, 0.01, 200); var z4 = HedgeAuto.SizeFor(8000, 7000, true, zsy, 0.01, 200);
                    Check(Math.Abs(z1.Item1 - 0.01) < 1e-9 && Math.Abs(z1.Item2 - 2) < 1e-9 && z1.Item3.Contains("2.3× the tested risk at $431") && Math.Abs(z2.Item1 - 0.08) < 1e-9 && Math.Abs(z2.Item2 - 16) < 1e-9 && z2.Item3 == "" && Math.Abs(z3.Item1 - 0.12) < 1e-9 && Math.Abs(z3.Item2 - 24) < 1e-9 && Math.Abs(z4.Item1 - 0.07) < 1e-9 && AutoSettings.Load(new AutoSettings { SizePer1k = 0.01 }.Save()).SizePer1k == 0.01,
                        "💰 size from the account: $431 → 0.01 (+ \"2.3× the tested risk\" warning), target $2; $8,000 → 0.08 / $16; $12,345 → 0.12 (rounded down); another basket open → the equity $7,000 → 0.07", z1.Item3);
                }
                {   // 🐞 #39: the 🔐 breakeven lock (LOCK / RISK GUARD off) then a hedge → the basket is no longer even → NOT "LOCKED" any more
                    var zb = S(); zb.FirstDir = "BUY"; zb.MaxIterations = 1; zb.Target = 20; zb.EntryLots = 0.1; zb.HedgeSize = "ADVANTAGE"; zb.NetAdvantage = 0.1; zb.HedgeMode = "PRICE"; zb.HedgePrice = 1.5; zb.HedgeCap = 0; zb.BeLock = true; zb.BeClose = false; zb.BeLockAt = 80; zb.BeLockKeep = 10; zb.Rescue = false;
                    bool zsawLock = false; HedgeAuto zba = null;
                    var zbr = Run(Path(Day1, 4000, Line(4000, 0, 5).Concat(Line(4000, 0.1, 18)).Concat(Line(4001.8, -0.2, 10)).Concat(Line(3999.8, 0.25, 16))), zb, null, a => zba = a, sb => { var prev = sb.OnTickHook; sb.OnTickHook = () => { prev(); if (zba != null && zba.Locked) zsawLock = true; }; });
                    double zbn; lock (zbr.Item1.State.Gate) zbn = zbr.Item1.State.Positions.Sum(p => p.Dir * p.Lots);
                    Check(zsawLock && zbr.Item2.Story.Any(x => x.Item2.Contains("BREAKEVEN LOCK")) && zbr.Item2.Story.Any(x => x.Item2.Contains("the basket LOCKS there")) && (zbr.Item2.Log.Any(x => x.Contains("no longer even")) || zbr.Item2.Trades.Any(t => t.Why.StartsWith("🔓 UNLOCK"))) && !zbr.Item2.Locked, "🐞 breakeven lock, then the price leaves the range: the basket leans again (09q: the 🔓 UNLOCK on the break, else a hedge) → the LOCKED state is cleared (the banner shows the real net and the next hedge)", "sawLock " + zsawLock + " locked " + zbr.Item2.Locked + " net " + zbn + " | " + string.Join(" / ", zbr.Item2.Log.Skip(Math.Max(0, zbr.Item2.Log.Count - 6))));
                }
                {   // 🔐 #39: SELL entry, the BE line armed, the price comes back past it → BeClose = CLOSE ALL at the market (the iteration ends in profit); the ARMED text says what it will do
                    var zc = S(); zc.FirstDir = "SELL"; zc.MaxIterations = 1; zc.Target = 20; zc.EntryLots = 0.1; zc.HedgeSize = "ADVANTAGE"; zc.NetAdvantage = 0.1; zc.HedgeMode = "PRICE"; zc.HedgePrice = 1.5; zc.HedgeCap = 0; zc.BeLock = true; zc.BeClose = true; zc.BeLockAt = 80; zc.BeLockKeep = 10; zc.Rescue = false;
                    var zcr = Run(Path(Day1, 4000, Line(4000, 0, 5).Concat(Line(4000, -0.1, 18)).Concat(Line(3998.2, 0.2, 10)).Concat(Line(4000.2, 0.25, 10))), zc);
                    var zit = zcr.Item2.Iterations.FirstOrDefault();
                    Check(zit != null && zit.Done && zit.Reason == "BE PROFIT" && zit.Result > 0 && zcr.Item2.Story.Any(x => x.Item2.Contains("BREAKEVEN PROFIT ARMED") && x.Item2.Contains("CLOSES there at the market")) && !zcr.Item2.Trades.Any(t => t.Why.StartsWith("🛡 RISK GUARD LOCK")),
                        "🔐 #39 with CLOSE ALL: armed → the price comes back → everything closes at the market in profit (no lock leg); the armed text says CLOSES", zit == null ? "none" : zit.Reason + " " + zit.Result.ToString("0.00"));
                }
                {   // 🐞 audit: the opening commission sits in the balance already (the IN deal) — the iteration total counts it ONCE: +$20 means +$20 after every fee
                    var zqwarm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var zqtest = Path(Day1, 4000, Line(4000, 0, 3).Concat(Line(4000, 0.02, 200)));
                    var zqsbC = new SimBroker(); zqsbC.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 7, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), zqwarm.Concat(zqtest).ToList());
                    var zqsC = S(); zqsC.FirstDir = "BUY"; zqsC.MaxIterations = 1; zqsC.EntryLots = 0.1; zqsC.Target = 20; zqsC.HedgeMode = "NONE"; zqsC.ScaleOut = true; zqsC.ScaleLots = 0.01; zqsC.ScaleStep = 50; zqsC.NetAdvantage = 0.1;
                    var zqaC = new HedgeAuto(); zqaC.Send = (c, a, l) => zqsbC.Command(c, a); zqsbC.OnTickHook = () => zqaC.Step(zqsbC); zqaC.Start(zqsbC, zqsC, false); zqsbC.Playing = true; zqsbC.Speed = 15;
                    for (int k = 0; k < 6000 && !zqsbC.Ended; k++) zqsbC.Advance(0.25);
                    var zqiC = zqaC.Iterations.FirstOrDefault();
                    Check(zqiC != null && zqiC.Done && zqiC.Reason == "TARGET" && zqiC.Result >= 20 && zqiC.Result < 20.3, "🐞 the opening commission is counted once: a +$20 target closes at +$20.00–20.30 after every fee (it was +$20 + the opening commission)", zqiC == null ? "none" : zqiC.Reason + " " + zqiC.Result.ToString("0.00"));
                }
                Check(zcA.Item2.LastDir == -1 && zcR.Item2.LastDir == -1, "🐞 after a compress the next hedge still follows the net (SELL basket → a BUY above)", zcA.Item2.LastDir + " " + zcR.Item2.LastDir);
            }
            // 🧹 a pending order placed during the iteration is cancelled when it closes
            var rPo = Run(Path(Day1, 4000, Line(4000, 0.5, 40)), S(), mid: (b0, a0) => b0.Command("ORDER", "XAUUSD", "BUY LIMIT", 0.1, 3950.0, 0.0, 0.0)); int ordPo; lock (rPo.Item1.State.Gate) ordPo = rPo.Item1.State.Orders.Count;
            Check(rPo.Item2.Iterations.Any(x => x.Done) && ordPo == 0 && rPo.Item2.Story.Any(x => x.Item2.Contains("pending order")), "iteration closed → its pending limit / stop orders are cancelled (off the chart)", ordPo + " orders left");
        }
        if (Environment.GetEnvironmentVariable("SHOW_STORY") == "1") foreach (var x in dr) Console.WriteLine("   " + x.Csv() + " " + x.Note);
        return fails;
    }
}
public static class AutoStoryPeek { public static void Print(HedgeAuto a) { foreach (var s in a.Story) Console.WriteLine("   " + s.Item2); } }
    // the 🕵 decoder's view of the account: the same state; counts any command it would send (it must send none)
sealed class CountingBroker : IBroker
    {
        readonly SimBroker b; readonly Action hit; public CountingBroker(SimBroker b, Action hit) { this.b = b; this.hit = hit; }
        public Mt5State State { get { return b.State; } }
        public bool Send(string line) { if (!(line.StartsWith("BARS|") || line.StartsWith("WATCH|"))) hit(); return true; }
        public string Command(string cmd, params object[] args) { hit(); return null; }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return true; } }
    }
sealed class RealBroker : IBroker
{
    readonly Mt5State st = new Mt5State(); public Mt5State State { get { return st; } }
    public bool Send(string line) { return true; } public string Command(string cmd, params object[] args) { return null; }
    public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
}
