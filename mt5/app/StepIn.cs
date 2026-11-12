// SpasiX — 🚑 STEP IN (09n, the user's request: "learn from the saves and build the STEP IN mode — at that point we are willing to risk the whole account,
// larger lot sizes following the trend, trimming profit, trailing profit, closing … managing on steroids, like it's the last thing the algo can do").
// Learned from his own step-ins (mt5/docs/step-in-study.md): he stepped in when a basket was ~8 × its target deep or stuck ~2 h, and what saved the
// baskets was (a) going WITH the trend in size and stopping the flip-flop, (b) cutting losers against winners, (c) freezing / cutting a pure chop (#83).
// When a basket reaches the TRIGGER, the normal hedge logic hands the iteration over to this manager until it closes:
//   GOAL      the iteration TOTAL (open + this iteration's closed legs) ≥ the recovery goal (TARGET = breakeven + the target, or BE) → CLOSE ALL
//   FLOOR     the equity reached the floor (MAX RISK / the $ floor) → neutralise (net 0) and add nothing more — it never closes at a loss by itself
//   TRAIL     the stacked lots' profit is trailed (TrailAtr × ATR M5 behind their best) → the stack closes when it gives back
//   FLIP      the trend turned against the stack (≥ 75) or the stack is StepInStopAtr × ATR M5 against it (its stop) → the stack closes
//   TRIM      every TrimAtr × ATR M5 in the stack's favour → TrimPct % of the stack is banked
//   CUT PAIR  a counter-trend loser + a with-trend winner whose sum ≥ $0 → both closed (the same lots: the net, the total unchanged)
//   HOLD      no trend → net 0 once (the chop lesson of #83 / 05 Nov #5), nothing else until a trend shows
//   STACK     a confirmed trend + a NEW finished swing break (M1 or M5) → size UP the trend's way: enough to reach the goal in MoveK × ATR M5,
//             capped by the risk budget (the equity above the floor over RiskMoveK × ATR M5 against), the margin guard and the bridge's max lots
// Every action goes through HedgeAuto's Ask → AutoSend (✋ MANUAL MANAGE → 💡). SIMULATED accounts (🎮 DEMO / 🏁 RACE / 🧪 BACKTEST): ON in the settings, the
// default floor 0 = it may blow (a blow-up is recorded, 💥 AUTO RESTART resets the DEMO / RACE). MT5 accounts (user's decision, 09n): OFF by default, an
// OPT-IN per session — the typed STEP IN (and WHOLE ACCOUNT for 100 %) again after every SpasiX restart and after DISARM, MAX RISK default 50 %, the margin
// guard keeps the margin level ≥ 1.5 × the stop-out level, PROP's limits still refuse new entries, never a loss close by itself.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class StepInAction
    {
        public string Kind = "NONE";   // NONE / ORDER / CLOSE (Ticket, Lots: 0 = all) / CLOSEALL
        public string Tag = "";        // GOAL, FLOOR, TRAIL, FLIP, TRIM, CUT, CUT2, HOLD, STACK
        public int Dir; public double Lots; public long Ticket; public string Why = "";
        public static StepInAction None(string why = "") { return new StepInAction { Why = why }; }
    }

    // what the manager sees on one tick (HedgeAuto fills it from its snapshot; tests fill it by hand)
    public sealed class StepInView
    {
        public List<Mt5Position> Pos = new List<Mt5Position>(); public List<Mt5Bar> M1 = new List<Mt5Bar>(); public TrendState Trend;
        public double Total, Target, Bid = double.NaN, Ask = double.NaN, PerPrice, Balance, Equity, Margin, MarginLevel, MarginPerLot, StopOutPct = 50, Step = 0.01, MaxOrder;
        public bool Simulated = true; public DateTime Now;
        public double Net { get { return Math.Round(Pos.Sum(p => p.Dir * p.Lots), 2); } }
        public double Gross { get { return Math.Round(Pos.Sum(p => p.Lots), 2); } }
    }

    // one step-in, from the trigger to the iteration's end (…-stepin.csv)
    public sealed class StepInEpisode
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public string Account = "", Mode = "SIM", Trend0 = "", Why = "", Outcome = "OPEN", Reason = "";
        public int Iter, Stacks, Trims, Cuts, Trails, Flips; public DateTime IterStart, At, End;
        public double Total0, Target, X0, Pct0, Minutes0, Eq0, Floor, Goal, Worst, MaxGross, Trimmed, Result = double.NaN, Alone = double.NaN; public bool Blown, FloorHit; public string AloneReason = "";
        public double WorstX { get { return Target > 0 ? Worst / Target : 0; } }
        public double MinutesToEnd { get { return End > At ? (End - At).TotalMinutes : double.NaN; } }
        public const string Header = "account,mode,iteration,iteration_start,stepin_at,total_at_trigger,target,x_target,pct_balance,minutes_open,trend,equity,floor,goal,worst_after,max_lots,stacks,trims,trimmed,cuts,trails,flips,end,result,outcome,blown,floor_hit,reason,alone_result,alone_reason,why";
        public string Csv()
        {
            Func<double, string> d = v => double.IsNaN(v) ? "" : v.ToString("0.##", IC); Func<string, string> q = s => "\"" + (s ?? "").Replace("\"", "'") + "\"";
            return string.Join(",", q(Account), Mode, Iter.ToString(IC), IterStart.ToString("yyyy-MM-dd HH:mm:ss", IC), At.ToString("yyyy-MM-dd HH:mm:ss", IC), d(Total0), d(Target), d(X0), d(Pct0), d(Minutes0), q(Trend0), d(Eq0), d(Floor), d(Goal), d(Worst), d(MaxGross),
                Stacks.ToString(IC), Trims.ToString(IC), d(Trimmed), Cuts.ToString(IC), Trails.ToString(IC), Flips.ToString(IC), End > DateTime.MinValue ? End.ToString("yyyy-MM-dd HH:mm:ss", IC) : "", d(Result), Outcome, Blown ? "1" : "0", FloorHit ? "1" : "0", q(Reason), d(Alone), q(AloneReason), q(Why));
        }
        static List<string> Split(string l)
        {
            var r = new List<string>(); var cur = new System.Text.StringBuilder(); bool inq = false;
            foreach (var ch in l) { if (ch == '"') inq = !inq; else if (ch == ',' && !inq) { r.Add(cur.ToString()); cur.Clear(); } else cur.Append(ch); }
            r.Add(cur.ToString()); return r;
        }
        public static StepInEpisode Parse(string line)
        {
            try
            {
                var a = Split(line); if (a.Count < 31) return null; Func<int, double> d = i => a[i] == "" ? double.NaN : double.Parse(a[i], IC); Func<int, DateTime> t = i => a[i] == "" ? DateTime.MinValue : DateTime.ParseExact(a[i], "yyyy-MM-dd HH:mm:ss", IC);
                return new StepInEpisode { Account = a[0], Mode = a[1], Iter = int.Parse(a[2], IC), IterStart = t(3), At = t(4), Total0 = d(5), Target = d(6), X0 = d(7), Pct0 = d(8), Minutes0 = d(9), Trend0 = a[10], Eq0 = d(11), Floor = d(12), Goal = d(13), Worst = d(14), MaxGross = d(15),
                    Stacks = int.Parse(a[16], IC), Trims = int.Parse(a[17], IC), Trimmed = d(18), Cuts = int.Parse(a[19], IC), Trails = int.Parse(a[20], IC), Flips = int.Parse(a[21], IC), End = t(22), Result = d(23), Outcome = a[24], Blown = a[25] == "1", FloorHit = a[26] == "1", Reason = a[27], Alone = d(28), AloneReason = a[29], Why = a[30] };
            }
            catch { return null; }
        }
        public static List<StepInEpisode> LoadAll(string file) { var r = new List<StepInEpisode>(); try { if (File.Exists(file)) foreach (var l in File.ReadAllLines(file).Skip(1)) { var e = Parse(l); if (e != null) r.Add(e); } } catch { } return r; }
        public static void SaveAll(string file, IEnumerable<StepInEpisode> eps) { try { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllLines(file, new[] { Header }.Concat(eps.Select(e => e.Csv()))); } catch { } }
        // the outcome words: BLOWN (a stop-out / the account gone), GREEN (≥ half the target), BE (within one target of $0), LOSS (closed lower — by hand, the floor)
        public static string Classify(double result, double target, bool blown)
        {
            if (blown) return "BLOWN"; if (double.IsNaN(result)) return "OPEN";
            double t = Math.Max(0.01, Math.Abs(target)); return result >= 0.5 * t ? "GREEN" : result >= -t ? "BE" : "LOSS";
        }
    }

    // 🚑 the statistics (pure: tested) — the panel in LEARN / ANALYSIS, the RACE scoreboard, the backtest report
    public sealed class StepInStats
    {
        public int N, Green, Be, Loss, Blown, Open, WithAlone, AloneGreen, AloneBlownOrStop; public double Net, AvgWorstX, WorstX, AvgMin, AtTrigger, AloneNet, AvgX0;
        public List<Tuple<string, int, int, int, int, double>> Bands = new List<Tuple<string, int, int, int, int, double>>();   // band, n, green, be, blown, net
        public double Pct(int k) { int done = N - Open; return done > 0 ? 100.0 * k / done : 0; }
        public static readonly double[] BandEdges = { 5, 10, 20, 40 };
        public static string BandOf(double x0)
        {
            double a = Math.Abs(x0); if (a < 5) return "< 5×"; if (a < 10) return "5–10×"; if (a < 20) return "10–20×"; if (a < 40) return "20–40×"; return "≥ 40×";
        }
        public static StepInStats Of(IEnumerable<StepInEpisode> eps)
        {
            var l = (eps ?? new StepInEpisode[0]).ToList(); var s = new StepInStats { N = l.Count };
            var done = l.Where(e => e.Outcome != "OPEN").ToList(); s.Open = l.Count - done.Count;
            s.Green = done.Count(e => e.Outcome == "GREEN"); s.Be = done.Count(e => e.Outcome == "BE"); s.Loss = done.Count(e => e.Outcome == "LOSS"); s.Blown = done.Count(e => e.Outcome == "BLOWN");
            s.Net = Math.Round(done.Sum(e => double.IsNaN(e.Result) ? 0 : e.Result), 2); s.AtTrigger = Math.Round(done.Sum(e => e.Total0), 2);
            if (l.Count > 0) { s.AvgWorstX = Math.Round(l.Average(e => e.WorstX), 1); s.WorstX = Math.Round(l.Min(e => e.WorstX), 1); s.AvgX0 = Math.Round(l.Average(e => e.X0), 1); }
            var rec = done.Where(e => (e.Outcome == "GREEN" || e.Outcome == "BE") && !double.IsNaN(e.MinutesToEnd)).ToList(); s.AvgMin = rec.Count > 0 ? Math.Round(rec.Average(e => e.MinutesToEnd), 0) : double.NaN;
            var wa = done.Where(e => !double.IsNaN(e.Alone)).ToList(); s.WithAlone = wa.Count; s.AloneNet = Math.Round(wa.Sum(e => e.Alone), 2); s.AloneGreen = wa.Count(e => e.Alone >= 0.5 * Math.Max(0.01, e.Target));
            s.AloneBlownOrStop = wa.Count(e => (e.AloneReason ?? "").Contains("HARD STOP") || (e.AloneReason ?? "").Contains("STOP OUT") || (e.AloneReason ?? "").Contains("BLOWN"));
            foreach (var g in done.GroupBy(e => BandOf(e.X0)).OrderBy(g => Array.IndexOf(new[] { "< 5×", "5–10×", "10–20×", "20–40×", "≥ 40×" }, g.Key)))
                s.Bands.Add(Tuple.Create(g.Key, g.Count(), g.Count(e => e.Outcome == "GREEN"), g.Count(e => e.Outcome == "BE"), g.Count(e => e.Outcome == "BLOWN"), Math.Round(g.Sum(e => double.IsNaN(e.Result) ? 0 : e.Result), 2)));
            return s;
        }
        // 🚑 the step-in context of a blow-up (blowups-*.csv): "stepin,x_at_trigger,worst,stacks,outcome" — the open episode first, else the last one
        public static string BlowCtx(StepInEpisode active, StepInEpisode last)
        {
            var ep = active ?? last; if (ep == null) return "no-stepin,,,,"; var ic = CultureInfo.InvariantCulture;
            return "stepin," + ep.X0.ToString("0.0", ic) + "," + ep.Worst.ToString("0.00", ic) + "," + ep.Stacks + "," + (active != null ? "ACTIVE" : ep.Outcome);
        }
        static string U(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture); }
        public List<string> Lines(string title)
        {
            var IC = CultureInfo.InvariantCulture; var L = new List<string>();
            if (N == 0) { L.Add(title + ": no step-in yet."); return L; }
            int done = N - Open;
            L.Add(title + ": " + N + " step-in" + (N == 1 ? "" : "s") + (Open > 0 ? " (" + Open + " still open)" : "") + " — recovered GREEN " + Green + " (" + Pct(Green).ToString("0", IC) + " %) • back to BREAKEVEN " + Be + " (" + Pct(Be).ToString("0", IC) + " %) • closed at a LOSS " + Loss + " (" + Pct(Loss).ToString("0", IC) + " %) • 💥 BLOWN " + Blown + " (" + Pct(Blown).ToString("0", IC) + " %)");
            L.Add("   depth at the trigger avg " + AvgX0.ToString("0.0", IC) + "× the target • after the step-in: avg worst " + AvgWorstX.ToString("0.0", IC) + "×, the worst " + WorstX.ToString("0.0", IC) + "× • time to recover " + (double.IsNaN(AvgMin) ? "—" : AvgMin.ToString("0", IC) + " min") + " (avg)");
            L.Add("   $ net of all step-ins " + U(Net) + " vs CLOSE AT THE TRIGGER " + U(AtTrigger) + (WithAlone > 0 ? " vs THE PLAIN AUTOMATION ALONE " + U(AloneNet) + " (" + WithAlone + " replayed: " + AloneGreen + " green, " + AloneBlownOrStop + " hard stop / blown)" : " • the automation alone: not replayed yet"));
            foreach (var b in Bands) L.Add(string.Format(IC, "   trigger depth {0,-7} n {1,3}  green {2,3}  breakeven {3,3}  blown {4,3}  net {5}", b.Item1, b.Item2, b.Item3, b.Item4, b.Item5, U(b.Item6)));
            if (done < 20) L.Add("   ⚠ small sample (" + done + " finished) — not a statistic yet.");
            return L;
        }
    }

    // ✋ the per-session typed confirmation for an MT5 account (in memory only: again after every SpasiX restart and after DISARM)
    public static class StepInGate
    {
        static readonly Dictionary<string, bool> ok = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);   // account → WHOLE ACCOUNT confirmed too
        public const string Word = "STEP IN", WholeWord = "WHOLE ACCOUNT";
        public static string KeyOf(Mt5Account a) { return a == null ? "" : a.Login.ToString(CultureInfo.InvariantCulture) + "@" + (a.Server ?? ""); }
        public static bool Typed(string typed, string word) { return typed != null && string.Join(" ", typed.Trim().ToUpperInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) == word; }
        // the typed words → confirmed (WHOLE ACCOUNT needed for a MAX RISK of 100 %)
        public static bool Confirm(string key, string typed, double riskPct, string typedWhole = null)
        {
            if (string.IsNullOrEmpty(key) || !Typed(typed, Word)) return false;
            bool whole = riskPct >= 99.999; if (whole && !Typed(typedWhole, WholeWord)) return false;
            lock (ok) ok[key] = whole; return true;
        }
        public static bool Ok(string key, double riskPct) { lock (ok) { bool w; return !string.IsNullOrEmpty(key) && ok.TryGetValue(key, out w) && (riskPct < 99.999 || w); } }
        public static void Revoke(string key) { lock (ok) ok.Remove(key ?? ""); }
        public static void RevokeAll() { lock (ok) ok.Clear(); }   // a new SpasiX session (tests: a restart)
        public static string Dialog(double riskPct, double balance)
        {
            var IC = CultureInfo.InvariantCulture;
            return "🚑 STEP IN on a REAL MONEY / MT5 account.\n\nWhen a basket is deep (the trigger), STEP IN takes the iteration over: it ADDS LARGER LOTS WITH THE TREND, trims, trails and cuts pairs to recover the basket.\n"
                + "It may lose up to the MAX RISK of this account: " + riskPct.ToString("0", IC) + " % of the balance at the moment it activates (≈ $" + (balance * riskPct / 100).ToString("N0", IC) + " at today's $" + balance.ToString("N0", IC) + ").\n"
                + "At that floor it stops adding and NEUTRALISES (full hedge, net 0) — it never closes the basket at a loss by itself: you decide.\n"
                + "It never sends an order that would take the margin level under 1.5 × the broker's stop-out level. PROP: every firm limit still refuses new entries.\n"
                + "💥 AUTO RESTART never happens on a real account.\n\nThis confirmation lasts until DISARM or until SpasiX closes.\nType STEP IN to switch it on.";
        }
        public const string NotConfirmed = "🚑 STEP IN is ON for this MT5 account but not confirmed in this session — type STEP IN in ⚙ SETTINGS ▸ 🚑 STEP IN (again after every SpasiX restart and after DISARM), or turn it OFF.";
        public const string LiveNote = "⛔ REAL MONEY: on this MT5 account STEP IN is an opt-in — the typed STEP IN each session, MAX RISK (default 50 %), at the floor it neutralises and never closes at a loss, the margin guard (≥ 1.5 × the stop-out level). PROP: the firm's limits still refuse new entries — STEP IN cannot bypass them.";
        public const string SimNote = "🎮 simulated account: STEP IN may use the WHOLE account (floor $0 = it may blow) — every blow-up is recorded and 💥 AUTO RESTART resets the account, so the statistics accumulate.";
    }

    public sealed class StepInManager
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public bool Active, Frozen, Held; public int Iter = -1; public StepInEpisode Ep; public readonly HashSet<long> Stack = new HashSet<long>();
        public double FloorEq, Goal, TrailPx = double.NaN, BestPx = double.NaN; public int StackTrims; public string Text = "", LastTrend = "";
        double usedSwing = double.NaN, pairOwe; long pairTicket; bool trailHit, flipping; public long TrimTicket;
        public void Reset() { Active = Frozen = Held = false; Iter = -1; Ep = null; Stack.Clear(); FloorEq = 0; Goal = 0; TrailPx = BestPx = double.NaN; StackTrims = 0; Text = ""; usedSwing = double.NaN; pairOwe = 0; pairTicket = 0; trailHit = flipping = false; }
        public static string P(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.00", IC); }
        static string U(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", IC); }
        // the trigger (pure): the basket's total vs the depth ($ or × the target), or stuck for StepInMin minutes and at least StepInMinX × the target deep
        public static string Trigger(AutoSettings s, double total, double target, double minutesOpen)
        {
            double depth = s.StepInUsd > 0 ? s.StepInUsd : s.StepInX * Math.Max(0.01, target);
            if (total <= -depth) return "the basket " + U(total) + " ≤ −" + (s.StepInUsd > 0 ? "$" + s.StepInUsd.ToString("0.##", IC) : s.StepInX.ToString("0.#", IC) + " × the target ($" + depth.ToString("0.##", IC) + ")");
            if (s.StepInMin > 0 && minutesOpen >= s.StepInMin && total <= -s.StepInMinX * Math.Max(0.01, target)) return "stuck " + minutesOpen.ToString("0", IC) + " min (≥ " + s.StepInMin + ") at " + U(total) + " (≤ −" + s.StepInMinX.ToString("0.#", IC) + " × the target)";
            return "";
        }
        // the floor (pure): the equity it may not go under — the $ floor, or what MAX RISK % of the equity at the trigger leaves
        public static double FloorOf(AutoSettings s, double eq0) { return Math.Max(Math.Max(0, s.StepInFloor), s.StepInRisk >= 99.999 ? 0 : eq0 * (1 - Math.Max(1, s.StepInRisk) / 100)); }
        public static double GoalOf(AutoSettings s, double target) { return s.StepInGoal == "BE" ? Math.Max(0.5, s.GuardKeep) : Math.Max(0.5, target); }
        public void Activate(AutoSettings s, int iter, StepInView v, DateTime iterStart, string why, string account)
        {
            Reset(); Active = true; Iter = iter; FloorEq = FloorOf(s, v.Equity); Goal = GoalOf(s, v.Target);
            Ep = new StepInEpisode { Account = account ?? "", Mode = v.Simulated ? "SIM" : "LIVE", Iter = iter, IterStart = iterStart, At = v.Now, Total0 = Math.Round(v.Total, 2), Target = v.Target, X0 = Math.Round(v.Total / Math.Max(0.01, v.Target), 1),
                Pct0 = v.Balance > 0 ? Math.Round(100 * v.Total / v.Balance, 2) : 0, Minutes0 = Math.Round((v.Now - iterStart).TotalMinutes, 0), Trend0 = v.Trend != null ? v.Trend.Text : "—", Eq0 = v.Equity, Floor = Math.Round(FloorEq, 2), Goal = Goal, Worst = v.Total, MaxGross = v.Gross, Why = why };
            Text = "🚑 STEP IN ACTIVE — " + why;
        }
        public void Track(StepInView v) { if (Ep == null) return; Ep.Worst = Math.Min(Ep.Worst, v.Total); Ep.MaxGross = Math.Max(Ep.MaxGross, v.Gross); }
        public void Filled(long ticket, string tag) { if (tag == "STACK" && ticket != 0) Stack.Add(ticket); }
        static double Rnd(double lots, double step) { step = step > 0 ? step : 0.01; return Math.Round(Math.Floor(lots / step + 1e-9) * step, 2); }
        static double Pl(Mt5Position p) { return p.Profit + p.Swap + p.Commission; }
        // the M5 swing high / low before the newest two finished M5 candles (the same rule as the M1 one), and the newest M5 close
        public static Tuple<double, double, double> SwingM5(IList<Mt5Bar> m1)
        {
            var m5 = TrendDetect.ToM5(m1 ?? new List<Mt5Bar>()); if (m5.Count < 6) return Tuple.Create(double.NaN, double.NaN, double.NaN);
            int i = m5.Count - 1; double hi = double.NaN, lo = double.NaN;
            for (int k = i - 2; k >= Math.Max(2, i - 30) && double.IsNaN(hi); k--) if (m5[k].H > m5[k - 1].H && m5[k].H > m5[k - 2].H && m5[k].H >= m5[k + 1].H && m5[k].H >= m5[k + 2].H) hi = m5[k].H;
            for (int k = i - 2; k >= Math.Max(2, i - 30) && double.IsNaN(lo); k--) if (m5[k].L < m5[k - 1].L && m5[k].L < m5[k - 2].L && m5[k].L <= m5[k + 1].L && m5[k].L <= m5[k + 2].L) lo = m5[k].L;
            return Tuple.Create(hi, lo, m5[i].C);
        }
        // the margin guard (pure): the most lots this order may have so the margin level stays ≥ minLevel % (perLot = the margin of 1 lot, counted in full)
        public static double MarginRoom(double equity, double margin, double perLot, double minLevelPct)
        {
            if (perLot <= 0 || minLevelPct <= 0) return 1e9; double maxMargin = equity / (minLevelPct / 100.0); return Math.Max(0, (maxMargin - Math.Max(0, margin)) / perLot);
        }
        // the broker refuses an order without the free margin for it (level after < 100 %); an MT5 account also keeps 1.5 × its stop-out level (the user's rule)
        public static double MinLevel(bool simulated, double stopOutPct) { return Math.Max(100, Math.Max(1, stopOutPct) * (simulated ? 1.0 : 1.5)); }
        // an order the manager decided was not sent (a margin hold / refused): it does not count
        public void Unsent(StepInAction a) { if (a == null) return; if (a.Tag == "STACK" && Ep != null && Ep.Stacks > 0) Ep.Stacks--; if (a.Tag == "HOLD") Held = false; }

        public StepInAction Decide(AutoSettings s, StepInView v)
        {
            if (!Active || v.Pos.Count == 0 || double.IsNaN(v.Bid)) return StepInAction.None();
            Track(v); Stack.RemoveWhere(tk => !v.Pos.Any(p => p.Ticket == tk));
            double ask = double.IsNaN(v.Ask) ? v.Bid : v.Ask, net = v.Net, step = v.Step > 0 ? v.Step : 0.01; var tr = v.Trend; int d = tr != null ? tr.Dir : 0;
            LastTrend = tr != null ? tr.Text : "—";
            // 1. GOAL: the total reached the recovery goal → everything closes
            if (v.Total >= Goal) return new StepInAction { Kind = "CLOSEALL", Tag = "GOAL", Why = "🚑 RECOVERED — the total " + U(v.Total) + " ≥ the goal " + U(Goal) + (s.StepInGoal == "BE" ? " (breakeven)" : " (breakeven + the target)") + " → CLOSE ALL" };
            // 2. FLOOR: the equity at the floor → net 0, nothing more is added (never a loss close)
            if (FloorEq > 0 && v.Equity <= FloorEq && !Frozen) { Frozen = true; if (Ep != null) Ep.FloorHit = true; }
            if (Frozen)
            {
                Text = "🚑 STEP IN at the FLOOR — the equity $" + v.Equity.ToString("N2", IC) + " ≤ $" + FloorEq.ToString("N2", IC) + ": neutralised (net 0), nothing more is added — the basket waits for the goal / your decision (never closed at a loss by itself)";
                if (Math.Abs(net) >= step - 1e-9) return new StepInAction { Kind = "ORDER", Tag = "FLOOR", Dir = net > 0 ? -1 : 1, Lots = Math.Abs(net), Why = "🚑 FLOOR — the equity $" + v.Equity.ToString("N2", IC) + " reached the floor $" + FloorEq.ToString("N2", IC) + " (MAX RISK) → " + (net > 0 ? "SELL " : "BUY ") + Math.Abs(net).ToString("0.##", IC) + " = net 0 (full hedge): the loss stops growing; it never closes at a loss by itself" };
                return StepInAction.None(Text);
            }
            // 3. the second half of a pair cut
            if (pairOwe >= step - 1e-9)
            {
                var w = v.Pos.FirstOrDefault(p => p.Ticket == pairTicket); double m = pairOwe; pairOwe = 0;
                if (w != null) { double cl = Math.Min(w.Lots, m); return new StepInAction { Kind = "CLOSE", Tag = "CUT2", Ticket = w.Ticket, Lots = cl >= w.Lots - 1e-9 ? 0 : Rnd(cl, step), Why = "🚑 CUT PAIR (2/2) — the winner " + w.Side + " " + Rnd(cl, step).ToString("0.##", IC) + " of #" + w.Ticket + " closes too: the net is what it was" }; }
            }
            double atr5 = tr != null && !double.IsNaN(tr.Atr5) && tr.Atr5 > 0 ? tr.Atr5 : (tr != null && !double.IsNaN(tr.Atr1) ? tr.Atr1 * Math.Sqrt(5) : double.NaN);
            double spread = Math.Max(0, ask - v.Bid); if (!double.IsNaN(atr5) && spread > 0) atr5 = Math.Max(atr5, 4 * spread);   // the distances (stop 1.5×, trail, trims) never inside 6 spreads: the spread would eat the stack
            // 4. the STACK: trail / flip / trim
            var stack = v.Pos.Where(p => Stack.Contains(p.Ticket)).ToList();
            if (stack.Count == 0) { TrailPx = BestPx = double.NaN; trailHit = flipping = false; StackTrims = 0; }
            else
            {
                int sd = stack[0].Dir; double lots = stack.Sum(p => p.Lots), avg = stack.Sum(p => p.Open * p.Lots) / Math.Max(1e-9, lots), px = sd > 0 ? v.Bid : ask, move = sd * (px - avg);
                var big = stack.OrderByDescending(p => p.Lots).First();
                if (trailHit || flipping) return new StepInAction { Kind = "CLOSE", Tag = trailHit ? "TRAIL" : "FLIP", Ticket = big.Ticket, Why = (trailHit ? "🚑 TRAIL" : "🚑 FLIP") + " — the next stacked leg " + big.Side + " " + big.Lots.ToString("0.##", IC) + " #" + big.Ticket + " closes (" + U(Pl(big)) + ")" };
                if (!double.IsNaN(atr5) && move <= -s.StepInStopAtr * atr5)
                { flipping = true; if (Ep != null) Ep.Flips++; return new StepInAction { Kind = "CLOSE", Tag = "FLIP", Ticket = big.Ticket, Why = "🚑 STACK STOP — the stack is " + (-move).ToString("0.00", IC) + " against it (≥ " + s.StepInStopAtr.ToString("0.#", IC) + " × ATR M5 $" + atr5.ToString("0.00", IC) + ") → the stacked " + big.Side + " " + big.Lots.ToString("0.##", IC) + " #" + big.Ticket + " closes (" + U(Pl(big)) + "), the rest of the stack follows" }; }
                if (d == -sd && tr.Strength >= Math.Max(s.StepInMinStrength, 75))
                { flipping = true; if (Ep != null) Ep.Flips++; return new StepInAction { Kind = "CLOSE", Tag = "FLIP", Ticket = big.Ticket, Why = "🚑 FLIP — " + tr.Text + " turned against the stack → the stacked " + big.Side + " " + big.Lots.ToString("0.##", IC) + " #" + big.Ticket + " closes (" + U(Pl(big)) + "), the rest of the stack follows" }; }
                if (!double.IsNaN(atr5))
                {
                    if (move >= s.StepInTrailStart * atr5) { BestPx = double.IsNaN(BestPx) ? px : sd > 0 ? Math.Max(BestPx, px) : Math.Min(BestPx, px); double t = BestPx - sd * s.StepInTrailAtr * atr5; TrailPx = double.IsNaN(TrailPx) ? t : sd > 0 ? Math.Max(TrailPx, t) : Math.Min(TrailPx, t); }
                    if (!double.IsNaN(TrailPx) && (sd > 0 ? v.Bid <= TrailPx : ask >= TrailPx))
                    { trailHit = true; if (Ep != null) Ep.Trails++; return new StepInAction { Kind = "CLOSE", Tag = "TRAIL", Ticket = big.Ticket, Why = "🚑 TRAIL — the " + (sd > 0 ? "bid " + P(v.Bid) + " ≤ " : "ask " + P(ask) + " ≥ ") + "the trail " + P(TrailPx) + " (" + s.StepInTrailAtr.ToString("0.#", IC) + " × ATR M5 behind the best " + P(BestPx) + ") → the stack closes, its profit banked: " + big.Side + " " + big.Lots.ToString("0.##", IC) + " #" + big.Ticket + " (" + U(Pl(big)) + ")" }; }
                    if (s.StepInTrimPct > 0 && move >= (StackTrims + 1) * s.StepInTrimAtr * atr5)
                    {
                        double cut = Math.Max(step, Rnd(lots * s.StepInTrimPct / 100, step)); cut = Math.Min(cut, big.Lots); StackTrims++;
                        if (cut < lots - 1e-9)
                        {
                            double pl = Pl(big) * cut / Math.Max(1e-9, big.Lots); if (Ep != null) { Ep.Trims++; Ep.Trimmed = Math.Round(Ep.Trimmed + pl, 2); }
                            return new StepInAction { Kind = "CLOSE", Tag = "TRIM", Ticket = big.Ticket, Lots = cut >= big.Lots - 1e-9 ? 0 : cut, Why = "🚑 TRIM " + StackTrims + " — the stack is " + move.ToString("0.00", IC) + " in its favour (≥ " + StackTrims + " × " + s.StepInTrimAtr.ToString("0.#", IC) + " ATR M5) → " + cut.ToString("0.##", IC) + " of " + big.Side + " #" + big.Ticket + " banked (≈ " + U(pl) + ", " + s.StepInTrimPct.ToString("0", IC) + " % of the stack)" };
                        }
                    }
                }
            }
            // 5. CUT PAIR: a counter-trend loser + a with-trend winner, the same lots, their sum ≥ $0 (the net and the total do not change)
            if (d != 0)
            {
                var L = v.Pos.Where(p => p.Dir == -d && Pl(p) < 0).OrderBy(p => Pl(p) / Math.Max(1e-9, p.Lots)).FirstOrDefault();
                var W = v.Pos.Where(p => p.Dir == d && Pl(p) > 0).OrderByDescending(p => Pl(p) / Math.Max(1e-9, p.Lots)).FirstOrDefault();
                if (L != null && W != null)
                {
                    double m = Rnd(Math.Min(L.Lots, W.Lots), step);
                    // the most of the loser the winner can pay for: m × (loss per lot + win per lot) ≥ 0
                    double lpl = Pl(L) / L.Lots, wpl = Pl(W) / W.Lots;
                    if (m >= step - 1e-9 && lpl + wpl >= 0)
                    {
                        pairOwe = m; pairTicket = W.Ticket; if (Ep != null) Ep.Cuts++;
                        return new StepInAction { Kind = "CLOSE", Tag = "CUT", Ticket = L.Ticket, Lots = m >= L.Lots - 1e-9 ? 0 : m, Why = "🚑 CUT PAIR — the counter-trend loser " + L.Side + " " + m.ToString("0.##", IC) + " of #" + L.Ticket + " (" + U(lpl * m) + ") + the winner " + W.Side + " " + m.ToString("0.##", IC) + " of #" + W.Ticket + " (" + U(wpl * m) + ") = " + U((lpl + wpl) * m) + " ≥ $0 → both close, the net stays " + net.ToString("+0.##;−0.##", IC) };
                    }
                }
            }
            // 6. HOLD: no trend → net 0 once (no flip-flop in a chop), then nothing until a trend shows
            if (d == 0)
            {
                Text = "🚑 STEP IN ACTIVE — no trend (" + (tr != null ? tr.Text : "—") + "): " + (Held ? "holding at net 0, waits for a trend" : "freezing at net 0");
                if (!Held && stack.Count == 0 && Math.Abs(net) >= step - 1e-9)
                {   // first the net side's legs IN PROFIT close (the gross shrinks, the total is unchanged, no new spread); the rest is hedged by a new leg
                    int nd = net > 0 ? 1 : -1; var win = v.Pos.Where(p => p.Dir == nd && Pl(p) > 0).OrderByDescending(p => Pl(p) / Math.Max(1e-9, p.Lots)).FirstOrDefault();
                    if (win != null) { double cl = Math.Min(win.Lots, Math.Abs(net)); return new StepInAction { Kind = "CLOSE", Tag = "HOLD", Ticket = win.Ticket, Lots = cl >= win.Lots - 1e-9 ? 0 : Rnd(cl, step), Why = "🚑 HOLD — no trend (" + (tr != null ? tr.Text : "—") + ") → the " + win.Side + " " + Rnd(cl, step).ToString("0.##", IC) + " of #" + win.Ticket + " in profit closes (" + U(Pl(win) * cl / Math.Max(1e-9, win.Lots)) + ") toward net 0: fewer lots, the same total" }; }
                    Held = true; return new StepInAction { Kind = "ORDER", Tag = "HOLD", Dir = net > 0 ? -1 : 1, Lots = Math.Abs(net), Why = "🚑 HOLD — no trend (" + (tr != null ? tr.Text : "—") + ") → " + (net > 0 ? "SELL " : "BUY ") + Math.Abs(net).ToString("0.##", IC) + " = net 0: no more flip-flop in the chop, it waits for a trend" }; }
                return StepInAction.None(Text);
            }
            Held = false;
            // 7. STACK: a confirmed trend + a new finished swing break (M1 or M5) → size UP the trend's way
            string trendTxt = (d > 0 ? "UP " : "DOWN ") + tr.Strength;
            Text = "🚑 STEP IN ACTIVE — stacking with the trend (" + (d > 0 ? "UP" : "DOWN") + ")" + (Ep != null && Ep.Trimmed != 0 ? " • trimmed " + U(Ep.Trimmed) : "") + (!double.IsNaN(TrailPx) ? " • trailing at " + P(TrailPx) : "") + " • total " + U(v.Total) + " → goal " + U(Goal);
            if (tr.Strength < s.StepInMinStrength) return StepInAction.None(Text + " (the trend " + tr.Strength + " < " + s.StepInMinStrength + ": waits)");
            if (stack.Count > 0 && stack[0].Dir != d) return StepInAction.None(Text + " (the open stack is the other way: its stop / trail / flip goes first)");
            if (Stack.Count >= s.StepInMaxStacks) return StepInAction.None(Text + " (" + Stack.Count + "/" + s.StepInMaxStacks + " stacked legs open)");
            double lvl, close;
            if (s.StepInTf >= 5) { var m5 = SwingM5(v.M1); lvl = d > 0 ? m5.Item1 : m5.Item2; close = m5.Item3; } else { lvl = d > 0 ? tr.SwingHi : tr.SwingLo; close = tr.Close; }
            if (double.IsNaN(lvl) || double.IsNaN(close) || !(d > 0 ? close > lvl : close < lvl) || (!double.IsNaN(usedSwing) && Math.Abs(lvl - usedSwing) < 1e-9)) return StepInAction.None(Text + " (waits for a new " + (s.StepInTf >= 5 ? "M5" : "M1") + " swing break " + (d > 0 ? "up" : "down") + ")");
            if (double.IsNaN(atr5) || v.PerPrice <= 0) return StepInAction.None(Text + " (no ATR yet)");
            double netD = net * d, flip = Math.Max(0, -netD), have = Math.Max(0, netD);
            double need = Math.Max(0, Goal - v.Total) / (s.StepInMoveK * atr5 * v.PerPrice) - have;
            double cap = Math.Max(0, v.Equity - FloorEq) / (s.StepInRiskMoveK * atr5 * v.PerPrice) - have;   // the risk budget: the equity above the floor over RiskMoveK × ATR M5 against
            // one stack never risks more than StepInStackRisk % of the equity above the floor at its own stop (StepInStopAtr × ATR M5): a failed stack costs a slice, not the account
            double perStack = s.StepInStackRisk > 0 ? s.StepInStackRisk / 100 * Math.Max(0, v.Equity - FloorEq) / (s.StepInStopAtr * atr5 * v.PerPrice) : 1e9;
            double add = Math.Min(Math.Min(Math.Max(step, Math.Ceiling(Math.Max(0, need) / step - 1e-9) * step), Rnd(Math.Max(0, cap), step)), Rnd(Math.Max(0, perStack), step));
            double lotsO = Rnd(flip + Math.Max(0, add), step);
            double room = MarginRoom(v.Equity, v.Margin, v.MarginPerLot, MinLevel(v.Simulated, v.StopOutPct));
            string capWhy = "";
            if (lotsO > room) { lotsO = Rnd(room, step); capWhy = " (the margin guard: the margin level stays ≥ " + MinLevel(v.Simulated, v.StopOutPct).ToString("0", IC) + " %)"; }
            if (v.MaxOrder > 0 && lotsO > v.MaxOrder) { lotsO = Rnd(v.MaxOrder, step); capWhy += " (the bridge's max " + v.MaxOrder.ToString("0.##", IC) + " lots an order)"; }
            usedSwing = lvl;
            if (lotsO < step - 1e-9 || (flip <= 0 && add < step - 1e-9)) return StepInAction.None(Text + " — no room to stack" + (capWhy != "" ? capWhy : cap < step ? " (the risk budget above the floor $" + FloorEq.ToString("N0", IC) + " is used)" : ""));
            if (Ep != null) Ep.Stacks++;
            return new StepInAction { Kind = "ORDER", Tag = "STACK", Dir = d, Lots = lotsO, Why = "🚑 STACK " + (Ep != null ? Ep.Stacks : 1) + " (" + (Stack.Count + 1) + "/" + s.StepInMaxStacks + " open) " + (d > 0 ? "BUY " : "SELL ") + lotsO.ToString("0.##", IC) + " — trend " + trendTxt + ", the " + (s.StepInTf >= 5 ? "M5" : "M1") + " close " + P(close) + " broke the swing " + P(lvl)
                + (flip > 0 ? " · flips the net " + net.ToString("+0.##;−0.##", IC) + " to the trend's side" : "") + " · needs " + U(Goal - v.Total) + " to the goal in " + s.StepInMoveK.ToString("0.#", IC) + " × ATR M5 ($" + atr5.ToString("0.00", IC) + ") · risk budget " + (have + Math.Max(0, cap)).ToString("0.##", IC) + " net lots above the floor $" + FloorEq.ToString("N0", IC) + (s.StepInStackRisk > 0 ? ", ≤ " + s.StepInStackRisk.ToString("0", IC) + " % of it at this stack's stop" : "") + capWhy };
        }
    }
}
