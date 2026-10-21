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

public static class AccountLinkTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static KeystoneReplayTrader Trader() { var bars = new List<KeystoneArcBar> { new KeystoneArcBar { Symbol = "MNQ", Time = new DateTime(2026, 10, 6, 9, 45, 0), Open = 20000, High = 20001, Low = 19999, Close = 20000 } }; var t = KeystoneReplayTrader.LiveStart(bars, "MNQ", new DateTime(2026, 10, 6, 9, 45, 0)); t.Slave = true; return t; }

    public static int Main()
    {
        var t0 = new DateTime(2026, 10, 6, 9, 45, 0);
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
            var b2 = new FakeBroker(); var l2 = new KeystoneAccountLink(b2, "MNQ") { DayProfitCap = 900 }; var t2 = Trader(); l2.Arm(t0); b2.Exec(1, 20000, "Buy"); b2.Pnl = 910; l2.Tick(t2, t0);
            Check(!l2.Armed && b2.Sent.Contains("FLATTEN"), "day profit cap $900 (consistency) → flatten + done for today");
            var b3 = new FakeBroker(); var l3 = new KeystoneAccountLink(b3, "MNQ") { MaxContracts = 5 }; var t3 = Trader(); l3.Arm(t0);
            for (int i = 0; i < 12 && l3.Armed; i++) { t3.Place("LIMIT", 1, 1, 19900 - i); l3.Tick(t3, t0.AddSeconds(i)); }
            Check(!l3.Armed && l3.State.Contains("RUNAWAY"), "runaway guard: 8 orders in a minute → disarm", l3.State);
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
            gs[0].Confirmed = true; var gs3 = KeystoneCopyGroups.Parse(KeystoneCopyGroups.Serialize(gs)); Check(gs3[0].Confirmed && !gs3[1].Confirmed, "✓ CONFIRMED saves and loads");
            KeystoneCopyGroups.Place(gs3, "ASIAN", "TPT1", true); Check(!gs3[0].Confirmed && gs3[0].Leader == "TPT1", "another account on the chart → the group must be confirmed again");
            gs3[0].Confirmed = true; KeystoneCopyGroups.Place(gs3, "ASIAN", "TPT9", false); Check(!gs3[0].Confirmed, "an account added → confirm again");
            gs3[0].Confirmed = true; KeystoneCopyGroups.Remove(gs3, "TPT9"); Check(!gs3[0].Confirmed, "an account taken out → confirm again");
            gs3[0].Confirmed = true; KeystoneCopyGroups.Place(gs3, "ASIAN", "TPT1", true); Check(gs3[0].Confirmed, "the same chart account again → still confirmed");
            KeystoneCopyGroups.Remove(gs2, "TPT2"); Check(gs2[0].Leader == "" && KeystoneCopyGroups.GroupOf(gs2, "TPT2") == null && KeystoneCopyGroups.Place(gs2, "GROUP 1", "TPT2", true) == null, "taken out → free to join another group");
            Check(!KeystoneFirmPolicies.AutoAllowed("TPT", true) && KeystoneFirmPolicies.AutoAllowed("TPT", false) && KeystoneFirmPolicies.CopyAllowed("TPT"), "TPT: automation in the evaluation only, copying allowed");
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
        Console.WriteLine(failures == 0 ? "ALL ACCOUNT LINK TESTS PASSED" : failures + " ACCOUNT LINK TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
