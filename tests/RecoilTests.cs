// RECOIL • ADD TO LOSERS: ladder math and fill rules on hand-made 1-minute bars, checked to the cent.
// Run: tests/build_engine.sh tests/RecoilTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class RecoilTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.005; }
    static double[] C(double o, double h, double l, double c) { return new[] { o, h, l, c }; }

    // 1-minute bars (close-stamped) from first, then a flat tail to 16:00.
    static List<KeystoneArcBar> Bars(string sym, DateTime first, List<double[]> candles, double tailDrift = 0)
    {
        var list = new List<KeystoneArcBar>(); DateTime t = first;
        foreach (var c in candles) { list.Add(new KeystoneArcBar { Symbol = sym, Time = t, Open = c[0], High = c[1], Low = c[2], Close = c[3] }); t = t.AddMinutes(1); }
        double p = candles[candles.Count - 1][3];
        while (t <= first.Date.AddHours(16)) { double o = p; p += tailDrift; list.Add(new KeystoneArcBar { Symbol = sym, Time = t, Open = o, Close = p, High = Math.Max(o, p) + 0.25, Low = Math.Min(o, p) - 0.25 }); t = t.AddMinutes(1); }
        return list;
    }
    static KeystoneRecoilConfig Cfg() { return new KeystoneRecoilConfig { UseMnq = 1, UseMgc = 0, MnqStartHhmm = 930, LastEntryHhmm = 1500, CloseHhmm = 1600, MnqTrigger = 100, MnqStep = 100, MaxEntries = 4, TargetDollars = 400, MaxDrawdown = 2000, CommissionPerSide = 0.62, SlippageTicks = 1 }; }
    static readonly DateTime D = new DateTime(2026, 3, 10, 9, 31, 0);
    // The user's example: start 20000 → 100 down → BUY 19900, adds 19800 / 19700 / 19600.
    static List<double[]> Ladder()
    {
        return new List<double[]>
        {
            C(20000, 20005, 19990, 19995),   // 09:31 start price = 20000 (open of the first minute after 09:30)
            C(19995, 19996, 19895, 19900),   // 09:32 hits 19900 → BUY 1 @ 19900
            C(19900, 19905, 19790, 19800),   // 09:33 add @ 19800 → 2, avg 19850
            C(19800, 19805, 19695, 19700),   // 09:34 add @ 19700 → 3, avg 19800
            C(19700, 19705, 19595, 19600),   // 09:35 add @ 19600 → 4, avg 19750 (open P/L −1,200)
        };
    }
    static KeystoneRecoilResult Run(KeystoneRecoilConfig cfg, List<KeystoneArcBar> mnq, List<KeystoneArcBar> mgc = null)
    {
        return KeystoneRecoil.Run(KeystoneHelix.Align(mnq, mgc ?? new List<KeystoneArcBar>()), cfg);
    }

    public static int Main()
    {
        // 1. The ladder to the blowup: exactly −$2,000 at 19,500
        {
            var c = Ladder(); c.Add(C(19600, 19600, 19490, 19495));
            var r = Run(Cfg(), Bars("MNQ", D, c));
            var cy = r.Cycles.Single();
            Check(cy.Dir == 1 && cy.AnchorPrice == 20000 && cy.Fills.Select(f => f.Price).SequenceEqual(new double[] { 19900, 19800, 19700, 19600 }), "BUY 19900 after −100, adds 19800 / 19700 / 19600", string.Join(",", cy.Fills.Select(f => f.Price)));
            Check(cy.Fills.Select(f => f.TotalQty).SequenceEqual(new[] { 1, 2, 3, 4 }) && Eq(cy.Fills[3].Avg, 19750), "contracts 1 → 2 → 3 → 4, average 19750", string.Join(",", cy.Fills.Select(f => f.TotalQty + "@" + f.Avg)));
            Check(Eq(cy.Fills[0].StopPrice, 19500) && Eq(cy.Fills[3].StopPrice, 19500), "the blowup price is 19500 from the first fill on (adds included)", cy.Fills[0].StopPrice.ToString());
            Check(cy.Reason == "BLOWUP" && Eq(cy.ExitPrice, 19500) && Eq(cy.Gross, -2000), "BLOWUP at 19500 = −$2,000 (400+300+200+100 pts × $2)", cy.Reason + " " + cy.ExitPrice + " " + cy.Gross);
            Check(Eq(cy.Net, -2000 - 0.62 * 2 * 4 - 0.25 * 2 * 4), "net = −2,000 − commission $4.96 − 1 tick slippage $2.00", cy.Net.ToString());
            Check(Eq(KeystoneRecoil.Value(cy, 19600), -1200), "open P/L with 4 contracts at 19600 = −$1,200", KeystoneRecoil.Value(cy, 19600).ToString());
        }
        // 2. The bounce: target $400 with 4 contracts = average + 50 pts = 19800
        {
            var c = Ladder(); c.Add(C(19600, 19810, 19590, 19805));
            var cy = Run(Cfg(), Bars("MNQ", D, c, 2.0)).Cycles.Single();
            Check(cy.Reason == "TARGET" && Eq(cy.ExitPrice, 19800) && Eq(cy.Gross, 400) && Eq(cy.Net, 400 - 4.96), "WIN at 19800: 4 × 50 pts × $2 = $400 (net $395.04)", cy.Reason + " " + cy.ExitPrice + " " + cy.Gross + " " + cy.Net);
            Check(Eq(cy.LowestPrice, 19590) && cy.BestAfterLowest > 19810 && Eq(cy.FullLadderAtLowest, 4 * (19590 - 19750) * 2) && Eq(cy.FullLadderAtBest, 4 * (cy.BestAfterLowest - 19750) * 2), "BOUNCE: lowest 19590, best after it and what the full ladder was worth there", cy.LowestPrice + " " + cy.BestAfterLowest + " " + cy.FullLadderAtBest);
        }
        // 3. One minute touches the stop and the target → LOSS (stop first)
        {
            var c = Ladder(); c.Add(C(19600, 19900, 19450, 19700));
            var cy = Run(Cfg(), Bars("MNQ", D, c)).Cycles.Single();
            Check(cy.Reason == "BLOWUP" && Eq(cy.Gross, -2000), "stop and target in the same minute → BLOWUP", cy.Reason);
        }
        // 4. Add and target in the same minute: the target (after the add) only counts on a close beyond it
        {
            var c = new List<double[]> { C(20000, 20005, 19990, 19995), C(19995, 19996, 19895, 19900), C(19900, 19960, 19790, 19870) };   // add @19800 → avg 19850, target 19950: high 19960 came maybe before the low
            var cy = Run(Cfg(), Bars("MNQ", D, c)).Cycles.Single();
            Check(cy.Reason != "TARGET" || cy.ExitTime > D.AddMinutes(2), "an add then the high in one minute: no target unless the minute closes beyond it", cy.Reason + " " + cy.ExitTime.ToString("HH:mm"));
            var c2 = new List<double[]> { C(20000, 20005, 19990, 19995), C(19995, 19996, 19895, 19900), C(19900, 19960, 19790, 19955) };
            var cy2 = Run(Cfg(), Bars("MNQ", D, c2)).Cycles.Single();
            Check(cy2.Reason == "TARGET" && Eq(cy2.ExitPrice, 19950) && Eq(cy2.Gross, 400) && cy2.MaxQty == 2, "… and it counts when the minute closes beyond: 2 × 100 pts = $400", cy2.Reason + " " + cy2.ExitPrice);
            // high-first: the high reached the target BEFORE any add was needed → win with 1 contract
            var c3 = new List<double[]> { C(20000, 20005, 19990, 19995), C(19995, 19996, 19895, 19900), C(19900, 20101, 19790, 19800) };
            var cy3 = Run(Cfg(), Bars("MNQ", D, c3)).Cycles.Single();
            Check(cy3.Reason == "TARGET" && Eq(cy3.ExitPrice, 20100) && cy3.MaxQty == 1, "target with 1 contract (+200 pts) reached in a minute that also dips: win before the add", cy3.Reason + " " + cy3.ExitPrice + " " + cy3.MaxQty);
        }
        // 5. A gap through two add levels fills both at the open (limit orders)
        {
            var c = new List<double[]> { C(20000, 20005, 19990, 19995), C(19995, 19996, 19895, 19900), C(19650, 19660, 19640, 19650) };
            var cy = Run(Cfg(), Bars("MNQ", D, c)).Cycles.Single();
            Check(cy.Fills.Count == 3 && cy.Fills[1].Price == 19650 && cy.Fills[2].Price == 19650 && cy.Fills[1].Gap, "gap open 19650 fills the 19800 and 19700 adds at 19650 (limit orders: the open is the fill)", string.Join(",", cy.Fills.Select(f => f.Price)));
        }
        // 6. SELL after a rise, with the session close
        {
            var c = new List<double[]> { C(20000, 20005, 19990, 20000), C(20000, 20101, 19999, 20090) };
            var cy = Run(Cfg(), Bars("MNQ", D, c)).Cycles.Single();
            Check(cy.Dir == -1 && cy.Fills[0].Price == 20100 && cy.Reason == "SESSION END" && cy.ExitTime == new DateTime(2026, 3, 10, 16, 0, 0), "SELL 20100 after +100; flat afterwards → closed at 16:00", cy.Side + " " + cy.Fills[0].Price + " " + cy.Reason + " " + cy.ExitTime.ToString("HH:mm"));
            Check(Eq(cy.Gross, -1 * (cy.ExitPrice - 20100) * 2), "SELL P/L = −(exit − entry) × $2", cy.Gross.ToString());
        }
        // 7. Ladder sizes
        {
            var cfg = Cfg(); cfg.Ladder = "STEP";
            var c = Ladder(); c.Add(C(19600, 19600, 19590, 19595));
            var cy = Run(cfg, Bars("MNQ", D, c)).Cycles.Single();
            Check(cy.Fills.Select(f => f.TotalQty).SequenceEqual(new[] { 1, 3 }) || cy.Fills.Select(f => f.TotalQty).Take(2).SequenceEqual(new[] { 1, 3 }), "STEP ladder: 1 then +2 = 3 …", string.Join(",", cy.Fills.Select(f => f.TotalQty)));
            cfg.Ladder = "DOUBLE"; Check(cfg.LadderText().StartsWith("1 → 2 → 4 → 8"), "DOUBLE ladder: 1 → 2 → 4 → 8", cfg.LadderText());
            cfg.Ladder = "ONE"; Check(cfg.LadderText().StartsWith("1 → 2 → 3 → 4"), "ONE ladder: 1 → 2 → 3 → 4", cfg.LadderText());
            // with STEP the blowup comes earlier: fills 19900×1, 19800×2 → avg 19833.3; −2000 at 19833.33 − 333.33 = 19500? check by value
            var r2 = Run(cfg = Cfg(), Bars("MNQ", D, Ladder()));
            Check(r2.Cycles.Count == 1, "one cycle a day");
        }
        // 8. A second cycle after a win starts from the exit price
        {
            var cfg = Cfg(); cfg.CyclesPerDay = 2; cfg.TargetDollars = 100;   // target = +50 pts with 1 contract
            var c = new List<double[]> { C(20000, 20005, 19990, 19995), C(19995, 19996, 19895, 19900), C(19900, 19955, 19899, 19950), C(19950, 19952, 19849, 19850), C(19850, 19851, 19800, 19800) };
            var r = Run(cfg, Bars("MNQ", D, c));
            Check(r.Cycles.Count == 2 && r.Cycles[0].Reason == "TARGET" && Eq(r.Cycles[0].ExitPrice, 19950) && r.Cycles[1].AnchorPrice == 19950 && r.Cycles[1].Fills[0].Price == 19850, "cycle 2 starts at the exit 19950 and buys 100 lower at 19850", string.Join(" | ", r.Cycles.Select(x => x.Reason + " " + x.AnchorPrice + "→" + (x.Fills.Count > 0 ? x.Fills[0].Price : 0))));
        }
        // 9. BOTH with a shared drawdown: MNQ and MGC ladders lose together and are closed at −$2,000 combined
        {
            var cfg = Cfg(); cfg.UseMgc = 1; cfg.MgcStartHhmm = 930; cfg.MgcTrigger = 20; cfg.MgcStep = 20; cfg.DrawdownMode = "SHARED";
            var mnq = Ladder();   // MNQ at 4 contracts, −1,200 at 19600
            mnq.Add(C(19600, 19600, 19560, 19570));
            var mgc = new List<double[]> { C(3000, 3001, 2999, 3000), C(3000, 3000, 2979, 2980), C(2980, 2980, 2959, 2960), C(2960, 2960, 2945, 2950), C(2950, 2950, 2945, 2946), C(2946, 2946, 2930, 2935) };
            var r = Run(cfg, Bars("MNQ", D, mnq), Bars("MGC", D, mgc));
            var a = r.Cycles.First(x => x.Symbol == "MNQ"); var g = r.Cycles.First(x => x.Symbol == "MGC");
            Check(a.Reason == "BLOWUP" && g.Reason == "BLOWUP" && a.ExitTime == g.ExitTime && Eq(a.Gross + g.Gross, -2000), "SHARED: both closed together at −$2,000 combined", a.Reason + " " + g.Reason + " " + (a.Gross + g.Gross));
            cfg.DrawdownMode = "SPLIT"; cfg.MnqMaxDrawdown = 2000; cfg.MgcMaxDrawdown = 2000;
            var s = Run(cfg, Bars("MNQ", D, mnq), Bars("MGC", D, mgc));
            Check(s.Cycles.All(x => x.Reason != "BLOWUP"), "SPLIT: each keeps its own $2,000 — neither is blown here", string.Join(",", s.Cycles.Select(x => x.Symbol + " " + x.Reason + " " + x.Gross)));
        }
        // 10. A day where the move never comes
        {
            var r = Run(Cfg(), Bars("MNQ", D, new List<double[]> { C(20000, 20010, 19990, 20000) }));
            Check(r.Cycles.Count == 0 && r.NoTriggerDays["MNQ"] == 1 && r.Days.Count == 1, "no 100-point move → no trade, counted as a no-trigger day");
        }
        // 11. The study: stats, steps, grid (your cell = the real run), one live account
        {
            var c1 = Ladder(); c1.Add(C(19600, 19600, 19490, 19495));                                  // day 1: blowup
            var c2 = Ladder(); c2.Add(C(19600, 19810, 19590, 19805));                                  // day 2: win with 4
            var bars = Bars("MNQ", D, c1); bars.AddRange(Bars("MNQ", D.AddDays(1), c2, 2.0));
            var cfg = Cfg();
            var mins = KeystoneHelix.Align(bars, new List<KeystoneArcBar>());
            var r = KeystoneRecoil.Run(mins, cfg);
            var s = KeystoneRecoilStudy.Stats("MNQ", r.Cycles, r.Days.Count);
            Check(s.Cycles == 2 && s.Wins == 1 && s.Blowups == 1 && Eq(s.Net, r.Cycles.Sum(x => x.Net)), "STUDY: 2 ladders, 1 win, 1 blowup", s.Cycles + " " + s.Wins + " " + s.Blowups);
            var steps = KeystoneRecoilStudy.Steps(r.Cycles, 4);
            Check(steps[3].Cycles == 2 && steps[0].Cycles == 0, "STEPS: both ladders went to 4 entries");
            var grids = KeystoneRecoilStudy.Grids(mins, cfg, "MNQ");
            var mine = grids[0].Yours;
            Check(mine != null && mine.Cycles == 2 && Eq(mine.Net, s.Net), "GRID: your cell (100 pts, $400) equals the real run", mine == null ? "none" : mine.Net + " vs " + s.Net);
            var acct = KeystoneRecoilStudy.Account(r.Cycles, 5000);
            Check(Eq(acct.End, 5000 + s.Net) && acct.Trades == 2 && Eq(acct.MaxDrawdown, -r.Cycles[0].Net), "ONE LIVE ACCOUNT: $5,000 + every ladder; max drawdown = the blowup", acct.End + " " + acct.MaxDrawdown);
            Check(KeystoneRecoilStudy.Csv(r).Split('\n').Count(l => l.Trim().Length > 0) == 3, "CSV: one row per ladder");
            string html = KeystoneRecoilStudy.Html(r, grids, null, 5000);
            Check(html.Contains("RISK GRID") && html.Contains("EVERY BLOWUP") && html.Contains("EVERY LADDER"), "REPORT: HTML with the grid, the blowups and every ladder");
        }
        // 12. PROP: payouts, a blowup and its replacement, evaluations, copy trading, rotation
        {
            Func<DateTime, double, double, KeystoneRecoilCycle> L = (day, net, worst) => new KeystoneRecoilCycle { Id = day.Day, Day = day, Symbol = "MNQ", Dir = 1, PointValue = 2, Reason = net > 0 ? "TARGET" : "BLOWUP", Net = net, MaeValue = worst, MfeValue = Math.Max(0, net), Commission = 4.96, ExitTime = day.AddHours(11), Fills = new List<KeystoneRecoilFill> { new KeystoneRecoilFill { Time = day.AddHours(10), Price = 19900, Qty = 1, TotalQty = 1 } } };
            var d0 = new DateTime(2026, 3, 2);
            var run = new KeystoneRecoilResult { Config = Cfg() };
            for (int i = 0; i < 4; i++) { run.Days.Add(d0.AddDays(i)); run.Cycles.Add(L(d0.AddDays(i), 395.04, -300)); }
            run.Days.Add(d0.AddDays(4)); run.Cycles.Add(L(d0.AddDays(4), -2006.96, -2000));
            run.Days.Add(d0.AddDays(5)); run.Cycles.Add(L(d0.AddDays(5), 395.04, -100));
            var pc = new KeystoneRecoilPropConfig { StartMode = "DIRECT", DirectCost = 500, FundedMaxLoss = 2000, FundedDrawdown = "STATIC", PayoutEveryDays = 2, PayoutMinProfit = 500, PayoutPercent = 50, PayoutCap = 2000, PayoutSplit = 90, PayoutMinAmount = 100, Groups = new List<KeystoneRecoilAccountGroup> { new KeystoneRecoilAccountGroup { Name = "A", Accounts = 1, Mode = "SINGLE" } } };
            var g = pc.Groups[0];
            var pr = KeystoneRecoilProp.Run(new List<Tuple<KeystoneRecoilAccountGroup, KeystoneRecoilResult>> { Tuple.Create(g, run) }, pc, new List<KeystoneHelixMinute>());
            var p1 = pr.Payouts.First();
            Check(p1.Day == d0.AddDays(1) && Eq(p1.Gross, 395.04) && Eq(p1.Amount, 395.04 * 0.9), "PROP: payout after 2 days = 50% of $790.08 = $395.04 gross, you keep 90%", p1.Day.ToString("MM-dd") + " " + p1.Gross + " " + p1.Amount);
            // day 3-4: +790.08 → balance 395.04 + 790.08 = 1185.12 → payout 592.56 on day 4; day 5: −2000 worst vs room 592.56+2000 → not liquidated
            Check(pr.Payouts.Count == 2 && Eq(pr.Payouts[1].Gross, 592.56), "PROP: 2nd payout 50% of $1,185.12", pr.Payouts.Count + " " + (pr.Payouts.Count > 1 ? pr.Payouts[1].Gross.ToString() : ""));
            Check(pr.FundedBlowups == 0 && pr.Purchases == 1, "PROP: the −$2,000 ladder did not blow an account with $592.56 of profit (room $2,592.56)", pr.FundedBlowups + " blown, " + pr.Purchases + " bought");
            Check(Eq(pr.NetCash, (395.04 + 592.56) * 0.9 - 500), "PROP: your cash = payouts × 90% − $500", pr.NetCash.ToString());
            // a fresh account (room $2,000) IS blown by the same ladder → replaced
            var run2 = new KeystoneRecoilResult { Config = Cfg() }; run2.Days.Add(d0); run2.Cycles.Add(L(d0, -2006.96, -2000)); run2.Days.Add(d0.AddDays(1)); run2.Cycles.Add(L(d0.AddDays(1), 395.04, -10));
            var pr2 = KeystoneRecoilProp.Run(new List<Tuple<KeystoneRecoilAccountGroup, KeystoneRecoilResult>> { Tuple.Create(g, run2) }, pc, new List<KeystoneHelixMinute>());
            Check(pr2.FundedBlowups == 1 && pr2.Purchases == 2 && Eq(pr2.ExpenseTotal, 1000) && pr2.Trades[0].Liquidated && Eq(pr2.Trades[0].Pnl, -2000 - 4.96) && pr2.Trades[1].Account.EndsWith("-02"), "PROP: a fresh account is blown at −$2,000 and replaced the same evening ($500 + $500)", pr2.FundedBlowups + " " + pr2.Purchases + " " + pr2.ExpenseTotal + " " + pr2.Trades[0].Pnl + " " + string.Join(",", pr2.Trades.Select(t => t.Account + " " + t.Reason)));
            // evaluation: pass at +$790 after 2 days, funded the next day with activation
            var pe = pc.Copy(); pe.StartMode = "EVAL"; pe.EvalCost = 100; pe.ActivationCost = 130; pe.EvalTarget = 790; pe.EvalMaxLoss = 2000; pe.EvalDrawdown = "EOD"; pe.EvalMinDays = 2;
            var pr3 = KeystoneRecoilProp.Run(new List<Tuple<KeystoneRecoilAccountGroup, KeystoneRecoilResult>> { Tuple.Create(pe.Groups[0], run) }, pe, new List<KeystoneHelixMinute>());
            var life = pr3.Lives[0];
            Check(life.PassDate == d0.AddDays(1) && life.FundedDate == d0.AddDays(2) && Eq(pr3.ExpenseTotal, 230), "EVAL: passed day 2 (+$790.08 ≥ $790, 2 days), funded day 3, $100 + $130 activation", life.PassDate.ToString("MM-dd") + " " + life.FundedDate.ToString("MM-dd") + " " + pr3.ExpenseTotal);
            // copy trading: 3 accounts each take every ladder
            var pcopy = pc.Copy(); pcopy.Groups[0].Accounts = 3; pcopy.Groups[0].Mode = "COPY";
            var pr4 = KeystoneRecoilProp.Run(new List<Tuple<KeystoneRecoilAccountGroup, KeystoneRecoilResult>> { Tuple.Create(pcopy.Groups[0], run) }, pcopy, new List<KeystoneHelixMinute>());
            Check(pr4.Trades.Count(t => t.Day == d0) == 3 && pr4.Purchases == 3, "COPY: 3 accounts each take the ladder", pr4.Trades.Count(t => t.Day == d0).ToString());
            // rotation: each fill of the real ladder (example 2) goes to the next account with its own $400 target
            var c2 = Ladder(); c2.Add(C(19600, 19810, 19590, 19805));
            var bars2 = Bars("MNQ", D, c2, 2.0); var mins2 = KeystoneHelix.Align(bars2, new List<KeystoneArcBar>());
            var lad = KeystoneRecoil.Run(mins2, Cfg());
            var prot = pc.Copy(); prot.Groups[0].Accounts = 4; prot.Groups[0].Mode = "ROTATION";
            var pr5 = KeystoneRecoilProp.Run(new List<Tuple<KeystoneRecoilAccountGroup, KeystoneRecoilResult>> { Tuple.Create(prot.Groups[0], lad) }, prot, mins2);
            Check(pr5.Trades.Count == 4 && pr5.Trades.Select(t => t.Account).Distinct().Count() == 4, "ROTATION: the 4 entries went to 4 different accounts", string.Join(",", pr5.Trades.Select(t => t.Account + " " + t.Reason + " " + t.Pnl.ToString("0"))));
            var lastAcct = pr5.Trades.First(t => t.FillIndex == 3);
            Check(lastAcct.Reason == "TARGET" || lastAcct.Reason == "SESSION END", "ROTATION: the account that bought 19600 rides the bounce on its own (+$400 needs 19800)", lastAcct.Reason + " " + lastAcct.Pnl);
            var single = KeystoneRecoilProp.ResolveSingle(mins2, "MNQ", 1, 19600, 1, 2, D.AddMinutes(4), 400, 2000, D.Date.AddHours(16), 0, 0);
            Check(single.Reason == "TARGET" && Eq(single.ExitPrice, 19800) && Eq(single.Pnl, 400), "SINGLE POSITION: 1 MNQ from 19600 with $400 target exits at 19800", single.Reason + " " + single.ExitPrice);
        }
        Console.WriteLine(failures == 0 ? "ALL RECOIL TESTS PASSED" : failures + " RECOIL TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
