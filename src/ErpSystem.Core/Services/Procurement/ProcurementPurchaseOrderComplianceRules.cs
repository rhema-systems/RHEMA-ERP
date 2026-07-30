using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementPurchaseOrderComplianceRules
{
    public static bool IsBudgetExposureCovered(
        decimal activeReservedAmount,
        decimal activePurchaseOrderExposure) =>
        activeReservedAmount > 0m &&
        activePurchaseOrderExposure >= 0m &&
        activePurchaseOrderExposure <= activeReservedAmount;

    public static bool IsContractSignatureComplete(
        DateTime? organizationSignedAt,
        Guid? organizationSignatoryId,
        string? organizationSignatoryName,
        DateTime? contractorSignedAt,
        string? contractorSignatoryName,
        bool signedEvidenceAvailable) =>
        organizationSignedAt.HasValue &&
        organizationSignatoryId.HasValue &&
        organizationSignatoryId.Value != Guid.Empty &&
        !string.IsNullOrWhiteSpace(organizationSignatoryName) &&
        contractorSignedAt.HasValue &&
        !string.IsNullOrWhiteSpace(contractorSignatoryName) &&
        signedEvidenceAvailable;

    public static bool IsPrerequisiteGroupSatisfied(
        IEnumerable<ProcurementAwardReadinessPrerequisiteGroupDto> groups,
        ProcurementAwardReadinessPrerequisiteGroup group) =>
        groups.Any(item =>
            item.Group == group &&
            item.Status is ProcurementAwardReadinessPrerequisiteStatus.Passed or
                ProcurementAwardReadinessPrerequisiteStatus.NotApplicable &&
            item.Items.All(child =>
                child.Status is ProcurementAwardReadinessPrerequisiteStatus.Passed or
                    ProcurementAwardReadinessPrerequisiteStatus.NotApplicable));

    public static string NormalizeAction(string? action)
    {
        var value = action?.Trim();
        if (string.Equals(value, "Submit", StringComparison.OrdinalIgnoreCase))
            return "Submit";
        if (string.Equals(value, "Approve", StringComparison.OrdinalIgnoreCase))
            return "Approve";
        return "Preview";
    }
}
