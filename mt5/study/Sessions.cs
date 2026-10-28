// ONE TARGET A DAY: a basket opens inside a New York session, closes at the target and the day is done. Every session × hedge style × exit,
// for A ($350, 0.1 / 0.2, $20) and B ($5,000, 0.25 / 0.5, $100). Chosen on 2025, then checked on 2026 (the days it was not chosen on).
using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Globalization; using System.Threading.Tasks; using KeystoneMT5;
public static class Sessions
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static readonly (string name, int from, int to)[] All = {
        ("all day", -1, -1), ("Asia 19:00-23:00", 19 * 60, 23 * 60), ("London open 03:00-05:00", 3 * 60, 5 * 60), ("London 03:00-08:00", 3 * 60, 8 * 60),
        ("NY open 08:30-10:30", 8 * 60 + 30, 10 * 60 + 30), ("NY 09:30-11:30", 9 * 60 + 30, 11 * 60 + 30), ("overlap 08:00-12:00", 8 * 60, 12 * 60), ("NY pm 13:00-16:00", 13 * 60, 16 * 60) };
    static readonly (string name, int look, double zone)[] Hedges = { ("recent 5", 5, 0), ("recent 10", 10, 0), ("recent 20", 20, 0), ("recent 30", 30, 0), ("recent 60", 60, 0), ("zone $2", 10, 2), ("zone $4", 10, 4), ("zone $8", 10, 8) };
    static readonly (string name, bool flat, double stopX)[] Exits = { ("hold to target", false, 0), ("flat at session end", true, 0), ("basket stop 2x", false, 2), ("basket stop 5x", false, 5), ("flat + stop 2x", true, 2) };

    public static HedgeSettings Make(Func<string, double, double, double, int, HedgeSettings> raw, string acct, (string name, int from, int to) se, (string name, int look, double zone) h, (string name, bool flat, double stopX) x)
    {
        var s = acct == "A" ? raw("", 350, 0.1, 20, h.look) : raw("", 5000, 0.25, 100, h.look);
        s.OneTargetADay = true; s.ReEnter = false; s.Zone = h.zone; s.SessionFrom = se.from; s.SessionTo = se.to; s.FlattenAtSessionEnd = x.flat && se.from >= 0; s.BasketStop = x.stopX * s.Target;
        s.Name = acct + " | " + se.name + " | " + h.name + " | " + x.name; return s;
    }
    static string Row(HedgeResult r)
    {
        var d = r.Days.Where(x => x.Cycles > 0).ToList(); double worst = r.Days.Count == 0 ? 0 : r.Days.Min(x => x.EndEquity - x.StartEquity);
        return string.Format(IC, "{0,8:N0} {1,-17} won {2,3}/{3,-3} days  worst day {4,7:N0}  max DD {5,6:N0} ({6,3:0}%)  legs≤{7,2}", r.Net, r.Blown ? "BLOWN " + r.BlownAt.ToString("dd MMM yy", IC) : "ok", r.Days.Count(x => x.Won), r.Days.Count, worst, r.MaxDd, r.MaxDdPct, r.MaxLegs);
    }
    public static void Run(List<Mt5Bar> bars, Func<string, double, double, double, int, HedgeSettings> raw)
    {
        DateTime a0 = new DateTime(2025, 1, 6), a1 = new DateTime(2025, 12, 31), b0 = new DateTime(2026, 1, 5), b1 = new DateTime(2026, 10, 2);
        var csv = new List<string> { "account,session,hedge,exit,net_2025,blown_2025,won_days_2025,days_2025,maxdd_2025,net_2026,blown_2026,won_days_2026,days_2026,maxdd_2026" };
        foreach (var acct in new[] { "A", "B" })
        {
            var sets = new List<HedgeSettings>(); var keys = new List<string[]>();
            foreach (var se in All) foreach (var h in Hedges) foreach (var x in Exits) { sets.Add(Make(raw, acct, se, h, x)); keys.Add(new[] { acct, se.name, h.name, x.name }); }
            var r25 = new HedgeResult[sets.Count]; var r26 = new HedgeResult[sets.Count];
            Parallel.For(0, sets.Count, i => { r25[i] = HedgeLab.Run(bars, sets[i], a0, a1); r26[i] = HedgeLab.Run(bars, sets[i], b0, b1); });
            for (int i = 0; i < sets.Count; i++) csv.Add(string.Join(",", keys[i].Select(k => "\"" + k + "\"")) + "," + string.Join(",", new[] { r25[i], r26[i] }.SelectMany(r => new[] { r.Net.ToString("0", IC), r.Blown ? "1" : "0", r.Days.Count(x => x.Won).ToString(IC), r.Days.Count.ToString(IC), r.MaxDd.ToString("0", IC) })));
            Func<int, double> score = i => r25[i].Blown ? -1e9 + r25[i].Net : r25[i].Net;
            Console.WriteLine("\n================ " + (acct == "A" ? "A: $350, 0.1 / 0.2, one $20 target a day" : "B: $5,000, 0.25 / 0.5, one $100 target a day") + " ================");
            Console.WriteLine("survived 2025: " + Enumerable.Range(0, sets.Count).Count(i => !r25[i].Blown) + " of " + sets.Count + "   survived 2026: " + Enumerable.Range(0, sets.Count).Count(i => !r26[i].Blown) + "   survived both and profitable in both: " + Enumerable.Range(0, sets.Count).Count(i => !r25[i].Blown && !r26[i].Blown && r25[i].Net > 0 && r26[i].Net > 0));
            Console.WriteLine("\nTHE BEST 12 ON 2025 → the same settings on 2026:");
            foreach (var i in Enumerable.Range(0, sets.Count).OrderByDescending(score).Take(12)) Console.WriteLine("  " + sets[i].Name.Substring(4).PadRight(62) + "\n      2025 " + Row(r25[i]) + "\n      2026 " + Row(r26[i]));
            Console.WriteLine("\nEACH SESSION'S BEST ON 2025 → on 2026:");
            foreach (var se in All) { var i = Enumerable.Range(0, sets.Count).Where(k => keys[k][1] == se.name).OrderByDescending(score).First(); Console.WriteLine("  " + se.name.PadRight(24) + (keys[i][2] + " | " + keys[i][3]).PadRight(36) + " 2025 " + Row(r25[i]) + "\n" + "".PadRight(62) + " 2026 " + Row(r26[i])); }
            Console.WriteLine("\nEACH EXIT (every session and hedge style): survived 2025 / 2026, median net 2025 / 2026");
            foreach (var x in Exits) { var ix = Enumerable.Range(0, sets.Count).Where(k => keys[k][3] == x.name).ToList(); Func<HedgeResult[], double> med = rr => ix.Select(k => rr[k].Net).OrderBy(v => v).ElementAt(ix.Count / 2); Console.WriteLine(string.Format(IC, "  {0,-22} {1,3}/{2} / {3,3}/{2}   {4,8:N0} / {5,8:N0}", x.name, ix.Count(k => !r25[k].Blown), ix.Count, ix.Count(k => !r26[k].Blown), med(r25), med(r26))); }
        }
        File.WriteAllLines("../../docs/research/hedge/sessions_results.csv", csv);
    }
}
