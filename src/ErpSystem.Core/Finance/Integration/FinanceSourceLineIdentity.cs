using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Creates stable, non-random source-line identities for server-derived Finance components.
/// The identity is deterministic across retries and does not depend on display order.
/// </summary>
public static class FinanceSourceLineIdentity
{
    public static Guid Create(Guid sourceDocumentId, string component, params Guid[] lineage)
    {
        if (sourceDocumentId == Guid.Empty)
            throw new ArgumentException("A source document id is required.", nameof(sourceDocumentId));
        if (string.IsNullOrWhiteSpace(component))
            throw new ArgumentException("A source-line component is required.", nameof(component));

        var payload = string.Join('|', new[]
        {
            sourceDocumentId.ToString("N"),
            component.Trim().ToUpperInvariant(),
            string.Join(':', lineage.Select(value => value.ToString("N")))
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var guidBytes = bytes[..16];
        // RFC 4122 variant plus a version-5 marker make generated values recognizable while the
        // SHA-256 payload provides deterministic collision resistance for the Finance namespace.
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
