// THE BUILT-IN ENTRY INDICATOR: learned on Oct 2020 – 2023 (the app's own engine: every setup moment × all 4 sides with +$1,500 /
// −$1,000 on 10 MNQ + 10 MGC, the window and session choice checked on the later half of those years), then judged on 2024 – 2026
// days it never saw: the target-hit rate against random sides, and whole pass-first prop runs (50 evaluations) against 10 random seeds.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class IndicatorBuild
{
    public static void Run(List<RDay> mnq, List<RDay> mgc, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = m => { Console.WriteLine(m); log.AppendLine(m); };
        var gm = mgc.ToDictionary(d => d.Day.Date, d => d);
        var days = mnq.Where(m => gm.ContainsKey(m.Day.Date)).Select(m => RotationEntries.Day.Of(m, gm[m.Day.Date]).R).OrderBy(d => d.Day).ToList();
        var cut = new DateTime(2024, 1, 1); var train = days.Where(d => d.Day < cut).ToList(); var test = days.Where(d => d.Day >= cut).ToList();
        var s = new KeystoneSimSettings { Strategy = "ROTATION", Accounts = 50, MnqQty = 10, MgcQty = 10, FundMnqQty = 10, FundMgcQty = 10, RotTarget = 1500, RotStop = 1000, FundRotTarget = 1500, FundRotStop = 1000,
            StartHhmm = 1800, EndHhmm = 1555, MaxTradesPerDay = 1, DayPlan = "ONEWIN", BuyRule = "BREAKEVEN", FundedWhen = "ASK", Rules = KeystoneSimRules.Presets()[1].Copy() };
        say("BUILT-IN INDICATOR • train " + train[0].Day.ToString("yyyy-MM-dd") + " → " + train[train.Count - 1].Day.ToString("yyyy-MM-dd") + " (" + train.Count + " days) • test " + test[0].Day.ToString("yyyy-MM-dd") + " → " + test[test.Count - 1].Day.ToString("yyyy-MM-dd") + " (" + test.Count + " days)");
        var ind = KeystoneEntryIndicator.Build(s, train, 1, 60, 2, null); ind.Source = "built in: 6 years of MNQ + MGC";
        say(ind.Describe()); say("versions on the later half: " + ind.Variants);
        foreach (var r in ind.Rules) say("  " + KeystoneEntryIndicator.StateName(r.Key, ind.Window) + " → " + KeystoneSimRotation.SidesName(r.DirM, r.DirG) + " • " + r.Trades + " • " + r.WinPct.ToString("0.0") + "%");
        // unseen 2024–26: every setup moment's target-hit rate with the indicator's sides vs all sides (the coin flip)
        var xs = KeystoneEntryIndicator.Sample(s, test, 1, null); int n = 0, w = 0; double allW = 0; int allN = 0;
        foreach (var x in xs)
        {
            allN += 4; allW += x.Win.Count(b => b);
        }
        // the rule taken at the moments it fires (fresh in its window)
        int wi = Array.IndexOf(KeystoneEntryIndicator.Windows, ind.Window);
        foreach (var x in xs.Where(x => x.Fresh[wi]))
        {
            var sg = ind.Signal(x.M[wi], x.G[wi], x.Hhmm); if (sg.Item1 == 0 && sg.Item2 == 0) continue;
            int c = Array.FindIndex(KeystoneEntryIndicator.Sides, sd => sd[0] == sg.Item1 && sd[1] == sg.Item2); n++; if (x.Win[c]) w++;
        }
        double baseFresh = 100.0 * xs.Where(x => x.Fresh[wi]).Sum(x => x.Win.Count(b => b)) / Math.Max(1, 4.0 * xs.Count(x => x.Fresh[wi]));
        say("UNSEEN 2024–26 • the indicator's setups: " + n + " • target hit " + (n == 0 ? 0 : 100.0 * w / n).ToString("0.0") + "% • all sides at the same moments " + baseFresh.ToString("0.0") + "%");
        // whole pass-first prop runs on the unseen days (50 evaluations at a time, as the pass study) — indicator vs 10 random seeds
        var si = s.Copy(); si.Direction = "INDICATOR"; si.Indicator = ind.ToText();
        var rows = KeystoneRotationLab.CompareSides(si, test, null, null);
        foreach (var r in rows) say("  PROP 2024–26 • " + r.Name + " • passed " + r.Passed + "/" + r.Bought + " (" + r.PassRate.ToString("0.0") + "%) • trades " + r.Trades + " won " + r.WinPct.ToString("0") + "% • trading P/L " + r.Pnl.ToString("0"));
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "builtin_indicator.txt"), ind.ToText());
        File.WriteAllText(Path.Combine(outDir, "builtin_indicator_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
