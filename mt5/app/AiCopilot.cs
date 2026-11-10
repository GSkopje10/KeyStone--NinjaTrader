// SpasiX — 🧠 AI CO-PILOT (09i, the user's request): a Claude advisor for baskets that get out of control. This file holds everything that is
// NOT the network call (that is AiClient.cs, the official Anthropic C# SDK): the context sent (compact JSON), the answer's schema, its parsing +
// validation, the rate limit and the monthly $ cap, the journal and the key storage. The advice is NEVER executed by itself on an MT5 account:
// a click (that asks first) sends it through the normal order path; "🧠 AI AUTO" exists only on simulated accounts (refused on MT5, like TREND STACK).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeystoneMT5
{
    // ---- the model's reply as the app sees it (the SDK client fills it; tests use a fake)
    public sealed class AiReply { public string Text = "", StopReason = "", RefusalCategory = "", RefusalExplanation = "", Model = "", Error = ""; public long InputTokens, OutputTokens, CacheRead, CacheWrite; public bool FellBack; }
    public interface IAiAdvisor { Task<AiReply> AskAsync(string system, string user, CancellationToken ct); Task<string> TestAsync(CancellationToken ct); string ModelName { get; } }   // TestAsync: "" = the key works and the model exists, else why not

    public sealed class AiLeg { public long Ticket; public string Side = "BUY"; public double Lots, Open, Pl; public DateTime Time; }
    public sealed class AiInput
    {
        public string Kind = "DEMO", Symbol = "XAUUSD", Settings = "", Radar = "", RadarNote = "", Trend = "", WhatIf = ""; public double Balance, Equity, MarginLevel, Spread, SwapLong, SwapShort, Banked, Total, BePx = double.NaN, TargetPx = double.NaN, Target, Bid, Ask, Net, Gross;
        public List<AiLeg> Legs = new List<AiLeg>(); public List<string> Similar = new List<string>(), News = new List<string>(); public List<Mt5Bar> M1 = new List<Mt5Bar>(); public DateTime Now;
    }
    public sealed class AiOrder { public string Side = "NONE", EntryType = "MARKET", Exit = ""; public double Lots, EntryLevel, Stop; }
    public sealed class AiAdvice
    {
        public string Action = "hold", Reason = "", ChangeMind = ""; public List<long> CloseTickets = new List<long>(); public AiOrder Order = new AiOrder(); public double Confidence, RiskUsd, ExpectedUsd;
        public DateTime At; public string Hash = "", Raw = ""; public List<string> Problems = new List<string>(); public bool Valid { get { return Problems.Count == 0; } }
        public string Summary { get { var o = Order; string what = Action == "close_all" ? "CLOSE ALL" : Action == "close_legs" ? "CLOSE #" + string.Join(", #", CloseTickets) : Action == "lock_net_zero" ? "LOCK at net 0 (" + o.Side + " " + o.Lots.ToString("0.##", AiCopilot.IC) + ")" : Action == "hold" ? "HOLD" : (Action == "recovery_trade" ? "RECOVERY " : Action == "add_leg" ? "ADD " : "REDUCE ") + o.Side + " " + o.Lots.ToString("0.##", AiCopilot.IC) + (o.EntryType != "MARKET" ? " " + o.EntryType + " @ " + o.EntryLevel.ToString("0.00", AiCopilot.IC) : "") + (o.Stop > 0 ? " stop " + o.Stop.ToString("0.00", AiCopilot.IC) : ""); return "🧠 " + what + " · confidence " + (100 * Confidence).ToString("0", AiCopilot.IC) + " % · if wrong −$" + Math.Abs(RiskUsd).ToString("0", AiCopilot.IC) + " · if right +$" + Math.Abs(ExpectedUsd).ToString("0", AiCopilot.IC); } }
    }

    public static class AiCopilot
    {
        public static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public const string Model = "claude-opus-5-5";
        public static readonly string[] Actions = { "hold", "close_all", "close_legs", "lock_net_zero", "recovery_trade", "add_leg", "reduce_net" };
        // $ per million tokens (Claude Opus 5.5): input 4, output 20, cache read 0.20, cache write (5 min) 5
        public static double Cost(long inTok, long outTok, long cacheRead, long cacheWrite) { return (inTok * 4.0 + outTok * 20.0 + cacheRead * 0.20 + cacheWrite * 5.0) / 1e6; }

        // the stable part (cached): who it is, the facts of the research, how to answer
        public const string System =
            "You are the risk co-pilot inside SpasiX, a hedging-basket trading app for MetaTrader 5 (mostly gold, XAUUSD). A basket is a set of BUY and SELL legs on one symbol, " +
            "managed together: it closes when its TOTAL (open profit/loss + the legs already closed in this iteration) reaches its target. You are called when a basket looks like it is " +
            "getting out of control. You receive the account, the basket's legs, its total, the breakeven and target prices, the danger radar, similar past baskets, recent candles, " +
            "indicators, the short-term trend reading, upcoming news and how past manual interventions compared with the automation.\n\n" +
            "Facts from the app's own 6-year research on 1-minute gold (state them when they matter, never contradict them):\n" +
            "- No tested rule showed a reliable direction edge; trend detectors, setups and recovery stacking all LOST over six years. Treat any directional idea as uncertain.\n" +
            "- Adding size to recover a loss is a martingale: it can finish the basket sooner, but a wrong move gets much more expensive. Always state the $ risk if wrong.\n" +
            "- Swap is charged on every open lot each night (about −62 per lot for longs, +28 for shorts on gold at this broker; Wednesday triple). Closing matched pairs saves it.\n" +
            "- Closing everything at breakeven after a deep basket is often the least-bad outcome; holding a deep basket can take days.\n" +
            "- The danger radar ranks baskets reasonably (AUC about 0.69) but its percentages are calibrated band rates, not forecasts.\n\n" +
            "Give ONE recommendation in the required JSON schema. Choose the smallest action that controls the risk. Use only tickets that exist in the basket. Sizes are in lots, " +
            "prices in the symbol's price. The exit of a new order is the BASKET's breakeven or target (the app closes the whole basket there), not a leg take-profit. " +
            "Keep the reason to at most three plain sentences a trader can act on, and say what would change your mind. If holding is best, say hold.\n\n" +
            "Field guide for the JSON you receive: account.kind is REAL (real money), PROP (a prop-firm account with drawdown rules) or DEMO (simulated). basket.legs are the open positions " +
            "(ticket, side, lots, open price, open_pl in USD after swap and commission); banked_closed_legs is what this iteration already realised; total = open + banked. breakeven_price " +
            "and target_price are where the basket's total would be $0 and +target with the current net lots (absent when the basket is flat-net). usd_per_1_price_per_lot converts a price move " +
            "into dollars per lot. danger_radar.reading is the app's calibrated danger after the last hedge; similar_past_baskets are its nearest historical cases (date, hedges, how deep in " +
            "multiples of the target, how long, how they ended). candles are [time, open, high, low, close] in server time, oldest first, finished candles only. trend_reading is the app's " +
            "short-term detector (EMA 9/21 on M1 and M5, higher highs / lower lows, the swing break, the VWAP side). past_manual_interventions_vs_automation summarises how the user's own " +
            "manual decisions compared with the automation left alone.\n\n" +
            "Action guide: hold = change nothing. close_all = close every leg of the basket at the market. close_legs = close only the listed tickets (e.g. a pair that pays swap, or the leg " +
            "with the worst risk). lock_net_zero = one order that makes buys equal sells (side opposite to the net, lots = |net|), freezing the total. recovery_trade = a new leg in the " +
            "direction you expect, sized so a realistic move brings the basket to breakeven or target; give its protective stop. add_leg = add to an existing side for another reason. " +
            "reduce_net = close part of the net side to cut exposure (side = the opposite of the net, lots ≤ |net|). For every order give the side, lots, MARKET or a STOP / LIMIT level, a " +
            "protective stop (0 = none) and say in exit that the basket closes at its breakeven or target. confidence is 0 to 1; risk_usd is the loss if you are wrong, expected_usd the gain if right.";

        // the answer's JSON schema (structured outputs)
        public static Dictionary<string, JsonElement> Schema()
        {
            var props = new Dictionary<string, object>
            {
                ["action"] = new { type = "string", @enum = Actions },
                ["close_tickets"] = new { type = "array", items = new { type = "integer" } },
                ["order"] = new
                {
                    type = "object", additionalProperties = false, required = new[] { "side", "lots", "entry_type", "entry_level", "stop", "exit" },
                    properties = new { side = new { type = "string", @enum = new[] { "BUY", "SELL", "NONE" } }, lots = new { type = "number" }, entry_type = new { type = "string", @enum = new[] { "MARKET", "STOP", "LIMIT" } }, entry_level = new { type = "number" }, stop = new { type = "number" }, exit = new { type = "string" } }
                },
                ["confidence"] = new { type = "number" }, ["risk_usd"] = new { type = "number" }, ["expected_usd"] = new { type = "number" },
                ["reason"] = new { type = "string" }, ["change_my_mind"] = new { type = "string" },
            };
            return new Dictionary<string, JsonElement>
            {
                ["type"] = JsonSerializer.SerializeToElement("object"), ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
                ["properties"] = JsonSerializer.SerializeToElement(props),
                ["required"] = JsonSerializer.SerializeToElement(new[] { "action", "close_tickets", "order", "confidence", "risk_usd", "expected_usd", "reason", "change_my_mind" }),
            };
        }

        static double R(double v, int d = 2) { return double.IsNaN(v) || double.IsInfinity(v) ? 0 : Math.Round(v, d); }
        public static List<Mt5Bar> Agg(IList<Mt5Bar> m1, int mins)
        {
            var res = new List<Mt5Bar>(); long span = TimeSpan.TicksPerMinute * mins;
            foreach (var g in m1.GroupBy(b => b.Time.Ticks / span)) { var l = g.OrderBy(b => b.Time).ToList(); res.Add(new Mt5Bar { Time = new DateTime(g.Key * span), O = l[0].O, H = l.Max(b => b.H), L = l.Min(b => b.L), C = l[l.Count - 1].C, V = l.Sum(b => b.V) }); }
            return res.OrderBy(b => b.Time).ToList();
        }
        static object Bars(IList<Mt5Bar> b, int n) { return b.Skip(Math.Max(0, b.Count - n)).Select(x => new object[] { x.Time.ToString("MM-dd HH:mm", IC), R(x.O), R(x.H), R(x.L), R(x.C) }).ToList(); }

        // 🧠 the context (compact, structured, deterministic: the same input = the same bytes = the same hash)
        public static string Context(AiInput a)
        {
            var m1 = a.M1.Where(b => b.Time.AddMinutes(1) <= a.Now).ToList(); var m5 = Agg(m1, 5); var m15 = Agg(m1, 15); var h1 = Agg(m1, 60);
            var closes = m1.Select(b => b.C).ToList(); var e9 = TrendDetect.Ema(closes, 9); var e21 = TrendDetect.Ema(closes, 21);
            double vw = TrendDetect.DayVwap(m1, a.Now); double atr5 = m5.Count > 15 ? TrendDetect.Atr(m5, m5.Count - 1, 14) : double.NaN, atrH = h1.Count > 15 ? TrendDetect.Atr(h1, h1.Count - 1, 14) : double.NaN;
            var o = new Dictionary<string, object>
            {
                ["now_server"] = a.Now.ToString("yyyy-MM-dd HH:mm", IC),
                ["account"] = new Dictionary<string, object> { ["kind"] = a.Kind, ["balance"] = R(a.Balance), ["equity"] = R(a.Equity), ["margin_level_pct"] = R(a.MarginLevel, 0) },
                ["symbol"] = new Dictionary<string, object> { ["name"] = a.Symbol, ["bid"] = R(a.Bid), ["ask"] = R(a.Ask), ["spread"] = R(a.Spread), ["swap_long_per_lot"] = R(a.SwapLong), ["swap_short_per_lot"] = R(a.SwapShort), ["usd_per_1_price_per_lot"] = 100 },
                ["basket"] = new Dictionary<string, object>
                {
                    ["legs"] = a.Legs.Select(l => new Dictionary<string, object> { ["ticket"] = l.Ticket, ["side"] = l.Side, ["lots"] = R(l.Lots), ["open"] = R(l.Open), ["open_pl"] = R(l.Pl) }).ToList(),
                    ["net_lots"] = R(a.Legs.Sum(l => (l.Side == "BUY" ? 1 : -1) * l.Lots)), ["gross_lots"] = R(a.Legs.Sum(l => l.Lots)), ["banked_closed_legs"] = R(a.Banked), ["total"] = R(a.Total), ["target"] = R(a.Target),
                    ["breakeven_price"] = R(a.BePx), ["target_price"] = R(a.TargetPx),
                },
                ["settings"] = a.Settings, ["danger_radar"] = new Dictionary<string, object> { ["reading"] = a.Radar, ["calibration_note"] = a.RadarNote }, ["similar_past_baskets"] = a.Similar,
                ["indicators"] = new Dictionary<string, object> { ["vwap"] = R(vw), ["ema9_m1"] = R(e9.Length > 0 ? e9[e9.Length - 1] : double.NaN), ["ema21_m1"] = R(e21.Length > 0 ? e21[e21.Length - 1] : double.NaN), ["atr14_m5"] = R(atr5), ["atr14_h1"] = R(atrH), ["trend_reading"] = a.Trend },
                ["candles_ohlc"] = new Dictionary<string, object> { ["m1_last120"] = Bars(m1, 120), ["m5_last96"] = Bars(m5, 96), ["m15_last96"] = Bars(m15, 96), ["h1_last72"] = Bars(h1, 72) },
                ["news_high_impact"] = a.News, ["past_manual_interventions_vs_automation"] = a.WhatIf,
            };
            return JsonSerializer.Serialize(o, new JsonSerializerOptions { Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });   // UTF-8 as is: fewer bytes / tokens
        }
        public static string Hash(string ctx) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(ctx)).Take(6).Select(b => b.ToString("x2"))); }

        // the answer → advice, validated against the basket (tickets that exist, sizes under the cap, a sane order)
        public static AiAdvice Parse(string json, AiInput a, double maxLots)
        {
            var ad = new AiAdvice { Raw = json ?? "" };
            try
            {
                using (var d = JsonDocument.Parse(json ?? ""))
                {
                    var r = d.RootElement; Func<string, JsonElement?> g = k => { JsonElement e; return r.TryGetProperty(k, out e) ? e : (JsonElement?)null; };
                    ad.Action = g("action")?.GetString() ?? ""; ad.Reason = g("reason")?.GetString() ?? ""; ad.ChangeMind = g("change_my_mind")?.GetString() ?? "";
                    ad.Confidence = g("confidence")?.GetDouble() ?? -1; ad.RiskUsd = g("risk_usd")?.GetDouble() ?? 0; ad.ExpectedUsd = g("expected_usd")?.GetDouble() ?? 0;
                    var ct = g("close_tickets"); if (ct.HasValue && ct.Value.ValueKind == JsonValueKind.Array) foreach (var t in ct.Value.EnumerateArray()) ad.CloseTickets.Add(t.GetInt64());
                    var or = g("order"); if (or.HasValue && or.Value.ValueKind == JsonValueKind.Object)
                    {
                        var x = or.Value; JsonElement e;
                        ad.Order.Side = x.TryGetProperty("side", out e) ? e.GetString() : "NONE"; ad.Order.Lots = x.TryGetProperty("lots", out e) ? e.GetDouble() : 0; ad.Order.EntryType = x.TryGetProperty("entry_type", out e) ? e.GetString() : "MARKET";
                        ad.Order.EntryLevel = x.TryGetProperty("entry_level", out e) ? e.GetDouble() : 0; ad.Order.Stop = x.TryGetProperty("stop", out e) ? e.GetDouble() : 0; ad.Order.Exit = x.TryGetProperty("exit", out e) ? e.GetString() : "";
                    }
                }
            }
            catch (Exception ex) { ad.Problems.Add("not valid JSON: " + ex.Message); return ad; }
            if (!Actions.Contains(ad.Action)) ad.Problems.Add("unknown action \"" + ad.Action + "\"");
            if (ad.Confidence < 0 || ad.Confidence > 1) ad.Problems.Add("confidence must be 0–1");
            var tickets = new HashSet<long>(a.Legs.Select(l => l.Ticket));
            if (ad.Action == "close_legs") { if (ad.CloseTickets.Count == 0) ad.Problems.Add("close_legs without tickets"); foreach (var t in ad.CloseTickets) if (!tickets.Contains(t)) ad.Problems.Add("ticket #" + t + " is not in the basket"); }
            bool needsOrder = ad.Action == "recovery_trade" || ad.Action == "add_leg" || ad.Action == "reduce_net" || ad.Action == "lock_net_zero";
            if (needsOrder)
            {
                if (ad.Order.Side != "BUY" && ad.Order.Side != "SELL") ad.Problems.Add("the order needs a side");
                if (ad.Order.Lots <= 0 || ad.Order.Lots > maxLots + 1e-9) ad.Problems.Add("lots " + ad.Order.Lots.ToString("0.##", IC) + " outside 0–" + maxLots.ToString("0.##", IC));
                if (ad.Action == "lock_net_zero") { double net = a.Legs.Sum(l => (l.Side == "BUY" ? 1 : -1) * l.Lots); if (Math.Abs(ad.Order.Lots - Math.Abs(net)) > 0.005 || (net > 0 ? ad.Order.Side != "SELL" : ad.Order.Side != "BUY")) ad.Problems.Add("lock_net_zero must be " + (net > 0 ? "SELL " : "BUY ") + Math.Abs(net).ToString("0.##", IC)); }
                if (ad.Action == "reduce_net") { double net = a.Legs.Sum(l => (l.Side == "BUY" ? 1 : -1) * l.Lots); if ((net > 0 ? ad.Order.Side != "SELL" : ad.Order.Side != "BUY") || ad.Order.Lots > Math.Abs(net) + 1e-9) ad.Problems.Add("reduce_net must close part of the net side"); }
                if (ad.Order.Stop > 0 && a.Bid > 0 && (ad.Order.Side == "BUY" ? ad.Order.Stop >= a.Bid : ad.Order.Stop <= a.Ask)) ad.Problems.Add("the protective stop is on the wrong side of the price");
            }
            var sentences = ad.Reason.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries); if (sentences.Length > 3) ad.Reason = string.Join(". ", sentences.Take(3)).TrimEnd('.') + ".";
            return ad;
        }
    }

    // ---- the rate limit + the monthly $ cap (persisted: one line per call — month, time, basket, cost)
    public sealed class AiBudget
    {
        public int PerBasketMinutes = 10, PerHour = 12; public double MonthlyCap = 20; readonly string file; readonly List<Tuple<DateTime, string, double>> calls = new List<Tuple<DateTime, string, double>>();
        public AiBudget(string file) { this.file = file; try { if (file != null && File.Exists(file)) foreach (var l in File.ReadAllLines(file)) { var a = l.Split('|'); if (a.Length >= 3) calls.Add(Tuple.Create(new DateTime(long.Parse(a[0], AiCopilot.IC)), a[1], double.Parse(a[2], AiCopilot.IC))); } } catch { } }
        public double SpentThisMonth(DateTime now) { return calls.Where(c => c.Item1.Year == now.Year && c.Item1.Month == now.Month).Sum(c => c.Item3); }
        // auto = a radar / trouble trigger (the per-basket limit applies); a click on 🧠 ASK AI skips the per-basket limit, never the hourly limit or the $ cap
        public string Check(string basket, DateTime now, bool auto)
        {
            if (SpentThisMonth(now) >= MonthlyCap) return "the monthly cap $" + MonthlyCap.ToString("0.##", AiCopilot.IC) + " is reached ($" + SpentThisMonth(now).ToString("0.00", AiCopilot.IC) + " spent)";
            if (calls.Count(c => (now - c.Item1).TotalMinutes < 60) >= PerHour) return PerHour + " calls in the last hour (the limit)";
            if (auto) { var last = calls.Where(c => c.Item2 == basket).Select(c => c.Item1).DefaultIfEmpty(DateTime.MinValue).Max(); if ((now - last).TotalMinutes < PerBasketMinutes) return "this basket was asked " + (now - last).TotalMinutes.ToString("0", AiCopilot.IC) + " min ago (one automatic call per " + PerBasketMinutes + " min)"; }
            return "";
        }
        public void Record(string basket, DateTime now, double cost) { calls.Add(Tuple.Create(now, basket, cost)); try { if (file != null) { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.AppendAllText(file, now.Ticks + "|" + basket + "|" + cost.ToString("R", AiCopilot.IC) + "\n"); } } catch { } }
    }

    // ---- the 🧠 AI JOURNAL (per account): every advice, executed or not, and how it turned out
    public sealed class AiEntry
    {
        public DateTime At; public int Iter; public string Hash = "", Action = "", Summary = "", Reason = "", Model = "", Outcome = ""; public bool Executed; public double Cost, Total;
        public string Save() { return string.Join("|", "AI", At.Ticks, Iter, Hash, Action, Executed ? 1 : 0, Cost.ToString("R", AiCopilot.IC), Total.ToString("R", AiCopilot.IC), B(Summary), B(Reason), Model, B(Outcome)); }
        static string B(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? "")); } static string U(string s) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); } catch { return ""; } }
        public static AiEntry Load(string l) { var a = l.Split('|'); if (a.Length < 12 || a[0] != "AI") return null; try { return new AiEntry { At = new DateTime(long.Parse(a[1], AiCopilot.IC)), Iter = int.Parse(a[2], AiCopilot.IC), Hash = a[3], Action = a[4], Executed = a[5] == "1", Cost = double.Parse(a[6], AiCopilot.IC), Total = double.Parse(a[7], AiCopilot.IC), Summary = U(a[8]), Reason = U(a[9]), Model = a[10], Outcome = U(a[11]) }; } catch { return null; } }
    }

    // ---- the API key: stored encrypted (Windows DPAPI per user — the app passes the protector), never written in clear, never logged or exported
    public sealed class AiKeyStore
    {
        readonly string file; readonly Func<byte[], byte[]> protect, unprotect;
        public AiKeyStore(string file, Func<byte[], byte[]> protect, Func<byte[], byte[]> unprotect) { this.file = file; this.protect = protect; this.unprotect = unprotect; }
        public bool Has { get { return file != null && File.Exists(file); } }
        public void Save(string key) { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllBytes(file, protect(Encoding.UTF8.GetBytes((key ?? "").Trim()))); }
        public string Load() { try { return Has ? Encoding.UTF8.GetString(unprotect(File.ReadAllBytes(file))) : ""; } catch { return ""; } }
        public void Delete() { try { if (Has) File.Delete(file); } catch { } }
        public static string Mask(string key) { return string.IsNullOrEmpty(key) ? "(none)" : key.Length <= 8 ? "••••" : key.Substring(0, 7) + "…" + key.Substring(key.Length - 4); }
    }
}

