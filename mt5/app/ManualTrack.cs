// SpasiX — ✋ MANUAL tab: every trade on the account, read only (user's request: "when the hedging automation is OFF and I trade in the MT5
// terminal, every iteration, its profit / loss, the flow and the results are tracked and saved"). It NEVER sends, closes or changes anything:
// it reads the positions and the deals history (SpasiX, the MT5 terminal by hand, any other EA) and groups them per symbol from FLAT to FLAT.
// The 🛡 HEDGE automation's own iterations are marked "→ HEDGE #n" and not counted twice (unless COUNT THE HEDGE'S TOO is on).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class ManualLeg
    {
        public DateTime Time; public string Kind = "", Side = "", Source = "", Reason = ""; public double Lots, Price, OpenPrice = double.NaN, Pl; public long Position, Deal;
    }
    public sealed class ManualIter
    {
        public int N; public string Symbol = "", HedgeRef = "", How = "", Key = ""; public DateTime Start, End, LastSeen; public bool Done, Rebuilt, Saved;
        public readonly List<ManualLeg> Legs = new List<ManualLeg>(); public readonly List<string> Story = new List<string>();
        public int Opens, AddOns; public double MaxNet, MaxGross, Worst, Best, Result, Total;
        public bool ByHedge { get { return HedgeRef != ""; } }
        public double Minutes(DateTime now) { return Math.Max(0, ((Done ? End : now) - Start).TotalMinutes); }
    }
    public sealed class ManualTracker
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const long SpasixMagic = 770077;
        public readonly List<ManualIter> Iters = new List<ManualIter>();
        public bool CountHedge; public int Version;
        readonly HashSet<long> seen = new HashSet<long>();
        readonly Dictionary<string, ManualIter> open = new Dictionary<string, ManualIter>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<long, double> posLots = new Dictionary<long, double>();
        readonly Dictionary<long, Mt5Deal> ins = new Dictionary<long, Mt5Deal>();
        readonly Dictionary<long, ManualIter> posIter = new Dictionary<long, ManualIter>();

        static string M(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("0.00", IC); }
        // who placed it: SpasiX (its magic), the MT5 terminal by hand (desktop / mobile / web), another EA (its magic), the broker (TP / SL / stop-out)
        public static string SourceOf(Mt5Deal d)
        {
            if (d.Reason == "TP") return "broker TP"; if (d.Reason == "SL") return "broker SL"; if (d.Reason == "STOPOUT") return "stop-out";
            if (d.Magic == SpasixMagic) return "SpasiX";
            if (d.Reason == "DESKTOP") return "MT5 by hand"; if (d.Reason == "MOBILE") return "MT5 mobile"; if (d.Reason == "WEB") return "MT5 web";
            if (d.Magic != 0 || d.Reason == "EA") return "EA #" + d.Magic.ToString(IC);
            return "SpasiX";   // the DEMO / BACKTEST accounts: every order comes from SpasiX
        }
        public IEnumerable<ManualIter> Counted { get { return Iters.Where(x => x.Done && (CountHedge || !x.ByHedge)); } }
        public ManualIter OpenOf(string sym) { ManualIter it; return open.TryGetValue(sym, out it) ? it : null; }

        // one look at the account. The iterations are REBUILT from the deals (in time order) whenever the deals or the positions change — the
        // history can arrive after the positions (at start-up) and must land in the right place; the live numbers (worst / best / max lots, seen
        // only while the trade was open) and the → HEDGE marks are kept per iteration across the rebuilds
        sealed class Live { public double Worst, Best, MaxGross, MaxNet, Total; public DateTime LastSeen; }
        readonly Dictionary<string, Live> live = new Dictionary<string, Live>();
        readonly Dictionary<string, string> hedgeRefs = new Dictionary<string, string>();
        readonly List<ManualIter> saved = new List<ManualIter>(); readonly HashSet<long> savedSeen = new HashSet<long>();
        string sig = "";
        public void Update(IList<Mt5Position> pos, IList<Mt5Deal> deals, DateTime now, bool hedgeRunning, string hedgeSym, int hedgeIter)
        {
            var trade = deals.Where(x => x.IsTrade && !savedSeen.Contains(x.Ticket)).OrderBy(x => x.Time).ThenBy(x => x.Ticket).ToList();
            string sg = trade.Count + "|" + (trade.Count > 0 ? trade[trade.Count - 1].Ticket : 0) + "|" + string.Join(",", pos.Select(p => p.Ticket).OrderBy(t => t));
            if (sg != sig)
            {
                sig = sg; Iters.Clear(); Iters.AddRange(saved); open.Clear(); posLots.Clear(); ins.Clear(); posIter.Clear(); seen.Clear();
                foreach (var d in trade) Apply(d, hedgeRunning, hedgeSym, hedgeIter);
                foreach (var p in pos.OrderBy(p => p.Time))
                {   // positions with no history behind them (opened before the deals MT5 sent): taken from the positions themselves
                    if (posLots.ContainsKey(p.Ticket)) continue;
                    var it = OpenOf(p.Symbol) ?? NewIter(p.Symbol, p.Time, "P" + p.Ticket, hedgeRunning, hedgeSym, hedgeIter);
                    posLots[p.Ticket] = p.Lots; posIter[p.Ticket] = it; it.Opens++; if (it.Opens > 1) it.AddOns++; it.Rebuilt = true;
                    it.Legs.Add(new ManualLeg { Time = p.Time, Kind = "OPEN", Side = p.Side, Lots = p.Lots, Price = p.Open, Position = p.Ticket, Source = p.Magic == SpasixMagic ? "SpasiX" : p.Magic != 0 ? "EA #" + p.Magic : "MT5 / SpasiX" });
                    it.Story.Add(p.Time.ToString("dd MMM HH:mm", IC) + "  " + p.Side + " " + p.Lots.ToString("0.##", IC) + " @ " + p.Open.ToString("0.#####", IC) + " (open before the history MT5 sent)");
                }
                foreach (var it in Iters) { Live lv; if (it.Key != "" && live.TryGetValue(it.Key, out lv)) { it.Worst = Math.Min(it.Worst, lv.Worst); it.Best = Math.Max(it.Best, lv.Best); it.MaxGross = Math.Max(it.MaxGross, lv.MaxGross); it.MaxNet = Math.Max(it.MaxNet, lv.MaxNet); it.LastSeen = lv.LastSeen; it.Total = lv.Total; if (it.Done && !it.Saved) it.Rebuilt = false; } }
            }
            foreach (var it in open.Values)
            {
                var ps = pos.Where(p => string.Equals(p.Symbol, it.Symbol, StringComparison.OrdinalIgnoreCase)).ToList(); if (ps.Count == 0) continue;
                it.Total = HedgeAuto.OpenPl(ps, it.Symbol) + HedgeAuto.ClosedSince(deals, ps, it.Symbol, it.Start);
                it.Worst = Math.Min(it.Worst, it.Total); it.Best = Math.Max(it.Best, it.Total);
                it.MaxGross = Math.Max(it.MaxGross, ps.Sum(p => p.Lots)); it.MaxNet = Math.Max(it.MaxNet, Math.Abs(ps.Sum(p => p.Dir * p.Lots))); it.LastSeen = now;
                Live lv; if (!live.TryGetValue(it.Key, out lv)) { lv = new Live(); live[it.Key] = lv; } lv.Worst = it.Worst; lv.Best = it.Best; lv.MaxGross = it.MaxGross; lv.MaxNet = it.MaxNet; lv.LastSeen = now; lv.Total = it.Total;
            }
            Version++;
        }
        ManualIter NewIter(string sym, DateTime t, string key, bool hedgeRunning, string hedgeSym, int hedgeIter)
        {
            var it = new ManualIter { N = Iters.Count + 1, Symbol = sym, Start = t, Key = sym + "|" + key }; Iters.Add(it); open[sym] = it;
            string hr; if (!hedgeRefs.TryGetValue(it.Key, out hr)) { hr = hedgeRunning && string.Equals(hedgeSym, sym, StringComparison.OrdinalIgnoreCase) ? "→ HEDGE #" + hedgeIter.ToString(IC) : ""; hedgeRefs[it.Key] = hr; }
            it.HedgeRef = hr; return it;
        }
        void Apply(Mt5Deal d, bool hedgeRunning, string hedgeSym, int hedgeIter)
        {
            seen.Add(d.Ticket); var it = OpenOf(d.Symbol); double money = d.Profit + d.Commission + d.Swap;
            if (d.Entry == "IN")
            {
                if (it == null) it = NewIter(d.Symbol, d.Time, d.Ticket.ToString(IC), hedgeRunning, hedgeSym, hedgeIter);
                ins[d.Position] = d; double l0; posLots.TryGetValue(d.Position, out l0); posLots[d.Position] = Math.Round(l0 + d.Lots, 2); posIter[d.Position] = it;
                it.Opens++; if (it.Opens > 1) it.AddOns++; it.Result += money;
                var leg = new ManualLeg { Time = d.Time, Kind = "OPEN", Side = d.Type, Lots = d.Lots, Price = d.Price, Pl = money, Position = d.Position, Deal = d.Ticket, Source = SourceOf(d), Reason = d.Reason }; it.Legs.Add(leg);
                double net = Net(it); it.MaxNet = Math.Max(it.MaxNet, Math.Abs(net)); it.MaxGross = Math.Max(it.MaxGross, Gross(it));
                it.Story.Add(d.Time.ToString("dd MMM HH:mm", IC) + "  " + d.Type + " " + d.Lots.ToString("0.##", IC) + " @ " + d.Price.ToString("0.#####", IC) + " (" + leg.Source + ")" + (it.Opens > 1 ? (Math.Sign(net) != 0 && Math.Sign(net) == (d.Type == "BUY" ? 1 : -1) && Math.Abs(net) > d.Lots - 1e-9 ? " add" : " hedge") : " entry") + " · net " + net.ToString("+0.##;−0.##;0", IC) + " lot, gross " + Gross(it).ToString("0.##", IC));
                return;
            }
            if (!(d.Entry == "OUT" || d.Entry == "OUTBY" || d.Entry == "INOUT")) return;
            ManualIter pi; if (!posIter.TryGetValue(d.Position, out pi)) pi = it;
            if (pi == null) return;   // the close of a trade opened before the history we have: nothing to attach it to
            it = pi; double lots0; posLots.TryGetValue(d.Position, out lots0); posLots[d.Position] = Math.Round(Math.Max(0, lots0 - d.Lots), 2);
            Mt5Deal inn; ins.TryGetValue(d.Position, out inn); var side = d.Type == "BUY" ? "SELL" : "BUY";
            it.Result += money; it.Worst = Math.Min(it.Worst, it.Result); it.Best = Math.Max(it.Best, it.Result);
            var cl = new ManualLeg { Time = d.Time, Kind = "CLOSE", Side = side, Lots = d.Lots, Price = d.Price, OpenPrice = inn != null ? inn.Price : double.NaN, Pl = money, Position = d.Position, Deal = d.Ticket, Source = SourceOf(d), Reason = d.Reason }; it.Legs.Add(cl);
            bool flat = it.Legs.Where(l => l.Kind == "OPEN").Select(l => l.Position).Distinct().All(p => { double v; return !posLots.TryGetValue(p, out v) || v < 0.005; });
            it.Story.Add(d.Time.ToString("dd MMM HH:mm", IC) + "  CLOSE " + side + " " + d.Lots.ToString("0.##", IC) + (double.IsNaN(cl.OpenPrice) ? "" : " " + cl.OpenPrice.ToString("0.#####", IC) + " →") + " @ " + d.Price.ToString("0.#####", IC) + " " + M(money) + " (" + cl.Source + ")" + (lots0 - d.Lots > 0.005 ? " partial" : "") + " · banked so far " + M(it.Result) + (flat ? "" : " · net " + Net(it).ToString("+0.##;−0.##;0", IC) + " lot open"));
            if (!flat) return;
            it.Done = true; it.End = d.Time; open.Remove(it.Symbol);
            it.Worst = Math.Min(it.Worst, it.Result); it.Best = Math.Max(it.Best, it.Result); if (it.LastSeen == DateTime.MinValue) it.Rebuilt = true;   // never seen live: the drawdown is from the closed legs only
            var exits = it.Legs.Where(l => l.Kind == "CLOSE").ToList(); int exitTimes = exits.Select(l => l.Time).Distinct().Count();
            it.How = d.Reason == "TP" ? "TP hit at the broker" : d.Reason == "SL" ? "SL hit at the broker" : d.Reason == "STOPOUT" ? "stop-out" : (exitTimes > 1 ? "closed in parts" : "closed at once") + " (" + cl.Source + ")";
            it.Story.Add(d.Time.ToString("dd MMM HH:mm", IC) + "  ■ FLAT — " + it.How + " · result " + M(it.Result) + (it.LastSeen != DateTime.MinValue ? " · worst " + M(it.Worst) + " · best " + M(it.Best) : "") + " · " + ((int)it.Minutes(d.Time)).ToString(IC) + " min");
        }
        double Net(ManualIter it) { return Math.Round(it.Legs.Where(l => l.Kind == "OPEN").Select(l => l.Position).Distinct().Sum(p => { double v; posLots.TryGetValue(p, out v); var o = it.Legs.First(l => l.Kind == "OPEN" && l.Position == p); return (o.Side == "BUY" ? 1 : -1) * v; }), 2); }
        double Gross(ManualIter it) { return Math.Round(it.Legs.Where(l => l.Kind == "OPEN").Select(l => l.Position).Distinct().Sum(p => { double v; posLots.TryGetValue(p, out v); return v; }), 2); }

        // ---- the numbers for the summary cards
        public sealed class Summary { public int N, Won, Lost; public double Net, Today, AvgDd, WorstDd, AvgMin, Best, Worst; public string ByDay = "", ByHour = "", BySymbol = ""; }
        public Summary Sum(DateTime now)
        {
            var d = Counted.ToList(); var s = new Summary { N = d.Count, Won = d.Count(x => x.Result > 0), Lost = d.Count(x => x.Result <= 0), Net = d.Sum(x => x.Result), Today = d.Where(x => x.End.Date == now.Date).Sum(x => x.Result) };
            if (d.Count > 0) { s.AvgDd = d.Average(x => x.Worst); s.WorstDd = d.Min(x => x.Worst); s.AvgMin = d.Average(x => x.Minutes(now)); s.Best = d.Max(x => x.Result); s.Worst = d.Min(x => x.Result); }
            s.ByDay = string.Join("  ", d.GroupBy(x => x.End.Date).OrderByDescending(g => g.Key).Take(7).Select(g => g.Key.ToString("dd MMM", IC) + " " + M(g.Sum(x => x.Result)) + " (" + g.Count() + ")"));
            s.ByHour = string.Join("  ", d.GroupBy(x => x.Start.Hour).OrderBy(g => g.Key).Select(g => g.Key.ToString("00", IC) + "h " + M(g.Sum(x => x.Result))));
            s.BySymbol = string.Join("  ", d.GroupBy(x => x.Symbol).Select(g => g.Key + " " + M(g.Sum(x => x.Result)) + " (" + g.Count() + ")"));
            return s;
        }

        // ---- saved per account + mode: <prefix>-iterations.csv (one row per finished iteration) + <prefix>-legs.csv + <prefix>-story.txt
        public static readonly string IterHeader = "n,symbol,start,end,minutes,opens,addons,max_net,max_gross,worst,best,result,how,hedge,rebuilt";
        public static readonly string LegHeader = "n,time,kind,side,lots,open_price,price,money,source,reason,position,deal";
        public void Save(string prefix)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(prefix));
                var done = Iters.Where(x => x.Done).ToList(); Func<DateTime, string> T = t => t.ToString("yyyy-MM-dd HH:mm:ss", IC); Func<double, string> F = v => double.IsNaN(v) ? "" : v.ToString("0.##", IC);
                File.WriteAllLines(prefix + "-iterations.csv", new[] { IterHeader }.Concat(done.Select(x => string.Join(",", x.N, x.Symbol, T(x.Start), T(x.End), F(x.Minutes(x.End)), x.Opens, x.AddOns, F(x.MaxNet), F(x.MaxGross), F(x.Worst), F(x.Best), F(x.Result), "\"" + x.How + "\"", x.HedgeRef, x.Rebuilt ? 1 : 0))));
                File.WriteAllLines(prefix + "-legs.csv", new[] { LegHeader }.Concat(done.SelectMany(x => x.Legs.Select(l => string.Join(",", x.N, T(l.Time), l.Kind, l.Side, F(l.Lots), F(l.OpenPrice), F(l.Price), F(l.Pl), "\"" + l.Source + "\"", l.Reason, l.Position, l.Deal)))));
                File.WriteAllLines(prefix + "-story.txt", done.SelectMany(x => new[] { "#" + x.N + " " + x.Symbol + (x.HedgeRef != "" ? " " + x.HedgeRef : "") }.Concat(x.Story.Select(s => "   " + s))));
            }
            catch { }
        }
        public void Load(string prefix)
        {
            try
            {
                var fi = prefix + "-iterations.csv"; if (!File.Exists(fi)) return; var byN = new Dictionary<int, ManualIter>();
                foreach (var l in File.ReadAllLines(fi).Skip(1))
                {
                    var a = Split(l); if (a.Count < 15) continue; Func<int, double> D = i => a[i] == "" ? double.NaN : double.Parse(a[i], IC); Func<int, DateTime> T = i => DateTime.ParseExact(a[i], "yyyy-MM-dd HH:mm:ss", IC);
                    var it = new ManualIter { N = int.Parse(a[0], IC), Symbol = a[1], Start = T(2), End = T(3), Opens = (int)D(5), AddOns = (int)D(6), MaxNet = D(7), MaxGross = D(8), Worst = D(9), Best = D(10), Result = D(11), How = a[12], HedgeRef = a[13], Rebuilt = a[14] == "1", Done = true };
                    it.Saved = true; saved.Add(it); Iters.Add(it); byN[it.N] = it;
                }
                var fl = prefix + "-legs.csv";
                if (File.Exists(fl)) foreach (var l in File.ReadAllLines(fl).Skip(1))
                {
                    var a = Split(l); if (a.Count < 12) continue; ManualIter it; if (!byN.TryGetValue(int.Parse(a[0], IC), out it)) continue; Func<int, double> D = i => a[i] == "" ? double.NaN : double.Parse(a[i], IC);
                    var leg = new ManualLeg { Time = DateTime.ParseExact(a[1], "yyyy-MM-dd HH:mm:ss", IC), Kind = a[2], Side = a[3], Lots = D(4), OpenPrice = D(5), Price = D(6), Pl = D(7), Source = a[8], Reason = a[9], Position = long.Parse(a[10], IC), Deal = long.Parse(a[11], IC) };
                    it.Legs.Add(leg); if (leg.Deal != 0) savedSeen.Add(leg.Deal);
                }
                var fs = prefix + "-story.txt"; ManualIter cur = null;
                if (File.Exists(fs)) foreach (var l in File.ReadAllLines(fs)) { if (l.StartsWith("#")) { int n; int sp = l.IndexOf(' '); cur = sp > 1 && int.TryParse(l.Substring(1, sp - 1), out n) && byN.ContainsKey(n) ? byN[n] : null; } else if (cur != null) cur.Story.Add(l.Trim()); }
                Version++;
            }
            catch { }
        }
        public void Reset() { Iters.Clear(); open.Clear(); posLots.Clear(); ins.Clear(); posIter.Clear(); seen.Clear(); saved.Clear(); savedSeen.Clear(); live.Clear(); hedgeRefs.Clear(); sig = ""; Version++; }
        public void Clear(string prefix) { Reset(); foreach (var f in new[] { "-iterations.csv", "-legs.csv", "-story.txt" }) try { File.Delete(prefix + f); } catch { } }
        // after a CLEAR the deals already in the history are not counted again
        public void MarkSeen(IEnumerable<Mt5Deal> deals) { foreach (var d in deals) savedSeen.Add(d.Ticket); sig = ""; }
        static List<string> Split(string l)
        {
            var r = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
            foreach (var c in l) { if (c == '"') q = !q; else if (c == ',' && !q) { r.Add(cur.ToString()); cur.Clear(); } else cur.Append(c); }
            r.Add(cur.ToString()); return r;
        }
    }
}
