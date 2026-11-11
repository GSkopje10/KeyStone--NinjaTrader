// 🎯 3-SETUP ROTATION (BUILD 2026-11-11o): the setup detectors, the bracket outcome and the 30-account rotation on hand-made bars (research only, nothing is sent).
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class ThreeSetupTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Eq(double a, double b, double tol = 1e-6) { return Math.Abs(a - b) <= tol; }
    static readonly DateTime D0 = new DateTime(2025, 3, 10);   // a Monday
    // one 1-minute bar closing at hh:mm (NY)
    static KeystoneArcBar M(DateTime day, int hh, int mm, double o, double h, double l, double c) { return new KeystoneArcBar { Symbol = "MNQ", Time = day.AddHours(hh).AddMinutes(mm), Open = o, High = h, Low = l, Close = c, Volume = 10 }; }
    // a 5-minute candle (o, h, l, c) as five 1-minute bars ending at end (the high first, then the low, then the close)
    static IEnumerable<KeystoneArcBar> Five(DateTime end, double o, double h, double l, double c)
    {
        var t0 = end.AddMinutes(-5);
        yield return new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(1), Open = o, High = Math.Max(o, h), Low = Math.Min(o, Math.Max(l, Math.Min(o, c))), Close = h };
        yield return new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(2), Open = h, High = h, Low = l, Close = l };
        yield return new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(3), Open = l, High = Math.Max(l, c), Low = l, Close = c };
        yield return new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(4), Open = c, High = c, Low = c, Close = c };
        yield return new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(5), Open = c, High = c, Low = c, Close = c };
    }
    static List<KeystoneArcBar> Flat(DateTime day, int fromH, int fromM, int toH, int toM, double px)
    {
        var o = new List<KeystoneArcBar>(); for (var t = day.AddHours(fromH).AddMinutes(fromM); t <= day.AddHours(toH).AddMinutes(toM); t = t.AddMinutes(1)) o.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = px, High = px + 0.5, Low = px - 0.5, Close = px }); return o;
    }

    public static int Main()
    {
        var s = new KeystoneTriSettings { Window = "NY" }.ApplyWindow(); double cost = s.Costs;
        Check(Eq(s.StopPts, 16.75) && Eq(s.TargetPts, 50) && Eq(cost, 15 * KeystoneMoveStudy.Cost("MNQ")), "15 MNQ: $500 → 16.75 pts, $1,500 → 50.00 pts (0.25 tick), costs 15 × the per-micro cost", s.StopPts + " / " + s.TargetPts + " / " + cost);
        Check(s.Rules.Name == "LUCID FLEX 50K" && Eq(s.Rules.Target, 3000) && s.Rules.EvalMinDays == 2 && s.Rules.PayoutDays == 5 && Eq(s.Rules.QualifyingDay, 150) && Eq(s.Rules.PayoutCap, 2000) && Eq(s.Rules.Split, 90) && s.Rules.MaxPayouts == 5 && Eq(s.Rules.PayoutMin, 250), "rules: Lucid Flex 50K (+$3,000, 2 days, 5 × $150 days, 50 % capped $2,000, 90 %, max 5) + the firm book's $250 minimum payout");
        Check(KeystoneTriSettings.Off(1800) == 0 && KeystoneTriSettings.Off(930) == 930 && new KeystoneTriSettings { Window = "ASIA" }.ApplyWindow().OffEnd == 480 && new KeystoneTriSettings { Window = "ALLDAY" }.ApplyWindow().OffFlat == 1365, "window offsets from 18:00 (ASIA ends at +480 min, ALL DAY flat at the firm's 16:45)");

        // ---- 1. FVG: a bullish gap 101 → 105 (50 % = 103): filled when a minute trades to 103, not when the retrace stops at 104
        Func<double, List<KeystoneArcBar>> fvgDay = retraceLow =>
        {
            var b = Flat(D0, 9, 1, 9, 30, 100);
            b.AddRange(Five(D0.AddHours(9).AddMinutes(35), 100, 101, 99, 100.5));   // c1 high 101
            b.AddRange(Five(D0.AddHours(9).AddMinutes(40), 100.5, 110, 100.5, 109)); // c2
            b.AddRange(Five(D0.AddHours(9).AddMinutes(45), 109, 112, 105, 111));     // c3 low 105 → gap 101–105
            b.Add(M(D0, 9, 46, 111, 111, retraceLow, retraceLow + 1)); b.Add(M(D0, 9, 47, retraceLow + 1, 130, retraceLow + 1, 130)); b.Add(M(D0, 9, 48, 130, 160, 130, 160));
            b.AddRange(Flat(D0, 9, 49, 16, 0, 160)); return b;
        };
        var d1 = KeystoneTriData.From(fvgDay(103)); var f1 = KeystoneTriSetup.Fvg(d1, s);
        Check(f1.Count == 1 && f1[0].Dir == 1 && Eq(f1[0].EntryPx, 103) && f1[0].Limit && f1[0].Entry == D0.AddHours(9).AddMinutes(46), "FVG bull: the limit at 50 % (103) fills when a 1-minute low reaches it (9:46)", string.Join(";", f1.Select(c => c.Entry + " " + c.EntryPx)));
        var x1 = KeystoneTriSetup.Resolve(d1, s, f1[0], double.PositiveInfinity);
        Check(x1.Reason == "TARGET" && Eq(x1.ExitPx, 153) && Eq(x1.Pnl, 1500 - cost), "… and reaches its target 103 + 50 = 153 → +$1,500 − costs", x1.Reason + " " + x1.Pnl);
        var d2 = KeystoneTriData.From(fvgDay(104)); var f2 = KeystoneTriSetup.Fvg(d2, s);
        Check(f2.Count == 0, "FVG never retraced to 50 % (low 104) → no trade", f2.Count.ToString());
        var bear = new List<KeystoneArcBar>(Flat(D0, 9, 1, 9, 30, 200)); bear.AddRange(Five(D0.AddHours(9).AddMinutes(35), 200, 201, 199, 199.5)); bear.AddRange(Five(D0.AddHours(9).AddMinutes(40), 199.5, 199.5, 190, 191)); bear.AddRange(Five(D0.AddHours(9).AddMinutes(45), 191, 195, 188, 189));
        bear.Add(M(D0, 9, 46, 189, 197, 189, 196)); bear.AddRange(Flat(D0, 9, 47, 16, 0, 196));
        var fb = KeystoneTriSetup.Fvg(KeystoneTriData.From(bear), s);
        Check(fb.Count == 1 && fb[0].Dir == -1 && Eq(fb[0].EntryPx, 197) && Eq(fb[0].StopPx, 213.75), "FVG bear mirror: gap 195–199 → SELL limit 197, stop 213.75", string.Join(";", fb.Select(c => c.Dir + " " + c.EntryPx + " " + c.StopPx)));
        var outW = new KeystoneTriSettings { Window = "NYAM" }.ApplyWindow(); outW.StartHhmm = 1000; Check(KeystoneTriSetup.Fvg(d1, outW).Count == 0, "a gap whose candles close before the window starts is not traded");

        // ---- 2. AGGRESSION BREAK: 20 quiet candles (range 2), then a big green candle closing above their high
        Func<double, double, List<KeystoneArcBar>> aggDay = (hi, close) =>
        {
            var b = new List<KeystoneArcBar>(); var t = D0.AddHours(7).AddMinutes(55);
            for (int k = 0; k < 20; k++) { t = t.AddMinutes(5); double o = k % 2 == 0 ? 100 : 101; b.AddRange(Five(t, o, 102, 100, 201 - o)); }
            t = t.AddMinutes(5); b.AddRange(Five(t, 101, hi, 100.5, close)); b.AddRange(Flat(D0, t.Hour, t.Minute + 1, 16, 0, close)); return b;
        };
        var da = KeystoneTriData.From(aggDay(112, 111.5)); var ag = KeystoneTriSetup.Aggression(da, s);
        Check(ag.Count == 1 && ag[0].Dir == 1 && Eq(ag[0].EntryPx, 111.5) && ag[0].Setup == "AGG" && ag[0].Signal == D0.AddHours(9).AddMinutes(40), "AGG bull: close above the 10-candle high, body 91 %, range 5.75 × ATR → BUY at its close 111.5 (9:40)", string.Join(";", ag.Select(c => c.Signal + " " + c.EntryPx)));
        Check(KeystoneTriSetup.Aggression(KeystoneTriData.From(aggDay(112, 103)), s).Count == 0, "AGG: a weak body (close 103 of a 100.5–112 candle) is no aggression");
        Check(KeystoneTriSetup.Aggression(KeystoneTriData.From(aggDay(103, 102.5)), s).Count == 0, "AGG: a small candle (range < 1.5 × ATR) is no aggression");

        // ---- 3. 123 ENGULFING: two red candles, then a green one closing above the last red's open
        Func<int, List<KeystoneArcBar>> engDay = reds =>
        {
            var b = new List<KeystoneArcBar>(Flat(D0, 9, 1, 9, 30, 100)); var t = D0.AddHours(9).AddMinutes(30); double px = 100;
            b.AddRange(Five(t = t.AddMinutes(5), px, px + 3, px - 0.5, px + 2)); px += 2;   // green
            for (int k = 0; k < reds; k++) { b.AddRange(Five(t = t.AddMinutes(5), px, px + 0.5, px - 3, px - 2)); px -= 2; }
            b.AddRange(Five(t = t.AddMinutes(5), px, px + 5, px - 0.5, px + 4.5)); b.AddRange(Flat(D0, t.Hour, t.Minute + 1, 16, 0, px + 4.5)); return b;
        };
        var en = KeystoneTriSetup.Engulfing(KeystoneTriData.From(engDay(2)), s);
        Check(en.Count == 1 && en[0].Dir == 1 && Eq(en[0].EntryPx, 102.5) && en[0].Signal == D0.AddHours(9).AddMinutes(50), "123 ENGULFING bull: 2 red, then a green close (102.5) above the last red's open (100) → BUY at 9:50", string.Join(";", en.Select(c => c.Signal + " " + c.EntryPx)));
        Check(KeystoneTriSetup.Engulfing(KeystoneTriData.From(engDay(1)), s).Count == 0, "123 ENGULFING: one red candle is under the minimum run (2)");

        // ---- 4. the bracket outcome: both touched in a minute = STOP (+1 tick), the target alone, flat at the window's end, the floor
        var bb = new List<KeystoneArcBar>(Flat(D0, 9, 31, 9, 40, 100)); bb.Add(M(D0, 9, 41, 100, 151, 83, 120)); bb.AddRange(Flat(D0, 9, 42, 16, 55, 120));
        var db = KeystoneTriData.From(bb); int ei = db.LastAtOrBefore(D0.AddHours(9).AddMinutes(40).Ticks);
        var cnd = new KeystoneTriCandidate { Setup = "AGG", Dir = 1, Day = D0, Entry = D0.AddHours(9).AddMinutes(40), EntryIdx = ei, EntryPx = 100, StopPx = 100 - s.StopPts, TargetPx = 150 };
        var xb = KeystoneTriSetup.Resolve(db, s, cnd, double.PositiveInfinity);
        Check(xb.Reason == "STOP (BOTH)" && xb.Both && Eq(xb.ExitPx, 83) && Eq(xb.Pnl, -17 * 30 - cost), "a minute touching both stop and target = STOP, filled 1 tick worse (83.00) → −$510 − costs", xb.Reason + " " + xb.ExitPx + " " + xb.Pnl);
        var ct = new KeystoneTriCandidate { Setup = "AGG", Dir = 1, Day = D0, Entry = cnd.Entry, EntryIdx = ei, EntryPx = 100, StopPx = 80, TargetPx = 150 };
        var xt = KeystoneTriSetup.Resolve(db, s, ct, double.PositiveInfinity); Check(xt.Reason == "TARGET" && Eq(xt.Pnl, 1500 - cost) && xt.Exit == D0.AddHours(9).AddMinutes(41), "the target alone → +$1,500 − costs at 9:41", xt.Reason + " " + xt.Pnl);
        var cf = new KeystoneTriCandidate { Setup = "AGG", Dir = 1, Day = D0, Entry = cnd.Entry, EntryIdx = ei, EntryPx = 100, StopPx = 50, TargetPx = 200 };
        var xf = KeystoneTriSetup.Resolve(db, s, cf, double.PositiveInfinity); Check(xf.Reason == "FLAT" && xf.Exit == D0.AddHours(16) && Eq(xf.Pnl, 20 * 30 - cost), "neither → flat at the window's end (16:00 close 120)", xf.Reason + " " + xf.Exit + " " + xf.Pnl);
        var xl = KeystoneTriSetup.Resolve(db, s, cnd, 300); Check(xl.Liquidated && xl.Reason == "FLOOR" && Eq(xl.Pnl, -300), "an account with $300 of room is liquidated at its floor (−$300) before the stop", xl.Reason + " " + xl.Pnl);
        var aw = new KeystoneTriSettings { Window = "ALLDAY" }.ApplyWindow(); var xa = KeystoneTriSetup.Resolve(db, aw, cf, double.PositiveInfinity); Check(xa.Exit == D0.AddHours(16).AddMinutes(45), "ALL DAY: flat at the firm's flat-by time (16:45)", xa.Exit.ToString());

        // ---- 5. the rotation: a steady rise of 1 point a minute; a BUY every 10 minutes wins after 50 minutes — one trade at a time
        Func<int, List<KeystoneArcBar>> rise = days =>
        {
            var o = new List<KeystoneArcBar>(); var dy = D0;
            for (int k = 0; k < days; k++, dy = dy.AddDays(dy.DayOfWeek == DayOfWeek.Friday ? 3 : 1)) { double px = 1000; for (var t = dy.AddHours(9).AddMinutes(31); t <= dy.AddHours(16); t = t.AddMinutes(1), px += 1) o.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = px - 1, High = px, Low = px - 1.5, Close = px }); }
            return o;
        };
        Func<KeystoneTriData, int, List<KeystoneTriCandidate>> every = (d, step) => { var o = new List<KeystoneTriCandidate>(); for (int i = 0; i < d.N; i += step) { if (d.Off[i] - 1 < s.OffStart || d.Off[i] >= s.OffEnd) continue; double px = d.C[i]; o.Add(new KeystoneTriCandidate { Setup = "AGG", Dir = 1, Day = d.Session[i], Signal = new DateTime(d.T[i]), Entry = new DateTime(d.T[i]), EntryIdx = i, EntryPx = px, StopPx = px - s.StopPts, TargetPx = px + s.TargetPts }); } return o; };
        var dr = KeystoneTriData.From(rise(1)); var cr = every(dr, 10);
        var sr = s.Copy(); sr.ConsistencyGuard = false; var rr = KeystoneTriRotation.Run(dr, cr, sr, D0, D0);
        Check(rr.Trades.Count >= 5 && rr.Trades.Select(t => t.Account).Take(5).SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "ROTATE: each trade goes to the next account (1, 2, 3, 4, 5 …)", string.Join(",", rr.Trades.Select(t => t.Account)));
        Check(rr.Trades.Zip(rr.Trades.Skip(1), (a, b) => b.Entry >= a.Exit).All(z => z) && rr.SkippedBusy > 0 && rr.Trades.Take(rr.Trades.Count - 1).All(t => t.Reason == "TARGET"), "one trade at a time: a signal while in a trade is skipped (" + rr.SkippedBusy + " skipped)");
        Check(rr.Batches.Count == 1 && rr.Batches[0].Accounts == 30 && Eq(rr.Batches[0].Cost, 30 * 120), "the batch: 30 evaluations bought on its first session ($3,600)", rr.Batches.Count + " " + rr.Batches[0].Accounts + " " + rr.Batches[0].Cost);
        // STAY: the same account takes every trade until it is done
        var ss = s.Copy(); ss.Mode = "STAY"; ss.ConsistencyGuard = false; var r3 = KeystoneTriRotation.Run(dr, cr, ss, D0, D0);
        Check(r3.Trades.Count >= 3 && r3.Trades.All(t => t.Account == 1), "STAY: account 1 takes every trade of the day (no consistency guard: it keeps going, best day too big to pass)", string.Join(",", r3.Trades.Select(t => t.Account)));
        // STAY with the consistency guard: one win a day (a 2nd win would make the day > 50 % of $3,000) → the next account takes over that day
        var dr3 = KeystoneTriData.From(rise(3)); var cr3 = every(dr3, 10); var ss2 = s.Copy(); ss2.Mode = "STAY"; var r4 = KeystoneTriRotation.Run(dr3, cr3, ss2, D0, D0.AddDays(10));
        var day1 = r4.Trades.Where(t => t.Day == D0).Select(t => t.Account).ToList(); var acc1 = r4.Trades.Where(t => t.Account == 1).ToList();
        Check(day1.SequenceEqual(new[] { 1 }) && r4.SkippedNoAccount > 0, "STAY + the consistency day cap: account 1 stops after its win (a 2nd would make the day > 50 % of $3,000) and the day's other signals WAIT for it", string.Join(",", day1) + " skipped " + r4.SkippedNoAccount);
        Check(acc1.Count == 3 && acc1.Select(t => t.Day).Distinct().Count() == 3 && r4.Trades.All(t => t.Account == 1), "2 wins (+$2,948) are under $3,000 → account 1 trades a 3rd day, 3 wins on 3 days", string.Join(",", acc1.Select(t => t.Day.ToString("MM-dd") + " " + t.Pnl.ToString("0"))));
        Check(r4.Batches[0].Passed == 1 && r4.Batches[0].End == r4.Trades.Last().Day, "… and passes at the close of its 3rd day (+$4,422, best day 33 %) → the rotation moves on the next session", r4.Batches[0].Passed.ToString());
        var dr5 = KeystoneTriData.From(rise(5)); var r7 = KeystoneTriRotation.Run(dr5, every(dr5, 10), ss2, D0, D0.AddDays(10));
        Check(r7.Trades.Where(t => t.Day == D0.AddDays(3)).All(t => t.Account == 2) && r7.Trades.Any(t => t.Day == D0.AddDays(3)), "STAY: the day after account 1 passed, account 2 takes every signal", string.Join(",", r7.Trades.Select(t => t.Day.ToString("dd") + ":" + t.Account)));
        var sn = ss2.Copy(); sn.Mode = "STAYNEXT"; var r6 = KeystoneTriRotation.Run(dr3, cr3, sn, D0, D0.AddDays(10)); var d1n = r6.Trades.Where(t => t.Day == D0).Select(t => t.Account).ToList();
        Check(d1n.Take(3).SequenceEqual(new[] { 1, 2, 3 }), "STAYNEXT: on the stopped day the next account takes over (1, 2, 3 …)", string.Join(",", d1n));
        // STAY: a blown account hands over to the next one — every trade loses (a fall)
        var fall = new List<KeystoneArcBar>(); var dd = D0; for (int k = 0; k < 3; k++, dd = dd.AddDays(1)) { double px = 5000; for (var t = dd.AddHours(9).AddMinutes(31); t <= dd.AddHours(16); t = t.AddMinutes(1), px -= 1) fall.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = px + 1, High = px + 1.5, Low = px, Close = px }); }
        var df = KeystoneTriData.From(fall); var r5 = KeystoneTriRotation.Run(df, every(df, 20), ss2, D0, D0.AddDays(3));
        var seq = r5.Trades.Select(t => t.Account).ToList(); int firstOther = seq.FindIndex(a => a != 1);
        Check(firstOther == 4 && seq[firstOther] == 2 && r5.Trades[firstOther - 1].Liquidated && r5.Batches[0].BlownEval >= 1, "STAY: account 1 takes 3 stops, the 4th is liquidated at its floor (−$2,000), then account 2 takes over", string.Join(",", seq.Take(12)));
        Check(r5.Trades.Where(t => t.Account == 1).Sum(t => t.Pnl) <= -2000 + 1e-6, "… account 1 lost its $2,000 drawdown", r5.Trades.Where(t => t.Account == 1).Sum(t => t.Pnl).ToString("0"));

        // ---- 6. the account lifecycle (KeystonePropSim with the study's settings): an eval passes only on its 2nd day; a payout after 5 days ≥ $150
        var sim = KeystoneTriRotation.NewBatch(s); var day = D0; sim.BeginDay(day); var a1 = sim.Accounts[0];
        sim.AddFill(a1, 1550); sim.EndTrade(a1, new KeystoneSimTrade { Entry = day.AddHours(10), Exit = day.AddHours(11), Pnl = 1550, Dir = 1 }); sim.EndDay();
        Check(a1.Phase == "EVAL" && sim.Accounts.Count == 30, "day 1 +$1,550: EVAL", a1.Phase);
        day = day.AddDays(1); sim.BeginDay(day); sim.S.Accounts = 1; sim.AddFill(a1, 1550); sim.EndTrade(a1, new KeystoneSimTrade { Entry = day.AddHours(10), Exit = day.AddHours(11), Pnl = 1550, Dir = 1 }); sim.EndDay();
        Check(a1.Phase == "FUNDED" && a1.PassedOn == day && sim.Accounts.Count == 30, "day 2 +$1,550: $3,100 in 2 days, best day 50 % → PASSED → FUNDED (no new account bought)", a1.Phase + " " + a1.PassedOn);
        var simB = KeystoneTriRotation.NewBatch(s); simB.BeginDay(D0); var c1 = simB.Accounts[0]; simB.AddFill(c1, 3100); simB.EndTrade(c1, new KeystoneSimTrade { Entry = D0.AddHours(10), Exit = D0.AddHours(11), Pnl = 3100, Dir = 1 }); simB.EndDay();
        simB.BeginDay(D0.AddDays(1)); simB.EndDay();
        Check(c1.Phase == "EVAL", "+$3,100 in ONE day is no pass (minimum 2 days and best day ≤ 50 %): the day-count day does not pass it either", c1.Phase);
        double[] fundedDays = { 400, 400, 400, 400, 120, 400 }; int paidOn = -1;
        for (int k = 0; k < fundedDays.Length; k++) { day = day.AddDays(1); sim.BeginDay(day); sim.AddFill(a1, fundedDays[k]); sim.EndTrade(a1, new KeystoneSimTrade { Entry = day.AddHours(10), Exit = day.AddHours(11), Pnl = fundedDays[k], Dir = 1 }); sim.EndDay(); if (paidOn < 0 && a1.Payouts > 0) paidOn = k; }
        Check(paidOn == 5 && a1.Payouts == 1 && Eq(a1.Gross, Math.Floor(0.5 * 2120)) && Eq(a1.Cash, Math.Floor(0.5 * 2120) * 0.9), "a payout on the 5th day ≥ $150 (the $120 day does not count): 50 % of $2,120 = $1,060 gross → $954 to you (90 %)", paidOn + " " + a1.Gross + " " + a1.Cash);
        var sim2 = KeystoneTriRotation.NewBatch(s); var day2 = D0; sim2.BeginDay(day2); var b1 = sim2.Accounts[0]; b1.Phase = "FUNDED"; b1.WasPhase = "FUNDED"; b1.PassedOn = D0; b1.Bal = 9000; b1.PeakEod = 9000; b1.Qualifying = 4; sim2.AddFill(b1, 200); sim2.EndTrade(b1, new KeystoneSimTrade { Entry = day2.AddHours(10), Exit = day2.AddHours(11), Pnl = 200, Dir = 1 }); sim2.EndDay();
        Check(b1.Payouts == 1 && Eq(b1.Gross, 2000) && Eq(b1.Cash, 1800), "a big funded balance: the payout is capped at $2,000 → $1,800 to you", b1.Gross + " " + b1.Cash);

        // ---- 7. batch rollover: every account blown → the next batch of 30 starts the next session
        var many = new List<KeystoneArcBar>(); var dm = D0; var mdays = new List<DateTime>();
        for (int k = 0; k < 14; k++, dm = dm.AddDays(dm.DayOfWeek == DayOfWeek.Friday ? 3 : 1)) { mdays.Add(dm); double px = 9000; for (var t = dm.AddHours(9).AddMinutes(31); t <= dm.AddHours(16); t = t.AddMinutes(1), px -= 1) many.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = px + 1, High = px + 1.5, Low = px, Close = px }); }
        var dmm = KeystoneTriData.From(many); var rb = KeystoneTriRotation.Run(dmm, every(dmm, 3), s, D0, mdays.Last());
        Check(rb.Batches.Count >= 2 && rb.Batches[0].Finished && rb.Batches[0].BlownEval == 30 && rb.Batches[1].Start > rb.Batches[0].End && rb.Batches[1].Start == mdays[mdays.IndexOf(rb.Batches[0].End) + 1], "rollover: batch 1 ends when all 30 are blown, batch 2 (30 new evaluations) starts the next session", string.Join(" | ", rb.Batches.Select(b => b.No + ":" + b.Start.ToString("MM-dd") + "→" + b.End.ToString("MM-dd") + " blown " + b.BlownEval)));
        Check(rb.Trades.Where(t => t.Batch == 2).All(t => t.Account >= 1 && t.Account <= 30) && Eq(rb.Batches[0].Net, -3600) && rb.Stats.Finished >= 1 && Eq(rb.Stats.PctPositive, 0), "the batch's net = payouts − cost (−$3,600 here), stats per finished batch", rb.Batches[0].Net.ToString());

        // ---- 8. the random control: the same number of entries a session, inside the window, reproducible by seed
        var per = new Dictionary<DateTime, int> { { D0, 7 } }; var ra = KeystoneTriSetup.Random(dr, s, per, 5); var rb2 = KeystoneTriSetup.Random(dr, s, per, 5);
        Check(ra.Count == 7 && ra.All(c => c.Setup == "RANDOM" && c.Entry > D0.AddHours(9).AddMinutes(30) && c.Entry < D0.AddHours(16)) && ra.Select(c => c.EntryIdx).SequenceEqual(rb2.Select(c => c.EntryIdx)), "RANDOM: 7 entries in the window, the same seed = the same entries");

        Console.WriteLine(failures == 0 ? "ALL THREE-SETUP TESTS PASSED" : "THREE-SETUP TESTS: " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
