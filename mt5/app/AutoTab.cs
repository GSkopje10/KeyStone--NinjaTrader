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
        AutoSettings aset; TextBlock aPhase, aBasket, aInfo, aLevels; WrapPanel aSetups; DateTime aScanAt; Border aSetCard; Button aSetBtn, aGuideBtn; bool aShowSet = true, aGuidesOn = true; List<ChartGuide> aGuides = new List<ChartGuide>(); string aGuideSym = ""; Border aCard; Button aStart, aStop, aStopClose; ContentControl aTiles; TextBox aTable; StackPanel aViews, aSettings;
        readonly Dictionary<string, Button> aDrops = new Dictionary<string, Button>();
        string aView = "STORY"; int aSeen = -1, aSaved; string aHist = null; bool aWasOn; DispatcherTimer aTimer; readonly Dictionary<string, FrameworkElement> afBox = new Dictionary<string, FrameworkElement>();

        FrameworkElement BuildAuto()
        {
            aset = AutoSettings.Load(Get(AKey, Get("auto." + Mode, ""))); if (aset.Symbol == "" || aset.Symbol == "XAUUSD") { try { if (panes.Count > 0 && !string.IsNullOrEmpty(Act.Sym)) aset.Symbol = Act.Sym; } catch { } }
            auto.Send = AutoSend; auto.NewsNear = AutoNews;
            auto.OnTell = (n, l) => AutoAppend("story", "#" + n + "  " + l); auto.OnNote = (n, l) => AutoAppend("log", l);
            aGuidesOn = Get("auto.guides", "1") == "1"; foreach (var pn in panes) { var pp = pn; pp.V.Marks = () => AutoMarks(pp.Sym); pp.V.Guides = () => aGuidesOn && string.Equals(pp.Sym, aGuideSym, StringComparison.OrdinalIgnoreCase) ? aGuides : null; }
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
            aSetups = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(aSetups, Dock.Top); root.Children.Add(aSetups);

            // the parameters
            var card = new Border { Background = Sx.CardGrad, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(card, Dock.Top); aSetCard = card; aShowSet = Get("auto.showset", "1") == "1";
            aSettings = new StackPanel(); card.Child = aSettings;
            var r0 = new WrapPanel();
            r0.Children.Add(ADrop("preset", "STRATEGY (FROM THE RESEARCH)", () => aset.Preset == "AH" ? "A + SIGNAL HEDGE" : aset.Preset == "A" ? "A · London engulfing — lower risk" : aset.Preset == "B" ? "B · NY FVG 50% — no stop" : aset.Preset == "C" ? "C · all day VWAP rejection" : aset.Preset == "BOX" ? "HEDGE BOX · consolidation flip (swap-free only)" : "your own settings", () => new List<Mi> {
                new Mi { Text = "A · LONDON ENGULFING — M5 engulfing at a 20-candle low / high, buy under VWAP / sell over, 03:00-06:00 NY, +$20, emergency stop −$300, close 16:55 NY (2023-26: profitable every year, worst drawdown $648 on $5k)", Fg = Green, Do = () => AutoModel("A") },
                new Mi { Text = "A + SIGNAL HEDGE — model A, and while it is losing $5+ an OPPOSITE M5 engulfing at a 20-candle extreme opens 2× the net the other way (once); each leg closes at its own +$20 per 0.1 lot; basket stop −$400; close 16:55 NY (2023-26: +$1,770 / +$1,612 / +$2,391 vs +$1,465 / +$1,458 / +$2,217 without the hedge)", Fg = Violet, Do = () => AutoModel("AH") },
                new Mi { Text = "B · NY FVG 50% — M5 strong fair value gap, entry at its middle within 12 min, buy over VWAP / sell under, 08:30-11:30 NY, +$20, NO stop (held until +$20; floating drawdown reached $3,130)", Fg = Amber, Do = () => AutoModel("B") },
                new Mi { Text = "C · ALL DAY VWAP REJECTION — M15: 6 candles above (under) the day's VWAP, then a dip to it closing back above (under) → BUY (SELL); 03:00-15:30 NY, +$20, emergency stop −$200, close 16:55 NY (2023-26: profitable every period, worst drawdown $1,054, ~0.7 trades a day)", Fg = Cyan, Do = () => AutoModel("C") },
                new Mi { Text = "HEDGE BOX — the 96-hour H1 consolidation: hedge (flip 0.1 → 0.2 the other way) only at a break of its high / low, nothing inside, basket +$150 — ONLY on a swap-free account (deep floating losses, −$11k per 0.1 lot seen)", Fg = Red, Do = () => AutoModel("BOX") },
                new Mi { Text = "YOUR OWN — keep the settings below as you set them", Do = () => aset.Preset = "" } }));
            r0.Children.Add(ADrop("vf", "VWAP FILTER", () => aset.VwapFilter == "DISCOUNT" ? "buy UNDER / sell OVER" : aset.VwapFilter == "FLOW" ? "buy OVER / sell UNDER" : "none", () => new List<Mi> {
                new Mi { Text = "NONE — every signal", Do = () => aset.VwapFilter = "NONE" }, new Mi { Text = "DISCOUNT — buy only under the day's VWAP, sell only over it (reversals: A)", Do = () => aset.VwapFilter = "DISCOUNT" },
                new Mi { Text = "FLOW — buy only over the VWAP, sell only under it (with the flow: B)", Do = () => aset.VwapFilter = "FLOW" } }));
            r0.Children.Add(AField("eod", "CLOSE AT (NY, EMPTY = HOLD)", aset.CloseAtNy, 70, "what is open closes at this New York time and no new iteration starts until the next day (16:55 = before the daily break); empty = hold until the target"));
            r0.Children.Add(ADrop("man", "MANUAL TRADES", () => aset.TrackManual ? "TRACK + MANAGE ✋" : "ignore", () => new List<Mi> {
                new Mi { Text = "TRACK + MANAGE — a trade you open by hand on the symbol becomes an iteration (✋): the automation takes it to the target / stop / close time and records it", Do = () => aset.TrackManual = true },
                new Mi { Text = "IGNORE — wait until your trades are closed (the old way)", Do = () => aset.TrackManual = false } }));
            r0.Children.Add(AField("fage", "FVG GOOD FOR (MIN, 0 = 60 CANDLES)", aset.FvgAge.ToString(IC), 60, "an FVG entry only within this many minutes after the gap formed (B: 12)"));
            r0.Children.Add(ADrop("fstrict", "FVG STRENGTH", () => aset.FvgStrict ? "STRONG gaps only" : "every gap", () => new List<Mi> { new Mi { Text = "EVERY gap", Do = () => aset.FvgStrict = false }, new Mi { Text = "STRONG — the middle candle's body is bigger than the average range of the last 20 (the research's gap)", Do = () => aset.FvgStrict = true } }));
            aSettings.Children.Add(r0);
            var r1 = new WrapPanel();
            r1.Children.Add(AField("sym", "SYMBOL", aset.Symbol, 110, "the symbol as MT5 names it (XAUUSD, NAS100, USTEC …) — the automation owns every position on it"));
            r1.Children.Add(ADrop("tf", "TIMEFRAME", () => Tf.Name(aset.Tf), () => Tf.All.Select(m => new Mi { Text = Tf.Name(m), Do = () => { aset.Tf = m; } }).ToList()));
            r1.Children.Add(ADrop("swing", "LEVELS (BREAKS)", () => aset.Swing ? "the last SWING high / low" : "the last N candles", () => new List<Mi> { new Mi { Text = "SWING — the most recent swing high (BUY) / swing low (SELL): a candle higher / lower than its neighbours", Do = () => aset.Swing = true }, new Mi { Text = "LAST N CANDLES — the highest high / lowest low of the last N finished candles", Do = () => aset.Swing = false } }));
            r1.Children.Add(AField("swk", "SWING STRENGTH (CANDLES EACH SIDE)", aset.SwingK.ToString(IC), 60, "a swing high = a candle higher than this many finished candles on each side (2 = the usual)"));
            r1.Children.Add(AField("look", "RECENT HIGH / LOW (CANDLES)", aset.Lookback.ToString(IC), 60, "the high / low of the last N FINISHED candles of the timeframe (1 = the previous candle)"));
            r1.Children.Add(AField("entry", "ENTRY LOTS", aset.EntryLots.ToString(IC), 70, "the first trade of every iteration"));
            r1.Children.Add(AField("add", "ADD-ON LOTS (OTHER WAY)", aset.AddLots.ToString(IC), 70, "each add-on the other way: entry 0.5 buy → add-on 1.0 sell (net 0.5 sell) → 1.0 buy …"));
            r1.Children.Add(AField("target", "TARGET $ / ITERATION", aset.Target.ToString(IC), 80, "the whole basket closes at +$ (profit + swap + commission) — whatever the drawdown on the way"));
            r1.Children.Add(AField("dd", "MAX DRAWDOWN $ (0 = NONE)", aset.MaxDrawdown.ToString(IC), 80, "0 = none: until the target or the account blows; else the basket closes at −$"));
            aSettings.Children.Add(r1);
            var r2 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            r2.Children.Add(ADrop("first", "FIRST DIRECTION", () => aset.FirstDir == "AUTO" ? "AUTO — the break" : aset.FirstDir + " — at once", () => new List<Mi> { new Mi { Text = "AUTO — the first break of the recent high (BUY) / low (SELL)", Do = () => aset.FirstDir = "AUTO" }, new Mi { Text = "BUY — at once", Fg = Green, Do = () => aset.FirstDir = "BUY" }, new Mi { Text = "SELL — at once", Fg = Red, Do = () => aset.FirstDir = "SELL" } }));
            r2.Children.Add(ADrop("hmode", "ADD-ON WHEN", () => aset.HedgeMode == "SIGNAL" ? "SIGNAL (opposite engulfing)" : aset.HedgeMode == "NONE" ? "NO HEDGE" : aset.HedgeMode == "PRICE" ? "a PRICE move" : aset.HedgeMode == "MONEY" ? "a $ LOSS" : "the BREAK of the recent low / high", () => new List<Mi> { new Mi { Text = "BREAK — the recent low (after a buy) / high (after a sell)", Do = () => aset.HedgeMode = "BREAK" }, new Mi { Text = "PRICE — a fixed move against the last leg (gold 5 = $5, NAS100 50 = 50 points)", Do = () => aset.HedgeMode = "PRICE" }, new Mi { Text = "$ LOSS — a fixed $ lost since the last leg (on what the basket holds)", Do = () => aset.HedgeMode = "MONEY" }, new Mi { Text = "SIGNAL — hedge only on an OPPOSITE engulfing at a 20-candle extreme (the TIMEFRAME's candles) while the newest leg is down the $ LOSS or more: HEDGE × NET the other way, at most MAX HEDGES", Do = () => aset.HedgeMode = "SIGNAL" }, new Mi { Text = "NO HEDGE — never add the other way (target, the max drawdown as an emergency stop, the close time)", Do = () => aset.HedgeMode = "NONE" } }));
            r2.Children.Add(AField("hprice", "PRICE MOVE", aset.HedgePrice.ToString(IC), 60, "ADD-ON WHEN = PRICE: the move against the last leg in price (gold: 5 = $5; NAS100: 50 = 50 points)"));
            r2.Children.Add(AField("hmoney", "$ LOSS", aset.HedgeMoney.ToString(IC), 60, "$ LOSS: the $ lost since the last leg opened • SIGNAL: how much the newest leg must be losing before a hedge"));
            r2.Children.Add(AField("hmult", "HEDGE × NET", aset.HedgeMult.ToString(IC), 50, "SIGNAL: the hedge = this × the net lots open, the other way (2 = 0.1 buy → 0.2 sell)"));
            r2.Children.Add(AField("maxh", "MAX HEDGES", aset.MaxHedges.ToString(IC), 50, "SIGNAL: hedges an iteration at most"));
            r2.Children.Add(ADrop("exit", "TAKE PROFIT", () => aset.ExitMode == "LEGS" ? "each LEG its own +$" : "the whole BASKET +$", () => new List<Mi> { new Mi { Text = "BASKET — everything closes when the basket reaches the target", Do = () => aset.ExitMode = "BASKET" }, new Mi { Text = "EACH LEG — every position closes at its own target × its lots / the entry lots (0.2 lots = 2 × the target)", Do = () => aset.ExitMode = "LEGS" } }));
            r2.Children.Add(ADrop("roll", "BREAK LEVEL", () => aset.Rolling ? "follows new candles" : "fixed at the leg", () => new List<Mi> { new Mi { Text = "FIXED — the recent low / high when the leg opened", Do = () => aset.Rolling = false }, new Mi { Text = "FOLLOWS — the recent low / high of the newest candles", Do = () => aset.Rolling = true } }));
            r2.Children.Add(ADrop("next", "NEXT ITERATION", () => aset.NextStart == "PAUSE" ? "after a PAUSE" : aset.NextStart == "WINNER" ? "after a WIN: same direction at once" : "the next ENTRY SIGNAL", () => new List<Mi> { new Mi { Text = "the very next ENTRY SIGNAL of the setup (a break, an FVG touch …)", Do = () => aset.NextStart = "BREAK" }, new Mi { Text = "after a PAUSE — then the last basket's direction at once", Do = () => aset.NextStart = "PAUSE" }, new Mi { Text = "after a WIN: the SAME DIRECTION again at once (a winning SELL → SELL again); after a loss: the next entry signal", Do = () => aset.NextStart = "WINNER" } }));
            r2.Children.Add(AField("pause", "PAUSE (SECONDS)", aset.PauseSec.ToString(IC), 60, "NEXT ITERATION = PAUSE: seconds of the market's clock (300 = 5 minutes)"));
            r2.Children.Add(ADrop("afterdd", "AFTER MAX DRAWDOWN", () => aset.AfterDrawdown == "NEXT" ? "the NEXT iteration" : "STOP", () => new List<Mi> { new Mi { Text = "STOP the automation", Do = () => aset.AfterDrawdown = "STOP" }, new Mi { Text = "the NEXT iteration", Do = () => aset.AfterDrawdown = "NEXT" } }));
            r2.Children.Add(AField("maxit", "MAX ITERATIONS (0 = ∞)", aset.MaxIterations.ToString(IC), 60, ""));
            r2.Children.Add(AField("comm", "COMMISSION $/LOT", aset.CommissionPerLot.ToString(IC), 60, "round turn, taken off the basket when MT5 does not show it (RAW accounts); 0 = as MT5 shows"));
            aSettings.Children.Add(r2);
            var r3 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            r3.Children.Add(ADrop("emode", "ENTRY SETUP", () => aset.EntryMode == "VWAPREJ" ? "VWAP REJECTION" : aset.EntryMode == "ENGULF" ? "ENGULFING at a 20-candle extreme" : aset.EntryMode == "FVG50" ? "FVG 50% touch" : aset.EntryMode == "FVGBREAK" ? "FVG + BREAK" : aset.EntryMode == "FVG" ? "FVG retest" : aset.EntryMode == "VWAP" ? "VWAP cross" : aset.EntryMode == "FLOW" ? "ORDER FLOW surge" : "BREAK of high / low", () => new List<Mi> {
                new Mi { Text = "ENGULFING — an engulfing candle at the lowest low (BUY) / highest high (SELL) of the last 20 candles of the TIMEFRAME (model A)", Do = () => aset.EntryMode = "ENGULF" },
                new Mi { Text = "VWAP REJECTION — after 6 candles above (under) the day's VWAP, a candle dips to it and closes back above (under) → BUY (SELL) (model C)", Do = () => aset.EntryMode = "VWAPREJ" },
                new Mi { Text = "BREAK ONLY — the break of the swing high (BUY) / swing low (SELL) (or the last N candles: LEVELS)", Do = () => aset.EntryMode = "BREAK" },
                new Mi { Text = "FVG + BREAK — the break of the swing high / low while the latest FVG (its own timeframe) points the same way", Do = () => aset.EntryMode = "FVGBREAK" },
                new Mi { Text = "FVG 50% — the price comes back to the MIDDLE of a fresh fair value gap (any timeframe: FVG TIMEFRAME)", Do = () => aset.EntryMode = "FVG50" },
                new Mi { Text = "FVG — the price comes back into a fresh fair value gap (bullish = BUY, bearish = SELL)", Do = () => aset.EntryMode = "FVG" },
                new Mi { Text = "VWAP — the price crosses the day's VWAP (up = BUY, down = SELL)", Do = () => aset.EntryMode = "VWAP" },
                new Mi { Text = "ORDER FLOW — a tick-volume surge candle closing strong, then its high / low breaks (estimated: MT5 CFDs have no order book)", Do = () => aset.EntryMode = "FLOW" } }));
            r3.Children.Add(ADrop("fvgtf", "FVG TIMEFRAME", () => Tf.Name(aset.FvgTf), () => Tf.All.Where(m => m <= 240).Select(m => new Mi { Text = Tf.Name(m) + (m == 5 ? " (the futures interface's 5-min FVG)" : ""), Do = () => { aset.FvgTf = m; } }).ToList()));
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
            r4.Children.Add(AField("wlots", "WINNER LOTS (BUY SIDE)", aset.WinnerLots.ToString(IC), 60, "the lots of each add when the leading side is BUY (0 = no adds on buys)"));
            r4.Children.Add(AField("wlots2", "WINNER LOTS (SELL SIDE)", aset.WinnerLotsSell.ToString(IC), 60, "the lots of each add when the leading side is SELL (0 = no adds on sells)"));
            r4.Children.Add(AField("wmax", "MAX ADDS / ITERATION (0 = ∞)", aset.MaxWinnerAdds.ToString(IC), 60, ""));
            r4.Children.Add(AField("fvol", "FLOW: VOLUME ×", aset.FlowVol.ToString(IC), 50, "ORDER FLOW: the candle's tick volume at least this × the average of the last 20"));
            r4.Children.Add(AField("fshare", "FLOW: BUYERS / SELLERS ≥", aset.FlowShare.ToString(IC), 50, "ORDER FLOW: the estimated buyers' (or sellers') share, 0.5 – 1"));
            aSettings.Children.Add(r4);
            root.Children.Add(card);

            // the numbers, the views
            aTiles = new ContentControl { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(aTiles, Dock.Top); root.Children.Add(aTiles);
            aViews = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(aViews, Dock.Top);
            foreach (var v in new[] { "STORY", "ITERATIONS", "TRADES", "ANALYSIS", "LEARN", "LOG" }) { var vv = v; var b = Pill(v, Tint(Sx.Card2, 220)); b.Height = 26; b.Click += delegate { aView = vv; AutoPaint(true); aTable.ScrollToHome(); }; aViews.Children.Add(b); }
            aSetBtn = Pill("⚙ SETTINGS", Tint(Sx.Card2, 220)); aSetBtn.Height = 26; aSetBtn.ToolTip = "show / hide the settings — hidden, the story and the iterations get the whole screen"; aSetBtn.Click += delegate { aShowSet = !aShowSet; Put("auto.showset", aShowSet ? "1" : "0"); AutoPaint(true); }; aViews.Children.Add(aSetBtn);
            aGuideBtn = Pill("🎯 CHART GUIDES", Tint(Sx.Card2, 220)); aGuideBtn.Height = 26; aGuideBtn.ToolTip = "draw what the strategy watches on the 📈 CHART (VWAP, the 20-candle high / low, the FVG zone, the hedge box, the next hedge) — a STRONG setup blinks"; aGuideBtn.Click += delegate { aGuidesOn = !aGuidesOn; Put("auto.guides", aGuidesOn ? "1" : "0"); AutoPaint(true); }; aViews.Children.Add(aGuideBtn);
            var clr = Pill("🗑 CLEAR HISTORY", Tint(Sx.Card2, 220)); clr.Height = 26; clr.Click += delegate { AutoClear(); }; aViews.Children.Add(clr);
            root.Children.Add(aViews);
            aTable = new TextBox { IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 13.5, MinHeight = 160, Background = Panel, Foreground = Text, BorderBrush = Line, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, Padding = new Thickness(8) };
            root.Children.Add(aTable);

            // the engine's clock: every tick of the BACKTEST; every 200 ms live (the bridge sends the state every 200 ms)
            if (Sim) sim.OnTickHook = () => auto.Step(sim);
            aTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
            aTimer.Tick += delegate { if (!Sim) { try { auto.Step(broker); } catch (Exception ex) { auto.Stop("error: " + ex.Message); } } AutoSaveNew(); AutoPaint(false); if ((DateTime.Now - aScanAt).TotalSeconds >= 1) { aScanAt = DateTime.Now; AutoSetups(); } };
            aTimer.Start();
            Closed += delegate { aTimer.Stop(); if (auto.Running) auto.Stop("the window closed"); if (Sim) sim.OnTickHook = null; };
            AutoVis(); AutoPaint(true);
            return root;
        }

        FrameworkElement AField(string key, string label, string val, double w, string tip)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 4) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            var tb = Sx.Input(val, w); if (tip != "") tb.ToolTip = tip; af[key] = tb; sp.Children.Add(tb); afBox[key] = sp; return sp;
        }
        FrameworkElement ADrop(string key, string label, Func<string> now, Func<List<Mi>> items)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 4) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
            var b = Pill(now() + "  ▾", Tint(Sx.Card2, 230)); b.Height = 28; b.Tag = now; aDrops[key] = b; afBox[key] = sp;
            b.Click += delegate { if (auto.Running) return; var list = items(); foreach (var m in list) { var act = m.Do; m.Do = () => { act(); b.Content = now() + "  ▾"; AutoSaveSet(); AutoVis(); }; } SxMenu.Show(b, new Point(0, b.ActualHeight + 4), list); };
            sp.Children.Add(b); return sp;
        }
        // a researched model: its settings into the boxes (the drop-downs' labels follow)
        void AutoModel(string name)
        {
            aset = AutoSettings.Model(name, AutoRead());
            Action<string, string> t = (k, v) => { TextBox tb; if (af.TryGetValue(k, out tb)) tb.Text = v; };
            t("look", aset.Lookback.ToString(IC)); t("entry", aset.EntryLots.ToString(IC)); t("add", aset.AddLots.ToString(IC)); t("target", aset.Target.ToString(IC)); t("dd", aset.MaxDrawdown.ToString(IC));
            t("win", aset.Windows); t("eod", aset.CloseAtNy); t("fage", aset.FvgAge.ToString(IC)); t("perday", aset.PerDay.ToString(IC)); t("hmoney", aset.HedgeMoney.ToString(IC)); t("hmult", aset.HedgeMult.ToString(IC)); t("maxh", aset.MaxHedges.ToString(IC)); t("maxit", aset.MaxIterations.ToString(IC));
            foreach (var b in aDrops.Values) { var f = b.Tag as Func<string>; if (f != null) b.Content = f() + "  ▾"; }
        }
        // SETUPS NOW: the three researched setups on the symbol, STRONG / WEAK / WAIT
        void AutoSetups()
        {
            if (aSetups == null) return; var sym = auto.Running ? auto.Set.Symbol : (af.ContainsKey("sym") ? (af["sym"].Text ?? "").Trim() : ""); int off = auto.Running ? auto.Set.NyOffsetHours : (int)ANum("nyoff", -7);
            List<SetupState> list; try { list = SetupScan.Scan(broker, sym, off); } catch { return; }
            aSetups.Children.Clear(); AutoGuidesBuild(sym, list);
            foreach (var x in list)
            {
                var col = x.Level == "STRONG" ? Green : x.Level == "WEAK" ? Amber : Muted;
                var sp = new StackPanel { Width = 355 };
                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(new Border { Background = Tint(col, 60), BorderBrush = col, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 8, 0), Child = new TextBlock { Text = x.Level + (x.Dir > 0 ? " ▲" : x.Dir < 0 ? " ▼" : ""), Foreground = col, FontWeight = FontWeights.Bold, FontSize = 11 } });
                head.Children.Add(new TextBlock { Text = x.Name, Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(head); sp.Children.Add(new TextBlock { Text = x.Text, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
                aSetups.Children.Add(new Border { Child = sp, Background = Sx.CardGrad, BorderBrush = x.Level == "STRONG" ? Green : Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 0) });
            }
        }
        // the chart's 🎯 guides for the chosen strategy (all of them for your own settings), rebuilt every second
        void AutoGuidesBuild(string sym, List<SetupState> list)
        {
            var g = new List<ChartGuide>(); var pre = auto.Running ? auto.Set.Preset : aset.Preset; var s = auto.Running ? auto.Set : aset;
            Func<string, bool> want = k => pre == "" ? (k == "A" && s.EntryMode == "ENGULF") || (k == "B" && s.EntryMode.StartsWith("FVG")) || (k == "C" && s.EntryMode == "VWAPREJ") : pre == k || (pre == "AH" && k == "A");
            Func<string, Brush> col = k => k == "A" ? (pre == "AH" ? Violet : Green) : k == "B" ? Amber : k == "C" ? Cyan : Orange;
            if (!double.IsNaN(SetupScan.LastVwap) && (pre == "" || pre == "C" || s.VwapFilter != "NONE" || s.EntryMode == "VWAPREJ" || s.EntryMode == "VWAP")) g.Add(new ChartGuide { P1 = SetupScan.LastVwap, Color = Gold, Label = "VWAP " + SetupScan.LastVwap.ToString("0.##", IC) + (s.VwapFilter == "DISCOUNT" ? " — buy UNDER / sell OVER" : s.VwapFilter == "FLOW" ? " — buy OVER / sell UNDER" : "") });
            foreach (var st in list) if (want(st.Key)) foreach (var d in st.Draw)
                g.Add(new ChartGuide { Kind = d.Kind, P1 = d.P1, P2 = d.P2, From = d.From, Label = d.Label, Dash = d.Dash, Color = col(st.Key), Blink = st.Level == "STRONG" && (d.Kind != "LINE" || st.Key == "BOX" || st.Key == "B") });
            // the BREAK levels the automation enters on (and hedges on): the swing high / low, or the high / low of the last N candles
            bool inTrade = auto.Running && auto.Current != null;
            if (inTrade)
            {   // in a trade: what happens next and how far away — the take profit, the next hedge, the stop
                double bid = double.NaN; lock (broker.State.Gate) { Mt5Quote q; if (broker.State.Quotes.TryGetValue(sym, out q)) bid = q.Bid; }
                Func<double, string> away = v => double.IsNaN(bid) ? "" : " (" + Math.Abs(v - bid).ToString("0.00", IC) + " away)";
                if (!double.IsNaN(auto.TargetPx)) g.Add(new ChartGuide { P1 = auto.TargetPx, Color = Green, Blink = !double.IsNaN(bid) && Math.Abs(auto.TargetPx - bid) < 0.3, Label = "🎯 TP +$" + s.Target.ToString("0.##", IC) + " @ " + auto.TargetPx.ToString("0.00", IC) + away(auto.TargetPx) });
                if (s.HedgeMode != "NONE" && s.HedgeMode != "SIGNAL" && !double.IsNaN(auto.HedgeLevel))
                {
                    double lossTo = double.IsNaN(bid) ? 0 : Math.Abs(auto.HedgeLevel - bid) * Math.Abs(auto.NetLots) * auto.PerPrice;
                    g.Add(new ChartGuide { P1 = auto.HedgeLevel, Color = Red, Dash = true, Blink = !double.IsNaN(bid) && Math.Abs(auto.HedgeLevel - bid) < 0.3, Label = "🛡 HEDGE " + (auto.LastDir > 0 ? "SELL " : "BUY ") + s.AddLots.ToString("0.##", IC) + " @ " + auto.HedgeLevel.ToString("0.00", IC) + away(auto.HedgeLevel) + (lossTo > 0 ? " ≈ −$" + lossTo.ToString("0", IC) : "") });
                }
                if (!double.IsNaN(auto.StopPx)) g.Add(new ChartGuide { P1 = auto.StopPx, Color = Red, Label = "⛔ STOP −$" + s.MaxDrawdown.ToString("0.##", IC) + " @ " + auto.StopPx.ToString("0.00", IC) + away(auto.StopPx) });
            }
            if (!inTrade && (s.EntryMode == "BREAK" || s.EntryMode == "FVGBREAK" || s.HedgeMode == "BREAK"))
            {
                DateTime ht, lt; double bid; var lv = BreakLevels(sym, s, out ht, out lt, out bid); string what = s.Swing ? "swing" : "last " + s.Lookback + "-candle";
                Func<double, bool> near = v => !double.IsNaN(bid) && Math.Abs(bid - v) <= Math.Max(0.25, Math.Abs(lv[0] - lv[1]) * 0.1);
                bool entry = s.EntryMode == "BREAK" || s.EntryMode == "FVGBREAK";
                if (!double.IsNaN(lv[0])) g.Add(new ChartGuide { P1 = lv[0], From = ht, Color = Green, Dash = !entry, Blink = near(lv[0]) || (!double.IsNaN(bid) && bid > lv[0]), Label = (entry ? "▲ BUY above the " : "hedge BUY above the ") + what + " HIGH " + lv[0].ToString("0.##", IC) + (ht > DateTime.MinValue ? " (" + ht.ToString("HH:mm", IC) + ")" : "") });
                if (!double.IsNaN(lv[1])) g.Add(new ChartGuide { P1 = lv[1], From = lt, Color = Red, Dash = !entry, Blink = near(lv[1]) || (!double.IsNaN(bid) && bid < lv[1]), Label = (entry ? "▼ SELL below the " : "hedge SELL below the ") + what + " LOW " + lv[1].ToString("0.##", IC) + (lt > DateTime.MinValue ? " (" + lt.ToString("HH:mm", IC) + ")" : "") });
            }
            g.RemoveAll(x => double.IsNaN(x.P1)); aGuides = g; aGuideSym = sym;
        }
        // the same levels as the engine (HedgeAuto.Pivot / Lookback): from the finished candles of the strategy's timeframe, newest first
        double[] BreakLevels(string sym, AutoSettings s, out DateTime hiT, out DateTime loT, out double bid)
        {
            hiT = loT = DateTime.MinValue; bid = double.NaN; var f = new List<Mt5Bar>();
            lock (broker.State.Gate)
            {
                var st = broker.State; var now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; Mt5Quote q; if (st.Quotes.TryGetValue(sym, out q)) bid = q.Bid;
                var se = st.SeriesOf(sym, s.Tf); for (int i = se.Bars.Count - 1; i >= 0 && f.Count < Math.Max(60, s.Lookback); i--) if (se.Bars[i].Time.AddMinutes(s.Tf) <= now) f.Add(se.Bars[i]);
            }
            double hi = double.NaN, lo = double.NaN;
            if (s.Swing)
            {
                int k = Math.Max(1, s.SwingK);
                for (int j = k; j + k < f.Count && double.IsNaN(hi); j++) { bool ok = true; for (int d = 1; d <= k && ok; d++) ok = f[j].H > f[j - d].H && f[j].H > f[j + d].H; for (int i = 0; i < j && ok; i++) ok = f[i].H <= f[j].H; if (ok) { hi = f[j].H; hiT = f[j].Time; } }
                for (int j = k; j + k < f.Count && double.IsNaN(lo); j++) { bool ok = true; for (int d = 1; d <= k && ok; d++) ok = f[j].L < f[j - d].L && f[j].L < f[j + d].L; for (int i = 0; i < j && ok; i++) ok = f[i].L >= f[j].L; if (ok) { lo = f[j].L; loT = f[j].Time; } }
            }
            else if (f.Count >= s.Lookback)
            {
                var w = f.Take(s.Lookback).ToList(); var bh = w.OrderByDescending(x => x.H).First(); var bl = w.OrderBy(x => x.L).First(); hi = bh.H; hiT = bh.Time; lo = bl.L; loT = bl.Time;
            }
            return new[] { hi, lo };
        }
        double ANum(string k, double def) { double x; return double.TryParse((af[k].Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, IC, out x) ? x : def; }
        AutoSettings AutoRead()
        {
            var s = aset.Copy(); s.Symbol = (af["sym"].Text ?? "").Trim(); s.Lookback = (int)Math.Max(1, ANum("look", 1)); s.EntryLots = ANum("entry", 0); s.AddLots = ANum("add", 0); s.Target = ANum("target", 0);
            s.MaxDrawdown = Math.Max(0, ANum("dd", 0)); s.HedgePrice = ANum("hprice", 0); s.HedgeMoney = ANum("hmoney", 0); s.HedgeMult = Math.Max(0.1, ANum("hmult", 2)); s.MaxHedges = (int)Math.Max(1, ANum("maxh", 1)); s.PauseSec = (int)Math.Max(0, ANum("pause", 60)); s.MaxIterations = (int)Math.Max(0, ANum("maxit", 0)); s.CommissionPerLot = Math.Max(0, ANum("comm", 0));
            s.Windows = (af["win"].Text ?? "").Trim(); s.CloseAtNy = (af["eod"].Text ?? "").Trim(); s.FvgAge = (int)Math.Max(0, ANum("fage", 0)); s.NyOffsetHours = (int)ANum("nyoff", -7); s.PerDay = (int)Math.Max(0, ANum("perday", 0)); s.NewsBefore = (int)Math.Max(0, ANum("nb", 15)); s.NewsAfter = (int)Math.Max(0, ANum("na", 15));
            s.WinnerLots = Math.Max(0, ANum("wlots", 0.1)); s.WinnerLotsSell = Math.Max(0, ANum("wlots2", 0.1)); s.SwingK = (int)Math.Max(1, ANum("swk", 2)); s.MaxWinnerAdds = (int)Math.Max(0, ANum("wmax", 0)); s.FlowVol = Math.Max(1, ANum("fvol", 1.5)); s.FlowShare = Math.Max(0.5, Math.Min(1, ANum("fshare", 0.7)));
            return s;
        }
        // the automation's settings: per MT5 account on LIVE / PROP (each account its own), per mode otherwise
        string AKey { get { return "auto." + Mode + (!Virtual && live != null && live.Key != "" ? "." + live.Key : ""); } }
        void AutoSaveSet() { aset = AutoRead(); Put(AKey, aset.Save()); }
        // only the boxes the chosen options use: ONE add-on trigger (BREAK / PRICE / $), the pause, the FVG timeframe, the flow numbers, the news minutes, the winner lots
        void AutoVis()
        {
            Action<string, bool> v = (k, on) => { FrameworkElement e; if (afBox.TryGetValue(k, out e)) e.Visibility = on ? Visibility.Visible : Visibility.Collapsed; };
            v("hprice", aset.HedgeMode == "PRICE"); v("hmoney", aset.HedgeMode == "MONEY" || aset.HedgeMode == "SIGNAL"); v("hmult", aset.HedgeMode == "SIGNAL"); v("maxh", aset.HedgeMode == "SIGNAL"); v("roll", aset.HedgeMode == "BREAK");
            v("pause", aset.NextStart == "PAUSE"); v("fvgtf", aset.EntryMode.StartsWith("FVG")); v("fvol", aset.EntryMode == "FLOW"); v("fshare", aset.EntryMode == "FLOW");
            v("nb", aset.NewsMode != "IGNORE"); v("na", aset.NewsMode != "IGNORE"); foreach (var k in new[] { "wlots", "wlots2", "wmax" }) v(k, aset.AddToWinner);
            v("look", !aset.Swing); v("swk", aset.Swing); bool hedge = aset.HedgeMode != "NONE" && aset.HedgeMode != "SIGNAL"; v("add", hedge); v("roll", hedge && aset.HedgeMode == "BREAK"); v("fage", aset.EntryMode.StartsWith("FVG")); v("fstrict", aset.EntryMode.StartsWith("FVG"));
        }

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
            string what = (s.Preset != "" ? "STRATEGY " + (s.Preset == "BOX" ? "HEDGE BOX (swap-free accounts only)" : s.Preset) + "  •  setup " + s.EntryMode + "  •  VWAP filter " + s.VwapFilter + (s.HedgeMode == "NONE" ? "  •  NO HEDGE" : "") + (s.CloseAtNy != "" ? "  •  closes at " + s.CloseAtNy + " NY" : "") + (s.Windows != "" ? "  •  " + s.Windows + " NY" : "") + "\n" : "") + s.Symbol + " " + Tf.Name(s.Tf) + "  •  entry " + s.EntryLots.ToString("0.##", IC) + "  •  add-on " + s.AddLots.ToString("0.##", IC) + " the other way (" + (s.HedgeMode == "PRICE" ? "a " + s.HedgePrice.ToString("0.#####", IC) + " price move" : s.HedgeMode == "MONEY" ? "−$" + s.HedgeMoney.ToString("0.##", IC) : "the break of the recent low / high") + ")\n"
                + "target +$" + s.Target.ToString("0.##", IC) + " per iteration  •  max drawdown " + (s.MaxDrawdown > 0 ? "$" + s.MaxDrawdown.ToString("0.##", IC) + " then " + s.AfterDrawdown : "NONE — until the target or the account blows") + "\n"
                + "first: " + (s.FirstDir == "AUTO" ? "the first break" : s.FirstDir + " at once") + "  •  next: " + (s.NextStart == "PAUSE" ? "after " + s.PauseSec + " s" : "the next break") + (s.MaxIterations > 0 ? "  •  " + s.MaxIterations + " iterations" : "");
            if (Sim || Paper) { if (!Sx.Ask(this, "🤖 START the automation", (Sim ? "On the BACKTEST's simulated account (press ▶ play on the 📈 CHART)." : "On the DEMO account (simulated, live prices).") + "\n\n" + what, "▶ START", "CANCEL", Green)) return; }
            else if (kind == "REAL")
            {
                var typed = Sx.Prompt(this, "⚠ 🤖 START on a REAL account", "This MT5 account (" + login + ") is REAL MONEY. The automation sends orders by itself, with no stop" + (s.MaxDrawdown > 0 ? " but the max drawdown" : "") + ".\n\n" + what + "\n\nType START to go on.", "");
                if (typed == null || typed.Trim().ToUpperInvariant() != "START") return;
            }
            else if (!Sx.Ask(this, "🤖 START on MT5 (" + kind + " account " + login + ")", "Orders go to MT5 by themselves while it runs.\n\n" + what, "▶ START", "CANCEL", Green)) return;
            AutoLoadHistory(); aShowSet = false; Put("auto.showset", "0"); auto.Start(broker, s, adopt); Put("auto.run." + AKey, "1"); aWasOn = true; AutoPaint(true);
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
            if (aHist != null && aHist != "") { foreach (var w in new[] { "iterations", "trades", "story", "log" }) try { File.Delete(AutoFile(w)); } catch { } }
            AutoPaint(true);
        }

        // ---- history on this PC (LIVE / PROP / DEMO, per account): every finished iteration (with its context), its trades, the STORY and the LOG as they happen
        string AutoId() { if (Sim) return ""; var k = live != null ? live.Key : ""; if (k == "") { long lg; lock (broker.State.Gate) lg = broker.State.Account.Login; k = lg > 0 ? lg.ToString(IC) : ""; } foreach (var c in Path.GetInvalidFileNameChars()) k = k.Replace(c, '_'); return k; }
        string AutoFile(string what) { return Path.Combine(Dir, "auto", Mode + "-" + aHist + "-" + what + (what == "story" || what == "log" ? ".txt" : ".csv")); }
        void AutoAppend(string what, string line) { if (string.IsNullOrEmpty(aHist)) AutoLoadHistory(); if (string.IsNullOrEmpty(aHist)) return; try { Directory.CreateDirectory(Path.Combine(Dir, "auto")); File.AppendAllText(AutoFile(what), line + "\n"); } catch { } }
        void AutoLoadHistory()
        {
            var id = AutoId(); if (id == "" || aHist == id) return; aHist = id; if (auto.Iterations.Count > 0) return;
            try
            {
                var f = AutoFile("iterations"); if (File.Exists(f)) foreach (var l in File.ReadAllLines(f).Skip(1)) { var a = l.Split(','); if (a.Length < 16) continue; Func<int, double> d = i => double.Parse(a[i], IC); auto.Iterations.Add(new AutoIteration { N = int.Parse(a[0], IC), Start = new DateTime(long.Parse(a[1], IC)), End = new DateTime(long.Parse(a[2], IC)), FirstDir = a[3], Buys = int.Parse(a[4], IC), Sells = int.Parse(a[5], IC), AddOns = int.Parse(a[6], IC), BuyLots = d(7), SellLots = d(8), MaxGross = d(9), MaxDd = d(10), Result = d(11), Reason = a[12], BalanceStart = d(13), BalanceEnd = d(14), Peak = d(15), WinnerAdds = a.Length > 16 ? int.Parse(a[16], IC) : 0, Ctx = a.Length > 17 ? a[17] : "", Manual = a.Length > 17 && a[17].Contains("manual=yes") }); }
                var t2 = AutoFile("trades"); if (File.Exists(t2)) foreach (var l in File.ReadAllLines(t2).Skip(1)) { var a = l.Split(new[] { ',' }, 8); if (a.Length < 8) continue; auto.Trades.Add(new AutoTrade { Iter = int.Parse(a[0], IC), Time = new DateTime(long.Parse(a[1], IC)), Side = a[2], Lots = double.Parse(a[3], IC), Price = double.Parse(a[4], IC), Level = double.Parse(a[5], IC), Basket = double.Parse(a[6], IC), Why = a[7].Trim('"') }); }
                var s = AutoFile("story"); if (File.Exists(s)) foreach (var l in File.ReadAllLines(s).Reverse().Take(4000).Reverse()) { int sp = l.IndexOf("  "); int n; if (l.StartsWith("#") && sp > 1 && int.TryParse(l.Substring(1, sp - 1), out n)) auto.Story.Add(Tuple.Create(n, l.Substring(sp + 2))); }
                var g = AutoFile("log"); if (File.Exists(g)) auto.Log.AddRange(File.ReadAllLines(g).Reverse().Take(2000).Reverse());
            }
            catch { }
            aSaved = auto.Iterations.Count(x => x.Done); auto.Version++;
        }
        void AutoSaveNew()
        {
            if (aHist == null) AutoLoadHistory(); if (string.IsNullOrEmpty(aHist)) return; var done = auto.Iterations.Where(x => x.Done).ToList(); if (done.Count <= aSaved) return;
            try
            {
                Directory.CreateDirectory(Path.Combine(Dir, "auto")); var f = AutoFile("iterations"); var t = AutoFile("trades");
                if (!File.Exists(f)) File.WriteAllText(f, "n,start,end,first,buys,sells,addons,buylots,selllots,maxgross,maxdd,result,reason,balstart,balend,peak,winneradds,context\n");
                if (!File.Exists(t)) File.WriteAllText(t, "iteration,time,side,lots,price,level,basket,why\n");
                foreach (var x in done.Skip(aSaved))
                {
                    File.AppendAllText(f, string.Join(",", x.N.ToString(IC), x.Start.Ticks.ToString(IC), x.End.Ticks.ToString(IC), x.FirstDir, x.Buys.ToString(IC), x.Sells.ToString(IC), x.AddOns.ToString(IC), x.BuyLots.ToString("R", IC), x.SellLots.ToString("R", IC), x.MaxGross.ToString("R", IC), x.MaxDd.ToString("R", IC), x.Result.ToString("R", IC), x.Reason, x.BalanceStart.ToString("R", IC), x.BalanceEnd.ToString("R", IC), x.Peak.ToString("R", IC), x.WinnerAdds.ToString(IC), (x.Ctx ?? "").Replace(",", " ")) + "\n");
                    File.AppendAllLines(t, auto.Trades.Where(tr => tr.Iter == x.N && tr.Time >= x.Start).Select(tr => string.Join(",", tr.Iter.ToString(IC), tr.Time.Ticks.ToString(IC), tr.Side, tr.Lots.ToString("R", IC), tr.Price.ToString("R", IC), (double.IsNaN(tr.Level) ? 0 : tr.Level).ToString("R", IC), tr.Basket.ToString("R", IC), "\"" + tr.Why.Replace("\"", "'").Replace(",", ";") + "\"")));
                }
                aSaved = done.Count;
            }
            catch { }
        }
        // the arrows on the chart: each entry / hedge / add (▲ buy, ▼ sell) and each close (● with its result)
        List<Tuple<DateTime, double, string, string>> AutoMarks(string sym)
        {
            var res = new List<Tuple<DateTime, double, string, string>>(); var s = auto.Running ? auto.Set : aset; if (!string.Equals(s.Symbol, sym, StringComparison.OrdinalIgnoreCase)) return res;
            foreach (var tr in auto.Trades.Skip(Math.Max(0, auto.Trades.Count - 400))) res.Add(Tuple.Create(tr.Time, tr.Price, tr.Side, (tr.Why.StartsWith("ENTRY") ? "#" + tr.Iter + " " : tr.Why.StartsWith("ADD TO") ? "+W " : "H ") + tr.Lots.ToString("0.##", IC)));
            foreach (var it in auto.Iterations.Where(x => x.Done && x.CloseBid > 0).Skip(Math.Max(0, auto.Iterations.Count - 200))) res.Add(Tuple.Create(it.End, it.CloseBid, "CLOSE", (it.Result >= 0 ? "+" : "−") + "$" + Math.Abs(it.Result).ToString("0", IC)));
            return res;
        }

        // ---- the screen
        void AutoPaint(bool force)
        {
            if (aPhase == null) return; bool on = auto.Running; var s = on ? auto.Set : aset; var it = auto.Current;
            if (aHist == null && !Sim) AutoLoadHistory();
            if (aWasOn && !on && auto.StopReason != "the window closed") Put("auto.run." + AKey, "0"); aWasOn = on;
            if (!on) aStart.Content = Get("auto.run." + AKey, "0") == "1" ? "⟳  RESUME (it was running)" : "▶  START";
            aPhase.Text = "🤖 AUTO — " + (on ? auto.Phase : "OFF") + (on ? "  •  " + s.Symbol + " " + Tf.Name(s.Tf) : ""); aPhase.Foreground = on ? Green : Muted;
            aCard.BorderBrush = on ? Green : Line;
            aBasket.Text = it != null ? Signed(auto.Basket, "USD") + "  /  +$" + s.Target.ToString("0.##", IC) : "—"; aBasket.Foreground = it == null ? Muted : auto.Basket >= 0 ? Green : Red;
            aInfo.Text = on ? (it != null ? "iteration #" + it.N + " since " + it.Start.ToString("dd MMM HH:mm:ss", IC) + "  •  " + it.Buys + " buys (" + it.BuyLots.ToString("0.##", IC) + ") / " + it.Sells + " sells (" + it.SellLots.ToString("0.##", IC) + ")  •  " + it.AddOns + " add-ons  •  max drawdown " + it.MaxDd.ToString("0.00", IC) + (s.MaxDrawdown > 0 ? " (limit −$" + s.MaxDrawdown.ToString("0.##", IC) + ")" : " (no limit)")
                    : auto.Phase == "PAUSE" ? "pause until " + auto.PauseUntil.ToString("HH:mm:ss", IC) + " (" + Math.Max(0, (auto.PauseUntil - auto.Now).TotalSeconds).ToString("0", IC) + " s)" : auto.Phase == "FIRST" ? "the first " + s.FirstDir + " goes now" : "waiting for the next " + auto.WaitingFor() + (auto.Blocked != "" ? "  •  NOT NOW: " + auto.Blocked : ""))
                : (auto.StopReason != "" ? "stopped: " + auto.StopReason + "  •  " : "") + (Sim ? "BACKTEST: press START here, then ▶ play on the 📈 CHART." : Paper ? "DEMO account: simulated on live prices." : "MT5 account: needs 🔓 ARM (chart panel) and MT5's Algo Trading.");
            aInfo.Foreground = !on && auto.StopReason != "" ? Amber : Muted;
            aLevels.Text = on ? (s.HedgeMode == "NONE" || s.HedgeMode == "SIGNAL" ? "" : "hedge levels: recent high " + Px(auto.BuyLevel) + "  •  recent low " + Px(auto.SellLevel)) + (!double.IsNaN(auto.Vwap) ? "  •  VWAP " + Px(auto.Vwap) : "") + (it != null ? "  •  next add-on (" + (s.HedgeMode == "PRICE" ? "price move" : s.HedgeMode == "MONEY" ? "$ loss" : "break") + ") at " + Px(auto.HedgeLevel) : "") : "";
            if (aGuideBtn != null) aGuideBtn.Content = aGuidesOn ? "🎯 CHART GUIDES: ON" : "🎯 CHART GUIDES: OFF";
            if (aSetCard != null) { aSetCard.Visibility = aShowSet ? Visibility.Visible : Visibility.Collapsed; aSetBtn.Content = aShowSet ? "⚙ HIDE SETTINGS ▴" : "⚙ SHOW SETTINGS ▾"; }
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
                foreach (var x in auto.Iterations.AsEnumerable().Reverse()) L.Add(string.Format(IC, "{0,4}  {1,-10} {2,-8} {3,-8} {4,-5} {5,5} {6,5} {7,7} {8,9:0.##} {9,9:0.##} {10,9:0.##} {11,11:N2} {12,11}  {13}", x.N, x.Start.ToString("dd MMM yy", IC), x.Start.ToString("HH:mm:ss", IC), x.Done ? x.End.ToString("HH:mm:ss", IC) : "open", x.FirstDir, x.Buys, x.Sells, x.AddOns, x.BuyLots, x.SellLots, x.MaxGross, x.MaxDd, x.Done ? x.Result.ToString("+#,0.00;−#,0.00;0.00", IC) : "(" + auto.Basket.ToString("+0.00;−0.00", IC) + ")", (x.Manual ? "✋ " : "") + x.Reason + (x.WinnerAdds > 0 ? " • " + x.WinnerAdds + " added to the winner" : "") + (x.ManualAdds > 0 ? " • " + x.ManualAdds + " added by hand" : "") + (x.Manual ? " (your trade)" : x.Adopted ? " (taken over)" : "")));
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
            else if (aView == "LEARN")
            {
                // every finished iteration's context at its start → which conditions win, which lose (the strongest confirmations first)
                var d = auto.Iterations.Where(x => x.Done && !string.IsNullOrEmpty(x.Ctx)).ToList();
                L.Add("LEARN — " + d.Count + " finished iterations with their context (what the market looked like when each one started). n = how many • WIN% • NET • the average worst drawdown • the average hedges");
                if (d.Count == 0) L.Add("\nnothing yet — every new iteration is saved with its context");
                var rows = new List<Tuple<string, int, int, double, double, double>>();
                var keys = d.SelectMany(x => x.Ctx.Split(';').Select(kv => kv.Split('=')[0])).Where(k => k != "range" && k != "spread" && k != "toOtherSide").Distinct().ToList();
                foreach (var k in keys)
                {
                    L.Add(""); L.Add(k.ToUpperInvariant());
                    foreach (var g in d.GroupBy(x => { var kv = x.Ctx.Split(';').FirstOrDefault(z => z.StartsWith(k + "=")); return kv == null ? "?" : kv.Substring(k.Length + 1); }).OrderByDescending(g => g.Count()))
                    {
                        int n = g.Count(), w = g.Count(x => x.Won); double net = g.Sum(x => x.Result), dd = g.Average(x => x.MaxDd), h = g.Average(x => (double)x.AddOns);
                        rows.Add(Tuple.Create(k + " = " + g.Key, n, w, net, dd, h));
                        L.Add(string.Format(IC, "   {0,-12} n {1,4}   win {2,5:0}%   net {3,11}   avg worst DD {4,9:N2}   avg hedges {5,4:0.0}", g.Key, n, 100.0 * w / n, Signed(net, "USD"), dd, h));
                    }
                }
                var strong = rows.Where(r => r.Item2 >= 3).OrderByDescending(r => (double)r.Item3 / r.Item2).ThenByDescending(r => r.Item5).Take(8).ToList();
                if (strong.Count > 0) { L.Add(""); L.Add("STRONGEST CONFIRMATIONS (n ≥ 3, the best win rate, the smallest drawdown)"); foreach (var r in strong) L.Add(string.Format(IC, "   ✔ {0,-26} win {1,5:0}% of {2,3}   avg worst DD {3,9:N2}   avg hedges {4,4:0.0}", r.Item1, 100.0 * r.Item3 / r.Item2, r.Item2, r.Item5, r.Item6)); }
                var weak = rows.Where(r => r.Item2 >= 3).OrderBy(r => (double)r.Item3 / r.Item2).ThenBy(r => r.Item5).Take(6).ToList();
                if (weak.Count > 0) { L.Add(""); L.Add("WHAT LOSES / DRAWS DOWN MOST (avoid or filter)"); foreach (var r in weak) L.Add(string.Format(IC, "   ✖ {0,-26} win {1,5:0}% of {2,3}   avg worst DD {3,9:N2}   avg hedges {4,4:0.0}", r.Item1, 100.0 * r.Item3 / r.Item2, r.Item2, r.Item5, r.Item6)); }
                L.Add(""); L.Add("Files (send them to Claude to build an indicator from them): " + (string.IsNullOrEmpty(aHist) ? "—" : AutoFile("iterations") + "  •  " + AutoFile("story")));
            }
            else if (aView == "STORY")
            {
                L.Add("WHAT HAPPENED AND WHY — the newest iteration first, each one from its entry to its close (the candle, the level, the prices, the $)");
                foreach (var g in auto.Story.GroupBy(x => x.Item1).OrderByDescending(g => g.Key)) { L.Add(""); L.Add(g.Key > 0 ? "──── ITERATION #" + g.Key + " ────" : "──── waiting ────"); foreach (var x in g) L.Add(x.Item2); }
                if (auto.Story.Count == 0) L.Add("\nnothing yet — START the automation");
            }
            else L.AddRange(auto.Log.AsEnumerable().Reverse());
            aTable.TextWrapping = aView == "STORY" || aView == "LOG" ? TextWrapping.Wrap : TextWrapping.NoWrap;
            var txt = string.Join("\n", L); if (aTable.Text != txt) { double vo = aTable.VerticalOffset, ho = aTable.HorizontalOffset; aTable.Text = txt; aTable.ScrollToVerticalOffset(vo); aTable.ScrollToHorizontalOffset(ho); }   // new lines never jump you back to the top
        }
    }
}
