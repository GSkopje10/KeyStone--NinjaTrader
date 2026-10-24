// SpasiX — 🏆 PROP mode: the firm's rules for this account (presets FTMO / The5ers / FundedNext / E8 or your own numbers): the DAILY LOSS (from the
// day's starting balance, against the equity), the MAX LOSS (static or trailing the highest balance), the PROFIT TARGET, the minimum trading days and the
// consistency rule. Always on screen (the header strip + this tab), and SpasiX REFUSES a new entry when a limit is reached or when the order's stop
// would risk more than what is left today. Closes, stops and targets are never blocked.
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
        FrameworkElement propStrip; TextBlock propHint; readonly List<Tuple<string, Border, Border, TextBlock>> propBars = new List<Tuple<string, Border, Border, TextBlock>>();
        TextBox prSize, prDaily, prMax, prTarget, prDays, prCons; Button prTrail; TextBlock prHead, prAdvice; WrapPanel prGauges; StackPanel prDaysList; long prLogin = -1; string propSig = "";
        sealed class PropNow { public bool Set; public double Size, Daily, Max, Target, DayStart, DailyUsed, DailyLeft, Floor, MaxLeft, Profit, BestDay, Cons; public int Days, MinDays; public bool Trailing; }

        string PK(string k) { long lg; lock (broker.State.Gate) lg = broker.State.Account.Login; return "prop." + lg.ToString(IC) + "." + k; }
        double PD(string k, double d) { double v; return double.TryParse(Get(PK(k), ""), NumberStyles.Float, IC, out v) ? v : d; }

        FrameworkElement BuildPropStrip()
        {
            var w = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
            foreach (var k in new[] { "DAILY LOSS", "MAX LOSS", "TARGET", "DAYS" })
            {
                var cap = new TextBlock { Text = k, Foreground = Muted, FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                var fill = new Border { CornerRadius = new CornerRadius(3), Background = Green, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
                var track = new Border { Width = 80, Height = 6, CornerRadius = new CornerRadius(3), Background = Line, Child = fill, VerticalAlignment = VerticalAlignment.Center };
                var val = new TextBlock { Foreground = Text, FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, FontFamily = Sx.Mono };
                var r = new StackPanel { Orientation = Orientation.Horizontal }; r.Children.Add(cap); r.Children.Add(track); r.Children.Add(val); w.Children.Add(r);
                propBars.Add(Tuple.Create(k, track, fill, val));
            }
            propHint = new TextBlock { Text = "🏆 PROP: set this account's rules in the 🏆 PROP tab (daily loss, max loss, target) — then they show here and SpasiX guards them", Foreground = Violet, FontSize = 11, FontWeight = FontWeights.SemiBold }; w.Children.Add(propHint);
            return w;
        }
        PropNow PropCalc(Mt5State s)
        {
            var p = new PropNow(); var a = s.Account; p.Size = PD("size", 0); p.Daily = PD("daily", 0); p.Max = PD("max", 0); p.Target = PD("target", 0); p.MinDays = (int)PD("days", 0); p.Cons = PD("cons", 0); p.Trailing = Get(PK("trail"), "0") == "1";
            p.Set = p.Size > 0 && (p.Daily > 0 || p.Max > 0);
            double todayClosed = TodayClosed(s); p.DayStart = a.Balance - todayClosed;
            p.DailyUsed = Math.Max(0, p.DayStart - a.Equity); p.DailyLeft = p.Daily > 0 ? p.Daily - p.DailyUsed : double.PositiveInfinity;
            double hwm = Math.Max(p.Size, PD("hwm", p.Size)); if (p.Set && a.Balance > hwm && a.Login > 0) { hwm = a.Balance; Put(PK("hwm"), hwm.ToString("R", IC)); }
            p.Floor = p.Trailing ? Math.Min(p.Size, hwm - p.Max) : p.Size - p.Max;   // trailing: follows the highest balance until it reaches the size
            p.MaxLeft = p.Max > 0 ? a.Equity - p.Floor : double.PositiveInfinity; p.Profit = a.Balance - p.Size;
            var tr = Trades(s.Deals); p.Days = tr.Select(t => t.Close.Date).Distinct().Count();
            var days = tr.GroupBy(t => t.Close.Date).Select(g => g.Sum(t => t.Net)).ToList(); p.BestDay = days.Count > 0 ? days.Max() : 0;
            return p;
        }
        void FillPropStrip(Mt5State s)
        {
            if (propBars.Count == 0) return; var p = PropCalc(s); string cur = s.Account.Currency;
            Action<int, double, string, SolidColorBrush> bar = (i, frac, txt, col) => { var t = propBars[i]; t.Item3.Width = 80 * Math.Max(0, Math.Min(1, frac)); t.Item3.Background = col; t.Item4.Text = txt; t.Item4.Foreground = col; };
            var wp = propStrip as WrapPanel; if (wp != null) foreach (UIElement c in wp.Children) c.Visibility = p.Set ? (c == propHint ? Visibility.Collapsed : Visibility.Visible) : (c == propHint ? Visibility.Visible : Visibility.Collapsed);
            if (!p.Set) return;
            double du = p.Daily > 0 ? p.DailyUsed / p.Daily : 0, mu = p.Max > 0 ? 1 - p.MaxLeft / p.Max : 0, tg = p.Target > 0 ? p.Profit / p.Target : 0;
            bar(0, du, p.Daily > 0 ? Money(p.DailyUsed, cur) + " / " + Money(p.Daily, cur) : "—", du >= 0.8 ? Red : du >= 0.5 ? Gold : Green);
            bar(1, mu, p.Max > 0 ? Money(Math.Max(0, p.Max - p.MaxLeft), cur) + " / " + Money(p.Max, cur) : "—", mu >= 0.8 ? Red : mu >= 0.5 ? Gold : Green);
            bar(2, tg, p.Target > 0 ? Signed(p.Profit, cur) + " / " + Money(p.Target, cur) : "—", tg >= 1 ? Green : tg >= 0 ? Cyan : Red);
            bar(3, p.MinDays > 0 ? (double)p.Days / p.MinDays : 1, p.Days + (p.MinDays > 0 ? " / " + p.MinDays : "") + " days", p.MinDays == 0 || p.Days >= p.MinDays ? Green : Cyan);
        }
        // the guard: a new entry only while the limits allow it (and its stop fits in what is left today)
        bool PropAllows(out string why)
        {
            why = ""; if (Mode != "PROP") return true; PropNow p; lock (broker.State.Gate) p = PropCalc(broker.State); if (!p.Set) return true;
            if (p.Daily > 0 && p.DailyLeft <= 0) { why = "PROP: the daily loss limit is reached (" + Money(p.DailyUsed, "USD") + " of " + Money(p.Daily, "USD") + ") — no new entries today"; return false; }
            if (p.Max > 0 && p.MaxLeft <= 0) { why = "PROP: the max loss is reached — no new entries"; return false; }
            double risk = OrderRisk(); if (risk > 0 && p.Daily > 0 && risk > p.DailyLeft) { why = "PROP: this order's stop risks " + Money(risk, "USD") + " but only " + Money(p.DailyLeft, "USD") + " of today's loss limit is left — smaller lots or a closer stop"; return false; }
            return true;
        }
        double OrderRisk() { double lots = Num(lotsBox), sd = Num(slBox); var v = Act.V; return sd > 0 && v.TickSize > 0 ? sd / v.TickSize * v.TickValue * lots : 0; }

        FrameworkElement BuildProp()
        {
            var sp = new StackPanel { MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 10) };
            prHead = new TextBlock { Text = "🏆 PROP ACCOUNT", Foreground = Violet, FontSize = 22, FontWeight = FontWeights.Bold }; sp.Children.Add(prHead);
            sp.Children.Add(new TextBlock { Text = "The firm's rules for THIS MT5 account (saved per account number). SpasiX shows them all the time and refuses a new entry at a limit — closes, stops and targets always work.", Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            prGauges = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) }; sp.Children.Add(prGauges);
            prAdvice = new TextBlock { Foreground = Text, FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 12) }; sp.Children.Add(prAdvice);
            var c = new StackPanel();
            c.Children.Add(new TextBlock { Text = "THE RULES", Foreground = Violet, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            var pre = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var f in new[] { Tuple.Create("FTMO", 5.0, 10.0, 10.0, 4), Tuple.Create("THE5ERS", 5.0, 10.0, 8.0, 3), Tuple.Create("FUNDEDNEXT", 5.0, 10.0, 10.0, 5), Tuple.Create("E8", 5.0, 8.0, 8.0, 0), Tuple.Create("FUNDED (no target)", 5.0, 10.0, 0.0, 0) })
            {
                var ff = f; var b = Pill("◆ " + f.Item1, Card); b.Foreground = Violet; b.Height = 28; b.Margin = new Thickness(0, 0, 6, 6); b.ToolTip = "daily " + f.Item2 + " % • max " + f.Item3 + " % • target " + f.Item4 + " % of the account size — check your firm's page, they change";
                b.Click += delegate { double size = Num(prSize); if (size <= 0) { lock (broker.State.Gate) size = Math.Round(broker.State.Account.Balance / 1000) * 1000; prSize.Text = size.ToString("0", IC); } prDaily.Text = (size * ff.Item2 / 100).ToString("0", IC); prMax.Text = (size * ff.Item3 / 100).ToString("0", IC); prTarget.Text = (size * ff.Item4 / 100).ToString("0", IC); prDays.Text = ff.Item5.ToString(IC); SaveProp(); Toast("◆ " + ff.Item1 + " rules filled — check them against your firm's page"); };
                pre.Children.Add(b);
            }
            c.Children.Add(pre);
            Func<string, TextBox, string, FrameworkElement> row = (cap, box, unit) => { var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) }; r.Children.Add(new TextBlock { Text = cap, Foreground = Text, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Width = 190, VerticalAlignment = VerticalAlignment.Center }); r.Children.Add(box); r.Children.Add(new TextBlock { Text = "  " + unit, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }); return r; };
            prSize = Sx.Input("", 120); prDaily = Sx.Input("", 120); prMax = Sx.Input("", 120); prTarget = Sx.Input("", 120); prDays = Sx.Input("", 70); prCons = Sx.Input("", 70);
            c.Children.Add(row("ACCOUNT SIZE", prSize, "$ (the starting balance of the challenge)"));
            c.Children.Add(row("DAILY LOSS LIMIT", prDaily, "$ below the day's starting balance (equity counts)"));
            c.Children.Add(row("MAX LOSS", prMax, "$ below the size"));
            prTrail = Pill("STATIC", Card); prTrail.Height = 26; prTrail.Click += delegate { Put(PK("trail"), Get(PK("trail"), "0") == "1" ? "0" : "1"); PaintTrail(); propSig = ""; };
            var tr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) }; tr.Children.Add(new TextBlock { Text = "MAX LOSS TYPE", Foreground = Text, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Width = 190, VerticalAlignment = VerticalAlignment.Center }); tr.Children.Add(prTrail); tr.Children.Add(new TextBlock { Text = "  STATIC = from the size • TRAILING = follows the highest balance (up to the size)", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }); c.Children.Add(tr);
            c.Children.Add(row("PROFIT TARGET", prTarget, "$ (0 = funded, no target)"));
            c.Children.Add(row("MINIMUM TRADING DAYS", prDays, "days with a trade"));
            c.Children.Add(row("CONSISTENCY", prCons, "% — the best day may be at most this share of the profit (0 = none)"));
            foreach (var b in new[] { prSize, prDaily, prMax, prTarget, prDays, prCons }) b.LostFocus += delegate { SaveProp(); };
            var save = Pill("✓ SAVE THE RULES", Violet); save.Foreground = Bg; save.Height = 34; save.HorizontalAlignment = HorizontalAlignment.Left; save.Click += delegate { SaveProp(); Toast("🏆 rules saved for this account"); }; c.Children.Add(save);
            sp.Children.Add(new Border { Background = Card, BorderBrush = Tint(Violet, 120), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12, 16, 14), Child = c, Margin = new Thickness(0, 0, 0, 12) });
            sp.Children.Add(new TextBlock { Text = "THE DAYS (closed trades)", Foreground = Muted, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 0, 4) });
            prDaysList = new StackPanel(); sp.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8, 12, 8), BorderBrush = Line, BorderThickness = new Thickness(1), Child = prDaysList });
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        void PaintTrail() { bool t = Get(PK("trail"), "0") == "1"; prTrail.Content = t ? "TRAILING" : "STATIC"; prTrail.Background = t ? Tint(Violet, 190) : (Brush)Card; prTrail.Foreground = t ? Bg : Text; }
        void SaveProp()
        {
            foreach (var t in new[] { Tuple.Create(prSize, "size"), Tuple.Create(prDaily, "daily"), Tuple.Create(prMax, "max"), Tuple.Create(prTarget, "target"), Tuple.Create(prDays, "days"), Tuple.Create(prCons, "cons") }) Put(PK(t.Item2), t.Item1.Text.Trim());
            propSig = "";
        }
        void FillProp(Mt5State s)
        {
            var a = s.Account; string cur = a.Currency;
            if (a.Login != prLogin)
            {
                prLogin = a.Login; prSize.Text = Get(PK("size"), ""); prDaily.Text = Get(PK("daily"), ""); prMax.Text = Get(PK("max"), ""); prTarget.Text = Get(PK("target"), ""); prDays.Text = Get(PK("days"), ""); prCons.Text = Get(PK("cons"), ""); PaintTrail();
            }
            prHead.Text = "🏆 PROP ACCOUNT " + (streamMode ? "•••••" : a.Login.ToString(IC)) + " • " + a.Server;
            var p = PropCalc(s); string sig = Math.Round(p.DailyUsed) + "|" + Math.Round(p.MaxLeft) + "|" + Math.Round(p.Profit) + "|" + p.Days + "|" + p.Set + "|" + s.DealsVersion + "|" + lotsBox.Text + "|" + Act.Sym + "|" + Get(PK("trail"), "0") + "|" + p.Size + p.Daily + p.Max + p.Target;
            if (sig == propSig) return; propSig = sig; prGauges.Children.Clear();
            Action<string, double, string, string, SolidColorBrush> gauge = (k, frac, big, small, col) =>
            {
                var g = new StackPanel { Width = 250 };
                g.Children.Add(new TextBlock { Text = k, Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.Bold });
                g.Children.Add(new TextBlock { Text = big, Foreground = col, FontSize = 22, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono });
                var fill = new Border { CornerRadius = new CornerRadius(5), Background = col, HorizontalAlignment = HorizontalAlignment.Left, Width = 222 * Math.Max(0, Math.Min(1, frac)) };
                g.Children.Add(new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = Line, Child = fill, Margin = new Thickness(0, 6, 0, 6), Width = 222, HorizontalAlignment = HorizontalAlignment.Left });
                g.Children.Add(new TextBlock { Text = small, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
                prGauges.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 14, 12), Margin = new Thickness(0, 0, 10, 10), BorderBrush = Tint(col, 120), BorderThickness = new Thickness(1.2), Child = g });
            };
            if (!p.Set) { prAdvice.Text = "Set the account size and at least the daily or the max loss below (or press a firm preset) — then the limits show here and in the header."; prAdvice.Foreground = Gold; }
            else
            {
                double du = p.Daily > 0 ? p.DailyUsed / p.Daily : 0, mu = p.Max > 0 ? 1 - p.MaxLeft / p.Max : 0, tg = p.Target > 0 ? p.Profit / p.Target : 0;
                gauge("DAILY LOSS TODAY", du, p.Daily > 0 ? Money(Math.Max(0, p.DailyLeft), cur) + " left" : "—", "used " + Money(p.DailyUsed, cur) + " of " + Money(p.Daily, cur) + " • the day started at " + Money(p.DayStart, cur), du >= 0.8 ? Red : du >= 0.5 ? Gold : Green);
                gauge("MAX LOSS", mu, p.Max > 0 ? Money(Math.Max(0, p.MaxLeft), cur) + " left" : "—", "the equity may not go under " + Money(p.Floor, cur) + (p.Trailing ? " (trailing)" : " (static)"), mu >= 0.8 ? Red : mu >= 0.5 ? Gold : Green);
                gauge("PROFIT TARGET", tg, p.Target > 0 ? (100 * Math.Max(0, tg)).ToString("0", IC) + " %" : "FUNDED", p.Target > 0 ? Signed(p.Profit, cur) + " of " + Money(p.Target, cur) + " • " + Money(Math.Max(0, p.Target - p.Profit), cur) + " to go" : "profit " + Signed(p.Profit, cur), tg >= 1 ? Green : Cyan);
                gauge("TRADING DAYS", p.MinDays > 0 ? (double)p.Days / p.MinDays : 1, p.Days + (p.MinDays > 0 ? " / " + p.MinDays : ""), p.MinDays > 0 && p.Days < p.MinDays ? (p.MinDays - p.Days) + " more day(s) with a trade" : "✓ enough days", p.MinDays == 0 || p.Days >= p.MinDays ? Green : Cyan);
                if (p.Cons > 0 && p.Profit > 0) { double share = p.BestDay / p.Profit * 100; gauge("CONSISTENCY", share / p.Cons, share.ToString("0", IC) + " %", "the best day " + Signed(p.BestDay, cur) + " is " + share.ToString("0", IC) + " % of the profit (limit " + p.Cons.ToString("0", IC) + " %)", share <= p.Cons ? Green : Red); }
                var v = Act.V; double pv = v.TickSize > 0 ? v.TickValue / v.TickSize : 0; double lots = Num(lotsBox);
                string room = p.Daily > 0 && pv > 0 && lots > 0 ? " • with " + lots.ToString("0.00", IC) + " lots of " + Act.Sym + " that is a " + (Math.Max(0, p.DailyLeft) / (pv * lots)).ToString("0.##", IC) + " move against you" : "";
                prAdvice.Text = p.DailyLeft <= 0 || p.MaxLeft <= 0 ? "🛑 A LIMIT IS REACHED — SpasiX refuses new entries. Close what is open and stop for today." : "Today you can lose " + Money(Math.Max(0, Math.Min(p.DailyLeft, p.MaxLeft)), cur) + " more before a limit" + room + ".";
                prAdvice.Foreground = p.DailyLeft <= 0 || p.MaxLeft <= 0 ? Red : du >= 0.5 ? Gold : Green;
            }
            prDaysList.Children.Clear();
            foreach (var g in Trades(s.Deals).GroupBy(t => t.Close.Date).OrderByDescending(g => g.Key).Take(30))
            {
                double n = g.Sum(t => t.Net); var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                r.Children.Add(Cell(g.Key.ToString("ddd dd MMM yyyy", IC), Text, 140)); r.Children.Add(Cell(g.Count() + " trades", Muted, 90)); r.Children.Add(Cell(Signed(n, cur), n >= 0 ? Green : Red, 110, true, true));
                if (p.Set && p.Daily > 0 && -n >= p.Daily) r.Children.Add(Cell("⚠ over the daily limit", Red, 180));
                prDaysList.Children.Add(r);
            }
            if (prDaysList.Children.Count == 0) prDaysList.Children.Add(new TextBlock { Text = s.BridgeVersion < 4 ? "the days need the SpasiXBridge v4 (⚙ SETUP)" : "no closed trades in the last 3 months", Foreground = Muted, FontSize = 12 });
        }
    }
}
