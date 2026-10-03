// STUDIO DATA STORE: whole-session merges (no mixed contracts), thin data never overwrites a full day, session list from
// headers, loading a range, missing weekdays, weekly download chunks, best source per session.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class StudioStoreTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    // one session: from 18:00 the evening before to 16:59, n minutes, price level p
    static List<KeystoneArcBar> Session(DateTime day, int n, double p)
    {
        var start = day.AddDays(-1).AddHours(18); var list = new List<KeystoneArcBar>();
        for (int i = 0; i < n; i++) list.Add(new KeystoneArcBar { Symbol = "MNQ", Time = start.AddMinutes(i + 1), Open = p, High = p + 1, Low = p - 1, Close = p + 0.25, Volume = 1 });
        return list;
    }

    public static int Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ks_studio_" + Guid.NewGuid().ToString("N"));
        try
        {
            var mon = new DateTime(2026, 9, 28); var tue = mon.AddDays(1); var oct1 = new DateTime(2026, 10, 1);
            int ch = KeystoneStudioStore.Merge(folder, "MNQ", Session(mon, 1380, 100).Concat(Session(tue, 1380, 100)).Concat(Session(oct1, 1380, 100)));
            var s = KeystoneStudioStore.Sessions(folder, "MNQ");
            Check(ch == 3 && s.Count == 3 && s[mon] == 1380 && s[oct1] == 1380, "three sessions stored, minute counts in the header", ch + " / " + s.Count);
            Check(File.Exists(Path.Combine(folder, "MNQ", "2026-10.bars")) && File.Exists(Path.Combine(folder, "MNQ", "2026-09.bars")), "the Oct 1 session (starting Sep 30 18:00) lives in the October file");
            var oct = KeystoneStudioStore.Load(folder, "MNQ", oct1, oct1);
            Check(oct.Count == 1380 && oct[0].Time == new DateTime(2026, 9, 30, 18, 1, 0), "load one session = all its minutes, from the evening before", oct.Count + " " + (oct.Count > 0 ? oct[0].Time.ToString() : ""));
            ch = KeystoneStudioStore.Merge(folder, "MNQ", Session(tue, 300, 500));
            Check(ch == 0 && KeystoneStudioStore.Load(folder, "MNQ", tue, tue).All(b => b.Open == 100), "a thinner copy (another contract) never replaces a full day");
            ch = KeystoneStudioStore.Merge(folder, "MNQ", Session(tue, 1380, 200));
            var t2 = KeystoneStudioStore.Load(folder, "MNQ", tue, tue);
            Check(ch == 1 && t2.Count == 1380 && t2.All(b => b.Open == 200), "a full new copy replaces the WHOLE day — never a mix of two contracts", ch + " " + t2.Select(b => b.Open).Distinct().Count());
            ch = KeystoneStudioStore.Merge(folder, "MNQ", Session(tue, 1380, 200));
            Check(ch == 0, "the same data again changes nothing");
            var range = KeystoneStudioStore.Load(folder, "MNQ", mon, oct1);
            Check(range.Count == 3 * 1380 && range.Zip(range.Skip(1), (a, b) => a.Time < b.Time).All(x => x), "a range across two month files loads in time order");
            var missing = KeystoneStudioStore.Missing(s, mon, new DateTime(2026, 10, 2), 60);
            Check(missing.Count == 2 && missing[0] == new DateTime(2026, 9, 30) && missing[1] == new DateTime(2026, 10, 2), "missing weekdays: Sep 30 and Oct 2", string.Join(",", missing.Select(d => d.ToString("MM-dd"))));
            var weeks = KeystoneStudioStore.Weeks(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
            Check(weeks.Count == 5 && weeks[0].Item1 == new DateTime(2026, 8, 31) && weeks[1].Item1 == new DateTime(2026, 9, 6) && weeks.Last().Item2 == new DateTime(2026, 9, 30, 23, 59, 0), "weekly chunks Sunday → Saturday, clipped to the range (the first starts the evening before)", string.Join(" | ", weeks.Select(w => w.Item1.ToString("MM-dd HH:mm") + "→" + w.Item2.ToString("MM-dd HH:mm"))));
            Check(KeystoneStudioStore.WeekKey(new DateTime(2026, 9, 9, 15, 0, 0)) == new DateTime(2026, 9, 6), "week key = its Sunday");
            KeystoneStudioStore.MarkRequested(folder, "MNQ", new DateTime(2026, 9, 6));
            Check(KeystoneStudioStore.Requested(folder, "MNQ").Contains(new DateTime(2026, 9, 6)), "finished weeks are remembered");
            var best = KeystoneStudioStore.BestPerSession(new[] { Session(mon, 400, 1), Session(mon, 1300, 2).Concat(Session(tue, 10, 2)).ToList(), Session(tue, 900, 3) });
            Check(best.Count == 1300 + 900 && best.Where(b => KeystoneStudioStore.SessionOf(b.Time) == mon).All(b => b.Open == 2) && best.Where(b => KeystoneStudioStore.SessionOf(b.Time) == tue).All(b => b.Open == 3), "several old caches: each session comes from the source with the most minutes");
            Check(KeystoneStudioStore.Sessions(folder, "MGC").Count == 0 && KeystoneStudioStore.Load(folder, "MGC", mon, tue).Count == 0, "an instrument with nothing stored is empty, not an error");
        }
        finally { try { Directory.Delete(folder, true); } catch { } }
        Console.WriteLine(failures == 0 ? "STUDIO STORE TESTS PASSED" : failures + " STUDIO STORE TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
