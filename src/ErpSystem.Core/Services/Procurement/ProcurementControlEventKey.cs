using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementControlEventKey
{
    public static string Create(string prefix, params object?[] components)
    {
        var canonical = string.Join('|', components.Select(item => item?.ToString()?.Trim() ?? string.Empty));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"{prefix.Trim().ToLowerInvariant()}:{hash}";
    }
}
