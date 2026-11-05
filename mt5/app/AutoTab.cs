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
        AutoSettings aset; TextBlock aPhase, aBasket, aInfo, aLevels; WrapPanel aSetups; DateTime aScanAt; Border aSetCard, aLock; ScrollViewer aSetScroll; Button aSetBtn, aGuideBtn; bool aShowSet = true, aGuidesOn = true; List<ChartGuide> aGuides = new List<ChartGuide>(); string aGuideSym = ""; Border aCard; Button aStart, aStop, aStopClose, aManual; ContentControl aTiles; TextBox aTable; StackPanel aViews, aSettings, aRows; ScrollViewer aRowsView;
        readonly Dictionary<string, Button> aDrops = new Dictionary<string, Button>();
        bool aProAsked;
        bool aResumePending, aPctChecked; DateTime aArchAt = DateTime.MinValue; DateTime aResumeAt; CheckBox aRollCb; Button aSaveBtn, aUndoBtn, aStopSmall, aNowBtn; TextBlock aLockTxt; string aView = "⚙ SETTINGS"; int aSeen = -1, aSaved; string aHist = null; bool aWasOn; DispatcherTimer aTimer; readonly Dictionary<string, FrameworkElement> afBox = new Dictionary<string, FrameworkElement>();

        FrameworkElement BuildAuto()
        {
            aset = AutoSettings.Load(Get(AKey, Get("auto." + Mode, "")));
            // ★ the tested defaults (user's request, once per window): the boxes open with ★ SAFE HEDGE's values — every box can still be changed
            // (a window whose automation was running keeps the settings it ran with)
            // 🛡 the new default (user's request): DEFENDER once per window, sized from the balance (a running automation keeps its settings — pick it in STRATEGY)
            if (Get("auto.def05." + AKey, "") != "1" && !AutoWasRunning()) { var sym1 = aset.Symbol; aset = AutoSettings.Model("DEFENDER", aset, AutoBal()); if (sym1 != "") aset.Symbol = sym1; Put(AKey, aset.Save()); Put("auto.def05." + AKey, "1"); Put("auto.safe04." + AKey, "1"); }
            Loaded += delegate { Dispatcher.BeginInvoke(new Action(AutoWhatsNew), System.Windows.Threading.DispatcherPriority.ApplicationIdle); };
            if (Get("auto.safe04." + AKey, "") != "1" && !AutoWasRunning()) { var sym0 = aset.Symbol; aset = AutoSettings.Model("SAFE", aset); if (sym0 != "") aset.Symbol = sym0; Put(AKey, aset.Save()); Put("auto.safe04." + AKey, "1"); }   // an older build kept one DEMO key for every broker: it is the fallback if (aset.Symbol == "" || aset.Symbol == "XAUUSD") { try { if (panes.Count > 0 && !string.IsNullOrEmpty(Act.Sym)) aset.Symbol = Act.Sym; } catch { } }
            auto.Send = AutoSend; auto.NewsNear = AutoNews; auto.OnSignalDone = AutoSignalSave;
            auto.OnTell = (n, l) => AutoAppend("story", "#" + n + "  " + l);
            auto.OnPath = line => AutoAppend("paths", line);
            auto.OnOpenState = line => { try { if (string.IsNullOrEmpty(aHist)) return; var f = AutoFile("open"); if (line == "") { if (File.Exists(f)) File.Delete(f); } else { Directory.CreateDirectory(Path.GetDirectoryName(f)); File.WriteAllText(f, line); } } catch { } };
            auto.RestoreOpen = () => { try { if (string.IsNullOrEmpty(aHist)) AutoLoadHistory(); var f = string.IsNullOrEmpty(aHist) ? null : AutoFile("open"); return f != null && File.Exists(f) ? HedgeAuto.ParseOpen(File.ReadAllText(f)) : null; } catch { return null; } };
            auto.OnAfterDone = ia => AutoAppend("after", string.Join(",", ia.N.ToString(IC), ia.Start.Ticks.ToString(IC), ia.AfterMax.ToString("0.##", IC), ia.AfterMaxMin.ToString("0.#", IC), ia.AfterBackTargetMin.ToString("0.#", IC), ia.AfterBreakevenMin.ToString("0.#", IC), ia.AfterMove.ToString("0.###", IC)));
            auto.OnHedgeDone = he => AutoAppend("hedges", string.Join(",", he.Iter.ToString(IC), he.Time.Ticks.ToString(IC), he.Kind, he.Dir > 0 ? "BUY" : "SELL", he.Lots.ToString("0.##", IC), he.Price.ToString("R", IC), he.RangeLo.ToString("R", IC), he.RangeHi.ToString("R", IC), he.Atr.ToString("0.###", IC), he.Before.ToString(IC), he.Basket.ToString("0.00", IC), he.Best.ToString("0.###", IC), he.Worst.ToString("0.###", IC), he.Whipsaw ? "1" : "0", (he.Ctx ?? "").Replace(",", " "))); auto.OnNote = (n, l) => AutoAppend("log", l);
            aGuidesOn = Get("auto.guides", "1") == "1"; foreach (var pn in panes) { var pp = pn; pp.V.Marks = () => aGuidesOn || auto.Running ? AutoMarks(pp.Sym) : null; pp.V.Guides = () => !string.Equals(pp.Sym, aGuideSym, StringComparison.OrdinalIgnoreCase) ? null : aGuidesOn ? aGuides : aGuides.Where(x => x.Core).ToList();
                pp.V.GuideMoved = (id, px) => { if (id == "HEDGE" && auto.Running && auto.Current != null) { auto.SetManualHedge(px); Toast("✋ HEDGE moved to " + px.ToString("0.00", IC) + " — the automation hedges there (until the next leg)"); AutoSetups(); } };
                pp.V.GuideMenu = (id, at, px) => { if (id != "HEDGE" || !auto.Running || auto.Current == null) return; var v0 = pp.V; SxMenu.Show(v0, at, new List<Mi> {
                    Mi.H("🛡 THE HEDGE • " + (double.IsNaN(auto.HedgeLevel) ? "—" : auto.HedgeLevel.ToString("0.00", IC)) + (double.IsNaN(auto.ManualHedge) ? " (the automation's)" : " (yours)")),
                    new Mi("⇕", "MOVE IT HERE • " + px.ToString("0.00", IC), () => { auto.SetManualHedge(px); AutoSetups(); }),
                    new Mi("✎", "TYPE A PRICE …", () => { var t0 = Sx.Prompt(this, "🛡 HEDGE PRICE", "The automation hedges at this price (until the next leg fills):", (double.IsNaN(auto.HedgeLevel) ? px : auto.HedgeLevel).ToString("0.00", IC)); double v; if (t0 != null && double.TryParse(t0.Trim().Replace(',', '.'), NumberStyles.Float, IC, out v) && v > 0) { auto.SetManualHedge(v); AutoSetups(); } }),
                    new Mi("🚫", "REMOVE THE HEDGE (no hedge for this leg)", () => { auto.RemoveHedge(); AutoSetups(); }),
                    new Mi("↺", "BACK TO AUTO", () => { auto.AutoHedgeAgain(); AutoSetups(); }) }); }; /* SIGNALS OFF hides the setup drawings, never the automation's own levels / arrows / banner */ }
            var root = new DockPanel { LastChildFill = true };

            // the live card + the buttons
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(top, Dock.Top);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(btns, Dock.Right);
            aStart = Pill("▶  START", Tint(Green, 90)); aStart.Height = 40; aStart.FontSize = 14; aStart.Click += delegate { AutoStart(); }; btns.Children.Add(aStart);
            aStop = Pill("■  STOP (positions stay)", Tint(Amber, 70)); aStop.Height = 40; aStop.ToolTip = "the automation stops; the open positions stay as they are"; aStop.Click += delegate { auto.Stop("stopped by you"); AutoPaint(true); }; btns.Children.Add(aStop);
            aStopClose = Pill("⛔  STOP + CLOSE ALL", Tint(Red, 170)); aStopClose.Height = 40; aStopClose.Foreground = Sx.White; aStopClose.Click += delegate { AutoStopClose(); }; btns.Children.Add(aStopClose);
            // ✋ MANUAL MANAGE (user's request): AUTO keeps watching and planning but only SUGGESTS (nothing sent but the ⛔ hard stops)
            aManual = Pill("✋  MANUAL MANAGE: OFF", Tint(Violet, 50)); aManual.Height = 40; aManual.Margin = new Thickness(0, 0, 8, 0);
            aManual.ToolTip = "ON = you manage the positions: AUTO sends nothing by itself (only the ⛔ hard stops) — it shows 💡 suggestions on the chart and in the story; your trades are adopted. OFF = AUTO sends its orders again.";
            aManual.Click += delegate { AutoManualToggle(); };
            btns.Children.Insert(0, aManual);
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
            // ✏ the SETTINGS bar (user's request): change anything while it runs → 💾 SAVE = the NEXT iteration uses it (the open one keeps its own);
            // stopped → ▶ START / RESUME right here. Big and coloured, always at the top of the settings.
            aLock = new Border { Background = Tint(Green, 40), BorderBrush = Green, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Visible };
            var lockRow = new DockPanel();
            aSaveBtn = Pill("💾  SAVE — FROM THE NEXT ITERATION", Tint(Green, 200)); aSaveBtn.Height = 40; aSaveBtn.FontSize = 15; aSaveBtn.Foreground = Bg; aSaveBtn.Margin = new Thickness(10, 0, 0, 0);
            aNowBtn = Pill("⚡  APPLY NOW (also the open basket)", Tint(Amber, 200)); aNowBtn.Height = 40; aNowBtn.FontSize = 13; aNowBtn.Foreground = Bg; aNowBtn.Margin = new Thickness(10, 0, 0, 0);
            aNowBtn.Click += delegate
            {
                if (!auto.Running) return; AutoSaveSet(); var ns = aset.Copy(); ns.Symbol = auto.Set.Symbol; var err = auto.Check(broker, ns); if (err != "") { Sx.Info(this, "🤖 AUTO", err, Red); return; }
                if (auto.Current != null && !Sx.Ask(this, "⚡ Apply now", "The new settings take over AT ONCE — also for the open iteration #" + auto.Current.N + " (" + auto.Current.Buys + " buys / " + auto.Current.Sells + " sells). Its positions stay; from now on its hedges, its exit and its target follow the new settings.", "⚡ APPLY NOW", "CANCEL", Amber)) return;
                auto.ApplyNow(ns); Toast("⚡ applied — the open basket follows the new settings from now on"); AutoPaint(true);
            };
            aUndoBtn = Pill("↺  UNDO CHANGES", Card); aUndoBtn.Height = 40; aUndoBtn.FontSize = 13; aUndoBtn.Margin = new Thickness(10, 0, 0, 0);
            var stopSmall = Pill("■ STOP", Tint(Amber, 140)); stopSmall.Height = 40; stopSmall.Margin = new Thickness(10, 0, 0, 0);
            aSaveBtn.Click += delegate
            {
                if (!auto.Running) { AutoStart(); return; }   // stopped: the same button = ▶ START / RESUME with these settings
                AutoSaveSet(); var ns = aset.Copy(); ns.Symbol = auto.Set.Symbol; var err = auto.Check(broker, ns); if (err != "") { Sx.Info(this, "🤖 AUTO", err, Red); return; }
                auto.Queue(ns); Toast(auto.Current != null ? "💾 saved — iteration #" + auto.Current.N + " keeps its settings, the NEXT one uses the new ones" : "💾 saved — in use now (no iteration open)"); AutoPaint(true);
            };
            aUndoBtn.Click += delegate { if (!auto.Running) return; aset = (auto.Next ?? auto.Set).Copy(); AutoModelBoxes(); AutoSaveSet(); AutoPaint(true); Toast("↺ the boxes show the settings in use again"); };
            stopSmall.Click += delegate { if (!auto.Running) return; if (!Sx.Ask(this, "■ STOP the automation", "STOP now? The open positions stay as they are; ▶ START / RESUME takes them over again.", "■ STOP", "KEEP RUNNING", Amber)) return; auto.Stop("stopped by you"); AutoPaint(true); };
            aStopSmall = stopSmall;
            DockPanel.SetDock(stopSmall, Dock.Right); DockPanel.SetDock(aUndoBtn, Dock.Right); DockPanel.SetDock(aNowBtn, Dock.Right); DockPanel.SetDock(aSaveBtn, Dock.Right);
            lockRow.Children.Add(stopSmall); lockRow.Children.Add(aUndoBtn); lockRow.Children.Add(aNowBtn); lockRow.Children.Add(aSaveBtn);
            aLockTxt = new TextBlock { Text = "", Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }; lockRow.Children.Add(aLockTxt);
            aLock.Child = lockRow; aSettings.Children.Add(aLock);
            var r0 = new WrapPanel();
            r0.Children.Add(ADrop("preset", "STRATEGY (FROM THE RESEARCH)", () => aset.Preset == "DEFENDER" ? "🛡 DEFENDER (default — capital first)" : aset.Preset == "PRO" ? "★ SAFE HEDGE PRO (+ recovery)" : aset.Preset == "LADDER" ? "🪜 HM LADDER (draft, DEMO)" : aset.Preset == "SAFE" ? "★ SAFE HEDGE (tested)" : aset.Preset == "AH" ? "A + SIGNAL HEDGE" : aset.Preset == "A" ? "A · London engulfing — lower risk" : aset.Preset == "B" ? "B · NY FVG 50% — no stop" : aset.Preset == "C" ? "C · all day VWAP rejection" : aset.Preset == "D" ? "D · M1 aggression break" : aset.Preset == "F" ? "F · FVG 50% + FVG hedge" : aset.Preset == "BOX" ? "HEDGE BOX · consolidation flip (swap-free only)" : "your own settings", () => new List<Mi> {
                new Mi { Text = "★ MY DEFAULT — the settings you saved yourself" + (Get("auto.mydefault", "") == "" ? " (none saved yet: set the boxes, then 💾 SAVE AS MY DEFAULT below)" : ""), Fg = Gold, Do = () => { var md = Get("auto.mydefault", ""); if (md == "") { Toast("★ no default saved yet — set the boxes, then 💾 SAVE AS MY DEFAULT"); return; } aset = AutoSettings.Load(md); AutoModelBoxes(); } },
                new Mi { Text = "💾 SAVE THESE SETTINGS AS ★ MY DEFAULT (every box as it is now)", Do = () => { Put("auto.mydefault", AutoRead().Save()); Toast("💾 saved as ★ MY DEFAULT — pick it in STRATEGY any time"); } },
                new Mi { Text = "✔ TURN ON ALL THE PROTECTIONS — keep MY entry, size and target; add 🛡 RISK GUARD + 🚨 DANGER EXIT + 📉 SCALE OUT + range-only hedge + 🛟 rescue + ⛔ hard stops (from the next iteration while it runs)", Fg = Green, Do = () => AutoProtectAll() },
                new Mi { Text = "🛡 DEFENDER (THE DEFAULT) — capital first, small profits, a calm hedging flow: your break entry + a FIXED-lead hedge only beyond the basket's range (never a growing step), 0.01 lot per $1,000 (0.05 on $5k), +$10 per $5,000 • 🛡 RISK GUARD: too big (0.6 lot per $5,000) / margin under 500 % / 3 hedges in an hour → LOCK both sides even instead of hedging, unlock only on an obvious trend, 🛟 pair-off • 📉 scale out on the way • 🛟 rescue after 3 hedges • 🚨 DANGER METER on the chart (loss vs balance, margin, chop, time, how similar baskets ended) → at 60+ it takes a SMALLER profit (25 % of the target) • ⛔ hard stops 60 % / margin 60 %. Sized from your balance when picked", Fg = Cyan, Do = () => AutoModel("DEFENDER") },
                new Mi { Text = "★ SAFE HEDGE PRO (TEST ON DEMO) — YOUR setup at the size that survived the 5-month replay (0.02 lot, STEP LEAD 0.02, +$10, 📉 scale out 0.01) + 🧭 trend-aware hedge + 🛡 RISK GUARD + 🛟 DANGER RECOVERY + ⛔ HARD STOPS (−60 % / margin 60 %). Replayed June–October on $5k: −6 %, no blow-up, one basket stuck for months — at 0.05 lot or more the same setup was wiped out. Every box can be changed", Fg = Gold, Do = () => AutoModel("PRO") },
                new Mi { Text = "🪜 HM LADDER (DRAFT, DEMO / BACKTEST only) — decoded from the Hedging Mastermind's 3 Nov stream: a straddle to start (buy stop + sell stop), then a BUY only above the basket's highest leg + 4 and a SELL only below its lowest leg − 4, every leg 0.05 lot (his size on ≈ $5k), max 10 legs, the basket closes at +$15. Replayed on his day (3 Nov, $5k, standard costs): +$455, 26 baskets, all won (step 2.5 got stuck); over 12 months it was thin and can get stuck — watch it next to his streams", Fg = Amber, Do = () => AutoModel("LADDER") },
                new Mi { Text = "★ SAFE HEDGE — YOUR break strategy + hedge only beyond the basket's range + 🛟 rescue exit after 3 hedges, 0.01 lot, lead 0.01, +$2 (per $5,000 — scale it with the balance). 5 months of 1-minute gold replayed: 257 closed, none at a loss, +11 %, the deepest basket ≈ −8 % of the balance. Every box can be changed after picking it", Fg = Green, Do = () => AutoModel("SAFE") },
                new Mi { Text = "A · LONDON ENGULFING — M5 engulfing at a 20-candle low / high, buy under VWAP / sell over, 03:00-06:00 NY, +$20, emergency stop −$300, close 16:55 NY (2023-26: profitable every year, worst drawdown $648 on $5k)", Fg = Green, Do = () => AutoModel("A") },
                new Mi { Text = "A + SIGNAL HEDGE — model A, and while it is losing $5+ an OPPOSITE M5 engulfing at a 20-candle extreme opens 2× the net the other way (once); each leg closes at its own +$20 per 0.1 lot; basket stop −$400; close 16:55 NY (2023-26: +$1,770 / +$1,612 / +$2,391 vs +$1,465 / +$1,458 / +$2,217 without the hedge)", Fg = Violet, Do = () => AutoModel("AH") },
                new Mi { Text = "B · NY FVG 50% — M5 strong fair value gap, entry at its middle within 12 min, buy over VWAP / sell under, 08:30-11:30 NY, +$20, NO stop (held until +$20; floating drawdown reached $3,130)", Fg = Amber, Do = () => AutoModel("B") },
                new Mi { Text = "C · ALL DAY VWAP REJECTION — M15: 6 candles above (under) the day's VWAP, then a dip to it closing back above (under) → BUY (SELL); 03:00-15:30 NY, +$20, emergency stop −$200, close 16:55 NY (2023-26: profitable every period, worst drawdown $1,054, ~0.7 trades a day)", Fg = Cyan, Do = () => AutoModel("C") },
                new Mi { Text = "D · M1 AGGRESSION BREAK (high frequency) — 4+ one-colour M1 candles, the first opposite close, then the break of its high / low; buy over VWAP / sell under; 03:00-15:30 NY, +$20, stop −$300, close 16:55 NY (2023-26 at Duramarkets' cost: +$4,045 / +$2,320 / +$2,423, 88% won, ~5 trades a day, worst drawdown $2,908)", Fg = Green, Do = () => AutoModel("D") },
                new Mi { Text = "F · FVG 50% + FVG HEDGE — the entry at the 50% of the fair value gap (stacked gaps in a row = the 50% of all of them), the hedge at the LOW (buys) / HIGH (sells) of the gap's first candle, +$20 (not in the research yet — test it on DEMO)", Fg = Amber, Do = () => AutoModel("F") },
                new Mi { Text = "HEDGE BOX — the 96-hour H1 consolidation: hedge (flip 0.1 → 0.2 the other way) only at a break of its high / low, nothing inside, basket +$150 — ONLY on a swap-free account (deep floating losses, −$11k per 0.1 lot seen)", Fg = Red, Do = () => AutoModel("BOX") },
                new Mi { Text = "YOUR OWN — keep the settings below as you set them", Do = () => aset.Preset = "" } }));
            r0.Children.Add(ADrop("vf", "VWAP FILTER", () => aset.VwapFilter == "DISCOUNT" ? "buy UNDER / sell OVER" : aset.VwapFilter == "FLOW" ? "buy OVER / sell UNDER" : "none", () => new List<Mi> {
                new Mi { Text = "NONE — every signal", Do = () => aset.VwapFilter = "NONE" }, new Mi { Text = "DISCOUNT — buy only under the day's VWAP, sell only over it (reversals: A)", Do = () => aset.VwapFilter = "DISCOUNT" },
                new Mi { Text = "FLOW — buy only over the VWAP, sell only under it (with the flow: B)", Do = () => aset.VwapFilter = "FLOW" } }));
            r0.Children.Add(AField("eod", "NO NEW TRADES AFTER (NY)", aset.CloseAtNy, 70, "no new iteration starts after this New York time until the next day (16:55 = before the daily break); a basket in profit closes then; empty = trade all the time"));
            r0.Children.Add(ADrop("eodl", "AT THAT TIME, IN A LOSS", () => aset.EodLoss == "CLOSE" ? "CLOSE anyway" : "HOLD OPEN (hedging)", () => new List<Mi> {
                new Mi { Text = "HOLD OPEN (default) — a hedge never closes at a loss: the basket stays open, hedged and managed until its target", Fg = Green, Do = () => aset.EodLoss = "HOLD" },
                new Mi { Text = "CLOSE anyway — everything closes at that time, even at a loss", Fg = Amber, Do = () => aset.EodLoss = "CLOSE" } }));
            r0.Children.Add(ADrop("man", "MANUAL TRADES", () => aset.TrackManual ? "TRACK + MANAGE ✋" : "ignore", () => new List<Mi> {
                new Mi { Text = "TRACK + MANAGE — a trade you open by hand on the symbol becomes an iteration (✋): the automation takes it to the target / stop / close time and records it", Do = () => aset.TrackManual = true },
                new Mi { Text = "IGNORE — wait until your trades are closed (the old way)", Do = () => aset.TrackManual = false } }));
            r0.Children.Add(AField("aggrun", "AGGRESSION CANDLES", aset.AggRun.ToString(IC), 50, "AGGRESSION BREAK: at least this many candles of one colour before the first opposite close (the research: 4 on M1, 5 on M5 / M15)"));
            r0.Children.Add(AField("fage", "FVG GOOD FOR (MIN, 0 = 60 CANDLES)", aset.FvgAge.ToString(IC), 60, "an FVG entry only within this many minutes after the gap formed (B: 12)"));
            r0.Children.Add(ADrop("fstack", "FVGs IN A ROW", () => aset.FvgStack ? "STACKED: one zone" : "each gap alone", () => new List<Mi> { new Mi { Text = "STACKED — gaps formed one after another the same way are ONE zone: the entry at the 50% of all of them, the hedge at the first gap's first candle", Do = () => aset.FvgStack = true }, new Mi { Text = "EACH GAP ALONE", Do = () => aset.FvgStack = false } }));
            r0.Children.Add(ADrop("fstrict", "FVG STRENGTH", () => aset.FvgStrict ? "STRONG gaps only" : "every gap", () => new List<Mi> { new Mi { Text = "EVERY gap", Do = () => aset.FvgStrict = false }, new Mi { Text = "STRONG — the middle candle's body is bigger than the average range of the last 20 (the research's gap)", Do = () => aset.FvgStrict = true } }));
            aSettings.Children.Add(r0);
            var r1 = new WrapPanel();
            r1.Children.Add(AField("sym", "SYMBOL", aset.Symbol, 110, "the symbol as MT5 names it (XAUUSD, NAS100, USTEC …) — the automation owns every position on it"));
            r1.Children.Add(ADrop("tf", "TIMEFRAME", () => Tf.Name(aset.Tf), () => Tf.All.Select(m => new Mi { Text = Tf.Name(m), Do = () => { aset.Tf = m; } }).ToList()));
            r1.Children.Add(ADrop("swing", "LEVELS (BREAKS)", () => aset.Swing ? "the last SWING high / low" : "the last N candles", () => new List<Mi> { new Mi { Text = "SWING — the most recent swing high (BUY) / swing low (SELL): a candle higher / lower than its neighbours", Do = () => aset.Swing = true }, new Mi { Text = "LAST N CANDLES — the highest high / lowest low of the last N finished candles", Do = () => aset.Swing = false } }));
            r1.Children.Add(AField("swk", "SWING STRENGTH (CANDLES EACH SIDE)", aset.SwingK.ToString(IC), 60, "a swing high = a candle higher than this many finished candles on each side (2 = the usual)"));
            r1.Children.Add(AField("look", "RECENT HIGH / LOW (CANDLES)", aset.Lookback.ToString(IC), 60, "the high / low of the last N FINISHED candles of the timeframe (1 = the previous candle)"));
            r1.Children.Add(AField("entry", "ENTRY LOTS", aset.EntryLots.ToString(IC), 70, "the first trade of every iteration"));
            r1.Children.Add(AField("add", "FIXED ADD-ON LOTS (OTHER WAY)", aset.AddLots.ToString(IC), 70, "each add-on the other way: entry 0.5 buy → add-on 1.0 sell (net 0.5 sell) → 1.0 buy …"));
            r1.Children.Add(AField("target", "TARGET $ / ITERATION", aset.Target.ToString(IC), 80, "the whole basket closes at +$ (profit + swap + commission) — whatever the drawdown on the way"));
            r1.Children.Add(AField("dd", "MAX DRAWDOWN $ (0 = NONE)", aset.MaxDrawdown.ToString(IC), 80, "0 = none: until the target or the account blows; else the basket closes at −$"));
            r1.Children.Add(AField("ddpct", "⛔ MAX DRAWDOWN % (0 = none)", aset.MaxDdPct.ToString(IC), 60, "the iteration closes when it is down this % of its starting balance (open + realised) — then the next trade (AFTER MAX DRAWDOWN)"));
            r1.Children.Add(AField("dayloss", "⛔ DAILY LOSS $ (0 = none)", aset.DayLoss.ToString(IC), 70, "today's (New York day) closed + open loss reaches it → close everything, NO new iteration until tomorrow"));
            r1.Children.Add(AField("daypct", "⛔ DAILY LOSS % (0 = none)", aset.DayLossPct.ToString(IC), 60, "the same as a % of the day's starting balance"));
            aSettings.Children.Add(r1);
            var r2 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            r2.Children.Add(ADrop("first", "FIRST DIRECTION", () => aset.FirstDir == "AUTO" ? "AUTO — the break" : aset.FirstDir + " — at once", () => new List<Mi> { new Mi { Text = "AUTO — the first break of the recent high (BUY) / low (SELL)", Do = () => aset.FirstDir = "AUTO" }, new Mi { Text = "BUY — at once", Fg = Green, Do = () => aset.FirstDir = "BUY" }, new Mi { Text = "SELL — at once", Fg = Red, Do = () => aset.FirstDir = "SELL" } }));
            r2.Children.Add(ADrop("hmode", "ADD-ON WHEN", () => aset.HedgeMode == "LADDER" ? "🪜 LADDER (his: beyond the basket's high / low)" : aset.HedgeMode == "FVG" ? "FVG (the gap's first candle)" : aset.HedgeMode == "SIGNAL" ? "SIGNAL (opposite engulfing)" : aset.HedgeMode == "NONE" ? "NO HEDGE" : aset.HedgeMode == "PRICE" ? "a PRICE move" : aset.HedgeMode == "MONEY" ? "a $ LOSS" : "the BREAK of the recent low / high", () => new List<Mi> { new Mi { Text = "BREAK — the recent low (after a buy) / high (after a sell)", Do = () => aset.HedgeMode = "BREAK" }, new Mi { Text = "🪜 LADDER — a BUY only above the basket's HIGHEST leg + LADDER STEP, a SELL only below its LOWEST leg − LADDER STEP, every leg the entry lots, at most MAX LEGS (DEMO / BACKTEST only)", Do = () => aset.HedgeMode = "LADDER" }, new Mi { Text = "PRICE — a fixed move against the last leg (gold 5 = $5, NAS100 50 = 50 points)", Do = () => aset.HedgeMode = "PRICE" }, new Mi { Text = "$ LOSS — a fixed $ lost since the last leg (on what the basket holds)", Do = () => aset.HedgeMode = "MONEY" }, new Mi { Text = "FVG — for FVG entries: the first hedge at the LOW (buys) / HIGH (sells) of the first candle of the gap (stacked gaps: the first one), later hedges at the recent low / high", Do = () => aset.HedgeMode = "FVG" }, new Mi { Text = "SIGNAL — hedge only on an OPPOSITE engulfing at a 20-candle extreme (the TIMEFRAME's candles) while the newest leg is down the $ LOSS or more: HEDGE × NET the other way, at most MAX HEDGES", Do = () => aset.HedgeMode = "SIGNAL" }, new Mi { Text = "NO HEDGE — never add the other way (target, the max drawdown as an emergency stop, the close time)", Do = () => aset.HedgeMode = "NONE" } }));
            r2.Children.Add(ADrop("trend", "✅ TREND CONFIRMATION", () => aset.TrendFilter == "EMA" ? "EMA 50 / 200 — only with the trend" : "none", () => new List<Mi> { new Mi { Text = "NONE — every entry signal opens a basket", Do = () => aset.TrendFilter = "NONE" }, new Mi { Text = "EMA 50 / 200 — a new basket only in the trend's direction: BUY when the EMA 50 is over the EMA 200 and the price over the EMA 200 (SELL the mirror); no trend = no entry. LADDER: a leg against the trend only as a hedge (it reduces the net). 12 months replayed: fewer stuck baskets, the deepest dip −17 % instead of −24 %", Do = () => aset.TrendFilter = "EMA" } }));
            r2.Children.Add(AField("lstep", "🪜 LADDER STEP (price)", aset.LadderStep.ToString(IC), 60, "LADDER: the distance beyond the basket's highest / lowest leg for the next leg (his day 1: ≈ 2–4 points); STRADDLE: half of it each side of the price"));
            r2.Children.Add(AField("lmax", "🪜 MAX LEGS (0 = ∞)", aset.LadderMax.ToString(IC), 50, "LADDER: no more legs once the basket holds this many (his day 1: 9 at most; without a cap the 12-month backtest blew every account)"));
            r2.Children.Add(AField("hprice", "PRICE MOVE", aset.HedgePrice.ToString(IC), 60, "ADD-ON WHEN = PRICE: the move against the last leg in price (gold: 5 = $5; NAS100: 50 = 50 points)"));
            r2.Children.Add(AField("hmoney", "$ LOSS", aset.HedgeMoney.ToString(IC), 60, "$ LOSS: the $ lost since the last leg opened • SIGNAL: how much the newest leg must be losing before a hedge"));
            r2.Children.Add(ADrop("hsize", "HEDGE SIZE", () => aset.HedgeSize == "RECOVER" ? "RECOVER in a move" : aset.HedgeSize == "ADVANTAGE" ? "LEAD BY " + aset.NetAdvantage.ToString("0.##", IC) + " lots" : aset.HedgeSize == "STEPLEAD" ? "STEP LEAD +" + aset.NetAdvantage.ToString("0.##", IC) + " per hedge" : "FIXED add-on lots", () => new List<Mi> { new Mi { Text = "LEAD BY — the new side always leads by LEAD BY lots (0.2: BUY 0.1 → SELL 0.3 = sells lead by 0.2 → BUY 0.4 = buys lead by 0.2 → SELL 0.4 …)", Do = () => aset.HedgeSize = "ADVANTAGE" }, new Mi { Text = "STEP LEAD — the lead GROWS with every hedge by LEAD BY (0.1: the 1st hedge leads by 0.1, the 2nd by 0.2, the 3rd by 0.3 …) — bigger each time the market turns, so a late turn pays more", Do = () => aset.HedgeSize = "STEPLEAD" }, new Mi { Text = "FIXED — every hedge = the ADD-ON LOTS (the net advantage shrinks as legs pile up)", Do = () => aset.HedgeSize = "FIXED" }, new Mi { Text = "RECOVER — each hedge sized so a move of RECOVER IN (price) its way brings the WHOLE basket to the target: out fast, bigger lots when the drawdown is deeper (capped by MAX HEDGE LOTS)", Do = () => aset.HedgeSize = "RECOVER" } }));
            r2.Children.Add(AField("nadv", "LEADS BY (LOTS)", aset.NetAdvantage.ToString(IC), 80, "every add-on is sized so the NEW direction leads by this many lots: 0.2 → BUY 0.1, then SELL 0.3 (sells lead by 0.2), then BUY 0.4 (buys lead by 0.2), then SELL 0.4 …"));
            af["nadv"].TextChanged += delegate { double v; if (double.TryParse((af["nadv"].Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, IC, out v) && v > 0) { aset.NetAdvantage = v; Button hb; if ((aset.HedgeSize == "ADVANTAGE" || aset.HedgeSize == "STEPLEAD") && aDrops.TryGetValue("hsize", out hb)) hb.Content = (aset.HedgeSize == "STEPLEAD" ? "STEP " : "") + "LEAD BY " + v.ToString("0.##", IC) + " lots  ▾"; } };   // the HEDGE SIZE label follows the box as you type
            r2.Children.Add(AField("rmove", "RECOVER IN (PRICE)", aset.RecoverMove.ToString(IC), 55, "HEDGE SIZE = RECOVER: the price move (gold 3 = $3) that should take the whole basket to the target"));
            r2.Children.Add(AField("hmax", "MAX HEDGE LOTS", aset.HedgeMaxLots.ToString(IC), 55, "HEDGE SIZE = RECOVER: never a bigger hedge than this"));
            r2.Children.Add(AField("hmult", "HEDGE × NET", aset.HedgeMult.ToString(IC), 50, "SIGNAL: the hedge = this × the net lots open, the other way (2 = 0.1 buy → 0.2 sell)"));
            r2.Children.Add(AField("maxh", "MAX HEDGES", aset.MaxHedges.ToString(IC), 50, "SIGNAL: hedges an iteration at most"));
            r2.Children.Add(ADrop("exit", "TAKE PROFIT", () => aset.ExitMode == "LEGS" ? "each LEG its own +$" : "the whole BASKET +$", () => new List<Mi> { new Mi { Text = "BASKET — everything closes when the basket reaches the target", Do = () => aset.ExitMode = "BASKET" }, new Mi { Text = "EACH LEG — every position closes at its own target × its lots / the entry lots (0.2 lots = 2 × the target)", Do = () => aset.ExitMode = "LEGS" } }));
            {   // ☑ UPDATE RECENT HIGHS / LOWS IN REAL TIME (user's request: an option): on = the hedge level follows the newest swing low / high
                // (the price went up and came back down = a new recent low); off = fixed at the low / high when the leg opened
                var rsp = new StackPanel { Margin = new Thickness(0, 0, 12, 4) };
                rsp.Children.Add(new TextBlock { Text = "BREAK LEVEL", Foreground = Muted, FontSize = 9.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
                aRollCb = new CheckBox { Content = "UPDATE RECENT HIGHS / LOWS IN REAL TIME", IsChecked = aset.Rolling, Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 0, 0),
                    ToolTip = "ON: the hedge line moves to the NEWEST swing low (buy leading) / high (sell leading) as soon as one forms — up and back down = a new recent low\nOFF: the hedge line stays at the low / high it had when the leg opened" };
                aRollCb.Click += delegate { aset.Rolling = aRollCb.IsChecked == true; AutoSaveSet(); ARollPaint(); };
                ARollPaint();
                rsp.Children.Add(aRollCb); afBox["roll"] = rsp; r2.Children.Add(rsp);
            }
            r2.Children.Add(ADrop("next", "NEXT ITERATION", () => aset.NextStart == "PAUSE" ? "after a PAUSE" : aset.NextStart == "WINNER" ? "after a WIN: same direction at once" : "the next ENTRY SIGNAL", () => new List<Mi> { new Mi { Text = "the very next ENTRY SIGNAL of the setup (a break, an FVG touch …)", Do = () => aset.NextStart = "BREAK" }, new Mi { Text = "after a PAUSE — then the last basket's direction at once", Do = () => aset.NextStart = "PAUSE" }, new Mi { Text = "after a WIN: the SAME DIRECTION again at once (a winning SELL → SELL again); after a loss: the next entry signal", Do = () => aset.NextStart = "WINNER" } }));
            r2.Children.Add(AField("pause", "PAUSE (SECONDS)", aset.PauseSec.ToString(IC), 60, "NEXT ITERATION = PAUSE: seconds of the market's clock (300 = 5 minutes)"));
            r2.Children.Add(ADrop("afterdd", "AFTER MAX DRAWDOWN", () => aset.AfterDrawdown == "NEXT" ? "the NEXT iteration" : "STOP", () => new List<Mi> { new Mi { Text = "STOP the automation", Do = () => aset.AfterDrawdown = "STOP" }, new Mi { Text = "the NEXT iteration", Do = () => aset.AfterDrawdown = "NEXT" } }));
            r2.Children.Add(AField("maxit", "MAX ITERATIONS (0 = ∞)", aset.MaxIterations.ToString(IC), 60, ""));
            r2.Children.Add(AField("comm", "COMMISSION $/LOT", aset.CommissionPerLot.ToString(IC), 60, "round turn, taken off the basket when MT5 does not show it (RAW accounts); 0 = as MT5 shows"));
            aSettings.Children.Add(r2);
            var r3 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            r3.Children.Add(ADrop("emode", "ENTRY SETUP", () => aset.EntryMode == "STRADDLE" ? "🪜 STRADDLE (buy stop + sell stop)" : aset.EntryMode == "AGGBRK" ? "AGGRESSION BREAK" : aset.EntryMode == "VWAPREJ" ? "VWAP REJECTION" : aset.EntryMode == "ENGULF" ? "ENGULFING at a 20-candle extreme" : aset.EntryMode == "FVG50" ? "FVG 50% touch" : aset.EntryMode == "FVGBREAK" ? "FVG + BREAK" : aset.EntryMode == "FVG" ? "FVG retest" : aset.EntryMode == "VWAP" ? "VWAP cross" : aset.EntryMode == "FLOW" ? "ORDER FLOW surge" : "BREAK of high / low", () => new List<Mi> {
                new Mi { Text = "🪜 STRADDLE — a buy stop LADDER STEP / 2 above and a sell stop LADDER STEP / 2 below the price: whichever the price reaches first (DEMO / BACKTEST only)", Do = () => aset.EntryMode = "STRADDLE" },
                new Mi { Text = "ENGULFING — an engulfing candle at the lowest low (BUY) / highest high (SELL) of the last 20 candles of the TIMEFRAME (model A)", Do = () => aset.EntryMode = "ENGULF" },
                new Mi { Text = "AGGRESSION BREAK — AGGRESSION CANDLES+ of one colour (3, 4, 5 …), the FIRST opposite close = the signal; the break of its high (BUY) / low (SELL) during the next candle = the entry (any timeframe)", Do = () => aset.EntryMode = "AGGBRK" },
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
            {   // 🧱 ANTI-CHOP (user's request — every part a checkbox): hedge only beyond the basket's range; LOCK in the chop and wait for the break
                var r5 = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
                r5.Children.Add(new TextBlock { Text = "🧱 ANTI-CHOP", Foreground = Amber, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
                r5.Children.Add(AField("hcap", "🛡 MAX HEDGE DISTANCE (price, 0 = off)", aset.HedgeCap.ToString(IC), 60, "the hedge is NEVER further than this from the losing side (its newest leg; the basket's high / low with range-only). In a straight trend there is no swing to hedge at — then the hedge sits here, and a price already past it hedges at once. Gold: 5 = $5"));
                r5.Children.Add(ACheck("rguard", "🛡 RISK GUARD", () => aset.RiskGuard, v => aset.RiskGuard = v, "ON: your setup runs as it is while things look normal. When a basket turns DANGEROUS — the next hedge would make it TOO BIG (more than MAX LOTS per $5,000), the MARGIN LEVEL is low, or it is CHOP (HEDGES / HOUR) — it LOCKS instead of hedging (buys = sells, the loss stops growing) and waits for the real break; still too big at the break → it LIFTS (closes the side against the move) instead of adding more. The STEP LEAD never grows past MAX LEAD STEPS, the hedge distance widens when the market swings more, and at half the margin limit it locks at once"));
                r5.Children.Add(ACheck("thedge", "🧭 TREND-AWARE HEDGE", () => aset.TrendHedge, v => aset.TrendHedge = v, "ON: when the trend (EMA 50 / 200) is WITH the basket's net side, a hedge against it waits deeper — beyond the basket's lowest / highest leg and at least 2× the normal distance — so gold's normal pullbacks in a trend never trigger it"));
                r5.Children.Add(ACheck("scaleout", "📉 SCALE OUT", () => aset.ScaleOut, v => aset.ScaleOut = v, "ON: while the basket holds more than its lead, every SCALE STEP of price in its favour closes SCALE LOTS of its best leg — it gets smaller as it wins (the slices are banked and count for the target), so a turn hurts less"));
                r5.Children.Add(AField("slots", "SCALE LOTS", aset.ScaleLots.ToString(IC), 45, "📉 the size of each slice (0.05)"));
                r5.Children.Add(AField("sstep", "SCALE STEP (price)", aset.ScaleStep.ToString(IC), 45, "📉 how far the price goes your way between slices (gold 1.5 = $1.50)"));
                r5.Children.Add(ACheck("pairoff", "🛟 PAIR-OFF", () => aset.PairOff, v => aset.PairOff = v, "🛟 DANGER RECOVERY (with the RISK GUARD): a locked basket waits for an OBVIOUS trend (its range broken by TREND BREAK × the average candle AND the EMA 50 / 200 the same way); then it closes the best winning leg together with the worst losing leg whenever they add up to ≥ $0 — the basket shrinks for free — before it leans with the trend"));
                r5.Children.Add(AField("gbreak", "TREND BREAK (× candle)", aset.GuardBreak.ToString(IC), 40, "🛟 how far beyond the locked range the price must go (in average candles) before it counts as an obvious trend (1 = one average 1-minute candle)"));
                r5.Children.Add(AField("dat", "🚨 DANGER EXIT AT (0 = off)", (aset.DangerExit ? aset.DangerAt : 0).ToString(IC), 45, "🚨 the danger meter (0 calm … 100 critical: loss vs balance, margin level, chop, time in the trade, size vs the cap, how similar baskets ended) at / over this → the basket closes at the smaller profit below"));
                r5.Children.Add(AField("dkeep", "🚨 SMALLER PROFIT % OF TARGET", aset.DangerKeep.ToString(IC), 45, "in danger the basket closes as soon as it is back at this % of the target (25 = +$2.50 of a +$10 target)"));
                r5.Children.Add(AField("hfloat", "⛔ HARD STOP FLOATING % (0 = off)", aset.HardFloat.ToString(IC), 45, "⛔ the floating loss of the basket reaches this % of the balance → CLOSE EVERYTHING (it is what it is)"));
                r5.Children.Add(AField("hmargin", "⛔ HARD STOP MARGIN % (0 = off)", aset.HardMargin.ToString(IC), 45, "⛔ the margin level falls to this % → CLOSE EVERYTHING before the broker's stop-out (50 %) does it worse"));
                r5.Children.Add(AField("glots", "MAX LOTS / $5k", aset.GuardLots.ToString(IC), 45, "🛡 the basket's total lots (buys + sells) never above this per $5,000 of balance — the next hedge locks instead (1.0 = 1 lot on $5,000)"));
                r5.Children.Add(AField("gmargin", "MIN MARGIN %", aset.GuardMargin.ToString(IC), 50, "🛡 below this margin level the next hedge locks instead; at half of it the basket locks at once (the broker stops you out at 50 %)"));
                r5.Children.Add(AField("gchop", "HEDGES / HOUR", aset.GuardChop.ToString(IC), 40, "🛡 this many hedges in the last hour = chop: the next one locks instead (0 = off)"));
                r5.Children.Add(AField("glead", "MAX LEAD STEPS", aset.GuardMaxLead.ToString(IC), 40, "🛡 STEP LEAD grows at most this many steps (3: 0.1 → 0.2 → 0.3, then stays 0.3)"));
                r5.Children.Add(AField("pat", "🔐 PROTECT AT (% of target, 0 = off)", aset.ProtectAt.ToString(IC), 50, "a basket that reached this % of its target (e.g. 70 = $35 of $50) and then falls back closes at the KEEP level — a near miss of the take profit never turns into a long hedge"));
                r5.Children.Add(AField("pkeep", "KEEP (% of target)", aset.ProtectKeep.ToString(IC), 50, "where a protected basket closes (10 = $5 of a $50 target, 0 = breakeven)"));
                r5.Children.Add(ACheck("quick", "⚡ QUICK HEDGE", () => aset.QuickHedge, v => aset.QuickHedge = v, "ON: once the price has run the QUICK AFTER distance beyond the basket's best leg, the hedge moves up to the LAST FINISHED CANDLE's low (long basket) / high (short) — the first red candle that breaks a low hedges at once, near the top, instead of waiting back at the old swing low. With ADVANTAGE sizing the hedge flips the basket to the new side (and ADD TO THE WINNER adds to it)"));
                r5.Children.Add(ACheck("smart", "🧠 SMART (learns from its history)", () => aset.Smart, v => aset.Smart = v, "ON: before each new basket it checks how the baskets that started in the same conditions (session, hour, VWAP side, order flow, FVG, side) ended — mostly stuck / lost → it skips (every 5th still trades, to keep learning); and the post-mortems' best protection becomes part of the settings from the next iteration (DEMO by itself, a real account: proposed)"));
                r5.Children.Add(ACheck("belock", "🔐 BREAKEVEN LOCK", () => aset.BeLock, v => aset.BeLock = v, "ON: once the basket reached the % of the target below, an EQUAL hedge waits where the basket would be back at the small profit below — if the price turns there it LOCKS (net 0, the profit kept) and waits for the next break"));
                r5.Children.Add(AField("belat", "🔐 ARM AT % OF TARGET", aset.BeLockAt.ToString(IC), 45, "80 = once the basket made +$40 of a +$50 target"));
                r5.Children.Add(AField("belkeep", "🔐 LOCK AT % OF TARGET", aset.BeLockKeep.ToString(IC), 45, "10 = the lock comes in when the basket is back at +$5 (of a +$50 target)"));
                r5.Children.Add(ACheck("rexit", "🔄 RECOVERED EXIT", () => aset.RecoverExit, v => aset.RecoverExit = v, "ON: a basket that went DEEP (× the target below) and came back close to breakeven closes there — a tiny loss or better instead of riding the same basket down again"));
                r5.Children.Add(AField("rdeep", "🔄 DEEP = × TARGET", aset.RecDeep.ToString(IC), 45, "how deep the basket must have been: 2 = twice the target (−$100 with a $50 target)"));
                r5.Children.Add(AField("rkeep", "🔄 CLOSE WITHIN % OF TARGET", aset.RecKeep.ToString(IC), 45, "20 = it closes once it is back at −$10 or better with a $50 target"));
                r5.Children.Add(ACheck("sbest", "🏆 ONLY THE BEST SETUPS (SMART, 100+ baskets)", () => aset.SmartBest, v => aset.SmartBest = v, "with 🧠 SMART and 100+ learned baskets: a new basket only where one of its conditions gave CLEAN baskets (a win, no hedge, ≤ 30 min, ≤ 0.5 % deep) clearly more often than usual — see 🏆 THE BEST SETUPS in LEARN"));
                r5.Children.Add(ACheck("khedge", "🔑 KEY-LEVEL HEDGE", () => aset.KeyHedge, v => aset.KeyHedge = v, "ON: a hedge that would sit just past a key level (repeated swing lows / highs, VWAP, EMA 50 / 200, the day's high / low, round $5) moves beyond it by the buffer — a bounce there no longer fills it, a real break does"));
                r5.Children.Add(AField("kbuf", "🔑 BUFFER × CANDLE", aset.KeyBuffer.ToString(IC), 45, "how far beyond the key level: 0.5 = half an average candle"));
                r5.Children.Add(ACheck("chedge", "🕯 HEDGE AT THE ENTRY CANDLE", () => aset.CandleHedge, v => aset.CandleHedge = v, "ON: the hedge sits just beyond the LOW (buy) / HIGH (sell) of the candle the newest leg filled in (the breakout candle before it while that one is forming) when that is closer than the swing — a quicker hedge (more hedges in a chop: watch 🧠 LEARN)"));
                r5.Children.Add(AField("qafter", "QUICK AFTER (price)", aset.QuickAfter.ToString(IC), 60, "how far the price must run beyond the basket's best leg before the quick hedge trails the last candle (gold: 3 = $3)"));
                r5.Children.Add(ACheck("ronly", "HEDGE ONLY BEYOND THE BASKET'S RANGE", () => aset.RangeOnly, v => aset.RangeOnly = v, "ON: a hedge SELL only under the basket's lowest leg, a hedge BUY only over its highest — never inside the small range it already holds (no hedging in chop)"));
                r5.Children.Add(AField("beafter", "🛟 RESCUE EXIT AFTER (HEDGES, 0 = OFF)", aset.BeAfter.ToString(IC), 60, "a basket with this many hedges or more closes as soon as it is back at breakeven ($0 after every cost) — no endless baskets (3 = tested best over 5 months of gold)"));
                r5.Children.Add(ACheck("lockon", "🔒 LOCK IN CHOP (net 0, wait for the break)", () => aset.LockOn, v => aset.LockOn = v, "ON: after LOCK AFTER hedges inside LOCK RANGE $ of price, buys = sells (the basket's P/L is frozen); nothing happens until the price breaks that range — then UNLOCK"));
                r5.Children.Add(AField("lafter", "LOCK AFTER (HEDGES)", aset.LockAfter.ToString(IC), 50, "how many hedges inside the small range before it locks (2 = the second one)"));
                r5.Children.Add(AField("lrange", "LOCK RANGE $ (PRICE)", aset.LockRange.ToString(IC), 60, "the basket's legs all inside this many $ of gold price = chop (3 = a $3 range)"));
                r5.Children.Add(ADrop("unlock", "UNLOCK ON THE BREAK", () => aset.Unlock == "LIFT" ? "LIFT the side against it" : "LEAD the break's way", () => new List<Mi> {
                    new Mi { Text = "LEAD — the break's side gets LEADS BY lots more (every leg stays open, the basket's +$ target as usual)", Do = () => aset.Unlock = "LEAD" },
                    new Mi { Text = "LIFT — every leg AGAINST the break closes; the legs with the break keep running", Do = () => aset.Unlock = "LIFT" } }));
                aSettings.Children.Add(r5);
            }
            root.Children.Add(card);

            // the numbers, the views
            aTiles = new ContentControl { Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(aTiles, Dock.Top); root.Children.Add(aTiles);
            aViews = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) }; DockPanel.SetDock(aViews, Dock.Top);
            foreach (var v in new[] { "STORY", "ITERATIONS", "TRADES", "ANALYSIS", "LEARN", "LOG", "🗂 TESTS" }) { var vv = v; var b = Pill(v, Tint(Sx.Card2, 220)); b.Height = 26; b.Click += delegate { aView = vv; AutoPaint(true); aTable.ScrollToHome(); }; aViews.Children.Add(b); }
            aSetBtn = Pill("⚙ 1 · SETTINGS — SET UP FIRST", Tint(Gold, 200)); aSetBtn.Foreground = Bg; aSetBtn.Height = 26; aSetBtn.ToolTip = "step 1: the strategy's settings (they lock while it runs) — then ▶ START"; aSetBtn.Click += delegate { aView = "⚙ SETTINGS"; AutoPaint(true); }; aViews.Children.Insert(0, aSetBtn);   // user's request: the first tab, an obvious colour
            var raceBtn = Pill("🏁 RACE", Tint(Sx.Card2, 220)); raceBtn.Height = 26; raceBtn.ToolTip = "every strategy at once in the background, each on its own simulated account — the leaderboard of the day and the history"; raceBtn.Click += delegate { aView = "🏁 RACE"; AutoPaint(true); RacePaint(); }; aViews.Children.Add(raceBtn);
            aGuideBtn = Pill("🎯 CHART GUIDES", Tint(Sx.Card2, 220)); aGuideBtn.Height = 26; aGuideBtn.ToolTip = "draw what the strategy watches on the 📈 CHART (VWAP, the 20-candle high / low, the FVG zone, the hedge box, the next hedge) — a STRONG setup blinks"; aGuideBtn.Click += delegate { aGuidesOn = !aGuidesOn; Put("auto.guides", aGuidesOn ? "1" : "0"); AutoPaint(true); }; aViews.Children.Add(aGuideBtn);
            var exp = Pill("📦 EXPORT FOR CLAUDE", Tint(Violet, 90)); exp.Height = 26; exp.ToolTip = "everything of this account in ONE organized folder + ONE .zip (settings, iterations, trades, story, analysis, learn, log, the candles, the race) — send the .zip to Claude to analyze"; exp.Click += delegate { AutoExport(); }; aViews.Children.Add(exp);
            aRebornBtn = Pill("", Tint(Sx.Card2, 220)); aRebornBtn.Height = 26; aRebornBtn.ToolTip = "DEMO only: a blown account (no money / margin, or under 10 % of the start) restarts by itself at the DEMO's start balance — the blow-up and every iteration are saved"; aRebornBtn.Click += delegate { Put("auto.reborn." + AKey, RebornOn ? "0" : "1"); AutoPaint(true); }; aRebornBtn.Visibility = Paper ? Visibility.Visible : Visibility.Collapsed; aViews.Children.Add(aRebornBtn);
            var clr = Pill("🗑 CLEAR HISTORY", Tint(Sx.Card2, 220)); clr.Height = 26; clr.Click += delegate { AutoClear(); }; aViews.Children.Add(clr);
            root.Children.Add(aViews);
            aTable = new TextBox { IsReadOnly = true, FontFamily = Sx.Mono, FontSize = 13.5, MinHeight = 160, Background = Panel, Foreground = Text, BorderBrush = Line, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, Padding = new Thickness(8) };
            aRows = new StackPanel(); aRowsView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = aRows, Background = Panel, Visibility = Visibility.Collapsed };
            var tableHost = new Grid(); tableHost.Children.Add(aTable); tableHost.Children.Add(aRowsView); root.Children.Add(tableHost);
            root.Children.Remove(card); aSetScroll = new ScrollViewer { Content = card, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, Visibility = Visibility.Collapsed }; tableHost.Children.Add(aSetScroll);
            tableHost.Children.Add(BuildRace());
            root.Children.Remove(aViews); root.Children.Insert(1, aViews);   // the views stay at the top, always reachable

            // the engine's clock: every tick of the BACKTEST; every 200 ms live (the bridge sends the state every 200 ms)
            if (Sim) sim.OnTickHook = () => { auto.Step(sim); RaceStep(); };
            aTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
            aResumeAt = DateTime.Now; aResumePending = !Sim && AutoWasRunning();
            raceResumeAt = DateTime.Now; raceResumePending = !Sim && Get("race.run", "0") == "1";
            aTimer.Tick += delegate {  int curN = auto.Running && auto.Current != null ? auto.Current.N : 0; if (curN != 0 && curN != aSeenIter) { aSeenIter = curN; if (raceShow != "") { raceShow = ""; if (raceShowBox != null) raceShowBox.SelectedIndex = 0; raceChipsSig = ""; } }   // your automation opened an iteration: the chart goes back to 🤖 MINE
                if (aResumePending) AutoTryResume(); if (raceResumePending) RaceTryResume(); if (Paper && !aPctChecked) AutoDemoCommission(); if (Paper && !aHedgedAsked) AutoDemoHedgedMargin(); if (!Sim) { try { auto.Step(broker); } catch (Exception ex) { auto.Stop("error: " + ex.Message); } RaceBars(); RaceStep(); } AutoSaveNew(); AutoPaint(false); if ((DateTime.Now - aScanAt).TotalSeconds >= 1) { aScanAt = DateTime.Now; AutoSetups(); if (!Sim && (DateTime.Now - aArchAt).TotalMinutes >= 10) { aArchAt = DateTime.Now; AutoArchiveCandles(); } if (aView == "🏁 RACE") RacePaint(); RaceChipsPaint(); } };
            aTimer.Start();
            Closed += delegate { aTimer.Stop(); if (auto.Running) auto.Stop("the window closed"); if (Sim) sim.OnTickHook = null; };
            AutoVis(); AutoPaint(true);
            return root;
        }

        // a coloured ON / OFF checkbox setting (kept in aChecks so a preset / UNDO repaints it)
        readonly Dictionary<string, Tuple<CheckBox, Func<bool>, string>> aChecks = new Dictionary<string, Tuple<CheckBox, Func<bool>, string>>();
        FrameworkElement ACheck(string key, string label, Func<bool> get, Action<bool> set, string tip)
        {
            var cb = new CheckBox { IsChecked = get(), FontWeight = FontWeights.Bold, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 16, 4), ToolTip = tip };
            Action paint = () => { bool on = cb.IsChecked == true; cb.Content = (on ? "ON • " : "OFF • ") + label; cb.Foreground = on ? Green : Muted; };
            cb.Click += delegate { set(cb.IsChecked == true); AutoSaveSet(); paint(); AutoVis(); }; paint(); aChecks[key] = Tuple.Create(cb, get, label); afBox[key] = cb; return cb;
        }
        void AChecksPaint() { foreach (var c in aChecks.Values) { bool on = c.Item2(); c.Item1.IsChecked = on; c.Item1.Content = (on ? "ON • " : "OFF • ") + c.Item3; c.Item1.Foreground = on ? Green : Muted; } }
        // the real-time box says ON / OFF in words and colour (the tick alone is hard to see on the dark theme)
        void ARollPaint() { if (aRollCb == null) return; bool on = aRollCb.IsChecked == true; aRollCb.Content = on ? "ON • UPDATE RECENT HIGHS / LOWS IN REAL TIME" : "OFF • fixed at the leg (tick to update in real time)"; aRollCb.Foreground = on ? Green : Muted; }
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
            b.Click += delegate { var list = items(); foreach (var m in list) { var act = m.Do; m.Do = () => { act(); b.Content = now() + "  ▾"; AutoSaveSet(); AutoVis(); }; } SxMenu.Show(b, new Point(0, b.ActualHeight + 4), list); };
            sp.Children.Add(b); return sp;
        }
        // a researched model: its settings into the boxes (the drop-downs' labels follow)
        // every box and drop-down shows aset (UNDO: back to the settings in use)
        void AutoModelBoxes()
        {
            Action<string, string> t = (k, v) => { TextBox tb; if (af.TryGetValue(k, out tb)) tb.Text = v; }; Func<double, string> d = v => v.ToString(IC);
            t("sym", aset.Symbol); t("look", d(aset.Lookback)); t("entry", d(aset.EntryLots)); t("add", d(aset.AddLots)); t("target", d(aset.Target)); t("dd", d(aset.MaxDrawdown)); t("comm", d(aset.CommissionPerLot));
            t("pause", d(aset.PauseSec)); t("maxit", d(aset.MaxIterations)); t("hprice", d(aset.HedgePrice)); t("hmoney", d(aset.HedgeMoney)); t("win", aset.Windows); t("nyoff", d(aset.NyOffsetHours)); t("perday", d(aset.PerDay));
            t("nb", d(aset.NewsBefore)); t("na", d(aset.NewsAfter)); t("wlots", d(aset.WinnerLots)); t("wlots2", d(aset.WinnerLotsSell)); t("wmax", d(aset.MaxWinnerAdds)); t("swk", d(aset.SwingK)); t("fvol", d(aset.FlowVol)); t("fshare", d(aset.FlowShare));
            t("eod", aset.CloseAtNy); t("fage", d(aset.FvgAge)); t("aggrun", d(aset.AggRun)); t("hmult", d(aset.HedgeMult)); t("maxh", d(aset.MaxHedges)); t("nadv", d(aset.NetAdvantage)); t("rmove", d(aset.RecoverMove)); t("hmax", d(aset.HedgeMaxLots));
            if (aRollCb != null) { aRollCb.IsChecked = aset.Rolling; ARollPaint(); } AChecksPaint(); t("beafter", d(aset.BeAfter)); t("qafter", d(aset.QuickAfter)); t("pat", d(aset.ProtectAt)); t("glots", d(aset.GuardLots)); t("kbuf", d(aset.KeyBuffer)); t("belat", d(aset.BeLockAt)); t("belkeep", d(aset.BeLockKeep)); t("rdeep", d(aset.RecDeep)); t("rkeep", d(aset.RecKeep)); t("ddpct", d(aset.MaxDdPct)); t("dayloss", d(aset.DayLoss)); t("daypct", d(aset.DayLossPct)); t("dat", d(aset.DangerExit ? aset.DangerAt : 0)); t("dkeep", d(aset.DangerKeep)); t("gbreak", d(aset.GuardBreak)); t("slots", d(aset.ScaleLots)); t("sstep", d(aset.ScaleStep)); t("hfloat", d(aset.HardFloat)); t("hmargin", d(aset.HardMargin)); t("gmargin", d(aset.GuardMargin)); t("gchop", d(aset.GuardChop)); t("glead", d(aset.GuardMaxLead)); t("pkeep", d(aset.ProtectKeep)); t("hcap", d(aset.HedgeCap)); t("lstep", d(aset.LadderStep)); t("lmax", d(aset.LadderMax)); t("lafter", d(aset.LockAfter)); t("lrange", d(aset.LockRange));
            foreach (var b in aDrops.Values) { var f = b.Tag as Func<string>; if (f != null) b.Content = f() + "  ▾"; }
            AutoVis();
        }
        // 🧠 POST-MORTEM of every iteration that closes in this window (LIVE / PROP / DEMO): the same candles replayed with the protections,
        // in the background (PostMortem.cs — simulated only), saved per account and shown first in 🧠 LEARN
        readonly HashSet<string> pmSeen = new HashSet<string>(); bool pmBusy, pmPrimed; List<PostMortemRow> pmRows;
        string PmFile { get { return PostMortem.FileOf(Dir, AKey); } }
        void AutoPostMortems()
        {
            if (Sim || pmBusy) return;
            var done = auto.Iterations.Where(x => x.Done).ToList();
            if (!pmPrimed) { pmPrimed = true; foreach (var x in done) pmSeen.Add(x.N + "@" + x.Start.Ticks); pmRows = PostMortem.Load(PmFile); return; }   // history is not replayed again
            var it = done.FirstOrDefault(x => !pmSeen.Contains(x.N + "@" + x.Start.Ticks)); if (it == null) return; pmSeen.Add(it.N + "@" + it.Start.Ticks);
            var set = auto.Set.Copy(); SimSpec spec = null; List<Mt5Bar> m1 = null; int spread = -1; double bal = it.BalanceStart > 0 ? it.BalanceStart : 5000;
            lock (broker.State.Gate)
            {
                var st = broker.State; Mt5Symbol ms; if (st.Symbols.TryGetValue(set.Symbol, out ms)) spec = SimSpec.From(ms, st.ServerOffsetSec, set.NyOffsetHours, st.Account.Currency);
                m1 = st.SeriesOf(set.Symbol, 1).Bars.Where(b => b.Time >= it.Start.AddDays(-3) && b.Time <= it.End.AddHours(6)).Select(b => b.Copy()).ToList();
                Mt5Quote q; if (m1.All(b => b.Spread <= 0) && st.Quotes.TryGetValue(set.Symbol, out q) && spec != null && spec.Point > 0) spread = (int)Math.Round((q.Ask - q.Bid) / spec.Point);
            }
            if (spec == null || m1.Count(b => b.Time >= it.Start && b.Time <= it.End) < 2) return;
            double comm = Paper && paper != null ? paper.Acct.Set.CommissionPerLot : Math.Max(0, set.CommissionPerLot); string file = PmFile; pmBusy = true;
            var tape = auto.TickTape.Where(t => t.T >= it.Start.AddMinutes(-3) && t.T <= it.End.AddHours(6)).ToList();   // 🎞 the real ticks of that basket (and after)
            System.Threading.Tasks.Task.Run(() =>
            {
                PostMortemRow r = null; try { r = PostMortem.Run(spec, m1, it, set, bal, spread, comm, tape); if (r.RealTicks) r.Lesson = "🎞 " + r.Lesson; PostMortem.Save(file, r); } catch { }
                Dispatcher.BeginInvoke(new Action(() => { pmBusy = false; if (r != null) { pmRows = PostMortem.Load(file); auto.Note("🧠 POST-MORTEM " + r.Lesson); AutoSelfTune(); if (aView == "LEARN") AutoPaint(true); } }));
            });
        }
        // 🧠 SELF-TUNING (SMART on): when one protection was the post-mortems' best in 6+ of the last 10 iterations, it becomes part of the settings
        // from the NEXT iteration — by itself on DEMO / BACKTEST (the user's call: these are tests), only proposed in the story on a real account
        void AutoSelfTune()
        {
            if (!auto.Set.Smart || pmRows == null || pmRows.Count < 5) return;
            var last = pmRows.OrderByDescending(x => x.Start).Take(10).ToList();
            var top = last.Where(x => x.Best != "" && x.Best != "—").GroupBy(x => x.Best).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (top == null || top.Count() < Math.Max(3, (int)Math.Ceiling(last.Count * 0.6))) return;
            string done = Get("auto.smart.tuned." + AKey, ""); if (done.Split('|').Contains(top.Key)) return;
            var alt = PostMortem.Alternatives(AutoBal()).FirstOrDefault(x => x.Item1 == top.Key); if (alt == null) return;
            var ns = auto.Set.Copy(); alt.Item2(ns); ns.Smart = true;
            string why = "🧠 SELF-TUNING: in " + top.Count() + " of the last " + last.Count + " iterations the post-mortem's best protection was " + top.Key + " (it saved $" + top.Average(x => { var b = x.Alts.FirstOrDefault(z => z.Name == x.Best); return b == null ? 0 : b.Dd - x.Dd; }).ToString("N0", IC) + " of drawdown on average)";
            if (Virtual)
            {
                auto.Queue(ns); aset = ns; Put(AKey, aset.Save()); AutoModelBoxes(); Put("auto.smart.tuned." + AKey, (done == "" ? "" : done + "|") + top.Key);
                auto.Note(why + " → applied from the NEXT iteration (DEMO / BACKTEST: by itself). The open basket keeps its settings."); Toast("🧠 learned: " + top.Key + " — from the next iteration");
            }
            else auto.Note(why + " → PROPOSED (a real account changes only when you pick it in ⚙ SETTINGS).");
        }
        // 💥 AUTO RESTART ON A BLOW-UP (user's request: "it keeps testing while I am away") — DEMO only, never a real / prop account:
        // blown = stopped for lack of money / margin, or flat under 10 % of the start balance → close what is left, save the blow-up
        // (DataDir\auto\blowups-*.csv + the story), the simulated balance back to the DEMO's start balance (a balance deal: the history and
        // every iteration stay) and the automation starts again by itself with the same settings. 💥 AUTO RESTART in the views row turns it off.
        Button aRebornBtn; DateTime aRebornAt = DateTime.MinValue;
        bool RebornOn { get { return Get("auto.reborn." + AKey, "1") == "1"; } }
        void AutoReborn()
        {
            if (!Paper || paper == null || !RebornOn || !paper.Ready || (DateTime.Now - aRebornAt).TotalSeconds < 10) return;
            double start; if (!double.TryParse(Get("demo.bal", "5000"), System.Globalization.NumberStyles.Float, IC, out start) || start <= 0) start = 5000;
            double bal, eq; int open; DateTime now; string sym = (auto.Running ? auto.Set : aset).Symbol;
            lock (broker.State.Gate) { bal = broker.State.Account.Balance; eq = broker.State.Account.Equity; open = broker.State.Positions.Count(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)); now = broker.State.ServerNow > DateTime.MinValue ? broker.State.ServerNow : DateTime.Now; }
            bool wasOn = Get("auto.run." + AKey, "0") == "1" || auto.Running;
            bool broke = !auto.Running && HedgeAuto.IsBlowReason(auto.StopReason), low = open == 0 && bal < 0.1 * start && (auto.Running || wasOn);
            if (!(broke || low)) return;
            aRebornAt = DateTime.Now;
            if (open > 0)
            {   // positions still open = NOT blown (the demo's own stop-out closes a truly blown account): never close them here — the
                // automation goes on managing them (it was only an order it could not afford)
                if (!auto.Running) { var sr = (auto.Set ?? aset).Copy(); if (auto.Check(broker, sr) == "") { auto.Start(broker, sr, true); Put("auto.run." + AKey, "1"); aWasOn = true; auto.Note("⟳ the automation was stopped for lack of margin with " + open + " position(s) open → it RESUMED and manages them (nothing was closed)"); } }
                return;
            }
            int life = 1; int.TryParse(Get("auto.life." + AKey, "0"), out life); life++; Put("auto.life." + AKey, life.ToString(IC));
            string why = broke ? auto.StopReason : "the balance $" + bal.ToString("N2", IC) + " is under 10 % of the start $" + start.ToString("N0", IC);
            try { var f = System.IO.Path.Combine(Dir, "auto", "blowups-" + string.Concat(AKey.Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c))) + ".csv"); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f)); if (!System.IO.File.Exists(f)) System.IO.File.WriteAllText(f, "time,blowup,balance,equity,iterations_done,why\n"); System.IO.File.AppendAllText(f, now.ToString("yyyy-MM-dd HH:mm:ss", IC) + "," + life + "," + bal.ToString("0.00", IC) + "," + eq.ToString("0.00", IC) + "," + auto.Iterations.Count(x => x.Done) + ",\"" + why.Replace("\"", "'") + "\"\n"); } catch { }
            if (auto.Running) auto.Stop("💥 blow-up: the DEMO restarts");
            lock (broker.State.Gate) paper.Acct.SetBalance(start, now);
            var s = (auto.Set ?? aset).Copy(); auto.Note("💥 BLOW-UP #" + life + " — " + why + " → the DEMO balance is back at $" + start.ToString("N0", IC) + " and the automation starts again by itself (every iteration and the blow-up are saved)");
            if (auto.Check(broker, s) == "") { auto.Start(broker, s, false); Put("auto.run." + AKey, "1"); aWasOn = true; }
            Toast("💥 blow-up #" + life + " — the DEMO restarted with $" + start.ToString("N0", IC)); AutoPaint(true);
        }
        // 📡 THE SIGNAL JOURNAL: every entry signal (taken or not) + what the price did in the next 15 / 30 / 60 minutes, saved per account
        string SigFile { get { return System.IO.Path.Combine(Dir, "auto", "signals-" + string.Concat(AKey.Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c))) + ".csv"); } }
        void AutoSignalSave(SignalRec r)
        {
            try { var f = SigFile; System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f)); if (!System.IO.File.Exists(f)) System.IO.File.WriteAllText(f, SignalRec.Header + "\n"); System.IO.File.AppendAllText(f, r.Csv() + "\n"); } catch { }
        }
        List<string[]> SigRows() { try { return System.IO.File.Exists(SigFile) ? System.IO.File.ReadAllLines(SigFile).Skip(1).Select(l => l.Split(',')).Where(a => a.Length >= 15).ToList() : new List<string[]>(); } catch { return new List<string[]>(); } }
        internal Button aChartManual;   // the same switch on the 📈 chart's toolbar (user's request)
        void AutoManualToggle()
        {
            bool on = !auto.ManualManage;
            if (!on && auto.Running && !Sx.Ask(this, "🤖 AUTO TAKES OVER AGAIN", "MANUAL MANAGE OFF: AUTO sends its own orders again from now on (hedges, add-ons, closes) on the positions as they are now.", "🤖 AUTO ON", "CANCEL", Green)) return;
            auto.SetManualManage(on); AutoPaint(true); AutoSetups(); foreach (var pq in panes) pq.V.InvalidateVisual();
        }
        // 🆕 WHAT'S NEW (user's request: "a window every time there is a new version, to confirm the new settings are applied"): once per build and
        // window — what changed, what THIS window's 🛡 HEDGE settings have ON / OFF now, and one button that turns the new protections on
        // (from the NEXT iteration when it is running: the open basket keeps its settings)
        void AutoWhatsNew()
        {
            string k = "seen.build." + AKey; if (Get(k, "") == Build) return; Put(k, Build);
            var a = auto.Running ? auto.Set : aset; Func<bool, string> on = b => b ? "✅ ON" : "⬜ OFF";
            string txt = "NEW IN THIS VERSION\n"
                + "• 🔄 RECOVERED EXIT (optional): a basket that went deep and came back near breakeven closes there\n"
                + "• 🧠 SMART (optional): skips entries in conditions that mostly went wrong before, learns from the post-mortems\n"
                + "• the chart banner wraps onto 2 lines (nothing cut off) • ✔ TURN ON ALL THE PROTECTIONS in the STRATEGY menu\n"
                + "• 🧠 POST-MORTEM of every closed iteration in 🛡 HEDGE ▸ LEARN (how the drawdown could have been prevented)\n"
                + "• ⛔ MAX DRAWDOWN % and ⛔ DAILY LOSS limits • 🕯 hedge at the entry candle • 🏁 RACE: BH entry on M1–H1\n"
                + "• a break by a quick wick now enters (the entry no longer moves up and up)\n\n"
                + "THIS WINDOW'S SETTINGS NOW (" + (string.IsNullOrEmpty(a.Preset) ? "your own" : a.Preset) + ", entry " + a.EntryLots.ToString("0.##", IC) + " lot, target $" + a.Target.ToString("0.##", IC) + (auto.Running ? ", running" : "") + ")\n"
                + "🔄 RECOVERED EXIT  " + on(a.RecoverExit) + "\n🧠 SMART  " + on(a.Smart) + "\n🚨 DANGER EXIT  " + (a.DangerExit ? "✅ at " + a.DangerAt.ToString("0", IC) : "⬜ OFF") + "\n🛡 RISK GUARD  " + on(a.RiskGuard)
                + "\n⛔ MAX DRAWDOWN %  " + (a.MaxDdPct > 0 ? a.MaxDdPct.ToString("0.##", IC) + " %" : "⬜ OFF") + "\n⛔ DAILY LOSS  " + (a.DayLoss > 0 || a.DayLossPct > 0 ? (a.DayLoss > 0 ? "$" + a.DayLoss.ToString("0", IC) + " " : "") + (a.DayLossPct > 0 ? a.DayLossPct.ToString("0.#", IC) + " %" : "") : "⬜ OFF")
                + "\n\nTURN ON THE PROTECTIONS = 🛡 RISK GUARD + 🚨 DANGER EXIT + 📉 SCALE OUT + range-only hedge + 🛟 rescue after 3 + ⛔ hard stops — the set that held up in the replays" + (auto.Running ? " — from the NEXT iteration (the open basket keeps its settings)" : "") + ". Your size, target and entry stay as they are. 🧠 SMART and 🔄 RECOVERED EXIT stay optional (⚙ SETTINGS ▸ ANTI-CHOP): they were lower in the replays.";
            if (!Sx.Ask(this, "🆕 " + Build, txt, "✔ TURN ON THE PROTECTIONS", "KEEP MY SETTINGS", Green)) { auto.Note("🆕 " + Build + ": settings kept as they were"); return; }
            AutoProtectAll();
        }
        // ✔ ALL THE PROTECTIONS ON, the user's entry / size / target kept (the 🆕 window and the STRATEGY menu)
        void AutoProtectAll()
        {
            // the protections that held up in the replays (your Jun–Oct 2026 settings with them: $5,000 → $6,716, lowest $4,762); 🧠 SMART and
            // 🔄 RECOVERED EXIT stay optional checkboxes (lower / far worse in the replays)
            var ns = (auto.Running ? auto.Set : AutoRead()).Copy(); ns.DangerExit = true; ns.DangerAt = 60; ns.DangerKeep = 25;
            ns.RiskGuard = true; ns.GuardMode = "LOCK"; ns.GuardLots = Math.Min(ns.GuardLots > 0 ? ns.GuardLots : 0.6, 0.6); ns.GuardMargin = Math.Max(ns.GuardMargin, 500); ns.GuardChop = ns.GuardChop > 0 ? ns.GuardChop : 3; ns.PairOff = true;
            ns.ScaleOut = true; if (ns.ScaleLots <= 0) ns.ScaleLots = 0.01; ns.RangeOnly = true; if (ns.BeAfter <= 0) ns.BeAfter = 3; if (ns.HardFloat <= 0) ns.HardFloat = 60; if (ns.HardMargin <= 0) ns.HardMargin = 60;
            aset = ns; Put(AKey, aset.Save()); AutoModelBoxes(); if (auto.Running) auto.Queue(ns);
            auto.Note("✔ PROTECTIONS ON: 🛡 RISK GUARD (lock at 0.6 lot per $5k / margin 500 % / chop) + 🚨 DANGER EXIT + 📉 SCALE OUT + range-only hedge + 🛟 rescue + ⛔ hard stops" + (auto.Running ? " — from the next iteration" : ""));
            Toast("✔ the protections are ON" + (auto.Running ? " from the next iteration" : "")); AutoPaint(true);
        }
        double AutoBal() { try { lock (broker.State.Gate) return broker.State.Account.Balance > 0 ? broker.State.Account.Balance : 5000; } catch { return 5000; } }
        void AutoModel(string name)
        {
            aset = AutoSettings.Model(name, AutoRead(), AutoBal());
            Action<string, string> t = (k, v) => { TextBox tb; if (af.TryGetValue(k, out tb)) tb.Text = v; };
            t("look", aset.Lookback.ToString(IC)); t("entry", aset.EntryLots.ToString(IC)); t("add", aset.AddLots.ToString(IC)); t("target", aset.Target.ToString(IC)); t("dd", aset.MaxDrawdown.ToString(IC));
            t("win", aset.Windows); t("eod", aset.CloseAtNy); t("fage", aset.FvgAge.ToString(IC)); t("perday", aset.PerDay.ToString(IC)); t("aggrun", aset.AggRun.ToString(IC)); t("hmoney", aset.HedgeMoney.ToString(IC)); t("hmult", aset.HedgeMult.ToString(IC)); t("maxh", aset.MaxHedges.ToString(IC)); t("maxit", aset.MaxIterations.ToString(IC));
            if (aRollCb != null) { aRollCb.IsChecked = aset.Rolling; ARollPaint(); } AChecksPaint(); t("nadv", aset.NetAdvantage.ToString(IC)); t("beafter", aset.BeAfter.ToString(IC)); t("qafter", aset.QuickAfter.ToString(IC)); t("pat", aset.ProtectAt.ToString(IC)); t("ddpct", aset.MaxDdPct.ToString(IC)); t("dayloss", aset.DayLoss.ToString(IC)); t("daypct", aset.DayLossPct.ToString(IC)); t("rdeep", aset.RecDeep.ToString(IC)); t("rkeep", aset.RecKeep.ToString(IC)); t("belat", aset.BeLockAt.ToString(IC)); t("belkeep", aset.BeLockKeep.ToString(IC)); t("kbuf", aset.KeyBuffer.ToString(IC)); t("dat", (aset.DangerExit ? aset.DangerAt : 0).ToString(IC)); t("dkeep", aset.DangerKeep.ToString(IC)); t("glots", aset.GuardLots.ToString(IC)); t("gbreak", aset.GuardBreak.ToString(IC)); t("slots", aset.ScaleLots.ToString(IC)); t("sstep", aset.ScaleStep.ToString(IC)); t("hfloat", aset.HardFloat.ToString(IC)); t("hmargin", aset.HardMargin.ToString(IC)); t("gmargin", aset.GuardMargin.ToString(IC)); t("gchop", aset.GuardChop.ToString(IC)); t("glead", aset.GuardMaxLead.ToString(IC)); t("pkeep", aset.ProtectKeep.ToString(IC)); t("hcap", aset.HedgeCap.ToString(IC)); t("lstep", aset.LadderStep.ToString(IC)); t("lmax", aset.LadderMax.ToString(IC)); t("swk", aset.SwingK.ToString(IC));
            foreach (var b in aDrops.Values) { var f = b.Tag as Func<string>; if (f != null) b.Content = f() + "  ▾"; }
            AutoVis();
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
        // ITERATIONS / TRADES as coloured cards: the time in the trade, the result, how it ended
        static string Dur(TimeSpan t) { return t.TotalHours >= 24 ? ((int)t.TotalDays) + "d " + t.ToString(@"hh\:mm", IC) : t.TotalHours >= 1 ? ((int)t.TotalHours) + "h " + t.ToString(@"mm\:ss", IC) : t.ToString(@"m\:ss", IC); }
        FrameworkElement Cell(string text, double w, Brush fg, bool bold = false, double size = 12.5, Brush chip = null)
        {
            var tb = new TextBlock { Text = text, Foreground = fg, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontFamily = Sx.Mono, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            if (chip == null) { tb.Width = w; tb.Margin = new Thickness(0, 0, 8, 0); return tb; }
            return new Border { Width = w, Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Left, Child = new Border { Background = chip is SolidColorBrush ? (Brush)Tint((SolidColorBrush)chip, 55) : chip, BorderBrush = chip, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1, 6, 1), HorizontalAlignment = HorizontalAlignment.Left, Child = tb } };
        }
        Border Row(Brush stripe, params FrameworkElement[] cells)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var c in cells) sp.Children.Add(c);
            return new Border { Child = sp, Background = Sx.CardGrad, BorderBrush = stripe, BorderThickness = new Thickness(4, 0, 0, 0), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(4, 2, 4, 2) };
        }
        void AutoRowsOf(HedgeAuto e, StackPanel aRows, string aView)
        {
            if (aView == "ITERATIONS")
            {
                var d = e.Iterations.Where(x => x.Done).ToList(); double avgS = d.Count > 0 ? d.Average(x => (x.End - x.Start).TotalSeconds) : 0;
                aRows.Children.Add(new TextBlock { Text = e.Iterations.Count + " iterations  •  won " + d.Count(x => x.Won) + " / lost " + d.Count(x => !x.Won) + "  •  net " + Signed(d.Sum(x => x.Result), "USD") + " (after " + Signed(-e.Iterations.Sum(x => x.Fees), "USD") + " commission • gross " + Signed(d.Sum(x => x.Result + x.Fees), "USD") + ")  •  average time in the trade " + (d.Count > 0 ? Dur(TimeSpan.FromSeconds(avgS)) : "—") + "  •  winners " + (d.Any(x => x.Won) ? Dur(TimeSpan.FromSeconds(d.Where(x => x.Won).Average(x => (x.End - x.Start).TotalSeconds))) : "—") + ", losers " + (d.Any(x => !x.Won) ? Dur(TimeSpan.FromSeconds(d.Where(x => !x.Won).Average(x => (x.End - x.Start).TotalSeconds))) : "—"), Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(8, 6, 8, 6) });
                aRows.Children.Add(Row(Line, Cell("#", 36, Muted, true, 11), Cell("DATE", 84, Muted, true, 11), Cell("START → END", 150, Muted, true, 11), Cell("⏱ IN TRADE", 92, Muted, true, 11), Cell("FIRST", 64, Muted, true, 11), Cell("BUYS / SELLS / HEDGES", 150, Muted, true, 11), Cell("MAX LOTS", 74, Muted, true, 11), Cell("WORST", 90, Muted, true, 11), Cell("COMMISSION", 96, Muted, true, 11), Cell("RESULT (NET)", 110, Muted, true, 11), Cell("HOW", 120, Muted, true, 11), Cell("📈 AFTER THE TARGET (if it had stayed open)", 330, Muted, true, 11)));
                foreach (var x in e.Iterations.AsEnumerable().Reverse())
                {
                    var end = x.Done ? x.End : e.Now; var col = !x.Done ? Cyan : x.Won ? Green : Red;
                    aRows.Children.Add(Row(col, Cell("#" + x.N, 36, Text, true), Cell(x.Start.ToString("dd MMM", IC), 84, Muted), Cell(x.Start.ToString("HH:mm:ss", IC) + " → " + (x.Done ? x.End.ToString("HH:mm:ss", IC) : "open"), 150, Text),
                        Cell(Dur(end - x.Start), 92, Violet, true, 12.5, Violet), Cell(x.FirstDir, 64, x.FirstDir == "BUY" ? Green : Red, true, 12, x.FirstDir == "BUY" ? Green : Red),
                        Cell(x.Buys + " / " + x.Sells + " / " + x.AddOns + (x.ManualAdds > 0 ? " ✋" + x.ManualAdds : ""), 150, Text), Cell(x.MaxGross.ToString("0.##", IC), 74, Text), Cell(x.MaxDd.ToString("0.00", IC), 90, x.MaxDd < 0 ? Red : Muted), Cell(x.Fees > 0 ? "−" + x.Fees.ToString("0.00", IC) : "0.00", 96, x.Fees > 0 ? Amber : Muted, true),
                        Cell(x.Done ? x.Result.ToString("+0.00;−0.00", IC) : "(" + e.Basket.ToString("+0.00;−0.00", IC) + ")", 110, col, true, 14), Cell((x.Manual ? "✋ " : "") + (x.Done ? x.Reason : x.Start.Date < e.Now.Date ? "OPEN ↪ since " + x.Start.ToString("dd MMM", IC) : "OPEN"), 120, col, true, 11.5, col), Cell(AfterText(x), 330, double.IsNaN(x.AfterMax) ? Muted : x.AfterMax >= x.Result * 2 ? Gold : Cyan, true, 11.5)));
                }
                if (e.Iterations.Count == 0) aRows.Children.Add(new TextBlock { Text = "no iterations yet — START the automation", Foreground = Muted, Margin = new Thickness(10) });
            }
            else
            {
                aRows.Children.Add(Row(Line, Cell("#", 36, Muted, true, 11), Cell("DATE", 84, Muted, true, 11), Cell("TIME", 80, Muted, true, 11), Cell("SIDE", 70, Muted, true, 11), Cell("LOTS", 60, Muted, true, 11), Cell("PRICE", 100, Muted, true, 11), Cell("LEVEL", 100, Muted, true, 11), Cell("BASKET", 90, Muted, true, 11), Cell("WHY", 520, Muted, true, 11)));
                foreach (var t in e.Trades.AsEnumerable().Reverse().Take(1500))
                {
                    var col = t.Side == "BUY" ? Green : t.Side == "SELL" ? Red : Amber;
                    aRows.Children.Add(Row(col, Cell("#" + t.Iter, 36, Text, true), Cell(t.Time.ToString("dd MMM", IC), 84, Muted), Cell(t.Time.ToString("HH:mm:ss", IC), 80, Text), Cell(t.Side, 70, col, true, 12, col), Cell(t.Lots.ToString("0.##", IC), 60, Text, true),
                        Cell(Px(t.Price), 100, Text), Cell(Px(t.Level), 100, Muted), Cell(t.Basket.ToString("+0.00;−0.00", IC), 90, t.Basket >= 0 ? Green : Red), Cell(t.Why, 520, t.Why.StartsWith("✋") ? Amber : Text)));
                }
                if (e.Trades.Count == 0) aRows.Children.Add(new TextBlock { Text = "no trades yet", Foreground = Muted, Margin = new Thickness(10) });
            }
        }
        // the chart's 🎯 guides for the chosen strategy (all of them for your own settings), rebuilt every second
        void AutoGuidesBuild(string sym, List<SetupState> list)
        {
            var g = new List<ChartGuide>(); var ga = ShowEngine(); bool shadow = ga != auto; var pre = ga.Running || shadow ? ga.Set.Preset : aset.Preset; var s = ga.Running || shadow ? ga.Set : aset;
            Func<string, bool> want = k => pre == "" ? (k == "A" && s.EntryMode == "ENGULF") || (k == "B" && s.EntryMode.StartsWith("FVG")) || (k == "C" && s.EntryMode == "VWAPREJ") : pre == k || (pre == "AH" && k == "A");
            Func<string, Brush> col = k => k == "A" ? (pre == "AH" ? Violet : Green) : k == "B" ? Amber : k == "C" ? Cyan : Orange;
            if (!double.IsNaN(SetupScan.LastVwap) && (pre == "C" || s.VwapFilter != "NONE" || s.EntryMode == "VWAPREJ" || s.EntryMode == "VWAP"))   // only when the strategy USES the VWAP (its filter or its entry) g.Add(new ChartGuide { P1 = SetupScan.LastVwap, Color = Gold, Label = "VWAP " + SetupScan.LastVwap.ToString("0.##", IC) + (s.VwapFilter == "DISCOUNT" ? " — buy UNDER / sell OVER" : s.VwapFilter == "FLOW" ? " — buy OVER / sell UNDER" : "") });
            foreach (var st in list) if (want(st.Key)) foreach (var d in st.Draw)
                g.Add(new ChartGuide { Kind = d.Kind, P1 = d.P1, P2 = d.P2, From = d.From, Label = d.Label, Dash = d.Dash, Color = col(st.Key), Blink = st.Level == "STRONG" && (d.Kind != "LINE" || st.Key == "BOX" || st.Key == "B") });
            // the BREAK levels the automation enters on (and hedges on): the swing high / low, or the high / low of the last N candles
            bool inTrade = ga.Running && ga.Current != null;
            if (inTrade)
            {   // in a trade: what happens next and how far away — the take profit, the next hedge, the stop
                double bid = double.NaN; lock (broker.State.Gate) { Mt5Quote q; if (broker.State.Quotes.TryGetValue(sym, out q)) bid = q.Bid; }
                Func<double, string> away = v => double.IsNaN(bid) ? "" : " (" + Math.Abs(v - bid).ToString("0.00", IC) + " away)";
                if (!double.IsNaN(ga.TargetPx)) g.Add(new ChartGuide { P1 = ga.TargetPx, Color = Green, Blink = !double.IsNaN(bid) && Math.Abs(ga.TargetPx - bid) < 0.3, Core = true, Label = "🎯 TP +$" + s.Target.ToString("0.##", IC) + " @ " + ga.TargetPx.ToString("0.00", IC) + away(ga.TargetPx) });
                if (ga.Locked && !double.IsNaN(ga.LockHi))
                {   // 🔒 LOCKED: the range it waits to break (violet), nothing in between
                    g.Add(new ChartGuide { P1 = ga.LockHi, Color = Violet, Dash = true, Blink = !double.IsNaN(bid) && ga.LockHi - bid < 0.3, Core = true, Label = "🔒 LOCKED • UNLOCK ▲ above " + ga.LockHi.ToString("0.00", IC) + away(ga.LockHi) });
                    g.Add(new ChartGuide { P1 = ga.LockLo, Color = Violet, Dash = true, Blink = !double.IsNaN(bid) && bid - ga.LockLo < 0.3, Core = true, Label = "🔒 LOCKED • UNLOCK ▼ below " + ga.LockLo.ToString("0.00", IC) + away(ga.LockLo) });
                }
                if (s.HedgeMode != "NONE" && s.HedgeMode != "SIGNAL" && !double.IsNaN(ga.HedgeLevel) && !ga.Locked)
                {
                    double lossTo = double.IsNaN(bid) ? 0 : Math.Abs(ga.HedgeLevel - bid) * Math.Abs(ga.NetLots) * ga.PerPrice;
                    g.Add(new ChartGuide { P1 = ga.HedgeLevel, Color = Red, Dash = true, DragId = ga == auto ? "HEDGE" : "", Blink = !double.IsNaN(bid) && Math.Abs(ga.HedgeLevel - bid) < 0.3, Core = true, Label = (!double.IsNaN(ga.ManualHedge) ? "✋ " : "") + "🛡 HEDGE " + (ga.LastDir > 0 ? "SELL " : "BUY ") + (double.IsNaN(ga.NextHedgeLots) ? s.AddLots : ga.NextHedgeLots).ToString("0.##", IC) + " @ " + ga.HedgeLevel.ToString("0.00", IC) + away(ga.HedgeLevel) + (lossTo > 0 ? " ≈ −$" + lossTo.ToString("0", IC) : "") });
                }
                if (!double.IsNaN(ga.BeLockPx)) g.Add(new ChartGuide { P1 = ga.BeLockPx, Color = Gold, Dash = true, Core = true, Blink = true, Label = "🔐 BREAKEVEN LOCK " + (ga.NetLots > 0 ? "SELL " : "BUY ") + Math.Abs(ga.NetLots).ToString("0.##", IC) + " @ " + ga.BeLockPx.ToString("0.00", IC) + " (the profit is kept if the price comes back here)" });
                if (ga == auto && ga.ManualNoHedge && !double.IsNaN(ga.AutoLevelShadow)) g.Add(new ChartGuide { P1 = ga.AutoLevelShadow, Color = Muted, Dash = true, DragId = "HEDGE", Core = true, Label = "✋ NO HEDGE (you removed it) • drag = hedge there • right-click = ↺ BACK TO AUTO" });
                if (!double.IsNaN(ga.StopPx)) g.Add(new ChartGuide { P1 = ga.StopPx, Color = Red, Core = true, Label = "⛔ STOP −$" + s.MaxDrawdown.ToString("0.##", IC) + " @ " + ga.StopPx.ToString("0.00", IC) + away(ga.StopPx) });
            }
            if (inTrade && s.HedgeMode == "LADDER" && !double.IsNaN(ga.LadderHi))
            {
                double bL = double.NaN; lock (broker.State.Gate) { Mt5Quote q; if (broker.State.Quotes.TryGetValue(sym, out q)) bL = q.Bid; }
                g.Add(new ChartGuide { P1 = ga.LadderHi, Color = Green, Dash = true, Core = true, Blink = !double.IsNaN(bL) && ga.LadderHi - bL < 0.4, Label = "🪜 next BUY " + s.EntryLots.ToString("0.##", IC) + " above " + ga.LadderHi.ToString("0.00", IC) + (double.IsNaN(bL) ? "" : " (" + (ga.LadderHi - bL).ToString("0.00", IC) + " away)") });
                g.Add(new ChartGuide { P1 = ga.LadderLo, Color = Red, Dash = true, Core = true, Blink = !double.IsNaN(bL) && bL - ga.LadderLo < 0.4, Label = "🪜 next SELL " + s.EntryLots.ToString("0.##", IC) + " below " + ga.LadderLo.ToString("0.00", IC) + (double.IsNaN(bL) ? "" : " (" + (bL - ga.LadderLo).ToString("0.00", IC) + " away)") });
            }
            if (!inTrade && ga.Running && s.EntryMode == "STRADDLE" && !double.IsNaN(ga.StraddleRef))
            {
                double hs = s.LadderStep / 2; g.Add(new ChartGuide { P1 = ga.StraddleRef + hs, Color = Green, Dash = true, Core = true, Blink = true, Label = "🪜 STRADDLE BUY above " + (ga.StraddleRef + hs).ToString("0.00", IC) });
                g.Add(new ChartGuide { P1 = ga.StraddleRef - hs, Color = Red, Dash = true, Core = true, Blink = true, Label = "🪜 STRADDLE SELL below " + (ga.StraddleRef - hs).ToString("0.00", IC) });
            }
            if (ga.Running && s.EntryMode.StartsWith("FVG"))
            {   // the automation's own gaps: the zone (pulsing while the price is in it), its 50% entry, the FVG hedge level
                double b2 = double.NaN; lock (broker.State.Gate) { Mt5Quote q; if (broker.State.Quotes.TryGetValue(sym, out q)) b2 = q.Bid; }
                foreach (var z in ga.FvgZones)
                {
                    bool inside = !double.IsNaN(b2) && b2 >= z.Item2 && b2 <= z.Item3; double mid = (z.Item2 + z.Item3) / 2;
                    g.Add(new ChartGuide { Kind = "ZONE", P1 = z.Item2, P2 = z.Item3, From = z.Item4, Color = z.Item1 > 0 ? Green : Red, Blink = inside, Label = (z.Item1 > 0 ? "▲ bullish FVG" : "▼ bearish FVG") + (z.Item5 > 1 ? " ×" + z.Item5 + " stacked" : "") });
                    g.Add(new ChartGuide { P1 = mid, From = z.Item4, Color = z.Item1 > 0 ? Green : Red, Dash = true, Blink = inside, Label = (z.Item1 > 0 ? "BUY" : "SELL") + " at the 50% " + mid.ToString("0.00", IC) });
                }
            }
            if (!inTrade && ga.Running && s.EntryMode == "AGGBRK" && ga.AggDir != 0 && !double.IsNaN(ga.AggLevel))
                g.Add(new ChartGuide { P1 = ga.AggLevel, From = ga.AggBar, Color = ga.AggDir > 0 ? Green : Red, Blink = true, Core = true, Label = (ga.AggDir > 0 ? "▲ AGGRESSION BUY above " : "▼ AGGRESSION SELL below ") + ga.AggLevel.ToString("0.00", IC) + " (the " + ga.AggBar.ToString("HH:mm", IC) + " candle) — this candle only" });
            if (!inTrade && (s.EntryMode == "BREAK" || s.EntryMode == "FVGBREAK" || s.HedgeMode == "BREAK"))
            {
                DateTime ht, lt; double bid; var lv = BreakLevels(sym, s, out ht, out lt, out bid); string what = s.Swing ? "swing" : "last " + s.Lookback + "-candle";
                Func<double, bool> near = v => !double.IsNaN(bid) && Math.Abs(bid - v) <= Math.Max(0.25, Math.Abs(lv[0] - lv[1]) * 0.1);
                bool entry = s.EntryMode == "BREAK" || s.EntryMode == "FVGBREAK";
                if (!double.IsNaN(lv[0])) g.Add(new ChartGuide { P1 = lv[0], From = ht, Color = Green, Dash = !entry, Blink = near(lv[0]) || (!double.IsNaN(bid) && bid > lv[0]), Core = true, Label = (entry ? "▲ BUY above the " : "hedge BUY above the ") + what + " HIGH " + lv[0].ToString("0.##", IC) + (ht > DateTime.MinValue ? " (" + ht.ToString("HH:mm", IC) + ")" : "") });
                if (!double.IsNaN(lv[1])) g.Add(new ChartGuide { P1 = lv[1], From = lt, Color = Red, Dash = !entry, Blink = near(lv[1]) || (!double.IsNaN(bid) && bid < lv[1]), Core = true, Label = (entry ? "▼ SELL below the " : "hedge SELL below the ") + what + " LOW " + lv[1].ToString("0.##", IC) + (lt > DateTime.MinValue ? " (" + lt.ToString("HH:mm", IC) + ")" : "") });
            }
            // ⏳ the banner at the top of the chart: ALWAYS says what the automation waits for, where and how far (it blinks faster when close)
            try { var bn = AutoBanner(sym, ga, s, shadow, inTrade); if (bn != null) g.Add(bn); } catch { }
            g.RemoveAll(x => double.IsNaN(x.P1) && x.Kind != "BANNER"); if (!ga.Running) { foreach (var gx in g) if (gx.Label != "" && gx.Kind != "BANNER") gx.Label += "  (AUTO OFF)"; } else if (shadow) { foreach (var gx in g) if (gx.Label != "" && gx.Kind != "BANNER") gx.Label = "🏁 " + gx.Label; } aGuides = g; aGuideSym = sym;
        }
        int aSeenIter;
        double sn0Bid(string sym) { lock (broker.State.Gate) { Mt5Quote q; return broker.State.Quotes.TryGetValue(sym, out q) ? q.Bid : 0; } }
        // ★ SAFE HEDGE PRO, offered once per account: from the NEXT iteration (the open one keeps every setting and every record)
        void AutoOfferPro()
        {
            if (Get("auto.pro01." + AKey, "") == "1" || aset.Preset == "PRO") { Put("auto.pro01." + AKey, "1"); return; }
            Put("auto.pro01." + AKey, "1");
            var pro = AutoSettings.Model("PRO", aset); if (aset.Symbol != "") pro.Symbol = aset.Symbol;
            bool open = auto.Running && auto.Current != null;
            if (!Sx.Ask(this, "★ SAFE HEDGE PRO", "Your setup + 🛡 RISK GUARD + 🛟 DANGER RECOVERY + ⛔ HARD STOPS, every parameter set:\n\n• too big / low margin / chop → LOCK (both sides even), no more hedges\n• an OBVIOUS trend (range broken + EMA 50 / 200) → PAIR-OFF the best winner with the worst loser at ≥ $0, then lean with the trend\n• floating −" + pro.HardFloat.ToString("0", IC) + " % of the balance or margin " + pro.HardMargin.ToString("0", IC) + " % → CLOSE EVERYTHING\n\n" + (open ? "Iteration #" + auto.Current.N + " is open: it keeps its settings and all its data — PRO starts with the NEXT iteration." : auto.Running ? "It applies from the next iteration." : "It becomes your settings now (START when you are ready).") + "\n\nEvery box can be changed later (STRATEGY ▾).", "USE PRO FROM THE NEXT ITERATION", "KEEP MINE", Gold)) return;
            if (auto.Running) { auto.Queue(pro); aset = pro; Put(AKey, aset.Save()); AutoModelBoxes(); Toast("★ SAFE HEDGE PRO — from the next iteration" + (open ? " (#" + auto.Current.N + " keeps running as it is)" : "")); }
            else { aset = pro; Put(AKey, aset.Save()); AutoModelBoxes(); Toast("★ SAFE HEDGE PRO — set (START when you are ready)"); }
        }
        ChartGuide AutoBanner(string sym, HedgeAuto ga, AutoSettings s, bool shadow, bool inTrade)
        {
            double bid = double.NaN; lock (broker.State.Gate) { Mt5Quote q; if (broker.State.Quotes.TryGetValue(sym, out q)) bid = q.Bid; }
            Func<double, string> P2 = v => v.ToString("0.00", IC); Func<double, string> dist = v => double.IsNaN(bid) ? "" : " (" + Math.Abs(v - bid).ToString("0.00", IC) + " away)";
            string who = shadow ? "🏁 RACE LANE " + raceShow + " (tap 🤖 MINE above the chart for yours)  •  " : "🤖 ";
            string txt; Brush col = Cyan; bool fast = false;
            AutoTrade last = ga.Trades.Count > 0 ? ga.Trades[ga.Trades.Count - 1] : null; bool fresh = last != null && inTrade && (ga.Now - last.Time).TotalSeconds >= 0 && (ga.Now - last.Time).TotalSeconds < 90;
            if (fresh)
            {   // ⚡ for 90 s after every order: WHAT it did, WHEN and WHY
                txt = who + "⚡ " + last.Side + " " + last.Lots.ToString("0.##", IC) + " @ " + P2(last.Price) + " at " + last.Time.ToString("HH:mm:ss", IC) + " — " + last.Why + "  •  basket " + ga.Basket.ToString("+0.00;−0.00", IC)
                    + (s.HedgeMode != "NONE" && s.HedgeMode != "SIGNAL" && !double.IsNaN(ga.HedgeLevel) && !ga.Locked ? "  •  🛡 hedge " + (ga.LastDir > 0 ? "SELL" : "BUY") + " " + P2(ga.HedgeLevel) + dist(ga.HedgeLevel) : "");
                col = last.Side == "BUY" ? Green : last.Side == "SELL" ? Red : Amber; fast = true;
            }
            else if (inTrade)
            {
                txt = who + "IN ITERATION #" + (ga.Current != null ? ga.Current.N.ToString(IC) : "?") + "  basket " + ga.Basket.ToString("+0.00;−0.00", IC);
                if (!double.IsNaN(ga.TargetPx)) { txt += "  •  🎯 TP " + P2(ga.TargetPx) + dist(ga.TargetPx); fast |= !double.IsNaN(bid) && Math.Abs(ga.TargetPx - bid) < 0.5; }
                if (s.HedgeMode == "LADDER" && !double.IsNaN(ga.LadderHi)) { txt += "  •  🪜 next BUY > " + P2(ga.LadderHi) + dist(ga.LadderHi) + " • next SELL < " + P2(ga.LadderLo) + dist(ga.LadderLo); fast |= !double.IsNaN(bid) && (ga.LadderHi - bid < 0.4 || bid - ga.LadderLo < 0.4); }
                else if (ga.Locked && !double.IsNaN(ga.LockHi)) txt += "  •  🔒 LOCKED " + P2(ga.LockLo) + " – " + P2(ga.LockHi);
                else if (s.HedgeMode != "NONE" && s.HedgeMode != "SIGNAL" && !double.IsNaN(ga.HedgeLevel)) { txt += "  •  🛡 next hedge " + (ga.LastDir > 0 ? "SELL " : "BUY ") + (double.IsNaN(ga.NextHedgeLots) ? "" : ga.NextHedgeLots.ToString("0.##", IC) + " ") + "@ " + P2(ga.HedgeLevel) + dist(ga.HedgeLevel); fast |= !double.IsNaN(bid) && Math.Abs(ga.HedgeLevel - bid) < 0.5; }
                col = ga.Basket >= 0 ? Green : Amber;
                if (ga.Danger > 0 || ga.DangerWhy != "")
                {   // 🚨 the danger meter, always in the trade's banner
                    string lvl = ga.Danger >= 60 ? "🔴 DANGER " : ga.Danger >= 35 ? "🟡 CAUTION " : "🟢 CALM ";
                    txt += "  •  " + lvl + ga.Danger + (ga.Danger >= 35 && ga.DangerWhy != "" && ga.DangerWhy != "calm" ? " (" + ga.DangerWhy + ")" : "") + (ga.Danger >= 35 && ga.Similar != "" ? "  •  📚 " + ga.Similar : "")
                        + (ga.DangerPeak > ga.Danger + 9 ? "  •  peak " + ga.DangerPeak : "") + (s.DangerExit && ga.DangerPeak >= s.DangerAt ? "  •  🚨 in danger: closes at +$" + Math.Max(0.5, s.Target * s.DangerKeep / 100).ToString("0.##", IC) : "");
                    if (ga.Danger >= 60) { col = Red; fast = true; }
                }
            }
            else if (ga.Running && ga.Phase == "PAUSE") { txt = who + "⏸ PAUSE — the next iteration from " + ga.PauseUntil.ToString("HH:mm:ss", IC); col = Muted; }
            else if (ga.Running && ga.Blocked != "") { txt = who + "⏸ NOT TRADING NOW: " + ga.Blocked; col = Muted; }
            else if (s.EntryMode == "STRADDLE" && ga.Running && !double.IsNaN(ga.StraddleRef)) { txt = who + "⏳ 🪜 STRADDLE   ▲ BUY above " + P2(ga.StraddleRef + s.LadderStep / 2) + dist(ga.StraddleRef + s.LadderStep / 2) + "   •   ▼ SELL below " + P2(ga.StraddleRef - s.LadderStep / 2) + dist(ga.StraddleRef - s.LadderStep / 2); col = Gold; fast = true; }
            else if (s.EntryMode == "BREAK" || s.EntryMode == "FVGBREAK")
            {
                double hi, lo; DateTime ht, lt; double b0; var lv = BreakLevels(sym, s, out ht, out lt, out b0);
                hi = ga.Running && !double.IsNaN(ga.BuyLevel) ? ga.BuyLevel : lv[0]; lo = ga.Running && !double.IsNaN(ga.SellLevel) ? ga.SellLevel : lv[1];
                string what = s.Swing ? "swing" : s.Lookback + "-candle";
                txt = who + (ga.Running ? "⏳ WAITING FOR A BREAK" : "AUTO OFF — it would enter") + "   ▲ BUY above " + (double.IsNaN(hi) ? "(no " + what + " high yet — one is forming)" : P2(hi) + dist(hi)) + "   •   ▼ SELL below " + (double.IsNaN(lo) ? "(no " + what + " low yet — one is forming)" : P2(lo) + dist(lo));
                double near = Math.Min(double.IsNaN(hi) || double.IsNaN(bid) ? 99 : Math.Abs(hi - bid), double.IsNaN(lo) || double.IsNaN(bid) ? 99 : Math.Abs(bid - lo));
                fast = near < 0.5; col = ga.Running ? Gold : Muted;
            }
            else txt = who + (ga.Running ? "⏳ WAITING — " + ga.Phase + (ga.SignalText != "" ? ": " + ga.SignalText : "") : "AUTO OFF");
            if (s.Smart && !inTrade && ga.SmartState != "") txt += "  •  " + ga.SmartState + (ga.SmartSkips > 0 ? " • " + ga.SmartSkips + " risky entries skipped" : "");
            if (!shadow && ga.ManualManage)
            {   // ✋ MANUAL MANAGING: AUTO only suggests — the latest suggestion first, in violet, blinking
                bool sNew = ga.Suggestion != "" && (DateTime.Now - ga.SuggestAt).TotalSeconds < 120;
                txt = "✋ MANUAL MANAGING — nothing is sent by AUTO" + (sNew ? "  •  💡 SUGGEST: " + ga.Suggestion + " (" + ga.SuggestAt.ToString("HH:mm:ss", IC) + ")" : "") + "  •  " + txt.Replace("🤖 ", "");
                col = Violet; fast = sNew;
            }
            return new ChartGuide { Kind = "BANNER", Core = true, P1 = double.NaN, Label = txt, Color = col, Blink = ga.Running, Dash = fast };   // Dash on a banner = blink FAST (close to the level)
        }
        // the same levels as the engine (HedgeAuto.Pivot / Lookback): from the finished candles of the strategy's timeframe, newest first
        double[] BreakLevels(string sym, AutoSettings s, out DateTime hiT, out DateTime loT, out double bid)
        {
            hiT = loT = DateTime.MinValue; bid = double.NaN; var f = new List<Mt5Bar>(); double formH = double.NaN, formL = double.NaN;
            lock (broker.State.Gate)
            {
                var st = broker.State; var now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; Mt5Quote q; if (st.Quotes.TryGetValue(sym, out q)) bid = q.Bid;
                var se = st.SeriesOf(sym, s.Tf); for (int i = se.Bars.Count - 1; i >= 0 && f.Count < Math.Max(60, s.Lookback); i--) if (se.Bars[i].Time.AddMinutes(s.Tf) <= now) f.Add(se.Bars[i]);
                if (se.Bars.Count > 0 && se.Bars[se.Bars.Count - 1].Time.AddMinutes(s.Tf) > now) { var fb = se.Bars[se.Bars.Count - 1]; formH = fb.H; formL = fb.L; }   // the forming candle: a level it already went through is used up on the chart at once
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
            if (s.Swing && !double.IsNaN(hi) && !double.IsNaN(formH) && formH > hi) { hi = double.NaN; hiT = DateTime.MinValue; }
            if (s.Swing && !double.IsNaN(lo) && !double.IsNaN(formL) && formL < lo) { lo = double.NaN; loT = DateTime.MinValue; }
            return new[] { hi, lo };
        }
        double ANum(string k, double def) { double x; return double.TryParse((af[k].Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, IC, out x) ? x : def; }
        AutoSettings AutoRead()
        {
            var s = aset.Copy(); s.Symbol = (af["sym"].Text ?? "").Trim(); s.Lookback = (int)Math.Max(1, ANum("look", 1)); s.EntryLots = ANum("entry", 0); s.AddLots = ANum("add", 0); s.Target = ANum("target", 0);
            s.MaxDrawdown = Math.Max(0, ANum("dd", 0)); s.HedgePrice = ANum("hprice", 0); s.HedgeMoney = ANum("hmoney", 0); s.HedgeMult = Math.Max(0.1, ANum("hmult", 2)); s.AggRun = (int)Math.Max(1, ANum("aggrun", 4)); s.RecoverMove = Math.Max(0.01, ANum("rmove", 3)); s.NetAdvantage = Math.Max(0.01, ANum("nadv", 0.2)); s.HedgeMaxLots = Math.Max(0.01, ANum("hmax", 1)); s.MaxHedges = (int)Math.Max(1, ANum("maxh", 1)); s.PauseSec = (int)Math.Max(0, ANum("pause", 60)); s.MaxIterations = (int)Math.Max(0, ANum("maxit", 0)); s.CommissionPerLot = Math.Max(0, ANum("comm", 0));
            s.Windows = (af["win"].Text ?? "").Trim(); s.CloseAtNy = (af["eod"].Text ?? "").Trim(); s.FvgAge = (int)Math.Max(0, ANum("fage", 0)); s.NyOffsetHours = (int)ANum("nyoff", -7); s.PerDay = (int)Math.Max(0, ANum("perday", 0)); s.NewsBefore = (int)Math.Max(0, ANum("nb", 15)); s.NewsAfter = (int)Math.Max(0, ANum("na", 15));
            s.BeAfter = (int)Math.Max(0, ANum("beafter", 0)); s.QuickAfter = Math.Max(0.1, ANum("qafter", 3)); s.ProtectAt = Math.Max(0, Math.Min(99, ANum("pat", 0))); s.GuardLots = Math.Max(0.01, ANum("glots", 1)); s.KeyBuffer = Math.Max(0.1, Math.Min(3, ANum("kbuf", 0.5))); s.BeLockAt = Math.Max(10, Math.Min(99, ANum("belat", 80))); s.BeLockKeep = Math.Max(0, Math.Min(90, ANum("belkeep", 10))); s.RecDeep = Math.Max(0.5, ANum("rdeep", 2)); s.RecKeep = Math.Max(0, Math.Min(100, ANum("rkeep", 20))); s.MaxDdPct = Math.Max(0, Math.Min(100, ANum("ddpct", 0))); s.DayLoss = Math.Max(0, ANum("dayloss", 0)); s.DayLossPct = Math.Max(0, Math.Min(100, ANum("daypct", 0))); { double dA = ANum("dat", 0); s.DangerExit = dA > 0; if (dA > 0) s.DangerAt = Math.Max(10, Math.Min(100, dA)); s.DangerKeep = Math.Max(0, Math.Min(100, ANum("dkeep", 25))); } s.GuardBreak = Math.Max(0, ANum("gbreak", 1)); s.ScaleLots = Math.Max(0.01, ANum("slots", 0.05)); s.ScaleStep = Math.Max(0.1, ANum("sstep", 1.5)); s.HardFloat = Math.Max(0, ANum("hfloat", 0)); s.HardMargin = Math.Max(0, ANum("hmargin", 0)); s.GuardMargin = Math.Max(0, ANum("gmargin", 500)); s.GuardChop = (int)Math.Max(0, ANum("gchop", 3)); s.GuardMaxLead = (int)Math.Max(1, ANum("glead", 3)); s.ProtectKeep = Math.Max(0, Math.Min(95, ANum("pkeep", 10))); s.HedgeCap = Math.Max(0, ANum("hcap", 5)); s.LadderStep = Math.Max(0.1, ANum("lstep", 2.5)); s.LadderMax = (int)Math.Max(0, ANum("lmax", 10)); s.LockAfter = (int)Math.Max(1, ANum("lafter", 2)); s.LockRange = Math.Max(0.01, ANum("lrange", 3));
            s.WinnerLots = Math.Max(0, ANum("wlots", 0.1)); s.WinnerLotsSell = Math.Max(0, ANum("wlots2", 0.1)); s.SwingK = (int)Math.Max(1, ANum("swk", 2)); s.MaxWinnerAdds = (int)Math.Max(0, ANum("wmax", 0)); s.FlowVol = Math.Max(1, ANum("fvol", 1.5)); s.FlowShare = Math.Max(0.5, Math.Min(1, ANum("fshare", 0.7)));
            return s;
        }
        // the automation's settings: per MT5 account on LIVE / PROP (each account its own), per mode otherwise
        // each broker's own key — also for its DEMO (two brokers' DEMO windows never share the settings or the "it was running" flag)
        string AKey { get { return "auto." + Mode + (!Sim && live != null && live.Key != "" ? "." + live.Key : ""); } }
        // the "it was running" flag of THIS window (a DEMO saved by an older build: the shared flag counts only for the broker it ran on)
        bool AutoWasRunning() { if (Get("auto.run." + AKey, "") == "1") return true; return Paper && Get("auto.run." + AKey, "") == "" && Get("auto.run.auto.DEMO", "0") == "1" && live != null && Get("auto.run.demolink", "") == live.Key; }
        void AutoSaveSet() { aset = AutoRead(); Put(AKey, aset.Save()); }
        // only the boxes the chosen options use: ONE add-on trigger (BREAK / PRICE / $), the pause, the FVG timeframe, the flow numbers, the news minutes, the winner lots
        void AutoVis()
        {
            Action<string, bool> v = (k, on) => { FrameworkElement e; if (afBox.TryGetValue(k, out e)) e.Visibility = on ? Visibility.Visible : Visibility.Collapsed; };
            v("hprice", aset.HedgeMode == "PRICE"); v("hmoney", aset.HedgeMode == "MONEY" || aset.HedgeMode == "SIGNAL"); v("hmult", aset.HedgeMode == "SIGNAL"); v("maxh", aset.HedgeMode == "SIGNAL"); v("roll", aset.HedgeMode == "BREAK");
            v("pause", aset.NextStart == "PAUSE"); v("fvgtf", aset.EntryMode.StartsWith("FVG")); v("fvol", aset.EntryMode == "FLOW"); v("fshare", aset.EntryMode == "FLOW");
            v("nb", aset.NewsMode != "IGNORE"); v("na", aset.NewsMode != "IGNORE"); foreach (var k in new[] { "wlots", "wlots2", "wmax" }) v(k, aset.AddToWinner);
            v("lafter", aset.LockOn); v("lrange", aset.LockOn); v("unlock", aset.LockOn);
            v("look", !aset.Swing); v("swk", aset.Swing); bool hedge = aset.HedgeMode != "NONE" && aset.HedgeMode != "SIGNAL"; v("add", hedge); v("roll", hedge && aset.HedgeMode == "BREAK"); v("fage", aset.EntryMode.StartsWith("FVG")); v("aggrun", aset.EntryMode == "AGGBRK"); bool hb = aset.HedgeMode == "BREAK" || aset.HedgeMode == "PRICE" || aset.HedgeMode == "MONEY" || aset.HedgeMode == "FVG"; v("hsize", hb); v("rmove", hb && aset.HedgeSize == "RECOVER"); v("lstep", aset.HedgeMode == "LADDER" || aset.EntryMode == "STRADDLE"); v("lmax", aset.HedgeMode == "LADDER"); v("nadv", hb && (aset.HedgeSize == "ADVANTAGE" || aset.HedgeSize == "STEPLEAD")); v("add", aset.HedgeMode != "NONE" && aset.HedgeMode != "SIGNAL" && aset.HedgeSize == "FIXED"); v("hmax", hb && aset.HedgeSize == "RECOVER"); v("fstack", aset.EntryMode.StartsWith("FVG")); v("fstrict", aset.EntryMode.StartsWith("FVG"));
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

        // ⟳ AUTO-RESUME after a restart (user's request: a new SpasiX version must not leave the hedges unmanaged): the automation was
        // running when SpasiX closed → as soon as the account, its prices and its candles are back, it starts again with the same settings and
        // takes the open positions over. DEMO: by itself. A REAL / PROP account: ONE window (ARM + resume in one click; a REAL account types
        // RESUME) — real orders never start without the user.
        void AutoTryResume()
        {
            if (auto.Running) { aResumePending = false; return; }
            if ((DateTime.Now - aResumeAt).TotalMinutes > 5) { aResumePending = false; Log("⟳ the automation was running before SpasiX closed — the account / prices did not come back in 5 minutes: press ⟳ RESUME in 🤖 AUTO", Amber); return; }
            string sym = aset.Symbol; bool ready; int open; bool can; string kind; long login;
            lock (broker.State.Gate)
            {
                Mt5Quote q; ready = broker.State.Linked && broker.State.Quotes.TryGetValue(sym, out q) && q.Bid > 0 && broker.State.SeriesOf(sym, aset.Tf).Bars.Count >= 20;
                open = broker.State.Positions.Count(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)); can = broker.State.CanTrade; kind = broker.State.Account.Kind; login = broker.State.Account.Login;
            }
            if (Paper && !paper.Ready) return;
            if (!ready) return;
            if (!Virtual && !can) return;   // MT5's Algo Trading not on yet
            aResumePending = false;
            if (!Virtual)
            {
                if (!IsVisible) Shell.Switch(this);   // the question shows on this account's own tab (a window never shown cannot own a dialog)
                string msg = "The 🤖 automation was RUNNING when SpasiX closed" + (open > 0 ? " — " + open + " position" + (open == 1 ? "" : "s") + " open on " + sym + " (nobody manages them until it runs)" : "") + ".\n\n⟳ RESUME = ARM this account and start the automation again with the same settings; it takes the open positions over.";
                if (kind == "REAL") { var typed = Sx.Prompt(this, "⟳ RESUME on a REAL account (" + login + ")", msg + "\n\nType RESUME to go on.", ""); if (typed == null || typed.Trim().ToUpperInvariant() != "RESUME") { Log("⟳ resume declined — press ⟳ RESUME in 🤖 AUTO when ready", Amber); return; } }
                else if (!Sx.Ask(this, "⟳ RESUME the automation (" + kind + " " + login + ")", msg, "⟳ RESUME", "NOT NOW", Green)) { Log("⟳ resume declined — press ⟳ RESUME in 🤖 AUTO when ready", Amber); return; }
                if (!armed) SetArmed(true, null);
            }
            AutoSaveSet(); var s = aset.Copy(); var err = auto.Check(broker, s); if (err != "") { Log("⟳ could not resume: " + err, Red); return; }
            AutoLoadHistory(); aView = "STORY"; auto.Start(broker, s, open > 0); Put("auto.run." + AKey, "1"); if (Paper && live != null) Put("auto.run.demolink", live.Key); aWasOn = true; AutoPaint(true);
            Log("⟳ AUTO RESUMED after the restart" + (open > 0 ? " — " + open + " open position" + (open == 1 ? "" : "s") + " taken over" : ""), Green); Toast("⟳ the automation resumed by itself" + (open > 0 ? " (" + open + " positions taken over)" : ""));
        }
        // a DEMO made before the % commission existed charges nothing: on Duramarkets it gets its 0.001 % of each deal by itself (once; the
        // DEMO ACCOUNT box can still change it)
        // the DEMO's margin for hedged pairs (user's request: realistic, margin calls included): when MT5 says a covered pair needs NO margin,
        // the DEMO asks once to use 50 % (both legs at half margin) — the DEMO ACCOUNT box can still set 100 % or MT5's value (−1)
        bool aHedgedAsked;
        void AutoDemoHedgedMargin()
        {
            if (aHedgedAsked || !paper.Ready || !IsVisible || live == null || live.Key == "") return; aHedgedAsked = true;
            if (paper.Acct.Set.HedgedPct >= 0 || Get("demo.hpct.asked." + live.Key, "") == "1") return;
            double ratio = -1; lock (live.State.Gate) { Mt5Symbol xs; if (live.State.Symbols.TryGetValue("XAUUSD", out xs) && xs.MarginHedged >= 0) ratio = xs.MarginInitial > 0 && xs.MarginPerLot > 0 ? xs.MarginHedged / xs.MarginPerLot : xs.Contract > 0 ? xs.MarginHedged / xs.Contract : -1; }
            if (ratio > 0.01) return;   // MT5 already charges hedged pairs: its own number stays
            Put("demo.hpct.asked." + live.Key, "1");
            double eq, mg50; lock (paper.Acct.State.Gate) { eq = paper.Acct.State.Account.Equity; paper.Acct.Set.HedgedPct = 50; paper.Acct.Mark(); mg50 = paper.Acct.State.Account.Margin; paper.Acct.Set.HedgedPct = -1; paper.Acct.Mark(); }
            double ml = mg50 > 0 ? eq / mg50 * 100 : 0;
            if (!Sx.Ask(this, "⚖ Realistic margin for hedged positions", "This broker's MT5 says a buy + a sell of the same size need NO margin, so this DEMO's margin level looks very high.\n\nUse 50 % margin for hedged pairs instead (a realistic, stricter rule)?" + (mg50 > 0 ? "\n\nWith the positions open now: margin $" + mg50.ToString("N0", IC) + ", margin level " + ml.ToString("0", IC) + " %" + (ml < paper.Acct.Set.StopOutPct ? " — UNDER the " + paper.Acct.Set.StopOutPct.ToString("0", IC) + " % stop-out: the biggest losing positions close at once until it is above again (a margin call, as at a real broker)." : ".") : ""), "⚖ USE 50 %", "KEEP MT5'S", Amber)) { Log("⚖ DEMO margin: MT5's own hedged margin kept (0 %) — DEMO ACCOUNT ▸ HEDGED PAIR MARGIN % changes it", Muted); return; }
            lock (paper.Acct.State.Gate) { paper.Acct.Set.HedgedPct = 50; paper.Acct.Mark(); } Put("demo.hpct", "50"); try { paper.Save(); } catch { }
            Log("⚖ DEMO margin: hedged pairs now need 50 % margin (stop-out under " + paper.Acct.Set.StopOutPct.ToString("0", IC) + " %)", Amber);
        }
        void AutoDemoCommission()
        {
            string srv; lock (live.State.Gate) srv = live.State.Account.Server ?? ""; if (srv == "" || !paper.Ready) return; aPctChecked = true;
            if (srv.IndexOf("dura", StringComparison.OrdinalIgnoreCase) < 0 || paper.Acct.Set.CommissionPct > 0 || Get("demo.pct.auto." + live.Key, "") == "1") return;
            paper.Acct.Set.CommissionPct = 0.001; Put("demo.pct.auto." + live.Key, "1"); Put("demo.pct", "0.001"); try { paper.Save(); } catch { }
            Log("💲 DEMO: Duramarkets' commission is now charged — 0.001 % of each deal, in and out (gold 0.1 lot ≈ $0.40 a side)", Amber); Toast("💲 DEMO now charges Duramarkets' commission (0.001 % per deal)");
        }
        // 🗄 the 1-minute candles of the strategy's symbol, saved per day (DataDir\auto\candles\SYMBOL-yyyy-MM-dd.csv) every 10 minutes —
        // every traded day can be studied / replayed later, even when nothing was exported
        void AutoArchiveCandles()
        {
            try
            {
                string sym = (auto.Running ? auto.Set.Symbol : aset.Symbol); if (string.IsNullOrEmpty(sym)) return; List<Mt5Bar> bars;
                lock (broker.State.Gate) bars = broker.State.SeriesOf(sym, 1).Bars.Select(b => b.Copy()).ToList(); if (bars.Count == 0) return;
                var dir = Path.Combine(Dir, "auto", "candles"); Directory.CreateDirectory(dir);
                foreach (var g in bars.GroupBy(b => b.Time.Date))
                {
                    var f = Path.Combine(dir, sym + "-" + g.Key.ToString("yyyy-MM-dd", IC) + ".csv"); var have = new Dictionary<DateTime, string>();
                    if (File.Exists(f)) foreach (var l in File.ReadAllLines(f).Skip(1)) { var a = l.Split(','); DateTime t; if (a.Length >= 7 && DateTime.TryParseExact(a[0], "yyyy-MM-dd HH:mm", IC, DateTimeStyles.None, out t)) have[t] = l; }
                    foreach (var b in g) have[b.Time] = string.Join(",", b.Time.ToString("yyyy-MM-dd HH:mm", IC), b.O.ToString("R", IC), b.H.ToString("R", IC), b.L.ToString("R", IC), b.C.ToString("R", IC), b.V.ToString(IC), b.Spread.ToString(IC));
                    File.WriteAllLines(f, new[] { "time,open,high,low,close,tick_volume,spread_points" }.Concat(have.OrderBy(kv => kv.Key).Select(kv => kv.Value)));
                }
            }
            catch { }
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
            AutoLoadHistory(); if (aView == "⚙ SETTINGS") aView = "STORY"; auto.Start(broker, s, adopt); Put("auto.run." + AKey, "1"); if (Paper && live != null) Put("auto.run.demolink", live.Key); aWasOn = true; AutoPaint(true);
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
            if (!Sx.Ask(this, "🗑 Clear the history", "Every iteration, trade, story and log line of the automation in this " + Mode + " window is cleared — a fresh start.\n\nThe run so far is KEPT as a test in 🗂 TESTS (its settings, iterations, trades, story, hedges …), so it can be compared and exported later.", "CLEAR (keep it in 🗂 TESTS)", "CANCEL", Red)) return;
            var arch = AutoArchive(); auto.Iterations.Clear(); auto.Trades.Clear(); auto.Log.Clear(); auto.Story.Clear(); auto.Hedges.Clear(); aSaved = 0; auto.Version++;
            if (aHist != null && aHist != "") { foreach (var w in new[] { "iterations", "trades", "story", "log", "hedges", "after", "paths", "open" }) try { File.Delete(AutoFile(w)); } catch { } }
            if (arch != "") Toast("🗂 the run is kept in TESTS — the history starts fresh"); AutoPaint(true);
        }

        // 🗂 TESTS: CLEAR HISTORY keeps the run so far in DataDir\auto\tests\<account>\<from → to>\ (its files + settings + a summary)
        string AutoTestsDir() { return Path.Combine(Dir, "auto", "tests", Mode + "-" + (string.IsNullOrEmpty(aHist) ? "account" : aHist)); }
        string AutoArchive()
        {
            try
            {
                var its = auto.Iterations.ToList(); if (its.Count == 0 && auto.Story.Count == 0) return "";
                var from = its.Count > 0 ? its.Min(x => x.Start) : DateTime.Now; var to = its.Count > 0 ? its.Max(x => x.Done ? x.End : x.Start) : DateTime.Now;
                var d = Path.Combine(AutoTestsDir(), from.ToString("yyyy-MM-dd_HHmm", IC) + " to " + to.ToString("yyyy-MM-dd_HHmm", IC)); Directory.CreateDirectory(d);
                if (!string.IsNullOrEmpty(aHist)) foreach (var w in new[] { "iterations", "trades", "story", "log", "hedges", "after", "paths" }) { var f = AutoFile(w); if (File.Exists(f)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true); }
                var st = auto.Calc(its); var dn = its.Where(x => x.Done).ToList();
                File.WriteAllLines(Path.Combine(d, "summary.txt"), new[] { "from=" + from.Ticks, "to=" + to.Ticks, "iterations=" + st.Done, "won=" + st.Wins, "lost=" + st.Losses, "net=" + st.Net.ToString("0.##", IC), "fees=" + its.Sum(x => x.Fees).ToString("0.##", IC), "worstdd=" + st.WorstDd.ToString("0.##", IC), "mostlots=" + (its.Count > 0 ? its.Max(x => x.MaxGross) : 0).ToString("0.##", IC), "open=" + (its.Any(x => !x.Done) ? "1" : "0"), "settings=" + (auto.Running ? auto.Set : aset).Save() });
                return d;
            }
            catch { return ""; }
        }
        void AutoTestsRows()
        {
            aRows.Children.Add(new TextBlock { Text = "🗂 TESTS — every run you cleared with 🗑 CLEAR HISTORY, kept with its settings and all its data (newest first)", Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(8, 6, 8, 8) });
            var dirs = Directory.Exists(AutoTestsDir()) ? Directory.GetDirectories(AutoTestsDir()).OrderByDescending(x => x).ToList() : new List<string>();
            if (dirs.Count == 0) { aRows.Children.Add(new TextBlock { Text = "no saved test yet — a run is kept here when you press 🗑 CLEAR HISTORY", Foreground = Muted, Margin = new Thickness(10) }); return; }
            foreach (var d in dirs)
            {
                var kv = new Dictionary<string, string>(); try { foreach (var l in File.ReadAllLines(Path.Combine(d, "summary.txt"))) { int i = l.IndexOf('='); if (i > 0) kv[l.Substring(0, i)] = l.Substring(i + 1); } } catch { }
                Func<string, string> g = k => kv.ContainsKey(k) ? kv[k] : ""; Func<string, double> gn = k => { double v; return double.TryParse(g(k), NumberStyles.Float, IC, out v) ? v : 0; };
                long ft, tt; long.TryParse(g("from"), out ft); long.TryParse(g("to"), out tt); var set = AutoSettings.Load(g("settings")); double net = gn("net");
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = (ft > 0 ? new DateTime(ft).ToString("ddd dd MMM HH:mm", IC) : "?") + "  →  " + (tt > 0 ? new DateTime(tt).ToString("ddd dd MMM HH:mm", IC) : "?") + "   •   " + g("iterations") + " iterations (won " + g("won") + ", lost " + g("lost") + ")" + (g("open") == "1" ? " + 1 open" : ""), Foreground = Text, FontWeight = FontWeights.Bold, FontSize = 13 });
                sp.Children.Add(new TextBlock { Text = "net " + Signed(net, "USD") + "  •  commissions −$" + gn("fees").ToString("0.00", IC) + "  •  worst drawdown " + gn("worstdd").ToString("0.00", IC) + "  •  most lots " + g("mostlots"), Foreground = net >= 0 ? Green : Red, FontWeight = FontWeights.Bold, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 2) });
                sp.Children.Add(new TextBlock { Text = "settings: " + set.EntryMode + " " + Tf.Name(set.Tf) + " • entry " + set.EntryLots.ToString("0.##", IC) + " • lead " + set.NetAdvantage.ToString("0.##", IC) + " • target +$" + set.Target.ToString("0.##", IC) + " • range-only " + (set.RangeOnly ? "ON" : "off") + " • rescue " + (set.BeAfter > 0 ? "after " + set.BeAfter : "off") + " • lock " + (set.LockOn ? "ON" : "off"), Foreground = Muted, FontSize = 11.5 });
                var bw = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) }; var dd = d;
                var ob = Pill("📂 OPEN FOLDER", Card); ob.Height = 26; ob.Click += delegate { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + dd + "\"") { UseShellExecute = true }); } catch { } }; bw.Children.Add(ob);
                var zb = Pill("📦 ZIP FOR CLAUDE", Tint(Violet, 90)); zb.Height = 26; zb.Click += delegate { try { var z = dd + ".zip"; if (File.Exists(z)) File.Delete(z); System.IO.Compression.ZipFile.CreateFromDirectory(dd, z); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + z + "\"") { UseShellExecute = true }); Toast("📦 " + Path.GetFileName(z)); } catch (Exception ex) { Toast("📦 " + ex.Message); } }; bw.Children.Add(zb);
                var ub = Pill("↺ USE THESE SETTINGS", Card); ub.Height = 26; ub.Click += delegate { if (auto.Running) { Toast("stop the automation first (or change the settings and ⚡ APPLY NOW)"); return; } aset = set; AutoModelBoxes(); AutoSaveSet(); Toast("↺ the boxes show this test's settings"); }; bw.Children.Add(ub);
                sp.Children.Add(bw);
                aRows.Children.Add(new Border { Background = Sx.CardGrad, BorderBrush = net >= 0 ? Green : Red, BorderThickness = new Thickness(5, 1, 1, 1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(6, 0, 6, 6), Child = sp });
            }
        }
        // ---- history on this PC (LIVE / PROP / DEMO, per account): every finished iteration (with its context), its trades, the STORY and the LOG as they happen
        string AutoId() { if (Sim) return ""; var k = live != null ? live.Key : ""; if (k == "") { long lg; lock (broker.State.Gate) lg = broker.State.Account.Login; k = lg > 0 ? lg.ToString(IC) : ""; } foreach (var c in Path.GetInvalidFileNameChars()) k = k.Replace(c, '_'); return k; }
        // 📦 EXPORT FOR CLAUDE: everything this account's automation knows, organized, in DataDir\export\<name>\ + <name>.zip (files only, no orders)
        void AutoExport()
        {
            try
            {
                if (aHist == null && !Sim) AutoLoadHistory();
                var now = DateTime.Now; var bad = Path.GetInvalidFileNameChars();
                var id = new string((string.IsNullOrEmpty(aHist) ? (Sim ? "backtest" : "account") : aHist).Select(c => bad.Contains(c) || c == '@' || c == ' ' ? '_' : c).ToArray());
                var name = "SpasiX-export-" + Mode + "-" + id + "-" + now.ToString("yyyy-MM-dd_HHmm", IC);
                var root = Path.Combine(Dir, "export"); var dir = Path.Combine(root, name); Directory.CreateDirectory(dir);
                var s = auto.Running ? auto.Set : AutoRead(); var its = auto.Iterations.ToList(); var trs = auto.Trades.ToList(); var st = auto.Calc(its);
                Func<DateTime, string> T = d => d == DateTime.MinValue ? "" : d.ToString("yyyy-MM-dd HH:mm:ss", IC);
                Func<string, string> Q = x => "\"" + (x ?? "").Replace("\"", "'") + "\"";
                Func<double, string> N = v => double.IsNaN(v) ? "" : v.ToString("0.#####", IC);
                var files = new List<string>();
                Action<string, IEnumerable<string>> W = (f, lines) => { File.WriteAllLines(Path.Combine(dir, f), lines); files.Add(f); };

                // 01 settings: one key per line
                W("01-settings.txt", new[] { "THE STRATEGY'S SETTINGS (" + (auto.Running ? "running now" : "in the boxes, not running") + ")", "preset = " + s.Preset, "" }.Concat(s.Save().Split(';')));

                // 02 iterations: readable times, the context split into columns
                var ctxKeys = its.SelectMany(x => (x.Ctx ?? "").Split(';').Where(kv => kv.Contains("=")).Select(kv => kv.Split('=')[0])).Distinct().ToList();
                var rows = new List<string> { "n,start,end,seconds,first,buys,sells,addons,winner_adds,manual_adds,buy_lots,sell_lots,max_lots,max_drawdown,result,reason,balance_start,balance_end,manual,adopted,done,commission,after_best,after_best_min,after_back_to_target_min,after_breakeven_min,after_move" + string.Concat(ctxKeys.Select(k => ",ctx_" + k)) };
                foreach (var x in its)
                {
                    var ctx = (x.Ctx ?? "").Split(';').Where(kv => kv.Contains("=")).GroupBy(kv => kv.Split('=')[0]).ToDictionary(g => g.Key, g => g.First().Substring(g.Key.Length + 1));
                    string cv; rows.Add(string.Join(",", x.N.ToString(IC), T(x.Start), x.Done ? T(x.End) : "", x.Done ? ((int)(x.End - x.Start).TotalSeconds).ToString(IC) : "", x.FirstDir, x.Buys.ToString(IC), x.Sells.ToString(IC), x.AddOns.ToString(IC), x.WinnerAdds.ToString(IC), x.ManualAdds.ToString(IC), N(x.BuyLots), N(x.SellLots), N(x.MaxGross), N(x.MaxDd), x.Done ? N(x.Result) : "", Q(x.Reason), N(x.BalanceStart), N(x.BalanceEnd), x.Manual ? "1" : "0", x.Adopted ? "1" : "0", x.Done ? "1" : "0", N(x.Fees), N(x.AfterMax), x.AfterMaxMin.ToString("0.#", IC), x.AfterBackTargetMin.ToString("0.#", IC), x.AfterBreakevenMin.ToString("0.#", IC), N(x.AfterMove)) + string.Concat(ctxKeys.Select(k => "," + Q(ctx.TryGetValue(k, out cv) ? cv : ""))));
                }
                W("02-iterations.csv", rows);

                // 03 trades
                W("03-trades.csv", new[] { "iteration,time,side,lots,price,level,basket,why" }.Concat(trs.Select(t => string.Join(",", t.Iter.ToString(IC), T(t.Time), t.Side, N(t.Lots), N(t.Price), N(t.Level), N(t.Basket), Q(t.Why)))));

                // 04-07 the views as on the screen
                W("04-story.txt", AutoLines("STORY")); W("05-analysis.txt", AutoLines("ANALYSIS")); W("06-learn.txt", AutoLines("LEARN")); W("07-log.txt", AutoLines("LOG"));

                // 10 the recorded hedges (with what the price did after each)
                try { if (System.IO.File.Exists(SigFile)) W("13-signals.csv", System.IO.File.ReadAllLines(SigFile)); } catch { }
                { var hr = AutoHedgeRows(); if (hr.Count > 0) W("10-hedges.csv", new[] { HedgeHeader }.Concat(hr.Select(a => { var b2 = (string[])a.Clone(); long tk; if (long.TryParse(b2[1], NumberStyles.Integer, IC, out tk)) b2[1] = T(new DateTime(tk)); return string.Join(",", b2); }))); }
                // 11 every basket's minute-by-minute path; 12 the archived 1-minute candles of every traded day
                try { var pf = string.IsNullOrEmpty(aHist) ? null : AutoFile("paths"); if (pf != null && File.Exists(pf)) W("11-basket-paths.csv", new[] { "iteration,time,bid,basket,net_lots,legs,gross_lots,state" }.Concat(File.ReadAllLines(pf).Select(l => { var a = l.Split(','); long tk; if (a.Length > 1 && long.TryParse(a[1], NumberStyles.Integer, IC, out tk)) a[1] = T(new DateTime(tk)); return string.Join(",", a); }))); } catch { }
                try
                {
                    var cd = Path.Combine(Dir, "auto", "candles"); if (Directory.Exists(cd))
                    {
                        var days = new HashSet<string>(its.Select(x => x.Start.ToString("yyyy-MM-dd", IC))); var outD = Path.Combine(dir, "12-candles-by-day"); int nC = 0;
                        foreach (var f in Directory.GetFiles(cd, s.Symbol + "-*.csv")) if (days.Contains(Path.GetFileNameWithoutExtension(f).Substring(s.Symbol.Length + 1))) { Directory.CreateDirectory(outD); File.Copy(f, Path.Combine(outD, Path.GetFileName(f)), true); nC++; }
                        if (nC > 0) files.Add("12-candles-by-day\\ (" + nC + " days)");
                    }
                }
                catch { }
                // 08 the candles of the symbol (M1 + the strategy's timeframe + the FVG timeframe), from the first iteration − 1 day
                var from = its.Count > 0 ? its.Min(x => x.Start).AddDays(-1) : DateTime.MinValue; int candleFiles = 0;
                foreach (var tf in new[] { 1, s.Tf, s.FvgTf }.Where(m => m > 0).Distinct())
                {
                    List<Mt5Bar> bars; lock (broker.State.Gate) bars = broker.State.SeriesOf(s.Symbol, tf).Bars.Where(b => b.Time >= from).Select(b => b.Copy()).ToList();
                    if (bars.Count == 0) continue; candleFiles++;
                    W("08-candles-" + s.Symbol + "-" + Tf.Name(tf) + ".csv", new[] { "time,open,high,low,close,tick_volume,spread_points" }.Concat(bars.Select(b => string.Join(",", T(b.Time), N(b.O), N(b.H), N(b.L), N(b.C), b.V.ToString(IC), b.Spread.ToString(IC)))));
                }

                // 09 the race: its saved days + the leaderboard
                var rf = RaceFolder(); int raceFiles = 0;
                if (Directory.Exists(rf))
                {
                    var rd = Path.Combine(dir, "09-race"); Directory.CreateDirectory(rd);
                    foreach (var f in Directory.GetFiles(rf, "*.csv")) { File.Copy(f, Path.Combine(rd, Path.GetFileName(f)), true); raceFiles++; }
                    var stats = RaceStore.Stats(RaceStore.Load(rf, DateTime.MinValue));
                    File.WriteAllLines(Path.Combine(rd, "leaderboard.txt"), new[] { string.Format("{0,-28} {1,5} {2,6} {3,6} {4,5} {5,11} {6,10} {7,10} {8,10} {9,8}", "STRATEGY", "DAYS", "GREEN", "ITER.", "WON", "NET", "PER DAY", "WORST DAY", "WORST DD", "SCORE") }
                        .Concat(stats.Select(x => string.Format(IC, "{0,-28} {1,5} {2,6} {3,6} {4,5} {5,11:0.00} {6,10:0.00} {7,10:0.00} {8,10:0.00} {9,8:0.00}", x.Key, x.Days, x.GreenDays, x.Iterations, x.Wins, x.Net, x.PerDay, x.WorstDay, x.WorstDd, x.Score))));
                    files.Add("09-race\\ (" + raceFiles + " day files + leaderboard.txt)");
                }

                // 00 README: what this is and what every file holds
                var rd0 = new List<string> {
                    "SPASIX EXPORT FOR CLAUDE", "",
                    "app            " + Build, "exported       " + now.ToString("yyyy-MM-dd HH:mm:ss", IC) + " (this PC's clock)",
                    "window         " + Mode + (Sim ? " (backtest replay)" : Paper ? " (demo: simulated account on live prices)" : " (MT5 account)"),
                    "account        " + (string.IsNullOrEmpty(aHist) ? "—" : aHist),
                    "symbol         " + s.Symbol + "   timeframe " + Tf.Name(s.Tf) + "   preset " + s.Preset + "   entry " + s.EntryMode + "   hedge " + s.HedgeMode + " / " + s.HedgeSize,
                    "times          every time in these files is MT5's SERVER time; New York = server " + (s.NyOffsetHours >= 0 ? "+" : "") + s.NyOffsetHours + " h",
                    "automation     " + (auto.Running ? "RUNNING (" + auto.Phase + ")" : "stopped" + (auto.StopReason != "" ? ": " + auto.StopReason : "")), "",
                    "SUMMARY        " + st.Done + " iterations, won " + st.Wins + ", lost " + st.Losses + ", net " + st.Net.ToString("0.00", IC) + ", worst drawdown " + st.WorstDd.ToString("0.00", IC) + ", " + st.Buys + " buys / " + st.Sells + " sells / " + st.AddOns + " add-ons", "",
                    "FILES",
                    "  01-settings.txt     every setting of the strategy (key = value)",
                    "  02-iterations.csv   one row per iteration: start/end, seconds in the trade, legs, lots, worst drawdown, result, why it closed, the balance, ctx_* = the market when it started",
                    "  03-trades.csv       every order: iteration, time, side, lots, fill price, the level that triggered it, the basket $ at that moment, why",
                    "  04-story.txt        what happened and why, iteration by iteration (as the STORY tab)",
                    "  05-analysis.txt     the totals, by add-ons, by day, by hour (as the ANALYSIS tab)",
                    "  06-learn.txt        win rate / drawdown by market context (as the LEARN tab)",
                    "  07-log.txt          the engine's log",
                    "  08-candles-*.csv    " + (candleFiles > 0 ? "the symbol's candles the app had in memory (from a day before the first iteration)" : "(no candles in memory — open the 📈 chart on the symbol, then export again)"),
                    "  11-basket-paths.csv every open basket once a minute: price, basket $, net lots, legs, gross lots (LOCKED when locked)",
                    "  12-candles-by-day\\ the 1-minute candles of every day that had an iteration (archived by SpasiX every 10 minutes)",
                    "  10-hedges.csv       every hedge / lock / unlock: the basket's range, ATR, hedges before it, its best / worst move in the next 30 min, whipsaw = 1",
                    "  09-race\\            " + (raceFiles > 0 ? "every strategy of the 🏁 RACE: the saved iterations / trades per day + leaderboard.txt" : "(no race saved yet)") };
                File.WriteAllLines(Path.Combine(dir, "00-README.txt"), rd0);

                var zip = dir + ".zip"; if (File.Exists(zip)) File.Delete(zip); System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
                Log("📦 exported for Claude: " + zip, Green); Toast("📦 exported — send this .zip to Claude: " + Path.GetFileName(zip));
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + zip + "\"") { UseShellExecute = true }); } catch { }
            }
            catch (Exception ex) { Log("📦 export failed: " + ex.Message, Red); Toast("📦 export failed: " + ex.Message); }
        }
        // the recorded hedges / locks / unlocks of this account (each with what the price did in the 30 minutes after it)
        List<string[]> AutoHedgeRows() { var res = new List<string[]>(); if (string.IsNullOrEmpty(aHist)) return res; try { var f = AutoFile("hedges"); if (File.Exists(f)) foreach (var l in File.ReadAllLines(f)) { var a = l.Split(','); if (a.Length >= 15 && a[0] != "iteration") res.Add(a); } } catch { } return res; }
        const string HedgeHeader = "iteration,time,kind,side,lots,price,range_low,range_high,atr,hedges_before,basket,best_30min,worst_30min,whipsaw,context";
        string AutoFile(string what) { return Path.Combine(Dir, "auto", Mode + "-" + aHist + "-" + what + (what == "story" || what == "log" ? ".txt" : ".csv")); }
        void AutoAppend(string what, string line) { if (string.IsNullOrEmpty(aHist)) AutoLoadHistory(); if (string.IsNullOrEmpty(aHist)) return; try { Directory.CreateDirectory(Path.Combine(Dir, "auto")); File.AppendAllText(AutoFile(what), line + "\n"); } catch { } }
        void AutoLoadHistory()
        {
            var id = AutoId(); if (id == "" || aHist == id) return; aHist = id; if (auto.Iterations.Count > 0) return;
            try
            {
                var f = AutoFile("iterations"); if (File.Exists(f)) foreach (var l in File.ReadAllLines(f).Skip(1)) { var a = l.Split(','); if (a.Length < 16) continue; Func<int, double> d = i => double.Parse(a[i], IC); auto.Iterations.Add(new AutoIteration { N = int.Parse(a[0], IC), Start = new DateTime(long.Parse(a[1], IC)), End = new DateTime(long.Parse(a[2], IC)), FirstDir = a[3], Buys = int.Parse(a[4], IC), Sells = int.Parse(a[5], IC), AddOns = int.Parse(a[6], IC), BuyLots = d(7), SellLots = d(8), MaxGross = d(9), MaxDd = d(10), Result = d(11), Reason = a[12], BalanceStart = d(13), BalanceEnd = d(14), Peak = d(15), WinnerAdds = a.Length > 16 ? int.Parse(a[16], IC) : 0, Ctx = a.Length > 17 ? a[17] : "", Fees = a.Length > 18 ? double.Parse(a[18], IC) : 0, Manual = a.Length > 17 && a[17].Contains("manual=yes") }); }
                var t2 = AutoFile("trades"); if (File.Exists(t2)) foreach (var l in File.ReadAllLines(t2).Skip(1)) { var a = l.Split(new[] { ',' }, 8); if (a.Length < 8) continue; auto.Trades.Add(new AutoTrade { Iter = int.Parse(a[0], IC), Time = new DateTime(long.Parse(a[1], IC)), Side = a[2], Lots = double.Parse(a[3], IC), Price = double.Parse(a[4], IC), Level = double.Parse(a[5], IC), Basket = double.Parse(a[6], IC), Why = a[7].Trim('"') }); }
                var s = AutoFile("story"); if (File.Exists(s)) foreach (var l in File.ReadAllLines(s).Reverse().Take(4000).Reverse()) { int sp = l.IndexOf("  "); int n; if (l.StartsWith("#") && sp > 1 && int.TryParse(l.Substring(1, sp - 1), out n)) auto.Story.Add(Tuple.Create(n, l.Substring(sp + 2))); }
                var af2 = AutoFile("after"); if (File.Exists(af2)) foreach (var l in File.ReadAllLines(af2)) { var a = l.Split(','); if (a.Length < 7) continue; long tk; int n; if (!int.TryParse(a[0], out n) || !long.TryParse(a[1], NumberStyles.Integer, IC, out tk)) continue; var x = auto.Iterations.FirstOrDefault(i => i.N == n && i.Start.Ticks == tk); if (x == null) continue; Func<int, double> d2 = i => double.Parse(a[i], IC); x.AfterMax = d2(2); x.AfterMaxMin = d2(3); x.AfterBackTargetMin = d2(4); x.AfterBreakevenMin = d2(5); x.AfterMove = d2(6); x.AfterDone = true; }
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
                if (!File.Exists(f)) File.WriteAllText(f, "n,start,end,first,buys,sells,addons,buylots,selllots,maxgross,maxdd,result,reason,balstart,balend,peak,winneradds,context,fees\n");
                if (!File.Exists(t)) File.WriteAllText(t, "iteration,time,side,lots,price,level,basket,why\n");
                foreach (var x in done.Skip(aSaved))
                {
                    File.AppendAllText(f, string.Join(",", x.N.ToString(IC), x.Start.Ticks.ToString(IC), x.End.Ticks.ToString(IC), x.FirstDir, x.Buys.ToString(IC), x.Sells.ToString(IC), x.AddOns.ToString(IC), x.BuyLots.ToString("R", IC), x.SellLots.ToString("R", IC), x.MaxGross.ToString("R", IC), x.MaxDd.ToString("R", IC), x.Result.ToString("R", IC), x.Reason, x.BalanceStart.ToString("R", IC), x.BalanceEnd.ToString("R", IC), x.Peak.ToString("R", IC), x.WinnerAdds.ToString(IC), (x.Ctx ?? "").Replace(",", " "), x.Fees.ToString("0.##", IC)) + "\n");
                    File.AppendAllLines(t, auto.Trades.Where(tr => tr.Iter == x.N && tr.Time >= x.Start).Select(tr => string.Join(",", tr.Iter.ToString(IC), tr.Time.Ticks.ToString(IC), tr.Side, tr.Lots.ToString("R", IC), tr.Price.ToString("R", IC), (double.IsNaN(tr.Level) ? 0 : tr.Level).ToString("R", IC), tr.Basket.ToString("R", IC), "\"" + tr.Why.Replace("\"", "'").Replace(",", ";") + "\"")));
                }
                aSaved = done.Count;
            }
            catch { }
        }
        // the arrows on the chart: each entry / hedge / add (▲ buy, ▼ sell) and each close (● with its result)
        List<Tuple<DateTime, double, string, string>> AutoMarks(string sym)
        {
            var res = new List<Tuple<DateTime, double, string, string>>(); var e = ShowEngine(); var s = e.Running || e != auto ? e.Set : aset; if (!string.Equals(s.Symbol, sym, StringComparison.OrdinalIgnoreCase)) return res;
            foreach (var tr in e.Trades.Skip(Math.Max(0, e.Trades.Count - 400))) res.Add(Tuple.Create(tr.Time, tr.Price, tr.Side, (tr.Why.StartsWith("ENTRY") ? "#" + tr.Iter + " " : tr.Why.StartsWith("ADD TO") ? "+W " : "H ") + tr.Lots.ToString("0.##", IC)));
            foreach (var it in e.Iterations.Where(x => x.Done && x.CloseBid > 0).Skip(Math.Max(0, e.Iterations.Count - 200))) res.Add(Tuple.Create(it.End, it.CloseBid, "CLOSE", (it.Result >= 0 ? "+" : "−") + "$" + Math.Abs(it.Result).ToString("0", IC)));
            return res;
        }

        // ---- the screen
        void AutoPaint(bool force)
        {
            if (aPhase == null) return; bool on = auto.Running; var s = on ? auto.Set : aset; var it = auto.Current;
            if (aHist == null && !Sim) AutoLoadHistory();
            if (aWasOn && !on && auto.StopReason != "the window closed") Put("auto.run." + AKey, "0"); aWasOn = on;
            if (!on) aStart.Content = AutoWasRunning() ? "⟳  RESUME (it was running)" : "▶  START";
            try { AutoPostMortems(); } catch { }
            try { AutoReborn(); } catch { }
            if (aManual != null) { aManual.Content = auto.ManualManage ? "✋  MANUAL MANAGE: ON" : "✋  MANUAL MANAGE: OFF"; aManual.Background = auto.ManualManage ? Tint(Violet, 170) : Tint(Violet, 50); }
            if (aRebornBtn != null) { aRebornBtn.Content = "💥 AUTO RESTART: " + (RebornOn ? "ON" : "OFF"); aRebornBtn.Background = RebornOn ? Tint(Green, 90) : Tint(Sx.Card2, 220); }
            if (aChartManual != null) { aChartManual.Content = auto.ManualManage ? "✋ MANUAL: ON" : "✋ MANUAL: OFF"; aChartManual.Background = auto.ManualManage ? Tint(Violet, 170) : (Brush)Card; }
            aPhase.Text = "🛡 HEDGE — " + (on ? auto.Phase : "OFF") + (on ? "  •  " + s.Symbol + " " + Tf.Name(s.Tf) : ""); aPhase.Foreground = on ? Green : Muted;
            aCard.BorderBrush = on ? Green : Line;
            aBasket.Text = it != null ? Signed(auto.Basket, "USD") + "  /  +$" + s.Target.ToString("0.##", IC) : "—"; aBasket.Foreground = it == null ? Muted : auto.Basket >= 0 ? Green : Red;
            aInfo.Text = on ? (it != null ? "iteration #" + it.N + " since " + it.Start.ToString("dd MMM HH:mm:ss", IC) + "  •  " + it.Buys + " buys (" + it.BuyLots.ToString("0.##", IC) + ") / " + it.Sells + " sells (" + it.SellLots.ToString("0.##", IC) + ")  •  " + it.AddOns + " add-ons  •  max drawdown " + it.MaxDd.ToString("0.00", IC) + (s.MaxDrawdown > 0 ? " (limit −$" + s.MaxDrawdown.ToString("0.##", IC) + ")" : " (no limit)")
                    : auto.Phase == "PAUSE" ? "pause until " + auto.PauseUntil.ToString("HH:mm:ss", IC) + " (" + Math.Max(0, (auto.PauseUntil - auto.Now).TotalSeconds).ToString("0", IC) + " s)" : auto.Phase == "FIRST" ? "the first " + s.FirstDir + " goes now" : "waiting for the next " + auto.WaitingFor() + (auto.Blocked != "" ? "  •  NOT NOW: " + auto.Blocked : ""))
                : (auto.StopReason != "" ? "stopped: " + auto.StopReason + "  •  " : "") + (Sim ? "BACKTEST: press START here, then ▶ play on the 📈 CHART." : Paper ? "DEMO account: simulated on live prices." : "MT5 account: needs 🔓 ARM (chart panel) and MT5's Algo Trading.");
            aInfo.Foreground = !on && auto.StopReason != "" ? Amber : Muted;
            aLevels.Text = on ? (s.HedgeMode == "NONE" || s.HedgeMode == "SIGNAL" ? "" : "hedge levels: recent high " + Px(auto.BuyLevel) + "  •  recent low " + Px(auto.SellLevel)) + (!double.IsNaN(auto.Vwap) ? "  •  VWAP " + Px(auto.Vwap) : "") + (it != null ? "  •  next add-on (" + (s.HedgeMode == "PRICE" ? "price move" : s.HedgeMode == "MONEY" ? "$ loss" : "break") + ") at " + Px(auto.HedgeLevel) : "") : "";
            if (aGuideBtn != null) aGuideBtn.Content = aGuidesOn ? "🎯 CHART GUIDES: ON" : "🎯 CHART GUIDES: OFF";
            if (aSetScroll != null) { aSetScroll.Visibility = aView == "⚙ SETTINGS" ? Visibility.Visible : Visibility.Collapsed; if (aRaceHost != null) aRaceHost.Visibility = aView == "🏁 RACE" ? Visibility.Visible : Visibility.Collapsed; }
            {   // SETTINGS and RACE get the whole room: the setup cards and the number tiles belong to the 🤖 views
                bool full = aView == "⚙ SETTINGS" || aView == "🏁 RACE"; if (aSetups != null) aSetups.Visibility = full ? Visibility.Collapsed : Visibility.Visible; if (aTiles != null) aTiles.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
            }
            aStart.IsEnabled = !on; aStop.IsEnabled = on;
            if (aLockTxt != null)
            {   // the settings bar: running → SAVE for the next iteration (amber while there are unsaved changes); stopped → START / RESUME here
                string inUse = (auto.Next ?? auto.Set).Save(), boxes = AutoRead().Save(); bool dirty = on && inUse != boxes, queued = on && auto.Next != null;
                aLockTxt.Text = !on ? "⏸ STOPPED — change what you want, then ▶ START / RESUME (the open positions are taken over again)"
                    : dirty ? "✏ CHANGES NOT SAVED YET — press 💾 SAVE: the open iteration keeps its settings, the NEXT one uses these"
                    : queued ? "⏳ SAVED — the new settings start with the NEXT iteration" + (auto.Current != null ? " (#" + auto.Current.N + " is still open with the old ones)" : "")
                    : "🟢 RUNNING — you can change any setting below; 💾 SAVE applies it from the next iteration (the open one is not touched)";
                aLock.Background = Tint(!on ? Cyan : dirty ? Amber : Green, 45); aLock.BorderBrush = !on ? Cyan : dirty ? Amber : Green;
                aSaveBtn.Content = !on ? (AutoWasRunning() ? "⟳  RESUME WITH THESE SETTINGS" : "▶  START WITH THESE SETTINGS") : "💾  SAVE — FROM THE NEXT ITERATION";
                aSaveBtn.Background = Tint(!on ? Cyan : Green, (byte)(dirty ? 200 + 55 * Math.Abs(Math.Sin(DateTime.Now.TimeOfDay.TotalMilliseconds / 300.0)) : 200)); aSaveBtn.IsEnabled = !on || dirty;
                aUndoBtn.Visibility = on && dirty ? Visibility.Visible : Visibility.Collapsed; aNowBtn.Visibility = on && (dirty || queued) ? Visibility.Visible : Visibility.Collapsed; aStopSmall.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            }
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
                Tuple.Create("NET (AFTER COMMISSION)", Signed(st.Net, "USD"), st.Net >= 0 ? Green : Red),
                Tuple.Create("COMMISSIONS", Signed(-auto.Iterations.Sum(x => x.Fees), "USD") + "  (today " + Signed(-auto.Iterations.Where(x => x.Start.Date == today).Sum(x => x.Fees), "USD") + ")", Amber),
                Tuple.Create("WORST DRAWDOWN", st.WorstDd.ToString("0.00", IC), st.WorstDd < 0 ? Red : Muted),
                Tuple.Create("BUYS / SELLS / ADD-ONS", st.Buys + " / " + st.Sells + " / " + st.AddOns, Violet),
                Tuple.Create("MOST ADD-ONS", st.MostAddOns.ToString(IC), Cyan));   // the open iteration's commissions count in COMMISSIONS live
        }
        // the text of one view (STORY / ITERATIONS / TRADES / ANALYSIS / LEARN / LOG) — the screen and the 📦 EXPORT use the same lines
        List<string> AutoLines(string aView)
        {
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
                    {   // 📡 THE SIGNAL JOURNAL: the entries the engine saw — what the next hour did — by how extended the move was at the signal
                        var sr = SigRows(); Func<string, double> nf = x => { double v; return double.TryParse(x, NumberStyles.Float, IC, out v) ? v : 0; };
                        L.Add("📡 THE SIGNAL JOURNAL — " + sr.Count + " entry signals with what the price did in the next 15 / 30 / 60 min (taken or not) • saved in " + SigFile);
                        if (sr.Count >= 5)
                        {
                            L.Add(string.Format(IC, "   {0,-34} {1,7} {2,11} {3,11} {4,13}", "AT THE SIGNAL", "SIGNALS", "BEST 60 min", "WORST 60 min", "HIT TARGET 1st"));
                            Func<string[], string, string> cv = (a, k) => { var ctx = string.Join(",", a.Skip(14)).Trim('"'); var kv = ctx.Split(';').FirstOrDefault(z => z.StartsWith(k + "=")); return kv == null ? "?" : kv.Substring(k.Length + 1); };
                            foreach (var key in new[] { "ext", "pullback", "run", "toDayEdge", "withTrend", "session" })
                                foreach (var g in sr.GroupBy(a => cv(a, key)).Where(g => g.Key != "?" && g.Count() >= 3).OrderBy(g => g.Key))
                                {
                                    double hitT = g.Count(a => nf(a[12]) >= 0 && (nf(a[13]) < 0 || nf(a[12]) <= nf(a[13]))) * 100.0 / g.Count();
                                    L.Add(string.Format(IC, "   {0,-34} {1,7} {2,11:0.00} {3,11:0.00} {4,12:0}%", key + " = " + g.Key, g.Count(), g.Average(a => nf(a[9])), g.Average(a => nf(a[10])), hitT));
                                }
                            L.Add("   (BEST / WORST = the average move for / against the signal in price; HIT TARGET 1st = it reached the target distance before the same distance against)");
                        }
                        else L.Add("   collecting — every signal is measured for an hour after it, then saved here");
                        L.Add("");
                    }
                    {   // 🏆 THE BEST SETUPS (user's request): which conditions give CLEAN baskets — a win, no hedge, ≤ 30 min, never deeper than 0.5 %
                        var all0 = auto.Iterations.Where(x => x.Done && !string.IsNullOrEmpty(x.Ctx) && !x.Manual).ToList(); int min0 = Math.Max(5, Math.Min(10, all0.Count / 10));
                        L.Add("🏆 THE BEST SETUPS — what the CLEAN baskets (a win, no hedge, ≤ 30 min, never deeper than 0.5 %) had in common: " + all0.Count + " baskets, " + (all0.Count > 0 ? (all0.Count(HedgeAuto.Clean) * 100.0 / all0.Count).ToString("0", IC) : "0") + " % clean overall • a condition shows once it has " + min0 + "+ baskets • the more baskets, the surer");
                        var bs = auto.BestSetups(min0);
                        if (bs.Count == 0) L.Add("   not enough baskets yet — every new iteration is saved with its conditions (session, hour, trend, day range, candles, VWAP, flow, volatility, news …)");
                        else { L.Add(string.Format(IC, "   {0,-30} {1,8} {2,8} {3,8} {4,10}", "CONDITION", "BASKETS", "CLEAN", "WENT BAD", "AVG MIN")); foreach (var t in bs.Take(12)) L.Add(string.Format(IC, "   {0,-30} {1,8} {2,7:0}% {3,7:0}% {4,10:0}", t.Item1, t.Item2, t.Item3, t.Item4, t.Item5)); if (bs.Count > 12) { L.Add("   … the WORST:"); foreach (var t in bs.Skip(Math.Max(12, bs.Count - 5))) L.Add(string.Format(IC, "   {0,-30} {1,8} {2,7:0}% {3,7:0}% {4,10:0}", t.Item1, t.Item2, t.Item3, t.Item4, t.Item5)); } }
                        L.Add("");
                    }
                    // 🧠 first: the post-mortems — how each closed iteration's drawdown could have been prevented (its own candles, replayed)
                    var pm = pmRows ?? PostMortem.Load(PmFile);
                    L.Add("🧠 HOW THE DRAWDOWN COULD HAVE BEEN PREVENTED — every iteration that closes here is replayed on ITS OWN prices with each protection (simulated, nothing sent): " + pm.Count + " so far • 🎞 = on the REAL ticks as they came (exact), without it = on the 1-minute candles (approximate inside each minute) • saved in " + PmFile);
                    L.Add("WHAT HELPS MOST: " + PostMortem.Tally(pm));
                    foreach (var r0 in pm.OrderByDescending(x => x.Start).Take(15))
                    {
                        L.Add(""); L.Add(r0.Start.ToString("dd MMM HH:mm", IC) + "  " + r0.Lesson);
                        foreach (var a0 in r0.Alts) L.Add(string.Format(IC, "     {0,-40} {1,10} closed   worst {2,10}   {3,5} lots   {4,3} hedges{5}", a0.Name, (a0.Result >= 0 ? "+" : "") + a0.Result.ToString("0.00", IC), a0.Dd.ToString("0.00", IC), a0.MaxLots.ToString("0.##", IC), a0.Hedges, a0.Closed ? "" : "   (still open at the end of the candles)"));
                    }
                    L.Add(""); L.Add("────────────────────────────────────────");
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
                    {   // 📈 AFTER THE TARGET: how much more each TARGET basket would have made, and how often a bigger target would have been reached
                    var at = auto.Iterations.Where(x => x.Done && x.Reason == "TARGET" && !double.IsNaN(x.AfterMax)).ToList(); var atd = at.Where(x => x.AfterDone).ToList();
                    L.Add(""); L.Add("📈 AFTER THE TARGET — " + at.Count + " TARGET baskets followed as if they had stayed open (" + atd.Count + " finished: back to breakeven or 8 hours)");
                    if (at.Count > 0)
                    {
                        var ups = at.Select(x => x.AfterMax - x.Result).OrderBy(v => v).ToList(); var tm = at.Where(x => x.AfterMax > x.Result + 0.5).Select(x => x.AfterMaxMin).OrderBy(v => v).ToList();
                        L.Add(string.Format(IC, "   extra after the target: median +${0:0.00} • average +${1:0.00} • best +${2:0.00}   •   the best came after a median of {3}", ups[ups.Count / 2], ups.Average(), ups.Last(), tm.Count > 0 ? tm[tm.Count / 2].ToString("0", IC) + " min" : "—"));
                        if (atd.Count > 0) L.Add(string.Format(IC, "   came back to breakeven within 8 h: {0:0}% • stayed above breakeven 8 h: {1:0}%", 100.0 * atd.Count(x => x.AfterBreakevenMin >= 0) / atd.Count, 100.0 * atd.Count(x => x.AfterBreakevenMin < 0) / atd.Count));
                        L.Add("   a bigger target — how many of these baskets would have reached it (before coming back):");
                        foreach (var tg in new[] { 20.0, 30, 40, 60, 80, 100, 150 }) L.Add(string.Format(IC, "      +${0,-4:0}  {1,5:0}% of {2}", tg, 100.0 * at.Count(x => x.AfterMax >= tg) / at.Count, at.Count));
                        var ctxK = new[] { "session", "withFlow", "withVwap", "side" };
                        foreach (var ck in ctxK)
                        {
                            var grp = at.GroupBy(x => { var kv = (x.Ctx ?? "").Split(';').FirstOrDefault(z => z.StartsWith(ck + "=")); return kv == null ? "?" : kv.Substring(ck.Length + 1); }).Where(g2 => g2.Count() >= 2).ToList();
                            if (grp.Count > 1) L.Add("   by " + ck + ": " + string.Join("   ", grp.Select(g2 => g2.Key + " " + g2.Average(x => x.AfterMax - x.Result).ToString("+$0;−$0", IC) + " extra (" + g2.Count() + ")")));
                        }
                    }
                }
                {   // 🧱 HEDGES: which hedges were whipsaws (the price went further against the hedge than for it in the next 30 minutes)
                    var hr = AutoHedgeRows(); Func<string, double> dv = x => { double v; return double.TryParse(x, NumberStyles.Float, IC, out v) ? v : 0; };
                    L.Add(""); L.Add("🧱 HEDGES — " + hr.Count + " recorded (hedges, locks, unlocks); a WHIPSAW = in the next 30 minutes the price went further AGAINST it than for it");
                    if (hr.Count > 0)
                    {
                        Action<string, IEnumerable<string[]>> row = (name, g) => { var l2 = g.ToList(); if (l2.Count == 0) return; int w = l2.Count(a => a[13] == "1"); L.Add(string.Format(IC, "   {0,-34} n {1,4}   whipsaws {2,5:0}%   avg best {3,6:0.00}   avg worst {4,6:0.00}", name, l2.Count, 100.0 * w / l2.Count, l2.Average(a => dv(a[11])), l2.Average(a => dv(a[12])))); };
                        foreach (var k in new[] { "HEDGE", "LOCK", "UNLOCK" }) row(k, hr.Where(a => a[2] == k));
                        L.Add("   by the basket's range when it hedged (in ATRs of the timeframe):");
                        row("   range < 1 ATR (tight chop)", hr.Where(a => a[2] == "HEDGE" && dv(a[8]) > 0 && (dv(a[7]) - dv(a[6])) / dv(a[8]) < 1));
                        row("   range 1–2 ATR", hr.Where(a => a[2] == "HEDGE" && dv(a[8]) > 0 && (dv(a[7]) - dv(a[6])) / dv(a[8]) >= 1 && (dv(a[7]) - dv(a[6])) / dv(a[8]) < 2));
                        row("   range ≥ 2 ATR (a real move)", hr.Where(a => a[2] == "HEDGE" && dv(a[8]) > 0 && (dv(a[7]) - dv(a[6])) / dv(a[8]) >= 2));
                        L.Add("   by how many hedges came before it in the basket:");
                        foreach (var gb in hr.Where(a => a[2] == "HEDGE").GroupBy(a => Math.Min(3, (int)dv(a[9]))).OrderBy(g => g.Key)) row("   hedge #" + (gb.Key + 1) + (gb.Key >= 3 ? "+" : ""), gb);
                    }
                }
                L.Add(""); L.Add("To send everything to Claude: press 📦 EXPORT FOR CLAUDE (one .zip with the settings, iterations, trades, story, analysis, learn, log, the candles and the race).");
                }
                else if (aView == "STORY")
                {
                    L.Add("WHAT HAPPENED AND WHY — the newest iteration first, each one from its entry to its close (the candle, the level, the prices, the $)");
                    foreach (var g in auto.Story.GroupBy(x => x.Item1).OrderByDescending(g => g.Key)) { L.Add(""); L.Add(g.Key > 0 ? "──── ITERATION #" + g.Key + " ────" : "──── waiting ────"); foreach (var x in g) L.Add(x.Item2); }
                    if (auto.Story.Count == 0) L.Add("\nnothing yet — START the automation");
                }
                else L.AddRange(auto.Log.AsEnumerable().Reverse());
                return L;
        }
        // 📈 AFTER THE TARGET in one line: the best it reached afterwards (and when), back to the target, back to breakeven
        static string AfterText(AutoIteration x)
        {
            if (double.IsNaN(x.AfterMax)) return x.Done && x.Reason == "TARGET" ? "—" : "";
            Func<double, string> mm = m => m < 0 ? "" : m < 60 ? m.ToString("0", IC) + " min" : (m / 60).ToString("0.#", IC) + " h";
            string best = x.AfterMax > x.Result + 0.5 ? "best +$" + x.AfterMax.ToString("0", IC) + " after " + mm(x.AfterMaxMin) + " (+" + x.AfterMove.ToString("0.00", IC) + ")" : "no more than the target";
            return (x.AfterDone ? "" : "▶ ") + best + (x.AfterBackTargetMin >= 0 ? " • back to $" + x.Result.ToString("0", IC) + " at " + mm(x.AfterBackTargetMin) : "") + (x.AfterBreakevenMin >= 0 ? " • breakeven at " + mm(x.AfterBreakevenMin) : x.AfterDone ? " • no breakeven in 8 h" : " (following)");
        }
        void AutoTablePaint()
        {
            foreach (Button b in aViews.Children) if (b.Content is string) { if (b == aRebornBtn) continue; if (b == aSetBtn) { bool selS = aView == "⚙ SETTINGS"; b.Background = Tint(Gold, (byte)(selS ? 255 : 190)); b.Foreground = Bg; b.BorderBrush = selS ? Sx.White : Gold; continue; } b.Background = (string)b.Content == aView ? Tint(Cyan, 70) : Tint(Sx.Card2, 220); }   // ⚙ SETTINGS stays gold (step 1)
            var L = AutoLines(aView);
            aTable.TextWrapping = aView == "STORY" || aView == "LOG" ? TextWrapping.Wrap : TextWrapping.NoWrap;
            bool other = aView == "⚙ SETTINGS" || aView == "🏁 RACE", rowsView = !other && (aView == "ITERATIONS" || aView == "TRADES" || aView == "🗂 TESTS");
            aRowsView.Visibility = rowsView ? Visibility.Visible : Visibility.Collapsed; aTable.Visibility = !other && !rowsView ? Visibility.Visible : Visibility.Collapsed;
            if (other) return; if (rowsView) { aRows.Children.Clear(); if (aView == "🗂 TESTS") AutoTestsRows(); else AutoRowsOf(auto, aRows, aView); return; }
            var txt = string.Join("\n", L); if (aTable.Text != txt) { double vo = aTable.VerticalOffset, ho = aTable.HorizontalOffset; aTable.Text = txt; aTable.ScrollToVerticalOffset(vo); aTable.ScrollToHorizontalOffset(ho); }   // new lines never jump you back to the top
        }
    }
}
