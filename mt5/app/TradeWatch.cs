// SpasiX — 🔍 DECODE: watches EVERY trade on the account (yours, SpasiX's 🤖 AUTO, a third-party EA's) and writes down the market around each
// one — READ ONLY, it never sends anything. Each new position / pending order: its side, lots, price, magic number, comment; its place in its
// BASKET (the open positions of the same symbol + magic): the first leg, a leg the OPPOSITE way (a hedge) or the SAME way (adding), how far
// from the last leg (price and $), the basket's $ at that moment, the lot ratio, the seconds since; and which recent high / low of the last
// N finished M1 / M5 / M15 candles the price sits on. When the basket is flat again: how it closed (together at ≈ $X, one by one, by a TP).
// From all of it RULES: the most likely entry, add-on (hedge) trigger, lot multiplier and take profit of each bot — with how consistent they are.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class WatchEvent
    {
        public DateTime Time; public string Key = "", Symbol = "", Kind = "", Side = "", Comment = "", Role = "", Match = "", Story = "";
        public long Ticket, Magic; public double Lots, Price, Bid, Ask, Basket, Net, FromLast = double.NaN, FromLastMoney = double.NaN, SecSinceLast = double.NaN, LotRatio = double.NaN, LotsVsFirst = double.NaN, Sl, Tp, MatchDist = double.NaN;
        public int Leg, Basket0;
        public List<Tuple<string, double>> Levels;           // the recent highs / lows when this leg opened (the next leg is compared with them too)
        public List<string> Hits = new List<string>();      // every level the price sat on (within ~2 spreads)
    }
    public sealed class WatchBasket
    {
        public string Key = "", Symbol = "", FirstSide = ""; public long Magic; public DateTime Start, End; public int Legs, Opposite, Same, Pendings; public double MaxGross, MinPl = 0, MaxPl = 0, Result; public bool Together, Done; public int N;
    }

    public sealed class TradeWatch
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public readonly List<WatchEvent> Events = new List<WatchEvent>();
        public readonly List<WatchBasket> Baskets = new List<WatchBasket>();
        public int Version;
        readonly Dictionary<long, Mt5Position> pos = new Dictionary<long, Mt5Position>();
        readonly Dictionary<long, Mt5Order> ords = new Dictionary<long, Mt5Order>();
        readonly Dictionary<string, WatchBasket> open = new Dictionary<string, WatchBasket>();
        readonly Dictionary<string, List<WatchEvent>> legs = new Dictionary<string, List<WatchEvent>>();
        bool primed;

        static string P(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.#####", IC); }
        static string M(double v) { return double.IsNaN(v) ? "—" : (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", IC); }
        public static string KeyOf(string sym, long magic) { return sym + " • magic " + magic; }

        // one look: what opened, what closed, which pending orders came / moved / went
        public void Step(IBroker b)
        {
            List<Mt5Position> ps; List<Mt5Order> os; DateTime now; var quotes = new Dictionary<string, Mt5Quote>(StringComparer.OrdinalIgnoreCase); var per = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            lock (b.State.Gate)
            {
                var st = b.State; now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now;
                ps = st.Positions.Select(p => new Mt5Position { Ticket = p.Ticket, Symbol = p.Symbol, Side = p.Side, Comment = p.Comment, Lots = p.Lots, Open = p.Open, Sl = p.Sl, Tp = p.Tp, Current = p.Current, Profit = p.Profit, Swap = p.Swap, Commission = p.Commission, Time = p.Time, Magic = p.Magic }).ToList();
                os = st.Orders.Select(o => new Mt5Order { Ticket = o.Ticket, Symbol = o.Symbol, Type = o.Type, Lots = o.Lots, Price = o.Price, Sl = o.Sl, Tp = o.Tp, Time = o.Time }).ToList();
                foreach (var q in st.Quotes) quotes[q.Key] = q.Value;
                foreach (var s in st.Symbols) if (s.Value.TickSize > 0) per[s.Key] = s.Value.TickValue / s.Value.TickSize;
            }
            if (!primed)
            {
                primed = true;
                foreach (var p in ps.OrderBy(x => x.Time).ThenBy(x => x.Ticket)) { pos[p.Ticket] = p; var wb0 = StartIfNeeded(p, now, true); wb0.Legs++; legs[wb0.Key].Add(new WatchEvent { Time = p.Time, Key = wb0.Key, Side = p.Side, Lots = p.Lots, Price = p.Open, Ticket = p.Ticket, Kind = "OPEN (before DECODE)" }); }
                foreach (var o in os) ords[o.Ticket] = o; Version++; return;
            }
            // the basket's running P/L (its worst / best)
            foreach (var g in ps.GroupBy(p => KeyOf(p.Symbol, p.Magic))) { WatchBasket wb; if (open.TryGetValue(g.Key, out wb)) { double pl = g.Sum(p => p.Profit + p.Swap + p.Commission); wb.MinPl = Math.Min(wb.MinPl, pl); wb.MaxPl = Math.Max(wb.MaxPl, pl); wb.MaxGross = Math.Max(wb.MaxGross, g.Sum(p => p.Lots)); } }
            bool changed = ps.Count != pos.Count || ps.Any(p => !pos.ContainsKey(p.Ticket)) || os.Count != ords.Count || os.Any(o => { Mt5Order x; return !ords.TryGetValue(o.Ticket, out x) || Math.Abs(x.Price - o.Price) > 1e-9; });
            if (!changed) { foreach (var p in ps) pos[p.Ticket] = p; return; }
            var nowPos = ps.ToDictionary(p => p.Ticket);
            // closed
            var gone = pos.Values.Where(p => !nowPos.ContainsKey(p.Ticket)).ToList();
            foreach (var p in gone)
            {
                pos.Remove(p.Ticket); var key = KeyOf(p.Symbol, p.Magic); WatchBasket wb; open.TryGetValue(key, out wb);
                Mt5Quote cq; quotes.TryGetValue(p.Symbol, out cq); double cpp; per.TryGetValue(p.Symbol, out cpp); double exit = cq != null ? (p.Dir > 0 ? cq.Bid : cq.Ask) : p.Current;
                double pl = cq != null && cpp > 0 ? p.Dir * (exit - p.Open) * p.Lots * cpp + p.Swap + p.Commission : p.Profit + p.Swap + p.Commission;
                var e = new WatchEvent { Time = now, Key = key, Symbol = p.Symbol, Kind = "CLOSE", Side = p.Side, Lots = p.Lots, Price = exit, Ticket = p.Ticket, Magic = p.Magic, Comment = p.Comment, Basket = pl, Sl = p.Sl, Tp = p.Tp };
                bool tp = p.Tp > 0 && Math.Abs(exit - p.Tp) <= Math.Max(Math.Abs(p.Tp) * 0.00005, 1e-9) * 4, sl = p.Sl > 0 && Math.Abs(exit - p.Sl) <= Math.Max(Math.Abs(p.Sl) * 0.00005, 1e-9) * 4;
                e.Story = Hm(now) + "CLOSED " + p.Side + " " + p.Lots.ToString("0.##", IC) + " #" + p.Ticket + " (opened " + p.Time.ToString("HH:mm:ss", IC) + " @ " + P(p.Open) + ") ≈ " + P(exit) + ", its P/L ≈ " + M(e.Basket) + (tp ? " — by its TAKE PROFIT " + P(p.Tp) : sl ? " — by its STOP LOSS " + P(p.Sl) : "") + ".";
                if (wb != null) { wb.Result += e.Basket; e.Basket0 = wb.N; }
                Add(e);
            }
            foreach (var key in gone.Select(p => KeyOf(p.Symbol, p.Magic)).Distinct().ToList())
            {
                WatchBasket wb; if (!open.TryGetValue(key, out wb)) continue; if (ps.Any(p => KeyOf(p.Symbol, p.Magic) == key)) continue;
                wb.End = now; wb.Done = true; wb.Together = gone.Count(p => KeyOf(p.Symbol, p.Magic) == key) == wb.Legs || gone.Count(p => KeyOf(p.Symbol, p.Magic) == key) > 1; open.Remove(key); legs.Remove(key);
                Add(new WatchEvent { Time = now, Key = key, Symbol = wb.Symbol, Magic = wb.Magic, Kind = "BASKET CLOSED", Basket = wb.Result, Basket0 = wb.N,
                    Story = Hm(now) + "BASKET #" + wb.N + " FLAT — " + (wb.Together ? "closed together" : "the last leg closed") + " ≈ " + M(wb.Result) + " after " + wb.Legs + " leg(s) (" + wb.Opposite + " the other way, " + wb.Same + " the same way), worst " + M(wb.MinPl) + ", best " + M(wb.MaxPl) + ", most lots " + wb.MaxGross.ToString("0.##", IC) + ", " + (wb.End - wb.Start).ToString(@"hh\:mm\:ss", IC) + "." });
            }
            // opened
            foreach (var p in ps.Where(p => !pos.ContainsKey(p.Ticket)).OrderBy(p => p.Time).ThenBy(p => p.Ticket))
            {
                pos[p.Ticket] = p; Mt5Quote q; quotes.TryGetValue(p.Symbol, out q); double pp; per.TryGetValue(p.Symbol, out pp);
                var filled = ords.Values.FirstOrDefault(o => !os.Any(x => x.Ticket == o.Ticket) && string.Equals(o.Symbol, p.Symbol, StringComparison.OrdinalIgnoreCase) && o.Type.StartsWith(p.Side) && Math.Abs(o.Lots - p.Lots) < 1e-6);
                Opened(b, p, q, pp, now, ps, filled);
            }
            foreach (var p in ps) pos[p.Ticket] = p;
            // pending orders
            foreach (var o in os)
            {
                Mt5Order old; if (!ords.TryGetValue(o.Ticket, out old)) { Pending(b, o, quotes, per, now, "PENDING"); ords[o.Ticket] = o; }
                else if (Math.Abs(old.Price - o.Price) > 1e-9) { Pending(b, o, quotes, per, now, "PENDING MOVED"); ords[o.Ticket] = o; }
            }
            foreach (var o in ords.Values.Where(o => !os.Any(x => x.Ticket == o.Ticket)).ToList())
            {
                ords.Remove(o.Ticket); bool fill = ps.Any(p => string.Equals(p.Symbol, o.Symbol, StringComparison.OrdinalIgnoreCase) && o.Type.StartsWith(p.Side) && Math.Abs(o.Lots - p.Lots) < 1e-6 && p.Time >= o.Time);
                if (!fill) Add(new WatchEvent { Time = now, Key = o.Symbol + " • pending", Symbol = o.Symbol, Kind = "PENDING GONE", Side = o.Type, Lots = o.Lots, Price = o.Price, Ticket = o.Ticket, Story = Hm(now) + "PENDING " + o.Type + " " + o.Lots.ToString("0.##", IC) + " @ " + P(o.Price) + " #" + o.Ticket + " CANCELLED (not filled)." });
            }
            Version++;
        }
        static string Hm(DateTime t) { return t.ToString("dd MMM yy HH:mm:ss", IC) + "  "; }
        void Add(WatchEvent e) { Events.Add(e); if (Events.Count > 20000) Events.RemoveRange(0, 5000); }
        WatchBasket StartIfNeeded(Mt5Position p, DateTime now, bool adopted)
        {
            var key = KeyOf(p.Symbol, p.Magic); WatchBasket wb; if (open.TryGetValue(key, out wb)) return wb;
            wb = new WatchBasket { Key = key, Symbol = p.Symbol, Magic = p.Magic, Start = adopted ? p.Time : now, FirstSide = p.Side, N = Baskets.Count(x => x.Key == key) + 1 }; open[key] = wb; Baskets.Add(wb); legs[key] = new List<WatchEvent>();
            return wb;
        }

        // ---- the market around a price: which recent high / low (last N finished candles of M1 / M5 / M15) it sits on, and how far
        sealed class Lvl { public string Name; public double Price; }
        List<Lvl> Levels(IBroker b, string sym, DateTime now)
        {
            var res = new List<Lvl>();
            lock (b.State.Gate)
            {
                foreach (var tf in new[] { 1, 5, 15 })
                {
                    var se = b.State.SeriesOf(sym, tf); if (!b.IsSim && !se.Loaded && (DateTime.Now - se.AskedAt).TotalSeconds > 5) { se.AskedAt = DateTime.Now; b.Send("BARS|" + sym + "|" + tf + "|300"); }
                    var fin = new List<Mt5Bar>(); for (int i = se.Bars.Count - 1; i >= 0 && fin.Count < 20; i--) if (se.Bars[i].Time.AddMinutes(tf) <= now) fin.Add(se.Bars[i]);
                    foreach (var n in new[] { 1, 2, 3, 5, 10, 20 }) { if (fin.Count < n) break; var part = fin.Take(n).ToList(); res.Add(new Lvl { Name = "the HIGH of the last " + n + " " + Tf.Name(tf) + " candle" + (n == 1 ? "" : "s"), Price = part.Max(x => x.H) }); res.Add(new Lvl { Name = "the LOW of the last " + n + " " + Tf.Name(tf) + " candle" + (n == 1 ? "" : "s"), Price = part.Min(x => x.L) }); }
                }
            }
            return res;
        }
        // the closest level to a price (a buy that broke a high sits just above it, a sell just below a low)
        Tuple<string, double> Nearest(List<Lvl> lv, double price, string side, double spread)
        {
            bool buy = side.StartsWith("BUY"); var cands = lv.Where(l => buy ? l.Name.StartsWith("the HIGH") : l.Name.StartsWith("the LOW")).ToList(); if (cands.Count == 0) return Tuple.Create("", double.NaN);
            var best = cands.OrderBy(l => Math.Abs(price - l.Price)).ThenBy(l => l.Name.Length).First(); double d = price - best.Price;
            return Tuple.Create(best.Name, d);
        }

        void Opened(IBroker b, Mt5Position p, Mt5Quote q, double perPrice, DateTime now, List<Mt5Position> all, Mt5Order filled)
        {
            var key = KeyOf(p.Symbol, p.Magic); var wb = StartIfNeeded(p, now, false); var lst = legs[key];
            var e = new WatchEvent { Time = now, Key = key, Symbol = p.Symbol, Kind = "OPEN", Side = p.Side, Lots = p.Lots, Price = p.Open, Ticket = p.Ticket, Magic = p.Magic, Comment = p.Comment, Sl = p.Sl, Tp = p.Tp, Bid = q != null ? q.Bid : double.NaN, Ask = q != null ? q.Ask : double.NaN, Leg = lst.Count + 1, Basket0 = wb.N };
            var others = all.Where(x => KeyOf(x.Symbol, x.Magic) == key && x.Ticket != p.Ticket).ToList();
            e.Basket = others.Sum(x => x.Profit + x.Swap + x.Commission); e.Net = others.Sum(x => x.Dir * x.Lots);
            var prev = lst.LastOrDefault(); var first = lst.FirstOrDefault();
            e.Role = prev == null ? "FIRST" : prev.Side == p.Side ? "SAME WAY" : "OPPOSITE";
            if (prev != null)
            {
                int pd = prev.Side == "BUY" ? 1 : -1; e.FromLast = -(p.Open - prev.Price) * pd;   // + = the price went AGAINST the last leg
                e.FromLastMoney = perPrice > 0 ? -(p.Open - prev.Price) * pd * prev.Lots * perPrice : double.NaN; e.SecSinceLast = (p.Time - prev.Time).TotalSeconds; e.LotRatio = prev.Lots > 0 ? p.Lots / prev.Lots : double.NaN;
            }
            if (first != null && first.Lots > 0) e.LotsVsFirst = p.Lots / first.Lots;
            double spread = q != null ? Math.Max(0, q.Ask - q.Bid) : 0; var lv = Levels(b, p.Symbol, now); double at = p.Side == "BUY" ? p.Open - spread : p.Open;   // the bid that triggered it
            var cand = lv.Select(x => new Lvl { Name = x.Name, Price = x.Price }).ToList(); if (prev != null && prev.Levels != null) cand.AddRange(prev.Levels.Select(x => new Lvl { Name = x.Item1 + " when the last leg opened", Price = x.Item2 }));
            var nm = Nearest(cand, at, p.Side, spread); e.Match = nm.Item1; e.MatchDist = nm.Item2; e.Levels = lv.Select(x => Tuple.Create(x.Name, x.Price)).ToList();
            // the same rule seen from both sides: a buy breaks the HIGH, a sell the LOW → one name
            double tol = Math.Max(3 * spread, Math.Abs(at) * 0.0001); e.Hits = cand.Where(x => (p.Side == "BUY" ? x.Name.StartsWith("the HIGH") : x.Name.StartsWith("the LOW")) && Math.Abs(at - x.Price) <= tol).Select(x => x.Name.Replace("the HIGH of", "the HIGH (buy) / LOW (sell) of").Replace("the LOW of", "the HIGH (buy) / LOW (sell) of")).Distinct().ToList();
            wb.Legs++; if (e.Role == "OPPOSITE") wb.Opposite++; else if (e.Role == "SAME WAY") wb.Same++; if (filled != null) wb.Pendings++;
            e.Story = Hm(now) + (e.Role == "FIRST" ? "BASKET #" + wb.N + " — FIRST LEG " : e.Role == "OPPOSITE" ? "LEG " + e.Leg + " THE OTHER WAY (a hedge) " : "LEG " + e.Leg + " THE SAME WAY (adding) ")
                + p.Side + " " + p.Lots.ToString("0.##", IC) + " @ " + P(p.Open) + " #" + p.Ticket + (p.Comment != "" ? " \"" + p.Comment + "\"" : "") + (filled != null ? " — a filled pending " + filled.Type + " @ " + P(filled.Price) : " — at the market")
                + ". Bid " + P(e.Bid) + " / ask " + P(e.Ask) + ". The price sits " + (double.IsNaN(e.MatchDist) ? "—" : (e.MatchDist >= 0 ? P(e.MatchDist) + " above " : P(-e.MatchDist) + " below ")) + e.Match + "."
                + (prev != null ? " The last leg was " + prev.Side + " " + prev.Lots.ToString("0.##", IC) + " @ " + P(prev.Price) + " " + TimeSpan.FromSeconds(Math.Max(0, e.SecSinceLast)).ToString(@"hh\:mm\:ss", IC) + " ago; the price moved " + P(Math.Abs(e.FromLast)) + (e.FromLast >= 0 ? " AGAINST it (" : " in its favour (") + M(e.FromLastMoney) + " on that leg). Lots × " + e.LotRatio.ToString("0.##", IC) + " the last leg, × " + e.LotsVsFirst.ToString("0.##", IC) + " the first." : "")
                + (others.Count > 0 ? " The basket was " + M(e.Basket) + " with net " + (e.Net >= 0 ? "+" : "") + e.Net.ToString("0.##", IC) + " lots." : "")
                + (p.Tp > 0 ? " TP " + P(p.Tp) + " (" + P(Math.Abs(p.Tp - p.Open)) + " away)." : "") + (p.Sl > 0 ? " SL " + P(p.Sl) + " (" + P(Math.Abs(p.Sl - p.Open)) + " away)." : "");
            lst.Add(e); Add(e);
        }
        void Pending(IBroker b, Mt5Order o, Dictionary<string, Mt5Quote> quotes, Dictionary<string, double> per, DateTime now, string kind)
        {
            Mt5Quote q; quotes.TryGetValue(o.Symbol, out q); var lv = Levels(b, o.Symbol, now); var nm = Nearest(lv, o.Price, o.Type, q != null ? q.Ask - q.Bid : 0);
            var key = open.Keys.FirstOrDefault(k => k.StartsWith(o.Symbol + " •")); List<WatchEvent> lst = null; if (key != null) legs.TryGetValue(key, out lst); var last = lst != null ? lst.LastOrDefault() : null;
            var e = new WatchEvent { Time = now, Key = key ?? o.Symbol + " • pending", Symbol = o.Symbol, Kind = kind, Side = o.Type, Lots = o.Lots, Price = o.Price, Ticket = o.Ticket, Sl = o.Sl, Tp = o.Tp, Bid = q != null ? q.Bid : double.NaN, Ask = q != null ? q.Ask : double.NaN, Match = nm.Item1, MatchDist = nm.Item2 };
            if (last != null) { int pd = last.Side == "BUY" ? 1 : -1; e.FromLast = -(o.Price - last.Price) * pd; e.LotRatio = last.Lots > 0 ? o.Lots / last.Lots : double.NaN; e.Role = o.Type.StartsWith(last.Side) ? "SAME WAY" : "OPPOSITE"; }
            e.Story = Hm(now) + (kind == "PENDING" ? "PENDING ORDER " : "PENDING ORDER MOVED ") + o.Type + " " + o.Lots.ToString("0.##", IC) + " @ " + P(o.Price) + " #" + o.Ticket + ": " + (double.IsNaN(e.MatchDist) ? "" : (e.MatchDist >= 0 ? P(e.MatchDist) + " above " : P(-e.MatchDist) + " below ") + e.Match + "; ")
                + "the price is " + P(e.Bid) + (last != null ? "; " + P(Math.Abs(e.FromLast)) + (e.FromLast >= 0 ? " against" : " beyond") + " the last leg " + last.Side + " " + last.Lots.ToString("0.##", IC) + " @ " + P(last.Price) + " (" + (e.Role == "OPPOSITE" ? "a HEDGE waiting" : "an ADD waiting") + ", lots × " + e.LotRatio.ToString("0.##", IC) + ")" : "") + ".";
            Add(e);
        }

        // ---- the RULES of one bot (key), from what it did
        public List<string> Rules(string key)
        {
            var L = new List<string>(); var ev = Events.Where(e => e.Key == key).ToList(); var bs = Baskets.Where(x => x.Key == key).ToList(); var done = bs.Where(x => x.Done).ToList();
            var opens = ev.Where(e => e.Kind == "OPEN").ToList(); var first = opens.Where(e => e.Role == "FIRST").ToList(); var opp = opens.Where(e => e.Role == "OPPOSITE").ToList(); var same = opens.Where(e => e.Role == "SAME WAY").ToList();
            L.Add("BOT " + key + (opens.Count > 0 && opens.Any(e => e.Comment != "") ? "   comments: " + string.Join(", ", opens.Select(e => e.Comment).Where(c => c != "").Distinct().Take(5)) : ""));
            L.Add("seen: " + bs.Count + " baskets (" + done.Count + " finished), " + opens.Count + " legs: " + first.Count + " first, " + opp.Count + " the other way (hedges), " + same.Count + " the same way (adds); " + ev.Count(e => e.Kind == "PENDING") + " pending orders placed");
            if (first.Count == 0) { L.Add("\nnot enough yet — the rules appear after the first baskets"); return L; }
            L.Add("");
            L.Add("ENTRY (the first leg)");
            L.Add("  lots: " + Common(first.Select(e => e.Lots.ToString("0.##", IC))) + "   sides: " + first.Count(e => e.Side == "BUY") + " BUY / " + first.Count(e => e.Side == "SELL") + " SELL");
            L.Add("  where: " + Matches(first) + "   at the market: " + Pct(first.Count(e => !e.Story.Contains("filled pending")), first.Count));
            L.Add("  hours (server): " + string.Join("  ", first.GroupBy(e => e.Time.Hour).OrderByDescending(g => g.Count()).Take(6).Select(g => g.Key.ToString("00", IC) + ":00 ×" + g.Count())));
            if (opp.Count > 0)
            {
                L.Add(""); L.Add("THE OTHER WAY (hedges)");
                L.Add("  lots: " + Common(opp.Select(e => e.Lots.ToString("0.##", IC))) + "   × the last leg: " + Spread(opp.Select(e => e.LotRatio)) + "   × the first: " + Spread(opp.Select(e => e.LotsVsFirst)));
                L.Add("  price AGAINST the last leg: " + Spread(opp.Select(e => e.FromLast)) + "   $ on that leg: " + Spread(opp.Select(e => e.FromLastMoney)) + "   basket $: " + Spread(opp.Select(e => e.Basket)));
                L.Add("  time since the last leg (s): " + Spread(opp.Select(e => e.SecSinceLast)));
                L.Add("  where: " + Matches(opp) + "   from a pending order: " + Pct(opp.Count(e => e.Story.Contains("filled pending")), opp.Count));
                L.Add("  ⇒ " + Guess(opp));
            }
            if (same.Count > 0)
            {
                L.Add(""); L.Add("THE SAME WAY (adds)");
                L.Add("  lots: " + Common(same.Select(e => e.Lots.ToString("0.##", IC))) + "   × the last leg: " + Spread(same.Select(e => e.LotRatio)));
                L.Add("  price vs the last leg (+ = against, − = in favour): " + Spread(same.Select(e => e.FromLast)) + "   basket $: " + Spread(same.Select(e => e.Basket)));
                L.Add("  where: " + Matches(same)); L.Add("  ⇒ " + (same.Count(e => e.FromLast < 0) > same.Count / 2 ? "adds to the WINNER (the price moved in favour)" : "AVERAGES DOWN (the price moved against)") + "; " + Guess(same));
            }
            if (done.Count > 0)
            {
                L.Add(""); L.Add("THE EXIT (the basket flat again)");
                L.Add("  result: " + Spread(done.Select(x => x.Result)) + "   closed together: " + Pct(done.Count(x => x.Together), done.Count) + "   by a TP on the leg: " + Pct(ev.Count(e => e.Kind == "CLOSE" && e.Story.Contains("TAKE PROFIT")), Math.Max(1, ev.Count(e => e.Kind == "CLOSE"))));
                var wins = done.Where(x => x.Result > 0).ToList(); if (wins.Count >= 2) L.Add("  ⇒ " + (Cv(wins.Select(x => x.Result)) < 0.25 ? "TAKE PROFIT ≈ " + M(Med(wins.Select(x => x.Result))) + " on the WHOLE basket (the winning baskets end within ±" + (100 * Cv(wins.Select(x => x.Result))).ToString("0", IC) + "%)" : "no fixed $ take profit (the winning baskets end anywhere from " + M(wins.Min(x => x.Result)) + " to " + M(wins.Max(x => x.Result)) + ")"));
                L.Add("  legs a basket: " + Spread(done.Select(x => (double)x.Legs)) + "   worst drawdown: " + M(done.Min(x => x.MinPl)) + "   most lots: " + done.Max(x => x.MaxGross).ToString("0.##", IC));
                var gaps = new List<double>(); var sides = 0; var cnt = 0; for (int i = 1; i < done.Count; i++) { gaps.Add((done[i].Start - done[i - 1].End).TotalSeconds); cnt++; if (done[i].FirstSide == done[i - 1].FirstSide) sides++; }
                if (gaps.Count > 0) L.Add("  the next basket: " + Spread(gaps) + " s later; the same first side " + Pct(sides, cnt));
            }
            return L;
        }
        static double Med(IEnumerable<double> v) { var l = v.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); return l.Count == 0 ? double.NaN : l[l.Count / 2]; }
        static double Cv(IEnumerable<double> v) { var l = v.Where(x => !double.IsNaN(x)).ToList(); if (l.Count < 2) return double.NaN; double m = l.Average(); return Math.Abs(m) < 1e-12 ? double.NaN : Math.Sqrt(l.Sum(x => (x - m) * (x - m)) / l.Count) / Math.Abs(m); }
        static string Spread(IEnumerable<double> v) { var l = v.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); if (l.Count == 0) return "—"; return "median " + l[l.Count / 2].ToString("0.###", IC) + " (" + l.First().ToString("0.###", IC) + " … " + l.Last().ToString("0.###", IC) + ", n " + l.Count + ")"; }
        static string Common(IEnumerable<string> v) { return string.Join(", ", v.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(4).Select(g => g.Key + " ×" + g.Count())); }
        static string Pct(int a, int n) { return n == 0 ? "—" : (100.0 * a / n).ToString("0", IC) + "%"; }
        // which recent high / low the legs sat on (within a few points), the most common first
        static string Matches(List<WatchEvent> l) { var g = l.SelectMany(e => e.Hits).GroupBy(x => x).OrderByDescending(x => x.Count()).ThenBy(x => x.Key.Length).Take(3).Select(x => x.Key + " " + Pct(x.Count(), l.Count)); var s = string.Join("; ", g); return s == "" ? "no recent high / low within 3 spreads" : s; }
        // the most consistent explanation of an add: a fixed price step, a fixed $ loss, a fixed time, or a recent high / low
        static string Guess(List<WatchEvent> l)
        {
            var c = new List<Tuple<double, string>>();
            double cp = Cv(l.Select(e => Math.Abs(e.FromLast))), cm = Cv(l.Select(e => Math.Abs(e.FromLastMoney))), ct = Cv(l.Select(e => e.SecSinceLast)), cb = Cv(l.Select(e => Math.Abs(e.Basket)));
            if (!double.IsNaN(cp)) c.Add(Tuple.Create(cp, "a FIXED PRICE STEP of ≈ " + Med(l.Select(e => Math.Abs(e.FromLast))).ToString("0.###", IC) + " from the last leg (±" + (100 * cp).ToString("0", IC) + "%)"));
            if (!double.IsNaN(cm)) c.Add(Tuple.Create(cm, "a FIXED $ LOSS of ≈ " + M(-Med(l.Select(e => Math.Abs(e.FromLastMoney)))) + " on the last leg (±" + (100 * cm).ToString("0", IC) + "%)"));
            if (!double.IsNaN(cb)) c.Add(Tuple.Create(cb, "the BASKET at ≈ " + M(-Med(l.Select(e => Math.Abs(e.Basket)))) + " (±" + (100 * cb).ToString("0", IC) + "%)"));
            if (!double.IsNaN(ct)) c.Add(Tuple.Create(ct * 1.5, "a TIMER of ≈ " + Med(l.Select(e => e.SecSinceLast)).ToString("0", IC) + " s (±" + (100 * ct).ToString("0", IC) + "%)"));
            var lv = l.SelectMany(e => e.Hits).GroupBy(x => x).OrderByDescending(x => x.Count()).ThenBy(x => x.Key.Length).FirstOrDefault();
            if (lv != null) { double share = (double)lv.Count() / l.Count; c.Add(Tuple.Create(share >= 0.6 ? 0.1 / share : 1.0, "the BREAK of " + lv.Key + " (" + Pct(lv.Count(), l.Count) + " of them within 3 spreads)")); }
            if (c.Count == 0 || l.Count < 3) return "most likely: needs at least 3 of them to tell";
            var best = c.OrderBy(x => x.Item1).ToList(); return "most likely " + best[0].Item2 + (best.Count > 1 ? "; next: " + best[1].Item2 : "");
        }
    }
}
