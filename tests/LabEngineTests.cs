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
        // ==== PHASE B (BUILD 2026-11-11b) ====
        // ---- item 32: MFF STUDIO — the static floor, instant withdrawals, the $5,000 total cap; "only at the cap" withdraws exactly once, at $5,000
        {
            var st2 = KeystonePayoutRules.StudioTemplates(); var r = st2.First(x => x.Firm == "MFF002");
            Check(r.Studio && r.StaticDd && Eq(r.FloorOf(0), -1000) && Eq(r.FloorOf(3000), -1000) && Eq(r.TotalCap, 5000) && Eq(r.Split, 80) && r.QualDays == 0 && r.EvalMinDays == 1 && Eq(r.EvalTarget, 2000) && Eq(r.EvalPrice, 280) && r.Size == 20,
                "🎬 32: MFF Studio 002 = $20K, eval +$2,000, STATIC $1,000 (the floor never trails), no minimum days, $280 • funded 80 %, $5,000 total cap");
            var back = KeystonePayoutRules.Load(KeystonePayoutRules.Save(st2)).First(x => x.Firm == "MFF002");
            Check(back.Studio && back.Withdraw == "WIN" && Eq(back.WithdrawMin, 250) && Eq(back.TotalCap, 5000) && back.QualDays == 0 && back.MaxPayouts >= 999, "🎬 the firm book keeps the Studio rules (kind, policy, threshold, cap)");
            var cap = KeystonePayoutPlan.Presets().First(x => x.Name.StartsWith("🎬 MFF MAX $5K")).Copy();
            Func<int, int, double> lon = (i, m) => m < -390 ? 0 : m < 0 ? 0.05 * (m + 390) : Math.Min(350, 19.5 + 0.8 * m);   // up from 3:00 (the London side = BUY), +330 points after 9:30 (MNQ 5 = +$3,300 a day)
            var up = Days(6, lon);
            var run = KeystoneFundedBench.Run(st2, cap, new List<KeystoneEvalAccount> { new KeystoneEvalAccount { Firm = "MFF002", Nick = "M1" } }, up, new KeystoneBench()); var a = run.Accounts[0];
            Check(a.Payouts == 1 && Eq(a.Gross, 5000) && Eq(a.Net, 4000) && a.Stage == "LIVE" && run.Events.Count == 1 && run.Trades.All(t => t.Legs.Contains("5 MNQ")),
                "🎬 32 (the user's test): an MFF funded account with 'only at the cap' withdraws exactly ONCE, at $5,000 (80 % → $4,000), then moves to live", a.Payouts + " payouts · gross " + a.Gross + " · " + a.Stage + " · " + string.Join(" ", run.Trades.Select(t => t.Pnl.ToString("0"))));
            var steady = KeystonePayoutPlan.Presets().First(x => x.Name.StartsWith("🎬 MFF STEADY")).Copy(); var run2 = KeystoneFundedBench.Run(st2, steady, new List<KeystoneEvalAccount> { new KeystoneEvalAccount { Firm = "MFF002", Nick = "M1" } }, Days(25, lon), new KeystoneBench()); var b = run2.Accounts[0];
            Check(b.Payouts == 20 && Eq(b.Gross, 5000) && b.Paid.All(e => Eq(e.Gross, 250)) && b.Stage == "LIVE", "🎬 32: STEADY withdraws every +$250 at once (20 × $250 = the $5,000 cap → live)", b.Payouts + " · " + b.Gross);
            var daily = KeystonePayoutPlan.Presets().First(x => x.Name.StartsWith("🎬 MFF DAILY")).Copy(); var run3 = KeystoneFundedBench.Run(st2, daily, new List<KeystoneEvalAccount> { new KeystoneEvalAccount { Firm = "MFF002", Nick = "M1" } }, Days(1, lon), new KeystoneBench());
            Check(run3.Accounts[0].Payouts == 1 && Eq(run3.Accounts[0].Gross, 2000), "🎬 32c: DAILY +$2,000 a day, withdrawn at the day's end", run3.Accounts[0].Gross.ToString("0"));
            var dn = Days(1, (i, m) => m < 0 ? 0 : -1.0 * m); var ride = KeystonePayoutPlan.Presets().First(x => x.Name.StartsWith("🎬 MFF RIDE")).Copy(); var c = new KeystonePayoutAccount { Id = 1, Name = "M", Firm = "MFF002", FirmNo = 1, Floor = r.FloorOf(0), Bal = 600, PeakEod = 3000 };
            KeystonePayout.Day(c, r, ride, dn[0], 1, null, null, null);
            Check(c.Stage == "BLOWN" && Eq(c.Floor, -1000) && c.Bal <= -999, "🎬 the static floor stays at −$1,000 after gains (a +$3,000 peak does not lift it)", c.Stage + " " + c.Bal.ToString("0"));
            var other = KeystonePayoutRules.For("LUCID", null); Check(!other.Studio && Eq(other.FloorOf(1000), -1000) && Eq(other.FloorOf(3000), 100), "🎬 the other firms keep their EOD trailing floor");
            Check(KeystonePayoutPlan.EvalsFor(0.11, 0.85) == 17 && KeystonePayoutPlan.EvalsFor(0.09, 0.85) == 21 && KeystonePayoutPlan.Presets().First(x => x.Name.StartsWith("🎬 MFF MAX")).Note.Contains("17 evals for an 85 %"), "🎬 32b: N evals for an 85 % chance of at least one $5K (11 % → 17, 9 % → 21)");
            Check(KeystonePayoutPlan.Presets().Count(x => x.Name.StartsWith("🎬 MFF")) == 7 && KeystoneEvalLab.Presets().Count(x => x.Name.StartsWith("🎬 MFF EVAL")) == 2, "🎬 32 / 32b / 32c: 7 funded MFF presets + 2 eval presets");
        }
        // ---- item 32b: the direction rules only read prices known at the entry minute (the MOk guard) — no rule reads a bar after the entry minute
        {
            var rnd = new Random(11); var prev = Days(1, (i, m) => 40 * Math.Sin(m / 37.0))[0]; bool allSame = true; int checkedN = 0; string bad = "";
            foreach (int mode in Enumerable.Range(0, 7)) foreach (int hhmm in new[] { 200, 301, 830, 930, 935, 1000, 1100 })
            {
                if (!KeystoneMffDir.MOk(mode, hhmm)) continue;
                for (int rep = 0; rep < 20; rep++)
                {
                    var d = Days(1, (i, m) => 30 * Math.Sin((m + rep * 13) / 29.0) + rnd.NextDouble())[0]; int k = KeystoneRotDay.Slot(hhmm); int coin = rep % 2 == 0 ? 1 : -1;
                    int before = KeystoneMffDir.Dir(d, prev, k, mode, coin);
                    for (int j = k; j < KeystoneRotDay.Slots; j++) { d.MC[j] = (float)(19000 + 2000 * rnd.NextDouble()); d.MH[j] = d.MC[j] + 5; d.ML[j] = d.MC[j] - 5; if (j > k) d.MO[j] = (float)(19000 + 2000 * rnd.NextDouble()); }
                    int after = KeystoneMffDir.Dir(d, prev, k, mode, coin); checkedN++; if (before != after) { allSame = false; bad = "mode " + mode + " at " + hhmm; }
                }
            }
            Check(allSame && checkedN > 400, "🔎 32b MOk guard: every MFF direction rule gives the same side when every bar after the entry minute is scrambled (" + checkedN + " checks)", bad);
            Check(!KeystoneMffDir.MOk(5, 930) && !KeystoneMffDir.MOk(1, 300) && !KeystoneMffDir.MOk(3, 929) && KeystoneMffDir.MOk(5, 935) && KeystoneMffDir.MOk(1, 930), "🔎 MOk refuses a rule whose base is not before the entry (first 5 min at 9:30, London at 3:00, the gap before 9:30)");
            var d2 = Days(1, (i, m) => m < 0 ? 0 : 1.0 * m)[0]; Check(KeystoneMffDir.Dir(d2, null, KeystoneRotDay.Slot(930), 5, -1) == -1 && KeystoneMffDir.Dir(d2, null, KeystoneRotDay.Slot(935), 5, -1) == 1, "🔎 a refused rule falls back to the coin; 9:35 follows the first 5 minutes (up → BUY)");
        }
        // ---- items 9 / 13 / 15 / 23 / 25 / 26 / 16: ONE run record — every view reads it, the counts agree everywhere, the export is valid JSON
        {
            var days = Days(40, (i, m) => (i % 3 == 0 ? -1 : 1) * 0.25 * Math.Max(0, m + 450));
            var st = new KeystoneSimSettings { Strategy = "ROTATION", Direction = "RANDOM", Auto = true, Allocation = "ROTATE", Accounts = 8, BuyRule = "BATCH", FundedWhen = "ASK", AskEachBatch = true, ReplacePassed = true, DayPlan = "NONE", MaxTradesPerDay = 1,
                From = days[0].Day, To = days[days.Count - 1].Day, DataOnly = true, StartAs = "EVAL", MnqQty = 20, FundMnqQty = 20, RotTarget = 1500, RotStop = 1e6, FundRotTarget = 1500, FundRotStop = 1e6, AllAtOnce = true, Stagger = true, AllSide = "FIRM", StartHhmm = 155, EndHhmm = 1655 };
            st.Rules = KeystoneSimRules.Presets().First().Copy(); st.Rules.Target = 3000; st.Rules.EvalDrawdown = 2000; st.Rules.EvalDdType = "EOD"; st.Rules.EvalConsistency = 50; st.Rules.EvalMinDays = 2;
            var plan = KeystonePayoutPlan.Presets()[0].Copy(); plan.SprintAccounts = 0; plan.Spread = false; plan.Side = "BUY";
            var br = KeystoneBatch.Run(st, days, 155, 1655, null, true, null, plan, (s0, a) => a.Id % 2 == 0 ? "LUCID" : "TRADEIFY", null);
            var res = KeystoneLabResults.Of("BOTH", br.Sim, 8, null, a => a.Id % 2 == 0 ? "LUCID" : "TRADEIFY", br.Pay, br.Start, br.LastDay, br.Sessions); var rec = res.Record;
            Check(rec != null && rec.Accounts.Count == 8 && rec.PassedN == res.Passed && rec.EvalBlownN == res.Blown && rec.PayoutN == res.Payouts && Eq(rec.PaidNet, res.Paid) && res.PayoutRows.Count - 1 == rec.PayoutN && res.PayByPayout.Count - 1 == rec.PayoutN,
                "📒 one record: the header counts (passed incl. TPT pending, blown, payouts, $) = the record = the PAYOUTS rows", rec == null ? "null" : rec.PassedN + "/" + res.Passed + " " + rec.PayoutN + "/" + res.Payouts);
            int evTr = br.Sim.Trades.Count(t => t.Phase != "FUNDED"), fuTr = br.Pay == null ? 0 : br.Pay.Trades.Count;
            Check(rec.Trades.Count == evTr + fuTr && rec.Trades.Count(t => t.Stage == "FUNDED") == fuTr && rec.Trades.Where(t => t.Stage == "FUNDED").All(t => !double.IsNaN(t.EntryPx) && !double.IsNaN(t.ExitPx)) && rec.Days.Count(d => d.Stage == "EVAL") > 0 && (fuTr == 0 || rec.Days.Count(d => d.Stage == "FUNDED") > 0),
                "📒 every trade (eval + funded, with entry / exit prices) and every account-day is in the record", rec.Trades.Count + " vs " + (evTr + fuTr));
            var acc = res.Accounts2; var firms = rec.Accounts.Select(a => a.FirmNice).Distinct().Count();
            Check(acc.Count == 1 + 8 + firms && acc.Skip(1).Take(8).All(row => row.Length == acc[0].Length) && res.FundedFirm.Count >= 2 && res.Eval[0].Contains("$ TO YOU") && res.PerAccount[0].Contains("FUNDED ON"), "👤 item 9 / 23: ACCOUNTS = one row per account + per-firm rows; the EVAL tables carry the funded columns");
            var tl = rec.Timeline(); var tAll = tl.Skip(1).ToList(); var tFirm = rec.Timeline(null, "LUCID");
            Check(tAll.Count == rec.Trades.Count + rec.Events.Count && tFirm.Skip(1).All(x => x[3] == "Lucid") && rec.Timeline(null, null, null, "PAYOUT").Count - 1 == rec.PayoutN, "🕒 item 15: the TIMELINE = every trade + event in time order; the filters (firm, event type) work");
            var withFunded = rec.Accounts.FirstOrDefault(a => a.Funded); if (withFunded != null) { var card = rec.Card(withFunded.Key); Check(card.Trades.Count - 1 == rec.Trades.Count(t => t.Account == withFunded.Key) && card.Cycles.Count >= 2 && card.Path.Count > 0 && card.Lines.Count >= 2, "🗂 item 9: the ACCOUNT CARD = every trade, the eval + each payout cycle, the balance-vs-floor path"); }
            var paid = rec.Payouts.FirstOrDefault(); if (paid != null) { var cy = rec.Cycle(paid.Account, paid.N); Check(cy.Count - 1 >= paid.CycleDays && cy.Skip(1).Count(x => x[3].StartsWith("✓")) >= Math.Min(5, paid.CycleQual) && paid.Rule.Contains("qualifying"), "💵 item 26: a payout opens its cycle (each day, the qualifying days, the rule that allowed it)", cy.Count + " rows · " + paid.CycleDays + " days"); }
            Check(rec.NoPayout().Skip(1).All(x => x[3] != "") && rec.PayoutHeader().Contains("payouts"), "💵 item 26: the accounts with no payout are listed with WHY");
            Check(KeystoneRunRecord.Dist(new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }) == "median 6 · 10th–90th 2–10 (n = 11)" && res.Dist.Count >= 2, "📊 item 13: the distributions (median, 10th–90th) per firm");
            var json = rec.Json(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("shown", "a \"quoted\" line\nwith a break") }, true); var parsed = KeystoneJson.Parse(json) as Dictionary<string, object>;
            Check(parsed != null && ((List<object>)parsed["trades"]).Count == rec.Trades.Count && ((List<object>)parsed["payouts"]).Count == rec.PayoutN && parsed.ContainsKey("account_days") && parsed.ContainsKey("minutes") && ((Dictionary<string, object>)parsed["data"]).ContainsKey("mnq_minutes") && ((Dictionary<string, object>)parsed["summary"])["shown"].ToString().Contains("\"quoted\""),
                "📦 item 25: EXPORT FOR CLAUDE = one valid JSON (data facts, settings, firm rules, every trade / account-day / payout / event, the summary as shown, the price minutes optional)", json.Length + " chars");
            Check(KeystoneRunRecord.ExportName(new DateTime(2026, 11, 8, 9, 5, 7)) == "keystone-check-2026-11-08-090507.json" && res.Trust.Contains("1-minute MNQ / MGC") && res.Trust.Contains("$1 a contract + 1 tick") && res.Trust.Contains("TP on a touch") && KeystoneRunRecord.Verified.Contains("tests/LabEngineTests.cs"), "✔ item 16: the trust line (data, costs, fills) + the verified note pinned to tests/LabEngineTests.cs");
            var cmp = KeystoneRunRecord.Compare(rec, "SAFE", rec, "SAFE again"); Check(cmp.Count >= 10 && cmp.Skip(1).All(x => x[1] == x[2]), "⚖ item 26: COMPARE two records side by side");
            // ---- 🔁 items 11 + 14: the BUSINESS RUN — the same engine, blown accounts replaced, the money causal
            var bs = new KeystoneBusinessSettings { Funding = "POCKET", Fee = 100, Delay = 1 }; bs.PerFirm.Add(Tuple.Create("LUCID", 2)); bs.PerFirm.Add(Tuple.Create("TRADEIFY", 2));
            var biz = KeystoneBusiness.Run(st, bs, days, 155, 1655, null, null, plan);
            Check(biz.Bought > 4 && Eq(biz.Spent, biz.Bought * 100) && biz.Months.Sum(m => m.Bought) == biz.Bought && biz.Record.Accounts.Count == biz.Bought && Eq(biz.Record.PaidNet, biz.PaidNet) && biz.Cash.Count == days.Count,
                "🔁 item 11: OUT OF POCKET — every blown / finished account is replaced (" + biz.Bought + " evals for 4 slots), the months add up, the record holds every account", biz.Summary());
            var b2 = new KeystoneBusinessSettings { Funding = "REINVEST", Budget = 250, Fee = 100, Delay = 1 }; b2.PerFirm.Add(Tuple.Create("LUCID", 3));
            var biz2 = KeystoneBusiness.Run(st, b2, days, 155, 1655, null, null, plan);
            Check(biz2.Cash.All(c => c.Item2 >= -1e-6) && biz2.Months[0].Bought == 2 && biz2.WaitDays > 0, "🔁 REINVEST: never spends money it doesn't have ($250 buys 2 of 3 on day 1, the 3rd slot waits for a payout)", biz2.Summary());
            var b3 = new KeystoneBusinessSettings { Funding = "BATCH", Fee = 100, BatchEvery = 10 }; b3.PerFirm.Add(Tuple.Create("LUCID", 2));
            var biz3 = KeystoneBusiness.Run(st, b3, days, 155, 1655, null, null, plan); Check(biz3.Bought == 2 * ((days.Count - 1) / 10 + 1), "🔁 BATCH: a new batch of the mix every 10 sessions, no replacement", biz3.Bought.ToString());
            // ---- 🔁 BUILD 11d: CONTINUOUS ⚡ CALCULATE — the user's rule "keep running … new eval new funded payouts over and over again until the date we entered"
            {
                var days60 = Days(60, (i, m) => (i % 3 == 0 ? -1 : 1) * 0.25 * Math.Max(0, m + 450));
                var mix = KeystoneLabWizard.Mix(new List<Tuple<string, int>> { Tuple.Create("LUCID", 3), Tuple.Create("TRADEIFY", 3) }); var pool6 = new KeystoneEvalPool { Name = "six", Simulated = true }; pool6.Accounts.AddRange(mix);
                var st6 = st.Copy(); st6.Accounts = 6; st6.Firms = KeystoneEvalLab.FirmsText(pool6, st.Rules, 100); st6.From = days60[0].Day; st6.To = days60[days60.Count - 1].Day;
                var plan2 = plan.Copy(); plan2.SprintAccounts = 2;
                var cs = new KeystoneBusinessSettings { Funding = "POCKET", Fee = 100, Delay = 1, Continuous = true }; cs.PerFirm.Add(Tuple.Create("LUCID", 3)); cs.PerFirm.Add(Tuple.Create("TRADEIFY", 3));
                Func<KeystonePropSim, KeystoneSimAccount, string> firm6 = (s0, a) => (s0.FirmName(a) ?? "").Trim().Split(' ')[0];
                var cont = KeystoneBusiness.Run(st6, cs, days60, 155, 1655, null, null, plan2, null, null, firm6, (s0, a) => firm6(s0, a).ToUpperInvariant() + " " + a.Id);
                var crec = cont.Record; var bySlot = crec.Accounts.GroupBy(a => a.Slot).ToList();
                var blownThenNew = crec.Accounts.Where(a => a.Blown).Any(a => crec.Accounts.Any(y => y.Slot == a.Slot && y.Gen == a.Gen + 1 && y.Bought > a.Ended && y.Firm == a.Firm));
                Check(cont.FirstN == 6 && cont.Bought > 6 && blownThenNew && bySlot.Count == 6 && bySlot.Count(g => g.Count() >= 2) >= 3 && cont.Generations >= 3 && crec.Accounts.All(a => a.Name == a.Slot + " · #" + a.Gen) && bySlot.All(g => g.Select(a => a.Firm).Distinct().Count() == 1),
                    "🔁 11d: a CONTINUOUS run over 60 sessions buys a new eval of the same firm after a blow-up (the session after) — the record holds every generation of every slot (\"LUCID 1 · #3\")", cont.ContinuousSummary() + " · slots " + string.Join(" ", bySlot.Select(g => g.Key + "×" + g.Count())));
                Check(Eq(cont.Spent, cont.Bought * 100) && Eq(crec.Accounts.Sum(a => a.Spent), cont.Spent) && crec.Accounts.Count == cont.Bought && cont.Months.Sum(m => m.Bought) == cont.Bought && Eq(cont.Months.Sum(m => m.Spent), cont.Spent) && Eq(cont.Months.Sum(m => m.PaidNet), crec.PaidNet) && cont.Months.Sum(m => m.Payouts) == crec.PayoutN,
                    "🔁 11d: the totals match — spent = evals × price, the months add up to the run, the record = the money", cont.Bought + " × $100 = " + cont.Spent);
                var cres = KeystoneLabResults.FromContinuous(cont, "BOTH"); var mt = cont.ContinuousMonthTable(false);
                Check(cres.Continuous && cres.Scope == "BOTH" && cres.Months.Count == cont.Months.Count + 2 && mt[0].Contains("CUMULATIVE NET") && mt[0].Contains("FUNDED ALIVE (MONTH END)") && mt[mt.Count - 1][10] == mt[mt.Count - 1][9] && cres.Header.Contains("continuous: " + cont.Bought + " evals bought") && cres.Header.Contains("went funded") && cres.Header.Contains(" net ")
                    && cres.Accounts == cont.Bought && cres.Accounts2.Count > cont.Bought && cres.PerAccount.Count > cont.Bought && cres.Eval[0].Contains("$ PER PASSED") && cres.PayoutRows.Count - 1 == crec.PayoutN && cres.Days.Count > 1 && cres.Stats.Any(x => x[0] == "cost per passed account"),
                    "🔁 11d: RESULTS of a continuous run — the header, MONTH BY MONTH (the cumulative net), per-firm totals, every account of every generation", cres.Header);
                int spr = crec.Accounts.Count(a => a.Sprint);
                Check(KeystoneBusiness.SprintBudget(2, 30, 30) == 2 && KeystoneBusiness.SprintBudget(2, 31, 30) == 4 && KeystoneBusiness.SprintBudget(99, 5, 30) == int.MaxValue && spr <= KeystoneBusiness.SprintBudget(2, cont.Bought, cont.Slots) && (cont.WentFunded == 0 || spr >= 1)
                    && (spr == 0 || (crec.AccountsTable().Any(x => x[0].EndsWith("⚡ SPRINT")) && crec.Card(crec.Accounts.First(a => a.Sprint).Name + " · ⚡ SPRINT").Lines[0].Contains("⚡ SPRINT") && crec.FundedFirmTable().Any(x => x[0].EndsWith("· ⚡ SPRINT")) && (!crec.Accounts.Any(a => a.Funded && !a.Sprint) || crec.FundedFirmTable().Any(x => x[0].EndsWith("· 🛟 SAFE"))))),
                    "⚡ 11d: SPRINT = N per batch-worth of evals bought; the badge on rows + the card, FUNDED RESULTS split ⚡ SPRINT / 🛟 SAFE per firm", spr + " sprint of " + cont.WentFunded + " funded");
                var ce = new KeystoneBusinessSettings { Funding = "POCKET", Fee = 100, Delay = 1, Continuous = true, EvalOnly = true }; ce.PerFirm.AddRange(cs.PerFirm);
                var conte = KeystoneBusiness.Run(st6, ce, days60, 155, 1655, null, null, plan2, null, null, firm6, (s0, a) => firm6(s0, a).ToUpperInvariant() + " " + a.Id); var eres = KeystoneLabResults.FromContinuous(conte, "EVAL");
                Check(conte.Bought > 6 && conte.Record.Accounts.All(a => !a.Funded) && conte.Record.PassedN > 0 && Eq(conte.Spent, conte.Bought * 100) && eres.Scope == "EVAL" && eres.Header.Contains("per passed account") && eres.Months[0].Contains("$ PER PASSED") && eres.Stats.Any(x => x[0] == "pass rate"),
                    "🔁 11d: EVAL ONLY is continuous too — blown (and passed) evals replaced until END, the pass rate + the cost per passed account", conte.ContinuousSummary());
                var one = KeystoneBatch.Run(st6, days60, 155, 1655, null, true, null, plan2, (s0, a) => firm6(s0, a), null); DateTime end60 = days60[days60.Count - 1].Day;
                Check(!one.DaysRanOut && one.LastDay < end60 && (one.Pay == null || one.Pay.Accounts.All(a => !a.Active)) && one.EndedEarly(end60) && one.EndedText(end60).StartsWith("the batch ended ") && one.EndedText(end60).Contains("all 6 accounts finished (") && one.EndedText(end60).Contains("nothing left to trade until " + end60.ToString("ddd d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)),
                    "🔁 11d: \"one batch only\" still stops when nobody is active — and says so (the batch ended …, nothing left to trade until END)", one.EndedText(end60));
                Check(KeystoneFundedChoice.Text().Contains("onebatch=0"), "💾 11d: the one-batch choice is saved with the funded plan choice");
                // 🔬 11d regression (the coordinator's check): a continuous EVAL ONLY run passes at the same rate as one batch started on every session of the same data
                // (no carry-over between generations, no trading on the blow day, no selection effect) — also with the replacement delayed a random 0–4 sessions
                {
                    Func<int, int, double> rnd = (i, k) => (double)(KeystoneLabFlow.Mix64((ulong)(i * 1000 + k + 77)) % 10000) / 10000.0;
                    var dR = Days(160, (i, m) => { double up = rnd(i, 1) < 0.5 ? 1 : -1, sw = 0.12 + 0.3 * rnd(i, 2); int turn = (int)(60 + 300 * rnd(i, 3)); return m < -450 ? 0 : up * sw * (Math.Min(m + 450, turn + 450) - Math.Max(0, m - turn) * (rnd(i, 4) < 0.5 ? 1.6 : 0.4)); });
                    var mixR = KeystoneLabWizard.Mix(new List<Tuple<string, int>> { Tuple.Create("LUCID", 10), Tuple.Create("TRADEIFY", 10) }); var poolR = new KeystoneEvalPool { Name = "r", Simulated = true }; poolR.Accounts.AddRange(mixR);
                    var stR = st.Copy(); stR.Accounts = 20; stR.Firms = KeystoneEvalLab.FirmsText(poolR, st.Rules, 100);
                    int nb = 0, pb = 0; for (int i = 0; i < dR.Count - 30; i++) { var s0 = stR.Copy(); s0.From = dR[i].Day; var b1 = KeystoneBatch.Run(s0, dR.Skip(i).Take(30).ToList(), 155, 1655, null, false, null, plan2, null, null); foreach (var a in b1.Sim.Accounts.Take(20)) { bool ok = b1.Sim.PassedOrPending(a); if (ok || !a.Alive) { nb++; if (ok) pb++; } } }
                    double oneP = 100.0 * pb / Math.Max(1, nb); var res2 = new List<string>(); bool allIn = true;
                    foreach (int jt in new[] { 0, 4 })
                    {
                        var ceR = new KeystoneBusinessSettings { Funding = "POCKET", Fee = 100, Delay = 1, Continuous = true, EvalOnly = true, DelayJitter = jt }; ceR.PerFirm.Add(Tuple.Create("LUCID", 10)); ceR.PerFirm.Add(Tuple.Create("TRADEIFY", 10));
                        var cr = KeystoneBusiness.Run(stR, ceR, dR.Take(dR.Count - 30).ToList(), 155, 1655, null, null, plan2, null, null, firm6, (s0, a) => firm6(s0, a).ToUpperInvariant() + " " + a.Id);
                        int fin = cr.Record.PassedN + cr.Record.EvalBlownN; double cp = 100.0 * cr.Record.PassedN / Math.Max(1, fin); var g2 = cr.Record.Accounts.Where(a => a.Gen > 1 && (a.Passed || a.EvalBlown)).ToList(); double gp = 100.0 * g2.Count(a => a.Passed) / Math.Max(1, g2.Count);
                        res2.Add("jitter " + jt + ": " + cp.ToString("0.0") + " % of " + fin + " (gen 2+ " + gp.ToString("0.0") + " %)"); allIn &= fin > 300 && Math.Abs(cp - oneP) <= 3 && Math.Abs(gp - oneP) <= 3.5;
                    }
                    Check(nb > 2000 && oneP > 10 && oneP < 60 && allIn, "🔬 11d: continuous EVAL ONLY pass % = one batch from every start (±3 points), also with a random 0–4 session replacement delay and for generation 2+"); Console.WriteLine("      🔬 one batch " + oneP.ToString("0.0") + " % of " + nb + " · " + string.Join(" · ", res2));
                }
            }
            Check(biz.MonthTable().Count == biz.Months.Count + 2 && biz.FirmTable().Count == 4 /* 2 firms + ALL (11d) */ && biz.Record.AccountsTable().Count > biz.Bought, "🔁 the month-by-month table, the firm totals and 👤 ACCOUNTS work on a business run");
            // ---- 🏆 item 17: BEST START DAYS — every weekday start through the same engine
            var rows = KeystoneBestStarts.Run(st, days, 155, 1655, null, true, null, plan, days[0].Day, days[9].Day); var br0 = KeystoneBatch.Run(st, days.Take(KeystoneBestStarts.FundedCap).ToList(), 155, 1655, null, true, null, plan, null, null);
            var bres = KeystoneBestStarts.Results(rows, true);
            Check(rows.Count == 10 && rows[0].Passed == br0.Sim.Accounts.Take(8).Count(br0.Sim.PassedOrPending) && Eq(rows[0].ToYou, br0.Pay == null ? 0 : br0.Pay.Net) && bres.Stats.Any(x => x[0] == "most $ to you") && bres.Days.Count == 11 && bres.Header.Contains("hindsight"),
                "🏆 item 17: one row per weekday start = the same numbers as ⚡ CALCULATE from that day; winner cards; the hindsight note");
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
