using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ErpSystem.Api.Services.MobilePos;

public sealed record MobilePosOfflineGrantTokenPayload(
    int Version,
    Guid GrantId,
    Guid TenantId,
    Guid UserId,
    Guid DeviceId,
    Guid StoreId,
    Guid TillId,
    Guid CashierTillSessionId,
    Guid OfflinePolicyId,
    string PolicySnapshotHash,
    long RevocationEpoch,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

public interface IMobilePosOfflineGrantTokenService
{
    string Sign(MobilePosOfflineGrantTokenPayload payload);
    MobilePosOfflineGrantTokenPayload Validate(string token, DateTime? nowUtc = null);
}

/// <summary>
/// Issues a compact authenticated token for later server-side verification during synchronization.
/// The application receives the readable policy snapshot separately; the signing key never leaves
/// the API. A purpose-derived key isolates these tokens from normal authentication JWTs even when
/// the deployment uses the JWT secret as its configured key source.
/// </summary>
public sealed class MobilePosOfflineGrantTokenService : IMobilePosOfflineGrantTokenService
{
    public const int CurrentVersion = 1;
    private const string Prefix = "MPG1";
    private const string Purpose = "RHEMA.MobilePOS.OfflineGrant.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _signingKey;

    public MobilePosOfflineGrantTokenService(IConfiguration configuration)
    {
        var secret = configuration["MobilePos:OfflineGrantSigningKey"]
            ?? configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException(
                "Configure MobilePos:OfflineGrantSigningKey or JwtSettings:SecretKey before issuing offline grants.");
        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("The Mobile POS offline grant signing key must contain at least 32 bytes.");

        using var derivation = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        _signingKey = derivation.ComputeHash(Encoding.UTF8.GetBytes(Purpose));
    }

    public string Sign(MobilePosOfflineGrantTokenPayload payload)
    {
        ValidatePayload(payload);
        var body = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var unsigned = $"{Prefix}.{body}";
        return $"{unsigned}.{Base64UrlEncode(ComputeSignature(unsigned))}";
    }

    public MobilePosOfflineGrantTokenPayload Validate(string token, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("The offline grant token is required.");

        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The offline grant token format is invalid.");

        byte[] suppliedSignature;
        byte[] payloadBytes;
        try
        {
            suppliedSignature = Base64UrlDecode(parts[2]);
            payloadBytes = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            throw new UnauthorizedAccessException("The offline grant token encoding is invalid.");
        }

        var expectedSignature = ComputeSignature($"{parts[0]}.{parts[1]}");
        if (suppliedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            throw new UnauthorizedAccessException("The offline grant token signature is invalid.");

        MobilePosOfflineGrantTokenPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<MobilePosOfflineGrantTokenPayload>(payloadBytes, JsonOptions)
                ?? throw new JsonException("Empty payload.");
        }
        catch (JsonException)
        {
            throw new UnauthorizedAccessException("The offline grant token payload is invalid.");
        }

        ValidatePayload(payload);
        var effectiveNow = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();
        if (payload.ExpiresAtUtc.ToUniversalTime() <= effectiveNow)
            throw new UnauthorizedAccessException("The offline grant token has expired.");
        if (payload.IssuedAtUtc.ToUniversalTime() > effectiveNow.AddMinutes(5))
            throw new UnauthorizedAccessException("The offline grant token issue time is invalid.");
        return payload;
    }

    private byte[] ComputeSignature(string value)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static void ValidatePayload(MobilePosOfflineGrantTokenPayload payload)
    {
        if (payload.Version != CurrentVersion || payload.GrantId == Guid.Empty || payload.TenantId == Guid.Empty ||
            payload.UserId == Guid.Empty || payload.DeviceId == Guid.Empty || payload.StoreId == Guid.Empty ||
            payload.TillId == Guid.Empty || payload.CashierTillSessionId == Guid.Empty ||
            payload.OfflinePolicyId == Guid.Empty || payload.PolicySnapshotHash.Length != 64 ||
            payload.ExpiresAtUtc <= payload.IssuedAtUtc)
            throw new InvalidOperationException("The offline grant token payload is incomplete or invalid.");
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException() };
        return Convert.FromBase64String(padded);
    }
}
