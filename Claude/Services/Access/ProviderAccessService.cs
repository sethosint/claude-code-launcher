using ClaudeLauncher.Providers;
using ClaudeLauncher.Services.Logging;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ClaudeLauncher.Services.Access;

/// <summary>
/// Verifies, before Claude Code is started, that the configured key is actually
/// accepted by the configured endpoint.
///
/// Why this exists: the launcher used to infer "the provider is fine" from a
/// GET /models call, but on both supported catalogs (openrouter.ai and
/// some providers) /models is public — it answers 200 with no key at all, and
/// 200 with a key belonging to a different service. So a launch with an
/// one provider's base URL and another provider's key passed every check, and the
/// first sign of trouble was a raw
/// {"error":{"message":"Missing Authentication header","code":401}} printed
/// inside the Claude session, where the launcher can no longer explain it.
///
/// Two layers:
///   1. an offline key-shape check (an "sk-or-v1-…" key sent to a different host can never work), and
///   2. a real authenticated request — OpenRouter's free GET /key, which also
///      reports the remaining credit, or a one-token completion elsewhere.
/// </summary>
public sealed class ProviderAccessService : IProviderAccessService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    /// <summary>Key prefix → (host fragment the key belongs to, display name).</summary>
    private static readonly (string Prefix, string HostFragment, string Vendor)[] KeyOwners =
    {
        ("sk-or-v1-", "openrouter.ai", "OpenRouter")
    };

    public ProviderAccessService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(12);
        _logger = logger;
    }

    public bool CanCheck(IProvider provider)
    {
        if (string.IsNullOrWhiteSpace(provider.BaseUrl) ||
            string.IsNullOrWhiteSpace(provider.AuthToken))
        {
            return false;
        }

        if (!Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        return !uri.IsLoopback;
    }

    public async Task<AccessCheckResult> CheckAsync(
        IProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (!CanCheck(provider))
            return AccessCheckResult.Unknown("Not checked");

        var baseUrl = provider.BaseUrl.Trim().TrimEnd('/');
        var key = provider.AuthToken!.Trim();
        var host = new Uri(baseUrl).Host;

        var mismatch = DetectKeyHostMismatch(key, host);

        if (mismatch is not null)
            return mismatch;

        try
        {
            return host.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase)
                ? await CheckOpenRouterAsync(baseUrl, key, host, cancellationToken)
                : await CheckWithTinyCompletionAsync(baseUrl, key, host, provider.Model, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Access check against {host} failed: {ex.Message}");

            return AccessCheckResult.Unknown($"Could not reach {host}");
        }
    }

    /// <summary>
    /// Catches the mistake that no live probe is even needed for: a key issued
    /// by one vendor pointed at another vendor's endpoint.
    /// </summary>
    private static AccessCheckResult? DetectKeyHostMismatch(string key, string host)
    {
        foreach (var (prefix, hostFragment, vendor) in KeyOwners)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (host.Contains(hostFragment, StringComparison.OrdinalIgnoreCase))
                return null;

            return AccessCheckResult.Invalid(
                $"{vendor} key sent to {host}",
                $"The API key starts with \"{prefix}\", which means it was issued by {vendor}, " +
                $"but the Base URL points at {host}. Those never match: {host} answers " +
                "\"401 Missing Authentication header\" for it. Either set the Base URL to " +
                $"{vendor}'s endpoint, or paste a key issued by {host}.");
        }

        return null;
    }

    /// <summary>
    /// OpenRouter exposes GET /key: free, authenticated, and it reports the
    /// remaining credit, so it doubles as the credit check.
    /// </summary>
    private async Task<AccessCheckResult> CheckOpenRouterAsync(
        string baseUrl,
        string key,
        string host,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/key");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        var body = await SafeReadAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return AccessCheckResult.Invalid(
                $"Key rejected by {host}",
                $"{host} rejected the API key ({(int)response.StatusCode}: {Describe(body)}). " +
                "Check that the key is correct, still active, and belongs to this account.");
        }

        if (!response.IsSuccessStatusCode)
            return AccessCheckResult.Unknown($"{host} answered {(int)response.StatusCode}");

        var credit = ReadOpenRouterCredit(body);

        if (credit is { Remaining: <= 0 })
        {
            return AccessCheckResult.Exhausted(
                "No credit left",
                $"The key is valid, but {host} reports no remaining credit. Top up the " +
                "account or switch to the other provider.");
        }

        return AccessCheckResult.Ok(
            credit?.Remaining is { } remaining
                ? $"Key valid · ${remaining.ToString("0.00", CultureInfo.InvariantCulture)} left"
                : "Key valid");
    }

    /// <summary>
    /// Generic fallback for OpenAI-compatible endpoints without a dedicated key
    /// endpoint: the smallest possible real request (one token). A rejected key
    /// answers 401/403 long before any tokens are billed.
    /// </summary>
    private async Task<AccessCheckResult> CheckWithTinyCompletionAsync(
        string baseUrl,
        string key,
        string host,
        string model,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model = string.IsNullOrWhiteSpace(model) ? "claude-sonnet-4.5" : model,
            max_tokens = 1,
            messages = new[] { new { role = "user", content = "ping" } }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        var body = await SafeReadAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return AccessCheckResult.Invalid(
                $"Key rejected by {host}",
                $"{host} rejected the API key ({(int)response.StatusCode}: {Describe(body)}). " +
                "Check the key in Settings — and that it belongs to this endpoint.");
        }

        if (response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.TooManyRequests)
        {
            return AccessCheckResult.Exhausted(
                "Out of credit / rate limited",
                $"{host} accepted the key but refused the request ({(int)response.StatusCode}: " +
                $"{Describe(body)}).");
        }

        // A wrong model id (400/404) still proves the credentials were accepted,
        // which is all this check is about.
        if (!response.IsSuccessStatusCode)
            return AccessCheckResult.Ok($"Key accepted (model check: {(int)response.StatusCode})");

        // Quote the first words the endpoint actually produced. It costs
        // nothing and it is the fastest way to notice that a "working"
        // endpoint is in fact a mock answering every prompt with the same
        // canned text, which no status code would ever reveal.
        var reply = ReadFirstChoice(body);

        if (string.IsNullOrWhiteSpace(reply))
            return AccessCheckResult.Ok("Key valid");

        // Detect known canned/joke responses that indicate the endpoint is
        // not actually forwarding to a real model. The key is accepted, but
        // every prompt gets the same hardcoded text back.
        if (IsSuspiciousCannedResponse(reply))
        {
            return AccessCheckResult.Ok(
                $"\u26a0 Endpoint replied \u201c{reply}\u201d to a test prompt \u2014 " +
                "this is a fixed response from the server, not from an AI model. " +
                "Check your account/credits at the provider's dashboard.");
        }

        return AccessCheckResult.Ok($"Key valid \u00b7 replied \u201c{reply}\u201d");
    }

    /// <summary>
    /// True when the response text matches a known hardcoded/joke reply that
    /// real models would never produce for a "ping" prompt. Catching these
    /// early saves the user from a long debugging session.
    /// </summary>
    private static bool IsSuspiciousCannedResponse(string reply)
    {
        var lower = reply.ToLowerInvariant();

        return lower.Contains("never gonna give you up")
            || lower.Contains("never gonna let you down")
            || lower.Contains("rick astley")
            || lower == "hello world"
            || lower == "test"
            || lower == "ok";
    }

    /// <summary>First words of an OpenAI-style completion response, capped for one UI line.</summary>
    private static string ReadFirstChoice(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var first = choices[0];

            if (!first.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            var text = (content.GetString() ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            return text.Length <= 42 ? text : text[..40] + "...";
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record OpenRouterCredit(double? Remaining);

    private static OpenRouterCredit? ReadOpenRouterCredit(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("data", out var data))
                return null;

            if (data.TryGetProperty("limit_remaining", out var remaining) &&
                remaining.ValueKind == JsonValueKind.Number)
            {
                return new OpenRouterCredit(remaining.GetDouble());
            }

            // Prepaid accounts report a total limit and how much of it was used.
            if (data.TryGetProperty("limit", out var limit) &&
                limit.ValueKind == JsonValueKind.Number &&
                data.TryGetProperty("usage", out var usage) &&
                usage.ValueKind == JsonValueKind.Number)
            {
                return new OpenRouterCredit(limit.GetDouble() - usage.GetDouble());
            }

            return new OpenRouterCredit(null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Pulls the provider's own error sentence out of the response body, trimmed for a single UI line.</summary>
    private static string Describe(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "no details";

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return Shorten(error.GetString() ?? "");

                if (error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                {
                    return Shorten(message.GetString() ?? "");
                }
            }
        }
        catch
        {
            // Not JSON — fall through to the raw text.
        }

        return Shorten(body);
    }

    private static string Shorten(string text)
    {
        var single = text.Replace("\r", " ").Replace("\n", " ").Trim();

        return single.Length <= 160 ? single : single[..157] + "...";
    }

    private static async Task<string> SafeReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }
}
