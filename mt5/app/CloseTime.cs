// SpasiX 09p — the DAILY CLOSE (17:00 New York = the broker's rollover, gold's daily break 17:00–18:00 NY, Friday 17:00 = the weekly close
// until Sunday 18:00) and the two rules the user asked for (mt5/docs/iter23-study.md):
//   ⏰ NO NEW TRADES BEFORE THE CLOSE (AutoSettings.CloseGap / CloseGapMin, DEFENDER's default 45 min): no NEW iteration in the last N minutes
//      before 17:00 NY and until the reopen at 18:00 NY — the hedges / management / exits of an open basket go on as usual;
//   🌙 AT THE CLOSE, CLOSE ONLY IF ≥ BREAKEVEN (AutoSettings.CloseIfBe / CloseBeMin, OFF by default): N minutes before the close a basket whose
//      TOTAL (open + this iteration's closed legs) is ≥ +$0.50 closes; a basket in drawdown is NEVER closed by this rule (kept over the rollover).
// New York = the server's clock + AutoSettings.NyOffsetHours (IC Markets: −7 all year — the server follows New York's DST, so the offset is fixed;
// the same number every other New York rule of the 🛡 HEDGE uses). Pure: no UI, no broker.
using System;
using System.Globalization;

namespace KeystoneMT5
{
    public static class CloseClock
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const int CloseHourNy = 17, ReopenHourNy = 18;   // gold / FX: the daily close 17:00 NY, trading again at 18:00 NY
        public const double BeKeep = 0.5;                       // 🌙 "≥ breakeven" = a TOTAL of at least +$0.50 (the 🎯 guard's breakeven level)

        // the next daily close (New York time) at or after `ny` — Mon–Fri 17:00; on a weekend (or Sunday before the reopen) Monday's / the next weekday's
        public static DateTime NextCloseNy(DateTime ny)
        {
            var d = ny.Date; if (ny.TimeOfDay >= TimeSpan.FromHours(CloseHourNy)) d = d.AddDays(1);
            while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
            return d.AddHours(CloseHourNy);
        }
        // the reopen after a close: 18:00 NY the same day, Friday's close → Sunday 18:00 NY
        public static DateTime ReopenNy(DateTime closeNy) { return closeNy.DayOfWeek == DayOfWeek.Friday ? closeNy.Date.AddDays(2).AddHours(ReopenHourNy) : closeNy.Date.AddHours(ReopenHourNy); }
        // the close just passed (New York): the market is closed between it and its reopen
        static bool AfterClose(DateTime ny, out DateTime closeNy)
        {
            closeNy = DateTime.MinValue;
            for (int back = 0; back <= 3; back++)
            {   // the latest weekday 17:00 at or before ny
                var d = ny.Date.AddDays(-back); if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                var c = d.AddHours(CloseHourNy); if (c > ny) continue; closeNy = c; return ny < ReopenNy(c);
            }
            return false;
        }
        // ⏰ inside the no-new-trades window? (the last `min` minutes before a close, or the market closed until its reopen) — min ≤ 0 = off.
        // closeNy = the close it is about, minsLeft = minutes to it (≤ 0 = already past), reopenNy = when new iterations may start again
        public static bool InGap(DateTime server, int nyOff, int min, out DateTime closeNy, out double minsLeft, out DateTime reopenNy)
        {
            var ny = server.AddHours(nyOff); closeNy = reopenNy = DateTime.MinValue; minsLeft = 0; if (min <= 0) return false;
            DateTime past; if (AfterClose(ny, out past)) { closeNy = past; minsLeft = (past - ny).TotalMinutes; reopenNy = ReopenNy(past); return true; }
            var c = NextCloseNy(ny); minsLeft = (c - ny).TotalMinutes; closeNy = c; reopenNy = ReopenNy(c);
            return minsLeft <= min;
        }
        public static bool InGap(DateTime server, int nyOff, int min) { DateTime c, r; double m; return InGap(server, nyOff, min, out c, out m, out r); }
        // 🌙 inside the breakeven-close window: the last `min` minutes before a close (not after it)
        public static bool InBeWindow(DateTime server, int nyOff, int min, out double minsLeft)
        {
            var ny = server.AddHours(nyOff); minsLeft = 0; if (min <= 0) return false; DateTime past; if (AfterClose(ny, out past)) return false;
            var c = NextCloseNy(ny); minsLeft = (c - ny).TotalMinutes; return minsLeft > 0 && minsLeft <= min;
        }
        // 🌙 the rule itself: close only at ≥ breakeven, never a basket in drawdown
        public static bool BeCloseNow(double total) { return !double.IsNaN(total) && total >= BeKeep - 1e-9; }

