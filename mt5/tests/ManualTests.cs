using System; using System.Collections.Generic; using System.IO; using System.Linq; using KeystoneMT5;
// ✋ MANUAL tab: the trades of the account (any source) grouped per symbol from flat to flat — read only
public static class ManualTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static List<Mt5Bar> Path(DateTime start, double first, IEnumerable<double> closes)
    {
        var res = new List<Mt5Bar>(); double o = first; int i = 0;
        foreach (var c in closes) { res.Add(new Mt5Bar { Time = start.AddMinutes(i++), O = o, C = c, H = Math.Round(Math.Max(o, c) + 0.1, 2), L = Math.Round(Math.Min(o, c) - 0.1, 2), V = 100, Spread = 10 }); o = c; }
        return res;
    }
    public static int Main6()
    {
        var day0 = new DateTime(2026, 10, 20); var day1 = new DateTime(2026, 10, 21);
        var spec = new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 };
        var closes = new List<double>(); for (int i = 0; i < 10; i++) closes.Add(4000); for (int i = 1; i <= 10; i++) closes.Add(4000 - 0.3 * i); for (int i = 1; i <= 20; i++) closes.Add(3997 + 0.2 * i); for (int i = 0; i < 30; i++) closes.Add(4001 + 0.05 * i);
        var warm = Path(day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var test = Path(day1, 4000, closes);
        var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = day1, To = day1, Balance = 10000, CommissionPerLot = 7, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, spec, warm.Concat(test).ToList());
        var tr = new ManualTracker(); int samples = 0;
        sb.OnTickHook = () => { tr.Update(sb.State.Positions, sb.State.Deals, sb.State.ServerNow, false, "", 0); samples++; };
        sb.Playing = true; sb.Speed = 60;
        Action<int> run = n => { for (int k = 0; k < n; k++) sb.Advance(0.25); };   // 15 s of the market each
        Func<string, long> last = side => sb.State.Positions.Where(p => p.Side == side).OrderBy(p => p.Time).Select(p => p.Ticket).LastOrDefault();
        run(8); sb.Command("ORDER", "XAUUSD", "BUY", 0.08, 0.0, 0.0, 0.0);                 // the entry
        run(24); sb.Command("ORDER", "XAUUSD", "SELL", 0.16, 0.0, 0.0, 0.0);               // the hedge, 2.4 lower
        run(12); sb.Command("CLOSE", last("BUY"), 0.04);                                    // a partial close of the buy
        run(20); sb.Command("CLOSE", last("BUY"), 0.0); run(2); sb.Command("CLOSE", last("SELL"), 0.0);   // the rest, by hand → flat
        run(4); double px = sb.State.Quotes["XAUUSD"].Ask; sb.Command("ORDER", "XAUUSD", "BUY", 0.05, 0.0, 0.0, Math.Round(px + 0.6, 2));   // #2 with a TP at the broker
        run(60);
        var a = tr.Iters.FirstOrDefault(); var b = tr.Iters.Skip(1).FirstOrDefault();
        double dealsNet1 = sb.State.Deals.Where(d => d.IsTrade && a != null && a.Legs.Any(l => l.Deal == d.Ticket)).Sum(d => d.Profit + d.Commission + d.Swap);
        Check(tr.Iters.Count == 2 && a.Done && a.Opens == 2 && a.AddOns == 1 && a.Legs.Count(l => l.Kind == "CLOSE") == 3 && Math.Abs(a.Result - dealsNet1) < 1e-6 && Math.Abs(a.MaxGross - 0.24) < 1e-9 && a.Worst < -10 && a.Best >= a.Worst && a.Story.Any(x => x.Contains("partial")) && a.Story.Any(x => x.Contains("hedge")) && a.How.StartsWith("closed in parts"),
            "✋ manual: entry + hedge + a partial + two closes = ONE iteration from flat to flat (2 opens, 1 add-on, 3 closes, gross 0.24, result = the deals' net, worst < 0, the story says hedge / partial)", tr.Iters.Count + " iters " + (a == null ? "" : a.Opens + "/" + a.AddOns + "/" + a.Legs.Count + " res " + a.Result + " vs " + dealsNet1 + " worst " + a.Worst + " gross " + a.MaxGross + " how " + a.How));
        Check(b != null && b.Done && b.How == "TP hit at the broker" && b.Result > 0 && b.Legs.Any(l => l.Kind == "CLOSE" && l.Source == "broker TP"), "✋ manual: a TP hit at the broker ends iteration #2 (source = broker TP)", b == null ? "none" : b.How + " " + b.Result);
        var sm = tr.Sum(sb.State.ServerNow);
        Check(sm.N == 2 && sm.Won + sm.Lost == 2 && Math.Abs(sm.Net - tr.Iters.Sum(x => x.Result)) < 1e-9 && sm.BySymbol.StartsWith("XAUUSD"), "✋ manual: the summary (iterations, won / lost, net, by symbol)", sm.N + " " + sm.Net);
        // the HEDGE automation running on the symbol → marked "→ HEDGE #n", not counted (unless asked)
        var th = new ManualTracker(); th.Update(new List<Mt5Position>(), sb.State.Deals.Where(d => a.Legs.Any(l => l.Deal == d.Ticket)).ToList(), sb.State.ServerNow, true, "XAUUSD", 7);
        Check(th.Iters.Count == 1 && th.Iters[0].HedgeRef == "→ HEDGE #7" && th.Sum(sb.State.ServerNow).N == 0, "✋ manual: an iteration of the running 🛡 HEDGE automation is marked → HEDGE #7 and not counted twice");
        th.CountHedge = true; Check(th.Sum(sb.State.ServerNow).N == 1, "✋ manual: COUNT THE HEDGE'S TOO counts it");
        // saved + reloaded after a restart: the same iterations, nothing counted twice
        var pre = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spasix-manual-test-" + Guid.NewGuid().ToString("N"), "DEMO-123");
        tr.Save(pre); var t2 = new ManualTracker(); t2.Load(pre); t2.Update(sb.State.Positions, sb.State.Deals, sb.State.ServerNow, false, "", 0);
        Check(t2.Iters.Count == 2 && Math.Abs(t2.Iters.Sum(x => x.Result) - tr.Iters.Sum(x => x.Result)) < 0.01 && t2.Iters[0].Legs.Count == a.Legs.Count && t2.Iters[0].Story.Count == a.Story.Count, "✋ manual: saved per account + mode and reloaded after a restart — the same 2 iterations, nothing doubled", t2.Iters.Count + "");
        // rebuilt from the history alone (the app was closed during the trades)
        var t3 = new ManualTracker(); t3.Update(new List<Mt5Position>(), sb.State.Deals, sb.State.ServerNow, false, "", 0);
        Check(t3.Iters.Count == 2 && t3.Iters.All(x => x.Rebuilt) && Math.Abs(t3.Iters[0].Result - a.Result) < 1e-9, "✋ manual: rebuilt from the deals history alone (marked rebuilt: the drawdown from the closed legs only)");
        // at start-up the positions can arrive BEFORE the deals history: the history must still land in the right iterations (rebuilt, no doubles)
        sb.Command("ORDER", "XAUUSD", "SELL", 0.03, 0.0, 0.0, 0.0); run(2);
        var t4 = new ManualTracker(); t4.Update(sb.State.Positions, new List<Mt5Deal>(), sb.State.ServerNow, false, "", 0); int early = t4.Iters.Count;
        t4.Update(sb.State.Positions, sb.State.Deals, sb.State.ServerNow, false, "", 0); var c4 = t4.Iters.LastOrDefault();
        Check(early == 1 && t4.Iters.Count == 3 && t4.Iters.Count(x => x.Done) == 2 && c4 != null && !c4.Done && c4.Legs.Count == 1 && c4.Legs[0].Deal != 0, "✋ manual: positions first, the history a moment later → #1 / #2 finished from the history, #3 open with its real deal (no doubled legs)", early + " → " + t4.Iters.Count + " " + (c4 == null ? "" : c4.Legs.Count + " legs"));
        // sources
        Check(ManualTracker.SourceOf(new Mt5Deal { Magic = 770077, Reason = "EA" }) == "SpasiX" && ManualTracker.SourceOf(new Mt5Deal { Reason = "DESKTOP" }) == "MT5 by hand" && ManualTracker.SourceOf(new Mt5Deal { Magic = 42, Reason = "EA" }) == "EA #42" && ManualTracker.SourceOf(new Mt5Deal { Reason = "SL" }) == "broker SL",
            "✋ manual: the source of every leg (SpasiX magic 770077 / MT5 by hand / EA #magic / broker TP / SL)");
        // read only: the tracker has no way to send (no broker, no Send) — its only inputs are the positions and the deals lists
        Check(typeof(ManualTracker).GetMethods().All(m => !m.Name.Contains("Send") && m.GetParameters().All(p => !typeof(IBroker).IsAssignableFrom(p.ParameterType))) && samples > 100, "✋ manual: read only — no method takes a broker or sends anything");
        try { Directory.Delete(System.IO.Path.GetDirectoryName(pre), true); } catch { }
        return fails;
    }
}
