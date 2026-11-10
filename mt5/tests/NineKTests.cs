// 09k: 🦅 RUNNER (experiment: simulated accounts only — its settings for $8,000 / $431, refused on every MT5 account, its 🏁 RACE lane in the
// compare set, sized from the live balance), the 🎞 TICK RECORDER (the exact binary format, appending, the server folders, LoadTicks' fallback,
// the bridge's quotes recorded) and 📦 EXPORT's 17-ticks (the iteration days, capped at 10)
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using KeystoneMT5;
public static class NineKTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 1e-9; }
    static readonly DateTime Day0 = new DateTime(2026, 10, 20), Day1 = new DateTime(2026, 10, 21);
    static List<Mt5Bar> Path0(DateTime start, double first, IEnumerable<double> closes)
    {
        var res = new List<Mt5Bar>(); double o = first; int i = 0;
        foreach (var c in closes) { res.Add(new Mt5Bar { Time = start.AddMinutes(i++), O = o, C = c, H = Math.Round(Math.Max(o, c) + 0.1, 2), L = Math.Round(Math.Min(o, c) - 0.1, 2), V = 100, Spread = 10 }); o = c; }
        return res;
    }
    static IEnumerable<double> Zz(double from, int n, int up, double stepUp, int dn, double stepDn) { double p = from; int k = 0; for (int i = 0; i < n; i++) { p += (k < up ? stepUp : -stepDn); k = (k + 1) % (up + dn); yield return Math.Round(p, 2); } }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    sealed class FakeBroker : IBroker
    {
        public readonly Mt5State St = new Mt5State(); public readonly List<string> Cmds = new List<string>();
        public Mt5State State { get { return St; } } public bool Send(string line) { return true; } public string Command(string cmd, params object[] a) { Cmds.Add(cmd); return "F" + Cmds.Count; }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
    }

    public static int Main13()
    {
        // ---------------------------------------------------------------- 1. RUNNER's settings
        var b0 = new AutoSettings { Symbol = "XAUUSD" };
        var r8 = AutoSettings.Model("RUNNER", b0, 8000); var r4 = AutoSettings.Model("RUNNER", b0, 431); var d8 = AutoSettings.Model("DEFENDER", b0, 8000);
        Check(r8.Preset == "RUNNER" && r8.IsRunner && Eq(r8.EntryLots, 0.05) && Eq(r8.NetAdvantage, 0.05) && Eq(r8.AddLots, 0.10) && Eq(r8.Target, 10) && Eq(r8.ScaleLots, 0.01) && Eq(r8.SizePer1k, 0.006) && Eq(r8.TargetPerLot, 200 * AutoSettings.RunnerTargetMult) && AutoSettings.RunnerTargetMult == 1,
            "🦅 RUNNER at $8,000: entry 0.05 (0.006 × 8 = 0.048 → nearest step), lead 0.05, add-ons 0.10, target $10 (0.05 × 200 × 1, 09l), scale 0.01, re-sized from the balance (0.006 per $1,000, $200 per lot)",
            r8.EntryLots + " " + r8.NetAdvantage + " " + r8.AddLots + " " + r8.Target + " " + r8.SizePer1k + " " + r8.TargetPerLot);
        Check(Eq(r4.EntryLots, 0.01) && Eq(r4.NetAdvantage, 0.01) && Eq(r4.AddLots, 0.02) && Eq(r4.Target, 2),
            "🦅 RUNNER at $431: the broker minimum 0.01, lead 0.01, add-ons 0.02, target $2", r4.EntryLots + " " + r4.AddLots + " " + r4.Target);
        Check(r8.CompressMode == "ALWAYS" && r8.RiskGuard && r8.GuardMode == "LOCK" && Eq(r8.GuardLots, 0.6) && Eq(r8.GuardMargin, 500) && r8.GuardChop == 3 && r8.PairOff && r8.HedgeSize == "ADVANTAGE"
            && r4.CompressMode == "ALWAYS" && r4.RiskGuard && r4.GuardMode == "LOCK",
            "🦅 RUNNER: 🧹 COMPRESS ALWAYS + the chop LOCK (RISK GUARD in LOCK mode: 0.6 lot, margin 500 %, 3 hedges, pair-off)", r8.CompressMode + " " + r8.RiskGuard + " " + r8.GuardMode);
        Check(r8.ExitGuard == d8.ExitGuard && r8.Rescue == d8.Rescue && r8.DangerExit == d8.DangerExit && r8.BeLock == d8.BeLock && r8.EntryMode == d8.EntryMode && r8.HedgeMode == d8.HedgeMode && r8.HedgeCap == d8.HedgeCap && r8.HardFloat == d8.HardFloat && !d8.RiskGuard && d8.CompressMode == "OFF" && Eq(d8.EntryLots, 0.08),
            "🦅 RUNNER: everything else as DEFENDER (exit guard, rescue, danger exit, breakeven lock, break entries / hedges, hard stops); DEFENDER itself unchanged (0.08, no guard, no compress)");
        {   // the smallest lot is 0.01: never below it, and the warning says how much bigger the risk is
            string w4 = AutoSettings.RunnerSizeWarning(431), w8 = AutoSettings.RunnerSizeWarning(8000), w833 = AutoSettings.RunnerSizeWarning(834), w800 = AutoSettings.RunnerSizeWarning(800);
            Check(w8 == "" && w833 == "" && w4.Contains("$431") && w4.Contains("0.01 lot") && w4.Contains("3.9× the planned risk") && w4.Contains("0.0026") && w800.Contains("2.1×") && r4.EntryLots >= 0.01,
                "🦅 RUNNER at $431: 0.01 (never under the smallest lot) and the warning \"" + w4 + "\"; none at $8,000 / $834", w4 + " | " + w800 + " | " + w833);
        }
        var rl = AutoSettings.Load(r8.Save());
        Check(rl.IsRunner && Eq(rl.SizePer1k, 0.006) && Eq(rl.TargetPerLot, 200) && rl.RiskGuard && rl.CompressMode == "ALWAYS" && !new AutoSettings().IsRunner, "🦅 RUNNER survives save / load; nothing is RUNNER by default");
        {   // the live sizing: nearest step for RUNNER, the old floor for 💰 RISK / SAFE LIVE
            var n = HedgeAuto.SizeFor(8000, 8000, false, null, 0.006, 600, true); var f = HedgeAuto.SizeFor(8000, 8000, false, null, 0.006, 600); var n4 = HedgeAuto.SizeFor(431, 431, false, null, 0.006, 600, true); var n12 = HedgeAuto.SizeFor(12345, 12345, false, null, 0.006, 600, true);
            Check(Eq(n.Item1, 0.05) && Eq(n.Item2, 30) && n.Item3 == "" && Eq(f.Item1, 0.04) && Eq(n4.Item1, 0.01) && n4.Item3.Contains("minimum") && Eq(n12.Item1, 0.07) && Eq(n12.Item2, 42),
                "🦅 SizeFor: $8,000 → 0.05 / $30 (the floor way stays 0.04 for the others), $431 → 0.01 + the minimum warning, $12,345 → 0.07 / $42", n.Item1 + " " + f.Item1 + " " + n4.Item1 + " " + n12.Item1);
        }
        // ---------------------------------------------------------------- 2. never on an MT5 account
        {
            var s = r8.Copy(); var real = new FakeBroker(); real.St.Account.Kind = "REAL"; var mtDemo = new FakeBroker(); mtDemo.St.Account.Kind = "DEMO"; var au = new HedgeAuto();
            var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 8000, Kind = "STANDARD", FixedSpread = 10 }, Gold(), Path0(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)).Concat(Path0(Day1, 4000, Enumerable.Repeat(4000.0, 30))).ToList());
            string cr = au.Check(real, s), cd = au.Check(mtDemo, s), cn = au.Check(null, s), cs = au.Check(sb, s); var other = d8.Copy();
            real.St.ServerNow = Day1.AddHours(12); real.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4000, Ask = 4000.2, Time = real.St.ServerNow }; au.Send = (c, a, l) => real.Command(c, a); au.Start(real, s, false); au.Step(real);
            Check(cr == AutoSettings.RunnerNo && cd == AutoSettings.RunnerNo && cn == AutoSettings.RunnerNo && cs == "" && au.Check(real, other) != AutoSettings.RunnerNo && AutoSettings.RunnerNo.Contains("EXPERIMENT") && AutoSettings.RunnerNo.Contains("MT5 account")
                && !AutoSettings.RunnerAllowed(false) && AutoSettings.RunnerAllowed(true) && !au.Running && au.StopReason.Contains("RUNNER") && real.Cmds.Count == 0,
                "🦅 never on an MT5 account: START refused on a REAL and on an MT5 demo / PROP link (\"" + cr.Substring(0, 40) + "…\"), allowed on the simulator; a run that slipped through stops at once, nothing sent", cr + " | " + cd + " | " + cs + " | " + au.StopReason);
        }
        // ---------------------------------------------------------------- 3. the 🏁 RACE lane RUN
        {
            var mine = d8.Copy(); var set = RaceLanes.For("RUN", mine, 5000); set.Swing = false; set.Lookback = 1;
            Check(RaceLanes.Compare.Contains("RUN") && RaceLanes.Compare[0] == "MINE" && RaceLanes.Compare[1] == "RUN" && RaceLanes.IsExperimental("RUN") && RaceLanes.Tip("RUN").Contains("simulated accounts only") && set.IsRunner && Eq(set.EntryLots, 0.03) && Eq(set.Target, 6) && set.RiskGuard && set.CompressMode == "ALWAYS" && set.Symbol == "XAUUSD" && !set.TrackManual,
                "🏁 RACE: the 🦅 RUNNER lane (RUN) is in ▶ COMPARE WITH MY SETTINGS next to MINE, experimental, sized from the race balance ($5,000 → 0.03 / $6)", string.Join(",", RaceLanes.Compare) + " " + set.EntryLots + " " + set.Target);
            var mk = new SimBroker(); mk.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, Kind = "STANDARD", FixedSpread = 10 }, Gold(), Path0(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)).Concat(Path0(Day1, 4000, Zz(4000, 400, 4, 0.5, 2, 0.3))).ToList());
            int sent = 0; var l = new RaceLane { Key = "RUN", Set = set, Br = new ShadowBroker(mk.State, line => { if (line.StartsWith("ORDER") || line.StartsWith("CLOSE")) sent++; return false; }, new SimAccountSettings { Balance = 5000, Leverage = 500, Kind = "STANDARD" }) };
            l.Eng.Send = (c, a, lab) => l.Br.Command(c, a); var e = l.Eng.Check(l.Br, set); if (e == "") l.Eng.Start(l.Br, set, false);
            mk.OnTickHook = () => l.Step(); mk.Playing = true; mk.Speed = 60; for (int k = 0; k < 4000 && !mk.Ended; k++) mk.Advance(0.25);
            int mkPos; lock (mk.State.Gate) mkPos = mk.State.Positions.Count + mk.State.Deals.Count(d => d.Type != "BALANCE"); var first = l.Eng.Trades.FirstOrDefault();
            Check(e == "" && sent == 0 && mkPos == 0 && first != null && Eq(first.Lots, 0.03),
                "🏁 the RUN lane trades its own simulated account: " + l.Eng.Trades.Count + " trades, the first " + (first == null ? "—" : first.Side + " " + first.Lots.ToString("0.##", IC)) + " (0.006 × $5,000, sized at the iteration start), 0 orders sent, the market's account untouched", e + " sent " + sent + " mk " + mkPos + " trades " + l.Eng.Trades.Count);
        }
        // ---------------------------------------------------------------- 4. 🎞 the tick recorder
        var tmp = Path.Combine(Path.GetTempPath(), "spasix-09k-" + Guid.NewGuid().ToString("N")); var oldRec = TickRecorder.RootOverride; var oldHist = HistoryStore.Root;
        try
        {
            TickRecorder.RootOverride = Path.Combine(tmp, "ticks"); HistoryStore.Root = Path.Combine(tmp, "history"); TickRecorder.ResetForTests();
            var t0 = Day1.AddHours(9).AddMilliseconds(123);
            TickRecorder.Record("Broker-Live 1", "XAUUSD", t0, 4001.25, 4001.45); TickRecorder.Record("Broker-Live 1", "XAUUSD", t0.AddSeconds(1.5), 4001.5, 4001.7);
            var f = TickRecorder.FileOf("Broker-Live 1", "XAUUSD", Day1); bool before = File.Exists(f); TickRecorder.Flush();
            byte[] raw = File.ReadAllBytes(f); int ms0 = BitConverter.ToInt32(raw, 0); double bid0 = BitConverter.ToDouble(raw, 4), ask0 = BitConverter.ToDouble(raw, 12); int ms1 = BitConverter.ToInt32(raw, 20);
            Check(!before && raw.Length == 40 && ms0 == 9 * 3600000 + 123 && bid0 == 4001.25 && ask0 == 4001.45 && ms1 == 9 * 3600000 + 1623 && f.Replace('\\', '/').EndsWith("ticks/Broker-Live 1/XAUUSD/20261021.ticks"),
                "🎞 recorder: memory first (no file before the flush), then ticks\\<server>\\<SYMBOL>\\yyyyMMdd.ticks = per tick int ms from the day's start + double bid + double ask (20 bytes)", raw.Length + " " + ms0 + " " + bid0 + " " + ask0 + " " + ms1 + " " + f);
            TickRecorder.Record("Broker-Live 1", "XAUUSD", t0.AddMinutes(1), 4002, 4002.2); TickRecorder.Record("Broker-Live 1", "XAUUSD", Day1.AddDays(1).AddHours(1), 4003, 0); TickRecorder.Flush();
            var back = TickRecorder.Load("XAUUSD", Day1); var nd = TickRecorder.Load("XAUUSD", Day1.AddDays(1));
            Check(new FileInfo(f).Length == 60 && back.Count == 3 && back[0].T == t0 && back[2].Bid == 4002 && back[2].Ask == 4002.2 && nd.Count == 1 && nd[0].Ask == 4003 && nd[0].T == Day1.AddDays(1).AddHours(1),
                "🎞 recorder: appends (3 ticks on the day), a new server day = a new file, a missing ask = the bid, read back exactly", back.Count + " " + nd.Count);
            // the same bytes HistoryStore's own reader gets (the format is THE format)
            Directory.CreateDirectory(HistoryStore.DirOf("XAUUSD")); File.Copy(f, HistoryStore.TickFileOf("XAUUSD", Day0)); var hs = HistoryStore.LoadTicks("XAUUSD", Day0); File.Delete(HistoryStore.TickFileOf("XAUUSD", Day0));
            Check(hs.Count == 3 && hs[0].T == Day0.AddHours(9).AddMilliseconds(123) && hs[1].Bid == 4001.5, "🎞 a recorded file read by HistoryStore.LoadTicks as a downloaded one = the same ticks (same format)", hs.Count.ToString());
            // LoadTicks' fallback: no downloaded ticks → the recorded ones; downloaded ones win; the preferred server first
            var fb = HistoryStore.LoadTicks("XAUUSD", Day1); bool hasAny = HistoryStore.HasAnyTicks("XAUUSD", Day1), hasDl = HistoryStore.HasTicks("XAUUSD", Day1);
            HistoryStore.SaveTicks("XAUUSD", Day1, new List<SimTick> { new SimTick { T = Day1.AddHours(2), Bid = 3990, Ask = 3990.3 } }); var dl = HistoryStore.LoadTicks("XAUUSD", Day1); File.Delete(HistoryStore.TickFileOf("XAUUSD", Day1));
            TickRecorder.Record("Other-Server", "XAUUSD", t0, 5000, 5000.5); TickRecorder.Flush();
            var pA = HistoryStore.LoadTicks("XAUUSD", Day1, "Other-Server"); var pB = HistoryStore.LoadTicks("XAUUSD", Day1, "Broker-Live 1"); var rng = HistoryStore.LoadTickRange("XAUUSD", Day1, Day1.AddDays(1), "Broker-Live 1");
            Check(fb.Count == 3 && hasAny && !hasDl && dl.Count == 1 && dl[0].Bid == 3990 && pA.Count == 1 && pA[0].Bid == 5000 && pB.Count == 3 && rng.Count == 4 && TickRecorder.Servers("XAUUSD", Day1, "Other-Server")[0] == "Other-Server",
                "🎞 HistoryStore.LoadTicks: no downloaded day → the recorded ticks (" + fb.Count + "); a downloaded day wins; two servers → the account's server first", fb.Count + " " + dl.Count + " " + pA.Count + " " + pB.Count + " " + rng.Count);
            // the bridge's quotes are recorded under the account's server (ACC first); OFF records nothing
            var st = new Mt5State(); st.Apply("TICK|EURUSD|1.1|1.1002|" + (long)(Day1.AddHours(3) - new DateTime(1970, 1, 1)).TotalMilliseconds);
            st.Apply("ACC|123|Me|Fake-Server|USD|1000|1000|0|1000|0|500|HEDGE|1|DEMO"); st.Apply("TICK|EURUSD|1.1|1.1002|" + (long)(Day1.AddHours(3).AddMilliseconds(250) - new DateTime(1970, 1, 1)).TotalMilliseconds);
            TickRecorder.On = false; st.Apply("TICK|EURUSD|1.2|1.2002|" + (long)(Day1.AddHours(4) - new DateTime(1970, 1, 1)).TotalMilliseconds); bool offSaved = File.ReadAllText(Path.Combine(TickRecorder.Root, "recorder.txt")) == "off";
            TickRecorder.ResetForTests(); bool offKept = !TickRecorder.On; TickRecorder.On = true; TickRecorder.Flush();
            var eu = TickRecorder.Load("EURUSD", Day1);
            Check(eu.Count == 1 && eu[0].T == Day1.AddHours(3).AddMilliseconds(250) && eu[0].Ask == 1.1002 && File.Exists(TickRecorder.FileOf("Fake-Server", "EURUSD", Day1)) && offSaved && offKept && TickRecorder.DaysRecorded() == 2,
                "🎞 the bridge: every TICK line is recorded under the account's server (none before ACC); 🎞 SAVE LIVE TICKS: OFF records nothing and is remembered; 2 days recorded", eu.Count + " " + offSaved + " " + offKept + " " + TickRecorder.DaysRecorded());
            {   // a failure to write is silent (a FILE where the server's folder should be) — nothing thrown, trading goes on
                File.WriteAllText(Path.Combine(TickRecorder.Root, "Blocked"), "x"); bool threw = false;
                try { TickRecorder.Record("Blocked", "XAUUSD", t0, 1, 2); TickRecorder.Flush(); } catch { threw = true; }
                Check(!threw && TickRecorder.Failed >= 1, "🎞 a write failure is silent: the ticks are dropped, nothing thrown", threw + " " + TickRecorder.Failed);
            }
            // ---------------------------------------------------------------- 5. 📦 EXPORT: 17-ticks
            for (int i = 0; i < 12; i++) TickRecorder.Record("Broker-Live 1", "XAUUSD", Day1.AddDays(2 + i).AddHours(10), 4100 + i, 4100.2 + i);
            TickRecorder.Flush(); HistoryStore.SaveTicks("XAUUSD", Day1.AddDays(13), new List<SimTick> { new SimTick { T = Day1.AddDays(13).AddHours(1), Bid = 1, Ask = 2 }, new SimTick { T = Day1.AddDays(13).AddHours(2), Bid = 3, Ask = 4 } });
            var itDays = Enumerable.Range(0, 14).Select(i => Day1.AddDays(i).AddHours(15)).Concat(new[] { Day1.AddHours(18) }).ToList();   // 14 iteration days (one twice)
            var outDir = Path.Combine(tmp, "export", "17-ticks"); var ex = TickRecorder.ExportDays("XAUUSD", itDays, outDir, 10, "Broker-Live 1");
            var names = ex.Select(System.IO.Path.GetFileName).ToList(); var firstDay = Day1.AddDays(4);
            Check(ex.Count == 10 && names.Contains("XAUUSD-20261103-download.ticks") && names.Contains("XAUUSD-20261102-Broker-Live 1.ticks") && !names.Any(n => n.Contains("20261021")) && names[0] == "XAUUSD-" + firstDay.ToString("yyyyMMdd", IC) + "-Broker-Live 1.ticks"
                && TickRecorder.ReadFile(ex.First(x => x.Contains("download")), Day1.AddDays(13)).Count == 2 && new FileInfo(ex[0]).Length == 20 && TickRecorder.ReadFile(ex[0], firstDay)[0].Bid == 4102 && TickRecorder.FormatText.Contains("20 bytes"),
                "📦 EXPORT 17-ticks: the tick files of the iteration days, the newest 10 only (" + names[0] + " … " + names[names.Count - 1] + "), a downloaded day as -download, the same binary format", string.Join(" ", names));
        }
        finally { TickRecorder.RootOverride = oldRec; HistoryStore.Root = oldHist; TickRecorder.ResetForTests(); try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine(fails == 0 ? "ALL 09k TESTS PASSED" : fails + " 09k TEST(S) FAILED");
        return fails;
    }
}
