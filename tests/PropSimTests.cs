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
        var s = new KeystoneSimSettings { Accounts = 2, Strategy = "MANUAL", RotateBy = "TRADE", MaxLossesPerDay = 0 };
        s.Rules = KeystoneSimRules.Presets()[0].Copy(); s.Rules.MinHoldSeconds = 11; s.Rules.MinHoldShare = 50;
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
        var tpt = KeystoneSimRules.Presets().First(x => x.Name.StartsWith("TAKE PROFIT")); var s5 = new KeystoneSimSettings { Accounts = 1, ReplacePassed = false, Strategy = "MANUAL", MaxLossesPerDay = 0, Rules = tpt.Copy() };
        var simT = new KeystonePropSim(s5); simT.BeginDay(d1); var tf = simT.Active; tf.Phase = "FUNDED"; tf.WasPhase = "FUNDED"; simT.EndDay();
        simT.BeginDay(d1.AddDays(1)); Trade(simT, 30, 2100); simT.EndDay();
        Check(tf.Payouts == 0, "TPT: +$2,100 is only $100 above the buffer (< $250 minimum) → no payout");
        simT.BeginDay(d1.AddDays(2)); Trade(simT, 30, 400); simT.EndDay();
        Check(tf.Payouts == 1 && Eq(tf.Gross, 500) && Eq(tf.Cash, 400) && Eq(tf.Bal, 2000), "TPT: $500 above the buffer paid out, 80% = $400 to you", tf.Gross + " " + tf.Cash);

        // day limits: 2 losses lock the account for the day; an evaluation at its target stops trading today
        var s6 = new KeystoneSimSettings { Accounts = 1, Strategy = "ROTATION", MaxLossesPerDay = 2, Rules = KeystoneSimRules.Presets()[0].Copy() };
        var simL = new KeystonePropSim(s6); simL.BeginDay(d1); var l = simL.Active; Trade(simL, 30, -100); Check(simL.Active == l, "1 loss: still trading"); Trade(simL, 30, -100);
        Check(l.LockedToday && simL.Active == null, "2 losses: locked for the day");
        simL.EndDay(); simL.BeginDay(d1.AddDays(1)); Trade(simL, 30, 3300);
        Check(!l.LockedToday && simL.Active == l, "target reached, but this day is 106% of the profit (consistency) → it keeps trading");
        simL.EndDay(); simL.BeginDay(d1.AddDays(2));
        Check(l.Phase == "EVAL" && !l.LockedToday && simL.Active == l, "at the target but the best day (3,300) breaks consistency → it keeps trading to fix it", l.DayNote);
        Trade(simL, 30, 100);
        Check(simL.Active == l && !l.LockedToday, "still inconsistent after +$100 → not locked at the target");
        // consistent at the target but too few days: locked, then a day-count trade a day until the minimum days → PASSED
        var s8 = new KeystoneSimSettings { Accounts = 1, Strategy = "ROTATION", MaxLossesPerDay = 0, Rules = KeystoneSimRules.Presets()[0].Copy() }; s8.Rules.EvalMinDays = 4; s8.Rules.MinHoldSeconds = 0;
        var simM = new KeystonePropSim(s8); var m = (KeystoneSimAccount)null;
        foreach (var pnl in new[] { 1000.0, 1000, 1100 }) { simM.BeginDay(d1.AddDays(simM.Days.Count)); m = simM.Active; Trade(simM, 30, pnl); simM.EndDay(); }
        Check(m.Phase == "EVAL" && Eq(m.Bal, 3100) && m.LockedToday, "3,100 in 3 days (best 35%) but 4 days needed → locked at the target");
        simM.BeginDay(d1.AddDays(3));
        Check(m.LockedToday && m.DayNote == "DAY-COUNT TRADE" && simM.Active == null, "day 4: only the day-count trade (≈ the commission)");
        simM.EndDay();
        Check(m.Phase == "FUNDED" && m.EvalDays == 4, "day 4 counted → PASSED", m.Phase + " " + m.EvalDays);

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

        // ---- saving and loading a run
        string folder = Path.Combine(Path.GetTempPath(), "keystone_propsim_test_" + Guid.NewGuid().ToString("N"));
        var s7 = new KeystoneSimSettings { Accounts = 2, Strategy = "ROTATION", Direction = "RANDOM", MnqQty = 3, From = d1, To = d1.AddDays(9), Rules = KeystoneSimRules.Presets()[1].Copy() };
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
