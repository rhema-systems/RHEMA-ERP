using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>One appraisal's facts, its cycle's settings, and where the gates put it.</summary>
public sealed class AppraisalGateState
{
    public AppraisalGateState(AppraisalGateFacts facts, AppraisalSettings settings, Guid employeeId, Guid cycleId)
    {
        Facts = facts;
        Settings = settings;
        EmployeeId = employeeId;
        CycleId = cycleId;
        Block = AppraisalGates.Resolve(facts, settings);
    }

    public AppraisalGateFacts Facts { get; }
    public AppraisalSettings Settings { get; }
    public Guid EmployeeId { get; }
    public Guid CycleId { get; }
    public AppraisalGateBlock Block { get; }

    public AppraisalSubStatus SubStatus => Block.Step;
    public string StepLabel => AppraisalGates.Label(Block.Step);
    public AppraisalPhase Phase => AppraisalGates.ToPhase(Block.Step);
}

/// <summary>What one sync did.</summary>
public sealed record AppraisalSyncResult(
    Guid AppraisalId,
    AppraisalSubStatus SubStatus,
    AppraisalStatus StatusBefore,
    AppraisalStatus StatusAfter,
    AppraisalSettleResult? Settle)
{
    public bool Moved => StatusBefore != StatusAfter;
    public bool Settled => Settle != null;
}

