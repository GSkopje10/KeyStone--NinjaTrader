// 09o: 🪜 2ND-SWING HEDGE — the hedge trigger SWING2: the swings counted after the entry / after a hedge, the break of the N-th one both
// sides, the flip back at the most recent swing, the min swing size, the cool-down, no trigger while in profit, the chop guard, the
// max distance as the safety, and everything through Ask → AutoSend (a refused order sends nothing, ✋ MANUAL MANAGE only suggests)
using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using KeystoneMT5;
public static class NineOTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 1e-6; }
    static readonly DateTime Day0 = new DateTime(2026, 10, 20), Day1 = new DateTime(2026, 10, 21, 10, 0, 0);
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    // one candle a minute around a mid: H = mid + 0.5, L = mid − 0.5 (O = C = mid)
    static List<Mt5Bar> Mids(DateTime start, IEnumerable<double> mids) { int i = 0; return mids.Select(m => new Mt5Bar { Time = start.AddMinutes(i++), O = m, H = m + 0.5, L = m - 0.5, C = m, V = 100, Spread = 10 }).ToList(); }
    static List<Mt5Bar> NewestFirst(List<Mt5Bar> b) { var r = b.ToList(); r.Reverse(); return r; }
    // the falling path of the user's case: S1 low 3996.5 (bar 7), a swing high 3999 (bar 9), S2 low 3994.5 (bar 13), a swing high 3997 (bar 15),
    // the break of S2 at bar 17 (low 3993.5), then it comes back: above 3997 at bar 22
    static readonly double[] Fall = { 4000, 4000, 4000, 4000, 4000, 3999, 3998, 3997, 3998, 3998.5, 3998, 3997.5, 3996, 3995, 3996, 3996.5, 3996, 3994, 3993, 3994, 3995, 3996, 3997.6, 3998.2, 3998.6, 3998.2, 3998.4, 3998.6, 3998.4, 3998.5 };
    static AutoSettings Base(string trig)
    {
        return new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, AddLots = 0.2, NetAdvantage = 0.1, HedgeSize = "ADVANTAGE", Target = 500, FirstDir = "BUY", HedgeMode = "BREAK", Rolling = true, Swing = true, SwingK = 2,
            NextStart = "PAUSE", PauseSec = 999999, TrackManual = false, OnManual = "ADOPT", RangeOnly = false, HedgeCap = 0, DdTightEvery = 0, ScaleOut = false, BeLock = false, Rescue = false, DangerExit = false, ExitGuard = false,
            HardFloat = 0, HardMargin = 0, RiskGuard = false, BeAfter = 0, CompressMode = "OFF", FullAuto = false, HedgeTrigger = trig, SwN = 2 };
    }
    static Tuple<HedgeAuto, SimBroker> Run(AutoSettings s, double[] path, Func<string, object[], SimBroker, string> send = null, Action<HedgeAuto, SimBroker> afterEntry = null)
    {
        var warm = Mids(Day0.AddHours(10), Enumerable.Range(0, 120).Select(i => 4000 + Math.Round(Math.Sin(i / 5.0) * 0.3, 2)));
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = Day1.Date, To = Day1.Date, Balance = 10000, Kind = "STANDARD", FixedSpread = 10, Leverage = 500, StopOutPct = 50 }, Gold(), warm.Concat(Mids(Day1, path)).ToList());
        var au = new HedgeAuto(); au.Send = (c, a, l) => send != null ? send(c, a, sb) : sb.Command(c, a);
        sb.Playing = true; sb.Speed = 60; while (!sb.Ended && sb.State.ServerNow < Day1) sb.Advance(0.25);
        sb.OnTickHook = () => au.Step(sb); if (au.Check(sb, s) == "") au.Start(sb, s, false);
        bool hooked = false;
        for (int k = 0; k < 200000 && !sb.Ended; k++) { sb.Advance(0.25); if (!hooked && afterEntry != null && sb.State.Positions.Count == 1 && au.Trades.Count == 1) { hooked = true; afterEntry(au, sb); } }
        return Tuple.Create(au, sb);
    }
    static AutoTrade FirstHedge(HedgeAuto au) { return au.Trades.FirstOrDefault(t => t.Why.StartsWith("ADD-ON")); }

    public static int Main16()
    {
        // ---------------------------------------------------------------- 1. the setting
        {
            var d = new AutoSettings(); var s = new AutoSettings { HedgeTrigger = "SWING2", SwN = 3, SwCool = 7, SwMinAtr = 0.8, SwMinTicks = 40, SwBreak = "CLOSE", SwChop = 1 }; var l = AutoSettings.Load(s.Save());
            Check(d.HedgeTrigger == "LEAD" && !d.IsSwing2 && d.SwN == 2 && d.SwChop == 2 && l.HedgeTrigger == "SWING2" && l.IsSwing2 && l.SwN == 3 && l.SwCool == 7 && Eq(l.SwMinAtr, 0.8) && Eq(l.SwMinTicks, 40) && l.SwBreak == "CLOSE" && l.SwChop == 1
                && AutoSettings.Load(d.Save()).HedgeTrigger == "LEAD" && AutoSettings.Load("htrig=WHAT;swn=9").HedgeTrigger == "LEAD" && AutoSettings.Load("htrig=SWING2;swn=9").SwN == 3 && !new AutoSettings { HedgeTrigger = "SWING2", HedgeMode = "PRICE" }.IsSwing2
                && AutoSettings.Model("DEFENDER", new AutoSettings(), 5000).HedgeTrigger == "LEAD",
                "🪜 setting: LEAD by default (DEFENDER too), SWING2 / N / cool-down / min size / CLOSE / chop guard survive save / load, N clamped 1–3, only with HEDGE MODE = BREAK");
            var diff = AutoSettings.Diff(d, s); Check(diff.Any(x => x.StartsWith("🪜 HEDGE TRIGGER: LEAD → SWING2")), "🪜 APPLY NOW names the change", string.Join(" | ", diff));
            var mine = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000); var lane = RaceLanes.For("SW2", mine, 8000);
            Check(lane.IsSwing2 && lane.SwN == 2 && !mine.IsSwing2 && RaceLanes.Keys.Contains("SW2") && RaceLanes.Compare.Contains("SW2") && RaceLanes.CompareFor(AutoSettings.Model("RUNNER", mine, 8000)).Contains("SW2") && !RaceLanes.IsExperimental("SW2") && RaceLanes.Tip("SW2").Contains("2ND-SWING"),
                "🏁 lane SW2 = MY SETTINGS with SWING2, in ▶ COMPARE WITH MY SETTINGS (also when MINE is RUNNER), a trigger variant (not an experiment)");
        }
        // ---------------------------------------------------------------- 2. counting the swings (pure)
        var fall = Mids(Day1, Fall); var nf = NewestFirst(fall.Take(17).ToList());   // bars 0–16 finished
        {
            var lows = SwingHedge.Swings(nf, 2, false, 0, Day1); var nf18 = NewestFirst(fall.Take(18).ToList()); var highs = SwingHedge.Swings(nf18, 2, true, 0, Day1);   // ③ 3997 needs bars 16 + 17 after it
            Check(lows.Count == 2 && Eq(lows[0].Price, 3996.5) && Eq(lows[1].Price, 3994.5) && lows[0].N == 1 && lows[1].N == 2 && lows[0].Used && !lows[1].Used && lows[0].Time == Day1.AddMinutes(7),
                "🪜 swing lows since the entry, oldest first: ① 3996.5 (used — the price went through it) ② 3994.5", string.Join(", ", lows.Select(x => x.N + ":" + x.Price + (x.Used ? "u" : ""))));
            Check(highs.Count == 2 && Eq(highs[0].Price, 3999) && Eq(highs[1].Price, 3997), "🪜 the swing highs between them: 3999 then 3997", string.Join(", ", highs.Select(x => x.Price)));
            var after = SwingHedge.Swings(nf, 2, false, 0, Day1.AddMinutes(8));
            Check(after.Count == 1 && Eq(after[0].Price, 3994.5) && after[0].N == 1, "🪜 only swings formed AFTER the leg count (a leg at 10:08 → 3994.5 is its 1st)");
            var pk = SwingHedge.Pick(nf, 2, 1, Day1, 2, false, 0); var pk1 = SwingHedge.Pick(NewestFirst(fall.Take(11).ToList()), 2, 1, Day1, 2, false, 0);
            Check(Eq(pk.Level, 3994.5) && pk.Tag == "the 2nd swing low" && pk.Counted.Count == 2 && double.IsNaN(pk1.Level) && pk1.Have == 1 && pk1.Why.Contains("1 of 2"),
                "🪜 net long: the hedge line = the 2nd swing low 3994.5; with only ① formed there is no line yet (1 of 2)", pk.Tag + " " + pk.Level + " | " + pk1.Why);
            var fb = SwingHedge.Pick(nf18, 2, -1, Day1.AddMinutes(17), 2, true, 0);
            Check(fb.FlipBack && Eq(fb.Level, 3997) && fb.Tag == "the swing high", "🪜 after the hedge (a SELL leg): the flip back at the most recent swing HIGH 3997 (not N swings again)", fb.Tag + " " + fb.Level);
            var sh = SwingHedge.Pick(NewestFirst(Mids(Day1, Fall.Select(x => 8000 - x)).Take(17).ToList()), 2, -1, Day1, 2, false, 0);
            Check(Eq(sh.Level, 8000 - 3994.5) && sh.Tag == "the 2nd swing high", "🪜 mirror — net short: the 2nd swing HIGH", sh.Tag + " " + sh.Level);
            // min swing size: a 0.3-deep wiggle does not count
            var wig = Mids(Day1, new double[] { 4000, 4000, 4000, 3999.9, 3999.6, 3999.9, 4000, 4000, 3999, 3998, 3997, 3998, 3999, 3999.5, 3999.5 }); var wn = NewestFirst(wig);
            foreach (var b in wig) { b.H = b.C + 0.15; b.L = b.C - 0.15; }
            var all = SwingHedge.Swings(wn, 2, false, 0, Day1); double ms = SwingHedge.MinSize(wn, 0.3, 0, 0.01); var big = SwingHedge.Swings(wn, 2, false, 2.0, Day1);
            Check(all.Count == 2 && big.Count == 1 && Eq(big[0].Price, 3996.85) && Eq(SwingHedge.MinSize(wn, 0, 50, 0.01), 0.5) && ms > 0.08,
                "🪜 MIN SWING: the 0.7 wiggle is skipped at a 2.0 minimum, the real 3-point swing counts; min ticks 50 × 0.01 = 0.5", all.Count + " / " + big.Count + " min " + ms);
        }
        // ---------------------------------------------------------------- 3. the break and the gate (pure)
        {
            Check(SwingHedge.Broken(1, 3994.5, 3994.5, double.NaN, "TOUCH") && !SwingHedge.Broken(1, 3994.5, 3994.6, double.NaN, "TOUCH") && SwingHedge.Broken(-1, 4005, 4005.1, double.NaN, "TOUCH")
                && !SwingHedge.Broken(1, 3994.5, 3990, 3994.6, "CLOSE") && SwingHedge.Broken(1, 3994.5, 3999, 3994.4, "CLOSE") && SwingHedge.Broken(-1, 4005, 4000, 4005.2, "CLOSE"),
                "🪜 the break: TOUCH = the bid at / past the swing; CLOSE = the last finished candle closed past it (both sides)");
            Check(SwingHedge.Gate(12, false, "TOUCH", true, -1, 3).Contains("not in drawdown") && SwingHedge.Gate(0, true, "TOUCH", true, -1, 3) != "" && SwingHedge.Gate(-40, false, "TOUCH", true, 1.5, 3).Contains("cool-down")
                && SwingHedge.Gate(-40, true, "TOUCH", true, 1.5, 3) == "" && SwingHedge.Gate(-40, false, "TOUCH", true, 3.2, 3) == "" && SwingHedge.Gate(-40, false, "CLOSE", false, -1, 3).Contains("CLOSED") && SwingHedge.Gate(-40, false, "TOUCH", false, -1, 0) == "",
                "🪜 gate: no hedge while in profit (even at the max distance), the cool-down after a hedge (never for the max distance), CLOSE waits for a closed candle");
        }
        // ---------------------------------------------------------------- 4. the engine on the user's shape (SimBroker, every order through AutoSend)
        {
            var sw = Run(Base("SWING2"), Fall); var au = sw.Item1; var h = FirstHedge(au); var flip = au.Trades.Where(t => t.Why.StartsWith("ADD-ON")).Skip(1).FirstOrDefault();
            Check(h != null && h.Side == "SELL" && Eq(h.Level, 3994.5) && h.Time >= Day1.AddMinutes(17) && h.Time < Day1.AddMinutes(18) && h.Why.Contains("🪜 the 2nd swing low"),
                "🪜 SWING2: the 1st swing low 3996.5 breaks (10:12) — NO hedge; the 2nd swing low 3994.5 breaks (10:17) → HEDGE SELL there", h == null ? "none: " + string.Join(" | ", au.Log.Skip(Math.Max(0, au.Log.Count - 8))) : h.Side + " " + h.Level + " " + h.Time.ToString("HH:mm:ss") + " " + h.Why);
            Check(flip != null && flip.Side == "BUY" && Eq(flip.Level, 3997) && flip.Time >= Day1.AddMinutes(22) && flip.Why.Contains("🪜 the swing high"),
                "🪜 it did not continue down and came back → the flip back BUY at the most recent swing HIGH 3997 (10:22)", flip == null ? "none" : flip.Side + " " + flip.Level + " " + flip.Time.ToString("HH:mm:ss") + " " + flip.Why);
            var ld = Run(Base("LEAD"), Fall).Item1; var lh = FirstHedge(ld);
            Check(lh != null && lh.Side == "SELL" && lh.Level >= 3996.5 - 1e-6 && lh.Time < Day1.AddMinutes(13), "🪜 the same path with LEAD (the default): the hedge at the most recent swing low, by 10:12 at the latest — earlier and higher than SWING2, unchanged", lh == null ? "none" : lh.Level + " " + lh.Time.ToString("HH:mm"));
            // mirror: a SELL entry on the mirrored path → the BUY hedge at the 2nd swing high
            var ms = Base("SWING2"); ms.FirstDir = "SELL"; var mr = Run(ms, Fall.Select(x => 8000 - x).ToArray()).Item1; var mh = FirstHedge(mr);
            Check(mh != null && mh.Side == "BUY" && Eq(mh.Level, 8000 - 3994.5) && mh.Time >= Day1.AddMinutes(17), "🪜 mirror: net SHORT → the hedge BUY at the 2nd swing HIGH", mh == null ? "none" : mh.Side + " " + mh.Level + " " + mh.Time.ToString("HH:mm"));
            // the cool-down: 10 minutes → the flip back (5 min after the hedge) waits
            var cs = Base("SWING2"); cs.SwCool = 10; var co = Run(cs, Fall).Item1; var cf = co.Trades.Where(t => t.Why.StartsWith("ADD-ON")).Skip(1).FirstOrDefault();
            Check(cf == null || cf.Time >= FirstHedge(co).Time.AddMinutes(10), "🪜 COOL-DOWN 10 min: no flip back within 10 minutes of the hedge", cf == null ? "" : cf.Time.ToString("HH:mm"));
            // the chop guard: after 1 hedge, the flip back only beyond the basket's range (above the BUY ≈ 4000.1) — not at 3997
            var gs = Base("SWING2"); gs.SwChop = 1; var gu = Run(gs, Fall).Item1; var gf = gu.Trades.Where(t => t.Why.StartsWith("ADD-ON")).Skip(1).FirstOrDefault();
            Check(gf == null && gu.HedgeLevel > 4000 && gu.HedgeTag == "beyond the basket's range", "🧱 chop guard (RANGE-ONLY AFTER 1 HEDGE): the flip back waits beyond the basket's range, not inside the chop", gf == null ? gu.HedgeLevel + " " + gu.HedgeTag : gf.Level + "");
            // the counted swings for the chart (① ②) and the line's words
            var mk = Run(Base("SWING2"), Fall.Take(17).ToArray()).Item1;
            Check(mk.SwingMarks.Count == 2 && mk.SwingMarks[1].N == 2 && Eq(mk.HedgeLevel, 3994.5) && mk.HedgeTag == "the 2nd swing low" && SwingHedge.Circled(2) == "②",
                "🪜 the chart: the swings counted ① ② and the line \"🪜 HEDGE SELL … at the 2nd swing low 3994.50\"", mk.SwingMarks.Count + " " + mk.HedgeTag + " " + mk.HedgeLevel);
            // the max distance is the safety when no 2nd swing formed (a straight fall)
            var cap = Base("SWING2"); cap.HedgeCap = 3; var straight = new double[] { 4000, 4000, 4000, 3999.5, 3999, 3998.5, 3998, 3997.5, 3997, 3996.5, 3996, 3995.5, 3995, 3994.5, 3994, 3993.5, 3993 };
            var ca = Run(cap, straight).Item1; var ch = FirstHedge(ca);
            Check(ch != null && ch.Why.Contains("the max distance") && ch.Level > 3996.5 && ch.Level < 3997.5, "🛡 a straight fall (no swing): the MAX HEDGE DISTANCE 3 from the BUY hedges (the safety)", ch == null ? "none" : ch.Level + " " + ch.Why);
        }
        // ---------------------------------------------------------------- 5. no trigger while in profit
        {
            var up = new double[] { 4000, 4000, 4003, 4005, 4007, 4006, 4005, 4006, 4007, 4006, 4004.5, 4005.5, 4006, 4005, 4003.5, 4004.5, 4005, 4003, 4002.5, 4003, 4003.5, 4003 };
            var r = Run(Base("SWING2"), up).Item1; var nf2 = NewestFirst(Mids(Day1, up).Take(14).ToList()); var lows = SwingHedge.Swings(nf2, 2, false, 0, Day1);
            Check(lows.Count >= 2 && FirstHedge(r) == null && r.Log.Any(x => x.Contains("not in drawdown")),
                "🪜 two swing lows formed and the 2nd broke while the basket is IN PROFIT (long from 4000, the price 4003) → no hedge", lows.Count + " lows; hedge " + (FirstHedge(r) != null));
        }
        // ---------------------------------------------------------------- 6. through AutoSend only: refused = nothing sent; ✋ MANUAL MANAGE = a 💡 suggestion
        {
            int orders = 0; var r = Run(Base("SWING2"), Fall, (c, a, sb) => { if (c == "ORDER" && ++orders > 1) return null; return sb.Command(c, a); });
            Check(r.Item2.State.Positions.Count == 1 && r.Item2.State.Positions[0].Side == "BUY" && !r.Item1.Running && r.Item1.StopReason.Contains("not sent") && orders == 2,
                "🪜 the hedge refused by AutoSend (not armed / PROP limit) → nothing reaches the account, the automation stops and says why", r.Item1.StopReason + " pos " + r.Item2.State.Positions.Count);
            var m = Run(Base("SWING2"), Fall, null, (au, sb) => au.SetManualManage(true));
            Check(m.Item2.State.Positions.Count == 1 && m.Item1.Suggestion.Contains("SELL") && m.Item1.Suggestion.Contains("🪜"),
                "🪜 ✋ MANUAL MANAGE: the 2nd-swing hedge is only a 💡 suggestion (nothing sent)", m.Item1.Suggestion);
        }
        Console.WriteLine(fails == 0 ? "09o: all green" : "09o: " + fails + " FAILED");
        return fails;
    }
}
