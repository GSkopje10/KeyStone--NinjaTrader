// PASS STUDY: the MNQ + MGC rotation used ONLY to pass evaluations (funded ignored). 50 evaluations at a time (5 firms × 10,
// rotating firm by firm: A B C D E A B …, so the same firm comes back every 5 accounts), never more than that first investment:
// the batch runs until every one of the 50 has passed or blown, then a new batch of 50 starts the next day. Compared: one trade a
// day vs trading until the day target vs full recovery • +$1,500 / +$900 targets with −$500 / −$1,000 stops • every side
// combination • the sessions. Chosen on Oct 2020 – 2023, judged on 2024 – 2026 (and against random sides on many seeds).
// The app's own engine (KeystoneRotationLab.Run + KeystonePropSim): stop first at both legs' worst of the minute, targets at a
// minute's close, costs per contract, the firm's trailing drawdown / consistency / minimum days.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;

public static class PassStudy
{
    public static readonly string[] Firms = { "NORTHWIND CAPITAL", "BLUEPEAK FUNDING", "IRONBRIDGE TRADERS", "SUMMIT EDGE PROP", "COBALT RIDGE FUTURES" };   // made-up names
    public const int Batch = 50, QM = 10, QG = 10;
    public static readonly string[][] Plans = { new[] { "ONE", "ONE TRADE A DAY" }, new[] { "UNTIL", "UNTIL THE DAY TARGET" }, new[] { "RECOVER", "FULL RECOVERY" } };
    public static readonly double[][] Brackets = { new double[] { 1500, 500 }, new double[] { 1500, 1000 }, new double[] { 900, 500 }, new double[] { 900, 1000 } };
    public static readonly string[] Dirs = { "BUY", "SELL", "BS", "SB", "RANDOM", "RANDOMHEDGE", "RANDOM4", "CYCLE4", "FOLLOW5", "FADE5", "FOLLOW5EACH", "FADE5EACH", "NQLEAD5" };
    public static readonly string[] SessNames = { "ASIA 20:00–00:00", "LONDON 03:00–07:00", "NY OPEN 09:30–11:00", "NY MID 11:00–13:30", "NY CLOSE 13:30–15:55", "NY DAY 09:30–15:55" };
    public static readonly int[][] SessHours = { new[] { 2000, 2359 }, new[] { 300, 659 }, new[] { 930, 1059 }, new[] { 1100, 1329 }, new[] { 1330, 1555 }, new[] { 930, 1555 } };
    public static readonly DateTime OosStart = new DateTime(2024, 1, 1);

    public sealed class Cfg { public int Plan, Br, Dir, Sess; public string Key { get { return Plans[Plan][0] + "|" + Brackets[Br][0] + "/" + Brackets[Br][1] + "|" + Dirs[Dir] + "|" + SessNames[Sess]; } } }
    public sealed class BatchRes { public DateTime Start, End; public int Days, Passed, Blown, Open, Trades, Wins; public double Pnl, Spent; public int[] FirmPassed = new int[5], FirmBlown = new int[5]; public List<int> PassDays = new List<int>(); }
    public sealed class Res
    {
        public Cfg C; public List<BatchRes> B = new List<BatchRes>();
        public int Accounts { get { return B.Sum(b => b.Passed + b.Blown); } }
        public int Passed { get { return B.Sum(b => b.Passed); } }
        public double PassRate { get { return Accounts == 0 ? 0 : 100.0 * Passed / Accounts; } }
        public double Spent { get { return B.Sum(b => b.Spent); } }
        public int Trades { get { return B.Sum(b => b.Trades); } }
        public double WinRate { get { return Trades == 0 ? 0 : 100.0 * B.Sum(b => b.Wins) / Trades; } }
        public double Pnl { get { return B.Sum(b => b.Pnl); } }
        public double AvgBatchDays { get { var d = B.Where(b => b.Open == 0).ToList(); return d.Count == 0 ? 0 : d.Average(b => b.Days); } }
        public double AvgPassDays { get { var d = B.SelectMany(b => b.PassDays).ToList(); return d.Count == 0 ? 0 : d.Average(); } }
    }

    static KeystoneSimSettings Settings(Cfg c, int seed)
    {
        var s = new KeystoneSimSettings
        {
            Strategy = "ROTATION", Accounts = Batch, BuyRule = "BREAKEVEN", ReplacePassed = false, FundedWhen = "AFTER", Direction = Dirs[c.Dir], Seed = seed,
            MnqQty = QM, MgcQty = QG, FundMnqQty = QM, FundMgcQty = QG, RotTarget = Brackets[c.Br][0], RotStop = Brackets[c.Br][1], FundRotTarget = Brackets[c.Br][0], FundRotStop = Brackets[c.Br][1],
            PauseSeconds = 60, StartHhmm = SessHours[c.Sess][0], EndHhmm = SessHours[c.Sess][1], Rules = KeystoneSimRules.Presets()[1].Copy(), MinRoomShare = 0.25
        };
        if (c.Plan == 0) { s.MaxTradesPerDay = 1; s.DayPlan = "ONEWIN"; }
        else if (c.Plan == 1) { s.MaxTradesPerDay = 0; s.DayPlan = "NONE"; }
        else { s.MaxTradesPerDay = 0; s.DayPlan = "RECOVER"; }
        return s;
    }

