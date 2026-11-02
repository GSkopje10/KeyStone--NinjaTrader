// ⚖ DRAWDOWN LAB: trailing vs end-of-day drawdown on the same trades
using System; using System.Linq; using NinjaTrader.NinjaScript;
public static class DrawdownLabTests
{
    static int fails;
    static void Check(bool ok, string name, string d = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + d)); if (!ok) fails++; }
    public static int Main()
    {
        var t = new DateTime(2026, 11, 2, 9, 30, 0);
        Func<string, KeystoneDdAccount> mk = k => { var a = new KeystoneDdAccount { Kind = k, Symbol = "MGC", Start = 50000, MaxDd = 2000, Target = 3000 }; a.Reset(); return a; };
        var tr = mk("TRAILING"); var eod = mk("EOD");
        foreach (var a in new[] { tr, eod }) { a.Trade(t, 1, 2000); a.Mark(t.AddMinutes(1), 2050); a.Mark(t.AddMinutes(2), 2000); }
        Check(Math.Abs(tr.Buffer(2000) - 1500) < 0.01 && Math.Abs(tr.Floor - 48500) < 0.01, "trailing: +$500 then back to 0 → buffer $1,500 (the floor followed the peak)", tr.Buffer(2000) + " " + tr.Floor);
        Check(Math.Abs(eod.Buffer(2000) - 2000) < 0.01 && Math.Abs(eod.Floor - 48000) < 0.01, "EOD: the same trade → buffer still $2,000 during the day", eod.Buffer(2000) + "");
        eod.Mark(t.AddMinutes(3), 2050); eod.EndOfDay(t.AddHours(7), 2050);
        Check(Math.Abs(eod.Floor - 48500) < 0.01, "EOD: closing the day at +$500 → the floor rises to $48,500", eod.Floor + "");
        var lk = new KeystoneDdAccount { Kind = "TRAILING", Symbol = "MNQ", Start = 50000, MaxDd = 2000, LockAt = 50000 }; lk.Reset(); lk.Trade(t, 2, 20000); lk.Mark(t, 20600);
        Check(Math.Abs(lk.Floor - 50000) < 0.01 && Math.Abs(lk.PointValue - 2) < 1e-9, "lock: the trailing floor stops at the start balance (MNQ $2 a point)", lk.Floor + "");
        var b = mk("TRAILING"); b.Trade(t, -1, 2000); b.Mark(t.AddMinutes(1), 2201);
        Check(b.Blown && b.Pos == 0 && b.Realized < -2000, "a short against a $201 move breaches the $2,000 floor → blown, flat", b.Blown + " " + b.Realized);
        var tp = mk("EOD"); tp.TakeProfit = 300; tp.Trade(t, 1, 2000); tp.Mark(t.AddMinutes(1), 2031);
        Check(tp.Pos == 0 && Math.Abs(tp.Realized - 310) < 0.01, "take profit $300: closed at the first price past +$300", tp.Realized + "");
        var rv = mk("EOD"); rv.Trade(t, 2, 2000); rv.Trade(t, -3, 2010);
        Check(rv.Pos == -1 && Math.Abs(rv.Realized - 200) < 0.01 && Math.Abs(rv.Avg - 2010) < 1e-9, "reverse: long 2, sell 3 → +$200 realized, short 1 @ 2010", rv.Pos + " " + rv.Realized);
        Console.WriteLine(fails == 0 ? "ALL DRAWDOWN LAB TESTS PASSED" : fails + " DRAWDOWN LAB TEST(S) FAILED"); return fails;
    }
}
