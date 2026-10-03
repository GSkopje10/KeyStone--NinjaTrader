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
        try { a(); Console.WriteLine("PASS  " + name); }
        catch (Exception ex) { var e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex; Console.WriteLine("FAIL  " + name + "  → " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace.Split('\n').Take(6).Aggregate((x, y) => x + "\n" + y)); failures++; }
    }
    static object Call(object o, string m, params object[] a) { var mi = o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).First(x => x.Name == m && x.GetParameters().Length == a.Length); return mi.Invoke(o, a); }
    static void Set(object o, string f, object v) { o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v); }
    public static int Main()
    {
        Environment.SetEnvironmentVariable("HOME", System.IO.Path.GetTempPath());
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
                    string journal = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KeystoneArc5MResearch", "ReplayJournal.csv");
                    if (System.IO.File.Exists(journal)) System.IO.File.Delete(journal);
                    string accountFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(journal), "ReplayAccount.txt"), daysFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(journal), "ReplayDays.csv");
                    if (System.IO.File.Exists(accountFile)) System.IO.File.Delete(accountFile); if (System.IO.File.Exists(daysFile)) System.IO.File.Delete(daysFile);
                    string studio = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(journal), "Studio"); if (System.IO.Directory.Exists(studio)) System.IO.Directory.Delete(studio, true);
                    Call(lab2, "OpenLauncher");
                    Call(lab2, "OpenReplayTrader");
                    int stored = KeystoneStudioStore.Sessions(studio, "MNQ").Count;
                    Console.WriteLine("      studio store: MNQ " + stored + " sessions, MGC " + KeystoneStudioStore.Sessions(studio, "MGC").Count + " (copied from the lab's bars)");
                    if (stored == 0) throw new Exception("the lab's 1-minute bars were not copied into the studio store");
                    ((Action)F("replayTraderLoad"))();
                    var rt = (KeystoneReplayTrader)F("replayTrader");
                    if (rt != null && rt.Finished) throw new Exception("the loaded day starts finished");
                    if (rt == null || rt.Bars.Count == 0) throw new Exception("no day loaded");
                    ((Action<int>)F("replayTraderStep"))(5);
                    ((Action<int>)F("replayTraderOrder"))(1);
                    double entry = rt.AvgPrice; ((Action<int>)F("replayTraderStep"))(20);
                    ((Action)F("replayTraderFlat"))();
                    ((Action<int>)F("replayTraderOrder"))(-1); ((Action<int>)F("replayTraderStep"))(10); ((Action)F("replayTraderFlat"))();
                    var lines = System.IO.File.ReadAllLines(journal);
                    Console.WriteLine("      " + rt.Symbol + " " + rt.Bars.Count + " minutes • bought " + entry + " • " + rt.Fills.Count + " trades • today " + rt.Realized.ToString("0.00") + " • journal lines " + lines.Length);
                    if (rt.Fills.Count != 2 || lines.Length != 3 || rt.Fills[0].Side != "BUY" || rt.Fills[1].Side != "SELL") throw new Exception("replay trades / journal wrong");
                    ((Action)F("replayTraderEnd"))();
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
                    var win = (System.Windows.Window)F("replayTraderWindow"); win.Close();
                    if (!System.IO.File.Exists(System.IO.Path.Combine(studio, "Workspace.txt"))) throw new Exception("studio workspace not saved on close");
                    Console.WriteLine("      workspace saved: " + string.Join(" ", System.IO.File.ReadAllLines(System.IO.Path.Combine(studio, "Workspace.txt")).Take(4)));
                    Call(lab2, "OpenDataLibrary");
                    var dl = (Action<string, DateTime, DateTime, bool>)F("dataLibraryDownload");
                    dl("BOTH", new DateTime(2026, 9, 1), new DateTime(2026, 9, 20), false);
                    Console.WriteLine("      DATA LIBRARY opened, download queued (no NinjaTrader feed here: the contract lookup answers nothing)");
                });
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
        Console.WriteLine(failures == 0 ? "UI SMOKE TEST PASSED" : "UI SMOKE TEST: " + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
