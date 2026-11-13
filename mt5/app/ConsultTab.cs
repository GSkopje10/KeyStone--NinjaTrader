// SpasiX — 🛡 HEDGE ▸ 🧭 CONSULT + 💸 WITHDRAWALS (13s; Consult.cs / Withdraw.cs). The consult's memory per account (the library, labelled as an estimate,
// + the user's real-tick points: his own baskets measured after they close, and 🧭 BUILD FROM MY TICKS), its view, the CONSULT MODE gate, and the
// withdrawal-plan statistic (DEMO: a withdrawal can be booked — asks first; MT5: tracking + a reminder only).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        ConsultMemory aCm; string aCmFor = null; readonly List<ConsultCase> aCq = new List<ConsultCase>(); bool aCqBusy; ConsultAdvice aConsult; DateTime aCmAt = DateTime.MinValue;
        ConsultAdvisor.Track aCmTrack; int aCmTrackN = -1; bool aCmTrackBusy;
        volatile bool aCbBusy, aCbCancel; string aCbStatus = "";
        string ConsultTicksFile(string sym) { return Path.Combine(Dir, "auto", "consult-ticks-" + sym + ".txt"); }
        void ConsultSetup()
        {
            auto.OnConsultPoint += cc => { if (Sim) return; cc.Point.Src = ConsultSrc.MINE; Dispatcher.BeginInvoke(new Action(() => { lock (aCq) aCq.Add(cc); ConsultSaveQueue(); })); };
            auto.OnConsult += a => Dispatcher.BeginInvoke(new Action(() => { aConsult = a; AutoPaint(true); }));
        }
        // the memory of THIS account: the library (estimate) + the real-tick points built from the user's ticks + this account's own measured baskets
        void ConsultEnsure()
        {
            var key = (aHist ?? "") + "|" + (aset != null ? aset.Symbol : "");
            if (aCm != null && aCmFor == key) return; aCmFor = key;
            var m = ConsultMemory.WithLibrary(); var sym = aset != null ? aset.Symbol : "XAUUSD";
            m.Add(ConsultMemory.LoadFile(ConsultTicksFile(sym)));
            if (!Sim && !string.IsNullOrEmpty(aHist)) { m.Add(ConsultMemory.LoadFile(AutoFile("consult"))); lock (aCq) { aCq.Clear(); try { var f = AutoFile("consultq"); if (File.Exists(f)) foreach (var l in File.ReadAllLines(f)) { var t = l.Split('\t'); if (t.Length < 2) continue; var p = ConsultPoint.Parse(t[0]); var c = WhatIfCase.Load(t[1]); if (p != null && c != null) { p.Src = ConsultSrc.MINE; aCq.Add(new ConsultCase { Point = p, Snap = c }); } } } catch { } } }
            aCm = m; auto.ConsultMem = m; aCmTrackN = -1;
            if (Environment.GetEnvironmentVariable("SPASIX_TEST_CONSULT") == "1" && aConsult == null)
            {   // screens.sh only: the advice the memory gives for a typical deep decision (hedge #3, 3 × the target down, 2 h in) — display, nothing is sent
                var x = new double[ConsultFeatures.Names.Length]; x[0] = 3; x[1] = -3; x[2] = 4; x[3] = Math.Log(121); x[4] = -1; x[5] = -2; x[7] = 1; x[8] = -1.5; x[9] = 0.1;
                aConsult = ConsultAdvisor.Advise(m.Points, x, "BREAK", DateTime.Now, (aset ?? new AutoSettings()).Target > 0 ? aset.Target : 10); aConsult.Key = "0|test";
            }
        }
        void ConsultSaveQueue() { if (Sim || string.IsNullOrEmpty(aHist)) return; try { List<string> l; lock (aCq) l = aCq.Select(c => c.Point.Line() + "\t" + c.Snap.Save()).ToList(); Directory.CreateDirectory(Path.GetDirectoryName(AutoFile("consultq"))); File.WriteAllLines(AutoFile("consultq"), l); } catch { } }
        // every minute: the decisions of baskets that have CLOSED are measured in the background (the five options replayed, in-memory SimBroker) → memory
        void AutoConsultRun()
        {
            try { ConsultEnsure(); } catch { }
            ConsultTrackRun();
            if (aCqBusy || Sim) return; List<ConsultCase> todo; lock (aCq) todo = aCq.Where(c => auto.Iterations.Any(x => x.N == c.Point.Iter && x.Start == c.Snap.Start && x.Done)).Take(10).ToList();
            if (todo.Count == 0) return;
            var sym = todo[0].Snap.Symbol; SimSpec spec = null; lock (broker.State.Gate) { Mt5Symbol ms; if (broker.State.Symbols.TryGetValue(sym, out ms)) spec = SimSpec.From(ms, broker.State.ServerOffsetSec, -7, broker.State.Account.Currency); }
            if (spec == null) spec = HistoryStore.LoadSpec(sym); if (spec == null) return;
            var from = todo.Min(c => c.Point.At).Date.AddDays(-2); var bars = AutoCandles(sym, from); string srv = TickServer(); aCqBusy = true;
            var kind = Paper ? paper.Acct.Set.Kind : "STANDARD"; double comm = Paper ? paper.Acct.Set.CommissionPerLot : 0; int lev = Paper ? paper.Acct.Set.Leverage : 500;
            Task.Run(() =>
            {
                var done = new List<ConsultCase>();
                foreach (var c in todo)
                {
                    try
                    {
                        var to = bars.Count > 0 ? bars.Max(b => b.Time) : c.Point.At; List<SimTick> tk = null; try { tk = HistoryStore.LoadTickRange(sym, c.Point.At.Date, to, srv); } catch { }
                        c.Snap.Kind = kind; c.Snap.Commission = comm; c.Snap.Leverage = lev; c.Snap.Spread = -1;
                        ConsultSim.Measure(c, bars, spec, ConsultSim.HorizonMin, tk != null && tk.Count > 100 ? tk : null); c.Point.Src = ConsultSrc.MINE;
                    }
                    catch { }
                    done.Add(c);
                }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    aCqBusy = false; var ok = done.Where(c => c.Point.Complete).Select(c => c.Point).ToList();
                    lock (aCq) foreach (var c in done) aCq.Remove(c); ConsultSaveQueue();
                    if (ok.Count > 0 && aCm != null) { aCm.Add(ok); try { File.AppendAllLines(AutoFile("consult"), ok.Select(p => p.Line())); } catch { } auto.Note("🧭 CONSULT learned " + ok.Count + " decision(s) of your closed basket(s) (each option replayed on the real prices that followed — simulated only)."); }
                    AutoPaint(true);
                }));
            });
        }
        // its own track record on YOUR real-tick decisions (walk-forward: each advised only from what was known before it) — in the background when the memory grows
        void ConsultTrackRun()
        {
            if (aCm == null || aCmTrackBusy) return; int n = aCm.MineCount; if (n == aCmTrackN) return; aCmTrackN = n; aCmTrackBusy = true; var mem = aCm.Points; var s = auto.Set ?? aset;
            Task.Run(() => { ConsultAdvisor.Track t = null; try { t = ConsultAdvisor.TrackOf(mem, mem.Where(p => ConsultSrc.Real(p.Src)).ToList(), s.ConsultK, s.ConsultMinN); } catch { } Dispatcher.BeginInvoke(new Action(() => { aCmTrack = t; aCmTrackBusy = false; })); });
        }
        // the 🛡 HEDGE banner line (only for the open iteration's latest decision)
        string ConsultBannerLine()
        {
            var a = aConsult; var it = auto.Current; if (a == null || it == null || !auto.Running || !(a.Key ?? "").StartsWith(it.N + "|")) return "";
            var s = auto.Set ?? aset; string act = s.ConsultMode == "ADVISE" ? "" : ConsultGate.MayAct(s.ConsultMode, Virtual, HedgeAuto.AccKeyOf(broker)) ? (a.Clear ? " → ACTING" : "") : " (ACT needs the typed CONSULT)";
            return a.Line + act;
        }
        // 🧭 the CONSULT MODE choice: ACT on an MT5 account = the typed CONSULT (this session)
        bool ConsultConfirm()
        {
            var key = HedgeAuto.AccKeyOf(broker); if (Virtual || ConsultGate.Ok(key)) return true;
            var typed = Sx.Prompt(this, "⚠ 🧭 CONSULT on an MT5 account", BadgeLine() + ConsultGate.Dialog(), "");
            if (!ConsultGate.Confirm(key, typed)) { if (typed != null) Sx.Info(this, "🧭 CONSULT", "Not switched on: the word CONSULT was not typed — it keeps ADVISING only.", Amber); return false; }
            Toast("🧭 CONSULT may act on " + key + " (until DISARM / SpasiX closes)"); auto.Note("🧭 CONSULT — ACT ON THIS ACCOUNT confirmed by typing for this session"); return true;
        }
        List<Mi> ConsultModeItems()
        {
            return new List<Mi> {
                new Mi { Text = "ADVISE (the default) — it compares and SHOWS the advice; nothing is sent because of it", Fg = Green, Do = () => aset.ConsultMode = "ADVISE" },
                new Mi { Text = "ACT ON SIMULATED — it follows a CLEAR advice on the DEMO / BACKTEST / RACE only (an MT5 account: advise only). ⚠ The 6-year bench: acting made it WORSE (−$357k vs −$258k, 47 vs 38 blow-ups) — an estimate on futures candles; watch the 🏁 lane CNS", Fg = Amber, Do = () => aset.ConsultMode = "SIM" },
                new Mi { Text = "ACT ON THIS ACCOUNT — " + (Virtual ? "this simulated account follows a CLEAR advice" : "REAL MONEY: it follows a CLEAR advice here after you type CONSULT (again after every restart and DISARM); ARM, PROP's limits, STOP ON MANUAL and the EXIT GUARD all apply"), Fg = Virtual ? Amber : Red,
                    Do = () => { if (!Virtual && !ConsultConfirm()) return; aset.ConsultMode = "ACT"; } } };
        }
        string ConsultModeText(AutoSettings s) { return s.ConsultMode == "SIM" ? "ACT ON SIMULATED" : s.ConsultMode == "ACT" ? "ACT ON THIS ACCOUNT" + (Virtual || ConsultGate.Ok(HedgeAuto.AccKeyOf(broker)) ? "" : " (not confirmed: advising)") : "ADVISE"; }
        // 🧭 BUILD FROM MY TICKS: the downloaded / recorded real-tick days of the symbol → the user's settings replayed with the alternatives → TICKS points
        void ConsultBuildFromTicks()
        {
            if (aCbBusy) { if (Sx.Ask(this, "🧭 BUILD FROM MY TICKS", "It is building (" + aCbStatus + "). Stop it?", "STOP", "GO ON", Amber)) aCbCancel = true; return; }
            var s = AutoRead(); var sym = s.Symbol; var days = new List<DateTime>(); for (var d = DateTime.Today.AddDays(-400); d < DateTime.Today; d = d.AddDays(1)) if (HistoryStore.HasAnyTicks(sym, d)) days.Add(d);
            if (days.Count == 0) { Sx.Info(this, "🧭 BUILD FROM MY TICKS", "No real-tick day of " + sym + " is on this PC — 🧪 BACKTEST ▸ ⬇ DOWNLOAD REAL TICKS first (the live ticks SpasiX records count too).", Violet); return; }
            var have = ConsultMemory.LoadFile(ConsultTicksFile(sym)); var doneDays = new HashSet<DateTime>(have.Select(p => p.At.Date)); var runs = ConsultBuilder.Runs(days.Where(d => !doneDays.Contains(d)));
            if (runs.Count == 0) { Sx.Info(this, "🧭 BUILD FROM MY TICKS", "Every tick day of " + sym + " is already in the memory (" + have.Count + " decisions).", Violet); return; }
            if (!Sx.Ask(this, "🧭 BUILD FROM MY TICKS", "Replay YOUR settings on " + runs.Sum(r => r.Count) + " real-tick day(s) of " + sym + " (" + runs.Count + " run(s)) and measure every hedge decision's five options on the same ticks.\nIn the background, simulated only (never MT5); it can take a while. Go?", "BUILD", "CANCEL", Violet)) return;
            SimSpec spec = null; lock (broker.State.Gate) { Mt5Symbol ms; if (broker.State.Symbols.TryGetValue(sym, out ms)) spec = SimSpec.From(ms, broker.State.ServerOffsetSec, -7, broker.State.Account.Currency); } if (spec == null) spec = HistoryStore.LoadSpec(sym);
            if (spec == null) { Sx.Info(this, "🧭 BUILD FROM MY TICKS", "The symbol's specification is not known yet — connect MT5 (or download candles of " + sym + " once).", Amber); return; }
            aCbBusy = true; aCbCancel = false; aCbStatus = "starting"; double bal = Math.Max(1000, AutoBal()); var file = ConsultTicksFile(sym);
            Task.Run(() =>
            {
                int total = 0; int ri = 0;
                foreach (var run in runs)
                {
                    if (aCbCancel) break; ri++; aCbStatus = "run " + ri + " / " + runs.Count + " (" + run[0].ToString("dd MMM", IC) + " – " + run[run.Count - 1].ToString("dd MMM yyyy", IC) + ")";
                    List<ConsultPoint> pts = null;
                    try
                    {
                        var ticks = new List<SimTick>(); foreach (var d in run) ticks.AddRange(HistoryStore.LoadTicks(sym, d)); ticks.Sort((a, b) => a.T.CompareTo(b.T));
                        var bars = HistoryStore.LoadRange(sym, run[0].AddDays(-2), run[run.Count - 1]); if (bars.Count(b => b.Time >= run[0]) < 100) bars = ConsultBuilder.BarsFromTicks(ticks);
                        pts = ConsultBuilder.FromRun(s, spec, bars, ticks, bal, () => aCbCancel, st => aCbStatus = "run " + ri + " / " + runs.Count + ": " + st);
                    }
                    catch (Exception ex) { aCbStatus = "✖ " + ex.Message; }
                    if (pts != null && pts.Count > 0) { try { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.AppendAllLines(file, pts.Select(p => p.Line())); } catch { } total += pts.Count; var pp = pts; Dispatcher.BeginInvoke(new Action(() => { if (aCm != null) aCm.Add(pp); })); }
                }
                Dispatcher.BeginInvoke(new Action(() => { aCbBusy = false; aCbStatus = (aCbCancel ? "■ stopped — " : "✓ done — ") + total + " decision(s) from your real ticks added"; Toast("🧭 BUILD FROM MY TICKS: " + aCbStatus); auto.Note("🧭 " + aCbStatus); aCmTrackN = -1; AutoPaint(true); }));
            });
        }
        // the 🧭 CONSULT view
        List<string> ConsultLines()
        {
            try { ConsultEnsure(); } catch { }
            var L = new List<string>(); var s = auto.Set ?? aset; var m = aCm;
            int lib = m == null ? 0 : m.Count - m.MineCount, real = m == null ? 0 : m.MineCount, mine = m == null ? 0 : m.Points.Count(p => p.Src == ConsultSrc.MINE), tk = real - mine;
            L.Add("🧭 HISTORY CONSULT — at each hedge decision the basket is compared with the similar past decisions; MODE: " + ConsultModeText(s) + " (⚙ SETTINGS ▸ 🧭 CONSULT MODE)");
            L.Add("   MEMORY: " + real + " decisions on YOUR real ticks (" + mine + " from your own baskets, " + tk + " from 🧭 BUILD FROM MY TICKS) + " + lib + " in the 6-year library — " + ConsultSrc.LibLabel.ToUpperInvariant() + ", weighted below yours (used only when fewer than " + s.ConsultMinN + " of yours are known)");
            if (m != null && m.LibraryNote != "") L.Add("   library: " + m.LibraryNote);
            L.Add("   " + (aCbBusy ? "🧭 BUILD FROM MY TICKS: " + aCbStatus + " (⚙ SETTINGS ▸ 🧭 BUILD FROM MY TICKS again to stop)" : aCbStatus != "" ? "🧭 BUILD FROM MY TICKS: " + aCbStatus : "🧭 BUILD FROM MY TICKS (⚙ SETTINGS): your settings + the alternatives replayed on the real ticks you downloaded in 🧪 BACKTEST"));
            int q; lock (aCq) q = aCq.Count; if (q > 0) L.Add("   " + q + " decision(s) of your open / recent baskets wait to be measured (after the basket closes)");
            L.Add("");
            var a = aConsult;
            if (a == null) L.Add("THE LAST DECISION: none yet — the advice appears at the next hedge decision of a running basket.");
            else
            {
                L.Add("THE LAST DECISION " + a.At.ToString("dd MMM HH:mm", IC) + " (#" + (a.Key ?? "").Split('|')[0] + "): " + a.Line);
                L.Add(string.Format("   {0,-28} {1,5} {2,10} {3,22} {4,10} {5,12} {6,9} {7,24}", "OPTION", "n", "AVERAGE", "95 % INTERVAL", "RECOVERED", "WORST AFTER", "MINUTES", "VS HEDGE NOW (95 %)"));
                foreach (var r in a.Rows) L.Add(string.Format(IC, "   {0,-28} {1,5} {2,10} {3,22} {4,9:0}% {5,12} {6,9:0} {7,24}", ConsultOpt.Name(r.Opt), r.N, ConsultAdvisor.Usd(r.Mean), ConsultAdvisor.Usd(r.Lo) + " … " + ConsultAdvisor.Usd(r.Hi), r.Recov, ConsultAdvisor.Usd(r.Worst), r.Minutes,
                    r.Opt == ConsultOpt.HEDGE ? "(the engine)" : ConsultAdvisor.Usd(r.Diff) + " (" + ConsultAdvisor.Usd(r.DiffLo) + " … " + ConsultAdvisor.Usd(r.DiffHi) + ")"));
                L.Add("   VERDICT: " + (a.Clear ? "CONSULT says " + ConsultOpt.Name(a.Says).ToUpperInvariant() + " — " + a.Why : "no clear answer — your rules (" + a.Why + ")") + a.SourceNote);
                L.Add(""); L.Add("   THE NEAREST SIMILAR DECISIONS (what each option did there, × that basket's target):");
                foreach (var p in a.Neighbours.Take(12)) L.Add("   " + p.At.ToString("yyyy-MM-dd HH:mm", IC) + "  " + (p.Src == ConsultSrc.LIB ? "📚 library (estimate)" : p.Src == ConsultSrc.TICKS ? "🎞 your ticks" : "✋ your basket").PadRight(22) + " hedge #" + p.X[0].ToString("0", IC) + "  depth " + p.X[1].ToString("0.0", IC) + "×  →  "
                    + string.Join("  ", ConsultOpt.All.Select(o => ConsultOpt.Short(o) + " " + (p.R(o) >= 0 ? "+" : "") + p.R(o).ToString("0.0", IC) + "×")));
            }
            L.Add("");
            L.Add("ITS TRACK RECORD on your real-tick decisions (each judged only from what was known before it): " + (aCmTrack == null ? (real == 0 ? "nothing measured yet" : "computing…") : aCmTrack.Text));
            if (m != null && m.TrackLine != "") L.Add("   on the 6-year library (" + ConsultSrc.LibLabel + "): " + m.TrackLine);
            L.Add("   THE 6-YEAR BENCH (" + ConsultSrc.LibLabel + "): acting on the advice made it WORSE — −$356,617 / 47 blow-ups vs your settings −$257,875 / 38 (🛡 RISK GUARD: −$25,714 / 4). That is why it only ADVISES by default; mt5/docs/consult-study.md.");
            L.Add("   It never acts without a CLEAR answer; ADVISE (the default) never sends anything because of it.");
            return L;
        }
        // ---- 💸 WITHDRAWAL PLAN (a statistic)
        WdStats WithdrawStats()
        {
            var s = auto.Set ?? aset; List<Mt5Deal> deals; double bal; DateTime now; lock (broker.State.Gate) { deals = broker.State.Deals.Where(d => d.Type == "BALANCE").ToList(); bal = broker.State.Account.Balance; now = broker.State.ServerNow > DateTime.MinValue ? broker.State.ServerNow : DateTime.Now; }
            return WithdrawPlan.Of(deals, bal, s.WdStart, s.WdAtPct, now);
        }
        List<string> WithdrawLines() { var s = auto.Set ?? aset; try { return WithdrawPlan.Lines(WithdrawStats(), Paper, s.WdAtPct); } catch { return new List<string>(); } }
        string WithdrawBanner() { try { var st = WithdrawStats(); if (!st.Due) return ""; return Paper ? "💸 withdrawal due: " + WithdrawPlan.ProgressText(st) + " — 🛡 HEDGE ▸ 💸 WITHDRAW (simulated)" : "💸 withdrawal due (" + WithdrawPlan.Usd(st.Balance) + " ≥ " + WithdrawPlan.Usd(st.Level) + ") — do it at your broker; SpasiX never moves money"; } catch { return ""; } }
        // DEMO only: book the withdrawal (a balance deal), asking first
        void WithdrawBook()
        {
            if (!Paper) { Sx.Info(this, "💸 WITHDRAWAL PLAN", "On an MT5 account SpasiX only TRACKS the plan (the broker's deposits / withdrawals) — it never moves money. Withdraw at your broker.", Cyan); return; }
            var st = WithdrawStats(); var s = auto.Set ?? aset; double amt = WithdrawPlan.AmountNow(st.Balance, s.WdStart);
            if (!st.Due || amt <= 0) { Sx.Info(this, "💸 WITHDRAWAL PLAN", "Not due yet: " + WithdrawPlan.ProgressText(st) + ".", Cyan); return; }
            if (!Sx.Ask(this, "💸 WITHDRAWAL PLAN", "Withdraw " + WithdrawPlan.Usd(amt) + " now? (simulated)\nThe DEMO balance goes from " + WithdrawPlan.Usd(st.Balance) + " back to " + WithdrawPlan.Usd(s.WdStart) + " (a balance deal; open positions stay).", "WITHDRAW", "CANCEL", Green)) return;
            DateTime now; lock (broker.State.Gate) { now = broker.State.ServerNow > DateTime.MinValue ? broker.State.ServerNow : DateTime.Now; paper.Acct.SetBalance(st.Balance - amt, now, WithdrawPlan.SimComment); }
            auto.Note("💸 WITHDRAWAL (simulated) " + WithdrawPlan.Usd(amt) + " — the plan continues at " + WithdrawPlan.Usd(s.WdStart)); Toast("💸 withdrawn " + WithdrawPlan.Usd(amt) + " (simulated)"); AutoPaint(true);
        }
    
        // ---- 🍂 13s LOOK (Look.cs / Shell): the ⚙ SETUP card + what this window's header line knows (read only)
        SxLookCtx LookCtxNow()
        {
            var c = new SxLookCtx(); var s = auto != null ? (auto.Set ?? aset) : aset;
            try
            {
                lock (broker.State.Gate)
                {
                    c.Server = broker.State.ServerNow; c.Balance = broker.State.Account.Balance; var day = c.Server.Date;
                    if (c.Server > DateTime.MinValue) c.DayPl = broker.State.Deals.Where(d => d.IsTrade && d.Time.Date == day).Sum(d => d.Net);
                }
                if (s != null) { c.NyOff = s.NyOffsetHours; c.CloseGapMin = s.CloseGap ? s.CloseGapMin : 0; c.Target = s.Target; c.WdStart = s.WdStart; c.WdLevel = WithdrawPlan.LevelOf(s.WdStart, s.WdAtPct); }
                if (s != null && !s.CloseGap) c.CloseGapMin = 30;   // the reminder only (no rule)
                if (auto != null && auto.Running && auto.Current != null) { c.Running = true; c.Total = auto.LiveTotal().Item1; }
                c.Simulated = Paper; if (Sim) { c.WdLevel = double.NaN; }
            }
            catch { }
            return c;
        }
        FrameworkElement LookCard()
        {
            var st = new StackPanel(); var lk = Shell.Look;
            st.Children.Add(new TextBlock { Text = "🍂 LOOK — the seasons (accents only)", Foreground = Gold, FontSize = 15, FontWeight = FontWeights.Bold });
            st.Children.Add(new TextBlock { Text = "The title band, its glow line and a drawn scene take the season's colours (Fall amber / copper, Winter ice / silver, Spring mint / pink, Summer gold / coral) — never the P/L green / red, never the candles. One header line: the urgent reminders first (the 17:00 NY close, a basket in drawdown, a withdrawal near, today's P/L), else a season quote. Nothing here can be clicked by mistake or sends anything.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
            var row = new WrapPanel();
            Action<Button, bool> lit = (b, on) => { b.Background = on ? (Brush)Tint(Cyan, 200) : Card; b.Foreground = on ? Bg : Text; };
            var sb = new List<Tuple<string, Button>>();
            foreach (var x in new[] { "AUTO", "FALL", "WINTER", "SPRING", "SUMMER", "OFF" })
            {
                var xx = x; var b = Pill(x == "AUTO" ? "SEASON AUTO" : x == "OFF" ? "OFF" : SxSeasons.Icon(x) + " " + x, Card); b.Height = 30; b.Margin = new Thickness(0, 0, 6, 6); sb.Add(Tuple.Create(x, b));
                b.Click += delegate { var l = Shell.Look; l.Season = xx; Shell.SetLook(l); foreach (var t in sb) lit(t.Item2, t.Item1 == xx); };
                lit(b, lk.Season == x); row.Children.Add(b);
            }
            st.Children.Add(row);
            var row2 = new WrapPanel();
            Func<string, Func<SxLook, bool>, Action<SxLook, bool>, Button> tog = (label, get, set) =>
            {
                var b = Pill("", Card); b.Height = 30; b.Margin = new Thickness(0, 0, 6, 6); Action paint = () => { bool on = get(Shell.Look); b.Content = label + ": " + (on ? "ON" : "OFF"); lit(b, on); };
                b.Click += delegate { var l = Shell.Look; set(l, !get(l)); Shell.SetLook(l); paint(); }; paint(); return b;
            };
            row2.Children.Add(tog("🏔 SCENE IN THE TITLE BAND", l => l.Scene, (l, v) => l.Scene = v));
            row2.Children.Add(tog("💬 HEADER LINE (reminders + quotes)", l => l.Quotes, (l, v) => l.Quotes = v));
            row2.Children.Add(tog("✨ ANIMATION (light)", l => l.Anim, (l, v) => l.Anim = v));
            st.Children.Add(row2);
            return new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 12), BorderBrush = Line, BorderThickness = new Thickness(1), Child = st, Margin = new Thickness(0, 12, 0, 0) };
        }
    }
}
