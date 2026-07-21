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
/// Provides lifecycle phase computation, transition enforcement, and role-edit guards
/// for <see cref="PerformanceAppraisal"/> entities.
/// This service never persists evaluation data — it only reads/updates <c>Status</c>.
/// </summary>
public class AppraisalWorkflowService : IAppraisalWorkflowService
{
    // ────────────────────────────────────────────────────────────────────────
    //  Allowed lifecycle transitions for the 6-value AppraisalStatus enum.
    //  Key   = current status
    //  Value = set of valid next states
    // ────────────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<AppraisalStatus, HashSet<AppraisalStatus>> AllowedTransitions =
        new Dictionary<AppraisalStatus, HashSet<AppraisalStatus>>
        {
            // Draft → Active: HR/manager opens appraisal for employee action
            [AppraisalStatus.Draft] = new() { AppraisalStatus.Active },

            // Active → Governance: manager submitted, escalated to HR/Calibration
            // Active → Completed: shortcut when neither HR review nor acknowledgment required
            // Active → Appealed: rare edge case guard
            [AppraisalStatus.Active] = new()
            {
                AppraisalStatus.Governance,
                AppraisalStatus.Completed,
                AppraisalStatus.Appealed,
            },

            // Governance → Completed: HR finalised, employee acknowledged (or ack not required)
            // Governance → Appealed: appeal lodged while in HR governance
            [AppraisalStatus.Governance] = new()
            {
                AppraisalStatus.Completed,
                AppraisalStatus.Appealed,
                AppraisalStatus.Active,   // HR can return to employee/manager for corrections
            },

            // Completed → Appealed: employee files appeal after acknowledgment
            // Completed → Closed: HR closes out the cycle record
            [AppraisalStatus.Completed] = new()
            {
                AppraisalStatus.Appealed,
                AppraisalStatus.Closed,
            },

            // Appealed → Completed: appeal resolved — back to completed state
            // Appealed → Closed: appeal resolved and HR closes immediately
            [AppraisalStatus.Appealed] = new()
            {
                AppraisalStatus.Completed,
                AppraisalStatus.Closed,
            },

            // Terminal — no transitions out of Closed
            [AppraisalStatus.Closed] = new(),
        };

    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evalRepository;
    private readonly IGenericRepository<PeerNomination> _nominationRepository;
    private readonly IGenericRepository<AppraisalHRReview> _hrReviewRepository;
    private readonly IGenericRepository<AppraisalConversation> _conversationRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IGenericRepository<AppraisalManualAdvanceLog> _advanceLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalWorkflowService> _logger;

