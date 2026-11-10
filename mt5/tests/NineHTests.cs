// 09h: ⚠ DANGER RADAR — the calibrated danger of a basket at its hedges (display + alert only), the 5 most similar past baskets, the optional
// actions (OFF by default) and the TAH lane
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using KeystoneMT5;
public static class NineHTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static string Data() { var d = AppContext.BaseDirectory; for (int i = 0; i < 6 && !Directory.Exists(Path.Combine(d, "data")); i++) d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar)); return Path.Combine(d, "data"); }
    static string AppDir() { return Path.Combine(Path.GetDirectoryName(Data()), "..", "app"); }
    static List<Mt5Bar> Candles() { return File.ReadAllLines(Path.Combine(Data(), "xau-m1-1105-1110.csv")).Skip(1).Select(l => l.Split(',')).Select(x => new Mt5Bar { Time = DateTime.ParseExact(x[0], "yyyy-MM-dd HH:mm:ss", IC), O = double.Parse(x[1], IC), H = double.Parse(x[2], IC), L = double.Parse(x[3], IC), C = double.Parse(x[4], IC), V = long.Parse(x[5], IC), Spread = 25 }).ToList(); }
    sealed class FakeBroker : IBroker
    {
        public readonly Mt5State St = new Mt5State(); public readonly List<string> Cmds = new List<string>();
        public Mt5State State { get { return St; } } public bool Send(string line) { return true; } public string Command(string cmd, params object[] a) { Cmds.Add(cmd); return "F" + Cmds.Count; }
        public string Problem { get { return ""; } } public int Port { get { return 0; } } public bool IsSim { get { return false; } }
    }
    static Mt5Position Leg(long tk, string side, double lots, double open, DateTime t, long magic = 770077) { return new Mt5Position { Ticket = tk, Symbol = "XAUUSD", Side = side, Lots = lots, Open = open, Time = t, Magic = magic }; }
    // his DEMO #47 (ICMarkets): the automation's BUY 0.08 @4117.45, SELL 0.16 @4112.37, BUY 0.16 @4118.03; then BY HAND BUY 0.65 @4118.97 and BUY 0.35 @4119.25
    static Tuple<FakeBroker, HedgeAuto, List<string>> His47(AutoSettings s)
    {
        var fb = new FakeBroker(); var t0 = new DateTime(2026, 11, 10, 9, 0, 0); fb.St.ServerNow = t0.AddMinutes(30); fb.St.Account.Kind = "DEMO"; fb.St.Account.Balance = 8000;
        fb.St.Symbols["XAUUSD"] = new Mt5Symbol { Name = "XAUUSD", TickSize = 0.01, TickValue = 1, VolStep = 0.01, VolMin = 0.01 }; fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = 4118.90, Ask = 4119.15, Time = fb.St.ServerNow };
        fb.St.Positions.Add(Leg(1, "BUY", 0.08, 4117.45, t0)); fb.St.Positions.Add(Leg(2, "SELL", 0.16, 4112.37, t0.AddMinutes(5))); fb.St.Positions.Add(Leg(3, "BUY", 0.16, 4118.03, t0.AddMinutes(10)));
        Price47(fb, 4118.90);
        var au = new HedgeAuto(); au.Send = (c, a, l) => fb.Command(c, a); var closes = new List<string>(); au.GuardClose = sym => { closes.Add(sym); return "G" + closes.Count; };
        au.Start(fb, s, true); return Tuple.Create(fb, au, closes);
    }
    static void Price47(FakeBroker fb, double bid) { fb.St.Quotes["XAUUSD"] = new Mt5Quote { Bid = bid, Ask = bid + 0.25, Time = fb.St.ServerNow }; foreach (var p in fb.St.Positions) p.Profit = Math.Round((p.Dir > 0 ? bid - p.Open : p.Open - (bid + 0.25)) * p.Lots * 100, 2); }
    static void Hand47(FakeBroker fb, long tk, double lots, double px)
    {
        var t = fb.St.ServerNow.AddSeconds(30); fb.St.ServerNow = t; fb.St.Positions.Add(Leg(tk, "BUY", lots, px, t, 0));
        fb.St.Deals.Add(new Mt5Deal { Ticket = 900 + tk, Position = tk, Time = t, Symbol = "XAUUSD", Type = "BUY", Entry = "IN", Lots = lots, Price = px, Reason = "DESKTOP" }); fb.St.ServerNow = t.AddSeconds(1);
    }
    static void Flat47(FakeBroker fb, string reason)
    {   // every leg closes at the current prices: one OUT deal each with its P/L
        var t = fb.St.ServerNow.AddSeconds(5); fb.St.ServerNow = t; var q = fb.St.Quotes["XAUUSD"];
        foreach (var p in fb.St.Positions) fb.St.Deals.Add(new Mt5Deal { Ticket = 5000 + p.Ticket, Position = p.Ticket, Time = t, Symbol = "XAUUSD", Type = p.Dir > 0 ? "SELL" : "BUY", Entry = "OUT", Lots = p.Lots, Price = p.Dir > 0 ? q.Bid : q.Ask, Profit = p.Profit, Reason = reason });
        fb.St.Positions.Clear(); fb.St.ServerNow = t.AddSeconds(1);
    }
    static AutoSettings S47() { return new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.08, AddLots = 0.16, Target = 16, HedgeMode = "NONE", HedgeSize = "FIXED", Swing = false, Rolling = false, OnManual = "STOP", FirstDir = "AUTO" }; }
    public static int Main11()
    {
        var model = RadarModel.Parse(File.ReadAllText(Path.Combine(AppDir(), "danger-model.txt"))); RadarModel.Default = model;
        bool mono = true; for (int i = 1; i < model.Bins.Count; i++) if (model.Bins[i].Cal < model.Bins[i - 1].Cal - 1e-9) mono = false;
        var cal = DangerRadar.CalibrationText(model);
        Check(model.Ready && model.Bins.Count == 10 && Math.Abs(model.Auc - 0.687) < 0.0006 && model.Samples == 471 && model.Positives == 186 && model.Library.Count == 479 && model.Names.Length == 10 && model.WalkForward.Count == 6 && mono
            && cal[0].Contains("AUC 0.69") && cal.Last().Contains("badly calibrated"),
            "⚠ the radar's model: 10 features, walk-forward AUC 0.687 on 471 hedges (186 went below −10 × the target), 10 calibration bands (monotone), 6 walk-forward models, 479 past baskets to compare", model.Auc + " " + model.Samples + " " + model.Bins.Count + " " + model.Library.Count);
        // #43 (export_1109c) at its 2nd hedge: BUY 0.08 @4142.14 22:56:46 → SELL 0.16 @4137.38 23:26:41 → BUY 0.16 @4145.30 23:29:56 (the basket −$109.78 at
        // that fill, 03-trades.csv), net +0.08, target $16 → read when that minute has finished (23:30:00), as the research sampled it
        var bars = Candles(); var at = new DateTime(2026, 11, 9, 23, 30, 0);
        var r = DangerRadar.Read(model, bars.Where(b => b.Time <= at).ToList(), at, new DateTime(2026, 11, 9, 22, 56, 46), -109.78, 16, 0.08, 2);
        var r2 = DangerRadar.Read(model, bars, at, new DateTime(2026, 11, 9, 22, 56, 46), -109.78, 16, 0.08, 2);
        Console.WriteLine("  #43 radar: raw " + r.Raw.ToString("0.0000", IC) + " → " + r.Text + " | " + string.Join(" / ", r.Near.Select(n => n.Text + " d=" + n.Dist.ToString("0.000", IC))));
        Check(r.X != null && !double.IsNaN(r.P) && r.Text.StartsWith("⚠ DANGER ") && r.Text.Contains("below −$160") && r.Near.Count == 5 && r.Near.Zip(r.Near.Skip(1), (a, b) => a.Dist <= b.Dist).All(x => x)
            && r2.Text == r.Text && r2.Near.Select(n => n.Date).SequenceEqual(r.Near.Select(n => n.Date)) && Math.Abs(r.P - r.Bin.Cal) < 1e-12,
            "⚠ #43 at its 2nd hedge (23:30): \"" + r.Text + "\" — the 5 nearest past baskets in a fixed order; candles after 23:30 change nothing (only finished candles)", r.Text);
        Check(Math.Abs(r.Raw - 0.6353) < 0.0005 && Math.Abs(r.P - 0.4375) < 0.0005 && r.Text.StartsWith("⚠ DANGER 44 %") && Math.Abs(r.StuckH - 1.93) < 1e-9 && r.Near[0].Date == "2026-01-23 21:47" && Math.Abs(r.Near[0].Depth + 18.4) < 0.05,
            "⚠ #43 exact: raw score 0.635 → its band says 44 % (observed in 2021–26), median stuck 1.9 h; the nearest past basket 2026-01-23 21:47 (went −18.4 × the target, 2.1 days, RESCUE BE)", r.Raw + " " + r.P + " " + r.StuckH + " " + (r.Near.Count > 0 ? r.Near[0].Date : ""));
        {   // the radar alone sends NOTHING: the same run with and without the model → the same trades; the options are OFF by default
            Func<bool, Tuple<int, double, int>> run = withModel =>
            {
                RadarModel.Default = withModel ? model : new RadarModel(); var sb = new SimBroker(); var sel = bars.Where(b => b.Time >= new DateTime(2026, 11, 7) || true).ToList();
                sb.Load(new SimSettings { Symbol = "XAUUSD", From = new DateTime(2026, 11, 9), To = new DateTime(2026, 11, 10), Balance = 8000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 25 }, new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }, sel);
                var s = AutoSettings.Load(string.Join(";", File.ReadAllLines(Path.Combine(Data(), "settings-1109c.txt")))); s.TrackManual = false; s.OnManual = "ADOPT"; s.Windows = "";
                var au = new HedgeAuto(); int readings = 0; au.OnRadar += x => { if (!double.IsNaN(x.P)) readings++; }; au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 250;
                for (int k = 0; k < 200000 && !sb.Ended; k++) sb.Advance(0.5);
                return Tuple.Create(au.Trades.Count, Math.Round(au.Iterations.Where(x => x.Done).Sum(x => x.Result), 2), readings);
            };
            var a1 = run(true); var a0 = run(false); RadarModel.Default = model; var d = new AutoSettings(); var l0 = AutoSettings.Load("sym=XAUUSD");
            Check(a1.Item3 > 0 && a1.Item1 == a0.Item1 && a1.Item2 == a0.Item2 && !d.RadarNoAdd && !d.RadarLock && !d.RadarBe && !d.SlopeFilter && !l0.RadarNoAdd && !l0.RadarLock && !l0.RadarBe && d.RadarAt == 50 && d.RadarAlert == 35,
                "⚠ display + alert only: " + a1.Item3 + " readings on 9–10 Nov with his settings, the same " + a1.Item1 + " trades / " + a1.Item2.ToString("0.00", IC) + " as without the radar; its actions are OFF by default (alert at 35 %, act at 50 %)", a1 + " vs " + a0);
        }
        {   // the options when ticked (a model forced to 90 %): no more add-ons / close at the next breakeven
            var hot = RadarModel.Parse(File.ReadAllText(Path.Combine(AppDir(), "danger-model.txt"))); foreach (var b in hot.Bins) b.Cal = 0.9; RadarModel.Default = hot;
            Func<Action<AutoSettings>, HedgeAuto> run = tune =>
            {
                var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = new DateTime(2026, 11, 9), To = new DateTime(2026, 11, 10), Balance = 8000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 25 }, new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }, bars);
                var s = AutoSettings.Load(string.Join(";", File.ReadAllLines(Path.Combine(Data(), "settings-1109c.txt")))); s.TrackManual = false; s.OnManual = "ADOPT"; s.Windows = ""; tune(s);
                var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb); au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 250; for (int k = 0; k < 200000 && !sb.Ended; k++) sb.Advance(0.5); return au;
            };
            var na = run(x => x.RadarNoAdd = true); var be = run(x => x.RadarBe = true); var none = run(x => { });
            Check(na.Story.Any(x => x.Item2.Contains("⚠ RADAR → NO ADD-ON")) && na.Trades.Count(t => t.Why.StartsWith("ADD-ON")) < none.Trades.Count(t => t.Why.StartsWith("ADD-ON")) && be.Story.Any(x => x.Item2.Contains("⚠ RADAR → CLOSE AT THE NEXT BREAKEVEN")),
                "⚠ the options act only when ticked: NO MORE ADD-ONS skips the hedges above the level (" + na.Trades.Count(t => t.Why.StartsWith("ADD-ON")) + " add-ons vs " + none.Trades.Count(t => t.Why.StartsWith("ADD-ON")) + "); CLOSE AT THE NEXT BREAKEVEN arms itself", "");
            RadarModel.Default = model;
        }
        {   // the RACE lanes: experimental, built from MY SETTINGS
            var mine = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000);
            Check(RaceLanes.For("RNA", mine, 8000).RadarNoAdd && RaceLanes.For("RLK", mine, 8000).RadarLock && RaceLanes.For("RBE", mine, 8000).RadarBe && RaceLanes.For("TAH", mine, 8000).SlopeFilter && new[] { "RNA", "RLK", "RBE", "TAH" }.All(RaceLanes.IsExperimental)
                && RaceLanes.Tip("TAH").Contains("opposite rule did about as well") && !mine.SlopeFilter,
                "⚠ 🏁 RACE lanes RNA / RLK / RBE (the radar options) and TAH (H1 EMA 20 slope entries, \"the opposite rule did about as well — unproven\"), all experimental", "");
            double up = DangerRadar.H1Ema20Slope3(Enumerable.Range(0, 60 * 30).Select(i => new Mt5Bar { Time = new DateTime(2026, 11, 2).AddMinutes(i), O = 4000 + i * 0.01, H = 4000 + i * 0.01 + 0.1, L = 4000 + i * 0.01 - 0.1, C = 4000 + i * 0.01 }).ToList(), new DateTime(2026, 11, 3, 6, 0, 0));
            Check(up > 0, "🧪 TAH: a rising H1 series → a positive 3-hour EMA 20 slope (" + up.ToString("0.00", IC) + ")", up + "");
        }
        {   // ➕ ADD ON BREAK — his DEMO #47: BUY 0.08 @4117.45 → SELL 0.16 @4112.37 → BUY 0.16 @4118.03 (net +0.08), total −$51.62, target $16 → TP 4131.52
            // (bid 4123.07, ask 4123.32), the next hedge SELL 0.16 @4112.22, a clean bullish break
            var t0 = new DateTime(2026, 11, 10, 10, 0, 0);
            var legs = new List<Mt5Position> { new Mt5Position { Ticket = 1, Symbol = "XAUUSD", Side = "BUY", Lots = 0.08, Open = 4117.45, Time = t0 }, new Mt5Position { Ticket = 2, Symbol = "XAUUSD", Side = "SELL", Lots = 0.16, Open = 4112.37, Time = t0.AddMinutes(5) }, new Mt5Position { Ticket = 3, Symbol = "XAUUSD", Side = "BUY", Lots = 0.16, Open = 4118.03, Time = t0.AddMinutes(10) } };
            var tr = new TrendState { Dir = 1, Strength = 75, SwingHi = 4122.50, SwingLo = 4118.60, Close = 4123.00, Atr1 = 1.2 };
            Func<TrendState, double, int, int, double, string, AddBreakTicket> T = (x, tot, used, max, mult, stop) => TrendDetect.AddOnBreak(legs, tot, 16, 4123.07, 4123.32, 100, 0, 4112.22, x, 70, mult, stop, used, max);
            var a = T(tr, -51.62, 0, 1, 1, "LEG");
            Console.WriteLine("  #47 add: " + a.Short + " | " + a.Text.Substring(0, Math.Min(160, a.Text.Length)));
            Check(a.Ok && a.Dir == 1 && Math.Abs(a.Lots - 0.08) < 1e-9 && Math.Abs(a.NetAfter - 0.16) < 1e-9 && Math.Abs(4123.07 + (16 + 51.62) / 8 - 4131.52) < 0.01 && Math.Abs(a.ExitPx - 4127.42125) < 1e-6 && Math.Abs(a.RiskAtHedge + 227.22) < 1e-6
                && Math.Abs(a.Stop - 4118.03) < 1e-9 && Math.Abs(a.AddAtStop + 42.32) < 1e-6 && Math.Abs(a.RiskNoAdd + 138.42) < 1e-6 && a.Short.StartsWith("BUY 0.08 (add to winner) — exit 4127.4 · risk −$227"),
                "➕ #47: BUY 0.08 (1× the entry) → net +0.16, exit 4127.42 (the $16 target incl. the add's spread), at the next hedge 4112.22 the basket −$227.22 (−$138.42 without the add); the add's stop at the last BUY leg 4118.03 = −$42.32", a.Short);
            var weak = new TrendState { Dir = 1, Strength = 65, SwingHi = 4122.5, SwingLo = 4118.6, Close = 4123.0 }; var noBreak = new TrendState { Dir = 1, Strength = 80, SwingHi = 4123.5, SwingLo = 4118.6, Close = 4123.0 }; var down = new TrendState { Dir = -1, Strength = 90, SwingHi = 4125, SwingLo = 4122, Close = 4121 };
            var two = T(tr, -51.62, 0, 1, 2, "LEG"); var sw = T(tr, -51.62, 0, 1, 1, "SWING");
            Check(!T(weak, -51.62, 0, 1, 1, "LEG").Ok && !T(noBreak, -51.62, 0, 1, 1, "LEG").Ok && !T(down, -51.62, 0, 1, 1, "LEG").Ok && !T(tr, 3, 0, 1, 1, "LEG").Ok && !T(tr, -51.62, 1, 1, 1, "LEG").Ok && T(tr, -51.62, 1, 2, 1, "LEG").Ok
                && !TrendDetect.AddOnBreak(legs, -51.62, 16, 4123.07, 4123.32, 100, 0, 4112.22, tr, 70, 1, "LEG", 1, 2, 4122.50).Ok
                && two.Ok && Math.Abs(two.Lots - 0.16) < 1e-9 && sw.Ok && Math.Abs(sw.Stop - 4122.50) < 1e-9,
                "➕ the conditions: trend ≥ 70 in the NET's direction + a finished M1 close beyond the swing, only while losing; the cap (1 a basket, up to 2, a NEW swing each); 2× = 0.16; SWING stop = 4122.50", "");
            var d0 = new AutoSettings(); var l0 = AutoSettings.Load("sym=XAUUSD"); var mine = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000); var rt = AutoSettings.Load(new AutoSettings { AddOnBreak = true, AobMult = 2, AobStop = "SWING", AobMax = 2 }.Save());
            Check(!d0.AddOnBreak && !l0.AddOnBreak && !mine.AddOnBreak && d0.AobMax == 1 && d0.AobMin == 70 && rt.AddOnBreak && rt.AobMult == 2 && rt.AobStop == "SWING" && rt.AobMax == 2 && RaceLanes.For("AOB", mine, 8000).AddOnBreak && RaceLanes.IsExperimental("AOB")
                && RaceLanes.Tip("AOB").Contains("4 of 7 years"),
                "➕ OFF by default (new / loaded / DEFENDER), saved and loaded; its 🏁 RACE lane; the evidence \"lost to plain settings in 4 of 7 years\" shown", "");
            // the engine: 9–10 Nov with his settings + ADD ON BREAK → at most one add a basket, each with its stop, nothing else changes when OFF
            RadarModel.Default = model;
            Func<Action<AutoSettings>, HedgeAuto> run = tune =>
            {
                var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = new DateTime(2026, 11, 9), To = new DateTime(2026, 11, 10), Balance = 8000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 25 }, new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }, bars);
                var s = AutoSettings.Load(string.Join(";", File.ReadAllLines(Path.Combine(Data(), "settings-1109c.txt")))); s.TrackManual = false; s.OnManual = "ADOPT"; s.Windows = ""; tune(s);
                var au = new HedgeAuto(); au.Send = (c, x, l) => sb.Command(c, x); sb.OnTickHook = () => au.Step(sb); au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 250; for (int k = 0; k < 200000 && !sb.Ended; k++) sb.Advance(0.5); return au;
            };
            var on = run(x => x.AddOnBreak = true); var off = run(x => { x.ProtectAdd = "OFF"; }); var auto0 = run(x => { });
            int adds = on.Trades.Count(x => x.Why.StartsWith("➕ ADD ON BREAK")); bool perIt = on.Trades.Where(x => x.Why.StartsWith("➕ ADD ON BREAK")).GroupBy(x => x.Iter).All(g => g.Count() <= 1);
            Console.WriteLine("  ➕ 9–10 Nov: " + adds + " adds, " + on.AobStops + " stops, " + on.Prot.Locks + " protect locks; net " + on.Iterations.Where(x => x.Done).Sum(x => x.Result).ToString("0.00", IC) + " vs " + off.Iterations.Where(x => x.Done).Sum(x => x.Result).ToString("0.00", IC));
            Check(perIt && on.Story.Any(x => x.Item2.Contains("➕ ADD ON BREAK")) && off.Trades.Count == auto0.Trades.Count && Math.Abs(off.Iterations.Where(x => x.Done).Sum(x => x.Result) - auto0.Iterations.Where(x => x.Done).Sum(x => x.Result)) < 1e-6 && !auto0.Trades.Any(x => x.Why.StartsWith("🛡 PROTECT") || x.Why.StartsWith("🛡 RESUME")),
                "➕ the engine: " + adds + " add(s) on 9–10 Nov, never more than 1 a basket, each decision explained; 🛡 PROTECT never acts on the normal hedges (AUTO = OFF, the same trades)", adds + "");
        }
        {   // 🛡 PROTECT THE ADD — his DEMO #47: after BUY 0.08 / SELL 0.16 / BUY 0.16 he added BUY 0.65 @4118.97 by hand → net LONG 0.73, total −$73.43, breakeven 4120.16
            var t0 = new DateTime(2026, 11, 10, 10, 0, 0);
            var legs = new List<Mt5Position> { new Mt5Position { Ticket = 1, Symbol = "XAUUSD", Side = "BUY", Lots = 0.08, Open = 4117.45, Time = t0 }, new Mt5Position { Ticket = 2, Symbol = "XAUUSD", Side = "SELL", Lots = 0.16, Open = 4112.37, Time = t0.AddMinutes(5) }, new Mt5Position { Ticket = 3, Symbol = "XAUUSD", Side = "BUY", Lots = 0.16, Open = 4118.03, Time = t0.AddMinutes(10) } };
            var m1 = Enumerable.Range(0, 40).Select(i => new Mt5Bar { Time = t0.AddMinutes(-20 + i), O = 4119, C = 4119, H = 4120.085, L = 4117.915 }).ToList();   // ATR(M1) = 2.17
            var pr = new AddProtector(); var now = t0.AddMinutes(20);
            var a0 = pr.Tick(legs, 4119.15, 4119.40, now, m1, -51.62, 100, 0, "ATR", 1, 2, 15);
            legs.Add(new Mt5Position { Ticket = 4, Symbol = "XAUUSD", Side = "BUY", Lots = 0.65, Open = 4118.97, Time = now });
            var a1 = pr.Tick(legs, 4119.15, 4119.40, now.AddSeconds(5), m1, -73.43, 100, 0, "ATR", 1, 2, 15); var txt = pr.Text(now);
            Console.WriteLine("  #47 protect: " + txt);
            Check(a0 == null && a1 == null && pr.Phase == "ARMED" && pr.AddTicket == 4 && pr.Source == "✋ your add" && Math.Abs(pr.Level - 4116.80) < 1e-9 && Math.Abs(pr.EstLockTotal + 263.23) < 1e-6 && Math.Abs(pr.EntryLots - 0.08) < 1e-9
                && txt.StartsWith("🛡 add protected") && txt.Contains("if 4116.80 → lock at net 0 (≈ −$263), wait 15 min, restart 0.08"),
                "🛡 #47: his BUY 0.65 (≥ 2× the entry, the net's direction) arms the protection at once: 4118.97 − 1 × ATR(M1) 2.17 = 4116.80; the lock there ≈ −$263 (−$245 of move + the lock's spread)", txt);
            var lk = pr.Tick(legs, 4116.80, 4117.05, now.AddMinutes(3), m1, -245.0, 100, 0, "ATR", 1, 2, 15);
            Check(lk != null && lk.Kind == "LOCK" && lk.Dir == -1 && Math.Abs(lk.Lots - 0.73) < 1e-9, "🛡 the bid reaches 4116.80 → EQUALIZE: SELL 0.73 = net 0", lk == null ? "null" : lk.Kind + lk.Dir + lk.Lots);
            pr.Sent(lk, now.AddMinutes(3), -263.23); legs.Add(new Mt5Position { Ticket = 5, Symbol = "XAUUSD", Side = "SELL", Lots = 0.73, Open = 4116.80, Time = now.AddMinutes(3) });
            var lockAt = now.AddMinutes(3); var m1b = m1.Concat(Enumerable.Range(0, 15).Select(i => new Mt5Bar { Time = new DateTime(lockAt.Ticks - lockAt.Ticks % TimeSpan.TicksPerMinute).AddMinutes(i), O = 4116, C = 4116.5, H = 4117.6, L = 4115.2 })).ToList();
            var c1 = pr.Tick(legs, 4116.5, 4116.75, lockAt.AddMinutes(10), m1b, -263.23, 100, 0, "ATR", 1, 2, 15); string ctext = pr.Text(lockAt.AddMinutes(10)); bool blk = pr.Blocks;
            var c2 = pr.Tick(legs, 4116.5, 4116.75, lockAt.AddMinutes(15), m1b, -263.23, 100, 0, "ATR", 1, 2, 15); string ph2 = pr.Phase;
            var inside = m1b.Concat(new[] { new Mt5Bar { Time = lockAt.AddMinutes(15), O = 4116, C = 4117.0, H = 4117.2, L = 4115.9 } }).ToList();
            var c3 = pr.Tick(legs, 4117, 4117.25, lockAt.AddMinutes(16), inside, -263.23, 100, 0, "ATR", 1, 2, 15);
            var brk = inside.Concat(new[] { new Mt5Bar { Time = lockAt.AddMinutes(16), O = 4117, C = 4118.1, H = 4118.3, L = 4116.9 } }).ToList();
            var rs = pr.Tick(legs, 4118.1, 4118.35, lockAt.AddMinutes(17), brk, -263.23, 100, 0, "ATR", 1, 2, 15);
            Check(c1 == null && blk && ctext.StartsWith("🛡 LOCKED at net 0 (total −$263) — cool-down 5:00 left") && c2 == null && ph2 == "BREAK" && Math.Abs(pr.RangeHi - 4117.6) < 1e-9 && Math.Abs(pr.RangeLo - 4115.2) < 1e-9 && c3 == null
                && rs != null && rs.Kind == "RESUME" && rs.Dir == 1 && Math.Abs(rs.Lots - 0.08) < 1e-9,
                "🛡 the cool-down (15 min, no hedges / adds: " + ctext + "), then the first finished M1 close beyond its range 4115.20–4117.60 → RESUME SMALL: BUY 0.08 (the entry size)", ctext + " | " + ph2 + " " + (rs == null ? "null" : rs.Kind));
            pr.Sent(rs, lockAt.AddMinutes(17), -263.23);
            // a normal hedge never arms it; the defaults per account kind
            var p2 = new AddProtector(); var h = new List<Mt5Position> { legs[0], legs[1] }; p2.Tick(h, 4113, 4113.25, now, m1, -40, 100, 0, "ATR", 1, 2, 15); h.Add(legs[2]); p2.Tick(h, 4118, 4118.25, now.AddMinutes(1), m1, -40, 100, 0, "ATR", 1, 2, 15);
            var p3 = new AddProtector(); var h3 = new List<Mt5Position> { legs[0], legs[1], legs[2] }; p3.Tick(h3, 4119, 4119.25, now, m1, -40, 100, 0, "ATR", 1, 2, 15); p3.FlagNext(1, now.AddSeconds(90));
            h3.Add(new Mt5Position { Ticket = 9, Symbol = "XAUUSD", Side = "BUY", Lots = 0.08, Open = 4119.2, Time = now.AddSeconds(30) }); p3.Tick(h3, 4119, 4119.25, now.AddSeconds(30), m1, -40, 100, 0, "ATR", 1, 2, 15);
            var mine = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000);
            Check(pr.Phase == "" && pr.Resumes == 1 && p2.Phase == "" && p3.Phase == "ARMED" && p3.Source == "💡 RECOVERY" && new AutoSettings().ProtectAdd == "AUTO" && AutoSettings.Load("sym=XAUUSD").ProtectAdd == "AUTO"
                && AddProtector.On("AUTO", true) && !AddProtector.On("AUTO", false) && AddProtector.On("ON", false) && !AddProtector.On("OFF", true) && AutoSettings.Load(new AutoSettings { ProtectAdd = "ON", ProtectCool = 20 }.Save()).ProtectCool == 20
                && RaceLanes.For("AOP", mine, 8000).AddOnBreak && RaceLanes.For("AOP", mine, 8000).ProtectAdd == "ON" && RaceLanes.IsExperimental("AOP"),
                "🛡 a normal hedge (it flips the net) never arms it; a 💡 RECOVERY click does; ON by default on 🎮 DEMO / 🧪 BACKTEST / 🏁 RACE, OFF on an MT5 account until ticked ON; the 🏁 lane ADD ON BREAK + PROTECT", pr.Phase + " " + p2.Phase + " " + p3.Phase);
        }
        {   // ✋ #47 STOPPED BY HAND: his BUY 0.65 stops the automation, his BUY 0.35 follows; the TOTAL reaches +$66.83 → the 🎯 guard closes ALL at once;
            // the iteration closes with ALL its deals, counts his legs (✋ 2 manual legs, 1.00 lots, MAX LOTS 1.40), never "open (+0.00)"
            var h = His47(S47()); var fb = h.Item1; var au = h.Item2; var closes = h.Item3; var it = au.Current;
            Hand47(fb, 60, 0.65, 4118.97); Price47(fb, 4118.97); au.Step(fb); bool stopped = au.HandStopped && !au.Running;
            au.GuardStep(fb, S47()); Hand47(fb, 61, 0.35, 4119.25); Price47(fb, 4119.25); au.GuardStep(fb, S47()); int before = closes.Count;
            double bidTot = (66.83 + 100 * (0.08 * 4117.45 + 0.16 * 4118.03 + 0.65 * 4118.97 + 0.35 * 4119.25) - 100 * 0.16 * (4112.37 - 0.25)) / 108.0;
            Price47(fb, Math.Round(bidTot, 2)); au.GuardStep(fb, S47()); double tot = au.GuardTotal;
            Check(stopped && before == 0 && closes.Count == 1 && tot > 16 && Math.Abs(tot - 66.83) < 0.6 && it.ManualAdds == 2 && Math.Abs(it.ManualLots - 1.0) < 1e-9 && it.Buys == 4 && it.Sells == 1 && Math.Abs(it.MaxGross - 1.40) < 1e-9
                && au.Story.Count(x => x.Item2.Contains("✋ MANUAL LEG BUY")) == 2 && au.Story.Any(x => x.Item2.Contains("MT5 (desktop)")) && !fb.Cmds.Contains("ORDER"),
                "🎯 #47: stopped by his BUY 0.65, then BUY 0.35; at a bid of " + Math.Round(bidTot, 2).ToString("0.00", IC) + " the TOTAL " + tot.ToString("+0.00;−0.00", IC) + " ≥ the target → CLOSE ALL at once; his 2 legs counted (✋ 1.00 lots, BUYS 4 / SELLS 1, MAX LOTS 1.40), a STORY line each (MT5 desktop)", stopped + " " + before + " " + closes.Count + " tot " + tot + " man " + it.ManualAdds + " " + it.ManualLots + " " + it.Buys + "/" + it.Sells + " max " + it.MaxGross);
            Flat47(fb, "EXPERT"); au.GuardStep(fb, S47());
            Check(it.Done && au.Current == null && it.Reason.StartsWith("🎯 EXIT GUARD") && Math.Abs(it.Result - Math.Round(tot, 2)) < 0.02 && !it.Unverified,
                "🎯 then flat: the iteration CLOSES (" + it.Reason + ") with its full result " + it.Result.ToString("+0.00;−0.00", IC) + " = all its deals (the automation's + his)", it.Reason + " " + it.Result + " " + it.Done);
            Func<string, bool, AutoIteration> how = (reason, guard) =>
            {
                var s2 = S47(); s2.ExitGuard = guard; var h2 = His47(s2); var f2 = h2.Item1; var a2 = h2.Item2; var i2 = a2.Current; Hand47(f2, 60, 0.65, 4118.97); Price47(f2, 4118.0); a2.Step(f2); a2.GuardStep(f2, s2);
                Flat47(f2, reason); a2.GuardStep(f2, s2); return i2;
            };
            var byHand = how("DESKTOP", false); var byTp = how("TP", true);
            Check(byHand.Done && byHand.Reason == "✋ CLOSED BY HAND" && byHand.Result < 0 && byHand.ManualAdds == 1 && byTp.Done && byTp.Reason == "broker TP",
                "✋ closed by hand (even with the 🎯 guard off) → \"✋ CLOSED BY HAND\" with its full result " + byHand.Result.ToString("+0.00;−0.00", IC) + "; a broker TP → \"broker TP\"", byHand.Reason + " " + byTp.Reason);
            var s3 = S47(); s3.ExitGuard = false; var h3 = His47(s3); var f3 = h3.Item1; var a3 = h3.Item2; var i3 = a3.Current; Hand47(f3, 60, 0.65, 4118.97); a3.Step(f3); Flat47(f3, "DESKTOP"); a3.Start(f3, s3, true);
            Check(i3.Done && i3.Reason == "✋ CLOSED BY HAND" && a3.Current == null && a3.Running, "▶ RESUME on a flat account: the old iteration #" + i3.N + " closes first (" + i3.Reason + ", " + i3.Result.ToString("+0.00;−0.00", IC) + "), then a new one can start — never \"open (+0.00)\" forever", i3.Reason + " " + i3.Done);
            Func<double, string> fmt = v => (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("0.00", IC);
            string t1 = HedgeAuto.TotalTip(true, 66.83, 66.83, 0, fmt), t0 = HedgeAuto.TotalTip(false, -37.65, -391.07, 353.42, fmt);
            Check(t1 == "TOTAL NOW +$66.83 = open +$66.83 + closed legs +$0.00 (this iteration, commissions and swap included)" && t0 == null,
                "🧮 the strip's TOTAL tooltip is rebuilt every refresh from the same numbers as the box (+$66.83 = +$66.83 + $0.00 — never the old −$391.07 + $353.42 of #43)", t1 + " | " + t0);
        }
        Console.WriteLine(fails == 0 ? "ALL 09h TESTS PASSED" : fails + " 09h TEST(S) FAILED");
        return fails;
    }
}
