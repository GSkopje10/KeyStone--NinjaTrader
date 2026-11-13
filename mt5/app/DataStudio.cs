// SpasiX — 📥 DATA STUDIO (13s, the user's request: one place ONLY to download and manage history; it never sends an order).
// DataPlan = the range presets and what a range still needs; DataInventory = what is on this PC per symbol (candles / ticks / known empty, sizes),
// a calendar per month, and 🗑 delete a range; DataJob = candles (the existing HistoryDownloader) then real ticks (TickDownloader) per symbol,
// background, resumable, cancel; DataExport = 📦 one zip for Claude (ticks .sxt.gz per month + candles .m1.csv.gz per month), its size known first.
// The files stay HistoryStore's (yyyyMMdd.csv / .none / .ticks / .noticks, ticks-limit.txt) — the BACKTEST loads them as before.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace KeystoneMT5
{
    public static class DataPlan
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static readonly Tuple<string, int>[] Presets = { Tuple.Create("1 WEEK", 7), Tuple.Create("1 MONTH", 31), Tuple.Create("3 MONTHS", 92), Tuple.Create("6 MONTHS", 183), Tuple.Create("12 MONTHS", 365), Tuple.Create("AS FAR AS THE BROKER HAS", 3650) };
        // a preset → FROM … TO (TO = yesterday: today is still running)
        public static Tuple<DateTime, DateTime> Range(int days, DateTime today) { return Tuple.Create(today.Date.AddDays(-days), today.Date.AddDays(-1)); }
        public static bool Weekday(DateTime d) { return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday; }
        // the weekdays of a range whose candles are not on this PC (and not known empty)
        public static List<DateTime> MissingCandles(string sym, DateTime from, DateTime to) { var r = new List<DateTime>(); for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) if (Weekday(d) && !HistoryStore.HasDay(sym, d)) r.Add(d); return r; }
        public static List<DateTime> MissingTicks(string sym, DateTime from, DateTime to, ITickStore store) { int sk; var l = TickPlan.Days(sym, from, to, store, out sk); l.Reverse(); return l; }
        public static string Describe(string sym, DateTime from, DateTime to, bool candles, bool ticks, ITickStore store)
        {
            var parts = new List<string>(); if (candles) parts.Add(MissingCandles(sym, from, to).Count + " day(s) of candles to get"); if (ticks) parts.Add(MissingTicks(sym, from, to, store).Count + " day(s) of real ticks to get");
            var lim = store.Limit(sym); return sym + ": " + (parts.Count == 0 ? "nothing ticked" : string.Join(", ", parts)) + (ticks && lim > DateTime.MinValue ? " (the broker keeps ticks from " + lim.ToString("dd MMM yyyy", IC) + ")" : "");
        }
    }

    public sealed class DataDay { public DateTime Day; public bool Candles, NoCandles, Ticks, NoTicks; public long CandleBytes, TickBytes; }
    public static class DataInventory
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static DateTime? DayOf(string f) { DateTime d; return DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyyMMdd", IC, DateTimeStyles.None, out d) ? d : (DateTime?)null; }
        // every day of a symbol that has a file on this PC (oldest first)
        public static List<DataDay> Days(string sym)
        {
            var map = new SortedDictionary<DateTime, DataDay>(); var dir = HistoryStore.DirOf(sym); if (!Directory.Exists(dir)) return new List<DataDay>();
            Func<DateTime, DataDay> at = d => { DataDay x; if (!map.TryGetValue(d, out x)) map[d] = x = new DataDay { Day = d }; return x; };
            foreach (var f in Directory.GetFiles(dir))
            {
                var d = DayOf(f); if (d == null) continue; var ext = Path.GetExtension(f).ToLowerInvariant(); long len = 0; try { len = new FileInfo(f).Length; } catch { }
                if (ext == ".csv") { if (len > 10) { var x = at(d.Value); x.Candles = true; x.CandleBytes = len; } }
                else if (ext == ".none") at(d.Value).NoCandles = true;
                else if (ext == ".ticks") { var x = at(d.Value); x.Ticks = true; x.TickBytes = len; }
                else if (ext == ".noticks") at(d.Value).NoTicks = true;
            }
            return map.Values.ToList();
        }
        // one glyph per calendar day: ● candles + ticks, ○ candles only, ▪ ticks only, × MT5 has none, · not on this PC (weekday), space = weekend
        public static char Glyph(DataDay x, DateTime d)
        {
            if (x != null && x.Candles && x.Ticks) return '●'; if (x != null && x.Candles) return '○'; if (x != null && x.Ticks) return '▪';
            if (x != null && (x.NoCandles || x.NoTicks)) return '×'; return DataPlan.Weekday(d) ? '·' : ' ';
        }
        public const string Legend = "● candles + real ticks   ○ candles only   ▪ ticks only   × MT5 has none   · not on this PC   (blank = weekend)";
        // the table: per month the days with candles / ticks, the sizes and a calendar line (newest month first)
        public static List<string> Table(string sym, int maxMonths = 24)
        {
            var days = Days(sym); var L = new List<string>(); var lim = new DiskTickStore().Limit(sym);
            long cb = days.Sum(x => x.CandleBytes), tb = days.Sum(x => x.TickBytes);
            L.Add(sym + " — " + days.Count(x => x.Candles) + " day(s) of candles (" + Mb(cb) + "), " + days.Count(x => x.Ticks) + " day(s) of real ticks (" + Mb(tb) + ")" + (days.Count > 0 ? ", " + days[0].Day.ToString("dd MMM yyyy", IC) + " → " + days[days.Count - 1].Day.ToString("dd MMM yyyy", IC) : ", nothing on this PC yet")
                + (lim > DateTime.MinValue ? " • the broker keeps real ticks from " + lim.ToString("dd MMM yyyy", IC) : ""));
            if (days.Count == 0) return L;
            var byDay = days.ToDictionary(x => x.Day);
            L.Add(string.Format("   {0,-8} {1,8} {2,7} {3,7} {4,10} {5,10}  {6}", "MONTH", "CANDLES", "TICKS", "EMPTY", "CANDLE MB", "TICK MB", "1 ……… 10 ……… 20 ……… 31"));
            foreach (var g in days.GroupBy(x => new DateTime(x.Day.Year, x.Day.Month, 1)).OrderByDescending(g => g.Key).Take(maxMonths))
            {
                var cal = new StringBuilder(); for (var d = g.Key; d.Month == g.Key.Month; d = d.AddDays(1)) { DataDay x; byDay.TryGetValue(d, out x); cal.Append(Glyph(x, d)); }
                L.Add(string.Format(IC, "   {0,-8} {1,8} {2,7} {3,7} {4,10:0.0} {5,10:0.0}  {6}", g.Key.ToString("yyyy-MM", IC), g.Count(x => x.Candles), g.Count(x => x.Ticks), g.Count(x => !x.Candles && !x.Ticks), g.Sum(x => x.CandleBytes) / 1048576.0, g.Sum(x => x.TickBytes) / 1048576.0, cal));
            }
            return L;
        }
        public static string Mb(long b) { return (b / 1048576.0).ToString(b < 10 * 1048576L ? "0.0" : "N0", IC) + " MB"; }
        // 🗑 delete a range: candles (.csv + .none) and / or ticks (.ticks + .noticks); a deleted range that holds the broker's tick limit forgets the limit
        public static Tuple<int, int> Delete(string sym, DateTime from, DateTime to, bool candles, bool ticks)
        {
            int c = 0, t = 0; var dir = HistoryStore.DirOf(sym); if (!Directory.Exists(dir)) return Tuple.Create(0, 0);
            foreach (var f in Directory.GetFiles(dir))
            {
                var d = DayOf(f); if (d == null || d.Value < from.Date || d.Value > to.Date) continue; var ext = Path.GetExtension(f).ToLowerInvariant();
                try
                {
                    if (candles && (ext == ".csv" || ext == ".none")) { File.Delete(f); if (ext == ".csv") c++; }
                    else if (ticks && (ext == ".ticks" || ext == ".noticks")) { File.Delete(f); if (ext == ".ticks") t++; }
                }
                catch { }
            }
            if (ticks) { var lim = new DiskTickStore().Limit(sym); if (lim > DateTime.MinValue && lim >= from.Date.AddDays(-7) && lim <= to.Date.AddDays(7)) try { File.Delete(Path.Combine(dir, "ticks-limit.txt")); } catch { } }
            return Tuple.Create(c, t);
        }
    }

    // ⬇ the download job: for each symbol the candles (HistoryDownloader, a week per request) then the real ticks (TickDownloader, day by day)
    public sealed class DataJob
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        readonly IBroker live; readonly List<string> syms; readonly DateTime from, to; readonly bool candles, ticks; int si = -1; HistoryDownloader cd; TickDownloader td; DateTime started = DateTime.MinValue;
        public bool Done, Cancelled; public string Status = ""; public readonly List<string> Results = new List<string>();
        public DataJob(IBroker live, IEnumerable<string> symbols, DateTime from, DateTime to, bool candles, bool ticks) { this.live = live; syms = symbols.ToList(); this.from = from.Date; this.to = to.Date; this.candles = candles; this.ticks = ticks; }
        public double Progress { get { if (syms.Count == 0) return 1; double part = cd != null && !cd.Done ? (ticks ? 0.3 : 1) * cd.Progress : td != null ? (candles ? 0.3 : 0) + (candles ? 0.7 : 1) * td.Progress : 0; return Math.Min(1, (Math.Max(0, si) + part) / syms.Count); } }
        public TimeSpan? Eta(DateTime now) { double p = Progress; if (p < 0.02 || started == DateTime.MinValue) return null; return TimeSpan.FromSeconds((now - started).TotalSeconds / p * (1 - p)); }
        public void Cancel() { if (Done) return; Cancelled = true; Done = true; if (td != null) td.Cancel(); Status = "■ cancelled — what was saved stays; press ⬇ DOWNLOAD again to go on (the saved days are skipped)"; }
        public bool Step(DateTime now)
        {
            if (Done) return false; if (started == DateTime.MinValue) started = now;
            if (cd == null && td == null) { si++; if (si >= syms.Count) { Done = true; Status = "✓ done — " + string.Join(" • ", Results); return false; } SaveSpec(syms[si]); if (candles) cd = new HistoryDownloader(live, syms[si], from, to); else td = NewTicks(); }
            if (cd != null)
            {
                bool more = cd.Step(now); Status = cd.Status; var eta0 = Eta(now); if (eta0.HasValue && !cd.Done) Status += " • about " + TickDownloader.Dur(eta0.Value) + " left";
                if (!more || cd.Done) { Results.Add(cd.Status.TrimStart('✓', ' ')); bool failed = cd.Failed; cd = null; if (failed) { Done = true; Status = Results.Last(); return false; } if (ticks) td = NewTicks(); }
                return true;
            }
            if (td != null)
            {
                bool more = td.Step(now); Status = td.Status; var eta = Eta(now); if (eta.HasValue && !td.Done) Status += " • all: about " + TickDownloader.Dur(eta.Value) + " left";
                if (!more || td.Done) { Results.Add(td.Status.TrimStart('✓', ' ')); bool failed = td.Status.StartsWith("✖"); td = null; if (failed) { Done = true; Status = Results.Last(); return false; } }
                return true;
            }
            return true;
        }
        // the symbol's MT5 specification, once (the BACKTEST needs it offline; a backtest with MT5 connected refreshes it)
        void SaveSpec(string sym)
        {
            try { if (HistoryStore.LoadSpec(sym) != null) return; Mt5Symbol ms = null; int off; string cur; lock (live.State.Gate) { live.State.Symbols.TryGetValue(sym, out ms); off = live.State.ServerOffsetSec; cur = live.State.Account.Currency; } if (ms != null) HistoryStore.SaveSpec(SimSpec.From(ms, off, -7, cur)); } catch { }
        }
        TickDownloader NewTicks() { return new TickDownloader(new BridgeTickSource(live), new DiskTickStore(), new[] { syms[si] }, from, to); }
    }

    // 📦 one zip for Claude: per symbol and month the real ticks (.sxt.gz) and the 1-minute candles (.m1.csv.gz)
    public static class DataExport
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public sealed class Entry { public string Name = ""; public byte[] Data; public long Raw; }
        public static List<Entry> Build(IEnumerable<string> syms, DateTime from, DateTime to, bool candles, bool ticks, Func<bool> cancel = null)
        {
            var res = new List<Entry>();
            foreach (var sym in syms)
            {
                var days = DataInventory.Days(sym).Where(x => x.Day >= from.Date && x.Day <= to.Date).ToList();
                if (ticks) { var sp = HistoryStore.LoadSpec(sym); foreach (var m in TickExport.Build(sym, sp != null && sp.Point > 0 ? sp.Point : 0.01, days.Where(x => x.Ticks).Select(x => x.Day), (s, d) => HistoryStore.LoadTicks(s, d), cancel)) res.Add(new Entry { Name = "ticks/" + m.Name, Data = m.Data, Raw = m.RawBytes }); }
                if (candles)
                    foreach (var g in days.Where(x => x.Candles).GroupBy(x => new DateTime(x.Day.Year, x.Day.Month, 1)))
                    {
                        if (cancel != null && cancel()) break;
                        var sb = new StringBuilder("time_server,open,high,low,close,tick_volume,spread_points\n"); long raw = 0;
                        foreach (var x in g.OrderBy(x => x.Day)) { raw += x.CandleBytes; foreach (var b in HistoryStore.LoadDay(sym, x.Day)) sb.Append(b.Time.ToString("yyyy-MM-dd HH:mm", IC)).Append(',').Append(b.O.ToString("R", IC)).Append(',').Append(b.H.ToString("R", IC)).Append(',').Append(b.L.ToString("R", IC)).Append(',').Append(b.C.ToString("R", IC)).Append(',').Append(b.V.ToString(IC)).Append(',').Append(b.Spread.ToString(IC)).Append('\n'); }
                        using (var ms = new MemoryStream()) { using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true)) { var bytes = Encoding.UTF8.GetBytes(sb.ToString()); gz.Write(bytes, 0, bytes.Length); } res.Add(new Entry { Name = "candles/" + sym + "-" + g.Key.ToString("yyyy-MM", IC) + ".m1.csv.gz", Data = ms.ToArray(), Raw = raw }); }
                    }
            }
            return res;
        }
        public static long Size(List<Entry> e) { return e.Sum(x => (long)x.Data.Length); }
        public static void WriteZip(string zipPath, List<Entry> entries, string readme)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)); if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var z = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var r = z.CreateEntry("00-README.txt"); using (var w = new StreamWriter(r.Open(), new UTF8Encoding(false))) w.Write(readme);
                foreach (var e in entries) { var x = z.CreateEntry(e.Name, CompressionLevel.NoCompression); using (var s = x.Open()) s.Write(e.Data, 0, e.Data.Length); }
            }
        }
        public static string Readme(List<Entry> e, string build, DateTime from, DateTime to)
        {
            return "SPASIX HISTORY FOR CLAUDE (📥 DATA STUDIO)\n\napp      " + build + "\nrange    " + from.ToString("yyyy-MM-dd", IC) + " → " + to.ToString("yyyy-MM-dd", IC) + " (MT5's SERVER time)\nfiles    " + e.Count + ", " + (Size(e) / 1048576.0).ToString("0.0", IC) + " MB packed vs " + (e.Sum(x => x.Raw) / 1048576.0).ToString("0.0", IC) + " MB raw\n\n"
                + string.Join("\n", e.Select(x => "  " + x.Name + "  " + (x.Data.Length / 1024.0).ToString("N0", IC) + " KB")) + "\n\n"
                + "candles/*.m1.csv.gz  gzip CSV: time_server (the candle's OPENING minute), open, high, low, close (BID), tick_volume, spread_points\n"
                + "ticks/*.sxt.gz       real ticks — the format below; reader: mt5/study/SxtReader.cs\n\n" + TickCodec.Spec + "\n";
        }
    }
}
