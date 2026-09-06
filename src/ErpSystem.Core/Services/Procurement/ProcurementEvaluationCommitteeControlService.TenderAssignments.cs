using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed partial class ProcurementEvaluationCommitteeControlService
{
    /// <summary>
    /// Legacy score rows require a TenderEvaluator FK. Materialize that projection from
    /// the source committee, never as a second membership or permission grant. Drafts
    /// need accepted, conflict-free membership; signed quorum is still required at lock.
    /// </summary>
    public async Task<TenderEvaluator?> EnsureTenderEvaluatorAsync(
        Guid tenderId, string correlationId, CancellationToken cancellationToken = default)
    {
        var control = await LatestTenderAssignmentControlAsync(tenderId, cancellationToken);
        if (control is null) return null;

        await EnforceCapabilityAsync(EvaluatePermission, ProcurementEvaluationSourceType.Tender,
            tenderId, null, correlationId, cancellationToken);
        _ = await ResolveSourceAsync(ProcurementEvaluationSourceType.Tender, tenderId, cancellationToken);
        ValidateTenderDraftMember(control);

        TenderEvaluator? result = null;
        async Task ProjectAsync()
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                $"tender-committee-evaluators:{_currentUser.TenantId:N}:{tenderId:N}", cancellationToken);
            // Re-read under the lock: concurrent first scorers must not create duplicate rows.
            var current = await LatestTenderAssignmentControlAsync(tenderId, cancellationToken)
                ?? throw new ProcurementEvaluationCommitteeConflictException(
                    "EVALUATION_COMMITTEE_REQUIRED", "The source committee is no longer available.");
            ValidateTenderDraftMember(current);
            var assignments = await TenderEvaluators.GetQueryable(item =>
                    item.TenderId == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            var added = new List<Guid>();
            // Project every voting seat, not just the first scorer. Pending or temporarily
            // ineligible members must not disappear from the completion denominator.
            foreach (var member in TenderScoringMembers(current))
            {
                var assignment = assignments.FirstOrDefault(item => item.UserId == member.UserId);
                if (assignment is null)
                {
                    assignment = new TenderEvaluator
                    {
                        Id = Guid.NewGuid(), TenantId = _currentUser.TenantId,
                        TenderId = tenderId, UserId = member.UserId,
                        Role = member.MemberKind.ToString(), Status = "Assigned",
                        AssignedDate = DateTime.UtcNow, AssignedById = _currentUser.UserId,
                        CreatedById = _currentUser.UserId, CreatedAt = DateTime.UtcNow,
                        Notes = $"Source committee {current.Id:D}; appointment {member.Id:D}. Membership and scoring authority remain committee-controlled."
                    };
                    await TenderEvaluators.AddAsync(assignment);
                    assignments.Add(assignment);
                    added.Add(assignment.Id);
                }
                if (member.UserId == _currentUser.UserId) result = assignment;
            }
            if (added.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordAsync(current, "TenderEvaluatorProjectionCreated",
                    ProcurementControlEventResult.Succeeded,
                    new { TenderId = tenderId, CommitteeControlId = current.Id },
                    new { AssignmentIds = added }, correlationId, cancellationToken);
            }
        }

        if (_unitOfWork.HasActiveTransaction)
            await ProjectAsync();
        else
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    await ProjectAsync();
                    await _unitOfWork.CommitAsync(cancellationToken);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }, cancellationToken);
        return result;
    }

    public async Task<IReadOnlyCollection<Guid>?> GetTenderScoringUserIdsAsync(
        Guid tenderId, CancellationToken cancellationToken = default)
    {
        var control = await LatestTenderAssignmentControlAsync(tenderId, cancellationToken);
        if (control is null) return null;
        EnsureReader();
        return TenderScoringMembers(control).Select(item => item.UserId).Distinct().ToArray();
    }

    private Task<ProcurementEvaluationCommitteeControl?> LatestTenderAssignmentControlAsync(
        Guid tenderId, CancellationToken cancellationToken) =>
        ControlQuery().AsNoTracking().Where(item =>
                item.SourceType == ProcurementEvaluationSourceType.Tender && item.SourceId == tenderId)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);

    private IEnumerable<ProcurementEvaluationCommitteeAppointment> TenderScoringMembers(
        ProcurementEvaluationCommitteeControl control) =>
        control.Appointments.Where(item => item.TenantId == _currentUser.TenantId &&
            !item.IsDeleted && item.IsVoting && item.MemberKind != ProcurementCommitteeMemberKind.Observer);

    private void ValidateTenderDraftMember(ProcurementEvaluationCommitteeControl control)
    {
        var now = DateTime.UtcNow;
        var member = TenderScoringMembers(control).FirstOrDefault(item => item.UserId == _currentUser.UserId);
        if (member is null)
            throw new ProcurementEvaluationCommitteeAuthorizationException(
                "The current user is not an appointed voting evaluator for this tender committee.");
        var issues = AppointmentEligibilityIssues(member, now);
        if (control.Status != ProcurementEvaluationCommitteeControlStatus.Active || !IsEffective(control, now))
            issues.Insert(0, "The source evaluation committee is not active and effective.");
        if (issues.Count > 0)
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORER_INELIGIBLE", string.Join(" ", issues));
    }
}
