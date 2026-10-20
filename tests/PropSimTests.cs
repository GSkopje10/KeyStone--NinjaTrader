// PROP SIMULATION (Backtest Studio): accounts in rotation, EVAL → FUNDED → payouts, blown on the floor, replacement buys,
// the rotation driver (target after the hold time, stop, breakeven, lock tiers), saving / loading a run, added instruments.
// Run: tools/compile_engine.sh tests/.build/propsim.exe tests/PropSimTests.cs && mono tests/.build/propsim.exe
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class PropSimTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 0.01; }
    static KeystoneSimTrade T(DateTime d, int secs, double pnl) { var e = d.AddHours(10); return new KeystoneSimTrade { Entry = e, Exit = e.AddSeconds(secs), Pnl = pnl, Dir = 1, Reason = pnl > 0 ? "TARGET" : "STOP", Source = "TEST" }; }
    // one trade on the active account: fill + end of trade
    static KeystoneSimAccount Trade(KeystonePropSim sim, int secs, double pnl)
    {
        var a = sim.Active; if (a == null) return null; sim.AddFill(a, pnl); sim.EndTrade(a, T(sim.Day, secs, pnl)); return a;
    }

    public static int Main()
    {
        // ---- instruments: MNQ / MGC keep their built-in values; an added one uses NinjaTrader's
        Check(Eq(KeystoneMoveStudy.PointValue("MNQ"), 2) && Eq(KeystoneMoveStudy.PointValue("MGC"), 10) && Eq(KeystoneMoveStudy.TickSize("MNQ"), 0.25) && Eq(KeystoneMoveStudy.TickSize("MGC"), 0.1) && Eq(KeystoneMoveStudy.Cost("MNQ"), 1.74) && Eq(KeystoneMoveStudy.Cost("MGC"), 2.24), "MNQ / MGC point value, tick and cost unchanged");
        Check(!KeystoneSymbols.Register(new KeystoneSymbolSpec { Symbol = "MNQ", PointValue = 99, TickSize = 1 }) && Eq(KeystoneMoveStudy.PointValue("MNQ"), 2), "MNQ cannot be overridden by an added instrument");
        KeystoneSymbols.Register(new KeystoneSymbolSpec { Symbol = "es", Name = "E-mini S&P 500", Type = "FUTURE", PointValue = 50, TickSize = 0.25, Commission = 2.25 });
        Check(Eq(KeystoneMoveStudy.PointValue("ES"), 50) && Eq(KeystoneMoveStudy.TickSize("ES"), 0.25) && Eq(KeystoneMoveStudy.Cost("ES"), 2 * 2.25 + 0.25 * 50), "added ES: $50 a point, tick 0.25, cost = commission both sides + 1 tick", KeystoneMoveStudy.Cost("ES").ToString());
        var eu = new KeystoneSymbolSpec { Symbol = "EURUSD", Name = "Euro / US dollar", Type = "FOREX", PointValue = 100000, TickSize = 0.00001, Commission = 0 };
        var round = KeystoneSymbols.Parse(KeystoneSymbols.Serialize(new[] { eu, KeystoneSymbols.Get("ES") }));
        Check(round.Count == 2 && round[0].Symbol == "EURUSD" && Eq(round[0].PointValue, 100000) && Math.Abs(round[0].TickSize - 0.00001) < 1e-12 && round[1].Type == "FUTURE", "instrument list saved and read back");
        var rt = new KeystoneReplayTrader(new List<KeystoneArcBar> { new KeystoneArcBar { Symbol = "ES", Time = new DateTime(2026, 1, 5, 9, 31, 0), Open = 6000, High = 6001, Low = 5999, Close = 6000.5 } }, "ES", 60);
        rt.Advance(); rt.Buy(1); rt.Price = rt.AvgPrice + 2; rt.CloseAll("TEST");
        Check(Eq(rt.Fills[0].Net, 2 * 50 - KeystoneMoveStudy.Cost("ES")) && Eq(rt.Tick, 0.25), "the replay trader trades ES at $50 a point", rt.Fills[0].Net.ToString());
        Check(KeystoneSymbols.TypeName("Future") == "FUTURE" && KeystoneSymbols.TypeName("Forex") == "FOREX" && KeystoneSymbols.TypeName("Stock") == "STOCK", "NinjaTrader instrument types → ours");

        // ---- the run: 2 evaluations, Lucid-like rules (target 3,000, drawdown 2,000 EOD locking at +100, 50% consistency, 2 days)
        var s = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", BuyRule = "ALWAYS", Accounts = 2, Strategy = "MANUAL", RotateBy = "TRADE", MaxLossesPerDay = 0 };
        s.Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy(); s.Rules.MinHoldSeconds = 11; s.Rules.MinHoldShare = 50;
        var sim = new KeystonePropSim(s);
        var d1 = new DateTime(2026, 3, 2); sim.BeginDay(d1);
        Check(sim.Accounts.Count == 2 && sim.Accounts.All(a => a.Phase == "EVAL") && Eq(sim.Spent, 240) && sim.Active.Name == "A01", "day 1: two evaluations bought ($240), A01 trades first");
        Trade(sim, 30, 1600);   // A01 +1,600 → next account
        Check(sim.Active.Name == "A02", "after a trade the next account trades (rotation)");
        Trade(sim, 30, -500);   // A02 −500
        Check(sim.Active.Name == "A01", "round robin back to A01");
        var day1 = sim.EndDay();
        var a1 = sim.Accounts[0]; var a2 = sim.Accounts[1];
        Check(Eq(a1.Bal, 1600) && Eq(a1.PeakEod, 1600) && Eq(a2.Bal, -500) && day1.Trades == 2 && day1.Wins == 1, "end of day 1: balances, EOD peaks, day row", a1.Bal + " " + a2.Bal);
        Check(Eq(sim.Floor(a1), -400) && Eq(sim.Floor(a2), -2000), "EOD trailing floors: A01 1,600 − 2,000 = −400, A02 −2,000");

        // day 2: A01 +1,500 → 3,100 ≥ target, 2 days, best day 1,600 = 52% of 3,100 > 50% → NOT passed (consistency)
        sim.BeginDay(d1.AddDays(1)); Trade(sim, 30, 1500); Trade(sim, 30, 100); sim.EndDay();
        Check(a1.Phase == "EVAL" && Eq(a1.Bal, 3100), "A01 at the target but the best day is 52% of the profit → not passed yet (consistency)", a1.Phase + " " + a1.Bal);
        // day 3: A01 +300 → 3,400, best 1,600 = 47% → PASSED → FUNDED, a new evaluation replaces it (A03)
        sim.BeginDay(d1.AddDays(2));
        Check(sim.Accounts.Count == 2, "no purchase while both evaluations live");
        Trade(sim, 30, 300); sim.EndDay();
        Check(a1.Phase == "FUNDED" && a1.PassedOn == d1.AddDays(2) && Eq(a1.Bal, 0) && sim.PassedCount == 1, "A01 passed (3 days, best day 47%) → FUNDED, balance restarts at 0", a1.Phase + " " + a1.Bal);
        sim.BeginDay(d1.AddDays(3));
        Check(sim.Accounts.Count == 3 && sim.Accounts[2].Phase == "EVAL" && Eq(sim.Spent, 360), "the passed evaluation is replaced (A03 bought)");

        // the hold-time rule: profit from trades held < 11 s does not count toward the 50% share
        // 📋 COPY TRADING: every account that may trade takes the same trade (scaled by its contracts); blown / locked ones drop out
        var cpS = s.Copy(); cpS.Accounts = 3; cpS.Allocation = "COPY"; cpS.MnqQty = 2; cpS.MgcQty = 0; cpS.FundMnqQty = 1; cpS.FundMgcQty = 0; var cpSim = new KeystonePropSim(cpS); cpSim.BeginDay(d1);
        var cpLead = cpSim.Active; cpSim.CheckEquity(cpLead, -100); Trade(cpSim, 30, 600);
        Check(cpLead.Name == "A01" && cpSim.Active == cpLead && cpSim.Accounts.All(a => Eq(a.DayPnl, 600)) && cpSim.Trades.Count == 3 && cpSim.Trades.Count(t => t.Source.StartsWith("COPY of A01")) == 2 && !cpSim.PerSetup, "COPY: one trade → all 3 accounts +600 (3 trades saved, 2 copies); the leader stays", string.Join(" ", cpSim.Accounts.Select(a => a.Name + " " + a.DayPnl)));
        cpSim.Lock(cpSim.Accounts[1], d1, "test"); Trade(cpSim, 30, -200);
        Check(Eq(cpSim.Accounts[0].DayPnl, 400) && Eq(cpSim.Accounts[1].DayPnl, 600) && Eq(cpSim.Accounts[2].DayPnl, 400), "COPY: a locked account does not copy the next trade");
        var cpS2 = cpS.Copy(); cpS2.BuyRule = "BREAKEVEN"; var cpSim2 = new KeystonePropSim(cpS2); cpSim2.BeginDay(d1); var cpA = cpSim2.Active; cpSim2.CheckEquity(cpA, -2100); Trade(cpSim2, 30, -2100); cpSim2.EndDay();
        cpSim2.BeginDay(d1.AddDays(1));
        Check(cpSim2.Accounts.Count(a => a.Phase == "BLOWN") == 3 && cpSim2.Accounts.Count(a => a.Alive) == 3 && cpSim2.Active != null, "COPY: a loss blows all 3 → the next day starts with 3 new evaluations (the budget rule does not stop a copy batch)", cpSim2.Accounts.Count + " accounts");
        // 📦 BATCH (default): when every evaluation passed or blew, the batch is saved and the next day starts with new ones • ◀ rewind
        var bS = cpS.Copy(); bS.Allocation = "ROTATE"; bS.BuyRule = "BATCH"; bS.Accounts = 2; string bDir = Path.Combine(Path.GetTempPath(), "ks_batch_" + Guid.NewGuid().ToString("N"));
        var bSim = new KeystonePropSim(bS) { Folder = bDir }; bSim.Plan = KeystonePropSim.Weekdays(d1, d1.AddDays(10)); bSim.Snap(); bSim.BeginDay(d1);
        Trade(bSim, 30, -2100); Trade(bSim, 30, -2100); bSim.EndDay(); bSim.PlanIndex++; bSim.SaveState(); bSim.Snap(); bSim.BeginDay(d1.AddDays(1));
        Check(bSim.Batches.Count == 1 && bSim.Batches[0].Blown == 2 && bSim.Batches[0].Trades == 2 && bSim.Accounts.Count(a => a.Alive) == 2 && bSim.Events.Any(e => e.Kind == "BATCH") && File.Exists(Path.Combine(bDir, "batches.csv")),
            "BATCH: 2 blown → batch 1 saved (2 trades, 2 blown) and 2 new evaluations the next morning", bSim.Batches.Count + " batches • " + bSim.Accounts.Count(a => a.Alive) + " alive");
        var bBack = KeystonePropSim.Rewind(bDir, 0);
        Check(bBack != null && bBack.PlanIndex == 0 && bBack.Accounts.Count == 0 && bBack.Batches.Count == 0 && !KeystonePropSim.HasSnap(bDir, 1), "◀ PREVIOUS DAY: the run back to day 1's morning (nothing bought yet, later saves dropped)");
        try { Directory.Delete(bDir, true); } catch { }
        var oS = new KeystonePropSim(s.Copy()); Check(Eq(oS.Ruin(20), 1) && oS.OddsPrompt().Contains("PASS RATE") && KeystonePropSim.AiOddsSystem.Contains("payouts minus evaluation fees"), "🎲 ODDS: nothing paid yet = 100% risk of no payout; the AI prompt carries the run");
        var cpL = cpSim.Active; string cpSt = cpSim.CheckEquity(cpL, -2600);
        Check(cpSt == "BLOWN" && cpSim.Accounts[2].Phase == "BLOWN" && cpSim.Accounts[1].Alive, "COPY: an open loss through the floor blows every copying account too (the locked one is safe)", cpSt + " " + cpSim.Accounts[2].Phase);
        Check(Eq(cpSim.CopyRatio(cpSim.Accounts[0], cpSim.Accounts[0]), 1) && cpS.Describe().Contains("COPY TRADING") && KeystoneSimSettings.FromText(cpS.ToText()).Allocation == "COPY", "COPY: sized by contracts, described, saved with the set-up");
        var s2 = s.Copy(); s2.Accounts = 1; var simH = new KeystonePropSim(s2); simH.BeginDay(d1);
        Trade(simH, 5, 1400); simH.EndDay(); simH.BeginDay(d1.AddDays(1)); Trade(simH, 5, 1400); Trade(simH, 30, 300); simH.EndDay();
        var h = simH.Accounts[0];
        Check(h.Phase == "EVAL" && Eq(h.Bal, 3100) && Eq(h.ProfitShort, 2800), "3,100 made but 2,800 from trades held 5 s → not passed (11-second rule)", h.Phase + " " + h.ProfitShort);

        // blown: equity (open trade included) touches the floor
        var s3 = s.Copy(); s3.Accounts = 1; var simB = new KeystonePropSim(s3); simB.BeginDay(d1);
        var b = simB.Active;
        Check(simB.CheckEquity(b, -1500) == "" && simB.CheckEquity(b, -2000) == "BLOWN", "open −$2,000 on a fresh evaluation = BLOWN (floor −2,000)");
        simB.AddFill(b, -2010); simB.EndTrade(b, T(d1, 60, -2010));
        Check(b.Phase == "BLOWN" && b.WasPhase == "EVAL" && simB.Active == null, "the account is blown when the trade closes below the floor; no account left today");
        var dB = simB.EndDay();
        Check(dB.Blown == 1 && simB.BlownEval == 1, "the day row counts the blown evaluation");
        simB.BeginDay(d1.AddDays(1));
        Check(simB.Accounts.Count == 2 && simB.Active != null && simB.Active.Name == "A02" && Eq(simB.Spent, 240), "a new evaluation replaces the blown one the next day");
        // BREAKEVEN (the default): the first evaluations are the budget — no new one until the payouts have paid back all that was spent
        Check(new KeystoneSimSettings().BuyRule == "BATCH", "the default: a new batch of evaluations by itself once the last one is done");
        var dflt = new KeystoneSimSettings();
        Check(dflt.MaxTradesPerDay == 1 && dflt.MaxTradesOf(true) == 1 && dflt.RotTarget == 1500 && dflt.RotStop == 500 && dflt.FundRotTarget == 1500 && dflt.FundRotStop == 500, "the default rotation: ONE trade a day per account, +1,500 / −500, funded the same");
        var sBE = s3.Copy(); sBE.BuyRule = "BREAKEVEN"; sBE.Accounts = 2; var simE = new KeystonePropSim(sBE); simE.BeginDay(d1);
        Check(simE.Accounts.Count == 2 && Eq(simE.Spent, 240), "day 1: the budget buys the first 2 evaluations");
        var e1 = simE.Active; simE.AddFill(e1, -2010); simE.EndTrade(e1, T(d1, 60, -2010)); simE.EndDay();
        simE.BeginDay(d1.AddDays(1));
        Check(simE.Accounts.Count == 2 && Eq(simE.Spent, 240) && Eq(simE.ToBreakeven, 240) && simE.Events.Count(x => x.Kind == "WAITING") == 1, "one blown: NOT replaced while the run is $240 under water (waiting noted once)", simE.Accounts.Count + " " + simE.ToBreakeven);
        simE.EndDay(); simE.BeginDay(d1.AddDays(2));
        Check(simE.Events.Count(x => x.Kind == "WAITING") == 1, "the wait is noted once, not every day");
        simE.Accounts[1].Cash += 300; simE.EndDay(); simE.BeginDay(d1.AddDays(3));   // paid back, but an evaluation is still running
        Check(simE.Accounts.Count == 2, "paid back but an evaluation is still running → no new one yet");
        simE.Accounts[1].Phase = "FUNDED"; simE.Accounts[1].WasPhase = "FUNDED"; simE.EndDay(); simE.BeginDay(d1.AddDays(4));   // every evaluation done
        Check(simE.Accounts.Count == 4 && Eq(simE.Spent, 480) && simE.Events.Any(x => x.Kind == "REFILL"), "paid back AND every evaluation done → the pool is refilled (REFILL saved)", simE.Accounts.Count + " " + simE.Spent);
        simE.EndDay(); var grw = simE.Growth(); var gl = grw.Last();
        Check(grw.Count == simE.Days.Count && gl.Initial == 2 && gl.TotalBought == 4 && gl.SinceInitial == 2 && gl.TotalBlownEval == 1 && grw[0].BlownEval == 1 && gl.Refill && !grw[0].Refill && Eq(gl.FirstSpent, 240) && Eq(gl.FirstNet, -240), "EVAL TRACKER: per day — 2 first evaluations, 2 bought since (refill day), 1 blown on day 1, the first investment alone −240", gl.Initial + " " + gl.TotalBought + " " + gl.SinceInitial + " " + gl.TotalBlownEval + " " + gl.FirstNet);
        // TODAY: traded / at the day target / locked / passed / blown, and each account's day
        var sT = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "ONEWIN", BuyRule = "ALWAYS", Accounts = 3, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simTd = new KeystonePropSim(sT); simTd.BeginDay(d1); Trade(simTd, 30, 1480); Trade(simTd, 30, -500); var tdy = simTd.DaySummary(d1);
        Check(tdy.Open && tdy.Traded == 2 && tdy.AtTarget == 1 && tdy.Locked == 1 && tdy.Trades == 2 && tdy.Wins == 1 && Eq(tdy.Pnl, 980) && tdy.Rows.Count == 2 && tdy.Rows[0].State.Contains("AT DAY TARGET"), "TODAY (open): 2 traded, 1 at its day target (+1,480 ≈ 1,500) and locked, P/L +980", tdy.Traded + " " + tdy.AtTarget + " " + tdy.Locked + " " + tdy.Pnl);
        simTd.EndDay(); var past = simTd.DaySummary(d1);
        Check(!past.Open && past.Traded == 2 && past.AtTarget == 1 && past.Locked == 1 && Eq(past.Pnl, 980), "the same day after its close (from the trades and events)", past.Traded + " " + past.AtTarget + " " + past.Locked);

        // funded payouts: 5 qualifying days of ≥ $150 → 50% of the profit, cap $2,000, 90% split; 2 payouts max → RETIRED
        var s4 = s.Copy(); s4.Accounts = 1; s4.ReplacePassed = false; s4.Rules.MaxPayouts = 2; s4.Rules.MinHoldSeconds = 0; var simF = new KeystonePropSim(s4); simF.BeginDay(d1);
        var f = simF.Active; f.Phase = "FUNDED"; f.WasPhase = "FUNDED"; simF.EndDay();
        for (int i = 1; i <= 5; i++) { simF.BeginDay(d1.AddDays(i)); Trade(simF, 30, 400); simF.EndDay(); }
        Check(f.Payouts == 1 && Eq(f.Gross, 1000) && Eq(f.Cash, 900) && Eq(f.Bal, 1000), "after 5 days of +$400: payout 50% of $2,000 = $1,000 gross → $900 to you, balance $1,000", f.Payouts + " " + f.Gross + " " + f.Cash + " " + f.Bal);
        for (int i = 6; i <= 9; i++) { simF.BeginDay(d1.AddDays(i)); Trade(simF, 30, 100); simF.EndDay(); }
        Check(f.Payouts == 1 && f.Qualifying == 0, "+$100 days do not qualify (need ≥ $150)");
        for (int i = 10; i <= 14; i++) { simF.BeginDay(d1.AddDays(i)); Trade(simF, 30, 2000); simF.EndDay(); }
        Check(f.Payouts == 2 && Eq(f.Gross, 1000 + 2000) && f.Phase == "RETIRED", "second payout capped at $2,000, then RETIRED (max 2 payouts)", f.Payouts + " " + f.Gross + " " + f.Phase);

        // 💰 payout cycles in order: 5 qualifying days → payout (gross, to you, carried over), then cycle 2 runs
        var sc = s.Copy(); sc.Accounts = 1; sc.ReplacePassed = false; sc.Rules.MinHoldSeconds = 0; var simC = new KeystonePropSim(sc); simC.BeginDay(d1);
        var fc = simC.Active; fc.Phase = "FUNDED"; fc.WasPhase = "FUNDED"; fc.PassedOn = d1; simC.EndDay();
        for (int i = 1; i <= 6; i++) { simC.BeginDay(d1.AddDays(i)); Trade(simC, 30, 400); simC.EndDay(); }
        var pcy = simC.PayoutCycles(fc);
        Check(pcy.Count == 2 && !pcy[0].Open && pcy[0].Qualifying == 5 && Eq(pcy[0].Gross, 1000) && Eq(pcy[0].Cash, 900) && Eq(pcy[0].BalanceAfter, 1000) && pcy[1].Open && pcy[1].Days.Count == 1 && Eq(pcy[1].BalanceAfter, 1400), "payout cycles: 5 qualifying days → $1,000 paid ($900 to you), $1,000 carried; cycle 2 running at $1,400", pcy.Count + " " + (pcy.Count > 0 ? pcy[0].Gross + " " + pcy[0].BalanceAfter : ""));
        // TPT-like: daily payouts above a $2,000 buffer, min $250, 80% split
        var tpt = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("TAKE PROFIT")); var s5 = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", Accounts = 1, ReplacePassed = false, Strategy = "MANUAL", MaxLossesPerDay = 0, Rules = tpt.Copy() };
        var simT = new KeystonePropSim(s5); simT.BeginDay(d1); var tf = simT.Active; tf.Phase = "FUNDED"; tf.WasPhase = "FUNDED"; simT.EndDay();
        simT.BeginDay(d1.AddDays(1)); Trade(simT, 30, 2100); simT.EndDay();
        Check(tf.Payouts == 0, "TPT: +$2,100 is only $100 above the buffer (< $250 minimum) → no payout");
        simT.BeginDay(d1.AddDays(2)); Trade(simT, 30, 400); simT.EndDay();
        Check(tf.Payouts == 1 && Eq(tf.Gross, 500) && Eq(tf.Cash, 400) && Eq(tf.Bal, 2000), "TPT: $500 above the buffer paid out, 80% = $400 to you", tf.Gross + " " + tf.Cash);

        // day limits: 2 losses lock the account for the day; an evaluation at its target stops trading today
        var s6 = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", Accounts = 1, Strategy = "ROTATION", RotTarget = 5000, MaxLossesPerDay = 2, Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy() };
        var simL = new KeystonePropSim(s6); simL.BeginDay(d1); var l = simL.Active; Trade(simL, 30, -100); Check(simL.Active == l, "1 loss: still trading"); Trade(simL, 30, -100);
        Check(l.LockedToday && simL.Active == null, "2 losses: locked for the day");
        simL.EndDay(); simL.BeginDay(d1.AddDays(1)); Trade(simL, 30, 3300);
        Check(!l.LockedToday && simL.Active == l, "target reached, but this day is 106% of the profit (consistency) → it keeps trading");
        simL.EndDay(); simL.BeginDay(d1.AddDays(2));
        Check(l.Phase == "EVAL" && !l.LockedToday && simL.Active == l, "at the target but the best day (3,300) breaks consistency → it keeps trading to fix it", l.DayNote);
        Trade(simL, 30, 100);
        Check(simL.Active == l && !l.LockedToday, "still inconsistent after +$100 → not locked at the target");
        // consistent at the target but too few days: locked, then a day-count trade a day until the minimum days → PASSED
        var s8 = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", Accounts = 1, Strategy = "ROTATION", MaxLossesPerDay = 0, Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy() }; s8.Rules.EvalMinDays = 4; s8.Rules.MinHoldSeconds = 0;
        var simM = new KeystonePropSim(s8); var m = (KeystoneSimAccount)null;
        foreach (var pnl in new[] { 1000.0, 1000, 1100 }) { simM.BeginDay(d1.AddDays(simM.Days.Count)); m = simM.Active; Trade(simM, 30, pnl); simM.EndDay(); }
        Check(m.Phase == "EVAL" && Eq(m.Bal, 3100) && m.LockedToday, "3,100 in 3 days (best 35%) but 4 days needed → locked at the target");
        simM.BeginDay(d1.AddDays(3));
        Check(m.LockedToday && m.DayNote == "DAY-COUNT TRADE" && simM.Active == null, "day 4: only the day-count trade (≈ the commission)");
        simM.EndDay();
        Check(m.Phase == "FUNDED" && m.EvalDays == 4, "day 4 counted → PASSED", m.Phase + " " + m.EvalDays);

        // ---- each account's day: ONE WIN A DAY (−500, −500, +1,500 → +500 locked) and FULL RECOVERY (locked at the day's +target)
        var sw = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "ONEWIN", Accounts = 1, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simW = new KeystonePropSim(sw); simW.BeginDay(d1);
        var w1 = Trade(simW, 30, -500); Trade(simW, 30, -500);
        Check(!w1.LockedToday && simW.Active == w1, "ONE WIN A DAY: two losses, still trading");
        Trade(simW, 30, 1440);
        Check(w1.LockedToday && Eq(w1.DayPnl, 440) && simW.Active == null, "ONE WIN A DAY: the first win (+1,440 after −1,000) locks the day at +440", w1.DayNote);
        simW.EndDay();
        var sr = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "RECOVER", Accounts = 1, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simR = new KeystonePropSim(sr); simR.BeginDay(d1);
        var r1 = Trade(simR, 30, -500); Trade(simR, 30, 600);
        Check(!r1.LockedToday, "FULL RECOVERY: −500 then +600 = +100, still short of +1,500 → keeps trading");
        Trade(simR, 30, 1380);
        Check(r1.LockedToday && Eq(r1.DayPnl, 1480), "FULL RECOVERY: +1,480 for the day (≈ the target, fees in) → locked", r1.DayNote);
        simR.EndDay();

        // ---- the rotation on 1-minute bars (the what-if engine): a day never ends above its target; the stop is checked first
        var bs = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var ba = new KeystoneSimAccount { Id = 1, Phase = "EVAL" }; double bt, bst;
        ba.DayPnl = 1000; KeystoneRotationLab.Bracket(bs, ba, 30, 3000, out bt, out bst);
        Check(Eq(bt, 530) && Eq(bst, 500), "UNTIL THE DAY TARGET: +1,000 made today → the next trade aims at the 500 left (+ fees)", bt + " / " + bst);
        // funded accounts: their own day plan / trades a day, and (AFTER) no trading while an evaluation is still running
        var sf = new KeystoneSimSettings { MaxTradesPerDay = 0,  DayPlan = "NONE", BuyRule = "ALWAYS", ReplacePassed = false, Accounts = 2, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, FundMaxTrades = 1, FundedWhen = "AFTER", Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simF2 = new KeystonePropSim(sf); simF2.BeginDay(d1); simF2.Accounts[0].Phase = "FUNDED"; simF2.Accounts[0].WasPhase = "FUNDED"; simF2.Rotate();
        Check(simF2.Active != null && simF2.Active.Phase == "EVAL" && !simF2.CanTrade(simF2.Accounts[0]), "AFTER: the funded account waits while an evaluation is still running", simF2.Active == null ? "none" : simF2.Active.Name);
        sf.FundedWhen = "WITH"; Check(simF2.CanTrade(simF2.Accounts[0]), "WITH: funded trade alongside the evaluations");
        var fa = simF2.Accounts[0]; simF2.Active = fa; simF2.AddFill(fa, -100); simF2.EndTrade(fa, T(d1, 60, -100));
        Check(fa.LockedToday && fa.DayNote.Contains("funded"), "funded max 1 trade a day → locked after its trade", fa.DayNote);
        var ev = simF2.Accounts[1]; ev.Bal = 1500; ev.BestDay = 1500; ev.Days = 1; simF2.Active = ev; simF2.AddFill(ev, 1500); simF2.EndTrade(ev, T(d1, 60, 1500));
        Check(simF2.PassesAtClose(ev) && simF2.DaySummary(d1).WillPass == 1, "an evaluation at its target today counts as PASSED (funded at the close)");
        var cs0 = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "ONEWIN", RotTarget = 1500, FundRotTarget = 1500, RotStop = 500, FundRotStop = 500, FundCushion = 4000, FundCushionTarget = 200, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var fca = new KeystoneSimAccount { Id = 3, Phase = "FUNDED", Bal = 4100 }; double ct, cst; KeystoneRotationLab.Bracket(cs0, fca, 30, 6000, out ct, out cst);
        fca.Bal = 2500; double ct2, cst2; KeystoneRotationLab.Bracket(cs0, fca, 30, 6000, out ct2, out cst2);
        Check(Eq(ct, 230) && Eq(ct2, 1530), "CUSHION: a funded balance of 4,100 aims at +200 (+fees); under 4,000 the full +1,500", ct + " / " + ct2);
        var fresh = new KeystoneSimAccount { Id = 2, Phase = "EVAL" }; double ft, fst; KeystoneRotationLab.Bracket(bs, fresh, 30, 3000, out ft, out fst);
        Check(Eq(ft, 1530), "the $ target is net of the fees: +1,500 day = 1,530 gross with $30 fees", ft.ToString());
        var lucid2 = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "ONEWIN", BuyRule = "ALWAYS", Accounts = 1, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy() }; lucid2.Rules.MinHoldSeconds = 0;
        var simP = new KeystonePropSim(lucid2); simP.BeginDay(d1); Trade(simP, 30, 1500); simP.EndDay(); simP.BeginDay(d1.AddDays(1)); Trade(simP, 30, 1500); simP.EndDay();
        Check(simP.Accounts[0].Phase == "FUNDED", "two +1,500 days = +3,000, best day 50% → PASSED on day 2", simP.Accounts[0].Phase);
        ba.DayPnl = -500; bs.DayPlan = "RECOVER"; KeystoneRotationLab.Bracket(bs, ba, 30, 520, out bt, out bst);
        Check(Eq(bt, 2030) && Eq(bst, 489), "FULL RECOVERY after −500: aims at +2,000 (+ fees); the stop stays inside the room to the floor", bt + " / " + bst);
        var rd = new KeystoneRotDay { Day = d1 }; int k0 = KeystoneRotDay.Slot(930);
        Check(k0 == 15 * 60 + 30, "09:30 is slot " + (15 * 60 + 30) + " (minutes since 18:00)", k0.ToString());
        for (int i = k0; i < k0 + 60; i++) { float pm0 = 20000 + (i - k0) * 2f, pg0 = 2400 + (i - k0) * 0.5f; rd.MO[i] = pm0; rd.MH[i] = pm0 + 1; rd.ML[i] = pm0 - 1; rd.MC[i] = pm0 + 1; rd.GO[i] = pg0; rd.GH[i] = pg0 + 0.3f; rd.GL[i] = pg0 - 0.3f; rd.GC[i] = pg0 + 0.3f; }
        int hw, xs; double pk, ws;
        double up = KeystoneRotationLab.Trade(rd, k0, k0 + 59, 1, 1, 1, 1, 100, 500, 0, null, out hw, out xs, out pk, out ws);
        Check(hw == 1 && xs > k0 && Eq(up, 100 - KeystoneRotationLab.CostMnq - KeystoneRotationLab.CostMgc), "BUY BUY in a rising market: +$100 target at a minute close, minus the costs", hw + " " + up.ToString("0.00"));
        double dn = KeystoneRotationLab.Trade(rd, k0, k0 + 59, -1, -1, 1, 1, 1000, 30, 0, null, out hw, out xs, out pk, out ws);
        Check(hw == -1 && dn < -30, "SELL SELL in a rising market: the $30 stop, slippage and costs on top", hw + " " + dn.ToString("0.00"));
        var wdays = new List<KeystoneRotDay> { rd };
        var wsim = KeystoneRotationLab.Run(new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", Accounts = 2, Direction = "BUY", MnqQty = 1, MgcQty = 1, RotTarget = 100, RotStop = 100, PauseSeconds = 60, Rules = KeystoneSimRules.Presets()[0].Copy() }, wdays, 930, 1029, null);
        Check(wsim.Days.Count == 1 && wsim.Trades.Count >= 2 && wsim.Trades.GroupBy(t => t.Account).All(g => g.Sum(t => t.Pnl) <= 100 + 1e-6), "what-if run: both accounts trade, no account's day passes its +$100 target", wsim.Trades.Count + " trades");
        Check(KeystoneRotationLab.DaysAtTarget(wsim) >= 1, "account-days at their target are counted", KeystoneRotationLab.DaysAtTarget(wsim).ToString());

        // ---- ⚖ COMPARE SESSIONS: the same days in every session window; your own hours first when they are not one of them
        var rng = new Random(11); var cdays = new List<KeystoneRotDay>();
        for (int dd = 0; dd < 12; dd++)
        {
            var cd = new KeystoneRotDay { Day = d1.AddDays(dd) }; double pm1 = 20000, pg1 = 2400;
            for (int i = 0; i < KeystoneRotDay.Slots - 5; i++)
            {
                double am = (rng.NextDouble() - 0.5) * 8, ag = (rng.NextDouble() - 0.5) * 1.6; double om = pm1, og = pg1; pm1 += am; pg1 += ag;
                cd.MO[i] = (float)om; cd.MC[i] = (float)pm1; cd.MH[i] = (float)(Math.Max(om, pm1) + 1); cd.ML[i] = (float)(Math.Min(om, pm1) - 1);
                cd.GO[i] = (float)og; cd.GC[i] = (float)pg1; cd.GH[i] = (float)(Math.Max(og, pg1) + 0.2); cd.GL[i] = (float)(Math.Min(og, pg1) - 0.2);
            }
            cdays.Add(cd);
        }
        var cs = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "ONEWIN", Accounts = 3, Direction = "RANDOM", MnqQty = 2, MgcQty = 2, FundMnqQty = 2, FundMgcQty = 2, RotTarget = 300, RotStop = 300, FundRotTarget = 300, FundRotStop = 300, StartHhmm = 1000, EndHhmm = 1400, Rules = KeystoneSimRules.Presets()[0].Copy() };
        int calls = 0; var rows = KeystoneRotationLab.Compare(cs, cdays, null, (k, n) => calls++);
        Check(rows.Count == KeystoneRotationLab.SessionNames.Length + 1 && rows[0].Mine && rows[0].Name.StartsWith("YOUR HOURS 10:00") && rows.Count(r => r.Mine) == 1 && calls == rows.Count, "every session window + YOUR HOURS 10:00–14:00 first (not one of the windows)", rows.Count + " rows • " + rows[0].Name);
        Check(rows.All(r => r.Days == 12 && r.Trades > 0 && r.AcctDays > 0 && r.AtTarget <= r.AcctDays && r.Curve.Count == 12), "each row: the 12 days, its trades, account-days, the money curve", string.Join(" | ", rows.Select(r => r.Name.Split(' ')[0] + " " + r.Trades)));
        var wn0 = KeystoneRotationLab.Winner(rows);
        Check(wn0 != null && rows.All(r => Math.Round(r.Net, 2) <= Math.Round(wn0.Net, 2)), "the winner has the most net money", wn0 == null ? "none" : wn0.Name + " " + wn0.Net.ToString("0"));
        var cs2 = cs.Copy(); cs2.StartHhmm = 930; cs2.EndHhmm = 1555; var rows2 = KeystoneRotationLab.Compare(cs2, cdays, null, null);
        Check(rows2.Count == KeystoneRotationLab.SessionNames.Length && rows2.Count(r => r.Mine) == 1 && rows2.First(r => r.Mine).Name.StartsWith("NY DAY"), "hours 09:30–15:55 = the NY DAY row, marked as yours");
        var rt0 = KeystoneSessionWhatIf.Parse(rows[2].Line());
        Check(rt0 != null && rt0.Name == rows[2].Name && rt0.Start == rows[2].Start && rt0.Trades == rows[2].Trades && Eq(rt0.Net, rows[2].Net) && rt0.Curve.Count == rows[2].Curve.Count && rt0.Mine == rows[2].Mine, "a row is saved and read back the same", rows[2].Line().Substring(0, 60));
        string cfold = Path.Combine(Path.GetTempPath(), "keystone_cmp_" + Guid.NewGuid().ToString("N")); KeystoneSessionWhatIf.Save(cfold, rows);
        Check(KeystoneSessionWhatIf.Load(cfold).Count == rows.Count && KeystoneSessionWhatIf.Load(cfold + "_none").Count == 0, "sessions.txt saved with the run; none = empty list");
        // ---- 📡 ENTRY INDICATOR: days that trend in 45-minute blocks (each instrument its own way) → the sides that follow both
        // instruments' last minutes win; it is chosen on the later half of the training days and must beat random sides on unseen days
        Func<int, int, List<KeystoneRotDay>> trendDays = (seed, n) =>
        {
            var r = new Random(seed); var list = new List<KeystoneRotDay>();
            for (int dd = 0; dd < n; dd++)
            {
                var td = new KeystoneRotDay { Day = new DateTime(2024, 1, 1).AddDays(dd + seed * 100) }; double pm = 20000, pg = 2400; int sm = 1, sg = 1;
                for (int i = 0; i < KeystoneRotDay.Slots - 5; i++)
                {
                    if (i % 45 == 0) { sm = r.Next(2) == 0 ? 1 : -1; sg = r.Next(2) == 0 ? 1 : -1; }
                    double om = pm, og = pg; pm += sm * 3 + (r.NextDouble() - 0.5) * 2; pg += sg * 0.3 + (r.NextDouble() - 0.5) * 0.2;
                    td.MO[i] = (float)om; td.MC[i] = (float)pm; td.MH[i] = (float)(Math.Max(om, pm) + 0.5); td.ML[i] = (float)(Math.Min(om, pm) - 0.5);
                    td.GO[i] = (float)og; td.GC[i] = (float)pg; td.GH[i] = (float)(Math.Max(og, pg) + 0.05); td.GL[i] = (float)(Math.Min(og, pg) - 0.05);
                }
                list.Add(td);
            }
            return list;
        };
        var trainD = trendDays(1, 30); var testD = trendDays(2, 15);
        var isS = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", Accounts = 3, Direction = "RANDOM4", BuyRule = "ALWAYS", MnqQty = 2, MgcQty = 2, FundMnqQty = 2, FundMgcQty = 2, RotTarget = 300, RotStop = 300, FundRotTarget = 300, FundRotStop = 300, StartHhmm = 930, EndHhmm = 1555, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var ind = KeystoneEntryIndicator.Build(isS, trainD, 1, 30, 3, null);
        Console.WriteLine("      indicator: " + ind.Describe());
        Check(ind.Samples > 300 && ind.Rules.Count >= 4 && ind.HoldoutEdge, "INDICATOR: learned from every moment × 4 sides; rules found; the later half confirms the edge", ind.Samples + " samples, " + ind.Rules.Count + " rules, " + ind.HoldoutWin.ToString("0") + "% vs " + ind.HoldoutBase.ToString("0") + "%");
        Check(ind.Rules.All(r => r.DirM.ToString() == r.Key.Split(',')[0] && r.DirG.ToString() == r.Key.Split(',')[1]) && ind.Rules.All(r => r.WinPct >= ind.BaseWin + 3), "INDICATOR: in trending blocks every rule follows both instruments (above the coin flip by the minimum edge)", string.Join(" ", ind.Rules.Select(r => r.Key + "→" + r.DirM + "/" + r.DirG)));
        var ind2 = KeystoneEntryIndicator.FromText(ind.ToText());
        Check(ind2 != null && ind2.Window == ind.Window && ind2.BySession == ind.BySession && ind2.Rules.Count == ind.Rules.Count && ind2.Rules[0].Key == ind.Rules[0].Key && ind2.Rules[0].Wins == ind.Rules[0].Wins && ind2.TrainFrom == ind.TrainFrom && Eq(ind2.HoldoutWin, ind.HoldoutWin), "INDICATOR: saved in one line and read back the same");
        var sig = ind.Signal(5, -2, 1000); var sig0 = ind.Signal(0, 3, 1000);
        Check(sig.Item1 == 1 && sig.Item2 == -1 && sig0.Item1 == 0 && sig0.Item2 == 0, "INDICATOR: MNQ up + MGC down → BUY MNQ + SELL MGC • a flat instrument → WAIT", sig + " " + sig0);
        Check(KeystoneEntryIndicator.FromText("") == null && KeystoneEntryIndicator.FromText("garbage") == null, "INDICATOR: nothing saved = none");
        var isT = isS.Copy(); isT.Indicator = ind.ToText(); isT.AutoFallback = "WAIT";   // the indicator on its own (no BUY BOTH after the pause)
        var irows = KeystoneRotationLab.CompareSides(isT, testD, null, null); var irow = irows.First(r => r.Mine); var mid = irows.FirstOrDefault(r => r.Name.Contains("MIDDLE"));
        Console.WriteLine("      unseen days: " + string.Join(" | ", irows.Select(r => r.Name + " " + r.Net.ToString("0") + " (" + r.WinPct.ToString("0") + "%)")));
        Check(irows.Count >= 8 && irows.Count(r => r.Mine) == 1 && irow.Name.Contains("INDICATOR") && irow.Trades > 0 && irows.Any(r => r.Name.Contains("LUCKIEST")), "INDICATOR vs RANDOM: the indicator, the luckiest and middle of 10 random seeds, the fixed rules");
        Check(mid != null && irow.WinPct > mid.WinPct + 10 && irow.Pnl > mid.Pnl, "INDICATOR on unseen trending days: wins clearly more often than random sides and trades more $", irow.WinPct.ToString("0") + "% / " + irow.Pnl.ToString("0") + " vs " + (mid == null ? "" : mid.WinPct.ToString("0") + "% / " + mid.Pnl.ToString("0")));
        var held = ind.Signal(5, -2, 1000, 4, -1); var formed = ind.Signal(5, -2, 1000, 4, 3);
        Check(held.Item1 == 0 && held.Item2 == 0 && formed.Item1 == 1 && formed.Item2 == -1, "INDICATOR: a state that was already there a minute ago = WAIT • the minute it forms (MGC turned down) = the entry", held + " " + formed);
        Check(ind2.Formed == ind.Formed && ind2.Source == "BUILD" && ind.Describe().StartsWith("formed "), "INDICATOR: the date it was formed and its source are saved with it");
        var isRun = isT.Copy(); isRun.Direction = "INDICATOR"; isRun.PauseSeconds = 600; isRun.MaxTradesPerDay = 1; var irun = KeystoneRotationLab.Run(isRun, testD, 930, 1555, null);
        bool allFresh = irun.Trades.Count > 0 && irun.Trades.All(t => { var dd = testD.First(x => x.Day == t.Day.Date); var sg = ind.Signal(dd, KeystoneRotDay.SlotOf(t.Entry)); return sg.Item1 != 0; });
        Check(allFresh && irun.Trades.GroupBy(t => t.Day.ToString("yyyyMMdd") + t.Account).All(g => g.Count() == 1), "INDICATOR run: every entry is the minute a setup formed (no 10-minute pause in between) • one trade a day per account", irun.Trades.Count + " trades");
        // ---- 🛑 LOSERS + BREAKEVEN MATH: a trade that was up ~+$1,000 then lost −$500 is saved by a breakeven step at +$500, not at +$1,100
        var bd = new KeystoneRotDay { Day = new DateTime(2025, 2, 3) }; int bk = KeystoneRotDay.Slot(1000); double bp = 20000;
        for (int i = 0; i < KeystoneRotDay.Slots - 5; i++)
        {
            double o = bp; if (i >= bk && i < bk + 50) bp += 1; else if (i >= bk + 50) bp -= 2;
            bd.MO[i] = (float)o; bd.MC[i] = (float)bp; bd.MH[i] = (float)Math.Max(o, bp); bd.ML[i] = (float)Math.Min(o, bp);
            bd.GO[i] = 2400; bd.GC[i] = 2400; bd.GH[i] = 2400; bd.GL[i] = 2400;
        }
        var beS = new KeystoneSimSettings { RotTarget = 1500, RotStop = 500, FundRotTarget = 1500, FundRotStop = 500, StartHhmm = 930, EndHhmm = 1555, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var beSim = new KeystonePropSim(beS); beSim.Trades.Add(new KeystoneSimTrade { Day = bd.Day, Entry = bd.Day.AddHours(10), Legs = "BUY 10 MNQ", Phase = "EVAL", Pnl = -540, Reason = "STOP" });
        int lm, lg, lqm, lqg; Check(KeystoneBreakevenStudy.Legs("BUY 10 MNQ + SELL 3 MGC", out lm, out lg, out lqm, out lqg) && lm == 1 && lg == -1 && lqm == 10 && lqg == 3, "LOSERS: the legs are read back (sides + contracts)");
        List<KeystoneLoser> losersL; var beRows = KeystoneBreakevenStudy.Run(beSim, new List<KeystoneRotDay> { bd }, new double[] { 500, 1100 }, out losersL, null);
        var r500 = beRows.First(r => r.At == 500); var r1100 = beRows.First(r => r.At == 1100); var bestBe = KeystoneBreakevenStudy.Best(beRows);
        Console.WriteLine("      breakeven math: base " + beRows[0].Net.ToString("0") + " • +500 " + r500.Net.ToString("0") + " (saved " + r500.Saved + ") • +1100 " + r1100.Net.ToString("0") + " • loser peak " + (losersL.Count > 0 ? losersL[0].Peak.ToString("0") : "-"));
        Check(losersL.Count == 1 && losersL[0].Peak > 900 && losersL[0].Peak < 1100, "LOSERS: the losing trade's best open profit before the loss (~+$1,000) is measured on the bars", losersL.Count > 0 ? losersL[0].Peak.ToString("0") : "none");
        Check(beRows[0].Net < -500 && r500.Saved == 1 && r500.Change > 400 && r1100.Saved == 0 && Math.Abs(r1100.Change) < 1 && bestBe != null && bestBe.At == 500, "BREAKEVEN MATH: +$500 saves the loser (≈ +$500 better), +$1,100 never triggers; the best level is +$500");
        var btr = new KeystoneSimTrade { Day = bd.Day, Entry = bd.Day.AddHours(10), Legs = "BUY 1 MNQ", Pnl = -10, Peak = 123.5, Worst = -60 };
        Check(btr.Line().EndsWith(",123.50,-60.00,,") && KeystoneSimTrade.Header.EndsWith("peak,worst,entry_px,exit_px"), "a trade saves its peak and worst open P/L (and its prices when known)");
        btr.EntryPx = 29913.5; btr.ExitPx = 29813.5; Check(btr.Line().EndsWith(",123.50,-60.00,29913.5,29813.5"), "a trade saves its entry / exit prices", btr.Line());
        // an account $50 above its floor cannot carry a −$500 trade: RETIRED at the floor (not locked again every day, never trading)
        var rfS = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", Accounts = 2, BuyRule = "ALWAYS", Strategy = "ROTATION", Rules = KeystoneSimRules.Presets()[0].Copy() }; var rf = new KeystonePropSim(rfS); rf.BeginDay(new DateTime(2025, 3, 3));
        var ra = rf.Accounts[0]; ra.Bal = rf.Floor(ra) + 50; var rb = rf.Accounts[1];
        Check(rf.RetireIfNoRoom(ra, new DateTime(2025, 3, 3, 10, 0, 0), 40) && ra.Phase == "RETIRED" && ra.EndReason == "at the floor" && !ra.Alive && rf.Events.Any(e => e.Kind == "RETIRED" && e.Text.Contains("too close")), "NO ROOM: $50 above the floor (−$500 stop) → RETIRED at the floor, saved as an event");
        Check(!rf.RetireIfNoRoom(rb, new DateTime(2025, 3, 3, 10, 0, 0), 40) && rb.Alive && rf.Active == rb, "an account with room keeps trading (and is the one in turn)");
        // ONE SETUP PER ACCOUNT (studio strategy): MNQ and MGC each rotate through the accounts; an account takes one setup per instrument a day
        var psS = new KeystoneSimSettings { Strategy = "STUDIO", Accounts = 3, BuyRule = "ALWAYS", Rules = KeystoneSimRules.Presets()[0].Copy() }; var ps = new KeystonePropSim(psS); ps.BeginDay(new DateTime(2025, 3, 3));
        var mA = ps.NextFor("MNQ", null); ps.Took(mA, "MNQ"); var gA = ps.NextFor("MGC", new List<KeystoneSimAccount> { mA }); ps.Took(gA, "MGC");
        Check(ps.PerSetup && psS.SetupsPerAccount == 1 && mA.Id == 1 && gA.Id == 2, "ONE SETUP PER ACCOUNT (default): the MNQ setup → A01, the MGC setup at the same time → A02 (not the same account)", mA.Name + " / " + gA.Name);
        ps.AddFill(mA, -100); ps.EndTrade(mA, new KeystoneSimTrade { Entry = new DateTime(2025, 3, 3, 10, 0, 0), Exit = new DateTime(2025, 3, 3, 10, 5, 0), Legs = "SELL 10 MNQ", Pnl = -100, Reason = "STOP" });
        var m2 = ps.NextFor("MNQ", new List<KeystoneSimAccount> { gA }); var m3 = ps.NextFor("MNQ", new List<KeystoneSimAccount> { gA }); ps.Took(m2, "MNQ");
        Check(m2.Id == 3 && !mA.LockedToday && ps.NextFor("MNQ", new List<KeystoneSimAccount> { gA }) == null && ps.NextFor("MGC", new List<KeystoneSimAccount>()).Id == 3 && ps.NextFor("MGC", new List<KeystoneSimAccount> { m2 }).Id == 1, "the next MNQ setup → A03 (A01 had its MNQ setup, A02 holds the MGC one) • no 4th MNQ account • MGC goes on in turn: A03, or A01 while A03 is busy", m2.Name);
        var sh2 = psS.Copy(); sh2.SetupsPerAccount = 2; Check(!new KeystonePropSim(sh2).PerSetup && !new KeystonePropSim(new KeystoneSimSettings { Strategy = "ROTATION" }).PerSetup, "SETUPS PER ACCOUNT = 2: one account may hold both (as before) • the rotation keeps its own turn");
        var od = KeystonePropSim.Weekdays(new DateTime(2026, 6, 1), new DateTime(2026, 6, 1)); var odW = KeystonePropSim.Weekdays(new DateTime(2026, 5, 31), new DateTime(2026, 5, 31));
        Check(od.Count == 1 && od[0] == new DateTime(2026, 6, 1) && odW.Count == 0, "JUST ONE DAY: FROM = TO is a one-day run (a Sunday has none)");
        // PASS FIRST (default): a passed account waits in the funded pool; when every evaluation is done the run asks; started → it trades
        Check(new KeystoneSimSettings().FundedWhen == "STAGES", "the default: SIMPLE (evals → funded → next batch)");
        // SIMPLE (STAGES): the evaluations trade until each passed or blew; then the funded ones by themselves (no question); the next
        // batch only when every funded account is blown / retired
        var stS = new KeystoneSimSettings { MaxTradesPerDay = 0, Accounts = 2, BuyRule = "BATCH", ReplacePassed = false, Strategy = "ROTATION", Rules = KeystoneSimRules.Presets()[0].Copy() };
        var st = new KeystonePropSim(stS); st.BeginDay(new DateTime(2025, 3, 3)); st.Accounts[0].Phase = "FUNDED"; st.Accounts[0].WasPhase = "FUNDED"; st.Accounts[0].PassedOn = new DateTime(2025, 3, 3);
        bool stWait = !st.CanTrade(st.Accounts[0]) && st.CanTrade(st.Accounts[1]);
        st.Accounts[1].Phase = "BLOWN"; st.Accounts[1].WasPhase = "EVAL"; st.EndDay();
        st.BeginDay(new DateTime(2025, 3, 4)); bool stFunded = st.Accounts.Count == 2 && st.CanTrade(st.Accounts[0]) && !st.FundedWaiting;
        st.Accounts[0].Phase = "BLOWN"; st.EndDay(); st.BeginDay(new DateTime(2025, 3, 5));
        Check(stWait && stFunded && st.Accounts.Count(x => x.Alive && x.Phase == "EVAL") == 2, "SIMPLE: funded waits while evals trade → evals done: funded trade by themselves, no new batch yet → funded done: a new batch of 2", stWait + " " + stFunded + " " + st.Accounts.Count);
        // FUNDED ONLY: every account starts funded (fee + activation), trades at once; a new set only when they are all done
        var foS = new KeystoneSimSettings { MaxTradesPerDay = 0, Accounts = 2, StartAs = "FUNDED", BuyRule = "BATCH", Strategy = "ROTATION", Rules = KeystoneSimRules.Presets()[0].Copy() };
        var fo = new KeystonePropSim(foS); fo.BeginDay(new DateTime(2025, 3, 3)); bool foStart = fo.Accounts.Count == 2 && fo.Accounts.All(x => x.Phase == "FUNDED" && fo.CanTrade(x)) && !fo.FundedWaiting;
        fo.Accounts[0].Phase = "BLOWN"; fo.EndDay(); fo.BeginDay(new DateTime(2025, 3, 4)); bool foWait = fo.Accounts.Count == 2;
        fo.Accounts[1].Phase = "BLOWN"; fo.EndDay(); fo.BeginDay(new DateTime(2025, 3, 5));
        Check(foStart && foWait && fo.Accounts.Count(x => x.Alive && x.Phase == "FUNDED") == 2, "FUNDED ONLY: accounts start funded and trade at once; one blown → the other carries on; all done → a new set of 2 funded", foStart + " " + foWait + " " + fo.Accounts.Count);
        var pf = new KeystonePropSim(new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "ASK", Accounts = 2, BuyRule = "ALWAYS", ReplacePassed = false, Strategy = "ROTATION", Rules = KeystoneSimRules.Presets()[0].Copy() }); pf.BeginDay(new DateTime(2025, 3, 3));
        pf.Accounts[0].Phase = "FUNDED"; pf.Accounts[0].WasPhase = "FUNDED";
        Check(!pf.CanTrade(pf.Accounts[0]) && pf.CanTrade(pf.Accounts[1]) && !pf.FundedWaiting, "PASS FIRST: the funded account does not trade while an evaluation is running (no question yet)");
        pf.Accounts[1].Phase = "BLOWN"; pf.Accounts[1].WasPhase = "EVAL";
        Check(pf.FundedWaiting && !pf.CanTrade(pf.Accounts[0]), "every evaluation done → the funded accounts WAIT for my choice");
        pf.FundedStarted = true; Check(!pf.FundedWaiting && pf.CanTrade(pf.Accounts[0]), "START FUNDED → they trade");
        // PROP FIRMS: named firms with their own evaluation count and rules, bought firm by firm (the rotation goes A, B, C, A …)
        var fmS = new KeystoneSimSettings { MaxTradesPerDay = 0, BuyRule = "ALWAYS", ReplacePassed = false, Strategy = "ROTATION", Rules = KeystoneSimRules.Presets()[0].Copy(),
            Firms = "North Wind:2:120:3000:2000:EOD:0:50:2:0;Blue Peak:1:99:3000:2500:STATIC:0:40:3:0;Iron Bridge:2:150:2500:1500:INTRADAY:0:50:2:0" };
        var fm = new KeystonePropSim(fmS); fm.BeginDay(new DateTime(2025, 3, 3));
        Check(fm.HasFirms && fm.Accounts.Count == 5 && string.Join(",", fm.Accounts.Select(x => x.Name)) == "NW-01,BP-01,IB-01,NW-02,IB-02" && string.Join("", fm.Accounts.Select(x => x.Firm)) == "01202", "FIRMS: 2 + 1 + 2 evaluations bought firm by firm, named by the firm (NW-01, BP-01, IB-01, NW-02, IB-02)", string.Join(",", fm.Accounts.Select(x => x.Name)));
        Check(Eq(fm.Spent, 120 * 2 + 99 + 150 * 2) && Eq(fm.Floor(fm.Accounts[1]), -2500) && Eq(fm.Floor(fm.Accounts[2]), -1500) && fm.RulesOf(fm.Accounts[2]).Target == 2500 && fm.FirmName(fm.Accounts[1]) == "Blue Peak", "FIRMS: each its own cost, drawdown (−2,500 static / −1,500 intraday) and target");
        var nwA = fm.Accounts[0]; nwA.PeakEod = 1000; var bpA = fm.Accounts[1]; bpA.PeakEod = 1000;
        Check(Eq(fm.Floor(nwA), -1000) && Eq(fm.Floor(bpA), -2500), "TRAILING (end of day): +$1,000 lifts the floor to −1,000 • STATIC: the floor stays at −2,500");
        nwA.PeakEod = 2600; Check(Eq(fm.Floor(nwA), 0), "TRAILING stops at the start balance (locks at 0 once the peak is +$2,000 or more)");
        var ibA = fm.Accounts[2]; var eqSt = fm.CheckEquity(ibA, 800); Check(eqSt == "" && Eq(fm.Floor(ibA), -700), "INTRADAY trailing: open profit +$800 lifts the floor at once (−1,500 → −700)", fm.Floor(ibA).ToString());
        // 🛡 THE TRAILING VIEW: INTRADAY follows the open profit live; EOD keeps today's floor until the close; the buffer = equity − floor
        var tvI = fm.Trail(ibA, 300); var tvE = fm.Trail(nwA, 500);   // IB peak +800 (from the open profit above), now +300 open • NW: best close +2,600
        Check(tvI.Type == "INTRADAY" && Eq(tvI.Peak, 800) && Eq(tvI.Floor, -700) && Eq(tvI.Room, 1000) && Eq(tvI.FromPeak, 500) && !tvI.Locked && Eq(tvI.GoalLeft, 2200) && tvI.GoalName == "PROFIT TARGET",
            "TRAILING VIEW (live): peak +800 → floor −700 • now +300 = buffer $1,000, $500 off the peak • $2,200 to the target", tvI.Peak + " " + tvI.Floor + " " + tvI.Room);
        var tvI2 = fm.Trail(ibA, 1200); Check(Eq(tvI2.Peak, 1200) && Eq(tvI2.Floor, -300) && Eq(tvI2.Room, 1500), "TRAILING VIEW (live): a new high +1,200 → the floor follows at once (−300), the buffer stays the full $1,500");
        nwA.PeakEod = 1000; var tvE2 = fm.Trail(nwA, 1800); Check(tvE.Type == "EOD" && Eq(tvE2.Peak, 1000) && Eq(tvE2.Floor, -1000) && Eq(tvE2.Room, 2800), "TRAILING VIEW (end of day): +1,800 open does NOT move the floor (−1,000 from the last best close) until the close");
        nwA.PeakEod = 2600; Check(fm.Trail(nwA, 0).Locked && Eq(fm.Trail(nwA, 0).Floor, 0), "TRAILING VIEW: locked at the start balance once the peak is +$2,000");
        var fl2 = KeystoneSimFirm.Parse(KeystoneSimFirm.Text(fm.FirmList), fmS.Rules, 0); Check(fl2.Count == 3 && fl2[2].Rules.EvalDdType == "INTRADAY" && fl2[1].Count == 1 && fl2[0].Code == "NW", "FIRMS: saved in one line and read back");
        // 🗓 SESSIONS BY DAY: every day × six session windows; the best session per day; saved and read back
        var dsRows = KeystoneDaySession.Run(isT, testD.Take(3).ToList(), null, null);
        var dsBest = KeystoneDaySession.BestOf(dsRows);
        Check(dsRows.Count == 3 * 6 && dsBest.Count == 3 && dsRows.All(r => r.Setup == KeystoneDaySession.SetupOf(isT)) && dsRows.Sum(r => r.Trades) > 0 && dsRows.All(r => r.SideTrades.Sum() == r.Trades), "SESSIONS BY DAY: 3 days × 6 sessions, the best session for each day, sides counted", dsRows.Sum(r => r.Trades) + " trades");
        var dsBack = KeystoneDaySession.Parse(dsRows[4].Line()); Check(dsBack != null && dsBack.Key == dsRows[4].Key && Eq(dsBack.Pnl, dsRows[4].Pnl) && dsBack.SideWins[0] == dsRows[4].SideWins[0], "a session-day row is saved and read back");
        Check(KeystoneDaySession.AiPrompt(dsRows).Contains("BY SESSION") && KeystoneDaySession.AiPrompt(dsRows).Contains("BEST SESSION BY WEEKDAY"), "the AI's research prompt sums up the pool");
        // THE BUILT-IN INDICATOR (6 years): it reads, has rules, and checked on the later half of its training years
        var bi = KeystoneEntryIndicator.FromText(System.IO.File.ReadAllText("docs/research/indicator/builtin_indicator.txt"));
        Check(bi != null && bi.Rules.Count >= 10 && bi.HoldoutEdge && bi.Target == 1500 && bi.Stop == 1000 && bi.TrainTo < new DateTime(2024, 1, 1), "BUILT-IN INDICATOR: " + (bi == null ? "none" : bi.Rules.Count + " rules, trained to " + bi.TrainTo.ToString("yyyy-MM-dd") + ", later-half " + bi.HoldoutWin.ToString("0.0") + "% vs " + bi.HoldoutBase.ToString("0.0") + "%"));
        // OPENING FVG • MNQ: synthetic 1-minute days from 09:30 (each minute: open, high, low, close)
        Func<DateTime, double[][], List<KeystoneArcBar>> fvgDay = (dd, mins) =>
        {
            var fl = new List<KeystoneArcBar>(); var ft0 = dd.AddDays(-1).AddHours(18);
            for (int i = 0; i < 930; i++) { double p0 = 100; fl.Add(new KeystoneArcBar { Symbol = "MNQ", Time = ft0.AddMinutes(i + 1), Open = p0, High = p0, Low = p0, Close = p0 }); }   // 18:00 → 09:30 flat
            var open = dd.AddHours(9).AddMinutes(30); for (int i = 0; i < mins.Length; i++) fl.Add(new KeystoneArcBar { Symbol = "MNQ", Time = open.AddMinutes(i + 1), Open = mins[i][0], High = mins[i][1], Low = mins[i][2], Close = mins[i][3] });
            return fl;
        };
        Func<double, double, double, double, double[][]> candle5 = (o, ch5, l0, c) => Enumerable.Range(0, 5).Select(i => i == 0 ? new[] { o, Math.Max(o, ch5), Math.Min(o, l0), o } : i == 4 ? new[] { o, ch5, l0, c } : new[] { o, o, o, o }).ToArray();
        var ofR = KeystoneOpeningRange.Presets().First(x => x.Mode == "FVG");
        // c1 high 101 • c2 up • c3 low 106 → bullish FVG 101–106, 50% = 103.5 (c4 overlaps c2: no second FVG); then a dip to 103 fills; then +50 pts (10 MNQ: $1,000) = target
        var ofMins = candle5(100, 101, 99, 100.5).Concat(candle5(100.5, 107, 100.5, 106.5)).Concat(candle5(106.5, 110, 106, 109)).Concat(candle5(109, 109, 106.8, 107))
            .Concat(new[] { new[] { 107.0, 107, 103, 104 } }).Concat(Enumerable.Range(0, 60).Select(i => new[] { 104.0 + i, 105.0 + i, 103.6 + i, 105.0 + i })).ToArray();
        var of1 = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 4), ofMins), ofR, "MNQ");
        Check(of1.Dir == 1 && of1.Stack == 1 && Eq(of1.ZoneLo, 101) && Eq(of1.ZoneHi, 106) && of1.Filled && Eq(of1.Entry, 103.5) && Eq(of1.TargetPts, 50) && Eq(of1.StopPts, 100) && of1.Reason == "TARGET" && Eq(of1.Points, 50), "OPENING FVG: bullish 101–106 → BUY limit at 50% (103.5) → +50 pts = +$1,000 for 10 MNQ", of1.Reason + " " + of1.Entry + " " + of1.Points);
        // stacked: c4 also forms a bullish FVG (low 112 above c2's high 107) → one zone 101–112, 50% = 106.5
        var ofMins2 = candle5(100, 101, 99, 100.5).Concat(candle5(100.5, 107, 100.5, 106.5)).Concat(candle5(106.5, 110, 106, 109)).Concat(candle5(112.5, 115, 112, 114)).Concat(candle5(114, 114, 109.5, 113))
            .Concat(new[] { new[] { 113.0, 113, 106, 107 } }).Concat(Enumerable.Range(0, 30).Select(i => new[] { 107.0, 107.5, 106.8, 107 })).ToArray();
        var of2 = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 5), ofMins2), ofR, "MNQ");
        Check(of2.Dir == 1 && of2.Stack == 2 && Eq(of2.ZoneLo, 101) && Eq(of2.ZoneHi, 112) && of2.Filled && Eq(of2.Entry, 106.5) && (of2.Reason == "TIME EXIT" || of2.Reason == "SESSION END"), "OPENING FVG: two FVGs in a row = one zone 101–112 → the entry is 50% of all of it (106.5)", of2.Stack + " " + of2.ZoneLo + "–" + of2.ZoneHi + " @ " + of2.Entry);
        // bearish, never comes back → no trade • 25% setting → the level 25% up from the bottom
        var ofMins3 = candle5(100, 101, 99, 99.5).Concat(candle5(99.5, 99.5, 93, 93.5)).Concat(candle5(93.5, 94, 90, 91)).Concat(Enumerable.Range(0, 30).Select(i => new[] { 91.0, 93.5, 90.0, 91.0 })).ToArray();
        var r25 = ofR.Copy(); r25.FvgPct = 25; var of3 = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 6), ofMins3), r25, "MNQ");
        Check(of3.Dir == -1 && Eq(of3.ZoneLo, 94) && Eq(of3.ZoneHi, 99) && Eq(of3.Entry, 95.25) && !of3.Filled, "OPENING FVG: bearish 94–99, 25% → SELL limit at 95.25; price never comes back = no trade", of3.Entry + " filled " + of3.Filled);
        var sg3 = KeystoneOpeningRange.Signals(ofR, fvgDay(new DateTime(2025, 3, 4), ofMins), null, "MNQ"); var sgG = KeystoneOpeningRange.Signals(ofR, fvgDay(new DateTime(2025, 3, 4), ofMins), null, "MGC");
        Check(sg3.Count == 1 && sg3[0].Order == "LIMIT" && sg3[0].Dir == 1 && Eq(sg3[0].Entry, 103.5) && Eq(sg3[0].StopDist, 100) && sgG.Count == 0 && KeystoneReplaySignals.Names[11].Contains("OPENING FVG"), "OPENING FVG in the studio: a BUY LIMIT signal at the level with its stop / target (MNQ only)");
        var sg2 = KeystoneOpeningRange.Signals(ofR, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MNQ");
        Check(of2.Stages.Count == 2 && Eq(of2.Stages[0].Item4, 103.5) && Eq(of2.Stages[1].Item4, 106.5) && sg2.Count == 1 && sg2[0].Time == of2.Stages[0].Item1 && Eq(sg2[0].Entry, 103.5) && sg2[0].StageAt(of2.Stages[1].Item1).Item4 == 106.5 && sg2[0].ZoneFrom == new DateTime(2025, 3, 5, 9, 30, 0),
            "OPENING FVG: the limit goes in at the FIRST FVG (103.5) and moves to 106.5 when the second one joins; the zone is drawn from the first candle", sg2.Count > 0 ? sg2[0].Time.ToString("HH:mm") + " " + sg2[0].Entry : "none");
        var ofTick = candle5(100, 101, 99, 100.5).Concat(candle5(100.5, 107, 100.5, 106.5)).Concat(candle5(106.5, 110, 106.25, 109)).Concat(candle5(109, 109, 106.8, 107)).ToArray();
        var oft = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 7), ofTick), ofR, "MNQ");
        Check(Eq(oft.ZoneHi, 106.25) && Eq(oft.Entry, 103.5), "OPENING FVG: the level on the tick — 50% of 101–106.25 = 103.625 → BUY limit 103.50 (a tie goes the safer way)", oft.Entry.ToString());
        var rBoth = ofR.Copy(); rBoth.Symbols = "BOTH"; var rG = ofR.Copy(); rG.Symbols = "MGC";
        Check(KeystoneOpeningRange.Signals(rBoth, fvgDay(new DateTime(2025, 3, 4), ofMins), null, "MGC").Count == 1 && KeystoneOpeningRange.Signals(rG, fvgDay(new DateTime(2025, 3, 4), ofMins), null, "MNQ").Count == 0 && rBoth.Describe().Contains("MNQ + MGC"), "OPENING FVG: INSTRUMENTS = MNQ / MGC / BOTH");
        // 09:30 as the MIDDLE (gap) candle: candle 1 = 09:25 → an FVG of 09:25 / 09:30 / 09:35 counts
        var pre = new List<double[]>(); for (int i = 0; i < 5; i++) pre.Add(new[] { 100.0, 100, 100, 100 });
        Func<DateTime, double[][], List<KeystoneArcBar>> fvgDay2 = (dd, mins) => { var lst = fvgDay(dd, mins); var o0 = dd.AddHours(9).AddMinutes(30); var x5 = lst.Where(b0 => b0.Time > o0.AddMinutes(-5) && b0.Time <= o0).ToList(); foreach (var b0 in x5) { b0.High = 101; b0.Low = 99; } return lst; };
        var midM = candle5(100.5, 107, 100.5, 106.5).Concat(candle5(106.5, 110, 106, 109)).Concat(candle5(109, 109, 106.8, 107)).ToArray();
        var rMid = ofR.Copy(); rMid.FvgFrom = 1; var omF = KeystoneOpenFvg.Run(fvgDay2(new DateTime(2025, 3, 10), midM), rMid, "MNQ"); var omD = KeystoneOpenFvg.Run(fvgDay2(new DateTime(2025, 3, 10), midM), ofR, "MNQ");
        Check(omF.Dir == 1 && Eq(omF.ZoneLo, 101) && omF.ZoneStart == new DateTime(2025, 3, 10, 9, 25, 0) && (omD.Dir == 0 || omD.ZoneStart >= new DateTime(2025, 3, 10, 9, 30, 0)), "OPENING FVG: '09:30 is the MIDDLE candle' finds the 09:25 / 09:30 / 09:35 FVG (the default starts at 09:30)", omF.ZoneLo + "–" + omF.ZoneHi + " from " + omF.ZoneStart.ToString("HH:mm"));
        // OPENING FVG filters + the funded bracket (all settings; ANY / 0 = off)
        var rS2 = ofR.Copy(); rS2.MinStack = 2; rS2.JoinMode = "ROW"; var fS1 = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 4), ofMins), rS2, "MNQ"); var fS2 = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 5), ofMins2), rS2, "MNQ");
        Check(!fS1.Filled && fS1.Reason.Contains("ONLY 1 FVG") && fS2.Filled && fS2.Stages.Count == 1 && Eq(fS2.Entry, 106.5), "FILTER FVGS IN A ROW ≥ 2: one FVG = no trade • two = the order goes in at 50% of both (106.5)", fS1.Reason + " | " + fS2.Entry);
        // EVERY FVG (default) vs ROW: bullish FVG, a candle without one, another bullish FVG → ALL joins them, ROW keeps the first
        var gapMins = candle5(100, 101, 99, 100.5).Concat(candle5(100.5, 107, 100.5, 106.5)).Concat(candle5(106.5, 110, 106, 109)).Concat(candle5(109, 109.5, 106.8, 107)).Concat(candle5(107, 115, 107, 114)).Concat(candle5(114, 118, 113, 117)).Concat(Enumerable.Range(0, 20).Select(i => new[] { 114.0, 114.5, 113.5, 114 })).ToArray();
        var rRow = ofR.Copy(); rRow.JoinMode = "ROW"; var gAll = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 11), gapMins), ofR, "MNQ"); var gRow = KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 11), gapMins), rRow, "MNQ");
        Check(gAll.Stack == 2 && Eq(gAll.ZoneLo, 101) && Eq(gAll.ZoneHi, 113) && gAll.Fvgs.Count == 2 && Eq(gAll.Entry, 107) && gRow.Stack == 1 && Eq(gRow.ZoneHi, 106), "THE ZONE GROWS WITH EVERY FVG (default): a later bullish FVG joins (101–113, BUY limit 107); ROW keeps only the first (101–106)", gAll.Stack + " " + gAll.ZoneLo + "–" + gAll.ZoneHi + " | " + gRow.Stack + " " + gRow.ZoneHi);
        var rZb = ofR.Copy(); rZb.ZoneByHhmm = 940; var rOn = ofR.Copy(); rOn.Overnight = "WITH";
        Check(KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 4), ofMins), rZb, "MNQ").Reason.Contains("ZONE SET AFTER 09:40") && KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 4), ofMins), rOn, "MNQ").Reason.Contains("OVERNIGHT"), "FILTERS: zone set by 09:40 (it was 09:45) • overnight WITH the side (it was flat) → no trade");
        var rF = ofR.Copy(); rF.FundStopUsd = 1000; rF.FundTargetUsd = 500; var sgF = KeystoneOpeningRange.Signals(rF, fvgDay(new DateTime(2025, 3, 4), ofMins), null, "MNQ");
        Check(sgF.Count == 1 && Eq(sgF[0].FundStopDist, 50) && Eq(sgF[0].FundTargetDist, 25) && Eq(sgF[0].StopDist, 100), "FUNDED bracket: −$1,000 / +$500 on 10 MNQ = 50 / 25 pts for funded accounts (evaluations keep 100 / 50)");
        // ★ FVG RETRACE (FREQUENT): every 5-min FVG in the window → a limit at 10%, valid 60 min, $1,500 / $1,500 on 10 MNQ (75 pts)
        var frR = KeystoneOpeningRange.Presets().First(x => x.Mode == "FVGR"); var frR0 = frR.Copy(); frR0.FvgFrom = 0; var frS = KeystoneOpeningRange.Signals(frR0, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MNQ");
        Check(frS.Count >= 2 && frS.All(x => x.Order == "LIMIT" && x.Tier == "T" && Eq(x.StopDist, 75) && Eq(x.TargetDist, 75)) && Eq(frS[0].Entry, 105.5) && frS[0].Time == new DateTime(2025, 3, 5, 9, 45, 0) && frS[0].CancelAt == new DateTime(2025, 3, 5, 10, 45, 0)
            && KeystoneOpeningRange.Signals(frR, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MGC").Count == 0 && KeystoneReplaySignals.Names[12].Contains("FVG RETRACE"),
            "FVG RETRACE: every FVG gets its own BUY limit at 10% (101–106 → 105.50 at 09:45, valid to 10:45), ±75 pts on 10 MNQ; MNQ only by default", frS.Count + " • " + (frS.Count > 0 ? frS[0].Entry + " " + frS[0].Time.ToString("HH:mm") : ""));
        // FVG RETRACE, THE 09:30 CANDLE AS THE MIDDLE (FvgFrom 1): 09:25 high 100 • 09:30 jumps to 110 • 09:35 low 106 → bullish FVG 100–106 at 09:40
        var midMins = candle5(100, 110, 100, 109.5).Concat(candle5(109.5, 112, 106, 111)).Concat(Enumerable.Range(0, 10).Select(i => new[] { 111.0, 111.5, 110.5, 111 })).ToArray();
        var frMid = frR; var frFirstR = frR.Copy(); frFirstR.FvgFrom = 0; var sMid = KeystoneOpeningRange.Signals(frMid, fvgDay(new DateTime(2025, 3, 7), midMins), null, "MNQ"); var sFirst = KeystoneOpeningRange.Signals(frFirstR, fvgDay(new DateTime(2025, 3, 7), midMins), null, "MNQ");
        Check(frR.WindowEndHhmm == 1500 && frR.FvgFrom == 1 && frR.WaitExit == 1 && sMid.All(x => x.WaitExit) && sMid.Count == sFirst.Count + 1 && sMid[0].Dir == 1 && Eq(sMid[0].ZoneLo, 100) && Eq(sMid[0].ZoneHi, 106) && Eq(sMid[0].Entry, 105.5) && sMid[0].Time == new DateTime(2025, 3, 7, 9, 40, 0) && sFirst.All(x => x.Time > new DateTime(2025, 3, 7, 9, 40, 0)),
            "FVG RETRACE: the 09:30 opening candle's FVG (default 1: 09:25 / 09:30 / 09:35) → gap 100–106 traded (BUY limit 105.50 at 09:40); setting 0 = the 09:30 candle only as a first candle → not that one; setups until 15:00 by default", sMid.Count + " / " + sFirst.Count);
        // ★ FVG RETRACE 1M (FAST ROTATION): 1-minute FVGs of at least 1 point, a limit at 10% valid 15 min, ±75 pts on 10 MNQ
        var fr1 = KeystoneOpeningRange.Presets().First(x => x.Short == "FVGR1").Copy(); fr1.VwapSide = 0; var s1m = KeystoneOpeningRange.Signals(fr1, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MNQ");
        Check(KeystoneReplaySignals.Names[13].Contains("1M") && fr1.FvgMinutes == 1 && fr1.MinGapPts == 1 && s1m.Count > 0 && s1m.All(x => x.Order == "LIMIT" && Eq(x.StopDist, 75) && (x.CancelAt - x.Time).TotalMinutes <= 15 && x.ZoneHi - x.ZoneLo >= 1 - 1e-9),
            "FVG RETRACE 1M: every 1-minute FVG ≥ 1 point → a 10% limit valid 15 min, ±75 pts on 10 MNQ", s1m.Count + " signals");
        var fr1v = KeystoneOpeningRange.Presets().First(x => x.Short == "FVGR1"); var s1v = KeystoneOpeningRange.Signals(fr1v, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MNQ");
        Check(fr1v.VwapSide == 1 && s1v.Count <= s1m.Count, "FVG RETRACE 1M + VWAP: the VWAP side keeps a subset of the FVGs (buys above VWAP, sells below)", s1v.Count + " of " + s1m.Count);
        // INVERSION: the same FVGs and prices, the opposite side, sent as a stop order; MGC gets its own setups (BOTH by default)
        var frInv = fr1.Copy(); frInv.Invert = 1; var sInv = KeystoneOpeningRange.Signals(frInv, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MNQ");
        bool invOk = sInv.Count == s1m.Count && sInv.Count > 0; for (int i = 0; invOk && i < sInv.Count; i++) invOk = sInv[i].Inverted && !s1m[i].Inverted && sInv[i].Dir == -s1m[i].Dir && Eq(sInv[i].Entry, s1m[i].Entry) && sInv[i].Time == s1m[i].Time && sInv[i].CancelAt == s1m[i].CancelAt && Eq(sInv[i].Stop, sInv[i].Entry - sInv[i].Dir * 75) && Eq(sInv[i].Target, sInv[i].Entry + sInv[i].Dir * 75) && sInv[i].Tag.StartsWith("FVG INV");
        Check(invOk && frInv.Describe().Contains("INVERTED"), "INVERSION: every FVG the same (time, price, valid until), the side flipped (bullish → SELL stop), stop / target mirrored", sInv.Count + " vs " + s1m.Count);
        var frBoth = fr1.Copy(); frBoth.Symbols = "BOTH";
        Check(fr1v.Symbols == "MNQ" && KeystoneOpeningRange.Signals(fr1, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MGC").Count == 0 && KeystoneOpeningRange.Signals(frBoth, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MGC").Count > 0, "FVG RETRACE 1M: MNQ only by default (MGC lost money in the research) • INSTRUMENTS = BOTH gives MGC its setups");
        // ★ TPT 50K ROTATION preset: the studied plan (evaluation −$1,500 / +$1,520, PRO −$1,500 / +$1,500, plain 1-minute FVG) + TPT counts 3 days
        var tptR = KeystoneOpeningRange.Presets().First(x => x.Short == "FVGR-TPT"); var tptRules = KeystoneSimRules.Presets().First(x => x.Name == "TAKE PROFIT TRADER 50K");
        var tptS = KeystoneOpeningRange.Signals(tptR, fvgDay(new DateTime(2025, 3, 5), ofMins2), null, "MNQ");
        Check(KeystoneReplaySignals.Names[14].Contains("TPT") && tptR.VwapSide == 0 && tptR.FvgMinutes == 1 && tptS.Count > 0 && tptS.All(x => Eq(x.StopDist, 75) && Eq(x.TargetDist, 76) && Eq(x.FundStopDist, 75) && Eq(x.FundTargetDist, 75)) && tptRules.EvalMinDays == 3 && tptRules.FundDdType == "INTRADAY" && Eq(tptRules.PayoutBuffer, 2000),
            "TPT 50K ROTATION preset: evaluation stop 75 / target 76 pts (−$1,500 / +$1,520 on 10 MNQ), PRO 75 / 75 • TPT 50K: 3 days, PRO intraday trailing, $2,000 buffer", tptS.Count + " signals");
        // TPT PRO: trailing live until the buffer is made (best equity +$2,000 → the floor locks at the start), then withdrawals (≤ $10,000 a day, 80%)
        var tpS = new KeystoneSimSettings { Accounts = 1, StartAs = "FUNDED", Strategy = "STUDIO", BuyRule = "ALWAYS", Rules = tptRules.Copy() }; var tps = new KeystonePropSim(tpS); tps.BeginDay(new DateTime(2026, 4, 6));
        var pro = tps.Accounts[0]; tps.CheckEquity(pro, 1500); Check(pro.BufferOn == DateTime.MinValue && Eq(tps.Floor(pro), -500), "TPT PRO: open +$1,500 → the floor follows to −$500, no buffer yet");
        tps.CheckEquity(pro, 2100); Check(pro.BufferOn == new DateTime(2026, 4, 6) && Eq(tps.Floor(pro), 0) && tps.Events.Any(e => e.Kind == "BUFFER"), "TPT PRO: best equity +$2,100 → BUFFER MADE, the floor locked at the start (0)");
        Check(tptRules.PayoutCap == 10000 && tptRules.Split == 80 && tptRules.PayoutDays == 1 && tptRules.PayoutConsistency <= 0, "TPT PRO: no consistency rule, any day, up to $10,000 a day, 80% to you");
        var rRd = new KeystoneSimRotation(1); rRd.Done(new DateTime(2026, 4, 6, 10, 0, 0), 25); Check(rRd.ReadyAt == new DateTime(2026, 4, 6, 10, 0, 25) && new KeystoneSimSettings().AutoFallback == "BUY", "AUTO: the pause (25 s) ends at 10:00:25 — with the entry indicator and no setup, AUTO then takes the side set at the start (BUY BOTH by default)");
        // THE MONEY ROAD: max budget, streaks, the longest wait for a payout, the payout cycle
        var rsim = new KeystonePropSim(null); double rcs = 0, rcc = 0; var rd0 = new DateTime(2026, 1, 5);
        double[] spend = { 120, 120, 0, 120, 0, 0, 0 }, cash = { 0, 0, 0, 0, 1500, 1500, 0 };
        for (int i = 0; i < spend.Length; i++) { rcs += spend[i]; rcc += cash[i]; rsim.Days.Add(new KeystoneSimDay { Day = rd0.AddDays(i), CumSpent = rcs, CumCash = rcc, Payouts = cash[i] > 0 ? 1 : 0 }); }
        rsim.Accounts.Add(new KeystoneSimAccount { Id = 1, Bought = rd0, PassedOn = rd0.AddDays(2), Phase = "FUNDED", Payouts = 2, Cash = 3000 });
        rsim.Events.Add(new KeystoneSimEvent { Time = rd0.AddDays(4), Account = 1, Kind = "PAYOUT", Amount = 1500 });
        var road = KeystoneSimRoad.Of(rsim);
        Check(Eq(road.MaxBudget, 360) && road.MaxBudgetDay == rd0.AddDays(3) && road.LoseDays == 3 && Eq(road.LoseAmount, -360) && road.WinDays == 2 && Eq(road.WinAmount, 3000) && road.NoPayoutDays == 4 && road.PaidAccounts == 1 && Eq(road.AvgDaysToFirstPay, 2) && Eq(road.AvgPayout, 1500) && Eq(road.SpentBeforeFirstPay, 360),
            "MONEY ROAD: max budget $360 (day 4) • losing streak 3 days −$360 (a flat day keeps it) • winning 2 days +$3,000 • 4 days before the first payout • paid 2 days after passing", road.MaxBudget + " " + road.LoseDays + " " + road.WinDays + " " + road.NoPayoutDays + " " + road.AvgDaysToFirstPay + " " + road.SpentBeforeFirstPay);
        var rBuy = ofR.Copy(); rBuy.Side = 1; Check(KeystoneOpenFvg.Run(fvgDay(new DateTime(2025, 3, 6), ofMins3), rBuy, "MNQ").Dir == 0, "SIDE = buys only: a bearish first FVG means no trade that day");
        // 📒 MY TRADES POOL: tagged lines + the older journal (no double counting), days, breakdowns, AI prompt
        var mf1 = new KeystoneReplayFill { Symbol = "MNQ", Side = "BUY", Qty = 2, EntryTime = new DateTime(2026, 3, 10, 9, 41, 0), ExitTime = new DateTime(2026, 3, 10, 9, 52, 30), Entry = 100, Exit = 110, Points = 10, Net = 36.5, Reason = "TARGET" };
        var mf2 = new KeystoneReplayFill { Symbol = "MGC", Side = "SELL", Qty = 1, EntryTime = new DateTime(2026, 3, 11, 3, 5, 0), ExitTime = new DateTime(2026, 3, 11, 3, 9, 0), Entry = 2000, Exit = 2003, Points = -3, Net = -32.2, Reason = "STOP" };
        var mf0 = new KeystoneReplayFill { Symbol = "MNQ", Side = "SELL", Qty = 1, EntryTime = new DateTime(2026, 3, 9, 10, 0, 0), ExitTime = new DateTime(2026, 3, 9, 11, 30, 0), Entry = 100, Exit = 95, Points = 5, Net = 8.3, Reason = "MANUAL" };
        string mdir = Path.Combine(Path.GetTempPath(), "ks_mypool_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(mdir);
        string mtf = Path.Combine(mdir, "StudioTrades.csv"), mjf = Path.Combine(mdir, "ReplayJournal.csv");
        File.WriteAllLines(mtf, new[] { KeystoneManualPool.Header, KeystoneManualPool.Line(new DateTime(2026, 3, 10), mf1, "REGULAR", "I TRADE", "OPENING FVG, MNQ"), KeystoneManualPool.Line(new DateTime(2026, 3, 11), mf2, "PROP SIM", "AUTO + ME", "ROTATION") });
        File.WriteAllLines(mjf, new[] { KeystoneReplayTrader.JournalHeader, KeystoneReplayTrader.JournalLine(new DateTime(2026, 3, 9), mf0), KeystoneReplayTrader.JournalLine(new DateTime(2026, 3, 10), mf1), KeystoneReplayTrader.JournalLine(new DateTime(2026, 3, 11), mf2) });
        var mp = KeystoneManualPool.Load(mtf, mjf); try { Directory.Delete(mdir, true); } catch { }
        Check(mp.Count == 3 && mp[0].How == "BEFORE TAGS" && mp[1].Strategy == "OPENING FVG  MNQ" && mp[2].Mode == "PROP SIM" && Eq(mp[1].HoldMinutes, 11.5), "MY TRADES: tagged trades + the older journal once each (commas in names cleaned)", mp.Count + " " + string.Join("|", mp.Select(t => t.How + "/" + t.Strategy)));
        var mpd = KeystoneManualPool.Days(mp); var mps = KeystoneManualPool.By(mp, "INSTRUMENT"); var mpw = KeystoneManualPool.By(mp, "WEEKDAY");
        Check(mpd.Count == 3 && mpd[0].Item1 == new DateTime(2026, 3, 11) && mpd[0].Item3.StartsWith("MGC 1 −$32") && mps.Count == 2 && mps.First(r => r.Name == "MNQ").Trades == 2 && Eq(mps.First(r => r.Name == "MNQ").Net, 44.8) && mpw[0].Name == "MONDAY", "MY TRADES: every day (newest first, per instrument) and the breakdowns", mpd[0].Item3 + " • " + mpw[0].Name);
        var mst = KeystoneManualPool.Streaks(mp); Check(mst[0] == 2 && mst[1] == 1 && Eq(KeystoneManualPool.MaxDrawdown(mp), 32.2) && KeystoneManualPool.AiPrompt(mp).Contains("BY HOW") && KeystoneManualPool.Parse("garbage,1") == null, "MY TRADES: runs, drawdown, AI prompt, bad lines skipped");
        var noInd = isS.Copy(); noInd.Direction = "INDICATOR"; noInd.Indicator = ""; var nsim = KeystoneRotationLab.Run(noInd, testD, 930, 1555, null);
        Check(nsim.Trades.Count == 0, "SIDES = INDICATOR with no indicator: it waits (no trade)", nsim.Trades.Count.ToString());
        var cpR = isS.Copy(); cpR.Allocation = "ROTATE"; var rotRun = KeystoneRotationLab.Run(cpR, testD, 930, 1555, null); var cpC = isS.Copy(); cpC.Allocation = "COPY"; var copyRun = KeystoneRotationLab.Run(cpC, testD, 930, 1555, null);
        int rotAcctsDay1 = rotRun.Trades.Where(t => t.Day == testD[0].Day).Select(t => t.Account).Distinct().Count(), copyAcctsDay1 = copyRun.Trades.Where(t => t.Day == testD[0].Day).Select(t => t.Account).Distinct().Count();
        Check(copyRun.Trades.Count > rotRun.Trades.Count && copyAcctsDay1 == cpC.Accounts && copyRun.Trades.Any(t => t.Source.StartsWith("COPY of")), "COPY over real days: on day 1 all " + cpC.Accounts + " accounts trade (rotation: " + rotAcctsDay1 + ")", rotRun.Trades.Count + " vs " + copyRun.Trades.Count + " trades • day 1 accounts " + copyAcctsDay1);
        var ai = ind.AiPrompt(irows, testD[0].Day, testD[testD.Count - 1].Day);
        Check(ai.Contains("RULES") && ai.Contains("never seen in training") && ai.Contains("LUCKIEST") && KeystoneAiCoach.RequestFor(KeystoneEntryIndicator.AiSystem, ai).Contains("\"system\""), "INDICATOR: the AI review prompt carries the rules, the checks and the unseen-day test");
        bool stopNow = false; int seen = 0; var part = KeystoneRotationLab.Compare(cs, cdays, () => stopNow, (k, n) => { if (++seen == 2) stopNow = true; });
        Check(part.Count < rows.Count, "the comparison stops when asked", part.Count.ToString());
        // 📄 the HTML report of a run (+ the comparison)
        var hsim = KeystoneRotationLab.Run(cs, cdays, 1000, 1400, null); hsim.Folder = cfold;
        string html = KeystonePropSimReport.Html(hsim, rows);
        Check(html.StartsWith("<!doctype html>") && html.Contains("Prop Simulation Report") && html.Contains("CLEAR WINNER") == false && html.Contains("WINNER:") && html.Contains(System.Net.WebUtility.HtmlEncode(wn0.Name)) && html.Contains("ACCOUNTS") && html.Contains("EVERY DAY") && html.Contains("class='click win") && !html.Contains("NaN"), "the HTML report: summary, winner row, accounts, days", html.Length + " chars");
        Check(!KeystonePropSimReport.Html(hsim, null).Contains("WHAT IF"), "no comparison = no session table in the report");
        try { Directory.Delete(cfold, true); } catch { }
        // after the day's CLOSE ALL: the next day at my hours or from 18:00 (saved with the settings)
        var nd = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", NextDay = "1800", StartHhmm = 1800, EndHhmm = 1555 }; var nd2 = KeystoneSimSettings.FromText(nd.ToText());
        Check(nd2.NextDay == "1800" && nd2.StartHhmm == 1800 && new KeystoneSimSettings().NextDay == "HOURS" && nd.Describe().Contains("18:00"), "NEXT DAY setting saved / read; the default skips to my hours");

        // ---- the rotation driver
        var rot = new KeystoneSimRotation(7); var t0 = new DateTime(2026, 3, 2, 9, 30, 0);
        rot.Start(t0, 1, null, 300, 300, 10);
        Check(rot.Check(t0.AddSeconds(5), 320, 11, 0, null) == "", "target reached after 5 s: waits for the 11-second hold");
        Check(rot.Check(t0.AddSeconds(12), 320, 11, 0, null) == "TARGET", "target after 12 s → TARGET");
        rot.Start(t0, 1, null, 300, 300, 10);
        Check(rot.Check(t0.AddSeconds(30), -300, 11, 0, null) == "STOP", "−$300 → STOP (the stop never waits)");
        rot.Start(t0, 1, null, 300, 300, 10);
        rot.Check(t0.AddSeconds(30), 160, 0, 150, null);
        Check(rot.Check(t0.AddSeconds(40), 9, 0, 150, null) == "BREAKEVEN", "+$160 peak with breakeven at $150, back to +$9 → BREAKEVEN (stop at the fees)");
        rot.Start(t0, 1, null, 1000, 300, 10); var tiers = new KeystoneRotationConfig { LockTiers = "500:150" }.Tiers();
        rot.Check(t0.AddSeconds(30), 520, 0, 0, tiers);
        Check(rot.Check(t0.AddSeconds(40), 140, 0, 0, tiers) == "LOCK", "peak +$520, tier 500:150, back to +$140 → LOCK");
        // TRAILING STOP: −$500 stop, peak +$635 → the stop is +$135 and only moves up
        var trs = new KeystoneSimRotation(3) { TrailStop = true }; trs.Start(t0, 1, null, 790, 500, 40);
        Check(trs.Check(t0.AddSeconds(30), 635, 0, 0, null) == "" && Eq(trs.Floor, 135) && trs.Check(t0.AddSeconds(40), 400, 0, 0, null) == "" && Eq(trs.Floor, 135) && trs.Check(t0.AddSeconds(50), 130, 0, 0, null) == "TRAIL STOP",
            "TRAILING STOP: peak +$635 lifts the −$500 stop to +$135 (stays there when price pulls back) → out at +$130 = TRAIL STOP", trs.Floor.ToString());
        var plain = new KeystoneSimRotation(3); plain.Start(t0, 1, null, 790, 500, 40); plain.Check(t0.AddSeconds(30), 635, 0, 0, null);
        Check(plain.Check(t0.AddSeconds(50), 130, 0, 0, null) == "" && Eq(plain.Floor, -500), "TRAILING STOP OFF: the stop stays at −$500");
        Check(new KeystoneSimSettings().TrailStop, "TRAILING STOP is on by default in the prop simulation");
        var rr = new KeystoneSimRotation(1); int buys = 0; for (int i = 0; i < 400; i++) if (rr.PickDir("RANDOM", 0, 0) > 0) buys++;
        Check(buys > 160 && buys < 240, "RANDOM: about half buy-buy, half sell-sell", buys.ToString());
        Check(rr.PickDir("FOLLOW5", 5, 1) == 1 && rr.PickDir("FOLLOW5", 5, -1) == 0 && rr.PickDir("FADE5", -5, -1) == 1, "FOLLOW5 / FADE5 need both instruments to agree");
        // every MNQ / MGC combination
        Func<Tuple<int, int>, string> sd = d => (d.Item1 > 0 ? "B" : d.Item1 < 0 ? "S" : "0") + (d.Item2 > 0 ? "B" : d.Item2 < 0 ? "S" : "0");
        Check(sd(rr.PickDirs("BUY", 0, 0)) == "BB" && sd(rr.PickDirs("SELL", 0, 0)) == "SS" && sd(rr.PickDirs("BS", 0, 0)) == "BS" && sd(rr.PickDirs("SB", 0, 0)) == "SB", "fixed sides: BB / SS / BUY MNQ + SELL MGC / SELL MNQ + BUY MGC");
        var seen4 = new HashSet<string>(); for (int i = 0; i < 200; i++) seen4.Add(sd(rr.PickDirs("RANDOM4", 0, 0)));
        Check(seen4.SetEquals(new[] { "BB", "SS", "BS", "SB" }), "RANDOM4 gives all four combinations", string.Join(",", seen4));
        var seenH = new HashSet<string>(); for (int i = 0; i < 200; i++) seenH.Add(sd(rr.PickDirs("RANDOMHEDGE", 0, 0)));
        Check(seenH.SetEquals(new[] { "BS", "SB" }), "RANDOMHEDGE: always one bought, one sold", string.Join(",", seenH));
        var cyc = new KeystoneSimRotation(3); Check(string.Join(",", Enumerable.Range(0, 5).Select(i => sd(cyc.PickDirs("CYCLE4", 0, 0)))) == "BB,SS,BS,SB,BB", "CYCLE4 goes BB → SS → BS → SB in turn");
        Check(sd(rr.PickDirs("FOLLOW5EACH", 3, -2)) == "BS" && sd(rr.PickDirs("FADE5EACH", 3, -2)) == "SB" && sd(rr.PickDirs("FOLLOW5EACH", 0, 2)) == "0B", "FOLLOW / FADE 5 EACH: each instrument its own last 5 minutes");
        Check(sd(rr.PickDirs("NQLEAD5", 3, -2)) == "BB" && sd(rr.PickDirs("NQLEAD5", -3, 2)) == "SS" && sd(rr.PickDirs("NQLEAD5", 0, 2)) == "00" && KeystoneSimRotation.Arrows(1, -1) == "▲▼" && KeystoneSimRotation.Confirms("NQLEAD5") && !KeystoneSimRotation.Confirms("RANDOM"), "NQ LEADS: both follow MNQ's last 5 minutes (flat MNQ = wait); the signal arrows");
        rot.Start(t0, 1, -1, null, 300, 300, 10); rot.Check(t0.AddSeconds(5), 120, 0, 0, null); rot.Check(t0.AddSeconds(9), -80, 0, 0, null);
        Check(rot.Dir == 1 && rot.DirG == -1 && Eq(rot.Peak, 120) && Eq(rot.Worst, -80), "a BUY MNQ + SELL MGC trade keeps both sides, its peak and its worst point");
        rot.Done(t0.AddSeconds(20), 45); Check(rot.PauseUntil == t0.AddSeconds(65), "the pause after a trade is in seconds");

        // ---- saving and loading a run
        string folder = Path.Combine(Path.GetTempPath(), "keystone_propsim_test_" + Guid.NewGuid().ToString("N"));
        var s7 = new KeystoneSimSettings { MaxTradesPerDay = 0, FundedWhen = "WITH", DayPlan = "NONE", Accounts = 2, Strategy = "ROTATION", Direction = "RANDOM", MnqQty = 3, From = d1, To = d1.AddDays(9), Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("TRADEIFY")).Copy() };
        var simS = new KeystonePropSim(s7) { Folder = folder }; simS.Plan = KeystonePropSim.Weekdays(s7.From, s7.To);
        simS.BeginDay(simS.Plan[0]); Trade(simS, 30, 250); Trade(simS, 30, -120); simS.EndDay(); simS.PlanIndex = 1;
        simS.BeginDay(simS.Plan[1]); Trade(simS, 30, 600); simS.EndDay(); simS.PlanIndex = 2; simS.SaveState();
        var back = KeystonePropSim.Load(folder);
        Check(back != null && back.S.MnqQty == 3 && back.S.Rules.Name.StartsWith("TRADEIFY") && back.S.From == d1 && back.Accounts.Count == 2 && Eq(back.Accounts[0].Bal, simS.Accounts[0].Bal) && back.Days.Count == 2 && back.Trades.Count == 3 && back.PlanIndex == 2 && back.Plan.Count == simS.Plan.Count && back.Events.Count(e => e.Kind == "BOUGHT") == 2,
            "run saved (settings, accounts, days, trades, events, plan) and loaded back", back == null ? "null" : back.Accounts.Count + " " + back.Days.Count + " " + back.Trades.Count + " " + back.PlanIndex);
        back.BeginDay(back.Plan[back.PlanIndex]);
        Check(back.Active != null && back.Accounts.Count == 2, "a loaded run continues with the next day");
        Check(File.ReadAllLines(Path.Combine(folder, "days.csv")).Length == 3 && File.ReadAllLines(Path.Combine(folder, "trades.csv")).Length == 4, "days.csv / trades.csv written as the days go by");
        try { Directory.Delete(folder, true); } catch { }
        Check(KeystonePropSim.Weekdays(new DateTime(2026, 3, 6), new DateTime(2026, 3, 9)).Count == 2, "run days: weekdays only (Fri + Mon)");
        Check(KeystoneSimRules.Presets().All(x => x.Describe().Length > 40) && s7.Describe().Contains("RANDOM"), "rules and settings describe themselves");

        Console.WriteLine(failures == 0 ? "PROP SIM TESTS: ALL PASSED" : "PROP SIM TESTS: " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
