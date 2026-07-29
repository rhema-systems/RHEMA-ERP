using System.Text.Json;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementPurchaseOrderSourceRules
{
    public static bool CanCreate(ProcurementPurchaseOrderSourceType sourceType) =>
        sourceType is ProcurementPurchaseOrderSourceType.RfqAward
            or ProcurementPurchaseOrderSourceType.TenderAward
            or ProcurementPurchaseOrderSourceType.Contract
            or ProcurementPurchaseOrderSourceType.ApprovedException;

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
}
