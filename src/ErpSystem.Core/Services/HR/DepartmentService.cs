using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for department operations
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<DepartmentService> _logger;

    public DepartmentService(
        IDepartmentRepository departmentRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<DepartmentService> logger)
    {
        _departmentRepository = departmentRepository;
        _currentUserProvider = currentUserProvider;
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

    private async Task<Department?> GetOwnedOrNullAsync(Guid id)
    {
        var department = await _departmentRepository.GetByIdAsync(id);
        if (department == null || department.TenantId != GetTenantId())
            return null;
        return department;
    }

    public async Task<DepartmentDto?> GetByIdAsync(Guid id)
    {
        var department = await GetOwnedOrNullAsync(id);
        return department == null ? null : MapToDto(department);
    }

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var departments = await _departmentRepository.GetQueryable(d => d.TenantId == tenantId).ToListAsync();
        return departments.Select(MapToDto);
    }

    public async Task<IEnumerable<DepartmentDto>> GetActiveDepartmentsAsync()
    {
        var tenantId = GetTenantId();
        var departments = await _departmentRepository.GetActiveDepartmentsAsync();
        return departments.Where(d => d.TenantId == tenantId).Select(MapToDto);
    }

    public async Task<IEnumerable<DepartmentDto>> GetRootDepartmentsAsync()
    {
        var tenantId = GetTenantId();
        var departments = await _departmentRepository.GetRootDepartmentsAsync();
        return departments.Where(d => d.TenantId == tenantId).Select(MapToDto);
    }

    public async Task<IEnumerable<DepartmentDto>> GetSubDepartmentsAsync(Guid parentDepartmentId)
    {
        var tenantId = GetTenantId();
        var parent = await GetOwnedOrNullAsync(parentDepartmentId);
        if (parent == null)
            return Enumerable.Empty<DepartmentDto>();

        var departments = await _departmentRepository.GetSubDepartmentsAsync(parentDepartmentId);
        return departments.Where(d => d.TenantId == tenantId).Select(MapToDto);
    }

    public async Task<DepartmentDto?> GetByCodeAsync(string code)
    {
        var tenantId = GetTenantId();
        var department = await _departmentRepository.GetByCodeAsync(code);
        return department != null && department.TenantId == tenantId ? MapToDto(department) : null;
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto createDto)
    {
        var tenantId = GetTenantId();

        var department = new Department
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = createDto.Name,
            Code = createDto.Code,
            Description = createDto.Description,
            DepartmentType = createDto.DepartmentType,
            ParentDepartmentId = createDto.ParentDepartmentId,
            DepartmentHeadId = createDto.DepartmentHeadId,
            Budget = createDto.Budget,
            Color = createDto.Color,
            Icon = createDto.Icon,
            IsActive = true
        };

        await _departmentRepository.AddAsync(department);
        _logger.LogInformation("Created department: {Code} - {Name}", department.Code, department.Name);

        return MapToDto(department);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, CreateDepartmentDto updateDto)
    {
        var department = await GetOwnedOrNullAsync(id);
        if (department == null)
            throw new ArgumentException($"Department with ID {id} not found");

        department.Name = updateDto.Name;
        department.Code = updateDto.Code;
        department.Description = updateDto.Description;
        department.DepartmentType = updateDto.DepartmentType;
        department.ParentDepartmentId = updateDto.ParentDepartmentId;
        department.DepartmentHeadId = updateDto.DepartmentHeadId;
        department.Budget = updateDto.Budget;
        department.Color = updateDto.Color;
        department.Icon = updateDto.Icon;

        await _departmentRepository.UpdateAsync(department);
        _logger.LogInformation("Updated department: {Code} - {Name}", department.Code, department.Name);

        return MapToDto(department);
    }

    public async Task<bool> DeleteDepartmentAsync(Guid id)
    {
        var department = await GetOwnedOrNullAsync(id);
        if (department == null)
            return false;

        department.IsDeleted = true;
        await _departmentRepository.UpdateAsync(department);
        _logger.LogInformation("Deleted department: {Code} - {Name}", department.Code, department.Name);

        return true;
    }

    public async Task<bool> CodeExistsAsync(string code)
    {
        var tenantId = GetTenantId();
        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        return await _departmentRepository.GetQueryable()
            .AnyAsync(d => d.TenantId == tenantId && d.Code == code);
    }

    private static DepartmentDto MapToDto(Department department)
    {
        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Code = department.Code,
            Description = department.Description,
            DepartmentType = department.DepartmentType,
            ParentDepartmentId = department.ParentDepartmentId,
            ParentDepartmentName = department.ParentDepartment?.Name,
            DepartmentHeadId = department.DepartmentHeadId,
            DepartmentHeadName = department.DepartmentHead?.FullName,
            Budget = department.Budget,
            IsActive = department.IsActive,
            Color = department.Color,
            Icon = department.Icon,
            EmployeeCount = department.Employees?.Count ?? 0,
            SectionCount = department.Sections?.Count ?? 0
        };
    }
}
