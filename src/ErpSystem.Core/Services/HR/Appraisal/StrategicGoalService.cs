using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class StrategicGoalService : IStrategicGoalService
{
    private readonly IGenericRepository<StrategicGoal> _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StrategicGoalService> _logger;

    public StrategicGoalService(
        IGenericRepository<StrategicGoal> repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StrategicGoalService> logger)
    {
        _repository = repository;
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

    // A strategic goal owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<StrategicGoal> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Strategic goal with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<StrategicGoal> BaseQuery()
    {
        var tenantId = GetTenantId();
        return _repository.GetQueryable().Where(g => g.TenantId == tenantId);
    }

    public async Task<StrategicGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .Include(g => g.CompanyGoals)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Strategic goal with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<StrategicGoalDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Include(g => g.CompanyGoals).AsQueryable();

        if (activeOnly)
            query = query.Where(g => g.IsActive);

        var entities = await query
            .OrderByDescending(g => g.StartYear)
            .ThenBy(g => g.Priority)
            .ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<StrategicGoalDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Include(g => g.CompanyGoals)
            .OrderByDescending(g => g.StartYear)
            .ThenBy(g => g.Priority);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<StrategicGoalDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StrategicGoalDto> CreateAsync(CreateStrategicGoalDto createDto, CancellationToken cancellationToken = default)
    {
        if (createDto.EndYear < createDto.StartYear)
            throw new ArgumentException("End year cannot be earlier than start year.");

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic goal created: {Id} '{Title}'", entity.Id, entity.Title);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<StrategicGoalDto> UpdateAsync(UpdateStrategicGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        if (updateDto.EndYear < updateDto.StartYear)
            throw new ArgumentException("End year cannot be earlier than start year.");

        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic goal updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await BaseQuery()
            .Include(g => g.CompanyGoals)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Strategic goal with ID '{id}' not found.");

        if (entity.CompanyGoals.Any(c => c.TenantId == tenantId))
            throw new InvalidOperationException("Cannot delete a strategic goal that has yearly objectives derived from it. Detach or delete those first.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic goal deleted: {Id}", id);
        return true;
    }

    public async Task<bool> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        entity.IsActive = isActive;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic goal {Id} active status set to {Active}", id, isActive);
        return true;
    }
}
