// SpasiX — the 🕵 DECODE HEDGING studio (title bar button, every tab): what HedgeDecode.cs records from the trades COPIED onto your MT5
// accounts, in tabs — LIVE (the basket now + the newest executions), EXECUTIONS (the pool), HEDGES & SIZING, PENDING ORDERS (placed / moved /
// removed with the floating $), MOVING AVERAGES, CYCLES, HINTS (the candidate rules) and VIDEO DAYS. READ ONLY: no button here sends an order.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed class HedgeDecodeWindow : Window
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static HedgeDecodeWindow open; static DispatcherTimer svc;
        // the recorder runs from start-up, with the studio open or not (every connected account, every second)
        public static void StartService()
        {
            if (svc != null) return; var d = HedgeDecode.Service;
            d.Symbol = MainWindow.SGet("hdec.sym", "XAU"); d.Filter = MainWindow.SGet("hdec.filter", ""); foreach (var k in MainWindow.SGet("hdec.off", "").Split('|')) if (k != "") d.Off.Add(k);
            svc = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            svc.Tick += delegate { try { var hub = Shell.Hub; var links = hub != null ? hub.Links : new List<Mt5Bridge>(); if (links.Count == 0 && Shell.Bridge != null) links.Add(Shell.Bridge); d.StepAll(links); } catch { } };
            svc.Start();
        }
        public static void ShowStudio(Window owner)
        {
            StartService();
            if (open == null) { open = new HedgeDecodeWindow(); var me = open; open.Closed += delegate { open = null; Shell.StudioClosed(me); }; Shell.Studio = open; }
            Shell.Switch(open);   // a full tab of its own in the title bar, like LIVE / DEMO / BACKTEST
        }

        readonly HedgeDecode d = HedgeDecode.Service;
        readonly StackPanel views = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 6) }, accts = new StackPanel { Orientation = Orientation.Horizontal }, rows = new StackPanel();
        readonly ContentControl tiles = new ContentControl(); readonly TextBlock status = Sx.Label("", 12, Sx.Muted);
        string view = "🔴 LIVE", acct = ""; int seen = -1; DateTime paintAt;
        static readonly string[] Views = { "🔴 LIVE", "📋 EXECUTIONS", "🛡 HEDGES & SIZING", "⏳ PENDING ORDERS", "📈 MOVING AVERAGES", "🔁 CYCLES", "💡 HINTS", "🎬 VIDEO DAYS" };

        public HedgeDecodeWindow()
        {
            Title = "SpasiX — 🕵 DECODE HEDGING"; Width = 1400; Height = 900; MinWidth = 960; MinHeight = 600; Background = Sx.Bg;
            var lg = MainWindow.Logo(); if (lg != null) Icon = lg;
            var root = new DockPanel { Margin = new Thickness(16, 12, 16, 12), LastChildFill = true };
            var head = new StackPanel(); DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
            var t1 = new StackPanel { Orientation = Orientation.Horizontal }; t1.Children.Add(Sx.Label("🕵 DECODE HEDGING", 22, Sx.Gold, true));
            t1.Children.Add(new Border { Background = Sx.Tint(Sx.Green, 40), BorderBrush = Sx.Green, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Sx.Label("READ ONLY • COPY TRADING ONLY (not PAMM) • this studio never sends an order", 11.5, Sx.Green, true) });
            head.Children.Add(t1);
            head.Children.Add(new TextBlock { Text = "Copy his trades onto a connected MT5 account (Duramarkets or IC Markets — every terminal SpasiX is linked to is watched). Every execution lands in the pool with the basket, the hedge size and distance, the pending stops / limits (placed, moved, filled, REMOVED with the floating $ at that moment), the moving averages, the swing and the session — saved per account and day in " + HedgeDecode.Folder + ". Recording runs from start-up, also with this window closed.", Foreground = Sx.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            // the accounts + filters + files
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) }; head.Children.Add(bar);
            bar.Children.Add(Sx.Label("ACCOUNTS  ", 11, Sx.Muted, true)); bar.Children.Add(accts);
            bar.Children.Add(Sx.Label("    SYMBOL CONTAINS ", 11, Sx.Muted, true)); var sym = Sx.Input(d.Symbol, 80); bar.Children.Add(sym);
            sym.LostFocus += delegate { d.Symbol = (sym.Text ?? "").Trim(); MainWindow.SPut("hdec.sym", d.Symbol); };
            bar.Children.Add(Sx.Label("   ONLY COMMENT / MAGIC (empty = every trade) ", 11, Sx.Muted, true)); var flt = Sx.Input(d.Filter, 150); bar.Children.Add(flt);
            flt.ToolTip = "the copy service's comment or magic number, comma-separated (e.g. copy, 123456) — empty = every trade on the account (best: an account that only receives his copies)";
            flt.LostFocus += delegate { d.Filter = (flt.Text ?? "").Trim(); MainWindow.SPut("hdec.filter", d.Filter); };
            var bar2 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) }; head.Children.Add(bar2);
            var fold = Sx.Pill("📂 OPEN FOLDER", Sx.Card2); fold.Click += delegate { try { Directory.CreateDirectory(HedgeDecode.Folder); System.Diagnostics.Process.Start("explorer.exe", HedgeDecode.Folder); } catch { } }; bar2.Children.Add(fold);
            var zip = Sx.Pill("📦 ZIP FOR CLAUDE", Sx.Tint(Sx.Cyan, 60)); zip.Click += delegate { Zip(); }; bar2.Children.Add(zip);
            var imp = Sx.Pill("📥 IMPORT A VIDEO DAY (CSV)", Sx.Tint(Sx.Violet, 60)); imp.ToolTip = "a day decoded from his livestream video (the same columns) — counted in the HINTS like a copied day"; imp.Click += delegate { Import(); }; bar2.Children.Add(imp);
            bar2.Children.Add(status);
            head.Children.Add(tiles); head.Children.Add(views);
            foreach (var v in Views) { var vv = v; var b = Sx.Pill(v, Sx.Card2); b.Height = 30; b.Click += delegate { view = vv; seen = -1; Paint(); }; views.Children.Add(b); }
            var sv = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Sx.Panel };
            root.Children.Add(new Border { Child = sv, BorderBrush = Sx.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) });
            var host = new Grid { Background = Sx.Bg }; host.Children.Add(root); Content = Shell.Chrome(this, "DECODE", host); SxMenu.Attach(this, host);
            Closing += (s2, e2) => { if (Shell.Quitting) return; if (!Sx.Ask(this, "Close 🕵 DECODE HEDGING?", "The studio window closes. The RECORDING keeps running in the background (every copied execution is still saved) — open it again from HOME or the title bar.", "CLOSE THE STUDIO", "STAY", Sx.Gold)) e2.Cancel = true; };
            var tm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; tm.Tick += delegate { if (d.Version != seen || (view == "🔴 LIVE" && (DateTime.Now - paintAt).TotalSeconds >= 2)) Paint(); }; tm.Start(); Closed += delegate { tm.Stop(); };
            Paint();
        }

        List<Mt5Bridge> Links() { var hub = Shell.Hub; var l = hub != null ? hub.Links.Where(x => x.Key != "").ToList() : new List<Mt5Bridge>(); if (l.Count == 0 && Shell.Bridge != null && Shell.Bridge.Key != "") l.Add(Shell.Bridge); return l; }
        void Accounts()
        {
            accts.Children.Clear();
            var all = Pill("ALL", acct == ""); all.Click += delegate { acct = ""; seen = -1; Paint(); }; accts.Children.Add(all);
            foreach (var l in Links())
            {
                var k = l.Key; string comp; lock (l.State.Gate) comp = l.State.Account.Server; bool on = !d.Off.Contains(k);
                var b = Pill((on ? "● " : "○ ") + Short(comp) + " " + k.Split('@')[0], acct == k); b.ToolTip = k + (on ? " — recorded • right-click = stop recording it" : " — NOT recorded • right-click = record it");
                b.Click += delegate { acct = k; seen = -1; Paint(); };
                b.MouseRightButtonUp += delegate { if (d.Off.Contains(k)) d.Off.Remove(k); else d.Off.Add(k); MainWindow.SPut("hdec.off", string.Join("|", d.Off)); seen = -1; Paint(); };
                accts.Children.Add(b);
            }
        }
        static string Short(string server) { var s = server ?? ""; if (s.IndexOf("dura", StringComparison.OrdinalIgnoreCase) >= 0) return "Duramarkets"; if (s.IndexOf("icmarkets", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("ic markets", StringComparison.OrdinalIgnoreCase) >= 0) return "IC Markets"; int i = s.IndexOf('-'); return i > 0 ? s.Substring(0, i) : s; }
        static Button Pill(string t, bool sel) { var b = Sx.Pill(t, sel ? Sx.Tint(Sx.Cyan, 120) : Sx.Card2); b.Height = 26; b.FontSize = 11; if (sel) b.Foreground = Sx.Bg; return b; }

        IEnumerable<DecEvent> Ev() { return d.Events.Where(e => acct == "" || e.Account == acct); }
        static string N(double v, string f = "0.00") { return double.IsNaN(v) ? "—" : v.ToString(f, IC); }
        static string S(double v) { return double.IsNaN(v) ? "—" : v.ToString("+0.00;−0.00", IC); }
        void Paint()
        {
            seen = d.Version; paintAt = DateTime.Now; Accounts();
            foreach (Button b in views.Children) { bool sel = (string)b.Content == view; b.Background = sel ? Sx.Tint(Sx.Gold, 120) : Sx.Card2; b.Foreground = sel ? Sx.Bg : Sx.Text; }
            var ev = Ev().ToList(); var legs = ev.Where(IsLeg).ToList(); var cy = d.Cycles.Where(c => acct == "" || c.Account == acct).ToList();
            tiles.Content = Sx.Tiles(Tuple.Create("EXECUTIONS", ev.Count.ToString(IC), Sx.Cyan), Tuple.Create("LEGS", legs.Count.ToString(IC), Sx.Text), Tuple.Create("HEDGES", legs.Count(e => e.Kind == "HEDGE" || e.Note.Contains("HEDGE")).ToString(IC), Sx.Violet),
                Tuple.Create("PENDING PLACED / REMOVED", ev.Count(e => e.Kind.StartsWith("PLACED")) + " / " + ev.Count(e => e.Kind.StartsWith("REMOVED")), Sx.Amber), Tuple.Create("CYCLES DONE", cy.Count(c => c.Done).ToString(IC), Sx.Green),
                Tuple.Create("CYCLES RESULT", S(cy.Where(c => c.Done).Sum(c => c.Result)), cy.Where(c => c.Done).Sum(c => c.Result) >= 0 ? Sx.Green : Sx.Red));
            status.Text = "   " + Links().Count(l => !d.Off.Contains(l.Key)) + " account(s) recorded • " + d.Events.Count + " events in the pool";
            rows.Children.Clear();
            if (view == "🔴 LIVE") Live(ev);
            else if (view == "📋 EXECUTIONS") Table(ev.AsEnumerable().Reverse().Take(1500).ToList(), false);
            else if (view == "🛡 HEDGES & SIZING") Hedges(legs);
            else if (view == "⏳ PENDING ORDERS") Table(ev.Where(e => e.Kind.StartsWith("PLACED") || e.Kind.StartsWith("MOVED") || e.Kind.StartsWith("REMOVED") || e.Kind.StartsWith("FILL")).Reverse().Take(1500).ToList(), true);
            else if (view == "📈 MOVING AVERAGES") Mas(legs);
            else if (view == "🔁 CYCLES") Cycles(cy);
            else if (view == "💡 HINTS") Hints();
            else Video();
        }
        static bool IsLeg(DecEvent e) { return e.Kind == "FIRST LEG" || e.Kind == "HEDGE" || e.Kind == "ADD" || e.Kind.StartsWith("FILL"); }

        // ---- rows
        FrameworkElement C(string t, double w, Brush fg, bool bold = false) { return new TextBlock { Text = t, Width = w, Foreground = fg, FontSize = 12, FontFamily = Sx.Mono, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(0, 0, 8, 0), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }; }
        void Row(Brush stripe, params FrameworkElement[] cells) { var sp = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var c in cells) sp.Children.Add(c); rows.Children.Add(new Border { Child = sp, Background = Sx.CardGrad, BorderBrush = stripe, BorderThickness = new Thickness(4, 0, 0, 0), CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4, 2, 4, 2) }); }
        void Text(string t, Brush fg, double size = 13, bool bold = false) { rows.Children.Add(new TextBlock { Text = t, Foreground = fg, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 6, 10, 4) }); }
        static Brush ColOf(DecEvent e) { return e.Kind.StartsWith("REMOVED") ? Sx.Amber : e.Kind.StartsWith("PLACED") || e.Kind.StartsWith("MOVED") ? Sx.Cyan : e.Kind == "CLOSE" || e.Kind == "CYCLE FLAT" ? Sx.Gold : e.Side == "BUY" ? Sx.Green : e.Side == "SELL" ? Sx.Red : Sx.Text; }
        void Table(List<DecEvent> ev, bool pend)
        {
            Row(Sx.Line, C("TIME", 128, Sx.Muted, true), C("ACCOUNT", 120, Sx.Muted, true), C("WHAT", 150, Sx.Muted, true), C("SIDE", 92, Sx.Muted, true), C("LOTS", 54, Sx.Muted, true), C("PRICE", 82, Sx.Muted, true), C(pend ? "FROM PRICE" : "FROM LAST", 80, Sx.Muted, true), C("RATIO", 54, Sx.Muted, true),
                C("BUY / SELL LOTS", 112, Sx.Muted, true), C("FLOATING", 92, Sx.Muted, true), C(pend ? "FLOAT PLACED" : "P/L", 92, Sx.Muted, true), C("NEAREST MA", 150, Sx.Muted, true), C("NOTE", 420, Sx.Muted, true));
            foreach (var e in ev)
                Row(ColOf(e), C(e.Time.ToString("dd MMM HH:mm:ss", IC), 128, Sx.Text), C(e.Account, 120, Sx.Muted), C(e.Kind, 150, ColOf(e), true), C(e.Side, 92, ColOf(e)), C(N(e.Lots, "0.##"), 54, Sx.Text, true), C(N(e.Price), 82, Sx.Text),
                    C(pend ? S(e.DistBid) : S(e.FromLast), 80, Sx.Text), C(N(e.RatioPrev, "0.##") + (double.IsNaN(e.RatioPrev) ? "" : "×"), 54, Sx.Violet), C(N(e.BuyLots, "0.##") + " / " + N(e.SellLots, "0.##"), 112, Sx.Text), C(S(e.Floating), 92, e.Floating >= 0 ? Sx.Green : Sx.Red, true),
                    C(pend ? S(e.PlacedFloat) : S(e.Profit), 92, Sx.Text), C(e.NearMa, 150, Sx.Muted), C(e.Note, 420, Sx.Muted));
            if (ev.Count == 0) Text("nothing yet — " + (pend ? "his pending stop / limit orders appear here the moment the copy places them" : "every copied execution appears here"), Sx.Muted);
        }
        void Live(List<DecEvent> ev)
        {
            foreach (var l in Links().Where(x => acct == "" || x.Key == acct))
            {
                List<Mt5Position> ps; List<Mt5Order> os; Mt5Quote q = null; string server; double bal, eq;
                lock (l.State.Gate) { ps = l.State.Positions.Where(d.Wants).ToList(); os = l.State.Orders.Where(o => d.Symbol == "" || o.Symbol.IndexOf(d.Symbol, StringComparison.OrdinalIgnoreCase) >= 0).ToList(); server = l.State.Account.Server; bal = l.State.Account.Balance; eq = l.State.Account.Equity; var sy = ps.Select(p => p.Symbol).Concat(os.Select(o => o.Symbol)).FirstOrDefault(); if (sy != null) l.State.Quotes.TryGetValue(sy, out q); }
                double fl = ps.Sum(p => p.Profit + p.Swap + p.Commission), bl = ps.Where(p => p.Side == "BUY").Sum(p => p.Lots), sl = ps.Where(p => p.Side != "BUY").Sum(p => p.Lots);
                Text((d.Off.Contains(l.Key) ? "○ NOT RECORDED  " : "● RECORDING  ") + Short(server) + " " + l.Key + "   •   balance " + N(bal) + "  equity " + N(eq) + "   •   " + ps.Count + " copied positions: BUY " + N(bl, "0.##") + " / SELL " + N(sl, "0.##") + " (net " + N(bl - sl, "+0.##;−0.##;0") + ")   •   floating " + S(fl) + (q != null ? "   •   bid " + N(q.Bid) : ""), fl >= 0 ? Sx.Green : Sx.Red, 14, true);
                foreach (var p in ps.OrderBy(p => p.Time)) Row(p.Side == "BUY" ? Sx.Green : Sx.Red, C(p.Time.ToString("HH:mm:ss", IC), 80, Sx.Muted), C(p.Side, 60, p.Side == "BUY" ? Sx.Green : Sx.Red, true), C(N(p.Lots, "0.##"), 54, Sx.Text, true), C("@ " + N(p.Open), 100, Sx.Text), C("#" + p.Ticket, 110, Sx.Muted), C(S(p.Profit + p.Swap + p.Commission), 100, p.Profit >= 0 ? Sx.Green : Sx.Red, true), C(p.Comment, 200, Sx.Muted));
                foreach (var o in os) Row(Sx.Cyan, C("PENDING", 80, Sx.Cyan, true), C(o.Type, 110, Sx.Cyan, true), C(N(o.Lots, "0.##"), 54, Sx.Text, true), C("@ " + N(o.Price), 100, Sx.Text), C("#" + o.Ticket, 110, Sx.Muted), C(q != null ? N(Math.Abs(o.Price - (q.Bid + q.Ask) / 2)) + " from the price" : "", 200, Sx.Muted));
            }
            if (Links().Count == 0) Text("no MT5 account is connected — open MT5 with SpasiXBridge on the account that receives his copies (Duramarkets or IC Markets)", Sx.Amber, 14, true);
            Text("THE NEWEST EXECUTIONS", Sx.Gold, 12, true); Table(ev.AsEnumerable().Reverse().Take(40).ToList(), false);
        }
        void Hedges(List<DecEvent> legs)
        {
            var hs = legs.Where(e => e.Kind == "HEDGE" || e.Note.Contains("HEDGE")).ToList();
            Text("Every leg with its size against the last leg and the first one, how far from the last leg (and the last opposite leg) and how long after — the hedge sizing ladder.", Sx.Muted, 12);
            Row(Sx.Line, C("TIME", 128, Sx.Muted, true), C("CYCLE", 50, Sx.Muted, true), C("WHAT", 150, Sx.Muted, true), C("SIDE", 50, Sx.Muted, true), C("LOTS", 54, Sx.Muted, true), C("× LAST", 60, Sx.Muted, true), C("× FIRST", 64, Sx.Muted, true), C("PER $10K", 70, Sx.Muted, true), C("FROM LAST", 80, Sx.Muted, true), C("FROM OPP.", 80, Sx.Muted, true), C("SECONDS", 70, Sx.Muted, true), C("NET AFTER", 80, Sx.Muted, true), C("FLOATING", 92, Sx.Muted, true));
            foreach (var e in legs.AsEnumerable().Reverse().Take(1500))
                Row(ColOf(e), C(e.Time.ToString("dd MMM HH:mm:ss", IC), 128, Sx.Text), C("#" + e.Cycle, 50, Sx.Muted), C(e.Kind, 150, ColOf(e), true), C(e.Side, 50, ColOf(e)), C(N(e.Lots, "0.##"), 54, Sx.Text, true), C(N(e.RatioPrev, "0.##"), 60, Sx.Violet, true), C(N(e.RatioFirst, "0.##"), 64, Sx.Violet), C(N(e.LotsPer10k, "0.###"), 70, Sx.Text),
                    C(S(e.FromLast), 80, Sx.Text), C(N(e.FromLastOpp), 80, Sx.Text), C(N(e.SecLast, "0"), 70, Sx.Muted), C(N(e.BuyLots - e.SellLots, "+0.##;−0.##;0"), 80, Sx.Text), C(S(e.Floating), 92, e.Floating >= 0 ? Sx.Green : Sx.Red));
            if (hs.Count > 0) Text(hs.Count + " hedges • size " + N(hs.Where(e => !double.IsNaN(e.RatioPrev)).Select(e => e.RatioPrev).DefaultIfEmpty(double.NaN).Average(), "0.##") + "× the last leg on average • distance " + N(hs.Where(e => !double.IsNaN(e.FromLastOpp)).Select(e => e.FromLastOpp).DefaultIfEmpty(double.NaN).Average()) + " from the last opposite leg on average", Sx.Violet, 13, true);
        }
        void Mas(List<DecEvent> legs)
        {
            Text("Each leg against the moving averages at that moment (price − MA, + = above). The HINTS tab names the ones that line up with his buys / sells.", Sx.Muted, 12);
            var head = new List<FrameworkElement> { C("TIME", 128, Sx.Muted, true), C("SIDE", 50, Sx.Muted, true), C("PRICE", 82, Sx.Muted, true) }; foreach (var m in DecEvent.MaNames) head.Add(C(m, 82, Sx.Muted, true)); head.Add(C("SWING HI / LO", 150, Sx.Muted, true)); Row(Sx.Line, head.ToArray());
            foreach (var e in legs.AsEnumerable().Reverse().Take(1500))
            {
                var c = new List<FrameworkElement> { C(e.Time.ToString("dd MMM HH:mm:ss", IC), 128, Sx.Text), C(e.Side, 50, ColOf(e), true), C(N(e.Price), 82, Sx.Text) };
                foreach (var m in DecEvent.MaNames) { double v; bool has = e.Ma.TryGetValue(m, out v); double dd = has ? e.Price - v : double.NaN; c.Add(C(S(dd), 82, double.IsNaN(dd) ? Sx.Muted : dd >= 0 ? Sx.Green : Sx.Red)); }
                c.Add(C(N(e.SwingHi) + " / " + N(e.SwingLo), 150, Sx.Muted)); Row(ColOf(e), c.ToArray());
            }
        }
        void Cycles(List<DecCycle> cy)
        {
            Row(Sx.Line, C("CYCLE", 60, Sx.Muted, true), C("ACCOUNT", 120, Sx.Muted, true), C("START", 128, Sx.Muted, true), C("MINUTES", 70, Sx.Muted, true), C("FIRST", 110, Sx.Muted, true), C("LEGS B/S", 80, Sx.Muted, true), C("PENDING / REMOVED", 130, Sx.Muted, true), C("MOST LOTS", 80, Sx.Muted, true), C("WORST", 92, Sx.Muted, true), C("BEST", 92, Sx.Muted, true), C("RESULT", 100, Sx.Muted, true));
            foreach (var c in cy.AsEnumerable().Reverse())
            {
                var col = !c.Done ? Sx.Cyan : c.Result >= 0 ? Sx.Green : Sx.Red; var end = c.Done ? c.End : DateTime.Now;
                Row(col, C("#" + c.N, 60, Sx.Text, true), C(c.Account, 120, Sx.Muted), C(c.Start.ToString("dd MMM HH:mm:ss", IC), 128, Sx.Text), C(N((end - c.Start).TotalMinutes, "0"), 70, Sx.Violet, true), C(c.FirstSide + " " + N(c.FirstLots, "0.##"), 110, c.FirstSide == "BUY" ? Sx.Green : Sx.Red), C(c.Buys + " / " + c.Sells, 80, Sx.Text), C(c.Pendings + " / " + c.Removed, 130, Sx.Amber), C(N(c.MaxLots, "0.##"), 80, Sx.Text), C(S(c.Worst), 92, Sx.Red), C(S(c.Best), 92, Sx.Green), C(c.Done ? S(c.Result) : "OPEN", 100, col, true));
            }
            if (cy.Count == 0) Text("a cycle = from his first leg until he is flat again", Sx.Muted);
        }
        void Hints()
        {
            Text("The candidate rules from everything in the pool (copied days + imported video days). The more days, the surer — each line says how many times it was seen and how consistent it is.", Sx.Muted, 12);
            foreach (var h in d.Hints(acct)) Row(Sx.Gold, C(h.Item1, 260, Sx.Gold, true), C(h.Item2, 560, Sx.Text, true), C(h.Item3, 520, Sx.Muted));
        }
        void Video()
        {
            Text("Days decoded from his livestream videos (the 12-hour streams): import each day's CSV (📥 above) and it joins the pool. Files: " + Path.Combine(HedgeDecode.Folder, "video"), Sx.Muted, 12);
            try { var dir = Path.Combine(HedgeDecode.Folder, "video"); if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.csv").OrderByDescending(x => x)) Row(Sx.Violet, C(Path.GetFileName(f), 420, Sx.Text, true), C(Math.Max(0, File.ReadLines(f).Count() - 1) + " events", 120, Sx.Muted)); } catch { }
        }
        void Import()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "CSV|*.csv", Title = "a decoded video day (CSV)" }; if (dlg.ShowDialog(this) != true) return;
            int n = d.Import(dlg.FileName); status.Text = "   📥 imported: " + n + " events"; seen = -1; Paint();
        }
        void Zip()
        {
            try
            {
                Directory.CreateDirectory(HedgeDecode.Folder); var outF = Path.Combine(DataDir.Root, "decode-hedging-" + DateTime.Now.ToString("yyyy-MM-dd_HHmm", IC) + ".zip"); if (File.Exists(outF)) File.Delete(outF);
                ZipFile.CreateFromDirectory(HedgeDecode.Folder, outF); System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + outF + "\""); status.Text = "   📦 " + outF;
            }
            catch (Exception ex) { status.Text = "   ⚠ " + ex.Message; }
        }
    }
}
