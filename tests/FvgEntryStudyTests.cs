// FIRST 5M FVG ENTRY STUDY on a hand-made MGC day (1-minute bars → 5-minute candles), every entry checked to the price.
// Run: tools/compile_engine.sh tests/.build/fes.exe tests/FvgEntryStudyTests.cs && mono tests/.build/fes.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class FvgEntryStudyTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.001; }
    static List<KeystoneArcBar> bars = new List<KeystoneArcBar>();
    static void Min(DateTime t, double o, double h, double l, double c) { bars.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = o, High = h, Low = l, Close = c }); }
    // one 5-minute candle ending at `end` as 5 minutes: low first, then high, then the close
    static void Five(DateTime end, double o, double h, double l, double c)
    {
        var t = end.AddMinutes(-4);
        Min(t, o, o, l, l); Min(t.AddMinutes(1), l, h, l, h); Min(t.AddMinutes(2), h, h, c, c); Min(t.AddMinutes(3), c, c, c, c); Min(t.AddMinutes(4), c, c, c, c);
    }
    static void Flat(DateTime from, DateTime to, double p) { for (var t = from; t <= to; t = t.AddMinutes(1)) Min(t, p, p + 0.25, p - 0.25, p); }

    public static int Main()
    {
        var day = new DateTime(2025, 6, 4); var ev = day.AddDays(-1);
        Flat(ev.AddHours(18).AddMinutes(1), day.AddHours(1).AddMinutes(55), 1990);
        Five(day.AddHours(2), 1990, 1990.5, 1989.5, 1990.2);          // prior c1 (high 1990.5)
        Five(day.AddHours(2).AddMinutes(5), 1990.2, 1996, 1990.2, 1995.8);
        Five(day.AddHours(2).AddMinutes(10), 1995.8, 1997, 1995, 1996.5);  // prior c3 low 1995 → gap 1990.5–1995, never touched before 08:00
        Flat(day.AddHours(2).AddMinutes(11), day.AddHours(8), 1998.5);
        Five(day.AddHours(8).AddMinutes(5), 1999, 1999, 1994, 1995.5);   // opening candle dips to 1994 → touches the prior gap top 1995
        Five(day.AddHours(8).AddMinutes(10), 1995.5, 1996, 1994.5, 1995.8); // c1 (high 1996)
        Five(day.AddHours(8).AddMinutes(15), 1995.8, 2002, 1995.8, 2001.8); // c2
        Five(day.AddHours(8).AddMinutes(20), 2001.8, 2003, 1998, 2002.5);   // c3 low 1998 → FIRST FVG 1996–1998
        Five(day.AddHours(8).AddMinutes(25), 2002.5, 2004, 2002, 2003.8);
        Five(day.AddHours(8).AddMinutes(30), 2003.8, 2004, 1997.5, 1999);   // red, back into the gap: touch 1998, 25% 1997.5 (50% = 1997 not reached)
        Five(day.AddHours(8).AddMinutes(35), 1999, 2001.5, 1998.8, 2001);   // green → reference high 2001.5
        Five(day.AddHours(8).AddMinutes(40), 2001, 2003, 2000.5, 2002.8);   // breaks 2001.5 → entry
        double p = 2002.8; for (var t = day.AddHours(8).AddMinutes(41); t <= day.AddHours(16); t = t.AddMinutes(1)) { double o = p; p += 0.017; Min(t, o, p + 0.1, o - 0.1, p); }
        var five = KeystoneFvgEntryStudy.FiveMinute(bars);
        var c3 = five.First(b => b.Time == day.AddHours(8).AddMinutes(20));
        Check(Eq(c3.Low, 1998) && Eq(c3.High, 2003) && Eq(c3.Open, 2001.8) && Eq(c3.Close, 2002.5), "1-minute bars build the 5-minute candle 08:15-08:20 correctly");
        var sets = KeystoneFvgEntryStudy.Run(bars, new KeystoneFvgStudyConfig());
        Func<string, KeystoneArcEvent> one = s => sets[s].SingleOrDefault();
        var touch = one(KeystoneFvgEntryStudy.Touch); var d25 = one(KeystoneFvgEntryStudy.Dip25); var brk = one(KeystoneFvgEntryStudy.Break); var pt = one(KeystoneFvgEntryStudy.PriorTouch);
        Check(touch != null && Eq(touch.Entry, 1998) && touch.EntryTime == day.AddHours(8).AddMinutes(26) && Eq(touch.FvgLower, 1996) && Eq(touch.FvgUpper, 1998), "TOUCH: first FVG 1996–1998 after 08:00, filled at the top 1998 at 08:26", touch == null ? "none" : touch.Entry + " " + touch.EntryTime.ToString("HH:mm"));
        Check(d25 != null && Eq(d25.Entry, 1997.5), "25% DIP: filled at 1997.5", d25 == null ? "none" : d25.Entry.ToString());
        Check(sets[KeystoneFvgEntryStudy.Dip50].Count == 0, "50% DIP: the middle 1997 was never reached → no entry that day");
        Check(brk != null && Eq(brk.Entry, 2001.5) && brk.EntryTime == day.AddHours(8).AddMinutes(37), "GREEN CLOSE + BREAK: red touch, next candle green (high 2001.5), next candle breaks it → 2001.5 at 08:37", brk == null ? "none" : brk.Entry + " " + brk.EntryTime.ToString("HH:mm"));
        Check(pt != null && Eq(pt.Entry, 1995) && pt.EntryTime == day.AddHours(8).AddMinutes(1) && Eq(pt.FvgUpper, 1995), "PRIOR FVG: the overnight gap 1990.5–1995 untouched at 08:00; the opening candle touches it → 1995 at 08:01", pt == null ? "none" : pt.Entry + " " + pt.EntryTime.ToString("HH:mm"));
        Check(sets[KeystoneFvgEntryStudy.Prior50].Count == 0, "PRIOR 50%: 1992.75 never reached in the first 30 minutes");
        Check(touch.MoveHeat != null && touch.MoveMfe > 5 && Eq(touch.MoveMae, 0.5) && touch.MoveClose > 5, "measured to the close: for us > +5, against −0.5 (the 1997.5 low), closed up", touch.MoveMfe + " " + touch.MoveMae + " " + touch.MoveClose);
        Check(sets.Where(kv => kv.Key != KeystoneFvgEntryStudy.FirstBh).SelectMany(kv => kv.Value).All(e => e.SetupClass != "BH"), "no BH in the FVG sets");
        var bh = sets[KeystoneFvgEntryStudy.FirstBh].SingleOrDefault();
        Check(bh != null && Eq(bh.Entry, 1996) && bh.EntryTime == day.AddHours(8).AddMinutes(12), "FIRST 5M BH (own set, for comparison): red 08:05 → green 08:10 (high 1996) → broken at 08:12", bh == null ? "none" : bh.Entry + " " + bh.EntryTime.ToString("HH:mm"));
        var groups = KeystoneMoveStudy.Run(sets, KeystoneMoveStudy.BaselineRows(bars, "MGC", 800, 1555));
        Check(groups.Any(g => g.Set == KeystoneFvgEntryStudy.Break) && groups.Any(g => g.Set == KeystoneMoveStudy.Baseline), "the sets go straight into the MOVE STUDY ranking", string.Join(" | ", groups.Select(g => g.Name)));
        Console.WriteLine(failures == 0 ? "ALL FVG ENTRY STUDY TESTS PASSED" : failures + " FVG ENTRY STUDY TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
