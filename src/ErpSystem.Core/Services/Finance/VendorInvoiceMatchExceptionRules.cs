using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Services.Finance;

public static class VendorInvoiceMatchExceptionRules
{
    public const string RuleCode = "AP-006";
    public const string RuleVersion = "TDC-0507";
    public const string EventType = "VendorInvoiceMatchException";
    public const string WorkflowEntityType = "VendorInvoiceMatchException";
    public const string ApprovalAction = "InvoiceMatchExceptionApproved";
    public const string RejectionAction = "InvoiceMatchExceptionRejected";
    public const string RequestAction = "InvoiceMatchExceptionRequested";
    public const string CancellationAction = "InvoiceMatchExceptionCancelled";
    public const string CorrectiveActionCompleted = "InvoiceMatchExceptionCorrectiveActionCompleted";
    public const string RootCauseEvidenceKey = "ROOT_CAUSE_EVIDENCE";
    public const string CorrectiveActionEvidenceKey = "CORRECTIVE_ACTION_PLAN";
    public const string CorrectiveCompletionEvidenceKey = "CORRECTIVE_ACTION_COMPLETION";

    public static readonly IReadOnlyList<string> DecisionKeys = Enumerable.Range(1, 14)
        .Select(number => $"DEC-{number:000}")
        .ToArray();

    public static readonly IReadOnlyList<string> RequiredEvidenceKeys =
        new[] { RootCauseEvidenceKey, CorrectiveActionEvidenceKey };

    public static bool IsExceptionable(InvoiceMatchingResultDto readiness) =>
        readiness.IsRequired &&
        !readiness.IsMatched &&
        readiness.Discrepancies.Count > 0 &&
        readiness.Checks.All(check => check.Passed || check.ExceptionEligible);

    public static VendorInvoiceMatchExceptionStatus EffectiveStatus(
        VendorInvoiceMatchExceptionStatus status,
        DateTime expiresAtUtc,
        DateTime nowUtc) =>
        (status is VendorInvoiceMatchExceptionStatus.PendingApproval or
            VendorInvoiceMatchExceptionStatus.Approved) && expiresAtUtc <= nowUtc
            ? VendorInvoiceMatchExceptionStatus.Expired
            : status;

    public static bool CanDecide(VendorInvoiceMatchExceptionStatus status) =>
        status == VendorInvoiceMatchExceptionStatus.PendingApproval;

    public static bool CanCancel(VendorInvoiceMatchExceptionStatus status) =>
        status == VendorInvoiceMatchExceptionStatus.PendingApproval;

    public static bool CanCompleteCorrectiveAction(
        VendorInvoiceMatchExceptionStatus status,
        VendorInvoiceMatchCorrectiveActionStatus correctiveStatus,
        DateTime expiresAtUtc,
        DateTime nowUtc) =>
        status == VendorInvoiceMatchExceptionStatus.Approved &&
        correctiveStatus == VendorInvoiceMatchCorrectiveActionStatus.Planned;

    public static bool IsIndependent(
        Guid actorId,
        Guid requesterId,
        Guid? invoiceProcessorId,
        IEnumerable<Guid> priorApproverIds) =>
        actorId != Guid.Empty &&
        actorId != requesterId &&
        (!invoiceProcessorId.HasValue || actorId != invoiceProcessorId.Value) &&
        priorApproverIds.All(id => id != actorId);

    public static string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }
}
