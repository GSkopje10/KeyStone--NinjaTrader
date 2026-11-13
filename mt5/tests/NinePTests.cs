// 09p (BUILD 2026-11-13p): ⏰ NO NEW TRADES BEFORE THE CLOSE (no new iteration in the last N min before 17:00 New York and until the reopen —
// hedges / exits of an open basket go on), the New York clock (DST, Friday → Sunday), 🌙 AT THE CLOSE CLOSE ONLY IF ≥ BREAKEVEN (never a basket in
// drawdown; through Ask → AutoSend; ✋ MANUAL MANAGE → a 💡 suggestion), ⭐ 13p RECOMMENDED (DEFENDER's new defaults, existing settings unchanged until
// APPLY), 💵 the DEMO's costs per account + the one-time migration, the broker-like labels, 💤 the swap known / unknown
using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using KeystoneMT5;
public static class NinePTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static bool Eq(double a, double b) { return Math.Abs(a - b) < 1e-6; }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7 }; }
    static List<Mt5Bar> Mids(DateTime start, IEnumerable<double> mids) { int i = 0; return mids.Select(m => new Mt5Bar { Time = start.AddMinutes(i++), O = m, H = m + 0.5, L = m - 0.5, C = m, V = 100, Spread = 10 }).ToList(); }
    static AutoSettings Base()
    {
        return new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, AddLots = 0.2, NetAdvantage = 0.1, HedgeSize = "ADVANTAGE", Target = 500, FirstDir = "BUY", HedgeMode = "BREAK", Rolling = true, Swing = true, SwingK = 2,
            NextStart = "PAUSE", PauseSec = 60, TrackManual = false, OnManual = "ADOPT", RangeOnly = false, HedgeCap = 0, DdTightEvery = 0, ScaleOut = false, BeLock = false, Rescue = false, DangerExit = false, ExitGuard = false,
            HardFloat = 0, HardMargin = 0, RiskGuard = false, BeAfter = 0, CompressMode = "OFF", FullAuto = false, NyOffsetHours = -7 };
    }
    sealed class Run1 { public HedgeAuto Au; public SimBroker Sb; public List<string> Story = new List<string>(); public List<Tuple<DateTime, string, string>> Sent = new List<Tuple<DateTime, string, string>>(); }
    // the engine on the BACKTEST SimBroker from `start` (server time): Step on every tick, like the 🛡 HEDGE tab; every command through `send`
    static Run1 Run(AutoSettings s, DateTime start, double[] path, Func<string, object[], SimBroker, string> send = null, Action<Run1> atStart = null, Action<Run1, int> each = null)
    {
        var r = new Run1(); var warm = Mids(start.AddHours(-2), Enumerable.Range(0, 120).Select(i => 4000 + Math.Round(Math.Sin(i / 5.0) * 0.3, 2)));
        var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = "XAUUSD", From = start.Date, Balance = 10000, Kind = "STANDARD", FixedSpread = 10, Leverage = 500, StopOutPct = 50 }, Gold(), warm.Concat(Mids(start, path)).ToList());
        if (err != "") throw new Exception(err);
        var au = new HedgeAuto(); r.Au = au; r.Sb = sb; au.OnTell = (n, l) => r.Story.Add(l);
        au.Send = (c, a, l) => { r.Sent.Add(Tuple.Create(sb.State.ServerNow, c, l)); return send != null ? send(c, a, sb) : sb.Command(c, a); };
        sb.Playing = true; sb.Speed = 60; while (!sb.Ended && sb.State.ServerNow < start) sb.Advance(0.25);
        sb.OnTickHook = () => au.Step(sb); if (atStart != null) atStart(r); if (au.Check(sb, s) == "") au.Start(sb, s, false); else throw new Exception(au.Check(sb, s));
        for (int k = 0; k < 400000 && !sb.Ended; k++) { sb.Advance(0.25); if (each != null) each(r, k); }
        return r;
    }

    public static int Main17()
    {
        // ---------------------------------------------------------------- 1. the New York clock (pure)
        {
            int off = -7; var tue = new DateTime(2026, 11, 10);   // a Tuesday (server = New York + 7: 00:00 server = 17:00 NY = the rollover)
            Func<DateTime, bool> gap = t => CloseClock.InGap(t, off, 45);
            Check(!gap(tue.AddHours(23).AddMinutes(14)) && gap(tue.AddHours(23).AddMinutes(15)) && gap(tue.AddHours(23).AddMinutes(59)) && gap(tue.AddDays(1)) && gap(tue.AddDays(1).AddMinutes(59)) && !gap(tue.AddDays(1).AddHours(1)) && !gap(tue.AddHours(10)),
                "⏰ the window: NY 16:14 trades, 16:15 … 17:00 … 17:59 = no new trades, 18:00 NY (the reopen) trades again");
            DateTime c, ro; double left; CloseClock.InGap(tue.AddHours(23).AddMinutes(28), off, 45, out c, out left, out ro);
            Check(c == tue.AddHours(17) && Eq(left, 32) && ro == tue.AddHours(18) && CloseClock.GapText(tue.AddHours(23).AddMinutes(28), off, 45) == "⏰ no new trades until 18:00 NY — the close in 32 min",
                "⏰ the banner line: \"⏰ no new trades until 18:00 NY — the close in 32 min\"", CloseClock.GapText(tue.AddHours(23).AddMinutes(28), off, 45));
            Check(CloseClock.GapText(tue.AddHours(10), off, 45) == "" && CloseClock.GapSoonText(tue.AddHours(21), off, 45) == "⏰ no new trades from 16:15 NY (45 min before the 17:00 close)" && CloseClock.GapSoonText(tue.AddHours(12), off, 45) == ""
                && !CloseClock.InGap(tue.AddHours(23).AddMinutes(30), off, 0) && CloseClock.GapText(tue.AddHours(23).AddMinutes(30), off, 0) == "",
                "⏰ outside the window nothing; 3 h ahead one quiet line; 0 minutes = off");
            // DST: IC Markets' server follows New York's DST (always NY + 7) → the same server times in January (EST) and July (EDT)
            var jan = new DateTime(2026, 1, 13); var jul = new DateTime(2026, 7, 14); var mar = new DateTime(2026, 3, 9); var nov = new DateTime(2026, 11, 2);
            Check(new[] { jan, jul, mar, nov }.All(d => !gap(d.AddHours(23).AddMinutes(14)) && gap(d.AddHours(23).AddMinutes(15)) && !gap(d.AddDays(1).AddHours(1))),
                "⏰ DST: winter, summer and the switch weeks (Mon 9 Mar, Mon 2 Nov) — the close is 00:00 server = 17:00 NY every day (the offset the app already uses)");
            Check(CloseClock.InGap(new DateTime(2026, 11, 10, 22, 15, 0), -6, 45) && !CloseClock.InGap(new DateTime(2026, 11, 10, 22, 14, 0), -6, 45),
                "⏰ another broker's offset (NY = server − 6): the window follows NY = server + NY OFFSET");
            // Friday: the weekly close → until Sunday 18:00 NY
            var fri = new DateTime(2026, 11, 13);
            Check(gap(fri.AddHours(23).AddMinutes(20)) && CloseClock.GapText(fri.AddHours(23).AddMinutes(20), off, 45) == "⏰ no new trades until Sun 18:00 NY — the close in 40 min" && gap(fri.AddDays(1).AddHours(5)) && gap(fri.AddDays(2).AddHours(12))
                && gap(fri.AddDays(3).AddMinutes(59)) && !gap(fri.AddDays(3).AddHours(1)) && CloseClock.GapText(fri.AddDays(1).AddHours(5), off, 45).Contains("Friday: the weekend"),
                "⏰ Friday: 16:15 NY → no new trades until Sunday 18:00 NY (Saturday / Sunday afternoon blocked, Sunday 18:00 NY = server Mon 01:00 trades)", CloseClock.GapText(fri.AddHours(23).AddMinutes(20), off, 45));
            var g1 = CloseClock.GateText(tue.AddHours(23).AddMinutes(20), off, 45); var g2 = CloseClock.GateText(tue.AddHours(23).AddMinutes(50), off, 45); Func<string, string> key = g => g.Substring(0, g.IndexOf(" (New York "));
            Check(g1.StartsWith("⏰ NO NEW TRADES BEFORE THE CLOSE") && key(g1) == key(g2) && g1 != g2, "⏰ the STORY reason keeps the same key through the window (logged once), the minutes change after \" (New York \"");
            double bl; Check(CloseClock.InBeWindow(tue.AddHours(23).AddMinutes(50), off, 10, out bl) && Eq(bl, 10) && !CloseClock.InBeWindow(tue.AddHours(23).AddMinutes(49), off, 10, out bl) && !CloseClock.InBeWindow(tue.AddDays(1).AddMinutes(5), off, 10, out bl)
                && CloseClock.BeCloseNow(0.5) && CloseClock.BeCloseNow(12) && !CloseClock.BeCloseNow(0.49) && !CloseClock.BeCloseNow(-1100) && !CloseClock.BeCloseNow(double.NaN),
                "🌙 the breakeven window = the last 10 min before 17:00 NY (not after it); ≥ +$0.50 closes, anything below (−$1,100 …) never");
        }
        // ---------------------------------------------------------------- 2. the setting (save / load / diff)
        {
            var d = new AutoSettings(); var s = new AutoSettings { CloseGap = true, CloseGapMin = 30, CloseIfBe = true, CloseBeMin = 5 }; var l = AutoSettings.Load(s.Save());
            Check(!d.CloseGap && !d.CloseIfBe && d.GapMin == 0 && l.CloseGap && l.CloseGapMin == 30 && l.GapMin == 30 && l.CloseIfBe && l.CloseBeMin == 5,
                "⏰ 🌙 survive save / load; a raw setting has both OFF (the presets decide)");
            var old = "sym=XAUUSD;tf=1;entry=0.05;target=10;rg=0;preset=DEFENDER"; var lo = AutoSettings.Load(old);
            Check(!lo.CloseGap && !lo.CloseIfBe && !lo.RiskGuard && Eq(lo.EntryLots, 0.05), "⏰ an existing saved line (before 13p) loads with ⏰ / 🌙 OFF and its RISK GUARD as saved — nothing changes silently");
            var diff = AutoSettings.Diff(lo, s); Check(diff.Any(x => x.StartsWith("⏰ NO NEW TRADES BEFORE THE CLOSE: 0 → 1")) && diff.Any(x => x.StartsWith("🌙 AT THE CLOSE: CLOSE IF ≥ BREAKEVEN: 0 → 1")), "⏰ 🌙 APPLY NOW names the changes", string.Join(" | ", diff));
        }
        // ---------------------------------------------------------------- 3. ⭐ 13p: DEFENDER's defaults for NEW settings, the existing ones unchanged until APPLY
        {
            var def = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000);
            Check(def.RiskGuard && def.GuardMode == "LOCK" && def.GuardMaxLead == 3 && def.CloseGap && def.CloseGapMin == 45 && !def.CloseIfBe && def.HedgeTrigger == "LEAD" && def.Preset == "DEFENDER",
                "⭐ DEFENDER (a new window's default) = 🛡 RISK GUARD ON (LOCK, lead ≤ 3) + ⏰ 45 min; 🌙 stays OFF (harmful alone in the bench)");
            var sl = AutoSettings.Model("SAFELIVE", new AutoSettings(), 1000); var fa = AutoSettings.Model("FULLAUTO", new AutoSettings(), 5000);
            Check(!sl.RiskGuard && !sl.CloseGap && !fa.RiskGuard && !fa.CloseGap, "⭐ the other presets built on DEFENDER (SAFE LIVE, FULL AUTO) keep their own values");
            var mine = AutoSettings.Load("sym=XAUUSD;tf=1;entry=0.05;add=0.1;target=10;hsize=STEPLEAD;rg=0;glead=5;gmode=HOLD;preset=DEFENDER;cmp=ALWAYS"); var before = mine.Save();
            var rec = AutoSettings.Recommended13p(mine); var chg = AutoSettings.Diff(mine, rec);
            Check(mine.Save() == before && !mine.RiskGuard && rec.RiskGuard && rec.CloseGap && rec.CloseGapMin == 45 && rec.HedgeSize == mine.HedgeSize && Eq(rec.EntryLots, 0.05) && Eq(rec.Target, 10) && rec.CompressMode == mine.CompressMode,
                "⭐ RECOMMENDED = YOUR settings + RISK GUARD + ⏰ — the saved ones are untouched until ⭐ APPLY (a copy)");
            Check(chg.Count == 4 && chg.Contains("🛡 RISK GUARD: 0 → 1") && chg.Contains("🛡 GUARD MODE: HOLD → LOCK") && chg.Contains("🛡 MAX LEAD STEPS: 5 → 3") && chg.Contains("⏰ NO NEW TRADES BEFORE THE CLOSE: 0 → 1"),
                "⭐ the card lists the exact changes (AutoSettings.Diff) — nothing else", string.Join(" | ", chg));
            Check(AutoSettings.Diff(rec, AutoSettings.Recommended13p(rec)).Count == 0 && AutoSettings.Rec13pEvidence.Contains("−$110k") && AutoSettings.Rec13pEvidence.Contains("14 blow-ups") && AutoSettings.Rec13pEvidence.Contains("−$9.9k") && AutoSettings.Rec13pEvidence.Contains("NOT a profit edge") && AutoSettings.Rec13pEvidence.Contains("swap paid on both sides"),
                "⭐ applied once = nothing more to apply; the honest numbers (−$110k / 14 → −$9.9k / 2, not a profit edge, locks pay swap both sides)");
            Check(CloseClock.BeEvidence.Contains("HARMFUL") && CloseClock.BeEvidence.Contains("neutral") && CloseClock.GapEvidence.Contains("no proven benefit") && CloseClock.GapEvidence.Contains("ASSUMED swap"),
                "🌙 / ⏰ tooltips state the 6-year finding (neutral with RISK GUARD, harmful alone; no proven benefit; the swap was an assumption)");
        }
        // ---------------------------------------------------------------- 4. ⏰ the engine: no new iteration in the window, hedges / exits go on
        var wed = new DateTime(2026, 11, 11);   // server time; NY = −7
        {
            var s = Base(); s.CloseGap = true; s.CloseGapMin = 45;   // start at NY 16:20 (server 23:20): inside the window
            var r = Run(s, wed.AddHours(23).AddMinutes(20), Enumerable.Repeat(4000.0, 120).ToArray());
            var first = r.Sent.FirstOrDefault(x => x.Item2 == "ORDER");
            Check(first != null && first.Item1 >= wed.AddDays(1).AddHours(1) && first.Item1 < wed.AddDays(1).AddHours(1).AddMinutes(2) && r.Au.Trades.Count >= 1,
                "⏰ START at NY 16:20: the first BUY waits until 18:00 NY (server 01:00) — nothing sent before", first == null ? "none" : first.Item1.ToString("HH:mm:ss", IC));
            int told = r.Story.Count(x => x.Contains("NOT TRADING NOW: ⏰ NO NEW TRADES BEFORE THE CLOSE"));
            Check(told == 1 && r.Story.Any(x => x.Contains("TRADING ALLOWED again")), "⏰ the STORY says it ONCE for the window (then TRADING ALLOWED again)", told + "×");
            var off = Base(); var r0 = Run(off, wed.AddHours(23).AddMinutes(20), Enumerable.Repeat(4000.0, 20).ToArray());
            var f0 = r0.Sent.FirstOrDefault(x => x.Item2 == "ORDER"); Check(f0 != null && f0.Item1 < wed.AddHours(23).AddMinutes(22), "⏰ OFF: the same START buys at once");
        }
        {   // an iteration opened at NY 15:30, falling into the window: its hedge still goes; it then hits its target inside the window → the NEXT one waits for 18:00
            var s = Base(); s.CloseGap = true; s.CloseGapMin = 45; s.Target = 30; s.PauseSec = 60;
            var path = Enumerable.Repeat(4000.0, 45).Concat(new[] { 3999.5, 3999, 3998.5, 3998, 3998.5, 3999, 3999.2, 3999 }).Concat(Enumerable.Range(1, 24).Select(i => 3999 - i * 0.5)).Concat(Enumerable.Repeat(3987.0, 100)).ToArray();   // a swing low at NY 16:18, broken in the window
            var r = Run(s, wed.AddHours(22).AddMinutes(30), path);
            var hedge = r.Au.Trades.FirstOrDefault(t => t.Why.StartsWith("ADD-ON")); var win = wed.AddHours(23).AddMinutes(15);
            Check(hedge != null && hedge.Time >= win && hedge.Time < wed.AddDays(1), "⏰ the open basket's HEDGE goes inside the window (NY " + (hedge != null ? hedge.Time.AddHours(-7).ToString("HH:mm", IC) : "—") + ")");
            var it1 = r.Au.Iterations.FirstOrDefault(); var orders = r.Sent.Where(x => x.Item2 == "ORDER").ToList();
            var after = it1 != null && it1.Done ? orders.Where(x => x.Item1 > it1.End).ToList() : new List<Tuple<DateTime, string, string>>();
            Check(it1 != null && it1.Done && it1.End >= win && it1.End < wed.AddDays(1) && it1.Reason.Contains("TARGET") && after.Count > 0 && after[0].Item1 >= wed.AddDays(1).AddHours(1),
                "⏰ its exit (TARGET) works inside the window; the next iteration waits until 18:00 NY", it1 == null ? "no iteration" : it1.Reason + " " + it1.End.ToString("HH:mm", IC) + " next " + (after.Count > 0 ? after[0].Item1.ToString("HH:mm", IC) : "—"));
        }
        // ---------------------------------------------------------------- 5. 🌙 close at the close only if ≥ breakeven
        {
            var s = Base(); s.HedgeMode = "NONE"; s.CloseIfBe = true; s.CloseBeMin = 10;   // BUY at NY 16:30, drifting up: +$ at 16:50 → closes
            var up = Enumerable.Range(0, 60).Select(i => 4000 + i * 0.05).ToArray();
            var r = Run(s, wed.AddHours(23).AddMinutes(30), up);
            var it = r.Au.Iterations.FirstOrDefault(); var close = r.Sent.FirstOrDefault(x => x.Item2 == "CLOSEALL");
            Check(it != null && it.Done && it.Reason == "🌙 BE AT THE CLOSE" && close != null && close.Item1 >= wed.AddHours(23).AddMinutes(50) && close.Item1 < wed.AddHours(23).AddMinutes(51) && it.Result > 0,
                "🌙 a basket ≥ breakeven 10 min before the close → CLOSE ALL through AutoSend at NY 16:50", it == null ? "—" : it.Reason + " " + (close != null ? close.Item1.ToString("HH:mm:ss", IC) : "no close"));
            Check(r.Story.Any(x => x.Contains("🌙 AT THE CLOSE, CLOSE IF ≥ BREAKEVEN. WHY")), "🌙 the STORY says why");
            var dn = Enumerable.Range(0, 60).Select(i => 4000 - i * 0.2).ToArray();
            string bct = ""; var r2 = Run(s, wed.AddHours(23).AddMinutes(30), dn, null, null, (rr, k) => { if (rr.Au.BeCloseText != "") bct = rr.Au.BeCloseText; });
            var it2 = r2.Au.Iterations.FirstOrDefault(); int kept = r2.Story.Count(x => x.Contains("KEPT OVER THE ROLLOVER"));
            Check(it2 != null && !it2.Done && !r2.Sent.Any(x => x.Item2 == "CLOSEALL" || x.Item2 == "CLOSE") && r2.Sb.State.Positions.Count == 1 && kept == 1 && bct.Contains("kept over the rollover"),
                "🌙 a basket in DRAWDOWN is never closed by it — kept over the rollover (said once), nothing sent", "kept " + kept + " sent " + string.Join(",", r2.Sent.Select(x => x.Item2)));
            var off = Base(); off.HedgeMode = "NONE"; var r3 = Run(off, wed.AddHours(23).AddMinutes(30), up);
            Check(!r3.Sent.Any(x => x.Item2 == "CLOSEALL") && r3.Au.Iterations[0].Done == false, "🌙 OFF (the default): nothing closes at the close");
            // ✋ MANUAL MANAGE → only a 💡 suggestion, nothing sent
            var r4 = Run(s, wed.AddHours(23).AddMinutes(30), up, null, null, (rr, k) => { if (rr.Au.Trades.Count == 1 && !rr.Au.ManualManage) rr.Au.SetManualManage(true); });
            Check(!r4.Sent.Any(x => x.Item2 == "CLOSEALL") && r4.Sb.State.Positions.Count == 1 && r4.Au.Suggestion.Contains("BE AT THE CLOSE"), "🌙 ✋ MANUAL MANAGE: the close is only a 💡 suggestion (nothing sent)", r4.Au.Suggestion);
            // refused by AutoSend (not armed / PROP / MT5 off) → nothing reaches the account
            var r5 = Run(s, wed.AddHours(23).AddMinutes(30), up, (c, a, sb) => c == "CLOSEALL" ? null : sb.Command(c, a));
            Check(r5.Sent.Any(x => x.Item2 == "CLOSEALL") && r5.Sb.State.Positions.Count == 1, "🌙 refused by AutoSend → the position stays (no other path)");
        }
        // ---------------------------------------------------------------- 6. ⭐ APPLY on a running basket = ⚡ APPLY NOW semantics (nothing closed / opened by it)
        {
            var s = Base(); s.HedgeMode = "NONE"; int sentAt = -1; int nBefore = 0;
            var r = Run(s, wed.AddHours(10), Enumerable.Range(0, 40).Select(i => 4000 - i * 0.05).ToArray(), null, null, (rr, k) =>
            {
                if (sentAt < 0 && rr.Au.Trades.Count == 1) { sentAt = k; nBefore = rr.Sent.Count; rr.Au.ApplyNow(AutoSettings.Recommended13p(rr.Au.Set)); }
            });
            Check(sentAt >= 0 && r.Au.Set.RiskGuard && r.Au.Set.CloseGap && r.Sent.Count == nBefore && r.Sb.State.Positions.Count == 1 && !r.Au.Iterations[0].Done,
                "⭐ applied to a running basket: in use at once, the basket stays open, nothing sent by the button", "sent " + (r.Sent.Count - nBefore));
        }
        // ---------------------------------------------------------------- 7. 💵 the DEMO's costs per account + the migration + the labels
        {
            Check(DemoCosts.Key("pct", "5298760@ICMarketsSC-MT5") == "demo.pct.5298760@ICMarketsSC-MT5" && DemoCosts.Key("pct", "11@Duramarkets-Live") != DemoCosts.Key("pct", "5298760@ICMarketsSC-MT5"), "💵 the costs are keyed per account (login@server)");
            Check(DemoCosts.Default("kind", "ICMarketsSC-MT5") == "RAW" && DemoCosts.Default("com", "ICMarketsSC-MT5") == "0" && DemoCosts.Default("mk", "ICMarketsSC-MT5") == "0" && DemoCosts.Default("pct", "ICMarketsSC-MT5") == "0"
                && DemoCosts.Default("pct", "Duramarkets-Live") == "0.001" && DemoCosts.Default("pct", "OtherBroker-Demo") == "0",
                "💵 a new IC Markets demo = spread only, no commission, no extra spread; Duramarkets keeps its 0.001 %; others 0 %");
            var ic = new SimAccountSettings { Kind = "RAW", MarkupPts = 2, CommissionPct = 0.001 }; var note = DemoCosts.Migrate(ic, "ICMarketsSC-MT5", false);
            Check(Eq(ic.CommissionPct, 0) && note == "commission was 0.001 % inherited from another account — now 0 (extra spread +2 pts kept); ⚙ DEMO ACCOUNT to change" && Eq(ic.MarkupPts, 2), "💵 migration: the IC Markets demo's inherited 0.001 % → 0, the note for the header (the extra spread kept)", note);
            var du = new SimAccountSettings { CommissionPct = 0.001 }; var own = new SimAccountSettings { CommissionPct = 0.002 }; var zero = new SimAccountSettings();
            Check(DemoCosts.Migrate(du, "Duramarkets-Live", false) == "" && Eq(du.CommissionPct, 0.001) && DemoCosts.Migrate(own, "ICMarketsSC-MT5", true) == "" && Eq(own.CommissionPct, 0.002) && DemoCosts.Migrate(zero, "ICMarketsSC-MT5", false) == "" && DemoCosts.Migrate(null, "x", false) == "",
                "💵 the migration never touches a Duramarkets demo, a value the account set itself, or a demo without a %");
            Check(DemoCosts.KindLabel("RAW") == "SPREAD ONLY (no commission)" && DemoCosts.KindLabel("STANDARD") == "RAW SPREAD + COMMISSION per lot"
                && DemoCosts.Describe(new SimAccountSettings { Kind = "RAW", MarkupPts = 0 }) == "SPREAD ONLY (no commission)" && DemoCosts.Describe(new SimAccountSettings { Kind = "RAW", MarkupPts = 2, CommissionPct = 0.001 }) == "SPREAD ONLY +2 pts spread + commission 0.001 % a deal"
                && DemoCosts.Describe(new SimAccountSettings { Kind = "STANDARD", CommissionPerLot = 7 }) == "RAW SPREAD + $7 / lot",
                "💵 the labels read like the brokers' (the header shows the commission % too)", DemoCosts.Describe(new SimAccountSettings { Kind = "RAW", MarkupPts = 2, CommissionPct = 0.001 }));
            Check(Eq(new SimAccountSettings { Kind = "RAW", CommissionPerLot = 7 }.Commission, 0) && Eq(new SimAccountSettings { Kind = "STANDARD", CommissionPerLot = 7 }.Commission, 7) && new SimAccountSettings { Kind = "RAW" }.Widened,
                "💵 the stored values stay compatible (RAW = no commission per lot + the extra spread, STANDARD = commission per lot)");
        }
        // ---------------------------------------------------------------- 8. 💤 the swap: the broker's or "unknown", never a bench guess
        {
            Check(!HedgeAuto.SwapKnownOf(null) && !HedgeAuto.SwapKnownOf(new Mt5Symbol { Name = "XAUUSD" }) && HedgeAuto.SwapKnownOf(new Mt5Symbol { Name = "XAUUSD", HasX = true }) && HedgeAuto.SwapKnownOf(new Mt5Symbol { Name = "XAUUSD", SwapLong = -62.4 }),
                "💤 the swap is known only when MT5 sent the symbol's swap (SYMX) or a simulated account has its own");
            var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = wed.Date, Balance = 10000, Kind = "STANDARD", FixedSpread = 10 }, Gold(), Mids(wed.AddHours(1), Enumerable.Repeat(4000.0, 5)));
            Check(HedgeAuto.SwapKnownOf(sb.State.Symbols["XAUUSD"]), "💤 a BACKTEST / DEMO symbol's swap = its own spec (known, even 0)");
        }
        Console.WriteLine(fails == 0 ? "09p: all green" : "09p: " + fails + " FAILED");
        return fails;
    }
}
