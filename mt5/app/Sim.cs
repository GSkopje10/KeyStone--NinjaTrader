// SpasiX — the BACKTEST: MT5's own 1-minute candles (with their spread) saved on this PC, replayed tick by tick through the same windows as live.
// The simulated broker (SimBroker) answers the same commands as the bridge (ORDER / CLOSE / CLOSEALL / MODIFY / MODORD / CANCEL / CANCELALL / BE)
// with a HEDGING account: every order its own ticket, market orders at the ask / bid (+ slippage), limits at their price, stops at the market,
// stops / targets checked on every tick, commission per lot (half in, half out), swap at the server's midnight (×3 on Wednesday), margin with the
// leverage and a stop-out. Inside a candle the price walks open → low → high → close (a rising candle) or open → high → low → close (a falling one),
// so the candles you see are exactly MT5's. Nothing here touches the real account.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class SimSpec
    {
        public string Name = "", Description = "", Currency = "USD"; public int Digits = 2; public double Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100;
        public int ServerOffsetSec = 10800; public int NyOffsetHours = -7;   // NY = server time + this
        public double SwapLong, SwapShort; public int SwapMode; public DayOfWeek Swap3Day = DayOfWeek.Wednesday; public double CommissionPerLot = -1;   // MT5's swap (its mode), your commission seen in MT5's deals (−1 = unknown)
        public static SimSpec From(Mt5Symbol s, int serverOffsetSec, int nyOff, string cur)
        {
            return new SimSpec { Name = s.Name, Description = s.Description, Digits = s.Digits, Point = s.Point > 0 ? s.Point : 0.01, Contract = s.Contract > 0 ? s.Contract : 1, TickValue = s.TickValue > 0 ? s.TickValue : 1, TickSize = s.TickSize > 0 ? s.TickSize : (s.Point > 0 ? s.Point : 0.01), VolMin = s.VolMin > 0 ? s.VolMin : 0.01, VolStep = s.VolStep > 0 ? s.VolStep : 0.01, VolMax = s.VolMax > 0 ? s.VolMax : 100, ServerOffsetSec = serverOffsetSec, NyOffsetHours = nyOff, Currency = cur ?? "USD",
                SwapLong = s.SwapLong, SwapShort = s.SwapShort, SwapMode = s.SwapMode, Swap3Day = (DayOfWeek)Math.Max(0, Math.Min(6, s.Swap3Day)), CommissionPerLot = s.CommissionPerLot,
                MarginPerLot = s.MarginPerLot, MarginRefPrice = s.MarginRefPrice, MarginUseLeg = s.MarginUseLeg,
                HedgedRatio = s.MarginHedged < 0 ? -1 : s.MarginInitial > 0 && s.MarginPerLot > 0 ? Math.Min(1, s.MarginHedged / s.MarginPerLot) : s.Contract > 0 ? Math.Min(1, s.MarginHedged / s.Contract) : -1 };
        }
        // MT5's margin: per lot at MarginRefPrice with the account's leverage then (MarginLeverage); HedgedRatio = what a hedged lot costs vs a normal one (−1 = unknown); larger leg only
        public double MarginPerLot, MarginRefPrice, HedgedRatio = -1; public int MarginLeverage; public bool MarginUseLeg;
        public double PointValue { get { return TickSize > 0 ? TickValue / TickSize * Point : 0; } }   // $ per point per lot
    }

    // the saved history: %AppData%\SpasiX\MT5\history\SYMBOL\yyyyMMdd.csv (one server day of 1-minute candles: epoch,o,h,l,c,v,spread) + spec.txt
    public static class HistoryStore
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpasiX", "MT5", "history");
        static string Safe(string sym) { foreach (var c in Path.GetInvalidFileNameChars()) sym = sym.Replace(c, '_'); return sym; }
        public static string DirOf(string sym) { return Path.Combine(Root, Safe(sym)); }
        static string FileOf(string sym, DateTime day) { return Path.Combine(DirOf(sym), day.ToString("yyyyMMdd", IC) + ".csv"); }
        public static bool HasDay(string sym, DateTime day) { return File.Exists(FileOf(sym, day)); }
        public static void SaveDay(string sym, DateTime day, List<Mt5Bar> bars)
        {
            Directory.CreateDirectory(DirOf(sym));
            var lines = bars.Count == 0 ? new List<string> { "#empty" } : bars.Select(b => Mt5State.Epoch(b.Time).ToString(IC) + "," + b.O.ToString("R", IC) + "," + b.H.ToString("R", IC) + "," + b.L.ToString("R", IC) + "," + b.C.ToString("R", IC) + "," + b.V.ToString(IC) + "," + b.Spread.ToString(IC)).ToList();
            File.WriteAllLines(FileOf(sym, day), lines);
        }
        public static List<Mt5Bar> LoadDay(string sym, DateTime day)
        {
            var res = new List<Mt5Bar>(); var f = FileOf(sym, day); if (!File.Exists(f)) return res;
            foreach (var ln in File.ReadAllLines(f))
            {
                if (ln.Length == 0 || ln[0] == '#') continue; var a = ln.Split(','); if (a.Length < 7) continue; long t; double o, h, l, c; long v; int sp;
                if (!long.TryParse(a[0], NumberStyles.Integer, IC, out t) || !double.TryParse(a[1], NumberStyles.Float, IC, out o) || !double.TryParse(a[2], NumberStyles.Float, IC, out h) || !double.TryParse(a[3], NumberStyles.Float, IC, out l) || !double.TryParse(a[4], NumberStyles.Float, IC, out c)) continue;
                long.TryParse(a[5], NumberStyles.Integer, IC, out v); int.TryParse(a[6], NumberStyles.Integer, IC, out sp);
                res.Add(new Mt5Bar { Time = Mt5State.Unix(t), O = o, H = h, L = l, C = c, V = v, Spread = sp });
            }
            return res;
        }
        // MT5's real ticks of one day: yyyyMMdd.ticks (binary: per tick the milliseconds from the day's start, the bid, the ask)
        static string TickFile(string sym, DateTime day) { return Path.Combine(DirOf(sym), day.ToString("yyyyMMdd", IC) + ".ticks"); }
        public static bool HasTicks(string sym, DateTime day) { return File.Exists(TickFile(sym, day)); }
        public static void SaveTicks(string sym, DateTime day, List<SimTick> ticks)
        {
            Directory.CreateDirectory(DirOf(sym));
            using (var w = new BinaryWriter(File.Create(TickFile(sym, day)))) foreach (var t in ticks) { w.Write((int)(t.T - day).TotalMilliseconds); w.Write(t.Bid); w.Write(t.Ask); }
        }
        public static List<SimTick> LoadTicks(string sym, DateTime day)
        {
            var res = new List<SimTick>(); var f = TickFile(sym, day); if (!File.Exists(f)) return res;
            try { using (var r = new BinaryReader(File.OpenRead(f))) { long n = r.BaseStream.Length / 20; res.Capacity = (int)n; for (long i = 0; i < n; i++) { int ms = r.ReadInt32(); double b = r.ReadDouble(), a = r.ReadDouble(); res.Add(new SimTick { T = day.AddMilliseconds(ms), Bid = b, Ask = a }); } } } catch { }
            return res;
        }
        public static List<SimTick> LoadTickRange(string sym, DateTime from, DateTime to) { var res = new List<SimTick>(); for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) res.AddRange(LoadTicks(sym, d)); return res; }
        public static int TickDays(string sym) { try { var d = DirOf(sym); return Directory.Exists(d) ? Directory.GetFiles(d, "*.ticks").Length : 0; } catch { return 0; } }
        public static List<Mt5Bar> LoadRange(string sym, DateTime from, DateTime to) { var res = new List<Mt5Bar>(); for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) res.AddRange(LoadDay(sym, d)); res.Sort((a, b) => a.Time.CompareTo(b.Time)); return res; }
        public static List<DateTime> Days(string sym)
        {
            var res = new List<DateTime>(); var d = DirOf(sym); if (!Directory.Exists(d)) return res;
            foreach (var f in Directory.GetFiles(d, "*.csv")) { DateTime t; if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyyMMdd", IC, DateTimeStyles.None, out t)) { var fi = new FileInfo(f); if (fi.Length > 10) res.Add(t); } }
            res.Sort(); return res;
        }
        public static List<string> Symbols() { try { return Directory.Exists(Root) ? Directory.GetDirectories(Root).Select(Path.GetFileName).Where(x => File.Exists(Path.Combine(Root, x, "spec.txt"))).OrderBy(x => x).ToList() : new List<string>(); } catch { return new List<string>(); } }
        public static void SaveSpec(SimSpec s)
        {
            Directory.CreateDirectory(DirOf(s.Name));
            File.WriteAllLines(Path.Combine(DirOf(s.Name), "spec.txt"), new[] { "name=" + s.Name, "description=" + s.Description, "currency=" + s.Currency, "digits=" + s.Digits.ToString(IC), "point=" + s.Point.ToString("R", IC), "contract=" + s.Contract.ToString("R", IC),
                "tickvalue=" + s.TickValue.ToString("R", IC), "ticksize=" + s.TickSize.ToString("R", IC), "volmin=" + s.VolMin.ToString("R", IC), "volstep=" + s.VolStep.ToString("R", IC), "volmax=" + s.VolMax.ToString("R", IC), "serveroffset=" + s.ServerOffsetSec.ToString(IC), "nyoffset=" + s.NyOffsetHours.ToString(IC),
                "swaplong=" + s.SwapLong.ToString("R", IC), "swapshort=" + s.SwapShort.ToString("R", IC), "swapmode=" + s.SwapMode.ToString(IC), "swap3=" + ((int)s.Swap3Day).ToString(IC), "commission=" + s.CommissionPerLot.ToString("R", IC),
                "marginperlot=" + s.MarginPerLot.ToString("R", IC), "marginref=" + s.MarginRefPrice.ToString("R", IC), "hedgedratio=" + s.HedgedRatio.ToString("R", IC), "marginlev=" + s.MarginLeverage.ToString(IC), "marginleg=" + (s.MarginUseLeg ? "1" : "0") });
        }
        public static SimSpec LoadSpec(string sym)
        {
            var f = Path.Combine(DirOf(sym), "spec.txt"); if (!File.Exists(f)) return null; var d = new Dictionary<string, string>();
            foreach (var ln in File.ReadAllLines(f)) { int i = ln.IndexOf('='); if (i > 0) d[ln.Substring(0, i)] = ln.Substring(i + 1); }
            Func<string, double, double> n = (k, def) => { string v; double x; return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, IC, out x) ? x : def; };
            string nm; d.TryGetValue("name", out nm); string ds; d.TryGetValue("description", out ds); string cu; d.TryGetValue("currency", out cu);
            return new SimSpec { Name = nm ?? sym, Description = ds ?? "", Currency = string.IsNullOrEmpty(cu) ? "USD" : cu, Digits = (int)n("digits", 2), Point = n("point", 0.01), Contract = n("contract", 1), TickValue = n("tickvalue", 1), TickSize = n("ticksize", 0.01), VolMin = n("volmin", 0.01), VolStep = n("volstep", 0.01), VolMax = n("volmax", 100), ServerOffsetSec = (int)n("serveroffset", 10800), NyOffsetHours = (int)n("nyoffset", -7),
                SwapLong = n("swaplong", 0), SwapShort = n("swapshort", 0), SwapMode = (int)n("swapmode", 0), Swap3Day = (DayOfWeek)(int)n("swap3", 3), CommissionPerLot = n("commission", -1),
                MarginPerLot = n("marginperlot", 0), MarginRefPrice = n("marginref", 0), HedgedRatio = n("hedgedratio", -1), MarginLeverage = (int)n("marginlev", 0), MarginUseLeg = n("marginleg", 0) == 1 };
        }
    }

    // downloads missing days from MT5 through the bridge, one day at a time (HIST|symbol|from|to → HIST … HISTEND), and saves them
    // downloads missing days from MT5 through the bridge, one day at a time: the 1-minute candles (HIST → HIST … HISTEND) and MT5's REAL TICKS
    // (HISTT → TT … TTEND) of the days to replay, and saves them on this PC
    public sealed class HistoryDownloader
    {
        readonly IBroker live; readonly string sym; readonly List<DateTime> days; readonly DateTime tickFrom; readonly bool wantTicks;
        int idx, tries, timeouts, phase; DateTime askedAt = DateTime.MinValue, retryAt = DateTime.MinValue; bool waiting;
        public string Status = ""; public bool Done, Failed; public int Saved, Skipped, TickDays; public long TickCount;
        public double Progress { get { return days.Count == 0 ? 1 : (idx + (phase == 1 ? 0.5 : 0)) / days.Count; } }
        public HistoryDownloader(IBroker live, string sym, DateTime from, DateTime to, DateTime ticksFrom, bool ticks = true)
        {
            this.live = live; this.sym = sym; tickFrom = ticksFrom.Date; wantTicks = ticks; days = new List<DateTime>(); var today = DateTime.Today;
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
                if (!HistoryStore.HasDay(sym, d) || d >= today.AddDays(-1) || (wantTicks && d >= tickFrom && !HistoryStore.HasTicks(sym, d) && HistoryStore.LoadDay(sym, d).Count > 0)) days.Add(d);   // today / yesterday: always fresh
            if (days.Count == 0) { Done = true; Status = "✓ every day is already on this PC"; }
        }
        public HistoryDownloader(IBroker live, string sym, DateTime from, DateTime to) : this(live, sym, from, to, to.AddDays(1), false) { }
        // call every ~100 ms; true while it works
        public bool Step(DateTime now)
        {
            if (Done) return false;
            if (idx >= days.Count) { Done = true; Status = "✓ " + Saved + " day(s) of candles" + (TickDays > 0 ? " + " + TickDays + " day(s) of real ticks (" + TickCount.ToString("N0", CultureInfo.InvariantCulture) + ")" : "") + " downloaded" + (Skipped > 0 ? " • " + Skipped + " without candles (weekend / holiday)" : ""); return false; }
            var day = days[idx]; long from = Mt5State.Epoch(day), to = Mt5State.Epoch(day.AddDays(1)); string key = sym + "|" + from; bool weekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            string dn = day.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture);
            if (phase == 0 && HistoryStore.HasDay(sym, day) && day < DateTime.Today.AddDays(-1)) { phase = 1; }
            if (phase == 1 && (!wantTicks || day < tickFrom || HistoryStore.HasTicks(sym, day) && day < DateTime.Today.AddDays(-1) || HistoryStore.LoadDay(sym, day).Count == 0)) { Next(); return true; }
            if (phase == 0)
            {
                Tuple<List<Mt5Bar>, int> got = null;
                lock (live.State.Gate) { if (live.State.Hist.TryGetValue(key, out got)) live.State.Hist.Remove(key); }
                if (got != null)
                {
                    waiting = false;
                    if ((got.Item2 != 0 || (got.Item1.Count == 0 && !weekend)) && tries < 4) { tries++; retryAt = now.AddSeconds(1.5 * tries); Status = "MT5 is loading " + dn + " from the broker … (try " + tries + ")"; return true; }
                    if (day < DateTime.Today || got.Item1.Count > 0) HistoryStore.SaveDay(sym, day, got.Item1);
                    if (got.Item1.Count > 0) Saved++; else Skipped++;
                    phase = 1; tries = 0; timeouts = 0; retryAt = DateTime.MinValue; return true;
                }
                return Ask(now, "HIST|" + sym + "|" + from + "|" + to, "downloading the candles of " + sym + " " + dn + " (" + (idx + 1) + " / " + days.Count + ")", 8);
            }
            Tuple<List<SimTick>, int> gt = null;
            lock (live.State.Gate) { if (live.State.TickHist.TryGetValue(key, out gt)) live.State.TickHist.Remove(key); }
            if (gt != null)
            {
                waiting = false;
                if ((gt.Item2 != 0 || gt.Item1.Count == 0) && !weekend && tries < 5) { tries++; retryAt = now.AddSeconds(2.0 * tries); Status = "MT5 is loading the real ticks of " + dn + " … (try " + tries + ")"; return true; }
                if (gt.Item1.Count > 0) { HistoryStore.SaveTicks(sym, day, gt.Item1); TickDays++; TickCount += gt.Item1.Count; }
                Next(); return true;
            }
            return Ask(now, "HISTT|" + sym + "|" + from + "|" + to, "downloading MT5's real ticks of " + sym + " " + dn + " (" + (idx + 1) + " / " + days.Count + ")", 45);
        }
        void Next() { idx++; phase = 0; tries = 0; timeouts = 0; retryAt = DateTime.MinValue; waiting = false; }
        bool Ask(DateTime now, string cmd, string status, double timeout)
        {
            if (!waiting)
            {
                if (now < retryAt) return true;
                if (!live.Send(cmd)) { Failed = true; Done = true; Status = "✖ MT5 is not connected — start MT5 with the SpasiXBridge (the days already on this PC still work)"; return false; }
                waiting = true; askedAt = now; Status = status; return true;
            }
            if ((now - askedAt).TotalSeconds > timeout)
            {
                if (++timeouts > 3) { if (phase == 1) { Next(); return true; } Failed = true; Done = true; Status = "✖ MT5 does not answer — put the new SpasiXBridge v5 on the chart (⚙ SETUP)"; return false; }
                waiting = false;
            }
            return true;
        }
    }

    public sealed class SimSettings
    {
        public string Symbol = ""; public DateTime From, To; public int WarmDays = 3; public double StartNyHour = double.NaN;   // NaN = the server's midnight; NY hours otherwise (−6.5 = 17:30 the day before)
        public double Balance = 10000, CommissionPerLot = 7, SwapLong = 0, SwapShort = 0, StopOutPct = 50, MarkupPts = 0; public int Leverage = 500, FixedSpread = -1, SlipPts = 0; public string Currency = "USD", Kind = "RAW";
        public SimAccountSettings Account() { return new SimAccountSettings { Balance = Balance, CommissionPerLot = CommissionPerLot, SwapLong = SwapLong, SwapShort = SwapShort, StopOutPct = StopOutPct, Leverage = Leverage, SlipPts = SlipPts, Currency = Currency, Kind = Kind, MarkupPts = MarkupPts }; }
    }

    // the simulated broker of the BACKTEST: one or more symbols replayed together, minute by minute, on one simulated account
    public sealed class SimBroker : IBroker
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        readonly Mt5State state = new Mt5State(); readonly SimAccount acct;
        public Mt5State State { get { return state; } } public bool IsSim { get { return true; } } public string Problem { get { return ""; } } public int Port { get { return 0; } }
        public SimSettings Set { get; private set; }
        // one symbol: its candles, its real ticks (if downloaded), where the replay is; Path = the prices inside the current minute: {time from the minute's start (.NET ticks), bid, ask (NaN = from the candle's spread)}
        sealed class Feed { public SimSpec Spec; public List<Mt5Bar> Bars; public List<SimTick> Ticks; public int TickIdx; public int Start, Cur; public List<double[]> Path; public int PathPos; public long VolPerTick; public int RealBars, MadeBars;
            public DateTime Next { get { return Cur >= Bars.Count ? DateTime.MaxValue : Path != null ? Bars[Cur].Time.AddTicks((long)Path[PathPos][0]) : Bars[Cur].Time; } } }
        readonly List<Feed> feeds = new List<Feed>(); List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> data;
        // how much of the replay so far came from MT5's real ticks (the rest: the candle's open → low → high → close)
        public double RealShare { get { int r = feeds.Sum(f => f.RealBars), m = feeds.Sum(f => f.MadeBars); return r + m == 0 ? (feeds.Any(f => f.Ticks != null && f.Ticks.Count > 0) ? 1 : 0) : (double)r / (r + m); } }
        public bool HasTicks { get { return feeds.Any(f => f.Ticks != null && f.Ticks.Count > 0); } }
        public SimSpec Spec { get { return feeds.Count > 0 ? feeds[0].Spec : null; } }
        public List<SimSpec> Specs { get { return feeds.Select(f => f.Spec).ToList(); } }
        public DateTime Clock { get; private set; } public DateTime StartTime { get; private set; } public DateTime EndTime { get; private set; }
        public bool Playing; public double Speed = 1; public bool Ended { get; private set; }
        public const double MaxSpeed = 100000;
        public List<string> Notes { get { return acct.Notes; } }
        public readonly List<DateTime> ClosedDays = new List<DateTime>();   // the server days that ended during the replay (the app saves them)
        public int TicksPerBar = 16; int cmdSeq;
        public SimBroker() { acct = new SimAccount(state); }
        public SimAccount Account { get { return acct; } }

        public double Progress { get { var span = (EndTime - StartTime).TotalSeconds; return span <= 0 ? 0 : Math.Max(0, Math.Min(1, (Clock - StartTime).TotalSeconds / span)); } }
        List<DateTime> days = new List<DateTime>();
        public int DaysTotal { get { return days.Count; } }
        public int DayNumber { get { var d = Clock.Date; return days.Count(x => x <= d); } }

        public string Load(SimSettings s, SimSpec spec, List<Mt5Bar> m1) { return Load(s, new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> { Tuple.Create(spec, m1, (List<SimTick>)null) }); }
        public string Load(SimSettings s, List<Tuple<SimSpec, List<Mt5Bar>>> input) { return Load(s, input.Select(x => Tuple.Create(x.Item1, x.Item2, (List<SimTick>)null)).ToList()); }
        // load a backtest: the warm-up days are drawn at once, the rest is replayed from the start time
        public string Load(SimSettings s, List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> input)
        {
            Set = s; data = input; Ended = false; Playing = false; feeds.Clear(); ClosedDays.Clear(); if (input.Count == 0) return "no symbol";
            var spec0 = input[0].Item1; var start = s.From.Date; if (!double.IsNaN(s.StartNyHour)) start = start.AddHours(s.StartNyHour - spec0.NyOffsetHours);
            foreach (var d in input)
            {
                var bars = d.Item2.OrderBy(b => b.Time).Select(b => b.Copy()).ToList(); int st = bars.FindIndex(b => b.Time >= start);
                if (st < 0) continue; var ticks = d.Item3 != null && d.Item3.Count > 0 ? d.Item3.OrderBy(x => x.T).ToList() : null;
                feeds.Add(new Feed { Spec = d.Item1, Bars = bars, Start = st, Cur = st, Ticks = ticks });
            }
            if (feeds.Count == 0) return "no candles from " + s.From.ToString("dd MMM yyyy", IC) + " in the loaded history";
            StartTime = feeds.Min(f => f.Bars[f.Start].Time); EndTime = feeds.Max(f => f.Bars[f.Bars.Count - 1].Time.AddMinutes(1)); Clock = StartTime;
            days = feeds.SelectMany(f => f.Bars.Skip(f.Start).Select(b => b.Time.Date)).Distinct().OrderBy(x => x).ToList();
            lock (state.Gate)
            {
                state.Symbols.Clear(); state.Quotes.Clear(); state.Series.Clear(); state.Results.Clear(); acct.Specs.Clear();
                state.BridgeVersion = 5; state.AllowInput = state.AlgoTerminal = state.AlgoEa = state.AccountTrade = state.AccountExpert = true; state.CanKnown = true; state.MaxLots = 100;
                state.Company = "SpasiX BACKTEST"; state.ServerOffsetSec = spec0.ServerOffsetSec; state.Linked = true; state.LastLine = DateTime.Now; state.LinkedAt = DateTime.Now;
                acct.Reset(s.Account(), StartTime); state.Account.Kind = "BACKTEST"; state.Account.Name = "BACKTEST";
                foreach (var f in feeds)
                {
                    var sp = f.Spec; acct.Specs[sp.Name] = sp;
                    state.Symbols[sp.Name] = new Mt5Symbol { Name = sp.Name, Description = sp.Description, Digits = sp.Digits, Point = sp.Point, Contract = sp.Contract, TickValue = sp.TickValue, TickSize = sp.TickSize, VolMin = sp.VolMin, VolStep = sp.VolStep, VolMax = sp.VolMax, SwapLong = sp.SwapLong, SwapShort = sp.SwapShort, SwapMode = sp.SwapMode, Swap3Day = (int)sp.Swap3Day };
                    var warm = f.Bars.Take(f.Start).ToList();
                    foreach (var m in Tf.All) { var se = state.SeriesOf(sp.Name, m); se.Bars.Clear(); se.Bars.AddRange(BarSeries.Fold(warm, m)); se.Loaded = true; se.LoadedAt = DateTime.Now; se.Version++; }
                    var first = f.Bars[f.Start]; double px = f.Start > 0 ? f.Bars[f.Start - 1].C : first.O; Quote(f, first.Time, px, first.Spread);
                }
                state.ServerNow = Clock;
            }
            return "";
        }
        public string Restart() { return Load(Set, data); }

        // ---- the clock
        public void Advance(double realSeconds)
        {
            lock (state.Gate)
            {
                state.LastLine = DateTime.Now;
                if (!Playing || Ended) return;
                if (Speed >= MaxSpeed) RunUntil(DateTime.MaxValue, 12000);
                else RunUntil(Clock.AddSeconds(Math.Min(realSeconds, 0.5) * Speed), 60000);
            }
        }
        public void StepBar(int minutes) { lock (state.Gate) { var next = Tf.Start(Clock, minutes).AddMinutes(minutes); RunUntil(NextBarTimeAtOrAfter(next), int.MaxValue); } }
        public void JumpTo(DateTime t) { lock (state.Gate) { if (t > Clock) RunUntil(NextBarTimeAtOrAfter(t), int.MaxValue); } }
        DateTime NextBarTimeAtOrAfter(DateTime t)
        {
            var best = EndTime; foreach (var f in feeds) for (int i = f.Cur; i < f.Bars.Count; i++) if (f.Bars[i].Time >= t) { if (f.Bars[i].Time < best) best = f.Bars[i].Time; break; }
            return best;
        }
        public DateTime NextDayStart() { return NextBarTimeAtOrAfter(Clock.Date.AddDays(1)); }
        public DateTime NextNy(double nyHour) { var t = Clock.Date.AddHours(nyHour - Spec.NyOffsetHours); while (t <= Clock.AddSeconds(30)) t = t.AddDays(1); return NextBarTimeAtOrAfter(t); }

        void RunUntil(DateTime target, int budget)
        {
            while (budget > 0)
            {
                Feed f = null; var t = DateTime.MaxValue; foreach (var x in feeds) { var nx = x.Next; if (nx < t) { t = nx; f = x; } }
                if (f == null) { End(); return; }
                if (t > target) { if (target > Clock && target < DateTime.MaxValue) Clock = target; state.ServerNow = Clock; return; }
                if (f.Path == null) BeginBar(f);
                var b = f.Bars[f.Cur]; var tick = f.Path[f.PathPos];
                Tick(f, b.Time.AddTicks((long)tick[0]), tick[1], tick[2], b.Spread, f.PathPos == 0 ? f.VolPerTick + (b.V - f.VolPerTick * f.Path.Count) : f.VolPerTick);
                f.PathPos++; budget--;
                if (f.PathPos >= f.Path.Count) { f.Path = null; f.PathPos = 0; f.Cur++; }
            }
        }
        void End() { if (Ended) return; Ended = true; Playing = false; Clock = EndTime; state.ServerNow = Clock; ClosedDays.Add(Clock.AddMinutes(-1).Date); acct.Notes.Add("the end of the loaded history — the STATS and POOL tabs show the results"); }
        // the prices inside one minute: MT5's REAL TICKS of that minute when they are on this PC; otherwise a path open → (low, high) or
        // (high, low) → close in steps proportional to the distance (the candle stays exactly MT5's, the way inside it is not known)
        void BeginBar(Feed f)
        {
            var b = f.Bars[f.Cur]; var path = new List<double[]>();
            if (f.Ticks != null)
            {
                var end = b.Time.AddMinutes(1);
                while (f.TickIdx < f.Ticks.Count && f.Ticks[f.TickIdx].T < b.Time) f.TickIdx++;
                for (int i = f.TickIdx; i < f.Ticks.Count && f.Ticks[i].T < end; i++) path.Add(new[] { (double)Math.Max(0, (f.Ticks[i].T - b.Time).Ticks), f.Ticks[i].Bid, f.Ticks[i].Ask });
            }
            if (path.Count > 0) f.RealBars++;
            else
            {
                f.MadeBars++; int k = Speed <= 15 ? TicksPerBar : Speed <= 200 ? 8 : 4;
                var pts = b.C >= b.O ? new[] { b.O, b.L, b.H, b.C } : new[] { b.O, b.H, b.L, b.C };
                double total = Math.Abs(pts[1] - pts[0]) + Math.Abs(pts[2] - pts[1]) + Math.Abs(pts[3] - pts[2]);
                var prices = new List<double> { pts[0] };
                for (int leg = 0; leg < 3; leg++)
                {
                    double len = Math.Abs(pts[leg + 1] - pts[leg]); int steps = total <= 0 ? (leg == 2 ? 1 : 0) : Math.Max(len > 0 ? 1 : 0, (int)Math.Round((k - 1) * len / total));
                    for (int j = 1; j <= steps; j++) prices.Add(Round(f.Spec, pts[leg] + (pts[leg + 1] - pts[leg]) * j / steps));
                }
                if (prices[prices.Count - 1] != b.C) prices.Add(b.C);
                for (int i = 0; i < prices.Count; i++) path.Add(new[] { prices.Count == 1 ? 0 : 59.0 * TimeSpan.TicksPerSecond * i / (prices.Count - 1), prices[i], double.NaN });
            }
            f.Path = path; f.PathPos = 0; f.VolPerTick = Math.Max(0, b.V / Math.Max(1, path.Count));
        }
        static double Round(SimSpec s, double p) { return s.Point > 0 ? Math.Round(p / s.Point) * s.Point : p; }
        // one price: the spread of the candle (or the fixed one) + STANDARD's markup
        void Quote(Feed f, DateTime t, double bid, int spreadPts, double realAsk = double.NaN)
        {
            double mk = (Set.Kind == "STANDARD" ? Set.MarkupPts : 0) * f.Spec.Point;
            double ask = Set.FixedSpread >= 0 ? Round(f.Spec, bid + (Set.FixedSpread * f.Spec.Point) + mk) : !double.IsNaN(realAsk) && realAsk >= bid ? Round(f.Spec, realAsk + mk) : Round(f.Spec, bid + Math.Max(0, spreadPts) * f.Spec.Point + mk);
            state.Quotes[f.Spec.Name] = new Mt5Quote { Symbol = f.Spec.Name, Bid = bid, Ask = ask, Time = t };
            acct.OnPrice(f.Spec.Name, t, bid, ask);
        }
        void Tick(Feed f, DateTime t, double price, double ask, int spreadPts, long vol)
        {
            if (Clock > DateTime.MinValue && t.Date > Clock.Date) ClosedDays.Add(Clock.Date);
            if (t > Clock) Clock = t; state.ServerNow = Clock;
            foreach (var m in Tf.All) state.SeriesOf(f.Spec.Name, m).OnTick(t, price, vol);
            Quote(f, t, price, spreadPts, ask);
        }

        // ---- the commands (the same as the bridge's)
        public bool Send(string line) { return true; }   // candles / symbols requests: the backtest has everything already
        public string Command(string cmd, params object[] a)
        {
            string id = "S" + (++cmdSeq);
            lock (state.Gate)
            {
                Tuple<bool, string> r;
                try { r = Ended && cmd == "ORDER" ? Tuple.Create(false, "the backtest has ended — restart it") : acct.Do(cmd, a, Clock); }
                catch (Exception ex) { r = Tuple.Create(false, cmd + ": " + ex.Message); }
                state.AddResult(id, r.Item1, r.Item2);
            }
            return id;
        }
    }
}