        static string Hm(DateTime ny, DateTime now)
        {
            string t = ny.ToString("HH:mm", IC); if (ny.Date == now.Date) return t;
            return ny.ToString("ddd", IC) + " " + t;
        }
        static string Mins(double m) { m = Math.Ceiling(Math.Max(0, m)); return m >= 90 ? Math.Floor(m / 60) + " h " + (m % 60).ToString("00", IC) + " min" : m.ToString("0", IC) + " min"; }
        // the banner / SETUPS NOW line: "⏰ no new trades until 18:00 NY — the close in 32 min" ("" = not in the window, or the rule is off)
        public static string GapText(DateTime server, int nyOff, int min)
        {
            DateTime c, r; double m; if (server == DateTime.MinValue || !InGap(server, nyOff, min, out c, out m, out r)) return "";
            var ny = server.AddHours(nyOff);
            return "⏰ no new trades until " + Hm(r, ny) + " NY — " + (m > 0 ? "the close in " + Mins(m) : "the market closed at 17:00 NY" + (c.DayOfWeek == DayOfWeek.Friday ? " (Friday: the weekend)" : ""));
        }
        // the same, ahead of it (the next 3 hours): "⏰ no new trades from 16:15 NY (45 min before the 17:00 close)"
        public static string GapSoonText(DateTime server, int nyOff, int min)
        {
            if (server == DateTime.MinValue || min <= 0 || InGap(server, nyOff, min)) return "";
            var ny = server.AddHours(nyOff); var c = NextCloseNy(ny); var from = c.AddMinutes(-min); if ((from - ny).TotalHours > 3) return "";
            return "⏰ no new trades from " + Hm(from, ny) + " NY (" + min + " min before the 17:00 close)";
        }
        // the STORY / gate reason — its stable part comes BEFORE " (New York " so the story logs it once per close window
        public static string GateText(DateTime server, int nyOff, int min)
        {
            DateTime c, r; double m; if (!InGap(server, nyOff, min, out c, out m, out r)) return "";
            var ny = server.AddHours(nyOff);
            return "⏰ NO NEW TRADES BEFORE THE CLOSE — the last " + min + " min before the 17:00 New York close" + (c.DayOfWeek == DayOfWeek.Friday ? " (Friday: the weekly close)" : "") + "; entries are skipped until " + Hm(r, ny) + " NY, an open basket is managed as usual (New York " + ny.ToString("HH:mm", IC) + (m > 0 ? ", the close in " + Mins(m) : ", market closed") + ")";
        }
        // the 6-year evidence (mt5/docs/iter23-study.md § 5), for the tooltips — honest: neither rule is a profit edge
        public const string GapEvidence = "6 years of 1-minute gold (spread only + an ASSUMED swap, $8,000, AUTO RESTART): with 🛡 RISK GUARD −$9.9k / 2 blow-ups vs RISK GUARD alone −$4.7k / 2; alone −$136k / 16 vs as-is −$110k / 14 — inside the bench's noise: no proven benefit, no visible harm. It keeps fresh baskets from being born into the illiquid close and the rollover.";
        public const string BeEvidence = "6 years of 1-minute gold: with 🛡 RISK GUARD it was neutral (−$5.7k / 2 blow-ups vs −$4.7k / 2); ALONE it was HARMFUL (−$285k / 38 blow-ups vs −$110k / 14) — ending baskets early at ≈ $0 starts 3× more iterations, each a new ticket into the deep-basket tail. A basket in drawdown is never closed by it (kept over the rollover).";
    }
}
