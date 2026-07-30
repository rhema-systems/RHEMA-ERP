using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class GoalLibraryService : IGoalLibraryService
{
    private readonly IGenericRepository<GoalLibrary> _goalLibraryRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GoalLibraryService> _logger;

    public GoalLibraryService(
        IGenericRepository<GoalLibrary> goalLibraryRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<GoalLibraryService> logger)
    {
        _goalLibraryRepository = goalLibraryRepository;
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

    // A library item owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<GoalLibrary> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _goalLibraryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Goal library item with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<GoalLibrary> BaseQuery()
    {
        var tenantId = GetTenantId();
        return _goalLibraryRepository.GetQueryable().Where(g => g.TenantId == tenantId);
    }

    public async Task<GoalLibraryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .Include(g => g.OrganizationLevel)
            .Include(g => g.OrganizationUnit)
            .Include(g => g.Position)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Goal library item with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<GoalLibraryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Include(g => g.OrganizationLevel)
            .Include(g => g.OrganizationUnit)
            .Include(g => g.Position)
            .OrderBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<GoalLibraryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery()
            .Include(g => g.OrganizationLevel)
            .Include(g => g.OrganizationUnit)
            .Include(g => g.Position)
            .OrderBy(g => g.Title);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<GoalLibraryDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<GoalLibraryDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.PositionId == positionId && g.IsActive)
            .Include(g => g.Position)
            .OrderBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<GoalLibraryDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.OrganizationUnitId == orgUnitId && g.IsActive)
            .Include(g => g.OrganizationUnit)
            .OrderBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<GoalLibraryDto>> GetActiveItemsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.IsActive)
            .Include(g => g.OrganizationLevel)
            .Include(g => g.OrganizationUnit)
            .Include(g => g.Position)
            .OrderBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<GoalLibraryDto> CreateAsync(CreateGoalLibraryDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _goalLibraryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal library item created: {Id} '{Title}'", entity.Id, entity.Title);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<GoalLibraryDto> UpdateAsync(UpdateGoalLibraryDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _goalLibraryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal library item updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _goalLibraryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal library item deleted: {Id}", id);
        return true;
    }

    public async Task<bool> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        entity.IsActive = isActive;
        await _goalLibraryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal library item {Id} active status set to {Status}", id, isActive);
        return true;
    }

    public async Task<int> GetEmployeeGoalUsageCountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(id, cancellationToken);
        var tenantId = GetTenantId();
        var count = await BaseQuery()
            .Where(g => g.Id == id)
            .SelectMany(g => g.EmployeeGoals.Where(eg => eg.TenantId == tenantId))
            .CountAsync(cancellationToken);
        return count;
    }

    public async Task<GoalLibraryDetailsDto> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .Include(g => g.OrganizationLevel)
            .Include(g => g.OrganizationUnit)
            .Include(g => g.Position)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Goal library item with ID '{id}' not found.");

        var tenantId = GetTenantId();
        var usageQuery = BaseQuery()
            .Where(g => g.Id == id)
            .SelectMany(g => g.EmployeeGoals.Where(eg => eg.TenantId == tenantId));

        var totalGoals = await usageQuery.CountAsync(cancellationToken);
        var uniqueEmployees = await usageQuery.Select(eg => eg.EmployeeId).Distinct().CountAsync(cancellationToken);
        var cycleCount = await usageQuery.Select(eg => eg.AppraisalCycleId).Distinct().CountAsync(cancellationToken);

        var dto = entity.ToDto();
        return new GoalLibraryDetailsDto
        {
            Id = dto.Id,
            TenantId = dto.TenantId,
            Title = dto.Title,
            Description = dto.Description,
            SuccessCriteria = dto.SuccessCriteria,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationLevelName = dto.OrganizationLevelName,
            OrganizationUnitId = dto.OrganizationUnitId,
            OrganizationUnitName = dto.OrganizationUnitName,
            PositionId = dto.PositionId,
            PositionTitle = dto.PositionTitle,
            IsActive = dto.IsActive,
            TotalGoals = totalGoals,
            UniqueEmployees = uniqueEmployees,
            CycleCount = cycleCount
        };
    }

    public async Task<GoalLibraryUsageStatsDto> GetUsageStatsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(id, cancellationToken);
        var tenantId = GetTenantId();
        var usageQuery = BaseQuery()
            .Where(g => g.Id == id)
            .SelectMany(g => g.EmployeeGoals.Where(eg => eg.TenantId == tenantId));

        return new GoalLibraryUsageStatsDto
        {
            TotalGoals = await usageQuery.CountAsync(cancellationToken),
            UniqueEmployees = await usageQuery.Select(eg => eg.EmployeeId).Distinct().CountAsync(cancellationToken),
            CycleCount = await usageQuery.Select(eg => eg.AppraisalCycleId).Distinct().CountAsync(cancellationToken)
        };
    }

    public async Task<PagedResult<GoalLibrarySelectorDto>> GetSelectorPagedAsync(
        string? search,
        bool activeOnly,
        Guid? organizationLevelId,
        Guid? organizationUnitId,
        Guid? positionId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // ── Base query — AsNoTracking, no entity materialisation
        var query = BaseQuery().AsNoTracking();

        // ── Scope filters (passed from the consumer context; respects caller intent)
        if (activeOnly)
            query = query.Where(g => g.IsActive);

        if (organizationLevelId.HasValue)
            query = query.Where(g => g.OrganizationLevelId == organizationLevelId);

        if (organizationUnitId.HasValue)
            query = query.Where(g => g.OrganizationUnitId == organizationUnitId);

        if (positionId.HasValue)
            query = query.Where(g => g.PositionId == positionId);

        // ── Full-text search across title and description
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(g =>
                g.Title.ToLower().Contains(term) ||
                (g.Description != null && g.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // ── Direct IQueryable projection — EF Core auto-joins navigation props
        var items = await query
            .OrderBy(g => g.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GoalLibrarySelectorDto
            {
                Id = g.Id,
                Title = g.Title,
                Description = g.Description,
                SuccessCriteria = g.SuccessCriteria,
                IsActive = g.IsActive,
                ScopeSummary =
                    g.Position != null ? "Position: " + g.Position.Title :
                    g.OrganizationUnit != null ? "Unit: " + g.OrganizationUnit.Name :
                    g.OrganizationLevel != null ? "Level: " + g.OrganizationLevel.Name :
                    "Global"
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<GoalLibrarySelectorDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<GoalLibraryUsageRowDto>> GetUsagePagedAsync(Guid id, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(id, cancellationToken);
        var tenantId = GetTenantId();
        var usageQuery = BaseQuery()
            .Where(g => g.Id == id)
            .SelectMany(g => g.EmployeeGoals.Where(eg => eg.TenantId == tenantId))
            .Include(eg => eg.Employee)
            .Include(eg => eg.AppraisalCycle)
            .OrderBy(eg => eg.Employee.LastName)
            .ThenBy(eg => eg.Employee.FirstName);

        var totalCount = await usageQuery.CountAsync(cancellationToken);
        var items = await usageQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(eg => new GoalLibraryUsageRowDto
            {
                EmployeeGoalId = eg.Id,
                EmployeeName = eg.Employee.FirstName + " " + eg.Employee.LastName,
                AppraisalCycleName = eg.AppraisalCycle != null ? eg.AppraisalCycle.CycleName : string.Empty,
                GoalTitle = eg.Title,
                Status = eg.Status,
                ProgressPercent = eg.ProgressPercent
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<GoalLibraryUsageRowDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }
}
