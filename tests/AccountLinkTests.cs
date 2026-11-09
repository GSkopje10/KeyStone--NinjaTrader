// ACCOUNT LINK: the studio's live chart = the real account (a fake broker). Trades placed in "NinjaTrader" appear in the studio;
// studio actions become real orders only while armed; fills become trades; limits; plus the prop presets and the account watch.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NinjaTrader.NinjaScript;

public sealed class FakeBroker : IKeystoneBroker
{
    public bool Conn = true; public int Q; public double Avg, Pnl; int seq, eseq;
    public List<string> Sent = new List<string>();
    public readonly List<KeystoneBrokerOrder> All = new List<KeystoneBrokerOrder>();
    public readonly List<KeystoneExec> Execs = new List<KeystoneExec>();
    public DateTime Now = new DateTime(2026, 10, 6, 9, 45, 0);
    public bool Connected { get { return Conn; } }
    public int Qty { get { return Q; } }
    public double AvgPrice { get { return Avg; } }
    public double DayPnl { get { return Pnl; } }
    public double Tick { get { return 0.25; } }
    public List<KeystoneBrokerOrder> Working() { return All.Where(o => !o.Done).ToList(); }
    public List<KeystoneExec> Executions() { return Execs.ToList(); }
    public KeystoneBrokerOrder Submit(string kind, int signedQty, double price, string oco, bool automated)
    {
        var o = new KeystoneBrokerOrder { Id = "O" + (++seq), Kind = kind, Side = Math.Sign(signedQty), Qty = Math.Abs(signedQty), Price = price, Oco = oco ?? "", Name = "KEYSTONE" };
        All.Add(o); Sent.Add(kind + " " + signedQty + (kind == "MARKET" ? "" : " @ " + price) + (o.Oco != "" ? " oco" : "") + (automated ? " auto" : ""));
        return o;
    }
    public void ChangePrice(KeystoneBrokerOrder o, double price) { o.Price = price; Sent.Add("CHANGE " + o.Kind + " @ " + price); }
    public void Cancel(KeystoneBrokerOrder o) { Sent.Add("CANCEL " + o.Kind); o.CancelSent = true; o.Done = true; Oco(o); }
    public void Flatten() { Sent.Add("FLATTEN"); foreach (var o in All) o.Done = true; if (Q != 0) Exec(-Q, Last, "FLATTEN"); }
    public double Last = 20000;
    void Oco(KeystoneBrokerOrder o) { if (o.Oco != "") foreach (var x in All.Where(x => x.Oco == o.Oco && !x.Done)) x.Done = true; }
    public void Exec(int signedQty, double px, string name)
    {
        int nq = Q + signedQty;
        if (Q != 0 && Math.Sign(signedQty) == Math.Sign(Q)) Avg = (Avg * Math.Abs(Q) + px * Math.Abs(signedQty)) / Math.Abs(nq);
        else if (nq != 0 && (Q == 0 || Math.Sign(nq) != Math.Sign(Q))) Avg = px;
        Q = nq; if (Q == 0) Avg = 0; Now = Now.AddSeconds(1);
        Execs.Add(new KeystoneExec { Id = "E" + (++eseq), Name = name, Time = Now, SignedQty = signedQty, Price = px });
    }
    public void FillMarket(double px) { foreach (var o in All.Where(x => x.Kind == "MARKET" && !x.Done).ToList()) { o.Filled = o.Qty; o.Done = true; Exec(o.Side * o.Qty, px, o.Name); } }
    public void Trigger(KeystoneBrokerOrder o) { o.Filled = o.Qty; o.Done = true; Oco(o); Exec(o.Side * o.Qty, o.Price, o.Name); }
    // something done in NinjaTrader itself (Chart Trader / DOM)
    public KeystoneBrokerOrder External(string kind, int signedQty, double price) { var o = new KeystoneBrokerOrder { Id = "N" + (++seq), Kind = kind, Side = Math.Sign(signedQty), Qty = Math.Abs(signedQty), Price = price, Name = "Stop1" }; All.Add(o); return o; }
}

// 🔒 stand-ins for NinjaTrader accounts with a lockout member (the real name is read by reflection)
public sealed class StubNtAccount { public string Name { get; set; } public DateTime LockedUntil { get; set; } }
public sealed class StubNtRisk { public bool IsLocked { get; set; } }
public sealed class StubNtRiskAccount { public string Name = "R"; public StubNtRisk Risk { get; set; } public StubNtRiskAccount() { Risk = new StubNtRisk(); } }
public sealed class StubNtNullable { public DateTime? LockoutTime { get; set; } }
public sealed class StubNtSettings { public bool AllowUnlock { get; set; } public bool LockFlatten { get; set; } }

