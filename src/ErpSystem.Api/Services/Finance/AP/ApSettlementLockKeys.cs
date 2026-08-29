namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>
/// Canonical Finance settlement locks. Every AP path that mutates a payment, invoice reservation,
/// or supplier debit note must use these exact keys so independently implemented features cannot
/// race one another inside the same settlement boundary.
/// </summary>
internal static class ApSettlementLockKeys
{
    internal static string Payment(Guid tenantId, Guid paymentId) =>
        $"tdc0505-payment:{tenantId:N}:{paymentId:N}";

    internal static string Invoice(Guid tenantId, Guid invoiceId) =>
        $"tdc0505-invoice:{tenantId:N}:{invoiceId:N}";

    internal static string SupplierDebitNote(Guid tenantId, Guid noteId) =>
        $"ap-supplier-debit-note:{tenantId:N}:{noteId:N}";

    internal static string SupplierIdentity(Guid tenantId, Guid identityId) =>
        $"ap-supplier-identity:{tenantId:N}:{identityId:N}";
}
