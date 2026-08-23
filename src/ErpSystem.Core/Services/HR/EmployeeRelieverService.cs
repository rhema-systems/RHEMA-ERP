using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Services.HR.Extensions;

namespace ErpSystem.Core.Services.HR;

public class EmployeeRelieverService : IEmployeeRelieverService
{
    private readonly IGenericRepository<EmployeeReliever> _repository;
    private readonly IGenericRepository<Employee> _employees;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeRelieverService> _logger;

    public EmployeeRelieverService(
        IGenericRepository<EmployeeReliever> repository,
        IGenericRepository<Employee> employees,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeRelieverService> logger)
    {
        _repository = repository;
        _employees = employees;
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

    /// <summary>
    /// Loads an employee that belongs to this tenant, or refuses.
    /// </summary>
    /// <remarks>
    /// ⚠ Nothing checked either id before slice 7. An id that did not exist reached the database and
    /// came back as a foreign-key violation — measured: <b>500</b>, no body, for both the employee
    /// and the reliever. And because the ids were never scoped, a caller could name <i>another
    /// tenant's</i> employee as a reliever: the row would be stamped with the caller's tenant while
    /// pointing at a stranger, and the roster would then render that stranger's name, position and
    /// unit. The FK cannot catch it — <c>Employees</c> is one table for every tenant.
    /// </remarks>
    private async Task<Employee> RequireEmployeeAsync(Guid id, Guid tenantId, string role)
    {
        var employee = await _employees.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        if (employee == null)
            throw new ArgumentException($"The {role} employee '{id}' was not found.");

        return employee;
    }

    /// <summary>
    /// Neither party to a cover arrangement may have left.
    /// </summary>
    /// <remarks>
    /// The same predicate the rest of HR uses for "is this person still with us"
    /// (<c>EmployeeService.cs:2385</c>), stated the way <c>TeamService</c> states it rather than
    /// restated afresh. A leaver cannot cover for anybody, and nobody needs cover arranged for a
    /// leaver — the roster feeds the leave form, and a leaver takes no leave.
    /// </remarks>
    private static void RequireOnStrength(Employee employee, string role)
    {
        if (!employee.IsActive || employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} has left the organisation and cannot be the {role}.");
    }

    /// <summary>
    /// The rules a roster row must satisfy, whichever way it is being written.
    /// </summary>
    /// <remarks>
    /// Shared by create and update deliberately: an update that skipped them would be a second door
    /// into the same store with different rules, which is the shape slice 3 found on the
    /// organisation-unit audit trail.
    /// </remarks>
    private async Task ValidateAsync(Guid tenantId, Guid employeeId, Guid relieverEmployeeId, int priority, Guid? excludeId)
    {
        if (relieverEmployeeId == employeeId)
            throw new InvalidOperationException("An employee cannot be their own reliever.");

        if (priority < 1)
            throw new InvalidOperationException("Priority must be 1 or greater — 1 is the primary reliever.");

        var subject = await RequireEmployeeAsync(employeeId, tenantId, "covered");
        var reliever = await RequireEmployeeAsync(relieverEmployeeId, tenantId, "reliever");
        RequireOnStrength(subject, "covered employee");
        RequireOnStrength(reliever, "reliever");

        var siblings = await _repository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.EmployeeId == employeeId
                        && (excludeId == null || r.Id != excludeId))
            .ToListAsync();

        // ⚠ Both of these read LIVE rows only, which is right and which is why D-9's filtered index
        // had to land with them: the service checks what a person can see, and the index must agree.
        // Before the migration the index counted soft-deleted rows the service could not, so a
        // released priority slot was refused by SQL with an opaque 500 rather than by the service
        // with a sentence.
        if (siblings.Any(r => r.Priority == priority))
            throw new InvalidOperationException(
                $"A priority-{priority} reliever is already set for this employee.");

        // One person, one slot. Listing the same reliever as both primary and backup describes a
        // roster with no backup at all.
        if (siblings.Any(r => r.RelieverEmployeeId == relieverEmployeeId))
            throw new InvalidOperationException(
                $"{reliever.FirstName} {reliever.LastName} is already a reliever for this employee.");
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
            .Include(r => r.Employee)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.Position)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.OrganizationUnit)
            .Where(r => r.TenantId == tenantId && r.EmployeeId == employeeId);

        if (activeOnly)
            query = query.Where(r => r.IsActive);

        // Priority is the roster's whole meaning — the leave form fills its first slot from row 1 and
        // its second from row 2 — so the order is never left to the engine. Id breaks a tie that the
        // filtered unique index should now make impossible, and costs nothing if it never fires.
        var items = await query.OrderBy(r => r.Priority).ThenBy(r => r.Id).ToListAsync();
        return items.Select(ToDto);
    }

    public async Task<EmployeeRelieverDto> CreateAsync(CreateEmployeeRelieverDto dto)
    {
        var tenantId = GetTenantId();
        await ValidateAsync(tenantId, dto.EmployeeId, dto.RelieverEmployeeId, dto.Priority, excludeId: null);

        var entity = new EmployeeReliever
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            RelieverEmployeeId = dto.RelieverEmployeeId,
            Priority = dto.Priority,
            IsActive = dto.IsActive
        };

        entity.StampCreated(_currentUserProvider);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Pre-defined reliever set for employee {EmployeeId} (priority {Priority})", dto.EmployeeId, dto.Priority);
        return await ReloadAsync(entity.Id);
    }

    public async Task<EmployeeRelieverDto> UpdateAsync(Guid id, UpdateEmployeeRelieverDto dto)
    {
        var entity = await GetOwnedAsync(id);

        // ⚠ Validated in full, not only when the priority moved. The old guard re-checked the clash
        // solely on a priority change, so an update could quietly install a leaver, a stranger from
        // another tenant, or a duplicate reliever as long as the number stayed put.
        await ValidateAsync(entity.TenantId, entity.EmployeeId, dto.RelieverEmployeeId, dto.Priority, excludeId: id);

        entity.RelieverEmployeeId = dto.RelieverEmployeeId;
        entity.Priority = dto.Priority;
        entity.IsActive = dto.IsActive;
        entity.StampUpdated(_currentUserProvider);

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await ReloadAsync(id);
    }

    public async Task<Guid> GetOwnerEmployeeIdAsync(Guid id)
        => (await GetOwnedAsync(id)).EmployeeId;

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
            .Include(r => r.Employee)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.Position)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.OrganizationUnit)
            .FirstAsync(r => r.TenantId == tenantId && r.Id == id);
        return ToDto(entity);
    }

    private static EmployeeRelieverDto ToDto(EmployeeReliever e) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        RelieverEmployeeId = e.RelieverEmployeeId,
        RelieverName = e.RelieverEmployee?.FullName ?? string.Empty,
        RelieverPositionName = e.RelieverEmployee?.Position?.Title,
        RelieverOrganizationUnitName = e.RelieverEmployee?.OrganizationUnit?.Name,
        Priority = e.Priority,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };
}
