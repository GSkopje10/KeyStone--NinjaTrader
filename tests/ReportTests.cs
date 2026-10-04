// KEYSTONE REPORT FILES: the .kreport.json reader (the REPORTS window imports these).
using System;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class ReportTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }
    public static int Main()
    {
        string json = "{\"format\":\"keystone-report-1\",\"title\":\"The Opening Minute\",\"subtitle\":\"MNQ \\u2022 test\",\"instrument\":\"mnq\",\"summary\":[{\"label\":\"Days\",\"value\":\"2\",\"note\":\"\"}],"
            + "\"sections\":[{\"title\":\"T1\",\"note\":\"n\",\"columns\":[\"Year\",\"Bull %\"],\"rows\":[[\"2024\",\"37%\"],[\"2025\",\"-1.5\"]]}],"
            + "\"days\":[{\"date\":\"2025-04-30\",\"label\":\"BEAR 40 pts → STRAIGHT UP\",\"color\":\"green\",\"note\":\"a \\\"quote\\\"\",\"fields\":{\"first\":\"BEAR\"},\"markers\":[{\"time\":\"09:30\",\"price\":19301.5,\"text\":\"1ST BEAR\",\"kind\":\"sell\"}],\"trades\":[{\"dir\":-1,\"entryTime\":\"09:31\",\"entry\":19295.25,\"stop\":null,\"target\":null,\"exitTime\":\"09:36\",\"exit\":19280,\"pnl\":28.5,\"why\":\"w\"}]},"
            + "{\"date\":\"2024-01-02\",\"label\":\"BULL\",\"markers\":[],\"trades\":[]}]}";
        var r = KeystoneReport.Parse(json);
        Check(r.Title == "The Opening Minute" && r.Subtitle == "MNQ • test" && r.Instrument == "MNQ", "title, subtitle (\\u escape), instrument upper-cased");
        Check(r.Summary.Count == 1 && r.Summary[0][1] == "2", "summary cards");
        Check(r.Sections.Count == 1 && r.Sections[0].Rows.Count == 2 && r.Sections[0].Rows[1][1] == "-1.5", "tables");
        Check(r.Days.Count == 2 && r.Days[0].Date == new DateTime(2024, 1, 2), "days sorted by date");
        var d = r.Days[1];
        Check(d.Note == "a \"quote\"" && d.Fields["first"] == "BEAR", "note with quotes, fields");
        Check(d.Markers.Count == 1 && d.Markers[0].Time == new TimeSpan(9, 30, 0) && d.Markers[0].Price == 19301.5 && d.Markers[0].Kind == "sell", "markers (time = candle start)");
        Check(d.Trades.Count == 1 && d.Trades[0].Dir == -1 && double.IsNaN(d.Trades[0].Stop) && d.Trades[0].ExitTime == new TimeSpan(9, 36, 0) && d.Trades[0].Pnl == 28.5, "trades (null → no stop)");
        bool threw = false; try { KeystoneReport.Parse("{\"format\":\"other\"}"); } catch (FormatException) { threw = true; }
        Check(threw, "a file that is not a Keystone report is refused");
        Console.WriteLine(failures == 0 ? "ALL REPORT FILE TESTS PASSED" : failures + " REPORT FILE TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
