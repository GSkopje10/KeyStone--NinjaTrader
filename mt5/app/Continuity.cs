// SpasiX — 🔁 CONTINUITY (09d, user's rule: "installing a new version must be a SIMPLE RESUME of everything"): every account's files are
// keyed by its login@server, never by connection order or tab position. At start-up each account finds its existing files — including the
// names older builds used (DEMO's demo-account.txt of "the first broker", histories keyed by the login only) — and links them; it never
// starts an empty history while files exist. When it cannot decide which broker owns the old demo-account.txt it asks ONCE and remembers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public static class Continuity
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static string San(string k) { k = k ?? ""; foreach (var c in Path.GetInvalidFileNameChars()) k = k.Replace(c, '_'); return k; }
        public static string LegacyDemo(string dir) { return Path.Combine(dir, "demo-account.txt"); }
        public static string OwnerFile(string dir) { return Path.Combine(dir, "demo-account.owner"); }
        public static string DemoKeyFile(string dir, string key) { return Path.Combine(dir, "demo-account-" + San(key) + ".txt"); }
        public static readonly List<string> Notes = new List<string>();   // what was linked / migrated (shown in the log)

        // 🎮 the DEMO account file of an account: always demo-account-<login@server>.txt. The old shared demo-account.txt (09c and before:
        // "the first broker keeps it") is moved ONCE to the broker that owns it: its stamped key, else the auto history that matches its deals,
        // else the only candidate, else the user's answer (remembered); undecided → nobody takes it (it stays for the next start)
        public static string DemoFile(string dir, string key, IEnumerable<string> connected, Func<List<string>, string> ask)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var mine = DemoKeyFile(dir, key); var legacy = LegacyDemo(dir);
            if (!File.Exists(legacy)) return mine;
            var owner = LegacyOwner(dir, connected, ask); if (owner == null) return mine;
            if (San(owner) != San(key)) return mine;   // another broker's: it migrates when that broker opens its DEMO
            try
            {
                if (File.Exists(mine)) { File.Copy(mine, mine + ".before-09d", true); Notes.Add("🔁 kept the account created by the old build as " + Path.GetFileName(mine) + ".before-09d"); }
                var lines = File.ReadAllLines(legacy).Where(l => !l.StartsWith("KEY|")).ToList(); lines.Add("KEY|" + key); File.WriteAllLines(mine, lines);
                File.Move(legacy, legacy + ".migrated-09d"); File.WriteAllText(OwnerFile(dir), key);
                Notes.Add("🔁 the DEMO account of " + key + " (the old demo-account.txt) is now bound to it for good — the connection order no longer matters");
                // another broker's tab took this account over in 09c: its "open basket" file named these tickets → set aside (renamed, not deleted)
                var tickets = new HashSet<string>(lines.Where(l => l.StartsWith("P|")).Select(l => l.Split('|')[1]));
                var auto = Path.Combine(dir, "auto");
                if (Directory.Exists(auto) && tickets.Count > 0)
                    foreach (var f in Directory.GetFiles(auto, "DEMO-*-open.csv"))
                    {
                        if (Path.GetFileName(f) == "DEMO-" + San(key) + "-open.csv") continue;
                        try { var t = File.ReadAllText(f); if (tickets.Any(x => t.Contains(x))) { File.Move(f, f + ".moved-09d"); Notes.Add("🔁 " + Path.GetFileName(f) + " named " + key + "'s positions → set aside"); } } catch { }
                    }
            }
            catch { return File.Exists(mine) ? mine : null; }
            return mine;
        }
        public static string LegacyOwner(string dir, IEnumerable<string> connected, Func<List<string>, string> ask)
        {
            try { var of = OwnerFile(dir); if (File.Exists(of)) { var o = File.ReadAllText(of).Trim(); if (o != "") return o; } } catch { }
            var legacy = LegacyDemo(dir); List<string> lines; try { lines = File.ReadAllLines(legacy).ToList(); } catch { return null; }
            var stamp = lines.FirstOrDefault(l => l.StartsWith("KEY|")); if (stamp != null && stamp.Length > 4) return Remember(dir, stamp.Substring(4));
            // the histories: DEMO-<key>-iterations.csv whose finished iterations end on a deal of this account
            var dealTimes = lines.Where(l => l.StartsWith("D|")).Select(l => { var a = l.Split('|'); long t; return a.Length > 3 && long.TryParse(a[3], NumberStyles.Integer, IC, out t) ? t : 0; }).Where(t => t > 0).OrderBy(t => t).ToArray();
            var cands = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in connected ?? Enumerable.Empty<string>()) if (!string.IsNullOrEmpty(k) && k.Contains("@")) cands[k] = 0;
            var auto = Path.Combine(dir, "auto");
            if (Directory.Exists(auto))
                foreach (var f in Directory.GetFiles(auto, "DEMO-*-iterations.csv"))
                {
                    var n = Path.GetFileName(f); var k = n.Substring(5, n.Length - 5 - "-iterations.csv".Length); if (!k.Contains("@")) continue;
                    var real = cands.Keys.FirstOrDefault(x => San(x) == k) ?? k; int score = 0;
                    try
                    {
                        foreach (var l in File.ReadAllLines(f).Skip(1)) { var a = l.Split(','); long end; if (a.Length > 2 && long.TryParse(a[2], NumberStyles.Integer, IC, out end) && end > 0 && Near(dealTimes, end, TimeSpan.TicksPerSecond * 5)) score++; }
                    }
                    catch { }
                    int was; cands.TryGetValue(real, out was); cands[real] = Math.Max(was, score);
                }
            var best = cands.Where(c => c.Value > 0).OrderByDescending(c => c.Value).ToList();
            if (best.Count == 1 || (best.Count > 1 && best[0].Value > best[1].Value)) return Remember(dir, best[0].Key);
            if (cands.Count == 1 && dealTimes.Length == 0) return Remember(dir, cands.Keys.First());
            if (cands.Count == 1 && !Directory.Exists(auto)) return Remember(dir, cands.Keys.First());
            if (cands.Count == 0 || ask == null) return null;
            var ans = ask(cands.Keys.OrderBy(x => x).ToList()); return string.IsNullOrEmpty(ans) ? null : Remember(dir, ans);
        }
        static bool Near(long[] sorted, long t, long tol)
        {
            int i = Array.BinarySearch(sorted, t); if (i >= 0) return true; i = ~i;
            return (i < sorted.Length && sorted[i] - t <= tol) || (i > 0 && t - sorted[i - 1] <= tol);
        }
        static string Remember(string dir, string key) { try { Directory.CreateDirectory(dir); File.WriteAllText(OwnerFile(dir), key); } catch { } return key; }

        // 📜 the HEDGE / MANUAL / RACE histories of an account: files of an older key (the login only, used while the server was unknown) are
        // copied to the login@server name when that name has none yet — the old files stay as they were
        public static int LinkHistory(string dir, string mode, string key)
        {
            if (string.IsNullOrEmpty(key) || !key.Contains("@")) return 0; var sk = San(key); var login = San(key.Split('@')[0]); int linked = 0;
            foreach (var sub in new[] { "auto", "manual" })
            {
                var d = Path.Combine(dir, sub); if (!Directory.Exists(d)) continue;
                if (Directory.GetFiles(d, mode + "-" + sk + "-*").Length > 0) continue;
                foreach (var f in Directory.GetFiles(d, mode + "-" + login + "-*"))
                {
                    var n = Path.GetFileName(f).Substring((mode + "-" + login).Length);
                    try { File.Copy(f, Path.Combine(d, mode + "-" + sk + n), false); linked++; } catch { }
                }
            }
            var rOld = Path.Combine(dir, "race", mode + "-" + login); var rNew = Path.Combine(dir, "race", mode + "-" + sk);
            if (Directory.Exists(rOld) && !Directory.Exists(rNew)) { try { Copy(rOld, rNew); linked++; } catch { } }
            if (linked > 0) Notes.Add("🔁 " + mode + " " + key + ": the history saved under the login " + login + " is linked (" + linked + " file" + (linked == 1 ? "" : "s") + ")");
            return linked;
        }
        static void Copy(string a, string b) { Directory.CreateDirectory(b); foreach (var f in Directory.GetFiles(a)) File.Copy(f, Path.Combine(b, Path.GetFileName(f)), false); foreach (var d in Directory.GetDirectories(a)) Copy(d, Path.Combine(b, Path.GetFileName(d))); }

        // the HEDGE banner after START / RESUME: "resumed: 39 iterations · today +$248.89 · open basket #40 taken over"
        public static string ResumeLine(IEnumerable<AutoIteration> its, AutoIteration current, DateTime today)
        {
            var done = its.Where(x => x.Done).ToList(); if (done.Count == 0 && current == null) return "";
            double t = done.Where(x => x.End.Date == today.Date).Sum(x => x.Result);
            return "🔁 resumed: " + done.Count + " iteration" + (done.Count == 1 ? "" : "s") + " · today " + (t >= 0 ? "+$" : "−$") + Math.Abs(t).ToString("N2", IC) + (current != null && !current.Done ? " · open basket #" + current.N + " taken over" : "");
        }
    }
}
