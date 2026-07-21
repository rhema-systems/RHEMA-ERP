using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Staff Level application service.
///
/// Responsibilities:
/// - Orchestrates Staff Level use cases
/// - Enforces tenant-scoped invariants (unique Code when provided)
/// - Applies safety checks (prevent delete when in use)
/// - Uses repositories for data access only
/// - Uses <see cref="IUnitOfWork"/> for transactional persistence
/// </summary>
public class StaffLevelService : IStaffLevelService
{
    private readonly IStaffLevelRepository _staffLevelRepository;
    private readonly IGenericRepository<EmployeePosition> _employeePositionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffLevelService> _logger;

    public StaffLevelService(
        IStaffLevelRepository staffLevelRepository,
        IGenericRepository<EmployeePosition> employeePositionRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffLevelService> logger)
    {
        _staffLevelRepository = staffLevelRepository;
        _employeePositionRepository = employeePositionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StaffLevelListDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _staffLevelRepository.GetAllOrderedByRankAsync(tenantId, cancellationToken);
        return items.Select(sl => sl.ToListDto()).ToList();
    }

    public async Task<IReadOnlyList<StaffLevelListDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = await _staffLevelRepository.GetActiveOrderedByRankAsync(tenantId, cancellationToken);
        return items.Select(sl => sl.ToListDto()).ToList();
    }

    public async Task<StaffLevelDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _staffLevelRepository
            .GetQueryable(sl => sl.TenantId == tenantId && sl.Id == id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Staff level with ID '{id}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<StaffLevelDto> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Code is required.");
        }

        var entity = await _staffLevelRepository.GetByCodeAsync(tenantId, normalized, cancellationToken);
        if (entity == null)
        {
            throw new ArgumentException($"Staff level with code '{normalized}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<StaffLevelDetailDto> GetDetailAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _staffLevelRepository
            .GetQueryable(sl => sl.TenantId == tenantId && sl.Id == id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Staff level with ID '{id}' not found.");
        }

        var positionCount = await _employeePositionRepository
            .GetQueryable(ep => ep.TenantId == tenantId && ep.StaffLevelId == id)
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var dto = entity.ToDetailDto();
        dto.EmployeePositionCount = positionCount;
        return dto;
    }

    public async Task<StaffLevelDto> CreateAsync(Guid tenantId, CreateStaffLevelDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var normalizedCode = NormalizeCode(dto.Code);
        var normalizedName = NormalizeName(dto.Name);

        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            if (await _staffLevelRepository.CodeExistsAsync(tenantId, normalizedCode, excludeId: null, cancellationToken))
            {
                throw new InvalidOperationException($"A staff level with code '{normalizedCode}' already exists.");
            }
        }

        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.Name = normalizedName;
        entity.Code = normalizedCode;

        await _staffLevelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff level created: {StaffLevelId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<StaffLevelDto> UpdateAsync(Guid tenantId, UpdateStaffLevelDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _staffLevelRepository
            .GetQueryable(sl => sl.TenantId == tenantId && sl.Id == dto.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Staff level with ID '{dto.Id}' not found.");
        }

        var normalizedCode = NormalizeCode(dto.Code);
        var normalizedName = NormalizeName(dto.Name);

        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            if (await _staffLevelRepository.CodeExistsAsync(tenantId, normalizedCode, excludeId: dto.Id, cancellationToken))
            {
                throw new InvalidOperationException($"A staff level with code '{normalizedCode}' already exists.");
            }
        }

        dto.UpdateEntity(entity);
        entity.Name = normalizedName;
        entity.Code = normalizedCode;

        await _staffLevelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff level updated: {StaffLevelId}", entity.Id);

        return entity.ToDto();
    }

    public Task<StaffLevelDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => SetActiveAsync(tenantId, id, isActive: true, cancellationToken);

    public Task<StaffLevelDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => SetActiveAsync(tenantId, id, isActive: false, cancellationToken);

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _staffLevelRepository
            .GetQueryable(sl => sl.TenantId == tenantId && sl.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Staff level with ID '{id}' not found.");
        }

        var inUse = await _staffLevelRepository.IsInUseAsync(tenantId, id, cancellationToken);
        if (inUse)
        {
            throw new InvalidOperationException("Cannot delete this staff level because it is assigned to one or more employee positions.");
        }

        await _staffLevelRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff level deleted (soft): {StaffLevelId}", id);

        return true;
    }

    private async Task<StaffLevelDto> SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var entity = await _staffLevelRepository
            .GetQueryable(sl => sl.TenantId == tenantId && sl.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Staff level with ID '{id}' not found.");
        }

        if (entity.IsActive != isActive)
        {
            entity.IsActive = isActive;
            await _staffLevelRepository.UpdateAsync(entity);
        }

        // Idempotent: if already in desired state, SaveChanges will be a no-op.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff level active status set: {StaffLevelId} -> {IsActive}", id, isActive);

        return entity.ToDto();
    }

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim();

    private static string NormalizeName(string? name)
        => (name ?? string.Empty).Trim();
}
