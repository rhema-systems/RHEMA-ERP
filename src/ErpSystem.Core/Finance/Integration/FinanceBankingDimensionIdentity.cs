using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Code-owned identities for economic lines that do not have their own operational row.
/// Capture, draft hydration, readiness and posting evidence must all use these same identities.
/// </summary>
public static class FinanceBankingDimensionIdentity
{
    public static Guid ReturnedChequeBankLine(Guid returnedChequeId) =>
        Stable(returnedChequeId, "bank-leg");

    public static Guid ReturnedChequeCustomerLine(Guid returnedChequeId) =>
        Stable(returnedChequeId, "customer-balance");

    public static Guid ReturnedChequeExpenseLine(Guid returnedChequeId) =>
        Stable(returnedChequeId, "bank-charge-expense");

    public static Guid ReturnedChequeInheritedSettlementLine(
        Guid returnedChequeId,
        Guid originalEvidenceId) =>
        Stable(returnedChequeId, $"receipt-evidence:{originalEvidenceId:N}");

    private static Guid Stable(Guid documentId, string lineKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"FIN-BANKING-DIM|{documentId:N}|{lineKey}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
