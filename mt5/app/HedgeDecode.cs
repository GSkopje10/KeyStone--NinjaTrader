// SpasiX — 🕵 DECODE HEDGING: learns a trader's hedging from the trades COPIED onto your MT5 account(s) (copy trading, not PAMM).
// READ ONLY: it never sends an order — the copy service opens / closes the trades, this only WATCHES every connected account (Duramarkets,
// IC Markets, …: each MT5 link of Mt5Hub) and writes down every execution with everything around it: the basket (buy / sell lots, net,
// floating $, legs), the distance and time from the last leg, the lot ratios, the pending stop / limit orders (placed, moved, filled,
// REMOVED — with the floating $ at that moment), the moving averages (M1 / M5 EMA 9 / 21 / 50 / 100 / 200, SMA 20 / 50 / 200), the swing high /
// low, the session. Saved per account and day in DataDir\decode-hedging\ (CSV) so nothing is lost; HINTS turns the pool into candidate rules.
// The only things it ever asks MT5 for are prices and candles (WATCH / BARS) — data, never an order.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace KeystoneMT5
{
    public sealed class DecEvent
    {
        public DateTime Time; public string Account = "", Symbol = "", Kind = "", Side = "", Comment = "", Note = "";
        public long Ticket, Magic; public double Lots, Price, Bid, Ask, Sl, Tp, Profit = double.NaN;
        // the basket at that moment (the copied positions of this symbol on this account)
        public int Legs; public double BuyLots, SellLots, Floating, Balance, Equity;
        public double FromLast = double.NaN, FromLastOpp = double.NaN, FromFirst = double.NaN, SecLast = double.NaN, RatioPrev = double.NaN, RatioFirst = double.NaN, LotsPer10k = double.NaN;
        public double DistBid = double.NaN, PlacedFloat = double.NaN, LivedSec = double.NaN;   // pending orders: the distance from the price; when it went: the floating $ when it was placed, how long it lived
        public int Cycle;
        public Dictionary<string, double> Ma = new Dictionary<string, double>();   // "M1 EMA21" → its value (the price minus it = Price − value)
        public double SwingHi = double.NaN, SwingLo = double.NaN; public string Session = "", NearMa = "";
        public static readonly string[] MaNames = { "M1 EMA9", "M1 EMA21", "M1 EMA50", "M1 EMA100", "M1 EMA200", "M1 SMA20", "M1 SMA50", "M1 SMA200", "M5 EMA9", "M5 EMA21", "M5 EMA50", "M5 EMA200" };
    }
    public sealed class DecCycle
    {
        public string Account = "", Symbol = "", FirstSide = ""; public int N, Legs, Buys, Sells, Pendings, Removed; public DateTime Start, End; public bool Done;
        public double MaxLots, MaxNet, Worst, Best, Result, FirstLots, FirstPrice;
    }

    public sealed class HedgeDecode
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static readonly HedgeDecode Service = new HedgeDecode();
        public readonly List<DecEvent> Events = new List<DecEvent>();
        public readonly List<DecCycle> Cycles = new List<DecCycle>();
        public int Version; public string Symbol = "XAU"; public string Filter = "";   // the symbol (contains) and an optional comment / magic filter ("" = every trade on the account)
        public readonly HashSet<string> Off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // accounts NOT watched
        sealed class Acc
        {
            public bool Primed; public Dictionary<long, Mt5Position> Pos = new Dictionary<long, Mt5Position>(); public Dictionary<long, Mt5Order> Ord = new Dictionary<long, Mt5Order>();
            public Dictionary<long, Tuple<DateTime, double>> OrdBorn = new Dictionary<long, Tuple<DateTime, double>>(); public Dictionary<string, DecCycle> Open = new Dictionary<string, DecCycle>();
            public Dictionary<string, List<DecEvent>> Legs = new Dictionary<string, List<DecEvent>>(); public DateTime BarsAt = DateTime.MinValue;
        }
        readonly Dictionary<string, Acc> accs = new Dictionary<string, Acc>();
        bool loaded;
        public static string RootOverride;   // tests: a temp folder
        public static string Folder { get { return Path.Combine(RootOverride ?? DataDir.Root, "decode-hedging"); } }

        public bool Wants(Mt5Position p) { return Has(p.Symbol) && PassFilter(p.Comment, p.Magic); }
        bool Has(string sym) { return Symbol == "" || sym.IndexOf(Symbol, StringComparison.OrdinalIgnoreCase) >= 0; }
        bool PassFilter(string comment, long magic)
        {
            if (Filter == "") return true; foreach (var f in Filter.Split(',').Select(x => x.Trim()).Where(x => x != ""))
            { long m; if (long.TryParse(f, out m) && m == magic) return true; if ((comment ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true; }
            return false;
        }

        // one look at every account: called every second by the app (also with the studio closed)
        public void StepAll(IEnumerable<Mt5Bridge> links)
        {
            if (!loaded) { loaded = true; Load(); }
            foreach (var l in links) { if (l == null || l.Key == "" || Off.Contains(l.Key)) continue; try { Step(l, l.Key); } catch { } }
        }
        public void Step(IBroker b, string key)
        {
            Acc a; if (!accs.TryGetValue(key, out a)) accs[key] = a = new Acc();
            List<Mt5Position> ps; List<Mt5Order> os; DateTime now; double bal, eq; var quotes = new Dictionary<string, Mt5Quote>(StringComparer.OrdinalIgnoreCase); var per = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            lock (b.State.Gate)
            {
                var st = b.State; if (!st.Linked) return; now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; bal = st.Account.Balance; eq = st.Account.Equity;
                ps = st.Positions.Where(Wants).Select(p => new Mt5Position { Ticket = p.Ticket, Symbol = p.Symbol, Side = p.Side, Comment = p.Comment, Lots = p.Lots, Open = p.Open, Sl = p.Sl, Tp = p.Tp, Current = p.Current, Profit = p.Profit, Swap = p.Swap, Commission = p.Commission, Time = p.Time, Magic = p.Magic }).ToList();
                os = st.Orders.Where(o => Has(o.Symbol)).Select(o => new Mt5Order { Ticket = o.Ticket, Symbol = o.Symbol, Type = o.Type, Lots = o.Lots, Price = o.Price, Sl = o.Sl, Tp = o.Tp, Time = o.Time }).ToList();
                foreach (var q in st.Quotes) quotes[q.Key] = q.Value;
                foreach (var s in st.Symbols) if (s.Value.TickSize > 0) per[s.Key] = s.Value.TickValue / s.Value.TickSize;
                // keep the candles fresh for the moving averages (data only)
                if ((DateTime.Now - a.BarsAt).TotalSeconds > 60) { a.BarsAt = DateTime.Now; foreach (var sy in ps.Select(p => p.Symbol).Concat(os.Select(o => o.Symbol)).Distinct(StringComparer.OrdinalIgnoreCase)) { b.Send("WATCH|" + sy); b.Send("BARS|" + sy + "|1|400"); b.Send("BARS|" + sy + "|5|400"); } }
            }
            if (!a.Primed)
            {
                a.Primed = true;
                foreach (var p in ps.OrderBy(x => x.Time)) { a.Pos[p.Ticket] = p; var c = CycleOf(a, key, p, now); c.Legs++; if (p.Side == "BUY") c.Buys++; else c.Sells++; a.Legs[c.Symbol].Add(new DecEvent { Time = p.Time, Side = p.Side, Lots = p.Lots, Price = p.Open, Ticket = p.Ticket });
                    var e0 = Ctx(b, key, p.Symbol, now, ps, quotes, bal, eq); e0.Kind = "ALREADY OPEN"; e0.Side = p.Side; e0.Lots = p.Lots; e0.Price = p.Open; e0.Ticket = p.Ticket; e0.Magic = p.Magic; e0.Comment = p.Comment; e0.Cycle = c.N; e0.Note = "open before the recording started (opened " + p.Time.ToString("dd MMM HH:mm:ss", IC) + ")"; Add(e0); }
                foreach (var o in os) { a.Ord[o.Ticket] = o; a.OrdBorn[o.Ticket] = Tuple.Create(now, Floating(ps)); }
                return;
            }
            foreach (var g in ps.GroupBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase)) { DecCycle c; if (a.Open.TryGetValue(g.Key.ToUpperInvariant(), out c)) { double fl = g.Sum(p => p.Profit + p.Swap + p.Commission); c.Worst = Math.Min(c.Worst, fl); c.Best = Math.Max(c.Best, fl); c.MaxLots = Math.Max(c.MaxLots, g.Sum(p => p.Lots)); c.MaxNet = Math.Max(c.MaxNet, Math.Abs(g.Sum(p => p.Dir * p.Lots))); } }
            var nowPos = ps.ToDictionary(p => p.Ticket); bool any = false;
            // CLOSED
            foreach (var p in a.Pos.Values.Where(p => !nowPos.ContainsKey(p.Ticket)).ToList())
            {
                a.Pos.Remove(p.Ticket); Mt5Quote q; quotes.TryGetValue(p.Symbol, out q); double pp; per.TryGetValue(p.Symbol, out pp); double exit = q != null ? (p.Dir > 0 ? q.Bid : q.Ask) : p.Current;
                double pl = q != null && pp > 0 ? p.Dir * (exit - p.Open) * p.Lots * pp + p.Swap + p.Commission : p.Profit + p.Swap + p.Commission;
                var e = Ctx(b, key, p.Symbol, now, ps, quotes, bal, eq); e.Kind = "CLOSE"; e.Side = p.Side; e.Lots = p.Lots; e.Price = exit; e.Ticket = p.Ticket; e.Magic = p.Magic; e.Comment = p.Comment; e.Profit = pl; e.LivedSec = (now - p.Time).TotalSeconds;
                bool tp = p.Tp > 0 && Math.Abs(exit - p.Tp) < 0.3, sl = p.Sl > 0 && Math.Abs(exit - p.Sl) < 0.3; e.Note = tp ? "by its TP" : sl ? "by its SL" : "closed by hand / by the copy";
                DecCycle c; if (a.Open.TryGetValue(p.Symbol.ToUpperInvariant(), out c)) { c.Result += pl; e.Cycle = c.N; }
                Add(e); any = true;
            }
            foreach (var c in a.Open.Values.ToList())
                if (!ps.Any(p => string.Equals(p.Symbol, c.Symbol, StringComparison.OrdinalIgnoreCase)))
                {
                    c.Done = true; c.End = now; a.Open.Remove(c.Symbol.ToUpperInvariant()); a.Legs.Remove(c.Symbol);
                    var e = Ctx(b, key, c.Symbol, now, ps, quotes, bal, eq); e.Kind = "CYCLE FLAT"; e.Profit = c.Result; e.Cycle = c.N; e.LivedSec = (c.End - c.Start).TotalSeconds;
                    e.Note = "cycle #" + c.N + ": " + c.Legs + " legs (" + c.Buys + " buys / " + c.Sells + " sells), most lots " + c.MaxLots.ToString("0.##", IC) + ", worst " + c.Worst.ToString("0.00", IC) + ", result " + c.Result.ToString("0.00", IC);
                    Add(e); SaveCycle(c); any = true;
                }
            // OPENED
            foreach (var p in ps.Where(p => !a.Pos.ContainsKey(p.Ticket)).OrderBy(p => p.Time).ThenBy(p => p.Ticket))
            {
                var filled = a.Ord.Values.FirstOrDefault(o => !os.Any(x => x.Ticket == o.Ticket) && string.Equals(o.Symbol, p.Symbol, StringComparison.OrdinalIgnoreCase) && o.Type.StartsWith(p.Side) && Math.Abs(o.Lots - p.Lots) < 1e-6);
                var c = CycleOf(a, key, p, now); var legs = a.Legs[c.Symbol];
                var e = Ctx(b, key, p.Symbol, now, ps, quotes, bal, eq); e.Kind = filled != null ? "FILL " + filled.Type : legs.Count == 0 ? "FIRST LEG" : (legs.Last().Side != p.Side ? "HEDGE" : "ADD"); e.Side = p.Side; e.Lots = p.Lots; e.Price = p.Open; e.Ticket = p.Ticket; e.Magic = p.Magic; e.Comment = p.Comment; e.Sl = p.Sl; e.Tp = p.Tp; e.Cycle = c.N;
                if (filled != null && legs.Count > 0) e.Note = legs.Last().Side != p.Side ? "a HEDGE by a pending stop" : "an ADD by a pending order";
                int d = p.Side == "BUY" ? 1 : -1;
                if (legs.Count > 0)
                {
                    var last = legs.Last(); e.FromLast = (p.Open - last.Price) * d; e.SecLast = (p.Time - last.Time).TotalSeconds; e.RatioPrev = last.Lots > 0 ? p.Lots / last.Lots : double.NaN; e.RatioFirst = legs[0].Lots > 0 ? p.Lots / legs[0].Lots : double.NaN; e.FromFirst = (p.Open - legs[0].Price) * d;
                    var opp = legs.LastOrDefault(x => x.Side != p.Side); if (opp != null) e.FromLastOpp = Math.Abs(p.Open - opp.Price);
                }
                if (bal > 0) e.LotsPer10k = p.Lots / (bal / 10000.0);
                legs.Add(new DecEvent { Time = p.Time, Side = p.Side, Lots = p.Lots, Price = p.Open, Ticket = p.Ticket }); c.Legs++; if (p.Side == "BUY") c.Buys++; else c.Sells++;
                a.Pos[p.Ticket] = p; Add(e); any = true;
            }
            foreach (var p in ps) a.Pos[p.Ticket] = p;
            // PENDING: placed / moved / filled / removed — with the floating $ when it happened
            double fnow = Floating(ps);
            foreach (var o in os)
            {
                Mt5Order old; Mt5Quote q; quotes.TryGetValue(o.Symbol, out q); double mid = q != null ? (q.Bid + q.Ask) / 2 : double.NaN;
                if (!a.Ord.TryGetValue(o.Ticket, out old))
                {
                    var e = Ctx(b, key, o.Symbol, now, ps, quotes, bal, eq); e.Kind = "PLACED " + o.Type; e.Side = o.Type; e.Lots = o.Lots; e.Price = o.Price; e.Ticket = o.Ticket; e.Sl = o.Sl; e.Tp = o.Tp; e.DistBid = o.Price - mid;
                    DecCycle c; if (a.Open.TryGetValue(o.Symbol.ToUpperInvariant(), out c)) { c.Pendings++; e.Cycle = c.N; var legs = a.Legs[c.Symbol]; if (legs.Count > 0) e.FromLast = Math.Abs(o.Price - legs.Last().Price); }
                    a.OrdBorn[o.Ticket] = Tuple.Create(now, fnow); a.Ord[o.Ticket] = o; Add(e); any = true;
                }
                else if (Math.Abs(old.Price - o.Price) > 1e-9 || Math.Abs(old.Lots - o.Lots) > 1e-9)
                {
                    var e = Ctx(b, key, o.Symbol, now, ps, quotes, bal, eq); e.Kind = "MOVED " + o.Type; e.Side = o.Type; e.Lots = o.Lots; e.Price = o.Price; e.Ticket = o.Ticket; e.DistBid = o.Price - mid; e.Note = "from " + old.Price.ToString("0.00", IC) + " (" + (o.Price - old.Price).ToString("+0.00;−0.00", IC) + ")";
                    a.Ord[o.Ticket] = o; Add(e); any = true;
                }
            }
            foreach (var o in a.Ord.Values.Where(o => !os.Any(x => x.Ticket == o.Ticket)).ToList())
            {
                a.Ord.Remove(o.Ticket); bool fill = ps.Any(p => string.Equals(p.Symbol, o.Symbol, StringComparison.OrdinalIgnoreCase) && o.Type.StartsWith(p.Side) && Math.Abs(o.Lots - p.Lots) < 1e-6 && p.Time >= o.Time.AddSeconds(-5) && (now - p.Time).TotalSeconds < 10);
                Tuple<DateTime, double> born; a.OrdBorn.TryGetValue(o.Ticket, out born); a.OrdBorn.Remove(o.Ticket);
                if (fill) continue;   // its FILL is the position above
                Mt5Quote q; quotes.TryGetValue(o.Symbol, out q); double mid = q != null ? (q.Bid + q.Ask) / 2 : double.NaN;
                var e = Ctx(b, key, o.Symbol, now, ps, quotes, bal, eq); e.Kind = "REMOVED " + o.Type; e.Side = o.Type; e.Lots = o.Lots; e.Price = o.Price; e.Ticket = o.Ticket; e.DistBid = o.Price - mid;
                if (born != null) { e.PlacedFloat = born.Item2; e.LivedSec = (now - born.Item1).TotalSeconds; }
                e.Note = "removed while the floating was " + fnow.ToString("0.00", IC) + (born != null ? " (it was " + born.Item2.ToString("0.00", IC) + " when placed)" : "") + ", the price " + Math.Abs(e.DistBid).ToString("0.00", IC) + " away";
                DecCycle c; if (a.Open.TryGetValue(o.Symbol.ToUpperInvariant(), out c)) { c.Removed++; e.Cycle = c.N; }
                Add(e); any = true;
            }
            if (any) Version++;
        }
        static double Floating(List<Mt5Position> ps) { return ps.Sum(p => p.Profit + p.Swap + p.Commission); }
        DecCycle CycleOf(Acc a, string key, Mt5Position p, DateTime now)
        {
            DecCycle c; var k = p.Symbol.ToUpperInvariant(); if (a.Open.TryGetValue(k, out c)) return c;
            c = new DecCycle { Account = key, Symbol = p.Symbol, FirstSide = p.Side, N = Cycles.Count(x => x.Account == key) + 1, Start = p.Time > DateTime.MinValue ? p.Time : now, FirstLots = p.Lots, FirstPrice = p.Open }; a.Open[k] = c; a.Legs[p.Symbol] = new List<DecEvent>(); Cycles.Add(c); return c;
        }
        // the market and the basket around an event
        DecEvent Ctx(IBroker b, string key, string sym, DateTime now, List<Mt5Position> ps, Dictionary<string, Mt5Quote> quotes, double bal, double eq)
        {
            var e = new DecEvent { Time = now, Account = key, Symbol = sym, Balance = bal, Equity = eq }; Mt5Quote q; if (quotes.TryGetValue(sym, out q)) { e.Bid = q.Bid; e.Ask = q.Ask; }
            var mine = ps.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList(); e.Legs = mine.Count; e.BuyLots = mine.Where(p => p.Side == "BUY").Sum(p => p.Lots); e.SellLots = mine.Where(p => p.Side != "BUY").Sum(p => p.Lots); e.Floating = Floating(mine);
            List<Mt5Bar> m1, m5; lock (b.State.Gate) { m1 = b.State.SeriesOf(sym, 1).Bars.ToList(); m5 = b.State.SeriesOf(sym, 5).Bars.ToList(); }
            double px = !double.IsNaN(e.Bid) && e.Bid > 0 ? e.Bid : m1.Count > 0 ? m1[m1.Count - 1].C : double.NaN;
            Action<string, List<Mt5Bar>, int, bool> ma = (name, bars, n, ema) => { var v = Ma(bars, n, ema); if (!double.IsNaN(v)) e.Ma[name] = v; };
            ma("M1 EMA9", m1, 9, true); ma("M1 EMA21", m1, 21, true); ma("M1 EMA50", m1, 50, true); ma("M1 EMA100", m1, 100, true); ma("M1 EMA200", m1, 200, true); ma("M1 SMA20", m1, 20, false); ma("M1 SMA50", m1, 50, false); ma("M1 SMA200", m1, 200, false);
            ma("M5 EMA9", m5, 9, true); ma("M5 EMA21", m5, 21, true); ma("M5 EMA50", m5, 50, true); ma("M5 EMA200", m5, 200, true);
            if (!double.IsNaN(px) && e.Ma.Count > 0) { var nm = e.Ma.OrderBy(x => Math.Abs(px - x.Value)).First(); e.NearMa = nm.Key + " (" + (px - nm.Value).ToString("+0.00;−0.00", IC) + ")"; }
            // the last swing high / low of the finished M1 candles (2 lower / higher on each side)
            for (int j = m1.Count - 4; j >= 2 && (double.IsNaN(e.SwingHi) || double.IsNaN(e.SwingLo)); j--)
            {
                if (double.IsNaN(e.SwingHi) && m1[j].H > m1[j - 1].H && m1[j].H > m1[j - 2].H && m1[j].H > m1[j + 1].H && m1[j].H > m1[j + 2].H) e.SwingHi = m1[j].H;
                if (double.IsNaN(e.SwingLo) && m1[j].L < m1[j - 1].L && m1[j].L < m1[j - 2].L && m1[j].L < m1[j + 1].L && m1[j].L < m1[j + 2].L) e.SwingLo = m1[j].L;
            }
            var ny = DateTime.UtcNow.AddHours(-5); int h = ny.Hour; e.Session = h >= 19 || h < 3 ? "ASIA" : h < 8 ? "LONDON" : h < 12 ? "LONDON+NY" : h < 17 ? "NEW YORK" : "LATE NY";
            return e;
        }
        public static double Ma(List<Mt5Bar> bars, int n, bool ema)
        {
            if (bars == null || bars.Count < n) return double.NaN;
            if (!ema) return bars.Skip(bars.Count - n).Average(x => x.C);
            double k = 2.0 / (n + 1), v = bars.Skip(Math.Max(0, bars.Count - n * 4)).Take(n).Average(x => x.C);
            for (int i = Math.Max(0, bars.Count - n * 4) + n; i < bars.Count; i++) v = bars[i].C * k + v * (1 - k);
            return v;
        }

        // ---- saved: one CSV per account per day + cycles.csv
        static readonly string[] Cols = { "time", "account", "symbol", "kind", "side", "lots", "price", "bid", "ask", "ticket", "magic", "comment", "profit", "legs", "buy_lots", "sell_lots", "floating", "balance", "equity", "from_last", "from_last_opposite", "from_first", "sec_since_last", "ratio_prev", "ratio_first", "lots_per_10k", "dist_from_price", "floating_when_placed", "lived_sec", "cycle", "swing_hi", "swing_lo", "session", "nearest_ma", "sl", "tp", "note" };
        static string N(double v) { return double.IsNaN(v) ? "" : v.ToString("0.#####", IC); }
        static string Q(string s) { s = s ?? ""; return s.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }
        static string Safe(string k) { foreach (var c in Path.GetInvalidFileNameChars()) k = k.Replace(c, '_'); return k; }
        void Add(DecEvent e)
        {
            Events.Add(e); if (Events.Count > 200000) Events.RemoveRange(0, 20000);
            try
            {
                var dir = Path.Combine(Folder, Safe(e.Account)); Directory.CreateDirectory(dir); var f = Path.Combine(dir, "events-" + e.Time.ToString("yyyy-MM-dd", IC) + ".csv");
                var sb = new StringBuilder(); if (!File.Exists(f)) sb.AppendLine(string.Join(",", Cols.Concat(DecEvent.MaNames.Select(m => m.Replace(' ', '_')))));
                sb.AppendLine(Line(e)); File.AppendAllText(f, sb.ToString());
            }
            catch { }
        }
        static string Line(DecEvent e)
        {
            var v = new List<string> { e.Time.ToString("yyyy-MM-dd HH:mm:ss", IC), Q(e.Account), e.Symbol, Q(e.Kind), Q(e.Side), N(e.Lots), N(e.Price), N(e.Bid), N(e.Ask), e.Ticket.ToString(IC), e.Magic.ToString(IC), Q(e.Comment), N(e.Profit), e.Legs.ToString(IC), N(e.BuyLots), N(e.SellLots), N(e.Floating), N(e.Balance), N(e.Equity),
                N(e.FromLast), N(e.FromLastOpp), N(e.FromFirst), N(e.SecLast), N(e.RatioPrev), N(e.RatioFirst), N(e.LotsPer10k), N(e.DistBid), N(e.PlacedFloat), N(e.LivedSec), e.Cycle.ToString(IC), N(e.SwingHi), N(e.SwingLo), e.Session, Q(e.NearMa), N(e.Sl), N(e.Tp), Q(e.Note) };
            foreach (var m in DecEvent.MaNames) { double x; v.Add(e.Ma.TryGetValue(m, out x) ? N(x) : ""); }
            return string.Join(",", v);
        }
        void SaveCycle(DecCycle c)
        {
            try
            {
                var dir = Path.Combine(Folder, Safe(c.Account)); Directory.CreateDirectory(dir); var f = Path.Combine(dir, "cycles.csv"); var sb = new StringBuilder();
                if (!File.Exists(f)) sb.AppendLine("cycle,symbol,start,end,minutes,first_side,first_lots,first_price,legs,buys,sells,pendings,removed,max_lots,max_net,worst,best,result");
                sb.AppendLine(string.Join(",", c.N.ToString(IC), c.Symbol, c.Start.ToString("yyyy-MM-dd HH:mm:ss", IC), c.End.ToString("yyyy-MM-dd HH:mm:ss", IC), N(Math.Round((c.End - c.Start).TotalMinutes, 1)), c.FirstSide, N(c.FirstLots), N(c.FirstPrice), c.Legs, c.Buys, c.Sells, c.Pendings, c.Removed, N(c.MaxLots), N(c.MaxNet), N(c.Worst), N(c.Best), N(c.Result)));
                File.AppendAllText(f, sb.ToString());
            }
            catch { }
        }
        static List<string> Split(string l)
        {
            var r = new List<string>(); var sb = new StringBuilder(); bool q = false;
            for (int i = 0; i < l.Length; i++) { char ch = l[i]; if (q) { if (ch == '"') { if (i + 1 < l.Length && l[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(ch); } else if (ch == '"') q = true; else if (ch == ',') { r.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); }
            r.Add(sb.ToString()); return r;
        }
        static double D(string s) { double v; return double.TryParse(s, NumberStyles.Float, IC, out v) ? v : double.NaN; }
        // everything saved before (every account, every day) back into the pool, so the HINTS learn from all of it
        public void Load()
        {
            try
            {
                if (!Directory.Exists(Folder)) return;
                foreach (var f in Directory.GetFiles(Folder, "events-*.csv", SearchOption.AllDirectories).OrderBy(x => Path.GetFileName(x)))
                {
                    var lines = File.ReadAllLines(f); if (lines.Length < 2) continue; var head = Split(lines[0]); Func<List<string>, string, string> g = (r, c) => { int i = head.IndexOf(c); return i >= 0 && i < r.Count ? r[i] : ""; };
                    foreach (var l in lines.Skip(1))
                    {
                        var r = Split(l); DateTime t; if (!DateTime.TryParseExact(g(r, "time"), "yyyy-MM-dd HH:mm:ss", IC, DateTimeStyles.None, out t)) continue;
                        var e = new DecEvent { Time = t, Account = g(r, "account"), Symbol = g(r, "symbol"), Kind = g(r, "kind"), Side = g(r, "side"), Lots = D(g(r, "lots")), Price = D(g(r, "price")), Bid = D(g(r, "bid")), Ask = D(g(r, "ask")), Comment = g(r, "comment"), Profit = D(g(r, "profit")), BuyLots = D(g(r, "buy_lots")), SellLots = D(g(r, "sell_lots")), Floating = D(g(r, "floating")), Balance = D(g(r, "balance")), Equity = D(g(r, "equity")),
                            FromLast = D(g(r, "from_last")), FromLastOpp = D(g(r, "from_last_opposite")), FromFirst = D(g(r, "from_first")), SecLast = D(g(r, "sec_since_last")), RatioPrev = D(g(r, "ratio_prev")), RatioFirst = D(g(r, "ratio_first")), LotsPer10k = D(g(r, "lots_per_10k")), DistBid = D(g(r, "dist_from_price")), PlacedFloat = D(g(r, "floating_when_placed")), LivedSec = D(g(r, "lived_sec")),
                            SwingHi = D(g(r, "swing_hi")), SwingLo = D(g(r, "swing_lo")), Session = g(r, "session"), NearMa = g(r, "nearest_ma"), Note = g(r, "note") };
                        long tk; long.TryParse(g(r, "ticket"), out tk); e.Ticket = tk; int lg; int.TryParse(g(r, "legs"), out lg); e.Legs = lg; int cy; int.TryParse(g(r, "cycle"), out cy); e.Cycle = cy;
                        foreach (var m in DecEvent.MaNames) { var x = D(g(r, m.Replace(' ', '_'))); if (!double.IsNaN(x)) e.Ma[m] = x; }
                        Events.Add(e);
                    }
                }
                foreach (var f in Directory.GetFiles(Folder, "cycles.csv", SearchOption.AllDirectories))
                {
                    var acc = Path.GetFileName(Path.GetDirectoryName(f));
                    foreach (var l in File.ReadAllLines(f).Skip(1))
                    {
                        var r = Split(l); if (r.Count < 18) continue; DateTime s, en; DateTime.TryParseExact(r[2], "yyyy-MM-dd HH:mm:ss", IC, DateTimeStyles.None, out s); DateTime.TryParseExact(r[3], "yyyy-MM-dd HH:mm:ss", IC, DateTimeStyles.None, out en);
                        int n; int.TryParse(r[0], out n); Func<int, int> I = i => { int v; int.TryParse(r[i], out v); return v; };
                        Cycles.Add(new DecCycle { Account = acc, N = n, Symbol = r[1], Start = s, End = en, FirstSide = r[5], FirstLots = D(r[6]), FirstPrice = D(r[7]), Legs = I(8), Buys = I(9), Sells = I(10), Pendings = I(11), Removed = I(12), MaxLots = D(r[13]), MaxNet = D(r[14]), Worst = D(r[15]), Best = D(r[16]), Result = D(r[17]), Done = true });
                    }
                }
                Version++;
            }
            catch { }
        }
        // a video-decoded day (the same columns) into the pool: what was read off his livestream counts like a copied day
        public int Import(string file)
        {
            int before = Events.Count; var tmp = Path.Combine(Folder, "video");
            try { Directory.CreateDirectory(tmp); File.Copy(file, Path.Combine(tmp, "events-video-" + Path.GetFileNameWithoutExtension(file) + ".csv"), true); } catch { return 0; }
            Events.Clear(); Cycles.Clear(); Load(); Version++; return Events.Count - before;   // everything reloaded from the files (the new day included)
        }
        public void Clear() { Events.Clear(); Cycles.Clear(); Version++; }

        // ---- HINTS: the candidate rules from the pool, each with how many times it was seen and how consistent it is
        static double Med(IEnumerable<double> xs) { var l = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); return l.Count == 0 ? double.NaN : l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2; }
        static double Share(IEnumerable<double> xs, double center, double tol) { var l = xs.Where(x => !double.IsNaN(x)).ToList(); return l.Count == 0 ? 0 : l.Count(x => Math.Abs(x - center) <= tol) * 100.0 / l.Count; }
        public List<Tuple<string, string, string>> Hints(string account)
        {
            var ev = Events.Where(e => account == "" || e.Account == account).ToList(); var h = new List<Tuple<string, string, string>>();
            Action<string, string, string> add = (a, b2, c) => h.Add(Tuple.Create(a, b2, c));
            var legs = ev.Where(e => e.Kind == "FIRST LEG" || e.Kind == "HEDGE" || e.Kind == "ADD" || e.Kind.StartsWith("FILL")).ToList();
            add("📦 POOL", ev.Count + " executions / orders recorded, " + legs.Count + " legs, " + Cycles.Count(c => account == "" || c.Account == account) + " cycles", ev.Count > 0 ? "from " + ev.Min(e => e.Time).ToString("dd MMM HH:mm", IC) + " to " + ev.Max(e => e.Time).ToString("dd MMM HH:mm", IC) : "copy his trades onto a connected account — every one is recorded");
            if (legs.Count == 0) return h;
            var sizes = legs.GroupBy(e => e.Lots).OrderByDescending(g => g.Count()).Take(4).Select(g => g.Key.ToString("0.##", IC) + " lots ×" + g.Count());
            add("⚖ LOT SIZES", string.Join(" • ", sizes), "per $10k balance: " + N(Math.Round(Med(legs.Select(e => e.LotsPer10k)), 3)) + " lots (median)");
            var hedges = legs.Where(e => e.Kind == "HEDGE" || (e.Kind.StartsWith("FILL") && e.Note.Contains("HEDGE"))).ToList();
            if (hedges.Count > 0)
            {
                double r = Med(hedges.Select(e => e.RatioPrev)), d = Med(hedges.Select(e => e.FromLastOpp)), s = Med(hedges.Select(e => e.SecLast));
                add("🛡 HEDGE SIZE", "the hedge = " + N(Math.Round(r, 2)) + "× the last leg (median of " + hedges.Count + ")", Share(hedges.Select(e => e.RatioPrev), r, 0.05).ToString("0", IC) + "% of hedges within ±0.05× of it");
                add("📏 HEDGE DISTANCE", N(Math.Round(d, 2)) + " from the last opposite leg (median)", Share(hedges.Select(e => e.FromLastOpp), d, Math.Max(0.3, d * 0.15)).ToString("0", IC) + "% within ±15% • median " + N(Math.Round(s, 0)) + " s after the last leg");
                add("🔁 BY PENDING STOP?", hedges.Count(e => e.Kind.StartsWith("FILL")) + " of " + hedges.Count + " hedges came from a pending order", hedges.Count(e => e.Kind.StartsWith("FILL")) * 2 > hedges.Count ? "he PRE-PLACES his hedges (stop orders)" : "most hedges are market orders");
            }
            var adds = legs.Where(e => e.Kind == "ADD").ToList();
            if (adds.Count > 0) add("➕ ADDS (same side)", adds.Count + " adds • median " + N(Math.Round(Med(adds.Select(e => e.FromLast)), 2)) + " from the last leg (− = against it), ratio " + N(Math.Round(Med(adds.Select(e => e.RatioPrev)), 2)) + "×", "");
            var placed = ev.Where(e => e.Kind.StartsWith("PLACED")).ToList(); var removed = ev.Where(e => e.Kind.StartsWith("REMOVED")).ToList();
            if (placed.Count > 0) add("⏳ PENDING PLACED", placed.Count + " • median " + N(Math.Round(Med(placed.Select(e => Math.Abs(e.DistBid))), 2)) + " from the price, " + N(Math.Round(Med(placed.Select(e => e.FromLast)), 2)) + " from the last leg", string.Join(" • ", placed.GroupBy(e => e.Side).Select(g => g.Key + " ×" + g.Count())));
            if (removed.Count > 0)
            {
                double fr = Med(removed.Select(e => e.Floating)), fp = Med(removed.Select(e => e.PlacedFloat));
                add("🗑 PENDING REMOVED", removed.Count + " • floating at removal: median " + N(Math.Round(fr, 2)) + " (when placed: " + N(Math.Round(fp, 2)) + ")", "removed " + removed.Count(e => !double.IsNaN(e.PlacedFloat) && e.Floating > e.PlacedFloat) + " times after the floating IMPROVED, " + removed.Count(e => !double.IsNaN(e.PlacedFloat) && e.Floating <= e.PlacedFloat) + " after it got worse • median distance " + N(Math.Round(Med(removed.Select(e => Math.Abs(e.DistBid))), 2)) + " • lived " + N(Math.Round(Med(removed.Select(e => e.LivedSec)) / 60, 1)) + " min");
            }
            // the moving averages: on which side of each MA the buys / sells were, and which MA the price sat on
            foreach (var m in DecEvent.MaNames)
            {
                var b2 = legs.Where(e => e.Side == "BUY" && e.Ma.ContainsKey(m)).ToList(); var s2 = legs.Where(e => e.Side == "SELL" && e.Ma.ContainsKey(m)).ToList(); if (b2.Count + s2.Count < 3) continue;
                double ba = b2.Count > 0 ? b2.Count(e => e.Price > e.Ma[m]) * 100.0 / b2.Count : double.NaN, sb = s2.Count > 0 ? s2.Count(e => e.Price < e.Ma[m]) * 100.0 / s2.Count : double.NaN;
                if ((!double.IsNaN(ba) && (ba >= 75 || ba <= 25)) || (!double.IsNaN(sb) && (sb >= 75 || sb <= 25)))
                    add("📈 " + m, "buys ABOVE it: " + (double.IsNaN(ba) ? "—" : ba.ToString("0", IC) + "%") + " • sells BELOW it: " + (double.IsNaN(sb) ? "—" : sb.ToString("0", IC) + "%"), "median distance " + N(Math.Round(Med(b2.Concat(s2).Select(e => Math.Abs(e.Price - e.Ma[m]))), 2)));
            }
            var near = legs.Where(e => e.NearMa != "").GroupBy(e => e.NearMa.Split(' ').Take(2).Aggregate((x, y) => x + " " + y)).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key + " ×" + g.Count());
            add("🎯 THE MA THE PRICE SAT ON", string.Join(" • ", near), "the closest MA at each leg");
            var firsts = legs.Where(e => e.Kind == "FIRST LEG").ToList();
            if (firsts.Count > 0)
            {
                add("🚀 FIRST LEG", firsts.Count(e => e.Side == "BUY") + " buys / " + firsts.Count(e => e.Side == "SELL") + " sells • sessions: " + string.Join(", ", firsts.GroupBy(e => e.Session).OrderByDescending(g => g.Count()).Select(g => g.Key + " ×" + g.Count())), "vs the swing: buys " + firsts.Where(e => e.Side == "BUY" && !double.IsNaN(e.SwingHi)).Count(e => e.Price > e.SwingHi) + " above the swing high, sells " + firsts.Where(e => e.Side == "SELL" && !double.IsNaN(e.SwingLo)).Count(e => e.Price < e.SwingLo) + " below the swing low");
            }
            var cy = Cycles.Where(c => c.Done && (account == "" || c.Account == account)).ToList();
            if (cy.Count > 0) add("🔁 CYCLES", cy.Count + " done • won " + cy.Count(c => c.Result > 0) + " • median result " + N(Math.Round(Med(cy.Select(c => c.Result)), 2)) + " • median " + N(Math.Round(Med(cy.Select(c => (c.End - c.Start).TotalMinutes)), 0)) + " min, " + N(Med(cy.Select(c => (double)c.Legs))) + " legs", "worst floating: median " + N(Math.Round(Med(cy.Select(c => c.Worst)), 2)) + ", the worst " + N(Math.Round(cy.Min(c => c.Worst), 2)));
            var closes = ev.Where(e => e.Kind == "CLOSE").ToList();
            if (closes.Count > 0) add("✅ HOW HE CLOSES", closes.Count(e => e.Note.Contains("TP")) + " by TP, " + closes.Count(e => e.Note.Contains("SL")) + " by SL, " + closes.Count(e => e.Note.Contains("hand")) + " by hand", "floating when the cycle went flat: median " + N(Math.Round(Med(ev.Where(e => e.Kind == "CYCLE FLAT").Select(e => e.Profit)), 2)));
            return h;
        }
    }
}
