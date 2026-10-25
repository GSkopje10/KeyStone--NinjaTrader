// SpasiX — 💼 POSITIONS: one card per symbol (buys and sells side by side — hedging), every ticket with its own buttons (✕ close, ½ close,
// ⚖ breakeven, S/L and T/P boxes + SET), the bulk buttons on top (close all / buys / sells / profit / loss, breakeven, cancel the orders,
// ⛔ close everything) and the pending orders (✕ cancel). Everything goes through the same armed commands as the chart.
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
        StackPanel posPanel; TextBlock posSummary; string posSig = "";
        readonly Dictionary<long, TextBlock[]> posCells = new Dictionary<long, TextBlock[]>(); readonly Dictionary<string, TextBlock> posHeads = new Dictionary<string, TextBlock>();

        FrameworkElement BuildPositions()
        {
            var sp = new StackPanel();
            var bulk = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            // the only button for every instrument at once (it asks first and lists them); everything else is per instrument, on its own card
            var all = Pill("⛔ CLOSE EVERYTHING • every instrument, every position", Tint(Red, 210)); all.Foreground = Sx.White; all.Height = 34; all.ToolTip = "closes every open position on every instrument (asks first, with the list)"; all.Click += delegate { CloseAll("", "ALL"); }; bulk.Children.Add(all);
            bulk.Children.Add(new TextBlock { Text = "each instrument has its own buttons on its card ▸ they never touch another instrument", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            sp.Children.Add(bulk);
            posSummary = new TextBlock { Text = "no positions", Foreground = Muted, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 0, 8), TextWrapping = TextWrapping.Wrap }; sp.Children.Add(posSummary);
            posPanel = new StackPanel(); sp.Children.Add(posPanel);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        // one instrument's buttons (positions tab): close all / buys / sells / in profit / in loss, breakeven, cancel its orders — never another instrument
        FrameworkElement SymControls(string sym)
        {
            var box = new Border { BorderBrush = Tint(Cyan, 90), BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 10), Margin = new Thickness(18, 34, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 430, Background = Tint(Cyan, 12) };
            var sp = new StackPanel(); box.Child = sp;
            sp.Children.Add(new TextBlock { Text = "🧹 " + sym + " ONLY", Foreground = Cyan, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            var g = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 }; sp.Children.Add(g);
            Func<string, SolidColorBrush, string, Action, Button> add = (t, col, tip, act) => { var b = Pill(t, Tint(col, 34)); b.Foreground = col; b.BorderBrush = Tint(col, 140); b.Height = 32; b.FontSize = 11.5; b.Margin = new Thickness(0, 0, 6, 6); b.ToolTip = tip; b.Click += delegate { act(); }; g.Children.Add(b); return b; };
            add("✕ CLOSE ALL " + sym, Red, "close every position on " + sym + " (asks first)", () => CloseAll(sym, "ALL"));
            add("✕ BUYS", Green, "close the buys on " + sym, () => CloseAll(sym, "BUY"));
            add("✕ SELLS", Red, "close the sells on " + sym, () => CloseAll(sym, "SELL"));
            add("✕ IN PROFIT", Green, "close the " + sym + " positions in profit", () => CloseAll(sym, "PROFIT"));
            add("✕ IN LOSS", Red, "close the " + sym + " positions in loss", () => CloseAll(sym, "LOSS"));
            add("⚖ BREAKEVEN IN PROFIT", Gold, "the stop of every " + sym + " position in profit to its entry (+ the panel's points / $)", () => BreakevenAll(sym));
            add("⏳ CANCEL " + sym + " ORDERS", Amber, "cancel the pending orders on " + sym, () => { if (Ask("Cancel every pending order on " + sym + "?")) Cmd("cancel the orders on " + sym, "CANCELALL", sym); });
            return box;
        }
        static TextBlock Cell(string t, Brush fg, double w, bool right = false, bool bold = false) { return new TextBlock { Text = t, Foreground = fg, Width = w, FontSize = 12.5, FontFamily = new FontFamily("Consolas"), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextAlignment = right ? TextAlignment.Right : TextAlignment.Left, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center }; }
        Button Mini(string t, Brush fg, string tip) { var b = Pill(t, Card); b.Height = 24; b.FontSize = 11; b.Padding = new Thickness(7, 0, 7, 0); b.Margin = new Thickness(0, 0, 4, 0); b.Foreground = fg; b.ToolTip = tip; return b; }
        TextBox Small(string t) { return new TextBox { Text = t, Width = 78, Height = 24, FontSize = 12, FontFamily = new FontFamily("Consolas"), Background = Bg, Foreground = Text, BorderBrush = Line, CaretBrush = Gold, Margin = new Thickness(0, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center }; }

        void FillPositions(Mt5State s)
        {
            var acc = s.Account; string cur = acc.Currency; var pos = s.Positions; var ords = s.Orders;
            double pl = pos.Sum(p => p.Profit + p.Swap);
            posSummary.Text = pos.Count == 0 && ords.Count == 0 ? "no open positions or orders" : pos.Count + " open ticket" + (pos.Count == 1 ? "" : "s") + " • " + pos.Where(p => p.Dir > 0).Sum(p => p.Lots).ToString("0.00", IC) + " lots bought • " + pos.Where(p => p.Dir < 0).Sum(p => p.Lots).ToString("0.00", IC) + " lots sold • open P/L " + Signed(pl, cur) + (ords.Count > 0 ? " • " + ords.Count + " pending order" + (ords.Count == 1 ? "" : "s") : "") + (armed || Virtual ? "" : "   •   🔒 ARM in the 📈 CHART panel to manage them from here");
            posSummary.Foreground = pos.Count == 0 ? Muted : pl >= 0 ? Green : Red;
            string sig = string.Join(",", pos.Select(p => p.Ticket)) + "|" + string.Join(",", ords.Select(o => o.Ticket + ":" + o.Price));
            if (sig != posSig)
            {
                posSig = sig; posPanel.Children.Clear(); posCells.Clear(); posHeads.Clear();
                foreach (var g in pos.GroupBy(p => p.Symbol).OrderBy(x => x.Key))
                {
                    var sym = g.Key; var card = new StackPanel();
                    var hdrRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                    var hb = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(hb, Dock.Right);
                    var chart = Mini("📈 CHART", Cyan, "open its chart"); chart.Click += delegate { SetPane(Act, sym, Act.Tf); Show("CHART"); }; hb.Children.Add(chart);
                    hdrRow.Children.Add(hb);
                    var hdr = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }; hdrRow.Children.Add(hdr); posHeads[sym] = hdr;
                    card.Children.Add(hdrRow);
                    var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
                    foreach (var h in new[] { Tuple.Create("", 62.0), Tuple.Create("TICKET", 100.0), Tuple.Create("SIDE", 50.0), Tuple.Create("LOTS", 50.0), Tuple.Create("OPEN", 86.0), Tuple.Create("NOW", 86.0), Tuple.Create("SWAP", 70.0), Tuple.Create("P/L", 96.0), Tuple.Create("OPENED", 120.0), Tuple.Create("S/L        T/P", 250.0) })
                        head.Children.Add(Cell(h.Item1, Muted, h.Item2, h.Item1 == "P/L" || h.Item1 == "SWAP"));
                    card.Children.Add(head);
                    foreach (var p in g.OrderBy(x => x.Time))
                    {
                        var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) }; long tk = p.Ticket; var p0 = p;
                        var bx = Mini("✕", Red, "close this ticket"); bx.Click += delegate { Cmd("close #" + tk, "CLOSE", tk, 0.0); };
                        var bh = Mini("½", Text, "close half of it"); bh.Click += delegate { Cmd("close half of #" + tk, "CLOSE", tk, Math.Round(p0.Lots / 2, 2)); };
                        var bxw = new StackPanel { Orientation = Orientation.Horizontal, Width = 62, Margin = new Thickness(0, 0, 10, 0) }; bxw.Children.Add(bx); bxw.Children.Add(bh); r.Children.Add(bxw);
                        var cells = new[] { Cell("", Muted, 100), Cell("", Text, 50, false, true), Cell("", Text, 50), Cell("", Text, 86), Cell("", Cyan, 86), Cell("", Muted, 70, true), Cell("", Text, 96, true, true), Cell("", Muted, 120) };
                        foreach (var c in cells) r.Children.Add(c); posCells[tk] = cells;
                        var slB = Small(p.Sl > 0 ? p.Sl.ToString("0.#####", IC) : ""); var tpB = Small(p.Tp > 0 ? p.Tp.ToString("0.#####", IC) : ""); slB.ToolTip = "stop (price, empty = none)"; tpB.ToolTip = "target (price, empty = none)";
                        var set = Mini("SET", Gold, "send this stop / target"); set.Click += delegate { double sl, tp; double.TryParse(slB.Text.Replace(',', '.'), NumberStyles.Float, IC, out sl); double.TryParse(tpB.Text.Replace(',', '.'), NumberStyles.Float, IC, out tp); Cmd("S/L T/P on #" + tk, "MODIFY", tk, sl, tp); };
                        var bet = Mini("⚖", Gold, "breakeven: the stop to the entry (+ the panel's points / $)"); bet.Click += delegate { Cmd("breakeven #" + tk, "MODIFY", tk, BePrice(p0), p0.Tp); };
                        r.Children.Add(slB); r.Children.Add(tpB); r.Children.Add(set); r.Children.Add(bet);
                        card.Children.Add(r);
                    }
                    // this instrument's controls, to the right of its tickets: they act on this instrument only (each asks first)
                    var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.Children.Add(card); var ctl = SymControls(sym); Grid.SetColumn(ctl, 1); row.Children.Add(ctl);
                    posPanel.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), BorderBrush = Line, BorderThickness = new Thickness(1), Child = row });
                }
                if (ords.Count > 0)
                {
                    var card = new StackPanel(); card.Children.Add(new TextBlock { Text = "⏳ PENDING ORDERS", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Amber, Margin = new Thickness(0, 0, 0, 6) });
                    foreach (var o in ords)
                    {
                        var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) }; var sym = s.Symbols.ContainsKey(o.Symbol) ? s.Symbols[o.Symbol] : null; string f = "F" + (sym != null ? sym.Digits : 2); long tk = o.Ticket;
                        var bx = Mini("✕", Red, "cancel this order"); bx.Click += delegate { Cmd("cancel order #" + tk, "CANCEL", tk); }; r.Children.Add(bx);
                        r.Children.Add(Cell(o.Ticket.ToString(IC), Muted, 100)); r.Children.Add(Cell(o.Symbol, Text, 90, false, true)); r.Children.Add(Cell(o.Type, Amber, 120, false, true));
                        r.Children.Add(Cell(o.Lots.ToString("0.00", IC) + " lots", Text, 90)); r.Children.Add(Cell("@ " + o.Price.ToString(f, IC), Text, 110)); r.Children.Add(Cell(o.Sl > 0 ? "S/L " + o.Sl.ToString(f, IC) : "", Red, 110)); r.Children.Add(Cell(o.Tp > 0 ? "T/P " + o.Tp.ToString(f, IC) : "", Green, 110));
                        card.Children.Add(r);
                    }
                    posPanel.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), BorderBrush = Tint(Amber, 110), BorderThickness = new Thickness(1), Child = card });
                }
            }
            // the numbers, every refresh
            foreach (var kv in posHeads)
            {
                var ps = pos.Where(p => p.Symbol == kv.Key).ToList(); double gpl = ps.Sum(p => p.Profit + p.Swap), bl = ps.Where(p => p.Dir > 0).Sum(p => p.Lots), sl = ps.Where(p => p.Dir < 0).Sum(p => p.Lots);
                Mt5Quote q; s.Quotes.TryGetValue(kv.Key, out q); var sym = s.Symbols.ContainsKey(kv.Key) ? s.Symbols[kv.Key] : null; string f = "F" + (sym != null ? sym.Digits : 2);
                kv.Value.Text = kv.Key + "   ▲ " + bl.ToString("0.00", IC) + " bought (" + ps.Count(p => p.Dir > 0) + ")   ▼ " + sl.ToString("0.00", IC) + " sold (" + ps.Count(p => p.Dir < 0) + ")   net " + (bl - sl).ToString("+0.00;−0.00;0.00", IC) + "   " + Signed(gpl, cur) + (q != null ? "   bid " + q.Bid.ToString(f, IC) + " / ask " + q.Ask.ToString(f, IC) : "");
                kv.Value.Foreground = gpl >= 0 ? Green : Red;
            }
            foreach (var p in pos)
            {
                TextBlock[] c; if (!posCells.TryGetValue(p.Ticket, out c)) continue; var symI = s.Symbols.ContainsKey(p.Symbol) ? s.Symbols[p.Symbol] : null; string fm = "F" + (symI != null ? symI.Digits : 2); double net = p.Profit + p.Swap;
                c[0].Text = p.Ticket.ToString(IC); c[1].Text = p.Side; c[1].Foreground = p.Dir > 0 ? Green : Red; c[2].Text = p.Lots.ToString("0.00", IC); c[3].Text = p.Open.ToString(fm, IC); c[4].Text = p.Current.ToString(fm, IC);
                c[5].Text = Signed(p.Swap, cur); c[5].Foreground = p.Swap >= 0 ? Muted : Red; c[6].Text = Signed(net, cur); c[6].Foreground = net >= 0 ? Green : Red; c[7].Text = p.Time == DateTime.MinValue ? "" : p.Time.ToString("MM-dd HH:mm:ss", IC);
            }
        }
    }
}
