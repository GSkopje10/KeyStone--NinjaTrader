// SYSTEM STUDY: the most money from prop firms. profit per evaluation = pass rate × what a funded account pays − evaluation price.
// Each year 2021–2026 is traded from January with $6,000-worth of evaluations at a time, comparing:
// • the evaluation rule: today's (BUY BOTH 10+10 +$1,500 / −$1,000) vs the boosted one (BUY 10 MNQ, +$1,500 / the whole room), 1 a day
// • the speed: BATCH (50 evaluations → the passed ones funded → only then the next 50) vs NONSTOP (50 evaluations always running,
//   a blown or passed one is replaced at once, the funded ones trade alongside)
// • the firm: Lucid-like (EOD drawdown, 50 % consistency) vs EOD with no consistency rule vs a STATIC drawdown with 50 % consistency
// The funded style is the funded study's best: 5 MNQ, +$700 / −$1,000, until +$700, withdraw as soon as allowed.
// The evaluation price ($120 / $60 / $40 with discounts) doesn't change the trading, so the money is worked out for each from one run.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;

public static class SystemStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class Job { public string Eval, Mode, Firm, Year; public List<KeystoneRotDay> Days; }
    public static void Run(string folder, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var all = KeystoneStudioStore.Sessions(folder, "MNQ").Keys.Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday).OrderBy(d => d).ToList();
        var days = KeystoneRotationLab.LoadDays(folder, all.Where(d => d >= new DateTime(2021, 1, 1)));
        var jobs = new List<Job>();
        foreach (var y in new[] { 2021, 2022, 2023, 2024, 2025, 2026 }) foreach (var e in new[] { "TODAY", "BOOST" }) foreach (var m in new[] { "BATCH" }) foreach (var f in new[] { "LUCID", "NOCONS", "STATIC", "STATIC1K" })
            jobs.Add(new Job { Eval = e, Mode = m, Firm = f, Year = y.ToString(IC), Days = days.Where(d => d.Day.Year == y).ToList() });
        var outRows = new string[jobs.Count]; int done = 0;
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var j = jobs[i]; var r = KeystoneSimRules.Presets().First(x => x.Name == "LUCID FLEX 50K").Copy();
            r.Target = 3000; r.EvalDrawdown = 2000; r.EvalDdType = "EOD"; r.EvalConsistency = 50; r.EvalMinDays = 2; r.MaxMicros = 40; r.FlatByHhmm = 1645; r.EvalCost = 120; r.Activation = 0; r.PayoutMin = 0;
            if (j.Firm == "NOCONS") { r.EvalConsistency = 0; r.EvalMinDays = 1; }
            if (j.Firm == "STATIC") { r.EvalDdType = "STATIC"; r.FundDdType = "STATIC"; }
            if (j.Firm == "STATIC1K") { r.EvalDdType = "STATIC"; r.FundDdType = "STATIC"; r.EvalDrawdown = 1000; r.FundDrawdown = 1000; }
            bool boost = j.Eval == "BOOST";
            var s = new KeystoneSimSettings { Strategy = "ROTATION", Accounts = 50, StartAs = "EVAL", Allocation = "ROTATE", ReplacePassed = true, DayPlan = "NONE", Direction = "BUY", Auto = true, AutoFallback = "WAIT", PauseSeconds = 60, TrailStop = false,
                MnqQty = 10, MgcQty = boost ? 0 : 10, RotTarget = 1500, RotStop = boost ? 2000 : 1000, MaxTradesPerDay = 1,
                FundMnqQty = 5, FundMgcQty = 0, FundRotTarget = 700, FundRotStop = 1000, FundMaxTrades = 0, FundDayPlan = "SAME", Rules = r, MinRoomShare = 0.25 };
            if (j.Firm == "NOCONS") s.RotTarget = 3000;   // no consistency rule: one +$3,000 trade can pass in a day
            if (j.Firm == "STATIC1K") { s.RotStop = boost ? 1000 : 500; s.FundRotStop = 500; s.FundRotTarget = 500; }   // $1,000 of room: half the brackets
            if (j.Mode == "BATCH") { s.FundedWhen = "STAGES"; s.BuyRule = "BATCH"; } else { s.FundedWhen = "WITH"; s.BuyRule = "ALWAYS"; }
            var sim = KeystoneRotationLab.Run(s, j.Days, 1800, 1645, null);
            int passed = sim.PassedCount, failed = sim.BlownEval + sim.Accounts.Count(a => a.Phase == "RETIRED" && a.WasPhase == "EVAL");
            int funded = sim.Accounts.Count(a => a.PassedOn != DateTime.MinValue); double cash = sim.Accounts.Sum(a => a.Cash); int bought = sim.Bought; int payouts = sim.Accounts.Sum(a => a.Payouts);
            var cells = new List<string>();
            foreach (var price in new[] { 120.0, 60, 40 })
            {
                // the bank (start $6,000 = 50 evaluations at $120): the lowest point of cash out − fees, and the month it is back to break-even for good
                double worst = 0, cumS = 0, cumC = 0; string be = ""; var curve = new List<string>();
                foreach (var d in sim.Days) { cumS += d.Bought * price; cumC += d.Cash; double net = cumC - cumS; worst = Math.Min(worst, net); curve.Add(Math.Round(net).ToString(IC)); }
                cumS = 0; cumC = 0; for (int k = 0; k < sim.Days.Count; k++) { cumS += sim.Days[k].Bought * price; cumC += sim.Days[k].Cash; if (cumC - cumS >= 0 && be == "" && cumC > 0) be = sim.Days[k].Day.ToString("yyyy-MM-dd", IC); if (cumC - cumS < 0) be = ""; }
                double netAll = cash - bought * price;
                cells.Add("\"p" + price + "\":{\"net\":" + Math.Round(netAll) + ",\"worst\":" + Math.Round(worst) + ",\"breakEven\":\"" + be + "\",\"curve\":[" + string.Join(",", curve) + "]}");
            }
            outRows[i] = "{\"eval\":\"" + j.Eval + "\",\"mode\":\"" + j.Mode + "\",\"firm\":\"" + j.Firm + "\",\"year\":\"" + j.Year + "\",\"days\":" + j.Days.Count + ",\"dayList\":[" + string.Join(",", sim.Days.Select(d => "\"" + d.Day.ToString("MM-dd", IC) + "\"")) + "],\"bought\":" + bought + ",\"passed\":" + passed + ",\"failed\":" + failed + ",\"rate\":" + Math.Round(passed + failed == 0 ? 0 : 100.0 * passed / (passed + failed), 1).ToString(IC)
                + ",\"funded\":" + funded + ",\"paidAccounts\":" + sim.Accounts.Count(a => a.Payouts > 0) + ",\"payouts\":" + payouts + ",\"cash\":" + Math.Round(cash) + ",\"perFunded\":" + Math.Round(funded == 0 ? 0 : cash / funded) + "," + string.Join(",", cells) + "}";
            int n = System.Threading.Interlocked.Increment(ref done); if (n % 12 == 0) Console.WriteLine("  " + n + " / " + jobs.Count);
        });
        File.WriteAllText(Path.Combine(outDir, "system.json"), "{\"rows\":[" + string.Join(",", outRows) + "]}");
        Console.WriteLine("done");
    }
}
