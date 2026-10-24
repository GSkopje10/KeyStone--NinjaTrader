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
        public static SimSpec From(Mt5Symbol s, int serverOffsetSec, int nyOff, string cur)
        {
            return new SimSpec { Name = s.Name, Description = s.Description, Digits = s.Digits, Point = s.Point > 0 ? s.Point : 0.01, Contract = s.Contract > 0 ? s.Contract : 1, TickValue = s.TickValue > 0 ? s.TickValue : 1, TickSize = s.TickSize > 0 ? s.TickSize : (s.Point > 0 ? s.Point : 0.01), VolMin = s.VolMin > 0 ? s.VolMin : 0.01, VolStep = s.VolStep > 0 ? s.VolStep : 0.01, VolMax = s.VolMax > 0 ? s.VolMax : 100, ServerOffsetSec = serverOffsetSec, NyOffsetHours = nyOff, Currency = cur ?? "USD" };
        }
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
                "tickvalue=" + s.TickValue.ToString("R", IC), "ticksize=" + s.TickSize.ToString("R", IC), "volmin=" + s.VolMin.ToString("R", IC), "volstep=" + s.VolStep.ToString("R", IC), "volmax=" + s.VolMax.ToString("R", IC), "serveroffset=" + s.ServerOffsetSec.ToString(IC), "nyoffset=" + s.NyOffsetHours.ToString(IC) });
        }
        public static SimSpec LoadSpec(string sym)
        {
            var f = Path.Combine(DirOf(sym), "spec.txt"); if (!File.Exists(f)) return null; var d = new Dictionary<string, string>();
            foreach (var ln in File.ReadAllLines(f)) { int i = ln.IndexOf('='); if (i > 0) d[ln.Substring(0, i)] = ln.Substring(i + 1); }
            Func<string, double, double> n = (k, def) => { string v; double x; return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, IC, out x) ? x : def; };
            string nm; d.TryGetValue("name", out nm); string ds; d.TryGetValue("description", out ds); string cu; d.TryGetValue("currency", out cu);
            return new SimSpec { Name = nm ?? sym, Description = ds ?? "", Currency = string.IsNullOrEmpty(cu) ? "USD" : cu, Digits = (int)n("digits", 2), Point = n("point", 0.01), Contract = n("contract", 1), TickValue = n("tickvalue", 1), TickSize = n("ticksize", 0.01), VolMin = n("volmin", 0.01), VolStep = n("volstep", 0.01), VolMax = n("volmax", 100), ServerOffsetSec = (int)n("serveroffset", 10800), NyOffsetHours = (int)n("nyoffset", -7) };
        }
    }

    // downloads missing days from MT5 through the bridge, one day at a time (HIST|symbol|from|to → HIST … HISTEND), and saves them
    public sealed class HistoryDownloader
    {
        readonly IBroker live; readonly string sym; readonly List<DateTime> days; int idx, tries, timeouts; DateTime askedAt = DateTime.MinValue, retryAt = DateTime.MinValue; bool waiting;
        public string Status = ""; public bool Done, Failed; public int Saved, Skipped;
        public double Progress { get { return days.Count == 0 ? 1 : (double)idx / days.Count; } }
        public HistoryDownloader(IBroker live, string sym, DateTime from, DateTime to)
        {
            this.live = live; this.sym = sym; days = new List<DateTime>(); var today = DateTime.Today;
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) if (!HistoryStore.HasDay(sym, d) || d >= today.AddDays(-1)) days.Add(d);   // today / yesterday: always fresh
            if (days.Count == 0) { Done = true; Status = "✓ every day is already on this PC"; }
        }
        // call every ~100 ms; true while it works
        public bool Step(DateTime now)
        {
            if (Done) return false;
            if (idx >= days.Count) { Done = true; Status = "✓ " + Saved + " day(s) downloaded" + (Skipped > 0 ? " • " + Skipped + " without candles (weekend / holiday)" : ""); return false; }
            var day = days[idx]; long from = Mt5State.Epoch(day), to = Mt5State.Epoch(day.AddDays(1)); string key = sym + "|" + from;
            Tuple<List<Mt5Bar>, int> got = null;
            lock (live.State.Gate) { if (live.State.Hist.TryGetValue(key, out got)) live.State.Hist.Remove(key); }
            if (got != null)
            {
                waiting = false; bool weekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
                if ((got.Item2 != 0 || (got.Item1.Count == 0 && !weekend)) && tries < 4) { tries++; retryAt = now.AddSeconds(1.5 * tries); Status = "MT5 is loading " + day.ToString("ddd dd MMM", CultureInfo.InvariantCulture) + " from the broker … (try " + tries + ")"; return true; }
                if (day < DateTime.Today || got.Item1.Count > 0) HistoryStore.SaveDay(sym, day, got.Item1);
                if (got.Item1.Count > 0) Saved++; else Skipped++;
                idx++; tries = 0; timeouts = 0; retryAt = DateTime.MinValue; return true;
            }
            if (!waiting)
            {
                if (now < retryAt) return true;
                if (!live.Send("HIST|" + sym + "|" + from + "|" + to)) { Failed = true; Done = true; Status = "✖ MT5 is not connected — start MT5 with the SpasiXBridge (the days already on this PC still work)"; return false; }
                waiting = true; askedAt = now; Status = "downloading " + sym + " " + day.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture) + " (" + (idx + 1) + " / " + days.Count + ")"; return true;
            }
            if ((now - askedAt).TotalSeconds > 8) { if (++timeouts > 3) { Failed = true; Done = true; Status = "✖ MT5 does not answer — put the new SpasiXBridge v4 on the chart (⚙ SETUP)"; return false; } waiting = false; }
            return true;
        }
    }

    public sealed class SimSettings
    {
        public string Symbol = ""; public DateTime From, To; public int WarmDays = 3; public double StartNyHour = double.NaN;   // NaN = the server's midnight; NY hours otherwise (−6.5 = 17:30 the day before)
        public double Balance = 10000, CommissionPerLot = 7, SwapLong = 0, SwapShort = 0, StopOutPct = 50; public int Leverage = 500, FixedSpread = -1, SlipPts = 0; public string Currency = "USD";
    }

    // the simulated broker
    public sealed class SimBroker : IBroker
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        readonly Mt5State state = new Mt5State();
        public Mt5State State { get { return state; } } public bool IsSim { get { return true; } } public string Problem { get { return ""; } } public int Port { get { return 0; } }
        public SimSettings Set { get; private set; } public SimSpec Spec { get; private set; }
        List<Mt5Bar> bars = new List<Mt5Bar>(); int startIdx, cur; List<Tuple<double, double>> path; int pathPos; long volPerTick;
        public DateTime Clock { get; private set; } public DateTime StartTime { get; private set; } public DateTime EndTime { get; private set; }
        public bool Playing; public double Speed = 1; public bool Ended { get; private set; }
        public const double MaxSpeed = 100000;
        long nextTicket = 50000001, nextDeal = 70000001; int cmdSeq; double balance;
        public readonly List<string> Notes = new List<string>();   // stop-outs, the end of the data …
        public int TicksPerBar = 16;

        public double Progress { get { int n = bars.Count - startIdx; return n <= 0 ? 0 : Math.Max(0, Math.Min(1, (cur - startIdx + (path != null && path.Count > 0 ? (double)pathPos / path.Count : 0)) / n)); } }
        public int DaysTotal { get { return bars.Skip(startIdx).Select(b => b.Time.Date).Distinct().Count(); } }
        public int DayNumber { get { var d = Clock.Date; return bars.Skip(startIdx).Select(b => b.Time.Date).Distinct().Count(x => x <= d); } }

        // load a backtest: the warm-up days are drawn at once, the rest is replayed from the start time
        public string Load(SimSettings s, SimSpec spec, List<Mt5Bar> m1)
        {
            Set = s; Spec = spec; bars = m1.OrderBy(b => b.Time).Select(b => b.Copy()).ToList(); Ended = false; Playing = false; Notes.Clear(); path = null; pathPos = 0;
            var start = s.From.Date; if (!double.IsNaN(s.StartNyHour)) start = start.AddHours(s.StartNyHour - spec.NyOffsetHours);
            startIdx = bars.FindIndex(b => b.Time >= start); if (startIdx < 0) return "no candles from " + s.From.ToString("dd MMM yyyy", IC) + " in the loaded history";
            if (startIdx == 0 && bars.Count < 2) return "too few candles";
            cur = startIdx; StartTime = bars[startIdx].Time; EndTime = bars[bars.Count - 1].Time.AddMinutes(1); Clock = StartTime;
            lock (state.Gate)
            {
                state.Symbols.Clear(); state.Quotes.Clear(); state.Series.Clear(); state.Positions = new List<Mt5Position>(); state.Orders = new List<Mt5Order>(); state.Deals = new List<Mt5Deal>(); state.Results.Clear();
                state.Symbols[spec.Name] = new Mt5Symbol { Name = spec.Name, Description = spec.Description, Digits = spec.Digits, Point = spec.Point, Contract = spec.Contract, TickValue = spec.TickValue, TickSize = spec.TickSize, VolMin = spec.VolMin, VolStep = spec.VolStep, VolMax = spec.VolMax };
                state.BridgeVersion = 4; state.AllowInput = state.AlgoTerminal = state.AlgoEa = state.AccountTrade = state.AccountExpert = true; state.CanKnown = true; state.MaxLots = Math.Max(1, spec.VolMax);
                state.Company = "SpasiX BACKTEST"; state.ServerOffsetSec = spec.ServerOffsetSec; state.Linked = true; state.LastLine = DateTime.Now; state.LinkedAt = DateTime.Now;
                balance = s.Balance;
                state.Account = new Mt5Account { Login = 0, Name = "BACKTEST", Server = "SpasiX simulator", Currency = s.Currency, Balance = balance, Equity = balance, FreeMargin = balance, Leverage = s.Leverage, Mode = "HEDGE", Connected = true, Kind = "BACKTEST" };
                var warm = bars.Take(startIdx).ToList();
                foreach (var m in Tf.All) { var se = state.SeriesOf(spec.Name, m); se.Bars.Clear(); se.Bars.AddRange(BarSeries.Fold(warm, m)); se.Loaded = true; se.LoadedAt = DateTime.Now; se.Version++; }
                var first = bars[startIdx]; double px = startIdx > 0 ? bars[startIdx - 1].C : first.O; Quote(first.Time, px, first.Spread);
                state.Deals.Add(new Mt5Deal { Ticket = nextDeal++, Time = StartTime, Type = "BALANCE", Profit = balance, Comment = "start balance" }); state.DealsVersion++; state.DealsAt = DateTime.Now; state.DealsFrom = StartTime.Date;
                state.ServerNow = Clock;
            }
            return "";
        }
        public string Restart() { var m1 = bars; return Load(Set, Spec, m1); }

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
        // one candle of this timeframe further (the next one opens)
        public void StepBar(int minutes) { lock (state.Gate) { var next = Tf.Start(Clock, minutes).AddMinutes(minutes); if (minutes >= 1440) next = NextBarTimeAtOrAfter(next); RunUntil(next, int.MaxValue); } }
        public void JumpTo(DateTime t) { lock (state.Gate) { if (t > Clock) RunUntil(NextBarTimeAtOrAfter(t), int.MaxValue); } }
        DateTime NextBarTimeAtOrAfter(DateTime t) { for (int i = cur; i < bars.Count; i++) if (bars[i].Time >= t) return bars[i].Time; return EndTime; }
        public DateTime NextDayStart() { var d = Clock.Date.AddDays(1); return NextBarTimeAtOrAfter(d); }
        // the next time the New York clock shows this hour (e.g. 9.5 = 09:30)
        public DateTime NextNy(double nyHour) { var t = Clock.Date.AddHours(nyHour - Spec.NyOffsetHours); while (t <= Clock.AddSeconds(30)) t = t.AddDays(1); return NextBarTimeAtOrAfter(t); }

        void RunUntil(DateTime target, int budget)
        {
            while (budget > 0)
            {
                if (cur >= bars.Count) { End(); return; }
                var b = bars[cur]; if (path == null) { if (b.Time > target) { Clock = target > Clock ? target : Clock; state.ServerNow = Clock; return; } BeginBar(b); }
                while (pathPos < path.Count && budget > 0)
                {
                    var t = b.Time.AddSeconds(path[pathPos].Item1); if (t > target) { if (target > Clock) Clock = target; state.ServerNow = Clock; return; }
                    Tick(t, path[pathPos].Item2, b.Spread, pathPos == 0 ? volPerTick + (b.V - volPerTick * path.Count) : volPerTick); pathPos++; budget--;
                }
                if (pathPos >= path.Count) { path = null; pathPos = 0; cur++; }
            }
        }
        void End() { if (Ended) return; Ended = true; Playing = false; Clock = EndTime; state.ServerNow = Clock; Notes.Add("the end of the loaded history — the STATS tab shows the results"); }
        // the price path inside one candle: open → (low, high) or (high, low) → close, in steps proportional to the distance
        void BeginBar(Mt5Bar b)
        {
            int k = Speed <= 15 ? TicksPerBar : Speed <= 200 ? 8 : 4; path = new List<Tuple<double, double>>(); pathPos = 0;
            var pts = b.C >= b.O ? new[] { b.O, b.L, b.H, b.C } : new[] { b.O, b.H, b.L, b.C };
            double total = Math.Abs(pts[1] - pts[0]) + Math.Abs(pts[2] - pts[1]) + Math.Abs(pts[3] - pts[2]);
            var prices = new List<double> { pts[0] };
            for (int leg = 0; leg < 3; leg++)
            {
                double len = Math.Abs(pts[leg + 1] - pts[leg]); int steps = total <= 0 ? (leg == 2 ? 1 : 0) : Math.Max(len > 0 ? 1 : 0, (int)Math.Round((k - 1) * len / total));
                for (int j = 1; j <= steps; j++) prices.Add(Round(pts[leg] + (pts[leg + 1] - pts[leg]) * j / steps));
            }
            if (prices[prices.Count - 1] != b.C) prices.Add(b.C);
            for (int i = 0; i < prices.Count; i++) path.Add(Tuple.Create(prices.Count == 1 ? 0 : 59.0 * i / (prices.Count - 1), prices[i]));
            volPerTick = Math.Max(0, b.V / Math.Max(1, path.Count));
        }
        double Round(double p) { return Spec.Point > 0 ? Math.Round(p / Spec.Point) * Spec.Point : p; }

        // ---- one price
        double bid, ask;
        void Quote(DateTime t, double b, int spreadPts)
        {
            int sp = Set.FixedSpread >= 0 ? Set.FixedSpread : Math.Max(0, spreadPts);
            bid = b; ask = Round(b + sp * Spec.Point);
            state.Quotes[Spec.Name] = new Mt5Quote { Symbol = Spec.Name, Bid = bid, Ask = ask, Time = t };
        }
        void Tick(DateTime t, double price, int spreadPts, long vol)
        {
            if (t.Date != Clock.Date && Clock > DateTime.MinValue) Rollover(t);
            Clock = t; state.ServerNow = t; Quote(t, price, spreadPts);
            foreach (var m in Tf.All) state.SeriesOf(Spec.Name, m).OnTick(t, price, vol);
            Triggers(t); Mark(); StopOut(t);
        }
        // swap at the server's midnight for every position held over it (Wednesday counts 3 days)
        void Rollover(DateTime t)
        {
            if (Set.SwapLong == 0 && Set.SwapShort == 0) return;
            int mult = Clock.DayOfWeek == DayOfWeek.Wednesday ? 3 : 1; if (Clock.DayOfWeek == DayOfWeek.Saturday || Clock.DayOfWeek == DayOfWeek.Sunday) return;
            foreach (var p in state.Positions) p.Swap += p.Lots * (p.Dir > 0 ? Set.SwapLong : Set.SwapShort) * mult;
        }
        void Triggers(DateTime t)
        {
            foreach (var o in state.Orders.ToList())
            {
                bool fire = o.Type == "BUY LIMIT" ? ask <= o.Price : o.Type == "SELL LIMIT" ? bid >= o.Price : o.Type == "BUY STOP" ? ask >= o.Price : o.Type == "SELL STOP" ? bid <= o.Price : false;
                if (!fire) continue;
                state.Orders.Remove(o); int dir = o.Type.StartsWith("BUY") ? 1 : -1;
                double px = o.Type.EndsWith("LIMIT") ? o.Price : (dir > 0 ? ask : bid) + dir * Set.SlipPts * Spec.Point;
                Open(t, dir, o.Lots, Round(px), o.Sl, o.Tp, "#" + o.Ticket + " " + o.Type.ToLowerInvariant(), o.Ticket);
                Notes.Add("⏳ " + o.Type + " #" + o.Ticket + " filled @ " + px.ToString("F" + Spec.Digits, IC));
            }
            foreach (var p in state.Positions.ToList())
            {
                if (p.Dir > 0) { if (p.Sl > 0 && bid <= p.Sl) Close(t, p, p.Lots, bid, "SL"); else if (p.Tp > 0 && bid >= p.Tp) Close(t, p, p.Lots, p.Tp, "TP"); }
                else { if (p.Sl > 0 && ask >= p.Sl) Close(t, p, p.Lots, ask, "SL"); else if (p.Tp > 0 && ask <= p.Tp) Close(t, p, p.Lots, p.Tp, "TP"); }
            }
        }
        double Pl(int dir, double open, double close, double lots) { return (close - open) * dir * lots * Spec.TickValue / Spec.TickSize; }
        double MarginOf(double lots, double price)
        {
            var n = Spec.Name.ToUpperInvariant(); bool usdBase = n.Length >= 6 && n.StartsWith("USD") && n.Substring(0, 6).All(char.IsLetter);
            return lots * Spec.Contract * (usdBase ? 1 : price) / Math.Max(1, Set.Leverage);
        }
        void Mark()
        {
            double fl = 0, mg = 0;
            foreach (var p in state.Positions) { p.Current = p.Dir > 0 ? bid : ask; p.Profit = Math.Round(Pl(p.Dir, p.Open, p.Current, p.Lots), 2); fl += p.Profit + p.Swap; mg += MarginOf(p.Lots, p.Open); }
            var a = state.Account; a.Balance = Math.Round(balance, 2); a.Equity = Math.Round(balance + fl, 2); a.Margin = Math.Round(mg, 2); a.FreeMargin = Math.Round(a.Equity - mg, 2); a.MarginLevel = mg > 0 ? a.Equity / mg * 100 : 0;
        }
        void StopOut(DateTime t)
        {
            for (int guard = 0; guard < 200; guard++)
            {
                var a = state.Account; if (a.Margin <= 0 || a.MarginLevel >= Set.StopOutPct || state.Positions.Count == 0) return;
                var worst = state.Positions.OrderBy(p => p.Profit + p.Swap).First(); Close(t, worst, worst.Lots, worst.Current, "STOPOUT");
                Notes.Add("⚠ STOP OUT: margin level under " + Set.StopOutPct.ToString("0", IC) + " % — #" + worst.Ticket + " closed"); Mark();
            }
        }
        Mt5Position Open(DateTime t, int dir, double lots, double px, double sl, double tp, string comment, long ticket = 0)
        {
            double comm = -Math.Round(Set.CommissionPerLot / 2 * lots, 2); balance += comm;
            var p = new Mt5Position { Ticket = ticket > 0 ? ticket : nextTicket++, Symbol = Spec.Name, Side = dir > 0 ? "BUY" : "SELL", Lots = lots, Open = px, Sl = sl, Tp = tp, Current = dir > 0 ? bid : ask, Time = t, Comment = comment, Commission = comm };
            state.Positions.Add(p);
            state.Deals.Add(new Mt5Deal { Ticket = nextDeal++, Position = p.Ticket, Time = t, Symbol = Spec.Name, Type = p.Side, Entry = "IN", Lots = lots, Price = px, Commission = comm, Comment = comment }); state.DealsVersion++;
            Mark(); return p;
        }
        void Close(DateTime t, Mt5Position p, double lots, double px, string reason)
        {
            lots = Math.Min(p.Lots, lots); double frac = p.Lots > 0 ? lots / p.Lots : 1;
            double pl = Math.Round(Pl(p.Dir, p.Open, px, lots), 2), comm = -Math.Round(Set.CommissionPerLot / 2 * lots, 2), sw = Math.Round(p.Swap * frac, 2);
            balance += pl + comm + sw; p.Swap -= sw;
            state.Deals.Add(new Mt5Deal { Ticket = nextDeal++, Position = p.Ticket, Time = t, Symbol = Spec.Name, Type = p.Dir > 0 ? "SELL" : "BUY", Entry = "OUT", Lots = lots, Price = px, Profit = pl, Commission = comm, Swap = sw, Reason = reason }); state.DealsVersion++;
            if (lots >= p.Lots - 1e-9) state.Positions.Remove(p); else p.Lots = Math.Round(p.Lots - lots, 2);
            if (reason == "SL" || reason == "TP") Notes.Add((reason == "TP" ? "🎯 TARGET" : "🛑 STOP") + " #" + p.Ticket + " " + (pl + comm >= 0 ? "+" : "−") + "$" + Math.Abs(pl + comm).ToString("N2", IC));
            Mark();
        }

        // ---- the commands (the same as the bridge's)
        public bool Send(string line) { return true; }   // candles / symbols requests: the backtest has everything already
        public string Command(string cmd, params object[] a)
        {
            string id = "S" + (++cmdSeq);
            lock (state.Gate)
            {
                try { var r = Do(cmd, a); state.AddResult(id, r.Item1, r.Item2); }
                catch (Exception ex) { state.AddResult(id, false, cmd + ": " + ex.Message); }
            }
            return id;
        }
        static double N(object o) { return Convert.ToDouble(o, IC); }
        static long Tk(object o) { return Convert.ToInt64(o, IC); }
        double Lots(double v) { double st = Spec.VolStep > 0 ? Spec.VolStep : 0.01; v = Math.Floor(v / st + 1e-9) * st; return Math.Round(Math.Max(Spec.VolMin, Math.Min(Spec.VolMax, v)), 2); }
        string F(double v) { return v.ToString("F" + Spec.Digits, IC); }
        Tuple<bool, string> Ok(string s) { return Tuple.Create(true, s); }
        Tuple<bool, string> No(string s) { return Tuple.Create(false, s); }
        Tuple<bool, string> Do(string cmd, object[] a)
        {
            if (Ended && cmd == "ORDER") return No("the backtest has ended — restart it");
            var t = Clock;
            switch (cmd)
            {
                case "ORDER":
                    {
                        string kind = Convert.ToString(a[1], IC); double lots = Lots(N(a[2])), px = N(a[3]), sl = Round(N(a[4])), tp = Round(N(a[5])); int dir = kind.StartsWith("BUY") ? 1 : -1;
                        if (N(a[2]) <= 0) return No(kind + ": the lots are 0");
                        if (kind == "BUY" || kind == "SELL")
                        {
                            double fill = Round((dir > 0 ? ask : bid) + dir * Set.SlipPts * Spec.Point);
                            if (!StopsOk(dir, dir > 0 ? bid : ask, sl, tp)) return No(kind + " " + lots.ToString("0.00", IC) + ": invalid stops (a stop on the losing side, a target on the winning side)");
                            double need = MarginOf(lots, fill); if (need > state.Account.FreeMargin) return No(kind + " " + lots.ToString("0.00", IC) + ": not enough money (margin " + need.ToString("N2", IC) + ")");
                            var p = Open(t, dir, lots, fill, sl, tp, "SpasiX"); return Ok(kind + " " + lots.ToString("0.00", IC) + " " + Spec.Name + " @ " + F(fill) + ": done (#" + p.Ticket + ")");
                        }
                        px = Round(px);
                        bool okPx = kind == "BUY LIMIT" ? px < ask : kind == "SELL LIMIT" ? px > bid : kind == "BUY STOP" ? px > ask : kind == "SELL STOP" ? px < bid : false;
                        if (!okPx) return No(kind + " @ " + F(px) + ": invalid price (a limit below / above the market, a stop beyond it)");
                        if (!StopsOk(dir, px, sl, tp)) return No(kind + ": invalid stops");
                        var o = new Mt5Order { Ticket = nextTicket++, Symbol = Spec.Name, Type = kind, Lots = lots, Price = px, Sl = sl, Tp = tp, Time = t }; state.Orders.Add(o);
                        return Ok(kind + " " + lots.ToString("0.00", IC) + " @ " + F(px) + ": placed (#" + o.Ticket + ")");
                    }
                case "CLOSE":
                    {
                        var p = state.Positions.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (p == null) return No("close #" + a[0] + ": no such position");
                        double lots = N(a[1]); lots = lots > 0 && lots < p.Lots ? Lots(lots) : p.Lots; Close(t, p, lots, p.Dir > 0 ? bid : ask, "CLIENT"); return Ok("close #" + p.Ticket + " " + lots.ToString("0.00", IC) + ": done");
                    }
                case "CLOSEALL":
                    {
                        string sym = Convert.ToString(a[0], IC), which = Convert.ToString(a[1], IC); int n = 0;
                        foreach (var p in state.Positions.ToList())
                        {
                            double pl = p.Profit + p.Swap;
                            if ((which == "BUY" && p.Dir < 0) || (which == "SELL" && p.Dir > 0) || (which == "PROFIT" && pl <= 0) || (which == "LOSS" && pl >= 0)) continue;
                            Close(t, p, p.Lots, p.Dir > 0 ? bid : ask, "CLIENT"); n++;
                        }
                        return Ok("closed " + n);
                    }
                case "MODIFY":
                    {
                        var p = state.Positions.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (p == null) return No("S/L T/P #" + a[0] + ": no such position");
                        double sl = Round(N(a[1])), tp = Round(N(a[2])); if (!StopsOk(p.Dir, p.Dir > 0 ? bid : ask, sl, tp)) return No("S/L T/P #" + p.Ticket + ": invalid stops");
                        p.Sl = sl; p.Tp = tp; return Ok("S/L T/P #" + p.Ticket + ": done");
                    }
                case "MODORD":
                    {
                        var o = state.Orders.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (o == null) return No("order #" + a[0] + ": no such order");
                        double px = Round(N(a[1])), sl = Round(N(a[2])), tp = Round(N(a[3]));
                        bool okPx = o.Type == "BUY LIMIT" ? px < ask : o.Type == "SELL LIMIT" ? px > bid : o.Type == "BUY STOP" ? px > ask : px < bid;
                        if (!okPx) return No("order #" + o.Ticket + " @ " + F(px) + ": invalid price");
                        double d = px - o.Price; o.Price = px; if (o.Sl > 0 && sl == o.Sl) sl += d; if (o.Tp > 0 && tp == o.Tp) tp += d; o.Sl = Round(sl); o.Tp = Round(tp);   // the bracket moves with the order
                        return Ok("order #" + o.Ticket + " → " + F(px) + ": done");
                    }
                case "CANCEL": { var o = state.Orders.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (o == null) return No("cancel #" + a[0] + ": no such order"); state.Orders.Remove(o); return Ok("cancel #" + o.Ticket + ": done"); }
                case "CANCELALL": { int n = state.Orders.Count; state.Orders.Clear(); return Ok("cancelled " + n + " order(s)"); }
                case "BE":
                    {
                        string which = Convert.ToString(a[1], IC); int off = (int)N(a[2]); int done = 0, skip = 0;
                        foreach (var p in state.Positions)
                        {
                            if ((which == "BUY" && p.Dir < 0) || (which == "SELL" && p.Dir > 0)) continue;
                            double be = Round(p.Open + p.Dir * off * Spec.Point); bool inProfit = p.Dir > 0 ? p.Current > be + Spec.Point : p.Current < be - Spec.Point; bool better = p.Sl == 0 || (p.Dir > 0 ? be > p.Sl : be < p.Sl);
                            if (!inProfit || !better) { skip++; continue; }
                            p.Sl = be; done++;
                        }
                        return Ok("breakeven on " + done + " • " + skip + " not in profit / already better");
                    }
            }
            return No("unknown command " + cmd);
        }
        bool StopsOk(int dir, double px, double sl, double tp) { return dir > 0 ? (sl <= 0 || sl < px) && (tp <= 0 || tp > px) : (sl <= 0 || sl > px) && (tp <= 0 || tp < px); }
    }
}
