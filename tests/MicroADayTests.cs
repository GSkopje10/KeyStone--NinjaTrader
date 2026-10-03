// MICRO A DAY: 1 micro at the 18:00 open, held through every session to the take profit or the 16:59 close.
// Run: tools/compile_engine.sh tests/.build/mad.exe tests/MicroADayTests.cs && mono tests/.build/mad.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class MicroADayTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.005; }
    // one Globex session: Sunday 18:01 → Monday 16:59, flat at p unless overridden
    static List<KeystoneArcBar> Session(string sym, DateTime open, double p, Dictionary<int, double[]> o)
    {
        var list = new List<KeystoneArcBar>(); int minutes = (int)(open.Date.AddDays(1).AddHours(16).AddMinutes(59) - open).TotalMinutes;
        for (int m = 1; m <= minutes; m++) { double[] v; var t = open.AddMinutes(m); if (o.TryGetValue(m, out v)) list.Add(new KeystoneArcBar { Symbol = sym, Time = t, Open = v[0], High = v[1], Low = v[2], Close = v[3] }); else list.Add(new KeystoneArcBar { Symbol = sym, Time = t, Open = p, High = p + 0.25, Low = p - 0.25, Close = p }); }
        return list;
    }
    static KeystoneArcRunConfig Cfg(string dir, double mnqTp, double mgcTp)
    {
        return new KeystoneArcRunConfig { StrategyCode = "MAD", Scope = "BOTH", SessionMode = "FULL_GLOBEX", EndTime = 1659, SetupMinutes = 1, OutcomeModelEnabled = 1, MadDirection = dir, MadMnqTarget = mnqTp, MadMgcTarget = mgcTp, MadQty = 1,
            Start = new DateTime(2025, 6, 1, 18, 0, 0), End = new DateTime(2025, 6, 3, 16, 59, 0) };
    }
    public static int Main()
    {
        var open = new DateTime(2025, 6, 1, 18, 0, 0);   // Sunday evening open
        // MNQ: opens 20000; 03:00 (+540 min) dips to 19900; 11:00 (+1020) reaches 20120; closes 16:59 at 20000
        var mnq = Session("MNQ", open, 20000, new Dictionary<int, double[]> { { 540, new[] { 20000.0, 20000, 19900, 19950 } }, { 1020, new[] { 20000.0, 20120, 19990, 20100 } } });
        var ev = KeystoneArcEngine.DetectAndResolve(mnq, mnq, Cfg("BUY", 100, 0));
        var e = ev.SingleOrDefault();
        Check(e != null && e.SetupClass == "MAD" && Eq(e.Entry, 20000) && e.EntryTime == open, "one trade per session: BUY 1 MNQ at the 18:00 open 20000", e == null ? ev.Count.ToString() : e.Entry + " " + e.EntryTime);
        Check(e != null && e.Outcome == "WIN" && Eq(e.ExitPrice, 20100) && Eq(e.GrossPnl, 200) && e.ExitTime == open.AddMinutes(1020), "take profit +100 pts at 11:00 Monday → +$200 (1 micro), no stop on the 03:00 dip", e == null ? "" : e.Outcome + " " + e.ExitPrice + " " + e.GrossPnl);
        Check(e != null && Eq(e.TroughAfterEntry, 19900) && double.IsNaN(e.Stop), "the worst point (−100) is recorded; there is no stop");
        var hold = KeystoneArcEngine.DetectAndResolve(mnq, mnq, Cfg("BUY", 0, 0)).Single();
        Check(hold.Outcome == "SESSION EXIT" && Eq(hold.ExitPrice, 20000) && hold.ExitTime == open.Date.AddDays(1).AddHours(16).AddMinutes(59), "no take profit: held all day, closed 16:59 Monday at 20000", hold.Outcome + " " + hold.ExitPrice + " " + hold.ExitTime);
        var sell = KeystoneArcEngine.DetectAndResolve(mnq, mnq, Cfg("SELL", 100, 0)).Single();
        Check(sell.Direction == "SHORT" && sell.Outcome == "WIN" && Eq(sell.ExitPrice, 19900) && Eq(sell.GrossPnl, 200) && sell.ExitTime == open.AddMinutes(540), "SELL 1 MNQ at 20000: the 03:00 dip to 19900 is its +100 → +$200", sell.Outcome + " " + sell.ExitPrice + " " + sell.GrossPnl);
        // MGC alongside (BOTH): opens 2000, +10 at 02:00
        var mgc = Session("MGC", open, 2000, new Dictionary<int, double[]> { { 480, new[] { 2000.0, 2010.5, 1999.5, 2010 } } });
        var both = KeystoneArcEngine.DetectAndResolve(mnq.Concat(mgc).ToList(), mnq.Concat(mgc).ToList(), Cfg("BUY", 100, 10));
        var g = both.SingleOrDefault(x => x.Symbol == "MGC");
        Check(both.Count == 2 && g != null && g.Outcome == "WIN" && Eq(g.GrossPnl, 100), "BOTH: 1 MNQ + 1 MGC, gold +10 pts = +$100", both.Count + " " + (g == null ? "" : g.Outcome + " " + g.GrossPnl));
        // missing open (holiday): first bar at 19:00 → no trade that session
        var late = mnq.Where(b => b.Time > open.AddMinutes(60)).ToList();
        Check(KeystoneArcEngine.DetectAndResolve(late, late, Cfg("BUY", 100, 0)).Count == 0, "no bar within 15 minutes of the open → no trade (no invented price)");
        // COMPARE: 2 years of drifting sessions → every version, prop history, per year
        var rng = new Random(4); var all = new List<KeystoneArcBar>(); double pn = 20000, pg = 2000;
        for (var d = new DateTime(2024, 1, 7); d < new DateTime(2025, 12, 20); d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Friday || d.DayOfWeek == DayOfWeek.Saturday) continue;
            var o = d.AddHours(18);
            for (int m = 1; m <= 23 * 60 - 1; m += 1) { var t = o.AddMinutes(m); double a = pn, b = pg; pn += (rng.NextDouble() - 0.48) * 6; pg += (rng.NextDouble() - 0.49) * 0.8;
                all.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = a, High = Math.Max(a, pn) + 0.5, Low = Math.Min(a, pn) - 0.5, Close = pn }); all.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = b, High = Math.Max(b, pg) + 0.1, Low = Math.Min(b, pg) - 0.1, Close = pg }); }
        }
        var basis = Cfg("BUY", 0, 0); basis.Start = new DateTime(2024, 1, 7, 18, 0, 0); basis.End = new DateTime(2025, 12, 20, 16, 59, 0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cmp = KeystoneMicroADay.Compare(all.OrderBy(b => b.Time).ToList(), basis, new KeystonePropRules());
        Console.WriteLine("      " + cmp.Count + " versions in " + sw.ElapsedMilliseconds + " ms • #1 " + cmp[0].Label + " • net $" + cmp[0].Net.ToString("0") + " • prop " + cmp[0].PropNet.ToString("0") + " (" + cmp[0].Passed + " passed / " + cmp[0].Bought + " bought, " + cmp[0].Payouts + " payouts)");
        Check(cmp.Count == 36 && cmp.All(r => r.Trades > 400) && cmp.Any(r => r.Instruments == "BOTH" && r.Trades > 900), "36 versions (BUY/SELL × MNQ/MGC/BOTH × 6 take profits), every session traded", cmp.Count + " " + cmp.Min(r => r.Trades));
        var bh = cmp.First(r => r.Direction == "BUY" && r.Instruments == "MNQ" && r.MnqTp == 0); var sh = cmp.First(r => r.Direction == "SELL" && r.Instruments == "MNQ" && r.MnqTp == 0);
        Check(Math.Abs(bh.Net + sh.Net + 2 * bh.Trades * KeystoneMoveStudy.Cost("MNQ")) < 1, "hold to the close: BUY and SELL are exact mirrors (minus costs)", bh.Net + " vs " + sh.Net);
        Check(bh.NetByYear.Count == 2 && bh.MaxDrawdown >= 0 && bh.Bought >= 1, "per year, worst drawdown, prop history are filled");
        Console.WriteLine(failures == 0 ? "ALL MICRO A DAY TESTS PASSED" : failures + " MICRO A DAY TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
