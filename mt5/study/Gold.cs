// the repo's 1-minute gold futures candles (data/MGC, the studio's month files, New York time) as a stand-in for MT5's XAUUSD:
// times moved to the MT5 server clock (New York + 7 h), prices as the BID
using System; using System.Collections.Generic; using System.IO; using System.IO.Compression; using KeystoneMT5;
public static class Gold
{
    public static List<Mt5Bar> Load(string folder, DateTime from, DateTime to)
    {
        var res = new List<Mt5Bar>();
        foreach (var f in Directory.GetFiles(folder, "*.bars.gz"))
        {
            DateTime m; if (!DateTime.TryParseExact(Path.GetFileName(f).Substring(0, 7), "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out m)) continue;
            if (m.AddMonths(1) < from.AddDays(-3) || m > to) continue;
            var ms = new MemoryStream(); using (var gz = new GZipStream(File.OpenRead(f), CompressionMode.Decompress)) gz.CopyTo(ms); ms.Position = 0;
            using (var r = new BinaryReader(ms))
            {
                if (!r.ReadString().StartsWith("studio1")) continue; int days = r.ReadInt32(); for (int i = 0; i < days; i++) { r.ReadInt64(); r.ReadInt32(); }
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    var t = new DateTime(r.ReadInt64()); double o = r.ReadDouble(), h = r.ReadDouble(), l = r.ReadDouble(), c = r.ReadDouble(); long v = r.ReadInt64();
                    // the studio stamps a candle with its CLOSING minute; MT5 with its opening minute
                    var st = t.AddMinutes(-1).AddHours(7); if (st < from.AddDays(-3) || st >= to.AddDays(1)) continue;
                    res.Add(new Mt5Bar { Time = st, O = o, H = h, L = l, C = c, V = v });
                }
            }
        }
        res.Sort((a, b) => a.Time.CompareTo(b.Time));
        var dedup = new List<Mt5Bar>(res.Count); foreach (var b in res) if (dedup.Count == 0 || b.Time > dedup[dedup.Count - 1].Time) dedup.Add(b);
        return dedup;
    }
}
