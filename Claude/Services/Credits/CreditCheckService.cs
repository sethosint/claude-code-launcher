using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using ClaudeLauncher.Providers;
using ClaudeLauncher.Services.Logging;

namespace ClaudeLauncher.Services.Credits;

/// <summary>
/// Probes a provider's "/models" endpoint to infer whether it still has usable
/// credits/quota, without spending a real completion. Providers that are out of
/// credits typically answer with 402 Payment Required or 429 Too Many Requests,
/// or with a 401/403 whose body mentions credits/quota/balance.
/// </summary>
public sealed class CreditCheckService : ICreditCheckService
{
    private static readonly string[] ExhaustionKeywords =
    {
        "insufficient", "credit", "quota", "balance", "out of funds", "exceeded"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public CreditCheckService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(8);
        _logger = logger;
    }

    public async Task<CreditStatus> CheckAsync(
        IProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
            return CreditStatus.Unknown;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{provider.BaseUrl.TrimEnd('/')}/models");

            if (!string.IsNullOrWhiteSpace(provider.AuthToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", provider.AuthToken);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
                return CreditStatus.Available;

            var body = await SafeReadBodyAsync(response, cancellationToken);

            if (response.StatusCode == HttpStatusCode.PaymentRequired ||
                response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.Warning(
                    $"Provider '{provider.DisplayName}' reported exhausted credits/quota ({(int)response.StatusCode}).");

                return CreditStatus.Exhausted;
            }

            if ((response.StatusCode == HttpStatusCode.Unauthorized ||
                 response.StatusCode == HttpStatusCode.Forbidden) &&
                ContainsExhaustionKeyword(body))
            {
                _logger.Warning(
                    $"Provider '{provider.DisplayName}' reported exhausted credits/quota ({(int)response.StatusCode}: {body}).");

                return CreditStatus.Exhausted;
            }

            _logger.Warning(
                $"Provider '{provider.DisplayName}' credit check returned {(int)response.StatusCode}; treating as inconclusive.");

            return CreditStatus.Unknown;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(
                $"Provider '{provider.DisplayName}' credit check failed: {ex.Message}");

            return CreditStatus.Unknown;
        }
    }

    private static bool ContainsExhaustionKeyword(string body)
    {
        return ExhaustionKeywords.Any(
            keyword => body.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> SafeReadBodyAsync(
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
