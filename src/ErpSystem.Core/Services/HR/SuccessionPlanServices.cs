using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SUCCESSION PLAN SERVICE
// ============================================================================

#region Succession Plan Service

public class SuccessionPlanService : ISuccessionPlanService
{
    private readonly ISuccessionPlanRepository _planRepository;
    private readonly ISuccessionCompetencyRequirementRepository _competencyRequirementRepository;
    private readonly ISuccessionActionRepository _actionRepository;
    private readonly ISuccessionPlanHistoryRepository _historyRepository;
    private readonly ISuccessionDocumentRepository _documentRepository;
    private readonly ICompanyHrPolicyProvider _hrPolicyProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SuccessionPlanService> _logger;

    public SuccessionPlanService(
        ISuccessionPlanRepository planRepository,
        ISuccessionCompetencyRequirementRepository competencyRequirementRepository,
        ISuccessionActionRepository actionRepository,
        ISuccessionPlanHistoryRepository historyRepository,
        ISuccessionDocumentRepository documentRepository,
        ICompanyHrPolicyProvider hrPolicyProvider,
        IUnitOfWork unitOfWork,
        ILogger<SuccessionPlanService> logger)
    {
        _planRepository = planRepository;
        _competencyRequirementRepository = competencyRequirementRepository;
        _actionRepository = actionRepository;
        _historyRepository = historyRepository;
        _documentRepository = documentRepository;
        _hrPolicyProvider = hrPolicyProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SuccessionPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Succession plan with ID '{id}' not found.");

        var dto = entity.ToDto();
        var settings = await _hrPolicyProvider.GetAsync(cancellationToken);
        EnrichDemographics(dto, entity, settings);
        return dto;
    }

    /// <summary>
    /// Fills settings-driven demographics (effective retirement date &amp; active service-years-left)
    /// for the incumbent and each candidate. Age / years-of-service are set by the mapper.
    /// </summary>
    private static void EnrichDemographics(SuccessionPlanDto dto, SuccessionPlan entity, Entities.HR.CompanyHrPolicySettings settings)
    {
        if (entity.CurrentIncumbent != null)
        {
            dto.IncumbentAge = HrPolicyCalculations.Age(entity.CurrentIncumbent.DateOfBirth);
            dto.IncumbentServiceYearsLeft = HrPolicyCalculations.ServiceYearsLeft(settings, entity.CurrentIncumbent);
            dto.IncumbentEffectiveRetirementDate =
                HrPolicyCalculations.RetirementDate(settings, entity.CurrentIncumbent)?.ToDateTime(TimeOnly.MinValue);
        }

        var candidatesById = entity.Candidates.ToDictionary(c => c.Id);
        foreach (var candidateDto in dto.Candidates)
        {
            if (candidatesById.TryGetValue(candidateDto.Id, out var candidate) && candidate.Employee != null)
                candidateDto.ServiceYearsLeft = HrPolicyCalculations.ServiceYearsLeft(settings, candidate.Employee);
        }
    }

