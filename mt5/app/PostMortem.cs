// 🧠 POST-MORTEM (user's request: "save every iteration, explain what happened, learn and improve; how would we have prevented the loss / the
// huge drawdown"): when an iteration closes, the SAME candles are replayed from its start with protective alternatives, each in its own
// in-memory SimBroker (never MT5) — your settings, half size, a fixed lead, the risk guard's lock, the danger exit, trend-aware hedging,
// DEFENDER — and the one that would have cut the drawdown most while still closing ≥ $0 is the LESSON. Saved per account
// (DataDir\auto\postmortem-*.csv) and shown in 🧠 LEARN; the tally across iterations says which protection helps most often.
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq;
namespace KeystoneMT5
{
    public sealed class PostMortemAlt { public string Name = ""; public double Result, Dd, MaxLots; public bool Closed; public int Hedges; }
    public sealed class PostMortemRow
    {
        public int N; public DateTime Start, End; public string First = ""; public double Result, Dd, MaxLots; public int Hedges;
        public List<PostMortemAlt> Alts = new List<PostMortemAlt>(); public string Best = "", Lesson = "";
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public string Csv()
        {
            return string.Join(",", N.ToString(IC), Start.ToString("yyyy-MM-dd HH:mm:ss", IC), End.ToString("yyyy-MM-dd HH:mm:ss", IC), First, Result.ToString("0.00", IC), Dd.ToString("0.00", IC), MaxLots.ToString("0.##", IC), Hedges.ToString(IC),
                "\"" + Best + "\"", "\"" + Lesson.Replace("\"", "'") + "\"", "\"" + string.Join(" | ", Alts.Select(a => a.Name + ";" + a.Result.ToString("0.00", IC) + ";" + a.Dd.ToString("0.00", IC) + ";" + a.MaxLots.ToString("0.##", IC) + ";" + (a.Closed ? 1 : 0) + ";" + a.Hedges)) + "\"");
        }
        public static PostMortemRow Parse(string l)
        {
            try
            {
                var f = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
                foreach (var ch in l) { if (ch == '"') q = !q; else if (ch == ',' && !q) { f.Add(cur.ToString()); cur.Clear(); } else cur.Append(ch); } f.Add(cur.ToString());
                if (f.Count < 11) return null;
                var r = new PostMortemRow { N = int.Parse(f[0], IC), Start = DateTime.ParseExact(f[1], "yyyy-MM-dd HH:mm:ss", IC), End = DateTime.ParseExact(f[2], "yyyy-MM-dd HH:mm:ss", IC), First = f[3], Result = double.Parse(f[4], IC), Dd = double.Parse(f[5], IC), MaxLots = double.Parse(f[6], IC), Hedges = int.Parse(f[7], IC), Best = f[8], Lesson = f[9] };
                foreach (var a in f[10].Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries)) { var x = a.Split(';'); if (x.Length >= 6) r.Alts.Add(new PostMortemAlt { Name = x[0], Result = double.Parse(x[1], IC), Dd = double.Parse(x[2], IC), MaxLots = double.Parse(x[3], IC), Closed = x[4] == "1", Hedges = int.Parse(x[5], IC) }); }
                return r;
            }
            catch { return null; }
        }
    }

    public static class PostMortem
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const string Header = "iteration,start,end,first,result,worst_drawdown,max_lots,hedges,best_protection,lesson,alternatives(name;result;drawdown;max_lots;closed;hedges)";
        public static string FileOf(string dir, string key) { return Path.Combine(dir, "auto", "postmortem-" + string.Concat((key ?? "").Where(c => !Path.GetInvalidFileNameChars().Contains(c))) + ".csv"); }
        public static List<PostMortemRow> Load(string file) { try { return File.Exists(file) ? File.ReadAllLines(file).Skip(1).Select(PostMortemRow.Parse).Where(x => x != null).ToList() : new List<PostMortemRow>(); } catch { return new List<PostMortemRow>(); } }
        public static void Save(string file, PostMortemRow r)
        {
            var all = Load(file).Where(x => !(x.N == r.N && x.Start == r.Start)).ToList(); all.Add(r);
            Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllLines(file, new[] { Header }.Concat(all.OrderBy(x => x.Start).Select(x => x.Csv())));
        }

        // the alternatives: (name, how it changes YOUR settings)
        public static List<Tuple<string, Action<AutoSettings>>> Alternatives(double balance)
        {
            return new List<Tuple<string, Action<AutoSettings>>>
            {
                Tuple.Create("YOURS (replayed)", (Action<AutoSettings>)(s => { })),
                Tuple.Create("HALF SIZE", (Action<AutoSettings>)(s => { s.EntryLots = Math.Max(0.01, Math.Round(s.EntryLots / 2, 2)); s.NetAdvantage = Math.Max(0.01, Math.Round(s.NetAdvantage / 2, 2)); s.AddLots = Math.Max(0.01, Math.Round(s.AddLots / 2, 2)); s.Target = s.Target / 2; s.WinnerLots = Math.Max(0.01, Math.Round(s.WinnerLots / 2, 2)); })),
                Tuple.Create("FIXED LEAD (no growing step)", (Action<AutoSettings>)(s => { if (s.HedgeSize == "STEPLEAD") s.HedgeSize = "ADVANTAGE"; })),
                Tuple.Create("RISK GUARD LOCK", (Action<AutoSettings>)(s => { s.RiskGuard = true; s.GuardMode = "LOCK"; s.GuardLots = Math.Min(s.GuardLots > 0 ? s.GuardLots : 1, 0.6); s.GuardMargin = 500; s.GuardChop = 3; s.PairOff = true; })),
                Tuple.Create("DANGER EXIT", (Action<AutoSettings>)(s => { s.DangerExit = true; s.DangerAt = 50; s.DangerKeep = 25; })),
                Tuple.Create("TREND-AWARE HEDGE", (Action<AutoSettings>)(s => { s.TrendHedge = true; s.TrendHedgeMult = 2; })),
                Tuple.Create("HEDGE ONLY BEYOND THE RANGE + RESCUE 3", (Action<AutoSettings>)(s => { s.RangeOnly = true; s.BeAfter = 3; })),
                Tuple.Create("DEFENDER", (Action<AutoSettings>)(s => { var d = AutoSettings.Model("DEFENDER", s, balance); foreach (var f in typeof(AutoSettings).GetFields()) if (!f.IsInitOnly && !f.IsLiteral) f.SetValue(s, f.GetValue(d)); })),
            };
        }

        // replay one iteration: from its start, the same first side, one iteration only, at most `hoursAfter` past its real end
        public static PostMortemAlt Replay(SimSpec spec, List<Mt5Bar> m1, AutoIteration it, AutoSettings s0, double balance, int spreadPts, double commission, string name, double hoursAfter = 6)
        {
            var alt = new PostMortemAlt { Name = name };
            var s = s0.Copy(); s.Symbol = spec.Name; s.FirstDir = it.FirstDir == "SELL" ? "SELL" : "BUY"; s.MaxIterations = 1; s.TrackManual = false; s.Windows = ""; s.CloseAtNy = ""; s.NewsMode = "IGNORE"; s.CommissionPerLot = 0;
            DateTime stopAt = it.End.AddHours(hoursAfter);
            var bars = m1.Where(b => b.Time <= stopAt).ToList(); if (!bars.Any(b => b.Time >= it.Start)) return null;
            var sb = new SimBroker(); var err = sb.Load(new SimSettings { Symbol = spec.Name, From = it.Start.Date, To = stopAt.Date, Balance = balance, CommissionPerLot = Math.Max(0, commission), Kind = "STANDARD", FixedSpread = spreadPts, Leverage = 500 }, spec, bars);
            if (err != "") return null;
            sb.JumpTo(it.Start.AddSeconds(-1));
            var au = new HedgeAuto(); au.Send = (c, a, l) => sb.Command(c, a); sb.OnTickHook = () => au.Step(sb);
            if (au.Check(sb, s) != "") return null;
            au.Start(sb, s, false); sb.Playing = true; sb.Speed = 60; double maxLots = 0;   // the live engine's pace (a coarser one fills at other ticks)
            for (int k = 0; k < 4000000 && !sb.Ended && (au.Running || au.Current != null); k++)
            {
                sb.Advance(0.25); lock (sb.State.Gate) { maxLots = Math.Max(maxLots, sb.State.Positions.Sum(p => p.Lots)); if (sb.State.ServerNow > stopAt) break; }
                if (au.Iterations.Count > 0 && au.Iterations[0].Done) break;
            }
            var x = au.Iterations.FirstOrDefault(); if (x == null) return null;
            alt.Closed = x.Done; alt.Result = x.Done ? x.Result : au.Basket + (sb.State.Account.Balance - x.BalanceStart); alt.Dd = x.MaxDd; alt.MaxLots = Math.Max(maxLots, x.MaxGross); alt.Hedges = x.AddOns;
            return alt;
        }

        // the whole post-mortem of one iteration
        public static PostMortemRow Run(SimSpec spec, List<Mt5Bar> m1, AutoIteration it, AutoSettings yours, double balance, int spreadPts, double commission)
        {
            var r = new PostMortemRow { N = it.N, Start = it.Start, End = it.End, First = it.FirstDir, Result = it.Result, Dd = it.MaxDd, MaxLots = it.MaxGross, Hedges = it.AddOns };
            foreach (var v in Alternatives(balance))
            {
                var s = yours.Copy(); v.Item2(s);
                PostMortemAlt a = null; try { a = Replay(spec, m1, it, s, balance, spreadPts, commission, v.Item1); } catch { }
                if (a != null) r.Alts.Add(a);
            }
            Explain(r); return r;
        }

        public static void Explain(PostMortemRow r)
        {
            Func<double, string> M = v => (v >= 0 ? "+$" : "−$") + Math.Abs(v).ToString("N2", IC);
            var ok = r.Alts.Where(a => a.Name != "YOURS (replayed)" && a.Closed && a.Result >= 0).ToList();
            double real = Math.Min(r.Dd, 0);
            var best = ok.OrderByDescending(a => a.Dd).ThenByDescending(a => a.Result).FirstOrDefault();   // the shallowest drawdown that still ended ≥ $0
            if (best == null || best.Dd <= real + 1) { r.Best = "—"; r.Lesson = "#" + r.N + " (" + M(r.Result) + ", worst " + M(r.Dd) + "): none of the protections would have done better on these candles — " + (r.Dd > -50 ? "a calm basket." : "keep the size small: the move itself was the risk."); return; }
            double cut = real < 0 ? (1 - best.Dd / real) * 100 : 0;
            r.Best = best.Name;
            r.Lesson = "#" + r.N + " went " + M(r.Dd) + " deep (" + r.Hedges + " hedges, " + r.MaxLots.ToString("0.##", IC) + " lots) and closed " + M(r.Result) + ". " + best.Name + " would have gone only " + M(best.Dd) + " deep (" + cut.ToString("0", IC) + " % less) and closed " + M(best.Result) + " with " + best.MaxLots.ToString("0.##", IC) + " lots"
                + (ok.Count > 1 ? " • also safer: " + string.Join(", ", ok.Where(a => a != best && a.Dd > real + 1).OrderByDescending(a => a.Dd).Take(3).Select(a => a.Name + " " + M(a.Dd))) : "") + ".";
        }

        // across iterations: which protection was the best most often, and the drawdown it saved
        public static string Tally(List<PostMortemRow> rows)
        {
            var w = rows.Where(x => x.Best != "" && x.Best != "—").GroupBy(x => x.Best).Select(g => Tuple.Create(g.Key, g.Count(), g.Average(x => { var b = x.Alts.FirstOrDefault(a => a.Name == x.Best); return b == null ? 0 : b.Dd - x.Dd; }))).OrderByDescending(t => t.Item2).ToList();
            if (w.Count == 0) return "no lesson yet — every basket so far was calm or nothing would have done better";
            return string.Join("  •  ", w.Select(t => t.Item1 + ": best " + t.Item2 + "× (saved $" + t.Item3.ToString("N0", IC) + " of drawdown on average)"));
        }
    }
}
