using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Canonical evidence that the complete trusted economic-line context used during capture can be
/// reproduced during certification readiness. Browser payload ordering never affects the hash.
/// </summary>
public static class FinanceSourceLineManifest
{
    public static string Compute(IEnumerable<(Guid SourceLineId, Guid AccountId)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var canonical = string.Join("\n", lines
            .OrderBy(item => item.SourceLineId)
            .ThenBy(item => item.AccountId)
            .Select(item => $"{item.SourceLineId:D}|{item.AccountId:D}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
