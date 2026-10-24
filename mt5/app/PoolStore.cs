// SpasiX — the POOL's storage (no UI): the days and trades of a backtest TEST saved on this PC (%AppData%\SpasiX\MT5\backtest-pool\TEST\
// days.txt + trades.txt), built from the simulated account's deals, grouped by day / week / month / year, and the 🌐 HTML tracker page.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace KeystoneMT5
{
    // one saved day / one saved trade of a test
    public sealed class PoolDay { public DateTime Day; public string Syms = "", Kind = ""; public int Trades, Wins; public double Net, Gross, Comm, Swap, Lots, Best, Worst, EndBal; public DateTime SavedAt; }
    public sealed class PoolTrade { public DateTime Day, OpenT, CloseT; public string Sym = "", Side = "", Reason = ""; public double Lots, OpenP, CloseP, Profit, Comm, Swap; public double Net { get { return Profit + Comm + Swap; } } }

    public static class BtPool
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpasiX", "MT5", "backtest-pool");
        static string Safe(string n) { foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return n.Trim(); }
        public static string DirOf(string test) { return Path.Combine(Root, Safe(test)); }
        public static List<string> Tests() { try { return Directory.Exists(Root) ? Directory.GetDirectories(Root).Select(Path.GetFileName).OrderBy(x => x).ToList() : new List<string>(); } catch { return new List<string>(); } }
        public static void Create(string test) { Directory.CreateDirectory(DirOf(test)); }
        public static void Clear(string test) { try { foreach (var f in new[] { "days.txt", "trades.txt" }) { var p = Path.Combine(DirOf(test), f); if (File.Exists(p)) File.Delete(p); } } catch { } }
        public static void Delete(string test) { try { if (Directory.Exists(DirOf(test))) Directory.Delete(DirOf(test), true); } catch { } }
        static string F(double v) { return v.ToString("0.##", IC); }
        static double D(string s) { double v; return double.TryParse(s, NumberStyles.Float, IC, out v) ? v : 0; }

        // the day out of the account's deals: the trades that closed that day, the commission / swap paid that day, the balance at its end
        public static Tuple<PoolDay, List<PoolTrade>> Build(List<Mt5Deal> deals, DateTime day, string kind)
        {
            var d0 = day.Date; var dd = deals.Where(x => x.IsTrade && x.Time.Date == d0).ToList(); if (dd.Count == 0) return null;
            var trades = new List<PoolTrade>();
            foreach (var ex in dd.Where(x => x.IsExit))
            {
                var inD = deals.FirstOrDefault(x => x.Position == ex.Position && x.Entry == "IN");
                double inComm = inD != null && inD.Lots > 0 ? inD.Commission * Math.Min(1, ex.Lots / inD.Lots) : 0;
                trades.Add(new PoolTrade { Day = d0, Sym = ex.Symbol, Side = inD != null ? inD.Type : (ex.Type == "BUY" ? "SELL" : "BUY"), Lots = ex.Lots, OpenT = inD != null ? inD.Time : ex.Time, OpenP = inD != null ? inD.Price : 0, CloseT = ex.Time, CloseP = ex.Price, Profit = ex.Profit, Comm = ex.Commission + inComm, Swap = ex.Swap, Reason = ex.Reason });
            }
            double endBal = deals.Where(x => x.Time.Date <= d0).Sum(x => x.IsTrade ? x.Net : x.Profit);
            var pd = new PoolDay { Day = d0, Syms = string.Join("+", dd.Select(x => x.Symbol).Distinct()), Kind = kind, Trades = trades.Count, Wins = trades.Count(t => t.Net > 0), Net = Math.Round(dd.Sum(x => x.Net), 2), Gross = Math.Round(dd.Sum(x => x.Profit), 2),
                Comm = Math.Round(dd.Sum(x => x.Commission), 2), Swap = Math.Round(dd.Sum(x => x.Swap), 2), Lots = dd.Where(x => x.Entry == "IN").Sum(x => x.Lots), Best = trades.Count > 0 ? trades.Max(t => t.Net) : 0, Worst = trades.Count > 0 ? trades.Min(t => t.Net) : 0, EndBal = Math.Round(endBal, 2), SavedAt = DateTime.Now };
            return Tuple.Create(pd, trades);
        }
        // save (or replace) one day of a test
        public static void Save(string test, PoolDay d, List<PoolTrade> trades)
        {
            var dir = DirOf(test); Directory.CreateDirectory(dir); string key = d.Day.ToString("yyyy-MM-dd", IC);
            var df = Path.Combine(dir, "days.txt"); var tf = Path.Combine(dir, "trades.txt");
            var days = File.Exists(df) ? File.ReadAllLines(df).Where(l => !l.StartsWith(key + "|")).ToList() : new List<string>();
            days.Add(key + "|" + d.Syms + "|" + d.Trades + "|" + d.Wins + "|" + F(d.Net) + "|" + F(d.Gross) + "|" + F(d.Comm) + "|" + F(d.Swap) + "|" + F(d.Lots) + "|" + F(d.Best) + "|" + F(d.Worst) + "|" + F(d.EndBal) + "|" + d.SavedAt.ToString("yyyy-MM-dd HH:mm", IC) + "|" + d.Kind);
            File.WriteAllLines(df, days.OrderBy(l => l));
            var tl = File.Exists(tf) ? File.ReadAllLines(tf).Where(l => !l.StartsWith(key + "|")).ToList() : new List<string>();
            foreach (var t in trades) tl.Add(key + "|" + t.Sym + "|" + t.Side + "|" + F(t.Lots) + "|" + t.OpenT.ToString("yyyy-MM-dd HH:mm:ss", IC) + "|" + t.OpenP.ToString("R", IC) + "|" + t.CloseT.ToString("yyyy-MM-dd HH:mm:ss", IC) + "|" + t.CloseP.ToString("R", IC) + "|" + F(t.Profit) + "|" + F(t.Comm) + "|" + F(t.Swap) + "|" + t.Reason);
            File.WriteAllLines(tf, tl.OrderBy(l => l));
        }
        public static List<PoolDay> Days(string test)
        {
            var res = new List<PoolDay>(); try
            {
                var df = Path.Combine(DirOf(test), "days.txt"); if (!File.Exists(df)) return res;
                foreach (var l in File.ReadAllLines(df))
                {
                    var a = l.Split('|'); DateTime day; if (a.Length < 12 || !DateTime.TryParseExact(a[0], "yyyy-MM-dd", IC, DateTimeStyles.None, out day)) continue; DateTime sa; DateTime.TryParseExact(a.Length > 12 ? a[12] : "", "yyyy-MM-dd HH:mm", IC, DateTimeStyles.None, out sa);
                    res.Add(new PoolDay { Day = day, Syms = a[1], Trades = (int)D(a[2]), Wins = (int)D(a[3]), Net = D(a[4]), Gross = D(a[5]), Comm = D(a[6]), Swap = D(a[7]), Lots = D(a[8]), Best = D(a[9]), Worst = D(a[10]), EndBal = D(a[11]), SavedAt = sa, Kind = a.Length > 13 ? a[13] : "" });
                }
            }
            catch { }
            return res;
        }
        public static List<PoolTrade> Trades(string test)
        {
            var res = new List<PoolTrade>(); try
            {
                var tf = Path.Combine(DirOf(test), "trades.txt"); if (!File.Exists(tf)) return res;
                foreach (var l in File.ReadAllLines(tf))
                {
                    var a = l.Split('|'); if (a.Length < 12) continue; DateTime day, ot, ct;
                    DateTime.TryParseExact(a[0], "yyyy-MM-dd", IC, DateTimeStyles.None, out day); DateTime.TryParseExact(a[4], "yyyy-MM-dd HH:mm:ss", IC, DateTimeStyles.None, out ot); DateTime.TryParseExact(a[6], "yyyy-MM-dd HH:mm:ss", IC, DateTimeStyles.None, out ct);
                    res.Add(new PoolTrade { Day = day, Sym = a[1], Side = a[2], Lots = D(a[3]), OpenT = ot, OpenP = D(a[5]), CloseT = ct, CloseP = D(a[7]), Profit = D(a[8]), Comm = D(a[9]), Swap = D(a[10]), Reason = a[11] });
                }
            }
            catch { }
            return res;
        }
        // the periods: DAYS / WEEKS (Monday) / MONTHS / YEARS
        public static string PeriodKey(DateTime d, string by) { return by == "WEEKS" ? d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", IC) : by == "MONTHS" ? d.ToString("yyyy-MM", IC) : by == "YEARS" ? d.ToString("yyyy", IC) : d.ToString("yyyy-MM-dd", IC); }
        public static string PeriodName(string key, string by)
        {
            DateTime d; if (by == "WEEKS" && DateTime.TryParseExact(key, "yyyy-MM-dd", IC, DateTimeStyles.None, out d)) return "week of " + d.ToString("dd MMM yyyy", IC);
            if (by == "MONTHS" && DateTime.TryParseExact(key + "-01", "yyyy-MM-dd", IC, DateTimeStyles.None, out d)) return d.ToString("MMMM yyyy", IC);
            if (by == "DAYS" && DateTime.TryParseExact(key, "yyyy-MM-dd", IC, DateTimeStyles.None, out d)) return d.ToString("ddd dd MMM yyyy", IC);
            return key;
        }

        // 🌐 the HTML tracker
        public static string Html(string test, List<PoolDay> days, List<PoolTrade> trades)
        {
            Func<double, string> m = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N2", IC); Func<double, string> sm = v => (v > 0 ? "+" : "") + m(v);
            Func<double, string> cls = v => v > 0 ? "g" : v < 0 ? "r" : "";
            Func<string, string> esc = s => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            var sb = new StringBuilder();
            double net = days.Sum(d => d.Net); int tr = days.Sum(d => d.Trades), wins = days.Sum(d => d.Wins); double gw = trades.Where(t => t.Net > 0).Sum(t => t.Net), gl = -trades.Where(t => t.Net < 0).Sum(t => t.Net);
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>SpasiX tracker – " + esc(test) + "</title><style>");
            sb.Append(":root{--bg:#080C14;--card:#131B2A;--line:#22304A;--text:#E8ECF4;--muted:#8590A6;--gold:#FFC94D;--cyan:#4DD8FF;--g:#2EE59D;--r:#FF4D6D;--v:#A98BFF}");
            sb.Append("body{margin:0;background:var(--bg);color:var(--text);font-family:Segoe UI,Arial,sans-serif;padding:22px 26px}h1{margin:0;font-size:26px}h1 b{color:var(--gold)}h1 i{color:var(--cyan);font-style:normal}h2{color:var(--gold);font-size:16px;margin:26px 0 10px}");
            sb.Append(".sub{color:var(--muted);margin:4px 0 18px}.cards{display:flex;flex-wrap:wrap;gap:10px}.c{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:10px 14px;min-width:120px}.c .k{color:var(--muted);font-size:11px;font-weight:700}.c .v{font:700 20px Consolas,monospace;margin-top:2px}");
            sb.Append(".g{color:var(--g)}.r{color:var(--r)}table{border-collapse:collapse;width:100%;background:var(--card);border-radius:12px;overflow:hidden;font-size:13px}th{color:var(--muted);text-align:left;font-size:11px;padding:8px 10px;border-bottom:1px solid var(--line)}td{padding:6px 10px;border-bottom:1px solid #182236;font-family:Consolas,monospace}td.n{text-align:right}");
            sb.Append(".tabs{display:flex;gap:6px;margin-bottom:10px}.tabs button{background:var(--card);color:var(--text);border:1px solid var(--line);border-radius:8px;padding:6px 14px;font-weight:700;cursor:pointer}.tabs button.on{background:var(--gold);color:#080C14}");
            sb.Append(".months{display:flex;flex-wrap:wrap;gap:14px}.mon{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:10px}.mon h3{margin:0 0 6px;font-size:13px;color:var(--cyan)}.grid{display:grid;grid-template-columns:repeat(5,74px);gap:4px}.dh{color:var(--muted);font-size:10px;text-align:center}");
            sb.Append(".day{height:52px;border-radius:7px;background:#0D131F;padding:4px;font-size:10px;color:var(--muted)}.day b{display:block;font:700 12px Consolas,monospace;margin-top:6px;color:#fff}.bar{height:8px;border-radius:4px}svg{background:var(--card);border-radius:12px;border:1px solid var(--line)}</style></head><body>");
            sb.Append("<h1><b>Spasi</b><i>X</i> • backtest tracker</h1><div class='sub'>test <b>" + esc(test) + "</b> • " + days.Count + " day(s) saved" + (days.Count > 0 ? " • " + days.Min(d => d.Day).ToString("dd MMM yyyy", IC) + " → " + days.Max(d => d.Day).ToString("dd MMM yyyy", IC) : "") + " • written " + DateTime.Now.ToString("dd MMM yyyy HH:mm", IC) + "</div>");
            sb.Append("<div class='cards'>");
            Action<string, string, string> card = (k, v, c) => sb.Append("<div class='c'><div class='k'>" + k + "</div><div class='v " + c + "'>" + v + "</div></div>");
            card("NET", sm(net), cls(net)); card("DAYS", days.Count + " (" + days.Count(d => d.Net > 0) + " green)", ""); card("TRADES", tr.ToString(IC), ""); card("WIN RATE", tr > 0 ? (100.0 * wins / tr).ToString("0", IC) + " %" : "—", "");
            card("PROFIT FACTOR", gl > 0 ? (gw / gl).ToString("0.00", IC) : gw > 0 ? "∞" : "—", ""); card("AVG DAY", days.Count > 0 ? sm(net / days.Count) : "—", cls(net)); card("BEST DAY", days.Count > 0 ? sm(days.Max(d => d.Net)) : "—", days.Count > 0 ? cls(days.Max(d => d.Net)) : ""); card("WORST DAY", days.Count > 0 ? sm(days.Min(d => d.Net)) : "—", days.Count > 0 ? cls(days.Min(d => d.Net)) : "");
            card("COMMISSION", m(days.Sum(d => d.Comm)), "r"); card("SWAP", sm(days.Sum(d => d.Swap)), cls(days.Sum(d => d.Swap)));
            double peak = 0, run = 0, dd = 0; foreach (var d in days.OrderBy(x => x.Day)) { run += d.Net; peak = Math.Max(peak, run); dd = Math.Max(dd, peak - run); } card("MAX DRAWDOWN", m(-dd), dd > 0 ? "r" : "");
            sb.Append("</div>");
            // equity curve
            var od = days.OrderBy(d => d.Day).ToList();
            if (od.Count > 0)
            {
                sb.Append("<h2>Equity (cumulative net)</h2>"); int W = 1000, H = 220; double c = 0; var pts = new List<double> { 0 }; foreach (var d in od) { c += d.Net; pts.Add(c); }
                double lo = Math.Min(0, pts.Min()), hi = Math.Max(0, pts.Max()); if (hi - lo < 1) hi = lo + 1;
                Func<int, double> X = i => 20 + (W - 40) * i / (double)Math.Max(1, pts.Count - 1); Func<double, double> Y = v => H - 20 - (H - 40) * (v - lo) / (hi - lo);
                sb.Append("<svg viewBox='0 0 " + W + " " + H + "' width='100%' height='" + H + "'><line x1='20' x2='" + (W - 20) + "' y1='" + Y(0).ToString("0.#", IC) + "' y2='" + Y(0).ToString("0.#", IC) + "' stroke='#22304A' stroke-dasharray='4 4'/>");
                sb.Append("<polyline fill='none' stroke='" + (c >= 0 ? "#2EE59D" : "#FF4D6D") + "' stroke-width='2.5' points='" + string.Join(" ", pts.Select((v, i) => X(i).ToString("0.#", IC) + "," + Y(v).ToString("0.#", IC))) + "'/></svg>");
            }
            // the calendar
            sb.Append("<h2>Calendar</h2><div class='months'>");
            foreach (var mg in od.GroupBy(d => new DateTime(d.Day.Year, d.Day.Month, 1)))
            {
                sb.Append("<div class='mon'><h3>" + mg.Key.ToString("MMMM yyyy", IC) + " • <span class='" + cls(mg.Sum(x => x.Net)) + "'>" + sm(mg.Sum(x => x.Net)) + "</span></h3><div class='grid'>");
                foreach (var n in new[] { "MON", "TUE", "WED", "THU", "FRI" }) sb.Append("<div class='dh'>" + n + "</div>");
                var first = mg.Key; int lead = ((int)first.DayOfWeek + 6) % 7; if (lead > 4) lead = 0; for (int i = 0; i < lead; i++) sb.Append("<div></div>");
                double mx = Math.Max(1, mg.Max(x => Math.Abs(x.Net)));
                for (var d = first; d.Month == first.Month; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue; var pd = mg.FirstOrDefault(x => x.Day == d);
                    string bg = pd == null ? "" : " style='background:" + (pd.Net >= 0 ? "rgba(46,229,157," : "rgba(255,77,109,") + (0.15 + 0.55 * Math.Abs(pd.Net) / mx).ToString("0.00", IC) + ")'";
                    sb.Append("<div class='day'" + bg + ">" + d.Day + (pd != null ? "<b>" + sm(pd.Net) + "</b>" + pd.Trades + " tr" : "") + "</div>");
                }
                sb.Append("</div></div>");
            }
            sb.Append("</div>");
            // the periods (tabs)
            sb.Append("<h2>By period</h2><div class='tabs'>");
            var bys = new[] { "DAYS", "WEEKS", "MONTHS", "YEARS" }; foreach (var b in bys) sb.Append("<button onclick=\"sh('" + b + "')\" id='b" + b + "'" + (b == "WEEKS" ? " class='on'" : "") + ">" + b + "</button>");
            sb.Append("</div>");
            foreach (var by in bys)
            {
                var gs = od.GroupBy(d => PeriodKey(d.Day, by)).OrderByDescending(g => g.Key).ToList(); double mx = gs.Count > 0 ? Math.Max(1, gs.Max(g => Math.Abs(g.Sum(x => x.Net)))) : 1;
                sb.Append("<table id='t" + by + "' style='display:" + (by == "WEEKS" ? "table" : "none") + "'><tr><th>" + by.TrimEnd('S') + "</th><th>DAYS</th><th>TRADES</th><th>WIN %</th><th>COMMISSION</th><th>SWAP</th><th style='text-align:right'>NET</th><th style='width:180px'></th></tr>");
                foreach (var g in gs)
                {
                    double n = g.Sum(x => x.Net); int t = g.Sum(x => x.Trades), w = g.Sum(x => x.Wins);
                    sb.Append("<tr><td>" + PeriodName(g.Key, by) + "</td><td>" + g.Count() + "</td><td>" + t + "</td><td>" + (t > 0 ? (100.0 * w / t).ToString("0", IC) : "—") + "</td><td class='r'>" + m(g.Sum(x => x.Comm)) + "</td><td>" + sm(g.Sum(x => x.Swap)) + "</td><td class='n " + cls(n) + "'>" + sm(n) + "</td><td><div class='bar' style='width:" + (100 * Math.Abs(n) / mx).ToString("0", IC) + "%;background:" + (n >= 0 ? "#2EE59D" : "#FF4D6D") + "'></div></td></tr>");
                }
                sb.Append("</table>");
            }
            // trades
            sb.Append("<h2>Trades (" + trades.Count + ")</h2><table><tr><th>DAY</th><th>SYMBOL</th><th>SIDE</th><th>LOTS</th><th>OPEN</th><th>PRICE</th><th>CLOSE</th><th>PRICE</th><th>WHY</th><th style='text-align:right'>NET</th></tr>");
            foreach (var t in trades.OrderByDescending(x => x.CloseT).Take(1500))
                sb.Append("<tr><td>" + t.Day.ToString("dd MMM", IC) + "</td><td>" + esc(t.Sym) + "</td><td class='" + (t.Side == "BUY" ? "g" : "r") + "'>" + t.Side + "</td><td>" + t.Lots.ToString("0.00", IC) + "</td><td>" + t.OpenT.ToString("HH:mm:ss", IC) + "</td><td>" + t.OpenP.ToString("0.#####", IC) + "</td><td>" + t.CloseT.ToString("HH:mm:ss", IC) + "</td><td>" + t.CloseP.ToString("0.#####", IC) + "</td><td>" + esc(t.Reason == "CLIENT" ? "manual" : t.Reason) + "</td><td class='n " + cls(t.Net) + "'>" + sm(t.Net) + "</td></tr>");
            sb.Append("</table><script>function sh(b){['DAYS','WEEKS','MONTHS','YEARS'].forEach(function(x){document.getElementById('t'+x).style.display=x==b?'table':'none';document.getElementById('b'+x).className=x==b?'on':''})}</script></body></html>");
            return sb.ToString();
        }
    }

}
