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
        Step("strategy switch to ASIAN / FVG / BH", () => { var box = (System.Windows.Controls.ComboBox)lab.GetType().GetField("strategyBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab); foreach (int i in new[] { 1, 2, 0 }) { box.SelectedIndex = i; box.SelectedItem = box.Items[i]; Call(lab, "RefreshStrategyInputState"); } });
        // A pool result through the UI render paths.
        var rng = new Random(2); var ev = new List<KeystoneArcEvent>(); DateTime day = new DateTime(2025, 1, 6);
        for (int d = 0; d < 120; d++) { var s = day.AddDays(d); if (s.DayOfWeek == DayOfWeek.Saturday || s.DayOfWeek == DayOfWeek.Sunday) continue; for (int k = 0; k < 3; k++) { var t = s.AddHours(9).AddMinutes(35 + k * 40); bool w = rng.NextDouble() < 0.5; ev.Add(new KeystoneArcEvent { Id = "E" + d + k, Symbol = k % 2 == 0 ? "MNQ" : "MGC", SetupClass = "BH", TriggerTime = t, EntryTime = t, ReferenceTime = t, ExitTime = t.AddMinutes(20), Outcome = w ? "WIN" : "LOSS", GrossPnl = w ? 1500 : -500, Quantity = 10, Entry = 100, ReviewState = "ACCEPTED" }); } }
        var cfg = (KeystoneArcRunConfig)lab.GetType().GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
        cfg.StrategyCode = "BH"; cfg.Scope = "BOTH"; cfg.Start = day; cfg.End = day.AddDays(130); cfg.PoolSize = 10; cfg.EvaluationEnabled = 1; cfg.OutcomeModelEnabled = 1; cfg.SessionMode = "NY_OPEN";
        var accounts = KeystoneArcEngine.SimulatePool(ev, cfg);
        Set(lab, "events", ev); Set(lab, "loadedEvents", ev); Set(lab, "loadedScope", "BOTH"); Set(lab, "accounts", accounts);
        Step("render pool results (ledger, lifecycle, dashboard, banner)", () => { Call(lab, "RenderEvents"); Call(lab, "RenderPoolLedger"); Call(lab, "RenderLifecycle"); Call(lab, "RenderPoolDashboard"); Call(lab, "RenderPoolDayPanel"); Call(lab, "RefreshInstrumentViewControls"); });
        Step("render heavy tabs (periods, payout cycles, first return, findings)", () => { Call(lab, "RenderDailySessionScoreboard"); Call(lab, "RenderPayoutCycleDashboard"); Call(lab, "RenderPayoutAccountDashboard"); Call(lab, "RenderFirstReturnDashboard"); Call(lab, "RenderResearchFindings"); });
        Step("top boxes for every tab", () => { var tabs = (System.Windows.Controls.TabControl)lab.GetType().GetField("resultViewTabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab); for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; tabs.SelectedItem = tabs.Items[i]; Call(lab, "UpdateTopTilesForTab"); } });
        Step("report html", () => Call(lab, "BuildHtmlReport"));
        // Evidence chart + replay on real detected setups from random-walk bars.
        foreach (string strategy in new[] { "BH", "FVG", "ASIAN75" })
        {
            var lab2 = new KeystoneArc5MResearchLab();
            Call(lab2, "OpenWindow");
            var c = (KeystoneArcRunConfig)lab2.GetType().GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab2);
            bool asian = strategy == "ASIAN75";
            c.StrategyCode = strategy; c.Scope = "BOTH"; c.SetupMinutes = asian ? 1 : 5; c.SessionMode = asian ? "ASIAN75" : "NY_OPEN"; c.CustomStart = 930; c.EndTime = 1555; c.EnableBh = strategy == "BH" ? 1 : 0; c.OutcomeModelEnabled = 1;
            c.Start = new DateTime(2025, 3, 3, asian ? 18 : 9, asian ? 0 : 30, 0); c.End = new DateTime(2025, 3, 7, 15, 55, 0); c.PoolSize = 5; c.EvaluationEnabled = 1; c.TargetDollars = 300; c.StopDollars = 200; c.Quantity = 2;
            var r = new Random(9); var m1 = new List<KeystoneArcBar>(); var g1 = new List<KeystoneArcBar>(); double pm = 20000, pg = 2900;
            for (DateTime t = new DateTime(2025, 3, 2, 18, 1, 0); t < new DateTime(2025, 3, 8); t = t.AddMinutes(1))
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
        Console.WriteLine(failures == 0 ? "UI SMOKE TEST PASSED" : "UI SMOKE TEST: " + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
