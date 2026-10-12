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
        var s = new KeystoneSimSettings { DayPlan = "NONE", BuyRule = "ALWAYS", Accounts = 2, Strategy = "MANUAL", RotateBy = "TRADE", MaxLossesPerDay = 0 };
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
        Check(new KeystoneSimSettings().BuyRule == "BREAKEVEN", "the default: new evaluations only after the payouts pay back what was spent");
        var sBE = s3.Copy(); sBE.BuyRule = "BREAKEVEN"; sBE.Accounts = 2; var simE = new KeystonePropSim(sBE); simE.BeginDay(d1);
        Check(simE.Accounts.Count == 2 && Eq(simE.Spent, 240), "day 1: the budget buys the first 2 evaluations");
        var e1 = simE.Active; simE.AddFill(e1, -2010); simE.EndTrade(e1, T(d1, 60, -2010)); simE.EndDay();
        simE.BeginDay(d1.AddDays(1));
        Check(simE.Accounts.Count == 2 && Eq(simE.Spent, 240) && Eq(simE.ToBreakeven, 240) && simE.Events.Count(x => x.Kind == "WAITING") == 1, "one blown: NOT replaced while the run is $240 under water (waiting noted once)", simE.Accounts.Count + " " + simE.ToBreakeven);
        simE.EndDay(); simE.BeginDay(d1.AddDays(2));
        Check(simE.Events.Count(x => x.Kind == "WAITING") == 1, "the wait is noted once, not every day");
        simE.Accounts[1].Cash += 300; simE.EndDay(); simE.BeginDay(d1.AddDays(3));   // a payout of $300 to you (net +$60)
        Check(simE.Accounts.Count == 3 && Eq(simE.Spent, 360), "paid back (net ≥ 0) → the pool is refilled", simE.Accounts.Count + " " + simE.Spent);
        // TODAY: traded / at the day target / locked / passed / blown, and each account's day
        var sT = new KeystoneSimSettings { DayPlan = "ONEWIN", BuyRule = "ALWAYS", Accounts = 3, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
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

        // TPT-like: daily payouts above a $2,000 buffer, min $250, 80% split
        var tpt = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("TAKE PROFIT")); var s5 = new KeystoneSimSettings { DayPlan = "NONE", Accounts = 1, ReplacePassed = false, Strategy = "MANUAL", MaxLossesPerDay = 0, Rules = tpt.Copy() };
        var simT = new KeystonePropSim(s5); simT.BeginDay(d1); var tf = simT.Active; tf.Phase = "FUNDED"; tf.WasPhase = "FUNDED"; simT.EndDay();
        simT.BeginDay(d1.AddDays(1)); Trade(simT, 30, 2100); simT.EndDay();
        Check(tf.Payouts == 0, "TPT: +$2,100 is only $100 above the buffer (< $250 minimum) → no payout");
        simT.BeginDay(d1.AddDays(2)); Trade(simT, 30, 400); simT.EndDay();
        Check(tf.Payouts == 1 && Eq(tf.Gross, 500) && Eq(tf.Cash, 400) && Eq(tf.Bal, 2000), "TPT: $500 above the buffer paid out, 80% = $400 to you", tf.Gross + " " + tf.Cash);

        // day limits: 2 losses lock the account for the day; an evaluation at its target stops trading today
        var s6 = new KeystoneSimSettings { DayPlan = "NONE", Accounts = 1, Strategy = "ROTATION", RotTarget = 5000, MaxLossesPerDay = 2, Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy() };
        var simL = new KeystonePropSim(s6); simL.BeginDay(d1); var l = simL.Active; Trade(simL, 30, -100); Check(simL.Active == l, "1 loss: still trading"); Trade(simL, 30, -100);
        Check(l.LockedToday && simL.Active == null, "2 losses: locked for the day");
        simL.EndDay(); simL.BeginDay(d1.AddDays(1)); Trade(simL, 30, 3300);
        Check(!l.LockedToday && simL.Active == l, "target reached, but this day is 106% of the profit (consistency) → it keeps trading");
        simL.EndDay(); simL.BeginDay(d1.AddDays(2));
        Check(l.Phase == "EVAL" && !l.LockedToday && simL.Active == l, "at the target but the best day (3,300) breaks consistency → it keeps trading to fix it", l.DayNote);
        Trade(simL, 30, 100);
        Check(simL.Active == l && !l.LockedToday, "still inconsistent after +$100 → not locked at the target");
        // consistent at the target but too few days: locked, then a day-count trade a day until the minimum days → PASSED
        var s8 = new KeystoneSimSettings { DayPlan = "NONE", Accounts = 1, Strategy = "ROTATION", MaxLossesPerDay = 0, Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy() }; s8.Rules.EvalMinDays = 4; s8.Rules.MinHoldSeconds = 0;
        var simM = new KeystonePropSim(s8); var m = (KeystoneSimAccount)null;
        foreach (var pnl in new[] { 1000.0, 1000, 1100 }) { simM.BeginDay(d1.AddDays(simM.Days.Count)); m = simM.Active; Trade(simM, 30, pnl); simM.EndDay(); }
        Check(m.Phase == "EVAL" && Eq(m.Bal, 3100) && m.LockedToday, "3,100 in 3 days (best 35%) but 4 days needed → locked at the target");
        simM.BeginDay(d1.AddDays(3));
        Check(m.LockedToday && m.DayNote == "DAY-COUNT TRADE" && simM.Active == null, "day 4: only the day-count trade (≈ the commission)");
        simM.EndDay();
        Check(m.Phase == "FUNDED" && m.EvalDays == 4, "day 4 counted → PASSED", m.Phase + " " + m.EvalDays);

        // ---- each account's day: ONE WIN A DAY (−500, −500, +1,500 → +500 locked) and FULL RECOVERY (locked at the day's +target)
        var sw = new KeystoneSimSettings { DayPlan = "ONEWIN", Accounts = 1, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simW = new KeystonePropSim(sw); simW.BeginDay(d1);
        var w1 = Trade(simW, 30, -500); Trade(simW, 30, -500);
        Check(!w1.LockedToday && simW.Active == w1, "ONE WIN A DAY: two losses, still trading");
        Trade(simW, 30, 1440);
        Check(w1.LockedToday && Eq(w1.DayPnl, 440) && simW.Active == null, "ONE WIN A DAY: the first win (+1,440 after −1,000) locks the day at +440", w1.DayNote);
        simW.EndDay();
        var sr = new KeystoneSimSettings { DayPlan = "RECOVER", Accounts = 1, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simR = new KeystonePropSim(sr); simR.BeginDay(d1);
        var r1 = Trade(simR, 30, -500); Trade(simR, 30, 600);
        Check(!r1.LockedToday, "FULL RECOVERY: −500 then +600 = +100, still short of +1,500 → keeps trading");
        Trade(simR, 30, 1380);
        Check(r1.LockedToday && Eq(r1.DayPnl, 1480), "FULL RECOVERY: +1,480 for the day (≈ the target, fees in) → locked", r1.DayNote);
        simR.EndDay();

        // ---- the rotation on 1-minute bars (the what-if engine): a day never ends above its target; the stop is checked first
        var bs = new KeystoneSimSettings { DayPlan = "NONE", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var ba = new KeystoneSimAccount { Id = 1, Phase = "EVAL" }; double bt, bst;
        ba.DayPnl = 1000; KeystoneRotationLab.Bracket(bs, ba, 30, 3000, out bt, out bst);
        Check(Eq(bt, 530) && Eq(bst, 500), "UNTIL THE DAY TARGET: +1,000 made today → the next trade aims at the 500 left (+ fees)", bt + " / " + bst);
        // funded accounts: their own day plan / trades a day, and (AFTER) no trading while an evaluation is still running
        var sf = new KeystoneSimSettings { DayPlan = "NONE", BuyRule = "ALWAYS", ReplacePassed = false, Accounts = 2, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, FundMaxTrades = 1, FundedWhen = "AFTER", Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simF2 = new KeystonePropSim(sf); simF2.BeginDay(d1); simF2.Accounts[0].Phase = "FUNDED"; simF2.Accounts[0].WasPhase = "FUNDED"; simF2.Rotate();
        Check(simF2.Active != null && simF2.Active.Phase == "EVAL" && !simF2.CanTrade(simF2.Accounts[0]), "AFTER: the funded account waits while an evaluation is still running", simF2.Active == null ? "none" : simF2.Active.Name);
        sf.FundedWhen = "WITH"; Check(simF2.CanTrade(simF2.Accounts[0]), "WITH: funded trade alongside the evaluations");
        var fa = simF2.Accounts[0]; simF2.Active = fa; simF2.AddFill(fa, -100); simF2.EndTrade(fa, T(d1, 60, -100));
        Check(fa.LockedToday && fa.DayNote.Contains("funded"), "funded max 1 trade a day → locked after its trade", fa.DayNote);
        var ev = simF2.Accounts[1]; ev.Bal = 1500; ev.BestDay = 1500; ev.Days = 1; simF2.Active = ev; simF2.AddFill(ev, 1500); simF2.EndTrade(ev, T(d1, 60, 1500));
        Check(simF2.PassesAtClose(ev) && simF2.DaySummary(d1).WillPass == 1, "an evaluation at its target today counts as PASSED (funded at the close)");
        var fresh = new KeystoneSimAccount { Id = 2, Phase = "EVAL" }; double ft, fst; KeystoneRotationLab.Bracket(bs, fresh, 30, 3000, out ft, out fst);
        Check(Eq(ft, 1530), "the $ target is net of the fees: +1,500 day = 1,530 gross with $30 fees", ft.ToString());
        var lucid2 = new KeystoneSimSettings { DayPlan = "ONEWIN", BuyRule = "ALWAYS", Accounts = 1, Strategy = "ROTATION", RotTarget = 1500, RotStop = 500, Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("LUCID")).Copy() }; lucid2.Rules.MinHoldSeconds = 0;
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
        var wsim = KeystoneRotationLab.Run(new KeystoneSimSettings { DayPlan = "NONE", Accounts = 2, Direction = "BUY", MnqQty = 1, MgcQty = 1, RotTarget = 100, RotStop = 100, PauseSeconds = 60, Rules = KeystoneSimRules.Presets()[0].Copy() }, wdays, 930, 1029, null);
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
        var cs = new KeystoneSimSettings { DayPlan = "ONEWIN", Accounts = 3, Direction = "RANDOM", MnqQty = 2, MgcQty = 2, FundMnqQty = 2, FundMgcQty = 2, RotTarget = 300, RotStop = 300, FundRotTarget = 300, FundRotStop = 300, StartHhmm = 1000, EndHhmm = 1400, Rules = KeystoneSimRules.Presets()[0].Copy() };
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
        bool stopNow = false; int seen = 0; var part = KeystoneRotationLab.Compare(cs, cdays, () => stopNow, (k, n) => { if (++seen == 2) stopNow = true; });
        Check(part.Count < rows.Count, "the comparison stops when asked", part.Count.ToString());
        // 📄 the HTML report of a run (+ the comparison)
        var hsim = KeystoneRotationLab.Run(cs, cdays, 1000, 1400, null); hsim.Folder = cfold;
        string html = KeystonePropSimReport.Html(hsim, rows);
        Check(html.StartsWith("<!doctype html>") && html.Contains("Prop Simulation Report") && html.Contains("CLEAR WINNER") == false && html.Contains("WINNER:") && html.Contains(System.Net.WebUtility.HtmlEncode(wn0.Name)) && html.Contains("ACCOUNTS") && html.Contains("EVERY DAY") && html.Contains("class='click win") && !html.Contains("NaN"), "the HTML report: summary, winner row, accounts, days", html.Length + " chars");
        Check(!KeystonePropSimReport.Html(hsim, null).Contains("WHAT IF"), "no comparison = no session table in the report");
        try { Directory.Delete(cfold, true); } catch { }
        // after the day's CLOSE ALL: the next day at my hours or from 18:00 (saved with the settings)
        var nd = new KeystoneSimSettings { NextDay = "1800", StartHhmm = 1800, EndHhmm = 1555 }; var nd2 = KeystoneSimSettings.FromText(nd.ToText());
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
        var s7 = new KeystoneSimSettings { DayPlan = "NONE", Accounts = 2, Strategy = "ROTATION", Direction = "RANDOM", MnqQty = 3, From = d1, To = d1.AddDays(9), Rules = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("TRADEIFY")).Copy() };
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
