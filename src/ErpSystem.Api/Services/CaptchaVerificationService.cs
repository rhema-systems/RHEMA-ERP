using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.Services;

namespace ErpSystem.Api.Services;

public interface ICaptchaVerificationService
{
    Task EnsureCaptchaValidAsync(
        Guid tenantId,
        string? captchaToken,
        string? expectedHostname,
        string? remoteIp,
        CancellationToken cancellationToken);
}

public sealed class CaptchaVerificationService : ICaptchaVerificationService
{
    private readonly ISettingsService _settingsService;
    private readonly ICryptoService _cryptoService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CaptchaVerificationService> _logger;

    public CaptchaVerificationService(
        ISettingsService settingsService,
        ICryptoService cryptoService,
        IHttpClientFactory httpClientFactory,
        ILogger<CaptchaVerificationService> logger)
    {
        _settingsService = settingsService;
        _cryptoService = cryptoService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task EnsureCaptchaValidAsync(
        Guid tenantId,
        string? captchaToken,
        string? expectedHostname,
        string? remoteIp,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetSecuritySettingsAsync(tenantId);
        if (settings?.CaptchaEnabled != true)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(captchaToken))
        {
            throw new CaptchaVerificationException("CAPTCHA token is required.");
        }

        var provider = (settings.CaptchaProvider ?? "recaptcha").Trim().ToLowerInvariant();
        var (endpoint, storedSecret) = provider switch
        {
            "hcaptcha" => ("https://hcaptcha.com/siteverify", settings.HCaptchaSecretKey),
            _ => ("https://www.google.com/recaptcha/api/siteverify", settings.RecaptchaSecretKey)
        };

        var secret = DecryptSecretOrLegacyPlaintext(storedSecret);

        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("CAPTCHA is enabled for tenant {TenantId} but no secret key is configured (provider={Provider})", tenantId, provider);
            throw new CaptchaVerificationException("CAPTCHA is enabled but not configured correctly.");
        }

        var client = _httpClientFactory.CreateClient("captcha");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = secret,
            ["response"] = captchaToken,
            ["remoteip"] = remoteIp ?? string.Empty
        });

        using var resp = await client.PostAsync(endpoint, form, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("CAPTCHA verification HTTP failure for tenant {TenantId}: {StatusCode}", tenantId, resp.StatusCode);
            throw new CaptchaVerificationException("CAPTCHA verification failed.");
        }

        var payload = await resp.Content.ReadFromJsonAsync<CaptchaVerifyResponse>(cancellationToken: cancellationToken);
        if (payload?.Success != true)
        {
            var codes = payload?.ErrorCodes != null ? string.Join(",", payload.ErrorCodes) : "(none)";
            _logger.LogInformation("CAPTCHA verification rejected for tenant {TenantId} (provider={Provider}) errorCodes={ErrorCodes}", tenantId, provider, codes);
            throw new CaptchaVerificationException("CAPTCHA verification failed.");
        }

        if (!string.IsNullOrWhiteSpace(expectedHostname) &&
            !string.IsNullOrWhiteSpace(payload.Hostname) &&
            !string.Equals(expectedHostname.Trim(), payload.Hostname.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("CAPTCHA hostname mismatch for tenant {TenantId}: expected={Expected} actual={Actual}", tenantId, expectedHostname, payload.Hostname);
            throw new CaptchaVerificationException("CAPTCHA verification failed.");
        }
    }

    private string? DecryptSecretOrLegacyPlaintext(string? storedSecret)
    {
        if (string.IsNullOrWhiteSpace(storedSecret))
        {
            return storedSecret;
        }

        try
        {
            return _cryptoService.Decrypt(storedSecret);
        }
        catch
        {
            // Existing deployments may have pre-hardening plaintext values. They are
            // upgraded to encrypted storage on the next security-settings update.
            return storedSecret;
        }
    }

    private sealed class CaptchaVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("score")]
        public double? Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTs { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}

public sealed class CaptchaVerificationException : Exception
{
    public CaptchaVerificationException(string message) : base(message) { }
}

