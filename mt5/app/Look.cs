// SpasiX — 🍂 SEASONS LOOK (13s, the same look as KeyStone 13k): seasonal ACCENTS only (the title band, its glow line, an optional drawn scene and a light
// animation in the band, OFF by default) — never the P/L green / red, never the candles; ONE header line: an urgent SpasiX reminder first (the 17:00 NY
// close / no new trades, a basket in drawdown, a withdrawal near, the day's P/L), else a season quote every 3 hours. Display only: every layer is
// IsHitTestVisible = false and nothing here sends anything.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class SxPalette { public string Season = "", Accent = "#4DD8FF", Accent2 = "#A98BFF", Glow = "#7DF9FF", Sky1 = "#0A0F1E", Sky2 = "#1A2340", Ink = "#96A5C3"; }
    public static class SxSeasons
    {
        public static readonly string[] Names = { "FALL", "WINTER", "SPRING", "SUMMER" };
        // northern hemisphere, astronomical starts on the NY calendar date: spring Mar 20, summer Jun 21, fall Sep 22, winter Dec 21 (as KeyStone)
        public static string Of(DateTime nyDate)
        {
            int md = nyDate.Month * 100 + nyDate.Day;
            if (md >= 1221 || md < 320) return "WINTER"; if (md < 621) return "SPRING"; if (md < 922) return "SUMMER"; return "FALL";
        }
        public static string Icon(string s) { return s == "FALL" ? "🍂" : s == "WINTER" ? "❄" : s == "SPRING" ? "🌸" : s == "SUMMER" ? "☀" : "◌"; }
        // KeyStone 13k's palettes: Fall amber / copper / plum, Winter ice / silver / navy, Spring mint / pink / teal, Summer gold / coral / ocean teal
        public static SxPalette Palette(string s)
        {
            switch (s)
            {
                case "FALL": return new SxPalette { Season = s, Accent = "#E8A33D", Accent2 = "#C8693A", Glow = "#F2B95A", Sky1 = "#3B1F33", Sky2 = "#9A5238", Ink = "#F3DCC0" };
                case "WINTER": return new SxPalette { Season = s, Accent = "#9FD8F0", Accent2 = "#C9D3DE", Glow = "#BFE9FF", Sky1 = "#0E1A33", Sky2 = "#2E4E78", Ink = "#DCEBF7" };
                case "SPRING": return new SxPalette { Season = s, Accent = "#8EE3C1", Accent2 = "#F2A7C3", Glow = "#FFD1E0", Sky1 = "#143A3A", Sky2 = "#3E8E8A", Ink = "#E4F6EE" };
                case "SUMMER": return new SxPalette { Season = s, Accent = "#F5C451", Accent2 = "#FF7F6B", Glow = "#FFE08A", Sky1 = "#0E3A4F", Sky2 = "#E07A5F", Ink = "#FFF1D6" };
                default: return new SxPalette();
            }
        }
        // the P/L colours the look may never use (red / green of the P/L boxes)
        public static readonly string[] Reserved = { "#2EE59D", "#FF4D6D" };
    }

    // the look's settings (⚙ SETUP ▸ 🍂 LOOK): SEASON AUTO / FALL / WINTER / SPRING / SUMMER / OFF, the drawn scene, the header line, the animation
    public sealed class SxLook
    {
        public string Season = "AUTO"; public bool Scene = true, Quotes = true, Anim = false;
        public string Save() { return string.Join(";", Season, Scene ? 1 : 0, Quotes ? 1 : 0, Anim ? 1 : 0); }
        public static SxLook Load(string s)
        {
            var l = new SxLook(); var a = (s ?? "").Split(';'); if (a.Length < 4) return l;
            l.Season = a[0] == "OFF" || SxSeasons.Names.Contains(a[0]) ? a[0] : "AUTO"; l.Scene = a[1] == "1"; l.Quotes = a[2] == "1"; l.Anim = a[3] == "1"; return l;
        }
        // the season shown now ("" = OFF)
        public string Resolve(DateTime nyDate) { return Season == "OFF" ? "" : Season == "AUTO" ? SxSeasons.Of(nyDate) : Season; }
    }

    // what the header line knows about this window (NaN / null = unknown)
    public sealed class SxLookCtx
    {
        public DateTime Server = DateTime.MinValue; public int NyOff = -7; public int CloseGapMin = 45; public bool Running;
        public double Total = double.NaN, Target = double.NaN, DayPl = double.NaN, Balance = double.NaN, WdLevel = double.NaN, WdStart = double.NaN; public bool Simulated;
    }

    public static class SxSeasonQuotes
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static readonly Dictionary<string, string[]> Q = new Dictionary<string, string[]>
        {
            { "FALL", new[] { "🍂 Let the leaves fall — a basket closed small is still a basket closed.", "🍂 Harvest what is ripe: the target, not the hope.", "🍂 Short days, long patience: the hedge buys time, not certainty." } },
            { "WINTER", new[] { "❄ Cold hands, calm head — the plan was made when it was warm.", "❄ Snow covers every path: wait for the break, not the guess.", "❄ Keep the account warm: risk small, sleep well." } },
            { "SPRING", new[] { "🌸 New growth comes slowly — so does an account that compounds.", "🌸 Rain then sun: a drawdown is weather, the rules are the season.", "🌸 Plant small lots, let the good days grow." } },
            { "SUMMER", new[] { "☀ Long days, same rules: the 17:00 close still comes.", "☀ The tide comes back — a hedge is a boat, not an anchor.", "☀ Take the target and go for a swim." } },
            { "", new[] { "Every basket ends — your rules decide how.", "Small and steady beats big and lucky." } },
        };
        public static string Quote(string season, DateTime t) { string[] q; if (!Q.TryGetValue(season ?? "", out q)) q = Q[""]; int slot = (int)((t - new DateTime(2020, 1, 1)).TotalHours / 3); return q[((slot % q.Length) + q.Length) % q.Length]; }
        public static string Usd(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString(Math.Abs(v) >= 1000 ? "N0" : "0.00", IC); }
        // the header line: urgent reminders first, else the season quote (changes every 3 h)
        public static string Line(SxLookCtx c, string season, DateTime now)
        {
            if (c != null && c.Server > DateTime.MinValue)
            {
                DateTime closeNy, reopenNy; double left;
                if (CloseClock.InGap(c.Server, c.NyOff, Math.Max(1, c.CloseGapMin), out closeNy, out left, out reopenNy))
                    return left > 0 ? "⏰ the 17:00 NY close in " + Math.Ceiling(left).ToString("0", IC) + " min — no new trades now; an open basket is still managed" : "⏰ the market is closed — no new trades until " + reopenNy.ToString("ddd HH:mm", IC) + " NY";
                if (!double.IsNaN(c.Total) && !double.IsNaN(c.Target) && c.Target > 0 && c.Total <= -3 * c.Target)
                    return "⚠ the basket is in drawdown: " + Usd(c.Total) + " (" + (c.Total / c.Target).ToString("0.0", IC).Replace("-", "−") + " × the target) — your rules and the 🎯 exit guard are watching";
                if (!double.IsNaN(c.WdLevel) && !double.IsNaN(c.Balance) && !double.IsNaN(c.WdStart) && c.WdLevel > c.WdStart)
                {
                    double p = (c.Balance - c.WdStart) / (c.WdLevel - c.WdStart);
                    if (p >= 1) return "💸 withdrawal due — " + Usd(c.Balance - c.WdStart).TrimStart('+') + " above the plan's start" + (c.Simulated ? " (🛡 HEDGE ▸ 💸 WITHDRAW, simulated)" : " — do it at your broker");
                    if (p >= 0.9) return "💸 the next withdrawal is near: " + (100 * p).ToString("0", IC) + " % of the way to $" + c.WdLevel.ToString("N0", IC);
                }
                if (!double.IsNaN(c.DayPl) && Math.Abs(c.DayPl) >= 0.005 && (now.Minute / 10) % 3 == 0) return "📅 today " + Usd(c.DayPl) + (c.DayPl < 0 ? " — a red day is a day, not the account" : " — well done, keep it");
            }
            return Quote(season, now);
        }
    }
}
