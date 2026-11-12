// 09n: 🚑 STEP IN — the trigger, the trend-side sizing (flip, risk budget, margin guard, the bridge's max), trims / trail, pair cuts that never
// worsen the total, the goal close, the floor (neutralise, never a loss close), the chop HOLD, the stats math, the session gate on MT5 accounts
// (OFF by default, typed STEP IN / WHOLE ACCOUNT, DISARM / restart revoke, Check refuses, Step stops), PROP refusals stay, and a simulated
// run that blows (stop-out recorded, the account reset like 💥 AUTO RESTART, it runs again)
using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using KeystoneMT5;
public static class NineNTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 1e-9; }
    static readonly DateTime Day0 = new DateTime(2026, 10, 20), Day1 = new DateTime(2026, 10, 21);
    sealed class FakeBroker : IBroker
    {
        public readonly Mt5State St = new Mt5State(); public readonly List<string> Cmds = new List<string>();
        public Mt5State State { get { return St; } } public bool Send(string line) { return true; } public string Command(string cmd, params object[] a) { Cmds.Add(cmd); return "F" + Cmds.Count; }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
    }
    static Mt5Position Leg(long tk, string side, double lots, double open, double profit, DateTime t) { return new Mt5Position { Ticket = tk, Symbol = "XAUUSD", Side = side, Lots = lots, Open = open, Profit = profit, Time = t, Magic = 770077 }; }
    static TrendState Up(int strength = 80) { return new TrendState { Dir = 1, Strength = strength, Atr5 = 2, Atr1 = 0.9, SwingHi = 4000, SwingLo = 3990, Close = 4000.5, Why = "test" }; }
    static StepInView View(double total, IEnumerable<Mt5Position> pos, TrendState tr, double eq = 10000, bool sim = true)
    {
        return new StepInView { Pos = pos.ToList(), Trend = tr, Total = total, Target = 10, Bid = 4000.5, Ask = 4000.7, PerPrice = 100, Balance = 10000, Equity = eq, Margin = 500, MarginLevel = eq / 500 * 100, MarginPerLot = 100, StopOutPct = 50, Step = 0.01, Simulated = sim, Now = Day1.AddHours(12) };
    }
    static StepInManager Active(AutoSettings s, StepInView v) { var m = new StepInManager(); m.Activate(s, 7, v, v.Now.AddMinutes(-90), "test", "acc"); return m; }
    static List<Mt5Bar> Path0(DateTime start, double first, IEnumerable<double> closes)
    {
        var res = new List<Mt5Bar>(); double o = first; int i = 0;
        foreach (var c in closes) { res.Add(new Mt5Bar { Time = start.AddMinutes(i++), O = o, C = c, H = Math.Round(Math.Max(o, c) + 0.1, 2), L = Math.Round(Math.Min(o, c) - 0.1, 2), V = 100, Spread = 10 }); o = c; }
        return res;
    }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }

    // a chop that drives a flip-flopping basket deep, then a down trend with swings (downBars), then a rally of rallyStep a minute
    static Tuple<HedgeAuto, SimBroker, string> RunSim(int downBars, double rallyStep, double bal, int lev, double gap = 0, double moveK = 2)
    {
        var chop = Enumerable.Range(0, 180).Select(i => Math.Round(4000 + 6 * Math.Sin(i * 2 * Math.PI / 14), 2)).ToList(); var down = new List<double>(); double p = chop[chop.Count - 1];
        for (int i = 0; i < downBars; i++) { p += (i % 6 >= 4) ? 0.7 : -1.0; down.Add(Math.Round(p, 2)); }
        var up = new List<double>(); for (int i = 0; i < 160; i++) { p += rallyStep + (i == 0 ? gap : 0); up.Add(Math.Round(p, 2)); }
        var warm = Path0(Day0.AddHours(10), 4000, Enumerable.Range(0, 260).Select(i => 4000 + Math.Sin(i / 7.0) * 0.8));
        var path = warm.Concat(Path0(Day1, 4000, Enumerable.Repeat(4000.0, 5).Concat(chop).Concat(down).Concat(up))).ToList();
        var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = bal, Kind = "STANDARD", FixedSpread = 10, Leverage = lev, StopOutPct = 50 }, Gold(), path);
        var s = new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, AddLots = 0.2, Target = 30, FirstDir = "SELL", HedgeMode = "PRICE", HedgePrice = 4, HedgeSize = "FIXED", NextStart = "PAUSE", PauseSec = 99999, Swing = false, Lookback = 1, Rolling = false, TrackManual = false, OnManual = "ADOPT",
            Rescue = false, HardFloat = 0, HardMargin = 0, ExitGuard = false, StepIn = true, StepInX = 4, StepInMin = 0, StepInRisk = 100, StepInFloor = 0, StepInMoveK = moveK };
        var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); string chk = au.Check(sb, s); if (chk == "") au.Start(sb, s, false);
        sb.Playing = true; sb.Speed = 60; for (int k = 0; k < 40000 && !sb.Ended && au.StepIns.Count == 0; k++) sb.Advance(0.25);
        if (Environment.GetEnvironmentVariable("SIDBG") == "1") { foreach (var lg in au.Log) Console.WriteLine("  LOG " + lg); }
        return Tuple.Create(au, sb, chk);
    }

    public static int Main15()
    {
        var t0 = Day1.AddHours(12);
        // ---------------------------------------------------------------- 1. settings + the trigger
        {
            var d = new AutoSettings(); var s = new AutoSettings { StepIn = true, StepInX = 6, StepInMin = 90, StepInRisk = 40, StepInTf = 5, StepInGoal = "BE" }; var l = AutoSettings.Load(s.Save());
            Check(!d.StepIn && Eq(d.StepInX, 8) && d.StepInMin == 120 && Eq(d.StepInRisk, 100) && Eq(AutoSettings.StepInLiveRisk, 50) && l.StepIn && Eq(l.StepInX, 6) && l.StepInMin == 90 && Eq(l.StepInRisk, 40) && l.StepInTf == 5 && l.StepInGoal == "BE" && !AutoSettings.Load(d.Save()).StepIn,
                "🚑 settings: OFF by default (trigger 8 × the target or 120 min, MAX RISK 100 % simulated / 50 % on MT5), survive save / load");
            var a = new AutoSettings { StepInX = 8, StepInMin = 120, StepInMinX = 3 };
            string deep = StepInManager.Trigger(a, -80, 10, 5), shallow = StepInManager.Trigger(a, -79, 10, 5), stuck = StepInManager.Trigger(a, -31, 10, 125), stuckShallow = StepInManager.Trigger(a, -29, 10, 500);
            a.StepInUsd = 50; string usd = StepInManager.Trigger(a, -50, 10, 0), usdNo = StepInManager.Trigger(a, -49, 10, 0);
            Check(deep.Contains("8 × the target") && shallow == "" && stuck.Contains("stuck 125 min") && stuckShallow == "" && usd.Contains("$50") && usdNo == "",
                "🚑 trigger: −$80 at 8 × $10 fires, −$79 not; 125 min stuck at −3.1 × fires, 500 min at −2.9 × not; a $ depth replaces × the target", deep + " | " + stuck + " | " + usd);
            Check(Eq(StepInManager.FloorOf(new AutoSettings { StepInRisk = 100 }, 10000), 0) && Eq(StepInManager.FloorOf(new AutoSettings { StepInRisk = 50 }, 10000), 5000) && Eq(StepInManager.FloorOf(new AutoSettings { StepInRisk = 100, StepInFloor = 2500 }, 10000), 2500)
                && Eq(StepInManager.GoalOf(new AutoSettings(), 10), 10) && Eq(StepInManager.GoalOf(new AutoSettings { StepInGoal = "BE" }, 10), 0.5),
                "🚑 floor: 100 % = $0 (may blow), 50 % of $10,000 = $5,000, a $ floor wins; goal: breakeven + the target / breakeven");
        }
        // ---------------------------------------------------------------- 2. trend-side sizing
        var S = new AutoSettings { StepIn = true, Target = 10 };
        {
            var pos = new[] { Leg(1, "BUY", 0.10, 4004, -35, t0.AddMinutes(-80)), Leg(2, "SELL", 0.10, 3998, -25, t0.AddMinutes(-70)), Leg(3, "BUY", 0.05, 4006, -27, t0.AddMinutes(-60)) };   // net +0.05
            var v = View(-120, pos, Up()); var m = Active(S, v); var a = m.Decide(S, v);
            // need = (10 + 120) / (2 × 2 × 100) − 0.05 = 0.325 − 0.05 = 0.275 → 0.28
            Check(a.Kind == "ORDER" && a.Tag == "STACK" && a.Dir == 1 && Eq(a.Lots, 0.28) && a.Why.Contains("trend UP 80") && m.Ep.Stacks == 1,
                "🚑 STACK with the trend: net +0.05 long, trend UP 80, the M1 close broke the swing → BUY 0.28 = enough to reach the goal in 2 × ATR M5 (0.325 net) − the 0.05 already long", a.Kind + " " + a.Dir + " " + a.Lots + " " + a.Why);
            var a2 = m.Decide(S, v); Check(a2.Kind == "NONE" && a2.Why.Contains("new M1 swing break"), "🚑 the same swing never stacks twice", a2.Why);
            var sh = new[] { Leg(1, "SELL", 0.40, 3995, -220, t0.AddMinutes(-80)) }; var vs = View(-220, sh, Up()); var ms = Active(S, vs); var f = ms.Decide(S, vs);
            Check(f.Kind == "ORDER" && f.Dir == 1 && Eq(f.Lots, Math.Round(0.40 + 0.58, 2)) && f.Why.Contains("flips the net"),
                "🚑 the net is against the trend (SELL 0.40, trend UP): one order flips it — BUY 0.40 + the stack 0.58 ((10 + 220) / 400)", f.Lots + " " + f.Why);
            // the risk budget: the equity above the floor over 3 × ATR M5 against → (2,000 − 0) / (3 × 2 × 100) = 3.33 net lots
            var vb = View(-1500, new[] { Leg(1, "BUY", 0.10, 4010, -1500, t0) }, Up(), 2000); var mb = Active(S, vb); var b = mb.Decide(S, vb);
            Check(b.Kind == "ORDER" && Eq(b.Lots, 3.23), "🚑 risk budget: a $1,500 hole needs 3.775 lots, the whole $2,000 equity over 3 × ATR M5 allows 3.33 net (0.10 held) → BUY 3.23", b.Lots + " " + b.Why);
            var S50 = new AutoSettings { StepIn = true, Target = 10, StepInRisk = 50 }; var mh = Active(S50, vb); var bh = mh.Decide(S50, vb);
            Check(Eq(mh.FloorEq, 1000) && bh.Kind == "ORDER" && Eq(bh.Lots, 1.56), "🚑 MAX RISK 50 %: the floor $1,000 → budget $1,000 / $600 = 1.66 net (0.10 held) → BUY 1.56", bh.Lots + " floor " + mh.FloorEq);
            // the margin guard: never under 100 % after the order (the broker's free margin), an MT5 account also ≥ 1.5 × its stop-out level (here 80 % → 120 %)
            double rs = StepInManager.MarginRoom(2000, 500, 800, StepInManager.MinLevel(true, 80)), rl = StepInManager.MarginRoom(2000, 500, 800, StepInManager.MinLevel(false, 80));
            var vl = View(-1500, new[] { Leg(1, "BUY", 0.10, 4010, -1500, t0) }, Up(), 2000, false); vl.StopOutPct = 80; vl.MarginPerLot = 800; var ml = Active(S, vl); var bl = ml.Decide(S, vl);
            var vs2 = View(-1500, new[] { Leg(1, "BUY", 0.10, 4010, -1500, t0) }, Up(), 2000, true); vs2.StopOutPct = 80; vs2.MarginPerLot = 800; var ms2 = Active(S, vs2); var bs2 = ms2.Decide(S, vs2);
            Check(Eq(StepInManager.MinLevel(false, 80), 120) && Eq(StepInManager.MinLevel(true, 80), 100) && Eq(StepInManager.MinLevel(false, 50), 100) && Math.Abs(rs - 1.875) < 1e-9 && Math.Abs(rl - (2000 / 1.2 - 500) / 800) < 1e-9 && bl.Kind == "ORDER" && Eq(bl.Lots, 1.45) && bl.Why.Contains("margin guard") && Eq(bs2.Lots, 1.87)
                && (vl.Equity / (vl.Margin + bl.Lots * vl.MarginPerLot) * 100) >= 120 - 1e-6,
                "🚑 margin guard: the order is cut so the margin level stays ≥ 100 % (the broker's free margin) and on an MT5 account ≥ 1.5 × the stop-out level (80 % → 120 %): 3.23 → 1.45 lots live, 1.87 simulated", bl.Lots + " " + bs2.Lots + " " + rl);
            var vm = View(-1500, new[] { Leg(1, "BUY", 0.10, 4010, -1500, t0) }, Up(), 2000); vm.MaxOrder = 1; var mm = Active(S, vm); var bm = mm.Decide(S, vm);
            Check(Eq(bm.Lots, 1) && bm.Why.Contains("max 1 lots"), "🚑 the bridge's MaxLotsPerOrder caps one order", bm.Lots.ToString(IC));
            var vw = View(-120, pos, Up(50)); var mw = Active(S, vw); var w = mw.Decide(S, vw);
            Check(w.Kind == "NONE" && w.Why.Contains("< 60"), "🚑 a weak trend (50 < 60) never stacks", w.Why);
        }
        // ---------------------------------------------------------------- 3. trims, the trail, the flip
        {
            var stk = Leg(9, "BUY", 0.60, 3998, 150, t0); var basket = new List<Mt5Position> { Leg(1, "SELL", 0.40, 3990, -420, t0.AddMinutes(-90)), stk };
            var v = View(-270, basket, Up()); v.Bid = 4000.5; var m = Active(S, v); m.Filled(9, "STACK");
            var tr = m.Decide(S, v);   // move = 2.5 ≥ 1 × ATR 2 → trim 33 % of 0.60 = 0.19
            Check(tr.Kind == "CLOSE" && tr.Tag == "TRIM" && tr.Ticket == 9 && Eq(tr.Lots, 0.19) && m.Ep.Trims == 1 && m.Ep.Trimmed > 0, "🚑 TRIM: the stack 2.5 in its favour (≥ 1 × ATR M5) → 0.19 of it banked (33 %)", tr.Kind + " " + tr.Lots + " " + tr.Why);
            var nt = m.Decide(S, v); Check(nt.Tag != "TRIM", "🚑 the next trim waits for 2 × ATR", nt.Tag + " " + nt.Why);
            Check(!double.IsNaN(m.TrailPx) && Eq(m.TrailPx, 4000.5 - 1.5 * 2), "🚑 TRAIL armed once ≥ 1 ATR in profit: 1.5 × ATR M5 behind the best (3997.50)", m.TrailPx.ToString(IC));
            var v2 = View(-270, basket, Up()); v2.Bid = 4003; v2.Ask = 4003.2; m.Decide(S, v2); double trail2 = m.TrailPx;
            var v3 = View(-300, basket, Up()); v3.Bid = 3999.9; v3.Ask = 4000.1; var th = m.Decide(S, v3);
            Check(Eq(trail2, 4000) && th.Kind == "CLOSE" && th.Tag == "TRAIL" && th.Ticket == 9 && m.Ep.Trails == 1, "🚑 the trail ratchets (best 4003 → 4000.00) and the bid 3999.90 under it closes the stack", trail2 + " " + th.Tag + " " + th.Why);
            var m2 = Active(S, v); m2.Filled(9, "STACK"); var dn = new TrendState { Dir = -1, Strength = 75, Atr5 = 2, SwingHi = 4005, SwingLo = 3995, Close = 3999 }; var v4 = View(-270, basket, dn); var fl = m2.Decide(S, v4);
            Check(fl.Kind == "CLOSE" && fl.Tag == "FLIP" && fl.Ticket == 9 && m2.Ep.Flips == 1, "🚑 FLIP: the trend turned DOWN 75 against the BUY stack → the stack closes", fl.Tag + " " + fl.Why);
        }
        // ---------------------------------------------------------------- 4. pair cuts never worsen the total
        {
            var L = Leg(1, "SELL", 0.30, 3990, -300, t0.AddMinutes(-80)); var W = Leg(2, "BUY", 0.20, 3980, 400, t0.AddMinutes(-70)); var X = Leg(3, "SELL", 0.10, 4001, 5, t0);
            var pos = new List<Mt5Position> { L, W, X }; double net0 = pos.Sum(p => p.Dir * p.Lots), tot0 = -400; var v = View(tot0, pos, Up()); v.Trend.SwingHi = double.NaN; var m = Active(S, v);
            var c1 = m.Decide(S, v); var c2 = m.Decide(S, v);
            // close 0.20 of the loser (−$200) and 0.20 of the winner (+$400): +$200 ≥ 0, the net unchanged
            double closedPl = -300 * 0.20 / 0.30 + 400 * 0.20 / 0.20, net1 = net0 - (-1 * 0.20) - (1 * 0.20);
            Check(c1.Kind == "CLOSE" && c1.Tag == "CUT" && c1.Ticket == 1 && Eq(c1.Lots, 0.20) && c2.Tag == "CUT2" && c2.Ticket == 2 && Eq(c2.Lots, 0) && closedPl >= 0 && Eq(net1, net0) && m.Ep.Cuts == 1,
                "🚑 CUT PAIR: the counter-trend SELL loser 0.20 (−$200) + the BUY winner 0.20 (+$400) = +$200 ≥ $0 → both close, the net stays " + net0.ToString("+0.##;−0.##", IC) + " and the total (open + closed) is unchanged", c1.Tag + " " + c1.Lots + " / " + c2.Tag + " " + c2.Ticket + " " + c2.Lots);
            var L2 = Leg(1, "SELL", 0.30, 3990, -900, t0); var W2 = Leg(2, "BUY", 0.20, 3980, 100, t0); var v2 = View(-800, new[] { L2, W2 }, Up()); v2.Trend.SwingHi = double.NaN; var m2 = Active(S, v2); var n2 = m2.Decide(S, v2);
            Check(n2.Tag != "CUT" && m2.Ep.Cuts == 0, "🚑 no cut when the pair adds up to < $0 (−$3,000 a lot vs +$500 a lot)", n2.Tag + " " + n2.Why);
        }
        // ---------------------------------------------------------------- 5. the goal, the floor, the chop
        {
            var pos = new[] { Leg(1, "BUY", 0.50, 3990, 20, t0) }; var v = View(10.2, pos, Up()); var m = Active(S, View(-90, pos, Up())); var g = m.Decide(S, v);
            Check(g.Kind == "CLOSEALL" && g.Tag == "GOAL" && g.Why.Contains("breakeven + the target"), "🚑 GOAL: the total +$10.20 ≥ breakeven + the target $10 → CLOSE ALL", g.Kind + " " + g.Why);
            var S50 = new AutoSettings { StepIn = true, Target = 10, StepInRisk = 50 }; var lp = new[] { Leg(1, "BUY", 1.0, 4050, -4600, t0), Leg(2, "SELL", 0.3, 4040, -1000, t0) };
            var vf0 = View(-500, lp, Up(), 10000, false); var mf = Active(S50, vf0); var vf = View(-5600, lp, Up(), 4400, false); var a1 = mf.Decide(S50, vf);
            var kinds = new List<string> { a1.Kind }; for (int i = 0; i < 5; i++) kinds.Add(mf.Decide(S50, View(-5900, new[] { Leg(1, "BUY", 1.0, 4050, -4700, t0), Leg(2, "SELL", 1.0, 4040, -1200, t0) }, Up(), 4100, false)).Kind);
            Check(Eq(mf.FloorEq, 5000) && a1.Kind == "ORDER" && a1.Tag == "FLOOR" && a1.Dir == -1 && Eq(a1.Lots, 0.7) && mf.Frozen && mf.Ep.FloorHit && kinds.Skip(1).All(k => k == "NONE") && !kinds.Contains("CLOSE") && !kinds.Contains("CLOSEALL"),
                "🚑 FLOOR (MT5, MAX RISK 50 %): the equity $4,400 ≤ $5,000 → SELL 0.70 = net 0 (full hedge), then nothing more — never a CLOSE / CLOSE ALL at a loss", string.Join(",", kinds) + " " + a1.Why);
            var gr = mf.Decide(S50, View(12, new[] { Leg(1, "BUY", 1.0, 4050, 6, t0), Leg(2, "SELL", 1.0, 4040, 6, t0) }, Up(), 10012, false));
            Check(gr.Kind == "CLOSEALL", "🚑 a frozen basket still closes at the goal (a profit close)", gr.Kind);
            var flat = new TrendState { Dir = 0, Strength = 30, Atr5 = 2, Why = "no alignment" }; var vc = View(-150, new[] { Leg(1, "BUY", 0.6, 4005, -200, t0), Leg(2, "SELL", 0.4, 3999, 50, t0) }, flat); var mc = Active(S, vc);
            var h1 = mc.Decide(S, vc); var h2 = mc.Decide(S, View(-150, new[] { Leg(1, "BUY", 0.6, 4005, -200, t0), Leg(2, "SELL", 0.4, 3999, 50, t0), Leg(3, "SELL", 0.2, 4000.5, 0, t0) }, flat));
            Check(h1.Kind == "ORDER" && h1.Tag == "HOLD" && h1.Dir == -1 && Eq(h1.Lots, 0.2) && h2.Kind == "NONE" && mc.Held, "🚑 HOLD: no trend → SELL 0.20 = net 0 once (no flip-flop in a chop, #83), then it waits", h1.Why + " | " + h2.Why);
        }
        // ---------------------------------------------------------------- 6. the stats math
        {
            Func<double, double, string, double, double, StepInEpisode> E = (x0, res, outc, worst, alone) => new StepInEpisode { Target = 10, Total0 = x0 * 10, X0 = x0, Result = res, Outcome = outc, Worst = worst, At = t0, End = t0.AddMinutes(60), Alone = alone, AloneReason = alone < -1000 ? "HARD STOP" : "TARGET" };
            var eps = new List<StepInEpisode> { E(-8, 12, "GREEN", -150, 10), E(-9, -2, "BE", -200, -4700), E(-25, -4000, "BLOWN", -4000, double.NaN), E(-12, 11, "GREEN", -130, 10), new StepInEpisode { Target = 10, X0 = -30, Total0 = -300, Worst = -310, Outcome = "OPEN" } };
            var st = StepInStats.Of(eps); var lines = st.Lines("🚑 T");
            Check(st.N == 5 && st.Open == 1 && st.Green == 2 && st.Be == 1 && st.Blown == 1 && Eq(st.Pct(st.Green), 50) && Eq(st.Pct(st.Blown), 25) && Eq(st.Net, 12 - 2 - 4000 + 11) && Eq(st.AtTrigger, -80 - 90 - 250 - 120)
                && st.WithAlone == 3 && Eq(st.AloneNet, 10 - 4700 + 10) && st.AloneGreen == 2 && st.AloneBlownOrStop == 1 && Eq(st.WorstX, -400) && Eq(st.AvgMin, 60)
                && st.Bands.Count == 3 && st.Bands[0].Item1 == "5–10×" && st.Bands[0].Item2 == 2 && st.Bands[2].Item1 == "20–40×" && st.Bands[2].Item5 == 1 && lines.Any(l => l.Contains("small sample")) && lines[0].Contains("BLOWN 1 (25 %)"),
                "🚑 stats: 4 finished — GREEN 2 (50 %), BE 1, BLOWN 1 (25 %), net, vs close at the trigger, vs the automation alone (2 green, 1 hard stop), per depth band, time to recover, \"small sample\"", string.Join(" / ", lines.Take(3)));
            Check(StepInEpisode.Classify(6, 10, false) == "GREEN" && StepInEpisode.Classify(-9, 10, false) == "BE" && StepInEpisode.Classify(-11, 10, false) == "LOSS" && StepInEpisode.Classify(50, 10, true) == "BLOWN" && StepInEpisode.Classify(double.NaN, 10, false) == "OPEN",
                "🚑 outcome words: ≥ ½ target GREEN, within one target BE, else LOSS, a stop-out BLOWN");
            var back = StepInEpisode.Parse(eps[0].Csv()); var e2 = new StepInEpisode { Account = "a,\"b\"", Why = "x, y", Outcome = "BE", Result = 1.5, Alone = double.NaN }; var b2 = StepInEpisode.Parse(e2.Csv());
            Check(back != null && back.Outcome == "GREEN" && Eq(back.Result, 12) && Eq(back.Alone, 10) && b2 != null && b2.Why == "x, y" && double.IsNaN(b2.Alone) && StepInEpisode.Header.Split(',').Length == 31,
                "🚑 …-stepin.csv: one line per episode, read back (quoted texts with commas)");
            Check(StepInStats.BlowCtx(null, null) == "no-stepin,,,," && StepInStats.BlowCtx(null, eps[2]) == "stepin,-25.0,-4000.00,0,BLOWN", "🚑 blowups-*.csv carry the step-in context", StepInStats.BlowCtx(null, eps[2]));
        }
        // ---------------------------------------------------------------- 7. MT5 accounts: OFF by default, the typed STEP IN / WHOLE ACCOUNT, DISARM / restart
        {
            StepInGate.RevokeAll(); var fb = new FakeBroker(); fb.St.Account.Kind = "REAL"; fb.St.Account.Login = 4242; fb.St.Account.Server = "Live-1"; fb.St.ServerNow = t0;
            fb.St.Symbols["XAUUSD"] = new Mt5Symbol { Name = "XAUUSD", TickSize = 0.01, TickValue = 1, VolStep = 0.01, VolMin = 0.01, Contract = 100 }; fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4000, Ask = 4000.2, Time = t0 };
            var s = new AutoSettings { Symbol = "XAUUSD", StepIn = true, StepInRisk = 50, EntryLots = 0.1, AddLots = 0.2, Target = 10 }; var au = new HedgeAuto(); string key = HedgeAuto.AccKeyOf(fb);
            string c0 = au.Check(fb, s), cOff = au.Check(fb, new AutoSettings { Symbol = "XAUUSD", EntryLots = 0.1, AddLots = 0.2, Target = 10 });
            bool wrong = StepInGate.Confirm(key, "step", 50), ok50 = StepInGate.Confirm(key, " step  in ", 50); string c1 = au.Check(fb, s);
            var s100 = s.Copy(); s100.StepInRisk = 100; string c100 = au.Check(fb, s100); bool noWhole = StepInGate.Confirm(key, "STEP IN", 100, "whole"), whole = StepInGate.Confirm(key, "STEP IN", 100, "WHOLE ACCOUNT"); string c100b = au.Check(fb, s100);
            StepInGate.Revoke(key); string cDis = au.Check(fb, s); StepInGate.Confirm(key, "STEP IN", 50); StepInGate.RevokeAll(); string cRestart = au.Check(fb, s);
            Check(!new AutoSettings().StepIn && c0 == StepInGate.NotConfirmed && cOff == "" && !wrong && ok50 && c1 == "" && c100 == StepInGate.NotConfirmed && !noWhole && whole && c100b == "" && cDis == StepInGate.NotConfirmed && cRestart == StepInGate.NotConfirmed && key == "4242@Live-1",
                "🚑 MT5 account: OFF by default; ON is refused until STEP IN is typed (\"step\" no); MAX RISK 100 % also needs WHOLE ACCOUNT; DISARM (revoke) and a SpasiX restart need the typed STEP IN again; OFF starts as before", c0 + " | " + c100 + " | " + cDis);
            Check(StepInGate.Dialog(50, 8000).Contains("ADDS LARGER LOTS WITH THE TREND") && StepInGate.Dialog(50, 8000).Contains("MAX RISK") && StepInGate.Dialog(50, 8000).Contains("AUTO RESTART never happens on a real account") && StepInGate.Dialog(50, 8000).Contains("never closes the basket at a loss") && StepInGate.LiveNote.Contains("PROP") && StepInGate.LiveNote.Contains("cannot bypass"),
                "🚑 the dialog says it plainly: larger lots with the trend, MAX RISK, never a loss close, no AUTO RESTART on a real account; the settings note: PROP's limits apply");
            // a running step-in automation stops at once when the confirmation ends (DISARM) — nothing sent
            StepInGate.Confirm(key, "STEP IN", 50); fb.St.Positions.Add(Leg(1, "BUY", 0.5, 4010, -500, t0.AddMinutes(-30))); au.Send = (c, a, l) => fb.Command(c, a); au.Start(fb, s, true); bool ran = au.Running;
            StepInGate.Revoke(key); fb.St.ServerNow = t0.AddSeconds(5); au.Step(fb);
            Check(ran && !au.Running && au.StopReason == StepInGate.NotConfirmed && fb.Cmds.Count == 0, "🚑 Step: a run on an MT5 account whose confirmation ended (DISARM / restart) stops at once, nothing sent", au.StopReason + " cmds " + fb.Cmds.Count);
            // PROP: the firm's limits still refuse — the step-in's order goes through the same Send, refused → nothing reaches MT5, the automation stops
            StepInGate.Confirm(key, "STEP IN", 50); var au2 = new HedgeAuto(); var asked = new List<string>(); au2.Send = (c, a, l) => { asked.Add(l); au2.Refused = "🏆 PROP: the daily loss limit is reached — new entries are refused"; return null; };
            var fb2 = new FakeBroker(); fb2.St.Account.Kind = "REAL"; fb2.St.Account.Login = 4242; fb2.St.Account.Server = "Live-1"; fb2.St.ServerNow = t0; fb2.St.Account.Balance = 10000; fb2.St.Account.Equity = 9500; fb2.St.Account.Margin = 400; fb2.St.Account.MarginLevel = 2375;
            fb2.St.Symbols["XAUUSD"] = fb.St.Symbols["XAUUSD"]; fb2.St.Quotes["XAUUSD"] = fb.St.Quotes["XAUUSD"]; fb2.St.Positions.Add(Leg(1, "BUY", 0.6, 4010, -600, t0.AddMinutes(-30))); fb2.St.Positions.Add(Leg(2, "SELL", 0.2, 3995, -100, t0.AddMinutes(-20)));
            au2.Start(fb2, s, true); bool act = false; for (int i = 1; i <= 3 && au2.Running; i++) { fb2.St.ServerNow = t0.AddSeconds(i); au2.Step(fb2); act |= au2.SI.Active; }
            Check(act && asked.Count == 1 && asked[0].Contains("🚑 HOLD") && fb2.Cmds.Count == 0 && !au2.Running && au2.StopReason.Contains("PROP"),
                "🚑 PROP: the step-in's order (here the HOLD to net 0, no trend data) goes through AutoSend like every order — the firm's limit refuses it, nothing reaches MT5, the automation stops (STEP IN cannot bypass PROP)", string.Join(" | ", asked) + " / " + au2.StopReason);
            // ✋ MANUAL MANAGE: the same action only becomes a 💡 suggestion
            var au3 = new HedgeAuto(); int sent3 = 0; au3.Send = (c, a, l) => { sent3++; return "X"; }; au3.SetManualManage(true); au3.Start(fb2, s, true); for (int i = 4; i <= 6; i++) { fb2.St.ServerNow = t0.AddSeconds(i); au3.Step(fb2); }
            Check(sent3 == 0 && au3.Suggestion.Contains("🚑 HOLD") && au3.Running, "🚑 ✋ MANUAL MANAGE: the step-in sends nothing, its action is a 💡 suggestion", au3.Suggestion + " sent " + sent3);
            StepInGate.RevokeAll();
        }
        // ---------------------------------------------------------------- 8. simulated runs: a recovery, a blow-up (stop-out) recorded, the reset (💥 AUTO RESTART) and on again
        {
            var r1 = RunSim(140, 1.8, 1500, 100); var e1 = r1.Item1.StepIns.FirstOrDefault();
            Check(r1.Item3 == "" && e1 != null && e1.Mode == "SIM" && e1.X0 <= -4 && e1.Stacks >= 1 && r1.Item1.Trades.Any(t => (t.Why ?? "").StartsWith("🚑 STACK")) && e1.Outcome == "GREEN" && e1.Result >= 0.5 * e1.Target && r1.Item1.Iterations[0].Reason == "🚑 STEP IN GOAL",
                "🚑 simulated recovery: a chop drove the basket to " + (e1 == null ? "—" : e1.X0.ToString("0.0", IC)) + "× the target, STEP IN froze it at net 0 (HOLD), then stacked SELL with the down trend (" + (e1 == null ? "—" : e1.Stacks + " stacks") + ") and closed at the goal " + (e1 == null ? "" : e1.Result.ToString("+0.00", IC)), e1 == null ? r1.Item3 : e1.Csv());
            var r2 = RunSim(4, 2, 1500, 500, 40, 0.5); var au = r2.Item1; var sb = r2.Item2; var e2 = au.StepIns.FirstOrDefault(); int so; double bal2; lock (sb.State.Gate) { so = sb.State.Deals.Count(d => d.Reason == "STOPOUT"); bal2 = sb.State.Account.Balance; }
            Check(e2 != null && so > 0 && e2.Blown && e2.Outcome == "BLOWN" && e2.Stacks >= 1 && StepInStats.BlowCtx(null, e2).StartsWith("stepin,") && StepInStats.BlowCtx(null, e2).EndsWith(",BLOWN") && StepInStats.Of(au.StepIns).Blown == 1,
                "🚑 simulated blow-up (MOVE × 0.5 = bigger stacks): stacked SELL, then a $40 gap up against the stack → the broker's stop-out (" + so + " legs, balance $" + bal2.ToString("0", IC) + ") → the episode is recorded BLOWN with its context for blowups-*.csv (" + (e2 == null ? "" : StepInStats.BlowCtx(null, e2)) + ")", e2 == null ? "no episode" : e2.Csv() + " so " + so);
            // 💥 AUTO RESTART (as the DEMO / RACE do it): the balance back to the start by a balance deal, the automation again — STEP IN armed again
            lock (sb.State.Gate) sb.Account.SetBalance(1500, sb.State.ServerNow); if (au.Running) au.Stop("💥 blow-up: restart"); var s2 = au.Set.Copy(); string chk2 = au.Check(sb, s2); if (chk2 == "") au.Start(sb, s2, false); double balR; lock (sb.State.Gate) balR = sb.State.Account.Balance;
            Check(chk2 == "" && au.Running && au.Set.StepIn && !au.SI.Active && au.StepIns.Count == 1 && Eq(balR, 1500), "🚑 after the reset ($1,500 again) the automation starts again with STEP IN armed — the blown episode stays in the statistics", chk2 + " " + balR);
        }
        Console.WriteLine(fails == 0 ? "ALL 09n TESTS PASSED" : fails + " 09n TEST(S) FAILED");
        return fails;
    }
}
