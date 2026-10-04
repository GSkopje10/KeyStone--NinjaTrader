// ORDER ROUTER: the studio's live position mirrored onto a (fake) broker — arming, entries, the real OCO bracket, stop moves,
// real exits followed by the studio, safety limits — plus the prop presets and the account watch (EOD floor, consistency).
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public sealed class FakeBroker : IKeystoneBroker
{
    public bool Conn = true; public int Q; public double Avg, Pnl, Last = 20000; int seq;
    public List<string> Sent = new List<string>();
    readonly List<KeystoneBrokerOrder> orders = new List<KeystoneBrokerOrder>();
    public bool Connected { get { return Conn; } }
    public int Qty { get { return Q; } }
    public double AvgPrice { get { return Avg; } }
    public double DayPnl { get { return Pnl; } }
    public double Tick { get { return 0.25; } }
    public List<KeystoneBrokerOrder> Orders { get { return orders; } }
    public KeystoneBrokerOrder Submit(string role, string kind, int signedQty, double price, string oco, bool automated)
    {
        var o = new KeystoneBrokerOrder { Id = "O" + (++seq), Role = role, Qty = Math.Abs(signedQty), Price = price, Oco = oco };
        o.Native = signedQty; orders.Add(o); Sent.Add(role + " " + kind + " " + signedQty + (kind == "MARKET" ? "" : " @ " + price) + (oco != "" ? " oco" : "") + (automated ? " auto" : ""));
        return o;
    }
    public void ChangePrice(KeystoneBrokerOrder o, double price) { o.Price = price; Sent.Add("CHANGE " + o.Role + " @ " + price); }
    public void Cancel(KeystoneBrokerOrder o)
    {
        Sent.Add("CANCEL " + o.Role); o.CancelSent = true; o.Done = true;
        if (o.Oco != "") foreach (var x in orders.Where(x => x.Oco == o.Oco && !x.Done)) x.Done = true;   // OCO: the other leg goes too
    }
    public void Flatten() { Sent.Add("FLATTEN"); Q = 0; Avg = 0; foreach (var o in orders) o.Done = true; }
    // the exchange fills every working market order at `px`
    public void FillMarket(double px)
    {
        foreach (var o in orders.Where(x => x.Role == "ENTRY" && !x.Done).ToList())
        {
            int s = (int)o.Native; int nq = Q + s;
            if (nq != 0 && Math.Sign(nq) == Math.Sign(s) && Q != 0 && Math.Sign(Q) == Math.Sign(s)) Avg = (Avg * Math.Abs(Q) + px * Math.Abs(s)) / Math.Abs(nq);
            else if (nq != 0 && (Q == 0 || Math.Sign(nq) != Math.Sign(Q))) Avg = px;
            Q = nq; if (Q == 0) Avg = 0; o.Filled = o.Qty; o.Done = true;
        }
    }
    public void FillBracket(string role)
    {
        var o = orders.First(x => x.Role == role && !x.Done); o.Filled = o.Qty; o.Done = true; Q = 0; Avg = 0;
        if (o.Oco != "") foreach (var x in orders.Where(x => x.Oco == o.Oco && !x.Done)) x.Done = true;
    }
}

