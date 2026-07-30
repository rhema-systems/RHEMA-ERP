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
            .Where(e => e.EvaluatorId == evaluatorId && e.EvaluatorRole == EvaluatorRole.Peer)
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken);

        var assignments = evaluations.Select(e => new PeerEvaluationAssignmentDto
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
            DueDate = e.Appraisal.AppraisalCycle?.PeerEvaluationDeadline,
            EvaluatorWeight = e.EvaluatorWeight
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
            .FirstOrDefaultAsync(e => e.Id == evaluationId && e.EvaluatorId == evaluatorId, cancellationToken);

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
            DueDate = evaluation.Appraisal.AppraisalCycle?.PeerEvaluationDeadline,
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
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
            .Include(e => e.CriterionScores)
            .FirstOrDefaultAsync(e => e.Id == saveDto.EvaluationId && e.EvaluatorId == evaluatorId, cancellationToken);

        if (evaluation == null)
        {
            throw new InvalidOperationException("Evaluation not found or access denied.");
        }

        if (evaluation.SubmittedDate.HasValue)
        {
            throw new InvalidOperationException("Cannot modify submitted evaluation.");
        }

        // Enforce the 0–100 score invariant.
        if (saveDto.ItemScores.Any(e => e.NumericScore.HasValue && !AppraisalScoring.IsValidScore(e.NumericScore.Value)))
            throw new InvalidOperationException($"Scores must be between {AppraisalScoring.MinScore:0} and {AppraisalScoring.MaxScore:0}.");

        // Mark as started if not already
        if (!evaluation.StartedDate.HasValue)
        {
            evaluation.StartedDate = DateTime.UtcNow;
            await _evaluatorEvaluationRepository.UpdateAsync(evaluation);
        }

        // Update criterion scores from ItemScores (keyed by CriteriaId)
        foreach (var itemInput in saveDto.ItemScores)
        {
            if (!itemInput.NumericScore.HasValue && !itemInput.ActualValue.HasValue)
                continue;

            var templateItemId = itemInput.TemplateItemId;
            var existingScore = evaluation.CriterionScores
                .FirstOrDefault(cs => cs.TemplateItemId == templateItemId);

            if (existingScore != null)
            {
                existingScore.NumericScore = itemInput.NumericScore;
                existingScore.ActualValue  = itemInput.ActualValue;
                existingScore.Notes = itemInput.Notes;

                await CalculateAndSetWeightedScoreAsync(existingScore, evaluation, cancellationToken);
                await _criterionScoreRepository.UpdateAsync(existingScore);
            }
            else
            {
                var newScore = new CriterionScore
                {
                    TenantId = GetTenantId(),
                    EvaluatorEvaluationId = evaluation.Id,
                    TemplateItemId = templateItemId,
                    NumericScore = itemInput.NumericScore,
                    ActualValue  = itemInput.ActualValue,
                    Notes = itemInput.Notes
                };

                await CalculateAndSetWeightedScoreAsync(newScore, evaluation, cancellationToken);
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
            .Include(e => e.CriterionScores)
            .FirstOrDefaultAsync(e => e.Id == evaluationId && e.EvaluatorId == evaluatorId, cancellationToken);

        if (evaluation == null)
        {
            throw new InvalidOperationException("Evaluation not found or access denied.");
        }

        if (evaluation.SubmittedDate.HasValue)
        {
            throw new InvalidOperationException("Evaluation already submitted.");
        }

        // Validate all required criteria are scored (using the CriterionConfig snapshot), mirroring
        // the peer scoring form's IsScoreable rule: competencies are always required; KPI items are
        // required only when AllowPeerKpiEvaluation is enabled. A KPI item counts as scored when it
        // has either a numeric score or an actual value.
        var allowPeerKpi = evaluation.Appraisal.AppraisalCycle?.AppraisalSettings?.AllowPeerKpiEvaluation ?? false;

        var unscoredCount = evaluation.Appraisal.CriterionConfigs
            .Where(cc => cc.TemplateItem?.CompetencyId != null
                      || (allowPeerKpi && cc.TemplateItem?.KpiDefinitionId != null))
            .Count(cc =>
            {
                var isKpi = cc.TemplateItem?.KpiDefinitionId != null;
                return !evaluation.CriterionScores.Any(cs => cs.TemplateItemId == cc.TemplateItemId
                    && (cs.NumericScore.HasValue || (isKpi && cs.ActualValue.HasValue)));
            });

        if (unscoredCount > 0)
        {
            throw new InvalidOperationException($"Please score all required criteria before submitting. {unscoredCount} criteria remaining.");
        }

        // Calculate total score
        var totalWeightedScore = evaluation.CriterionScores
            .Sum(cs => cs.WeightedScore);

        evaluation.TotalScore = totalWeightedScore;
        evaluation.SubmittedDate = DateTime.UtcNow;
        
        await _evaluatorEvaluationRepository.UpdateAsync(evaluation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer evaluation submitted for evaluation {evaluationId} by evaluator {evaluatorId} with total score {score}", 
            evaluation.Id, evaluatorId, totalWeightedScore);

        // TODO: Send notification to manager

        return await GetPeerEvaluationDetailAsync(evaluationId, evaluatorId, cancellationToken);
    }

    /// <summary>
    /// Calculates and sets the WeightedScore for a CriterionScore in peer evaluation.
    /// Uses the immutable <see cref="PerformanceAppraisalCriterionConfig"/> snapshot for weight and grade range.
    /// Formula: WeightedScore = (NumericScore / MaxScore) × CriterionWeight × EvaluatorWeight
    /// </summary>
    private async Task CalculateAndSetWeightedScoreAsync(CriterionScore criterionScore, EvaluatorEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        if (!criterionScore.NumericScore.HasValue)
        {
            criterionScore.WeightedScore = 0;
            return;
        }

        var tenantId = GetTenantId();
        var snapshot = await _criterionConfigRepository.GetQueryable()
            .Include(c => c.GradeRanges)
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                  && c.PerformanceAppraisalId == evaluation.AppraisalId
                  && c.TemplateItemId == criterionScore.TemplateItemId,
                cancellationToken);

        if (snapshot == null)
        {
            criterionScore.WeightedScore = 0;
            return;
        }

        var maxScore = snapshot.GradeRanges.Any()
            ? snapshot.GradeRanges.Max(r => r.HighScore)
            : 100m;

        criterionScore.WeightedScore = maxScore > 0
            ? (criterionScore.NumericScore.Value / maxScore) * snapshot.WeightUsed * evaluation.EvaluatorWeight
            : 0;
    }

    #region Section Building Helpers

    private List<PeerEvaluationSectionDto> BuildPeerEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation peerEval,
        bool allowPeerKpiEvaluation)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        var configsByTemplateItemId = appraisal.CriterionConfigs.ToDictionary(cc => cc.TemplateItemId);
        var scoresByTemplateItemId  = peerEval.CriterionScores.ToDictionary(cs => cs.TemplateItemId);
        var goalsByKpiDefId         = appraisal.Goals
                                      .Where(g => g.KpiDefinitionId.HasValue)
                                      .ToLookup(g => g.KpiDefinitionId!.Value);

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            var fallbackItems = appraisal.CriterionConfigs
                .Select(cc =>
                {
                    scoresByTemplateItemId.TryGetValue(cc.TemplateItemId, out var score);
                    var kpiDef    = cc.TemplateItem?.KpiDefinition;
                    var goal      = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                    bool isKpi    = cc.TemplateItem?.KpiDefinitionId.HasValue == true;
                    bool scoreable = !isKpi || allowPeerKpiEvaluation;
                    return MapPeerItem(cc, cc.TemplateItem?.Competency, kpiDef, goal, score, scoreable);
                })
                .ToList();
            return new List<PeerEvaluationSectionDto>
            {
                new() { SectionName = "Evaluation Criteria", SectionWeight = 100, Items = fallbackItems }
            };
        }

        return appraisal.Template.Sections
            .OrderBy(s => s.DisplayOrder)
            .Select(section =>
            {
                var items = section.TemplateItems
                    .OrderBy(ti => ti.DisplayOrder)
                    .Where(ti => configsByTemplateItemId.ContainsKey(ti.Id))
                    .Select(ti =>
                    {
                        var config    = configsByTemplateItemId[ti.Id];
                        scoresByTemplateItemId.TryGetValue(ti.Id, out var score);
                        var kpiDef    = config.TemplateItem?.KpiDefinition;
                        var goal      = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                        bool isKpi    = config.TemplateItem?.KpiDefinitionId.HasValue == true;
                        bool scoreable = !isKpi || allowPeerKpiEvaluation;
                        var item      = MapPeerItem(config, config.TemplateItem?.Competency, kpiDef, goal, score, scoreable);
                        item.DisplayOrder   = ti.DisplayOrder;
                        item.CustomQuestion = ti.CustomQuestion;
                        return item;
                    })
                    .ToList();
                return new PeerEvaluationSectionDto
                {
                    SectionId          = section.Id,
                    SectionName        = section.SectionName,
                    SectionDescription = section.Description,
                    DisplayOrder       = section.DisplayOrder,
                    SectionWeight      = section.Weight,
                    Items              = items
                };
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
        ItemName                = competency?.CriteriaName ?? kpiDef?.KpiName ?? string.Empty,
        ItemDescription         = competency?.Description,
        ItemWeight              = config.WeightUsed,
        RequireEvidence         = competency?.RequireEvidence ?? false,
        KpiDefinitionId         = kpiDef?.Id,
        KpiUnit                 = kpiDef?.Unit ?? goal?.Unit,
        MeasurementType         = kpiDef?.MeasurementType,
        KpiTargetValue          = goal?.TargetValue,
        KpiMinValue             = goal?.MinValue,
        KpiMaxValue             = goal?.MaxValue,
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
