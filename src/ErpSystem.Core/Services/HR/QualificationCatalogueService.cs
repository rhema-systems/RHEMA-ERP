using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<QualificationCatalogueService> _logger;

    public QualificationCatalogueService(
        IQualificationCatalogueRepository repository,
        ICurrentUserProvider currentUserProvider,
        ILogger<QualificationCatalogueService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant
    // query-filter and TenantId auto-stamp are inert. Following the RHEMA convention,
    // this service scopes reads/writes to the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<QualificationCatalogueDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<QualificationCatalogueDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var items = await _repository.GetAllAsync();
        return items.Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).Select(MapToDto);
    }

    public Task<IEnumerable<QualificationCatalogueDto>> GetActiveAsync()
        => GetActiveAsync(GetTenantId());

    // Anonymous callers (public career portal) have no tenant claim, so the tenant is passed in
    // from the X-Tenant-Id header instead. Both overloads share one filter so they cannot drift.
    public async Task<IEnumerable<QualificationCatalogueDto>> GetActiveAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));

        var items = await _repository.GetActiveAsync();
        return items.Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).Select(MapToDto);
    }

    public async Task<IEnumerable<QualificationCatalogueDto>> GetByTypeAsync(QualificationType type)
    {
        var tenantId = GetTenantId();
        var items = await _repository.GetByTypeAsync(type);
        return items.Where(x => x.TenantId == tenantId).Select(MapToDto);
    }

    public async Task<QualificationCatalogueDto?> GetByNameAsync(string name)
    {
        var entity = await _repository.GetByNameAsync(name);
        return entity == null || entity.TenantId != GetTenantId() ? null : MapToDto(entity);
    }

    public async Task<QualificationCatalogueDto> CreateAsync(CreateQualificationCatalogueDto dto)
    {
        if (await _repository.NameExistsAsync(dto.Name))
            throw new InvalidOperationException($"Qualification '{dto.Name}' already exists in the catalogue.");

        var entity = new Qualification
        {
            TenantId = GetTenantId(),
            Name = dto.Name,
            ShortCode = dto.ShortCode?.Trim(),
            Description = dto.Description,
            Type = dto.Type,
            IssuingAuthority = dto.IssuingAuthority,
            QualificationLevelId = dto.QualificationLevelId,
            IsActive = dto.IsActive,
        };

        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Qualification catalogue entry created: {Id} ({Name})", entity.Id, entity.Name);

        // ⚠ Re-read, because the level navigation is not loaded on an entity we just built by hand
        // and the DTO reports its name. Returning the in-memory graph is how a write response ends
        // up contradicting the list the caller refreshes a moment later.
        return MapToDto(await _repository.GetByIdAsync(entity.Id) ?? entity);
    }

    public async Task<QualificationCatalogueDto> UpdateAsync(Guid id, CreateQualificationCatalogueDto dto)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new InvalidOperationException($"Qualification with ID {id} not found.");

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
        entity.QualificationLevelId = dto.QualificationLevelId;
        entity.IsActive = dto.IsActive;

        await _repository.UpdateAsync(entity);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Qualification catalogue entry updated: {Id} ({Name})", entity.Id, entity.Name);

        // ⚠ Re-read for the same reason as create: changing the level must change the name the
        // response carries, and the stale navigation would keep reporting the old rung.
        return MapToDto(await _repository.GetByIdAsync(entity.Id) ?? entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return false;

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
        QualificationLevelId = entity.QualificationLevelId,
        // ⚠ Resolved from the navigation, which means the READS have to Include it. A name declared
        // on a DTO and set by nothing is the shape this module has met six times; the reads below
        // load the level for exactly this reason.
        QualificationLevelName = entity.QualificationLevel?.Name,
        QualificationLevelRank = entity.QualificationLevel?.Rank,
        IsActive = entity.IsActive,
    };
}
