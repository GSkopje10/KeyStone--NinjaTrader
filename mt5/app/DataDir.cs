// SpasiX — WHERE the data lives (settings, drawings, the DEMO account, the downloaded history and real ticks, the backtest POOL, the news):
// by default a second drive when there is one (F:\SpasiX\MT5 — the user's C: is nearly full), else %AppData%\SpasiX\MT5. The choice is kept in
// %AppData%\SpasiX\MT5\datadir.txt; ⚙ SETUP changes it, and the old folder's data is MOVED to the new one at the next start.
using System;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public static class DataDir
    {
        public static readonly string Default = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpasiX", "MT5");
        static string Pointer { get { return Path.Combine(Default, "datadir.txt"); } }
        public static string Root { get; private set; }
        public static string Moved = "";   // what the start-up move did (shown in ⚙ SETUP)

        static DataDir() { Root = Default; }

        // at start: read the choice (or pick F: the first time), move old data if the folder changed, point every store at it
        public static void Init(Action<string> status = null)
        {
            string target = null, from = null;
            try { if (File.Exists(Pointer)) { var l = File.ReadAllLines(Pointer); if (l.Length > 0 && l[0].Trim() != "") target = l[0].Trim(); if (l.Length > 1 && l[1].Trim() != "") from = l[1].Trim(); } } catch { }
            if (target == null) { target = FirstChoice(); if (!Same(target, Default)) from = Default; }
            try { Directory.CreateDirectory(target); } catch { target = Default; from = null; }
            if (from != null && !Same(from, target) && Directory.Exists(from)) { if (status != null) status("moving your data to " + target + " …"); Moved = Move(from, target); }
            Root = target; Write(target, null);
            HistoryStore.Root = Path.Combine(Root, "history"); BtPool.Root = Path.Combine(Root, "backtest-pool");
        }
        // ⚙ SETUP: a new folder — the data moves at the next start
        public static void Change(string target) { Write(target, Root); }
        static void Write(string target, string from) { try { Directory.CreateDirectory(Default); File.WriteAllLines(Pointer, from == null ? new[] { target } : new[] { target, from }); } catch { } }
        static string FirstChoice()
        {
            try { var f = new DriveInfo("F"); if (f.IsReady && f.DriveType == DriveType.Fixed && f.AvailableFreeSpace > 2L * 1024 * 1024 * 1024) return @"F:\SpasiX\MT5"; } catch { }
            return Default;
        }
        static bool Same(string a, string b) { try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        // copy every file (except the pointer), then delete the old copy; a file that fails stays where it was
        static string Move(string from, string to)
        {
            int n = 0, bad = 0; long bytes = 0;
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(f), "datadir.txt", StringComparison.OrdinalIgnoreCase) && Same(Path.GetDirectoryName(f), Default)) continue;
                try
                {
                    var rel = f.Substring(from.TrimEnd('\\', '/').Length).TrimStart('\\', '/'); var dst = Path.Combine(to, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)); if (!File.Exists(dst)) File.Copy(f, dst); bytes += new FileInfo(f).Length; File.Delete(f); n++;
                }
                catch { bad++; }
            }
            try { foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length)) if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { }
            return n == 0 && bad == 0 ? "" : "moved " + n + " file(s), " + (bytes / 1048576.0).ToString("0") + " MB, from " + from + " to " + to + (bad > 0 ? " (" + bad + " could not be moved and stay there)" : "");
        }
    }
}
