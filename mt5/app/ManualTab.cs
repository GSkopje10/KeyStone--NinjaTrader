// SpasiX — ✋ MANUAL tab (user's request): every trade on this account + mode, grouped per symbol from FLAT to FLAT, whoever placed it (SpasiX, the MT5
// terminal by hand, another EA). READ ONLY: it never sends, closes or changes anything — it reads the positions and the deals history (ManualTrack.cs).
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
    public sealed partial class MainWindow
    {
        readonly ManualTracker manual = new ManualTracker();
        string manualId = ""; int manualSaved = -1, manualSeen = -1, manualSel = 0; DateTime manualPaintAt;
        StackPanel mHead, mList, mDetail; FrameworkElement mTiles; Border mTilesHost; TextBlock mInfo;

        string ManualPrefix() { return Path.Combine(Dir, "manual", Mode + "-" + (manualId == "" ? "BACKTEST" : manualId)); }
        FrameworkElement BuildManual()
        {
            var root = new DockPanel();
            mHead = new StackPanel { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(mHead, Dock.Top); root.Children.Add(mHead);
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
            bar.Children.Add(new TextBlock { Text = "✋ MANUAL — every trade on this account (SpasiX, the MT5 terminal by hand, any EA), flat to flat per symbol. Read only: nothing is ever sent or closed from here.", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 14, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 760 });
            var cnt = Pill(manual.CountHedge ? "🛡 COUNT THE HEDGE'S TOO: ON" : "🛡 COUNT THE HEDGE'S TOO: OFF", Card); cnt.ToolTip = "OFF: the 🛡 HEDGE automation's own iterations are listed as → HEDGE #n and left out of the numbers (they are in the 🛡 HEDGE tab). ON: counted here too.";
            cnt.Click += delegate { manual.CountHedge = !manual.CountHedge; Put("manual.counthedge", manual.CountHedge ? "1" : "0"); cnt.Content = manual.CountHedge ? "🛡 COUNT THE HEDGE'S TOO: ON" : "🛡 COUNT THE HEDGE'S TOO: OFF"; manualSeen = -1; };
            var exp = Pill("⬇ EXPORT", Card); exp.Click += delegate { ManualExport(null); };
            var clr = Pill("🧹 CLEAR", Card); clr.Click += delegate
            {
                if (!Sx.Ask(this, "🧹 Clear the MANUAL history", AccountBadge().Item1 + "\n\nEvery recorded manual iteration of this " + Mode + " window is cleared (the account itself is not touched). The trades already in the history are not counted again.", "🧹 CLEAR", "CANCEL", Red)) return;
                manual.Clear(ManualPrefix()); lock (broker.State.Gate) manual.MarkSeen(broker.State.Deals.ToList()); manualSaved = 0; manualSeen = -1; manualSel = 0;
            };
            bar.Children.Add(cnt); bar.Children.Add(exp); bar.Children.Add(clr);
            mTilesHost = new Border { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(mTilesHost, Dock.Top); root.Children.Add(mTilesHost);
            mInfo = new TextBlock { Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 6) }; DockPanel.SetDock(mInfo, Dock.Top); root.Children.Add(mInfo);
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            mList = new StackPanel(); var sl = new ScrollViewer { Content = mList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 8, 0) }; Grid.SetColumn(sl, 0); g.Children.Add(sl);
            mDetail = new StackPanel(); var sd = new ScrollViewer { Content = mDetail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(sd, 1); g.Children.Add(sd);
            root.Children.Add(g);
            manual.CountHedge = Get("manual.counthedge", "0") == "1";
            return new Border { Child = root, Background = Sx.CardGrad, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12) };
        }
        // every tick (whatever tab is shown): read the account, keep the iterations, save the finished ones
        void ManualTick(Mt5State s)
        {
            if (mList == null) return;
            var id = AutoId(); if (id != manualId || manualSaved < 0) { if (id != manualId && manualSaved >= 0) manual.Reset(); manualId = id; manual.Load(ManualPrefix()); manualSaved = manual.Iters.Count(x => x.Done); }
            List<Mt5Position> pos; List<Mt5Deal> deals; DateTime now;
            lock (broker.State.Gate) { pos = broker.State.Positions.ToList(); deals = broker.State.Deals.ToList(); now = broker.State.ServerNow > DateTime.MinValue ? broker.State.ServerNow : DateTime.Now; }
            manual.Update(pos, deals, now, auto.Running, auto.Set != null ? auto.Set.Symbol : "", auto.Current != null ? auto.Current.N : 0);
            int done = manual.Iters.Count(x => x.Done); if (done != manualSaved) { manualSaved = done; manual.Save(ManualPrefix()); }
        }
        static string Mm(double v) { return double.IsNaN(v) ? "—" : (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture); }
        void FillManual()
        {
            if (mList == null) return; if (manual.Version == manualSeen && (DateTime.Now - manualPaintAt).TotalSeconds < 5) return; manualSeen = manual.Version; manualPaintAt = DateTime.Now;
            var now = auto.Now > DateTime.MinValue ? auto.Now : DateTime.Now;
            mHead.Children.Clear(); mHead.Children.Add(AccountBadgeEl());
            var sm = manual.Sum(now);
            mTilesHost.Child = Sx.Tiles(Tuple.Create("ITERATIONS", sm.N.ToString(IC), Cyan), Tuple.Create("WON / LOST", sm.Won + " / " + sm.Lost, sm.Won >= sm.Lost ? Green : Red), Tuple.Create("WIN %", sm.N > 0 ? (100.0 * sm.Won / sm.N).ToString("0", IC) + " %" : "—", Muted),
                Tuple.Create("NET", Mm(sm.Net), sm.Net >= 0 ? Green : Red), Tuple.Create("TODAY", Mm(sm.Today), sm.Today >= 0 ? Green : Red), Tuple.Create("AVG / WORST DD", Mm(sm.AvgDd) + " / " + Mm(sm.WorstDd), Amber),
                Tuple.Create("AVG TIME", sm.N > 0 ? ((int)sm.AvgMin).ToString(IC) + " min" : "—", Muted), Tuple.Create("BEST / WORST", Mm(sm.Best) + " / " + Mm(sm.Worst), Muted));
            mInfo.Text = (sm.ByDay != "" ? "by day: " + sm.ByDay : "no finished manual iteration yet") + (sm.ByHour != "" ? "\nby hour (server): " + sm.ByHour : "") + (sm.BySymbol != "" ? "\nby symbol: " + sm.BySymbol : "")
                + "\nSaved in " + ManualPrefix() + "-*.csv • rebuilt at start-up from the deals history MT5 sends (" + (Virtual ? "the simulated account's whole history" : "the last 3 months") + ")";
            mList.Children.Clear();
            foreach (var it in manual.Iters.AsEnumerable().Reverse().Take(300))
            {
                var col = !it.Done ? Cyan : it.ByHedge ? Muted : it.Result > 0 ? Green : Red; var x = it;
                var t = new TextBlock { FontFamily = Sx.Mono, FontSize = 12, Foreground = col, TextWrapping = TextWrapping.Wrap,
                    Text = "#" + it.N + "  " + it.Symbol + "  " + it.Start.ToString("dd MMM HH:mm", IC) + (it.Done ? " → " + it.End.ToString("HH:mm", IC) : " → OPEN") + "  " + ((int)it.Minutes(now)).ToString(IC) + " min  •  " + it.Opens + " open" + (it.Opens == 1 ? "" : "s") + " (" + it.AddOns + " add-on / hedge)  •  max " + it.MaxGross.ToString("0.##", IC) + " lot  •  worst " + Mm(it.Worst) + "  •  " + (it.Done ? "result " + Mm(it.Result) + "  •  " + it.How : "now " + Mm(it.Total)) + (it.ByHedge ? "  " + it.HedgeRef : "") + (it.Rebuilt ? "  (rebuilt)" : "") };
                var b = new Border { Child = t, Background = it.N == manualSel ? Tint(col, 40) : Brushes.Transparent, BorderBrush = Tint(col, 120), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(8, 4, 6, 4), Margin = new Thickness(0, 1, 0, 1), Cursor = System.Windows.Input.Cursors.Hand };
                b.MouseLeftButtonUp += delegate { manualSel = x.N; manualSeen = -1; FillManual(); };
                mList.Children.Add(b);
            }
            var sel = manual.Iters.FirstOrDefault(x => x.N == manualSel) ?? manual.Iters.LastOrDefault();
            mDetail.Children.Clear(); if (sel == null) { mDetail.Children.Add(new TextBlock { Text = "No trade on this account yet.", Foreground = Muted, Margin = new Thickness(6) }); return; }
            mDetail.Children.Add(new TextBlock { Text = "#" + sel.N + " " + sel.Symbol + " — THE FLOW" + (sel.ByHedge ? "  " + sel.HedgeRef : ""), Foreground = Gold, FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(4, 0, 0, 6) });
            foreach (var l in sel.Story) mDetail.Children.Add(new TextBlock { Text = l, Foreground = l.Contains("■ FLAT") ? Gold : Text, FontFamily = Sx.Mono, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 1, 0, 1) });
            mDetail.Children.Add(new TextBlock { Text = "LEGS", Foreground = Muted, FontWeight = FontWeights.Bold, FontSize = 11, Margin = new Thickness(4, 10, 0, 2) });
            foreach (var l in sel.Legs) mDetail.Children.Add(new TextBlock { FontFamily = Sx.Mono, FontSize = 11.5, Foreground = l.Kind == "OPEN" ? (l.Side == "BUY" ? Green : Red) : Text, Margin = new Thickness(4, 0, 0, 0),
                Text = l.Time.ToString("dd MMM HH:mm:ss", IC) + "  " + l.Kind.PadRight(5) + " " + l.Side.PadRight(4) + " " + l.Lots.ToString("0.00", IC) + "  " + (double.IsNaN(l.OpenPrice) ? "" : l.OpenPrice.ToString("0.#####", IC) + " → ") + l.Price.ToString("0.#####", IC) + "  " + Mm(l.Pl) + "  " + l.Source });
        }
        // the selected manual iteration on the chart (its legs as markers), when the chart shows its symbol
        void ManualGuides(string sym, List<ChartGuide> g)
        {
            if (view != "MANUAL" && Get("manual.marks", "1") != "1") return;
            var it = manual.Iters.FirstOrDefault(x => x.N == manualSel); if (it == null || !string.Equals(it.Symbol, sym, StringComparison.OrdinalIgnoreCase)) return;
            foreach (var l in it.Legs) g.Add(new ChartGuide { Kind = "LINE", P1 = l.Price, From = l.Time, Color = l.Kind == "OPEN" ? (l.Side == "BUY" ? Green : Red) : Muted, Dash = l.Kind != "OPEN", Label = "✋ #" + it.N + " " + l.Kind + " " + l.Side + " " + l.Lots.ToString("0.##", IC) + " @ " + l.Price.ToString("0.##", IC) + (l.Kind == "CLOSE" ? " " + Mm(l.Pl) : "") });
        }
        // ⬇ EXPORT: the three files (also inside 📦 EXPORT FOR CLAUDE as 14- / 15- / 16-)
        void ManualExport(string intoDir)
        {
            try
            {
                manual.Save(ManualPrefix()); var dir = intoDir ?? Path.Combine(Dir, "export", "manual-" + Mode + "-" + (manualId == "" ? "BACKTEST" : manualId) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", IC)); Directory.CreateDirectory(dir);
                foreach (var f in new[] { Tuple.Create("-iterations.csv", "14-manual-iterations.csv"), Tuple.Create("-legs.csv", "15-manual-legs.csv"), Tuple.Create("-story.txt", "16-manual-story.txt") })
                    if (File.Exists(ManualPrefix() + f.Item1)) File.Copy(ManualPrefix() + f.Item1, Path.Combine(dir, f.Item2), true);
                if (intoDir == null) Toast("⬇ the MANUAL history is in " + dir);
            }
            catch (Exception ex) { if (intoDir == null) Toast("⚠ export failed: " + ex.Message); }
        }
    }
}
