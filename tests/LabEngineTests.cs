// 🧪💰 THE LABS' ENGINE (BUILD 2026-11-11a, phase A): the hash coin (item 30), the funded spread inside a firm (27a), the costs counted once (27b),
// TPT phase 1 like the research (27c), the one-batch run model (8 / 31 / 5), ⚡ SPRINT (1), the goal, the saved plan choice (27e) and a pinned
// regression on the real saved minutes when data/ is present (27f).
// Run: tools/compile_engine.sh tests/.build/labeng.exe tests/LabEngineTests.cs && mono tests/.build/labeng.exe
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class LabEngineTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b, double tol = 0.01) { return Math.Abs(a - b) < tol; }
    static bool In(double v, double lo, double hi) { return v >= lo && v <= hi; }

    // n weekdays from 2026-03-02, MNQ on the path (minutes after 9:30 NY → points), flat before; MGC flat
    static List<KeystoneRotDay> Days(int n, Func<int, int, double> path)
    {
        var L = new List<KeystoneRotDay>(); var d0 = new DateTime(2026, 3, 2); int k0 = KeystoneRotDay.Slot(930);
        for (int i = 0; i < n; i++, d0 = d0.AddDays(d0.DayOfWeek == DayOfWeek.Friday ? 3 : 1))
        {
            var d = new KeystoneRotDay { Day = d0 }; double last = 20000;
            for (int k = 0; k < KeystoneRotDay.Slots; k++) { double c = 20000 + path(i, k - k0); d.MO[k] = (float)last; d.MC[k] = (float)c; d.MH[k] = (float)Math.Max(last, c); d.ML[k] = (float)Math.Min(last, c); d.GO[k] = d.GC[k] = d.GH[k] = d.GL[k] = 4000; last = c; }
            L.Add(d);
        }
        return L;
    }

    public static int Main(string[] args)
    {
        // ---- item 30: the side coin — firms, slots and days independent, ≈ half BUY
        {
            int n = 0, f01 = 0, f02 = 0, f12 = 0, s01 = 0, s24 = 0, buy = 0, d01 = 0;
            for (var d = new DateTime(2020, 7, 1); d <= new DateTime(2026, 9, 30); d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                for (int s = 0; s < 5; s++)
                {
                    int a = KeystoneLabFlow.SideOf(d, "F0", s), b = KeystoneLabFlow.SideOf(d, "F1", s), c = KeystoneLabFlow.SideOf(d, "F2", s); n++;
                    if (a == b) f01++; if (a == c) f02++; if (b == c) f12++; if (a > 0) buy++;
                    if (s == 0 && a == KeystoneLabFlow.SideOf(d, "F0", 1)) s01++; if (s == 0 && KeystoneLabFlow.SideOf(d, "F0", 2) == KeystoneLabFlow.SideOf(d, "F0", 4)) s24++;
                    if (a == KeystoneLabFlow.SideOf(d.AddDays(1), "F0", s)) d01++;
                }
            }
            double pf01 = 100.0 * f01 / n, pf02 = 100.0 * f02 / n, pf12 = 100.0 * f12 / n, ps01 = 100.0 * s01 / (n / 5), ps24 = 100.0 * s24 / (n / 5), pb = 100.0 * buy / n, pd = 100.0 * d01 / n;
            Check(In(pf01, 45, 55) && In(pf02, 45, 55) && In(pf12, 45, 55), "🎲 item 30: over 6 years firm-vs-firm agreement 45–55 % (was 89.9 % / 79.7 % with new Random(seed))", pf01.ToString("0.0") + " / " + pf02.ToString("0.0") + " / " + pf12.ToString("0.0"));
            Check(In(ps01, 45, 55) && In(ps24, 45, 55), "🎲 slot-vs-slot agreement 45–55 % (was 30 %)", ps01.ToString("0.0") + " / " + ps24.ToString("0.0"));
            Check(In(pb, 45, 55) && In(pd, 45, 55), "🎲 BUY share 45–55 %, day-to-day 45–55 %", pb.ToString("0.0") + " / " + pd.ToString("0.0"));
            Check(KeystoneLabFlow.SideOf(new DateTime(2026, 3, 3), "F1", 2, 0) == KeystoneLabFlow.SideOf(new DateTime(2026, 3, 3), "f1", 2, 0) && KeystoneLabFlow.PlanSide("FIRM", new DateTime(2026, 3, 3), "F1", 7, 2, 0) == KeystoneLabFlow.SideOf(new DateTime(2026, 3, 3), "F1", 2, 0),
                "🎲 deterministic, case-blind, ⚡ CALCULATE and the replay ask the same PlanSide");
            int sameP = 0, nP = 0; for (var d = new DateTime(2024, 1, 1); d < new DateTime(2026, 1, 1); d = d.AddDays(1)) { nP++; if (KeystonePayout.Coin(d, "LUCID", 1) == KeystonePayout.Coin(d, "TRADEIFY", 1)) sameP++; }
            Check(In(100.0 * sameP / nP, 45, 55), "🎲 the funded coin (KeystonePayout.Coin): Lucid vs Tradeify independent", (100.0 * sameP / nP).ToString("0.0"));
            var seeds = Enumerable.Range(0, 400).Select(i => new Random(KeystoneLabFlow.SeedOf(7, i)).Next(2)).ToList(); double sp = 100.0 * Enumerable.Range(1, 399).Count(i => seeds[i] == seeds[i - 1]) / 399;
            Check(In(sp, 40, 60), "🎲 per-day System.Random seeds are mixed (adjacent days' first draws independent)", sp.ToString("0.0"));
        }
        // ---- item 27b: the costs counted once — $1 a contract round trip + 1 tick each side
        {
            Check(Eq(KeystoneRotationLab.CostMnq, 1.5) && Eq(KeystoneRotationLab.CostMgc, 2.0) && Eq(KeystonePayout.Slip(2, 0), 0), "💲 27b: MNQ $1 + the exit tick ($0.50) = $1.50 a contract, MGC $2.00; the entry tick is in the fill (no third tick)");
            var up = Days(1, (i, m) => m < 0 ? 0 : 0.5 * m); var dn = Days(1, (i, m) => m < 0 ? 0 : -0.5 * m); int k = KeystoneRotDay.Slot(930), end = KeystoneFunded.EndSlot(up[0]), how, xs; double pk, wr;
            double fees = 10 * KeystoneRotationLab.CostMnq;
            double vT = KeystoneRotationLab.Trade(up[0], k, end, 1, 0, 10, 0, 300 + fees, 200, 0, null, out how, out xs, out pk, out wr, false, 1);
            Check(how == 1 && Eq(vT, 300), "💲 a target keeps exactly the +$300 after costs", how + " " + vT);
            double vS = KeystoneRotationLab.Trade(dn[0], k, end, 1, 0, 10, 0, 300 + fees, 200, 0, null, out how, out xs, out pk, out wr, false, 1);
            Check(how == -1 && Eq(vS, -200 - fees), "💲 a stop loses the stop + $1.50 a contract (no extra slippage tick on top)", how + " " + vS);
        }
        // ---- item 27a: same-firm funded accounts get different time slots, one side per firm per time
        {
            var plan = KeystonePayoutPlan.Presets()[0].Copy(); plan.SprintAccounts = 0; var src = new List<KeystoneEvalAccount>();
            for (int i = 1; i <= 5; i++) src.Add(new KeystoneEvalAccount { Firm = "LUCID", Nick = "L" + i }); for (int i = 1; i <= 5; i++) src.Add(new KeystoneEvalAccount { Firm = "TRADEIFY", Nick = "T" + i });
            var days = Days(6, (i, m) => 3 * Math.Sin((m + 600 + 37 * i) / 23.0));
            var run = KeystoneFundedBench.Run(null, plan, src, days, new KeystoneBench());
            var luc = run.Accounts.Where(a => a.Firm == "LUCID").ToList(); var times = luc.Select(a => plan.TimeOf(a)).ToList();
            var tr0 = run.Trades.Where(t => t.Day == days[0].Day).ToList(); Func<int, string> firm = id => run.Accounts.First(a => a.Id == id).Firm;
            bool distinct = luc.Select(a => a.FirmNo).Distinct().Count() == 5 && times.Distinct().Count() == 5 && tr0.Where(t => firm(t.Account) == "LUCID").Select(t => t.Entry.TimeOfDay).Distinct().Count() == tr0.Count(t => firm(t.Account) == "LUCID");
            bool tradeify = run.Accounts.Where(a => a.Firm == "TRADEIFY").Select(a => plan.TimeOf(a)).Distinct().Count() == 5 && run.Accounts.First(a => a.Name == "T1").FirmNo == 1;
            bool oneSide = run.Trades.GroupBy(t => t.Day.Ticks + "|" + firm(t.Account) + "|" + t.Entry.TimeOfDay).All(g => g.Select(t => t.Dir).Distinct().Count() == 1);
            var sides = new HashSet<string>(); foreach (var d in days) sides.Add(string.Join("", luc.Select(a => KeystonePayout.CoinOf(plan, d.Day, a, 1) > 0 ? "B" : "S")));
            Check(distinct && tradeify && oneSide && sides.Count > 1, "💰 27a: 5 Lucid funded → 5 numbers within the firm → 5 entry times (Tradeify 1 = time 1 again), one side per firm per time, the sides differ between times",
                string.Join(",", times) + " sides " + string.Join(" ", sides));
            var ln = luc.Select(a => KeystonePayout.CoinOf(plan, days[0].Day, a, 1)).ToList(); var same = new List<int>(); foreach (var d in Enumerable.Range(0, 300).Select(i => new DateTime(2025, 1, 1).AddDays(i))) same.Add(KeystonePayout.CoinOf(plan, d, luc[0], 1) == KeystonePayout.CoinOf(plan, d, luc[1], 1) ? 1 : 0);
            Check(In(100.0 * same.Sum() / same.Count, 40, 60), "💰 27a: Lucid 1 and Lucid 2 (two times) are not copies (≈ half the days the same side)", (100.0 * same.Sum() / same.Count).ToString("0.0"));
        }
        // ---- item 27c: TPT phase 1 in SAFE = MNQ 2 +$800 / −$300 a day until the best equity reaches +$2,000 (the floor locks), then withdraw $1,000 at +$3,000
        {
            var safe = KeystonePayoutPlan.Presets()[0].Copy(); var r = KeystonePayoutRules.For("TPT", null);
            var days = Days(8, (i, m) => m < 0 ? 0 : Math.Min(250, 1.0 * m));   // every day up +250 points from 9:30 (MNQ 2 → +$1,000 > +$800)
            var a = new KeystonePayoutAccount { Id = 1, Name = "TPT 1", Firm = "TPT", FirmNo = 1, Floor = r.FloorOf(0) }; var log = new List<KeystoneSimTrade>(); var ev = new List<KeystonePayoutEvent>();
            var dayPnl = new List<double>(); foreach (var d in days) { if (!a.Active) break; dayPnl.Add(KeystonePayout.Day(a, r, safe, d, 1, null, log, ev)); }
            bool p1 = !safe.TptOneShot && log.Count > 0 && log[0].Legs.Contains("2 MNQ") && Eq(dayPnl[0], 800) && Eq(dayPnl[1], 800);
            Check(p1 && a.Locked && ev.Count >= 1 && Eq(ev[0].Gross, 1000) && Eq(ev[0].Net, 800), "🔒 27c: TPT SAFE phase 1 = MNQ 2 +$800 a day (not one ride to +$2,250); locked at +$2,000, a $1,000 withdrawal at +$3,000 (80 % → $800)",
                string.Join(" ", dayPnl.Select(v => v.ToString("0"))) + " locked " + a.Locked + " payouts " + string.Join(",", ev.Select(e => e.Gross.ToString("0"))));
            var one = KeystonePayoutPlan.Presets().First(x => x.Name.StartsWith("🎯 TPT")).Copy(); var b = new KeystonePayoutAccount { Id = 1, Name = "TPT 1", Firm = "TPT", FirmNo = 1, Floor = r.FloorOf(0) }; var lg2 = new List<KeystoneSimTrade>();
            double v1 = KeystonePayout.Day(b, r, one, Days(1, (i, m) => m < 0 ? 0 : 1.0 * m)[0], 1, null, lg2, null);
            Check(one.TptOneShot && lg2.Count == 1 && lg2[0].Legs.Contains("15 MNQ") && Eq(v1, 2250) && b.Locked, "🔒 the one-shot lock stays an option (🎯 TPT +$10K: MNQ 15 ride to +$2,250 → locked)", v1.ToString("0") + " " + b.Locked);
            var dn = Days(1, (i, m) => m < 0 ? 0 : -1.0 * m)[0]; var c = new KeystoneFundedAccount(); var fa = new KeystonePayoutAccount { Id = 1, Name = "TPT 2", Firm = "TPT", FirmNo = 1, Floor = r.FloorOf(0) };
            double vL = KeystonePayout.Day(fa, r, safe, dn, 1, null, null, null);
            Check(Eq(vL, -300 - 2 * KeystoneRotationLab.CostMnq) && fa.Active, "🔒 TPT SAFE phase 1 loses the −$300 stop + costs (the stop on the minute's worst, not the close)", vL.ToString("0.00"));
            double peak = 0; int how, xs; var spike = Days(1, (i, m) => m < 0 ? 0 : m == 3 ? 1200 : m > 3 ? -1200 : 0)[0];
            double vf = KeystonePayout.TrailTrade(spike, KeystoneRotDay.Slot(930), KeystoneFunded.EndSlot(spike), 1, 2, 5000, 0, 0, ref peak, 2000, 0, out how, out xs);
            Check(how == -4 && Eq(vf, Math.Min(peak - 2000, 0)) && peak > 2000, "🔒 the intraday trailing floor follows the open profit (+$2,400 open → the floor at the start) and closes there", how + " " + vf.ToString("0") + " peak " + peak.ToString("0"));
        }
        // ---- item 1: ⚡ SPRINT — the first 5 funded days MNQ 10, no stop, TP = what the max payout needs ÷ the days left (800–2,500); max payout or → SAFE
        {
            var plan = KeystonePayoutPlan.Presets()[0].Copy(); plan.SprintAccounts = 1; plan.Spread = false; plan.Side = "BUY"; var src = new List<KeystoneEvalAccount> { new KeystoneEvalAccount { Firm = "LUCID", Nick = "L1" }, new KeystoneEvalAccount { Firm = "LUCID", Nick = "L2" } };
            var days = Days(12, (i, m) => m < -480 ? 0 : 1.0 * (m + 480));   // up all day: every trade reaches its target
            var run = KeystoneFundedBench.Run(null, plan, src, days, new KeystoneBench()); var s1 = run.Accounts.First(a => a.Name == "L1"); var s2 = run.Accounts.First(a => a.Name == "L2");
            var t1 = run.Trades.Where(t => t.Account == s1.Id).ToList();
            bool sprintSize = t1.Count >= 5 && t1.Take(4).All(t => t.Legs.Contains("10 MNQ") && t.Source.Contains("SPRINT")) && Eq(t1[0].Pnl, 800);   // Lucid max $2,000 = 50 % of $4,000 → $4,000 ÷ 5 = $800 a day
            bool maxPaid = s1.Paid.Count >= 1 && Eq(s1.Paid[0].Gross, 2000) && run.Trades.Count(t => t.Account == s1.Id && t.Day <= s1.Paid[0].Day) == 5;
            bool safeAfter = t1.Count > 5 && t1.Skip(5).All(t => t.Legs.Contains("1 MNQ") && !t.Source.Contains("SPRINT"));
            Check(s1.Sprint && !s2.Sprint && sprintSize && maxPaid && safeAfter && run.Trades.Where(t => t.Account == s2.Id).All(t => t.Legs.Contains("1 MNQ")),
                "⚡ SPRINT (1 account): MNQ 10, TP $4,000 ÷ 5 = +$800 a day, the MAX payout ($2,000) on the 5th qualifying day, then SAFE (MNQ 1) • the other account SAFE from day 1",
                string.Join(" | ", t1.Take(7).Select(t => t.Legs + " " + t.Pnl.ToString("0"))) + " paid " + string.Join(",", s1.Paid.Select(p => p.Gross.ToString("0"))));
            var big = KeystonePayoutPlan.Presets()[0].Copy(); big.SprintAccounts = 99; big.Spread = false; big.Side = "BUY"; var tr = KeystoneFundedBench.Run(null, big, new List<KeystoneEvalAccount> { new KeystoneEvalAccount { Firm = "TRADEIFY", Nick = "T1" }, new KeystoneEvalAccount { Firm = "TPT", Nick = "P1" } }, Days(1, (i, m) => m < -480 ? 0 : 1.0 * (m + 480)), new KeystoneBench());
            Check(tr.Accounts.First(a => a.Firm == "TRADEIFY").Sprint && !tr.Accounts.First(a => a.Firm == "TPT").Sprint && Eq(tr.Trades.First(t => t.Account == tr.Accounts.First(a => a.Firm == "TRADEIFY").Id).Pnl, 1000),
                "⚡ SPRINT ALL: Tradeify ($2,500 max = $5,000 profit → +$1,000 a day); TPT keeps its own plan");
            Check(KeystonePayoutPlan.Presets()[0].SprintAccounts == 2 && KeystonePayoutPlan.SprintChoices.SequenceEqual(new[] { 0, 1, 2, 3, 99 }), "⚡ the default: 2 sprint accounts per batch (0 / 1 / 2 / 3 / all)");
        }
        // ---- item 8: the goal — an account stops at its goal (the first payout)
        {
            var plan = KeystonePayoutPlan.Presets()[0].Copy(); plan.SprintAccounts = 0; plan.Goal = 1; plan.Spread = false; plan.Side = "BUY";
            var run = KeystoneFundedBench.Run(null, plan, new List<KeystoneEvalAccount> { new KeystoneEvalAccount { Firm = "LUCID", Nick = "L1" } }, Days(30, (i, m) => m < -480 ? 0 : 1.0 * (m + 480)), new KeystoneBench());
            var a = run.Accounts[0]; Check(a.Stage == "GOAL" && a.Payouts == 1 && run.Trades.All(t => t.Day <= a.Ended), "🏁 goal = the first payout: the account stops the day it is paid", a.Stage + " " + a.Payouts);
            Check(KeystonePayoutPlan.GoalName(0).Contains("all") && KeystonePayoutPlan.GoalName(1) == "the first payout" && KeystonePayoutPlan.GoalName(3) == "3 payouts", "🏁 goal names");
        }
        // ---- items 8 / 31 / 5: ONE batch — the eval runs until each account passed or blew, each passed account goes funded on its own
        {
            var days = Days(60, (i, m) => (i % 2 == 0 ? 1 : -1) * 0.2 * Math.Max(0, m + 450));
            var st = new KeystoneSimSettings { Strategy = "ROTATION", Direction = "RANDOM", Auto = true, Allocation = "ROTATE", Accounts = 6, AltInstrument = false, BuyRule = "BATCH", FundedWhen = "ASK", AskEachBatch = true, ReplacePassed = true, DayPlan = "NONE", MaxTradesPerDay = 1,
                From = days[0].Day, To = days[days.Count - 1].Day, DataOnly = true, StartAs = "EVAL", MnqQty = 20, MgcQty = 0, FundMnqQty = 20, RotTarget = 1500, RotStop = 1e6, FundRotTarget = 1500, FundRotStop = 1e6, AllAtOnce = true, Stagger = true, AllSide = "FIRM", StartHhmm = 155, EndHhmm = 1655 };
            st.Rules = KeystoneSimRules.Presets().First().Copy(); st.Rules.Target = 3000; st.Rules.EvalDrawdown = 2000; st.Rules.EvalDdType = "EOD"; st.Rules.EvalConsistency = 50; st.Rules.EvalMinDays = 2;
            var plan = KeystonePayoutPlan.Presets()[0].Copy(); plan.SprintAccounts = 0;
            var br = KeystoneBatch.Run(st, days, 155, 1655, null, true, null, plan, (s0, a) => "LUCID", null);
            bool evalDone = KeystoneBatch.EvalDone(br.Sim, 6); int fin = br.Sim.Accounts.Take(6).Count(a => !a.Alive || br.Sim.PassedOrPending(a));
            Check(br.Bought == 6 && evalDone && fin == 6 && br.EvalSessions >= 2 && br.EvalSessions < 60 && br.Header.StartsWith("one batch of 6 bought on Mon 2 Mar 2026; blown accounts are not replaced"),
                "🔁 item 8: the eval runs until all 6 passed or blew (" + br.EvalSessions + " sessions), then stops; the header says one batch, no replacement", br.EvalSessions + " " + fin + " " + br.Header);
            bool ownStart = br.Pay == null || br.Passed.All(p => br.Pay.Trades.Where(t => br.Pay.Accounts.First(x => x.Id == t.Account).Name == p.Nick).All(t => t.Day >= p.StartOn));
            Check(br.Passed.Count == br.Sim.Accounts.Take(6).Count(br.Sim.PassedOrPending) && ownStart && br.LastDay >= br.EvalEnd, "🔁 every passed account goes funded on its own date (the session after its pass) and runs on", br.Passed.Count + " passed");
            if (br.Pay != null && br.Pay.Trades.Count > 0)
            {
                var res = KeystoneLabResults.Of("BOTH", br.Sim, 6, null, (a) => "LUCID", br.Pay, br.Start, br.LastDay, br.Sessions);
                var dates = res.Days.Skip(1).Select(x => x[0].Substring(4)).ToList();
                Check(res.Days.Skip(1).Any(x => x[1].EndsWith("· funded")) && res.Days.Skip(1).Any(x => !x[1].EndsWith("· funded")) && dates.SequenceEqual(dates.OrderBy(x => x, StringComparer.Ordinal)), "📅 item 8: the DAYS tab shows the eval days and the funded days on one timeline");
            }
            var shortDays = days.Take(2).ToList(); var br2 = KeystoneBatch.Run(st, shortDays, 155, 1655, null, true, null, plan, (s0, a) => "LUCID", null);
            Check((br2.StillEval > 0 || br2.StillFunded > 0) && br2.DaysRanOut && br2.StillNote.Contains("a payout needs ≥ 5 funded trading days"), "⏳ item 5: the days end with accounts still running → 'still running · the days end …'", br2.StillNote);
        }
        // ---- item 27e: the funded plan choice saved between sessions
        {
            string f = Path.Combine(Path.GetTempPath(), "ks-fplan-" + Guid.NewGuid().ToString("N") + ".txt");
            KeystoneFundedChoice.PlanName = KeystonePayoutPlan.Presets()[3].Name; KeystoneFundedChoice.Sprint = 3; KeystoneFundedChoice.Goal = 1; KeystoneFundedChoice.Save(f);
            KeystoneFundedChoice.PlanName = ""; KeystoneFundedChoice.Sprint = 2; KeystoneFundedChoice.Goal = 0; KeystoneFundedChoice.Load(f);
            var ap = KeystoneFundedChoice.Apply(KeystonePayoutPlan.Presets()[KeystoneFundedChoice.IndexIn(KeystonePayoutPlan.Presets())]);
            Check(KeystoneFundedChoice.IndexIn(KeystonePayoutPlan.Presets()) == 3 && ap.SprintAccounts == 3 && ap.Goal == 1, "💾 27e: the funded plan + sprint + goal come back after a restart", KeystoneFundedChoice.Text());
            try { File.Delete(f); } catch { } KeystoneFundedChoice.PlanName = ""; KeystoneFundedChoice.Sprint = 2; KeystoneFundedChoice.Goal = 0;
        }
        // ---- item 27f: the pinned regression on the real saved minutes (data/MNQ + data/MGC, when present): Lucid SAFE 9:30 London side, one account
        // from every 2nd session 2020-07-06 → 2026-08-31, 150 sessions max → paid at least once within ±3 points of 68.8 % (the cross-check)
        {
            string root = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "data"); string tmp = null;
            try
            {
                if (Directory.Exists(Path.Combine(root, "MNQ")) && Directory.Exists(Path.Combine(root, "MGC")))
                {
                    tmp = Path.Combine(Path.GetTempPath(), "ks-data-" + Guid.NewGuid().ToString("N"));
                    foreach (var sym in new[] { "MNQ", "MGC" }) { Directory.CreateDirectory(Path.Combine(tmp, sym)); foreach (var gz in Directory.GetFiles(Path.Combine(root, sym), "*.bars.gz")) using (var i = File.OpenRead(gz)) using (var z = new GZipStream(i, CompressionMode.Decompress)) using (var o = File.Create(Path.Combine(tmp, sym, Path.GetFileNameWithoutExtension(gz)))) z.CopyTo(o); }
                    var all = new List<KeystoneRotDay>(); for (int y = 2020; y <= 2026; y++) all.AddRange(KeystoneRotationLab.LoadDays(tmp, KeystonePropSim.Weekdays(new DateTime(y, 1, 1), new DateTime(y, 12, 31))));
                    int n = 0, paid = 0, cnt = 0;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var d0 = all[i].Day; if (d0 < new DateTime(2020, 7, 6) || d0 > new DateTime(2026, 8, 31)) continue; if (cnt++ % 2 != 0) continue;
                        var lon = KeystonePayoutPlan.Presets()[0].Copy(); lon.Spread = false; lon.Side = "LONDON"; lon.StartHhmm = 930; lon.SprintAccounts = 0;
                        var run = KeystoneFundedBench.Run(KeystonePayoutRules.Presets(), lon, new List<KeystoneEvalAccount> { new KeystoneEvalAccount { Firm = "LUCID", Nick = "L1", StartOn = d0 } }, all.Skip(i).Take(150).ToList(), new KeystoneBench());
                        n++; if (run.Accounts[0].Payouts > 0) paid++;
                    }
                    double pct = n == 0 ? 0 : 100.0 * paid / n;
                    Check(all.Count > 1000 && n > 600 && In(pct, 65.8, 71.8), "📌 27f pinned: lab Lucid SAFE 9:30 London 2020–26 paid ≥ 1 within ±3 points of 68.8 % (" + n + " starts on the real minutes)", pct.ToString("0.0") + " %");
                }
                else Console.WriteLine("SKIP  📌 27f pinned regression: no data/MNQ + data/MGC here (the synthetic checks above still run)");
            }
            finally { if (tmp != null) try { Directory.Delete(tmp, true); } catch { } }
        }
        Console.WriteLine(failures == 0 ? "ALL LAB ENGINE TESTS PASSED" : "LAB ENGINE TESTS: " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
