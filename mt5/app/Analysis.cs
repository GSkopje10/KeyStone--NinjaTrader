// SpasiX — the ANALYSIS tab: a chart of its own for the DECISION (its own indicators per instrument — order flow, volume profile, sessions,
// bulls vs bears … — so the trading chart stays clean) and, on the right, the reading of the market now: the trend, the VWAP, where the price
// is in today's value area, the order flow (estimated), the sessions open now, the day's gap and a verdict (LEAN LONG / LEAN SHORT / WAIT)
// with its reasons. Analysis only: nothing is sent from here — "TRADE ON THE CHART" goes back to the 📈 CHART tab.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        ChartView anView; StackPanel anCards; TextBlock anHead; int anTf = 5; string anSym = "", anSig = ""; DateTime anAt = DateTime.MinValue;
        readonly List<Tuple<int, Button>> anTfBtns = new List<Tuple<int, Button>>(); Button anIndBtn;
        static readonly string[] AnDefault = { "VWAP", "VPROF", "VOL", "FIGHT", "FVG", "SESS_LON", "SESS_NY" };

        FrameworkElement BuildAnalysis()
        {
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition());
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) }; Grid.SetColumnSpan(bar, 2); g.Children.Add(bar);
            anHead = new TextBlock { Foreground = Gold, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 14, 0), Text = "📊 ANALYSIS" }; bar.Children.Add(anHead);
            anTf = IntOf(Get(ModeKey + "an.tf", "5"), 5);
            foreach (var m in new[] { 1, 5, 15, 30, 60, 240 })
            {
                int mm = m; var b = Pill(Tf.Name(m), Card); b.Height = 30; b.MinWidth = 48; b.Foreground = Gold; b.Click += delegate { anTf = mm; Put(ModeKey + "an.tf", mm.ToString(IC)); anView.ResetView(); PaintAnBar(); }; bar.Children.Add(b); anTfBtns.Add(Tuple.Create(m, b));
            }
            anIndBtn = Sx.Drop("📈 INDICATORS", Card, () => AnIndMenu()); anIndBtn.Height = 30; anIndBtn.Margin = new Thickness(8, 0, 6, 0); bar.Children.Add(anIndBtn);
            var back = Pill("📈 TRADE ON THE CHART ›", Tint(Green, 60)); back.Height = 30; back.Foreground = Green; back.ToolTip = "orders go from the 📈 CHART tab (same symbol)"; back.Click += delegate { Show("CHART"); }; bar.Children.Add(back);
            bar.Children.Add(new TextBlock { Text = "analysis only — nothing is sent from this tab • the symbol follows the chart's symbol tabs", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            anView = new ChartView { Sim = Sim, Theme = CurTheme() };
            anView.IndOff = key => { AnSet(key, false); };
            var host = new Border { Child = anView, CornerRadius = new CornerRadius(12), BorderBrush = Tint(Gold, 90), BorderThickness = new Thickness(1), ClipToBounds = true, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetRow(host, 1); g.Children.Add(host);
            anCards = new StackPanel(); var sv = new ScrollViewer { Content = anCards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(sv, 1); Grid.SetColumn(sv, 1); g.Children.Add(sv);
            PaintAnBar(); return g;
        }
        string AnKey(string sym, string key) { return ModeKey + "an." + (sym ?? "").ToUpperInvariant() + "." + key; }
        void AnLoadInd()
        {
            anView.Ind.Clear(); bool any = false;
            foreach (var t in ChartView.Indicators) { var v = Get(AnKey(anSym, t.Key), ""); if (v != "") any = true; if (v == "1") anView.Ind.Add(t.Key); }
            if (!any) foreach (var k in AnDefault) anView.Ind.Add(k);   // a symbol seen for the first time: the order-flow set
            int fs; anView.FvgSide = int.TryParse(Get(AnKey(anSym, "FVGSIDE"), "0"), NumberStyles.Integer, IC, out fs) ? fs : 0;
        }
        void AnSet(string key, bool on)
        {
            foreach (var t in ChartView.Indicators) Put(AnKey(anSym, t.Key), (t.Key == key ? on : anView.Ind.Contains(t.Key)) ? "1" : "0");   // the whole set is saved: this symbol's own from now on
            if (on) anView.Ind.Add(key); else anView.Ind.Remove(key); anView.InvalidateVisual(); PaintAnBar();
        }
        List<Mi> AnIndMenu()
        {
            var l = new List<Mi> { Mi.H("analysis indicators of " + anSym + " • tick several") };
            foreach (var t in ChartView.Indicators)
            {
                var key = t.Key; if (key == "SESS_SYD") { l.Add(Mi.S()); l.Add(Mi.H("market sessions")); } else if (key == "VOL") { l.Add(Mi.S()); l.Add(Mi.H("volume & order flow (estimated)")); } else if (key == "SETUPS") { l.Add(Mi.S()); l.Add(Mi.H("signals")); }
                l.Add(new Mi { Text = t.Name, Check = anView.Ind.Contains(key), Dot = Sx.B(t.Color), KeepOpen = true, Do = () => AnSet(key, !anView.Ind.Contains(key)) });
                if (key == "FVG") foreach (var o in new[] { Tuple.Create(0, "      FVG: bullish + bearish"), Tuple.Create(1, "      FVG: bullish only"), Tuple.Create(-1, "      FVG: bearish only") })
                    { var oo = o; l.Add(new Mi { Text = o.Item2, Check = anView.FvgSide == o.Item1, Radio = true, Do = () => { anView.FvgSide = oo.Item1; Put(AnKey(anSym, "FVGSIDE"), oo.Item1.ToString(IC)); if (!anView.Ind.Contains("FVG")) AnSet("FVG", true); anView.InvalidateVisual(); } }); }
            }
            l.Add(Mi.S()); l.Add(new Mi("⟲", "Back to the order-flow set", () => { foreach (var t in ChartView.Indicators) Put(AnKey(anSym, t.Key), AnDefault.Contains(t.Key) ? "1" : "0"); AnLoadInd(); anView.InvalidateVisual(); PaintAnBar(); }));
            return l;
        }
        void PaintAnBar()
        {
            foreach (var t in anTfBtns) { bool on = t.Item1 == anTf; t.Item2.Background = on ? Gold : (Brush)Card; t.Item2.Foreground = on ? Bg : Gold; }
            if (anIndBtn != null) anIndBtn.Content = "📈 INDICATORS" + (anView != null && anView.Ind.Count > 0 ? " (" + anView.Ind.Count + ")" : "") + "  ▾";
        }

        // every refresh while the tab is open: the chart (same feed as the trading chart) + the cards once a second
        void FillAnalysis(Mt5State s)
        {
            if (anView == null) return; var sym = Act != null && Act.Sym != "" ? Act.Sym : panes.Select(p => p.Sym).FirstOrDefault(x => x != "") ?? "";
            if (sym == "") { anHead.Text = "📊 ANALYSIS — open a symbol on the chart first"; return; }
            if (!string.Equals(sym, anSym, StringComparison.OrdinalIgnoreCase)) { anSym = sym; AnLoadInd(); anView.ResetView(); anView.Drawings = DrawingsOf(sym); PaintAnBar(); }
            var se = s.SeriesOf(sym, anTf); var now = DateTime.Now;
            if (!Sim && !se.Loaded && (now - se.AskedAt).TotalSeconds > 3) { se.AskedAt = now; broker.Send("BARS|" + sym + "|" + anTf + "|" + BarsWanted(sym, anTf)); }
            Mt5Quote q; s.Quotes.TryGetValue(sym, out q); Mt5Symbol ms; s.Symbols.TryGetValue(sym, out ms); string zn; var conv = ZoneConv(s, out zn);
            var v = anView; v.Series = se.Loaded ? se : null; v.Status = se.Loaded ? "" : Sim ? BtIdleText() : "loading " + sym + " " + Tf.Name(anTf) + " from MT5 …"; v.Symbol = sym;
            v.Digits = ms != null ? ms.Digits : 2; v.Point = ms != null && ms.Point > 0 ? ms.Point : 0.01; v.TickValue = ms != null && ms.TickValue > 0 ? ms.TickValue : 1; v.TickSize = ms != null && ms.TickSize > 0 ? ms.TickSize : v.Point;
            v.Bid = q != null ? q.Bid : 0; v.Ask = q != null ? q.Ask : 0; v.QuoteTime = q != null ? q.Time : DateTime.MinValue; v.Positions = s.Positions; v.Orders = s.Orders; v.Currency = s.Account.Currency; v.Gate = s.Gate;
            v.ToView = conv; v.ZoneName = zn; v.ServerNow = LiveServerNow(s); v.NyOffsetHours = NyOffset(s); v.ServerOffsetSec = s.ServerOffsetSec; v.TradeOn = false; v.Theme = CurTheme();
            v.InvalidateVisual();
            anHead.Text = "📊 ANALYSIS • " + sym + " • " + Tf.Name(anTf) + (q != null ? "   " + q.Bid.ToString("F" + v.Digits, IC) : "");
            if ((now - anAt).TotalSeconds < 1 || !se.Loaded) return; anAt = now;
            BuildAnCards(se.Bars, v, s);
        }

        void BuildAnCards(List<Mt5Bar> bars, ChartView v, Mt5State s)
        {
            int n = bars.Count; if (n < 30) { anCards.Children.Clear(); anCards.Children.Add(AnCard("⏳", "WAITING", "not enough candles yet", Muted, null)); return; }
            string f = "F" + v.Digits; double px = v.Bid > 0 ? v.Bid : bars[n - 1].C; var last = bars[n - 1];
            Func<int, double> ema = len => { double k = 2.0 / (len + 1), e = bars[Math.Max(0, n - len * 4)].C; for (int i = Math.Max(0, n - len * 4) + 1; i < n; i++) e = bars[i].C * k + e * (1 - k); return e; };
            double e21 = ema(21), e50 = ema(50);
            int trend = px > e21 && e21 > e50 ? 1 : px < e21 && e21 < e50 ? -1 : 0;
            // the day's VWAP (from the server's midnight)
            var day = last.Time.Date; int d0 = n - 1; while (d0 > 0 && bars[d0 - 1].Time.Date == day) d0--;
            double pv = 0, vv = 0; for (int i = d0; i < n; i++) { double w = Math.Max(1, bars[i].V); pv += (bars[i].H + bars[i].L + bars[i].C) / 3 * w; vv += w; } double vwap = vv > 0 ? pv / vv : px;
            int vw = px > vwap ? 1 : px < vwap ? -1 : 0;
            // today's value area
            var va = ChartView.ValueArea(bars, d0, n - 1); int loc = 0; string locTxt = "no volume yet";
            if (va != null) { if (px > va[1]) { loc = 1; locTxt = "ABOVE VALUE (VAH " + va[1].ToString(f, IC) + ") — buyers accepted higher prices"; } else if (px < va[2]) { loc = -1; locTxt = "BELOW VALUE (VAL " + va[2].ToString(f, IC) + ") — sellers in control while it holds"; } else locTxt = "INSIDE VALUE (POC " + va[0].ToString(f, IC) + ", VAH " + va[1].ToString(f, IC) + ", VAL " + va[2].ToString(f, IC) + ") — no edge from the location"; }
            // the order flow of the last 20 candles + the last 5
            double b20 = 0, s20 = 0, d5 = 0, cvd = 0, cvdOld = 0; for (int i = n - 20; i < n; i++) { double bs = ChartView.BuyOf(bars[i]), w = Math.Max(1, bars[i].V); b20 += w * bs; s20 += w * (1 - bs); cvd += w * (2 * bs - 1); if (i == n - 11) cvdOld = cvd; if (i >= n - 5) d5 += w * (2 * bs - 1); }
            double share = b20 + s20 > 0 ? b20 / (b20 + s20) : 0.5; int flow = share > 0.55 ? 1 : share < 0.45 ? -1 : 0;
            double move10 = bars[n - 1].C - bars[n - 11].C, cvd10 = cvd - cvdOld; string div = move10 > 0 && cvd10 < 0 ? "⚠ price UP but the delta DOWN — the buyers are weak" : move10 < 0 && cvd10 > 0 ? "⚠ price DOWN but the delta UP — the sellers are weak" : "price and delta agree";
            // the day's gap: today's first candle vs yesterday's last
            string gapTxt = "—"; if (d0 > 0) { double gp = bars[d0].O - bars[d0 - 1].C; double pts = gp / v.Point; gapTxt = (Math.Abs(pts) < 1 ? "no gap" : (gp > 0 ? "▲ GAP UP " : "▼ GAP DOWN ") + Math.Abs(gp).ToString(f, IC) + " (" + Math.Abs(pts).ToString("N0", IC) + " pts)") + " • yesterday's close " + bars[d0 - 1].C.ToString(f, IC) + ((gp > 0 && px <= bars[d0 - 1].C) || (gp < 0 && px >= bars[d0 - 1].C) ? " • FILLED" : Math.Abs(pts) >= 1 ? " • open" : ""); }
            // the verdict
            int score = trend + vw + loc + flow; var reasons = new List<string>();
            reasons.Add((trend > 0 ? "▲" : trend < 0 ? "▼" : "•") + " trend (EMA 21 / 50)"); reasons.Add((vw > 0 ? "▲" : vw < 0 ? "▼" : "•") + " vs the VWAP"); reasons.Add((loc > 0 ? "▲" : loc < 0 ? "▼" : "•") + " the value area"); reasons.Add((flow > 0 ? "▲" : flow < 0 ? "▼" : "•") + " the order flow");
            string verdict = score >= 3 ? "LEAN LONG — strong" : score == 2 ? "LEAN LONG" : score <= -3 ? "LEAN SHORT — strong" : score == -2 ? "LEAN SHORT" : "WAIT — mixed signals";
            var vc = score >= 2 ? Green : score <= -2 ? Red : Gold;
            var sig = verdict + trend + vw + loc + flow + Math.Round(share * 100) + gapTxt + DateTime.Now.Minute; if (sig == anSig && anCards.Children.Count > 0) return; anSig = sig;
            anCards.Children.Clear();
            anCards.Children.Add(AnCard(score >= 2 ? "▲" : score <= -2 ? "▼" : "⏸", verdict, string.Join("   ", reasons) + "\nscore " + score.ToString("+0;−0;0", IC) + " of ±4 • a reading, not a signal: confirm on the chart", vc, Math.Abs(score) / 4.0));
            anCards.Children.Add(AnCard("📈", "TREND", (trend > 0 ? "UP — price over the EMA 21 over the EMA 50" : trend < 0 ? "DOWN — price under the EMA 21 under the EMA 50" : "SIDEWAYS — the averages are mixed") + "\nEMA 21 " + e21.ToString(f, IC) + " • EMA 50 " + e50.ToString(f, IC), trend > 0 ? Green : trend < 0 ? Red : Muted, null));
            anCards.Children.Add(AnCard("≈", "VWAP " + vwap.ToString(f, IC), (vw > 0 ? "price ABOVE the day's VWAP (+" : "price BELOW the day's VWAP (") + ((px - vwap) / v.Point).ToString("N0", IC) + " pts) — " + (vw > 0 ? "the buyers pay up" : "the sellers lead"), vw > 0 ? Green : Red, null));
            anCards.Children.Add(AnCard("📍", "LOCATION", locTxt, loc > 0 ? Green : loc < 0 ? Red : Cyan, null));
            anCards.Children.Add(AnCard("⚔", "ORDER FLOW • last 20 candles", (flow > 0 ? "BUYERS lead • " : flow < 0 ? "SELLERS lead • " : "EVEN • ") + (100 * share).ToString("0", IC) + " % bought • last 5 delta " + (d5 >= 0 ? "+" : "−") + Math.Abs(d5).ToString("N0", IC) + "\n" + div + "\nest. from tick volume (no exchange book on CFDs)", flow > 0 ? Green : flow < 0 ? Red : Gold, share));
            var ses = ChartView.SessionsNow(DateTime.UtcNow); var open = ses.Where(x => x.Item3 == true).Select(x => x.Item1 + " (closes in " + Span(x.Item4) + ")").ToList(); var pre = ses.Where(x => x.Item3 == false).Select(x => x.Item1 + " pre").ToList(); var next = ses.Where(x => x.Item3 == null && x.Item4 != TimeSpan.MaxValue).OrderBy(x => x.Item4).FirstOrDefault();
            anCards.Children.Add(AnCard("🌐", "SESSIONS NOW", (open.Count > 0 ? "OPEN: " + string.Join(", ", open) : "no main session open") + (pre.Count > 0 ? "\n" + string.Join(", ", pre) : "") + (next != null ? "\nnext: " + next.Item1 + " in " + Span(next.Item4) : ""), open.Count > 0 ? Cyan : Muted, null));
            anCards.Children.Add(AnCard("⇅", "TODAY'S GAP", gapTxt, Violet, null));
        }
        static string Span(TimeSpan t) { return t.TotalHours >= 1 ? ((int)t.TotalHours) + " h " + t.Minutes.ToString("00") + " min" : t.Minutes + " min"; }
        Border AnCard(string icon, string title, string text, SolidColorBrush col, double? meter)
        {
            var sp = new StackPanel();
            var h = new StackPanel { Orientation = Orientation.Horizontal }; h.Children.Add(new TextBlock { Text = icon + "  ", Foreground = col, FontSize = 14, FontWeight = FontWeights.Bold }); h.Children.Add(new TextBlock { Text = title, Foreground = col, FontSize = 13.5, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = 290 }); sp.Children.Add(h);
            if (meter.HasValue) { var tr = new Grid { Height = 7, Margin = new Thickness(0, 6, 0, 4) }; tr.Children.Add(new Border { Background = Tint(col, 40), CornerRadius = new CornerRadius(4) }); tr.Children.Add(new Border { Background = col, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Max(4, 320 * Math.Max(0, Math.Min(1, meter.Value))) }); sp.Children.Add(tr); }
            sp.Children.Add(new TextBlock { Text = text, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0), LineHeight = 17 });
            return new Border { Child = sp, Background = Tint(col, 18), BorderBrush = Tint(col, 110), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 9, 12, 10), Margin = new Thickness(0, 0, 0, 8) };
        }
    }
}
