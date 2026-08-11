using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementWorksCloseoutRules
{
    private static readonly IReadOnlyDictionary<ProcurementWorksCloseoutActionType, IReadOnlyList<string>>
        EvidenceByAction = new Dictionary<ProcurementWorksCloseoutActionType, IReadOnlyList<string>>
        {
            [ProcurementWorksCloseoutActionType.InitialTakeover] =
                ["practical-completion-certificate", "takeover-inspection"],
            [ProcurementWorksCloseoutActionType.DefectRectification] =
                ["rectification-completion", "rectification-verification"],
            [ProcurementWorksCloseoutActionType.FinalTakeover] =
                ["final-completion-certificate", "final-inspection"],
            [ProcurementWorksCloseoutActionType.WarrantyRelease] =
                ["warranty-expiry-confirmation", "no-outstanding-defects"],
            [ProcurementWorksCloseoutActionType.PerformanceSecurityRelease] =
                ["security-release-recommendation", "no-outstanding-claims"],
            [ProcurementWorksCloseoutActionType.RetentionRelease] =
                ["retention-certificate", "finance-release-approval"],
            [ProcurementWorksCloseoutActionType.DisputeOpen] =
                ["dispute-notice", "dispute-supporting-evidence"],
            [ProcurementWorksCloseoutActionType.DisputeResolve] =
                ["settlement-or-determination", "resolution-acceptance"],
            [ProcurementWorksCloseoutActionType.Termination] =
                ["termination-notice", "legal-review", "termination-account"],
            [ProcurementWorksCloseoutActionType.FinalAccount] =
                ["approved-final-account", "final-account-reconciliation"],
            [ProcurementWorksCloseoutActionType.Closeout] =
                ["closeout-certificate", "records-archive-confirmation", "lessons-and-handover"]
        };

    public static IReadOnlyDictionary<ProcurementWorksCloseoutActionType, IReadOnlyList<string>>
        RequiredEvidence => EvidenceByAction;

    public static bool IsWorks(string? contractType) =>
        string.Equals(contractType?.Trim(), "Works", StringComparison.OrdinalIgnoreCase);

    public static bool CanSubmit(string? contractStatus, ProcurementWorksCloseoutActionType actionType)
    {
        if (actionType is ProcurementWorksCloseoutActionType.DisputeResolve
            or ProcurementWorksCloseoutActionType.Termination)
            return contractStatus is not null &&
                   (contractStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                    contractStatus.Equals("Suspended", StringComparison.OrdinalIgnoreCase));
        return string.Equals(contractStatus, "Active", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanDecide(ProcurementWorksCloseoutActionStatus status) =>
        status is ProcurementWorksCloseoutActionStatus.PendingApproval
            or ProcurementWorksCloseoutActionStatus.RevalidationFailed;

    public static bool IsIndependent(Guid actorId, Guid submittedById, Guid? contractCreatedById) =>
        actorId != Guid.Empty && actorId != submittedById &&
        (!contractCreatedById.HasValue || actorId != contractCreatedById.Value);

    public static bool IsMonetary(ProcurementWorksCloseoutActionType actionType) =>
        actionType is ProcurementWorksCloseoutActionType.RetentionRelease
            or ProcurementWorksCloseoutActionType.FinalAccount
            or ProcurementWorksCloseoutActionType.Termination;

    public static bool IsOpenDefectStatus(string? status) =>
        !string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(status, "WarrantyExpired", StringComparison.OrdinalIgnoreCase);

    public static bool AmountMatches(decimal? requested, decimal expected) =>
        requested.HasValue && Math.Abs(requested.Value - expected) <= 0.01m;

    public static DateTime? DefectsLiabilityEnd(DateTime? initialTakeoverAt, int? warrantyDays) =>
        initialTakeoverAt.HasValue && warrantyDays.GetValueOrDefault() > 0
            ? initialTakeoverAt.Value.AddDays(warrantyDays!.Value)
            : initialTakeoverAt;

    public static decimal RetentionStageLimit(
        decimal retentionHeld,
        decimal retentionReleased,
        decimal releasedForStage,
        ProcurementRetentionReleaseStage stage,
        decimal practicalCompletionPercent,
        decimal sectionalTakeoverPercent,
        decimal defectsReleasePercent)
    {
        var available = Math.Max(decimal.Round(retentionHeld - retentionReleased, 2), 0m);
        if (stage == ProcurementRetentionReleaseStage.FinalRelease)
            return available;

        var percent = stage switch
        {
            ProcurementRetentionReleaseStage.PracticalCompletion => practicalCompletionPercent,
            ProcurementRetentionReleaseStage.SectionalTakeover => sectionalTakeoverPercent,
            ProcurementRetentionReleaseStage.DefectsLiability => defectsReleasePercent,
            _ => 0m
        };
        var stageCap = decimal.Round(retentionHeld * percent / 100m, 2);
        return Math.Min(available, Math.Max(decimal.Round(stageCap - releasedForStage, 2), 0m));
    }
}
