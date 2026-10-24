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
            foreach (var m in new[] { Tuple.Create("⛔ CLOSE EVERYTHING", "ALL", Red), Tuple.Create("CLOSE ALL BUYS", "BUY", Green), Tuple.Create("CLOSE ALL SELLS", "SELL", Red), Tuple.Create("CLOSE ALL IN PROFIT", "PROFIT", Green), Tuple.Create("CLOSE ALL IN LOSS", "LOSS", Red) })
            { var which = m.Item2; var b = Pill(m.Item1, which == "ALL" ? (Brush)Tint(Red, 210) : Card); b.Foreground = which == "ALL" ? Sx.White : m.Item3; b.Click += delegate { CloseAll("", which); }; bulk.Children.Add(b); }
            var be = Pill("⚖ BREAKEVEN ALL IN PROFIT", Tint(Gold, 170)); be.Foreground = Bg; be.Click += delegate { if (Ask("Breakeven on every ticket in profit (all symbols)?")) Cmd("breakeven on every symbol", "BE", "", "ALL", IntOf(beOff.Text, 10)); }; bulk.Children.Add(be);
            var ca = Pill("CANCEL ALL ORDERS", Card); ca.Foreground = Cyan; ca.Click += delegate { if (Ask("Cancel every pending order (all symbols)?")) Cmd("cancel every order", "CANCELALL", ""); }; bulk.Children.Add(ca);
            sp.Children.Add(bulk);
            posSummary = new TextBlock { Text = "no positions", Foreground = Muted, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 0, 8), TextWrapping = TextWrapping.Wrap }; sp.Children.Add(posSummary);
            posPanel = new StackPanel(); sp.Children.Add(posPanel);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        static TextBlock Cell(string t, Brush fg, double w, bool right = false, bool bold = false) { return new TextBlock { Text = t, Foreground = fg, Width = w, FontSize = 12.5, FontFamily = new FontFamily("Consolas"), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextAlignment = right ? TextAlignment.Right : TextAlignment.Left, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center }; }
        Button Mini(string t, Brush fg, string tip) { var b = Pill(t, Card); b.Height = 24; b.FontSize = 11; b.Padding = new Thickness(7, 0, 7, 0); b.Margin = new Thickness(0, 0, 4, 0); b.Foreground = fg; b.ToolTip = tip; return b; }
        TextBox Small(string t) { return new TextBox { Text = t, Width = 78, Height = 24, FontSize = 12, FontFamily = new FontFamily("Consolas"), Background = Bg, Foreground = Text, BorderBrush = Line, CaretBrush = Gold, Margin = new Thickness(0, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center }; }

        void FillPositions(Mt5State s)
        {
            var acc = s.Account; string cur = acc.Currency; var pos = s.Positions; var ords = s.Orders;
            double pl = pos.Sum(p => p.Profit + p.Swap);
            posSummary.Text = pos.Count == 0 && ords.Count == 0 ? "no open positions or orders" : pos.Count + " open ticket" + (pos.Count == 1 ? "" : "s") + " • " + pos.Where(p => p.Dir > 0).Sum(p => p.Lots).ToString("0.00", IC) + " lots bought • " + pos.Where(p => p.Dir < 0).Sum(p => p.Lots).ToString("0.00", IC) + " lots sold • open P/L " + Signed(pl, cur) + (ords.Count > 0 ? " • " + ords.Count + " pending order" + (ords.Count == 1 ? "" : "s") : "") + (armed || Sim ? "" : "   •   🔒 ARM in the 📈 CHART panel to manage them from here");
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
                    foreach (var m in new[] { Tuple.Create("CLOSE ALL", "ALL", Red), Tuple.Create("BUYS", "BUY", Green), Tuple.Create("SELLS", "SELL", Red), Tuple.Create("PROFIT", "PROFIT", Green), Tuple.Create("LOSS", "LOSS", Red) })
                    { var which = m.Item2; var b = Mini(m.Item1, m.Item3, "close " + m.Item1.ToLowerInvariant() + " on " + sym); b.Click += delegate { CloseAll(sym, which); }; hb.Children.Add(b); }
                    var beS = Mini("⚖ BE", Gold, "breakeven on " + sym); beS.Click += delegate { Cmd("breakeven on " + sym, "BE", sym, "ALL", IntOf(beOff.Text, 10)); }; hb.Children.Add(beS);
                    var chart = Mini("📈", Cyan, "open its chart"); chart.Click += delegate { SetPane(Act, sym, Act.Tf); Show("CHART"); }; hb.Children.Add(chart);
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
                        var bx = Mini("✕", Red, "close this ticket"); bx.Click += delegate { if (Ask("Close ticket #" + tk + "?")) Cmd("close #" + tk, "CLOSE", tk, 0.0); };
                        var bh = Mini("½", Text, "close half of it"); bh.Click += delegate { Cmd("close half of #" + tk, "CLOSE", tk, Math.Round(p0.Lots / 2, 2)); };
                        var bxw = new StackPanel { Orientation = Orientation.Horizontal, Width = 62, Margin = new Thickness(0, 0, 10, 0) }; bxw.Children.Add(bx); bxw.Children.Add(bh); r.Children.Add(bxw);
                        var cells = new[] { Cell("", Muted, 100), Cell("", Text, 50, false, true), Cell("", Text, 50), Cell("", Text, 86), Cell("", Cyan, 86), Cell("", Muted, 70, true), Cell("", Text, 96, true, true), Cell("", Muted, 120) };
                        foreach (var c in cells) r.Children.Add(c); posCells[tk] = cells;
                        var slB = Small(p.Sl > 0 ? p.Sl.ToString("0.#####", IC) : ""); var tpB = Small(p.Tp > 0 ? p.Tp.ToString("0.#####", IC) : ""); slB.ToolTip = "stop (price, empty = none)"; tpB.ToolTip = "target (price, empty = none)";
                        var set = Mini("SET", Gold, "send this stop / target"); set.Click += delegate { double sl, tp; double.TryParse(slB.Text.Replace(',', '.'), NumberStyles.Float, IC, out sl); double.TryParse(tpB.Text.Replace(',', '.'), NumberStyles.Float, IC, out tp); Cmd("S/L T/P on #" + tk, "MODIFY", tk, sl, tp); };
                        var bet = Mini("⚖", Gold, "breakeven: the stop to the entry (+ the panel's points)"); bet.Click += delegate { int off = IntOf(beOff.Text, 10); double pt = 0.01; Mt5Symbol sy; if (broker.State.Symbols.TryGetValue(p0.Symbol, out sy)) pt = sy.Point; Cmd("breakeven #" + tk, "MODIFY", tk, p0.Open + p0.Dir * off * pt, p0.Tp); };
                        r.Children.Add(slB); r.Children.Add(tpB); r.Children.Add(set); r.Children.Add(bet);
                        card.Children.Add(r);
                    }
                    posPanel.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), BorderBrush = Line, BorderThickness = new Thickness(1), Child = card });
                }
                if (ords.Count > 0)
                {
                    var card = new StackPanel(); card.Children.Add(new TextBlock { Text = "⏳ PENDING ORDERS", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Amber, Margin = new Thickness(0, 0, 0, 6) });
                    foreach (var o in ords)
                    {
                        var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) }; var sym = s.Symbols.ContainsKey(o.Symbol) ? s.Symbols[o.Symbol] : null; string f = "F" + (sym != null ? sym.Digits : 2); long tk = o.Ticket;
                        var bx = Mini("✕", Red, "cancel this order"); bx.Click += delegate { if (Ask("Cancel the pending order #" + tk + "?")) Cmd("cancel order #" + tk, "CANCEL", tk); }; r.Children.Add(bx);
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