public static class OrderRouterTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    public static int Main()
    {
        var t = new DateTime(2026, 10, 6, 9, 45, 0);
        // 1. arming rules
        {
            var b = new FakeBroker(); var r = new KeystoneOrderRouter(b);
            r.Sync(1, 10, 20, t); Check(b.Sent.Count == 0, "not armed → nothing is sent");
            Check(r.Arm(1, t) != null && !r.Armed, "arm refused while the studio holds a position");
            b.Q = 2; Check(r.Arm(0, t) != null && !r.Armed, "arm refused while the real account holds a position"); b.Q = 0;
            b.Conn = false; Check(r.Arm(0, t) != null, "arm refused while disconnected"); b.Conn = true;
            Check(r.Arm(0, t) == null && r.Armed, "arm accepted when both are flat");
        }
        // 2. entry → bracket → stop move → studio exit
        {
            var b = new FakeBroker(); var r = new KeystoneOrderRouter(b) { MaxContracts = 3 }; r.Arm(0, t);
            r.Sync(2, 10, 20, t);
            Check(b.Sent.SequenceEqual(new[] { "ENTRY MARKET 2" }), "studio long 2 → one market buy of 2", string.Join(" | ", b.Sent));
            r.Sync(2, 10, 20, t.AddSeconds(0.25)); Check(b.Sent.Count == 1, "order in flight → nothing more is sent");
            b.FillMarket(20000.50); b.Sent.Clear(); r.Sync(2, 10, 20, t.AddSeconds(1));
            Check(b.Sent.SequenceEqual(new[] { "STOP STOP -2 @ 19990.5 oco", "TARGET LIMIT -2 @ 20020.5 oco" }), "after the fill: real stop + target, one-cancels-other, from the REAL fill price", string.Join(" | ", b.Sent));
            b.Sent.Clear(); r.Sync(2, 10, 20, t.AddSeconds(2)); Check(b.Sent.Count == 0, "bracket in place → nothing resent");
            r.Sync(2, -1, 20, t.AddSeconds(3)); Check(b.Sent.SequenceEqual(new[] { "CHANGE STOP @ 20001.5" }), "stop dragged past the entry (profit stop) → the real stop is changed", string.Join(" | ", b.Sent));
            b.Sent.Clear(); r.Sync(0, 0, 0, t.AddSeconds(4));
            Check(b.Sent.SequenceEqual(new[] { "FLATTEN" }), "studio flat → FLATTEN (cancels the bracket and closes in one step)", string.Join(" | ", b.Sent));
            b.Sent.Clear(); r.Sync(0, 0, 0, t.AddSeconds(5)); Check(b.Q == 0 && r.State == "ARMED • FLAT" && b.Sent.Count == 0, "flat again, nothing left working", r.State);
        }
        // 3. the real stop fills first → the studio is told, the router does not re-enter
        {
            var b = new FakeBroker(); var r = new KeystoneOrderRouter(b); string exit = null; r.RealExit = x => exit = x; r.Arm(0, t);
            r.Sync(-1, 8, 16, t); b.FillMarket(20000); r.Sync(-1, 8, 16, t.AddSeconds(1));
            b.FillBracket("STOP"); b.Sent.Clear(); r.Sync(-1, 8, 16, t.AddSeconds(2));
            Check(exit == "STOP", "real stop filled → the studio is told to close");
            Check(!b.Sent.Any(x => x.StartsWith("ENTRY")), "the router does not re-enter while the studio still shows the position", string.Join(" | ", b.Sent));
            r.Sync(0, 0, 0, t.AddSeconds(3)); r.Sync(1, 8, 16, t.AddSeconds(4)); Check(b.Sent.Contains("ENTRY MARKET 1"), "a NEW studio trade after flat is routed again", string.Join(" | ", b.Sent));
        }
        // 3b. scale in 1 → 2: the bracket is cancelled first, the market order only after the cancel is confirmed
        {
            var b = new FakeBroker(); var r = new KeystoneOrderRouter(b) { MaxContracts = 4 }; r.Arm(0, t);
            r.Sync(1, 10, 20, t); b.FillMarket(20000); r.Sync(1, 10, 20, t.AddSeconds(1)); b.Sent.Clear();
            r.Sync(2, 10, 20, t.AddSeconds(2));
            Check(b.Sent.SequenceEqual(new[] { "CANCEL STOP" }), "scale in: bracket cancelled first (OCO takes the target too), no market order yet", string.Join(" | ", b.Sent));
            r.Sync(2, 10, 20, t.AddSeconds(3)); Check(b.Sent.Last() == "ENTRY MARKET 1", "scale in: then buy 1 more", string.Join(" | ", b.Sent));
            b.FillMarket(20004); b.Sent.Clear(); r.Sync(2, 10, 20, t.AddSeconds(4));
            Check(b.Sent.SequenceEqual(new[] { "STOP STOP -2 @ 19992 oco", "TARGET LIMIT -2 @ 20022 oco" }), "scale in: new bracket for 2 from the average price", string.Join(" | ", b.Sent));
        }
        // 4. safety: max contracts, day loss limit, runaway guard, rejection
        {
            var b = new FakeBroker(); var r = new KeystoneOrderRouter(b) { MaxContracts = 2 }; r.Arm(0, t);
            r.Sync(5, 10, 0, t); Check(b.Sent[0] == "ENTRY MARKET 2", "studio 5 → capped at MAX CONTRACTS 2", b.Sent[0]);
            b.FillMarket(20000); b.Pnl = -600; string why = null; r.Disarmed = x => why = x; r.Sync(5, 10, 0, t.AddSeconds(1));
            Check(!r.Armed && b.Sent.Contains("FLATTEN") && why.StartsWith("DAY LOSS"), "day loss limit $500 hit → flatten + disarm", why);

            var bc = new FakeBroker(); var rc = new KeystoneOrderRouter(bc) { DayProfitCap = 900 }; rc.Arm(0, t); rc.Sync(1, 10, 0, t); bc.FillMarket(20000); bc.Pnl = 905; rc.Sync(1, 10, 0, t.AddSeconds(1));
            Check(!rc.Armed && bc.Sent.Contains("FLATTEN") && rc.State.Contains("PROFIT CAP"), "day profit cap $900 (consistency) → flatten + done for today", rc.State);

            var b2 = new FakeBroker(); var r2 = new KeystoneOrderRouter(b2) { MaxContracts = 5 }; r2.Arm(0, t);
            for (int i = 0; i < 7; i++) { r2.Sync(i % 2 == 0 ? 1 : 0, 0, 0, t.AddSeconds(i * 5)); b2.FillMarket(20000); }
            Check(!r2.Armed && r2.State.Contains("RUNAWAY"), "runaway guard: 6 entries in a minute → disarm", r2.State);

            var b3 = new FakeBroker(); var r3 = new KeystoneOrderRouter(b3); r3.Arm(0, t); r3.Sync(1, 0, 0, t);
            b3.Orders[0].Rejected = true; b3.Orders[0].Done = true; r3.Sync(1, 0, 0, t.AddSeconds(1));
            Check(!r3.Armed && r3.State.Contains("REJECTED"), "a rejected entry disarms", r3.State);
        }
        // 5. minimum hold (Lucid 11-second rule): no exit and no target before 11 s; the stop is there at once
        {
            var b = new FakeBroker(); var r = new KeystoneOrderRouter(b) { MinHoldSeconds = 11 }; r.Arm(0, t);
            r.Sync(1, 10, 20, t); b.FillMarket(20000); b.Sent.Clear(); r.Sync(1, 10, 20, t.AddSeconds(1));
            Check(b.Sent.SequenceEqual(new[] { "STOP STOP -1 @ 19990" }), "min hold: only the stop at first", string.Join(" | ", b.Sent));
            b.Sent.Clear(); r.Sync(0, 0, 0, t.AddSeconds(5)); Check(b.Sent.Count == 0 && r.State.Contains("HOLDING"), "min hold: a studio exit after 5 s waits", r.State);
            r.Sync(0, 0, 0, t.AddSeconds(12)); Check(b.Sent.Contains("FLATTEN"), "min hold: exit sent once 11 s passed", string.Join(" | ", b.Sent));
        }
        // 6. AUTO orders carry the automated flag (Take Profit Trader refuses automation)
        {
            var b = new FakeBroker(); var r = new KeystoneOrderRouter(b) { Automated = true }; r.Arm(0, t); r.Sync(1, 0, 0, t);
            Check(b.Sent[0].EndsWith("auto"), "AUTO orders are sent as automated", b.Sent[0]);
            Check(!KeystoneFirmRules.Find("TAKE PROFIT TRADER 50K").Automation, "TAKE PROFIT TRADER preset: no automation");
        }
        // 7. presets + account watch
        {
            var p = KeystoneFirmRules.Guess("MFFUEVREOD723518001");
            Check(p != null && p.Dd == 2000 && p.Target == 3000 && p.ConsistencyPct == 40 && p.DayProfitCap == 900 && p.PayoutBuffer == 2100 && p.MaxMicros == 30, "MFFUEVREOD… → MFFU RAPID EOD 50K ($2,000 EOD, $3,000 target, 40% → $900 a day, 30 micros, first payout at +$2,100)");
            Check(KeystoneFirmRules.Guess("Sim101").Firm == "SIM", "Sim101 → practice preset");
            var w = new KeystoneAccountWatch { Account = "X" }; var d0 = new DateTime(2026, 10, 5);
            w.Observe(d0, 50000); w.Observe(d0, 50800); w.Observe(d0.AddDays(1), 51200); w.Observe(d0.AddDays(2), 50900);
            Check(Math.Abs(w.PeakEod(d0.AddDays(2)) - 51200) < 1e-9 && Math.Abs(w.Floor(d0.AddDays(2), 2000, true) - 49200) < 1e-9, "EOD floor = highest close before today − $2,000");
            Check(Math.Abs(w.ConsistencyPct() - 100.0 * 800 / 900) < 1e-6, "consistency = best day ÷ total profit", w.ConsistencyPct().ToString());
            var w2 = KeystoneAccountWatch.Parse(w.Serialize()); Check(w2 != null && w2.Closes.Count == 3 && w2.StartBalance == 50000, "account watch saves and loads");
        }
        Console.WriteLine(failures == 0 ? "ALL ORDER ROUTER TESTS PASSED" : failures + " ORDER ROUTER TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
