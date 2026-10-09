using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Callable Finance-owned boundaries for non-Finance producers. Values are intentionally distinct
/// from FinanceDimensionRouteId so a producer cannot select a manual/internal Finance route.
/// </summary>
public enum FinanceExternalProducerContractId
{
    ProcurementAcceptedInventoryReceipt,
    InventoryStockAdjustment,
    InventoryLandedCost,
    InventoryDisposalProceeds,
    SalesCreditNote,
    QuantitySurveyPaymentCertificate,
    QuantitySurveySubcontractCertificate,
    EstateGroundRentCharge,
    EstateGroundRentPenalty,
    EstatePropertyRentBilling,
    EstatePropertySaleBilling,
    EstateFacilitiesBilling,
    EstateLandAcquisition,
    LegalTransferFeeBilling,
    MaintenanceWorkOrderBilling,
    HrPayrollJournal,
    ProcurementSupplierReturnDispatch,
    ProcurementSupplierReturnResolution,
    MobilePosCustomerInvoice,
    MobilePosCustomerPayment
}

public static class FinanceExternalProducerContractCatalog
{
    private static readonly IReadOnlyDictionary<FinanceExternalProducerContractId, FinanceDimensionRouteId> Routes =
        new Dictionary<FinanceExternalProducerContractId, FinanceDimensionRouteId>
        {
            [FinanceExternalProducerContractId.ProcurementAcceptedInventoryReceipt] = FinanceDimensionRouteId.ProcurementAcceptedInventoryReceipt,
            [FinanceExternalProducerContractId.InventoryStockAdjustment] = FinanceDimensionRouteId.InventoryStockAdjustment,
            [FinanceExternalProducerContractId.InventoryLandedCost] = FinanceDimensionRouteId.InventoryLandedCost,
            [FinanceExternalProducerContractId.InventoryDisposalProceeds] = FinanceDimensionRouteId.InventoryDisposalProceeds,
            [FinanceExternalProducerContractId.SalesCreditNote] = FinanceDimensionRouteId.SalesCreditNote,
            [FinanceExternalProducerContractId.QuantitySurveyPaymentCertificate] = FinanceDimensionRouteId.QuantitySurveyPaymentCertificate,
            [FinanceExternalProducerContractId.QuantitySurveySubcontractCertificate] = FinanceDimensionRouteId.QuantitySurveySubcontractCertificate,
            [FinanceExternalProducerContractId.EstateGroundRentCharge] = FinanceDimensionRouteId.EstateGroundRentCharge,
            [FinanceExternalProducerContractId.EstateGroundRentPenalty] = FinanceDimensionRouteId.EstateGroundRentPenalty,
            [FinanceExternalProducerContractId.EstatePropertyRentBilling] = FinanceDimensionRouteId.EstatePropertyRentBilling,
            [FinanceExternalProducerContractId.EstatePropertySaleBilling] = FinanceDimensionRouteId.EstatePropertySaleBilling,
            [FinanceExternalProducerContractId.EstateFacilitiesBilling] = FinanceDimensionRouteId.EstateFacilitiesBilling,
            [FinanceExternalProducerContractId.EstateLandAcquisition] = FinanceDimensionRouteId.EstateLandAcquisition,
            [FinanceExternalProducerContractId.LegalTransferFeeBilling] = FinanceDimensionRouteId.LegalTransferFeeBilling,
            [FinanceExternalProducerContractId.MaintenanceWorkOrderBilling] = FinanceDimensionRouteId.MaintenanceWorkOrderBilling,
            [FinanceExternalProducerContractId.HrPayrollJournal] = FinanceDimensionRouteId.HrPayrollJournal,
            [FinanceExternalProducerContractId.ProcurementSupplierReturnDispatch] = FinanceDimensionRouteId.ProcurementSupplierReturnDispatch,
            [FinanceExternalProducerContractId.ProcurementSupplierReturnResolution] = FinanceDimensionRouteId.ProcurementSupplierReturnResolution,
            [FinanceExternalProducerContractId.MobilePosCustomerInvoice] = FinanceDimensionRouteId.MobilePosCustomerInvoice,
            [FinanceExternalProducerContractId.MobilePosCustomerPayment] = FinanceDimensionRouteId.MobilePosCustomerPayment
        };

    public static FinancePostingProducerContext GetRequired(FinanceExternalProducerContractId contractId)
    {
        if (!Routes.TryGetValue(contractId, out var routeId))
            throw new KeyNotFoundException($"External Finance contract '{contractId}' is not registered.");
        var route = FinanceDimensionRouteCatalog.GetRequired(routeId);
        if (string.Equals(route.ProducerModule, "Finance", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An external producer contract cannot resolve to a Finance-owned route.");
        return new FinancePostingProducerContext(routeId);
    }
}

public static class FinanceExternalDimensionIdentity
{
    public static Guid SourceLine(
        FinanceExternalProducerContractId contractId,
        Guid sourceDocumentId,
        string economicLineKind,
        Guid? sourceComponentId = null)
    {
        if (sourceDocumentId == Guid.Empty) throw new ArgumentException("Source document id is required.", nameof(sourceDocumentId));
        if (string.IsNullOrWhiteSpace(economicLineKind)) throw new ArgumentException("Economic line kind is required.", nameof(economicLineKind));
        var canonical = $"finance-external:v1:{contractId}:{sourceDocumentId:N}:{economicLineKind.Trim().ToUpperInvariant()}:{sourceComponentId?.ToString("N") ?? "none"}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
