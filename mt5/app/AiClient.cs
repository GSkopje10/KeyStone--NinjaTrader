// SpasiX — 🧠 AI CO-PILOT: the ONE network call (09i). The official Anthropic C# SDK (NuGet "Anthropic"), model claude-opus-5-5, adaptive thinking
// (always on for this model) with effort HIGH set explicitly, structured outputs (output_config.format = the advice's JSON schema), the stable system
// prompt cached, server-side refusal fallbacks ("fallbacks: default" + the server-side-fallback-2026-07-01 beta), streamed (a long answer never times
// out half-way) and the final stop reason / usage read from the stream. The key lives only in the client object (never logged).
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace KeystoneMT5
{
    public sealed class ClaudeAdvisor : IAiAdvisor
    {
        readonly AnthropicClient client;
        public string ModelName { get { return AiCopilot.Model; } }
        public ClaudeAdvisor(string apiKey, HttpClient http = null) { client = http == null ? new AnthropicClient { ApiKey = apiKey, Timeout = TimeSpan.FromMinutes(5), MaxRetries = 2 } : new AnthropicClient { ApiKey = apiKey, Timeout = TimeSpan.FromMinutes(5), MaxRetries = 0, HttpClient = http }; }   // http: the tests' fake transport (no network)
        // the SDK prints its enum values as JSON strings ("\"refusal\"") — the plain value
        static string Plain(string v) { return (v ?? "").Trim().Trim('"'); }
        static bool IsConn(Exception e) { for (var x = e; x != null; x = x.InnerException) if (x is HttpRequestException || x is System.IO.IOException || x is System.Net.Sockets.SocketException) return true; return e.GetType().Name.Contains("IO"); }
        static string Inner(Exception e) { var x = e; while (x.InnerException != null) x = x.InnerException; return x.Message; }
        // ⚙ SETUP's TEST CONNECTION: the key + the model, nothing billed (a model lookup, not a message)
        public async Task<string> TestAsync(CancellationToken ct)
        {
            try { var m = await client.Models.Retrieve(AiCopilot.Model, null, ct); return m != null ? "" : "the model " + AiCopilot.Model + " was not found"; }
            catch (AnthropicUnauthorizedException) { return "the API key was refused (401)"; }
            catch (AnthropicForbiddenException e) { return "permission denied (403): " + e.Message; }
            catch (AnthropicNotFoundException) { return "the model " + AiCopilot.Model + " is not available to this key (404)"; }
            catch (AnthropicApiException e) { return "API error: " + e.Message; }
            catch (OperationCanceledException) { return ct.IsCancellationRequested ? "cancelled" : "timed out"; }
            catch (HttpRequestException e) { return "no connection to the API: " + e.Message; }
            catch (AnthropicException e) { return IsConn(e) ? "no connection to the API: " + Inner(e) : "SDK error: " + e.Message; }
        }
        public async Task<AiReply> AskAsync(string system, string user, CancellationToken ct)
        {
            var r = new AiReply { Model = AiCopilot.Model };
            var p = new MessageCreateParams
            {
                Model = AiCopilot.Model,
                MaxTokens = 16000,
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),                         // a declined request is re-served by Anthropic's recommended fallback model
                Thinking = new BetaThinkingConfigAdaptive(),
                OutputConfig = new BetaOutputConfig { Effort = Effort.High, Format = new BetaJsonOutputFormat { Schema = AiCopilot.Schema() } },
                System = new List<BetaTextBlockParam> { new() { Text = system, CacheControl = new BetaCacheControlEphemeral() } },
                Messages = [new BetaUserMessageParam { Content = user }],
            };
            var sb = new StringBuilder();
            try
            {
                await foreach (var ev in client.Beta.Messages.CreateStreaming(p, ct))
                {
                    if (ev.TryPickStart(out var ms))
                    {
                        r.Model = Plain(ms.Message.Model.ToString()); r.InputTokens = ms.Message.Usage.InputTokens; r.CacheRead = ms.Message.Usage.CacheReadInputTokens ?? 0; r.CacheWrite = ms.Message.Usage.CacheCreationInputTokens ?? 0;
                    }
                    else if (ev.TryPickContentBlockDelta(out var d) && d.Delta.TryPickText(out var t)) sb.Append(t.Text);
                    else if (ev.TryPickDelta(out var md))
                    {
                        r.StopReason = Plain(md.Delta.StopReason?.ToString()); r.OutputTokens = md.Usage.OutputTokens;
                        if (md.Delta.StopDetails is { } sd) { r.RefusalCategory = Plain(sd.Category?.ToString()); r.RefusalExplanation = sd.Explanation ?? ""; }
                    }
                }
                r.Text = sb.ToString(); r.FellBack = !r.Model.Contains(AiCopilot.Model);
            }
            catch (AnthropicRateLimitException e) { r.Error = "rate limited by the API — try again in a minute (" + e.Message + ")"; }
            catch (AnthropicUnauthorizedException) { r.Error = "the API key was refused (401) — check it in ⚙ SETUP"; }
            catch (AnthropicForbiddenException e) { r.Error = "permission denied (403): " + e.Message; }
            catch (AnthropicOverloadedException) { r.Error = "the API is overloaded right now — try again in a minute"; }
            catch (AnthropicBadRequestException e) { r.Error = "the request was refused (400): " + e.Message; }
            catch (Anthropic4xxException e) { r.Error = "the request was refused: " + e.Message; }
            catch (Anthropic5xxException e) { r.Error = "the API had a server error — try again (" + e.Message + ")"; }
            catch (AnthropicSseException e) { r.Error = "the answer stream broke off: " + e.Message; }
            catch (AnthropicApiException e) { r.Error = "API error: " + e.Message; }
            catch (OperationCanceledException) { r.Error = ct.IsCancellationRequested ? "cancelled" : "timed out (no answer within 5 minutes)"; }
            catch (HttpRequestException e) { r.Error = "no connection to the API: " + e.Message; }
            catch (AnthropicException e) { r.Error = IsConn(e) ? "no connection to the API: " + Inner(e) : "SDK error: " + e.Message; }
            return r;
        }
    }
}
