// 09d: 🔒 SAFE LIVE preset, the REAL-account START warning, the experimental 🏁 RACE lanes (never MT5), 🔁 continuity (each DEMO bound to its
// login@server whatever the connection order, the history linked, the iteration number continues), #41 (a taken-over basket's earlier P/L and
// balance jumps are not trading results), ⏸ STOP ON MANUAL (+ RESUME on the same iteration; ADOPT keeps the old way)
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using KeystoneMT5;
public static class NineDTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static readonly DateTime Day0 = new DateTime(2026, 10, 20), Day1 = new DateTime(2026, 10, 21);
    static List<Mt5Bar> Path(DateTime start, double first, IEnumerable<double> closes)
    {
        var res = new List<Mt5Bar>(); double o = first; int i = 0;
        foreach (var c in closes) { res.Add(new Mt5Bar { Time = start.AddMinutes(i++), O = o, C = c, H = Math.Round(Math.Max(o, c) + 0.1, 2), L = Math.Round(Math.Min(o, c) - 0.1, 2), V = 100, Spread = 10 }); o = c; }
        return res;
    }
    static IEnumerable<double> Line(double from, double step, int n) { for (int i = 1; i <= n; i++) yield return Math.Round(from + step * i, 2); }
    static IEnumerable<double> Zig(double mid, double amp, int n) { for (int i = 0; i < n; i++) yield return Math.Round(mid + amp * Math.Sin(i / 6.0) + (i % 7 == 0 ? 1.5 : 0), 2); }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static AutoSettings S() { return new AutoSettings { HedgeSize = "FIXED", Rolling = false, Swing = false, FvgTf = 1, Symbol = "XAUUSD", Tf = 1, Lookback = 1, EntryLots = 0.1, AddLots = 0.2, Target = 20, FirstDir = "AUTO", NextStart = "BREAK" }; }
    static SimBroker Market(List<Mt5Bar> test, double bal = 10000)
    {
        var warm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30));
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = bal, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm.Concat(test).ToList()); if (err != "") throw new Exception(err);
        return sb;
    }
    sealed class FakeBroker : IBroker
    {
        public readonly Mt5State St = new Mt5State(); public readonly List<string> Cmds = new List<string>();
        public Mt5State State { get { return St; } } public bool Send(string line) { return true; } public string Command(string cmd, params object[] a) { Cmds.Add(cmd); return "F" + Cmds.Count; }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
    }

    public static int Main7()
    {
        // ---------------------------------------------------------------- 1. 🔒 SAFE LIVE
        var sl = AutoSettings.Model("SAFELIVE", new AutoSettings { Symbol = "XAUUSD" }, 431);
        Check(sl.Preset == "SAFELIVE" && Math.Abs(sl.SizePer1k - 0.01) < 1e-9 && Math.Abs(sl.TargetPerLot - 200) < 1e-9 && Math.Abs(sl.EntryLots - 0.01) < 1e-9 && Math.Abs(sl.AddLots - 0.01) < 1e-9 && Math.Abs(sl.Target - 2) < 1e-9
            && sl.HedgeSize == "SAME" && sl.CompressMode == "ALWAYS" && Math.Abs(sl.MaxDdPct - 10) < 1e-9 && sl.AfterDrawdown == "NEXT" && Math.Abs(sl.DayLossPct - 5) < 1e-9 && sl.BeLock && sl.BeClose && sl.DangerExit,
            "🔒 SAFE LIVE on $431: 0.01 lot (the minimum), +$2, SAME-size hedges, 🧹 compress ALWAYS, −10 % per iteration → next, −5 % a day, breakeven = CLOSE ALL, DANGER EXIT", sl.Save());
        var sl5 = AutoSettings.Model("SAFELIVE", new AutoSettings { Symbol = "XAUUSD" }, 5000);
        Check(Math.Abs(sl5.EntryLots - 0.05) < 1e-9 && Math.Abs(sl5.AddLots - 0.05) < 1e-9 && Math.Abs(sl5.Target - 10) < 1e-9, "🔒 SAFE LIVE on $5,000: 0.05 lot, every hedge 0.05, +$10", sl5.EntryLots + " " + sl5.AddLots + " " + sl5.Target);
        var df = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 10000);
        Check(df.HedgeSize == "ADVANTAGE" && df.CompressMode == "OFF" && df.MaxDdPct == 0 && df.DayLossPct == 0 && !df.BeClose && df.DangerExit && Math.Abs(df.EntryLots - 0.1) < 1e-9,
            "🛡 DEFENDER (still the default) unchanged: lead hedges, no compress, no % limits, breakeven LOCK");
        var ev = AutoSettings.SafeLiveEvidence;
        Check(ev.Contains("$431") && ev.Contains("3 %") && ev.Contains("5 %") && ev.Contains("$1,000") && ev.Contains("0 % and 0 %") && ev.Contains("did NOT grow") && ev.Contains("not part of that sweep"), "🔒 the evidence line quotes the sweep's numbers and says it did not grow the account", ev);
        var ed = sl.Copy(); ed.MaxDdPct = 7; ed.Target = 3; ed.HedgeSize = "ADVANTAGE"; ed.DayLossPct = 0; ed.SizePer1k = 0; var edl = AutoSettings.Load(ed.Save());
        Check(Math.Abs(edl.MaxDdPct - 7) < 1e-9 && Math.Abs(edl.Target - 3) < 1e-9 && edl.HedgeSize == "ADVANTAGE" && edl.DayLossPct == 0 && edl.SizePer1k == 0 && edl.Preset == "SAFELIVE" && edl.CompressMode == "ALWAYS" && AutoSettings.Diff(sl, ed).Count >= 4
            && AutoSettings.Load(sl.Save()).HedgeSize == "SAME", "🔒 edited after picking it: your values are saved and read back (nothing snaps back to the preset)", edl.Save());
        {   // SAME hedges in a real run: every leg = the entry lots
            var sb = Market(Path(Day1, 4000, Zig(4000, 4, 300)), 431); var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb);
            var s = AutoSettings.Model("SAFELIVE", S(), 431); s.DangerExit = false; s.Swing = false;   /* the synthetic path: the recent high / low breaks (swing breaks need real swings) */ var err = au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60; for (int k = 0; k < 3000 && !sb.Ended; k++) sb.Advance(0.25);
            var legs = au.Trades.Where(t => t.Side == "BUY" || t.Side == "SELL").ToList();
            Check(err == "" && legs.Count >= 3 && legs.All(t => Math.Abs(t.Lots - 0.01) < 1e-9) && au.Trades.Any(t => t.Why.Contains("ADD-ON")), "🔒 SAME-size hedges in a run: every leg (entry and hedges) is 0.01 lot", err + " " + string.Join(",", legs.Select(t => t.Side + t.Lots)));
        }
        // ---------------------------------------------------------------- 2. ⚠ START on a REAL account without limits
        var none = S(); var w1 = AutoSettings.LiveLimitsWarning("REAL", none);
        var onlyDd = S(); onlyDd.MaxDdPct = 10; var w2 = AutoSettings.LiveLimitsWarning("REAL", onlyDd);
        var dollar = S(); dollar.MaxDrawdown = 150; dollar.DayLoss = 200;
        Check(w1.Contains("NO BASKET STOP") && w1.Contains("NO DAILY LOSS") && w1.Contains("SAFE LIVE") && w2.Contains("NO DAILY LOSS") && !w2.Contains("NO BASKET STOP")
            && AutoSettings.LiveLimitsWarning("DEMO", none) == "" && AutoSettings.LiveLimitsWarning("CONTEST", none) == "" && AutoSettings.LiveLimitsWarning("REAL", sl) == "" && AutoSettings.LiveLimitsWarning("REAL", dollar) == "",
            "⚠ the START warning shows ONLY on a REAL account with no basket stop / no daily loss ($ or %)", w1 + " | " + w2);
        var ap = S(); ap.MaxDrawdown = 150; AutoSettings.ApplySafeLiveLimits(ap); var ap2 = S(); AutoSettings.ApplySafeLiveLimits(ap2);
        Check(ap.MaxDdPct == 0 && Math.Abs(ap.MaxDrawdown - 150) < 1e-9 && Math.Abs(ap.DayLossPct - 5) < 1e-9 && Math.Abs(ap2.MaxDdPct - 10) < 1e-9 && ap2.AfterDrawdown == "NEXT" && Math.Abs(ap2.DayLossPct - 5) < 1e-9 && ap2.EntryLots == S().EntryLots && ap2.HedgeSize == "FIXED" && AutoSettings.LiveLimitsWarning("REAL", ap2) == "",
            "⚠ APPLY SAFE LIVE LIMITS fills only the missing limits (−10 % / −5 %), nothing else changes");
        // ---------------------------------------------------------------- 3. 🏁 the experimental RACE lanes
        var mine = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 5000);
        var lSlv = RaceLanes.For("SLV", mine, 5000); var lFx = RaceLanes.For("FX10", mine, 5000); var lBox = RaceLanes.For("NFB", mine, 5000);
        Check(lSlv.Preset == "SAFELIVE" && lSlv.HedgeSize == "SAME" && lFx.HedgeMode == "PRICE" && Math.Abs(lFx.HedgePrice - 10) < 1e-9 && lFx.HedgeCap == 0 && lFx.BoxBars == 0 && lBox.BoxBars == 30 && lBox.HedgeSize == mine.HedgeSize && !lSlv.TrackManual && !lFx.TrackManual && !lBox.TrackManual
            && RaceLanes.Experimental.All(RaceLanes.IsExperimental) && !RaceLanes.IsExperimental("MINE") && RaceLanes.Experimental.All(k => RaceLanes.Tip(k).Contains("EXPERIMENTAL")) && mine.BoxBars == 0 && !mine.Save().Contains("box="),
            "🏁 lanes SLV / FX10 ($10 fixed hedge) / NFB (no flip inside a 30-candle box) built from MY SETTINGS, marked experimental — the box rule is never a saved live setting");
        {   // they trade a ShadowBroker only: nothing reaches the market's account, no ORDER line leaves
            var mk = Market(Path(Day1, 4000, Zig(4000, 8, 400))); int sent = 0; var lanes = new List<RaceLane>();
            foreach (var k in RaceLanes.Experimental)
            {
                var set = RaceLanes.For(k, mine, 5000); set.Swing = false; /* the synthetic path */ var l = new RaceLane { Key = k, Set = set, Br = new ShadowBroker(mk.State, line => { if (line.StartsWith("ORDER") || line.StartsWith("CLOSE")) sent++; return false; }, new SimAccountSettings { Balance = 5000, Leverage = 500, Kind = "STANDARD" }) };
                l.Eng.Send = (c, a, lab) => l.Br.Command(c, a); var e = l.Eng.Check(l.Br, set); if (e != "") l.Problem = e; else l.Eng.Start(l.Br, set, false); lanes.Add(l);
            }
            mk.OnTickHook = () => { foreach (var l in lanes) l.Step(); }; mk.Playing = true; mk.Speed = 60; for (int k = 0; k < 4000 && !mk.Ended; k++) mk.Advance(0.25);
            int mkPos, mkDeals; lock (mk.State.Gate) { mkPos = mk.State.Positions.Count; mkDeals = mk.State.Deals.Count(d => d.Type != "BALANCE"); }
            Check(sent == 0 && mkPos == 0 && mkDeals == 0 && lanes.All(l => l.Problem == "") && lanes.All(l => l.Eng.Trades.Count > 0) && lanes.All(l => l.Br.IsSim),
                "🏁 the 3 experimental lanes trade their own simulated accounts only: 0 orders sent, the market's account untouched", "sent " + sent + " pos " + mkPos + " deals " + mkDeals + " " + string.Join(" ", lanes.Select(l => l.Key + ":" + l.Eng.Trades.Count + l.Problem)));
            var fx = lanes.First(l => l.Key == "FX10").Eng; var fxAdd = fx.Trades.FirstOrDefault(t => t.Why.Contains("ADD-ON"));
            var fxEntry = fxAdd == null ? null : fx.Trades.LastOrDefault(t => t.Iter == fxAdd.Iter && t.Time < fxAdd.Time && t.Side != "CLOSE");
            Check(fxAdd == null || (fxEntry != null && Math.Abs(Math.Abs(fxAdd.Level - fxEntry.Price) - 10) < 0.6), "🏁 FX10: the hedge waits $10 of price from the last leg", fxAdd == null ? "" : fxAdd.Level + " vs " + (fxEntry == null ? "?" : fxEntry.Price.ToString(IC)));
        }
        // ---------------------------------------------------------------- 4. 🔁 continuity: an upgrade from 09c with the brokers in the other order
        string IC5 = "5298760@ICMarketsSC-MT5-2", DU = "512563@Duramarkets-Live", dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spx09d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "auto"));
        Func<double, int, List<string>> account = (bal, closes) =>
        {   // a simulated DEMO account like 09c saved it: closed trades (deals) + an open basket of 2
            var st = new Mt5State(); var ac = new SimAccount(st); ac.Specs["XAUUSD"] = Gold(); var t = Day1.AddHours(9); ac.Reset(new SimAccountSettings { Balance = bal, Leverage = 500, Kind = "STANDARD" }, t);
            for (int i = 0; i < closes; i++) { t = t.AddMinutes(10); ac.OnPrice("XAUUSD", t, 4000 + i, 4000.2 + i); ac.Do("ORDER", new object[] { "XAUUSD", "BUY", 0.1, 0.0, 0.0, 0.0 }, t); t = t.AddMinutes(5); ac.OnPrice("XAUUSD", t, 4002 + i, 4002.2 + i); ac.Do("CLOSEALL", new object[] { "XAUUSD", "ALL" }, t); }
            t = t.AddMinutes(10); ac.OnPrice("XAUUSD", t, 4100, 4100.2); ac.Do("ORDER", new object[] { "XAUUSD", "BUY", 0.08, 0.0, 0.0, 0.0 }, t); ac.Do("ORDER", new object[] { "XAUUSD", "SELL", 0.16, 0.0, 0.0, 0.0 }, t.AddSeconds(30));
            return ac.Save();
        };
        var icLines = account(7971.0, 39); File.WriteAllLines(Continuity.LegacyDemo(dir), icLines);
        {   // IC's 09c history: 39 iterations, each ending on its account's closing deal; today's results +$248.89
            var st = new Mt5State(); var ac = new SimAccount(st); ac.Load(icLines); var outs = st.Deals.Where(d => d.Entry == "OUT").OrderBy(d => d.Time).ToList(); var rows = new List<string> { "n,start,end,first,buys,sells,addons,buylots,selllots,maxgross,maxdd,result,reason,balstart,balend,peak,winneradds,context,fees" };
            for (int i = 0; i < outs.Count; i++) rows.Add(string.Join(",", (i + 1).ToString(IC), outs[i].Time.AddMinutes(-5).Ticks.ToString(IC), outs[i].Time.Ticks.ToString(IC), "BUY", "1", "0", "0", "0.1", "0", "0.1", "0", (i == 0 ? 248.89 - 38 * 1.0 : 1.0).ToString("R", IC), "TARGET", "7971", "7972", "0", "0", "", "0"));
            File.WriteAllLines(System.IO.Path.Combine(dir, "auto", "DEMO-" + Continuity.San(IC5) + "-iterations.csv"), rows);
            File.WriteAllText(System.IO.Path.Combine(dir, "auto", "DEMO-" + Continuity.San(DU) + "-open.csv"), "1,x," + string.Join(";", st.Positions.Select(p => p.Ticket)));   // the 09c take-over by the wrong tab
        }
        var duAcc = account(1000, 2); File.WriteAllLines(Continuity.DemoKeyFile(dir, DU), duAcc);   // Duramarkets' own DEMO
        int asked = 0; Func<List<string>, string> ask = l => { asked++; return null; };
        var keysDuFirst = new[] { DU, IC5 };
        var fDu1 = Continuity.DemoFile(dir, DU, keysDuFirst, ask); var fIc1 = Continuity.DemoFile(dir, IC5, keysDuFirst, ask);
        Func<string, Tuple<double, int, int>> load = f => { var st = new Mt5State(); var ac = new SimAccount(st); ac.Load(File.ReadAllLines(f)); return Tuple.Create(ac.Balance, st.Positions.Count, st.Deals.Count); };
        var icBefore = load(Continuity.LegacyDemo(dir) + ".migrated-09d"); var icA = load(fIc1); var duA = load(fDu1);
        Check(fDu1 == Continuity.DemoKeyFile(dir, DU) && fIc1 == Continuity.DemoKeyFile(dir, IC5) && asked == 0 && File.ReadAllText(Continuity.OwnerFile(dir)).Trim() == IC5 && !File.Exists(Continuity.LegacyDemo(dir))
            && Math.Abs(icA.Item1 - icBefore.Item1) < 1e-9 && icA.Item2 == 2 && icA.Item3 == icBefore.Item3 && icA.Item3 > 78 && Math.Abs(duA.Item1 - load(Continuity.DemoKeyFile(dir, DU)).Item1) < 1e-9 && duA.Item3 < 10
            && File.Exists(System.IO.Path.Combine(dir, "auto", "DEMO-" + Continuity.San(DU) + "-open.csv.moved-09d")),
            "🔁 upgrade, Duramarkets connects FIRST: the old demo-account.txt goes to ICMarkets (its 39 iterations end on its deals) — balance, 2 open positions and every deal intact; Duramarkets keeps its own; the wrong tab's open-basket file set aside", fDu1 + " | " + fIc1 + " asked " + asked + " " + icA + " " + duA);
        var keysIcFirst = new[] { IC5, DU }; var fIc2 = Continuity.DemoFile(dir, IC5, keysIcFirst, l => { asked++; return DU; }); var fDu2 = Continuity.DemoFile(dir, DU, keysIcFirst, l => { asked++; return IC5; });
        var icB = load(fIc2); var duB = load(fDu2);
        Check(fIc2 == fIc1 && fDu2 == fDu1 && asked == 0 && Math.Abs(icB.Item1 - icA.Item1) < 1e-9 && icB.Item2 == 2 && Math.Abs(duB.Item1 - duA.Item1) < 1e-9 && File.ReadAllLines(fIc2).Any(l => l == "KEY|" + IC5),
            "🔁 the next start with the brokers in the OTHER order: each DEMO opens its own account again (stamped with its login@server), nothing asked", fIc2 + " | " + fDu2);
        {   // can't decide → asks ONCE and remembers; "not sure" → nobody takes it, asked again next time
            string d2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spx09d-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d2); File.WriteAllLines(Continuity.LegacyDemo(d2), account(5000, 1));
            int n2 = 0; var fa = Continuity.DemoFile(d2, DU, keysDuFirst, l => { n2++; return null; }); bool stays = File.Exists(Continuity.LegacyDemo(d2));
            var fb = Continuity.DemoFile(d2, DU, keysDuFirst, l => { n2++; return IC5; }); var fc = Continuity.DemoFile(d2, IC5, keysDuFirst, l => { n2++; return DU; });
            Check(stays && n2 == 2 && fa == Continuity.DemoKeyFile(d2, DU) && !File.Exists(fa) && fc == Continuity.DemoKeyFile(d2, IC5) && File.Exists(fc) && load(fc).Item2 == 2 && File.ReadAllText(Continuity.OwnerFile(d2)).Trim() == IC5,
                "🔁 no evidence: it ASKS which broker owns the old DEMO (NOT SURE → nothing moves, asked again), then remembers the answer for good", n2 + " " + fa + " " + fc);
            try { Directory.Delete(d2, true); } catch { }
        }
        {   // the history saved under the login alone (an older key) is linked to login@server, once
            var legacy = System.IO.Path.Combine(dir, "auto", "LIVE-5298760-iterations.csv"); File.WriteAllText(legacy, "n,start\n1,2\n");
            int a1 = Continuity.LinkHistory(dir, "LIVE", IC5), a2 = Continuity.LinkHistory(dir, "LIVE", IC5); var nf = System.IO.Path.Combine(dir, "auto", "LIVE-" + Continuity.San(IC5) + "-iterations.csv");
            Check(a1 == 1 && a2 == 0 && File.Exists(nf) && File.ReadAllText(nf) == File.ReadAllText(legacy) && File.Exists(legacy) && Continuity.LinkHistory(dir, "DEMO", IC5) == 0,
                "🔁 a history saved under the login only is linked to its login@server (copied, the old file kept; never over an existing history)", a1 + " " + a2);
        }
        {   // the engine side of the upgrade: 39 iterations loaded → the open basket is #40 (not #1), the banner line, the settings
            var sb = Market(Path(Day1, 4100, Enumerable.Repeat(4100.0, 30))); sb.Playing = true; sb.Speed = 60; sb.Advance(0.25);
            sb.Command("ORDER", "XAUUSD", "BUY", 0.08, 0.0, 0.0, 0.0); sb.Advance(0.05); sb.Command("ORDER", "XAUUSD", "SELL", 0.16, 0.0, 0.0, 0.0); sb.Advance(0.05);
            var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); var now = sb.State.ServerNow;
            for (int i = 1; i <= 39; i++) au.Iterations.Add(new AutoIteration { N = i, Start = now.AddHours(-40 + i).AddMinutes(-5), End = now.AddHours(-40 + i), Result = i >= 30 ? 248.89 / 10 : 5, Reason = "TARGET", BalanceStart = 7971, BalanceEnd = 7972 });
            foreach (var x in au.Iterations) if (x.N >= 30) x.End = now.Date.AddHours(1 + x.N - 30).AddMinutes(1); else x.End = now.Date.AddDays(-1).AddHours(x.N % 20);
            var s09c = AutoSettings.Load("sym=XAUUSD;tf=1;entry=0.08;add=0.16;target=16;hsize=ADVANTAGE;nadv=0.08;cmp=ALWAYS;preset=DEFENDER;dexit=1;man=1");
            Check(s09c.OnManual == "STOP" && Math.Abs(s09c.Target - 16) < 1e-9 && s09c.CompressMode == "ALWAYS" && s09c.Preset == "DEFENDER" && Math.Abs(s09c.EntryLots - 0.08) < 1e-9, "🔁 the 09c settings come back as they were (+ the new default ⏸ STOP ON MANUAL)", s09c.Save());
            au.Check(sb, s09c); au.Start(sb, s09c, true); var line = Continuity.ResumeLine(au.Iterations, au.Current, now);
            Check(au.Current != null && au.Current.N == 40 && au.Running && line.Contains("39 iterations") && line.Contains("today +$248.89") && line.Contains("open basket #40 taken over"),
                "🔁 39 iterations in the history → the open basket is taken over as #40 (not #1); the banner: \"" + line + "\"", (au.Current == null ? "none" : "#" + au.Current.N) + " " + line);
            var tk = sb.State.Positions.Select(p => p.Ticket).ToList();
            var au2 = new HedgeAuto(); au2.Send = au.Send; au2.Iterations.AddRange(au.Iterations.Where(x => x.Done)); au2.RestoreOpen = () => new HedgeAuto.OpenSave { It = new AutoIteration { N = 77, Start = now.AddHours(-3), BalanceStart = 1 }, Tickets = new HashSet<long>(tk), SavedAt = now.AddMinutes(-1) };
            au2.Start(sb, s09c, true); var au3 = new HedgeAuto(); au3.Send = au.Send; au3.Iterations.AddRange(au.Iterations.Where(x => x.Done)); au3.RestoreOpen = () => new HedgeAuto.OpenSave { It = new AutoIteration { N = 77, Start = now.AddHours(-3), BalanceStart = 1 }, Tickets = new HashSet<long> { 990001, 990002 }, SavedAt = now.AddDays(-1) };
            au3.Start(sb, s09c, true);
            Check(au2.Current.N == 77 && au3.Current.N == 40 && au3.Current.BalanceStart > 1000, "🔁 the saved open basket continues only when ITS tickets are still open (another account's basket, all newer than the save, is not \"the same\" — #41's mix-up)", au2.Current.N + " / " + au3.Current.N + " " + au3.Current.BalanceStart);
        }
        try { Directory.Delete(dir, true); } catch { }
        // ---------------------------------------------------------------- 5. #41: results from the iteration's own deals
        {   // a basket already +$200 when it was taken over: the target closes it at once, but those $200 are not this iteration's
            var sb = Market(Path(Day1, 4000, Line(4000, 1, 20).Concat(Enumerable.Repeat(4020.0, 20)))); sb.Playing = true; sb.Speed = 60; sb.Advance(0.25);
            sb.Command("ORDER", "XAUUSD", "BUY", 0.1, 0.0, 0.0, 0.0); for (int k = 0; k < 90; k++) sb.Advance(0.25);
            var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); var s = S(); s.Target = 16; s.HedgeMode = "NONE"; s.MaxIterations = 1;
            double open; lock (sb.State.Gate) open = sb.State.Positions.Sum(p => p.Profit); au.Start(sb, s, true); for (int k = 0; k < 40; k++) sb.Advance(0.25); var it = au.Iterations.FirstOrDefault();
            Check(open > 150 && it != null && it.Done && it.Reason.StartsWith("TARGET") && Math.Abs(it.Result) < 16 && it.Reason.Contains("taken over"), "#41: a basket taken over at +$" + open.ToString("0", IC) + " that hits the $16 target at once counts only what it made after the take-over (not +$" + open.ToString("0", IC) + ")", it == null ? "none" : it.Reason + " " + it.Result);
        }
        {   // a balance jump (a deposit / SET BALANCE) during an iteration is not trading P/L
            var sb = Market(Path(Day1, 4000, Enumerable.Repeat(4000.0, 4).Concat(Line(4000, 0.5, 40)))); var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb);
            var s = S(); s.FirstDir = "BUY"; s.HedgeMode = "NONE"; s.Target = 20; s.MaxIterations = 1; au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
            bool dep = false; for (int k = 0; k < 2000 && !sb.Ended; k++) { sb.Advance(0.25); if (!dep && au.Current != null && sb.State.Positions.Count > 0) { lock (sb.State.Gate) sb.Account.SetBalance(sb.Account.Balance + 1000, sb.Account.Now); dep = true; } }
            var it = au.Iterations.FirstOrDefault();
            Check(dep && it != null && it.Done && it.Reason == "TARGET" && it.Result >= 19 && it.Result < 30, "🧮 a +$1,000 balance deal during the iteration: the result is its own deals (+$20), not the balance difference (+$1,020)", it == null ? "none" : it.Reason + " " + it.Result);
        }
        {   // the one-time repair of saved results
            var deals = new List<Mt5Deal>(); var t = Day1.AddHours(10);
            deals.Add(new Mt5Deal { Ticket = 1, Position = 11, Time = t.AddHours(-30), Symbol = "XAUUSD", Type = "BUY", Entry = "IN", Lots = 0.1, Commission = -0.35 });
            deals.Add(new Mt5Deal { Ticket = 2, Position = 11, Time = t.AddSeconds(4), Symbol = "XAUUSD", Type = "SELL", Entry = "OUT", Lots = 0.1, Profit = 204.37 });   // taken over: opened 30 h before #41
            deals.Add(new Mt5Deal { Ticket = 3, Position = 12, Time = t.AddHours(1), Symbol = "XAUUSD", Type = "BUY", Entry = "IN", Lots = 0.1, Commission = -0.7 });
            deals.Add(new Mt5Deal { Ticket = 4, Position = 12, Time = t.AddHours(1).AddMinutes(9), Symbol = "XAUUSD", Type = "SELL", Entry = "OUT", Lots = 0.1, Profit = 17, Commission = -0.7 });
            deals.Add(new Mt5Deal { Ticket = 5, Time = t.AddHours(1).AddMinutes(5), Type = "BALANCE", Profit = 2000 });
            var its = new List<AutoIteration> {
                new AutoIteration { N = 41, Start = t, End = t.AddSeconds(4), Result = 204.37, Reason = "TARGET" },
                new AutoIteration { N = 42, Start = t.AddHours(1), End = t.AddHours(1).AddMinutes(10), Result = 2015.6, Reason = "TARGET" },   // a balance jump inside
                new AutoIteration { N = 43, Start = t.AddHours(2), End = t.AddHours(2).AddMinutes(1), Result = 50, Reason = "TARGET" } };
            var ch = Continuity.RepairResults(its, deals, "XAUUSD"); var again = Continuity.RepairResults(its, deals, "XAUUSD");
            Check(Math.Abs(its[0].Result) < 0.01 && its[0].Reason.Contains("taken-over") && Math.Abs(its[1].Result - 15.6) < 0.01 && its[2].Reason.Contains("unverified") && Math.Abs(its[2].Result - 50) < 1e-9 && ch.Count == 3 && again.Count == 0,
                "🧮 the one-time repair: #41 +$204.37 → $0.00 (a leg taken over 30 h earlier), #42 +$2,015.60 → +$15.60 (the $2,000 balance deal), #43 flagged unverified (no deal); run twice = no change", string.Join(" | ", ch));
        }
        // ---------------------------------------------------------------- 6. ⏸ STOP ON MANUAL
        {   // in SpasiX: a SELL by hand during an iteration → stopped at once, nothing sent after, positions untouched; ▶ RESUME → the same #1
            var sb = Market(Path(Day1, 4000, Enumerable.Repeat(4000.0, 60))); var au = new HedgeAuto(); int sends = 0, sendsAtStop = -1; string open = null;
            au.Send = (c, a, l) => { sends++; return sb.Command(c, a); }; au.OnOpenState = l => { if (l != "") open = l; }; au.RestoreOpen = () => open == null ? null : HedgeAuto.ParseOpen(open);
            au.OnHandStop += w => sendsAtStop = sends; sb.OnTickHook = () => au.Step(sb);
            var s = S(); s.OnManual = "STOP"; s.FirstDir = "BUY"; s.Target = 500; s.HedgeMode = "NONE"; au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
            for (int k = 0; k < 20; k++) sb.Advance(0.25); int before = sb.State.Positions.Count; sb.Command("ORDER", "XAUUSD", "SELL", 0.05, 0.0, 0.0, 0.0); for (int k = 0; k < 40; k++) sb.Advance(0.25);
            Check(before == 1 && au.HandStopped && !au.Running && sb.State.Positions.Count == 2 && sends == sendsAtStop && au.HandWhat.Contains("SELL 0.05") && au.Story.Any(x => x.Item2.Contains("AUTOMATION STOPPED")) && au.StopReason.Contains("by hand"),
                "⏸ a SELL by hand during the iteration: the automation STOPS at once (no question), nothing sent after, the 2 positions untouched, the warning says what you did", before + " " + au.HandStopped + " " + au.Running + " " + sb.State.Positions.Count + " sends " + sends + "/" + sendsAtStop + " " + au.HandWhat);
            au.Start(sb, s, true); for (int k = 0; k < 4; k++) sb.Advance(0.25);
            Check(au.Running && !au.HandStopped && au.Current != null && au.Current.N == 1 && au.Iterations.Count == 1, "⏸ ▶ RESUME AUTOMATION: running again on the SAME iteration #1 (the basket taken over)", au.Running + " #" + (au.Current == null ? 0 : au.Current.N) + " " + au.Iterations.Count);
            // ✕ a ticket closed by hand → stops too
            var tk = sb.State.Positions.First().Ticket; sb.Command("CLOSE", tk, 0.0); for (int k = 0; k < 20; k++) sb.Advance(0.25);
            Check(au.HandStopped && !au.Running && au.HandWhat.Contains("closed outside"), "⏸ a ticket closed by hand → stopped as well", au.HandWhat);
        }
        {   // the old behaviour, now an explicit option: ADOPT
            var sb = Market(Path(Day1, 4000, Enumerable.Repeat(4000.0, 60))); var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb);
            var s = S(); s.OnManual = "ADOPT"; s.FirstDir = "BUY"; s.Target = 500; s.HedgeMode = "NONE"; au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
            for (int k = 0; k < 20; k++) sb.Advance(0.25); sb.Command("ORDER", "XAUUSD", "SELL", 0.05, 0.0, 0.0, 0.0); for (int k = 0; k < 40; k++) sb.Advance(0.25);
            Check(!au.HandStopped && au.Running && au.Current != null && au.Current.ManualAdds == 1, "⏸ ADOPT MANUAL TRADES (the option): the SELL by hand joins the basket and the automation goes on (the old way)", au.HandStopped + " " + au.Running);
        }
        {   // MT5: a deal with reason DESKTOP / an SL changed in MT5 → stopped; the bridge's own deals (magic 770077) and broker TP never stop it
            Func<string, Tuple<FakeBroker, HedgeAuto>> mk = mode =>
            {
                var fb = new FakeBroker(); var t = Day1.AddHours(12); fb.St.ServerNow = t; fb.St.Account.Kind = "REAL"; fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4100, Ask = 4100.2, Time = t };
                fb.St.Positions.Add(new Mt5Position { Ticket = 7, Symbol = "XAUUSD", Side = "BUY", Lots = 0.05, Open = 4100, Time = t.AddMinutes(-3), Magic = 770077 });
                var au = new HedgeAuto(); au.Send = (c, a, l) => fb.Command(c, a); var s = S(); s.OnManual = mode; s.HedgeMode = "NONE"; s.Target = 500; au.Start(fb, s, true); return Tuple.Create(fb, au);
            };
            var a = mk("STOP"); var tt = a.Item1.St.ServerNow;
            a.Item1.St.Deals.Add(new Mt5Deal { Ticket = 900, Position = 8, Time = tt.AddSeconds(1), Symbol = "XAUUSD", Type = "SELL", Entry = "IN", Lots = 0.02, Price = 4100.1, Magic = 770077, Reason = "EXPERT" });
            a.Item1.St.Deals.Add(new Mt5Deal { Ticket = 901, Position = 3, Time = tt.AddSeconds(1), Symbol = "EURUSD", Type = "SELL", Entry = "IN", Lots = 1, Price = 1.1, Magic = 0, Reason = "DESKTOP" });
            a.Item1.St.ServerNow = tt.AddSeconds(2); int c0 = a.Item1.Cmds.Count; a.Item2.Step(a.Item1); bool ownOk = a.Item2.Running;
            a.Item1.St.Deals.Add(new Mt5Deal { Ticket = 902, Position = 9, Time = tt.AddSeconds(3), Symbol = "XAUUSD", Type = "SELL", Entry = "IN", Lots = 0.05, Price = 4101.97, Magic = 0, Reason = "DESKTOP" });
            a.Item1.St.ServerNow = tt.AddSeconds(4); a.Item2.Step(a.Item1);
            Check(ownOk && !a.Item2.Running && a.Item2.HandStopped && a.Item2.HandWhat.StartsWith("SELL 0.05 @ 4101.97 in MT5 (desktop)") && a.Item1.Cmds.Count == c0,
                "⏸ MT5: a SELL with reason DESKTOP on the symbol → stopped, nothing sent (its own EXPERT deals and another symbol's trade did not stop it)", ownOk + " " + a.Item2.HandWhat + " cmds " + a.Item1.Cmds.Count);
            var b = mk("STOP"); b.Item1.St.Positions[0].Sl = 4090; b.Item1.St.ServerNow = b.Item1.St.ServerNow.AddSeconds(2); b.Item2.Step(b.Item1);
            var m = mk("STOP"); m.Item1.St.Deals.Add(new Mt5Deal { Ticket = 903, Position = 7, Time = m.Item1.St.ServerNow.AddSeconds(1), Symbol = "XAUUSD", Type = "BUY", Entry = "IN", Lots = 0.05, Price = 4101, Magic = 0, Reason = "MOBILE" }); m.Item1.St.ServerNow = m.Item1.St.ServerNow.AddSeconds(2); m.Item2.Step(m.Item1);
            var ad = mk("ADOPT"); ad.Item1.St.Deals.Add(new Mt5Deal { Ticket = 904, Position = 10, Time = ad.Item1.St.ServerNow.AddSeconds(1), Symbol = "XAUUSD", Type = "SELL", Entry = "IN", Lots = 0.05, Price = 4101.97, Magic = 0, Reason = "DESKTOP" }); ad.Item1.St.ServerNow = ad.Item1.St.ServerNow.AddSeconds(2); ad.Item2.Step(ad.Item1);
            var mm = mk("STOP"); mm.Item2.SetManualManage(true); mm.Item1.St.Deals.Add(new Mt5Deal { Ticket = 905, Position = 10, Time = mm.Item1.St.ServerNow.AddSeconds(1), Symbol = "XAUUSD", Type = "SELL", Entry = "IN", Lots = 0.05, Price = 4101.97, Magic = 0, Reason = "DESKTOP" }); mm.Item1.St.ServerNow = mm.Item1.St.ServerNow.AddSeconds(2); mm.Item2.Step(mm.Item1);
            Check(b.Item2.HandStopped && b.Item2.HandWhat.Contains("SL 4090") && m.Item2.HandStopped && m.Item2.HandWhat.Contains("MT5 mobile") && !ad.Item2.HandStopped && ad.Item2.Running && !mm.Item2.HandStopped && mm.Item2.Running,
                "⏸ MT5: an SL moved / a mobile trade → stopped; ADOPT and ✋ MANUAL MANAGE (suggestions) are not stopped", b.Item2.HandWhat + " | " + m.Item2.HandWhat + " | " + ad.Item2.Running + " " + mm.Item2.Running);
        }
        Console.WriteLine(fails == 0 ? "ALL 09d TESTS PASSED" : fails + " 09d TEST(S) FAILED");
        return fails;
    }
}