    public async Task<SuccessionPlanDto?> GetByPlanNumberAsync(string planNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByPlanNumberAsync(planNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<SuccessionPlanSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _planRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.PlanYear)
            .ThenByDescending(p => p.VersionNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SuccessionPlanSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<SuccessionPlanDto?> GetActiveVersionForPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetActiveVersionForPositionAsync(positionId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetAllVersionsForPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetAllVersionsForPositionAsync(positionId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByStatusAsync(SuccessionPlanStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByYearAsync(int planYear, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByYearAsync(planYear);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByYearAndStatusAsync(int planYear, SuccessionPlanStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByYearAndStatusAsync(planYear, status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByCriticalityAsync(PositionCriticality criticality, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByCriticalityAsync(criticality);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByRiskLevelAsync(SuccessionRisk riskLevel, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByRiskLevelAsync(riskLevel);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetDueForReviewAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithNoReadyNowSuccessorAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetWithNoReadyNowSuccessorAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithNoSuccessorsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetWithNoSuccessorsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByIncumbentAsync(Guid incumbentEmployeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByIncumbentAsync(incumbentEmployeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithImpendingVacancyAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetWithImpendingVacancyAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionPlanDto> CreateAsync(CreateSuccessionPlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.PlanNumber = await GeneratePlanNumberAsync(cancellationToken);
        entity.VersionNumber = await _planRepository.GetNextVersionNumberAsync(createDto.PositionId);
        entity.IsActiveVersion = true;
        entity.Status = SuccessionPlanStatus.Draft;

        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan created: {PlanNumber}", entity.PlanNumber);

        return entity.ToDto();
    }

    public async Task<SuccessionPlanDto> UpdateAsync(UpdateSuccessionPlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Succession plan with ID '{updateDto.Id}' not found.");

        if (entity.Status == SuccessionPlanStatus.Approved)
            throw new InvalidOperationException("An approved succession plan cannot be edited. Create a new version instead.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan updated: {PlanNumber}", entity.PlanNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Succession plan with ID '{id}' not found.");

        if (entity.Status == SuccessionPlanStatus.Approved)
            throw new InvalidOperationException("An approved succession plan cannot be deleted.");

        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan deleted: {PlanNumber}", entity.PlanNumber);

        return true;
    }

    public async Task<bool> SubmitForReviewAsync(Guid planId, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(planId);

        if (entity == null)
            throw new ArgumentException($"Succession plan with ID '{planId}' not found.");

        if (entity.Status != SuccessionPlanStatus.Draft)
            throw new InvalidOperationException("Only draft succession plans can be submitted for review.");

        entity.Status = SuccessionPlanStatus.UnderReview;

        await _planRepository.UpdateAsync(entity);
        await CreateSnapshotAsync(entity, submittedByUserId, "Submitted for review", null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan submitted for review: {PlanNumber}", entity.PlanNumber);

        return true;
    }

    public async Task<bool> ReviewAsync(ReviewSuccessionPlanDto reviewDto, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(reviewDto.PlanId);

        if (entity == null)
            throw new ArgumentException($"Succession plan with ID '{reviewDto.PlanId}' not found.");

        if (entity.Status != SuccessionPlanStatus.UnderReview)
            throw new InvalidOperationException("Only plans under review can be reviewed.");

        entity.ReviewedById = reviewDto.ReviewedById;
        entity.ReviewDate = reviewDto.ReviewDate;
        entity.Status = reviewDto.NewStatus;

        await _planRepository.UpdateAsync(entity);
        await CreateSnapshotAsync(entity, reviewDto.ReviewedById, $"Reviewed — status set to {reviewDto.NewStatus}", reviewDto.ReviewNotes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan reviewed: {PlanNumber}, NewStatus: {Status}", entity.PlanNumber, entity.Status);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveSuccessionPlanDto approveDto, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(approveDto.PlanId);

        if (entity == null)
            throw new ArgumentException($"Succession plan with ID '{approveDto.PlanId}' not found.");

        if (entity.Status != SuccessionPlanStatus.UnderReview)
            throw new InvalidOperationException("Only plans under review can be approved.");

        entity.ApprovedById = approveDto.ApprovedById;
        entity.ApprovalDate = approveDto.ApprovalDate;
        entity.Status = SuccessionPlanStatus.Approved;
        entity.IsActiveVersion = true;

        // Supersede any previously approved active version for the same position
        var previousVersions = await _planRepository.GetQueryable()
            .Where(p => p.PositionId == entity.PositionId &&
                        p.Id != entity.Id &&
                        p.IsActiveVersion &&
                        !p.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var previous in previousVersions)
        {
            previous.IsActiveVersion = false;
            previous.SupersededByPlanId = entity.Id;
            previous.Status = SuccessionPlanStatus.Archived;
            await _planRepository.UpdateAsync(previous);
        }

        await _planRepository.UpdateAsync(entity);
        await CreateSnapshotAsync(entity, approveDto.ApprovedById, "Approved", approveDto.ApprovalNotes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan approved: {PlanNumber}", entity.PlanNumber);

        return true;
    }

    #region Competency Requirement Operations

    public async Task<IEnumerable<CompetencyLookupDto>> GetAllActiveCompetenciesAsync(CancellationToken cancellationToken = default)
    {
        var competencies = await _unitOfWork.Repository<Competency>().FindAsync(c => c.IsActive);
        return competencies
            .OrderBy(c => c.Name)
            .Select(c => new CompetencyLookupDto
            {
                Id                 = c.Id,
                Name               = c.Name,
                Code               = c.Code,
                CompetencyCategory = c.CompetencyCategory,
                ProficiencyScaleMax = c.ProficiencyScaleMax
            });
    }

    public async Task<SuccessionCompetencyRequirementDto> AddCompetencyRequirementAsync(CreateSuccessionCompetencyRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _competencyRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionCompetencyRequirementDto>> GetCompetencyRequirementsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _competencyRequirementRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<SuccessionCompetencyRequirementDto> UpdateCompetencyRequirementAsync(UpdateSuccessionCompetencyRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _competencyRequirementRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Competency requirement with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _competencyRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCompetencyRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var entity = await _competencyRequirementRepository.GetByIdAsync(requirementId);

        if (entity == null)
            throw new ArgumentException($"Competency requirement with ID '{requirementId}' not found.");

        await _competencyRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Action Operations

    public async Task<SuccessionActionDto> AddActionAsync(CreateSuccessionActionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await EnsureNoDependencyCycleAsync(entity.Id, entity.DependsOnActionId, cancellationToken);
        await _actionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionActionSummaryDto>> GetActionsForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _actionRepository.GetByPlanIdAsync(planId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionActionDto> UpdateActionAsync(UpdateSuccessionActionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _actionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Succession action with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await EnsureNoDependencyCycleAsync(entity.Id, entity.DependsOnActionId, cancellationToken);

        await _actionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteActionAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var entity = await _actionRepository.GetByIdAsync(actionId);

        if (entity == null)
            throw new ArgumentException($"Succession action with ID '{actionId}' not found.");

        await _actionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region History Operations

    public async Task<IEnumerable<SuccessionPlanHistorySummaryDto>> GetHistoryAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _historyRepository.GetByPlanIdAsync(planId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionPlanHistoryDto?> GetLatestSnapshotAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entity = await _historyRepository.GetLatestSnapshotAsync(planId);
        return entity?.ToDto();
    }

    #endregion

    #region Document Operations

    public async Task<SuccessionDocumentDto> AddDocumentAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.UploadDate = DateTime.UtcNow;
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _documentRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetConfidentialDocumentsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _documentRepository.GetConfidentialDocumentsAsync(planId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(documentId);

        if (entity == null)
            throw new ArgumentException($"Document with ID '{documentId}' not found.");

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<SuccessionDashboardDto> GetDashboardAsync(int? planYear = null, CancellationToken cancellationToken = default)
    {
        // ── Build base plan query (active versions only) ───────────────────────
        var planQuery = _planRepository.GetQueryable()
            .Where(p => p.IsActiveVersion
                     && p.Status != SuccessionPlanStatus.Archived
                     && p.Status != SuccessionPlanStatus.Rejected);

        if (planYear.HasValue)
            planQuery = planQuery.Where(p => p.PlanYear == planYear.Value);

        var plans = await planQuery
            .Select(p => new
            {
                p.Id,
                PositionTitle     = p.Position.Title,
                IncumbentName     = p.CurrentIncumbent == null
                    ? (string?)null
                    : p.CurrentIncumbent.FirstName + " " + p.CurrentIncumbent.LastName,
                p.Criticality,
                p.RiskLevel,
                p.HasReadyNowSuccessor,
                p.HasEmergencySuccessor,
                p.NumberOfIdentifiedSuccessors,
                p.Status,
            })
            .ToListAsync(cancellationToken);

        // ── Actions ───────────────────────────────────────────────────────────
        var actionQuery = _actionRepository.GetQueryable();
        if (planYear.HasValue)
        {
            var planIds = plans.Select(p => p.Id).ToHashSet();
            actionQuery = actionQuery.Where(a => planIds.Contains(a.SuccessionPlanId));
        }

        var today = DateTime.UtcNow.Date;
        var actions = await actionQuery
            .Select(a => new
            {
                a.Id,
                a.ActionDescription,
                ResponsiblePersonName = a.ResponsiblePerson == null
                    ? (string?)null
                    : a.ResponsiblePerson.FirstName + " " + a.ResponsiblePerson.LastName,
                a.DueDate,
                a.Status,
                a.SuccessionPlanId,
                PositionTitle = a.SuccessionPlan.Position.Title,
            })
            .ToListAsync(cancellationToken);

        // ── Talent pool members (all active) ───────────────────────────────────
        var memberRepo = _unitOfWork.Repository<TalentPoolMember>();
        var members = await memberRepo.GetProjectedAsync(
            m => m.RemovedDate == null,
            m => new { m.Readiness });

        // ── KPI counts ─────────────────────────────────────────────────────────
        int totalActivePlans   = plans.Count;
        int totalCritical      = plans.Count(p => p.Criticality >= PositionCriticality.High);
        int readyNowCount      = plans.Count(p => p.HasReadyNowSuccessor);
        int highRiskCount      = plans.Count(p => p.RiskLevel == SuccessionRisk.HighRisk);
        int noSuccessorsCount  = plans.Count(p => p.NumberOfIdentifiedSuccessors == 0);
        double coverage        = totalActivePlans == 0
                                    ? 0
                                    : Math.Round(readyNowCount * 100.0 / totalActivePlans, 1);

        // ── Risk heatmap ───────────────────────────────────────────────────────
        var riskLevels        = new[] { SuccessionRisk.HighRisk, SuccessionRisk.MediumRisk, SuccessionRisk.LowRisk, SuccessionRisk.NoRisk };
        var criticalityLevels = new[] { PositionCriticality.Low, PositionCriticality.Medium, PositionCriticality.High, PositionCriticality.Critical };

        var heatmap = riskLevels
            .SelectMany(r => criticalityLevels.Select(c => new RiskHeatmapCell
            {
                RiskLevel   = r,
                Criticality = c,
                Count       = plans.Count(p => p.RiskLevel == r && p.Criticality == c),
            }))
            .Where(cell => cell.Count > 0)
            .ToList();

        // ── Coverage rows ──────────────────────────────────────────────────────
        var withReadyNow = plans
            .Where(p => p.HasReadyNowSuccessor)
            .OrderBy(p => p.PositionTitle)
            .Select(p => new PositionCoverageRow
            {
                PlanId               = p.Id,
                PositionTitle        = p.PositionTitle,
                CurrentIncumbentName = p.IncumbentName,
                RiskLevel            = p.RiskLevel,
                SuccessorCount       = p.NumberOfIdentifiedSuccessors,
            })
            .ToList();

        var withoutReadyNow = plans
            .Where(p => !p.HasReadyNowSuccessor)
            .OrderByDescending(p => p.RiskLevel == SuccessionRisk.HighRisk ? 10 : (int)p.Criticality)
            .Select(p => new PositionCoverageRow
            {
                PlanId               = p.Id,
                PositionTitle        = p.PositionTitle,
                CurrentIncumbentName = p.IncumbentName,
                RiskLevel            = p.RiskLevel,
                SuccessorCount       = p.NumberOfIdentifiedSuccessors,
            })
            .ToList();

        // ── Talent pool summary ────────────────────────────────────────────────
        var memberList  = members.ToList();
        int totalMembers    = memberList.Count;
        int talentReadyNow  = memberList.Count(m => m.Readiness == ReadinessLevel.ReadyNow);
        int talentReady1To2 = memberList.Count(m =>
            m.Readiness == ReadinessLevel.ReadyIn12Months ||
            m.Readiness == ReadinessLevel.ReadyIn24Months);
        int talentLongTerm  = memberList.Count(m =>
            m.Readiness == ReadinessLevel.ReadyIn36PlusMonths ||
            m.Readiness == ReadinessLevel.NotReady);

        // ── Action metrics ─────────────────────────────────────────────────────
        int totalActions     = actions.Count;
        int completedActions = actions.Count(a => a.Status == ActionStatus.Completed);
        int inProgress       = actions.Count(a => a.Status == ActionStatus.InProgress);
        int overdueCount     = actions.Count(a =>
            a.Status != ActionStatus.Completed && a.Status != ActionStatus.Cancelled
            && a.DueDate.HasValue && a.DueDate.Value.Date < today);

        var overdueAlerts = actions
            .Where(a =>
                a.Status != ActionStatus.Completed && a.Status != ActionStatus.Cancelled
                && a.DueDate.HasValue && a.DueDate.Value.Date < today)
            .OrderBy(a => a.DueDate)
            .Take(20)
            .Select(a => new OverdueActionAlert
            {
                ActionId              = a.Id,
                ActionDescription     = a.ActionDescription,
                ResponsiblePersonName = a.ResponsiblePersonName,
                DueDate               = a.DueDate!.Value,
                PositionTitle         = a.PositionTitle,
            })
            .ToList();

        // ── Bench strength (High/Critical positions) ───────────────────────────
        var benchRows = plans
            .Where(p => p.Criticality >= PositionCriticality.High)
            .OrderBy(p => p.NumberOfIdentifiedSuccessors)
            .ThenBy(p => p.PositionTitle)
            .Select(p => new BenchStrengthRow
            {
                PlanId         = p.Id,
                PositionTitle  = p.PositionTitle,
                SuccessorCount = p.NumberOfIdentifiedSuccessors,
            })
            .ToList();

        // ── Emergency-cover gaps (High/Critical positions with no emergency successor) ──
        var emergencyGaps = plans
            .Where(p => p.Criticality >= PositionCriticality.High && !p.HasEmergencySuccessor)
            .OrderByDescending(p => p.Criticality)
            .ThenBy(p => p.PositionTitle)
            .Select(p => new EmergencyCoverageRow
            {
                PlanId               = p.Id,
                PositionTitle        = p.PositionTitle,
                CurrentIncumbentName = p.IncumbentName,
                Criticality          = p.Criticality,
                RiskLevel            = p.RiskLevel,
                HasReadyNowSuccessor = p.HasReadyNowSuccessor,
            })
            .ToList();
        int positionsWithoutEmergencyCover = plans.Count(p => !p.HasEmergencySuccessor);

        // ── Coverage trend across plan-years (independent of the year filter) ──────
        var trendRaw = await _planRepository.GetQueryable()
            .Where(p => p.IsActiveVersion
                     && p.Status != SuccessionPlanStatus.Archived
                     && p.Status != SuccessionPlanStatus.Rejected)
            .GroupBy(p => p.PlanYear)
            .Select(g => new
            {
                PlanYear = g.Key,
                Total    = g.Count(),
                ReadyNow = g.Count(p => p.HasReadyNowSuccessor),
                Strong   = g.Count(p => p.NumberOfIdentifiedSuccessors >= 3),
            })
            .ToListAsync(cancellationToken);

        var coverageTrend = trendRaw
            .OrderBy(x => x.PlanYear)
            .TakeLast(6)
            .Select(x => new CoverageTrendPoint
            {
                PlanYear         = x.PlanYear,
                TotalPlans       = x.Total,
                ReadyNowPlans    = x.ReadyNow,
                StrongBenchPlans = x.Strong,
            })
            .ToList();

        // ── Plan status distribution ────────────────────────────────────────────
        int draftPlans       = plans.Count(p => p.Status == SuccessionPlanStatus.Draft);
        int underReviewPlans = plans.Count(p => p.Status == SuccessionPlanStatus.UnderReview);
        int approvedPlans    = plans.Count(p => p.Status == SuccessionPlanStatus.Approved);

        // ── Upcoming vacancies & retirements (within configured lead times) ──────
        var settings = await _hrPolicyProvider.GetAsync(cancellationToken);
        var vacancyHorizon = today.AddDays(settings.VacancyAlertLeadDays);
        var retirementHorizon = today.AddDays(settings.RetirementCountdownLeadDays);

        var alertSource = await planQuery
            .Where(p => (p.AnticipatedVacancyDate != null && p.AnticipatedVacancyDate <= vacancyHorizon)
                     || (p.IncumbentRetirementDate != null && p.IncumbentRetirementDate <= retirementHorizon))
            .Select(p => new
            {
                p.Id,
                PositionTitle = p.Position.Title,
                IncumbentName = p.CurrentIncumbent == null ? (string?)null
                    : p.CurrentIncumbent.FirstName + " " + p.CurrentIncumbent.LastName,
                p.AnticipatedVacancyDate,
                p.AnticipatedVacancyReason,
                p.IncumbentRetirementDate,
                p.HasReadyNowSuccessor,
                p.RiskLevel,
            })
            .ToListAsync(cancellationToken);

        var upcomingVacancies = new List<UpcomingVacancyAlert>();
        foreach (var p in alertSource)
        {
            if (p.IncumbentRetirementDate is DateTime retire && retire.Date <= retirementHorizon)
            {
                upcomingVacancies.Add(new UpcomingVacancyAlert
                {
                    PlanId = p.Id,
                    PositionTitle = p.PositionTitle,
                    CurrentIncumbentName = p.IncumbentName,
                    Kind = "Retirement",
                    EventDate = retire,
                    DaysUntil = (int)(retire.Date - today).TotalDays,
                    HasReadyNowSuccessor = p.HasReadyNowSuccessor,
                    RiskLevel = p.RiskLevel,
                });
            }
            if (p.AnticipatedVacancyDate is DateTime vacancy && vacancy.Date <= vacancyHorizon)
            {
                upcomingVacancies.Add(new UpcomingVacancyAlert
                {
                    PlanId = p.Id,
                    PositionTitle = p.PositionTitle,
                    CurrentIncumbentName = p.IncumbentName,
                    Kind = "Vacancy",
                    EventDate = vacancy,
                    DaysUntil = (int)(vacancy.Date - today).TotalDays,
                    Reason = p.AnticipatedVacancyReason?.ToString(),
                    HasReadyNowSuccessor = p.HasReadyNowSuccessor,
                    RiskLevel = p.RiskLevel,
                });
            }
        }
        upcomingVacancies = upcomingVacancies.OrderBy(a => a.EventDate).Take(25).ToList();

        return new SuccessionDashboardDto
        {
            TotalActivePlans               = totalActivePlans,
            TotalCriticalPositions         = totalCritical,
            PositionsWithReadyNowSuccessor = readyNowCount,
            HighRiskPositions              = highRiskCount,
            PositionsWithoutSuccessors     = noSuccessorsCount,
            PositionsWithoutEmergencyCover = positionsWithoutEmergencyCover,
            CoveragePercentage             = coverage,
            RiskHeatmap                    = heatmap,
            WithReadyNow                   = withReadyNow,
            WithoutReadyNow                = withoutReadyNow,
            TotalTalentPoolMembers         = totalMembers,
            TalentReadyNow                 = talentReadyNow,
            TalentReady1To2Years           = talentReady1To2,
            TalentLongTerm                 = talentLongTerm,
            TotalActions                   = totalActions,
            CompletedActions               = completedActions,
            InProgressActions              = inProgress,
            OverdueActionsCount            = overdueCount,
            OverdueAlerts                  = overdueAlerts,
            BenchStrength                  = benchRows,
            UpcomingVacancies              = upcomingVacancies,
            EmergencyCoverageGaps          = emergencyGaps,
            CoverageTrend                  = coverageTrend,
            DraftPlans                     = draftPlans,
            UnderReviewPlans               = underReviewPlans,
            ApprovedPlans                  = approvedPlans,
            FilterYear                     = planYear,
            ComputedAt                     = DateTime.UtcNow,
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Guards <see cref="SuccessionAction.DependsOnActionId"/> against circular chains
    /// (A → B → A). No DB constraint can prevent this — the entity doc requires an
    /// application-layer check. Walks the proposed dependency's chain and throws if it
    /// leads back to the action being edited.
    /// </summary>
    private async Task EnsureNoDependencyCycleAsync(Guid actionId, Guid? dependsOnActionId, CancellationToken cancellationToken)
    {
        if (dependsOnActionId is null)
            return;

        if (dependsOnActionId == actionId)
            throw new InvalidOperationException("An action cannot depend on itself.");

        var visited = new HashSet<Guid>();
        var currentId = dependsOnActionId;

        while (currentId is not null)
        {
            if (currentId == actionId)
                throw new InvalidOperationException("This dependency would create a circular chain of actions.");

            if (!visited.Add(currentId.Value))
                break; // a pre-existing cycle elsewhere — stop to avoid looping forever

            var current = await _actionRepository.GetByIdAsync(currentId.Value);
            currentId = current?.DependsOnActionId;
        }
    }

    private async Task<string> GeneratePlanNumberAsync(CancellationToken cancellationToken)
    {
        var settings = await _hrPolicyProvider.GetAsync(cancellationToken);
        var prefix = string.IsNullOrWhiteSpace(settings.SuccessionPlanNumberPrefix)
            ? "SP"
            : settings.SuccessionPlanNumberPrefix.Trim();

        var count = await _planRepository.CountAsync();
        return $"{prefix}-{DateTime.UtcNow.Year}-{(count + 1):D4}";
    }

    private async Task CreateSnapshotAsync(SuccessionPlan plan, Guid createdById, string changeReason, string? notes, CancellationToken cancellationToken)
    {
        var history = new SuccessionPlanHistory
        {
            SuccessionPlanId = plan.Id,
            VersionNumber = plan.VersionNumber,
            PlanYear = plan.PlanYear,
            StatusAtSnapshot = plan.Status,
            RiskLevelAtSnapshot = plan.RiskLevel,
            HasReadyNowSuccessorAtSnapshot = plan.HasReadyNowSuccessor,
            NumberOfSuccessorsAtSnapshot = plan.NumberOfIdentifiedSuccessors,
            NotesAtSnapshot = notes,
            SnapshotDate = DateTime.UtcNow,
            SnapshotCreatedById = createdById,
            ChangeReason = changeReason
        };

        await _historyRepository.AddAsync(history);
    }

    #endregion
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE SERVICE
// ============================================================================

#region Succession Candidate Service

public class SuccessionCandidateService : ISuccessionCandidateService
{
    private readonly ISuccessionCandidateRepository _candidateRepository;
    private readonly ISuccessionPlanRepository _planRepository;
    private readonly ISuccessionCandidateGapRepository _gapRepository;
    private readonly ISuccessionDevelopmentActivityRepository _activityRepository;
    private readonly ISuccessionDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SuccessionCandidateService> _logger;

    private readonly ICompanyHrPolicyProvider _hrPolicyProvider;
    private readonly IGenericRepository<SuccessionCandidateFeedback> _feedbackRepository;
    private readonly IGenericRepository<PositionCompetency> _positionCompetencyRepository;
    private readonly IGenericRepository<EmployeeCompetency> _employeeCompetencyRepository;

    public SuccessionCandidateService(
        ISuccessionCandidateRepository candidateRepository,
        ISuccessionPlanRepository planRepository,
        ISuccessionCandidateGapRepository gapRepository,
        ISuccessionDevelopmentActivityRepository activityRepository,
        ISuccessionDocumentRepository documentRepository,
        ICompanyHrPolicyProvider hrPolicyProvider,
        IGenericRepository<SuccessionCandidateFeedback> feedbackRepository,
        IGenericRepository<PositionCompetency> positionCompetencyRepository,
        IGenericRepository<EmployeeCompetency> employeeCompetencyRepository,
        IUnitOfWork unitOfWork,
        ILogger<SuccessionCandidateService> logger)
    {
        _candidateRepository = candidateRepository;
        _planRepository = planRepository;
        _gapRepository = gapRepository;
        _activityRepository = activityRepository;
        _documentRepository = documentRepository;
        _hrPolicyProvider = hrPolicyProvider;
        _feedbackRepository = feedbackRepository;
        _positionCompetencyRepository = positionCompetencyRepository;
        _employeeCompetencyRepository = employeeCompetencyRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SuccessionCandidateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Succession candidate with ID '{id}' not found.");

        var dto = entity.ToDto();
        if (entity.Employee != null)
        {
            var settings = await _hrPolicyProvider.GetAsync(cancellationToken);
            dto.ServiceYearsLeft = HrPolicyCalculations.ServiceYearsLeft(settings, entity.Employee);
            dto.RetirementDate = HrPolicyCalculations.RetirementDate(settings, entity.Employee)?.ToDateTime(TimeOnly.MinValue);
        }
        return dto;
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = (await _candidateRepository.GetByPlanIdAsync(planId)).ToList();
        var dtos = entities.ToSummaryDtoList().ToList();

        var settings = await _hrPolicyProvider.GetAsync(cancellationToken);
        var byId = entities.ToDictionary(e => e.Id);
        foreach (var dto in dtos)
        {
            if (byId.TryGetValue(dto.Id, out var entity) && entity.Employee != null)
                dto.ServiceYearsLeft = HrPolicyCalculations.ServiceYearsLeft(settings, entity.Employee);
        }
        return dtos;
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _candidateRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByReadinessAsync(Guid planId, ReadinessLevel readiness, CancellationToken cancellationToken = default)
    {
        var entities = await _candidateRepository.GetByReadinessAsync(planId, readiness);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetReadyNowCandidatesForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _candidateRepository.GetReadyNowCandidatesForPlanAsync(planId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetEmergencyCandidatesForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _candidateRepository.GetEmergencyCandidatesForPlanAsync(planId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionCandidateDto?> GetSelectedCandidateForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetSelectedCandidateForPlanAsync(planId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByRetentionRiskAsync(Guid planId, RetentionRisk minimumRisk, CancellationToken cancellationToken = default)
    {
        var entities = await _candidateRepository.GetByRetentionRiskAsync(planId, minimumRisk);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionCandidateDto> CreateAsync(CreateSuccessionCandidateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _candidateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecalculatePlanDerivedFieldsAsync(createDto.SuccessionPlanId, cancellationToken);

        _logger.LogInformation("Succession candidate created for plan '{PlanId}', employee '{EmployeeId}'", createDto.SuccessionPlanId, createDto.EmployeeId);

        return entity.ToDto();
    }

    public async Task<SuccessionCandidateDto> UpdateAsync(UpdateSuccessionCandidateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Succession candidate with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecalculatePlanDerivedFieldsAsync(entity.SuccessionPlanId, cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Succession candidate with ID '{id}' not found.");

        var planId = entity.SuccessionPlanId;

        await _candidateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecalculatePlanDerivedFieldsAsync(planId, cancellationToken);

        _logger.LogInformation("Succession candidate '{CandidateId}' deleted", id);

        return true;
    }

    public async Task<bool> AssessAsync(AssessCandidateDto assessDto, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetByIdAsync(assessDto.CandidateId);

        if (entity == null)
            throw new ArgumentException($"Succession candidate with ID '{assessDto.CandidateId}' not found.");

        entity.AssessedById = assessDto.AssessedById;
        entity.AssessmentDate = assessDto.AssessmentDate;
        entity.AssessmentNotes = assessDto.AssessmentNotes;
        entity.IsRecommended = assessDto.IsRecommended;
        entity.RecommendationNotes = assessDto.RecommendationNotes;
        entity.RecommendationDate = assessDto.IsRecommended ? assessDto.AssessmentDate : null;
        entity.RecommendedById = assessDto.IsRecommended ? assessDto.AssessedById : null;

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession candidate '{CandidateId}' assessed, recommended: {IsRecommended}", assessDto.CandidateId, assessDto.IsRecommended);

        return true;
    }

    public async Task<bool> SelectCandidateAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _candidateRepository.GetByIdAsync(candidateId);

        if (entity == null)
            throw new ArgumentException($"Succession candidate with ID '{candidateId}' not found.");

        if (!entity.IsRecommended)
            throw new InvalidOperationException("Only recommended candidates can be selected.");

        // Deselect any currently selected candidate for this plan
        var previouslySelected = await _candidateRepository.GetSelectedCandidateForPlanAsync(entity.SuccessionPlanId);
        if (previouslySelected != null && previouslySelected.Id != candidateId)
        {
            previouslySelected.IsSelected = false;
            previouslySelected.SelectionDate = null;
            await _candidateRepository.UpdateAsync(previouslySelected);
        }

        entity.IsSelected = true;
        entity.SelectionDate = DateTime.UtcNow;

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession candidate '{CandidateId}' selected for plan '{PlanId}'", candidateId, entity.SuccessionPlanId);

        return true;
    }

    public async Task BulkUpdateRanksAsync(IEnumerable<CandidateRankUpdateDto> updates, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var updateList = updates.ToList();
        if (updateList.Count == 0)
            return;

        // Reject duplicate target ranks up-front — otherwise the (SuccessionPlanId, Rank)
        // unique index would surface as an opaque DB error on the final write.
        var duplicateRank = updateList.GroupBy(u => u.Rank).FirstOrDefault(g => g.Count() > 1);
        if (duplicateRank is not null)
            throw new InvalidOperationException($"Rank {duplicateRank.Key} was assigned to more than one candidate.");

        var ids = updateList.Select(u => u.Id).ToList();

        // The (SuccessionPlanId, Rank) unique index means a plain reorder that swaps ranks
        // (e.g. 1↔2) violates the constraint mid-statement. Apply the change in two phases
        // inside a single (retry-safe) transaction: first park every affected row at a
        // collision-free temporary rank, then write the final ranks. Re-read inside the
        // operation so it is safe if the execution strategy re-runs it.
        const int tempOffset = 100000;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var entities = await _candidateRepository.GetQueryable()
                .Where(c => ids.Contains(c.Id))
                .ToListAsync(ct);

            var byId = entities.ToDictionary(e => e.Id);

            // Phase 1 — park at temporary, collision-free ranks.
            foreach (var update in updateList)
            {
                if (!byId.TryGetValue(update.Id, out var entity)) continue;
                entity.Rank = tempOffset + update.Rank;
                entity.LastModifiedById = updatedByUserId;
                entity.UpdatedAt = DateTime.UtcNow;
                await _candidateRepository.UpdateAsync(entity);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Phase 2 — write the final ranks; previous occupants have already moved away.
            foreach (var update in updateList)
            {
                if (!byId.TryGetValue(update.Id, out var entity)) continue;
                entity.Rank = update.Rank;
                await _candidateRepository.UpdateAsync(entity);
            }
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation("Bulk rank update applied for {Count} candidates by {UserId}", updateList.Count, updatedByUserId);
    }

    #region Competency Gap Operations

    public async Task<SuccessionCandidateGapDto> AddCompetencyGapAsync(CreateSuccessionCandidateGapDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _gapRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionCandidateGapDto>> GetCompetencyGapsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entities = await _gapRepository.GetByCandidateIdAsync(candidateId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionCandidateGapDto>> GetUnaddressedGapsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entities = await _gapRepository.GetUnaddressedGapsAsync(candidateId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<SuccessionCandidateGapDto> UpdateCompetencyGapAsync(UpdateSuccessionCandidateGapDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _gapRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Competency gap with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _gapRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCompetencyGapAsync(Guid gapId, CancellationToken cancellationToken = default)
    {
        var entity = await _gapRepository.GetByIdAsync(gapId);

        if (entity == null)
            throw new ArgumentException($"Competency gap with ID '{gapId}' not found.");

        await _gapRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<SuccessionCandidateGapDto>> GenerateGapsFromPositionAsync(Guid candidateId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var candidate = await _candidateRepository.GetWithFullDetailsAsync(candidateId);
        if (candidate == null)
            throw new ArgumentException($"Succession candidate with ID '{candidateId}' not found.");

        var positionId = candidate.SuccessionPlan?.PositionId ?? Guid.Empty;
        if (positionId == Guid.Empty)
            throw new InvalidOperationException("The candidate's plan has no target position to map competencies from.");

        // Required proficiency levels for the target position.
        var required = await _positionCompetencyRepository.GetQueryable()
            .Where(pc => pc.PositionId == positionId && !pc.IsDeleted)
            .Select(pc => new { pc.CompetencyId, pc.RequiredProficiencyLevel })
            .ToListAsync(cancellationToken);
        if (required.Count == 0)
            return Enumerable.Empty<SuccessionCandidateGapDto>();

        // The employee's own assessed competency levels.
        var currentLevels = await _employeeCompetencyRepository.GetQueryable()
            .Where(ec => ec.EmployeeId == candidate.EmployeeId && !ec.IsDeleted)
            .Select(ec => new { ec.CompetencyId, ec.CurrentProficiencyLevel })
            .ToListAsync(cancellationToken);
        var currentByCompetency = currentLevels
            .GroupBy(x => x.CompetencyId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CurrentProficiencyLevel));

        // Skip competencies already tracked as a gap for this candidate.
        var existing = (await _gapRepository.GetQueryable()
            .Where(g => g.CandidateId == candidateId && !g.IsDeleted)
            .Select(g => g.CompetencyId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var created = new List<SuccessionCandidateGap>();
        foreach (var pc in required)
        {
            if (existing.Contains(pc.CompetencyId)) continue;

            var gap = new SuccessionCandidateGap
            {
                TenantId = tenantId,
                CandidateId = candidateId,
                CompetencyId = pc.CompetencyId,
                RequiredLevel = pc.RequiredProficiencyLevel,
                CurrentLevel = currentByCompetency.GetValueOrDefault(pc.CompetencyId, 0),
                CreatedBy = createdByUserId.ToString(),
            };
            await _gapRepository.AddAsync(gap);
            created.Add(gap);
        }

        if (created.Count == 0)
            return Enumerable.Empty<SuccessionCandidateGapDto>();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var createdIds = created.Select(c => c.Id).ToList();
        var saved = await _gapRepository.GetQueryable()
            .Include(g => g.Competency)
            .Where(g => createdIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Generated {Count} position-based competency gaps for candidate {CandidateId}", saved.Count, candidateId);
        return saved.Select(g => g.ToDto()).ToList();
    }

    #endregion

    #region Reviewer Feedback Operations

    public async Task<IEnumerable<SuccessionCandidateFeedbackDto>> GetFeedbackAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var items = await _feedbackRepository.GetQueryable()
            .Include(f => f.Reviewer)
            .Where(f => f.CandidateId == candidateId && !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(f => f.ToDto()).ToList();
    }

    public async Task<SuccessionCandidateFeedbackDto> AddFeedbackAsync(Guid candidateId, CreateSuccessionCandidateFeedbackDto createDto, Guid tenantId, Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _candidateRepository.GetByIdAsync(candidateId);
        if (candidate == null)
            throw new ArgumentException($"Succession candidate with ID '{candidateId}' not found.");

        var entity = new SuccessionCandidateFeedback
        {
            TenantId = tenantId,
            CandidateId = candidateId,
            ReviewerId = reviewerEmployeeId,
            Note = createDto.Note.Trim(),
            Disposition = createDto.Disposition,
            CreatedBy = reviewerEmployeeId.ToString(),
        };

        await _feedbackRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with reviewer for the response.
        var saved = await _feedbackRepository.GetQueryable()
            .Include(f => f.Reviewer)
            .FirstOrDefaultAsync(f => f.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Feedback added to candidate {CandidateId} by {ReviewerId}", candidateId, reviewerEmployeeId);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> DeleteFeedbackAsync(Guid feedbackId, CancellationToken cancellationToken = default)
    {
        var entity = await _feedbackRepository.GetByIdAsync(feedbackId);
        if (entity == null)
            throw new ArgumentException($"Feedback with ID '{feedbackId}' not found.");

        await _feedbackRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    #endregion

    #region Development Activity Operations

    public async Task<SuccessionDevelopmentActivityDto> AddDevelopmentActivityAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (createDto.CandidateId == null)
            throw new ArgumentException("CandidateId is required when adding a development activity through the candidate service.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetDevelopmentActivitiesAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetByCandidateIdAsync(candidateId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<SuccessionDevelopmentActivityDto> UpdateDevelopmentActivityAsync(UpdateSuccessionDevelopmentActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _activityRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Development activity with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _activityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteDevelopmentActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var entity = await _activityRepository.GetByIdAsync(activityId);

        if (entity == null)
            throw new ArgumentException($"Development activity with ID '{activityId}' not found.");

        await _activityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Document Operations

    public async Task<SuccessionDocumentDto> AddDocumentAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.UploadDate = DateTime.UtcNow;
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entities = await _documentRepository.GetByCandidateIdAsync(candidateId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(documentId);

        if (entity == null)
            throw new ArgumentException($"Document with ID '{documentId}' not found.");

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Helper Methods

    private async Task RecalculatePlanDerivedFieldsAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetWithFullDetailsAsync(planId);
        if (plan != null)
        {
            plan.RecalculateDerivedFields();
            await _planRepository.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT ACTIVITY SERVICE
// ============================================================================

#region Succession Development Activity Service

public class SuccessionDevelopmentActivityService : ISuccessionDevelopmentActivityService
{
    private readonly ISuccessionDevelopmentActivityRepository _activityRepository;
    private readonly ISuccessionDevelopmentMilestoneRepository _milestoneRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SuccessionDevelopmentActivityService> _logger;

    public SuccessionDevelopmentActivityService(
        ISuccessionDevelopmentActivityRepository activityRepository,
        ISuccessionDevelopmentMilestoneRepository milestoneRepository,
        IUnitOfWork unitOfWork,
        ILogger<SuccessionDevelopmentActivityService> logger)
    {
        _activityRepository = activityRepository;
        _milestoneRepository = milestoneRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SuccessionDevelopmentActivityDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _activityRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Development activity with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetByCandidateIdAsync(candidateId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivityDto>> GetFullByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetByCandidateIdAsync(candidateId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByTalentPoolMemberIdAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetByTalentPoolMemberIdAsync(memberId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivityDto>> GetFullByTalentPoolMemberIdAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetByTalentPoolMemberIdAsync(memberId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByStatusAsync(DevelopmentActivityStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetByStatusAsync(status);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetOverdueActivitiesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetOverdueActivitiesAsync();
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<SuccessionDevelopmentActivityDto> CreateAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (createDto.CandidateId == null && createDto.TalentPoolMemberId == null)
            throw new ArgumentException("A development activity must be linked to either a candidate or a talent pool member.");

        if (createDto.CandidateId != null && createDto.TalentPoolMemberId != null)
            throw new ArgumentException("A development activity cannot be linked to both a candidate and a talent pool member.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development activity created: {ActivityName}", entity.ActivityName);

        return entity.ToDto();
    }

    public async Task<SuccessionDevelopmentActivityDto> UpdateAsync(UpdateSuccessionDevelopmentActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _activityRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Development activity with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _activityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _activityRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Development activity with ID '{id}' not found.");

        if (entity.Status == DevelopmentActivityStatus.InProgress)
            throw new InvalidOperationException("An in-progress development activity cannot be deleted. Cancel it first.");

        await _activityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development activity deleted: {ActivityId}", id);

        return true;
    }

    #region Milestone Operations

    public async Task<SuccessionDevelopmentMilestoneDto> AddMilestoneAsync(CreateSuccessionDevelopmentMilestoneDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _milestoneRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentMilestoneDto>> GetMilestonesAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var entities = await _milestoneRepository.GetByActivityIdAsync(activityId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentMilestoneDto>> GetOverdueMilestonesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _milestoneRepository.GetOverdueMilestonesAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<SuccessionDevelopmentMilestoneDto> UpdateMilestoneAsync(UpdateSuccessionDevelopmentMilestoneDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _milestoneRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Development milestone with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _milestoneRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteMilestoneAsync(Guid milestoneId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _milestoneRepository.GetByIdAsync(milestoneId);

        if (entity == null)
            throw new ArgumentException($"Development milestone with ID '{milestoneId}' not found.");

        if (entity.IsCompleted)
            throw new InvalidOperationException("This milestone is already completed.");

        entity.IsCompleted = true;
        entity.CompletedDate = DateTime.UtcNow;

        await _milestoneRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Milestone '{MilestoneId}' completed", milestoneId);

        return true;
    }

    public async Task<bool> DeleteMilestoneAsync(Guid milestoneId, CancellationToken cancellationToken = default)
    {
        var entity = await _milestoneRepository.GetByIdAsync(milestoneId);

        if (entity == null)
            throw new ArgumentException($"Development milestone with ID '{milestoneId}' not found.");

        await _milestoneRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion
}

#endregion

// ============================================================================
// TALENT POOL SERVICE
// ============================================================================

#region Talent Pool Service

public class TalentPoolService : ITalentPoolService
{
    private readonly ITalentPoolRepository _poolRepository;
    private readonly ITalentPoolMemberRepository _memberRepository;
    private readonly ISuccessionDevelopmentActivityRepository _activityRepository;
    private readonly ISuccessionDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentPoolService> _logger;

    public TalentPoolService(
        ITalentPoolRepository poolRepository,
        ITalentPoolMemberRepository memberRepository,
        ISuccessionDevelopmentActivityRepository activityRepository,
        ISuccessionDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        ILogger<TalentPoolService> logger)
    {
        _poolRepository = poolRepository;
        _memberRepository = memberRepository;
        _activityRepository = activityRepository;
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TalentPoolDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _poolRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Talent pool with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<TalentPoolSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _poolRepository.GetQueryable()
            .Include(p => p.PoolType)
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<PagedResult<TalentPoolSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _poolRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(p => p.PoolType)
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TalentPoolSummaryDto>
        {
            Items = items.Select(e => e.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TalentPoolSummaryDto>> GetByPoolTypeAsync(Guid poolTypeId, CancellationToken cancellationToken = default)
    {
        var entities = await _poolRepository.GetByPoolTypeAsync(poolTypeId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentPoolSummaryDto>> GetActivePoolsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _poolRepository.GetActivePoolsAsync();
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentPoolSummaryDto>> GetByOwnerAsync(Guid ownerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _poolRepository.GetByOwnerAsync(ownerEmployeeId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentPoolDto> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _poolRepository.GetWithMembersAsync(id);

        if (entity == null)
            throw new ArgumentException($"Talent pool with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TalentPoolDto> CreateAsync(CreateTalentPoolDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _poolRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool created: {PoolName}", entity.Name);

        return entity.ToDto();
    }

    public async Task<TalentPoolDto> UpdateAsync(UpdateTalentPoolDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _poolRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Talent pool with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _poolRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool updated: {PoolName}", entity.Name);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _poolRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Talent pool with ID '{id}' not found.");

        await _poolRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool deleted: {PoolId}", id);

        return true;
    }

    #region Member Operations

    public async Task<TalentPoolMemberDto> AddMemberAsync(CreateTalentPoolMemberDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        // Check if employee is already an active member of this pool
        var existing = await _memberRepository.GetMembershipAsync(createDto.TalentPoolId, createDto.EmployeeId);
        if (existing != null && existing.IsActive)
            throw new InvalidOperationException("This employee is already an active member of the talent pool.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.IsActive = true;

        await _memberRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee '{EmployeeId}' added to talent pool '{PoolId}'", createDto.EmployeeId, createDto.TalentPoolId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TalentPoolMemberSummaryDto>> GetMembersAsync(Guid poolId, CancellationToken cancellationToken = default)
    {
        var entities = await _memberRepository.GetByTalentPoolIdAsync(poolId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentPoolMemberDto> GetMemberByIdAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entity = await _memberRepository.GetWithFullDetailsAsync(memberId);

        if (entity == null)
            throw new ArgumentException($"Talent pool member with ID '{memberId}' not found.");

        return entity.ToDto();
    }

    public async Task<TalentPoolMemberDto> UpdateMemberAsync(UpdateTalentPoolMemberDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _memberRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Talent pool member with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _memberRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveMemberAsync(Guid memberId, string removalReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _memberRepository.GetByIdAsync(memberId);

        if (entity == null)
            throw new ArgumentException($"Talent pool member with ID '{memberId}' not found.");

        if (!entity.IsActive)
            throw new InvalidOperationException("This member is already inactive.");

        entity.IsActive = false;
        entity.RemovedDate = DateTime.UtcNow;
        entity.RemovalReason = removalReason;

        await _memberRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool member '{MemberId}' removed from pool", memberId);

        return true;
    }

    public async Task<IEnumerable<TalentPoolMemberSummaryDto>> GetMembersByReadinessAsync(Guid poolId, ReadinessLevel readiness, CancellationToken cancellationToken = default)
    {
        var entities = await _memberRepository.GetByReadinessAsync(poolId, readiness);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentPoolMemberSummaryDto>> GetMembersDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _memberRepository.GetDueForReviewAsync(daysAhead);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region Member Development Activity Operations

    public async Task<SuccessionDevelopmentActivityDto> AddDevelopmentActivityForMemberAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (createDto.TalentPoolMemberId == null)
            throw new ArgumentException("TalentPoolMemberId is required when adding a development activity through the talent pool service.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetDevelopmentActivitiesForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entities = await _activityRepository.GetByTalentPoolMemberIdAsync(memberId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region Document Operations

    public async Task<SuccessionDocumentDto> AddDocumentForMemberAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.UploadDate = DateTime.UtcNow;
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entities = await _documentRepository.GetByTalentPoolMemberIdAsync(memberId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(documentId);

        if (entity == null)
            throw new ArgumentException($"Document with ID '{documentId}' not found.");

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion
}

#endregion

// ============================================================================
// TALENT REVIEW SESSION SERVICE
// ============================================================================

#region Talent Review Session Service

public class TalentReviewSessionService : ITalentReviewSessionService
{
    private readonly ITalentReviewSessionRepository _sessionRepository;
    private readonly ITalentReviewRatingRepository _ratingRepository;
    private readonly ITalentPoolMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentReviewSessionService> _logger;

    public TalentReviewSessionService(
        ITalentReviewSessionRepository sessionRepository,
        ITalentReviewRatingRepository ratingRepository,
        ITalentPoolMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        ILogger<TalentReviewSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _ratingRepository = ratingRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TalentReviewSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Talent review session with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _sessionRepository.GetAllAsync();
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<PagedResult<TalentReviewSessionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _sessionRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.SessionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TalentReviewSessionSummaryDto>
        {
            Items = items.Select(e => e.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetByYearAsync(int reviewYear, CancellationToken cancellationToken = default)
    {
        var entities = await _sessionRepository.GetByYearAsync(reviewYear);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await _sessionRepository.GetByOrganizationUnitAsync(organizationUnitId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetFinalizedSessionsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _sessionRepository.GetFinalizedSessionsAsync();
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetPendingSessionsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _sessionRepository.GetPendingSessionsAsync();
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentReviewSessionDto> GetWithRatingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetWithRatingsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Talent review session with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TalentReviewSessionDto> CreateAsync(CreateTalentReviewSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.IsFinalized = false;

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent review session created: {SessionName}", entity.SessionName);

        return entity.ToDto();
    }

    public async Task<TalentReviewSessionDto> UpdateAsync(UpdateTalentReviewSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Talent review session with ID '{updateDto.Id}' not found.");

        if (entity.IsFinalized)
            throw new InvalidOperationException("A finalized talent review session cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> FinalizeAsync(FinalizeTalentReviewSessionDto finalizeDto, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(finalizeDto.SessionId);

        if (entity == null)
            throw new ArgumentException($"Talent review session with ID '{finalizeDto.SessionId}' not found.");

        if (entity.IsFinalized)
            throw new InvalidOperationException("This talent review session is already finalized.");

        entity.IsFinalized = true;
        entity.FinalizedDate = finalizeDto.FinalizedDate;
        entity.FinalizedById = finalizeDto.FinalizedById;
        entity.SessionNotes = finalizeDto.SessionNotes;

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent review session finalized: {SessionId}", finalizeDto.SessionId);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Talent review session with ID '{id}' not found.");

        if (entity.IsFinalized)
            throw new InvalidOperationException("A finalized talent review session cannot be deleted.");

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent review session deleted: {SessionId}", id);

        return true;
    }

    #region Rating Operations

    public async Task<TalentReviewRatingDto> AddRatingAsync(CreateTalentReviewRatingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(createDto.SessionId);

        if (session == null)
            throw new ArgumentException($"Talent review session with ID '{createDto.SessionId}' not found.");

        if (session.IsFinalized)
            throw new InvalidOperationException("Cannot add ratings to a finalized talent review session.");

        // Check if this employee already has a rating in this session
        var existing = await _ratingRepository.GetBySessionAndEmployeeAsync(createDto.SessionId, createDto.EmployeeId);
        if (existing != null)
            throw new InvalidOperationException("This employee already has a rating in this session. Update the existing rating instead.");

        // Look up previous rating for trend tracking
        var previousRating = await _ratingRepository.GetLatestConfirmedRatingForEmployeeAsync(createDto.EmployeeId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        if (previousRating != null)
        {
            entity.PreviousRatingSessionId = previousRating.SessionId;
            entity.PreviousPerformance = previousRating.Performance;
            entity.PreviousPotential = previousRating.Potential;
        }

        await _ratingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent review rating added for employee '{EmployeeId}' in session '{SessionId}'", createDto.EmployeeId, createDto.SessionId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetRatingsForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await _ratingRepository.GetBySessionIdAsync(sessionId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetRatingsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _ratingRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetByNineBoxPositionAsync(Guid sessionId, PerformanceRating performance, PotentialRating potential, CancellationToken cancellationToken = default)
    {
        var entities = await _ratingRepository.GetByNineBoxPositionAsync(sessionId, performance, potential);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentReviewRatingDto?> GetRatingByIdAsync(Guid ratingId, CancellationToken cancellationToken = default)
    {
        var entity = await _ratingRepository.GetByIdAsync(ratingId);
        return entity?.ToDto();
    }

    public async Task<TalentReviewRatingDto?> GetLatestConfirmedRatingForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await _ratingRepository.GetLatestConfirmedRatingForEmployeeAsync(employeeId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetCalibratedRatingsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await _ratingRepository.GetCalibratedRatingsAsync(sessionId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetPendingCalibrationAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await _ratingRepository.GetPendingCalibrationAsync(sessionId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentReviewRatingDto> UpdateRatingAsync(UpdateTalentReviewRatingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _ratingRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Talent review rating with ID '{updateDto.Id}' not found.");

        if (entity.CalibrationConfirmed)
            throw new InvalidOperationException("A calibration-confirmed rating cannot be modified.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _ratingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> ConfirmCalibrationAsync(ConfirmCalibrationDto confirmDto, CancellationToken cancellationToken = default)
    {
        var entity = await _ratingRepository.GetByIdAsync(confirmDto.RatingId);

        if (entity == null)
            throw new ArgumentException($"Talent review rating with ID '{confirmDto.RatingId}' not found.");

        if (entity.CalibrationConfirmed)
            throw new InvalidOperationException("Calibration is already confirmed for this rating.");

        entity.CalibrationConfirmed = true;
        entity.CalibrationConfirmedById = confirmDto.ConfirmedById;
        entity.CalibrationConfirmedDate = confirmDto.ConfirmedDate;
        entity.CalibrationNotes = confirmDto.CalibrationNotes;

        await _ratingRepository.UpdateAsync(entity);

        // Sync cached performance and potential on the talent pool member
        if (entity.TalentPoolMemberId.HasValue)
        {
            var member = await _memberRepository.GetByIdAsync(entity.TalentPoolMemberId.Value);
            if (member != null)
            {
                member.LatestPerformanceRating = entity.Performance;
                member.LatestPotentialRating = entity.Potential;
                member.RatingLastUpdated = DateTime.UtcNow;
                await _memberRepository.UpdateAsync(member);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calibration confirmed for rating '{RatingId}'", confirmDto.RatingId);

        return true;
    }

    public async Task<bool> DeleteRatingAsync(Guid ratingId, CancellationToken cancellationToken = default)
    {
        var entity = await _ratingRepository.GetByIdAsync(ratingId);

        if (entity == null)
            throw new ArgumentException($"Talent review rating with ID '{ratingId}' not found.");

        if (entity.CalibrationConfirmed)
            throw new InvalidOperationException("A calibration-confirmed rating cannot be deleted.");

        await _ratingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion
}

#endregion
