// the ⇅ HEDGE LAB engine against hand-made 1-minute candles: the entry on a break, the basket target (commission and spread included) and the
// re-entry the same way, the hedge legs (0.1 → 0.2 the other way → 0.2 back), MT5 refusing a hedge the margin cannot carry, the day target
using System; using System.Collections.Generic; using System.Linq; using KeystoneMT5;
public static class HedgeLabTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static readonly DateTime T0 = new DateTime(2026, 10, 20, 2, 0, 0);
    static Mt5Bar B(int i, double o, double h, double l, double c) { return new Mt5Bar { Time = T0.AddMinutes(i), O = o, H = h, L = l, C = c, Spread = 0 }; }
    // 3 wide candles (99 … 101), then a steady climb of 0.5 a candle
    static List<Mt5Bar> Climb(int n)
    {
        var res = new List<Mt5Bar> { B(0, 100, 101, 99, 100), B(1, 100, 101, 99, 100), B(2, 100, 101, 99, 100) };
        for (int k = 3; k < n; k++) { double o = 100 + 0.5 * (k - 3); res.Add(B(k, o, o + 0.6, o - 0.1, o + 0.5)); }
        return res;
    }
    static HedgeSettings Set() { return new HedgeSettings { Balance = 350, BaseLot = 0.1, HedgeLot = 0.2, Target = 20, Lookback = 3, Contract = 100, Point = 0.01, CommissionPerLot = 0, FixedSpreadPts = 0, SlipPts = 0, Leverage = 100, HedgedRatio = 0.5, StopOutPct = 50, Trace = true }; }

    public static int Main3()
    {
        // 1. the break of the recent high buys 0.1 at 101; +$20 = 103.00 closes the basket; the same way again at once
        var r = HedgeLab.Run(Climb(12), Set(), T0, T0);
        Check(r.Log.Count > 0 && r.Log[0].Contains("BUY  0.10 @ 101.00"), "entry: the break of the last 3 candles' high buys 0.10 at 101.00", r.Log.FirstOrDefault());
        Check(r.Cycles.Count >= 1 && Math.Abs(r.Cycles[0].Profit - 20) < 1e-6 && r.Cycles[0].Reason == "TARGET", "target: the basket closes at exactly +$20", r.Cycles.Count > 0 ? r.Cycles[0].Profit + " " + r.Cycles[0].Reason : "none");
        Check(r.Log.Any(l => l.Contains("BUY  0.10 @ 103.00")), "re-entry: the same direction again at the target's price (103.00)", string.Join(" | ", r.Log));

        // 2. spread 0.20 and $7 a lot: the buy fills at the ask, the target still nets exactly +$20 after both commissions
        var s2 = Set(); s2.FixedSpreadPts = 20; s2.CommissionPerLot = 7; s2.DailyTarget = 20;
        r = HedgeLab.Run(Climb(14), s2, T0, T0);
        Check(r.Log[0].Contains("@ 101.20"), "costs: a buy fills at the ask (101.00 + 0.20 spread)", r.Log[0]);
        Check(r.Cycles.Count == 1 && Math.Abs(r.EndBalance - 370) < 1e-6 && Math.Abs(r.Commission - 0.7) < 1e-9, "costs: balance 350 → 370.00 after $0.70 commission (the target is net)", r.EndBalance + " / " + r.Commission);
        Check(r.Days.Count == 1 && r.Days[0].HitTarget && r.Cycles.Count == 1, "day target: after +$20 banked no new cycle that day", r.Cycles.Count + "");

        // 3. whipsaw: buy 0.1 @ 101 → the break of 99 sells 0.2 → the break of the recent high 101.10 buys 0.2 (0.30 bought / 0.20 sold)
        var w = Climb(5).Take(5).ToList();   // candle 4 breaks 101 (the buy)
        w.Add(B(5, 101, 101.1, 98.8, 99)); w.Add(B(6, 99, 101.5, 98.9, 101.4));
        r = HedgeLab.Run(w, Set(), T0, T0);
        var opens = r.Log.Where(l => l.Contains(" @ ")).ToList();
        Check(opens.Count == 3 && opens[1].Contains("SELL 0.20 @ 99.00") && opens[2].Contains("BUY  0.20 @ 101.10"), "hedges: 0.10 buy → 0.20 sell at the low → 0.20 buy at the recent high", string.Join(" | ", opens));
        Check(r.MaxLegs == 3 && Math.Abs(r.MaxGross - 0.5) < 1e-9 && r.Cycles.Count == 0, "hedges: 3 legs, 0.50 lots open, no cycle closed", r.MaxLegs + " / " + r.MaxGross);

        // 4. $25 account: at 99 the equity is $5 — MT5 refuses the 0.2 hedge (margin $20), then the stop-out ends the run
        var s4 = Set(); s4.Balance = 25; var w4 = Climb(5).Take(5).ToList(); w4.Add(B(5, 101, 101.1, 98.0, 98.2));
        r = HedgeLab.Run(w4, s4, T0, T0);
        Check(r.Rejected == 1 && r.Blown, "margin: the hedge is refused (no money) and the account is stopped out", r.Rejected + " / " + r.Blown);

        // 5. a gap: the candle opens past the hedge level — the hedge fills at the open, not at the level
        var w5 = Climb(5).Take(5).ToList(); w5.Add(B(5, 97.5, 97.6, 97.0, 97.2));
        r = HedgeLab.Run(w5, Set(), T0, T0);
        Check(r.Log.Any(l => l.Contains("SELL 0.20 @ 97.50")), "gap: the hedge fills at the open 97.50", string.Join(" | ", r.Log));
        return fails;
    }
}
