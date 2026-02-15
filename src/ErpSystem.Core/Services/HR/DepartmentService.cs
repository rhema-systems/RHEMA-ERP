using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for department operations
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<DepartmentService> _logger;

    public DepartmentService(
        IDepartmentRepository departmentRepository,
        ILogger<DepartmentService> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<DepartmentDto?> GetByIdAsync(Guid id)
    {
        var department = await _departmentRepository.GetByIdAsync(id);
        return department == null ? null : MapToDto(department);
    }

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        var departments = await _departmentRepository.GetAllAsync();
        return departments.Select(MapToDto);
    }

    public async Task<IEnumerable<DepartmentDto>> GetActiveDepartmentsAsync()
    {
        var departments = await _departmentRepository.GetActiveDepartmentsAsync();
        return departments.Select(MapToDto);
    }

    public async Task<IEnumerable<DepartmentDto>> GetRootDepartmentsAsync()
    {
        var departments = await _departmentRepository.GetRootDepartmentsAsync();
        return departments.Select(MapToDto);
    }

    public async Task<IEnumerable<DepartmentDto>> GetSubDepartmentsAsync(Guid parentDepartmentId)
    {
        var departments = await _departmentRepository.GetSubDepartmentsAsync(parentDepartmentId);
        return departments.Select(MapToDto);
    }

    public async Task<DepartmentDto?> GetByCodeAsync(string code)
    {
        var department = await _departmentRepository.GetByCodeAsync(code);
        return department == null ? null : MapToDto(department);
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto createDto)
    {
        var department = new Department
        {
            Id = Guid.NewGuid(),
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
        var department = await _departmentRepository.GetByIdAsync(id);
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
        var department = await _departmentRepository.GetByIdAsync(id);
        if (department == null)
            return false;

        department.IsDeleted = true;
        await _departmentRepository.UpdateAsync(department);
        _logger.LogInformation("Deleted department: {Code} - {Name}", department.Code, department.Name);

        return true;
    }

    public async Task<bool> CodeExistsAsync(string code)
    {
        return await _departmentRepository.CodeExistsAsync(code);
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

