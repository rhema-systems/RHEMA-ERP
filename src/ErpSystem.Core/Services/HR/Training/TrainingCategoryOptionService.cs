using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingCategoryOptionService : ITrainingCategoryOptionService
{
    private readonly IGenericRepository<TrainingCategoryOption> _optionRepository;
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingCategoryOptionService> _logger;

    public TrainingCategoryOptionService(
        IGenericRepository<TrainingCategoryOption> optionRepository,
        IGenericRepository<TrainingProgram> programRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingCategoryOptionService> logger)
    {
        _optionRepository = optionRepository;
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

    // A category owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<TrainingCategoryOption> GetOwnedAsync(Guid id)
    {
        var entity = await _optionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training category with ID '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<TrainingCategoryOptionDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _optionRepository.GetQueryable().Where(o => o.TenantId == tenantId);
        if (activeOnly)
            query = query.Where(o => o.IsActive);

        var options = await query
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
            .ToListAsync(cancellationToken);

        var counts = await _programRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId && p.CategoryOptionId != null)
            .GroupBy(p => p.CategoryOptionId!.Value)
            .Select(g => new { OptionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dtos = options.Select(o => o.ToDto()).ToList();
        foreach (var dto in dtos)
            dto.ProgramsCount = counts.FirstOrDefault(c => c.OptionId == dto.Id)?.Count ?? 0;

        return dtos;
    }

    public async Task<TrainingCategoryOptionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<TrainingCategoryOptionDto> CreateAsync(CreateTrainingCategoryOptionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        var duplicate = await _optionRepository.GetQueryable()
            .AnyAsync(o => o.TenantId == current && o.Code == dto.Code, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A training category with code '{dto.Code}' already exists.");

        var entity = dto.ToEntity(current, createdByUserId);
        await _optionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training category created: {Code} - {Name}", entity.Code, entity.Name);
        return entity.ToDto();
    }

    public async Task<TrainingCategoryOptionDto> UpdateAsync(Guid id, UpdateTrainingCategoryOptionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        entity.UpdateEntity(dto, updatedByUserId);
        await _optionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training category updated: {Id}", id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        var inUse = await _programRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == entity.TenantId && p.CategoryOptionId == id, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("This category cannot be deleted because it is assigned to one or more training programs. Deactivate it instead.");

        await _optionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training category deleted: {Id}", id);
        return true;
    }
}