/// <summary>
/// The appraisal pipeline's gate evaluator over what is saved (performance closure lane B1). The
/// decisions are <see cref="AppraisalGates"/>'s; this loads the facts they read — one projection,
/// the cycle's settings, the employee's goals in the cycle — and applies the status they imply.
///
/// <para>The status used to be written by hand on each path: <c>DetermineNextStatusAsync</c> for the
/// self and manager submissions (its peer and HR arms had no caller), a fixed Governance-or-Completed
/// choice at HR sign-off, Completed at acknowledgment, and the advance's own copy of the resolver.
/// None of them asked where the appraisal actually was, so a calibration commit or a completed final
/// conversation that was the last step left the appraisal in Governance for good.</para>
/// </summary>
public class AppraisalLifecycleService : IAppraisalLifecycleService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalSettings> _settingsRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IAppraisalScoreService _scores;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalLifecycleService> _logger;

    public AppraisalLifecycleService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalSettings> settingsRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        IAppraisalScoreService scores,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalLifecycleService> logger)
    {
        _appraisalRepository = appraisalRepository;
        _settingsRepository = settingsRepository;
        _goalRepository = goalRepository;
        _scores = scores;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

    // ── Loading the facts ────────────────────────────────────────────────────────────────────

    private sealed class GateRow
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid CycleId { get; set; }
        public Guid SettingsId { get; set; }
        public AppraisalStatus Status { get; set; }
        public AppraisalAppealStatus? CurrentAppealStatus { get; set; }
        public bool Remanded { get; set; }
        public DateTime? RemandDeadline { get; set; }
        public bool HasAppeal { get; set; }
        public bool IsCalibrated { get; set; }
        public bool CalibrationStarted { get; set; }
        public bool EmployeeAcknowledged { get; set; }
        public DateTime? EmployeeAcknowledgedDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool GoalSetMustBeLocked { get; set; }
        public List<GateEvaluationRow> Evaluations { get; set; } = new();
        public List<PeerNominationStatus> Nominations { get; set; } = new();
        public List<GateHrReviewRow> HrReviews { get; set; } = new();
        public List<ConversationType> ConversationsHeld { get; set; } = new();
        public List<string> AdvancedPast { get; set; } = new();
    }

    private sealed class GateEvaluationRow
    {
        public EvaluatorRole Role { get; set; }
        public DateTime? SubmittedDate { get; set; }
    }

    private sealed class GateHrReviewRow
    {
        public DateTime? CompletedDate { get; set; }
        public bool IsApproved { get; set; }
    }

    /// <inheritdoc/>
    public async Task<AppraisalGateState> GetStateAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var states = await LoadAsync(new[] { appraisalId }, cancellationToken);
        return states.TryGetValue(appraisalId, out var state)
            ? state
            : throw new ArgumentException($"Performance appraisal with ID '{appraisalId}' not found.");
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<Guid, AppraisalGateState>> GetStatesAsync(
        IReadOnlyCollection<Guid> appraisalIds, CancellationToken cancellationToken = default)
        => LoadAsync(appraisalIds, cancellationToken);

    /// <summary>
    /// A fixed number of queries whatever the count: the appraisals with their child facts, the
    /// settings of their cycles, and the employees' goals in those cycles. Everything is read
    /// untracked and from what is saved.
    ///
    /// <para>⚠ Split per collection, one appraisal or many: under EF's single-query default the
    /// five collections are LEFT JOINed and the rows multiply — the shape two HR reads here have
    /// already timed out on (see <c>JobApplicationRepositories.GetWithFullDetailsAsync</c>).</para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, AppraisalGateState>> LoadAsync(
        IReadOnlyCollection<Guid> appraisalIds, CancellationToken cancellationToken)
    {
        var ids = appraisalIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, AppraisalGateState>();

        var tenantId = GetTenantId();

        var query = _appraisalRepository.GetQueryable()
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && ids.Contains(a.Id))
            .Select(a => new GateRow
            {
                Id = a.Id,
                EmployeeId = a.EmployeeId,
                CycleId = a.AppraisalCycleId,
                SettingsId = a.AppraisalCycle.AppraisalSettingsId,
                Status = a.Status,
                CurrentAppealStatus = a.CurrentAppealStatus,
                Remanded = a.AppealRemandedDate != null,
                RemandDeadline = a.AppealRemandDeadline,
                HasAppeal = a.HasAppeal,
                IsCalibrated = a.IsCalibrated,
                // In a session that is still sitting. A link to a session that has been committed,
                // cancelled or deleted is history, not a calibration in progress.
                CalibrationStarted = a.CalibrationSession != null
                    && (a.CalibrationSession.Status == CalibrationStatus.Pending
                        || a.CalibrationSession.Status == CalibrationStatus.InProgress),
                EmployeeAcknowledged = a.EmployeeAcknowledged,
                EmployeeAcknowledgedDate = a.EmployeeAcknowledgedDate,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                GoalSetMustBeLocked = a.Template != null
                    && a.Template.Sections.Any(s => s.Kind == AppraisalSectionKind.EmployeeGoals),
                Evaluations = a.EvaluatorEvaluations
                    .Select(e => new GateEvaluationRow { Role = e.EvaluatorRole, SubmittedDate = e.SubmittedDate })
                    .ToList(),
                Nominations = a.PeerNominations.Select(n => n.NominationStatus).ToList(),
                HrReviews = a.HRReviews
                    .Select(r => new GateHrReviewRow { CompletedDate = r.ReviewCompletedDate, IsApproved = r.IsApproved })
                    .ToList(),
                ConversationsHeld = a.Conversations.Where(c => c.IsCompleted).Select(c => c.Type).ToList(),
                AdvancedPast = a.ManualAdvanceLogs.Select(l => l.FromSubStatus).ToList(),
            });

        var rows = await query.AsSplitQuery().ToListAsync(cancellationToken);
        if (rows.Count == 0) return new Dictionary<Guid, AppraisalGateState>();

        var settingsIds = rows.Select(r => r.SettingsId).Distinct().ToList();
        var settings = await _settingsRepository.GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && settingsIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        // By employee and cycle — the goal's own appraisal link is set only when the goal was created
        // after its appraisal existed (performance closure L2).
        var cycleIds = rows.Select(r => r.CycleId).Distinct().ToList();
        var employeeIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
        var goals = (await _goalRepository.GetQueryable()
                .AsNoTracking()
                .Where(g => g.TenantId == tenantId
                         && cycleIds.Contains(g.AppraisalCycleId)
                         && employeeIds.Contains(g.EmployeeId))
                .Select(g => new { g.EmployeeId, g.AppraisalCycleId, g.Status, g.IsLocked })
                .ToListAsync(cancellationToken))
            .ToLookup(g => (g.EmployeeId, g.AppraisalCycleId), g => new AppraisalGateGoal(g.Status, g.IsLocked));

        var states = new Dictionary<Guid, AppraisalGateState>(rows.Count);
        foreach (var row in rows)
        {
            if (!settings.TryGetValue(row.SettingsId, out var rowSettings))
            {
                _logger.LogWarning(
                    "Appraisal {AppraisalId}: its cycle's settings profile {SettingsId} was not found; it has no gate state.",
                    row.Id, row.SettingsId);
                continue;
            }

            var facts = new AppraisalGateFacts
            {
                AppraisalId = row.Id,
                Status = row.Status,
                CurrentAppealStatus = row.CurrentAppealStatus,
                Remanded = row.Remanded,
                RemandDeadline = row.RemandDeadline,
                HasAppeal = row.HasAppeal,
                IsCalibrated = row.IsCalibrated,
                CalibrationStarted = row.CalibrationStarted,
                EmployeeAcknowledged = row.EmployeeAcknowledged,
                EmployeeAcknowledgedDate = row.EmployeeAcknowledgedDate,
                LastChangedAt = row.UpdatedAt ?? row.CreatedAt,
                GoalSetMustBeLocked = row.GoalSetMustBeLocked,
                Evaluations = row.Evaluations.Select(e => new AppraisalGateEvaluation(e.Role, e.SubmittedDate)).ToList(),
                Nominations = row.Nominations,
                HrReviews = row.HrReviews.Select(r => new AppraisalGateHrReview(r.CompletedDate, r.IsApproved)).ToList(),
                ConversationsHeld = row.ConversationsHeld.ToHashSet(),
                Goals = goals[(row.EmployeeId, row.CycleId)].ToList(),
                AdvancedPast = row.AdvancedPast
                    .Select(AppraisalGates.ParseLoggedStep)
                    .OfType<AppraisalSubStatus>()
                    .ToHashSet(),
            };

            states[row.Id] = new AppraisalGateState(facts, rowSettings, row.EmployeeId, row.CycleId);
        }

        return states;
    }

    // ── Refusing a write ─────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AppraisalGateState> EnsureAtAsync(
        Guid appraisalId, string action, AppraisalSubStatus[] steps, CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(appraisalId, cancellationToken);
        AppraisalGates.EnsureAt(state.Facts, state.Settings, action, steps);
        return state;
    }

    // ── Sync ─────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AppraisalSyncResult> SyncAsync(
        Guid appraisalId,
        AppraisalScoreChangeSource source = AppraisalScoreChangeSource.Settle,
        bool publish = true,
        CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(appraisalId, cancellationToken);
        var before = state.Facts.Status;
        var sub = state.SubStatus;
        var target = AppraisalGates.ExpectedMajorStatus(sub);

        // Moves forward only, and only between the pipeline's own states. Draft is left by the first
        // save or by HR opening the appraisal — a conversation held on a Draft appraisal does not open
        // it — and appeals, withdrawals and closures are moved by their own actions.
        var rankBefore = AppraisalLifecycle.ForwardRank(before);
        var rankTarget = AppraisalLifecycle.ForwardRank(target);
        var moves = rankBefore > 0
            && rankTarget > rankBefore
            && AppraisalLifecycle.CanTransition(before, target);

        if (rankBefore > 0 && rankTarget >= 0 && rankTarget < rankBefore)
        {
            // Recorded, never acted on: B8's transition report lists these for HR to waive.
            _logger.LogInformation(
                "Appraisal {AppraisalId} is {Status} but its gates put it at {Step} ({Expected}); it is left where it is.",
                appraisalId, before, sub, target);
        }

        var after = before;
        if (moves)
        {
            var appraisal = await _appraisalRepository.GetByIdAsync(appraisalId)
                ?? throw new ArgumentException($"Performance appraisal with ID '{appraisalId}' not found.");

            // The move was checked against the saved status (above), not the tracked copy: inside a
            // retrying transaction a failed attempt may already have changed the tracked one.
            appraisal.Status = target;
            after = target;

            _logger.LogInformation(
                "Appraisal {AppraisalId} moved {From} → {To}: its gates put it at {Step}.",
                appraisalId, before, target, sub);
        }

        // Completing the appraisal, or bringing it to the employee's end, settles the score in the
        // same save as the move: a Completed appraisal is never left without its score, and the
        // employee acknowledges a settled score rather than a blank (lane A, A7).
        AppraisalSettleResult? settle = null;
        if ((moves && target == AppraisalStatus.Completed) || (rankBefore > 0 && AppraisalGates.IsEmployeeEnd(sub)))
            settle = await _scores.SettleAsync(appraisalId, source, publish, cancellationToken);
        else if (moves)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AppraisalSyncResult(appraisalId, sub, before, after, settle);
    }
}
