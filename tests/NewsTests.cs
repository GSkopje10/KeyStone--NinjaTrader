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
        string html = "<li><a href=\"/news.release/archives/cpi_09112025.htm\">Consumer Price Index for August 2025</a> (<a href=\"/news.release/archives/cpi_09112025.pdf\">PDF</a>)</li><li><a href=\"/news.release/archives/cpi_08122025.htm\">July 2025</a></li><a href=\"/news.release/archives/cpi_08122025.htm\">dup</a>";
        var bls = KeystoneNews.ParseBlsArchive(html, "cpi", "CPI (BLS)");
        Check(bls.Count == 2 && bls[1].Time == new DateTime(2025, 9, 11, 8, 30, 0) && KeystoneNews.Tag(bls[0]) == "CPI", "BLS archive: release days from the file names, 08:30, no duplicates", bls.Count.ToString());
        var emp = KeystoneNews.ParseBlsArchive("<a href=\"/news.release/archives/empsit_11202025.htm\">x</a>", "empsit", "NON-FARM PAYROLLS (BLS)");
        var comb = KeystoneNews.Combine(KeystoneNews.BuiltIn(new DateTime(2025, 11, 1), new DateTime(2025, 11, 30)), emp);
        Check(comb.Count(e => KeystoneNews.Tag(e) == "NFP") == 1 && comb.Any(e => e.Time == new DateTime(2025, 11, 20, 8, 30, 0)), "the real BLS payrolls day replaces the rule date of that month (Nov 2025 shutdown delay)");
        // 📰 the NEWS tab: forecast / previous from ForexFactory, the calendar cache line, the countdown, the RSS headlines
        {
            var ffN = KeystoneNews.ParseForexFactory("[{\"title\":\"CPI m/m\",\"country\":\"USD\",\"date\":\"2026-10-29T08:30:00-04:00\",\"impact\":\"High\",\"forecast\":\"0.3%\",\"previous\":\"0.4%\"}]", o => o.DateTime);
            Check(ffN.Count == 1 && ffN[0].Forecast == "0.3%" && ffN[0].Previous == "0.4%" && ffN[0].Impact == "HIGH", "ForexFactory: forecast and previous are kept");
            var back = KeystoneNews.FromCalLine(KeystoneNews.ToCalLine(ffN[0]));
            Check(back != null && back.Time == ffN[0].Time && back.Title == "CPI m/m" && back.Forecast == "0.3%" && back.Previous == "0.4%" && back.Currency == "USD", "the calendar cache line reads back the same event");
            var now0 = new DateTime(2026, 10, 28, 12, 0, 0);
            Check(KeystoneNews.Until(now0.AddMinutes(125), now0) == "in 2 h 05 min" && KeystoneNews.Until(now0.AddHours(27), now0) == "in 1 d 3 h" && KeystoneNews.Until(now0.AddSeconds(20), now0) == "NOW" && KeystoneNews.Until(now0.AddMinutes(-4), now0) == "now (4 min ago)", "the countdown text");
            var rss = "<rss><channel><item><title><![CDATA[Fed's Powell: rates stay]]></title><link>https://x.test/a</link><pubDate>Wed, 28 Oct 2026 15:00:00 GMT</pubDate></item><item><title>Gold &amp; dollar</title><link>https://x.test/b</link><pubDate>Wed, 28 Oct 2026 16:00:00 GMT</pubDate></item></channel></rss>";
            var hl = KeystoneNews.ParseRss(rss, "TEST");
            Check(hl.Count == 2 && hl[0].Item2 == "Gold & dollar" && hl[1].Item2 == "Fed's Powell: rates stay" && hl[1].Item3 == "https://x.test/a" && hl[0].Item1 == new DateTime(2026, 10, 28, 16, 0, 0), "RSS headlines: CDATA + entities, newest first", string.Join(" | ", hl.Select(x => x.Item2)));
        }
        LiveMathChecks.Run((ok, name, detail) => Check(ok, name, detail));
        Console.WriteLine(failures == 0 ? "ALL NEWS TESTS PASSED" : failures + " NEWS TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}

public static class LiveMathChecks
{
    // run from NewsTests.Main
    public static int Run(Action<bool, string, string> check)
    {
        check(KeystoneLiveMath.Room(50000, 50000, 50000, 2500, true) == 2500, "a fresh 50K with $2,500 drawdown has $2,500 room", "");
        check(KeystoneLiveMath.Room(51000, 51500, 50000, 2500, false) == 2000, "trailing: peak 51,500 → floor 49,000 → room at 51,000 = 2,000", KeystoneLiveMath.Room(51000, 51500, 50000, 2500, false).ToString());
        check(KeystoneLiveMath.Room(53000, 54000, 50000, 2500, true) == 3000, "stops at start: the floor stays at 50,000 once reached", KeystoneLiveMath.Room(53000, 54000, 50000, 2500, true).ToString());
        check(Math.Abs(KeystoneLiveMath.Progress(51500, 50000, 3000) - 0.5) < 1e-9 && KeystoneLiveMath.Progress(49000, 50000, 3000) == 0, "target progress 1,500 of 3,000 = 50%", "");
        return 0;
    }
}
