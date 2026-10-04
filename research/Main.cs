using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public sealed class VResult { public Variant V; public List<RTrade> Trades; public Stats All, Is, Oos; public int IsYearsUp; }

public static class ResearchMain
{
    public static int Main(string[] args)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        string data = args[0], outDir = args[1];
        if (args.Length > 3 && args[2] == "bhdays")
        {
            var days = R.Load(data, "MNQ"); var want = args[3].Split(',').Select(x => DateTime.Parse(x, CultureInfo.InvariantCulture)).ToList();
            foreach (var cfg in new[] { new BhCfg { TargetR = 2 }, new BhCfg { TargetR = 0, LastEntry = R.S(15, 0) }, new BhCfg { Start = R.S(10, 0), TargetR = 2 } })
            {
                Console.WriteLine("== " + cfg.Name());
                foreach (var dd in want) { var d = days.FirstOrDefault(x => x.Day == dd); if (d == null) { Console.WriteLine("  " + dd.ToString("yyyy-MM-dd") + " not in the data"); continue; } foreach (var t in Bh.Day(d, cfg)) Console.WriteLine("  " + dd.ToString("yyyy-MM-dd ddd") + " entry " + R.T(t.InSlot) + " @ " + t.Entry + " stop " + t.Stop.ToString("0.00") + " → " + t.Why.Trim() + " " + R.T(t.OutSlot) + " $" + t.Usd.ToString("0")); }
            }
            return 0;
        }
        if (args.Length > 2 && args[2] == "bhrot") { Directory.CreateDirectory(outDir); BhRotation.Run(R.Load(data, "MNQ"), outDir); return 0; }
        if (args.Length > 2 && args[2] == "bh") { Directory.CreateDirectory(outDir); Bh.Run(R.Load(data, "MNQ"), outDir); return 0; } if (args.Length > 2 && args[2] != "bh") R.ExtraTicks = double.Parse(args[2], CultureInfo.InvariantCulture); Directory.CreateDirectory(outDir);
        var all = new List<VResult>(); var cal = new Dictionary<string, List<RDay>>();
        foreach (var sym in new[] { "MNQ", "MGC" })
        {
            var t0 = DateTime.Now; var days = R.Load(data, sym);
            Console.WriteLine(sym + ": " + days.Count + " full days " + days.First().Day.ToString("yyyy-MM-dd") + " .. " + days.Last().Day.ToString("yyyy-MM-dd") + " (" + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s)");
            cal[sym] = days; var vs = R.Variants(sym); vs.AddRange(R.Variants2(sym, days)); var res = new VResult[vs.Count];
            Parallel.For(0, vs.Count, i =>
            {
                var tr = new List<RTrade>(); foreach (var d in days) { var t = vs[i].Run(d); if (t != null) tr.Add(t); }
                var isT = tr.Where(t => t.Day < R.OosStart).ToList();
                var r = new VResult { V = vs[i], Trades = tr, All = Stats.Of(tr), Is = Stats.Of(isT), Oos = Stats.Of(tr.Where(t => t.Day >= R.OosStart)) };
                r.IsYearsUp = r.Is.Years.Count(y => y.Value > 0); res[i] = r;
            });
            all.AddRange(res);
            Console.WriteLine(sym + ": " + vs.Count + " variants in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        }
        using (var w = new StreamWriter(Path.Combine(outDir, "variants.csv")))
        {
            w.WriteLine("sym,family,name,is_n,is_total,is_avg,is_win,is_pf,is_t,is_maxdd,is_years_up,oos_n,oos_total,oos_avg,oos_win,oos_pf,oos_t,oos_maxdd,y2020,y2021,y2022,y2023,y2024,y2025,y2026");
            foreach (var r in all)
            {
                Func<int, string> y = yr => { double v; return r.All.Years.TryGetValue(yr, out v) ? v.ToString("0") : ""; };
                w.WriteLine(string.Join(",", r.V.Sym, r.V.Family, "\"" + r.V.Name + "\"", r.Is.N, r.Is.Total.ToString("0"), r.Is.Avg.ToString("0.00"), r.Is.Win.ToString("0.0"), r.Is.Pf.ToString("0.00"), r.Is.T.ToString("0.00"), r.Is.MaxDd.ToString("0"), r.IsYearsUp,
                    r.Oos.N, r.Oos.Total.ToString("0"), r.Oos.Avg.ToString("0.00"), r.Oos.Win.ToString("0.0"), r.Oos.Pf.ToString("0.00"), r.Oos.T.ToString("0.00"), r.Oos.MaxDd.ToString("0"), y(2020), y(2021), y(2022), y(2023), y(2024), y(2025), y(2026)));
            }
        }
        // selection on 2020–2023 only: per symbol × family the 5 best by t-stat (≥ 100 trades, ≥ 3 of 4 years up)
        Console.WriteLine("\nTOP BY 2020–2023 (selection) → 2024–2026 (unseen)");
        foreach (var g in all.Where(r => r.Is.N >= 100 && r.IsYearsUp >= 3).GroupBy(r => r.V.Sym + " " + r.V.Family).OrderBy(g => g.Key))
        {
            Console.WriteLine("== " + g.Key);
            foreach (var r in g.OrderByDescending(r => r.Is.T).Take(5))
                Console.WriteLine(string.Format("  IS t {0,5:0.0} n {1,4} avg {2,7:0.0} pf {3,4:0.00} | OOS t {4,5:0.0} n {5,4} avg {6,7:0.0} pf {7,4:0.00} tot {8,8:0} | {9}", r.Is.T, r.Is.N, r.Is.Avg, r.Is.Pf, r.Oos.T, r.Oos.N, r.Oos.Avg, r.Oos.Pf, r.Oos.Total, r.V.Name));
        }
        // WALK-FORWARD of the selection itself: each year, take the variant with the best t-stat on every earlier year, trade it that year
        Action<string, Func<VResult, bool>> wf = (label, pick) =>
        {
            var pool = all.Where(pick).ToList(); double tot = 0; var sb = new System.Text.StringBuilder();
            foreach (int y in new[] { 2022, 2023, 2024, 2025, 2026 })
            {
                VResult best = null; double bt = double.MinValue;
                foreach (var r in pool) { var pr = r.Trades.Where(t => t.Day.Year < y).ToList(); if (pr.Count < 150) continue; var st = Stats.Of(pr); if (st.T > bt) { bt = st.T; best = r; } }
                if (best == null) continue; double yv = best.Trades.Where(t => t.Day.Year == y).Sum(t => t.Usd); tot += yv;
                sb.AppendLine("    " + y + ": " + yv.ToString("0").PadLeft(7) + "  (chosen on < " + y + ", t " + bt.ToString("0.0") + ") " + best.V.Sym + " " + best.V.Name);
            }
            Console.WriteLine("WALK-FORWARD " + label + ": 2022–2026 total $" + tot.ToString("0") + " per contract\n" + sb);
        };
        wf("MNQ any rule", r => r.V.Sym == "MNQ"); wf("MGC any rule", r => r.V.Sym == "MGC");
        wf("MNQ opening-range family", r => r.V.Sym == "MNQ" && (r.V.Family == "ORB" || r.V.Family == "ORB2"));
        wf("MGC opening-range family", r => r.V.Sym == "MGC" && (r.V.Family == "ORB" || r.V.Family == "ORB2"));
        wf("MGC Asia family", r => r.V.Sym == "MGC" && r.V.Family == "ASIA");
        var sel = all.Where(r => r.Is.N >= 100 && r.IsYearsUp >= 3).ToList();
        Console.WriteLine("\nselected pool " + sel.Count + " of " + all.Count + "; OOS positive " + sel.Count(r => r.Oos.Total > 0));
        using (var pw = new StreamWriter(Path.Combine(outDir, "prop.txt"))) PropMain.Run(all, cal, outDir, pw);
        return 0;
    }
}
