// FVG retest strategy tests on hand-made candles (docs/FVG.md scenarios).
// Run: tools/compile_engine.sh tests/.build/fvg.exe tests/FvgTests.cs && mono tests/.build/fvg.exe
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class FvgTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail));
        if (!ok) failures++;
    }

    static readonly DateTime T0 = new DateTime(2026, 3, 10, 9, 0, 0);
    // Candles are 1 minute apart; o/h/l/c given directly.
    static List<KeystoneArcBar> Bars(params double[][] ohlc)
    {
        var list = new List<KeystoneArcBar>();
        for (int i = 0; i < ohlc.Length; i++)
            list.Add(new KeystoneArcBar { Symbol = "MGC", Time = T0.AddMinutes(i), Open = ohlc[i][0], High = ohlc[i][1], Low = ohlc[i][2], Close = ohlc[i][3] });
        return list;
    }
    static double[] C(double o, double h, double l, double c) { return new[] { o, h, l, c }; }

    static KeystoneArcRunConfig Cfg()
    {
        return new KeystoneArcRunConfig
        {
            StrategyCode = "FVG", Scope = "MGC", SetupMinutes = 1, SessionMode = "CUSTOM", CustomStart = 800, EndTime = 1655,
            Start = T0.Date, End = T0.Date.AddDays(1), TargetDollars = 100, StopDollars = 50, Quantity = 1, StopMode = "STANDARD", OutcomeModelEnabled = 1, FvgMaxEntriesPerBox = 1, FvgStopMode = "FIXED"
        };
    }

    // Drop (3 red), box forms between candle 1 high (100) and candle 3 low (102), then a pullback.
    static List<double[]> Base()
    {
        return new List<double[]>
        {
            C(110, 110.5, 107, 107.5), C(107.5, 108, 104, 104.5), C(104.5, 105, 99, 99.5), // red run
            C(99.5, 100, 98, 99.8),     // candle 1 (high 100)
            C(99.8, 104, 99.6, 103.8),  // candle 2 big green
            C(103.8, 105, 102, 104.6),  // candle 3 (low 102) → box 100..102
            C(104.6, 105.2, 103.5, 104.0)
        };
    }

    static List<KeystoneArcFvgZone> Scan(List<double[]> c, KeystoneArcRunConfig cfg, out List<KeystoneArcEngine.FvgScanEntry> entries)
    {
        entries = new List<KeystoneArcEngine.FvgScanEntry>();
        return KeystoneArcEngine.FvgScan(Bars(c.ToArray()), cfg, "MGC", entries);
    }

    public static int Main()
    {
        List<KeystoneArcEngine.FvgScanEntry> en;
        // 1. Screenshot 1: a candle wicks deep into the box and closes green; next candle breaks its high.
        {
            var c = Base(); c.Add(C(104.0, 104.2, 100.8, 103.0)); /* red dip to 100.8 (60% deep) */ c.Add(C(103.0, 103.6, 101.5, 103.4)); /* green close → reference 103.6 */ c.Add(C(103.4, 104.5, 103.2, 104.3)); /* breaks 103.6 */
            var zones = Scan(c, Cfg(), out en);
            Check(zones.Count >= 1 && Math.Abs(zones[0].Lower - 100) < 1e-9 && Math.Abs(zones[0].Upper - 102) < 1e-9, "box = candle 1 high (100) → candle 3 low (102)", zones.Count == 0 ? "none" : zones[0].Lower + "–" + zones[0].Upper);
            Check(en.Count == 1 && Math.Abs(en[0].Entry - 103.6) < 1e-9 && en[0].TriggerIndex == 9, "entry at the green candle's high when the next candle breaks it", en.Count == 0 ? "no entry" : en[0].Entry + " @" + en[0].TriggerIndex);
            Check(zones[0].RedRun >= 3 && zones[0].Aggressive, "aggression recorded: red run before the box", zones[0].RedRun + " red, drop " + zones[0].Drop);
            Check(zones[0].EndReason == "USED", "box is used after one entry (default 1 per box)", zones[0].EndReason);
        }
        // 2. The dip candle itself closes green → it is the confirmation.
        {
            var c = Base(); c.Add(C(103.0, 103.9, 100.9, 103.5)); c.Add(C(103.5, 104.2, 103.3, 104.0));
            Scan(c, Cfg(), out en);
            Check(en.Count == 1 && Math.Abs(en[0].Entry - 103.9) < 1e-9, "green dip candle is its own confirmation", en.Count.ToString());
        }
        // 3. A close below the box kills it.
        {
            var c = Base(); c.Add(C(104.0, 104.1, 99.0, 99.5)); c.Add(C(99.5, 101.5, 99.4, 101.3)); c.Add(C(101.3, 103, 101, 102.8));
            var zones = Scan(c, Cfg(), out en);
            Check(en.Count == 0 && zones[0].EndReason == "CLOSED BELOW", "close below the box → no entry", zones[0].EndReason);
        }
        // 4. Shallow touch (10% into the box) is not a retest at the default 25% depth; 0% accepts it.
        {
            var c = Base(); c.Add(C(103.0, 103.5, 101.8, 103.2)); c.Add(C(103.2, 104.0, 103.0, 103.8));
            Scan(c, Cfg(), out en);
            Check(en.Count == 0, "10% touch ignored at 25% depth", en.Count.ToString());
            var cfg = Cfg(); cfg.FvgMinDepthPercent = 0; Scan(c, cfg, out en);
            Check(en.Count == 1, "depth 0 = any touch counts", en.Count.ToString());
        }
        // 5. Green candle not broken: default needs a new dip; option 0 lets a later green close re-arm.
        {
            var c = Base(); c.Add(C(103.0, 103.6, 100.8, 103.4)); /* dip+green ref 103.6 */ c.Add(C(103.4, 103.5, 102.6, 102.8)); /* no break, red */ c.Add(C(102.8, 103.7, 102.7, 103.6)); /* green, no dip */ c.Add(C(103.6, 104.5, 103.5, 104.4)); /* would break 103.7 */
            Scan(c, Cfg(), out en);
            Check(en.Count == 0, "default: after a missed break a new dip is required", en.Count.ToString());
            var cfg = Cfg(); cfg.FvgNeedNewDipAfterMiss = 0; Scan(c, cfg, out en);
            Check(en.Count == 1 && Math.Abs(en[0].Entry - 103.7) < 1e-9, "option: a later green close re-arms without a new dip", en.Count == 0 ? "none" : en[0].Entry.ToString());
        }
        // 6. Run-away: price goes 2+ box heights above (above 106) → box retired; 0 = never retire.
        {
            var c = Base(); c.Add(C(104.0, 106.5, 103.9, 106.4)); c.Add(C(106.4, 108.0, 106.3, 107.8)); /* whole candle above 106 */ c.Add(C(107.8, 107.9, 100.9, 103.0)); c.Add(C(103.0, 103.6, 102.5, 103.5)); c.Add(C(103.5, 104.4, 103.4, 104.2));
            var zones = Scan(c, Cfg(), out en);
            Check(en.Count == 0 && zones[0].EndReason == "RUN AWAY", "run-away above 2× box height retires the box", zones[0].EndReason);
            var cfg = Cfg(); cfg.FvgRunAwayMultiple = 0; Scan(c, cfg, out en);
            Check(en.Count == 1, "run-away 0: box stays until a close below", en.Count.ToString());
        }
        // 6b. A candle that dips into the box is never "run away", even with a high far above
        //     (user's 2026-09-23 MGC 10:55 setup: small box, the dip candle spiked above 2× box height).
        {
            var c = Base(); c.Add(C(104.0, 109.0, 101.0, 103.0)); /* high 109 > 106 but dips to 101 */ c.Add(C(103.0, 103.6, 102.2, 103.5)); /* green → ref 103.6 */ c.Add(C(103.5, 104.4, 103.4, 104.2));
            var zones = Scan(c, Cfg(), out en);
            Check(en.Count == 1 && Math.Abs(en[0].Entry - 103.6) < 1e-9, "a dip candle with a big high still counts: entry at the next break", en.Count + " / " + zones[0].EndReason);
        }
        // 7. Two entries per box: second visit gives a second entry (screenshot 2).
        {
            var c = Base(); c.Add(C(103.0, 103.6, 100.8, 103.4)); c.Add(C(103.4, 104.0, 103.3, 103.5)); /* entry 1 at 103.6 */
            c.Add(C(103.5, 103.6, 100.6, 101.0)); /* dip again, red */ c.Add(C(101.0, 102.9, 100.9, 102.7)); /* green ref 102.9 */ c.Add(C(102.7, 103.5, 102.6, 103.3)); /* entry 2 */
            var cfg = Cfg(); cfg.FvgMaxEntriesPerBox = 2; var zones = Scan(c, cfg, out en);
            Check(en.Count == 2 && en[1].Visit == 2 && Math.Abs(en[1].Entry - 102.9) < 1e-9, "max 2 per box: second visit gives a second entry", string.Join(",", en.Select(x => x.Entry + "/v" + x.Visit)));
            Scan(c, Cfg(), out en);
            Check(en.Count == 1, "default 1 per box: only the first entry", en.Count.ToString());
        }
        // 8. Aggression REQUIRED drops boxes without a red run; TAG keeps them untagged.
        {
            var c = new List<double[]> { C(100, 100.4, 99.8, 100.2), C(100.2, 100.6, 100.0, 100.5), C(100.5, 101, 100.3, 100.9), C(100.9, 104, 100.8, 103.8), C(103.8, 105, 102, 104.6), C(104.0, 104.2, 101.4, 103.0), C(103.0, 103.6, 101.5, 103.4), C(103.4, 104.5, 103.2, 104.3) };
            var tag = Cfg(); tag.FvgRunAwayMultiple = 0; tag.FvgMergeStacked = 0; // this small box would otherwise be retired by the 104.2 high
            var zones = Scan(c, tag, out en);
            Check(zones.Count >= 1 && zones.All(z => !z.Aggressive) && en.Count == 1, "TAG: box without aggression is kept, marked BASE", zones.Count + " / " + en.Count);
            var cfg = Cfg(); cfg.FvgRunAwayMultiple = 0; cfg.FvgMergeStacked = 0; cfg.FvgAggressionMode = "REQUIRED"; zones = Scan(c, cfg, out en);
            Check(zones.Count == 0 && en.Count == 0, "REQUIRED: box without aggression is ignored", zones.Count.ToString());
        }
        // 9. Minimum gap: a 2-point box is skipped when the minimum is 3.
        {
            var cfg = Cfg(); cfg.FvgMgcMinGap = 3; var c = Base(); var zones = Scan(c, cfg, out en);
            Check(zones.All(z => z.Height >= 3), "minimum gap filters small boxes", string.Join(",", zones.Select(z => z.Height)));
        }
        // 10. Full pipeline: DetectAndResolve builds FVG events and resolves them on 1-minute bars.
        {
            var c = Base(); c.Add(C(104.0, 104.2, 100.8, 103.0)); c.Add(C(103.0, 103.6, 101.5, 103.4)); c.Add(C(103.4, 104.5, 103.2, 104.3));
            c.Add(C(104.3, 110.0, 104.0, 109.5)); // runs to target (MGC $10/pt, $100 target = 10 pts → 113.6)… keep going
            c.Add(C(109.5, 114.0, 109.0, 113.8));
            var bars = Bars(c.ToArray());
            var cfg = Cfg();
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(ev.Count == 1 && ev[0].SetupClass == "FVG" && ev[0].Outcome == "WIN" && Math.Abs(ev[0].GrossPnl - 100) < 0.01, "FVG event resolves on 1-minute bars (fixed $ target)", ev.Count == 0 ? "none" : ev[0].Outcome + " " + ev[0].GrossPnl);
            Check(ev.Count == 1 && ev[0].FvgVisit == 1 && Math.Abs(ev[0].FvgGap - 2) < 1e-9 && ev[0].StrengthTag == "AGGR", "event records visit, box size, aggression", ev.Count == 0 ? "" : ev[0].ReviewNote);
            // Box stop: stop below the box (100), risk-sized, price-based P/L on a loss.
            var lossBars = Base(); lossBars.Add(C(104.0, 104.2, 100.8, 103.0)); lossBars.Add(C(103.0, 103.6, 101.5, 103.4)); lossBars.Add(C(103.4, 104.0, 103.2, 103.5)); lossBars.Add(C(103.5, 103.6, 101.0, 101.2)); lossBars.Add(C(101.2, 101.3, 99.5, 100.1));
            // (the dip below 100 closes above 100, so the box is still valid when the stop hits)
            var lb = Bars(lossBars.ToArray());
            var cfgBox = Cfg(); cfgBox.FvgStopMode = "BOX";
            var ev2 = KeystoneArcEngine.DetectAndResolve(lb, lb, cfgBox);
            double expectedQty = Math.Max(1, Math.Floor(50 / ((103.6 - 100) * 10)));
            Check(ev2.Count == 1 && Math.Abs(ev2[0].Stop - 100) < 1e-9 && ev2[0].Outcome.StartsWith("LOSS") && Math.Abs(ev2[0].GrossPnl + (103.6 - 100) * 10 * expectedQty) < 0.01, "BOX stop: stop at box bottom, loss = real price move × size", ev2.Count == 0 ? "none" : ev2[0].Stop + " " + ev2[0].Outcome + " " + ev2[0].GrossPnl);
        }
        // 10b. Defaults for the lab: 3 tries per box (fake-out retry) and the stop below the 3 FVG candles, auto-sized.
        {
            var def = new KeystoneArcRunConfig();
            Check(def.FvgMaxEntriesPerBox == 3 && def.FvgStopMode == "FORMATION_LOW", "defaults: 3 tries per box, stop below the FVG candles", def.FvgMaxEntriesPerBox + " / " + def.FvgStopMode);
            // Fake-out: entry 1 stops out without closing below the box, a new dip + green + break gives entry 2.
            var c = Base(); c.Add(C(104.0, 104.2, 100.8, 103.0)); c.Add(C(103.0, 103.6, 101.5, 103.4)); c.Add(C(103.4, 104.0, 103.2, 103.5)); // entry 1 @103.6
            c.Add(C(103.5, 103.6, 100.2, 100.6)); // dips, closes inside the box (not below 100)
            c.Add(C(100.6, 102.4, 100.4, 102.2)); // green → ref 102.4
            c.Add(C(102.2, 103.0, 102.0, 102.9)); // entry 2 @102.4
            var cfg = Cfg(); cfg.FvgMaxEntriesPerBox = 3; Scan(c, cfg, out en);
            Check(en.Count == 2 && Math.Abs(en[1].Entry - 102.4) < 1e-9 && en[1].Visit == 2, "fake-out: the same box gives a second try after the first", string.Join(",", en.Select(x => x.Entry)));
            // Stop below the lowest low of the 3 FVG candles (98), size = floor(risk / (distance × $10)).
            var bars = Bars(Base().Concat(new[] { C(104.0, 104.2, 100.8, 103.0), C(103.0, 103.6, 101.5, 103.4), C(103.4, 104.5, 103.2, 104.3), C(104.3, 104.4, 97.0, 97.5) }).ToArray());
            var cf = Cfg(); cf.FvgStopMode = "FORMATION_LOW"; cf.StopDollars = 200;
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cf);
            double qty = Math.Floor(200 / ((103.6 - 98) * 10));
            Check(ev.Count == 1 && Math.Abs(ev[0].Stop - 98) < 1e-9 && Math.Abs(ev[0].Quantity - qty) < 1e-9 && Math.Abs(ev[0].GrossPnl + (103.6 - 98) * 10 * qty) < 0.01, "stop below the FVG candles' lowest low (98), contracts sized to the $200 risk", ev.Count == 0 ? "none" : ev[0].Stop + " x" + ev[0].Quantity + " " + ev[0].GrossPnl);
        }
        // 12. Stacked FVGs (in a row) = one zone; the dip must reach its middle. Every zone keeps a log.
        {
            var c = new List<double[]> { C(100, 100.4, 99.8, 100.2), C(100.2, 100.6, 100.0, 100.5), C(100.5, 101, 100.3, 100.9), C(100.9, 104, 100.8, 103.8), C(103.8, 105, 102, 104.6) };
            // zone 100.6–102 (two boxes), middle 101.3
            var shallow = new List<double[]>(c) { C(104.0, 104.2, 101.4, 103.0), C(103.0, 103.6, 101.5, 103.4), C(103.4, 104.5, 103.2, 104.3) };
            var cfg = Cfg(); cfg.FvgRunAwayMultiple = 0;
            var zones = Scan(shallow, cfg, out en);
            Check(zones.Count == 1 && zones[0].Stacked == 2 && Math.Abs(zones[0].Lower - 100.6) < 1e-9 && Math.Abs(zones[0].Upper - 102) < 1e-9, "two FVGs in a row merge into one zone", zones.Count + " zones");
            Check(en.Count == 0 && zones[0].Log.Any(l => l.Contains("touch only") && l.Contains("needs 50%")), "a dip above the middle of the stacked zone is not a retest (log says why)", string.Join(" | ", zones[0].Log));
            var deep = new List<double[]>(c) { C(104.0, 104.2, 101.1, 103.0), C(103.0, 103.6, 101.5, 103.4), C(103.4, 104.5, 103.2, 104.3) };
            zones = Scan(deep, cfg, out en);
            Check(en.Count == 1 && zones[0].Log.Any(l => l.Contains("ENTRY")), "dip to the middle of the stacked zone → entry", string.Join(" | ", zones[0].Log));
            cfg.FvgMergeStacked = 0; zones = Scan(shallow, cfg, out en);
            Check(zones.Count == 2, "stacking off: two separate boxes", zones.Count.ToString());
        }
        // 13. DOUBLE TROUBLE: aggression before the box + red candle, green reference, break (= BH) on the FVG entry.
        {
            var c = Base(); c.Add(C(104.0, 104.2, 100.8, 103.0)); /* red dip */ c.Add(C(103.0, 103.6, 101.5, 103.4)); /* green ref */ c.Add(C(103.4, 104.5, 103.2, 104.3)); c.Add(C(104.3, 110, 104, 109)); c.Add(C(109, 114, 108.5, 113.9));
            var bars = Bars(c.ToArray());
            var cfg = Cfg();
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(ev.Count == 1 && ev[0].QualityTier == "DT" && (ev[0].ReviewNote ?? "").Contains("DOUBLE TROUBLE"), "aggressive box + red/green/break = DOUBLE TROUBLE (top grade)", ev.Count == 0 ? "none" : ev[0].QualityTier + " " + ev[0].ReviewNote);
            var c2 = Base(); c2[6] = C(104.0, 105.2, 103.5, 104.6); /* green */ c2.Add(C(103.0, 104.2, 100.8, 103.3)); /* green dip = reference */ c2.Add(C(103.3, 104.5, 103.2, 104.3)); /* break */
            var ev2 = KeystoneArcEngine.DetectAndResolve(Bars(c2.ToArray()), Bars(c2.ToArray()), cfg);
            Check(ev2.Count == 1 && ev2[0].QualityTier != "DT", "no red candle before the green reference → not DOUBLE TROUBLE", string.Join(",", ev2.Select(e => e.QualityTier)));
            Check(KeystoneArcQualityLearner.TierAllowed("DT", "DT") && !KeystoneArcQualityLearner.TierAllowed("DT", "A") && KeystoneArcQualityLearner.TierAllowed("A", "DT") && KeystoneArcQualityLearner.TierAllowed("AB", "DT"), "filters: DT only; A and A+B include DT", "");
        }
        // 14. POINTS sizing per grade with the user's contract size: DT stop 10 / target 15.
        {
            var c = Base(); c.Add(C(104.0, 104.2, 100.8, 103.0)); c.Add(C(103.0, 103.6, 101.5, 103.4)); c.Add(C(103.4, 104.5, 103.2, 104.3)); c.Add(C(104.3, 119, 104, 118.8));
            var bars = Bars(c.ToArray());
            var cfg = Cfg(); cfg.FvgStopMode = "POINTS"; cfg.Quantity = 10;
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(ev.Count == 1 && Math.Abs(ev[0].Stop - (103.6 - 10)) < 1e-9 && Math.Abs(ev[0].Target - (103.6 + 15)) < 1e-9 && ev[0].Outcome == "WIN" && Math.Abs(ev[0].GrossPnl - 15 * 10 * 10) < 0.01, "DT with POINTS: stop 10, target 15 points, 10 MGC = +$1,500", ev.Count == 0 ? "none" : ev[0].Stop + "/" + ev[0].Target + " " + ev[0].Outcome + " " + ev[0].GrossPnl);
        }
        // 11. BH strategy is untouched by the FVG code path.
        {
            var c = Base(); c.Add(C(104.0, 104.2, 100.8, 103.0)); c.Add(C(103.0, 103.6, 101.5, 103.4)); c.Add(C(103.4, 104.5, 103.2, 104.3));
            var bars = Bars(c.ToArray());
            var cfg = Cfg(); cfg.StrategyCode = "BH"; cfg.EnableBh = 1; cfg.EnableFvg = 0;
            var ev = KeystoneArcEngine.DetectAndResolve(bars, bars, cfg);
            Check(ev.All(e => e.SetupClass == "BH" || e.SetupClass == "BL"), "BH run produces only BH setups", string.Join(",", ev.Select(e => e.SetupClass)));
        }
        Console.WriteLine(failures == 0 ? "\nALL FVG TESTS PASSED" : "\n" + failures + " FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
