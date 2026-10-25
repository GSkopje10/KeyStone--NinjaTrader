// EVAL BOOST STUDY: what lifts the pass rate of a batch of 50 evaluations above the 20.8 % of BUY BOTH 10+10 +$1,500 / −$1,000,
// and what the passed accounts then pay when they trade funded. Every rule is measured on EACH year 2021–2025 (the history, where
// rules are chosen) and on 2026 (the test: Jan → Oct, never used to choose), with the app's own engine (KeystoneRotationLab.Run).
// Families: the bracket (stop size up to the whole room, one trade a day or until +$1,500, the size), one instrument, the sides
// (fixed, yesterday's direction, the last 5 sessions, a 30 / 60-minute confirmation, buy the dip), quiet / busy days, the start hour,
// copy trading; then the best of each combined. Funded: the passed accounts of ONE batch trade funded (same style or gentler,
// withdraw as soon as allowed or only the full $2,000 at +$4,000) until every one is blown or retired — no new evaluations until the
// batch is done — and the money: one batch only, or the payouts reinvested into the next batches (the first $6,000 is all you put in).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;

public static class EvalBoostStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class Feat { public double ChM, ChG, RangeM, MedRangeM; public double Ch5M, Ch5G; public DayOfWeek Dow; }
    static Dictionary<DateTime, Feat> feats = new Dictionary<DateTime, Feat>();

    sealed class Cfg
    {
        public string Name = "", Group = "", Rule = "BUY"; public double Target = 1500, Stop = 1000; public int QM = 10, QG = 10, Tries = 1, Start = 1800, End = 1645, Seed = 7; public string Alloc = "ROTATE";
        public string Key { get { return Rule + "|" + Stop + "|" + Tries + "|" + QM + "|" + QG + "|" + Start + "|" + Alloc + "|" + Target; } }
        public Cfg With(Action<Cfg> f) { var c = (Cfg)MemberwiseClone(); f(c); return c; }
        public string Geometry { get { return (QG == 0 ? QM + " MNQ" : QM == 0 ? QG + " MGC" : QM + "+" + QG) + " • +$" + Target.ToString("N0", IC) + " / " + (Stop >= 2000 ? "the whole room" : "−$" + Stop.ToString("N0", IC)) + " • " + (Tries == 1 ? "1 a day" : "until +$1,500"); } }
    }
    sealed class Period { public string Name; public bool Test; public List<KeystoneRotDay> Days; }
    sealed class Score { public int Passed, Failed, Bought, Batches, Zero; public double Rate { get { return Passed + Failed == 0 ? 0 : 100.0 * Passed / (Passed + Failed); } } }

    static KeystoneSimSettings Base()
    {
        var r = KeystoneSimRules.Presets().First(x => x.Name == "LUCID FLEX 50K").Copy();
        r.Target = 3000; r.EvalDrawdown = 2000; r.EvalDdType = "EOD"; r.EvalConsistency = 50; r.EvalMinDays = 2; r.MaxMicros = 40; r.FlatByHhmm = 1645;
        return new KeystoneSimSettings { Strategy = "ROTATION", Accounts = 50, StartAs = "EVAL", FundedWhen = "ASK", BuyRule = "BATCH", Allocation = "ROTATE", ReplacePassed = true, DayPlan = "NONE", MaxTradesPerDay = 1,
            PauseSeconds = 60, Auto = true, AutoFallback = "WAIT", TrailStop = false, RotTarget = 1500, FundRotTarget = 1500, Rules = r, MinRoomShare = 0.25 };
    }
    static KeystoneSimSettings Of(Cfg c)
    {
        var s = Base(); s.Direction = c.Rule == "RANDOM" ? "RANDOM" : "BUY"; s.RotTarget = s.FundRotTarget = c.Target; s.RotStop = s.FundRotStop = c.Stop; s.MaxTradesPerDay = c.Tries;
        s.MnqQty = s.FundMnqQty = c.QM; s.MgcQty = s.FundMgcQty = c.QG; s.StartHhmm = c.Start; s.EndHhmm = c.End; s.Seed = c.Seed; s.Allocation = c.Alloc; return s;
    }
    static Tuple<int, int> T(int a, int b) { return Tuple.Create(a, b); }
    static int Sg(double v) { return v > 0 ? 1 : v < 0 ? -1 : 0; }
    // the sides of a trade at minute k (0, 0 = wait for the next minute); null = the engine's own (BUY / RANDOM)
    static Func<KeystoneRotDay, int, Tuple<int, int>> SidesOf(string rule)
    {
        Func<KeystoneRotDay, Feat> F = d => { Feat f; return feats.TryGetValue(d.Day, out f) ? f : null; };
        Func<KeystoneRotDay, int, int, int> trM = (d, k, n) => k < n ? 0 : Sg(d.CloseM(k - 1) - d.OpenM(k - n)); Func<KeystoneRotDay, int, int, int> trG = (d, k, n) => k < n ? 0 : Sg(d.CloseG(k - 1) - d.OpenG(k - n));
        switch (rule)
        {
            case "BUY": case "RANDOM": return null;
            case "SELL": return (d, k) => T(-1, -1);
            case "BS": return (d, k) => T(1, -1);
            case "SB": return (d, k) => T(-1, 1);
            case "YDAY": return (d, k) => { var f = F(d); return f == null ? T(1, 1) : T(f.ChM < 0 ? -1 : 1, f.ChG < 0 ? -1 : 1); };
            case "WEEK": return (d, k) => { var f = F(d); return f == null ? T(1, 1) : T(f.Ch5M < 0 ? -1 : 1, f.Ch5G < 0 ? -1 : 1); };
            case "TREND30": return (d, k) => { int a = trM(d, k, 30), b = trG(d, k, 30); return a != 0 && a == b ? T(a, b) : T(0, 0); };
            case "TREND60": return (d, k) => { int a = trM(d, k, 60), b = trG(d, k, 60); return a != 0 && a == b ? T(a, b) : T(0, 0); };
            case "UP30": return (d, k) => { int a = trM(d, k, 30), b = trG(d, k, 30); return a > 0 && b > 0 ? T(1, 1) : T(0, 0); };
            case "DIP30": return (d, k) => { int a = trM(d, k, 30), b = trG(d, k, 30); return a < 0 && b < 0 ? T(1, 1) : T(0, 0); };
            case "EACH30": return (d, k) => { int a = trM(d, k, 30), b = trG(d, k, 30); return T(a == 0 ? 1 : a, b == 0 ? 1 : b); };
            case "WEEKUP": return (d, k) => { var f = F(d); return f != null && f.Ch5M > 0 && f.Ch5G > 0 ? T(1, 1) : T(0, 0); };
            case "CALM": return (d, k) => { var f = F(d); return f != null && f.MedRangeM > 0 && f.RangeM <= f.MedRangeM ? T(1, 1) : T(0, 0); };
            case "BUSY": return (d, k) => { var f = F(d); return f != null && f.MedRangeM > 0 && f.RangeM > f.MedRangeM ? T(1, 1) : T(0, 0); };
            case "NOMON": return (d, k) => d.Day.DayOfWeek == DayOfWeek.Monday ? T(0, 0) : T(1, 1);
            case "NOFRI": return (d, k) => d.Day.DayOfWeek == DayOfWeek.Friday ? T(0, 0) : T(1, 1);
            case "WEEKUP_DIP": return (d, k) => { var f = F(d); if (f == null || !(f.Ch5M > 0 && f.Ch5G > 0)) return T(0, 0); int a = trM(d, k, 30), b = trG(d, k, 30); return a < 0 && b < 0 ? T(1, 1) : T(0, 0); };
        }
        throw new ArgumentException(rule);
    }
    static readonly Dictionary<string, string> RuleNames = new Dictionary<string, string> {
        { "BUY", "▲▲ BUY BOTH" }, { "RANDOM", "🎲 RANDOM BB / SS" }, { "SELL", "▼▼ SELL BOTH" }, { "BS", "▲▼ BUY MNQ • SELL MGC" }, { "SB", "▼▲ SELL MNQ • BUY MGC" },
        { "YDAY", "↻ each leg follows yesterday's direction" }, { "WEEK", "📅 each leg follows the last 5 sessions" },
        { "TREND30", "⏳ wait: both moved the same way over 30 min → that side" }, { "TREND60", "⏳ wait: both moved the same way over 60 min → that side" },
        { "UP30", "⏳ wait: both UP over 30 min → buy both" }, { "DIP30", "⏳ wait: both DOWN over 30 min → buy the dip" }, { "EACH30", "each leg follows its own last 30 min" },
        { "WEEKUP", "📅 only when both rose over the last 5 sessions → buy both" }, { "WEEKUP_DIP", "📅 both up over 5 sessions + a 30-min dip → buy" },
        { "CALM", "🌤 only after a quiet day (MNQ range under its 20-day median)" }, { "BUSY", "⚡ only after a busy day (range over the median)" },
        { "NOMON", "no new trades on Mondays" }, { "NOFRI", "no new trades on Fridays" } };

    static Score Measure(KeystonePropSim sim)
    {
        var sc = new Score { Bought = sim.Bought, Passed = sim.PassedCount, Failed = sim.BlownEval + sim.Accounts.Count(a => a.Phase == "RETIRED" && a.WasPhase == "EVAL") };
        sc.Batches = sim.Batches.Count; sc.Zero = sim.Batches.Count(b => b.Passed == 0); return sc;
    }
    sealed class Row { public Cfg C; public Dictionary<string, Score> By = new Dictionary<string, Score>(); public Score Hist = new Score(), Test = new Score(); public double MinYear = 999, MaxYear = 0; }

    static List<Row> Eval(IList<Cfg> cfgs, List<Period> periods, string tag)
    {
        var rows = cfgs.Select(c => new Row { C = c }).ToList(); var jobs = new List<Tuple<int, int>>();
        for (int i = 0; i < rows.Count; i++) for (int p = 0; p < periods.Count; p++) jobs.Add(Tuple.Create(i, p));
        var res = new Score[rows.Count, periods.Count]; int done = 0;
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, j =>
        {
            var c = rows[j.Item1].C; var per = periods[j.Item2];
            var sim = KeystoneRotationLab.Run(Of(c), per.Days, c.Start, c.End, null, SidesOf(c.Rule)); res[j.Item1, j.Item2] = Measure(sim);
            int n = System.Threading.Interlocked.Increment(ref done); if (n % 40 == 0) Console.WriteLine("  " + tag + " " + n + " / " + jobs.Count);
        });
        for (int i = 0; i < rows.Count; i++)
            for (int p = 0; p < periods.Count; p++)
            {
                var s = res[i, p]; rows[i].By[periods[p].Name] = s; var agg = periods[p].Test ? rows[i].Test : rows[i].Hist;
                agg.Passed += s.Passed; agg.Failed += s.Failed; agg.Bought += s.Bought; agg.Batches += s.Batches; agg.Zero += s.Zero;
                if (!periods[p].Test) { rows[i].MinYear = Math.Min(rows[i].MinYear, s.Rate); rows[i].MaxYear = Math.Max(rows[i].MaxYear, s.Rate); }
            }
        return rows;
    }

    // ---------- the funded stage and the money (one batch at a time: evaluations → the passed ones funded → all done → the next)
    sealed class Fund { public string Name, Key; public int QM, QG, Tries; public double Target, Stop, PayoutMin; }
    sealed class BatchOut { public int Bought, Passed, Funded, PaidAccounts, Payouts, Blown, Retired, Open, Days; public double Spent, Cash; public DateTime Start, End, FirstPayout, BreakEven; public bool Finished; }
    static BatchOut RunBatch(Cfg c, Fund f, List<KeystoneRotDay> days, int from, int n)
    {
        var s = Of(c); s.Accounts = n; s.FundedWhen = "STAGES"; s.BuyRule = "BATCH"; s.FundMnqQty = f.QM; s.FundMgcQty = f.QG; s.FundRotTarget = f.Target; s.FundRotStop = f.Stop; s.FundMaxTrades = f.Tries; s.FundDayPlan = "SAME";
        s.Rules.PayoutMin = f.PayoutMin;
        var part = days.Skip(from).ToList(); var sim = KeystoneRotationLab.Run(s, part, c.Start, c.End, null, SidesOf(c.Rule));
        var first = part[0].Day; var mine = sim.Accounts.Where(a => a.Bought == first).ToList(); var ids = new HashSet<int>(mine.Select(a => a.Id));
        var o = new BatchOut { Bought = mine.Count, Start = first, Spent = mine.Sum(a => a.Spent) };
        o.Passed = mine.Count(a => a.PassedOn != DateTime.MinValue); o.Payouts = mine.Sum(a => a.Payouts); o.PaidAccounts = mine.Count(a => a.Payouts > 0); o.Cash = mine.Sum(a => a.Cash);
        o.Retired = mine.Count(a => a.Phase == "RETIRED" && a.WasPhase == "FUNDED"); o.Open = mine.Count(a => a.Alive); o.Finished = o.Open == 0;
        o.End = o.Finished ? mine.Max(a => a.Ended) : part[part.Count - 1].Day; o.Days = part.Count(d => d.Day <= o.End);
        double cum = 0; foreach (var e in sim.Events.Where(e => e.Kind == "PAYOUT" && ids.Contains(e.Account)).OrderBy(e => e.Time)) { if (o.FirstPayout == DateTime.MinValue) o.FirstPayout = e.Time.Date; cum += e.Amount; if (cum >= o.Spent - 1e-6 && o.BreakEven == DateTime.MinValue) o.BreakEven = e.Time.Date; }
        return o;
    }
    sealed class Money { public string Mode; public int Batches, Bought, Passed, Payouts, PaidAccounts; public double Invested, Cash, Final; public bool Stopped, Open; public DateTime BreakEven; public List<double> Curve = new List<double>(); public List<string> CurveDays = new List<string>(); }
    static Money Reinvest(Cfg c, Fund f, List<KeystoneRotDay> days, double start, int size)
    {
        var m = new Money { Mode = "REINVEST", Invested = start }; double bank = start; int i = 0;
        while (i < days.Count - 5)
        {
            int n = Math.Min(size, (int)Math.Floor(bank / 120.0 + 1e-9)); if (n < 1) { m.Stopped = true; break; }
            var o = RunBatch(c, f, days, i, n); bank -= o.Spent; m.Curve.Add(bank); m.CurveDays.Add(o.Start.ToString("yyyy-MM-dd", IC));
            bank += o.Cash; m.Curve.Add(bank); m.CurveDays.Add(o.End.ToString("yyyy-MM-dd", IC));
            m.Batches++; m.Bought += o.Bought; m.Passed += o.Passed; m.Payouts += o.Payouts; m.PaidAccounts += o.PaidAccounts; m.Cash += o.Cash;
            if (m.BreakEven == DateTime.MinValue && bank >= start - 1e-6 && o.Cash > 0) m.BreakEven = o.BreakEven != DateTime.MinValue ? o.BreakEven : o.End;
            if (!o.Finished) { m.Open = true; break; }
            int next = days.FindIndex(d => d.Day > o.End); if (next < 0) break; i = next;
        }
        m.Final = bank; return m;
    }

    // ---------- JSON (no library)
    static string J(string s) { return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
    static string N(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : Math.Round(v, 2).ToString(IC); }

    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var all = KeystoneStudioStore.Sessions(folder, "MNQ").Keys.Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday).OrderBy(d => d).ToList();
        var allDays = KeystoneRotationLab.LoadDays(folder, all.Where(d => d >= new DateTime(2020, 10, 1)));
        // day features from the sessions BEFORE each day (no peeking)
        for (int i = 0; i < allDays.Count; i++)
        {
            var d = allDays[i]; var f = new Feat { Dow = d.Day.DayOfWeek };
            if (i >= 1) { var p = allDays[i - 1]; f.ChM = p.CloseM(KeystoneRotDay.Slots - 1) - p.OpenM(0); f.ChG = p.CloseG(KeystoneRotDay.Slots - 1) - p.OpenG(0); f.RangeM = Range(p); }
            if (i >= 5) { f.Ch5M = allDays[i - 1].CloseM(KeystoneRotDay.Slots - 1) - allDays[i - 5].OpenM(0); f.Ch5G = allDays[i - 1].CloseG(KeystoneRotDay.Slots - 1) - allDays[i - 5].OpenG(0); }
            if (i >= 21) { var r = Enumerable.Range(i - 21, 20).Select(j => Range(allDays[j])).OrderBy(x => x).ToList(); f.MedRangeM = r[10]; }
            feats[d.Day] = f;
        }
        var periods = new List<Period>();
        foreach (var y in new[] { 2021, 2022, 2023, 2024, 2025 }) periods.Add(new Period { Name = y.ToString(IC), Days = allDays.Where(d => d.Day.Year == y).ToList() });
        periods.Add(new Period { Name = "2026", Test = true, Days = allDays.Where(d => d.Day.Year == 2026).ToList() });
        foreach (var p in periods) Console.WriteLine(p.Name + ": " + p.Days.Count + " days");
        var baseC = new Cfg { Name = "TODAY: BUY BOTH 10+10 +$1,500 / −$1,000, 1 a day", Group = "baseline" };

        // 1 the bracket and the size (BUY BOTH)
        var g1 = new List<Cfg>();
        foreach (var st in new[] { 1000.0, 1500, 2000 }) foreach (var tr in new[] { 1, 0 }) foreach (var q in new[] { 5, 7, 10 }) g1.Add(baseC.With(c => { c.Group = "bracket"; c.Stop = st; c.Tries = tr; c.QM = c.QG = q; }));
        foreach (var tg in new[] { 750.0, 1000 }) foreach (var st in new[] { 1000.0, 2000 }) g1.Add(baseC.With(c => { c.Group = "bracket"; c.Target = tg; c.Stop = st; c.Tries = 0; }));
        // 2 one instrument
        foreach (var st in new[] { 1000.0, 2000 }) foreach (var q in new[] { 10, 20 }) { g1.Add(baseC.With(c => { c.Group = "instrument"; c.Stop = st; c.QM = q; c.QG = 0; })); g1.Add(baseC.With(c => { c.Group = "instrument"; c.Stop = st; c.QM = 0; c.QG = q; })); }
        // 3 the sides, confirmations and day filters (two brackets each)
        foreach (var rule in RuleNames.Keys.Where(r => r != "BUY")) foreach (var st in new[] { 1000.0, 2000 }) g1.Add(baseC.With(c => { c.Group = "sides"; c.Rule = rule; c.Stop = st; }));
        // 4 the start hour
        foreach (var h in new[] { 2000, 300, 800, 930 }) foreach (var st in new[] { 1000.0, 2000 }) g1.Add(baseC.With(c => { c.Group = "hours"; c.Start = h; c.Stop = st; }));
        // 4b MNQ only around the winner: the size, the stop, the tries, with the best day filters
        foreach (var q in new[] { 5, 7, 10, 12, 15 }) foreach (var st in new[] { 1500.0, 1750, 2000 }) foreach (var tr in new[] { 1, 0 }) g1.Add(baseC.With(c => { c.Group = "mnq"; c.QM = q; c.QG = 0; c.Stop = st; c.Tries = tr; }));
        foreach (var rule in new[] { "BUSY", "WEEKUP", "WEEKUP_DIP", "TREND30", "CALM" }) foreach (var q in new[] { 7, 10 }) g1.Add(baseC.With(c => { c.Group = "mnq"; c.Rule = rule; c.QM = q; c.QG = 0; c.Stop = 2000; }));
        foreach (var h in new[] { 2000, 300 }) g1.Add(baseC.With(c => { c.Group = "mnq"; c.QM = 10; c.QG = 0; c.Stop = 2000; c.Start = h; }));
        // 5 copy trading (every account takes every trade)
        foreach (var st in new[] { 1000.0, 2000 }) g1.Add(baseC.With(c => { c.Group = "copy"; c.Alloc = "COPY"; c.Stop = st; }));
        g1.Insert(0, baseC); g1 = g1.GroupBy(c => c.Key).Select(g => g.First()).ToList();
        var rows = Eval(g1, periods, "families");
        // 6 the best of each family combined (chosen on 2021–2025 only)
        Func<string, int, List<Row>> top = (grp, n) => rows.Where(r => r.C.Group == grp && r.Hist.Passed + r.Hist.Failed >= 100).OrderByDescending(r => r.Hist.Rate).Take(n).ToList();
        var geos = top("bracket", 3).Select(r => r.C).ToList(); var sides = top("sides", 4).Select(r => r.C.Rule).Distinct().Take(3).ToList(); sides.Insert(0, "BUY"); var hours = new List<int> { 1800 }; hours.AddRange(top("hours", 2).Select(r => r.C.Start));
        var combos = new List<Cfg>();
        foreach (var g in geos) foreach (var sd in sides.Distinct()) foreach (var h in hours.Distinct()) combos.Add(g.With(c => { c.Group = "combo"; c.Rule = sd; c.Start = h; }));
        combos = combos.Where(c => !rows.Any(r => r.C.Key == c.Key)).GroupBy(c => c.Key).Select(g => g.First()).ToList();
        rows.AddRange(Eval(combos, periods, "combos"));
        var best = rows.Where(r => r.Hist.Passed + r.Hist.Failed >= 100).OrderByDescending(r => r.Hist.Rate).First();
        var baseRow = rows.First(r => r.C.Key == baseC.Key);
        var bestBuy = rows.Where(r => r.C.Rule == "BUY" && r.C.Start == 1800 && r.C.Alloc == "ROTATE" && r.Hist.Passed + r.Hist.Failed >= 100).OrderByDescending(r => r.Hist.Rate).First();
        Console.WriteLine("best: " + Label(best.C) + " hist " + best.Hist.Rate.ToString("0.0", IC) + " test " + best.Test.Rate.ToString("0.0", IC));

        // 7 funded + money for the baseline, the best plain BUY BOTH and the best overall
        var funds = new List<Fund> {
            new Fund { Name = "same as the evaluation • withdraw when allowed", Key = "SAME_ANY", QM = -1, Tries = 1, PayoutMin = 0 },
            new Fund { Name = "same as the evaluation • only the full $2,000 at +$4,000", Key = "SAME_FULL", QM = -1, Tries = 1, PayoutMin = 2000 },
            new Fund { Name = "half size 5+5 • +$750 / −$750 • withdraw when allowed", Key = "HALF_ANY", QM = 5, QG = 5, Target = 750, Stop = 750, Tries = 1, PayoutMin = 0 },
            new Fund { Name = "half size 5+5 • +$750 / −$750 • only the full $2,000", Key = "HALF_FULL", QM = 5, QG = 5, Target = 750, Stop = 750, Tries = 1, PayoutMin = 2000 },
            new Fund { Name = "small 3+3 • +$500 / −$500 • only the full $2,000", Key = "SMALL_FULL", QM = 3, QG = 3, Target = 500, Stop = 500, Tries = 1, PayoutMin = 2000 },
            new Fund { Name = "two lives: 10 MNQ • +$1,500 / −$1,000 • withdraw when allowed", Key = "MNQ_1000_ANY", QM = 10, QG = 0, Target = 1500, Stop = 1000, Tries = 1, PayoutMin = 0 },
            new Fund { Name = "steady: 5 MNQ • +$500 / −$1,000 • withdraw when allowed", Key = "MNQ5_500_ANY", QM = 5, QG = 0, Target = 500, Stop = 1000, Tries = 1, PayoutMin = 0 },
            new Fund { Name = "qualify: 3 MNQ • +$300 / −$600 • withdraw when allowed", Key = "MNQ3_300_ANY", QM = 3, QG = 0, Target = 300, Stop = 600, Tries = 1, PayoutMin = 0 },
            new Fund { Name = "steady: 5 MNQ • +$500 / −$1,000 • only the full $2,000", Key = "MNQ5_500_FULL", QM = 5, QG = 0, Target = 500, Stop = 1000, Tries = 1, PayoutMin = 2000 } };
        var evalPick = new List<Row> { baseRow, bestBuy, best }.GroupBy(r => r.C.Key).Select(g => g.First()).ToList();
        var mjobs = new List<Tuple<Row, Fund, Period>>(); foreach (var r in evalPick) foreach (var f in funds) foreach (var p in periods) mjobs.Add(Tuple.Create(r, f, p));
        var one = new Dictionary<string, BatchOut>(); var re = new Dictionary<string, Money>(); int md = 0;
        Parallel.ForEach(mjobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, j =>
        {
            var c = j.Item1.C; var f = j.Item2; var ff = new Fund { Name = f.Name, Key = f.Key, QM = f.QM < 0 ? c.QM : f.QM, QG = f.QM < 0 ? c.QG : f.QG, Target = f.QM < 0 ? c.Target : f.Target, Stop = f.QM < 0 ? c.Stop : f.Stop, Tries = f.Tries, PayoutMin = f.PayoutMin };
            var o = RunBatch(c, ff, j.Item3.Days, 0, 50); var m = Reinvest(c, ff, j.Item3.Days, 6000, 50);
            lock (one) { one[c.Key + "#" + f.Key + "#" + j.Item3.Name] = o; re[c.Key + "#" + f.Key + "#" + j.Item3.Name] = m; }
            int n = System.Threading.Interlocked.Increment(ref md); if (n % 10 == 0) Console.WriteLine("  money " + n + " / " + mjobs.Count);
        });

        // ---------- the JSON for the website + a short report
        var js = new StringBuilder("{");
        js.Append("\"periods\":[" + string.Join(",", periods.Select(p => "{\"name\":" + J(p.Name) + ",\"test\":" + (p.Test ? "true" : "false") + ",\"days\":" + p.Days.Count + ",\"from\":" + J(p.Days.First().Day.ToString("yyyy-MM-dd", IC)) + ",\"to\":" + J(p.Days.Last().Day.ToString("yyyy-MM-dd", IC)) + "}")) + "],");
        js.Append("\"rows\":[" + string.Join(",", rows.Select(r => "{\"key\":" + J(r.C.Key) + ",\"group\":" + J(r.C.Group) + ",\"label\":" + J(Label(r.C)) + ",\"rule\":" + J(RuleNames[r.C.Rule]) + ",\"geometry\":" + J(r.C.Geometry) + ",\"start\":" + J(Hm(r.C.Start)) + ",\"alloc\":" + J(r.C.Alloc)
            + ",\"hist\":" + N(r.Hist.Rate) + ",\"histPassed\":" + r.Hist.Passed + ",\"histFailed\":" + r.Hist.Failed + ",\"test\":" + N(r.Test.Rate) + ",\"testPassed\":" + r.Test.Passed + ",\"testFailed\":" + r.Test.Failed + ",\"testBought\":" + r.Test.Bought + ",\"minYear\":" + N(r.MinYear) + ",\"maxYear\":" + N(r.MaxYear)
            + ",\"zeroBatches\":" + (r.Hist.Zero + r.Test.Zero) + ",\"batches\":" + (r.Hist.Batches + r.Test.Batches) + ",\"years\":{" + string.Join(",", r.By.Select(kv => J(kv.Key) + ":{\"rate\":" + N(kv.Value.Rate) + ",\"passed\":" + kv.Value.Passed + ",\"failed\":" + kv.Value.Failed + ",\"zero\":" + kv.Value.Zero + ",\"batches\":" + kv.Value.Batches + "}")) + "}}")) + "],");
        js.Append("\"picks\":{\"baseline\":" + J(baseRow.C.Key) + ",\"bestBuy\":" + J(bestBuy.C.Key) + ",\"best\":" + J(best.C.Key) + "},");
        js.Append("\"funds\":[" + string.Join(",", funds.Select(f => "{\"key\":" + J(f.Key) + ",\"name\":" + J(f.Name) + "}")) + "],");
        js.Append("\"money\":[" + string.Join(",", one.Keys.OrderBy(k => k).Select(k => { var o = one[k]; var m = re[k]; var a = k.Split('#');
            return "{\"eval\":" + J(a[0]) + ",\"fund\":" + J(a[1]) + ",\"period\":" + J(a[2]) + ",\"one\":{\"bought\":" + o.Bought + ",\"passed\":" + o.Passed + ",\"paidAccounts\":" + o.PaidAccounts + ",\"payouts\":" + o.Payouts + ",\"spent\":" + N(o.Spent) + ",\"cash\":" + N(o.Cash) + ",\"net\":" + N(o.Cash - o.Spent)
                + ",\"days\":" + o.Days + ",\"finished\":" + (o.Finished ? "true" : "false") + ",\"open\":" + o.Open + ",\"firstPayout\":" + J(o.FirstPayout == DateTime.MinValue ? "" : o.FirstPayout.ToString("yyyy-MM-dd", IC)) + ",\"breakEven\":" + J(o.BreakEven == DateTime.MinValue ? "" : o.BreakEven.ToString("yyyy-MM-dd", IC)) + ",\"start\":" + J(o.Start.ToString("yyyy-MM-dd", IC)) + ",\"end\":" + J(o.End.ToString("yyyy-MM-dd", IC)) + "}"
                + ",\"re\":{\"batches\":" + m.Batches + ",\"bought\":" + m.Bought + ",\"passed\":" + m.Passed + ",\"payouts\":" + m.Payouts + ",\"paidAccounts\":" + m.PaidAccounts + ",\"invested\":" + N(m.Invested) + ",\"cash\":" + N(m.Cash) + ",\"final\":" + N(m.Final) + ",\"stopped\":" + (m.Stopped ? "true" : "false") + ",\"open\":" + (m.Open ? "true" : "false")
                + ",\"breakEven\":" + J(m.BreakEven == DateTime.MinValue ? "" : m.BreakEven.ToString("yyyy-MM-dd", IC)) + ",\"curve\":[" + string.Join(",", m.Curve.Select(N)) + "],\"curveDays\":[" + string.Join(",", m.CurveDays.Select(J)) + "]}}"; })) + "]");
        js.Append("}");
        File.WriteAllText(Path.Combine(outDir, "boost.json"), js.ToString());
        var sb = new StringBuilder("# EVAL BOOST STUDY\n\nHistory = 2021–2025 (rules chosen here), test = 2026 Jan → Oct (never used to choose). Batches of 50, Lucid Flex 50K rules.\n\n| rule | 2021–2025 pass % | worst / best year | 2026 pass % |\n|---|---|---|---|\n");
        foreach (var r in rows.OrderByDescending(r => r.Hist.Rate).Take(40)) sb.AppendLine("| " + Label(r.C) + " | " + r.Hist.Rate.ToString("0.0", IC) + " | " + r.MinYear.ToString("0", IC) + "–" + r.MaxYear.ToString("0", IC) + " | " + r.Test.Rate.ToString("0.0", IC) + " |");
        File.WriteAllText(Path.Combine(outDir, "report.md"), sb.ToString());
        Console.WriteLine("done");
    }
    static double Range(KeystoneRotDay d) { double hi = double.MinValue, lo = double.MaxValue; for (int i = 0; i < KeystoneRotDay.Slots; i++) if (d.HasM(i)) { hi = Math.Max(hi, d.MH[i]); lo = Math.Min(lo, d.ML[i]); } return hi > lo ? hi - lo : 0; }
    static string Hm(int h) { return (h / 100).ToString("00", IC) + ":" + (h % 100).ToString("00", IC); }
    static string Label(Cfg c) { return RuleNames[c.Rule] + " • " + c.Geometry + (c.Start != 1800 ? " • from " + Hm(c.Start) : "") + (c.Alloc == "COPY" ? " • COPY (every account every trade)" : ""); }
}
