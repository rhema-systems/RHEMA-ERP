using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The appraisal pipeline's read and override endpoints: where an appraisal is, who may edit it,
/// the raw status transition, and HR's audited advance. Where an appraisal is comes from
/// <see cref="AppraisalGates"/> through <see cref="IAppraisalLifecycleService"/> — the same answer
/// every write path is held to (performance closure B1).
/// </summary>
public class AppraisalWorkflowService : IAppraisalWorkflowService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evalRepository;
    private readonly IGenericRepository<AppraisalHRReview> _hrReviewRepository;
    private readonly IGenericRepository<AppraisalConversation> _conversationRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IGenericRepository<AppraisalManualAdvanceLog> _advanceLogRepository;
    private readonly IAppraisalScoreService _scores;
    private readonly IAppraisalLifecycleService _lifecycle;
    private readonly IAppraisalGoalRowService _goalRows;
    private readonly IPeerNominationService _peerNominations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<AppraisalWorkflowService> _logger;

    public AppraisalWorkflowService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<EvaluatorEvaluation> evalRepository,
        IGenericRepository<AppraisalHRReview> hrReviewRepository,
        IGenericRepository<AppraisalConversation> conversationRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<AppraisalManualAdvanceLog> advanceLogRepository,
        IAppraisalScoreService scores,
        IAppraisalLifecycleService lifecycle,
        IAppraisalGoalRowService goalRows,
        IPeerNominationService peerNominations,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<AppraisalWorkflowService> logger)
    {
        _goalRows             = goalRows;
        _peerNominations      = peerNominations;
        _appraisalRepository  = appraisalRepository;
        _evalRepository       = evalRepository;
        _hrReviewRepository   = hrReviewRepository;
        _conversationRepository = conversationRepository;
        _goalRepository       = goalRepository;
        _advanceLogRepository = advanceLogRepository;
        _scores               = scores;
        _lifecycle            = lifecycle;
        _unitOfWork           = unitOfWork;
        _currentUserProvider  = currentUserProvider;
        _logger               = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An appraisal owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PerformanceAppraisal> GetOwnedAppraisalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal '{id}' not found.", nameof(id));
        return entity;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Where the appraisal is
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// ⚠ This used to be its own walk of the pipeline — no nomination gate, no conversation gates,
    /// goals counted by their appraisal link — so the rail could say "Self-evaluation" while HR's
    /// dashboard said "Goal setting" for the same appraisal. It is the gates' answer now.
    /// </remarks>
    public Task<AppraisalGateState> GetCurrentStepAsync(Guid appraisalId, CancellationToken cancellationToken = default)
        => _lifecycle.GetStateAsync(appraisalId, cancellationToken);

    // ────────────────────────────────────────────────────────────────────────
    //  TransitionAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task TransitionAsync(
        Guid appraisalId,
        AppraisalStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var appraisal = await GetOwnedAppraisalAsync(appraisalId, cancellationToken);

        var from = appraisal.Status;
        AppraisalLifecycle.EnsureTransition(from, newStatus);

        appraisal.Status = newStatus;

        await _appraisalRepository.UpdateAsync(appraisal);

        // Completed by any route settles and publishes in the same save (performance closure A7).
        if (newStatus == AppraisalStatus.Completed)
            await _scores.SettleAsync(appraisalId, AppraisalScoreChangeSource.Settle, publish: true, cancellationToken);
        else
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Appraisal {AppraisalId} transitioned from {From} → {To}.",
            appraisalId, from, newStatus);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  IsEditableByRole
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> IsEditableByRoleAsync(Guid appraisalId, string role, CancellationToken cancellationToken = default)
    {
        var state = await _lifecycle.GetStateAsync(appraisalId, cancellationToken);
        return IsEditableByRole(state, role);
    }

    /// <summary>
    /// Who may write, per the step the appraisal is at — the same windows the write paths enforce:
    /// the employee until their self-evaluation is in, a peer inside the cycle's peer window, the
    /// manager (drafts included) until they submit, HR in governance.
    /// </summary>
    private static bool IsEditableByRole(AppraisalGateState state, string role)
    {
        var r = role.ToLowerInvariant();
        var facts = state.Facts;

        // A remanded appeal is the manager's to re-evaluate and HR's to decide, whatever the status.
        if (facts.Remanded && facts.CurrentAppealStatus == AppraisalAppealStatus.Remanded)
            return r is "manager" or "hr";

        return facts.Status switch
        {
            // Draft (not yet worked on) and Active: by the step — the windows the writes are held to.
            // A Draft appraisal is at a step too: a peer may start alongside the self-evaluation
            // before anyone has saved anything.
            AppraisalStatus.Draft or AppraisalStatus.Active => r switch
            {
                "employee" => AppraisalGates.Check(state.Block,
                    AppraisalSubStatus.GoalSetting, AppraisalSubStatus.PeerNomination, AppraisalSubStatus.SelfEvaluation),
                "peer" => AppraisalGates.Check(state.Block, AppraisalGates.PeerWindow(state.Settings)),
                "manager" => AppraisalGates.Check(state.Block,
                    AppraisalSubStatus.GoalSetting, AppraisalSubStatus.PeerNomination, AppraisalSubStatus.SelfEvaluation,
                    AppraisalSubStatus.PeerEvaluation, AppraisalSubStatus.ManagerEvaluation),
                // HR may manage a Draft appraisal; in Active it governs nothing yet.
                "hr" => facts.Status == AppraisalStatus.Draft,
                _ => false,
            },

            // Governance: calibration/HR review stage; HR acts, manager may be asked to revise
            AppraisalStatus.Governance => r is "hr" or "manager",

            // Appealed: only HR resolves the appeal
            AppraisalStatus.Appealed => r == "hr",

            // Completed, Closed, Withdrawn: read-only (filing an appeal is its own action, not an edit)
            _ => false,
        };
    }

    // ────────────────────────────────────────────────────────────────────────
    //  ManuallyAdvanceStepAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>The appraisal with what the advance's step actions touch, tracked.</summary>
    private async Task<PerformanceAppraisal> LoadForAdvanceAsync(Guid appraisalId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return await _appraisalRepository
            .GetQueryable(a => a.Id == appraisalId && a.TenantId == tenantId)
            .Include(a => a.EvaluatorEvaluations)
            .Include(a => a.HRReviews)
            .Include(a => a.PeerNominations)
            .Include(a => a.Conversations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Appraisal '{appraisalId}' not found.", nameof(appraisalId));
    }

    /// <inheritdoc/>
    public async Task<ManualAdvanceResult> ManuallyAdvanceStepAsync(
        Guid appraisalId,
        AppraisalSubStatus? targetSubStatus,
        string reason,
        Guid advancedByEmployeeId,
        CancellationToken ct = default)
    {
        var state = await _lifecycle.GetStateAsync(appraisalId, ct);
        var previousMajorStatus = state.Facts.Status;
        var previousSubStatus   = state.SubStatus;

        // Guard: terminal and appeal states cannot be advanced — appeals move by their own decisions.
        if (previousSubStatus is AppraisalSubStatus.Completed
            or AppraisalSubStatus.Closed
            or AppraisalSubStatus.Withdrawn
            or AppraisalSubStatus.AppealSubmitted
            or AppraisalSubStatus.AppealUnderReview
            or AppraisalSubStatus.AppealResolved)
        {
            return new ManualAdvanceResult
            {
                Success              = false,
                ErrorMessage         = $"Cannot manually advance an appraisal that is in sub-status '{previousSubStatus}'.",
                PreviousSubStatus    = previousSubStatus,
                NewSubStatus         = previousSubStatus,
                PreviousMajorStatus  = previousMajorStatus,
                NewMajorStatus       = previousMajorStatus,
            };
        }

        // The step advanced past is the one the appraisal is at (B1). A target anywhere else used to
        // run that step's actions wherever the appraisal stood — marking an appraisal calibrated
        // before its manager had scored it, approving goals on one already in governance.
        if (targetSubStatus is AppraisalSubStatus target
            && AppraisalGates.StepOf(target) != AppraisalGates.StepOf(previousSubStatus))
        {
            throw new AppraisalGateException(previousSubStatus,
                $"This appraisal is at {AppraisalGates.Label(previousSubStatus)}, not {AppraisalGates.Label(target)}: " +
                "HR's advance moves an appraisal past the step it is at.");
        }

        var stepToComplete = previousSubStatus;
        var appraisal = await LoadForAdvanceAsync(appraisalId, ct);
        var actions = new List<string>();
        var now     = DateTime.UtcNow;
        IReadOnlyList<PeerNomination> approvedByAdvance = Array.Empty<PeerNomination>();

        // The advance is work on the appraisal, as a first save is: a Draft one is opened.
        if (appraisal.Status == AppraisalStatus.Draft)
        {
            appraisal.Status = AppraisalStatus.Active;
            actions.Add("Opened the appraisal (Draft → Active).");
        }

        // ── Per-step auto-completion ─────────────────────────────────────────

        switch (stepToComplete)
        {
            case AppraisalSubStatus.GoalSetting:
            {
                // The goals the gate counts: the employee's in this cycle, not only those linked to
                // the appraisal (a goal agreed before generation has no link).
                //
                // HR's audited waiver of goal setting (closure plan L2, D-16): the goals the
                // employee put to the manager are approved on HR's recorded reason, and the agreed
                // set is locked, so the year is appraised on what was agreed. A draft was never put
                // to the manager and a rejected goal was refused by them — neither joins the set on
                // a waiver; they used to be approved along with the rest. The lock is the flag only
                // (D-29): the goals' year runs on.
                var goals = await _goalRepository.GetQueryable()
                    .Where(g => g.TenantId == appraisal.TenantId
                             && g.EmployeeId == appraisal.EmployeeId
                             && g.AppraisalCycleId == appraisal.AppraisalCycleId)
                    .ToListAsync(ct);

                var submitted = goals.Where(g => g.Status == GoalStatus.PendingApproval).ToList();
                foreach (var g in submitted)
                {
                    g.Status       = GoalStatus.Approved;
                    g.ApprovalDate = now;
                }

                var toLock = goals
                    .Where(g => GoalSetRules.IsAgreed(g.Status) && !GoalSetRules.IsLocked(g.IsLocked, g.Status))
                    .ToList();
                foreach (var g in toLock)
                {
                    g.IsLocked   = true;
                    g.LockedDate = now;
                }

                foreach (var g in submitted.Union(toLock))
                    await _goalRepository.UpdateAsync(g);

                var leftOut = goals.Count(g => g.Status is GoalStatus.Draft or GoalStatus.Rejected);
                if (submitted.Count > 0)
                    actions.Add($"Approved {submitted.Count} submitted goal(s) on HR's recorded reason.");
                if (toLock.Count > 0)
                    actions.Add($"Locked the agreed goal set: {toLock.Count} goal(s).");
                if (leftOut > 0)
                    actions.Add($"Left {leftOut} draft or rejected goal(s) out of the set.");
                actions.Add($"Waived goal setting: {state.Block.Reason}.");
                break;
            }

            case AppraisalSubStatus.PeerNomination:
            {
                // D-39: through the one approval path — each peer's evaluation, the count, the due
                // date — and the peers are told once the advance is saved. The status alone was set,
                // so the "approved" peers had no form to fill in and heard nothing.
                var pending = appraisal.PeerNominations
                    .Where(n => n.NominationStatus == PeerNominationStatus.Pending)
                    .ToList();

                approvedByAdvance = await _peerNominations.StageApprovalAsync(appraisal, pending, null, ct);

                actions.Add($"Approved {approvedByAdvance.Count} pending nomination(s), each peer asked for their feedback. Minimum peer requirement bypassed by HR.");
                actions.Add($"Waived peer nomination: {state.Block.Reason}.");
                break;
            }

            case AppraisalSubStatus.SelfEvaluation:
            {
                // Waived, not written: the employee's own judgement cannot be submitted for them. A
                // draft stays a draft and — like every unsubmitted evaluation — counts in no score
                // (lane A). This used to submit the draft on the employee's behalf, or create an empty
                // "submitted" one, only so the gate would pass.
                var hasDraft = appraisal.EvaluatorEvaluations
                    .Any(e => e.EvaluatorRole == EvaluatorRole.Self && e.SubmittedDate == null);
                actions.Add(hasDraft
                    ? "Waived the self-evaluation: the employee's draft stays a draft and does not count in the score."
                    : "Waived the self-evaluation: the employee has not written one.");
                break;
            }

            case AppraisalSubStatus.PeerEvaluation:
            {
                // Expire all unsubmitted peer evals
                var pendingPeerEvals = appraisal.EvaluatorEvaluations
                    .Where(e => e.EvaluatorRole == EvaluatorRole.Peer && e.SubmittedDate == null)
                    .ToList();

                foreach (var e in pendingPeerEvals)
                {
                    e.SubmittedDate  = now;
                    e.OverallNotes   = (e.OverallNotes ?? "") + $" [Auto-submitted by HR: {reason}]";
                    await _evalRepository.UpdateAsync(e);
                }

                actions.Add($"Auto-submitted {pendingPeerEvals.Count} pending peer evaluation(s). Minimum threshold bypassed by HR.");
                actions.Add($"Waived peer evaluation: {state.Block.Reason}.");
                break;
            }

            case AppraisalSubStatus.ManagerEvaluation:
            {
                var managerEval = appraisal.EvaluatorEvaluations
                    .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate == null);

                if (managerEval != null)
                {
                    managerEval.SubmittedDate = now;
                    if (managerEval.TotalScore.HasValue && appraisal.PreCalibrationScore == null)
                        appraisal.PreCalibrationScore = managerEval.TotalScore;
                    await _evalRepository.UpdateAsync(managerEval);
                    actions.Add("Auto-submitted existing manager evaluation draft.");
                }
                else
                {
                    var newEval = new EvaluatorEvaluation
                    {
                        AppraisalId     = appraisalId,
                        EvaluatorId     = advancedByEmployeeId,
                        EvaluatorRole   = EvaluatorRole.Manager,
                        EvaluatorWeight = 1,
                        StartedDate     = now,
                        SubmittedDate   = now,
                        OverallNotes    = $"[Auto-created by HR manual advance: {reason}]",
                    };
                    SetTenantId(newEval, appraisal);
                    await _evalRepository.AddAsync(newEval);
                    actions.Add("Created placeholder manager evaluation record (no prior draft existed).");
                }
                break;
            }

            case AppraisalSubStatus.PendingCalibration:
            case AppraisalSubStatus.CalibrationInProgress:
            {
                appraisal.IsCalibrated = true;
                actions.Add("Bypassed calibration requirement — marked appraisal as calibrated without a session.");
                break;
            }

            case AppraisalSubStatus.PendingHRReview:
            case AppraisalSubStatus.HRReviewInProgress:
            {
                // Close any open HR review records first
                var openReviews = appraisal.HRReviews
                    .Where(r => r.ReviewCompletedDate == null)
                    .ToList();
                foreach (var r in openReviews)
                {
                    r.ReviewCompletedDate = now;
                    r.IsApproved          = true;
                    r.HRNotes             = (r.HRNotes ?? "") + $" [Auto-approved by HR manual advance: {reason}]";
                    await _hrReviewRepository.UpdateAsync(r);
                }

                if (!openReviews.Any())
                {
                    // Create a completed HR review
                    var review = new AppraisalHRReview
                    {
                        AppraisalId           = appraisalId,
                        ReviewedByHRId        = advancedByEmployeeId,
                        ReviewStartedDate     = now,
                        ReviewCompletedDate   = now,
                        IsApproved            = true,
                        HRNotes               = $"[Auto-approved by HR manual advance: {reason}]",
                    };
                    SetTenantId(review, appraisal);
                    await _hrReviewRepository.AddAsync(review);
                    actions.Add("Created and auto-approved HR review record.");
                }
                else
                {
                    actions.Add($"Auto-approved {openReviews.Count} pending HR review(s).");
                }
                break;
            }

            case AppraisalSubStatus.PendingConversation:
            {
                // Mark any existing incomplete final-review conversation as done, or create one
                var conv = appraisal.Conversations
                    .FirstOrDefault(c => c.Type == ConversationType.FinalReview && !c.IsCompleted);

                if (conv != null)
                {
                    conv.IsCompleted = true;
                    conv.HeldDate    = now;
                    await _conversationRepository.UpdateAsync(conv);
                    actions.Add("Marked existing final-review conversation as completed.");
                }
                else
                {
                    var newConv = new AppraisalConversation
                    {
                        AppraisalId   = appraisalId,
                        Type          = ConversationType.FinalReview,
                        ConductedById = advancedByEmployeeId,
                        HeldDate      = now,
                        IsCompleted   = true,
                        PostMeetingNotes = $"[Auto-created by HR manual advance: {reason}]",
                    };
                    SetTenantId(newConv, appraisal);
                    await _conversationRepository.AddAsync(newConv);
                    actions.Add("Created placeholder final-review conversation record and marked as completed.");
                }
                break;
            }

            case AppraisalSubStatus.PendingAcknowledgment:
            {
                appraisal.EmployeeAcknowledged         = true;
                appraisal.EmployeeAcknowledgedDate      = now;
                appraisal.EmployeeAcknowledgmentComments =
                    $"[Auto-acknowledged by HR manual advance: {reason}]";
                actions.Add("Auto-acknowledged appraisal on behalf of employee.");
                break;
            }
        }

        // ── The audit log row — for a step before the manager's evaluation, the waiver itself ──
        // Written before the sync, because the gates read it: a waived step is behind the appraisal
        // from this row on, however few goals, nominations or peers it has.
        var auditLog = new AppraisalManualAdvanceLog
        {
            PerformanceAppraisalId = appraisalId,
            AdvancedByEmployeeId   = advancedByEmployeeId,
            AdvancedDate           = now,
            FromSubStatus          = previousSubStatus.ToString(),
            ToSubStatus            = previousSubStatus.ToString(),
            FromMajorStatus        = previousMajorStatus.ToString(),
            ToMajorStatus          = appraisal.Status.ToString(),
            Reason                 = reason,
        };
        SetTenantId(auditLog, appraisal);
        await _advanceLogRepository.AddAsync(auditLog);
        await _unitOfWork.SaveChangesAsync(ct);

        // The peers the advance approved are asked, as an approval by the manager asks them (D-39).
        await _peerNominations.NotifyApprovedAsync(appraisalId, approvedByAdvance, null, ct);

        // HR's waiver of goal setting locked the agreed set, so the appraisal's goals section
        // follows it (closure plan L2): one row per locked goal, when the template has one.
        if (stepToComplete == AppraisalSubStatus.GoalSetting)
        {
            var built = await _goalRows.RebuildAsync(appraisal.EmployeeId, appraisal.AppraisalCycleId, ct);
            if (built > 0)
                actions.Add($"Built the goals section: {built} goal row(s).");
        }

        // ── Where the gates put it now: the major status follows, and completion settles ────
        // An advance that completes the appraisal settles its score and publishes it, like every
        // other way to Completed (performance closure A7) — it used to finish with whatever score
        // was stored, often none.
        var sync = await _lifecycle.SyncAsync(appraisalId, AppraisalScoreChangeSource.Advance, publish: true, ct);
        var newSubStatus   = sync.SubStatus;
        var newMajorStatus = sync.StatusAfter;

        if (sync.Moved)
        {
            actions.Add($"Appraisal major status auto-transitioned: {sync.StatusBefore} → {sync.StatusAfter}.");
            _logger.LogInformation(
                "ManualAdvance: Appraisal {Id} major status auto-transitioned {From} → {To}.",
                appraisalId, sync.StatusBefore, sync.StatusAfter);
        }

        if (sync.Settle is { } settled)
        {
            actions.Add(settled.ScoreAfter is decimal score
                ? $"Settled the overall score at {score:0.##}."
                : "Settled the overall score: no submitted evaluation scored anything, so it has none.");
        }

        auditLog.ToSubStatus          = newSubStatus.ToString();
        auditLog.ToMajorStatus        = newMajorStatus.ToString();
        auditLog.ActionsPerformedJson = JsonSerializer.Serialize(actions);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "HR manually advanced appraisal {Id}: {From} → {To} ({MajorFrom} → {MajorTo}). Reason: {Reason}",
            appraisalId, previousSubStatus, newSubStatus, previousMajorStatus, newMajorStatus, reason);

        return new ManualAdvanceResult
        {
            Success             = true,
            PreviousSubStatus   = previousSubStatus,
            NewSubStatus        = newSubStatus,
            PreviousMajorStatus = previousMajorStatus,
            NewMajorStatus      = newMajorStatus,
            ActionsPerformed    = actions,
        };
    }

    // ────────────────────────────────────────────────────────────────────────
    //  AdvanceOverdueAppraisalsAsync (AutoLockOnDeadline — Phase 2E)
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<DeadlineEnforcementResult> AdvanceOverdueAppraisalsAsync(
        Guid cycleId, Guid advancedByEmployeeId, CancellationToken ct = default)
    {
        var result = new DeadlineEnforcementResult();

        var tenantId = GetTenantId();
        var appraisals = await _appraisalRepository
            .GetQueryable(a => a.TenantId == tenantId
                            && a.AppraisalCycleId == cycleId
                            && a.Status != AppraisalStatus.Completed
                            && a.Status != AppraisalStatus.Closed
                            && a.Status != AppraisalStatus.Appealed
                            && a.Status != AppraisalStatus.Withdrawn)
            .Include(a => a.AppraisalCycle).ThenInclude(c => c!.AppraisalSettings)
            .AsNoTracking()
            .ToListAsync(ct);

        var settings = appraisals.FirstOrDefault()?.AppraisalCycle?.AppraisalSettings;
        result.AutoLockEnabled = settings?.AutoLockOnDeadline ?? false;

        // Respect the setting: when auto-lock is off (or nothing to do), advance nothing.
        if (!result.AutoLockEnabled || settings is null || appraisals.Count == 0)
            return result;

        var states = await _lifecycle.GetStatesAsync(appraisals.Select(a => a.Id).ToList(), ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var appraisal in appraisals)
        {
            result.Evaluated++;

            if (!states.TryGetValue(appraisal.Id, out var state))
                continue;

            var subStatus = state.SubStatus;
            var deadline  = DeadlineForSubStatus(appraisal.AppraisalCycle!, subStatus);

            // Only act on a configured, already-passed deadline.
            if (deadline is null || deadline.Value >= today)
                continue;

            try
            {
                var advance = await ManuallyAdvanceStepAsync(
                    appraisal.Id, subStatus,
                    $"Auto-advanced past overdue '{subStatus}' deadline ({deadline:yyyy-MM-dd}).",
                    advancedByEmployeeId, ct);

                if (advance.Success)
                {
                    result.Advanced++;
                    result.Messages.Add($"{appraisal.AppraisalNumber ?? appraisal.Id.ToString()}: {subStatus} → {advance.NewSubStatus}");
                }
                else
                {
                    result.Messages.Add($"{appraisal.AppraisalNumber ?? appraisal.Id.ToString()}: skipped ({advance.ErrorMessage})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Advance-overdue failed for appraisal {AppraisalId}", appraisal.Id);
                result.Messages.Add($"{appraisal.AppraisalNumber ?? appraisal.Id.ToString()}: error ({ex.Message})");
            }
        }

        _logger.LogInformation(
            "Advance-overdue for cycle {CycleId}: evaluated {Evaluated}, advanced {Advanced}.",
            cycleId, result.Evaluated, result.Advanced);

        return result;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  GetTransitionReportAsync (performance closure B8)
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AppraisalTransitionReportDto> GetTransitionReportAsync(Guid cycleId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        // In flight: what the sweep above examines.
        var appraisals = await _appraisalRepository
            .GetQueryable(a => a.TenantId == tenantId
                            && a.AppraisalCycleId == cycleId
                            && a.Status != AppraisalStatus.Completed
                            && a.Status != AppraisalStatus.Closed
                            && a.Status != AppraisalStatus.Appealed
                            && a.Status != AppraisalStatus.Withdrawn)
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.AppraisalNumber,
                a.EmployeeId,
                a.Status,
                EmployeeName = a.Employee.FirstName + " " + a.Employee.LastName,
                a.Employee.EmployeeNumber,
                CycleName = a.AppraisalCycle.CycleName,
            })
            .ToListAsync(ct);

        var report = new AppraisalTransitionReportDto
        {
            CycleId = cycleId,
            CycleName = appraisals.FirstOrDefault()?.CycleName,
            GeneratedAt = DateTime.UtcNow,
            Examined = appraisals.Count,
        };
        if (appraisals.Count == 0)
            return report;

        var states = await _lifecycle.GetStatesAsync(appraisals.Select(a => a.Id).ToList(), ct);
        foreach (var appraisal in appraisals.OrderBy(a => a.EmployeeName))
        {
            if (!states.TryGetValue(appraisal.Id, out var state))
                continue;

            var ahead = AppraisalGates.RecordedAhead(state.Facts, state.Settings, state.SubStatus);
            if (ahead.Count == 0)
                continue;

            report.Rows.Add(new AppraisalTransitionRowDto
            {
                AppraisalId = appraisal.Id,
                AppraisalNumber = appraisal.AppraisalNumber,
                EmployeeId = appraisal.EmployeeId,
                EmployeeName = appraisal.EmployeeName.Trim(),
                EmployeeNumber = appraisal.EmployeeNumber,
                Status = appraisal.Status,
                SubStatus = state.SubStatus,
                StepLabel = state.StepLabel,
                Reason = state.Block.Reason,
                RecordedAhead = ahead.ToList(),
                CanWaive = AppraisalGates.CanBeWaived(state.SubStatus),
            });
        }

        return report;
    }

    /// <summary>Maps a blocking sub-status to the cycle phase deadline that governs it.</summary>
    private static DateOnly? DeadlineForSubStatus(AppraisalCycle cycle, AppraisalSubStatus subStatus) => subStatus switch
    {
        AppraisalSubStatus.GoalSetting           => cycle.GoalSettingDeadline,
        AppraisalSubStatus.PeerNomination        => cycle.PeerNominationDeadline,
        AppraisalSubStatus.SelfEvaluation        => cycle.SelfEvaluationDeadline,
        AppraisalSubStatus.PeerEvaluation        => cycle.PeerEvaluationDeadline,
        AppraisalSubStatus.ManagerEvaluation     => cycle.ManagerEvaluationDeadline,
        AppraisalSubStatus.PendingCalibration
            or AppraisalSubStatus.CalibrationInProgress => cycle.CalibrationDeadline,
        AppraisalSubStatus.PendingHRReview
            or AppraisalSubStatus.HRReviewInProgress    => cycle.HRReviewDeadline,
        AppraisalSubStatus.PendingConversation   => cycle.FinalConversationDeadline,
        AppraisalSubStatus.PendingAcknowledgment => cycle.EmployeeAcknowledgeDeadline,
        _ => null,
    };

    // ── Tenant propagation helper ───────────────────────────────────────────

    /// <summary>
    /// Copies TenantId and CreatedBy from the parent appraisal to a new TenantEntity so that
    /// auto-created child records pass multi-tenancy validation.
    /// </summary>
    private static void SetTenantId(TenantEntity target, PerformanceAppraisal source)
    {
        target.TenantId = source.TenantId;
    }
}
