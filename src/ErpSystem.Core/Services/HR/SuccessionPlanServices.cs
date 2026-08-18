using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SuccessionPlanService> _logger;

    public SuccessionPlanService(
        ISuccessionPlanRepository planRepository,
        ISuccessionCompetencyRequirementRepository competencyRequirementRepository,
        ISuccessionActionRepository actionRepository,
        ISuccessionPlanHistoryRepository historyRepository,
        ISuccessionDocumentRepository documentRepository,
        ICompanyHrPolicyProvider hrPolicyProvider,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SuccessionPlanService> logger)
    {
        _planRepository = planRepository;
        _competencyRequirementRepository = competencyRequirementRepository;
        _actionRepository = actionRepository;
        _historyRepository = historyRepository;
        _documentRepository = documentRepository;
        _hrPolicyProvider = hrPolicyProvider;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SuccessionPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Succession plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionPlan> GetOwnedPlanWithDetailsAsync(Guid id)
    {
        var entity = await _planRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Succession plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionCompetencyRequirement> GetOwnedCompetencyRequirementAsync(Guid id)
    {
        var entity = await _competencyRequirementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Competency requirement with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionAction> GetOwnedActionAsync(Guid id)
    {
        var entity = await _actionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Succession action with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Document with ID '{id}' not found.");
        return entity;
    }

    public async Task<SuccessionPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanWithDetailsAsync(id);

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
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetByPlanNumberAsync(planNumber);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    /// <summary>
    /// The navigations <c>ToSummaryDto</c> reads.
    /// </summary>
    /// <remarks>
    /// ⚠ Every <i>filtered</i> list query on the repository (by status, year, criticality, risk,
    /// due-for-review, no-successors …) already includes these. The two <i>default</i> views did
    /// not: <c>GetAllAsync</c> went through the generic repository and the paged read used a bare
    /// <c>GetQueryable()</c>. Both mapped through the same summary mapper, whose
    /// <c>entity.Position?.Title ?? string.Empty</c> silently produced a blank — so the register,
    /// the first screen anyone opens, showed an empty Position column on every row while every
    /// filtered view beside it showed the title. The position is the subject of a succession plan;
    /// a register without it is unreadable.
    /// </remarks>
    private IQueryable<SuccessionPlan> SummaryQuery(Guid tenantId)
        => _planRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Position)
            .Include(p => p.CurrentIncumbent);

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await SummaryQuery(GetTenantId())
            .OrderByDescending(p => p.PlanYear)
            .ThenByDescending(p => p.VersionNumber)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<SuccessionPlanSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = SummaryQuery(tenantId);
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
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetActiveVersionForPositionAsync(positionId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetAllVersionsForPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetAllVersionsForPositionAsync(positionId))
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByStatusAsync(SuccessionPlanStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetByStatusAsync(status)).Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByYearAsync(int planYear, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetByYearAsync(planYear)).Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByYearAndStatusAsync(int planYear, SuccessionPlanStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetByYearAndStatusAsync(planYear, status))
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByCriticalityAsync(PositionCriticality criticality, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetByCriticalityAsync(criticality))
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByRiskLevelAsync(SuccessionRisk riskLevel, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetByRiskLevelAsync(riskLevel))
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetDueForReviewAsync(daysAhead))
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithNoReadyNowSuccessorAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetWithNoReadyNowSuccessorAsync())
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithNoSuccessorsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetWithNoSuccessorsAsync())
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetByIncumbentAsync(Guid incumbentEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetByIncumbentAsync(incumbentEmployeeId))
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithImpendingVacancyAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _planRepository.GetWithImpendingVacancyAsync(daysAhead))
            .Where(p => p.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionPlanDto> CreateAsync(CreateSuccessionPlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // A position may carry only one plan that is still being worked on. Checked here so the
        // rule can name the plan that blocks it; left to the database alone it surfaced as a bare
        // 500 from UX_SuccessionPlan_ActiveVersion.
        var openPlan = await _planRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId
                        && p.PositionId == createDto.PositionId
                        && (p.Status == SuccessionPlanStatus.Draft || p.Status == SuccessionPlanStatus.UnderReview))
            .Select(p => new { p.PlanNumber, p.PlanYear, p.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (openPlan != null)
            throw new SuccessionConflictException(
                $"This position already has a succession plan in progress ({openPlan.PlanNumber}, {openPlan.PlanYear}, " +
                $"{openPlan.Status}). Finish or delete that plan before starting another.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.PlanNumber = await GeneratePlanNumberAsync(tenantId, cancellationToken);
        entity.VersionNumber = await GetNextVersionNumberForPositionAsync(tenantId, createDto.PositionId, cancellationToken);

        // ⚠ A draft does NOT hold the position's active-version slot. It used to be set true here,
        // which meant a position with an approved plan could never receive a successor version:
        // the create tripped UX_SuccessionPlan_ActiveVersion and 500'd, so the supersede branch in
        // ApproveAsync — which archives the previous version and sets SupersededByPlanId — could
        // never be reached and versioning had never once worked. The flag is now raised on
        // approval, which is what "active version" means.
        entity.IsActiveVersion = false;
        entity.Status = SuccessionPlanStatus.Draft;

        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan created: {PlanNumber}", entity.PlanNumber);

        // Re-read through the detail loader rather than mapping the tracked entity: its Position
        // and incumbent navigations were never loaded, so ToDto() returned PositionTitle = "" and
        // CurrentIncumbentName = null while the detail read of the very same row returned both.
        // A create form that renders the response would show a blank position until refresh.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SuccessionPlanDto> UpdateAsync(UpdateSuccessionPlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(updateDto.Id);

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
        var entity = await GetOwnedPlanAsync(id);

        if (entity.Status == SuccessionPlanStatus.Approved)
            throw new InvalidOperationException("An approved succession plan cannot be deleted.");

        // ⚠ DeleteAsync is a soft delete, but UX_SuccessionPlan_ActiveVersion is filtered on
        // IsActiveVersion alone and knows nothing about IsDeleted — so a deleted plan would keep
        // holding its position's only active-version slot, and the position could never be
        // planned for again. Stand the flag down as part of the delete.
        entity.IsActiveVersion = false;

        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan deleted: {PlanNumber}", entity.PlanNumber);

        return true;
    }

    public async Task<bool> SubmitForReviewAsync(Guid planId, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(planId);

        if (entity.Status != SuccessionPlanStatus.Draft)
            throw new InvalidOperationException("Only draft succession plans can be submitted for review.");

        entity.Status = SuccessionPlanStatus.UnderReview;

        await _planRepository.UpdateAsync(entity);
        await CreateSnapshotAsync(entity, submittedByUserId, "Submitted for review", null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan submitted for review: {PlanNumber}", entity.PlanNumber);

        return true;
    }

    public async Task<bool> ReviewAsync(ReviewSuccessionPlanDto reviewDto, Guid reviewedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(reviewDto.PlanId);

        if (entity.Status != SuccessionPlanStatus.UnderReview)
            throw new InvalidOperationException("Only plans under review can be reviewed.");

        entity.ReviewedById = reviewedByEmployeeId;
        entity.ReviewDate = DateTime.UtcNow;
        entity.Status = reviewDto.NewStatus;

        await _planRepository.UpdateAsync(entity);
        await CreateSnapshotAsync(entity, reviewedByEmployeeId, $"Reviewed — status set to {reviewDto.NewStatus}", reviewDto.ReviewNotes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan reviewed: {PlanNumber}, NewStatus: {Status}", entity.PlanNumber, entity.Status);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveSuccessionPlanDto approveDto, Guid approvedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(approveDto.PlanId);

        if (entity.Status != SuccessionPlanStatus.UnderReview)
            throw new InvalidOperationException("Only plans under review can be approved.");

        // Supersede any previously approved active version for the same position (same tenant).
        //
        // ⚠ This must be saved BEFORE the new version raises its own flag.
        // UX_SuccessionPlan_ActiveVersion is a unique filtered index on (PositionId,
        // IsActiveVersion) — two rows may not both be active, not even mid-transaction. Clearing
        // the old flag and setting the new one in a single SaveChanges leaves the order to EF, and
        // when it wrote the new row first the index rejected it: approving a successor version
        // 500'd. Nobody had met this because a draft used to be unable to exist alongside an
        // approved plan at all, so the supersede branch was unreachable code.
        //
        // The two saves are not atomic: if the second fails, the position is briefly left with no
        // active version. That state is self-healing — re-approving finds nothing to supersede and
        // raises the flag — and is strictly better than the permanent 500 it replaces.
        // ⚠ Deleted rows are included on purpose. The index filters on IsActiveVersion alone, so a
        // soft-deleted plan that still carries the flag occupies the slot just as firmly as a live
        // one — and the delete path only started standing the flag down today, so rows deleted
        // before that are still holding positions hostage. Whatever holds the flag must be cleared,
        // alive or not; a deleted plan is archived silently rather than being marked superseded,
        // because it was never anyone's predecessor.
        var holdingTheSlot = await _planRepository
            .GetQueryableIncludingDeleted(p => p.TenantId == entity.TenantId &&
                                               p.PositionId == entity.PositionId &&
                                               p.Id != entity.Id &&
                                               p.IsActiveVersion)
            .ToListAsync(cancellationToken);

        if (holdingTheSlot.Count > 0)
        {
            foreach (var previous in holdingTheSlot)
            {
                previous.IsActiveVersion = false;

                if (!previous.IsDeleted)
                {
                    previous.SupersededByPlanId = entity.Id;
                    previous.Status = SuccessionPlanStatus.Archived;
                }

                await _planRepository.UpdateAsync(previous);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        entity.ApprovedById = approvedByEmployeeId;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.Status = SuccessionPlanStatus.Approved;
        entity.IsActiveVersion = true;

        await _planRepository.UpdateAsync(entity);
        await CreateSnapshotAsync(entity, approvedByEmployeeId, "Approved", approveDto.ApprovalNotes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession plan approved: {PlanNumber}", entity.PlanNumber);

        return true;
    }

    #region Competency Requirement Operations

    public async Task<IEnumerable<CompetencyLookupDto>> GetAllActiveCompetenciesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var competencies = await _unitOfWork.Repository<Competency>().FindAsync(c => c.TenantId == tenantId && c.IsActive);
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
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(createDto.SuccessionPlanId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _competencyRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionCompetencyRequirementDto>> GetCompetencyRequirementsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _competencyRequirementRepository.GetByPlanIdAsync(planId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<SuccessionCompetencyRequirementDto> UpdateCompetencyRequirementAsync(UpdateSuccessionCompetencyRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCompetencyRequirementAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _competencyRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCompetencyRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCompetencyRequirementAsync(requirementId);

        await _competencyRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Action Operations

    public async Task<SuccessionActionDto> AddActionAsync(CreateSuccessionActionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(createDto.SuccessionPlanId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await EnsureNoDependencyCycleAsync(entity.Id, entity.DependsOnActionId, cancellationToken);
        await _actionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionActionSummaryDto>> GetActionsForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _actionRepository.GetByPlanIdAsync(planId)).Where(a => a.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionActionDto> UpdateActionAsync(UpdateSuccessionActionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActionAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await EnsureNoDependencyCycleAsync(entity.Id, entity.DependsOnActionId, cancellationToken);

        await _actionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteActionAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActionAsync(actionId);

        await _actionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region History Operations

    public async Task<IEnumerable<SuccessionPlanHistorySummaryDto>> GetHistoryAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _historyRepository.GetByPlanIdAsync(planId)).Where(h => h.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionPlanHistoryDto?> GetLatestSnapshotAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entity = await _historyRepository.GetLatestSnapshotAsync(planId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    #endregion

    #region Document Operations

    public async Task<SuccessionDocumentDto> AddDocumentAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        if (createDto.SuccessionPlanId.HasValue)
            await GetOwnedPlanAsync(createDto.SuccessionPlanId.Value);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.UploadDate = DateTime.UtcNow;
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _documentRepository.GetByPlanIdAsync(planId)).Where(d => d.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetConfidentialDocumentsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _documentRepository.GetConfidentialDocumentsAsync(planId))
            .Where(d => d.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<SuccessionDashboardDto> GetDashboardAsync(int? planYear = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // ── Build base plan query (active versions only) ───────────────────────
        var planQuery = _planRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId
                     && p.IsActiveVersion
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
        var actionQuery = _actionRepository.GetQueryable().Where(a => a.TenantId == tenantId);
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
            m => m.TenantId == tenantId && m.RemovedDate == null,
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
            .Where(p => p.TenantId == tenantId
                     && p.IsActiveVersion
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
            if (current == null || current.TenantId != GetTenantId())
                break;
            currentId = current.DependsOnActionId;
        }
    }

    private async Task<int> GetNextVersionNumberForPositionAsync(Guid tenantId, Guid positionId, CancellationToken cancellationToken)
    {
        var maxVersion = await _planRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId && p.PositionId == positionId && !p.IsDeleted)
            .Select(p => (int?)p.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        return maxVersion + 1;
    }

    /// <summary>
    /// Next plan number for the tenant, derived from the highest sequence already issued this year.
    /// </summary>
    /// <remarks>
    /// ⚠ This deliberately counts <b>deleted rows too</b>. It used to be
    /// <c>GetQueryable().CountAsync(...) + 1</c>, and <c>GetQueryable()</c> excludes soft-deleted
    /// rows — but <c>IX_SuccessionPlan_Tenant_PlanNumber</c> is a plain unique index that does not,
    /// so a deleted plan keeps its number reserved forever. The result was that deleting any plan
    /// made the counter fall back onto a number the index still held, and <b>every subsequent
    /// create failed</b> with a duplicate-key 500 — permanently, from one ordinary use of the
    /// delete button. Measured 2026-08-18.
    ///
    /// Taking the maximum issued sequence rather than a live count also survives the other way a
    /// count drifts: numbers are never reused, so two plans can never collide even if rows are
    /// later purged.
    /// </remarks>
    private async Task<string> GeneratePlanNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _hrPolicyProvider.GetAsync(cancellationToken);
        var prefix = string.IsNullOrWhiteSpace(settings.SuccessionPlanNumberPrefix)
            ? "SP"
            : settings.SuccessionPlanNumberPrefix.Trim();

        var yearPrefix = $"{prefix}-{DateTime.UtcNow.Year}-";

        var issued = await _planRepository
            .GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.PlanNumber.StartsWith(yearPrefix))
            .Select(p => p.PlanNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(number => int.TryParse(number[yearPrefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{yearPrefix}{(highest + 1):D4}";
    }

    private async Task CreateSnapshotAsync(SuccessionPlan plan, Guid createdById, string changeReason, string? notes, CancellationToken cancellationToken)
    {
        var history = new SuccessionPlanHistory
        {
            TenantId = plan.TenantId,
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
    private readonly ICurrentUserProvider _currentUserProvider;
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
        ICurrentUserProvider currentUserProvider,
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
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SuccessionCandidate> GetOwnedCandidateAsync(Guid id)
    {
        var entity = await _candidateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Succession candidate with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionCandidate> GetOwnedCandidateWithDetailsAsync(Guid id)
    {
        var entity = await _candidateRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Succession candidate with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Succession plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionCandidateGap> GetOwnedGapAsync(Guid id)
    {
        var entity = await _gapRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Competency gap with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionDevelopmentActivity> GetOwnedActivityAsync(Guid id)
    {
        var entity = await _activityRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Development activity with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Document with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionCandidateFeedback> GetOwnedFeedbackAsync(Guid id)
    {
        var entity = await _feedbackRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Feedback with ID '{id}' not found.");
        return entity;
    }

    public async Task<SuccessionCandidateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateWithDetailsAsync(id);

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
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _candidateRepository.GetByPlanIdAsync(planId))
            .Where(c => c.TenantId == tenantId).ToList();
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
        var tenantId = GetTenantId();
        var entities = (await _candidateRepository.GetByEmployeeIdAsync(employeeId))
            .Where(c => c.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByReadinessAsync(Guid planId, ReadinessLevel readiness, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _candidateRepository.GetByReadinessAsync(planId, readiness))
            .Where(c => c.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetReadyNowCandidatesForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _candidateRepository.GetReadyNowCandidatesForPlanAsync(planId))
            .Where(c => c.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetEmergencyCandidatesForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _candidateRepository.GetEmergencyCandidatesForPlanAsync(planId))
            .Where(c => c.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionCandidateDto?> GetSelectedCandidateForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entity = await _candidateRepository.GetSelectedCandidateForPlanAsync(planId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByRetentionRiskAsync(Guid planId, RetentionRisk minimumRisk, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        var tenantId = GetTenantId();
        var entities = (await _candidateRepository.GetByRetentionRiskAsync(planId, minimumRisk))
            .Where(c => c.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<SuccessionCandidateDto> CreateAsync(CreateSuccessionCandidateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(createDto.SuccessionPlanId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _candidateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecalculatePlanDerivedFieldsAsync(createDto.SuccessionPlanId, cancellationToken);

        _logger.LogInformation("Succession candidate created for plan '{PlanId}', employee '{EmployeeId}'", createDto.SuccessionPlanId, createDto.EmployeeId);

        return entity.ToDto();
    }

    public async Task<SuccessionCandidateDto> UpdateAsync(UpdateSuccessionCandidateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecalculatePlanDerivedFieldsAsync(entity.SuccessionPlanId, cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(id);

        var planId = entity.SuccessionPlanId;

        await _candidateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecalculatePlanDerivedFieldsAsync(planId, cancellationToken);

        _logger.LogInformation("Succession candidate '{CandidateId}' deleted", id);

        return true;
    }

    public async Task<bool> AssessAsync(AssessCandidateDto assessDto, Guid assessedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(assessDto.CandidateId);

        // Both the assessor and the date used to arrive on the body. Recommending a candidate is
        // what unlocks selecting them, so a caller-declared assessor was a way to manufacture a
        // recommendation in someone else's name.
        var assessedOn = DateTime.UtcNow;

        entity.AssessedById = assessedByEmployeeId;
        entity.AssessmentDate = assessedOn;
        entity.AssessmentNotes = assessDto.AssessmentNotes;
        entity.IsRecommended = assessDto.IsRecommended;
        entity.RecommendationNotes = assessDto.RecommendationNotes;
        entity.RecommendationDate = assessDto.IsRecommended ? assessedOn : null;
        entity.RecommendedById = assessDto.IsRecommended ? assessedByEmployeeId : null;

        await _candidateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession candidate '{CandidateId}' assessed, recommended: {IsRecommended}", assessDto.CandidateId, assessDto.IsRecommended);

        return true;
    }

    public async Task<bool> SelectCandidateAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCandidateAsync(candidateId);

        if (!entity.IsRecommended)
            throw new InvalidOperationException("Only recommended candidates can be selected.");

        // Deselect any currently selected candidate for this plan (same tenant)
        var previouslySelected = await _candidateRepository.GetSelectedCandidateForPlanAsync(entity.SuccessionPlanId);
        if (previouslySelected != null && previouslySelected.TenantId == entity.TenantId && previouslySelected.Id != candidateId)
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

        var tenantId = GetTenantId();
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var entities = await _candidateRepository.GetQueryable()
                .Where(c => c.TenantId == tenantId && ids.Contains(c.Id))
                .ToListAsync(ct);

            if (entities.Count != ids.Distinct().Count())
                throw new ArgumentException("One or more succession candidates were not found.");

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
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedCandidateAsync(createDto.CandidateId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _gapRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionCandidateGapDto>> GetCompetencyGapsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var tenantId = GetTenantId();
        var entities = (await _gapRepository.GetByCandidateIdAsync(candidateId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionCandidateGapDto>> GetUnaddressedGapsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var tenantId = GetTenantId();
        var entities = (await _gapRepository.GetUnaddressedGapsAsync(candidateId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<SuccessionCandidateGapDto> UpdateCompetencyGapAsync(UpdateSuccessionCandidateGapDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGapAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _gapRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCompetencyGapAsync(Guid gapId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGapAsync(gapId);

        await _gapRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<SuccessionCandidateGapDto>> GenerateGapsFromPositionAsync(Guid candidateId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var candidate = await GetOwnedCandidateWithDetailsAsync(candidateId);

        var positionId = candidate.SuccessionPlan?.PositionId ?? Guid.Empty;
        if (positionId == Guid.Empty)
            throw new InvalidOperationException("The candidate's plan has no target position to map competencies from.");

        // Required proficiency levels for the target position.
        var required = await _positionCompetencyRepository.GetQueryable()
            .Where(pc => pc.TenantId == tenantId && pc.PositionId == positionId && !pc.IsDeleted)
            .Select(pc => new { pc.CompetencyId, pc.RequiredProficiencyLevel })
            .ToListAsync(cancellationToken);
        if (required.Count == 0)
            return Enumerable.Empty<SuccessionCandidateGapDto>();

        // The employee's own assessed competency levels.
        var currentLevels = await _employeeCompetencyRepository.GetQueryable()
            .Where(ec => ec.TenantId == tenantId && ec.EmployeeId == candidate.EmployeeId && !ec.IsDeleted)
            .Select(ec => new { ec.CompetencyId, ec.CurrentProficiencyLevel })
            .ToListAsync(cancellationToken);
        var currentByCompetency = currentLevels
            .GroupBy(x => x.CompetencyId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CurrentProficiencyLevel));

        // Skip competencies already tracked as a gap for this candidate.
        var existing = (await _gapRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId && g.CandidateId == candidateId && !g.IsDeleted)
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
            .Where(g => g.TenantId == tenantId && createdIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Generated {Count} position-based competency gaps for candidate {CandidateId}", saved.Count, candidateId);
        return saved.Select(g => g.ToDto()).ToList();
    }

    #endregion

    #region Reviewer Feedback Operations

    public async Task<IEnumerable<SuccessionCandidateFeedbackDto>> GetFeedbackAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var tenantId = GetTenantId();
        var items = await _feedbackRepository.GetQueryable()
            .Include(f => f.Reviewer)
            .Where(f => f.TenantId == tenantId && f.CandidateId == candidateId && !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(f => f.ToDto()).ToList();
    }

    public async Task<SuccessionCandidateFeedbackDto> AddFeedbackAsync(Guid candidateId, CreateSuccessionCandidateFeedbackDto createDto, Guid tenantId, Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedCandidateAsync(candidateId);

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
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Feedback added to candidate {CandidateId} by {ReviewerId}", candidateId, reviewerEmployeeId);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> DeleteFeedbackAsync(Guid feedbackId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFeedbackAsync(feedbackId);

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

        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedCandidateAsync(createDto.CandidateId.Value);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetDevelopmentActivitiesAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetByCandidateIdAsync(candidateId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<SuccessionDevelopmentActivityDto> UpdateDevelopmentActivityAsync(UpdateSuccessionDevelopmentActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _activityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteDevelopmentActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(activityId);

        await _activityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Document Operations

    public async Task<SuccessionDocumentDto> AddDocumentAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        if (createDto.CandidateId.HasValue)
            await GetOwnedCandidateAsync(createDto.CandidateId.Value);
        if (createDto.SuccessionPlanId.HasValue)
            await GetOwnedPlanAsync(createDto.SuccessionPlanId.Value);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.UploadDate = DateTime.UtcNow;
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCandidateAsync(candidateId);
        var tenantId = GetTenantId();
        var entities = (await _documentRepository.GetByCandidateIdAsync(candidateId))
            .Where(d => d.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Helper Methods

    private async Task RecalculatePlanDerivedFieldsAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetWithFullDetailsAsync(planId);
        if (plan != null && plan.TenantId == GetTenantId())
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SuccessionDevelopmentActivityService> _logger;

    public SuccessionDevelopmentActivityService(
        ISuccessionDevelopmentActivityRepository activityRepository,
        ISuccessionDevelopmentMilestoneRepository milestoneRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SuccessionDevelopmentActivityService> logger)
    {
        _activityRepository = activityRepository;
        _milestoneRepository = milestoneRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SuccessionDevelopmentActivity> GetOwnedActivityAsync(Guid id)
    {
        var entity = await _activityRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Development activity with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionDevelopmentActivity> GetOwnedActivityWithDetailsAsync(Guid id)
    {
        var entity = await _activityRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Development activity with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionDevelopmentMilestone> GetOwnedMilestoneAsync(Guid id)
    {
        var entity = await _milestoneRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Development milestone with ID '{id}' not found.");
        return entity;
    }

    public async Task<SuccessionDevelopmentActivityDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetByCandidateIdAsync(candidateId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivityDto>> GetFullByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetByCandidateIdAsync(candidateId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByTalentPoolMemberIdAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetByTalentPoolMemberIdAsync(memberId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivityDto>> GetFullByTalentPoolMemberIdAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetByTalentPoolMemberIdAsync(memberId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByStatusAsync(DevelopmentActivityStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetOverdueActivitiesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetOverdueActivitiesAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<SuccessionDevelopmentActivityDto> CreateAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (createDto.CandidateId == null && createDto.TalentPoolMemberId == null)
            throw new ArgumentException("A development activity must be linked to either a candidate or a talent pool member.");

        if (createDto.CandidateId != null && createDto.TalentPoolMemberId != null)
            throw new ArgumentException("A development activity cannot be linked to both a candidate and a talent pool member.");

        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development activity created: {ActivityName}", entity.ActivityName);

        return entity.ToDto();
    }

    public async Task<SuccessionDevelopmentActivityDto> UpdateAsync(UpdateSuccessionDevelopmentActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _activityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(id);

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
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedActivityAsync(createDto.ActivityId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _milestoneRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentMilestoneDto>> GetMilestonesAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        await GetOwnedActivityAsync(activityId);
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetByActivityIdAsync(activityId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<SuccessionDevelopmentMilestoneDto>> GetOverdueMilestonesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetOverdueMilestonesAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<SuccessionDevelopmentMilestoneDto> UpdateMilestoneAsync(UpdateSuccessionDevelopmentMilestoneDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMilestoneAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _milestoneRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteMilestoneAsync(Guid milestoneId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMilestoneAsync(milestoneId);

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
        var entity = await GetOwnedMilestoneAsync(milestoneId);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentPoolService> _logger;

    public TalentPoolService(
        ITalentPoolRepository poolRepository,
        ITalentPoolMemberRepository memberRepository,
        ISuccessionDevelopmentActivityRepository activityRepository,
        ISuccessionDocumentRepository documentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TalentPoolService> logger)
    {
        _poolRepository = poolRepository;
        _memberRepository = memberRepository;
        _activityRepository = activityRepository;
        _documentRepository = documentRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<TalentPool> GetOwnedPoolAsync(Guid id)
    {
        var entity = await _poolRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent pool with ID '{id}' not found.");
        return entity;
    }

    private async Task<TalentPool> GetOwnedPoolWithMembersAsync(Guid id)
    {
        var entity = await _poolRepository.GetWithMembersAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent pool with ID '{id}' not found.");
        return entity;
    }

    private async Task<TalentPoolMember> GetOwnedMemberAsync(Guid id)
    {
        var entity = await _memberRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent pool member with ID '{id}' not found.");
        return entity;
    }

    private async Task<TalentPoolMember> GetOwnedMemberWithDetailsAsync(Guid id)
    {
        var entity = await _memberRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent pool member with ID '{id}' not found.");
        return entity;
    }

    private async Task<SuccessionDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Document with ID '{id}' not found.");
        return entity;
    }

    public async Task<TalentPoolDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPoolAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<TalentPoolSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _poolRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.PoolType)
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<PagedResult<TalentPoolSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _poolRepository.GetQueryable().Where(p => p.TenantId == tenantId);
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
        var tenantId = GetTenantId();
        var entities = (await _poolRepository.GetByPoolTypeAsync(poolTypeId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentPoolSummaryDto>> GetActivePoolsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _poolRepository.GetActivePoolsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentPoolSummaryDto>> GetByOwnerAsync(Guid ownerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _poolRepository.GetByOwnerAsync(ownerEmployeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentPoolDto> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPoolWithMembersAsync(id);
        return entity.ToDto();
    }

    public async Task<TalentPoolDto> CreateAsync(CreateTalentPoolDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _poolRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool created: {PoolName}", entity.Name);

        return entity.ToDto();
    }

    public async Task<TalentPoolDto> UpdateAsync(UpdateTalentPoolDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPoolAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _poolRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool updated: {PoolName}", entity.Name);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPoolAsync(id);

        await _poolRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool deleted: {PoolId}", id);

        return true;
    }

    #region Member Operations

    public async Task<TalentPoolMemberDto> AddMemberAsync(CreateTalentPoolMemberDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPoolAsync(createDto.TalentPoolId);

        // Check if employee is already an active member of this pool
        var existing = await _memberRepository.GetMembershipAsync(createDto.TalentPoolId, createDto.EmployeeId);
        if (existing != null && existing.TenantId == tenantId && existing.IsActive)
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
        await GetOwnedPoolAsync(poolId);
        var tenantId = GetTenantId();
        var entities = (await _memberRepository.GetByTalentPoolIdAsync(poolId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentPoolMemberDto> GetMemberByIdAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMemberWithDetailsAsync(memberId);
        return entity.ToDto();
    }

    public async Task<TalentPoolMemberDto> UpdateMemberAsync(UpdateTalentPoolMemberDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMemberAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _memberRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveMemberAsync(Guid memberId, string removalReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMemberAsync(memberId);

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
        await GetOwnedPoolAsync(poolId);
        var tenantId = GetTenantId();
        var entities = (await _memberRepository.GetByReadinessAsync(poolId, readiness))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentPoolMemberSummaryDto>> GetMembersDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _memberRepository.GetDueForReviewAsync(daysAhead))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region Member Development Activity Operations

    public async Task<SuccessionDevelopmentActivityDto> AddDevelopmentActivityForMemberAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (createDto.TalentPoolMemberId == null)
            throw new ArgumentException("TalentPoolMemberId is required when adding a development activity through the talent pool service.");

        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedMemberAsync(createDto.TalentPoolMemberId.Value);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetDevelopmentActivitiesForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMemberAsync(memberId);
        var tenantId = GetTenantId();
        var entities = (await _activityRepository.GetByTalentPoolMemberIdAsync(memberId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region Document Operations

    public async Task<SuccessionDocumentDto> AddDocumentForMemberAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        if (createDto.TalentPoolMemberId.HasValue)
            await GetOwnedMemberAsync(createDto.TalentPoolMemberId.Value);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.UploadDate = DateTime.UtcNow;
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMemberAsync(memberId);
        var tenantId = GetTenantId();
        var entities = (await _documentRepository.GetByTalentPoolMemberIdAsync(memberId))
            .Where(d => d.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentReviewSessionService> _logger;

    public TalentReviewSessionService(
        ITalentReviewSessionRepository sessionRepository,
        ITalentReviewRatingRepository ratingRepository,
        ITalentPoolMemberRepository memberRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TalentReviewSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _ratingRepository = ratingRepository;
        _memberRepository = memberRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<TalentReviewSession> GetOwnedSessionAsync(Guid id)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent review session with ID '{id}' not found.");
        return entity;
    }

    private async Task<TalentReviewSession> GetOwnedSessionWithRatingsAsync(Guid id)
    {
        var entity = await _sessionRepository.GetWithRatingsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent review session with ID '{id}' not found.");
        return entity;
    }

    private async Task<TalentReviewRating> GetOwnedRatingAsync(Guid id)
    {
        var entity = await _ratingRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Talent review rating with ID '{id}' not found.");
        return entity;
    }

    public async Task<TalentReviewSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _sessionRepository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<PagedResult<TalentReviewSessionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _sessionRepository.GetQueryable().Where(s => s.TenantId == tenantId);
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
        var tenantId = GetTenantId();
        var entities = (await _sessionRepository.GetByYearAsync(reviewYear))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _sessionRepository.GetByOrganizationUnitAsync(organizationUnitId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetFinalizedSessionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _sessionRepository.GetFinalizedSessionsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewSessionSummaryDto>> GetPendingSessionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _sessionRepository.GetPendingSessionsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentReviewSessionDto> GetWithRatingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionWithRatingsAsync(id);
        return entity.ToDto();
    }

    public async Task<TalentReviewSessionDto> CreateAsync(CreateTalentReviewSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.IsFinalized = false;

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent review session created: {SessionName}", entity.SessionName);

        return entity.ToDto();
    }

    public async Task<TalentReviewSessionDto> UpdateAsync(UpdateTalentReviewSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(updateDto.Id);

        if (entity.IsFinalized)
            throw new InvalidOperationException("A finalized talent review session cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> FinalizeAsync(FinalizeTalentReviewSessionDto finalizeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(finalizeDto.SessionId);

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
        var entity = await GetOwnedSessionAsync(id);

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
        tenantId = RequireCurrentTenant(tenantId);
        var session = await GetOwnedSessionAsync(createDto.SessionId);

        if (session.IsFinalized)
            throw new InvalidOperationException("Cannot add ratings to a finalized talent review session.");

        // Check if this employee already has a rating in this session
        var existing = await _ratingRepository.GetBySessionAndEmployeeAsync(createDto.SessionId, createDto.EmployeeId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("This employee already has a rating in this session. Update the existing rating instead.");

        // Look up previous rating for trend tracking (same tenant)
        var previousRating = await _ratingRepository.GetLatestConfirmedRatingForEmployeeAsync(createDto.EmployeeId);
        if (previousRating != null && previousRating.TenantId != tenantId)
            previousRating = null;

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
        await GetOwnedSessionAsync(sessionId);
        var tenantId = GetTenantId();
        var entities = (await _ratingRepository.GetBySessionIdAsync(sessionId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetRatingsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _ratingRepository.GetByEmployeeIdAsync(employeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetByNineBoxPositionAsync(Guid sessionId, PerformanceRating performance, PotentialRating potential, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var tenantId = GetTenantId();
        var entities = (await _ratingRepository.GetByNineBoxPositionAsync(sessionId, performance, potential))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentReviewRatingDto?> GetRatingByIdAsync(Guid ratingId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _ratingRepository.GetByIdAsync(ratingId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<TalentReviewRatingDto?> GetLatestConfirmedRatingForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _ratingRepository.GetLatestConfirmedRatingForEmployeeAsync(employeeId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetCalibratedRatingsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var tenantId = GetTenantId();
        var entities = (await _ratingRepository.GetCalibratedRatingsAsync(sessionId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<IEnumerable<TalentReviewRatingSummaryDto>> GetPendingCalibrationAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedSessionAsync(sessionId);
        var tenantId = GetTenantId();
        var entities = (await _ratingRepository.GetPendingCalibrationAsync(sessionId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    public async Task<TalentReviewRatingDto> UpdateRatingAsync(UpdateTalentReviewRatingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRatingAsync(updateDto.Id);

        if (entity.CalibrationConfirmed)
            throw new InvalidOperationException("A calibration-confirmed rating cannot be modified.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _ratingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> ConfirmCalibrationAsync(ConfirmCalibrationDto confirmDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRatingAsync(confirmDto.RatingId);

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
            if (member != null && member.TenantId == GetTenantId())
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
        var entity = await GetOwnedRatingAsync(ratingId);

        if (entity.CalibrationConfirmed)
            throw new InvalidOperationException("A calibration-confirmed rating cannot be deleted.");

        await _ratingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion
}

#endregion
