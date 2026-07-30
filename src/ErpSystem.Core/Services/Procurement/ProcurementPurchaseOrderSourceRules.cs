using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementPurchaseOrderSourceRules
{
    public sealed record OrderValidationResult(
        bool IsValid,
        string Code,
        string Message)
    {
        public static OrderValidationResult Allowed() =>
            new(true, "PO_SOURCE_COMMERCIAL_TERMS_VALID", "The order matches its approved source.");

        public static OrderValidationResult Denied(string code, string message) =>
            new(false, code, message);
    }

    public static bool CanCreate(ProcurementPurchaseOrderSourceType sourceType) =>
        sourceType is ProcurementPurchaseOrderSourceType.RfqAward
            or ProcurementPurchaseOrderSourceType.TenderAward
            or ProcurementPurchaseOrderSourceType.Contract
            or ProcurementPurchaseOrderSourceType.ApprovedException;

    public static bool IsOneTime(
        ProcurementPurchaseOrderSourceType sourceType) =>
        sourceType is ProcurementPurchaseOrderSourceType.RfqAward
            or ProcurementPurchaseOrderSourceType.TenderAward
            or ProcurementPurchaseOrderSourceType.ApprovedException;

    public static bool IsContractBoundToAward(
        Guid contractTenderAwardId,
        Guid requestedTenderAwardId) =>
        contractTenderAwardId != Guid.Empty &&
        requestedTenderAwardId != Guid.Empty &&
        contractTenderAwardId == requestedTenderAwardId;

    public static Guid ResolveAwardReadinessSourceId(
        ProcurementPurchaseOrderSourceType sourceType,
        Guid sourceId,
        Guid? exceptionalTenderId = null) =>
        sourceType == ProcurementPurchaseOrderSourceType.ApprovedException
            ? exceptionalTenderId ?? Guid.Empty
            : sourceId;

    public static string BuildAwardReservationLock(
        Guid tenantId,
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId) =>
        $"TDC:AWARD-SOURCE:{tenantId:N}:{(int)sourceType}:{sourceId:N}:{businessPartnerId:N}";

    public static bool RequiresRevalidation(string? targetStatus) =>
        targetStatus is not null &&
        (targetStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase) ||
         targetStatus.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) ||
         targetStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
         targetStatus.Equals("Sent", StringComparison.OrdinalIgnoreCase) ||
         targetStatus.Equals("Acknowledged", StringComparison.OrdinalIgnoreCase));

    public static bool IsApprovedRequisitionStatus(string? status) =>
        status is not null &&
        (status.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
         status.Equals("Ordered", StringComparison.OrdinalIgnoreCase));

    public static OrderValidationResult ValidateContractEffectivePeriod(
        DateTime? startDate,
        DateTime? endDate,
        DateTime asOfUtc)
    {
        if (startDate.HasValue && startDate.Value > asOfUtc)
        {
            return OrderValidationResult.Denied(
                "PO_CONTRACT_NOT_STARTED",
                "The contract cannot authorize a purchase order before its start date.");
        }
        if (endDate.HasValue && endDate.Value < asOfUtc)
        {
            return OrderValidationResult.Denied(
                "PO_CONTRACT_EXPIRED",
                "The contract cannot authorize a purchase order after its end date.");
        }
        return OrderValidationResult.Allowed();
    }

    public static bool ContainsApprovedSupplier(string? json, Guid value)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array &&
                   document.RootElement.EnumerateArray().Any(item =>
                       item.ValueKind == JsonValueKind.String &&
                       Guid.TryParse(item.GetString(), out var parsed) &&
                       parsed == value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static OrderValidationResult ValidateOrder(
        ProcurementPurchaseOrderSourceType sourceType,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceLineDto> approvedLines,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine> orderLines,
        decimal? approvedAmount,
        decimal totalAmount,
        string? sourceCurrency,
        string? orderCurrency)
    {
        if (approvedLines.Count == 0)
            return OrderValidationResult.Denied(
                "PO_SOURCE_LINES_NOT_FOUND",
                "The approved source has no authoritative commercial lines.");
        if (orderLines.Count == 0)
            return OrderValidationResult.Denied(
                "PO_SOURCE_LINES_REQUIRED",
                "At least one purchase-order line is required.");
        if (!string.Equals(
                Normalize(sourceCurrency),
                Normalize(orderCurrency),
                StringComparison.OrdinalIgnoreCase))
        {
            return OrderValidationResult.Denied(
                "PO_SOURCE_CURRENCY_MISMATCH",
                "The purchase-order currency does not match the approved source.");
        }
        if (totalAmount < 0)
            return OrderValidationResult.Denied(
                "PO_SOURCE_TOTAL_INVALID",
                "The purchase-order total cannot be negative.");

        var approvedGroups = approvedLines
            .GroupBy(line => new CommercialLineKey(
                Identity(line.InventoryItemId, line.Description),
                Normalize(line.UnitOfMeasure),
                RoundPrice(line.UnitPrice)))
            .ToDictionary(
                group => group.Key,
                group => new CommercialLineAggregate(
                    group.Sum(line => RoundQuantity(line.Quantity)),
                    group.Sum(line => RoundMoney(line.LineTotal))));

        var submittedGroups = new Dictionary<CommercialLineKey, CommercialLineAggregate>();
        foreach (var line in orderLines)
        {
            if (line.OrderedQuantity <= 0 || line.UnitPrice < 0)
            {
                return OrderValidationResult.Denied(
                    "PO_SOURCE_LINE_INVALID",
                    "Purchase-order quantities must be positive and prices cannot be negative.");
            }

            var identityMatches = approvedGroups.Keys
                .Where(key =>
                    IdentityMatches(
                        key.Identity,
                        line.InventoryItemId,
                        line.ItemDescription) &&
                    string.Equals(
                        key.UnitOfMeasure,
                        Normalize(line.UnitOfMeasure),
                        StringComparison.Ordinal))
                .ToList();
            if (identityMatches.Count == 0)
            {
                return OrderValidationResult.Denied(
                    "PO_SOURCE_LINE_NOT_APPROVED",
                    $"The line '{line.ItemDescription ?? line.InventoryItemId?.ToString() ?? "unknown"}' is not part of the approved source.");
            }

            var key = identityMatches.FirstOrDefault(candidate =>
                candidate.UnitPrice == RoundPrice(line.UnitPrice));
            if (key == default)
            {
                return OrderValidationResult.Denied(
                    "PO_SOURCE_LINE_PRICE_MISMATCH",
                    $"The price for '{line.ItemDescription ?? line.InventoryItemId?.ToString() ?? "unknown"}' does not match the approved source.");
            }

            var quantity = RoundQuantity(line.OrderedQuantity);
            var lineTotal = RoundMoney(quantity * RoundPrice(line.UnitPrice));
            if (submittedGroups.TryGetValue(key, out var existing))
            {
                submittedGroups[key] = new CommercialLineAggregate(
                    existing.Quantity + quantity,
                    existing.LineTotal + lineTotal);
            }
            else
            {
                submittedGroups[key] = new CommercialLineAggregate(quantity, lineTotal);
            }
        }

        var exactAward = IsOneTime(sourceType);

        foreach (var (key, submitted) in submittedGroups)
        {
            var approved = approvedGroups[key];
            if (submitted.Quantity > approved.Quantity ||
                (exactAward && submitted.Quantity != approved.Quantity))
            {
                return OrderValidationResult.Denied(
                    "PO_SOURCE_LINE_QUANTITY_MISMATCH",
                    "A purchase-order quantity differs from or exceeds the approved source quantity.");
            }
            if (submitted.LineTotal > approved.LineTotal ||
                (exactAward && submitted.LineTotal != approved.LineTotal))
            {
                return OrderValidationResult.Denied(
                    "PO_SOURCE_LINE_TOTAL_MISMATCH",
                    "A purchase-order line total differs from or exceeds the approved source line total.");
            }
        }

        if (exactAward && approvedGroups.Keys.Any(key => !submittedGroups.ContainsKey(key)))
        {
            return OrderValidationResult.Denied(
                "PO_SOURCE_LINE_SET_MISMATCH",
                "The purchase order omits one or more lines from the approved award.");
        }

        var sourceLimit = RoundMoney(
            approvedAmount ?? approvedGroups.Values.Sum(line => line.LineTotal));
        var submittedSubtotal = RoundMoney(
            submittedGroups.Values.Sum(line => line.LineTotal));
        var submittedTotal = RoundMoney(totalAmount);
        if (submittedSubtotal > sourceLimit || submittedTotal > sourceLimit)
        {
            return OrderValidationResult.Denied(
                "PO_SOURCE_AMOUNT_EXCEEDED",
                "The purchase-order amount exceeds the approved source value.");
        }
        if (exactAward && submittedTotal != sourceLimit)
        {
            return OrderValidationResult.Denied(
                "PO_SOURCE_TOTAL_MISMATCH",
                "The purchase-order total must equal the approved one-time award value.");
        }

        return OrderValidationResult.Allowed();
    }

    private sealed record CommercialLineKey(
        string Identity,
        string UnitOfMeasure,
        decimal UnitPrice);

    private sealed record CommercialLineAggregate(decimal Quantity, decimal LineTotal);

    private static string Identity(Guid? inventoryItemId, string? description) =>
        inventoryItemId.HasValue && inventoryItemId.Value != Guid.Empty
            ? $"inventory:{inventoryItemId.Value:N}"
            : $"description:{Normalize(description)}";

    private static bool IdentityMatches(
        string approvedIdentity,
        Guid? inventoryItemId,
        string? description)
    {
        if (approvedIdentity.StartsWith("inventory:", StringComparison.Ordinal))
        {
            return inventoryItemId.HasValue &&
                   inventoryItemId.Value != Guid.Empty &&
                   string.Equals(
                       approvedIdentity,
                       $"inventory:{inventoryItemId.Value:N}",
                       StringComparison.Ordinal);
        }
        return string.Equals(
            approvedIdentity,
            $"description:{Normalize(description)}",
            StringComparison.Ordinal);
    }

    private static string Normalize(string? value) =>
        string.Join(
            ' ',
            (value ?? string.Empty).Trim().ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static decimal RoundQuantity(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal RoundPrice(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
