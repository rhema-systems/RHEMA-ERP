using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Peer Evaluation Service

public interface IPeerEvaluationService
{
    Task<IEnumerable<PeerEvaluationAssignmentDto>> GetPeerEvaluationAssignmentsAsync(
        Guid evaluatorId, 
        CancellationToken cancellationToken = default);
    
    Task<PeerEvaluationDetailDto> GetPeerEvaluationDetailAsync(
        Guid evaluationId, 
        Guid evaluatorId, 
        CancellationToken cancellationToken = default);
    
    Task<PeerEvaluationDetailDto> SavePeerEvaluationDraftAsync(
        SavePeerEvaluationDto saveDto, 
        Guid evaluatorId, 
        CancellationToken cancellationToken = default);
    
    Task<PeerEvaluationDetailDto> SubmitPeerEvaluationAsync(
        Guid evaluationId, 
        Guid evaluatorId, 
        CancellationToken cancellationToken = default);
}

public class PeerEvaluationService : IPeerEvaluationService
{
    private readonly IGenericRepository<EvaluatorEvaluation> _evaluatorEvaluationRepository;
    private readonly IGenericRepository<CriterionScore> _criterionScoreRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalSettings> _settingsRepository;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeRepository;
    private readonly IGenericRepository<AppraisalCompetency> _appraisalCompetencyRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfig> _criterionConfigRepository;
    private readonly IAppraisalScoreService _scores;
    private readonly IAppraisalLifecycleService _lifecycle;
    private readonly IAppraisalNotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PeerEvaluationService> _logger;

