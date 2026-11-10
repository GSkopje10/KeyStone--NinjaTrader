// SpasiX — 🤖 WHAT IF (09g, the user's request: "would the automation ALONE have saved it — when, and with what result?"). For every iteration where
// he intervened by hand, the basket just BEFORE his first manual action is kept (its legs, what it had banked, the settings), and the automation
// alone is replayed from that moment on the REAL prices that followed — in an in-memory SimBroker only, never MT5 — and carried forward as new
// candles arrive until it resolves (TARGET / RESCUE / DANGER / HARD STOP / max drawdown) or stays "still open". Deterministic: the same candles
// give the same answer, so a restart simply replays it again (the cases are saved per account).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class WhatIfLeg { public long Ticket; public string Side = "BUY"; public double Lots, Open; public DateTime Time; }
    public sealed class WhatIfCase
    {
        public int N; public string Symbol = "XAUUSD", Settings = "", What = ""; public DateTime Start, SnapAt; public double Banked, MaxDd, Balance = 10000, Commission, Target;
        public string Kind = "STANDARD"; public int Leverage = 500, Spread = -1; public List<WhatIfLeg> Legs = new List<WhatIfLeg>();
        // the replay's answer
        public bool Done; public string Reason = ""; public double Result, Worst, NowTotal; public DateTime End, LastBar; public List<Tuple<DateTime, double>> Path = new List<Tuple<DateTime, double>>();
        // the real iteration (filled by the app): done + its result, or its total now
        public bool ActualDone; public double Actual;
        public static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public string Save()
        {
            return string.Join("|", "WI", N, Symbol, Start.Ticks, SnapAt.Ticks, Banked.ToString("R", IC), MaxDd.ToString("R", IC), Balance.ToString("R", IC), Commission.ToString("R", IC), Target.ToString("R", IC), Kind, Leverage, Spread,
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Settings)), Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(What)),
                string.Join(";", Legs.Select(l => l.Ticket + ":" + l.Side + ":" + l.Lots.ToString("R", IC) + ":" + l.Open.ToString("R", IC) + ":" + l.Time.Ticks)));
        }
        public static WhatIfCase Load(string line)
        {
            var a = line.Split('|'); if (a.Length < 16 || a[0] != "WI") return null;
            try
            {
                Func<int, double> d = i => double.Parse(a[i], IC);
                var c = new WhatIfCase { N = int.Parse(a[1], IC), Symbol = a[2], Start = new DateTime(long.Parse(a[3], IC)), SnapAt = new DateTime(long.Parse(a[4], IC)), Banked = d(5), MaxDd = d(6), Balance = d(7), Commission = d(8), Target = d(9), Kind = a[10], Leverage = int.Parse(a[11], IC), Spread = int.Parse(a[12], IC),
                    Settings = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(a[13])), What = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(a[14])) };
                foreach (var l in a[15].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { var x = l.Split(':'); c.Legs.Add(new WhatIfLeg { Ticket = long.Parse(x[0], IC), Side = x[1], Lots = double.Parse(x[2], IC), Open = double.Parse(x[3], IC), Time = new DateTime(long.Parse(x[4], IC)) }); }
                return c;
            }
            catch { return null; }
        }
        static string Usd(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", IC); }
        static string Dur(TimeSpan t) { return t.TotalDays >= 1 ? ((int)t.TotalDays) + "d " + t.Hours + "h" : t.TotalHours >= 1 ? ((int)t.TotalHours) + "h " + t.Minutes + "m" : ((int)t.TotalMinutes) + " min"; }
        // "🤖 WITHOUT YOU": what the automation alone did / does
        public string WithoutYou
        {
            get
            {
                if (LastBar == DateTime.MinValue) return "⏳ waiting for the candles after " + SnapAt.ToString("dd MMM HH:mm", IC);
                if (Done) return (Reason.StartsWith("HARD STOP") ? "would have hit the HARD STOP " + Usd(Result) : "would have closed " + Usd(Result) + " (" + Reason + ")") + " on " + End.ToString("dd MMM HH:mm", IC) + " after " + Dur(End - SnapAt) + ", worst " + Usd(Worst);
                return "still open after " + Dur(LastBar - SnapAt) + ": " + Usd(NowTotal) + " now, worst " + Usd(Worst);
            }
        }
        // the verdict chip: you vs the automation alone (the real result or total now − its result or total now)
        public string Verdict
        {
            get
            {
                if (LastBar == DateTime.MinValue) return "⏳";
                double mine = Actual, its = Done ? Result : NowTotal, diff = mine - its, tol = Math.Max(2, 0.25 * Math.Abs(Target));
                if (Math.Abs(diff) <= tol) return "⚖ SIMILAR";
                if (diff > 0) return "✋ YOU SAVED IT " + Usd(diff).Replace("+", "+") + (Done ? "" : " (so far)");
                return "🤖 WOULD HAVE DONE IT TOO (better by " + Usd(-diff).TrimStart('+') + (Done ? "" : " so far") + ")";
            }
        }
        public double Diff { get { return Actual - (Done ? Result : NowTotal); } }
    }

    public static class WhatIf
    {
        // 🤖 the replay: the snapshot's legs in a fresh simulated account, the automation alone (the same settings) from the snapshot on the candles
        // that followed; m1 = 1-minute candles (warm-up before the snapshot welcome); spec = the symbol (swap at the rollover counted)
        // ticks (09k, optional) = real ticks of those days (recorded live / downloaded): where they exist the replay follows them exactly
        public static void Replay(WhatIfCase c, List<Mt5Bar> m1, SimSpec spec, List<SimTick> ticks = null)
        {
            c.Done = false; c.Reason = ""; c.Path.Clear(); c.Worst = Math.Min(0, c.MaxDd); c.NowTotal = double.NaN; c.LastBar = DateTime.MinValue;
            var after = m1.Where(b => b.Time >= c.SnapAt.AddMinutes(-1)).ToList(); if (after.Count < 2) return;
            var last = m1.Max(b => b.Time); var from = c.SnapAt.Date;
            var sb = new SimBroker();
            var rtk = ticks != null && ticks.Count(t => t.T >= c.SnapAt && t.T <= last.AddMinutes(1)) >= 10 ? ticks.Where(t => t.T <= last.AddMinutes(1)).OrderBy(t => t.T).ToList() : null;   // 🎞 real ticks
            var err = sb.Load(new SimSettings { Symbol = c.Symbol, From = from, To = last.Date, Balance = c.Balance, CommissionPerLot = c.Commission, Leverage = c.Leverage, Kind = c.Kind, FixedSpread = rtk != null ? -1 : c.Spread, StartNyHour = (c.SnapAt - from).TotalHours + spec.NyOffsetHours }, new List<Tuple<SimSpec, List<Mt5Bar>, List<SimTick>>> { Tuple.Create(spec, m1.Where(b => b.Time <= last).ToList(), rtk) });
            if (err != "") { c.Reason = "no replay: " + err; return; }
            sb.Playing = true; sb.Speed = 60; sb.Advance(0.02);
            lock (sb.State.Gate)
            {   // the basket as it was just before your first manual action
                var lines = sb.Account.Save().Where(l => !l.StartsWith("P|")).ToList();
                foreach (var l in c.Legs) lines.Add("P|" + l.Ticket + "|" + c.Symbol + "|" + l.Side + "|" + l.Lots.ToString("R", WhatIfCase.IC) + "|" + l.Open.ToString("R", WhatIfCase.IC) + "|0|0|" + l.Time.Ticks + "|0|0");
                sb.Account.Load(lines);
            }
            var s = AutoSettings.Load(c.Settings); s.Symbol = c.Symbol; s.TrackManual = false; s.OnManual = "ADOPT"; s.TrendStack = false; c.Target = c.Target > 0 ? c.Target : s.Target;
            var au = new HedgeAuto(); au.Send = (cmd, a, lab) => sb.Command(cmd, a);
            var tk = new HashSet<long>(c.Legs.Select(l => l.Ticket)); double balStart; lock (sb.State.Gate) balStart = sb.State.Account.Balance;
            au.RestoreOpen = () => new HedgeAuto.OpenSave { It = new AutoIteration { N = c.N, Start = c.Start, FirstDir = c.Legs.Count > 0 ? c.Legs.OrderBy(l => l.Time).First().Side : "BUY", BalanceStart = balStart - c.Banked, MaxDd = c.MaxDd }, Tickets = tk, SavedAt = c.SnapAt };
            sb.OnTickHook = () => au.Step(sb);
            if (au.Check(sb, s) != "" && sb.State.Positions.Count == 0) { c.Reason = "no replay: " + au.Check(sb, s); return; }
            au.Start(sb, s, true); DateTime lastPath = DateTime.MinValue;
            for (int k = 0; k < 20000000 && !sb.Ended; k++)
            {
                sb.Advance(0.25); var now = sb.State.ServerNow; var it = au.Iterations.FirstOrDefault(x => x.N == c.N);
                if (it != null && !it.Done && sb.State.Positions.Count > 0) { double tot = au.BasketTotal; c.Worst = Math.Min(c.Worst, tot); c.NowTotal = tot; if ((now - lastPath).TotalMinutes >= 5) { lastPath = now; c.Path.Add(Tuple.Create(now, Math.Round(tot, 2))); } }
                if (it != null && it.Done)
                {
                    double bal; lock (sb.State.Gate) bal = sb.State.Account.Balance; c.Done = true; c.Reason = it.Reason; c.End = it.End; c.Result = Math.Round(bal - balStart + c.Banked, 2); c.Path.Add(Tuple.Create(it.End, c.Result)); c.LastBar = now; return;
                }
                if (!au.Running && it == null) break;
            }
            c.LastBar = sb.State.ServerNow > c.SnapAt ? sb.State.ServerNow : last;
            if (double.IsNaN(c.NowTotal)) c.NowTotal = c.Banked;
        }
        // the summary for ANALYSIS / LEARN
        public static string Summary(IEnumerable<WhatIfCase> cases)
        {
            var l = cases.Where(c => c.LastBar > DateTime.MinValue).ToList(); if (l.Count == 0) return "🤖 WHAT IF: no manual interventions replayed yet.";
            int alone = l.Count(c => c.Done && c.Result >= 0), saved = l.Count(c => c.Verdict.StartsWith("✋")), open = l.Count(c => !c.Done);
            double diff = l.Sum(c => c.Diff);
            return "🤖 WHAT IF — " + l.Count + " manual intervention" + (l.Count == 1 ? "" : "s") + ": the automation alone would have closed " + alone + " in profit, " + l.Count(c => c.Done && c.Result < 0) + " at a loss, " + open + " still open • you saved " + saved + " • you vs it: " + (diff >= 0 ? "+$" : "−$") + Math.Abs(diff).ToString("N2", WhatIfCase.IC) + " in all";
        }
        // the saved cases of an account (one line each)
        public static List<WhatIfCase> LoadAll(string file) { var r = new List<WhatIfCase>(); try { if (File.Exists(file)) foreach (var l in File.ReadAllLines(file)) { var c = WhatIfCase.Load(l); if (c != null) r.Add(c); } } catch { } return r; }
        public static void SaveAll(string file, IEnumerable<WhatIfCase> cases) { try { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllLines(file, cases.Select(c => c.Save())); } catch { } }
    }
}