    public AppraisalWorkflowService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<EvaluatorEvaluation> evalRepository,
        IGenericRepository<PeerNomination> nominationRepository,
        IGenericRepository<AppraisalHRReview> hrReviewRepository,
        IGenericRepository<AppraisalConversation> conversationRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<AppraisalManualAdvanceLog> advanceLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalWorkflowService> logger)
    {
        _appraisalRepository  = appraisalRepository;
        _evalRepository       = evalRepository;
        _nominationRepository = nominationRepository;
        _hrReviewRepository   = hrReviewRepository;
        _conversationRepository = conversationRepository;
        _goalRepository       = goalRepository;
        _advanceLogRepository = advanceLogRepository;
        _unitOfWork           = unitOfWork;
        _logger               = logger;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  GetCurrentPhase
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Requires the following navigation properties to be loaded on <paramref name="appraisal"/>:
    /// <list type="bullet">
    ///   <item><c>AppraisalCycle.AppraisalSettings</c></item>
    ///   <item><c>Goals</c></item>
    ///   <item><c>EvaluatorEvaluations</c></item>
    ///   <item><c>HRReviews</c></item>
    /// </list>
    /// </remarks>
    public AppraisalPhase GetCurrentPhase(PerformanceAppraisal appraisal)
    {
        var settings = appraisal.AppraisalCycle?.AppraisalSettings
            ?? throw new InvalidOperationException(
                "AppraisalCycle.AppraisalSettings must be loaded before calling GetCurrentPhase.");

        // ── 1. Goal-setting gate ────────────────────────────────────────────
        // Phase stays at GoalSetting until all goals are past Draft/PendingApproval/Rejected.
        if (settings.RequireGoalSetting)
        {
            bool goalsReady = appraisal.Goals.Any()
                && appraisal.Goals.All(g =>
                    g.Status != GoalStatus.Draft
                    && g.Status != GoalStatus.PendingApproval
                    && g.Status != GoalStatus.Rejected);

            if (!goalsReady)
                return AppraisalPhase.GoalSetting;
        }

        // ── 2. Self-evaluation ──────────────────────────────────────────────
        if (settings.RequireSelfEvaluation)
        {
            bool selfSubmitted = appraisal.EvaluatorEvaluations
                .Any(e => e.EvaluatorRole == EvaluatorRole.Self && e.SubmittedDate != null);

            if (!selfSubmitted)
                return AppraisalPhase.SelfEvaluation;
        }

        // ── 3. Peer evaluation ──────────────────────────────────────────────
        if (settings.RequirePeerReviews && settings.MinPeerEvaluators > 0)
        {
            int submittedPeerCount = appraisal.EvaluatorEvaluations
                .Count(e => e.EvaluatorRole == EvaluatorRole.Peer && e.SubmittedDate != null);

            if (submittedPeerCount < settings.MinPeerEvaluators)
                return AppraisalPhase.PeerEvaluation;
        }

        // ── 4. Manager evaluation ───────────────────────────────────────────
        if (settings.RequireManagerEvaluation)
        {
            bool managerSubmitted = appraisal.EvaluatorEvaluations
                .Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate != null);

            if (!managerSubmitted)
                return AppraisalPhase.ManagerEvaluation;
        }

        // ── 5/6. Calibration and HR Review ──────────────────────────────────
        // Step order depends on HRReviewTiming:
        //   AfterCalibration  (default): Calibration → HR Review
        //   BeforeCalibration          : HR Review → Calibration
        if (settings.HRReviewTiming == HRReviewTiming.BeforeCalibration)
        {
            if (settings.RequireHRReview)
            {
                bool hrApproved = appraisal.HRReviews
                    .Any(r => r.ReviewCompletedDate != null && r.IsApproved);
                if (!hrApproved)
                    return AppraisalPhase.HRReview;
            }

            if (settings.RequireCalibration && !appraisal.IsCalibrated)
                return AppraisalPhase.Calibration;
        }
        else // AfterCalibration (default)
        {
            if (settings.RequireCalibration && !appraisal.IsCalibrated)
                return AppraisalPhase.Calibration;

            if (settings.RequireHRReview)
            {
                bool hrApproved = appraisal.HRReviews
                    .Any(r => r.ReviewCompletedDate != null && r.IsApproved);
                if (!hrApproved)
                    return AppraisalPhase.HRReview;
            }
        }

        // ── 7. Employee acknowledgment ──────────────────────────────────────
        if (settings.RequireEmployeeAcknowledgment && !appraisal.EmployeeAcknowledged)
            return AppraisalPhase.EmployeeReview;

        // ── 8. All gates passed ────────────────────────────────────────────
        return AppraisalPhase.Closed;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  TransitionAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task TransitionAsync(
        Guid appraisalId,
        AppraisalStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetByIdAsync(appraisalId)
            ?? throw new ArgumentException(
                $"Appraisal '{appraisalId}' not found.", nameof(appraisalId));

        var from = appraisal.Status;

        if (!AllowedTransitions.TryGetValue(from, out var validNext) || !validNext.Contains(newStatus))
        {
            var allowed = validNext?.Count > 0
                ? string.Join(", ", validNext)
                : "none (terminal state)";

            throw new InvalidOperationException(
                $"Invalid appraisal lifecycle transition from '{from}' to '{newStatus}'. " +
                $"Allowed transitions from '{from}': {allowed}.");
        }

        appraisal.Status = newStatus;

        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Appraisal {AppraisalId} transitioned from {From} → {To}.",
            appraisalId, from, newStatus);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  IsEditableByRole
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Requires <c>AppraisalCycle.AppraisalSettings</c>, <c>Goals</c>,
    /// <c>EvaluatorEvaluations</c>, and <c>HRReviews</c> loaded on <paramref name="appraisal"/>
    /// when the status is <see cref="AppraisalStatus.Active"/>.
    /// </remarks>
    public bool IsEditableByRole(PerformanceAppraisal appraisal, string role)
    {
        // Terminal state — nobody can edit
        if (appraisal.Status == AppraisalStatus.Closed)
            return false;

        var r = role.ToLowerInvariant();

        return appraisal.Status switch
        {
            // Draft: goals are being set / approved — employee and manager may act; HR may manage
            AppraisalStatus.Draft => r is "employee" or "manager" or "hr",

            // Active: edit rights depend on the current fine-grained phase
            AppraisalStatus.Active => r switch
            {
                "employee" => IsInPhase(appraisal, AppraisalPhase.GoalSetting, AppraisalPhase.SelfEvaluation),
                // In WithSelfEval mode peers can evaluate in parallel with self-evaluation;
                // in AfterSelfEval mode peers must wait until self-evaluation is complete.
                "peer" => appraisal.AppraisalCycle?.AppraisalSettings?.PeerEvaluationOpenMode
                              == PeerEvaluationOpenMode.WithSelfEval
                          ? IsInPhase(appraisal, AppraisalPhase.SelfEvaluation, AppraisalPhase.PeerEvaluation)
                          : IsInPhase(appraisal, AppraisalPhase.PeerEvaluation),
                "manager"  => IsInPhase(appraisal, AppraisalPhase.GoalSetting, AppraisalPhase.ManagerEvaluation),
                "hr"       => false,   // HR governs in Governance status, not Active
                _          => false,
            },

            // Governance: calibration/HR review stage; HR acts, manager may be asked to revise
            AppraisalStatus.Governance => r is "hr" or "manager",

            // Appealed: only HR resolves the appeal
            AppraisalStatus.Appealed => r == "hr",

            // Completed: read-only (appeal filing is a distinct explicit action, not an edit)
            AppraisalStatus.Completed => false,

            _ => false,
        };
    }

    // ── private helpers ─────────────────────────────────────────────────────

    private bool IsInPhase(PerformanceAppraisal appraisal, params AppraisalPhase[] phases)
    {
        var current = GetCurrentPhase(appraisal);
        return Array.IndexOf(phases, current) >= 0;
    }

    private async Task<PerformanceAppraisal> LoadWithNavigationsAsync(Guid appraisalId, CancellationToken cancellationToken)
    {
        var appraisal = await _appraisalRepository
            .GetQueryable(a => a.Id == appraisalId)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c!.AppraisalSettings)
            .Include(a => a.Goals)
            .Include(a => a.EvaluatorEvaluations)
            .Include(a => a.HRReviews)
            .Include(a => a.PeerNominations)
            .Include(a => a.Conversations)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Appraisal '{appraisalId}' not found.", nameof(appraisalId));

        return appraisal;
    }

    /// <inheritdoc/>
    public async Task<AppraisalPhase> GetCurrentPhaseAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await LoadWithNavigationsAsync(appraisalId, cancellationToken);
        return GetCurrentPhase(appraisal);
    }

    /// <inheritdoc/>
    public async Task<bool> IsEditableByRoleAsync(Guid appraisalId, string role, CancellationToken cancellationToken = default)
    {
        var appraisal = await LoadWithNavigationsAsync(appraisalId, cancellationToken);
        return IsEditableByRole(appraisal, role);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  ManuallyAdvanceStepAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ManualAdvanceResult> ManuallyAdvanceStepAsync(
        Guid appraisalId,
        AppraisalSubStatus? targetSubStatus,
        string reason,
        Guid advancedByEmployeeId,
        CancellationToken ct = default)
    {
        var appraisal = await LoadWithNavigationsAsync(appraisalId, ct);
        var settings  = appraisal.AppraisalCycle?.AppraisalSettings
            ?? throw new InvalidOperationException("AppraisalCycle.AppraisalSettings must be loaded.");

        var previousMajorStatus = appraisal.Status;
        var previousSubStatus   = AppraisalSubStatusResolver.Resolve(appraisal, settings);

        // Determine which sub-step to complete (caller may specify or we auto-detect)
        var stepToComplete = targetSubStatus ?? previousSubStatus;

        // Guard: terminal states cannot be advanced
        if (stepToComplete is AppraisalSubStatus.Completed
            or AppraisalSubStatus.Closed
            or AppraisalSubStatus.AppealSubmitted
            or AppraisalSubStatus.AppealUnderReview
            or AppraisalSubStatus.AppealResolved)
        {
            return new ManualAdvanceResult
            {
                Success              = false,
                ErrorMessage         = $"Cannot manually advance an appraisal that is in sub-status '{stepToComplete}'.",
                PreviousSubStatus    = previousSubStatus,
                NewSubStatus         = previousSubStatus,
                PreviousMajorStatus  = previousMajorStatus,
                NewMajorStatus       = previousMajorStatus,
            };
        }

        var actions = new List<string>();
        var now     = DateTime.UtcNow;

        // ── Per-step auto-completion ─────────────────────────────────────────

        switch (stepToComplete)
        {
            case AppraisalSubStatus.GoalSetting:
            {
                var pendingGoals = appraisal.Goals
                    .Where(g => g.Status is GoalStatus.Draft
                                         or GoalStatus.PendingApproval
                                         or GoalStatus.Rejected)
                    .ToList();

                foreach (var g in pendingGoals)
                {
                    g.Status       = GoalStatus.Approved;
                    g.ApprovalDate = now;
                    await _goalRepository.UpdateAsync(g);
                }

                actions.Add($"Auto-approved {pendingGoals.Count} pending goal(s) to unblock goal-setting gate.");
                break;
            }

            case AppraisalSubStatus.PeerNomination:
            {
                var pending = appraisal.PeerNominations
                    .Where(n => n.NominationStatus == PeerNominationStatus.Pending)
                    .ToList();

                foreach (var n in pending)
                {
                    n.NominationStatus = PeerNominationStatus.Approved;
                    n.ApprovedDate     = now;
                    await _nominationRepository.UpdateAsync(n);
                }

                actions.Add($"Approved {pending.Count} pending nomination(s). Minimum peer requirement bypassed by HR.");
                break;
            }

            case AppraisalSubStatus.SelfEvaluation:
            {
                var selfEval = appraisal.EvaluatorEvaluations
                    .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self && e.SubmittedDate == null);

                if (selfEval != null)
                {
                    selfEval.SubmittedDate = now;
                    await _evalRepository.UpdateAsync(selfEval);
                    actions.Add("Auto-submitted existing employee self-evaluation draft.");
                }
                else
                {
                    // Create a minimal self-eval record so the gate passes
                    var newEval = new EvaluatorEvaluation
                    {
                        AppraisalId       = appraisalId,
                        EvaluatorId       = appraisal.EmployeeId,
                        EvaluatorRole     = EvaluatorRole.Self,
                        EvaluatorWeight   = 0,
                        StartedDate       = now,
                        SubmittedDate     = now,
                        OverallNotes      = $"[Auto-created by HR manual advance: {reason}]",
                    };
                    SetTenantId(newEval, appraisal);
                    await _evalRepository.AddAsync(newEval);
                    actions.Add("Created placeholder self-evaluation record (no prior draft existed).");
                }
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

                await _appraisalRepository.UpdateAsync(appraisal);
                break;
            }

            case AppraisalSubStatus.PendingCalibration:
            case AppraisalSubStatus.CalibrationInProgress:
            {
                appraisal.IsCalibrated = true;
                await _appraisalRepository.UpdateAsync(appraisal);
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
                await _appraisalRepository.UpdateAsync(appraisal);
                actions.Add("Auto-acknowledged appraisal on behalf of employee.");
                break;
            }
        }

        // ── Save data mutations ──────────────────────────────────────────────
        await _unitOfWork.SaveChangesAsync(ct);

        // ── Reload navigations to resolve the NEW sub-status ────────────────
        appraisal = await LoadWithNavigationsAsync(appraisalId, ct);
        settings  = appraisal.AppraisalCycle!.AppraisalSettings!;

        var newSubStatus = AppraisalSubStatusResolver.Resolve(appraisal, settings);

        // ── Automatic major-status transitions ───────────────────────────────
        var targetMajorStatus = AppraisalSubStatusResolver.ExpectedMajorStatus(newSubStatus, settings);
        var newMajorStatus    = appraisal.Status;

        if (targetMajorStatus != appraisal.Status
            && AllowedTransitions.TryGetValue(appraisal.Status, out var validNext)
            && validNext.Contains(targetMajorStatus))
        {
            appraisal.Status = targetMajorStatus;
            await _appraisalRepository.UpdateAsync(appraisal);
            await _unitOfWork.SaveChangesAsync(ct);
            newMajorStatus = targetMajorStatus;
            actions.Add($"Appraisal major status auto-transitioned: {previousMajorStatus} → {newMajorStatus}.");

            _logger.LogInformation(
                "ManualAdvance: Appraisal {Id} major status auto-transitioned {From} → {To}.",
                appraisalId, previousMajorStatus, newMajorStatus);
        }

        // ── Write audit log ──────────────────────────────────────────────────
        var auditLog = new AppraisalManualAdvanceLog
        {
            PerformanceAppraisalId = appraisalId,
            AdvancedByEmployeeId   = advancedByEmployeeId,
            AdvancedDate           = now,
            FromSubStatus          = previousSubStatus.ToString(),
            ToSubStatus            = newSubStatus.ToString(),
            FromMajorStatus        = previousMajorStatus.ToString(),
            ToMajorStatus          = newMajorStatus.ToString(),
            Reason                 = reason,
            ActionsPerformedJson   = JsonSerializer.Serialize(actions),
        };
        SetTenantId(auditLog, appraisal);
        await _advanceLogRepository.AddAsync(auditLog);
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

        var appraisals = await _appraisalRepository
            .GetQueryable(a => a.AppraisalCycleId == cycleId
                            && a.Status != AppraisalStatus.Completed
                            && a.Status != AppraisalStatus.Closed
                            && a.Status != AppraisalStatus.Appealed)
            .Include(a => a.AppraisalCycle).ThenInclude(c => c!.AppraisalSettings)
            .Include(a => a.Goals)
            .Include(a => a.EvaluatorEvaluations)
            .Include(a => a.HRReviews)
            .Include(a => a.PeerNominations)
            .Include(a => a.Conversations)
            .ToListAsync(ct);

        var settings = appraisals.FirstOrDefault()?.AppraisalCycle?.AppraisalSettings;
        result.AutoLockEnabled = settings?.AutoLockOnDeadline ?? false;

        // Respect the setting: when auto-lock is off (or nothing to do), advance nothing.
        if (!result.AutoLockEnabled || settings is null || appraisals.Count == 0)
            return result;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var appraisal in appraisals)
        {
            result.Evaluated++;

            var subStatus = AppraisalSubStatusResolver.Resolve(appraisal, settings);
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
