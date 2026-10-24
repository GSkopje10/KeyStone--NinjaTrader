// SpasiX — the chart, drawn directly (smooth at any zoom): candles from MT5 in one of the THEMES, the time axis in the chosen TIME ZONE,
// the bid / ask with the candle's COUNTDOWN, INDICATORS (EMA 9 / 21 / 50 / 200, VWAP, fair-value gaps, NY opening range, yesterday's high / low),
// DRAWINGS (horizontal / vertical line, trend line, rectangle), YOUR TICKETS (every ticket its own line + label with ✕, S/L and T/P you drag,
// a drag from a ticket line sets its stop or target), PENDING ORDERS (drag = new price, ✕ = cancel), a NEWS banner and a crosshair.
// Mouse: wheel = zoom • drag the chart = move in time / up-down • drag the TIME axis = stretch / squeeze the candles • drag the PRICE axis =
// stretch the price • double-click = back to now, automatic • right-click = the menu (the host builds it).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed class ChartTheme
    {
        public string Name; public Color Bg, Plot, Grid, Up, Down, UpWick, DownWick, Axis, Text, Accent; public bool HollowUp, Glow;
        static Color C(string h) { return (Color)ColorConverter.ConvertFromString(h); }
        public static readonly ChartTheme[] All = {
            new ChartTheme { Name = "SpasiX Night", Bg = C("#0A0E16"), Plot = C("#0C111C"), Grid = C("#172031"), Up = C("#4DD8FF"), Down = C("#FF3D71"), UpWick = C("#4DD8FF"), DownWick = C("#FF3D71"), Axis = C("#8A94A8"), Text = C("#E8ECF4"), Accent = C("#FFC94D") },
            new ChartTheme { Name = "Neon Pulse", Bg = C("#07040F"), Plot = C("#0B0618"), Grid = C("#1C1233"), Up = C("#00F5D4"), Down = C("#F72585"), UpWick = C("#00F5D4"), DownWick = C("#F72585"), Axis = C("#9D8FC4"), Text = C("#F1EAFF"), Accent = C("#B5179E"), Glow = true },
            new ChartTheme { Name = "Classic Pro", Bg = C("#0D1117"), Plot = C("#0F141B"), Grid = C("#1B222C"), Up = C("#26A69A"), Down = C("#EF5350"), UpWick = C("#26A69A"), DownWick = C("#EF5350"), Axis = C("#8B949E"), Text = C("#E6EDF3"), Accent = C("#58A6FF") },
            new ChartTheme { Name = "Gold Rush", Bg = C("#0E0B06"), Plot = C("#120E07"), Grid = C("#251D10"), Up = C("#FFC94D"), Down = C("#9B5DE5"), UpWick = C("#FFC94D"), DownWick = C("#9B5DE5"), Axis = C("#B8A57A"), Text = C("#FFF3D6"), Accent = C("#FFC94D") },
            new ChartTheme { Name = "Ocean Deep", Bg = C("#03121C"), Plot = C("#041824"), Grid = C("#0B2A3B"), Up = C("#2EC4B6"), Down = C("#FF6B6B"), UpWick = C("#2EC4B6"), DownWick = C("#FF6B6B"), Axis = C("#7FA7BC"), Text = C("#E0F2FA"), Accent = C("#90E0EF") },
            new ChartTheme { Name = "Hollow Pro", Bg = C("#0A0D12"), Plot = C("#0B0F15"), Grid = C("#181E28"), Up = C("#00E676"), Down = C("#FF5252"), UpWick = C("#00E676"), DownWick = C("#FF5252"), Axis = C("#8A94A8"), Text = C("#E8ECF4"), Accent = C("#FFC94D"), HollowUp = true },
            new ChartTheme { Name = "Mono Carbon", Bg = C("#0B0B0C"), Plot = C("#0E0E10"), Grid = C("#1E1E22"), Up = C("#F2F2F2"), Down = C("#5C5F66"), UpWick = C("#BDBDBD"), DownWick = C("#7A7D85"), Axis = C("#8E9096"), Text = C("#F2F2F2"), Accent = C("#FFC94D"), HollowUp = true },
            new ChartTheme { Name = "Paper Light", Bg = C("#F4F6FA"), Plot = C("#FFFFFF"), Grid = C("#E6EAF0"), Up = C("#089981"), Down = C("#F23645"), UpWick = C("#089981"), DownWick = C("#F23645"), Axis = C("#5C6370"), Text = C("#151A22"), Accent = C("#2962FF") } };
    }
    public sealed class Drawing { public string Kind = "HLINE"; public DateTime T1, T2; public double P1, P2; public string Color = "#FFC94D"; }
    public sealed class ChartHit { public Rect R; public string Kind; public long Ticket; public Action Click; }

    public sealed class ChartView : FrameworkElement
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static SolidColorBrush Br(Color c, byte a = 255) { var x = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); x.Freeze(); return x; }
        static SolidColorBrush Hex(string h, byte a = 255) { return Br((Color)ColorConverter.ConvertFromString(h), a); }
        static Pen P(Brush b, double w, params double[] dash) { var p = new Pen(b, w); if (dash.Length > 0) p.DashStyle = new DashStyle(dash, 0); p.Freeze(); return p; }
        static readonly Typeface Face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), Mono = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        static readonly SolidColorBrush GreenB = Hex("#00E676"), RedB = Hex("#FF3D71"), GoldB = Hex("#FFC94D"), CyanB = Hex("#4DD8FF"), VioletB = Hex("#B388FF"), OrangeB = Hex("#FF9F43");

        // ---- what the host gives the chart
        public BarSeries Series; public int Digits = 2; public double Point = 0.01, Bid, Ask; public string Symbol = "", Currency = "USD", Status = "";
        public List<Mt5Position> Positions = new List<Mt5Position>(); public List<Mt5Order> Orders = new List<Mt5Order>();
        public object Gate = new object(); public ChartTheme Theme = ChartTheme.All[0];
        public Func<DateTime, DateTime> ToView = t => t; public string ZoneName = "SERVER"; public DateTime ServerNow = DateTime.MinValue;
        public int NyOffsetHours = -7;   // NY time = server time + this (IC Markets: server = New York + 7)
        public HashSet<string> Ind = new HashSet<string>();   // EMA9 EMA21 EMA50 EMA200 VWAP FVG ORB PDHL
        public List<Drawing> Drawings = new List<Drawing>(); public string Tool = "";   // "" = cursor, HLINE / VLINE / TREND / RECT
        public string News = ""; public bool NewsHot; public bool TradeOn; public double PickPrice = double.NaN; public string PickKind = "";
        // ---- what the chart tells the host
        public Action<long, double> CloseTicket;              // ticket, lots (0 = all)
        public Action<long, double, double> ModifyTicket;     // ticket, sl, tp
        public Action<long, double, double, double> ModifyOrder; // ticket, price, sl, tp
        public Action<long> CancelOrder;
        public Action<double> PricePicked;                    // a click while picking a limit / stop price
        public Action<Point, double, DateTime> RightClick;    // where, price, time
        public Action DrawingsChanged; public Action<string> Toast;

        double barW = 8, rightOffset = 6, mHi = double.NaN, mLo = double.NaN; bool auto = true;
        Point? mouse; Point dragFrom; double dragOff, dragHi, dragLo, dragBarW; string drag = null;
        readonly List<ChartHit> hits = new List<ChartHit>();
        long dragTicket; string dragWhat = ""; double dragPrice = double.NaN; Drawing dragDraw, newDraw; double dragDP; DateTime dragT1, dragT2; double dragP1, dragP2; Drawing selected;
        double hi, lo, plotW, plotH; int nBars;
        const double AxisW = 86, TimeH = 26, TopPad = 8;

        public ChartView() { ClipToBounds = true; Focusable = true; }
        public void ResetView() { auto = true; rightOffset = 6; InvalidateVisual(); }

        FormattedText T(string s, double size, Brush b, bool mono = false) { return new FormattedText(s, IC, FlowDirection.LeftToRight, mono ? Mono : Face, size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip); }
        string F(double v) { return v.ToString("F" + Digits, IC); }
        static double Nice(double raw) { if (raw <= 0) return 1; double e = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / e; return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * e; }
        double Y(double p) { return TopPad + (hi - p) / (hi - lo) * plotH; }
        double PriceAt(double y) { return hi - (y - TopPad) / plotH * (hi - lo); }
        double X(double i) { return plotW - (nBars - 1 - i + rightOffset + 0.5) * barW; }
        double IndexAt(double x) { return nBars - 1 + rightOffset + 0.5 - (plotW - x) / barW - 0.5; }
        double IndexOf(DateTime t)
        {
            var b = Series.Bars; int n = b.Count; if (n == 0) return 0; double m = Math.Max(1, Series.Minutes);
            if (t >= b[n - 1].Time) return n - 1 + (t - b[n - 1].Time).TotalMinutes / m; if (t <= b[0].Time) return (t - b[0].Time).TotalMinutes / m;
            int l = 0, h = n - 1; while (l < h) { int mid = (l + h) / 2; if (b[mid].Time >= t) h = mid; else l = mid + 1; }
            return l - Math.Min(1, (b[l].Time - t).TotalMinutes / m);
        }
        DateTime TimeAt(double idx) { var b = Series.Bars; int n = b.Count; if (n == 0) return DateTime.MinValue; int i = (int)Math.Round(Math.Max(0, Math.Min(n - 1, idx))); return b[i].Time.AddMinutes((idx - i) * Series.Minutes); }

        protected override void OnRender(DrawingContext dc) { lock (Gate) Draw(dc); }

        void Draw(DrawingContext dc)
        {
            hits.Clear();
            double W = ActualWidth, H = ActualHeight; if (W < 60 || H < 60) return; var th = Theme;
            plotW = W - AxisW; plotH = H - TimeH - TopPad;
            var axisB = Br(th.Axis); var textB = Br(th.Text); var gridP = P(Br(th.Grid), 1); var tagBg = Br(th.Plot, 235);
            dc.DrawRectangle(Br(th.Bg), null, new Rect(0, 0, W, H)); dc.DrawRectangle(Br(th.Plot), null, new Rect(0, TopPad, plotW, plotH));
            var bars = Series != null ? Series.Bars : null;
            if (bars == null || bars.Count == 0) { var msg = T(Status != "" ? Status : "loading the candles from MT5 …", 15, axisB); dc.DrawText(msg, new Point((plotW - msg.Width) / 2, H / 2 - 10)); return; }
            int n = bars.Count; nBars = n;
            int i1 = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(n - 1 + rightOffset + 0.5))), i0 = Math.Max(0, Math.Min(i1, (int)Math.Floor(n - 1 + rightOffset + 0.5 - plotW / barW)));
            hi = double.MinValue; lo = double.MaxValue;
            for (int i = i0; i <= i1; i++) { hi = Math.Max(hi, bars[i].H); lo = Math.Min(lo, bars[i].L); }
            if (Bid > 0 && rightOffset > -2) { hi = Math.Max(hi, Bid); lo = Math.Min(lo, Bid); }
            if (hi <= lo) hi = lo + Point * 10; double pad = (hi - lo) * 0.07; hi += pad; lo -= pad;
            if (!auto && !double.IsNaN(mHi)) { hi = mHi; lo = mLo; } else { mHi = hi; mLo = lo; }
            // grid, price axis
            double step = Nice((hi - lo) / Math.Max(3, plotH / 70));
            for (double p = Math.Ceiling(lo / step) * step; p <= hi; p += step) { double y = Y(p); dc.DrawLine(gridP, new Point(0, y), new Point(plotW, y)); var t = T(F(p), 11, axisB, true); dc.DrawText(t, new Point(plotW + 6, y - t.Height / 2)); }
            // time axis (in the chosen time zone)
            int every = Math.Max(1, (int)Math.Ceiling(95 / barW)); int tf = Series.Minutes;
            for (int i = i0; i <= i1; i++)
            {
                if (i % every != 0) continue; double x = X(i); if (x < 0 || x > plotW) continue;
                dc.DrawLine(gridP, new Point(x, TopPad), new Point(x, TopPad + plotH));
                var bt = ToView(bars[i].Time); bool newDay = i > 0 && ToView(bars[i - 1].Time).Date != bt.Date;
                var t = T(tf >= 1440 ? bt.ToString("dd MMM", IC) : newDay || tf >= 240 ? bt.ToString("dd MMM HH:mm", IC) : bt.ToString("HH:mm", IC), 11, newDay ? textB : axisB); dc.DrawText(t, new Point(x - t.Width / 2, TopPad + plotH + 5));
            }
            dc.PushClip(new RectangleGeometry(new Rect(0, TopPad, plotW, plotH)));
            DrawIndicators(dc, bars, i0, i1);
            // candles
            double bw = Math.Max(1, barW * 0.68); var upB = Br(th.Up); var dnB = Br(th.Down); var upW = P(Br(th.UpWick), 1); var dnW = P(Br(th.DownWick), 1); var upHollow = P(upB, 1.2);
            for (int i = i0; i <= i1; i++)
            {
                var b = bars[i]; double x = X(i); if (x < -barW || x > plotW + barW) continue; bool up = b.C >= b.O;
                double yo = Y(b.O), yc = Y(b.C), top = Math.Min(yo, yc), hgt = Math.Max(1, Math.Abs(yc - yo));
                if (th.Glow && barW >= 4) dc.DrawRectangle(Br(up ? th.Up : th.Down, 40), null, new Rect(x - bw / 2 - 2, top - 2, bw + 4, hgt + 4));
                dc.DrawLine(up ? upW : dnW, new Point(x, Y(b.H)), new Point(x, top)); dc.DrawLine(up ? upW : dnW, new Point(x, top + hgt), new Point(x, Y(b.L)));
                if (up && th.HollowUp && bw >= 3) dc.DrawRectangle(Br(th.Plot), upHollow, new Rect(x - bw / 2, top, bw, hgt));
                else dc.DrawRectangle(up ? upB : dnB, null, new Rect(x - bw / 2, top, bw, hgt));
            }
            DrawDrawings(dc);
            dc.Pop();
            DrawTrading(dc, W);
            // ask (faint) • bid tag + the candle's COUNTDOWN on the axis
            if (Ask > 0) { double ya = Y(Ask); if (ya > TopPad && ya < TopPad + plotH) dc.DrawLine(P(Br(th.Down, 90), 1, 2, 2), new Point(0, ya), new Point(plotW, ya)); }
            if (Bid > 0)
            {
                double yb = Y(Bid); var last = bars[n - 1]; var bc = last.C >= last.O ? upB : dnB;
                if (yb > TopPad && yb < TopPad + plotH) dc.DrawLine(P(bc, 1, 4, 3), new Point(0, yb), new Point(plotW, yb));
                double ty = Math.Max(TopPad, Math.Min(TopPad + plotH - 34, yb - 9));
                dc.DrawRoundedRectangle(bc, null, new Rect(plotW + 2, ty, AxisW - 4, 18), 3, 3); dc.DrawText(T(F(Bid), 11.5, Br(th.Bg), true), new Point(plotW + 6, ty + 1));
                if (ServerNow > DateTime.MinValue && tf < 10080)
                {
                    var left = last.Time.AddMinutes(tf) - ServerNow; if (left.TotalSeconds < 0 || left.TotalDays > 7) left = TimeSpan.Zero;
                    string cd = left.TotalHours >= 1 ? ((int)left.TotalHours) + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00") : left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
                    dc.DrawRoundedRectangle(tagBg, P(bc, 1), new Rect(plotW + 2, ty + 18, AxisW - 4, 16), 3, 3); dc.DrawText(T("⏱ " + cd, 10.5, textB, true), new Point(plotW + 6, ty + 19));
                }
            }
            // the picked price for a limit / stop order
            if (!double.IsNaN(PickPrice)) Level(dc, Y(PickPrice), OrangeB, "🎯 " + PickKind + " @ " + F(PickPrice) + " — press PLACE in the panel", true, 6, null);
            // NEWS banner
            if (News != "")
            {
                bool fl = NewsHot && DateTime.Now.Millisecond < 500; var nb = NewsHot ? RedB : OrangeB; var nt = T("📰 " + News, 12.5, fl ? Br(th.Bg) : textB);
                double nx = Math.Max(6, (plotW - nt.Width) / 2 - 10); dc.DrawRoundedRectangle(fl ? nb : tagBg, P(nb, 1.4), new Rect(nx, TopPad + 6, nt.Width + 20, 24), 12, 12); dc.DrawText(nt, new Point(nx + 10, TopPad + 9));
            }
            // crosshair (only inside the chart)
            if (mouse.HasValue && drag == null && mouse.Value.X < plotW && mouse.Value.Y > TopPad && mouse.Value.Y < TopPad + plotH)
            {
                var m = mouse.Value; var cp = P(Br(th.Axis, 150), 1, 3, 3); dc.DrawLine(cp, new Point(0, m.Y), new Point(plotW, m.Y)); dc.DrawLine(cp, new Point(m.X, TopPad), new Point(m.X, TopPad + plotH));
                dc.DrawRoundedRectangle(axisB, null, new Rect(plotW + 2, m.Y - 9, AxisW - 4, 18), 3, 3); dc.DrawText(T(F(PriceAt(m.Y)), 11, Br(th.Bg), true), new Point(plotW + 6, m.Y - 8));
                int ci = (int)Math.Round(IndexAt(m.X));
                if (ci >= 0 && ci < n)
                {
                    var cb = bars[ci]; var tt = T(ToView(cb.Time).ToString(tf >= 1440 ? "ddd dd MMM yyyy" : "ddd dd MMM HH:mm", IC) + " " + ZoneName, 11, Br(th.Bg));
                    dc.DrawRoundedRectangle(axisB, null, new Rect(m.X - tt.Width / 2 - 5, TopPad + plotH + 3, tt.Width + 10, 18), 3, 3); dc.DrawText(tt, new Point(m.X - tt.Width / 2, TopPad + plotH + 4));
                    var info = T("O " + F(cb.O) + "   H " + F(cb.H) + "   L " + F(cb.L) + "   C " + F(cb.C) + "   " + (cb.C - cb.O >= 0 ? "+" : "") + F(cb.C - cb.O), 12, textB, true);
                    dc.DrawRoundedRectangle(tagBg, null, new Rect(8, TopPad + (News != "" ? 36 : 6), info.Width + 16, 22), 6, 6); dc.DrawText(info, new Point(16, TopPad + (News != "" ? 39 : 9)));
                }
            }
            if (Tool != "") { var tt = T("✏ " + ToolName(Tool) + " — click on the chart" + (Tool == "TREND" || Tool == "RECT" ? " and drag" : "") + " • Esc = cancel", 11.5, GoldB); dc.DrawText(tt, new Point(10, TopPad + plotH - 20)); }
            else if (!auto) dc.DrawText(T("PRICE SCALE: MANUAL • double-click = automatic", 10.5, axisB), new Point(8, TopPad + plotH - 18));
            dc.DrawRectangle(null, P(Br(th.Accent, 110), 1.4), new Rect(0.5, TopPad + 0.5, plotW - 1, plotH - 1));
        }
        static string ToolName(string t) { return t == "HLINE" ? "HORIZONTAL LINE" : t == "VLINE" ? "VERTICAL LINE" : t == "TREND" ? "TREND LINE" : t == "RECT" ? "RECTANGLE" : t; }

        // ---- indicators (from the candles on screen and before them)
        void DrawIndicators(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            int n = bars.Count;
            foreach (var e in new[] { Tuple.Create("EMA9", 9, "#FFC94D"), Tuple.Create("EMA21", 21, "#4DD8FF"), Tuple.Create("EMA50", 50, "#B388FF"), Tuple.Create("EMA200", 200, "#FF9F43") })
            {
                if (!Ind.Contains(e.Item1) || n < 3) continue; double k = 2.0 / (e.Item2 + 1), v = bars[0].C; var g = new StreamGeometry();
                using (var c = g.Open()) { bool started = false; for (int i = 0; i < n; i++) { v = i == 0 ? bars[0].C : bars[i].C * k + v * (1 - k); if (i < i0 - 1 || i > i1) continue; var pt = new Point(X(i), Y(v)); if (!started) { c.BeginFigure(pt, false, false); started = true; } else c.LineTo(pt, true, false); } }
                g.Freeze(); dc.DrawGeometry(null, P(Hex(e.Item3), 1.6), g);
            }
            if (Ind.Contains("VWAP"))
            {
                // the day's VWAP from the server's midnight (the forex day: New York 17:00)
                var g = new StreamGeometry(); double pv = 0, vv = 0; DateTime day = DateTime.MinValue;
                using (var c = g.Open()) { bool started = false; for (int i = 0; i <= i1 && i < n; i++) { var b = bars[i]; if (b.Time.Date != day) { day = b.Time.Date; pv = vv = 0; if (i >= i0) started = false; } double tp = (b.H + b.L + b.C) / 3, w = Math.Max(1, b.V); pv += tp * w; vv += w; if (i < i0) continue; var pt = new Point(X(i), Y(pv / vv)); if (!started) { c.BeginFigure(pt, false, false); started = true; } else c.LineTo(pt, true, false); } }
                g.Freeze(); dc.DrawGeometry(null, P(GoldB, 2.2), g);
            }
            if (Ind.Contains("FVG"))
            {
                // fair-value gaps (3 candles whose wicks do not overlap) that price has not filled yet — the last 12 of each side
                var gaps = new List<Tuple<int, double, double, int>>();
                for (int i = Math.Max(2, i0 - 300); i <= i1; i++)
                {
                    if (bars[i].L > bars[i - 2].H) gaps.Add(Tuple.Create(i - 1, bars[i - 2].H, bars[i].L, 1)); else if (bars[i].H < bars[i - 2].L) gaps.Add(Tuple.Create(i - 1, bars[i].H, bars[i - 2].L, -1));
                }
                int nb = 0, ns = 0;
                foreach (var gp in gaps.AsEnumerable().Reverse())
                {
                    int end = n - 1; for (int j = gp.Item1 + 2; j < n; j++) if (gp.Item4 > 0 ? bars[j].L <= gp.Item2 : bars[j].H >= gp.Item3) { end = j; break; }
                    if (end < n - 1) continue; if (gp.Item4 > 0 && ++nb > 12) continue; if (gp.Item4 < 0 && ++ns > 12) continue;
                    var col = gp.Item4 > 0 ? Hex("#00E676", 38) : Hex("#FF3D71", 38); double x1 = X(gp.Item1), y1 = Y(gp.Item3), y2 = Y(gp.Item2);
                    dc.DrawRectangle(col, P(gp.Item4 > 0 ? Hex("#00E676", 120) : Hex("#FF3D71", 120), 0.8), new Rect(x1, Math.Min(y1, y2), Math.Max(2, plotW - x1), Math.Max(1, Math.Abs(y2 - y1))));
                }
            }
            if (Ind.Contains("ORB") && Series.Minutes < 60)
            {
                // the New York opening range 09:30–09:45 of the last sessions on screen (server = New York + 7 h → 16:30–16:45)
                var days = bars.Skip(i0).Take(i1 - i0 + 1).Select(b => b.Time.Date).Distinct().ToList();
                foreach (var d in days)
                {
                    var s0 = d.AddHours(9.5 - NyOffsetHours); var s1 = s0.AddMinutes(15); var inR = bars.Where(b => b.Time >= s0 && b.Time < s1).ToList(); if (inR.Count == 0) continue;
                    double h = inR.Max(b => b.H), l = inR.Min(b => b.L); double x1 = X(IndexOf(s0)), x2 = X(IndexOf(d.AddHours(16 - NyOffsetHours)));
                    dc.DrawRectangle(Hex("#B388FF", 26), P(Hex("#B388FF", 170), 1, 4, 3), new Rect(x1, Y(h), Math.Max(2, x2 - x1), Math.Max(1, Y(l) - Y(h))));
                    dc.DrawText(T("ORB " + F(h) + " / " + F(l), 10, VioletB), new Point(x1 + 3, Y(h) - 14));
                }
            }
            if (Ind.Contains("PDHL") && Series.Minutes < 1440)
            {
                var lastDay = bars[n - 1].Time.Date; var prev = bars.Where(b => b.Time.Date < lastDay).ToList();
                if (prev.Count > 0) { var pd = prev[prev.Count - 1].Time.Date; var pb = prev.Where(b => b.Time.Date == pd).ToList(); double h = pb.Max(b => b.H), l = pb.Min(b => b.L);
                    Level(dc, Y(h), Hex("#42A5F5"), "PDH " + F(h), true, plotW - 130, null); Level(dc, Y(l), Hex("#AB47BC"), "PDL " + F(l), true, plotW - 130, null); }
            }
        }

        // ---- drawings
        void DrawDrawings(DrawingContext dc)
        {
            foreach (var d in Drawings.Concat(newDraw != null ? new[] { newDraw } : new Drawing[0]))
            {
                var col = Hex(d.Color); var pen = P(col, d == selected ? 2.4 : 1.6); double x1 = X(IndexOf(d.T1)), x2 = X(IndexOf(d.T2)), y1 = Y(d.P1), y2 = Y(d.P2);
                if (d.Kind == "HLINE") { dc.DrawLine(pen, new Point(0, y1), new Point(plotW, y1)); dc.DrawText(T(F(d.P1), 10.5, col, true), new Point(plotW - 70, y1 - 15)); }
                else if (d.Kind == "VLINE") dc.DrawLine(pen, new Point(x1, TopPad), new Point(x1, TopPad + plotH));
                else if (d.Kind == "TREND") dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
                else if (d.Kind == "RECT") dc.DrawRectangle(Hex(d.Color, 30), pen, new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)));
                if (d == selected) foreach (var pt in new[] { new Point(x1, y1), new Point(x2, y2) }) dc.DrawEllipse(col, P(Br(Theme.Plot), 1.5), pt, 5, 5);
            }
        }
        Drawing DrawingAt(Point m)
        {
            foreach (var d in Drawings.AsEnumerable().Reverse())
            {
                double x1 = X(IndexOf(d.T1)), x2 = X(IndexOf(d.T2)), y1 = Y(d.P1), y2 = Y(d.P2);
                if (d.Kind == "HLINE" && Math.Abs(m.Y - y1) <= 5) return d;
                if (d.Kind == "VLINE" && Math.Abs(m.X - x1) <= 5) return d;
                if (d.Kind == "RECT" && m.X >= Math.Min(x1, x2) - 4 && m.X <= Math.Max(x1, x2) + 4 && m.Y >= Math.Min(y1, y2) - 4 && m.Y <= Math.Max(y1, y2) + 4) return d;
                if (d.Kind == "TREND") { double len2 = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1); double u = len2 < 1 ? 0 : Math.Max(0, Math.Min(1, ((m.X - x1) * (x2 - x1) + (m.Y - y1) * (y2 - y1)) / len2)); double px = x1 + u * (x2 - x1), py = y1 + u * (y2 - y1); if (Math.Abs(m.X - px) + Math.Abs(m.Y - py) <= 7) return d; }
            }
            return null;
        }
        public void DeleteSelected() { if (selected != null) { Drawings.Remove(selected); selected = null; if (DrawingsChanged != null) DrawingsChanged(); InvalidateVisual(); } }

        // ---- your tickets and orders: every line, a label on the right with ✕, S/L / T/P lines you drag
        void DrawTrading(DrawingContext dc, double W)
        {
            var mine = Positions.Where(p => string.Equals(p.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            var ords = Orders.Where(o => string.Equals(o.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            // the labels sit on the right, pushed apart so none hides another
            var tags = new List<Tuple<double, string, SolidColorBrush, string, long, double>>();   // y, text, colour, kind (POS / SL / TP / ORD), ticket, price
            foreach (var p in mine)
            {
                bool dragged = drag == "TICKET" && dragTicket == p.Ticket;
                double sl = dragged && dragWhat == "SL" ? dragPrice : p.Sl, tp = dragged && dragWhat == "TP" ? dragPrice : p.Tp;
                if (dragged && dragWhat == "NEW") { if ((p.Dir > 0) == (dragPrice < p.Open)) sl = dragPrice; else tp = dragPrice; }
                var col = p.Dir > 0 ? GreenB : RedB; double pl = p.Profit + p.Swap;
                tags.Add(Tuple.Create(Y(p.Open), (p.Dir > 0 ? "▲ " : "▼ ") + p.Lots.ToString("0.00", IC) + "  " + (pl >= 0 ? "+" : "−") + "$" + Math.Abs(pl).ToString("N2", IC), col, "POS", p.Ticket, p.Open));
                if (sl > 0) tags.Add(Tuple.Create(Y(sl), "S/L " + F(sl), RedB, "SL", p.Ticket, sl));
                if (tp > 0) tags.Add(Tuple.Create(Y(tp), "T/P " + F(tp), GreenB, "TP", p.Ticket, tp));
            }
            foreach (var o in ords) { double px = drag == "ORDER" && dragTicket == o.Ticket ? dragPrice : o.Price; tags.Add(Tuple.Create(Y(px), "⏳ " + o.Type + " " + o.Lots.ToString("0.00", IC), CyanB, "ORD", o.Ticket, px)); }
            // the lines first (full width, faint for tickets, dashed for stops / targets / orders)
            foreach (var t in tags)
            {
                if (t.Item1 < TopPad || t.Item1 > TopPad + plotH) continue;
                var c = t.Item3.Color; var pen = t.Item4 == "POS" ? P(Br(c, 150), 1) : P(Br(c, 200), 1, 5, 4);
                dc.DrawLine(pen, new Point(0, t.Item1), new Point(plotW, t.Item1));
            }
            // the side averages (a strong line + summary on the left)
            foreach (var side in new[] { 1, -1 })
            {
                var ps = mine.Where(p => p.Dir == side).ToList(); if (ps.Count < 2) continue;
                double lots = ps.Sum(p => p.Lots), avg = ps.Sum(p => p.Open * p.Lots) / Math.Max(1e-9, lots), pl = ps.Sum(p => p.Profit + p.Swap);
                Level(dc, Y(avg), side > 0 ? GreenB : RedB, (side > 0 ? "▲ BUYS " : "▼ SELLS ") + ps.Count + " • " + lots.ToString("0.00", IC) + " lots • avg " + F(avg) + " • " + (pl >= 0 ? "+" : "−") + "$" + Math.Abs(pl).ToString("N2", IC), false, 6, pl >= 0 ? GreenB : RedB);
            }
            // the labels, stacked without overlap
            var vis = tags.Where(t => t.Item1 >= TopPad - 40 && t.Item1 <= TopPad + plotH + 40).OrderBy(t => t.Item1).ToList(); double lastY = double.MinValue;
            foreach (var t in vis)
            {
                double y = Math.Max(t.Item1, lastY + 19); lastY = y; if (y > TopPad + plotH - 8) continue;
                var ft = T(t.Item2, 11, t.Item3, true); double w = ft.Width + 34, x = plotW - w - 6;
                if (Math.Abs(y - t.Item1) > 2) dc.DrawLine(P(Br(t.Item3.Color, 120), 1), new Point(x + w, t.Item1), new Point(x + w - 6, y));
                var r = new Rect(x, y - 9, w, 18); dc.DrawRoundedRectangle(Br(Theme.Plot, 240), P(t.Item3, 1), r, 5, 5); dc.DrawText(ft, new Point(x + 6, y - 7.5));
                var xr = new Rect(x + w - 20, y - 8, 16, 16); dc.DrawRoundedRectangle(Br(t.Item3.Color, 60), null, xr, 4, 4); dc.DrawText(T("✕", 10.5, t.Item3), new Point(xr.X + 3.5, xr.Y));
                long tk = t.Item5; string kind = t.Item4;
                var pos = mine.FirstOrDefault(p => p.Ticket == tk); var ord = ords.FirstOrDefault(o => o.Ticket == tk);
                hits.Add(new ChartHit { R = xr, Kind = "X", Ticket = tk, Click = () =>
                {
                    if (kind == "POS" && CloseTicket != null) CloseTicket(tk, 0);
                    else if (kind == "SL" && pos != null && ModifyTicket != null) ModifyTicket(tk, 0, pos.Tp);
                    else if (kind == "TP" && pos != null && ModifyTicket != null) ModifyTicket(tk, pos.Sl, 0);
                    else if (kind == "ORD" && CancelOrder != null) CancelOrder(tk);
                } });
                hits.Add(new ChartHit { R = new Rect(x, y - 9, w - 22, 18), Kind = kind, Ticket = tk });
            }
        }

        // a horizontal level with its label
        void Level(DrawingContext dc, double y, SolidColorBrush br, string text, bool dashed, double labelX, Brush labelFg)
        {
            if (y < TopPad || y > TopPad + plotH) return;
            dc.DrawLine(dashed ? P(br, 1, 5, 4) : P(br, 1.8), new Point(0, y), new Point(plotW, y));
            var t = T(text, 11.5, labelFg ?? br); double lx = Math.Min(labelX, plotW - t.Width - 20); dc.DrawRoundedRectangle(Br(Theme.Plot, 235), P(br, 1), new Rect(lx, y - 10, t.Width + 12, 20), 5, 5); dc.DrawText(t, new Point(lx + 6, y - 8));
        }

        // ---- what is under the mouse (for the cursor and the clicks)
        string HitAt(Point p, out long ticket, out string kind)
        {
            ticket = 0; kind = "";
            foreach (var h in hits.AsEnumerable().Reverse()) if (h.R.Contains(p)) { ticket = h.Ticket; kind = h.Kind; return "HIT"; }
            if (p.X > plotW) return "PRICEAXIS"; if (p.Y > TopPad + plotH) return "TIMEAXIS";
            if (TradeOn)
            {
                // a ticket / S/L / T/P / order line within 4 px
                foreach (var ps in Positions.Where(q => string.Equals(q.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)))
                {
                    if (ps.Sl > 0 && Math.Abs(p.Y - Y(ps.Sl)) <= 4) { ticket = ps.Ticket; kind = "SL"; return "LINE"; }
                    if (ps.Tp > 0 && Math.Abs(p.Y - Y(ps.Tp)) <= 4) { ticket = ps.Ticket; kind = "TP"; return "LINE"; }
                }
                foreach (var o in Orders.Where(q => string.Equals(q.Symbol, Symbol, StringComparison.OrdinalIgnoreCase))) if (Math.Abs(p.Y - Y(o.Price)) <= 4) { ticket = o.Ticket; kind = "ORD"; return "LINE"; }
                foreach (var ps in Positions.Where(q => string.Equals(q.Symbol, Symbol, StringComparison.OrdinalIgnoreCase))) if (Math.Abs(p.Y - Y(ps.Open)) <= 3) { ticket = ps.Ticket; kind = "POS"; return "LINE"; }
            }
            if (DrawingAt(p) != null) return "DRAWING";
            return "PLOT";
        }

        // ---- mouse
        protected override void OnMouseWheel(MouseWheelEventArgs e) { barW = Math.Max(1.2, Math.Min(80, barW * (e.Delta > 0 ? 1.15 : 1 / 1.15))); InvalidateVisual(); e.Handled = true; }
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            var p = e.GetPosition(this); if (Series == null || Series.Bars.Count == 0) return; e.Handled = true;
            var d = DrawingAt(p); if (d != null) selected = d;
            if (RightClick != null) RightClick(p, PriceAt(p.Y), TimeAt(IndexAt(p.X)));
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Tool = ""; newDraw = null; selected = null; PickPrice = double.NaN; InvalidateVisual(); }
            if (e.Key == Key.Delete) DeleteSelected();
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus(); var p = e.GetPosition(this); if (Series == null || Series.Bars.Count == 0) return;
            if (e.ClickCount == 2 && Tool == "") { ResetView(); e.Handled = true; return; }
            long tk; string kind; string what = HitAt(p, out tk, out kind);
            if (what == "HIT") { var h = hits.LastOrDefault(x => x.R.Contains(p)); if (h != null && h.Click != null) { h.Click(); e.Handled = true; return; } }
            if (PricePicked != null && PickKind != "" && double.IsNaN(PickPrice) && p.X < plotW) { PickPrice = Math.Round(PriceAt(p.Y) / Point) * Point; PricePicked(PickPrice); InvalidateVisual(); e.Handled = true; return; }
            if (Tool != "" && p.X < plotW && p.Y < TopPad + plotH)
            {
                var t = TimeAt(IndexAt(p.X)); double pr = PriceAt(p.Y);
                newDraw = new Drawing { Kind = Tool, T1 = t, T2 = t, P1 = pr, P2 = pr, Color = Tool == "HLINE" ? "#FFC94D" : Tool == "VLINE" ? "#8A94A8" : Tool == "TREND" ? "#4DD8FF" : "#B388FF" };
                if (Tool == "HLINE" || Tool == "VLINE") { Drawings.Add(newDraw); selected = newDraw; newDraw = null; Tool = ""; if (DrawingsChanged != null) DrawingsChanged(); InvalidateVisual(); e.Handled = true; return; }
                drag = "NEWDRAW"; CaptureMouse(); e.Handled = true; return;
            }
            dragFrom = p; dragOff = rightOffset; dragHi = mHi; dragLo = mLo; dragBarW = barW;
            if (what == "PRICEAXIS") drag = "AXIS";
            else if (what == "TIMEAXIS") drag = "TIME";
            else if ((what == "LINE" || what == "HIT") && (kind == "SL" || kind == "TP" || kind == "POS" || kind == "ORD")) { drag = kind == "ORD" ? "ORDER" : "TICKET"; dragTicket = tk; dragWhat = kind == "POS" ? "NEW" : kind; dragPrice = PriceAt(p.Y); }
            else if (what == "DRAWING") { dragDraw = DrawingAt(p); selected = dragDraw; drag = "MOVEDRAW"; dragT1 = dragDraw.T1; dragT2 = dragDraw.T2; dragP1 = dragDraw.P1; dragP2 = dragDraw.P2; dragDP = IndexAt(p.X); }
            else { drag = "PAN"; selected = null; }
            CaptureMouse(); e.Handled = true; InvalidateVisual();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this); mouse = p;
            if (drag == null)
            {
                long tk; string kind; string what = Series == null ? "PLOT" : HitAt(p, out tk, out kind);
                Cursor = what == "HIT" ? Cursors.Hand : what == "PRICEAXIS" ? Cursors.SizeNS : what == "TIMEAXIS" ? Cursors.SizeWE : what == "LINE" ? Cursors.SizeNS : what == "DRAWING" ? Cursors.SizeAll : Tool != "" || (PickKind != "" && double.IsNaN(PickPrice)) ? Cursors.Pen : Cursors.Cross;
            }
            else
            {
                double dy = p.Y - dragFrom.Y, rng = dragHi - dragLo;
                if (drag == "PAN")
                {
                    rightOffset = Math.Min(plotW / barW * 0.8, Math.Max(-((Series != null ? Series.Bars.Count : 0) - 5), dragOff - (p.X - dragFrom.X) / barW));
                    if (!double.IsNaN(dragHi) && (Math.Abs(dy) > 8 || !auto)) { auto = false; double sh = dy / plotH * rng; mHi = dragHi + sh; mLo = dragLo + sh; }
                }
                else if (drag == "AXIS" && !double.IsNaN(dragHi)) { auto = false; double k = Math.Exp(dy / 160.0), mid = (dragHi + dragLo) / 2; mHi = mid + rng * k / 2; mLo = mid - rng * k / 2; }
                else if (drag == "TIME") barW = Math.Max(1.2, Math.Min(80, dragBarW * Math.Exp((p.X - dragFrom.X) / 180.0)));   // drag right = wider candles, left = more candles
                else if (drag == "TICKET" || drag == "ORDER") dragPrice = Math.Round(PriceAt(p.Y) / Point) * Point;
                else if (drag == "NEWDRAW" && newDraw != null) { newDraw.T2 = TimeAt(IndexAt(p.X)); newDraw.P2 = PriceAt(p.Y); }
                else if (drag == "MOVEDRAW" && dragDraw != null)
                {
                    double di = IndexAt(p.X) - dragDP, dp = PriceAt(p.Y) - PriceAt(dragFrom.Y);
                    dragDraw.T1 = dragT1.AddMinutes(di * Series.Minutes); dragDraw.T2 = dragT2.AddMinutes(di * Series.Minutes); dragDraw.P1 = dragP1 + dp; dragDraw.P2 = dragP2 + dp;
                }
            }
            InvalidateVisual();
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            var was = drag; drag = null; ReleaseMouseCapture();
            if (was == "TICKET")
            {
                var ps = Positions.FirstOrDefault(q => q.Ticket == dragTicket); double pr = dragPrice;
                if (ps != null && ModifyTicket != null && !double.IsNaN(pr) && Math.Abs(pr - (dragWhat == "SL" ? ps.Sl : dragWhat == "TP" ? ps.Tp : ps.Open)) > Point / 2)
                {
                    double sl = ps.Sl, tp = ps.Tp;
                    if (dragWhat == "SL") sl = pr; else if (dragWhat == "TP") tp = pr; else if ((ps.Dir > 0) == (pr < ps.Open)) sl = pr; else tp = pr;
                    bool bad = ps.Dir > 0 ? (sl > 0 && sl >= Bid) || (tp > 0 && tp <= Bid) : (sl > 0 && sl <= Ask) || (tp > 0 && tp >= Ask);
                    if (bad) { if (Toast != null) Toast("a stop must be on the losing side of the price, a target on the winning side"); } else ModifyTicket(ps.Ticket, sl, tp);
                }
            }
            else if (was == "ORDER") { var o = Orders.FirstOrDefault(q => q.Ticket == dragTicket); if (o != null && ModifyOrder != null && Math.Abs(dragPrice - o.Price) > Point / 2) ModifyOrder(o.Ticket, dragPrice, o.Sl, o.Tp); }
            else if (was == "NEWDRAW" && newDraw != null) { Drawings.Add(newDraw); selected = newDraw; newDraw = null; Tool = ""; if (DrawingsChanged != null) DrawingsChanged(); }
            else if (was == "MOVEDRAW" && DrawingsChanged != null) DrawingsChanged();
            dragPrice = double.NaN; InvalidateVisual();
        }
        protected override void OnMouseLeave(MouseEventArgs e) { mouse = null; InvalidateVisual(); }
    }
}
