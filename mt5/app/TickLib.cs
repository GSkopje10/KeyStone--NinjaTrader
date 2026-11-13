// SpasiX — ⬇ DOWNLOAD REAL TICKS + 📦 EXPORT TICKS FOR CLAUDE (13r, the user's request: "the download feature" — months of MT5's real XAUUSD ticks,
// not only the 1-minute candles of ⬇ DOWNLOAD YEARS).
// TickDownloader: day by day through the existing HISTT request (the bridge's CopyTicksRange), newest day first, in the background; days already on
// this PC are skipped (resumable: press again and it goes on), progress + ETA, ■ CANCEL; a symbol stops after N empty trading days in a row = the
// broker's limit (said in plain words, kept in ticks-limit.txt). It only READS history and WRITES files (HistoryStore's yyyyMMdd.ticks) — no order.
// TickCodec (.sxt): bid / ask as integer points, delta-encoded, zig-zag varints, time as varint millisecond deltas, then gzip — exact round trip.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace KeystoneMT5
{
    // where the downloader gets a day's ticks from (the app: the bridge's HISTT; tests: a fake)
    public interface ITickSource { bool Ask(string sym, DateTime day); Tuple<List<SimTick>, int> Take(string sym, DateTime day); }
    // where they are kept (the app: HistoryStore; tests: memory)
    public interface ITickStore { bool Has(string sym, DateTime day); bool IsEmpty(string sym, DateTime day); void Save(string sym, DateTime day, List<SimTick> ticks); void MarkEmpty(string sym, DateTime day); DateTime Limit(string sym); void SetLimit(string sym, DateTime day); }

    public sealed class BridgeTickSource : ITickSource
    {
        readonly IBroker live; public BridgeTickSource(IBroker b) { live = b; }
        public bool Ask(string sym, DateTime day) { return live.Send("HISTT|" + sym + "|" + Mt5State.Epoch(day) + "|" + Mt5State.Epoch(day.AddDays(1))); }
        public Tuple<List<SimTick>, int> Take(string sym, DateTime day) { var k = sym + "|" + Mt5State.Epoch(day); Tuple<List<SimTick>, int> g = null; lock (live.State.Gate) { if (live.State.TickHist.TryGetValue(k, out g)) live.State.TickHist.Remove(k); } return g; }
    }
    public sealed class DiskTickStore : ITickStore
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string Empty(string sym, DateTime d) { return Path.Combine(HistoryStore.DirOf(sym), d.ToString("yyyyMMdd", IC) + ".noticks"); }
        static string LimitFile(string sym) { return Path.Combine(HistoryStore.DirOf(sym), "ticks-limit.txt"); }
        public bool Has(string sym, DateTime day) { return HistoryStore.HasTicks(sym, day); }
        public bool IsEmpty(string sym, DateTime day) { return File.Exists(Empty(sym, day)); }
        public void Save(string sym, DateTime day, List<SimTick> t) { HistoryStore.SaveTicks(sym, day, t); }
        public void MarkEmpty(string sym, DateTime day) { try { Directory.CreateDirectory(HistoryStore.DirOf(sym)); File.WriteAllText(Empty(sym, day), "MT5 had no real ticks for this day"); } catch { } }
        public DateTime Limit(string sym) { try { var f = LimitFile(sym); DateTime d; if (File.Exists(f) && DateTime.TryParseExact(File.ReadAllText(f).Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out d)) return d; } catch { } return DateTime.MinValue; }
        public void SetLimit(string sym, DateTime day) { try { Directory.CreateDirectory(HistoryStore.DirOf(sym)); File.WriteAllText(LimitFile(sym), day.ToString("yyyy-MM-dd", IC)); } catch { } }
    }

    public static class TickPlan
    {
        public static readonly Tuple<string, int>[] Spans = { Tuple.Create("1 week", 7), Tuple.Create("1 month", 31), Tuple.Create("3 months", 92), Tuple.Create("6 months", 183), Tuple.Create("12 months", 365), Tuple.Create("as far back as the broker has", 3650) };
        public static bool Trading(DateTime d) { return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday; }
        // the days to fetch, NEWEST first: trading days from `today − 1` back `spanDays`, without the days already saved / known empty / older than the broker's limit
        public static List<DateTime> Days(string sym, DateTime today, int spanDays, ITickStore store, out int skipped) { return Days(sym, today.Date.AddDays(-spanDays), today.Date.AddDays(-1), store, out skipped); }
        // the same for a FROM → TO range (📥 DATA STUDIO), newest first
        public static List<DateTime> Days(string sym, DateTime from, DateTime to, ITickStore store, out int skipped)
        {
            var r = new List<DateTime>(); skipped = 0; var lim = store.Limit(sym);
            for (var d = to.Date; d >= from.Date; d = d.AddDays(-1))
            {
                if (!Trading(d)) continue;
                if (lim > DateTime.MinValue && d < lim) continue;
                if (store.Has(sym, d) || store.IsEmpty(sym, d)) { skipped++; continue; }
                r.Add(d);
            }
            return r;
        }
    }

    public sealed class TickDownloader
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        readonly ITickSource src; readonly ITickStore store; readonly List<string> syms; readonly int emptyStop; readonly DateTime from, to;
        List<DateTime> days = new List<DateTime>(); int si = -1, di, tries, timeouts, emptyRun; bool waiting; DateTime askedAt, retryAt, started, lastEmptyFrom;
        public string Status = "", Symbol = ""; public bool Done, Cancelled; public int Saved, Skipped, Empty, NotDelivered, TotalDays, DoneDays; public long TickCount;
        public readonly List<string> Limits = new List<string>(); public readonly List<DateTime> NewDays = new List<DateTime>();
        public double TimeoutSec = 45; public int MaxTries = 3;
        public TickDownloader(ITickSource src, ITickStore store, IEnumerable<string> symbols, int spanDays, DateTime today, int emptyStop = 5)
            : this(src, store, symbols, today.Date.AddDays(-spanDays), today.Date.AddDays(-1), emptyStop, today) { }
        // 📥 DATA STUDIO: a FROM → TO range (today itself is never asked: it is still running)
        public TickDownloader(ITickSource src, ITickStore store, IEnumerable<string> symbols, DateTime from, DateTime to, int emptyStop = 5, DateTime? today = null)
        { this.src = src; this.store = store; syms = symbols.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); this.from = from.Date; var lastOk = (today ?? DateTime.Today).Date.AddDays(-1); this.to = to.Date > lastOk ? lastOk : to.Date; this.emptyStop = Math.Max(1, emptyStop); }
        public double Progress { get { return TotalDays == 0 ? (Done ? 1 : 0) : Math.Min(1, (double)DoneDays / TotalDays); } }
        // the estimated time left from the pace so far
        public TimeSpan? Eta(DateTime now) { if (DoneDays < 2 || TotalDays <= DoneDays) return null; double per = (now - started).TotalSeconds / DoneDays; return TimeSpan.FromSeconds(per * (TotalDays - DoneDays)); }
        public static string Dur(TimeSpan t) { return t.TotalHours >= 1 ? ((int)t.TotalHours) + " h " + t.Minutes + " min" : t.TotalMinutes >= 1 ? ((int)t.TotalMinutes) + " min" : Math.Max(1, (int)t.TotalSeconds) + " s"; }
        public void Cancel() { if (Done) return; Cancelled = true; Done = true; Status = "■ cancelled — " + Saved + " day(s) saved. Press ⬇ DOWNLOAD REAL TICKS again to go on: the saved days are skipped."; }
        bool NextSymbol()
        {
            si++; if (si >= syms.Count) return false; Symbol = syms[si]; int sk; days = TickPlan.Days(Symbol, from, to, store, out sk); Skipped += sk; di = 0; emptyRun = 0; tries = 0; timeouts = 0; waiting = false;
            TotalDays += days.Count; return true;
        }
        void Finish()
        {
            Done = true;
            Status = "✓ real ticks: " + Saved + " day(s) saved (" + TickCount.ToString("N0", IC) + " ticks)" + (Skipped > 0 ? " • " + Skipped + " day(s) were already on this PC" : "") + (Empty > 0 ? " • " + Empty + " day(s) without ticks" : "")
                + (NotDelivered > 0 ? " • " + NotDelivered + " day(s) MT5 did not answer — press again later" : "") + (Limits.Count > 0 ? " • " + string.Join(" • ", Limits) : "");
        }
        // call every ~100 ms; true while it works
        public bool Step(DateTime now)
        {
            if (Done) return false;
            if (started == DateTime.MinValue) { started = now; if (!NextSymbol()) { Finish(); return false; } }
            while (di >= days.Count) { if (!NextSymbol()) { Finish(); return false; } }
            var day = days[di]; var got = src.Take(Symbol, day);
            if (got != null)
            {
                waiting = false;
                if (got.Item2 != 0 && tries < MaxTries) { tries++; retryAt = now.AddSeconds(2.0 * tries); Status = "MT5 is loading the real ticks of " + Symbol + " " + day.ToString("ddd dd MMM yyyy", IC) + " … (try " + tries + ")"; return true; }
                if (got.Item1.Count > 0) { store.Save(Symbol, day, got.Item1); Saved++; TickCount += got.Item1.Count; NewDays.Add(day); emptyRun = 0; }
                else
                {
                    Empty++; store.MarkEmpty(Symbol, day); if (emptyRun == 0) lastEmptyFrom = day; emptyRun++;
                    if (emptyRun >= emptyStop)
                    {   // N empty trading days in a row = the broker keeps no older ticks
                        var first = lastEmptyFrom.AddDays(1); store.SetLimit(Symbol, first);
                        Limits.Add(Symbol + ": the broker has no real ticks before " + first.ToString("dd MMM yyyy", IC) + " (" + emptyStop + " trading days in a row came back empty — that is how far back it keeps them)");
                        DoneDays += days.Count - di - 1; di = days.Count; return true;
                    }
                }
                Next(); return true;
            }
            if (!waiting)
            {
                if (now < retryAt) return true;
                if (!src.Ask(Symbol, day)) { Done = true; Status = "✖ MT5 is not connected — start MT5 with the SpasiXBridge (the days already saved stay; press again to go on)"; return false; }
                waiting = true; askedAt = now;
                var eta = Eta(now); Status = "⬇ " + Symbol + " real ticks " + day.ToString("ddd dd MMM yyyy", IC) + " — day " + (DoneDays + 1) + " / " + TotalDays + (eta.HasValue ? " • about " + Dur(eta.Value) + " left" : "") + " • " + Saved + " saved";
                return true;
            }
            if ((now - askedAt).TotalSeconds > TimeoutSec) { waiting = false; if (++timeouts >= MaxTries) { NotDelivered++; Next(); } }
            return true;
        }
        void Next() { di++; DoneDays++; tries = 0; timeouts = 0; waiting = false; retryAt = DateTime.MinValue; }
    }

    // 📦 .sxt — the compact tick format (the spec is TickCodec.Spec, also written into the export's README)
    public static class TickCodec
    {
        public const string Magic = "SXT1";
        public const string Spec =
            "SXT1 tick file (SpasiX 13r). The whole file is GZIP-compressed; inside, little-endian:\n"
            + "  header: 4 bytes ASCII \"SXT1\", then a varint-prefixed UTF-8 symbol, int32 digits, float64 point\n"
            + "  then blocks, one per day, until the end of the stream:\n"
            + "    int64 day (.NET DateTime ticks of the server day's 00:00), varint count\n"
            + "    count ticks, each: varint u = (dt_ms << 1) | raw   (dt_ms = milliseconds since the previous tick of the day; the first tick: since 00:00)\n"
            + "      raw = 0: zigzag-varint (bid_points − previous bid_points), zigzag-varint (spread_points − previous spread_points)\n"
            + "               bid = bid_points × point, ask = (bid_points + spread_points) × point, both rounded to `digits` decimals (exact)\n"
            + "      raw = 1: float64 bid, float64 ask (a price that is not a whole number of points); the previous points are then bid / point rounded\n"
            + "  varint = 7 bits per byte, low first, high bit = more; zigzag(n) = (n << 1) ^ (n >> 63).\n"
            + "  The previous bid_points / spread_points start at 0 for each day.";
        static void VarU(Stream s, ulong v) { while (v >= 0x80) { s.WriteByte((byte)(v | 0x80)); v >>= 7; } s.WriteByte((byte)v); }
        static ulong ReadVarU(Stream s) { ulong r = 0; int sh = 0; while (true) { int b = s.ReadByte(); if (b < 0) throw new EndOfStreamException(); r |= (ulong)(b & 0x7f) << sh; if ((b & 0x80) == 0) return r; sh += 7; } }
        static ulong Zig(long n) { return (ulong)((n << 1) ^ (n >> 63)); }
        static long Unzig(ulong u) { return (long)(u >> 1) ^ -(long)(u & 1); }
        public static int DigitsOf(double point) { int d = 0; while (d < 10 && Math.Abs(Math.Round(point * Math.Pow(10, d)) - point * Math.Pow(10, d)) > 1e-9) d++; return d; }
        public sealed class Writer : IDisposable
        {
            readonly GZipStream gz; readonly MemoryStream buf = new MemoryStream(); readonly double point; readonly int digits; readonly BinaryWriter bw;
            public Writer(Stream outp, string sym, double point)
            {
                this.point = point; digits = DigitsOf(point); gz = new GZipStream(outp, CompressionLevel.Optimal, true); bw = new BinaryWriter(gz, Encoding.UTF8, true);
                bw.Write(Encoding.ASCII.GetBytes(Magic)); var sb = Encoding.UTF8.GetBytes(sym ?? ""); VarU(gz, (ulong)sb.Length); bw.Write(sb); bw.Write(digits); bw.Write(point);
            }
            public void Day(DateTime day, IList<SimTick> ticks)
            {
                bw.Write(day.Date.Ticks); VarU(gz, (ulong)ticks.Count); long pb = 0, ps = 0; long pt = 0;
                foreach (var t in ticks)
                {
                    long ms = (long)Math.Round((t.T - day.Date).TotalMilliseconds); long dt = Math.Max(0, ms - pt); pt += dt;
                    long b = (long)Math.Round(t.Bid / point), a = (long)Math.Round(t.Ask / point); bool raw = Math.Round(b * point, digits) != t.Bid || Math.Round(a * point, digits) != t.Ask;
                    VarU(gz, ((ulong)dt << 1) | (raw ? 1UL : 0UL));
                    if (raw) { bw.Write(t.Bid); bw.Write(t.Ask); pb = b; ps = a - b; continue; }
                    VarU(gz, Zig(b - pb)); VarU(gz, Zig((a - b) - ps)); pb = b; ps = a - b;
                }
            }
            public void Dispose() { bw.Flush(); gz.Dispose(); }
        }
        // the whole file back: (symbol, point, [(day, ticks)])
        public static Tuple<string, double, List<Tuple<DateTime, List<SimTick>>>> Read(Stream inp)
        {
            var days = new List<Tuple<DateTime, List<SimTick>>>();
            using (var gz = new GZipStream(inp, CompressionMode.Decompress, true))
            using (var br = new BinaryReader(gz, Encoding.UTF8, true))
            {
                var mg = Encoding.ASCII.GetString(br.ReadBytes(4)); if (mg != Magic) throw new InvalidDataException("not an SXT1 file");
                int n = (int)ReadVarU(gz); var sym = Encoding.UTF8.GetString(br.ReadBytes(n)); int digits = br.ReadInt32(); double point = br.ReadDouble();
                while (true)
                {
                    long dayTicks; try { dayTicks = br.ReadInt64(); } catch (EndOfStreamException) { break; }
                    var day = new DateTime(dayTicks); int cnt = (int)ReadVarU(gz); var l = new List<SimTick>(cnt); long pb = 0, ps = 0, pt = 0;
                    for (int i = 0; i < cnt; i++)
                    {
                        ulong u = ReadVarU(gz); pt += (long)(u >> 1); var tm = day.AddMilliseconds(pt);
                        if ((u & 1) == 1) { double b = br.ReadDouble(), a = br.ReadDouble(); l.Add(new SimTick { T = tm, Bid = b, Ask = a }); pb = (long)Math.Round(b / point); ps = (long)Math.Round(a / point) - pb; continue; }
                        pb += Unzig(ReadVarU(gz)); ps += Unzig(ReadVarU(gz));
                        l.Add(new SimTick { T = tm, Bid = Math.Round(pb * point, digits), Ask = Math.Round((pb + ps) * point, digits) });
                    }
                    days.Add(Tuple.Create(day, l));
                }
                return Tuple.Create(sym, point, days);
            }
        }
        public static byte[] Encode(string sym, double point, IEnumerable<Tuple<DateTime, List<SimTick>>> days)
        {
            using (var ms = new MemoryStream()) { using (var w = new Writer(ms, sym, point)) foreach (var d in days) w.Day(d.Item1, d.Item2); return ms.ToArray(); }
        }
    }

    // 📦 EXPORT TICKS FOR CLAUDE: one .sxt.gz per month of the downloaded / recorded tick days in a range, + a README with the format
    public static class TickExport
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public sealed class Month { public string Name = ""; public int Days; public long Ticks, RawBytes; public byte[] Data; }
        // the tick days on this PC (downloaded first, else recorded live) in [from, to]
        public static List<DateTime> DaysOf(string sym, DateTime from, DateTime to) { var r = new List<DateTime>(); for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) if (HistoryStore.HasAnyTicks(sym, d)) r.Add(d); return r; }
        // every DOWNLOADED tick day of a symbol (HistoryStore's yyyyMMdd.ticks)
        public static List<DateTime> Downloaded(string sym)
        {
            var r = new List<DateTime>(); try { var d = HistoryStore.DirOf(sym); if (Directory.Exists(d)) foreach (var f in Directory.GetFiles(d, "*.ticks")) { DateTime t; if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyyMMdd", IC, DateTimeStyles.None, out t)) r.Add(t); } } catch { }
            r.Sort(); return r;
        }
        public const double PackRatio = 6.5;   // the size estimate before packing (raw ÷ this; the real ratio was 6–8 ×)
        public static long RawBytes(string sym, IEnumerable<DateTime> days)
        {
            long n = 0; foreach (var d in days) try { var f = HistoryStore.TickFileOf(sym, d); if (File.Exists(f)) n += new FileInfo(f).Length; } catch { }
            return n;
        }
        public static List<Month> Build(string sym, double point, IEnumerable<DateTime> days, Func<string, DateTime, List<SimTick>> load = null, Func<bool> cancel = null)
        {
            load = load ?? ((s, d) => HistoryStore.LoadTicks(s, d)); var res = new List<Month>();
            foreach (var g in days.OrderBy(d => d).GroupBy(d => new DateTime(d.Year, d.Month, 1)))
            {
                if (cancel != null && cancel()) break;
                var m = new Month { Name = sym + "-" + g.Key.ToString("yyyy-MM", IC) + ".sxt.gz" }; var list = new List<Tuple<DateTime, List<SimTick>>>();
                foreach (var d in g) { var t = load(sym, d); if (t.Count == 0) continue; list.Add(Tuple.Create(d, t)); m.Days++; m.Ticks += t.Count; m.RawBytes += 20L * t.Count; }
                if (m.Days == 0) continue; m.Data = TickCodec.Encode(sym, point, list); res.Add(m);
            }
            return res;
        }
        public static string Readme(string sym, List<Month> months, string build)
        {
            long raw = months.Sum(m => m.RawBytes), packed = months.Sum(m => (long)m.Data.Length);
            return "SPASIX REAL TICKS FOR CLAUDE\n\napp      " + build + "\nsymbol   " + sym + "\ntimes    MT5's SERVER time\nmonths   " + months.Count + " (" + months.Sum(m => m.Days) + " days, " + months.Sum(m => m.Ticks).ToString("N0", IC) + " ticks)\n"
                + "size     " + (packed / 1048576.0).ToString("0.0", IC) + " MB packed vs " + (raw / 1048576.0).ToString("0.0", IC) + " MB as 20-byte records (" + (packed > 0 ? ((double)raw / packed).ToString("0.0", IC) : "—") + " × smaller)\n\n"
                + string.Join("\n", months.Select(m => "  " + m.Name + "  " + m.Days + " days  " + m.Ticks.ToString("N0", IC) + " ticks  " + (m.Data.Length / 1024.0).ToString("N0", IC) + " KB")) + "\n\nFORMAT\n" + TickCodec.Spec
                + "\n\nREADER: mt5/study/SxtReader.cs (SxtReader.Read(path) → the days with their ticks).\n";
        }
        public static void WriteZip(string zipPath, string sym, List<Month> months, string build)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)); if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var z = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var r = z.CreateEntry("00-README.txt"); using (var w = new StreamWriter(r.Open(), new UTF8Encoding(false))) w.Write(Readme(sym, months, build));
                foreach (var m in months) { var e = z.CreateEntry(m.Name, CompressionLevel.NoCompression); using (var s = e.Open()) s.Write(m.Data, 0, m.Data.Length); }
            }
        }
    }
}
