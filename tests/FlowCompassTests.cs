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
            Check(back.Style == "SWING" && back.Key.EndsWith("|SWING") && old.Style == "SCALP" && KeystoneOfSetupBook.Header.EndsWith(",style"), "📒 SETUPS records the style (old files read as SCALP)");
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
        Console.WriteLine(failures == 0 ? "ALL FLOW COMPASS TESTS PASSED" : failures + " FLOW COMPASS TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
