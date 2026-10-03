// FLIP: START → GOAL on a tested trade stream, risk % or fixed sizing, replay + odds.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class FlipTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static List<KeystoneLabTrade> Stream(params double[] nets) { var t0 = new DateTime(2024, 1, 2, 10, 0, 0); return nets.Select((n, i) => new KeystoneLabTrade { EntryTime = t0.AddDays(i), Day = t0.AddDays(i).Date, Net = n, Worst = Math.Min(0, n), Qty = 1 }).ToList(); }
    public static int Main()
    {
        // +100 every trade, 1 micro fixed: $500 → $1,500 in 10 trades, then again
        var up = KeystoneFlip.Units(Stream(Enumerable.Repeat(100.0, 25).ToArray()));
        var f = KeystoneFlip.Run(up, 500, 1500, 100, 0, 1);
        Check(f.Flips == 2 && f.Busts == 0 && f.Withdrawn == 2000 && f.MedianDaysPerFlip == 9, "fixed 1 micro, +$100 a trade: 2 flips of $1,000 in 25 trades, 9 days each", f.Flips + " " + f.Withdrawn + " " + f.MedianDaysPerFlip);
        Check(f.ReachPct > 0 && f.BustPct == 0, "odds: reaches the goal from early starts, never busts", f.ReachPct + " / " + f.BustPct);
        // −$450 then up: busts the $500 account (bust level $100) and re-deposits
        var dn = KeystoneFlip.Units(Stream(new[] { -450.0 }.Concat(Enumerable.Repeat(100.0, 10)).ToArray()));
        var b = KeystoneFlip.Run(dn, 500, 1500, 100, 0, 1);
        Check(b.Busts == 1 && b.Flips == 1 && b.Deposited == 1000 && b.Net == 0, "a −$450 trade busts the account (below $100): re-deposit $500, then a flip", b.Busts + " " + b.Flips + " " + b.Deposited + " " + b.Net);
        // intratrade: a winning trade whose worst point is beyond the account kills it
        var deep = new List<KeystoneLabTrade> { new KeystoneLabTrade { EntryTime = new DateTime(2024, 1, 2), Net = 50, Worst = -450, Qty = 1 } };
        var d = KeystoneFlip.Run(KeystoneFlip.Units(deep), 500, 1500, 100, 0, 1);
        Check(d.Busts == 1 && d.Flips == 0, "the worst open loss counts: a trade that dips −$450 before winning still busts a $500 account");
        // risk %: contracts grow with the balance
        var g = KeystoneFlip.Run(up, 500, 1500, 100, 20, 0);
        Check(g.MaxContracts > 1 && g.Flips >= 2, "20% risk: contracts grow as the balance grows (compounding)", g.MaxContracts + " max, " + g.Flips + " flips");
        var grid = KeystoneFlip.Grid(up, 500, 1500, 100, new[] { 5.0, 10, 20 }, new[] { 1, 2 });
        Check(grid.Count == 5 && grid[0].Net >= grid[grid.Count - 1].Net, "grid of sizings, best first");
        Console.WriteLine(failures == 0 ? "ALL FLIP TESTS PASSED" : failures + " FLIP TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
