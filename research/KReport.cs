// KEYSTONE REPORT (.kreport.json): one portable file per study — summary, tables and every labelled day (markers + trades)
// that the studio's REPORTS window imports and draws on the chart.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public sealed class KReport
{
    public string Title = "", Subtitle = "", Instrument = "MNQ", Method = "";
    public List<string[]> Summary = new List<string[]>();                 // label, value, note
    public List<KSection> Sections = new List<KSection>();
    public List<KDay> Days = new List<KDay>();

    public sealed class KSection { public string Title = "", Note = "", Chart = ""; public List<string> Columns = new List<string>(); public List<List<string>> Rows = new List<List<string>>(); }
    public sealed class KMarker { public string Time = "", Text = "", Kind = "info"; public double Price; }
    public sealed class KTrade { public int Dir; public string EntryTime = "", ExitTime = "", Why = ""; public double Entry, Stop = double.NaN, Target = double.NaN, Exit, Pnl; }
    public sealed class KDay { public DateTime Date; public string Label = "", Color = "", Note = ""; public Dictionary<string, string> Fields = new Dictionary<string, string>(); public List<KMarker> Markers = new List<KMarker>(); public List<KTrade> Trades = new List<KTrade>(); }

    static string Q(string s) { var b = new StringBuilder("\""); foreach (char c in s ?? "") { if (c == '"' || c == '\\') b.Append('\\').Append(c); else if (c == '\n') b.Append("\\n"); else if (c < 32) b.Append(' '); else b.Append(c); } return b.Append('"').ToString(); }
    static string N(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("0.####", CultureInfo.InvariantCulture); }

    public string Json()
    {
        var b = new StringBuilder();
        b.Append("{\"format\":\"keystone-report-1\",\"title\":").Append(Q(Title)).Append(",\"subtitle\":").Append(Q(Subtitle)).Append(",\"instrument\":").Append(Q(Instrument))
         .Append(",\"method\":").Append(Q(Method)).Append(",\"generated\":").Append(Q(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)));
        b.Append(",\"summary\":[").Append(string.Join(",", Summary.Select(s => "{\"label\":" + Q(s[0]) + ",\"value\":" + Q(s[1]) + ",\"note\":" + Q(s.Length > 2 ? s[2] : "") + "}"))).Append("]");
        b.Append(",\"sections\":[").Append(string.Join(",", Sections.Select(s => "{\"title\":" + Q(s.Title) + ",\"note\":" + Q(s.Note) + ",\"chart\":" + Q(s.Chart) + ",\"columns\":[" + string.Join(",", s.Columns.Select(Q)) + "],\"rows\":[" + string.Join(",", s.Rows.Select(r => "[" + string.Join(",", r.Select(Q)) + "]")) + "]}"))).Append("]");
        b.Append(",\"days\":[").Append(string.Join(",", Days.Select(d => "{\"date\":" + Q(d.Date.ToString("yyyy-MM-dd")) + ",\"label\":" + Q(d.Label) + ",\"color\":" + Q(d.Color) + ",\"note\":" + Q(d.Note)
            + ",\"fields\":{" + string.Join(",", d.Fields.Select(kv => Q(kv.Key) + ":" + Q(kv.Value))) + "}"
            + ",\"markers\":[" + string.Join(",", d.Markers.Select(m => "{\"time\":" + Q(m.Time) + ",\"price\":" + N(m.Price) + ",\"text\":" + Q(m.Text) + ",\"kind\":" + Q(m.Kind) + "}")) + "]"
            + ",\"trades\":[" + string.Join(",", d.Trades.Select(t => "{\"dir\":" + t.Dir + ",\"entryTime\":" + Q(t.EntryTime) + ",\"entry\":" + N(t.Entry) + ",\"stop\":" + N(t.Stop) + ",\"target\":" + N(t.Target) + ",\"exitTime\":" + Q(t.ExitTime) + ",\"exit\":" + N(t.Exit) + ",\"pnl\":" + N(t.Pnl) + ",\"why\":" + Q(t.Why) + "}")) + "]}"))).Append("]}");
        return b.ToString();
    }
}
