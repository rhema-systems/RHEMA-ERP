using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementPurchaseOrderSodRules
{
    public const string ApprovalControl = "SOD-INITIATOR-APPROVER";
    public const string ReceiptControl = "SOD-PO-CREATOR-RECEIVER";
    public const string CreatePurchaseOrderReceipt = "CreatePurchaseOrderReceipt";
    public const string CreateGoodsReceiptNote = "CreateGoodsReceiptNote";
    public const string InitializeReceiptInspection = "InitializeReceiptInspection";
    public const string SaveReceiptInspection = "SaveReceiptInspection";
    public const string SubmitReceiptInspection = "SubmitReceiptInspection";
    public const string ApproveReceiptInspection = "ApproveReceiptInspection";
    public const string ConfirmReplacementReceipt = "ConfirmReplacementReceipt";
    public const string CloseReceiptInspection = "CloseReceiptInspection";
    public const string PostGoodsReceiptNoteToInventory =
        "PostGoodsReceiptNoteToInventory";

    public static IReadOnlyList<string> ReceiptActionCoverage { get; } =
    [
        CreatePurchaseOrderReceipt,
        CreateGoodsReceiptNote,
        InitializeReceiptInspection,
        SaveReceiptInspection,
        SubmitReceiptInspection,
        ApproveReceiptInspection,
        ConfirmReplacementReceipt,
        CloseReceiptInspection,
        PostGoodsReceiptNoteToInventory
    ];

    public static string NormalizeReceiptAction(string? value)
    {
        var action = value?.Trim();
        if (ReceiptActionCoverage.Contains(action, StringComparer.Ordinal))
            return action!;
        throw new ArgumentException(
            "The receipt SOD action is not registered.",
            nameof(value));
    }

    public static bool IsReceiptAction(string? value) =>
        value is not null &&
        ReceiptActionCoverage.Contains(value, StringComparer.Ordinal);

    public static IReadOnlyList<Guid> Participants(params Guid?[] values) =>
        values
            .Where(value => value.HasValue && value.Value != Guid.Empty)
            .Select(value => value!.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

    public static bool IsActorIndependent(
        Guid actorUserId,
        IReadOnlyCollection<Guid> prohibitedActorUserIds) =>
        actorUserId != Guid.Empty &&
        prohibitedActorUserIds.Count > 0 &&
        !prohibitedActorUserIds.Contains(actorUserId);

    public static bool IsAutomaticApproval(WorkflowOutcome outcome) =>
        outcome == WorkflowOutcome.Approved;

    public static string NormalizeBypassAttempt(string? value) =>
        value?.Trim() switch
        {
            "AwardAutoApprove" => "AwardAutoApprove",
            "WorkflowAutoApprove" => "WorkflowAutoApprove",
            "FrameworkWorkflowAutoApprove" => "FrameworkWorkflowAutoApprove",
            "DirectStatusApprove" => "DirectStatusApprove",
            _ => "ApprovalBypass"
        };

    public static string BypassCode(string attempt) =>
        NormalizeBypassAttempt(attempt) == "DirectStatusApprove"
            ? "PO_DIRECT_APPROVAL_PROHIBITED"
            : "PO_AUTOMATIC_APPROVAL_PROHIBITED";
}
