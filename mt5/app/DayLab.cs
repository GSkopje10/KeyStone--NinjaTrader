// SpasiX — 📅 DAY: one server day replayed with EVERY entry setup (BREAK, FVG, VWAP, ORDER FLOW) and the 🤖 AUTO's other settings (lots, hedge,
// target, drawdown, trading times, news …), through the BACKTEST's broker — which setup would have made the most that day. Saved per day.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class DayResult
    {
        public DateTime Day; public string Symbol = "", Mode = ""; public int Iterations, Won, Lost, MaxAddOns, Legs; public double Net, WorstDd, Best, Worst; public string Note = "";
        public string Csv() { return string.Join(",", Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Symbol, Mode, Iterations, Won, Lost, MaxAddOns, Legs, Net.ToString("0.00", CultureInfo.InvariantCulture), WorstDd.ToString("0.00", CultureInfo.InvariantCulture), Best.ToString("0.00", CultureInfo.InvariantCulture), Worst.ToString("0.00", CultureInfo.InvariantCulture)); }
        public static DayResult Parse(string l)
        {
            var a = l.Split(','); var IC = CultureInfo.InvariantCulture; if (a.Length < 12) return null; DateTime d; if (!DateTime.TryParseExact(a[0], "yyyy-MM-dd", IC, DateTimeStyles.None, out d)) return null;
            try { return new DayResult { Day = d, Symbol = a[1], Mode = a[2], Iterations = int.Parse(a[3], IC), Won = int.Parse(a[4], IC), Lost = int.Parse(a[5], IC), MaxAddOns = int.Parse(a[6], IC), Legs = int.Parse(a[7], IC), Net = double.Parse(a[8], IC), WorstDd = double.Parse(a[9], IC), Best = double.Parse(a[10], IC), Worst = double.Parse(a[11], IC) }; } catch { return null; }
        }
    }

    public static class DayLab
    {
        public static readonly string[] Modes = { "BREAK", "FVG", "VWAP", "FLOW" };
        public static string ModeName(string m) { return m == "FVG" ? "FVG retest" : m == "VWAP" ? "VWAP cross" : m == "FLOW" ? "ORDER FLOW surge" : "BREAK high / low"; }
        // m1: one-minute candles of the day + the days before (the warm-up for the recent highs / lows); spreadPts < 0 = the candles' own
        public static List<DayResult> Run(SimSpec spec, List<Mt5Bar> m1, DateTime day, AutoSettings baseSet, double balance, int spreadPts, Func<DateTime, int, int, string> news)
        {
            var res = new List<DayResult>();
            foreach (var mode in Modes)
            {
                var r = new DayResult { Day = day.Date, Symbol = spec.Name, Mode = mode };
                var bars = m1.Where(b => b.Time < day.Date.AddDays(1)).ToList(); if (!bars.Any(b => b.Time.Date == day.Date)) { r.Note = "no candles that day"; res.Add(r); continue; }
                var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = spec.Name, From = day.Date, To = day.Date, Balance = balance, CommissionPerLot = Math.Max(0, baseSet.CommissionPerLot), Kind = "STANDARD", FixedSpread = spreadPts, Leverage = 500 }, spec, bars);
                if (err != "") { r.Note = err; res.Add(r); continue; }
                var s = baseSet.Copy(); s.Symbol = spec.Name; s.EntryMode = mode; s.FirstDir = "AUTO"; s.CommissionPerLot = 0;   // the simulated account charges the commission itself
                var au = new HedgeAuto { NewsNear = news }; au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb);
                var chk = au.Check(sb, s); if (chk != "") { r.Note = chk; res.Add(r); continue; }
                au.Start(sb, s, false); sb.Playing = true; sb.Speed = SimBroker.MaxSpeed;
                for (int k = 0; k < 100000 && !sb.Ended && (au.Running || au.Current != null); k++) sb.Advance(1);
                var d = au.Iterations.Where(x => x.Done).ToList(); var open = au.Current;
                r.Iterations = d.Count; r.Won = d.Count(x => x.Won); r.Lost = d.Count(x => !x.Won); r.Net = d.Sum(x => x.Result) + (open != null ? au.Basket : 0);
                r.WorstDd = au.Iterations.Count > 0 ? au.Iterations.Min(x => x.MaxDd) : 0; r.MaxAddOns = au.Iterations.Count > 0 ? au.Iterations.Max(x => x.AddOns) : 0; r.Legs = au.Trades.Count;
                r.Best = d.Count > 0 ? d.Max(x => x.Result) : 0; r.Worst = d.Count > 0 ? d.Min(x => x.Result) : 0;
                r.Note = (open != null ? "still open at the day's end " + au.Basket.ToString("+0.00;−0.00", CultureInfo.InvariantCulture) + " (counted)" : "") + (!au.Running && au.StopReason != "" && open == null && au.StopReason.Contains("drawdown") ? (open != null ? "; " : "") + "stopped: " + au.StopReason : "");
                res.Add(r);
            }
            return res.OrderByDescending(x => x.Net).ToList();
        }
        public static string FileOf(string dir, string sym) { return Path.Combine(dir, "auto", "day-setups-" + string.Concat(sym.Where(c => !Path.GetInvalidFileNameChars().Contains(c))) + ".csv"); }
        public static List<DayResult> Load(string file) { try { return File.Exists(file) ? File.ReadAllLines(file).Skip(1).Select(DayResult.Parse).Where(x => x != null).ToList() : new List<DayResult>(); } catch { return new List<DayResult>(); } }
        public static void Save(string file, List<DayResult> day)
        {
            if (day.Count == 0) return; var all = Load(file).Where(x => !(x.Day == day[0].Day && x.Symbol == day[0].Symbol)).ToList(); all.AddRange(day);
            Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllLines(file, new[] { "day,symbol,setup,iterations,won,lost,max_addons,legs,net,worst_dd,best,worst" }.Concat(all.OrderBy(x => x.Day).ThenByDescending(x => x.Net).Select(x => x.Csv())));
        }
    }
}
