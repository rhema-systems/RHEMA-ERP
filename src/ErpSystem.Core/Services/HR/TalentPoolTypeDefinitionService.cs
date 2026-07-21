using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Manages tenant-configurable talent pool types. Built-in (system-default) types cannot be
/// deleted, and a type in use by any pool cannot be deleted — deactivate it instead.
/// </summary>
public class TalentPoolTypeDefinitionService : ITalentPoolTypeDefinitionService
{
    private readonly IGenericRepository<TalentPoolTypeDefinition> _repository;
    private readonly IGenericRepository<TalentPool> _poolRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentPoolTypeDefinitionService> _logger;

    public TalentPoolTypeDefinitionService(
        IGenericRepository<TalentPoolTypeDefinition> repository,
        IGenericRepository<TalentPool> poolRepository,
        IUnitOfWork unitOfWork,
        ILogger<TalentPoolTypeDefinitionService> logger)
    {
        _repository = repository;
        _poolRepository = poolRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<TalentPoolTypeDefinitionDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable().Where(t => !t.IsDeleted);
        if (activeOnly)
            query = query.Where(t => t.IsActive);

        var types = await query
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var counts = await _poolRepository.GetQueryable()
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.PoolTypeId)
            .Select(g => new { PoolTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PoolTypeId, x => x.Count, cancellationToken);

        return types.Select(t =>
        {
            var dto = t.ToDto();
            dto.PoolCount = counts.TryGetValue(t.Id, out var c) ? c : 0;
            return dto;
        }).ToList();
    }

    public async Task<TalentPoolTypeDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity is null)
            throw new ArgumentException($"Talent pool type '{id}' not found.");

        var dto = entity.ToDto();
        dto.PoolCount = await _poolRepository.GetQueryable().CountAsync(p => p.PoolTypeId == id && !p.IsDeleted, cancellationToken);
        return dto;
    }

    public async Task<TalentPoolTypeDefinitionDto> CreateAsync(CreateTalentPoolTypeDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await EnsureUniqueAsync(entity.Name, entity.Code, null, cancellationToken);

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool type '{Name}' created ({Id})", entity.Name, entity.Id);
        return entity.ToDto();
    }

    public async Task<TalentPoolTypeDefinitionDto> UpdateAsync(UpdateTalentPoolTypeDefinitionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity is null)
            throw new ArgumentException($"Talent pool type '{dto.Id}' not found.");

        await EnsureUniqueAsync(dto.Name, entity.Code, dto.Id, cancellationToken);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool type '{Name}' updated ({Id})", entity.Name, entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity is null)
            throw new ArgumentException($"Talent pool type '{id}' not found.");

        if (entity.IsSystemDefault)
            throw new InvalidOperationException("Built-in pool types cannot be deleted. Deactivate it instead.");

        var inUse = await _poolRepository.GetQueryable().CountAsync(p => p.PoolTypeId == id && !p.IsDeleted, cancellationToken);
        if (inUse > 0)
            throw new InvalidOperationException($"This pool type is used by {inUse} pool(s) and cannot be deleted. Deactivate it or reassign those pools first.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Talent pool type deleted ({Id})", id);
        return true;
    }

    private async Task EnsureUniqueAsync(string name, string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var nameClash = await _repository.GetQueryable()
            .AnyAsync(t => !t.IsDeleted && t.Name == name && (excludeId == null || t.Id != excludeId), cancellationToken);
        if (nameClash)
            throw new InvalidOperationException($"A pool type named '{name}' already exists.");

        var codeClash = await _repository.GetQueryable()
            .AnyAsync(t => !t.IsDeleted && t.Code == code && (excludeId == null || t.Id != excludeId), cancellationToken);
        if (codeClash)
            throw new InvalidOperationException($"A pool type with code '{code}' already exists.");
    }
}
