// Research harness — second wave: refined opening-range rules (ATR stops, opening-candle direction, width filters)
// and calendar rules (FOMC day, turn of the month, weekday).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NinjaTrader.NinjaScript;

public static partial class R
{
    public static HashSet<DateTime> Fomc = new HashSet<DateTime>(KeystoneNews.FomcDates.Select(s => DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
    public static bool IsNfp(DateTime d) { return KeystoneNews.NfpDate(d.Year, d.Month) == d.Date; }

    public static List<Variant> Variants2(string sym, List<RDay> days)
    {
        var v = new List<Variant>();
        var opens = sym == "MGC" ? new[] { S(8, 20), S(9, 30) } : new[] { S(9, 30) };
        // turn of the month: last trading day of a month + first 3 of the next
        var tom = new HashSet<DateTime>();
        for (int i = 0; i < days.Count; i++)
        {
            bool last = i + 1 < days.Count && days[i + 1].Day.Month != days[i].Day.Month;
            int first = 0; for (int j = i; j >= 0 && days[j].Day.Month == days[i].Day.Month; j--) first++;
            if (last || first <= 3) tom.Add(days[i].Day);
        }

        foreach (int o in opens)
        foreach (int n in new[] { 5, 10, 15, 30 })
        foreach (string stopMode in new[] { "OPP", "0.10", "0.15", "0.25" })
        foreach (double rr in new[] { 0, 2, 4 })
        foreach (string filt in new[] { "ALL", "NARROW", "NOTWIDE", "NONEWS" })
        foreach (int dir in new[] { 1, -1, 0 })
        foreach (string entry in new[] { "BREAK", "CANDLE" })
        {
            int oo = o, nn = n, dd = dir; string sm = stopMode, fl = filt, en = entry; double r2 = rr;
            v.Add(new Variant { Family = "ORB2", Sym = sym, Name = "ORB2 " + T(o) + " " + n + "m " + (entry == "BREAK" ? "break" : "opening-candle direction") + " " + D(dir) + " stop " + (sm == "OPP" ? "range" : sm + "×ATR") + " " + (rr > 0 ? rr + "R" : "to 15:55") + " days " + filt, Run = d =>
            {
                if (fl == "NONEWS" && (Fomc.Contains(d.Day) || IsNfp(d.Day))) return null;
                double hi = d.Hi(oo, oo + nn - 1), lo = d.Lo(oo, oo + nn - 1), op = d.OpenAt(oo), cl = d.CloseAt(oo + nn - 1);
                if (double.IsNaN(hi) || hi - lo < 4 * Spec.Of(d.Sym).Tick) return null;
                double w = hi - lo;
                if (fl == "NARROW" && w > 0.25 * d.RthAtr) return null;
                if (fl == "NOTWIDE" && w > 0.5 * d.RthAtr) return null;
                double stopD = sm == "OPP" ? w : double.Parse(sm, CultureInfo.InvariantCulture) * d.RthAtr;
                if (en == "CANDLE")
                {
                    int side = Math.Sign(cl - op); if (side == 0 || (dd != 0 && side != dd)) return null;
                    return Trade(d, side, "MKT", 0, oo + nn, oo + nn + 1, stopD, r2 > 0 ? r2 * stopD : 0, S(15, 55), "ORB2");
                }
                RTrade best = null;
                foreach (int side in dd == 0 ? new[] { 1, -1 } : new[] { dd })
                {
                    var t = Trade(d, side, "STOP", side > 0 ? hi : lo, oo + nn, oo + nn + 150, stopD, r2 > 0 ? r2 * stopD : 0, S(15, 55), "ORB2");
                    if (t != null && (best == null || t.InSlot < best.InSlot)) best = t;
                }
                return best;
            }});
        }

        // calendar
        foreach (int dir in new[] { 1, -1 })
        {
            int dd = dir;
            v.Add(new Variant { Family = "CAL", Sym = sym, Name = "FOMC DAY hold 18:00→13:59 " + D(dir), Run = d => Fomc.Contains(d.Day) ? Trade(d, dd, "MKT", 0, 0, 5, 0, 0, S(13, 58), "FOMC") : null });
            v.Add(new Variant { Family = "CAL", Sym = sym, Name = "FOMC DAY hold 09:30→13:59 " + D(dir), Run = d => Fomc.Contains(d.Day) ? Trade(d, dd, "MKT", 0, S(9, 30), S(9, 32), 0, 0, S(13, 58), "FOMC") : null });
            v.Add(new Variant { Family = "CAL", Sym = sym, Name = "TURN OF MONTH hold 09:30→15:55 " + D(dir), Run = d => tom.Contains(d.Day) ? Trade(d, dd, "MKT", 0, S(9, 30), S(9, 32), 0, 0, S(15, 55), "TOM") : null });
            v.Add(new Variant { Family = "CAL", Sym = sym, Name = "TURN OF MONTH hold 18:00→15:55 " + D(dir), Run = d => tom.Contains(d.Day) ? Trade(d, dd, "MKT", 0, 0, 5, 0, 0, S(15, 55), "TOM") : null });
            foreach (DayOfWeek wd in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            {
                var w2 = wd;
                v.Add(new Variant { Family = "CAL", Sym = sym, Name = wd + " hold 09:30→15:55 " + D(dir), Run = d => d.Day.DayOfWeek == w2 ? Trade(d, dd, "MKT", 0, S(9, 30), S(9, 32), 0, 0, S(15, 55), "DOW") : null });
                v.Add(new Variant { Family = "CAL", Sym = sym, Name = wd + " hold 18:00→09:29 " + D(dir), Run = d => d.Day.DayOfWeek == w2 ? Trade(d, dd, "MKT", 0, 0, 5, 0, 0, S(9, 28), "DOW") : null });
            }
        }
        return v;
    }
}