    // batches of 50 over [from, to]: each runs until all 50 are passed / blown, the next starts the day after
    public static Res Run(Cfg c, List<KeystoneRotDay> days, DateTime from, DateTime to, int seed)
    {
        var r = new Res { C = c }; int i = days.FindIndex(d => d.Day >= from); if (i < 0) return r; int bn = 0;
        while (i < days.Count && days[i].Day <= to)
        {
            var all = days.Skip(i).TakeWhile(d => d.Day <= to).ToList(); KeystonePropSim sim = null; int take = 120; bool open = true;
            while (true)
            {
                var chunk = all.Take(take).ToList(); sim = KeystoneRotationLab.Run(Settings(c, seed * 1000 + bn), chunk, SessHours[c.Sess][0], SessHours[c.Sess][1], null);
                open = sim.Accounts.Any(a => a.Id <= Batch && a.Phase == "EVAL");
                if (!open || take >= all.Count) break; take *= 2;
            }
            var ev = sim.Accounts.Where(a => a.Id <= Batch).ToList();
            Func<KeystoneSimAccount, DateTime> doneOn = a => a.PassedOn != DateTime.MinValue ? a.PassedOn : a.Ended;
            var finished = ev.Where(a => a.Phase != "EVAL").ToList();
            DateTime end = open ? all[all.Count - 1].Day : finished.Max(doneOn);
            var b = new BatchRes { Start = all[0].Day, End = end, Open = ev.Count(a => a.Phase == "EVAL"), Spent = ev.Sum(a => a.Rules0Cost()) };
            b.Days = days.Count(d => d.Day >= b.Start && d.Day <= b.End);
            foreach (var a in finished)
            {
                int f = (a.Id - 1) % 5; bool passed = a.PassedOn != DateTime.MinValue;
                if (passed) { b.Passed++; b.FirmPassed[f]++; b.PassDays.Add(a.EvalDays); } else { b.Blown++; b.FirmBlown[f]++; }
            }
            var tr = sim.Trades.Where(t => t.Account <= Batch && t.Phase == "EVAL" && t.Day <= end).ToList();
            b.Trades = tr.Count; b.Wins = tr.Count(t => t.Pnl > 0); b.Pnl = tr.Sum(t => t.Pnl);
            r.B.Add(b); bn++;
            if (open) break;
            i = days.FindIndex(d => d.Day > end); if (i < 0) break;
        }
        return r;
    }
    static double Rules0Cost(this KeystoneSimAccount a) { return a.Spent; }

    // the trades of one configuration (for the side / hour / firm tables of the chosen one)
    public static List<KeystoneSimTrade> Trades(Cfg c, List<KeystoneRotDay> days, DateTime from, DateTime to, int seed)
    {
        var list = new List<KeystoneSimTrade>(); int i = days.FindIndex(d => d.Day >= from); int bn = 0;
        while (i >= 0 && i < days.Count && days[i].Day <= to)
        {
            var all = days.Skip(i).TakeWhile(d => d.Day <= to).ToList(); KeystonePropSim sim = null; int take = 120; bool open = true;
            while (true) { sim = KeystoneRotationLab.Run(Settings(c, seed * 1000 + bn), all.Take(take).ToList(), SessHours[c.Sess][0], SessHours[c.Sess][1], null); open = sim.Accounts.Any(a => a.Id <= Batch && a.Phase == "EVAL"); if (!open || take >= all.Count) break; take *= 2; }
            var ev = sim.Accounts.Where(a => a.Id <= Batch && a.Phase != "EVAL").ToList();
            DateTime end = open ? all[all.Count - 1].Day : ev.Max(a => a.PassedOn != DateTime.MinValue ? a.PassedOn : a.Ended);
            list.AddRange(sim.Trades.Where(t => t.Account <= Batch && t.Phase == "EVAL" && t.Day <= end)); bn++;
            if (open) break; i = days.FindIndex(d => d.Day > end);
        }
        return list;
    }

