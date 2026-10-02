using System.Security.Cryptography;
using System.Text;
using ErpSystem.Api.Services;

namespace ErpSystem.Api.Services.Otp;

public enum OtpChannel
{
    Email = 1,
    Sms = 2
}

public enum OtpPurpose
{
    Login = 1,
    PhoneVerification = 2,
    SupplierApplicantVerification = 3,
    SupplierApplicantContactCorrection = 4,
    PublicPropertyEnquiry = 5
}

public interface IOtpService
{
    Task<string> CreateOtpAsync(
        Guid tenantId,
        OtpPurpose purpose,
        OtpChannel channel,
        string target,
        TimeSpan ttl,
        int maxAttempts,
        CancellationToken cancellationToken);

    Task<OtpVerifyResult> VerifyOtpAsync(
        Guid tenantId,
        OtpPurpose purpose,
        OtpChannel channel,
        string target,
        string otpCode,
        bool consumeOnSuccess,
        CancellationToken cancellationToken);
}

public sealed record OtpVerifyResult(bool Success, string? FailureReason);

public sealed class OtpService : IOtpService
{
    private readonly IRedisService _cache;
    private readonly ILogger<OtpService> _logger;

    public OtpService(IRedisService cache, ILogger<OtpService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> CreateOtpAsync(
        Guid tenantId,
        OtpPurpose purpose,
        OtpChannel channel,
        string target,
        TimeSpan ttl,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var normalizedTarget = NormalizeTarget(target);
        var otp = GenerateNumericCode(6);
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var hash = ComputeHash(salt, otp);

        var record = new OtpRecord
        {
            TenantId = tenantId,
            Purpose = purpose.ToString(),
            Channel = channel.ToString(),
            NormalizedTarget = normalizedTarget,
            Salt = salt,
            CodeHash = hash,
            Attempts = 0,
            MaxAttempts = Math.Max(1, maxAttempts),
            ExpiresAtUtc = DateTime.UtcNow.Add(ttl)
        };

        var key = BuildKey(tenantId, purpose, channel, normalizedTarget);
        await _cache.SetAsync(key, record, ttl, cancellationToken);

        _logger.LogInformation(
            "OTP created (tenant={TenantId} purpose={Purpose} channel={Channel} targetHash={TargetHash} ttlMinutes={Ttl})",
            tenantId, purpose, channel, HashForLog(normalizedTarget), Math.Round(ttl.TotalMinutes, 2));

        return otp;
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(
        Guid tenantId,
        OtpPurpose purpose,
        OtpChannel channel,
        string target,
        string otpCode,
        bool consumeOnSuccess,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(otpCode) || otpCode.Length != 6 || !otpCode.All(char.IsDigit))
        {
            return new OtpVerifyResult(false, "Invalid code format");
        }

        var normalizedTarget = NormalizeTarget(target);
        var key = BuildKey(tenantId, purpose, channel, normalizedTarget);

        var record = await _cache.GetAsync<OtpRecord>(key, cancellationToken);
        if (record == null)
        {
            return new OtpVerifyResult(false, "Code expired or not found");
        }

        if (record.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await _cache.RemoveAsync(key, cancellationToken);
            return new OtpVerifyResult(false, "Code expired");
        }

        if (record.Attempts >= record.MaxAttempts)
        {
            await _cache.RemoveAsync(key, cancellationToken);
            return new OtpVerifyResult(false, "Too many attempts");
        }

        var expectedHash = ComputeHash(record.Salt, otpCode);
        var ok = FixedTimeEquals(record.CodeHash, expectedHash);

        if (!ok)
        {
            record.Attempts++;
            var remaining = record.ExpiresAtUtc - DateTime.UtcNow;
            if (remaining < TimeSpan.FromSeconds(1))
            {
                await _cache.RemoveAsync(key, cancellationToken);
                return new OtpVerifyResult(false, "Code expired");
            }

            await _cache.SetAsync(key, record, remaining, cancellationToken);
            return new OtpVerifyResult(false, "Invalid code");
        }

        if (consumeOnSuccess)
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        return new OtpVerifyResult(true, null);
    }

    private static string BuildKey(Guid tenantId, OtpPurpose purpose, OtpChannel channel, string normalizedTarget)
    {
        var targetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedTarget))).ToLowerInvariant();
        return $"otp:{purpose}:{tenantId:N}:{channel}:{targetHash}";
    }

    private static string NormalizeTarget(string target)
    {
        var t = (target ?? string.Empty).Trim();
        if (t.Contains('@'))
        {
            return t.ToLowerInvariant();
        }

        // Phone normalization: keep leading '+' (if any) and digits only.
        var sb = new StringBuilder();
        foreach (var c in t)
        {
            if (c == '+' && sb.Length == 0)
            {
                sb.Append(c);
                continue;
            }

            if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string GenerateNumericCode(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)('0' + (bytes[i] % 10));
        }
        return new string(chars);
    }

    private static string ComputeHash(string saltBase64, string otpCode)
    {
        var data = $"{saltBase64}:{otpCode}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string HashForLog(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private sealed class OtpRecord
    {
        public Guid TenantId { get; set; }
        public required string Purpose { get; set; }
        public required string Channel { get; set; }
        public required string NormalizedTarget { get; set; }
        public required string Salt { get; set; }
        public required string CodeHash { get; set; }
        public int Attempts { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}
