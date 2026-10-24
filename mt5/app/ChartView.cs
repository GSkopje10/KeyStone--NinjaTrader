// SpasiX MT5 — the chart: candles from MT5, drawn directly (smooth at any zoom), the bid / ask, your tickets (each side's average with its
// lots and P/L, every ticket faint, its S/L / T/P), the pending orders and a crosshair.
// Mouse: wheel = zoom • drag = scroll back in time / up-down (the scale goes manual) • drag the price axis = stretch • double-click = auto again.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed class ChartView : FrameworkElement
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static SolidColorBrush B(byte a, byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromArgb(a, r, g, b)); x.Freeze(); return x; }
        static Pen P(Brush b, double w, params double[] dash) { var p = new Pen(b, w); if (dash.Length > 0) p.DashStyle = new DashStyle(dash, 0); p.Freeze(); return p; }
        static readonly Brush BgB = B(255, 0x0A, 0x0E, 0x16), PlotB = B(255, 0x0C, 0x11, 0x1C), GridB = B(255, 0x17, 0x20, 0x31), AxisT = B(255, 0x8A, 0x94, 0xA8), TextB = B(255, 0xE8, 0xEC, 0xF4),
            Up = B(255, 0x4D, 0xD8, 0xFF), Dn = B(255, 0xFF, 0x3D, 0x71), GreenB = B(255, 0x00, 0xE6, 0x76), RedB = B(255, 0xFF, 0x3D, 0x71), GoldB = B(255, 0xFF, 0xC9, 0x4D), CyanB = B(255, 0x4D, 0xD8, 0xFF), MutedB = B(255, 0x7D, 0x87, 0x9A),
            TagBg = B(235, 0x0F, 0x15, 0x22), CrossB = B(150, 0xC8, 0xD0, 0xE0);
        static readonly Pen GridP = P(GridB, 1), UpP = P(Up, 1), DnP = P(Dn, 1), CrossP = P(CrossB, 1, 3, 3), FrameP = P(B(110, 0xFF, 0xC9, 0x4D), 1.4);
        static readonly Typeface Face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), Mono = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        public BarSeries Series; public int Digits = 2; public double Point = 0.01, Bid, Ask; public string Symbol = "", Currency = "USD";
        public List<Mt5Position> Positions = new List<Mt5Position>(); public List<Mt5Order> Orders = new List<Mt5Order>();
        public string Status = "";   // shown when there are no candles yet
        public object Gate = new object();   // the bridge thread adds candles: drawn under its lock
        double barW = 8, rightOffset = 6, mHi = double.NaN, mLo = double.NaN; bool auto = true;
        Point? mouse; Point dragFrom; double dragOff, dragHi, dragLo; string drag = null;
        const double AxisW = 84, TimeH = 26, TopPad = 8;

        public ChartView() { ClipToBounds = true; Focusable = true; Cursor = Cursors.Cross; }
        public void ResetView() { auto = true; rightOffset = 6; InvalidateVisual(); }

        FormattedText T(string s, double size, Brush b, bool mono = false)
        { return new FormattedText(s, IC, FlowDirection.LeftToRight, mono ? Mono : Face, size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip); }
        string F(double v) { return v.ToString("F" + Digits, IC); }
        static double Nice(double raw) { if (raw <= 0) return 1; double e = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / e; return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * e; }

        protected override void OnRender(DrawingContext dc)
        {
            lock (Gate) Draw(dc);
        }
        void Draw(DrawingContext dc)
        {
            double W = ActualWidth, H = ActualHeight; if (W < 50 || H < 50) return;
            double plotW = W - AxisW, plotH = H - TimeH - TopPad;
            dc.DrawRectangle(BgB, null, new Rect(0, 0, W, H)); dc.DrawRectangle(PlotB, null, new Rect(0, TopPad, plotW, plotH));
            var bars = Series != null ? Series.Bars : null;
            if (bars == null || bars.Count == 0)
            {
                var msg = T(Status != "" ? Status : "loading the candles from MT5 …", 15, MutedB); dc.DrawText(msg, new Point((plotW - msg.Width) / 2, H / 2 - 10)); return;
            }
            int n = bars.Count;
            Func<double, double> xOf = i => plotW - (n - 1 - i + rightOffset + 0.5) * barW;
            int i1 = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(n - 1 + rightOffset + 0.5))), i0 = Math.Max(0, Math.Min(i1, (int)Math.Floor(n - 1 + rightOffset + 0.5 - plotW / barW)));
            // the price scale: automatic = the visible candles (+ the bid), or yours after dragging
            double hi = double.MinValue, lo = double.MaxValue;
            for (int i = i0; i <= i1; i++) { hi = Math.Max(hi, bars[i].H); lo = Math.Min(lo, bars[i].L); }
            if (Bid > 0 && rightOffset > -2) { hi = Math.Max(hi, Bid); lo = Math.Min(lo, Bid); }
            if (hi <= lo) { hi = lo + Point * 10; }
            double pad = (hi - lo) * 0.07; hi += pad; lo -= pad;
            if (!auto && !double.IsNaN(mHi)) { hi = mHi; lo = mLo; } else { mHi = hi; mLo = lo; }
            Func<double, double> yOf = p => TopPad + (hi - p) / (hi - lo) * plotH;
            // grid + price axis
            double step = Nice((hi - lo) / Math.Max(3, plotH / 70));
            for (double p = Math.Ceiling(lo / step) * step; p <= hi; p += step)
            {
                double y = yOf(p); dc.DrawLine(GridP, new Point(0, y), new Point(plotW, y));
                var t = T(F(p), 11, AxisT, true); dc.DrawText(t, new Point(plotW + 6, y - t.Height / 2));
            }
            // time axis
            int every = Math.Max(1, (int)Math.Ceiling(95 / barW)); int tf = Series.Minutes;
            for (int i = i0; i <= i1; i++)
            {
                if (i % every != 0) continue; double x = xOf(i); if (x < 0 || x > plotW) continue;
                dc.DrawLine(GridP, new Point(x, TopPad), new Point(x, TopPad + plotH));
                var bt = bars[i].Time; bool newDay = i > 0 && bars[i - 1].Time.Date != bt.Date;
                string lab = tf >= 1440 ? bt.ToString("dd MMM", IC) : newDay || tf >= 240 ? bt.ToString("dd MMM HH:mm", IC) : bt.ToString("HH:mm", IC);
                var t = T(lab, 11, newDay ? TextB : AxisT); dc.DrawText(t, new Point(x - t.Width / 2, TopPad + plotH + 5));
            }
            // candles
            double bw = Math.Max(1, barW * 0.68);
            for (int i = i0; i <= i1; i++)
            {
                var b = bars[i]; double x = xOf(i); if (x < -barW || x > plotW + barW) continue; bool up = b.C >= b.O; var pen = up ? UpP : DnP; var br = up ? Up : Dn;
                dc.DrawLine(pen, new Point(x, yOf(b.H)), new Point(x, yOf(b.L)));
                double yo = yOf(b.O), yc = yOf(b.C); dc.DrawRectangle(br, null, new Rect(x - bw / 2, Math.Min(yo, yc), bw, Math.Max(1, Math.Abs(yc - yo))));
            }
            // your tickets: every ticket faint, each side's average strong with its lots and P/L, S/L and T/P per ticket
            var mine = Positions.Where(p => string.Equals(p.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var p in mine)
            {
                var br = p.Dir > 0 ? GreenB : RedB; double y = yOf(p.Open); if (y > TopPad && y < TopPad + plotH) dc.DrawLine(P(B(70, ((SolidColorBrush)br).Color.R, ((SolidColorBrush)br).Color.G, ((SolidColorBrush)br).Color.B), 1, 2, 3), new Point(0, y), new Point(plotW, y));
                if (p.Sl > 0) Level(dc, yOf(p.Sl), plotW, RedB, "S/L " + F(p.Sl) + "  #" + p.Ticket, true);
                if (p.Tp > 0) Level(dc, yOf(p.Tp), plotW, GreenB, "T/P " + F(p.Tp) + "  #" + p.Ticket, true);
            }
            foreach (var side in new[] { 1, -1 })
            {
                var ps = mine.Where(p => p.Dir == side).ToList(); if (ps.Count == 0) continue;
                double lots = ps.Sum(p => p.Lots), avg = ps.Sum(p => p.Open * p.Lots) / Math.Max(1e-9, lots), pl = ps.Sum(p => p.Profit + p.Swap);
                Level(dc, yOf(avg), plotW, side > 0 ? GreenB : RedB, (side > 0 ? "▲ BUY " : "▼ SELL ") + ps.Count + (ps.Count == 1 ? " ticket" : " tickets") + " • " + lots.ToString("0.00", IC) + " lots • avg " + F(avg) + " • " + (pl >= 0 ? "+" : "−") + "$" + Math.Abs(pl).ToString("N2", IC), false, pl >= 0 ? GreenB : RedB);
            }
            foreach (var o in Orders.Where(o => string.Equals(o.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)))
                Level(dc, yOf(o.Price), plotW, CyanB, "⏳ " + o.Type + " " + o.Lots.ToString("0.00", IC) + " @ " + F(o.Price) + "  #" + o.Ticket, true);
            // ask (faint) and bid (tag on the axis)
            if (Ask > 0) { double ya = yOf(Ask); if (ya > TopPad && ya < TopPad + plotH) dc.DrawLine(P(B(90, 0xFF, 0x3D, 0x71), 1, 2, 2), new Point(0, ya), new Point(plotW, ya)); }
            if (Bid > 0)
            {
                double yb = yOf(Bid); var last = bars[n - 1]; var bc = last.C >= last.O ? Up : Dn;
                if (yb > TopPad && yb < TopPad + plotH) dc.DrawLine(P(bc, 1, 4, 3), new Point(0, yb), new Point(plotW, yb));
                var t = T(F(Bid), 11.5, BgB, true); double ty = Math.Max(TopPad, Math.Min(TopPad + plotH - 18, yb - 9));
                dc.DrawRoundedRectangle(bc, null, new Rect(plotW + 2, ty, AxisW - 4, 18), 3, 3); dc.DrawText(t, new Point(plotW + 6, ty + 1));
            }
            // crosshair
            if (mouse.HasValue && drag == null && mouse.Value.X < plotW && mouse.Value.Y > TopPad && mouse.Value.Y < TopPad + plotH)
            {
                var m = mouse.Value; dc.DrawLine(CrossP, new Point(0, m.Y), new Point(plotW, m.Y)); dc.DrawLine(CrossP, new Point(m.X, TopPad), new Point(m.X, TopPad + plotH));
                double pr = hi - (m.Y - TopPad) / plotH * (hi - lo); var pt = T(F(pr), 11, BgB, true); dc.DrawRoundedRectangle(MutedB, null, new Rect(plotW + 2, m.Y - 9, AxisW - 4, 18), 3, 3); dc.DrawText(pt, new Point(plotW + 6, m.Y - 8));
                int ci = (int)Math.Round(n - 1 + rightOffset - (plotW - m.X) / barW - 0.5);
                if (ci >= 0 && ci < n)
                {
                    var cb = bars[ci]; var tt = T(cb.Time.ToString(tf >= 1440 ? "ddd dd MMM yyyy" : "ddd dd MMM HH:mm", IC), 11, BgB); dc.DrawRoundedRectangle(MutedB, null, new Rect(m.X - tt.Width / 2 - 5, TopPad + plotH + 3, tt.Width + 10, 18), 3, 3); dc.DrawText(tt, new Point(m.X - tt.Width / 2, TopPad + plotH + 4));
                    var info = T("O " + F(cb.O) + "   H " + F(cb.H) + "   L " + F(cb.L) + "   C " + F(cb.C) + "   " + (cb.C - cb.O >= 0 ? "+" : "") + F(cb.C - cb.O), 12, TextB, true);
                    dc.DrawRoundedRectangle(TagBg, null, new Rect(8, TopPad + 6, info.Width + 16, 22), 6, 6); dc.DrawText(info, new Point(16, TopPad + 9));
                }
            }
            if (!auto) { var mt = T("PRICE SCALE: MANUAL • double-click = auto", 10.5, MutedB); dc.DrawText(mt, new Point(8, TopPad + plotH - 18)); }
            dc.DrawRectangle(null, FrameP, new Rect(0.5, TopPad + 0.5, plotW - 1, plotH - 1));
        }

        // a horizontal level with its label on the left (tickets, S/L, T/P, orders)
        void Level(DrawingContext dc, double y, double plotW, Brush br, string text, bool dashed, Brush labelFg = null)
        {
            if (y < TopPad || y > ActualHeight - TimeH) return;
            dc.DrawLine(dashed ? P(br, 1, 5, 4) : P(br, 1.6), new Point(0, y), new Point(plotW, y));
            var t = T(text, 11.5, labelFg ?? br); dc.DrawRoundedRectangle(TagBg, P(br, 1), new Rect(6, y - 10, t.Width + 12, 20), 5, 5); dc.DrawText(t, new Point(12, y - 8));
        }

        // ---- mouse
        protected override void OnMouseWheel(MouseWheelEventArgs e) { barW = Math.Max(1.5, Math.Min(60, barW * (e.Delta > 0 ? 1.15 : 1 / 1.15))); InvalidateVisual(); e.Handled = true; }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus(); var p = e.GetPosition(this); double plotW = ActualWidth - AxisW;
            if (e.ClickCount == 2) { ResetView(); e.Handled = true; return; }
            drag = p.X > plotW ? "AXIS" : "PAN"; dragFrom = p; dragOff = rightOffset; dragHi = mHi; dragLo = mLo; CaptureMouse(); e.Handled = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this); mouse = p;
            if (drag != null && !double.IsNaN(dragHi))
            {
                double plotH = ActualHeight - TimeH - TopPad, dy = p.Y - dragFrom.Y, rng = dragHi - dragLo;
                if (drag == "PAN")
                {
                    double plotW = ActualWidth - AxisW;   // drag right = older candles come in
                    rightOffset = Math.Min(plotW / barW * 0.8, Math.Max(-((Series != null ? Series.Bars.Count : 0) - 5), dragOff - (p.X - dragFrom.X) / barW));
                    if (Math.Abs(dy) > 6 || !auto) { auto = false; double sh = dy / plotH * rng; mHi = dragHi + sh; mLo = dragLo + sh; }
                }
                else { auto = false; double k = Math.Exp(dy / 160.0), mid = (dragHi + dragLo) / 2; mHi = mid + rng * k / 2; mLo = mid - rng * k / 2; }
            }
            InvalidateVisual();
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { drag = null; ReleaseMouseCapture(); }
        protected override void OnMouseLeave(MouseEventArgs e) { mouse = null; InvalidateVisual(); }
    }
}
