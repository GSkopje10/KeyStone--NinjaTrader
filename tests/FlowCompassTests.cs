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
            KeystoneOfExport.Build(dir, new[] { "MNQ" }, new[] { day }, new[] { "ACC1" }, "time_ny,text\n2026-03-09 10:00:00,x\n", "", zip, prog.Add);
            var z = KeystoneZip.Read(zip); string rd = System.Text.Encoding.UTF8.GetString(z["README.txt"]);
            Check(z.ContainsKey("ticks/MNQ/2026-03-09.csv.gz") && z.ContainsKey("setups/setups-MNQ-2026-03-09.csv") && z.ContainsKey("story.csv") && !z.ContainsKey("compass.csv"), "the bundle: ticks, setups, story; an empty piece left out", string.Join(",", z.Keys));
            Check(rd.Contains("compass.csv") && rd.Contains("no compass readings yet") && rd.Contains("no 🧾 EXECUTIONS days saved for ACC1") && rd.Contains("no saved 1-minute candles for MNQ") && prog.Contains("done"), "README describes every file + notes the skipped pieces; progress reported");
        }
        // ---- 12. palette
        Check(KeystoneOfPalette.Names.Length == 3 && KeystoneOfPalette.Buy(0) == "#26C6DA" && KeystoneOfPalette.Sell(0) == "#FF5C8A" && KeystoneOfPalette.Buy(9) == KeystoneOfPalette.Buy(0), "three palettes, TEAL / CORAL default");

        Console.WriteLine(failures == 0 ? "ALL FLOW COMPASS TESTS PASSED" : failures + " FLOW COMPASS TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
