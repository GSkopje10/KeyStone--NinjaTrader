// SpasiX — 💸 WITHDRAWAL PLAN (09r, the user's plan: "keep the current settings; start $5k; when the balance hits $10k withdraw $5k and continue at $5k;
// if blown, deposit a new $5k — put this withdrawal in the statistics"). A STATISTIC built from the account's own BALANCE deals:
// deposits (the first one + each after a blow-up), withdrawals, NET CASH = withdrawn − deposited, the money in the account now, the cycles
// (start → a withdrawal or a blow-up, time to double) and the progress to the next withdrawal. On the simulated DEMO a due withdrawal can be BOOKED
// (a balance deal, asks first) and 💥 AUTO RESTART counts as a deposit; on an MT5 account it only TRACKS what the broker reports and reminds — it
// never moves money.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class WdEvent { public DateTime Time; public string Kind = ""; public double Amount, BalanceAfter; public string Comment = ""; }   // DEPOSIT / RESTART (a deposit after a blow-up) / WITHDRAW
    public sealed class WdCycle { public DateTime Start, End; public string How = "open"; public double Days { get { return End > Start ? (End - Start).TotalDays : 0; } } }
    public sealed class WdStats
    {
        public int Deposits, Restarts, Withdrawals; public double Deposited, Withdrawn, Balance, Start, Level, Progress; public bool Due;
        public double NetCash { get { return Withdrawn - Deposited; } }
        public List<WdEvent> Events = new List<WdEvent>(); public List<WdCycle> Cycles = new List<WdCycle>();
        public double AvgDaysToDouble { get { var d = Cycles.Where(c => c.How == "💸 withdrawal").ToList(); return d.Count == 0 ? double.NaN : d.Average(c => c.Days); } }
    }
    public static class WithdrawPlan
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const string SimComment = "💸 withdrawal (plan, simulated)", RestartComment = "💥 auto restart (deposit)";
        // the research headline (mt5/docs/withdrawal-plan-study.md) — bench numbers are an ESTIMATE (futures candles, synthetic ticks)
        public static string StudyHeadline = "📚 the 6-year study (mt5/docs/withdrawal-plan-study.md) is pending — when it lands its bench numbers are an " + ConsultSrc.LibLabel + ".";
        public static double LevelOf(double start, double pct) { return Math.Round(start * (1 + pct / 100), 2); }
        public static bool IsDue(double balance, double start, double pct) { return balance >= LevelOf(start, pct) - 0.005; }
        public static double AmountNow(double balance, double start) { return Math.Max(0, Math.Round(balance - start, 2)); }   // back to the start
        public static double ProgressOf(double balance, double start, double pct) { double lv = LevelOf(start, pct); return lv - start <= 0 ? 0 : Math.Max(0, Math.Min(100, 100 * (balance - start) / (lv - start))); }
        // the statistics from the account's BALANCE deals (deposits > 0, withdrawals < 0) + the balance now
        public static WdStats Of(IEnumerable<Mt5Deal> deals, double balanceNow, double start, double pct, DateTime now)
        {
            var s = new WdStats { Balance = balanceNow, Start = start, Level = LevelOf(start, pct) };
            var all = (deals ?? Enumerable.Empty<Mt5Deal>()).OrderBy(d => d.Time).ThenBy(d => d.Ticket).ToList(); double bal = 0; WdCycle cur = null; bool first = true;
            foreach (var d in all)
            {
                if (d.Type != "BALANCE") { bal += d.Profit + d.Commission + d.Swap; continue; }
                if (Math.Abs(d.Profit) < 0.005) continue;
                if (d.Profit > 0)
                {
                    bool restart = !first && ((d.Comment ?? "").IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0 || bal < 0.1 * start);
                    if (restart) { s.Restarts++; if (cur != null) { cur.End = d.Time; cur.How = "💥 blow-up"; s.Cycles.Add(cur); } }
                    s.Deposits++; s.Deposited += d.Profit; bal += d.Profit; first = false;
                    s.Events.Add(new WdEvent { Time = d.Time, Kind = restart ? "RESTART" : "DEPOSIT", Amount = d.Profit, BalanceAfter = bal, Comment = d.Comment ?? "" });
                    if (cur == null || restart) cur = new WdCycle { Start = d.Time };
                }
                else
                {
                    s.Withdrawals++; s.Withdrawn += -d.Profit; bal += d.Profit;
                    s.Events.Add(new WdEvent { Time = d.Time, Kind = "WITHDRAW", Amount = -d.Profit, BalanceAfter = bal, Comment = d.Comment ?? "" });
                    if (cur != null) { cur.End = d.Time; cur.How = "💸 withdrawal"; s.Cycles.Add(cur); } cur = new WdCycle { Start = d.Time };
                }
            }
            if (cur != null) { cur.End = now; cur.How = "open"; s.Cycles.Add(cur); }
            s.Progress = ProgressOf(balanceNow, start, pct); s.Due = IsDue(balanceNow, start, pct);
            return s;
        }
        public static string Usd(double v) { return (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC); }
        public static string Bar(double pct, int width = 20) { int n = (int)Math.Round(Math.Max(0, Math.Min(100, pct)) / 100 * width); return new string('█', n) + new string('░', width - n); }
        public static string ProgressText(WdStats s) { return Usd(s.Balance) + " / " + Usd(s.Level) + " — " + s.Progress.ToString("0", IC) + " % of the way"; }
        // the ANALYSIS block; simulated = it may book; live = it only tracks and reminds
        public static List<string> Lines(WdStats s, bool simulated, double pct)
        {
            var L = new List<string>();
            L.Add("💸 WITHDRAWAL PLAN — start " + Usd(s.Start) + ", withdraw at +" + pct.ToString("0", IC) + " % (" + Usd(s.Level) + ") back to " + Usd(s.Start) + "; a blow-up = a new " + Usd(s.Start) + " deposit" + (simulated ? " (simulated DEMO: it can book them)" : " (MT5: tracking only — it never moves money)"));
            L.Add("   deposits " + s.Deposits + " (" + Usd(s.Deposited) + (s.Restarts > 0 ? ", " + s.Restarts + " after a blow-up" : "") + ")  •  withdrawals " + s.Withdrawals + " (" + Usd(s.Withdrawn) + ")  •  NET CASH " + (s.NetCash >= 0 ? "+" : "") + Usd(s.NetCash) + "  •  in the account now " + Usd(s.Balance) + "  •  cash + account " + Usd(s.NetCash + s.Balance));
            var dbl = s.Cycles.Count(c => c.How == "💸 withdrawal"); var blw = s.Cycles.Count(c => c.How == "💥 blow-up");
            L.Add("   cycles: " + dbl + " doubled (withdrawal), " + blw + " blown" + (double.IsNaN(s.AvgDaysToDouble) ? "" : ", time to double " + s.AvgDaysToDouble.ToString("0.#", IC) + " days on average"));
            L.Add("   next withdrawal: " + Bar(s.Progress) + " " + ProgressText(s) + (s.Due ? (simulated ? "  — 💸 DUE: WITHDRAW " + Usd(AmountNow(s.Balance, s.Start)) + " (asks first)" : "  — 💸 withdrawal due — do it at your broker") : ""));
            foreach (var e in s.Events.Skip(Math.Max(0, s.Events.Count - 6))) L.Add("   " + e.Time.ToString("yyyy-MM-dd HH:mm", IC) + "  " + (e.Kind == "WITHDRAW" ? "💸 withdrawn " : e.Kind == "RESTART" ? "💥 deposit after a blow-up " : "➕ deposit ") + Usd(e.Amount) + "  → balance " + Usd(e.BalanceAfter));
            L.Add("   " + StudyHeadline);
            return L;
        }
    }
}
