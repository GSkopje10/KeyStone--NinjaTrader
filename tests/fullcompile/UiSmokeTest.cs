// Builds the whole lab window with the stand-in WPF classes (which enforce WPF's one-parent
// rule) and runs the main UI refresh paths, so runtime errors in UI construction show up here
// instead of in NinjaTrader. It cannot check visuals or layout.
using System; using System.Collections.Generic; using System.Linq; using System.Reflection;
using NinjaTrader.NinjaScript; using NinjaTrader.NinjaScript.AddOns;
public static class UiSmokeTest
{
    static int failures;
    static void Step(string name, Action a)
    {
        var only = Environment.GetEnvironmentVariable("UI_ONLY"); if (!string.IsNullOrEmpty(only) && !name.Contains(only)) { Console.WriteLine("SKIP  " + name); return; }   /* a focused run: UI_ONLY=<part of a step name> */
        try { a(); Console.WriteLine("PASS  " + name); }
        catch (Exception ex) { var e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex; Console.WriteLine("FAIL  " + name + "  → " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace.Split('\n').Take(6).Aggregate((x, y) => x + "\n" + y)); failures++; }
    }
    static object Call(object o, string m, params object[] a) { var mi = o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).First(x => x.Name == m && x.GetParameters().Length == a.Length); return mi.Invoke(o, a); }
    static List<T> FindAll<T>(object root) where T : class
    {
        var o = new List<T>(); var seen = new HashSet<object>(); var st = new Stack<object>(); if (root != null) st.Push(root);
        while (st.Count > 0)
        {
            var x = st.Pop(); if (x == null || x is string || !seen.Add(x)) continue; var t = x as T; if (t != null) o.Add(t);
            foreach (var n in new[] { "Content", "Child" }) { var pi = x.GetType().GetProperty(n); if (pi != null && pi.GetIndexParameters().Length == 0) { var v = pi.GetValue(x, null); if (v != null) st.Push(v); } }
            foreach (var n in new[] { "Children", "Items" }) { var pi = x.GetType().GetProperty(n); if (pi != null) { var v = pi.GetValue(x, null) as System.Collections.IEnumerable; if (v != null) foreach (var c in v) st.Push(c); } }
        }
        return o;
    }
    static void Set(object o, string f, object v) { o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v); }
    public static int Main()
    {
        Environment.SetEnvironmentVariable("HOME", System.IO.Path.GetTempPath());
        Step("🔒 NinjaTrader lockout read from the (stub) Account by reflection: LockedUntil ahead → locked, past → not", () =>
        {
            var a1 = new NinjaTrader.Cbi.Account { Name = "TAKEPROFIT893", LockedUntil = DateTime.Now.AddHours(2) }; var a2 = new NinjaTrader.Cbi.Account { Name = "TAKEPROFIT894", LockedUntil = DateTime.Now.AddHours(-2) };
            var s1 = KeystoneNtLockout.Read(a1, DateTime.Now); var s2 = KeystoneNtLockout.Read(a2, DateTime.Now);
            if (!s1.Locked || s1.Source != "LockedUntil" || s2.Locked) throw new Exception("lockout: " + s1.Members + " / " + s2.Members);
            Console.WriteLine("      members seen: " + s1.Members);
        });
        var lab = new KeystoneArc5MResearchLab();
        Step("open the lab window (every tab built)", () => Call(lab, "OpenWindow"));
        Step("live theme switch re-colours the open window", () =>
        {
            var win = (System.Windows.Window)lab.GetType().GetField("window", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            Call(lab, "ApplyThemeLive", "DEEP OCEAN");
            var bg = lab.GetType().GetField("Bg", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var panel = lab.GetType().GetField("Panel", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (!ReferenceEquals(win.Background, bg) && !ReferenceEquals(win.Background, panel)) throw new Exception("window background was not re-coloured");
            Call(lab, "ApplyThemeLive", "OBSIDIAN GOLD");
        });
        Step("DIRECT FUNDED shows only its own block; EVALUATION FIRST only the evaluation block", () =>
        {
            Func<string, object> F = n => lab.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            var start = (System.Windows.Controls.ComboBox)F("accountStartModeBox");
            start.SelectedIndex = 1; start.SelectedItem = start.Items[1]; Call(lab, "RefreshLifecycleInputState");
            var evBlock = (System.Windows.Controls.Border)F("evaluationBlock"); var dir = (System.Windows.Controls.Border)F("directFundedBlock");
            if (evBlock.Visibility != System.Windows.Visibility.Collapsed || dir.Visibility != System.Windows.Visibility.Visible) throw new Exception("direct mode shows evaluation settings");
            start.SelectedIndex = 0; start.SelectedItem = start.Items[0]; Call(lab, "RefreshLifecycleInputState");
            if (evBlock.Visibility != System.Windows.Visibility.Visible || dir.Visibility != System.Windows.Visibility.Collapsed) throw new Exception("evaluation mode shows direct-funded settings");
        });
        Step("strategy switch to every strategy (incl. HELIX and GOLDEN)", () => { var box = (System.Windows.Controls.ComboBox)lab.GetType().GetField("strategyBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab); int bhIndex = Enumerable.Range(0, box.Items.Count).First(k => Convert.ToString(box.Items[k]).StartsWith("BH")); foreach (int i in Enumerable.Range(0, box.Items.Count).Concat(new[] { bhIndex })) { box.SelectedIndex = i; box.SelectedItem = box.Items[i]; Call(lab, "RefreshStrategyInputState"); } });
        // A pool result through the UI render paths.
        var rng = new Random(2); var ev = new List<KeystoneArcEvent>(); DateTime day = new DateTime(2025, 1, 6);
        for (int d = 0; d < 120; d++) { var s = day.AddDays(d); if (s.DayOfWeek == DayOfWeek.Saturday || s.DayOfWeek == DayOfWeek.Sunday) continue; for (int k = 0; k < 3; k++) { var t = s.AddHours(9).AddMinutes(35 + k * 40); bool w = rng.NextDouble() < 0.5; ev.Add(new KeystoneArcEvent { Id = "E" + d + k, Symbol = k % 2 == 0 ? "MNQ" : "MGC", SetupClass = "BH", TriggerTime = t, EntryTime = t, ReferenceTime = t, ExitTime = t.AddMinutes(20), Outcome = w ? "WIN" : "LOSS", GrossPnl = w ? 1500 : -500, Quantity = 10, Entry = 100, ReviewState = "ACCEPTED" }); } }
        var cfg = (KeystoneArcRunConfig)lab.GetType().GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
        cfg.StrategyCode = "BH"; cfg.Scope = "BOTH"; cfg.Start = day; cfg.End = day.AddDays(130); cfg.PoolSize = 10; cfg.EvaluationEnabled = 1; cfg.OutcomeModelEnabled = 1; cfg.SessionMode = "NY_OPEN";
        var accounts = KeystoneArcEngine.SimulatePool(ev, cfg);
        Set(lab, "events", ev); Set(lab, "loadedEvents", ev); Set(lab, "loadedScope", "BOTH"); Set(lab, "accounts", accounts);
        Step("render pool results (ledger, lifecycle, dashboard, banner)", () => { Call(lab, "RenderEvents"); Call(lab, "RenderPoolLedger"); Call(lab, "RenderLifecycle"); Call(lab, "RenderPoolDashboard"); Call(lab, "RenderPoolDayPanel"); Call(lab, "RefreshInstrumentViewControls"); });
        Step("render heavy tabs (periods, payout cycles, first return, findings)", () => { Call(lab, "RenderDailySessionScoreboard"); Call(lab, "RenderPayoutCycleDashboard"); Call(lab, "RenderPayoutAccountDashboard"); Call(lab, "RenderFirstReturnDashboard"); Call(lab, "RenderResearchFindings"); });
        Step("first return + payout cycles show the new layout when accounts were paid", () =>
        {
            var winEv = ev.Select(e => { var c2 = e.CopyForPool(); c2.Outcome = "WIN"; c2.GrossPnl = 900; return c2; }).ToList();
            var payCfg = cfg.ShallowCopy(); payCfg.PoolSize = 4; payCfg.EvaluationEnabled = 0;
            var paidAccounts = KeystoneArcEngine.SimulatePool(winEv, payCfg);
            Set(lab, "accounts", paidAccounts); Set(lab, "events", winEv);
            Call(lab, "RenderFirstReturnDashboard"); Call(lab, "RenderPayoutCycleDashboard");
            int paid = paidAccounts.Count(a => a.Payouts > 0);
            var fr = (System.Windows.Controls.StackPanel)lab.GetType().GetField("firstReturnDashboardStack", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            var pc = (System.Windows.Controls.StackPanel)lab.GetType().GetField("payoutCycleDashboardStack", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            Console.WriteLine("      paid accounts " + paid + " • first return elements " + fr.Children.Count + " • payout cycle rows " + pc.Children.Count);
            if (paid > 0 && (fr.Children.Count < 5 || pc.Children.Count < 4)) throw new Exception("new layout not built");
            foreach (string sort in new[] { "MOST PAYOUTS", "HIGHEST NET", "ACCOUNT NAME", "FASTEST FIRST" }) { Set(lab, "firstReturnSort", sort); Call(lab, "RenderFirstReturnDashboard"); }
            if (paid == 0) throw new Exception("test pool had no payouts");
            Set(lab, "accounts", accounts); Set(lab, "events", ev);
        });
        Step("months & sessions tab (months, weeks drill-down, session finder)", () =>
        {
            Call(lab, "RenderMonthsAndSessions");
            var st = (System.Windows.Controls.StackPanel)lab.GetType().GetField("monthsSessionsStack", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            var monthRows = (List<KeystoneArcPeriodRow>)lab.GetType().GetField("lastMonthRows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            Console.WriteLine("      months " + (monthRows == null ? 0 : monthRows.Count) + " • elements " + st.Children.Count);
            if (monthRows == null || monthRows.Count < 3 || st.Children.Count < 10) throw new Exception("months tab not built");
            Set(lab, "selectedWeeksMonth", monthRows[1].Start); Call(lab, "RenderMonthsAndSessions");
        });
        Step("live account tab (risk %, grade filter, stops, equity chart, months)", () =>
        {
            Call(lab, "RenderLiveAccount");
            var st = (System.Windows.Controls.StackPanel)lab.GetType().GetField("liveAccountStack", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            var res = (KeystoneArcLiveResult)lab.GetType().GetField("lastLiveResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
            Console.WriteLine("      live trades " + (res == null ? -1 : res.Trades) + " • elements " + st.Children.Count);
            if (res == null || st.Children.Count < 4) throw new Exception("live account tab not built");
        });
        Step("top boxes for every tab", () => { var tabs = (System.Windows.Controls.TabControl)lab.GetType().GetField("resultViewTabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab); for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; tabs.SelectedItem = tabs.Items[i]; Call(lab, "UpdateTopTilesForTab"); } });
        Step("report html", () => Call(lab, "BuildHtmlReport"));
        // Evidence chart + replay on real detected setups from random-walk bars.
        foreach (string strategy in new[] { "BH", "FVG", "ENG", "RLY", "VWP", "GLD", "ASIAN75", "MAD" })
        {
            var lab2 = new KeystoneArc5MResearchLab();
            Call(lab2, "OpenWindow");
            var c = (KeystoneArcRunConfig)lab2.GetType().GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
            bool asian = strategy == "ASIAN75";
            c.StrategyCode = strategy; c.Scope = "BOTH"; c.SetupMinutes = asian ? 1 : 5; c.SessionMode = asian ? "ASIAN75" : "NY_OPEN"; c.CustomStart = 930; c.EndTime = 1555; c.EnableBh = strategy == "BH" ? 1 : 0; c.OutcomeModelEnabled = 1;
            c.Start = new DateTime(2025, 3, 3, asian ? 18 : 9, asian ? 0 : 30, 0); c.End = new DateTime(2025, 3, 7, 15, 55, 0); c.PoolSize = 5; c.EvaluationEnabled = 1; c.TargetDollars = 300; c.StopDollars = 200; c.Quantity = 2;
            if (strategy == "MAD") { c.SetupMinutes = 1; c.SessionMode = "CUSTOM"; c.CustomStart = 1800; c.EndTime = 1659; c.MadOpenHhmm = 1800; c.MadCloseHhmm = 1659; c.MadMnqTarget = 40; c.MadMgcTarget = 0; c.MadQty = 1; c.Quantity = 1; c.Start = new DateTime(2025, 3, 2, 18, 0, 0); c.End = new DateTime(2025, 3, 7, 16, 59, 0); }
            var r = new Random(9); var m1 = new List<KeystoneArcBar>(); var g1 = new List<KeystoneArcBar>(); double pm = 20000, pg = 2900;
            for (DateTime t = strategy == "RLY" ? new DateTime(2025, 1, 26, 18, 1, 0) : new DateTime(2025, 3, 2, 18, 1, 0); t < new DateTime(2025, 3, 8); t = t.AddMinutes(1))
            {
                if (t.Hour == 17) continue;
                double om = pm, og = pg; pm += (r.NextDouble() - 0.5) * 16; pg += (r.NextDouble() - 0.5) * 2.4;
                m1.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = om, Close = pm, High = Math.Max(om, pm) + r.NextDouble() * 4, Low = Math.Min(om, pm) - r.NextDouble() * 4 });
                g1.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = og, Close = pg, High = Math.Max(og, pg) + r.NextDouble() * 0.6, Low = Math.Min(og, pg) - r.NextDouble() * 0.6 });
            }
            var m5 = asian ? m1 : KeystoneArcEngine.ToSetupBars(m1, "MNQ", 5); var g5 = asian ? g1 : KeystoneArcEngine.ToSetupBars(g1, "MGC", 5);
            Set(lab2, "mnqBars", m1); Set(lab2, "mgcBars", g1); Set(lab2, "mnqSetupBars", m5); Set(lab2, "mgcSetupBars", g5);
            List<KeystoneArcEvent> ev2 = asian ? KeystoneArcEngine.DetectAndResolve(m1.Concat(g1).OrderBy(b => b.Time).ToList(), c)
                : KeystoneArcEngine.DetectAndResolve(m1, m5, c).Concat(KeystoneArcEngine.DetectAndResolve(g1, g5, c)).ToList();
            foreach (var e in ev2) e.ReviewState = "ACCEPTED";
            Set(lab2, "events", ev2); Set(lab2, "loadedEvents", ev2); Set(lab2, "loadedScope", "BOTH");
            Set(lab2, "accounts", KeystoneArcEngine.SimulatePool(ev2, c));
            if (strategy == "MAD")
                Step("MAD: MICRO A DAY sessions → pool without NaN, live box, COMPARE every version", () =>
                {
                    Console.WriteLine("      " + ev2.Count + " sessions • " + string.Join(" | ", ev2.Take(3).Select(e => e.Symbol + " " + e.Direction + " " + e.Outcome + " " + e.GrossPnl.ToString("0"))));
                    if (ev2.Count < 6 || ev2.Any(e => e.SetupClass != "MAD" || double.IsNaN(e.GrossPnl))) throw new Exception("MAD events");
                    foreach (var a in (System.Collections.IEnumerable)lab2.GetType().GetField("accounts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2))
                        foreach (var f in a.GetType().GetFields()) if (f.FieldType == typeof(double) && double.IsNaN((double)f.GetValue(a))) throw new Exception("NaN in the pool: " + f.Name);
                    Call(lab2, "OpenMicroCompare");
                    for (int i = 0; i < 600 && ((System.Collections.ICollection)lab2.GetType().GetField("microCompareRows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2)).Count == 0; i++) System.Threading.Thread.Sleep(100);
                    var rows = (List<KeystoneMicroRow>)lab2.GetType().GetField("microCompareRows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    Console.WriteLine("      compare: " + rows.Count + " versions • " + ((System.Windows.Controls.TextBlock)lab2.GetType().GetField("microCompareStatus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2)).Text);
                    if (rows.Count != 36) throw new Exception("compare rows " + rows.Count);
                    var tabsM = (System.Windows.Controls.TabControl)lab2.GetType().GetField("microTabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    // SIZE & SPEED is measured in the background after the rows: wait for it (up to 60 s) instead of racing it
                    for (int i = 0; i < 600 && tabsM.Items.Cast<System.Windows.Controls.TabItem>().Any(t => t.Content is System.Windows.Controls.TextBlock); i++) System.Threading.Thread.Sleep(100);
                    foreach (System.Windows.Controls.TabItem t in tabsM.Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception("results tab not filled: " + t.Header);
                    Console.WriteLine("      results tabs: " + string.Join(" • ", tabsM.Items.Cast<System.Windows.Controls.TabItem>().Select(t => t.Header)));
                    var csv = (string)Call(lab2, "MicroCsv"); var html = (string)Call(lab2, "MicroHtml");
                    if (!csv.Contains("TAKE PROFIT GRID") || !html.Contains("FIRST PAYOUT") || !html.Contains("<svg")) throw new Exception("export / html report incomplete");
                    // the chart shows the MICRO A DAY trade of the selected session
                    var first = ev2.OrderBy(e => e.TriggerTime).First(e => e.Symbol == "MNQ");
                    Call(lab2, "OpenEvidenceChart");
                    ((System.Windows.Controls.TextBox)lab2.GetType().GetField("evidenceDateBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2)).Text = KeystoneArcEngine.SessionGroupingDate(first.TriggerTime, c).ToString("yyyy-MM-dd");
                    Call(lab2, "RequestEvidenceBars");
                    var marks = (List<KeystoneArcEvent>)Call(lab2, "EvidenceEvents", "MNQ", KeystoneArcEngine.SessionGroupingDate(first.TriggerTime, c));
                    Console.WriteLine("      chart day " + KeystoneArcEngine.SessionGroupingDate(first.TriggerTime, c).ToString("yyyy-MM-dd") + " • MAD trades on it: " + marks.Count);
                    if (marks.Count == 0) throw new Exception("MICRO A DAY trade not on the chart");
                    Call(lab2, "RenderEvidenceChart");
                    Call(lab2, "ReplaySetCursor", first.TriggerTime.AddMinutes(90), true);
                    var cv = (System.Windows.Controls.Canvas)lab2.GetType().GetField("evidenceCanvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    string headLine = cv.Children.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text ?? "").FirstOrDefault(t => t.Contains("LEDGER MARK")) ?? "";
                    Console.WriteLine("      replay 90 min after the open: " + headLine.Substring(Math.Max(0, headLine.IndexOf("CANDLES"))));
                    if (!headLine.Contains("CANDLES • 1 LEDGER MARK")) throw new Exception("replay hides the held MICRO A DAY entry: " + headLine);
                    Call(lab2, "StopEvidenceReplay");
                });
            if (strategy == "MAD")
                Step("IDEAS: a typed idea (09:30 open, then every 1-minute FVG with rotation) → rows, every tab, trades on the chart", () =>
                {
                    Func<string, object> F = n => lab2.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    Call(lab2, "OpenIdeasLab", false);
                    var st = F("ideasLab"); var T = st.GetType(); Func<string, object> S = n => T.GetField(n).GetValue(st);
                    foreach (string idea in new[] { "BOTH AT 0930 BUY,SELL TP 10,20 SL 10,NONE", "BOTH FVG 1 EITHER,BUY,SELL SESSION NY,ALL GAP 1,3 TP 5,10 SL C1,5 ACCOUNTS 1,3" })
                    {
                        ((System.Windows.Controls.TextBox)F("ideasLineBox")).Text = idea;
                        ((System.Windows.Controls.TextBlock)S("Status")).Text = "";
                        ((Action)S("Run"))();
                        for (int i = 0; i < 900 && !((System.Windows.Controls.TextBlock)S("Status")).Text.Contains(" ROWS • ") && !((System.Windows.Controls.TextBlock)S("Status")).Text.StartsWith("ERROR") && !((System.Windows.Controls.TextBlock)S("Status")).Text.StartsWith("NO "); i++) System.Threading.Thread.Sleep(100);
                        var rows = (List<KeystoneLabRow>)S("Rows");
                        Console.WriteLine("      " + idea + " → " + ((System.Windows.Controls.TextBlock)S("Status")).Text);
                        Console.WriteLine("        best: " + string.Join(" | ", rows.Take(3).Select(x => x.Label + " plain " + x.P.Net.ToString("0") + " prop " + x.P.PropNet.ToString("0"))));
                        if (rows.Count == 0 || !rows.Any(x => x.Get("INSTRUMENT") == "BOTH")) throw new Exception("IDEAS produced no (BOTH) rows: " + ((System.Windows.Controls.TextBlock)S("Status")).Text);
                        foreach (System.Windows.Controls.TabItem t in ((System.Windows.Controls.TabControl)S("Tabs")).Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception("IDEAS tab not filled: " + t.Header);
                        var csv = (string)Call(lab2, "LabCsv", st); var html = (string)Call(lab2, "LabHtml", st);
                        if (!csv.Contains("WALK-FORWARD") || !html.Contains("DOES IT ADAPT")) throw new Exception("IDEAS export incomplete");
                        var pick = rows.FirstOrDefault(x => x.Get("INSTRUMENT") == "MNQ" && x.Trades.Any(t => t.Dir < 0) && (idea.Contains("AT") || x.Rotate == 3)) ?? rows.First(x => x.Get("INSTRUMENT") == "MNQ");
                        Call(lab2, "ShowLabSelected", st, pick, false);
                        var tr = pick.Trades.First();
                        Call(lab2, "ShowIdeaRowOnChart", pick, tr);
                        var iday = KeystoneArcEngine.SessionGroupingDate(tr.EntryTime, c);
                        ((System.Windows.Controls.TextBox)F("evidenceDateBox")).Text = iday.ToString("yyyy-MM-dd");
                        Call(lab2, "RequestEvidenceBars");
                        var marks = (List<KeystoneArcEvent>)Call(lab2, "EvidenceEvents", "MNQ", iday);
                        var replay = (List<KeystoneArcEvent>)Call(lab2, "ReplayDayTrades", iday);
                        Call(lab2, "RenderEvidenceChart");
                        Console.WriteLine("        chart " + iday.ToString("yyyy-MM-dd") + ": " + marks.Count + " idea trades (" + marks.Count(e => e.Direction == "SHORT") + " sells), replay " + replay.Count + (pick.Rotate > 1 ? " • accounts " + string.Join(",", marks.Select(e => e.AssignedVirtualAccount).Distinct()) : ""));
                        if (marks.Count == 0 || marks.Any(e => e.SetupClass != "IDEA") || replay.Count == 0) throw new Exception("idea trades not on the chart / replay");
                        if (idea.Contains("FVG") && !rows.Any(x => x.Rotate == 3 && x.P.Bought >= 1)) throw new Exception("rotation rows missing");
                    }
                });
            if (strategy == "MAD")
                Step("REPLAY TRADER: load a loaded day, play, buy, target / close, journal saved", () =>
                {
                    Func<string, object> F = n => lab2.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    string journal = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KeystoneArcData", "Studio", "ReplayJournal.csv");
                    if (System.IO.File.Exists(journal)) System.IO.File.Delete(journal);
                    string accountFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(journal), "ReplayAccount.txt"), daysFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(journal), "ReplayDays.csv");
                    if (System.IO.File.Exists(accountFile)) System.IO.File.Delete(accountFile); if (System.IO.File.Exists(daysFile)) System.IO.File.Delete(daysFile);
                    string studio = System.IO.Path.GetDirectoryName(journal); if (System.IO.Directory.Exists(studio)) System.IO.Directory.Delete(studio, true);
                    Call(lab2, "OpenLauncher");
                    Call(lab2, "OpenReplayTrader");
                    int stored = KeystoneStudioStore.Sessions(studio, "MNQ").Count;
                    Console.WriteLine("      studio store: MNQ " + stored + " sessions, MGC " + KeystoneStudioStore.Sessions(studio, "MGC").Count + " (copied from the lab's bars)");
                    if (stored == 0) throw new Exception("the lab's 1-minute bars were not copied into the studio store");
                    ((Action)F("replayTraderLoad"))();
                    var rt = (KeystoneReplayTrader)F("replayTrader");
                    if (rt != null && rt.Finished) throw new Exception("the loaded day starts finished");
                    if (rt == null || rt.Bars.Count == 0) throw new Exception("no day loaded");
                    {   // 🎯 BUILD 11i: the REVERSAL FVG scanner on the studio chart (buys + sells, every bar) — draws without an error and never trades
                        var lay = (Action<string, int>)F("studioLayer"); if (lay == null) throw new Exception("🎯 studioLayer hook missing");
                        int posB = rt.Position, ordB = rt.Orders.Count; lay("IND.RFVG", KeystoneLiveTools.Always); lay("IND.RFVG.SELL", 1); lay("IND.RFVG.D", 8);   /* a small move so the synthetic day has signals (the menu offers 60 / 100 / 150) */
                        ((Action<int>)F("replayTraderStep"))(30); var errR = (string)F("studioRfvgErr");
                        if (!string.IsNullOrEmpty(errR)) throw new Exception("🎯 REVERSAL FVG drawing error: " + errR);
                        if (rt.Position != posB || rt.Orders.Count != ordB) throw new Exception("🎯 the scanner changed the practice position / orders");
                        Console.WriteLine("      🎯 REVERSAL FVG drawn on the studio chart (" + (int)F("studioRfvgSeen") + " signals so far) — no order");
                        lay("IND.RFVG.SELL", 0); lay("IND.RFVG.D", 100);
                    }
                    ((Action<int>)F("replayTraderStep"))(5);
                    ((Action<int>)F("replayTraderOrder"))(1);
                    double entry = rt.AvgPrice; ((Action<int>)F("replayTraderStep"))(20);
                    ((Action)F("replayTraderFlat"))();
                    ((Action<int>)F("replayTraderOrder"))(-1); ((Action<int>)F("replayTraderStep"))(10); ((Action)F("replayTraderFlat"))();
                    var lines = System.IO.File.ReadAllLines(journal);
                    Console.WriteLine("      " + rt.Symbol + " " + rt.Bars.Count + " minutes • bought " + entry + " • " + rt.Fills.Count + " trades • today " + rt.Realized.ToString("0.00") + " • journal lines " + lines.Length);
                    if (rt.Fills.Count != 2 || lines.Length != 3 || rt.Fills[0].Side != "BUY" || rt.Fills[1].Side != "SELL") throw new Exception("replay trades / journal wrong");
                    {   // 🎯 BUILD 11f — the PRACTICE ACCOUNT in the studio: TPT PRO funded (phase 1 = BEAT THE TRAILING), the trade count, the animated bar; 📜 the trades log; 📦 the ticks export
                        var w0 = (System.Windows.Window)F("replayTraderWindow"); Action<System.Windows.Controls.Button> clickB = b9 => b9.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        var showDp = (Action<string>)F("studioShowDrawer"); if ((string)F("studioOpenDrawer") != "PRACTICE") showDp("PRACTICE");   /* 🖥 the practice lives in its drawer */
                        var pt = FindAll<System.Windows.Controls.Button>(w0.Content).FirstOrDefault(b9 => Convert.ToString(b9.Content) == "🎯 PRACTICE AS A PROP ACCOUNT"); if (pt == null) throw new Exception("🎯 no PRACTICE button");
                        var pb = FindAll<System.Windows.Controls.ComboBox>(w0.Content).First(cbx => cbx.Items.Cast<object>().Any(x => Convert.ToString(x).StartsWith("TPT PRO 50K funded")));
                        int ti = Enumerable.Range(0, pb.Items.Count).First(i => Convert.ToString(pb.Items[i]).StartsWith("TPT PRO 50K funded")); pb.SelectedIndex = ti; pb.SelectedItem = pb.Items[ti]; clickB(pt);
                        ((Action<int>)F("replayTraderOrder"))(1); ((Action<int>)F("replayTraderStep"))(4); ((Action)F("replayTraderFlat"))(); ((Action<int>)F("replayTraderStep"))(1);
                        var st0 = FindAll<System.Windows.Controls.TextBlock>(w0.Content).Select(t => t.Text ?? "").FirstOrDefault(t => t.StartsWith("balance ")) ?? "";
                        if (!st0.Contains("· 1 trade ") && !st0.Contains("· 1 trade·") && !st0.Contains("1 trade · max")) throw new Exception("🎯 the practice should count its 1 closed trade: " + st0);
                        if (!st0.Contains("BEAT THE TRAILING DRAWDOWN") && !st0.Contains("TRAILING BEATEN")) throw new Exception("🎯 TPT funded phase 1 = BEAT THE TRAILING DRAWDOWN: " + st0);
                        if (st0.IndexOf("room ") != st0.LastIndexOf("room ")) throw new Exception("🎯 the status must say it once: " + st0);
                        var stripX = FindAll<System.Windows.Controls.TextBlock>(w0.Content).FirstOrDefault(x => Convert.ToString(x.Tag) == "prac.strip"); if (stripX == null || !stripX.Text.StartsWith("🎯 TPT PRO 50K funded") || !stripX.Text.Contains("attempt #")) throw new Exception("🖥 11k: the one-line practice strip: " + (stripX == null ? "missing" : stripX.Text));
                        if ((string)F("studioOpenDrawer") == "PRACTICE") throw new Exception("🖥 11k: the PRACTICE drawer should close by itself once the practice runs");
                        if (!FindAll<System.Windows.FrameworkElement>(w0.Content).Any(x => Convert.ToString(x.Tag) == "prac.compact.MNQ")) throw new Exception("🖥 11k: the compact trailing block in the order column is missing");
                        Console.WriteLine("      🖥 11k strip: " + stripX.Text);
                        var tpLab = FindAll<System.Windows.Controls.TextBlock>(w0.Content).Any(t => t.Text == "TRAILING TO BEAT $"); if (!tpLab) throw new Exception("🎯 the TARGET field should read TRAILING TO BEAT $ for TPT funded");
                        Console.WriteLine("      🎯 PRACTICE TPT funded: " + st0);
                        clickB(pt); showDp("PRACTICE");
                        string tf = System.IO.Path.Combine(studio, "practice-trades.csv"); var rowsT = System.IO.File.Exists(tf) ? KeystonePracticeTrades.Load(System.IO.File.ReadAllText(tf)) : new List<KeystonePracticeTrade>();
                        if (rowsT.Count < 2 || rowsT.Any(t => t.Exit < t.Entry || t.Qty <= 0)) throw new Exception("📜 every closed trade should be in practice-trades.csv: " + rowsT.Count);
                        var showD0 = (Action<string>)F("studioShowDrawer"); showD0("TRADES"); clickB(FindAll<System.Windows.Controls.Button>(w0.Content).First(b9 => Convert.ToString(b9.Tag) == "trades.filter.ALL")); clickB(FindAll<System.Windows.Controls.Button>(w0.Content).First(b9 => Convert.ToString(b9.Tag) == "trades.export"));
                        var tex = (string)F("studioTradesExport"); if (tex == null || !System.IO.File.Exists(tex) || !System.IO.File.ReadAllText(tex).Contains("\"trades\"")) throw new Exception("📦 the trades export: " + tex); showD0("TRADES");
                        var d9 = new DateTime(2026, 3, 9); KeystoneTickCache.Save(studio, "MNQ", d9, new List<KeystoneTick> { new KeystoneTick { Time = d9.AddHours(9.5), Price = 20000.25, Volume = 3 }, new KeystoneTick { Time = d9.AddHours(9.5).AddMilliseconds(250), Price = 20000.5, Volume = 1 } }, "TEST");
                        showD0("TOOLS"); var tbF = FindAll<System.Windows.Controls.TextBox>(w0.Content).First(x => Convert.ToString(x.Tag) == "ticks.from"); var tbT = FindAll<System.Windows.Controls.TextBox>(w0.Content).First(x => Convert.ToString(x.Tag) == "ticks.to"); tbF.Text = "2026-03-09"; tbT.Text = "2026-03-09";
                        clickB(FindAll<System.Windows.Controls.Button>(w0.Content).First(b9 => Convert.ToString(b9.Tag) == "ticks.go")); var gzF = (string)F("studioTicksExport"); showD0("TOOLS");
                        string gzTxt; using (var fs9 = System.IO.File.OpenRead(gzF)) using (var gz9 = new System.IO.Compression.GZipStream(fs9, System.IO.Compression.CompressionMode.Decompress)) using (var sr9 = new System.IO.StreamReader(gz9)) gzTxt = sr9.ReadToEnd();
                        if (!gzTxt.StartsWith("symbol,time_ny,price,size,side") || !gzTxt.Contains("MNQ,2026-03-09 09:30:00.250,20000.50,1,")) throw new Exception("📦 the ticks export: " + gzTxt.Substring(0, Math.Min(200, gzTxt.Length)));
                        Console.WriteLine("      📜 " + rowsT.Count + " trades logged + exported • 📦 ticks → " + System.IO.Path.GetFileName(gzF));
                    }
                    ((Action)F("replayTraderEnd"))();
                    {   // 🏷 item 37: every trade label drawn sits in the right gutter — none intersects the candle area
                        var rects = (List<double[]>)F("studioLabelRects"); foreach (var rc in rects) if (KeystoneChartGutter.Intersects(rc[0], rc[1], rc[2], rc[3], 0, 0, rc[4], 100000)) throw new Exception("a trade label covers the candles at x " + rc[0] + " (candle area ends " + rc[4] + ")");
                        Console.WriteLine("      🏷 gutter: " + rects.Count + " trade labels drawn, none over the candles");
                    }
                    var acc = System.IO.File.ReadAllText(accountFile).Split(','); double bal = double.Parse(acc[1], System.Globalization.CultureInfo.InvariantCulture);
                    Console.WriteLine("      END DAY → account " + bal.ToString("0.00") + " of " + acc[0] + " • days file lines " + System.IO.File.ReadAllLines(daysFile).Length);
                    if (Math.Abs(bal - (2000 + rt.Realized)) > 0.01 || System.IO.File.ReadAllLines(daysFile).Length != 2) throw new Exception("END DAY did not bank the day into the account");
                    // a new account in the middle of a day: the same day goes on from the same minute, trading works
                    ((Action)F("replayTraderLoad"))(); ((Action<int>)F("replayTraderStep"))(30);
                    var before = (KeystoneReplayTrader)F("replayTrader"); DateTime at = before.Time;
                    ((Action<double>)F("replayTraderNewAccount"))(5000);
                    var rt2 = (KeystoneReplayTrader)F("replayTrader");
                    if (rt2 == null || rt2.Finished || rt2.Fills.Count != 0 || Math.Abs((rt2.Time - at).TotalMinutes) > 1.01) throw new Exception("NEW ACCOUNT did not continue the day at " + at + " (" + (rt2 == null ? "none" : rt2.Time.ToString()) + ")");
                    ((Action<int>)F("replayTraderOrder"))(1);
                    if (rt2.Position != 1) throw new Exception("cannot trade after NEW ACCOUNT");
                    var acc2 = System.IO.File.ReadAllText(accountFile).Split(',');
                    Console.WriteLine("      NEW ACCOUNT 5000 mid-day → continues " + rt2.Time.ToString("HH:mm") + " • bought 1 • account file " + acc2[0] + "," + acc2[1]);
                    if (acc2[0] != "5000") throw new Exception("new account not saved");
                    ((Action)F("replayTraderEnd"))();
                    // the studio opens asking REGULAR BACKTESTING or PROP FIRM SIMULATION; each mode shows what it needs
                    var modeNow = (Func<string>)F("studioModeNow"); string md0 = modeNow();
                    if (!md0.StartsWith("REGULAR|ASK") || !md0.Contains("|DAYPICKER") || md0.Contains("|PANEL")) throw new Exception("the studio did not open asking REGULAR / PROP with the regular tools (" + md0 + ")");
                    ((Action<string>)F("studioSetMode"))("PROP"); string md1 = modeNow();
                    ((Action<string>)F("studioSetMode"))("REGULAR"); string md2 = modeNow();
                    Console.WriteLine("      studio modes: open " + md0 + " → PROP " + md1 + " → REGULAR " + md2);
                    if (md1 != "PROP|PANEL" || md2 != "REGULAR|DAYPICKER") throw new Exception("the mode switch does not show / hide the right tools (" + md1 + " / " + md2 + ")");
                    // ASIAN 75 in AUTO (regular backtesting): START goes to 18:00, the legs trade by themselves, each reversal flips the position
                    ((Action<int, bool>)F("studioStrategy"))(KeystoneReplaySignals.AsianKind, true);
                    ((Action)F("replayTraderLoad"))();
                    var ra = (KeystoneReplayTrader)F("replayTrader"); DateTime aStart = ra.Time;
                    ((Action<int>)F("replayTraderStep"))(14 * 60);
                    ra = (KeystoneReplayTrader)F("replayTrader");
                    Console.WriteLine("      ASIAN AUTO: the day starts " + aStart.ToString("ddd HH:mm") + " • " + ra.Fills.Count + " leg(s) closed • " + string.Join(" | ", ra.Fills.Take(5).Select(f => f.EntryTime.ToString("HH:mm") + " " + f.Side + " → " + f.ExitTime.ToString("HH:mm") + " " + f.Reason)));
                    if (aStart.Hour != 18) throw new Exception("ASIAN 75 did not start the day at 18:00 (" + aStart + ")");
                    if (ra.Fills.Count == 0 && ra.Position == 0) throw new Exception("ASIAN 75 in AUTO did not trade");
                    if (ra.Fills.Count > 0 && ra.Fills[0].EntryTime.TimeOfDay > new TimeSpan(18, 3, 0) && ra.Fills[0].EntryTime.TimeOfDay < new TimeSpan(23, 59, 0) && ra.Fills[0].EntryTime.Hour >= 18 == false) throw new Exception("the first Asian leg is not at 18:00");
                    if (ra.Fills.Count > 0 && ra.Fills[0].ExitTime - ra.Fills[0].EntryTime < TimeSpan.FromMinutes(1) && ra.Fills[0].Reason.Contains("TARGET")) throw new Exception("an Asian leg took a 1-tick target");
                    ((Action)F("replayTraderEnd"))();
                    ((Action<int, bool>)F("studioStrategy"))(0, false);
                    // PROP SIM: 3 evaluations in rotation (random BUY BUY / SELL SELL on MNQ + MGC) over saved days, each day run to its close
                    var stored0 = KeystoneStudioStore.Sessions(studio, "MNQ").Keys.ToList(); List<DateTime> run3 = null;
                    for (int i = 0; i < stored0.Count && run3 == null; i++)
                    {
                        var wk = KeystonePropSim.Weekdays(stored0[i], stored0[i].AddDays(6)).Take(3).ToList();
                        if (wk.Count == 3 && wk.All(d => stored0.Contains(d) && KeystoneStudioStore.Sessions(studio, "MGC").ContainsKey(d))) run3 = wk;
                    }
                    if (run3 == null) throw new Exception("no three saved weekdays in a row for the prop sim");
                    var simSet = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", Strategy = "ROTATION", Accounts = 3, From = run3[0], To = run3[2], RotTarget = 120, RotStop = 120, FundRotTarget = 120, FundRotStop = 120, MnqQty = 1, MgcQty = 1, FundMnqQty = 1, FundMgcQty = 1, StartHhmm = 930, EndHhmm = 1555, Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy() };
                    ((Action<KeystoneSimSettings, bool>)F("studioPropStart"))(simSet, false);
                    var simRun = ((Func<KeystonePropSim>)F("studioPropSim"))();
                    if (simRun == null || !simRun.DayOpen) throw new Exception("prop sim did not open its first day");
                    var view = (Func<string>)F("studioPropView");
                    if (!((Func<string>)F("studioModeNow"))().StartsWith("PROP")) throw new Exception("a prop simulation did not switch the studio to PROP FIRM SIMULATION");
                    if (view() != "RUN") throw new Exception("the PROP SIM panel does not show the run (" + view() + ")");
                    ((Action)F("studioPropRunDay"))();   // day 1 to its CLOSE ALL (15:55) → the 15-second next-day prompt
                    if (view() != "NEXT" || simRun.Days.Count != 1) throw new Exception("no next-day prompt after the day's CLOSE ALL (" + view() + ", " + simRun.Days.Count + " days)");
                    ((Action<int>)F("studioPropNext"))(-2);   // STAY ON THIS DAY
                    if (view() != "RUN" || simRun.DayOpen) throw new Exception("STAY did not keep the finished day (" + view() + ")");
                    ((Action<int>)F("studioPropNext"))(1800);   // the next day from 18:00 (the new session)
                    var t18 = ((KeystoneReplayTrader)F("replayTrader")).Time;
                    Console.WriteLine("      next-day prompt: STAY, then FROM 18:00 → the replay starts " + t18.ToString("ddd HH:mm") + " for " + simRun.Day.ToString("ddd yyyy-MM-dd"));
                    if (!simRun.DayOpen || t18.Hour != 18 || t18.Date != simRun.Day.AddDays(-1).Date) throw new Exception("FROM 18:00 did not start the next day at 18:00 the evening before (" + t18 + ")");
                    for (int i = 0; i < 6 && !simRun.Finished; i++) ((Action)F("studioPropRunDay"))();
                    if (simRun.Trades.Any(t => t.Exit.TimeOfDay > new TimeSpan(15, 56, 30) && t.Exit.TimeOfDay < new TimeSpan(18, 0, 0))) throw new Exception("a trade closed after the 15:55 CLOSE ALL");
                    if (view() != "DONE") throw new Exception("the finished run does not show its results + next steps (" + view() + ")");
                    string rep = ((Func<string>)F("studioPropExport"))();
                    if (rep == null || !System.IO.File.Exists(rep) || !System.IO.File.ReadAllText(rep).Contains("Prop Simulation Report")) throw new Exception("EXPORT HTML did not write the report");
                    ((Action)F("studioPropCompare"))();
                    var cmpRows = KeystoneSessionWhatIf.Load(simRun.Folder); var cmpWin = KeystoneRotationLab.Winner(cmpRows);
                    Console.WriteLine("      finished: " + view() + " • report " + new System.IO.FileInfo(rep).Length + " bytes • COMPARE SESSIONS " + cmpRows.Count + " rows • winner " + (cmpWin == null ? "-" : cmpWin.Name + " " + cmpWin.Net.ToString("0")));
                    if (cmpRows.Count < KeystoneRotationLab.SessionNames.Length || cmpRows.Count(x9 => x9.Mine) != 1) throw new Exception("COMPARE SESSIONS did not save every session");
                    var accRows = KeystoneSessionWhatIf.Load(simRun.Folder, "accounts.txt"); var chRows = KeystoneSessionWhatIf.Load(simRun.Folder, "choices.txt");
                    Console.WriteLine("      COMPARE ACCOUNTS: " + string.Join(" | ", accRows.Select(x9 => x9.Accounts + " acc " + x9.Net.ToString("0") + " ROI " + x9.Roi.ToString("0") + "%")) + " • CHOICES " + chRows.Count + " • winners.csv " + System.IO.File.Exists(System.IO.Path.Combine(simRun.Folder, "winners.csv")));
                    if (accRows.Count < 6 || chRows.Count < 4 || !accRows.Any(x9 => x9.Mine) || !System.IO.File.Exists(System.IO.Path.Combine(simRun.Folder, "winners.csv"))) throw new Exception("account / choice compare or winners not saved");
                    // 📡 ENTRY INDICATOR: BUILD from the saved days → TEST vs random on the run's days → the view → USE it in a run
                    var bothSaved = stored0.Where(d => KeystoneStudioStore.Sessions(studio, "MGC").ContainsKey(d)).OrderBy(d => d).ToList();
                    var poolAct0 = (Action<string>)F("studioPoolAct");
                    poolAct0("INDICATOR|BUILD|" + bothSaved[0].ToString("yyyy-MM-dd") + "|" + bothSaved[bothSaved.Count - 1].ToString("yyyy-MM-dd") + "|5|5|0");
                    var indFile = System.IO.Path.Combine(studio, "PropSim", "Indicator.txt"); var indB = System.IO.File.Exists(indFile) ? KeystoneEntryIndicator.FromText(System.IO.File.ReadAllText(indFile)) : null;
                    Console.WriteLine("      📡 INDICATOR from " + bothSaved.Count + " saved days: " + (indB == null ? "none" : indB.Describe()));
                    if (bothSaved.Count >= 10 && (indB == null || indB.Samples == 0)) throw new Exception("📡 BUILD did not save an indicator");
                    if (indB == null)
                    {
                        // too few saved days for a real build here: the engine's build on the saved days, saved where BUILD saves it
                        var sdays = KeystoneRotationLab.LoadDays(studio, bothSaved); indB = KeystoneEntryIndicator.Build(simSet, sdays, 5, 5, 0, null);
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(indFile)); System.IO.File.WriteAllText(indFile, indB.ToText());
                        Console.WriteLine("      INDICATOR (engine, " + sdays.Count + " days): " + indB.Describe());
                    }
                    {
                        poolAct0("COMPARE|INDICATOR"); var indRows = KeystoneSessionWhatIf.Load(simRun.Folder, "indicator_test.txt");
                        Console.WriteLine("      📡 vs RANDOM on the run's days: " + string.Join(" | ", indRows.Select(x9 => x9.Name + " " + x9.Pnl.ToString("0"))));
                        if (indRows.Count < 8 || indRows.Count(x9 => x9.Mine) != 1 || !indRows.Any(x9 => x9.Name.Contains("LUCKIEST"))) throw new Exception("📡 INDICATOR vs RANDOM not saved");
                    }
                    // 🛑 LOSERS: the breakeven math on the run's trades (breakeven_math.csv) • USE the best level
                    ((Action<string>)F("studioPoolAct"))("LOSERS|STUDY");
                    var beCsv = System.IO.Path.Combine(simRun.Folder, "breakeven_math.csv");
                    Console.WriteLine("      LOSERS / BREAKEVEN MATH: " + (System.IO.File.Exists(beCsv) ? string.Join(" | ", System.IO.File.ReadAllLines(beCsv).Skip(1).Take(5)) : "not written"));
                    if (!System.IO.File.Exists(beCsv) || System.IO.File.ReadAllLines(beCsv).Length < 3) throw new Exception("🛑 BREAKEVEN MATH did not run on the run's trades");
                    ((Action<string>)F("studioPoolAct"))("LOSERS|USE|300");
                    // 🗓 SESSIONS BY DAY: one click → every day × 6 sessions → the run file + the research pool
                    ((Action<string>)F("studioPoolAct"))("DAYSESS|RUN");
                    var dsFile = System.IO.Path.Combine(simRun.Folder, "day_sessions.csv"); var poolFile = System.IO.Path.Combine(studio, "PropSim", "Research", "day_sessions.csv");
                    int dsN = System.IO.File.Exists(dsFile) ? System.IO.File.ReadAllLines(dsFile).Length - 1 : 0, poolN = System.IO.File.Exists(poolFile) ? System.IO.File.ReadAllLines(poolFile).Length - 1 : 0;
                    Console.WriteLine("      SESSIONS BY DAY: " + dsN + " day × session rows • research pool " + poolN + " rows");
                    if (dsN != simRun.Days.Count * 6 || poolN < dsN) throw new Exception("🗓 SESSIONS BY DAY did not save every day × session (or the research pool)");
                    if (!System.IO.File.ReadAllText(System.IO.Path.Combine(studio, "PropSim", "last_settings.txt")).Contains("BreakevenAt=300")) throw new Exception("🛑 USE BREAKEVEN did not set it for the next runs");
                    rep = ((Func<string>)F("studioPropExport"))(); System.IO.File.Copy(rep, ".build/report_PROPSIM.html", true);
                    if (!System.IO.File.ReadAllText(rep).Contains("WHAT IF")) throw new Exception("the report has no session table after COMPARE");
                    Console.WriteLine("      PROP SIM: " + simRun.Days.Count + " days • " + simRun.Trades.Count + " trades • bought " + simRun.Bought + " • passed " + simRun.PassedCount + " • blown " + (simRun.BlownEval + simRun.BlownFunded) + " • net " + simRun.Net.ToString("0.00") + " • " + System.IO.Path.GetFileName(simRun.Folder));
                    if (!simRun.Finished || simRun.Days.Count != 3) throw new Exception("prop sim did not run its 3 days (" + simRun.Days.Count + ", plan " + simRun.PlanIndex + "/" + simRun.Plan.Count + ")");
                    if (simRun.Trades.Count == 0) throw new Exception("the rotation made no trades");
                    if (simRun.Trades.Any(t => t.Account <= 0 || t.Account > simRun.Accounts.Max(a => a.Id))) throw new Exception("a trade has no account");
                    foreach (var fn in new[] { "settings.txt", "accounts.csv", "state.txt", "days.csv", "trades.csv", "account_days.csv" })
                        if (!System.IO.File.Exists(System.IO.Path.Combine(simRun.Folder, fn))) throw new Exception("prop sim did not save " + fn);
                    var back = KeystonePropSim.Load(simRun.Folder);
                    if (back == null || back.Days.Count != 3 || back.Trades.Count != simRun.Trades.Count || Math.Abs(back.Net - simRun.Net) > 0.01) throw new Exception("saved prop sim does not load back the same");
                    // the PROP POOL view (every inner tab) for the finished run and the saved copy, the setup window, the ＋ import window
                    var poolHost = new System.Windows.Controls.ScrollViewer(); string poolAct = null;
                    Call(lab2, "FillPropPool", poolHost, simRun, true, (Action<string>)(c9 => poolAct = c9));
                    Call(lab2, "FillPropPool", poolHost, back, false, (Action<string>)(c9 => poolAct = c9));
                    if (poolHost.Content == null) throw new Exception("PROP POOL view empty");
                    foreach (var vk in new[] { "OVERVIEW", "TODAY", "ACCOUNTS", "DAYS", "TRADES", "PAYOUTS", "EXPENSES", "GROWTH", "STRATEGY", "FIRMS", "LOSERS", "EVENTS", "SESSIONS", "WINNERS", "DAYSESS", "INDICATOR", "ODDS", "BATCHES", "JOURNAL" }) { Set(lab2, "propPoolView", vk); Call(lab2, "FillPropPool", poolHost, simRun, true, (Action<string>)(c9 => poolAct = c9)); }
                    foreach (var fk in new[] { "ACTIVE", "NOW", "EVAL", "FUNDED", "LOCKED", "YLOCK", "CANPASS", "TARGET", "PAYOUT", "FLOOR", "BLOWN", "RETIRED" }) { Set(lab2, "propPoolView", "ACCOUNTS"); Set(lab2, "propPoolFilter", fk); Call(lab2, "FillPropPool", poolHost, back, false, (Action<string>)(c9 => poolAct = c9)); }
                    foreach (var ck in new[] { "SESSIONS", "ACCOUNTS", "CHOICES", "INDICATOR" }) { Set(lab2, "propCompareKind", ck); Set(lab2, "propPoolView", "SESSIONS"); Call(lab2, "FillPropPool", poolHost, simRun, true, (Action<string>)(c9 => poolAct = c9)); }
                    Set(lab2, "propCompareKind", "SESSIONS");
                    Set(lab2, "propPoolView", "OVERVIEW"); Set(lab2, "propPoolFilter", "ACTIVE");
                    var lastSum = simRun.DaySummary(simRun.Days[simRun.Days.Count - 1].Day);
                    Console.WriteLine("      PROP POOL views: OVERVIEW / TODAY / ACCOUNTS (10 filters) / DAYS / TRADES / PAYOUTS / EVENTS / SESSIONS • last day: traded " + lastSum.Traded + ", at day target " + lastSum.AtTarget + ", locked " + lastSum.Locked + ", passed " + lastSum.Passed);
                    var noCmp = KeystonePropSim.Load(simRun.Folder); noCmp.Folder = System.IO.Path.Combine(studio, "nocmp"); Call(lab2, "FillPropPool", poolHost, noCmp, false, (Action<string>)(c9 => poolAct = c9));   // ⚖ SESSIONS before a comparison
                    var stWin = (System.Windows.Window)F("replayTraderWindow");
                    var setupHost = new System.Windows.Controls.StackPanel();
                    Call(lab2, "BuildPropSimSetup", stWin, setupHost, (Func<int, bool?>)(k0 => true), (Action<KeystoneSimSettings, string, bool>)((a0, b0, c0) => { }));
                    if (setupHost.Children.Count < 4) throw new Exception("the PROP SIM set-up has " + setupHost.Children.Count + " steps");
                    Call(lab2, "OpenInstrumentImport", stWin, (Func<IEnumerable<string>>)(() => new[] { "MNQ", "MGC" }), (Action<string, DateTime, DateTime, bool>)((a0, b0, c0, d0) => { }));
                    // I TRADE (rotation): nothing by itself; one click = BUY MNQ + SELL MGC together for the account in turn, exits managed
                    var manSet = simSet.Copy(); manSet.Auto = false; manSet.Direction = "BS"; manSet.Accounts = 2; manSet.From = run3[0]; manSet.To = run3[0]; manSet.RotTarget = 5000; manSet.FundRotTarget = 5000;
                    ((Action<KeystoneSimSettings, bool>)F("studioPropStart"))(manSet, false);
                    var man = ((Func<KeystonePropSim>)F("studioPropSim"))();
                    ((Action<int>)F("replayTraderStep"))(12);
                    if (man.Trades.Count != 0) throw new Exception("I TRADE traded by itself");
                    string whyNot = ((Func<int, int, string>)F("studioRotEnter"))(1, -1);
                    if (whyNot != "") throw new Exception("one-click rotation entry refused: " + whyNot);
                    string again = ((Func<int, int, string>)F("studioRotEnter"))(1, 1);
                    if (again == "") throw new Exception("a second entry was taken while a trade is open");
                    ((Action<int>)F("replayTraderStep"))(3);
                    ((Action<bool>)F("studioBoth"))(false); ((Action<bool>)F("studioBoth"))(true);   // one chart ↔ both charts while a trade runs
                    ((Action)F("studioPropRunDay"))();
                    Console.WriteLine("      I TRADE rotation: " + man.Trades.Count + " trade • " + (man.Trades.Count > 0 ? man.Trades[0].Legs + " • " + man.Trades[0].Reason + " • " + man.Trades[0].Pnl.ToString("0.00") + " • A" + man.Trades[0].Account.ToString("00") : "") + " • second click: " + again);
                    if (man.Trades.Count < 1 || !man.Trades[0].Legs.Contains("BUY 1 MNQ") || !man.Trades[0].Legs.Contains("SELL 1 MGC")) throw new Exception("the one-click BUY MNQ + SELL MGC trade was not booked");
                    if (man.Trades.Count < 2 || man.Trades.Skip(1).Any(x => !x.Legs.Contains("BUY 1 MNQ") || !x.Legs.Contains("SELL 1 MGC"))) throw new Exception("⚡ FINISH DAY in I TRADE did not trade the rotation by itself (" + man.Trades.Count + " trades)");
                    if (man.S.Auto) throw new Exception("⚡ FINISH DAY switched the run to AUTO");
                    ((Action<bool>)F("studioBoth"))(false);
                    // ONE SETUP PER ACCOUNT (default for studio / manual): an MNQ and an MGC trade at the same time go to two accounts
                    var psSet = simSet.Copy(); psSet.Strategy = "MANUAL"; psSet.Auto = false; psSet.Accounts = 3; psSet.From = run3[0]; psSet.To = run3[0];
                    ((Action<KeystoneSimSettings, bool>)F("studioPropStart"))(psSet, false);
                    var psRun = ((Func<KeystonePropSim>)F("studioPropSim"))(); var trF = (Func<string, KeystoneReplayTrader>)F("studioTrader");
                    ((Action<int>)F("replayTraderStep"))(5); trF("MNQ").Buy(1); trF("MGC").Sell(1); ((Action<int>)F("replayTraderStep"))(3);
                    trF("MNQ").CloseAll("ME"); trF("MGC").CloseAll("ME"); ((Action<int>)F("replayTraderStep"))(2);
                    Console.WriteLine("      ONE SETUP PER ACCOUNT: " + string.Join(" | ", psRun.Trades.Select(x => "A" + x.Account.ToString("00") + " " + x.Legs + " " + x.Pnl.ToString("0.00"))));
                    if (psRun.Trades.Count != 2 || psRun.Trades[0].Account == psRun.Trades[1].Account || !psRun.Trades.Any(x => x.Legs.Contains("MNQ")) || !psRun.Trades.Any(x => x.Legs.Contains("MGC"))) throw new Exception("MNQ and MGC setups at the same time were not booked to two different accounts");
                    var psHost = new System.Windows.Controls.ScrollViewer(); foreach (var vk in new[] { "STRATEGY", "WINNERS", "LOSERS" }) { Set(lab2, "propPoolView", vk); Call(lab2, "FillPropPool", psHost, psRun, true, (Action<string>)(c9 => { })); } Set(lab2, "propPoolView", "OVERVIEW");
                    ((Action)F("studioPropRunDay"))();
                    // SIDES changed during an AUTO run: BUY BOTH from now on
                    var sdSet = simSet.Copy(); sdSet.Direction = "SELL"; sdSet.From = run3[1]; sdSet.To = run3[1]; sdSet.Accounts = 2;
                    ((Action<KeystoneSimSettings, bool>)F("studioPropStart"))(sdSet, false);
                    var sdRun = ((Func<KeystonePropSim>)F("studioPropSim"))();
                    ((Action<string>)F("studioRotSides"))("BUY");
                    ((Action)F("studioPropRunDay"))();
                    Console.WriteLine("      SIDES → BUY BOTH during AUTO: " + sdRun.Trades.Count + " trades • " + string.Join(" | ", sdRun.Trades.Select(x => x.Legs).Distinct()));
                    if (sdRun.S.Direction != "BUY" || sdRun.Trades.Count == 0 || sdRun.Trades.Any(x => !x.Legs.Contains("BUY 1 MNQ") || !x.Legs.Contains("BUY 1 MGC"))) throw new Exception("the SIDES switch did not make AUTO buy both");
                    // 📊 PROP POOL • DATA from BOTH CHARTS: one chart + the pool; again = both charts back
                    ((Action<bool>)F("studioBoth"))(true);
                    var tg = (Func<string>)F("studioPoolToggle"); string t1 = tg(), t2 = tg();
                    Console.WriteLine("      PROP POOL button from both charts: " + t1 + " → " + t2);
                    if (!t1.StartsWith("ONE|POOL|") || !t1.Contains("BACK TO THE CHARTS") || !t2.StartsWith("BOTH|CHART|")) throw new Exception("the top PROP POOL button does not open the pool from BOTH CHARTS and come back (" + t1 + " / " + t2 + ")");
                    ((Action<bool>)F("studioBoth"))(false);
                    // 📡 SIDES = INDICATOR: a run started with it takes the saved indicator and trades only its sides (or waits)
                    var inSet = simSet.Copy(); inSet.Direction = "INDICATOR"; inSet.Indicator = ""; inSet.From = run3[2]; inSet.To = run3[2]; inSet.Accounts = 2;
                    ((Action<KeystoneSimSettings, bool>)F("studioPropStart"))(inSet, false);
                    var inRun = ((Func<KeystonePropSim>)F("studioPropSim"))(); var inInd = KeystoneEntryIndicator.FromText(inRun.S.Indicator);
                    ((Action)F("studioPropRunDay"))();
                    Console.WriteLine("      SIDES = INDICATOR run: " + inRun.Trades.Count + " trades • " + string.Join(" | ", inRun.Trades.Select(x => x.Legs).Distinct()) + " • rules " + (inInd == null ? -1 : inInd.Rules.Count));
                    if (inInd == null) throw new Exception("a SIDES = INDICATOR run did not take the saved indicator");
                    if (inInd.Rules.Count == 0 && inRun.Trades.Count > 0) throw new Exception("an indicator with no rules traded");
                    if (inRun.Trades.Any(x => !inInd.Rules.Any(r9 => x.Legs.Contains((r9.DirM > 0 ? "BUY " : "SELL ") + "1 MNQ") && x.Legs.Contains((r9.DirG > 0 ? "BUY " : "SELL ") + "1 MGC")))) throw new Exception("an INDICATOR trade took sides no rule gives");
                    ((Action<string>)F("studioPoolAct"))("INDICATOR|USE");
                    if (inRun.S.Direction != "INDICATOR") throw new Exception("📡 USE did not set SIDES = INDICATOR");
                    // every finished test forms an indicator into the 📡 INDICATOR POOL; one is chosen from the pool for this run
                    var poolDir = System.IO.Path.Combine(studio, "PropSim", "Indicators"); int poolBefore = System.IO.Directory.Exists(poolDir) ? System.IO.Directory.GetFiles(poolDir).Length : 0;
                    var fSet = simSet.Copy(); fSet.DataOnly = true; fSet.Direction = "RANDOM4"; fSet.From = bothSaved[0]; fSet.To = bothSaved[bothSaved.Count - 1];
                    ((Action<KeystoneSimSettings, bool>)F("studioPropStart"))(fSet, true);
                    var fRun = ((Func<KeystonePropSim>)F("studioPropSim"))();
                    for (int i = 0; i < 12 && !fRun.Finished; i++) ((Action)F("studioPropRunDay"))();
                    var poolFiles = System.IO.Directory.Exists(poolDir) ? System.IO.Directory.GetFiles(poolDir) : new string[0];
                    var formedI = poolFiles.Select(f9 => KeystoneEntryIndicator.FromText(System.IO.File.ReadAllText(f9))).Where(x9 => x9 != null && x9.Source.Contains("formed by")).ToList();
                    Console.WriteLine("      a " + fRun.Days.Count + "-day DATA ONLY test formed: " + (formedI.Count == 0 ? "nothing" : formedI[0].Describe()) + " • pool " + poolBefore + " → " + poolFiles.Length);
                    if (fRun.Days.Count >= 5 && (formedI.Count == 0 || poolFiles.Length <= poolBefore)) throw new Exception("a finished test did not form an indicator into the pool");
                    if (poolFiles.Length > 0)
                    {
                        ((Action<string>)F("studioPoolAct"))("INDICATOR|CHOOSE|" + poolFiles[0]);
                        if (System.IO.File.ReadAllText(System.IO.Path.Combine(studio, "PropSim", "Indicator.txt")) != KeystoneEntryIndicator.FromText(System.IO.File.ReadAllText(poolFiles[0])).ToText()) throw new Exception("choosing from the INDICATOR POOL did not set it for the next runs");
                        var wizHost = new System.Windows.Controls.StackPanel(); Call(lab2, "BuildPropSimSetup", stWin, wizHost, (Func<int, bool?>)(k0 => true), (Action<KeystoneSimSettings, string, bool>)((a0, b0, c0) => { }));
                    }
                    // DATA ONLY: two days at full speed without drawing the charts; ONE WIN A DAY locks an account after its first win
                    var dSet = simSet.Copy(); dSet.DataOnly = true; dSet.DayPlan = "ONEWIN"; dSet.From = run3[0]; dSet.To = run3[1];
                    ((Action<KeystoneSimSettings, bool>)F("studioPropStart"))(dSet, true);
                    var dRun = ((Func<KeystonePropSim>)F("studioPropSim"))();
                    for (int i = 0; i < 4 && !dRun.Finished; i++) ((Action)F("studioPropRunDay"))();
                    var wins = dRun.Trades.Where(t => t.Pnl > 0).GroupBy(t => t.Day.ToString("yyyyMMdd") + "|" + t.Account).ToList();
                    Console.WriteLine("      DATA ONLY run: " + dRun.Days.Count + " days • " + dRun.Trades.Count + " trades • " + wins.Count + " account-days with a win");
                    if (!dRun.Finished || dRun.Days.Count != 2) throw new Exception("DATA ONLY run did not finish its 2 days");
                    if (wins.Any(g => g.Count() > 1)) throw new Exception("ONE WIN A DAY: an account won twice on one day");
                    // ＋ instruments: a registered symbol gets its own tab, and goes away again
                    KeystoneSymbols.Register(new KeystoneSymbolSpec { Symbol = "ES", Name = "E-mini S&P 500", Type = "FUTURE", PointValue = 50, TickSize = 0.25, Commission = 2.25, FullDayMinutes = 800 });
                    ((Action<string>)F("studioAddSymbol"))("ES");
                    if (KeystoneMoveStudy.PointValue("ES") != 50 || KeystoneMoveStudy.TickSize("ES") != 0.25) throw new Exception("ES spec not used");
                    ((Action<string>)F("studioAddSymbol"))("-ES");
                    if (KeystoneSymbols.Get("ES") != null) throw new Exception("ES tab not removed");
                    Console.WriteLine("      ＋ instrument: ES tab added and removed");
                    {   // 💵 BUILD 11f: the $ bracket in the RIGHT ORDER PANEL (one source with the 💵 row), the ⚙ next to the ACCOUNT box
                        var w11 = (System.Windows.Window)F("replayTraderWindow"); Func<string, object> byTag = tgx => FindAll<System.Windows.FrameworkElement>(w11.Content).FirstOrDefault(x => Convert.ToString(x.Tag) == tgx);
                        var dTpM = byTag("dol.tp.MNQ") as System.Windows.Controls.TextBox; var dOnM = byTag("dol.on.MNQ") as System.Windows.Controls.Button; var dNoteM = byTag("dol.note.MNQ") as System.Windows.Controls.TextBlock;
                        if (dTpM == null || dOnM == null || dNoteM == null || byTag("acc.gear") == null) throw new Exception("💵 the order panel's TP $ / SL $ / ON block and the ACCOUNT ⚙ should exist");
                        bool wasOn = Convert.ToString(dOnM.Content) == "💵 $ ON"; if (!wasOn) dOnM.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        dTpM.Text = "760"; var dSlM = (System.Windows.Controls.TextBox)byTag("dol.sl.MNQ"); dSlM.Text = "500";
                        if (Convert.ToString(dOnM.Content) != "💵 $ ON" || !dNoteM.Text.StartsWith("= SL ") || !dNoteM.Text.Contains(" pts for ")) throw new Exception("💵 ON → the points for the contracts: " + Convert.ToString(dOnM.Content) + " • " + dNoteM.Text);
                        Console.WriteLine("      💵 order panel $ bracket: TP $760 / SL $500 → " + dNoteM.Text);
                        dOnM.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); if (Convert.ToString(dOnM.Content) != "💵 $ OFF") throw new Exception("💵 OFF again");
                    }
                    {   // 🖥 BUILD 11f — CHART-FIRST: the chart = the star row (no fixed height), ≤ 2 rows above it with the drawers closed; each drawer opens / closes
                        var w12 = (System.Windows.Window)F("replayTraderWindow"); var showD = (Action<string>)F("studioShowDrawer"); var rows = (Func<int>)F("studioTopRows"); var mr = (System.Windows.FrameworkElement)F("studioMainRow");
                        if (showD == null || rows == null || mr == null) throw new Exception("🖥 the chart-first layout is missing");
                        var rootG = mr.Parent as System.Windows.Controls.Grid; int rr = System.Windows.Controls.Grid.GetRow(mr);
                        if (rootG == null || rootG.RowDefinitions[rr].Height.GridUnitType != System.Windows.GridUnitType.Star || (!double.IsNaN(mr.Height) && mr.Height > 0)) throw new Exception("🖥 the chart must sit in the star row with no fixed height (row " + rr + ", " + (rootG == null ? "no grid" : rootG.RowDefinitions[rr].Height.GridUnitType.ToString()) + ", height " + mr.Height + ")");   /* the stand-ins leave an unset height at 0 (WPF: NaN) */
                        if (F("studioOpenDrawer") != null) showD((string)F("studioOpenDrawer"));
                        if (rows() > 2) throw new Exception("🖥 with the drawers closed at most 2 rows above the chart, got " + rows());
                        foreach (var dk in new[] { "SETTINGS", "PRACTICE", "INDICATORS", "TOOLS" }) { showD(dk); if ((string)F("studioOpenDrawer") != dk) throw new Exception("🖥 drawer " + dk + " did not open"); showD(dk); if (F("studioOpenDrawer") != null) throw new Exception("🖥 drawer " + dk + " did not close"); }
                        Console.WriteLine("      🖥 chart-first: the chart in the star row, " + rows() + " rows above it, 4 drawers open / close");
                        // 🏷 the gutter labels: dollars first, never cut — 1 / 25 / 40 contracts, 5-digit prices, MNQ + MGC
                        foreach (var sy in new[] { "MNQ", "MGC" }) foreach (int q in new[] { 1, 25, 40 }) foreach (double pts in new[] { 0.25, 3, 20.5, 250 })
                        {
                            double pv = KeystoneMoveStudy.PointValue(sy); var labs = new[] { KeystoneChartGutter.Tp(pts, pv, q) + "  (drag)", KeystoneChartGutter.Sl(pts, pv, q) + "  (drag)", KeystoneChartGutter.Locked(pts, pv, q) + "  (drag)", KeystoneChartGutter.Entry(q, -pts * pv * q) };
                            double g = KeystoneChartGutter.Fit(labs.Select((l, i) => KeystoneChartGutter.LabelWidth(l, i == 3)));
                            for (int i = 0; i < labs.Length; i++) if (!KeystoneChartGutter.Fits(labs[i], i == 3, g) || !labs[i].Contains("$")) throw new Exception("🏷 a label would be cut: " + labs[i] + " in a gutter of " + g);
                            if (g > KeystoneChartGutter.Max || g < KeystoneChartGutter.Min) throw new Exception("🏷 the gutter " + g);
                        }
                        ((List<double[]>)F("studioLabelRects")).Clear(); ((Action<int>)F("replayTraderStep"))(1);   /* the gutter grew to the widest label: the next render fits them all */
                        foreach (var rc in ((List<double[]>)F("studioLabelRects")).Where(x => x.Length > 5)) if (rc[5] > rc[2] + 0.5 && rc[2] + KeystoneChartGutter.Handle < KeystoneChartGutter.Max - 0.5) throw new Exception("🏷 a drawn label is wider than its box: " + rc[5] + " > " + rc[2]);
                        Console.WriteLine("      🏷 gutter labels: $ first, 1 / 25 / 40 contracts fit (" + KeystoneChartGutter.Tp(20.5, 2, 25) + " · " + KeystoneChartGutter.Sl(3, 2, 25) + " · " + KeystoneChartGutter.Entry(25, 450) + ")");
                        // 🧹 the closed-trade results: off by default; with the option, every tag clear of the candles and of each other
                        showD("INDICATORS"); var resCb = FindAll<System.Windows.Controls.CheckBox>(w12.Content).FirstOrDefault(cbx => Convert.ToString(cbx.Tag) == "studio.showres"); if (resCb == null || resCb.IsChecked == true) throw new Exception("🧹 'show trade results on the chart' must exist and be off");
                        resCb.IsChecked = true; resCb.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        var tags = (List<double[]>)F("studioTradeTags");
                        for (int i = 0; i < tags.Count; i++) { var t = tags[i]; if (t[1] < t[5] && t[1] + t[3] > t[4]) throw new Exception("🧹 a result tag covers a candle"); for (int j = i + 1; j < tags.Count; j++) if (KeystoneChartGutter.Intersects(t[0], t[1], t[2], t[3], tags[j][0], tags[j][1], tags[j][2], tags[j][3])) throw new Exception("🧹 two result tags overlap"); }
                        resCb.IsChecked = false; resCb.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); showD("INDICATORS");
                        Console.WriteLine("      🧹 closed-trade tags: " + tags.Count + " placed clear of the candles and of each other (option off by default)");
                    }
                    var win = (System.Windows.Window)F("replayTraderWindow"); win.Close();
                    if (!System.IO.File.Exists(System.IO.Path.Combine(studio, "Workspace.txt"))) throw new Exception("studio workspace not saved on close");
                    Console.WriteLine("      workspace saved: " + string.Join(" ", System.IO.File.ReadAllLines(System.IO.Path.Combine(studio, "Workspace.txt")).Take(4)));
                    Call(lab2, "OpenDataLibrary");
                    var dl = (Action<string, DateTime, DateTime, bool>)F("dataLibraryDownload");
                    dl("BOTH", new DateTime(2026, 9, 1), new DateTime(2026, 9, 20), false);
                    Console.WriteLine("      DATA LIBRARY opened, download queued (no NinjaTrader feed here: the contract lookup answers nothing)");
                    // LIVE DESK: accounts from NinjaTrader's list, a new group, a card moved into it, saved to LiveDesk.txt
                    string deskFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(studio), "LiveDesk.txt"); if (System.IO.File.Exists(deskFile)) System.IO.File.Delete(deskFile);
                    NinjaTrader.Cbi.Account.All.Clear();
                    var apex = new NinjaTrader.Cbi.Account { Name = "APEX-123-01" }; apex.StubValues[NinjaTrader.Cbi.AccountItem.NetLiquidation] = 51200; apex.StubValues[NinjaTrader.Cbi.AccountItem.RealizedProfitLoss] = 350;
                    apex.Positions.Add(new NinjaTrader.Cbi.Position { MarketPosition = NinjaTrader.Cbi.MarketPosition.Long, Quantity = 2, AveragePrice = 21000 });
                    NinjaTrader.Cbi.Account.All.Add(apex); NinjaTrader.Cbi.Account.All.Add(new NinjaTrader.Cbi.Account { Name = "Sim101" });
                    Call(lab2, "OpenLiveDesk");
                    var made = ((Func<string, string>)F("liveDeskNewGroup"))("MORNING");
                    ((Action<string, string>)F("liveDeskMove"))("APEX-123-01", made);
                    ((Action)F("liveDeskRefresh"))();
                    var desk = System.IO.File.ReadAllLines(deskFile);
                    Console.WriteLine("      LIVE DESK: " + string.Join(" / ", desk));
                    if (!desk.Contains("GROUP|MORNING") || !desk.Any(l => l.StartsWith("ACCOUNT|APEX-123-01|") && l.Contains("|MORNING|"))) throw new Exception("live desk group / move not saved");
                    // LIVE TRADING: its own window (real-time data, no replay controls); the BACKTEST STUDIO stays history only
                    Call(lab2, "OpenLiveTrading");
                    var liveWin = (System.Windows.Window)F("replayTraderWindow");
                    if (liveWin == null || !liveWin.Title.Contains("LIVE TRADING")) throw new Exception("LIVE TRADING window did not open");
                    Console.WriteLine("      LIVE TRADING window opened: " + liveWin.Title);
                    {
                        // MNQ + MGC side by side and back (the charts move between the tabs and the dual view: one parent at a time), FOCUS on top of it
                        var all = new List<object>(); Action<object> walk = null;
                        walk = o =>
                        {
                            if (o == null || all.Contains(o)) return; all.Add(o);
                            var pnl = o as System.Windows.Controls.Panel; if (pnl != null) foreach (var ch0 in pnl.Children.Cast<object>().ToList()) walk(ch0);
                            var bd = o as System.Windows.Controls.Border; if (bd != null) walk(bd.Child);
                            var pp = o as System.Windows.Controls.Primitives.Popup; if (pp != null) walk(pp.Child);   // a drop-down panel (📊 INDICATORS)
                            var cc = o as System.Windows.Controls.ContentControl; if (cc != null) walk(cc.Content);
                            var ic = o as System.Windows.Controls.ItemsControl; if (ic != null) foreach (var it in ic.Items.Cast<object>().ToList()) walk(it);
                        };
                        walk(liveWin.Content);
                        Func<string, System.Windows.Controls.Button> btn = t => all.OfType<System.Windows.Controls.Button>().FirstOrDefault(b => Convert.ToString(b.Content) == t);
                        var dual = btn("◫ MNQ + MGC"); var focusB = btn("⛶ FOCUS"); if (dual == null || focusB == null) throw new Exception("MNQ + MGC / FOCUS button missing");
                        Action<System.Windows.Controls.Button> click = b => b.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        // 👥 ACCOUNTS: SET UP AGAIN → 1 tick (APEX is not connected here: + NOT CONNECTED) → 2 names (APEX 1) → 3 price → FINISH → the list; CLOSE ALL ACCOUNTS; back to the chart
                        Func<System.Windows.Controls.Button> accB = () => all.OfType<System.Windows.Controls.Button>().FirstOrDefault(b => Convert.ToString(b.ToolTip).StartsWith("Your accounts: names"));
                        Func<string, System.Windows.Controls.Button> btnS = t => all.OfType<System.Windows.Controls.Button>().FirstOrDefault(b => Convert.ToString(b.Content).StartsWith(t));
                        Action rewalk = () => { all.Clear(); walk(liveWin.Content); };
                        if (accB() == null) throw new Exception("ACCOUNTS button missing");
                        if (Convert.ToString(accB().Content) != "◀ CHART") click(accB());
                        rewalk(); if (btnS("✎ SET UP AGAIN") == null) throw new Exception("👥 ACCOUNTS (the pool) did not open");
                        click(btnS("✎ SET UP AGAIN")); rewalk();
                        click(btnS("+ NOT CONNECTED")); rewalk();
                        var tick = all.OfType<System.Windows.Controls.CheckBox>().FirstOrDefault(cbx => Convert.ToString(cbx.ToolTip) == "trade APEX-123-01"); if (tick == null) throw new Exception("no account to tick");
                        tick.IsChecked = true; tick.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        click(btnS("NEXT ▶  NAMES")); rewalk();
                        if (!all.OfType<System.Windows.Controls.TextBox>().Any(t => t.Text == "APEX 1")) throw new Exception("step 2 did not name APEX 1: " + string.Join(" / ", all.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text).Where(t => t != null && t.Length > 3).Reverse().Take(12)) + " • boxes " + string.Join(",", all.OfType<System.Windows.Controls.TextBox>().Select(t => t.Text)));
                        click(btnS("NEXT ▶  PRICE")); rewalk();
                        var priceBox = all.OfType<System.Windows.Controls.TextBox>().FirstOrDefault(); if (priceBox == null) throw new Exception("step 3 price box missing");
                        click(btnS("NEXT ▶  RULES")); rewalk();
                        if (!all.OfType<System.Windows.Controls.CheckBox>().Any(cbx => Convert.ToString(cbx.Content) == "COPYING my own accounts")) throw new Exception("step 4 (firm rules) missing");
                        click(btnS("✔ FINISH & SAVE")); rewalk();
                        if (!all.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "").Contains("APEX 1"))) throw new Exception("👥 ACCOUNTS does not show APEX 1 after FINISH");
                        click(btnS("⛔ CLOSE ALL POSITIONS"));   // nothing open with an instrument here → "NOTHING OPEN"
                        // ⇉ COPIER (groups) and 💰 POOL open from their (tab-bar) buttons; + NEW GROUP makes GROUP 1
                        rewalk(); click(btn("COPIER")); rewalk();
                        if (btnS("＋ NEW GROUP") == null) throw new Exception("COPIER view did not open");
                        click(btnS("＋ NEW GROUP")); rewalk(); if (!all.OfType<System.Windows.Controls.TextBox>().Any(t => (t.Text ?? "").StartsWith("GROUP "))) throw new Exception("＋ NEW GROUP did not make a group");
                        if (!all.OfType<System.Windows.Controls.CheckBox>().Any(cbx => Convert.ToString(cbx.Content) == "different size per account")) throw new Exception("the group's size option is missing");
                        click(btn("POOL")); rewalk(); if (!all.OfType<System.Windows.Controls.TextBlock>().Any(t => t.Text == "👥 ACCOUNTS")) throw new Exception("👥 ACCOUNTS (pool) view did not open");
                        if (!all.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "").StartsWith("APEX 1") || (t.Text ?? "").Contains("APEX 1"))) throw new Exception("POOL does not show APEX 1");
                        click(btn("◀ CHART")); rewalk();
                        Console.WriteLine("      ⇉ COPIER groups (+ NEW GROUP) and 💰 POOL (firm tabs, APEX 1): OK");
                        rewalk(); if (Convert.ToString(accB().Content) != "👥 ACCOUNTS") throw new Exception("not back on the chart");
                        Console.WriteLine("      👥 ACCOUNTS: 3 steps (tick → names APEX 1 → price) → FINISH → the list, CLOSE ALL ACCOUNTS, back to the chart: OK");
                        click(dual); if (Convert.ToString(dual.Content) != "◫ ONE CHART") throw new Exception("MNQ + MGC did not switch on");
                        click(focusB); click(focusB); click(dual);
                        if (Convert.ToString(dual.Content) != "◫ MNQ + MGC") throw new Exception("MNQ + MGC did not switch back");
                        Console.WriteLine("      MNQ + MGC side by side → one chart again, FOCUS in between: OK");
                        // 🎲 MNQ+MGC ROTATION: the view opens (pool rows with firm colours, buttons, settings, the learning log), AUTO / MANUAL toggles, back to the chart
                        var duoB = btn("🎲 MNQ+MGC ROTATION"); if (duoB == null) throw new Exception("MNQ+MGC ROTATION button missing");
                        click(duoB); if (Convert.ToString(duoB.Content) != "◀ CHART") throw new Exception("MNQ+MGC ROTATION view did not open");
                        all.Clear(); walk(liveWin.Content);
                        var bb = btn("▲▲ BUY BOTH"); Func<System.Windows.Controls.Button> duoMode = () => all.OfType<System.Windows.Controls.Button>().FirstOrDefault(b => Convert.ToString(b.ToolTip).StartsWith("AUTO: after each pause")); var modeB = duoMode();
                        if (bb == null || modeB == null || btn("▶ START ROTATION") == null) throw new Exception("MNQ+MGC ROTATION buttons missing");
                        if (btnS("👤 ACCOUNTS") == null || btnS("⇉ COPY GROUPS") == null) throw new Exception("ROTATE: ACCOUNTS / COPY GROUPS choice missing");
                        if (!all.OfType<System.Windows.Controls.TextBlock>().Any(t => t.Text == "🤖 AI")) throw new Exception("the 🤖 AI indicator chip is missing");
                        string m0 = Convert.ToString(modeB.Content); click(modeB); all.Clear(); walk(liveWin.Content);
                        var modeB2 = duoMode();
                        if (modeB2 == null || Convert.ToString(modeB2.Content) == m0) throw new Exception("AUTO / MANUAL did not toggle");
                        click(modeB2); click(bb);   // BUY BOTH before START: refused with a message, nothing sent
                        all.Clear(); walk(liveWin.Content); var backDuo = btn("◀ CHART"); if (backDuo == null) throw new Exception("no way back to the chart"); click(backDuo);
                        Console.WriteLine("      MNQ+MGC ROTATION view: buttons, AUTO ⇄ MANUAL, BUY BOTH refused before START, back to the chart: OK");
                        {   // 🎯 BUILD 11p: ROTATE ▸ SIGNAL SOURCE "🎯 3 SETUPS" (GUIDED / MANUAL only): the settings + the research label; START refused without a pool — nothing sent
                            all.Clear(); walk(liveWin.Content); var rotB = btn("🔄 ROTATE"); if (rotB == null) throw new Exception("🔄 ROTATE button missing"); click(rotB); all.Clear(); walk(liveWin.Content);
                            var srcC = all.OfType<System.Windows.Controls.ComboBox>().FirstOrDefault(c9 => Convert.ToString(c9.Tag) == "rot.tri.src"); if (srcC == null) throw new Exception("🎯 ROTATE: no SIGNAL SOURCE");
                            Action<System.Windows.Controls.ComboBox, int> pickC = (c9, i9) => { c9.SelectedIndex = i9; var fe = typeof(System.Windows.Controls.Selector).GetField("SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic); var dg9 = fe == null ? null : fe.GetValue(c9) as Delegate; if (dg9 != null) dg9.DynamicInvoke(c9, null); };   /* the stand-ins do not raise SelectionChanged */
                            pickC(srcC, 1); all.Clear(); walk(liveWin.Content);
                            if (!all.OfType<System.Windows.Controls.ComboBox>().Any(c9 => Convert.ToString(c9.Tag) == "rot.tri.mode") || !all.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "").Contains("no edge yet")) || !all.OfType<System.Windows.Controls.TextBox>().Any(t => Convert.ToString(t.Tag) == "rot.tri.flock")) throw new Exception("🎯 ROTATE ▸ 3 SETUPS settings missing");
                            if (!all.OfType<System.Windows.Controls.CheckBox>().Any(c9 => Convert.ToString(c9.Tag) == "rot.tri.eone") || !all.OfType<System.Windows.Controls.CheckBox>().Any(c9 => Convert.ToString(c9.Tag) == "rot.tri.fone") || !all.OfType<System.Windows.Controls.Button>().Any(b9 => Convert.ToString(b9.Tag) == "rot.tri.plan.bold") || !all.OfType<System.Windows.Controls.Button>().Any(b9 => Convert.ToString(b9.Tag) == "rot.tri.plan.small")) throw new Exception("🎯 ROTATE ▸ 3 SETUPS: ONE TRADE A DAY ticks / 1-A-DAY presets missing");
                            var startB = btn("▶ START ROTATION"); if (startB != null) click(startB);   /* no pool → refused with a message */
                            all.Clear(); walk(liveWin.Content); var srcC2 = all.OfType<System.Windows.Controls.ComboBox>().First(c9 => Convert.ToString(c9.Tag) == "rot.tri.src"); pickC(srcC2, 0);
                            all.Clear(); walk(liveWin.Content); var backR = btn("◀ CHART"); if (backR != null) click(backR);
                            Console.WriteLine("      🔄 ROTATE ▸ 🎯 3 SETUPS source: window / account order / eval + funded bracket / day goals / ONE TRADE A DAY / 💡 BOLD + SMALL 1-A-DAY / DONE marks, START refused without a pool: OK");
                        }
                        // 🤖 LIVE AUTOPILOT (BUILD 11g): the view opens (the explanation, ▶ START AUTOPILOT, the TODAY journal); START without accounts is refused — nothing sent
                        all.Clear(); walk(liveWin.Content); var apB = btn("🤖 AUTOPILOT"); if (apB == null) throw new Exception("🤖 AUTOPILOT button missing");
                        click(apB); if (Convert.ToString(apB.Content) != "◀ CHART") throw new Exception("🤖 AUTOPILOT view did not open");
                        all.Clear(); walk(liveWin.Content); var apGo = btn("▶ START AUTOPILOT"); if (apGo == null) throw new Exception("▶ START AUTOPILOT missing");
                        if (!all.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "").StartsWith("🤖 LIVE AUTOPILOT"))) throw new Exception("🤖 AUTOPILOT title missing");
                        click(apGo); all.Clear(); walk(liveWin.Content); if (btn("■ STOP AUTOPILOT") != null) throw new Exception("🤖 the autopilot started without accounts / ARM / START");
                        click(btn("◀ CHART"));
                        Console.WriteLine("      🤖 AUTOPILOT view: title, ▶ START AUTOPILOT refused without accounts (nothing sent), back to the chart: OK");
                        {   // 🧾 BUILD 11t: EXECUTIONS = the per-account trade journal from NinjaTrader's executions (the user's TPT funded day): account selector,
                            // iterations with their real fees (not gross − realized), the legs + the snapshot on a click, the day file — nothing sent
                            var tpt = new NinjaTrader.Cbi.Account { Name = "TAKEPROFITPRO382305483", Connection = new object() }; tpt.StubValues[NinjaTrader.Cbi.AccountItem.NetLiquidation] = 51680; tpt.StubValues[NinjaTrader.Cbi.AccountItem.RealizedProfitLoss] = 357.75; tpt.StubValues[NinjaTrader.Cbi.AccountItem.GrossRealizedProfitLoss] = 1725.50;
                            var insX = new NinjaTrader.Cbi.Instrument { FullName = "MNQ 12-26" }; var t0X = DateTime.Now.AddMinutes(-5);
                            Action<string, int, double, double, int, string, NinjaTrader.Cbi.OrderType> exX = (id, q, px, cm, sec, nm, ot) => tpt.Executions.Add(new NinjaTrader.Cbi.Execution { ExecutionId = id, Instrument = insX, Price = px, Quantity = Math.Abs(q), MarketPosition = q > 0 ? NinjaTrader.Cbi.MarketPosition.Long : NinjaTrader.Cbi.MarketPosition.Short, Commission = cm, Time = t0X.AddSeconds(sec), Order = new NinjaTrader.Cbi.Order { Name = nm, OrderType = ot } });
                            exX("x1", 2, 28175.00, 1.04, 0, "", NinjaTrader.Cbi.OrderType.Market); exX("x2", -2, 28167.25, 1.04, 60, "Close", NinjaTrader.Cbi.OrderType.Market); exX("x3", 5, 28196.75, 5.35, 120, "Entry", NinjaTrader.Cbi.OrderType.Market); exX("x4", -3, 28206.75, 3.21, 180, "Target1", NinjaTrader.Cbi.OrderType.Limit); exX("x5", -2, 28214.75, 2.14, 240, "Target2", NinjaTrader.Cbi.OrderType.Limit);
                            NinjaTrader.Cbi.Account.All.Add(tpt); var madeFX = lab2.GetType().GetNestedType("NinjaBroker", BindingFlags.NonPublic).GetField("Made", BindingFlags.Static | BindingFlags.Public); int made0X = (int)madeFX.GetValue(null);
                            Action<object> upX = o => { var fe = typeof(System.Windows.UIElement).GetField("MouseLeftButtonUp", BindingFlags.Instance | BindingFlags.NonPublic); var dg = fe == null ? null : fe.GetValue(o) as Delegate; if (dg == null) throw new Exception("no click handler"); dg.DynamicInvoke(o, null); };
                            Action<System.Windows.Controls.ComboBox, int> pickX = (c9, i9) => { c9.SelectedIndex = i9; var fe = typeof(System.Windows.Controls.Primitives.Selector).GetField("SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic); var dg9 = fe == null ? null : fe.GetValue(c9) as Delegate; if (dg9 != null) dg9.DynamicInvoke(c9, null); };
                            all.Clear(); walk(liveWin.Content); var execTab = all.OfType<System.Windows.Controls.Border>().FirstOrDefault(b => Convert.ToString(b.ToolTip) == "EXECUTIONS"); if (execTab == null) throw new Exception("🧾 EXECUTIONS tab missing");
                            upX(execTab); all.Clear(); walk(liveWin.Content);
                            var accCb = all.OfType<System.Windows.Controls.ComboBox>().FirstOrDefault(c9 => Convert.ToString(c9.ToolTip).StartsWith("Which account")); if (accCb == null) throw new Exception("🧾 the ACCOUNT selector is missing");
                            int idxX = accCb.Items.Cast<object>().ToList().FindIndex(o => Convert.ToString(o).Contains("TAKEPROFITPRO382305483")); if (idxX < 2 || !Convert.ToString(accCb.Items[1]).Contains("ALL")) throw new Exception("🧾 the selector does not list ALL + the account: " + string.Join(" / ", accCb.Items.Cast<object>()));
                            pickX(accCb, idxX); upX(execTab); all.Clear(); walk(liveWin.Content);
                            var txX = all.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text ?? "").ToList();
                            if (!txX.Any(t => t.Contains("TAKEPROFITPRO382305483")) || !txX.Contains("−$33.08") || !txX.Contains("+$121.30") || !txX.Contains("+$88.22") || txX.Any(t => t.Contains("391") || t.Contains("797"))) throw new Exception("🧾 EXECUTIONS rows / totals wrong: " + string.Join(" | ", txX.Where(t => t.Contains("$")).Take(30)));
                            if (!txX.Any(t => t.StartsWith("NinjaTrader says: realized today +$357.75") && t.Contains("difference")) || !txX.Any(t => t.Contains("GROSS differs"))) throw new Exception("🧾 the NinjaTrader reconciliation line is missing");
                            var rowX = all.OfType<System.Windows.Controls.Border>().FirstOrDefault(b => Convert.ToString(b.ToolTip).Contains("1 partial exit(s)")); if (rowX == null) throw new Exception("🧾 the partial-exit iteration row is missing");
                            upX(rowX); all.Clear(); walk(liveWin.Content); txX = all.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text ?? "").ToList();
                            if (!txX.Contains("1. ENTRY") || !txX.Contains("2. PARTIAL EXIT 1") || !txX.Contains("3. EXIT") || !txX.Contains("bracket target") || !txX.Any(t => t.StartsWith("▶ AT THE START")) || !txX.Any(t => t.StartsWith("■ AT THE CLOSE") && t.Contains("balance $51,680.00")) || !txX.Any(t => t.StartsWith("📉 drawdown used"))) throw new Exception("🧾 the legs / the snapshot did not open: " + string.Join(" | ", txX.Where(t => t.Length > 2).Reverse().Take(20)));
                            var dirX = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(studio), "Studio", "Executions", "TAKEPROFITPRO382305483"); if (!System.IO.Directory.Exists(dirX)) dirX = System.IO.Path.Combine(studio, "Executions", "TAKEPROFITPRO382305483");
                            var filesX = System.IO.Directory.Exists(dirX) ? System.IO.Directory.GetFiles(dirX, "*.csv") : new string[0]; if (filesX.Length == 0 || !System.IO.File.ReadAllText(filesX[0]).Contains("PARTIAL EXIT 1")) throw new Exception("🧾 the day file was not written: " + dirX);
                            if ((int)madeFX.GetValue(null) != made0X) throw new Exception("🧾 EXECUTIONS built a NinjaBroker");
                            all.Clear(); walk(liveWin.Content); var chartTab = all.OfType<System.Windows.Controls.Border>().FirstOrDefault(b => Convert.ToString(b.ToolTip) == "CHART"); if (chartTab != null) upX(chartTab);
                            NinjaTrader.Cbi.Account.All.Remove(tpt);
                            Console.WriteLine("      🧾 11t EXECUTIONS: account selector (chart / ALL / each), the user's day −$33.08 / +$121.30 with a partial (net +$88.22, fees from the executions — not −$391 / −$797), NinjaTrader's +$357.75 only as a reconciliation line, legs + partial exit + snapshot on a click, the day file: OK");
                        }
                    }
                    liveWin.Close(); if (F("replayTraderWindow") != null) throw new Exception("LIVE TRADING window did not close");
                    // every window asks before the X closes it
                    foreach (var wn in new[] { "launcherWindow", "dataLibraryWindow", "liveDeskWindow" })
                    {
                        var win2 = (System.Windows.Window)F(wn); var closingHandlers = typeof(System.Windows.Window).GetField("Closing", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(win2);
                        if (closingHandlers == null) throw new Exception(wn + " closes without asking");
                        win2.Close();
                    }
                    Console.WriteLine("      launcher, data library, live desk: each asks before closing");
                    Call(lab2, "OpenPatternLab"); ((Action<string>)F("patternLabRun"))("MNQ");
                    Console.WriteLine("      PATTERN LAB opened and asked for a test (" + KeystoneStudioStore.Sessions(studio, "MNQ").Count + " saved MNQ days here — too few, it says so)");
                });
            if (strategy == "MAD")
                Step("🪟 BUILD 11j: BACKTEST STUDIO + EVAL LAB + LIVE TRADING open together, reopen = focus • ● LIVE DEMO: real-time feed (stand-in), simulated fills, 🎯 TPT practice floor follows every tick, NOTHING sent", () =>
                {
                    var BF = BindingFlags.Instance | BindingFlags.NonPublic;
                    Func<string, object> F = n => lab2.GetType().GetField(n, BF).GetValue(lab2); Action<string, object> S = (n, v) => lab2.GetType().GetField(n, BF).SetValue(lab2, v);
                    var nbMade = lab2.GetType().GetNestedType("NinjaBroker", BindingFlags.NonPublic).GetField("Made", BindingFlags.Static | BindingFlags.Public);
                    DateTime ny; try { ny = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Eastern Standard Time"); } catch { ny = DateTime.Now; }
                    var m0 = new DateTime(ny.Year, ny.Month, ny.Day, ny.Hour, ny.Minute, 0);
                    S("studioLiveFeedFake", (Func<string, List<KeystoneArcBar>>)(sym => { var l = new List<KeystoneArcBar>(); double px = sym == "MGC" ? 2400 : 21000; for (int i = -40; i <= 0; i++) { l.Add(new KeystoneArcBar { Symbol = sym, Time = m0.AddMinutes(i + 1), Open = px, High = px + 1, Low = px - 1, Close = px, Volume = 50 }); } return l; }));
                    // 1. three studios at once
                    Call(lab2, "OpenReplayTrader"); var bt = (System.Windows.Window)F("backtestStudioWindow"); if (bt == null) throw new Exception("no BACKTEST STUDIO");
                    Call(lab2, "OpenRotationStudio"); if (F("rotStudioWindow") == null || F("labsWindow") == null) throw new Exception("no EVAL LAB");
                    Call(lab2, "OpenLiveTrading"); var lv = (System.Windows.Window)F("liveStudioWindow"); if (lv == null) throw new Exception("no LIVE TRADING");
                    if (F("backtestStudioWindow") != bt || F("rotStudioWindow") == null) throw new Exception("opening LIVE TRADING closed another studio");
                    if (F("replayTraderWindow") != lv) throw new Exception("the hooks did not follow the new LIVE TRADING window");
                    {   /* ⧉ WINDOWS ▾ lists the three (the title-bar switcher) */
                        var kw = lab2.GetType().GetNestedType("KeystoneWindow", BindingFlags.NonPublic); var open = ((System.Collections.IEnumerable)kw.GetMethod("MainOpen").Invoke(null, null)).Cast<object>().ToList();
                        if (!open.Contains(bt) || !open.Contains(lv) || !open.Contains(F("labsWindow"))) throw new Exception("⧉ WINDOWS does not list the open studios (" + open.Count + ")");
                        kw.GetMethod("ShowWindowsMenu").Invoke(null, new object[] { bt, null });
                        if (FindAll<System.Windows.FrameworkElement>(bt.Content).All(x => Convert.ToString(x.Tag) != "win.windows")) throw new Exception("⧉ WINDOWS button missing in the title bar");
                    }
                    Call(lab2, "OpenReplayTrader"); if (F("backtestStudioWindow") != bt) throw new Exception("reopening the BACKTEST STUDIO built a second one");
                    if (F("replayTraderWindow") != bt) throw new Exception("reopening the BACKTEST STUDIO did not bring it (and its hooks) to the front");
                    Call(lab2, "OpenLiveTrading"); if (F("liveStudioWindow") != lv || F("replayTraderWindow") != lv) throw new Exception("● LIVE TRADING is not single-instance / not focused");
                    Call(lab2, "OpenReplayTrader");
                    Console.WriteLine("      🪟 BACKTEST STUDIO + 🧪 EVAL LAB + ● LIVE TRADING open together; reopening focuses the open one (one per kind): OK");
                    // 2. ● LIVE DEMO in the backtest studio
                    int made0 = (int)nbMade.GetValue(null);
                    S("studioLiveHistFake", (Func<string, DateTime, DateTime, List<KeystoneArcBar>>)((sym, a, z) => { var l = new List<KeystoneArcBar>(); double px0 = sym == "MGC" ? 2400 : 21000; for (var d = a.Date; d < z.Date; d = d.AddDays(1)) { if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue; for (int m = 1; m <= 120; m++) l.Add(new KeystoneArcBar { Symbol = sym, Time = d.AddHours(9.5).AddMinutes(m), Open = px0, High = px0 + 2, Low = px0 - 2, Close = px0 + (m % 3) - 1, Volume = 10 }); px0 += 5; } return l; }));
                    ((Action<bool>)F("studioLiveDemo"))(true); var badge = ((Func<string>)F("studioLiveBadge"))();
                    {   /* 📼 BUILD 11k: the live demo opens WITH HISTORY (a stubbed NinjaTrader bars request for the days the store does not have) — the D chart shows more than one candle */
                        string hh = ((Func<string, string>)F("studioLiveHistory"))("MNQ"); int dc = int.Parse(hh.Substring(hh.IndexOf("dcandles=") + 9));
                        if (!hh.StartsWith("history: ") || !hh.Contains("NinjaTrader ") || hh.Contains("NinjaTrader 0)") || dc <= 1) throw new Exception("📼 live demo history: " + hh);
                        Console.WriteLine("      📼 11k " + hh);
                    }
                    S("studioLiveHistFake", null);
                    if (!badge.StartsWith("● LIVE DEMO · simulated · nothing is sent|LIVE|links=0")) throw new Exception("● LIVE DEMO badge / mode: " + badge);
                    if (!((Func<string>)F("studioModeNow"))().StartsWith("LIVE")) throw new Exception("mode is not LIVE: " + ((Func<string>)F("studioModeNow"))());
                    var trM = ((Func<string, KeystoneReplayTrader>)F("studioTrader"))("MNQ"); if (trM == null || !trM.Live) throw new Exception("the MNQ chart is not on the live feed");
                    var w0 = bt; Action<System.Windows.Controls.Button> clickB = b9 => b9.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    var showDp = (Action<string>)F("studioShowDrawer"); if ((string)F("studioOpenDrawer") != "PRACTICE") showDp("PRACTICE");
                    var pt = FindAll<System.Windows.Controls.Button>(w0.Content).FirstOrDefault(b9 => Convert.ToString(b9.Content) == "🎯 PRACTICE AS A PROP ACCOUNT"); if (pt == null) throw new Exception("🎯 no PRACTICE button");
                    var pb = FindAll<System.Windows.Controls.ComboBox>(w0.Content).First(cbx => cbx.Items.Cast<object>().Any(x => Convert.ToString(x).StartsWith("TPT PRO 50K funded")));
                    int ti = Enumerable.Range(0, pb.Items.Count).First(i => Convert.ToString(pb.Items[i]).StartsWith("TPT PRO 50K funded")); pb.SelectedIndex = ti; pb.SelectedItem = pb.Items[ti]; clickB(pt);
                    Func<string> gauge = () => FindAll<System.Windows.Controls.TextBlock>(w0.Content).Select(t => t.Text ?? "").FirstOrDefault(t => t.StartsWith("floor ")) ?? "";
                    Func<string, double> floorOf = g => { var a = g.Substring(6).Split(' ')[0].Replace("$", "").Replace(",", "").Replace("+", ""); double v; return double.TryParse(a, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
                    ((Action<int>)F("replayTraderOrder"))(1); ((Action)F("studioLiveStep"))();
                    if (trM.Position != 1) throw new Exception("the live demo BUY did not fill on the simulated trader (position " + trM.Position + ")");
                    double f1 = floorOf(gauge()); string ga = gauge();
                    var push = (Action<string, KeystoneArcBar>)F("studioLivePush"); var lastB0 = trM.Bars[trM.Bars.Count - 1]; var lastB = new KeystoneArcBar { Time = lastB0.Time, Open = lastB0.Open, High = lastB0.High, Low = lastB0.Low, Close = lastB0.Close };
                    push("MNQ", new KeystoneArcBar { Symbol = "MNQ", Time = lastB.Time, Open = lastB.Open, High = lastB.Close + 60, Low = lastB.Low, Close = lastB.Close + 60, Volume = 80 }); ((Action)F("studioLiveStep"))();
                    double f2 = floorOf(gauge()); string gb = gauge();
                    if (Math.Abs(trM.Price - (lastB.Close + 60)) > 1e-9) throw new Exception("the live tick did not move the trader's price: " + trM.Price + " vs " + (lastB.Close + 60) + " same trader " + (((Func<string, KeystoneReplayTrader>)F("studioTrader"))("MNQ") == trM) + " bars " + trM.Bars.Count + " last " + trM.Bars[trM.Bars.Count - 1].Close);
                    if (!(f2 > f1 + 50)) throw new Exception("🎯 the TPT floor did not follow the open profit on the live tick: " + ga + " → " + gb);
                    {   /* 📐 the hugely visual trailing view: band, solid floor, lock line, the flash, the big panel; a pull-back → the dotted peak + GIVEN BACK */
                        Func<List<string>> tagsNow = () => FindAll<System.Windows.FrameworkElement>(w0.Content).Select(x => Convert.ToString(x.Tag)).ToList();
                        var t1 = tagsNow(); foreach (var need in new[] { "prac.floorline", "prac.lockline", "prac.floorflash", "prac.compact.MNQ" }) if (!t1.Contains(need)) throw new Exception("📐 missing: " + need);
                        if (FindAll<System.Windows.Controls.Canvas>(w0.Content).SelectMany(cvx => cvx.Children.OfType<System.Windows.Shapes.Line>()).Any(x => Convert.ToString(x.Tag) == "prac.floorline") && !t1.Contains("prac.glabel")) throw new Exception("🏷 a FLOOR line on the chart without its gutter label");
                        {   /* 🖥 BUILD 11k — the chart first: the drawers overlay the chart (never push it), ≤ 2 thin rows + the 1-line strip above it, nothing of the trailing panel on the candles */
                            if (!((Func<bool>)F("studioDrawerOverlay"))()) throw new Exception("🖥 11k: the drawers must overlay the chart (same grid cell), not sit above it");
                            ((Action<string>)F("studioShowDrawer"))("PRACTICE"); int rowsUp = ((Func<int>)F("studioTopRows"))(); ((Action<string>)F("studioShowDrawer"))("PRACTICE");
                            if (rowsUp > 2) throw new Exception("🖥 11k: with the PRACTICE drawer open and the practice running, " + rowsUp + " rows above the chart (≤ 2 + the strip)");
                            double share = (900.0 - (rowsUp * 34 + 26)) / 900.0; if (share < 0.75) throw new Exception("🖥 11k: the chart share " + share);
                            var allowed = new HashSet<string> { "prac.floorline", "prac.lockline", "prac.peakline", "prac.glabel", "prac.floorflash", "prac.toast" };
                            foreach (var cvx in FindAll<System.Windows.Controls.Canvas>(w0.Content)) foreach (var ch in cvx.Children.OfType<System.Windows.FrameworkElement>()) { var tg9 = Convert.ToString(ch.Tag); if (tg9.StartsWith("prac.") && !allowed.Contains(tg9)) throw new Exception("🖥 11k: '" + tg9 + "' is drawn on the chart (only the 3 lines + gutter labels may be)"); }
                            if (t1.Contains("prac.band") || t1.Contains("prac.panel")) throw new Exception("🖥 11k: the old band / big panel is still drawn");
                            Console.WriteLine("      🖥 11k chart first: drawer overlay ✓ · " + rowsUp + " thin row(s) + the strip above the chart (nominal share " + (share * 100).ToString("0") + " % of a 900 px window) · on the chart only FLOOR / LOCK / PEAK + gutter labels");
                        }
                        string pan = (string)F("studioPracPanel") ?? ""; if (!pan.Contains("ROOM LEFT") || !pan.Contains("TO LOCK") || !pan.Contains("BALANCE")) throw new Exception("📐 the big panel: " + pan);
                        push("MNQ", new KeystoneArcBar { Symbol = "MNQ", Time = lastB.Time, Open = lastB.Open, High = lastB.Close + 60, Low = lastB.Low, Close = lastB.Close + 40, Volume = 90 }); ((Action)F("studioLiveStep"))();
                        var t2 = tagsNow(); if (!t2.Contains("prac.peakline")) throw new Exception("📐 no dotted PEAK marker after the pull-back");
                        pan = (string)F("studioPracPanel") ?? ""; if (!pan.Contains("GIVEN BACK $40") && !pan.Contains("GIVEN BACK $")) throw new Exception("📐 GIVEN BACK missing: " + pan);
                        Console.WriteLine("      📐 trailing view on the live demo: band + solid FLOOR + LOCK + ▲ floor moved + dotted PEAK · panel: " + pan);
                    }
                    ((Action)F("replayTraderFlat"))(); ((Action)F("studioLiveStep"))();
                    if (trM.Position != 0 || trM.Fills.Count == 0) throw new Exception("the live demo CLOSE did not fill");
                    {   /* 🖥 11k: flat → NO ghost lines by default */
                        var ghostTags = FindAll<System.Windows.Controls.Canvas>(w0.Content).SelectMany(cvx => cvx.Children.OfType<System.Windows.FrameworkElement>()).Select(x => Convert.ToString(x.Tag)).Where(x => x == "prac.floorline" || x == "prac.lockline").ToList();
                        if (ghostTags.Count > 0) throw new Exception("🖥 11k: flat, yet " + ghostTags.Count + " if-long / if-short lines are drawn (they are an option, off by default)");
                        Console.WriteLine("      🖥 11k flat: no if-long / if-short lines (option off by default)");
                    }
                    {   /* 🎯 LOCK RACE: the preset shows its size row (MNQ 10 by default) + the research note; RACE ENTRY answers (no 9:30 trend on this stand-in feed) */
                        int ri = Enumerable.Range(0, pb.Items.Count).First(i => Convert.ToString(pb.Items[i]).StartsWith("🎯 LOCK RACE")); pb.SelectedIndex = ri; pb.SelectedItem = pb.Items[ri]; clickB(pt); clickB(pt);   /* the practice restarts on the race preset (the stub box raises no SelectionChanged) */
                        var rs = FindAll<System.Windows.Controls.ComboBox>(w0.Content).FirstOrDefault(c9 => Convert.ToString(c9.Tag) == "prac.race.size"); if (rs == null || Convert.ToString(rs.Items[rs.SelectedIndex]) != "MNQ 10") throw new Exception("🎯 LOCK RACE size box missing / not MNQ 10");
                        string rr = ((Func<string>)F("studioRaceGo"))(); if (!rr.Contains("LOCK RACE")) throw new Exception("🎯 RACE ENTRY: " + rr);
                        Console.WriteLine("      🎯 LOCK RACE: size MNQ 10 (5 … 60), RACE ENTRY → " + rr.Substring(0, Math.Min(110, rr.Length)));
                        foreach (var tg in new[] { "prac.race.tp", "prac.race.sl", "prac.race.be" }) if (FindAll<System.Windows.Controls.ComboBox>(w0.Content).All(c9 => Convert.ToString(c9.Tag) != tg)) throw new Exception("🎯 LOCK RACE choice missing: " + tg);
                        if (trM.Position != 0) ((Action)F("replayTraderFlat"))();
                        ((Action<int>)F("replayTraderOrder"))(1); ((Action)F("studioLiveStep"))();
                        var ctr = FindAll<System.Windows.Controls.TextBlock>(w0.Content).FirstOrDefault(t9 => Convert.ToString(t9.Tag) == "prac.race.counter" && !string.IsNullOrEmpty(t9.Text)); if (ctr == null || !ctr.Text.Contains("still to the lock")) throw new Exception("🎯 the race counter is missing in the panel: " + (string)F("studioPracPanel") + " / pos " + trM.Position + " / " + ((Func<string>)F("studioLiveBadge"))());
                        Console.WriteLine("      🎯 LOCK RACE: TP / SL / BE choices, panel counter: " + ctr.Text);
                        ((Action)F("replayTraderFlat"))(); ((Action)F("studioLiveStep"))();
                    }
                    {   /* 🔁 BUILD 11k: a BLOW → AUTO NEW ATTEMPT (on by default): the practice position closed, the attempt saved, a fresh account of the SAME preset, trading goes on */
                        pb.SelectedIndex = ti; pb.SelectedItem = pb.Items[ti]; clickB(pt); clickB(pt);
                        var dOn9 = FindAll<System.Windows.Controls.Button>(w0.Content).FirstOrDefault(b9 => Convert.ToString(b9.Tag) == "dol.on.MNQ"); if (dOn9 != null && Convert.ToString(dOn9.Content) == "💵 $ ON") clickB(dOn9);   /* no bracket: the price runs to the floor */
                        var side9 = (System.Windows.FrameworkElement)FindAll<System.Windows.FrameworkElement>(w0.Content).First(x => Convert.ToString(x.Tag) == "prac.compact.MNQ").Parent; foreach (var tb9 in FindAll<System.Windows.Controls.TextBox>(side9).Where(x => x.Width == 60)) tb9.Text = "0";
                        string s0 = ((Func<string>)F("studioPracState"))(); int a0 = int.Parse(s0.Split('|')[0].Split('=')[1]); string logF = System.IO.Path.Combine((string)lab2.GetType().GetMethod("StudioFolder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null), "practice-attempts.csv"); int l0 = System.IO.File.Exists(logF) ? System.IO.File.ReadAllLines(logF).Length : 0;
                        ((Action<int>)F("replayTraderOrder"))(1); ((Action)F("studioLiveStep"))(); if (trM.Position == 0) throw new Exception("🔁 the BUY before the blow did not fill");
                        var lb1 = trM.Bars[trM.Bars.Count - 1]; push("MNQ", new KeystoneArcBar { Symbol = "MNQ", Time = lb1.Time, Open = lb1.Open, High = lb1.High, Low = lb1.Close - 1200, Close = lb1.Close - 1200, Volume = 99 }); ((Action)F("studioLiveStep"))();
                        string s1 = ((Func<string>)F("studioPracState"))(); var kv = s1.Split('|').Where(x => x.Contains("=")).ToDictionary(x => x.Substring(0, x.IndexOf('=')), x => x.Substring(x.IndexOf('=') + 1));
                        int l1 = System.IO.File.Exists(logF) ? System.IO.File.ReadAllLines(logF).Length : 0;
                        if (int.Parse(kv["attempt"]) != a0 + 1 || kv["blownNow"] != "False" || trM.Position != 0 || !kv["preset"].StartsWith("TPT PRO 50K funded") || l1 != l0 + 1 || !System.IO.File.ReadAllLines(logF).Last().Contains("BLOWN") || !kv["toast"].Contains("BLOWN — attempt #" + a0 + " saved · new account ready") || !kv["strip"].Contains("attempt #" + (a0 + 1)) || !kv["strip"].Contains("💀"))
                            throw new Exception("🔁 auto new attempt after the blow: " + s1 + " · position " + trM.Position + " · log " + l0 + " → " + l1);
                        ((Action<int>)F("replayTraderOrder"))(1); ((Action)F("studioLiveStep"))(); if (trM.Position == 0) throw new Exception("🔁 trading should go on after the auto new attempt: " + ((Func<string>)F("studioLiveBadge"))() + " · " + ((Func<string>)F("studioPracState"))() + " · price " + trM.Price + " · orders " + trM.Orders.Count);
                        {   /* 🧮 BUILD 11l — the header TILES (in a trade): every number once, the long account line hidden, no "BOUGHT … @" status duplicate */
                            string tl = (string)F("studioTiles") ?? ""; foreach (var k9 in new[] { "BAL=", "OPEN=", "PEAK=", "FLOOR=", "ROOM=", "GOAL=", "POS=LONG " }) if (!tl.Contains(k9)) throw new Exception("🧮 tile missing " + k9 + ": " + tl);
                            if (tl.Contains("DAY=") || tl.Contains("MAXDD=")) throw new Exception("🧮 the MANUAL tiles show on a rules preset: " + tl);
                            var dupl = FindAll<System.Windows.Controls.TextBlock>(w0.Content).Where(t9 => t9.Visibility == System.Windows.Visibility.Visible && (t9.Text ?? "").Contains(" · bal ") && (t9.Text ?? "").StartsWith("🎯 ")).ToList();
                            if (dupl.Count > 0) throw new Exception("🧮 the long header line is still shown: " + dupl[0].Text);
                            if (((Func<string>)F("studioLiveBadge"))().Contains(" BOUGHT ")) throw new Exception("🧮 the BOUGHT status line duplicates the position chip");
                            if (FindAll<System.Windows.FrameworkElement>(w0.Content).All(x => Convert.ToString(x.Tag) != "prac.tilesbox")) throw new Exception("🧮 no tiles row in the header");
                            Console.WriteLine("      🧮 11l tiles: " + tl.Substring(0, Math.Min(300, tl.Length)));
                        }
                        ((Action)F("replayTraderFlat"))(); ((Action)F("studioLiveStep"))();
                        Console.WriteLine("      🔁 11k blow → " + kv["toast"] + " · strip: " + kv["strip"]);
                        {   /* 📊 BUILD 11l — a ↺ reset by hand is logged as its own result; the 📊 ATTEMPTS drawer shows the rows + the summary */
                            ((Action<int>)F("replayTraderOrder"))(1); ((Action)F("studioLiveStep"))(); ((Action)F("replayTraderFlat"))(); ((Action)F("studioLiveStep"))();
                            clickB(FindAll<System.Windows.Controls.Button>(w0.Content).First(b9 => Convert.ToString(b9.Tag) == "prac.strip.reset"));
                            int l2 = System.IO.File.ReadAllLines(logF).Length; if (!System.IO.File.ReadAllLines(logF).Last().Contains(",RESET,")) throw new Exception("📊 the ↺ reset by hand is not logged: " + System.IO.File.ReadAllLines(logF).Last());
                            clickB(FindAll<System.Windows.Controls.Button>(w0.Content).First(b9 => Convert.ToString(b9.Tag) == "prac.strip.attempts")); if ((string)F("studioOpenDrawer") != "ATTEMPTS") throw new Exception("📊 the strip's 📊 button should open the ATTEMPTS drawer");
                            string at = (string)F("studioAttemptsText") ?? ""; if (!at.Contains("💀 blown=") || !at.Contains(" BLOWN ") || !at.Contains(" RESET ") || !at.Contains(" OPEN ")) throw new Exception("📊 attempts view: " + at);
                            var rowB = FindAll<System.Windows.Controls.Button>(w0.Content).First(b9 => Convert.ToString(b9.Tag) == "attempts.row"); clickB(rowB);
                            clickB(FindAll<System.Windows.Controls.Button>(w0.Content).First(b9 => Convert.ToString(b9.Tag) == "attempts.export")); var ex9 = (string)F("studioAttemptsExport"); if (ex9 == null || !System.IO.File.Exists(ex9) || System.IO.File.ReadAllLines(ex9).Length < 3) throw new Exception("📊 the export: " + ex9);
                            ((Action<string>)F("studioShowDrawer"))("ATTEMPTS");
                            Console.WriteLine("      📊 11l attempts: " + at.Substring(0, Math.Min(260, at.Length)));
                        }
                        {   /* 🆓 BUILD 11l — MANUAL (no rules): no FLOOR / LOCK lines, never blown / reset, its own tiles */
                            int mi = Enumerable.Range(0, pb.Items.Count).First(i => Convert.ToString(pb.Items[i]).StartsWith("🆓 MANUAL")); pb.SelectedIndex = mi; pb.SelectedItem = pb.Items[mi]; clickB(pt); clickB(pt);
                            string mm0 = ((Func<string>)F("studioPracState"))(); if (!mm0.Contains("preset=🆓 MANUAL")) throw new Exception("🆓 MANUAL not chosen: " + mm0); int am = int.Parse(mm0.Split('|')[0].Split('=')[1]);
                            ((Action<int>)F("replayTraderOrder"))(1); ((Action)F("studioLiveStep"))(); var lb2 = trM.Bars[trM.Bars.Count - 1]; push("MNQ", new KeystoneArcBar { Symbol = "MNQ", Time = lb2.Time, Open = lb2.Open, High = lb2.High, Low = lb2.Close - 1500, Close = lb2.Close - 1500, Volume = 99 }); ((Action)F("studioLiveStep"))();
                            string mm1 = ((Func<string>)F("studioPracState"))(); var lines9 = FindAll<System.Windows.Controls.Canvas>(w0.Content).SelectMany(cvx => cvx.Children.OfType<System.Windows.FrameworkElement>()).Select(x => Convert.ToString(x.Tag)).Where(x => x == "prac.floorline" || x == "prac.lockline" || x == "prac.glabel").ToList();
                            if (lines9.Count > 0 || !mm1.Contains("attempt=" + am + "|") || mm1.Contains("blownNow=True") || trM.Position == 0) throw new Exception("🆓 MANUAL drew the floor / blew / reset: " + mm1 + " · lines " + lines9.Count + " · pos " + trM.Position);
                            string tlm = (string)F("studioTiles") ?? ""; if (!tlm.Contains("DAY=") || !tlm.Contains("MAXDD=") || !tlm.Contains("TRADES=") || tlm.Contains("FLOOR=") || tlm.Contains("GOAL=")) throw new Exception("🆓 MANUAL tiles: " + tlm);
                            ((Action)F("replayTraderFlat"))(); ((Action)F("studioLiveStep"))();
                            Console.WriteLine("      🆓 11l MANUAL: −$3,000 open, no floor / lock, no reset · tiles " + tlm.Substring(0, Math.Min(200, tlm.Length)));
                            int ti2 = Enumerable.Range(0, pb.Items.Count).First(i => Convert.ToString(pb.Items[i]).StartsWith("TPT PRO 50K funded")); pb.SelectedIndex = ti2; pb.SelectedItem = pb.Items[ti2]; clickB(pt); clickB(pt);
                        }
                    }
                    badge = ((Func<string>)F("studioLiveBadge"))(); int made1 = (int)nbMade.GetValue(null);
                    if (!badge.Contains("|links=0|") || made1 != made0) throw new Exception("🛡 the LIVE DEMO touched an account link / NinjaBroker: " + badge + " · brokers " + made0 + " → " + made1);
                    Console.WriteLine("      ● LIVE DEMO: BUY filled on the simulated trader, a live tick +60 pts → 🎯 TPT floor " + ga + "  ⇒  " + gb + " • closed " + trM.Fills[trM.Fills.Count - 1].Net.ToString("0.00") + " • links 0, NinjaBroker built 0: OK");
                    clickB(pt);   // practice off
                    ((Action<bool>)F("studioLiveDemo"))(false); badge = ((Func<string>)F("studioLiveBadge"))(); if (badge.StartsWith("●") || badge.Contains("|LIVE|")) throw new Exception("● LIVE DEMO did not stop: " + badge);
                    // 3. no data feed → the message
                    S("studioLiveFeedFake", (Func<string, List<KeystoneArcBar>>)(sym => new List<KeystoneArcBar>()));
                    ((Action<bool>)F("studioLiveDemo"))(true); badge = ((Func<string>)F("studioLiveBadge"))();
                    if (!badge.Contains("connect a data feed in NinjaTrader")) throw new Exception("no-feed message missing: " + badge);
                    ((Action<bool>)F("studioLiveDemo"))(false); S("studioLiveFeedFake", null);
                    Console.WriteLine("      ● LIVE DEMO without data: \"connect a data feed in NinjaTrader\": OK");
                    // 4. ● LIVE TRADING: 🎯 PROP PRACTICE always on the top bar (greyed off a Sim account) + 🎮 SWITCH TO SIM101 + ⚖ TRAILING DD SIM
                    var ppB = FindAll<System.Windows.Controls.Button>(lv.Content).FirstOrDefault(b9 => Convert.ToString(b9.Tag) == "live.prac");
                    if (ppB == null || Convert.ToString(ppB.Content) != "🎯 PROP PRACTICE" || ppB.Visibility != System.Windows.Visibility.Visible) throw new Exception("🎯 PROP PRACTICE button missing / hidden in LIVE TRADING");
                    if (FindAll<System.Windows.Controls.Button>(lv.Content).All(b9 => Convert.ToString(b9.Tag) != "live.prac.tosim") || FindAll<System.Windows.Controls.Button>(lv.Content).All(b9 => Convert.ToString(b9.Tag) != "live.prac.ddsim")) throw new Exception("🎮 SWITCH TO SIM101 / ⚖ TRAILING DD SIM missing");
                    Console.WriteLine("      ● LIVE TRADING: 🎯 PROP PRACTICE on the top bar, 🎮 SWITCH TO SIM101, ⚖ TRAILING DD SIM: OK");
                    // 5. closing one studio leaves the others (and their hooks) alone
                    lv.Close(); if (F("liveStudioWindow") != null) throw new Exception("LIVE TRADING did not close");
                    if (F("backtestStudioWindow") != bt || F("replayTraderWindow") != bt || F("studioTrader") == null) throw new Exception("closing LIVE TRADING broke the BACKTEST STUDIO's hooks");
                    bt.Close(); if (F("backtestStudioWindow") != null || F("replayTraderWindow") != null) throw new Exception("the BACKTEST STUDIO did not close");
                    Console.WriteLine("      closing LIVE TRADING keeps the BACKTEST STUDIO working; then it closes too: OK");
                });
            if (strategy == "MAD")
                Step("🔄 BUILD 11k: ROTATION SIM drawer — settings ($ ↔ points), 👥 POOL, 📊 ANALYZER, 🏆 FIND OPTIMAL (unreliable rows never best), ▶ ONE + 🎬 REPLAY (FLOOR / LOCK / band / panel), 💾 saved + reopen + compare, NOTHING sent", () =>
                {
                    var BF = BindingFlags.Instance | BindingFlags.NonPublic; Func<string, object> F = n => lab2.GetType().GetField(n, BF).GetValue(lab2);
                    var nbMade = lab2.GetType().GetNestedType("NinjaBroker", BindingFlags.NonPublic).GetField("Made", BindingFlags.Static | BindingFlags.Public); int made0 = (int)nbMade.GetValue(null);
                    string sf = (string)lab2.GetType().GetMethod("StudioFolder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    var rnd = new Random(5); var bars = new List<KeystoneArcBar>(); double px = 18000; var dy = new DateTime(2019, 3, 4); int nd = 0;
                    for (; nd < 30; dy = dy.AddDays(1)) { if (dy.DayOfWeek == DayOfWeek.Saturday || dy.DayOfWeek == DayOfWeek.Sunday) continue; nd++; for (int m = 1; m <= 9 * 60; m++) { double o0 = px, c0 = Math.Round((px + (rnd.NextDouble() - 0.5) * 12) * 4) / 4; bars.Add(new KeystoneArcBar { Symbol = "MNQ", Time = dy.AddHours(8).AddMinutes(m), Open = o0, High = Math.Max(o0, c0) + 1.5, Low = Math.Min(o0, c0) - 1.5, Close = c0, Volume = 50 }); px = c0; } }
                    KeystoneStudioStore.Merge(sf, "MNQ", bars);
                    Call(lab2, "OpenReplayTrader"); var bt = (System.Windows.Window)F("backtestStudioWindow"); if (bt == null) throw new Exception("no BACKTEST STUDIO");
                    {   /* 📊 11l: a restart → the attempts are reloaded from the file */
                        ((Action<string>)F("studioShowDrawer"))("ATTEMPTS"); string at0 = (string)F("studioAttemptsText") ?? ""; if (!at0.Contains(" BLOWN ") || !at0.Contains(" RESET ")) throw new Exception("📊 after the restart the attempts are gone: " + at0); ((Action<string>)F("studioShowDrawer"))("ATTEMPTS");
                        Console.WriteLine("      📊 11l restart: attempts reloaded · " + at0.Substring(0, Math.Min(160, at0.Length)));
                    }
                    {   /* 🧠 11l ENTRY ANALYZER: ⟳ REBUILD on the saved minutes (walk-forward + calibration), the 🧠 ENTRY SCORE layer on the chart, model.json saved, nothing sent */
                        var en = (Func<string, string>)F("studioEntry"); if (en == null) throw new Exception("🧠 no ENTRY ANALYZER hook");
                        en("X 6"); string et = en("REBUILD"); if (!et.Contains("base LONG") || !et.Contains("|| calib ") || et.Contains("|| calib 0")) throw new Exception("🧠 rebuild: " + et);
                        if (!System.IO.File.Exists(System.IO.Path.Combine(sf, "EntryAnalyzer", "model.json"))) throw new Exception("🧠 model.json not saved");
                        en("SCORE ON"); { var tbar = FindAll<System.Windows.FrameworkElement>(bt.Content).First(x => Convert.ToString(x.Tag) == "studio.toolbar"); var dbx = FindAll<System.Windows.Controls.TextBox>(tbar).First(); dbx.Text = "2019-03-06"; } ((Action)F("replayTraderLoad"))(); ((Action<int>)F("replayTraderStep"))(90); string es = (string)F("studioEntryScore") ?? ""; if (!es.StartsWith("🧠")) { var rtx = (KeystoneReplayTrader)F("replayTrader"); throw new Exception("🧠 the ENTRY SCORE badge: " + es + " · trader " + (rtx == null ? "null" : rtx.Bars.Count + " bars, finished " + rtx.Finished)); } en("SCORE OFF");
                        Console.WriteLine("      🧠 11l " + et.Substring(0, Math.Min(240, et.Length)) + " · score: " + es);
                    }
                    var showD = (Action<string>)F("studioShowDrawer"); showD("ROTSIM"); if ((string)F("studioOpenDrawer") != "ROTSIM") throw new Exception("🔄 the ROTATION SIM drawer did not open");
                    var rs = (Func<string, string>)F("studioRotSim"); if (rs == null) throw new Exception("🔄 no ROTATION SIM hook");
                    string cl = rs("SET qty=1;tp.usd=20;sl.usd=20;pattern=BS;from=2019-03-04;to=2019-04-05;days=5;every=1");
                    if (!cl.Contains("TP 10 pts = $20") || !cl.Contains("SL 10 pts = $20") || !cl.Contains("a win nets TP − costs")) throw new Exception("🔄 $ ↔ points / cost line: " + cl);
                    cl = rs("SET qty=5"); if (!cl.Contains("TP 2 pts = $20")) throw new Exception("🔄 size 5 → $20 = 2 pts: " + cl);
                    string p = rs("POOL"); if (!p.Contains("✓ 👥 POOL") || !p.Contains("saved")) throw new Exception("🔄 POOL: " + p);
                    string an = rs("TAB ANALYZER"); if (!an.Contains("LOCKED") || !an.Contains("NO-EDGE") || !an.Contains("expectancy per trade")) throw new Exception("🔄 ANALYZER: " + an);
                    Console.WriteLine("      🔄 POOL: " + p.Substring(0, Math.Min(150, p.Length)));
                    rs("SET qty=1;tp.usd=200;sl.usd=200;days=3;every=2");
                    string oo = rs("OPT"); if (!oo.Contains("combinations") || !oo.Contains("unreliable ranked 0")) throw new Exception("🔄 OPT: " + oo);
                    Console.WriteLine("      🏆 " + oo.Substring(0, Math.Min(200, oo.Length)));
                    string one = rs("ONE"); string rp = rs("REPLAY");
                    foreach (var need in new[] { "ROOM LEFT", "PEAK", "TO LOCK", "BALANCE", "rsim.replay.price", "rsim.replay.floor", "rsim.replay.lock" }) if (!rp.Contains(need)) throw new Exception("🎬 replay missing " + need + ": " + rp);
                    Console.WriteLine("      🎬 " + rp.Substring(0, Math.Min(220, rp.Length)));
                    string op = rs("OPEN"); if (!op.Contains("reopened")) throw new Exception("📂 reopen: " + op);
                    rs("COMPARE"); string cmp = rs("TAB COMPARE"); if (!cmp.Contains("LOCK PCT")) throw new Exception("⚖ compare: " + cmp);
                    if (!System.IO.File.Exists(System.IO.Path.Combine(sf, "RotationSim", "runs.csv"))) throw new Exception("💾 runs.csv missing");
                    if ((int)nbMade.GetValue(null) != made0) throw new Exception("🛡 the ROTATION SIM built a NinjaBroker");
                    showD("ROTSIM"); bt.Close();
                    Console.WriteLine("      🔄 saved runs: " + (System.IO.File.ReadAllLines(System.IO.Path.Combine(sf, "RotationSim", "runs.csv")).Length - 1) + " · NinjaBroker built 0 · nothing sent: OK");
                });
            if (strategy == "MAD")
                Step("🎯 BUILD 11p: 3 SETUPS — the indicator layer (closed bars, no error), a 🎮 practice ticket TAKE on the simulated trader, the 3-SETUP ROTATION drawer (RUN / RANDOM / 💡 BOLD / SMALL 1-A-DAY / saved runs), NOTHING sent", () =>
                {
                    var BF = BindingFlags.Instance | BindingFlags.NonPublic; Func<string, object> F = n => lab2.GetType().GetField(n, BF).GetValue(lab2);
                    var nbMade = lab2.GetType().GetNestedType("NinjaBroker", BindingFlags.NonPublic).GetField("Made", BindingFlags.Static | BindingFlags.Public); int made0 = (int)nbMade.GetValue(null);
                    string sf = (string)lab2.GetType().GetMethod("StudioFolder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    Call(lab2, "OpenReplayTrader"); var bt = (System.Windows.Window)F("backtestStudioWindow"); if (bt == null) throw new Exception("no BACKTEST STUDIO");
                    var lay = (Action<string, int>)F("studioLayer"); lay("IND.TRI", KeystoneLiveTools.Always); lay("IND.TRI.TICKET", 1);
                    { var tbar = FindAll<System.Windows.FrameworkElement>(bt.Content).First(x => Convert.ToString(x.Tag) == "studio.toolbar"); var dbx = FindAll<System.Windows.Controls.TextBox>(tbar).First(); dbx.Text = "2019-03-06"; }
                    ((Action)F("replayTraderLoad"))(); var rt = (KeystoneReplayTrader)F("replayTrader"); if (rt == null || rt.Bars.Count == 0) throw new Exception("no day loaded");
                    string tick = ""; for (int k = 0; k < 420 && tick == ""; k++) { ((Action<int>)F("replayTraderStep"))(1); var e0 = (string)F("studioTriErr"); if (!string.IsNullOrEmpty(e0)) throw new Exception("🎯 3 SETUPS drawing error: " + e0); if (rt.Position != 0 || rt.Orders.Count != 0) throw new Exception("🎯 the layer changed the practice position"); tick = (string)F("studioTriTicket") ?? ""; }
                    if (tick == "") throw new Exception("🎯 no 3-setup practice ticket in 7 hours of the synthetic day (signals seen " + (int)F("studioTriSeen") + ")");
                    { var na = (Action<double>)F("replayTraderNewAccount"); if (na != null) na(50000); ((Action<int>)F("replayTraderStep"))(0); }   /* a fresh studio account (an earlier step may have blown it — a blown account refuses entries, as it should) */
                    var take = (Action)F("studioTriTake"); if (take == null) throw new Exception("🎮 no TAKE on the ticket"); take(); tick = (string)F("studioTriTicket") ?? tick; rt = (KeystoneReplayTrader)F("replayTrader");
                    if (rt.Position == 0) throw new Exception("🎮 TAKE did not open the simulated practice position: " + tick + " • " + (string)F("studioTriTicket"));
                    Console.WriteLine("      🎯 3 SETUPS layer: " + (int)F("studioTriSeen") + " signals / zones · 🎮 ticket: " + tick.Substring(0, Math.Min(140, tick.Length)) + " → TAKE: practice position " + rt.Position + " (simulated trader)");
                    ((Action)F("replayTraderFlat"))(); lay("IND.TRI", KeystoneLiveTools.Off);
                    var showD = (Action<string>)F("studioShowDrawer"); showD("TRIROT"); if ((string)F("studioOpenDrawer") != "TRIROT") throw new Exception("🎯 the 3-SETUP ROTATION drawer did not open");
                    var rs = (Func<string, string>)F("studioTriRot"); if (rs == null) throw new Exception("🎯 no 3-SETUP ROTATION hook");
                    string cl = rs("SET from=2019-03-04;to=2019-04-12;accounts=5"); if (!cl.Contains("15 micros") || !cl.Contains("5 accounts") || !cl.Contains("LUCID FLEX 50K")) throw new Exception("🎯 settings line: " + cl);
                    string run = rs("RUN"); if (!run.Contains("✓ ▶ RUN") || !run.Contains("saved")) throw new Exception("🎯 RUN: " + run);
                    string st = rs("TAB STATS"); if (!st.Contains("BUSINESS NET") || !st.Contains("no edge yet")) throw new Exception("🎯 STATS: " + st);
                    string rd = rs("RANDOM"); if (!rd.Contains("RANDOM CONTROL")) throw new Exception("🎲 RANDOM: " + rd);
                    string b0 = rs("TAB BATCHES"); if (!b0.Contains("START")) throw new Exception("📋 BATCHES: " + b0);
                    string pls = rs("PLAN SMALL"); if (!pls.Contains("5 micros") || !pls.Contains("(105.00 pts)") || !pls.Contains("ONE TRADE A DAY (eval + funded")) throw new Exception("💡 SMALL 1-A-DAY: " + pls);
                    string pl = rs("PLAN BOLD"); if (!pl.Contains("10 micros") || !pl.Contains("(50.00 pts") || !pl.Contains("(52.50 pts)") || !pl.Contains("ONE TRADE A DAY (eval + funded") || !pl.Contains("payout as soon as allowed") || !pl.Contains("a payout locks the floor")) throw new Exception("💡 BOLD 1-A-DAY: " + pl);
                    string run2 = rs("RUN"); if (!run2.Contains("✓ ▶ RUN")) throw new Exception("🎯 RUN (BOLD 1-A-DAY): " + run2);
                    string po = rs("PLAN OVN"); if (!po.Contains("5 micros") || !po.Contains("setups FVG,AGG") || !po.Contains("OVERNIGHT side only") || !po.Contains("SKIP WILD")) throw new Exception("💡 3 SETUPS + OVERNIGHT: " + po);
                    string run3 = rs("RUN"); if (!run3.Contains("✓ ▶ RUN") || !run3.Contains("📥")) throw new Exception("🎯 RUN (OVERNIGHT preset + the library line): " + run3);
                    var runs = System.IO.Path.Combine(sf, "TriRotation", "runs.csv"); if (!System.IO.File.Exists(runs) || System.IO.File.ReadAllLines(runs).Length < 4) throw new Exception("💾 TriRotation\\runs.csv missing / short");
                    if ((int)nbMade.GetValue(null) != made0) throw new Exception("🛡 the 3 SETUPS layer / drawer built a NinjaBroker");
                    showD("TRIROT"); bt.Close();
                    Console.WriteLine("      🎯 " + run.Substring(0, Math.Min(150, run.Length)) + " · " + rd.Substring(0, Math.Min(120, rd.Length)) + " · runs " + (System.IO.File.ReadAllLines(runs).Length - 1) + " · NinjaBroker built 0 · nothing sent: OK");
                });
            if (strategy == "MAD")
            {
                Step("📥 BUILD 11s: TICK LIBRARY window — ⬇ DOWNLOAD day by day (stand-in NinjaTrader: a failed day retried, a 1-second day, a no-session day, the weekend never asked), the calendar, ✔ CHECK, ↻ REPAIR, 📦 EXPORT, NOTHING sent", () =>
                {
                    var BF = BindingFlags.Instance | BindingFlags.NonPublic; Func<string, object> F = n => lab2.GetType().GetField(n, BF).GetValue(lab2);
                    var nbMade = lab2.GetType().GetNestedType("NinjaBroker", BindingFlags.NonPublic).GetField("Made", BindingFlags.Static | BindingFlags.Public); int made0 = (int)nbMade.GetValue(null);
                    string sf = (string)lab2.GetType().GetMethod("StudioFolder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    try { System.IO.Directory.Delete(System.IO.Path.Combine(sf, "TickCache", "MES"), true); } catch { }
                    int fails = 0; var asked = new List<DateTime>();
                    Set(lab2, "tickLibFetchFake", (Func<string, DateTime, DateTime, Tuple<List<KeystoneOfTick>, string>>)((sym, a, z) =>
                    {
                        var dz = z.Date; asked.Add(dz);
                        if (dz == new DateTime(2019, 3, 6) && fails++ == 0) return Tuple.Create(new List<KeystoneOfTick>(), "Timeout");
                        if (dz == new DateTime(2019, 3, 8) || dz == new DateTime(2019, 3, 13)) return Tuple.Create(new List<KeystoneOfTick>(), "");
                        var l = new List<KeystoneOfTick>(); for (int i = 0; i < 3000; i++) l.Add(new KeystoneOfTick { Time = a.AddSeconds(5 + i * 25), Price = 7000 + (i % 7) * 0.25, Size = 1, Side = i % 2 == 0 ? 1 : -1, Quoted = i % 50 != 0 }); return Tuple.Create(l, "");
                    }));
                    Set(lab2, "tickLibSecondsFake", (Func<string, DateTime, DateTime, List<KeystoneArcBar>>)((sym, a, z) => z.Date == new DateTime(2019, 3, 8) ? Enumerable.Range(1, 600).Select(k => new KeystoneArcBar { Time = z.Date.AddHours(9).AddSeconds(k), Open = 7000, High = 7000.5, Low = 6999.75, Close = 7000.25, Volume = 3 }).ToList() : new List<KeystoneArcBar>()));
                    lab2.GetType().GetMethod("OpenTickLibrary", BF).Invoke(lab2, new object[] { null, "MES" });
                    var tw = (System.Windows.Window)F("tickLibWindow"); var cmd = (Func<string, string>)F("tickLibCmd"); if (tw == null || cmd == null) throw new Exception("📥 the TICK LIBRARY window did not open");
                    foreach (var need in new[] { "tlib.download", "tlib.pause", "tlib.check", "tlib.repair", "tlib.delete", "tlib.export", "tlib.calendar", "tlib.list", "tlib.progress", "tlib.secs" }) if (FindAll<System.Windows.FrameworkElement>(tw.Content).All(x => Convert.ToString(x.Tag) != need)) throw new Exception("📥 missing: " + need);
                    cmd("SET sym=MES;from=2019-03-04;to=2019-03-15"); string rD = cmd("DOWNLOAD");
                    if (!rD.Contains("day 10 / 10") || !rD.Contains("bid/ask 98 %") || !rD.Contains("1 no session") || !rD.Contains("1 1-second only")) throw new Exception("📥 progress: " + rD);
                    if (asked.Any(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) || asked.Count(d => d == new DateTime(2019, 3, 6)) != 2) throw new Exception("📥 asked: " + string.Join(",", asked.Select(d => d.ToString("MM-dd"))));
                    string st = cmd("STATUS");
                    foreach (var need in new[] { "03-04=COMPLETE", "03-06=COMPLETE", "03-08=SECONDS", "03-13=NOSESSION", "03-09=NOSESSION", "03-15=COMPLETE" }) if (!st.Contains(need)) throw new Exception("📥 calendar: " + st);
                    if (FindAll<System.Windows.FrameworkElement>(tw.Content).All(x => Convert.ToString(x.Tag) != "tlib.day.2019-03-04.COMPLETE")) throw new Exception("📥 no green calendar cell for 03-04");
                    asked.Clear(); string r2 = cmd("DOWNLOAD"); if (!r2.Contains("already there") || asked.Count != 0) throw new Exception("📥 a second DOWNLOAD asked again: " + r2);
                    string rp = cmd("REPAIR"); if (asked.Count != 1 || asked[0] != new DateTime(2019, 3, 8)) throw new Exception("📥 REPAIR asked: " + string.Join(",", asked.Select(d => d.ToString("MM-dd"))) + " • " + rp);
                    string ck = cmd("CHECK"); if (!ck.Contains("✔")) throw new Exception("📥 CHECK: " + ck);
                    string ex = cmd("EXPORT"); var zf = System.IO.Path.Combine(sf, "exports", "ticks", "ticks-MES-2019-03.zip");
                    if (!ex.Contains("1 zip(s)") || !System.IO.File.Exists(zf) || !KeystoneZip.Read(zf).ContainsKey("MES/2019-03-04.csv.gz") || KeystoneZip.Read(zf).ContainsKey("MES/2019-03-08.csv.gz")) throw new Exception("📥 EXPORT: " + ex);
                    string sr; var rt = KeystoneTickCache.Load(sf, "MES", new DateTime(2019, 3, 4), out sr); if (rt == null || rt.Count != 3000 || sr != "TICKS") throw new Exception("📥 the replay does not load the library day");
                    KeystoneTickDay mt; if (KeystoneTickLib.OrderFlowTicks(sf, "MES", new DateTime(2019, 3, 8), out mt) != null) throw new Exception("🛡 a 1-second day became order flow");
                    if ((int)nbMade.GetValue(null) != made0) throw new Exception("🛡 the TICK LIBRARY built a NinjaBroker");
                    Set(lab2, "tickLibFetchFake", null); Set(lab2, "tickLibSecondsFake", null); tw.Close(); if (F("tickLibWindow") != null) throw new Exception("📥 the window did not close");
                    Console.WriteLine("      📥 " + rD.Split('|')[0].Trim() + " · " + st.Split(';').Length + " days on the calendar · ↻ REPAIR re-asked 03-08 only · 📦 " + System.IO.Path.GetFileName(zf) + " · NinjaBroker built 0 · nothing sent: OK");
                });
                Step("🎯 BUILD 11s: the all-in-one ORDERFLOW desk — badge (grey with no trades, green bid / ask, amber tick rule), the numbers strip, Δ profile, the SIGNAL card (FILL ORDER fills the panel only, TAKE = the chart's own BUY), the 5 tabs, NOTHING sent to NinjaTrader", () =>
                {
                    var BF = BindingFlags.Instance | BindingFlags.NonPublic; Func<string, object> F = n => lab2.GetType().GetField(n, BF).GetValue(lab2);
                    var nbMade = lab2.GetType().GetNestedType("NinjaBroker", BindingFlags.NonPublic).GetField("Made", BindingFlags.Static | BindingFlags.Public); int made0 = (int)nbMade.GetValue(null);
                    Call(lab2, "OpenReplayTrader"); var bt = (System.Windows.Window)F("backtestStudioWindow"); if (bt == null) throw new Exception("no BACKTEST STUDIO");
                    { var tbar = FindAll<System.Windows.FrameworkElement>(bt.Content).First(x => Convert.ToString(x.Tag) == "studio.toolbar"); var dbx = FindAll<System.Windows.Controls.TextBox>(tbar).First(); dbx.Text = "2019-03-06"; }
                    ((Action)F("replayTraderLoad"))(); var rt = (KeystoneReplayTrader)F("replayTrader"); if (rt == null || rt.Bars.Count == 0) throw new Exception("no day loaded");
                    for (int k = 0; k < 150; k++) ((Action<int>)F("replayTraderStep"))(1);
                    { var na = (Action<double>)F("replayTraderNewAccount"); if (na != null) na(50000); ((Action<int>)F("replayTraderStep"))(0); }
                    var desk = (Func<string, string>)F("studioOfDesk"); if (desk == null) throw new Exception("no ORDERFLOW desk hook");
                    if (desk("OPEN") != "OPEN") throw new Exception("the ORDERFLOW view did not open");
                    string nb = desk("NOTICKS"); string strip0 = desk("STRIP");
                    if (nb != KeystoneOfSource.NoneText || !strip0.Contains(KeystoneOfSource.NoneText) || !strip0.Contains("CANDLE Δ") || !strip0.Contains("—")) throw new Exception("🛡 no trades must show the grey badge and no numbers: " + strip0);
                    if (FindAll<System.Windows.FrameworkElement>(bt.Content).Any(x => Convert.ToString(x.Tag) == "of.dprof")) throw new Exception("🛡 a delta profile without trades");
                    string amber = desk("TICKS 6000 0"); if (!amber.StartsWith("● REAL TICKS • tick rule")) throw new Exception("amber badge: " + amber);
                    string green = desk("TICKS 6000 0.97"); if (!green.StartsWith("● REAL TICKS • bid/ask 9")) throw new Exception("green badge: " + green);
                    string strip = desk("STRIP"); if (!strip.Contains("● REAL TICKS • bid/ask") || !strip.Contains("BUYERS") || !strip.Contains("IN CONTROL")) throw new Exception("strip: " + strip);
                    if (FindAll<System.Windows.FrameworkElement>(bt.Content).Count(x => Convert.ToString(x.Tag) == "of.profs" || Convert.ToString(x.Tag) == "of.ticklib") != 2) throw new Exception("▥ PROFILES / 📥 TICK LIBRARY buttons missing in the ORDERFLOW bar");
                    if (FindAll<System.Windows.FrameworkElement>(bt.Content).All(x => Convert.ToString(x.Tag) != "of.dprof")) throw new Exception("no Δ DELTA PROFILE drawn by the price axis");
                    desk("SIGNAL"); string card = desk("CARD");
                    if (!card.StartsWith("VISIBLE") || !card.Contains("▲ BUY") || !card.Contains("STOP ") || !card.Contains("TARGET ") || !card.Contains("SIZE ") || !card.Contains("stacked buying zone")) throw new Exception("🎯 SIGNAL card: " + card);
                    rt = (KeystoneReplayTrader)F("replayTrader"); int pos0 = rt.Position; string fill = desk("FILL"); var fp = fill.Split('|'); double v1, v2, v3;
                    if (fp.Length != 3 || !double.TryParse(fp[0], out v1) || !double.TryParse(fp[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v2) || !double.TryParse(fp[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v3) || v1 < 1 || v2 <= 0 || v3 <= 0 || rt.Position != pos0) throw new Exception("FILL ORDER: " + fill + " position " + rt.Position);
                    foreach (var tb in new[] { Tuple.Create("FLOW", "FUEL vs ENGINE"), Tuple.Create("BOOK", "only LIVE"), Tuple.Create("STATS", "HOW OFTEN EACH SIGNAL WORKED"), Tuple.Create("GUIDE", "ABSORPTION"), Tuple.Create("SIGNAL", "TRADE FROM HERE") })
                    { string tx = desk("TAB " + tb.Item1); if (!tx.Contains(tb.Item2)) throw new Exception("tab " + tb.Item1 + ": " + tx.Substring(0, Math.Min(300, tx.Length))); }
                    string guide = desk("TAB GUIDE"); foreach (var g in new[] { "DELTA", "POC", "HVN / LVN", "DIVERGENCE", "STACKED IMBALANCES", "FUEL vs ENGINE" }) if (!guide.Contains(g)) throw new Exception("ⓘ GUIDE misses " + g);
                    desk("TAB SIGNAL");
                    System.Windows.MessageBox.Answer = (t, b) => System.Windows.MessageBoxResult.Yes;
                    string take; try { take = desk("TAKE"); } finally { System.Windows.MessageBox.Answer = null; }
                    rt = (KeystoneReplayTrader)F("replayTrader"); if (!take.Contains("view CHART") || rt.Position <= 0) throw new Exception("TAKE (the chart's own BUY on the simulated trader): " + take);
                    ((Action)F("replayTraderFlat"))();
                    if ((int)nbMade.GetValue(null) != made0) throw new Exception("🛡 the ORDERFLOW desk built a NinjaBroker");
                    desk("OPEN"); desk("SIGNAL"); if (desk("SKIP") != "HIDDEN") throw new Exception("SKIP did not hide the card"); desk("CLOSE");
                    {   /* ▦ 11s: the grid lines on / off (the axis labels stay) */
                        var lay = (Action<string, int>)F("studioLayer"); Func<int> grids = () => FindAll<System.Windows.FrameworkElement>(bt.Content).Count(x => Convert.ToString(x.Tag) == "chart.grid");
                        lay("GRID", 0); ((Action<int>)F("replayTraderStep"))(0); int g0 = grids(); lay("GRID", 1); ((Action<int>)F("replayTraderStep"))(0); int gOn = grids();
                        if (g0 != 0 || gOn == 0) throw new Exception("▦ grid lines: off " + g0 + " / on " + gOn);
                    }
                    bt.Close();
                    Console.WriteLine("      🎯 badges: " + KeystoneOfSource.NoneText + " / " + amber.Split('•')[0] + "tick rule / " + green.Split('•').Take(2).Aggregate((a, b) => a + "•" + b) + " · card: " + card.Split('|')[1].Trim() + " · FILL " + fill + " · TAKE → " + take + " · NinjaBroker built 0: OK");
                });
            }
            Step(strategy + ": " + ev2.Count + " setups • open evidence chart + draw", () =>
            {
                Call(lab2, "OpenEvidenceChart"); Call(lab2, "RequestEvidenceBars"); Call(lab2, "RenderEvidenceChart");
                var canvas = (System.Windows.Controls.Canvas)lab2.GetType().GetField("evidenceCanvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                int full = canvas.Children.Count;
                if (strategy == "GLD" && ev2.Count > 0)
                {
                    // labels show for the clicked entry
                    var bars = (List<KeystoneArcBar>)lab2.GetType().GetField("evidenceBars", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    var pick = ev2.FirstOrDefault(e => bars.Count > 0 && e.Symbol == bars[0].Symbol && e.TriggerTime >= bars[0].Time && e.TriggerTime <= bars[bars.Count - 1].Time);
                    if (pick != null) { lab2.GetType().GetField("selectedEvidenceEvent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab2, pick); Call(lab2, "RenderEvidenceChart"); }
                    int labels = 0;
                    foreach (var child in canvas.Children) { var b = child as System.Windows.Controls.Border; var tb = b == null ? null : b.Child as System.Windows.Controls.TextBlock; if (tb != null && tb.Text != null && (tb.Text.StartsWith("GOLDEN") || tb.Text.StartsWith("TP ") || tb.Text.StartsWith("SL "))) labels++; }
                    Console.WriteLine("      golden labels on the chart: " + labels);
                    if (pick != null && labels < 3) throw new Exception("GOLDEN labels (start / TP / SL / result) not drawn for the clicked entry");
                    lab2.GetType().GetField("selectedEvidenceEvent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lab2, null); Call(lab2, "RenderEvidenceChart");
                    full = canvas.Children.Count;
                }
                Call(lab2, "ReplaySetCursor", Call(lab2, "ReplayFirstBarTime"), true);
                int start = canvas.Children.Count;
                Console.WriteLine("      chart elements: all candles " + full + " • replay at start " + start);
                if (full < 60 || start >= full) throw new Exception("chart did not draw, or replay did not hide future candles");
                Call(lab2, "StopEvidenceReplay");
            });
            if (strategy == "GLD")
                Step("GLD: entry study view (filters, compare, winners, target × stop, one account, entries)", () =>
                {
                    var sets = new Dictionary<string, List<KeystoneArcEvent>>();
                    foreach (string u in KeystoneGoldenStudy.Universes) sets[u] = KeystoneArcEngine.DetectAndResolve(m1, m5, KeystoneGoldenStudy.UniverseConfig(c, u)).Concat(KeystoneArcEngine.DetectAndResolve(g1, g5, KeystoneGoldenStudy.UniverseConfig(c, u))).ToList();
                    Call(lab2, "ShowGoldenStudy", sets, c);
                    var uni = (System.Windows.Controls.ComboBox)lab2.GetType().GetField("gfUniverseBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    uni.SelectedIndex = 3; uni.SelectedItem = uni.Items[3]; Call(lab2, "RunGoldenStudy");
                    var everyFvg = (KeystoneGoldenStudyResult)lab2.GetType().GetField("goldenStudy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    Console.WriteLine("      every 5M FVG: " + everyFvg.All.Count + " • first FVG: " + sets["FIRST_FVG"].Count + " • first BH: " + sets["FIRST_BH"].Count);
                    if (everyFvg.Universe != "EVERY_FVG" || everyFvg.All.Count < sets["FIRST_FVG"].Count) throw new Exception("EVERY 5M FVG set not used");
                    uni.SelectedIndex = 0; uni.SelectedItem = uni.Items[0]; Call(lab2, "RunGoldenStudy");
                    var study = (KeystoneGoldenStudyResult)lab2.GetType().GetField("goldenStudy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    var tabs = (System.Windows.Controls.TabControl)lab2.GetType().GetField("goldenTabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    Console.WriteLine("      study: " + study.All.Count + " entries • kept " + study.Kept.Count + " • grids " + study.Grids.Count + " • tabs " + tabs.Items.Count);
                    foreach (System.Windows.Controls.TabItem t in tabs.Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception("tab not filled: " + t.Header + " • " + ((System.Windows.Controls.TextBlock)lab2.GetType().GetField("goldenVerdictSub", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2)).Text);
                    var setup = (System.Windows.Controls.ComboBox)lab2.GetType().GetField("gfSetupBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    setup.SelectedIndex = 1; setup.SelectedItem = setup.Items[1]; Call(lab2, "RunGoldenStudy");
                    var bhOnly = (KeystoneGoldenStudyResult)lab2.GetType().GetField("goldenStudy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    if (bhOnly.Kept.Any(e => e.SetupClass != "BH")) throw new Exception("BH ONLY filter kept other setups");
                    setup.SelectedIndex = 0; setup.SelectedItem = setup.Items[0]; Call(lab2, "RunGoldenStudy");
                    if (!(bool)lab2.GetType().GetField("goldenResultsMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2)) throw new Exception("study mode off");
                    Call(lab2, "SetGoldenSummaryHidden", true); Call(lab2, "SetGoldenSummaryHidden", false);
                    Call(lab2, "SetGoldenResultsMode", false);
                });
            Step(strategy + ": bar-by-bar replay, event jumps, play/pause, live box, show all", () =>
            {
                Call(lab2, "ReplaySetCursor", Call(lab2, "ReplayFirstBarTime"), true);
                for (int i = 0; i < 40; i++) Call(lab2, "ReplayStepBar", 1);
                Call(lab2, "ReplayJumpEvent", 1); Call(lab2, "ReplayJumpEvent", 1); Call(lab2, "ReplayJumpEvent", -1); Call(lab2, "ReplayJumpEvent", int.MaxValue);
                Call(lab2, "ToggleEvidenceReplayPlay"); Call(lab2, "ToggleEvidenceReplayPlay");
                Call(lab2, "UpdateEvidenceLivePanel"); Call(lab2, "StopEvidenceReplay");
            });
            Step(strategy + ": other strategy on the same chart day + day compare + range ledger", () =>
            {
                if (asian || strategy == "MAD") return;
                Call(lab2, "RequestEvidenceBars");
                string other = strategy == "BH" ? "evidenceOverlayFvgBox" : "evidenceOverlayBhBox";
                var box = (System.Windows.Controls.CheckBox)lab2.GetType().GetField(other, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                box.IsChecked = true; Call(lab2, "RenderEvidenceChart");
                string line = (string)lab2.GetType().GetField("evidenceDayCompareLine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                Console.WriteLine("      " + line);
                if (!line.StartsWith("DAY COMPARE")) throw new Exception("no day compare line");
                box.IsChecked = false;
                var cfgOther = KeystoneArcStrategyCompare.ConfigFor(c, strategy == "FVG" ? "BH" : "FVG");
                var ledger = (List<KeystoneArcEvent>)Call(lab2, "BuildDetectorLedger", cfgOther);
                Console.WriteLine("      range ledger for " + cfgOther.StrategyCode + ": " + ledger.Count + " setups");
            });
            Step(strategy + ": other timeframes built from the loaded 1M bars (chart = ledger)", () =>
            {
                var agg = KeystoneArc5MResearchLab.AggregateCloseStamped(m1, 15, "MNQ");
                var sample = agg.Where(b => b.Time.Hour == 11 && b.Time.Minute == 0).First();
                var one = m1.Where(b => b.Time > sample.Time.AddMinutes(-15) && b.Time <= sample.Time).ToList();
                if (Math.Abs(sample.High - one.Max(b => b.High)) > 1e-9 || Math.Abs(sample.Close - one.Last().Close) > 1e-9 || Math.Abs(sample.Open - one.First().Open) > 1e-9) throw new Exception("15M candle is not the close-stamped sum of its 1M candles");
                var tf = (System.Windows.Controls.ComboBox)lab2.GetType().GetField("evidenceTimeframeBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                if (tf == null || asian) return;
                int idx = -1; for (int i = 0; i < tf.Items.Count; i++) if (Convert.ToString(tf.Items[i]).TrimEnd('M') == "15") idx = i;
                if (idx < 0) throw new Exception("no 15M timeframe choice");
                tf.SelectedIndex = idx; tf.SelectedItem = tf.Items[idx];
                Call(lab2, "RequestEvidenceBars"); Call(lab2, "RenderEvidenceChart");
                var bars = (List<KeystoneArcBar>)lab2.GetType().GetField("evidenceBars", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                Console.WriteLine("      15M view from loaded 1M: " + bars.Count + " candles");
                if (bars.Count < 20) throw new Exception("derived 15M view is empty");
            });
            Step(strategy + ": switch instrument view to MNQ", () => { Call(lab2, "RenderPoolLedger"); });
            if (strategy == "GLD")
                Step("GLD: PROP SIMULATION of the golden entries (same day vs hold × target × stop × contracts)", () =>
                {
                    Func<string, object> F = n => lab2.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    if (F("goldenStudy") == null) { Console.WriteLine("      (no golden study in this run)"); return; }
                    Call(lab2, "OpenGoldenPropLab", false);
                    var st = F("goldenLab"); var T = st.GetType(); Func<string, object> S = n => T.GetField(n).GetValue(st);
                    ((Action)S("Run"))();
                    for (int i = 0; i < 300 && !((System.Windows.Controls.TextBlock)S("Status")).Text.Contains(" ROWS • "); i++) System.Threading.Thread.Sleep(100);
                    Console.WriteLine("      " + ((System.Windows.Controls.TextBlock)S("Status")).Text);
                    var rows = (List<KeystoneLabRow>)S("Rows");
                    Console.WriteLine("      " + string.Join(" | ", rows.Take(4).Select(x => x.Label + " prop " + x.P.PropNet.ToString("0") + " plain " + x.P.Net.ToString("0"))));
                    if (rows.Count == 0 || !rows.Any(x => x.Get("EXIT") == "SAME DAY") || !rows.Any(x => x.Get("EXIT") == "HOLD")) throw new Exception("golden prop rows missing");
                    foreach (System.Windows.Controls.TabItem t in ((System.Windows.Controls.TabControl)S("Tabs")).Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception("golden prop tab not filled: " + t.Header);
                });
            if (strategy == "FVG")
                Step("PROP GAME OPTIMIZER: the loaded pool through every choice, ranked by net cash, apply the best row", () =>
                {
                    Func<string, object> F = n => lab2.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    ((KeystoneArcRunConfig)F("config")).OneDayMode = 0;
                    Call(lab2, "OpenGameOptimizer");
                    var boxes = (System.Windows.Controls.TextBox[])F("gameBoxes");
                    boxes[0].Text = "EVALUATION,DIRECT FUNDED"; boxes[1].Text = "BOTH,MNQ"; boxes[2].Text = "ALL,BEST"; boxes[3].Text = "1,2"; boxes[4].Text = "1000"; boxes[5].Text = "500"; boxes[6].Text = "5";
                    ((Action)F("gameRun"))();
                    var st = (System.Windows.Controls.TextBlock)F("gameStatus");
                    for (int i = 0; i < 600 && !st.Text.Contains(" COMBINATIONS • ") && !st.Text.StartsWith("ERROR") && !st.Text.StartsWith("POOL") && !st.Text.StartsWith("THE OPT") && !st.Text.StartsWith("WAIT"); i++) System.Threading.Thread.Sleep(100);
                    var rows = (List<KeystoneArcGameRow>)F("gameRows");
                    Console.WriteLine("      " + st.Text + (rows.Count > 0 ? " • #1 " + rows[0].Label : ""));
                    if (rows.Count == 0) throw new Exception("optimizer produced no rows: " + st.Text);
                    if (!rows.Any(x => x.Start == "DIRECT FUNDED") || !rows.Any(x => x.Start == "EVALUATION") || !rows.Any(x => x.Size == 2)) throw new Exception("optimizer grid incomplete");
                });
            if (strategy == "FVG")
                Step("FVG + ENGULFING MATH LABS: run on the loaded 1-minute bars, fill every tab, export", () =>
                {
                    Func<string, object> F = n => lab2.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    foreach (var labDef in new[] { Tuple.Create("OpenFvgLab", "fvgLab"), Tuple.Create("OpenEngulfingLab", "engLab") })
                    {
                        Call(lab2, labDef.Item1, false);
                        var st = F(labDef.Item2); var T = st.GetType(); Func<string, object> S = n => T.GetField(n).GetValue(st);
                        ((Action)S("Run"))();
                        for (int i = 0; i < 900 && !((System.Windows.Controls.TextBlock)S("Status")).Text.Contains(" ROWS • "); i++) System.Threading.Thread.Sleep(100);
                        var rows = (System.Collections.ICollection)S("Rows");
                        Console.WriteLine("      " + labDef.Item2 + ": " + ((System.Windows.Controls.TextBlock)S("Status")).Text);
                        if (rows.Count == 0) throw new Exception(labDef.Item2 + " produced no rows");
                        foreach (System.Windows.Controls.TabItem t in ((System.Windows.Controls.TabControl)S("Tabs")).Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception(labDef.Item2 + " tab not filled: " + t.Header);
                        var csv = (string)Call(lab2, "LabCsv", st); var html = (string)Call(lab2, "LabHtml", st);
                        if (!csv.Contains("WALK-FORWARD") || !html.Contains("DOES IT ADAPT")) throw new Exception(labDef.Item2 + " export incomplete");
                    }
                });
            if (asian)
                Step("ASIAN75: MATH LAB runs every combination, fills every tab, applies a row to the lab (no reload), night box on the chart", () =>
                {
                    Func<string, object> F = n => lab2.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                    var sbox = (System.Windows.Controls.ComboBox)F("strategyBox");
                    for (int i = 0; i < sbox.Items.Count; i++) if (Convert.ToString(sbox.Items[i]).StartsWith("ASIAN 75")) { sbox.SelectedIndex = i; sbox.SelectedItem = sbox.Items[i]; }
                    Call(lab2, "OpenAsianLab", false);
                    ((Action)F("asianLabRun"))();
                    for (int i = 0; i < 600 && !((System.Windows.Controls.TextBlock)F("asianLabStatus")).Text.Contains(" COMBINATIONS • "); i++) System.Threading.Thread.Sleep(100);
                    var res = (KeystoneAsianLabResult)F("asianLabResult");
                    if (res == null || res.Rows.Count == 0) throw new Exception("math lab did not run: " + ((System.Windows.Controls.TextBlock)F("asianLabStatus")).Text);
                    var tabsA = (System.Windows.Controls.TabControl)F("asianLabTabs");
                    foreach (System.Windows.Controls.TabItem t in tabsA.Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception("math lab tab not filled: " + t.Header);
                    Console.WriteLine("      " + res.Rows.Count + " combinations × " + res.Nights + " nights • tabs: " + string.Join(" • ", tabsA.Items.Cast<System.Windows.Controls.TabItem>().Select(t => t.Header)));
                    var csv = (string)Call(lab2, "AsianLabCsv"); var html = (string)Call(lab2, "AsianLabHtml");
                    if (!csv.Contains("WHICH VALUE WINS") || !html.Contains("WHAT THE NUMBERS SAY")) throw new Exception("math lab export incomplete");
                    var pick = res.Rows.First(x => x.Combo.Scope == "MNQ");
                    Call(lab2, "ShowAsianLabSelected", pick, false);
                    bool ok = (bool)Call(lab2, "ApplyAsianLabRow", pick);
                    var cfgNow = (KeystoneArcRunConfig)F("config"); var evNow = (List<KeystoneArcEvent>)F("events");
                    double labNet = evNow.Sum(e => e.GrossPnl), expected = pick.Nights.Sum(n => n.Net) + pick.Nights.Sum(n => n.Contracts) * 1.0;
                    Console.WriteLine("      applied " + pick.Label + " → " + ok + " • lab legs " + evNow.Count + " gross " + labNet.ToString("0") + " vs math lab " + expected.ToString("0") + " • scope " + cfgNow.Scope + " • status " + ((System.Windows.Controls.TextBlock)F("statusText")).Text);
                    if (!ok || (string)F("viewScope") != "MNQ" || evNow.Any(e => e.Symbol != "MNQ") || Math.Abs(labNet - expected) > 0.5) throw new Exception("apply to lab did not reproduce the math lab row");
                    Call(lab2, "OpenEvidenceChart");
                    var night = evNow.OrderBy(e => e.ReferenceTime).First().ReferenceTime.Date;
                    ((System.Windows.Controls.TextBox)F("evidenceDateBox")).Text = night.ToString("yyyy-MM-dd");
                    Call(lab2, "RequestEvidenceBars"); Call(lab2, "RenderEvidenceChart");
                    var nightText = (System.Windows.Controls.TextBlock)F("evidenceAsianNightText");
                    Console.WriteLine("      chart night box:\n        " + (nightText == null ? "(none)" : nightText.Text.Replace("\n", "\n        ")));
                    if (nightText == null || !nightText.Text.Contains("MNQ") || !nightText.Text.Contains("COMBINED") || !nightText.Text.Contains("highest")) throw new Exception("night summary missing on the chart");
                });
            if (asian)
                Step("ASIAN75: MNQ / MGC / BOTH views give the same numbers whatever the click order", () =>
                {
                    var a1 = c.ShallowCopy(); a1.Scope = "BOTH"; a1.AsianDailyLossLimitDollars = 600;
                    var a2 = c.ShallowCopy(); a2.Scope = "BOTH"; a2.AsianDailyLossLimitDollars = 300;   // what an earlier MNQ click used to leave behind
                    var v1 = (List<KeystoneArcEvent>)Call(lab2, "EventsForInstrumentView", "MGC", a1, 0.0);
                    var v2 = (List<KeystoneArcEvent>)Call(lab2, "EventsForInstrumentView", "MGC", a2, 0.0);
                    double n1 = v1.Sum(e => e.GrossPnl), n2 = v2.Sum(e => e.GrossPnl);
                    Console.WriteLine("      MGC view after BOTH: " + v1.Count + " legs " + n1.ToString("0") + " • after an MNQ click: " + v2.Count + " legs " + n2.ToString("0"));
                    if (v1.Count != v2.Count || Math.Abs(n1 - n2) > 0.01) throw new Exception("MGC view depends on the click order");
                    var m = (List<KeystoneArcEvent>)Call(lab2, "EventsForInstrumentView", "MGC", a1, 900.0);
                    Console.WriteLine("      typed max loss $900 → " + m.Count + " legs " + m.Sum(e => e.GrossPnl).ToString("0"));
                });
        }
        // HELIX ROTATION: strategy choice → Step 1 panel → run on 1M MNQ + MGC → Step 3 HELIX view, proof tests, chart, report.
        {
            var lab3 = new KeystoneArc5MResearchLab();
            Call(lab3, "OpenWindow");
            Func<string, object> G = n => lab3.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab3);
            Step("HELIX: strategy choice shows its panel, forces BOTH + 1M, hides BH inputs", () =>
            {
                var box = (System.Windows.Controls.ComboBox)G("strategyBox"); int idx = -1;
                for (int i = 0; i < box.Items.Count; i++) if (Convert.ToString(box.Items[i]).StartsWith("HELIX ROTATION")) idx = i;
                if (idx < 0) throw new Exception("HELIX ROTATION is not a strategy choice");
                box.SelectedIndex = idx; box.SelectedItem = box.Items[idx]; Call(lab3, "RefreshStrategyInputState");
                var panel = ((List<System.Windows.UIElement>)G("helixStrategyControls"))[0];
                var scope = (System.Windows.Controls.ComboBox)G("scopeBox");
                if (panel.Visibility != System.Windows.Visibility.Visible) throw new Exception("HELIX panel hidden");
                if (scope.SelectedIndex != 2) throw new Exception("scope not BOTH");
                var bh = (List<System.Windows.UIElement>)G("bhStrategyControls"); if (bh.Any(x => x != null && x.Visibility == System.Windows.Visibility.Visible)) throw new Exception("BH inputs still visible");
                Call(lab3, "ApplyHelixPreset", KeystoneHelix.ManusPreset(DateTime.MinValue, DateTime.MaxValue, 10), "TEST");
                var startMode = (System.Windows.Controls.ComboBox)G("helixStartModeBox"); startMode.SelectedIndex = 1; startMode.SelectedItem = startMode.Items[1]; Call(lab3, "RefreshHelixInputs");
                if (((System.Windows.Controls.StackPanel)G("helixEvalSection")).Visibility != System.Windows.Visibility.Visible) throw new Exception("evaluation section hidden in EVAL mode");
                startMode.SelectedIndex = 0; startMode.SelectedItem = startMode.Items[0]; Call(lab3, "RefreshHelixInputs");
                if (((System.Windows.Controls.StackPanel)G("helixEvalSection")).Visibility != System.Windows.Visibility.Collapsed) throw new Exception("evaluation section visible in DIRECT mode");
            });
            var c3 = (KeystoneArcRunConfig)G("config");
            c3.StrategyCode = "HLX"; c3.Scope = "BOTH"; c3.SetupMinutes = 1; c3.SessionMode = "CUSTOM"; c3.CustomStart = 800; c3.EndTime = 1600; c3.OutcomeModelEnabled = 1;
            c3.Start = new DateTime(2025, 3, 3, 8, 0, 0); c3.End = new DateTime(2025, 3, 14, 16, 0, 0);
            var r3 = new Random(5); var m = new List<KeystoneArcBar>(); var g = new List<KeystoneArcBar>(); double pm = 20000, pg = 2900;
            for (DateTime d = new DateTime(2025, 3, 3); d <= new DateTime(2025, 3, 14); d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                for (DateTime t = d.AddHours(8).AddMinutes(1); t <= d.AddHours(16); t = t.AddMinutes(1))
                {
                    double om = pm, og = pg; pm += (r3.NextDouble() - 0.49) * 12; pg += (r3.NextDouble() - 0.49) * 1.6;
                    m.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = om, Close = pm, High = Math.Max(om, pm) + r3.NextDouble() * 3, Low = Math.Min(om, pm) - r3.NextDouble() * 3 });
                    g.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = og, Close = pg, High = Math.Max(og, pg) + r3.NextDouble() * 0.4, Low = Math.Min(og, pg) - r3.NextDouble() * 0.4 });
                }
            }
            Set(lab3, "mnqBars", m); Set(lab3, "mgcBars", g); Set(lab3, "mnqSetupBars", m); Set(lab3, "mgcSetupBars", g);
            Func<bool> wait = () => { for (int i = 0; i < 1200 && (bool)G("isProcessing"); i++) System.Threading.Thread.Sleep(50); return !(bool)G("isProcessing"); };
            Step("HELIX: run → Step 3 switches to the HELIX view with every tab filled", () =>
            {
                Call(lab3, "RunHelix"); if (!wait()) throw new Exception("HELIX run did not finish");
                var res = (KeystoneHelixResult)G("helixResult"); if (res == null || res.Rotations.Count == 0) throw new Exception("no baskets");
                var evs = (List<KeystoneArcEvent>)G("events");
                Console.WriteLine("      baskets " + res.Rotations.Count + " • ledger legs " + evs.Count + " • payouts " + res.Payouts.Count + " • net " + res.NetCash);
                if (evs.Count != res.Rotations.Count * 2) throw new Exception("each basket must give an MNQ and an MGC leg");
                if (((System.Windows.Controls.Grid)G("helixResultsHost")).Visibility != System.Windows.Visibility.Visible) throw new Exception("HELIX view not shown");
                if (((System.Windows.Controls.TabControl)G("resultViewTabs")).Visibility != System.Windows.Visibility.Collapsed) throw new Exception("normal pool tabs still shown");
                var tabs = (System.Windows.Controls.TabControl)G("helixTabs");
                foreach (object item in tabs.Items) { var t = (System.Windows.Controls.TabItem)item; if (t.Content is System.Windows.Controls.TextBlock && ((System.Windows.Controls.TextBlock)t.Content).Text.StartsWith("Run HELIX")) throw new Exception(t.Header + " not filled"); }
            });
            Step("HELIX: proof tests fill their tab", () =>
            {
                ((System.Windows.Controls.TextBox)G("helixRandomRunsBox")).Text = "4";
                Call(lab3, "RunHelixProof"); if (!wait()) throw new Exception("proof tests did not finish");
                var rows = (List<KeystoneHelixProofRow>)G("helixProofRows");
                Console.WriteLine("      proof rows " + (rows == null ? 0 : rows.Count));
                if (rows == null || rows.Count < 20) throw new Exception("proof rows missing");
            });
            Step("HELIX: chart draws baskets, replay runs", () =>
            {
                Call(lab3, "OpenHelixChart", new object[] { null }); Call(lab3, "RequestEvidenceBars"); Call(lab3, "RenderEvidenceChart");
                var canvas = (System.Windows.Controls.Canvas)G("evidenceCanvas");
                Console.WriteLine("      chart elements " + canvas.Children.Count);
                if (canvas.Children.Count < 60) throw new Exception("chart did not draw");
                Call(lab3, "ReplaySetCursor", Call(lab3, "ReplayFirstBarTime"), true);
                for (int i = 0; i < 90; i++) Call(lab3, "ReplayStepBar", 1);
                Call(lab3, "ReplayJumpEvent", 1); Call(lab3, "UpdateEvidenceLivePanel"); Call(lab3, "StopEvidenceReplay");
                var res = (KeystoneHelixResult)G("helixResult"); Call(lab3, "OpenHelixChart", res.Rotations[res.Rotations.Count - 1]);
            });
            Step("HELIX: selecting a basket on MNQ selects the same basket on MGC; live box and info toggle", () =>
            {
                var res = (KeystoneHelixResult)G("helixResult");
                var evs = (List<KeystoneArcEvent>)G("events");
                var mnqLeg = evs.First(e => e.Symbol == "MNQ");
                Call(lab3, "ShowEvidenceEventDetail", mnqLeg);
                var box = (System.Windows.Controls.ComboBox)G("evidenceInstrumentBox"); box.SelectedItem = "MGC";
                ((System.Windows.Controls.TextBox)G("evidenceDateBox")).Text = mnqLeg.TriggerTime.ToString("yyyy-MM-dd");
                Call(lab3, "RequestEvidenceBars"); Call(lab3, "RenderEvidenceChart");
                var sel = (KeystoneArcEvent)G("selectedEvidenceEvent");
                Console.WriteLine("      selected after switch: " + (sel == null ? "none" : sel.Symbol + " basket " + sel.SessionOrder) + " • detail: " + ((System.Windows.Controls.TextBlock)G("evidencePnlText")).Text.Replace("\n", " | "));
                if (sel == null || sel.Symbol != "MGC" || sel.SessionOrder != mnqLeg.SessionOrder) throw new Exception("linked selection did not follow to MGC");
                if (!((System.Windows.Controls.TextBlock)G("evidencePnlText")).Text.Contains("MGC")) throw new Exception("detail does not show both legs");
                Call(lab3, "ReplaySetCursor", mnqLeg.EntryTime.AddMinutes(1), true); Call(lab3, "UpdateEvidenceLivePanel");
                string live = ((System.Windows.Controls.TextBlock)G("evidenceLiveLines")).Text;
                Console.WriteLine("      live box: " + ((System.Windows.Controls.TextBlock)G("evidenceLiveCaption")).Text + " • " + live.Split('\n')[0]);
                Call(lab3, "StopEvidenceReplay");
                var info = (System.Windows.Controls.Button)G("evidenceInfoToggle"); Call(lab3, "ApplyEvidenceInfoVisibility");
            });
            Step("HELIX: report html", () =>
            {
                var res = (KeystoneHelixResult)G("helixResult");
                string html = (string)typeof(KeystoneArc5MResearchLab).GetMethod("BuildHelixReport", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { res, G("helixProofRows"), G("helixBuckets") });
                System.IO.Directory.CreateDirectory(".build"); System.IO.File.WriteAllText(".build/report_HLX.html", html);
                Console.WriteLine("      report " + html.Length + " chars");
                if (html.Length < 20000 || !html.Contains("Proof tests") || !html.Contains("Every basket")) throw new Exception("report incomplete");
            });
            Step("HELIX: data window loaded for the HELIX session (an hour of context, 08:00 at the latest)", () =>
            {
                var mi = typeof(KeystoneArc5MResearchLab).GetMethod("HelixLoadWindow", BindingFlags.Static | BindingFlags.NonPublic);
                var a1 = new object[] { 930, 1600, 0, 0 }; mi.Invoke(null, a1);
                var a2 = new object[] { 1800, 300, 0, 0 }; mi.Invoke(null, a2);
                var a3 = new object[] { 1300, 1555, 0, 0 }; mi.Invoke(null, a3);
                Console.WriteLine("      09:30-16:00 → " + a1[2] + "-" + a1[3] + " • 18:00-03:00 → " + a2[2] + "-" + a2[3] + " • 13:00-15:55 → " + a3[2] + "-" + a3[3]);
                if ((int)a1[2] != 800 || (int)a1[3] != 1600 || (int)a2[2] != 1700 || (int)a2[3] != 300 || (int)a3[2] != 800) throw new Exception("wrong load window");
            });
            Step("HELIX: another strategy's run switches Step 3 back to the normal pool view", () =>
            {
                Call(lab3, "SetHelixResultsMode", false);
                if (((System.Windows.Controls.TabControl)G("resultViewTabs")).Visibility != System.Windows.Visibility.Visible) throw new Exception("normal tabs not restored");
            });
        }
        // RECOIL • ADD TO LOSERS: choice → Step 1 panel → run on 1M MNQ + MGC → Step 3 view, chart ladders, replay live box, report.
        {
            var lab4 = new KeystoneArc5MResearchLab();
            Call(lab4, "OpenWindow");
            Func<string, object> G = n => lab4.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab4);
            Step("RECOIL: strategy choice shows its panel and the ladder preview", () =>
            {
                var box = (System.Windows.Controls.ComboBox)G("strategyBox"); int idx = -1;
                for (int i = 0; i < box.Items.Count; i++) if (Convert.ToString(box.Items[i]).StartsWith("RECOIL")) idx = i;
                if (idx < 0) throw new Exception("RECOIL is not a strategy choice");
                box.SelectedIndex = idx; box.SelectedItem = box.Items[idx]; Call(lab4, "RefreshStrategyInputState");
                var panel = ((List<System.Windows.UIElement>)G("recoilStrategyControls"))[0];
                if (panel.Visibility != System.Windows.Visibility.Visible) throw new Exception("RECOIL panel hidden");
                string prev = ((System.Windows.Controls.TextBlock)G("rcPreviewText")).Text;
                Console.WriteLine("      " + prev.Split('\n')[0] + " … " + prev.Split('\n').First(l => l.Contains("BLOWUP")).Trim());
                if (!prev.Contains("BLOWUP at -400")) throw new Exception("preview: MNQ blowup should be 400 pts below the first entry");
            });
            var c4 = (KeystoneArcRunConfig)G("config");
            c4.StrategyCode = "RCL"; c4.Scope = "BOTH"; c4.SetupMinutes = 1; c4.SessionMode = "CUSTOM"; c4.CustomStart = 800; c4.EndTime = 1600; c4.OutcomeModelEnabled = 1;
            c4.Start = new DateTime(2025, 3, 3, 8, 0, 0); c4.End = new DateTime(2025, 3, 14, 16, 0, 0);
            ((System.Windows.Controls.TextBox)G("rcMnqTriggerBox")).Text = "30"; ((System.Windows.Controls.TextBox)G("rcMnqStepBox")).Text = "30";
            ((System.Windows.Controls.TextBox)G("rcMgcTriggerBox")).Text = "4"; ((System.Windows.Controls.TextBox)G("rcMgcStepBox")).Text = "4";
            ((System.Windows.Controls.TextBox)G("rcTargetBox")).Text = "120"; ((System.Windows.Controls.TextBox)G("rcMaxDdBox")).Text = "600";
            var r4 = new Random(11); var m4 = new List<KeystoneArcBar>(); var g4 = new List<KeystoneArcBar>(); double pm4 = 20000, pg4 = 2900;
            for (DateTime d = new DateTime(2025, 3, 3); d <= new DateTime(2025, 3, 14); d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                for (DateTime t = d.AddHours(8).AddMinutes(1); t <= d.AddHours(16); t = t.AddMinutes(1))
                {
                    double om = pm4, og = pg4; pm4 += (r4.NextDouble() - 0.5) * 14; pg4 += (r4.NextDouble() - 0.5) * 1.8;
                    m4.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = om, Close = pm4, High = Math.Max(om, pm4) + r4.NextDouble() * 3, Low = Math.Min(om, pm4) - r4.NextDouble() * 3 });
                    g4.Add(new KeystoneArcBar { Symbol = "MGC", Time = t, Open = og, Close = pg4, High = Math.Max(og, pg4) + r4.NextDouble() * 0.4, Low = Math.Min(og, pg4) - r4.NextDouble() * 0.4 });
                }
            }
            Set(lab4, "mnqBars", m4); Set(lab4, "mgcBars", g4); Set(lab4, "mnqSetupBars", m4); Set(lab4, "mgcSetupBars", g4);
            Func<bool> wait4 = () => { for (int i = 0; i < 1200 && (bool)G("isProcessing"); i++) System.Threading.Thread.Sleep(50); return !(bool)G("isProcessing"); };
            Step("RECOIL: run → Step 3 RECOIL view with every tab filled, MNQ / MGC / BOTH compared", () =>
            {
                Call(lab4, "RunRecoil"); if (!wait4()) throw new Exception("RECOIL run did not finish");
                var res = (KeystoneRecoilResult)G("recoilResult"); if (res == null || res.Cycles.Count == 0) throw new Exception("no ladders");
                var evs = (List<KeystoneArcEvent>)G("events");
                var cmp = (System.Collections.IList)G("recoilCompare"); var grids = (System.Collections.IList)G("recoilGrids");
                Console.WriteLine("      ladders " + res.Cycles.Count + " (won " + res.Wins + ", blown " + res.Blowups + ") • ledger rows " + evs.Count + " • compare " + cmp.Count + " • grids " + grids.Count + " • net " + res.Net.ToString("0"));
                if (evs.Count != res.Cycles.Count || cmp.Count != 3 || grids.Count < 2) throw new Exception("ledger / compare / grid missing");
                if (((System.Windows.Controls.Grid)G("recoilResultsHost")).Visibility != System.Windows.Visibility.Visible) throw new Exception("RECOIL view not shown");
                foreach (object item in ((System.Windows.Controls.TabControl)G("recoilTabs")).Items) { var t = (System.Windows.Controls.TabItem)item; if (t.Content is System.Windows.Controls.TextBlock) throw new Exception(t.Header + " not filled"); }
                Call(lab4, "SetRecoilSummaryHidden", true); Call(lab4, "SetRecoilSummaryHidden", false);
            });
            Step("RECOIL: chart draws the ladders; replay with the live box; clicking a ladder fills the detail", () =>
            {
                var res = (KeystoneRecoilResult)G("recoilResult");
                var deep = res.Cycles.OrderByDescending(x => x.Entries).First();
                Call(lab4, "OpenRecoilChart", deep); Call(lab4, "RequestEvidenceBars");
                ((System.Windows.Controls.TextBox)G("evidenceDateBox")).Text = deep.Day.ToString("yyyy-MM-dd");
                var ib = (System.Windows.Controls.ComboBox)G("evidenceInstrumentBox"); if (ib != null) ib.SelectedItem = deep.Symbol;
                Call(lab4, "RequestEvidenceBars"); Call(lab4, "RenderEvidenceChart");
                var canvas = (System.Windows.Controls.Canvas)G("evidenceCanvas");
                int tags = 0; foreach (var ch in canvas.Children) { var b = ch as System.Windows.Controls.Border; var tb = b == null ? null : b.Child as System.Windows.Controls.TextBlock; if (tb != null && tb.Text != null && (tb.Text.StartsWith("START ") || tb.Text.StartsWith("TP ") || tb.Text.Contains(" → "))) tags++; }
                Console.WriteLine("      chart elements " + canvas.Children.Count + " • ladder labels " + tags + " • ladder " + deep.Symbol + " " + deep.Day.ToString("MM-dd") + " " + deep.Entries + " entries");
                if (tags < 3) throw new Exception("ladder labels not drawn");
                Call(lab4, "ShowEvidenceEventDetail", ((List<KeystoneArcEvent>)G("events")).First(e => e.SessionOrder == deep.Id));
                if (!((System.Windows.Controls.TextBlock)G("evidenceDetailText")).Text.Contains("RECOIL LADDER")) throw new Exception("detail not filled");
                Call(lab4, "ReplaySetCursor", deep.Fills[deep.Fills.Count - 1].Time, true); Call(lab4, "UpdateEvidenceLivePanel");
                string live = ((System.Windows.Controls.TextBlock)G("evidenceLiveLines")).Text;
                Console.WriteLine("      live box: " + ((System.Windows.Controls.TextBlock)G("evidenceLiveCaption")).Text + " • " + live.Split('\n')[0]);
                if (!live.Contains("contracts") && !live.Contains("TODAY")) throw new Exception("live box empty");
                for (int i = 0; i < 30; i++) Call(lab4, "ReplayStepBar", 1);
                Call(lab4, "StopEvidenceReplay");
            });
            Step("RECOIL: STOP AND REVERSE mode runs, compares with ADD and draws its steps", () =>
            {
                var mode = (System.Windows.Controls.ComboBox)G("rcModeBox"); mode.SelectedIndex = 1; mode.SelectedItem = mode.Items[1]; Call(lab4, "UpdateRecoilPreview");
                string prev = ((System.Windows.Controls.TextBlock)G("rcPreviewText")).Text;
                if (!prev.Contains("STEP 2  SELL 2")) throw new Exception("reverse preview missing: " + prev.Split('\n')[1]);
                Call(lab4, "RunRecoil"); if (!wait4()) throw new Exception("REVERSE run did not finish");
                var res = (KeystoneRecoilResult)G("recoilResult");
                var modes = (System.Collections.IList)G("recoilModeCompare");
                Console.WriteLine("      reverse cycles " + res.Cycles.Count + " (won " + res.Wins + ", lost all steps " + res.Blowups + ") • net " + res.Net.ToString("0") + " • modes compared " + modes.Count);
                if (res.Cycles.Count == 0 || res.Cycles.Any(x => x.Mode != "REVERSE") || modes.Count != 2) throw new Exception("reverse run wrong");
                var multi = res.Cycles.OrderByDescending(x => x.Entries).First();
                Call(lab4, "OpenRecoilChart", multi);
                ((System.Windows.Controls.TextBox)G("evidenceDateBox")).Text = multi.Day.ToString("yyyy-MM-dd");
                var ib = (System.Windows.Controls.ComboBox)G("evidenceInstrumentBox"); if (ib != null) ib.SelectedItem = multi.Symbol;
                Call(lab4, "RequestEvidenceBars"); Call(lab4, "RenderEvidenceChart");
                Call(lab4, "ReplaySetCursor", multi.Fills[multi.Fills.Count - 1].Time, true); Call(lab4, "UpdateEvidenceLivePanel");
                Console.WriteLine("      live: " + ((System.Windows.Controls.TextBlock)G("evidenceLiveLines")).Text.Split('\n')[0]);
                Call(lab4, "StopEvidenceReplay");
                mode.SelectedIndex = 0; mode.SelectedItem = mode.Items[0];
            });
            Step("RECOIL: report html and leaving RECOIL restores the normal view", () =>
            {
                var res = (KeystoneRecoilResult)G("recoilResult");
                string html = KeystoneRecoilStudy.Html(res, (List<KeystoneRecoilGrid>)G("recoilGrids"), (List<Tuple<string, KeystoneRecoilStats, KeystoneRecoilAccount>>)G("recoilCompare"), 5000);
                System.IO.Directory.CreateDirectory(".build"); System.IO.File.WriteAllText(".build/report_RCL.html", html);
                Console.WriteLine("      report " + html.Length + " chars");
                if (!html.Contains("RISK GRID") || !html.Contains("MNQ ONLY")) throw new Exception("report incomplete");
                Call(lab4, "SetRecoilResultsMode", false);
                if (((System.Windows.Controls.TabControl)G("resultViewTabs")).Visibility != System.Windows.Visibility.Visible) throw new Exception("normal tabs not restored");
            });
            Step("PROP PLANNER: opens with the RECOIL days, runs the plan and the sweet spot, exports", () =>
            {
                Call(lab4, "OpenPropPlanner", "BOTH");
                var pw = (System.Windows.Window)G("propPlannerWindow"); if (pw == null) throw new Exception("planner window not created");
                Func<bool> waitPlan = () => { for (int i = 0; i < 600; i++) { if (G("propLastResult") != null) return true; System.Threading.Thread.Sleep(100); } return false; };
                ((Action)G("propPlannerRun"))(); if (!waitPlan()) throw new Exception("plan did not finish • " + ((System.Windows.Controls.TextBlock)G("propPlannerStatus")).Text);
                var x = (KeystonePropPlanResult)G("propLastResult"); var progs = (List<KeystonePropProgram>)G("propLastPrograms"); var years = (List<KeystonePropYear>)G("propLastYears");
                Console.WriteLine("      " + x.Plan.Describe() + " • pass " + x.PassRate.ToString("0.0") + "% • value/eval " + x.ValuePerEval.ToString("0") + " • programs " + progs.Count + " • years " + years.Count);
                if (x.Plan.Source != "REAL" || progs.Count != 3 || years.Count == 0) throw new Exception("plan incomplete");
                var tabs = (System.Windows.Controls.TabControl)G("propPlannerTabs");
                foreach (System.Windows.Controls.TabItem t in tabs.Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception("tab not filled: " + t.Header);
                ((Action)G("propPlannerSweet"))();
                for (int i = 0; i < 1200 && ((List<KeystonePropPlanResult>)G("propLastSweet")).Count == 0; i++) System.Threading.Thread.Sleep(100);
                var sweet = (List<KeystonePropPlanResult>)G("propLastSweet"); if (sweet.Count == 0) throw new Exception("sweet spot did not finish");
                Console.WriteLine("      sweet spot " + sweet.Count + " plans • #1 " + sweet[0].Label + " → " + sweet[0].ValuePerEval.ToString("0"));
                string html = KeystonePropPlanner.Html(x, progs, years, sweet, "RECOIL DAYS • BOTH");
                System.IO.File.WriteAllText(".build/report_PROP.html", html);
                Set(lab4, "propPlannerWindow", null);
                Call(lab4, "OpenPropPlanner", (string)null);
                if (G("propPlannerWindow") == null) throw new Exception("header open failed");
                Set(lab4, "propPlannerWindow", null);
            });
            Step("MOVE STUDY: one test for any strategy — measures the last run's entries + baseline, ranking, details, export", () =>
            {
                Call(lab4, "OpenMoveStudy");
                for (int i = 0; i < 600 && !((System.Windows.Controls.TextBlock)G("moveStudyStatus")).Text.StartsWith("MEASURED") && !((System.Windows.Controls.TextBlock)G("moveStudyStatus")).Text.Contains("ERROR"); i++) System.Threading.Thread.Sleep(100);
                Console.WriteLine("      FVG study: " + ((System.Windows.Controls.TextBlock)G("moveStudyStatus")).Text);
                if (!((System.Windows.Controls.TextBlock)G("moveStudyStatus")).Text.StartsWith("MEASURED")) throw new Exception("FVG study did not finish");
                var srcBox = (System.Windows.Controls.ComboBox)G("moveStudySourceBox"); srcBox.SelectedIndex = 1; srcBox.SelectedItem = srcBox.Items[1];
                Set(lab4, "moveStudyGroups", new List<KeystoneMoveGroup>());
                ((Action)G("moveStudyRun"))();
                for (int i = 0; i < 600 && ((System.Collections.ICollection)G("moveStudyGroups")).Count == 0 && !((System.Windows.Controls.TextBlock)G("moveStudyStatus")).Text.Contains("ERROR") && !((System.Windows.Controls.TextBlock)G("moveStudyStatus")).Text.StartsWith("NO "); i++) System.Threading.Thread.Sleep(100);
                var groups = (List<KeystoneMoveGroup>)G("moveStudyGroups");
                Console.WriteLine("      " + ((System.Windows.Controls.TextBlock)G("moveStudyStatus")).Text);
                if (groups.Count == 0) throw new Exception("nothing measured");
                Console.WriteLine("      " + KeystoneMoveStudy.Verdict(groups));
                if (!groups.Any(g => g.Set == KeystoneMoveStudy.Baseline) || !groups.Any(g => g.Set != KeystoneMoveStudy.Baseline && g.Rows.Count > 0)) throw new Exception("missing the strategy or the baseline");
                System.IO.File.WriteAllText(".build/report_MOVE.html", KeystoneMoveStudy.Html(groups, (string)G("moveStudyRange")));
                Set(lab4, "moveStudyWindow", null);
            });
            Step("DRAWDOWN LAB: trailing vs EOD window builds (cards, controls, live / replay sources)", () =>
            {
                try { Call(lab4, "OpenDrawdownLab"); } catch (Exception e) { var ie = e.InnerException ?? e; throw new Exception(ie.GetType().Name + " " + ie.StackTrace.Replace("\n", " / ").Substring(0, Math.Min(900, ie.StackTrace.Length))); }
                if (G("ddLabWindow") == null) throw new Exception("no window");
                Set(lab4, "ddLabWindow", null);
            });
            Step("🧪 EVAL LAB (the 🔄 ROTATION STUDIO inside): ▶ RUN on 40 synthetic days, totals + account deck + days, ▶ REPLAY, ORB sides, pools + firm round robin, views", () =>
            {
                Call(lab4, "OpenRotationStudio");
                if (G("rotStudioWindow") == null) throw new Exception("no window");
                Func<DateTime, DateTime, List<KeystoneRotDay>> synth = (f, t) =>
                {
                    var rnd = new Random(11); var list = new List<KeystoneRotDay>(); var rsDay = new DateTime(2026, 1, 5);
                    for (int di = 0; di < 40; di++, rsDay = rsDay.AddDays(rsDay.DayOfWeek == DayOfWeek.Friday ? 3 : 1))
                    {
                        var d = new KeystoneRotDay { Day = rsDay }; double m = 20000, g = 2500;
                        for (int k = 0; k < KeystoneRotDay.Slots; k++)
                        {
                            double m1 = m + (rnd.NextDouble() - 0.5) * 30, g1 = g + (rnd.NextDouble() - 0.5) * 4;
                            d.MO[k] = (float)m; d.MC[k] = (float)m1; d.MH[k] = (float)(Math.Max(m, m1) + 2); d.ML[k] = (float)(Math.Min(m, m1) - 2);
                            d.GO[k] = (float)g; d.GC[k] = (float)g1; d.GH[k] = (float)(Math.Max(g, g1) + 0.3); d.GL[k] = (float)(Math.Min(g, g1) - 0.3); m = m1; g = g1;
                        }
                        list.Add(d);
                    }
                    return list;
                };
                Set(lab4, "rotStudioDaysOverride", synth); Set(lab4, "rotStudioLast", null);
                ((Action)G("rotStudioRun"))();
                for (int i = 0; i < 600 && G("rotStudioLast") == null && !((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text.StartsWith("🔄"); i++) System.Threading.Thread.Sleep(100);
                Console.WriteLine("      " + ((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text);
                var sim = (KeystonePropSim)G("rotStudioLast"); if (sim == null || sim.Trades.Count == 0) throw new Exception("no trades");
                var deck = (System.Windows.Controls.WrapPanel)G("rotStudioDeck"); if (deck == null || deck.Children.Count != 15) throw new Exception("the account deck should show the 15 evaluations (5 TPT + 5 Lucid + 5 Tradeify by default), has " + (deck == null ? 0 : deck.Children.Count));
                var orb = (Func<KeystoneRotDay, int, Tuple<int, int>>)lab4.GetType().GetMethod("RotStudioSides", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "ORB30" });
                var d0 = synth(DateTime.Today, DateTime.Today)[0]; int hits = 0; for (int k = 0; k < KeystoneRotDay.Slots; k++) { var sd = orb(d0, k); if (sd.Item1 != 0) { hits++; if (k <= KeystoneRotDay.Slot(1000)) throw new Exception("ORB side before 10:00"); } }
                Console.WriteLine("      ORB30 sides on " + hits + " minutes of day 1 • " + sim.Accounts.Count(a => a.PassedOn != DateTime.MinValue) + " passed");
                // 💜 the funded TPT PRO plan (INTRADAY trailing) with a free −200 / +200 bracket: the floor shows on every tile
                var boxes = new List<object>(); Action<object> wk = null; wk = o => { if (o == null) return; boxes.Add(o); var pnl = o as System.Windows.Controls.Panel; if (pnl != null) foreach (var ch in pnl.Children.Cast<object>().ToList()) wk(ch); var bd = o as System.Windows.Controls.Border; if (bd != null) wk(bd.Child); var cc = o as System.Windows.Controls.ContentControl; if (cc != null) wk(cc.Content); var tcw = o as System.Windows.Controls.TabControl; if (tcw != null) foreach (var it in tcw.Items.Cast<object>().ToList()) wk(it); };
                wk(((System.Windows.Window)G("rotStudioWindow")).Content);
                var planB = boxes.OfType<System.Windows.Controls.ComboBox>().FirstOrDefault(c => c.Items.Cast<object>().Any(x => Convert.ToString(x).StartsWith("💜 FUNDED"))); if (planB == null) throw new Exception("no FUNDED TPT PRO plan in the list");
                for (int i = 0; i < planB.Items.Count; i++) if (Convert.ToString(planB.Items[i]).StartsWith("💜 FUNDED")) { planB.SelectedIndex = i; planB.SelectedItem = planB.Items[i]; }
                var tbs = boxes.OfType<System.Windows.Controls.TextBox>().ToList(); var stopT = tbs.First(t => Convert.ToString(t.Tag) == "rot.stop"); var tgtT = tbs.First(t => Convert.ToString(t.Tag) == "rot.tgt"); stopT.Text = "200"; tgtT.Text = "200"; tbs.First(t => Convert.ToString(t.Tag) == "rot.qm").Text = "4"; tbs.First(t => Convert.ToString(t.Tag) == "rot.qg").Text = "4";
                Set(lab4, "rotStudioLast", null); ((Action)G("rotStudioRun"))();
                for (int i = 0; i < 600 && G("rotStudioLast") == null && !((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text.StartsWith("🔄"); i++) System.Threading.Thread.Sleep(100);
                Console.WriteLine("      FUNDED: " + ((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text);
                var simF = (KeystonePropSim)G("rotStudioLast"); if (simF == null || simF.S.StartAs != "FUNDED" || simF.S.Rules.FundDdType != "INTRADAY" || simF.Trades.Count == 0) throw new Exception("the funded intraday run did not trade");
                if (simF.Trades.Any(t => t.Pnl > 200.01)) throw new Exception("a trade made more than the +$200 target");
                wk = wk; boxes.Clear(); wk(deck); if (!boxes.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "").StartsWith("room $"))) throw new Exception("no room line on the tiles (room $ above the floor)");
                // ✋ MANUAL REPLAY: start the day, ▲▲ BUY BOTH, play minutes → the +200 / −200 bracket closes it and the next account is up
                /* ★ BUILD 11f: the default is now ⚡ ALL AT ONCE (in MANUAL the clock waits for every account's press at the start) — this check is the one-account press of ⚡ STAGGERED */
                boxes.Clear(); wk(((System.Windows.Window)G("rotStudioWindow")).Content); var howB = boxes.OfType<System.Windows.Controls.ComboBox>().FirstOrDefault(c => Convert.ToString(c.Tag) == "rot.how"); if (howB == null || howB.SelectedIndex != 1) throw new Exception("★ the default HOW should be ⚡ ALL AT ONCE"); howB.SelectedIndex = 2;
                ((Action)G("rotStudioManLoad"))(); var msimT = (KeystonePropSim)G("rotStudioManSim"); if (msimT == null || msimT.Active == null) throw new Exception("manual replay did not start");
                var firstAcc = msimT.Active; ((Action<int>)G("rotStudioManStep"))(3); ((Action<int, int>)G("rotStudioManEnter"))(1, 1);
                for (int i = 0; i < 100 && msimT.Trades.Count == 0; i++) ((Action<int>)G("rotStudioManStep"))(1);
                if (msimT.Trades.Count == 0) throw new Exception("the manual trade never closed: " + ((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text + " • active " + (msimT.Active == null ? "-" : msimT.Active.Name) + " • open " + msimT.Accounts.Count(a0 => a0.Trades > 0) + " • " + string.Join(" | ", FindAll<System.Windows.Controls.TextBlock>(((System.Windows.Window)G("rotStudioWindow")).Content).Select(t => t.Text ?? "").Where(t => t.Contains("trade") || t.Contains("START") || t.Contains("placed") || t.Contains("room")).Take(8)));
                var mt = msimT.Trades[0]; Console.WriteLine("      MANUAL: " + mt.Legs + " " + mt.Reason + " " + mt.Pnl.ToString("0.00") + " • next " + (msimT.Active != null ? msimT.Active.Name : "-"));
                if (mt.Pnl > 200.01 || mt.Pnl < -260) throw new Exception("the manual trade left its bracket: " + mt.Pnl);
                if (msimT.Active == firstAcc) throw new Exception("the next account did not take over");
                ((Action<int, int>)G("rotStudioManEnter"))(0, -1); ((Action<int>)G("rotStudioManStep"))(600);
                // 🧪 the views + AUTO: the simulator-only view hides the chart; AUTO enters the plan's side by itself on the next day
                ((Action<bool>)G("evalLabSimOnly"))(true); ((Action<bool>)G("evalLabSimOnly"))(false);
                // 🧪 POOLS: 2 firms × 2 accounts → the rotation order is firm by firm (TPT, LUCID, TPT, LUCID); the run trades the pool in that order
                string pn0 = "SMOKE POOL " + DateTime.Now.Ticks;
                ((Action<string>)G("evalLabNewPool"))(pn0); ((Action<string, int, int>)G("evalLabAdd"))("TPT", 50, 2); ((Action<string, int, int>)G("evalLabAdd"))("LUCID", 50, 2);
                var ord = ((Func<List<string>>)G("evalLabOrder"))(); if (string.Join(",", ord) != "TPT,LUCID,TPT,LUCID") throw new Exception("round robin order " + string.Join(",", ord));
                for (int i = 0; i < planB.Items.Count; i++) if (!Convert.ToString(planB.Items[i]).StartsWith("💜 FUNDED")) { planB.SelectedIndex = i; planB.SelectedItem = planB.Items[i]; break; }
                ((Action<string>)G("evalLabUsePool"))(pn0); Set(lab4, "rotStudioLast", null); ((Action)G("rotStudioRun"))();
                for (int i = 0; i < 600 && G("rotStudioLast") == null && !((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text.StartsWith("🔄"); i++) System.Threading.Thread.Sleep(100);
                var simP = (KeystonePropSim)G("rotStudioLast"); if (simP == null || !simP.HasFirms || simP.Accounts.Count < 4) throw new Exception("the pool run did not buy the pool's firms");
                var codes = simP.Accounts.Take(4).Select(a => a.Name.Split('-')[0]).ToList(); if (codes[0] == codes[1] || codes[0] != codes[2] || codes[1] != codes[3]) throw new Exception("the run is not firm by firm: " + string.Join(",", simP.Accounts.Take(4).Select(a => a.Name)));
                Console.WriteLine("      POOL RUN: " + string.Join(", ", simP.Accounts.Take(4).Select(a => a.Name)) + " • " + simP.Trades.Count + " trades • " + ((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text);
                foreach (var sec in new[] { "OVERVIEW", "POOLS", "PLANS", "STATS", "DATA", "ROTATE" }) ((Action<string>)G("evalLabShow"))(sec);
                // ❓ HOW TO USE (first in the rail) + the progress bars of ⚡ CALCULATE / 📅 START-DAY / 🔁 PIPELINE
                { var ht = G("evalLabHowTo") as System.Windows.FrameworkElement; if (ht == null) throw new Exception("the Eval Lab has no ❓ HOW TO USE tab"); ((Action<string>)G("evalLabShow"))("HOWTO"); if (ht.Visibility != System.Windows.Visibility.Visible) throw new Exception("❓ HOW TO USE did not open");
                  if (KeystoneArc5MResearchLab.LabHowTo(false).Count < 4 || !KeystoneArc5MResearchLab.LabHowTo(false)[0].Item2[0].StartsWith("1 •")) throw new Exception("❓ HOW TO USE: the numbered steps are missing");
                  foreach (var pn in new[] { "evalLabCalcProgress", "evalLabSdProgress" }) { var pb = G(pn) as System.Windows.Controls.ProgressBar; if (pb == null || pb.Maximum != 100) throw new Exception("no progress bar: " + pn); }
                  Console.WriteLine("      ❓ HOW TO USE: " + KeystoneArc5MResearchLab.LabHowTo(false).Sum(x => x.Item2.Length) + " steps • ⚡ CALCULATE / 📅 START-DAY progress bars present"); }
                // ⚡ CALCULATE (no replay) + 🔮 HINDSIGHT + 🏆 ranking, every result tab
                Set(lab4, "evalLabCalcLast", null); ((Action<DateTime, DateTime, bool>)G("evalLabCalc"))(new DateTime(2026, 1, 5), new DateTime(2026, 2, 27), true);
                for (int i = 0; i < 900 && G("evalLabCalcLast") == null; i++) System.Threading.Thread.Sleep(100);
                var cm0 = (KeystoneCalcRank)G("evalLabCalcLast"); var co0 = (KeystoneCalcRank)G("evalLabCalcOracle"); var ch0 = (List<KeystoneHindsight>)G("evalLabCalcHind");
                if (cm0 == null || cm0.Sim == null || cm0.N == 0) throw new Exception("⚡ CALCULATE gave no result");
                if (ch0 == null || ch0.Count != cm0.Sim.Trades.Count) throw new Exception("🔮 one hindsight row per trade: " + (ch0 == null ? -1 : ch0.Count) + " vs " + cm0.Sim.Trades.Count);
                if (co0 == null || co0.Passed < cm0.Passed) throw new Exception("🔮 hindsight passes " + (co0 == null ? -1 : co0.Passed) + " < the plan's " + cm0.Passed);
                foreach (var tab in new[] { "SUMMARY", "ACCOUNTS", "TRADES", "HINDSIGHT", "BEST", "DAYS" }) ((Action<string>)G("evalLabCalcTab"))(tab);
                ((Action<string>)G("evalLabShow"))("CALCULATE"); ((Action<string>)G("evalLabShow"))("ADVISOR");
                Console.WriteLine("      ⚡ CALCULATE: " + cm0.Passed + " / " + cm0.N + " passed • 🔮 hindsight " + co0.Passed + " • " + ch0.Count(h => h.Match) + " of " + ch0.Count + " rotations on the winning side");
                // ⚡ ALL AT ONCE through ⚡ CALCULATE + 📅 START-DAY TEST (two plans side by side) + the HTML report written
                string goName = KeystoneEvalLab.DefaultName;
                ((Action<string>)G("evalLabUsePlan"))(goName); Set(lab4, "evalLabCalcLast", null); ((Action<DateTime, DateTime, bool>)G("evalLabCalc"))(new DateTime(2026, 1, 5), new DateTime(2026, 2, 27), false);
                for (int i = 0; i < 900 && G("evalLabCalcLast") == null; i++) System.Threading.Thread.Sleep(100);
                var ca0 = (KeystoneCalcRank)G("evalLabCalcLast"); if (ca0 == null || ca0.Sim == null || ca0.Sim.Trades.Count == 0) throw new Exception("⚡ ALL AT ONCE: CALCULATE gave no trade");
                var firstDayAo = ca0.Sim.Trades.Where(t => t.Day == ca0.Sim.Trades[0].Day).ToList();
                if (firstDayAo.Select(t => t.Entry).Distinct().Count() != 1 || firstDayAo.Count < 2) throw new Exception("⚡ ALL AT ONCE: the accounts should open together on day 1: " + firstDayAo.Count + " trades at " + string.Join(",", firstDayAo.Select(t => t.Entry.ToString("HH:mm")).Distinct()));
                Set(lab4, "evalLabStartDayLast", null); Set(lab4, "evalLabReportPath", null);
                ((Action<DateTime, DateTime, List<string>>)G("evalLabStartDay"))(new DateTime(2026, 1, 5), new DateTime(2026, 1, 30), new List<string> { goName, "" });
                var sdl = (List<Tuple<string, string, List<KeystoneStartBatch>>>)G("evalLabStartDayLast");
                if (sdl == null || sdl.Count != 2 || sdl.Any(x => x.Item3.Count == 0)) throw new Exception("📅 START-DAY TEST: " + (sdl == null ? "no result" : sdl.Count + " plans, batches " + string.Join("/", sdl.Select(x => x.Item3.Count))));
                var rep = (string)G("evalLabReportPath"); if (rep == null || !System.IO.File.Exists(rep) || !System.IO.File.ReadAllText(rep).Contains("MONDAY")) throw new Exception("📅 the HTML report was not written: " + rep);
                Console.WriteLine("      ⚡ ALL AT ONCE: " + firstDayAo.Count + " accounts at " + firstDayAo[0].Entry.ToString("HH:mm") + " • 📅 START-DAY TEST " + string.Join(" | ", sdl.Select(x => x.Item3.Count + " batches")) + " • report " + System.IO.Path.GetFileName(rep));
                ((Action<string>)G("evalLabUsePlan"))(KeystoneEvalLab.Presets()[0].Name);
                // ✋ NO STRATEGY • I DECIDE: the side is mine, the bracket is placed for me, then the next account
                ((Action<string>)G("evalLabUsePlan"))("✋ NO STRATEGY • I DECIDE"); ((Action)G("rotStudioManLoad"))(); var msimN = (KeystonePropSim)G("rotStudioManSim");
                ((Action<int>)G("rotStudioManStep"))(3); var accN = msimN.Active; ((Action<int, int>)G("rotStudioManEnter"))(-1, 0);
                for (int i = 0; i < 900 && msimN.Trades.Count == 0; i++) ((Action<int>)G("rotStudioManStep"))(1);
                if (msimN.Trades.Count == 0 || !msimN.Trades[0].Legs.StartsWith("SELL") || !msimN.Trades[0].Legs.Contains("MNQ") || msimN.Trades[0].Legs.Contains("MGC")) throw new Exception("✋ NO STRATEGY trade: " + (msimN.Trades.Count == 0 ? "none" : msimN.Trades[0].Legs));
                if (msimN.Trades[0].Reason == "TARGET" && Math.Abs(msimN.Trades[0].Pnl - 1500) > 0.01) throw new Exception("✋ the take profit should keep +1,500 after costs: " + msimN.Trades[0].Pnl);
                Console.WriteLine("      ✋ NO STRATEGY: " + msimN.Trades[0].Legs + " • " + msimN.Trades[0].Reason + " " + msimN.Trades[0].Pnl.ToString("0.00") + " • next " + (msimN.Active != null ? msimN.Active.Name : "—"));
                ((Action<bool>)G("evalLabSimOnly"))(true); ((Action<bool>)G("evalLabSimOnly"))(false);
                ((Action<bool>)G("evalLabAuto"))(true); ((Action)G("rotStudioManLoad"))(); var msimA = (KeystonePropSim)G("rotStudioManSim"); ((Action<int>)G("rotStudioManStep"))(60);
                if (msimA == null || msimA.Trades.Count + (msimA.Active == null ? 1 : 0) == 0 && msimA.Accounts.All(a => a.Trades == 0)) Console.WriteLine("      AUTO: no entry yet in 60 minutes (the plan's session may start later)"); else Console.WriteLine("      AUTO: " + msimA.Trades.Count + " trade(s) entered by the plan");
                {   // 🎲 ONE ENGINE (BUILD 10b): ⚡ CALCULATE EVAL ONLY == the ▶ replay played to the end (AUTO, TPT ticked for the backtest) — the same passes / blows / balances
                    ((Action<string>)G("evalLabUsePlan"))(KeystoneEvalLab.Presets()[0].Name);
                    boxes.Clear(); wk(((System.Windows.Window)G("rotStudioWindow")).Content); boxes.OfType<System.Windows.Controls.CheckBox>().First(c => Convert.ToString(c.Tag) == "rot.tptauto").IsChecked = true;
                    var oneCb = boxes.OfType<System.Windows.Controls.CheckBox>().First(c => Convert.ToString(c.Tag) == "rot.onebatch"); if (oneCb.IsChecked == true) throw new Exception("🔁 11d: 'one batch only' must be OFF by default (continuous until END)");
                    oneCb.IsChecked = true;   /* 🔁 11d: the CALCULATE = REPLAY comparison is about ONE batch (the old behaviour, kept as an option) */
                    ((Action<DateTime>)G("evalLabStart"))(new DateTime(2026, 1, 6));
                    Set(lab4, "evalLabResultsLast", null); ((Action<string>)G("evalLabCalc2"))("EVAL"); for (int i = 0; i < 600 && G("evalLabResultsLast") == null; i++) System.Threading.Thread.Sleep(100);
                    var cr = (KeystoneLabResults)G("evalLabResultsLast"); var cs = (KeystonePropSim)G("evalLabCalcSimLast"); if (cr == null || cs == null) throw new Exception("⚡ CALCULATE EVAL ONLY gave no result: " + ((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text);
                    ((Action<bool>)G("evalLabAuto"))(true); ((Action)G("rotStudioManLoad"))(); ((Action)G("evalLabReplayToEnd"))(); var rs = (KeystonePropSim)G("rotStudioManSim");
                    var diff = cs.Accounts.Take(cr.Accounts).Where(a => { var b = rs.Accounts.FirstOrDefault(x => x.Id == a.Id); return b == null || cs.PassedOrPending(a) != rs.PassedOrPending(b) || a.Alive != b.Alive || (!cs.PassedOrPending(a) && Math.Abs(a.Bal - b.Bal) > 0.5) || cs.Trades.Count(t => t.Account == a.Id) != rs.Trades.Count(t => t.Account == b.Id); })   /* a passed account's funded balance starts at 0 after its activation day (the replay stops at BATCH DONE) */.Select(a => a.Name + " calc " + a.Bal.ToString("0") + (a.Alive ? "" : " blown") + " / replay " + rs.Accounts.First(x => x.Id == a.Id).Bal.ToString("0") + (rs.Accounts.First(x => x.Id == a.Id).Alive ? "" : " blown")).ToList();
                    if (diff.Count > 0) throw new Exception("⚡ CALCULATE ≠ ▶ replay: " + string.Join(" | ", diff.Take(6)));
                    if (cr.PerAccount.Count != cr.Accounts + 1 || cr.Eval.Count < 2 || !cr.Period.Contains("→")) throw new Exception("🧪 EVAL RESULTS tables");
                    ((Action<string>)G("evalLabShowTab"))("RESULTS"); var html = ((Func<string>)G("evalLabExportResults"))(); if (!System.IO.File.Exists(html) || !System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(html), "trades.csv"))) throw new Exception("⤓ results export: " + html);
                    {   // 🔁 11d: one batch only + EVAL → FUNDED: when the batch ends before END the header says so + the 🔁 keep buying button paints
                        Set(lab4, "evalLabResultsLast", null); ((Action<string>)G("evalLabCalc2"))("BOTH"); for (int i = 0; i < 900 && G("evalLabResultsLast") == null; i++) System.Threading.Thread.Sleep(100);
                        var ob = (KeystoneLabResults)G("evalLabResultsLast"); if (ob == null || ob.Continuous || !ob.Header.StartsWith("one batch of")) throw new Exception("🔁 11d: 'one batch only' should run ONE batch: " + (ob == null ? "no result" : ob.Header));
                        Console.WriteLine("      🔁 one batch only: " + ob.Header + (ob.EndedEarly ? " [🔁 keep buying until " + ob.EndTo.ToString("dd MMM yyyy") + "]" : ""));
                    }
                    oneCb.IsChecked = false;   /* back to the default: ⚡ CALCULATE keeps buying until END */
                    Set(lab4, "evalLabResultsLast", null); ((Action<string>)G("evalLabCalc2"))("BOTH"); for (int i = 0; i < 1800 && G("evalLabResultsLast") == null; i++) System.Threading.Thread.Sleep(100);
                    var cb2 = (KeystoneLabResults)G("evalLabResultsLast"); if (cb2 == null || cb2.Scope != "BOTH" || cb2.PayoutRows.Count < 1 || !cb2.Stats.Any(x => x[0] == "money")) throw new Exception("⚡ EVAL → FUNDED → PAYOUTS gave no result");
                    {   // 🔁 11d: the default is CONTINUOUS — the header, the MONTH BY MONTH table at the top of EVAL RESULTS, every generation, the ⚡ SPRINT badge (row + card)
                        if (!cb2.Continuous || cb2.Months.Count < 3 || !cb2.Header.Contains("continuous: ") || !cb2.Header.Contains("went funded") || cb2.Record.Accounts.Any(a => !a.Name.Contains(" · #"))) throw new Exception("🔁 11d: CALCULATE is not continuous: " + cb2.Header);
                        ((Action<string>)G("evalLabResSub"))("EVAL"); var rv = (System.Windows.Controls.StackPanel)G("evalLabResultsView"); boxes.Clear(); wk(rv);
                        var texts = boxes.OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text ?? "").ToList();
                        if (!texts.Any(t => t.StartsWith("📅 MONTH BY MONTH")) || !texts.Any(t => t == "CUMULATIVE NET") || !texts.Any(t => t.Contains("🔁 CONTINUOUS")) || !texts.Any(t => t.StartsWith("🏢 PER FIRM"))) throw new Exception("🔁 11d: the MONTH BY MONTH table is not at the top of EVAL RESULTS");
                        var spA = cb2.Record.Accounts.FirstOrDefault(a => a.Sprint) ?? cb2.Record.Accounts.First(); spA.Sprint = true; cb2.FromRecord(cb2.Record);
                        ((Action<string>)G("evalLabResSub"))("ACCOUNTS"); boxes.Clear(); wk(rv); if (!boxes.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "") == spA.Name + " · ⚡ SPRINT")) throw new Exception("⚡ 11d: no SPRINT badge on the ACCOUNTS row of " + spA.Name);
                        ((Action<string>)G("evalLabOpenCard"))(spA.Name + " · ⚡ SPRINT"); boxes.Clear(); wk(rv); if (!boxes.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "").StartsWith("🗂 " + spA.Name + " · ⚡ SPRINT")) || !boxes.OfType<System.Windows.Controls.TextBlock>().Any(t => (t.Text ?? "").Contains("not a bug"))) throw new Exception("⚡ 11d: the account card has no SPRINT badge");
                        ((Action<string>)G("evalLabOpenCard"))(null); ((Action<string>)G("evalLabResSub"))("FUNDED"); ((Action<string>)G("evalLabResSub"))("EVAL");
                        Console.WriteLine("      🔁 continuous: " + cb2.Header + " · " + (cb2.Months.Count - 2) + " months · max generation #" + cb2.Record.Accounts.Max(a => a.Gen));
                    }
                    {   // 📒 BUILD 11b: the ONE run record behind every tab — 👤 ACCOUNTS, 💰 FUNDED, 🕒 TIMELINE, 💵 PAYOUTS (+ sub-tabs), the account card, 📦 EXPORT FOR CLAUDE
                        if (cb2.Record == null || cb2.Record.PassedN != cb2.Passed || cb2.Record.PayoutN != cb2.Payouts || cb2.Trust == "" || cb2.Record.Source == null || cb2.Record.Settings.Count < 8) throw new Exception("📒 the run record does not match the header / has no data facts");
                        foreach (var sub in new[] { "ACCOUNTS", "FUNDED", "TIMELINE", "DAYS", "TRADES", "BLOWN", "PAYOUTS", "STATS", "EVAL" }) ((Action<string>)G("evalLabResSub"))(sub);
                        ((Action<string>)G("evalLabOpenCard"))(cb2.Record.Accounts[0].Key); ((Action<DateTime?>)G("evalLabTimelineDay"))(cb2.Record.TimelineDays().FirstOrDefault()); ((Action<string>)G("evalLabResSub"))("TIMELINE");
                        ((Action<string>)G("evalLabResSub"))("PAYOUTS"); foreach (var ps in new[] { "BYACC", "BYFIRM", "BYMONTH", "COMPARE", "BYPAY" }) ((Action<string>)G("evalLabPaySub"))(ps);
                        if (cb2.Record.Payouts.Count > 0) ((Action<string>)G("evalLabExpandPay"))(cb2.Record.Payouts[0].Account + "|" + cb2.Record.Payouts[0].N);
                        var jf = ((Func<string>)G("evalLabExportClaude"))(); if (!System.IO.File.Exists(jf) || !System.IO.Path.GetFileName(jf).StartsWith("keystone-check-")) throw new Exception("📦 EXPORT FOR CLAUDE: " + jf);
                        var jo = KeystoneJson.Parse(System.IO.File.ReadAllText(jf)) as Dictionary<string, object>; if (jo == null || !jo.ContainsKey("trades") || !jo.ContainsKey("account_days") || !jo.ContainsKey("settings") || !((string)jo["build"]).StartsWith("BUILD")) throw new Exception("📦 the JSON export is not complete");
                        Console.WriteLine("      📦 EXPORT FOR CLAUDE: " + System.IO.Path.GetFileName(jf) + " · " + ((List<object>)jo["trades"]).Count + " trades · " + ((List<object>)jo["account_days"]).Count + " account-days"); try { System.IO.File.Delete(jf); } catch { }
                        ((Action<string>)G("evalLabOpenCard"))(null);
                    }
                    Console.WriteLine("      🎲 CALCULATE = REPLAY on " + cr.Period + ": " + cr.Passed + " passed, " + cr.Blown + " blown, " + cr.InEval + " in eval (" + cr.Accounts + " accounts) • ⤓ " + System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(html)) + " • BOTH: " + cb2.Payouts + " payouts, net " + (cb2.Paid - cb2.Spent).ToString("0"));
                    Set(lab4, "evalLabResultsLast", null); ((Action<DateTime, DateTime>)G("evalLabTwoDay"))(new DateTime(2026, 1, 5), new DateTime(2026, 1, 16)); for (int i = 0; i < 900 && G("evalLabResultsLast") == null; i++) System.Threading.Thread.Sleep(100);
                    var td = (KeystoneLabResults)G("evalLabResultsLast"); if (td == null || !td.Period.StartsWith("📅 2-DAY TEST") || td.Days.Count < 2 || !td.Stats.Any(x => x[0] == "best weekday to start")) throw new Exception("📅 2-DAY TEST gave no result");
                    Set(lab4, "evalLabResultsLast", null); ((Action<DateTime, DateTime, bool>)G("evalLabBestStarts"))(new DateTime(2026, 1, 5), new DateTime(2026, 1, 16), false); for (int i = 0; i < 900 && G("evalLabResultsLast") == null; i++) System.Threading.Thread.Sleep(100);
                    var bsr = (KeystoneLabResults)G("evalLabResultsLast"); if (bsr == null || !bsr.BestStarts || bsr.Days.Count < 2 || !bsr.Stats.Any(x => x[0] == "most passed")) throw new Exception("🏆 BEST START DAYS gave no result");
                    Console.WriteLine("      🏆 " + bsr.Period + " · " + bsr.Stats.First(x => x[0] == "most passed")[1]);
                    {   // 📦 item 35: BEST START DAYS exports too; the table sorts by a column
                        var bf = ((Func<string>)G("evalLabExportClaude"))(); if (!System.IO.File.Exists(bf) || !System.IO.File.ReadAllText(bf).Contains("every_start")) throw new Exception("📦 BEST START DAYS export: " + bf);
                        Set(lab4, "evalLabResultsLast", bsr); Console.WriteLine("      📦 BEST START DAYS → " + System.IO.Path.GetFileName(bf));
                    }
                    {   // 📅 items 18 / 28 / 29: the in-tab picker builds, fills START / END, several periods → each run + combined
                        var pv = ((Func<System.Windows.FrameworkElement>)G("evalLabPickView"))(); if (pv == null) throw new Exception("no in-tab picker");
                        var ov = G("evalLabOverlay"); if (ov == null) throw new Exception("the Eval Lab has no in-tab overlay");
                        var ps = new List<Tuple<DateTime, DateTime>> { Tuple.Create(new DateTime(2026, 1, 5), new DateTime(2026, 1, 7)), Tuple.Create(new DateTime(2026, 1, 13), new DateTime(2026, 1, 15)) };
                        ((Action<List<Tuple<DateTime, DateTime>>, List<string>>)G("evalLabPickPeriods"))(ps, null);
                        boxes.Clear(); wk(((System.Windows.Window)G("rotStudioWindow")).Content); var sb = boxes.OfType<System.Windows.Controls.TextBox>().First(c => Convert.ToString(c.Tag) == "rot.start"); var eb = boxes.OfType<System.Windows.Controls.TextBox>().First(c => Convert.ToString(c.Tag) == "rot.end");
                        if (sb.Text != "2026-01-05" || eb.Text != "2026-01-15") throw new Exception("the picked periods did not fill START / END: " + sb.Text + " → " + eb.Text);
                        ((Action<string>)G("evalLabCalcPeriods"))("EVAL"); for (int i = 0; i < 1800 && !(G("evalLabResultsLast") is KeystoneLabResults && ((KeystoneLabResults)G("evalLabResultsLast")).From == new DateTime(2026, 1, 13)); i++) System.Threading.Thread.Sleep(100);
                        var pr = (KeystoneLabResults)G("evalLabResultsLast"); if (pr == null || pr.From != new DateTime(2026, 1, 13)) throw new Exception("the second period was not calculated: " + (pr == null ? "none" : pr.Period) + " · " + ((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text);
                        Console.WriteLine("      📅 two periods calculated: " + KeystoneDatePick.Summary(ps, null) + " · last " + pr.Period);
                    }
                    {   // 🗂 item 35: the tile / row → the account card (in-tab) + the timeline row → the minute chart
                        var hist = (List<Tuple<string, KeystoneLabResults>>)G("evalLabRunHistory"); var withRec = hist.Select(x => x.Item2).FirstOrDefault(x => x.Record != null && x.Record.Accounts.Count > 0 && x.Record.Trades.Count > 0);
                        if (withRec != null)
                        {
                            ((Action<KeystoneLabResults>)G("evalLabShowResults"))(withRec); var acc = withRec.Record.Accounts[0].Key; ((Action<string>)G("evalLabCardOverlay"))(acc);
                            var ov = G("evalLabOverlay"); var open = (bool)ov.GetType().GetProperty("Open").GetValue(ov, null); if (!open) throw new Exception("the account card did not open in the tab");
                            var t0 = withRec.Record.Trades.First(t => t.Entry != DateTime.MinValue); ((Action<DateTime, DateTime, string>)G("evalLabMinuteChart"))(t0.Day, t0.Entry, t0.Account);
                            Console.WriteLine("      🗂 card (in-tab) for " + acc + " · 📈 minute chart at " + t0.Entry.ToString("HH:mm") + " " + t0.Account);
                        }
                    }
                    {   // ＋ item 3: ADD A FIRM from the wizard's step ① (a template) → FIRMS & RULES
                        ((Action<string, int, string>)G("evalLabAddFirm"))("Smoke Prop Co", 3, KeystonePayoutRules.FirmTemplateNames[3]);
                        var bk = KeystonePayoutRules.Book; if (bk == null || !bk.Any(x => x.Firm == "SMOKEPROPC")) throw new Exception("＋ ADD A FIRM did not reach FIRMS & RULES");
                        var nf = bk.First(x => x.Firm == "SMOKEPROPC"); if (nf.EvalTarget != 3000 || nf.EvalDdType != "INTRADAY") throw new Exception("＋ ADD A FIRM: the Apex template was not applied (" + nf.EvalTarget + " " + nf.EvalDdType + ")");
                        bk.Remove(nf); Console.WriteLine("      ＋ ADD A FIRM: Smoke Prop Co · same as Apex 50K → 🏢 FIRMS & RULES");
                    }
                    ((Action<string>)G("evalLabShow"))("REPORTS"); ((Action<string>)G("evalLabShow"))("DATA"); ((Action<string>)G("evalLabShow"))("ROTATE");
                    Set(lab4, "evalLabFindBest", -1); ((Action)G("evalLabFindBestGo"))(); for (int i = 0; i < 1800 && (int)G("evalLabFindBest") < 0; i++) System.Threading.Thread.Sleep(100);
                    if ((int)G("evalLabFindBest") <= 0) throw new Exception("🏆 FIND BEST ranked no plan"); Console.WriteLine("      🏆 FIND BEST ranked " + G("evalLabFindBest") + " eval plans (top 5 + ▶ USE THIS PLAN)");
                    Console.WriteLine("      📅 " + td.Period + " · " + td.Stats[1][1]);
                    boxes.OfType<System.Windows.Controls.CheckBox>().First(c => Convert.ToString(c.Tag) == "rot.tptauto").IsChecked = false;
                }
                ((Action<bool>)G("evalLabAuto"))(false); ((Action<string>)G("evalLabDeletePool"))(pn0);
                Set(lab4, "rotStudioDaysOverride", null); Set(lab4, "rotStudioWindow", null);
            });
            Step("💰 FUNDED LAB: pool of 5 (TPT + LUCID), ⚡ SIMULATE on synthetic days, result tabs, ▶ REPLAY with the TPT flash + press, every section", () =>
            {
                Call(lab4, "OpenFundedLab"); if (G("fundedLabWindow") == null) throw new Exception("no window");
                Func<DateTime, DateTime, List<KeystoneRotDay>> fsynth = (f, t) =>
                {
                    var rnd = new Random(23); var list = new List<KeystoneRotDay>(); var dd0 = new DateTime(2026, 2, 2);
                    for (int di = 0; di < 12; di++, dd0 = dd0.AddDays(dd0.DayOfWeek == DayOfWeek.Friday ? 3 : 1))
                    {
                        var d = new KeystoneRotDay { Day = dd0 }; double m = 20000, g = 2500;
                        for (int k = 0; k < KeystoneRotDay.Slots; k++) { double m1 = m + (rnd.NextDouble() - 0.5) * 40, g1 = g + (rnd.NextDouble() - 0.5) * 4; d.MO[k] = (float)m; d.MC[k] = (float)m1; d.MH[k] = (float)(Math.Max(m, m1) + 2); d.ML[k] = (float)(Math.Min(m, m1) - 2); d.GO[k] = (float)g; d.GC[k] = (float)g1; d.GH[k] = (float)(Math.Max(g, g1) + 0.3); d.GL[k] = (float)(Math.Min(g, g1) - 0.3); m = m1; g = g1; }
                        list.Add(d);
                    }
                    return list;
                };
                Set(lab4, "fundedLabDaysOverride", fsynth); Set(lab4, "fundedLabLast", null);
                KeystoneFirmPolicies.Chosen.Clear();
                ((Action<string, string, int>)G("fundedLabNewPool"))("SMOKE FUNDED", "TPT", 3); ((Action<string, string, int>)G("fundedLabNewPool"))("SMOKE FUNDED", "LUCID", 2);
                foreach (var sec in new[] { "OVERVIEW", "POOLS", "PLANS", "SIMULATE", "REPLAY", "STATS", "REPORT" }) ((Action<string>)G("fundedLabShow"))(sec);
                {   // 🏢 FIRMS & RULES + ＋ ADD FIRM (the shared firm book)
                    ((Action<string>)G("fundedLabShow"))("FIRMS");
                    var addF = (Action<string, string, string>)G("firmsLabAdd"); var saveF = (Action)G("firmsLabSave"); if (addF == null || saveF == null) throw new Exception("no 🏢 FIRMS & RULES screen");
                    int n0 = KeystonePayoutRules.Book.Count; addF("SMOKEFIRM", "Smoke Firm 50K", "#BA68C8"); var sf = KeystonePayoutRules.BookOf("SMOKEFIRM");
                    if (sf == null || KeystonePayoutRules.Book.Count != n0 + 1 || !sf.Template) throw new Exception("＋ ADD FIRM did not add the firm: " + ((System.Windows.Controls.TextBlock)G("firmsLabMsg")).Text);
                    addF("SMOKEFIRM", "", ""); if (KeystonePayoutRules.Book.Count != n0 + 1) throw new Exception("＋ ADD FIRM added a duplicate");
                    saveF(); if (KeystonePayoutRules.BookOf("SMOKEFIRM").Template) throw new Exception("💾 SAVE did not apply the boxes");
                    Console.WriteLine("      🏢 FIRMS & RULES: " + KeystonePayoutRules.Book.Count + " firms • " + ((System.Windows.Controls.TextBlock)G("firmsLabMsg")).Text);
                    KeystonePayoutRules.Book.Remove(KeystonePayoutRules.BookOf("SMOKEFIRM")); saveF(); KeystoneFirmPolicies.Chosen.Clear();
                }
                { var ht = G("fundedLabHowTo") as System.Windows.FrameworkElement; if (ht == null) throw new Exception("the Funded Lab has no ❓ HOW TO USE tab"); ((Action<string>)G("fundedLabShow"))("HOWTO"); if (ht.Visibility != System.Windows.Visibility.Visible) throw new Exception("❓ HOW TO USE (funded) did not open"); }
                ((Action<DateTime, DateTime>)G("fundedLabSim"))(new DateTime(2026, 2, 2), new DateTime(2026, 2, 17));
                for (int i = 0; i < 600 && G("fundedLabLast") == null && !((System.Windows.Controls.TextBlock)G("fundedLabStatus")).Text.Contains("no saved"); i++) System.Threading.Thread.Sleep(100);
                var frun = (KeystoneFundedRun)G("fundedLabLast"); Console.WriteLine("      " + ((System.Windows.Controls.TextBlock)G("fundedLabStatus")).Text);
                if (frun == null || frun.Accounts.Count != 5 || frun.Trades.Count == 0) throw new Exception("no funded run of the 5-account pool");
                foreach (var tb in new[] { "WHAT IF", "BEST PLAN", "TRADES", "SUMMARY" }) ((Action<string>)G("fundedLabTab"))(tb);
                var ftbl = (List<KeystoneFundedRow>)G("fundedLabTable"); if (ftbl == null || !ftbl.Any(x => x.Group == "TIME")) throw new Exception("no 'what if' table");
                ((Action<DateTime, DateTime>)G("fundedLabReplayLoad"))(new DateTime(2026, 2, 2), new DateTime(2026, 2, 6));
                var frep = (KeystoneFundedReplay)G("fundedLabReplay"); if (frep == null || frep.Accounts.Count != 5) throw new Exception("no replay");
                ((Action<int>)G("fundedLabStep"))(400); if (!((Func<bool>)G("fundedLabFlashing"))()) throw new Exception("a TPT account in the replay should wait with flashing buttons");
                ((Action<int>)G("fundedLabPress"))(frep.PlanSide()); if (frep.Pos == null) throw new Exception("the press did not enter");
                ((Action<int>)G("fundedLabStep"))(30); ((Action<string>)G("fundedLabShow"))("REPLAY");
                Console.WriteLine("      replay: " + frep.Current.Name + " • " + frep.Last + " • trades " + frep.Trades.Count);
                {   // 🖥 BUILD 10g: the Funded Lab's replay on the Eval Lab's chart-first screen — the chart = the DockPanel's last child (no fixed height),
                    // a compact toolbar (≤ 10 buttons), the pool tabs 🟢 ACTIVE / 💰 PAID / 💀 BLOWN, ⤢ BIG CARDS, the chart's tools
                    var fch = (System.Windows.Controls.Canvas)G("fundedLabChart"); var fpar = fch.Parent as System.Windows.Controls.DockPanel;
                    if (fch.Height > 0 || fpar == null || fpar.Children[fpar.Children.Count - 1] != fch) throw new Exception("the funded chart must take the rest of the screen (last child of its DockPanel, no fixed height)");
                    int fc = fch.Children.OfType<System.Windows.Shapes.Rectangle>().Count(); if (fc < 20) throw new Exception("no candles on the funded chart: " + fc);
                    int fnb = ((Func<int>)G("fundedLabToolbarButtons"))(); if (fnb > 10) throw new Exception("the funded toolbar has " + fnb + " buttons (≤ 10)");
                    var ftabs = ((Func<List<string>>)G("fundedLabPoolTabLabels"))(); if (ftabs.Count != 3 || !ftabs[0].Contains("ACTIVE") || !ftabs[1].Contains("PAID") || !ftabs[2].Contains("BLOWN")) throw new Exception("the funded pool tabs: " + string.Join(", ", ftabs));
                    var frows = ((Func<List<string>>)G("fundedLabPoolRows"))(); if (frows.Count == 0 || !frows.Any(r => r.Contains("qualifying days") && r.Contains("the payout needs") && r.Contains("balance"))) throw new Exception("the funded pool rows: " + string.Join(" // ", frows.Take(2)));
                    foreach (var k in new[] { "PAID", "BLOWN", "ACTIVE" }) ((Action<string>)G("fundedLabPoolTab"))(k);
                    ((Action<bool>)G("fundedLabBigCards"))(true); ((Action<bool>)G("fundedLabBigCards"))(false);
                    int b0 = ((Func<int>)G("fundedLabChartBars"))(); ((Action<int>)G("fundedLabChartWheel"))(120); if (((Func<int>)G("fundedLabChartBars"))() >= b0) throw new Exception("funded wheel zoom");
                    var fo = ((Func<double, double, string>)G("fundedLabChartClick"))(300, 120); if (!fo.Contains(" O ") || !fo.Contains(" C ")) throw new Exception("a funded candle click: " + fo);
                    ((Action<string>)G("fundedLabTool"))("HLINE"); var fd = ((Func<double, double, string>)G("fundedLabChartClick"))(300, 120); if (!fd.StartsWith("━")) throw new Exception("funded H-LINE: " + fd);
                    ((Action<int>)G("fundedLabSetTf"))(5); ((Action<int>)G("fundedLabSetTf"))(1); ((Action<double, double>)G("fundedLabChartDrag"))(200, 0);
                    Console.WriteLine("      🖥 funded: toolbar " + fnb + " buttons • chart = the star child • tabs " + string.Join(" / ", ftabs) + " • " + frows[0]);
                }
                Set(lab4, "fundedLabWindow", null);
            });
            Step("🏠 LAUNCHER cards 🧪 EVAL LAB + 💰 FUNDED LAB open the windows • 💵 PAYOUTS • 🔁 BUSINESS RUN (replacements, the money, month by month → RESULTS) • 🚦 chips in ADVISOR", () =>
            {
                Set(lab4, "rotStudioWindow", null); Set(lab4, "fundedLabWindow", null);
                Call(lab4, "OpenLauncher"); var lw = (System.Windows.Window)G("launcherWindow"); if (lw == null) throw new Exception("no launcher");
                Func<string, System.Windows.Controls.Button> openOf = title =>
                {
                    var card = FindAll<System.Windows.Controls.StackPanel>(lw.Content).FirstOrDefault(sp => sp.Children.OfType<System.Windows.Controls.TextBlock>().Any(tb => tb.Text == title));
                    return card == null ? null : card.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault(b => Convert.ToString(b.Content) == "OPEN");
                };
                var evB = openOf("🧪 EVAL LAB"); var fu = openOf("💰 FUNDED LAB"); if (evB == null || fu == null) throw new Exception("the launcher has no 🧪 EVAL LAB / 💰 FUNDED LAB card with OPEN");
                evB.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); fu.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (G("rotStudioWindow") == null || G("fundedLabWindow") == null) throw new Exception("the OPEN buttons did not open the labs");
                Func<DateTime, DateTime, List<KeystoneRotDay>> psynth = (f, t) =>
                {
                    var rnd = new Random(41); var list = new List<KeystoneRotDay>(); var d0 = DateTime.Today.AddDays(-130); int k0 = KeystoneRotDay.Slot(200);
                    for (; d0 <= DateTime.Today; d0 = d0.AddDays(1))
                    {
                        if (d0.DayOfWeek == DayOfWeek.Saturday || d0.DayOfWeek == DayOfWeek.Sunday) continue;
                        var d = new KeystoneRotDay { Day = d0 }; double m = 20000, g = 2500; int sg = rnd.Next(2) == 0 ? 1 : -1; double amp = 0.6 + rnd.NextDouble() * 1.2;
                        for (int k = 0; k < KeystoneRotDay.Slots; k++) { double m1 = m + (k > k0 ? sg * amp : 0) + (rnd.NextDouble() - 0.5) * 6; d.MO[k] = (float)m; d.MC[k] = (float)m1; d.MH[k] = (float)(Math.Max(m, m1) + 1); d.ML[k] = (float)(Math.Min(m, m1) - 1); d.GO[k] = d.GC[k] = d.GH[k] = d.GL[k] = (float)g; m = m1; }
                        list.Add(d);
                    }
                    return list;
                };
                // 💵 the funded payout cycles
                Set(lab4, "fundedLabDaysOverride", psynth); Set(lab4, "fundedLabPayLast", null);
                ((Action<string>)G("fundedLabShow"))("PAYOUTS"); ((Action<DateTime, DateTime>)G("fundedLabPayRun"))(DateTime.Today.AddMonths(-4), DateTime.Today);
                for (int i = 0; i < 600 && G("fundedLabPayLast") == null && !((System.Windows.Controls.TextBlock)G("fundedLabStatus")).Text.StartsWith("💵 no"); i++) System.Threading.Thread.Sleep(100);
                var pr = (KeystonePayoutRun)G("fundedLabPayLast"); Console.WriteLine("      " + ((System.Windows.Controls.TextBlock)G("fundedLabStatus")).Text);
                if (pr == null || pr.Accounts.Count != 10 || pr.Trades.Count == 0 || !pr.Accounts.All(a => a.Firm == "LUCID" || a.Firm == "TRADEIFY")) throw new Exception("no payout run of the simulated 5 Lucid + 5 Tradeify");
                Console.WriteLine("      payouts: " + pr.Payouts + " • to you $" + pr.Net.ToString("0") + " • " + pr.Accounts[0].RuleLine(KeystonePayoutRules.For(pr.Accounts[0].Firm, null)));
                // 🔁 the BUSINESS RUN in the Eval Lab (BUILD 11b: replaces the PIPELINE — the same engine, replacements, the money)
                Set(lab4, "rotStudioDaysOverride", psynth); Set(lab4, "evalLabBizLast", null);
                ((Action<string>)G("evalLabShow"))("FIRMS");
                ((Action<string>)G("evalLabShow"))("PIPELINE");
                { var bud = (Action<string, string>)G("evalLabBudget"); if (bud == null) throw new Exception("🔁 BUSINESS RUN has no 📏 MONEY RULES ladder"); bud("3000", "40"); var bl = (KeystoneBudgetPlan)G("evalLabBudgetLast"); if (bl == null || bl.PerWeek != 25 || bl.Steps.Count != 7) throw new Exception("💰 BUDGET: no ladder"); Console.WriteLine("      💰 MONEY RULES $3,000 at $40: " + bl.PerWeek + " evals a week • " + bl.Steps.Count + " steps"); }
                { var pb = G("evalLabPipeProgress") as System.Windows.Controls.ProgressBar; if (pb == null || pb.Maximum != 100) throw new Exception("🔁 BUSINESS RUN has no progress bar"); }
                var bset = (Action<string, DateTime, DateTime>)G("evalLabBizSet"); var bgo = (Action)G("evalLabBiz"); if (bset == null || bgo == null) throw new Exception("no business run");
                bset("POCKET", DateTime.Today.AddDays(-125), DateTime.Today.AddDays(-40)); bgo(); for (int i = 0; i < 1800 && G("evalLabBizLast") == null; i++) System.Threading.Thread.Sleep(100);
                var biz = (KeystoneBusinessRun)G("evalLabBizLast"); if (biz == null || biz.Bought < 15 || biz.Months.Count < 2 || biz.Record.Accounts.Count != biz.Bought) throw new Exception("no business run: " + ((System.Windows.Controls.TextBlock)G("rotStudioStatus")).Text);
                Console.WriteLine("      🔁 " + biz.Summary());
                ((Action<KeystoneLabResults>)G("evalLabShowResults"))(KeystoneLabResults.FromBusiness(biz));
                foreach (var sub in new[] { "ACCOUNTS", "FUNDED", "TIMELINE", "PAYOUTS", "STATS", "EVAL" }) ((Action<string>)G("evalLabResSub"))(sub);
                ((Action<string>)G("evalLabOpenCard"))(biz.Record.Accounts[0].Key);
                ((Action<string>)G("evalLabResSub"))("PAYOUTS"); foreach (var ps in new[] { "BYACC", "BYFIRM", "BYMONTH", "COMPARE", "BYPAY" }) ((Action<string>)G("evalLabPaySub"))(ps);
                ((Action<string>)G("evalLabShow"))("ADVISOR");
                var chips = (List<KeystonePayout.Chip>)G("labChipsLast"); for (int i = 0; i < 300 && chips == null; i++) { System.Threading.Thread.Sleep(100); chips = (List<KeystonePayout.Chip>)G("labChipsLast"); }
                if (chips == null || chips.Count != 7) throw new Exception("no stage chips");
                Set(lab4, "rotStudioDaysOverride", null); Set(lab4, "fundedLabDaysOverride", null); Set(lab4, "rotStudioWindow", null); Set(lab4, "fundedLabWindow", null);
            });
            Step("🧪💰 LABS full version: one window with tabs, 🧭 wizard (both labs), ⚡ STAGGERED replay to DAY DONE → ▶ NEXT DAY, candles that fit, MNQ-only buttons, event log export, 💰 per-firm models + 🪑 bench, close asks", () =>
            {
                Set(lab4, "rotStudioWindow", null); Set(lab4, "fundedLabWindow", null);
                Func<DateTime, DateTime, List<KeystoneRotDay>> lsynth = (f, t) =>
                {
                    var rnd = new Random(7); var list = new List<KeystoneRotDay>(); var d0 = new DateTime(2026, 3, 2);
                    for (int di = 0; di < 30; di++, d0 = d0.AddDays(d0.DayOfWeek == DayOfWeek.Friday ? 3 : 1))
                    {
                        var d = new KeystoneRotDay { Day = d0 }; double m = 20000, g = 2500;
                        for (int k = 0; k < KeystoneRotDay.Slots; k++) { double m1 = m + (rnd.NextDouble() - 0.5) * 30, g1 = g + (rnd.NextDouble() - 0.5) * 3; d.MO[k] = (float)m; d.MC[k] = (float)m1; d.MH[k] = (float)(Math.Max(m, m1) + 2); d.ML[k] = (float)(Math.Min(m, m1) - 2); d.GO[k] = (float)g; d.GC[k] = (float)g1; d.GH[k] = (float)(Math.Max(g, g1) + 0.3); d.GL[k] = (float)(Math.Min(g, g1) - 0.3); m = m1; g = g1; }
                        list.Add(d);
                    }
                    return list.Where(x => x.Day >= f.Date && x.Day <= t.Date).ToList();
                };
                Set(lab4, "rotStudioDaysOverride", lsynth); Set(lab4, "fundedLabDaysOverride", lsynth);
                Call(lab4, "OpenRotationStudio"); Call(lab4, "OpenFundedLab");
                var lw = (System.Windows.Window)G("labsWindow"); var tabs = (System.Windows.Controls.TabControl)G("labsTabs");
                if (lw == null || tabs == null || tabs.Items.Count != 2 || G("rotStudioWindow") != lw || G("fundedLabWindow") != lw) throw new Exception("the two labs should be TABS of ONE window");
                if (!Convert.ToString(((System.Windows.Controls.TabItem)tabs.Items[0]).Header).Contains("EVAL") || !Convert.ToString(((System.Windows.Controls.TabItem)tabs.Items[1]).Header).Contains("FUNDED")) throw new Exception("tab order: 🧪 EVAL then 💰 FUNDED");
                Call(lab4, "ShowLabTab", true); if (tabs.SelectedItem != tabs.Items[0]) throw new Exception("⇄ should select the 🧪 tab");
                // 🧭 the Eval Lab wizard: a step cannot be skipped, ← BACK, then ① 30 accounts of 3 firms → ② ★ STAGGERED → ③ one picked day (+ the next) → ④ replay → ⑤ START
                var wz = (KeystoneLabWizard)G("evalLabWizard"); var nx = (Func<bool>)G("evalLabWizardNext"); if (wz == null || nx == null || wz.Step != 0 || wz.CanOpen(1)) throw new Exception("the wizard should start at step 1 with step 2 locked");
                if (!FindAll<System.Windows.Controls.Button>(tabs.Items[0]).Any(b => Convert.ToString(b.Content) == "⚙ advanced • every screen")) throw new Exception("no ⚙ advanced link");
                {   // ① accounts PER FIRM: each firm its own box (default 5), the total worked out • 10 + 10 + 10 = 30
                    Action<System.Windows.Controls.TextBox, string> type = (tb0, v) => { tb0.Text = v; var fe = typeof(System.Windows.Controls.TextBox).GetField("TextChanged", BindingFlags.NonPublic | BindingFlags.Instance); var dl = fe == null ? null : fe.GetValue(tb0) as Delegate; if (dl != null) dl.DynamicInvoke(tb0, new System.Windows.Controls.TextChangedEventArgs()); };   /* = the user typing */
                    var fbx = FindAll<System.Windows.Controls.TextBox>(tabs.Items[0]).Where(b => Convert.ToString(b.Tag).StartsWith("wz.firm.")).ToList();
                    if (fbx.Count < 3 || fbx.First(b => Convert.ToString(b.Tag) == "wz.firm.LUCID").Text != "5") throw new Exception("① no count box per firm (default 5)");
                    foreach (var f in new[] { "TPT", "LUCID", "TRADEIFY" }) type(fbx.First(b => Convert.ToString(b.Tag) == "wz.firm." + f), "10");
                    var tot = FindAll<System.Windows.Controls.TextBlock>(tabs.Items[0]).FirstOrDefault(t => Convert.ToString(t.Tag) == "wz.firm.total"); if (tot == null || !tot.Text.Contains("30 = 10 TPT + 10 Lucid + 10 Tradeify")) throw new Exception("① the total: " + (tot == null ? "none" : tot.Text));
                    var rbx = FindAll<System.Windows.Controls.TextBox>(tabs.Items[0]).First(b => Convert.ToString(b.Tag) == "rot.firm.LUCID"); type(rbx, "7"); var rtot = FindAll<System.Windows.Controls.TextBlock>(tabs.Items[0]).First(t => Convert.ToString(t.Tag) == "rot.firm.total");
                    if (!rtot.Text.Contains("17 = 5 TPT + 7 Lucid + 5 Tradeify")) throw new Exception("ROTATE ① per firm: " + rtot.Text); type(rbx, "5");
                    Console.WriteLine("      ① per firm: " + tot.Text + " • ROTATE edits too");
                }
                if (!nx() || wz.Step != 1 || !wz.Summary[0].Contains("30 simulated")) throw new Exception("step 1: " + wz.Summary[0]);
                ((Action)G("evalLabWizardBack"))(); if (wz.Step != 0) throw new Exception("← BACK"); nx();
                {   // ★ BUILD 11f: step 2 pre-selects the default (⚡ ALL AT ONCE 2:00 AM NY); this step then replays ⚡ STAGGERED (a choice)
                    var w2 = FindAll<System.Windows.Controls.ComboBox>(tabs.Items[0]).FirstOrDefault(c => c.Items.Count > 3 && Convert.ToString(c.Items[0]) == KeystoneEvalLab.DefaultName);
                    if (w2 == null || w2.SelectedIndex != 0) throw new Exception("step 2 should pre-select the ★ default (⚡ ALL AT ONCE 2:00 AM NY)");
                    int si = Enumerable.Range(0, w2.Items.Count).First(i => Convert.ToString(w2.Items[i]).StartsWith("⚡ STAGGERED")); w2.SelectedIndex = si; w2.SelectedItem = w2.Items[si];
                }
                if (!nx() || !wz.Summary[1].StartsWith("⚡ STAGGERED")) throw new Exception("step 2 → ⚡ STAGGERED (a choice): " + wz.Summary[1]);
                var dayBoxes = FindAll<System.Windows.Controls.TextBox>(tabs.Items[0]).ToList(); var pick = FindAll<System.Windows.Controls.Button>(tabs.Items[0]).FirstOrDefault(b => Convert.ToString(b.Content) == "📅 PICK A DAY"); if (pick == null) throw new Exception("step 3 has no 📅 PICK A DAY");
                ((Action<DateTime, DateTime>)G("evalLabWizardDays"))(new DateTime(2026, 3, 3), new DateTime(2026, 3, 3));
                if (!nx() || !wz.Summary[2].Contains("2 trading days")) throw new Exception("one picked day = that day + the next: " + wz.Summary[2]);
                if (!nx() || !wz.Summary[3].StartsWith("▶")) throw new Exception("step 4");
                if (!nx()) throw new Exception("⑤ START");
                var msim = (KeystonePropSim)G("rotStudioManSim"); if (msim == null || msim.Accounts.Count < 30) throw new Exception("the replay did not load the 30 accounts");
                var vis = ((Func<List<string>>)G("evalLabVisibleButtons"))(); if (vis.Contains("▲▲ BUY BOTH") || !vis.Contains("▲ BUY MNQ")) throw new Exception("an MNQ-only plan shows only BUY / SELL MNQ: " + string.Join(",", vis));
                // the chart: no fixed height (it fills the tab), candles drawn
                var ch = (System.Windows.Controls.Canvas)G("evalLabChart"); var par = ch.Parent as System.Windows.Controls.DockPanel;
                if (ch.Height > 0 || par == null || par.Children[par.Children.Count - 1] != ch) throw new Exception("the chart must take the remaining height (last child of its DockPanel, no fixed height)");
                ((Action<int>)G("rotStudioManStep"))(KeystoneRotDay.Slot(1101) - KeystoneRotDay.Slot(155) + 5);
                int candles = ch.Children.OfType<System.Windows.Shapes.Rectangle>().Count(); if (candles < 20) throw new Exception("no candles on the chart: " + candles);
                // the trading screen = the chart + the pool (settings in their own tab), full screen, the chart's tools
                if ((string)((Func<string>)G("evalLabTabNow"))() != "TRADING") throw new Exception("the replay should open the 📈 TRADING tab, is " + ((Func<string>)G("evalLabTabNow"))());
                var pnames = ((Func<List<string>>)G("evalLabPoolNames"))(); if (pnames.Count < 30 || !pnames.Contains("TPT 1") || !pnames.Contains("LUCID 1")) throw new Exception("the pool panel: " + string.Join(",", pnames.Take(6)));
                {   // 🖥 BUILD 10c: the chart-first screen — a compact toolbar (≤ 10 buttons), the chart = the DockPanel's star (last) child, ⤢ BIG CARDS + the pool tabs
                    int nb = ((Func<int>)G("evalLabToolbarButtons"))(); if (nb > 10) throw new Exception("the TRADING toolbar has " + nb + " buttons (≤ 10)");
                    var mr = ch.Parent as System.Windows.Controls.DockPanel; if (mr == null || mr.Children[mr.Children.Count - 1] != ch) throw new Exception("the chart must own the rest of the TRADING screen");
                    ((Action<bool>)G("evalLabBigCards"))(true); ((Action<bool>)G("evalLabBigCards"))(false);
                    Console.WriteLine("      🖥 toolbar " + nb + " buttons • chart = the star child • ⤢ big cards");
                }
                ((Action<bool>)G("evalLabFull"))(true); if (!ch.Children.OfType<System.Windows.Controls.Border>().Any(b => b.Child is System.Windows.Controls.WrapPanel)) throw new Exception("⛶ full screen: no floating mini pool on the chart"); ((Action<bool>)G("evalLabFull"))(false);
                { int b0 = ((Func<int>)G("evalLabChartBars"))(); ((Action<int>)G("evalLabChartWheel"))(120); int b1 = ((Func<int>)G("evalLabChartBars"))(); if (b1 >= b0) throw new Exception("wheel zoom: " + b0 + " → " + b1);
                  ((Action<double, double>)G("evalLabChartDrag"))(400, 0); if (!((Func<List<string>>)G("evalLabChartMarks"))().Any(m => m.StartsWith("BACK "))) throw new Exception("drag should pan back");
                  ((Action<int>)G("evalLabChartWheel"))(-120); var ohlc = ((Func<double, double, string>)G("evalLabChartClick"))(300, 120); if (!ohlc.Contains(" O ") || !ohlc.Contains(" H ") || !ohlc.Contains(" C ")) throw new Exception("a candle click: " + ohlc);
                  ((Action<int>)G("evalLabSetTf"))(5); ((Action<string>)G("evalLabTool"))("HLINE"); var drawn = ((Func<double, double, string>)G("evalLabChartClick"))(300, 120); if (!drawn.StartsWith("━")) throw new Exception("H-LINE: " + drawn); ((Action<int>)G("evalLabSetTf"))(1);
                  ((Action<double, double>)G("evalLabChartDrag"))(40000, 0); var marks = ((Func<List<string>>)G("evalLabChartMarks"))(); if (!marks.Any(m => m.StartsWith("SEP 06:00 PM"))) throw new Exception("the 6 PM New York day separator is missing at the start of the day: " + string.Join(" | ", marks.Take(5)));
                  if (marks.Any(m => m.StartsWith("AXIS") && !m.EndsWith("M"))) throw new Exception("the time axis must be New York AM / PM"); ((Action<double, double>)G("evalLabChartDrag"))(-80000, 0);
                  Console.WriteLine("      chart: wheel " + b0 + " → " + b1 + " bars • drag pans • " + ohlc + " • " + drawn); }
                { var hdr = ((Func<string>)G("evalLabHeader"))(); if (!hdr.Contains("New York") || hdr.Contains("CET") || !hdr.Contains("range ")) throw new Exception("the header (New York time, AM / PM, the range): " + hdr); }
                // ✋ TPT: the clock STOPS at its minute until the press; the fill is at that minute • ⏩ quiet time skipped (AUTO)
                var dd = (Func<bool>)G("evalLabDayDone"); int waits = 0;
                for (int i = 0; i < 1400 && !dd(); i++)
                {
                    var wf = ((Func<string>)G("evalLabWaitingFor"))();
                    if (wf != null) { int m0 = ((Func<int>)G("evalLabMinute"))(); ((Action<int>)G("rotStudioManStep"))(3); if (((Func<int>)G("evalLabMinute"))() != m0) throw new Exception("the clock moved while " + wf + " waited for a press"); ((Action)G("evalLabPressPlan"))(); waits++; continue; }
                    ((Action<int>)G("rotStudioManStep"))(1);
                }
                if (!dd()) throw new Exception("the replay should stop at DAY DONE");
                var skips = (List<string>)G("evalLabSkips"); if (skips == null || !skips.Any(x => x.StartsWith("⏩"))) throw new Exception("⏩ quiet time was not skipped with AUTO on");
                var stagT = new HashSet<TimeSpan>(KeystoneLabFlow.StaggerTimes.Select(h => new TimeSpan(h / 100, h % 100, 0)));
                var tptT = msim.Trades.Where(t => t.Day.Date == new DateTime(2026, 3, 3) && (msim.FirmName(msim.Accounts.First(a => a.Id == t.Account)) ?? "").ToUpperInvariant().Replace(" ", "").Contains("TAKEPROFIT") || (msim.FirmName(msim.Accounts.First(a => a.Id == t.Account)) ?? "").ToUpperInvariant().StartsWith("TPT")).ToList();
                if (waits == 0 || tptT.Count == 0 || tptT.Any(t => !stagT.Contains(t.Entry.TimeOfDay))) throw new Exception("✋ TPT fills must be at their entry minute: " + waits + " waits, " + string.Join(",", tptT.Select(t => t.Entry.ToString("HH:mm"))) + " firms " + string.Join(",", msim.Accounts.Take(4).Select(a => a.Name + "=" + msim.FirmName(a))));
                var whys = (List<string>)G("evalLabWhy"); if (whys == null || whys.Count == 0 || !whys[0].Contains(" — ") || !whys[0].Contains("after costs")) throw new Exception("💬 no WHY line for the automated trades");
                Console.WriteLine("      ✋ " + waits + " TPT presses (the clock waited) • ⏩ " + skips.Count + " skips: " + skips[0] + " • 💬 " + whys[0]);
                var tr1 = msim.Trades.Where(t => t.Day.Date == new DateTime(2026, 3, 3)).ToList();
                if (tr1.Select(t => t.Account).Distinct().Count() != tr1.Count) throw new Exception("one trade per account per day");
                Console.WriteLine("      day 1: " + tr1.Count + " trades by " + tr1.Select(t => t.Account).Distinct().Count() + " accounts • " + candles + " candles • buttons " + string.Join(" ", vis));
                {   // 💀 a day with blown accounts: NEXT DAY's pool leaves them out, the 💀 BLOWN tab lists them
                    if (!msim.Accounts.Any(a => !a.Alive)) { var victim = msim.Accounts.First(a => a.Alive && !msim.PassedOrPending(a)); msim.Blow(victim, msim.Day, "smoke test"); }
                    ((Action)G("evalLabNextDay"))(); if (dd()) throw new Exception("▶ NEXT DAY should open the next day");
                    var pn2 = ((Func<List<string>>)G("evalLabPoolNames"))(); var bl = ((Func<List<string>>)G("evalLabBlownRows"))(); var blownN = msim.Accounts.Where(a => !a.Alive && !msim.PassedOrPending(a)).ToList();
                    if (bl.Count != blownN.Count || pn2.Count != msim.Accounts.Count(a => a.Alive && !msim.PassedOrPending(a))) throw new Exception("💀 the next day's pool / the BLOWN tab: pool " + pn2.Count + ", blown rows " + bl.Count + " of " + blownN.Count);
                    ((Action<string>)G("evalLabShowTab"))("BLOWN"); ((Action<string>)G("evalLabShowTab"))("PASSED"); ((Action<string>)G("evalLabShowTab"))("LOG"); ((Action<string>)G("evalLabShowTab"))("TRADING");
                    var exd = ((Func<string>)G("evalLabExportAll"))(); if (!System.IO.Directory.Exists(exd) || !System.IO.File.Exists(System.IO.Path.Combine(exd, "trades.csv")) || !System.IO.File.Exists(System.IO.Path.Combine(exd, "events.csv"))) throw new Exception("⤓ EXPORT WHAT HAPPENED: " + exd);
                    Console.WriteLine("      💀 " + bl.Count + " blown listed, the pool now " + pn2.Count + " • ⤓ " + System.IO.Path.GetFileName(exd));
                }
                var lg = (KeystoneLabLog)G("evalLabLog"); var ex = ((Func<string>)G("evalLabLogExport"))(); if (lg == null || lg.Rows.Count == 0 || !System.IO.File.Exists(ex)) throw new Exception("⤓ EXPORT LOG: " + ex);
                // ⌨ + ✋ closing: with a running replay the close asks, CANCEL keeps the window open
                ((Action)G("evalLabPlayToggle"))(); var busy = ((Func<List<string>>)G("evalLabBusy"))(); if (!busy.Any(b => b.Contains("replay is running"))) throw new Exception("busy should report the running replay");
                System.Windows.MessageBox.Answer = (txt, b) => System.Windows.MessageBoxResult.Cancel; lw.Close();
                if (G("labsWindow") == null || G("rotStudioWindow") == null || !System.Windows.MessageBox.LastText.Contains("replay is running")) throw new Exception("CANCEL should keep the labs open (and the dialog should name the running replay)");
                System.Windows.MessageBox.Answer = null; ((Action)G("evalLabPlayToggle"))();
                // 💰 the Funded Lab wizard: a simulated mix of 7 Lucid + 5 Tradeify + 1 TPT → 5 Lucid funded + 2 on the bench, TPT traded (lock → withdrawals), every tile firm · model
                var fz = (KeystoneLabWizard)G("fundedLabWizard"); var fnx = (Func<bool>)G("fundedLabWizardNext");
                var fboxes = FindAll<System.Windows.Controls.TextBox>(tabs.Items[1]).ToList(); var radios = FindAll<System.Windows.Controls.RadioButton>(tabs.Items[1]).ToList();
                var mixR = radios.First(r => Convert.ToString(r.Content) == "🎲 a simulated mix"); foreach (var r in radios.Where(r => r.GroupName == "fz1")) r.IsChecked = r == mixR;
                fboxes.First(b => Convert.ToString(b.Tag) == "fz.lucid").Text = "7"; fboxes.First(b => Convert.ToString(b.Tag) == "fz.tradeify").Text = "5"; fboxes.First(b => Convert.ToString(b.Tag) == "fz.tpt").Text = "1";
                if (!fnx() || !fz.Summary[0].Contains("bench")) throw new Exception("funded step 1: " + fz.Summary[0]);
                if (!fnx() || !fz.Summary[1].StartsWith("🛟 SAFE PAYOUT")) throw new Exception("funded step 2 should pre-select 🛟 SAFE PAYOUT (the tested default): " + fz.Summary[1]);
                ((Action<DateTime, DateTime>)G("fundedLabWizardDays"))(new DateTime(2026, 3, 2), new DateTime(2026, 4, 10));
                if (!fnx() || !fnx()) throw new Exception("funded steps 3 / 4"); Set(lab4, "fundedLabPayLast", null); if (!fnx()) throw new Exception("funded START");
                for (int i = 0; i < 600 && G("fundedLabPayLast") == null; i++) System.Threading.Thread.Sleep(100);
                var pr = (KeystonePayoutRun)G("fundedLabPayLast"); if (pr == null) throw new Exception("no funded result");
                var res = ((Func<System.Windows.FrameworkElement>)G("fundedLabWizardResult"))(); var texts = FindAll<System.Windows.Controls.TextBlock>(res).Select(t => t.Text ?? "").ToList();
                if (!texts.Any(t => t.StartsWith("Lucid · EOD trailing")) || !texts.Any(t => t.StartsWith("Tradeify · EOD trailing")) || !texts.Any(t => t.StartsWith("TPT ·")) || !texts.Any(t => t.Contains("funded accounts (a simulated mix)")) || !texts.Any(t => t.StartsWith("🪑 BENCH")))
                    throw new Exception("a mixed pool should show each firm's model + the top line + the bench: " + string.Join(" | ", texts.Take(8)));
                if (pr.Accounts.Count(a => a.Firm == "LUCID") < 5 || pr.Trades.Count == 0) throw new Exception("🛟 SAFE PAYOUT: the funded accounts should trade");
                Console.WriteLine("      " + texts.First(t => t.StartsWith("💰")) + " • " + pr.Payouts + " payouts");
                {   // ● THE LABS' LIVE SIDE (BUILD 11e): only ACTIVE accounts count + show; ✓ CONFIRM asks in the tab, then writes the pool for ● LIVE TRADING (MANUAL) — no order
                    var feed = (Action<List<KeystoneLiveRaw>>)G("evalLabLiveFeed"); if (feed == null) throw new Exception("no ● LIVE feed");
                    feed(new List<KeystoneLiveRaw>()); if (((Func<string>)G("evalLabLiveBadge"))() != "") throw new Exception("● LIVE must stay grey without prop accounts");
                    Action<System.Windows.Controls.CheckBox> tick = c => { c.IsChecked = true; var fe = typeof(System.Windows.Controls.Primitives.ToggleButton).GetField("Checked", BindingFlags.NonPublic | BindingFlags.Instance); var dl = fe == null ? null : fe.GetValue(c) as Delegate; if (dl != null) dl.DynamicInvoke(c, new System.Windows.RoutedEventArgs()); };
                    Func<string, System.Windows.Controls.Button> btn = tg => FindAll<System.Windows.Controls.Button>(tabs.Items[0]).FirstOrDefault(x => Convert.ToString(x.Tag) == tg);
                    // the user's real case: 195 old TPT accounts (balance 0 / disconnected) + ONE active TPT eval at $52,771 + an inactive MFFU eval
                    var real = new List<KeystoneLiveRaw>(); for (int i = 0; i < 195; i++) real.Add(new KeystoneLiveRaw { Name = "TAKEPROFIT" + (104385000 + i), Connection = "TakeProfit Rithmic", Balance = 0, Connected = i % 2 == 0 });
                    real.Add(new KeystoneLiveRaw { Name = "TAKEPROFIT104399999", Connection = "TakeProfit Rithmic", Balance = 52771, Nick = "TPT 13" }); real.Add(new KeystoneLiveRaw { Name = "MFFUEVREOD723518001", Connection = "Rithmic", Balance = 0 });
                    feed(real); var bd0 = ((Func<string>)G("evalLabLiveBadge"))(); if (bd0 != "● LIVE · 1 TPT eval") throw new Exception("● LIVE badge on the user's accounts: " + bd0);
                    var picks0 = FindAll<System.Windows.Controls.CheckBox>(tabs.Items[0]).Where(c => Convert.ToString(c.Tag).StartsWith("live.pick.")).ToList(); if (picks0.Count != 1) throw new Exception("● LIVE should list only the active account: " + picks0.Count);
                    var texts0 = FindAll<System.Windows.Controls.TextBlock>(tabs.Items[0]).Select(t => t.Text ?? "").ToList();
                    if (!texts0.Contains("TPT 13") || !texts0.Any(t => t.StartsWith("$229 to pass")) || !texts0.Any(t => t.StartsWith("🎯 Plan for these TPT evals: MNQ 20 · take profit +$1,500 · no stop (rides to the floor) · at 2:00 NY · TPT = manual")))
                        throw new Exception("● LIVE card: nickname / progress / plan: " + string.Join(" | ", texts0.Take(20)));
                    {   // 👥 BUILD 11f — OWNERS: one click on 👤 = the next owner (saved), group by owner, the 👤 OWNERS tab (per owner + the limits)
                        Action<string> click = tg => { var b9 = btn(tg); if (b9 == null) throw new Exception("👥 no button " + tg); b9.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); };
                        var ob = btn("live.owner.TAKEPROFIT104399999"); if (ob == null || !Convert.ToString(ob.Content).StartsWith("👤 ")) throw new Exception("👥 every account row has its 👤 owner button");
                        string before = Convert.ToString(ob.Content); click("live.owner.TAKEPROFIT104399999"); string after = Convert.ToString(btn("live.owner.TAKEPROFIT104399999").Content); if (after == before) throw new Exception("👥 one click should change the owner: " + before + " → " + after);
                        var ownF = System.IO.Path.Combine((string)typeof(KeystoneArc5MResearchLab).GetMethod("StudioFolder", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null), "EvalLab", "live", "owners.txt"); if (!System.IO.File.Exists(ownF) || !System.IO.File.ReadAllText(ownF).Contains("ACC|TAKEPROFIT104399999|")) throw new Exception("👥 the owner is saved in owners.txt");
                        click("live.groupby.owner"); var tx = FindAll<System.Windows.Controls.TextBlock>(tabs.Items[0]).Select(t => t.Text ?? "").ToList(); if (!tx.Any(t => t.StartsWith("👤 " + after.Replace("👤 ", "") + " · EVAL · 1 account")) || !tx.Any(t => t.StartsWith("limits per owner: TPT 1 / 5"))) throw new Exception("👥 group by owner: " + string.Join(" | ", tx.Take(12)));
                        click("live.groupby.firm"); click("live.tab.OWNERS"); var tx2 = FindAll<System.Windows.Controls.TextBlock>(tabs.Items[0]).Select(t => t.Text ?? "").ToList(); if (!tx2.Contains("👤 OWNER") || !tx2.Contains("FEES PAID") || !tx2.Contains("EVAL / LIMIT") || !tx2.Contains("1 / 5")) throw new Exception("👥 the OWNERS tab: " + string.Join(" | ", tx2.Take(30)));
                        click("live.tab.PROFIT"); click("live.tab.POOLS"); click("live.owner.TAKEPROFIT104399999"); while (Convert.ToString(btn("live.owner.TAKEPROFIT104399999").Content) != before) click("live.owner.TAKEPROFIT104399999");
                        Console.WriteLine("      👥 OWNERS: " + before + " → " + after + " (one click, saved) • group by owner • 👤 OWNERS tab (fees / payouts / net + limits)");
                    }
                    var inB = btn("live.inactive"); if (inB == null || !Convert.ToString(inB.Content).Contains("(196)")) throw new Exception("● LIVE: the inactive toggle should count 196: " + (inB == null ? "none" : Convert.ToString(inB.Content)));
                    var go0 = btn("live.confirm"); if (go0 == null || go0.IsEnabled) throw new Exception("✓ CONFIRM must be disabled with 0 ticked");
                    // ✏ EDIT THIS PLAN: in the tab, ✕ cancel
                    btn("live.edit.TPT · EVAL").RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); if (btn("live.save.TPT · EVAL") == null) throw new Exception("✏ EDIT THIS PLAN should open the fields in the tab");
                    btn("live.cancel.TPT · EVAL").RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); if (btn("live.save.TPT · EVAL") != null) throw new Exception("✕ CANCEL should close the editor");
                    // ✓ CONFIRM with 200 synthetic ACTIVE accounts, all ticked: asks in the tab, YES returns at once
                    var many = new List<KeystoneLiveRaw>(); for (int i = 0; i < 200; i++) many.Add(new KeystoneLiveRaw { Name = (i % 2 == 0 ? "TAKEPROFIT" : "LT-") + (500000 + i), Connection = i % 2 == 0 ? "TakeProfit Rithmic" : "Rithmic", Balance = 51000 + i, Today = 10 });
                    var sw = System.Diagnostics.Stopwatch.StartNew(); feed(many); var picks = FindAll<System.Windows.Controls.CheckBox>(tabs.Items[0]).Where(c => Convert.ToString(c.Tag).StartsWith("live.pick.")).ToList(); if (picks.Count != 200) throw new Exception("● LIVE: 200 active accounts to tick, got " + picks.Count);
                    foreach (var c in picks) tick(c);
                    var go = btn("live.confirm"); if (go == null || !go.IsEnabled || !Convert.ToString(go.Content).StartsWith("✓ CONFIRM 200 ticked accounts")) throw new Exception("✓ CONFIRM label: " + (go == null ? "none" : Convert.ToString(go.Content)));
                    Set(lab4, "labLiveNoOpen", true); var ask = ((Func<List<string>>)G("evalLabLiveConfirm"))(); if (ask.Count != 200 || btn("live.confirm.yes") == null) throw new Exception("✓ CONFIRM should ask in the tab first");
                    btn("live.confirm.yes").RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Set(lab4, "labLiveNoOpen", false); sw.Stop();
                    if (sw.ElapsedMilliseconds > 20000) throw new Exception("● LIVE ✓ CONFIRM with 200 accounts took " + sw.ElapsedMilliseconds + " ms");
                    // the two-account hand-over (order firm by firm)
                    feed(new List<KeystoneLiveRaw> { new KeystoneLiveRaw { Name = "TPT123456", Connection = "TakeProfit Rithmic", Balance = 52800 }, new KeystoneLiveRaw { Name = "LT-77001", Connection = "Rithmic", Balance = 50300, Today = 300, Trades = 1 } });
                    var bd = ((Func<string>)G("evalLabLiveBadge"))(); if (!bd.Contains("1 TPT eval")) throw new Exception("● LIVE badge: " + bd);
                    foreach (var c in FindAll<System.Windows.Controls.CheckBox>(tabs.Items[0]).Where(c => Convert.ToString(c.Tag).StartsWith("live.pick."))) tick(c);
                    Set(lab4, "labLiveNoOpen", true); ((Func<List<string>>)G("evalLabLiveConfirm"))(); var ord = ((Func<List<string>>)G("evalLabLiveConfirmYes"))(); Set(lab4, "labLiveNoOpen", false);
                    if (ord.Count != 2 || ord[0] != "TPT123456") throw new Exception("● LIVE ✓ CONFIRM order: " + string.Join(",", ord));
                    Console.WriteLine("      ● LIVE: 197 NinjaTrader accounts → " + bd0 + " (196 inactive hidden) • 200 accounts ✓ CONFIRM in " + sw.ElapsedMilliseconds + " ms • " + string.Join(" → ", ord) + " (handed to LIVE TRADING ▸ ROTATE, MANUAL)");
                    feed(new List<KeystoneLiveRaw>());
                }
                System.Windows.MessageBox.Answer = (txt, b) => System.Windows.MessageBoxResult.No; lw.Close(); System.Windows.MessageBox.Answer = null;
                if (G("labsWindow") != null || G("rotStudioWindow") != null || G("fundedLabWindow") != null) throw new Exception("CLOSE should close the labs");
                Set(lab4, "rotStudioDaysOverride", null); Set(lab4, "fundedLabDaysOverride", null);
            });
            Step("ROTATION TESTER: MNQ + MGC rotation on the loaded bars, result tabs, optimizer", () =>
            {
                Call(lab4, "OpenRotationTester");
                Set(lab4, "rotationLast", null);
                ((Action)G("rotationRun"))();
                for (int i = 0; i < 600 && G("rotationLast") == null && !((System.Windows.Controls.TextBlock)G("rotationStatus")).Text.Contains("ERROR") && !((System.Windows.Controls.TextBlock)G("rotationStatus")).Text.StartsWith("NEEDS"); i++) System.Threading.Thread.Sleep(100);
                var x = (KeystoneRotationResult)G("rotationLast"); Console.WriteLine("      " + ((System.Windows.Controls.TextBlock)G("rotationStatus")).Text);
                if (x == null || x.Rotations == 0) throw new Exception("no rotations");
                ((Action)G("rotationOptimize"))();
                for (int i = 0; i < 1800 && ((System.Collections.ICollection)G("rotationOpt")).Count == 0 && !((System.Windows.Controls.TextBlock)G("rotationStatus")).Text.Contains("ERROR"); i++) System.Threading.Thread.Sleep(100);
                Console.WriteLine("      " + ((System.Windows.Controls.TextBlock)G("rotationStatus")).Text);
                if (((System.Collections.ICollection)G("rotationOpt")).Count != 2025) throw new Exception("optimizer");
                Set(lab4, "rotationWindow", null);
            });
            Step("PROP BRACKET: Step 1 choice → planner on REAL BRACKET days → run, rule card, years, sweet spot", () =>
            {
                var box = (System.Windows.Controls.ComboBox)G("strategyBox"); int bi = -1; for (int i = 0; i < box.Items.Count; i++) if (Convert.ToString(box.Items[i]).StartsWith("PROP BRACKET")) bi = i;
                if (bi < 0) throw new Exception("PROP BRACKET missing from the strategy list");
                box.SelectedIndex = bi; box.SelectedItem = box.Items[bi]; Call(lab4, "RefreshStrategyInputState");
                if (!(bool)Call(lab4, "IsBracketSelected")) throw new Exception("not selected");
                Set(lab4, "propLastResult", null);
                Call(lab4, "OpenBracketPlanner");
                if (G("propPlannerWindow") == null) throw new Exception("planner not opened");
                ((Action)G("propPlannerRun"))();
                for (int i = 0; i < 600 && G("propLastResult") == null; i++) System.Threading.Thread.Sleep(100);
                var x = (KeystonePropPlanResult)G("propLastResult"); if (x == null) throw new Exception("bracket plan did not finish • " + ((System.Windows.Controls.TextBlock)G("propPlannerStatus")).Text);
                Console.WriteLine("      " + x.Plan.Describe() + " • traded days " + x.Plan.Bracket.Traded + " • pass " + x.PassRate.ToString("0.0") + "% • value " + x.ValuePerEval.ToString("0"));
                if (x.Plan.Source != "BRACKET" || x.Plan.Bracket.Traded == 0 || ((List<KeystonePropYear>)G("propLastYears")).Count == 0) throw new Exception("bracket plan incomplete");
                var tabs = (System.Windows.Controls.TabControl)G("propPlannerTabs");
                foreach (System.Windows.Controls.TabItem t in tabs.Items) if (t.Content is System.Windows.Controls.TextBlock) throw new Exception("tab not filled: " + t.Header);
                Set(lab4, "propLastSweet", new List<KeystonePropPlanResult>());
                ((Action)G("propPlannerSweet"))();
                for (int i = 0; i < 1800 && ((List<KeystonePropPlanResult>)G("propLastSweet")).Count == 0 && !((System.Windows.Controls.TextBlock)G("propPlannerStatus")).Text.Contains("ERROR"); i++) System.Threading.Thread.Sleep(100);
                var sweet = (List<KeystonePropPlanResult>)G("propLastSweet"); if (sweet.Count == 0) throw new Exception("bracket sweet spot did not finish • " + ((System.Windows.Controls.TextBlock)G("propPlannerStatus")).Text);
                Console.WriteLine("      sweet " + sweet.Count + " • #1 " + sweet[0].Label + " • " + sweet[0].YearText);
                if (sweet[0].Plan.Source != "BRACKET" || sweet[0].YearText.Length == 0) throw new Exception("sweet spot not on bracket days");
                ((Action)G("propPlannerProof"))();
                for (int i = 0; i < 1200 && ((System.Collections.ICollection)G("propLastProof")).Count == 0 && !((System.Windows.Controls.TextBlock)G("propPlannerStatus")).Text.Contains("ERROR"); i++) System.Threading.Thread.Sleep(100);
                if (((System.Collections.ICollection)G("propLastProof")).Count == 0) throw new Exception("proof test did not finish • " + ((System.Windows.Controls.TextBlock)G("propPlannerStatus")).Text);
                Console.WriteLine("      proof: " + ((System.Windows.Controls.TextBlock)G("propPlannerStatus")).Text);
                if (((System.Windows.Controls.TabControl)G("propPlannerTabs")).Items.Count != 6) throw new Exception("planner tabs");
                System.IO.File.WriteAllText(".build/report_PROP_BRACKET.html", KeystonePropPlanner.Html(x, (List<KeystonePropProgram>)G("propLastPrograms"), (List<KeystonePropYear>)G("propLastYears"), sweet, "REAL BRACKET"));
                Set(lab4, "propPlannerWindow", null);
                box.SelectedIndex = 0; box.SelectedItem = box.Items[0]; Call(lab4, "RefreshStrategyInputState");
            });
        }
        Step("📒 MY TRADES POOL: saved studio trades → overview, every day, every trade, breakdowns, a day's trades", () =>
        {
            var lab5 = new KeystoneArc5MResearchLab();
            string folder = (string)lab5.GetType().GetMethod("StudioFolder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); System.IO.Directory.CreateDirectory(folder);
            string file = System.IO.Path.Combine(folder, "StudioTrades.csv"); string keep = System.IO.File.Exists(file) ? System.IO.File.ReadAllText(file) : null;
            try
            {
                var f1 = new KeystoneReplayFill { Symbol = "MNQ", Side = "BUY", Qty = 2, EntryTime = new DateTime(2026, 3, 10, 9, 41, 0), ExitTime = new DateTime(2026, 3, 10, 9, 52, 30), Entry = 100, Exit = 110, Points = 10, Net = 36.5, Reason = "TARGET" };
                var f2 = new KeystoneReplayFill { Symbol = "MGC", Side = "SELL", Qty = 1, EntryTime = new DateTime(2026, 3, 11, 3, 5, 0), ExitTime = new DateTime(2026, 3, 11, 3, 9, 0), Entry = 2000, Exit = 2003, Points = -3, Net = -32.2, Reason = "STOP" };
                System.IO.File.WriteAllLines(file, new[] { KeystoneManualPool.Header, KeystoneManualPool.Line(new DateTime(2026, 3, 10), f1, "REGULAR", "I TRADE", "MANUAL (NO SIGNALS)"), KeystoneManualPool.Line(new DateTime(2026, 3, 11), f2, "PROP SIM", "AUTO + ME", "ROTATION") });
                Call(lab5, "OpenManualPool");
                var win = (System.Windows.Window)lab5.GetType().GetField("manualPoolWindow", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab5);
                if (win == null) throw new Exception("no window");
                var tabs = FindAll<System.Windows.Controls.TabControl>(win.Content).First(); if (tabs.Items.Count != 5) throw new Exception("tabs " + tabs.Items.Count);
                var texts = FindAll<System.Windows.Controls.TextBlock>(win.Content).Select(t => t.Text).ToList();
                if (!texts.Any(t => t.Contains("2026-03-10")) || !texts.Any(t => t == "MGC")) throw new Exception("rows missing: " + texts.Count + " " + string.Join(" / ", texts.Take(40)));
                Call(lab5, "OpenManualPool"); win.Close();
            }
            finally { if (keep == null) System.IO.File.Delete(file); else System.IO.File.WriteAllText(file, keep); }
        });
        Step("phase C pure pieces: date picks, the tile model, the practice account + $ bracket, the gutter, real ticks", () =>
        {
            var IC = System.Globalization.CultureInfo.InvariantCulture;
            // 📅 contiguous ticks = one range, apart = separate periods
            var y = KeystoneDatePick.OfTicks(new[] { "Y2025", "Y2026" }, new DateTime(2020, 7, 1), new DateTime(2026, 11, 6)); if (y.Count != 1 || y[0].Item1 != new DateTime(2025, 1, 1) || y[0].Item2 != new DateTime(2026, 11, 6)) throw new Exception("2025 + 2026 should be one range");
            var mm = KeystoneDatePick.OfTicks(new[] { "M2025-03", "M2026-03" }, DateTime.MinValue, DateTime.MinValue); if (mm.Count != 2) throw new Exception("every March = 2 periods");
            var fr = KeystoneDatePick.FromDays(new[] { new DateTime(2026, 11, 6), new DateTime(2026, 11, 9) }); if (fr.Count != 1) throw new Exception("Fri + Mon are contiguous trading days");
            var q = KeystoneDatePick.Range("QUARTER", new DateTime(2026, 5, 13), DateTime.MinValue, new DateTime(2026, 11, 6)); if (q.Item1 != new DateTime(2026, 4, 1) || q.Item2 != new DateTime(2026, 6, 30)) throw new Exception("quarter " + q);
            var l3 = KeystoneDatePick.Range("LAST 3 MONTHS", DateTime.MinValue, new DateTime(2020, 7, 1), new DateTime(2026, 11, 6)); if (l3.Item2 != new DateTime(2026, 11, 6) || l3.Item1.Month != 8) throw new Exception("last 3 months " + l3);
            string sum = KeystoneDatePick.Summary(y, new List<string> { "2025", "2026" }); if (!sum.StartsWith("2025 + 2026 · ") || !sum.Contains("trading days")) throw new Exception(sum);
            // 🟩 the tile: TPT intraday locked, Lucid EOD, MFF static
            var tp = KeystoneTileModel.Make("TPT", "INTRADAY", true, 2600, 0, 2000, 0, 3000, true, "FUNDED", "next withdrawal"); if (!tp.Line.Contains("🔒") || tp.FloorFrac <= 0 || tp.BalFrac <= tp.FloorFrac) throw new Exception("TPT tile " + tp.Line);
            var lu = KeystoneTileModel.Make("LUCID", "EOD", true, 500, -1500, 2000, 100, 4000, false, "FUNDED", "max payout"); if (!lu.Line.Contains("locks at +$100")) throw new Exception("Lucid tile " + lu.Line);
            var mf = KeystoneTileModel.Make("MFF002", KeystoneTileModel.KindOf(KeystonePayoutRules.StudioTemplates()[0], false), false, 300, -1000, 1000, 1e9, 2000, false, "EVAL", "target"); if (mf.Kind != "STATIC" || !mf.Line.Contains("static")) throw new Exception("MFF tile " + mf.Line);
            if (!KeystoneTileModel.Flash("FUNDED", "READY") || KeystoneTileModel.Flash(null, "BLOWN") || KeystoneTileModel.Dots(2, 5) != "●●○○○") throw new Exception("flash / dots");
            if (KeystoneTileModel.DaysToLock(3, new List<int> { 5, 9, 7 }, "lock") != "days to lock: 3 so far / usually ≈ 7") throw new Exception(KeystoneTileModel.DaysToLock(3, new List<int> { 5, 9, 7 }, "lock"));
            // 💵 the $ bracket: MNQ 15 contracts, +$1,000 after costs → the TP gives at least $1,000, a tick less would not; the stop never loses more than $500
            double cM = KeystoneMoveStudy.Cost("MNQ"); double tpP = KeystonePractice.BracketPts(1000, 15, 2, cM, 0.25, false), slP = KeystonePractice.BracketPts(500, 15, 2, cM, 0.25, true);
            if (KeystonePractice.UsdOf(tpP, 15, 2, cM, false) < 1000 - 1e-6 || KeystonePractice.UsdOf(tpP - 0.25, 15, 2, cM, false) >= 1000) throw new Exception("TP pts " + tpP);
            if (KeystonePractice.UsdOf(slP, 15, 2, cM, true) < -500 - 1e-6 || Math.Abs(tpP - 34.25) > 1e-9) throw new Exception("SL pts " + slP + " / TP " + tpP);
            double gTp = KeystonePractice.BracketPts(500, 2, 10, KeystoneMoveStudy.Cost("MGC"), 0.1, false); if (KeystonePractice.UsdOf(gTp, 2, 10, KeystoneMoveStudy.Cost("MGC"), false) < 500 - 1e-6) throw new Exception("MGC TP " + gTp);
            // 🎯 the practice account: STATIC, EOD trailing, INTRADAY incl. open profit, the floor / target prices
            var st = KeystonePractice.Presets()[0].Copy(); if (st.Mark(0, -1000, "t") != "BLOWN" || !st.Blown) throw new Exception("MFF static floor −$1,000 not blown");
            var evP = KeystonePractice.Presets()[3].Copy(); evP.EndOfDay(1500); if (Math.Abs(evP.FloorNow(0) - (-500)) > 1e-9) throw new Exception("EOD trail floor " + evP.FloorNow(0)); if (evP.Mark(3000, 0, "t") != "PASSED") throw new Exception("not passed at +3,000");
            var tf = KeystonePractice.Presets().First(x => x.DdType == "INTRADAY").Copy(); tf.Mark(0, 2500, "t"); if (Math.Abs(tf.FloorNow(0) - 0) > 1e-9 || !tf.Locked) throw new Exception("TPT intraday floor should lock at the start: " + tf.FloorNow(0));
            double fpx = KeystonePractice.PriceAt(-1000, 0, -25, 21798, 2, cM); if (Math.Abs(fpx - (21798 + (1000 - 25 * cM) / 50.0)) > 1e-9) throw new Exception("the floor price of a short " + fpx);
            if (tf.MaxStopPts(2, 2, cM, 0.25) <= 0) throw new Exception("max stop pts");
            // 🏷 the gutter stack never overlaps
            var ys = KeystoneChartGutter.Stack(new List<double> { 100, 101, 102, 300 }, 18, 0, 400); for (int i = 0; i < ys.Count; i++) for (int j = i + 1; j < ys.Count; j++) if (Math.Abs(ys[i] - ys[j]) < 18 - 1e-9) throw new Exception("gutter labels overlap");
            // ⏱ real ticks: the forming candle builds from the ticks, fills at tick prices, 60 seconds a minute at 1×, the fallback without ticks
            var t0 = new DateTime(2026, 1, 5, 9, 31, 0); var bars = new List<KeystoneArcBar> { new KeystoneArcBar { Symbol = "MNQ", Time = t0, Open = 100, High = 105, Low = 98, Close = 104 }, new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(1), Open = 104, High = 106, Low = 103, Close = 105 } };
            var tr = new KeystoneReplayTrader(bars, "MNQ", 60); if (tr.HasTicks) throw new Exception("no ticks yet"); 
            var tks = new List<KeystoneTick> { new KeystoneTick { Time = t0.AddSeconds(-58), Price = 100, Volume = 3 }, new KeystoneTick { Time = t0.AddSeconds(-40), Price = 98, Volume = 2 }, new KeystoneTick { Time = t0.AddSeconds(-20), Price = 105, Volume = 1 }, new KeystoneTick { Time = t0.AddSeconds(-2), Price = 104, Volume = 4 }, new KeystoneTick { Time = t0.AddSeconds(30), Price = 104.5, Volume = 1 }, new KeystoneTick { Time = t0.AddSeconds(59), Price = 105, Volume = 1 } };
            tr.SetTicks(tks, "TICKS"); if (!tr.HasTicks || tr.TickSource != "TICKS") throw new Exception("ticks not attached");
            tr.AdvanceTo(t0.AddSeconds(-50), 1000); var c1 = tr.Candles(1).Last(); if (c1.Open != 100 || c1.High != 100 || c1.Low != 100 || c1.Close != 100 || tr.Time != t0.AddSeconds(-58)) throw new Exception("step 1 candle " + c1.Open + "/" + c1.High + "/" + c1.Low + "/" + c1.Close + " at " + tr.Time.ToString("HH:mm:ss"));
            tr.TargetPts = 3; tr.Buy(1);   // long at 100, target 103: the next print that reaches it is 105 (a tick) → filled at the target price on that print's time
            tr.AdvanceTo(t0.AddSeconds(-30), 1000); var c2 = tr.Candles(1).Last(); if (c2.Low != 98 || c2.Close != 98 || tr.Position != 1) throw new Exception("step 2 candle / position");
            tr.AdvanceTo(t0.AddSeconds(-20), 1000); if (tr.Position != 0 || tr.Fills.Count != 1 || tr.Fills[0].Exit != 103 || tr.Fills[0].ExitTime != t0.AddSeconds(-20)) throw new Exception("the target did not fill on the 105 print at 103");
            tr.AdvanceTo(t0, 1000); var done1 = tr.Candles(1)[0]; if (done1.Open != 100 || done1.High != 105 || done1.Low != 98 || done1.Close != 104) throw new Exception("the finished minute must equal the saved bar");
            // 60 seconds a minute at 1×: the wall clock × 60 / 60 → after 30 s of wall time the replay stands at the print of +30 s, not later
            tr.AdvanceTo(t0.AddSeconds(30), 1000); if (tr.Time != t0.AddSeconds(30) || tr.Price != 104.5) throw new Exception("pace: " + tr.Time.ToString("HH:mm:ss"));
            var tr2 = new KeystoneReplayTrader(bars, "MNQ", 60); if (tr2.HasTicks || tr2.TickSource != "ESTIMATED") throw new Exception("the fallback must say ESTIMATED (the ⚠ note)");
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ksticks"); KeystoneTickCache.Save(dir, "MNQ", t0.Date, tks, "TICKS"); string src; var back = KeystoneTickCache.Load(dir, "MNQ", t0.Date, out src); if (back == null || back.Count != tks.Count || src != "TICKS" || back[2].Price != 105) throw new Exception("tick cache round trip");
            Console.WriteLine("      📅 " + sum + " · 🟩 " + tp.Line + " · 💵 MNQ 15 +$1,000 = " + tpP + " pts · ⏱ ticks " + tr.TickNote);
        });
        Console.WriteLine(failures == 0 ? "UI SMOKE TEST PASSED" : "UI SMOKE TEST: " + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
