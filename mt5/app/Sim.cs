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
        // a day is "here" when its candles are saved — or, for a weekday, when MT5 confirmed it has none (before its history starts, a holiday)
        public static bool HasDay(string sym, DateTime day)
        {
            var f = FileOf(sym, day); bool wk = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            try { if (File.Exists(f) && (wk || new FileInfo(f).Length > 10)) return true; } catch { }
            return File.Exists(NoneFile(sym, day));
        }
        static string NoneFile(string sym, DateTime day) { return Path.Combine(DirOf(sym), day.ToString("yyyyMMdd", IC) + ".none"); }
        public static void MarkNone(string sym, DateTime day) { try { Directory.CreateDirectory(DirOf(sym)); File.WriteAllText(NoneFile(sym, day), "MT5 has no 1-minute candles for this day"); } catch { } }
        public static bool IsNone(string sym, DateTime day) { return File.Exists(NoneFile(sym, day)); }
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
        // FAST: the 1-minute candles come a WEEK per request (split into days and saved), then MT5's real ticks day by day (only for the days
        // asked, tickFrom..tickTo) — the app can start the backtest when the candles and the first tick day are in and keep downloading the rest
        readonly IBroker live; readonly string sym; readonly DateTime tickFrom, tickTo; readonly bool wantTicks;
        readonly List<Tuple<DateTime, DateTime>> chunks = new List<Tuple<DateTime, DateTime>>(); readonly List<DateTime> tickDays = new List<DateTime>();
        int ci, ti, tries, timeouts; bool tickPlan; readonly DateTime mainFrom; DateTime askedAt = DateTime.MinValue, retryAt = DateTime.MinValue; bool waiting;
        public string Status = ""; public bool Done, Failed; public int Saved, Skipped, TickDays, NoHistory, NotDelivered; public long TickCount; public DateTime FirstDate = DateTime.MinValue;
        public readonly List<DateTime> NewTickDays = new List<DateTime>();   // the days whose ticks just arrived (the app takes them)
        public string Symbol { get { return sym; } }
        public bool BarsDone { get { return ci >= chunks.Count; } }
        public int TickDaysTotal { get { return tickDays.Count; } } public int TickDaysDone { get { return ti; } }
        public double Progress { get { double a = chunks.Count == 0 ? 1 : (double)ci / chunks.Count; if (!wantTicks) return a; double b = !tickPlan ? 0 : tickDays.Count == 0 ? 1 : (double)ti / tickDays.Count; return tickPlan && tickDays.Count == 0 ? a : 0.2 * a + 0.8 * b; } }
        public HistoryDownloader(IBroker live, string sym, DateTime from, DateTime to, DateTime ticksFrom, bool ticks = true) : this(live, sym, from, to, ticksFrom, to, ticks) { }
        public HistoryDownloader(IBroker live, string sym, DateTime from, DateTime to, DateTime ticksFrom, DateTime ticksTo, bool ticks) : this(live, sym, from, to, ticksFrom, ticksTo, ticks, DateTime.MinValue) { }
        // mainFrom: the first day the user asked for — those days come first, the warm-up days before it last (shown as "warm-up")
        public HistoryDownloader(IBroker live, string sym, DateTime from, DateTime to, DateTime ticksFrom, DateTime ticksTo, bool ticks, DateTime mainFrom)
        {
            this.mainFrom = mainFrom.Date;
            this.live = live; this.sym = sym; tickFrom = ticksFrom.Date; tickTo = ticksTo.Date; wantTicks = ticks; var today = DateTime.Today;
            DateTime first; lock (live.State.Gate) if (live.State.FirstDate.TryGetValue(sym, out first)) FirstDate = first;
            var need = new List<DateTime>();
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                if (HistoryStore.HasDay(sym, d) && d < today.AddDays(-1)) continue;   // today / yesterday: always fresh
                if (FirstDate > DateTime.MinValue && d.AddDays(1) <= FirstDate.Date) { if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) { HistoryStore.MarkNone(sym, d); NoHistory++; } continue; }   // before MT5's history starts
                need.Add(d);
            }
            // consecutive missing days → runs of up to 7 days = one request each
            Action<List<DateTime>> runs = ds => { for (int i = 0; i < ds.Count;) { int j = i; while (j + 1 < ds.Count && ds[j + 1] == ds[j].AddDays(1) && j + 1 - i < 7) j++; chunks.Add(Tuple.Create(ds[i], ds[j].AddDays(1))); i = j + 1; } };
            runs(need.Where(d => d >= this.mainFrom).ToList()); runs(need.Where(d => d < this.mainFrom).ToList());   // the asked days first, the warm-up last
            if (chunks.Count == 0) PlanTicks();
        }
        public HistoryDownloader(IBroker live, string sym, DateTime from, DateTime to) : this(live, sym, from, to, to.AddDays(1), false) { }
        void PlanTicks()
        {
            tickPlan = true; if (!wantTicks) return;
            for (var d = tickFrom; d <= tickTo; d = d.AddDays(1)) if ((!HistoryStore.HasTicks(sym, d) || d >= DateTime.Today.AddDays(-1)) && HistoryStore.LoadDay(sym, d).Count > 0) tickDays.Add(d);
        }
        // call every ~100 ms; true while it works
        public bool Step(DateTime now)
        {
            if (Done) return false;
            if (ci < chunks.Count)
            {
                var c = chunks[ci]; long from = Mt5State.Epoch(c.Item1), to = Mt5State.Epoch(c.Item2); string key = sym + "|" + from;
                string dn = c.Item1.ToString("dd MMM", CultureInfo.InvariantCulture) + (c.Item2.AddDays(-1) > c.Item1 ? " – " + c.Item2.AddDays(-1).ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : " " + c.Item1.Year);
                bool warm = c.Item1 < mainFrom; if (warm) dn += " (warm-up before " + mainFrom.ToString("dd MMM", CultureInfo.InvariantCulture) + ")";
                bool weekdays = false; for (var d = c.Item1; d < c.Item2; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) weekdays = true;
                Tuple<List<Mt5Bar>, int> got = null;
                lock (live.State.Gate) { if (live.State.Hist.TryGetValue(key, out got)) live.State.Hist.Remove(key); }
                if (got != null)
                {
                    waiting = false;
                    if ((got.Item2 != 0 || (got.Item1.Count == 0 && weekdays)) && tries < (warm ? 2 : 4)) { tries++; retryAt = now.AddSeconds(1.5 * tries); Status = "MT5 is loading " + sym + " " + dn + " from the broker … (try " + tries + ")"; return true; }
                    var byDay = got.Item1.GroupBy(x => x.Time.Date).ToDictionary(g => g.Key, g => g.ToList());
                    for (var d = c.Item1; d < c.Item2; d = d.AddDays(1))
                    {
                        List<Mt5Bar> bars; if (!byDay.TryGetValue(d, out bars)) bars = new List<Mt5Bar>(); bool wk = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                        if (bars.Count > 0) { HistoryStore.SaveDay(sym, d, bars); Saved++; }
                        else if (wk) { if (d < DateTime.Today) HistoryStore.SaveDay(sym, d, bars); Skipped++; }
                        else if (got.Item2 == 0 && d < DateTime.Today.AddDays(-1)) { HistoryStore.MarkNone(sym, d); NoHistory++; }   // MT5 answered: no candles that day
                        else NotDelivered++;   // MT5 was still loading — nothing saved, the next START asks again
                    }
                    ci++; tries = 0; timeouts = 0; retryAt = DateTime.MinValue; if (ci >= chunks.Count) PlanTicks(); return true;
                }
                return Ask(now, "HIST|" + sym + "|" + from + "|" + to, "⬇ " + sym + " candles " + dn + " (" + (ci + 1) + " / " + chunks.Count + ")", 15);
            }
            if (!tickPlan) PlanTicks();
            if (ti >= tickDays.Count)
            {
                Done = true; var ic = CultureInfo.InvariantCulture;
                Status = "✓ " + sym + ": " + Saved + " day(s) of candles" + (TickDays > 0 ? " + " + TickDays + " day(s) of real ticks (" + TickCount.ToString("N0", ic) + ")" : "") + " downloaded" + (Saved + TickDays == 0 && NoHistory + NotDelivered == 0 ? " — everything was already on this PC" : "")
                    + (NoHistory > 0 ? " • " + NoHistory + " weekday(s) are not in MT5's history" + (FirstDate > DateTime.MinValue ? " (it starts " + FirstDate.ToString("dd MMM yyyy", ic) + ")" : "") : "")
                    + (NotDelivered > 0 ? " • " + NotDelivered + " day(s) MT5 was still loading — press ▶ START again in a minute" : "");
                return false;
            }
            var day = tickDays[ti]; long f2 = Mt5State.Epoch(day), t2 = Mt5State.Epoch(day.AddDays(1)); string k2 = sym + "|" + f2; string dn2 = day.ToString("ddd dd MMM", CultureInfo.InvariantCulture);
            Tuple<List<SimTick>, int> gt = null;
            lock (live.State.Gate) { if (live.State.TickHist.TryGetValue(k2, out gt)) live.State.TickHist.Remove(k2); }
            if (gt != null)
            {
                waiting = false;
                if ((gt.Item2 != 0 || gt.Item1.Count == 0) && tries < 3) { tries++; retryAt = now.AddSeconds(2.0 * tries); Status = "MT5 is loading the real ticks of " + sym + " " + dn2 + " … (try " + tries + ")"; return true; }
                if (gt.Item1.Count > 0) { HistoryStore.SaveTicks(sym, day, gt.Item1); TickDays++; TickCount += gt.Item1.Count; NewTickDays.Add(day); }
                NextTick(); return true;
            }
            return Ask(now, "HISTT|" + sym + "|" + f2 + "|" + t2, "⬇ " + sym + " real ticks " + dn2 + " (" + (ti + 1) + " / " + tickDays.Count + ")", 45);
        }
        void NextTick() { ti++; tries = 0; timeouts = 0; retryAt = DateTime.MinValue; waiting = false; }
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
                if (++timeouts > 3) { if (ci >= chunks.Count) { NextTick(); return true; } Failed = true; Done = true; Status = "✖ MT5 does not answer — put the new SpasiXBridge v5 on the chart (⚙ SETUP)"; return false; }
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
        public List<DateTime> Days { get { return days.ToList(); } }   // the replayed server days
        public int DayNumber { get { var d = Clock.Date; return days.Count(x => x <= d); } }

        public string Load(SimSettings s, SimSpec spec, List<Mt5Bar> m1) { return Load(s, new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> { Tuple.Create(spec, m1, (List<SimTick>)null) }); }
        public string Load(SimSettings s, List<Tuple<SimSpec, List<Mt5Bar>>> input) { return Load(s, input.Select(x => Tuple.Create(x.Item1, x.Item2, (List<SimTick>)null)).ToList()); }
        // load a backtest: the warm-up days are drawn at once, the rest is replayed from the start time
        public string Load(SimSettings s, List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> input)
        {
            if (!ReferenceEquals(input, data)) cmdLog.Clear(); Set = s; data = input; Ended = false; Playing = false; feeds.Clear(); ClosedDays.Clear(); if (input.Count == 0) return "no symbol";
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
        public string Restart() { cmdLog.Clear(); return Load(Set, data); }
        // real ticks that arrived after the start (downloaded in the background): appended to the symbol's feed (and to the reload data)
        public int AddTicks(string sym, List<SimTick> ticks)
        {
            if (ticks == null || ticks.Count == 0) return 0; int n = 0;
            lock (state.Gate)
            {
                foreach (var f in feeds.Where(x => string.Equals(x.Spec.Name, sym, StringComparison.OrdinalIgnoreCase)))
                {
                    if (f.Ticks == null) f.Ticks = new List<SimTick>(); var last = f.Ticks.Count > 0 ? f.Ticks[f.Ticks.Count - 1].T : DateTime.MinValue;
                    var add = ticks.Where(t => t.T > last).OrderBy(t => t.T).ToList(); f.Ticks.AddRange(add); n += add.Count;
                }
                if (data != null) for (int i = 0; i < data.Count; i++) if (string.Equals(data[i].Item1.Name, sym, StringComparison.OrdinalIgnoreCase))
                {
                    var l = data[i].Item3 ?? new List<SimTick>(); var last = l.Count > 0 ? l[l.Count - 1].T : DateTime.MinValue; l.AddRange(ticks.Where(t => t.T > last).OrderBy(t => t.T)); data[i] = Tuple.Create(data[i].Item1, data[i].Item2, l);
                }
            }
            return n;
        }
        // BACK in time (previous minute / candle / day, a date in the past): the replay restarts and runs again to that moment, your commands
        // re-done at their own times — the trades before it stay exactly as they were, the ones after it are gone
        readonly List<Tuple<DateTime, string, object[]>> cmdLog = new List<Tuple<DateTime, string, object[]>>();
        public int CommandsAfter(DateTime t) { return cmdLog.Count(c => c.Item1 > t); }
        public string RewindTo(DateTime t)
        {
            if (data == null) return "nothing loaded"; if (t >= Clock) return "";
            var keep = cmdLog.Where(c => c.Item1 <= t).ToList(); var err = Load(Set, data); if (err != "") return err; cmdLog.Clear();
            lock (state.Gate)
            {
                foreach (var c in keep) { RunUntil(c.Item1, int.MaxValue); try { var r = acct.Do(c.Item2, c.Item3, Clock); if (r.Item1) cmdLog.Add(c); } catch { } }
                if (t > Clock) RunUntil(t, int.MaxValue);
                state.Results.Clear(); state.ResultsDropped = 0;
            }
            Playing = false; return "";
        }
        // the first candle of any symbol at or after t (from the start of the replay)
        public DateTime FirstBarAtOrAfter(DateTime t) { var best = EndTime; foreach (var f in feeds) for (int i = f.Start; i < f.Bars.Count; i++) if (f.Bars[i].Time >= t) { if (f.Bars[i].Time < best) best = f.Bars[i].Time; break; } return best; }
        public DateTime PrevBar(int minutes) { var s0 = Tf.Start(Clock, minutes); var t = (Clock - s0).TotalSeconds < 1 ? s0.AddMinutes(-minutes) : s0; return t < StartTime ? StartTime : t; }
        public DateTime PrevDayStart() { var today = FirstBarAtOrAfter(Clock.Date); var d = (Clock - today).TotalMinutes > 2 ? today : FirstBarAtOrAfter(Clock.Date.AddDays(-1)); if (d >= Clock) { var day = Clock.Date.AddDays(-1); while (day >= StartTime.Date && (d = FirstBarAtOrAfter(day)) >= Clock) day = day.AddDays(-1); } return d < StartTime || d >= Clock ? StartTime : d; }

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
        // one price: the spread of the candle (or the fixed one) + RAW's markup
        void Quote(Feed f, DateTime t, double bid, int spreadPts, double realAsk = double.NaN)
        {
            double mk = (Set.Kind == "RAW" ? Set.MarkupPts : 0) * f.Spec.Point;
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
                try { r = Ended && cmd == "ORDER" ? Tuple.Create(false, "the backtest has ended — restart it") : acct.Do(cmd, a, Clock); if (r.Item1) cmdLog.Add(Tuple.Create(Clock, cmd, a)); }
                catch (Exception ex) { r = Tuple.Create(false, cmd + ": " + ex.Message); }
                state.AddResult(id, r.Item1, r.Item2);
            }
            return id;
        }
    }
}