    static string J(string s) { var b = new StringBuilder("\""); foreach (char ch in s ?? "") { if (ch == '"' || ch == '\\') b.Append('\\').Append(ch); else if (ch < 32) b.Append(' '); else b.Append(ch); } return b.Append('"').ToString(); }
    static string N(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 2).ToString(CultureInfo.InvariantCulture); }
    static string ResJ(Res r)
    {
        var b = new StringBuilder("{");
        b.Append("\"batches\":").Append(r.B.Count).Append(",\"accounts\":").Append(r.Accounts).Append(",\"passed\":").Append(r.Passed).Append(",\"rate\":").Append(N(r.PassRate))
         .Append(",\"spent\":").Append(N(r.Spent)).Append(",\"trades\":").Append(r.Trades).Append(",\"win\":").Append(N(r.WinRate)).Append(",\"pnl\":").Append(N(r.Pnl))
         .Append(",\"batchDays\":").Append(N(r.AvgBatchDays)).Append(",\"passDays\":").Append(N(r.AvgPassDays)).Append(",\"open\":").Append(r.B.Sum(x => x.Open))
         .Append(",\"firms\":[").Append(string.Join(",", Enumerable.Range(0, 5).Select(f => { int p = r.B.Sum(x => x.FirmPassed[f]), bl = r.B.Sum(x => x.FirmBlown[f]); return "[" + p + "," + bl + "]"; }))).Append("]")
         .Append(",\"batchRates\":[").Append(string.Join(",", r.B.Where(x => x.Passed + x.Blown > 0).Select(x => N(100.0 * x.Passed / (x.Passed + x.Blown))))).Append("]");
        return b.Append("}").ToString();
    }

    public static void Main2(List<RDay> mnq, List<RDay> mgc, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var gm = mgc.ToDictionary(d => d.Day.Date, d => d);
        var days = mnq.Where(m => gm.ContainsKey(m.Day.Date)).Select(m => RotationEntries.Day.Of(m, gm[m.Day.Date]).R).OrderBy(d => d.Day).ToList();
        DateTime first = days[0].Day, last = days[days.Count - 1].Day;
        say("PASS STUDY • MNQ + MGC rotation • " + days.Count + " days with both • " + first.ToString("yyyy-MM-dd") + " → " + last.ToString("yyyy-MM-dd") + " • " + Batch + " evaluations a batch (5 firms × 10) • " + QM + " MNQ + " + QG + " MGC");
        var cfgs = new List<Cfg>();
        for (int p = 0; p < Plans.Length; p++) for (int b = 0; b < Brackets.Length; b++) for (int d = 0; d < Dirs.Length; d++) for (int s = 0; s < SessNames.Length; s++) cfgs.Add(new Cfg { Plan = p, Br = b, Dir = d, Sess = s });
        if (Environment.GetEnvironmentVariable("PASS_QUICK") == "1") cfgs = cfgs.Where(c => c.Sess == 5 && (c.Dir == 0 || c.Dir == 6)).ToList();
        var isR = new Res[cfgs.Count]; var oosR = new Res[cfgs.Count]; int doneN = 0;
        Parallel.For(0, cfgs.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            isR[i] = Run(cfgs[i], days, first, OosStart.AddDays(-1), 7); oosR[i] = Run(cfgs[i], days, OosStart, last, 7);
            int n = System.Threading.Interlocked.Increment(ref doneN); if (n % 50 == 0) Console.WriteLine("  " + n + " / " + cfgs.Count + " configurations • " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        });
        say("configurations: " + cfgs.Count + " in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        // the choice: the best pass rate 2020–23 (with ≥ 100 accounts finished), judged on 2024–26
        int best = Enumerable.Range(0, cfgs.Count).Where(i => isR[i].Accounts >= 100).OrderByDescending(i => isR[i].PassRate).ThenByDescending(i => isR[i].Pnl).First();
        say("chosen on 2020–23: " + cfgs[best].Key + " • IS pass " + isR[best].PassRate.ToString("0.0") + "% (" + isR[best].Passed + "/" + isR[best].Accounts + ") • OOS pass " + oosR[best].PassRate.ToString("0.0") + "% (" + oosR[best].Passed + "/" + oosR[best].Accounts + ")");
        // luck band: the chosen plan / bracket / session with RANDOM sides (any of 4) on 30 seeds, both periods
        var rnd = cfgs[best]; var rc = new Cfg { Plan = rnd.Plan, Br = rnd.Br, Sess = rnd.Sess, Dir = Array.IndexOf(Dirs, "RANDOM4") };
        var luckIs = new double[30]; var luckOos = new double[30];
        Parallel.For(0, 30, k => { luckIs[k] = Run(rc, days, first, OosStart.AddDays(-1), 100 + k).PassRate; luckOos[k] = Run(rc, days, OosStart, last, 100 + k).PassRate; });
        Array.Sort(luckIs); Array.Sort(luckOos);
        say("luck band (random sides, 30 seeds): IS " + luckIs[1].ToString("0.0") + "–" + luckIs[28].ToString("0.0") + "% (median " + luckIs[15].ToString("0.0") + ") • OOS " + luckOos[1].ToString("0.0") + "–" + luckOos[28].ToString("0.0") + "% (median " + luckOos[15].ToString("0.0") + ")");
        // the best of each family by OOS (for the conclusion) and the chosen one's batches + trades
        var full = Run(cfgs[best], days, first, last, 7); var trades = Trades(cfgs[best], days, first, last, 7);
        var sb = new StringBuilder("{");
        sb.Append("\"meta\":{\"days\":").Append(days.Count).Append(",\"first\":").Append(J(first.ToString("yyyy-MM-dd"))).Append(",\"last\":").Append(J(last.ToString("yyyy-MM-dd"))).Append(",\"oos\":").Append(J(OosStart.ToString("yyyy-MM-dd")))
          .Append(",\"batch\":").Append(Batch).Append(",\"qm\":").Append(QM).Append(",\"qg\":").Append(QG).Append(",\"rules\":").Append(J(KeystoneSimRules.Presets()[1].Describe())).Append(",\"cost\":").Append(N(KeystoneSimRules.Presets()[1].EvalCost))
          .Append(",\"firms\":[").Append(string.Join(",", Firms.Select(J))).Append("],\"plans\":[").Append(string.Join(",", Plans.Select(p => J(p[1])))).Append("],\"brackets\":[").Append(string.Join(",", Brackets.Select(b => J("+$" + b[0].ToString("N0", CultureInfo.InvariantCulture) + " / −$" + b[1].ToString("N0", CultureInfo.InvariantCulture)))))
          .Append("],\"dirs\":[").Append(string.Join(",", Dirs.Select(d => J(KeystoneSimRotation.DirectionName(d))))).Append("],\"sessions\":[").Append(string.Join(",", SessNames.Select(J))).Append("]")
          .Append(",\"luckIs\":[").Append(string.Join(",", luckIs.Select(N))).Append("],\"luckOos\":[").Append(string.Join(",", luckOos.Select(N))).Append("],\"best\":").Append(best).Append("},");
        sb.Append("\"rows\":[");
        for (int i = 0; i < cfgs.Count; i++) { if (i > 0) sb.Append(","); var c = cfgs[i]; sb.Append("{\"p\":").Append(c.Plan).Append(",\"b\":").Append(c.Br).Append(",\"d\":").Append(c.Dir).Append(",\"s\":").Append(c.Sess).Append(",\"is\":").Append(ResJ(isR[i])).Append(",\"oos\":").Append(ResJ(oosR[i])).Append("}"); }
        sb.Append("],\"chosen\":{\"full\":").Append(ResJ(full)).Append(",\"batches\":[");
        sb.Append(string.Join(",", full.B.Select(b => "{\"s\":" + J(b.Start.ToString("yyyy-MM-dd")) + ",\"e\":" + J(b.End.ToString("yyyy-MM-dd")) + ",\"days\":" + b.Days + ",\"passed\":" + b.Passed + ",\"blown\":" + b.Blown + ",\"open\":" + b.Open + ",\"pnl\":" + N(b.Pnl) + ",\"trades\":" + b.Trades + ",\"firms\":[" + string.Join(",", Enumerable.Range(0, 5).Select(f => "[" + b.FirmPassed[f] + "," + b.FirmBlown[f] + "]")) + "]}")));
        sb.Append("],\"hours\":[");
        sb.Append(string.Join(",", trades.GroupBy(t => t.Entry.Hour).OrderBy(g => (g.Key + 6) % 24).Select(g => "[" + g.Key + "," + g.Count() + "," + g.Count(t => t.Pnl > 0) + "," + N(g.Sum(t => t.Pnl)) + "]")));
        sb.Append("],\"sides\":[");
        sb.Append(string.Join(",", trades.GroupBy(t => { int dM, dG, qM, qG; KeystoneBreakevenStudy.Legs(t.Legs, out dM, out dG, out qM, out qG); return KeystoneSimRotation.SidesName(dM, dG); }).OrderByDescending(g => g.Count()).Select(g => "[" + J(g.Key) + "," + g.Count() + "," + g.Count(t => t.Pnl > 0) + "," + N(g.Sum(t => t.Pnl)) + "]")));
        sb.Append("],\"years\":[");
        sb.Append(string.Join(",", full.B.GroupBy(b => b.Start.Year).Select(g => "[" + g.Key + "," + g.Sum(b => b.Passed) + "," + g.Sum(b => b.Passed + b.Blown) + "]")));
        sb.Append("]}}");
        Directory.CreateDirectory(outDir); File.WriteAllText(Path.Combine(outDir, "pass_study.json"), sb.ToString()); File.WriteAllText(Path.Combine(outDir, "pass_study_log.txt"), log.ToString());
        say("written " + Path.Combine(outDir, "pass_study.json") + " • " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
