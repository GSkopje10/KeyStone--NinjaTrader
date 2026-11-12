// 🧾 BUILD 11t — EXECUTIONS as a per-account trade journal: fills → iterations flat to flat (FIFO), legs (entry / add / partial exits / exit),
// commissions per leg from the executions only (estimated + marked when the connection reports 0), MFE / MAE, day totals, reconciliation, the day file
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class ExecJournalTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static bool Near(double a, double b, double eps = 0.005) { return Math.Abs(a - b) <= eps; }
    public static int Main()
    {
        const string A = "TAKEPROFITPRO382305483", I = "MNQ 12-26"; Func<string, double> pv = s => KeystoneExecJournal.Root(s) == "MGC" ? 10 : 2; Func<string, double> est = s => 0.62;
        var d = new DateTime(2026, 11, 12, 9, 31, 0);
        Func<string, int, double, double, int, string, string, KeystoneXFill> F = (id, q, px, c, sec, name, kind) => new KeystoneXFill { Id = id, Account = A, Instrument = I, SignedQty = q, Price = px, Commission = c, Time = d.AddSeconds(sec), OrderName = name, OrderKind = kind };

        // 1. the user's day (11s screenshot): LONG 2 @28175 → 28167.25 by hand, LONG 5 @28196.75 → 28214.75 — commissions from the executions ($0.52 / $1.07 a side)
        var fills = new List<KeystoneXFill> { F("x1", 2, 28175.00, 1.04, 0, "", "MARKET"), F("x2", -2, 28167.25, 1.04, 95, "Close", "MARKET"), F("x3", 5, 28196.75, 5.35, 600, "Entry", "MARKET"), F("x4", -5, 28214.75, 5.35, 1500, "Target1", "LIMIT") };
        var its = KeystoneExecJournal.Build(fills, pv, est);
        Check(its.Count == 2 && its.All(i => !i.Open), "the user's day: 2 iterations, both closed", its.Count.ToString());
        var a = its[0]; var b = its[1];
        Check(a.Side == 1 && a.MaxQty == 2 && Near(a.Gross, -31) && Near(a.Commission, 2.08) && Near(a.Net, -33.08) && !a.CommEst, "#1 LONG 2 28175 → 28167.25: gross −$31, fees $2.08 (not −$391), net −$33.08", a.Gross + " " + a.Commission + " " + a.Net);
        Check(b.Side == 1 && b.MaxQty == 5 && Near(b.Gross, 180) && Near(b.Commission, 10.70) && Near(b.Net, 169.30), "#2 LONG 5 28196.75 → 28214.75: gross +$180, fees $10.70, net +$169.30 (not −$797)", b.Gross + " " + b.Commission + " " + b.Net);
        Check(a.Legs.Count == 2 && a.Legs[0].Kind == "ENTRY" && a.Legs[1].Kind == "EXIT" && a.Legs[0].Who.StartsWith("by hand") && a.Legs[1].Who == "flatten / close" && b.Legs[1].Who == "bracket target", "legs: ENTRY by hand → EXIT (flatten / close); #2 exit = bracket target", string.Join(" | ", a.Legs.Select(l => l.Kind + " " + l.Who)) + " / " + b.Legs[1].Who);
        Check(Near(a.AvgIn, 28175) && Near(a.AvgOut, 28167.25) && (a.End - a.Start).TotalSeconds == 95 && a.Partials == 0, "#1 avg in / out, duration 95 s, no partials");
        var days = KeystoneExecJournal.Days(its); var dy = days.Single();
        Check(dy.Iterations == 2 && dy.Won == 1 && dy.Lost == 1 && Near(dy.Net, 136.22) && Near(dy.Net, its.Sum(i => i.Net)) && Near(dy.Gross, 149) && Near(dy.Commission, 12.78) && Near(dy.Best, 169.30) && Near(dy.Worst, -33.08), "the day = Σ the iterations: net +$136.22, gross +$149, fees $12.78, best +$169.30, worst −$33.08", dy.Net + " " + dy.Gross);
        // MFE / MAE from a price path (the screenshot: peak +$57 / lowest −$70 on #1)
        KeystoneExecJournal.ApplyPath(a, new[] { Tuple.Create(d.AddSeconds(10), 28189.25), Tuple.Create(d.AddSeconds(50), 28157.50), Tuple.Create(d.AddSeconds(500), 28100.0) });
        Check(Near(a.Mfe, 57) && Near(a.Mae, -70) && Near(a.MfePts, 14.25) && Near(a.MaePts, -17.5) && Near(a.DdUsed, 70), "#1 MFE +$57 (14.25 pts) / MAE −$70 (−17.5 pts) from the path; a price after the close is ignored", a.Mfe + " " + a.Mae + " " + a.MfePts + " " + a.MaePts);

        // 2. partials: LONG 5, sell 2 at +10 pts, sell 3 at +20 → one iteration, 2 exits, realized per leg $40 / $120
        var p = KeystoneExecJournal.Build(new List<KeystoneXFill> { F("p1", 5, 20000, 2.5, 0, "Entry", "MARKET"), F("p2", -2, 20010, 1.0, 60, "Target1", "LIMIT"), F("p3", -3, 20020, 1.5, 180, "Target2", "LIMIT") }, pv, est);
        var pi = p.Single();
        Check(pi.Legs.Count == 3 && pi.Partials == 1 && pi.Legs[1].Kind == "PARTIAL EXIT 1" && pi.Legs[2].Kind == "EXIT" && Near(pi.Legs[1].Realized, 40) && Near(pi.Legs[2].Realized, 120) && pi.Legs[1].PosAfter == 3, "partials: ENTRY 5 → PARTIAL EXIT 1 (2 @ +10: +$40, 3 left) → EXIT (3 @ +20: +$120)", string.Join(" | ", pi.Legs.Select(l => l.Kind + " " + l.Realized)));
        Check(Near(pi.Gross, 160) && Near(pi.Commission, 5) && Near(pi.Net, 155) && Near(pi.AvgOut, 20016) && pi.MaxQty == 5, "partials: gross +$160, fees $5 (per leg), net +$155, avg out 20,016");

        // 3. adds + FIFO: BUY 1 @100, BUY 1 @110 (ADD), SELL 1 @120 (FIFO closes the 100 lot: +$40), SELL 1 @105 (−$10)
        var f2 = KeystoneExecJournal.Build(new List<KeystoneXFill> { F("f1", 1, 100, 0.5, 0, "", "MARKET"), F("f2", 1, 110, 0.5, 10, "", "MARKET"), F("f3", -1, 120, 0.5, 20, "", "LIMIT"), F("f4", -1, 105, 0.5, 30, "Stop1", "STOP") }, pv, est).Single();
        Check(f2.Adds == 1 && f2.Legs[1].Kind == "ADD" && Near(f2.Legs[2].Realized, 40) && Near(f2.Legs[3].Realized, -10) && Near(f2.Gross, 30) && Near(f2.AvgIn, 105) && f2.Legs[3].Who == "bracket stop" && f2.Legs[2].Who.StartsWith("limit (target)"), "add + FIFO: the first lot closes first (+$40, then −$10), gross +$30, avg in 105", string.Join(" | ", f2.Legs.Select(l => l.Kind + " " + l.Realized + " " + l.Who)));

        // 4. a reversal through flat: SHORT 2 @100, BUY 5 @90 → close 2 (+$40) + a new LONG 3 iteration; the fill's commission split 2 / 3
        var rv = KeystoneExecJournal.Build(new List<KeystoneXFill> { F("r1", -2, 100, 1.0, 0, "", "MARKET"), F("r2", 5, 90, 2.5, 60, "", "MARKET") }, pv, est);
        Check(rv.Count == 2 && !rv[0].Open && rv[0].Side == -1 && Near(rv[0].Gross, 40) && Near(rv[0].Commission, 2.0) && rv[1].Open && rv[1].Side == 1 && rv[1].OpenQty == 3 && Near(rv[1].Commission, 1.5) && rv[1].Legs[0].Kind == "ENTRY", "reversal: SHORT 2 closed +$40 (fees $1 + $1), LONG 3 opens (fees $1.50)", string.Join(" / ", rv.Select(i => i.Side + " " + i.Gross + " " + i.Commission + " " + i.OpenQty)));
        // the open one, live: price 95 → open P/L +$30, MFE / MAE follow; NetIfClosed = realized + open − fees
        KeystoneExecJournal.Live(rv[1], 95, d.AddSeconds(120)); KeystoneExecJournal.Live(rv[1], 88, d.AddSeconds(130));
        Check(Near(rv[1].OpenPl, -12) && Near(rv[1].Mfe, 30) && Near(rv[1].Mae, -12) && Near(rv[1].NetIfClosed, -13.5), "open iteration live: open P/L −$12 at 88, MFE +$30 (at 95), MAE −$12, if closed −$13.50", rv[1].OpenPl + " " + rv[1].Mfe + " " + rv[1].Mae + " " + rv[1].NetIfClosed);
        var dr = KeystoneExecJournal.Days(rv).Single(); Check(dr.Iterations == 1 && dr.OpenNow == 1 && Near(dr.Net, 38), "the day counts the closed iteration (+$38), the open one apart");

        // 5. no commission from the connection → the per-side estimate, marked est.
        var e1 = KeystoneExecJournal.Build(new List<KeystoneXFill> { F("e1", 2, 100, 0, 0, "", "MARKET"), F("e2", -2, 101, 0, 5, "", "MARKET") }, pv, est).Single();
        Check(e1.CommEst && Near(e1.Commission, 2.48) && e1.Legs.All(l => l.CommEst) && Near(e1.Net, 4 - 2.48), "no commission reported → 4 sides × $0.62 = $2.48, marked est.");

        // 6. the same execution twice (a restart / the saved file + NinjaTrader) is counted once
        var into = new List<KeystoneXFill>(fills); int added = KeystoneExecJournal.Merge(into, new[] { F("x1", 2, 28175, 1.04, 0, "", "MARKET"), F("x9", 1, 28200, 0.52, 2000, "", "MARKET") });
        Check(added == 1 && into.Count == 5, "merge by execution id: x1 again is ignored, x9 is new");

        // 7. two instruments at once stay separate iterations
        var mg = new KeystoneXFill { Id = "g1", Account = A, Instrument = "MGC 02-27", SignedQty = -1, Price = 4000, Commission = 0.8, Time = d.AddSeconds(30) };
        var two = KeystoneExecJournal.Build(new List<KeystoneXFill>(fills) { mg }, pv, est);
        Check(two.Count == 3 && two.Count(i => i.Open) == 1 && two.Single(i => i.Open).Instrument == "MGC 02-27" && KeystoneExecJournal.OpenByRoot(two)["MGC"] == -1, "MNQ + MGC: separate iterations; MGC SHORT 1 open");

        // 8. day file: write → read back the fills (source) + the snapshots / MFE kept by key, rebuilt the same
        a.AtClose = new KeystoneXSnap { Time = a.End, Balance = 51322.42, Today = -33.08, Realized = -33.08, Start = 50000, Peak = 51602.75, Floor = 49602.75, Buffer = 1719.67, ToLock = 397.25, Funded = true };
        a.AtStart = new KeystoneXSnap { Balance = 51355.50, Peak = 51602.75, Floor = 49602.75, Buffer = 1752.75 };
        var txt = KeystoneExecJournal.Format(fills, its);
        var back = new List<KeystoneXFill>(); var saved = new Dictionary<string, KeystoneXIter>(); KeystoneExecJournal.Parse(txt, back, saved);
        var re = KeystoneExecJournal.Build(back, pv, est); foreach (var r in re) { KeystoneXIter sv; if (saved.TryGetValue(r.Key, out sv)) KeystoneExecJournal.Carry(r, sv); }
        Check(back.Count == 4 && re.Count == 2 && Near(re[0].Net, -33.08) && Near(re[1].Net, 169.30) && re[0].Key == a.Key, "the day file reads back: 4 fills → the same 2 iterations", back.Count + " " + re.Count);
        Check(re[0].AtClose.Has && Near(re[0].AtClose.Floor, 49602.75) && Near(re[0].AtClose.Buffer, 1719.67) && re[0].AtClose.Funded && Near(re[0].AtStart.Buffer, 1752.75) && Near(re[0].Mfe, 57) && Near(re[0].Mae, -70), "the snapshot (balance, floor, buffer, peak) and MFE / MAE come back with the iteration");
        Check(txt.Contains("LEG,") && txt.Contains("PARTIAL") == false && txt.Split('\n').Count(l => l.StartsWith("ITER,") && !l.StartsWith("ITER,key")) == 2 && txt.Split('\n').Count(l => l.StartsWith("LEG,") && !l.StartsWith("LEG,key")) == 4, "the file lists every iteration with its legs");

        // 9. reconciliation with NinjaTrader is a separate line, never mixed into the rows
        var rec = KeystoneExecJournal.Reconcile(its, 136.22, 149);
        Check(rec.Count == 1 && rec[0].Contains("✓ match"), "NinjaTrader's realized = the journal → ✓ match", string.Join(" / ", rec));
        var rec2 = KeystoneExecJournal.Reconcile(its, 357.75, 1725.50);
        Check(rec2.Count >= 2 && rec2[0].Contains("difference") && rec2.Any(l => l.Contains("GROSS differs")) && Near(its.Sum(i => i.Net), 136.22), "the screenshot's NinjaTrader numbers (+$357.75 / gross +$1,725.50) → a difference line + why; the rows keep their own numbers", string.Join(" / ", rec2));
        // the 11q per-side rate from gross − realized is refused when absurd (here $1,367.75 / 14 sides)
        string src; double ps = KeystoneFundedLiq.PerSide(0, 14, 1725.50, 357.75, 0, 1.07, out src);
        Check(Near(ps, 1.07) && src == "last rate learned", "gross − realized = $97.70 a side is refused (> $5) → the last rate learned", ps + " " + src);

        Console.WriteLine(failures == 0 ? "ALL EXEC JOURNAL TESTS PASSED" : failures + " EXEC JOURNAL TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
