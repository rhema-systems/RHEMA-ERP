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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeRelieverService> _logger;

    public EmployeeRelieverService(
        IGenericRepository<EmployeeReliever> repository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeRelieverService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<EmployeeRelieverDto>> GetForEmployeeAsync(Guid employeeId, bool activeOnly = false)
    {
        var query = _repository.GetQueryable()
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.Position)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.OrganizationUnit)
            .Where(r => r.EmployeeId == employeeId);

        if (activeOnly)
            query = query.Where(r => r.IsActive);

        var items = await query.OrderBy(r => r.Priority).ToListAsync();
        return items.Select(ToDto);
    }

    public async Task<EmployeeRelieverDto> CreateAsync(CreateEmployeeRelieverDto dto)
    {
        if (dto.RelieverEmployeeId == dto.EmployeeId)
            throw new InvalidOperationException("An employee cannot be their own reliever.");

        var clash = await _repository.GetQueryable()
            .AnyAsync(r => r.EmployeeId == dto.EmployeeId && r.Priority == dto.Priority);
        if (clash)
            throw new InvalidOperationException($"A priority-{dto.Priority} reliever is already set for this employee.");

        var entity = new EmployeeReliever
        {
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
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Reliever setup '{id}' not found.");

        if (dto.RelieverEmployeeId == entity.EmployeeId)
            throw new InvalidOperationException("An employee cannot be their own reliever.");

        if (dto.Priority != entity.Priority)
        {
            var clash = await _repository.GetQueryable()
                .AnyAsync(r => r.EmployeeId == entity.EmployeeId && r.Priority == dto.Priority && r.Id != id);
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
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Reliever setup '{id}' not found.");
        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<EmployeeRelieverDto> ReloadAsync(Guid id)
    {
        var entity = await _repository.GetQueryable()
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.Position)
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.OrganizationUnit)
            .FirstAsync(r => r.Id == id);
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
