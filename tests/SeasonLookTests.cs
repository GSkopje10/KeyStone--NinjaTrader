// 🍂 BUILD 2026-11-13k — SEASONS + SCENES (display only): the season of a NY date (northern hemisphere, the boundaries),
// the quote rotation (deterministic, every 3 h), the reminder priority, the scene / particle bounds, the look settings,
// MY PICTURE / MY VIDEO file checks. Optional: `mono season.exe --svg <dir>` writes the HTML/SVG preview of the 4 seasons × scenes
// from the SAME KeystoneScene layers the WPF view draws.
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text;
using NinjaTrader.NinjaScript;
public static class SeasonLookTests
{
    static int fails = 0, n = 0;
    static void Ok(bool c, string what) { n++; if (!c) { fails++; Console.WriteLine("FAIL  " + what); } }
    static DateTime D(int y, int m, int d, int h = 12, int mi = 0, int s = 0) { return new DateTime(y, m, d, h, mi, s); }
    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        if (args.Length >= 2 && args[0] == "--svg") { Preview(args[1]); if (args.Length >= 3) Gallery(args[1], args[2]); return 0; }
        // 1. the season of a NY date (northern hemisphere, astronomical starts)
        Ok(KeystoneSeasons.Of(D(2026, 11, 13)) == "FALL", "Nov 13 = FALL");
        Ok(KeystoneSeasons.Of(D(2026, 3, 19)) == "WINTER" && KeystoneSeasons.Of(D(2026, 3, 20)) == "SPRING", "Mar 19 winter / Mar 20 spring");
        Ok(KeystoneSeasons.Of(D(2026, 6, 20)) == "SPRING" && KeystoneSeasons.Of(D(2026, 6, 21)) == "SUMMER", "Jun 20 spring / Jun 21 summer");
        Ok(KeystoneSeasons.Of(D(2026, 9, 21)) == "SUMMER" && KeystoneSeasons.Of(D(2026, 9, 22)) == "FALL", "Sep 21 summer / Sep 22 fall");
        Ok(KeystoneSeasons.Of(D(2026, 12, 20)) == "FALL" && KeystoneSeasons.Of(D(2026, 12, 21)) == "WINTER", "Dec 20 fall / Dec 21 winter");
        Ok(KeystoneSeasons.Of(D(2027, 1, 1)) == "WINTER" && KeystoneSeasons.Of(D(2028, 2, 29)) == "WINTER", "Jan 1 / Feb 29 winter");
        Ok(KeystoneSeasons.Of(D(2026, 3, 20, 0, 0)) == "SPRING" && KeystoneSeasons.Of(D(2026, 3, 19, 23, 59)) == "WINTER", "the NY calendar date decides (midnight)");
        // the NY date, not UTC: 2026-12-21 02:00 UTC is still Dec 20 in New York → FALL
        { DateTime utc = new DateTime(2026, 12, 21, 2, 0, 0, DateTimeKind.Utc); DateTime ny; try { ny = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("America/New_York")); } catch { ny = utc.AddHours(-5); } Ok(KeystoneSeasons.Of(ny) == "FALL", "UTC Dec 21 02:00 = NY Dec 20 = FALL"); }
        foreach (var s in KeystoneSeasons.Names) { var p = KeystoneSeasons.Palette(s); Ok(p.Season == s && p.Accent != null && p.Deep != null && p.Ink != null, "palette " + s); }
        Ok(KeystoneSeasons.Palette("WINTER").Frost && !KeystoneSeasons.Palette("SUMMER").Frost, "frost only in winter");
        Ok(KeystoneSeasons.Mix("#000000", "#FFFFFF", 0.5) == "#FF808080" && KeystoneSeasons.Alpha("#FF0000", 0.5) == "#80FF0000", "colour helpers");
        // 2. quotes: ~20 a season, deterministic, a new one every 3 h
        foreach (var s in KeystoneSeasons.Names) { var l = KeystoneSeasonQuotes.For(s); Ok(l.Length >= 20 && l.Distinct().Count() == l.Length && l.All(q => q.Length <= 90), "20 distinct short quotes " + s); }
        var t0 = D(2026, 11, 13, 9, 5); Ok(KeystoneSeasonQuotes.Quote("FALL", t0) == KeystoneSeasonQuotes.Quote("FALL", D(2026, 11, 13, 11, 59)), "same quote inside the 3-h slot");
        Ok(KeystoneSeasonQuotes.Quote("FALL", t0) != KeystoneSeasonQuotes.Quote("FALL", D(2026, 11, 13, 12, 0)), "a new quote at the next slot");
        { var seen = new HashSet<string>(); for (int h = 0; h < 24 * 3; h += 3) seen.Add(KeystoneSeasonQuotes.Quote("WINTER", D(2027, 1, 5, 0).AddHours(h))); Ok(seen.Count == 20, "every winter quote within 3 days (" + seen.Count + ")"); }
        // 3. the header line: urgent reminders first, never more than one line
        var I = new KeystoneLookInputs { Live = true, HasAccount = true, Dd = 2000, Room = 1500, Today = 0, DayCap = 1000, DayLoss = 600, Ny = D(2026, 11, 13, 10, 0, 5) };
        var line = KeystoneSeasonQuotes.Line("BOTH", "FALL", I, false); Ok(line.Item2 == "QUOTE" && line.Item1.StartsWith("🍂 "), "calm: the quote (" + line.Item1 + ")");
        Ok(KeystoneSeasonQuotes.Line("BOTH", "FALL", new KeystoneLookInputs { Live = true, HasAccount = true, Dd = 2000, Room = 1500, Ny = D(2026, 11, 13, 10, 0, 45) }, false).Item1.StartsWith("🛡"), "every 3rd 20-s slot: a gentle reminder");
        I.Room = 250; var lf = KeystoneSeasonQuotes.Line("BOTH", "FALL", I, false); Ok(lf.Item1.StartsWith("⛔") && lf.Item2 == "WARN", "floor close beats the quote");
        I.Locked = true; Ok(KeystoneSeasonQuotes.Line("BOTH", "FALL", I, false).Item1.StartsWith("🔒"), "a lock beats everything");
        Ok(KeystoneSeasonQuotes.Reminders(I).Select(x => x.Pri).SequenceEqual(KeystoneSeasonQuotes.Reminders(I).Select(x => x.Pri).OrderBy(x => x)), "ordered by priority");
        Ok(KeystoneSeasonQuotes.Line("QUOTES", "FALL", I, false).Item2 == "QUOTE", "QUOTES ONLY ignores reminders");
        Ok(KeystoneSeasonQuotes.Line("REMINDERS", "FALL", I, false).Item1.StartsWith("🔒"), "REMINDERS keeps the lock");
        Ok(KeystoneSeasonQuotes.Line("OFF", "FALL", I, false).Item1 == "", "OFF = nothing");
        var calm = new KeystoneLookInputs { Live = true, Ny = D(2026, 11, 13, 10, 0, 5) };
        Ok(KeystoneSeasonQuotes.Line("BOTH", "FALL", calm, true).Item1 == "", "✕ hidden today + nothing to remind = empty");
        Ok(KeystoneSeasonQuotes.Line("REMINDERS", "FALL", calm, false).Item1 == "", "REMINDERS with nothing to say = empty");
        var close = new KeystoneLookInputs { Live = true, Ny = D(2026, 11, 13, 16, 40) }; var lc = KeystoneSeasonQuotes.Line("BOTH", "FALL", close, true);
        Ok(lc.Item1.StartsWith("⏰ 16:54 NY in 14 min") && lc.Item2 == "CLOSE", "the 16:54 close shows even with quotes hidden (" + lc.Item1 + ")");
        Ok(KeystoneSeasonQuotes.Reminders(new KeystoneLookInputs { Live = true, Ny = D(2026, 11, 13, 16, 23) }).All(x => x.Key != "CLOSE"), "16:23 = not yet");
        Ok(KeystoneSeasonQuotes.Reminders(new KeystoneLookInputs { Live = true, Ny = D(2026, 11, 14, 11, 0) }).Any(x => x.Key == "WEEKEND"), "Saturday = weekend note");
        var loss = new KeystoneLookInputs { Live = true, HasAccount = true, Today = -460, DayLoss = 600, Ny = D(2026, 11, 13, 11, 0) }; Ok(KeystoneSeasonQuotes.Line("BOTH", "FALL", loss, false).Item1.StartsWith("⚠ −$460 of your $600"), "day loss close");
        var cap = new KeystoneLookInputs { Live = true, HasAccount = true, Today = 820, DayCap = 1000, Ny = D(2026, 11, 13, 11, 0, 45) }; Ok(KeystoneSeasonQuotes.Line("BOTH", "FALL", cap, false).Item1.StartsWith("💰 +$820"), "day cap → protect");
        var ev = new KeystoneLookInputs { Live = true, HasAccount = true, TargetLeft = 1200, Ny = D(2026, 11, 13, 11, 0, 45) }; Ok(KeystoneSeasonQuotes.Line("REMINDERS", "FALL", ev, false).Item1.StartsWith("🎯 $1,200"), "eval target to go");
        Ok(KeystoneSeasonQuotes.Reminders(new KeystoneLookInputs { Live = false, Locked = true, Ny = D(2026, 11, 13, 16, 40) }).Count == 0, "the BACKTEST STUDIO: no live reminders");
        foreach (var m in KeystoneLookSettings.QuoteModes) foreach (var s in KeystoneSeasons.Names) { var x = KeystoneSeasonQuotes.Line(m, s, I, false).Item1; Ok(!x.Contains("\n"), "one line " + m + " " + s); }
        // 4. scenes: every scene × season builds, every point inside the box (+ tiny tolerance), colours parse
        foreach (var s in KeystoneSeasons.Names.Concat(new[] { "" }))
            foreach (var sc in KeystoneScene.Names.Concat(new[] { "" }))
                foreach (var sz in new[] { new[] { 1920.0, 34 }, new[] { 900.0, 34 }, new[] { 1400.0, 700 } })
                {
                    var ls = KeystoneScene.Build(sc, KeystoneSeasons.Palette(s), sz[0], sz[1], 7); bool inside = true, cols = true; int pts = 0;
                    foreach (var l in ls)
                    {
                        if (l.Kind == "DISC") continue;
                        int stp = l.Kind == "DOTS" ? 3 : 2; for (int k = 0; k + 1 < l.Pts.Count; k += stp) { pts++; if (l.Pts[k] < -sz[0] * 0.02 || l.Pts[k] > sz[0] * 1.02 || l.Pts[k + 1] < -sz[1] * 0.6 || l.Pts[k + 1] > sz[1] * 1.001 + 0.5) inside = false; }
                        foreach (var c in l.Colors) if (KeystoneSeasons.Argb(c).Length != 4 || !c.StartsWith("#")) cols = false;
                    }
                    Ok(ls.Count > 0 && inside && cols, "scene " + (sc == "" ? "(none)" : sc) + " / " + (s == "" ? "neutral" : s) + " " + sz[0] + "×" + sz[1] + " inside the box");
                    Ok(pts < 6000, "scene stays light (" + pts + " points)");
                }
        Ok(KeystoneScene.Build("MOUNTAINS", KeystoneSeasons.Palette("FALL"), 800, 34, 3).Select(l => string.Join(",", l.Pts)).SequenceEqual(KeystoneScene.Build("MOUNTAINS", KeystoneSeasons.Palette("FALL"), 800, 34, 3).Select(l => string.Join(",", l.Pts))), "scenes are deterministic");
        Ok(KeystoneScene.Build("SNOWPEAKS", KeystoneSeasons.Palette("WINTER"), 800, 34, 3).Any(l => l.Tag == "frost") && !KeystoneScene.Build("OCEAN", KeystoneSeasons.Palette("SUMMER"), 800, 34, 3).Any(l => l.Tag == "frost"), "winter frost layer");
        // 5. particles: ≤ 150, inside the band after minutes of steps, reduce = fewer + slower, off = none
        foreach (var k in KeystoneParticles.Kinds)
            foreach (var w in new[] { 300.0, 1920, 3840, 7680 })
            {
                var p = new KeystoneParticles(); p.Reset(k, w, 34, false, 5); Ok(p.N <= KeystoneParticles.Max, k + " ≤ 150 at " + w);
                for (int f = 0; f < 30 * 120; f++) p.Step(1 / 30.0);
                bool ins = true; for (int i = 0; i < p.N; i++) if (!p.Inside(i) || p.Alpha(i) < 0 || p.Alpha(i) > 1) ins = false; Ok(ins, k + " stays in the band, alpha 0..1 (" + w + ")");
                var r = new KeystoneParticles(); r.Reset(k, w, 34, true, 5); Ok(r.N <= Math.Max(1, p.N / 2 + 1), k + " reduce motion = fewer");
            }
        Ok(KeystoneParticles.CountFor("", 1920, false) == 0 && KeystoneParticles.CountFor("SNOW", 0, false) == 0, "off = none");
        // 6. settings: defaults, read / write, per-window independence, MATCH
        var d0 = KeystoneLookSettings.Read(k => ""); Ok(d0.Season == "AUTO" && d0.Scene == "MATCH" && d0.Anim == "OFF" && d0.Place == "AROUND" && d0.Quotes == "BOTH" && !d0.LowPower, "defaults: season AUTO, scene MATCH (static), animation OFF, around the chart");
        var d1 = new KeystoneLookSettings { Season = "WINTER", Scene = "AURORA", Anim = "MATCH", Place = "FAINT", Quotes = "REMINDERS", Reduce = true, LowPower = true, File = "C:\\x.png" }; var wr = d1.Write(); var d2 = KeystoneLookSettings.Read(k => wr.ContainsKey(k) ? wr[k] : "");
        Ok(d2.Season == "WINTER" && d2.Scene == "AURORA" && d2.Anim == "MATCH" && d2.Place == "FAINT" && d2.Quotes == "REMINDERS" && d2.Reduce && d2.LowPower && d2.File == "C:\\x.png", "round trip");
        Ok(d2.AnimNow(D(2026, 7, 1)) == "STARS", "MATCH under the aurora = stars");
        Ok(KeystoneLookSettings.Read(k => k == "season" ? "nonsense" : "").Season == "AUTO", "bad values fall back");
        Ok(d0.SceneNow(D(2026, 11, 13)) == "FOREST" && d0.SceneNow(D(2026, 1, 13)) == "SNOWPEAKS" && d0.AnimNow(D(2026, 11, 13)) == "", "MATCH follows the season; no animation by default");
        Ok(new KeystoneLookSettings { Season = "OFF" }.SceneNow(D(2026, 11, 13)) == "", "season OFF + MATCH = no scene");
        Ok(KeystoneLookSettings.FaintOpacity <= 0.06, "faint ≤ 6 %");
        Ok(new KeystoneLookSettings { HideDay = "2026-11-13" }.QuotesHiddenOn(D(2026, 11, 13, 23, 0)) && !new KeystoneLookSettings { HideDay = "2026-11-13" }.QuotesHiddenOn(D(2026, 11, 14, 0, 1)), "✕ hides for today only");
        // 7. MY PICTURE / MY VIDEO files
        var dir = Path.Combine(Path.GetTempPath(), "ks-scenes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "lake.jpg"), new byte[] { 1, 2, 3 }); File.WriteAllBytes(Path.Combine(dir, "falls.mp4"), new byte[] { 1 }); File.WriteAllText(Path.Combine(dir, "notes.txt"), "x"); File.WriteAllBytes(Path.Combine(dir, "empty.png"), new byte[0]);
        var fl = KeystoneLookFiles.List(dir); Ok(fl.Count == 3 && fl.All(f => KeystoneLookFiles.KindOf(f) != ""), "lists pictures + videos only");
        Ok(KeystoneLookFiles.Problem(Path.Combine(dir, "lake.jpg")) == "" && KeystoneLookFiles.Problem(Path.Combine(dir, "empty.png")).StartsWith("the file is empty") && KeystoneLookFiles.Problem(Path.Combine(dir, "gone.jpg")).StartsWith("the file is gone") && KeystoneLookFiles.Problem(Path.Combine(dir, "notes.txt")).StartsWith("not a picture") && KeystoneLookFiles.Problem("") == "no file chosen", "file problems in one line");
        Ok(KeystoneLookFiles.KindOf("A.MP4") == "VIDEO" && KeystoneLookFiles.KindOf("b.PNG") == "PICTURE" && KeystoneLookFiles.List(Path.Combine(dir, "nope")).Count == 0, "kinds / a missing folder");
        Ok(KeystoneLookFiles.DecodeWidth(1920) == 1920 && KeystoneLookFiles.DecodeWidth(20000) == 3840 && KeystoneLookFiles.DecodeWidth(0) == 1920, "decode at the display width");
        try { Directory.Delete(dir, true); } catch { }
        // 8. 📷 PHOTO TEMPLATES: the catalog (URLs only — nothing is downloaded by a test), HTTPS to the two hosts, the byte check, the band slice
        var ph = KeystoneLookCatalog.Photos; Ok(ph.Length >= 6 && ph.Length <= 8 && ph.Select(x => x.Id).Distinct().Count() == ph.Length, "6–8 photo templates, unique ids");
        foreach (var x in ph) Ok(KeystoneLookCatalog.UrlOk(x.Url) && x.Name.StartsWith("📷 ") && KeystoneScene.Names.Contains(x.Fallback) && x.Credit.Contains("License") && (x.Anim == "" || KeystoneParticles.Kinds.Contains(x.Anim)) && x.ChartTheme != "" && x.BandY >= 0 && x.BandY <= 1 && KeystoneSeasons.Argb(x.Accent).Length == 4, "photo " + x.Id + " complete");
        Ok(!KeystoneLookCatalog.UrlOk("http://images.unsplash.com/x.jpg") && !KeystoneLookCatalog.UrlOk("https://evil.example.com/x.jpg") && !KeystoneLookCatalog.UrlOk("file:///c:/x.jpg"), "HTTPS to the two image hosts only");
        var jpg = new byte[5000]; jpg[0] = 0xFF; jpg[1] = 0xD8; Ok(KeystoneLookCatalog.CheckBytes(jpg) == "" && KeystoneLookCatalog.CheckBytes(new byte[5000]) != "" && KeystoneLookCatalog.CheckBytes(new byte[10]) != "" && KeystoneLookCatalog.CheckBytes(new byte[3000001]) != "", "the byte check (jpeg, not a picture, empty, > 3 MB)");
        var sl = KeystoneLookCatalog.BandSlice(0.3, 1920, 34, 16.0 / 9); Ok(sl[1] > 0 && sl[1] < 0.05 && Math.Abs(sl[0] + sl[1] / 2 - 0.3) < 1e-9, "the band slice centred on BandY");
        Ok(KeystoneLookCatalog.BandSlice(0.99, 1920, 34, 16.0 / 9)[0] + KeystoneLookCatalog.BandSlice(0.99, 1920, 34, 16.0 / 9)[1] <= 1 + 1e-9, "the slice stays inside the photo");
        Ok(KeystoneLookSettings.Read(k => k == "scene" ? "photo:aurora" : "").Scene == "PHOTO:AURORA" && KeystoneLookSettings.Read(k => k == "scene" ? "PHOTO:nope" : "").Scene == "MATCH", "PHOTO:<id> kept, unknown → MATCH");
        Ok(new KeystoneLookSettings { Scene = "PHOTO:SNOWNIGHT", Anim = "MATCH" }.AnimNow(D(2026, 7, 1)) == "SNOW" && new KeystoneLookSettings { Scene = "PHOTO:SNOWNIGHT" }.AnimNow(D(2026, 7, 1)) == "", "a photo's animation only when asked (MATCH)");
        Console.WriteLine(fails == 0 ? "SEASONS + SCENES TESTS PASSED (" + n + " checks)" : "SEASONS + SCENES TESTS FAILED " + fails + " of " + n);
        return fails == 0 ? 0 : 1;
    }
    // the HTML / SVG preview: the SAME layers the WPF view draws (KeystoneScene.Build), at the header-band size and at a big size
    static string Col(string hex) { var a = KeystoneSeasons.Argb(hex); return "rgba(" + a[1] + "," + a[2] + "," + a[3] + "," + (a[0] / 255.0).ToString("0.###", CultureInfo.InvariantCulture) + ")"; }
    static string F(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    static int gid = 0;
    public static string Svg(string scene, KeystoneSeasonPalette p, double w, double h, string extra)
    {
        var sb = new StringBuilder(); var defs = new StringBuilder(); sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='" + F(w) + "' height='" + F(h) + "' viewBox='0 0 " + F(w) + " " + F(h) + "'>");
        var body = new StringBuilder();
        foreach (var l in KeystoneScene.Build(scene, p, w, h, 7))
        {
            string fill;
            if (l.Colors.Length == 1) fill = Col(l.Colors[0]);
            else
            {
                string id = "g" + (gid++); defs.Append(l.Kind == "DISC" ? "<radialGradient id='" + id + "'>" : "<linearGradient id='" + id + "' x1='0' y1='0' x2='" + (l.Horizontal ? "1" : "0") + "' y2='" + (l.Horizontal ? "0" : "1") + "'>");
                for (int i = 0; i < l.Colors.Length; i++) defs.Append("<stop offset='" + F(i < l.Offsets.Length ? l.Offsets[i] : (double)i / (l.Colors.Length - 1)) + "' stop-color='" + Col(l.Colors[i]) + "'/>");
                defs.Append(l.Kind == "DISC" ? "</radialGradient>" : "</linearGradient>"); fill = "url(#" + id + ")";
            }
            if (l.Kind == "POLY") { body.Append("<path fill='" + fill + "' d='M"); for (int i = 0; i + 1 < l.Pts.Count; i += 2) body.Append((i == 0 ? "" : " L") + F(l.Pts[i]) + "," + F(l.Pts[i + 1])); body.Append(" Z'/>"); }
            else if (l.Kind == "DISC") body.Append("<circle cx='" + F(l.Pts[0]) + "' cy='" + F(l.Pts[1]) + "' r='" + F(l.Pts[2]) + "' fill='" + fill + "'/>");
            else if (l.Kind == "DOTS") for (int i = 0; i + 2 < l.Pts.Count; i += 3) body.Append("<circle cx='" + F(l.Pts[i]) + "' cy='" + F(l.Pts[i + 1]) + "' r='" + F(l.Pts[i + 2]) + "' fill='" + fill + "'/>");
            else if (l.Kind == "LINES") for (int i = 0; i + 3 < l.Pts.Count; i += 4) body.Append("<line x1='" + F(l.Pts[i]) + "' y1='" + F(l.Pts[i + 1]) + "' x2='" + F(l.Pts[i + 2]) + "' y2='" + F(l.Pts[i + 3]) + "' stroke='" + fill + "' stroke-width='" + F(l.Width) + "' stroke-linecap='round'/>");
        }
        sb.Append("<defs>" + defs + "</defs>" + body + extra + "</svg>"); return sb.ToString();
    }
    static string Particles(string kind, double w, double h, KeystoneSeasonPalette p)
    {
        var x = new KeystoneParticles(); x.Reset(kind, w, h, false, 5); for (int f = 0; f < 90; f++) x.Step(1 / 30.0); var sb = new StringBuilder();
        for (int i = 0; i < x.N; i++)
        {
            double a = x.Alpha(i);
            if (kind == "SNOW") sb.Append("<circle cx='" + F(x.X[i]) + "' cy='" + F(x.Y[i]) + "' r='" + F(x.S[i]) + "' fill='rgba(255,255,255," + F(a) + ")'/>");
            else if (kind == "RAIN") sb.Append("<line x1='" + F(x.X[i]) + "' y1='" + F(x.Y[i]) + "' x2='" + F(x.X[i] - 1.2) + "' y2='" + F(x.Y[i] + x.S[i]) + "' stroke='" + Col(KeystoneSeasons.Alpha(KeystoneSeasons.Mix("#FFFFFF", p.Accent, 0.3), a)) + "' stroke-width='0.8'/>");
            else if (kind == "LEAVES") sb.Append("<ellipse cx='" + F(x.X[i]) + "' cy='" + F(x.Y[i]) + "' rx='" + F(x.S[i] * (0.35 + 0.65 * x.Tumble(i))) + "' ry='" + F(x.S[i] * 0.6) + "' fill='" + Col(KeystoneSeasons.Alpha(i % 3 == 0 ? p.Accent : i % 3 == 1 ? p.Accent2 : p.Glow, a)) + "'/>");
            else if (kind == "STARS") sb.Append("<circle cx='" + F(x.X[i]) + "' cy='" + F(x.Y[i]) + "' r='" + F(x.S[i] * 0.55) + "' fill='rgba(255,255,255," + F(Math.Min(1, a)) + ")'/>");
            else if (kind == "SUN") { double cx = x.X[i], cy = x.Y[i], ang = x.P[i], len = w * 0.22, sp = 0.05; sb.Append("<path d='M" + F(cx) + "," + F(cy) + " L" + F(cx + len * Math.Cos(ang - sp)) + "," + F(cy + len * Math.Sin(ang - sp)) + " L" + F(cx + len * Math.Cos(ang + sp)) + "," + F(cy + len * Math.Sin(ang + sp)) + " Z' fill='" + Col(KeystoneSeasons.Alpha(p.Glow, a)) + "'/>"); }
        }
        return sb.ToString();
    }
    static void Preview(string outDir)
    {
        Directory.CreateDirectory(outDir); var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset='utf-8'><title>KeyStone 13k look preview</title><style>body{margin:0;background:#07090f;color:#cfd7e6;font:13px 'Segoe UI',Arial,sans-serif;padding:18px}h2{margin:26px 0 8px;font-size:16px;letter-spacing:.5px}.cap{color:#8a94a8;margin:4px 0 2px;font-size:12px}.band{position:relative;width:1400px;height:34px;overflow:hidden;border-radius:2px}.band .t{position:absolute;left:0;top:0;height:34px;display:flex;align-items:center;gap:10px;padding:0 12px;font-weight:700;font-size:13px}.band .q{position:absolute;right:150px;top:0;height:34px;display:flex;align-items:center;font-size:12px;font-style:italic}.band .b{position:absolute;right:0;top:0;height:34px;width:138px;display:flex;justify-content:space-around;align-items:center;color:#c8d2e6}.line{height:2px;width:1400px}.win{width:1400px;background:#0b0f18;padding-bottom:8px}.chart{margin:6px 8px 0;height:110px;background:#131722;position:relative;overflow:hidden;border:1px solid #1e222d}.grid4{display:grid;grid-template-columns:repeat(3,460px);gap:12px}.tile{width:460px}</style></head><body>");
        html.Append("<h1 style='font-size:20px'>🍂 SEASONS + 🖼 SCENES — BUILD 2026-11-13k preview (the same KeystoneScene layers + palettes the WPF header draws)</h1><div class='cap'>The header band = the title bar (34 px). The chart below each band is a stand-in (the real chart template is unchanged — candles, P/L green / red never change).</div>");
        foreach (var s in KeystoneSeasons.Names)
        {
            var p = KeystoneSeasons.Palette(s); string sc = KeystoneSeasons.DefaultScene(s), an = KeystoneSeasons.DefaultAnim(s);
            html.Append("<h2>" + KeystoneSeasons.Icon(s) + " " + s + " — default scene " + KeystoneScene.Label(sc) + " • animation " + KeystoneParticles.Label(an) + " (when switched on)</h2>");
            html.Append("<div class='win'><div class='band'>" + Svg(sc, p, 1400, 34, Particles(an, 1400, 34, p)) + "<div class='t' style='color:" + Col(p.Ink) + "'>◆ SPASIX <span style='color:" + Col(p.Accent) + "'>FUTURE</span> <span style='opacity:.8;font-weight:600'>⧉ WINDOWS ▾ &nbsp; LIVE TRADING</span></div><div class='q' style='color:" + Col(p.Ink) + "'>" + KeystoneSeasons.Icon(s) + " " + KeystoneSeasonQuotes.Quote(s, new DateTime(2026, 11, 13, 10, 0, 0)) + " &nbsp;✕</div><div class='b'>— ▢ ✕</div></div>");
            html.Append("<div class='line' style='background:linear-gradient(90deg," + Col(p.Accent) + "," + Col(KeystoneSeasons.Alpha(p.Accent2, 0)) + ");box-shadow:0 0 8px " + Col(KeystoneSeasons.Alpha(p.Accent, 0.6)) + "'></div>");
            html.Append("<div class='chart'>" + Candles() + "</div></div>");
            html.Append("<div class='cap'>every scene in " + s + " colours (header band 460 × 34, then the 'faint behind the chart' veil at 5 % over a plot):</div><div class='grid4'>");
            foreach (var x in KeystoneScene.Names)
                html.Append("<div class='tile'><div class='cap'>" + KeystoneScene.Label(x) + "</div><div style='height:34px;overflow:hidden'>" + Svg(x, p, 460, 34, "") + "</div><div style='position:relative;height:120px;background:#131722;margin-top:4px;overflow:hidden'>" + Candles() + "<div style='position:absolute;inset:0;opacity:0.05'>" + Svg(x, p, 460, 120, "") + "</div></div></div>");
            html.Append("</div>");
        }
        html.Append("<h2>Big view of each scene (what the procedural art looks like at 1400 × 360 — the header shows a 34-px slice of the same drawing logic)</h2><div class='grid4' style='grid-template-columns:repeat(2,690px)'>");
        foreach (var s in KeystoneSeasons.Names) foreach (var x in new[] { KeystoneSeasons.DefaultScene(s), s == "FALL" ? "MOUNTAINS" : s == "WINTER" ? "AURORA" : s == "SPRING" ? "MOUNTAINS" : "AURORA" })
            html.Append("<div><div class='cap'>" + KeystoneSeasons.Icon(s) + " " + s + " • " + KeystoneScene.Label(x) + "</div>" + Svg(x, KeystoneSeasons.Palette(s), 690, 180, Particles(KeystoneSeasons.DefaultAnim(s), 690, 180, KeystoneSeasons.Palette(s))) + "</div>");
        html.Append("</div></body></html>");
        File.WriteAllText(Path.Combine(outDir, "look-preview.html"), html.ToString(), new UTF8Encoding(false));
        Console.WriteLine("wrote " + Path.Combine(outDir, "look-preview.html"));
    }
    // the 🎨 LOOK gallery preview: every PHOTO template (the scratchpad copies of the catalog URLs) + every DRAWN template, and one window mock per photo
    static readonly Dictionary<string, string[]> Themes = new Dictionary<string, string[]> {
        { "NORDIC FROST", new[] { "#232A36", "#2C3442", "#88C0D0", "#BF616A" } }, { "OCEAN DEEP", new[] { "#041824", "#0B2A3B", "#2EC4B6", "#FF6B6B" } }, { "WARM DUSK", new[] { "#1B1611", "#272019", "#C9A86A", "#B5655B" } },
        { "MIDNIGHT VELVET", new[] { "#13142A", "#1E2038", "#6FC3B8", "#C98BB9" } }, { "TWILIGHT SOFT", new[] { "#1C1F36", "#292D4A", "#9AD0C2", "#D7A2C8" } }, { "MIDNIGHT EMBER", new[] { "#151A26", "#212838", "#7FC8A9", "#E89A6B" } },
        { "VELVET ICE", new[] { "#17112D", "#261D44", "#9FE3F0", "#B78CFF" } } };
    static string CandlesIn(string up, string down, string grid, int w, int h)
    {
        var r = new Random(5); var sb = new StringBuilder("<svg width='" + w + "' height='" + h + "' style='position:absolute;left:0;top:0'>"); for (int g = 1; g < 6; g++) sb.Append("<line x1='0' x2='" + w + "' y1='" + (g * h / 6) + "' y2='" + (g * h / 6) + "' stroke='" + grid + "'/>");
        double px = h / 2.0; for (int i = 0; i < w / 9; i++) { double o = px, c = px + (r.NextDouble() - 0.5) * 14, hi = Math.Min(o, c) - r.NextDouble() * 6, lo = Math.Max(o, c) + r.NextDouble() * 6; if (c < 20 || c > h - 20) c = h / 2.0; px = c; string col = c <= o ? up : down; double x = 6 + i * 9; sb.Append("<line x1='" + F(x + 3) + "' x2='" + F(x + 3) + "' y1='" + F(hi) + "' y2='" + F(lo) + "' stroke='" + col + "'/><rect x='" + F(x) + "' y='" + F(Math.Min(o, c)) + "' width='6' height='" + F(Math.Max(1, Math.Abs(c - o))) + "' fill='" + col + "'/>"); }
        return sb.Append("</svg>").ToString();
    }
    static void Gallery(string outDir, string photoDir)
    {
        var h = new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><title>KeyStone 13k LOOK gallery</title><style>body{margin:0;background:#07090f;color:#cfd7e6;font:13px 'Segoe UI',Arial,sans-serif;padding:18px}h2{font-size:15px;margin:22px 0 8px;color:#b48cff}.wrap{display:flex;flex-wrap:wrap;gap:8px}.tile{width:182px;background:#1b1f2a;border:1px solid #333a4a;border-radius:9px;padding:5px}.tile .th{width:170px;height:62px;border-radius:6px;overflow:hidden;background-size:cover;background-position:center}.tile .n{font-weight:600;font-size:11px;margin-top:3px}.tile .c{font-size:9.5px;color:#8a94a8;margin-top:2px;line-height:1.25}.win{width:1400px;margin:10px 0 26px;position:relative;border-radius:4px;overflow:hidden;background-size:cover;background-position:center}.dim{position:absolute;inset:0;background:rgba(9,13,20,.45)}.band{position:relative;height:34px;background-size:100% auto;background-repeat:no-repeat}.scrim{position:absolute;inset:0}.band .t{position:absolute;left:0;top:0;height:34px;display:flex;align-items:center;gap:10px;padding:0 12px;font-weight:700}.band .q{position:absolute;right:150px;top:0;height:34px;display:flex;align-items:center;font-style:italic;font-size:12px}.band .cr{position:absolute;left:50%;transform:translateX(-50%);bottom:0;font-size:9px;opacity:.6;color:#fff}.band .b{position:absolute;right:0;top:0;width:138px;height:34px;display:flex;justify-content:space-around;align-items:center;color:#c8d2e6}.line{height:2px;position:relative}.body{position:relative;padding:6px}.panel{background:rgba(16,23,34,.96);height:28px;border-radius:4px;margin-bottom:6px;display:flex;align-items:center;padding:0 10px;font-size:11px;color:#9aa6bd;gap:14px}.chart{position:relative;height:190px;border-radius:3px;overflow:hidden}</style></head><body>");
        h.Append("<h1 style='font-size:20px'>🎨 LOOK ▾ — the gallery (BUILD 2026-11-13k). Point at a tile = a live preview on the chart, click = choose, ✓ KEEP / ↺ BACK (Esc)</h1>");
        h.Append("<div style='color:#8a94a8'>Photos: free-licence (Unsplash License / Pexels License), downloaded once by KeyStone when you click one; KeyStone itself ships only the addresses. Drawn scenes are made in code.</div>");
        h.Append("<h2>📷 PHOTO TEMPLATES</h2><div class='wrap'>");
        foreach (var p in KeystoneLookCatalog.Photos) h.Append("<div class='tile'><div class='th' style=\"background-image:url('" + photoDir + "/" + p.Id.ToLowerInvariant() + ".jpg')\"></div><div class='n'>" + p.Name + "</div><div class='c'>" + p.Mood + " • chart: " + p.ChartTheme + (p.Anim != "" ? " • " + KeystoneParticles.Label(p.Anim) : "") + "<br>" + p.Credit + "</div></div>");
        h.Append("</div><h2>✏ DRAWN SCENES (fall colours shown — they follow the season)</h2><div class='wrap'>");
        foreach (var sc in KeystoneScene.Names) h.Append("<div class='tile'><div class='th'>" + Svg(sc, KeystoneSeasons.Palette("FALL"), 170, 62, "") + "</div><div class='n'>✏ " + KeystoneScene.Label(sc).Substring(KeystoneScene.Label(sc).IndexOf(' ') + 1) + " (drawn)</div></div>");
        h.Append("</div><h2>Each photo template on a window (title band = the photo's horizon slice + scrim; the window frame = the photo dimmed; the chart = its matching template; candles untouched)</h2>");
        foreach (var p in KeystoneLookCatalog.Photos)
        {
            var pal = p.Palette(); var th = Themes.ContainsKey(p.ChartTheme) ? Themes[p.ChartTheme] : new[] { "#131722", "#1E222D", "#26A69A", "#EF5350" }; var sl = KeystoneLookCatalog.BandSlice(p.BandY, 1400, 34, 16.0 / 9); double posY = sl[1] >= 1 ? 0 : sl[0] / (1 - sl[1]) * 100;
            string img = photoDir + "/" + p.Id.ToLowerInvariant() + ".jpg";
            h.Append("<div class='cap' style='margin-top:14px'>" + p.Name + " — " + p.Credit + "</div><div class='win' style=\"background-image:url('" + img + "')\"><div class='dim'></div>");
            h.Append("<div class='band' style=\"background-image:url('" + img + "');background-position:0 " + F(posY) + "%\"><div class='scrim' style='background:linear-gradient(90deg," + Col(KeystoneSeasons.Alpha(pal.Deep, .72)) + " 0%," + Col(KeystoneSeasons.Alpha(pal.Deep, .2)) + " 26%," + Col(KeystoneSeasons.Alpha(pal.Deep, .14)) + " 55%," + Col(KeystoneSeasons.Alpha(pal.Deep, .62)) + " 100%)'></div>");
            h.Append("<div class='t' style='color:" + Col(pal.Ink) + "'>◆ SPASIX <span style='color:" + Col(pal.Accent) + "'>FUTURE</span> <span style='opacity:.8;font-weight:600'>⧉ WINDOWS ▾ &nbsp; LIVE TRADING</span></div><div class='q' style='color:" + Col(pal.Ink) + "'>" + KeystoneSeasons.Icon("FALL") + " " + KeystoneSeasonQuotes.Quote("FALL", new DateTime(2026, 11, 13, 13, 0, 0)) + " &nbsp;✕</div><div class='cr'>" + p.Credit + "</div><div class='b'>— ▢ ✕</div></div>");
            h.Append("<div class='line' style='background:linear-gradient(90deg," + Col(pal.Accent) + "," + Col(pal.Accent2) + " 55%,transparent);box-shadow:0 0 10px " + Col(KeystoneSeasons.Alpha(pal.Glow, .55)) + "'></div>");
            h.Append("<div class='body'><div class='panel' style='border:1px solid " + Col(KeystoneSeasons.Alpha(pal.Accent, .34)) + "'>MNQ ▾ &nbsp; 1m 2m 5m 15m &nbsp; 🎨 " + p.ChartTheme + " ▾ &nbsp; BALANCE $51,240 &nbsp; <span style='color:#3AC488'>TODAY +$420</span></div><div class='chart' style='background:" + th[0] + "'>" + CandlesIn(th[2], th[3], th[1], 1388, 190) + "</div></div></div>");
        }
        h.Append("</body></html>"); File.WriteAllText(Path.Combine(outDir, "look-gallery.html"), h.ToString(), new UTF8Encoding(false)); Console.WriteLine("wrote " + Path.Combine(outDir, "look-gallery.html"));
    }
    static string Candles()
    {
        var r = new Random(3); var sb = new StringBuilder("<svg width='1400' height='120' style='position:absolute;left:0;top:0'>"); double px = 60;
        for (int i = 0; i < 160; i++) { double o = px, c = px + (r.NextDouble() - 0.48) * 8, hi = Math.Max(o, c) + r.NextDouble() * 4, lo = Math.Min(o, c) - r.NextDouble() * 4; px = c; string col = c >= o ? "#26A69A" : "#EF5350"; double x = 10 + i * 8.6; sb.Append("<line x1='" + F(x + 3) + "' x2='" + F(x + 3) + "' y1='" + F(hi) + "' y2='" + F(lo) + "' stroke='" + col + "'/><rect x='" + F(x) + "' y='" + F(Math.Min(o, c)) + "' width='6' height='" + F(Math.Max(1, Math.Abs(c - o))) + "' fill='" + col + "'/>"); }
        return sb.Append("</svg>").ToString();
    }
}
