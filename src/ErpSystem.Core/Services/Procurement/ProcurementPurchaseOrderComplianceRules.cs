using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementPurchaseOrderComplianceRules
{
    public static ProcurementCommitmentLifecycleResult ValidateCommitment(
        ProcurementCommitmentLifecycleSnapshot value)
    {
        if (value.CurrentTenantId == Guid.Empty ||
            value.RequisitionTenantId != value.CurrentTenantId ||
            value.ReleaseTenantId != value.CurrentTenantId ||
            value.CommitmentTenantId != value.CurrentTenantId ||
            value.BudgetTenantId != value.CurrentTenantId)
        {
            return Invalid("PO_BUDGET_COMMITMENT_TENANT_MISMATCH",
                "The approved budget commitment is not owned by the current tenant.");
        }

        if (value.ReleaseRequisitionId != value.RequisitionId ||
            value.CommitmentRequisitionId != value.RequisitionId ||
            (value.ReleaseCommitmentId.HasValue &&
             value.ReleaseCommitmentId.Value != value.CommitmentId) ||
            (!string.IsNullOrWhiteSpace(value.ReleaseCommitmentReference) &&
             !string.Equals(value.ReleaseCommitmentReference,
                 value.CommitmentReference, StringComparison.Ordinal)))
        {
            return Invalid("PO_BUDGET_COMMITMENT_LINEAGE_MISMATCH",
                "The approved requisition, sourcing release, and authoritative budget commitment do not agree.");
        }

        if (!value.RequisitionBudgetId.HasValue ||
            value.RequisitionBudgetId.Value != value.CommitmentBudgetId)
            return Invalid("PO_BUDGET_COMMITMENT_REQUISITION_BUDGET_MISMATCH",
                "The approved requisition no longer identifies the commitment's procurement budget.");

        if (value.CommitmentStatus != ProcurementBudgetCommitmentStatus.Reserved)
            return Invalid("PO_BUDGET_COMMITMENT_INACTIVE",
                "The approved requisition budget commitment is no longer active.");
        if (value.CommitmentBudgetId != value.BudgetId)
            return Invalid("PO_BUDGET_COMMITMENT_BUDGET_MISMATCH",
                "The budget commitment no longer identifies its approved budget.");
        if (!string.Equals(value.BudgetStatus, "Approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(value.BudgetStatus, "Active", StringComparison.OrdinalIgnoreCase))
            return Invalid("PO_BUDGET_NOT_APPROVED",
                "The committed procurement budget is no longer approved or active.");
        if (!value.BudgetApprovedAtUtc.HasValue || !value.BudgetApprovedById.HasValue ||
            value.BudgetApprovedById.Value == Guid.Empty)
            return Invalid("PO_BUDGET_APPROVAL_INCOMPLETE",
                "The committed procurement budget lacks complete Finance approval lineage.");

        var atUtc = value.AtUtc.Kind == DateTimeKind.Utc
            ? value.AtUtc
            : value.AtUtc.ToUniversalTime();
        if (value.BudgetEffectiveFromUtc.HasValue &&
            value.BudgetEffectiveFromUtc.Value.ToUniversalTime() > atUtc)
            return Invalid("PO_BUDGET_NOT_EFFECTIVE",
                "The committed procurement budget is not yet effective.");
        if (value.BudgetEffectiveToUtc.HasValue &&
            value.BudgetEffectiveToUtc.Value.ToUniversalTime() < atUtc)
            return Invalid("PO_BUDGET_EXPIRED",
                "The committed procurement budget has expired.");

        var currency = NormalizeCurrency(value.RequiredCurrency);
        if (currency.Length == 0 ||
            !string.Equals(currency, NormalizeCurrency(value.RequisitionCurrency), StringComparison.Ordinal) ||
            !string.Equals(currency, NormalizeCurrency(value.CommitmentCurrency), StringComparison.Ordinal) ||
            !string.Equals(currency, NormalizeCurrency(value.BudgetCurrency), StringComparison.Ordinal))
            return Invalid("PO_BUDGET_COMMITMENT_CURRENCY_MISMATCH",
                "The purchase-order or contract currency does not match the approved requisition budget commitment.");
        if (value.ReservedAmount <= 0m || value.RequiredExposure <= 0m)
            return Invalid("PO_BUDGET_COMMITMENT_AMOUNT_INVALID",
                "The approved budget commitment and downstream exposure must be positive.");
        if (value.BudgetCommittedAmount < value.ReservedAmount)
            return Invalid("PO_BUDGET_COMMITMENT_LEDGER_MISMATCH",
                "The budget committed balance is lower than its active requisition commitment.");
        if (!IsBudgetExposureCovered(value.ReservedAmount, value.RequiredExposure))
            return Invalid("PO_BUDGET_COMMITMENT_INSUFFICIENT",
                $"The active reservation does not cover cumulative downstream exposure {value.RequiredExposure:N2} of {value.ReservedAmount:N2} {currency}.");

        return new ProcurementCommitmentLifecycleResult(
            true,
            "PO_BUDGET_COMMITMENT_CURRENT",
            $"Active reservation {value.CommitmentReference} covers cumulative downstream exposure {value.RequiredExposure:N2} of {value.ReservedAmount:N2} {currency}.");
    }

    public static bool IsBudgetExposureCovered(
        decimal activeReservedAmount,
        decimal activePurchaseOrderExposure) =>
        activeReservedAmount > 0m &&
        activePurchaseOrderExposure >= 0m &&
        activePurchaseOrderExposure <= activeReservedAmount;

    public static bool RequiresActiveBudgetCommitment(string? purchaseOrderStatus) =>
        !string.IsNullOrWhiteSpace(purchaseOrderStatus) &&
        !purchaseOrderStatus.Equals("Draft", StringComparison.OrdinalIgnoreCase) &&
        !purchaseOrderStatus.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) &&
        !purchaseOrderStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase) &&
        !purchaseOrderStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase) &&
        !purchaseOrderStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

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

    private static ProcurementCommitmentLifecycleResult Invalid(
        string code, string message) => new(false, code, message);

    private static string NormalizeCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
}

public sealed record ProcurementCommitmentLifecycleSnapshot(
    Guid CurrentTenantId,
    Guid RequisitionId,
    Guid RequisitionTenantId,
    string RequisitionCurrency,
    Guid? RequisitionBudgetId,
    Guid ReleaseTenantId,
    Guid ReleaseRequisitionId,
    Guid? ReleaseCommitmentId,
    string? ReleaseCommitmentReference,
    Guid CommitmentId,
    Guid CommitmentTenantId,
    Guid CommitmentRequisitionId,
    Guid CommitmentBudgetId,
    string CommitmentReference,
    ProcurementBudgetCommitmentStatus CommitmentStatus,
    decimal ReservedAmount,
    string CommitmentCurrency,
    Guid BudgetId,
    Guid BudgetTenantId,
    string BudgetStatus,
    string BudgetCurrency,
    decimal BudgetCommittedAmount,
    Guid? BudgetApprovedById,
    DateTime? BudgetApprovedAtUtc,
    DateTime? BudgetEffectiveFromUtc,
    DateTime? BudgetEffectiveToUtc,
    decimal RequiredExposure,
    string RequiredCurrency,
    DateTime AtUtc);

public sealed record ProcurementCommitmentLifecycleResult(
    bool IsValid,
    string Code,
    string Message);