namespace KeystoneMT5
{
    // ---- the orchestration (no UI, no network of its own): key → budget → context → the advisor → refusal / errors → parse → journal. It NEVER
    // sends an order; Commands() turns an advice into the normal order-path commands only when a click (or AI AUTO on a simulated account) asks
    public sealed class AiCoordinator
    {
        public IAiAdvisor Advisor; public AiBudget Budget; public readonly List<AiEntry> Journal = new List<AiEntry>(); public string JournalFile; public double MaxLots = 1;
        public AiAdvice Last; public string LastStatus = "";
        public static bool AutoAllowed(bool simulated) { return simulated; }
        public const string AutoNo = "🧠 AI AUTO is for simulated accounts only (🎮 DEMO, 🧪 BACKTEST) — on an MT5 account (REAL / PROP / MT5 demo) the advice is only shown; it is executed by your click, which asks first.";
        public async Task<AiAdvice> AskAsync(AiInput input, string basket, bool auto, int iter, CancellationToken ct)
        {
            if (Advisor == null) { LastStatus = "🧠 no Anthropic API key — add one in ⚙ SETUP to use the AI co-pilot"; return null; }
            var why = Budget != null ? Budget.Check(basket, input.Now, auto) : ""; if (why != "") { LastStatus = "🧠 not asked: " + why; return null; }
            var ctx = AiCopilot.Context(input); var hash = AiCopilot.Hash(ctx); LastStatus = "🧠 asking " + Advisor.ModelName + " …";
            AiReply rep; try { rep = await Advisor.AskAsync(AiCopilot.System, "The basket and the market now (JSON):\n" + ctx, ct); } catch (Exception ex) { rep = new AiReply { Error = ex.Message }; }
            double cost = AiCopilot.Cost(rep.InputTokens, rep.OutputTokens, rep.CacheRead, rep.CacheWrite); if (Budget != null && (rep.InputTokens > 0 || rep.OutputTokens > 0)) Budget.Record(basket, input.Now, cost);
            AiAdvice ad;
            if (rep.Error != "") { ad = new AiAdvice { Action = "hold" }; ad.Problems.Add("no answer: " + rep.Error); }
            else if (rep.StopReason == "refusal") { ad = new AiAdvice { Action = "hold" }; ad.Problems.Add("the model declined" + (rep.RefusalCategory != "" ? " (" + rep.RefusalCategory + ")" : "") + (rep.RefusalExplanation != "" ? ": " + rep.RefusalExplanation : "") + " — nothing to execute"); }
            else if (rep.StopReason == "max_tokens") { ad = new AiAdvice { Action = "hold" }; ad.Problems.Add("the answer was cut off (max tokens) — nothing to execute"); }
            else ad = AiCopilot.Parse(rep.Text, input, MaxLots);
            ad.At = input.Now; ad.Hash = hash; Last = ad;
            LastStatus = ad.Valid ? ad.Summary + (rep.FellBack ? " (answered by the fallback model " + rep.Model + ")" : "") : "🧠 " + string.Join("; ", ad.Problems);
            var e = new AiEntry { At = input.Now, Iter = iter, Hash = hash, Action = ad.Valid ? ad.Action : "invalid", Summary = LastStatus, Reason = ad.Reason, Model = rep.Model, Cost = cost, Total = input.Total };
            Journal.Add(e); try { if (JournalFile != null) { Directory.CreateDirectory(Path.GetDirectoryName(JournalFile)); File.AppendAllText(JournalFile, e.Save() + "\n"); } } catch { }
            return ad;
        }
        // an advice → the normal order-path commands (CLOSEALL / CLOSE / ORDER), with labels; [] for hold / an invalid advice
        public static List<Tuple<string, object[], string>> Commands(AiAdvice ad, AiInput a)
        {
            var l = new List<Tuple<string, object[], string>>(); if (ad == null || !ad.Valid) return l; var o = ad.Order;
            if (ad.Action == "close_all") l.Add(Tuple.Create("CLOSEALL", new object[] { a.Symbol, "ALL" }, "🧠 AI: CLOSE ALL " + a.Symbol));
            else if (ad.Action == "close_legs") foreach (var t in ad.CloseTickets) l.Add(Tuple.Create("CLOSE", new object[] { t, 0.0 }, "🧠 AI: CLOSE #" + t));
            else if (ad.Action == "reduce_net")
            {   // close part of the net side: the newest legs first
                double left = o.Lots; int side = o.Side == "SELL" ? 1 : -1;   // a SELL reduces the buys
                foreach (var g in a.Legs.Where(x => (x.Side == "BUY" ? 1 : -1) == side).OrderByDescending(x => x.Ticket)) { if (left < 0.005) break; double c = Math.Round(Math.Min(left, g.Lots), 2); l.Add(Tuple.Create("CLOSE", new object[] { g.Ticket, c >= g.Lots - 1e-9 ? 0.0 : c }, "🧠 AI: REDUCE — close " + c.ToString("0.##", AiCopilot.IC) + " of #" + g.Ticket)); left = Math.Round(left - c, 2); }
            }
            else if (ad.Action == "lock_net_zero" || ad.Action == "recovery_trade" || ad.Action == "add_leg")
            {
                string kind = o.EntryType == "STOP" ? o.Side + " STOP" : o.EntryType == "LIMIT" ? o.Side + " LIMIT" : o.Side; double price = o.EntryType == "MARKET" ? 0.0 : Math.Round(o.EntryLevel, 2);
                l.Add(Tuple.Create("ORDER", new object[] { a.Symbol, kind, Math.Round(o.Lots, 2), price, ad.Action == "lock_net_zero" ? 0.0 : Math.Round(o.Stop, 2), 0.0 }, "🧠 AI: " + ad.Action.Replace('_', ' ').ToUpperInvariant() + " " + kind + " " + o.Lots.ToString("0.##", AiCopilot.IC) + " " + a.Symbol));
            }
            return l;
        }
        // 🧠 the what-if of an advice (09i): the basket as it would be right after following it → the automation alone replays it on the real candles
        // that followed (WhatIf.Replay, its own in-memory SimBroker — never MT5), compared with what really happened. close_all (or no leg left) = done at once.
        public static WhatIfCase Followed(AiAdvice ad, AiInput a, WhatIfCase t)
        {
            if (ad == null || !ad.Valid || ad.Action == "hold") return null;
            var c = new WhatIfCase { N = t.N, Symbol = a.Symbol, Start = t.Start, SnapAt = a.Now, Banked = a.Banked, MaxDd = Math.Min(t.MaxDd, a.Total), Balance = t.Balance, Commission = t.Commission, Target = a.Target, Kind = t.Kind, Leverage = t.Leverage, Spread = t.Spread, Settings = t.Settings, What = ad.Summary };
            foreach (var l in a.Legs) c.Legs.Add(new WhatIfLeg { Ticket = l.Ticket, Side = l.Side, Lots = l.Lots, Open = l.Open, Time = l.Time });
            var o = ad.Order;
            if (ad.Action == "close_all") { c.Banked = a.Total; c.Legs.Clear(); }
            else if (ad.Action == "close_legs") foreach (var tk in ad.CloseTickets) { var l = a.Legs.FirstOrDefault(x => x.Ticket == tk); if (l == null) continue; c.Banked += l.Pl; c.Legs.RemoveAll(x => x.Ticket == tk); }
            else if (ad.Action == "reduce_net")
            {
                double left = o.Lots; int side = o.Side == "SELL" ? 1 : -1;
                foreach (var l in a.Legs.Where(x => (x.Side == "BUY" ? 1 : -1) == side).OrderByDescending(x => x.Ticket))
                {
                    if (left < 0.005) break; double cl = Math.Round(Math.Min(left, l.Lots), 2); c.Banked += l.Pl * cl / l.Lots; var w = c.Legs.First(x => x.Ticket == l.Ticket);
                    if (cl >= l.Lots - 1e-9) c.Legs.Remove(w); else w.Lots = Math.Round(w.Lots - cl, 2); left = Math.Round(left - cl, 2);
                }
            }
            else
            {   // lock / recovery / add: the new leg as if filled now (a STOP / LIMIT at its level — an approximation, said in the journal)
                double px = o.EntryType == "MARKET" || o.EntryLevel <= 0 ? (o.Side == "BUY" ? a.Ask : a.Bid) : o.EntryLevel;
                c.Legs.Add(new WhatIfLeg { Ticket = 900000000 + a.Now.Ticks % 100000000, Side = o.Side, Lots = Math.Round(o.Lots, 2), Open = px, Time = a.Now });
            }
            if (c.Legs.Count == 0) { c.Done = true; c.Result = Math.Round(c.Banked, 2); c.Reason = "🧠 " + ad.Action.Replace('_', ' ').ToUpperInvariant(); c.End = a.Now; c.LastBar = a.Now; c.Worst = c.MaxDd; }
            return c;
        }
        // the LEARN summary: the advice so far, executed or not, what it cost
        public string Summary(DateTime now)
        {
            if (Journal.Count == 0) return "🧠 AI JOURNAL: no advice yet.";
            return "🧠 AI JOURNAL — " + Journal.Count + " advice (" + Journal.Count(j => j.Executed) + " executed) · " + string.Join(", ", Journal.GroupBy(j => j.Action).Select(g => g.Key + " " + g.Count())) + " · spent $" + Journal.Sum(j => j.Cost).ToString("0.00", AiCopilot.IC) + (Budget != null ? " (this month $" + Budget.SpentThisMonth(now).ToString("0.00", AiCopilot.IC) + " of $" + Budget.MonthlyCap.ToString("0", AiCopilot.IC) + ")" : "");
        }
    }
}
