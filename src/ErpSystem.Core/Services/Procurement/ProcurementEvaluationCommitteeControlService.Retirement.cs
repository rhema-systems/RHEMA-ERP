using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public sealed partial class ProcurementEvaluationCommitteeControlService
{
    public async Task<ProcurementEvaluationCommitteeDto> RetireDraftAsync(
        Guid committeeControlId,
        RetireProcurementEvaluationCommitteeDraftRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Require(request.Reason, "EVALUATION_COMMITTEE_RETIREMENT_REASON_REQUIRED",
            "A reason is required to retire a draft committee control.");
        if (request.Reason.Trim().Length < 10)
            throw Validation("EVALUATION_COMMITTEE_RETIREMENT_REASON_REQUIRED",
                "The retirement reason must contain at least 10 characters.");
        RequireIdempotency(request.IdempotencyKey);

        var control = await LoadControlAsync(committeeControlId, true, cancellationToken);
        await EnforceCapabilityAsync(ManagePermission, control.SourceType, control.SourceId,
            null, correlationId, cancellationToken);
        if (control.Status == ProcurementEvaluationCommitteeControlStatus.Retired &&
            string.Equals(control.RetirementIdempotencyKey, request.IdempotencyKey.Trim(),
                StringComparison.Ordinal))
            return Map(control, await ResolveSourceAsync(control.SourceType, control.SourceId,
                cancellationToken));

        EnsureRowVersion(control.RowVersion, request.RowVersion, "EVALUATION_COMMITTEE");
        if (control.Status != ProcurementEvaluationCommitteeControlStatus.Draft)
            throw Conflict("EVALUATION_COMMITTEE_RETIREMENT_NOT_DRAFT",
                "Only an unactivated draft committee control can be retired.");

        var activityReasons = RetirementActivityReasons(control);
        if (activityReasons.Count != 0)
            throw Conflict("EVALUATION_COMMITTEE_RETIREMENT_ACTIVITY_EXISTS",
                "This draft cannot be retired because substantive committee activity exists: " +
                string.Join(" ", activityReasons));

        var now = DateTime.UtcNow;
        var evidenceReference = ResolveReference(request.EvidenceReference,
            "draft-retirement", "evidence", control.Id, request.IdempotencyKey);
        control.Status = ProcurementEvaluationCommitteeControlStatus.Retired;
        control.RetiredAtUtc = now;
        control.RetiredByUserId = _currentUser.UserId;
        control.RetirementReason = request.Reason.Trim();
        control.RetirementEvidenceReference = evidenceReference;
        control.RetirementIdempotencyKey = request.IdempotencyKey.Trim();
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "DraftRetired", ProcurementControlEventResult.Succeeded,
            new { request.Reason, EvidenceReference = evidenceReference },
            new { control.Status, control.RetiredAtUtc, control.Version },
            correlationId, cancellationToken,
            External(evidenceReference, "Draft committee retirement", "SRC-008"));
        return Map(control, await ResolveSourceAsync(control.SourceType, control.SourceId,
            cancellationToken));
    }

    private static List<string> RetirementActivityReasons(
        ErpSystem.Core.Entities.Procurement.ProcurementEvaluationCommitteeControl control)
    {
        var reasons = new List<string>();
        if (control.ActivatedAtUtc.HasValue || control.ActivatedByUserId.HasValue ||
            !string.IsNullOrWhiteSpace(control.ActivationEvidenceReference) ||
            !string.IsNullOrWhiteSpace(control.ActivationIdempotencyKey))
            reasons.Add("the control has activation evidence;");
        if (control.WorkflowInstanceId.HasValue)
            reasons.Add("a workflow instance is linked;");
        if (control.Appointments.Any(item =>
                item.Status != ProcurementEvaluationAppointmentStatus.Pending ||
                item.AcceptedAtUtc.HasValue ||
                !string.IsNullOrWhiteSpace(item.AcceptanceSignatureReference) ||
                !string.IsNullOrWhiteSpace(item.AcceptanceEvidenceReference) ||
                !string.IsNullOrWhiteSpace(item.AcceptanceIdempotencyKey) ||
                !string.IsNullOrWhiteSpace(item.StatusReason)))
            reasons.Add("an appointment has been responded to;");
        if (control.Appointments.Any(item => item.ConflictDeclarations.Count != 0))
            reasons.Add("a conflict-of-interest declaration exists;");
        if (control.Meetings.Count != 0)
            reasons.Add("a meeting or attendance record exists;");
        if (control.ScoreSheets.Count != 0)
            reasons.Add("a score sheet or recall exists;");
        return reasons;
    }
}
