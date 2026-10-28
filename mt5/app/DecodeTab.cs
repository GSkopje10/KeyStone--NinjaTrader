// SpasiX — the 🔍 DECODE tab: TradeWatch.cs on this window's account (MT5 / the DEMO account / the BACKTEST), read only. One button per bot
// (symbol + magic number), its STORY (every leg and pending order with the candles and levels around it), its RULES (the most likely entry,
// hedge / add trigger, lots and take profit) and its EVENTS; 💾 SAVE writes everything to DataDir\decode\ (to send to Claude).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        readonly TradeWatch watch = new TradeWatch();
        WrapPanel dKeys; TextBox dTable; TextBlock dHead; StackPanel dViews; string dKey = "", dView = "STORY", dKeySig = ""; int dSeen = -1; DispatcherTimer dTimer;

        FrameworkElement BuildDecode()
        {
            var root = new DockPanel { LastChildFill = true };
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(head, Dock.Top);
            head.Children.Add(new TextBlock { Text = "🔍 DECODE — how a bot trades, from what it does", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Gold });
            head.Children.Add(new TextBlock { Text = "Every position and pending order on this account is written down with the market around it: the candle and the recent high / low it sits on (M1 / M5 / M15), its place in its basket (first leg, the other way = a hedge, the same way = an add), the distance and $ from the last leg, the lot ratio, the time, how the basket closed. RULES = the most consistent explanation. Read only — it never sends anything. A bot = its symbol + magic number (SpasiX's own trades: magic 770077).", Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
            var bar = new WrapPanel();
            var save = Pill("💾 SAVE FOR CLAUDE", Tint(Green, 70)); save.Click += delegate { DecodeSave(); }; bar.Children.Add(save);
            var clr = Pill("🗑 CLEAR", Tint(Sx.Card2, 220)); clr.Click += delegate { if (Sx.Ask(this, "🗑 Clear", "Forget everything DECODE saw in this window?", "CLEAR", "CANCEL", Red)) { watch.Events.Clear(); watch.Baskets.Clear(); watch.Version++; dKeySig = ""; } }; bar.Children.Add(clr);
            dHead = new TextBlock { Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) }; bar.Children.Add(dHead);
            head.Children.Add(bar);
            dKeys = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) }; head.Children.Add(dKeys);
            root.Children.Add(head);
            dViews = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(dViews, Dock.Top);
            foreach (var v in new[] { "STORY", "RULES", "EVENTS" }) { var vv = v; var b = Pill(v, Tint(Sx.Card2, 220)); b.Height = 26; b.Click += delegate { dView = vv; dSeen = -1; DecodePaint(); }; dViews.Children.Add(b); }
            root.Children.Add(dViews);
            dTable = new TextBox { IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 12.5, Background = Panel, Foreground = Text, BorderBrush = Line, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(8) };
            root.Children.Add(dTable);
            if (Sim) { var prev = sim.OnTickHook; sim.OnTickHook = () => { if (prev != null) prev(); watch.Step(sim); }; }
            dTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
            dTimer.Tick += delegate { if (!Sim) { try { watch.Step(broker); } catch { } } if (view == "DECODE") DecodePaint(); };
            dTimer.Start(); Closed += delegate { dTimer.Stop(); };
            DecodePaint(); return root;
        }

        void DecodePaint()
        {
            if (dTable == null) return;
            var keys = watch.Baskets.Select(b => b.Key).Distinct().ToList(); var sig = string.Join("|", keys) + watch.Baskets.Count;
            if (dKey == "" && keys.Count > 0) dKey = keys[0];
            if (sig != dKeySig)
            {
                dKeySig = sig; dKeys.Children.Clear();
                foreach (var k in keys) { var kk = k; int n = watch.Baskets.Count(b => b.Key == k); var b0 = Pill(k + "  (" + n + " basket" + (n == 1 ? "" : "s") + ")", kk == dKey ? Tint(Cyan, 80) : Tint(Sx.Card2, 220)); b0.Height = 28; b0.Click += delegate { dKey = kk; dKeySig = ""; dSeen = -1; DecodePaint(); }; dKeys.Children.Add(b0); }
                if (keys.Count == 0) dKeys.Children.Add(new TextBlock { Text = "nothing traded yet in this window — every new position / pending order appears here (a bot = symbol + magic)", Foreground = Muted, FontSize = 12 });
            }
            dHead.Text = watch.Events.Count + " events  •  " + watch.Baskets.Count + " baskets  •  " + keys.Count + " bot(s)";
            foreach (Button b in dViews.Children) b.Background = (string)b.Content == dView ? Tint(Cyan, 70) : Tint(Sx.Card2, 220);
            if (watch.Version == dSeen) return; dSeen = watch.Version;
            var L = new List<string>(); var ev = watch.Events.Where(e => e.Key == dKey || (e.Key.EndsWith("• pending") && dKey.StartsWith(e.Symbol + " •"))).ToList();
            if (dKey == "") L.Add("nothing yet");
            else if (dView == "RULES") L.AddRange(watch.Rules(dKey));
            else if (dView == "EVENTS")
            {
                L.Add(string.Format("{0,-19} {1,-14} {2,-11} {3,-10} {4,6} {5,12} {6,10} {7,10} {8,7} {9,8}  {10}", "TIME", "EVENT", "ROLE", "SIDE", "LOTS", "PRICE", "VS LAST", "$ LAST", "× LOTS", "SECONDS", "SITS ON"));
                foreach (var e in ev.AsEnumerable().Reverse()) L.Add(string.Format(IC, "{0,-19} {1,-14} {2,-11} {3,-10} {4,6:0.##} {5,12} {6,10} {7,10} {8,7} {9,8}  {10}", e.Time.ToString("dd MMM yy HH:mm:ss", IC), e.Kind, e.Role, e.Side, e.Lots, Px(e.Price), double.IsNaN(e.FromLast) ? "" : e.FromLast.ToString("+0.###;−0.###", IC), double.IsNaN(e.FromLastMoney) ? "" : e.FromLastMoney.ToString("0.00", IC), double.IsNaN(e.LotRatio) ? "" : e.LotRatio.ToString("0.##", IC), double.IsNaN(e.SecSinceLast) ? "" : e.SecSinceLast.ToString("0", IC), double.IsNaN(e.MatchDist) ? "" : (e.MatchDist >= 0 ? "+" : "−") + Math.Abs(e.MatchDist).ToString("0.###", IC) + " " + e.Match));
            }
            else foreach (var g in ev.GroupBy(e => e.Basket0).OrderByDescending(g => g.Key)) { L.Add(""); L.Add(g.Key > 0 ? "──── BASKET #" + g.Key + " ────" : "──── pending orders ────"); foreach (var e in g) L.Add(e.Story); }
            dTable.Text = string.Join("\n", L);
        }

        void DecodeSave()
        {
            try
            {
                var d = Path.Combine(Dir, "decode"); Directory.CreateDirectory(d); var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", IC); var f = Path.Combine(d, "decode-" + Mode + "-" + stamp);
                var csv = new List<string> { "time,bot,kind,role,side,lots,price,bid,ask,basket,net,vs_last,money_vs_last,seconds_since_last,lot_ratio,lots_vs_first,sl,tp,sits_on,off_by,ticket,magic,comment" };
                foreach (var e in watch.Events) csv.Add(string.Join(",", e.Time.ToString("yyyy-MM-dd HH:mm:ss", IC), "\"" + e.Key + "\"", e.Kind, e.Role, e.Side, e.Lots.ToString(IC), e.Price.ToString(IC), e.Bid.ToString(IC), e.Ask.ToString(IC), e.Basket.ToString("0.00", IC), e.Net.ToString(IC), e.FromLast.ToString(IC), e.FromLastMoney.ToString("0.00", IC), e.SecSinceLast.ToString("0", IC), e.LotRatio.ToString(IC), e.LotsVsFirst.ToString(IC), e.Sl.ToString(IC), e.Tp.ToString(IC), "\"" + e.Match + "\"", e.MatchDist.ToString(IC), e.Ticket.ToString(IC), e.Magic.ToString(IC), "\"" + e.Comment.Replace("\"", "'") + "\""));
                File.WriteAllLines(f + ".csv", csv);
                var txt = new List<string>(); foreach (var k in watch.Baskets.Select(b => b.Key).Distinct()) { txt.AddRange(watch.Rules(k)); txt.Add(""); }
                txt.Add("──── THE STORY ────"); txt.AddRange(watch.Events.Select(e => e.Story)); File.WriteAllLines(f + ".txt", txt);
                Sx.Info(this, "💾 Saved", "Saved for Claude:\n" + f + ".csv\n" + f + ".txt\n\nUpload both (with a screenshot of the chart) and ask Claude to decode the bot.", Green);
            }
            catch (Exception ex) { Sx.Info(this, "💾 Not saved", ex.Message, Red); }
        }
    }
}
