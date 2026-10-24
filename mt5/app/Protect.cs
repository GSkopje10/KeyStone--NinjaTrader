// SpasiX — ⚖ BREAKEVEN & BASKET (the trade panel, LIVE / PROP / DEMO / BACKTEST alike): the stop of every ticket in profit to its entry + points or
// + $ (0 = the entry), on this symbol or on every symbol — asks first; the BASKET closes everything when the open P/L reaches +$X (take profit) or
// −$Y (stop) — switched on with a confirmation, then it closes by itself once. And the DEMO account set-up (live prices, a simulated account).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        bool beUnitUsd, basketOn, basketFired; double basketTp, basketSl; Button beUnitBtn, basketBtn; TextBox basketTpBox, basketSlBox; TextBlock basketText; Border basketCard;

        FrameworkElement BuildProtect(Func<SolidColorBrush, Border> card)
        {
            var sp = new StackPanel(); var c = card(Gold); c.Child = sp; basketCard = c;
            Func<string, TextBlock> cap = t => new TextBlock { Text = t, Foreground = Muted, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(new TextBlock { Text = "⚖ BREAKEVEN & BASKET", Foreground = Gold, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            // breakeven: the stop to the entry + points / $
            sp.Children.Add(cap("BREAKEVEN — every ticket in profit: its stop to the entry +"));
            beUnitUsd = Get("beunit", "PTS") == "USD";
            var br = new Grid(); br.ColumnDefinitions.Add(new ColumnDefinition()); br.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            beOff = Sx.Input(Get("beoff", "0"), double.NaN); beOff.TextAlignment = TextAlignment.Center; beOff.LostFocus += delegate { Put("beoff", beOff.Text); };
            beUnitBtn = Pill("", Card); beUnitBtn.Margin = new Thickness(6, 0, 0, 0); beUnitBtn.Height = 28; beUnitBtn.ToolTip = "points (price steps) or $ (the profit each ticket keeps)";
            beUnitBtn.Click += delegate { beUnitUsd = !beUnitUsd; Put("beunit", beUnitUsd ? "USD" : "PTS"); PaintBeUnit(); };
            Grid.SetColumn(beUnitBtn, 1); br.Children.Add(beOff); br.Children.Add(beUnitBtn); sp.Children.Add(br); PaintBeUnit();
            sp.Children.Add(new TextBlock { Text = "0 = exactly the entry • a ticket whose stop is already better keeps it", Foreground = Muted, FontSize = 9.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 4) });
            var bb = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            var beSym = Pill("⚖ THIS SYMBOL", Tint(Gold, 60)); beSym.Height = 28; beSym.FontSize = 10.5; beSym.Margin = new Thickness(0, 0, 4, 0); beSym.Foreground = Gold; beSym.Click += delegate { BreakevenAll(Act.Sym); };
            var beAll = Pill("⚖ EVERY SYMBOL", Tint(Gold, 60)); beAll.Height = 28; beAll.FontSize = 10.5; beAll.Margin = new Thickness(0); beAll.Foreground = Gold; beAll.Click += delegate { BreakevenAll(""); };
            bb.Children.Add(beSym); bb.Children.Add(beAll); sp.Children.Add(bb);
            // the basket: close everything at +$X / −$Y
            sp.Children.Add(cap("BASKET — close everything when the open P/L (all symbols) reaches"));
            var bg = new Grid(); bg.ColumnDefinitions.Add(new ColumnDefinition()); bg.ColumnDefinitions.Add(new ColumnDefinition());
            var b1 = new StackPanel(); b1.Children.Add(new TextBlock { Text = "TAKE PROFIT  +$", Foreground = Green, FontSize = 9.5, FontWeight = FontWeights.Bold }); basketTpBox = Sx.Input(Get("basket.tp", "0"), double.NaN); b1.Children.Add(basketTpBox);
            var b2 = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; b2.Children.Add(new TextBlock { Text = "STOP  −$", Foreground = Red, FontSize = 9.5, FontWeight = FontWeights.Bold }); basketSlBox = Sx.Input(Get("basket.sl", "0"), double.NaN); b2.Children.Add(basketSlBox); Grid.SetColumn(b2, 1);
            bg.Children.Add(b1); bg.Children.Add(b2); sp.Children.Add(bg);
            basketTpBox.LostFocus += delegate { Put("basket.tp", basketTpBox.Text); }; basketSlBox.LostFocus += delegate { Put("basket.sl", basketSlBox.Text); };
            basketBtn = Pill("▶ START THE BASKET", Tint(Gold, 200)); basketBtn.Foreground = Bg; basketBtn.Height = 32; basketBtn.Margin = new Thickness(0, 6, 0, 0); basketBtn.Click += delegate { SetBasket(!basketOn); }; sp.Children.Add(basketBtn);
            basketText = new TextBlock { Text = "0 = not used • asks once, then it closes by itself", Foreground = Muted, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }; sp.Children.Add(basketText);
            return c;
        }
        void PaintBeUnit() { if (beUnitBtn == null) return; beUnitBtn.Content = beUnitUsd ? "$ (money)" : "POINTS"; beUnitBtn.Foreground = beUnitUsd ? Green : Cyan; }

        // the breakeven stop of one ticket: the entry + the panel's points, or + the $ it keeps (with its lots)
        double BePrice(Mt5Position p, ChartView v = null)
        {
            double off = Math.Max(0, Num(beOff)), pt = v != null && v.Symbol == p.Symbol ? v.Point : 0.01, tv = 1, ts = pt;
            lock (broker.State.Gate) { Mt5Symbol sy; if (broker.State.Symbols.TryGetValue(p.Symbol, out sy)) { if (sy.Point > 0) pt = sy.Point; tv = sy.TickValue > 0 ? sy.TickValue : 1; ts = sy.TickSize > 0 ? sy.TickSize : pt; } }
            double dist = beUnitUsd ? (p.Lots > 0 ? off / (p.Lots * tv / ts) : 0) : off * pt;
            double raw = p.Open + p.Dir * dist; return p.Dir > 0 ? Math.Ceiling(raw / pt - 1e-9) * pt : Math.Floor(raw / pt + 1e-9) * pt;
        }
        void BreakevenAll(string sym)
        {
            var todo = new List<Tuple<Mt5Position, double>>(); int inProfit = 0;
            List<Mt5Position> ps; lock (broker.State.Gate) ps = broker.State.Positions.Where(x => sym == "" || string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var p in ps)
            {
                if (p.Profit <= 0) continue; inProfit++; double be = BePrice(p, Act.V);
                bool room = p.Dir > 0 ? be < p.Current : be > p.Current; bool better = p.Sl > 0 && (p.Dir > 0 ? p.Sl >= be : p.Sl <= be);
                if (room && !better) todo.Add(Tuple.Create(p, be));
            }
            string unit = beUnitUsd ? "$" + Num(beOff).ToString("0.##", IC) + " kept" : Num(beOff).ToString("0.##", IC) + " points";
            if (todo.Count == 0) { Toast(inProfit == 0 ? "no ticket in profit" + (sym != "" ? " on " + sym : "") : "every ticket in profit already has its stop there or better (or the price is too close)"); return; }
            if (!Sx.Ask(this, "⚖ Breakeven", "Move the stop of " + todo.Count + " ticket" + (todo.Count == 1 ? "" : "s") + " in profit" + (sym == "" ? " on every symbol" : " on " + sym) + " to the entry + " + unit + "?\n\n" + string.Join("\n", todo.Take(8).Select(t => "#" + t.Item1.Ticket + " " + t.Item1.Side + " " + t.Item1.Lots.ToString("0.00", IC) + " " + t.Item1.Symbol + " → S/L " + t.Item2.ToString("0.#####", IC))) + (todo.Count > 8 ? "\n…" : ""), "SET THE STOPS", "NO", Gold, "BREAKEVEN " + todo.Count + " TICKET" + (todo.Count == 1 ? "" : "S"))) return;
            foreach (var t in todo) if (!Cmd("breakeven #" + t.Item1.Ticket, "MODIFY", t.Item1.Ticket, t.Item2, t.Item1.Tp)) break;
        }

        // the basket: on / off (asks), then every refresh compares the open P/L
        void SetBasket(bool on)
        {
            if (!on) { basketOn = false; PaintBasket(0, 0); Toast("basket off"); return; }
            double tp = Math.Max(0, Num(basketTpBox)), sl = Math.Max(0, Num(basketSlBox)); Put("basket.tp", basketTpBox.Text); Put("basket.sl", basketSlBox.Text);
            if (tp <= 0 && sl <= 0) { Toast("type a take profit (+$) and / or a stop (−$) first"); return; }
            if (!Virtual && !armed) { Toast("🔒 press 🔓 ARM TRADING first — the basket closes through the armed link"); return; }
            string what = (tp > 0 ? "reaches +" + Money(tp, "USD") : "") + (tp > 0 && sl > 0 ? " or " : "") + (sl > 0 ? "falls to −" + Money(sl, "USD") : "");
            if (!Sx.Ask(this, "🧺 Basket", "Close EVERY ticket on every symbol when the open P/L " + what + "?\n\nIt closes once by itself, then the basket switches off.", "START THE BASKET", "NO", Gold, "+" + Money(tp, "USD") + " / −" + Money(sl, "USD"))) return;
            basketTp = tp; basketSl = sl; basketOn = true; basketFired = false; PaintBasket(0, 0);
        }
        void BasketWatch(Mt5State s)
        {
            if (basketText == null) return; double fl; int n;
            lock (s.Gate) { fl = s.Positions.Sum(p => p.Profit + p.Swap); n = s.Positions.Count; }
            if (!basketOn) return;
            PaintBasket(fl, n);
            if (n == 0 || basketFired) return;
            bool hitTp = basketTp > 0 && fl >= basketTp, hitSl = basketSl > 0 && fl <= -basketSl;
            if (!hitTp && !hitSl) return;
            basketFired = true; basketOn = false;
            if (!Cmd("🧺 basket " + (hitTp ? "target +" + Money(basketTp, "USD") : "stop −" + Money(basketSl, "USD")) + " — close all " + n, "CLOSEALL", "", "ALL"))
            { basketFired = false; PaintBasket(fl, n); Toast("⚠ BASKET " + (hitTp ? "TARGET" : "STOP") + " reached " + Signed(fl, "USD") + " but NOTHING was closed — trading is not armed (the basket is off)"); return; }
            Toast((hitTp ? "🎯 BASKET TARGET reached " : "🛑 BASKET STOP reached ") + Signed(fl, "USD") + " — closing all " + n + " tickets"); PaintBasket(fl, n);
        }
        void PaintBasket(double fl, int n)
        {
            if (basketBtn == null) return;
            basketBtn.Content = basketOn ? "■ STOP THE BASKET" : "▶ START THE BASKET"; basketBtn.Background = basketOn ? Tint(Green, 210) : (Brush)Tint(Gold, 200);
            basketCard.BorderBrush = basketOn ? Green : Tint(Gold, 110);
            if (!basketOn) { if (!basketFired) { basketText.Text = "0 = not used • asks once, then it closes by itself"; basketText.Foreground = Muted; } else { basketText.Text = "✓ the basket closed everything — start it again for the next one"; basketText.Foreground = Green; } return; }
            double span = basketTp > 0 && basketSl > 0 ? basketTp + basketSl : 0;
            basketText.Text = "🟢 ON • open P/L " + Signed(fl, "USD") + (basketTp > 0 ? " • target +" + Money(basketTp, "USD") + (fl < basketTp ? " (" + Money(basketTp - fl, "USD") + " to go)" : "") : "") + (basketSl > 0 ? " • stop −" + Money(basketSl, "USD") : "") + (n == 0 ? " • no tickets yet" : "");
            basketText.Foreground = fl > 0 ? Green : fl < 0 ? Red : Text;
        }

        // ---------------------------------------------------------------- the DEMO account (live prices, simulated)
        void DemoSetup(bool first)
        {
            if (!Paper) return;
            var g = new Grid { Margin = new Thickness(0, 12, 0, 0) }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            int row = 0; Func<string, string, TextBox> field = (label, val) =>
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); var l = new TextBlock { Text = label, Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
                var b = Sx.Input(val, 150); b.Margin = new Thickness(0, 4, 0, 4); Grid.SetRow(l, row); Grid.SetRow(b, row); Grid.SetColumn(b, 1); g.Children.Add(l); g.Children.Add(b); row++; return b;
            };
            int lev0; lock (live.State.Gate) lev0 = live.State.Account.Leverage > 0 ? live.State.Account.Leverage : 500;
            var bal = field("BALANCE ($)", Get("demo.bal", "10000")); var lev = field("LEVERAGE 1:", Get("demo.lev", lev0.ToString(IC)));
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); var kl = new TextBlock { Text = "ACCOUNT TYPE", Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(kl, row); g.Children.Add(kl);
            string kind = Get("demo.kind", "RAW"); var kp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) }; Grid.SetRow(kp, row); Grid.SetColumn(kp, 1); g.Children.Add(kp); row++;
            var raw = Pill("RAW", Card); var std = Pill("STANDARD", Card); kp.Children.Add(raw); kp.Children.Add(std);
            var com = field("COMMISSION $ / lot (STANDARD)", Get("demo.com", "7")); var mk = field("EXTRA SPREAD points (RAW)", Get("demo.mk", "10"));
            Action paint = () => { raw.Background = kind == "RAW" ? Tint(Cyan, 200) : (Brush)Card; raw.Foreground = kind == "RAW" ? Bg : Text; std.Background = kind == "STANDARD" ? Tint(Violet, 200) : (Brush)Card; std.Foreground = kind == "STANDARD" ? Bg : Text; com.IsReadOnly = kind == "RAW"; com.Opacity = kind == "RAW" ? 0.35 : 1; mk.IsReadOnly = kind != "RAW"; mk.Opacity = kind == "RAW" ? 1 : 0.35; };
            raw.Click += delegate { kind = "RAW"; paint(); }; std.Click += delegate { kind = "STANDARD"; paint(); }; paint();
            string txt = "Your MT5's REAL prices and candles, with a simulated account: every button works like live — nothing reaches MT5.\nRAW = no commission, the spread wider by the extra points • STANDARD = MT5's spread (near 0) + commission per lot (round turn)."
                + (!first && paper.Ready ? "\n\n⚠ A NEW account replaces the current one (its tickets and history are cleared)." : "");
            if (!Sx.Form(this, "🎮 DEMO ACCOUNT", txt, g, first || !paper.Ready ? "START THE DEMO" : "START A NEW DEMO", "CANCEL", Orange)) { if (!paper.Ready) Toast("🎮 set up the DEMO account in the panel to trade"); return; }
            Func<TextBox, double, double> num = (b, d) => { double v; return double.TryParse(b.Text.Replace(',', '.'), NumberStyles.Float, IC, out v) && v >= 0 ? v : d; };
            var s = new SimAccountSettings { Balance = Math.Max(100, num(bal, 10000)), Leverage = (int)Math.Max(1, num(lev, 500)), Kind = kind, CommissionPerLot = num(com, 7), MarkupPts = num(mk, 10), Currency = "USD" };
            Put("demo.bal", bal.Text); Put("demo.lev", lev.Text); Put("demo.kind", kind); Put("demo.com", com.Text); Put("demo.mk", mk.Text);
            paper.Start(s); resSeen = 0; posSig = ""; trackSig = ""; Toast("🎮 DEMO started — " + Money(s.Balance, "USD") + " • " + kind + " • 1:" + s.Leverage);
        }
    }
}
