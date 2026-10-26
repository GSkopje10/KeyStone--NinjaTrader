// SpasiX — 📊 TRACKER (live / prop: the account's deals from MT5) and 📊 STATS (the backtest's own deals): a month CALENDAR (every day's net P/L
// after commission and swap, its trades, the week's total), the numbers of the month or of everything (net, win rate, profit factor, average win /
// loss, expectancy, best / worst day, the deepest drawdown, buys vs sells, costs), the EQUITY CURVE, P/L by symbol and by New York hour, the selected
// day's trades and a JOURNAL note per day (saved on this PC).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed class TradeRow { public long Position; public DateTime Open, Close; public string Symbol = "", Side = "", Reason = ""; public double Lots, Entry, Exit, Net, Costs; }

    // the curve / the bars of the tracker, drawn directly
    public sealed class MiniPlot : FrameworkElement
    {
        public List<double> Values = new List<double>(); public List<string> Labels = new List<string>(); public bool Bars; public string Title = "";
        static SolidColorBrush Br(string h, byte a = 255) { var c = (Color)ColorConverter.ConvertFromString(h); var b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); b.Freeze(); return b; }
        protected override void OnRender(DrawingContext dc)
        {
            double W = ActualWidth, H = ActualHeight; if (W < 40 || H < 30) return; double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; var face = new Typeface("Segoe UI");
            dc.DrawRoundedRectangle(Br("#0D131F"), new Pen(Br("#22304A"), 1), new Rect(0, 0, W, H), 10, 10);
            dc.DrawText(new FormattedText(Title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 11, Br("#8590A6"), dpi), new Point(10, 6));
            if (Values.Count == 0) { dc.DrawText(new FormattedText("no trades yet", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 12, Br("#8590A6"), dpi), new Point(W / 2 - 34, H / 2 - 8)); return; }
            double top = 26, bot = H - (Bars ? 20 : 10), left = 10, right = W - 10; double mx = Math.Max(0, Values.Max()), mn = Math.Min(0, Values.Min()); if (mx - mn < 1e-9) { mx += 1; mn -= 1; }
            Func<double, double> Y = v => top + (mx - v) / (mx - mn) * (bot - top);
            dc.DrawLine(new Pen(Br("#22304A"), 1), new Point(left, Y(0)), new Point(right, Y(0)));
            if (Bars)
            {
                double bw = (right - left) / Values.Count;
                for (int i = 0; i < Values.Count; i++)
                {
                    double v = Values[i]; if (v != 0) dc.DrawRoundedRectangle(v >= 0 ? Br("#2EE59D", 200) : Br("#FF4D6D", 200), null, new Rect(left + i * bw + 1.5, Math.Min(Y(v), Y(0)), Math.Max(1, bw - 3), Math.Max(1, Math.Abs(Y(v) - Y(0)))), 2, 2);
                    if (i < Labels.Count && (Values.Count <= 12 || i % 3 == 0)) dc.DrawText(new FormattedText(Labels[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 9.5, Br("#8590A6"), dpi), new Point(left + i * bw + 1, bot + 3));
                }
                return;
            }
            var g = new StreamGeometry(); var area = new StreamGeometry(); double step = Values.Count > 1 ? (right - left) / (Values.Count - 1) : 0;
            using (var c = g.Open()) using (var a = area.Open())
            {
                a.BeginFigure(new Point(left, Y(0)), true, true);
                for (int i = 0; i < Values.Count; i++) { var p = new Point(left + i * step, Y(Values[i])); if (i == 0) c.BeginFigure(p, false, false); else c.LineTo(p, true, true); a.LineTo(p, true, false); }
                a.LineTo(new Point(left + (Values.Count - 1) * step, Y(0)), true, false);
            }
            g.Freeze(); area.Freeze(); bool up = Values[Values.Count - 1] >= 0; var col = up ? "#2EE59D" : "#FF4D6D";
            dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(70, up ? (byte)0x2E : (byte)0xFF, up ? (byte)0xE5 : (byte)0x4D, up ? (byte)0x9D : (byte)0x6D), Color.FromArgb(0, 0, 0, 0), 90), null, area);
            dc.DrawGeometry(null, new Pen(Br(col), 2), g);
            var last = new FormattedText((Values[Values.Count - 1] >= 0 ? "+" : "−") + "$" + Math.Abs(Values[Values.Count - 1]).ToString("N2", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 12, Br(col), dpi);
            dc.DrawText(last, new Point(right - last.Width, 6));
        }
    }

    public sealed partial class MainWindow
    {
        DateTime trackMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), trackDay = DateTime.MinValue; bool trackAll; string trackSig = ""; DateTime dealsAskedAt = DateTime.MinValue;
        TextBlock standClosed, standOpen, standIf, standNote; Border standBox; double standRangeNet;
        UniformGridX calGrid; TextBlock trackHead, trackMonthText, trackDayHead; WrapPanel trackCards; MiniPlot curve, byHour, byDow; StackPanel bySym, dayRows; TextBox journal; Button trackAllBtn; string journalKey = "";
        sealed class UniformGridX { public readonly System.Windows.Controls.Primitives.UniformGrid G = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8 }; }

        FrameworkElement BuildTracker()
        {
            var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var top = new DockPanel { Margin = new Thickness(2, 0, 2, 8) };
            var right = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(right, Dock.Right);
            var prev = Pill("◀", Card); prev.Click += delegate { trackMonth = trackMonth.AddMonths(-1); trackAll = false; trackSig = ""; if (!Virtual) RequestDeals(true); };
            trackMonthText = new TextBlock { Foreground = Gold, FontSize = 15, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0), MinWidth = 120, TextAlignment = TextAlignment.Center };
            var next = Pill("▶", Card); next.Click += delegate { trackMonth = trackMonth.AddMonths(1); trackAll = false; trackSig = ""; };
            trackAllBtn = Pill("ALL", Card); trackAllBtn.ToolTip = "the numbers of every loaded day (not only this month)"; trackAllBtn.Click += delegate { trackAll = !trackAll; trackSig = ""; };
            right.Children.Add(prev); right.Children.Add(trackMonthText); right.Children.Add(next); right.Children.Add(trackAllBtn);
            if (!Virtual) { var rl = Pill("↻ RELOAD", Card); rl.Click += delegate { RequestDeals(true); }; right.Children.Add(rl); }
            top.Children.Add(right);
            trackHead = new TextBlock { Foreground = Text, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }; top.Children.Add(trackHead);
            root.Children.Add(top);
            var body = new Grid(); Grid.SetRow(body, 1); root.Children.Add(body);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // left: the calendar + the day
            var left = new StackPanel();
            var hdr = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var d in new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN", "WEEK" }) hdr.Children.Add(new TextBlock { Text = d, Foreground = d == "WEEK" ? Gold : Muted, FontSize = 10.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            left.Children.Add(hdr); calGrid = new UniformGridX(); left.Children.Add(calGrid.G);
            trackDayHead = new TextBlock { Foreground = Gold, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 14, 0, 6) }; left.Children.Add(trackDayHead);
            dayRows = new StackPanel(); left.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8, 12, 8), BorderBrush = Line, BorderThickness = new Thickness(1), Child = dayRows });
            left.Children.Add(new TextBlock { Text = "📝 JOURNAL — what you saw, what you did, what to do better (saved on this PC)", Foreground = Muted, FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 10, 0, 4) });
            journal = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Background = Card, Foreground = Text, BorderBrush = Line, CaretBrush = Gold, FontSize = 13, Padding = new Thickness(8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            journal.LostFocus += delegate { SaveJournal(); }; left.Children.Add(journal);
            var lsv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = left }; body.Children.Add(lsv);
            // right: the numbers, the curve, by symbol / hour / weekday
            var rsp = new StackPanel();
            // WHERE WE STAND: the closed net + the open P/L now = what we would have if everything were closed this second (live)
            var stand = new StackPanel { Orientation = Orientation.Horizontal };
            Func<string, TextBlock> cell = cap => { var v = new TextBlock { FontSize = 19, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono, Foreground = Text }; var st = new StackPanel { Margin = new Thickness(0, 0, 22, 0) }; st.Children.Add(new TextBlock { Text = cap, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold }); st.Children.Add(v); stand.Children.Add(st); return v; };
            standClosed = cell("CLOSED (this view)"); standOpen = cell("OPEN P/L NOW"); standIf = cell("IF ALL CLOSED NOW"); standNote = new TextBlock { Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 3) }; stand.Children.Add(standNote);
            standBox = new Border { Child = stand, Background = Card, BorderBrush = Gold, BorderThickness = new Thickness(1.8), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 8, 14, 9), Margin = new Thickness(0, 0, 0, 8), ToolTip = "the closed net of the shown period + the open P/L of every position now: where we stand if we closed everything this second" };
            rsp.Children.Add(standBox);
            trackCards = new WrapPanel(); rsp.Children.Add(trackCards);
            curve = new MiniPlot { Height = 190, Title = "EQUITY CURVE (closed trades, after costs)", Margin = new Thickness(0, 6, 0, 8) }; rsp.Children.Add(curve);
            byHour = new MiniPlot { Height = 130, Bars = true, Title = "P/L BY NEW YORK HOUR (entry)", Margin = new Thickness(0, 0, 0, 8) }; rsp.Children.Add(byHour);
            byDow = new MiniPlot { Height = 110, Bars = true, Title = "P/L BY WEEKDAY", Margin = new Thickness(0, 0, 0, 8) }; rsp.Children.Add(byDow);
            rsp.Children.Add(new TextBlock { Text = "BY SYMBOL", Foreground = Muted, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 2, 0, 4) });
            bySym = new StackPanel(); rsp.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8, 12, 8), BorderBrush = Line, BorderThickness = new Thickness(1), Child = bySym });
            var rsv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rsp }; Grid.SetColumn(rsv, 2); body.Children.Add(rsv);
            return root;
        }

        // the account's deals from MT5 (bridge v4): the last 3 months, again every minute
        void RequestDeals(bool force)
        {
            if (Virtual) return; var s = live.State; if (s.BridgeVersion < 4 || !s.Alive) return;
            if (!force && (DateTime.Now - dealsAskedAt).TotalSeconds < 60) return; dealsAskedAt = DateTime.Now;
            var now = s.ServerNow > DateTime.MinValue ? s.ServerNow : DateTime.Now; var from = new DateTime(Math.Min(trackMonth.Ticks, new DateTime(now.Year, now.Month, 1).AddMonths(-3).Ticks));
            live.Send("DEALS|" + Mt5State.Epoch(from) + "|" + Mt5State.Epoch(now.Date.AddDays(2)));
        }
        static List<TradeRow> Trades(List<Mt5Deal> deals)
        {
            var ins = new Dictionary<long, Mt5Deal>(); foreach (var d in deals) if (d.IsTrade && d.Entry == "IN" && !ins.ContainsKey(d.Position)) ins[d.Position] = d;
            var res = new List<TradeRow>();
            foreach (var d in deals.Where(x => x.IsExit))
            {
                Mt5Deal i; ins.TryGetValue(d.Position, out i); double inComm = i != null && i.Lots > 0 ? i.Commission * Math.Min(1, d.Lots / i.Lots) : 0;
                res.Add(new TradeRow { Position = d.Position, Open = i != null ? i.Time : d.Time, Close = d.Time, Symbol = d.Symbol, Side = i != null ? i.Type : (d.Type == "BUY" ? "SELL" : "BUY"), Lots = d.Lots, Entry = i != null ? i.Price : 0, Exit = d.Price, Net = d.Net + inComm, Costs = d.Commission + d.Swap + inComm, Reason = d.Reason });
            }
            return res.OrderBy(t => t.Close).ToList();
        }
        double TodayClosed(Mt5State s)
        {
            var day = (s.ServerNow > DateTime.MinValue ? s.ServerNow : DateTime.Now).Date; double sum = 0;
            foreach (var d in s.Deals) if (d.IsTrade && d.Time.Date == day) sum += d.Net;
            return sum;
        }
        void FillTracker(Mt5State s)
        {
            int nyOff = NyOffset(s);
            string sig = s.DealsVersion + "|" + s.Deals.Count + "|" + trackMonth.Ticks + "|" + trackDay.Ticks + "|" + trackAll + "|" + (s.ServerNow.Date.Ticks);
            if (standBox != null)
            {
                string c0 = s.Account.Currency; double open = s.Positions.Sum(x => x.Profit + x.Swap), ifc = standRangeNet + open;
                standClosed.Text = Signed(standRangeNet, c0); standClosed.Foreground = standRangeNet >= 0 ? Green : Red;
                standOpen.Text = Signed(open, c0); standOpen.Foreground = open >= 0 ? Green : Red;
                standIf.Text = Signed(ifc, c0); standIf.Foreground = ifc >= 0 ? Green : Red; standBox.BorderBrush = ifc >= 0 ? Green : Red;
                standNote.Text = s.Positions.Count + " open position" + (s.Positions.Count == 1 ? "" : "s");
            }
            if (sig == trackSig) return; trackSig = sig;
            var all = Trades(s.Deals); string cur = s.Account.Currency;
            trackMonthText.Text = trackMonth.ToString("MMMM yyyy", IC).ToUpperInvariant(); trackAllBtn.Background = trackAll ? Tint(Gold, 190) : (Brush)Card; trackAllBtn.Foreground = trackAll ? Bg : Text;
            var inMonth = all.Where(t => t.Close.Year == trackMonth.Year && t.Close.Month == trackMonth.Month).ToList(); var range = trackAll ? all : inMonth;
            if (!Virtual && s.BridgeVersion < 4) trackHead.Text = "📊 the TRACKER needs the SpasiXBridge v4 in MT5 (⚙ SETUP step 1)";
            else trackHead.Text = (Sim ? "📊 BACKTEST STATS • " : Paper ? "📊 DEMO TRACKER • " : "📊 TRACKER • ") + (trackAll ? "every loaded day" : trackMonth.ToString("MMMM yyyy", IC)) + " • " + range.Count + " trade" + (range.Count == 1 ? "" : "s") + " • net " + Signed(range.Sum(t => t.Net), cur) + (!Virtual && s.DealsAt > DateTime.MinValue ? " • from MT5 at " + s.DealsAt.ToString("HH:mm:ss", IC) : !Virtual ? " • asking MT5 …" : "");
            // the calendar
            var byDay = all.GroupBy(t => t.Close.Date).ToDictionary(g => g.Key, g => g.ToList());
            calGrid.G.Children.Clear(); var first = trackMonth; int lead = ((int)first.DayOfWeek + 6) % 7; var start = first.AddDays(-lead);
            double mxAbs = Math.Max(1, inMonth.GroupBy(t => t.Close.Date).Select(g => Math.Abs(g.Sum(t => t.Net))).DefaultIfEmpty(1).Max()); var today = (s.ServerNow > DateTime.MinValue ? s.ServerNow : DateTime.Now).Date;
            for (int w = 0; w < 6; w++)
            {
                var wkStart = start.AddDays(7 * w); if (wkStart > first.AddMonths(1).AddDays(-1)) break; double wk = 0; int wkN = 0;
                for (int d = 0; d < 7; d++)
                {
                    var day = wkStart.AddDays(d); bool inM = day.Month == trackMonth.Month; List<TradeRow> l; byDay.TryGetValue(day, out l); double net = l != null ? l.Sum(t => t.Net) : 0; int n = l != null ? l.Count : 0; if (inM) { wk += net; wkN += n; }
                    var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = day.Day.ToString(IC), Foreground = inM ? Text : Muted, FontSize = 11, FontWeight = FontWeights.Bold });
                    if (n > 0) { sp.Children.Add(new TextBlock { Text = Signed(net, cur), Foreground = net >= 0 ? Green : Red, FontSize = 11.5, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono, TextTrimming = TextTrimming.CharacterEllipsis }); sp.Children.Add(new TextBlock { Text = n + " trade" + (n == 1 ? "" : "s"), Foreground = Muted, FontSize = 9.5 }); }
                    byte alpha = n > 0 ? (byte)Math.Min(150, 30 + 120 * Math.Abs(net) / mxAbs) : (byte)0;
                    var cell = new Border { Height = 64, Margin = new Thickness(2), CornerRadius = new CornerRadius(9), Padding = new Thickness(7, 5, 5, 4), Child = sp, Cursor = System.Windows.Input.Cursors.Hand, Opacity = inM ? 1 : 0.45,
                        Background = n > 0 ? Tint(net >= 0 ? Green : Red, alpha) : Card, BorderBrush = day == trackDay ? Gold : day == today ? Cyan : Line, BorderThickness = new Thickness(day == trackDay || day == today ? 1.8 : 1) };
                    var dday = day; cell.MouseLeftButtonUp += delegate { trackDay = dday; trackSig = ""; }; calGrid.G.Children.Add(cell);
                }
                var ws = new StackPanel(); ws.Children.Add(new TextBlock { Text = "week", Foreground = Gold, FontSize = 10, FontWeight = FontWeights.Bold }); if (wkN > 0) { ws.Children.Add(new TextBlock { Text = Signed(wk, cur), Foreground = wk >= 0 ? Green : Red, FontSize = 11.5, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono }); ws.Children.Add(new TextBlock { Text = wkN + " trades", Foreground = Muted, FontSize = 9.5 }); }
                calGrid.G.Children.Add(new Border { Height = 64, Margin = new Thickness(2), CornerRadius = new CornerRadius(9), Padding = new Thickness(7, 5, 5, 4), Child = ws, Background = Tint(Gold, 22), BorderBrush = Tint(Gold, 90), BorderThickness = new Thickness(1) });
            }
            // the numbers
            trackCards.Children.Clear();
            var wins = range.Where(t => t.Net > 0).ToList(); var loss = range.Where(t => t.Net < 0).ToList(); double gw = wins.Sum(t => t.Net), gl = -loss.Sum(t => t.Net);
            var days = range.GroupBy(t => t.Close.Date).Select(g => Tuple.Create(g.Key, g.Sum(t => t.Net))).ToList();
            double peak = 0, eq = 0, dd = 0; var pts = new List<double>(); foreach (var t in range) { eq += t.Net; pts.Add(eq); peak = Math.Max(peak, eq); dd = Math.Max(dd, peak - eq); }
            Action<string, string, SolidColorBrush, string> stat = (k, v, col, tip) => { var sp = new StackPanel(); sp.Children.Add(new TextBlock { Text = k, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold }); sp.Children.Add(new TextBlock { Text = v, Foreground = col, FontSize = 16, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono }); trackCards.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(11, 6, 12, 7), Margin = new Thickness(0, 0, 6, 6), MinWidth = 128, BorderBrush = Line, BorderThickness = new Thickness(1), Child = sp, ToolTip = tip }); };
            double net0 = range.Sum(t => t.Net); standRangeNet = net0;
            stat("NET", Signed(net0, cur), net0 >= 0 ? Green : Red, "after commission and swap");
            stat("TRADES", range.Count.ToString(IC), Text, wins.Count + " won • " + loss.Count + " lost");
            stat("WIN RATE", range.Count > 0 ? (100.0 * wins.Count / range.Count).ToString("0", IC) + " %" : "—", Text, "");
            stat("PROFIT FACTOR", gl > 0 ? (gw / gl).ToString("0.00", IC) : gw > 0 ? "∞" : "—", gl > 0 && gw / gl >= 1 ? Green : Red, "won ÷ lost");
            stat("AVG WIN", wins.Count > 0 ? Money(gw / wins.Count, cur) : "—", Green, ""); stat("AVG LOSS", loss.Count > 0 ? Money(-gl / loss.Count, cur) : "—", Red, "");
            stat("EXPECTANCY", range.Count > 0 ? Signed(net0 / range.Count, cur) : "—", net0 >= 0 ? Green : Red, "the average trade");
            stat("BEST DAY", days.Count > 0 ? Signed(days.Max(x => x.Item2), cur) : "—", Green, days.Count > 0 ? days.OrderByDescending(x => x.Item2).First().Item1.ToString("ddd dd MMM", IC) : "");
            stat("WORST DAY", days.Count > 0 ? Signed(days.Min(x => x.Item2), cur) : "—", Red, days.Count > 0 ? days.OrderBy(x => x.Item2).First().Item1.ToString("ddd dd MMM", IC) : "");
            stat("DEEPEST DRAWDOWN", Money(-dd, cur), dd > 0 ? Red : Text, "the biggest fall of the closed equity from a high");
            stat("GREEN DAYS", days.Count > 0 ? days.Count(x => x.Item2 > 0) + " / " + days.Count : "—", Text, "");
            stat("BUYS / SELLS", Signed(range.Where(t => t.Side == "BUY").Sum(t => t.Net), cur) + " / " + Signed(range.Where(t => t.Side == "SELL").Sum(t => t.Net), cur), Text, range.Count(t => t.Side == "BUY") + " buys • " + range.Count(t => t.Side == "SELL") + " sells");
            stat("COSTS", Money(range.Sum(t => t.Costs), cur), Muted, "commission + swap paid");
            curve.Values = pts; curve.InvalidateVisual();
            var hv = new double[24]; foreach (var t in range) { int h = t.Open.AddHours(nyOff).Hour; hv[h] += t.Net; } byHour.Values = hv.ToList(); byHour.Labels = Enumerable.Range(0, 24).Select(h => h.ToString("00", IC)).ToList(); byHour.InvalidateVisual();
            var dv = new double[7]; foreach (var t in range) dv[((int)t.Close.DayOfWeek + 6) % 7] += t.Net; byDow.Values = dv.ToList(); byDow.Labels = new List<string> { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" }; byDow.InvalidateVisual();
            bySym.Children.Clear();
            foreach (var g in range.GroupBy(t => t.Symbol).OrderByDescending(g => g.Sum(t => t.Net)))
            {
                var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) }; double gn = g.Sum(t => t.Net);
                r.Children.Add(Cell(g.Key, Cyan, 100, false, true)); r.Children.Add(Cell(g.Count() + " trades", Text, 90)); r.Children.Add(Cell((100.0 * g.Count(t => t.Net > 0) / g.Count()).ToString("0", IC) + " % won", Text, 90)); r.Children.Add(Cell(g.Sum(t => t.Lots).ToString("0.00", IC) + " lots", Muted, 90)); r.Children.Add(Cell(Signed(gn, cur), gn >= 0 ? Green : Red, 110, true, true));
                bySym.Children.Add(r);
            }
            if (range.Count == 0) bySym.Children.Add(new TextBlock { Text = "no trades in this range", Foreground = Muted, FontSize = 12 });
            // the selected day
            var sel = trackDay == DateTime.MinValue ? (byDay.Keys.Where(k => k.Year == trackMonth.Year && k.Month == trackMonth.Month).DefaultIfEmpty(today).Max()) : trackDay;
            List<TradeRow> dl0; byDay.TryGetValue(sel, out dl0); dl0 = dl0 ?? new List<TradeRow>(); double dn = dl0.Sum(t => t.Net);
            trackDayHead.Text = "📅 " + sel.ToString("dddd dd MMMM yyyy", IC).ToUpperInvariant() + (dl0.Count > 0 ? "  •  " + dl0.Count + " trades  •  " + Signed(dn, cur) : "  •  no trades");
            trackDayHead.Foreground = dl0.Count == 0 ? Muted : dn >= 0 ? Green : Red;
            dayRows.Children.Clear();
            if (dl0.Count > 0)
            {
                var h = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
                foreach (var c in new[] { Tuple.Create("OPEN → CLOSE (NY)", 140.0), Tuple.Create("SYMBOL", 80.0), Tuple.Create("SIDE", 46.0), Tuple.Create("LOTS", 46.0), Tuple.Create("ENTRY", 80.0), Tuple.Create("EXIT", 80.0), Tuple.Create("WHY", 56.0), Tuple.Create("NET", 90.0) }) h.Children.Add(Cell(c.Item1, Muted, c.Item2, c.Item1 == "NET"));
                dayRows.Children.Add(h);
                foreach (var t in dl0)
                {
                    var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) }; var dg = t.Entry > 10 ? "F2" : "F5";
                    r.Children.Add(Cell(t.Open.AddHours(nyOff).ToString("HH:mm", IC) + " → " + t.Close.AddHours(nyOff).ToString("HH:mm", IC), Text, 140)); r.Children.Add(Cell(t.Symbol, Cyan, 80)); r.Children.Add(Cell(t.Side, t.Side == "BUY" ? Green : Red, 46, false, true));
                    r.Children.Add(Cell(t.Lots.ToString("0.00", IC), Text, 46)); r.Children.Add(Cell(t.Entry.ToString(dg, IC), Text, 80)); r.Children.Add(Cell(t.Exit.ToString(dg, IC), Text, 80)); r.Children.Add(Cell(t.Reason == "" ? "—" : t.Reason, t.Reason == "TP" ? Green : t.Reason == "SL" || t.Reason == "STOPOUT" ? Red : Muted, 56)); r.Children.Add(Cell(Signed(t.Net, cur), t.Net >= 0 ? Green : Red, 90, true, true));
                    dayRows.Children.Add(r);
                }
            }
            else dayRows.Children.Add(new TextBlock { Text = "click a day in the calendar", Foreground = Muted, FontSize = 12 });
            var jk = (Sim ? "bt" : Paper ? "demo" : s.Account.Login.ToString(IC)) + "|" + sel.ToString("yyyy-MM-dd", IC);
            if (jk != journalKey) { SaveJournal(); journalKey = jk; journal.Text = JournalGet(jk); }
        }
        static string JournalFile { get { return Path.Combine(Dir, "journal.txt"); } }
        Dictionary<string, string> journalMap;
        string JournalGet(string key)
        {
            if (journalMap == null) { journalMap = new Dictionary<string, string>(); try { if (File.Exists(JournalFile)) foreach (var l in File.ReadAllLines(JournalFile)) { int i = l.IndexOf('='); if (i > 0) journalMap[l.Substring(0, i)] = l.Substring(i + 1).Replace("\\n", "\n"); } } catch { } }
            string v; return journalMap.TryGetValue(key, out v) ? v : "";
        }
        void SaveJournal()
        {
            if (journalKey == "" || journal == null) return; JournalGet(journalKey); var txt = journal.Text ?? ""; string old; journalMap.TryGetValue(journalKey, out old); if ((old ?? "") == txt) return;
            if (txt.Trim() == "") journalMap.Remove(journalKey); else journalMap[journalKey] = txt;
            try { Directory.CreateDirectory(Dir); File.WriteAllLines(JournalFile, journalMap.Select(kv => kv.Key + "=" + kv.Value.Replace("\r", "").Replace("\n", "\\n"))); } catch { }
        }
    }
}
