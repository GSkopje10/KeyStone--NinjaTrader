// SpasiX — 👁 MARKET (every streamed symbol: bid / ask, spread, today's change and range from MT5's daily candle, your exposure, its next
// high-impact news; click = its chart) and 📰 NEWS (the ForexFactory calendar of the week: impact, currency, time in your zone, forecast /
// previous, a countdown; the charts show a banner before a high-impact event for their currencies).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed class NewsEvent { public string Title = "", Currency = "", Impact = "", Forecast = "", Previous = ""; public DateTime Utc; }

    public sealed partial class MainWindow
    {
        WrapPanel marketPanel; readonly Dictionary<string, Tuple<Border, TextBlock, TextBlock, TextBlock, TextBlock, TextBlock>> marketCards = new Dictionary<string, Tuple<Border, TextBlock, TextBlock, TextBlock, TextBlock, TextBlock>>();
        readonly List<NewsEvent> news = new List<NewsEvent>(); string newsStatus = "loading the ForexFactory calendar …"; DateTime newsAt = DateTime.MinValue;
        StackPanel newsPanel; TextBlock newsHead; Button newsImpact; int newsImp = 1; string newsSig = "";
        bool newsPast; int newsWeeks = 1; DateTime newsDay = DateTime.MinValue; Button newsWeeksBtn, newsUpBtn, newsPastBtn; WrapPanel newsDays;
        static string WeeksName(int w) { return w == 1 ? "1 WEEK" : w + " WEEKS"; }
        static string NewsCache { get { return System.IO.Path.Combine(Dir, "news-cache.txt"); } }
        static string ImpName(int i) { return i == 0 ? "🔴 HIGH only" : i == 1 ? "🟠 HIGH + MEDIUM" : "ALL"; }

        // ---------------------------------------------------------------- MARKET
        FrameworkElement BuildMarket()
        {
            var sp = new StackPanel();
            var top = new DockPanel { Margin = new Thickness(2, 0, 0, 8) };
            var add = Pill("+ ADD AN INSTRUMENT", Tint(Cyan, 60)); add.Click += delegate { PickSymbol(); }; DockPanel.SetDock(add, Dock.Right); top.Children.Add(add);
            top.Children.Add(new TextBlock { Text = "👁 MARKET • live from MT5 • today = since the server's midnight (New York 17:00) • click a card = its chart", Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(top);
            marketPanel = new WrapPanel(); sp.Children.Add(marketPanel);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        void FillMarket(Mt5State s)
        {
            var now = DateTime.Now;
            foreach (var q in s.Quotes.Values.OrderBy(x => x.Symbol))
            {
                Tuple<Border, TextBlock, TextBlock, TextBlock, TextBlock, TextBlock> c;
                if (!marketCards.TryGetValue(q.Symbol, out c))
                {
                    var name = new TextBlock { Text = q.Symbol, Foreground = Gold, FontSize = 16, FontWeight = FontWeights.Bold };
                    var px = new TextBlock { Foreground = Text, FontSize = 20, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 2, 0, 0) };
                    var chg = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
                    var spr = new TextBlock { Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap }; var expo = new TextBlock { Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
                    var nw = new TextBlock { Foreground = Orange, FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
                    var sp = new StackPanel(); sp.Children.Add(name); sp.Children.Add(px); sp.Children.Add(chg); sp.Children.Add(spr); sp.Children.Add(expo); sp.Children.Add(nw);
                    var bd = new Border { Width = 280, Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 10, 10), BorderBrush = Line, BorderThickness = new Thickness(1), Child = sp, Cursor = System.Windows.Input.Cursors.Hand };
                    var symQ = q.Symbol; bd.ToolTip = "open the 📈 CHART of " + symQ; bd.MouseLeftButtonUp += delegate { SetPane(Act, symQ, Act.Tf); Show("CHART"); };
                    marketPanel.Children.Add(bd); c = Tuple.Create(bd, name, px, chg, spr, expo); marketCards[q.Symbol] = c; bd.Tag = nw;
                }
                var sym = s.Symbols.ContainsKey(q.Symbol) ? s.Symbols[q.Symbol] : null; string f = "F" + (sym != null ? sym.Digits : 2);
                c.Item3.Text = q.Bid.ToString(f, IC) + "  /  " + q.Ask.ToString(f, IC);
                // today from MT5's daily candle (asked every minute)
                var d1 = s.SeriesOf(q.Symbol, 1440); if ((now - d1.AskedAt).TotalSeconds > 60) { d1.AskedAt = now; broker.Send("BARS|" + q.Symbol + "|1440|3"); }
                if (d1.Loaded && d1.Bars.Count > 0)
                {
                    var td = d1.Bars[d1.Bars.Count - 1]; double prevC = d1.Bars.Count > 1 ? d1.Bars[d1.Bars.Count - 2].C : td.O, ch = q.Bid - prevC, pct = prevC > 0 ? 100 * ch / prevC : 0;
                    c.Item4.Text = (ch >= 0 ? "▲ +" : "▼ ") + ch.ToString(f, IC) + "  (" + pct.ToString("+0.00;−0.00", IC) + " %)\nrange today " + td.L.ToString(f, IC) + " – " + td.H.ToString(f, IC); c.Item4.Foreground = ch >= 0 ? Green : Red;
                }
                c.Item5.Text = "spread " + q.Spread(sym != null ? sym.Point : 0).ToString("0", IC) + " pts" + (sym != null ? " • lots " + sym.VolMin.ToString("0.##", IC) + "–" + sym.VolMax.ToString("0.##", IC) + " • " + sym.Description : "") + " • " + q.Time.ToString("HH:mm:ss", IC) + " server";
                var ps = s.Positions.Where(p => p.Symbol == q.Symbol).ToList();
                if (ps.Count == 0) { c.Item6.Text = "no position"; c.Item6.Foreground = Muted; c.Item1.BorderBrush = Line; }
                else { double gpl = ps.Sum(p => p.Profit + p.Swap); c.Item6.Text = ps.Count + " ticket" + (ps.Count == 1 ? "" : "s") + " • net " + ps.Sum(p => p.Dir * p.Lots).ToString("+0.00;−0.00;0.00", IC) + " lots • " + Signed(gpl, s.Account.Currency); c.Item6.Foreground = gpl >= 0 ? Green : Red; c.Item1.BorderBrush = gpl >= 0 ? Tint(Green, 140) : Tint(Red, 140); }
                var nb = c.Item1.Tag as TextBlock; var nx = NextNews(q.Symbol, "High"); if (nb != null) nb.Text = nx != null ? "📰 " + nx.Currency + " " + nx.Title + " • " + Until(nx.Utc) : "";
            }
        }

        // ---------------------------------------------------------------- NEWS (ForexFactory)
        FrameworkElement BuildNews()
        {
            var sp = new StackPanel();
            var top = new DockPanel { Margin = new Thickness(2, 0, 0, 8) };
            var right = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(right, Dock.Right);
            newsImp = Math.Max(0, Math.Min(2, IntOf(Get("newsimp", "1"), 1)));
            newsImpact = Sx.Drop(ImpName(newsImp), Card, () => { var l = new List<Mi>(); for (int i = 0; i < 3; i++) { int ii = i; l.Add(new Mi { Text = ImpName(i), Check = newsImp == i, Radio = true, Do = () => { newsImp = ii; Put("newsimp", ii.ToString(IC)); newsImpact.Content = ImpName(ii) + "  ▾"; newsSig = ""; } }); } return l; });
            var reload = Pill("↻ RELOAD", Card); reload.Click += delegate { newsAt = DateTime.MinValue; LoadNews(); LoadHeadlines(); };
            newsWeeks = Math.Max(1, Math.Min(4, IntOf(Get("newsweeks", "2"), 2)));
            newsWeeksBtn = Sx.Drop(WeeksName(newsWeeks), Card, () => { var l = new List<Mi> { Mi.H(newsPast ? "how far back" : "how far ahead") }; foreach (var w in new[] { 1, 2, 3, 4 }) { int ww = w; l.Add(new Mi { Text = WeeksName(w), Check = newsWeeks == w, Radio = true, Do = () => { newsWeeks = ww; Put("newsweeks", ww.ToString(IC)); newsWeeksBtn.Content = WeeksName(ww) + "  ▾"; newsDay = DateTime.MinValue; newsSig = ""; } }); } return l; });
            right.Children.Add(newsWeeksBtn); right.Children.Add(newsImpact); right.Children.Add(reload); top.Children.Add(right);
            var tabsP = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            newsUpBtn = Pill("⏭  UPCOMING", Card); newsUpBtn.Height = 34; newsUpBtn.FontSize = 13; newsUpBtn.Click += delegate { newsPast = false; newsDay = DateTime.MinValue; newsSig = ""; };
            newsPastBtn = Pill("⏮  PAST", Card); newsPastBtn.Height = 34; newsPastBtn.FontSize = 13; newsPastBtn.Click += delegate { newsPast = true; newsDay = DateTime.MinValue; newsSig = ""; };
            tabsP.Children.Add(newsUpBtn); tabsP.Children.Add(newsPastBtn); top.Children.Add(tabsP);
            sp.Children.Add(top);
            newsHead = new TextBlock { Foreground = Muted, FontSize = 11.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 6) }; sp.Children.Add(newsHead);
            newsDays = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) }; sp.Children.Add(newsDays);
            newsPanel = new StackPanel(); sp.Children.Add(newsPanel);
            var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.7, GridUnitType.Star) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp });
            // the headlines (RSS of ForexLive and FXStreet), newest first; click = open in the browser
            var hp = new DockPanel();
            headHead = new TextBlock { Text = "🗞 HEADLINES • loading …", Foreground = Cyan, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 4, 0, 8), TextWrapping = TextWrapping.Wrap }; DockPanel.SetDock(headHead, Dock.Top); hp.Children.Add(headHead);
            headPanel = new StackPanel(); hp.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = headPanel });
            var hb = new Border { Background = Panel, CornerRadius = new CornerRadius(12), BorderBrush = Tint(Cyan, 90), BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 8), Child = hp }; Grid.SetColumn(hb, 2); root.Children.Add(hb);
            return root;
        }
        TextBlock headHead; StackPanel headPanel; readonly List<Tuple<DateTime, string, string, string>> heads = new List<Tuple<DateTime, string, string, string>>(); string headSig = "", headStatus = "";
        static readonly Tuple<string, string>[] Feeds = { Tuple.Create("ForexLive", "https://www.forexlive.com/feed/news"), Tuple.Create("FXStreet", "https://www.fxstreet.com/rss/news"), Tuple.Create("Investing", "https://www.investing.com/rss/news_1.rss") };
        async void LoadHeadlines()
        {
            var got = new List<Tuple<DateTime, string, string, string>>(); var okFrom = new List<string>();
            foreach (var f in Feeds)
            {
                try
                {
                    string xml; using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) }) { http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 SpasiX/1.0"); xml = await http.GetStringAsync(f.Item2); }
                    var items = await Task.Run(() =>
                    {
                        var l = new List<Tuple<DateTime, string, string, string>>(); var doc = System.Xml.Linq.XDocument.Parse(xml);
                        foreach (var it in doc.Descendants().Where(e => e.Name.LocalName == "item").Take(40))
                        {
                            Func<string, string> v = n => { var e = it.Elements().FirstOrDefault(x => x.Name.LocalName == n); return e != null ? e.Value.Trim() : ""; };
                            DateTimeOffset d; DateTime t = DateTimeOffset.TryParse(v("pubDate"), IC, DateTimeStyles.None, out d) ? d.UtcDateTime : DateTime.UtcNow;
                            var title = System.Net.WebUtility.HtmlDecode(v("title")); if (title != "") l.Add(Tuple.Create(t, title, v("link"), f.Item1));
                        }
                        return l;
                    });
                    got.AddRange(items); okFrom.Add(f.Item1);
                }
                catch { }
            }
            if (got.Count > 0) { heads.Clear(); heads.AddRange(got.OrderByDescending(x => x.Item1).Take(80)); }
            headStatus = okFrom.Count > 0 ? "🗞 HEADLINES • " + string.Join(" + ", okFrom) + " • " + DateTime.Now.ToString("HH:mm", IC) : "🗞 HEADLINES could not be loaded (no internet / the sites refused) — ↻ RELOAD later";
            headSig = "";
        }
        void FillHeadlines()
        {
            if (headPanel == null) return; string zn; var conv = ZoneConv(broker.State, out zn); int off = broker.State.ServerOffsetSec;
            headHead.Text = headStatus == "" ? "🗞 HEADLINES • loading …" : headStatus;
            string sig = headStatus + heads.Count + "|" + zoneIdx + "|" + DateTime.Now.Minute; if (sig == headSig) return; headSig = sig; headPanel.Children.Clear();
            var mine = broker.State.Positions.SelectMany(p => CurrenciesOf(p.Symbol)).Concat(panes.Take(split).SelectMany(p => CurrenciesOf(p.Sym))).Distinct().ToList();
            var words = mine.Concat(mine.Contains("USD") ? new[] { "Fed", "dollar", "Powell", "CPI", "NFP", "payrolls", "gold", "Gold", "Treasury" } : new string[0]).ToList();
            foreach (var h in heads)
            {
                bool hot = words.Any(w => h.Item2.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0); var age = DateTime.UtcNow - h.Item1;
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = h.Item2, Foreground = hot ? Text : Muted, FontSize = 12.5, FontWeight = hot ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(new TextBlock { Text = conv(h.Item1.AddSeconds(off)).ToString("ddd HH:mm", IC) + " " + zn + " • " + (age.TotalMinutes < 60 ? ((int)Math.Max(0, age.TotalMinutes)) + " min ago" : age.TotalHours < 24 ? ((int)age.TotalHours) + " h ago" : ((int)age.TotalDays) + " d ago") + " • " + h.Item4, Foreground = hot ? Cyan : Muted, FontSize = 10.5, Margin = new Thickness(0, 1, 0, 0) });
                var link = h.Item3; var bd = new Border { Child = sp, Padding = new Thickness(8, 5, 8, 6), Margin = new Thickness(0, 0, 0, 4), CornerRadius = new CornerRadius(8), Background = hot ? Tint(Cyan, 20) : Card, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "open in the browser" + (hot ? " • it mentions your currencies" : "") };
                bd.MouseEnter += delegate { bd.BorderBrush = Cyan; bd.BorderThickness = new Thickness(1); }; bd.MouseLeave += delegate { bd.BorderThickness = new Thickness(0); };
                bd.MouseLeftButtonUp += delegate { try { if (link.StartsWith("http")) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link) { UseShellExecute = true }); } catch { } };
                headPanel.Children.Add(bd);
            }
        }
        void StartNews()
        {
            LoadNews(); LoadHeadlines(); var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(20) }; tm.Tick += delegate { LoadNews(); }; tm.Start();
            var th = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) }; th.Tick += delegate { LoadHeadlines(); }; th.Start();
        }
        async void LoadNews()
        {
            if ((DateTime.Now - newsAt).TotalMinutes < 5) return; newsAt = DateTime.Now; newsStatus = "loading the ForexFactory calendar …"; newsSig = "";
            var got = new List<NewsEvent>(); var okWeeks = new List<string>(); string err = "";
            foreach (var w in new[] { Tuple.Create("this week", "https://nfs.faireconomy.media/ff_calendar_thisweek.json"), Tuple.Create("next week", "https://nfs.faireconomy.media/ff_calendar_nextweek.json") })
            {
                try
                {
                    string json; using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { http.DefaultRequestHeaders.UserAgent.ParseAdd("SpasiX/1.0"); json = await http.GetStringAsync(w.Item2); }
                    got.AddRange(await Task.Run(() => ParseFf(json))); okWeeks.Add("ForexFactory " + w.Item1);
                }
                catch (Exception ex) { if (w.Item1 == "this week") err = ex.Message; }
            }
            // TradingView's economic calendar (the same official releases) for the days OUTSIDE ForexFactory's week: next week and later, with the actual values
            int tvN = 0;
            try
            {
                DateTime ffLo = got.Count > 0 ? got.Min(e => e.Utc).Date : DateTime.MaxValue, ffHi = got.Count > 0 ? got.Max(e => e.Utc).Date.AddDays(1) : DateTime.MinValue;
                var url = "https://economic-calendar.tradingview.com/events?from=" + DateTime.UtcNow.Date.AddDays(-7).ToString("yyyy-MM-dd", IC) + "T00:00:00.000Z&to=" + DateTime.UtcNow.Date.AddDays(21).ToString("yyyy-MM-dd", IC) + "T00:00:00.000Z&countries=US,EU,GB,JP,AU,NZ,CA,CH,CN,DE";
                string json; using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 SpasiX/1.0"); http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.tradingview.com"); json = await http.GetStringAsync(url); }
                var tv = await Task.Run(() => ParseTv(json)); foreach (var e in tv) if (e.Utc < ffLo || e.Utc >= ffHi) { got.Add(e); tvN++; }
                if (tvN > 0) okWeeks.Add("TradingView (next weeks)");
            }
            catch { }
            // the past stays: every event ever loaded is kept on this PC (news-cache.txt), so ⏮ PAST reaches back further than ForexFactory's week
            var all = LoadNewsCache(); foreach (var e in got) all[e.Utc.Ticks + "|" + e.Currency + "|" + e.Title] = e;
            var list = all.Values.Where(e => e.Utc > DateTime.UtcNow.AddDays(-35)).OrderBy(x => x.Utc).ToList(); SaveNewsCache(list);
            news.Clear(); news.AddRange(list);
            newsStatus = okWeeks.Count > 0 ? "📰 " + string.Join(" + ", okWeeks) + " • " + got.Count + " events • updated " + DateTime.Now.ToString("HH:mm", IC) : "📰 the ForexFactory calendar could not be loaded (" + err + ") — ↻ RELOAD in a minute" + (list.Count > 0 ? " • showing the saved events" : "");
            newsSig = "";
        }
        static List<NewsEvent> ParseFf(string json)
        {
            var res = new List<NewsEvent>();
            using (var doc = JsonDocument.Parse(json))
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    Func<string, string> str = k => { JsonElement v; return e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : ""; };
                    DateTimeOffset dto; if (!DateTimeOffset.TryParse(str("date"), IC, DateTimeStyles.None, out dto)) continue;
                    res.Add(new NewsEvent { Title = str("title"), Currency = str("country"), Impact = str("impact"), Forecast = str("forecast"), Previous = str("previous"), Utc = dto.UtcDateTime });
                }
            return res;
        }
        static List<NewsEvent> ParseTv(string json)
        {
            var res = new List<NewsEvent>();
            using (var doc = JsonDocument.Parse(json))
            {
                JsonElement arr; if (!doc.RootElement.TryGetProperty("result", out arr) || arr.ValueKind != JsonValueKind.Array) return res;
                foreach (var e in arr.EnumerateArray())
                {
                    Func<string, string> str = k => { JsonElement v; return e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : ""; };
                    Func<string, string> num = k => { JsonElement v; if (!e.TryGetProperty(k, out v) || v.ValueKind != JsonValueKind.Number) return ""; return v.GetDouble().ToString("0.###", IC) + str("scale") + (str("unit") == "%" ? "%" : ""); };
                    DateTimeOffset dto; if (!DateTimeOffset.TryParse(str("date"), IC, DateTimeStyles.None, out dto)) continue;
                    int imp = 0; JsonElement iv; if (e.TryGetProperty("importance", out iv) && iv.ValueKind == JsonValueKind.Number) imp = iv.GetInt32();
                    var cur = str("currency"); if (cur == "") cur = str("country") == "US" ? "USD" : str("country") == "GB" ? "GBP" : str("country") == "JP" ? "JPY" : str("country") == "AU" ? "AUD" : str("country") == "CA" ? "CAD" : str("country") == "CH" ? "CHF" : str("country") == "NZ" ? "NZD" : str("country") == "CN" ? "CNY" : "EUR";
                    var act = num("actual");
                    res.Add(new NewsEvent { Title = str("title") + (act != "" ? "  ✔ actual " + act : ""), Currency = cur, Impact = imp >= 1 ? "High" : imp == 0 ? "Medium" : "Low", Forecast = num("forecast"), Previous = num("previous"), Utc = dto.UtcDateTime });
                }
            }
            return res;
        }
        static Dictionary<string, NewsEvent> LoadNewsCache()
        {
            var d = new Dictionary<string, NewsEvent>(); try
            {
                if (!System.IO.File.Exists(NewsCache)) return d;
                foreach (var l in System.IO.File.ReadAllLines(NewsCache)) { var a = l.Split('\t'); long t; if (a.Length < 6 || !long.TryParse(a[0], out t)) continue; var e = new NewsEvent { Utc = new DateTime(t, DateTimeKind.Utc), Currency = a[1], Impact = a[2], Title = a[3], Forecast = a[4], Previous = a[5] }; d[e.Utc.Ticks + "|" + e.Currency + "|" + e.Title] = e; }
            }
            catch { }
            return d;
        }
        static void SaveNewsCache(List<NewsEvent> l) { try { System.IO.Directory.CreateDirectory(Dir); System.IO.File.WriteAllLines(NewsCache, l.Select(e => e.Utc.Ticks + "\t" + e.Currency + "\t" + e.Impact + "\t" + (e.Title ?? "").Replace("\t", " ") + "\t" + e.Forecast + "\t" + e.Previous)); } catch { } }
        // ---- the header's news badge: the next HIGH-impact event; from 5 minutes before it: big, red, flashing (+ a pop-up once); back to normal after it
        Border newsBadge; TextBlock newsBadgeCap, newsBadgeText; bool newsHotOn; readonly HashSet<string> newsAlerted = new HashSet<string>(); Window newsAlertWin;
        FrameworkElement BuildNewsBadge()
        {
            newsBadgeCap = new TextBlock { Text = "📰 NEXT HIGH IMPACT", Foreground = Orange, FontSize = 9, FontWeight = FontWeights.Bold };
            newsBadgeText = new TextBlock { Text = "—", Foreground = Text, FontSize = 12.5, FontWeight = FontWeights.Bold, MaxWidth = 300, TextTrimming = TextTrimming.CharacterEllipsis };
            var st = new StackPanel(); st.Children.Add(newsBadgeCap); st.Children.Add(newsBadgeText);
            newsBadge = new Border { Child = st, Background = Tint(Orange, 26), BorderBrush = Tint(Orange, 120), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2, 9, 3), Margin = new Thickness(5, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "the next high-impact event (ForexFactory) • click = 📰 NEWS" };
            newsBadge.MouseLeftButtonUp += delegate { Show("NEWS"); };
            return newsBadge;
        }
        void PaintNewsBadge()
        {
            if (newsBadge == null) return; var nowU = DateTime.UtcNow;
            var n = news.Where(x => x.Impact == "High" && x.Utc >= nowU.AddMinutes(-2)).OrderBy(x => x.Utc).FirstOrDefault();
            if (n == null) { newsBadgeText.Text = news.Count == 0 ? "loading …" : "none this week"; SetNewsHot(false); return; }
            var left = n.Utc - nowU; string zn; var conv = ZoneConv(broker.State, out zn); var local = conv(n.Utc.AddSeconds(broker.State.ServerOffsetSec));
            bool hot = left.TotalMinutes <= 5 && left.TotalMinutes >= -2;
            newsBadgeCap.Text = hot ? (left.TotalSeconds > 0 ? "⚠ HIGH IMPACT NEWS IN " + ((int)left.TotalMinutes) + ":" + left.Seconds.ToString("00") : "⚠ HIGH IMPACT NEWS — RELEASED") : "📰 NEXT HIGH IMPACT • " + local.ToString("ddd HH:mm", IC) + " " + zn;
            newsBadgeText.Text = n.Currency + " " + n.Title + (hot ? "" : " • " + Until(n.Utc));
            SetNewsHot(hot);
            string key = n.Utc.Ticks + "|" + n.Currency + "|" + n.Title;
            if (hot && left.TotalSeconds > 0 && !newsAlerted.Contains(key)) { newsAlerted.Add(key); NewsAlert(n); }
        }
        void SetNewsHot(bool on)
        {
            if (on == newsHotOn) return; newsHotOn = on;
            newsBadgeCap.FontSize = on ? 11 : 9; newsBadgeText.FontSize = on ? 15 : 12.5; newsBadgeCap.Foreground = on ? Sx.White : Orange; newsBadge.Padding = on ? new Thickness(12, 4, 12, 5) : new Thickness(8, 2, 9, 3);
            if (on) { var br = new SolidColorBrush(Red.Color); newsBadge.Background = br; newsBadge.BorderBrush = Sx.White; br.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(Red.Color, Color.FromRgb(90, 10, 25), TimeSpan.FromMilliseconds(450)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever }); }
            else { newsBadge.Background = Tint(Orange, 26); newsBadge.BorderBrush = Tint(Orange, 120); }
        }
        // the pop-up 5 minutes before: red, big, its own countdown; it closes by itself 3 minutes after the release (or OK)
        void NewsAlert(NewsEvent n)
        {
            try { if (newsAlertWin != null) newsAlertWin.Close(); } catch { }
            var w = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = Sx.Face };
            var sp = new StackPanel { Width = 460 };
            sp.Children.Add(new TextBlock { Text = "⚠  HIGH IMPACT NEWS", Foreground = Red, FontSize = 26, FontWeight = FontWeights.Black, HorizontalAlignment = HorizontalAlignment.Center });
            var cd = new TextBlock { Foreground = Sx.White, FontSize = 44, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 2) }; sp.Children.Add(cd);
            sp.Children.Add(new TextBlock { Text = n.Currency + "  •  " + n.Title, Foreground = Sx.White, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = (n.Forecast != "" ? "forecast " + n.Forecast + "   " : "") + (n.Previous != "" ? "previous " + n.Previous : ""), Foreground = Muted, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            sp.Children.Add(new TextBlock { Text = "spreads widen and prices jump — check your stops", Foreground = Gold, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            var ok = Pill("OK", Tint(Red, 200)); ok.Foreground = Sx.White; ok.Height = 36; ok.MinWidth = 120; ok.HorizontalAlignment = HorizontalAlignment.Center; ok.Margin = new Thickness(0, 14, 0, 0); ok.Click += delegate { w.Close(); }; sp.Children.Add(ok);
            var card = new Border { Child = sp, CornerRadius = new CornerRadius(20), Background = new LinearGradientBrush(Color.FromRgb(46, 8, 18), Color.FromRgb(12, 6, 12), 90), BorderBrush = Red, BorderThickness = new Thickness(2.2), Padding = new Thickness(26, 18, 26, 18), Margin = new Thickness(24),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Red.Color, BlurRadius = 40, ShadowDepth = 0, Opacity = 0.8 } };
            card.MouseLeftButtonDown += delegate { try { w.DragMove(); } catch { } };
            w.Content = card;
            var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            tm.Tick += delegate { var left = n.Utc - DateTime.UtcNow; cd.Text = left.TotalSeconds > 0 ? "in " + ((int)left.TotalMinutes) + ":" + left.Seconds.ToString("00") : "RELEASED"; cd.Foreground = left.TotalSeconds > 0 ? Sx.White : Gold; if (left.TotalMinutes < -3) w.Close(); };
            w.Closed += delegate { tm.Stop(); if (newsAlertWin == w) newsAlertWin = null; }; tm.Start(); newsAlertWin = w;
            try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
            try { w.Show(); } catch { }
        }

        // the currencies a symbol moves on
        static string[] CurrenciesOf(string sym)
        {
            var u = (sym ?? "").ToUpperInvariant();
            if (u.StartsWith("XAU") || u.StartsWith("XAG") || u.Contains("US30") || u.Contains("USTEC") || u.Contains("NAS") || u.Contains("SPX") || u.Contains("US500") || u.StartsWith("BTC") || u.StartsWith("ETH") || u.Contains("WTI") || u.Contains("OIL")) return new[] { "USD" };
            if (u.Contains("GER") || u.Contains("DE40") || u.Contains("EU50") || u.Contains("FRA")) return new[] { "EUR" };
            if (u.Contains("UK100")) return new[] { "GBP" }; if (u.Contains("JP225")) return new[] { "JPY" }; if (u.Contains("AUS200")) return new[] { "AUD" };
            if (u.Length >= 6 && u.Substring(0, 6).All(char.IsLetter)) return new[] { u.Substring(0, 3), u.Substring(3, 3) };
            return new[] { "USD" };
        }
        NewsEvent NextNews(string sym, string impact)
        {
            var cs = CurrenciesOf(sym); var now = DateTime.UtcNow;
            return news.FirstOrDefault(n => n.Utc >= now.AddMinutes(-5) && cs.Contains(n.Currency) && (impact == "" || n.Impact == impact));
        }
        static string Until(DateTime utc)
        {
            var d = utc - DateTime.UtcNow; if (d.TotalMinutes < -1) return "now (" + (-d.TotalMinutes).ToString("0", IC) + " min ago)"; if (d.TotalMinutes < 1) return "NOW";
            return d.TotalHours >= 24 ? "in " + ((int)d.TotalDays) + " d " + d.Hours + " h" : d.TotalMinutes >= 60 ? "in " + ((int)d.TotalHours) + " h " + d.Minutes.ToString("00") + " min" : "in " + ((int)d.TotalMinutes) + " min";
        }
        // the chart's banner: the next HIGH-impact event of its currencies within 3 hours (it flashes from 15 minutes before to 5 after)
        Tuple<string, bool> NewsFor(string sym)
        {
            var n = NextNews(sym, "High"); if (n == null) return Tuple.Create("", false);
            var d = n.Utc - DateTime.UtcNow; if (d.TotalHours > 3) return Tuple.Create("", false);
            string zn; var conv = ZoneConv(broker.State, out zn); var local = conv(n.Utc.AddSeconds(broker.State.ServerOffsetSec));
            return Tuple.Create("HIGH IMPACT • " + n.Currency + " " + n.Title + " • " + local.ToString("HH:mm", IC) + " " + zn + " • " + Until(n.Utc), d.TotalMinutes <= 15 && d.TotalMinutes >= -5);
        }
        void FillNews()
        {
            FillHeadlines();
            string sig = newsStatus + "|" + newsImp + "|" + zoneIdx + "|" + DateTime.Now.Minute + "|" + newsPast + "|" + newsWeeks + "|" + newsDay.Ticks + "|" + news.Count;
            if (sig == newsSig) return; newsSig = sig; newsPanel.Children.Clear(); newsDays.Children.Clear();
            string zn; var conv = ZoneConv(broker.State, out zn); int off = broker.State.ServerOffsetSec; var nowU = DateTime.UtcNow;
            newsUpBtn.Background = !newsPast ? Gold : (Brush)Card; newsUpBtn.Foreground = !newsPast ? Bg : Text; newsPastBtn.Background = newsPast ? Tint(Violet, 210) : (Brush)Card; newsPastBtn.Foreground = newsPast ? Bg : Text;
            var mine = broker.State.Positions.SelectMany(p => CurrenciesOf(p.Symbol)).Concat(panes.Take(split).SelectMany(p => CurrenciesOf(p.Sym))).Distinct().ToList();
            int imp = newsImp;
            var span = news.Where(n => imp == 2 || n.Impact == "High" || (imp == 1 && n.Impact == "Medium"))
                .Where(n => newsPast ? n.Utc < nowU.AddMinutes(-5) && n.Utc >= nowU.AddDays(-7 * newsWeeks) : n.Utc >= nowU.AddMinutes(-5) && n.Utc <= nowU.AddDays(7 * newsWeeks)).ToList();
            var today0 = conv(nowU.AddSeconds(off)).Date;
            // the day pool: 5 trading days a week (Monday – Friday), every one shown even without events — 2 weeks = 10 days
            var poolDays = new List<DateTime>(); var dd = today0; if (newsPast) dd = dd.AddDays(-1);
            while (poolDays.Count < 5 * newsWeeks) { if (dd.DayOfWeek != DayOfWeek.Saturday && dd.DayOfWeek != DayOfWeek.Sunday) poolDays.Add(dd); dd = dd.AddDays(newsPast ? -1 : 1); }
            if (!newsPast && today0.DayOfWeek != DayOfWeek.Saturday && today0.DayOfWeek != DayOfWeek.Sunday && !poolDays.Contains(today0)) poolDays.Insert(0, today0);
            var grouped = span.GroupBy(n => conv(n.Utc.AddSeconds(off)).Date).ToDictionary(g => g.Key, g => g.ToList());
            var byDay = poolDays.Select(d => new KeyValuePair<DateTime, List<NewsEvent>>(d, grouped.ContainsKey(d) ? grouped[d] : new List<NewsEvent>())).ToList();
            span = byDay.SelectMany(x => x.Value).ToList();
            newsHead.Text = newsStatus + "   •   " + (newsPast ? "the last " : "the next ") + WeeksName(newsWeeks).ToLowerInvariant() + " • " + span.Count + " events (" + span.Count(n => n.Impact == "High") + " high) • times in " + Zones[Math.Max(0, zoneIdx)].Item1;
            // the day sub-tabs
            Action<DateTime, string, int, int> chip = (d, t, n, hi) =>
            {
                bool on = newsDay == d; var st = new StackPanel { Orientation = Orientation.Horizontal };
                st.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
                if (n > 0) st.Children.Add(new TextBlock { Text = "  " + n, Foreground = on ? Bg : Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center });
                if (hi > 0) st.Children.Add(new Border { Background = Red, CornerRadius = new CornerRadius(7), Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(6, 0, 0, 0), Child = new TextBlock { Text = hi.ToString(IC), Foreground = Sx.White, FontSize = 10, FontWeight = FontWeights.Bold } });
                var b = Pill("", on ? (Brush)Tint(Cyan, 200) : Card); b.Content = st; b.Foreground = on ? Bg : d == today0 ? Gold : Text; b.Height = 30; b.Margin = new Thickness(0, 0, 5, 5); b.BorderBrush = d == today0 ? Gold : Line;
                b.ToolTip = d == DateTime.MinValue ? "every day" : d.ToString("dddd dd MMMM yyyy", IC) + " • " + n + " events, " + hi + " high impact";
                var dd = d; b.Click += delegate { newsDay = dd; newsSig = ""; }; newsDays.Children.Add(b);
            };
            chip(DateTime.MinValue, "ALL DAYS", span.Count, span.Count(n => n.Impact == "High"));
            foreach (var g in byDay) chip(g.Key, (g.Key == today0 ? "TODAY • " : g.Key == today0.AddDays(1) ? "TOMORROW • " : g.Key == today0.AddDays(-1) ? "YESTERDAY • " : "") + g.Key.ToString("dddd dd MMM", IC).ToUpperInvariant(), g.Value.Count, g.Value.Count(n => n.Impact == "High"));
            if (newsDay != DateTime.MinValue && !byDay.Any(g => g.Key == newsDay)) newsDay = DateTime.MinValue;
            if (span.Count == 0) newsPanel.Children.Add(new TextBlock { Text = newsPast ? "no past event saved yet in this range (SpasiX keeps every event it loads, from now on)" : "no event ahead in this range" + (newsStatus.Contains("next week") ? "" : " — ForexFactory has not published next week's calendar yet (it usually appears over the weekend); ⏮ PAST shows this week"), Foreground = Muted, FontSize = 13, Margin = new Thickness(4, 10, 0, 0) });
            foreach (var day in byDay.Where(g => newsDay == DateTime.MinValue || g.Key == newsDay))
            {
                if (day.Value.Count == 0 && newsDay == DateTime.MinValue && span.Count == 0) continue;
                bool today = day.Key == today0;
                var hd = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 12, 0, 5) };
                hd.Children.Add(new TextBlock { Text = day.Key.ToString("dddd", IC).ToUpperInvariant(), Foreground = today ? Gold : Cyan, FontSize = 15, FontWeight = FontWeights.Bold });
                hd.Children.Add(new TextBlock { Text = "  " + day.Key.ToString("dd MMMM yyyy", IC) + (today ? "  •  TODAY" : day.Key == today0.AddDays(1) ? "  •  TOMORROW" : ""), Foreground = today ? Gold : Muted, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Bottom });
                hd.Children.Add(new TextBlock { Text = "   " + day.Value.Count + " events • " + day.Value.Count(n => n.Impact == "High") + " high", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Bottom });
                newsPanel.Children.Add(hd);
                if (day.Value.Count == 0) newsPanel.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 0, 3), Opacity = 0.7, Child = new TextBlock { Text = "no " + (imp == 0 ? "high-impact " : imp == 1 ? "high / medium " : "") + "event this day", Foreground = Muted, FontSize = 12.5 } });
                foreach (var n in newsPast ? day.Value.OrderByDescending(x => x.Utc) : day.Value.OrderBy(x => x.Utc))
                {
                    bool past = n.Utc < nowU.AddMinutes(-5), yours = mine.Contains(n.Currency); var impB = n.Impact == "High" ? Red : n.Impact == "Medium" ? Orange : n.Impact == "Low" ? Gold : Muted;
                    var r = new StackPanel { Orientation = Orientation.Horizontal };
                    r.Children.Add(new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = impB, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = n.Impact + " impact" });
                    r.Children.Add(Cell(conv(n.Utc.AddSeconds(off)).ToString("HH:mm", IC), Text, 54, false, true)); r.Children.Add(Cell(n.Currency, yours ? Cyan : Text, 44, false, true));
                    r.Children.Add(new TextBlock { Text = n.Title, Foreground = past ? Muted : Text, FontSize = 13, FontWeight = n.Impact == "High" ? FontWeights.Bold : FontWeights.Normal, Width = 360, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = n.Title });
                    r.Children.Add(Cell(n.Forecast != "" ? "forecast " + n.Forecast : "", Muted, 130)); r.Children.Add(Cell(n.Previous != "" ? "previous " + n.Previous : "", Muted, 130));
                    r.Children.Add(Cell(past ? ((int)(nowU - n.Utc).TotalHours >= 24 ? ((int)(nowU - n.Utc).TotalDays) + " d ago" : ((int)(nowU - n.Utc).TotalHours) + " h ago") : Until(n.Utc), past ? Muted : n.Impact == "High" ? Red : Text, 140, false, true));
                    newsPanel.Children.Add(new Border { Background = yours && !past ? Tint(Cyan, 22) : Card, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 0, 3), Child = r, Opacity = past ? 0.75 : 1, ToolTip = yours ? "a currency of your positions / charts" : null });
                }
            }
        }
    }
}
