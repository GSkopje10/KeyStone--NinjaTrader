// 09f: 🎯 TOTAL EXIT GUARD — the user's DEMO case (his manual SELL 0.65 hit its broker TP while the automation was STOPPED by that manual trade:
// the total was positive, nothing closed the rest), breakeven vs target, ✋ MANUAL MANAGE = alert only, never opens, not armed = nothing closed
using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using KeystoneMT5;
public static class NineFTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static readonly DateTime Day0 = new DateTime(2026, 10, 20), Day1 = new DateTime(2026, 10, 21);
    sealed class FakeBroker : IBroker
    {
        public readonly Mt5State St = new Mt5State(); public readonly List<string> Cmds = new List<string>();
        public Mt5State State { get { return St; } } public bool Send(string line) { return true; } public string Command(string cmd, params object[] a) { Cmds.Add(cmd); return "F" + Cmds.Count; }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
    }
    static Mt5Position Leg(long tk, string side, double lots, double open, double profit, DateTime t, long magic = 770077) { return new Mt5Position { Ticket = tk, Symbol = "XAUUSD", Side = side, Lots = lots, Open = open, Profit = profit, Time = t, Magic = magic }; }
    static AutoSettings S() { return new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.08, AddLots = 0.16, Target = 16, HedgeMode = "NONE", HedgeSize = "FIXED", Swing = false, Rolling = false, OnManual = "STOP", FirstDir = "AUTO" }; }
    // his #43: BUY 0.16@4145.90, 0.16@4145.30, 0.08@4142.14, SELL 0.16@4137.38, 0.16@4137.03 (net +0.08), the automation running
    static Tuple<FakeBroker, HedgeAuto, List<string>> His(AutoSettings s)
    {
        var fb = new FakeBroker(); var t0 = Day1.AddHours(15); fb.St.ServerNow = t0; fb.St.Account.Kind = "DEMO"; fb.St.Account.Balance = 7954.49;
        fb.St.Symbols["XAUUSD"] = new Mt5Symbol { Name = "XAUUSD", TickSize = 0.01, TickValue = 1, VolStep = 0.01, VolMin = 0.01 }; fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4139.31, Ask = 4139.53, Time = t0 };
        foreach (var p in new[] { Leg(1, "BUY", 0.16, 4145.90, -105.44, t0.AddMinutes(-90)), Leg(2, "BUY", 0.16, 4145.30, -95.84, t0.AddMinutes(-80)), Leg(3, "BUY", 0.08, 4142.14, -22.64, t0.AddMinutes(-70)), Leg(4, "SELL", 0.16, 4137.38, -30.88, t0.AddMinutes(-60)), Leg(5, "SELL", 0.16, 4137.03, -36.48, t0.AddMinutes(-50)) }) fb.St.Positions.Add(p);
        var au = new HedgeAuto(); au.Send = (c, a, l) => fb.Command(c, a); var closes = new List<string>(); au.GuardClose = sym => { closes.Add(sym); return "G" + closes.Count; };
        au.Start(fb, s, true); return Tuple.Create(fb, au, closes);
    }
    static void Tp(FakeBroker fb)
    {   // his SELL 0.65 @4139.31 (by hand) → its broker TP 4133.79 fills: +$353.42 net; the other legs at 4133.79
        var t = fb.St.ServerNow.AddMinutes(20); fb.St.Positions.RemoveAll(p => p.Ticket == 60); fb.St.ServerNow = t; fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4133.79, Ask = 4134.01, Time = t };
        fb.St.Deals.Add(new Mt5Deal { Ticket = 961, Position = 60, Time = t, Symbol = "XAUUSD", Type = "BUY", Entry = "OUT", Lots = 0.65, Price = 4133.79, Profit = 353.42, Reason = "TP" });
        foreach (var p in fb.St.Positions) p.Profit = Math.Round((4133.79 - p.Open) * p.Dir * p.Lots * 100, 2);
    }
    static void Hand(FakeBroker fb, HedgeAuto au)
    {
        var t = fb.St.ServerNow.AddSeconds(30); fb.St.ServerNow = t; fb.St.Positions.Add(Leg(60, "SELL", 0.65, 4139.31, 0, t, 0));
        fb.St.Deals.Add(new Mt5Deal { Ticket = 960, Position = 60, Time = t, Symbol = "XAUUSD", Type = "SELL", Entry = "IN", Lots = 0.65, Price = 4139.31, Reason = "DESKTOP" });
        fb.St.ServerNow = t.AddSeconds(1); au.Step(fb);
    }

    public static int Main9()
    {
        // ---------------------------------------------------------------- 1. which exit
        var a = HedgeAuto.ExitLevel(16, 0.5, -10, 20, false, 3, 60); var b = HedgeAuto.ExitLevel(16, 0.5, -48.1, 20, false, 3, 60); var c = HedgeAuto.ExitLevel(16, 0.5, -10, 61, false, 3, 60); var d = HedgeAuto.ExitLevel(16, 0.5, 0, 5, true, 3, 60);
        Check(a.Item1 == "TARGET" && a.Item2 == 16 && b.Item1 == "BREAKEVEN" && b.Item2 == 0.5 && b.Item3.Contains("−$48") && c.Item1 == "BREAKEVEN" && c.Item3.Contains("61 min") && d.Item1 == "BREAKEVEN" && d.Item3.Contains("manual"),
            "🎯 the exit: a fresh basket → the TARGET +$16; in trouble (worst −$48 ≤ −3 × $16, or open 61 min, or a manual trade added) → BREAKEVEN +$0.50", a + " | " + b + " | " + c + " | " + d);
        // ---------------------------------------------------------------- 2. his exact case
        {
            var h = His(S()); var fb = h.Item1; var au = h.Item2; var closes = h.Item3; string toast = ""; au.OnGuard += m => toast = m;
            Hand(fb, au); bool stopped = au.HandStopped && !au.Running;
            au.GuardStep(fb, S()); int before = closes.Count;   // 4139.31: the total is −$291.28 → nothing
            Tp(fb); au.GuardStep(fb, S());
            Check(stopped && before == 0 && closes.Count == 1 && closes[0] == "XAUUSD" && au.GuardExit == "BREAKEVEN" && Math.Abs(au.GuardTotal - 17.98) < 0.01 && !fb.Cmds.Contains("ORDER") && toast.Contains("CLOSED AT BREAKEVEN") && au.Story.Any(x => x.Item2.Contains("🎯 TOTAL EXIT GUARD")),
                "🎯 his case: the automation STOPPED by his manual SELL 0.65; its broker TP fills (+$353.42) → total −$335.44 open + $353.42 closed = +$17.98 ≥ breakeven → the other 5 legs CLOSE at once, no question, a loud toast; nothing opened",
                stopped + " " + before + " closes " + closes.Count + " " + au.GuardExit + " total " + au.GuardTotal + " toast " + toast);
            fb.St.Positions.Clear(); fb.St.Deals.Add(new Mt5Deal { Ticket = 962, Position = 1, Time = fb.St.ServerNow.AddSeconds(1), Symbol = "XAUUSD", Type = "SELL", Entry = "OUT", Lots = 0.16, Price = 4133.79, Profit = -193.76 }); fb.St.ServerNow = fb.St.ServerNow.AddSeconds(2); au.GuardStep(fb, S());
            var it = au.Iterations.LastOrDefault();
            Check(it != null && it.Done && it.Reason == "🎯 EXIT GUARD BREAKEVEN" && au.Current == null && !au.Running && au.HandStopped, "🎯 then flat: the iteration is recorded as \"🎯 EXIT GUARD BREAKEVEN\" (the automation stays stopped)", it == null ? "none" : it.Reason + " " + it.Done);
        }
        {   // the price came back (his screen: open −$416.51 + closed +$353.42 = −$63.09) → it waits; 0.5 below the level → nothing
            var h = His(S()); var fb = h.Item1; var au = h.Item2; Hand(fb, au); Tp(fb); foreach (var p in fb.St.Positions) p.Profit = Math.Round((4129.0 - p.Open) * p.Dir * p.Lots * 100, 2);
            au.GuardStep(fb, S()); double shownBe = au.GuardBePx;
            Check(h.Item3.Count == 0 && au.GuardTotal < 0 && au.GuardText.Contains("🎯 EXIT AT BREAKEVEN") && au.GuardText.Contains("manual trade") && !double.IsNaN(shownBe), "🎯 below the level nothing closes; the banner says which exit is armed and why: \"" + au.GuardText + "\"", h.Item3.Count + " " + au.GuardText);
        }
        // ---------------------------------------------------------------- 3. a fresh basket waits for the TARGET; in trouble it takes breakeven
        {
            var fb = new FakeBroker(); var t0 = Day1.AddHours(10); fb.St.ServerNow = t0; fb.St.Symbols["XAUUSD"] = new Mt5Symbol { Name = "XAUUSD", TickSize = 0.01, TickValue = 1 }; fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4000, Ask = 4000.2, Time = t0 };
            fb.St.Positions.Add(Leg(9, "BUY", 0.08, 3999, 8, t0.AddMinutes(-5), 0)); var au = new HedgeAuto(); var closes = new List<string>(); au.GuardClose = sym => { closes.Add(sym); return "G"; };
            au.GuardStep(fb, S()); int n1 = closes.Count; fb.St.Positions[0].Profit = 16.4; au.GuardStep(fb, S()); int n2 = closes.Count; string ex2 = au.GuardExit;
            var fb2 = new FakeBroker(); fb2.St.ServerNow = t0; fb2.St.Symbols["XAUUSD"] = fb.St.Symbols["XAUUSD"]; fb2.St.Quotes["XAUUSD"] = fb.St.Quotes["XAUUSD"]; fb2.St.Positions.Add(Leg(9, "BUY", 0.08, 3999, -60, t0.AddMinutes(-5), 0));
            var au2 = new HedgeAuto(); var c2 = new List<string>(); au2.GuardClose = sym => { c2.Add(sym); return "G"; }; au2.GuardStep(fb2, S()); fb2.St.Positions[0].Profit = 0.6; au2.GuardStep(fb2, S());
            var sOff = S(); sOff.GuardManual = false; var au3 = new HedgeAuto(); var c3 = new List<string>(); au3.GuardClose = sym => { c3.Add(sym); return "G"; }; au3.GuardStep(fb, sOff);
            var sNo = S(); sNo.ExitGuard = false; var au4 = new HedgeAuto(); var c4 = new List<string>(); au4.GuardClose = sym => { c4.Add(sym); return "G"; }; au4.GuardStep(fb, sNo);
            Check(n1 == 0 && n2 == 1 && ex2 == "TARGET" && c2.Count == 1 && au2.GuardExit == "BREAKEVEN" && c3.Count == 0 && c4.Count == 0 && AutoSettings.Load(S().Save()).ExitGuard && new AutoSettings().ExitGuard && new AutoSettings().GuardManual && !AutoSettings.Load(sNo.Save()).ExitGuard,
                "🎯 a pure manual basket (guarded by default): fresh +$8 → waits, +$16.40 → closes at the TARGET; one that was −$60 (≤ −3 × $16) closes at +$0.60 = BREAKEVEN; \"guard manual baskets\" off or the guard off → nothing; ON by default, saved",
                n1 + " " + n2 + " " + ex2 + " / " + c2.Count + " " + au2.GuardExit + " / " + c3.Count + " " + c4.Count);
        }
        // ---------------------------------------------------------------- 4. ✋ MANUAL MANAGE = alert only; not armed = nothing closed
        {
            var h = His(S()); var fb = h.Item1; var au = h.Item2; Hand(fb, au); au.SetManualManage(true); string toast = ""; au.OnGuard += m => toast = m; Tp(fb); au.GuardStep(fb, S());
            Check(h.Item3.Count == 0 && au.GuardAlert && toast.Contains("CLOSE ALL NOW") && fb.St.Positions.Count == 5, "🎯 ✋ MANUAL MANAGE: the level reached → a loud alert with ⛔ CLOSE ALL NOW, nothing closed by itself", h.Item3.Count + " " + au.GuardAlert + " " + toast);
        }
        {
            var h = His(S()); var fb = h.Item1; var au = h.Item2; fb.St.Account.Kind = "REAL"; au.GuardClose = sym => null; string toast = ""; au.OnGuard += m => toast = m; Hand(fb, au); Tp(fb); au.GuardStep(fb, S()); au.GuardStep(fb, S());
            Check(toast.Contains("NOT closed") && toast.Contains("not armed") && au.Log.Count(x => x.Contains("not ARMED")) == 1 && !fb.Cmds.Contains("CLOSEALL"), "🎯 a REAL account not ARMED: nothing is closed (every close needs ARM) — a loud warning, once", toast);
        }
        // ---------------------------------------------------------------- 5. while the automation runs: a basket in trouble closes at breakeven through the engine
        {
            var warm = new List<Mt5Bar>(); for (int i = 0; i < 30; i++) warm.Add(new Mt5Bar { Time = Day0.AddHours(10).AddMinutes(i), O = 4000, H = 4000.1, L = 3999.9, C = 4000, V = 100, Spread = 10 });
            var path = new List<Mt5Bar>(); double o = 4000; var closesP = Enumerable.Range(1, 30).Select(i => 4000 - 0.25 * i).Concat(Enumerable.Range(1, 40).Select(i => 3992.5 + 0.25 * i)).ToList(); int k0 = 0;
            foreach (var cl in closesP) { path.Add(new Mt5Bar { Time = Day1.AddMinutes(k0++), O = o, C = cl, H = Math.Max(o, cl) + 0.1, L = Math.Min(o, cl) - 0.1, V = 100, Spread = 10 }); o = cl; }
            var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }, warm.Concat(path).ToList());
            var s = S(); s.FirstDir = "BUY"; s.EntryLots = 0.1; s.Target = 50; s.GuardTroubleX = 1; s.MaxIterations = 1; s.Rescue = false; s.OnManual = "ADOPT";
            var au = new HedgeAuto(); au.Send = (c, x, l) => sb.Command(c, x); au.GuardClose = sym => sb.Command("CLOSEALL", sym, "ALL"); sb.OnTickHook = () => { au.Step(sb); au.GuardStep(sb, s); };
            au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60; for (int k = 0; k < 3000 && !sb.Ended; k++) sb.Advance(0.25);
            var it = au.Iterations.FirstOrDefault();
            Check(it != null && it.Done && it.Reason == "🎯 EXIT GUARD BREAKEVEN" && it.MaxDd <= -50 && it.Result > -3 && it.Result < 10, "🎯 the automation running: the BUY fell to −$75 (in trouble), came back → closed at BREAKEVEN by the guard (not held for the +$50 target)", it == null ? "none" : it.Reason + " " + it.Result + " maxdd " + it.MaxDd);
        }
        // ---------------------------------------------------------------- 6. 🧭 RECOVERY ASSIST — #43 from export_1109c (10 Nov 01:36 server: the detector said
        // 📉 TREND DOWN 50 · EMA9<21 M1, broke 4141.37, below VWAP; ATR(M5) $4.42; the last swing high 4144.28; his basket −$266, net +0.08, target $16, $7,954.49)
        {
            var tr = new TrendState { Dir = -1, Strength = 50, Why = "EMA9<21 M1, broke 4141.37, below VWAP", SwingHi = 4144.28, SwingLo = 4141.37, Atr5 = 4.42, Close = 4139.43 };
            var r = TrendDetect.Recovery(-266, 16, 0.08, 4139.31, 100, 7954.49, 0.07, 1.5, 0, 300, tr);
            Check(r.Dir == -1 && Math.Abs(r.Lots - 0.51) < 1e-9 && Math.Abs(r.NetAfter + 0.43) < 1e-9 && Math.Abs(r.BePx - 4133.12) < 0.005 && Math.Abs(r.TpPx - 4132.75) < 0.005 && Math.Abs(r.Protect - 4144.28) < 1e-9 && Math.Abs(r.RiskLeg - 253.47) < 0.005 && Math.Abs(r.BasketAtProtect + 479.71) < 0.005 && Math.Abs(r.GainAtMove - 19.09) < 0.005
                && r.Text.Contains("SELL 0.51 now") && r.Text.Contains("breakeven 4133.12") && r.Text.Contains("protection 4144.28") && r.Text.Contains("−$253") && r.Text.Contains(TrendDetect.RecoveryHonest),
                "🧭 #43 at 01:36: SELL 0.51 (his 0.65) → net −0.43, breakeven 4133.12, +$16 at 4132.75; protection 4144.28 (the last swing high) = −$253 on the leg, the basket −$479.71 there; ≈ +$19.09 if the price moves 1.5 ATR ($6.63) down — and the ticket says the 6-year test lost",
                r.Lots + " " + r.NetAfter + " " + r.BePx + " " + r.TpPx + " " + r.Protect + " " + r.RiskLeg + " " + r.BasketAtProtect + " " + r.GainAtMove + " | " + r.Text);
            var none = TrendDetect.Recovery(-266, 16, 0.08, 4139.31, 100, 7954.49, 0.07, 1.5, 0, 300, new TrendState { Dir = 0, Strength = 35, Why = "EMA9>21 M5, above VWAP" });
            var lowM = TrendDetect.Recovery(-266, 16, 0.08, 4139.31, 100, 7954.49, 0.07, 1.5, 250, 300, tr);
            Check(none.Dir == 0 && none.Text.Contains("no trade — no clear trend") && lowM.Dir == 0 && lowM.Text.Contains("margin"), "🧭 no confirmed trend (01:30: NO TREND 35) → \"no trade — no clear trend\"; margin 250 % < 300 % → no trade", none.Text + " | " + lowM.Text);
        }
        {   // the recovery leg placed (SELL 0.51 by hand, no TP): when the price brings the TOTAL to breakeven the guard closes EVERYTHING
            var h = His(S()); var fb = h.Item1; var au = h.Item2; var t = fb.St.ServerNow.AddSeconds(30); fb.St.ServerNow = t; fb.St.Positions.Add(Leg(61, "SELL", 0.51, 4139.31, 0, t, 0));
            fb.St.Deals.Add(new Mt5Deal { Ticket = 970, Position = 61, Time = t, Symbol = "XAUUSD", Type = "SELL", Entry = "IN", Lots = 0.51, Price = 4139.31, Reason = "DESKTOP" }); fb.St.ServerNow = t.AddSeconds(1); au.Step(fb);
            double hit = double.NaN; for (double P = 4139.31; P > 4120; P = Math.Round(P - 0.01, 2)) { foreach (var p in fb.St.Positions) p.Profit = Math.Round((P - p.Open) * p.Dir * p.Lots * 100, 2); fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = P, Ask = P + 0.22, Time = fb.St.ServerNow }; au.GuardStep(fb, S()); if (h.Item3.Count > 0) { hit = P; break; } }
            Check(au.HandStopped && h.Item3.Count == 1 && au.GuardExit == "BREAKEVEN" && hit < 4139.31 && au.GuardTotal >= 0.5 && au.GuardTotal < 1.5, "🧭 + 🎯 the recovery SELL 0.51 in (the automation stopped); the price falls to " + hit.ToString("0.00", IC) + " → the TOTAL reaches breakeven (" + au.GuardTotal.ToString("0.00", IC) + ") → ALL 6 positions close", hit + " " + au.GuardTotal + " " + h.Item3.Count);
        }
        // ---------------------------------------------------------------- 7. ▶ RESUME preview: what it would do at once (the 04:29:13 SELL 0.16 @4126.64)
        {
            var warm = new List<Mt5Bar>(); for (int i = 0; i < 30; i++) warm.Add(new Mt5Bar { Time = Day0.AddHours(10).AddMinutes(i), O = 4000, H = 4000.1, L = 3999.9, C = 4000, V = 100, Spread = 10 });
            Func<List<Mt5Bar>> mk = () => { var l = new List<Mt5Bar>(); double o = 4000; int i = 0; foreach (var cl in Enumerable.Range(1, 20).Select(x => 4000 - 0.3 * x).Concat(Enumerable.Repeat(3994.0, 60))) { l.Add(new Mt5Bar { Time = Day1.AddMinutes(i++), O = o, C = cl, H = Math.Max(o, cl) + 0.1, L = Math.Min(o, cl) - 0.1, V = 100, Spread = 10 }); o = cl; } return l; };
            var spec = new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 };
            var s = S(); s.HedgeMode = "PRICE"; s.HedgePrice = 3; s.HedgeCap = 0; s.RangeOnly = false; s.AddLots = 0.16; s.OnManual = "ADOPT"; s.Target = 500;
            Func<bool, Tuple<SimBroker, HedgeAuto, List<string>, int>> run = noCatch =>
            {
                var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, spec, warm.Concat(mk()).ToList());
                sb.Playing = true; sb.Speed = 60; sb.Advance(0.25); sb.Command("ORDER", "XAUUSD", "BUY", 0.08, 0.0, 0.0, 0.0); for (int k = 0; k < 120; k++) sb.Advance(0.25);   // the price fell $6: past the line (entry − 3)
                int cmds0 = sb.State.Deals.Count; var said = HedgeAuto.PreviewStart(sb, s, null, null); int cmds1 = sb.State.Deals.Count, pos1 = sb.State.Positions.Count;
                var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); au.Start(sb, s, true); au.NoCatchUp = noCatch; for (int k = 0; k < 40; k++) sb.Advance(0.25);
                return Tuple.Create(sb, au, said, cmds1 - cmds0 + (pos1 - 1));
            };
            var withC = run(false); var noC = run(true); var txt = HedgeAuto.PreviewText(withC.Item3);
            Check(withC.Item3.Count == 1 && withC.Item3[0].StartsWith("SELL 0.16") && txt.Contains("IMMEDIATELY: SELL 0.16 XAUUSD") && txt.Contains("WITHOUT THE CATCH-UP ORDER") && withC.Item4 == 0
                && withC.Item2.Trades.Any(t => t.Side == "SELL") && !noC.Item2.Trades.Any(t => t.Side == "SELL") && noC.Item2.Story.Any(x => x.Item2.Contains("NO CATCH-UP")) && HedgeAuto.PreviewText(new List<string>()) == "",
                "▶ RESUME preview: \"" + txt + "\" — the dry run sends nothing; RESUME sells at once, RESUME WITHOUT THE CATCH-UP ORDER waits for the next signal", txt + " | " + withC.Item4 + " " + withC.Item2.Trades.Count + " / " + noC.Item2.Trades.Count);
        }
        // ---------------------------------------------------------------- 8. #41 (his 09d DEMO): +$204.37 TARGET 20:13:20 → 20:13:24, 2 / 1 / 2, max 0.22, worst 0.00 — its
        // deals are in the other DEMO file (the 09c swap): flagged "result unverified — balance jump", listed grey, left out of NET / TODAY / wins
        {
            var day = new DateTime(2026, 11, 9); var its = new List<AutoIteration> {
                new AutoIteration { N = 40, Start = day.AddHours(19), End = day.AddHours(19).AddMinutes(12), Buys = 1, Sells = 1, AddOns = 1, MaxGross = 0.24, MaxDd = -30, Result = 16.4, Reason = "TARGET" },
                new AutoIteration { N = 41, Start = day.AddHours(20).AddMinutes(13).AddSeconds(20), End = day.AddHours(20).AddMinutes(13).AddSeconds(24), Buys = 2, Sells = 1, AddOns = 2, MaxGross = 0.22, MaxDd = 0, Result = 204.37, Reason = "TARGET" },
                new AutoIteration { N = 42, Start = day.AddHours(21), End = day.AddHours(21).AddSeconds(40), Buys = 1, Sells = 0, AddOns = 0, MaxGross = 0.08, MaxDd = 0, Result = 16.9, Reason = "TARGET" } };
            var ch = Continuity.RepairResults(its, new List<Mt5Deal>(), "XAUUSD", 16); var st = new HedgeAuto().Calc(its); var line = Continuity.ResumeLine(its, null, day);
            Check(its[1].Unverified && its[1].Reason.Contains("result unverified — balance jump") && Math.Abs(its[1].Result - 204.37) < 1e-9 && !its[0].Unverified && !its[2].Unverified && ch.Count == 1
                && Math.Abs(st.Net - 33.3) < 1e-9 && st.Done == 2 && st.Wins == 2 && line.Contains("2 iterations") && line.Contains("+$33.30") && Continuity.RepairResults(its, new List<Mt5Deal>(), "XAUUSD", 16).Count == 0,
                "#41 (+$204.37 in 4 s, worst $0.00, 3 legs, target $16): flagged \"result unverified — balance jump\" — still listed, NET = +$33.30 without it (not +$237.67), today / wins the same; a normal quick +$16.90 is not flagged; run twice = no change",
                its[1].Reason + " | net " + st.Net + " " + line + " " + string.Join(" | ", ch));
        }
        Console.WriteLine(fails == 0 ? "ALL 09f TESTS PASSED" : fails + " 09f TEST(S) FAILED");
        return fails;
    }
}
