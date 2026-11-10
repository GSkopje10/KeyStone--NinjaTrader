// 09g: 🤖 WHAT IF — would the automation ALONE have saved the basket he touched by hand? #43 (export_1109c): the basket just before his manual
// SELL 0.65 (10 Nov 01:36:31) is replayed with his settings on the candles that followed — in-memory only, deterministic, resumable
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using KeystoneMT5;
public static class NineGTests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static string Dir() { var d = AppContext.BaseDirectory; for (int i = 0; i < 6 && !Directory.Exists(Path.Combine(d, "data")); i++) d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar)); return Path.Combine(d, "data"); }
    static List<Mt5Bar> Candles() { return File.ReadAllLines(Path.Combine(Dir(), "xau-m1-1105-1110.csv")).Skip(1).Select(l => l.Split(',')).Select(x => new Mt5Bar { Time = DateTime.ParseExact(x[0], "yyyy-MM-dd HH:mm:ss", IC), O = double.Parse(x[1], IC), H = double.Parse(x[2], IC), L = double.Parse(x[3], IC), C = double.Parse(x[4], IC), V = long.Parse(x[5], IC), Spread = 25 }).ToList(); }
    static SimSpec Gold() { return new SimSpec { Name = "XAUUSD", Digits = 2, Point = 0.01, Contract = 100, TickValue = 1, TickSize = 0.01, VolMin = 0.01, VolStep = 0.01, VolMax = 100, NyOffsetHours = -7, SwapLong = -62.4, SwapShort = 28.1, SwapMode = 1, Swap3Day = DayOfWeek.Wednesday }; }
    public static WhatIfCase Case43()
    {   // #43 from export_1109c/03-trades.csv: the 5 legs before 01:36:31, nothing banked, his settings (DEFENDER, entry 0.08, target $16), balance at its start $7,918.92
        var c = new WhatIfCase { N = 43, Symbol = "XAUUSD", Start = new DateTime(2026, 11, 9, 22, 56, 46), SnapAt = new DateTime(2026, 11, 10, 1, 36, 31), Banked = 0, MaxDd = -254.47, Balance = 7918.92, Commission = 0, Kind = "STANDARD", Spread = 25, Target = 16,
            Settings = string.Join(";", File.ReadAllLines(Path.Combine(Dir(), "settings-1109c.txt")).Select(l => l.Trim())), What = "SELL 0.65 @ 4139.31 in MT5 (desktop), 01:36:31" };
        c.Legs.Add(new WhatIfLeg { Ticket = 1, Side = "BUY", Lots = 0.08, Open = 4142.14, Time = new DateTime(2026, 11, 9, 22, 56, 46) });
        c.Legs.Add(new WhatIfLeg { Ticket = 2, Side = "SELL", Lots = 0.16, Open = 4137.38, Time = new DateTime(2026, 11, 9, 23, 26, 41) });
        c.Legs.Add(new WhatIfLeg { Ticket = 3, Side = "BUY", Lots = 0.16, Open = 4145.30, Time = new DateTime(2026, 11, 9, 23, 29, 56) });
        c.Legs.Add(new WhatIfLeg { Ticket = 4, Side = "SELL", Lots = 0.16, Open = 4137.03, Time = new DateTime(2026, 11, 9, 23, 37, 23) });
        c.Legs.Add(new WhatIfLeg { Ticket = 5, Side = "BUY", Lots = 0.16, Open = 4145.90, Time = new DateTime(2026, 11, 10, 1, 13, 16) });
        return c;
    }
    public static int Main10()
    {
        var bars = Candles(); var c = Case43(); WhatIf.Replay(c, bars, Gold());
        Console.WriteLine("  #43 WITHOUT YOU: " + c.WithoutYou + " | done " + c.Done + " " + c.Reason + " " + c.Result.ToString("0.00", IC) + " end " + c.End.ToString("dd HH:mm:ss", IC) + " worst " + c.Worst.ToString("0.00", IC) + " now " + c.NowTotal.ToString("0.00", IC) + " last " + c.LastBar.ToString("dd HH:mm", IC) + " path " + c.Path.Count);
        var c2 = Case43(); WhatIf.Replay(c2, bars, Gold());
        Check(!c.Done && Math.Abs(c.NowTotal + 216.88) < 0.005 && Math.Abs(c.Worst + 357.60) < 0.005 && c.LastBar > c.SnapAt && c.Path.Count > 0 && c2.WithoutYou == c.WithoutYou && c2.Result == c.Result && c2.Worst == c.Worst && c2.NowTotal == c.NowTotal && c.Worst <= c.MaxDd,
            "🤖 #43 replayed from 01:36:31 (the basket before his SELL 0.65) on the export's candles: \"" + c.WithoutYou + "\" — the same answer every time", c.WithoutYou + " / " + c2.WithoutYou);
        {   // the answer resumes after a restart: saved → loaded → replayed = the same; with fewer candles (live, earlier) it was still open, more candles carry it forward
            var saved = c.Save(); var back = WhatIfCase.Load(saved); WhatIf.Replay(back, bars, Gold());
            var early = Case43(); WhatIf.Replay(early, bars.Where(b => b.Time <= new DateTime(2026, 11, 10, 1, 50, 0)).ToList(), Gold());
            Check(back != null && back.WithoutYou == c.WithoutYou && back.Legs.Count == 5 && back.Settings == c.Settings && back.What == c.What && !early.Done && early.WithoutYou.StartsWith("still open") && early.LastBar < c.LastBar,
                "🤖 persisted per account: saved, loaded after a restart and replayed = the same answer; at 01:50 it was \"" + early.WithoutYou + "\" and goes on as candles arrive", (back == null ? "null" : back.WithoutYou) + " | " + early.WithoutYou);
        }
        {   // the verdict: you vs the automation alone (his #43 closed RESCUE BE +$0.67)
            var v = Case43(); WhatIf.Replay(v, bars, Gold()); v.ActualDone = true; v.Actual = 0.67; var verdict = v.Verdict;
            var a = new WhatIfCase { Target = 16, Done = true, Result = 16.4, LastBar = DateTime.Now, Actual = 16.2 }; var b = new WhatIfCase { Target = 16, Done = true, Result = -2900, Reason = "HARD STOP", LastBar = DateTime.Now, Actual = 0.67 }; var d = new WhatIfCase { Target = 16, Done = true, Result = 16, LastBar = DateTime.Now, Actual = -300 };
            Check(a.Verdict == "⚖ SIMILAR" && b.Verdict.StartsWith("✋ YOU SAVED IT +$2,900.67") && d.Verdict.StartsWith("🤖 WOULD HAVE DONE IT TOO (better by $316") && verdict.Length > 0,
                "🤖 verdicts: +$16.20 vs +$16.40 → ⚖ SIMILAR; +$0.67 vs a −$2,900 HARD STOP → ✋ YOU SAVED IT +$2,900.67; −$300 vs +$16 → 🤖 WOULD HAVE DONE IT TOO (better by $316); #43 (his +$0.67): " + verdict, a.Verdict + " | " + b.Verdict + " | " + d.Verdict);
            var sum = WhatIf.Summary(new[] { a, b, d });
            Check(sum.Contains("3 manual interventions") && sum.Contains("you saved 1") && sum.Contains("2 in profit"), "🤖 the summary: \"" + sum + "\"", sum);
        }
        {   // nothing is sent: the replay owns its SimBroker (no broker of yours is even passed in) — a fake live broker stays untouched
            var src = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Dir()), "..", "app", "WhatIf.cs"));
            Check(!src.Contains("IBroker ") && !src.Contains("Mt5Bridge") && src.Contains("new SimBroker()"), "🤖 the WHAT IF replay only ever trades its own in-memory SimBroker (never MT5)", "");
        }
        {   // the snapshot is the basket just BEFORE the first manual action, once per iteration: SpasiX (a SELL by hand) and MT5 (a DESKTOP deal)
            var warm = new List<Mt5Bar>(); for (int i = 0; i < 30; i++) warm.Add(new Mt5Bar { Time = new DateTime(2026, 10, 20, 10, i, 0), O = 4000, H = 4000.1, L = 3999.9, C = 4000, V = 100, Spread = 10 });
            var path = new List<Mt5Bar>(); for (int i = 0; i < 60; i++) path.Add(new Mt5Bar { Time = new DateTime(2026, 10, 21).AddMinutes(i), O = 4000, H = 4000.1, L = 3999.9, C = 4000, V = 100, Spread = 10 });
            var sb = new SimBroker(); sb.Load(new SimSettings { Symbol = "XAUUSD", From = new DateTime(2026, 10, 21), To = new DateTime(2026, 10, 21), Balance = 10000, CommissionPerLot = 0, Leverage = 500, Kind = "STANDARD", FixedSpread = 10 }, Gold(), warm.Concat(path).ToList());
            var au = new HedgeAuto(); au.Send = (c0, a, l) => sb.Command(c0, a); sb.OnTickHook = () => au.Step(sb); int fired = 0; List<WhatIfLeg> legs = null; string what = "";
            au.OnManualAction += (it, lg, bk, at, w) => { fired++; legs = lg; what = w; };
            var s = new AutoSettings { Symbol = "XAUUSD", Tf = 1, EntryLots = 0.1, Target = 500, HedgeMode = "NONE", FirstDir = "BUY", OnManual = "STOP", Swing = false };
            au.Check(sb, s); au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60; for (int k = 0; k < 20; k++) sb.Advance(0.25);
            sb.Command("ORDER", "XAUUSD", "SELL", 0.05, 0.0, 0.0, 0.0); for (int k = 0; k < 20; k++) sb.Advance(0.25); sb.Command("ORDER", "XAUUSD", "SELL", 0.05, 0.0, 0.0, 0.0); for (int k = 0; k < 20; k++) sb.Advance(0.25);
            Check(fired == 1 && legs != null && legs.Count == 1 && legs[0].Side == "BUY" && Math.Abs(legs[0].Lots - 0.1) < 1e-9 && what.Contains("SELL 0.05"), "🤖 the snapshot = the basket just BEFORE your first manual action (BUY 0.10, not your SELL 0.05), taken once per iteration", fired + " " + (legs == null ? "null" : string.Join(",", legs.Select(x => x.Side + x.Lots))) + " " + what);
        }
        {   // 09g fix of 09f: an MT5 account's deals history can have gaps → "no deals of its own" never drops a result there (and 09f's flag is undone)
            var day = new DateTime(2026, 11, 9); var deals = new List<Mt5Deal> { new Mt5Deal { Ticket = 1, Position = 9, Time = day.AddDays(-2), Symbol = "XAUUSD", Type = "BUY", Entry = "IN", Lots = 0.1 } };
            var its = new List<AutoIteration> { new AutoIteration { N = 7, Start = day.AddHours(10), End = day.AddHours(10).AddMinutes(20), Buys = 1, Result = 16.2, Reason = "TARGET · result unverified — balance jump (no deals of its own)" },
                new AutoIteration { N = 8, Start = day.AddHours(11), End = day.AddHours(11).AddMinutes(20), Buys = 1, Result = 16.4, Reason = "TARGET" } };
            var ch = Continuity.RepairResults(its, deals, "XAUUSD", 16, false); var demo = new List<AutoIteration> { new AutoIteration { N = 8, Start = day.AddHours(11), End = day.AddHours(11).AddMinutes(20), Buys = 1, Result = 16.4, Reason = "TARGET" } }; Continuity.RepairResults(demo, deals, "XAUUSD", 16, true);
            Check(!its[0].Unverified && its[0].Reason == "TARGET" && !its[1].Unverified && ch.Count == 1 && demo[0].Unverified, "🧮 fix: on an MT5 account (history may have gaps) \"no deals of its own\" is not evidence — 09f's flag removed, the result counted again; on the simulated DEMO it still flags", its[0].Reason + " | " + its[1].Reason + " | demo " + demo[0].Reason);
        }
        Console.WriteLine(fails == 0 ? "ALL 09g TESTS PASSED" : fails + " 09g TEST(S) FAILED");
        return fails;
    }
}
