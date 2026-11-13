// 🖼 BUILD 2026-11-13n — MY SCENE PACK (your 10 pictures built in) + ✨ AUTO-MATCH + ✨ PERFECT CONTRAST + 🎚 ADJUST + the content fingerprint + the robust Scenes-folder scan.
// Display only (no order path). Tool modes for the preview: `--grade <raw-bgra> <w> <h> <comboId> <out-raw>` (the SAME KeystoneLookGrade the chart bakes) and
// `--solve <raw-bgra> <w> <h> <light 0/1>` (the ✨ PERFECT result as one line).
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text;
using NinjaTrader.NinjaScript;
public static class ScenePackTests
{
    static int fails = 0, n = 0; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Ok(bool c, string what) { n++; if (!c) { fails++; Console.WriteLine("FAIL  " + what); } }
    // a synthetic BGRA picture
    static byte[] Img(int w, int h, Func<int, int, int[]> rgb) { var p = new byte[w * h * 4]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { var c = rgb(x, y); int i = (y * w + x) * 4; p[i] = (byte)c[2]; p[i + 1] = (byte)c[1]; p[i + 2] = (byte)c[0]; p[i + 3] = 255; } return p; }
    static int Cl(double v) { return (int)Math.Max(0, Math.Min(255, Math.Round(v))); }
    static double Lum(string hex) { return KeystoneLookBehind.Lum(hex); }
    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        if (args.Length >= 5 && args[0] == "--grade") { Grade(args); return 0; }
        if (args.Length >= 5 && args[0] == "--solve") { var px = File.ReadAllBytes(args[1]); int w0 = int.Parse(args[2]), h0 = int.Parse(args[3]); var c = KeystoneLookPerfect.Solve("PERFECT:X", "x", px, w0, h0, w0 * 4, args[4] == "1"); Console.WriteLine(Line(c)); return 0; }
        // 1. the pack catalog: 10 pictures, 2–3 combos each, every combo passes the checker (mean + bright + dark patch), unique ids / template names
        var pics = KeystoneScenePack.Pics; var cbs = KeystoneScenePack.Combos;
        Ok(pics.Length == 10 && pics.Select(p => p.Id).Distinct().Count() == 10, "10 pack pictures (the duplicate sea cliffs kept once)");
        Ok(pics.All(p => p.SigBytes.Length == KeystoneLookSig.GW * KeystoneLookSig.GH * 3 && Math.Abs(p.Aspect - 2400.0 / 1792) < 0.01), "every picture has its 12 × 9 fingerprint + the 4:3 aspect");
        Ok(pics.All(p => KeystoneScenePack.ForPic(p.Id).Count >= 2 && KeystoneScenePack.ForPic(p.Id).Count <= 3), "2–3 combos per picture");
        Ok(cbs.Count(c => c.Variant == "DAYLIGHT") >= 5 && cbs.Count(c => c.Variant == "DUSK") == 10 && cbs.Count(c => c.Variant == "CLASSIC") == 10, "dusk + classic for each, daylight where it works (" + cbs.Count(c => c.Variant == "DAYLIGHT") + ")");
        var allIds = KeystoneLookCombos.All.Select(c => c.Id).Concat(cbs.Select(c => c.Id)).ToList(); var allNames = KeystoneLookCombos.All.Select(c => c.ThemeName).Concat(cbs.Select(c => c.ThemeName)).ToList();
        Ok(allIds.Distinct().Count() == allIds.Count && allNames.Distinct().Count() == allNames.Count, "unique ids / template names across 📷 + 🖼");
        Ok(new[] { "golden-avenue", "golden-stream", "golden-fog-path", "autumn-pass", "lake-cabin", "alpine-lake", "alpine-meadow", "redwood-trail", "sea-cliffs", "green-meadow" }.All(sl => pics.Any(p => p.Slug == sl)), "the 10 pictures by name");
        foreach (var c in cbs)
        {
            var k = KeystoneLookCombos.Check(c);
            Ok(k.Ok, "combo " + c.Id + " readable: " + k.Why);
            Ok(c.Grade != null && c.Grade.Tint == c.Bg && c.ThemeName.StartsWith("🖼 ") && KeystoneLookCombos.IsGradedId(c.Id) && KeystoneLookCombos.Find(c.Id) == c, "combo " + c.Id + " graded toward its background, 🖼 template, found");
            Ok(Math.Abs(KeystoneLookBehind.Effective(Lum(c.Bg), Lum(c.Grade.ColorHex(c.SrcMean)), c.Pct / 100.0, k.Scrim) - Lum(c.Bg)) <= KeystoneLookBehind.Tol + 1e-9, "combo " + c.Id + " the contrast guard holds at " + c.Pct + " %");
            Ok(c.Light ? c.Grade.Darken == 0 && c.Grade.Wash > 0.5 : c.Grade.Darken > 0.3 && c.Grade.Darken < 0.9, "combo " + c.Id + " graded (dusk darkened, daylight faded)");
        }
        // 2. the grade is affine: the graded picture's mean = the grade of the mean (and the blur keeps it)
        {
            var rnd = new Random(3); int w = 96, h = 64; var px = Img(w, h, (x, y) => new[] { Cl(120 + 100 * Math.Sin(x * 0.3) + rnd.Next(-20, 20)), Cl(140 + 60 * Math.Cos(y * 0.2)), Cl(60 + x) });
            var s0 = KeystoneLookStats.Measure(px, w, h, w * 4); var g = new KeystoneLookGrade { Darken = 0.55, Wash = 0.35, Sat = 0.85, Blur = 0, Tint = "#140F07" };
            var q = (byte[])px.Clone(); g.Apply(q, w, h, w * 4, 1); var s1 = KeystoneLookStats.Measure(q, w, h, w * 4);
            var pred = KeystoneSeasons.Argb(g.ColorHex(s0.Mean)); var got = KeystoneSeasons.Argb(s1.Mean); Ok(Enumerable.Range(1, 3).All(i => Math.Abs(pred[i] - got[i]) <= 1), "graded mean = grade(mean) (" + g.ColorHex(s0.Mean) + " vs " + s1.Mean + ")");
            var g2 = g.Clone(); g2.Blur = 6; var q2 = (byte[])px.Clone(); g2.Apply(q2, w, h, w * 4, 1); var s2 = KeystoneLookStats.Measure(q2, w, h, w * 4); var got2 = KeystoneSeasons.Argb(s2.Mean);
            Ok(Enumerable.Range(1, 3).All(i => Math.Abs(pred[i] - got2[i]) <= 1) && s2.Busy < s1.Busy * 0.6, "the blur keeps the mean and calms the detail (busy " + s1.Busy.ToString("0.000", IC) + " → " + s2.Busy.ToString("0.000", IC) + ")");
            Ok(Lum(s1.Hi) < Lum(s0.Hi) && Lum(s1.Mean) < Lum(s0.Mean), "darkened");
            var r = new KeystoneLookGrade { CropY = 0.9, CropH = 0.6 }.CropRect(100, 80); Ok(r[1] + r[3] <= 80 && r[3] == 48 && r[1] == 32, "the focus crop stays inside the picture");
        }
        // 3. the fingerprint: the same picture renamed / re-encoded (noise) / smaller matches, another picture never does
        {
            int w = 240, h = 179; Func<int, int, int[]> a = (x, y) => new[] { Cl(200 - y), Cl(150 + 40 * Math.Sin(x * 0.05)), Cl(40 + x / 3) };
            var p0 = Img(w, h, a); var sA = KeystoneLookSig.Of(p0, w, h, w * 4); var rnd = new Random(9);
            var noisy = Img(w, h, (x, y) => { var c = a(x, y); return new[] { Cl(c[0] + rnd.Next(-6, 7)), Cl(c[1] + rnd.Next(-6, 7)), Cl(c[2] + rnd.Next(-6, 7)) }; });
            var small = Img(96, 72, (x, y) => a(x * w / 96, y * h / 72));
            var other = Img(w, h, (x, y) => new[] { Cl(40 + x / 2), Cl(200 - y / 2), Cl(160) });
            double dN = KeystoneLookSig.Dist(sA, KeystoneLookSig.Of(noisy, w, h, w * 4)), dS = KeystoneLookSig.Dist(sA, KeystoneLookSig.Of(small, 96, 72, 96 * 4)), dO = KeystoneLookSig.Dist(sA, KeystoneLookSig.Of(other, w, h, w * 4));
            Ok(dN < KeystoneLookSig.MatchDist && dS < KeystoneLookSig.MatchDist && dO > KeystoneLookSig.MatchDist * 2, "re-encoded " + dN.ToString("0.0", IC) + " / smaller " + dS.ToString("0.0", IC) + " match, another picture " + dO.ToString("0.0", IC) + " does not");
            var pk = pics[3]; var near = (byte[])pk.SigBytes.Clone(); for (int i = 0; i < near.Length; i++) near[i] = (byte)Math.Max(0, Math.Min(255, near[i] + (i % 2 == 0 ? 3 : -3)));
            Ok(KeystoneScenePack.Identify(pk.SigBytes, pk.Aspect) == pk && KeystoneScenePack.Identify(near, pk.Aspect) == pk, "a pack picture is recognised by content (exact + re-encoded)");
            Ok(KeystoneScenePack.Identify(pk.SigBytes, 16.0 / 9) == null && KeystoneScenePack.Identify(KeystoneLookSig.Of(other, w, h, w * 4), 1.34) == null, "another aspect / picture = not a pack picture");
            Ok(pics.All(p => pics.Where(q => q != p).All(q => KeystoneLookSig.Dist(p.SigBytes, q.SigBytes) > KeystoneLookSig.MatchDist * 2)), "the 10 pictures are far apart (no confusion)");
            Ok(KeystoneLookSig.Id(sA, 1.34) == KeystoneLookSig.Id(KeystoneLookSig.Of(noisy, w, h, w * 4), 1.34), "the ADJUST memory id survives a re-encode");
        }
        // 4. the registry, the built-in pictures, the missing-pack path
        {
            KeystoneScenePack.Reset(); Ok(KeystoneScenePack.Present().Count == 0 && KeystoneScenePack.MissingLine(0).StartsWith("⤓ get the scene pack") && KeystoneScenePack.MissingLine(3) == "" && KeystoneScenePack.FileFor("KS-GOLDEN-AVENUE") == "", "nothing registered = the one missing line, no exception");
            Ok(KeystoneEmbeddedScenes.Slugs.Count() == 10 && KeystoneEmbeddedScenes.Slugs.All(sl => pics.Any(p => p.Slug == sl)), "your 10 pictures are built in");
            foreach (var sl in KeystoneEmbeddedScenes.Slugs) { var b = KeystoneEmbeddedScenes.Bytes(sl); Ok(b != null && b.Length > 50000 && b.Length <= 180000 && KeystoneLookFiles.Magic(b) == "JPEG", "built-in " + sl + " = a JPEG of " + (b != null ? b.Length / 1024 : 0) + " KB (≤ 180)"); }
            Ok(KeystoneLookFiles.KindOf(KeystoneEmbeddedScenes.PathOf("golden-avenue")) == "PICTURE" && KeystoneLookFiles.Problem(KeystoneEmbeddedScenes.PathOf("golden-avenue")) == "" && KeystoneLookFiles.Problem(KeystoneEmbeddedScenes.PathOf("nope")) != "", "a built-in path is a picture");
            var pk = pics.First(p => p.Slug == "golden-avenue"); string ep = KeystoneEmbeddedScenes.PathOf("golden-avenue");
            KeystoneScenePack.Register(ep, pk.SigBytes, 1339, 1000, null, 0, 0, 1, DateTime.MinValue);
            var tmp = Path.Combine(Path.GetTempPath(), "ks13n-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tmp); string copy = Path.Combine(tmp, "Gemini_Generated (5).jfif"); File.WriteAllBytes(copy, KeystoneEmbeddedScenes.Bytes("golden-avenue"));
            var near = (byte[])pk.SigBytes.Clone(); near[0] = (byte)(near[0] ^ 2); KeystoneScenePack.Register(copy, near, 2400, 1792, null, 0, 0, new FileInfo(copy).Length, new FileInfo(copy).LastWriteTimeUtc);
            Ok(KeystoneScenePack.Present().Count == 1 && KeystoneScenePack.PicOf(copy) == pk.Id && KeystoneScenePack.FileFor(pk.Id) == ep, "a renamed original = the same picture (one entry), the built-in copy shown first");
            // the settings: a pack combo = PICTURE behind at its 90 %, ComboNow by CONTENT, remembered (read / write, opacity above 40 %), toggled back
            var c0 = KeystoneScenePack.ForPic(pk.Id)[0]; var t = new KeystoneLookSettings();
            Ok(KeystoneLookCombos.Toggle(t, c0.Id) && t.Scene == "PICTURE" && t.File == ep && t.Place == "BEHIND" && t.BehindPct == c0.Pct && c0.Pct > KeystoneLookSettings.BehindMax && t.ComboNow == c0, "a pack combo = its picture BEHIND at " + c0.Pct + " % (above 13l's 40 %: the darkening is baked in)");
            t.File = copy; Ok(t.ComboNow == c0, "the same combo on the user's renamed original (content, not name)");
            var d = t.Write(); var rt = KeystoneLookSettings.Read(k0 => d.ContainsKey(k0) ? d[k0] : ""); Ok(rt.Combo == c0.Id && rt.BehindPct == c0.Pct && rt.ComboNow == c0, "remembered (read / write)");
            Ok(KeystoneLookSettings.Read(k0 => k0 == "combo" ? "" : k0 == "behindpct" ? "90" : "").BehindPct == KeystoneLookSettings.BehindMax, "an ungraded picture stays ≤ 40 %");
            Ok(!KeystoneLookCombos.Toggle(t, c0.Id) && t.Combo == "" && t.Scene == "MATCH", "the chosen combo again = back to the default");
            Ok(KeystoneLookSettings.Read(k0 => k0 == "combo" ? "AUTO:F0000-134" : "").Combo == "AUTO:F0000-134", "an ✨ id waits for the folder scan (kept)");
            // 🎚 ADJUST: persistence, the grade it gives, the hint when it breaks readability (never overridden)
            var a = KeystoneLookAdjust.Of(c0); a.Set = true; a.Darken = 0.2; a.Blur = 7; a.Sat = 1.2; a.FocusY = 0.3; var aw = a.Write(); var ar = KeystoneLookAdjust.Read(aw);
            Ok(ar.Set && ar.Combo == c0.Id && Math.Abs(ar.Darken - 0.2) < 1e-9 && Math.Abs(ar.FocusY - 0.3) < 1e-9 && KeystoneLookAdjust.Read("junk").Set == false, "ADJUST read / write (" + aw + ")");
            var ga = ar.Apply(c0); Ok(Math.Abs(ga.Darken - 0.2) < 1e-9 && ga.Blur == 7 && Math.Abs(ga.Sat - 1.2) < 1e-9 && Math.Abs(ga.CropY - 0.3) < 1e-9 && c0.Grade.Darken != 0.2, "ADJUST replaces the grade (the combo itself unchanged)");
            Ok(Math.Abs(KeystoneLookAdjust.Read(aw).Apply(KeystoneScenePack.ForPic(pk.Id)[1]).Darken - KeystoneScenePack.ForPic(pk.Id)[1].Grade.Darken) < 1e-9, "another combo of the picture ignores it");
            Ok(KeystoneLookAdjust.Key("bt", pk.Id) == "look.bt.adj." + pk.Id, "remembered per window + picture");
            {   /* the runtime hint reads the BAKED picture's measured colours: an adjust that leaves a near-white patch behind the candles = the hint, the setting kept */
                var adjd = ar.On(c0); var kb = KeystoneLookCombos.CheckOn(adjd, "#2E2618", "#FFFFFF", "#100C08", KeystoneLookSettings.ClampPct(adjd.Pct, true)); string hint = KeystoneLookAdjust.Hint(kb, false);
                Ok(!kb.Ok && hint.StartsWith("⚠ candles hard to read — raise DARKEN") && KeystoneLookAdjust.Hint(KeystoneLookCombos.Check(c0), false) == "" && Math.Abs(adjd.Grade.Darken - 0.2) < 1e-9 && KeystoneLookAdjust.Hint(kb, true).Contains("raise FADE"), "a bright baked patch = the hint, the setting kept (" + hint + ")");
            }
            t = new KeystoneLookSettings(); KeystoneLookCombos.Apply(t, c0); t.Adj = aw; Ok(t.GradedNow != null && Math.Abs(t.GradedNow.Grade.Darken - 0.2) < 1e-9 && t.Write()["adj"] == aw, "the settings carry the ADJUST");
            try { Directory.Delete(tmp, true); } catch { }
            KeystoneScenePack.Reset();
        }
        // 5. ✨ PERFECT CONTRAST / AUTO-MATCH on synthetic bright / dark / busy / colourful pictures: readable, less darkening than a fixed 90 %, idempotent, calmest focus
        {
            int w = 128, h = 96; var rnd = new Random(5);
            var cases = new Dictionary<string, byte[]>
            {
                { "bright", Img(w, h, (x, y) => new[] { Cl(235 - y / 4), Cl(238 - y / 5), Cl(245) }) },
                { "dark", Img(w, h, (x, y) => new[] { Cl(12 + x / 16), Cl(14), Cl(20 + y / 12) }) },
                { "busy", Img(w, h, (x, y) => { int v = rnd.Next(0, 256); return new[] { v, Cl(v * 0.9), Cl(255 - v) }; }) },
                { "colourful", Img(w, h, (x, y) => { double hu = x * 360.0 / w; var hx = KeystoneSeasons.Argb(KeystoneLookStats.Hsv(hu, 0.85, 0.9)); return new[] { hx[1], hx[2], hx[3] }; }) },
                { "gold forest", Img(w, h, (x, y) => new[] { Cl(200 - y + 30 * Math.Sin(x * 0.4)), Cl(150 - y * 0.8), Cl(40) }) },
            };
            foreach (var kv in cases)
                foreach (var light in new[] { false, true })
                {
                    var c = KeystoneLookPerfect.Solve("PERFECT:T", kv.Key, kv.Value, w, h, w * 4, light); var k = KeystoneLookCombos.Check(c); var c2 = KeystoneLookPerfect.Solve("PERFECT:T", kv.Key, kv.Value, w, h, w * 4, light);
                    Ok(k.Ok, "✨ " + kv.Key + (light ? " daylight" : "") + " passes the checker: " + k.Why + " • " + Line(c));
                    Ok(light || KeystoneLookPerfect.Darkening(c) < 0.9, "✨ " + kv.Key + " uses less darkening than a fixed 90 % (" + KeystoneLookPerfect.Darkening(c).ToString("0.00", IC) + ")");
                    Ok(Line(c) == Line(c2), "✨ " + kv.Key + (light ? " daylight" : "") + " idempotent");
                    Ok(Math.Abs(KeystoneLookBehind.Effective(Lum(c.Bg), Lum(c.Grade.ColorHex(c.SrcMean)), c.Pct / 100.0, k.Scrim) - Lum(c.Bg)) <= KeystoneLookBehind.Tol + 1e-9, "✨ " + kv.Key + " within the contrast guard");
                }
            // the least darkening: one step lighter would no longer reach the target
            { var c = KeystoneLookPerfect.Solve("PERFECT:T", "gold", cases["gold forest"], w, h, w * 4, false); var lighter = c.Copy(); lighter.Grade.Darken = Math.Round(c.Grade.Darken - 0.01, 2); var kl = KeystoneLookCombos.Check(lighter);
              bool startCap = c.Grade.Darken <= 0.01 || Math.Abs(KeystoneLookBehind.Effective(Lum(lighter.Bg), Lum(lighter.Grade.ColorHex(lighter.SrcMean)), lighter.Pct / 100.0, 0) - Lum(lighter.Bg)) > 0.075 / 0.9 * 0.9 - 1e-6;
              Ok(startCap || !kl.Ok || Math.Min(Math.Min(kl.UpMean, kl.UpHi), kl.UpLo) < KeystoneLookPerfect.Target || Math.Min(Math.Min(kl.DownMean, kl.DownHi), kl.DownLo) < KeystoneLookPerfect.Target, "✨ the least darkening that reads (" + c.Grade.Darken.ToString("0.00", IC) + ")"); }
            // the focus = the calmest band: noise on top, flat at the bottom
            { var px = Img(w, h, (x, y) => y < h / 2 ? new[] { rnd.Next(0, 256), rnd.Next(0, 256), rnd.Next(0, 256) } : new[] { 60, 90, 70 }); var f = KeystoneLookPerfect.Focus(px, w, h, w * 4, false); Ok(f[0] >= 0.35, "✨ the focus goes to the calm part (cy " + f[0].ToString("0.00", IC) + ")"); }
            // the 13m failure: a picked picture stayed AROUND the chart (title band / frame) — now its ✨ combo puts it BEHIND, graded, at 90 %; and a graded dusk shows about as much (≥ 90 %)
            // of the picture as 13m's plain picture at its 40 % maximum (the contrast guard scrims a bright plain picture down)
            {
                var ac = KeystoneLookPerfect.Solve("AUTO:F1", "mine", cases["gold forest"], w, h, w * 4, false); ac.PicId = "F1"; KeystoneScenePack.AddDynamic(ac);
                var t = new KeystoneLookSettings { Scene = "PICTURE", File = "x.jpg" }; KeystoneLookCombos.Apply(t, ac);
                Ok(t.Place == "BEHIND" && t.Scene == "PICTURE" && t.BehindPct == 90 && t.Graded, "a picked picture's ✨ combo = BEHIND the chart, graded, 90 % (13m left it AROUND)");
                foreach (var c in KeystoneScenePack.Combos.Where(x => x.Variant == "DUSK"))
                {
                    double bg = Lum("#131722"), plain = 0.4 * (1 - KeystoneLookBehind.Scrim(bg, Lum(c.SrcMean), 0.4)) * (Lum(c.SrcHi) - Lum(c.SrcLo)); var k = KeystoneLookCombos.Check(c);
                    double graded = c.Pct / 100.0 * (1 - k.Scrim) * (Lum(c.Grade.ColorHex(c.SrcHi)) - Lum(c.Grade.ColorHex(c.SrcLo)));
                    Ok(graded >= plain * 0.9, c.Id + " shows about as much as (≥ 90 % of) the plain 40 % picture or more (" + plain.ToString("0.000", IC) + " → " + graded.ToString("0.000", IC) + ")");
                }
                KeystoneScenePack.Reset();
            }
        }
        // 6. the folder scan (13m field report): sub-folders, .jfif, double extensions, the content decides, skip reasons
        {
            var dir = Path.Combine(Path.GetTempPath(), "ks13n-scan-" + Guid.NewGuid().ToString("N")); var sub = Path.Combine(dir, "KeyStone-Scenes"); Directory.CreateDirectory(sub);
            byte[] jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1, 1 }, png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }, webp = { 0x52, 0x49, 0x46, 0x46, 1, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0 };
            File.WriteAllBytes(Path.Combine(dir, "Gemini_Generated_Image (5).jfif"), jpg); File.WriteAllBytes(Path.Combine(dir, "lake.jpg.jfif"), jpg); File.WriteAllBytes(Path.Combine(dir, "x.jfif.jpg"), jpg);
            File.WriteAllBytes(Path.Combine(sub, "ks-golden-avenue.jpg"), jpg); File.WriteAllBytes(Path.Combine(sub, "really-a-png.jpg"), png); File.WriteAllBytes(Path.Combine(sub, "shot.webp"), webp); File.WriteAllBytes(Path.Combine(sub, "noext"), jpg);
            File.WriteAllText(Path.Combine(dir, "fake.jpg"), "hello"); File.WriteAllBytes(Path.Combine(dir, "empty.png"), new byte[0]); File.WriteAllText(Path.Combine(dir, "notes.txt"), "x"); File.WriteAllBytes(Path.Combine(dir, "clip.mp4"), new byte[] { 0, 0, 0, 0x18 });
            var seen = KeystoneLookFiles.Scan(dir); Func<string, KeystoneLookFiles.Seen> S = nm => seen.FirstOrDefault(x => Path.GetFileName(x.Path) == nm);
            Ok(S("Gemini_Generated_Image (5).jfif").Kind == "PICTURE" && S("lake.jpg.jfif").Kind == "PICTURE" && S("x.jfif.jpg").Kind == "PICTURE", ".jfif + both double extensions found");
            Ok(S("ks-golden-avenue.jpg") != null && S("ks-golden-avenue.jpg").Kind == "PICTURE", "a sub-folder is scanned (the zip extracted into its own folder)");
            Ok(S("really-a-png.jpg").Format == "PNG" && S("shot.webp").Format == "WEBP" && S("noext").Kind == "PICTURE", "the content decides the format (PNG named .jpg, WEBP, no extension)");
            Ok(S("fake.jpg").Kind == "" && S("fake.jpg").Why.Contains("content is not") && S("empty.png").Why == "empty file" && S("notes.txt").Why == "not a picture or a video" && S("clip.mp4").Kind == "VIDEO", "every skip has its reason (" + S("fake.jpg").Why + ")");
            Ok(KeystoneLookFiles.List(dir).Count == 8 && KeystoneLookFiles.KindOf(Path.Combine(dir, "lake.jpg.jfif")) == "PICTURE" && KeystoneLookFiles.KindOf(Path.Combine(dir, "fake.jpg")) == "", "List = pictures + videos only (" + KeystoneLookFiles.List(dir).Count + ")");
            Ok(KeystoneLookFiles.Problem(Path.Combine(dir, "fake.jpg")).StartsWith("not a picture") && KeystoneLookFiles.Problem(Path.Combine(dir, "lake.jpg.jfif")) == "", "a picked file's problem in one line");
            Ok(KeystoneLookFiles.Scan(Path.Combine(dir, "nope")).Count == 0 && KeystoneLookFiles.Scan("").Count == 0, "a missing folder = nothing, no exception");
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine(fails == 0 ? "SCENE PACK TESTS PASSED (" + n + " checks)" : "SCENE PACK TESTS FAILED " + fails + " of " + n);
        return fails == 0 ? 0 : 1;
    }
    static string Line(KeystoneLookCombo c) { return c.Name + " bg " + c.Bg + " up " + c.Up + " down " + c.Down + " text " + c.Text + " grade " + c.Grade.Write() + " pct " + c.Pct; }
    static void Grade(string[] a)
    {
        var px = File.ReadAllBytes(a[1]); int w = int.Parse(a[2]), h = int.Parse(a[3]); var c = KeystoneLookCombos.Find(a[4]); var g = c.Grade;
        var r = g.CropRect(w, h); var cp = KeystoneLookGrade.Crop(px, w, h, w * 4, r); g.Apply(cp, r[2], r[3], r[2] * 4, w / 1920.0); File.WriteAllBytes(a[5], cp);
        var s = KeystoneLookStats.Measure(cp, r[2], r[3], r[2] * 4); var k = KeystoneLookCombos.CheckOn(c, s.Mean, s.Hi, s.Lo, c.Pct);
        Console.WriteLine(r[2] + " " + r[3] + " " + s.Mean + " " + s.Hi + " " + s.Lo + " " + (k.Ok ? "OK" : "BAD " + k.Why) + " up " + Math.Min(Math.Min(k.UpMean, k.UpHi), k.UpLo).ToString("0.0", IC) + " down " + Math.Min(Math.Min(k.DownMean, k.DownHi), k.DownLo).ToString("0.0", IC));
    }
}
