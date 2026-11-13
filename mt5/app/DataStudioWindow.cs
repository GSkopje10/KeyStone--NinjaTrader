// SpasiX — 📥 DATA STUDIO window (13s; DataStudio.cs). Downloads and manages MT5 history ONLY: the symbols, a FROM → TO range (presets), what to get
// (1-minute candles / real ticks), ⬇ DOWNLOAD in the background (resumable, saved days skipped, progress + ETA, ■ CANCEL, the broker's tick limit said
// plainly), what is on this PC (a calendar per month, sizes), 🗑 delete a range, 📦 EXPORT FOR CLAUDE (the size first). It never sends an order.
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
    public sealed class DataStudioWindow : Window
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static DataStudioWindow open;
        readonly IBroker live; readonly List<string> syms = new List<string>();
        readonly WrapPanel symChips = new WrapPanel(); readonly TextBox fromBox, toBox; readonly CheckBox getCandles, getTicks;
        readonly TextBlock status = Sx.Label("", 12.5, Sx.Green, true), plan = Sx.Label("", 11.5, Sx.Muted), acct = Sx.Label("", 11.5, Sx.Muted);
        readonly Border bar, fill; readonly Button dlBtn, cancelBtn; readonly TextBox table = new TextBox { IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 12.5, Background = Sx.Panel, Foreground = Sx.Text, BorderBrush = Sx.Line, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8) };
        readonly DispatcherTimer tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) }; DataJob job; DateTime invAt = DateTime.MinValue; bool exporting;
        public static string LastStatus = "";
        // open (or bring forward) the studio; optional: the symbols / range / what to get, and start at once (the BACKTEST's "download the missing days")
        public static DataStudioWindow Open(Window owner, IBroker live, IEnumerable<string> symbols = null, DateTime? from = null, DateTime? to = null, bool candles = true, bool ticks = true, bool start = false)
        {
            if (open == null) { open = new DataStudioWindow(live ?? Shell.Bridge); open.Closed += delegate { open = null; }; try { if (owner != null && owner.IsVisible) open.Owner = owner; } catch { } open.Show(); }
            else { if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal; open.Activate(); }
            if (symbols != null && symbols.Any()) { open.syms.Clear(); open.syms.AddRange(symbols); open.PaintSyms(); }
            if (from.HasValue) open.fromBox.Text = from.Value.ToString("yyyy-MM-dd", IC); if (to.HasValue) open.toBox.Text = to.Value.ToString("yyyy-MM-dd", IC);
            open.getCandles.IsChecked = candles; open.getTicks.IsChecked = ticks; open.Repaint(true);
            if (start) open.StartDownload();
            return open;
        }
        public static bool IsBusy { get { return open != null && open.job != null && !open.job.Done; } }
        DataStudioWindow(IBroker b)
        {
            live = b; Title = "SpasiX — 📥 DATA STUDIO"; Width = 1240; Height = 860; MinWidth = 960; MinHeight = 620; Background = Sx.Bg; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var lg = MainWindow.Logo(); if (lg != null) Icon = lg; Sx.DarkTitle(this);
            syms.AddRange((Get("ds.syms", "XAUUSD") ?? "XAUUSD").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            var root = new DockPanel { Margin = new Thickness(16, 12, 16, 12), LastChildFill = true };
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(head, Dock.Top);
            head.Children.Add(Sx.Label("📥 DATA STUDIO — download and manage MT5's history", 20, Sx.Cyan, true));
            head.Children.Add(new TextBlock { Text = "The 1-minute candles and MT5's REAL ticks, kept on this PC (" + HistoryStore.Root + ") — the 🧪 BACKTEST, the post-mortems and the 🧭 CONSULT load them from here. It only reads MT5's history: it never sends an order.", Foreground = Sx.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            head.Children.Add(acct); root.Children.Add(head);
            var card = new Border { Background = Sx.CardGrad, BorderBrush = Sx.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(card, Dock.Top);
            var cs = new StackPanel(); card.Child = cs;
            cs.Children.Add(Sx.Label("1 • SYMBOLS (tick one or more)", 12, Sx.Gold, true)); symChips.Margin = new Thickness(0, 4, 0, 8); cs.Children.Add(symChips);
            cs.Children.Add(Sx.Label("2 • THE DAYS (server time)", 12, Sx.Gold, true));
            var r1 = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
            var rg = DataPlan.Range(31, DateTime.Today);
            fromBox = Sx.Input(Get("ds.from", rg.Item1.ToString("yyyy-MM-dd", IC)), 110); toBox = Sx.Input(Get("ds.to", rg.Item2.ToString("yyyy-MM-dd", IC)), 110);
            fromBox.TextChanged += delegate { Repaint(false); }; toBox.TextChanged += delegate { Repaint(false); };
            r1.Children.Add(Sx.Label("FROM ", 12, Sx.Muted, true)); r1.Children.Add(fromBox); r1.Children.Add(Sx.Label("  →  TO ", 12, Sx.Muted, true)); r1.Children.Add(toBox); r1.Children.Add(new Border { Width = 16 });
            foreach (var p in DataPlan.Presets) { var pp = p; var b0 = Sx.Pill(pp.Item1, Sx.Card2); b0.Height = 28; b0.FontSize = 11; b0.Margin = new Thickness(0, 0, 6, 0); b0.Click += delegate { var r = DataPlan.Range(pp.Item2, DateTime.Today); fromBox.Text = r.Item1.ToString("yyyy-MM-dd", IC); toBox.Text = r.Item2.ToString("yyyy-MM-dd", IC); }; r1.Children.Add(b0); }
            cs.Children.Add(r1);
            cs.Children.Add(Sx.Label("3 • WHAT TO GET", 12, Sx.Gold, true));
            var r2 = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            getCandles = new CheckBox { Content = "1-minute candles", IsChecked = Get("ds.c", "1") == "1", Foreground = Sx.Text, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
            getTicks = new CheckBox { Content = "MT5's REAL ticks (bid, ask, time to the millisecond — much bigger, slower)", IsChecked = Get("ds.t", "1") == "1", Foreground = Sx.Text, VerticalAlignment = VerticalAlignment.Center };
            getCandles.Click += delegate { Repaint(false); }; getTicks.Click += delegate { Repaint(false); };
            r2.Children.Add(getCandles); r2.Children.Add(getTicks); cs.Children.Add(r2); cs.Children.Add(plan);
            root.Children.Add(card);
            var bt = new DockPanel { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(bt, Dock.Top);
            dlBtn = Sx.Pill("⬇  DOWNLOAD", Sx.Tint(Sx.Green, 200)); dlBtn.Foreground = Sx.Bg; dlBtn.Height = 40; dlBtn.FontSize = 14; dlBtn.Padding = new Thickness(22, 0, 22, 0); dlBtn.ToolTip = "in the background, day by day — the days already on this PC are skipped, so pressing it again resumes"; dlBtn.Click += delegate { StartDownload(); };
            cancelBtn = Sx.Pill("■ CANCEL", Sx.Tint(Sx.Red, 120)); cancelBtn.Height = 40; cancelBtn.Visibility = Visibility.Collapsed; cancelBtn.Click += delegate { if (job != null) { job.Cancel(); Repaint(true); } };
            var del = Sx.Pill("🗑 DELETE A RANGE …", Sx.Card2); del.Height = 40; del.Foreground = Sx.Red; del.ToolTip = "delete the candles / ticks of the ticked symbols between FROM and TO (asks first)"; del.Click += delegate { DeleteRange(); };
            var exp = Sx.Pill("📦 EXPORT FOR CLAUDE …", Sx.Tint(Sx.Violet, 90)); exp.Height = 40; exp.ToolTip = "the ticked symbols FROM → TO: real ticks (.sxt.gz per month) + candles (.m1.csv.gz per month) in ONE zip — the size is shown before anything is written"; exp.Click += delegate { Export(); };
            var rf = Sx.Pill("↻", Sx.Card2); rf.Height = 40; rf.ToolTip = "refresh what is on this PC"; rf.Click += delegate { Repaint(true); };
            foreach (var x in new[] { dlBtn, cancelBtn, del, exp, rf }) { x.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(x, Dock.Left); bt.Children.Add(x); }
            bt.Children.Add(new Border()); root.Children.Add(bt);
            var st = new StackPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(st, Dock.Top); status.TextWrapping = TextWrapping.Wrap; st.Children.Add(status);
            bar = new Border { Height = 7, CornerRadius = new CornerRadius(3), Background = Sx.Line, Margin = new Thickness(0, 5, 0, 0), Visibility = Visibility.Collapsed };
            fill = new Border { CornerRadius = new CornerRadius(3), Background = Sx.Green, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; bar.Child = fill; st.Children.Add(bar); root.Children.Add(st);
            var inv = new DockPanel(); var ih = Sx.Label("ON THIS PC   " + DataInventory.Legend, 11.5, Sx.Gold, true); DockPanel.SetDock(ih, Dock.Top); ih.Margin = new Thickness(0, 0, 0, 4); inv.Children.Add(ih); inv.Children.Add(table); root.Children.Add(inv);
            Content = root;
            tm.Tick += delegate { Tick(); }; Closed += delegate { tm.Stop(); if (job != null && !job.Done) job.Cancel(); };
            PaintSyms(); Repaint(true);
        }
        static string Get(string k, string d) { try { return MainWindow.GetSetting(k, d); } catch { return d; } }
        static void Put(string k, string v) { try { MainWindow.PutSetting(k, v); } catch { } }
        bool Range(out DateTime f, out DateTime t)
        {
            t = DateTime.MinValue; bool ok = DateTime.TryParseExact((fromBox.Text ?? "").Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out f) && DateTime.TryParseExact((toBox.Text ?? "").Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out t) && f <= t;
            return ok;
        }
        void PaintSyms()
        {
            symChips.Children.Clear(); List<string> mt = new List<string>(); try { if (live != null) lock (live.State.Gate) mt = live.State.Quotes.Keys.ToList(); } catch { }
            var all = syms.Concat(HistoryStore.Symbols()).Concat(new[] { "XAUUSD", "USTEC", "BTCUSD", "EURUSD" }.Where(x => mt.Count == 0 || mt.Contains(x, StringComparer.OrdinalIgnoreCase))).Concat(mt.OrderBy(x => x).Take(12)).Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToList();
            foreach (var s in all)
            {
                var ss = s; bool on = syms.Contains(s, StringComparer.OrdinalIgnoreCase); var b = Sx.Pill((on ? "✓ " : "") + s, on ? (Brush)Sx.Tint(Sx.Cyan, 200) : Sx.Card2); b.Height = 30; b.FontSize = 12; b.Margin = new Thickness(0, 0, 6, 6); b.Foreground = on ? Sx.Bg : Sx.Cyan;
                b.Click += delegate { if (syms.Contains(ss, StringComparer.OrdinalIgnoreCase)) syms.RemoveAll(x => string.Equals(x, ss, StringComparison.OrdinalIgnoreCase)); else syms.Add(ss); Put("ds.syms", string.Join(",", syms)); PaintSyms(); Repaint(true); };
                symChips.Children.Add(b);
            }
            var add = Sx.Pill("+ ANY SYMBOL", Sx.Card2); add.Height = 30; add.FontSize = 11; add.Click += delegate { var t = Sx.Prompt(this, "📥 DATA STUDIO", "The MT5 symbol (as in Market Watch):", ""); if (!string.IsNullOrWhiteSpace(t)) { syms.Add(t.Trim().ToUpperInvariant()); Put("ds.syms", string.Join(",", syms)); PaintSyms(); Repaint(true); } }; symChips.Children.Add(add);
        }
        void Repaint(bool inventory)
        {
            string who = "MT5 not connected — the days already on this PC can still be seen, deleted and exported";
            try { if (live != null) lock (live.State.Gate) if (live.State.Alive) who = "MT5: " + live.State.Account.Login + " @ " + live.State.Account.Server + " — the history comes from this terminal"; } catch { }
            acct.Text = who;
            DateTime f, t; if (Range(out f, out t)) { Put("ds.from", fromBox.Text.Trim()); Put("ds.to", toBox.Text.Trim()); Put("ds.c", getCandles.IsChecked == true ? "1" : "0"); Put("ds.t", getTicks.IsChecked == true ? "1" : "0");
                plan.Text = "📅 " + f.ToString("ddd dd MMM yyyy", IC) + " → " + t.ToString("ddd dd MMM yyyy", IC) + "   •   " + string.Join("   •   ", syms.Select(s => DataPlan.Describe(s, f, t, getCandles.IsChecked == true, getTicks.IsChecked == true, new DiskTickStore()))); plan.Foreground = Sx.Muted; }
            else { plan.Text = "✖ FROM / TO as yyyy-MM-dd, FROM ≤ TO"; plan.Foreground = Sx.Red; }
            bool run = job != null && !job.Done; dlBtn.IsEnabled = !run; cancelBtn.Visibility = run ? Visibility.Visible : Visibility.Collapsed; bar.Visibility = run ? Visibility.Visible : Visibility.Collapsed;
            if (job != null) { status.Text = job.Status; status.Foreground = job.Status.StartsWith("✖") ? Sx.Red : job.Cancelled ? Sx.Amber : Sx.Green; LastStatus = job.Status; } else if (status.Text == "") status.Text = "choose the symbols, the days and what to get → ⬇ DOWNLOAD";
            if (run) fill.Width = Math.Max(4, (bar.ActualWidth > 0 ? bar.ActualWidth : 900) * job.Progress);
            if (inventory) { invAt = DateTime.Now; var L = new List<string>(); foreach (var s in syms) { L.AddRange(DataInventory.Table(s)); L.Add(""); } table.Text = string.Join("\n", L); }
        }
        void StartDownload()
        {
            DateTime f, t; if (!Range(out f, out t)) { Repaint(false); return; }
            if (syms.Count == 0) { status.Text = "✖ tick a symbol first"; status.Foreground = Sx.Red; return; }
            if (getCandles.IsChecked != true && getTicks.IsChecked != true) { status.Text = "✖ tick what to get (candles and / or real ticks)"; status.Foreground = Sx.Red; return; }
            if (job != null && !job.Done) return;
            bool alive = false; try { lock (live.State.Gate) alive = live.State.Alive; } catch { }
            if (!alive) { status.Text = "✖ MT5 with the SpasiXBridge is needed to download — start MetaTrader 5 (the days already on this PC still work)"; status.Foreground = Sx.Red; return; }
            job = new DataJob(live, syms, f, t, getCandles.IsChecked == true, getTicks.IsChecked == true); tm.Start(); Repaint(false);
        }
        void Tick()
        {
            if (job == null) { tm.Stop(); return; }
            var now = DateTime.Now; for (int k = 0; k < 4 && job.Step(now); k++) { }
            Repaint((DateTime.Now - invAt).TotalSeconds >= 3 || job.Done);
            if (job.Done) tm.Stop();
        }
        void DeleteRange()
        {
            DateTime f, t; if (!Range(out f, out t) || syms.Count == 0) { Repaint(false); return; }
            if (job != null && !job.Done) { status.Text = "■ CANCEL the download first"; return; }
            int k = Sx.Choice(this, "🗑 DELETE A RANGE", "Delete " + string.Join(" + ", syms) + " from " + f.ToString("dd MMM yyyy", IC) + " to " + t.ToString("dd MMM yyyy", IC) + " on this PC?\nIt can be downloaded again later (as far as the broker keeps it).", null,
                new[] { "CANDLES + TICKS", "ONLY THE TICKS", "ONLY THE CANDLES", "CANCEL" }, new[] { Sx.Red, Sx.Amber, Sx.Amber, Sx.Muted });
            if (k < 0 || k == 3) return; int c = 0, tk = 0;
            foreach (var s in syms) { var r = DataInventory.Delete(s, f, t, k == 0 || k == 2, k == 0 || k == 1); c += r.Item1; tk += r.Item2; }
            status.Text = "🗑 deleted " + c + " day(s) of candles and " + tk + " day(s) of ticks"; status.Foreground = Sx.Amber; Repaint(true);
        }
        void Export()
        {
            DateTime f, t; if (!Range(out f, out t) || syms.Count == 0 || exporting) { Repaint(false); return; }
            bool c = getCandles.IsChecked == true, tk = getTicks.IsChecked == true; if (!c && !tk) { status.Text = "✖ tick candles and / or real ticks"; return; }
            exporting = true; status.Text = "📦 packing … (nothing is written yet — the size comes first)"; status.Foreground = Sx.Violet; var ss = syms.ToList();
            Task.Run(() =>
            {
                List<DataExport.Entry> e = null; string err = ""; try { e = DataExport.Build(ss, f, t, c, tk); } catch (Exception ex) { err = ex.Message; }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    exporting = false; if (err != "" || e == null) { status.Text = "✖ " + err; status.Foreground = Sx.Red; return; }
                    if (e.Count == 0) { Sx.Info(this, "📦 EXPORT FOR CLAUDE", "Nothing of " + string.Join(" + ", ss) + " is on this PC in that range — ⬇ DOWNLOAD it first.", Sx.Violet); status.Text = ""; return; }
                    long size = DataExport.Size(e), raw = e.Sum(x => x.Raw);
                    if (!Sx.Ask(this, "📦 EXPORT FOR CLAUDE", e.Count(x => x.Name.StartsWith("ticks/")) + " month file(s) of real ticks + " + e.Count(x => x.Name.StartsWith("candles/")) + " month file(s) of candles\n\nSIZE: " + DataInventory.Mb(size) + " (" + DataInventory.Mb(raw) + " as on this PC)\n\nWrite the zip to " + Path.Combine(DataDir.Root, "export") + "?", "WRITE THE ZIP", "CANCEL", Sx.Violet)) { status.Text = ""; return; }
                    var zip = Path.Combine(DataDir.Root, "export", "SpasiX-history-" + string.Join("+", ss) + "-" + f.ToString("yyyyMMdd", IC) + "_" + t.ToString("yyyyMMdd", IC) + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmm", IC) + ".zip");
                    try { DataExport.WriteZip(zip, e, DataExport.Readme(e, MainWindow.Build, f, t)); status.Text = "📦 written: " + zip + " (" + DataInventory.Mb(new FileInfo(zip).Length) + ") — send it to Claude"; status.Foreground = Sx.Green; try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + zip + "\"") { UseShellExecute = true }); } catch { } }
                    catch (Exception ex) { status.Text = "✖ " + ex.Message; status.Foreground = Sx.Red; }
                }));
            });
        }
    }
}
