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

    public Task SendAsync(Guid tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken = default)
        => SendAsync(tenantId, toPhoneNumber, message, isOtp: false, cancellationToken: cancellationToken);

    public Task SendOtpAsync(Guid tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken = default)
        => SendAsync(tenantId, toPhoneNumber, message, isOtp: true, cancellationToken: cancellationToken);

    public async Task<MNotifySmsBalance> GetMNotifyBalanceAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.SmsSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (settings?.GhanaGatewayEnabled != true)
            throw new InvalidOperationException("mNotify SMS is not enabled for this tenant.");

        var apiKey = DecryptSecret(settings.GhanaGatewayApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("mNotify API key is not configured for this tenant.");

        using var client = _httpClientFactory.CreateClient("mnotify");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.GhanaGatewayTimeoutSeconds, 1, 60));
        return await MNotifySmsGateway.GetBalanceAsync(client, apiKey, cancellationToken);
    }

    private async Task SendAsync(
        Guid tenantId,
        string toPhoneNumber,
        string message,
        bool isOtp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(toPhoneNumber));

        var settings = await _db.SmsSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);

        if (settings == null)
        {
            await SendWithFallbackOptionsAsync(toPhoneNumber, message, isOtp, cancellationToken);
            return;
        }

        var providers = BuildTenantProviderOrder(settings);
        if (providers.Count == 0)
            throw new InvalidOperationException("No SMS providers are enabled for this tenant.");

        Exception? last = null;
        var failures = new List<(string Provider, Exception Error)>();
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

                if (p is "mnotify" or "ghanagateway" or "ghana")
                {
                    await SendViaMNotifyAsync(settings, toPhoneNumber, message, isOtp, cancellationToken);
                    return;
                }

                throw new InvalidOperationException($"Unknown SMS provider '{provider}'.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
                failures.Add((provider, ex));
                _logger.LogWarning(ex, "Tenant SMS provider {Provider} failed; trying next if available", provider);
            }
        }

        throw new InvalidOperationException(BuildProviderFailureMessage(failures), last);
    }

    internal static IReadOnlyList<string> BuildTenantProviderOrder(SmsSettings settings)
    {
        var configuredProviders = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.DefaultProvider))
            configuredProviders.Add(settings.DefaultProvider);

        if (!string.IsNullOrWhiteSpace(settings.FallbackProvidersJson))
        {
            try
            {
                var fallbacks = JsonSerializer.Deserialize<string[]>(settings.FallbackProvidersJson) ?? Array.Empty<string>();
                configuredProviders.AddRange(fallbacks.Where(provider => !string.IsNullOrWhiteSpace(provider)));
            }
            catch (JsonException)
            {
                // A malformed legacy fallback list must not prevent an enabled provider from being used.
            }
        }

        var providers = new List<string>();
        foreach (var configuredProvider in configuredProviders)
        {
            var normalized = configuredProvider.Trim().ToLowerInvariant();
            if (normalized == "twilio" && settings.TwilioEnabled)
                providers.Add("Twilio");
            else if (normalized is "mnotify" or "ghanagateway" or "ghana" && settings.GhanaGatewayEnabled)
                providers.Add("GhanaGateway");
        }

        if (providers.Count == 0)
        {
            // Recover safely from legacy rows whose default still names a disabled provider.
            // mNotify is the product default, so prefer it when both providers are enabled but
            // neither appears in the persisted routing configuration.
            if (settings.GhanaGatewayEnabled)
                providers.Add("GhanaGateway");
            if (settings.TwilioEnabled)
                providers.Add("Twilio");
        }

        return providers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task SendWithFallbackOptionsAsync(
        string toPhoneNumber,
        string message,
        bool isOtp,
        CancellationToken cancellationToken)
    {
        var providers = new List<string>();
        if (!string.IsNullOrWhiteSpace(_fallbackOptions.DefaultProvider))
            providers.Add(_fallbackOptions.DefaultProvider);
        if (_fallbackOptions.FallbackProviders?.Count > 0)
            providers.AddRange(_fallbackOptions.FallbackProviders.Where(p => !string.IsNullOrWhiteSpace(p)));

        if (providers.Count == 0)
            throw new InvalidOperationException("No SMS providers are configured.");

        Exception? last = null;
        var failures = new List<(string Provider, Exception Error)>();
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

                if (p is "mnotify" or "ghanagateway" or "ghana")
                {
                    await SendViaMNotifyOptionsAsync(_fallbackOptions.GhanaGateway, toPhoneNumber, message, isOtp, cancellationToken);
                    return;
                }

                throw new InvalidOperationException($"Unknown SMS provider '{provider}'.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
                failures.Add((provider, ex));
                _logger.LogWarning(ex, "Fallback SMS provider {Provider} failed; trying next if available", provider);
            }
        }

        throw new InvalidOperationException(BuildProviderFailureMessage(failures), last);
    }

    internal static string BuildProviderFailureMessage(
        IReadOnlyCollection<(string Provider, Exception Error)> failures)
    {
        if (failures.Count == 0)
            return "SMS delivery failed because no configured provider accepted the request.";

        var details = failures.Select(failure =>
            $"{DisplayProviderName(failure.Provider)}: {DescribeProviderFailure(failure.Error)}");
        return $"SMS delivery failed. {string.Join("; ", details)}";
    }

    private static string DisplayProviderName(string provider)
    {
        var normalized = provider.Trim().ToLowerInvariant();
        return normalized switch
        {
            "mnotify" or "ghanagateway" or "ghana" => "mNotify",
            "twilio" => "Twilio",
            _ => string.IsNullOrWhiteSpace(provider) ? "Unknown provider" : provider.Trim()
        };
    }

    private static string DescribeProviderFailure(Exception error)
    {
        var description = error switch
        {
            TaskCanceledException => "The provider request timed out.",
            HttpRequestException { StatusCode: not null } requestError =>
                $"The provider could not be reached (HTTP {(int)requestError.StatusCode.Value}).",
            HttpRequestException => "The provider could not be reached.",
            InvalidOperationException or ArgumentException => error.Message,
            _ => "The provider rejected the request. Review the server log for its response."
        };

        var singleLine = string.Join(" ", description
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= 320 ? singleLine : $"{singleLine[..317]}...";
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

    private async Task SendViaMNotifyAsync(
        SmsSettings settings,
        string toPhoneNumber,
        string message,
        bool isOtp,
        CancellationToken cancellationToken)
    {
        if (settings.GhanaGatewayEnabled != true)
            throw new InvalidOperationException("mNotify SMS is not enabled for this tenant.");

        await SendViaMNotifyOptionsAsync(new GhanaGatewaySmsOptions
        {
            Enabled = true,
            UrlTemplate = settings.GhanaGatewayUrlTemplate,
            ApiKey = DecryptSecret(settings.GhanaGatewayApiKey),
            SenderId = settings.GhanaGatewaySenderId,
            TimeoutSeconds = settings.GhanaGatewayTimeoutSeconds <= 0 ? 10 : settings.GhanaGatewayTimeoutSeconds
        }, toPhoneNumber, message, isOtp, cancellationToken);
    }

    private async Task SendViaMNotifyOptionsAsync(
        GhanaGatewaySmsOptions options,
        string toPhoneNumber,
        string message,
        bool isOtp,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[SMS:mNotify] Sending to {To}; OTP={IsOtp}", Mask(toPhoneNumber), isOtp);

        using var client = _httpClientFactory.CreateClient("mnotify");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 60));
        await MNotifySmsGateway.SendAsync(client, options, toPhoneNumber, message, isOtp, cancellationToken);
    }

    private string? DecryptSecret(string? encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
            return null;

        try
        {
            return _crypto.Decrypt(encryptedValue);
        }
        catch
        {
            // Preserve compatibility with rows created before SMS secrets were encrypted.
            return encryptedValue;
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

