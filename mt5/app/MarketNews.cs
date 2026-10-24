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
        StackPanel newsPanel; TextBlock newsHead; ComboBox newsImpact; string newsSig = "";

        // ---------------------------------------------------------------- MARKET
        FrameworkElement BuildMarket()
        {
            var sp = new StackPanel();
            var top = new DockPanel { Margin = new Thickness(2, 0, 0, 8) };
            var add = Pill("＋ ADD AN INSTRUMENT", Tint(Cyan, 60)); add.Click += delegate { PickSymbol(); }; DockPanel.SetDock(add, Dock.Right); top.Children.Add(add);
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
                    var chg = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0) };
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
                var d1 = s.SeriesOf(q.Symbol, 1440); if ((now - d1.AskedAt).TotalSeconds > 60) { d1.AskedAt = now; bridge.Send("BARS|" + q.Symbol + "|1440|3"); }
                if (d1.Loaded && d1.Bars.Count > 0)
                {
                    var td = d1.Bars[d1.Bars.Count - 1]; double prevC = d1.Bars.Count > 1 ? d1.Bars[d1.Bars.Count - 2].C : td.O, ch = q.Bid - prevC, pct = prevC > 0 ? 100 * ch / prevC : 0;
                    c.Item4.Text = (ch >= 0 ? "▲ +" : "▼ ") + ch.ToString(f, IC) + "  (" + pct.ToString("+0.00;−0.00", IC) + " %)   range " + td.L.ToString(f, IC) + " – " + td.H.ToString(f, IC); c.Item4.Foreground = ch >= 0 ? Green : Red;
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
            newsImpact = Combo(new[] { "🔴 HIGH only", "🟠 HIGH + MEDIUM", "ALL" }, 170); newsImpact.SelectedIndex = int.Parse(Get("newsimp", "1"), IC); newsImpact.SelectionChanged += delegate { Put("newsimp", newsImpact.SelectedIndex.ToString(IC)); newsSig = ""; };
            var reload = Pill("↻ RELOAD", Card); reload.Click += delegate { newsAt = DateTime.MinValue; LoadNews(); };
            right.Children.Add(newsImpact); right.Children.Add(reload); top.Children.Add(right);
            newsHead = new TextBlock { Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }; top.Children.Add(newsHead);
            sp.Children.Add(top); newsPanel = new StackPanel(); sp.Children.Add(newsPanel);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }
        void StartNews()
        {
            LoadNews(); var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(20) }; tm.Tick += delegate { LoadNews(); }; tm.Start();
        }
        async void LoadNews()
        {
            if ((DateTime.Now - newsAt).TotalMinutes < 5) return; newsAt = DateTime.Now; newsStatus = "loading the ForexFactory calendar …"; newsSig = "";
            try
            {
                string json;
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { http.DefaultRequestHeaders.UserAgent.ParseAdd("SpasiX/1.0"); json = await http.GetStringAsync("https://nfs.faireconomy.media/ff_calendar_thisweek.json"); }
                var list = await Task.Run(() =>
                {
                    var res = new List<NewsEvent>();
                    using (var doc = JsonDocument.Parse(json))
                        foreach (var e in doc.RootElement.EnumerateArray())
                        {
                            Func<string, string> str = k => { JsonElement v; return e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : ""; };
                            DateTimeOffset dto; if (!DateTimeOffset.TryParse(str("date"), IC, DateTimeStyles.None, out dto)) continue;
                            res.Add(new NewsEvent { Title = str("title"), Currency = str("country"), Impact = str("impact"), Forecast = str("forecast"), Previous = str("previous"), Utc = dto.UtcDateTime });
                        }
                    return res.OrderBy(x => x.Utc).ToList();
                });
                news.Clear(); news.AddRange(list); newsStatus = "📰 ForexFactory • this week • " + list.Count + " events • " + list.Count(x => x.Impact == "High") + " high impact • updated " + DateTime.Now.ToString("HH:mm", IC);
            }
            catch (Exception ex) { newsStatus = "📰 the ForexFactory calendar could not be loaded (" + ex.Message + ") — ↻ RELOAD in a minute"; }
            newsSig = "";
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
            string zn; var conv = ZoneConv(bridge.State, out zn); var local = conv(n.Utc.AddSeconds(bridge.State.ServerOffsetSec));
            return Tuple.Create("HIGH IMPACT • " + n.Currency + " " + n.Title + " • " + local.ToString("HH:mm", IC) + " " + zn + " • " + Until(n.Utc), d.TotalMinutes <= 15 && d.TotalMinutes >= -5);
        }
        void FillNews()
        {
            newsHead.Text = newsStatus + "   •   times in " + Zones[Math.Max(0, zoneBox.SelectedIndex)].Item1;
            string sig = newsStatus + "|" + newsImpact.SelectedIndex + "|" + zoneBox.SelectedIndex + "|" + DateTime.Now.Minute;
            if (sig == newsSig) return; newsSig = sig; newsPanel.Children.Clear();
            string zn; var conv = ZoneConv(bridge.State, out zn); int off = bridge.State.ServerOffsetSec;
            var mine = bridge.State.Positions.SelectMany(p => CurrenciesOf(p.Symbol)).Concat(panes.Take(split).SelectMany(p => CurrenciesOf(p.Sym))).Distinct().ToList();
            int imp = newsImpact.SelectedIndex;
            foreach (var day in news.Where(n => imp == 2 || n.Impact == "High" || (imp == 1 && n.Impact == "Medium")).GroupBy(n => conv(n.Utc.AddSeconds(off)).Date))
            {
                bool today = day.Key == conv(DateTime.UtcNow.AddSeconds(off)).Date;
                newsPanel.Children.Add(new TextBlock { Text = day.Key.ToString("dddd dd MMMM", IC).ToUpperInvariant() + (today ? "  •  TODAY" : ""), Foreground = today ? Gold : Muted, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 10, 0, 4) });
                foreach (var n in day)
                {
                    bool past = n.Utc < DateTime.UtcNow.AddMinutes(-5), yours = mine.Contains(n.Currency); var impB = n.Impact == "High" ? Red : n.Impact == "Medium" ? Orange : n.Impact == "Low" ? Gold : Muted;
                    var r = new StackPanel { Orientation = Orientation.Horizontal };
                    r.Children.Add(new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = impB, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                    r.Children.Add(Cell(conv(n.Utc.AddSeconds(off)).ToString("HH:mm", IC), Text, 54, false, true)); r.Children.Add(Cell(n.Currency, yours ? Cyan : Text, 44, false, true));
                    r.Children.Add(new TextBlock { Text = n.Title, Foreground = past ? Muted : Text, FontSize = 13, FontWeight = n.Impact == "High" ? FontWeights.Bold : FontWeights.Normal, Width = 380, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                    r.Children.Add(Cell(n.Forecast != "" ? "forecast " + n.Forecast : "", Muted, 130)); r.Children.Add(Cell(n.Previous != "" ? "previous " + n.Previous : "", Muted, 130));
                    r.Children.Add(Cell(past ? "done" : Until(n.Utc), past ? Muted : n.Impact == "High" ? Red : Text, 140, false, true));
                    newsPanel.Children.Add(new Border { Background = yours && !past ? Tint(Cyan, 22) : Card, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 0, 3), Child = r, Opacity = past ? 0.6 : 1, ToolTip = yours ? "a currency of your positions / charts" : null });
                }
            }
        }
    }
}
