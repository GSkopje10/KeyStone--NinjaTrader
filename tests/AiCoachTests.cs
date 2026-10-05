// AI COACH: the Claude Messages API request body and the answer parsing (text, refusal, API error, unreadable) — no network here.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public static class AiCoachTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    public static int Main()
    {
        // request: valid JSON, the model, fallbacks, the system prompt, history, the chart state + question with quotes / newlines escaped
        var hist = new List<Tuple<string, string>> { Tuple.Create("first \"q\"", "first a") };
        string body = KeystoneAiCoach.Request("MNQ 5M\nlast 31150.25", "enter \"now\"?", hist);
        var d = KeystoneJson.Parse(body) as Dictionary<string, object>;
        Check(d != null && (string)d["model"] == "claude-opus-5-5" && (string)d["fallbacks"] == "default" && ((string)d["system"]).Contains("KEYSTONE ARC"), "request: model, fallbacks default, system prompt", body.Substring(0, Math.Min(160, body.Length)));
        var msgs = d == null ? null : d["messages"] as List<object>;
        Check(msgs != null && msgs.Count == 3 && (string)((Dictionary<string, object>)msgs[0])["role"] == "user" && (string)((Dictionary<string, object>)msgs[1])["role"] == "assistant", "request: history as user / assistant turns, then the question", msgs == null ? "null" : msgs.Count.ToString());
        string last = msgs == null ? "" : (string)((Dictionary<string, object>)msgs[2])["content"];
        Check(last.Contains("MNQ 5M\nlast 31150.25") && last.Contains("QUESTION: enter \"now\"?"), "request: chart state + question survive escaping", last);
        Check(!body.Contains("budget_tokens") && !body.Contains("\"thinking\"") && !body.Contains("temperature"), "request: no thinking / sampling settings (the model's defaults)");
        // answer: text blocks joined, thinking blocks skipped, usage read
        var ok = KeystoneAiCoach.Answer("{\"type\":\"message\",\"content\":[{\"type\":\"thinking\",\"thinking\":\"\"},{\"type\":\"text\",\"text\":\"WAIT \\u2014 no edge.\\nStop 31140.\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":1200,\"output_tokens\":85}}");
        Check(ok.Ok && ok.Text == "WAIT — no edge.\nStop 31140." && ok.InTokens == 1200 && ok.OutTokens == 85, "answer: text (unicode escape, newline), thinking skipped, usage", ok.Text + " / " + ok.Error);
        var refused = KeystoneAiCoach.Answer("{\"type\":\"message\",\"content\":[],\"stop_reason\":\"refusal\",\"stop_details\":{\"type\":\"refusal\",\"category\":null}}");
        Check(!refused.Ok && refused.Error.Contains("declined"), "answer: refusal → a clear message, not empty text", refused.Error);
        var err = KeystoneAiCoach.Answer("{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}");
        Check(!err.Ok && err.Error.Contains("authentication_error") && err.Error.Contains("invalid x-api-key"), "answer: API error type + message", err.Error);
        var junk = KeystoneAiCoach.Answer("<html>bad gateway</html>");
        Check(!junk.Ok && junk.Error.StartsWith("unreadable"), "answer: not JSON → unreadable reply", junk.Error);
        var cut = KeystoneAiCoach.Answer("{\"type\":\"message\",\"content\":[],\"stop_reason\":\"max_tokens\"}");
        Check(!cut.Ok && cut.Error.Contains("cut off"), "answer: max_tokens with no text → cut off", cut.Error);
        Console.WriteLine(failures == 0 ? "ALL AI COACH TESTS PASSED" : failures + " AI COACH TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
