// SpasiX 09k — 🎞 TICK RECORDER: every quote the app receives from each MT5 terminal (LIVE / PROP / the DEMO's prices) is saved to
// DataDir\ticks\<server>\<SYMBOL>\yyyyMMdd.ticks in the SAME binary format as the BACKTEST's downloaded ticks (HistoryStore.SaveTicks): per tick
// an int = the milliseconds from the day's start (MT5 server time), a double bid, a double ask (20 bytes). Appended, buffered in memory and
// written by a background timer every few seconds (and at the app's exit) — Record never touches the disk, never blocks the UI or the bridge, and a
// failed write is silent (the ticks are dropped, trading goes on). It only WRITES FILES: it sends nothing to MT5.
// Readers: HistoryStore.LoadTicks (a day without downloaded ticks uses the recorded ones), the post-mortem / what-if replays, 📦 EXPORT FOR CLAUDE.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace KeystoneMT5
{
    public static class TickRecorder
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const int RecordBytes = 20;
        public const int FlushMs = 3000;
        // the folder (null = DataDir\ticks); tests point it at a temp folder
        public static string RootOverride;
        public static string Root { get { return RootOverride ?? Path.Combine(DataDir.Root, "ticks"); } }
        static readonly object gate = new object(), io = new object();
        static Dictionary<string, List<SimTick>> pending = new Dictionary<string, List<SimTick>>();   // file → ticks not yet written
        static Timer timer; static bool hooked; static bool? on;
        public static long Recorded, Written, Failed;
        public static string LastServer = "";   // the last server that sent a quote (the preferred one when several recorded the same day)

        // ⚙ SETUP "🎞 SAVE LIVE TICKS": ON by default; the choice is kept in DataDir\ticks\recorder.txt (all windows share it)
        public static bool On
        {
            get { if (on == null) { bool v = true; try { var f = Path.Combine(Root, "recorder.txt"); if (File.Exists(f)) v = File.ReadAllText(f).Trim() != "off"; } catch { } on = v; } return on.Value; }
            set { on = value; try { Directory.CreateDirectory(Root); File.WriteAllText(Path.Combine(Root, "recorder.txt"), value ? "on" : "off"); } catch { } if (!value) Flush(); }
        }
        public static void ResetForTests() { lock (gate) pending.Clear(); on = null; Recorded = Written = Failed = 0; LastServer = ""; }

        public static string Safe(string s) { if (string.IsNullOrWhiteSpace(s)) return "_"; foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Trim(); }
        public static string DirOf(string server, string sym) { return Path.Combine(Root, Safe(server), Safe(sym)); }
        public static string FileOf(string server, string sym, DateTime day) { return Path.Combine(DirOf(server, sym), day.ToString("yyyyMMdd", IC) + ".ticks"); }

        // one quote (MT5 server time): memory only — the timer writes it
        public static void Record(string server, string sym, DateTime time, double bid, double ask)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(sym) || !(bid > 0) || time.Year < 2000 || !On) return;
                if (!(ask > 0)) ask = bid; var f = FileOf(server, sym, time.Date);
                lock (gate)
                {
                    List<SimTick> l; if (!pending.TryGetValue(f, out l)) pending[f] = l = new List<SimTick>();
                    if (l.Count < 2000000) l.Add(new SimTick { T = time, Bid = bid, Ask = ask }); Recorded++; LastServer = server;
                    if (timer == null) { timer = new Timer(_ => Flush(), null, FlushMs, FlushMs); }
                    if (!hooked) { hooked = true; try { AppDomain.CurrentDomain.ProcessExit += (s, e) => Flush(); } catch { } }
                }
            }
            catch { }
        }

        // write what is buffered (the timer's thread, the exit, the tests); never throws
        public static void Flush()
        {
            Dictionary<string, List<SimTick>> take;
            lock (gate) { if (pending.Count == 0) return; take = pending; pending = new Dictionary<string, List<SimTick>>(); }
            lock (io)
                foreach (var kv in take)
                {
                    try
                    {
                        var day = DateTime.ParseExact(Path.GetFileNameWithoutExtension(kv.Key), "yyyyMMdd", IC); Directory.CreateDirectory(Path.GetDirectoryName(kv.Key));
                        using (var fs = new FileStream(kv.Key, FileMode.Append, FileAccess.Write, FileShare.Read))
                        {
                            if (fs.Length % RecordBytes != 0) fs.SetLength(fs.Length - fs.Length % RecordBytes);   // a torn last record (a crash mid-write) is cut
                            fs.Seek(0, SeekOrigin.End);
                            using (var w = new BinaryWriter(fs)) foreach (var t in kv.Value) { w.Write((int)(t.T - day).TotalMilliseconds); w.Write(t.Bid); w.Write(t.Ask); }
                        }
                        Written += kv.Value.Count;
                    }
                    catch { Failed += kv.Value.Count; }
                }
        }

        // the servers that recorded this symbol on this day (the preferred one first)
        public static List<string> Servers(string sym, DateTime day, string prefer = null)
        {
            var res = new List<string>();
            try
            {
                if (!Directory.Exists(Root)) return res; var name = day.ToString("yyyyMMdd", IC) + ".ticks";
                foreach (var sd in Directory.GetDirectories(Root)) { var f = Path.Combine(sd, Safe(sym), name); if (File.Exists(f) && new FileInfo(f).Length >= RecordBytes) res.Add(Path.GetFileName(sd)); }
            }
            catch { }
            var p = prefer ?? LastServer; return res.OrderBy(x => string.Equals(x, Safe(p ?? ""), StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenByDescending(x => SafeLen(Path.Combine(Root, x, Safe(sym), day.ToString("yyyyMMdd", IC) + ".ticks"))).ToList();
        }
        static long SafeLen(string f) { try { return new FileInfo(f).Length; } catch { return 0; } }
        public static bool Has(string sym, DateTime day) { return Servers(sym, day).Count > 0; }
        public static string BestFile(string sym, DateTime day, string prefer = null) { var s = Servers(sym, day, prefer); return s.Count == 0 ? null : FileOf(s[0], sym, day); }

        // one recorded day (the preferred server, else the one with the most ticks); what is still buffered is written first; sorted by time
        public static List<SimTick> Load(string sym, DateTime day, string prefer = null)
        {
            Flush(); var f = BestFile(sym, day.Date, prefer); return f == null ? new List<SimTick>() : ReadFile(f, day.Date);
        }
        public static List<SimTick> ReadFile(string f, DateTime day)
        {
            var res = new List<SimTick>();
            try
            {
                using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new BinaryReader(fs))
                {
                    long n = fs.Length / RecordBytes; res.Capacity = (int)Math.Min(n, int.MaxValue);
                    for (long i = 0; i < n; i++) { int ms = r.ReadInt32(); double b = r.ReadDouble(), a = r.ReadDouble(); res.Add(new SimTick { T = day.AddMilliseconds(ms), Bid = b, Ask = a }); }
                }
            }
            catch { }
            for (int i = 1; i < res.Count; i++) if (res[i].T < res[i - 1].T) { res.Sort((x, y) => x.T.CompareTo(y.T)); break; }
            return res;
        }
        public static List<SimTick> LoadRange(string sym, DateTime from, DateTime to, string prefer = null) { var res = new List<SimTick>(); for (var d = from.Date; d <= to.Date; d = d.AddDays(1)) res.AddRange(Load(sym, d, prefer)); return res; }

        // how many days were recorded (any server, any symbol) — the small count in ⚙ SETUP
        public static int DaysRecorded()
        {
            try { if (!Directory.Exists(Root)) return 0; return Directory.GetFiles(Root, "*.ticks", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension).Distinct().Count(); } catch { return 0; }
        }
        public static long BytesRecorded() { try { return Directory.Exists(Root) ? Directory.GetFiles(Root, "*.ticks", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0; } catch { return 0; } }

        // 📦 EXPORT FOR CLAUDE: the tick files of the days that had an iteration (the newest `maxDays` days), copied into `dest` as
        // <SYMBOL>-yyyyMMdd-<source>.ticks (source = the server for recorded ones, "download" for the BACKTEST's) — the same binary format; returns the files
        public static List<string> ExportDays(string sym, IEnumerable<DateTime> days, string dest, int maxDays = 10, string prefer = null)
        {
            var res = new List<string>(); Flush();
            foreach (var d in days.Select(x => x.Date).Distinct().OrderByDescending(x => x).Take(maxDays).OrderBy(x => x))
            {
                try
                {
                    string src = null, tag = null; var dl = HistoryStore.TickFileOf(sym, d);
                    if (File.Exists(dl) && new FileInfo(dl).Length >= RecordBytes) { src = dl; tag = "download"; }
                    else { var s = Servers(sym, d, prefer); if (s.Count > 0) { src = FileOf(s[0], sym, d); tag = s[0]; } }
                    if (src == null) continue; Directory.CreateDirectory(dest);
                    var to = Path.Combine(dest, Safe(sym) + "-" + d.ToString("yyyyMMdd", IC) + "-" + Safe(tag) + ".ticks");
                    using (var i = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) using (var o = File.Create(to)) { i.CopyTo(o); o.SetLength(o.Length - o.Length % RecordBytes); }
                    res.Add(to);
                }
                catch { }
            }
            return res;
        }
        public const string FormatText = "binary, little-endian, 20 bytes per tick: int32 = milliseconds from the day's start (00:00 MT5 SERVER time of the date in the file name), float64 bid, float64 ask; in time order (the same format as SpasiX's downloaded history ticks)";
    }
}
