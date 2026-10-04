// YOUR PICKS: every arrow read from the screenshots (date + time), matched to the 5-minute BH it points at, measured on the
// real 1-minute bars, and compared with every BH the rule finds — to see what makes your picks different.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public static class Shots
{
    public sealed class Setup
    {
        public DateTime Day; public int Red, Ref, Brk; public double Entry, PatLow, StopD; public bool Picked; public string File = "";
        public double DropAtr, LegAtr, MinsSinceOpen, RedRangeAtr, BelowOpenAtr, VwapDistAtr; public int Reds; public bool Lod, Sweep, BelowOpen, GapDown, PrevDayDown;
        public Dictionary<string, double> Out = new Dictionary<string, double>(); public bool Filled;
    }

    static string[] Exits = { "1R", "2R", "3R", "EOD" };

    // every 5-minute BH of a day from 09:30 to 15:00 (red → green reference → next candle trades above the reference high)
    public static List<Setup> All(RDay d)
    {
        var list = new List<Setup>(); int o = R.S(9, 30); double open = d.OpenAt(o), onLow = d.Lo(0, o - 1);
        double prevClose = d.Prev != null ? d.Prev.CloseAt(R.S(15, 59)) : double.NaN, prevOpen = d.Prev != null ? d.Prev.OpenAt(o) : double.NaN;
        for (int k = o; k + 15 <= R.S(15, 0); k += 5)
        {
            int red = k, refc = k + 5, brk = k + 10;
            double rO = d.OpenAt(red), rC = d.CloseAt(red + 4), gO = d.OpenAt(refc), gC = d.CloseAt(refc + 4);
            if (double.IsNaN(rO) || double.IsNaN(gC) || !(rC < rO) || !(gC > gO)) continue;
            double refHigh = d.Hi(refc, refc + 4), low = Math.Min(d.Lo(red, red + 4), d.Lo(refc, refc + 4)), tk = 0.25;
            var s = new Setup { Day = d.Day, Red = red, Ref = refc, Brk = brk, Entry = refHigh + tk, PatLow = low, StopD = refHigh + tk - (low - tk) };
            double hiSince = d.Hi(o, refc + 4); s.DropAtr = (hiSince - low) / d.RthAtr; s.LegAtr = (d.Hi(Math.Max(o, red - 30), refc + 4) - low) / d.RthAtr;
            s.MinsSinceOpen = brk - o; s.RedRangeAtr = (d.Hi(red, red + 4) - d.Lo(red, red + 4)) / d.RthAtr;
            s.Lod = low <= d.Lo(o, refc + 4) + 1e-9; s.Sweep = low < onLow; s.BelowOpen = low < open; s.BelowOpenAtr = (open - low) / d.RthAtr;
            s.VwapDistAtr = (R.Vwap(d, o, refc + 4) - d.CloseAt(refc + 4)) / d.RthAtr; s.GapDown = open < prevClose; s.PrevDayDown = prevClose < prevOpen;
            int reds = 0; for (int j = red; j >= o; j -= 5) { if (d.CloseAt(j + 4) < d.OpenAt(j)) reds++; else break; } s.Reds = reds;
            foreach (var x in Exits)
            {
                double tg = x == "EOD" ? 0 : double.Parse(x.Substring(0, 1)) * s.StopD;
                var t = R.Trade(d, 1, "STOP", refHigh, brk, brk + 4, s.StopD, tg, R.S(15, 55), "BH");
                if (t != null) { s.Out[x] = t.Usd; s.Filled = true; }
            }
            if (s.Filled) list.Add(s);
        }
        return list;
    }

    public static void Run(List<RDay> days, string csvPath, string outDir)
    {
        var w = new StreamWriter(Path.Combine(outDir, "picks.txt")); var byDay = days.ToDictionary(d => d.Day, d => d);
        // Rithmic charts: which time zone? (Screenshot 2025-04-30, data box T 09:30 O 19301.25 H 19304.25 L 19232 C 19295.5)
        RDay probe; if (byDay.TryGetValue(new DateTime(2025, 4, 30), out probe))
            foreach (int off in new[] { 0, 60 }) { int s0 = R.S(9, 30) + off; w.WriteLine("Rithmic probe 2025-04-30 09:30 +" + off + " min → O " + probe.OpenAt(s0) + " H " + probe.Hi(s0, s0 + 4) + " L " + probe.Lo(s0, s0 + 4) + " C " + probe.CloseAt(s0 + 4)); }
        var all = new List<Setup>(); foreach (var d in days) all.AddRange(All(d));
        var picks = new List<Setup>(); int noDay = 0, noMatch = 0, dup = 0; var used = new HashSet<string>();
        foreach (var line in File.ReadAllLines(csvPath))
        {
            var f = line.Split(','); if (f.Length < 4) continue;
            DateTime day; if (!DateTime.TryParse(f[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) continue;
            var hm = f[3].Split(':'); int mins = int.Parse(hm[0]) * 60 + int.Parse(hm[1]);   // Rithmic charts are in New York time too (2025-04-30 09:30 data box matches the bars exactly)
            int slot = mins - 18 * 60; if (slot < 0) slot += 1440;
            if (!byDay.ContainsKey(day)) { noDay++; continue; }
            var cands = all.Where(s => s.Day == day && Math.Abs(s.Brk - slot) <= 12).OrderBy(s => Math.Abs(s.Brk - slot)).ToList();
            if (cands.Count == 0) { noMatch++; continue; }
            var c = cands[0]; string key = day.ToString("yyyyMMdd") + c.Brk; if (!used.Add(key)) { dup++; continue; }
            c.Picked = true; c.File = f[0]; picks.Add(c);
        }
        var rest = all.Where(s => !s.Picked).ToList();
        w.WriteLine("ARROWS read " + File.ReadAllLines(csvPath).Length + " • outside the data (before Oct 2020 or a holiday) " + noDay + " • no BH within 12 min of the arrow " + noMatch + " • same setup twice " + dup + " • MATCHED PICKS " + picks.Count + " on " + picks.Select(p => p.Day).Distinct().Count() + " days");
        w.WriteLine("ALL 5-MINUTE BH 09:30–15:00 on " + days.Count + " days: " + all.Count + " setups (" + (all.Count / (double)days.Count).ToString("0.0") + " a day)");
        Action<string, List<Setup>> outcome = (label, l) =>
        {
            w.WriteLine("\n== " + label + " • " + l.Count + " setups");
            foreach (var x in Exits)
            {
                var v = l.Where(s => s.Out.ContainsKey(x)).Select(s => s.Out[x]).ToList(); if (v.Count == 0) continue;
                double avg = v.Average(), win = 100.0 * v.Count(z => z > 0) / v.Count, sd = Math.Sqrt(v.Sum(z => (z - avg) * (z - avg)) / Math.Max(1, v.Count - 1));
                w.WriteLine(string.Format("   stop under the pattern, target {0,-3}: win {1,3:0}% • ${2,6:0.0} a trade per micro (t {3,4:0.0}) • total ${4,7:0} • by year {5}", x, win, avg, sd > 0 ? avg / sd * Math.Sqrt(v.Count) : 0, v.Sum(),
                    string.Join(" ", l.Where(s => s.Out.ContainsKey(x)).GroupBy(s => s.Day.Year).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Sum(s => s.Out[x]).ToString("0") + "/" + g.Count()))));
            }
        };
        outcome("YOUR PICKS (the BH each arrow points at)", picks);
        outcome("EVERY OTHER BH ON THE SAME DAYS", rest.Where(s => picks.Any(p => p.Day == s.Day)).ToList());
        outcome("EVERY BH ON ALL DAYS (the plain rule)", all);
        // what makes a pick different
        w.WriteLine("\n== WHAT YOUR PICKS LOOK LIKE vs EVERY OTHER BH (median, or share of setups)");
        Func<List<Setup>, Func<Setup, double>, double> med = (l, f) => { var v = l.Select(f).OrderBy(z => z).ToList(); return v.Count == 0 ? 0 : v[v.Count / 2]; };
        Func<List<Setup>, Func<Setup, bool>, double> share = (l, f) => l.Count == 0 ? 0 : 100.0 * l.Count(f) / l.Count;
        var feats = new List<Tuple<string, Func<List<Setup>, string>>>
        {
            Tuple.Create("minutes after 09:30", (Func<List<Setup>, string>)(l => med(l, s => s.MinsSinceOpen).ToString("0"))),
            Tuple.Create("drop from the day's high (× 14-day range)", (Func<List<Setup>, string>)(l => med(l, s => s.DropAtr).ToString("0.00"))),
            Tuple.Create("drop in the 30 min before (× range)", (Func<List<Setup>, string>)(l => med(l, s => s.LegAtr).ToString("0.00"))),
            Tuple.Create("red candles in a row before the green", (Func<List<Setup>, string>)(l => med(l, s => s.Reds).ToString("0"))),
            Tuple.Create("red candle size (× range)", (Func<List<Setup>, string>)(l => med(l, s => s.RedRangeAtr).ToString("0.000"))),
            Tuple.Create("pattern low = low of the day so far", (Func<List<Setup>, string>)(l => share(l, s => s.Lod).ToString("0") + "%")),
            Tuple.Create("pattern low under the 09:30 open", (Func<List<Setup>, string>)(l => share(l, s => s.BelowOpen).ToString("0") + "%")),
            Tuple.Create("pattern low under the overnight low", (Func<List<Setup>, string>)(l => share(l, s => s.Sweep).ToString("0") + "%")),
            Tuple.Create("price under VWAP at the green close", (Func<List<Setup>, string>)(l => share(l, s => s.VwapDistAtr > 0).ToString("0") + "%")),
            Tuple.Create("gap down at the open", (Func<List<Setup>, string>)(l => share(l, s => s.GapDown).ToString("0") + "%")),
            Tuple.Create("stop size (pts)", (Func<List<Setup>, string>)(l => med(l, s => s.StopD).ToString("0.0"))),
        };
        var others = rest.Where(s => picks.Any(p => p.Day == s.Day)).ToList();
        foreach (var ft in feats) w.WriteLine(string.Format("   {0,-44} picks {1,7} • others same days {2,7} • all BH {3,7}", ft.Item1, ft.Item2(picks), ft.Item2(others), ft.Item2(all)));
        // the rule your picks suggest, tested on ALL days (honest test: it never sees the screenshots)
        w.WriteLine("\n== RULES BUILT FROM WHAT YOUR PICKS HAVE IN COMMON • tested on every day, first qualifying BH only");
        var rules = new List<Tuple<string, Func<Setup, bool>>>
        {
            Tuple.Create("low of the day so far", (Func<Setup, bool>)(s => s.Lod)),
            Tuple.Create("low of the day + drop ≥ 0.2 range", (Func<Setup, bool>)(s => s.Lod && s.DropAtr >= 0.2)),
            Tuple.Create("low of the day + drop ≥ 0.3 range", (Func<Setup, bool>)(s => s.Lod && s.DropAtr >= 0.3)),
            Tuple.Create("low of the day + 30-min drop ≥ 0.2 range", (Func<Setup, bool>)(s => s.Lod && s.LegAtr >= 0.2)),
            Tuple.Create("low of the day + under the open + 2+ reds", (Func<Setup, bool>)(s => s.Lod && s.BelowOpen && s.Reds >= 2)),
            Tuple.Create("low of the day + under VWAP + drop ≥ 0.2", (Func<Setup, bool>)(s => s.Lod && s.VwapDistAtr > 0 && s.DropAtr >= 0.2)),
            Tuple.Create("low of the day + sweep of the overnight low", (Func<Setup, bool>)(s => s.Lod && s.Sweep)),
            Tuple.Create("low of the day before 11:00", (Func<Setup, bool>)(s => s.Lod && s.MinsSinceOpen <= 90)),
        };
        foreach (var rr in rules)
        {
            var first = all.Where(rr.Item2).GroupBy(s => s.Day).Select(g => g.OrderBy(s => s.Brk).First()).ToList();
            foreach (var x in new[] { "2R", "3R", "EOD" })
            {
                var v = first.Where(s => s.Out.ContainsKey(x)).ToList(); if (v.Count < 50) continue;
                Func<IEnumerable<Setup>, double> avg = l => l.Any() ? l.Average(s => s.Out[x]) : 0;
                var isv = v.Where(s => s.Day < R.OosStart).ToList(); var oos = v.Where(s => s.Day >= R.OosStart).ToList();
                w.WriteLine(string.Format("   {0,-44} {1,-3} n {2,4} win {3,3:0}% ${4,6:0.0}/trade | 2020–23 ${5,6:0.0} | 2024–26 ${6,6:0.0} | {7}", rr.Item1, x, v.Count, 100.0 * v.Count(s => s.Out[x] > 0) / v.Count, avg(v), avg(isv), avg(oos),
                    string.Join(" ", v.GroupBy(s => s.Day.Year).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Sum(s => s.Out[x]).ToString("0")))));
            }
        }
        using (var pw = new StreamWriter(Path.Combine(outDir, "picks.csv")))
        {
            pw.WriteLine("file,day,entry_time,entry,stop_pts,minutes_after_open,drop_x_range,leg30_x_range,reds,low_of_day,below_open,sweep,usd_1R,usd_2R,usd_3R,usd_EOD");
            foreach (var p in picks.OrderBy(p => p.Day)) { Func<string, string> o = x => p.Out.ContainsKey(x) ? p.Out[x].ToString("0") : ""; pw.WriteLine(string.Join(",", p.File, p.Day.ToString("yyyy-MM-dd"), R.T(p.Brk), p.Entry, p.StopD.ToString("0.00"), p.MinsSinceOpen, p.DropAtr.ToString("0.00"), p.LegAtr.ToString("0.00"), p.Reds, p.Lod, p.BelowOpen, p.Sweep, o("1R"), o("2R"), o("3R"), o("EOD"))); }
        }
        w.Close();
    }
}
