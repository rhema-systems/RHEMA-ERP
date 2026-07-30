using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingProgramGroupService : ITrainingProgramGroupService
{
    private readonly IGenericRepository<TrainingProgramGroup> _groupRepository;
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingProgramGroupService> _logger;

    public TrainingProgramGroupService(
        IGenericRepository<TrainingProgramGroup> groupRepository,
        IGenericRepository<TrainingProgram> programRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingProgramGroupService> logger)
    {
        _groupRepository = groupRepository;
        _programRepository = programRepository;
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

    // A group owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<TrainingProgramGroup> GetOwnedAsync(Guid id)
    {
        var entity = await _groupRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training program group with ID '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<TrainingProgramGroupDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _groupRepository.GetQueryable().Where(g => g.TenantId == tenantId);
        if (activeOnly)
            query = query.Where(g => g.IsActive);

        var groups = await query
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var counts = await _programRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId && p.ProgramGroupId != null)
            .GroupBy(p => p.ProgramGroupId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dtos = groups.Select(g => g.ToDto()).ToList();
        foreach (var dto in dtos)
            dto.ProgramsCount = counts.FirstOrDefault(c => c.GroupId == dto.Id)?.Count ?? 0;

        return dtos;
    }

    public async Task<TrainingProgramGroupDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<TrainingProgramGroupDto> CreateAsync(CreateTrainingProgramGroupDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        var duplicate = await _groupRepository.GetQueryable()
            .AnyAsync(g => g.TenantId == current && g.Code == dto.Code, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A training program group with code '{dto.Code}' already exists.");

        var entity = dto.ToEntity(current, createdByUserId);
        await _groupRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program group created: {Code} - {Name}", entity.Code, entity.Name);
        return entity.ToDto();
    }

    public async Task<TrainingProgramGroupDto> UpdateAsync(Guid id, UpdateTrainingProgramGroupDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        entity.UpdateEntity(dto, updatedByUserId);
        await _groupRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program group updated: {Id}", id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        var inUse = await _programRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == entity.TenantId && p.ProgramGroupId == id, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("This group cannot be deleted because it is assigned to one or more training programs. Deactivate it instead.");

        await _groupRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training program group deleted: {Id}", id);
        return true;
    }
}
