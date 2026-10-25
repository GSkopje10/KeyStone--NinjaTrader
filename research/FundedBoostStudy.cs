// FUNDED BOOST STUDY: how to get the most out of a funded account (Lucid Flex 50K funded: $0 start, $2,000 EOD drawdown that stops
// at +$100, a payout after 5 days of +$150 or more = 50 % of the balance, at most $2,000, you keep 90 %, 5 payouts then retired).
// The goal asked for: the full $2,000 every 5 days. Groups of 20 funded accounts start on a date every 20 trading days from 2021
// (FUNDED ONLY: they start funded) and trade a style until every one is blown or retired (or 160 trading days); the styles are
// ranked on the groups started 2021–2025 and checked on the 2026 groups. ROTATE (one account a trade, in turn) and COPY (every
// account takes every trade) are both measured. The app's engine (KeystoneRotationLab.Run).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;

public static class FundedBoostStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class Style
    {
        public int QM, QG; public double Target, Stop; public int Tries; public double PayoutMin; public string Alloc = "ROTATE"; public string Rule = "BUY";
        public string Key { get { return QM + "|" + QG + "|" + Target + "|" + Stop + "|" + Tries + "|" + PayoutMin + "|" + Alloc + "|" + Rule; } }
        public string Label { get { return (Rule == "BUSY" ? "⚡ busy days • " : "") + (QG == 0 ? QM + " MNQ" : QM + "+" + QG) + " • +$" + Target.ToString("N0", IC) + " / " + (Stop >= 2000 ? "the whole room" : "−$" + Stop.ToString("N0", IC)) + " • " + (Tries == 1 ? "1 a day" : "until +$" + Target.ToString("N0", IC)) + " • " + (PayoutMin >= 2000 ? "only the full $2,000" : "withdraw when allowed") + (Alloc == "COPY" ? " • COPY" : ""); } }
    }
    sealed class Agg { public int Accounts, Paid, FullPaid, Payouts, Full, Retired5, WeeklyGaps, Gaps; public double Cash, DaysFirst, GapDays; public int Groups; }

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var all = KeystoneStudioStore.Sessions(folder, "MNQ").Keys.Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday).OrderBy(d => d).ToList();
        var days = KeystoneRotationLab.LoadDays(folder, all.Where(d => d >= new DateTime(2021, 1, 1)));
        // busy-day flags (the previous session's MNQ range over its 20-day median), as in the eval study
        var busy = new HashSet<DateTime>(); Func<KeystoneRotDay, double> rng = d => { double hi = double.MinValue, lo = double.MaxValue; for (int i = 0; i < KeystoneRotDay.Slots; i++) if (d.HasM(i)) { hi = Math.Max(hi, d.MH[i]); lo = Math.Min(lo, d.ML[i]); } return hi > lo ? hi - lo : 0; };
        var ranges = days.Select(rng).ToList(); for (int i = 21; i < days.Count; i++) { var med = ranges.Skip(i - 21).Take(20).OrderBy(x => x).ElementAt(10); if (ranges[i - 1] > med) busy.Add(days[i].Day); }
        var starts = new List<int>(); for (int i = 25; i < days.Count - 60; i += 20) starts.Add(i);
        Console.WriteLine(days.Count + " days, " + starts.Count + " group starts");
        var styles = new List<Style>();
        foreach (var q in new[] { 3, 5, 7, 10 }) foreach (var tg in new[] { 400.0, 700, 1000, 1500 }) foreach (var st in new[] { 500.0, 1000, 2000 }) foreach (var tr in new[] { 1, 0 }) foreach (var pm in new[] { 0.0, 2000 })
            styles.Add(new Style { QM = q, QG = 0, Target = tg, Stop = st, Tries = tr, PayoutMin = pm });
        foreach (var q in new[] { 5, 10 }) foreach (var tg in new[] { 700.0, 1500 }) foreach (var st in new[] { 1000.0, 2000 }) styles.Add(new Style { QM = q, QG = q, Target = tg, Stop = st, Tries = 1, PayoutMin = 0 });
        var res = new Dictionary<string, Agg[]>(); foreach (var s in styles) res[s.Key] = new[] { new Agg(), new Agg() };
        Func<Style, Agg[], int> runAll = null;
        runAll = (sty, agg) =>
        {
            var jobs = starts.ToList(); var local = new List<Tuple<int, Agg>>();
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i0 =>
            {
                var part = days.Skip(i0).Take(160).ToList(); var a = Measure(sty, part, busy);
                lock (local) local.Add(Tuple.Create(days[i0].Day.Year == 2026 ? 1 : 0, a));
            });
            foreach (var t in local) Add(agg[t.Item1], t.Item2); return 0;
        };
        int n0 = 0; foreach (var s in styles) { runAll(s, res[s.Key]); if (++n0 % 20 == 0) Console.WriteLine("  styles " + n0 + " / " + styles.Count); }
        // COPY and the busy-day filter for the 12 best (by the history's cash per account)
        var top = styles.OrderByDescending(s => Per(res[s.Key][0])).Take(12).ToList(); var extra = new List<Style>();
        foreach (var s in top) { extra.Add(new Style { QM = s.QM, QG = s.QG, Target = s.Target, Stop = s.Stop, Tries = s.Tries, PayoutMin = s.PayoutMin, Alloc = "COPY" }); extra.Add(new Style { QM = s.QM, QG = s.QG, Target = s.Target, Stop = s.Stop, Tries = s.Tries, PayoutMin = s.PayoutMin, Rule = "BUSY" }); }
        foreach (var s in extra) { res[s.Key] = new[] { new Agg(), new Agg() }; runAll(s, res[s.Key]); }
        styles.AddRange(extra);
        // JSON
        var js = new StringBuilder("{\"starts\":" + starts.Count + ",\"groupSize\":20,\"styles\":[");
        js.Append(string.Join(",", styles.Select(s => { var h = res[s.Key][0]; var t = res[s.Key][1];
            return "{\"key\":" + J(s.Key) + ",\"label\":" + J(s.Label) + ",\"alloc\":" + J(s.Alloc) + ",\"rule\":" + J(s.Rule) + ",\"qm\":" + s.QM + ",\"qg\":" + s.QG + ",\"target\":" + N(s.Target) + ",\"stop\":" + N(s.Stop) + ",\"tries\":" + s.Tries + ",\"full\":" + (s.PayoutMin >= 2000 ? "true" : "false")
                + ",\"hist\":" + A(h) + ",\"test\":" + A(t) + "}"; })));
        js.Append("]}"); File.WriteAllText(Path.Combine(outDir, "funded.json"), js.ToString());
        var sb = new StringBuilder("# FUNDED BOOST STUDY\n\n| style | cash / funded account 2021–25 | paid ≥1 | full $2K payouts / acct | 2026 cash / acct |\n|---|---|---|---|---|\n");
        foreach (var s in styles.OrderByDescending(s => Per(res[s.Key][0])).Take(30)) { var h = res[s.Key][0]; var t = res[s.Key][1]; sb.AppendLine("| " + s.Label + " | $" + Per(h).ToString("N0", IC) + " | " + (100.0 * h.Paid / Math.Max(1, h.Accounts)).ToString("0", IC) + " % | " + ((double)h.Full / Math.Max(1, h.Accounts)).ToString("0.00", IC) + " | $" + Per(t).ToString("N0", IC) + " |"); }
        File.WriteAllText(Path.Combine(outDir, "funded.md"), sb.ToString()); Console.WriteLine("done");
    }
    static double Per(Agg a) { return a.Accounts == 0 ? 0 : a.Cash / a.Accounts; }
    static void Add(Agg t, Agg a) { t.Accounts += a.Accounts; t.Paid += a.Paid; t.FullPaid += a.FullPaid; t.Payouts += a.Payouts; t.Full += a.Full; t.Retired5 += a.Retired5; t.Cash += a.Cash; t.DaysFirst += a.DaysFirst; t.GapDays += a.GapDays; t.Gaps += a.Gaps; t.WeeklyGaps += a.WeeklyGaps; t.Groups++; }
    static Agg Measure(Style sty, List<KeystoneRotDay> part, HashSet<DateTime> busy)
    {
        var r = KeystoneSimRules.Presets().First(x => x.Name == "LUCID FLEX 50K").Copy(); r.FlatByHhmm = 1645; r.PayoutMin = sty.PayoutMin;
        var s = new KeystoneSimSettings { Strategy = "ROTATION", Accounts = 20, StartAs = "FUNDED", FundedWhen = "WITH", BuyRule = "BREAKEVEN", ReplacePassed = false, Allocation = sty.Alloc, DayPlan = "NONE", Direction = "BUY", Auto = true, AutoFallback = "WAIT", PauseSeconds = 60, TrailStop = false,
            MnqQty = sty.QM, MgcQty = sty.QG, FundMnqQty = sty.QM, FundMgcQty = sty.QG, RotTarget = sty.Target, FundRotTarget = sty.Target, RotStop = sty.Stop, FundRotStop = sty.Stop, MaxTradesPerDay = sty.Tries, FundMaxTrades = sty.Tries, Rules = r, MinRoomShare = 0.25 };
        // COPY = every account takes the same trade at the same moment, so each account is the leader's path: one account traded
        // alone (the engine's COPY mode keeps passed accounts waiting for a choice, so it is modelled this way here)
        if (sty.Alloc == "COPY") { s.Allocation = "ROTATE"; s.Accounts = 1; }
        Func<KeystoneRotDay, int, Tuple<int, int>> sides = null; if (sty.Rule == "BUSY") sides = (d, k) => busy.Contains(d.Day) ? Tuple.Create(1, 1) : Tuple.Create(0, 0);
        var sim = KeystoneRotationLab.Run(s, part, 1800, 1645, null, sides);
        var first = part[0].Day; var mine = sim.Accounts.Where(a => a.Bought == first).ToList(); var ids = new HashSet<int>(mine.Select(a => a.Id)); var dayIx = part.Select((d, i) => Tuple.Create(d.Day, i)).ToDictionary(x => x.Item1, x => x.Item2);
        var o = new Agg { Accounts = mine.Count };
        foreach (var a in mine)
        {
            var ev = sim.Events.Where(e => e.Account == a.Id && e.Kind == "PAYOUT").OrderBy(e => e.Time).ToList();
            o.Cash += a.Cash; o.Payouts += ev.Count; if (ev.Count > 0) o.Paid++; int full = ev.Count(e => e.Amount >= 1800 - 1e-6); o.Full += full; if (full > 0) o.FullPaid++; if (a.Payouts >= 5) o.Retired5++;
            int prev = 0; foreach (var e in ev) { int ix; if (!dayIx.TryGetValue(e.Time.Date, out ix)) continue; if (prev == 0 && e == ev[0]) o.DaysFirst += ix + 1; else { o.Gaps++; o.GapDays += ix - prev; if (ix - prev <= 7 && e.Amount >= 1800 - 1e-6) o.WeeklyGaps++; } prev = ix; }
        }
        return o;
    }
    static string A(Agg a)
    {
        double n = Math.Max(1, a.Accounts);
        return "{\"accounts\":" + a.Accounts + ",\"groups\":" + a.Groups + ",\"cash\":" + N(a.Cash / n) + ",\"paid\":" + N(100.0 * a.Paid / n) + ",\"fullPaid\":" + N(100.0 * a.FullPaid / n) + ",\"payouts\":" + N(a.Payouts / n) + ",\"full\":" + N(a.Full / n)
            + ",\"retired5\":" + N(100.0 * a.Retired5 / n) + ",\"daysFirst\":" + N(a.Paid == 0 ? double.NaN : a.DaysFirst / a.Paid) + ",\"gap\":" + N(a.Gaps == 0 ? double.NaN : a.GapDays / a.Gaps) + ",\"weekly\":" + N(a.Gaps == 0 ? 0 : 100.0 * a.WeeklyGaps / a.Gaps) + "}";
    }
    static string J(string s) { return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
    static string N(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 2).ToString(IC); }
}
