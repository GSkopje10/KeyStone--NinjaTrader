// 🗺 NEXT LEVELS — REACH STUDY (BUILD 2026-11-13d) — research only, nothing is sent: KeystoneLevelReach on the saved minutes in data/MNQ + data/MGC.
// Run from the repository root:  tests/build_engine.sh tests/LevelReachStudy.cs  [data dir] [output dir]
// Writes docs/reports/data/levels/: reach-MNQ.csv, reach-MGC.csv (the same format as Studio\Levels\reach-<SYM>.csv) + summary.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class LevelReachStudy
{
    public static int Main(string[] args)
    {
        string root = Directory.GetCurrentDirectory(); string dataDir = args.Length > 0 ? args[0] : Path.Combine(root, "data"); string outDir = args.Length > 1 ? args[1] : Path.Combine(root, "docs", "reports", "data", "levels");
        Directory.CreateDirectory(outDir); var summary = new List<string>(); var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var sym in new[] { "MNQ", "MGC" })
        {
            string tmp = Path.Combine(Path.GetTempPath(), "ks-lvl-" + Guid.NewGuid().ToString("N")); List<KeystoneArcBar> bars;
            try
            {
                Directory.CreateDirectory(Path.Combine(tmp, sym));
                foreach (var gz in Directory.GetFiles(Path.Combine(dataDir, sym), "*.bars.gz")) using (var i = File.OpenRead(gz)) using (var z = new GZipStream(i, CompressionMode.Decompress)) using (var o = File.Create(Path.Combine(tmp, sym, Path.GetFileNameWithoutExtension(gz)))) z.CopyTo(o);
                bars = KeystoneStudioStore.Load(tmp, sym, new DateTime(2000, 1, 1), new DateTime(2100, 1, 1));
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
            if (bars.Count == 0) { Console.WriteLine(sym + ": NO DATA"); continue; }
            var sessions = bars.Select(b => KeystoneStudioStore.SessionOf(b.Time)).Distinct().OrderBy(d => d).ToList();
            var split = sessions[sessions.Count * 2 / 3];
            var t = KeystoneLevelReach.Study(new[] { bars }, sym, sessions.Last().AddDays(1), split);
            File.WriteAllText(Path.Combine(outDir, "reach-" + sym + ".csv"), t.ToCsv());
            summary.Add("== " + sym + " • " + sessions.First().ToString("yyyy-MM-dd") + " → " + sessions.Last().ToString("yyyy-MM-dd") + " • " + t.Sessions + " sessions • " + t.Samples + " samples (neither " + t.Neither + ", ambiguous " + t.Ambiguous + ") • older 2/3 before " + split.ToString("yyyy-MM-dd") + " • hold " + t.HoldPts + " pts");
            for (int b = 0; b < 5; b++) { var c = t.Get("R:ANY|" + b); if (c != null) summary.Add("  ALL LEVELS, this side " + KeystoneReachTable.ShareName(b) + ": reached first " + c.Pct.ToString("0.0", CultureInfo.InvariantCulture) + " % vs distances alone " + (100 * c.Base / c.N).ToString("0.0", CultureInfo.InvariantCulture) + " % (n = " + c.N + ")"); }
            var h = t.Get("H:ANY"); if (h != null) summary.Add("  ALL LEVELS once touched: turned the price " + h.Pct.ToString("0.0", CultureInfo.InvariantCulture) + " % (n = " + h.N + ")");
            summary.AddRange(t.Highlights(40).Select(x => "  " + x));
            Console.WriteLine(sym + " done • " + clock.Elapsed);
        }
        File.WriteAllText(Path.Combine(outDir, "summary.txt"), string.Join("\n", summary) + "\n"); foreach (var l in summary) Console.WriteLine(l);
        return 0;
    }
}
