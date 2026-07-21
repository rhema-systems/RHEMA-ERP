using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for the Qualification master catalogue.
/// Mirrors SkillService in structure and conventions.
/// </summary>
public sealed class QualificationCatalogueService : IQualificationCatalogueService
{
    private readonly IQualificationCatalogueRepository _repository;
    private readonly ILogger<QualificationCatalogueService> _logger;

    public QualificationCatalogueService(
        IQualificationCatalogueRepository repository,
        ILogger<QualificationCatalogueService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<QualificationCatalogueDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<QualificationCatalogueDto>> GetAllAsync()
    {
        var items = await _repository.GetAllAsync();
        return items.OrderBy(x => x.Name).Select(MapToDto);
    }

    public async Task<IEnumerable<QualificationCatalogueDto>> GetActiveAsync()
    {
        var items = await _repository.GetActiveAsync();
        return items.Select(MapToDto);
    }

    public async Task<IEnumerable<QualificationCatalogueDto>> GetByTypeAsync(QualificationType type)
    {
        var items = await _repository.GetByTypeAsync(type);
        return items.Select(MapToDto);
    }

    public async Task<QualificationCatalogueDto?> GetByNameAsync(string name)
    {
        var entity = await _repository.GetByNameAsync(name);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<QualificationCatalogueDto> CreateAsync(CreateQualificationCatalogueDto dto)
    {
        if (await _repository.NameExistsAsync(dto.Name))
            throw new InvalidOperationException($"Qualification '{dto.Name}' already exists in the catalogue.");

        var entity = new Qualification
        {
            Name = dto.Name,
            ShortCode = dto.ShortCode?.Trim(),
            Description = dto.Description,
            Type = dto.Type,
            IssuingAuthority = dto.IssuingAuthority,
            IsActive = dto.IsActive,
        };

        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Qualification catalogue entry created: {Id} ({Name})", entity.Id, entity.Name);
        return MapToDto(entity);
    }

    public async Task<QualificationCatalogueDto> UpdateAsync(Guid id, CreateQualificationCatalogueDto dto)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Qualification with ID {id} not found.");

        if (!string.Equals(entity.Name, dto.Name, StringComparison.Ordinal)
            && await _repository.NameExistsAsync(dto.Name))
        {
            throw new InvalidOperationException($"Qualification '{dto.Name}' already exists in the catalogue.");
        }

        entity.Name = dto.Name;
        entity.ShortCode = dto.ShortCode?.Trim();
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.IssuingAuthority = dto.IssuingAuthority;
        entity.IsActive = dto.IsActive;

        await _repository.UpdateAsync(entity);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Qualification catalogue entry updated: {Id} ({Name})", entity.Id, entity.Name);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Qualification catalogue entry soft-deleted: {Id}", id);
        return true;
    }

    public Task<bool> NameExistsAsync(string name)
        => _repository.NameExistsAsync(name);

    private static QualificationCatalogueDto MapToDto(Qualification entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ShortCode = entity.ShortCode,
        Description = entity.Description,
        Type = entity.Type,
        IssuingAuthority = entity.IssuingAuthority,
        IsActive = entity.IsActive,
    };
}
