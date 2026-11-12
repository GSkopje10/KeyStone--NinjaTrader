// ⚡ BUILD 2026-11-13a — the order-flow PERF HARNESS on the stand-in UI: ● LIVE TRADING with MNQ + MGC on a stand-in live feed,
// 300k trades of history per instrument and 30 trades / s live, the window's timers fired at their intervals in real time,
// BeginInvoke'd work pumped on this ("UI") thread. Measures the UI-thread time per second by part (KeystonePerf scopes) and
// asserts the 13a rule: nothing order-flow runs while nothing needs it (switch off, chip off) — counted work, not timing.
using System; using System.Collections.Generic; using System.Linq; using System.Reflection; using System.Diagnostics;
using NinjaTrader.NinjaScript; using NinjaTrader.NinjaScript.AddOns;
public static class PerfTest
{
    static object lab; static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;
    static object F(string n) { return lab.GetType().GetField(n, BF).GetValue(lab); }
    static void S(string n, object v) { lab.GetType().GetField(n, BF).SetValue(lab, v); }
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
    static DateTime Ny() { try { return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Eastern Standard Time"); } catch { return DateTime.Now; } }
    static readonly Dictionary<string, double> lastPx = new Dictionary<string, double>(); static readonly Random rnd = new Random(11);
    static double Tick(string sym) { return sym == "MGC" ? 0.1 : 0.25; }
    static KeystoneOfTick Next(string sym, DateTime t)
    {
        double tk = Tick(sym), px = lastPx[sym]; int mv = rnd.Next(5) - 2; px = Math.Round((px + mv * tk) / tk) * tk; lastPx[sym] = px;
        bool buy = mv > 0 || (mv == 0 && rnd.Next(2) == 0); long sz = rnd.NextDouble() < 0.02 ? 15 + rnd.Next(40) : 1 + rnd.Next(4);
        return new KeystoneOfTick { Time = t, Price = px, Size = sz, Side = buy ? 1 : -1, Quoted = true };
    }
    static readonly Dictionary<string, List<KeystoneOfTick>> hist = new Dictionary<string, List<KeystoneOfTick>>(); static readonly Dictionary<string, List<KeystoneArcBar>> bars = new Dictionary<string, List<KeystoneArcBar>>();
    static void MakeHistory(string sym, int n, int minutes)
    {
        lastPx[sym] = sym == "MGC" ? 2400 : 21000; var m1 = Ny(); var end = new DateTime(m1.Year, m1.Month, m1.Day, m1.Hour, m1.Minute, 0).AddSeconds(-1); var start = end.AddMinutes(-minutes);
        var l = new List<KeystoneOfTick>(n); for (int i = 0; i < n; i++) l.Add(Next(sym, start.AddMilliseconds((end - start).TotalMilliseconds * i / n)));
        hist[sym] = l; var b = new List<KeystoneArcBar>();
        foreach (var g in l.GroupBy(x => new DateTime(x.Time.Year, x.Time.Month, x.Time.Day, x.Time.Hour, x.Time.Minute, 0).AddMinutes(1)))
        { var gl = g.ToList(); b.Add(new KeystoneArcBar { Symbol = sym, Time = g.Key, Open = gl[0].Price, High = gl.Max(x => x.Price), Low = gl.Min(x => x.Price), Close = gl[gl.Count - 1].Price, Volume = gl.Sum(x => x.Size) }); }
        bars[sym] = b;
    }
    sealed class Result { public string Name; public double Secs, UiMsPerSec, OfUiMsPerSec, OfBgMsPerSec, MaxOfBlock, MaxHandler; public long OfWork; public List<KeystonePerf.Row> Rows; public string State; }
    static Result Run(string name, double secs, int perSec, List<System.Windows.Threading.DispatcherTimer> timers)
    {
        KeystonePerf.Reset(); var due = timers.ToDictionary(t => t, t => 0.0); var sw = Stopwatch.StartNew(); double uiTicks = 0, maxH = 0; double pushed = 0; double warm = Math.Min(2, secs / 4); bool warmed = false; double t0m = 0;
        while (sw.Elapsed.TotalSeconds < secs + warm)
        {
            double now = sw.Elapsed.TotalMilliseconds;
            if (!warmed && now >= warm * 1000) { warmed = true; KeystonePerf.Reset(); uiTicks = 0; maxH = 0; t0m = now; }   /* the first load / first build is the warm-up: measured from here (steady state) */
            int want = (int)(now / 1000.0 * perSec) - (int)pushed;
            if (want > 0) { var ny = Ny(); foreach (var sym in new[] { "MNQ", "MGC" }) { var l = new List<KeystoneOfTick>(); for (int k = 0; k < want; k++) l.Add(Next(sym, ny)); ((Func<string, List<KeystoneOfTick>, bool>)F("studioOfPush"))(sym, l); } pushed += want; }
            foreach (var t in timers)
            {
                if (!t.IsEnabled || now < due[t]) continue; due[t] = now + Math.Max(10, t.Interval.TotalMilliseconds);
                long a = Stopwatch.GetTimestamp(); try { t.Fire(); } catch (Exception ex) { Console.WriteLine("      timer error: " + ex.Message + " " + string.Join(" < ", (ex.StackTrace ?? "").Split((char)10).Take(4).Select(x9 => x9.Trim()))); }
                double d = KeystonePerf.Ms(Stopwatch.GetTimestamp() - a); uiTicks += d; if (d > maxH) maxH = d;
            }
            { long a = Stopwatch.GetTimestamp(); System.Windows.Threading.Dispatcher.Pump(); double d = KeystonePerf.Ms(Stopwatch.GetTimestamp() - a); uiTicks += d; if (d > maxH) maxH = d; }
            System.Threading.Thread.Sleep(5);
        }
        double el = (sw.Elapsed.TotalMilliseconds - t0m) / 1000.0; System.Threading.Thread.Sleep(50); System.Windows.Threading.Dispatcher.Pump();
        var rows = KeystonePerf.Rows(); Func<string, KeystonePerf.Row> row = n => rows.FirstOrDefault(x => x.Name == n);
        var ofU = row("OF TOTAL (UI thread)"); var ofB = row("OF TOTAL (background)");
        return new Result { Name = name, Secs = el, UiMsPerSec = uiTicks / el, OfUiMsPerSec = ofU == null ? 0 : KeystonePerf.Ms(ofU.Ticks) / el, OfBgMsPerSec = ofB == null ? 0 : KeystonePerf.Ms(ofB.Ticks) / el, MaxOfBlock = ofU == null ? 0 : KeystonePerf.Ms(ofU.MaxTicks), MaxHandler = maxH, OfWork = KeystonePerf.OfWork, Rows = rows, State = ((Func<string, string>)F("studioOfEngine"))("STATE") };
    }
    static void Print(Result r)
    {
        Console.WriteLine("  ── " + r.Name + " (" + r.Secs.ToString("0.0") + " s) — UI thread busy " + r.UiMsPerSec.ToString("0.0") + " ms/s (all timers) • ORDER FLOW on the UI thread " + r.OfUiMsPerSec.ToString("0.0") + " ms/s, longest " + r.MaxOfBlock.ToString("0.0") + " ms • background " + r.OfBgMsPerSec.ToString("0.0") + " ms/s • longest UI handler " + r.MaxHandler.ToString("0.0") + " ms • order-flow work units " + r.OfWork);
        Console.WriteLine("     state: " + r.State);
        foreach (var x in r.Rows.OrderByDescending(x => x.Ticks).Take(22)) Console.WriteLine("     " + x.Name.PadRight(28) + (KeystonePerf.Ms(x.Ticks) / r.Secs).ToString("0.00").PadLeft(8) + " ms/s  n=" + (x.Count / r.Secs).ToString("0.0").PadLeft(6) + "/s  max " + KeystonePerf.Ms(x.MaxTicks).ToString("0.0").PadLeft(6) + " ms");
    }
    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        Environment.SetEnvironmentVariable("HOME", System.IO.Path.GetTempPath() + "/keystone-perf-" + Process.GetCurrentProcess().Id);
        double secs = args.Length > 0 ? double.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 8; int nHist = args.Length > 1 ? int.Parse(args[1]) : 300000;
        System.Windows.Threading.Dispatcher.QueueMode = true; KeystonePerf.UiThread = System.Threading.Thread.CurrentThread.ManagedThreadId; KeystonePerf.On = true;
        MakeHistory("MNQ", nHist, 600); MakeHistory("MGC", nHist / 3, 600);
        lab = new KeystoneArc5MResearchLab(); int fails = 0;
        S("studioLiveFeedFake", (Func<string, List<KeystoneArcBar>>)(sym => bars.ContainsKey(sym) ? bars[sym].ToList() : new List<KeystoneArcBar>()));
        S("studioOfTicksFake", (Func<string, List<KeystoneOfTick>>)(sym => hist.ContainsKey(sym) ? hist[sym].ToList() : new List<KeystoneOfTick>()));
        int t0 = System.Windows.Threading.DispatcherTimer.All.Count;
        lab.GetType().GetMethod("OpenLiveTrading", BF).Invoke(lab, null); var w = (System.Windows.Window)F("liveStudioWindow");
        var go = FindAll<System.Windows.Controls.Button>(w.Content).FirstOrDefault(bt => Convert.ToString(bt.Content) == "● GO LIVE"); if (go != null) go.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        System.Windows.Threading.Dispatcher.Pump();
        List<System.Windows.Threading.DispatcherTimer> timers; lock (System.Windows.Threading.DispatcherTimer.All) timers = System.Windows.Threading.DispatcherTimer.All.Skip(t0).Where(t => t.HasHandlers).ToList();
        Console.WriteLine("⚡ PERF HARNESS • ● LIVE TRADING, MNQ " + nHist.ToString("N0") + " + MGC " + (nHist / 3).ToString("N0") + " trades of history, 30 trades / s live each, " + timers.Count + " timers (" + timers.Count(t => t.IsEnabled) + " running) • " + secs + " s per scenario");
        Func<string, string> eng = cm => ((Func<string, string>)F("studioOfEngine"))(cm);
        if (Environment.GetEnvironmentVariable("PERF_KEEP") != "1") { eng("KILL 0"); eng("CHIP 0"); eng("MASTER 0"); eng("CHARTOFF"); }   /* a saved workspace from an earlier run (the smoke test switches CHART chips on): back to the defaults */
        Console.WriteLine("  default: " + eng("STATE"));
        var a = Run("A. defaults (📊 ORDER FLOW off, 📊 OF chip off) — 📈 CHART", secs, 30, timers); Print(a);
        if (a.OfWork != 0 || !a.State.Contains("MNQ=off") || !a.State.Contains("MGC=off") || !a.State.Contains("master=0") || !a.State.Contains("dockTimer=0")) { Console.WriteLine("FAIL  engine idle rule broken in A"); fails++; }
        eng("MASTER 1"); var b = Run("B. 📊 ORDER FLOW on — the 📊 ORDERFLOW tab open", secs, 30, timers); Print(b);
        if (!b.State.Contains("view=1") || !b.State.Contains("MNQ=on")) { Console.WriteLine("FAIL  the switch did not open the tab / start the engine"); fails++; }
        // back to the chart: the tab closed (the switch stays on) → nothing needs the engine
        var exit = (Action)F("studioOfExitForTest"); if (exit != null) exit();
        var c = Run("C. 📊 ORDER FLOW on, back on the 📈 CHART (tab not visible)", secs, 30, timers); Print(c);
        if (c.OfWork != 0 || !c.State.Contains("MNQ=off") || !c.State.Contains("view=0")) { Console.WriteLine("FAIL  engine idle rule broken in C"); fails++; }
        eng("MASTER 0"); eng("CHIP 1"); var d = Run("D. 📈 CHART with the 📊 OF chip (🎯 REVERSAL SCORE + ⭐ OF CONFIRM)", secs, 30, timers); Print(d);
        if (!d.State.Contains("MNQ=on") || !d.State.Contains("MGC=off")) { Console.WriteLine("FAIL  the chip must run the engine for the chart on screen only"); fails++; }
        eng("CHIP 0"); eng("KILL 1"); eng("MASTER 1"); eng("CHIP 1"); var e = Run("E. ⚡ ORDER FLOW ENGINE OFF (hard kill) with the switch + chip asked", Math.Min(secs, 4), 30, timers); Print(e);
        if (e.OfWork != 0 || !e.State.Contains("MNQ=off")) { Console.WriteLine("FAIL  the hard kill must stop everything"); fails++; }
        Console.WriteLine("SUMMARY  B open tab: OF UI " + b.OfUiMsPerSec.ToString("0.0") + " ms/s (longest " + b.MaxOfBlock.ToString("0.0") + " ms) • D chart chip: OF UI " + d.OfUiMsPerSec.ToString("0.0") + " ms/s (longest " + d.MaxOfBlock.ToString("0.0") + " ms) • A / C / E idle: " + a.OfWork + " / " + c.OfWork + " / " + e.OfWork + " work units");
        bool strict = Environment.GetEnvironmentVariable("PERF_STRICT") == "1";
        foreach (var r in new[] { b, d }) if (r.OfUiMsPerSec > 25 || r.MaxOfBlock > 50) { Console.WriteLine((strict ? "FAIL" : "WARN") + "  " + r.Name.Substring(0, 2) + " over budget: " + r.OfUiMsPerSec.ToString("0.0") + " ms/s, longest " + r.MaxOfBlock.ToString("0.0") + " ms (budget 25 ms/s, 50 ms)"); if (strict) fails++; }
        eng("KILL 0"); eng("CHIP 0"); eng("MASTER 0"); try { w.Close(); } catch { }
        Console.WriteLine(fails == 0 ? "PERF HARNESS PASSED (engine idle when nothing needs it)" : "PERF HARNESS FAILED " + fails);
        return fails == 0 ? 0 : 1;
    }
}
