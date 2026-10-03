// HIGH-IMPACT NEWS: payroll dates by rule (checked against real release days), FOMC days, News.csv, ForexFactory feed, combine.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class NewsTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    public static int Main()
    {
        // real release days: 2024-10-04, 2024-11-01, 2021-07-02, 2022-01-07, 2023-07-07, 2023-09-01, 2021-01-08, 2025-07-03 (Thu), 2020-07-02 (Thu)
        var real = new[] { "2024-10-04", "2024-11-01", "2021-07-02", "2022-01-07", "2023-07-07", "2023-09-01", "2021-01-08", "2025-07-03", "2020-07-02" };
        foreach (var r in real)
        {
            var d = DateTime.Parse(r); var got = KeystoneNews.NfpDate(d.Year, d.Month);
            Check(got == d, "payrolls " + r + " by rule", got.ToString("yyyy-MM-dd"));
        }
        var b = KeystoneNews.BuiltIn(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
        Check(b.Any(e => e.Time == new DateTime(2026, 9, 16, 14, 0, 0) && e.Title.StartsWith("FOMC")) && b.Any(e => e.Time == new DateTime(2026, 9, 16, 14, 30, 0)), "FOMC 2026-09-16 14:00 + press conference 14:30");
        var csv = KeystoneNews.ParseCsv(new[] { "# notes", "2026-09-04,08:30,USD,HIGH,Non-Farm Employment Change", "2026-09-11,8:30,usd,high,CPI m/m", "bad line" });
        Check(csv.Count == 2 && csv[1].Time == new DateTime(2026, 9, 11, 8, 30, 0) && csv[1].Impact == "HIGH", "News.csv lines read (bad lines skipped)");
        var all = KeystoneNews.Combine(KeystoneNews.BuiltIn(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)), csv);
        Check(!all.Any(e => e.Source == "RULE" && e.Time.Month == 9) && all.Count(e => e.Title.Contains("Non-Farm")) == 1, "a payrolls line in the file replaces the rule date of that month");
        var day = KeystoneNews.ForSession(all, new DateTime(2026, 9, 11), true);
        Check(day.Count == 1 && day[0].Title == "CPI m/m", "the session's high-impact USD news");
        string json = "[{\"title\":\"CPI m\\/m\",\"country\":\"USD\",\"date\":\"2026-10-14T08:30:00-04:00\",\"impact\":\"High\",\"forecast\":\"0.3%\"},{\"title\":\"German ZEW\",\"country\":\"EUR\",\"date\":\"2026-10-14T05:00:00-04:00\",\"impact\":\"High\"}]";
        var ff = KeystoneNews.ParseForexFactory(json, o => o.DateTime);
        Check(ff.Count == 2 && ff[0].Title == "CPI m/m" && ff[0].Time == new DateTime(2026, 10, 14, 8, 30, 0) && ff[0].Impact == "HIGH", "ForexFactory feed parsed");
        Check(KeystoneNews.ForSession(ff, new DateTime(2026, 10, 14), true).Count == 1, "only USD news counts for MNQ / MGC");
        Check(KeystoneNews.ParseCsv(new[] { KeystoneNews.ToCsv(ff[0]) })[0].Time == ff[0].Time, "write → read the file keeps the time");
        Console.WriteLine(failures == 0 ? "ALL NEWS TESTS PASSED" : failures + " NEWS TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
