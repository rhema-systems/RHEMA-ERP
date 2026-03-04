using System.Net;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Sms;

public sealed class GhanaGatewaySmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly SmsOptions _options;
    private readonly ILogger<GhanaGatewaySmsSender> _logger;

    public GhanaGatewaySmsSender(HttpClient httpClient, IOptions<SmsOptions> options, ILogger<GhanaGatewaySmsSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var gw = _options.GhanaGateway;
        if (gw.Enabled != true)
        {
            throw new InvalidOperationException("GhanaGateway SMS is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(gw.UrlTemplate))
        {
            throw new InvalidOperationException("GhanaGateway SMS is enabled but UrlTemplate is not configured.");
        }

        var url = gw.UrlTemplate
            .Replace("{to}", WebUtility.UrlEncode(toPhoneNumber))
            .Replace("{message}", WebUtility.UrlEncode(message))
            .Replace("{senderId}", WebUtility.UrlEncode(gw.SenderId ?? string.Empty))
            .Replace("{apiKey}", WebUtility.UrlEncode(gw.ApiKey ?? string.Empty));

        _logger.LogInformation("[SMS:GhanaGateway] Sending to {To}", Mask(toPhoneNumber));

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _httpClient.SendAsync(req, cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"GhanaGateway SMS failed: HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body={body}");
        }
    }

    private static string Mask(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "Unknown";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "***";
        return $"***{digits[^4..]}";
    }
}

