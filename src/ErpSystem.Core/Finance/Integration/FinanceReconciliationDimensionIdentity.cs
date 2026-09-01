using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Stable source-line identities for the two economic legs of a Finance-owned bank
/// reconciliation adjustment. They are derived from the persisted cash transaction rather than
/// browser row order, so posting, inquiry and frozen evidence always address the same lines.
/// </summary>
public static class FinanceReconciliationDimensionIdentity
{
    public static Guid BankLine(Guid cashTransactionId) =>
        DeterministicGuid($"FIN-RECON-ADJUSTMENT|{cashTransactionId:N}|BANK");

    public static Guid OffsetLine(Guid cashTransactionId) =>
        DeterministicGuid($"FIN-RECON-ADJUSTMENT|{cashTransactionId:N}|OFFSET");

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
