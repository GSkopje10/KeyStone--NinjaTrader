// Asian 75 optimizer, command-line front end for development and testing.
//
// The optimizer itself (KeystoneArcAsianOptimizer) lives in src/KeyStone.cs and is the same
// code the lab's OPTIMIZE ASIAN 75 button runs on NinjaTrader's loaded bars. This tool only
// adds file loading, so the engine can be exercised here on exported or synthetic data.
//
// Data: NinjaTrader 8 "Historical Data → Export" files (yyyyMMdd HHmmss;O;H;L;C;V) or CSV
// (yyyy-MM-dd HH:mm[:ss],O,H,L,C[,V]). Any time zone: each session is aligned to Eastern time
// from the daily 17:00–18:00 ET halt. Several contract files per symbol are fine: for every
// session the contract with the most volume (the front month) is used.
//
// Usage: tools/optimize_asian75.sh --mnq <file|dir>... --mgc <file|dir>... [grid options]
//        tools/optimize_asian75.sh --help
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class Asian75Optimizer
{
    static readonly Dictionary<string, List<string>> Opt = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    static List<string> L(string key, string dflt) { List<string> v; return Opt.TryGetValue(key, out v) ? v.SelectMany(x => x.Split(',')).Where(x => x.Length > 0).ToList() : dflt.Split(',').ToList(); }
    static double D(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }
    static string S(string key, string dflt) { return L(key, dflt)[0]; }

    public static int Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            string key = args[i].Substring(2); var vals = new List<string>();
            while (i + 1 < args.Length && !args[i + 1].StartsWith("--")) vals.Add(args[++i]);
            List<string> existing; if (Opt.TryGetValue(key, out existing)) existing.AddRange(vals); else Opt[key] = vals;
        }
        if (Opt.ContainsKey("help") || args.Length == 0) { Console.WriteLine(Help); return 0; }
        string outDir = S("out", "asian75-results"); Directory.CreateDirectory(outDir);
        if (Opt.ContainsKey("synthetic")) { WriteSynthetic(outDir, int.Parse(S("synthetic", "260"))); return 0; }

        var bars = new List<KeystoneArcBar>();
        foreach (string sym in new[] { "MNQ", "MGC" })
            if (Opt.ContainsKey(sym.ToLowerInvariant())) bars.AddRange(LoadSymbol(sym, L(sym.ToLowerInvariant(), "")));
        if (bars.Count == 0) { Console.WriteLine("No data. Pass --mnq and/or --mgc with NinjaTrader export files or folders."); return 1; }
        var probe = new KeystoneArcRunConfig { SessionMode = "ASIAN75", AsianStartHhmm = 1800, AsianEndHhmm = 1555 };
        if (Opt.ContainsKey("from")) { DateTime from = DateTime.Parse(S("from", ""), CultureInfo.InvariantCulture); bars = bars.Where(b => KeystoneArcEngine.SessionGroupingDate(b.Time, probe) >= from).ToList(); }
        if (Opt.ContainsKey("to")) { DateTime to = DateTime.Parse(S("to", ""), CultureInfo.InvariantCulture); bars = bars.Where(b => KeystoneArcEngine.SessionGroupingDate(b.Time, probe) <= to).ToList(); }
        foreach (var g0 in bars.GroupBy(b => b.Symbol)) Console.WriteLine("DATA  " + g0.Key + " " + g0.Count().ToString("N0") + " bars " + g0.Min(b => b.Time).ToString("yyyy-MM-dd") + " -> " + g0.Max(b => b.Time).ToString("yyyy-MM-dd"));

        string dirs = string.Join(",", L("dir", "LONG,SHORT"));
        var grid = new KeystoneArcAsianGrid
        {
            Scopes = L("scope", "MNQ,MGC,BOTH"), MnqDirections = L("mnq-dir", dirs), MgcDirections = L("mgc-dir", dirs), RiskModes = L("mode", "CASH"),
            LegLosses = L("loss", "50,75,100,125,150").Select(D).ToList(), Reversals = L("reversals", "1,2,3,4,5").Select(int.Parse).ToList(),
            Targets = L("target", "150,250,350,500,750").Select(D).ToList(), CycleStops = L("cycle-stop", "0").Select(D).ToList(),
            InstrumentCaps = L("instrument-cap", "0").Select(D).ToList(), BreakEvens = L("breakeven", "0").Select(D).ToList(),
            StartTimes = L("start", "1800").Select(int.Parse).ToList(), EndHhmm = int.Parse(S("end", "1555")), DailyLossLimit = D(S("daily-loss", "0")), LinkDirections = Opt.ContainsKey("link-directions"),
            CostPerContract = D(S("cost", "1.00")), OutOfSampleFraction = D(S("oos", "0.30")), MinimumNights = int.Parse(S("min-nights", "40")), PropDrawdown = D(S("prop-dd", "2000"))
        };
        var clock = System.Diagnostics.Stopwatch.StartNew(); int lastShown = 0;
        KeystoneArcAsianOptimization o = KeystoneArcAsianOptimizer.Run(bars, grid, (done, total) => { if (done - lastShown >= 250 || done == total) { lastShown = done; Console.Error.WriteLine("  " + done + "/" + total + "  " + clock.Elapsed.TotalSeconds.ToString("0") + "s"); } }, null);
        Console.WriteLine();
        var current = new KeystoneArcAsianCombo { Scope = "BOTH", MnqDirection = "LONG", MgcDirection = "LONG", RiskMode = "CASH", LegLoss = 75, Reversals = 3, Target = 350, StartHhmm = 1800 };
        Console.WriteLine(KeystoneArcAsianOptimizer.FormatTable(o, int.Parse(S("top", "25")), current));
        File.WriteAllText(Path.Combine(outDir, "asian75_grid_results.csv"), KeystoneArcAsianOptimizer.ToCsv(o));
        if (o.Ranked.Count > 0) File.WriteAllText(Path.Combine(outDir, "asian75_best_nights.csv"), KeystoneArcAsianOptimizer.NightsCsv(o.Ranked[0]));
        Console.WriteLine("Wrote " + Path.Combine(outDir, "asian75_grid_results.csv") + " and asian75_best_nights.csv • " + clock.Elapsed.TotalSeconds.ToString("0") + "s");
        return 0;
    }

    // ---------------------------------------------------------------- data loading
    static IEnumerable<string> Files(IEnumerable<string> paths)
    {
        foreach (string p in paths)
        {
            if (Directory.Exists(p)) { foreach (string f in Directory.GetFiles(p).Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".txt.gz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f)) yield return f; }
            else if (File.Exists(p)) yield return p;
            else Console.WriteLine("WARNING missing path " + p);
        }
    }

    static List<KeystoneArcBar> LoadSymbol(string symbol, List<string> paths)
    {
        // Load each contract file separately, then per session keep the most-traded contract.
        var perFile = new List<List<KeystoneArcBar>>();
        foreach (string f in Files(paths))
        {
            var raw = ParseFile(f, symbol);
            if (raw.Count == 0) { Console.WriteLine("WARNING no bars parsed from " + f); continue; }
            string note; var aligned = AlignToEastern(raw, out note);
            Console.WriteLine("LOAD  " + symbol + " " + Path.GetFileName(f) + "  " + raw.Count.ToString("N0") + " bars  " + note);
            perFile.Add(aligned);
        }
        var probe = new KeystoneArcRunConfig { SessionMode = "ASIAN75", AsianStartHhmm = 1800, AsianEndHhmm = 1555 };
        var chosen = new Dictionary<DateTime, List<KeystoneArcBar>>();
        var volume = new Dictionary<DateTime, double>();
        foreach (var file in perFile)
            foreach (var session in file.GroupBy(b => KeystoneArcEngine.SessionGroupingDate(b.Time, probe)))
            {
                double v = session.Sum(b => b.Volume); double best;
                if (!volume.TryGetValue(session.Key, out best) || v > best) { volume[session.Key] = v; chosen[session.Key] = session.ToList(); }
            }
        return chosen.Values.SelectMany(x => x).OrderBy(b => b.Time).ToList();
    }

    // Plain text or the lab's EXPORT FOR CLAUDE files (*.txt.gz).
    static IEnumerable<string> ReadLines(string path)
    {
        if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) { foreach (string l in File.ReadLines(path)) yield return l; yield break; }
        using (var fs = File.OpenRead(path))
        using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            string line; while ((line = r.ReadLine()) != null) yield return line;
        }
    }

    static List<KeystoneArcBar> ParseFile(string path, string symbol)
    {
        var list = new List<KeystoneArcBar>();
        foreach (string line in ReadLines(path))
        {
            string t = line.Trim(); if (t.Length == 0) continue;
            string[] p = t.Split(t.Contains(";") ? ';' : ',');
            if (p.Length < 5) continue;
            DateTime time;
            if (!DateTime.TryParseExact(p[0].Trim(), new[] { "yyyyMMdd HHmmss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) continue; // skips header rows
            double o, h, l, c, v = 0;
            if (!double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out o) || !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out h)
                || !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out l) || !double.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out c)) continue;
            if (p.Length > 5) double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            list.Add(new KeystoneArcBar { Symbol = symbol, Time = time, Open = o, High = h, Low = l, Close = c, Volume = (long)v });
        }
        return list.OrderBy(b => b.Time).ToList();
    }

    // CME equity/metal futures pause 17:00–18:00 ET daily and reopen at 18:00 ET (the first
    // close-stamped 1M bar reads 18:01). Whatever zone the export used, the first bar after each
    // pause of 40+ minutes therefore marks 18:00 ET. The whole-hour shift is measured per
    // session, so daylight-saving changes in UTC exports are handled automatically.
    static List<KeystoneArcBar> AlignToEastern(List<KeystoneArcBar> bars, out string note)
    {
        var shifts = new Dictionary<int, int>();
        int shift = int.MinValue;
        var output = new List<KeystoneArcBar>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            KeystoneArcBar b = bars[i];
            bool resumed = i == 0 || (b.Time - bars[i - 1].Time).TotalMinutes >= 40;
            if (resumed && b.Time.Minute <= 1)
            {
                int candidate = ((18 - b.Time.Hour) % 24 + 24) % 24; if (candidate > 12) candidate -= 24;
                // Accept the first measurement, and afterwards only DST-sized (±1h) moves, so a
                // holiday reopening at another hour does not re-align a normal session.
                if (shift == int.MinValue || Math.Abs(candidate - shift) <= 1) shift = candidate;
            }
            if (shift == int.MinValue) continue; // bars before the first identifiable reopen
            int count; shifts.TryGetValue(shift, out count); shifts[shift] = count + 1;
            output.Add(new KeystoneArcBar { Symbol = b.Symbol, Time = b.Time.AddHours(shift), Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = b.Volume });
        }
        note = shifts.Count == 0 ? "NO 18:00 ET REOPEN FOUND — check the export" : "time shift to ET: " + string.Join(", ", shifts.OrderByDescending(x => x.Value).Select(x => (x.Key >= 0 ? "+" : "") + x.Key + "h (" + x.Value.ToString("N0") + " bars)"));
        return output;
    }

    // Random-walk test data in NinjaTrader export format, stamped in UTC to exercise alignment.
    // A random walk has no edge, so results on it only show the tool works and what costs do.
    static void WriteSynthetic(string dir, int sessions)
    {
        var rng = new Random(7);
        foreach (var inst in new[] { new { Sym = "MNQ", Px = 20000.0, Vol = 4.0, Tick = 0.25 }, new { Sym = "MGC", Px = 2900.0, Vol = 0.8, Tick = 0.1 } })
        {
            var sb = new StringBuilder(); double px = inst.Px; DateTime day = new DateTime(2024, 1, 1); int made = 0;
            while (made < sessions)
            {
                day = day.AddDays(1);
                if (day.DayOfWeek == DayOfWeek.Friday || day.DayOfWeek == DayOfWeek.Saturday) continue;
                made++;
                DateTime openEt = day.AddHours(18);
                bool dst = day.Month > 3 && day.Month < 11; int utcOffset = dst ? 4 : 5;
                for (int m = 0; m < 22 * 60 - 5; m++)
                {
                    DateTime et = openEt.AddMinutes(m); int hour = et.Hour;
                    double vol = inst.Vol * (hour >= 9 && hour < 16 ? 2.2 : (hour >= 3 && hour < 9 ? 1.2 : 0.8));
                    double o = px, c = Math.Round((px + NextGaussian(rng) * vol) / inst.Tick) * inst.Tick;
                    double h = Math.Max(o, c) + Math.Round(Math.Abs(NextGaussian(rng)) * vol * 0.5 / inst.Tick) * inst.Tick;
                    double l = Math.Min(o, c) - Math.Round(Math.Abs(NextGaussian(rng)) * vol * 0.5 / inst.Tick) * inst.Tick;
                    px = c;
                    DateTime stampUtc = et.AddMinutes(1).AddHours(utcOffset);
                    sb.Append(stampUtc.ToString("yyyyMMdd HHmmss")).Append(';').Append(o.ToString(CultureInfo.InvariantCulture)).Append(';').Append(h.ToString(CultureInfo.InvariantCulture)).Append(';')
                      .Append(l.ToString(CultureInfo.InvariantCulture)).Append(';').Append(c.ToString(CultureInfo.InvariantCulture)).Append(";100\n");
                }
            }
            File.WriteAllText(Path.Combine(dir, inst.Sym + "_synthetic.Last.txt"), sb.ToString());
            Console.WriteLine("wrote " + Path.Combine(dir, inst.Sym + "_synthetic.Last.txt"));
        }
    }

    static double NextGaussian(Random r) { return Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble()); }

    const string Help = @"Asian 75 optimizer — runs the KeyStone.cs Asian engine over a parameter grid.

DATA   --mnq <files or folders>   --mgc <files or folders>   (NinjaTrader export .txt or CSV)
       --from 2021-01-01 --to 2025-12-31   limit session dates
GRID   (comma or space separated lists; defaults in brackets)
       --scope MNQ,MGC,BOTH [all]        --dir LONG,SHORT [both]  (or --mnq-dir / --mgc-dir)
       --loss 50,75,100,125,150          leg loss $ (CASH) or x1 leg loss $ converted to price move (PRICE)
       --reversals 1,2,3,4,5             --target 150,250,350,500,750
       --mode CASH,PRICE [CASH]          --cycle-stop 0 [off]   --instrument-cap 0 [off]
       --breakeven 0 [off]               --daily-loss 0 [auto = leg x reversals x instruments]
       --start 1800   --end 1555        --link-directions  (one direction for both, like the lab)
EVAL   --cost 1.00   round-trip $ per contract     --oos 0.30   share of latest sessions held out
       --min-nights 40   --prop-dd 2000   --top 25   --out asian75-results
TEST   --synthetic 260   writes random-walk sample files to --out";
}
