// 📊 BUILD 2026-11-12v — the calm ORDERFLOW: the FLOW COMPASS (hysteresis), the story, the card gate + hold (closed candles only, one setup at a time, fixed
// levels, first-touch resolution, every removal with its reason), the verdict (no flicker, TAKE only with a card), who's doing what (per-side averages), the trapped
// rule, big prints, key levels, the 📒 setup book (outcomes, no look-ahead), 🧠 learn (learning under 30, history says skip), the account fit + ranker, the export.
// Display / analysis only: nothing here sends, sizes or places an order.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class FlowCompassTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    static readonly DateTime T0 = new DateTime(2026, 11, 12, 10, 0, 0);
    // one trade a second: share = the chance it is a buy; drift = ticks per trade the price moves
    static List<KeystoneOfTick> Flow(DateTime from, int seconds, double share, double drift, double px0, int seed, double tick = 0.25)
    {
        var r = new Random(seed); var o = new List<KeystoneOfTick>(); double px = px0;
        for (int i = 0; i < seconds; i++) { px += drift * tick; int side = r.NextDouble() < share ? 1 : -1; o.Add(new KeystoneOfTick { Time = from.AddSeconds(i), Price = Math.Round(px / tick) * tick, Size = 1 + r.Next(4), Side = side, Quoted = true }); }
        return o;
    }
    static KeystoneFootprint Fp(DateTime s) { return new KeystoneFootprint { Start = s, End = s.AddMinutes(1), Open = 100, High = 101, Low = 99, Close = 100, Trades = 5, Buy = 5, Sell = 3 }; }

    public static int Main()
    {
        // ---- 1. the FLOW COMPASS: hysteresis
        {
            var c = new KeystoneFlowCompass(); var noise = Flow(T0, 600, 0.5, 0, 20000, 1); int flips = 0; int last = 0;
            foreach (var x in noise) { c.Feed(x.Time, x.Price, x.Size, x.Side, 0.25); if (c.State != last) { flips++; last = c.State; } }
            Check(flips == 0 && c.State == 0, "noise (50 / 50, flat price, 10 min): never leaves BALANCED", "flips " + flips + " state " + c.State);
            var c2 = new KeystoneFlowCompass(); var warm = Flow(T0, 90, 0.5, 0, 20000, 2); foreach (var x in warm) c2.Feed(x.Time, x.Price, x.Size, x.Side, 0.25);
            var push = Flow(T0.AddSeconds(90), 240, 0.95, 0.3, 20000, 3); DateTime crossed = DateTime.MinValue, flipped = DateTime.MinValue;
            foreach (var x in push) { c2.Feed(x.Time, x.Price, x.Size, x.Side, 0.25); if (crossed == DateTime.MinValue && c2.Score >= 35) crossed = x.Time; if (flipped == DateTime.MinValue && c2.State == 1) flipped = x.Time; }
            Check(flipped != DateTime.MinValue && crossed != DateTime.MinValue && (flipped - crossed).TotalSeconds >= 20, "a sustained one-sided flow flips to BUYERS only after ≥ 20 s past +35", "crossed " + crossed.ToString("HH:mm:ss") + " flipped " + flipped.ToString("HH:mm:ss"));
            Check(c2.Text == "BUYERS IN CONTROL", "… and says BUYERS IN CONTROL");
            // a brief dip to +20 (between exit 15 and enter 35) keeps BUYERS; below +15 → BALANCED (after the minimum hold)
            var c3 = new KeystoneFlowCompass(); foreach (var x in warm.Concat(push)) c3.Feed(x.Time, x.Price, x.Size, x.Side, 0.25);
            DateTime tB = push[push.Count - 1].Time; var mild = Flow(tB.AddSeconds(1), 40, 0.62, 0, push[push.Count - 1].Price, 4); bool kept = true;
            foreach (var x in mild) { c3.Feed(x.Time, x.Price, x.Size, x.Side, 0.25); if (c3.Score >= 15 && c3.State != 1) kept = false; }
            Check(kept, "between +15 and +35 the state is kept (no exit above +15)");
            var sell = Flow(tB.AddSeconds(42), 300, 0.1, -0.3, mild[mild.Count - 1].Price, 5); DateTime toBal = DateTime.MinValue; DateTime held = c3.Since;
            foreach (var x in sell) { c3.Feed(x.Time, x.Price, x.Size, x.Side, 0.25); if (toBal == DateTime.MinValue && c3.State == 0) toBal = x.Time; }
            Check(toBal != DateTime.MinValue && (toBal - held).TotalSeconds >= 60, "exit to BALANCED needs < +15 and the 60-s minimum hold", "held since " + held.ToString("HH:mm:ss") + " balanced " + toBal.ToString("HH:mm:ss"));
            Check(c3.Changes.Count >= 2 && c3.Changes.Zip(c3.Changes.Skip(1), (a, b) => (b.Item1 - a.Item1).TotalSeconds).All(s => s >= 60), "every state is held ≥ 60 s (min hold)");
            Check(c3.State == -1 && c3.Changes.Any(x => x.Item2 == 0), "BUYERS → BALANCED → SELLERS (never straight across)");
            // Sync: incremental = feeding one by one; a new list restarts
            var all = warm.Concat(push).ToList(); var cs = new KeystoneFlowCompass(); for (int n = 10; n <= all.Count; n += 37) cs.Sync(all, n, all[n - 1].Time, 0.25); cs.Sync(all, all.Count, all[all.Count - 1].Time, 0.25);
            Check(cs.State == c2.State && Math.Abs(cs.Score - c2.Score) < 1e-6, "Sync in pieces = the same compass");
        }
        // ---- 2. the story never says both sides control
        {
            bool ok = true; foreach (int st in new[] { -1, 0, 1 }) foreach (int def in new[] { -1, 0, 1 })
            {
                var l = KeystoneFlowStory.Lines(st, st * 50, "3 min", def, 28241, double.NaN, 0, "POC 28250.00", "28230.00"); string all = string.Join(" ", l);
                if (all.Contains("BUYERS IN CONTROL") && all.Contains("SELLERS IN CONTROL")) ok = false;
                if (st == 0 && (all.Contains("BUYERS IN CONTROL") || all.Contains("SELLERS IN CONTROL"))) ok = false;
                if (st != 0 && !all.Contains(KeystoneFlowCompass.StateText(st))) ok = false;
            }
            Check(ok, "the story names only the compass side as in control (never both, none when balanced)");
            var l2 = KeystoneFlowStory.Lines(1, 48, "2 min", -1, 28252, double.NaN, 0, "VAH", "28230.00");
            Check(l2[0].Contains("Buyers are pushing") && l2[0].Contains("sellers are absorbing at 28252.00") && l2[3].Contains("no %"), "who is winning in a sentence + no invented %", l2[0] + " | " + l2[3]);
            var l3 = KeystoneFlowStory.Lines(1, 48, "2 min", 0, double.NaN, 58, 31, "", ""); Check(l3[3].Contains("58 % of 31"), "a % only from the journal (n given)");
        }
        // ---- 3. the gate + hold
        {
            var sg = new KeystoneOfSignal { Bar = 10, Dir = 1, Kind = "STACKED", Price = 100, Time = T0 };
            var good = new KeystoneOfPlan { Dir = 1, Qty = 4, Entry = 100, Stop = 98, Target = 104, StopPts = 2, TargetPts = 4 };
            var bad = new KeystoneOfPlan { Dir = 1, Qty = 4, Entry = 100, Stop = 77.25, Target = 107.5, StopPts = 22.75, TargetPts = 7.5 };
            Check(KeystoneFlowGate.Decide(null, 12, 0, good).Item1 == KeystoneFlowGate.None, "no confirmed setup → no card");
            Check(KeystoneFlowGate.Decide(sg, 10, 0, good).Item1 == KeystoneFlowGate.Forming, "a setup on the forming candle → watching only");
            var rr = KeystoneFlowGate.Decide(sg, 12, 1, bad); Check(rr.Item1 == KeystoneFlowGate.SkipRr && rr.Item2.Contains("reward smaller than risk (0.3)"), "R:R 0.3 → skipped: reward smaller than risk (0.3)", rr.Item2);
            var ag = KeystoneFlowGate.Decide(sg, 12, -1, good); Check(ag.Item1 == KeystoneFlowGate.Against && ag.Item2.StartsWith("against the flow — not offered"), "against the compass → not offered", ag.Item2);
            Check(KeystoneFlowGate.Decide(sg, 12, 0, good).Item2 == "against no one / balanced" && KeystoneFlowGate.Decide(sg, 12, 1, good).Item1 == KeystoneFlowGate.Offer, "with the compass or balanced → offered");
            Check(KeystoneFlowGate.Decide(sg, 12, 1, good, "⚠ history says skip — x").Item1 == KeystoneFlowGate.HistorySkip, "history says skip → not offered");
            Check(KeystoneFlowGate.AgainstPosition(-1, 1) == "⚠ against your open LONG 1 — taking it would close / reverse" && KeystoneFlowGate.AgainstPosition(1, 2) == "" && KeystoneFlowGate.AgainstPosition(1, 0) == "", "⚠ against your open position");
            // a raw signal flickering on the forming candle never produces a card; 50 updates → at most one setup, constant levels
            var h = new KeystoneOfCardHold(); int shown = 0; double st0 = double.NaN, tg0 = double.NaN; bool constant = true; var rnd = new Random(9);
            for (int u = 0; u < 50; u++)
            {
                var raw = new KeystoneOfSignal { Bar = 20, Dir = rnd.Next(2) == 0 ? 1 : -1, Kind = "STACKED", Price = 100 + rnd.Next(5) * 0.25, Time = T0.AddSeconds(u) };
                if (h.Offer(raw, new KeystoneOfPlan { Dir = raw.Dir, Qty = 3, Entry = raw.Price, Stop = raw.Price - raw.Dir * 3, Target = raw.Price + raw.Dir * 6, StopPts = 3, TargetPts = 6 }, "MNQ", T0.AddSeconds(u), 20)) shown++;
            }
            Check(shown == 0 && h.Current == null, "a flickering raw signal on the forming candle (50 updates) never shows a card");
            for (int u = 0; u < 50; u++)
            {
                var conf = new KeystoneOfSignal { Bar = 18 + (u % 2), Dir = u % 3 == 0 ? -1 : 1, Kind = "STACKED", Price = 100 + (u % 4) * 0.25, Time = T0.AddSeconds(u % 5) };
                if (h.Offer(conf, new KeystoneOfPlan { Dir = conf.Dir, Qty = 3, Entry = conf.Price, Stop = conf.Price - conf.Dir * 3, Target = conf.Price + conf.Dir * 6, StopPts = 3, TargetPts = 6 }, "MNQ", T0.AddSeconds(u), 20)) shown++;
                if (h.Current != null) { if (double.IsNaN(st0)) { st0 = h.Current.Stop; tg0 = h.Current.Target; } else if (h.Current.Stop != st0 || h.Current.Target != tg0) constant = false; }
                var chk = h.Check(T0.AddSeconds(u), h.Current != null ? h.Current.Entry : 0, 20, 0); if (chk != null && chk.Item1 != "") constant = false;
            }
            Check(shown == 1 && constant && h.Current.Id == 1, "50 updates with changing signals → ONE setup (#1), its levels never move, not removed by the next tick", "shown " + shown);
            Check(KeystoneOfCardHold.Chip(h.Current, h.Frozen, "MNQ").StartsWith("#1 ") && KeystoneOfCardHold.Chip(h.Current, h.Frozen, "MNQ").Contains("R:R 2.0"), "the chip carries the id + time + R:R", KeystoneOfCardHold.Chip(h.Current, h.Frozen, "MNQ"));
            // the compass turning against it withdraws only after its candle + 1 closed, with the reason
            var w0 = h.Check(T0.AddMinutes(1), h.Current.Entry, 20, -h.Current.Dir); Check(w0.Item1 == "", "the compass against it: kept until the next candle closes");
            var w1 = h.Check(T0.AddMinutes(1), h.Current.Entry, 21, -h.Current.Dir); Check(w1.Item1 == "withdrawn" && w1.Item2.Contains("not offered any more"), "… then withdrawn with its reason", w1.Item2);
            var rec = h.End(T0.AddMinutes(1), w1.Item1, w1.Item2); Check(rec != null && h.Current == null && h.History.Count == 1 && h.LastRemoved.Contains("removed: the compass turned"), "every removal carries a reason (recent setups + STORY)", h.LastRemoved);
            Check(!h.Offer(new KeystoneOfSignal { Bar = 18, Dir = rec.Dir, Kind = "STACKED", Price = rec.Entry, Time = rec.Time }, new KeystoneOfPlan { Dir = rec.Dir, Qty = 3, Entry = 1, Stop = 0.5, Target = 2, StopPts = 0.5, TargetPts = 1 }, "MNQ", T0.AddMinutes(2), 21), "a removed setup never comes back");
            // first-touch resolution: target touched then pull-back = a win; stop likewise; expiry after 10 min
            var h2 = new KeystoneOfCardHold { PointValue = 2 }; var s2 = new KeystoneOfSignal { Bar = 5, Dir = 1, Kind = "ABSORB", Price = 100, Time = T0 };
            h2.Offer(s2, new KeystoneOfPlan { Dir = 1, Qty = 10, Entry = 100, Stop = 98, Target = 104, StopPts = 2, TargetPts = 4 }, "MNQ", T0, 6);
            var path = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0.AddSeconds(5), Price = 102, Size = 1, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(9), Price = 104, Size = 1, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(12), Price = 101, Size = 1, Side = -1 } };
            h2.TouchAll(path, path.Count, T0.AddSeconds(12)); var r2 = h2.Check(T0.AddSeconds(12), 101, 6, 0);
            Check(r2.Item1 == "target" && r2.Item2.StartsWith("✔ TAKE PROFIT HIT +$80"), "the target touched then a pull-back → resolved as a WIN (first touch)", r2.Item2);
            h2.End(T0.AddSeconds(12), r2.Item1, r2.Item2); Check(h2.Current == null && h2.LastResolved != null && h2.LastResolved.Result == "target", "… no card any more, the resolution kept for the fading label");
            var h3 = new KeystoneOfCardHold { PointValue = 2 }; h3.Offer(new KeystoneOfSignal { Bar = 5, Dir = -1, Kind = "STACKED", Price = 100, Time = T0 }, new KeystoneOfPlan { Dir = -1, Qty = 5, Entry = 100, Stop = 102, Target = 96, StopPts = 2, TargetPts = 4 }, "MNQ", T0, 6);
            var p3 = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0.AddSeconds(3), Price = 102.25, Size = 1, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(6), Price = 99, Size = 1, Side = -1 } };
            h3.TouchAll(p3, p3.Count, T0.AddSeconds(6)); var r3 = h3.Check(T0.AddSeconds(6), 99, 6, 0); Check(r3.Item1 == "stopped" && r3.Item2.StartsWith("✖ STOP LOSS HIT −$20"), "the stop touched then back → resolved as a LOSS", r3.Item2);
            {   // the PLANNED $ are fixed from the entry; the drift line; entry missed > 25 % of the stop distance
                var h5 = new KeystoneOfCardHold(); h5.Offer(new KeystoneOfSignal { Bar = 5, Dir = -1, Kind = "STACKED", Price = 28268.75, Time = T0 }, new KeystoneOfPlan { Dir = -1, Qty = 4, Entry = 28268.75, Stop = 28287.75, Target = 28243.25, StopPts = 19, TargetPts = 25.5 }, "MNQ", T0, 6);
                bool same = true; double p0 = KeystoneOfCardHold.PlannedProfit(h5.Current, 2), l0 = KeystoneOfCardHold.PlannedLoss(h5.Current, 2);
                foreach (var px in new[] { 28268.75, 28270, 28260, 28275.5, 28250 }) { h5.Check(T0.AddSeconds(1), px, 6, 0); if (KeystoneOfCardHold.PlannedProfit(h5.Current, 2) != p0 || KeystoneOfCardHold.PlannedLoss(h5.Current, 2) != l0) same = false; }
                Check(same && p0 == 204 && l0 == 152, "the planned $ (+$204 / −$152) never change with the price", p0 + " / " + l0);
                bool m1, m2; string d1 = KeystoneOfCardHold.EntryDrift(h5.Current, 28267.5, out m1), d2 = KeystoneOfCardHold.EntryDrift(h5.Current, 28263, out m2);   /* a SELL: lower = worse */
                Check(!m1 && d1 == "price now 28267.50 (1.25 pts worse than the entry)" && KeystoneOfCardHold.EntryDrift(h5.Current, 28270, out m1) == "price now 28270.00 (1.25 pts better than the entry)" && m2 && d2.StartsWith("⚠ entry missed — WAIT for a return to 28268.75"), "price vs the entry: worse by a little = a note; > 25 % of the stop = entry missed", d1 + " | " + d2);
                Check(KeystoneFlowVerdict.Raw(new KeystoneFlowVerdictInput { CardOffered = true, EntryMissed = true, MissedText = d2 }).Item1 == "WAIT", "entry missed → WAIT, no ✅");
            }
            var h4 = new KeystoneOfCardHold(); h4.Offer(s2, good, "MNQ", T0, 12); var r4 = h4.Check(T0.AddMinutes(10), 100, 30, 0); Check(r4.Item1 == "expired" && r4.Item2 == "expired after 10 min", "expires after 10 minutes with its reason");
        }
        // ---- 4. the verdict: TAKE only with a card, WAIT names its condition, AVOID reasons, no flicker, never contradicts the compass
        {
            var x = new KeystoneFlowVerdictInput { CompassState = 0, CompassScore = 5 }; Check(KeystoneFlowVerdict.Raw(x).Item1 == "AVOID" && KeystoneFlowVerdict.Raw(x).Item2.StartsWith("balanced / chop"), "balanced + small score → AVOID (chop)");
            x.CompassScore = 24; Check(KeystoneFlowVerdict.Raw(x).Item2.StartsWith("the compass to turn BUYERS"), "leaning buyers → WAIT for the compass to turn BUYERS", KeystoneFlowVerdict.Raw(x).Item2);
            x.RawWait = "the 1-min candle to close above 28252.00"; Check(KeystoneFlowVerdict.Raw(x).Item1 == "WAIT" && KeystoneFlowVerdict.Raw(x).Item2 == "the 1-min candle to close above 28252.00", "a raw setup → WAIT names the closed candle it waits for");
            x.SkippedRr = 0.3; Check(KeystoneFlowVerdict.Raw(x).Item2 == "reward smaller than risk (0.3)", "R:R < 1 → AVOID with the R:R");
            x.SkippedRr = double.NaN; x.RawWait = ""; x.AgainstFlow = true; x.AgainstText = "against the flow"; Check(KeystoneFlowVerdict.Raw(x).Item1 == "AVOID", "against the flow → AVOID");
            x.AgainstFlow = false; Check(KeystoneFlowVerdict.Raw(x).Item1 != "TAKE", "no card → never TAKE");
            x.CardOffered = true; x.CardText = "BUY 4 MNQ"; x.CardDir = 1; Check(KeystoneFlowVerdict.Raw(x).Item1 == "TAKE", "an offered card → TAKE");
            x.AgainstPosition = true; x.PositionText = "⚠ against your open SHORT 1"; Check(KeystoneFlowVerdict.Raw(x).Item1 == "AVOID", "an offered card against the open position → not ✅ (AVOID)");
            x.AgainstPosition = false; x.InTradeAtMax = true; Check(KeystoneFlowVerdict.Raw(x).Item1 == "AVOID", "in a trade at max contracts → AVOID");
            var v = new KeystoneFlowVerdict(); var a = new KeystoneFlowVerdictInput { CompassState = 0, CompassScore = 5 }; var b = new KeystoneFlowVerdictInput { CompassState = 0, CompassScore = 25 }; int changes = 0; string k0 = null;
            for (int s = 0; s < 120; s++) { v.Update(s % 2 == 0 ? a : b, T0.AddSeconds(s)); if (k0 != null && v.Kind != k0) changes++; k0 = v.Kind; }
            Check(changes == 0, "flickering between AVOID / WAIT every second → the verdict does not flicker", "changes " + changes);
            for (int s = 120; s < 140; s++) v.Update(b, T0.AddSeconds(s)); Check(v.Kind == "WAIT", "a steady new verdict takes over after 15 s");
            v.Update(new KeystoneFlowVerdictInput { CardOffered = true, CardDir = -1, CardText = "SELL", CompassState = -1, Proven = false, LearnText = "learning (4)" }, T0.AddSeconds(141));
            Check(v.Kind == "TAKE" && v.Head == "🧪 SELL • learning (4)", "a card appears → at once; not proven → 🧪 learning (n), never a ✅", v.Head);
            v.Update(new KeystoneFlowVerdictInput { CardOffered = true, CardDir = -1, CompassState = -1, Proven = true }, T0.AddSeconds(142)); Check(v.Head == "✅ SELL", "proven (n ≥ 30, beats break-even) → ✅ SELL");
            // never contradicts the compass: WAIT text names the compass side only
            bool okC = true; foreach (int st in new[] { -1, 0, 1 }) foreach (double sc in new[] { -60.0, -20, 0, 20, 60 })
            { var r = KeystoneFlowVerdict.Raw(new KeystoneFlowVerdictInput { CompassState = st, CompassScore = sc }).Item2; if (st > 0 && r.Contains("SELLERS") || st < 0 && r.Contains("BUYERS") || st > 0 && r.Contains("sellers") || st < 0 && r.Contains("buyers")) okC = false; }
            Check(okC, "the verdict never names the side the compass is against");
        }
        // ---- 5. who's doing what: per-side averages
        {
            var t = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0, Price = 100, Size = 2, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(10), Price = 101, Size = 2, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(20), Price = 99, Size = 4, Side = -1 }, new KeystoneOfTick { Time = T0.AddSeconds(200), Price = 50, Size = 9, Side = 1 } };
            var b = KeystoneFlowSides.Of(t, t.Count, T0.AddSeconds(30), 60, 1, 102); var s = KeystoneFlowSides.Of(t, t.Count, T0.AddSeconds(30), 60, -1, 102);
            Check(b.Volume == 4 && Math.Abs(b.Avg - 100.5) < 1e-9 && Math.Abs(b.PnlPts - 1.5) < 1e-9 && s.Volume == 4 && Math.Abs(s.PnlPts + 3) < 1e-9, "per side: contracts, average price, + / − pts now (no trade after the clock)");
            Check(KeystoneFlowSides.Line(b, "1 min").StartsWith("BUYERS since 10:00 bought 4 @ avg 100.50 → +1.50 pts in profit"), "the plain line", KeystoneFlowSides.Line(b, "1 min"));
        }
        // ---- 6. the trapped rule
        {
            var t = new List<KeystoneOfTick>(); for (int i = 0; i < 40; i++) t.Add(new KeystoneOfTick { Time = T0.AddSeconds(i), Price = 100.25 + (i % 2) * 0.25, Size = 10, Side = 1 });
            for (int i = 0; i < 20; i++) t.Add(new KeystoneOfTick { Time = T0.AddSeconds(60 + i * 3), Price = 99.75 - i * 0.25, Size = 2, Side = -1 });
            var now = t[t.Count - 1].Time; double px = t[t.Count - 1].Price;
            var z = KeystoneFlowTrap.Find(t, t.Count, now, 0.25, px, 200); Check(z.Count == 1 && z[0].Side == 1 && z[0].Volume == 400 && z[0].Lo <= 100.25 && z[0].Hi >= 100.5, "400 contracts bought at a band, then −8 ticks within 5 min, still below → trapped buyers", z.Count + "");
            Check(z.Count == 1 && KeystoneFlowTrap.Text(z[0]).Contains("trapped buyers") && KeystoneFlowTrap.Text(z[0]).Contains("~400 contracts bought"), "the plain sentence", z.Count == 1 ? KeystoneFlowTrap.Text(z[0]) : "");
            Check(KeystoneFlowTrap.Find(t, t.Count, now, 0.25, px, 500).Count == 0, "below the volume threshold → nothing");
            Check(KeystoneFlowTrap.Find(t, t.Count, now, 0.25, 100.75, 200).Count == 0, "the price back above the band → not trapped any more");
            Check(KeystoneFlowTrap.Find(t, 45, T0.AddSeconds(44), 0.25, 100.25, 200).Count == 0, "no move against yet (only the trades up to the clock) → nothing (no look-ahead)");
        }
        // ---- 7. big prints + key levels
        {
            var t = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0, Price = 100, Size = 30, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(1), Price = 100.25, Size = 25, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(60), Price = 101.5, Size = 1, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(200), Price = 101.5, Size = 1, Side = 1 } };
            var bp = KeystoneFlowBig.Find(t, t.Count, T0.AddSeconds(200), 0.25, 20); Check(bp.Count == 1 && bp[0].Size == 55 && bp[0].After == "pushed", "two big buys 1 s apart = one cluster of 55, the price pushed after", bp.Count + " " + (bp.Count > 0 ? bp[0].After : ""));
            var lv = KeystoneFlowLevels.Build(100, new[] { Tuple.Create("POC", 100.5, "magnet"), Tuple.Create("DAY HIGH", 103.0, ""), Tuple.Create("VAL", 99.0, ""), Tuple.Create("VWAP", double.NaN, "magnet") }, p => p == 100.5);
            Check(lv.Count == 3 && lv[0].Name == "POC" && lv[0].Touched && lv[1].Role == "support" && lv[2].Role == "resistance", "key levels: nearest first, roles, just touched, NaN dropped");
        }
        // ---- 8. the 📒 setup book: outcomes from the trades after it only
        {
            var r = new KeystoneOfSetupRow { Time = T0, Symbol = "MNQ", Kind = "STACKED", Dir = 1, Entry = 100, Stop = 98, Target = 104, Qty = 2, Compass = 1 };
            var t = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0.AddSeconds(-30), Price = 90, Size = 1, Side = -1 } };
            for (int i = 1; i <= 2400; i++) t.Add(new KeystoneOfTick { Time = T0.AddSeconds(i), Price = 100 + Math.Min(4.5, i / 60.0), Size = 1, Side = 1 });
            KeystoneOfSetupBook.Measure(r, t, t.Count, T0.AddMinutes(3), 0.25); Check(r.Result == 0 && !r.Done && r.Mae == 0 && double.IsNaN(r.M5), "at 3 min: still open, nothing from before it (−10 pts earlier ignored), no 5-min move yet (no look-ahead)");
            KeystoneOfSetupBook.Measure(r, t, t.Count, T0.AddMinutes(40), 0.25); Check(r.Result == 1 && Math.Abs(r.R - 2) < 1e-9 && r.Scalp == 1 && r.Done && Math.Abs(r.M5 - 4.5 + 0) < 0.5 && r.Mfe >= 4, "at 40 min: target first (+2 R), scalp +8 ticks first, 5 / 15 / 30-min moves", r.Result + " " + r.R + " " + r.M5);
            var back = KeystoneOfSetupBook.Parse(KeystoneOfSetupBook.Line(r)); Check(back != null && back.Result == 1 && back.Dir == 1 && back.Kind == "STACKED" && Math.Abs(back.R - 2) < 1e-9 && back.Done, "the CSV line round-trips");
            var dir = Path.Combine(Path.GetTempPath(), "ksof-" + Guid.NewGuid().ToString("N").Substring(0, 6)); KeystoneOfSetupBook.SaveDay(dir, "MNQ", T0.Date, new[] { r });
            Check(KeystoneOfSetupBook.LoadAll(dir, "MNQ").Count == 1 && File.Exists(Path.Combine(dir, "OrderFlow", "setups-MNQ-2026-11-12.csv")), "saved per instrument per day in OrderFlow\\setups-<SYM>-<day>.csv");
            var tb = KeystoneOfSetupStats.Table(new[] { r }, x => x.Kind); Check(tb.Count == 1 && tb[0][2] == "n < 20: not enough yet", "ANALYZE: n < 20 → not enough yet (never a %)");
        }
        // ---- 9. 🧠 learn: learning under 30, history says skip, no look-ahead, baseline honest
        {
            var rows = new List<KeystoneOfSetupRow>(); for (int i = 0; i < 40; i++) rows.Add(new KeystoneOfSetupRow { Time = T0.AddMinutes(i), Symbol = "MNQ", Kind = "STACKED", Dir = 1, Compass = -1, Entry = 100, Stop = 99, Target = 101, Result = i % 6 == 0 ? 1 : -1 });
            var now = new KeystoneOfSetupRow { Time = T0.AddMinutes(10), Kind = "STACKED", Dir = 1, Compass = -1, Entry = 100, Stop = 99, Target = 101 };
            var j1 = KeystoneOfLearner.Judge(rows, now); Check(j1.N == 10 && j1.Text.StartsWith("🧠 learning (10)") && j1.Skip == "", "tiny n → \"learning (n)\", no skip, only the setups BEFORE it", j1.Text);
            var now2 = new KeystoneOfSetupRow { Time = T0.AddMinutes(41), Kind = "STACKED", Dir = 1, Compass = -1, Entry = 100, Stop = 99, Target = 101 };
            var j2 = KeystoneOfLearner.Judge(rows, now2); Check(j2.N == 40 && j2.Skip.StartsWith("⚠ history says skip") && j2.Calibrated < 0.5, "n ≥ 30 and below break-even → ⚠ history says skip", j2.Skip);
            var other = new KeystoneOfSetupRow { Time = T0.AddMinutes(41), Kind = "STACKED", Dir = 1, Compass = 1, Entry = 100, Stop = 99, Target = 101 }; Check(KeystoneOfLearner.Judge(rows, other).N == 0, "another compass agreement = another group");
            Check(KeystoneOfLearner.Lessons(rows).Any(l => l.Contains("against SELLERS compass") && l.Contains("not offered")), "a lesson line per condition");
            Check(KeystoneOfLearner.Baseline(rows.Take(20)).Contains("learning"), "model vs random: learning with few setups");
            Check(KeystoneOfLearner.Baseline(rows).Contains("Brier"), "model vs random: a walk-forward Brier comparison", KeystoneOfLearner.Baseline(rows));
        }
        // ---- 10. 🏦 account fit + ranker
        {
            var f1 = KeystoneFlowFit.Text(10, 10, 49, 2000, double.NaN, false, true, 0, "MNQ", "the plan's stop"); Check(f1.Item2 == 10 && f1.Item1.Contains("fits end-of-day trailing (stop $100 = 5 % of the $2,000 buffer)"), "fits EOD: stop $100 = 5 % of the $2,000 buffer", f1.Item1);
            var f2 = KeystoneFlowFit.Text(10, 10, 49, 1948, double.NaN, true, true, 0, "MNQ", "the plan's stop"); Check(f2.Item1.Contains("intraday trailing: the +$490 run would raise your floor $490 before the target — OK, buffer $1,948"), "intraday: the run raises the floor", f2.Item1);
            var f3 = KeystoneFlowFit.Text(10, 10, 49, 2000, 80, false, true, 0, "MNQ", "the plan's stop"); Check(f3.Item2 == 8 && f3.Item1.StartsWith("⚠ too big for this account: stop $100 > today's room $80 → max 8 MNQ"), "too big: today's room → max 8", f3.Item1);
            var f4 = KeystoneFlowFit.Text(10, 10, 49, double.NaN, double.NaN, false, false, 0, "MNQ", "x"); Check(f4.Item1.Contains("unknown"), "unknown rules say so");
            var acc = new List<KeystoneFitAccount> {
                new KeystoneFitAccount { Name = "TPT483", Display = "TPT-…483", Stage = "FUNDED", Intraday = true, Buffer = 1948, MaxQty = 10 },
                new KeystoneFitAccount { Name = "LUC12", Display = "Lucid-…12", Stage = "EVAL", Buffer = 2000, ToTarget = 900, MaxQty = 10 },
                new KeystoneFitAccount { Name = "LUC07", Display = "Lucid-…07", Stage = "EVAL", Buffer = 2000, OneADay = true, TradedToday = true },
                new KeystoneFitAccount { Name = "LUC08", Display = "Lucid-…08", Stage = "EVAL", Buffer = 50 },
                new KeystoneFitAccount { Name = "Sim101", Display = "Sim101", Sim = true, Buffer = 99999 } };
            var rk = KeystoneSetupAccountFit.Rank(acc, 10, 49, 10, "MNQ");
            Check(rk.Count == 4 && rk.All(x => x.Acc.Name != "Sim101"), "🎮 sim accounts are never ranked");
            Check(rk[0].Acc.Name == "LUC12" && rk[0].Qty == 10, "the best fit first (EVAL near its target, room for 10)", rk[0].Acc.Name);
            var tx = KeystoneSetupAccountFit.Text(rk); Check(tx.StartsWith("🏦 best for: Lucid-…12 (EVAL • EOD • buffer $2,000 → 10 MNQ)") && tx.Contains("2nd: TPT-…483") && tx.Contains("Lucid-…07 (already traded today (ONE TRADE A DAY))") && tx.Contains("Lucid-…08 (the stop $10 a contract does not fit its room $50)") == false, "best / 2nd / not for with reasons", tx);
            Check(rk.First(x => x.Acc.Name == "LUC08").Qty == 5 || rk.First(x => x.Acc.Name == "LUC08").Why.Contains("does not fit"), "a small room → fewer contracts or excluded with the reason");
        }
        // ---- 11. 📦 export bundle: README + a missing piece skipped with a note
        {
            var dir = Path.Combine(Path.GetTempPath(), "ksexp-" + Guid.NewGuid().ToString("N").Substring(0, 6)); var day = new DateTime(2026, 3, 9);
            var tk = new List<KeystoneOfTick>(); var st = KeystoneTickLib.SessionStart(day); for (int i = 0; i < 100; i++) tk.Add(new KeystoneOfTick { Time = st.AddSeconds(10 + i), Price = 20000 + i * 0.25, Size = 1, Side = 1, Quoted = true });
            KeystoneTickLib.Save(dir, "MNQ", day, tk, "TICKS", true); KeystoneOfSetupBook.SaveDay(dir, "MNQ", day, new[] { new KeystoneOfSetupRow { Time = day.AddHours(10), Symbol = "MNQ", Kind = "STACKED", Dir = 1, Entry = 1, Stop = 0.5, Target = 2 } });
            var zip = Path.Combine(dir, "exports", "keystone-export-test.zip"); var prog = new List<string>();
            File.WriteAllText(Path.Combine(dir, "KeyStone.log"), new string('x', 5000) + "END");
            KeystoneOfExport.Build(dir, new[] { "MNQ" }, new[] { day }, new[] { "ACC1" }, "time_ny,text\n2026-03-09 10:00:00,x\n", "", zip, prog.Add, new[] { Tuple.Create("diagnostics/KeyStone.log", Path.Combine(dir, "KeyStone.log"), 1000L), Tuple.Create("diagnostics/CommissionRates.txt", Path.Combine(dir, "CommissionRates.txt"), 0L) }, new[] { Tuple.Create("diagnostics/funded-peaks.txt", "pk.sync.ACC1=2026-03-09 10:00") });
            var z = KeystoneZip.Read(zip); string rd = System.Text.Encoding.UTF8.GetString(z["README.txt"]);
            Check(z.ContainsKey("ticks/MNQ/2026-03-09.csv.gz") && z.ContainsKey("setups/setups-MNQ-2026-03-09.csv") && z.ContainsKey("story.csv") && !z.ContainsKey("compass.csv"), "the bundle: ticks, setups, story; an empty piece left out", string.Join(",", z.Keys));
            Check(z.ContainsKey("diagnostics/KeyStone.log") && z["diagnostics/KeyStone.log"].Length == 1000 && System.Text.Encoding.UTF8.GetString(z["diagnostics/KeyStone.log"]).EndsWith("END") && z.ContainsKey("diagnostics/funded-peaks.txt") && rd.Contains("no CommissionRates.txt yet") && rd.Contains("funded-peaks.txt"), "diagnostics: the log's tail (capped), the funded peaks, a missing commission file noted");
            Check(rd.Contains("compass.csv") && rd.Contains("no compass readings yet") && rd.Contains("no 🧾 EXECUTIONS days saved for ACC1") && rd.Contains("no saved 1-minute candles for MNQ") && prog.Contains("done"), "README describes every file + notes the skipped pieces; progress reported");
        }
        // ---- 12. in-trade confidence + the battle map since the entry
        {
            var good = KeystoneTradeConfidence.Of(2, 1, 48, 300, 1000, 2, 0, 10, 8); Check(good.Item1 >= 2 && good.Item3 == "HOLD" && good.Item4.Count <= 3 && good.Item4[0].Contains("with you"), "LONG with BUYERS, delta + events for → ++ / +++ HOLD", good.Item2);
            var bad = KeystoneTradeConfidence.Of(2, -1, -52, -400, 1000, 0, 3, 1, 10); Check(bad.Item1 <= -2 && bad.Item3 == "⛔ GET OUT" && bad.Item4.Any(x => x.Contains("against you")), "LONG while SELLERS lead, delta + events against → ⛔ GET OUT (text only)", bad.Item2);
            var mid = KeystoneTradeConfidence.Of(-1, 0, 5, 0, 0, 0, 0, double.NaN, double.NaN); Check(mid.Item1 == 0 && mid.Item3 == "HOLD", "nothing either way → ± HOLD");
            Check(KeystoneTradeConfidence.Of(0, 1, 50, 1, 1, 1, 0, 1, 1).Item2 == "", "flat → nothing");
            // 11x: the user's case — LONG 1 in profit 20 s after the entry, the compass still HELD on SELLERS (min hold) while its score is +48 for buyers
            var held = KeystoneTradeConfidence.Of(1, -1, 48, 20, 100, 0, 0, double.NaN, double.NaN, 6, 20);
            Check(held.Item1 >= 0 && held.Item3 == "HOLD" && !held.Item4.Any(x => x.Contains("against you")) && held.Item4.Any(x => x.Contains("changing")), "a held SELLERS state with a +48 score is 'changing', never 'against you' → HOLD", held.Item2 + " " + held.Item3);
            var fresh = KeystoneTradeConfidence.Of(2, -1, -52, -400, 1000, 0, 3, 1, 10, double.NaN, 30);
            Check(fresh.Item3 == "⚠ TIGHTEN", "a trade under 1 minute is never told GET OUT", fresh.Item3);
            var prof = KeystoneTradeConfidence.Of(2, -1, -52, -400, 1000, 0, 3, 1, 10, 120, 300);
            Check(prof.Item3.StartsWith("⚠ PROTECT THE PROFIT"), "in profit with the flow against → protect the profit, not GET OUT", prof.Item3);
            var tm = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0.AddSeconds(-5), Price = 99, Size = 50, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(2), Price = 100, Size = 3, Side = 1 }, new KeystoneOfTick { Time = T0.AddSeconds(3), Price = 100, Size = 2, Side = -1 }, new KeystoneOfTick { Time = T0.AddSeconds(4), Price = 100.25, Size = 4, Side = -1 }, new KeystoneOfTick { Time = T0.AddSeconds(90), Price = 101, Size = 9, Side = 1 } };
            var map = KeystoneTradeMap.Rows(tm, tm.Count, T0, T0.AddSeconds(10), 0.25); Check(map.Count == 2 && map[100][0] == 3 && map[100][1] == 2 && map[100.25][1] == 4, "battle map: buy / sell per row since the entry, nothing before it or after the clock");
        }
        // ---- 13. patience: the cool-down, PATIENT shows nothing unproven, scalp expectancy after costs
        {
            var h = new KeystoneOfCardHold(); var pl = new KeystoneOfPlan { Dir = 1, Qty = 1, Entry = 100, Stop = 98, Target = 104, StopPts = 2, TargetPts = 4 };
            h.Offer(new KeystoneOfSignal { Bar = 5, Dir = 1, Kind = "CONFIRMED", Price = 100, Time = T0 }, pl, "MNQ", T0, 6); h.Check(T0.AddSeconds(5), 100, 6, 0); h.End(T0.AddSeconds(5), "skipped", "you pressed SKIP");
            bool o1 = h.Offer(new KeystoneOfSignal { Bar = 6, Dir = 1, Kind = "CONFIRMED", Price = 100, Time = T0.AddMinutes(1) }, pl, "MNQ", T0.AddMinutes(1), 7);
            bool o2 = h.Offer(new KeystoneOfSignal { Bar = 7, Dir = 1, Kind = "CONFIRMED", Price = 100, Time = T0.AddMinutes(2) }, pl, "MNQ", T0.AddMinutes(2), 8);
            Check(!o1 && o2, "🧘 cool-down: no new setup for 2 closed candles after one ends");
            var rows = new List<KeystoneOfSetupRow>(); for (int i = 0; i < 12; i++) rows.Add(new KeystoneOfSetupRow { Time = T0.AddMinutes(i), Kind = "CONFIRMED", Dir = 1, Compass = 1, Entry = 100, Stop = 98, Target = 104, Result = 1 });
            var j = KeystoneOfLearner.Judge(rows, new KeystoneOfSetupRow { Time = T0.AddHours(1), Kind = "CONFIRMED", Dir = 1, Compass = 1, Entry = 100, Stop = 98, Target = 104 });
            Check(!j.Proven && KeystoneFlowGate.Decide(new KeystoneOfSignal { Bar = 3, Dir = 1, Kind = "CONFIRMED", Price = 100 }, 5, 1, pl, j.Proven ? null : "not offered — " + j.Text).Item1 == KeystoneFlowGate.HistorySkip, "PATIENT: n < 30 (even all wins) → not offered");
            Check(Math.Abs(KeystoneOfLearner.BreakEven(2, 4, 0.31) - (2.31 / 6)) < 1e-9, "break-even includes the costs");
            var sr = new List<KeystoneOfSetupRow>(); for (int i = 0; i < 40; i++) sr.Add(new KeystoneOfSetupRow { Entry = 100, Stop = 98, Scalps = "8:" + (i % 2 == 0 ? "+1" : "-1") + ";12:0;16:0;20:0" });
            var st = KeystoneOfScalp.Of(sr, 0.25, 2, 1.24); Check(st[0].N == 40 && Math.Abs(st[0].AvgTicks - 0) < 1e-9 && Math.Abs(st[0].ExpUsd + 1.24) < 1e-9 && KeystoneOfScalp.BestTicks(sr, 0.25, 2, 1.24) == 0, "scalp: +8 / −8 ticks 50 % = $0 before costs → −$1.24 after → not proven");
        }
        // ---- 14. history replay on synthetic real ticks: rows labelled, outcomes measured from later trades only
        {
            var r = new Random(4); var t = new List<KeystoneOfTick>(); double px = 20000; var st0 = new DateTime(2026, 3, 9, 9, 30, 0);
            for (int i = 0; i < 20000; i++) { px += (r.Next(3) - 1) * 0.25 + (i / 2000 % 2 == 0 ? 0.02 : -0.02); px = Math.Round(px * 4) / 4; t.Add(new KeystoneOfTick { Time = st0.AddMilliseconds(i * 900), Price = px, Size = 1 + r.Next(6), Side = r.Next(2) == 0 ? 1 : -1, Quoted = true }); }
            var rows = KeystoneOfHistory.Replay(t, "MNQ", 0.25, 2, new KeystoneOfSettings { NeedSweep = false }, 20, 12);
            Check(rows.All(x => x.Status == "history replay" && x.Entry > 0 && Math.Abs(x.Target - x.Entry - x.Dir * 3) < 1e-9), "history replay rows: labelled, scalp target 12 ticks from the entry (" + rows.Count + " setups)");
            Check(rows.All(x => x.Result == 0 || x.Mfe >= 0), "outcomes measured from the trades after each setup");
        }
        // ---- 15. 👀 zone retest: creation, expiry, break, retest + confirmation required
        {
            var zs = new KeystoneFlowZones(); var z = zs.Add("A|1", 1, 100, 100.5, "STACKED", "stacked buyers", T0, 10, 3);
            Check(z != null && zs.Add("A|1", 1, 100, 100.5, "STACKED", "x", T0, 10, 3) == null && zs.Live.Count == 1, "an event makes ONE buy zone (dedup)");
            var r1 = zs.OnClosed(11, T0.AddMinutes(1), 103, 104, 102.5, 103.5, 50, 0.25); Check(r1.Item1.Count == 0 && zs.Live.Count == 1, "price away: no setup");
            Check(KeystoneFlowZones.WaitText(z, 103.5).StartsWith("price 3.00 pts above the BUY ZONE 100.00–100.50"), "WAIT says how far and what to wait for", KeystoneFlowZones.WaitText(z, 103.5));
            var r2 = zs.OnClosed(12, T0.AddMinutes(2), 101, 101.25, 100.25, 100.5, -30, 0.25); Check(r2.Item1.Count == 0, "back into the zone but closing weak with the delta against → no confirmation, no setup");
            var r3 = zs.OnClosed(13, T0.AddMinutes(3), 100.5, 101.5, 100.25, 101.25, 40, 0.25); Check(r3.Item1.Count == 1 && r3.Item1[0].ConfirmBar == 13, "back into the zone + a closed candle holding it with the delta → retest CONFIRMED");
            var sg = KeystoneFlowZones.Signal(r3.Item1[0], 13, T0.AddMinutes(3), 101.25); Check(sg.Kind == "ZONE STACKED" && sg.Dir == 1 && sg.Why.Contains("mark") && sg.Why.Contains("confirm"), "the confirmed retest becomes a setup signal (mark → wait → confirm)");
            var zs2 = new KeystoneFlowZones(); zs2.Add("B", -1, 200, 200.5, "ABSORB", "sellers absorbed", T0, 1, 1); var r4 = zs2.OnClosed(2, T0.AddMinutes(1), 200.5, 202, 200.4, 201.75, 60, 0.25);
            Check(r4.Item2.Count == 1 && zs2.Live.Count == 0 && zs2.Ended[0].State.StartsWith("zone broken"), "a close through the sell zone with the flow against it → broken, removed with why");
            var zs3 = new KeystoneFlowZones(); zs3.Add("C", 1, 50, 51, "TRAP", "trapped sellers", T0, 1, 1); var r5 = zs3.OnClosed(2, T0.AddHours(5), 55, 56, 54, 55, 1, 0.25); Check(r5.Item2.Count == 1 && zs3.Ended[0].State.StartsWith("expired"), "zones expire after 4 h");
            var zs4 = new KeystoneFlowZones(); for (int i = 0; i < 9; i++) zs4.Add("D" + i, 1, i, i + 0.5, "BIG", "big buys", T0, 1, i); Check(zs4.Live.Count == 6 && zs4.Live.All(x => x.Strength >= 3), "max 6 live zones (the weakest go)");
        }
        // ---- 16. palette
        Check(KeystoneOfPalette.Names.Length == 3 && KeystoneOfPalette.Buy(0) == "#26C6DA" && KeystoneOfPalette.Sell(0) == "#FF5C8A" && KeystoneOfPalette.Buy(9) == KeystoneOfPalette.Buy(0), "three palettes, TEAL / CORAL default");

        // ---- 17. 🏃 11x SWING beside ⚡ SCALP
        {
            var sg = new KeystoneOfSignal { Bar = 5, Dir = 1, Kind = "ZONE STACKED", Price = 100, Time = T0, ZoneLo = 98, ZoneHi = 99 };
            var cdl = new KeystoneFootprint { Start = T0.AddMinutes(-5), End = T0, Open = 99.5, High = 100.25, Low = 98.5, Close = 100, Trades = 50, Buy = 30, Sell = 20 };
            var lv = new List<Tuple<double, string>> { Tuple.Create(103.0, "VAH"), Tuple.Create(106.0, "POC"), Tuple.Create(110.0, "PRIOR HIGH"), Tuple.Create(95.0, "VAL") };
            var sw = KeystoneOfSwing.Plan(sg, cdl, 100, 0.25, 2, 200, lv, 2, 8, 10);
            Check(sw.Plan != null && sw.Plan.Stop == 97.5 && sw.Plan.Target == 106 && sw.Plan.TargetName == "POC" && sw.Plan.Style == "SWING" && sw.Plan.TargetWhy.Contains("POC 106.00"), "swing: stop beyond the zone's far edge + 2 ticks, take profit = the FIRST major level ≥ 2 R (VAH at 1.2 R skipped → POC), named", sw.Plan == null ? sw.Why : sw.Plan.Stop + " " + sw.Plan.Target + " " + sw.Plan.TargetWhy);
            var rec0 = new KeystoneOfSetupRecord { Style = "SWING", TargetName = "POC", Entry = 100, Target = 106 }; string lab = KeystoneOfSwing.TpLabel(rec0, 120, "106.00");
            Check(lab == "🏃 💡 TAKE PROFIT 106.00 (POC) • +$120 planned", "the chart label names the level + 🏃", lab);
            var no = KeystoneOfSwing.Plan(sg, cdl, 100, 0.25, 2, 200, new List<Tuple<double, string>> { Tuple.Create(103.0, "VAH") }, 2, 8, 10);
            Check(no.Plan == null && no.Why == "no room for a swing: next level 1.2 R (VAH)", "no major level ≥ 2 R → no swing, says why", no.Why);
            var g0 = KeystoneFlowGate.DecideSwing(sg, 8, 0, no); Check(g0.Item1 == KeystoneFlowGate.NoRoom && g0.Item2.StartsWith("no room for a swing: next level 1.2 R"), "the swing gate: no room → not offered with the reason", g0.Item2);
            Check(KeystoneFlowGate.DecideSwing(sg, 8, 0, sw).Item1 == KeystoneFlowGate.Offer && KeystoneFlowGate.DecideSwing(sg, 8, -1, sw).Item1 == KeystoneFlowGate.Against && KeystoneFlowGate.DecideSwing(sg, 5, 0, sw).Item1 == KeystoneFlowGate.Forming, "the swing gate: closed candle + compass like the scalp");
            // the SWING minimum stop (MNQ 12 pts, MGC 3 pts, OF.SWMINSTOP when larger) + contracts from the wider stop (fewer than the scalp)
            Check(KeystoneOfStyle.SwingMinStopTicks("MNQ", 0) == 48 && KeystoneOfStyle.SwingMinStopTicks("MGC", 0) == 30 && KeystoneOfStyle.SwingMinStopTicks("MNQ", 60) == 60, "swing min stop: MNQ 12 pts, MGC 3 pts, the setting wins when larger");
            var tight = new KeystoneOfSignal { Bar = 5, Dir = 1, Kind = "ZONE ABSORB", Price = 100, Time = T0, ZoneLo = 99.75, ZoneHi = 100.25 };
            var lv2 = new List<Tuple<double, string>> { Tuple.Create(130.0, "PRIOR HIGH") };
            var sw2 = KeystoneOfSwing.Plan(tight, null, 100, 0.25, 2, 200, lv2, 0, KeystoneOfStyle.SwingMinStopTicks("MNQ", 0), 0);
            var sc2 = KeystoneOfDesk.Plan(new KeystoneOfSignal { Bar = 0, Dir = 1, Kind = "STACKED", Price = 100, Time = T0 }, new List<KeystoneFootprint> { Fp(T0) }, null, 100, 0.25, 2, 200, 40, 2, KeystoneOfCardPlan.MinStopTicks("MNQ", 0), 0);
            Check(sw2.Plan != null && sw2.Plan.Widened && sw2.Plan.Stop == 88 && sw2.Plan.Qty == 8 && sc2 != null && sc2.Qty == 20 && sw2.Plan.Qty < sc2.Qty, "a tiny zone → the swing stop widened to 12 pts; $200 risk → 8 MNQ on the swing vs 20 on the scalp", sw2.Plan == null ? sw2.Why : sw2.Plan.Stop + " q" + sw2.Plan.Qty + " vs " + (sc2 == null ? -1 : sc2.Qty));
            var sw3 = KeystoneOfSwing.Plan(tight, null, 100, 0.25, 2, 2000, lv2, 0, 48, 10); Check(sw3.Plan != null && sw3.Plan.Qty == 10 && sw3.Plan.Capped, "the 11u size cap applies to the swing too");
            // styles gated separately: 40 scalp wins prove the scalp, never the swing
            var rows = new List<KeystoneOfSetupRow>(); for (int i = 0; i < 40; i++) rows.Add(new KeystoneOfSetupRow { Time = T0.AddMinutes(i), Kind = "ZONE STACKED", Dir = 1, Compass = 1, Entry = 100, Stop = 98, Target = 104, Result = 1 });
            var jS = KeystoneOfLearner.Judge(rows, new KeystoneOfSetupRow { Time = T0.AddHours(2), Kind = "ZONE STACKED", Dir = 1, Compass = 1, Entry = 100, Stop = 98, Target = 104 });
            var jW = KeystoneOfLearner.Judge(rows, new KeystoneOfSetupRow { Style = "SWING", Time = T0.AddHours(2), Kind = "ZONE STACKED", Dir = 1, Compass = 1, Entry = 100, Stop = 88, Target = 130 });
            Check(jS.Proven && !jW.Proven && jW.N == 0 && jW.Text.StartsWith("🧠 learning (0)"), "a proven SCALP does not make the SWING ✅ (per type × side × style)", jS.Text + " | " + jW.Text);
            Check(KeystoneOfLearner.Lessons(rows.Concat(new[] { new KeystoneOfSetupRow { Style = "SWING", Time = T0, Kind = "ZONE STACKED", Dir = 1, Compass = 1, Entry = 100, Stop = 88, Target = 130, Result = -1 } })).Any(l => l.StartsWith("🏃 SWING ▲ ZONE STACKED")), "the lessons list the swing group on its own");
            // the swing card: lives up to 60 min, resolves on the first touch, expires at 16:50 NY, its own hold (BOTH = two cards for the same signal)
            var hs = KeystoneOfCardHold.NewSwing(); hs.PointValue = 2; var hc = new KeystoneOfCardHold { PointValue = 2 }; var sig = new KeystoneOfSignal { Bar = 5, Dir = 1, Kind = "ZONE STACKED", Price = 100, Time = T0 };
            bool o1 = hc.Offer(sig, new KeystoneOfPlan { Dir = 1, Qty = 10, Entry = 100, Stop = 98, Target = 101, StopPts = 2, TargetPts = 1 }, "MNQ", T0, 6), o2 = hs.Offer(sig, sw.Plan, "MNQ", T0, 6);
            Check(o1 && o2 && hc.Current.Key != hs.Current.Key && hs.Current.Style == "SWING" && hs.Current.TargetName == "POC" && KeystoneOfStyle.WantsScalp(KeystoneOfStyle.Both) && KeystoneOfStyle.WantsSwing(KeystoneOfStyle.Both) && !KeystoneOfStyle.WantsSwing(KeystoneOfStyle.Scalp), "⚖ BOTH: the same signal → a SCALP row and a SWING row, each its own setup");
            Check(hs.Check(T0.AddMinutes(30), 101, 12, 0).Item1 == "" && hc.Check(T0.AddMinutes(30), 100.5, 12, 0).Item1 == "expired", "at 30 min the scalp card expired (10 min), the swing card still lives");
            Check(hs.Check(T0.AddMinutes(20), 101, 7, -1).Item1 == "", "a swing is not withdrawn one candle after the compass turns (it tolerates a pullback)");
            var e60 = hs.Check(T0.AddMinutes(60), 101, 30, 0); Check(e60.Item1 == "expired" && e60.Item2 == "expired after 60 min", "a swing card lives up to 60 minutes", e60.Item2);
            var hs2 = KeystoneOfCardHold.NewSwing(); hs2.PointValue = 2; hs2.Offer(sig, sw.Plan, "MNQ", T0, 6);
            var pth = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0.AddMinutes(25), Price = 106, Size = 1, Side = 1 }, new KeystoneOfTick { Time = T0.AddMinutes(26), Price = 101, Size = 1, Side = -1 } };
            hs2.TouchAll(pth, pth.Count, T0.AddMinutes(27)); var rs = hs2.Check(T0.AddMinutes(27), 101, 20, 0); Check(rs.Item1 == "target" && rs.Item2.StartsWith("✔ TAKE PROFIT HIT"), "the swing resolves on the FIRST touch of its target (then a pull-back = still a win)", rs.Item2);
            var hs3 = KeystoneOfCardHold.NewSwing(); var late = new DateTime(2026, 11, 12, 16, 30, 0); hs3.Offer(new KeystoneOfSignal { Bar = 5, Dir = 1, Kind = "ZONE STACKED", Price = 100, Time = late }, sw.Plan, "MNQ", late, 6);
            var e1650 = hs3.Check(late.AddMinutes(20), 101, 8, 0); Check(e1650.Item1 == "expired" && e1650.Item2.StartsWith("expired at 16:50 NY"), "a swing expires at 16:50 NY", e1650.Item2);
            Check(KeystoneOfCardHold.Line(hs2.History.Count > 0 ? hs2.History[0] : hs2.End(T0.AddMinutes(27), rs.Item1, rs.Item2)).StartsWith("🏃 #1"), "the recent setups line carries the style");
            // ⚖ the scale-out idea: TEXT only on a swing, nothing on a scalp
            string so = KeystoneOfStyle.ScaleOutIdea(sw.Plan, 12, 0.25); Check(so.StartsWith("⚖ idea: take part off at +12 ticks (103.00) and let the rest run to the POC 106.00") && so.Contains("text only") && so.Contains("nothing is split") && KeystoneOfStyle.ScaleOutIdea(new KeystoneOfPlan { Dir = 1, Qty = 3 }, 12, 0.25) == "", "⚖ scale-out = a text idea on a swing card only (never an order, never a split)", so);
            // the verdict names the style; the 📒 book keeps it
            var vv = new KeystoneFlowVerdict(); vv.Update(new KeystoneFlowVerdictInput { CardOffered = true, CardDir = -1, CompassState = -1, Proven = true, CardStyle = "SWING", CardSummary = "3 MNQ • R:R 2.4 • TP at VAH" }, T0);
            Check(vv.Head == "✅ SWING SELL 3 MNQ • R:R 2.4 • TP at VAH", "the NOW verdict says which style", vv.Head);
            var br = new KeystoneOfSetupRow { Style = "SWING", Time = T0, Symbol = "MNQ", Kind = "5M CONFIRMED", Dir = -1, Entry = 100, Stop = 112, Target = 76 }; var back = KeystoneOfSetupBook.Parse(KeystoneOfSetupBook.Line(br));
            var old = KeystoneOfSetupBook.Parse(string.Join(",", KeystoneOfSetupBook.Line(br).Split(',').Take(27)));
            Check(back.Style == "SWING" && back.Key.EndsWith("|SWING") && old.Style == "SCALP" && KeystoneOfSetupBook.Header.Contains(",style,"), "📒 SETUPS records the style (old files read as SCALP)");
            // 🎯 in-trade: a swing holds while its zone holds; a broken structure counts against
            var cs1 = KeystoneTradeConfidence.Of(1, -1, -40, -100, 1000, 0, 1, double.NaN, double.NaN, -20, 300, "SWING", 1); var cs2 = KeystoneTradeConfidence.Of(1, -1, -40, -100, 1000, 0, 1, double.NaN, double.NaN, -20, 300, "SCALP", 0);
            Check(cs1.Item3 == "HOLD" && cs1.Item4[0].Contains("tolerates a pullback") && cs2.Item3 != "HOLD", "a swing in a pullback: HOLD while the zone holds (the same scalp is told to tighten)", cs1.Item3 + " / " + cs2.Item3);
            // 🧭 the compass shown honestly while its state is held but the score turned
            Check(KeystoneFlowCompass.Shown(-1, 48) == "SELLERS fading → BUYERS +48" && KeystoneFlowCompass.Fading(-1, 48) && KeystoneFlowCompass.Shown(1, 50) == "BUYERS +50" && KeystoneFlowCompass.Shown(1, 5) == "BUYERS fading → BALANCED +5" && KeystoneFlowCompass.Shown(0, 20) == "BALANCED +20" && KeystoneFlowCompass.ShownLong(-1, -60) == "SELLERS IN CONTROL −60", "the held state vs the score: \"SELLERS fading → BUYERS +48\"", KeystoneFlowCompass.Shown(-1, 48));
            // 📜 the history replay measures both styles
            var r = new Random(4); var t = new List<KeystoneOfTick>(); double px = 20000; var st0 = new DateTime(2026, 3, 9, 9, 30, 0);
            for (int i = 0; i < 20000; i++) { px += (r.Next(3) - 1) * 0.25 + (i / 2000 % 2 == 0 ? 0.03 : -0.03); px = Math.Round(px * 4) / 4; t.Add(new KeystoneOfTick { Time = st0.AddMilliseconds(i * 900), Price = px, Size = 1 + r.Next(6), Side = r.Next(2) == 0 ? 1 : -1, Quoted = true }); }
            var rr = KeystoneOfHistory.Replay(t, "MNQ", 0.25, 2, new KeystoneOfSettings { NeedSweep = false }, 20, 12, 1, true, 48, 10);
            var sws = rr.Where(x => x.Style == "SWING").ToList(); var scs = rr.Where(x => x.Style != "SWING").ToList();
            Check(scs.Count > 0 && scs.All(x => Math.Abs(x.Target - x.Entry - x.Dir * 3) < 1e-9) && sws.All(x => x.Status == "history replay" && x.Rr >= 2 - 1e-9 && Math.Abs(x.Entry - x.Stop) >= 12 - 1e-9), "📜 PROVE FROM HISTORY: scalp rows + swing rows (R:R ≥ 2, stop ≥ 12 pts), " + scs.Count + " scalp / " + sws.Count + " swing");
            var w5 = KeystoneOfStyle.Windows5(new DateTime(2026, 11, 12, 10, 7, 30), 1); Check(w5.Count == 12 && w5[w5.Count - 1].Item2 == new DateTime(2026, 11, 12, 10, 5, 0), "5-min windows: closed ones only (the forming 10:05–10:10 is left out)");
        }
        // ---- 18. 🪤 11x FAILED ZONE → FLIP, 🪤 the failed big print, 🚥 the decision bar + classes, 👀 what's happening now (beginner phrases)
        {
            var zf = new KeystoneFlowZones(); zf.Add("S1", -1, 100, 101, "STACKED", "stacked sellers", T0, 1, 3);
            zf.OnClosed(2, T0.AddMinutes(1), 100.5, 101.5, 100.25, 100.75, 40, 0.25);   // into the zone, closes inside: nothing yet
            Check(zf.Flips.Count == 0 && zf.Live.Count == 1, "a sell zone traded into but not failed → no FLIP yet");
            zf.OnClosed(3, T0.AddMinutes(2), 99.5, 102.75, 99.25, 102.5, 120, 0.25);   // pushed below, then a CLOSED candle back above it with buying delta
            Check(zf.Flips.Count == 1 && zf.NewFlips.Count == 1 && zf.Flips[0].Dir == 1 && zf.Flips[0].Kind == "FLIP" && zf.Flips[0].ZoneLo == 99.25 && zf.Live.Count == 0 && zf.NewFlipStory[0] == "▼ SELL ZONE 100.00–101.00 FAILED — sellers absorbed, 🪤 trapped sellers → BUY setup",
                "a FAILED ▼ sell zone (a closed candle back above it, delta teal) → 🪤 trapped sellers → ▲ BUY FLIP, stop beyond the failed push's low", zf.NewFlipStory.Count > 0 ? zf.NewFlipStory[0] : "none");
            var zb = new KeystoneFlowZones(); zb.Add("B1", 1, 100, 101, "ABSORB", "buyers absorbed", T0, 1, 3); zb.OnClosed(2, T0.AddMinutes(1), 100.5, 101.25, 98.75, 98.5, -90, 0.25);
            Check(zb.Flips.Count == 1 && zb.Flips[0].Dir == -1 && zb.Flips[0].ZoneHi == 101.25, "the mirror: a failed ▲ buy zone → 🪤 trapped buyers → ▼ SELL");
            var fr = new List<KeystoneOfSetupRow>(); for (int i = 0; i < 35; i++) fr.Add(new KeystoneOfSetupRow { Time = T0.AddMinutes(i), Kind = i % 2 == 0 ? "FLIP" : "ZONE STACKED", Dir = 1, Compass = 1, Entry = 100, Stop = 98, Target = 104, Result = 1 });
            var jf = KeystoneOfLearner.Judge(fr, new KeystoneOfSetupRow { Time = T0.AddHours(2), Kind = "FLIP", Dir = 1, Compass = 1, Entry = 100, Stop = 98, Target = 104 });
            Check(jf.N == 18 && KeystoneOfSetupBook.Parse(KeystoneOfSetupBook.Line(fr[0])).Kind == "FLIP", "FLIP = a setup type of its own (journaled, judged on its own history)", "n " + jf.N);
            // the failed big print
            var bp = new KeystoneFlowBigPrint { Side = -1, Size = 16, Price = 28207.5, After = "failed" };
            Check(KeystoneFlowBig.Failed(bp) && KeystoneFlowBig.PressureSide(bp) == 1 && KeystoneFlowBig.FailLabel(bp) == "🪤 seller failed → BUY pressure" && KeystoneFlowBig.FailTip(bp) == "a big seller hit the bid at 28207.50 but the price went UP after it → the seller is trapped; this supports a BUY"
                && !KeystoneFlowBig.Failed(new KeystoneFlowBigPrint { Side = -1, After = "pushed" }) && KeystoneFlowBig.FailLabel(new KeystoneFlowBigPrint { Side = 1, After = "failed" }) == "🪤 buyer failed → SELL pressure", "🪤 a failed big print = restyled, its meaning in words (seller failed → BUY pressure)");
            // the decision bar = the verdict (the same object), the classes
            var vb = new KeystoneFlowVerdict(); vb.Update(new KeystoneFlowVerdictInput { CardOffered = true, CardDir = 1, Proven = true, CardStyle = "SWING", CardSummary = "3 MNQ • R:R 2.4 • TP at POC", CardText = "BUY 3 MNQ" }, T0);
            Check(KeystoneOfClass.Text(vb) == vb.Head + " — " + vb.Reason && KeystoneOfClass.Text(vb).StartsWith("✅ SWING BUY 3 MNQ"), "the decision bar's text = the NOW verdict (one source)", KeystoneOfClass.Text(vb));
            Check(KeystoneOfClass.Of("TAKE", true, "SCALP", false) == "SCALP" && KeystoneOfClass.Of("TAKE", true, "SWING", false) == "SWING" && KeystoneOfClass.Of("TAKE", false, "SWING", true) == "LEARNING" && KeystoneOfClass.Of("TAKE", true, "SCALP", true) == "STRONG"
                && KeystoneOfClass.Of("WAIT", false, "", false) == "WAIT" && KeystoneOfClass.Of("TAKE", true, "SCALP", true, "⚠ PROTECT THE PROFIT") == "PROTECT" && KeystoneOfClass.Of("", false, "", false, "⛔ GET OUT") == "CLOSE" && KeystoneOfClass.Of("", false, "", false, "HOLD") == "HOLD", "the class of a setup / a trade");
            Check(KeystoneOfClass.Color("SCALP") == "#26C6DA" && KeystoneOfClass.Color("SWING") == "#B388FF" && KeystoneOfClass.Color("STRONG") == "#FFC94D" && KeystoneOfClass.Color("LEARNING") == "#8A93A3" && KeystoneOfClass.Color("PROTECT") == "#FF9F43" && KeystoneOfClass.Color("CLOSE") == "#FF4D5E", "class colours: scalp cyan, swing violet, strong gold, learning grey, protect orange, close red");
            Check(KeystoneOfClass.IsStrong(true, true, true, true) && !KeystoneOfClass.IsStrong(false, true, true, true) && !KeystoneOfClass.IsStrong(true, true, false, true) && !KeystoneOfClass.IsStrong(true, true, true, false), "⭐ STRONG only when proven AND flow + location + compass all agree");
            Check(KeystoneOfClass.Opacity("LEARNING", 0.5, false) == 1 && KeystoneOfClass.Opacity("STRONG", 0.5, false) < KeystoneOfClass.Opacity("SCALP", 0.5, false) && KeystoneOfClass.Opacity("SCALP", 31, false) == 1 && KeystoneOfClass.Opacity("CLOSE", 0.5, true) == 1, "pulse: 30 s then steady, STRONG deeper, LEARNING never, reduce motion = steady");
            Check(KeystoneOfClass.InTrade("⛔ GET OUT", new List<string> { "everything turned against you" }, "LONG 2") == "⛔ CLOSE — everything turned against you • LONG 2", "in a trade: the suggestion + its first reason");
            // what's happening now: never contradicts the compass; the accumulation rule
            var ph = KeystoneFlowPhrases.Now(1, 48, 0.3, 500, -1, 28210, 0, 1, 28215);
            Check(ph.Count == 1 && ph[0].StartsWith("nothing clear") , "BUYERS in control: sell-side phrases are left out (never contradict the compass)", string.Join(" | ", ph));
            var ph2 = KeystoneFlowPhrases.Now(1, 48, 0.7, 500, 1, 28207, 0, -1, 28215);
            Check(ph2.Count == 3 && ph2[0] == "AGGRESSION: buyers are hitting the ask hard" && ph2[1] == "ABSORPTION: sellers hit 28207.00 hard but the price won't drop — someone is buying it all" && ph2[2].StartsWith("TRAPPED: sellers"), "≤ 3 plain phrases with the beginner words", string.Join(" | ", ph2));
            var ph3 = KeystoneFlowPhrases.Now(-1, 48, 0.7, 500, 0, 0, 0, 0, 0); Check(ph3[0] == "AGGRESSION: buyers are hitting the ask hard", "a fading SELLERS state with a +48 score follows the score (\"fading\")");
            var acc = new List<KeystoneFootprint>(); for (int i = 0; i < 12; i++) acc.Add(new KeystoneFootprint { Start = T0.AddMinutes(i), End = T0.AddMinutes(i + 1), Open = 100, High = 101, Low = 99.5, Close = 100.25, Trades = 10, Buy = 40, Sell = 60 });
            Check(KeystoneFlowPhrases.AccDist(acc, 99, 120, 0.25) == 1, "ACCUMULATION: flat near the session low while the sellers keep hitting (−20 % delta) → +1");
            Check(KeystoneFlowPhrases.AccDist(acc, 90, 120, 0.25) == 0 && KeystoneFlowPhrases.AccDist(acc.Take(5).ToList(), 99, 120, 0.25) == 0, "… not near the low / too few minutes → nothing");
            var dist = acc.Select(f => new KeystoneFootprint { Start = f.Start, End = f.End, Open = 100, High = 101, Low = 99.5, Close = 100.25, Trades = 10, Buy = 60, Sell = 40 }).ToList(); Check(KeystoneFlowPhrases.AccDist(dist, 80, 101.5, 0.25) == -1, "DISTRIBUTION: the mirror at the high");
            Check(KeystoneFlowPhrases.Word("ABSORB", 1) == "🧱 ABSORBED" && KeystoneFlowPhrases.Word("TRAP", -1) == "🪤 TRAPPED SELLERS" && KeystoneFlowPhrases.Word("STACKED", -1) == "▤ SELLERS PUSHING", "🎓 beginner words next to the icons");
        }
        // ---- 19. 🔔 11x event toasts + the narrator + the event journal
        {
            var ts = new KeystoneOfToasts(); ts.Add("a", "A", "x", "SCALP", 1, T0); ts.Add("b", "B", "x", "SCALP", 1, T0.AddSeconds(1)); ts.Add("c", "C", "x", "SWING", -1, T0.AddSeconds(2));
            Check(ts.Items.Count == 2 && ts.Items[0].Key == "c" && ts.Items[1].Key == "b" && !ts.Add("c", "C", "x", "", 0, T0.AddSeconds(3)), "toasts: max 2, newest on top, each event once");
            Check(ts.Opacity(ts.Items[0], T0.AddSeconds(7)) == 1 && ts.Opacity(ts.Items[0], T0.AddSeconds(8.5)) < 1, "a toast stays 6 s, then fades");
            ts.Prune(T0.AddSeconds(9.6)); Check(ts.Items.Count == 0, "… and is gone after 6 s + the fade");
            double x1 = KeystoneOfToasts.Place(0, 550, 600, 900, 260), x2 = KeystoneOfToasts.Place(0, 600, 860, 900, 260);
            Check(x1 + 260 <= 892 && x1 >= 606 && (x2 + 260 <= 600 || x2 >= 860), "a toast never covers the newest candles (free space left of the strip, else left of the candles)", x1 + " / " + x2);
            Check(KeystoneOfEvents.Narrate(1, "a buyers' stack", 1, 50, 0, "") == "→ supports BUY (with the buyers in control)" && KeystoneOfEvents.Narrate(-1, "sellers absorbing", 1, 50, 0, "").StartsWith("→ nothing to do yet — WAIT: it goes against the buyers"), "the narrator never contradicts the compass");
            Check(KeystoneOfEvents.Narrate(-1, "a sellers' stack", 1, 50, 2, "") == "→ warning for your LONG: tighten — a sellers' stack points the other way (the compass is still with you)" && KeystoneOfEvents.Narrate(1, "x", -1, -50, 2, "") == "→ for your LONG: HOLD — x is on your side" && KeystoneOfEvents.Narrate(0, "x", 0, 0, 0, "a close above 101") == "→ nothing to do yet — WAIT for a close above 101", "with a position: HOLD / a warning WITH its reason (never a bare get out)");
            var ev = new KeystoneOfEvent { Time = T0, Symbol = "MNQ", Type = "STACKED", Side = 1, Price = 100, Text = "▤ STACKED, for buyers", Narrator = "→ supports BUY" };
            var tk = new List<KeystoneOfTick> { new KeystoneOfTick { Time = T0.AddSeconds(-10), Price = 90, Size = 1, Side = -1 } }; for (int i = 1; i <= 1900; i++) tk.Add(new KeystoneOfTick { Time = T0.AddSeconds(i), Price = 100 + Math.Min(3, i / 100.0), Size = 1, Side = 1 });
            KeystoneOfEvents.Measure(ev, tk, tk.Count, T0.AddSeconds(100), 0.25); Check(ev.Result == 0 && ev.Mae == 0 && !ev.Done && double.IsNaN(ev.M5), "event outcome at 100 s: nothing from before it, nothing after the clock (no look-ahead)");
            KeystoneOfEvents.Measure(ev, tk, tk.Count, T0.AddMinutes(31), 0.25); Check(ev.Result == 1 && ev.Done && Math.Abs(ev.M30 - 3) < 1e-9, "… at 31 min: +8 ticks first, the 30-min move, done");
            var back = KeystoneOfEvents.Parse(KeystoneOfEvents.Line(ev)); Check(back != null && back.Key == ev.Key && back.Result == 1 && back.Narrator == "→ supports BUY" && back.Text == "▤ STACKED; for buyers", "events CSV round-trips");
            var past = new List<KeystoneOfEvent>(); for (int i = 0; i < 25; i++) past.Add(new KeystoneOfEvent { Time = T0.AddMinutes(-60 + i), Type = "STACKED", Side = 1, Result = i < 15 ? 1 : -1 });
            Check(KeystoneOfEvents.Probability(past, "STACKED", 1, T0) == "buyers' stacked led to +8 ticks before −8 in 60 % of 25 cases" && KeystoneOfEvents.Probability(past.Take(10), "STACKED", 1, T0) == "learning (10)" && KeystoneOfEvents.Probability(past, "STACKED", 1, T0.AddMinutes(-50)) == "learning (10)", "a % only from n ≥ 20 decided past events (else learning (n))", KeystoneOfEvents.Probability(past, "STACKED", 1, T0));
            Check(KeystoneOfEvents.Classify("T|1|100", "🪤 trapped buyers • x").Item2 == -1 && KeystoneOfEvents.Classify("BF|1", "🪤 BIG PRINT FAILED • a big seller hit the bid at 1 but the price went UP after it → the seller is trapped; this supports a BUY").Item2 == 1 && KeystoneOfEvents.Classify("C|1", "🧭 the compass turned SELLERS").Item2 == -1 && KeystoneOfEvents.Classify("X|", "").Item1 == "", "events classified from the STORY");
        }
        // ---- 20. 💵 11x setup P/L
        {
            var w = new KeystoneOfSetupRow { Time = T0, Kind = "ZONE STACKED", Dir = 1, Entry = 100, Stop = 98, Target = 103, Qty = 4, Result = 1, Status = "shown" };
            var l = new KeystoneOfSetupRow { Time = T0.AddMinutes(5), Kind = "FLIP", Dir = -1, Entry = 100, Stop = 101.5, Target = 97, Qty = 2, Result = -1, Status = "not offered: 🧘 not offered — learning (2)" };
            var o = new KeystoneOfSetupRow { Time = T0.AddMinutes(9), Kind = "5M CONFIRMED", Dir = 1, Entry = 100, Stop = 88, Target = 130, Qty = 1, Result = 0, Status = "shown", Style = "SWING" };
            Check(KeystoneOfSetupPl.Ticks(w, 0.25) == 12 && KeystoneOfSetupPl.UsdCard(w, 0.25, 2, 1.74) == 12 * 0.25 * 2 * 4 - 1.74 * 4 && KeystoneOfSetupPl.Usd1(w, 0.25, 2, 1.74) == 6 - 1.74, "$ per row = ticks × tick value × contracts − costs (card / 1 contract)");
            Check(KeystoneOfSetupPl.Ticks(l, 0.25) == -6 && Math.Abs(KeystoneOfSetupPl.UsdCard(l, 0.25, 2, 1.74) - (-6 * 0.5 * 2 - 3.48)) < 1e-9 && double.IsNaN(KeystoneOfSetupPl.UsdCard(o, 0.25, 2, 1.74)), "a loss = the stop ticks; an open setup has no $");
            var tt = KeystoneOfSetupPl.Totals(new[] { w, l, o }, 0.25, 2, 1.74);
            Check(tt.Count == 3 && tt.Wins == 1 && tt.Losses == 1 && Math.Abs(tt.TotalUsd - (KeystoneOfSetupPl.UsdCard(w, 0.25, 2, 1.74) + KeystoneOfSetupPl.UsdCard(l, 0.25, 2, 1.74))) < 1e-9 && Math.Abs(tt.TotalUsd1 - (4.26 - 4.74)) < 1e-9 && Math.Abs(tt.TotalR - 0.5) < 1e-9 && tt.WinPct == 50, "totals = Σ the rows (R, $ card, $ per contract, win %)");
            Check(KeystoneOfSetupPl.Shown(w) == "✅ offered" && KeystoneOfSetupPl.Shown(l) == "🧪 learning 2" && KeystoneOfSetupPl.Shown(new KeystoneOfSetupRow { Status = "not offered: against the flow" }) == "✖ not offered" && KeystoneOfSetupPl.ResultWord(o) == "⏳ open" && KeystoneOfSetupPl.TypeOf("5M CONFIRMED") == "CONFIRMED" && KeystoneOfSetupPl.TypeOf("ZONE TRAP") == "ZONE", "SHOWN / RESULT / TYPE columns (no repeated \"not offered — not offered\")");
            var rowTx = KeystoneOfSetupPl.Row(w, 0.25, 2, 1.74); Check(rowTx.Contains("✔ TP") && rowTx.Contains("+12") && rowTx.Contains("+$17") && rowTx.Contains("+$4") && rowTx.Contains("ZONE"), "a table row: result, ticks, $ card, $ 1 contract", rowTx);
            KeystoneOfSetupPl.Fill(w, 0.25, 2, 1.74); var bk = KeystoneOfSetupBook.Parse(KeystoneOfSetupBook.Line(w)); Check(Math.Abs(bk.UsdCard - w.UsdCard) < 0.01 && KeystoneOfSetupBook.Header.EndsWith(",usd_card,usd_1"), "the CSV keeps its columns and adds the $ ones");
            var filt = new[] { w, l, o }.Where(r => KeystoneOfStyle.Of(r) == "SWING").ToList(); Check(filt.Count == 1 && KeystoneOfSetupPl.Totals(filt, 0.25, 2, 1.74).TotalUsd == 0, "the style filter + totals of the filtered rows");
        }
        // ---- 21. ⚡ 11x the longer-scalp rule
        {
            Check(KeystoneOfScalpRule.TargetTicks(12, 0.25, 10, 20) == 40 && KeystoneOfScalpRule.TargetTicks(60, 0.25, 10, 20) == 60 && KeystoneOfScalpRule.TargetTicks(120, 0.25, 10, 20) == 80 && KeystoneOfScalpRule.TargetTicks(8, 0.1, KeystoneOfScalpRule.MinPts("MGC", 0), KeystoneOfScalpRule.MaxPts("MGC", 0)) == 20, "the scalp target clamped: MNQ 10–20 pts (40–80 ticks), MGC 2–4 pts");
            Check(KeystoneOfScalpRule.MinPts("MNQ", 0) == 10 && KeystoneOfScalpRule.MaxPts("MNQ", 0) == 20 && KeystoneOfScalpRule.MinPts("MNQ", 12) == 12, "OF.SCMIN / OF.SCMAX per symbol (defaults 10 / 20 MNQ)");
            var opp = new List<Tuple<double, string>> { Tuple.Create(106.0, "sell zone"), Tuple.Create(95.0, "VAL"), Tuple.Create(130.0, "VAH") };
            Check(KeystoneOfScalpRule.NoRoom(1, 100, 10, opp) == "no room for a 10-pt scalp: the sell zone is 6 pts away" && KeystoneOfScalpRule.NoRoom(-1, 100, 10, opp) == "no room for a 10-pt scalp: the VAL is 5 pts away" && KeystoneOfScalpRule.NoRoom(1, 100, 5, opp) == "", "an opposing level closer than the minimum → no scalp, with the reason");
            Check(KeystoneOfScalpRule.Money(12, 2, 4) == "+12.00 pts • +$24 per contract • +$96 for 4", "the $ text on cards / labels", KeystoneOfScalpRule.Money(12, 2, 4));
            var pl = new KeystoneOfPlan { Dir = 1, Qty = 4, Entry = 100, Stop = 95, Target = 110, StopPts = 5, TargetPts = 10 }; Check(Math.Abs(KeystoneFlowGate.Rr(pl) - 2) < 1e-9 && KeystoneFlowGate.Decide(new KeystoneOfSignal { Bar = 1, Dir = 1, Kind = "STACKED" }, 3, 0, new KeystoneOfPlan { Dir = 1, Entry = 100, Stop = 88, Target = 110, StopPts = 12, TargetPts = 10 }, null, 1).Item1 == KeystoneFlowGate.SkipRr, "min stop 5 pts + a 10-pt scalp = 1:2; a stop wider than the target → skipped (R:R ≥ 1)");
        }
        // ---- 22. 🧭 11x the 5-step guide + the mark rule
        {
            var b = new KeystoneFlowStepInput { ZoneSide = 1, ZoneLo = 28258, ZoneHi = 28262, Price = 28270, Tf = 1, VerdictHead = "⏳ WAIT", VerdictReason = "a confirmed ▲ setup" };
            var s1 = KeystoneFlowSteps.Of(b); Check(s1.Item1[0] == 1 && s1.Item2.StartsWith("① LOCATION — price 8.00 pts above the BUY ZONE 28258.00–28262.00"), "① LOCATION: price away from the zone → wait for it", s1.Item2);
            b.Price = 28260; b.CandleDelta = -180; var s2 = KeystoneFlowSteps.Of(b);
            Check(s2.Item1[0] == 2 && s2.Item1[1] == 1 && s2.Item2 == "② BATTLE — sellers are attacking the BUY ZONE 28258.00–28262.00 (Δ −180 this candle): watch if they fail", "② BATTLE: sellers attacking the buy zone", s2.Item2);
            b.FailureText = "🧱 sellers absorbed at 28259.00"; var s3 = KeystoneFlowSteps.Of(b);
            Check(s3.Item1[2] == 2 && s3.Item1[3] == 1 && s3.Item2 == "③ FAILURE ✔ — 🧱 sellers absorbed at 28259.00; ④ wait for this 1-min candle to close above 28262.00", "③ FAILURE ✔ → ④ wait for the close", s3.Item2);
            b.ZoneConfirmed = true; var s4 = KeystoneFlowSteps.Of(b); Check(s4.Item1[4] == 1 && s4.Item2.Contains("⏳ WAIT") && !s4.Item2.Contains("TAKE"), "④ ✔ but no card → ⑤ says the verdict (never TAKE without a card)", s4.Item2);
            b.CardOffered = true; b.CardLine = "✅ SCALP BUY 4 MNQ — TP +12 pts (+$96), SL −5 pts (−$40): TAKE"; var s5 = KeystoneFlowSteps.Of(b); Check(s5.Item2 == "⑤ ✅ SCALP BUY 4 MNQ — TP +12 pts (+$96), SL −5 pts (−$40): TAKE" && KeystoneFlowSteps.Current(s5.Item1) == 5, "⑤ EXECUTE only with the offered card");
            var s6 = KeystoneFlowSteps.Of(new KeystoneFlowStepInput { InTrade = true, TradeLine = "HOLD — buyers still in control +41" }); Check(s6.Item2 == "HOLD — buyers still in control +41" && s6.Item1.All(v => v == 2), "in a trade the strip is the management line");
            Check(KeystoneFlowSteps.Of(new KeystoneFlowStepInput { Price = 100 }).Item2.StartsWith("① LOCATION — no zone") && KeystoneFlowSteps.Pills(s3.Item1) == "①✔ ②✔ ③✔ ④⏳ ⑤·", "no zone → step ①; the pills", KeystoneFlowSteps.Pills(s3.Item1));
            Check(KeystoneFlowSteps.Bright(28259, 28258, 28262, null, 0.25) && !KeystoneFlowSteps.Bright(28200, 28258, 28262, new[] { 28100.0 }, 0.25) && KeystoneFlowSteps.Bright(28101, double.NaN, double.NaN, new[] { 28100.0 }, 0.25), "marks are bright only near the step's zone / a key level, else dimmed");
        }
        // ---- 23. 🛡 11x a stable in-trade suggestion — the plan decides
        {
            var ad = new KeystoneTradeAdvice(); ad.Update("HOLD", T0); bool changed = false;
            for (int i = 1; i <= 300; i++) { var sh = ad.Update(i % 2 == 0 ? "⛔ GET OUT" : i % 3 == 0 ? "⚠ TIGHTEN" : "HOLD", T0.AddSeconds(i)); if (sh != "HOLD") changed = true; }
            Check(!changed, "noise flipping every second never changes the shown advice");
            var ad2 = new KeystoneTradeAdvice(); ad2.Update("HOLD", T0); string a44 = ad2.Update("⚠ TIGHTEN", T0.AddSeconds(16)); string a61 = ad2.Update("⚠ TIGHTEN", T0.AddSeconds(61)); string a62 = ad2.Update("HOLD", T0.AddSeconds(80)); string a92 = ad2.Update("HOLD", T0.AddSeconds(111)); string a125 = ad2.Update("HOLD", T0.AddSeconds(122));
            Check(a44 == "HOLD" && a61 == "⚠ TIGHTEN" && a62 == "⚠ TIGHTEN" && a92 == "⚠ TIGHTEN" && a125 == "HOLD", "TIGHTEN after 45 s held; back to HOLD after 30 s and the 60-s minimum", a44 + "/" + a61 + "/" + a62 + "/" + a92 + "/" + a125);
            var ad3 = new KeystoneTradeAdvice { Swing = true }; ad3.Update("HOLD", T0); ad3.Update("⚠ TIGHTEN", T0.AddSeconds(1)); Check(ad3.Update("⚠ TIGHTEN", T0.AddSeconds(80)) == "HOLD" && ad3.Update("⚠ TIGHTEN", T0.AddSeconds(125)) == "⚠ TIGHTEN", "a 🏃 swing doubles the times (90 s for TIGHTEN, 120-s minimum)");
            var p1 = KeystoneTradeAdvice.Plan("⛔ GET OUT", 1, 28260, 28258, "the BUY ZONE", 28255, 28265, false, 30, true, true, -24, 40, false);
            Check(p1.Item1 == "⚠ TIGHTEN" && p1.Item2.StartsWith("normal pullback — the plan holds while price stays above 28258.00 (the BUY ZONE)") && p1.Item2.Contains("DRAWDOWN IS NORMAL: −$24 of the planned −$40 risk • the plan holds"), "a red trade inside its plan: TIGHTEN at worst + the plan-holds / drawdown-is-normal text", p1.Item2);
            var p2 = KeystoneTradeAdvice.Plan("⛔ GET OUT", 1, 28256, 28258, "the BUY ZONE", 28250, 28265, true, 0, false, false, -36, 60, false);
            Check(p2.Item1 == "⛔ GET OUT" && p2.Item2.StartsWith("a CLOSED candle broke 28258.00 (the BUY ZONE)"), "GET OUT on a closed-candle break of the invalidation, saying which", p2.Item2);
            var p3 = KeystoneTradeAdvice.Plan("HOLD", 1, 28262, 28258, "z", 28250, 28265, false, 95, true, true, -6, 30, false); var p4 = KeystoneTradeAdvice.Plan("HOLD", 1, 28262, 28258, "z", 28250, 28265, false, 95, true, false, -6, 30, false); var p5 = KeystoneTradeAdvice.Plan("HOLD", 1, 28262, 28258, "z", 28250, 28265, false, 95, true, true, -6, 30, true);
            Check(p3.Item1 == "⛔ GET OUT" && p3.Item2.Contains("against you for 95 s") && p4.Item1 != "⛔ GET OUT" && p5.Item1 != "⛔ GET OUT", "… or the triple condition (flow ≥ 90 s + delta + an event); a swing needs 180 s");
        }
        // ---- 24. 🧼 11y: no "stop 0.00" ever — the plan's invalidation (card stop → zone → NO STOP SET) + the short decision line
        {
            var n1 = KeystoneTradeAdvice.Plan("⚠ TIGHTEN", 1, 28220, double.NaN, "", 0, 28230, false, 0, false, false, -26, 0, false);
            Check(!n1.Item2.Contains("0.00") && n1.Item2.StartsWith(KeystoneOfSimple.NoStop) && !n1.Item2.Contains("plan holds"), "no stop + no invalidation: \"⚠ NO STOP SET — set your stop\", never \"stop 0.00\", no \"plan holds\"", n1.Item2);
            var iv1 = KeystoneOfSimple.Invalidation(1, 28205, 28210); var iv2 = KeystoneOfSimple.Invalidation(1, double.NaN, 28210); var iv3 = KeystoneOfSimple.Invalidation(-1, 0, double.NaN);
            Check(iv1.Item1 == 28205 && iv1.Item2 == "the setup's stop" && iv2.Item1 == 28210 && iv2.Item2 == "the BUY ZONE" && double.IsNaN(iv3.Item1), "invalidation: the card's stop first, else the zone, else none");
            var n2 = KeystoneTradeAdvice.Plan("⚠ TIGHTEN", 1, 28220, iv1.Item1, iv1.Item2, 0, 28230, false, 0, false, false, -26, 0, false);
            Check(n2.Item2.Contains("above 28205.00 (the setup's stop)") && !n2.Item2.Contains("your stop 0.00"), "no stop but a card: the plan holds above the card's stop", n2.Item2);
            Check(KeystoneOfSimple.Holds(1, 0, "x") == KeystoneOfSimple.NoStop && KeystoneOfSimple.Holds(-1, 28300, "the SELL ZONE") == "the plan holds while price stays below 28300.00 (the SELL ZONE)", "Holds(): never a 0 price");
            Check(KeystoneOfSimple.StopPrice(1, 28230, 0, 0, null) == 0 && KeystoneOfSimple.StopPrice(1, 28230, 10, 0, null) == 28220 && KeystoneOfSimple.StopPrice(-1, 28230, 0, 6, null) == 28236 && KeystoneOfSimple.StopPrice(1, 28230, 0, 0, new[] { 0.0, 28210 }) == 28210, "the working stop: panel → trader → a STOP order; none = 0");
            string t1 = KeystoneOfSimple.InTrade("⚠ TIGHTEN", 1, double.NaN, false), t2 = KeystoneOfSimple.InTrade("⚠ TIGHTEN", 1, 28205, false), t3 = KeystoneOfSimple.InTrade("⛔ GET OUT", -1, 28300, true);
            Check(t1.StartsWith("⚠ NO STOP SET") && !t1.Contains("0.00") && t2 == "⚠ TIGHTEN • pullback, plan holds above 28205.00" && t3 == "⛔ GET OUT • a candle closed above 28300.00", "the in-trade line: NO STOP SET first / the level / the closed break", t1 + " | " + t2 + " | " + t3);
            Func<string, bool> oneLine = l => !l.Contains("\n") && (l.Contains(" • ") ? l.Substring(l.IndexOf(" • ") + 3).Split(' ').Length <= KeystoneOfSimple.MaxWords : true);
            string w1 = KeystoneOfSimple.Line("⏳ WAIT", "sellers are attacking the BUY ZONE 28258.00–28262.00 (Δ −180 this candle): watch if they fail\nmore"), w2 = KeystoneOfSimple.Card(true, "SCALP", 1, 4), w3 = KeystoneOfSimple.Line("⛔ AVOID", "balanced / chop — nobody is in control (score +3)");
            Check(w1.StartsWith("⏳ WAIT • ") && oneLine(w1) && w2 == "✅ ⚡ SCALP BUY 4 • TAP for details" && w3 == "⛔ AVOID • balanced / chop" && oneLine(t2), "the decision line: the verdict word first, ONE line, ≤ 8 words", w1 + " | " + w3);
            var si = new KeystoneFlowStepInput { ZoneSide = 1, ZoneLo = 28258, ZoneHi = 28262, Price = 28260, CandleDelta = -180 };
            Check(KeystoneOfSimple.Line("⏳ WAIT", KeystoneOfSimple.StepPhrase(si, 2)) == "⏳ WAIT • sellers attacking the buy zone" && KeystoneOfSimple.StepPhrase(new KeystoneFlowStepInput(), 1) == "no zone near the price yet", "the WAIT reason from the 5 steps");
            Check(KeystoneOfSimple.Allowed(null) && KeystoneOfSimple.Allowed("gutter-label") && KeystoneOfSimple.Allowed("of.decision") && KeystoneOfSimple.Allowed("of.zone.simple") && !KeystoneOfSimple.Allowed("of.toast") && !KeystoneOfSimple.Allowed("of.vp") && !KeystoneOfSimple.Allowed("of.trademap.label") && !KeystoneOfSimple.Allowed("layerbar.PROF"), "the SIMPLE plot's allowed tags");
        }
        // ---- 25. 📚 11y: the DOM for beginners — walls, pulled walls, refills (icebergs), the totals text, the no-depth message
        {
            var T = new DateTime(2026, 11, 12, 10, 0, 0); var dt = new KeystoneDomTracker();
            Func<long, Dictionary<double, long>> bidsW = w => new Dictionary<double, long> { { 100.00, 10 }, { 99.75, 10 }, { 99.50, w }, { 99.25, 10 } };
            var asks = new Dictionary<double, long> { { 100.25, 10 }, { 100.50, 10 }, { 100.75, 10 } };
            Check(KeystoneDomTracker.IsWall(70, 15) && !KeystoneDomTracker.IsWall(40, 15) && !KeystoneDomTracker.IsWall(4, 1), "a wall = ≥ 3× the average level (and ≥ 5)");
            dt.Update(T, bidsW(70), asks, null); dt.Update(T.AddSeconds(1), bidsW(20), asks, new Dictionary<double, long>());
            Check(dt.Events.Count == 1 && dt.Events[0].Item3 == "a 70-lot buy wall at 99.50 was pulled — not real support" && dt.Pulled(1, 99.50, T.AddSeconds(2)), "a wall losing ≥ 60 % without trades there = PULLED (journaled)", string.Join(" / ", dt.Events.Select(e => e.Item3)));
            var dt2 = new KeystoneDomTracker(); dt2.Update(T, bidsW(70), asks, null); dt2.Update(T.AddSeconds(1), bidsW(20), asks, new Dictionary<double, long> { { 99.50, 50 } });
            Check(dt2.Events.Count == 0 && !dt2.Pulled(1, 99.50, T.AddSeconds(2)), "the same drop EATEN by trades is not 'pulled'");
            var dt3 = new KeystoneDomTracker(); dt3.Update(T, bidsW(30), asks, null);
            for (int i = 1; i <= 3; i++) dt3.Update(T.AddSeconds(i), bidsW(30), asks, new Dictionary<double, long> { { 99.50, 25 } });
            Check(dt3.Refill(1, 99.50) && dt3.Events.Any(e => e.Item3.Contains("possible iceberg")), "a level hit by trades that keeps coming back = 🧊 refill (possible iceberg)");
            var lad = dt.Ladder(100.00, 0.25, 15, bidsW(70), asks, new Dictionary<double, long[]> { { 100.00, new long[] { 5, 3 } } }, T.AddSeconds(2));
            Check(lad.Count == 31 && lad[0].Price == 103.75 && lad[30].Price == 96.25 && lad.First(r => r.Price == 99.50).Wall && lad.First(r => r.Price == 100.00).Bought == 5, "the ladder: ±15 tick rows centred on the last price, walls flagged, traded column", lad.Count + " " + lad[0].Price);
            var tt = KeystoneDomTracker.Totals(1240, 980);
            Check(tt.Item1 == "Waiting to BUY 1,240 • waiting to SELL 980 → more buyers waiting (56 %)" && tt.Item2 != "", "the totals in plain words", tt.Item1);
            Check(KeystoneDomTracker.NoDepth(true, 0, 12, "") == "your data connection isn't sending market depth — the DOM needs Level 2 data" && KeystoneDomTracker.NoDepth(true, 0, 3, "").StartsWith("waiting") && KeystoneDomTracker.NoDepth(true, 5, 30, "") == "" && KeystoneDomTracker.NoDepth(false, 0, 0, "").Contains("only LIVE"), "no depth within 10 s = says so plainly");
        }
        // ---- 26. 🎯 11y: events "for your trade" + opposite events = ONE chop line + the in-trade distances
        {
            var T = new DateTime(2026, 11, 12, 10, 0, 0);
            string f1 = KeystoneOfTradeEvents.ForTrade(2, 28198.375, 1, "seller failed"), f2 = KeystoneOfTradeEvents.ForTrade(2, 28198.375, -1, "buyer failed"), f3 = KeystoneOfTradeEvents.ForTrade(-1, 28200, 0, "the compass");
            Check(f1 == "for your LONG 2 @ 28198.38: supports HOLD — seller failed is on your side" && f2.StartsWith("for your LONG 2 @ 28198.38: against you — buyer failed counts toward ⚠ TIGHTEN") && f3.StartsWith("for your SHORT 1 @ 28200.00: neutral") && KeystoneOfTradeEvents.ForTrade(0, 1, 1, "x") == "", "the per-trade line: supports HOLD / against you → TIGHTEN / neutral", f1 + " | " + f2);
            var evs = new List<KeystoneOfEvent> {
                new KeystoneOfEvent { Time = T, Type = "FAILED PRINT", Side = -1, Price = 28200, Text = "🪤 buyer failed → SELL pressure" },
                new KeystoneOfEvent { Time = T.AddMinutes(20), Type = "FAILED PRINT", Side = 1, Price = 28201, Text = "🪤 seller failed → BUY pressure" },
                new KeystoneOfEvent { Time = T.AddMinutes(25), Type = "ABSORPTION", Side = 1, Price = 28190, Text = "🧱 absorbed" } };
            var m = KeystoneOfTradeEvents.Merge(evs, 0.25, 45);
            Check(m.Count == 2 && m.Count(x => x.Item3.Count == 2) == 1 && m.First(x => x.Item3.Count == 2).Item2.StartsWith("⚖ both sides failed here (28200.00–28201.00) → balance / chop: no new information — follow your plan (stop / target)"), "opposite failed prints within 3 candles / a few ticks = ONE chop line", string.Join(" / ", m.Select(x => x.Item2)));
            var m2 = KeystoneOfTradeEvents.Merge(evs, 0.25, 5); var m3 = KeystoneOfTradeEvents.Merge(new List<KeystoneOfEvent> { evs[0], new KeystoneOfEvent { Time = T.AddMinutes(1), Type = "FAILED PRINT", Side = 1, Price = 28210, Text = "far" } }, 0.25, 45);
            Check(m2.Count == 3 && m3.Count == 2, "too far apart in time or price = two lines");
            string d1 = KeystoneOfTradeEvents.Distances(1, 28200, 28190, 28230, 2, 2), d2 = KeystoneOfTradeEvents.Distances(1, 28200, 0, 0, 2, 2);
            Check(d1 == "STOP 28190.00 • −10.00 pts • −$40\nTARGET 28230.00 • +30.00 pts • +$120" && d2 == KeystoneOfSimple.NoStop + "\nno target set" && !d2.Contains("0.00"), "the in-trade box: stop / target with pts and $ — or NO STOP SET", d1 + " | " + d2);
        }
        Console.WriteLine(failures == 0 ? "ALL FLOW COMPASS TESTS PASSED" : failures + " FLOW COMPASS TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
