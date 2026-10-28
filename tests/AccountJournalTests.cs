// 📋 the ACCOUNT LOG: orders + executions → every event line and the finished trades (target / stop / market, best / worst, partial targets)
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class AccountJournalTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    public static int Main()
    {
        var j = new KeystoneAccountJournal("TPT 1"); var t0 = new DateTime(2026, 10, 28, 10, 0, 0); const string I = "MNQ 12-26";
        Func<string, double> pv = s => 2.0; double last = double.NaN; Func<string, double> lp = s => last;
        var ords = new List<KeystoneJOrder>(); var ex = new List<KeystoneJExec>();
        // a BUY 5 at market, then a target limit 5 and a stop 5
        ex.Add(new KeystoneJExec { Id = "e1", Instrument = I, Time = t0, Price = 29800, SignedQty = 5, OrderKind = "MARKET", Commission = 2.5 });
        ords.Add(new KeystoneJOrder { Id = "o1", Instrument = I, Kind = "LIMIT", Side = -1, Qty = 5, Price = 29820, State = "Working", Name = "Target" });
        ords.Add(new KeystoneJOrder { Id = "o2", Instrument = I, Kind = "STOP", Side = -1, Qty = 5, Price = 29790, State = "Working", Name = "Stop" });
        j.Update(t0.AddSeconds(2), ords, ex, pv, lp);
        Check(j.OpenQty(I) == 5 && j.Events.Any(e => e.Item2.StartsWith("▶ TRADE OPENED LONG 5")) && j.Events.Count(e => e.Item2.StartsWith("ORDER PLACED")) == 2, "a fill opens LONG 5; the two working orders are logged as placed");
        last = 29815; j.Update(t0.AddSeconds(30), ords, ex, pv, lp);   // open +$150
        // the target fills only 3 of 5 (part filled), then the stop takes the other 2
        ords[0].Filled = 3; ords[0].State = "PartFilled"; ex.Add(new KeystoneJExec { Id = "e2", Instrument = I, Time = t0.AddMinutes(2), Price = 29820, SignedQty = -3, OrderKind = "LIMIT", OrderName = "Target", Commission = 1.5 });
        j.Update(t0.AddMinutes(2), ords, ex, pv, lp);
        Check(j.OpenQty(I) == 2 && j.Events.Any(e => e.Item2.StartsWith("◐ PART FILLED") && e.Item2.Contains("3 of 5")) && j.Events.Any(e => e.Item2.Contains("+$120") && e.Item2.Contains("LONG 2 left")), "the target part-fills 3 of 5: +$120, LONG 2 left");
        ords[1].Price = 29805; j.Update(t0.AddMinutes(3), ords, ex, pv, lp);
        Check(j.Events.Any(e => e.Item2.StartsWith("ORDER MOVED") && e.Item2.Contains("29790.00 → 29805.00")), "a moved stop is logged");
        ords[1].Filled = 2; ords[1].Qty = 2; ords[1].State = "Filled"; ords[0].State = "Cancelled"; ex.Add(new KeystoneJExec { Id = "e3", Instrument = I, Time = t0.AddMinutes(4), Price = 29805, SignedQty = -2, OrderKind = "STOP", OrderName = "Stop", Commission = 1 });
        j.Update(t0.AddMinutes(4), ords, ex, pv, lp);
        var tr = j.Finished.SingleOrDefault();
        Check(tr != null && tr.MaxQty == 5 && tr.ByTarget == 3 && tr.ByStop == 2 && Math.Abs(tr.Gross - 140) < 1e-6 && Math.Abs(tr.Fees - 5) < 1e-6 && tr.Best >= 150 && tr.Note.Contains("only 3 of 5"), "the trade: 5 contracts, 3 by target + 2 by stop, +$140 gross, $5 fees, best ≥ +$150, ⚠ target closed only 3 of 5", tr == null ? "none" : KeystoneAccountJournal.TradeLine(tr));
        Check(j.Events.Any(e => e.Item2.StartsWith("⊘ ORDER CANCELLED") && e.Item2.Contains("3 of 5 had filled")) && j.Events.Any(e => e.Item2.StartsWith("■ TRADE CLOSED")), "the leftover target is cancelled; the trade closed line");
        var back = KeystoneAccountJournal.ParseTrade(KeystoneAccountJournal.TradeLine(tr));
        Check(back != null && back.Key == tr.Key && back.ByTarget == 3 && back.Gross == tr.Gross && back.Note == tr.Note.Replace(",", ";"), "the trade line reads back");
        // the same executions again (a restart) are not counted twice
        j.Update(t0.AddMinutes(5), ords, ex, pv, lp); Check(j.Finished.Count == 1, "seen executions are not counted again");
        // a reversal: SHORT 2 then BUY 5 = close 2 + LONG 3
        var j2 = new KeystoneAccountJournal("A"); var ex2 = new List<KeystoneJExec> { new KeystoneJExec { Id = "a", Instrument = I, Time = t0, Price = 100, SignedQty = -2, OrderKind = "MARKET" }, new KeystoneJExec { Id = "b", Instrument = I, Time = t0.AddMinutes(1), Price = 90, SignedQty = 5, OrderKind = "MARKET" } };
        j2.Update(t0.AddMinutes(1), new List<KeystoneJOrder>(), ex2, pv, lp);
        Check(j2.Finished.Count == 1 && Math.Abs(j2.Finished[0].Gross - 40) < 1e-6 && j2.OpenQty(I) == 3, "a reversal closes the short (+$40) and opens LONG 3");
        Console.WriteLine(failures == 0 ? "ALL ACCOUNT LOG TESTS PASSED" : failures + " ACCOUNT LOG TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
