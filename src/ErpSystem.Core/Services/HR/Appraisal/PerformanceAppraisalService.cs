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

#region Performance Appraisal

public class PerformanceAppraisalService : IPerformanceAppraisalService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalCompetency> _appraisalCompetencyRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evaluatorEvaluationRepository;
    private readonly IGenericRepository<AppraisalTemplateItem> _templateItemRepository;
    private readonly IGenericRepository<CriterionScore> _criterionScoreRepository;
    private readonly IGenericRepository<AppraisalEmployeeResponse> _employeeResponseRepository;
    private readonly IGenericRepository<AppraisalCustomQuestionResponse> _customQuestionResponseRepository;
    private readonly IGenericRepository<AppraisalAttachment> _appraisalAttachmentRepository;
    private readonly IGenericRepository<PeerNomination> _peerNominationRepository;
    private readonly IGenericRepository<AppraisalCycle> _appraisalCycleRepository;
    private readonly IGenericRepository<AppraisalAppeal> _appealRepository;
    private readonly IGenericRepository<AppraisalAppealItem> _appealItemRepository;
    private readonly IGenericRepository<AppraisalEvaluationSnapshot> _evaluationSnapshotRepository;
    private readonly IGenericRepository<AppraisalCriterionScoreSnapshot> _criterionScoreSnapshotRepository;
    private readonly IGenericRepository<AppraisalKpiEvaluationSnapshot> _kpiEvaluationSnapshotRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfig> _criterionConfigRepository;
    private readonly IGenericRepository<EmployeeGoalAppraisalAssessment> _goalAssessmentRepository;
    private readonly ITalentRatingSyncService _talentRatingSync;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PerformanceAppraisal> _logger;

    public PerformanceAppraisalService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository, 
        IUnitOfWork unitOfWork, 
        ILogger<PerformanceAppraisal> logger, 
        IGenericRepository<Employee> employeeRepository, 
        IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository, 
        IGenericRepository<AppraisalCompetency> appraisalCompetencyRepository, 
        IGenericRepository<AppraisalTemplateItem> templateItemRepository,
        IGenericRepository<CriterionScore> criterionScoreRepository,
        IGenericRepository<AppraisalEmployeeResponse> employeeResponseRepository,
        IGenericRepository<AppraisalCustomQuestionResponse> customQuestionResponseRepository,
        IGenericRepository<AppraisalAttachment> appraisalAttachmentRepository, 
        IGenericRepository<PeerNomination> peerNominationRepository,
        IGenericRepository<AppraisalCycle> appraisalCycleRepository,
        IGenericRepository<AppraisalAppeal> appealRepository,
        IGenericRepository<AppraisalAppealItem> appealItemRepository,
        IGenericRepository<AppraisalEvaluationSnapshot> evaluationSnapshotRepository,
        IGenericRepository<AppraisalCriterionScoreSnapshot> criterionScoreSnapshotRepository,
        IGenericRepository<AppraisalKpiEvaluationSnapshot> kpiEvaluationSnapshotRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> criterionConfigRepository,
        IGenericRepository<EmployeeGoalAppraisalAssessment> goalAssessmentRepository,
        ITalentRatingSyncService talentRatingSync)
    {
        _appraisalRepository = appraisalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _employeeRepository = employeeRepository;
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _appraisalCompetencyRepository = appraisalCompetencyRepository;
        _templateItemRepository = templateItemRepository;
        _criterionScoreRepository = criterionScoreRepository;
        _employeeResponseRepository = employeeResponseRepository;
        _customQuestionResponseRepository = customQuestionResponseRepository;
        _appraisalAttachmentRepository = appraisalAttachmentRepository;
        _peerNominationRepository = peerNominationRepository;
        _appraisalCycleRepository = appraisalCycleRepository;
        _appealRepository = appealRepository;
        _appealItemRepository = appealItemRepository;
        _evaluationSnapshotRepository = evaluationSnapshotRepository;
        _criterionScoreSnapshotRepository = criterionScoreSnapshotRepository;
        _kpiEvaluationSnapshotRepository = kpiEvaluationSnapshotRepository;
        _criterionConfigRepository = criterionConfigRepository;
        _goalAssessmentRepository = goalAssessmentRepository;
        _talentRatingSync = talentRatingSync;
    }

    public async Task<bool> CalculateOverallScoreAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
                                                .Include(p => p.EvaluatorEvaluations)
                                                    .ThenInclude(e => e.CriterionScores)
                                                .FirstOrDefaultAsync(p => p.Id == appraisalId, cancellationToken);

        if (appraisal == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{appraisalId}' not found.");
        }

        // Score formula:
        //   EvaluatorTotalScore  = SUM(CriterionScore.WeightedScore)  for each evaluator
        //   OverallScore         = SUM(EvaluatorTotalScore * EvaluatorWeight) / SUM(EvaluatorWeight)
        //
        // The contributing evaluators' weights are normally configured to sum to 1.0, but we divide by
        // the actual sum of contributing weights so a mis-configured weight set (or a missing evaluator)
        // can never silently UNDER-normalise the final score below its true 0–100 value.
        decimal weightedSum = 0;
        decimal contributingWeight = 0;

        foreach (var evaluation in appraisal.EvaluatorEvaluations)
        {
            // Step 1: raw total for this evaluator (sum of weighted criterion scores)
            var hasScores = evaluation.CriterionScores.Any();
            evaluation.TotalScore = hasScores
                ? evaluation.CriterionScores.Sum(cs => cs.WeightedScore)
                : 0;

            await _evaluatorEvaluationRepository.UpdateAsync(evaluation);

            // Step 2: apply evaluator-role weight so Self/Peer/Manager contributions are proportional to
            // their configured weight (e.g. 0.2 / 0.2 / 0.6). Only evaluators that actually scored count
            // toward the normalisation denominator.
            if (hasScores)
            {
                weightedSum += (evaluation.TotalScore ?? 0) * evaluation.EvaluatorWeight;
                contributingWeight += evaluation.EvaluatorWeight;
            }
        }

        if (contributingWeight > 0 && Math.Abs(contributingWeight - 1m) > 0.001m)
        {
            _logger.LogWarning(
                "Appraisal {AppraisalId}: contributing evaluator weights sum to {Sum} (expected 1.0); normalising the overall score.",
                appraisalId, contributingWeight);
        }

        appraisal.OverallScore = contributingWeight > 0
            ? AppraisalScoring.Clamp(weightedSum / contributingWeight)
            : 0;

        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PerformanceAppraisalDto> CreateAsync(CreatePerformanceAppraisalDto createDto, CancellationToken cancellationToken = default)
    {
        // ── Data integrity: one appraisal per employee per cycle ──────────────
        var duplicate = await _appraisalRepository.ExistsAsync(
            a => a.EmployeeId == createDto.EmployeeId
              && a.AppraisalCycleId == createDto.AppraisalCycleId);

        if (duplicate)
            throw new InvalidOperationException(
                "An appraisal already exists for this employee in the specified cycle. " +
                "Only one appraisal per employee per cycle is permitted.");

        var entity = createDto.ToEntity();

        entity.AppraisalNumber = await GenerateAppraisalNumberAsync(createDto.Year, cancellationToken);
        entity.Status = AppraisalStatus.Draft;

        await _appraisalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal created successfully: {appraisalId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{id}' not found.");
        }

        await _appraisalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal deleted: {id}", id);

        return true;
    }

    public async Task<AppraisalAppealDto> FileAppealAsync(CreateAppraisalAppealDto appealDto, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetByIdAsync(appealDto.PerformanceAppraisalId);

        if (appraisal == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{appealDto.PerformanceAppraisalId}' not found.");
        }

        // Create new appeal entity
        var appealEntity = new AppraisalAppeal
        {
            TenantId = appraisal.TenantId,
            PerformanceAppraisalId = appealDto.PerformanceAppraisalId,
            EmployeeId = appraisal.EmployeeId,
            SubmittedDate = DateTime.UtcNow,
            AppealReason = appealDto.AppealReason,
            Status = AppraisalAppealStatus.Submitted
        };

        // Add appeal items if provided
        if (appealDto.Items != null && appealDto.Items.Any())
        {
            foreach (var itemDto in appealDto.Items)
            {
                var appealItem = new AppraisalAppealItem
                {
                    TenantId = appraisal.TenantId,
                    TemplateItemId = itemDto.TemplateItemId,
                    Reason = itemDto.Reason
                };
                appealEntity.Items.Add(appealItem);
            }
        }

        // Update appraisal HasAppeal flag
        appraisal.HasAppeal = true;
        appraisal.CurrentAppealStatus = AppraisalAppealStatus.Submitted;

        // Save entities
        await _appealRepository.AddAsync(appealEntity);
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal filed successfully for appraisal: {appraisalId}", appealDto.PerformanceAppraisalId);

        return appealEntity.ToDto();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var performanceAppraisals = await _appraisalRepository.GetAllAsync();
        return performanceAppraisals.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalRepository.GetQueryable()
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .Where(p => p.EmployeeId == employeeId)
                                                .OrderByDescending(p => p.Year)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PerformanceAppraisalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetQueryable()
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Department)
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Position)
                                            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{id}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByStatusAsync(AppraisalStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalRepository.GetQueryable(p => p.Status == status)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalRepository.GetQueryable(p => p.Year == year)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<PerformanceAppraisalDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _appraisalRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.OrderByDescending(p => p.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);

        var appraisalDtos = items.ToDtoList();

        return new PagedResult<PerformanceAppraisalDto>
        {
            Items = appraisalDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<bool> ResolveAppealAsync(ResolveAppraisalAppealDto resolveDto, CancellationToken cancellationToken = default)
    {
        var appeal = await _appealRepository.GetQueryable()
            .Include(a => a.Items)
            .Include(a => a.PerformanceAppraisal)
            .FirstOrDefaultAsync(a => a.Id == resolveDto.AppealId, cancellationToken);

        if (appeal == null)
        {
            throw new ArgumentException($"Appeal with ID '{resolveDto.AppealId}' not found.");
        }

        // Update appeal status and resolution
        appeal.Status = resolveDto.Status;
        appeal.ResolutionNotes = resolveDto.ResolutionNotes;
        appeal.ResolvedDate = DateTime.UtcNow;
        // Note: ReviewedById should be set from the current user context in the controller

        // Update individual appeal items if provided
        if (resolveDto.ItemResolutions != null && resolveDto.ItemResolutions.Any())
        {
            foreach (var itemResolution in resolveDto.ItemResolutions)
            {
                var appealItem = appeal.Items.FirstOrDefault(i => i.Id == itemResolution.AppealItemId);
                if (appealItem != null)
                {
                    appealItem.ResolutionNotes = itemResolution.ResolutionNotes;
                    appealItem.ScoreAdjusted = itemResolution.ScoreAdjusted;
                }
            }
        }

        // Update appraisal CurrentAppealStatus and (optionally) AdjustedScore
        if (appeal.PerformanceAppraisal != null)
        {
            appeal.PerformanceAppraisal.CurrentAppealStatus = resolveDto.Status;

            if (resolveDto.AdjustedScore.HasValue)
                appeal.PerformanceAppraisal.AdjustedScore = resolveDto.AdjustedScore;
        }

        await _appealRepository.UpdateAsync(appeal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal resolved successfully: {appealId}", resolveDto.AppealId);

        return true;
    }

    public async Task<PerformanceAppraisalDto> UpdateAsync(UpdatePerformanceAppraisalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetQueryable()
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Department)
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Position)
                                            .FirstOrDefaultAsync(p => p.Id == updateDto.Id, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);

        await _appraisalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> UpdateStatusAsync(UpdateAppraisalStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(statusDto.AppraisalId);

        if (entity == null)
            throw new ArgumentException($"Performance appraisal with ID '{statusDto.AppraisalId}' not found.");

        // ── State machine: enforce valid forward transitions ──────────────────
        // Prevents skipping stages or transitioning backwards.
        // Closed is terminal — no transitions are allowed out of it.
        var allowedTransitions = new Dictionary<AppraisalStatus, HashSet<AppraisalStatus>>
        {
            [AppraisalStatus.Draft]      = new() { AppraisalStatus.Active },
            [AppraisalStatus.Active]     = new() { AppraisalStatus.Governance, AppraisalStatus.Completed, AppraisalStatus.Appealed },
            [AppraisalStatus.Governance] = new() { AppraisalStatus.Completed, AppraisalStatus.Appealed, AppraisalStatus.Active },
            [AppraisalStatus.Completed]  = new() { AppraisalStatus.Appealed, AppraisalStatus.Closed },
            [AppraisalStatus.Appealed]   = new() { AppraisalStatus.Completed, AppraisalStatus.Closed },
            [AppraisalStatus.Closed]     = new(),
        };

        if (!allowedTransitions.TryGetValue(entity.Status, out var validNext)
            || !validNext.Contains(statusDto.Status))
        {
            throw new InvalidOperationException(
                $"Invalid appraisal status transition from '{entity.Status}' to '{statusDto.Status}'. " +
                (allowedTransitions.TryGetValue(entity.Status, out var allowed) && allowed.Any()
                    ? $"Allowed next states: {string.Join(", ", allowed)}."
                    : "No further transitions are permitted from the current state."));
        }

        entity.Status = statusDto.Status;

        await _appraisalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal status updated successfully: {Id}", statusDto.AppraisalId);

        return true;
    }

    private async Task<string> GenerateAppraisalNumberAsync(int year, CancellationToken cancellationToken)
    {
        var count = await _appraisalRepository.GetQueryable(p => p.Year == year)
                                            .CountAsync(cancellationToken);

        return $"APR-{year}-{(count + 1):D5}";
    }

    #region EvaluatorEvaluation Operations

    public async Task<EvaluatorEvaluationDto> AddEvaluatorEvaluationAsync(Guid appraisalId, CreateEvaluatorEvaluationDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate appraisal exists
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found");

        // Validate evaluator exists
        var evaluatorExists = await _employeeRepository.ExistsAsync(e => e.Id == createDto.EvaluatorId);
        if (!evaluatorExists)
            throw new ArgumentException("Evaluator not found");

        // Check if evaluator already evaluated this appraisal
        var duplicateExists = appraisal.EvaluatorEvaluations
            .Any(e => e.EvaluatorId == createDto.EvaluatorId && e.EvaluatorRole == createDto.EvaluatorRole);

        if (duplicateExists)
        {
            throw new InvalidOperationException("This evaluator has already submitted an evaluation for this appraisal with the same role");
        }

        // Validate total evaluator weights don't exceed 1.0
        var totalWeight = appraisal.EvaluatorEvaluations.Sum(e => e.EvaluatorWeight) + createDto.EvaluatorWeight;
        if (totalWeight > 1.0m)
        {
            throw new InvalidOperationException($"Total evaluator weight cannot exceed 1.0. Current total: {appraisal.EvaluatorEvaluations.Sum(e => e.EvaluatorWeight)}, attempting to add: {createDto.EvaluatorWeight}");
        }

        var entity = createDto.ToEntity();
        entity.AppraisalId = appraisalId;

        await _evaluatorEvaluationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.Evaluator)
            .FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Evaluator evaluation added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EvaluatorEvaluationDto>> GetEvaluatorEvaluationsAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await _evaluatorEvaluationRepository.GetQueryable(e => e.AppraisalId == appraisalId)
                                                .Include(e => e.Evaluator)
                                                .OrderByDescending(e => e.IsAuthoritative)
                                                .ThenByDescending(e => e.EvaluatorWeight)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<EvaluatorEvaluationDto> UpdateEvaluatorEvaluationAsync(Guid appraisalId, UpdateEvaluatorEvaluationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.Evaluator)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(e => e.Id == updateDto.Id && e.AppraisalId == appraisalId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Evaluator evaluation not found");

        // Validate total weights (excluding current)
        var otherEvaluations = entity.Appraisal.EvaluatorEvaluations.Where(e => e.Id != updateDto.Id);
        var totalWeight = otherEvaluations.Sum(e => e.EvaluatorWeight) + updateDto.EvaluatorWeight;

        if (totalWeight > 1.0m)
        {
            throw new InvalidOperationException($"Total evaluator weight cannot exceed 1.0. Current total would be: {totalWeight}");
        }

        updateDto.UpdateEntity(entity);
        await _evaluatorEvaluationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evaluator evaluation updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteEvaluatorEvaluationAsync(Guid appraisalId, Guid evaluationId, CancellationToken cancellationToken = default)
    {
        var entity = await _evaluatorEvaluationRepository.GetQueryable()
                                                        .FirstOrDefaultAsync(e => e.Id == evaluationId && e.AppraisalId == appraisalId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Evaluator evaluation not found");

        await _evaluatorEvaluationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evaluator evaluation deleted successfully");

        return true;
    }

    #endregion

    #region CriterionScore Operations

    public async Task<CriterionScoreDto> AddCriterionScoreAsync(Guid evaluationId, CreateCriterionScoreDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate evaluation exists and get criteria mapping info
        var evaluation = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
                    .ThenInclude(emp => emp.Position)
            .Include(e => e.CriterionScores)
            .FirstOrDefaultAsync(e => e.Id == evaluationId, cancellationToken);

        if (evaluation == null)
            throw new ArgumentException("Evaluator evaluation not found");

        // Validate template item is configured for this appraisal
        var templateItemConfigExists = await _criterionConfigRepository.ExistsAsync(
            c => c.PerformanceAppraisalId == evaluation.AppraisalId && c.TemplateItemId == createDto.TemplateItemId);
        if (!templateItemConfigExists)
            throw new ArgumentException("Appraisal template item not found or not configured for this appraisal");

        // Check if this template item has already been scored by this evaluator
        var duplicateExists = evaluation.CriterionScores.Any(cs => cs.TemplateItemId == createDto.TemplateItemId);
        if (duplicateExists)
        {
            throw new InvalidOperationException("This criteria has already been scored in this evaluation");
        }

        var entity = createDto.ToEntity();
        entity.EvaluatorEvaluationId = evaluationId;

        // Calculate weighted score per spec
        await CalculateAndSetWeightedScoreAsync(entity, evaluation, cancellationToken);

        await _criterionScoreRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _criterionScoreRepository.GetQueryable()
                                                .Include(cs => cs.TemplateItem)
                                                .FirstOrDefaultAsync(cs => cs.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Criterion score added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<CriterionScoreDto>> GetCriterionScoresAsync(Guid evaluationId, CancellationToken cancellationToken = default)
    {
        var entities = await _criterionScoreRepository.GetQueryable(cs => cs.EvaluatorEvaluationId == evaluationId)
                                            .Include(cs => cs.TemplateItem)
                                            .OrderBy(cs => cs.TemplateItemId)
                                            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<CriterionScoreDto> UpdateCriterionScoreAsync(Guid evaluationId, UpdateCriterionScoreDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _criterionScoreRepository.GetQueryable()
            .Include(cs => cs.TemplateItem)
            .Include(cs => cs.EvaluatorEvaluation)
                .ThenInclude(e => e.Appraisal)
                    .ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(cs => cs.Id == updateDto.Id && cs.EvaluatorEvaluationId == evaluationId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Criterion score not found");

        updateDto.UpdateEntity(entity);

        // Recalculate weighted score per spec
        await CalculateAndSetWeightedScoreAsync(entity, entity.EvaluatorEvaluation, cancellationToken);

        await _criterionScoreRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Criterion score updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCriterionScoreAsync(Guid evaluationId, Guid scoreId, CancellationToken cancellationToken = default)
    {
        var entity = await _criterionScoreRepository.GetQueryable()
            .FirstOrDefaultAsync(cs => cs.Id == scoreId && cs.EvaluatorEvaluationId == evaluationId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Criterion score not found");

        await _criterionScoreRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Criterion score deleted successfully");

        return true;
    }

    /// <summary>
    /// Calculates and sets the WeightedScore for a CriterionScore based on spec:
    /// - KPI / Competency: WeightedScore = (NumericScore / MaxScore) × CriterionWeight × EvaluatorWeight
    ///
    /// Weight and grade-band max-score are resolved snapshot-first:
    /// if a <see cref="PerformanceAppraisalCriterionConfig"/> row exists for (appraisalId, criteriaId)
    /// it is used exclusively, ensuring mid-cycle template edits cannot affect in-progress scoring.
    /// Live template/PCM data is only consulted as a fallback for appraisals without a snapshot.
    /// </summary>
    private async Task CalculateAndSetWeightedScoreAsync(CriterionScore criterionScore, EvaluatorEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        var appraisalId = evaluation.AppraisalId;
        var positionId  = evaluation.Appraisal?.Employee?.PositionId;

        // Resolve weight and max-score from snapshot (immutable) or live data (fallback).
        var criterionWeight = await ResolveCriterionWeightAsync(
            criterionScore.TemplateItemId, appraisalId, positionId, cancellationToken);

        var evaluatorWeight = evaluation.EvaluatorWeight;

        if (criterionScore.NumericScore.HasValue)
        {
            var maxScore = await GetMaxScoreForCriteriaAsync(
                criterionScore.TemplateItemId, appraisalId, positionId, cancellationToken);

            criterionScore.WeightedScore = maxScore > 0
                ? (criterionScore.NumericScore.Value / maxScore) * criterionWeight * evaluatorWeight
                : 0;
        }
        else
        {
            criterionScore.WeightedScore = 0;
        }
    }

    #endregion

    #region AppraisalEmployeeResponse Operations

    public async Task<AppraisalEmployeeResponseDto> AddEmployeeResponseAsync(Guid appraisalId, CreateAppraisalEmployeeResponseDto createDto, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle).ThenInclude(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);
        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found");

        // Gate: employee responses must be enabled in settings.
        if (appraisal.AppraisalCycle?.AppraisalSettings?.AllowEmployeeResponse == false)
            throw new InvalidOperationException("Employee responses are not enabled for this appraisal cycle.");

        if (createDto.TemplateItemId.HasValue)
        {
            var templateItemConfigExists = await _criterionConfigRepository.ExistsAsync(
                c => c.PerformanceAppraisalId == appraisalId && c.TemplateItemId == createDto.TemplateItemId.Value);
            // FK constraint will also enforce validity
        }

        var entity = createDto.ToEntity();
        entity.AppraisalId = appraisalId;

        await _employeeResponseRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _employeeResponseRepository.GetQueryable()
            .Include(r => r.TemplateItem)
            .FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Employee response added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<AppraisalEmployeeResponseDto>> GetEmployeeResponsesAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await _employeeResponseRepository.GetQueryable(r => r.AppraisalId == appraisalId)
                                                        .Include(r => r.TemplateItem)
                                                        .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    #endregion

    #region AppraisalAttachment Operations

    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(Guid appraisalId, CreateAppraisalAttachmentDto createDto, CancellationToken cancellationToken = default)
    {
        var appraisalExists = await _appraisalRepository.ExistsAsync(a => a.Id == appraisalId);
        
        if (!appraisalExists)
            throw new ArgumentException("Performance appraisal not found");

        var entity = createDto.ToEntity();
        entity.PerformanceAppraisalId = appraisalId;

        await _appraisalAttachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalAttachmentRepository.GetQueryable(a => a.PerformanceAppraisalId == appraisalId)
                                                .OrderByDescending(a => a.UploadDate)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    /// <summary>
    /// Get all appraisals for a specific employee, including their role and pending actions
    /// </summary>
    public async Task<IEnumerable<MyAppraisalDto>> GetMyAppraisalsAsync(Guid employeeId, string? cycleFilter = null, CancellationToken cancellationToken = default)
    {
        // Get all appraisals where the employee is the subject
        var appraisalsQuery = _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.EvaluatorEvaluations)
            .Where(a => a.EmployeeId == employeeId);

        // Apply cycle filter if provided
        if (!string.IsNullOrEmpty(cycleFilter))
        {
            switch (cycleFilter.ToLower())
            {
                case "open":
                    appraisalsQuery = appraisalsQuery.Where(a => 
                        a.Status == AppraisalStatus.Active ||
                        a.Status == AppraisalStatus.Draft);
                    break;
                case "completed":
                    appraisalsQuery = appraisalsQuery.Where(a => a.Status == AppraisalStatus.Completed);
                    break;
            }
        }

        var appraisals = await appraisalsQuery
            .OrderByDescending(a => a.Year)
            .ThenByDescending(a => a.StartDate)
            .ToListAsync(cancellationToken);

        // Get all peer nominations where this employee is a peer evaluator
        var peerNominations = await _peerNominationRepository
            .GetQueryable()
            .Where(pn => pn.PeerEmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        var peerAppraisalIds = peerNominations.Select(pn => pn.AppraisalId).ToHashSet();

        var result = new List<MyAppraisalDto>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var appraisal in appraisals)
        {
            var isPeerEvaluator = peerAppraisalIds.Contains(appraisal.Id);
            var peerNomination = isPeerEvaluator 
                ? peerNominations.FirstOrDefault(pn => pn.AppraisalId == appraisal.Id) 
                : null;

            // Determine role
            string role = "Self";
            if (isPeerEvaluator)
            {
                role = "Peer";
            }

            // Check self-evaluation completion
            var selfEvaluation = appraisal.EvaluatorEvaluations
                .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
            bool selfEvaluationComplete = selfEvaluation?.SubmittedDate != null;

            // Check peer evaluation completion (if applicable)
            bool peerEvaluationComplete = false;
            if (isPeerEvaluator)
            {
                var peerEvaluation = appraisal.EvaluatorEvaluations
                    .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Peer && e.EvaluatorId == employeeId);
                peerEvaluationComplete = peerEvaluation?.SubmittedDate != null;
            }

            // Determine action required and action text
            bool actionRequired = false;
            string? actionText = null;
            DateOnly? dueDate = null;
            bool isOverdue = false;

            var settings = appraisal.AppraisalCycle?.AppraisalSettings;

            switch (appraisal.Status)
            {
                case AppraisalStatus.Draft:
                    actionRequired = true;
                    actionText = "Prepare for evaluation (goals pending)";
                    break;

                case AppraisalStatus.Active:
                    if (!selfEvaluationComplete)
                    {
                        actionRequired = true;
                        actionText = "Complete self-evaluation";
                        dueDate = appraisal.AppraisalCycle?.SelfEvaluationDeadline;
                        if (dueDate.HasValue && dueDate.Value < today)
                        {
                            isOverdue = true;
                        }
                    }
                    else if (isPeerEvaluator && !peerEvaluationComplete)
                    {
                        actionRequired = true;
                        actionText = "Provide peer feedback";
                        dueDate = appraisal.AppraisalCycle?.PeerEvaluationDeadline;
                        if (dueDate.HasValue && dueDate.Value < today)
                        {
                            isOverdue = true;
                        }
                    }
                    break;

                case AppraisalStatus.Governance:
                    if (!appraisal.EmployeeAcknowledged)
                    {
                        actionRequired = true;
                        actionText = "Acknowledge appraisal";
                        if (settings?.RequireEmployeeAcknowledgment == true)
                        {
                            dueDate = appraisal.AppraisalCycle?.EmployeeAcknowledgeDeadline ?? today.AddDays(7);
                        }
                    }
                    break;

                case AppraisalStatus.Completed:
                    // No action required if completed
                    break;
            }

            // Determine if appeal can be filed
            bool canFileAppeal = false;
            if (appraisal.Status == AppraisalStatus.Completed && !appraisal.HasAppeal)
            {
                if (settings?.EnableAppeals == true)
                {
                    // Check if appeal window is still open
                    var appealDeadline = appraisal.AppraisalCycle?.EndDate.AddDays(settings.AppealWindowDays);
                    canFileAppeal = !appealDeadline.HasValue || today <= appealDeadline.Value;
                }
            }

            result.Add(new MyAppraisalDto
            {
                AppraisalId = appraisal.Id,
                AppraisalNumber = appraisal.AppraisalNumber,
                AppraisalCycleId = appraisal.AppraisalCycleId,
                AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "N/A",
                Year = appraisal.Year,
                PeriodStart = appraisal.StartDate,
                PeriodEnd = appraisal.EndDate,
                Status = appraisal.Status,
                MyRole = role,
                ActionRequired = actionRequired,
                ActionText = actionText,
                DueDate = dueDate,
                OverallScore = appraisal.OverallScore,
                IsAcknowledged = appraisal.EmployeeAcknowledgedDate.HasValue,
                AppealFiled = appraisal.HasAppeal,
                AppealStatus = appraisal.CurrentAppealStatus,
                CanFileAppeal = canFileAppeal,
                SelfEvaluationComplete = selfEvaluationComplete,
                PeerEvaluationComplete = peerEvaluationComplete,
                IsOverdue = isOverdue
            });
        }

        return result;
    }

    #endregion

    #region Self-Evaluation Operations

    /// <summary>
    /// Gets the complete context for an employee's self-evaluation page
    /// Includes appraisal info, KPIs with targets, grade ranges, and existing evaluations
    /// </summary>
    public async Task<SelfEvaluationContextDto> GetSelfEvaluationContextAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        // Get appraisal with all related data
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.GradeRanges)
                    .ThenInclude(gr => gr.GradeDefinition)
            .Include(a => a.Template)
                .ThenInclude(t => t.Sections)
                    .ThenInclude(s => s.TemplateItems)
            .Include(a => a.CustomQuestionResponses)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found");

        // Check if employee is authorized (basic check)
        // Additional authorization should be done at controller/API level

        // Get employee's KPI targets are now managed via EmployeeGoal - not loaded here.

        // Get existing self-evaluation if it exists
        var selfEvaluation = appraisal.EvaluatorEvaluations
            .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);

        bool isSelfEvaluationSubmitted = selfEvaluation?.SubmittedDate.HasValue ?? false;
        // Editable if not submitted and status is Open or SelfEvaluation
        bool isEditable = !isSelfEvaluationSubmitted && 
            (appraisal.Status == AppraisalStatus.Active || appraisal.Status == AppraisalStatus.Draft);
        
        // Get soft skill self-rating flag
        bool allowSelfSoftSkillRating = appraisal.AppraisalCycle?.AppraisalSettings?.AllowSelfSoftSkillRating ?? false;

        return new SelfEvaluationContextDto
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeeNumber = appraisal.Employee.EmployeeNumber,
            AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "N/A",
            PeriodStart = appraisal.StartDate,
            PeriodEnd = appraisal.EndDate,
            Status = appraisal.Status,
            IsSelfEvaluationSubmitted = isSelfEvaluationSubmitted,
            SelfEvaluationSubmittedDate = selfEvaluation?.SubmittedDate,
            IsEditable = isEditable,
            SelfEvaluationDeadline = appraisal.AppraisalCycle?.SelfEvaluationDeadline,
            AllowSelfSoftSkillRating = allowSelfSoftSkillRating,
            Settings = appraisal.AppraisalCycle?.AppraisalSettings?.ToDto(),
            Sections = BuildSelfEvaluationSections(appraisal, selfEvaluation, appraisal.CustomQuestionResponses)
        };
    }

    /// <summary>
    /// Saves or updates self-evaluation (draft or submission)
    /// </summary>
    public async Task<SelfEvaluationResultDto> SaveSelfEvaluationAsync(SaveSelfEvaluationDto saveDto, CancellationToken cancellationToken = default)
    {
        // Get appraisal
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self))
            .Include(a => a.PeerNominations)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == saveDto.AppraisalId, cancellationToken);

        if (appraisal == null)
            return new SelfEvaluationResultDto { Success = false, Message = "Appraisal not found" };

        // Validate employee
        if (appraisal.EmployeeId != saveDto.EmployeeId)
            return new SelfEvaluationResultDto { Success = false, Message = "Employee mismatch" };

        // Check if editable
        var existingSelfEval = appraisal.EvaluatorEvaluations
            .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);

        if (existingSelfEval?.SubmittedDate.HasValue ?? false)
            return new SelfEvaluationResultDto { Success = false, Message = "Self-evaluation already submitted" };

        if (appraisal.Status != AppraisalStatus.Active && appraisal.Status != AppraisalStatus.Draft)
            return new SelfEvaluationResultDto { Success = false, Message = "Appraisal is not open for self-evaluation" };

        // Enforce the 0–100 score invariant (applies to drafts and submissions alike).
        if (saveDto.ItemScores.Any(e => e.NumericScore.HasValue && !AppraisalScoring.IsValidScore(e.NumericScore.Value)))
            return new SelfEvaluationResultDto { Success = false, Message = $"Scores must be between {AppraisalScoring.MinScore:0} and {AppraisalScoring.MaxScore:0}." };

        // Validate all KPIs are evaluated if submitting (not draft)
        if (!saveDto.IsDraft)
        {
            // ItemScores only contains scored items (client filters out unscored ones before sending).
            // A score row with both values null should never arrive; treat it as incomplete.
            if (saveDto.ItemScores.Any(e => !e.ActualValue.HasValue && !e.NumericScore.HasValue))
                return new SelfEvaluationResultDto { Success = false, Message = "One or more scored items are missing a value. Please complete all evaluations before submitting." };

            // Enforce peer nominations when required and employee-driven nomination mode
            var settings = appraisal.AppraisalCycle?.AppraisalSettings;
            if (settings != null && settings.RequirePeerReviews && settings.PeerNominationMode == PeerNominationMode.Employee)
            {
                var nominatedPeers = appraisal.PeerNominations?.Count ?? 0;
                if (nominatedPeers < settings.MinPeerEvaluators || nominatedPeers > settings.MaxPeerEvaluators)
                {
                    return new SelfEvaluationResultDto
                    {
                        Success = false,
                        Message = $"Peer reviews are required. Please nominate at least {settings.MinPeerEvaluators} and no more than {settings.MaxPeerEvaluators} peers before submitting."
                    };
                }
            }

            // Enforce competency/soft-skill completion when enabled
            var allowSelfSoftSkillRating = appraisal.AppraisalCycle?.AppraisalSettings?.AllowSelfSoftSkillRating ?? false;
            if (allowSelfSoftSkillRating && appraisal.Employee.PositionId != Guid.Empty)
            {
                var requiredCompetencyIds = await _templateItemRepository.GetQueryable()
                    .Include(i => i.Section)
                    .Where(i => i.Section.AppraisalTemplateId == appraisal.AppraisalTemplateId)
                    .Where(i => i.CompetencyId != null)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken);

                if (requiredCompetencyIds.Any())
                {
                    var submittedCompetencyIds = saveDto.ItemScores
                        .Where(r => r.NumericScore.HasValue)
                        .Select(r => r.TemplateItemId)
                        .ToHashSet();

                    var missing = requiredCompetencyIds
                        .Where(id => !submittedCompetencyIds.Contains(id))
                        .ToList();
                    if (missing.Any())
                    {
                        return new SelfEvaluationResultDto
                        {
                            Success = false,
                            Message = "Please rate all competencies/soft skills before submitting."
                        };
                    }
                }
            }
        }

        try
        {
            // Create or update EvaluatorEvaluation
            EvaluatorEvaluation evaluatorEvaluation;

            if (existingSelfEval == null)
            {
                // Create new self-evaluation
                var settings = appraisal.AppraisalCycle.AppraisalSettings;
                
                evaluatorEvaluation = new EvaluatorEvaluation
                {
                    AppraisalId = appraisal.Id,
                    EvaluatorId = saveDto.EmployeeId,
                    EvaluatorRole = EvaluatorRole.Self,
                    EvaluatorWeight = settings.SelfEvaluationWeight,
                    IsAuthoritative = false,
                    StartedDate = DateTime.UtcNow, // Set on first save (draft or submit)
                    SubmittedDate = saveDto.IsDraft ? null : DateTime.UtcNow
                };
                
                await _evaluatorEvaluationRepository.AddAsync(evaluatorEvaluation);
            }
            else
            {
                evaluatorEvaluation = existingSelfEval;
                
                // Set StartedDate if not already set (in case first save was missed)
                if (!evaluatorEvaluation.StartedDate.HasValue)
                {
                    evaluatorEvaluation.StartedDate = DateTime.UtcNow;
                }
                
                if (!saveDto.IsDraft)
                {
                    evaluatorEvaluation.SubmittedDate = DateTime.UtcNow;
                }
            }

            // Save or update scores (KPI and competency items unified under ItemScores)
            var allowSelfSoftSkillRating = appraisal.AppraisalCycle?.AppraisalSettings?.AllowSelfSoftSkillRating ?? false;
            
            if (saveDto.ItemScores.Any())
            {
                // Need to save changes first to get the evaluatorEvaluation.Id if it's new
                if (existingSelfEval == null)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                foreach (var itemInput in saveDto.ItemScores)
                {
                    // Check if criterion score already exists
                    var existingScore = await _criterionScoreRepository.GetQueryable()
                        .FirstOrDefaultAsync(cs => cs.EvaluatorEvaluationId == evaluatorEvaluation.Id
                                                 && cs.TemplateItemId == itemInput.TemplateItemId,
                                           cancellationToken);

                    if (existingScore == null)
                    {
                        // Create new criterion score
                        var newScore = new CriterionScore
                        {
                            EvaluatorEvaluationId = evaluatorEvaluation.Id,
                            TemplateItemId = itemInput.TemplateItemId,
                            NumericScore = itemInput.NumericScore,
                            ActualValue  = itemInput.ActualValue,
                            Notes = itemInput.Notes
                        };

                        // Calculate WeightedScore immediately per spec
                        await CalculateAndSetWeightedScoreAsync(newScore, evaluatorEvaluation, cancellationToken);

                        await _criterionScoreRepository.AddAsync(newScore);
                    }
                    else
                    {
                        // Update existing score
                        existingScore.NumericScore = itemInput.NumericScore;
                        existingScore.ActualValue  = itemInput.ActualValue;
                        existingScore.Notes = itemInput.Notes;

                        // Recalculate WeightedScore
                        await CalculateAndSetWeightedScoreAsync(existingScore, evaluatorEvaluation, cancellationToken);

                        await _criterionScoreRepository.UpdateAsync(existingScore);
                    }
                }
            }

            // Calculate and set TotalScore only when submitting (not draft)
            if (!saveDto.IsDraft)
            {
                // Need to reload the evaluator evaluation with all criterion scores to calculate TotalScore
                var evalWithScores = await _evaluatorEvaluationRepository.GetQueryable()
                    .Include(e => e.CriterionScores)
                    .FirstOrDefaultAsync(e => e.Id == evaluatorEvaluation.Id, cancellationToken);
                
                if (evalWithScores != null && evalWithScores.CriterionScores.Any())
                {
                    evalWithScores.TotalScore = evalWithScores.CriterionScores.Sum(cs => cs.WeightedScore);
                    await _evaluatorEvaluationRepository.UpdateAsync(evalWithScores);
                }

                // Update appraisal status when self-evaluation is submitted
                var newStatus = await DetermineNextStatusAsync(appraisal, appraisal.Status, EvaluatorRole.Self, cancellationToken);
                if (newStatus != appraisal.Status)
                {
                    appraisal.Status = newStatus;
                    await _appraisalRepository.UpdateAsync(appraisal);
                }
            }
            else
            {
                // Update status when saving as draft (from Open to SelfEvaluation)
                var settings = appraisal.AppraisalCycle.AppraisalSettings;
                var draftStatus = UpdateStatusOnDraft(appraisal.Status, EvaluatorRole.Self, settings);
                if (draftStatus != appraisal.Status)
                {
                    appraisal.Status = draftStatus;
                    await _appraisalRepository.UpdateAsync(appraisal);
                }
            }

            // Save custom question responses
            if (saveDto.CustomQuestionResponses.Any())
            {
                foreach (var input in saveDto.CustomQuestionResponses)
                {
                    var existingResp = await _customQuestionResponseRepository.GetQueryable()
                        .FirstOrDefaultAsync(r => r.PerformanceAppraisalId == appraisal.Id
                                               && r.TemplateItemId == input.TemplateItemId,
                                           cancellationToken);

                    if (existingResp == null)
                    {
                        await _customQuestionResponseRepository.AddAsync(new AppraisalCustomQuestionResponse
                        {
                            PerformanceAppraisalId = appraisal.Id,
                            TemplateItemId         = input.TemplateItemId,
                            ResponseText           = input.ResponseText,
                            IsDraft                = saveDto.IsDraft,
                            SubmittedDate          = saveDto.IsDraft ? null : DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existingResp.ResponseText  = input.ResponseText;
                        existingResp.IsDraft        = saveDto.IsDraft;
                        if (!saveDto.IsDraft)
                            existingResp.SubmittedDate = DateTime.UtcNow;
                        await _customQuestionResponseRepository.UpdateAsync(existingResp);
                    }
                }
            }

            // Upsert goal appraisal assessments (self-assessment fields)
            if (saveDto.GoalAssessments != null && saveDto.GoalAssessments.Count > 0)
            {
                var goalLookup = appraisal.Goals?.ToDictionary(g => g.Id) ?? new Dictionary<Guid, EmployeeGoal>();

                foreach (var ga in saveDto.GoalAssessments)
                {
                    // Derive FinalProgressPercent server-side for KPI goals so the stored
                    // value always reflects the goal's measurement semantics rather than
                    // whatever the client slider happened to be set to.
                    decimal? computedProgress = ga.FinalProgressPercent;
                    if (goalLookup.TryGetValue(ga.GoalId, out var goal)
                        && goal.KpiDefinitionId.HasValue
                        && goal.MeasurementType != MeasurementType.Boolean
                        && ga.FinalActualValue.HasValue)
                    {
                        computedProgress = CalculateKpiAchievement(
                            ga.FinalActualValue.Value, goal.TargetValue, goal.MinValue, goal.MaxValue);
                    }

                    var existing = await _goalAssessmentRepository.GetQueryable()
                        .FirstOrDefaultAsync(a => a.EmployeeGoalId == ga.GoalId
                                               && a.PerformanceAppraisalId == appraisal.Id,
                                           cancellationToken);

                    if (existing == null)
                    {
                        await _goalAssessmentRepository.AddAsync(new EmployeeGoalAppraisalAssessment
                        {
                            EmployeeGoalId          = ga.GoalId,
                            PerformanceAppraisalId  = appraisal.Id,
                            TenantId                = appraisal.TenantId,
                            SelfFinalProgressPercent = computedProgress,
                            SelfFinalStatus         = ga.FinalStatus,
                            SelfFinalActualValue    = ga.FinalActualValue,
                            SelfAssessmentNotes     = ga.AssessmentNotes,
                            SelfEvidenceLinks       = ga.EvidenceLinks
                        });
                    }
                    else
                    {
                        existing.SelfFinalProgressPercent = computedProgress;
                        existing.SelfFinalStatus         = ga.FinalStatus;
                        existing.SelfFinalActualValue    = ga.FinalActualValue;
                        existing.SelfAssessmentNotes     = ga.AssessmentNotes;
                        existing.SelfEvidenceLinks       = ga.EvidenceLinks;
                        await _goalAssessmentRepository.UpdateAsync(existing);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SelfEvaluationResultDto
            {
                Success = true,
                Message = saveDto.IsDraft ? "Self-evaluation saved as draft" : "Self-evaluation submitted successfully",
                EvaluatorEvaluationId = evaluatorEvaluation.Id,
                SubmittedDate = evaluatorEvaluation.SubmittedDate
            };
        }
        catch (Exception ex)
        {
            return new SelfEvaluationResultDto
            {
                Success = false,
                Message = $"Error saving self-evaluation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Get read-only view of submitted self-evaluation
    /// </summary>
    public async Task<ViewSubmittedEvaluationDto> GetViewSubmittedEvaluationAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee.Department)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.GradeRanges)
                    .ThenInclude(gr => gr.GradeDefinition)
            .Include(a => a.Template)
                .ThenInclude(t => t.Sections)
                    .ThenInclude(s => s.TemplateItems)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException($"Appraisal {appraisalId} not found");

        var selfEvaluation = appraisal.EvaluatorEvaluations
            .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);

        if (selfEvaluation == null || !selfEvaluation.SubmittedDate.HasValue)
            throw new InvalidOperationException("Self-evaluation has not been submitted yet");

        var settings = appraisal.AppraisalCycle.AppraisalSettings;

        // KPI targets are now managed via EmployeeGoal - not loaded here.

        // Load attachments (read-only list)
        var attachments = await _appraisalAttachmentRepository.GetQueryable()
            .Where(a => a.PerformanceAppraisalId == appraisalId)
            .ToListAsync(cancellationToken);

        // Check peer/manager review progress (visual-only)
        var peerEvaluations = await _evaluatorEvaluationRepository.GetQueryable()
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Peer)
            .ToListAsync(cancellationToken);

        var managerEvaluation = await _evaluatorEvaluationRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager, cancellationToken);

        var hrEvaluation = await _evaluatorEvaluationRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.HR, cancellationToken);

        return new ViewSubmittedEvaluationDto
        {
            AppraisalId = appraisal.Id,
            AppraisalCycleName = appraisal.AppraisalCycle.CycleName,
            PeriodStart = appraisal.StartDate,
            PeriodEnd = appraisal.EndDate,
            Year = appraisal.Year,

            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeePosition = appraisal.Employee.Position?.Title,
            Department = appraisal.Employee.Department?.Name,

            SubmittedDate = selfEvaluation.SubmittedDate.Value,
            CurrentStatus = appraisal.Status,

            RequirePeerReviews = settings.RequirePeerReviews,
            PeerReviewsInProgress = peerEvaluations.Any(),
            ManagerReviewComplete = managerEvaluation?.SubmittedDate.HasValue ?? false,
            RequireHRReview = settings.RequireHRReview,
            HRReviewComplete = hrEvaluation?.SubmittedDate.HasValue ?? false,

            AllowSelfSoftSkillRating = settings.AllowSelfSoftSkillRating,

            Sections = BuildSubmittedEvaluationSections(appraisal, selfEvaluation),

            Attachments = attachments
                .Select(a => new SubmittedAttachmentDto
                {
                    AttachmentId = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    Description = a.Description,
                    UploadedDate = a.UploadDate
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Helper method to calculate KPI achievement percentage
    /// </summary>
    private decimal CalculateKpiAchievement(decimal actualValue, decimal? targetValue, decimal? minValue, decimal? maxValue)
    {
        if (!targetValue.HasValue || targetValue.Value == 0)
            return 0;

        // Respect an explicit KPI ceiling: an actual beyond MaxValue counts as attainment of the cap,
        // not unbounded over-achievement, before the percentage is computed.
        if (maxValue.HasValue && actualValue > maxValue.Value)
            actualValue = maxValue.Value;

        decimal achievementPercent;

        if (minValue.HasValue && targetValue.Value != minValue.Value)
        {
            // Two-segment: below min = 0%, at target = 100%
            // Formula: (actual - min) / (target - min) * 100
            if (actualValue <= minValue.Value)
                return 0;

            achievementPercent = (actualValue - minValue.Value) / (targetValue.Value - minValue.Value) * 100;
        }
        else
        {
            // No min defined: simple ratio
            achievementPercent = (actualValue / targetValue.Value) * 100;
        }

        // Bound to the 0–100 invariant — hitting target is full score, no extra credit.
        return AppraisalScoring.Clamp(achievementPercent);
    }

    /// <summary>
    /// Resolves the criterion weight, checking WeightOverride first, then falling back to template item weight.
    /// </summary>
    /// <summary>
    /// Returns the criterion weight for <paramref name="criteriaId"/> inside <paramref name="appraisalId"/>.
    /// Reads from the generation-time <see cref="PerformanceAppraisalCriterionConfig"/> snapshot first.
    /// Falls back to live PositionCriteriaMapping data only when no snapshot row exists.
    /// </summary>
    private async Task<int> ResolveCriterionWeightAsync(
        Guid templateItemId, Guid appraisalId, Guid? positionId, CancellationToken cancellationToken = default)
    {
        // ── Snapshot path (preferred) ────────────────────────────────────────
        var snapshot = await _criterionConfigRepository.GetQueryable()
            .Where(c => c.PerformanceAppraisalId == appraisalId && c.TemplateItemId == templateItemId)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot != null)
            return snapshot.WeightUsed;

        // ── Live fallback (appraisals generated before the snapshot feature) ──
        var templateItem = await _templateItemRepository.GetQueryable()
            .FirstOrDefaultAsync(i => i.Id == templateItemId, cancellationToken);

        return templateItem?.Weight ?? 0;
    }

    /// <summary>
    /// Returns the maximum grade-band score for <paramref name="criteriaId"/> inside <paramref name="appraisalId"/>.
    /// Reads from the generation-time <see cref="PerformanceAppraisalCriterionConfig"/> grade-range snapshot first.
    /// Falls back to live PositionCriteriaMapping data only when no snapshot row exists.
    /// </summary>
    private async Task<decimal> GetMaxScoreForCriteriaAsync(
        Guid templateItemId, Guid appraisalId, Guid? positionId, CancellationToken cancellationToken = default)
    {
        // ── Snapshot path (preferred) ────────────────────────────────────────
        var snapshot = await _criterionConfigRepository.GetQueryable()
            .Where(c => c.PerformanceAppraisalId == appraisalId && c.TemplateItemId == templateItemId)
            .Include(c => c.GradeRanges)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot != null && snapshot.GradeRanges.Any())
            return snapshot.GradeRanges.Max(r => r.HighScore);

        // ── Live fallback (appraisals generated before the snapshot feature) ──
        var templateItem = await _templateItemRepository.GetQueryable()
            .Include(ti => ti.GradeRanges)
            .FirstOrDefaultAsync(i => i.Id == templateItemId, cancellationToken);

        if (templateItem != null && templateItem.GradeRanges.Any())
            return templateItem.GradeRanges.Max(r => r.HighScore);

        // Default fallback: the appraisal module enforces a strict 0–100 score invariant
        // (see AppraisalScoring.MaxScore), so an un-banded criterion is scored out of 100.
        return 100;
    }

    /// <summary>
    /// Determines the next persisted <see cref="AppraisalStatus"/> after an evaluator submits.
    /// In the new 6-value lifecycle model, fine-grained phase is computed dynamically by
    /// <see cref="IAppraisalWorkflowService.GetCurrentPhase"/>; this method only drives the
    /// coarse-grained lifecycle state transitions.
    /// </summary>
    private async Task<AppraisalStatus> DetermineNextStatusAsync(
        PerformanceAppraisal appraisal,
        AppraisalStatus currentStatus,
        EvaluatorRole completedRole,
        CancellationToken cancellationToken = default)
    {
        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null) return currentStatus;

        switch (completedRole)
        {
            case EvaluatorRole.Self:
                // Self submitted: remain Active — phase advances automatically
                if (currentStatus == AppraisalStatus.Active || currentStatus == AppraisalStatus.Draft)
                    return AppraisalStatus.Active;
                break;

            case EvaluatorRole.Peer:
                // Peer submitted: check threshold; if all governance steps are skipped → Completed
                if (currentStatus == AppraisalStatus.Active)
                {
                    var submittedPeers = await _evaluatorEvaluationRepository.GetQueryable()
                        .Where(e => e.AppraisalId == appraisal.Id
                                 && e.EvaluatorRole == EvaluatorRole.Peer
                                 && e.SubmittedDate.HasValue)
                        .CountAsync(cancellationToken);

                    // As long as manager or any governance step is still required, stay Active
                    if (submittedPeers >= settings.MinPeerEvaluators
                        && !settings.RequireManagerEvaluation
                        && !settings.RequireCalibration
                        && !settings.RequireHRReview
                        && !settings.RequireEmployeeAcknowledgment)
                    {
                        return AppraisalStatus.Completed;
                    }

                    return AppraisalStatus.Active;
                }
                break;

            case EvaluatorRole.Manager:
                // Manager submitted: escalate to Governance when any governance step is required
                if (currentStatus == AppraisalStatus.Active)
                {
                    return (settings.RequireCalibration || settings.RequireHRReview || settings.RequireEmployeeAcknowledgment)
                        ? AppraisalStatus.Governance
                        : AppraisalStatus.Completed;
                }
                break;

            case EvaluatorRole.HR:
                // HR review approved: Governance → Completed (unless employee acknowledgment still pending)
                // AcknowledgeAppraisalAsync drives the final Governance → Completed transition.
                if (currentStatus == AppraisalStatus.Governance)
                {
                    return settings.RequireEmployeeAcknowledgment
                        ? AppraisalStatus.Governance   // Employee must acknowledge; handled by AcknowledgeAppraisalAsync
                        : AppraisalStatus.Completed;
                }
                break;
        }

        return currentStatus; // No status change
    }

    /// <summary>
    /// Updates appraisal status when an evaluation is started and saved as a draft.
    /// In the new lifecycle model, the only draft-save transition that changes status is
    /// <see cref="AppraisalStatus.Draft"/> → <see cref="AppraisalStatus.Active"/> when any
    /// evaluator first touches the appraisal. All in-progress phases are encoded in Active;
    /// no further promotions happen on draft saves.
    /// </summary>
    private static AppraisalStatus UpdateStatusOnDraft(AppraisalStatus currentStatus, EvaluatorRole role, AppraisalSettings settings)
    {
        // Transition Draft → Active the first time someone starts filling in the appraisal
        if (currentStatus == AppraisalStatus.Draft)
            return AppraisalStatus.Active;

        return currentStatus;
    }
    
    /// <summary>
    /// Creates immutable snapshot of manager evaluation state before appeal remand.
    /// Preserves original scores for before/after comparison.
    /// </summary>
    private async Task<Guid> CreateManagerEvaluationSnapshotAsync(
        Guid appraisalId, 
        string snapshotReason = "Appeal Remand",
        CancellationToken cancellationToken = default)
    {
        // Load manager evaluation with all related data
        var managerEvaluation = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.CriterionScores)
            .Include(e => e.Appraisal)
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);
            
        if (managerEvaluation == null)
        {
            throw new InvalidOperationException(
                $"No manager evaluation found for appraisal {appraisalId}. Cannot create snapshot.");
        }
        
        if (!managerEvaluation.SubmittedDate.HasValue)
        {
            throw new InvalidOperationException(
                "Manager evaluation must be submitted before creating snapshot.");
        }
        
        var snapshotDate = DateTime.UtcNow;
        
        // Create evaluation snapshot
        var evaluationSnapshot = new AppraisalEvaluationSnapshot
        {
            AppraisalId = appraisalId,
            EvaluatorId = managerEvaluation.EvaluatorId,
            EvaluatorRole = EvaluatorRole.Manager,
            TotalScore = managerEvaluation.TotalScore,
            SnapshotDate = snapshotDate,
            SnapshotReason = snapshotReason,
            TenantId = managerEvaluation.TenantId
        };
        
        await _evaluationSnapshotRepository.AddAsync(evaluationSnapshot);
        
        // Snapshot each criterion score
        foreach (var criterionScore in managerEvaluation.CriterionScores)
        {
            var criterionScoreSnapshot = new AppraisalCriterionScoreSnapshot
            {
                AppraisalEvaluationSnapshotId = evaluationSnapshot.Id,
                TemplateItemId = criterionScore.TemplateItemId,
                NumericScore = criterionScore.NumericScore,
                WeightedScore = criterionScore.WeightedScore,
                Notes = criterionScore.Notes,
                TenantId = criterionScore.TenantId
            };
            
            await _criterionScoreSnapshotRepository.AddAsync(criterionScoreSnapshot);
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created manager evaluation snapshot {SnapshotId} for appraisal {AppraisalId}. Reason: {Reason}",
            evaluationSnapshot.Id,
            appraisalId,
            snapshotReason);
            
        return evaluationSnapshot.Id;
    }

    #endregion

    #region Manager Evaluation Operations

    /// <summary>
    /// Gets all appraisal cycles where the manager has team members to evaluate
    /// </summary>
    public async Task<IEnumerable<TeamAppraisalCycleSummaryDto>> GetTeamAppraisalCyclesAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        // Get all cycles where this manager has direct reports with appraisals
        var cycles = await _appraisalCycleRepository.GetQueryable()
            .Include(c => c.PerformanceAppraisals)
                .ThenInclude(a => a.Employee)
            .Include(c => c.PerformanceAppraisals)
                .ThenInclude(a => a.EvaluatorEvaluations)
            .Where(c => c.Status == AppraisalCycleStatus.Open || c.Status == AppraisalCycleStatus.InProgress)
            .Where(c => c.PerformanceAppraisals.Any(a => a.Employee.ManagerId == managerId))
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        var summaries = new List<TeamAppraisalCycleSummaryDto>();

        foreach (var cycle in cycles)
        {
            var teamAppraisals = cycle.PerformanceAppraisals.Where(a => a.Employee.ManagerId == managerId).ToList();
            
            var totalEmployees = teamAppraisals.Count;
            var evaluatedCount = teamAppraisals.Count(a => 
                a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate.HasValue));
            var inProgressCount = teamAppraisals.Count(a => 
                a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.StartedDate.HasValue && !e.SubmittedDate.HasValue));
            var pendingCount = totalEmployees - evaluatedCount - inProgressCount;

            summaries.Add(new TeamAppraisalCycleSummaryDto
            {
                CycleId = cycle.Id,
                CycleName = cycle.CycleName,
                PeriodStart = cycle.StartDate,
                PeriodEnd = cycle.EndDate,
                Status = cycle.Status,
                TotalEmployees = totalEmployees,
                EvaluatedCount = evaluatedCount,
                PendingCount = pendingCount,
                InProgressCount = inProgressCount
            });
        }

        return summaries;
    }

    /// <summary>
    /// Gets all team members (direct reports) in a specific appraisal cycle
    /// </summary>
    public async Task<IEnumerable<TeamMemberAppraisalDto>> GetTeamMemberAppraisalsAsync(Guid cycleId, Guid managerId, CancellationToken cancellationToken = default)
    {
        var appraisals = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.EvaluatorEvaluations)
            .Where(a => a.AppraisalCycleId == cycleId && a.Employee.ManagerId == managerId)
            .OrderBy(a => a.Employee.FirstName)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync(cancellationToken);

        var teamMembers = new List<TeamMemberAppraisalDto>();

        foreach (var appraisal in appraisals)
        {
            var selfEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
            var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

            teamMembers.Add(new TeamMemberAppraisalDto
            {
                AppraisalId = appraisal.Id,
                CycleName = appraisal.AppraisalCycle?.CycleName ?? string.Empty,
                EmployeeId = appraisal.EmployeeId,
                EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
                EmployeeNumber = appraisal.Employee.EmployeeNumber,
                Position = appraisal.Employee.Position?.Title,
                OrganizationUnit = appraisal.Employee.OrganizationUnit?.Name,
                SelfEvaluationSubmitted = selfEvaluation?.SubmittedDate.HasValue ?? false,
                SelfEvaluationSubmittedDate = selfEvaluation?.SubmittedDate,
                ManagerEvaluationStarted = managerEvaluation?.StartedDate.HasValue ?? false,
                ManagerEvaluationSubmitted = managerEvaluation?.SubmittedDate.HasValue ?? false,
                ManagerEvaluationSubmittedDate = managerEvaluation?.SubmittedDate,
                AppraisalStatus = appraisal.Status,
                RequireSelfEvaluation = appraisal.AppraisalCycle?.AppraisalSettings?.RequireSelfEvaluation ?? true,
                IsRemandedAppeal = appraisal.AppealRemandedDate.HasValue,
                AppealRemandDeadline = appraisal.AppealRemandDeadline,
                OverallScore = appraisal.OverallScore
            });
        }

        return teamMembers;
    }

    /// <summary>
    /// Gets complete context for manager evaluation page, including employee self-evaluations
    /// </summary>
    public async Task<ManagerEvaluationContextDto> GetManagerEvaluationContextAsync(Guid appraisalId, Guid managerId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self || e.EvaluatorRole == EvaluatorRole.Manager))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.Appeals)
                .ThenInclude(aa => aa.Items)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.GradeRanges)
                    .ThenInclude(gr => gr.GradeDefinition)
            .Include(a => a.Template)
                .ThenInclude(t => t.Sections)
                    .ThenInclude(s => s.TemplateItems)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        // Verify manager authorization
        if (appraisal.Employee.ManagerId != managerId)
            throw new UnauthorizedAccessException("You are not authorized to evaluate this employee");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        var selfEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
        var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

        bool isManagerEvaluationSubmitted = managerEvaluation?.SubmittedDate.HasValue ?? false;
        // Editable if not submitted and status is appropriate for manager evaluation (Open, PeerEvaluation, or ManagerEvaluation)
        // Editable if manager has not yet submitted and appraisal is in an active workflow state
        bool isEditable = !isManagerEvaluationSubmitted && 
            (appraisal.Status == AppraisalStatus.Active || appraisal.Status == AppraisalStatus.Draft);

        // Pre-compute appealed criteria IDs (passed to section builder so it can mark items)
        var appealedCriteriaIds = new List<Guid>();
        if (appraisal.AppealRemandedDate.HasValue && appraisal.Appeals.Any())
        {
            var latestAppeal = appraisal.Appeals
                .OrderByDescending(a => a.SubmittedDate)
                .FirstOrDefault();
            if (latestAppeal?.Items != null)
            {
                appealedCriteriaIds = latestAppeal.Items
                    .Where(item => item.TemplateItemId.HasValue)
                    .Select(item => item.TemplateItemId!.Value)
                    .ToList();
            }
        }

        var result = new ManagerEvaluationContextDto
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeeNumber = appraisal.Employee.EmployeeNumber,
            Position = appraisal.Employee.Position?.Title,
            Department = appraisal.Employee.OrganizationUnit?.Name,
            CycleId = appraisal.AppraisalCycleId,
            AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "N/A",
            PeriodStart = appraisal.StartDate,
            PeriodEnd = appraisal.EndDate,
            ManagerEvaluationDeadline = appraisal.AppraisalCycle?.ManagerEvaluationDeadline,
            Status = appraisal.Status,

            // Appeal remand information
            IsRemandedAppeal = appraisal.AppealRemandedDate.HasValue,
            AppealRemandedDate = appraisal.AppealRemandedDate,
            AppealRemandDeadline = appraisal.AppealRemandDeadline,
            IsRemandDeadlineExceeded = appraisal.AppealRemandDeadline.HasValue && DateTime.UtcNow > appraisal.AppealRemandDeadline.Value,
            AppealedKpiIds = new(),
            AppealedTemplateItemIds = appealedCriteriaIds,

            ManagerEvaluatorEvaluationId = managerEvaluation?.Id,
            IsManagerEvaluationSubmitted = isManagerEvaluationSubmitted,
            ManagerEvaluationSubmittedDate = managerEvaluation?.SubmittedDate,
            IsEditable = isEditable,
            SelfEvaluationWeight = settings?.SelfEvaluationWeight ?? 0,
            ManagerEvaluationWeight = settings?.ManagerEvaluationWeight ?? 0,
            PeerEvaluationWeight = settings?.PeerEvaluationWeight ?? 0,
            IsManagerAuthoritative = settings?.IsManagerAuthoritative ?? false,
            Settings = settings?.ToDto(),
            // Manager Final Assessment fields from PerformanceAppraisal entity
            OverallComments = appraisal.OverallComments,
            StrengthsIdentified = appraisal.StrengthsIdentified,
            AreasForImprovement = appraisal.AreasForImprovement,
            TrainingNeeds = appraisal.TrainingNeeds,
            CareerAspirations = appraisal.CareerAspirations,
            RecommendPromotion = appraisal.RecommendPromotion,
            RecommendIncrement = appraisal.RecommendIncrement,
            RecommendTraining = appraisal.RecommendTraining,
            RecommendPIP = appraisal.RecommendPIP,
            RecommendTermination = appraisal.RecommendTermination,
            RecommendationNotes = appraisal.RecommendationNotes,
            Sections = BuildManagerEvaluationSections(appraisal, selfEvaluation, managerEvaluation, appealedCriteriaIds)
        };

        return result;
    }

    /// <summary>
    /// Saves or updates manager evaluation (draft or submission)
    /// </summary>
    public async Task<ManagerEvaluationResultDto> SaveManagerEvaluationAsync(SaveManagerEvaluationDto saveDto, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Manager))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == saveDto.AppraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        // Verify manager authorization
        if (appraisal.Employee.ManagerId != saveDto.ManagerId)
            throw new UnauthorizedAccessException("You are not authorized to evaluate this employee");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal settings not found");

        // Enforce the 0–100 score invariant (applies to drafts and submissions alike).
        if (saveDto.ItemScores.Any(e => e.NumericScore.HasValue && !AppraisalScoring.IsValidScore(e.NumericScore.Value)))
            return new ManagerEvaluationResultDto { Success = false, Message = $"Scores must be between {AppraisalScoring.MinScore:0} and {AppraisalScoring.MaxScore:0}." };

        // Check if this is a remanded appeal re-evaluation
        bool isRemandedReevaluation = appraisal.AppealRemandedDate.HasValue;
        
        if (isRemandedReevaluation)
        {
            // Enforce remand deadline
            if (appraisal.AppealRemandDeadline.HasValue && DateTime.UtcNow > appraisal.AppealRemandDeadline.Value)
            {
                throw new InvalidOperationException(
                    $"The re-evaluation deadline ({appraisal.AppealRemandDeadline.Value:yyyy-MM-dd HH:mm}) has passed. " +
                    "Please contact HR for further guidance.");
            }
            
            _logger.LogInformation(
                "Manager re-evaluating remanded appeal for appraisal {AppraisalId}. Deadline: {Deadline}",
                appraisal.Id,
                appraisal.AppealRemandDeadline);
        }

        // Get or create manager evaluator evaluation
        var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

        if (managerEvaluation == null)
        {
            managerEvaluation = new EvaluatorEvaluation
            {
                Id = Guid.NewGuid(),
                AppraisalId = appraisal.Id,
                EvaluatorId = saveDto.ManagerId,
                EvaluatorRole = EvaluatorRole.Manager,
                EvaluatorWeight = settings.ManagerEvaluationWeight,
                IsAuthoritative = settings.IsManagerAuthoritative,
                StartedDate = DateTime.UtcNow, // Set on first save (draft or submit)
                TenantId = appraisal.TenantId
            };
            await _evaluatorEvaluationRepository.AddAsync(managerEvaluation);
        }
        else if (managerEvaluation.SubmittedDate.HasValue)
        {
            // Block all writes (draft autosave included) once submitted. A legitimate re-evaluation
            // (appeal remand) first clears SubmittedDate, so this won't block that flow.
            return new ManagerEvaluationResultDto
            {
                Success = false,
                Message = "Manager evaluation has already been submitted and cannot be modified"
            };
        }
        else
        {
            // Set StartedDate if not already set (in case first save was missed)
            if (!managerEvaluation.StartedDate.HasValue)
            {
                managerEvaluation.StartedDate = DateTime.UtcNow;
            }
        }

        // Save criterion scores from ItemScores (covers both KPI and competency items)
        foreach (var itemInput in saveDto.ItemScores)
        {
            if (!itemInput.NumericScore.HasValue && !itemInput.ActualValue.HasValue)
                continue;

            var existingScore = await _criterionScoreRepository.GetQueryable()
                .FirstOrDefaultAsync(cs => cs.EvaluatorEvaluationId == managerEvaluation.Id
                                        && cs.TemplateItemId == itemInput.TemplateItemId,
                                      cancellationToken);

            if (existingScore != null)
            {
                existingScore.NumericScore = itemInput.NumericScore;
                existingScore.ActualValue  = itemInput.ActualValue;
                existingScore.Notes = itemInput.Notes;
                await CalculateAndSetWeightedScoreAsync(existingScore, managerEvaluation, cancellationToken);
                await _criterionScoreRepository.UpdateAsync(existingScore);
            }
            else
            {
                existingScore = new CriterionScore
                {
                    Id = Guid.NewGuid(),
                    EvaluatorEvaluationId = managerEvaluation.Id,
                    TemplateItemId = itemInput.TemplateItemId,
                    NumericScore = itemInput.NumericScore,
                    ActualValue  = itemInput.ActualValue,
                    Notes = itemInput.Notes,
                    TenantId = appraisal.TenantId
                };
                await _criterionScoreRepository.AddAsync(existingScore);
                await CalculateAndSetWeightedScoreAsync(existingScore, managerEvaluation, cancellationToken);
                await _criterionScoreRepository.UpdateAsync(existingScore);
            }
        }

        // Update overall notes and recommendation
        managerEvaluation.OverallNotes = saveDto.OverallNotes;
        managerEvaluation.Recommendation = saveDto.Recommendation;

        // Update Manager Final Assessment & Recommendations on PerformanceAppraisal entity
        // These fields are only editable during ManagerEvaluation status
        appraisal.OverallComments = saveDto.OverallComments;
        appraisal.StrengthsIdentified = saveDto.StrengthsIdentified;
        appraisal.AreasForImprovement = saveDto.AreasForImprovement;
        appraisal.TrainingNeeds = saveDto.TrainingNeeds;
        appraisal.CareerAspirations = saveDto.CareerAspirations;
        appraisal.RecommendPromotion = saveDto.RecommendPromotion;
        appraisal.RecommendIncrement = saveDto.RecommendIncrement;
        appraisal.RecommendTraining = saveDto.RecommendTraining;
        appraisal.RecommendPIP = saveDto.RecommendPIP;
        appraisal.RecommendTermination = saveDto.RecommendTermination;
        appraisal.RecommendationNotes = saveDto.RecommendationNotes;

        // If submitting (not draft), set submitted date and calculate TotalScore
        if (!saveDto.IsDraft)
        {
            managerEvaluation.SubmittedDate = DateTime.UtcNow;
            
            // Calculate and set TotalScore only when submitting (not draft)
            var evalWithScores = await _evaluatorEvaluationRepository.GetQueryable()
                .Include(e => e.CriterionScores)
                .FirstOrDefaultAsync(e => e.Id == managerEvaluation.Id, cancellationToken);
            
            if (evalWithScores != null && evalWithScores.CriterionScores.Any())
            {
                managerEvaluation.TotalScore = evalWithScores.CriterionScores.Sum(cs => cs.WeightedScore);
            }

            // Handle remanded appeal re-evaluation completion
            if (isRemandedReevaluation)
            {
                // Recalculate overall score with updated manager evaluation
                await CalculateOverallScoreAsync(appraisal.Id, cancellationToken);
                
                // Clear remand fields and progress to HR Review
                appraisal.AppealRemandDeadline = null;
                appraisal.Status = AppraisalStatus.Governance;
                
                _logger.LogInformation(
                    "Manager completed remanded re-evaluation for appraisal {AppraisalId}. Returned to HR/Governance.",
                    appraisal.Id);
            }
            else
            {
                // Standard flow: Update appraisal status when manager evaluation is submitted
                var newStatus = await DetermineNextStatusAsync(appraisal, appraisal.Status, EvaluatorRole.Manager, cancellationToken);
                if (newStatus != appraisal.Status)
                {
                    appraisal.Status = newStatus;
                }
            }
            
            await _appraisalRepository.UpdateAsync(appraisal);
        }
        else
        {
            // Update status when saving as draft (from previous status to ManagerEvaluation)
            var draftStatus = UpdateStatusOnDraft(appraisal.Status, EvaluatorRole.Manager, settings);
            if (draftStatus != appraisal.Status)
            {
                appraisal.Status = draftStatus;
                await _appraisalRepository.UpdateAsync(appraisal);
            }
        }

        await _evaluatorEvaluationRepository.UpdateAsync(managerEvaluation);

        // Upsert goal appraisal assessments (manager-side fields)
        if (saveDto.GoalAssessments != null && saveDto.GoalAssessments.Count > 0)
        {
            var goalLookup = appraisal.Goals?.ToDictionary(g => g.Id) ?? new Dictionary<Guid, EmployeeGoal>();

            foreach (var ga in saveDto.GoalAssessments)
            {
                // Derive FinalProgressPercent server-side for KPI goals
                decimal? computedProgress = ga.FinalProgressPercent;
                if (goalLookup.TryGetValue(ga.GoalId, out var goal)
                    && goal.KpiDefinitionId.HasValue
                    && goal.MeasurementType != MeasurementType.Boolean
                    && ga.FinalActualValue.HasValue)
                {
                    computedProgress = CalculateKpiAchievement(
                        ga.FinalActualValue.Value, goal.TargetValue, goal.MinValue, goal.MaxValue);
                }

                var existing = await _goalAssessmentRepository.GetQueryable()
                    .FirstOrDefaultAsync(a => a.EmployeeGoalId == ga.GoalId
                                           && a.PerformanceAppraisalId == appraisal.Id,
                                       cancellationToken);

                if (existing == null)
                {
                    await _goalAssessmentRepository.AddAsync(new EmployeeGoalAppraisalAssessment
                    {
                        EmployeeGoalId             = ga.GoalId,
                        PerformanceAppraisalId     = appraisal.Id,
                        TenantId                   = appraisal.TenantId,
                        ManagerFinalProgressPercent = computedProgress,
                        ManagerFinalStatus         = ga.FinalStatus,
                        ManagerFinalActualValue    = ga.FinalActualValue,
                        ManagerAssessmentNotes     = ga.AssessmentNotes,
                        ManagerEvidenceLinks       = ga.EvidenceLinks
                    });
                }
                else
                {
                    existing.ManagerFinalProgressPercent = computedProgress;
                    existing.ManagerFinalStatus         = ga.FinalStatus;
                    existing.ManagerFinalActualValue    = ga.FinalActualValue;
                    existing.ManagerAssessmentNotes     = ga.AssessmentNotes;
                    existing.ManagerEvidenceLinks       = ga.EvidenceLinks;
                    await _goalAssessmentRepository.UpdateAsync(existing);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manager evaluation saved for appraisal {AppraisalId} by manager {ManagerId}. IsDraft: {IsDraft}", 
            saveDto.AppraisalId, saveDto.ManagerId, saveDto.IsDraft);

        return new ManagerEvaluationResultDto
        {
            Success = true,
            Message = saveDto.IsDraft ? "Draft saved successfully" : "Manager evaluation submitted successfully",
            EvaluatorEvaluationId = managerEvaluation.Id,
            SubmittedDate = managerEvaluation.SubmittedDate
        };
    }

    /// <summary>
    /// Gets detailed peer evaluations for manager to review
    /// </summary>
    public async Task<ManagerPeerEvaluationReviewDto> GetManagerPeerEvaluationReviewAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c!.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer))
                .ThenInclude(e => e.Evaluator)
                    .ThenInclude(emp => emp.Position)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer))
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        var isAnonymous = settings?.PeerReviewsAnonymous ?? false;
        var allowKpiEvaluation = settings?.AllowPeerKpiEvaluation ?? false;

        var peerEvaluationsList = appraisal.EvaluatorEvaluations
            .Where(e => e.EvaluatorRole == EvaluatorRole.Peer)
            .ToList();

        var peerDetails = new List<PeerEvaluatorDetailDto>();

        foreach (var peerEval in peerEvaluationsList)
        {
            var competencyScores = peerEval.CriterionScores
                .Where(cs => cs.TemplateItem?.CompetencyId != null)
                .Select(cs => new PeerCompetencyScoreDto
                {
                    CriterionScoreId = cs.Id,
                    CriteriaName = cs.TemplateItem?.Competency?.CriteriaName ?? string.Empty,
                    CriteriaDescription = cs.TemplateItem?.Competency?.Description,
                    Weight = 0, // Weight is stored in PositionCriteriaMapping, not in AppraisalCompetency
                    NumericScore = cs.NumericScore ?? 0,
                    WeightedScore = cs.WeightedScore,
                    Comments = cs.Notes,
                    AchievedGrade = null // Grade calculation would require additional logic
                })
                .ToList();

            var kpiEvaluations = new List<PeerKpiEvaluationDto>();
            // KPI peer evaluations are deprecated (EmployeeKpiTarget removed)

            peerDetails.Add(new PeerEvaluatorDetailDto
            {
                EvaluationId = peerEval.Id,
                EvaluatorId = peerEval.EvaluatorId,
                EvaluatorName = $"{peerEval.Evaluator.FirstName} {peerEval.Evaluator.LastName}", // Always show actual name to managers
                EvaluatorEmployeeNumber = peerEval.Evaluator.EmployeeNumber, // Always show to managers
                EvaluatorPosition = peerEval.Evaluator.Position?.Title, // Always show to managers
                IsSubmitted = peerEval.SubmittedDate.HasValue,
                SubmittedDate = peerEval.SubmittedDate,
                TotalScore = peerEval.TotalScore,
                CompetencyScores = competencyScores,
                KpiEvaluations = kpiEvaluations
            });
        }

        return new ManagerPeerEvaluationReviewDto
        {
            AppraisalId = appraisalId,
            IsAnonymous = isAnonymous,
            AllowKpiEvaluation = allowKpiEvaluation,
            TotalPeerEvaluators = peerEvaluationsList.Count,
            SubmittedEvaluations = peerEvaluationsList.Count(e => e.SubmittedDate.HasValue),
            PeerEvaluations = peerDetails
        };
    }

    #endregion
    
    #region Employee Actions
    
    /// <summary>
    /// Employee acknowledges receipt of finalized appraisal
    /// </summary>
    public async Task AcknowledgeAppraisalAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetByIdAsync(appraisalId);
        
        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");
        
        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only acknowledge your own appraisal.");
        
        if (appraisal.Status != AppraisalStatus.Governance)
            throw new InvalidOperationException("Appraisal must be in Governance status (finalized by HR) before it can be acknowledged.");
        
        if (appraisal.EmployeeAcknowledgedDate.HasValue)
            throw new InvalidOperationException("Appraisal has already been acknowledged.");
        
        appraisal.EmployeeAcknowledgedDate = DateTime.UtcNow;
        appraisal.EmployeeAcknowledged = true;
        appraisal.Status = AppraisalStatus.Completed; // Governance → Completed after acknowledgment
        
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
    
    /// <summary>
    /// Get appeal page data for an employee
    /// </summary>
    public async Task<AppealPageDataDto> GetAppealPageDataAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Load appraisal with evaluations and scores
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
            .Include(a => a.CriterionConfigs)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
                        .ThenInclude(ti => ti.Competency)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);
        
        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");
        
        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only appeal your own appraisal.");
        
        // Check if can appeal
        bool canAppeal = true;
        string? cannotAppealReason = null;
        
        // Employee can appeal when status is Completed (acknowledged, no appeal yet)
        if (appraisal.Status != AppraisalStatus.Completed)
        {
            canAppeal = false;
            cannotAppealReason = "You can only file an appeal after the appraisal has been completed and acknowledged.";
        }
        else if (appraisal.HasAppeal)
        {
            canAppeal = false;
            cannotAppealReason = "An appeal has already been filed for this appraisal.";
        }
        
        // Get manager evaluation (final scores)
        var managerEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        
        var appealableKpis = new List<AppealableKpiDto>();
        var appealableCompetencies = new List<AppealableCompetencyDto>();
        
        if (managerEval != null)
        {
            // KPI appeals are deprecated - EmployeeKpiTarget has been replaced by EmployeeGoal
            // appealableKpis remains empty

            // Get Competency scores
            var competencyScores = managerEval.CriterionScores
                .Where(cs => cs.TemplateItem?.CompetencyId != null)
                .ToList();
            
            foreach (var score in competencyScores)
            {
                // Weight needs to come from the weighted score calculation, not directly from criteria
                // Use 0 as placeholder since weight is in PositionCriteriaMapping
                appealableCompetencies.Add(new AppealableCompetencyDto
                {
                    TemplateItemId = score.TemplateItemId,
                    ItemName = score.TemplateItem?.Competency?.CriteriaName ?? string.Empty,
                    Description = score.TemplateItem?.Competency?.Description,
                    NumericScore = score.NumericScore,
                    Weight = appraisal.CriterionConfigs
                                .FirstOrDefault(cc => cc.TemplateItemId == score.TemplateItemId)?.WeightUsed ?? 0,
                    WeightedScore = score.WeightedScore
                });
            }
        }
        
        return new AppealPageDataDto
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            FinalScore = appraisal.OverallScore,
            FinalGrade = null, // Not stored in entity
            CanAppeal = canAppeal,
            CannotAppealReason = cannotAppealReason,
            AppealableKpis = appealableKpis,
            AppealableCompetencies = appealableCompetencies
        };
    }
    
    /// <summary>
    /// Submit an appeal
    /// </summary>
    public async Task<AppraisalAppealDto> SubmitAppealAsync(SubmitAppealDto submitDto, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetByIdAsync(submitDto.AppraisalId);
        
        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{submitDto.AppraisalId}' not found.");
        
        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only appeal your own appraisal.");
        
        if (appraisal.Status != AppraisalStatus.Completed)
            throw new InvalidOperationException("You can only file an appeal after the appraisal has been completed and acknowledged.");
        
        if (appraisal.HasAppeal)
            throw new InvalidOperationException("An appeal has already been filed for this appraisal.");
        
        if (submitDto.AppealedItems == null || !submitDto.AppealedItems.Any())
            throw new ArgumentException("At least one item must be appealed.");
        
        // Create appeal
        var appeal = new AppraisalAppeal
        {
            Id = Guid.NewGuid(),
            TenantId = appraisal.TenantId,
            PerformanceAppraisalId = appraisal.Id,
            EmployeeId = employeeId,
            AppealReason = submitDto.OverallReason ?? "Appeal submitted",
            Status = AppraisalAppealStatus.Submitted,
            SubmittedDate = DateTime.UtcNow,
            CreatedBy = employeeId.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        
        // Create appeal items
        var appealItems = new List<AppraisalAppealItem>();
        foreach (var item in submitDto.AppealedItems)
        {
            appealItems.Add(new AppraisalAppealItem
            {
                Id = Guid.NewGuid(),
                TenantId = appraisal.TenantId,
                AppraisalAppealId = appeal.Id,
                TemplateItemId = item.TemplateItemId,
                Reason = item.Reason,
                CreatedBy = employeeId.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }
        
        // Update appraisal - set status to Appealed when appeal is filed
        appraisal.HasAppeal = true;
        appraisal.CurrentAppealStatus = AppraisalAppealStatus.Submitted;
        appraisal.Status = AppraisalStatus.Appealed;
        
        // Save to database
        await _appealRepository.AddAsync(appeal);
        foreach (var item in appealItems)
        {
            await _appealItemRepository.AddAsync(item);
        }
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Appeal submitted for appraisal {AppraisalId} with {ItemCount} items", appraisal.Id, appealItems.Count);
        
        // Return DTO
        return new AppraisalAppealDto
        {
            Id = appeal.Id,
            TenantId = appeal.TenantId,
            PerformanceAppraisalId = appeal.PerformanceAppraisalId,
            AppealReason = appeal.AppealReason,
            Status = appeal.Status,
            SubmittedDate = appeal.SubmittedDate,
            Items = appealItems.Select(ai => new AppraisalAppealItemDto
            {
                Id = ai.Id,
                TenantId = ai.TenantId,
                AppraisalAppealId = ai.AppraisalAppealId,
                TemplateItemId = ai.TemplateItemId,
                Reason = ai.Reason
            }).ToList()
        };
    }
    
    /// <summary>
    /// Get appeal status for viewing (read-only)
    /// </summary>
    public async Task<AppealStatusViewDto> GetAppealStatusAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);
        
        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");
        
        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only view your own appeal.");
        
        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal has been filed for this appraisal.");
        
        // Load the appeal with related data
        var appeal = await _appealRepository.GetQueryable()
            .Include(a => a.Items)
            .Include(a => a.Reviewer)
            .FirstOrDefaultAsync(a => a.PerformanceAppraisalId == appraisalId, cancellationToken);
        
        if (appeal == null)
            throw new InvalidOperationException("Appeal not found.");
        
        // Load appealed items details
        var appealedItemsView = new List<AppealedItemViewDto>();
        
        foreach (var item in appeal.Items)
        {
            if (item.TemplateItemId.HasValue)
            {
                // It's a competency
                var competency = await _appraisalCompetencyRepository.GetByIdAsync(
                    (await _criterionConfigRepository.GetQueryable()
                        .Where(cc => cc.TemplateItemId == item.TemplateItemId.Value)
                        .Select(cc => cc.TemplateItem.CompetencyId)
                        .FirstOrDefaultAsync(cancellationToken)) ?? Guid.Empty);

                // Get the criterion score
                var score = await _criterionScoreRepository.GetQueryable()
                    .Include(cs => cs.EvaluatorEvaluation)
                    .FirstOrDefaultAsync(cs => 
                        cs.TemplateItemId == item.TemplateItemId.Value && 
                        cs.EvaluatorEvaluation.AppraisalId == appraisalId &&
                        cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager, 
                        cancellationToken);

                appealedItemsView.Add(new AppealedItemViewDto
                {
                    ItemId = item.Id,
                    ItemType = "Competency",
                    ItemName = competency?.CriteriaName ?? item.TemplateItem?.Competency?.CriteriaName ?? "Item",
                    Reason = item.Reason,
                    OriginalScore = score?.NumericScore,
                    TargetValue = null,
                    ActualValue = null
                });
            }
        }
        
        return new AppealStatusViewDto
        {
            AppealId = appeal.Id,
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            Status = appeal.Status,
            SubmittedDate = appeal.SubmittedDate,
            AppealReason = appeal.AppealReason,
            ReviewedByName = appeal.Reviewer?.FirstName != null ? $"{appeal.Reviewer.FirstName} {appeal.Reviewer.LastName}" : null,
            ResolvedDate = appeal.ResolvedDate,
            ResolutionNotes = appeal.ResolutionNotes,
            OriginalScore = appraisal.OverallScore,
            AdjustedScore = appraisal.AdjustedScore ?? appraisal.OverallScore,
            AppealedItems = appealedItemsView
        };
    }
    
    /// <summary>
    /// Get list of all appeals for HR/Manager review
    /// </summary>
    public async Task<List<AppealListItemDto>> GetAppealsListAsync(Guid? cycleId = null, AppraisalAppealStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _appealRepository.GetQueryable()
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(pa => pa.Employee)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(pa => pa.AppraisalCycle)
            .Include(a => a.Items)
            .AsQueryable();
        
        // Filter by cycle if specified
        if (cycleId.HasValue)
        {
            query = query.Where(a => a.PerformanceAppraisal.AppraisalCycleId == cycleId.Value);
        }
        
        // Filter by status if specified
        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }
        
        var appeals = await query
            .OrderByDescending(a => a.SubmittedDate)
            .ToListAsync(cancellationToken);
        
        return appeals.Select(a => new AppealListItemDto
        {
            AppealId = a.Id,
            AppraisalId = a.PerformanceAppraisalId,
            AppraisalNumber = a.PerformanceAppraisal.AppraisalNumber,
            EmployeeName = $"{a.PerformanceAppraisal.Employee.FirstName} {a.PerformanceAppraisal.Employee.LastName}",
            EmployeeNumber = a.PerformanceAppraisal.Employee.EmployeeNumber ?? "",
            CycleName = a.PerformanceAppraisal.AppraisalCycle?.CycleName ?? "",
            CycleId = a.PerformanceAppraisal.AppraisalCycleId,
            SubmittedDate = a.SubmittedDate,
            AppealedItemsCount = a.Items.Count,
            Status = a.Status
        }).ToList();
    }

    /// <summary>
    /// Get comprehensive appeal review data for HR resolution page
    /// </summary>
    public async Task<AppealReviewDto> GetAppealReviewDataAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        // Load appraisal with basic info
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(pa => pa.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(pa => pa.AppraisalCycle)
                .ThenInclude(ac => ac!.AppraisalSettings)
            .FirstOrDefaultAsync(pa => pa.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal exists for this appraisal");

        // Load the appeal
        var appeal = await _appealRepository.GetQueryable()
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.PerformanceAppraisalId == appraisalId, cancellationToken);

        if (appeal == null)
            throw new InvalidOperationException("Appeal not found");

        if (appeal.Status == AppraisalAppealStatus.Upheld || appeal.Status == AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("This appeal has already been resolved");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal settings not found");

        // Get employee position
        var employee = await _employeeRepository.GetQueryable()
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .FirstOrDefaultAsync(e => e.Id == appraisal.EmployeeId, cancellationToken);

        var positionTitle = employee?.Position?.Title;

        // Load evaluator evaluations to get individual scores
        var evaluations = await _evaluatorEvaluationRepository.GetQueryable()
            .Where(e => e.AppraisalId == appraisalId && e.SubmittedDate.HasValue)
            .ToListAsync(cancellationToken);

        var selfEval = evaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
        var managerEval = evaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        var peerEvals = evaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer).ToList();

        // Build DTO
        var dto = new AppealReviewDto
        {
            AppealId = appeal.Id,
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            SubmittedDate = appeal.SubmittedDate,
            Status = appeal.Status,
            OverallAppealReason = appeal.AppealReason,
            
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeeNumber = appraisal.Employee.EmployeeNumber ?? "",
            PositionTitle = positionTitle,
            OrganizationUnitName = appraisal.Employee.OrganizationUnit?.Name,
            
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            CycleId = appraisal.AppraisalCycleId,
            CycleStartDate = appraisal.AppraisalCycle?.StartDate ?? default,
            CycleEndDate = appraisal.AppraisalCycle?.EndDate ?? default,
            
            SelfEvaluationScore = selfEval?.TotalScore,
            PeerEvaluationScore = peerEvals.Any() ? peerEvals.Average(p => p.TotalScore) : null,
            ManagerEvaluationScore = managerEval?.TotalScore,
            OverallScore = appraisal.OverallScore ?? 0,
            
            HRCanModifyScores = settings.HRCanModifyScores,
            AppraisalSettingsId = settings.Id,
            AppraisalSettingsName = settings.SettingsName
        };

        // Load appealed items details
        foreach (var item in appeal.Items)
        {
            if (item.TemplateItemId.HasValue)
            {
                // Load criteria details
                var scores = await _criterionScoreRepository.GetQueryable()
                    .Include(cs => cs.EvaluatorEvaluation)
                    .Where(cs => cs.TemplateItemId == item.TemplateItemId.Value && cs.EvaluatorEvaluation.AppraisalId == appraisalId)
                    .ToListAsync(cancellationToken);

                var managerScore = scores.FirstOrDefault(s => s.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager);
                var selfScore = scores.FirstOrDefault(s => s.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Self);

                var criterionDto = new AppealedCriterionReviewDto
                {
                    AppealItemId = item.Id,
                    TemplateItemId = item.TemplateItemId.Value,
                    ItemName = item.TemplateItem?.Competency?.CriteriaName ?? item.TemplateItem?.KpiDefinition?.KpiName ?? string.Empty,
                    ItemDescription = item.TemplateItem?.Competency?.Description ?? string.Empty,
                    Weight = 0, // Weight already reflected in WeightedScore
                    AppealReason = item.Reason,
                    SelfScore = selfScore?.NumericScore,
                    ManagerScore = managerScore?.NumericScore,
                    SelfWeightedScore = selfScore?.WeightedScore,
                    ManagerWeightedScore = managerScore?.WeightedScore,
                    ManagerComments = managerScore?.Notes,
                    SelfComments = selfScore?.Notes
                };
                
                dto.AppealedCriteria.Add(criterionDto);
            }
        }

        return dto;
    }

    /// <summary>
    /// Resolve an appraisal appeal with optional score modifications
    /// </summary>
    public async Task ResolveAppealAsync(Guid appraisalId, ResolveAppealDto resolveDto, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(pa => pa.AppraisalCycle)
                .ThenInclude(ac => ac!.AppraisalSettings)
            .FirstOrDefaultAsync(pa => pa.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal exists for this appraisal");

        var appeal = await _appealRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.PerformanceAppraisalId == appraisalId, cancellationToken);

        if (appeal == null)
            throw new InvalidOperationException("Appeal not found");

        if (appeal.Status == AppraisalAppealStatus.Upheld || appeal.Status == AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("This appeal has already been resolved");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal settings not found");

        // Validate score modifications if provided
        if (resolveDto.CriteriaModifications?.Any() ?? false)
        {
            if (!settings.HRCanModifyScores)
                throw new InvalidOperationException("Score modifications are not allowed by appraisal policy");
        }

        // Apply score modifications if allowed and provided
        if (settings.HRCanModifyScores)
        {
            if (resolveDto.CriteriaModifications != null)
            {
                foreach (var mod in resolveDto.CriteriaModifications)
                {
                    var scores = await _criterionScoreRepository.GetQueryable()
                        .Include(cs => cs.EvaluatorEvaluation)
                        .Where(cs => cs.TemplateItemId == mod.TemplateItemId && 
                                   cs.EvaluatorEvaluation.AppraisalId == appraisalId &&
                                   cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (scores != null)
                    {
                        scores.NumericScore = mod.NewScore;
                        scores.Notes = (scores.Notes ?? "") + 
                            $"\n\n[HR Appeal Resolution - {DateTime.UtcNow:yyyy-MM-dd}]: {mod.Justification}";
                        await _criterionScoreRepository.UpdateAsync(scores);
                    }
                }
            }

            // KPI score modifications are deprecated (EmployeeKpiTarget removed)

            // Recalculate overall score
            await CalculateOverallScoreAsync(appraisalId, cancellationToken);
        }

        // Update appeal status and resolution details
        appeal.Status = resolveDto.ResolutionDecision;
        appeal.ReviewedById = reviewerId;
        appeal.ResolvedDate = DateTime.UtcNow;
        appeal.ResolutionNotes = resolveDto.ResolutionNotes;
        
        await _appealRepository.UpdateAsync(appeal);

        // Update appraisal status based on resolution decision
        switch (resolveDto.ResolutionDecision)
        {
            case AppraisalAppealStatus.Remanded:
                // CRITICAL: Create immutable snapshot before remanding
                // This preserves the original manager evaluation state for before/after comparison
                try
                {
                    var snapshotId = await CreateManagerEvaluationSnapshotAsync(
                        appraisalId, 
                        "Appeal Remand - Pre-Reevaluation Snapshot",
                        cancellationToken);
                    
                    _logger.LogInformation(
                        "Created evaluation snapshot {SnapshotId} before remanding appraisal {AppraisalId}",
                        snapshotId,
                        appraisalId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to create snapshot before remanding appraisal {AppraisalId}. Remand operation aborted.",
                        appraisalId);
                    throw new InvalidOperationException(
                        "Cannot remand appeal: Failed to create evaluation snapshot. " +
                        "The original evaluation state must be preserved before remanding.", ex);
                }
                
                // Roll back to Active so manager can re-evaluate
                appraisal.Status = AppraisalStatus.Active;
                appraisal.CurrentAppealStatus = AppraisalAppealStatus.Remanded;
                
                // Set remand tracking fields
                appraisal.AppealRemandedDate = DateTime.UtcNow;
                appraisal.AppealRemandDeadline = DateTime.UtcNow.AddDays(settings.AppealReevaluationWindowDays);
                
                _logger.LogInformation(
                    "Appeal remanded for appraisal {AppraisalId}. Manager must re-evaluate by {Deadline}",
                    appraisalId, 
                    appraisal.AppealRemandDeadline);
                break;

            case AppraisalAppealStatus.Upheld:
                // Appeal upheld - employee's concerns validated (any score adjustments already applied above)
                // Final state: no further modifications allowed
                appraisal.CurrentAppealStatus = AppraisalAppealStatus.Upheld;
                appraisal.HasAppeal = false;
                appraisal.AppealRemandedDate = null;
                appraisal.AppealRemandDeadline = null;
                break;

            case AppraisalAppealStatus.Rejected:
                // Appeal rejected - employee's appeal not valid, current scores confirmed correct
                // Final state: no further modifications allowed
                appraisal.CurrentAppealStatus = AppraisalAppealStatus.Rejected;
                appraisal.HasAppeal = false;
                appraisal.AppealRemandedDate = null;
                appraisal.AppealRemandDeadline = null;
                break;
        }

        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
    
    /// <summary>
    /// Gets comprehensive post-remand review data for HR final decision
    /// Compares pre-remand (snapshot) vs post-remand (current) manager evaluation
    /// </summary>
    public async Task<PostRemandReviewDto> GetPostRemandReviewDataAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
            .Include(a => a.Appeals)
                .ThenInclude(ap => ap.Items)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException("Appraisal not found");

        if (appraisal.CurrentAppealStatus != AppraisalAppealStatus.Remanded)
            throw new InvalidOperationException("Appraisal is not in remanded status");

        var latestAppeal = appraisal.Appeals
            .OrderByDescending(a => a.SubmittedDate)
            .FirstOrDefault();

        if (latestAppeal == null)
            throw new InvalidOperationException("No appeal found for this appraisal");

        // Get the evaluation snapshot (pre-remand state)
        var snapshot = await _evaluationSnapshotRepository.GetQueryable()
            .Include(s => s.CriterionScores)
                .ThenInclude(cs => cs.KpiSnapshots)
            .Where(s => s.AppraisalId == appraisalId && s.SnapshotReason == "Appeal Remand")
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot == null)
            throw new InvalidOperationException("Pre-remand snapshot not found");

        // Get current manager evaluation (post-remand state)
        var currentManagerEval = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentManagerEval == null || !currentManagerEval.SubmittedDate.HasValue)
            throw new InvalidOperationException("Manager has not completed post-remand re-evaluation");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;

        // Get template item weights as dictionary: templateItemId -> weight
        var positionMappings = (await _templateItemRepository.GetQueryable()
                .Include(i => i.Section)
                .Where(i => i.Section.AppraisalTemplateId == appraisal.AppraisalTemplateId)
                .ToListAsync(cancellationToken))
                .ToDictionary(i => i.Id, i => i.Weight);

        var result = new PostRemandReviewDto
        {
            AppraisalId = appraisalId,
            AppraisalNumber = appraisal.AppraisalNumber,
            AppealId = latestAppeal.Id,
            AppealStatus = latestAppeal.Status,
            
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = appraisal.Employee.FullName,
            EmployeeNumber = appraisal.Employee.EmployeeNumber,
            PositionTitle = appraisal.Employee.Position?.Title,
            OrganizationUnitName = appraisal.Employee.OrganizationUnit?.Name,
            
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            CycleId = appraisal.AppraisalCycleId,
            CycleStartDate = appraisal.AppraisalCycle?.StartDate ?? default,
            CycleEndDate = appraisal.AppraisalCycle?.EndDate ?? default,
            
            AppealSubmittedDate = latestAppeal.SubmittedDate,
            AppealRemandedDate = appraisal.AppealRemandedDate ?? DateTime.UtcNow,
            AppealRemandDeadline = appraisal.AppealRemandDeadline ?? DateTime.UtcNow,
            ManagerReevaluationDate = currentManagerEval.SubmittedDate,
            
            OverallAppealReason = latestAppeal.AppealReason,
            HRRemandJustification = latestAppeal.ResolutionNotes ?? "",
            
            PreRemandOverallScore = snapshot.TotalScore ?? 0,
            PostRemandOverallScore = appraisal.OverallScore ?? 0,
            
            HRCanModifyScores = settings?.HRCanModifyScores ?? false
        };

        // Build criterion comparisons
        var appealedTemplateItemIds = latestAppeal.Items
            .Where(i => i.TemplateItemId.HasValue)
            .Select(i => i.TemplateItemId!.Value)
            .ToHashSet();

        foreach (var currentScore in currentManagerEval.CriterionScores)
        {
            var templateItemId = currentScore.TemplateItemId;
            var competency = currentScore.TemplateItem?.Competency;

            var snapshotScore = snapshot?.CriterionScores
                .FirstOrDefault(s => s.TemplateItemId == templateItemId);

            var appealItem = latestAppeal.Items
                .FirstOrDefault(i => i.TemplateItemId == templateItemId);

            result.CriteriaComparisons.Add(new CriterionScoreComparisonDto
            {
                TemplateItemId = templateItemId,
                ItemName = competency?.CriteriaName ?? currentScore.TemplateItem?.KpiDefinition?.KpiName ?? string.Empty,
                ItemDescription = competency?.Description ?? string.Empty,
                Weight = positionMappings.GetValueOrDefault(templateItemId, 0),
                WasAppealed = appealedTemplateItemIds.Contains(templateItemId),
                AppealReason = appealItem?.Reason,
                
                PreRemandScore = snapshotScore?.NumericScore,
                PreRemandWeightedScore = snapshotScore?.WeightedScore,
                PreRemandComments = snapshotScore?.Notes,
                
                PostRemandScore = currentScore.NumericScore,
                PostRemandWeightedScore = currentScore.WeightedScore,
                PostRemandComments = currentScore.Notes
            });
        }

        // KPI comparisons are deprecated (EmployeeKpiTarget removed)

        return result;
    }

    /// <summary>
    /// Finalizes post-remand appeal with HR's final decision (Upheld or Rejected)
    /// This locks scores and closes the appeal lifecycle
    /// </summary>
    public async Task FinalizePostRemandAppealAsync(
        Guid appraisalId, 
        PostRemandFinalDecisionDto decisionDto, 
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Appeals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException("Appraisal not found");

        if (appraisal.CurrentAppealStatus != AppraisalAppealStatus.Remanded)
            throw new InvalidOperationException("Appraisal is not in remanded status");

        if (decisionDto.FinalDecision != AppraisalAppealStatus.Upheld && 
            decisionDto.FinalDecision != AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("Final decision must be either Upheld or Rejected");

        var latestAppeal = appraisal.Appeals
            .OrderByDescending(a => a.SubmittedDate)
            .FirstOrDefault();

        if (latestAppeal == null)
            throw new InvalidOperationException("No appeal found");

        // Verify manager has completed re-evaluation
        var managerEval = await _evaluatorEvaluationRepository.GetQueryable()
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);

        if (managerEval == null || !managerEval.SubmittedDate.HasValue)
            throw new InvalidOperationException("Manager has not completed post-remand re-evaluation");

        // Update appeal status
        latestAppeal.Status = decisionDto.FinalDecision;
        latestAppeal.ResolutionNotes = decisionDto.HRFinalNotes;
        latestAppeal.ReviewedById = reviewerId;
        latestAppeal.ResolvedDate = DateTime.UtcNow;

        // Update appraisal status
        appraisal.CurrentAppealStatus = decisionDto.FinalDecision;
        appraisal.HasAppeal = false;
        appraisal.AppealRemandedDate = null;
        appraisal.AppealRemandDeadline = null;

        // If upheld, post-remand scores are already in place (manager re-evaluated)
        // If rejected, we keep current scores (which are the post-remand scores anyway)
        // In both cases, we're confirming the current state as final
        
        await _appealRepository.UpdateAsync(latestAppeal);
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Post-remand appeal finalized with decision {Decision} for appraisal {AppraisalId}",
            decisionDto.FinalDecision,
            appraisalId);
    }
    
    /// <summary>
    /// Gets employee read-only view of final appeal outcome after HR decision
    /// Shows final scores, HR notes, and outcome message
    /// </summary>
    public async Task<EmployeeAppealOutcomeDto> GetEmployeeAppealOutcomeAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
            .Include(a => a.Appeals)
                .ThenInclude(ap => ap.Items)
                    .ThenInclude(i => i.TemplateItem)
                        .ThenInclude(ti => ti.Competency)
            .Include(a => a.Appeals)
                .ThenInclude(ap => ap.Items)
                    .ThenInclude(i => i.TemplateItem)
                        .ThenInclude(ti => ti.KpiDefinition)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException("Appraisal not found");

        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only view your own appeal outcomes");

        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal exists for this appraisal");

        var latestAppeal = appraisal.Appeals
            .OrderByDescending(a => a.SubmittedDate)
            .FirstOrDefault();

        if (latestAppeal == null)
            throw new InvalidOperationException("Appeal not found");

        if (latestAppeal.Status != AppraisalAppealStatus.Upheld && latestAppeal.Status != AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("Appeal outcome is not yet final");

        // Get snapshot to determine if scores changed
        var snapshot = await _evaluationSnapshotRepository.GetQueryable()
            .Include(s => s.CriterionScores)
            .Where(s => s.AppraisalId == appraisalId && s.SnapshotReason == "Appeal Remand")
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);

        // Get final manager evaluation
        var managerEval = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);

        if (managerEval == null)
            throw new InvalidOperationException("Manager evaluation not found");

        // Get template item weights as dictionary: templateItemId -> weight
        var positionMappings = (await _templateItemRepository.GetQueryable()
                .Include(i => i.Section)
                .Where(i => i.Section.AppraisalTemplateId == appraisal.AppraisalTemplateId)
                .ToListAsync(cancellationToken))
                .ToDictionary(i => i.Id, i => i.Weight);

        var result = new EmployeeAppealOutcomeDto
        {
            AppraisalId = appraisalId,
            AppraisalNumber = appraisal.AppraisalNumber,
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            CycleStartDate = appraisal.AppraisalCycle?.StartDate ?? default,
            CycleEndDate = appraisal.AppraisalCycle?.EndDate ?? default,
            PositionTitle = appraisal.Employee.Position?.Title ?? "",
            OrganizationUnitName = appraisal.Employee.OrganizationUnit?.Name ?? "",
            
            AppealStatus = latestAppeal.Status,
            AppealResolvedDate = latestAppeal.ResolvedDate ?? DateTime.UtcNow,
            
            AppealSubmittedDate = latestAppeal.SubmittedDate,
            EmployeeAppealReason = latestAppeal.AppealReason,
            
            HRFinalNotes = latestAppeal.ResolutionNotes ?? "",
            OutcomeMessage = latestAppeal.Status == AppraisalAppealStatus.Upheld
                ? "Your appeal was accepted. Your appraisal scores were adjusted after review."
                : "Your appeal was reviewed, but the original appraisal outcome stands.",
            
            FinalOverallScore = appraisal.OverallScore ?? 0,
            ScoresChangedAfterAppeal = snapshot != null && snapshot.TotalScore != appraisal.OverallScore
        };

        // Build appealed items list
        foreach (var item in latestAppeal.Items)
        {
            if (item.TemplateItemId.HasValue)
            {
                var itemName = item.TemplateItem?.Competency?.CriteriaName 
                    ?? item.TemplateItem?.KpiDefinition?.KpiName 
                    ?? "Unknown";
                result.AppealedItems.Add($"Criterion: {itemName}");
            }
        }

        // Build final criterion scores
        var appealedTemplateItemIds = latestAppeal.Items
            .Where(i => i.TemplateItemId.HasValue)
            .Select(i => i.TemplateItemId!.Value)
            .ToHashSet();

        foreach (var score in managerEval.CriterionScores)
        {
            var competency = score.TemplateItem?.Competency;
            result.FinalCriteriaScores.Add(new FinalCriterionScoreDto
            {
                TemplateItemId = score.TemplateItemId,
                ItemName = competency?.CriteriaName ?? score.TemplateItem?.KpiDefinition?.KpiName ?? string.Empty,
                ItemDescription = competency?.Description ?? string.Empty,
                FinalScore = score.NumericScore,
                FinalWeightedScore = score.WeightedScore,
                Weight = positionMappings.GetValueOrDefault(score.TemplateItemId, 0),
                ManagerComments = score.Notes ?? "",
                WasAppealed = appealedTemplateItemIds.Contains(score.TemplateItemId)
            });
        }

        // FinalKpiScores are deprecated (EmployeeKpiTarget removed); result.FinalKpiScores remains empty.

        return result;
    }
    
    #endregion

    #region HR Review Workflow

    /// <summary>
    /// Progresses an appraisal to HR review stage by creating an HR evaluator evaluation
    /// This method orchestrates the transition to HR review phase
    /// </summary>
    public async Task<bool> ProgressToHRReviewAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        // Get the appraisal with necessary related data
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal cycle settings not found.");

        // Check if HR review is required
        if (!settings.RequireHRReview)
        {
            _logger.LogInformation("HR review not required for appraisal {AppraisalId}", appraisalId);
            return false;
        }

        // Check if HR evaluation already exists
        var existingHREvaluation = appraisal.EvaluatorEvaluations
            .Any(e => e.EvaluatorRole == EvaluatorRole.HR);

        if (existingHREvaluation)
        {
            _logger.LogWarning("HR evaluation already exists for appraisal {AppraisalId}", appraisalId);
            return false;
        }

        // Validate that manager evaluation is complete (if required)
        if (settings.RequireManagerEvaluation)
        {
            var managerEvaluation = appraisal.EvaluatorEvaluations
                .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

            if (managerEvaluation == null || !managerEvaluation.SubmittedDate.HasValue)
            {
                throw new InvalidOperationException("Manager evaluation must be completed before HR review.");
            }
        }

        // Assign HR reviewer
        var hrReviewerId = await AssignHRReviewerAsync(appraisal, cancellationToken);

        if (hrReviewerId == Guid.Empty)
        {
            throw new InvalidOperationException("Unable to assign HR reviewer. No eligible HR personnel found.");
        }

        // Create the HR evaluation
        var createDto = new CreateEvaluatorEvaluationDto
        {
            AppraisalId = appraisalId,
            EvaluatorId = hrReviewerId,
            EvaluatorRole = EvaluatorRole.HR,
            EvaluatorWeight = 0.15m, // Default HR review weight (15%)
            OverallNotes = "HR Review - Automatically assigned"
        };

        await AddEvaluatorEvaluationAsync(appraisalId, createDto, cancellationToken);

        _logger.LogInformation("HR review successfully assigned for appraisal {AppraisalId} to HR reviewer {HRReviewerId}", 
            appraisalId, hrReviewerId);

        // TODO: Send notification to assigned HR reviewer
        // await _notificationService.NotifyHRReviewAssignedAsync(hrReviewerId, appraisalId);

        return true;
    }

    /// <summary>
    /// Resolves the HR reviewer for an appraisal deterministically:
    /// (1) the configured <see cref="AppraisalSettings.DefaultHRReviewerId"/> when it points to an active
    /// employee, otherwise (2) the least-loaded active HR employee (fewest open HR reviews), with a stable
    /// EmployeeNumber tiebreak. Returns <see cref="Guid.Empty"/> only when no eligible HR employee exists —
    /// the caller turns that into a clear error rather than silently skipping HR review.
    /// </summary>
    private async Task<Guid> AssignHRReviewerAsync(PerformanceAppraisal appraisal, CancellationToken cancellationToken)
    {
        // 1. Prefer an explicitly configured default HR reviewer (must be an active employee).
        var configuredId = appraisal.AppraisalCycle?.AppraisalSettings?.DefaultHRReviewerId;
        if (configuredId.HasValue && configuredId.Value != Guid.Empty)
        {
            var configured = await _employeeRepository.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == configuredId.Value && e.StaffStatus == StaffStatus.Active, cancellationToken);
            if (configured != null)
            {
                _logger.LogInformation("Assigned configured HR reviewer {EmployeeId} for appraisal {AppraisalId}", configured.Id, appraisal.Id);
                return configured.Id;
            }
            _logger.LogWarning("Configured DefaultHRReviewerId {Id} is not an active employee; falling back to load-based assignment.", configuredId.Value);
        }

        // 2. Otherwise pick the least-loaded active HR employee.
        var hrCandidates = await _employeeRepository.GetQueryable()
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .Where(e => e.StaffStatus == StaffStatus.Active)
            .Where(e =>
                (e.Position != null && (e.Position.Title.Contains("HR") || e.Position.Title.Contains("Human Resource"))) ||
                (e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("HR")))
            .Select(e => new { e.Id, e.EmployeeNumber })
            .ToListAsync(cancellationToken);

        if (hrCandidates.Count == 0)
        {
            _logger.LogWarning("No active HR employee found for appraisal {AppraisalId}. HR assignment failed.", appraisal.Id);
            return Guid.Empty;
        }

        var candidateIds = hrCandidates.Select(c => c.Id).ToList();
        var openLoads = await _evaluatorEvaluationRepository.GetQueryable()
            .Where(ev => ev.EvaluatorRole == EvaluatorRole.HR && ev.SubmittedDate == null && candidateIds.Contains(ev.EvaluatorId))
            .GroupBy(ev => ev.EvaluatorId)
            .Select(g => new { EvaluatorId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var loadById = openLoads.ToDictionary(x => x.EvaluatorId, x => x.Count);

        var chosen = hrCandidates
            .OrderBy(c => loadById.TryGetValue(c.Id, out var n) ? n : 0)
            .ThenBy(c => c.EmployeeNumber)
            .First();

        _logger.LogInformation("Assigned least-loaded HR reviewer {EmployeeId} for appraisal {AppraisalId}", chosen.Id, appraisal.Id);
        return chosen.Id;
    }

    /// <summary>
    /// Gets complete HR review context for an appraisal
    /// </summary>
    public async Task<HRReviewDto> GetHRReviewAsync(Guid appraisalId, Guid? requestingEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
                        .ThenInclude(ti => ti.Competency)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
                        .ThenInclude(ti => ti.KpiDefinition)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal cycle settings not found.");

        // Check if the requester is the appraisee and peer reviews are anonymous
        var isAppraiseeViewing = requestingEmployeeId.HasValue && requestingEmployeeId.Value == appraisal.EmployeeId;
        var shouldHidePeerDetails = isAppraiseeViewing && settings.PeerReviewsAnonymous;

        // Get evaluations
        var selfEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
        var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        var peerEvaluations = appraisal.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer).ToList();
        var hrEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.HR);

        // Check prerequisites
        var isSelfComplete = selfEvaluation?.SubmittedDate.HasValue ?? false;
        var isManagerComplete = managerEvaluation?.SubmittedDate.HasValue ?? false;
        var minPeerReviews = settings.MinPeerEvaluators;
        var completedPeerReviews = peerEvaluations.Count(e => e.SubmittedDate.HasValue);
        var arePeerReviewsComplete = completedPeerReviews >= minPeerReviews;
        var canProceed = isSelfComplete && isManagerComplete && arePeerReviewsComplete;

        // Build evaluation summaries
        var selfSummary = selfEvaluation != null ? BuildEvaluationSummary(selfEvaluation) : null;
        var managerSummary = managerEvaluation != null ? BuildEvaluationSummary(managerEvaluation) : null;
        
        // Build peer evaluation summary - HIDE details if anonymous and appraisee is viewing
        PeerEvaluationSummaryDto? peerSummary = null;
        if (peerEvaluations.Any() && !shouldHidePeerDetails)
        {
            var peerCompetencyScores = peerEvaluations
                .SelectMany(e => e.CriterionScores)
                .Where(cs => cs.TemplateItem?.CompetencyId != null)
                .GroupBy(cs => cs.TemplateItemId)
                .Select(g => new PeerCompetencyScoreSummaryDto
                {
                    CriteriaName = g.First().TemplateItem?.Competency?.CriteriaName ?? "Unknown",
                    AverageScore = (decimal?)g.Average(cs => cs.NumericScore),
                    ResponseCount = g.Count()
                })
                .ToList();

            // KPI items scored by peers (only present when AllowPeerKpiEvaluation) → aggregated KPI rows.
            var peerKpiScores = peerEvaluations
                .SelectMany(e => e.CriterionScores)
                .Where(cs => cs.TemplateItem?.KpiDefinitionId != null)
                .GroupBy(cs => cs.TemplateItemId)
                .Select(g => new PeerKpiScoreSummaryDto
                {
                    KpiName = g.First().TemplateItem?.KpiDefinition?.KpiName ?? "Unknown",
                    AverageActual = g.Any(cs => cs.ActualValue.HasValue)
                        ? g.Where(cs => cs.ActualValue.HasValue).Average(cs => cs.ActualValue!.Value)
                        : (decimal?)null,
                    AverageTarget = g.First().TemplateItem?.KpiTargetValue,
                    ResponseCount = g.Count()
                })
                .ToList();

            peerSummary = new PeerEvaluationSummaryDto
            {
                IsAnonymous = settings.PeerReviewsAnonymous,
                AllowKpiEvaluation = settings.AllowPeerKpiEvaluation,
                CompetencyScores = peerCompetencyScores,
                KpiScores = peerKpiScores
            };
        }
        // If shouldHidePeerDetails is true, peerSummary remains null

        var totalWeight = settings.SelfEvaluationWeight + settings.ManagerEvaluationWeight + settings.PeerEvaluationWeight;

        return new HRReviewDto
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            Position = appraisal.Employee.Position?.Title ?? "",
            OrganizationUnit = appraisal.Employee.OrganizationUnit?.Name ?? "",
            AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            Status = appraisal.Status,
            IsSelfEvaluationComplete = isSelfComplete,
            IsManagerEvaluationComplete = isManagerComplete,
            RequiresPeerReviews = settings.MinPeerEvaluators > 0,
            ArePeerReviewsComplete = arePeerReviewsComplete,
            RequiredPeerReviews = minPeerReviews,
            CompletedPeerReviews = completedPeerReviews,
            CanProceedToHRReview = canProceed,
            WeightTotalValid = Math.Abs(totalWeight - 1.0m) < 0.01m,
            TotalWeight = totalWeight,
            IsCycleActive = appraisal.AppraisalCycle.Status == AppraisalCycleStatus.Open,
            SelfEvaluation = selfSummary,
            ManagerEvaluation = managerSummary,
            PeerEvaluationSummary = peerSummary,
            SelfWeight = settings.SelfEvaluationWeight,
            ManagerWeight = settings.ManagerEvaluationWeight,
            PeerWeight = settings.PeerEvaluationWeight,
            SelfScore = selfEvaluation?.TotalScore,
            ManagerScore = managerEvaluation?.TotalScore,
            PeerScore = peerEvaluations.Any() ? peerEvaluations.Where(e => e.SubmittedDate.HasValue).Average(e => e.TotalScore) : null,
            FinalScore = appraisal.OverallScore,
            FinalGrade = null, // Not in entity
            IsFinalized = appraisal.Status == AppraisalStatus.Completed,
            HRRemarks = hrEvaluation?.OverallNotes,
            FinalizedDate = hrEvaluation?.SubmittedDate,
            FinalizedByName = hrEvaluation != null && hrEvaluation.SubmittedDate.HasValue ? "HR Reviewer" : null,
            EmployeeAcknowledgedDate = appraisal.EmployeeAcknowledgedDate,
            HasAppeal = appraisal.HasAppeal,
            CurrentAppealStatus = appraisal.CurrentAppealStatus,
            IsRemandedAppeal = appraisal.AppealRemandedDate.HasValue,
            AppealRemandedDate = appraisal.AppealRemandedDate,
            AppealRemandDeadline = appraisal.AppealRemandDeadline,
            IsRemandDeadlineExceeded = appraisal.AppealRemandDeadline.HasValue && DateTime.UtcNow > appraisal.AppealRemandDeadline.Value
        };
    }

    /// <summary>
    /// Helper method to build evaluation summary from an EvaluatorEvaluation
    /// </summary>
    private EvaluationSummaryDto BuildEvaluationSummary(EvaluatorEvaluation evaluation)
    {
        // Competency criteria (non-KPI items) → competency score rows.
        var competencyScores = evaluation.CriterionScores
            .Where(cs => cs.TemplateItem?.CompetencyId != null)
            .Select(cs => new CompetencyScoreSummaryDto
            {
                CriteriaName = cs.TemplateItem?.Competency?.CriteriaName ?? "Unknown",
                Description = cs.TemplateItem?.Competency?.Description,
                NumericScore = cs.NumericScore,
                Weight = 0, // Would need to look this up
                WeightedScore = cs.WeightedScore,
                Comments = cs.Notes
            })
            .ToList();

        // KPI criteria → KPI score rows (NumericScore for a KPI item is its 0–100 achievement-mapped score).
        var kpiScores = evaluation.CriterionScores
            .Where(cs => cs.TemplateItem?.KpiDefinitionId != null)
            .Select(cs => new KpiScoreSummaryDto
            {
                KpiName = cs.TemplateItem?.KpiDefinition?.KpiName ?? "Unknown",
                Description = cs.TemplateItem?.KpiDefinition?.Description,
                ActualValue = cs.ActualValue,
                AchievementPercentage = cs.NumericScore,
                Unit = cs.TemplateItem?.KpiDefinition?.Unit,
                Notes = cs.Notes
            })
            .ToList();

        return new EvaluationSummaryDto
        {
            CompetencyScores = competencyScores,
            KpiScores = kpiScores,
            GeneralComments = evaluation.OverallNotes,
            TotalScore = evaluation.TotalScore
        };
    }

    /// <summary>
    /// Gets list of appraisals for HR review
    /// </summary>
    public async Task<IEnumerable<HRReviewListItemDto>> GetHRReviewListAsync(Guid? cycleId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations)
            .AsQueryable();

        // Filter by cycle if specified
        if (cycleId.HasValue)
        {
            query = query.Where(a => a.AppraisalCycleId == cycleId.Value);
        }

        // Filter by status if specified
        if (!string.IsNullOrEmpty(status))
        {
            switch (status.ToLower())
            {
                case "not started":
                    query = query.Where(a => a.Status == AppraisalStatus.Draft);
                    break;
                case "in review":
                    query = query.Where(a => a.Status == AppraisalStatus.Governance);
                    break;
                case "finalized":
                    query = query.Where(a => a.Status == AppraisalStatus.Completed);
                    break;
            }
        }

        var appraisals = await query.ToListAsync(cancellationToken);

        var results = appraisals.Select(a =>
        {
            var settings = a.AppraisalCycle?.AppraisalSettings;
            var selfEval = a.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
            var managerEval = a.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
            var peerEvals = a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer).ToList();
            var hrEval = a.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.HR);

            var isSelfComplete = selfEval?.SubmittedDate.HasValue ?? false;
            var isManagerComplete = managerEval?.SubmittedDate.HasValue ?? false;
            var requiredPeerReviews = settings?.MinPeerEvaluators ?? 0;
            var completedPeerReviews = peerEvals.Count(e => e.SubmittedDate.HasValue);
            var arePeerReviewsComplete = completedPeerReviews >= requiredPeerReviews;
            var isReadyForHR = isSelfComplete && isManagerComplete && arePeerReviewsComplete;

            string hrReviewStatus;
            if (a.Status == AppraisalStatus.Completed)
                hrReviewStatus = "Finalized";
            else if (a.Status == AppraisalStatus.Governance)
                hrReviewStatus = "In Review";
            else
                hrReviewStatus = "Not Started";

            return new HRReviewListItemDto
            {
                AppraisalId = a.Id,
                AppraisalNumber = a.AppraisalNumber,
                EmployeeId = a.EmployeeId,
                EmployeeNumber = a.Employee.EmployeeNumber,
                EmployeeName = $"{a.Employee.FirstName} {a.Employee.LastName}",
                Position = a.Employee.Position?.Title ?? "",
                OrganizationUnit = a.Employee.OrganizationUnit?.Name ?? "",
                AppraisalCycleName = a.AppraisalCycle?.CycleName ?? "",
                CycleYear = a.AppraisalCycle?.Year ?? 0,
                IsSelfEvaluationComplete = isSelfComplete,
                IsManagerEvaluationComplete = isManagerComplete,
                RequiredPeerReviews = requiredPeerReviews,
                CompletedPeerReviews = completedPeerReviews,
                ArePeerReviewsComplete = arePeerReviewsComplete,
                IsReadyForHRReview = isReadyForHR,
                HRReviewStatus = hrReviewStatus,
                IsFinalized = a.Status == AppraisalStatus.Completed,
                FinalizedDate = hrEval?.SubmittedDate,
                FinalizedByName = hrEval != null && hrEval.SubmittedDate.HasValue ? "HR Reviewer" : null,
                FinalScore = a.OverallScore,
                FinalGrade = null // Not in entity
            };
        }).ToList();

        return results;
    }

    /// <summary>
    /// Approves and finalizes an appraisal
    /// </summary>
    public async Task<HRReviewDto> ApproveAndFinalizeAsync(Guid appraisalId, ApproveAppraisalDto dto, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        // Validate prerequisites
        var selfEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
        var managerEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        var settings = appraisal.AppraisalCycle?.AppraisalSettings;

        if (!(selfEval?.SubmittedDate.HasValue ?? false))
            throw new InvalidOperationException("Self evaluation must be completed before finalizing.");

        if (!(managerEval?.SubmittedDate.HasValue ?? false))
            throw new InvalidOperationException("Manager evaluation must be completed before finalizing.");

        var completedPeerReviews = appraisal.EvaluatorEvaluations.Count(e => e.EvaluatorRole == EvaluatorRole.Peer && e.SubmittedDate.HasValue);
        if (completedPeerReviews < (settings?.MinPeerEvaluators ?? 0))
            throw new InvalidOperationException($"At least {settings?.MinPeerEvaluators} peer reviews must be completed before finalizing.");

        // Update HR evaluation
        var hrEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.HR);
        if (hrEval == null)
            throw new InvalidOperationException("HR evaluation not found.");

        // Atomic finalize: HR sign-off + recalculated OverallScore + Status=Completed persist together,
        // so we can never leave a finalized appraisal with a NULL OverallScore. Safe under the retrying
        // execution strategy (the mutations are idempotent on a retry).
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            hrEval.OverallNotes = dto.HRRemarks;
            hrEval.SubmittedDate = DateTime.UtcNow;

            // Finalize appraisal
            appraisal.Status = AppraisalStatus.Completed;

            // Recalculate final score
            await CalculateOverallScoreAsync(appraisalId, ct);

            await _appraisalRepository.UpdateAsync(appraisal);
        }, cancellationToken);

        // Theme 9 — push the finalized score onto the employee's succession/talent-pool records.
        // Best-effort: the appraisal is already finalized & saved, so a sync failure must not
        // surface as a 500 on an otherwise-successful finalization.
        try
        {
            var finalScore = (await _appraisalRepository.GetByIdAsync(appraisalId))?.OverallScore;
            await _talentRatingSync.SyncFromAppraisalAsync(appraisal.EmployeeId, finalScore, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Talent rating sync failed for finalized appraisal {AppraisalId}; finalization stands.", appraisalId);
        }

        _logger.LogInformation("Appraisal {AppraisalId} approved and finalized by HR", appraisalId);

        // Return updated HR review
        return await GetHRReviewAsync(appraisalId, null, cancellationToken);
    }

    /// <summary>
    /// Returns an appraisal to manager for corrections
    /// </summary>
    public async Task<HRReviewDto> ReturnToManagerAsync(Guid appraisalId, ReturnAppraisalDto dto, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        if (string.IsNullOrWhiteSpace(dto.HRRemarks))
            throw new ArgumentException("HR remarks are required when returning to manager.");

        // Update HR evaluation with remarks
        var hrEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.HR);
        if (hrEval == null)
            throw new InvalidOperationException("HR evaluation not found.");

        hrEval.OverallNotes = dto.HRRemarks;

        // Return to Active so manager can revise evaluation
        appraisal.Status = AppraisalStatus.Active;

        // Reopen manager evaluation (remove submitted date to allow edits)
        var managerEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        if (managerEval != null)
        {
            managerEval.SubmittedDate = null; // Reopen for editing
        }

        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal {AppraisalId} returned to manager by HR", appraisalId);

        // TODO: Send notification to manager
        // await _notificationService.NotifyManagerAppraisalReturnedAsync(appraisalId);

        // Return updated HR review
        return await GetHRReviewAsync(appraisalId, null, cancellationToken);
    }

    #endregion

    #region Section Building Helpers

    /// <summary>
    /// Builds the section/item tree for a self-evaluation context from the appraisal's
    /// CriterionConfig snapshot and (optionally) the linked AppraisalTemplate sections.
    /// Falls back to a single synthetic section when no template is attached.
    /// </summary>
    private List<SelfEvaluationSectionDto> BuildSelfEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation? selfEval,
        IEnumerable<AppraisalCustomQuestionResponse>? customResponses = null)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        var configsByTemplateItemId = appraisal.CriterionConfigs.ToDictionary(cc => cc.TemplateItemId);
        var scoresByTemplateItemId   = selfEval?.CriterionScores.ToDictionary(cs => cs.TemplateItemId)
                                  ?? new Dictionary<Guid, CriterionScore>();
        var goalsByKpiDefId     = appraisal.Goals
                                  .Where(g => g.KpiDefinitionId.HasValue)
                                  .ToLookup(g => g.KpiDefinitionId!.Value);
        var responsesByTemplateItemId = (customResponses ?? Enumerable.Empty<AppraisalCustomQuestionResponse>())
                                         .ToDictionary(r => r.TemplateItemId);

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            // Fallback: single section from all configured criteria
            var fallbackItems = appraisal.CriterionConfigs
                .Select(cc =>
                {
                    scoresByTemplateItemId.TryGetValue(cc.TemplateItemId, out var score);
                    var kpiDef = cc.TemplateItem?.KpiDefinition;
                    var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                    return MapSelfItem(cc, cc.TemplateItem?.Competency, kpiDef, goal, score);
                })
                .ToList();
            return new List<SelfEvaluationSectionDto>
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
                        var config = configsByTemplateItemId[ti.Id];
                        scoresByTemplateItemId.TryGetValue(ti.Id, out var score);
                        var kpiDef = config.TemplateItem?.KpiDefinition;
                        var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                        var item   = MapSelfItem(config, config.TemplateItem?.Competency, kpiDef, goal, score);
                        item.DisplayOrder   = ti.DisplayOrder;
                        item.CustomQuestion = ti.CustomQuestion;
                        return item;
                    })
                    .ToList();

                var customQuestions = section.TemplateItems
                    .OrderBy(ti => ti.DisplayOrder)
                    .Where(ti => ti.CompetencyId == null && ti.KpiDefinitionId == null
                              && !string.IsNullOrWhiteSpace(ti.CustomQuestion))
                    .Select(ti =>
                    {
                        responsesByTemplateItemId.TryGetValue(ti.Id, out var existing);
                        return new SelfEvaluationCustomQuestionDto
                        {
                            TemplateItemId  = ti.Id,
                            QuestionText    = ti.CustomQuestion!,
                            DisplayOrder    = ti.DisplayOrder,
                            ExistingResponse = existing?.ResponseText,
                            IsSubmitted      = !(existing?.IsDraft ?? true)
                        };
                    })
                    .ToList();

                return new SelfEvaluationSectionDto
                {
                    SectionId          = section.Id,
                    SectionName        = section.SectionName,
                    SectionDescription = section.Description,
                    DisplayOrder       = section.DisplayOrder,
                    SectionWeight      = section.Weight,
                    Items              = items,
                    CustomQuestions    = customQuestions
                };
            })
            .Where(s => s.Items.Any() || s.CustomQuestions.Any())
            .ToList();
    }

    private SelfEvaluationItemDto MapSelfItem(
        PerformanceAppraisalCriterionConfig config,
        AppraisalCompetency? competency,
        KpiDefinition? kpiDef,
        EmployeeGoal? goal,
        CriterionScore? existingScore)
    {
        // config.KpiTargetValue is the snapshotted 3-tier-resolved target at appraisal population time.
        // It is more authoritative than goal.TargetValue which may be null or out of sync.
        var effectiveTarget = config.KpiTargetValue ?? goal?.TargetValue;
        var effectiveMin    = config.KpiMinValue    ?? goal?.MinValue;
        var effectiveMax    = config.KpiMaxValue    ?? goal?.MaxValue;

        return new SelfEvaluationItemDto
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
            KpiTargetValue          = effectiveTarget,
            KpiMinValue             = effectiveMin,
            KpiMaxValue             = effectiveMax,
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
            AchievementPercent       = existingScore?.ActualValue.HasValue == true && effectiveTarget > 0
                                       ? existingScore.ActualValue.Value / effectiveTarget.Value * 100m
                                       : null,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────

    private List<ManagerEvaluationSectionDto> BuildManagerEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation? selfEval,
        EvaluatorEvaluation? managerEval,
        List<Guid> appealedTemplateItemIds)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        var configsByTemplateItemId       = appraisal.CriterionConfigs.ToDictionary(cc => cc.TemplateItemId);
        var selfScoresByTemplateItemId    = selfEval?.CriterionScores.ToDictionary(cs => cs.TemplateItemId)
                                           ?? new Dictionary<Guid, CriterionScore>();
        var managerScoresByTemplateItemId = managerEval?.CriterionScores.ToDictionary(cs => cs.TemplateItemId)
                                           ?? new Dictionary<Guid, CriterionScore>();
        var goalsByKpiDefId               = appraisal.Goals
                                           .Where(g => g.KpiDefinitionId.HasValue)
                                           .ToLookup(g => g.KpiDefinitionId!.Value);
        var appealedSet                   = appealedTemplateItemIds.ToHashSet();

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            var fallbackItems = appraisal.CriterionConfigs
                .Select(cc =>
                {
                    selfScoresByTemplateItemId.TryGetValue(cc.TemplateItemId, out var selfScore);
                    managerScoresByTemplateItemId.TryGetValue(cc.TemplateItemId, out var managerScore);
                    var kpiDef = cc.TemplateItem?.KpiDefinition;
                    var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                    return MapManagerItem(cc, cc.TemplateItem?.Competency, kpiDef, goal, selfScore, managerScore, appealedSet.Contains(cc.TemplateItemId));
                })
                .ToList();
            return new List<ManagerEvaluationSectionDto>
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
                        var config = configsByTemplateItemId[ti.Id];
                        selfScoresByTemplateItemId.TryGetValue(ti.Id, out var selfScore);
                        managerScoresByTemplateItemId.TryGetValue(ti.Id, out var managerScore);
                        var kpiDef = config.TemplateItem?.KpiDefinition;
                        var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                        var item   = MapManagerItem(config, config.TemplateItem?.Competency, kpiDef, goal, selfScore, managerScore, appealedSet.Contains(ti.Id));
                        item.DisplayOrder   = ti.DisplayOrder;
                        item.CustomQuestion = ti.CustomQuestion;
                        return item;
                    })
                    .ToList();
                return new ManagerEvaluationSectionDto
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

    private ManagerEvaluationItemDto MapManagerItem(
        PerformanceAppraisalCriterionConfig config,
        AppraisalCompetency? competency,
        KpiDefinition? kpiDef,
        EmployeeGoal? goal,
        CriterionScore? selfScore,
        CriterionScore? managerScore,
        bool isAppealed) => new()
    {
        TemplateItemId           = config.TemplateItemId,
        CriterionConfigId        = config.Id,
        ItemName                 = competency?.CriteriaName ?? kpiDef?.KpiName ?? string.Empty,
        ItemDescription          = competency?.Description,
        ItemWeight               = config.WeightUsed,
        RequireEvidence          = competency?.RequireEvidence ?? false,
        KpiDefinitionId          = kpiDef?.Id,
        KpiUnit                  = kpiDef?.Unit ?? goal?.Unit,
        MeasurementType          = kpiDef?.MeasurementType,
        KpiTargetValue           = goal?.TargetValue,
        KpiMinValue              = goal?.MinValue,
        KpiMaxValue              = goal?.MaxValue,
        GradeRanges              = config.GradeRanges.Select(gr => new EvaluationGradeRangeDto
        {
            GradeDefinitionId = gr.GradeDefinitionId,
            GradeName         = gr.GradeDefinition?.GradeName ?? string.Empty,
            GradeDescription  = gr.GradeDefinition?.Description,
            LowScore          = gr.LowScore,
            HighScore         = gr.HighScore
        }).ToList(),
        // Employee self-score (read-only reference)
        EmployeeSelfCriterionScoreId  = selfScore?.Id,
        EmployeeSelfNumericScore      = selfScore?.NumericScore,
        EmployeeSelfActualValue       = selfScore?.ActualValue,
        EmployeeSelfNotes             = selfScore?.Notes,
        EmployeeSelfEvidenceLinks     = selfScore?.EvidenceLinks,
        // Manager score
        ManagerCriterionScoreId       = managerScore?.Id,
        ManagerNumericScore           = managerScore?.NumericScore,
        ManagerActualValue            = managerScore?.ActualValue,
        ManagerNotes                  = managerScore?.Notes,
        ManagerEvidenceLinks          = managerScore?.EvidenceLinks,
        IsAppealed                    = isAppealed,
    };

    // ─────────────────────────────────────────────────────────────────────────

    private List<SubmittedEvaluationSectionDto> BuildSubmittedEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation selfEval)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        var configsByTemplateItemId = appraisal.CriterionConfigs.ToDictionary(cc => cc.TemplateItemId);
        var scoresByTemplateItemId   = selfEval.CriterionScores.ToDictionary(cs => cs.TemplateItemId);
        var goalsByKpiDefId     = appraisal.Goals
                                  .Where(g => g.KpiDefinitionId.HasValue)
                                  .ToLookup(g => g.KpiDefinitionId!.Value);

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            var fallbackItems = appraisal.CriterionConfigs
                .Select(cc =>
                {
                    scoresByTemplateItemId.TryGetValue(cc.TemplateItemId, out var score);
                    var kpiDef = cc.TemplateItem?.KpiDefinition;
                    var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                    return MapSubmittedItem(cc, cc.TemplateItem?.Competency, kpiDef, goal, score);
                })
                .ToList();
            return new List<SubmittedEvaluationSectionDto>
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
                        var config = configsByTemplateItemId[ti.Id];
                        scoresByTemplateItemId.TryGetValue(ti.Id, out var score);
                        var kpiDef = config.TemplateItem?.KpiDefinition;
                        var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                        return MapSubmittedItem(config, config.TemplateItem?.Competency, kpiDef, goal, score);
                    })
                    .ToList();
                return new SubmittedEvaluationSectionDto
                {
                    SectionName   = section.SectionName,
                    SectionWeight = section.Weight,
                    DisplayOrder  = section.DisplayOrder,
                    Items         = items
                };
            })
            .Where(s => s.Items.Any())
            .ToList();
    }

    private SubmittedEvaluationItemDto MapSubmittedItem(
        PerformanceAppraisalCriterionConfig config,
        AppraisalCompetency? competency,
        KpiDefinition? kpiDef,
        EmployeeGoal? goal,
        CriterionScore? score) => new()
    {
        TemplateItemId      = config.TemplateItemId,
        ItemName            = competency?.CriteriaName ?? kpiDef?.KpiName ?? string.Empty,
        ItemDescription     = competency?.Description,
        ItemWeight          = config.WeightUsed,
        NumericScore        = score?.NumericScore,
        ActualValue         = score?.ActualValue,
        Notes               = score?.Notes,
        EvidenceLinks       = score?.EvidenceLinks,
        KpiUnit             = kpiDef?.Unit ?? goal?.Unit,
        KpiTargetValue      = goal?.TargetValue,
        AchievementPercent  = score?.ActualValue.HasValue == true && goal?.TargetValue > 0
                              ? score.ActualValue.Value / goal.TargetValue.Value * 100m
                              : null
    };

    #endregion
}

#endregion Performance Appraisal

