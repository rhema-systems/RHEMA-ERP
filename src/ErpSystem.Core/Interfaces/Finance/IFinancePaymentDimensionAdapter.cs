using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-internal adapter from AP/AR payment aggregates to the module-neutral settlement
/// evidence contract. Other module owners do not call or implement this boundary.
/// </summary>
public interface IFinancePaymentDimensionAdapter
{
    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> SynchronizeVendorPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> SynchronizeCustomerPaymentAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> ValidateAndFreezeVendorPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> ValidateAndFreezeCustomerPaymentAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> GetVendorPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> GetCustomerPaymentAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancePostingDimensionValueDto>> ResolveVendorPostingDimensionsAsync(
        Guid componentEvidenceId,
        Guid postingAccountId,
        DateTime postingDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancePostingDimensionValueDto>> ResolveCustomerPostingDimensionsAsync(
        Guid componentEvidenceId,
        Guid postingAccountId,
        DateTime postingDate,
        CancellationToken cancellationToken = default);
}
