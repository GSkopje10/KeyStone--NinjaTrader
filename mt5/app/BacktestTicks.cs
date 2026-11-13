// SpasiX — 🧪 BACKTEST ▸ ⬇ DOWNLOAD REAL TICKS ▾ + 📦 EXPORT TICKS FOR CLAUDE (13r; TickLib.cs). Reads MT5's history and writes files only — never an order.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        TickDownloader tkDl; DispatcherTimer tkTimer; TextBlock tkStatus; Button tkCancel; Border tkBar, tkFill; bool tkExporting;
        List<string> TickSyms() { return btSyms.Count > 0 ? btSyms.ToList() : new List<string> { "XAUUSD" }; }
        // the two buttons (next to ⬇ DOWNLOAD YEARS) + the status row with its own progress bar and ■ CANCEL
        Tuple<FrameworkElement, FrameworkElement> BuildTickButtons()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var dlb = Sx.Drop("⬇ DOWNLOAD REAL TICKS", Card, () => TickPlan.Spans.Select(x => { var xx = x; return new Mi("⬇", (xx.Item2 >= 3650 ? "as far back as the broker has" : "the last " + xx.Item1) + " of MT5's REAL ticks (" + string.Join(" + ", TickSyms()) + ")", () => StartTickDownload(xx.Item2)) { Hint = xx.Item2 >= 3650 ? "stops at the broker's limit" : "" }; })
                .Concat(new[] { Mi.S(), new Mi { Text = "in the background, day by day • saved days skipped (press again = resume) • reads only", Off = true } }).ToList());
            dlb.Height = 46; dlb.Foreground = Green; dlb.ToolTip = "kept in " + HistoryStore.Root + " (yyyyMMdd.ticks) • ⬇ months of MT5's REAL ticks (bid, ask, time to the millisecond) — not only the 1-minute candles of ⬇ DOWNLOAD YEARS. In the background, resumable, ■ CANCEL any time. Reads history only, sends no order.";
            var exb = Sx.Drop("📦 EXPORT TICKS FOR CLAUDE", Card, () => new[] { Tuple.Create("the last month", 31), Tuple.Create("the last 3 months", 92), Tuple.Create("the last 6 months", 183), Tuple.Create("the last 12 months", 365), Tuple.Create("every tick day on this PC", 36500) }
                .Select(x => { var xx = x; return new Mi("📦", xx.Item1 + " (" + string.Join(" + ", TickSyms()) + ")", () => ExportTicks(xx.Item2)); })
                .Concat(new[] { Mi.S(), new Mi { Text = "one .sxt.gz per month (≈ 5–8 × smaller) in ONE .zip • the size is shown first", Off = true } }).ToList());
            exb.Height = 46; exb.Foreground = Violet; exb.ToolTip = "pick a range → one compressed file per month (delta-encoded bid / ask + varint times, gzip) in one .zip next to the other exports; the size is shown first";
            row.Children.Add(dlb); row.Children.Add(exb);
            var st = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            tkCancel = Pill("■ CANCEL", Tint(Red, 120)); tkCancel.Height = 26; tkCancel.Visibility = Visibility.Collapsed; tkCancel.Click += delegate { if (tkDl != null) { tkDl.Cancel(); TickPaint(); } };
            DockPanel.SetDock(tkCancel, Dock.Right); st.Children.Add(tkCancel);
            var col = new StackPanel();
            tkStatus = new TextBlock { Foreground = Green, FontSize = 12, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Text = TickSummary() };
            tkBar = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Line, Margin = new Thickness(0, 4, 8, 0), Visibility = Visibility.Collapsed };
            tkFill = new Border { CornerRadius = new CornerRadius(3), Background = Green, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; tkBar.Child = tkFill;
            col.Children.Add(tkStatus); col.Children.Add(tkBar); st.Children.Add(col);
            if (Environment.GetEnvironmentVariable("SPASIX_TEST_TICKDL") == "1") Dispatcher.BeginInvoke(new Action(() => StartTickDownload(Environment.GetEnvironmentVariable("SPASIX_TEST_TICKFAR") == "1" ? 3650 : 31)), DispatcherPriority.ApplicationIdle);   // screens.sh only: the progress row (reads the fake MT5's history)
            return Tuple.Create((FrameworkElement)row, (FrameworkElement)st);
        }
        string TickSummary()
        {
            var parts = TickSyms().Select(s => { int n = HistoryStore.TickDays(s); var lim = new DiskTickStore().Limit(s); return s + ": " + n + " day(s) of real ticks on this PC" + (lim > DateTime.MinValue ? " (the broker keeps them from " + lim.ToString("dd MMM yyyy", IC) + ")" : ""); });
            return "🎞 " + string.Join(" • ", parts);
        }
        void StartTickDownload(int spanDays)
        {
            if (tkDl != null && !tkDl.Done) { Toast("⬇ the real-tick download is already running — ■ CANCEL it first"); return; }
            tkDl = new TickDownloader(new BridgeTickSource(live), new DiskTickStore(), TickSyms(), spanDays, DateTime.Today);
            if (tkTimer == null) { tkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) }; tkTimer.Tick += delegate { TickStep(); }; }
            tkTimer.Start(); TickPaint();
        }
        void TickStep()
        {
            if (tkDl == null) { tkTimer.Stop(); return; }
            var now = DateTime.Now; for (int k = 0; k < 4 && tkDl.Step(now); k++) { }
            tkDl.NewDays.Clear(); TickPaint();
            if (tkDl.Done) { tkTimer.Stop(); Log(tkDl.Status, tkDl.Cancelled ? Amber : Green); }
        }
        void TickPaint()
        {
            if (tkStatus == null) return; bool run = tkDl != null && !tkDl.Done;
            tkStatus.Text = tkDl == null ? TickSummary() : tkDl.Status + (tkDl.Done ? "\n" + TickSummary() : "");
            tkStatus.Foreground = tkDl != null && tkDl.Status.StartsWith("✖") ? Red : tkDl != null && tkDl.Cancelled ? Amber : Green;
            tkCancel.Visibility = run ? Visibility.Visible : Visibility.Collapsed; tkBar.Visibility = run ? Visibility.Visible : Visibility.Collapsed;
            if (run) { double w = tkBar.ActualWidth > 0 ? tkBar.ActualWidth : 600; tkFill.Width = Math.Max(4, w * tkDl.Progress); }
        }
        // 📦 one .sxt.gz per month → ONE zip in DataDir\export (the size asked first)
        void ExportTicks(int spanDays)
        {
            if (tkExporting) { Toast("📦 an export is being prepared"); return; }
            var syms = TickSyms(); var to = DateTime.Today; var from = spanDays >= 36500 ? new DateTime(2000, 1, 1) : to.AddDays(-spanDays);
            var plan = syms.Select(s => Tuple.Create(s, TickExport.DaysOf(s, from, to))).Where(x => x.Item2.Count > 0).ToList();
            if (plan.Count == 0) { Sx.Info(this, "📦 EXPORT TICKS FOR CLAUDE", "No real-tick day of " + string.Join(" + ", syms) + " is on this PC in that range — ⬇ DOWNLOAD REAL TICKS first.", Violet); return; }
            tkExporting = true; tkStatus.Text = "📦 packing " + plan.Sum(x => x.Item2.Count) + " tick day(s) … (the size comes next, nothing is written yet)"; tkStatus.Foreground = Violet;
            Task.Run(() =>
            {
                var res = new List<Tuple<string, List<TickExport.Month>>>(); string err = "";
                try { foreach (var p in plan) { var sp = HistoryStore.LoadSpec(p.Item1); res.Add(Tuple.Create(p.Item1, TickExport.Build(p.Item1, sp != null && sp.Point > 0 ? sp.Point : 0.01, p.Item2))); } } catch (Exception ex) { err = ex.Message; }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    tkExporting = false; TickPaint(); if (err != "") { Sx.Info(this, "📦 EXPORT TICKS FOR CLAUDE", "Could not pack the ticks: " + err, Red); return; }
                    long packed = res.Sum(r => r.Item2.Sum(m => (long)m.Data.Length)), raw = res.Sum(r => r.Item2.Sum(m => m.RawBytes)); int months = res.Sum(r => r.Item2.Count), days = res.Sum(r => r.Item2.Sum(m => m.Days));
                    var txt = string.Join("\n", res.Select(r => r.Item1 + ": " + r.Item2.Count + " month file(s), " + r.Item2.Sum(m => m.Days) + " days, " + r.Item2.Sum(m => m.Ticks).ToString("N0", IC) + " ticks"))
                        + "\n\nSIZE: " + (packed / 1048576.0).ToString("0.0", IC) + " MB (" + (raw / 1048576.0).ToString("0.0", IC) + " MB as raw ticks — " + (packed > 0 ? ((double)raw / packed).ToString("0.0", IC) : "—") + " × smaller)\n\nWrite " + (res.Count == 1 ? "the .zip" : res.Count + " .zip files (one per symbol)") + " to " + Path.Combine(Dir, "export") + "?";
                    if (!Sx.Ask(this, "📦 EXPORT TICKS FOR CLAUDE", txt, "WRITE THE ZIP", "CANCEL", Violet)) return;
                    var made = new List<string>();
                    foreach (var r in res.Where(x => x.Item2.Count > 0))
                    {
                        var zip = Path.Combine(Dir, "export", "SpasiX-ticks-" + r.Item1 + "-" + r.Item2.First().Name.Substring(r.Item1.Length + 1, 7) + "_to_" + r.Item2.Last().Name.Substring(r.Item1.Length + 1, 7) + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmm", IC) + ".zip");
                        try { TickExport.WriteZip(zip, r.Item1, r.Item2, Build); made.Add(zip); } catch (Exception ex) { Log("📦 tick export failed: " + ex.Message, Red); }
                    }
                    if (made.Count == 0) return;
                    Log("📦 ticks exported for Claude: " + string.Join(", ", made), Green); Toast("📦 " + months + " month file(s), " + days + " days — send " + Path.GetFileName(made[0]) + (made.Count > 1 ? " (+" + (made.Count - 1) + ")" : "") + " to Claude");
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + made[0] + "\"") { UseShellExecute = true }); } catch { }
                }));
            });
        }
    }
}
