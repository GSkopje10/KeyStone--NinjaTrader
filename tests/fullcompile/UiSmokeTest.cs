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
        Step("strategy switch to every strategy (incl. HELIX and GOLDEN)", () => { var box = (System.Windows.Controls.ComboBox)lab.GetType().GetField("strategyBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab); foreach (int i in new[] { 1, 2, 3, 4, 5, 6, 7, 0 }) { box.SelectedIndex = i; box.SelectedItem = box.Items[i]; Call(lab, "RefreshStrategyInputState"); } });
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
        foreach (string strategy in new[] { "BH", "FVG", "ENG", "RLY", "VWP", "GLD", "ASIAN75" })
        {
            var lab2 = new KeystoneArc5MResearchLab();
            Call(lab2, "OpenWindow");
            var c = (KeystoneArcRunConfig)lab2.GetType().GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
            bool asian = strategy == "ASIAN75";
            c.StrategyCode = strategy; c.Scope = "BOTH"; c.SetupMinutes = asian ? 1 : 5; c.SessionMode = asian ? "ASIAN75" : "NY_OPEN"; c.CustomStart = 930; c.EndTime = 1555; c.EnableBh = strategy == "BH" ? 1 : 0; c.OutcomeModelEnabled = 1;
            c.Start = new DateTime(2025, 3, 3, asian ? 18 : 9, asian ? 0 : 30, 0); c.End = new DateTime(2025, 3, 7, 15, 55, 0); c.PoolSize = 5; c.EvaluationEnabled = 1; c.TargetDollars = 300; c.StopDollars = 200; c.Quantity = 2;
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
            Step(strategy + ": " + ev2.Count + " setups • open evidence chart + draw", () =>
            {
                Call(lab2, "OpenEvidenceChart"); Call(lab2, "RequestEvidenceBars"); Call(lab2, "RenderEvidenceChart");
                var canvas = (System.Windows.Controls.Canvas)lab2.GetType().GetField("evidenceCanvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
                int full = canvas.Children.Count;
                Call(lab2, "ReplaySetCursor", Call(lab2, "ReplayFirstBarTime"), true);
                int start = canvas.Children.Count;
                Console.WriteLine("      chart elements: all candles " + full + " • replay at start " + start);
                if (full < 60 || start >= full) throw new Exception("chart did not draw, or replay did not hide future candles");
                Call(lab2, "StopEvidenceReplay");
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
                if (asian) return;
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
        Console.WriteLine(failures == 0 ? "UI SMOKE TEST PASSED" : "UI SMOKE TEST: " + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
