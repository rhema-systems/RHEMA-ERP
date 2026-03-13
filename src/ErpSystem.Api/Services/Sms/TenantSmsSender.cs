using System.Net;
using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ErpSystem.Api.Services.Sms;

/// <summary>
/// Tenant-aware SMS sender that uses per-tenant SmsSettings when present, falling back to appsettings Sms options.
/// Secrets in SmsSettings are stored encrypted; they are decrypted at send time.
/// </summary>
public sealed class TenantSmsSender : ITenantSmsSender
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICryptoService _crypto;
    private readonly SmsOptions _fallbackOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TenantSmsSender> _logger;

    // Twilio SDK uses a static client; serialize init+send to avoid cross-tenant races.
    private static readonly SemaphoreSlim TwilioGate = new(1, 1);

    public TenantSmsSender(
        ErpSystem.Data.ApplicationDbContext db,
        ICryptoService crypto,
        IOptions<SmsOptions> fallbackOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<TenantSmsSender> logger)
    {
        _db = db;
        _crypto = crypto;
        _fallbackOptions = fallbackOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(Guid tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(toPhoneNumber));

        var settings = await _db.SmsSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);

        if (settings == null)
        {
            await SendWithFallbackOptionsAsync(toPhoneNumber, message, cancellationToken);
            return;
        }

        var providers = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.DefaultProvider))
            providers.Add(settings.DefaultProvider);

        if (!string.IsNullOrWhiteSpace(settings.FallbackProvidersJson))
        {
            try
            {
                var fallbacks = JsonSerializer.Deserialize<string[]>(settings.FallbackProvidersJson) ?? Array.Empty<string>();
                providers.AddRange(fallbacks.Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch
            {
                // ignore invalid JSON
            }
        }

        if (providers.Count == 0)
            providers.Add("Twilio");

        Exception? last = null;
        foreach (var provider in providers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var p = provider.Trim().ToLowerInvariant();
                if (p == "twilio")
                {
                    await SendViaTwilioAsync(settings, toPhoneNumber, message, cancellationToken);
                    return;
                }

                if (p is "ghanagateway" or "ghana")
                {
                    await SendViaGhanaGatewayAsync(settings, toPhoneNumber, message, cancellationToken);
                    return;
                }

                throw new InvalidOperationException($"Unknown SMS provider '{provider}'.");
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.LogWarning(ex, "Tenant SMS provider {Provider} failed; trying next if available", provider);
            }
        }

        throw new InvalidOperationException("All tenant SMS providers failed.", last);
    }

    private async Task SendWithFallbackOptionsAsync(string toPhoneNumber, string message, CancellationToken cancellationToken)
    {
        var providers = new List<string>();
        if (!string.IsNullOrWhiteSpace(_fallbackOptions.DefaultProvider))
            providers.Add(_fallbackOptions.DefaultProvider);
        if (_fallbackOptions.FallbackProviders?.Count > 0)
            providers.AddRange(_fallbackOptions.FallbackProviders.Where(p => !string.IsNullOrWhiteSpace(p)));

        if (providers.Count == 0)
            throw new InvalidOperationException("No SMS providers are configured.");

        Exception? last = null;
        foreach (var provider in providers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var p = provider.Trim().ToLowerInvariant();
                if (p == "twilio")
                {
                    await SendViaTwilioOptionsAsync(_fallbackOptions.Twilio, toPhoneNumber, message, cancellationToken);
                    return;
                }

                if (p is "ghanagateway" or "ghana")
                {
                    await SendViaGhanaGatewayOptionsAsync(_fallbackOptions.GhanaGateway, toPhoneNumber, message, cancellationToken);
                    return;
                }

                throw new InvalidOperationException($"Unknown SMS provider '{provider}'.");
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.LogWarning(ex, "Fallback SMS provider {Provider} failed; trying next if available", provider);
            }
        }

        throw new InvalidOperationException("All fallback SMS providers failed.", last);
    }

    private async Task SendViaTwilioAsync(SmsSettings settings, string toPhoneNumber, string message, CancellationToken cancellationToken)
    {
        if (settings.TwilioEnabled != true)
            throw new InvalidOperationException("Twilio SMS is not enabled for this tenant.");

        var accountSid = settings.TwilioAccountSid;
        var authToken = string.IsNullOrWhiteSpace(settings.TwilioAuthToken) ? null : _crypto.Decrypt(settings.TwilioAuthToken);
        var fromNumber = settings.TwilioFromNumber;

        await SendViaTwilioOptionsAsync(new TwilioSmsOptions
        {
            Enabled = true,
            AccountSid = accountSid,
            AuthToken = authToken,
            FromNumber = fromNumber
        }, toPhoneNumber, message, cancellationToken);
    }

    private async Task SendViaTwilioOptionsAsync(TwilioSmsOptions twilio, string toPhoneNumber, string message, CancellationToken cancellationToken)
    {
        if (twilio.Enabled != true)
            throw new InvalidOperationException("Twilio SMS is not enabled.");

        if (string.IsNullOrWhiteSpace(twilio.AccountSid) ||
            string.IsNullOrWhiteSpace(twilio.AuthToken) ||
            string.IsNullOrWhiteSpace(twilio.FromNumber))
        {
            throw new InvalidOperationException("Twilio SMS is enabled but not configured (AccountSid/AuthToken/FromNumber).");
        }

        await TwilioGate.WaitAsync(cancellationToken);
        try
        {
            TwilioClient.Init(twilio.AccountSid, twilio.AuthToken);
            _logger.LogInformation("[SMS:Twilio] Sending to {To}", Mask(toPhoneNumber));

            var to = new PhoneNumber(toPhoneNumber);
            var from = new PhoneNumber(twilio.FromNumber);

            // Twilio SDK does not accept CancellationToken directly here; keep it best-effort.
            await MessageResource.CreateAsync(to: to, from: from, body: message);
        }
        finally
        {
            TwilioGate.Release();
        }
    }

    private async Task SendViaGhanaGatewayAsync(SmsSettings settings, string toPhoneNumber, string message, CancellationToken cancellationToken)
    {
        if (settings.GhanaGatewayEnabled != true)
            throw new InvalidOperationException("GhanaGateway SMS is not enabled for this tenant.");

        await SendViaGhanaGatewayOptionsAsync(new GhanaGatewaySmsOptions
        {
            Enabled = true,
            UrlTemplate = settings.GhanaGatewayUrlTemplate,
            ApiKey = string.IsNullOrWhiteSpace(settings.GhanaGatewayApiKey) ? null : _crypto.Decrypt(settings.GhanaGatewayApiKey),
            SenderId = settings.GhanaGatewaySenderId,
            TimeoutSeconds = settings.GhanaGatewayTimeoutSeconds <= 0 ? 10 : settings.GhanaGatewayTimeoutSeconds
        }, toPhoneNumber, message, cancellationToken);
    }

    private async Task SendViaGhanaGatewayOptionsAsync(GhanaGatewaySmsOptions gw, string toPhoneNumber, string message, CancellationToken cancellationToken)
    {
        if (gw.Enabled != true)
            throw new InvalidOperationException("GhanaGateway SMS is not enabled.");

        if (string.IsNullOrWhiteSpace(gw.UrlTemplate))
            throw new InvalidOperationException("GhanaGateway SMS is enabled but UrlTemplate is not configured.");

        var url = gw.UrlTemplate
            .Replace("{to}", WebUtility.UrlEncode(toPhoneNumber))
            .Replace("{message}", WebUtility.UrlEncode(message))
            .Replace("{senderId}", WebUtility.UrlEncode(gw.SenderId ?? string.Empty))
            .Replace("{apiKey}", WebUtility.UrlEncode(gw.ApiKey ?? string.Empty));

        _logger.LogInformation("[SMS:GhanaGateway] Sending to {To}", Mask(toPhoneNumber));

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(gw.TimeoutSeconds, 1, 60));

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await client.SendAsync(req, cancellationToken);
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

