// IDEAS: one typed line → every combination (AT / ORB / FADE / FVG), FVG rotation across accounts with the direction lock.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class IdeasTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static KeystoneArcBar B(string s, DateTime t, double o, double h, double l, double c) { return new KeystoneArcBar { Symbol = s, Time = t, Open = o, High = h, Low = l, Close = c }; }
    static KeystoneLabTrade T(int dir, int fromMin, int toMin) { var d = new DateTime(2024, 3, 5, 10, 0, 0); return new KeystoneLabTrade { Dir = dir, EntryTime = d.AddMinutes(fromMin), ExitTime = d.AddMinutes(toMin), Day = d.Date, Net = 10 }; }

    // One day of flat 1-minute bars 09:31 → 15:59 at 100, with overrides.
    static List<KeystoneArcBar> Day(string sym, DateTime day, Dictionary<int, KeystoneArcBar> over)
    {
        var l = new List<KeystoneArcBar>();
        for (var t = day.AddHours(9).AddMinutes(31); t <= day.AddHours(16); t = t.AddMinutes(1)) { int k = t.Hour * 100 + t.Minute; KeystoneArcBar b; l.Add(over.TryGetValue(k, out b) ? b : B(sym, t, 100, 100.25, 99.75, 100)); }
        return l;
    }

    public static int Main()
    {
        // ---- parse
        var p = KeystoneIdeas.Parse("BOTH FVG 1 EITHER SL C1,10 ACCOUNTS 3");
        Check(p.IsFvg && p.Error == "" && p.Symbols.Count == 2 && p.Dirs.SequenceEqual(new[] { "EITHER" }) && p.MnqSl.SequenceEqual(new[] { -1.0, 10 }) && p.MgcSl.SequenceEqual(new[] { -1.0, 1 }) && p.Accounts.SequenceEqual(new[] { 3 }), "parse: BOTH FVG 1 EITHER SL C1,10 ACCOUNTS 3 (gold stop C1 / 1)", p.Error);
        Check(p.Windows.Count == 1 && p.Windows[0].Item1 == "NY" && p.MnqGap.Count == 2, "FVG defaults: NY session, gaps 2,5");
        Check(KeystoneIdeas.Parse("MNQ AT 0930 FOO").Error.Contains("FOO"), "unknown word is an error");
        Check(KeystoneIdeas.Parse("MNQ AT 0930 EITHER").Error != "", "EITHER only for FVG");
        Check(KeystoneIdeas.Parse("MNQ AT 0930 SL C1").Error != "", "SL C1 only for FVG");
        var a = KeystoneIdeas.Parse("mnq at 0930 buy,sell tp 10 sl 10,none exit 1555");
        Check(a.Error == "" && a.Entry == "AT" && a.Times[0] == 930 && a.MnqSl.SequenceEqual(new[] { 10.0, 0 }) && a.MgcTp[0] == 1, "AT idea parses (lower case, NONE stop, gold TP = MNQ ÷ 10)", a.Error);
        Check(KeystoneIdeas.Parse("MGC FVG 5 SESSION ASIA,LONDON FILL CLOSE,50 GAP 1").Windows.Count == 2, "two sessions, two fills");
        var g1 = KeystoneIdeas.Parse("MGC AT 0820 ORB 10 BUY,SELL TP 3,5 SL 3");
        Check(g1.MgcTp.SequenceEqual(new[] { 3.0, 5 }) && g1.MgcSl.SequenceEqual(new[] { 3.0 }), "MGC alone: TP / SL are gold points");
        Check(KeystoneIdeas.Parse("BOTH AT 0930 TP 50").MgcTp[0] == 5, "BOTH: gold TP = MNQ ÷ 10 unless MGCTP");
        Check(KeystoneIdeas.Parse("MNQ FVG 1 SESSION ASIA FROM 0930 TO 1100").Windows.Count == 2 && KeystoneIdeas.Parse("MNQ FVG 1 FROM 0930 TO 1100").Windows[0].Item2 == 930, "FROM / TO window");
        Check(KeystoneIdeas.SessionDay(new DateTime(2024, 3, 4, 18, 5, 0)) == new DateTime(2024, 3, 5) && KeystoneIdeas.SessionDay(new DateTime(2024, 3, 5, 10, 0, 0)) == new DateTime(2024, 3, 5), "18:05 belongs to the next date's session");
        DateTime end;
        Check(KeystoneIdeas.InWindow(new DateTime(2024, 3, 4, 19, 0, 0), 1800, 300, out end) && end == new DateTime(2024, 3, 5, 3, 0, 0) && !KeystoneIdeas.InWindow(new DateTime(2024, 3, 5, 9, 0, 0), 1800, 300, out end), "overnight window 18:00–03:00");

        // ---- AT 09:30: up 11 then fine → BUY TP 10 wins, SELL SL 10 stops
        var d1 = new DateTime(2024, 3, 5);
        var day = Day("MNQ", d1, new Dictionary<int, KeystoneArcBar> { { 931, B("MNQ", d1.AddHours(9).AddMinutes(31), 100, 105, 99, 104) }, { 932, B("MNQ", d1.AddHours(9).AddMinutes(32), 104, 111, 103, 110) } });
        var buy = KeystoneIdeas.Trades(day, "MNQ", 930, "AT", 0, "BUY", 10, 10, 1555); var sell = KeystoneIdeas.Trades(day, "MNQ", 930, "AT", 0, "SELL", 10, 10, 1555);
        Check(buy.Count == 1 && buy[0].Outcome == "TARGET" && buy[0].Entry == 100 && buy[0].Exit == 110 && buy[0].Dir == 1, "AT 09:30 BUY TP 10 → target at 110");
        Check(sell.Count == 1 && sell[0].Outcome == "STOP" && sell[0].Exit == 110 && sell[0].Net < 0 && sell[0].Dir == -1, "AT 09:30 SELL SL 10 → stopped at 110");
        var hold = KeystoneIdeas.Trades(day, "MNQ", 930, "AT", 0, "BUY", 50, 0, 1555);
        Check(hold.Count == 1 && hold[0].Outcome == "CLOSE" && double.IsNaN(hold[0].Stop), "no stop, target not reached → out at the close");

        // ---- FVG: c1 10:01 H100 L98, c2 10:02, c3 10:03 L103 → bullish gap 3; in at 10:04 open 104
        var f = Day("MNQ", d1, new Dictionary<int, KeystoneArcBar> {
            { 1001, B("MNQ", d1.AddHours(10).AddMinutes(1), 99, 100, 98, 100) }, { 1002, B("MNQ", d1.AddHours(10).AddMinutes(2), 100, 106, 99, 105) },
            { 1003, B("MNQ", d1.AddHours(10).AddMinutes(3), 105, 108, 103, 107) }, { 1004, B("MNQ", d1.AddHours(10).AddMinutes(4), 104, 109, 103, 108) }, { 1005, B("MNQ", d1.AddHours(10).AddMinutes(5), 102, 102.5, 101, 101.8) } });
        var ft = KeystoneIdeas.FvgTrades(f, "MNQ", 1, "CLOSE", 930, 1555, -1, 2, 4, -1);
        var bull = ft.FirstOrDefault(t => t.Dir == 1 && t.EntryTime == d1.AddHours(10).AddMinutes(4));
        Check(bull != null && bull.Entry == 104 && bull.Stop == 98 && bull.Outcome == "TARGET" && bull.Exit == 108, "bullish 1-minute FVG: in at 104, stop below candle 1 (98), target +4 hit", bull == null ? "none" : bull.Entry + " " + bull.Stop + " " + bull.Outcome);
        Check(!KeystoneIdeas.FvgTrades(f, "MNQ", 1, "CLOSE", 930, 1555, -1, 5, 4, -1).Any(t => t.Dir == 1 && t.EntryTime == d1.AddHours(10).AddMinutes(4)), "GAP 5 filters out the 3-point gap");
        Check(!KeystoneIdeas.FvgTrades(f, "MNQ", 1, "CLOSE", 1100, 1555, -1, 2, 4, -1).Any(t => t.EntryTime < d1.AddHours(11)), "outside the session window: no trade");
        var half = KeystoneIdeas.FvgTrades(f, "MNQ", 1, "50", 930, 1555, -1, 2, 4, -1).FirstOrDefault(t => t.Dir == 1);
        Check(half != null && half.Entry == 101.5, "FILL 50: limit at the gap's middle (101.5)", half == null ? "none" : half.Entry.ToString());

        // ---- rotation + direction lock
        var c = new List<KeystoneLabTrade> { T(1, 0, 30), T(-1, 10, 20), T(1, 15, 40), T(-1, 45, 50) };
        var s1 = new List<int>(); var r1 = KeystoneIdeas.Rotate(c, "EITHER", 1, s1);
        Check(r1.Count == 2 && r1[0] == c[0] && r1[1] == c[3] && s1.All(x => x == 0), "1 account, EITHER: the sell during the buy is skipped, the 2nd buy waits (busy), the later sell is taken");
        var s2 = new List<int>(); var r2 = KeystoneIdeas.Rotate(c, "EITHER", 2, s2);
        Check(r2.Count == 3 && r2[1] == c[2] && s2.SequenceEqual(new[] { 0, 1, 0 }), "2 accounts: the 2nd buy goes to account 2, the sell still waits until both buys are done", string.Join(",", s2));
        var s3 = new List<int>(); var r3 = KeystoneIdeas.Rotate(c, "SELL", 1, s3);
        Check(r3.Count == 2 && r3.All(t => t.Dir < 0), "SELL only: both sells");

        // ---- rotation prop accounting: all on one account = the plain single-account numbers
        var rules = new KeystonePropRules();
        var stream = Enumerable.Range(0, 60).Select(i => new KeystoneLabTrade { Day = new DateTime(2024, 1, 1).AddDays(i), EntryTime = new DateTime(2024, 1, 1, 10, 0, 0).AddDays(i), ExitTime = new DateTime(2024, 1, 1, 10, 30, 0).AddDays(i), Net = i % 3 == 2 ? -150 : 400, Worst = i % 3 == 2 ? -150 : -50, Best = 400, Dir = 1 }).ToList();
        var single = new KeystoneLabRow { Trades = stream }; KeystoneLab.Fill(single, rules, DateTime.MaxValue, true, 0);
        var same = new KeystoneLabRow { Trades = stream, Rotate = 2, Slots = stream.Select(t => 0).ToList() }; KeystoneLab.Fill(same, rules, DateTime.MaxValue, true, 0);
        Check(same.P.Bought == single.P.Bought && Math.Abs(same.P.Cash - single.P.Cash) < 0.01 && same.P.Payouts == single.P.Payouts && same.P.Accounts.Count == single.P.Accounts.Count, "rotation with every trade on account 1 = the single-account prop numbers", same.P.Bought + "/" + single.P.Bought + " " + same.P.Cash + "/" + single.P.Cash);
        var split = new KeystoneLabRow { Trades = stream, Rotate = 2, Slots = stream.Select((t, i) => i % 2).ToList() }; KeystoneLab.Fill(split, rules, DateTime.MaxValue, true, 0);
        Check(split.P.Bought >= 2 && split.P.Net == single.P.Net && split.P.Ledger.Count == 60 && split.P.Ledger.Sum(l => l.Bought) == split.P.Bought, "two accounts: at least 2 evaluations bought, plain net unchanged, one shared ledger", split.P.Bought + " " + split.P.Ledger.Sum(l => l.Bought));

        // ---- Run: BOTH rows = MNQ + MGC trades
        var mgc = Day("MGC", d1, new Dictionary<int, KeystoneArcBar> { { 931, B("MGC", d1.AddHours(9).AddMinutes(31), 100, 100.5, 99.9, 100.4) }, { 932, B("MGC", d1.AddHours(9).AddMinutes(32), 100.4, 101.2, 100.3, 101) } });
        DateTime fi, la, sp; int n;
        var rows = KeystoneIdeas.Run(KeystoneIdeas.Parse("BOTH AT 0930 BUY TP 10 SL 10"), day.Concat(mgc).ToList(), rules, null, null, out fi, out la, out sp, out n);
        var both = rows.FirstOrDefault(r => r.Get("INSTRUMENT") == "BOTH"); var mn = rows.First(r => r.Get("INSTRUMENT") == "MNQ"); var mg = rows.First(r => r.Get("INSTRUMENT") == "MGC");
        Check(rows.Count == 3 && both != null && Math.Abs(both.P.Net - mn.P.Net - mg.P.Net) < 0.01 && both.Get("TARGET") == "TP 10 / 1", "BOTH row = MNQ + MGC (targets 10 / 1)", both == null ? "none" : both.Label);
        var fr = KeystoneIdeas.Run(KeystoneIdeas.Parse("MNQ FVG 1 GAP 2 TP 4 SL C1 ACCOUNTS 1,2"), f, rules, null, null, out fi, out la, out sp, out n);
        Check(fr.Count == 6 && fr.All(r => r.Get("ACCOUNTS") != "") && fr.Where(r => r.Get("ACCOUNTS") == "2 ACCOUNTS").All(r => r.Rotate == 2), "FVG run: EITHER/BUY/SELL × 1/2 accounts", fr.Count.ToString());
        Check(KeystoneIdeas.Advice(fr).Any(l => l.Contains("ACCOUNTS")), "advice compares account counts");
        Console.WriteLine(failures == 0 ? "ALL IDEAS TESTS PASSED" : failures + " IDEAS TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