    public PeerEvaluationService(
        IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository,
        IGenericRepository<CriterionScore> criterionScoreRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalSettings> settingsRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeRepository,
        IGenericRepository<AppraisalCompetency> appraisalCompetencyRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> criterionConfigRepository,
        IAppraisalScoreService scores,
        IAppraisalLifecycleService lifecycle,
        IAppraisalNotificationService notifications,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<PeerEvaluationService> logger)
    {
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _criterionScoreRepository = criterionScoreRepository;
        _appraisalRepository = appraisalRepository;
        _settingsRepository = settingsRepository;
        _gradeRepository = gradeRepository;
        _appraisalCompetencyRepository = appraisalCompetencyRepository;
        _criterionConfigRepository = criterionConfigRepository;
        _scores = scores;
        _lifecycle = lifecycle;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
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

    private IQueryable<EvaluatorEvaluation> TenantEvaluationQuery()
    {
        var tenantId = GetTenantId();
        return _evaluatorEvaluationRepository.GetQueryable().Where(e => e.TenantId == tenantId);
    }

    /// <summary>
    /// A peer writes — draft or submission — inside the cycle's peer window (performance closure
    /// B1): alongside the self-evaluation, or only once it is in when the cycle says
    /// <see cref="PeerEvaluationOpenMode.AfterSelfEval"/>, and until the manager has submitted.
    /// No write enforced <c>PeerEvaluationOpenMode</c> before. Refused with a 422 naming the step the
    /// appraisal is at.
    /// </summary>
    private async Task EnsureInPeerWindowAsync(Guid appraisalId, string action, CancellationToken cancellationToken)
    {
        var state = await _lifecycle.GetStateAsync(appraisalId, cancellationToken);
        AppraisalGates.EnsureAt(state.Facts, state.Settings, action, AppraisalGates.PeerWindow(state.Settings));
    }

    /// <summary>
    /// The approved nomination behind each of this peer's evaluations, by appraisal: its due date and
    /// the nominator's instructions (performance closure D5). Neither reached the peer — the due date
    /// shown was always the cycle's, and the instructions nowhere.
    /// </summary>
    private async Task<Dictionary<Guid, (DateTime? DueDate, string? Instructions)>> NominationsBehindAsync(
        Guid evaluatorId, IEnumerable<Guid> appraisalIds, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var ids = appraisalIds.Distinct().ToList();
        var rows = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && ids.Contains(a.Id))
            .SelectMany(a => a.PeerNominations
                .Where(n => !n.IsDeleted && n.PeerEmployeeId == evaluatorId && n.NominationStatus == PeerNominationStatus.Approved)
                .Select(n => new { n.AppraisalId, n.DueDate, n.InstructionsToPeer }))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.AppraisalId)
            .ToDictionary(g => g.Key, g => (g.First().DueDate, g.First().InstructionsToPeer));
    }

    /// <summary>The nomination's due date, else the cycle's peer deadline.</summary>
    private static DateOnly? DueDateOf(DateTime? nominationDue, DateOnly? cycleDeadline)
        => nominationDue is DateTime due ? DateOnly.FromDateTime(due) : cycleDeadline;

    public async Task<IEnumerable<PeerEvaluationAssignmentDto>> GetPeerEvaluationAssignmentsAsync(
        Guid evaluatorId,
        CancellationToken cancellationToken = default)
    {
        // Get all peer evaluation assignments for this evaluator
        var tenantId = GetTenantId();
        var evaluations = await TenantEvaluationQuery()
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
                    .ThenInclude(emp => emp.Position)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
                    .ThenInclude(emp => emp.OrganizationUnit)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.AppraisalCycle)
            // A withdrawn appraisal's unsubmitted evaluations are asked of no one (performance closure
            // E-d1): they sat in the queue as "N peer reviews are waiting on you". A submitted one
            // stays, as the peer's record of what they wrote.
            .Where(e => e.EvaluatorId == evaluatorId && e.EvaluatorRole == EvaluatorRole.Peer
                        && (e.Appraisal.Status != AppraisalStatus.Withdrawn || e.SubmittedDate != null))
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken);

        var nominations = await NominationsBehindAsync(evaluatorId, evaluations.Select(e => e.AppraisalId), cancellationToken);

        var assignments = evaluations.Select(e =>
        {
            var nomination = nominations.GetValueOrDefault(e.AppraisalId);
            return new PeerEvaluationAssignmentDto
            {
                EvaluationId = e.Id,
                AppraisalId = e.AppraisalId,
                AppraisalCycleName = e.Appraisal.AppraisalCycle?.CycleName ?? "Unknown Cycle",
                AppraiseeId = e.Appraisal.EmployeeId,
                AppraiseeName = e.Appraisal.Employee.FullName,
                AppraiseePosition = e.Appraisal.Employee.Position?.Title ?? "Unknown",
                AppraiseeOrganizationUnit = e.Appraisal.Employee.OrganizationUnit?.Name ?? "Unknown",
                Status = e.SubmittedDate.HasValue ? "Submitted" :
                         e.StartedDate.HasValue ? "In Progress" : "Not Started",
                StartedDate = e.StartedDate,
                SubmittedDate = e.SubmittedDate,
                DueDate = DueDateOf(nomination.DueDate, e.Appraisal.AppraisalCycle?.PeerEvaluationDeadline),
                EvaluatorWeight = e.EvaluatorWeight,
                InstructionsToPeer = nomination.Instructions,
            };
        }).ToList();

        return assignments;
    }

    public async Task<PeerEvaluationDetailDto> GetPeerEvaluationDetailAsync(
        Guid evaluationId, 
        Guid evaluatorId, 
        CancellationToken cancellationToken = default)
    {
        // Load the evaluation
        var evaluation = await TenantEvaluationQuery()
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
                    .ThenInclude(emp => emp.Position)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
                    .ThenInclude(emp => emp.OrganizationUnit)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.AppraisalCycle)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.CriterionConfigs)
                    .ThenInclude(cc => cc.TemplateItem)
                        .ThenInclude(ti => ti.Competency)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.CriterionConfigs)
                    .ThenInclude(cc => cc.TemplateItem)
                        .ThenInclude(ti => ti.KpiDefinition)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.CriterionConfigs)
                    .ThenInclude(cc => cc.GradeRanges)
                        .ThenInclude(gr => gr.GradeDefinition)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Template)
                    .ThenInclude(t => t.Sections!)
                        .ThenInclude(s => s.TemplateItems)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Goals)
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
            // One query per collection: as a single twelve-way join it needed a memory grant a
            // loaded server could not give, and timed out reading back every peer save.
            .AsSplitQuery()
            // A peer's own evaluation only (performance closure D-62): the self and manager rows are read by their
            // own forms.
            .FirstOrDefaultAsync(e => e.Id == evaluationId && e.EvaluatorId == evaluatorId
                                   && e.EvaluatorRole == EvaluatorRole.Peer, cancellationToken);

        if (evaluation == null)
        {
            throw new InvalidOperationException("Peer evaluation not found or access denied.");
        }

        // Load appraisal settings
        var settings = await _settingsRepository.GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == evaluation.Appraisal.AppraisalCycle.AppraisalSettingsId && s.TenantId == GetTenantId(), cancellationToken);

        if (settings == null)
        {
            throw new InvalidOperationException("Appraisal settings not found.");
        }

        var nomination = (await NominationsBehindAsync(evaluatorId, new[] { evaluation.AppraisalId }, cancellationToken))
            .GetValueOrDefault(evaluation.AppraisalId);

        var detail = new PeerEvaluationDetailDto
        {
            EvaluationId = evaluation.Id,
            AppraisalId = evaluation.AppraisalId,
            AppraisalCycleName = evaluation.Appraisal.AppraisalCycle?.CycleName ?? "Unknown",
            AppraiseeName = evaluation.Appraisal.Employee.FullName,
            AppraiseePosition = evaluation.Appraisal.Employee.Position?.Title ?? "Unknown",
            AppraiseeOrganizationUnit = evaluation.Appraisal.Employee.OrganizationUnit?.Name ?? "Unknown",
            PeerEvaluationWeight = settings.PeerEvaluationWeight,
            AllowPeerKpiEvaluation = settings.AllowPeerKpiEvaluation,
            IsAnonymous = settings.PeerReviewsAnonymous,
            IsSubmitted = evaluation.SubmittedDate.HasValue,
            DueDate = DueDateOf(nomination.DueDate, evaluation.Appraisal.AppraisalCycle?.PeerEvaluationDeadline),
            InstructionsToPeer = nomination.Instructions,
            Sections = BuildPeerEvaluationSections(evaluation.Appraisal, evaluation, settings.AllowPeerKpiEvaluation)
        };

        return detail;
    }

    public async Task<PeerEvaluationDetailDto> SavePeerEvaluationDraftAsync(
        SavePeerEvaluationDto saveDto, 
        Guid evaluatorId, 
        CancellationToken cancellationToken = default)
    {
        // Load evaluation with necessary navigation properties
        var evaluation = await TenantEvaluationQuery()
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.AppraisalCycle)
                    .ThenInclude(c => c.AppraisalSettings)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
            .Include(e => e.CriterionScores)
            // A peer's own evaluation only (performance closure D-62). It matched on the id and the evaluator, so the
            // appraisee's self-evaluation or the manager's evaluation could be written here, past their own forms' rules.
            .FirstOrDefaultAsync(e => e.Id == saveDto.EvaluationId && e.EvaluatorId == evaluatorId
                                   && e.EvaluatorRole == EvaluatorRole.Peer, cancellationToken);

        if (evaluation == null)
        {
            throw new KeyNotFoundException("Evaluation not found or access denied.");
        }

        if (evaluation.SubmittedDate.HasValue)
        {
            throw new InvalidOperationException("Cannot modify submitted evaluation.");
        }

        await EnsureInPeerWindowAsync(evaluation.AppraisalId, "Peer feedback cannot be saved yet", cancellationToken);

        // Every score on its item's own scale — the same check as the self and manager forms (A11).
        var scaleError = await _scores.ValidateItemScoresAsync(evaluation.AppraisalId, saveDto.ItemScores, cancellationToken);
        if (scaleError != null)
            throw new InvalidOperationException(scaleError);

        // Mark as started if not already
        if (!evaluation.StartedDate.HasValue)
        {
            evaluation.StartedDate = DateTime.UtcNow;
            await _evaluatorEvaluationRepository.UpdateAsync(evaluation);
        }

        // One query for the whole form, not one per item. The arithmetic is the shared one
        // (AppraisalScoreService): this service used to keep its own copy, which read the live
        // section weight and scored a KPI restatement against the band top (A10).
        var scoring = await _scores.LoadScoringAsync(evaluation.AppraisalId, cancellationToken);

        // A peer scores the employee's goals only when the cycle lets peers score measured work (L7).
        var allowPeerKpi = evaluation.Appraisal.AppraisalCycle?.AppraisalSettings?.AllowPeerKpiEvaluation ?? false;

        // Update criterion scores from ItemScores, each resolved to the criterion it names — a
        // template item, or a goal row's snapshot id (lane L3).
        foreach (var itemInput in saveDto.ItemScores)
        {
            if (!itemInput.NumericScore.HasValue && !itemInput.ActualValue.HasValue)
                continue;

            var criterion = scoring.Resolve(itemInput)
                ?? throw new InvalidOperationException("An item on this form is not one of this appraisal's criteria.");
            if (!allowPeerKpi && criterion.CriterionConfigId is Guid configId && scoring.GoalRow(configId) != null)
                throw new InvalidOperationException("Peers do not score the employee's goals in this cycle.");
            // B2: nor a KPI on the template. The draft took one, and the peer's total — and so the
            // overall — counted it, though the form never offers it (AllowPeerKpiEvaluation).
            if (!allowPeerKpi && scoring.IsMeasured(criterion.Key))
                throw new InvalidOperationException("Peers do not score measured work (KPIs) in this cycle.");

            var existingScore = evaluation.CriterionScores
                .FirstOrDefault(cs => (cs.TemplateItemId.HasValue || cs.CriterionConfigId.HasValue)
                                   && cs.CriterionKey() == criterion.Key);

            if (existingScore != null)
            {
                existingScore.CriterionConfigId ??= criterion.CriterionConfigId;
                existingScore.NumericScore = itemInput.NumericScore;
                existingScore.ActualValue  = itemInput.ActualValue;
                existingScore.Notes = itemInput.Notes;
                existingScore.EvidenceLinks = itemInput.EvidenceLinks;

                await _scores.ScoreCriterionAsync(existingScore, scoring, cancellationToken);
                await _criterionScoreRepository.UpdateAsync(existingScore);
            }
            else
            {
                var newScore = new CriterionScore
                {
                    TenantId = GetTenantId(),
                    EvaluatorEvaluationId = evaluation.Id,
                    TemplateItemId = criterion.TemplateItemId,
                    CriterionConfigId = criterion.CriterionConfigId,
                    NumericScore = itemInput.NumericScore,
                    ActualValue  = itemInput.ActualValue,
                    Notes = itemInput.Notes,
                    // B2: the form sent it and no save kept it.
                    EvidenceLinks = itemInput.EvidenceLinks
                };

                await _scores.ScoreCriterionAsync(newScore, scoring, cancellationToken);
                await _criterionScoreRepository.AddAsync(newScore);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer evaluation draft saved for evaluation {evaluationId} by evaluator {evaluatorId}", 
            evaluation.Id, evaluatorId);

        // Return updated detail
        return await GetPeerEvaluationDetailAsync(saveDto.EvaluationId, evaluatorId, cancellationToken);
    }

    public async Task<PeerEvaluationDetailDto> SubmitPeerEvaluationAsync(
        Guid evaluationId, 
        Guid evaluatorId, 
        CancellationToken cancellationToken = default)
    {
        var evaluation = await TenantEvaluationQuery()
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.AppraisalCycle)
                    .ThenInclude(c => c.AppraisalSettings)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.CriterionConfigs)
                    .ThenInclude(cc => cc.TemplateItem)
                        .ThenInclude(ti => ti!.Competency)
            .Include(e => e.CriterionScores)
            // A peer's own evaluation only (performance closure D-62): the submission of a self or manager evaluation
            // through this route skipped their gates, and — where peers may not score KPIs — deleted that row's KPI scores.
            .FirstOrDefaultAsync(e => e.Id == evaluationId && e.EvaluatorId == evaluatorId
                                   && e.EvaluatorRole == EvaluatorRole.Peer, cancellationToken);

        if (evaluation == null)
        {
            throw new KeyNotFoundException("Evaluation not found or access denied.");
        }

        if (evaluation.SubmittedDate.HasValue)
        {
            throw new InvalidOperationException("Evaluation already submitted.");
        }

        await EnsureInPeerWindowAsync(evaluation.AppraisalId, "Peer feedback cannot be submitted yet", cancellationToken);

        // Validate all required criteria are scored (using the CriterionConfig snapshot), mirroring
        // the peer scoring form's IsScoreable rule: competencies are always required; KPI items and
        // the employee's goals only when AllowPeerKpiEvaluation is enabled (L7). A measured row
        // counts as scored when it has either a numeric score or an actual value.
        var allowPeerKpi = evaluation.Appraisal.AppraisalCycle?.AppraisalSettings?.AllowPeerKpiEvaluation ?? false;

        var unscoredCount = evaluation.Appraisal.CriterionConfigs
            .Where(cc => cc.TemplateItem?.CompetencyId != null
                      || (allowPeerKpi && (cc.TemplateItem?.KpiDefinitionId != null || cc.IsGoalRow())))
            .Count(cc =>
            {
                var measured = cc.IsMeasured();
                var key = cc.CriterionKey();
                return !evaluation.CriterionScores.Any(cs => (cs.TemplateItemId.HasValue || cs.CriterionConfigId.HasValue)
                    && cs.CriterionKey() == key
                    && (cs.NumericScore.HasValue || (measured && cs.ActualValue.HasValue)));
            });

        if (unscoredCount > 0)
        {
            throw new InvalidOperationException($"Please score all required criteria before submitting. {unscoredCount} criteria remaining.");
        }

        // B2: a scored criterion whose competency requires evidence needs a link. A peer's submit
        // carries no body — it submits the draft — so the stored entries are the ones read.
        var evidenceRequired = evaluation.Appraisal.CriterionConfigs
            .Where(cc => cc.TemplateItemId.HasValue && cc.TemplateItem?.Competency?.RequireEvidence == true)
            .Select(cc => new RequiredEvidence { Key = cc.TemplateItemId!.Value, Name = cc.TemplateItem!.Competency!.CriteriaName })
            .ToList();
        var evidenceError = AppraisalEvidence.Missing(evidenceRequired, evaluation.CriterionScores
            .Where(cs => !cs.IsDeleted)
            .GroupBy(cs => cs.CriterionKey())
            .ToDictionary(g => g.Key, g => new EvidenceEntry(
                g.Any(cs => cs.NumericScore.HasValue || cs.ActualValue.HasValue),
                g.Select(cs => cs.EvidenceLinks).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)))));
        if (evidenceError != null)
            throw new InvalidOperationException(evidenceError);

        // A weighted mean over the criteria this peer was asked to score, recomputed from the raw
        // inputs by the shared path — the same number the settle will use.
        var scoring = await _scores.LoadScoringAsync(evaluation.AppraisalId, cancellationToken);

        // B2: a row the cycle does not let a peer score — measured work or the employee's goals,
        // with AllowPeerKpiEvaluation off — is dropped here rather than counted. A draft saved before
        // the draft save refused them may hold one, and the form cannot show it to be removed.
        var scored = evaluation.CriterionScores.ToList();
        if (!allowPeerKpi)
        {
            var barred = scored
                .Where(cs => scoring.IsMeasured(cs.CriterionKey())
                          || (cs.CriterionConfigId is Guid rowId && scoring.GoalRow(rowId) != null))
                .ToList();
            foreach (var row in barred)
                await _criterionScoreRepository.DeleteAsync(row);
            if (barred.Count > 0)
            {
                _logger.LogWarning(
                    "Peer evaluation {EvaluationId}: dropped {Count} score(s) on measured work or goals the cycle does not let peers score",
                    evaluation.Id, barred.Count);
                scored = scored.Except(barred).ToList();
            }
        }

        var totalWeightedScore = await _scores.ScoreEvaluatorAsync(scored, scoring, cancellationToken);

        evaluation.TotalScore = totalWeightedScore;
        evaluation.SubmittedDate = DateTime.UtcNow;

        await _evaluatorEvaluationRepository.UpdateAsync(evaluation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer evaluation submitted for evaluation {evaluationId} by evaluator {evaluatorId} with total score {score}",
            evaluation.Id, evaluatorId, totalWeightedScore);

        // A peer's submission can be the step that completes the appraisal — a cycle with no manager
        // evaluation and nothing after the peers — which nothing handled: the arm of the old status
        // helper written for it had no caller. The gates decide now, and completion settles (B1, B5).
        await _lifecycle.SyncAsync(evaluation.AppraisalId, cancellationToken: cancellationToken);

        await NotifyPeerSubmissionAsync(evaluation, cancellationToken);

        return await GetPeerEvaluationDetailAsync(evaluationId, evaluatorId, cancellationToken);
    }

    /// <summary>
    /// Tells the appraisee's manager that a peer has reported back, and — once the minimum
    /// number of peers has submitted — that the peer leg is done.
    ///
    /// The peer's identity is deliberately never in the message: it goes to the manager, who may
    /// see it, but <c>PeerReviewsAnonymous</c> governs what the *appraisee* may see, and a
    /// notification is the wrong place to depend on that distinction. Best-effort throughout —
    /// the submission is already saved.
    /// </summary>
    private async Task NotifyPeerSubmissionAsync(EvaluatorEvaluation evaluation, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var appraisal = evaluation.Appraisal;
            var managerId = await _appraisalRepository.GetQueryable()
                .Where(a => a.Id == evaluation.AppraisalId && a.TenantId == tenantId)
                .Select(a => a.Employee.ManagerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (managerId is not Guid manager || manager == Guid.Empty) return;

            var appraiseeName = appraisal?.Employee?.FullName;
            var cycleName = appraisal?.AppraisalCycle?.CycleName;
            var minPeers = appraisal?.AppraisalCycle?.AppraisalSettings?.MinPeerEvaluators ?? 0;

            var submittedPeers = await TenantEvaluationQuery()
                .CountAsync(e => e.AppraisalId == evaluation.AppraisalId
                              && e.EvaluatorRole == EvaluatorRole.Peer
                              && e.SubmittedDate != null, cancellationToken);

            var allIn = minPeers > 0 && submittedPeers >= minPeers;

            await _notifications.RaiseAsync(new[]
            {
                new AppraisalNotificationRequest(
                    manager,
                    allIn ? AppraisalNotificationType.AllPeerEvalsComplete : AppraisalNotificationType.PeerEvaluationCompleted,
                    allIn
                        ? $"Peer feedback complete for {appraiseeName ?? "your report"}"
                        : $"A peer review came in for {appraiseeName ?? "your report"}",
                    allIn
                        ? $"All {submittedPeers} required peer evaluation(s) are in. You can complete your manager evaluation."
                        : $"{submittedPeers} of {minPeers} required peer evaluation(s) submitted so far.",
                    cycleName,
                    $"/hr/performance/team-appraisals/{evaluation.AppraisalId}",
                    evaluation.AppraisalId,
                    appraiseeName),
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify on peer evaluation {EvaluationId}; the submission stands.", evaluation.Id);
        }
    }

    #region Section Building Helpers

    private List<PeerEvaluationSectionDto> BuildPeerEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation peerEval,
        bool allowPeerKpiEvaluation)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        // Keyed by criterion, so a goals section shows the employee's goal rows (lane L3).
        var configsByKey    = appraisal.CriterionConfigs.ToDictionary(cc => cc.CriterionKey());
        var scoresByKey     = peerEval.CriterionScores.ToDictionary(cs => cs.CriterionKey());
        var goalsByKpiDefId = appraisal.Goals
                              .Where(g => g.KpiDefinitionId.HasValue)
                              .ToLookup(g => g.KpiDefinitionId!.Value);

        PeerEvaluationItemDto Item(PerformanceAppraisalCriterionConfig config, AppraisalTemplateItem? templateItem)
        {
            scoresByKey.TryGetValue(config.CriterionKey(), out var score);
            var kpiDef = config.TemplateItem?.KpiDefinition;
            var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
            // KPI items and the employee's goals only when peers may score measured work (L7).
            var measuredWork = config.IsGoalRow() || config.TemplateItem?.KpiDefinitionId.HasValue == true;
            var item = MapPeerItem(config, config.TemplateItem?.Competency, kpiDef, goal, score, !measuredWork || allowPeerKpiEvaluation);
            if (templateItem != null)
            {
                item.DisplayOrder   = templateItem.DisplayOrder;
                item.CustomQuestion = templateItem.CustomQuestion;
            }
            return item;
        }

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            var fallbackItems = appraisal.CriterionConfigs.Select(cc => Item(cc, null)).ToList();
            return new List<PeerEvaluationSectionDto>
            {
                new() { SectionName = "Evaluation Criteria", SectionWeight = 100, Items = fallbackItems }
            };
        }

        return appraisal.Template.Sections
            .OrderBy(s => s.DisplayOrder)
            .Select(section => new PeerEvaluationSectionDto
            {
                SectionId          = section.Id,
                SectionName        = section.SectionName,
                Kind               = section.Kind,
                SectionDescription = section.Description,
                DisplayOrder       = section.DisplayOrder,
                SectionWeight      = section.ScoredWeight(configsByKey),
                Items              = section.FormRows(configsByKey).Select(row => Item(row.Config, row.Item)).ToList()
            })
            .Where(s => s.Items.Any())
            .ToList();
    }

    private PeerEvaluationItemDto MapPeerItem(
        PerformanceAppraisalCriterionConfig config,
        AppraisalCompetency? competency,
        KpiDefinition? kpiDef,
        EmployeeGoal? goal,
        CriterionScore? existingScore,
        bool isScoreable) => new()
    {
        TemplateItemId          = config.TemplateItemId,
        CriterionConfigId       = config.Id,
        CriterionKey            = config.CriterionKey(),
        ScoringMethod           = config.EffectiveScoringMethod(),
        EmployeeGoalId          = config.EmployeeGoalId,
        ItemName                = competency?.CriteriaName ?? kpiDef?.KpiName ?? config.ItemLabel ?? string.Empty,
        ItemDescription         = competency?.Description,
        ItemWeight              = config.WeightUsed,
        DisplayOrder            = config.DisplayOrder ?? 0,
        RequireEvidence         = competency?.RequireEvidence ?? false,
        KpiDefinitionId         = kpiDef?.Id,
        KpiUnit                 = config.Unit ?? kpiDef?.Unit ?? goal?.Unit,
        MeasurementType         = config.MeasurementType ?? kpiDef?.MeasurementType,
        // The snapshot's target, floor and ceiling — the ones the score uses. This read the live
        // goal, so a peer saw a target the score never used (A12).
        KpiTargetValue          = config.KpiTargetValue,
        KpiMinValue             = config.KpiMinValue,
        KpiMaxValue             = config.KpiMaxValue,
        GradeRanges             = config.GradeRanges.Select(gr => new EvaluationGradeRangeDto
        {
            GradeDefinitionId = gr.GradeDefinitionId,
            GradeName         = gr.GradeDefinition?.GradeName ?? string.Empty,
            GradeDescription  = gr.GradeDefinition?.Description,
            LowScore          = gr.LowScore,
            HighScore         = gr.HighScore
        }).ToList(),
        ExistingCriterionScoreId = existingScore?.Id,
        ExistingNumericScore     = existingScore?.NumericScore,
        ExistingActualValue      = existingScore?.ActualValue,
        ExistingNotes            = existingScore?.Notes,
        ExistingEvidenceLinks    = existingScore?.EvidenceLinks,
        WeightedScore            = existingScore?.WeightedScore,
        IsScoreable              = isScoreable,
    };

    #endregion
}

#endregion Peer Evaluation Service