public static class AccountLinkTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static KeystoneReplayTrader Trader() { var bars = new List<KeystoneArcBar> { new KeystoneArcBar { Symbol = "MNQ", Time = new DateTime(2026, 10, 6, 9, 45, 0), Open = 20000, High = 20001, Low = 19999, Close = 20000 } }; var t = KeystoneReplayTrader.LiveStart(bars, "MNQ", new DateTime(2026, 10, 6, 9, 45, 0)); t.Slave = true; return t; }

    public static int Main()
    {
        var t0 = new DateTime(2026, 10, 6, 9, 45, 0);
        // 0b. contracts ADDED to a position with a profit stop: the stop / target keep their PRICE and are resized for EVERY contract (user's bug:
        //     the stop closed only the first contract and left the rest open)
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 6 }; var t = Trader();
            l.Arm(t0); t.StopPts = 10; t.TargetPts = 40; t.Buy(1); l.Tick(t, t0); l.Tick(t, t0.AddSeconds(0.25)); b.FillMarket(20000); for (int k = 0; k < 6; k++) l.Tick(t, t0.AddSeconds(1 + k));
            var st0 = b.All.FirstOrDefault(o => !o.Done && o.Kind == "STOP"); var tg0 = b.All.FirstOrDefault(o => !o.Done && o.Kind == "LIMIT");
            Check(st0 != null && st0.Qty == 1 && Math.Abs(st0.Price - 19990) < 1e-9 && tg0 != null && Math.Abs(tg0.Price - 20040) < 1e-9, "add: LONG 1 @ 20000 with a stop 19990 + target 20040 for 1", st0 == null ? "no stop" : st0.Qty + " @ " + st0.Price);
            t.StopPts = -4; for (int k = 0; k < 6; k++) l.Tick(t, t0.AddSeconds(8 + k));   // the stop dragged INTO profit: 20004
            var st1 = b.All.FirstOrDefault(o => !o.Done && o.Kind == "STOP");
            Check(st1 != null && Math.Abs(st1.Price - 20004) < 1e-9, "add: the stop dragged into profit → 20004", st1 == null ? "none" : st1.Price.ToString());
            t.Buy(2); l.Tick(t, t0.AddSeconds(20)); l.Tick(t, t0.AddSeconds(20.25)); b.FillMarket(20010); for (int k = 0; k < 12; k++) l.Tick(t, t0.AddSeconds(21 + k));
            var stops = b.All.Where(o => !o.Done && o.Kind == "STOP").ToList(); var tgts = b.All.Where(o => !o.Done && o.Kind == "LIMIT").ToList();
            Check(b.Q == 3 && stops.Count == 1 && stops[0].Qty == 3 && Math.Abs(stops[0].Price - 20004) < 1e-9 && tgts.Count == 1 && tgts[0].Qty == 3 && Math.Abs(tgts[0].Price - 20040) < 1e-9,
                "add 2 more (LONG 3): ONE stop for 3 at the SAME 20004, ONE target for 3 at the SAME 20040 (nothing left sized for 1)", "pos " + b.Q + " • " + string.Join(", ", stops.Select(o => "STOP " + o.Qty + "@" + o.Price)) + " • " + string.Join(", ", tgts.Select(o => "TGT " + o.Qty + "@" + o.Price)));
            double avg3 = (20000 + 2 * 20010) / 3.0;
            Check(Math.Abs(t.AvgPrice - avg3) < 0.01 && Math.Abs((t.AvgPrice - t.StopPts) - 20004) < 0.3 && Math.Abs((t.AvgPrice + t.TargetPts) - 20040) < 0.3, "the studio's lines stay at 20004 / 20040 (their distances follow the new average)", t.AvgPrice + " stop " + (t.AvgPrice - t.StopPts) + " tgt " + (t.AvgPrice + t.TargetPts));
        }
        // 0c. contracts added in NINJATRADER (not from the chart): the same — one stop / target for every contract, at the same prices
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 6 }; var t = Trader();
            l.Arm(t0); t.StopPts = 10; t.TargetPts = 40; t.Buy(1); l.Tick(t, t0); l.Tick(t, t0.AddSeconds(0.25)); b.FillMarket(20000); for (int k = 0; k < 6; k++) l.Tick(t, t0.AddSeconds(1 + k));
            b.Exec(2, 20020, "NT BUY 2"); for (int k = 0; k < 12; k++) l.Tick(t, t0.AddSeconds(10 + k));
            var stops = b.All.Where(o => !o.Done && o.Kind == "STOP").ToList(); var tgts = b.All.Where(o => !o.Done && o.Kind == "LIMIT").ToList();
            Check(b.Q == 3 && stops.Count == 1 && stops[0].Qty == 3 && Math.Abs(stops[0].Price - 19990) < 1e-9 && tgts.Count == 1 && tgts[0].Qty == 3 && Math.Abs(tgts[0].Price - 20040) < 1e-9 && t.Position == 3,
                "added in NinjaTrader (LONG 3): ONE stop for 3 at the SAME 19990, ONE target for 3 at the SAME 20040; the chart shows LONG 3", "pos " + b.Q + "/" + t.Position + " • " + string.Join(", ", stops.Select(o => "STOP " + o.Qty + "@" + o.Price)) + " • " + string.Join(", ", tgts.Select(o => "TGT " + o.Qty + "@" + o.Price)));
        }
        // 0d. the user's case: LONG 2 with the stop dragged INTO profit, then SELL 5 = reverse to SHORT 3 — the old profit stop must not become a
        //     BUY STOP below the market (rejected → disarm + flatten = "it closed all of the contracts")
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 6 }; var t = Trader();
            l.Arm(t0); t.StopPts = 10; t.TargetPts = 40; t.Buy(2); l.Tick(t, t0); l.Tick(t, t0.AddSeconds(0.25)); b.FillMarket(20000); for (int k = 0; k < 6; k++) l.Tick(t, t0.AddSeconds(1 + k));
            t.Price = 20012; b.Last = 20012; t.StopPts = -6; for (int k = 0; k < 6; k++) l.Tick(t, t0.AddSeconds(8 + k));   // profit stop @ 20006
            t.Sell(5); for (int k = 0; k < 4; k++) { l.Tick(t, t0.AddSeconds(15 + k * 0.25)); b.FillMarket(20012); } for (int k = 0; k < 10; k++) l.Tick(t, t0.AddSeconds(17 + k));
            var badStop = b.All.FirstOrDefault(o => !o.Done && o.Kind == "STOP" && o.Side > 0 && o.Price <= 20012);
            Check(b.Q == -3 && t.Position == -3 && l.Armed && badStop == null && !b.Sent.Contains("FLATTEN") && t.StopPts >= 0,
                "reverse LONG 2 (profit stop) → SELL 5 = SHORT 3, still ARMED, no buy stop below the market, nothing flattened", "pos " + b.Q + "/" + t.Position + " armed " + l.Armed + " stop " + t.StopPts + " • " + string.Join(" | ", b.Sent.Skip(Math.Max(0, b.Sent.Count - 6))));
        }
        // 0. TAKE PROFIT step: a resting studio limit for part of the position; when it fills, the bracket is rebuilt for what is left
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 4 }; var t = Trader();
            l.Arm(t0); t.StopPts = 10; t.TargetPts = 40; t.Buy(2); l.Tick(t, t0); l.Tick(t, t0.AddSeconds(0.25)); b.FillMarket(20000); l.Tick(t, t0.AddSeconds(1));
            b.Sent.Clear(); t.Place("LIMIT", -1, 1, 20010); l.Tick(t, t0.AddSeconds(2));
            Check(b.Sent.SequenceEqual(new[] { "LIMIT -1 @ 20010" }), "TP step: a resting SELL LIMIT 1 @ 20010 next to the bracket", string.Join(" | ", b.Sent));
            l.Tick(t, t0.AddSeconds(2.5));
            Check(Math.Abs(t.TargetPts - 40) < 1e-9 && t.Orders.Count == 1 && t.Position == 2, "the bracket target stays the target, the TP step stays a working order", t.TargetPts + " / " + t.Orders.Count);
            b.Sent.Clear(); b.Trigger(b.All.First(o => o.Kind == "LIMIT" && o.Qty == 1 && !o.Done)); l.Tick(t, t0.AddSeconds(3));
            Check(b.Q == 1 && b.Sent.Contains("CANCEL STOP"), "TP step filled (1 left) → the stop sized for 2 is cancelled", string.Join(" | ", b.Sent));
            for (int k = 0; k < 8; k++) l.Tick(t, t0.AddSeconds(5 + k));
            Check(b.All.Any(o => !o.Done && o.Kind == "STOP" && o.Qty == 1) && b.All.Any(o => !o.Done && o.Kind == "LIMIT" && o.Qty == 1) && !b.All.Any(o => !o.Done && o.Qty == 2), "→ a new stop + target for the 1 left, nothing sized for 2", string.Join(" | ", b.Sent));
            Check(t.Position == 1 && t.Orders.Count == 0, "the studio shows LONG 1, the TP step is gone (filled)", t.Position + " / " + t.Orders.Count);
        }
        // 1. watching: a trade placed in NinjaTrader shows in the studio, with its stop; nothing is sent
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ"); var t = Trader();
            b.Exec(1, 20000, "Buy"); l.Tick(t, t0);
            Check(t.Position == 1 && t.AvgPrice == 20000, "NinjaTrader buy 1 @ 20000 → the studio shows LONG 1 @ 20000", t.Position + " @ " + t.AvgPrice);
            Check(l.Warning.Contains("NO STOP"), "no stop on it → NO STOP warning", l.Warning);
            b.External("STOP", -1, 19990); l.Tick(t, t0.AddSeconds(1));
            Check(Math.Abs(t.StopPts - 10) < 1e-9 && l.Warning == "", "NinjaTrader stop @ 19990 → the studio's stop = 10 pts", t.StopPts.ToString());
            b.All.First(o => o.Kind == "STOP").Price = 20002; l.Tick(t, t0.AddSeconds(2));
            Check(Math.Abs(t.StopPts + 2) < 1e-9, "stop moved in NinjaTrader to 20002 → the studio shows a profit stop (−2 pts)", t.StopPts.ToString());
            b.Exec(-1, 20010, "Close"); b.All.ForEach(o => o.Done = true); l.Tick(t, t0.AddSeconds(3));
            Check(t.Position == 0 && t.Fills.Count == 1 && Math.Abs(t.Fills[0].Points - 10) < 1e-9 && t.Fills[0].Reason.Contains("NINJATRADER") && l.NewFills.Count == 1, "closed in NinjaTrader @ 20010 → studio flat + a +10 pt REAL trade in the journal", t.Fills.Count + "");
            Check(b.Sent.Count == 0, "watching: nothing was sent", string.Join(" | ", b.Sent));
            t.Buy(1); l.Tick(t, t0.AddSeconds(4));
            Check(t.Position == 0 && b.Sent.Count == 0 && l.Warning.Contains("NOT ARMED"), "studio BUY while not armed → refused (studio back to the real state)", l.Warning);
        }
        // 2. armed: studio BUY 2 with SL 10 / TP 20 → market, then a real OCO bracket from the real fill
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 3 }; var t = Trader();
            Check(l.Arm(t0) == null && l.Armed, "arm");
            t.StopPts = 10; t.TargetPts = 20; t.Buy(2); l.Tick(t, t0); l.Tick(t, t0.AddSeconds(0.25));
            Check(b.Sent.SequenceEqual(new[] { "MARKET 2" }), "studio BUY 2 → one market buy of 2", string.Join(" | ", b.Sent));
            b.FillMarket(20000.5); b.Sent.Clear(); l.Tick(t, t0.AddSeconds(1));
            Check(b.Sent.SequenceEqual(new[] { "STOP -2 @ 19990.5 oco", "LIMIT -2 @ 20020.5 oco" }), "filled → real stop + target (OCO) from the REAL fill", string.Join(" | ", b.Sent));
            b.Sent.Clear(); l.Tick(t, t0.AddSeconds(2)); l.Tick(t, t0.AddSeconds(3));
            Check(b.Sent.Count == 0 && t.Position == 2 && Math.Abs(t.AvgPrice - 20000.5) < 1e-9 && Math.Abs(t.StopPts - 10) < 1e-9 && Math.Abs(t.TargetPts - 20) < 1e-9, "in place → nothing resent, the studio shows the real position and bracket", string.Join(" | ", b.Sent) + " " + t.StopPts + "/" + t.TargetPts);
            t.StopPts = -1; l.Tick(t, t0.AddSeconds(4));
            Check(b.Sent.SequenceEqual(new[] { "CHANGE STOP @ 20001.5" }), "stop dragged past the entry in the studio → the real stop moves", string.Join(" | ", b.Sent));
            b.Sent.Clear(); b.Trigger(b.All.First(o => o.Kind == "STOP" && !o.Done)); l.Tick(t, t0.AddSeconds(5));
            Check(t.Position == 0 && t.Fills.Count == 1 && t.Fills[0].Net > 0 && t.Fills[0].Reason.Contains("STUDIO") && !b.All.Any(o => !o.Done), "real profit stop filled → studio flat, a winning trade, the target gone (OCO)", t.Fills.Count + " " + (t.Fills.Count > 0 ? t.Fills[0].Net.ToString() : ""));
            Check(b.Sent.Count == 0, "and nothing is re-entered", string.Join(" | ", b.Sent));
        }
        // 3. studio CLOSE → FLATTEN; position capped at MAX QTY
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 2 }; var t = Trader(); l.Arm(t0);
            t.Sell(5); l.Tick(t, t0); l.Tick(t, t0.AddSeconds(0.25)); Check(b.Sent.FirstOrDefault() == "MARKET -2", "studio SELL 5 → capped at MAX QTY 2", string.Join(" | ", b.Sent));
            b.FillMarket(20000); l.Tick(t, t0.AddSeconds(1)); Check(t.Position == -2, "the studio shows the real −2", t.Position.ToString());
            b.Sent.Clear(); t.CloseAll("CLOSE"); l.Tick(t, t0.AddSeconds(2)); l.Tick(t, t0.AddSeconds(2.25));
            Check(b.Sent.SequenceEqual(new[] { "FLATTEN" }) && b.Q == 0, "studio CLOSE → FLATTEN", string.Join(" | ", b.Sent));
            l.Tick(t, t0.AddSeconds(3)); Check(t.Position == 0 && t.Fills.Count == 1, "flat, one trade", t.Fills.Count.ToString());
        }
        // 4. working orders both ways
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ"); var t = Trader(); l.Arm(t0);
            var so = t.Place("LIMIT", 1, 1, 19980); l.Tick(t, t0);
            Check(b.Sent.SequenceEqual(new[] { "LIMIT 1 @ 19980" }), "studio buy limit → real buy limit", string.Join(" | ", b.Sent));
            b.Sent.Clear(); so.Price = 19975; l.Tick(t, t0.AddSeconds(1)); Check(b.Sent.SequenceEqual(new[] { "CHANGE LIMIT @ 19975" }), "dragged in the studio → the real order moves", string.Join(" | ", b.Sent));
            b.Sent.Clear(); t.Orders.Clear(); l.Tick(t, t0.AddSeconds(2)); Check(b.Sent.SequenceEqual(new[] { "CANCEL LIMIT" }), "cancelled in the studio → cancelled for real", string.Join(" | ", b.Sent));
            b.External("LIMIT", -1, 20050); l.Tick(t, t0.AddSeconds(3));
            Check(t.Orders.Count == 1 && t.Orders[0].Dir == -1 && t.Orders[0].Price == 20050, "a sell limit placed in NinjaTrader appears in the studio", t.Orders.Count.ToString());
            b.All.First(o => o.Name == "Stop1").Done = true; l.Tick(t, t0.AddSeconds(4)); Check(t.Orders.Count == 0, "cancelled in NinjaTrader → gone from the studio");
        }
        // 5. AUTO-style stop entry: the studio's SL / TP become the real bracket once it fills
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ"); var t = Trader(); l.Arm(t0);
            t.StopPts = 8; t.TargetPts = 8; t.Place("STOP", 1, 1, 20010); l.Tick(t, t0);
            b.Trigger(b.All.First(o => o.Kind == "STOP")); b.Sent.Clear(); l.Tick(t, t0.AddSeconds(5));
            l.Tick(t, t0.AddSeconds(6));
            Check(b.Sent.SequenceEqual(new[] { "STOP -1 @ 20002 oco", "LIMIT -1 @ 20018 oco" }), "buy stop @ 20010 filled → real stop 20002 + target 20018", string.Join(" | ", b.Sent));
            Check(t.Position == 1 && t.Orders.Count == 0, "studio shows LONG 1, the entry order gone");
        }
        // 6. safety
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { DayLossLimit = 500 }; var t = Trader(); l.Arm(t0);
            b.Exec(1, 20000, "Buy"); b.Pnl = -520; string why = null; l.Disarmed = x => why = x; l.Tick(t, t0);
            Check(!l.Armed && b.Sent.Contains("FLATTEN") && why.StartsWith("DAY LOSS"), "day loss limit → flatten + disarm", why);
            {   // 🔁 switching the chart account and back: the chart still holds the OTHER account's stop / target → the fresh link must NOT send it
                var bS = new FakeBroker(); bS.Exec(-2, 27134, "Sell"); var tpS = bS.Submit("LIMIT", 2, 27100, "", false); var slS = bS.Submit("STOP", 2, 27200, "", false); bS.Sent.Clear();
                var tS = Trader(); tS.StopPts = 5; tS.TargetPts = 8;   // the other account's bracket left on the chart's trader
                var lS = new KeystoneAccountLink(bS, "MNQ") { MaxContracts = 5 }; lS.Arm(t0); lS.Tick(tS, t0); lS.Tick(tS, t0.AddSeconds(1)); lS.Tick(tS, t0.AddSeconds(2));
                Check(bS.Sent.Count == 0 && !tpS.Done && !slS.Done && tS.Position == -2 && Math.Abs(tS.TargetPts - 34) < 0.01 && Math.Abs(tS.StopPts - 66) < 0.01, "switching back to an account: its real TP / SL are READ from NinjaTrader, nothing is sent (the other account's bracket never moves them)", string.Join(",", bS.Sent) + " | pos " + tS.Position + " tp " + tS.TargetPts + " sl " + tS.StopPts);
            }
            {   // 🛑 re-opening the window with the day loss ALREADY past: disarm + no new entries, but the open trade is NOT closed (user's catch)
                var bR = new FakeBroker(); bR.Exec(1, 20000, "Buy"); bR.Pnl = -900; var lR = new KeystoneAccountLink(bR, "MNQ") { DayLossLimit = 500, MaxContracts = 5 }; var tR = Trader(); string whyR = null; lR.Disarmed = x => whyR = x; lR.Arm(t0); lR.Tick(tR, t0);
                Check(!lR.Armed && !bR.Sent.Contains("FLATTEN") && whyR != null && whyR.Contains("NOT closed"), "day loss already past when arming (window re-opened) → disarmed, the position is NOT closed", whyR + " | " + string.Join(",", bR.Sent));
                var bC = new FakeBroker(); bC.Exec(1, 20000, "Buy"); bC.Pnl = 950; var lC = new KeystoneAccountLink(bC, "MNQ") { DayProfitCap = 900, MaxContracts = 5 }; var tC = Trader(); lC.Arm(t0); lC.Tick(tC, t0);
                Check(!lC.Armed && !bC.Sent.Contains("FLATTEN"), "day profit cap already past when arming → disarmed, the position is NOT closed", string.Join(",", bC.Sent));
            }
            var b2 = new FakeBroker(); var l2 = new KeystoneAccountLink(b2, "MNQ") { DayProfitCap = 900 }; var t2 = Trader(); l2.Arm(t0); b2.Exec(1, 20000, "Buy"); b2.Pnl = 910; l2.Tick(t2, t0);
            Check(!l2.Armed && b2.Sent.Contains("FLATTEN"), "day profit cap $900 (consistency) → flatten + done for today");
            var b3 = new FakeBroker(); var l3 = new KeystoneAccountLink(b3, "MNQ") { MaxContracts = 5 }; var t3 = Trader(); l3.Arm(t0);
            for (int i = 0; i < 12 && l3.Armed; i++) { t3.Place("LIMIT", 1, 1, 19900 - i); l3.Tick(t3, t0.AddSeconds(i)); }
            Check(!l3.Armed && l3.State.Contains("RUNAWAY"), "runaway guard: 8 orders in a minute → disarm", l3.State);
            {
                // 5 bracketed trades (entry + stop + target, then closed) in under a minute: only the entries count → still armed
                var bRg = new FakeBroker(); var lRg = new KeystoneAccountLink(bRg, "MNQ") { MaxContracts = 3 }; var tRg = Trader(); lRg.Arm(t0); var tmRg = t0;
                for (int k = 0; k < 5; k++)
                {
                    tRg.StopPts = 10; tRg.TargetPts = 20; tRg.Buy(1); lRg.Tick(tRg, tmRg); lRg.Tick(tRg, tmRg.AddSeconds(0.25)); bRg.FillMarket(20000); lRg.Tick(tRg, tmRg.AddSeconds(1));
                    tRg.CloseAll("X"); lRg.Tick(tRg, tmRg.AddSeconds(2)); lRg.Tick(tRg, tmRg.AddSeconds(6)); tmRg = tmRg.AddSeconds(10);
                }
                Check(lRg.Armed && bRg.Sent.Count(x => x.StartsWith("STOP") || x.StartsWith("LIMIT")) >= 8, "runaway guard: 5 bracketed trades in a minute → still armed (stops / targets counted apart)", lRg.State + " | " + string.Join(" | ", bRg.Sent));
            }
            var b4 = new FakeBroker(); var l4 = new KeystoneAccountLink(b4, "MNQ"); var t4 = Trader(); l4.Arm(t0); t4.Place("LIMIT", 1, 1, 19900); l4.Tick(t4, t0);
            b4.All[0].Rejected = true; b4.All[0].Done = true; l4.Tick(t4, t0.AddSeconds(1)); Check(!l4.Armed, "a rejected order disarms");
            var b5 = new FakeBroker(); b5.Exec(4, 20000, "Buy"); var l5 = new KeystoneAccountLink(b5, "MNQ") { MaxContracts = 2 }; Check(l5.Arm(t0) != null, "arm refused while the account holds more than MAX QTY");
            b.Conn = false; var l6 = new KeystoneAccountLink(b, "MNQ"); Check(l6.Arm(t0) != null, "arm refused while disconnected");
        }
        // 7. minimum hold (11 s): the exit waits, the stop is placed at once, the target only after the hold
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MinHoldSeconds = 11 }; var t = Trader(); l.Arm(t0);
            t.StopPts = 10; t.TargetPts = 20; t.Buy(1); l.Tick(t, t0); l.Tick(t, t0.AddSeconds(0.25)); b.FillMarket(20000); b.Sent.Clear(); l.Tick(t, t0.AddSeconds(1));
            Check(b.Sent.SequenceEqual(new[] { "STOP -1 @ 19990" }), "min hold: only the stop at first", string.Join(" | ", b.Sent));
            b.Sent.Clear(); t.CloseAll("CLOSE"); l.Tick(t, t0.AddSeconds(3)); l.Tick(t, t0.AddSeconds(4)); Check(b.Sent.Count == 0 && l.State.Contains("HOLDING"), "a studio exit after 3 s waits", l.State);
            l.Tick(t, t0.AddSeconds(13)); Check(b.Sent.Contains("FLATTEN"), "…and goes once 11 s passed", string.Join(" | ", b.Sent));
        }
        // 8. AUTO STOP for a position opened in NinjaTrader without a stop
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { AutoStop = true, DefaultStopPts = 12 }; var t = Trader(); l.Arm(t0);
            b.Exec(-1, 20000, "Sell"); l.Tick(t, t0.AddSeconds(5));
            Check(b.Sent.SequenceEqual(new[] { "STOP 1 @ 20012" }), "short from NinjaTrader with no stop → AUTO STOP 12 pts above", string.Join(" | ", b.Sent));
        }
        // 9. trade builder: partial closes, reversals
        {
            var tb = new KeystoneTradeBuilder("MNQ"); var tm = t0;
            var f = tb.Add(new[] { new KeystoneExec { Id = "1", Time = tm, SignedQty = 2, Price = 100 }, new KeystoneExec { Id = "2", Time = tm.AddMinutes(1), SignedQty = -1, Price = 110 }, new KeystoneExec { Id = "3", Time = tm.AddMinutes(2), SignedQty = -3, Price = 120 } });
            Check(f.Count == 2 && f[0].Qty == 1 && f[0].Points == 10 && f[1].Qty == 1 && f[1].Points == 20 && tb.Pos == -2 && tb.Avg == 120, "buy 2, sell 1, sell 3 → two closes (+10, +20 pts), now short 2 @ 120");
            Check(tb.Add(new[] { new KeystoneExec { Id = "3", Time = tm.AddMinutes(2), SignedQty = -3, Price = 120 } }).Count == 0, "an execution is never counted twice");
        }
        // 9b. disconnect: studio actions are refused (no stale order after the reconnect); a day lock refuses + can flatten
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ"); var t = Trader(); l.Arm(t0);
            b.Conn = false; t.Buy(1); l.Tick(t, t0); b.Conn = true; l.Tick(t, t0.AddSeconds(1)); l.Tick(t, t0.AddSeconds(2));
            Check(t.Position == 0 && b.Sent.Count == 0, "BUY while the connection is lost → refused, nothing sent after it is back", string.Join(" | ", b.Sent));
            l.Locked = true; t.Buy(1); l.Tick(t, t0.AddSeconds(3));
            Check(t.Position == 0 && b.Sent.Count == 0 && l.Warning.Contains("LOCKED"), "LOCKED FOR TODAY → studio BUY refused", l.Warning);
            l.LockFlatten = true; b.Exec(1, 20000, "Buy"); l.Tick(t, t0.AddSeconds(10));
            Check(b.Sent.Contains("FLATTEN") && b.Q == 0, "locked + a trade opened in NinjaTrader → closed", string.Join(" | ", b.Sent));
        }
        // 9c. live tools: level fade, market hours, gaps + merge, day locks
        {
            Check(KeystoneLiveTools.LevelOpacity(KeystoneLiveTools.Near, 100, 110, 15) > 0.3 && KeystoneLiveTools.LevelOpacity(KeystoneLiveTools.Near, 100, 120, 15) == 0 && KeystoneLiveTools.LevelOpacity(KeystoneLiveTools.Always, 100, 500, 15) == 1 && KeystoneLiveTools.LevelOpacity(KeystoneLiveTools.Off, 100, 100, 15) == 0, "levels: NEAR fades in within 15 pts, ALWAYS / OFF");
            Check(!KeystoneLiveTools.MarketOpen(new DateTime(2026, 10, 6, 17, 30, 0)) && KeystoneLiveTools.MarketOpen(new DateTime(2026, 10, 6, 18, 0, 0)) && !KeystoneLiveTools.MarketOpen(new DateTime(2026, 10, 10, 12, 0, 0)), "market hours: 17:00–18:00 break, Saturday closed");
            var bars = new List<KeystoneArcBar>(); var b0 = new DateTime(2026, 10, 6, 10, 0, 0);
            for (int i = 1; i <= 30; i++) if (i < 10 || i > 16) bars.Add(new KeystoneArcBar { Time = b0.AddMinutes(i), Close = i });
            var g = KeystoneLiveTools.Gaps(bars, b0, b0.AddMinutes(30), 3);
            Check(g.Count == 1 && g[0].Item1 == b0.AddMinutes(10) && g[0].Item2 == b0.AddMinutes(16), "gap finder: 10:10–10:16 missing", g.Count.ToString());
            var refill = Enumerable.Range(8, 12).Select(i => new KeystoneArcBar { Time = b0.AddMinutes(i), Close = i }).ToList();
            int added = KeystoneLiveTools.MergeBars(bars, refill);
            Check(added == 7 && bars.Count == 30 && bars.Select(x => x.Time).SequenceEqual(bars.Select(x => x.Time).OrderBy(x => x)), "merge: the 7 missing minutes added in order, nothing doubled", added.ToString());
            var dl = new KeystoneDayLocks(); var u = dl.Lock("MFFU1", new DateTime(2026, 10, 6, 11, 0, 0));
            Check(u == new DateTime(2026, 10, 6, 18, 0, 0) && dl.IsLocked("MFFU1", new DateTime(2026, 10, 6, 16, 0, 0)) && !dl.IsLocked("MFFU1", new DateTime(2026, 10, 6, 18, 1, 0)), "day lock until 18:00 NY");
            Check(KeystoneDayLocks.NextOpen(new DateTime(2026, 10, 9, 12, 0, 0)) == new DateTime(2026, 10, 11, 18, 0, 0), "Friday lock → Sunday 18:00");
            Check(KeystoneDayLocks.Parse(dl.Serialize()).IsLocked("MFFU1", new DateTime(2026, 10, 6, 12, 0, 0)), "locks save and load");
            var dh = new KeystoneDayLocks(); dh.Lock("A", new DateTime(2026, 10, 6, 11, 0, 0), true, "you • hard"); dh.Lock("B", new DateTime(2026, 10, 6, 11, 0, 0), false, "you"); dh.LockUntil("C", new DateTime(2026, 10, 6, 17, 0, 0), "NINJATRADER LOCKOUT");
            var dh2 = KeystoneDayLocks.Parse(dh.Serialize());
            Check(!dh2.Unlock("A") && dh2.IsLocked("A", new DateTime(2026, 10, 6, 12, 0, 0)) && dh2.Unlock("B") && !dh2.IsLocked("B", new DateTime(2026, 10, 6, 12, 0, 0)) && !dh2.Unlock("C") && dh2.Why["C"] == "NINJATRADER LOCKOUT" && !dh2.IsLocked("C", new DateTime(2026, 10, 6, 17, 1, 0)), "a HARD lock and a NinjaTrader lockout cannot be unlocked (they end at their time); a normal one can; saved with the reason");
        }
        // 9d. one cancels the other for orders placed in NinjaTrader too; fees from the reported commissions
        {
            var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ"); var t = Trader();
            b.Exec(1, 20000, "Buy"); var st = b.External("STOP", -1, 19990); var tg = b.External("LIMIT", -1, 20020); l.Tick(t, t0);
            b.Trigger(st); l.Tick(t, t0.AddSeconds(1));
            Check(tg.Done && b.Sent.Contains("CANCEL LIMIT"), "NinjaTrader stop + target without OCO: the stop fills → the target is cancelled", string.Join(" | ", b.Sent));
            b.Sent.Clear(); var e1 = b.External("STOP", 1, 20050); var e2 = b.External("STOP", -1, 19950); l.Tick(t, t0.AddSeconds(2));
            b.Trigger(e1); l.Tick(t, t0.AddSeconds(3));
            Check(e2.Done && b.Sent.Contains("CANCEL STOP"), "buy stop + sell stop: one fills → the other is cancelled", string.Join(" | ", b.Sent));
            // already long: a buy limit + a buy stop to add → one fills, the other is cancelled (the stop / target of the trade stay)
            b.Sent.Clear(); var pst = b.External("STOP", -2, 19900); var a1 = b.External("LIMIT", 1, 20030); var a2 = b.External("STOP", 1, 20080); l.Tick(t, t0.AddSeconds(4));
            b.Trigger(a1); l.Tick(t, t0.AddSeconds(5));
            Check(a2.Done && !pst.Done && b.Sent.Count(x => x.StartsWith("CANCEL")) == 1, "in a trade: an add limit fills → the add stop is cancelled, the trade's stop stays", string.Join(" | ", b.Sent));
            var tb = new KeystoneTradeBuilder("MNQ");
            var f = tb.Add(new[] { new KeystoneExec { Id = "a", Time = t0, SignedQty = 2, Price = 100, Commission = 1.24 }, new KeystoneExec { Id = "b", Time = t0.AddMinutes(1), SignedQty = -2, Price = 110, Commission = 1.24 } });
            Check(f.Count == 1 && Math.Abs(f[0].Net - (10 * 2 * 2 - 2.48)) < 1e-9, "fees: the real commissions ($2.48) come off the trade", f.Count > 0 ? f[0].Net.ToString() : "none");
        }
        // 9r. ROTATION: one account per trade, in turn; the entry limit goes to that account only; a fill there brings the bracket;
        // leader flat → that account flat; next setup → the next account; no fill → it keeps its turn; one trade per account a day;
        // a leader fill the account missed → market; MANUAL waits for ENTER / NEXT ACCOUNT; the funded accounts' 11-second hold
        {
            var rb = new[] { new FakeBroker(), new FakeBroker(), new FakeBroker() }; var names = new[] { "A", "B", "C" };
            var rot = new KeystoneRotator(); for (int i = 0; i < 3; i++) { var f = new KeystoneCopyFollower { Account = names[i], MaxQty = 10, Link = new KeystoneAccountLink(rb[i], "MNQ") { MaxContracts = 10 }, Mirror = Trader() }; f.Link.Arm(t0); rot.Pool.Add(f); }
            rot.IsFunded = a => a == "A";
            var leader = Trader(); leader.Slave = false; DateTime tt = t0;
            Func<double, DateTime> at = sec => t0.AddSeconds(sec);
            leader.Place("LIMIT", 1, 10, 19990); rot.Tick(leader, at(1)); rot.Tick(leader, at(1.25));
            Check(rb[0].Sent.SequenceEqual(new[] { "LIMIT 10 @ 19990" }) && rb[1].Sent.Count == 0 && rb[2].Sent.Count == 0 && rot.Current != null && rot.Current.Account == "A", "ROTATION: the setup's limit goes to the account in turn only (A)", string.Join(" | ", rb[0].Sent) + " / " + rb[1].Sent.Count);
            Check(rot.Pool[0].Link.MinHoldSeconds == 11 && rot.Pool[1].Link.MinHoldSeconds == 0, "ROTATION: the 11-second hold is set on the funded account only");
            rb[0].Trigger(rb[0].All.First(o => o.Kind == "LIMIT" && !o.Done)); leader.Orders.Clear(); leader.Adopt(10, 19990, at(2)); leader.StopPts = 75; leader.TargetPts = 75; rb[0].Sent.Clear();
            for (int i = 0; i < 4; i++) rot.Tick(leader, at(3 + i));
            Check(rb[0].Q == 10 && rb[0].Sent.Any(x => x.StartsWith("STOP -10 @ 19915")), "ROTATION: A filled → its stop follows at once (the target waits the funded 11 s)", string.Join(" | ", rb[0].Sent));
            rb[0].Sent.Clear(); for (int i = 0; i < 4; i++) rot.Tick(leader, at(20 + i));
            Check(rb[0].Sent.Any(x => x.StartsWith("LIMIT -10 @ 20065")), "ROTATION: after 11 s the funded account's target is placed", string.Join(" | ", rb[0].Sent));
            leader.Adopt(0, 0, at(30)); rb[0].Sent.Clear(); for (int i = 0; i < 4; i++) rot.Tick(leader, at(30 + i));
            Check(rb[0].Sent.Contains("FLATTEN") && rb[0].Q == 0 && rot.Current == null, "ROTATION: leader flat → A flat, the trade is released", string.Join(" | ", rb[0].Sent) + " • " + rot.Status);
            leader.Place("LIMIT", -1, 10, 20010); rot.Tick(leader, at(40)); rot.Tick(leader, at(40.25));
            Check(rot.Current != null && rot.Current.Account == "B" && rb[1].Sent.SequenceEqual(new[] { "LIMIT -10 @ 20010" }) && !rb[0].Sent.Any(x => x.StartsWith("LIMIT -10 @ 20010")), "ROTATION: the next setup goes to the next account (B)", string.Join(" | ", rb[1].Sent));
            leader.Orders.Clear(); for (int i = 0; i < 3; i++) rot.Tick(leader, at(50 + i));
            Check(rot.Current == null && rb[1].Sent.Contains("CANCEL LIMIT"), "ROTATION: the setup expired → B's order cancelled", string.Join(" | ", rb[1].Sent));
            leader.Place("LIMIT", 1, 10, 19980); rb[1].Sent.Clear(); rot.Tick(leader, at(60)); rot.Tick(leader, at(60.25));
            Check(rot.Current != null && rot.Current.Account == "B", "ROTATION: no fill → B keeps its turn for the next setup", rot.Current == null ? "none" : rot.Current.Account);
            leader.Orders.Clear(); leader.Adopt(10, 19980, at(61)); leader.StopPts = 75; leader.TargetPts = 75; rb[1].Sent.Clear(); rot.Tick(leader, at(62)); rot.Tick(leader, at(62.25));
            Check(rb[1].Sent.Contains("CANCEL LIMIT") && rb[1].Sent.Contains("MARKET 10"), "ROTATION: the leader filled but B's limit did not → B cancels it and buys at market", string.Join(" | ", rb[1].Sent));
            rb[1].FillMarket(19981); for (int i = 0; i < 3; i++) rot.Tick(leader, at(63 + i)); leader.Adopt(0, 0, at(70)); for (int i = 0; i < 4; i++) rot.Tick(leader, at(70 + i));
            leader.Place("LIMIT", 1, 10, 19970); rot.Tick(leader, at(80)); rot.Tick(leader, at(80.25));
            Check(rot.Current != null && rot.Current.Account == "C", "ROTATION: then C", rot.Current == null ? "none" : rot.Current.Account);
            rb[2].Trigger(rb[2].All.First(o => o.Kind == "LIMIT" && !o.Done)); leader.Orders.Clear(); leader.Adopt(10, 19970, at(81)); for (int i = 0; i < 3; i++) rot.Tick(leader, at(82 + i)); leader.Adopt(0, 0, at(90)); for (int i = 0; i < 4; i++) rot.Tick(leader, at(90 + i));
            leader.Place("LIMIT", 1, 10, 19960); rot.Tick(leader, at(100));
            Check(rot.Current == null && rot.Status.StartsWith("SETUP SKIPPED"), "ROTATION: ONE PER DAY — all 3 traded today → the 4th setup is skipped", rot.Status);
            leader.Orders.Clear(); rot.Tick(leader, at(101));
            var d2 = t0.AddDays(1); leader.Place("LIMIT", 1, 10, 19950); rot.Tick(leader, d2); rot.Tick(leader, d2.AddSeconds(0.25));
            Check(rot.Current != null && rot.Current.Account == "A", "ROTATION: the next day A trades again", rot.Current == null ? "none" : rot.Current.Account);
            leader.Orders.Clear(); for (int i = 0; i < 3; i++) rot.Tick(leader, d2.AddSeconds(1 + i));
            // MANUAL
            var mb = new[] { new FakeBroker(), new FakeBroker() }; var mr = new KeystoneRotator { Manual = true };
            for (int i = 0; i < 2; i++) { var f = new KeystoneCopyFollower { Account = "M" + i, MaxQty = 10, Link = new KeystoneAccountLink(mb[i], "MNQ") { MaxContracts = 10 }, Mirror = Trader() }; f.Link.Arm(t0); mr.Pool.Add(f); }
            var ml = Trader(); ml.Slave = false; ml.Place("LIMIT", 1, 10, 19990); mr.Tick(ml, at(1)); mr.Tick(ml, at(1.25));
            Check(mb[0].Sent.Count == 0 && mr.Selected != null && mr.Selected.Account == "M0" && mr.Status.Contains("ENTER"), "MANUAL: a setup → nothing is sent until ENTER (M0 is next)", mr.Status);
            mr.Go = true; mr.Tick(ml, at(2)); mr.Tick(ml, at(2.25));
            Check(mb[0].Sent.SequenceEqual(new[] { "LIMIT 10 @ 19990" }), "MANUAL: ENTER → M0's limit is placed", string.Join(" | ", mb[0].Sent));
            mb[0].Trigger(mb[0].All.First(o => o.Kind == "LIMIT" && !o.Done)); ml.Orders.Clear(); ml.Adopt(10, 19990, at(3)); for (int i = 0; i < 3; i++) mr.Tick(ml, at(4 + i)); ml.Adopt(0, 0, at(10)); for (int i = 0; i < 4; i++) mr.Tick(ml, at(10 + i));
            Check(mr.AwaitNext && mr.Current == null, "MANUAL: the trade is done → NEXT ACCOUNT is asked", mr.Status);
            mr.Advance(t0); Check(!mr.AwaitNext && mr.Selected != null && mr.Selected.Account == "M1", "MANUAL: NEXT ACCOUNT → M1", mr.Selected == null ? "none" : mr.Selected.Account);
        }
        // EXCEL export: a workbook with one sheet per table, a bold header, numbers as numbers, text escaped
        {
            var xml = KeystoneExcel.Build(new[] { Tuple.Create("ACCOUNTS", new List<string[]> { new[] { "account", "cost" }, new[] { "LFE <A&B>", "120" } }) });
            Check(xml.Contains("<Worksheet ss:Name=\"ACCOUNTS\">") && xml.Contains("ss:Type=\"Number\">120<") && xml.Contains("LFE &lt;A&amp;B&gt;") && KeystoneExcel.SplitCsv("a,\"b,c\",3").Length == 3, "EXCEL: sheet, header, number cells, escaped text, quoted CSV cells");
        }
        // 9d2. LIVE RANDOM ROTATION: MNQ + MGC on one account at a time, hard stops at the broker, the combined trailing stop, the next account
        {
            var dr = new KeystoneDuoRotation(5) { QtyM = 2, QtyG = 2, Target = 300, Stop = 200, Trail = true, PauseSeconds = 25, MaxTradesPerDay = 1 };
            var fbM = new List<FakeBroker>(); var fbG = new List<FakeBroker>();
            foreach (var nm in new[] { "LT1001", "TPT2002", "APEX-3003-01" })
            {
                var bm = new FakeBroker(); var bg = new FakeBroker(); fbM.Add(bm); fbG.Add(bg);
                var lm = new KeystoneAccountLink(bm, "MNQ") { MaxContracts = 5, DayLossLimit = 0 }; var lg = new KeystoneAccountLink(bg, "MGC") { MaxContracts = 5, DayLossLimit = 0 };
                lm.Arm(t0); lg.Arm(t0); var f = KeystoneFirmNames.Detect(nm, "");
                dr.Pool.Add(new KeystoneDuoSeat { Account = nm, Firm = f[0], Color = f[2], LinkM = lm, LinkG = lg, MirM = KeystoneReplayTrader.LiveStart(new List<KeystoneArcBar>(), "MNQ", t0), MirG = KeystoneReplayTrader.LiveStart(new List<KeystoneArcBar>(), "MGC", t0) });
            }
            Check(dr.Pool[0].Firm == "LUCID" && dr.Pool[1].Firm == "TPT" && dr.Pool[2].Firm == "APEX" && dr.Pool[0].Color != dr.Pool[1].Color, "FIRMS: LT1001 → Lucid, TPT2002 → Take Profit Trader, APEX-3003-01 → Apex, each its own colour");
            DateTime tt = t0; Action<double, double> tick = (pm, pg) => { tt = tt.AddSeconds(1); fbM.ForEach(b => b.Last = pm); fbG.ForEach(b => b.Last = pg); dr.Tick(pm, pg, tt); };
            Check(dr.Enter(1, 1, tt, 20000, 2000) == "" && dr.Current.Account == "LT1001", "ENTER BUY BOTH → the first account in the pool order (LT1001)");
            tick(20000, 2000); tick(20000, 2000);
            Check(fbM[0].Sent.Contains("MARKET 2") && fbG[0].Sent.Contains("MARKET 2") && fbM[1].Sent.Count == 0, "both legs go to LT1001 only: BUY 2 MNQ + BUY 2 MGC", string.Join(" | ", fbM[0].Sent) + " / " + string.Join(" | ", fbG[0].Sent));
            fbM[0].FillMarket(20000); fbG[0].FillMarket(2000); for (int k = 0; k < 4; k++) tick(20000, 2000);
            Check(fbM[0].Sent.Any(x => x.StartsWith("STOP -2 @ 19950")) && fbG[0].Sent.Any(x => x.StartsWith("STOP -2 @ 1990")), "each leg gets a hard stop AT THE BROKER (the whole $200 stop: MNQ 19950, MGC 1990)", string.Join(" | ", fbM[0].Sent) + " / " + string.Join(" | ", fbG[0].Sent));
            tick(20060, 2002); Check(Math.Abs(dr.Open - 280) < 1e-6 && Math.Abs(dr.Floor - 80) < 1e-6, "combined +$280 (MNQ +240, MGC +40) → the trailing stop follows to +$80", dr.Open + " / " + dr.Floor);
            tick(20030, 1999); Check(dr.Current != null && dr.Floor > 79, "back to +$100: still in (the stop stays at +$80, never moves down)", dr.Open.ToString());
            tick(20020, 1999); tick(20020, 1999); tick(20020, 1999);
            Check(dr.Current == null && dr.Finished.Count == 1 && dr.Finished[0].Reason == "TRAIL STOP" && fbM[0].Q == 0 && fbG[0].Q == 0, "+$60 ≤ +$80 → TRAIL STOP: both legs closed, the trade saved", dr.Status + " / " + fbM[0].Q + " " + fbG[0].Q);
            Check(dr.Pool[0].DoneToday && dr.Pool[0].Status.StartsWith("DONE TODAY"), "LT1001 traded today (1 a day) → done for today", dr.Pool[0].Status);
            dr.Selected = dr.Pool[2]; tt = tt.AddSeconds(30);
            Check(dr.Enter(-1, 1, tt, 20000, 2000) == "" && dr.Current.Account == "APEX-3003-01", "a click on APEX-3003-01 → the next ENTER (SELL MNQ • BUY MGC) goes there, not to the next in turn");
            tick(20000, 2000); tick(20000, 2000); fbM[2].FillMarket(20000); fbG[2].FillMarket(2000); tick(20000, 2000); tick(20000, 2000);
            Check(fbM[2].Q == -2 && fbG[2].Q == 2, "APEX: SELL 2 MNQ + BUY 2 MGC", fbM[2].Q + " " + fbG[2].Q);
            dr.CloseAll("CLOSE ALL", tt); tick(20000, 2000); tick(20000, 2000); tick(20000, 2000);
            Check(dr.Current == null && fbM[2].Q == 0 && fbG[2].Q == 0 && dr.Finished.Last().Reason == "CLOSE ALL", "CLOSE ALL → both legs flat");
            dr.Auto = true; dr.Direction = "BUY"; for (int k = 0; k < 30; k++) tick(20000, 2000);
            Check(dr.Current != null && dr.Current.Account == "TPT2002", "AUTO: after the 25 s pause the next free account (TPT2002) enters BUY BOTH by itself", dr.Status);
            dr.Move(2, 0); Check(dr.Pool[0].Account == "APEX-3003-01" && dr.Pool[1].Account == "LT1001", "drag & drop: the pool order changes (APEX first)");
            Check(KeystoneFirmNames.Detect("Sim101", "")[0] == "SIM" && KeystoneFirmNames.Detect("12345", "Tradeify Rithmic")[0] == "TRADEIFY" && KeystoneFirmNames.Detect("XQ55", "")[0] == "XQ" && KeystoneFirmNames.Detect("abc", "", "MFFU")[0] == "MFFU", "FIRMS: Sim101 • the connection name (Tradeify) • an unknown prefix keeps its own name • your override wins");
        }
        // 9e. copier: leader 2 → follower ×0.5 = 1, stop follows, leader flat → follower flat; own stop hit → sits the trade out; pause
        {
            var lb = new FakeBroker(); var leader = Trader();
            var fb = new FakeBroker(); var f = new KeystoneCopyFollower { Account = "F1", Multiplier = 0.5, MaxQty = 3, Link = new KeystoneAccountLink(fb, "MNQ"), Mirror = Trader() }; f.Link.Arm(t0);
            leader.Adopt(2, 20000, t0); leader.StopPts = 10; leader.TargetPts = 20;
            KeystoneCopier.Tick(leader, f, t0); KeystoneCopier.Tick(leader, f, t0.AddSeconds(0.25));
            Check(fb.Sent.SequenceEqual(new[] { "MARKET 1" }), "leader long 2 → follower ×0.5 buys 1", string.Join(" | ", fb.Sent));
            fb.FillMarket(20001); fb.Sent.Clear(); KeystoneCopier.Tick(leader, f, t0.AddSeconds(1));
            Check(fb.Sent.SequenceEqual(new[] { "STOP -1 @ 19991 oco", "LIMIT -1 @ 20021 oco" }), "follower gets the leader's stop / target distances from its own fill", string.Join(" | ", fb.Sent));
            fb.Sent.Clear(); leader.StopPts = -2; KeystoneCopier.Tick(leader, f, t0.AddSeconds(2));
            Check(fb.Sent.SequenceEqual(new[] { "CHANGE STOP @ 20003" }), "leader moves its stop into profit → follower's stop moves", string.Join(" | ", fb.Sent));
            fb.Sent.Clear(); leader.Adopt(0, 0, t0); KeystoneCopier.Tick(leader, f, t0.AddSeconds(3)); KeystoneCopier.Tick(leader, f, t0.AddSeconds(3.25));
            Check(fb.Sent.Contains("FLATTEN") && fb.Q == 0, "leader flat → follower flat", string.Join(" | ", fb.Sent));
            leader.Time = t0.AddMinutes(10); leader.Adopt(-1, 20050, t0); leader.StopPts = 10; leader.TargetPts = 0; fb.Sent.Clear();
            KeystoneCopier.Tick(leader, f, t0.AddMinutes(10)); KeystoneCopier.Tick(leader, f, t0.AddMinutes(10).AddSeconds(0.25)); fb.FillMarket(20050); KeystoneCopier.Tick(leader, f, t0.AddMinutes(10).AddSeconds(1));
            fb.Trigger(fb.All.First(o => o.Kind == "STOP" && !o.Done)); fb.Sent.Clear();
            for (int i = 2; i < 6; i++) KeystoneCopier.Tick(leader, f, t0.AddMinutes(10).AddSeconds(i));
            Check(fb.Q == 0 && !fb.Sent.Any(x => x.StartsWith("MARKET")) && f.Status.StartsWith("SAT OUT"), "follower's own stop hit while the leader is still in → sits the trade out", f.Status + " " + string.Join(" | ", fb.Sent));
            leader.Adopt(0, 0, t0); KeystoneCopier.Tick(leader, f, t0.AddMinutes(11)); f.Paused = true; leader.Time = t0.AddMinutes(20); leader.Adopt(1, 20100, t0); fb.Sent.Clear();
            KeystoneCopier.Tick(leader, f, t0.AddMinutes(20)); KeystoneCopier.Tick(leader, f, t0.AddMinutes(20).AddSeconds(1));
            Check(fb.Sent.Count == 0 && f.Status == "PAUSED", "paused follower copies nothing", f.Status);
            Check(KeystoneCopier.Desired(3, 2, 4) == 4 && KeystoneCopier.Desired(-1, 0.5, 3) == -1, "multiplier × leader size, capped at max, at least 1");
            var parsed = KeystoneCopier.Parse(KeystoneCopier.Serialize("LEAD", new[] { f })); Check(parsed.Item1 == "LEAD" && parsed.Item2.Count == 1 && parsed.Item2[0].Multiplier == 0.5 && parsed.Item2[0].Paused, "copier groups save and load");
        }
        // 9f. the pool: records saved / loaded, stats from daily closes + journal
        {
            var a = new KeystonePoolAccount { Account = "MFFU1", Nick = "MFF #1", Firm = "MYFUNDEDFUTURES", Status = "FUNDED", Started = new DateTime(2026, 10, 1), Cost = 112 };
            a.Payouts.Add(Tuple.Create(new DateTime(2026, 10, 20), 500.0)); a.Payouts.Add(Tuple.Create(new DateTime(2026, 10, 21), 650.0));
            var back = KeystonePool.Parse(KeystonePool.Serialize(new[] { a, new KeystonePoolAccount { Account = "TPT9", Status = "BLOWN" } }));
            Check(back.Count == 2 && back[0].Nick == "MFF #1" && back[0].PaidOut == 1150 && back[0].Cost == 112 && back[1].Archived, "pool saves and loads (payouts, cost, blown = archive)");
            var w = new KeystoneAccountWatch { Account = "MFFU1", StartBalance = 50000 }; w.Observe(new DateTime(2026, 10, 1), 50400); w.Observe(new DateTime(2026, 10, 2), 50300); w.Observe(new DateTime(2026, 10, 3), 50900);
            var st = KeystonePool.Stats(w, new[] { new[] { "MFFU1", "MNQ", "BUY", "1", "", "", "", "", "10", "19.5" }, new[] { "MFFU1", "MNQ", "SELL", "1", "", "", "", "", "-5", "-11" } });
            Check(st.Days == 3 && Math.Abs(st.Pnl - 900) < 1e-9 && st.Trades == 2 && st.Wins == 1, "pool stats: 3 days, +$900, 2 trades / 1 win", st.Days + " " + st.Pnl + " " + st.Trades);
            // POOL numbers: highs / lows / deepest drop seen live, funded day + peak (saved), the funded intraday trailing floor (locks at the start)
            var fa = new KeystonePoolAccount { Account = "TPT5", Status = "EVAL", Started = new DateTime(2026, 10, 20) };
            foreach (var eq in new[] { 50000.0, 50600, 50200, 50900, 50100 }) fa.Observe(eq);
            Check(fa.PeakBal == 50900 && fa.LowBal == 50000 && Math.Abs(fa.MaxDd - 800) < 1e-9 && fa.FundedPeak == 0, "pool: highest 50,900, lowest 50,000, deepest drop −800, no funded peak in the evaluation");
            fa.Status = "FUNDED"; fa.MarkFunded(new DateTime(2026, 10, 24), 51000); fa.Observe(51700); fa.Observe(51200);
            var fb2 = KeystonePool.Parse(KeystonePool.Serialize(new[] { fa }))[0];
            Check(fb2.Funded && fb2.FundedOn == new DateTime(2026, 10, 24) && fb2.FundedPeak == 51700 && fb2.PeakBal == 51700 && Math.Abs(fb2.MaxDd - 800) < 1e-9, "funded on Oct 24, its peak 51,700 — saved and loaded");
            Check(Math.Abs(KeystonePoolAccount.IntradayFloor(51700, 2000, 50000) - 49700) < 1e-9 && Math.Abs(KeystonePoolAccount.IntradayFloor(52600, 2000, 50000) - 50000) < 1e-9, "intraday trailing floor: best 51,700 − 2,000 = 49,700; locks at the 50,000 start");
            {   // FUNDED (TPT PRO): the floor trails the best equity by $2,000 until +$2,000 locks it at the start; EOD mode uses the day closes
                var fpa = new KeystonePoolAccount { Account = "TAKEPROFITPRO583654006", Status = "FUNDED", FundedPeak = 50350 };
                bool det = KeystonePoolAccount.LooksFunded("TAKEPROFITPRO583654006", "TPT") && KeystonePoolAccount.LooksFunded("PRO12345", "TPT") && !KeystonePoolAccount.LooksFunded("TAKEPROFIT583654006", "TPT") && !KeystonePoolAccount.LooksFunded("TAKEPROFITPRO1", "APEX");
                double f1 = fpa.FundedFloor(50281, 2000, 50000, double.NaN), f2 = fpa.FundedFloor(50900, 2000, 50000, double.NaN), f3 = fpa.FundedFloor(52300, 2000, 50000, double.NaN);
                fpa.FundedDd = "EOD"; double f4 = fpa.FundedFloor(52300, 2000, 50000, 48400);
                var fback = KeystonePool.Parse(KeystonePool.Serialize(new[] { fpa, new KeystonePoolAccount { Account = "X", AutoChecked = true } }));
                Check(det && Math.Abs(f1 - 48350) < 1e-9 && Math.Abs(f2 - 48900) < 1e-9 && Math.Abs(f3 - 50000) < 1e-9 && Math.Abs(f4 - 48400) < 1e-9 && fback[0].FundedDd == "EOD" && fback[1].AutoChecked && !fback[0].AutoChecked,
                    "funded: TPT PRO names detected (not TAKEPROFIT evals), trailing 48,350 → 48,900 → locked at 50,000; EOD uses the close floor; saved and read back", det + " " + f1 + " " + f2 + " " + f3 + " " + f4);
            }
            // GROUP DESK: a group traded from the COPIER tab (no chart): BUY → every account buys with the bracket, STOP → all flat
            {
                var dk = new KeystoneGroupDesk { Group = "ASIAN" }; var b1 = new FakeBroker(); var b2 = new FakeBroker(); var d0 = new DateTime(2026, 10, 21, 20, 0, 0);
                foreach (var bb in new[] { b1, b2 }) { var ff = new KeystoneCopyFollower { Account = "X", MaxQty = 5, Link = new KeystoneAccountLink(bb, "MNQ") { MaxContracts = 5 }, Mirror = Trader() }; ff.Link.Arm(d0); dk.Members.Add(Tuple.Create("MNQ", ff)); }
                dk.Enter("MNQ", 1, 2, 20000, 10, 20, d0); dk.Tick("MNQ", 20000, d0); dk.Tick("MNQ", 20000, d0.AddSeconds(0.25));
                Check(b1.Sent.SequenceEqual(new[] { "MARKET 2" }) && b2.Sent.SequenceEqual(new[] { "MARKET 2" }), "group desk BUY 2 → both accounts buy 2", string.Join(" | ", b1.Sent) + " / " + string.Join(" | ", b2.Sent));
                b1.FillMarket(20001); b2.FillMarket(20001); b1.Sent.Clear(); b2.Sent.Clear(); dk.Tick("MNQ", 20001, d0.AddSeconds(1));
                Check(b1.Sent.SequenceEqual(new[] { "STOP -2 @ 19991 oco", "LIMIT -2 @ 20021 oco" }), "each account gets the stop / target from its own fill", string.Join(" | ", b1.Sent));
                b1.Sent.Clear(); dk.Tick("MNQ", 19989, d0.AddSeconds(2)); dk.Tick("MNQ", 19989, d0.AddSeconds(2.25));
                Check(dk.Position("MNQ") == 0 && dk.Last.Contains("STOP") && b1.Sent.Contains("FLATTEN"), "the price through the stop → the group's leader flat → every account flat", dk.Last + " " + string.Join(" | ", b1.Sent));
            }
            // AI CONFLUENCE: a setup counts only when others agree (another strategy the same way ≤ 15 min, VWAP side, the 5-minute move)
            {
                var bars = new List<KeystoneArcBar>(); var b0 = new DateTime(2026, 10, 21, 9, 30, 0);
                for (int i = 0; i < 60; i++) bars.Add(new KeystoneArcBar { Symbol = "MNQ", Time = b0.AddMinutes(i), Open = 20000 + i, High = 20002 + i, Low = 19999 + i, Close = 20001 + i, Volume = 100 });
                Func<int, List<KeystoneReplaySignal>> src = k =>
                    k == 9 ? new List<KeystoneReplaySignal> { new KeystoneReplaySignal { Time = b0.AddMinutes(40), Exit = b0.AddMinutes(59), Dir = 1, Entry = 20041, Stop = 20031, Target = 20051 } } :
                    k == 3 ? new List<KeystoneReplaySignal> { new KeystoneReplaySignal { Time = b0.AddMinutes(45), Exit = b0.AddMinutes(59), Dir = 1, Entry = 20046, Stop = 20040 }, new KeystoneReplaySignal { Time = b0.AddMinutes(20), Exit = b0.AddMinutes(30), Dir = -1, Entry = 20021, Stop = 20025 } } : new List<KeystoneReplaySignal>();
                var ai = KeystoneConfluence.Find(bars, "MNQ", null, 3, src);
                Check(ai.Count == 1 && ai[0].Dir == 1 && ai[0].Tag.Contains("ORB 15") && ai[0].Tag.Contains("FVG CLOSE") && ai[0].Tag.Contains("VWAP"), "AI: ORB 15 + FVG CLOSE the same way + VWAP + the 5-min move → one strong BUY; the lone SELL against the trend is dropped", string.Join(" | ", ai.Select(x => x.Tag)));
                Check(ai.Count == 1 && Math.Abs(ai[0].Target - ai[0].Entry) >= 1.5 * Math.Abs(ai[0].Entry - ai[0].Stop) - 1e-9, "AI: the target is at least 1.5R from the setup's stop");
            }
            // GROUP ROTATION: a seat = a copy group — its other accounts take the same sides; the seat's broker stop → the whole group flat
            {
                var gr = new KeystoneDuoRotation(3) { QtyM = 1, QtyG = 0, Target = 300, Stop = 100, Trail = false, PauseSeconds = 0, MaxTradesPerDay = 1 }; var g0 = new DateTime(2026, 10, 21, 10, 0, 0);
                Func<FakeBroker, KeystoneDuoSeat> seat = bk => { var lk = new KeystoneAccountLink(bk, "MNQ") { MaxContracts = 5 }; lk.Arm(g0); return new KeystoneDuoSeat { Account = "A" + bk.GetHashCode(), LinkM = lk, MirM = KeystoneReplayTrader.LiveStart(new List<KeystoneArcBar>(), "MNQ", g0) }; };
                var bMain = new FakeBroker(); var bMate = new FakeBroker(); var main = seat(bMain); main.Label = "ASIAN"; main.Mates.Add(seat(bMate)); gr.Pool.Add(main);
                DateTime gt = g0; Action<double> gtick = px => { gt = gt.AddSeconds(1); bMain.Last = px; bMate.Last = px; gr.Tick(px, double.NaN, gt); };
                Check(gr.Enter(1, 0, gt, 20000, double.NaN) == "", "GROUP ROTATION: ENTER on the group ASIAN");
                gtick(20000); gtick(20000);
                Check(bMain.Sent.Contains("MARKET 1") && bMate.Sent.Contains("MARKET 1"), "both accounts of the group buy 1 MNQ", string.Join(" | ", bMain.Sent) + " / " + string.Join(" | ", bMate.Sent));
                bMain.FillMarket(20000); bMate.FillMarket(20000); gtick(20000); gtick(20000);
                bMain.Trigger(bMain.All.First(o => o.Kind == "STOP" && !o.Done)); gtick(19940); gtick(19940); gtick(19940);
                Check(gr.Current == null && bMate.Q == 0 && bMate.Sent.Contains("FLATTEN"), "the group's seat stopped at the broker → its other account is closed too", gr.Status + " • mate " + bMate.Q + " • " + string.Join(" | ", bMate.Sent));
            }
            // CONSISTENCY PLAN (TPT 50K: $3,000, best day ≤ 50%, 3 trading days)
            var cp1 = KeystoneConsistency.Plan(new List<double>(), 467, 3000, 50, 3);
            Check(Math.Abs(cp1.DayCap - 1500) < 1e-9 && Math.Abs(cp1.RoomToday - 1033) < 1e-9 && Math.Abs(cp1.Left - 2533) < 1e-9 && cp1.DaysLeft == 2, "day 1 +$467: up to +$1,033 more today, $2,533 left, 2 more days", cp1.Text);
            var cp2 = KeystoneConsistency.Plan(new List<double> { 467 }, 1600, 3000, 50, 3);
            Check(cp2.TodayTooBig && Math.Abs(cp2.NeedTotal - 3200) < 1e-9 && Math.Abs(cp2.Left - 1133) < 1e-9, "a +$1,600 day (over 50%) moves the target to $3,200", cp2.Text);
            var cp3 = KeystoneConsistency.Plan(new List<double> { 1500, 1490 }, 15, 3000, 50, 3);
            Check(cp3.Left == 0 && cp3.DaysLeft == 0 && cp3.Text.StartsWith("TARGET REACHED"), "1,500 + 1,490 + a small 3rd day → target reached, consistency OK", cp3.Text);
            // 🎯 AUTO TAKE PROFIT: points so that the close makes at most the room after costs (rounded down to the tick)
            double atp = KeystoneConsistency.AutoTargetPts(1500, 6, 10, 1.24 + 1.0, 0.1);   // MGC: 6 × $10 a point, cost $2.24 a contract
            Check(Math.Abs(atp - 25.2) < 1e-9 && 6 * atp * 10 - 6 * 2.24 <= 1500 + 1e-9, "auto TP: $1,500 room, 6 MGC → 25.2 pts (≤ $1,500 after costs)", atp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Check(KeystoneConsistency.AutoTargetPts(0, 6, 10, 2, 0.1) == 0 && KeystoneConsistency.AutoTargetPts(0.5, 6, 10, 0, 0.1) == 0, "auto TP: no room / less than a tick → none");
            Check(Math.Abs(KeystoneConsistency.AutoTargetPts(1440, 1, 2, 1.74, 0.25) - 720.75) < 1e-9, "auto TP: MNQ 1 contract, $1,440 room → 720.75 pts");
            // ⇉ a group traded from the COPIER: in a trade, the same side adds (average weighted), the other side reduces / reverses
            var dkA = new KeystoneGroupDesk { Group = "G" }; var tD0 = new DateTime(2026, 10, 21, 10, 0, 0);
            dkA.Enter("MGC", 1, 6, 4145.4, 0, 0, tD0); dkA.Enter("MGC", 1, 2, 4149.4, 0, 0, tD0.AddMinutes(1));
            Check(dkA.Position("MGC") == 8 && Math.Abs(dkA.Leaders["MGC"].AvgPrice - 4146.4) < 1e-9, "group desk: LONG 6 + BUY 2 → LONG 8 @ 4146.40", dkA.Position("MGC") + " @ " + dkA.Leaders["MGC"].AvgPrice);
            dkA.Enter("MGC", -1, 3, 4150, 0, 0, tD0.AddMinutes(2)); Check(dkA.Position("MGC") == 5 && Math.Abs(dkA.Leaders["MGC"].AvgPrice - 4146.4) < 1e-9, "group desk: SELL 3 → LONG 5, same average");
            dkA.Enter("MGC", -1, 7, 4151, 0, 0, tD0.AddMinutes(3)); Check(dkA.Position("MGC") == -2 && dkA.Leaders["MGC"].AvgPrice == 4151, "group desk: SELL 7 → reversed SHORT 2 @ 4151");
            {
                // 🔁 a group switched ON while its accounts are still in a trade: it takes the real position over and sends NOTHING
                var dkT = new KeystoneGroupDesk { Group = "NMP" }; var tT0 = new DateTime(2026, 10, 21, 19, 30, 0);
                Func<string, int, FakeBroker> mk = (acc, q) => { var fb = new FakeBroker(); if (q != 0) fb.Exec(q, 29827.75, "Buy"); var lk = new KeystoneAccountLink(fb, "MNQ") { MaxContracts = 10 }; lk.Arm(tT0); var mr = KeystoneReplayTrader.LiveStart(new List<KeystoneArcBar>(), "MNQ", tT0); mr.Slave = true; dkT.Members.Add(Tuple.Create("MNQ", new KeystoneCopyFollower { Account = acc, MaxQty = 10, Link = lk, Mirror = mr })); return fb; };
                var fA = mk("A", 6); var fB = mk("B", 7); var fC = mk("C", 0); fA.Sent.Clear(); fB.Sent.Clear(); fC.Sent.Clear();
                var adT = dkT.TakeOver("A", new Dictionary<string, double> { { "MNQ", 29800 } }, tT0);
                for (int i = 1; i <= 5; i++) dkT.Tick("MNQ", 29800 + i, tT0.AddSeconds(i));
                Check(adT.Count == 1 && dkT.Position("MNQ") == 6 && fA.Sent.Count == 0 && fB.Sent.Count == 0 && fC.Sent.Count == 0 && fA.Qty == 6 && fB.Qty == 7 && fC.Qty == 0,
                    "group ON while in a trade: takes LONG 6 over, A keeps 6, B keeps 7, flat C sits out — nothing sent", string.Join(" | ", fA.Sent.Concat(fB.Sent).Concat(fC.Sent)));
            }
            // ± one account's contracts on top of the group's size
            Check(KeystoneCopier.WithExtra(6, 2, 30) == 8 && KeystoneCopier.WithExtra(-6, 2, 30) == -8 && KeystoneCopier.WithExtra(6, -6, 30) == 0 && KeystoneCopier.WithExtra(6, -9, 30) == 0 && KeystoneCopier.WithExtra(6, 5, 8) == 8 && KeystoneCopier.WithExtra(0, 3, 30) == 0, "copier ± contracts: +2 / −6 = flat / never past MAX / nothing when the group is flat");
            // 🎯 per-account take profit in the copier: capped at the account's own room for its size; no room = the leader's target
            var fr = new KeystoneCopyFollower { Account = "TPT5", TargetRoom = 300 };
            Check(Math.Abs(KeystoneCopier.TargetFor(40, fr, 1, "MGC", 0.1) - 30.2) < 1e-9 && Math.Abs(fr.TargetCapPts - 30.2) < 1e-9, "copier TP: room $300, 1 MGC → capped at 30.2 pts (leader 40)", fr.TargetCapPts.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Check(KeystoneCopier.TargetFor(20, fr, 1, "MGC", 0.1) == 20 && Math.Abs(KeystoneCopier.TargetFor(0, fr, 1, "MGC", 0.1) - 30.2) < 1e-9, "copier TP: a closer leader target stays; no leader target → the cap");
            { var fAuto = new KeystoneCopyFollower { TargetRoom = 300, AutoOwn = true }; Check(Math.Abs(KeystoneCopier.TargetFor(20, fAuto, 1, "MGC", 0.1) - 30.2) < 1e-9, "copier TP: AUTO → the account's own target (30.2), not the leader's 20"); }
            Check(KeystoneCopier.TargetFor(40, new KeystoneCopyFollower(), 1, "MGC", 0.1) == 40, "copier TP: no room set → the leader's target");
            // COPY GROUPS: one group per account, leader swap, save / load; firm policy: copying apart from automation
            var gs = new List<KeystoneCopyGroup> { new KeystoneCopyGroup { Name = "ASIAN" }, new KeystoneCopyGroup { Name = KeystoneCopyGroups.NewName(new List<KeystoneCopyGroup>()) } };
            Check(gs[1].Name == "GROUP 1", "new group name GROUP 1");
            Check(KeystoneCopyGroups.Place(gs, "ASIAN", "TPT1", true) == null && KeystoneCopyGroups.Place(gs, "ASIAN", "TPT2", false) == null && KeystoneCopyGroups.Place(gs, "ASIAN", "TPT3", false, 3) == null, "ASIAN: leader TPT1 + followers TPT2, TPT3");
            string why = KeystoneCopyGroups.Place(gs, "GROUP 1", "TPT2", false);
            Check(why != null && why.Contains("already in ASIAN") && gs[1].Followers.Count == 0, "TPT2 refused in a second group (with the reason)", why);
            Check(KeystoneCopyGroups.Place(gs, "ASIAN", "TPT2", true) == null && gs[0].Leader == "TPT2" && gs[0].Followers.Select(f => f.Account).SequenceEqual(new[] { "TPT1", "TPT3" }), "TPT2 made the leader → TPT1 becomes a follower", string.Join(",", gs[0].Members));
            gs[0].Followers[1].Paused = true; gs[0].Followers[1].Multiplier = 0.5;
            var gs2 = KeystoneCopyGroups.Parse(KeystoneCopyGroups.Serialize(gs));
            Check(gs2.Count == 2 && gs2[0].Leader == "TPT2" && gs2[0].Followers.Count == 2 && gs2[0].Followers[1].Paused && gs2[0].Followers[1].Multiplier == 0.5 && gs2[0].Followers[1].MaxQty == 3, "copy groups save and load");
            gs[0].Confirmed = true; gs[0].Separate = true; var gs3 = KeystoneCopyGroups.Parse(KeystoneCopyGroups.Serialize(gs)); Check(gs3[0].Confirmed && !gs3[1].Confirmed && gs3[0].Separate && !gs3[1].Separate, "✓ CONFIRMED + ✋ single-account trading save and load");
            KeystoneCopyGroups.Place(gs3, "ASIAN", "TPT1", true); Check(!gs3[0].Confirmed && gs3[0].Leader == "TPT1", "another account on the chart → the group must be confirmed again");
            gs3[0].Confirmed = true; KeystoneCopyGroups.Place(gs3, "ASIAN", "TPT9", false); Check(!gs3[0].Confirmed, "an account added → confirm again");
            gs3[0].Confirmed = true; KeystoneCopyGroups.Remove(gs3, "TPT9"); Check(!gs3[0].Confirmed, "an account taken out → confirm again");
            gs3[0].Confirmed = true; KeystoneCopyGroups.Place(gs3, "ASIAN", "TPT1", true); Check(gs3[0].Confirmed, "the same chart account again → still confirmed");
            KeystoneCopyGroups.Remove(gs2, "TPT2"); Check(gs2[0].Leader == "" && KeystoneCopyGroups.GroupOf(gs2, "TPT2") == null && KeystoneCopyGroups.Place(gs2, "GROUP 1", "TPT2", true) == null, "taken out → free to join another group");
            Check(!KeystoneFirmPolicies.AutoAllowed("TPT", true) && !KeystoneFirmPolicies.AutoAllowed("TPT", false) && KeystoneFirmPolicies.CopyAllowed("TPT"), "TPT (the user's rule): no automation in the evaluation nor funded — the buttons flash, you click; copying allowed");
            KeystoneFirmPolicies.Parse("TPT|1|1|0"); Check(KeystoneFirmPolicies.AutoAllowed("TPT", true) && !KeystoneFirmPolicies.CopyAllowed("TPT") && KeystoneFirmPolicies.Serialize() == "TPT|1|1|0", "your firm rules win and save"); KeystoneFirmPolicies.Parse("");
            // ACCOUNTS: auto names per firm, firm colours you pick (saved, used everywhere)
            var nn = KeystoneFirmNames.AutoNicks(new[] { Tuple.Create("TAKEPROFIT250", "TPT"), Tuple.Create("TAKEPROFIT257", "TPT"), Tuple.Create("LT1", "LUCID"), Tuple.Create("TAKEPROFIT332", "tpt") });
            Check(nn["TAKEPROFIT250"] == "TPT 1" && nn["TAKEPROFIT257"] == "TPT 2" && nn["TAKEPROFIT332"] == "TPT 3" && nn["LT1"] == "LUCID 1", "AUTO-NAME: TPT 1, TPT 2, TPT 3 + LUCID 1", string.Join(",", nn.Values));
            Check(KeystoneFirmNames.Detect("TAKEPROFIT237407099", "TPT_TEST_PRO")[0] == "TPT", "TAKEPROFIT… on TPT_TEST_PRO → TPT");
            KeystoneFirmNames.ParseColors("TPT|#4FC3F7\nBAD|red"); Check(KeystoneFirmNames.Detect("TAKEPROFIT250", "")[2] == "#4FC3F7" && !KeystoneFirmNames.Colors.ContainsKey("BAD"), "a picked firm colour wins (bad lines ignored)");
            string sc = KeystoneFirmNames.SerializeColors(); KeystoneFirmNames.ParseColors(""); Check(KeystoneFirmNames.Detect("TAKEPROFIT250", "")[2] == "#FFB74D", "no pick → the built-in colour"); KeystoneFirmNames.ParseColors(sc); Check(KeystoneFirmNames.Colors["TPT"] == "#4FC3F7", "colours save and load"); KeystoneFirmNames.ParseColors("");
            // the prop tracker: dated expenses round-trip, Spent = price + expenses, the HTML page carries the totals and status colours
            a.Expenses.Add(Tuple.Create(new DateTime(2026, 10, 15), 85.0, "activation"));
            var back2 = KeystonePool.Parse(KeystonePool.Serialize(new[] { a }));
            Check(back2[0].Expenses.Count == 1 && back2[0].Expenses[0].Item3 == "activation" && back2[0].Expenses[0].Item1 == new DateTime(2026, 10, 15) && Math.Abs(back2[0].Spent - 197) < 1e-9, "pool expenses save and load (spent = $112 + $85)");
            string tdir = Path.Combine(Path.GetTempPath(), "ks_tracker_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tdir);
            string rt = Path.Combine(tdir, "RealTrades.csv"), rj = Path.Combine(tdir, "RealJournal.csv");
            File.WriteAllLines(rt, new[] { "session,account,symbol,side,qty,entry_time,entry,exit_time,exit,points,gross,fees,net,best_open,worst_open,hold_s,placed_from", "2026-10-20,MFFU1,MNQ,BUY,1,2026-10-20 10:00:00,20000,2026-10-20 10:05:00,20010,10,20.00,1.24,18.76,25,-5,300,STUDIO" });
            File.WriteAllLines(rj, new[] { "account,symbol,side,qty,entry_time,entry,exit_time,exit,points,net,hold_s,placed_from", "TPT9,MNQ,SELL,1,2026-10-19 19:00:00,20000,2026-10-19 19:10:00,20010,-10,-20.00,600,NINJATRADER" });
            var tt = KeystoneTracker.ReadTrades(rj, rt);
            Check(tt.Count == 2 && Math.Abs(tt.Sum(x => x.Fees) - 1.24) < 1e-9 && tt.Any(x => x.Account == "TPT9" && x.Day == new DateTime(2026, 10, 20)), "tracker reads both journals (fees from RealTrades, evening trade → next session)");
            try { Directory.Delete(tdir, true); } catch { }
            var live = new Dictionary<string, KeystoneTrackerLive> { { "MFFU1", new KeystoneTrackerLive { Account = "MFFU1", Balance = 51234, Today = 321, Connected = true } } };
            string html = KeystoneTracker.Build(new List<KeystonePoolAccount>(back2) { new KeystonePoolAccount { Account = "TPT9", Status = "BLOWN", Started = new DateTime(2026, 10, 2), Cost = 150 } }, tt, live, new DateTime(2026, 10, 21, 12, 0, 0), 30);
            Check(html.Contains("<meta http-equiv='refresh' content='30'>") && html.Contains("class='funded click'") && html.Contains("class='blown click'") && html.Contains("id='p-fund'") && html.Contains(KeystoneTracker.StatusColor("FUNDED")) && html.Contains("$347") && html.Contains("+$803") && html.Contains("$51,234"),
                "tracker page: auto refresh, funded row green, blown row, spent $347, net +$803, live balance", html.Length.ToString());
            // 📒 DAILY BOOK: NinjaTrader's day per account wins over the journal's single trades of that day; save / load; the page shows it
            var bookT = new List<KeystoneTrackerTrade>(); var d21 = new DateTime(2026, 10, 21);
            KeystoneDailyBook.Set(bookT, d21, "TPT1", 75, 60, 3); KeystoneDailyBook.Set(bookT, d21, "TPT2", 253, 227.5, 0); KeystoneDailyBook.Set(bookT, d21, "TPT1", 80, 64, 4);
            var bookT2 = KeystoneDailyBook.Parse(KeystoneDailyBook.Serialize(bookT));
            Check(bookT2.Count == 2 && bookT2.Single(x => x.Account == "TPT1").Net == 64 && Math.Abs(bookT2.Single(x => x.Account == "TPT2").Fees - 25.5) < 1e-9, "daily book: today's numbers replace, commissions = gross − realized, save / load");
            var jr = new List<KeystoneTrackerTrade> { new KeystoneTrackerTrade { Account = "TPT2", Day = d21, Gross = 10, Net = 9 }, new KeystoneTrackerTrade { Account = "TPT2", Day = d21, Gross = 12, Net = 11 }, new KeystoneTrackerTrade { Account = "TPT2", Day = d21.AddDays(-1), Gross = 5, Net = 4 } };
            var mg = KeystoneDailyBook.Merge(jr, bookT2);
            Check(mg.Count == 3 && mg.Where(x => x.Day == d21).Sum(x => x.Net) == 64 + 227.5 && mg.Single(x => x.Day == d21 && x.Account == "TPT2").Count == 2 && mg.Any(x => x.Day == d21.AddDays(-1) && x.Net == 4), "daily book merge: the day from NinjaTrader (all accounts), the journal's count, older journal days kept");
            var grp = new List<KeystoneTrackerGroup> { new KeystoneTrackerGroup { Name = "TPT High", Color = "#BA68C8", Leader = "TPT1", Members = new List<string> { "TPT1", "TPT2" }, On = true, Chart = true } };
            string html2 = KeystoneTracker.Build(new List<KeystonePoolAccount>(back2), mg, live, new DateTime(2026, 10, 21, 12, 0, 0), 10, grp);
            Check(html2.Contains("id='p-today'") && html2.Contains("id='p-grp'") && html2.Contains("TPT High") && html2.Contains("class='day today'") && html2.Contains("tr class='sub'") && html2.Contains("+$292"), "tracker: TODAY + GROUPS tabs, today's day row highlighted with its accounts, +$292 net", html2.Length.ToString());
            try { System.IO.File.WriteAllText(System.IO.Path.Combine("tests", ".build", "tracker_preview.html"), html2); } catch { }
            {
                // TODAY redesign: realized (after fees) against the day limit; over / near flagged; no evaluation total; funded adds up
                var poolT = new List<KeystonePoolAccount>(); var liveT = new Dictionary<string, KeystoneTrackerLive>();
                Action<string, double, double, bool> addT = (n, gross, closed, fund) => { poolT.Add(new KeystonePoolAccount { Account = n, Nick = n, Status = fund ? "PASSED" : "EVAL" }); liveT[n] = new KeystoneTrackerLive { Account = n, Connected = true, Gross = gross, Closed = closed, Today = closed, Balance = 50000 + closed, DayCap = fund ? double.NaN : 1500, Funded = fund, Left = fund ? double.NaN : 1450 - closed, Floor = 48000 + closed, Room = 2000 + closed, Start = 50000, Target = 3000, Dd = 2000, Plan = fund ? "" : "today: up to +$" + Math.Max(0, 1500 - closed).ToString("0") + " more" }; };
                addT("TPT 1", 1650, 1557, false); addT("TPT 2", 1300, 1250, false); addT("TPT 3", 400, 380, false); addT("TPT 9", 900, 870, true);
                string html3 = KeystoneTracker.Build(poolT, new List<KeystoneTrackerTrade>(), liveT, new DateTime(2026, 10, 21, 12, 0, 0), 10, new List<KeystoneTrackerGroup>());
                Check(html3.Contains("OVER by $57") && html3.Contains("Over the day limit") && html3.Contains("Funded &#8226; yours today") && !html3.Contains("Realized today<"), "tracker TODAY: realized vs the $1,500 day limit (TPT 1 OVER by $57), funded total only", html3.Length.ToString());
                try { System.IO.File.WriteAllText(System.IO.Path.Combine("tests", ".build", "tracker_today.html"), html3); } catch { }
                var trT = new List<KeystoneTrackerTrade> { new KeystoneTrackerTrade { Account = "TPT 1", Day = new DateTime(2026, 10, 21), Gross = 1650, Net = 1557, Fees = 93, Count = 4 } };
                string html4 = KeystoneTracker.Build(poolT, trT, liveT, new DateTime(2026, 10, 21, 19, 0, 0), 10, new List<KeystoneTrackerGroup>(), new DateTime(2026, 10, 21));
                Check(html4.Contains("Wednesday 2026-10-21") && html4.Contains("LAST SESSION") && html4.Contains("To pass") && html4.Contains("Auto-liq at") && html4.Contains("auto-liq $49,557"), "tracker after 18:00 NY: still Wednesday (the day of the last fill) as LAST SESSION; to pass / auto-liq shown", html4.Length.ToString());
                poolT.Add(new KeystonePoolAccount { Account = "TPT 4", Nick = "TPT 4", Status = "BLOWN", Notes = "BLOWN 2026-10-21 19:58 NY • balance $49,739.00 vs floor $49,740.00 • day −$2,001 realized (day high +$120, low −$2,050)" });
                html4 = KeystoneTracker.Build(poolT, trT, liveT, new DateTime(2026, 10, 21, 19, 0, 0), 10, new List<KeystoneTrackerGroup>(), new DateTime(2026, 10, 21));
                Check(html4.Contains("blownrow") && html4.Contains("💀 BLOWN") && html4.Contains("day high +$120"), "tracker: an account blown today stays in TODAY — red row with the details + a BLOWN card", html4.Length.ToString());
                try { System.IO.File.WriteAllText(System.IO.Path.Combine("tests", ".build", "tracker_today.html"), html4); } catch { }
            }
        }
        // 10. presets + account watch
        {
            var p = KeystoneFirmRules.Guess("MFFUEVREOD723518001");
            Check(p != null && p.Dd == 2000 && p.Target == 3000 && p.ConsistencyPct == 30 && p.DayProfitCap == 900 && p.PayoutBuffer == 2100 && p.MaxMicros == 30, "MFFUEVREOD… → MFFU RAPID EOD 50K ($2,000 EOD, $3,000 target, 30% → $900 a day, 30 micros, first payout at +$2,100)");
            Check(KeystoneFirmRules.Guess("Sim101").Firm == "SIM", "Sim101 → practice preset");
            Check(!KeystoneFirmRules.Find("TAKE PROFIT TRADER 50K").Automation, "TAKE PROFIT TRADER preset: no automation");
            var w = new KeystoneAccountWatch { Account = "X" }; var d0 = new DateTime(2026, 10, 5);
            w.Observe(d0, 50000); w.Observe(d0, 50800); w.Observe(d0.AddDays(1), 51200); w.Observe(d0.AddDays(2), 50900);
            Check(Math.Abs(w.PeakEod(d0.AddDays(2)) - 51200) < 1e-9 && Math.Abs(w.Floor(d0.AddDays(2), 2000, true) - 49200) < 1e-9, "EOD floor = highest close before today − $2,000");
            Check(Math.Abs(w.ConsistencyPct() - 100.0 * 800 / 900) < 1e-6, "consistency = best day ÷ total profit", w.ConsistencyPct().ToString());
            var w2 = KeystoneAccountWatch.Parse(w.Serialize()); Check(w2 != null && w2.Closes.Count == 3 && w2.StartBalance == 50000, "account watch saves and loads");
        }
        {   // ● THE LABS' LIVE SIDE (BUILD 10d): detection + stage + progress + pools + order + tracking — reads accounts only, never an order
            var snaps = new List<Tuple<string, string, double, double, int>> { Tuple.Create("TPT123456", "TakeProfit Rithmic", 52800.0, 0.0, 0), Tuple.Create("Sim101", "Simulated", 100000.0, 0.0, 0), Tuple.Create("LT-77001", "Rithmic", 50300.0, 300.0, 1), Tuple.Create("PRO98765-TPT", "TakeProfit Rithmic", 50000.0, 0.0, 0) };
            var acc = KeystoneLabLive.Classify(snaps, new Dictionary<string, string>(), new Dictionary<string, int> { { "TPT123456", 4 } });
            var tpt = acc.FirstOrDefault(a => a.Account == "TPT123456"); var pro = acc.FirstOrDefault(a => a.Account == "PRO98765-TPT");
            Check(acc.Count == 3 && tpt != null && tpt.Firm == "TPT" && tpt.Stage == "EVAL" && KeystoneLabLive.Progress(tpt).StartsWith("+$2,800 / $3,000 · $200 to go") && pro != null && pro.Stage == "FUNDED" && !acc.Any(a => a.Account == "Sim101"),
                "● LIVE detection: TPT123456 at $52,800 = TPT · EVAL '+$2,800 / $3,000 · $200 to go' • the PRO account = TPT · FUNDED • Sim101 left out", string.Join(" | ", acc.Select(a => a.Account + "=" + a.Firm + "·" + a.Stage + " " + KeystoneLabLive.Progress(a))));
            Check(KeystoneLabLive.Badge(acc).StartsWith("● LIVE · ") && KeystoneLabLive.Badge(new List<KeystoneLiveAcct>()) == "" && KeystoneLabLive.Badge(acc.Where(a => a.Account == "TPT123456").ToList()) == "● LIVE · 1 TPT eval", "● the LIVE button: grey without prop accounts, a count when found", KeystoneLabLive.Badge(acc));
            var ov = KeystoneLabLive.Classify(snaps, new Dictionary<string, string> { { "TPT123456", "FUNDED" } }, null); Check(ov.First(a => a.Account == "TPT123456").Stage == "FUNDED" && ov.First(a => a.Account == "TPT123456").Overridden, "● a saved one-click override wins (TPT123456 → FUNDED)");
            var passed = KeystoneLabLive.Classify(new[] { Tuple.Create("TPT555", "TakeProfit", 53050.0, 250.0, 1) }, null, new Dictionary<string, int> { { "TPT555", 5 } }); Check(KeystoneLabLive.TargetReached(passed[0]) && KeystoneLabLive.Progress(passed[0]).StartsWith("✅ target reached"), "● +$3,050 with the minimum days = target reached, waiting for the firm");
            var links = new Dictionary<string, string>(); KeystoneLabLive.LinkFunded(acc, new List<Tuple<string, string>> { Tuple.Create("TPT", "TPT555") }, links); Check(pro.LinkedEval == "TPT555" && links["PRO98765-TPT"] == "TPT555", "● the TPT PRO account is linked to the eval that passed");
            var pools = KeystoneLabLive.Pools(acc); Check(pools.Any(x => x.Item1 == "TPT · EVAL") && pools.Any(x => x.Item1 == "TPT · FUNDED") && pools.Any(x => x.Item1 == "LUCID · EVAL"), "● one pool per firm per stage", string.Join(", ", pools.Select(x => x.Item1)));
            var pick = new List<KeystoneLiveAcct> { new KeystoneLiveAcct { Account = "T1", Firm = "TPT" }, new KeystoneLiveAcct { Account = "T2", Firm = "TPT" }, new KeystoneLiveAcct { Account = "L1", Firm = "LUCID" } };
            Check(string.Join(",", KeystoneLabLive.Order(pick)) == "T1,L1,T2", "● the live rotation order = the backtest's (firm by firm)");
            var days = new List<KeystoneLiveDay>(); KeystoneLabLive.Upsert(days, new DateTime(2026, 11, 9), 50000, 0, 48000); KeystoneLabLive.Upsert(days, new DateTime(2026, 11, 9), 50400, 1, 48000); KeystoneLabLive.Upsert(days, new DateTime(2026, 11, 10), 50650, 1, 48400);
            var back = KeystoneLabLive.ParseDays(KeystoneLabLive.DaysCsv(days)); var lr = KeystonePayoutRules.For("LUCID", KeystonePayoutRules.Presets());
            var el = KeystoneLabLive.Eligibility(back, lr, 50000, new DateTime(2026, 11, 10));
            Check(back.Count == 2 && Math.Abs(back[0].Pnl - 400) < 0.01 && Math.Abs(back[1].StartBal - 50400) < 0.01 && Math.Abs(back[1].Pnl - 250) < 0.01 && el.StartsWith("qualifying days 2/" + lr.QualDays), "● daily tracking: a row per day (start = yesterday's end), saved and read back • eligibility n / N", el);
        }
        {   // ● LIVE (BUILD 11e): the stage on the name's tokens AFTER the firm's prefix (TAKE-PRO-FIT is not "PRO"), unsure = EVAL; only ACTIVE accounts count
            Check(KeystoneLabLive.Stage("TPT", "TAKEPROFIT104385428") == "EVAL", "● TAKEPROFIT104385428 = TPT · EVAL (the PRO inside TAKE-PRO-FIT does not count)", KeystoneLabLive.Stage("TPT", "TAKEPROFIT104385428"));
            Check(KeystoneLabLive.Stage("TPT", "TAKEPROFITPRO999531172") == "FUNDED", "● TAKEPROFITPRO999531172 → PRO… = TPT · FUNDED");
            Check(KeystoneLabLive.Stage("TPT", "PRO98765-TPT") == "FUNDED" && KeystoneLabLive.Stage("TPT", "TPT-PRO-123") == "FUNDED" && KeystoneLabLive.Stage("TPT", "TPT123456") == "EVAL", "● PRO98765-TPT / TPT-PRO-123 = FUNDED • TPT123456 = EVAL");
            Check(KeystoneLabLive.Stage("MFFU", "MFFUEVREOD723518001") == "EVAL", "● MFFUEVREOD723518001 → EV… = MFFU · EVAL");
            Check(KeystoneLabLive.Stage("LUCID", "LT-77001") == "EVAL" && KeystoneLabLive.Stage("LUCID", "LFP12345") == "FUNDED" && KeystoneLabLive.Stage("TRADEIFY", "TDFYSF5501") == "FUNDED" && KeystoneLabLive.Stage("TRADEIFY", "TDFY5501") == "EVAL", "● Lucid LT- = eval, LFP = funded • Tradeify TDFYSF = funded, TDFY = eval");
            Check(KeystoneLabLive.Stage("NOSUCHFIRM", "XYZPRO1") == "EVAL", "● an unknown firm / name: EVAL, never FUNDED by accident");
            var keep = KeystoneLabLive.SaveKeys(); KeystoneLabLive.LoadKeys("FUNDED|TPT|PRO,PA-,FUNDED,LIVE\nPREFIX|TPT|TAKEPROFIT,TPT"); Check(KeystoneLabLive.Stage("TPT", "TAKEPROFIT104385428") == "EVAL" && KeystoneLabLive.Stage("TPT", "TAKEPROFITPRO1") == "FUNDED" && keep.Contains("PREFIX|TPT|"), "● patterns.txt (an old file without PREFIX lines too) keeps the token rule"); KeystoneLabLive.LoadKeys(keep);
            var raw = new List<KeystoneLiveRaw>(); for (int i = 0; i < 195; i++) raw.Add(new KeystoneLiveRaw { Name = "TAKEPROFIT" + (104385000 + i), Connection = "TakeProfit Rithmic", Balance = 0, Connected = i % 3 != 0 });
            raw.Add(new KeystoneLiveRaw { Name = "TAKEPROFIT104385428", Connection = "TakeProfit Rithmic", Balance = 52771, Nick = "TPT 13" });
            raw.Add(new KeystoneLiveRaw { Name = "MFFUEVREOD723518001", Connection = "Rithmic", Balance = 0 });
            raw.Add(new KeystoneLiveRaw { Name = "TAKEPROFIT104300001", Connection = "TakeProfit Rithmic", Balance = 47900 });
            raw.Add(new KeystoneLiveRaw { Name = "TAKEPROFIT104300002", Connection = "TakeProfit Rithmic", Balance = 51000, Status = "Disabled" });
            var all = KeystoneLabLive.Classify(raw, null, null); var actv = KeystoneLabLive.ActiveOnly(all); var t13 = all.First(a => a.Account == "TAKEPROFIT104385428");
            Check(all.Count(a => a.Stage == "FUNDED") == 0, "● the user's 195 TPT accounts + the MFFU eval: none is FUNDED", string.Join(",", all.Where(a => a.Stage == "FUNDED").Select(a => a.Account).Take(5)));
            Check(actv.Count == 1 && actv[0] == t13 && t13.Display == "TPT 13" && KeystoneLabLive.Badge(all) == "● LIVE · 1 TPT eval", "● only the active account counts: ● LIVE · 1 TPT eval (TPT 13)", KeystoneLabLive.Badge(all) + " / " + actv.Count);
            Check(KeystoneLabLive.ShortProgress(t13).StartsWith("$229 to pass · days 0/"), "● TPT 13 at $52,771: $229 to pass", KeystoneLabLive.ShortProgress(t13));
            Check(all.First(a => a.Account == "TAKEPROFIT104300001").Why.StartsWith("blown") && all.First(a => a.Account == "TAKEPROFIT104300002").Why.StartsWith("disabled") && all.First(a => a.Account == "MFFUEVREOD723518001").Why == "balance 0 / unknown" && all.First(a => a.Account == "TAKEPROFIT104385000").Why == "disconnected",
                "● inactive reasons: blown at / under the floor • disabled • balance 0 • disconnected", string.Join(" | ", all.Where(a => !a.Active).Take(3).Select(a => a.Account + " " + a.Why)));
            var gs = KeystoneLabLive.ParseGroups("MY MIX|LT-1,TPT1\nBAD\nOTHER|TPT1,TPT2"); Check(gs.Count == 2 && gs[1].Item2.Count == 1 && gs[1].Item2[0] == "TPT2" && KeystoneLabLive.ParseGroups(KeystoneLabLive.GroupsText(gs)).Count == 2, "● your groups: an account in one group only, saved and read back");
            var ga = new List<KeystoneLiveAcct> { new KeystoneLiveAcct { Account = "LT-1", Firm = "LUCID", Stage = "EVAL" }, new KeystoneLiveAcct { Account = "TPT1", Firm = "TPT", Stage = "EVAL" }, new KeystoneLiveAcct { Account = "TPT3", Firm = "TPT", Stage = "EVAL" }, new KeystoneLiveAcct { Account = "OLD", Firm = "TPT", Stage = "EVAL", Active = false } };
            var grp = KeystoneLabLive.Groups(ga, gs); Check(grp.Count == 2 && grp[0].Item1 == "GROUP:MY MIX" && grp[0].Item3.Count == 2 && grp[1].Item1 == "TPT · EVAL" && grp[1].Item3.Count == 1, "● the cards: your mixed group first, then TPT · EVAL for the rest (inactive left out)", string.Join(", ", grp.Select(g => g.Item1 + ":" + g.Item3.Count)));
            Check(KeystoneLabLive.PlanWords(new Dictionary<string, string> { { "mnq", "20" }, { "tp", "1500" }, { "stop", "0" }, { "times", "200 830 930 1000 1100" }, { "side", "FIRM" } }, true) == "MNQ 20 · take profit +$1,500 · no stop (rides to the floor) · at 2:00 / 8:30 / 9:30 / 10:00 / 11:00 NY · TPT = manual: its button flashes", "● the plan in plain words");
        }
        // ================= 🤖 LIVE AUTOPILOT (BUILD 2026-11-11g): the labs' plans on real accounts ONLY through the armed links =================
        {
            var d0 = new DateTime(2026, 10, 6);   // a Tuesday (NY)
            Func<string, string, string, FakeBroker, int, double, KeystoneAutoSeat> seat = (acc, firm, stage, b, max, loss) => new KeystoneAutoSeat
            {
                Account = acc, Nick = acc, Firm = firm, Stage = stage, Owner = "ME", Group = firm + " · " + stage, Plan = stage == "FUNDED" ? KeystoneAutoPlan.FundedDefault(firm == "TPT") : KeystoneAutoPlan.EvalDefault(),
                Link = new KeystoneAccountLink(b, "MNQ") { MaxContracts = max, DayLossLimit = loss, AutoOco = true }, Mirror = Trader(), Start = 50000, Dd = 2000, Target = 3000, Balance = 50500, MinDays = 2
            };
            Func<FakeBroker, int> markets = b => b.Sent.Count(x => x.StartsWith("MARKET"));
            // runs the autopilot second by second; every pending market order fills at px (the fake broker)
            Action<KeystoneAutopilot, DateTime, int, double> run = (ap, from, secs, px) => { for (int i = 0; i <= secs; i++) { ap.Tick(from.AddSeconds(i), px); foreach (var s in ap.Seats) ((FakeBroker)s.Link.Broker).FillMarket(px); } };
            KeystoneFirmPolicies.Chosen.Clear();

            // 1. nothing is sent before ARM + START (typed)
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s);
                run(ap, d0.AddHours(1).AddMinutes(59), 240, 20000);
                Check(markets(b) == 0 && b.Sent.Count == 0 && !ap.Running, "🤖 not armed, not started: NOTHING is sent at the 2:00 entry minute", string.Join(" | ", b.Sent));
                string e1 = ap.Start("START", d0.AddHours(1).AddMinutes(58));
                Check(!ap.Running && s.Out && s.OutWhy == "NOT ARMED", "🤖 START with the account NOT ARMED: nothing starts (" + e1 + ")", e1 + " / " + s.OutWhy);
                s.Link.Arm(d0.AddHours(1).AddMinutes(58)); s.Out = false;
                string e2 = ap.Start("", d0.AddHours(1).AddMinutes(58)), e3 = ap.Start("yes", d0.AddHours(1).AddMinutes(58)), e4 = ap.Start("start", d0.AddHours(1).AddMinutes(58));
                run(ap, d0.AddHours(1).AddMinutes(59), 240, 20000);
                Check(!ap.Running && e2 != "" && e3 != "" && e4 != "" && b.Sent.Count == 0, "🤖 ARMED but START not typed (empty / yes / lower-case): refused, nothing sent", e2 + " | " + string.Join(" | ", b.Sent));
                var b2 = new FakeBroker(); var ap2 = new KeystoneAutopilot(); var s2 = seat("LT-1", "LUCID", "EVAL", b2, 20, 0); s2.WantAuto = true; ap2.Seats.Add(s2); s2.Link.Arm(d0);
                string e5 = ap2.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap2, d0.AddHours(1).AddMinutes(59), 50, 20000);
                Check(e5 == "" && ap2.Running && markets(b2) == 0, "🤖 ARMED + START typed: still nothing before the entry minute (1:59)", string.Join(" | ", b2.Sent));
                run(ap2, d0.AddHours(2), 20, 20000);
                Check(markets(b2) == 1 && b2.Sent.First(x => x.StartsWith("MARKET")).EndsWith(" auto") && b2.Q != 0 && Math.Abs(b2.Q) == 20, "🤖 Lucid eval AUTO: at 2:00 ONE market entry of MNQ 20 through the armed link (marked automated)", string.Join(" | ", b2.Sent));
                // 2. brackets attached to every entry: a STOP at the floor room and a TARGET +$1,500 after costs, one cancels the other
                var stp = b2.All.FirstOrDefault(o => !o.Done && o.Kind == "STOP"); var tgt = b2.All.FirstOrDefault(o => !o.Done && o.Kind == "LIMIT"); int dir = Math.Sign(b2.Q);
                double tpPts = KeystoneAutopilot.TpPoints(1500, 20, 0.25), slPts = KeystoneAutopilot.StopPoints(2500, 20, 0.25, 2);
                Check(stp != null && tgt != null && stp.Qty == 20 && tgt.Qty == 20 && stp.Oco != "" && stp.Oco == tgt.Oco && Math.Abs(tgt.Price - (20000 + dir * tpPts)) < 1e-9 && Math.Abs(stp.Price - (20000 - dir * slPts)) < 1e-9,
                    "🤖 the bracket at the broker: TARGET " + tpPts + " pts (+$1,500 after costs) and STOP " + slPts + " pts (balance $50,500 − floor $48,000, costs + 2 ticks off), OCO", stp == null || tgt == null ? "missing" : stp.Price + " / " + tgt.Price);
                Check(tpPts * 2 * 20 - KeystoneAutopilot.Cost * 20 >= 1500 && (tpPts - 0.25) * 2 * 20 - KeystoneAutopilot.Cost * 20 < 1500 && (slPts + 0.5) * 40 + KeystoneAutopilot.Cost * 20 <= 2500, "🤖 TP is the first tick netting ≥ $1,500 after costs; the stop leaves ≥ 2 ticks above the floor", tpPts + " / " + slPts);
                Check(ap2.Journal.Any(r => r.Event == "TICKET") && ap2.Journal.Any(r => r.Event == "ORDER") && ap2.Journal.Any(r => r.Event == "FILL" && r.Fill == 20000) && ap2.Journal.Any(r => r.Event == "BRACKET"), "🤖 journal: TICKET → ORDER → FILL → BRACKET", string.Join(",", ap2.Journal.Select(r => r.Event)));
                // 5. one entry per account per day (the target fills → flat; later minutes and a re-tick of 2:00 send nothing more)
                b2.Trigger(tgt); run(ap2, d0.AddHours(2).AddMinutes(1), 5, 20000); run(ap2, d0.AddHours(9), 30, 20000); run(ap2, d0.AddHours(2), 30, 20000);
                var ex = ap2.Journal.LastOrDefault(r => r.Event == "EXIT");
                Check(markets(b2) == 1 && b2.Q == 0 && ex != null && ex.Net > 1499 && ex.Detail.Contains("model expected TP at"), "🤖 ONE entry per account per day: after the target, nothing more today • EXIT journalled with live vs model", markets(b2) + " / " + (ex != null ? ex.Detail : "no exit"));
                run(ap2, d0.AddDays(1).AddHours(2), 20, 20100);
                Check(markets(b2) == 2, "🤖 the next trading day: one new entry at 2:00", markets(b2).ToString());
                Check(KeystoneAutoRow.Header.Split(',').Length == ap2.Journal[0].Line().Split(',').Length && ex.Line().Contains(",EXIT,"), "🤖 the journal CSV line has every column (autopilot-<date>.csv)");
            }
            // 3. TPT is never AUTO (even with automation switched on in its rules): GUIDED — the ticket flashes, ONE press places the bracket
            {
                KeystoneFirmPolicies.Chosen["TPT"] = new KeystoneFirmPolicy { AutoEval = true, AutoFunded = true, Copy = true };
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("TPT1", "TPT", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                Check(!KeystoneAutopilot.AutoAllowedFor("TPT", "EVAL") && !KeystoneAutopilot.AutoAllowedFor("TPT", "FUNDED") && !s.Auto && KeystoneAutopilot.AutoWhyNot("TPT", "EVAL").StartsWith("TPT"), "🤖 TPT: never AUTO (eval or funded), even when its rules say automation on");
                string e = ap.Start("", d0.AddHours(1).AddMinutes(58)); run(ap, d0.AddHours(2), 30, 20000);
                var fl = ap.Flashing();
                Check(e == "" && markets(b) == 0 && fl.Count == 1 && fl[0] == s && s.State.StartsWith("▶ PRESS") && s.Ticket.Qty == 20 && s.Ticket.TpUsd == 1500 && s.Ticket.Reason.Contains("side coin"), "🤖 TPT GUIDED: at 2:00 the ticket flashes (size, TP, stop, the side coin) — nothing sent by itself", e + " / " + s.State + " / " + string.Join(" | ", b.Sent));
                string pr = ap.Press("TPT1", d0.AddHours(2).AddSeconds(40), 20000); run(ap, d0.AddHours(2).AddSeconds(41), 10, 20000);
                Check(pr == "" && markets(b) == 1 && b.All.Any(o => !o.Done && o.Kind == "STOP") && b.All.Any(o => !o.Done && o.Kind == "LIMIT") && !b.Sent.First(x => x.StartsWith("MARKET")).EndsWith(" auto"), "🤖 ONE press = the full bracket through the armed link (entry + STOP + TARGET)", pr + " | " + string.Join(" | ", b.Sent));
                Check(ap.Press("TPT1", d0.AddHours(2).AddSeconds(50), 20000) != "" && markets(b) == 1, "🤖 a second press sends nothing (one entry a day)");
                KeystoneFirmPolicies.Chosen.Clear();
            }
            // 4. a firm whose automation is OFF refuses AUTO (Lucid eval switched off; Apex / Topstep are not AUTO firms here)
            {
                KeystoneFirmPolicies.Chosen["LUCID"] = new KeystoneFirmPolicy { AutoEval = false, AutoFunded = true, Copy = true };
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                Check(!s.Auto && KeystoneAutopilot.AutoWhyNot("LUCID", "EVAL").Contains("automation is OFF") && KeystoneAutopilot.AutoAllowedFor("LUCID", "FUNDED"), "🤖 Lucid eval with automation OFF in its rules: AUTO refused (funded still allowed)", KeystoneAutopilot.AutoWhyNot("LUCID", "EVAL"));
                ap.Start("", d0.AddHours(1).AddMinutes(58)); run(ap, d0.AddHours(2), 30, 20000);
                Check(markets(b) == 0 && s.Ticket != null && !s.Ticket.Auto, "🤖 … so nothing goes by itself: it becomes a GUIDED ticket", string.Join(" | ", b.Sent));
                Check(!KeystoneAutopilot.AutoAllowedFor("APEX", "EVAL") && !KeystoneAutopilot.AutoAllowedFor("TOPSTEP", "FUNDED") && KeystoneAutopilot.AutoAllowedFor("TRADEIFY", "EVAL"), "🤖 AUTO only for Lucid / Tradeify (Apex, Topstep … = GUIDED)");
                KeystoneFirmPolicies.Chosen.Clear();
            }
            // 6. the side: the same for every account of one firm at the same slot (the labs' hash coin), and a firm never holds both sides
            {
                bool same = true, varies = false, firmsDiffer = false; int prevL = 0;
                for (int k = 0; k < 40; k++)
                {
                    var day = d0.AddDays(k); var ap = new KeystoneAutopilot(); var bs = new List<FakeBroker>();
                    foreach (var f in new[] { "LUCID", "LUCID", "LUCID", "TRADEIFY", "TRADEIFY", "TPT", "TPT" }) { var b = new FakeBroker(); bs.Add(b); var s = seat(f + ap.Seats.Count, f, "EVAL", b, 20, 0); s.SlotTime = 200; ap.Seats.Add(s); }
                    var sides = ap.Seats.Select(s => ap.SideFor(s, day)).ToList();
                    foreach (var f in new[] { "LUCID", "TRADEIFY", "TPT" }) { var fs = ap.Seats.Where(s => s.Firm == f).Select(s => ap.SideFor(s, day)).Distinct().ToList(); if (fs.Count != 1 || fs[0] != KeystoneLabFlow.SideOf(day, f, 200, 0)) same = false; }
                    if (k > 0 && sides[0] != prevL) varies = true; prevL = sides[0]; if (sides[0] != sides[3] || sides[0] != sides[5]) firmsDiffer = true;
                }
                Check(same && varies && firmsDiffer, "🤖 side: one coin per firm per time slot (KeystoneLabFlow.SideOf) — every account of a firm the same side; days and firms independent");
                var ap2 = new KeystoneAutopilot(); var bA = new FakeBroker(); var bB = new FakeBroker(); var sA = seat("LT-A", "LUCID", "FUNDED", bA, 5, 0); var sB = seat("LT-B", "LUCID", "FUNDED", bB, 5, 0); sB.SlotTime = 830; ap2.Seats.Add(sA); ap2.Seats.Add(sB);
                int coin = KeystoneLabFlow.SideOf(d0, "LUCID", 830, 0); bA.Exec(-coin, 20000, "open");
                Check(ap2.SideFor(sB, d0) == -coin, "🤖 an account of a firm that already holds a position takes the SAME side (never both sides inside a firm)");
            }
            // 7. the runaway guard (8 entries a minute) trips on the seat's link → the autopilot sends nothing more there
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58)); var t1 = d0.AddHours(1).AddMinutes(58).AddSeconds(30);
                for (int k = 0; k < 9; k++) s.Mirror.Place("LIMIT", 1, 1, 19900 - k); ap.Tick(t1, 20000);
                run(ap, t1.AddSeconds(1), 5, 20000); run(ap, d0.AddHours(2), 30, 20000);
                Check(!s.Link.Armed && s.Link.Log.Any(x => x.Contains("RUNAWAY GUARD")) && b.Sent.Count(x => x.StartsWith("LIMIT")) == 8 && markets(b) == 0 && s.Out && ap.Journal.Any(r => r.Event == "DISARMED"), "🤖 runaway guard: the 9th order in a minute disarms the link — the autopilot's 2:00 entry is NOT sent", string.Join(" | ", b.Sent.Take(12)) + " / " + s.State);
            }
            // 8. a reject → that account is disarmed (the link's rule) and the autopilot sends nothing more on it
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58));
                for (int i = 0; i <= 3; i++) ap.Tick(d0.AddHours(2).AddSeconds(i), 20000);
                var mk = b.All.FirstOrDefault(o => o.Kind == "MARKET"); if (mk != null) { mk.Rejected = true; mk.Done = true; }
                for (int i = 4; i <= 40; i++) ap.Tick(d0.AddHours(2).AddSeconds(i), 20000);
                Check(mk != null && !s.Link.Armed && s.Out && markets(b) == 1 && b.Sent.All(x => x.StartsWith("MARKET") || x == "FLATTEN") && ap.Journal.Any(r => r.Event == "DISARMED" && r.Link.Contains("REJECTED")), "🤖 a REJECTED entry → the link disarms (its own rule: flatten) → the account is out of the autopilot (no new entry)", string.Join(" | ", b.Sent) + " / " + s.State);
            }
            // one order at a time: no fill → nothing is re-sent; a late fill still gets its STOP + TARGET
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58)); for (int i = 0; i <= 45; i++) ap.Tick(d0.AddHours(2).AddSeconds(i), 20000);
                bool none = markets(b) == 1 && b.Q == 0 && ap.Journal.Any(r => r.Event == "NO FILL");
                b.FillMarket(20000); for (int i = 46; i <= 55; i++) ap.Tick(d0.AddHours(2).AddSeconds(i), 20000);
                Check(none && markets(b) == 1 && b.Q != 0 && b.All.Any(o => !o.Done && o.Kind == "STOP") && b.All.Any(o => !o.Done && o.Kind == "LIMIT"), "🤖 one order at a time: no fill in 30 s → nothing re-sent; the late fill still gets its STOP + TARGET", string.Join(" | ", b.Sent));
            }
            // 9. DISARM / ⛔ CLOSE ALL / LOCK FOR TODAY stop the autopilot — positions stay with their broker brackets
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                var b2 = new FakeBroker(); var s2 = seat("LT-2", "LUCID", "EVAL", b2, 20, 0); s2.WantAuto = true; s2.SlotTime = 830; s2.Plan.Times = new[] { 200, 830 }; s.Plan.Times = new[] { 200, 830 }; ap.Seats.Add(s2); s2.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap, d0.AddHours(2), 20, 20000); int sentBefore = b.Sent.Count;
                ap.Stop("⛔ CLOSE ALL POSITIONS", d0.AddHours(3)); run(ap, d0.AddHours(3), 10, 20000); run(ap, d0.AddHours(8).AddMinutes(30), 30, 20000); run(ap, d0.AddDays(1).AddHours(2), 30, 20000);
                Check(!ap.Running && !s.Link.Armed && !s2.Link.Armed && b.Q != 0 && b.Sent.Count == sentBefore && !b.Sent.Contains("FLATTEN") && b.All.Count(o => !o.Done && (o.Kind == "STOP" || o.Kind == "LIMIT")) == 2 && markets(b2) == 0,
                    "🤖 ⛔ CLOSE ALL / STOP: the autopilot stops, its links are disarmed, nothing more is sent (8:30 / tomorrow) — the open position keeps its broker STOP + TARGET", sentBefore + " vs " + b.Sent.Count + " / " + string.Join(" | ", b2.Sent));
                var b3 = new FakeBroker(); var ap3 = new KeystoneAutopilot(); var s3 = seat("LT-3", "LUCID", "EVAL", b3, 20, 0); s3.WantAuto = true; ap3.Seats.Add(s3); s3.Link.Arm(d0);
                ap3.Start("START", d0.AddHours(1).AddMinutes(58)); s3.Link.Disarm("YOU DISARMED", d0.AddHours(1).AddMinutes(59), false); run(ap3, d0.AddHours(2), 30, 20000);
                Check(markets(b3) == 0 && s3.Out, "🤖 DISARM on an account: the autopilot sends nothing there", s3.State);
                var b4 = new FakeBroker(); var ap4 = new KeystoneAutopilot(); var s4 = seat("LT-4", "LUCID", "EVAL", b4, 20, 0); s4.WantAuto = true; ap4.Seats.Add(s4); s4.Link.Arm(d0);
                ap4.Start("START", d0.AddHours(1).AddMinutes(58)); s4.Link.Locked = true; run(ap4, d0.AddHours(2), 30, 20000);
                Check(!ap4.Running && markets(b4) == 0 && ap4.StopWhy.StartsWith("LOCK FOR TODAY"), "🤖 LOCK FOR TODAY stops the autopilot", ap4.Status);
            }
            // 10. the day-loss limit blocks entries (and caps the stop to the room left today)
            {
                var b = new FakeBroker { Pnl = -600 }; var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 500); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap, d0.AddHours(2), 30, 20000);
                Check(markets(b) == 0 && b.Sent.Count == 0 && s.Out, "🤖 day loss −$600 past the $500 limit: no autopilot entry", s.State + " / " + string.Join(" | ", b.Sent));
                var b2 = new FakeBroker { Pnl = -450 }; var ap2 = new KeystoneAutopilot(); var s2 = seat("LT-2", "LUCID", "EVAL", b2, 20, 500); s2.WantAuto = true; ap2.Seats.Add(s2); s2.Link.Arm(d0);
                ap2.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap2, d0.AddHours(2), 30, 20000);
                Check(markets(b2) == 0 && s2.State.Contains("no room"), "🤖 only $50 of day loss left for MNQ 20: no room for a stop → no entry", s2.State);
                var b3 = new FakeBroker { Pnl = 0 }; var ap3 = new KeystoneAutopilot(); var s3 = seat("LT-3", "LUCID", "FUNDED", b3, 5, 200); ap3.Seats.Add(s3); s3.Link.Arm(d0); s3.SlotTime = 200;
                string w3; var t3 = ap3.MakeTicket(s3, d0.AddHours(2), d0, 20000, out w3);
                Check(t3 != null && t3.StopPts <= KeystoneAutopilot.StopPoints(200, 1, 0.25, 1) + 1e-9 && t3.StopUsd <= 200, "🤖 funded −$300 stop with a $200 day loss limit: the stop is capped inside the day loss room", t3 == null ? w3 : t3.StopPts + " pts $" + t3.StopUsd);
            }
            // 11. a blown account is skipped (💀, out of the autopilot)
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; s.Balance = 47990; ap.Seats.Add(s); s.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap, d0.AddHours(2), 30, 20000);
                Check(markets(b) == 0 && s.Blown && s.State.StartsWith("💀") && ap.Journal.Any(r => r.Event == "BLOWN"), "🤖 balance $47,990 ≤ floor $48,000: 💀 BLOWN, removed — nothing sent", s.State);
                var days = new List<KeystoneLiveDay> { new KeystoneLiveDay { Date = d0.AddDays(-2), StartBal = 50000, EndBal = 51200 }, new KeystoneLiveDay { Date = d0.AddDays(-1), StartBal = 51200, EndBal = 50900 } };
                Check(Math.Abs(KeystoneAutopilot.FloorOf(50000, 2000, false, "LUCID", days, d0) - 49200) < 1e-9 && Math.Abs(KeystoneAutopilot.FloorOf(50000, 2000, true, "LUCID", days, d0) - 49200) < 1e-9 && Math.Abs(KeystoneAutopilot.FloorOf(50000, 2000, true, "LUCID", new List<KeystoneLiveDay> { new KeystoneLiveDay { Date = d0.AddDays(-1), EndBal = 53000 } }, d0) - 50100) < 1e-9,
                    "🤖 the floor trails the best end-of-day balance ($51,200 → $49,200); funded locks at start + $100");
            }
            // 12. the owner limit (👥 KeystoneOwners, 5 per firm per stage by default): the 6th Lucid eval of ME is never traded
            {
                var ap = new KeystoneAutopilot(); var bs = new List<FakeBroker>();
                for (int i = 0; i < 6; i++) { var b = new FakeBroker(); bs.Add(b); var s = seat("LT-" + i, "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0); }
                var bw = new FakeBroker(); var sw = seat("LT-W", "LUCID", "EVAL", bw, 20, 0); sw.Owner = "WIFE"; sw.WantAuto = true; ap.Seats.Add(sw); sw.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap, d0.AddHours(2), 30, 20000);
                Check(bs.Take(5).All(b => markets(b) == 1) && markets(bs[5]) == 0 && ap.Seats[5].OverOwner && ap.Seats[5].State.Contains("limit of 5") && markets(bw) == 1, "🤖 owner limit: ME's 6th Lucid eval is never traded (5 per owner per firm); WIFE's account trades", ap.Seats[5].State);
            }
            // 13. a missed minute gets no late entry (started late; a GUIDED ticket not pressed in 2 minutes)
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                ap.Start("START", d0.AddHours(2).AddMinutes(3)); run(ap, d0.AddHours(2).AddMinutes(3), 60, 20000);
                Check(markets(b) == 0 && s.State.StartsWith("MISSED") && ap.Journal.Any(r => r.Event == "MISSED"), "🤖 AUTO started at 2:03 (> 2 min after 2:00): MISSED, no late entry", s.State);
                var b2 = new FakeBroker(); var ap2 = new KeystoneAutopilot(); var s2 = seat("TPT1", "TPT", "EVAL", b2, 20, 0); ap2.Seats.Add(s2); s2.Link.Arm(d0);
                ap2.Start("", d0.AddHours(1).AddMinutes(58)); run(ap2, d0.AddHours(2), 125, 20000);
                string pr = ap2.Press("TPT1", d0.AddHours(2).AddMinutes(2).AddSeconds(6), 20000);
                Check(markets(b2) == 0 && pr != "" && s2.State.StartsWith("MISSED") && ap2.Flashing().Count == 0, "🤖 GUIDED not pressed within 2 minutes: MISSED — a late press is refused", pr + " / " + s2.State);
            }
            // 14. 16:54 NY: every open position is closed (through the link)
            {
                var b = new FakeBroker(); var ap = new KeystoneAutopilot(); var s = seat("LT-1", "LUCID", "EVAL", b, 20, 0); s.WantAuto = true; ap.Seats.Add(s); s.Link.Arm(d0);
                ap.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap, d0.AddHours(2), 20, 20000);
                run(ap, d0.AddHours(16).AddMinutes(53), 50, 20010); bool openAt1653 = b.Q != 0 && !b.Sent.Contains("FLATTEN");
                run(ap, d0.AddHours(16).AddMinutes(54), 20, 20010);
                Check(openAt1653 && b.Q == 0 && b.Sent.Contains("FLATTEN") && ap.Journal.Any(r => r.Event == "FLATTEN") && ap.Journal.Any(r => r.Event == "EXIT") && markets(b) == 1, "🤖 16:54 NY: the open position is flattened (16:53 still open), the exit journalled", string.Join(" | ", b.Sent));
            }
            // the plan feed, the defaults, target / payout states
            {
                var ev = KeystoneAutoPlan.FromMap(new Dictionary<string, string>(), false, false, "LUCID · EVAL"); var fu = KeystoneAutoPlan.FromMap(new Dictionary<string, string> { { "plan", "🛟 SAFE PAYOUT" }, { "mnq", "1" }, { "tp", "800" }, { "stop", "300" }, { "times", "200 830 930 1000 1100" } }, true, true, "TPT · FUNDED");
                var mine = KeystoneAutoPlan.FromMap(new Dictionary<string, string> { { "plan", "✏ YOUR PLAN" }, { "mnq", "10" }, { "tp", "900" }, { "stop", "400" }, { "times", "930" }, { "side", "LONG" } }, false, false, "GROUP:A");
                Check(ev.Qty == 20 && ev.TpUsd == 1500 && ev.StopUsd == 0 && ev.Times.SequenceEqual(new[] { 200 }) && fu.Qty == 2 && fu.TpUsd == 800 && fu.StopUsd == 300 && mine.Qty == 10 && mine.Side == "BUY" && mine.Times[0] == 930, "🤖 plans: eval default MNQ 20 / +$1,500 / floor stop at 2:00 • TPT PRO 🛟 SAFE PAYOUT = MNQ 2 +$800 / −$300 • ✏ your group's plan is used");
                var fd = new KeystoneAutoFeed { Account = "LT-1", Firm = "LUCID", Stage = "FUNDED", Owner = "WIFE", Group = "LUCID · FUNDED", Nick = "Lucid 1", Start = 50000, Dd = 2000, Target = 3000, MinDays = 2, DaysTraded = 1, Balance = 50400 }; fd.Plan["mnq"] = "1"; fd.Plan["times"] = "200 830";
                var back = KeystoneAutoFeed.Parse(KeystoneAutoFeed.Text(new[] { fd, fd }));
                Check(back.Count == 1 && back[0].Owner == "WIFE" && back[0].Stage == "FUNDED" && back[0].Plan["times"] == "200 830" && back[0].Balance == 50400, "🤖 the feed (✓ CONFIRM → autopilot-feed.txt) is written and read back (one line per account)");
                var ap = new KeystoneAutopilot(); var bs = new List<FakeBroker>();
                for (int i = 0; i < 3; i++) { var b = new FakeBroker(); bs.Add(b); var s = seat("LF-" + i, "LUCID", "FUNDED", b, 5, 0); s.Plan = KeystoneAutoPlan.FundedDefault(false); ap.Seats.Add(s); s.Link.Arm(d0); }
                ap.Start("", d0); Check(ap.Seats[0].SlotTime == 200 && ap.Seats[1].SlotTime == 830 && ap.Seats[2].SlotTime == 930, "🤖 funded: each account its own time (the plan's times spread over the firm's accounts)");
                var bT = new FakeBroker(); var apT = new KeystoneAutopilot(); var sT = seat("TPT1", "TPT", "EVAL", bT, 20, 0); sT.Balance = 53050; sT.MinDays = 3; sT.DaysTraded = 2; apT.Seats.Add(sT); sT.Link.Arm(d0); apT.Start("", d0.AddHours(1));
                run(apT, d0.AddHours(2), 10, 20000);
                Check(sT.Ticket != null && sT.Ticket.Tiny && sT.Ticket.Qty == 1 && sT.Ticket.TpUsd == 50 && !sT.Ticket.Auto && markets(bT) == 0, "🤖 TPT target made on day 2 of 3: the 3rd-day tiny trade is GUIDED (MNQ 1, TP +$50)", sT.State);
                var bL = new FakeBroker(); var apL = new KeystoneAutopilot(); var sL = seat("LT-9", "LUCID", "EVAL", bL, 20, 0); sL.WantAuto = true; sL.Balance = 53100; sL.DaysTraded = 2; apL.Seats.Add(sL); sL.Link.Arm(d0); apL.Start("START", d0.AddHours(1));
                run(apL, d0.AddHours(2), 10, 20000);
                Check(markets(bL) == 0 && sL.State.StartsWith("✅ target reached: wait for the firm"), "🤖 eval target reached (and the days): stop trading it — ✅ wait for the firm", sL.State);
                var sp = seat("LF-P", "LUCID", "FUNDED", new FakeBroker(), 5, 0); sp.Balance = 51200; sp.Days = Enumerable.Range(1, 5).Select(i => new KeystoneLiveDay { Date = d0.AddDays(-i), StartBal = 50000, EndBal = 50200, Trades = 1 }).ToList();
                var sq = seat("TPT-P", "TPT", "FUNDED", new FakeBroker(), 5, 0); sq.Balance = 54100;
                Check(KeystoneAutopilot.PayoutNote(sp) == "💰 payout ready: request $600" && KeystoneAutopilot.PayoutNote(sq) == "💰 payout ready: request $2,000" && KeystoneAutopilot.PayoutNote(seat("E", "LUCID", "EVAL", new FakeBroker(), 5, 0)) == "", "🤖 funded payout eligible → 💰 payout ready: request $x (a reminder only)", KeystoneAutopilot.PayoutNote(sp) + " / " + KeystoneAutopilot.PayoutNote(sq));
            }
            KeystoneFirmPolicies.Chosen.Clear();
        }
        // ================= 🎮 SIM ACCOUNTS + 🎯 LIVE PROP PRACTICE (BUILD 2026-11-11i) =================
        {
            var menu = KeystoneSimAccounts.MenuOrder(new[] { Tuple.Create("Sim101", true, 0.0), Tuple.Create("TAKEPROFIT893", true, 120.0), Tuple.Create("LT-77", true, 300.0), Tuple.Create("DEMO4", false, 0.0), Tuple.Create("Playback101", true, 0.0) });
            Check(string.Join("|", menu) == "LT-77|TAKEPROFIT893||Playback101|Sim101", "🎮 the account dropdown: prop accounts first, then the 🎮 SIMULATION header and Sim101 / Playback101 (connected only)", string.Join("|", menu));
            Check(KeystoneSimAccounts.IsSim("Sim101") && KeystoneSimAccounts.IsSim("DEMO123") && !KeystoneSimAccounts.IsSim("SIMONE-PA") && !KeystoneSimAccounts.IsSim("TAKEPROFIT893") && KeystoneFirmNames.Detect("Sim101", "TPT_TEST_PRO")[0] == "SIM" && KeystoneFirmNames.Detect("Sim101", "TakeProfitTrader")[0] == "SIM",
                "🎮 Sim101 is SIM by its name, whatever its connection (TPT_TEST_PRO / TakeProfitTrader)");
            var raws = new[] { new KeystoneLiveRaw { Name = "Sim101", Connection = "TPT_TEST_PRO", Balance = 2830488.5 }, new KeystoneLiveRaw { Name = "TAKEPROFIT893", Connection = "TPT", Balance = 53000 } };
            var cls = KeystoneLabLive.Classify(raws, null, null);
            Check(cls.Count == 1 && cls[0].Account == "TAKEPROFIT893" && !KeystoneLabLive.IsProp("SIM"), "🎮 Sim101 ($2.8M) stays OUT of the prop logic (labs' counts → the autopilot feed, copier / rotation pools)", string.Join(",", cls.Select(a => a.Account)));
            Check(KeystoneSimAccounts.PracticeOffered("Sim101", "TPT_TEST_PRO", null) && KeystoneSimAccounts.PracticeOffered("DEMO4", "", null) && !KeystoneSimAccounts.PracticeOffered("TAKEPROFIT893", "", null) && !KeystoneSimAccounts.PracticeOffered("LT-77", "", null) && !KeystoneSimAccounts.PracticeOffered("Sim101", "", "TPT"),
                "🎯 the live practice overlay is offered ONLY on a simulation account (never a prop account, never a Sim set to a prop firm)");
            // TPT PRO 50K funded on live updates: the INTRADAY peak includes the open profit seen at the bar's high, the floor trails it, the lock at +$2,000
            var pr = KeystonePractice.Presets().First(x => x.Name.StartsWith("TPT PRO 50K funded")).Copy(); pr.ApplyTyped(); var led = new KeystonePracticeLedger();
            double realized = 1234.5; led.Rebase(realized);   // the Sim account had already made $1,234.50 today: the practice starts at 0
            Check(Math.Abs(led.Closed(realized)) < 1e-9 && pr.Mark(led.Closed(realized), 0, "start") == null, "🎯 the practice start re-bases on the Sim's realized P/L (a ledger — the Sim account is untouched)");
            pr.Mark(led.Closed(realized), 300, "open +300"); pr.SeeBest(led.Closed(realized), 900);   // a live tick: +300 open, the bar's high showed +900
            Check(Math.Abs(pr.PeakNow(300) - 900) < 1e-9 && Math.Abs(pr.FloorNow(300) - (900 - 2000)) < 1e-9 && !pr.Locked, "🎯 the INTRADAY peak includes the open profit from the live updates (+$900 at the bar's high → floor −$1,100)", pr.PeakNow(300) + " / " + pr.FloorNow(300));
            realized += 1500; string h1 = pr.Mark(led.Closed(realized), 0, "closed +1500");
            pr.SeeBest(led.Closed(realized), 2100); string h2 = pr.Mark(led.Closed(realized), 2100, "open +2100");
            Check(h1 == null && h2 == "LOCKED" && pr.Locked && Math.Abs(pr.FloorNow(0) - 0) < 1e-9 && pr.Mark(led.Closed(realized), 2100, "again") == null, "🎯 the 🔒 LOCK transition once the peak reaches +$2,000 above the trailing (floor locked at the start), reported once", h1 + " / " + h2 + " / floor " + pr.FloorNow(0));
            realized += 2100; pr.Mark(led.Closed(realized), 0, "closed +2100");   // flat at +$3,600 → withdrawable = above the $2,000 buffer, flat only
            Check(Math.Abs(pr.CanWithdraw(0) - 1600) < 1e-9 && pr.CanWithdraw(50) == 0, "🎯 \"withdrawable now\" = only above the $2,000 buffer ($3,600 → $1,600), flat only", pr.CanWithdraw(0) + " / " + pr.CanWithdraw(50));
            double got = pr.Withdraw(0, 1000); Check(Math.Abs(got - 1000) < 1e-9 && pr.Withdrawn >= 1000, "🎯 💰 a practice withdrawal is booked on the practice ledger only", got + "");
            realized += 3000; led.Rebase(realized); var fresh = pr.Copy();
            Check(Math.Abs(led.Closed(realized)) < 1e-9 && fresh.Mark(led.Closed(realized), 0, "reset") == null && !fresh.Locked && fresh.Peak == 0, "🎯 RESET re-bases the ledger on the Sim's realized now: a fresh attempt at 0");
        }
        // ================= 🔒 NINJATRADER LOCKOUT (BUILD 2026-11-11i): read by reflection; entries refused, closes go, the autopilot blocked =================
        {
            KeystoneNtLockout.Clear(); KeystoneFirmPolicies.Chosen.Clear();
            var nowL = DateTime.Now;
            var fut = new StubNtAccount { Name = "TAKEPROFIT893", LockedUntil = nowL.AddHours(3) }; var past = new StubNtAccount { Name = "TAKEPROFIT893", LockedUntil = nowL.AddHours(-3) };
            var rf = KeystoneNtLockout.Read(fut, nowL); var rp = KeystoneNtLockout.Read(past, nowL);
            Check(rf.Locked && rf.Until == fut.LockedUntil && rf.Source == "LockedUntil" && rf.Label.StartsWith("🔒 LOCKED by NinjaTrader until ") && rf.Members.Contains("LockedUntil"), "🔒 LockedUntil in the future → locked until that time (label " + rf.Label + ")", rf.Members);
            Check(!rp.Locked, "🔒 LockedUntil in the past → not locked", rp.Members);
            Check(new KeystoneNtLock { Locked = true, Until = new DateTime(2026, 11, 9, 17, 0, 0) }.Label == "🔒 LOCKED by NinjaTrader until 5:00 PM", "🔒 the label reads like NinjaTrader's column: \"until 5:00 PM\"");
            var nested = new StubNtRiskAccount(); nested.Risk.IsLocked = true; var rn = KeystoneNtLockout.Read(nested, nowL);
            var nul = new StubNtNullable { LockoutTime = nowL.AddMinutes(30) }; var rnl = KeystoneNtLockout.Read(nul, nowL); var rnn = KeystoneNtLockout.Read(new StubNtNullable(), nowL);
            Check(rn.Locked && rn.Source == "Risk.IsLocked" && rnl.Locked && rnl.Source == "LockoutTime" && !rnn.Locked && !KeystoneNtLockout.Read(new StubNtAccount { Name = "X" }, nowL).Locked,
                "🔒 robust: a bool IsLocked on a related Risk object, a nullable DateTime LockoutTime (set / null), an unset (MinValue) time", rn.Source + " / " + rnl.Source);
            Check(!KeystoneNtLockout.Read(new StubNtSettings { AllowUnlock = true, LockFlatten = true }, nowL).Locked, "🔒 not a lock: AllowUnlock / LockFlatten switches are ignored");
            Check(KeystoneNtLockout.IsEntry(0, 1) && KeystoneNtLockout.IsEntry(2, 1) && KeystoneNtLockout.IsEntry(2, -3) && !KeystoneNtLockout.IsEntry(2, -2) && !KeystoneNtLockout.IsEntry(-3, 1), "🔒 entry = opens, adds or reverses; a reduce / close is not");

            // the link: ARMED, then NinjaTrader locks it → disarmed, every entry refused (no order reaches the broker), a full close still goes
            {
                var b = new FakeBroker(); var l = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 5, AccountName = "TAKEPROFIT893" }; var t = Trader();
                Check(l.Arm(t0) == null && l.Armed, "🔒 unlocked: ARM works");
                KeystoneNtLockout.Set("TAKEPROFIT893", rf); l.Tick(t, t0);
                Check(!l.Armed && l.ExternalLocked && l.State.StartsWith("🔒 LOCKED by NinjaTrader until"), "🔒 locked by NinjaTrader → the link disarms itself and shows the lock (not ARMED)", l.State);
                t.Buy(2); l.Tick(t, t0.AddSeconds(1)); l.Tick(t, t0.AddSeconds(2));
                var pl = t.Place("LIMIT", 1, 2, 19990); l.Tick(t, t0.AddSeconds(3));
                Check(b.Sent.Count == 0 && t.Position == 0 && !t.Orders.Contains(pl) && l.Warning.Contains("ENTRIES REFUSED"), "🔒 every ENTRY refused locally: a market BUY and a BUY LIMIT never reach the broker", string.Join(" | ", b.Sent) + " / " + l.Warning);
                Check(l.Arm(t0.AddSeconds(4)) != null && !l.Armed, "🔒 ARM is refused while NinjaTrader keeps it locked", l.Arm(t0.AddSeconds(4)) ?? "");
                b.External("MARKET", 2, 0); b.Exec(2, 20000, "NT"); l.Tick(t, t0.AddSeconds(5));
                Check(t.Position == 2, "🔒 a position (opened before / in NinjaTrader) shows on the chart", t.Position.ToString());
                t.Buy(1); l.Tick(t, t0.AddSeconds(6)); bool addRefused = !b.Sent.Any(x => x.StartsWith("MARKET")) && t.Position == 2;
                t.Sell(2); l.Tick(t, t0.AddSeconds(7));
                Check(addRefused && b.Sent.Contains("FLATTEN") && b.Q == 0, "🔒 adding is refused, CLOSING goes (a full close → FLATTEN at the broker)", string.Join(" | ", b.Sent));
                // the lock ends: shown as unlocked, NOT re-armed by itself
                KeystoneNtLockout.Set("TAKEPROFIT893", rp); l.Tick(t, t0.AddSeconds(30));
                Check(!l.ExternalLocked && !l.Armed && KeystoneNtLockout.Change("TAKEPROFIT893") == 0, "🔒 the lock ended (time in the past): the link is unlocked but NOT re-armed by itself", l.State);
                KeystoneNtLockout.Set("TAKEPROFIT893", rf); Check(KeystoneNtLockout.Change("X-NONE") == 0 && KeystoneNtLockout.Change("TAKEPROFIT893") == 1 && KeystoneNtLockout.Change("TAKEPROFIT893") == 0, "🔒 Change(): +1 once when the lock starts");
                KeystoneNtLockout.Set("TAKEPROFIT893", rp); Check(KeystoneNtLockout.Change("TAKEPROFIT893") == -1, "🔒 Change(): −1 once when it ends (→ \"unlocked\" shown)");
            }
            // the 🤖 autopilot: a locked account gets no ticket (journalled "locked by NinjaTrader"); unlocked (past) → the normal 2:00 entry
            {
                var d0 = new DateTime(2026, 10, 6);
                Func<string, FakeBroker, KeystoneAutoSeat> seat = (acc, b) => new KeystoneAutoSeat { Account = acc, Nick = acc, Firm = "LUCID", Stage = "EVAL", Owner = "ME", Group = "L", Plan = KeystoneAutoPlan.EvalDefault(), WantAuto = true,
                    Link = new KeystoneAccountLink(b, "MNQ") { MaxContracts = 20, AccountName = acc }, Mirror = Trader(), Start = 50000, Dd = 2000, Target = 3000, Balance = 50500, MinDays = 2 };
                Action<KeystoneAutopilot, DateTime, int> run = (ap, from, secs) => { for (int i = 0; i <= secs; i++) { ap.Tick(from.AddSeconds(i), 20000); foreach (var s in ap.Seats) ((FakeBroker)s.Link.Broker).FillMarket(20000); } };
                // locked before START
                KeystoneNtLockout.Set("LT-LOCK", rf); KeystoneNtLockout.Set("LT-OPEN", rp);
                var bA = new FakeBroker(); var bB = new FakeBroker(); var ap1 = new KeystoneAutopilot(); var sA = seat("LT-LOCK", bA); var sB = seat("LT-OPEN", bB); ap1.Seats.Add(sA); ap1.Seats.Add(sB);
                sA.Link.Arm(d0); sB.Link.Arm(d0); string e = ap1.Start("START", d0.AddHours(1).AddMinutes(58)); run(ap1, d0.AddHours(2), 20);
                Check(e == "" && ap1.Running && sA.Out && sA.Ticket == null && bA.Sent.Count == 0 && ap1.Journal.Any(r => r.Account == "LT-LOCK" && r.Event == "LOCKED" && r.Detail.Contains("locked by NinjaTrader")),
                    "🔒🤖 locked by NinjaTrader at START: no ticket, nothing sent, journalled \"locked by NinjaTrader\"", string.Join(" | ", bA.Sent) + " / " + sA.State);
                Check(bB.Sent.Count(x => x.StartsWith("MARKET")) == 1 && sB.Placed != null, "🔒🤖 the unlocked account (lock time in the past) trades normally at 2:00", string.Join(" | ", bB.Sent));
                // locked while running, before the entry minute → blocked, journalled once, the whole autopilot keeps running
                KeystoneNtLockout.Set("LT-LATE", null); var bC = new FakeBroker(); var ap2 = new KeystoneAutopilot(); var sC = seat("LT-LATE", bC); ap2.Seats.Add(sC); sC.Link.Arm(d0);
                ap2.Start("START", d0.AddHours(1).AddMinutes(50)); run(ap2, d0.AddHours(1).AddMinutes(55), 5);
                KeystoneNtLockout.Set("LT-LATE", rf); run(ap2, d0.AddHours(1).AddMinutes(59), 200);
                Check(ap2.Running && bC.Sent.Count == 0 && sC.Ticket == null && !sC.Link.Armed && ap2.Journal.Count(r => r.Event == "LOCKED" && r.Detail.Contains("locked by NinjaTrader")) == 1 && sC.State.StartsWith("🔒 LOCKED by NinjaTrader"),
                    "🔒🤖 locked by NinjaTrader while running: no ticket at 2:00, journalled once, the autopilot keeps running for the others", string.Join(",", ap2.Journal.Select(r => r.Event)) + " / " + sC.State);
                KeystoneNtLockout.Set("LT-LATE", rp); run(ap2, d0.AddHours(2).AddMinutes(5), 3);
                Check(ap2.Journal.Any(r => r.Event == "UNLOCKED") && !sC.Link.Armed && bC.Sent.Count == 0, "🔒🤖 the lock ends: journalled UNLOCKED, NOT re-armed by itself, nothing sent", string.Join(",", ap2.Journal.Select(r => r.Event)));
            }
            KeystoneNtLockout.Clear();
        }
        Console.WriteLine(failures == 0 ? "ALL ACCOUNT LINK TESTS PASSED" : failures + " ACCOUNT LINK TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
