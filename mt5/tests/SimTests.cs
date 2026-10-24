// the BACKTEST broker against hand-made 1-minute candles: the replayed candles must equal MT5's, fills / stops / targets / pending orders /
// hedging / commission / swap / stop-out must follow the rules, the history files and the downloader must round-trip
using System; using System.Collections.Generic; using System.Linq; using KeystoneMT5;
public static class SimTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static List<Mt5Bar> Bars(DateTime start, int n, double first, Func<int, double> delta, int spread = 20)
    {
        var res = new List<Mt5Bar>(); double c = first;
        for (int i = 0; i < n; i++) { double o = c; c = Math.Round(o + delta(i), 2); res.Add(new Mt5Bar { Time = start.AddMinutes(i), O = o, C = c, H = Math.Round(Math.Max(o, c) + 0.3, 2), L = Math.Round(Math.Min(o, c) - 0.3, 2), V = 100, Spread = spread }); }
        return res;
    }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static SimBroker Run(List<Mt5Bar> m1, SimSettings set) { var sb = new SimBroker(); var err = sb.Load(set, Gold(), m1); if (err != "") throw new Exception(err); return sb; }
    static double Bal(SimBroker sb) { return sb.State.Account.Balance; }

    public static int Main2()
    {
        var day0 = new DateTime(2026, 10, 20); var day1 = new DateTime(2026, 10, 21);
        var warm = Bars(day0.AddHours(10), 120, 4000, i => (i % 7) - 3);
        var test = Bars(day1, 30, 4000, i => 0.5).Concat(Bars(day1.AddMinutes(30), 40, 4015, i => -1)).ToList();
        var all = warm.Concat(test).ToList();
        var set = new SimSettings { Symbol = "XAUUSD", From = day1, To = day1, Balance = 10000, CommissionPerLot = 7, Leverage = 500, Kind = "STANDARD" };

        // 1. the replay draws exactly MT5's candles, on every timeframe
        var sb = Run(all, set); var m1 = sb.State.SeriesOf("XAUUSD", 1);
        Check(m1.Bars.Count == 120 && sb.State.SeriesOf("XAUUSD", 60).Bars.Count == 2, "load: the warm-up day is drawn at once (120 M1 = 2 H1)", m1.Bars.Count + "");
        sb.Playing = true; sb.Speed = SimBroker.MaxSpeed; for (int k = 0; k < 50 && !sb.Ended; k++) sb.Advance(0.05);
        bool same = m1.Bars.Count == all.Count && all.Select((b, i) => Math.Abs(m1.Bars[i].O - b.O) < 1e-9 && Math.Abs(m1.Bars[i].H - b.H) < 1e-9 && Math.Abs(m1.Bars[i].L - b.L) < 1e-9 && Math.Abs(m1.Bars[i].C - b.C) < 1e-9 && m1.Bars[i].Time == b.Time).All(x => x);
        Check(sb.Ended && same, "the replayed 1-minute candles equal the history (open, high, low, close, time)", m1.Bars.Count + " vs " + all.Count);
        var m15 = sb.State.SeriesOf("XAUUSD", 15).Bars.Last(); var src = all.Where(b => b.Time >= m15.Time).ToList();
        Check(Math.Abs(m15.H - src.Max(b => b.H)) < 1e-9 && Math.Abs(m15.C - src.Last().C) < 1e-9 && m15.V == src.Sum(b => b.V), "the M15 candle = its 1-minute candles (high, close, volume)");
        Check(sb.Notes.Any(n => n.Contains("end of the loaded history")), "the end of the data is announced");

        // 2. a market BUY fills at the ask (bid + the candle's spread), commission half in / half out, the stop closes it at the bid
        sb = Run(all, set); sb.State.Results.Clear();
        double ask0 = sb.State.Quotes["XAUUSD"].Ask, bid0 = sb.State.Quotes["XAUUSD"].Bid;
        Check(Math.Abs(ask0 - bid0 - 0.20) < 1e-9, "the spread = the candle's 20 points", (ask0 - bid0).ToString());
        sb.Command("ORDER", "XAUUSD", "BUY", 1.0, 0.0, 3990.0, 0.0);
        var p = sb.State.Positions.SingleOrDefault();
        Check(p != null && Math.Abs(p.Open - ask0) < 1e-9 && sb.State.Results.Last().Item2 && Math.Abs(Bal(sb) - 9996.5) < 1e-9, "BUY 1.00 at the ask, −$3.50 commission in", p == null ? "no position" : p.Open + " bal " + Bal(sb));
        sb.Command("ORDER", "XAUUSD", "SELL", 0.5, 0.0, 3990.0, 0.0);
        Check(sb.State.Results.Last().Item2 == false && sb.State.Results.Last().Item3.Contains("invalid stops"), "a SELL with its stop below the price is refused (invalid stops)", sb.State.Results.Last().Item3);
        sb.Command("ORDER", "XAUUSD", "SELL", 0.5, 0.0, 0.0, 0.0);
        Check(sb.State.Positions.Count == 2 && sb.State.Positions.Count(x => x.Dir < 0) == 1, "hedging: a sell next to the buy = 2 tickets");
        sb.Command("CLOSEALL", "", "SELL");
        Check(sb.State.Positions.Count == 1, "CLOSE SELLS leaves the buy");
        sb.Playing = true; sb.Speed = 600; sb.JumpTo(day1.AddMinutes(31)); sb.Command("MODIFY", p.Ticket, 4010.0, 0.0);
        Check(p.Sl == 4010 && sb.State.Results.Last().Item2, "the stop moved up to 4010 after the rise", sb.State.Results.Last().Item3);
        sb.Playing = true; sb.Speed = 600; for (int k = 0; k < 400 && sb.State.Positions.Count > 0 && !sb.Ended; k++) sb.Advance(0.05);
        var outD = sb.State.Deals.LastOrDefault(d => d.Entry == "OUT" && d.Position == p.Ticket);
        Check(outD != null && outD.Reason == "SL" && outD.Price <= 4010 && outD.Price > 4009.4, "the stop at 4010 closes the buy at the bid when the price falls through it", outD == null ? "no exit" : outD.Price + " " + outD.Reason);
        if (outD != null) Check(Math.Abs(outD.Profit - Math.Round((outD.Price - p.Open) * 100, 2)) < 0.011 && Math.Abs(outD.Commission + 3.5) < 1e-9, "the exit's P/L = (exit − entry) × 100 $ per point per lot, −$3.50 commission out", outD.Profit + "");

        // 3. pending orders: a buy limit fills at its price when the ask comes down, a sell stop at the market
        sb = Run(all, set); sb.Playing = true; sb.Speed = 600; sb.JumpTo(day1.AddMinutes(31));
        sb.Command("ORDER", "XAUUSD", "BUY LIMIT", 0.2, 4005.0, 0.0, 4012.0);
        sb.Command("ORDER", "XAUUSD", "BUY LIMIT", 0.2, 4100.0, 0.0, 0.0);
        Check(sb.State.Orders.Count == 1 && sb.State.Results.Last().Item3.Contains("invalid price"), "a buy limit above the market is refused");
        sb.Command("ORDER", "XAUUSD", "SELL STOP", 0.1, 4001.0, 0.0, 0.0);
        sb.Playing = true; sb.Speed = 600; for (int k = 0; k < 600 && !sb.Ended; k++) sb.Advance(0.05);
        var lim = sb.State.Deals.FirstOrDefault(d => d.Entry == "IN" && d.Lots == 0.2); var stp = sb.State.Deals.FirstOrDefault(d => d.Entry == "IN" && d.Lots == 0.1);
        Check(lim != null && Math.Abs(lim.Price - 4005) < 1e-9, "the buy limit filled at 4005", lim == null ? "not filled" : lim.Price + "");
        Check(stp != null && stp.Price <= 4001 && stp.Price > 4000, "the sell stop filled at the market when the bid fell to 4001", stp == null ? "not filled" : stp.Price + "");
        Check(sb.State.Orders.Count == 0, "no order left");

        // 4. modify / breakeven / partial close
        sb = Run(all, set); sb.Command("ORDER", "XAUUSD", "BUY", 1.0, 0.0, 0.0, 0.0); var b1 = sb.State.Positions[0];
        sb.Playing = true; sb.Speed = 600; sb.JumpTo(day1.AddMinutes(20));
        sb.Command("BE", "XAUUSD", "ALL", 10); Check(Math.Abs(b1.Sl - (b1.Open + 0.10)) < 1e-9, "breakeven: the stop to the entry + 10 points", b1.Sl + "");
        sb.Command("CLOSE", b1.Ticket, 0.4); Check(Math.Abs(b1.Lots - 0.6) < 1e-9 && sb.State.Deals.Count(d => d.Entry == "OUT") == 1, "a partial close leaves 0.60 lots", b1.Lots + "");
        sb.Command("MODIFY", b1.Ticket, 0.0, 4011.0); Check(b1.Tp == 4011 && b1.Sl == 0, "MODIFY sets the target and removes the stop");

        // 5. swap at the server's midnight (Wednesday = 3 days), stop-out with too little money
        var night = Bars(new DateTime(2026, 10, 21, 23, 50, 0), 20, 4000, i => 0.1);
        var set2 = new SimSettings { Symbol = "XAUUSD", From = new DateTime(2026, 10, 21), To = new DateTime(2026, 10, 22), Balance = 10000, CommissionPerLot = 0, SwapLong = -5, Leverage = 500, StartNyHour = double.NaN };
        sb = Run(warm.Concat(night).ToList(), set2); sb.Command("ORDER", "XAUUSD", "BUY", 1.0, 0.0, 0.0, 0.0); sb.Playing = true; sb.Speed = SimBroker.MaxSpeed; for (int k = 0; k < 20 && !sb.Ended; k++) sb.Advance(0.05);
        Check(Math.Abs(sb.State.Positions[0].Swap + 15) < 1e-9, "a buy held over Wednesday's midnight pays 3 × −$5 swap", sb.State.Positions[0].Swap + "");
        var crash = Bars(day1, 60, 4000, i => -3);
        var set3 = new SimSettings { Symbol = "XAUUSD", From = day1, To = day1, Balance = 1000, CommissionPerLot = 0, Leverage = 500, StopOutPct = 50 };
        sb = Run(warm.Concat(crash).ToList(), set3); sb.Command("ORDER", "XAUUSD", "BUY", 1.0, 0.0, 0.0, 0.0);
        Check(sb.State.Positions.Count == 1 && sb.State.Account.Margin > 700, "1 lot of gold at 1:500 uses ~$800 margin", sb.State.Account.Margin + "");
        sb.Playing = true; sb.Speed = SimBroker.MaxSpeed; for (int k = 0; k < 20 && !sb.Ended; k++) sb.Advance(0.05);
        Check(sb.State.Positions.Count == 0 && sb.State.Deals.Any(d => d.Reason == "STOPOUT") && sb.Notes.Any(n => n.Contains("STOP OUT")), "the margin level under 50 % → stop out");
        sb.Command("ORDER", "XAUUSD", "SELL", 99.0, 0.0, 0.0, 0.0);
        Check(sb.State.Results.Last().Item2 == false, "the backtest refuses orders once it has ended");

        // 6. the start time in New York hours (server = NY + 7)
        var set4 = new SimSettings { Symbol = "XAUUSD", From = day1, To = day1, StartNyHour = -6.5, Balance = 1000 };   // NY 17:30 the day before = server 00:30
        sb = Run(all, set4); Check(sb.Clock == day1.AddMinutes(30), "a start at NY 17:30 (the day before) = server 00:30", sb.Clock.ToString("o"));
        sb.StepBar(15); Check(sb.Clock == day1.AddMinutes(45), "▶| one M15 candle further opens the 00:45 candle", sb.Clock.ToString("o"));

        // 7. the bridge's history / deals lines, the history files, the downloader
        var st = new Mt5State();
        st.Apply("HIST|XAUUSD|1792800000|1792886400|2|0"); st.Apply("H|1792800000|4000.1|4001|3999|4000.5|120|18"); st.Apply("H|1792800060|4000.5|4002|4000|4001|90|20"); st.Apply("HISTEND|XAUUSD|1792800000|1792886400");
        Check(st.Hist.ContainsKey("XAUUSD|1792800000") && st.Hist["XAUUSD|1792800000"].Item1.Count == 2 && st.Hist["XAUUSD|1792800000"].Item1[1].Spread == 20, "HIST … HISTEND → 2 one-minute candles with their spread");
        st.Apply("DEALS|1790000000|1793000000|2"); st.Apply("D|11|1792800100|XAUUSD|BUY|IN|0.10|4000.10|0.00|-0.35|0.00|501|0||SpasiX"); st.Apply("D|12|1792800500|XAUUSD|SELL|OUT|0.10|4005.10|50.00|-0.35|-1.20|501|0|TP|"); st.Apply("DEALEND");
        Check(st.Deals.Count == 2 && st.Deals[1].IsExit && Math.Abs(st.Deals[1].Net - 48.45) < 1e-9 && st.Deals[1].Reason == "TP", "DEALS … DEALEND → the exit's net P/L (profit + commission + swap) and its reason");
        st.Apply("CAN|1|1|1|5.00|10800|1792800000|1|0"); Check(!st.CanTrade, "the account forbids expert trading → read only");
        st.Apply("FIRST|XAUUSD|1262304000|1420070400|1420070400|100000"); var rch = st.M1Reach;
        Check(st.FirstDate["XAUUSD"].Year == 2010 && st.MaxBars == 100000 && rch < DateTime.Today.AddDays(-95) && rch > DateTime.Today.AddDays(-110), "FIRST: the broker's first date + MT5's Max bars in chart (100,000 = about 3½ months of 1-minute candles)", rch.ToString("yyyy-MM-dd"));
        st.Apply("FIRST|XAUUSD|1262304000|1420070400|1420070400|0"); Check(st.M1Reach == DateTime.MinValue, "Max bars unknown / unlimited = no limit");
        HistoryStore.Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-test-" + Guid.NewGuid().ToString("N"));
        HistoryStore.SaveDay("XAUUSD", day1, test); HistoryStore.SaveSpec(Gold());
        var back = HistoryStore.LoadRange("XAUUSD", day1, day1); var spec = HistoryStore.LoadSpec("XAUUSD");
        Check(back.Count == test.Count && Math.Abs(back[10].H - test[10].H) < 1e-9 && back[10].Spread == 20 && spec != null && spec.Contract == 100 && HistoryStore.Symbols().Contains("XAUUSD"), "a day saved on this PC loads back the same (and its symbol spec)");
        var fake = new FakeMt5(); var dl = new HistoryDownloader(fake, "EURUSD", new DateTime(2026, 9, 1), new DateTime(2026, 9, 10), new DateTime(2026, 9, 2), new DateTime(2026, 9, 3), true); var now = DateTime.Now;
        for (int k = 0; k < 80 && dl.Step(now); k++) now = now.AddSeconds(0.5);
        Check(dl.Done && !dl.Failed && dl.Saved == 8 && HistoryStore.HasDay("EURUSD", new DateTime(2026, 9, 9)) && fake.Asked == 3, "candles a WEEK per request (10 days = 2 requests + 1 retry while MT5 loads), 8 weekdays saved", dl.Status + " asked " + fake.Asked);
        Check(fake.AskedTicks == 2 && dl.TickDays == 2 && HistoryStore.HasTicks("EURUSD", new DateTime(2026, 9, 2)) && !HistoryStore.HasTicks("EURUSD", new DateTime(2026, 9, 4)) && dl.NewTickDays.Count == 2, "real ticks only for the days asked (2 Sep – 3 Sep), reported as they arrive", "ticks asked " + fake.AskedTicks);
        var dl2 = new HistoryDownloader(fake, "EURUSD", new DateTime(2026, 9, 1), new DateTime(2026, 9, 3), new DateTime(2026, 9, 2), new DateTime(2026, 9, 3), true); int a0 = fake.Asked, t0 = fake.AskedTicks;
        for (int k = 0; k < 20 && dl2.Step(now); k++) now = now.AddSeconds(0.5);
        Check(dl2.Done && fake.Asked == a0 && fake.AskedTicks == t0, "a second load reads everything from this PC (nothing asked from MT5)");
        var fk3 = new FakeMt5(); var dl3 = new HistoryDownloader(fk3, "GBPUSD", new DateTime(2026, 9, 10), new DateTime(2026, 9, 18), new DateTime(2026, 9, 16), new DateTime(2026, 9, 16), false, new DateTime(2026, 9, 16));
        for (int k = 0; k < 80 && dl3.Step(now); k++) now = now.AddSeconds(0.5);
        Check(dl3.Done && fk3.FirstHist == Mt5State.Epoch(new DateTime(2026, 9, 16)) && HistoryStore.HasDay("GBPUSD", new DateTime(2026, 9, 11)), "the asked days are downloaded FIRST, the warm-up days before them last", "first asked " + fk3.FirstHist);
        try { System.IO.Directory.Delete(HistoryStore.Root, true); } catch { }
        // 8. two symbols replayed together on one account; RAW vs STANDARD; MT5's swap in points; the account saved and loaded back
        var nas = new SimSpec { Name = "USTEC", Digits = 2, Point = 0.01, Contract = 1, TickValue = 0.01, TickSize = 0.01, VolMin = 0.1, VolStep = 0.1, VolMax = 50, NyOffsetHours = -7 };
        var nasBars = Bars(day1, 70, 20000, i => i < 30 ? 2 : -3, 120);
        sb = new SimBroker(); var err2 = sb.Load(set, new List<Tuple<SimSpec, List<Mt5Bar>>> { Tuple.Create(Gold(), all), Tuple.Create(nas, Bars(day0.AddHours(10), 120, 20000, i => 0).Concat(nasBars).ToList()) });
        Check(err2 == "" && sb.State.Symbols.Count == 2 && sb.State.SeriesOf("USTEC", 1).Bars.Count == 120, "two symbols load together (each its warm-up)", err2);
        sb.Command("ORDER", "USTEC", "BUY", 1.0, 0.0, 0.0, 0.0); sb.Command("ORDER", "XAUUSD", "SELL", 0.1, 0.0, 0.0, 0.0);
        Check(sb.State.Positions.Count == 2 && sb.State.Positions.Any(x => x.Symbol == "USTEC"), "one account trades both symbols (hedging)");
        sb.Playing = true; sb.Speed = 600; sb.JumpTo(day1.AddMinutes(30));
        var nasPos = sb.State.Positions.First(x => x.Symbol == "USTEC");
        Check(Math.Abs(nasPos.Profit - Math.Round((nasPos.Current - nasPos.Open) * 1.0, 2)) < 0.02 && nasPos.Current > nasPos.Open + 50, "USTEC's P/L uses ITS spec ($1 per point per lot), XAUUSD's its own", nasPos.Profit + " " + nasPos.Current + " " + nasPos.Open);
        Check(sb.State.Quotes["USTEC"].Time == sb.State.Quotes["XAUUSD"].Time || Math.Abs((sb.State.Quotes["USTEC"].Time - sb.State.Quotes["XAUUSD"].Time).TotalSeconds) < 60, "both symbols move on the same clock");
        var std = new SimSettings { Symbol = "XAUUSD", From = day1, To = day1, Balance = 10000, CommissionPerLot = 7, Kind = "RAW", MarkupPts = 30 };
        sb = Run(all, std); double sBid = sb.State.Quotes["XAUUSD"].Bid, sAsk = sb.State.Quotes["XAUUSD"].Ask; sb.Command("ORDER", "XAUUSD", "BUY", 1.0, 0.0, 0.0, 0.0);
        Check(Math.Abs(sAsk - sBid - 0.50) < 1e-9 && Math.Abs(Bal(sb) - 10000) < 1e-9, "RAW: no commission, the spread wider by its markup (20 + 30 points)", (sAsk - sBid) + " " + Bal(sb));
        var acc = new SimAccount(new Mt5State()); var gsp = Gold(); gsp.SwapMode = 1; gsp.SwapLong = -40; acc.Specs["XAUUSD"] = gsp;
        Check(Math.Abs(acc.SwapPerLot(gsp, 1, 4000) + 40) < 1e-9, "MT5 swap in points: −40 points × $1 per point per lot = −$40 a lot a night");
        acc.Reset(new SimAccountSettings { Balance = 5000 }, day1); acc.OnPrice("XAUUSD", day1, 4000, 4000.2); acc.Do("ORDER", new object[] { "XAUUSD", "BUY", 0.5, 0.0, 3990.0, 0.0 }, day1); acc.Do("ORDER", new object[] { "XAUUSD", "SELL LIMIT", 0.2, 4010.0, 0.0, 0.0 }, day1);
        var saved = acc.Save(); var acc2 = new SimAccount(new Mt5State()); acc2.Specs["XAUUSD"] = gsp; bool ld = acc2.Load(saved);
        Check(ld && acc2.State.Positions.Count == 1 && acc2.State.Positions[0].Sl == 3990 && acc2.State.Orders.Count == 1 && Math.Abs(acc2.Balance - acc.Balance) < 1e-9 && acc2.State.Deals.Count == acc.State.Deals.Count, "the DEMO account is saved and comes back (positions, orders, balance, deals)");
        var mk = new Mt5State(); mk.Apply("SYM|XAUUSD|2|100.00|0.01|0.01|100.00|0.01000000|1.000000|0.01000000|Gold"); mk.Apply("SYMX|XAUUSD|-45.5|12.1|1|5|1000.00|2|10");
        var view = new Mt5State(mk); mk.Apply("TICK|XAUUSD|4000.1|4000.3|1792800000000");
        Check(mk.Symbols["XAUUSD"].HasX && mk.Symbols["XAUUSD"].SwapLong == -45.5 && mk.Symbols["XAUUSD"].Swap3Day == 5 && view.Quotes["XAUUSD"].Bid == 4000.1 && ReferenceEquals(view.Gate, mk.Gate) && view.Positions != mk.Positions, "SYMX → MT5's swap, triple day; the DEMO view shares the live prices but has its own positions");
        // 9. MT5's REAL TICKS: the replay plays them exactly (time, bid, ask), the candles are built from them; saved / loaded on this PC
        var tk = new List<SimTick>(); var rnd = new Random(5); double pb = 4000;
        for (int i = 0; i < 70 * 60; i += 3) { pb = Math.Round(pb + (rnd.NextDouble() - 0.5) * 0.4, 2); tk.Add(new SimTick { T = day1.AddSeconds(i).AddMilliseconds(rnd.Next(0, 900)), Bid = pb, Ask = Math.Round(pb + 0.11 + rnd.Next(0, 5) * 0.01, 2) }); }
        var tb = new List<Mt5Bar>(); foreach (var g in tk.GroupBy(x => new DateTime(x.T.Year, x.T.Month, x.T.Day, x.T.Hour, x.T.Minute, 0))) tb.Add(new Mt5Bar { Time = g.Key, O = g.First().Bid, H = g.Max(x => x.Bid), L = g.Min(x => x.Bid), C = g.Last().Bid, V = g.Count(), Spread = 11 });
        sb = new SimBroker(); var e3 = sb.Load(set, new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> { Tuple.Create(Gold(), warm.Concat(tb).ToList(), tk) });
        sb.Playing = true; sb.Speed = 1; var seen = new List<Tuple<DateTime, double, double>>();
        for (int k = 0; k < 400; k++) { sb.Advance(0.1); var q = sb.State.Quotes["XAUUSD"]; if (seen.Count == 0 || seen[seen.Count - 1].Item1 != q.Time) seen.Add(Tuple.Create(q.Time, q.Bid, q.Ask)); }
        var bad = seen.Skip(1).Where(x => !tk.Any(t => t.T == x.Item1 && t.Bid == x.Item2 && Math.Abs(t.Ask - x.Item3) < 1e-9)).ToList(); bool exact = bad.Count == 0;
        foreach (var x in bad.Take(3)) Console.WriteLine("   not a tick: " + x.Item1.ToString("HH:mm:ss.fff") + " " + x.Item2 + " " + x.Item3 + " | " + string.Join(", ", tk.Where(t => Math.Abs((t.T - x.Item1).TotalSeconds) < 2).Select(t => t.T.ToString("ss.fff") + " " + t.Bid + "/" + t.Ask)));
        Check(e3 == "" && sb.HasTicks && seen.Count > 5 && exact && Math.Abs((sb.Clock - day1).TotalSeconds - 40) < 2, "1× plays MT5's real ticks: every price is a real tick (its time, bid and ask), 40 s of replay = 40 s of market", seen.Count + " ticks, clock " + sb.Clock.ToString("HH:mm:ss.fff"));
        sb.Speed = SimBroker.MaxSpeed; for (int k = 0; k < 30 && !sb.Ended; k++) sb.Advance(0.05);
        var m1b = sb.State.SeriesOf("XAUUSD", 1).Bars; var lastReal = m1b.Last();
        Check(sb.RealShare > 0.99 && Math.Abs(lastReal.H - tb.Last().H) < 1e-9 && Math.Abs(lastReal.C - tb.Last().C) < 1e-9, "the candles built from the real ticks = MT5's candles; 100 % of the replay from real ticks", sb.RealShare.ToString("P0"));
        HistoryStore.Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-test-" + Guid.NewGuid().ToString("N"));
        HistoryStore.SaveTicks("XAUUSD", day1, tk); var tk2 = HistoryStore.LoadTicks("XAUUSD", day1);
        Check(tk2.Count == tk.Count && tk2[100].T == tk[100].T && tk2[100].Bid == tk[100].Bid && tk2[100].Ask == tk[100].Ask, "the real ticks are saved on this PC and load back to the millisecond");
        var mk2 = new Mt5State(); mk2.Apply("TT|XAUUSD|1792800000|1792886400|2|0"); mk2.Apply("T|1500|4000.10|4000.25"); mk2.Apply("T|2750|4000.12|4000.26"); mk2.Apply("TTEND|XAUUSD|1792800000|1792886400");
        Check(mk2.TickHist.ContainsKey("XAUUSD|1792800000") && mk2.TickHist["XAUUSD|1792800000"].Item1[1].T == Mt5State.Unix(1792800000).AddMilliseconds(2750), "TT … TTEND → the day's real ticks with their milliseconds");
        try { System.IO.Directory.Delete(HistoryStore.Root, true); } catch { }
        // 10. margin like MT5 in hedging: MT5's own margin per lot, the hedged margin (0 = an opposite trade frees the margin, e.g. NAS100), larger leg
        var am = new SimAccount(new Mt5State()); var nq = new SimSpec { Name = "USTEC", Digits = 2, Point = 0.01, Contract = 1, TickValue = 0.01, TickSize = 0.01, VolMin = 0.1, VolStep = 0.1, VolMax = 50, MarginPerLot = 200, MarginRefPrice = 20000, HedgedRatio = 0, MarginLeverage = 500 };
        var au = Gold(); au.MarginPerLot = 400; au.MarginRefPrice = 4000; au.HedgedRatio = 1; au.MarginLeverage = 500;
        am.Specs["USTEC"] = nq; am.Specs["XAUUSD"] = au; am.Reset(new SimAccountSettings { Balance = 10000, Leverage = 500, CommissionPerLot = 0 }, day1); am.OnPrice("USTEC", day1, 20000, 20001); am.OnPrice("XAUUSD", day1, 4000, 4000.2);
        am.Do("ORDER", new object[] { "USTEC", "BUY", 1.0, 0.0, 0.0, 0.0 }, day1); double m1n = am.State.Account.Margin;
        am.Do("ORDER", new object[] { "USTEC", "SELL", 1.0, 0.0, 0.0, 0.0 }, day1); double m2n = am.State.Account.Margin;
        Check(Math.Abs(m1n - 200) < 0.01 && m2n < 0.01, "USTEC (hedged margin 0): a buy uses $200 (MT5's number), the opposite sell frees it → margin $0", m1n + " → " + m2n);
        am.Do("ORDER", new object[] { "XAUUSD", "BUY", 1.0, 0.0, 0.0, 0.0 }, day1); am.Do("ORDER", new object[] { "XAUUSD", "SELL", 1.0, 0.0, 0.0, 0.0 }, day1);
        Check(Math.Abs(am.State.Account.Margin - 800) < 0.5, "XAUUSD (hedged margin = full): a buy and a sell both pay → $800", am.State.Account.Margin + "");
        // 11. back in time: the replay runs again to an earlier moment, the trades before it re-done exactly, the ones after it gone
        var rw = Run(all, set); rw.StepBar(1); rw.StepBar(1); rw.Command("ORDER", "XAUUSD", "BUY", 1.0, 0.0, 0.0, 0.0); var buyT = rw.State.Positions[0].Ticket; double buyAt = rw.State.Positions[0].Open;
        for (int k = 0; k < 5; k++) rw.StepBar(1); var mid = rw.Clock; rw.Command("CLOSE", buyT, 0.5); for (int k = 0; k < 5; k++) rw.StepBar(1); rw.Command("ORDER", "XAUUSD", "SELL", 2.0, 0.0, 0.0, 0.0);
        var late = rw.Clock; for (int k = 0; k < 3; k++) rw.StepBar(1); int after = rw.CommandsAfter(mid.AddMinutes(1));
        var e11 = rw.RewindTo(mid.AddMinutes(1)); var bk = rw.State.Positions;
        Check(e11 == "" && after == 1 && rw.Clock == mid.AddMinutes(1) && bk.Count == 1 && bk[0].Ticket == buyT && Math.Abs(bk[0].Lots - 0.5) < 1e-9 && bk[0].Open == buyAt && rw.State.SeriesOf("XAUUSD", 1).Bars.Last().Time == mid.AddMinutes(1),
            "⏪ back 1 minute after the half close: the same ticket, 0.50 lots left at the same price, the later SELL is gone, the chart ends there", e11 + " " + rw.Clock + " " + bk.Count);
        var pb5 = rw.PrevBar(5); Check(pb5 < rw.Clock && pb5.Minute % 5 == 0, "◀ previous M5 candle = the start of the M5 candle before", pb5.ToString("HH:mm"));
        Check(rw.PrevDayStart() == rw.StartTime, "◀◀ previous day inside the first day = its start");
        // 11b. real ticks that arrive in the background after the start are played when the replay reaches them
        var ab = Run(all, set); ab.StepBar(1); var tl = new List<SimTick>(); var tday = day1.AddMinutes(10);
        for (int k = 0; k < 120; k++) tl.Add(new SimTick { T = tday.AddMilliseconds(500 * k), Bid = 4006 + 0.01 * k, Ask = 4006.2 + 0.01 * k });
        int added = ab.AddTicks("XAUUSD", tl); ab.JumpTo(day1.AddMinutes(12));
        Check(added == 120 && ab.HasTicks && ab.RealShare > 0 && ab.State.SeriesOf("XAUUSD", 1).Bars.Any(b => b.Time == tday && Math.Abs(b.H - 4007.19) < 1e-6), "ticks added after the start: the minute they cover is played from them", added + " added, real " + ab.RealShare.ToString("P0"));
        // 12. the POOL: the finished days of a run saved with their trades, read back, grouped by week / month, and the HTML tracker
        var d2 = day1.AddDays(1); var two = Bars(day1, 300, 4000, i => (i % 11) - 5).Concat(Bars(d2, 300, 4010, i => (i % 9) - 4)).ToList();
        var pl = Run(warm.Concat(two).ToList(), new SimSettings { Symbol = "XAUUSD", From = day1, To = d2, Balance = 10000, CommissionPerLot = 7, Leverage = 500 });
        for (int k = 0; k < 3; k++) { pl.StepBar(15); pl.Command("ORDER", "XAUUSD", k % 2 == 0 ? "BUY" : "SELL", 0.5, 0.0, 0.0, 0.0); pl.StepBar(30); pl.Command("CLOSEALL", "", "ALL"); }
        pl.JumpTo(d2.AddHours(2)); pl.Command("ORDER", "XAUUSD", "BUY", 1.0, 0.0, 0.0, 0.0); pl.StepBar(60); pl.Command("CLOSEALL", "", "ALL");
        BtPool.Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-pool-" + Guid.NewGuid().ToString("N"));
        foreach (var dd in pl.ClosedDays.Concat(new[] { pl.Clock.Date }).Distinct()) { var b = BtPool.Build(pl.State.Deals, dd, "RAW"); if (b != null) BtPool.Save("Test A", b.Item1, b.Item2); }
        var pdays = BtPool.Days("Test A"); var ptr = BtPool.Trades("Test A"); double dealNet = pl.State.Deals.Where(x => x.IsTrade).Sum(x => x.Net);
        Check(pdays.Count == 2 && ptr.Count == 4 && pdays[0].Trades == 3 && Math.Abs(pdays.Sum(x => x.Net) - dealNet) < 0.02 && Math.Abs(pdays[1].EndBal - pl.State.Account.Balance) < 0.02,
            "POOL: 2 days saved (3 + 1 trades), their net = the account's deals, the end balance = the account's", pdays.Count + " days " + ptr.Count + " trades " + pdays.Sum(x => x.Net) + " vs " + dealNet);
        var again = BtPool.Build(pl.State.Deals, day1, "RAW"); BtPool.Save("Test A", again.Item1, again.Item2);
        Check(BtPool.Days("Test A").Count == 2 && BtPool.Trades("Test A").Count == 4, "saving a day again replaces it (no double count)");
        Check(BtPool.PeriodKey(new DateTime(2026, 10, 22), "WEEKS") == "2026-10-19" && BtPool.PeriodKey(new DateTime(2026, 10, 22), "MONTHS") == "2026-10", "weeks start on Monday, months by yyyy-MM");
        var html = BtPool.Html("Test A", pdays, ptr); System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-tracker-test.html"), html);
        Check(html.Contains("Calendar") && html.Contains("tWEEKS") && html.Contains("<polyline"), "the HTML tracker: calendar, period tabs, equity curve");
        BtPool.Create("Test B"); BtPool.Clear("Test A"); Check(BtPool.Days("Test A").Count == 0 && BtPool.Tests().Count == 2, "CLEAR empties a test, NEW TEST keeps the others");
        try { System.IO.Directory.Delete(BtPool.Root, true); } catch { }
        Console.WriteLine(fails == 0 ? "ALL BACKTEST TESTS PASSED" : fails + " BACKTEST TEST(S) FAILED");
        return fails;
    }
    // MT5 through the bridge, faked: answers HIST at once (the 2nd day first says "still downloading")
    sealed class FakeMt5 : IBroker
    {
        readonly Mt5State st = new Mt5State(); public int Asked; bool once;
        public Mt5State State { get { return st; } } public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
        public string Command(string c, params object[] a) { return null; }
        public int AskedTicks; public long FirstHist;
        public bool Send(string line)
        {
            var a = line.Split('|');
            if (a[0] == "HISTT")
            {
                AskedTicks++; long f0 = long.Parse(a[2]);
                st.Apply("TT|" + a[1] + "|" + a[2] + "|" + a[3] + "|3|0"); st.Apply("T|1000|1.10|1.11"); st.Apply("T|2500|1.12|1.13"); st.Apply("T|61000|1.11|1.12"); st.Apply("TTEND|" + a[1] + "|" + a[2] + "|" + a[3]); return true;
            }
            if (a[0] != "HIST") return true; Asked++; long from = long.Parse(a[2]), to = long.Parse(a[3]); if (FirstHist == 0) FirstHist = from;
            bool busy = !once; if (busy) once = true;   // MT5 is still loading the first time
            var bars = new List<string>(); for (long d = from; d < to; d += 86400) { var dt = Mt5State.Unix(d); if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday) continue; bars.Add("H|" + d + "|1.1|1.2|1.0|1.15|5|3"); bars.Add("H|" + (d + 60) + "|1.15|1.2|1.1|1.12|5|3"); }
            st.Apply("HIST|" + a[1] + "|" + a[2] + "|" + a[3] + "|" + (busy ? 0 : bars.Count) + "|" + (busy ? 4401 : 0));
            if (!busy) foreach (var b in bars) st.Apply(b);
            st.Apply("HISTEND|" + a[1] + "|" + a[2] + "|" + a[3]); return true;
        }
    }
}
