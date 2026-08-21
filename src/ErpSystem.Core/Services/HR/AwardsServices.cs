using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

#region Award Type Services

public class AwardTypeService : IAwardTypeService
{
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardTypeService(
        IAwardTypeRepository awardTypeRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _awardTypeRepo = awardTypeRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardType> GetOwnedAsync(Guid id)
    {
        var entity = await _awardTypeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardType {id} not found.");
        return entity;
    }

    public async Task<AwardTypeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _awardTypeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardTypeDto?> GetByCodeAsync(Guid tenantId, string code)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await _awardTypeRepo.GetByCodeAsync(tenantId, code);
        return entity?.ToDto();
    }

    public async Task<AwardTypeDto?> GetWithDetailsAsync(Guid id)
    {
        var entity = await _awardTypeRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetAllAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var types = await _awardTypeRepo.GetByTenantAsync(tenantId);
        var result = new List<AwardTypeSummaryDto>();
        foreach (var type in types)
        {
            var count = await _awardTypeRepo.GetAwardCountByTypeAsync(type.Id);
            result.Add(type.ToSummaryDto(count));
        }
        return result;
    }

    public async Task<PagedResult<AwardTypeSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, AwardCategory? category = null)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var all = await _awardTypeRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(at => at.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || at.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        if (category.HasValue)
            q = q.Where(at => at.Category == category.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var dtos = new List<AwardTypeSummaryDto>();
        foreach (var type in items)
        {
            var count = await _awardTypeRepo.GetAwardCountByTypeAsync(type.Id);
            dtos.Add(type.ToSummaryDto(count));
        }

        return new PagedResult<AwardTypeSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetActiveByCategoryAsync(Guid tenantId, AwardCategory category)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var types = await _awardTypeRepo.GetActiveByCategoryAsync(tenantId, category);
        return types.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetActiveByFrequencyAsync(Guid tenantId, AwardFrequency frequency)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var types = await _awardTypeRepo.GetActiveByFrequencyAsync(tenantId, frequency);
        return types.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetWithLevelsAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var types = await _awardTypeRepo.GetWithLevelsAsync(tenantId);
        return types.ToSummaryDtoList();
    }

    public async Task<bool> CanDeleteAsync(Guid id)
    {
        var entity = await _awardTypeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return false;
        return !await _awardTypeRepo.HasActiveNominationsAsync(id);
    }

    public async Task<bool> IsInUseAsync(Guid id)
    {
        var entity = await _awardTypeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return false;
        return await _awardTypeRepo.IsInUseAsync(id);
    }

    /// <summary>
    /// Refuses the one combination of source and decision that cannot be carried out.
    /// </summary>
    /// <remarks>
    /// A direct management selection has no nomination stage, so there is no candidate list for
    /// employees to vote on or for a committee to score. Every other pairing is real and named in
    /// TDC's note: open nomination decided by a vote, by a committee, or by management; and
    /// performance-triggered candidates decided any of those three ways.
    ///
    /// The message states both halves. A rule that fires correctly but cannot explain itself
    /// leaves the user on a form that will not submit with no idea which field to change.
    /// </remarks>
    private static void ValidateSelection(AwardNominationSource source, AwardWinnerDecision decision)
    {
        if (source == AwardNominationSource.ManagementDirect
            && decision != AwardWinnerDecision.ManagementDecision)
        {
            throw AwardsWorkflowException.Invalid(
                $"An award whose candidates come from a direct management selection cannot be decided by " +
                $"{decision}. There is no nomination stage, so there are no candidates to vote on or to score. " +
                $"Either set the winner decision to ManagementDecision, or change the nomination source to " +
                $"OpenNomination or PerformanceTriggered.");
        }
    }

    public async Task<AwardTypeDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardTypeDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);
        ValidateSelection(dto.NominationSource, dto.WinnerDecision);

        var entity = dto.ToEntity(tenantId, userId);
        await _awardTypeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardTypeDto> UpdateAsync(Guid id, Guid userId, UpdateAwardTypeDto dto)
    {
        var entity = await GetOwnedAsync(id);
        ValidateSelection(dto.NominationSource, dto.WinnerDecision);

        entity.UpdateEntity(dto, userId);
        await _awardTypeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _awardTypeRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Award Level Services

public class AwardLevelService : IAwardLevelService
{
    private readonly IAwardLevelRepository _levelRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardLevelService(
        IAwardLevelRepository levelRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _levelRepo = levelRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardLevel> GetOwnedAsync(Guid id)
    {
        var entity = await _levelRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardLevel {id} not found.");
        return entity;
    }

    public async Task<AwardLevelDto?> GetByIdAsync(Guid id)
    {
        var entity = await _levelRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardLevelDto?> GetByCodeAsync(Guid tenantId, string code)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await _levelRepo.GetByCodeAsync(tenantId, code);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardLevelDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var tenantId = GetTenantId();
        var levels = await _levelRepo.GetByAwardTypeIdAsync(awardTypeId);
        return levels.Where(l => l.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<AwardLevelDto>> GetActiveByAwardTypeIdAsync(Guid awardTypeId)
    {
        var tenantId = GetTenantId();
        var levels = await _levelRepo.GetActiveByAwardTypeIdAsync(awardTypeId);
        return levels.Where(l => l.TenantId == tenantId).ToDtoList();
    }

    public async Task<AwardLevelDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardLevelDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _levelRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardLevelDto> UpdateAsync(Guid id, Guid userId, UpdateAwardLevelDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _levelRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _levelRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardTypeTargetService : IAwardTypeTargetService
{
    private readonly IAwardTypeTargetRepository _targetRepo;
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly IAwardTargetNameResolver _targetNames;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardTypeTargetService(
        IAwardTypeTargetRepository targetRepo,
        IAwardTypeRepository awardTypeRepo,
        IAwardTargetNameResolver targetNames,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _targetRepo = targetRepo;
        _awardTypeRepo = awardTypeRepo;
        _targetNames = targetNames;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Maps targets and fills in what each one points at.
    /// </summary>
    /// <remarks>
    /// <c>TargetName</c> is on the DTO but had no writer anywhere, so every eligibility rule read
    /// back as its kind and a blank — "Employee: " — which is unusable on a screen and indexes
    /// nothing for a search. A target whose subject has since been deleted stays null rather than
    /// getting an invented label: a dangling reference should look like one.
    /// </remarks>
    private async Task<List<AwardTypeTargetDto>> ToDtosWithNamesAsync(IEnumerable<AwardTypeTarget> targets)
    {
        var list = targets.ToList();
        var names = await _targetNames.ResolveAsync(list);

        var dtos = list.ToDtoList();
        foreach (var dto in dtos)
        {
            if (dto.TargetId.HasValue && names.TryGetValue(dto.TargetId.Value, out var name))
                dto.TargetName = name;
        }
        return dtos;
    }

    private async Task<AwardTypeTargetDto> ToDtoWithNameAsync(AwardTypeTarget target)
        => (await ToDtosWithNamesAsync(new[] { target }))[0];

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

    private async Task<AwardTypeTarget> GetOwnedAsync(Guid id)
    {
        var entity = await _targetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardTypeTarget {id} not found.");
        return entity;
    }

    public async Task<AwardTypeTargetDto?> GetByIdAsync(Guid id)
    {
        var entity = await _targetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return await ToDtoWithNameAsync(entity);
    }

    public async Task<IEnumerable<AwardTypeTargetDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var tenantId = GetTenantId();
        var targets = await _targetRepo.GetByAwardTypeIdAsync(awardTypeId);
        return await ToDtosWithNamesAsync(targets.Where(t => t.TenantId == tenantId));
    }

    public async Task<IEnumerable<AwardTypeTargetDto>> GetByScopeAsync(Guid awardTypeId, AwardScope scope)
    {
        var tenantId = GetTenantId();
        var targets = await _targetRepo.GetByScopeAsync(awardTypeId, scope);
        return await ToDtosWithNamesAsync(targets.Where(t => t.TenantId == tenantId));
    }

    public async Task<bool> IsEmployeeEligibleAsync(Guid awardTypeId, Guid employeeId)
    {
        var awardType = await _awardTypeRepo.GetByIdAsync(awardTypeId);
        if (awardType == null || awardType.TenantId != GetTenantId())
            return false;
        return await _targetRepo.IsEmployeeEligibleAsync(awardTypeId, employeeId);
    }

    public async Task<AwardTypeTargetDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardTypeTargetDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _targetRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await ToDtoWithNameAsync(entity);
    }

    public async Task<AwardTypeTargetDto> UpdateAsync(Guid id, Guid userId, UpdateAwardTypeTargetDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _targetRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await ToDtoWithNameAsync(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _targetRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardBudgetService : IAwardBudgetService
{
    private readonly IAwardBudgetRepository _budgetRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardBudgetService(
        IAwardBudgetRepository budgetRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _budgetRepo = budgetRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardBudget> GetOwnedAsync(Guid id)
    {
        var entity = await _budgetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardBudget {id} not found.");
        return entity;
    }

    public async Task<AwardBudgetDto?> GetByIdAsync(Guid id)
    {
        var entity = await _budgetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardBudgetDto?> GetByYearAsync(Guid awardTypeId, int year)
    {
        var entity = await _budgetRepo.GetByYearAsync(awardTypeId, year);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardBudgetDto?> GetByBudgetCodeAsync(Guid tenantId, string budgetCode)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await _budgetRepo.GetByBudgetCodeAsync(tenantId, budgetCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardBudgetDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var tenantId = GetTenantId();
        var budgets = await _budgetRepo.GetByAwardTypeIdAsync(awardTypeId);
        return budgets.Where(b => b.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<AwardBudgetDto>> GetByYearRangeAsync(Guid tenantId, int startYear, int endYear)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var budgets = await _budgetRepo.GetByYearRangeAsync(tenantId, startYear, endYear);
        return budgets.ToDtoList();
    }

    public async Task<decimal> GetAvailableBudgetAsync(Guid awardTypeId, int year)
    {
        var entity = await _budgetRepo.GetByYearAsync(awardTypeId, year);
        if (entity == null || entity.TenantId != GetTenantId())
            return 0m;
        return await _budgetRepo.GetAvailableBudgetAsync(awardTypeId, year);
    }

    public async Task<AwardBudgetDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardBudgetDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Check if budget already exists for this award type and year (per-tenant)
        var existing = await _budgetRepo.GetByYearAsync(dto.AwardTypeId, dto.Year);
        if (existing != null && existing.TenantId == tenantId)
            throw AwardsWorkflowException.Conflict($"Budget for award type {dto.AwardTypeId} and year {dto.Year} already exists.");

        var entity = dto.ToEntity(tenantId, userId);
        await _budgetRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        // Re-read before mapping: ToDto renders names off navigations, and the entity we just
        // constructed has none loaded. See TeamAwardNomineeService.AddAsync.
        return (await _budgetRepo.GetByIdAsync(entity.Id) ?? entity).ToDto();
    }

    public async Task<AwardBudgetDto> UpdateAsync(Guid id, Guid userId, UpdateAwardBudgetDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _budgetRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _budgetRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Employee Award Services

public class EmployeeAwardService : IEmployeeAwardService
{
    private readonly IEmployeeAwardRepository _awardRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public EmployeeAwardService(
        IEmployeeAwardRepository awardRepo,
        IAwardNominationRepository nominationRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _awardRepo = awardRepo;
        _nominationRepo = nominationRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<EmployeeAward> GetOwnedAsync(Guid id)
    {
        var entity = await _awardRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"EmployeeAward {id} not found.");
        return entity;
    }

    public async Task<EmployeeAwardDto?> GetByIdAsync(Guid id)
    {
        var entity = await _awardRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<EmployeeAwardDto?> GetByAwardNumberAsync(Guid tenantId, string awardNumber)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await _awardRepo.GetByAwardNumberAsync(tenantId, awardNumber);
        return entity?.ToDto();
    }

    public async Task<EmployeeAwardDetailDto?> GetWithDetailsAsync(Guid id)
    {
        var entity = await _awardRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetAllAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _awardRepo.GetByTenantAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<PagedResult<EmployeeAwardSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? year = null, AwardStatus? status = null)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var all = await _awardRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(a => a.AwardNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (a.Employee != null && (a.Employee.FirstName + " " + a.Employee.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        // Filter by year via nomination if needed
        if (year.HasValue)
            q = q.Where(a => a.AwardNomination != null && a.AwardNomination.Year == year.Value);

        // Status filter removed - EmployeeAward doesn't have Status property
        // Award status is now tracked through the nomination workflow

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<EmployeeAwardSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var awards = await _awardRepo.GetByEmployeeIdAsync(employeeId);
        return awards.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var tenantId = GetTenantId();
        var awards = await _awardRepo.GetByAwardTypeIdAsync(awardTypeId);
        return awards.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByAwardLevelIdAsync(Guid awardLevelId)
    {
        var tenantId = GetTenantId();
        var awards = await _awardRepo.GetByAwardLevelIdAsync(awardLevelId);
        return awards.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var tenantId = GetTenantId();
        var awards = await _awardRepo.GetByNominationIdAsync(nominationId);
        return awards.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _awardRepo.GetByDateRangeAsync(tenantId, startDate, endDate);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingPresentationsAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _awardRepo.GetPendingPresentationsAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingPaymentsAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _awardRepo.GetPendingPaymentsAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingLeaveProcessingAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _awardRepo.GetPendingLeaveProcessingAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<EmployeeAwardDto> CreateAsync(Guid tenantId, Guid userId, CreateEmployeeAwardDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awardNumber = $"AWD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var entity = dto.ToEntity(tenantId, userId, awardNumber);
        await _awardRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _awardRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<EmployeeAwardDto> CreateFromNominationAsync(Guid nominationId, Guid userId, CreateEmployeeAwardFromNominationDto dto)
    {
        var tenantId = GetTenantId();
        var nomination = await _nominationRepo.GetWithDetailsAsync(nominationId);
        if (nomination == null || nomination.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardNomination {nominationId} not found.");

        var awardNumber = $"AWD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var entity = dto.ToEntity(nomination, tenantId, userId, awardNumber);
        await _awardRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _awardRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<EmployeeAwardDto> UpdateAsync(Guid id, Guid userId, UpdateEmployeeAwardDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _awardRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _awardRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SchedulePresentationAsync(Guid userId, ScheduleAwardPresentationDto dto)
    {
        var entity = await GetOwnedAsync(dto.AwardId);

        entity.PresentationDate = dto.PresentationDate;
        entity.PresentationVenue = dto.PresentationVenue;
        entity.PresentedById = dto.PresentedById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RecordPresentationAsync(Guid id, Guid userId, RecordAwardPresentationDto dto)
    {
        var entity = await GetOwnedAsync(id);

        entity.PresentationDate = dto.PresentationDate;
        entity.PresentationVenue = dto.PresentationVenue;
        entity.CertificateIssued = dto.CertificateIssued;
        entity.TrophyIssued = dto.TrophyIssued;
        entity.CertificateNumber = dto.CertificateNumber;
        entity.PublicationNotes = dto.PresentationNotes; // Store presentation notes in PublicationNotes
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ProcessPaymentAsync(Guid userId, ProcessAwardPaymentDto dto)
    {
        var entity = await GetOwnedAsync(dto.AwardId);

        entity.PaymentProcessed = true;
        entity.PaymentDate = DateTime.UtcNow;
        entity.PaymentReference = dto.PaymentReference;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ProcessLeaveAsync(Guid id, Guid userId)
    {
        var entity = await GetOwnedAsync(id);

        entity.LeaveProcessed = true;
        entity.LeaveProcessedDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardAttachmentService : IAwardAttachmentService
{
    private readonly IAwardAttachmentRepository _attachmentRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardAttachmentService(
        IAwardAttachmentRepository attachmentRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _attachmentRepo = attachmentRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardAttachment> GetOwnedAsync(Guid id)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardAttachment {id} not found.");
        return entity;
    }

    public async Task<AwardAttachmentDto?> GetByIdAsync(Guid id)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<AwardAttachmentDto>> GetByAwardIdAsync(Guid awardId)
    {
        var tenantId = GetTenantId();
        var attachments = await _attachmentRepo.GetByAwardIdAsync(awardId);
        return attachments.Where(a => a.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<AwardAttachmentDto>> GetByTypeAsync(Guid awardId, AwardAttachmentType type)
    {
        var tenantId = GetTenantId();
        var attachments = await _attachmentRepo.GetByTypeAsync(awardId, type);
        return attachments.Where(a => a.TenantId == tenantId).ToDtoList();
    }

    public async Task<AwardAttachmentDto> CreateAsync(Guid tenantId, Guid awardId, Guid userId, CreateAwardAttachmentDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, awardId, userId);
        await _attachmentRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _attachmentRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Award Nomination Services

public class AwardNominationService : IAwardNominationService
{
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IEmployeeAwardRepository _awardRepo;
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly IAwardCycleRepository _cycleRepo;
    private readonly IAwardEligibilityEvaluator _eligibility;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardNominationService(
        IAwardNominationRepository nominationRepo,
        IEmployeeAwardRepository awardRepo,
        IAwardTypeRepository awardTypeRepo,
        IAwardCycleRepository cycleRepo,
        IAwardEligibilityEvaluator eligibility,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _nominationRepo = nominationRepo;
        _awardRepo = awardRepo;
        _awardTypeRepo = awardTypeRepo;
        _cycleRepo = cycleRepo;
        _eligibility = eligibility;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardNomination> GetOwnedAsync(Guid id)
    {
        var entity = await _nominationRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardNomination {id} not found.");
        return entity;
    }

    public async Task<AwardNominationDto?> GetByIdAsync(Guid id)
    {
        var entity = await _nominationRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardNominationDto?> GetByNominationNumberAsync(Guid tenantId, string nominationNumber)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await _nominationRepo.GetByNominationNumberAsync(tenantId, nominationNumber);
        return entity?.ToDto();
    }

    public async Task<AwardNominationDetailDto?> GetWithDetailsAsync(Guid id)
    {
        var entity = await _nominationRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetAllAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var nominations = await _nominationRepo.GetByTenantAsync(tenantId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<PagedResult<AwardNominationSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? year = null, AwardNominationStatus? status = null)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var all = await _nominationRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(n => n.NominationNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (n.Nominee != null && (n.Nominee.FirstName + " " + n.Nominee.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (year.HasValue)
            q = q.Where(n => n.Year == year.Value);

        if (status.HasValue)
            q = q.Where(n => n.Status == status.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AwardNominationSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByNomineeIdAsync(Guid nomineeId)
    {
        var tenantId = GetTenantId();
        var nominations = await _nominationRepo.GetByNomineeIdAsync(nomineeId);
        return nominations.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByNominatedByIdAsync(Guid nominatedById)
    {
        var tenantId = GetTenantId();
        var nominations = await _nominationRepo.GetByNominatedByIdAsync(nominatedById);
        return nominations.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var tenantId = GetTenantId();
        var nominations = await _nominationRepo.GetByAwardTypeIdAsync(awardTypeId);
        return nominations.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByYearAsync(Guid tenantId, int year)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var nominations = await _nominationRepo.GetByYearAsync(tenantId, year);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByYearAndPeriodAsync(Guid tenantId, int year, int? quarter = null, int? month = null)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var nominations = await _nominationRepo.GetByYearAndPeriodAsync(tenantId, year, quarter, month);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByStatusAsync(Guid tenantId, AwardNominationStatus status)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var nominations = await _nominationRepo.GetByStatusAsync(tenantId, status);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByCommitteeIdAsync(Guid committeeId)
    {
        var tenantId = GetTenantId();
        var nominations = await _nominationRepo.GetByCommitteeIdAsync(committeeId);
        return nominations.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetRequiringCommitteeReviewAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var nominations = await _nominationRepo.GetRequiringCommitteeReviewAsync(tenantId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetApprovedWithoutAwardAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var nominations = await _nominationRepo.GetApprovedWithoutAwardAsync(tenantId);
        return nominations.ToSummaryDtoList();
    }

    /// <summary>
    /// Checks that a nomination is one this award will actually accept, right now, for this person.
    /// </summary>
    /// <remarks>
    /// <para>Before slice 4 a nomination was a row: any award type, any nominee, at any time. TDC's
    /// note describes a sequence - HR sets the criteria, the criteria qualify some employees, those
    /// employees are nominated during a window, and the result is voted on. Each gate below is one
    /// step of that sequence refusing to be skipped.</para>
    ///
    /// <para><b>Not gated: nominating yourself.</b> The note does not say whether an employee may
    /// put their own name forward, and this refuses to invent a rule TDC has not stated. Self
    /// nomination is therefore accepted, the harness asserts that it is, and the question is in
    /// <c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c>. Making the position visible is the point: a silent
    /// choice either way would be a decision nobody took.</para>
    /// </remarks>
    private async Task ValidateNominationAsync(Guid tenantId, Guid nominatedById, CreateAwardNominationDto dto)
    {
        var awardType = await _awardTypeRepo.GetByIdAsync(dto.AwardTypeId);
        if (awardType == null || awardType.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardType {dto.AwardTypeId} not found.");

        // 1. An award taken by direct management selection has no nomination stage to join.
        if (awardType.NominationSource == AwardNominationSource.ManagementDirect)
            throw AwardsWorkflowException.InvalidState(
                $"'{awardType.Name}' is awarded by direct management selection, so it does not accept " +
                "nominations. Confer the award directly instead.");

        // 2. The cycle is what says whether nominations are open. Without one there is no window,
        //    no closing date, and nothing for a vote to be held against.
        if (dto.AwardCycleId == null)
            throw AwardsWorkflowException.Invalid(
                $"'{awardType.Name}' is nominated for in cycles, so a nomination must name the cycle " +
                "it belongs to. Set AwardCycleId to a cycle that is open for nomination.");

        var cycle = await _cycleRepo.GetByIdAsync(dto.AwardCycleId.Value);
        if (cycle == null || cycle.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardCycle {dto.AwardCycleId} not found.");

        if (cycle.AwardTypeId != dto.AwardTypeId)
            throw AwardsWorkflowException.Invalid(
                $"Cycle '{cycle.Name}' belongs to a different award. Nominate into a cycle of " +
                $"'{awardType.Name}'.");

        // 3. The window, decided by the clock rather than by a stored flag.
        var now = DateTime.UtcNow;
        if (cycle.Status != AwardCycleStatus.Published)
            throw AwardsWorkflowException.InvalidState(
                $"Cycle '{cycle.Name}' is {cycle.Status} and is not open to nominations.");

        if (cycle.NominationOpensOn == null || cycle.NominationClosesOn == null)
            throw AwardsWorkflowException.InvalidState(
                $"Cycle '{cycle.Name}' has no nomination window.");

        if (now < cycle.NominationOpensOn)
            throw AwardsWorkflowException.InvalidState(
                $"Nominations for '{cycle.Name}' open on {cycle.NominationOpensOn:yyyy-MM-dd HH:mm}.");

        if (now > cycle.NominationClosesOn)
            throw AwardsWorkflowException.InvalidState(
                $"Nominations for '{cycle.Name}' closed on {cycle.NominationClosesOn:yyyy-MM-dd HH:mm}.");

        // 4. A team nomination names a team; an individual one names an eligible person.
        if (dto.NomineeId == null)
        {
            if (!awardType.IsTeamAward)
                throw AwardsWorkflowException.Invalid(
                    $"'{awardType.Name}' is not a team award, so the nomination must name a nominee.");
            if (string.IsNullOrWhiteSpace(dto.TeamName))
                throw AwardsWorkflowException.Invalid("A team nomination must name the team.");
            return;
        }

        // 5. Nominating yourself, where the award permits it.
        //
        //    Off by default because that is the enterprise norm: peer or manager nomination is the
        //    usual rule, and self-nomination is granted per award — to innovation and improvement
        //    awards, where the achievement is one the nominee can evidence, rather than to
        //    behavioural awards, where being chosen by somebody else is the substance of the award.
        //    An award decided by a staff vote is the strongest case for barring it.
        if (dto.NomineeId == nominatedById && !awardType.AllowSelfNomination)
            throw AwardsWorkflowException.Invalid(
                $"'{awardType.Name}' does not accept self-nomination — somebody else has to put you " +
                "forward. If this award should allow it, ask HR to enable self-nomination on the " +
                "award type.");

        // 6. The criteria HR configured decide who may be put forward. This is the step TDC's note
        //    calls qualifying employees, and until slice 3 nothing consulted it.
        var verdict = await _eligibility.EvaluateEmployeeAsync(dto.AwardTypeId, dto.NomineeId.Value, tenantId, now);
        if (!verdict.IsEligible)
        {
            var who = string.IsNullOrWhiteSpace(verdict.EmployeeName) ? "That employee" : verdict.EmployeeName;
            throw AwardsWorkflowException.Invalid(
                $"{who} is not eligible for '{awardType.Name}': {string.Join(" ", verdict.Reasons)}");
        }

        // 7. One nominator, one nomination per person per cycle. Several colleagues nominating the
        //    same person is normal and stays allowed; the same person doing it twice is not.
        var existing = await _nominationRepo.GetByNomineeIdAsync(dto.NomineeId.Value);
        if (existing.Any(n => !n.IsDeleted
            && n.TenantId == tenantId
            && n.AwardCycleId == dto.AwardCycleId
            && n.NominatedById == nominatedById))
        {
            throw AwardsWorkflowException.Conflict(
                $"You have already nominated {verdict.EmployeeName} for '{cycle.Name}'.");
        }
    }

    public async Task WithdrawOwnAsync(Guid id, Guid employeeId)
    {
        var entity = await GetOwnedAsync(id);

        // Ownership is checked here as well as in the controller. The controller's check turns
        // somebody else's nomination into a 404 so the surface cannot be used to enumerate ids;
        // this one is the rule itself, and it must hold for any caller of the service.
        if (entity.NominatedById != employeeId)
            throw AwardsWorkflowException.NotFound($"AwardNomination {id} not found.");

        if (entity.Status != AwardNominationStatus.Draft)
            throw AwardsWorkflowException.InvalidState(
                $"This nomination has been {entity.Status.ToString().ToLowerInvariant()} and can no longer be " +
                "withdrawn. Ask the awards desk to withdraw it for you.");

        await _nominationRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<AwardNominationDto> CreateAsync(Guid tenantId, Guid nominatedById, Guid userId, CreateAwardNominationDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await ValidateNominationAsync(tenantId, nominatedById, dto);

        var nominationNumber = $"NOM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var entity = dto.ToEntity(tenantId, nominatedById, userId, nominationNumber);
        await _nominationRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _nominationRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AwardNominationDto> UpdateAsync(Guid id, Guid userId, UpdateAwardNominationDto dto)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != AwardNominationStatus.Draft)
            throw AwardsWorkflowException.InvalidState("Only draft nominations can be updated.");

        entity.UpdateEntity(dto, userId);
        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _nominationRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<AwardNominationDto> SubmitAsync(Guid id, Guid userId)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != AwardNominationStatus.Draft)
            throw AwardsWorkflowException.InvalidState("Only draft nominations can be submitted.");

        entity.Status = AwardNominationStatus.Submitted;
        entity.NominationDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task<AwardNominationDto> AssignToCommitteeAsync(Guid id, Guid committeeId, Guid userId)
    {
        var entity = await GetOwnedAsync(id);

        entity.CommitteeId = committeeId;
        entity.Status = AwardNominationStatus.UnderReview;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task<AwardNominationDto> SetOutcomeAsync(Guid id, Guid userId, SetNominationOutcomeDto dto)
    {
        var entity = await GetOwnedAsync(id);

        entity.Status = dto.Status;
        entity.OutcomeDate = DateTime.UtcNow;
        entity.OutcomeReason = dto.OutcomeReason;
        entity.AwardLevelId = dto.AwardLevelId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }
}

public class TeamAwardNomineeService : ITeamAwardNomineeService
{
    private readonly ITeamAwardNomineeRepository _nomineeRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public TeamAwardNomineeService(
        ITeamAwardNomineeRepository nomineeRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _nomineeRepo = nomineeRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<TeamAwardNominee> GetOwnedAsync(Guid id)
    {
        var entity = await _nomineeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"TeamAwardNominee {id} not found.");
        return entity;
    }

    public async Task<IEnumerable<TeamAwardNomineeDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var tenantId = GetTenantId();
        var nominees = await _nomineeRepo.GetByNominationIdAsync(nominationId);
        return nominees.Where(n => n.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<TeamAwardNomineeDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var nominees = await _nomineeRepo.GetByEmployeeIdAsync(employeeId);
        return nominees.Where(n => n.TenantId == tenantId).ToDtoList();
    }

    public async Task<TeamAwardNomineeDto> AddAsync(Guid nominationId, Guid employeeId, Guid userId, CreateTeamAwardNomineeDto dto)
    {
        var entity = dto.ToEntity(nominationId, employeeId, userId);
        entity.TenantId = GetTenantId();
        await _nomineeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        // Re-read before mapping. The entity we just constructed has no Employee navigation loaded,
        // and the mapper renders an unloaded navigation as an empty string rather than failing — so
        // the write response came back with employeeName: "" while a subsequent read resolved it
        // correctly. A screen that renders the create response therefore showed a blank row that
        // fixed itself on refresh, which reads as a UI bug and is not one.
        var saved = await _nomineeRepo.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateTeamAwardNomineeDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _nomineeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _nomineeRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardNomineeContributionService : IAwardNomineeContributionService
{
    private readonly IAwardNomineeContributionRepository _contributionRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardNomineeContributionService(
        IAwardNomineeContributionRepository contributionRepo,
        IAwardNominationRepository nominationRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _contributionRepo = contributionRepo;
        _nominationRepo = nominationRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardNomineeContribution> GetOwnedAsync(Guid id)
    {
        var entity = await _contributionRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardNomineeContribution {id} not found.");
        return entity;
    }

    public async Task<IEnumerable<AwardNomineeContributionDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var tenantId = GetTenantId();
        var contributions = await _contributionRepo.GetByNominationIdAsync(nominationId);
        return contributions.Where(c => c.TenantId == tenantId).ToDtoList();
    }

    public async Task<AwardNomineeContributionDto> AddAsync(Guid nominationId, Guid userId, CreateAwardNomineeContributionDto dto)
    {
        var tenantId = GetTenantId();
        var nomination = await _nominationRepo.GetByIdAsync(nominationId);
        if (nomination == null || nomination.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardNomination {nominationId} not found.");

        var entity = dto.ToEntity(tenantId, nominationId, userId);
        await _contributionRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateAwardNomineeContributionDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _contributionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _contributionRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardNominationAttachmentService : IAwardNominationAttachmentService
{
    private readonly IAwardNominationAttachmentRepository _attachmentRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardNominationAttachmentService(
        IAwardNominationAttachmentRepository attachmentRepo,
        IAwardNominationRepository nominationRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _attachmentRepo = attachmentRepo;
        _nominationRepo = nominationRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardNominationAttachment> GetOwnedAsync(Guid id)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardNominationAttachment {id} not found.");
        return entity;
    }

    public async Task<IEnumerable<AwardNominationAttachmentDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var tenantId = GetTenantId();
        var attachments = await _attachmentRepo.GetByNominationIdAsync(nominationId);
        return attachments.Where(a => a.TenantId == tenantId).ToDtoList();
    }

    public async Task<AwardNominationAttachmentDto> AddAsync(Guid nominationId, Guid uploadedById, Guid userId, CreateAwardNominationAttachmentDto dto)
    {
        var tenantId = GetTenantId();
        var nomination = await _nominationRepo.GetByIdAsync(nominationId);
        if (nomination == null || nomination.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardNomination {nominationId} not found.");

        var entity = dto.ToEntity(tenantId, nominationId, uploadedById, userId);
        await _attachmentRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateAwardNominationAttachmentDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _attachmentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _attachmentRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardCommitteeService : IAwardCommitteeService
{
    private readonly IAwardCommitteeRepository _committeeRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCommitteeService(
        IAwardCommitteeRepository committeeRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _committeeRepo = committeeRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardCommittee> GetOwnedAsync(Guid id)
    {
        var entity = await _committeeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardCommittee {id} not found.");
        return entity;
    }

    public async Task<AwardCommitteeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _committeeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardCommitteeDto?> GetWithMembersAsync(Guid id)
    {
        var entity = await _committeeRepo.GetWithMembersAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardCommitteeDto?> GetActiveForDateAsync(Guid tenantId, DateTime date)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await _committeeRepo.GetActiveForDateAsync(tenantId, date);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardCommitteeDto>> GetAllAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var committees = await _committeeRepo.GetByTenantAsync(tenantId);
        return committees.ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeDto>> GetActiveCommitteesAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var committees = await _committeeRepo.GetActiveCommitteesAsync(tenantId);
        return committees.ToDtoList();
    }

    public async Task<bool> HasQuorumAsync(Guid committeeId)
    {
        var entity = await _committeeRepo.GetByIdAsync(committeeId);
        if (entity == null || entity.TenantId != GetTenantId())
            return false;
        return await _committeeRepo.HasQuorumAsync(committeeId);
    }

    public async Task<AwardCommitteeDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardCommitteeDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _committeeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardCommitteeDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCommitteeDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _committeeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _committeeRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardCommitteeMemberService : IAwardCommitteeMemberService
{
    private readonly IAwardCommitteeMemberRepository _memberRepo;
    private readonly IAwardCommitteeRepository _committeeRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCommitteeMemberService(
        IAwardCommitteeMemberRepository memberRepo,
        IAwardCommitteeRepository committeeRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _memberRepo = memberRepo;
        _committeeRepo = committeeRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardCommitteeMember> GetOwnedAsync(Guid id)
    {
        var entity = await _memberRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardCommitteeMember {id} not found.");
        return entity;
    }

    public async Task<AwardCommitteeMemberDto?> GetByIdAsync(Guid id)
    {
        var entity = await _memberRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<AwardCommitteeMemberDto>> GetByCommitteeIdAsync(Guid committeeId)
    {
        var tenantId = GetTenantId();
        var members = await _memberRepo.GetByCommitteeIdAsync(committeeId);
        return members.Where(m => m.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeMemberDto>> GetActiveByCommitteeIdAsync(Guid committeeId)
    {
        var tenantId = GetTenantId();
        var members = await _memberRepo.GetActiveByCommitteeIdAsync(committeeId);
        return members.Where(m => m.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeMemberDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var members = await _memberRepo.GetByEmployeeIdAsync(employeeId);
        return members.Where(m => m.TenantId == tenantId).ToDtoList();
    }

    public async Task<bool> IsActiveMemberAsync(Guid committeeId, Guid employeeId)
    {
        var committee = await _committeeRepo.GetByIdAsync(committeeId);
        if (committee == null || committee.TenantId != GetTenantId())
            return false;
        return await _memberRepo.IsActiveMemberAsync(committeeId, employeeId);
    }

    public async Task<AwardCommitteeMemberDto> AddAsync(Guid committeeId, Guid userId, CreateAwardCommitteeMemberDto dto)
    {
        var entity = dto.ToEntity(committeeId, userId);
        entity.TenantId = GetTenantId();
        await _memberRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        // Re-read before mapping: ToDto renders names off navigations, and the entity we just
        // constructed has none loaded. See TeamAwardNomineeService.AddAsync.
        return (await _memberRepo.GetByIdAsync(entity.Id) ?? entity).ToDto();
    }

    public async Task<AwardCommitteeMemberDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCommitteeMemberDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _memberRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task RemoveAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _memberRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeactivateAsync(Guid id, Guid userId, DateTime? endDate = null)
    {
        var entity = await GetOwnedAsync(id);

        entity.IsActive = false;
        entity.EndDate = endDate ?? DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _memberRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardCommitteeReviewService : IAwardCommitteeReviewService
{
    private readonly IAwardNominationReviewRepository _reviewRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCommitteeReviewService(
        IAwardNominationReviewRepository reviewRepo,
        IAwardNominationRepository nominationRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _reviewRepo = reviewRepo;
        _nominationRepo = nominationRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<AwardNominationReview> GetOwnedAsync(Guid id)
    {
        var entity = await _reviewRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardNominationReview {id} not found.");
        return entity;
    }

    public async Task<AwardCommitteeReviewDto?> GetByIdAsync(Guid id)
    {
        var entity = await _reviewRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<AwardCommitteeReviewDto?> GetReviewAsync(Guid nominationId, Guid reviewerId)
    {
        var entity = await _reviewRepo.GetReviewAsync(nominationId, reviewerId);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<AwardCommitteeReviewDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var tenantId = GetTenantId();
        var reviews = await _reviewRepo.GetByNominationIdAsync(nominationId);
        return reviews.Where(r => r.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeReviewDto>> GetByReviewerIdAsync(Guid reviewerId)
    {
        var tenantId = GetTenantId();
        var reviews = await _reviewRepo.GetByReviewerIdAsync(reviewerId);
        return reviews.Where(r => r.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeReviewDto>> GetPendingReviewsAsync(Guid reviewerId)
    {
        var tenantId = GetTenantId();
        var reviews = await _reviewRepo.GetPendingReviewsAsync(reviewerId);
        return reviews.Where(r => r.TenantId == tenantId).ToDtoList();
    }

    public async Task<int> GetApprovalCountAsync(Guid nominationId)
    {
        var tenantId = GetTenantId();
        var nomination = await _nominationRepo.GetByIdAsync(nominationId);
        if (nomination == null || nomination.TenantId != tenantId)
            return 0;
        return await _reviewRepo.GetApprovalCountAsync(nominationId);
    }

    public async Task<int> GetRejectionCountAsync(Guid nominationId)
    {
        var tenantId = GetTenantId();
        var nomination = await _nominationRepo.GetByIdAsync(nominationId);
        if (nomination == null || nomination.TenantId != tenantId)
            return 0;
        return await _reviewRepo.GetRejectionCountAsync(nominationId);
    }

    public async Task<AwardCommitteeReviewDto> SubmitReviewAsync(Guid nominationId, Guid reviewerId, Guid userId, SubmitCommitteeReviewDto dto)
    {
        var tenantId = GetTenantId();

        // Check if review already exists
        var existing = await _reviewRepo.GetReviewAsync(nominationId, reviewerId);
        if (existing != null && existing.TenantId == tenantId)
            throw AwardsWorkflowException.Conflict($"Review already exists for nomination {nominationId} by reviewer {reviewerId}.");

        var nomination = await _nominationRepo.GetByIdAsync(nominationId);
        if (nomination == null || nomination.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardNomination {nominationId} not found.");

        var entity = dto.ToEntity(nominationId, reviewerId, tenantId, userId);
        await _reviewRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        // Re-read before mapping: ToDto renders names off navigations, and the entity we just
        // constructed has none loaded. See TeamAwardNomineeService.AddAsync.
        return (await _reviewRepo.GetByIdAsync(entity.Id) ?? entity).ToDto();
    }

    public async Task<AwardCommitteeReviewDto> UpdateReviewAsync(Guid id, Guid userId, UpdateCommitteeReviewDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _reviewRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }
}

#endregion

#region Long Service Award Services

public class LongServiceAwardService : ILongServiceAwardService
{
    private readonly ILongServiceAwardRepository _lsaRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public LongServiceAwardService(
        ILongServiceAwardRepository lsaRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _lsaRepo = lsaRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    private async Task<LongServiceAward> GetOwnedAsync(Guid id)
    {
        var entity = await _lsaRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"LongServiceAward {id} not found.");
        return entity;
    }

    public async Task<LongServiceAwardDto?> GetByIdAsync(Guid id)
    {
        var entity = await _lsaRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetAllAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _lsaRepo.GetByTenantAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<PagedResult<LongServiceAwardSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? yearsOfService = null)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var all = await _lsaRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(lsa => lsa.Employee != null && (lsa.Employee.FirstName + " " + lsa.Employee.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        if (yearsOfService.HasValue)
            q = q.Where(lsa => lsa.YearsOfService == yearsOfService.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<LongServiceAwardSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var awards = await _lsaRepo.GetByEmployeeIdAsync(employeeId);
        return awards.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetUpcomingMilestonesAsync(Guid tenantId, int daysAhead = 90)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _lsaRepo.GetUpcomingMilestonesAsync(tenantId, daysAhead);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetPendingProcessingAsync(Guid tenantId)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var awards = await _lsaRepo.GetPendingProcessingAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<LongServiceAwardDto> CreateAsync(Guid tenantId, Guid userId, CreateLongServiceAwardDto dto)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Check if award already exists for this employee and years (per-tenant)
        var existing = await _lsaRepo.GetByEmployeeAndYearsAsync(dto.EmployeeId, dto.YearsOfService);
        if (existing != null && existing.TenantId == tenantId)
            throw AwardsWorkflowException.Conflict($"Long service award for {dto.YearsOfService} years already exists for this employee.");

        var entity = dto.ToEntity(tenantId, userId);
        await _lsaRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _lsaRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<LongServiceAwardDto> UpdateAsync(Guid id, Guid userId, UpdateLongServiceAwardDto dto)
    {
        var entity = await GetOwnedAsync(id);
        entity.UpdateEntity(dto, userId);
        await _lsaRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _lsaRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _lsaRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ProcessAsync(Guid userId, ProcessLongServiceAwardDto dto)
    {
        var entity = await GetOwnedAsync(dto.AwardId);

        entity.IsProcessed = true;
        entity.ProcessedDate = DateTime.UtcNow;
        entity.PresentationDate = dto.PresentationDate;
        entity.PresentationNotes = dto.PresentationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _lsaRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion
