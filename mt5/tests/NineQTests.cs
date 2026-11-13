// 09q (BUILD 2026-11-13q): 🐞 the user's DEMO iteration #30 — 🔐 BREAKEVEN PROFIT (LOCK / RISK GUARD / RADAR LOCK off) locked the basket at net 0 and NOTHING
// ever unlocked it, while the chart promised "🔓 UNLOCK ▲ above 3904.08" / "▼ below 3901.68 (5.42 away)" (the bid was already 5.42 BELOW). Now a lock that
// exists is always managed: the price alone unlocks the breakeven lock (the range + the spread → the user's UNLOCK LEAD / LIFT, through Order / Ask → AutoSend;
// ✋ MANUAL MANAGE → a 💡 suggestion, still locked); RISK GUARD on still needs its trend; a passed level never reads "x away".
// 🎨 the colour rule (user's): red only for negative values / boxes, positive = green, 0 / not a P/L = neutral (`Tone`).
using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using KeystoneMT5;
public static class NineQTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static List<Mt5Bar> Mids(DateTime start, IEnumerable<double> mids) { int i = 0; return mids.Select(m => new Mt5Bar { Time = start.AddMinutes(i++), O = m, H = m + 0.05, L = m - 0.05, C = m, V = 100, Spread = 10 }).ToList(); }
    static IEnumerable<double> Line(double from, double step, int n) { for (int i = 0; i < n; i++) yield return Math.Round(from + step * i, 2); }
    // the user's #30 settings: 🔐 BREAKEVEN PROFIT ON (80 % → 45 %), LOCK IN CHOP / RISK GUARD / RADAR LOCK OFF, UNLOCK = LEAD, PROFIT UNLOCK off
    static AutoSettings His()
    {
        return new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, AddLots = 0.1, NetAdvantage = 0.1, HedgeSize = "ADVANTAGE", Target = 20, FirstDir = "BUY", HedgeMode = "PRICE", HedgePrice = 50, Rolling = false,
            NextStart = "PAUSE", PauseSec = 999999, MaxIterations = 1, TrackManual = false, OnManual = "ADOPT", RangeOnly = false, HedgeCap = 0, DdTightEvery = 0, ScaleOut = false, Rescue = false, DangerExit = false, ExitGuard = false,
            HardFloat = 0, HardMargin = 0, BeAfter = 0, CompressMode = "OFF", FullAuto = false, NyOffsetHours = -7,
            BeLock = true, BeClose = false, BeLockAt = 80, BeLockKeep = 45, LockOn = false, RiskGuard = false, RadarLock = false, Unlock = "LEAD", ProfitUnlock = false };
    }
    sealed class Run1 { public HedgeAuto Au; public SimBroker Sb; public List<string> Story = new List<string>(); public List<string> Sent = new List<string>(); public bool SawLock0; public double LockLo = double.NaN, LockHi = double.NaN; public List<string> Labels = new List<string>(); }
    static Run1 Run(AutoSettings s, IEnumerable<double> warmMids, double[] path, Action<Run1> each = null)
    {
        var r = new Run1(); var start = new DateTime(2026, 11, 11, 10, 0, 0); var warm = warmMids.ToList(); var wb = Mids(start.AddMinutes(-warm.Count), warm);
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = wb[0].Time.Date, Balance = 10000, Kind = "STANDARD", FixedSpread = 10, Leverage = 500, StopOutPct = 50 }, Gold(), wb.Concat(Mids(start, path)).ToList());
        if (err != "") throw new Exception(err);
        var au = new HedgeAuto(); r.Au = au; r.Sb = sb; au.OnTell = (n, l) => r.Story.Add(l);
        au.Send = (c, a, l) => { r.Sent.Add(c + " " + l); return sb.Command(c, a); };
        sb.Playing = true; sb.Speed = 60; while (!sb.Ended && sb.State.ServerNow < start) sb.Advance(0.25);
        sb.OnTickHook = () =>
        {
            au.Step(sb);
            double net; lock (sb.State.Gate) net = sb.State.Positions.Sum(p => p.Dir * p.Lots);
            if (au.Locked && Math.Abs(net) < 0.005 && !double.IsNaN(au.LockHi)) { r.SawLock0 = true; r.LockLo = au.LockLo; r.LockHi = au.LockHi; }
            if (each != null) each(r);
        };
        if (au.Check(sb, s) == "") au.Start(sb, s, false); else throw new Exception(au.Check(sb, s));
        for (int k = 0; k < 400000 && !sb.Ended; k++) sb.Advance(0.25);
        return r;
    }
    static string Tail(Run1 r) { return string.Join(" / ", r.Story.Skip(Math.Max(0, r.Story.Count - 5))) + " || sent: " + string.Join(" / ", r.Sent); }
    // entry BUY 0.10 at ~4000, up to ≥ 80 % of the $20 target (+$16 = +1.6), back to 45 % (+$9) → the 🔐 lock SELL 0.10 = net 0, then `after`
    static double[] Path(IEnumerable<double> after) { return Line(4000, 0, 4).Concat(Line(4000, 0.15, 13)).Concat(Line(4001.65, -0.3, 3)).Concat(after).ToArray(); }

    public static int Main18()
    {
        var flat = Line(4000, 0, 120).Select((m, i) => Math.Round(m + Math.Sin(i / 5.0) * 0.3, 2));
        // ---------------------------------------------------------------- 1. #30: the price breaks BELOW the lock range → UNLOCK LEAD = SELL 0.10
        {
            var r = Run(His(), flat, Path(Line(4000.8, -0.3, 6).Concat(Line(3999.3, 0, 10))));
            var un = r.Au.Trades.FirstOrDefault(t => t.Why.StartsWith("🔓 UNLOCK"));
            Check(r.SawLock0 && un != null && un.Side == "SELL" && Math.Abs(un.Lots - 0.1) < 1e-9 && un.Price < r.LockLo && r.Sent.Any(x => x.StartsWith("ORDER SELL 0.1 XAUUSD") && x.Contains("🔓 UNLOCK SELL"))
                && r.Story.Any(x => x.Contains("🔓 UNLOCK — LEAD. WHY:") && x.Contains("breakeven lock: the price alone unlocks it")) && !r.Au.Locked,
                "🐞 #30 (🔐 BREAKEVEN PROFIT only, net 0 locked): the bid breaks BELOW the lock range → 🔓 UNLOCK LEAD = SELL 0.10 through Order → AutoSend, the WHY in the STORY, not locked any more",
                "sawLock " + r.SawLock0 + " lo " + r.LockLo + " unlock " + (un == null ? "none" : un.Side + " " + un.Lots + " @ " + un.Price) + " locked " + r.Au.Locked + " | " + Tail(r));
            Check(r.Story.Any(x => x.Contains("🔐 LOCK. WHY: 🔐 BREAKEVEN LOCK") && x.Contains("the price alone unlocks it")) && !r.Story.Any(x => x.Contains("nothing waits for a break")),
                "🐞 the lock's own STORY line says what unlocks it (no more \"nothing waits for a break\")", Tail(r));
        }
        // ---------------------------------------------------------------- 2. … and ABOVE → UNLOCK LEAD = BUY 0.10
        {
            var r = Run(His(), flat, Path(Line(4001.0, 0.3, 6).Concat(Line(4002.5, 0, 10))));
            var un = r.Au.Trades.FirstOrDefault(t => t.Why.StartsWith("🔓 UNLOCK"));
            Check(r.SawLock0 && un != null && un.Side == "BUY" && Math.Abs(un.Lots - 0.1) < 1e-9 && un.Price > r.LockHi && !r.Au.Locked,
                "🐞 #30: the bid breaks ABOVE the lock range → 🔓 UNLOCK LEAD = BUY 0.10", "sawLock " + r.SawLock0 + " hi " + r.LockHi + " unlock " + (un == null ? "none" : un.Side + " " + un.Lots + " @ " + un.Price) + " | " + Tail(r));
        }
        // ---------------------------------------------------------------- 3. UNLOCK = LIFT: the side against the break closes
        {
            var s = His(); s.Unlock = "LIFT"; var r = Run(s, flat, Path(Line(4000.8, -0.3, 6).Concat(Line(3999.3, 0, 10))));
            Check(r.SawLock0 && r.Sent.Any(x => x.StartsWith("CLOSEALL") && x.Contains("CLOSE the BUYS")) && r.Story.Any(x => x.Contains("🔓 UNLOCK — LIFT")),
                "🔐 lock + UNLOCK LIFT: the break below closes the BUYS (the sells run)", Tail(r));
        }
        // ---------------------------------------------------------------- 4. ✋ MANUAL MANAGE: a 💡 suggestion, nothing sent, still locked, the label says so
        {
            string lbl = ""; var r = Run(His(), flat, Path(Line(4000.8, -0.3, 6).Concat(Line(3999.3, 0, 10))), x =>
            {
                if (x.SawLock0 && !x.Au.ManualManage) x.Au.SetManualManage(true);
                if (x.Au.ManualManage && x.Au.Locked) { double bid; lock (x.Sb.State.Gate) bid = x.Sb.State.Quotes["XAUUSD"].Bid; var l = HedgeAuto.UnlockLabel(-1, x.Au.LockLo - 0.1, bid, "", x.Au.UnlockWhy); if (l.Contains("passed by")) lbl = l; }
            });
            Check(r.SawLock0 && !r.Sent.Any(x => x.Contains("UNLOCK")) && r.Au.Locked && r.Au.Suggestion.Contains("🔓 UNLOCK SELL") && r.Au.UnlockWhy.StartsWith("💡")
                && r.Story.Count(x => x.Contains("🔓 UNLOCK — LEAD")) == 1 && lbl.Contains("passed by") && lbl.Contains("💡 suggested") && !lbl.Contains("away"),
                "✋ MANUAL MANAGE: the break → a 💡 SUGGESTION (nothing sent), the basket stays LOCKED, the STORY tells it once, the label reads \"passed by … 💡 suggested\"",
                "locked " + r.Au.Locked + " sugg " + r.Au.Suggestion + " why " + r.Au.UnlockWhy + " lbl " + lbl + " | " + Tail(r));
        }
        // ---------------------------------------------------------------- 5. RISK GUARD on: still needs the trend the break's way
        {
            var down = Line(4030, -0.1, 300);   // a 5-hour fall: EMA 50 < EMA 200, the close under the EMA 200 → the trend is DOWN
            var s = His(); s.RiskGuard = true; s.GuardBreak = 1; s.GuardLots = 5; s.GuardChop = 0; s.GuardMargin = 0;
            var r = Run(s, down, Path(Line(4001.0, 0.2, 25)));   // breaks ABOVE (≥ one candle past) while the trend is DOWN
            Check(r.SawLock0 && !r.Au.Trades.Any(t => t.Why.StartsWith("🔓 UNLOCK")) && r.Au.Locked && r.Au.TrendNow < 0 && r.Au.UnlockWhy.Contains("waits for the trend UP"),
                "🛡 RISK GUARD on: the break above against a DOWN trend does NOT unlock (its rule); UnlockWhy = \"waits for the trend UP …\"", "trend " + r.Au.TrendNow + " locked " + r.Au.Locked + " why " + r.Au.UnlockWhy + " | " + Tail(r));
            var r2 = Run(s, down, Path(Line(4000.8, -0.3, 6).Concat(Line(3999.3, 0, 10))));   // the break BELOW with the DOWN trend → unlocks
            var un = r2.Au.Trades.FirstOrDefault(t => t.Why.StartsWith("🔓 UNLOCK"));
            Check(r2.SawLock0 && un != null && un.Side == "SELL", "🛡 RISK GUARD on: the break below WITH the DOWN trend unlocks (SELL)", Tail(r2));
        }
        // ---------------------------------------------------------------- 6. the labels (pure): a passed level never reads "x away"
        {
            string a = HedgeAuto.UnlockLabel(-1, 3901.68, 3896.26, "", ""), b = HedgeAuto.UnlockLabel(1, 3904.08, 3896.26, "", ""), c = HedgeAuto.UnlockLabel(1, 3904.08, 3905.0, " + the trend UP (EMA 50/200 now DOWN)", ""),
                d = HedgeAuto.UnlockLabel(-1, 3901.68, 3896.26, "", "💡 suggested (✋ MANUAL MANAGE) — nothing sent until you do it");
            Check(a == "🔓 UNLOCK ▼ below 3901.68 — ▼ passed by 5.42 — unlocking…" && b == "🔓 UNLOCK ▲ above 3904.08 (7.82 away)" && c.Contains("passed by 0.92 — waits for the trend UP (EMA 50/200 now DOWN)") && d.EndsWith("passed by 5.42 — 💡 suggested (✋ MANUAL MANAGE) — nothing sent until you do it"),
                "🏷 the #30 labels: ▼ passed by 5.42 — unlocking… (never \"5.42 away\"), ▲ 7.82 away, a trend wait / a 💡 named", a + " | " + b + " | " + c + " | " + d);
            var bn = HedgeAuto.LockBannerText(3901.68, 3904.08, 3901.58, 3904.18, 3896.26, "", "", "");
            Check(bn == "  •  🔒 LOCKED 3901.68 – 3904.08 • unlock ▲ > 3904.18 / ▼ < 3901.58 — ▼ passed by 5.32 — unlocking…", "🏷 the banner names the unlock prices and the passed side", bn);
        }
        // ---------------------------------------------------------------- 8. 🔐 BREAKEVEN PROFIT = CLOSE ALL (09q, user's request): the turn back to the keep level banks it
        {
            var s = His(); s.BeClose = true; var r = Run(s, flat, Path(Line(4000.8, -0.3, 6).Concat(Line(3999.3, 0, 10))));
            var it = r.Au.Iterations.FirstOrDefault();
            Check(it != null && it.Done && it.Reason == "BE PROFIT" && it.Result > 5 && !r.SawLock0 && !r.Au.Trades.Any(t => t.Why.Contains("LOCK")) && r.Sent.Any(x => x.StartsWith("CLOSEALL") && x.Contains("BE PROFIT")),
                "🔐 CLOSE ALL (the new default): armed at 80 %, back at 45 % → CLOSE ALL through AutoSend, the iteration ends in profit, no lock leg", it == null ? "none" : it.Reason + " " + it.Result.ToString("0.00", IC) + " | " + Tail(r));
            var d = new AutoSettings(); var l0 = AutoSettings.Load("sym=XAUUSD"); var def = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000);
            var lk = His(); var lkL = AutoSettings.Load(lk.Save());
            Check(d.BeClose && l0.BeClose && def.BeClose && def.BeLock && !lkL.BeClose, "🔐 CLOSE ALL is the default (new settings, a line without the key, DEFENDER); a saved LOCK loads as LOCK (until the one-time switch)");
            var m = AutoSettings.Mig13q(lkL); var lkOff = lkL.Copy(); lkOff.BeLock = false;
            Check(m != null && m.BeClose && m.BeLock && Math.Abs(m.BeLockAt - 80) < 1e-9 && Math.Abs(m.BeLockKeep - 45) < 1e-9 && AutoSettings.Diff(lkL, m).Count == 1 && AutoSettings.Mig13q(m) == null && AutoSettings.Mig13q(lkOff) == null
                && AutoSettings.Mig13qText == "🔐 BREAKEVEN PROFIT now CLOSES ALL at the keep level (your request) — LOCK is still an option in ⚙ SETTINGS",
                "🔐 the one-time switch of HIS saved setting: LOCK → CLOSE ALL (only that, 80 % / 45 % kept); nothing when already CLOSE or BREAKEVEN PROFIT is off", string.Join(" | ", AutoSettings.Diff(lkL, m ?? lkL)));
        }
        // ---------------------------------------------------------------- 9. #30 itself: ALREADY locked at net 0 above the keep level → closes on the next tick under the new rule (also after a restart)
        {
            foreach (bool restart in new[] { false, true })
            {
                bool switched = false; double atSwitch = double.NaN;
                var r = Run(His(), flat, Path(Line(4000.6, 0, 20)), x =>
                {   // the price stays INSIDE the lock range: the old rule kept it frozen forever
                    if (x.SawLock0 && !switched) { switched = true; var ns = x.Au.Set.Copy(); ns.BeClose = true; x.Au.ApplyNow(ns); if (restart) x.Au.Locked = false; atSwitch = x.Au.BasketTotal; }
                });
                var it = r.Au.Iterations.FirstOrDefault();
                Check(switched && it != null && it.Done && it.Reason == "BE PROFIT" && it.Result > 3 && r.Story.Any(x => x.Contains("🔐 BREAKEVEN PROFIT — CLOSE ALL. WHY: the basket is frozen at net 0")) && !r.Au.Trades.Any(t => t.Why.StartsWith("🔓 UNLOCK")),
                    "🔐 #30: a basket ALREADY frozen at net 0 above the keep level" + (restart ? " (after a restart: not 'locked' in memory, it once reached the arm level)" : "") + " → CLOSE ALL on the next tick with the WHY", (it == null ? "none" : it.Reason + " " + it.Result.ToString("0.00", IC)) + " at switch " + atSwitch + " | " + Tail(r));
            }
            var r2 = Run(His(), flat, Path(Line(4000.6, 0, 20)));
            Check(r2.Au.Locked && r2.Au.Current != null && !r2.Au.Current.Done, "🔐 LOCK kept as a choice: the same basket stays locked inside its range (it unlocks only on a break)", Tail(r2));
        }
        // ---------------------------------------------------------------- 10. ✋ MANUAL MANAGE + CLOSE ALL: a 💡 suggestion, nothing sent
        {
            var s = His(); s.BeClose = true; bool mm = false;
            var r = Run(s, flat, Path(Line(4000.8, -0.3, 6).Concat(Line(3999.3, 0, 10))), x => { if (!mm && x.Au.Current != null && x.Au.Current.BeLockArmed) { mm = true; x.Au.SetManualManage(true); } });
            Check(mm && !r.Sent.Any(x => x.StartsWith("CLOSEALL")) && r.Au.Suggestion.StartsWith("CLOSE ALL XAUUSD") && r.Au.Suggestion.Contains("BE PROFIT"), "✋ MANUAL MANAGE: the breakeven CLOSE ALL is a 💡 suggestion, nothing sent", "sugg " + r.Au.Suggestion + " | " + Tail(r));
        }
        // ---------------------------------------------------------------- 7. 🎨 the colour rule
        {
            Check(Tones.Of(7268.4) == 1 && Tones.Of(-0.01) == -1 && Tones.Of(0) == 0 && Tones.Of(0.004) == 0 && Tones.Of(double.NaN) == 0,
                "🎨 Tone: positive = green (+1), negative = red (−1), 0 / tiny / unknown = neutral");
            Check(Tones.Equity(7268.4, 8000) == 1 && Tones.Equity(-5, 100) == -1 && Tones.Equity(0, 100) == 0,
                "🎨 EQUITY: +$7,268 under a balance of $8,000 is GREEN (red only when the equity itself is negative)");
            Check(Tones.BelowBalance(7268.40, 8000) == "open −$731.60" && Tones.BelowBalance(8000, 8000) == "" && Tones.BelowBalance(8100, 8000) == "", "🎨 below the balance = a small red \"open −$731.60\" line, not a red box", Tones.BelowBalance(7268.40, 8000));
        }
        return fails;
    }
}
