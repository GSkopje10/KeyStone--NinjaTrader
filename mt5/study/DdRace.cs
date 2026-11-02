// TRAILING vs END-OF-DAY drawdown, the user's race: two accounts, the same instrument, the same contracts, OPPOSITE directions, entered at the
// same minute. Both: max drawdown $2,000, goal +$2,000. TRAILING: the floor = the highest equity (open profit included) − $2,000, moved every
// tick. EOD: the floor = −$2,000 until the day's close, then the closing equity − $2,000 (never down). Who reaches +$2,000 more often?
// 1-minute MGC / MNQ futures (Jun 2020 → Oct 2026), New York time; each candle walked open → low/high → close (the high first on a red
// candle); commission $0.62 a contract a side + 1 tick of slippage on the entry.
using System; using System.Collections.Generic; using System.Linq; using System.Text; using System.Globalization; using System.Threading.Tasks;
public static class DdRace
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    public static readonly (string n, int m)[] Starts = { ("03:00 London", 180), ("08:00 NY open", 480), ("08:30 data", 510), ("09:30 stock open", 570), ("10:00", 600), ("13:00", 780), ("14:00", 840) };
    public static readonly string[] Rules = { "TR_LONG", "TR_SHORT", "TR_MOMENTUM", "TR_FADE", "TR_VWAP", "TR_ORB_BREAK" };
    public static string RuleName(string r) { return r == "TR_LONG" ? "Trailing LONG / EOD SHORT" : r == "TR_SHORT" ? "Trailing SHORT / EOD LONG" : r == "TR_MOMENTUM" ? "Trailing WITH the last 15 min" : r == "TR_FADE" ? "Trailing AGAINST the last 15 min" : r == "TR_VWAP" ? "Trailing on the VWAP side" : "Trailing with the 15-min range break"; }
    public static readonly int[] Qtys = { 5, 10, 15, 20, 30 };
    public static readonly string[] Modes = { "INTRADAY", "HOLD" };
    const double Dd = 2000, Goal = 2000, Comm = 0.62;
    public sealed class Out { public string Tr = "OPEN", Eod = "OPEN"; public int TrMin = -1, EodMin = -1; public int Year; }
    public sealed class Row { public string Inst, Start, Rule, Mode; public int StartMin, Qty; public List<Out> Runs = new List<Out>(); }

    // one race from minute index s (the entry at its open), TR direction d; returns the outcome
    static Out Race(Lab6.M1 m, int s, int d, int q, string mode, double pv, double tick)
    {
        var o = new Out { Year = m.T[s].Year }; double cost = Comm * 2 * q;
        double eTr = m.O[s] + d * tick, eEo = m.O[s] - d * tick;   // each pays one tick
        double peak = -cost, trFloor = -cost - Dd, eoFloor = -Dd - cost; bool trOn = true, eoOn = true; int day0 = m.Day[s], days = 0, prevMin = m.Min[s];
        for (int j = s; j < m.N && (trOn || eoOn); j++)
        {
            bool newDay = m.Day[j] != m.Day[j - (j > s ? 1 : 0)];
            if (mode == "INTRADAY" && (m.Day[j] != day0 || m.Min[j] >= 1015)) break;
            if (mode == "HOLD")
            {   // the day's close (17:00 NY): the EOD floor moves up from the closing equity; at most 10 trading days
                if ((prevMin < 1020 && m.Min[j] >= 1020) || (newDay && prevMin < 1020)) { double cl = m.C[j - 1]; double eq = -d * (cl - eEo) * pv * q - cost; if (eoOn) eoFloor = Math.Max(eoFloor, eq - Dd); days++; if (days >= 10) break; }
                prevMin = m.Min[j];
            }
            bool up = m.C[j] >= m.O[j]; double a = m.O[j], b = up ? m.L[j] : m.H[j], c = up ? m.H[j] : m.L[j], e = m.C[j];
            foreach (var p in new[] { a, b, c, e })
            {
                if (trOn)
                {
                    double eq = d * (p - eTr) * pv * q - cost; if (eq > peak) { peak = eq; trFloor = peak - Dd; }
                    if (eq >= Goal) { trOn = false; o.Tr = "PASS"; o.TrMin = j - s; } else if (eq <= trFloor) { trOn = false; o.Tr = "BLOWN"; o.TrMin = j - s; }
                }
                if (eoOn)
                {
                    double eq = -d * (p - eEo) * pv * q - cost;
                    if (eq >= Goal) { eoOn = false; o.Eod = "PASS"; o.EodMin = j - s; } else if (eq <= eoFloor) { eoOn = false; o.Eod = "BLOWN"; o.EodMin = j - s; }
                }
            }
        }
        return o;
    }

    public static string Run(Dictionary<string, Lab6.M1> data)
    {
        var rows = new List<Row>();
        foreach (var kv in data) foreach (var st in Starts) foreach (var r in Rules) foreach (var q in Qtys) foreach (var md in Modes) rows.Add(new Row { Inst = kv.Key, Start = st.n, StartMin = st.m, Rule = r, Qty = q, Mode = md });
        Parallel.ForEach(rows, row =>
        {
            var m = data[row.Inst]; double pv = row.Inst == "MGC" ? 10 : 2, tick = row.Inst == "MGC" ? 0.1 : 0.25;
            // the first minute of each trading day at the start time (weekdays)
            int lastDay = -1; double pv2 = 0, vv = 0; int vwDay = int.MinValue;
            var vwap = new double[m.N]; for (int i = 0; i < m.N; i++) { int key = m.Min[i] >= 1080 ? m.Day[i] + 1 : m.Day[i]; if (key != vwDay) { vwDay = key; pv2 = 0; vv = 0; } double w = Math.Max(1, m.V[i]); pv2 += (m.H[i] + m.L[i] + m.C[i]) / 3 * w; vv += w; vwap[i] = pv2 / vv; }
            for (int i = 16; i < m.N; i++)
            {
                if (m.Min[i] != row.StartMin || m.Day[i] == lastDay || m.T[i].DayOfWeek == DayOfWeek.Saturday || m.T[i].DayOfWeek == DayOfWeek.Sunday) continue; lastDay = m.Day[i];
                int s = i, d;
                if (row.Rule == "TR_LONG") d = 1; else if (row.Rule == "TR_SHORT") d = -1;
                else if (row.Rule == "TR_MOMENTUM" || row.Rule == "TR_FADE") { if (m.Day[i - 15] != m.Day[i] && m.Min[i] > 15) continue; double mv = m.C[i - 1] - m.O[i - 15]; if (mv == 0) continue; d = Math.Sign(mv) * (row.Rule == "TR_MOMENTUM" ? 1 : -1); }
                else if (row.Rule == "TR_VWAP") { double pc = m.C[i - 1]; if (pc == vwap[i - 1]) continue; d = pc > vwap[i - 1] ? 1 : -1; }
                else
                {   // the range of the first 15 minutes from the start time, then the first close outside it within 2 hours
                    double hi = double.MinValue, lo = double.MaxValue; int k = i; for (; k < m.N && m.Day[k] == m.Day[i] && m.Min[k] < row.StartMin + 15; k++) { hi = Math.Max(hi, m.H[k]); lo = Math.Min(lo, m.L[k]); }
                    d = 0; for (; k < m.N && m.Day[k] == m.Day[i] && m.Min[k] < row.StartMin + 135; k++) { if (m.C[k] > hi) { d = 1; break; } if (m.C[k] < lo) { d = -1; break; } }
                    if (d == 0 || k + 1 >= m.N || m.Day[k + 1] != m.Day[i]) continue; s = k + 1;
                }
                var o = Race(m, s, d, row.Qty, row.Mode, pv, tick); lock (row.Runs) row.Runs.Add(o);
            }
        });
        // the JSON for the report: per setting the counts and the minutes; per year the passes
        var sb = new StringBuilder("{\"generated\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + "\",\"rows\":[");
        bool first = true;
        foreach (var r in rows)
        {
            var R = r.Runs; int n = R.Count; if (n == 0) continue;
            Func<IEnumerable<int>, string> med = xs => { var l = xs.Where(x => x >= 0).OrderBy(x => x).ToList(); return l.Count == 0 ? "null" : l[l.Count / 2].ToString(IC); };
            var years = R.GroupBy(x => x.Year).OrderBy(g => g.Key).Select(g => "\"" + g.Key + "\":[" + g.Count() + "," + g.Count(x => x.Tr == "PASS") + "," + g.Count(x => x.Eod == "PASS") + "]");
            sb.Append((first ? "" : ",") + "{\"i\":\"" + r.Inst + "\",\"s\":\"" + r.Start + "\",\"r\":\"" + r.Rule + "\",\"q\":" + r.Qty + ",\"m\":\"" + r.Mode + "\",\"n\":" + n
                + ",\"tp\":" + R.Count(x => x.Tr == "PASS") + ",\"tb\":" + R.Count(x => x.Tr == "BLOWN") + ",\"ep\":" + R.Count(x => x.Eod == "PASS") + ",\"eb\":" + R.Count(x => x.Eod == "BLOWN")
                + ",\"both_open\":" + R.Count(x => x.Tr == "OPEN" && x.Eod == "OPEN") + ",\"both_blown\":" + R.Count(x => x.Tr == "BLOWN" && x.Eod == "BLOWN")
                + ",\"tpm\":" + med(R.Where(x => x.Tr == "PASS").Select(x => x.TrMin)) + ",\"epm\":" + med(R.Where(x => x.Eod == "PASS").Select(x => x.EodMin)) + ",\"tbm\":" + med(R.Where(x => x.Tr == "BLOWN").Select(x => x.TrMin))
                + ",\"tb_ep\":" + R.Count(x => x.Tr == "BLOWN" && x.Eod == "PASS") + ",\"tb_eb\":" + R.Count(x => x.Tr == "BLOWN" && x.Eod == "BLOWN") + ",\"tb_eo\":" + R.Count(x => x.Tr == "BLOWN" && x.Eod == "OPEN")
                + ",\"tp_eb\":" + R.Count(x => x.Tr == "PASS" && x.Eod == "BLOWN") + ",\"tp_eo\":" + R.Count(x => x.Tr == "PASS" && x.Eod == "OPEN") + ",\"to_eb\":" + R.Count(x => x.Tr == "OPEN" && x.Eod == "BLOWN") + ",\"to_ep\":" + R.Count(x => x.Tr == "OPEN" && x.Eod == "PASS")
                + ",\"same\":" + R.Count(x => x.Tr == "BLOWN" && x.Eod == "PASS" && x.EodMin == x.TrMin) + ",\"after\":" + med(R.Where(x => x.Tr == "BLOWN" && x.Eod == "PASS").Select(x => x.EodMin - x.TrMin))
                + ",\"y\":{" + string.Join(",", years) + "}}");
            first = false;
        }
        sb.Append("]}");
        foreach (var r in rows.Where(x => x.Qty == 15 && x.Mode == "INTRADAY" && x.Runs.Count > 0).OrderBy(x => x.Inst).ThenBy(x => x.StartMin))
            if (r.Rule == "TR_MOMENTUM" || r.Rule == "TR_LONG") Console.WriteLine(string.Format(IC, "{0} {1,-18} {2,-14} n {3,4}  TRAILING pass {4,5:0.0}% blown {5,5:0.0}%  |  EOD pass {6,5:0.0}% blown {7,5:0.0}%  | both open {8,5:0.0}%", r.Inst, r.Start, r.Rule, r.Runs.Count, 100.0 * r.Runs.Count(x => x.Tr == "PASS") / r.Runs.Count, 100.0 * r.Runs.Count(x => x.Tr == "BLOWN") / r.Runs.Count, 100.0 * r.Runs.Count(x => x.Eod == "PASS") / r.Runs.Count, 100.0 * r.Runs.Count(x => x.Eod == "BLOWN") / r.Runs.Count, 100.0 * r.Runs.Count(x => x.Tr == "OPEN" && x.Eod == "OPEN") / r.Runs.Count));
        return sb.ToString();
    }
}
