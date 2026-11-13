// 09i: 🧠 AI CO-PILOT — the context builder (#43), the schema parse + validation, the rate limit / $ cap, refusals, no execution without the click,
// AI AUTO refused on MT5 accounts, the key never in the clear / in the journal / in the context. A FAKE advisor: no real API call in the tests.
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text; using System.Text.Json; using System.Threading; using System.Threading.Tasks; using KeystoneMT5;
public static class NineITests
{
    static int fails; static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    static string Data() { var d = AppContext.BaseDirectory; for (int i = 0; i < 6 && !Directory.Exists(Path.Combine(d, "data")); i++) d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar)); return Path.Combine(d, "data"); }
    sealed class Fake : IAiAdvisor
    {
        public Func<string, string, AiReply> Answer; public int Calls; public string LastUser = "";
        public string ModelName { get { return "claude-opus-5-5"; } }
        public Task<AiReply> AskAsync(string system, string user, CancellationToken ct) { Calls++; LastUser = user; return Task.FromResult(Answer(system, user)); }
        public Task<string> TestAsync(CancellationToken ct) { return Task.FromResult(""); }
    }
    static AiInput Case43()
    {   // #43 just before his SELL 0.65 (10 Nov 01:36:31): the 5 legs at 4139.31, total −$266 (his screen), target $16, balance $7,954.49 — DEMO
        var bars = File.ReadAllLines(Path.Combine(Data(), "xau-m1-1105-1110.csv")).Skip(1).Select(l => l.Split(',')).Select(x => new Mt5Bar { Time = DateTime.ParseExact(x[0], "yyyy-MM-dd HH:mm:ss", IC), O = double.Parse(x[1], IC), H = double.Parse(x[2], IC), L = double.Parse(x[3], IC), C = double.Parse(x[4], IC), V = long.Parse(x[5], IC) }).ToList();
        var a = new AiInput { Kind = "DEMO", Symbol = "XAUUSD", Balance = 7954.49, Equity = 7688.49, MarginLevel = 2150, Spread = 0.22, SwapLong = -62.4, SwapShort = 28.1, Bid = 4139.31, Ask = 4139.53, Banked = 0, Total = -266, Target = 16, BePx = 4142.64, TargetPx = 4144.64, Now = new DateTime(2026, 11, 10, 1, 36, 31), M1 = bars,
            Settings = "DEFENDER · entry 0.08 · target $16 · hedge lead 0.08 · compress OFF", Radar = "⚠ DANGER 44 % (…)", RadarNote = "walk-forward AUC 0.69; the % = the observed band rate", Trend = "📉 TREND DOWN 50 · EMA9<21 M1, broke 4141.37, below VWAP", WhatIf = "🤖 WHAT IF: no manual interventions replayed yet." };
        foreach (var l in new[] { Tuple.Create(1L, "BUY", 0.08, 4142.14), Tuple.Create(2L, "SELL", 0.16, 4137.38), Tuple.Create(3L, "BUY", 0.16, 4145.30), Tuple.Create(4L, "SELL", 0.16, 4137.03), Tuple.Create(5L, "BUY", 0.16, 4145.90) })
            a.Legs.Add(new AiLeg { Ticket = l.Item1, Side = l.Item2, Lots = l.Item3, Open = l.Item4, Pl = Math.Round((4139.31 - l.Item4) * (l.Item2 == "BUY" ? 1 : -1) * l.Item3 * 100, 2) });
        a.Similar.Add("2026-01-23 21:47 · hedge 3 · went -18.4× the target deep · 2.1 d · RESCUE BE"); a.News.Add("Tue 08:30 NY USD Core CPI m/m");
        return a;
    }
    static string Json(object o) { return JsonSerializer.Serialize(o); }
    sealed class FakeHttp : System.Net.Http.HttpMessageHandler
    {
        public string Reply = "", Body = "", Headers = "", Url = ""; public int Status; public bool Throw;
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage rq, CancellationToken ct)
        {
            Url = rq.RequestUri.ToString(); Headers = string.Join("\n", rq.Headers.Select(x => x.Key + ": " + string.Join(",", x.Value))); Body = rq.Content != null ? await rq.Content.ReadAsStringAsync() : "";
            if (Throw) throw new System.Net.Http.HttpRequestException("connection refused");
            if (Status != 0) return new System.Net.Http.HttpResponseMessage((System.Net.HttpStatusCode)Status) { Content = new System.Net.Http.StringContent("{\"type\":\"error\",\"error\":{\"type\":\"x\",\"message\":\"fake " + Status + "\"}}", Encoding.UTF8, "application/json") };
            var m = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(Reply, Encoding.UTF8, "text/event-stream") }; return m;
        }
    }
    static string Sse(string text, string stop, object details)
    {
        Func<string, object, string> ev = (n, o) => "event: " + n + "\ndata: " + Json(o) + "\n\n";
        var sb = new StringBuilder();
        sb.Append(ev("message_start", new { type = "message_start", message = new { id = "msg_1", type = "message", role = "assistant", model = "claude-opus-5-5", content = new object[0], stop_reason = (string)null, stop_sequence = (string)null, usage = new { input_tokens = 3000, output_tokens = 1, cache_read_input_tokens = 980, cache_creation_input_tokens = 0 } } }));
        if (text != "")
        {
            sb.Append(ev("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } }));
            sb.Append(ev("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text } }));
            sb.Append(ev("content_block_stop", new { type = "content_block_stop", index = 0 }));
        }
        sb.Append(ev("message_delta", details == null ? (object)new { type = "message_delta", delta = new { stop_reason = stop, stop_sequence = (string)null }, usage = new { output_tokens = 700 } } : new { type = "message_delta", delta = new { stop_reason = stop, stop_sequence = (string)null, stop_details = details }, usage = new { output_tokens = 700 } }));
        sb.Append(ev("message_stop", new { type = "message_stop" }));
        return sb.ToString();
    }
    public static int Main12()
    {
        var a = Case43(); var ctx = AiCopilot.Context(a); var ctx2 = AiCopilot.Context(Case43());
        using (var d = JsonDocument.Parse(ctx))
        {
            var r = d.RootElement; var b = r.GetProperty("basket"); var c = r.GetProperty("candles_ohlc");
            Check(r.GetProperty("account").GetProperty("kind").GetString() == "DEMO" && b.GetProperty("legs").GetArrayLength() == 5 && Math.Abs(b.GetProperty("net_lots").GetDouble() - 0.08) < 1e-9 && Math.Abs(b.GetProperty("gross_lots").GetDouble() - 0.72) < 1e-9
                && b.GetProperty("total").GetDouble() == -266 && b.GetProperty("breakeven_price").GetDouble() == 4142.64 && b.GetProperty("legs")[0].GetProperty("open_pl").GetDouble() == -22.64
                && c.GetProperty("m1_last120").GetArrayLength() == 120 && c.GetProperty("m5_last96").GetArrayLength() == 96 && c.GetProperty("m15_last96").GetArrayLength() == 96 && c.GetProperty("h1_last72").GetArrayLength() == 58
                && c.GetProperty("m1_last120")[119][0].GetString() == "11-10 01:35" && r.GetProperty("symbol").GetProperty("swap_long_per_lot").GetDouble() == -62.4 && r.GetProperty("indicators").GetProperty("trend_reading").GetString().StartsWith("📉")
                && ctx == ctx2 && AiCopilot.Hash(ctx) == AiCopilot.Hash(ctx2) && AiCopilot.Hash(ctx).Length == 12,
                "🧠 context of #43 (01:36:31): DEMO, 5 legs, net +0.08 / gross 0.72, total −$266, breakeven 4142.64, the 1st leg −$22.64; M1 120 (the last FINISHED one 01:35) / M5 96 / M15 96 / H1 58 (all the export holds; 72 when there are) candles, swap, the trend; the same input = the same bytes + hash (" + AiCopilot.Hash(ctx) + ")", ctx.Substring(0, Math.Min(300, ctx.Length)));
        }
        long bytes = Encoding.UTF8.GetByteCount(ctx), sysBytes = Encoding.UTF8.GetByteCount(AiCopilot.System); double tokIn = bytes / 3.0, tokSys = sysBytes / 4.0;
        double first = AiCopilot.Cost((long)tokIn, 1500, 0, (long)tokSys), next = AiCopilot.Cost((long)tokIn, 1500, (long)tokSys, 0);
        Console.WriteLine("  🧠 cost estimate: context " + bytes + " bytes ≈ " + tokIn.ToString("0") + " tokens + the cached system ≈ " + tokSys.ToString("0") + " tokens, ~1,500 output (thinking at effort high + the JSON) → the first call ≈ $" + first.ToString("0.000", IC) + ", later ones ≈ $" + next.ToString("0.000", IC) + " (output dominates; thinking can raise it to ~$0.10–0.20)");
        Check(bytes > 5000 && bytes < 40000 && first < 0.1, "🧠 the context stays compact: " + bytes + " bytes (≈ " + tokIn.ToString("0") + " input tokens) → ≈ $" + next.ToString("0.000", IC) + " a call with ~1,500 output tokens", bytes + "");
        // ---- the schema + parse + validation
        var schema = Json(AiCopilot.Schema());
        Func<object, AiAdvice> P = o => AiCopilot.Parse(Json(o), a, 1.0);
        Func<string, double, string, double, double, object> order = (side, lots, type, lvl, stop) => new { side, lots, entry_type = type, entry_level = lvl, stop, exit = "the basket's breakeven" };
        var ok = P(new { action = "recovery_trade", close_tickets = new long[0], order = order("SELL", 0.51, "MARKET", 0, 4144.28), confidence = 0.4, risk_usd = -253, expected_usd = 282, reason = "Trend down. Debt $266. One. Two.", change_my_mind = "a close above 4144.28" });
        var badT = P(new { action = "close_legs", close_tickets = new long[] { 9 }, order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.5, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
        var badA = P(new { action = "buy_more", close_tickets = new long[0], order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.5, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
        var big = P(new { action = "add_leg", close_tickets = new long[0], order = order("BUY", 3.0, "MARKET", 0, 4130), confidence = 0.5, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
        var wrongStop = P(new { action = "recovery_trade", close_tickets = new long[0], order = order("SELL", 0.5, "MARKET", 0, 4130), confidence = 0.5, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
        var lockBad = P(new { action = "lock_net_zero", close_tickets = new long[0], order = order("SELL", 0.5, "MARKET", 0, 0), confidence = 0.5, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
        var lockOk = P(new { action = "lock_net_zero", close_tickets = new long[0], order = order("SELL", 0.08, "MARKET", 0, 0), confidence = 0.5, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
        var junk = AiCopilot.Parse("not json", a, 1.0); var conf = P(new { action = "hold", close_tickets = new long[0], order = order("NONE", 0, "MARKET", 0, 0), confidence = 1.5, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
        Check(schema.Contains("\"enum\":[\"hold\",\"close_all\",\"close_legs\",\"lock_net_zero\",\"recovery_trade\",\"add_leg\",\"reduce_net\"]") && schema.Contains("\"additionalProperties\":false") && schema.Contains("change_my_mind")
            && ok.Valid && ok.Action == "recovery_trade" && Math.Abs(ok.Order.Lots - 0.51) < 1e-9 && ok.Reason == "Trend down. Debt $266. One." && ok.Summary.StartsWith("🧠 RECOVERY SELL 0.51")
            && !badT.Valid && badT.Problems[0].Contains("#9") && !badA.Valid && !big.Valid && big.Problems.Any(p => p.Contains("lots")) && !wrongStop.Valid && wrongStop.Problems.Any(p => p.Contains("wrong side")) && !lockBad.Valid && lockOk.Valid && !junk.Valid && !conf.Valid,
            "🧠 the schema (7 actions, strict) + validation: a valid recovery SELL 0.51 (reason cut to 3 sentences); a ticket not in the basket, an unknown action, lots over the cap, a stop on the wrong side, a wrong lock size, bad JSON, confidence 1.5 → refused", string.Join(" | ", new[] { ok, badT, badA, big, wrongStop, lockBad, junk, conf }.Select(x => string.Join(";", x.Problems))));
        // ---- the rate limit + the $ cap (persisted)
        var dir = Path.Combine(Path.GetTempPath(), "spx09i-" + Guid.NewGuid().ToString("N")); var bf = Path.Combine(dir, "ai-spend.txt"); var t0 = new DateTime(2026, 11, 10, 2, 0, 0);
        var bud = new AiBudget(bf) { PerBasketMinutes = 10, PerHour = 12, MonthlyCap = 20 };
        string c1 = bud.Check("#43", t0, true); bud.Record("#43", t0, 0.05); string c2 = bud.Check("#43", t0.AddMinutes(5), true), c3 = bud.Check("#43", t0.AddMinutes(5), false), c4 = bud.Check("#43", t0.AddMinutes(11), true);
        for (int i = 0; i < 11; i++) bud.Record("#x" + i, t0.AddMinutes(20 + i), 0.05); string c5 = bud.Check("#99", t0.AddMinutes(40), false);
        var bud2 = new AiBudget(bf) { MonthlyCap = 0.6 }; string c6 = bud2.Check("#99", t0.AddHours(3), false); var bud3 = new AiBudget(bf) { MonthlyCap = 20 };
        Check(c1 == "" && c2.Contains("one automatic call per 10 min") && c3 == "" && c4 == "" && c5.Contains("12 calls in the last hour") && c6.Contains("monthly cap") && Math.Abs(bud3.SpentThisMonth(t0) - 0.60) < 1e-9 && bud3.SpentThisMonth(t0.AddMonths(1)) == 0,
            "🧠 limits: one automatic call per basket per 10 min (your 🧠 ASK AI click is not held by it), 12 calls an hour, the monthly $ cap — saved, read back after a restart, a new month starts at $0", c1 + "|" + c2 + "|" + c3 + "|" + c4 + "|" + c5 + "|" + c6);
        // ---- the coordinator with a fake advisor: refusal, error, a valid answer — and NOTHING is executed by asking
        var fake = new Fake(); var co = new AiCoordinator { Advisor = fake, Budget = new AiBudget(Path.Combine(dir, "b2.txt")), JournalFile = Path.Combine(dir, "journal.txt"), MaxLots = 1 };
        fake.Answer = (s, u) => new AiReply { StopReason = "refusal", RefusalCategory = "cyber", RefusalExplanation = "declined", InputTokens = 9000, OutputTokens = 10 };
        var ref1 = co.AskAsync(a, "#43", false, 43, CancellationToken.None).Result;
        fake.Answer = (s, u) => new AiReply { Error = "timed out (no answer within 5 minutes)" }; var err = co.AskAsync(a, "#43", false, 43, CancellationToken.None).Result;
        fake.Answer = (s, u) => new AiReply { StopReason = "end_turn", Text = Json(new { action = "close_all", close_tickets = new long[0], order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.62, risk_usd = -266, expected_usd = -266, reason = "Cut it.", change_my_mind = "a break above 4142" }), InputTokens = 2000, CacheRead = 1200, OutputTokens = 900, Model = "claude-opus-5-5" };
        var good = co.AskAsync(a, "#43", false, 43, CancellationToken.None).Result;
        var noKey = new AiCoordinator { Advisor = null }; var nk = noKey.AskAsync(a, "#43", false, 43, CancellationToken.None).Result;
        var cmds = AiCoordinator.Commands(good, a); var cmdsRef = AiCoordinator.Commands(ref1, a);
        Check(!ref1.Valid && ref1.Problems[0].Contains("declined (cyber)") && cmdsRef.Count == 0 && !err.Valid && err.Problems[0].Contains("timed out") && good.Valid && good.Action == "close_all" && co.Journal.Count == 3 && co.Journal.All(j => !j.Executed)
            && nk == null && noKey.LastStatus.Contains("no Anthropic API key") && fake.Calls == 3 && cmds.Count == 1 && cmds[0].Item1 == "CLOSEALL" && fake.LastUser.Contains("\"total\":-266") && co.Summary(a.Now).Contains("3 advice (0 executed)"),
            "🧠 a refusal (cyber) → shown, nothing to execute; a time-out → shown; a valid CLOSE ALL → shown + journaled; asking executes NOTHING (the commands exist only for your click: CLOSEALL); no key → the feature is off", string.Join(" | ", co.Journal.Select(j => j.Summary)));
        {   // the mapping of each action to the normal order path
            Func<object, List<Tuple<string, object[], string>>> M = o => AiCoordinator.Commands(P(o), a);
            var rec = M(new { action = "recovery_trade", close_tickets = new long[0], order = order("SELL", 0.51, "MARKET", 0, 4144.28), confidence = 0.4, risk_usd = -253, expected_usd = 282, reason = "x", change_my_mind = "y" });
            var cl = M(new { action = "close_legs", close_tickets = new long[] { 3, 5 }, order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.4, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
            var red = M(new { action = "reduce_net", close_tickets = new long[0], order = order("SELL", 0.08, "MARKET", 0, 0), confidence = 0.4, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
            var hold = M(new { action = "hold", close_tickets = new long[0], order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.4, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
            Check(rec.Count == 1 && rec[0].Item1 == "ORDER" && (string)rec[0].Item2[1] == "SELL" && (double)rec[0].Item2[2] == 0.51 && (double)rec[0].Item2[4] == 4144.28 && cl.Count == 2 && cl.All(x => x.Item1 == "CLOSE") && red.Count == 1 && (long)red[0].Item2[0] == 5 && (double)red[0].Item2[1] == 0.08 && hold.Count == 0,
                "🧠 the click's commands: RECOVERY → ORDER SELL 0.51 stop 4144.28; CLOSE LEGS → CLOSE #3, #5; REDUCE NET → part of the newest BUY (#5 0.08); HOLD → nothing", "");
        }
        Check(!AiCoordinator.AutoAllowed(false) && AiCoordinator.AutoAllowed(true) && AiCoordinator.AutoNo.Contains("MT5 account"), "🧠 AI AUTO: refused on an MT5 account (REAL / PROP / MT5 demo), allowed on a simulated one", "");
        {   // the key: never in the clear on disk, never in the journal / the context, masked on screen
            var key = "sk-ant-api03-SECRETSECRETSECRET-abcd"; var kf = Path.Combine(dir, "ai-key.bin");
            Func<byte[], byte[]> xor = bs => bs.Select((x, i) => (byte)(x ^ (0x5A + i % 7))).ToArray(); var ks = new AiKeyStore(kf, xor, xor); ks.Save(key);
            var raw = File.ReadAllBytes(kf); var all = string.Join("\n", Directory.GetFiles(dir).Where(f => f != kf).Select(File.ReadAllText));
            Check(ks.Load() == key && !Encoding.UTF8.GetString(raw).Contains("SECRET") && !all.Contains("SECRET") && !ctx.Contains("sk-ant") && AiKeyStore.Mask(key) == "sk-ant-…abcd" && !co.Journal.Any(j => j.Save().Contains("SECRET")),
                "🧠 the API key: stored encrypted (never in the clear), never in the journal / spend files / the context sent, masked as " + AiKeyStore.Mask(key), AiKeyStore.Mask(key));
            ks.Delete();
        }
        {   // the REAL SDK client (ClaudeAdvisor) on a fake transport — no network: the request it builds, the streamed answer, a refusal, an error
            var key = "sk-ant-api03-TESTKEY-0000"; var h = new FakeHttp(); var adv = new ClaudeAdvisor(key, new System.Net.Http.HttpClient(h));
            h.Reply = Sse(Json(new { action = "hold", close_tickets = new long[0], order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.5, risk_usd = 0, expected_usd = 0, reason = "Wait.", change_my_mind = "a break" }), "end_turn", null);
            var r1 = adv.AskAsync(AiCopilot.System, "ctx", CancellationToken.None).Result; var body = h.Body; var hdr = h.Headers;
            JsonElement req; using (var d = JsonDocument.Parse(body)) req = d.RootElement.Clone();
            bool reqOk = req.GetProperty("model").GetString() == "claude-opus-5-5" && req.GetProperty("thinking").GetProperty("type").GetString() == "adaptive" && req.GetProperty("output_config").GetProperty("effort").GetString() == "high"
                && req.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString() == "json_schema" && req.GetProperty("system")[0].GetProperty("cache_control").GetProperty("type").GetString() == "ephemeral"
                && req.GetProperty("fallbacks").GetString() == "default" && req.GetProperty("stream").GetBoolean() && hdr.Contains("server-side-fallback-2026-07-01") && h.Url.StartsWith("https://api.anthropic.com/v1/messages") && hdr.Contains(key) && !body.Contains(key);
            h.Reply = Sse("", "refusal", new { type = "refusal", category = "cyber", explanation = "declined" }); var r2 = adv.AskAsync(AiCopilot.System, "ctx", CancellationToken.None).Result;
            h.Status = 429; var r3 = adv.AskAsync(AiCopilot.System, "ctx", CancellationToken.None).Result; h.Status = 401; var r4 = adv.AskAsync(AiCopilot.System, "ctx", CancellationToken.None).Result; var t4 = adv.TestAsync(CancellationToken.None).Result;
            h.Status = 0; h.Throw = true; var r5 = adv.AskAsync(AiCopilot.System, "ctx", CancellationToken.None).Result;
            Console.WriteLine("  🧠 SDK: " + r1.StopReason + " " + r1.InputTokens + "/" + r1.CacheRead + "/" + r1.OutputTokens + " · refusal \"" + r2.StopReason + "\" " + r2.RefusalCategory + " · 429 \"" + r3.Error + "\" · 401 \"" + r4.Error + "\" · test \"" + t4 + "\" · down \"" + r5.Error + "\"");
            Check(reqOk && r1.Error == "" && r1.StopReason == "end_turn" && AiCopilot.Parse(r1.Text, a, 1).Valid && r1.InputTokens == 3000 && r1.CacheRead == 980 && r1.OutputTokens == 700 && !r1.FellBack
                && r2.StopReason == "refusal" && r2.RefusalCategory == "cyber" && r3.Error.Contains("rate limited") && r4.Error.Contains("401") && t4.Contains("401") && r5.Error.Contains("no connection") && !(r3.Error + r4.Error + r5.Error + t4).Contains(key),
                "🧠 the real SDK client on a fake transport (no network): model claude-opus-5-5, adaptive thinking, effort HIGH, a JSON-schema output, the system prompt cached, fallbacks \"default\" + the server-side-fallback beta, streamed; usage read; a refusal (cyber) seen; 429 / 401 / no connection → plain errors; the key only in the API header, never in a message", body.Substring(0, Math.Min(400, body.Length)) + " | " + r2.StopReason + " " + r3.Error + " " + r4.Error + " " + r5.Error);
        }
        {   // 🧠 the what-if of an advice: the basket right after following it (the automation alone replays it), compared with what happened
            var t = new WhatIfCase { N = 43, Start = a.Now.AddHours(-2), Balance = 7954.49, Settings = "sym=XAUUSD;target=16", Target = 16, MaxDd = -120 };
            Func<object, WhatIfCase> F = o => AiCoordinator.Followed(P(o), a, t);
            var all = F(new { action = "close_all", close_tickets = new long[0], order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.6, risk_usd = -266, expected_usd = -266, reason = "x", change_my_mind = "y" });
            var cl = F(new { action = "close_legs", close_tickets = new long[] { 3, 5 }, order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.4, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
            var rec = F(new { action = "recovery_trade", close_tickets = new long[0], order = order("SELL", 0.51, "MARKET", 0, 4144.28), confidence = 0.4, risk_usd = -253, expected_usd = 282, reason = "x", change_my_mind = "y" });
            var hold = F(new { action = "hold", close_tickets = new long[0], order = order("NONE", 0, "MARKET", 0, 0), confidence = 0.4, risk_usd = 0, expected_usd = 0, reason = "x", change_my_mind = "y" });
            double pl35 = a.Legs.Where(l => l.Ticket == 3 || l.Ticket == 5).Sum(l => l.Pl); var back = WhatIfCase.Load(rec.Save());
            Check(all.Done && all.Result == -266 && all.Legs.Count == 0 && cl.Legs.Count == 3 && Math.Abs(cl.Banked - pl35) < 1e-9 && !cl.Done && rec.Legs.Count == 6 && rec.Legs.Last().Side == "SELL" && rec.Legs.Last().Open == 4139.31 && Math.Abs(rec.Legs.Last().Lots - 0.51) < 1e-9
                && rec.What.StartsWith("🧠 RECOVERY SELL 0.51") && hold == null && back != null && back.Legs.Count == 6,
                "🧠 the advice's what-if: CLOSE ALL = −$266 at once; CLOSE #3 + #5 = " + pl35.ToString("0.00", IC) + " banked, 3 legs replayed; RECOVERY = a SELL 0.51 at the bid 4139.31 added, then the automation alone (in-memory, never MT5) vs what really happened; HOLD = nothing to compare", "");
        }
        {   // 09j: the HEDGE tab's layout limits, the compact banner lines, the recovery stub, the 🏁 RACE comparison + scoreboard
            bool lay = true; foreach (var wh in new[] { Tuple.Create(1600.0, 1000.0), Tuple.Create(1920.0, 1080.0), Tuple.Create(1280.0, 800.0) })
            {   // the top part ≤ 45 % of the tab; the banner ≥ 520 px whatever the buttons need; the tab strip + body keep ≥ 50 % of the tab (≤ 5 % for the strip)
                double top = HedgeLayout.TopMax(wh.Item2), btn = HedgeLayout.ButtonsMax(wh.Item1); lay &= top <= 0.45 * wh.Item2 + 1e-9 && wh.Item2 - top - 0.05 * wh.Item2 >= 0.5 * wh.Item2 && wh.Item1 - btn >= HedgeLayout.BannerMin;
            }
            var gt = "🎯 EXIT AT BREAKEVEN (+$0.5) 4023.02 (+13.0) — the basket was −$1079, open 2 h, a manual trade was added · total now −$302.25"; var sh = HedgeLayout.Short(gt);
            var tr = new TrendState { Dir = 1, Strength = 75, SwingHi = 4009, SwingLo = 4004, Close = 4010, Atr1 = 1, Atr5 = 3 };
            var stub = TrendDetect.Recovery(-1100, 32, 0.25, 4010, 100, 7974, 0.07, 1.5, 633, 300, tr); var rok = TrendDetect.Recovery(-325, 32, 0.25, 4010, 100, 7974, 0.07, 1.5, 633, 300, tr);
            var board = RaceBoard.Rank(new[] { new RaceScore { Key = "MINE", Total = -40 }, new RaceScore { Key = "REC", Total = 25 }, new RaceScore { Key = "AOP", Total = -90 }, new RaceScore { Key = "TAH", Total = 5 } });
            var mine9 = AutoSettings.Model("DEFENDER", new AutoSettings { Symbol = "XAUUSD" }, 8000); var rec = RaceLanes.For("REC", mine9, 8000);
            Check(lay && sh == "🎯 EXIT AT BREAKEVEN (+$0.5) 4023.02 (+13.0) · total now −$302.25" && !HedgeLayout.IsLong("a\nb\nc\nd") && HedgeLayout.IsLong("a\nb\nc\nd\ne")
                && stub.Dir == 0 && stub.Text.Contains("no useful trade") && stub.Text.Contains("net cap 0.56") && rok.Dir == 1 && rok.Lots >= 0.01 && rok.Detail.Contains("breakeven")
                && string.Join(",", board.Select(b => b.Key)) == "REC,TAH,MINE,AOP" && board[0].Leader && !board[1].Leader && board[0].Vs == 65 && board.First(b => b.Key == "MINE").Vs == 0 && board.First(b => b.Key == "AOP").Vs == -50
                && string.Join(",", RaceLanes.Compare) == "MINE,RUN,REC,AOP,TAH,SW2,CNS" /* 09o: + 🪜 SW2, 09r: + 🧭 CNS */ && rec.CompressMode == "ALWAYS" && rec.BeLock && rec.BeClose && mine9.BeClose /* 09q: DEFENDER closes too */ && RaceLanes.For("AOP", mine9, 8000).AddOnBreak,
                "📐 09j: the HEDGE top part ≤ 45 % of the tab and the banner ≥ 520 px (1280 / 1600 / 1920), the guard line short (" + sh + "); a RECOVERY the net cap squeezes to a stub is not offered (\"" + (stub.Text.Length > 60 ? stub.Text.Substring(0, 60) : stub.Text) + "…\"); 🏁 ▶ COMPARE = MINE, MINE + RECOMMENDED (compress ALWAYS + BE close), ADD ON BREAK + PROTECT, TAH; the scoreboard sorted by total, the leader marked, vs MY SETTINGS", sh + " | " + stub.Text + " | " + rok.Text + " | " + string.Join(",", board.Select(b => b.Key + b.Vs)));
        }
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fails == 0 ? "ALL 09i TESTS PASSED" : fails + " 09i TEST(S) FAILED");
        return fails;
    }
}
