// SpasiX — 🧠 AI CO-PILOT (09i), the window side: the AI card in the 🛡 HEDGE banner (🧠 ASK AI, ✅ EXECUTE …), the 🧠 AI panel, the automatic
// triggers (the radar above the alert level, a basket in trouble) under the rate limits and the monthly $ cap, the journal per account, the what-if
// of each advice, and the ⚙ SETUP card (the Anthropic API key — encrypted with Windows DPAPI for this Windows user, TEST CONNECTION, spend, caps).
// The advice is NEVER executed by itself on an MT5 account (REAL / PROP / MT5 demo): ✅ EXECUTE asks first (naming the account) and sends through
// the chart's normal Cmd path (ARM, MT5's checks, PROP limits). 🧠 AI AUTO exists only on the simulated DEMO / BACKTEST.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeystoneMT5
{
    public partial class MainWindow
    {
        // ---- shared by every SpasiX window of this Windows user: the key (DPAPI) and the spend (one monthly cap)
        static readonly byte[] AiEntropy = Encoding.UTF8.GetBytes("SpasiX-AI-co-pilot-09i");
        static string AiKeyFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpasiX", "ai-key.bin"); } }   // never in the data folder (exports / copies)
        static AiKeyStore aiKeys; static AiBudget aiBudget; static IAiAdvisor aiAdvisor; static string aiTest = "";
        static AiKeyStore AiKeys { get { return aiKeys ?? (aiKeys = new AiKeyStore(AiKeyFile, b => ProtectedData.Protect(b, AiEntropy, DataProtectionScope.CurrentUser), b => ProtectedData.Unprotect(b, AiEntropy, DataProtectionScope.CurrentUser))); } }
        static IAiAdvisor AiAdvisor { get { if (aiAdvisor == null && AiKeys.Has) { var k = AiKeys.Load(); if (k != "") aiAdvisor = new ClaudeAdvisor(k); } return aiAdvisor; } }

        AiCoordinator aiCo; bool aiBusy, aiNew; Border aiCard; TextBlock aiTxt; Button aiAskBtn, aiExecBtn; AiInput aiLastIn; string aiLastBasket = ""; int aiLastIter; WhatIfCase aiLastCase;
        readonly List<WhatIfCase> aiWi = new List<WhatIfCase>(); string aiFor = ""; DateTime aiTrigAt = DateTime.MinValue; CancellationTokenSource aiCts;

        AiBudget AiBudgetNow()
        {
            if (aiBudget == null) aiBudget = new AiBudget(Path.Combine(Dir, "ai", "ai-spend.txt"));
            aiBudget.PerBasketMinutes = (int)AiNum("ai.pb", 10, 1, 240); aiBudget.PerHour = (int)AiNum("ai.ph", 12, 1, 120); aiBudget.MonthlyCap = AiNum("ai.cap", 20, 0, 1000); return aiBudget;
        }
        double AiNum(string k, double d, double lo, double hi) { double v; return double.TryParse(Get(k, d.ToString(IC)), NumberStyles.Float, IC, out v) ? Math.Max(lo, Math.Min(hi, v)) : d; }
        bool AiAutoOn { get { return Virtual && Get("ai.auto", "0") == "1" && AiCoordinator.AutoAllowed(Virtual); } }

        // the card in the 🛡 HEDGE banner (added to its info column)
        FrameworkElement AiCardBuild()
        {
            aiCo = new AiCoordinator();
            aiTxt = new TextBlock { FontSize = 12.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap, MaxWidth = 900 };
            aiAskBtn = Pill("🧠 ASK AI", Tint(Violet, 70)); aiAskBtn.Height = 28; aiAskBtn.Margin = new Thickness(0, 4, 8, 0); aiAskBtn.Click += delegate { AiAsk(false, "your click"); };
            aiAskBtn.ToolTip = "asks Claude (" + AiCopilot.Model + ") about this basket now: the account, the legs, the total, the radar, similar past baskets, the candles, the trend, the news — one structured recommendation. It sends NOTHING by itself.";
            aiExecBtn = Pill("", Tint(Green, 90)); aiExecBtn.Height = 28; aiExecBtn.Margin = new Thickness(0, 4, 8, 0); aiExecBtn.Visibility = Visibility.Collapsed; aiExecBtn.Click += delegate { AiExecute(false); };
            var more = new TextBlock();
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(aiAskBtn); row.Children.Add(aiExecBtn);
            var sp = new StackPanel(); sp.Children.Add(aiTxt); sp.Children.Add(row);
            aiCard = new Border { Child = sp, Background = Tint(Violet, 22), BorderBrush = Tint(Violet, 120), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6) };
            return aiCard;
        }
        // per account: the journal + the what-if cases (loaded when the account is known)
        void AiLoad()
        {
            if (aiCo == null || Sim || string.IsNullOrEmpty(aHist) || aiFor == aHist) return; aiFor = aHist;
            aiCo.JournalFile = AutoFile("ai"); aiCo.Journal.Clear(); try { if (File.Exists(aiCo.JournalFile)) foreach (var l in File.ReadAllLines(aiCo.JournalFile)) { var e = AiEntry.Load(l); if (e != null) aiCo.Journal.Add(e); } } catch { }
            aiWi.Clear(); aiWi.AddRange(WhatIf.LoadAll(AutoFile("aiwhatif")));
        }
        void AiPaint()
        {
            if (aiCard == null) return; AiLoad(); var adv = AiAdvisor; var ad = aiCo.Last;
            if (adv == null) { aiTxt.Text = "🧠 AI CO-PILOT is off — no Anthropic API key. Add yours in ⚙ SETUP (stored encrypted for this Windows user); the advice is shown here and never executed without your click."; aiTxt.Foreground = Muted; aiExecBtn.Visibility = Visibility.Collapsed; aiAskBtn.IsEnabled = false; return; }
            aiAskBtn.IsEnabled = !aiBusy; aiAskBtn.Content = aiBusy ? "🧠 asking …" : "🧠 ASK AI";
            string t = aiCo.LastStatus != "" ? aiCo.LastStatus : "🧠 AI CO-PILOT ready (" + AiCopilot.Model + ") — it asks by itself when the ⚠ radar reaches " + aset.RadarAlert.ToString("0", IC) + " % or the basket is in trouble (" + AiBudgetNow().PerBasketMinutes + " min / basket, " + aiBudget.PerHour + " / hour, $" + aiBudget.MonthlyCap.ToString("0", IC) + " / month)";
            if (ad != null && ad.Valid) t += "\n" + ad.Reason + (ad.ChangeMind != "" ? "  ·  would change my mind: " + ad.ChangeMind : "");
            aiTxt.Text = t; aiTxt.Foreground = ad != null && !ad.Valid ? Amber : ad != null ? Text : Muted;
            bool can = ad != null && ad.Valid && ad.Action != "hold" && aiLastIn != null && AiCoordinator.Commands(ad, aiLastIn).Count > 0 && !(aiCo.Journal.LastOrDefault()?.Executed ?? false);
            aiExecBtn.Visibility = can ? Visibility.Visible : Visibility.Collapsed; if (can) aiExecBtn.Content = "✅ EXECUTE: " + ad.Summary.Replace("🧠 ", "").Split('·')[0].Trim() + " …";
        }

        // ---- the context: the account, the basket, the market, the radar, the similar baskets, the candles, the trend, the news, the what-ifs
        AiInput AiInputNow()
        {
            var s = auto.Running && auto.Set != null ? auto.Set : aset; var sym = s.Symbol; var a = new AiInput { Symbol = sym, Target = s.Target, Settings = AiSettingsText(s) };
            double per = 0; List<Mt5Position> ps;
            lock (broker.State.Gate)
            {
                var st = broker.State; a.Now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; a.Balance = st.Account.Balance; a.Equity = st.Account.Equity; a.MarginLevel = st.Account.MarginLevel;
                a.Kind = Paper ? "DEMO" : Mode == "PROP" ? "PROP" : st.Account.Kind == "REAL" ? "REAL" : "MT5 DEMO";
                Mt5Quote q; if (st.Quotes.TryGetValue(sym, out q)) { a.Bid = q.Bid; a.Ask = q.Ask; a.Spread = Math.Round(q.Ask - q.Bid, 2); }
                Mt5Symbol ms; if (st.Symbols.TryGetValue(sym, out ms) && ms.TickSize > 0) { per = ms.TickValue / ms.TickSize; double k = ms.SwapMode == 1 ? ms.Point * per : 1; a.SwapLong = Math.Round(ms.SwapLong * k, 2); a.SwapShort = Math.Round(ms.SwapShort * k, 2); }
                ps = st.Positions.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            foreach (var p in ps.OrderBy(p => p.Time).ThenBy(p => p.Ticket)) a.Legs.Add(new AiLeg { Ticket = p.Ticket, Side = p.Side, Lots = p.Lots, Open = p.Open, Pl = Math.Round(p.Profit + p.Swap + p.Commission, 2), Time = p.Time });
            double open = a.Legs.Sum(l => l.Pl); a.Total = !double.IsNaN(auto.GuardTotal) && ps.Count > 0 ? Math.Round(auto.GuardTotal, 2) : open; a.Banked = Math.Round(a.Total - open, 2);
            double net = a.Legs.Sum(l => (l.Side == "BUY" ? 1 : -1) * l.Lots), px = net > 0 ? a.Bid : a.Ask;
            a.BePx = HedgeAuto.PriceForTotal(a.Total, 0, net, per, px); a.TargetPx = HedgeAuto.PriceForTotal(a.Total, a.Target, net, per, px);
            try { a.M1 = AutoCandles(sym, a.Now.AddDays(-4)) ?? new List<Mt5Bar>(); } catch { }
            if (aRadar != null) { a.Radar = aRadar.Text; foreach (var n in aRadar.Near) a.Similar.Add(n.Text); } else a.Radar = "no reading yet (it reads after each hedge)";
            if (RadarModel.Default.Ready) a.RadarNote = "walk-forward AUC " + RadarModel.Default.Auc.ToString("0.00", IC) + " on " + RadarModel.Default.Samples + " hedges (2021–26); the % is the observed rate of its calibration band, not a forecast";
            try { var fin = a.M1.Where(b => b.Time.AddMinutes(1) <= a.Now).ToList(); if (fin.Count > 220) fin = fin.Skip(fin.Count - 220).ToList(); a.Trend = TrendDetect.Read(fin, TrendDetect.DayVwap(fin, a.Now)).Text; } catch { }
            try { int off; lock (broker.State.Gate) off = broker.State.ServerOffsetSec; var utc = a.Now.AddSeconds(-off); foreach (var n in news.Where(x => x.Impact == "High" && x.Utc >= utc.AddMinutes(-30) && x.Utc <= utc.AddHours(24)).OrderBy(x => x.Utc).Take(8)) a.News.Add(n.Currency + " " + n.Title + " at " + n.Utc.AddSeconds(off).ToString("ddd HH:mm", IC) + " (server)"); } catch { }
            a.WhatIf = WhatIf.Summary(aWi) + " " + AiWiSummary();
            aiLastBasket = auto.Current != null && !auto.Current.Done ? "#" + auto.Current.N : ps.Count > 0 ? "M" + ps.Min(p => p.Ticket) : "flat"; aiLastIter = auto.Current != null ? auto.Current.N : 0;
            return a;
        }
        static string AiSettingsText(AutoSettings s)
        {
            return (s.Preset != "" ? s.Preset : "custom") + " · entry " + s.EntryLots.ToString("0.##", IC) + " lots · target $" + s.Target.ToString("0.##", IC) + " · hedge " + s.HedgeMode + " / " + s.HedgeSize + " lead " + s.NetAdvantage.ToString("0.##", IC)
                + " · compress " + s.CompressMode + " · exit guard " + (s.ExitGuard ? "ON" : "OFF") + " · hard stop " + (s.HardFloat > 0 ? s.HardFloat.ToString("0", IC) + " % of the balance" : "none") + " · daily loss " + (s.DayLoss > 0 ? "$" + s.DayLoss.ToString("0", IC) : s.DayLossPct > 0 ? s.DayLossPct.ToString("0", IC) + " %" : "none")
                + " · on manual: " + s.OnManual + " · add on break " + (s.AddOnBreak ? "ON" : "OFF") + " · protect the add " + s.ProtectAdd;
        }

        // ---- ask (a click, or a trigger) — on a background thread; the answer is shown, journaled, its what-if kept; NOTHING is sent
        void AiAsk(bool trigger, string why)
        {
            if (aiCo == null || aiBusy) return; var adv = AiAdvisor; if (adv == null) { if (!trigger) Sx.Info(this, "🧠 AI CO-PILOT", "There is no Anthropic API key yet. Add yours in ⚙ SETUP ▸ 🧠 AI CO-PILOT (it is stored encrypted for this Windows user and only ever sent to the Anthropic API).", Amber); return; }
            AiInput a; try { a = AiInputNow(); } catch (Exception ex) { aiCo.LastStatus = "🧠 could not build the context: " + ex.Message; AiPaint(); return; }
            if (a.Legs.Count == 0 && trigger) return;
            aiCo.Advisor = adv; aiCo.Budget = AiBudgetNow(); aiCo.MaxLots = Math.Max(0.01, Math.Max(1, a.Legs.Sum(l => l.Lots)) * 2);
            aiBusy = true; aiLastIn = a; var basket = aiLastBasket; int iter = aiLastIter; aiCts = new CancellationTokenSource(TimeSpan.FromMinutes(6)); var ct = aiCts.Token; AiPaint();
            Log("🧠 AI: asking " + AiCopilot.Model + " (" + why + ") about " + basket + " …", Violet);
            Task.Run(async () =>
            {
                AiAdvice ad = null; try { ad = await aiCo.AskAsync(a, basket, trigger, iter, ct); } catch (Exception ex) { aiCo.LastStatus = "🧠 error: " + ex.Message; }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    aiBusy = false; aiLastCase = null;
                    if (ad != null)
                    {
                        var e = aiCo.Journal.LastOrDefault(); Log(aiCo.LastStatus + (e != null ? " · $" + e.Cost.ToString("0.000", IC) : ""), ad.Valid ? Violet : Amber);
                        if (ad.Valid && ad.Action != "hold" && !Sim) AiWhatIfNew(ad, a);
                        aiNew = true; if (trigger || ad.Valid) Toast(AccountBadge().Item1 + " — " + aiCo.LastStatus);
                        if (ad.Valid && ad.Action != "hold" && AiAutoOn) AiExecute(true);   // 🧠 AI AUTO: the simulated DEMO only
                    }
                    AiPaint(); if (aView == "🧠 AI") AutoPaint(true);
                }));
            });
        }
        // the triggers, every second: the radar above the alert level, or the basket in trouble (the 🎯 guard's breakeven mode, the total under $0)
        void AiTriggers()
        {
            if (aiCo == null || aiBusy || Sim || AiAdvisor == null || Get("ai.triggers", "1") != "1" || (DateTime.Now - aiTrigAt).TotalSeconds < 20) return;
            bool open; lock (broker.State.Gate) open = broker.State.Positions.Any(p => string.Equals(p.Symbol, (auto.Set ?? aset).Symbol, StringComparison.OrdinalIgnoreCase)); if (!open) return;
            string why = aRadar != null && !double.IsNaN(aRadar.P) && aRadar.P * 100 >= aset.RadarAlert ? "⚠ the radar is at " + (aRadar.P * 100).ToString("0", IC) + " %" : auto.GuardExit == "BREAKEVEN" && !double.IsNaN(auto.GuardTotal) && auto.GuardTotal < 0 ? "the basket is in trouble (" + (auto.GuardTotal < 0 ? "−$" : "$") + Math.Abs(auto.GuardTotal).ToString("0", IC) + ")" : "";
            if (why == "") return; aiTrigAt = DateTime.Now;
            string basket = auto.Current != null && !auto.Current.Done ? "#" + auto.Current.N : "M"; DateTime now; lock (broker.State.Gate) now = broker.State.ServerNow > DateTime.MinValue ? broker.State.ServerNow : DateTime.Now;
            if (AiBudgetNow().Check(basket, now, true) != "") return;   // held by a limit: silently (the card keeps the last advice)
            AiAsk(true, why);
        }
        // ✅ EXECUTE — your click asks first, naming the account; then the normal path (the chart's Cmd: ARM, MT5's checks, PROP limits). auto = 🧠 AI AUTO (simulated only)
        void AiExecute(bool autoRun)
        {
            var ad = aiCo != null ? aiCo.Last : null; var a = aiLastIn; if (ad == null || !ad.Valid || a == null) return; var cmds = AiCoordinator.Commands(ad, a); if (cmds.Count == 0) return;
            if (autoRun && !AiCoordinator.AutoAllowed(Virtual)) { Log(AiCoordinator.AutoNo, Amber); return; }
            if (!autoRun)
            {
                string acct = AccountBadge().Item1; bool real = !Virtual;
                if (!Sx.Ask(this, "🧠 EXECUTE THE AI ADVICE — " + (real ? "a REAL MT5 order" : "simulated DEMO"), BadgeLine() + ad.Summary + "\n\n" + ad.Reason + (ad.ChangeMind != "" ? "\nWhat would change its mind: " + ad.ChangeMind : "") + "\n\nIt sends, as YOUR trade on " + acct + ":\n  " + string.Join("\n  ", cmds.Select(c => c.Item3)) + "\n\n" + (auto.Running ? "The automation stops (⏸ STOP ON MANUAL) until you ▶ RESUME; the 🎯 exit guard keeps watching the total.\n" : "") + "An AI can be wrong — the six-year research found no reliable direction edge.", "✅ EXECUTE", "NO", real ? Red : Green, real ? "REAL ACCOUNT" : "DEMO")) return;
            }
            int ok = 0; foreach (var c in cmds) if (autoRun ? AutoSend(c.Item1, c.Item2, c.Item3) != null : Cmd(c.Item3, c.Item1, c.Item2)) ok++;
            var e = aiCo.Journal.LastOrDefault(); if (e != null && ok > 0) { e.Executed = true; e.Outcome = (autoRun ? "🧠 AI AUTO sent " : "your click sent ") + ok + "/" + cmds.Count + " at " + a.Now.ToString("dd MMM HH:mm:ss", IC); AiJournalSave(); }
            Log("🧠 AI " + (autoRun ? "AUTO (simulated)" : "EXECUTE (your click)") + ": " + ok + "/" + cmds.Count + " sent — " + ad.Summary, ok > 0 ? Green : Red); AiPaint();
        }
        void AiJournalSave() { try { if (aiCo.JournalFile != null) { Directory.CreateDirectory(Path.GetDirectoryName(aiCo.JournalFile)); File.WriteAllLines(aiCo.JournalFile, aiCo.Journal.Select(j => j.Save())); } } catch { } }

        // the what-if of an advice: the basket as it would be after following it, replayed by the automation alone (the 🤖 WHAT IF runner, every minute)
        void AiWhatIfNew(AiAdvice ad, AiInput a)
        {
            var s = (auto.Set ?? aset).Copy(); var it = auto.Current;
            var t = new WhatIfCase { N = it != null ? it.N : 0, Start = it != null ? it.Start : a.Legs.Count > 0 ? a.Legs.Min(l => l.Time) : a.Now, Balance = Math.Max(100, a.Balance), Settings = s.Save(), Target = s.Target, MaxDd = it != null ? it.MaxDd : Math.Min(0, a.Total),
                Commission = Paper ? paper.Acct.Set.CommissionPerLot : s.CommissionPerLot, Kind = Paper ? paper.Acct.Set.Kind : "STANDARD", Leverage = Paper ? paper.Acct.Set.Leverage : 500 };
            var c = AiCoordinator.Followed(ad, a, t); if (c == null) return; aiLastCase = c; aiWi.Add(c); WhatIf.SaveAll(AutoFile("aiwhatif"), aiWi); aWiAt = DateTime.MinValue;
        }
        string AiWiSummary()
        {
            var l = aiWi.Where(c => c.LastBar > DateTime.MinValue).ToList(); if (l.Count == 0) return "🧠 AI advice what-if: none replayed yet.";
            double d = l.Sum(c => (c.Done ? c.Result : c.NowTotal) - c.Actual);
            return "🧠 AI advice what-if — " + l.Count + " advice replayed: following them would have given " + (d >= 0 ? "+$" : "−$") + Math.Abs(d).ToString("N2", IC) + " vs what really happened (" + l.Count(c => (c.Done ? c.Result : c.NowTotal) > c.Actual + 2) + " better, " + l.Count(c => (c.Done ? c.Result : c.NowTotal) < c.Actual - 2) + " worse; simulated, the automation alone after the advice).";
        }
        // the 🧠 AI panel (a view of the 🛡 HEDGE tab) and its LEARN / export lines
        List<string> AiLines()
        {
            var L = new List<string>(); var ad = aiCo != null ? aiCo.Last : null; AiLoad();
            L.Add("🧠 AI CO-PILOT — " + AiCopilot.Model + " (adaptive thinking, effort high, a structured answer) · " + (AiAdvisor == null ? "OFF: no API key (⚙ SETUP)" : "key " + (AiKeys.Has ? "saved (encrypted)" : "—")) + " · this month $" + AiBudgetNow().SpentThisMonth(DateTime.Now).ToString("0.00", IC) + " of $" + aiBudget.MonthlyCap.ToString("0", IC) + " · automatic: " + (Get("ai.triggers", "1") == "1" ? "ON (radar ≥ " + aset.RadarAlert.ToString("0", IC) + " % or a basket in trouble; " + aiBudget.PerBasketMinutes + " min / basket, " + aiBudget.PerHour + " / hour)" : "OFF") + " · 🧠 AI AUTO: " + (Virtual ? (Get("ai.auto", "0") == "1" ? "ON (simulated)" : "OFF") : "never on an MT5 account"));
            L.Add("");
            if (ad != null)
            {
                L.Add("THE LAST ADVICE  " + ad.At.ToString("dd MMM HH:mm:ss", IC) + "  context #" + ad.Hash); L.Add("  " + (ad.Valid ? ad.Summary : "✖ " + string.Join("; ", ad.Problems)));
                if (ad.Valid) { var o = ad.Order; if (o.Side != "NONE") L.Add("  order: " + o.Side + " " + o.Lots.ToString("0.##", IC) + " " + o.EntryType + (o.EntryType != "MARKET" ? " @ " + o.EntryLevel.ToString("0.00", IC) : "") + (o.Stop > 0 ? " · stop " + o.Stop.ToString("0.00", IC) : "") + " · exit: " + o.Exit); if (ad.CloseTickets.Count > 0) L.Add("  close: #" + string.Join(", #", ad.CloseTickets)); L.Add("  why: " + ad.Reason); L.Add("  what would change its mind: " + ad.ChangeMind); }
                if (aiLastCase != null) L.Add("  🧠 what-if (followed): " + aiLastCase.WithoutYou);
                L.Add("");
            }
            L.Add(aiCo != null ? aiCo.Summary(DateTime.Now) : ""); L.Add(AiWiSummary()); L.Add("");
            L.Add(string.Format("{0,-16} {1,5} {2,-14} {3,-8} {4,9} {5,10}  {6}", "TIME", "ITER", "ACTION", "DONE", "COST", "TOTAL", "ADVICE"));
            if (aiCo != null) foreach (var j in aiCo.Journal.ToList().AsEnumerable().Reverse().Take(300))
            {
                var w = aiWi.FirstOrDefault(c => c.SnapAt == j.At && c.N == j.Iter);
                L.Add(string.Format(IC, "{0,-16} {1,5} {2,-14} {3,-8} {4,9:0.000} {5,10:N2}  {6}", j.At.ToString("dd MMM HH:mm", IC), "#" + j.Iter, j.Action, j.Executed ? "✅ YES" : "no", j.Cost, j.Total, j.Summary + (j.Outcome != "" ? " · " + j.Outcome : "") + (w != null ? " · what-if: " + w.WithoutYou + " (real " + (w.Actual >= 0 ? "+$" : "−$") + Math.Abs(w.Actual).ToString("N2", IC) + ")" : "")));
            }
            if (aiCo == null || aiCo.Journal.Count == 0) L.Add("no advice yet — 🧠 ASK AI in the banner, or it asks by itself when a basket gets dangerous");
            return L;
        }

        // ---- ⚙ SETUP ▸ 🧠 AI CO-PILOT: the key (encrypted for this Windows user), TEST CONNECTION, the model, the spend, the caps
        FrameworkElement AiSetupCard()
        {
            var st = new StackPanel();
            st.Children.Add(new TextBlock { Text = "🧠 AI CO-PILOT (Claude)", Foreground = Gold, FontSize = 16, FontWeight = FontWeights.Bold });
            st.Children.Add(new TextBlock { Text = "Advice for a basket that gets out of control: model " + AiCopilot.Model + " (adaptive thinking, effort high), one structured recommendation with its $ risk. It never executes by itself on an MT5 account — ✅ EXECUTE asks first. Without a key the feature is off. Price: $4 / $20 per million input / output tokens (cache reads $0.20) — about $0.06 a call, more when it thinks longer.", Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) });
            var status = new TextBlock { Foreground = Cyan, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
            Action show = () => { try { var b = AiBudgetNow(); status.Text = "key: " + (AiKeys.Has ? AiKeyStore.Mask(AiKeys.Load()) + " (encrypted with Windows DPAPI for this user)" : "none — the AI co-pilot is OFF") + "  ·  model " + AiCopilot.Model + "  ·  spent this month $" + b.SpentThisMonth(DateTime.Now).ToString("0.00", IC) + " of $" + b.MonthlyCap.ToString("0", IC) + (aiTest != "" ? "  ·  " + aiTest : ""); } catch { } };
            var pb = new PasswordBox { Width = 380, Height = 30, FontFamily = Sx.Mono, Background = Sx.Bg, Foreground = Text, BorderBrush = Line, CaretBrush = Gold, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = "your Anthropic API key (sk-ant-…) — it is stored encrypted and only ever sent to the Anthropic API" };
            var save = Pill("💾 SAVE KEY", Tint(Green, 90)); save.Height = 30; save.Margin = new Thickness(0, 0, 8, 0);
            save.Click += delegate { var k = (pb.Password ?? "").Trim(); if (k.Length < 20) { Toast("🧠 that does not look like an API key"); return; } try { AiKeys.Save(k); aiAdvisor = null; aiTest = ""; pb.Clear(); Toast("🧠 the API key is saved (encrypted for this Windows user)"); } catch (Exception ex) { Toast("🧠 could not save the key: " + ex.Message); } show(); };
            var del = Pill("🗑 DELETE KEY", Tint(Red, 90)); del.Height = 30; del.Margin = new Thickness(0, 0, 8, 0);
            del.Click += delegate { if (!Sx.Ask(this, "🧠 DELETE THE API KEY", "The AI co-pilot turns off until you add a key again.", "DELETE", "NO", Red)) return; AiKeys.Delete(); aiAdvisor = null; aiTest = ""; show(); };
            var test = Pill("🔌 TEST CONNECTION", Tint(Cyan, 80)); test.Height = 30;
            test.Click += delegate
            {
                var adv = AiAdvisor; if (adv == null) { aiTest = "no key"; show(); return; } aiTest = "testing …"; show();
                Task.Run(async () => { string r; try { r = await adv.TestAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token); } catch (Exception ex) { r = ex.Message; } Dispatcher.BeginInvoke(new Action(() => { aiTest = r == "" ? "✔ connected — " + AiCopilot.Model + " is available" : "✖ " + r; show(); })); });
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) }; row.Children.Add(pb); row.Children.Add(save); row.Children.Add(del); row.Children.Add(test); st.Children.Add(row); st.Children.Add(status);
            var caps = new WrapPanel();
            Func<string, string, double, FrameworkElement> num = (k, label, d) =>
            {
                var tb = Sx.Input(Get(k, d.ToString(IC)), 60); tb.Margin = new Thickness(6, 0, 16, 0);
                tb.LostFocus += delegate { double v; if (double.TryParse(tb.Text, NumberStyles.Float, IC, out v) && v >= 0) { Put(k, v.ToString(IC)); show(); } else tb.Text = Get(k, d.ToString(IC)); };
                var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) }; p.Children.Add(new TextBlock { Text = label, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); p.Children.Add(tb); return p;
            };
            caps.Children.Add(num("ai.cap", "MONTHLY CAP $", 20)); caps.Children.Add(num("ai.pb", "MIN BETWEEN AUTOMATIC CALLS / BASKET", 10)); caps.Children.Add(num("ai.ph", "MAX CALLS / HOUR", 12)); st.Children.Add(caps);
            var trig = new CheckBox { Content = "ask by itself when the ⚠ radar reaches the alert level or a basket is in trouble (within the limits above)", Foreground = Text, IsChecked = Get("ai.triggers", "1") == "1", Margin = new Thickness(0, 2, 0, 4) };
            trig.Click += delegate { Put("ai.triggers", trig.IsChecked == true ? "1" : "0"); }; st.Children.Add(trig);
            var au = new CheckBox { Content = "🧠 AI AUTO — execute the advice by itself (🎮 simulated DEMO only; refused on any MT5 account)", Foreground = Virtual ? Amber : Muted, IsChecked = Get("ai.auto", "0") == "1" && Virtual, IsEnabled = Virtual, ToolTip = Virtual ? "simulated money only — an experiment" : AiCoordinator.AutoNo, Margin = new Thickness(0, 0, 0, 4) };
            au.Click += delegate { if (!Virtual) { au.IsChecked = false; Sx.Info(this, "🧠 AI AUTO", AiCoordinator.AutoNo, Amber); return; } Put("ai.auto", au.IsChecked == true ? "1" : "0"); }; st.Children.Add(au);
            show();
            return new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), BorderBrush = Tint(Violet, 120), BorderThickness = new Thickness(1), Child = st, Margin = new Thickness(0, 8, 0, 8) };
        }
    }
}
