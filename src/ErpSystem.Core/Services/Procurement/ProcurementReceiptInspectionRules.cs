using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementReceiptInspectionRules
{
    public static (decimal Accepted, decimal Rejected, decimal Pending,
        ProcurementReceiptDisposition Disposition) Evaluate(
        decimal received, decimal accepted, decimal rejected)
    {
        if (received <= 0 || accepted < 0 || rejected < 0 || accepted + rejected > received)
            throw new ProcurementReceiptInspectionValidationException(
                "RCV_INSPECTION_QUANTITY_INVALID",
                "Accepted and rejected quantities must be non-negative and cannot exceed the governed received quantity.");
        var pending = received - accepted - rejected;
        var disposition = pending > 0
            ? ProcurementReceiptDisposition.Pending
            : accepted == received
                ? ProcurementReceiptDisposition.Accepted
                : rejected == received
                    ? ProcurementReceiptDisposition.Rejected
                    : ProcurementReceiptDisposition.PartiallyAccepted;
        return (accepted, rejected, pending, disposition);
    }

    public static bool CanEdit(ProcurementReceiptInspectionStatus status) =>
        status == ProcurementReceiptInspectionStatus.Draft;

    public static bool CanSubmit(ProcurementReceiptInspectionStatus status) =>
        status == ProcurementReceiptInspectionStatus.Draft;

    public static bool CanDecide(ProcurementReceiptInspectionStatus status) =>
        status == ProcurementReceiptInspectionStatus.PendingApproval;

    public static bool RequiresResolution(decimal rejected) => rejected > 0;

    public static decimal CalculateWeightedAverageCost(
        decimal existingQuantity,
        decimal existingAverageCost,
        decimal receivedQuantity,
        decimal receivedUnitCost)
    {
        if (existingQuantity < 0 || existingAverageCost < 0 ||
            receivedQuantity < 0 || receivedUnitCost < 0)
            throw new ArgumentOutOfRangeException(nameof(existingQuantity),
                "Inventory quantities and costs cannot be negative.");

        var totalQuantity = existingQuantity + receivedQuantity;
        return totalQuantity == 0
            ? 0
            : ((existingQuantity * existingAverageCost) +
               (receivedQuantity * receivedUnitCost)) / totalQuantity;
    }

    public static bool CanResolve(ProcurementReceiptInspectionStatus status) =>
        status is ProcurementReceiptInspectionStatus.QualityHold
            or ProcurementReceiptInspectionStatus.ReturnPending
            or ProcurementReceiptInspectionStatus.ReplacementPending;

    public static bool CanClose(
        ProcurementReceiptSupplierAcknowledgementStatus acknowledgement,
        ProcurementReceiptResolutionStatus resolution) =>
        acknowledgement == ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged &&
        resolution is ProcurementReceiptResolutionStatus.Dispatched
            or ProcurementReceiptResolutionStatus.ReplacementReceived;

    public static bool IsApEligible(
        ProcurementReceiptInspectionStatus status,
        decimal pendingQuantity,
        decimal apEligibleQuantity) =>
        pendingQuantity == 0 && apEligibleQuantity > 0 &&
        status is ProcurementReceiptInspectionStatus.Approved
            or ProcurementReceiptInspectionStatus.QualityHold
            or ProcurementReceiptInspectionStatus.ReturnPending
            or ProcurementReceiptInspectionStatus.ReplacementPending
            or ProcurementReceiptInspectionStatus.ClosureReady
            or ProcurementReceiptInspectionStatus.Closed;

    public static bool IsApMatchingResolved(
        ProcurementReceiptInspectionStatus status,
        decimal pendingQuantity,
        decimal apEligibleQuantity) =>
        IsApEligible(status, pendingQuantity, apEligibleQuantity) ||
        (status == ProcurementReceiptInspectionStatus.Closed &&
         pendingQuantity == 0 && apEligibleQuantity == 0);
}
