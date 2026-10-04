// THE OPENING MINUTE (MNQ, 09:30 New York): what the first 1-minute candle did every day, what came right after
// (1 / 2 / 3 / 5 / 15 / 30 minutes), what happened before the open, and whether a 1–5 minute scalp on it has an edge.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public static class OpeningMinute
{
    public sealed class Row
    {
        public RDay D; public double O1, H1, L1, C1, Range1, Body1, RangeAtr; public int Dir1;   // 1 bull, −1 bear, 0 doji
        public Dictionary<int, double> Move = new Dictionary<int, double>();                      // close at 09:30+k minutes − first close, signed to the first candle (doji: raw)
        public string Path5 = "", Path15 = "", Path30 = ""; public int Min2Dir; public double Mfe5, Mae5;
        public double Gap, Overnight, Pre30, Pre5, NewsSpike; public int PrevDay; public bool Nfp;
        public string Follow5 = "";   // CONTINUED / REVERSED / NO FOLLOW-THROUGH (next 5 minutes vs the first candle)
    }

    static string PathOf(RDay d, int a, int b, double p0, double thr)
    {
        double up = d.Hi(a, b) - p0, dn = p0 - d.Lo(a, b), net = d.CloseAt(b) - p0;
        if (net >= thr && dn <= 0.5 * net) return "STRAIGHT UP";
        if (net <= -thr && up <= 0.5 * -net) return "STRAIGHT DOWN";
        if (up >= thr && dn >= thr) return "WHIPSAW";
        if (net >= thr) return "UP (CHOPPY)"; if (net <= -thr) return "DOWN (CHOPPY)";
        return "SIDEWAYS";
    }

    public static List<Row> Rows(List<RDay> days)
    {
        var rows = new List<Row>(); int o = R.S(9, 30);
        foreach (var d in days)
        {
            if (!d.Has(o) || !d.Has(o + 30) || d.Prev == null) continue;
            var r = new Row { D = d, O1 = d.O[o], H1 = d.H[o], L1 = d.L[o], C1 = d.C[o] };
            r.Range1 = r.H1 - r.L1; r.Body1 = r.C1 - r.O1; r.RangeAtr = r.Range1 / d.RthAtr;
            r.Dir1 = Math.Abs(r.Body1) < Math.Max(0.5, 0.15 * r.Range1) ? 0 : Math.Sign(r.Body1);
            int sg = r.Dir1 == 0 ? 1 : r.Dir1;
            foreach (int k in new[] { 1, 2, 3, 5, 10, 15, 30 }) r.Move[k] = sg * (d.CloseAt(o + k) - r.C1);
            r.Min2Dir = Math.Sign(d.C[o + 1] - d.O[o + 1]);
            r.Path5 = PathOf(d, o + 1, o + 5, r.C1, 0.03 * d.RthAtr); r.Path15 = PathOf(d, o + 1, o + 15, r.C1, 0.05 * d.RthAtr); r.Path30 = PathOf(d, o + 1, o + 30, r.C1, 0.08 * d.RthAtr);
            r.Mfe5 = sg > 0 ? d.Hi(o + 1, o + 5) - r.C1 : r.C1 - d.Lo(o + 1, o + 5); r.Mae5 = sg > 0 ? r.C1 - d.Lo(o + 1, o + 5) : d.Hi(o + 1, o + 5) - r.C1;
            double thr = 0.03 * d.RthAtr, net5 = d.CloseAt(o + 5) - r.C1;
            r.Follow5 = r.Dir1 == 0 ? "DOJI" : sg * net5 >= thr ? "CONTINUED" : sg * net5 <= -thr ? "REVERSED" : "NO FOLLOW-THROUGH";
            r.Gap = r.O1 - d.Prev.CloseAt(R.S(15, 59)); r.Overnight = d.CloseAt(o - 1) - d.OpenAt(0); r.Pre30 = d.CloseAt(o - 1) - d.CloseAt(o - 31); r.Pre5 = d.CloseAt(o - 1) - d.CloseAt(o - 6);
            r.NewsSpike = (d.Hi(R.S(8, 30), R.S(8, 34)) - d.Lo(R.S(8, 30), R.S(8, 34))) / d.RthAtr; r.Nfp = R.IsNfp(d.Day);
            r.PrevDay = Math.Sign(d.Prev.CloseAt(R.S(15, 59)) - d.Prev.OpenAt(o));
            rows.Add(r);
        }
        return rows;
    }

    static string P(double x) { return x.ToString("0", CultureInfo.InvariantCulture) + "%"; }
    static string F(double x, string f = "0.0") { return x.ToString(f, CultureInfo.InvariantCulture); }
    static string S(double x, string f = "0.0") { return (x >= 0 ? "+" : "") + x.ToString(f, CultureInfo.InvariantCulture); }

    public static void Run(List<RDay> days, string outDir)
    {
        var rows = Rows(days); var rep = new KReport { Title = "The Opening Minute", Instrument = "MNQ",
            Subtitle = "MNQ • the 09:30 New York 1-minute candle on " + rows.Count + " days (" + rows.First().D.Day.ToString("MMM yyyy", CultureInfo.InvariantCulture) + " – " + rows.Last().D.Day.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + ")",
            Method = "First candle = the 1-minute bar 09:30:00–09:31:00. BULL / BEAR by its body; DOJI if the body is under 15% of its range. Moves are measured from its close, in points, signed in its direction. Path over the next 5 minutes (09:31–09:36): STRAIGHT UP / DOWN = moved at least 3% of the 14-day 09:30–16:00 range with a pull-back of at most half the move; WHIPSAW = both sides; SIDEWAYS = neither. Scalps: market order at 09:31:00, $1.50 commission, 1 tick slippage per side, stop checked before target. 2020–23 picks settings, 2024–26 judges them." };
        int n = rows.Count, bull = rows.Count(r => r.Dir1 > 0), bear = rows.Count(r => r.Dir1 < 0), doji = n - bull - bear;
        var dirRows = rows.Where(r => r.Dir1 != 0).ToList();
        rep.Summary.Add(new[] { "Days", n.ToString(), "MNQ full sessions with a 09:30 candle" });
        rep.Summary.Add(new[] { "Bull / bear / doji", bull + " / " + bear + " / " + doji, P(100.0 * bull / n) + " bull • " + P(100.0 * bear / n) + " bear" });
        rep.Summary.Add(new[] { "Median first-candle range", F(Median(rows.Select(r => r.Range1)), "0") + " pts", F(Median(rows.Select(r => r.RangeAtr)) * 100, "0.0") + "% of the day's typical range" });
        rep.Summary.Add(new[] { "Next 5 min continued", P(100.0 * dirRows.Count(r => r.Follow5 == "CONTINUED") / dirRows.Count), "reversed " + P(100.0 * dirRows.Count(r => r.Follow5 == "REVERSED") / dirRows.Count) + " • no follow-through " + P(100.0 * dirRows.Count(r => r.Follow5 == "NO FOLLOW-THROUGH") / dirRows.Count) });
        rep.Summary.Add(new[] { "Avg move in its direction after 5 min", S(dirRows.Average(r => r.Move[5])) + " pts", "after 1 min " + S(dirRows.Average(r => r.Move[1])) + " • 15 min " + S(dirRows.Average(r => r.Move[15])) + " • 30 min " + S(dirRows.Average(r => r.Move[30])) });

        // 1. the first candle, by year
        var s1 = new KReport.KSection { Title = "The first candle, every year", Note = "Bull / bear share and size of the 09:30 1-minute candle.", Chart = "bars:2" };
        s1.Columns.AddRange(new[] { "Year", "Days", "Bull %", "Bear %", "Doji %", "Median range pts", "Avg range pts" });
        foreach (var g in rows.GroupBy(r => r.D.Day.Year).OrderBy(g => g.Key)) { var l = g.ToList(); s1.Rows.Add(new List<string> { g.Key.ToString(), l.Count.ToString(), P(100.0 * l.Count(r => r.Dir1 > 0) / l.Count), P(100.0 * l.Count(r => r.Dir1 < 0) / l.Count), P(100.0 * l.Count(r => r.Dir1 == 0) / l.Count), F(Median(l.Select(r => r.Range1)), "0"), F(l.Average(r => r.Range1), "0") }); }
        rep.Sections.Add(s1);

        // 2. what came next: path classes after BULL / BEAR
        foreach (var hz in new[] { Tuple.Create("5", (Func<Row, string>)(r => r.Path5)), Tuple.Create("15", (Func<Row, string>)(r => r.Path15)), Tuple.Create("30", (Func<Row, string>)(r => r.Path30)) })
        {
            var s2 = new KReport.KSection { Title = "What came next • the " + hz.Item1 + " minutes after the first candle", Note = "Share of days in each path, measured from the first candle's close." };
            var classes = new[] { "STRAIGHT UP", "UP (CHOPPY)", "SIDEWAYS", "WHIPSAW", "DOWN (CHOPPY)", "STRAIGHT DOWN" };
            s2.Columns.Add("First candle"); s2.Columns.Add("Days"); s2.Columns.AddRange(classes);
            foreach (var grp in new[] { Tuple.Create("BULL", 1), Tuple.Create("BEAR", -1), Tuple.Create("DOJI", 0) })
            {
                var l = rows.Where(r => r.Dir1 == grp.Item2).ToList(); if (l.Count == 0) continue;
                var row = new List<string> { grp.Item1, l.Count.ToString() }; row.AddRange(classes.Select(c => P(100.0 * l.Count(r => hz.Item2(r) == c) / l.Count))); s2.Rows.Add(row);
            }
            rep.Sections.Add(s2);
        }

        // 3. reversals: first candle bearish → bullish right after, and the other way
        var s3 = new KReport.KSection { Title = "Did it reverse right after?", Note = "CONTINUED / REVERSED = the next 5 minutes moved at least 3% of the typical range with / against the first candle. 2nd candle = colour of the 09:31 candle." };
        s3.Columns.AddRange(new[] { "First candle", "Days", "Continued", "Reversed", "No follow-through", "2nd candle same colour", "2nd candle opposite", "Avg pts 1 min", "Avg pts 5 min", "Avg pts 15 min" });
        foreach (var grp in new[] { Tuple.Create("BULL", 1), Tuple.Create("BEAR", -1) })
        {
            var l = rows.Where(r => r.Dir1 == grp.Item2).ToList();
            s3.Rows.Add(new List<string> { grp.Item1, l.Count.ToString(), P(100.0 * l.Count(r => r.Follow5 == "CONTINUED") / l.Count), P(100.0 * l.Count(r => r.Follow5 == "REVERSED") / l.Count), P(100.0 * l.Count(r => r.Follow5 == "NO FOLLOW-THROUGH") / l.Count),
                P(100.0 * l.Count(r => r.Min2Dir == grp.Item2) / l.Count), P(100.0 * l.Count(r => r.Min2Dir == -grp.Item2) / l.Count), S(l.Average(r => r.Move[1])), S(l.Average(r => r.Move[5])), S(l.Average(r => r.Move[15])) });
        }
        rep.Sections.Add(s3);

        // 4. points per year and per month (in the first candle's direction)
        var s4 = new KReport.KSection { Title = "Points in the first candle's direction • per year", Note = "Average close-to-close move after the first candle (positive = it kept going). Bull and bear days together; doji days left out.", Chart = "bars:5" };
        s4.Columns.AddRange(new[] { "Year", "Days", "+1 min", "+2 min", "+3 min", "+5 min", "+15 min", "+30 min", "Continued %" });
        foreach (var g in dirRows.GroupBy(r => r.D.Day.Year).OrderBy(g => g.Key)) { var l = g.ToList(); s4.Rows.Add(new List<string> { g.Key.ToString(), l.Count.ToString(), S(l.Average(r => r.Move[1])), S(l.Average(r => r.Move[2])), S(l.Average(r => r.Move[3])), S(l.Average(r => r.Move[5])), S(l.Average(r => r.Move[15])), S(l.Average(r => r.Move[30])), P(100.0 * l.Count(r => r.Follow5 == "CONTINUED") / l.Count) }); }
        rep.Sections.Add(s4);
        var s5 = new KReport.KSection { Title = "Points in the first candle's direction • per month (all years)", Note = "Same measure by calendar month." };
        s5.Columns.AddRange(new[] { "Month", "Days", "Bull %", "+1 min", "+5 min", "+15 min", "Continued %", "Reversed %" });
        foreach (var g in dirRows.GroupBy(r => r.D.Day.Month).OrderBy(g => g.Key)) { var l = g.ToList(); s5.Rows.Add(new List<string> { CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key), l.Count.ToString(), P(100.0 * l.Count(r => r.Dir1 > 0) / l.Count), S(l.Average(r => r.Move[1])), S(l.Average(r => r.Move[5])), S(l.Average(r => r.Move[15])), P(100.0 * l.Count(r => r.Follow5 == "CONTINUED") / l.Count), P(100.0 * l.Count(r => r.Follow5 == "REVERSED") / l.Count) }); }
        rep.Sections.Add(s5);

        // 5. what happened before — does anything tell the direction / the follow-through?
        var s6 = new KReport.KSection { Title = "Before the open: any warning signs?", Note = "For each condition known at 09:29: how often the first candle was bullish, how often the next 5 minutes continued its direction, and the average points 5 minutes later. A useful sign moves these well away from the all-days row." };
        s6.Columns.AddRange(new[] { "Known at 09:29", "Days", "1st candle bull %", "Continued %", "Reversed %", "Avg pts +5 min", "Same direction as the sign %" });
        Func<Row, int> none = r => 0;
        var signs = new List<Tuple<string, Func<Row, bool>, Func<Row, int>>>
        {
            Tuple.Create("All days", (Func<Row, bool>)(r => true), none),
            Tuple.Create("Gap up vs yesterday's close", (Func<Row, bool>)(r => r.Gap > 0), (Func<Row, int>)(r => 1)),
            Tuple.Create("Gap down", (Func<Row, bool>)(r => r.Gap < 0), (Func<Row, int>)(r => -1)),
            Tuple.Create("Big gap (> 10% of typical range) up", (Func<Row, bool>)(r => r.Gap > 0.1 * r.D.RthAtr), (Func<Row, int>)(r => 1)),
            Tuple.Create("Big gap down", (Func<Row, bool>)(r => r.Gap < -0.1 * r.D.RthAtr), (Func<Row, int>)(r => -1)),
            Tuple.Create("Overnight (18:00 → 09:29) up", (Func<Row, bool>)(r => r.Overnight > 0), (Func<Row, int>)(r => 1)),
            Tuple.Create("Overnight down", (Func<Row, bool>)(r => r.Overnight < 0), (Func<Row, int>)(r => -1)),
            Tuple.Create("Last 30 min before the open up", (Func<Row, bool>)(r => r.Pre30 > 0), (Func<Row, int>)(r => 1)),
            Tuple.Create("Last 30 min down", (Func<Row, bool>)(r => r.Pre30 < 0), (Func<Row, int>)(r => -1)),
            Tuple.Create("Last 5 min (09:25 → 09:29) up", (Func<Row, bool>)(r => r.Pre5 > 0), (Func<Row, int>)(r => 1)),
            Tuple.Create("Last 5 min down", (Func<Row, bool>)(r => r.Pre5 < 0), (Func<Row, int>)(r => -1)),
            Tuple.Create("Yesterday closed up", (Func<Row, bool>)(r => r.PrevDay > 0), (Func<Row, int>)(r => 1)),
            Tuple.Create("Yesterday closed down", (Func<Row, bool>)(r => r.PrevDay < 0), (Func<Row, int>)(r => -1)),
            Tuple.Create("08:30 news spike (8:30–8:35 range > 10% of typical)", (Func<Row, bool>)(r => r.NewsSpike > 0.1), none),
            Tuple.Create("Payrolls Friday", (Func<Row, bool>)(r => r.Nfp), none),
            Tuple.Create("Monday", (Func<Row, bool>)(r => r.D.Day.DayOfWeek == DayOfWeek.Monday), none),
            Tuple.Create("Friday", (Func<Row, bool>)(r => r.D.Day.DayOfWeek == DayOfWeek.Friday), none),
            Tuple.Create("Big first candle (top 25% of size)", (Func<Row, bool>)(r => r.RangeAtr >= Quantile(rows.Select(x => x.RangeAtr), 0.75)), none),
            Tuple.Create("Small first candle (bottom 25%)", (Func<Row, bool>)(r => r.RangeAtr <= Quantile(rows.Select(x => x.RangeAtr), 0.25)), none),
        };
        foreach (var sgn in signs)
        {
            var l = rows.Where(sgn.Item2).ToList(); var ld = l.Where(r => r.Dir1 != 0).ToList(); if (ld.Count < 20) continue;
            var withDir = l.Where(r => r.Dir1 != 0 && sgn.Item3(r) != 0).ToList();
            s6.Rows.Add(new List<string> { sgn.Item1, l.Count.ToString(), P(100.0 * l.Count(r => r.Dir1 > 0) / l.Count), P(100.0 * ld.Count(r => r.Follow5 == "CONTINUED") / ld.Count), P(100.0 * ld.Count(r => r.Follow5 == "REVERSED") / ld.Count), S(ld.Average(r => r.Move[5])),
                withDir.Count == 0 ? "" : P(100.0 * withDir.Count(r => r.Dir1 == sgn.Item3(r)) / withDir.Count) });
        }
        rep.Sections.Add(s6);

        // 6. the scalps
        double bigQ = Quantile(rows.Where(r => r.D.Day < R.OosStart).Select(r => r.RangeAtr), 0.75);
        var scalps = new List<Tuple<string, Func<Row, int>, Func<Row, bool>, int, double, double>>();   // name, side, filter, hold minutes, stop pts (0 = none), target pts (0 = none)
        var sides = new[] { Tuple.Create("WITH the 1st candle", (Func<Row, int>)(r => r.Dir1)), Tuple.Create("AGAINST the 1st candle", (Func<Row, int>)(r => -r.Dir1)) };
        var filters = new[] { Tuple.Create("all days", (Func<Row, bool>)(r => true)), Tuple.Create("big 1st candle", (Func<Row, bool>)(r => r.RangeAtr >= bigQ)), Tuple.Create("gap agrees", (Func<Row, bool>)(r => Math.Sign(r.Gap) == r.Dir1)), Tuple.Create("last 5 min agree", (Func<Row, bool>)(r => Math.Sign(r.Pre5) == r.Dir1)), Tuple.Create("bull 1st candle only", (Func<Row, bool>)(r => r.Dir1 > 0)), Tuple.Create("bear 1st candle only", (Func<Row, bool>)(r => r.Dir1 < 0)) };
        foreach (var sd in sides) foreach (var fl in filters) foreach (int hold in new[] { 1, 2, 3, 5 }) foreach (var bt in new[] { Tuple.Create(0.0, 0.0), Tuple.Create(10.0, 10.0), Tuple.Create(10.0, 20.0), Tuple.Create(20.0, 10.0), Tuple.Create(20.0, 20.0), Tuple.Create(15.0, 30.0), Tuple.Create(30.0, 15.0) })
            scalps.Add(Tuple.Create(sd.Item1 + " • " + fl.Item1 + " • hold " + hold + " min" + (bt.Item1 > 0 ? " • stop " + bt.Item1 + " / target " + bt.Item2 + " pts" : " • no stop / target"), sd.Item2, fl.Item2, hold, bt.Item1, bt.Item2));
        int o = R.S(9, 30); var results = new List<Tuple<string, List<RTrade>, Stats, Stats, Stats>>();
        foreach (var sc in scalps)
        {
            var tr = new List<RTrade>();
            foreach (var r in rows) { if (r.Dir1 == 0 || !sc.Item3(r)) continue; var t = R.Trade(r.D, sc.Item2(r), "MKT", 0, o + 1, o + 1, sc.Item5, sc.Item6, o + sc.Item4, "SCALP"); if (t != null) tr.Add(t); }
            results.Add(Tuple.Create(sc.Item1, tr, Stats.Of(tr), Stats.Of(tr.Where(t => t.Day < R.OosStart)), Stats.Of(tr.Where(t => t.Day >= R.OosStart))));
        }
        var s7 = new KReport.KSection { Title = "Quick scalps at 09:31 • the 15 best by 2020–23", Note = scalps.Count + " versions tested (with / against the first candle × 6 filters × hold 1–5 min × 7 stop/target pairs). Ranked on 2020–23 only; the 2024–26 column is the honest test. $ per MNQ micro after costs." };
        s7.Columns.AddRange(new[] { "Scalp", "Trades", "Win %", "$ / trade", "2020–23 $ / trade", "2024–26 $ / trade", "2021", "2022", "2023", "2024", "2025", "2026" });
        foreach (var x in results.Where(x => x.Item4.N >= 150).OrderByDescending(x => x.Item4.T).Take(15))
        {
            Func<int, string> y = yr => { double v; return x.Item3.Years.TryGetValue(yr, out v) ? S(v, "0") : ""; };
            s7.Rows.Add(new List<string> { x.Item1, x.Item3.N.ToString(), P(x.Item3.Win), S(x.Item3.Avg), S(x.Item4.Avg), S(x.Item5.Avg), y(2021), y(2022), y(2023), y(2024), y(2025), y(2026) });
        }
        rep.Sections.Add(s7);
        int pIs = results.Count(x => x.Item4.Total > 0), pOos = results.Count(x => x.Item5.Total > 0), pBoth = results.Count(x => x.Item4.Total > 0 && x.Item5.Total > 0);
        rep.Summary.Add(new[] { "Scalp versions profitable 2024–26", pOos + " of " + results.Count, "profitable in 2020–23: " + pIs + " • in both: " + pBoth });
        var bestIs = results.Where(x => x.Item4.N >= 150).OrderByDescending(x => x.Item4.T).First();
        rep.Summary.Add(new[] { "Best scalp on 2020–23", S(bestIs.Item4.Avg) + " → " + S(bestIs.Item5.Avg) + " $/trade", bestIs.Item1 + " (2020–23 → 2024–26, per micro)" });

        // 7. every day, labelled (for the studio)
        var momo = results.First(x => x.Item1 == "WITH the 1st candle • all days • hold 5 min • no stop / target").Item2.ToDictionary(t => t.Day, t => t);
        foreach (var r in rows)
        {
            var d = r.D; var kd = new KReport.KDay { Date = d.Day, Label = (r.Dir1 > 0 ? "BULL" : r.Dir1 < 0 ? "BEAR" : "DOJI") + " " + F(r.Range1, "0") + " pts → " + r.Path5 + " (5m) • " + r.Path15 + " (15m)",
                Color = r.Path5.StartsWith("STRAIGHT UP") ? "green" : r.Path5.StartsWith("STRAIGHT DOWN") ? "red" : r.Path5 == "WHIPSAW" ? "orange" : "gray",
                Note = r.Follow5 + " • +5 min " + S(r.Move[5]) + " pts in the 1st candle's direction • gap " + S(r.Gap, "0") + " • overnight " + S(r.Overnight, "0") + " • last 5 min " + S(r.Pre5, "0") };
            kd.Fields["first"] = r.Dir1 > 0 ? "BULL" : r.Dir1 < 0 ? "BEAR" : "DOJI"; kd.Fields["range_pts"] = F(r.Range1, "0.00"); kd.Fields["path5"] = r.Path5; kd.Fields["path15"] = r.Path15; kd.Fields["path30"] = r.Path30; kd.Fields["follow5"] = r.Follow5;
            foreach (int k in new[] { 1, 2, 3, 5, 15, 30 }) kd.Fields["move" + k] = F(r.Move[k], "0.00");
            kd.Fields["gap"] = F(r.Gap, "0.00"); kd.Fields["overnight"] = F(r.Overnight, "0.00"); kd.Fields["pre5"] = F(r.Pre5, "0.00");
            kd.Markers.Add(new KReport.KMarker { Time = "09:30", Price = r.Dir1 >= 0 ? r.H1 : r.L1, Text = "1ST " + kd.Fields["first"] + " " + F(r.Range1, "0") + " pts", Kind = r.Dir1 > 0 ? "buy" : r.Dir1 < 0 ? "sell" : "info" });
            kd.Markers.Add(new KReport.KMarker { Time = "09:35", Price = d.CloseAt(o + 5), Text = r.Path5 + " " + S(r.Move[5]) + " pts", Kind = "info" });
            RTrade t; if (momo.TryGetValue(d.Day, out t)) kd.Trades.Add(new KReport.KTrade { Dir = t.Dir, EntryTime = R.T(t.InSlot), Entry = t.Entry, ExitTime = R.T(t.OutSlot + 1), Exit = t.Exit, Pnl = t.Usd, Why = "scalp WITH the 1st candle, 09:31 → 09:36" });
            rep.Days.Add(kd);
        }
        File.WriteAllText(Path.Combine(outDir, "opening_minute.kreport.json"), rep.Json());
        using (var w = new StreamWriter(Path.Combine(outDir, "opening_minute.csv")))
        {
            w.WriteLine("date,weekday,first,open,high,low,close,range_pts,range_x_typical,path5,path15,path30,follow5,move1,move2,move3,move5,move15,move30,mfe5,mae5,gap,overnight,pre30,pre5,news_spike,payrolls,prev_day");
            foreach (var r in rows) w.WriteLine(string.Join(",", r.D.Day.ToString("yyyy-MM-dd"), r.D.Day.DayOfWeek, r.Dir1 > 0 ? "BULL" : r.Dir1 < 0 ? "BEAR" : "DOJI", r.O1, r.H1, r.L1, r.C1, F(r.Range1, "0.00"), F(r.RangeAtr, "0.000"), r.Path5, r.Path15, r.Path30, r.Follow5, F(r.Move[1], "0.00"), F(r.Move[2], "0.00"), F(r.Move[3], "0.00"), F(r.Move[5], "0.00"), F(r.Move[15], "0.00"), F(r.Move[30], "0.00"), F(r.Mfe5, "0.00"), F(r.Mae5, "0.00"), F(r.Gap, "0.00"), F(r.Overnight, "0.00"), F(r.Pre30, "0.00"), F(r.Pre5, "0.00"), F(r.NewsSpike, "0.000"), r.Nfp, r.PrevDay));
        }
        using (var w = new StreamWriter(Path.Combine(outDir, "opening_minute.txt")))
        {
            foreach (var s in rep.Summary) w.WriteLine(s[0] + ": " + s[1] + "  (" + s[2] + ")");
            foreach (var s in rep.Sections) { w.WriteLine("\n== " + s.Title); w.WriteLine("   " + string.Join(" | ", s.Columns)); foreach (var r in s.Rows) w.WriteLine("   " + string.Join(" | ", r)); }
        }
    }

    static double Median(IEnumerable<double> v) { return Quantile(v, 0.5); }
    static double Quantile(IEnumerable<double> v, double q) { var l = v.OrderBy(x => x).ToList(); return l.Count == 0 ? 0 : l[(int)Math.Min(l.Count - 1, Math.Round(q * (l.Count - 1)))]; }
}
