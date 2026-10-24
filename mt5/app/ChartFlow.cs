// SpasiX — the chart's MARKET SESSIONS (each exchange's official hours in its own time zone, so summer / winter time is always right:
// a coloured background, its open line, its close line, its key moments), VOLUME, the VOLUME PROFILE (POC, value area), the ORDER FLOW
// ESTIMATE (delta, cumulative delta) and BULLS vs BEARS. MT5 CFDs have no exchange order book: buyers / sellers are ESTIMATED from the
// tick volume and where each candle closed inside its range — said on the chart.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed partial class ChartView
    {
        // one market: its time zone, its main hours (one or more pieces), its light "before the open" hours, its key moments (local time)
        sealed class MarketDef
        {
            public string Key, Name, Tz, Color; public double[][] Main, Light; public Tuple<double, string>[] Marks; public bool Dark; public string LightName = "";
        }
        static double Hm(int h, int m) { return h + m / 60.0; }
        static readonly MarketDef[] Markets = {
            new MarketDef { Key = "SESS_SYD", Name = "SYDNEY", Tz = "AUS Eastern Standard Time", Color = "#26C6DA", Main = new[] { new[] { 10.0, 16.0 } }, Light = new[] { new[] { 7.0, 10.0 } }, LightName = "pre-open",
                Marks = new[] { Tuple.Create(10.0, "ASX OPEN"), Tuple.Create(16.0, "ASX CLOSE") } },
            new MarketDef { Key = "SESS_TKY", Name = "TOKYO", Tz = "Tokyo Standard Time", Color = "#A98BFF", Main = new[] { new[] { 9.0, 11.5 }, new[] { 12.5, 15.5 } }, Light = new[] { new[] { 8.0, 9.0 } }, LightName = "orders",
                Marks = new[] { Tuple.Create(Hm(8, 50), "JP DATA"), Tuple.Create(9.0, "TSE OPEN"), Tuple.Create(15.5, "TSE CLOSE") } },
            new MarketDef { Key = "SESS_CHN", Name = "SHANGHAI GOLD + HK", Tz = "China Standard Time", Color = "#FF5C7A", Main = new[] { new[] { 9.0, 15.5 } }, Light = new[] { new[] { 20.0, 26.5 } }, LightName = "SGE night",
                Marks = new[] { Tuple.Create(9.0, "SGE GOLD OPEN"), Tuple.Create(Hm(9, 15), "PBOC FIX"), Tuple.Create(9.5, "HK / CHINA STOCKS"), Tuple.Create(15.5, "SGE CLOSE") } },
            new MarketDef { Key = "SESS_LON", Name = "LONDON", Tz = "GMT Standard Time", Color = "#FFC94D", Main = new[] { new[] { 8.0, 16.5 } }, Light = new[] { new[] { 7.0, 8.0 } }, LightName = "Frankfurt pre",
                Marks = new[] { Tuple.Create(7.0, "FRANKFURT"), Tuple.Create(8.0, "LONDON OPEN"), Tuple.Create(10.5, "LBMA GOLD AM"), Tuple.Create(15.0, "LBMA GOLD PM"), Tuple.Create(16.5, "LSE CLOSE") } },
            new MarketDef { Key = "SESS_NY", Name = "NEW YORK", Tz = "Eastern Standard Time", Color = "#2EE59D", Main = new[] { new[] { 9.5, 16.0 } }, Light = new[] { new[] { 8.0, 9.5 } }, LightName = "COMEX + data",
                Marks = new[] { Tuple.Create(4.0, "US PRE-MARKET"), Tuple.Create(Hm(8, 20), "COMEX GOLD"), Tuple.Create(8.5, "US DATA"), Tuple.Create(9.5, "NYSE OPEN"), Tuple.Create(10.0, "10:00 DATA"), Tuple.Create(16.0, "NYSE CLOSE") } },
            new MarketDef { Key = "SESS_CME", Name = "CME BREAK", Tz = "Eastern Standard Time", Color = "#90A4AE", Main = new[] { new[] { 17.0, 18.0 } }, Light = new double[0][], Dark = true,
                Marks = new[] { Tuple.Create(18.0, "GLOBEX OPEN") } },
        };
        static readonly Dictionary<string, TimeZoneInfo> tzCache = new Dictionary<string, TimeZoneInfo>();
        static TimeZoneInfo Zone(string id) { TimeZoneInfo z; if (tzCache.TryGetValue(id, out z)) return z; try { z = TimeZoneInfo.FindSystemTimeZoneById(id); } catch { z = null; } tzCache[id] = z; return z; }
        // a local exchange time → the chart's server time
        DateTime ToServer(TimeZoneInfo tz, DateTime localDay, double hours)
        {
            var local = DateTime.SpecifyKind(localDay.Date.AddHours(hours), DateTimeKind.Unspecified);
            DateTime utc; try { if (tz.IsInvalidTime(local)) local = local.AddHours(1); utc = TimeZoneInfo.ConvertTimeToUtc(local, tz); } catch { utc = local; }
            return utc.AddSeconds(ServerOffsetSec);
        }

        void DrawMarketSessions(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            var on = Markets.Where(m => Ind.Contains(m.Key)).ToList(); if (on.Count == 0 || bars.Count == 0) return; double lh = LegendH;
            DateTime t0 = bars[Math.Max(0, i0)].Time.AddDays(-1), t1 = bars[Math.Min(bars.Count - 1, i1)].Time.AddDays(1); int row = 0;
            foreach (var m in on)
            {
                var tz = Zone(m.Tz); if (tz == null) continue; var col = Hex(m.Color); double ly = TopPad + 34 + lh + row * 15; row++;
                for (var d = t0.Date.AddDays(-1); d <= t1.Date; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                    if (m.Key == "SESS_CME" && d.DayOfWeek == DayOfWeek.Friday) continue;   // no Friday reopen
                    foreach (var seg in m.Light) Band(dc, ToServer(tz, d, seg[0]), ToServer(tz, d, seg[1]), m, col, true, ly, m.LightName);
                    for (int k = 0; k < m.Main.Length; k++) Band(dc, ToServer(tz, d, m.Main[k][0]), ToServer(tz, d, m.Main[k][1]), m, col, false, ly, k == 0 ? m.Name : "");
                    foreach (var mk in m.Marks) Mark(dc, ToServer(tz, d, mk.Item1), mk.Item2, col);
                }
            }
        }
        void Band(DrawingContext dc, DateTime s0, DateTime s1, MarketDef m, SolidColorBrush col, bool light, double labelY, string label)
        {
            double x1 = X(IndexOf(s0)) - barW / 2, x2 = X(IndexOf(s1)) - barW / 2; if (x2 < 0 || x1 > plotW || x2 - x1 < 1) return;
            var r = new Rect(Math.Max(-2, x1), TopPad, Math.Min(plotW + 2, x2) - Math.Max(-2, x1), plotH);
            if (m.Dark) dc.DrawRectangle(Br(Colors.Black, 70), null, r);
            else dc.DrawRectangle(Br(col.Color, light ? (byte)9 : (byte)20), null, r);
            if (x1 >= 0) dc.DrawLine(light ? P(Br(col.Color, 90), 1, 2, 3) : P(Br(col.Color, 170), 1.3), new Point(x1, TopPad), new Point(x1, TopPad + plotH));   // the open
            if (x2 <= plotW) dc.DrawLine(P(Br(col.Color, light ? (byte)60 : (byte)120), 1, 5, 4), new Point(x2, TopPad), new Point(x2, TopPad + plotH));        // the close
            if (label != "" && x2 - x1 > 30)
            {
                var t = T((light ? "" : "● ") + label, 9.5, Br(col.Color, light ? (byte)170 : (byte)245), false, true); double lx = Math.Max(2, x1 + 4); if (lx + t.Width > x2 - 2 && !light) lx = Math.Max(2, x2 - t.Width - 4);
                dc.DrawRoundedRectangle(Br(Theme.Plot, 200), P(Br(col.Color, light ? (byte)70 : (byte)150), 1), new Rect(lx - 3, labelY - 1, t.Width + 6, 14), 4, 4); dc.DrawText(t, new Point(lx, labelY));
            }
        }
        void Mark(DrawingContext dc, DateTime at, string text, SolidColorBrush col)
        {
            double x = X(IndexOf(at)) - barW / 2; if (x < 0 || x > plotW) return;
            dc.DrawLine(P(Br(col.Color, 55), 1, 1, 3), new Point(x, TopPad), new Point(x, TopPad + plotH));
            if (barW * 6 < 30 && Series.Minutes >= 30) return;   // too squeezed for the words
            var t = T(text, 8.5, Br(col.Color, 210), false, true); double y = TopPad + plotH - ((Ind.Contains("VOL") || Ind.Contains("DELTA")) ? plotH * 0.2 + 8 : 16);
            dc.PushTransform(new RotateTransform(-90, x + 3, y)); dc.DrawText(t, new Point(x + 3, y)); dc.Pop();
        }

        // ---- DAILY / WEEKLY OPENING GAPS: the open of a new day (week) away from the last close — a zone until the price comes back (filled)
        void DrawGaps(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            if (Series.Minutes >= 1440 || bars.Count < 3) return; int from = Math.Max(1, i0 - 2000), z = Math.Min(bars.Count - 1, i1);
            for (int i = from; i <= z; i++)
            {
                var b = bars[i]; var pb = bars[i - 1]; if (b.Time.Date == pb.Time.Date) continue;
                bool week = (b.Time - pb.Time).TotalHours > 30 || b.Time.DayOfWeek < pb.Time.DayOfWeek; double gap = b.O - pb.C; if (Math.Abs(gap) < Point * 3) continue;
                int fill = -1; for (int k = i; k < bars.Count; k++) { if (gap > 0 ? bars[k].L <= pb.C : bars[k].H >= pb.C) { fill = k; break; } if (k - i > 5000) break; }
                double x1 = X(i) - barW / 2, x2 = fill >= 0 ? X(fill) : plotW; if (x2 < 0 || x1 > plotW) continue;
                var col = gap > 0 ? BuyC : SellC; double y1 = Y(Math.Max(b.O, pb.C)), y2 = Y(Math.Min(b.O, pb.C));
                dc.DrawRectangle(Br(col, week ? (byte)46 : (byte)26), P(Br(col, week ? (byte)230 : (byte)150), week ? 1.6 : 1, week ? new double[0] : new[] { 4.0, 3 }), new Rect(x1, y1, Math.Max(2, x2 - x1), Math.Max(1, y2 - y1)));
                double pts = Math.Abs(gap) / Point;
                var t = T((week ? "WEEK GAP " : "DAY GAP ") + (gap > 0 ? "▲ " : "▼ ") + F(Math.Abs(gap)) + " (" + pts.ToString("N0", IC) + " pts) " + (fill >= 0 ? "• filled " + ToView(bars[fill].Time).ToString("ddd HH:mm", IC) : "• OPEN"), 9.5, Br(col), false, true);
                double lx = Math.Max(2, Math.Min(plotW - t.Width - 6, x1 + 3)), ly = gap > 0 ? y1 - 15 : y2 + 2;
                dc.DrawRoundedRectangle(Br(Theme.Plot, 220), null, new Rect(lx - 3, ly - 1, t.Width + 6, 14), 3, 3); dc.DrawText(t, new Point(lx, ly));
            }
        }
        // the gap statistics of a series of day (or week) candles: how many up / down, the sizes, how many filled in that same candle
        public static string GapStats(List<Mt5Bar> d, double point, int digits, string what)
        {
            var ups = new List<double>(); var dns = new List<double>(); int upF = 0, dnF = 0; var last = new List<string>(); var fmt = "F" + digits;
            for (int i = 1; i < d.Count; i++)
            {
                double g = d[i].O - d[i - 1].C; if (Math.Abs(g) < point * 3) continue; bool filled = g > 0 ? d[i].L <= d[i - 1].C : d[i].H >= d[i - 1].C;
                if (g > 0) { ups.Add(g); if (filled) upF++; } else { dns.Add(-g); if (filled) dnF++; }
                last.Add(d[i].Time.ToString("ddd dd MMM yyyy", IC) + "   " + (g > 0 ? "▲ up   " : "▼ down ") + Math.Abs(g).ToString(fmt, IC).PadLeft(10) + "  (" + (Math.Abs(g) / point).ToString("N0", IC) + " pts)   " + (filled ? "filled" : "NOT filled"));
            }
            int n = ups.Count + dns.Count; if (n == 0) return what + ": no gap in the " + d.Count + " candles loaded";
            Func<List<double>, string> st = l => l.Count == 0 ? "—" : "avg " + l.Average().ToString(fmt, IC) + " • median " + l.OrderBy(x => x).ElementAt(l.Count / 2).ToString(fmt, IC) + " • max " + l.Max().ToString(fmt, IC);
            return what + " — " + d.Count + " candles (" + d.First().Time.ToString("dd MMM yyyy", IC) + " → " + d.Last().Time.ToString("dd MMM yyyy", IC) + ")\n"
                + "  gaps: " + n + "   ▲ up " + ups.Count + " (" + (100.0 * ups.Count / n).ToString("0", IC) + " %)   ▼ down " + dns.Count + " (" + (100.0 * dns.Count / n).ToString("0", IC) + " %)\n"
                + "  ▲ up:   " + st(ups) + " • filled the same " + (what.StartsWith("WEEK") ? "week" : "day") + " " + (ups.Count > 0 ? (100.0 * upF / ups.Count).ToString("0", IC) : "—") + " %\n"
                + "  ▼ down: " + st(dns) + " • filled the same " + (what.StartsWith("WEEK") ? "week" : "day") + " " + (dns.Count > 0 ? (100.0 * dnF / dns.Count).ToString("0", IC) : "—") + " %\n"
                + "  the last ones:\n    " + string.Join("\n    ", Enumerable.Reverse(last).Take(8));
        }

        // ---- for the ANALYSIS tab: the value area of candles a..z ({POC, VAH, VAL}, null = no volume), a candle's buyers share, the sessions now
        public static double BuyOf(Mt5Bar b) { return BuyShare(b); }
        public static double[] ValueArea(List<Mt5Bar> bars, int a, int z)
        {
            a = Math.Max(0, a); z = Math.Min(bars.Count - 1, z); if (z < a) return null; double lo2 = double.MaxValue, hi2 = double.MinValue;
            for (int i = a; i <= z; i++) { lo2 = Math.Min(lo2, bars[i].L); hi2 = Math.Max(hi2, bars[i].H); }
            const int N = 60; var v = new double[N]; double step = (hi2 - lo2) / N; if (step <= 0) return null;
            for (int i = a; i <= z; i++) { var b = bars[i]; int x = Math.Max(0, (int)((b.L - lo2) / step)), y = Math.Min(N - 1, (int)((b.H - lo2) / step)); if (y < x) continue; double per = Math.Max(1, b.V) / (double)(y - x + 1); for (int k = x; k <= y; k++) v[k] += per; }
            double total = v.Sum(); if (total <= 0) return null; int poc = 0; for (int k = 1; k < N; k++) if (v[k] > v[poc]) poc = k;
            int va0 = poc, va1 = poc; double inVa = v[poc];
            while (inVa < total * 0.7 && (va0 > 0 || va1 < N - 1)) { double dn = va0 > 0 ? v[va0 - 1] : -1, up = va1 < N - 1 ? v[va1 + 1] : -1; if (up >= dn) { va1++; inVa += up; } else { va0--; inVa += dn; } }
            return new[] { lo2 + (poc + 0.5) * step, lo2 + (va1 + 1) * step, lo2 + va0 * step };
        }
        // every market: open now (true) / its pre-open now (false) / closed (null), with how long until the next change
        public static List<Tuple<string, string, bool?, TimeSpan>> SessionsNow(DateTime utcNow)
        {
            var res = new List<Tuple<string, string, bool?, TimeSpan>>();
            foreach (var m in Markets)
            {
                var tz = Zone(m.Tz); if (tz == null) continue; DateTime localNow; try { localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz); } catch { continue; }
                bool? state = null; TimeSpan left = TimeSpan.MaxValue;
                for (var d = localNow.Date.AddDays(-1); d <= localNow.Date.AddDays(3); d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || (m.Key == "SESS_CME" && d.DayOfWeek == DayOfWeek.Friday)) continue;
                    foreach (var pair in m.Main.Select(x => Tuple.Create(x, true)).Concat(m.Light.Select(x => Tuple.Create(x, false))))
                    {
                        DateTime s0 = d.AddHours(pair.Item1[0]), s1 = d.AddHours(pair.Item1[1]);
                        if (localNow >= s0 && localNow < s1) { if (state != true) { state = pair.Item2; left = s1 - localNow; } }
                        else if (s0 > localNow && state == null && s0 - localNow < left) left = s0 - localNow;
                    }
                }
                res.Add(Tuple.Create(m.Name, m.Color, m.Dark ? (state == true ? (bool?)true : null) : state, left));
            }
            return res;
        }

        // ---- buyers / sellers of a candle (estimated): where it closed in its range + its direction, times its tick volume
        static double BuyShare(Mt5Bar b)
        {
            double r = b.H - b.L; if (r <= 0) return 0.5;
            double pos = (b.C - b.L) / r, body = (b.C - b.O) / r;
            return Math.Max(0, Math.Min(1, 0.5 * pos + 0.5 * (0.5 + 0.5 * body)));
        }

        // ---- the VOLUME PROFILE of the visible candles (right side): POC, value area 70 %
        void DrawVolumeProfile(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            const int N = 56; var buy = new double[N]; var sell = new double[N]; double step = (hi - lo) / N; if (step <= 0) return;
            for (int i = Math.Max(0, i0); i <= Math.Min(bars.Count - 1, i1); i++)
            {
                var b = bars[i]; if (b.V <= 0) continue; double bs = BuyShare(b); int a = Math.Max(0, (int)((b.L - lo) / step)), z = Math.Min(N - 1, (int)((b.H - lo) / step)); if (z < a) continue;
                double per = (double)b.V / (z - a + 1); for (int k = a; k <= z; k++) { buy[k] += per * bs; sell[k] += per * (1 - bs); }
            }
            double total = 0, max = 0; int poc = 0; for (int k = 0; k < N; k++) { double v = buy[k] + sell[k]; total += v; if (v > max) { max = v; poc = k; } }
            if (total <= 0) return;
            int va0 = poc, va1 = poc; double inVa = buy[poc] + sell[poc];
            while (inVa < total * 0.7 && (va0 > 0 || va1 < N - 1)) { double dn = va0 > 0 ? buy[va0 - 1] + sell[va0 - 1] : -1, up = va1 < N - 1 ? buy[va1 + 1] + sell[va1 + 1] : -1; if (up >= dn) { va1++; inVa += up; } else { va0--; inVa += dn; } }
            double wMax = plotW * 0.16;
            for (int k = 0; k < N; k++)
            {
                double v = buy[k] + sell[k]; if (v <= 0) continue; double w = wMax * v / max, y1 = Y(lo + (k + 1) * step), h = Math.Max(1, Y(lo + k * step) - y1 - 1); bool va = k >= va0 && k <= va1;
                double wb = w * buy[k] / v; byte al = va ? (byte)85 : (byte)38;
                dc.DrawRectangle(Br(BuyC, al), null, new Rect(plotW - w, y1, wb, h)); dc.DrawRectangle(Br(SellC, al), null, new Rect(plotW - w + wb, y1, w - wb, h));
                if (k == poc) dc.DrawRectangle(null, P(GoldB, 1.2), new Rect(plotW - w, y1, w, h));
            }
            Action<double, string, Brush, bool> lvl = (price, name, br, solid) => { double y = Y(price); dc.DrawLine(solid ? P(br, 1.3, 6, 3) : P(br, 1, 2, 3), new Point(0, y), new Point(plotW, y)); var t = T(name + " " + F(price), 9.5, br, true); dc.DrawRoundedRectangle(Br(Theme.Plot, 220), null, new Rect(plotW - wMax - t.Width - 14, y - 8, t.Width + 8, 15), 3, 3); dc.DrawText(t, new Point(plotW - wMax - t.Width - 10, y - 7)); };
            lvl(lo + (poc + 0.5) * step, "POC", GoldB, true); lvl(lo + (va1 + 1) * step, "VAH", CyanB, false); lvl(lo + va0 * step, "VAL", CyanB, false);
        }

        // ---- VOLUME + ORDER FLOW (est.): the bottom of the chart — volume bars split buyers / sellers, the delta and the cumulative delta (CVD)
        void DrawFlow(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            bool vol = Ind.Contains("VOL"), del = Ind.Contains("DELTA"); double paneH = plotH * 0.2, bottom = TopPad + plotH - 2, top = bottom - paneH;
            int a = Math.Max(0, i0), z = Math.Min(bars.Count - 1, i1); if (z < a) return; double maxV = 1, maxD = 1;
            var cvd = new double[z - a + 1]; double run = 0;
            for (int i = a; i <= z; i++) { var b = bars[i]; maxV = Math.Max(maxV, b.V); double d = b.V * (2 * BuyShare(b) - 1); maxD = Math.Max(maxD, Math.Abs(d)); run += d; cvd[i - a] = run; }
            double bw = Math.Max(1, barW * 0.7);
            dc.DrawRectangle(Br(Theme.Plot, 120), null, new Rect(0, top - 2, plotW, paneH + 2)); dc.DrawLine(P(Br(Theme.Grid, 200), 1), new Point(0, top - 2), new Point(plotW, top - 2));
            if (vol)
            {
                for (int i = a; i <= z; i++)
                {
                    var b = bars[i]; double x = X(i); if (x < -barW || x > plotW + barW) continue; double h = (paneH - 4) * b.V / maxV, bs = BuyShare(b), hb = h * bs;
                    dc.DrawRectangle(Br(BuyC, 150), null, new Rect(x - bw / 2, bottom - hb, bw, Math.Max(0.5, hb))); dc.DrawRectangle(Br(SellC, 150), null, new Rect(x - bw / 2, bottom - h, bw, Math.Max(0.5, h - hb)));
                }
            }
            else if (del)
            {
                double mid = top + paneH / 2;
                for (int i = a; i <= z; i++) { var b = bars[i]; double x = X(i); if (x < -barW || x > plotW + barW) continue; double d = b.V * (2 * BuyShare(b) - 1), h = (paneH / 2 - 3) * Math.Abs(d) / maxD; dc.DrawRectangle(Br(d >= 0 ? BuyC : SellC, 160), null, new Rect(x - bw / 2, d >= 0 ? mid - h : mid, bw, Math.Max(0.5, h))); }
                dc.DrawLine(P(Br(Theme.Axis, 120), 1, 2, 3), new Point(0, mid), new Point(plotW, mid));
            }
            if (del)
            {
                double cMax = cvd.Max(), cMin = cvd.Min(); if (cMax - cMin < 1) cMax = cMin + 1; var g = new StreamGeometry();
                using (var ctx = g.Open()) { bool first = true; for (int i = a; i <= z; i++) { double x = X(i), y = bottom - 3 - (paneH - 8) * (cvd[i - a] - cMin) / (cMax - cMin); if (first) { ctx.BeginFigure(new Point(x, y), false, false); first = false; } else ctx.LineTo(new Point(x, y), true, false); } }
                g.Freeze(); double last = cvd[cvd.Length - 1]; dc.DrawGeometry(null, P(last >= 0 ? BuyB : SellB, 1.8), g);
                var t = T("CVD (est.) " + (last >= 0 ? "+" : "−") + Math.Abs(last).ToString("N0", IC) + "  •  delta = buyers − sellers, from tick volume", 9.5, last >= 0 ? BuyB : SellB, false, true); dc.DrawText(t, new Point(6, top + 1));
            }
            else { var t = T("VOLUME (MT5 ticks)  •  green = buyers, red = sellers (est.)", 9.5, Br(Theme.Axis), false, true); dc.DrawText(t, new Point(6, top + 1)); }
        }

        // ---- BULLS vs BEARS: the tug of war of the last 20 candles, and the candles where one side hit hard (or got absorbed)
        void DrawFight(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            int z = Math.Min(bars.Count - 1, i1), a = Math.Max(0, z - 19); if (z - a < 3) return; double b0 = 0, s0 = 0, b5 = 0, s5 = 0, avgV = 0;
            for (int i = a; i <= z; i++) { var b = bars[i]; double bs = BuyShare(b); b0 += b.V * bs; s0 += b.V * (1 - bs); avgV += b.V; if (i > z - 5) { b5 += b.V * bs; s5 += b.V * (1 - bs); } }
            avgV /= (z - a + 1); double tot = b0 + s0; if (tot <= 0) return; double bull = b0 / tot, bull5 = b5 + s5 > 0 ? b5 / (b5 + s5) : 0.5;
            // the markers: absorption (big volume, small candle) and aggressive candles (big volume, closed at an end)
            for (int i = Math.Max(Math.Max(0, i0), 20); i <= z; i++)
            {
                var b = bars[i]; double x = X(i); if (x < 0 || x > plotW) continue; double av = 0; for (int k = i - 20; k < i; k++) av += bars[k].V; av /= 20; if (av <= 0) continue;
                double r = b.H - b.L, body = Math.Abs(b.C - b.O), bs = BuyShare(b);
                if (b.V > av * 2 && r > 0 && body < r * 0.3) { dc.DrawText(T("◆", 10, GoldB, false, true), new Point(x - 4, Y(b.H) - 16)); }
                else if (b.V > av * 1.6 && bs > 0.78) dc.DrawText(T("▲", 9.5, BuyB, false, true), new Point(x - 4, Y(b.L) + 3));
                else if (b.V > av * 1.6 && bs < 0.22) dc.DrawText(T("▼", 9.5, SellB, false, true), new Point(x - 4, Y(b.H) - 15));
            }
            // the box (top right): the rope, the knot, who is winning
            double bw = 290, bh = 92, bx = plotW - bw - (Ind.Contains("VPROF") ? plotW * 0.16 + 8 : 10), by = TopPad + 34; if (bx < 10) bx = 10;
            bool bullWin = bull >= 0.55, bearWin = bull <= 0.45; var edge = bullWin ? BuyB : bearWin ? SellB : GoldB;
            dc.DrawRoundedRectangle(Br(Theme.Plot, 232), P(edge, 1.3), new Rect(bx, by, bw, bh), 10, 10);
            dc.DrawText(T("⚔ BULLS vs BEARS • last 20 candles", 10.5, Br(Theme.Text), false, true), new Point(bx + 10, by + 6));
            double rx = bx + 10, ry = by + 26, rw = bw - 20, rh = 14, knot = rx + rw * bull + Math.Sin(DateTime.Now.Millisecond / 1000.0 * 2 * Math.PI) * 1.5;
            dc.DrawRoundedRectangle(Br(BuyC, 200), null, new Rect(rx, ry, Math.Max(1, knot - rx), rh), 6, 6); dc.DrawRoundedRectangle(Br(SellC, 200), null, new Rect(knot, ry, Math.Max(1, rx + rw - knot), rh), 6, 6);
            dc.DrawEllipse(Br(Colors.White), P(Br(Theme.Plot), 2), new Point(knot, ry + rh / 2), 7, 7);
            dc.DrawText(T("BULLS " + (100 * bull).ToString("0", IC) + "%", 10, BuyB, true), new Point(rx, ry + rh + 3)); var bt = T((100 * (1 - bull)).ToString("0", IC) + "% BEARS", 10, SellB, true); dc.DrawText(bt, new Point(rx + rw - bt.Width, ry + rh + 3));
            string verdict = bullWin ? "bulls push" : bearWin ? "bears push" : "balanced"; string mom = bull5 > bull + 0.08 ? " • bulls gaining ↑" : bull5 < bull - 0.08 ? " • bears gaining ↓" : "";
            var vt = T(verdict.ToUpperInvariant() + mom, 11, edge, false, true); dc.DrawText(vt, new Point(bx + (bw - vt.Width) / 2, ry + rh + 19));
            dc.DrawText(T("est. from tick volume (no exchange order book on CFDs) • ◆ absorption ▲▼ aggressive", 8, Br(Theme.Axis)), new Point(bx + 10, by + bh - 13));
        }
    }
}
