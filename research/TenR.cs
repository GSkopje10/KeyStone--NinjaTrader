// TEN-R SCAN: every 1-minute bar of 6 years as an entry (at its close), both directions, with a tiny fixed stop and a 10x target.
// No entry rule — it answers "how often did price give +target before -stop, and when". MGC: stop 1.0 pt / target 10.0 pts
// (10 micros = $100 risk / $1,000 win). A bar that touches both the stop and the target counts as a LOSS (stop first:
// 1-minute bars do not say which came first). Trades still open at the 17:00 session end close at the last price.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class TenR
{
    sealed class Hit { public RDay Day; public int Slot, Dir, OutSlot; public double Entry, Mfe; public string Result; }

    public static void Run(List<RDay> days, string sym, double stopPts, double targetPts, int qty, string outDir)
    {
        double pv = sym == "MGC" ? 10.0 : 2.0, fee = 2 * 0.62 * qty;   // commission both sides per contract
        var hits = new List<Hit>(); long entries = 0, losses = 0, timeouts = 0, ambiguous = 0; double timeoutPts = 0;
        foreach (var d in days)
        {
            for (int k = 0; k < R.Slots - 1; k++)
            {
                if (!d.Has(k)) continue; double e = d.C[k];
                foreach (int dir in new[] { 1, -1 })
                {
                    entries++; double stop = e - dir * stopPts, tgt = e + dir * targetPts; string res = null; int outK = -1; double mfe = 0;
                    for (int j = k + 1; j < R.Slots; j++)
                    {
                        if (!d.Has(j)) continue;
                        bool hitStop = dir > 0 ? d.L[j] <= stop + 1e-6 : d.H[j] >= stop - 1e-6, hitTgt = dir > 0 ? d.H[j] >= tgt - 1e-6 : d.L[j] <= tgt + 1e-6;
                        if (hitStop) { res = "LOSS"; outK = j; if (hitTgt) ambiguous++; break; }
                        mfe = Math.Max(mfe, dir > 0 ? d.H[j] - e : e - d.L[j]);
                        if (hitTgt) { res = "WIN"; outK = j; break; }
                    }
                    if (res == null) { timeouts++; double last = d.CloseAt(R.Slots - 1); timeoutPts += dir * (last - e); continue; }
                    if (res == "LOSS") { losses++; continue; }
                    hits.Add(new Hit { Day = d, Slot = k, Dir = dir, Entry = e, OutSlot = outK, Mfe = mfe, Result = res });
                }
            }
        }
        // episodes: winning entries in the same direction within 15 minutes of each other = one move (you can only take it once)
        var eps = new List<List<Hit>>();
        foreach (var g in hits.GroupBy(h => h.Day.Day.Ticks + "|" + h.Dir))
        {
            List<Hit> cur = null; int lastSlot = -100;
            foreach (var h in g.OrderBy(x => x.Slot)) { if (cur == null || h.Slot - lastSlot > 15) { cur = new List<Hit>(); eps.Add(cur); } cur.Add(h); lastSlot = h.Slot; }
        }
        Func<RDay, int> yearOf = d => d.Day.Year;
        double winUsd = targetPts * pv * qty - fee, lossUsd = -stopPts * pv * qty - fee;
        long wins = hits.Count; double winRate = 100.0 * wins / Math.Max(1, entries), needed = 100.0 * stopPts / (stopPts + targetPts);
        var sb = new StringBuilder();
        sb.AppendLine("# " + sym + " TEN-R SCAN • every 1-minute bar as an entry • stop " + stopPts + " pt / target " + targetPts + " pts • " + qty + " micros");
        sb.AppendLine();
        sb.AppendLine(days.Count + " full sessions " + days.First().Day.ToString("yyyy-MM-dd") + " .. " + days.Last().Day.ToString("yyyy-MM-dd") + " • risk $" + (stopPts * pv * qty).ToString("0") + " → win $" + (targetPts * pv * qty).ToString("0") + " a trade (fees ~$" + fee.ToString("0.00") + " a round trip)");
        sb.AppendLine();
        sb.AppendLine("## Every minute, both directions");
        sb.AppendLine("- entries tested: " + entries.ToString("N0") + " (" + (entries / 2).ToString("N0") + " minutes × buy + sell)");
        sb.AppendLine("- reached the target first: **" + wins.ToString("N0") + "** (" + winRate.ToString("0.00") + "%) • buys " + hits.Count(h => h.Dir > 0).ToString("N0") + " • sells " + hits.Count(h => h.Dir < 0).ToString("N0"));
        sb.AppendLine("- stopped first: " + losses.ToString("N0") + " (of them " + ambiguous.ToString("N0") + " bars touched both → counted as losses) • still open at 17:00: " + timeouts.ToString("N0"));
        sb.AppendLine("- break-even win rate for 1 : " + (targetPts / stopPts).ToString("0") + " = " + needed.ToString("0.0") + "% → taking EVERY minute blindly: " + (wins * winUsd + losses * lossUsd + timeoutPts * pv * qty - timeouts * fee).ToString("N0") + " $ over all entries (" + ((wins * winUsd + losses * lossUsd + timeoutPts * pv * qty - timeouts * fee) / Math.Max(1, entries)).ToString("0.00") + " $ an entry)");
        sb.AppendLine();
        sb.AppendLine("## Separate moves (winning minutes in one direction within 15 min = one move)");
        sb.AppendLine("- moves: **" + eps.Count.ToString("N0") + "** in " + days.Count + " sessions = " + (eps.Count / (double)days.Count).ToString("0.00") + " a day • if you caught ONE entry per move: $" + (eps.Count * winUsd).ToString("N0") + " (" + (eps.Count * winUsd / Math.Max(1, days.Count)).ToString("N0") + " $ a day) — hindsight: the hard part is the entry rule");
        sb.AppendLine("- minutes from entry to target (median): " + Median(eps.Select(e => (double)(e[0].OutSlot - e[0].Slot))).ToString("0") + " • the winning window per move (median): " + Median(eps.Select(e => (double)e.Count)).ToString("0") + " entry minute(s)");
        sb.AppendLine();
        sb.AppendLine("### Each year");
        sb.AppendLine("| year | sessions | moves | buys | sells | moves a day | $ one entry per move | winning entry minutes | % of all entries |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var y in days.Select(yearOf).Distinct().OrderBy(x => x))
        {
            int ses = days.Count(d => d.Day.Year == y); var ey = eps.Where(e => e[0].Day.Day.Year == y).ToList(); long hy = hits.Count(h => h.Day.Day.Year == y);
            sb.AppendLine("| " + y + " | " + ses + " | " + ey.Count + " | " + ey.Count(e => e[0].Dir > 0) + " | " + ey.Count(e => e[0].Dir < 0) + " | " + (ey.Count / (double)Math.Max(1, ses)).ToString("0.00") + " | $" + (ey.Count * winUsd).ToString("N0") + " | " + hy.ToString("N0") + " | " + (100.0 * hy / Math.Max(1, ses * 2.0 * 1379)).ToString("0.00") + "% |");
        }
        sb.AppendLine();
        sb.AppendLine("### When (New York hour the move started)");
        sb.AppendLine("| hour | moves | buys | sells | % of all moves | win rate of every entry that hour |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var hourEntries = new long[24]; foreach (var d in days) for (int k = 0; k < R.Slots - 1; k++) if (d.Has(k)) hourEntries[((k + 18 * 60) % 1440) / 60] += 2;
        for (int hh = 0; hh < 24; hh++)
        {
            int h0 = hh; var eh = eps.Where(e => ((e[0].Slot + 18 * 60) % 1440) / 60 == h0).ToList(); if (hourEntries[hh] == 0) continue;
            long wh = hits.Count(h => ((h.Slot + 18 * 60) % 1440) / 60 == h0);
            sb.AppendLine("| " + hh.ToString("00") + ":00 | " + eh.Count + " | " + eh.Count(e => e[0].Dir > 0) + " | " + eh.Count(e => e[0].Dir < 0) + " | " + (100.0 * eh.Count / Math.Max(1, eps.Count)).ToString("0.0") + "% | " + (100.0 * wh / hourEntries[hh]).ToString("0.00") + "% |");
        }
        sb.AppendLine();
        sb.AppendLine("### Days with the most moves");
        foreach (var g in eps.GroupBy(e => e[0].Day.Day).OrderByDescending(g => g.Count()).Take(15))
            sb.AppendLine("- " + g.Key.ToString("yyyy-MM-dd ddd") + ": " + g.Count() + " moves (" + g.Count(e => e[0].Dir > 0) + " up, " + g.Count(e => e[0].Dir < 0) + " down) • day range " + (g.First()[0].Day.Hi(0, R.Slots - 1) - g.First()[0].Day.Lo(0, R.Slots - 1)).ToString("0.0") + " pts");
        sb.AppendLine();
        int daysWith = eps.Select(e => e[0].Day.Day).Distinct().Count();
        sb.AppendLine("Sessions with at least one move: " + daysWith + " of " + days.Count + " (" + (100.0 * daysWith / days.Count).ToString("0") + "%).");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, sym + "_tenR.md"), sb.ToString());
        var csv = new StringBuilder("date,start_ny,side,entry,stop,target,target_ny,minutes,entry_minutes_in_move\n");
        foreach (var e in eps.OrderBy(e => e[0].Day.Day).ThenBy(e => e[0].Slot))
        {
            var h = e[0]; csv.AppendLine(string.Join(",", h.Day.Day.ToString("yyyy-MM-dd"), R.T(h.Slot + 1), h.Dir > 0 ? "BUY" : "SELL", h.Entry.ToString("0.0", CultureInfo.InvariantCulture), (h.Entry - h.Dir * stopPts).ToString("0.0", CultureInfo.InvariantCulture), (h.Entry + h.Dir * targetPts).ToString("0.0", CultureInfo.InvariantCulture), R.T(h.OutSlot + 1), h.OutSlot - h.Slot, e.Count));
        }
        File.WriteAllText(Path.Combine(outDir, sym + "_tenR_moves.csv"), csv.ToString());
        Console.Write(sb.ToString());
    }
    static double Median(IEnumerable<double> xs) { var l = xs.OrderBy(x => x).ToList(); return l.Count == 0 ? 0 : l[l.Count / 2]; }
}
