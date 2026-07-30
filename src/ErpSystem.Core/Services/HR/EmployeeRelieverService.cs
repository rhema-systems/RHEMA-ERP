using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class EmployeeRelieverService : IEmployeeRelieverService
{
    private readonly IGenericRepository<EmployeeReliever> _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeRelieverService> _logger;

    public EmployeeRelieverService(
        IGenericRepository<EmployeeReliever> repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeRelieverService> logger)
    {
        _repository = repository;
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

    private async Task<EmployeeReliever> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Reliever setup '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<EmployeeRelieverDto>> GetForEmployeeAsync(Guid employeeId, bool activeOnly = false)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable()
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.Position)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.OrganizationUnit)
            .Where(r => r.TenantId == tenantId && r.EmployeeId == employeeId);

        if (activeOnly)
            query = query.Where(r => r.IsActive);

        var items = await query.OrderBy(r => r.Priority).ToListAsync();
        return items.Select(ToDto);
    }

    public async Task<EmployeeRelieverDto> CreateAsync(CreateEmployeeRelieverDto dto)
    {
        var tenantId = GetTenantId();

        if (dto.RelieverEmployeeId == dto.EmployeeId)
            throw new InvalidOperationException("An employee cannot be their own reliever.");

        var clash = await _repository.GetQueryable()
            .AnyAsync(r => r.TenantId == tenantId && r.EmployeeId == dto.EmployeeId && r.Priority == dto.Priority);
        if (clash)
            throw new InvalidOperationException($"A priority-{dto.Priority} reliever is already set for this employee.");

        var entity = new EmployeeReliever
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            RelieverEmployeeId = dto.RelieverEmployeeId,
            Priority = dto.Priority,
            IsActive = dto.IsActive
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Pre-defined reliever set for employee {EmployeeId} (priority {Priority})", dto.EmployeeId, dto.Priority);
        return await ReloadAsync(entity.Id);
    }

    public async Task<EmployeeRelieverDto> UpdateAsync(Guid id, UpdateEmployeeRelieverDto dto)
    {
        var entity = await GetOwnedAsync(id);

        if (dto.RelieverEmployeeId == entity.EmployeeId)
            throw new InvalidOperationException("An employee cannot be their own reliever.");

        if (dto.Priority != entity.Priority)
        {
            var clash = await _repository.GetQueryable()
                .AnyAsync(r => r.TenantId == entity.TenantId && r.EmployeeId == entity.EmployeeId && r.Priority == dto.Priority && r.Id != id);
            if (clash)
                throw new InvalidOperationException($"A priority-{dto.Priority} reliever is already set for this employee.");
        }

        entity.RelieverEmployeeId = dto.RelieverEmployeeId;
        entity.Priority = dto.Priority;
        entity.IsActive = dto.IsActive;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await ReloadAsync(id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAsync(id);
        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<EmployeeRelieverDto> ReloadAsync(Guid id)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.Position)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.OrganizationUnit)
            .FirstAsync(r => r.TenantId == tenantId && r.Id == id);
        return ToDto(entity);
    }

    private static EmployeeRelieverDto ToDto(EmployeeReliever e) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeId,
        RelieverEmployeeId = e.RelieverEmployeeId,
        RelieverName = e.RelieverEmployee?.FullName ?? string.Empty,
        RelieverPositionName = e.RelieverEmployee?.Position?.Title,
        RelieverOrganizationUnitName = e.RelieverEmployee?.OrganizationUnit?.Name,
        Priority = e.Priority,
        IsActive = e.IsActive
    };
}
