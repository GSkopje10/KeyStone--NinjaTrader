// 🎯 3-SETUP ROTATION STUDY (BUILD 2026-11-11o) — research only, nothing is sent: the engine (KeystoneTriSetup / KeystoneTriRotation) on the saved
// MNQ minutes in data/MNQ. Run from the repository root:  tests/build_engine.sh tests/ThreeSetupStudy.cs  [data dir] [output dir]
// Writes docs/reports/data/three-setups/: results.json, summary.txt, batches-<window>-<mode>.csv (full history), trades-NY-<mode>-12m.csv.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class ThreeSetupStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    sealed class Period { public string Code = "", Name = ""; public DateTime From, To; }
    sealed class Run { public string Window = "", Setups = "", Mode = "", Period = ""; public KeystoneTriResult R; }
    static readonly string[] SetupSets = { "FVG,AGG,ENG", "FVG", "AGG", "ENG" };
    static readonly string[] Modes = { "ROTATE", "STAY" };
    const int Seeds = 20;

    public static int Main(string[] args)
    {
        string root = Directory.GetCurrentDirectory(); string dataDir = args.Length > 0 ? args[0] : Path.Combine(root, "data"); string outDir = args.Length > 1 ? args[1] : Path.Combine(root, "docs", "reports", "data", "three-setups");
        string tmp = Path.Combine(Path.GetTempPath(), "ks-tri-" + Guid.NewGuid().ToString("N")); var clock = System.Diagnostics.Stopwatch.StartNew();
        KeystoneTriData d;
        try
        {
            Directory.CreateDirectory(Path.Combine(tmp, "MNQ"));
            foreach (var gz in Directory.GetFiles(Path.Combine(dataDir, "MNQ"), "*.bars.gz")) using (var i = File.OpenRead(gz)) using (var z = new GZipStream(i, CompressionMode.Decompress)) using (var o = File.Create(Path.Combine(tmp, "MNQ", Path.GetFileNameWithoutExtension(gz)))) z.CopyTo(o);
            d = KeystoneTriData.From(KeystoneStudioStore.Load(tmp, "MNQ", new DateTime(2000, 1, 1), new DateTime(2100, 1, 1)));
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        if (d.N == 0) { Console.WriteLine("NO DATA in " + dataDir); return 1; }
        DateTime first = d.Days.First(), last = d.Days.Last();
        var periods = new List<Period>
        {
            new Period { Code = "FULL", Name = "full history", From = first, To = last },
            new Period { Code = "Y1", Name = "last 12 months", From = last.AddYears(-1).AddDays(1), To = last },
            new Period { Code = "M1", Name = "last month", From = last.AddMonths(-1).AddDays(1), To = last },
            new Period { Code = "W1", Name = "last week", From = last.AddDays(-6), To = last },
            new Period { Code = "IS", Name = "walk-forward in-sample", From = first, To = new DateTime(2024, 12, 31) },
            new Period { Code = "OOS", Name = "walk-forward out-of-sample", From = new DateTime(2025, 1, 1), To = last },
        };
        Console.WriteLine("data " + first.ToString("yyyy-MM-dd") + " → " + last.ToString("yyyy-MM-dd") + " • " + d.Days.Count + " sessions • " + d.N + " minutes • " + clock.Elapsed);

        var runs = new List<Run>(); var rawRows = new List<Tuple<string, string, List<KeystoneTriSetupRow>>>(); var randoms = new List<Tuple<string, string, string, List<KeystoneTriResult>>>();
        foreach (var w in KeystoneTriSettings.Windows)
        {
            var bySetup = new Dictionary<string, List<KeystoneTriCandidate>>();
            foreach (var one in new[] { "FVG", "AGG", "ENG" }) bySetup[one] = KeystoneTriSetup.Candidates(d, new KeystoneTriSettings { Window = w.Code, Setups = one }.ApplyWindow());
            bySetup["FVG,AGG,ENG"] = KeystoneTriSetup.Sort(bySetup["FVG"].Concat(bySetup["AGG"]).Concat(bySetup["ENG"]));
            var all = bySetup["FVG,AGG,ENG"]; var baseS = new KeystoneTriSettings { Window = w.Code }.ApplyWindow();
            foreach (var p in periods) rawRows.Add(Tuple.Create(w.Code, p.Code, KeystoneTriRotation.RawRows(d, baseS, all, p.From, p.To)));
            foreach (var set in SetupSets) foreach (var mode in (set == "FVG,AGG,ENG" && (w.Code == "NY" || w.Code == "ALLDAY") ? new[] { "ROTATE", "STAY", "STAYNEXT" } : Modes)) foreach (var p in periods)
                    {
                        var s = new KeystoneTriSettings { Window = w.Code, Setups = set, Mode = mode }.ApplyWindow();
                        runs.Add(new Run { Window = w.Code, Setups = set, Mode = mode, Period = p.Code, R = KeystoneTriRotation.Run(d, bySetup[set], s, p.From, p.To) });
                    }
            // 🎲 the random-entry control: as many entries a session as the 3 setups gave in this window, random minute + side, 20 seeds
            var perDay = all.GroupBy(c => c.Day).ToDictionary(g => g.Key, g => g.Count());
            var rc = new List<List<KeystoneTriCandidate>>(); for (int seed = 1; seed <= Seeds; seed++) rc.Add(KeystoneTriSetup.Random(d, baseS, perDay, 1000 * seed + 7));
            foreach (var mode in Modes) foreach (var p in periods)
                {
                    var list = new List<KeystoneTriResult>(); var s = new KeystoneTriSettings { Window = w.Code, Setups = "RANDOM", Mode = mode }.ApplyWindow();
                    foreach (var c in rc) list.Add(KeystoneTriRotation.Run(d, c, s, p.From, p.To));
                    randoms.Add(Tuple.Create(w.Code, mode, p.Code, list));
                }
            Console.WriteLine(w.Code + " done • " + all.Count + " signals (FVG " + bySetup["FVG"].Count + ", AGG " + bySetup["AGG"].Count + ", ENG " + bySetup["ENG"].Count + ") • " + clock.Elapsed);
        }

        Directory.CreateDirectory(outDir);
        Func<string, string, string, string, Run> find = (w, set, mode, per) => runs.First(x => x.Window == w && x.Setups == set && x.Mode == mode && x.Period == per);
        // CSVs: the batches of the main windows (full history), the trades of NY (last 12 months)
        foreach (var w in new[] { "NY", "ALLDAY" }) foreach (var mode in Modes)
                File.WriteAllText(Path.Combine(outDir, "batches-" + w + "-" + mode + ".csv"), KeystoneTriBatch.Header + "\n" + string.Join("\n", find(w, "FVG,AGG,ENG", mode, "FULL").R.Batches.Select(b => b.Line())) + "\n");
        foreach (var mode in Modes) File.WriteAllText(Path.Combine(outDir, "trades-NY-" + mode + "-12m.csv"), KeystoneTriTrade.Header + "\n" + string.Join("\n", find("NY", "FVG,AGG,ENG", mode, "Y1").R.Trades.Select(t => t.Line())) + "\n");

        // results.json
        var j = new KeystoneJsonWriter(); j.Open();
        j.Key("meta"); j.Open(); j.Prop("study", "3-SETUP ROTATION (FVG 50 % / aggression break / 123 engulfing) • MNQ 15 micros • 30 Lucid Flex 50K accounts • research only, nothing sent");
        j.Prop("build", "BUILD 2026-11-11o"); j.Prop("data_first", first.ToString("yyyy-MM-dd")); j.Prop("data_last", last.ToString("yyyy-MM-dd")); j.Prop("sessions", (long)d.Days.Count); j.Prop("minutes", (long)d.N);
        var s0 = new KeystoneTriSettings(); j.Prop("settings", s0.Describe()); j.Key("settings_fields"); j.Open(); j.Fields(s0); j.Close(); j.Key("rules"); j.Open(); j.Fields(s0.Rules); j.Close();
        j.Num("stop_pts", s0.StopPts); j.Num("target_pts", s0.TargetPts); j.Num("costs_per_trade", s0.Costs); j.Num("win_net", s0.TargetPts * 30 - s0.Costs); j.Num("loss_net", -(s0.StopPts + 0.25) * 30 - s0.Costs);
        j.Num("breakeven_win_pct", 100 * (s0.StopPts * 30 + 7.5 + s0.Costs) / (s0.TargetPts * 30 + s0.StopPts * 30 + 7.5));
        j.Key("periods"); j.Arr(); foreach (var p in periods) { j.Open(); j.Prop("code", p.Code); j.Prop("name", p.Name); j.Prop("from", p.From.ToString("yyyy-MM-dd")); j.Prop("to", p.To.ToString("yyyy-MM-dd")); j.Prop("sessions", (long)d.Days.Count(x => x >= p.From && x <= p.To)); j.Close(); } j.CloseArr();
        j.Key("windows"); j.Arr(); foreach (var w in KeystoneTriSettings.Windows) { j.Open(); j.Prop("code", w.Code); j.Prop("name", w.Name); j.Prop("start", (long)w.StartHhmm); j.Prop("end", (long)w.EndHhmm); j.Close(); } j.CloseArr();
        j.Key("modes"); j.Arr(); j.Open(); j.Prop("code", "ROTATE"); j.Prop("name", "A • ONE TRADE PER ACCOUNT (round robin)"); j.Close(); j.Open(); j.Prop("code", "STAY"); j.Prop("name", "B • STAY UNTIL DONE (eval: passed / blown • funded: payout taken / blown / retired); a day the firm's rules stop it, the signals wait"); j.Close(); j.Open(); j.Prop("code", "STAYNEXT"); j.Prop("name", "B variant (NY + ALL DAY, 3 setups only) • STAY UNTIL DONE, but on a stopped day the next account takes over"); j.Close(); j.CloseArr();
        j.Close();
        j.Key("variants"); j.Arr();
        foreach (var r in runs) { j.Open(); j.Prop("window", r.Window); j.Prop("setups", r.Setups); j.Prop("mode", r.Mode); j.Prop("period", r.Period); j.Key("stats"); j.Open(); j.Fields(r.R.Stats); j.Close(); j.Close(); }
        j.CloseArr();
        j.Key("setups_raw"); j.Arr();
        foreach (var t in rawRows) { j.Open(); j.Prop("window", t.Item1); j.Prop("period", t.Item2); j.Key("rows"); j.Arr(); foreach (var row in t.Item3) { j.Open(); j.Fields(row); j.Num("WinPct", row.WinPct); j.Num("Avg", row.Avg); j.Num("Pf", row.Pf); j.Close(); } j.CloseArr(); j.Close(); }
        j.CloseArr();
        j.Key("setups_taken"); j.Arr();
        foreach (var r in runs.Where(x => x.Setups == "FVG,AGG,ENG")) { j.Open(); j.Prop("window", r.Window); j.Prop("mode", r.Mode); j.Prop("period", r.Period); j.Key("rows"); j.Arr(); foreach (var row in r.R.SetupRows()) { j.Open(); j.Fields(row); j.Num("WinPct", row.WinPct); j.Num("Avg", row.Avg); j.Num("Pf", row.Pf); j.Close(); } j.CloseArr(); j.Close(); }
        j.CloseArr();
        // the random control vs the 3 setups (same window / mode / period)
        var metrics = new Dictionary<string, Func<KeystoneTriStats, double>>
        {
            { "Trades", x => x.Trades }, { "WinPct", x => x.WinPct }, { "AvgUsd", x => x.AvgUsd }, { "TradingNet", x => x.TradingNet }, { "PassPct", x => x.PassPct }, { "PayoutsPerBatch", x => x.PayoutsPerBatch },
            { "NetMean", x => x.NetMean }, { "NetMedian", x => x.NetMedian }, { "PctPositive", x => x.PctPositive }, { "BusinessNet", x => x.BusinessNet }, { "BreakevenMedian", x => x.BreakevenMedian }, { "FirstPayoutMedian", x => x.FirstPayoutMedian },
        };
        j.Key("random_control"); j.Arr();
        var randomSummary = new Dictionary<string, Dictionary<string, double[]>>();
        foreach (var t in randoms)
        {
            var real = find(t.Item1, "FVG,AGG,ENG", t.Item2, t.Item3).R.Stats; var key = t.Item1 + "|" + t.Item2 + "|" + t.Item3; randomSummary[key] = new Dictionary<string, double[]>();
            j.Open(); j.Prop("window", t.Item1); j.Prop("mode", t.Item2); j.Prop("period", t.Item3); j.Prop("seeds", (long)t.Item4.Count); j.Key("metrics"); j.Open();
            foreach (var m in metrics)
            {
                var v = t.Item4.Select(x => m.Value(x.Stats)).Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); double rv = m.Value(real);
                double mean = v.Count == 0 ? double.NaN : v.Average(), med = KeystoneTriRotation.Median(v), p5 = v.Count == 0 ? double.NaN : v[(int)Math.Floor(0.05 * (v.Count - 1))], p95 = v.Count == 0 ? double.NaN : v[(int)Math.Ceiling(0.95 * (v.Count - 1))];
                int beats = v.Count(x => rv > x); randomSummary[key][m.Key] = new[] { mean, med, p5, p95, rv, beats, v.Count };
                j.Key(m.Key); j.Open(); j.Num("random_mean", mean); j.Num("random_median", med); j.Num("random_p5", p5); j.Num("random_p95", p95); j.Num("setups", rv); j.Prop("setups_beat_seeds", (long)beats); j.Prop("seeds", (long)v.Count); j.Close();
            }
            j.Close(); j.Close();
        }
        j.CloseArr();
        // walk-forward: the windows ranked by net per batch (3 setups) in-sample, and where they land out-of-sample
        j.Key("walk_forward"); j.Open(); var wfText = new StringBuilder();
        foreach (var mode in Modes)
        {
            j.Key(mode); j.Open();
            Func<string, List<Tuple<string, double, double>>> rank = per => KeystoneTriSettings.Windows.Select(w => { var st = find(w.Code, "FVG,AGG,ENG", mode, per).R.Stats; return Tuple.Create(w.Code, st.NetMean, st.AvgUsd); }).OrderByDescending(x => x.Item2).ToList();
            var isr = rank("IS"); var oos = rank("OOS");
            foreach (var pr in new[] { Tuple.Create("in_sample", isr), Tuple.Create("out_of_sample", oos) }) { j.Key(pr.Item1); j.Arr(); foreach (var x in pr.Item2) { j.Open(); j.Prop("window", x.Item1); j.Num("net_per_batch", x.Item2); j.Num("avg_per_trade", x.Item3); j.Close(); } j.CloseArr(); }
            string best = isr[0].Item1; int oosRank = oos.FindIndex(x => x.Item1 == best) + 1; j.Prop("best_in_sample", best); j.Prop("its_out_of_sample_rank", (long)oosRank); j.Prop("best_out_of_sample", oos[0].Item1);
            j.Close(); wfText.AppendLine("  " + mode + ": best in-sample (→2024) " + best + " (" + isr[0].Item2.ToString("+0;-0", IC) + "/batch) → out-of-sample (2025→) rank " + oosRank + " of " + oos.Count + " (" + oos.First(x => x.Item1 == best).Item2.ToString("+0;-0", IC) + "/batch); best out-of-sample " + oos[0].Item1 + " (" + oos[0].Item2.ToString("+0;-0", IC) + ")");
        }
        j.Close();
        j.Key("main_batches"); j.Arr();
        foreach (var w in new[] { "NY", "ALLDAY" }) foreach (var mode in Modes) foreach (var b in find(w, "FVG,AGG,ENG", mode, "FULL").R.Batches)
                { j.Open(); j.Prop("window", w); j.Prop("mode", mode); j.Fields(b); j.Num("Net", b.Net); j.Num("PassPct", b.PassPct); j.Num("Roi", b.Roi); j.Prop("Start", b.Start.ToString("yyyy-MM-dd")); j.Prop("End", b.End.ToString("yyyy-MM-dd")); j.Prop("FirstPayout", b.FirstPayout == DateTime.MinValue ? "" : b.FirstPayout.ToString("yyyy-MM-dd")); j.Prop("Breakeven", b.Breakeven == DateTime.MinValue ? "" : b.Breakeven.ToString("yyyy-MM-dd")); j.Close(); }
        j.CloseArr();
        j.Close();
        File.WriteAllText(Path.Combine(outDir, "results.json"), j.ToString() + "\n");

        // summary.txt
        var sb = new StringBuilder(); Func<double, string> m0 = v => double.IsNaN(v) ? "–" : v.ToString("+#,0;-#,0;0", IC); Func<double, string> p1 = v => double.IsNaN(v) ? "–" : v.ToString("0.0", IC); Func<double, string> n0 = v => double.IsNaN(v) ? "–" : v.ToString("0", IC);
        sb.AppendLine("3-SETUP ROTATION STUDY (BUILD 2026-11-11o) — research only, nothing is sent");
        sb.AppendLine("data MNQ " + first.ToString("yyyy-MM-dd") + " → " + last.ToString("yyyy-MM-dd") + " (" + d.Days.Count + " sessions, 1-minute bars; no real ticks in the repo → a minute touching both stop and target = STOP)");
        sb.AppendLine(s0.Describe());
        sb.AppendLine("a win nets " + m0(s0.TargetPts * 30 - s0.Costs) + ", a stop " + m0(-(s0.StopPts + 0.25) * 30 - s0.Costs) + " → break-even win rate " + p1(100 * (s0.StopPts * 30 + 7.5 + s0.Costs) / (s0.TargetPts * 30 + s0.StopPts * 30 + 7.5)) + " %");
        sb.AppendLine("periods: " + string.Join(" • ", periods.Select(p => p.Code + " " + p.From.ToString("yyyy-MM-dd") + "→" + p.To.ToString("yyyy-MM-dd"))));
        sb.AppendLine(); sb.AppendLine("KEY NUMBERS (3 setups combined) — net/batch = payouts to you − 30 × $120; batch numbers = finished batches (OPEN = none finished: the open batch so far)");
        string hdr = string.Format(IC, "{0,-7}{1,-9}{2,-5}{3,7}{4,6}{5,8}{6,8}{7,5}{8,7}{9,7}{10,9}{11,9}{12,6}{13,7}{14,7}{15,10}{16,6}", "window", "mode", "per", "trades", "win%", "$/trade", "batches", "fin", "pass%", "pay/b", "net/b", "med/b", "pos%", "1stPay", "BE", "busNet", "basis");
        sb.AppendLine(hdr);
        foreach (var w in KeystoneTriSettings.Windows) foreach (var p in periods) foreach (var mode in new[] { "ROTATE", "STAY", "STAYNEXT" })
                {
                    if (!runs.Any(x => x.Window == w.Code && x.Setups == "FVG,AGG,ENG" && x.Mode == mode && x.Period == p.Code)) continue;
                    var st = find(w.Code, "FVG,AGG,ENG", mode, p.Code).R.Stats;
                    sb.AppendLine(string.Format(IC, "{0,-7}{1,-9}{2,-5}{3,7}{4,6}{5,8}{6,8}{7,5}{8,7}{9,7}{10,9}{11,9}{12,6}{13,7}{14,7}{15,10}{16,6}", w.Code, mode, p.Code, st.Trades, p1(st.WinPct), m0(st.AvgUsd), st.Batches, st.Finished, p1(st.PassPct), p1(st.PayoutsPerBatch), m0(st.NetMean), m0(st.NetMedian), n0(st.PctPositive), n0(st.FirstPayoutMedian), n0(st.BreakevenMedian), m0(st.BusinessNet), st.Basis));
                }
        sb.AppendLine(); sb.AppendLine("EACH SETUP ALONE vs COMBINED (full history + last 12 months)");
        sb.AppendLine(string.Format(IC, "{0,-7}{1,-12}{2,-9}{3,-5}{4,7}{5,6}{6,8}{7,7}{8,7}{9,9}{10,6}{11,7}", "window", "setups", "mode", "per", "trades", "win%", "$/trade", "pass%", "pay/b", "net/b", "pos%", "BE"));
        foreach (var w in KeystoneTriSettings.Windows) foreach (var p in new[] { "FULL", "Y1" }) foreach (var set in SetupSets) foreach (var mode in Modes)
                    {
                        var st = find(w.Code, set, mode, p).R.Stats;
                        sb.AppendLine(string.Format(IC, "{0,-7}{1,-12}{2,-9}{3,-5}{4,7}{5,6}{6,8}{7,7}{8,7}{9,9}{10,6}{11,7}", w.Code, set == "FVG,AGG,ENG" ? "ALL3" : set, mode, p, st.Trades, p1(st.WinPct), m0(st.AvgUsd), p1(st.PassPct), p1(st.PayoutsPerBatch), m0(st.NetMean), n0(st.PctPositive), n0(st.BreakevenMedian)));
                    }
        sb.AppendLine(); sb.AppendLine("RANDOM CONTROL (20 seeds, same window / frequency / bracket / rotation) — random median [p5..p95] vs the 3 setups (seeds beaten of 20)");
        foreach (var w in KeystoneTriSettings.Windows) foreach (var mode in Modes) foreach (var p in periods)
                {
                    var rs = randomSummary[w.Code + "|" + mode + "|" + p.Code]; Func<string, string> f = k => { var v = rs[k]; return k + " " + (k.Contains("Pct") ? p1(v[1]) + " [" + p1(v[2]) + ".." + p1(v[3]) + "] vs " + p1(v[4]) : m0(v[1]) + " [" + m0(v[2]) + ".." + m0(v[3]) + "] vs " + m0(v[4])) + " (" + v[5] + "/" + v[6] + ")"; };
                    sb.AppendLine(w.Code + " " + mode + " " + p.Code + ": " + f("WinPct") + " • " + f("AvgUsd") + " • " + f("PassPct") + " • " + f("NetMean") + " • " + f("PctPositive"));
                }
        sb.AppendLine(); sb.AppendLine("SETUP × SIDE, every signal resolved alone (no rotation / overlap / floor) — full history and last 12 months");
        foreach (var t in rawRows.Where(x => x.Item2 == "FULL" || x.Item2 == "Y1"))
            foreach (var row in t.Item3) sb.AppendLine(string.Format(IC, "{0,-7}{1,-5}{2,-4}{3,-5}{4,7}{5,7}{6,8}{7,6}{8,5}{9,6}", t.Item1, t.Item2, row.Setup, row.Side, row.N, p1(row.WinPct), m0(row.Avg), row.Pf.ToString("0.00", IC), row.Both, row.Flat));
        sb.AppendLine(); sb.AppendLine("WALK-FORWARD (windows ranked by net per batch, 3 setups)"); sb.Append(wfText);
        sb.AppendLine(); sb.AppendLine("MAIN VARIANT DETAIL (NY 9:30–16:00, 3 setups, full history)");
        foreach (var mode in new[] { "ROTATE", "STAY", "STAYNEXT" })
        {
            var st = find("NY", "FVG,AGG,ENG", mode, "FULL").R.Stats;
            sb.AppendLine("  " + mode + ": PF " + st.ProfitFactor.ToString("0.00", IC) + " • streaks: wins " + st.MaxWinStreak + " / losses " + st.MaxLoseStreak + " trades, " + st.MaxWinDays + " / " + st.MaxLoseDays + " days • both-touched " + st.Both + " • liquidated at the floor " + st.Liquidated + " • skipped (in a trade) " + st.SkippedBusy + " • no account " + st.SkippedNoAccount
                + " • best batch " + st.BestBatch + " • worst " + st.WorstBatch + " • longest run of positive batches " + st.BestPositiveRun + " • fastest break-even " + n0(st.BreakevenMin) + " sessions (" + p1(st.PctBrokeEven) + " % of batches broke even) • fastest first payout " + n0(st.FirstPayoutMin) + " sessions • median calendar days to break even " + n0(st.CalBreakevenMedian) + " / to the first payout " + n0(st.CalFirstPayoutMedian) + " • ROI " + p1(st.Roi) + " %");
        }
        File.WriteAllText(Path.Combine(outDir, "summary.txt"), sb.ToString());
        Console.WriteLine(sb.ToString());
        Console.WriteLine("THREE-SETUP STUDY WRITTEN → " + outDir + " • " + clock.Elapsed);
        return 0;
    }
}
