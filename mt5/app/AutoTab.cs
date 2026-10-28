// SpasiX — the 🤖 AUTO tab (LIVE / PROP / DEMO / BACKTEST): the hedge automation (HedgeAuto.cs) with its parameters, START / STOP, the live
// iteration, and its own views: ITERATIONS (date, time, buys, sells, add-ons, max drawdown, result), TRADES, ANALYSIS (wins / losses, streaks,
// today's % …) and LOG. Its orders go through the same checks as the chart's buttons (ARM, MT5's Algo Trading, PROP's limits); the BACKTEST
// and the DEMO account trade their simulated account only. On a REAL MT5 account START asks you to type START. The automation owns EVERY
// position on its symbol. History (LIVE / PROP / DEMO): DataDir\auto\<mode>-<login>-iterations.csv and -trades.csv.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        readonly HedgeAuto auto = new HedgeAuto();
        readonly Dictionary<string, TextBox> af = new Dictionary<string, TextBox>();
        AutoSettings aset; TextBlock aPhase, aBasket, aInfo, aLevels; Border aCard; Button aStart, aStop, aStopClose; ContentControl aTiles; TextBox aTable; StackPanel aViews, aSettings;
        readonly Dictionary<string, Button> aDrops = new Dictionary<string, Button>();
        string aView = "STORY"; int aSeen = -1, aSaved; long aHistLogin = -1; DispatcherTimer aTimer;

        FrameworkElement BuildAuto()
        {
            aset = AutoSettings.Load(Get(AKey, Get("auto." + Mode, ""))); if (aset.Symbol == "" || aset.Symbol == "XAUUSD") { try { if (panes.Count > 0 && !string.IsNullOrEmpty(Act.Sym)) aset.Symbol = Act.Sym; } catch { } }
            auto.Send = AutoSend; auto.NewsNear = AutoNews;
            var root = new DockPanel { LastChildFill = true };

            // the live card + the buttons
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(top, Dock.Top);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(btns, Dock.Right);
            aStart = Pill("▶  START", Tint(Green, 90)); aStart.Height = 40; aStart.FontSize = 14; aStart.Click += delegate { AutoStart(); }; btns.Children.Add(aStart);
            aStop = Pill("■  STOP (positions stay)", Tint(Amber, 70)); aStop.Height = 40; aStop.ToolTip = "the automation stops; the open positions stay as they are"; aStop.Click += delegate { auto.Stop("stopped by you"); AutoPaint(true); }; btns.Children.Add(aStop);
            aStopClose = Pill("⛔  STOP + CLOSE ALL", Tint(Red, 170)); aStopClose.Height = 40; aStopClose.Foreground = Sx.White; aStopClose.Click += delegate { AutoStopClose(); }; btns.Children.Add(aStopClose);
            top.Children.Add(btns);
            var info = new StackPanel(); aPhase = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Muted, Text = "🤖 AUTO — OFF" };
            aBasket = new TextBlock { FontSize = 26, FontWeight = FontWeights.Bold, FontFamily = Sx.Mono, Foreground = Muted, Text = "—" };
            aInfo = new TextBlock { FontSize = 12.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap }; aLevels = new TextBlock { FontSize = 12.5, Foreground = Cyan, FontFamily = Sx.Mono };
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(aPhase); var bk = new StackPanel { Margin = new Thickness(24, 0, 0, 0) }; bk.Children.Add(new TextBlock { Text = "BASKET (all positions on the symbol)", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Muted }); bk.Children.Add(aBasket); row.Children.Add(bk);
            info.Children.Add(row); info.Children.Add(aInfo); info.Children.Add(aLevels);
            aCard = new Border { Child = info, Background = Sx.CardGrad, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 10, 0) };
            top.Children.Add(aCard); root.Children.Add(top);

            // the parameters
            var card = new Border { Background = Sx.CardGrad, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(card, Dock.Top);
            aSettings = new StackPanel(); card.Child = aSettings;
            var r1 = new WrapPanel();
            r1.Children.Add(AField("sym", "SYMBOL", aset.Symbol, 110, "the symbol as MT5 names it (XAUUSD, NAS100, USTEC …) — the automation owns every position on it"));
            r1.Children.Add(ADrop("tf", "TIMEFRAME", () => Tf.Name(aset.Tf), () => Tf.All.Select(m => new Mi { Text = Tf.Name(m), Do = () => { aset.Tf = m; } }).ToList()));
            r1.Children.Add(AField("look", "RECENT HIGH / LOW (CANDLES)", aset.Lookback.ToString(IC), 60, "the high / low of the last N FINISHED candles of the timeframe (1 = the previous candle)"));
            r1.Children.Add(AField("entry", "ENTRY LOTS", aset.EntryLots.ToString(IC), 70, "the first trade of every iteration"));
            r1.Children.Add(AField("add", "ADD-ON LOTS (OTHER WAY)", aset.AddLots.ToString(IC), 70, "each add-on the other way: entry 0.5 buy → add-on 1.0 sell (net 0.5 sell) → 1.0 buy …"));
            r1.Children.Add(AField("target", "TARGET $ / ITERATION", aset.Target.ToString(IC), 80, "the whole basket closes at +$ (profit + swap + commission) — whatever the drawdown on the way"));
            r1.Children.Add(AField("dd", "MAX DRAWDOWN $ (0 = NONE)", aset.MaxDrawdown.ToString(IC), 80, "0 = none: until the target or the account blows; else the basket closes at −$"));
            aSettings.Children.Add(r1);
            var r2 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            r2.Children.Add(ADrop("first", "FIRST DIRECTION", () => aset.FirstDir == "AUTO" ? "AUTO — the break" : aset.FirstDir + " — at once", () => new List<Mi> { new Mi { Text = "AUTO — the first break of the recent high (BUY) / low (SELL)", Do = () => aset.FirstDir = "AUTO" }, new Mi { Text = "BUY — at once", Fg = Green, Do = () => aset.FirstDir = "BUY" }, new Mi { Text = "SELL — at once", Fg = Red, Do = () => aset.FirstDir = "SELL" } }));
            r2.Children.Add(ADrop("hmode", "ADD-ON WHEN", () => aset.HedgeMode == "PRICE" ? "a PRICE move" : aset.HedgeMode == "MONEY" ? "a $ LOSS" : "the BREAK of the recent low / high", () => new List<Mi> { new Mi { Text = "BREAK — the recent low (after a buy) / high (after a sell)", Do = () => aset.HedgeMode = "BREAK" }, new Mi { Text = "PRICE — a fixed move against the last leg (gold 5 = $5, NAS100 50 = 50 points)", Do = () => aset.HedgeMode = "PRICE" }, new Mi { Text = "$ LOSS — a fixed $ lost since the last leg (on what the basket holds)", Do = () => aset.HedgeMode = "MONEY" } }));
            r2.Children.Add(AField("hprice", "PRICE MOVE", aset.HedgePrice.ToString(IC), 60, "ADD-ON WHEN = PRICE: the move against the last leg in price (gold: 5 = $5; NAS100: 50 = 50 points)"));
            r2.Children.Add(AField("hmoney", "$ LOSS", aset.HedgeMoney.ToString(IC), 60, "ADD-ON WHEN = $ LOSS: the $ lost since the last leg opened"));
            r2.Children.Add(ADrop("roll", "BREAK LEVEL", () => aset.Rolling ? "follows new candles" : "fixed at the leg", () => new List<Mi> { new Mi { Text = "FIXED — the recent low / high when the leg opened", Do = () => aset.Rolling = false }, new Mi { Text = "FOLLOWS — the recent low / high of the newest candles", Do = () => aset.Rolling = true } }));
            r2.Children.Add(ADrop("next", "NEXT ITERATION", () => aset.NextStart == "PAUSE" ? "after a PAUSE" : "the next BREAK", () => new List<Mi> { new Mi { Text = "the very next BREAK of the recent high (BUY) / low (SELL)", Do = () => aset.NextStart = "BREAK" }, new Mi { Text = "after a PAUSE — then the last basket's direction at once", Do = () => aset.NextStart = "PAUSE" } }));
            r2.Children.Add(AField("pause", "PAUSE (SECONDS)", aset.PauseSec.ToString(IC), 60, "NEXT ITERATION = PAUSE: seconds of the market's clock (300 = 5 minutes)"));
            r2.Children.Add(ADrop("afterdd", "AFTER MAX DRAWDOWN", () => aset.AfterDrawdown == "NEXT" ? "the NEXT iteration" : "STOP", () => new List<Mi> { new Mi { Text = "STOP the automation", Do = () => aset.AfterDrawdown = "STOP" }, new Mi { Text = "the NEXT iteration", Do = () => aset.AfterDrawdown = "NEXT" } }));
            r2.Children.Add(AField("maxit", "MAX ITERATIONS (0 = ∞)", aset.MaxIterations.ToString(IC), 60, ""));
            r2.Children.Add(AField("comm", "COMMISSION $/LOT", aset.CommissionPerLot.ToString(IC), 60, "round turn, taken off the basket when MT5 does not show it (RAW accounts); 0 = as MT5 shows"));
            aSettings.Children.Add(r2);
            var r3 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            r3.Children.Add(ADrop("emode", "ENTRY SETUP", () => aset.EntryMode == "FVG" ? "FVG retest" : aset.EntryMode == "VWAP" ? "VWAP cross" : aset.EntryMode == "FLOW" ? "ORDER FLOW surge" : "BREAK of high / low", () => new List<Mi> {
                new Mi { Text = "BREAK — the recent high (BUY) / low (SELL) of the last N finished candles", Do = () => aset.EntryMode = "BREAK" },
                new Mi { Text = "FVG — the price comes back into a fresh fair value gap (bullish = BUY, bearish = SELL)", Do = () => aset.EntryMode = "FVG" },
                new Mi { Text = "VWAP — the price crosses the day's VWAP (up = BUY, down = SELL)", Do = () => aset.EntryMode = "VWAP" },
                new Mi { Text = "ORDER FLOW — a tick-volume surge candle closing strong, then its high / low breaks (estimated: MT5 CFDs have no order book)", Do = () => aset.EntryMode = "FLOW" } }));
            r3.Children.Add(AField("win", "TRADING TIMES (NEW YORK)", aset.Windows, 190, "new iterations only inside these New York times, e.g. 08:30-11:00, 13:00-16:00 — empty = all the time (an open iteration always runs to its end)"));
            r3.Children.Add(ADrop("winp", "SESSIONS", () => "presets", () => new List<Mi> {
                new Mi { Text = "ALL THE TIME (empty)", Do = () => af["win"].Text = "" }, new Mi { Text = "Asia 19:00-23:00", Do = () => af["win"].Text = "19:00-23:00" },
                new Mi { Text = "London 03:00-08:00", Do = () => af["win"].Text = "03:00-08:00" }, new Mi { Text = "NY open 08:30-10:30", Do = () => af["win"].Text = "08:30-10:30" },
                new Mi { Text = "New York 09:30-16:00", Do = () => af["win"].Text = "09:30-16:00" }, new Mi { Text = "London + NY 03:00-12:00", Do = () => af["win"].Text = "03:00-12:00" },
                new Mi { Text = "London open + NY open 03:00-05:00, 08:30-10:30", Do = () => af["win"].Text = "03:00-05:00, 08:30-10:30" } }));
            r3.Children.Add(AField("nyoff", "NY = SERVER +", aset.NyOffsetHours.ToString(IC), 50, "hours from MT5's server clock to New York (IC Markets −7)"));
            r3.Children.Add(AField("perday", "ITERATIONS A DAY (0 = ∞)", aset.PerDay.ToString(IC), 60, "0 = unlimited (until you stop it / the trading times end)"));
            r3.Children.Add(ADrop("news", "NEWS (HIGH IMPACT)", () => aset.NewsMode == "AVOID" ? "AVOID" : aset.NewsMode == "ONLY" ? "ONLY around news" : "ignore", () => new List<Mi> {
                new Mi { Text = "IGNORE the news", Do = () => aset.NewsMode = "IGNORE" }, new Mi { Text = "AVOID — no new iteration around high-impact news (ForexFactory, the symbol's currencies + USD)", Do = () => aset.NewsMode = "AVOID" },
                new Mi { Text = "ONLY — new iterations only around high-impact news", Do = () => aset.NewsMode = "ONLY" } }));
            r3.Children.Add(AField("nb", "MIN BEFORE", aset.NewsBefore.ToString(IC), 50, "")); r3.Children.Add(AField("na", "MIN AFTER", aset.NewsAfter.ToString(IC), 50, ""));
            aSettings.Children.Add(r3);
            var r4 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            r4.Children.Add(ADrop("winner", "ADD TO THE WINNER", () => aset.AddToWinner ? "ON" : "OFF", () => new List<Mi> { new Mi { Text = "OFF — one leading leg", Do = () => aset.AddToWinner = false }, new Mi { Text = "ON — on each new signal of the setup in the leading side's direction (a new high after a buy …) until the target", Do = () => aset.AddToWinner = true } }));
            r4.Children.Add(AField("wlots", "WINNER LOTS", aset.WinnerLots.ToString(IC), 60, "the lots each add to the winner"));
            r4.Children.Add(AField("wmax", "MAX ADDS / ITERATION (0 = ∞)", aset.MaxWinnerAdds.ToString(IC), 60, ""));
            r4.Children.Add(AField("fvol", "FLOW: VOLUME ×", aset.FlowVol.ToString(IC), 50, "ORDER FLOW: the candle's tick volume at least this × the average of the last 20"));
            r4.Children.Add(AField("fshare", "FLOW: BUYERS / SELLERS ≥", aset.FlowShare.ToString(IC), 50, "ORDER FLOW: the estimated buyers' (or sellers') share, 0.5 – 1"));
            aSettings.Children.Add(r4);
            root.Children.Add(card);

            // the numbers, the views
            aTiles = new ContentControl { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(aTiles, Dock.Top); root.Children.Add(aTiles);
            aViews = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(aViews, Dock.Top);
            foreach (var v in new[] { "STORY", "ITERATIONS", "TRADES", "ANALYSIS", "LOG" }) { var vv = v; var b = Pill(v, Tint(Sx.Card2, 220)); b.Height = 26; b.Click += delegate { aView = vv; AutoPaint(true); }; aViews.Children.Add(b); }
            var clr = Pill("🗑 CLEAR HISTORY", Tint(Sx.Card2, 220)); clr.Height = 26; clr.Click += delegate { AutoClear(); }; aViews.Children.Add(clr);
            root.Children.Add(aViews);
            aTable = new TextBox { IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 12.5, Background = Panel, Foreground = Text, BorderBrush = Line, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, Padding = new Thickness(8) };
            root.Children.Add(aTable);

            // the engine's clock: every tick of the BACKTEST; every 200 ms live (the bridge sends the state every 200 ms)
            if (Sim) sim.OnTickHook = () => auto.Step(sim);
            aTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
            aTimer.Tick += delegate { if (!Sim) { try { auto.Step(broker); } catch (Exception ex) { auto.Stop("error: " + ex.Message); } } AutoSaveNew(); AutoPaint(false); };
            aTimer.Start();
            Closed += delegate { aTimer.Stop(); if (auto.Running) auto.Stop("the window closed"); if (Sim) sim.OnTickHook = null; };
            AutoPaint(true);
            return root;
        }

        FrameworkElement AField(string key, string label, string val, double w, string tip)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 4) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            var tb = Sx.Input(val, w); if (tip != "") tb.ToolTip = tip; af[key] = tb; sp.Children.Add(tb); return sp;
        }
        FrameworkElement ADrop(string key, string label, Func<string> now, Func<List<Mi>> items)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 4) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            var b = Pill(now() + "  ▾", Tint(Sx.Card2, 230)); b.Height = 28; b.Tag = now; aDrops[key] = b;
            b.Click += delegate { if (auto.Running) return; var list = items(); foreach (var m in list) { var act = m.Do; m.Do = () => { act(); b.Content = now() + "  ▾"; AutoSaveSet(); }; } SxMenu.Show(b, new Point(0, b.ActualHeight + 4), list); };
            sp.Children.Add(b); return sp;
        }
        double ANum(string k, double def) { double x; return double.TryParse((af[k].Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, IC, out x) ? x : def; }
        AutoSettings AutoRead()
        {
            var s = aset.Copy(); s.Symbol = (af["sym"].Text ?? "").Trim(); s.Lookback = (int)Math.Max(1, ANum("look", 1)); s.EntryLots = ANum("entry", 0); s.AddLots = ANum("add", 0); s.Target = ANum("target", 0);
            s.MaxDrawdown = Math.Max(0, ANum("dd", 0)); s.HedgePrice = ANum("hprice", 0); s.HedgeMoney = ANum("hmoney", 0); s.PauseSec = (int)Math.Max(0, ANum("pause", 60)); s.MaxIterations = (int)Math.Max(0, ANum("maxit", 0)); s.CommissionPerLot = Math.Max(0, ANum("comm", 0));
            s.Windows = (af["win"].Text ?? "").Trim(); s.NyOffsetHours = (int)ANum("nyoff", -7); s.PerDay = (int)Math.Max(0, ANum("perday", 0)); s.NewsBefore = (int)Math.Max(0, ANum("nb", 15)); s.NewsAfter = (int)Math.Max(0, ANum("na", 15));
            s.WinnerLots = ANum("wlots", 0.1); s.MaxWinnerAdds = (int)Math.Max(0, ANum("wmax", 0)); s.FlowVol = Math.Max(1, ANum("fvol", 1.5)); s.FlowShare = Math.Max(0.5, Math.Min(1, ANum("fshare", 0.7)));
            return s;
        }
        // the automation's settings: per MT5 account on LIVE / PROP (each account its own), per mode otherwise
        string AKey { get { return "auto." + Mode + (!Virtual && live != null && live.Key != "" ? "." + live.Key : ""); } }
        void AutoSaveSet() { aset = AutoRead(); Put(AKey, aset.Save()); }

        // a high-impact ForexFactory event near a server time, for the symbol's currencies (+ USD: gold, indices)
        string AutoNews(DateTime server, int before, int after)
        {
            int off; lock (broker.State.Gate) off = broker.State.ServerOffsetSec; var utc = server.AddSeconds(-off); var sym = (auto.Set.Symbol ?? "").ToUpperInvariant();
            var cur = new HashSet<string> { "USD" }; foreach (var c in new[] { "EUR", "GBP", "JPY", "AUD", "NZD", "CAD", "CHF", "CNY" }) if (sym.Contains(c)) cur.Add(c);
            var n = news.Where(x => x.Impact == "High" && cur.Contains(x.Currency) && utc >= x.Utc.AddMinutes(-before) && utc <= x.Utc.AddMinutes(after)).OrderBy(x => x.Utc).FirstOrDefault();
            return n == null ? null : n.Currency + " " + n.Title + " " + n.Utc.AddSeconds(off).ToString("HH:mm", IC) + " (server)";
        }

        // the order path: the same checks as the chart's buttons
        string AutoSend(string cmd, object[] args, string label)
        {
            bool can; lock (broker.State.Gate) can = broker.State.CanTrade;
            if (!Virtual && !can) { auto.Refused = "MT5 does not allow trading (Algo Trading in MT5, the EA's Allow Algo Trading, EnableTrading)"; return null; }
            if (!Virtual && !armed) { auto.Refused = "trading is DISARMED — 🔓 ARM in the chart's panel"; return null; }
            string why; if (cmd == "ORDER" && !PropAllows(out why)) { auto.Refused = why; return null; }
            var id = broker.Command(cmd, args); if (id == null) { auto.Refused = "MT5 is not connected"; return null; }
            sent[id] = "🤖 " + label; Log("→ 🤖 " + label, Muted); return id;
        }

        void AutoStart()
        {
            if (auto.Running) return; AutoSaveSet(); var s = aset.Copy();
            var err = auto.Check(broker, s); if (err != "") { Sx.Info(this, "🤖 AUTO", err, Red); return; }
            bool can; string kind; long login; int open; double pl; double bal;
            lock (broker.State.Gate) { can = broker.State.CanTrade; kind = broker.State.Account.Kind; login = broker.State.Account.Login; var ps = broker.State.Positions.Where(p => string.Equals(p.Symbol, s.Symbol, StringComparison.OrdinalIgnoreCase)).ToList(); open = ps.Count; pl = ps.Sum(p => p.Profit + p.Swap + p.Commission); bal = broker.State.Account.Balance; }
            if (!Virtual && !can) { Sx.Info(this, "🤖 AUTO", "MT5 does not allow trading yet: turn on Algo Trading in MT5 and the EA's Allow Algo Trading (the ⚙ SETUP tab shows every switch).", Red); return; }
            if (!Virtual && !armed) { Sx.Info(this, "🤖 AUTO", "Trading is not ARMED. Open 📈 CHART and press 🔓 ARM TRADING in the panel first — the automation trades through the same armed link.", Amber); return; }
            bool adopt = false;
            if (open > 0)
            {
                int k = Sx.Choice(this, "🤖 " + open + " position" + (open == 1 ? "" : "s") + " already open on " + s.Symbol, "The automation owns EVERY position on its symbol. Take these over as the current iteration (their P/L " + Signed(pl, "USD") + " counts toward the target), or cancel and close them first.", null, new[] { "TAKE THEM OVER", "CANCEL" }, new[] { Gold, (SolidColorBrush)null });
                if (k != 0) return; adopt = true;
            }
            string what = s.Symbol + " " + Tf.Name(s.Tf) + "  •  entry " + s.EntryLots.ToString("0.##", IC) + "  •  add-on " + s.AddLots.ToString("0.##", IC) + " the other way (" + (s.HedgeMode == "PRICE" ? "a " + s.HedgePrice.ToString("0.#####", IC) + " price move" : s.HedgeMode == "MONEY" ? "−$" + s.HedgeMoney.ToString("0.##", IC) : "the break of the recent low / high") + ")\n"
                + "target +$" + s.Target.ToString("0.##", IC) + " per iteration  •  max drawdown " + (s.MaxDrawdown > 0 ? "$" + s.MaxDrawdown.ToString("0.##", IC) + " then " + s.AfterDrawdown : "NONE — until the target or the account blows") + "\n"
                + "first: " + (s.FirstDir == "AUTO" ? "the first break" : s.FirstDir + " at once") + "  •  next: " + (s.NextStart == "PAUSE" ? "after " + s.PauseSec + " s" : "the next break") + (s.MaxIterations > 0 ? "  •  " + s.MaxIterations + " iterations" : "");
            if (Sim || Paper) { if (!Sx.Ask(this, "🤖 START the automation", (Sim ? "On the BACKTEST's simulated account (press ▶ play on the 📈 CHART)." : "On the DEMO account (simulated, live prices).") + "\n\n" + what, "▶ START", "CANCEL", Green)) return; }
            else if (kind == "REAL")
            {
                var typed = Sx.Prompt(this, "⚠ 🤖 START on a REAL account", "This MT5 account (" + login + ") is REAL MONEY. The automation sends orders by itself, with no stop" + (s.MaxDrawdown > 0 ? " but the max drawdown" : "") + ".\n\n" + what + "\n\nType START to go on.", "");
                if (typed == null || typed.Trim().ToUpperInvariant() != "START") return;
            }
            else if (!Sx.Ask(this, "🤖 START on MT5 (" + kind + " account " + login + ")", "Orders go to MT5 by themselves while it runs.\n\n" + what, "▶ START", "CANCEL", Green)) return;
            AutoLoadHistory(login); auto.Start(broker, s, adopt); AutoPaint(true);
        }
        void AutoStopClose()
        {
            var sym = auto.Running ? auto.Set.Symbol : (af["sym"].Text ?? "").Trim(); int open; lock (broker.State.Gate) open = broker.State.Positions.Count(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase));
            if (!Sx.Ask(this, "⛔ STOP + CLOSE ALL", "The automation stops and the " + open + " position" + (open == 1 ? "" : "s") + " on " + sym + " close at the market.", "⛔ STOP + CLOSE", "CANCEL", Red)) return;
            auto.Stop("stopped by you (close all)"); if (open > 0) { string id = Virtual || armed ? broker.Command("CLOSEALL", sym, "ALL") : null; if (id == null) Sx.Info(this, "🤖 AUTO", "Not closed: trading is not armed — close them on the 💼 POSITIONS tab.", Amber); else sent[id] = "🤖 close all " + sym; }
            AutoPaint(true);
        }
        void AutoClear()
        {
            if (auto.Running) { Sx.Info(this, "🤖 AUTO", "Stop the automation first.", Amber); return; }
            if (!Sx.Ask(this, "🗑 Clear the history", "Every iteration, trade and log line of the automation in this " + Mode + " window is removed" + (Virtual ? "." : " (and its files on this PC)."), "CLEAR", "CANCEL", Red)) return;
            auto.Iterations.Clear(); auto.Trades.Clear(); auto.Log.Clear(); aSaved = 0; auto.Version++;
            if (!Virtual && aHistLogin > 0) { try { File.Delete(AutoFile(aHistLogin, "iterations")); File.Delete(AutoFile(aHistLogin, "trades")); } catch { } }
            AutoPaint(true);
        }

        // ---- history on this PC (LIVE / PROP / DEMO): one line per finished iteration, its trades with it
        string AutoFile(long login, string what) { return Path.Combine(Dir, "auto", Mode + "-" + login + "-" + what + ".csv"); }
        void AutoLoadHistory(long login)
        {
            if (Sim || aHistLogin == login) return; aHistLogin = login; if (auto.Iterations.Count > 0) return;
            try
            {
                var f = AutoFile(login, "iterations"); if (File.Exists(f)) foreach (var l in File.ReadAllLines(f).Skip(1)) { var a = l.Split(','); if (a.Length < 16) continue; Func<int, double> d = i => double.Parse(a[i], IC); auto.Iterations.Add(new AutoIteration { N = int.Parse(a[0], IC), Start = new DateTime(long.Parse(a[1], IC)), End = new DateTime(long.Parse(a[2], IC)), FirstDir = a[3], Buys = int.Parse(a[4], IC), Sells = int.Parse(a[5], IC), AddOns = int.Parse(a[6], IC), BuyLots = d(7), SellLots = d(8), MaxGross = d(9), MaxDd = d(10), Result = d(11), Reason = a[12], BalanceStart = d(13), BalanceEnd = d(14), Peak = d(15), WinnerAdds = a.Length > 16 ? int.Parse(a[16], IC) : 0 }); }
                var t = AutoFile(login, "trades"); if (File.Exists(t)) foreach (var l in File.ReadAllLines(t).Skip(1)) { var a = l.Split(new[] { ',' }, 8); if (a.Length < 8) continue; auto.Trades.Add(new AutoTrade { Iter = int.Parse(a[0], IC), Time = new DateTime(long.Parse(a[1], IC)), Side = a[2], Lots = double.Parse(a[3], IC), Price = double.Parse(a[4], IC), Level = double.Parse(a[5], IC), Basket = double.Parse(a[6], IC), Why = a[7].Trim('"') }); }
            }
            catch { }
            aSaved = auto.Iterations.Count(x => x.Done); auto.Version++;
        }
        void AutoSaveNew()
        {
            if (Sim || aHistLogin <= 0) return; var done = auto.Iterations.Where(x => x.Done).ToList(); if (done.Count <= aSaved) return;
            try
            {
                Directory.CreateDirectory(Path.Combine(Dir, "auto")); var f = AutoFile(aHistLogin, "iterations"); var t = AutoFile(aHistLogin, "trades");
                if (!File.Exists(f)) File.WriteAllText(f, "n,start,end,first,buys,sells,addons,buylots,selllots,maxgross,maxdd,result,reason,balstart,balend,peak,winneradds\n");
                if (!File.Exists(t)) File.WriteAllText(t, "iteration,time,side,lots,price,level,basket,why\n");
                foreach (var x in done.Skip(aSaved))
                {
                    File.AppendAllText(f, string.Join(",", x.N.ToString(IC), x.Start.Ticks.ToString(IC), x.End.Ticks.ToString(IC), x.FirstDir, x.Buys.ToString(IC), x.Sells.ToString(IC), x.AddOns.ToString(IC), x.BuyLots.ToString("R", IC), x.SellLots.ToString("R", IC), x.MaxGross.ToString("R", IC), x.MaxDd.ToString("R", IC), x.Result.ToString("R", IC), x.Reason, x.BalanceStart.ToString("R", IC), x.BalanceEnd.ToString("R", IC), x.Peak.ToString("R", IC), x.WinnerAdds.ToString(IC)) + "\n");
                    File.AppendAllLines(t, auto.Trades.Where(tr => tr.Iter == x.N && tr.Time >= x.Start).Select(tr => string.Join(",", tr.Iter.ToString(IC), tr.Time.Ticks.ToString(IC), tr.Side, tr.Lots.ToString("R", IC), tr.Price.ToString("R", IC), (double.IsNaN(tr.Level) ? 0 : tr.Level).ToString("R", IC), tr.Basket.ToString("R", IC), "\"" + tr.Why.Replace("\"", "'").Replace(",", ";") + "\"")));
                }
                aSaved = done.Count;
            }
            catch { }
        }

        // ---- the screen
        void AutoPaint(bool force)
        {
            if (aPhase == null) return; bool on = auto.Running; var s = on ? auto.Set : aset; var it = auto.Current;
            aPhase.Text = "🤖 AUTO — " + (on ? auto.Phase : "OFF") + (on ? "  •  " + s.Symbol + " " + Tf.Name(s.Tf) : ""); aPhase.Foreground = on ? Green : Muted;
            aCard.BorderBrush = on ? Green : Line;
            aBasket.Text = it != null ? Signed(auto.Basket, "USD") + "  /  +$" + s.Target.ToString("0.##", IC) : "—"; aBasket.Foreground = it == null ? Muted : auto.Basket >= 0 ? Green : Red;
            aInfo.Text = on ? (it != null ? "iteration #" + it.N + " since " + it.Start.ToString("dd MMM HH:mm:ss", IC) + "  •  " + it.Buys + " buys (" + it.BuyLots.ToString("0.##", IC) + ") / " + it.Sells + " sells (" + it.SellLots.ToString("0.##", IC) + ")  •  " + it.AddOns + " add-ons  •  max drawdown " + it.MaxDd.ToString("0.00", IC) + (s.MaxDrawdown > 0 ? " (limit −$" + s.MaxDrawdown.ToString("0.##", IC) + ")" : " (no limit)")
                    : auto.Phase == "PAUSE" ? "pause until " + auto.PauseUntil.ToString("HH:mm:ss", IC) + " (" + Math.Max(0, (auto.PauseUntil - auto.Now).TotalSeconds).ToString("0", IC) + " s)" : auto.Phase == "FIRST" ? "the first " + s.FirstDir + " goes now" : "waiting for the next break of the recent high (BUY) / low (SELL)")
                : (auto.StopReason != "" ? "stopped: " + auto.StopReason + "  •  " : "") + (Sim ? "BACKTEST: press START here, then ▶ play on the 📈 CHART." : Paper ? "DEMO account: simulated on live prices." : "MT5 account: needs 🔓 ARM (chart panel) and MT5's Algo Trading.");
            aInfo.Foreground = !on && auto.StopReason != "" ? Amber : Muted;
            aLevels.Text = on ? "recent high " + Px(auto.BuyLevel) + "  •  recent low " + Px(auto.SellLevel) + (it != null ? "  •  next add-on (" + (s.HedgeMode == "PRICE" ? "price move" : s.HedgeMode == "MONEY" ? "$ loss" : "break") + ") at " + Px(auto.HedgeLevel) : "") : "";
            aStart.IsEnabled = !on; aStop.IsEnabled = on; foreach (var tb in af.Values) tb.IsEnabled = !on; foreach (var b in aDrops.Values) b.IsEnabled = !on;
            if (!force && auto.Version == aSeen) return; aSeen = auto.Version;
            AutoTilesPaint(); AutoTablePaint();
        }
        static string Px(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.#####", IC); }

        void AutoTilesPaint()
        {
            var st = auto.Calc(auto.Iterations); var today = (auto.Now > DateTime.MinValue ? auto.Now : DateTime.Now).Date;
            var td = auto.Iterations.Where(x => x.Done && x.End.Date == today).ToList(); double tdNet = td.Sum(x => x.Result); var first = auto.Iterations.FirstOrDefault(x => x.Start.Date == today); double tdBase = first != null ? first.BalanceStart : 0;
            aTiles.Content = Sx.Tiles(
                Tuple.Create("ITERATIONS", st.Done + (auto.Current != null ? " (+1 open)" : ""), Text),
                Tuple.Create("WON / LOST", st.Wins + " / " + st.Losses + (st.Done > 0 ? "  " + (100.0 * st.Wins / st.Done).ToString("0", IC) + "%" : ""), st.Losses > 0 ? Amber : Green),
                Tuple.Create("STREAK NOW / BEST", (st.Streak > 0 ? st.Streak + " won" : st.Streak < 0 ? -st.Streak + " lost" : "—") + " / " + st.BestStreak, st.Streak >= 0 ? Green : Red),
                Tuple.Create("TODAY", Signed(tdNet, "USD") + (tdBase > 0 ? "  " + (100 * tdNet / tdBase).ToString("+0.00;−0.00", IC) + "%" : ""), tdNet >= 0 ? Green : Red),
                Tuple.Create("NET", Signed(st.Net, "USD"), st.Net >= 0 ? Green : Red),
                Tuple.Create("WORST DRAWDOWN", st.WorstDd.ToString("0.00", IC), st.WorstDd < 0 ? Red : Muted),
                Tuple.Create("BUYS / SELLS / ADD-ONS", st.Buys + " / " + st.Sells + " / " + st.AddOns, Violet),
                Tuple.Create("MOST ADD-ONS", st.MostAddOns.ToString(IC), Cyan));
        }
        void AutoTablePaint()
        {
            foreach (Button b in aViews.Children) if (b.Content is string) b.Background = (string)b.Content == aView ? Tint(Cyan, 70) : Tint(Sx.Card2, 220);
            var L = new List<string>();
            if (aView == "ITERATIONS")
            {
                L.Add(string.Format("{0,4}  {1,-10} {2,-8} {3,-8} {4,-5} {5,5} {6,5} {7,7} {8,9} {9,9} {10,9} {11,11} {12,11}  {13}", "#", "DATE", "START", "END", "FIRST", "BUYS", "SELLS", "HEDGES", "BUY LOTS", "SELL LOTS", "MAX LOTS", "MAX DD", "RESULT", "HOW"));
                foreach (var x in auto.Iterations.AsEnumerable().Reverse()) L.Add(string.Format(IC, "{0,4}  {1,-10} {2,-8} {3,-8} {4,-5} {5,5} {6,5} {7,7} {8,9:0.##} {9,9:0.##} {10,9:0.##} {11,11:N2} {12,11}  {13}", x.N, x.Start.ToString("dd MMM yy", IC), x.Start.ToString("HH:mm:ss", IC), x.Done ? x.End.ToString("HH:mm:ss", IC) : "open", x.FirstDir, x.Buys, x.Sells, x.AddOns, x.BuyLots, x.SellLots, x.MaxGross, x.MaxDd, x.Done ? x.Result.ToString("+#,0.00;−#,0.00;0.00", IC) : "(" + auto.Basket.ToString("+0.00;−0.00", IC) + ")", x.Reason + (x.WinnerAdds > 0 ? " • " + x.WinnerAdds + " added to the winner" : "") + (x.Adopted ? " (taken over)" : "")));
                if (auto.Iterations.Count == 0) L.Add("no iterations yet");
            }
            else if (aView == "TRADES")
            {
                L.Add(string.Format("{0,4}  {1,-10} {2,-8} {3,-5} {4,6} {5,12} {6,12} {7,10}  {8}", "#", "DATE", "TIME", "SIDE", "LOTS", "PRICE", "LEVEL", "BASKET", "WHY"));
                foreach (var t in auto.Trades.AsEnumerable().Reverse()) L.Add(string.Format(IC, "{0,4}  {1,-10} {2,-8} {3,-5} {4,6:0.##} {5,12} {6,12} {7,10:N2}  {8}", t.Iter, t.Time.ToString("dd MMM yy", IC), t.Time.ToString("HH:mm:ss", IC), t.Side, t.Lots, Px(t.Price), Px(t.Level), t.Basket, t.Why));
                if (auto.Trades.Count == 0) L.Add("no trades yet");
            }
            else if (aView == "ANALYSIS")
            {
                var st = auto.Calc(auto.Iterations); var d = auto.Iterations.Where(x => x.Done).ToList();
                L.Add("ITERATIONS " + st.Done + "   won " + st.Wins + "   lost " + st.Losses + (st.Done > 0 ? "   win rate " + (100.0 * st.Wins / st.Done).ToString("0.0", IC) + "%" : ""));
                L.Add("NET " + Signed(st.Net, "USD") + "   average " + Signed(st.Avg, "USD") + "   best " + Signed(st.BestIt, "USD") + "   worst " + Signed(st.WorstIt, "USD"));
                L.Add("STREAKS   now " + (st.Streak >= 0 ? st.Streak + " won" : -st.Streak + " lost") + "   best winning " + st.BestStreak + "   worst losing " + st.WorstStreak);
                L.Add("TRADES    " + st.Buys + " buys, " + st.Sells + " sells, " + st.AddOns + " add-ons (most in one iteration: " + st.MostAddOns + ")");
                L.Add("DRAWDOWN  worst inside an iteration " + st.WorstDd.ToString("0.00", IC) + (d.Count > 0 ? "   average " + d.Average(x => x.MaxDd).ToString("0.00", IC) : ""));
                if (d.Count > 0) L.Add("TIME      average iteration " + TimeSpan.FromSeconds(d.Average(x => (x.End - x.Start).TotalSeconds)).ToString(@"hh\:mm\:ss", IC) + "   longest " + TimeSpan.FromSeconds(d.Max(x => (x.End - x.Start).TotalSeconds)).ToString(@"d\.hh\:mm\:ss", IC));
                L.Add(""); L.Add("BY ADD-ONS (how many add-ons an iteration needed → how it ended)");
                foreach (var g in d.GroupBy(x => x.AddOns).OrderBy(g => g.Key)) L.Add(string.Format(IC, "  {0,3} add-ons: {1,4} iterations   won {2,4}   net {3,12}   worst drawdown {4,10:N2}", g.Key, g.Count(), g.Count(x => x.Won), Signed(g.Sum(x => x.Result), "USD"), g.Min(x => x.MaxDd)));
                L.Add(""); L.Add(string.Format("{0,-12} {1,6} {2,5} {3,5} {4,12} {5,9}", "DAY", "ITER.", "WON", "LOST", "P/L", "% OF DAY"));
                foreach (var g in d.GroupBy(x => x.End.Date).OrderByDescending(g => g.Key)) { var b0 = g.OrderBy(x => x.Start).First().BalanceStart; double net = g.Sum(x => x.Result); L.Add(string.Format(IC, "{0,-12} {1,6} {2,5} {3,5} {4,12} {5,9}", g.Key.ToString("ddd dd MMM", IC), g.Count(), g.Count(x => x.Won), g.Count(x => !x.Won), Signed(net, "USD"), b0 > 0 ? (100 * net / b0).ToString("+0.00;−0.00", IC) + "%" : "")); }
                L.Add(""); L.Add(string.Format("{0,-6} {1,6} {2,5} {3,12}", "HOUR", "ITER.", "WON", "P/L")); foreach (var g in d.GroupBy(x => x.Start.Hour).OrderBy(g => g.Key)) L.Add(string.Format(IC, "{0,-6} {1,6} {2,5} {3,12}", g.Key.ToString("00", IC) + ":00", g.Count(), g.Count(x => x.Won), Signed(g.Sum(x => x.Result), "USD")));
            }
            else if (aView == "STORY")
            {
                L.Add("WHAT HAPPENED AND WHY — the newest iteration first, each one from its entry to its close (the candle, the level, the prices, the $)");
                foreach (var g in auto.Story.GroupBy(x => x.Item1).OrderByDescending(g => g.Key)) { L.Add(""); L.Add(g.Key > 0 ? "──── ITERATION #" + g.Key + " ────" : "──── waiting ────"); foreach (var x in g) L.Add(x.Item2); }
                if (auto.Story.Count == 0) L.Add("\nnothing yet — START the automation");
            }
            else L.AddRange(auto.Log.AsEnumerable().Reverse());
            aTable.TextWrapping = aView == "STORY" || aView == "LOG" ? TextWrapping.Wrap : TextWrapping.NoWrap;
            aTable.Text = string.Join("\n", L);
        }
    }
}
