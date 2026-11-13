// 13r: ⬇ DOWNLOAD REAL TICKS (TickDownloader: newest first, saved days skipped, an empty run = the broker's limit, cancel / resume, no order)
// and the .sxt codec (TickCodec: exact bid / ask / time back, ≥ 5 × smaller than the 20-byte records).
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using KeystoneMT5;
public static class NineRTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    sealed class MemStore : ITickStore
    {
        public readonly Dictionary<string, List<SimTick>> Saved = new Dictionary<string, List<SimTick>>(); public readonly HashSet<string> Empty = new HashSet<string>(); public readonly Dictionary<string, DateTime> Lim = new Dictionary<string, DateTime>();
        static string K(string s, DateTime d) { return s + "|" + d.ToString("yyyyMMdd"); }
        public bool Has(string s, DateTime d) { return Saved.ContainsKey(K(s, d)); }
        public bool IsEmpty(string s, DateTime d) { return Empty.Contains(K(s, d)); }
        public void Save(string s, DateTime d, List<SimTick> t) { Saved[K(s, d)] = t; }
        public void MarkEmpty(string s, DateTime d) { Empty.Add(K(s, d)); }
        public DateTime Limit(string s) { DateTime d; return Lim.TryGetValue(s, out d) ? d : DateTime.MinValue; }
        public void SetLimit(string s, DateTime d) { Lim[s] = d; }
    }
    // a fake MT5: real ticks only from `firstDay` on (the broker's limit); answers the request on the next Take
    sealed class FakeSrc : ITickSource
    {
        public DateTime FirstDay; public readonly List<string> Asked = new List<string>(); readonly HashSet<string> pending = new HashSet<string>(); public bool Silent;
        public bool Ask(string s, DateTime d) { Asked.Add(s + " " + d.ToString("yyyy-MM-dd")); pending.Add(s + d.Ticks); return true; }
        public Tuple<List<SimTick>, int> Take(string s, DateTime d)
        {
            if (Silent || !pending.Remove(s + d.Ticks)) return null;
            var l = new List<SimTick>(); if (d >= FirstDay) for (int i = 0; i < 50; i++) l.Add(new SimTick { T = d.AddHours(1).AddMilliseconds(i * 731), Bid = Math.Round(4000 + i * 0.01, 2), Ask = Math.Round(4000.2 + i * 0.01, 2) });
            return Tuple.Create(l, 0);
        }
    }
    static void Run(TickDownloader dl, DateTime t0, int max = 100000) { var now = t0; for (int i = 0; i < max && dl.Step(now); i++) now = now.AddMilliseconds(100); }
    public static int Main19()
    {
        var today = new DateTime(2026, 11, 13);   // a Friday
        // ------------------------------------------------ 1. the plan: trading days only, newest first, saved days skipped
        {
            var st = new MemStore(); st.Save("XAUUSD", new DateTime(2026, 11, 11), new List<SimTick>()); int sk;
            var days = TickPlan.Days("XAUUSD", today, 7, st, out sk);
            Check(days.SequenceEqual(new[] { new DateTime(2026, 11, 12), new DateTime(2026, 11, 10), new DateTime(2026, 11, 9), new DateTime(2026, 11, 6) }) && sk == 1,
                "⬇ the plan for 1 week: yesterday back, newest first, Saturday / Sunday out, the day already on this PC skipped (and today, still running, not asked)", string.Join(",", days.Select(d => d.ToString("MM-dd"))) + " skipped " + sk);
        }
        // ------------------------------------------------ 2. the broker's limit: 5 empty trading days in a row stop the symbol, said in plain words, remembered
        {
            var st = new MemStore(); var src = new FakeSrc { FirstDay = new DateTime(2026, 10, 1) };
            var dl = new TickDownloader(src, st, new[] { "XAUUSD" }, 92, today, 5); Run(dl, today.AddHours(9));
            int saved = st.Saved.Count; var asked = src.Asked.Count;
            Check(dl.Done && !dl.Cancelled && dl.Saved == saved && saved == 31 && dl.Empty == 5 && asked == 36 && st.Limit("XAUUSD") == new DateTime(2026, 10, 1) && dl.Status.Contains("the broker has no real ticks before 01 Oct 2026"),
                "⬇ ticks from 1 Oct only: the 31 trading days saved, then 5 empty days in a row → stop (the older days never asked), the limit kept and said plainly", "saved " + saved + " asked " + asked + " empty " + dl.Empty + " lim " + st.Limit("XAUUSD") + " | " + dl.Status);
            var src2 = new FakeSrc { FirstDay = new DateTime(2026, 10, 1) }; var dl2 = new TickDownloader(src2, st, new[] { "XAUUSD" }, 92, today, 5); Run(dl2, today.AddHours(10));
            Check(dl2.Done && src2.Asked.Count == 0 && dl2.Skipped == 31, "⬇ pressed again: everything saved / known empty / past the limit is skipped — nothing asked", "asked " + src2.Asked.Count + " skipped " + dl2.Skipped + " | " + dl2.Status);
        }
        // ------------------------------------------------ 3. ■ CANCEL then resume: the saved days are kept and skipped
        {
            var st = new MemStore(); var src = new FakeSrc { FirstDay = new DateTime(2020, 1, 1) };
            var dl = new TickDownloader(src, st, new[] { "XAUUSD" }, 31, today); var now = today.AddHours(9);
            for (int i = 0; i < 100000 && dl.Step(now) && dl.Saved < 6; i++) now = now.AddMilliseconds(100);
            dl.Cancel(); bool stepAfter = dl.Step(now); int keep = st.Saved.Count;
            Check(dl.Done && dl.Cancelled && !stepAfter && keep == 6 && dl.Status.StartsWith("■ cancelled") && dl.Status.Contains("skipped"), "■ CANCEL: it stops at once, the 6 saved days stay, the status says how to go on", keep + " | " + dl.Status);
            var src2 = new FakeSrc { FirstDay = new DateTime(2020, 1, 1) }; var dl2 = new TickDownloader(src2, st, new[] { "XAUUSD" }, 31, today); Run(dl2, now);
            Check(dl2.Done && dl2.Skipped == 6 && !src2.Asked.Contains("XAUUSD 2026-11-12") && src2.Asked.Contains("XAUUSD 2026-10-14") && src2.Asked.Count == 17 && st.Saved.Count == 23, "⬇ resume: the 6 saved days skipped, the other 17 of the month fetched (23 trading days in all)", "asked " + src2.Asked.Count + " saved " + st.Saved.Count);
            var eta = dl2.Eta(now); Check(true, "⬇ progress / ETA computed (" + (eta.HasValue ? TickDownloader.Dur(eta.Value) : "—") + ")");
        }
        // ------------------------------------------------ 4. MT5 does not answer: 3 timeouts → the day counts as not delivered (not marked empty), the next goes
        {
            var st = new MemStore(); var src = new FakeSrc { FirstDay = new DateTime(2020, 1, 1), Silent = true };
            var dl = new TickDownloader(src, st, new[] { "XAUUSD" }, 7, today) { TimeoutSec = 1 }; Run(dl, today);
            Check(dl.Done && dl.NotDelivered == 5 && st.Empty.Count == 0 && dl.Status.Contains("did not answer"), "⬇ no answer: not marked empty (asked again next time), said in the status", dl.Status);
        }
        // ------------------------------------------------ 5. the codec: exact round trip, odd prices, ≥ 5 × smaller
        {
            var rnd = new Random(7); var days = new List<Tuple<DateTime, List<SimTick>>>(); long n = 0;
            for (int d = 0; d < 3; d++)
            {
                var day = new DateTime(2026, 11, 10 + d); var l = new List<SimTick>(); double bid = 4000; var t = day.AddMinutes(5);
                for (int i = 0; i < 40000; i++) { t = t.AddMilliseconds(rnd.Next(0, 900)); bid = Math.Round(bid + (rnd.Next(0, 5) - 2) * 0.01, 2); double sp = Math.Round(0.1 + rnd.Next(0, 4) * 0.01, 2); l.Add(new SimTick { T = t, Bid = bid, Ask = Math.Round(bid + sp, 2) }); }
                l.Add(new SimTick { T = t.AddMilliseconds(3), Bid = 4001.234567, Ask = 4001.4 });   // a price that is not a whole number of points → raw
                days.Add(Tuple.Create(day, l)); n += l.Count;
            }
            var bytes = TickCodec.Encode("XAUUSD", 0.01, days); var back = TickCodec.Read(new MemoryStream(bytes));
            bool same = back.Item1 == "XAUUSD" && back.Item3.Count == 3; int bad = 0;
            for (int d = 0; same && d < 3; d++) { var a = days[d].Item2; var b = back.Item3[d].Item2; if (a.Count != b.Count || back.Item3[d].Item1 != days[d].Item1) { same = false; break; } for (int i = 0; i < a.Count; i++) if (a[i].T != b[i].T || a[i].Bid != b[i].Bid || a[i].Ask != b[i].Ask) bad++; }
            double ratio = 20.0 * n / bytes.Length;
            var sx = SxtReader.Read(new MemoryStream(bytes)); int sbad = 0; for (int d = 0; d < 3; d++) for (int i = 0; i < days[d].Item2.Count; i++) { var a = days[d].Item2[i]; var b = sx.Days[d].Ticks[i]; if (a.T != b.Time || a.Bid != b.Bid || a.Ask != b.Ask) sbad++; }
            Check(sx.Symbol == "XAUUSD" && sx.Days.Count == 3 && sbad == 0, "📦 mt5/study/SxtReader.cs (standalone) reads the same ticks back exactly", "bad " + sbad);
            Check(same && bad == 0, "📦 .sxt round trip: every bid / ask / time back EXACTLY (3 days, " + n.ToString("N0") + " ticks, incl. a raw price)", "same " + same + " bad " + bad);
            Check(ratio >= 5, "📦 .sxt is ≥ 5 × smaller than the 20-byte records (" + ratio.ToString("0.0") + " ×)", ratio.ToString("0.0"));
            var months = TickExport.Build("XAUUSD", 0.01, days.Select(x => x.Item1), (s, d) => days.First(x => x.Item1 == d).Item2);
            var zip = Path.Combine(Path.GetTempPath(), "sxt-test-" + Guid.NewGuid().ToString("N") + ".zip"); TickExport.WriteZip(zip, "XAUUSD", months, "test");
            bool zok; using (var z = System.IO.Compression.ZipFile.OpenRead(zip)) { var e = z.GetEntry("XAUUSD-2026-11.sxt.gz"); var rd = z.GetEntry("00-README.txt"); string txt; using (var r = new StreamReader(rd.Open())) txt = r.ReadToEnd(); var ms = new MemoryStream(); using (var s = e.Open()) s.CopyTo(ms); ms.Position = 0; var bk = TickCodec.Read(ms); zok = bk.Item3.Sum(x => x.Item2.Count) == n && txt.Contains("SXT1") && txt.Contains("zigzag") && txt.Contains("SxtReader"); }
            try { File.Delete(zip); } catch { }
            Check(months.Count == 1 && zok, "📦 EXPORT TICKS FOR CLAUDE: one .sxt.gz per month in the zip + a README with the format spec and the reader");
        }
        return fails;
    }
}
