// 09e: 🚀 TREND STACK (experiment) — the short-term trend detector on hand-made candles, the stack size and its caps, stacking with the trend in
// a backtest, the flip that closes the stacks, never on an MT5 account (REAL / PROP), its 🏁 RACE lane sends nothing
using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using KeystoneMT5;
public static class NineETests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static readonly DateTime Day0 = new DateTime(2026, 10, 20), Day1 = new DateTime(2026, 10, 21);
    static List<Mt5Bar> Path(DateTime start, double first, IEnumerable<double> closes)
    {
        var res = new List<Mt5Bar>(); double o = first; int i = 0;
        foreach (var c in closes) { res.Add(new Mt5Bar { Time = start.AddMinutes(i++), O = o, C = c, H = Math.Round(Math.Max(o, c) + 0.1, 2), L = Math.Round(Math.Min(o, c) - 0.1, 2), V = 100, Spread = 10 }); o = c; }
        return res;
    }
    // a zig-zag: `up` candles of +stepUp, then `dn` candles of −stepDn, repeated (a trend with pullbacks = swings to break)
    static IEnumerable<double> Zz(double from, int n, int up, double stepUp, int dn, double stepDn)
    {
        double p = from; int k = 0; for (int i = 0; i < n; i++) { p += (k < up ? stepUp : -stepDn); k = (k + 1) % (up + dn); yield return Math.Round(p, 2); }
    }
    static IEnumerable<double> Line(double from, double step, int n) { for (int i = 1; i <= n; i++) yield return Math.Round(from + step * i, 2); }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static AutoSettings S() { return new AutoSettings { OnManual = "ADOPT", HedgeSize = "FIXED", Rolling = false, Swing = false, FvgTf = 1, Symbol = "XAUUSD", Tf = 1, Lookback = 1, EntryLots = 0.1, AddLots = 0.2, Target = 20, FirstDir = "AUTO", NextStart = "BREAK" }; }
    sealed class FakeBroker : IBroker
    {
        public readonly Mt5State St = new Mt5State(); public readonly List<string> Cmds = new List<string>();
        public Mt5State State { get { return St; } } public bool Send(string line) { return true; } public string Command(string cmd, params object[] a) { Cmds.Add(cmd); return "F" + Cmds.Count; }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
    }
    static Tuple<SimBroker, HedgeAuto, int> Run(List<Mt5Bar> test, AutoSettings s, int steps = 6000)
    {
        var warm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30));
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm.Concat(test).ToList()); if (err != "") throw new Exception(err);
        var au = new HedgeAuto(); int sends = 0; au.Send = (c, a, l) => { sends++; return sb.Command(c, a); }; sb.OnTickHook = () => au.Step(sb);
        var chk = au.Check(sb, s); if (chk != "") throw new Exception(chk);
        au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
        for (int k = 0; k < steps && !sb.Ended; k++) sb.Advance(0.25);
        return Tuple.Create(sb, au, sends);
    }

    public static int Main8()
    {
        // ---------------------------------------------------------------- 1. the detector on hand-made candles
        var up = Path(Day1, 4000, Zz(4000, 160, 4, 0.5, 2, 0.3)); var dn = Path(Day1, 4000, Zz(4000, 160, 4, -0.5, 2, -0.3)); var flat = Path(Day1, 4000, Enumerable.Range(0, 160).Select(i => 4000 + (i % 2 == 0 ? 0.3 : -0.3)));
        var tu = TrendDetect.Read(up.Take(up.Count - 1).ToList(), 3990); var td = TrendDetect.Read(dn, 4010); var tf = TrendDetect.Read(flat, 4000); var tfew = TrendDetect.Read(up.Take(20).ToList(), double.NaN);
        Check(tu.Dir == 1 && tu.Strength >= 60 && tu.Why.Contains("EMA9>21") && tu.Why.Contains("above VWAP") && tu.Text.StartsWith("📈 TREND UP"), "🚀 detector: a rising zig-zag (+0.5 ×4, −0.3 ×2) = TREND UP " + tu.Strength + " · " + tu.Why, tu.Text);
        Check(td.Dir == -1 && td.Strength >= 60 && td.Why.Contains("EMA9<21") && td.Why.Contains("below VWAP") && td.Text.StartsWith("📉 TREND DOWN"), "🚀 detector: the mirror = TREND DOWN " + td.Strength, td.Text);
        Check(tf.Dir == 0 && tfew.Dir == 0 && tfew.Why == "not enough candles", "🚀 detector: a flat chop → NO TREND (" + tf.Strength + "); 20 candles → not enough", tf.Text + " | " + tfew.Text);
        {   // a strong run, then a sharp turn: the EMA 9 crosses under the 21 → CrossDown, the state leaves UP
            var turn = Path(Day1, 4000, Line(4000, 0.4, 100).Concat(Line(4040, -1.2, 20)).ToList()); bool cross = false; int firstNotUp = -1;
            for (int n = 100; n <= turn.Count; n++) { var x = TrendDetect.Read(turn.Take(n).ToList(), double.NaN); if (x.CrossDown) cross = true; if (firstNotUp < 0 && x.Dir != 1) firstNotUp = n; }
            Check(cross && firstNotUp > 100 && firstNotUp <= 104, "🚀 detector: a sharp turn after a run → the state leaves UP within 4 candles and the EMA 9/21 cross DOWN is seen", cross + " " + firstNotUp);
        }
        {   // finished candles only: the reading of n candles never changes when a later candle arrives
            var a1 = TrendDetect.Read(up.Take(120).ToList(), 3990); var a2 = TrendDetect.Read(up.Take(121).ToList().Take(120).ToList(), 3990);
            Check(a1.Text == a2.Text && a1.Bar == up[119].Time, "🚀 detector: reads only the candles it is given (finished ones) — the same input, the same state", a1.Text + " | " + a2.Text);
        }
        // ---------------------------------------------------------------- 2. the stack size and its caps
        string w1, w2, w3, w4; double l1 = TrendDetect.StackLots(-266, 16, 6, 100, 0.08, 0.01, 0.56, out w1), l2 = TrendDetect.StackLots(-266, 16, 6, 100, 0.08, 0.01, 0.30, out w2), l3 = TrendDetect.StackLots(-266, 16, 6, 100, 0.56, 0.01, 0.56, out w3), l4 = TrendDetect.StackLots(-10, 16, 6, 100, 0.08, 0.01, 0.56, out w4);
        Check(Math.Abs(l1 - 0.39) < 1e-9 && w1 == "" && Math.Abs(l2 - 0.22) < 1e-9 && l3 == 0 && w3.Contains("cap") && Math.Abs(l4 - 0.01) < 1e-9,
            "🚀 stack size (a long basket −$266, target $16, move $6, $100 per 1.0 per lot, net +0.08): (282 ÷ 600) − 0.08 = BUY 0.39; cap 0.30 → 0.22; at the cap → none; a small debt → the step 0.01", l1 + " " + l2 + " " + l3 + " " + w3 + " " + l4);
        {   // the user's DEMO #43 (09d): BUY 0.16@4145.90 + 0.16@4145.30 + 0.08@4142.14, SELL 0.16@4137.38 + 0.16@4137.03 → net +0.08, −$266, target $16,
            // balance $7,954.49; ATR(M5) at that hour of his export ≈ $4.4 → expected move 1.5 × 4.4 = $6.60; a DOWN trend → the stack is a SELL
            double cap = Math.Round(7954.49 / 1000 * 0.07, 2); string why; double lots = TrendDetect.StackLots(-266, 16, 6.6, 100, 0.08 * -1, 0.01, cap, out why);
            double net = Math.Round(0.08 - lots, 2), be = HedgeAuto.PriceForTotal(-266, 0, net, 100, 4139.31), tp = HedgeAuto.PriceForTotal(-266, 16, net, 100, 4139.31);
            double beHis = HedgeAuto.PriceForTotal(-266, 0, -0.57, 100, 4139.31), tpHis = HedgeAuto.PriceForTotal(-266, 16, -0.57, 100, 4139.31); string wh; double his = TrendDetect.StackLots(-266, 16, 4.6, 100, -0.08, 0.01, cap, out wh);
            Check(Math.Abs(cap - 0.56) < 1e-9 && Math.Abs(lots - 0.51) < 1e-9 && Math.Abs(net + 0.43) < 1e-9 && Math.Abs(be - 4133.12) < 0.005 && Math.Abs(tp - 4132.75) < 0.005 && Math.Abs(beHis - 4134.64) < 0.005 && Math.Abs(tpHis - 4134.36) < 0.005 && Math.Abs(his - 0.64) < 1e-9,
                "🚀 #43 (net +0.08, −$266, target $16, ATR M5 $4.4): the formula SELLs 0.51 (his 0.65) → net SHORT 0.43 = $43 per $1, breakeven 4133.12, +$16 at 4132.75 (his: $57 per $1, 4134.64 / 4134.36); the net cap 0.56 ($7,954) allows at most SELL 0.64 — his 0.65 is over it",
                cap + " " + lots + " " + net + " " + be + " " + tp + " | his " + beHis + " " + tpHis + " " + his);
        }
        // ---------------------------------------------------------------- 3. stacking with the trend in a backtest, then the target
        {   // BUY 0.10 at once, the price falls $10 (−$100), then climbs in a zig-zag: stacks BUY at the swing breaks, never SELL, under the caps, until the target
            var path = Path(Day1, 4000, Line(4000, -0.25, 40).Concat(Zz(3990, 300, 4, 0.5, 2, 0.3)).ToList());
            var s = S(); s.FirstDir = "BUY"; s.HedgeMode = "NONE"; s.Target = 20; s.MaxIterations = 1; s.TrendStack = true; s.StackMax = 3;
            var r = Run(path, s); var au = r.Item2; var st = au.Trades.Where(t => t.Why.StartsWith("🚀 STACK")).ToList(); var it = au.Iterations.FirstOrDefault();
            Check(st.Count >= 1 && st.Count <= 3 && st.All(t => t.Side == "BUY") && st.All(t => t.Lots >= 0.01 && t.Lots <= 0.70 + 1e-9) && it != null && it.Done && it.Reason == "TARGET" && it.Result >= 20 && au.StacksTotal == st.Count
                && st[0].Why.Contains("trend UP") && st[0].Why.Contains("to target") && st[0].Why.Contains("cap 0.7 net") && au.Story.Any(x => x.Item2.Contains("🚀 STACK 1/3")),
                "🚀 backtest: a −$100 basket, then an up-trend → " + st.Count + " stack(s) BUY (" + string.Join(", ", st.Select(t => t.Lots.ToString("0.##", IC))) + "), each with its numbers in the banner, closed at the TARGET (" + (it == null ? "" : it.Result.ToString("0.00", IC)) + ")",
                st.Count + " " + string.Join(" | ", st.Select(t => t.Side + " " + t.Lots + " " + t.Why)) + " / " + (it == null ? "none" : it.Reason + " " + it.Result));
            var s2 = s.Copy(); s2.TrendStack = false; var r0 = Run(path, s2); var it0 = r0.Item2.Iterations.FirstOrDefault();
            Check(it0 != null && it != null && (!it0.Done || it0.End > it.End), "🚀 the same path without stacking: the basket closes later (or not at all)", it0 == null ? "none" : it0.Done + " " + it0.End.ToString("HH:mm") + " vs " + it.End.ToString("HH:mm"));
        }
        {   // the flip: stacks in an up-trend, then a sharp fall → the stacked part closes at once (FLIP), the first BUY stays
            var path = Path(Day1, 4000, Line(4000, -0.25, 40).Concat(Zz(3990, 120, 4, 0.5, 2, 0.3)).Concat(Line(4009.4, -1.0, 25)).Concat(Enumerable.Repeat(3984.4, 30)).ToList());
            var s = S(); s.FirstDir = "BUY"; s.HedgeMode = "NONE"; s.Target = 5000; s.MaxIterations = 1; s.TrendStack = true; s.BankAt = 101;
            var r = Run(path, s); var au = r.Item2; var st = au.Trades.Where(t => t.Why.StartsWith("🚀 STACK") && t.Side == "BUY").ToList(); var fl = au.Trades.Where(t => t.Why.StartsWith("🚀 FLIP")).ToList();
            var sells = au.Trades.Where(t => t.Why.StartsWith("🚀 STACK") && t.Side == "SELL").ToList(); bool firstOpen; lock (r.Item1.State.Gate) firstOpen = r.Item1.State.Positions.Any(p => p.Side == "BUY" && Math.Abs(p.Lots - 0.1) < 1e-9);
            Check(st.Count >= 1 && fl.Count >= st.Count && fl.Take(st.Count).All(f => f.Time > st.Last().Time && f.Why.Contains("BUY")) && (sells.Count == 0 || sells[0].Time >= fl[st.Count - 1].Time) && firstOpen && au.Stacks <= s.StackMax && au.Story.Any(x => x.Item2.Contains("🚀 FLIP")),
                "🚀 the flip: " + st.Count + " BUY stack(s) in the up-trend, the trend turns → every BUY stack closed at once (FLIP) before anything else, the basket's own BUY 0.10 kept; at most " + s.StackMax + " stacks per basket", st.Count + " / " + fl.Count + " sells " + sells.Count + " first " + firstOpen + " stacks " + au.Stacks);
        }
        {   // 🏦 bank: a strong trend closes the other side's legs that are in profit (they stay in the total)
            var path = Path(Day1, 4000, Line(4000, 0.1, 20).Concat(Line(4002, -0.4, 30)).Concat(Zz(3990, 200, 4, 0.5, 2, 0.3)).ToList());
            var s = S(); s.FirstDir = "BUY"; s.HedgeMode = "PRICE"; s.HedgePrice = 3; s.HedgeSize = "FIXED"; s.AddLots = 0.1; s.Target = 500; s.MaxIterations = 1; s.TrendStack = true; s.BankAt = 60; s.StackMax = 1;
            var r = Run(path, s); var au = r.Item2; var bk = au.Trades.Where(t => t.Why.StartsWith("🏦 BANK")).ToList();
            Check(bk.Count >= 1 && bk.All(t => t.Why.Contains("SELL")) && au.StackBanks == bk.Count, "🚀 BANK: in the strong up-trend the hedge SELL in profit is closed (" + bk.Count + "), counted in the total", bk.Count + " " + string.Join(" | ", au.Trades.Select(t => t.Side + " " + t.Lots + " " + t.Why.Split('—')[0])));
        }
        {   // ✋ MANUAL MANAGE: the stacks are only 💡 suggestions
            var path = Path(Day1, 4000, Line(4000, -0.25, 40).Concat(Zz(3990, 200, 4, 0.5, 2, 0.3)).ToList());
            var s = S(); s.FirstDir = "BUY"; s.HedgeMode = "NONE"; s.Target = 500; s.MaxIterations = 1; s.TrendStack = true;
            var warm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm.Concat(path).ToList());
            var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60;
            for (int k = 0; k < 30; k++) sb.Advance(0.25); au.SetManualManage(true); bool sugg = false; for (int k = 0; k < 4000 && !sb.Ended; k++) { sb.Advance(0.25); if (au.Suggestion.Contains("🚀 STACK")) sugg = true; }
            int pos; lock (sb.State.Gate) pos = sb.State.Positions.Count;
            Check(sugg && pos == 1, "🚀 ✋ MANUAL MANAGE: a stack is only a 💡 suggestion — nothing sent", sugg + " pos " + pos);
        }
        {   // ⏸ 09e fix of 09d's STOP ON MANUAL: a leg closed by the BROKER (its SL here; a stop-out the same) is not "traded by hand"
            var warm = Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)); var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm.Concat(Path(Day1, 4000, Enumerable.Repeat(4000.0, 5).Concat(Line(4000, -0.5, 20)).Concat(Enumerable.Repeat(3990.0, 20)))).ToList());
            sb.Playing = true; sb.Speed = 60; sb.Advance(0.25); sb.Command("ORDER", "XAUUSD", "BUY", 0.1, 0.0, 3997.0, 0.0); sb.Advance(0.05); sb.Command("ORDER", "XAUUSD", "BUY", 0.05, 0.0, 0.0, 0.0); sb.Advance(0.05);
            var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); var s = S(); s.OnManual = "STOP"; s.HedgeMode = "NONE"; s.Target = 500; au.Check(sb, s); au.Start(sb, s, true);
            int pos0 = sb.State.Positions.Count; for (int k = 0; k < 400 && !sb.Ended; k++) sb.Advance(0.25); int pos1; bool slDeal; lock (sb.State.Gate) { pos1 = sb.State.Positions.Count; slDeal = sb.State.Deals.Any(d => d.Reason == "SL"); }
            Check(pos0 == 2 && pos1 == 1 && slDeal && au.Running && !au.HandStopped, "⏸ fix: the broker's SL closed one leg → the automation goes on (not \"traded by hand\"); a stop-out counts the same", pos0 + " → " + pos1 + " sl " + slDeal + " running " + au.Running + " " + au.HandWhat);
        }
        // ---------------------------------------------------------------- 4. never on an MT5 account
        {
            var s = S(); s.TrendStack = true; var real = new FakeBroker(); real.St.Account.Kind = "REAL"; var prop = new FakeBroker(); prop.St.Account.Kind = "DEMO"; var au = new HedgeAuto();
            var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, Kind = "STANDARD", FixedSpread = 10 }, Gold(), Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)).Concat(Path(Day1, 4000, Enumerable.Repeat(4000.0, 30))).ToList());
            string cr = au.Check(real, s), cp = au.Check(prop, s), cs = au.Check(sb, s), cn = au.Check(null, s); var off = s.Copy(); off.TrendStack = false;
            // a START that slipped past the check (e.g. settings loaded on a live window) stops at its first step, nothing sent
            real.St.ServerNow = Day1.AddHours(12); real.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4000, Ask = 4000.2, Time = real.St.ServerNow }; au.Send = (c, a, l) => real.Command(c, a); au.Start(real, s, false); au.Step(real);
            Check(cr == AutoSettings.TrendStackNo && cp == AutoSettings.TrendStackNo && cn == AutoSettings.TrendStackNo && cs == "" && au.Check(real, off) != AutoSettings.TrendStackNo && !AutoSettings.TrendStackAllowed(false) && AutoSettings.TrendStackAllowed(true)
                && !au.Running && au.StopReason.Contains("EXPERIMENT") && real.Cmds.Count == 0 && AutoSettings.Load(s.Save()).TrendStack && !new AutoSettings().TrendStack,
                "🚀 never on an MT5 account: START refused on a REAL and on an MT5 demo / PROP link (any non-simulated broker), allowed on the simulator; a run that slipped through stops at once, nothing sent; OFF by default", cr + " | " + cs + " | " + au.StopReason);
        }
        // ---------------------------------------------------------------- 5. the 🏁 RACE lane
        {
            var mine = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 5000); var set = RaceLanes.For("TST", mine, 5000); set.Swing = false;
            var mk = new SimBroker(); mk.Load(new SimSettings { Symbol = "XAUUSD", From = Day1, To = Day1, Balance = 10000, Kind = "STANDARD", FixedSpread = 10 }, Gold(), Path(Day0.AddHours(10), 4000, Enumerable.Repeat(4000.0, 30)).Concat(Path(Day1, 4000, Line(4000, -0.2, 40).Concat(Zz(3992, 300, 4, 0.5, 2, 0.3)))).ToList());
            int sent = 0; var l = new RaceLane { Key = "TST", Set = set, Br = new ShadowBroker(mk.State, line => { if (line.StartsWith("ORDER") || line.StartsWith("CLOSE")) sent++; return false; }, new SimAccountSettings { Balance = 5000, Leverage = 500, Kind = "STANDARD" }) };
            l.Eng.Send = (c, a, lab) => l.Br.Command(c, a); var e = l.Eng.Check(l.Br, set); if (e == "") l.Eng.Start(l.Br, set, false);
            mk.OnTickHook = () => l.Step(); mk.Playing = true; mk.Speed = 60; for (int k = 0; k < 4000 && !mk.Ended; k++) mk.Advance(0.25);
            int mkPos; lock (mk.State.Gate) mkPos = mk.State.Positions.Count + mk.State.Deals.Count(d => d.Type != "BALANCE");
            Check(e == "" && set.TrendStack && RaceLanes.IsExperimental("TST") && RaceLanes.Tip("TST").Contains("EXPERIMENTAL") && sent == 0 && mkPos == 0 && l.Eng.Trades.Count > 0,
                "🚀 the 🏁 RACE lane TREND STACK runs on its own simulated account: " + l.Eng.Trades.Count + " trades, 0 orders sent, the market's account untouched", e + " sent " + sent + " mk " + mkPos + " trades " + l.Eng.Trades.Count);
        }
        Console.WriteLine(fails == 0 ? "ALL 09e TESTS PASSED" : fails + " 09e TEST(S) FAILED");
        return fails;
    }
}
