using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementPurchaseOrderAmendmentRules
{
    private static readonly HashSet<string> AmendableStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Approved",
            "Sent",
            "Acknowledged"
        };

    public static bool CanCreate(
        string? purchaseOrderStatus,
        bool hasReceipts,
        bool hasOpenAmendment) =>
        AmendableStatuses.Contains(purchaseOrderStatus ?? string.Empty) &&
        !hasReceipts &&
        !hasOpenAmendment;

    public static decimal CalculateSubTotal(
        IEnumerable<ProcurementPurchaseOrderAmendmentItemRequest> items) =>
        Round(items.Sum(item => item.OrderedQuantity * item.UnitPrice));

    public static decimal CalculateTotal(
        decimal subTotal,
        decimal taxAmount,
        decimal shippingCost,
        decimal miscellaneousCost,
        decimal discountAmount) =>
        Round(subTotal + taxAmount + shippingCost + miscellaneousCost -
              discountAmount);

    public static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static bool IsTerminal(
        ProcurementPurchaseOrderAmendmentStatus status) =>
        status is ProcurementPurchaseOrderAmendmentStatus.Rejected or
            ProcurementPurchaseOrderAmendmentStatus.Cancelled or
            ProcurementPurchaseOrderAmendmentStatus.Acknowledged;

    public static bool IsAutomaticApproval(WorkflowOutcome outcome) =>
        outcome == WorkflowOutcome.Approved;

    public static void ValidateItems(
        IReadOnlyCollection<ProcurementPurchaseOrderAmendmentItemRequest> items)
    {
        if (items.Count == 0)
            throw new ProcurementPurchaseOrderAmendmentValidationException(
                "PO_AMENDMENT_ITEMS_REQUIRED",
                "At least one proposed purchase-order line is required.");
        if (items.Any(item =>
                item.InventoryItemId == Guid.Empty ||
                item.OrderedQuantity <= 0 ||
                item.UnitPrice < 0 ||
                string.IsNullOrWhiteSpace(item.UnitOfMeasure) ||
                string.IsNullOrWhiteSpace(item.ItemDescription)))
        {
            throw new ProcurementPurchaseOrderAmendmentValidationException(
                "PO_AMENDMENT_ITEM_INVALID",
                "Every proposed line requires an inventory item, description, positive quantity, unit of measure, and non-negative price.");
        }

        var duplicateExisting = items
            .Where(item => item.PurchaseOrderItemId.HasValue)
            .GroupBy(item => item.PurchaseOrderItemId)
            .Any(group => group.Count() > 1);
        if (duplicateExisting)
            throw new ProcurementPurchaseOrderAmendmentValidationException(
                "PO_AMENDMENT_ITEM_DUPLICATE",
                "A purchase-order line cannot appear more than once in an amendment.");
    }
}
