// 13r: ⬇ DOWNLOAD REAL TICKS (TickDownloader: newest first, saved days skipped, an empty run = the broker's limit, cancel / resume, no order)
// and the .sxt codec (TickCodec: exact bid / ask / time back, ≥ 5 × smaller than the 20-byte records).
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using KeystoneMT5;
public static class NineRTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    sealed class MemStore : ITickStore
    {
        public readonly Dictionary<string, List<SimTick>> Saved = new Dictionary<string, List<SimTick>>(); public readonly HashSet<string> Empty = new HashSet<string>(); public readonly Dictionary<string, DateTime> Lim = new Dictionary<string, DateTime>();
        static string K(string s, DateTime d) { return s + "|" + d.ToString("yyyyMMdd"); }
        public bool Has(string s, DateTime d) { return Saved.ContainsKey(K(s, d)); }
        public bool IsEmpty(string s, DateTime d) { return Empty.Contains(K(s, d)); }
        public void Save(string s, DateTime d, List<SimTick> t) { Saved[K(s, d)] = t; }
        public void MarkEmpty(string s, DateTime d) { Empty.Add(K(s, d)); }
        public DateTime Limit(string s) { DateTime d; return Lim.TryGetValue(s, out d) ? d : DateTime.MinValue; }
        public void SetLimit(string s, DateTime d) { Lim[s] = d; }
    }
    // a fake MT5: real ticks only from `firstDay` on (the broker's limit); answers the request on the next Take
    sealed class FakeSrc : ITickSource
    {
        public DateTime FirstDay; public readonly List<string> Asked = new List<string>(); readonly HashSet<string> pending = new HashSet<string>(); public bool Silent;
        public bool Ask(string s, DateTime d) { Asked.Add(s + " " + d.ToString("yyyy-MM-dd")); pending.Add(s + d.Ticks); return true; }
        public Tuple<List<SimTick>, int> Take(string s, DateTime d)
        {
            if (Silent || !pending.Remove(s + d.Ticks)) return null;
            var l = new List<SimTick>(); if (d >= FirstDay) for (int i = 0; i < 50; i++) l.Add(new SimTick { T = d.AddHours(1).AddMilliseconds(i * 731), Bid = Math.Round(4000 + i * 0.01, 2), Ask = Math.Round(4000.2 + i * 0.01, 2) });
            return Tuple.Create(l, 0);
        }
    }
    static void Run(TickDownloader dl, DateTime t0, int max = 100000) { var now = t0; for (int i = 0; i < max && dl.Step(now); i++) now = now.AddMilliseconds(100); }
    public static int Main19()
    {
        var today = new DateTime(2026, 11, 13);   // a Friday
        // ------------------------------------------------ 1. the plan: trading days only, newest first, saved days skipped
        {
            var st = new MemStore(); st.Save("XAUUSD", new DateTime(2026, 11, 11), new List<SimTick>()); int sk;
            var days = TickPlan.Days("XAUUSD", today, 7, st, out sk);
            Check(days.SequenceEqual(new[] { new DateTime(2026, 11, 12), new DateTime(2026, 11, 10), new DateTime(2026, 11, 9), new DateTime(2026, 11, 6) }) && sk == 1,
                "⬇ the plan for 1 week: yesterday back, newest first, Saturday / Sunday out, the day already on this PC skipped (and today, still running, not asked)", string.Join(",", days.Select(d => d.ToString("MM-dd"))) + " skipped " + sk);
        }
        // ------------------------------------------------ 2. the broker's limit: 5 empty trading days in a row stop the symbol, said in plain words, remembered
        {
            var st = new MemStore(); var src = new FakeSrc { FirstDay = new DateTime(2026, 10, 1) };
            var dl = new TickDownloader(src, st, new[] { "XAUUSD" }, 92, today, 5); Run(dl, today.AddHours(9));
            int saved = st.Saved.Count; var asked = src.Asked.Count;
            Check(dl.Done && !dl.Cancelled && dl.Saved == saved && saved == 31 && dl.Empty == 5 && asked == 36 && st.Limit("XAUUSD") == new DateTime(2026, 10, 1) && dl.Status.Contains("the broker has no real ticks before 01 Oct 2026"),
                "⬇ ticks from 1 Oct only: the 31 trading days saved, then 5 empty days in a row → stop (the older days never asked), the limit kept and said plainly", "saved " + saved + " asked " + asked + " empty " + dl.Empty + " lim " + st.Limit("XAUUSD") + " | " + dl.Status);
            var src2 = new FakeSrc { FirstDay = new DateTime(2026, 10, 1) }; var dl2 = new TickDownloader(src2, st, new[] { "XAUUSD" }, 92, today, 5); Run(dl2, today.AddHours(10));
            Check(dl2.Done && src2.Asked.Count == 0 && dl2.Skipped == 31, "⬇ pressed again: everything saved / known empty / past the limit is skipped — nothing asked", "asked " + src2.Asked.Count + " skipped " + dl2.Skipped + " | " + dl2.Status);
        }
        // ------------------------------------------------ 3. ■ CANCEL then resume: the saved days are kept and skipped
        {
            var st = new MemStore(); var src = new FakeSrc { FirstDay = new DateTime(2020, 1, 1) };
            var dl = new TickDownloader(src, st, new[] { "XAUUSD" }, 31, today); var now = today.AddHours(9);
            for (int i = 0; i < 100000 && dl.Step(now) && dl.Saved < 6; i++) now = now.AddMilliseconds(100);
            dl.Cancel(); bool stepAfter = dl.Step(now); int keep = st.Saved.Count;
            Check(dl.Done && dl.Cancelled && !stepAfter && keep == 6 && dl.Status.StartsWith("■ cancelled") && dl.Status.Contains("skipped"), "■ CANCEL: it stops at once, the 6 saved days stay, the status says how to go on", keep + " | " + dl.Status);
            var src2 = new FakeSrc { FirstDay = new DateTime(2020, 1, 1) }; var dl2 = new TickDownloader(src2, st, new[] { "XAUUSD" }, 31, today); Run(dl2, now);
            Check(dl2.Done && dl2.Skipped == 6 && !src2.Asked.Contains("XAUUSD 2026-11-12") && src2.Asked.Contains("XAUUSD 2026-10-14") && src2.Asked.Count == 17 && st.Saved.Count == 23, "⬇ resume: the 6 saved days skipped, the other 17 of the month fetched (23 trading days in all)", "asked " + src2.Asked.Count + " saved " + st.Saved.Count);
            var eta = dl2.Eta(now); Check(true, "⬇ progress / ETA computed (" + (eta.HasValue ? TickDownloader.Dur(eta.Value) : "—") + ")");
        }
        // ------------------------------------------------ 4. MT5 does not answer: 3 timeouts → the day counts as not delivered (not marked empty), the next goes
        {
            var st = new MemStore(); var src = new FakeSrc { FirstDay = new DateTime(2020, 1, 1), Silent = true };
            var dl = new TickDownloader(src, st, new[] { "XAUUSD" }, 7, today) { TimeoutSec = 1 }; Run(dl, today);
            Check(dl.Done && dl.NotDelivered == 5 && st.Empty.Count == 0 && dl.Status.Contains("did not answer"), "⬇ no answer: not marked empty (asked again next time), said in the status", dl.Status);
        }
        // ------------------------------------------------ 5. the codec: exact round trip, odd prices, ≥ 5 × smaller
        {
            var rnd = new Random(7); var days = new List<Tuple<DateTime, List<SimTick>>>(); long n = 0;
            for (int d = 0; d < 3; d++)
            {
                var day = new DateTime(2026, 11, 10 + d); var l = new List<SimTick>(); double bid = 4000; var t = day.AddMinutes(5);
                for (int i = 0; i < 40000; i++) { t = t.AddMilliseconds(rnd.Next(0, 900)); bid = Math.Round(bid + (rnd.Next(0, 5) - 2) * 0.01, 2); double sp = Math.Round(0.1 + rnd.Next(0, 4) * 0.01, 2); l.Add(new SimTick { T = t, Bid = bid, Ask = Math.Round(bid + sp, 2) }); }
                l.Add(new SimTick { T = t.AddMilliseconds(3), Bid = 4001.234567, Ask = 4001.4 });   // a price that is not a whole number of points → raw
                days.Add(Tuple.Create(day, l)); n += l.Count;
            }
            var bytes = TickCodec.Encode("XAUUSD", 0.01, days); var back = TickCodec.Read(new MemoryStream(bytes));
            bool same = back.Item1 == "XAUUSD" && back.Item3.Count == 3; int bad = 0;
            for (int d = 0; same && d < 3; d++) { var a = days[d].Item2; var b = back.Item3[d].Item2; if (a.Count != b.Count || back.Item3[d].Item1 != days[d].Item1) { same = false; break; } for (int i = 0; i < a.Count; i++) if (a[i].T != b[i].T || a[i].Bid != b[i].Bid || a[i].Ask != b[i].Ask) bad++; }
            double ratio = 20.0 * n / bytes.Length;
            var sx = SxtReader.Read(new MemoryStream(bytes)); int sbad = 0; for (int d = 0; d < 3; d++) for (int i = 0; i < days[d].Item2.Count; i++) { var a = days[d].Item2[i]; var b = sx.Days[d].Ticks[i]; if (a.T != b.Time || a.Bid != b.Bid || a.Ask != b.Ask) sbad++; }
            Check(sx.Symbol == "XAUUSD" && sx.Days.Count == 3 && sbad == 0, "📦 mt5/study/SxtReader.cs (standalone) reads the same ticks back exactly", "bad " + sbad);
            Check(same && bad == 0, "📦 .sxt round trip: every bid / ask / time back EXACTLY (3 days, " + n.ToString("N0") + " ticks, incl. a raw price)", "same " + same + " bad " + bad);
            Check(ratio >= 5, "📦 .sxt is ≥ 5 × smaller than the 20-byte records (" + ratio.ToString("0.0") + " ×)", ratio.ToString("0.0"));
            var months = TickExport.Build("XAUUSD", 0.01, days.Select(x => x.Item1), (s, d) => days.First(x => x.Item1 == d).Item2);
            var zip = Path.Combine(Path.GetTempPath(), "sxt-test-" + Guid.NewGuid().ToString("N") + ".zip"); TickExport.WriteZip(zip, "XAUUSD", months, "test");
            bool zok; using (var z = System.IO.Compression.ZipFile.OpenRead(zip)) { var e = z.GetEntry("XAUUSD-2026-11.sxt.gz"); var rd = z.GetEntry("00-README.txt"); string txt; using (var r = new StreamReader(rd.Open())) txt = r.ReadToEnd(); var ms = new MemoryStream(); using (var s = e.Open()) s.CopyTo(ms); ms.Position = 0; var bk = TickCodec.Read(ms); zok = bk.Item3.Sum(x => x.Item2.Count) == n && txt.Contains("SXT1") && txt.Contains("zigzag") && txt.Contains("SxtReader"); }
            try { File.Delete(zip); } catch { }
            Check(months.Count == 1 && zok, "📦 EXPORT TICKS FOR CLAUDE: one .sxt.gz per month in the zip + a README with the format spec and the reader");
        }
        Consult13s();
        DataStudio13s();
        Look13s();
        return fails;
    }

    // ================================================================ 🧭 HISTORY CONSULT + 💸 WITHDRAWALS (13s)
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static List<Mt5Bar> Mids(DateTime start, IEnumerable<double> mids) { int i = 0; return mids.Select(m => new Mt5Bar { Time = start.AddMinutes(i++), O = m, H = m + 0.05, L = m - 0.05, C = m, V = 100, Spread = 10 }).ToList(); }
    static IEnumerable<double> Line(double from, double step, int n) { for (int i = 0; i < n; i++) yield return Math.Round(from + step * i, 2); }
    sealed class Mt5Like : IBroker
    {   // the same simulated market, but the engine sees an MT5 account (IsSim = false)
        public readonly SimBroker Sb; public Mt5Like(SimBroker sb) { Sb = sb; }
        public Mt5State State { get { return Sb.State; } } public bool Send(string l) { return Sb.Send(l); } public string Command(string c, params object[] a) { return Sb.Command(c, a); }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
    }
    static ConsultPoint Pt(DateTime at, string src, double[] x, double hedge, double wait, double lck, double add, double close)
    {
        var p = new ConsultPoint { At = at, EndAt = at.AddHours(2), Src = src, Setup = "BREAK", Iter = 1, Target = 20, X = x };
        var v = new[] { hedge, wait, lck, add, close }; for (int i = 0; i < 5; i++) p.Out[ConsultOpt.All[i]] = new ConsultOutcome { ResultX = v[i], WorstX = -2, Minutes = 60, Closed = true };
        return p;
    }
    static double[] X0() { return new double[ConsultFeatures.Names.Length]; }
    sealed class R1 { public HedgeAuto Au; public List<string> Story = new List<string>(), Sent = new List<string>(); }
    static R1 RunEngine(string mode, ConsultMemory mem, bool mt5, string typed = null)
    {
        var r = new R1(); var start = new DateTime(2026, 11, 11, 10, 0, 0); var warm = Mids(start.AddMinutes(-240), Line(4000, 0, 240).Select((m, i) => Math.Round(m + Math.Sin(i / 5.0) * 0.3, 2)));
        var path = Line(4000, 0, 3).Concat(Line(4000, -0.3, 12)).Concat(Line(3996.4, 0, 30)).ToList();
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = warm[0].Time.Date, Balance = 10000, Kind = "STANDARD", FixedSpread = 10, Leverage = 500, StopOutPct = 50 }, Gold(), warm.Concat(Mids(start, path)).ToList());
        if (err != "") throw new Exception(err);
        IBroker b = mt5 ? (IBroker)new Mt5Like(sb) : sb;
        var s = new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, AddLots = 0.1, NetAdvantage = 0.1, HedgeSize = "ADVANTAGE", Target = 20, FirstDir = "BUY", HedgeMode = "PRICE", HedgePrice = 1, Rolling = false,
            NextStart = "PAUSE", PauseSec = 999999, MaxIterations = 1, TrackManual = false, OnManual = "ADOPT", RangeOnly = false, HedgeCap = 0, DdTightEvery = 0, ScaleOut = false, Rescue = false, DangerExit = false, ExitGuard = false,
            HardFloat = 0, HardMargin = 0, BeAfter = 0, CompressMode = "OFF", FullAuto = false, NyOffsetHours = -7, BeLock = false, LockOn = false, RiskGuard = false, ConsultMode = mode };
        var au = new HedgeAuto(); r.Au = au; au.OnTell = (n, l) => r.Story.Add(l); au.ConsultMem = mem;
        au.Send = (c, a, l) => { r.Sent.Add(c + " " + l); return sb.Command(c, a); };
        sb.Playing = true; sb.Speed = 60; while (!sb.Ended && sb.State.ServerNow < start) sb.Advance(0.25);
        if (typed != null) ConsultGate.Confirm(HedgeAuto.AccKeyOf(b), typed);
        sb.OnTickHook = () => au.Step(b);
        var chk = au.Check(b, s); if (chk != "") throw new Exception(chk); au.Start(b, s, false);
        for (int k = 0; k < 200000 && !sb.Ended; k++) sb.Advance(0.25);
        return r;
    }
    static int Consult13s()
    {
        int f0 = fails; var IC = CultureInfo.InvariantCulture;
        // ------------------------------------------------ 1. the features read ONLY finished candles before the decision
        {
            var start = new DateTime(2026, 11, 11, 0, 0, 0); var bars = Mids(start, Line(4000, 0.01, 900).Select((m, i) => Math.Round(m + Math.Sin(i / 7.0), 2)));
            var at = start.AddMinutes(600).AddSeconds(30); var st = new ConsultState { At = at, IterStart = at.AddMinutes(-45), Side = 1, HedgeN = 2, Total = -35, Target = 20, LegHi = 4006, LegLo = 4003, Spread = 0.2 };
            var x1 = ConsultFeatures.Compute(bars, st);
            var fut = bars.Select(b => b.Copy()).ToList(); foreach (var b in fut.Where(b => b.Time.AddMinutes(1) > at)) { b.C += 50; b.H += 60; b.L -= 60; b.V *= 9; }   // the forming candle + the future changed
            fut.AddRange(Mids(start.AddMinutes(900), Line(3000, -1, 50)));
            var x2 = ConsultFeatures.Compute(fut, st); var x3 = ConsultFeatures.Compute(bars.Where(b => b.Time.AddMinutes(1) <= at).ToList(), st);
            Check(x1.SequenceEqual(x2) && x1.SequenceEqual(x3) && x1.Length == ConsultFeatures.Names.Length && x1[0] == 2 && Math.Abs(x1[1] + 1.75) < 1e-9,
                "🧭 features: only the finished candles before the decision — changing the forming candle / the future, or cutting it, changes nothing (no lookahead)", string.Join(",", x1) + " | " + string.Join(",", x2));
        }
        // ------------------------------------------------ 2. the neighbours: walk-forward, your real ticks first, the library labelled as an estimate
        {
            var now = new DateTime(2026, 6, 1); var mem = new List<ConsultPoint>();
            for (int i = 0; i < 40; i++) mem.Add(Pt(new DateTime(2025, 1, 1).AddDays(i), ConsultSrc.LIB, X0(), 0, 1, 0, 0, -1));
            var late = Pt(new DateTime(2026, 5, 31, 23, 0, 0), ConsultSrc.MINE, X0(), 0, 0, 0, 0, 0);   // its outcome is known only at 01:00 on 1 Jun — after now
            mem.Add(late);
            var nb = ConsultAdvisor.Nearest(mem, X0(), "BREAK", now, 40);
            Check(nb.Count == 40 && !nb.Contains(late), "🧭 walk-forward: a decision whose outcome was not known yet at `now` is never a neighbour");
            for (int i = 0; i < 12; i++) { var x = X0(); x[1] = 3; mem.Add(Pt(new DateTime(2025, 6, 1).AddDays(i), ConsultSrc.TICKS, x, 0, -1, 0, 0, 0)); }   // further away than the library
            var a = ConsultAdvisor.Advise(mem, X0(), "BREAK", now, 20, 40, 30);
            Check(a.Mine == 12 && a.Library == 28 && a.Line.Contains(ConsultSrc.LibLabel), "🧭 fewer than 30 real-tick neighbours: all 12 of yours first (even further away), the library fills in — labelled \"" + ConsultSrc.LibLabel + "\"", a.Mine + " / " + a.Library + " | " + a.Line);
            for (int i = 0; i < 25; i++) { var x = X0(); x[1] = 4; mem.Add(Pt(new DateTime(2025, 8, 1).AddDays(i), ConsultSrc.MINE, x, 0, -1, 0, 0, 0)); }
            var b = ConsultAdvisor.Advise(mem, X0(), "BREAK", now, 20, 40, 30);
            Check(b.Mine == 37 && b.Library == 0 && !b.Line.Contains(ConsultSrc.LibLabel), "🧭 ≥ 30 real-tick neighbours known: ONLY yours are used (the library is left out)", b.Mine + " / " + b.Library);
        }
        // ------------------------------------------------ 3. the honesty gate
        {
            var now = new DateTime(2026, 6, 1); var rnd = new Random(3);
            Func<int, double, double, List<ConsultPoint>> mk = (n, edge, noise) => Enumerable.Range(0, n).Select(i => Pt(new DateTime(2025, 1, 1).AddDays(i), ConsultSrc.MINE, X0(), 0.2 + (rnd.NextDouble() - 0.5) * noise, 0.2 + edge + (rnd.NextDouble() - 0.5) * noise, 0.1, -0.5, -1)).ToList();
            var clear = ConsultAdvisor.Advise(mk(40, 1.0, 0.5), X0(), "BREAK", now, 20);
            var few = ConsultAdvisor.Advise(mk(20, 1.0, 0.5), X0(), "BREAK", now, 20);
            var noisy = ConsultAdvisor.Advise(mk(40, 0.05, 6), X0(), "BREAK", now, 20);
            Check(clear.Says == ConsultOpt.WAIT && clear.Line.Contains("CONSULT says WAIT") && clear.Row(ConsultOpt.WAIT).DiffLo > 0, "🧭 40 similar, wait beat hedge now by +$20 a basket with a tight interval → CONSULT says WAIT", clear.Line);
            Check(few.Says == "" && few.Line.Contains("no clear answer") && few.Why.Contains("only 20"), "🧭 only 20 similar (< 30) → no clear answer, your rules", few.Line);
            Check(noisy.Says == "" && noisy.Why.Contains("interval"), "🧭 the best option's 95 % interval includes 0 → no clear answer", noisy.Line);
            var tr = ConsultAdvisor.TrackOf(mk(70, 1.0, 0.5), mk(70, 1.0, 0.5).Select(p => { p.At = p.At.AddYears(1); return p; }).ToList());
            Check(tr.Calls > 0 && tr.Gain > 0 && tr.Text.Contains("clear calls"), "🧭 its own track record: walk-forward calls and their gain vs the engine", tr.Text);
        }
        // ------------------------------------------------ 4. the modes: ADVISE sends nothing because of it; SIM acts on a simulated broker; MT5 needs the typed CONSULT
        {
            Func<ConsultMemory> mem = () => { var m = new ConsultMemory(); m.Add(Enumerable.Range(0, 40).Select(i => Pt(new DateTime(2025, 1, 1).AddDays(i), ConsultSrc.MINE, X0(), -1 + 0.01 * (i % 3), -1, -1, -1, 2 + 0.01 * (i % 5)))); return m; };   // CLOSE clearly best
            var none = RunEngine("ADVISE", null, false); var adv = RunEngine("ADVISE", mem(), false);
            Check(adv.Au.Consult != null && adv.Au.Consult.Says == ConsultOpt.CLOSE && adv.Au.ConsultActs == 0 && adv.Sent.SequenceEqual(none.Sent) && adv.Sent.Any(x => x.Contains("ADD-ON SELL")),
                "🧭 ADVISE: the advice is computed (CLOSE) and shown, but the orders sent are exactly the engine's own (the hedge)", string.Join(" / ", adv.Sent) + " || " + string.Join(" / ", none.Sent));
            var sim = RunEngine("SIM", mem(), false);
            Check(sim.Au.ConsultActs == 1 && sim.Sent.Any(x => x.StartsWith("CLOSEALL")) && !sim.Sent.Any(x => x.Contains("ADD-ON SELL")) && sim.Story.Any(x => x.Contains("🧭 CONSULT → CLOSE NOW. WHY:")),
                "🧭 ACT ON SIMULATED on a simulated broker: the CLEAR advice (close now) goes through the engine's own CLOSEALL → Send path, its WHY in the STORY", string.Join(" / ", sim.Sent));
            var simMt5 = RunEngine("SIM", mem(), true);
            Check(simMt5.Au.ConsultActs == 0 && simMt5.Sent.Any(x => x.Contains("ADD-ON SELL")), "🧭 ACT ON SIMULATED on an MT5 account: it only advises (the engine's hedge goes)", string.Join(" / ", simMt5.Sent));
            ConsultGate.RevokeAll(); var actNo = RunEngine("ACT", mem(), true);
            Check(actNo.Au.ConsultActs == 0 && actNo.Sent.Any(x => x.Contains("ADD-ON SELL")) && actNo.Au.Log.Any(x => x.Contains("not confirmed")), "🧭 ACT ON THIS ACCOUNT on MT5 without the typed CONSULT: advises only, says why", string.Join(" / ", actNo.Sent));
            var actWrong = RunEngine("ACT", mem(), true, "consul"); ConsultGate.RevokeAll();
            var actYes = RunEngine("ACT", mem(), true, " consult ");
            Check(actWrong.Au.ConsultActs == 0 && actYes.Au.ConsultActs == 1 && actYes.Sent.Any(x => x.StartsWith("CLOSEALL")), "🧭 MT5 + the typed CONSULT (this session): it acts; a wrong word does nothing", string.Join(" / ", actYes.Sent));
            ConsultGate.RevokeAll(); Check(!ConsultGate.MayAct("ACT", false, "0@SpasiX simulator") && !ConsultGate.MayAct("ADVISE", true, "") && ConsultGate.MayAct("SIM", true, "") && !ConsultGate.MayAct("SIM", false, "x"), "🧭 DISARM / a restart revokes the confirmation; ADVISE never acts");
        }
        // ------------------------------------------------ 4b. the embedded 6-year library: loaded, all labelled LIB, the note says what it is
        {
            var m = ConsultMemory.WithLibrary();
            Check(m.Count > 1000 && m.MineCount == 0 && m.Points.All(p => p.Src == ConsultSrc.LIB && p.Complete) && m.LibraryNote.Contains("SYNTHETIC ticks") && m.LibraryNote.Contains("ESTIMATE"), "🧭 the embedded library (consult-library.txt.gz): " + m.Count + " decisions, all LIB, its note says futures candles + synthetic ticks = an estimate", m.Count + " | " + m.LibraryNote);
        }
        // ------------------------------------------------ 5. the race lane CNS
        {
            var mine = new AutoSettings { Symbol = "XAUUSD" }; var l = RaceLanes.For("CNS", mine, 5000);
            Check(l.ConsultMode == "SIM" && RaceLanes.Compare.Contains("CNS") && RaceLanes.Keys.Contains("CNS") && RaceLanes.Tip("CNS").Contains(ConsultSrc.LibLabel), "🏁 lane CNS = MY SETTINGS + CONSULT acting on its simulated account, in ▶ COMPARE WITH MY SETTINGS");
        }
        // ------------------------------------------------ 6. the replays: each option from the same basket, the option only once (then the engine's rules)
        {
            var start = new DateTime(2026, 11, 11, 10, 0, 0); var bars = Mids(start.AddMinutes(-300), Line(4000, 0, 300).Concat(Line(4000, -0.05, 60)).Concat(Line(3997, 0.1, 120)));
            var s = new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, AddLots = 0.1, NetAdvantage = 0.1, HedgeSize = "ADVANTAGE", Target = 20, FirstDir = "BUY", HedgeMode = "PRICE", HedgePrice = 1, ExitGuard = false, Rescue = false, ScaleOut = false, NyOffsetHours = -7 };
            var c = new WhatIfCase { N = 3, Symbol = "XAUUSD", Start = start.AddMinutes(-5), SnapAt = start.AddMinutes(25), Banked = 0, MaxDd = -15, Balance = 10000, Target = 20, Settings = s.Save(), Spread = 10 };
            c.Legs.Add(new WhatIfLeg { Ticket = 7001, Side = "BUY", Lots = 0.1, Open = 4000.2, Time = start.AddMinutes(-5) });
            var cc = new ConsultCase { Point = new ConsultPoint { At = c.SnapAt, X = X0(), Target = 20 }, Snap = c }; ConsultSim.Measure(cc, bars, Gold(), 600);
            var o = cc.Point.Out;
            Check(cc.Point.Complete && o[ConsultOpt.CLOSE].Closed && o[ConsultOpt.CLOSE].Minutes < 2 && o[ConsultOpt.CLOSE].ResultX < 0 && Math.Abs(o[ConsultOpt.HEDGE].ResultX - o[ConsultOpt.CLOSE].ResultX) > 0.05 && cc.Point.EndAt >= cc.Point.At,
                "🧭 the five options replayed from the same basket in an in-memory SimBroker: close now = at once at the loss; the others follow their own path", string.Join("; ", ConsultOpt.All.Select(k => k + " " + o[k].ResultX.ToString("0.00", IC) + "× " + o[k].Minutes.ToString("0") + "min " + o[k].Reason)));
            var line = cc.Point.Line(); var back = ConsultPoint.Parse(line);
            Check(back != null && back.Complete && back.R(ConsultOpt.WAIT) == cc.Point.R(ConsultOpt.WAIT), "🧭 a memory point saves and loads (one line)");
        }
        // ------------------------------------------------ 7. 🧭 BUILD FROM MY TICKS: the user's settings on real ticks → TICKS points
        {
            var rnd = new Random(11); var ticks = new List<SimTick>(); var t = new DateTime(2026, 11, 10, 1, 0, 0); double px = 4000;
            for (int i = 0; i < 9000; i++) { t = t.AddSeconds(4); px = Math.Round(px + (rnd.Next(0, 5) - 2) * 0.03 + Math.Sin(i / 400.0) * 0.02, 2); ticks.Add(new SimTick { T = t, Bid = px, Ask = Math.Round(px + 0.15, 2) }); }
            var s = new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, AddLots = 0.1, NetAdvantage = 0.1, HedgeSize = "ADVANTAGE", Target = 30, FirstDir = "BUY", HedgeMode = "PRICE", HedgePrice = 1.5, NextStart = "PAUSE", PauseSec = 60, ExitGuard = true, NyOffsetHours = -7 };
            var pts = ConsultBuilder.FromRun(s, Gold(), null, ticks, 10000);
            Check(pts.Count > 0 && pts.All(p => p.Src == ConsultSrc.TICKS && p.Complete), "🧭 BUILD FROM MY TICKS: the settings replayed on real ticks, " + pts.Count + " hedge decision(s) measured → points labelled as YOUR real ticks", pts.Count.ToString());
            Check(ConsultBuilder.Runs(new[] { new DateTime(2026, 1, 5), new DateTime(2026, 1, 6), new DateTime(2026, 1, 12), new DateTime(2026, 1, 13) }).Count == 2, "🧭 tick days are grouped in runs (a gap > 3 days starts a new run)");
        }
        // ------------------------------------------------ 8. 💸 the withdrawal plan bookkeeping
        {
            var t0 = new DateTime(2026, 1, 1); long tk = 1; Func<int, string, double, string, Mt5Deal> D = (day, type, p, cm) => new Mt5Deal { Ticket = tk++, Time = t0.AddDays(day), Type = type, Profit = p, Comment = cm };
            var deals = new List<Mt5Deal> { D(0, "BALANCE", 5000, "start balance"), D(10, "DEAL", 3000, ""), D(20, "DEAL", 2100, ""), D(21, "BALANCE", -5100, WithdrawPlan.SimComment), D(30, "DEAL", -4700, ""), D(31, "BALANCE", 4700, WithdrawPlan.RestartComment), D(40, "DEAL", 2415, "") };
            int before = deals.Count; var st = WithdrawPlan.Of(deals, 7415, 5000, 100, t0.AddDays(50));
            Check(st.Deposits == 2 && st.Restarts == 1 && st.Withdrawals == 1 && Math.Abs(st.Deposited - 9700) < 1e-6 && Math.Abs(st.Withdrawn - 5100) < 1e-6 && Math.Abs(st.NetCash + 4600) < 1e-6 && deals.Count == before,
                "💸 deposits (the first + one after a blow-up), a withdrawal, NET CASH = withdrawn − deposited (−$4,600); the deals are only read", st.Deposits + " " + st.Restarts + " " + st.Withdrawals + " " + st.NetCash);
            Check(st.Cycles.Count(c => c.How == "💸 withdrawal") == 1 && st.Cycles.Count(c => c.How == "💥 blow-up") == 1 && Math.Abs(st.AvgDaysToDouble - 21) < 1e-6, "💸 cycles: one doubled (21 days to double), one blown");
            Check(WithdrawPlan.ProgressText(st) == "$7,415 / $10,000 — 48 % of the way" && !st.Due, "💸 the next withdrawal: \"$7,415 / $10,000 — 48 % of the way\"", WithdrawPlan.ProgressText(st));
            var half = WithdrawPlan.Of(deals, 7600, 5000, 50, t0.AddDays(50)); var lines = WithdrawPlan.Lines(half, false, 50);
            Check(half.Due && half.Level == 7500 && WithdrawPlan.AmountNow(7600, 5000) == 2600 && lines.Any(l => l.Contains("do it at your broker")) && lines.Any(l => l.Contains(ConsultSrc.LibLabel)), "💸 +50 % option: due at $7,500 → withdraw $2,600 back to the start; MT5 = a reminder only (\"do it at your broker\"), bench numbers labelled as an estimate");
            var sim = WithdrawPlan.Lines(half, true, 50); Check(sim.Any(l => l.Contains("asks first")) && !sim.Any(l => l.Contains("do it at your broker")), "💸 DEMO: a due withdrawal is booked only after asking (simulated)");
            var acct = new SimAccount(new Mt5State()); acct.Reset(new SimAccountSettings { Balance = 5000 }, t0); acct.SetBalance(10000, t0.AddDays(5)); acct.SetBalance(5000, t0.AddDays(6), WithdrawPlan.SimComment); acct.SetBalance(5000, t0.AddDays(7));
            var st2 = WithdrawPlan.Of(acct.State.Deals, acct.State.Account.Balance, 5000, 100, t0.AddDays(8));
            Check(st2.Withdrawals == 1 && st2.Withdrawn == 5000 && acct.State.Deals.Any(d => d.Comment == WithdrawPlan.SimComment), "💸 a DEMO booking is a balance deal (SetBalance with its comment) and counts as a withdrawal", st2.Withdrawals + " " + st2.Withdrawn);
        }
        return fails - f0;
    }

    // ================================================================ 📥 DATA STUDIO (13s): range planning, inventory, delete, export
    static void DataStudio13s()
    {
        var root0 = HistoryStore.Root; var tmp = Path.Combine(Path.GetTempPath(), "spasix-ds-" + Guid.NewGuid().ToString("N")); HistoryStore.Root = tmp;
        try
        {
            var today = new DateTime(2026, 11, 13);
            var r = DataPlan.Range(31, today);
            Check(r.Item1 == new DateTime(2026, 10, 13) && r.Item2 == new DateTime(2026, 11, 12) && DataPlan.Presets.Length == 6 && DataPlan.Presets.Last().Item1.Contains("BROKER"), "📥 presets: 1 MONTH = 13 Oct → 12 Nov (TO = yesterday: today is still running); 1 week … as far as the broker has");
            // a few days on "this PC": candles 2–6 Nov, ticks 4–5 Nov, MT5 had none on 9 Nov, no ticks on 3 Nov
            var bars = Mids(new DateTime(2026, 11, 2), Line(4000, 0.01, 60));
            for (var d = new DateTime(2026, 11, 2); d <= new DateTime(2026, 11, 6); d = d.AddDays(1)) HistoryStore.SaveDay("XAUUSD", d, bars.Select(b => { var c = b.Copy(); c.Time = d.Add(b.Time - b.Time.Date); return c; }).ToList());
            HistoryStore.MarkNone("XAUUSD", new DateTime(2026, 11, 9));
            foreach (var d in new[] { new DateTime(2026, 11, 4), new DateTime(2026, 11, 5) }) HistoryStore.SaveTicks("XAUUSD", d, Enumerable.Range(0, 500).Select(i => new SimTick { T = d.AddHours(2).AddSeconds(i * 3), Bid = Math.Round(4000 + i * 0.01, 2), Ask = Math.Round(4000.2 + i * 0.01, 2) }).ToList());
            var st = new DiskTickStore(); st.MarkEmpty("XAUUSD", new DateTime(2026, 11, 3));
            var mc = DataPlan.MissingCandles("XAUUSD", new DateTime(2026, 11, 2), new DateTime(2026, 11, 12)); var mt = DataPlan.MissingTicks("XAUUSD", new DateTime(2026, 11, 2), new DateTime(2026, 11, 12), st);
            Check(mc.SequenceEqual(new[] { 10, 11, 12 }.Select(x => new DateTime(2026, 11, x))) && mt.SequenceEqual(new[] { 2, 6, 9, 10, 11, 12 }.Select(x => new DateTime(2026, 11, x))),
                "📥 the plan: candles missing 10–12 Nov (9 Nov = MT5 has none, the weekend skipped); ticks missing 2, 6, 9–12 Nov (3 Nov known empty, 4–5 saved)", string.Join(",", mc.Select(d => d.Day)) + " | " + string.Join(",", mt.Select(d => d.Day)));
            var inv = DataInventory.Days("XAUUSD"); var tab = DataInventory.Table("XAUUSD");
            var nov = tab.FirstOrDefault(l => l.Contains("2026-11")) ?? "";
            Check(inv.Count(x => x.Candles) == 5 && inv.Count(x => x.Ticks) == 2 && inv.Any(x => x.NoCandles && x.Day.Day == 9) && nov.Contains(" ○○●●○  ×··") && tab[0].Contains("5 day(s) of candles") && tab[0].Contains("2 day(s) of real ticks"),
                "📥 the inventory: 5 days of candles, 2 of ticks, the empty days; the calendar line of November reads ○○●●○  ×·· (1 Nov a Sunday)", string.Join(" / ", tab));
            var del = DataInventory.Delete("XAUUSD", new DateTime(2026, 11, 5), new DateTime(2026, 11, 6), false, true);
            var inv2 = DataInventory.Days("XAUUSD");
            Check(del.Item1 == 0 && del.Item2 == 1 && inv2.Count(x => x.Ticks) == 1 && inv2.Count(x => x.Candles) == 5, "🗑 delete the TICKS of 5–6 Nov: one tick day gone, every candle day kept", del.Item1 + "/" + del.Item2);
            st.SetLimit("XAUUSD", new DateTime(2026, 11, 4)); var del2 = DataInventory.Delete("XAUUSD", new DateTime(2026, 11, 2), new DateTime(2026, 11, 4), true, true);
            Check(del2.Item1 == 3 && del2.Item2 == 1 && st.Limit("XAUUSD") == DateTime.MinValue && !st.IsEmpty("XAUUSD", new DateTime(2026, 11, 3)) && DataInventory.Days("XAUUSD").Count(x => x.Candles) == 2,
                "🗑 delete candles + ticks of 2–4 Nov: 3 candle days + 1 tick day + the empty marks gone; the broker's tick limit inside the range is forgotten (asked again next time)", del2.Item1 + "/" + del2.Item2 + " lim " + st.Limit("XAUUSD"));
            HistoryStore.SaveTicks("XAUUSD", new DateTime(2026, 11, 6), Enumerable.Range(0, 300).Select(i => new SimTick { T = new DateTime(2026, 11, 6, 3, 0, 0).AddSeconds(i), Bid = 4001, Ask = 4001.2 }).ToList());
            var ex = DataExport.Build(new[] { "XAUUSD" }, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), true, true);
            var zip = Path.Combine(tmp, "x.zip"); DataExport.WriteZip(zip, ex, DataExport.Readme(ex, "test", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)));
            bool ok; using (var z = System.IO.Compression.ZipFile.OpenRead(zip)) { var t = z.GetEntry("ticks/XAUUSD-2026-11.sxt.gz"); var c = z.GetEntry("candles/XAUUSD-2026-11.m1.csv.gz"); var ms = new MemoryStream(); using (var s1 = t.Open()) s1.CopyTo(ms); ms.Position = 0; var back = TickCodec.Read(ms); string csv; using (var g = new System.IO.Compression.GZipStream(c.Open(), System.IO.Compression.CompressionMode.Decompress)) using (var rd = new StreamReader(g)) csv = rd.ReadToEnd(); ok = back.Item3.Sum(x => x.Item2.Count) == 300 && csv.Split('\n').Count(l => l.StartsWith("2026-11-0")) == 120 && z.GetEntry("00-README.txt") != null; }
            Check(ok && DataExport.Size(ex) > 0, "📦 DATA STUDIO export: ticks/<SYM>-yyyy-MM.sxt.gz + candles/<SYM>-yyyy-MM.m1.csv.gz + README, the size known before writing (" + DataInventory.Mb(DataExport.Size(ex)) + ")");
        }
        finally { HistoryStore.Root = root0; try { Directory.Delete(tmp, true); } catch { } }
    }

    // ================================================================ 🍂 the seasons look (13s): pure parts
    static void Look13s()
    {
        Check(SxSeasons.Of(new DateTime(2026, 11, 13)) == "FALL" && SxSeasons.Of(new DateTime(2026, 12, 21)) == "WINTER" && SxSeasons.Of(new DateTime(2027, 3, 19)) == "WINTER" && SxSeasons.Of(new DateTime(2027, 3, 20)) == "SPRING" && SxSeasons.Of(new DateTime(2027, 6, 21)) == "SUMMER" && SxSeasons.Of(new DateTime(2027, 9, 22)) == "FALL",
            "🍂 the season by the NY date (astronomical, as KeyStone): Dec 21 winter, Mar 20 spring, Jun 21 summer, Sep 22 fall");
        bool clean = SxSeasons.Names.All(n => { var p = SxSeasons.Palette(n); return new[] { p.Accent, p.Accent2, p.Glow, p.Sky1, p.Sky2, p.Ink }.All(c => !SxSeasons.Reserved.Contains(c, StringComparer.OrdinalIgnoreCase)); });
        Check(clean && SxSeasons.Palette("FALL").Accent == "#E8A33D" && SxSeasons.Palette("WINTER").Accent == "#9FD8F0", "🍂 KeyStone's palettes (accents only) — never the P/L green / red");
        var lk = new SxLook { Season = "WINTER", Scene = false, Quotes = true, Anim = false }; var back = SxLook.Load(lk.Save()); var def = SxLook.Load("");
        Check(back.Season == "WINTER" && !back.Scene && back.Quotes && !back.Anim && def.Season == "AUTO" && !def.Anim && def.Quotes && new SxLook { Season = "OFF" }.Resolve(DateTime.Today) == "", "🍂 LOOK settings: saved / loaded; the default = SEASON AUTO, the animation OFF; OFF = no season");
        var now = new DateTime(2026, 11, 13, 10, 5, 0);
        var gap = new SxLookCtx { Server = new DateTime(2026, 11, 13, 23, 30, 0), NyOff = -7, CloseGapMin = 45 };   // 16:30 NY
        var dd = new SxLookCtx { Server = new DateTime(2026, 11, 13, 17, 0, 0), NyOff = -7, CloseGapMin = 45, Total = -120, Target = 20, Balance = 9000, WdStart = 5000, WdLevel = 10000 };
        var near = new SxLookCtx { Server = dd.Server, NyOff = -7, CloseGapMin = 45, Total = -5, Target = 20, Balance = 9600, WdStart = 5000, WdLevel = 10000 };
        var due = new SxLookCtx { Server = dd.Server, NyOff = -7, CloseGapMin = 45, Balance = 10200, WdStart = 5000, WdLevel = 10000, Simulated = true };
        var calm = new SxLookCtx { Server = dd.Server, NyOff = -7, CloseGapMin = 45, Balance = 6000, WdStart = 5000, WdLevel = 10000 };
        string l1 = SxSeasonQuotes.Line(gap, "FALL", now), l2 = SxSeasonQuotes.Line(dd, "FALL", now), l3 = SxSeasonQuotes.Line(near, "FALL", now), l4 = SxSeasonQuotes.Line(due, "FALL", now), l5 = SxSeasonQuotes.Line(calm, "FALL", now.AddMinutes(10));
        Check(l1.StartsWith("⏰ the 17:00 NY close in 30 min") && l2.StartsWith("⚠ the basket is in drawdown: −$120.00 (−6.0 × the target)") && l3.Contains("withdrawal is near: 92 %") && l4.StartsWith("💸 withdrawal due") && l4.Contains("simulated") && l5.StartsWith("🍂"),
            "🍂 the header line: the close first, then a basket in drawdown, then a withdrawal near / due, else the season quote", l1 + " | " + l2 + " | " + l3 + " | " + l4 + " | " + l5);
        Check(SxSeasonQuotes.Quote("WINTER", now) == SxSeasonQuotes.Quote("WINTER", now.AddMinutes(59)) && SxSeasonQuotes.Quote("WINTER", now).StartsWith("❄"), "🍂 a season quote holds for 3 hours");
    }
}
