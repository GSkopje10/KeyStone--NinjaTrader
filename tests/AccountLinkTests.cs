// ACCOUNT LINK: the studio's live chart = the real account (a fake broker). Trades placed in "NinjaTrader" appear in the studio;
// studio actions become real orders only while armed; fills become trades; limits; plus the prop presets and the account watch.
using System;
using System.Collections.Generic;
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
        }
        // 10. presets + account watch
        {
            var p = KeystoneFirmRules.Guess("MFFUEVREOD723518001");
            Check(p != null && p.Dd == 2000 && p.Target == 3000 && p.ConsistencyPct == 40 && p.DayProfitCap == 900 && p.PayoutBuffer == 2100 && p.MaxMicros == 30, "MFFUEVREOD… → MFFU RAPID EOD 50K ($2,000 EOD, $3,000 target, 40% → $900 a day, 30 micros, first payout at +$2,100)");
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
