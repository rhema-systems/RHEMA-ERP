using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Shared canonical hashing for FIN-INT-012/013. Producer modules must compute the hash with this
/// helper after completing the immutable payload; Finance recomputes it before returning a contract
/// decision. This is a mutation/retry-drift checksum, not producer authentication or proof that source
/// records exist. Do not implement a module-local serializer: even harmless serialization differences
/// would otherwise make retry/correction evidence unverifiable.
/// </summary>
public static class SupplierReturnFinanceContractIntegrity
{
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string ComputeDispatchHash(SupplierReturnDispatchFinanceDto dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        return Compute(
            "FIN-INT-012/0.1",
            JsonSerializer.SerializeToUtf8Bytes(
                dispatch with { SourceIntegrityHash = string.Empty },
                CanonicalJson));
    }

    public static string ComputeCommercialResolutionHash(
        SupplierReturnCommercialResolutionFinanceDto resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return Compute(
            "FIN-INT-013/0.1",
            JsonSerializer.SerializeToUtf8Bytes(
                resolution with { SourceIntegrityHash = string.Empty },
                CanonicalJson));
    }

    public static bool VerifyDispatch(SupplierReturnDispatchFinanceDto dispatch) =>
        Verify(dispatch.SourceIntegrityHash, ComputeDispatchHash(dispatch));

    public static bool VerifyCommercialResolution(
        SupplierReturnCommercialResolutionFinanceDto resolution) =>
        Verify(resolution.SourceIntegrityHash, ComputeCommercialResolutionHash(resolution));

    private static string Compute(string contractDiscriminator, byte[] canonicalPayload)
    {
        var discriminator = Encoding.UTF8.GetBytes(contractDiscriminator + "\n");
        var bytes = new byte[discriminator.Length + canonicalPayload.Length];
        discriminator.CopyTo(bytes, 0);
        canonicalPayload.CopyTo(bytes, discriminator.Length);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool Verify(string? suppliedHash, string computedHash)
    {
        if (suppliedHash?.Trim() is not { Length: 64 } supplied ||
            supplied.Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(supplied),
            Convert.FromHexString(computedHash));
    }
}
