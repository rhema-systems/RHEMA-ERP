using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for skill operations.
/// </summary>
public sealed class SkillService : ISkillService
{
    private readonly ISkillRepository _skillRepository;
    private readonly ICertificationService _certifications;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SkillService> _logger;

    public SkillService(
        ISkillRepository skillRepository,
        ICertificationService certifications,
        ICurrentUserProvider currentUserProvider,
        ILogger<SkillService> logger)
    {
        _skillRepository = skillRepository;
        _certifications = certifications;
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

    public async Task<SkillDto?> GetByIdAsync(Guid id)
    {
        var skill = await _skillRepository.GetByIdAsync(id);
        if (skill == null) return null;
        var dto = MapToDto(skill);
        // The accepted credentials ride on the single read only; the list stays lean.
        dto.Certifications = (await _certifications.GetSkillCertificationsAsync(id)).ToList();
        return dto;
    }

    public async Task<IEnumerable<SkillDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var items = await _skillRepository.GetAllAsync();
        return items.Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).Select(MapToDto);
    }

    public Task<IEnumerable<SkillDto>> GetActiveSkillsAsync()
        => GetActiveSkillsAsync(GetTenantId());

    // Anonymous callers (public career portal) have no tenant claim, so the tenant is passed in
    // from the X-Tenant-Id header instead. Both overloads share one filter so they cannot drift.
    public async Task<IEnumerable<SkillDto>> GetActiveSkillsAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));

        var items = await _skillRepository.GetActiveSkillsAsync();
        return items.Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).Select(MapToDto);
    }

    public async Task<IEnumerable<SkillDto>> GetByCategoryAsync(string category)
    {
        var tenantId = GetTenantId();
        var items = await _skillRepository.GetByCategoryAsync(category);
        return items.Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).Select(MapToDto);
    }

    public async Task<SkillDto?> GetByNameAsync(string name)
    {
        var skill = await _skillRepository.GetByNameAsync(name);
        return skill == null ? null : MapToDto(skill);
    }

    public async Task<SkillDto> CreateSkillAsync(CreateSkillDto createDto)
    {
        if (await _skillRepository.NameExistsAsync(createDto.Name))
        {
            throw new InvalidOperationException($"Skill name '{createDto.Name}' already exists.");
        }

        var entity = new Skill
        {
            TenantId = GetTenantId(),
            Name = createDto.Name,
            Description = createDto.Description,
            Category = createDto.Category,
            RequiresCertification = createDto.RequiresCertification,
            IsActive = createDto.IsActive
        };

        await _skillRepository.AddAsync(entity);
        // Round 2, lane C2: the accepted credentials, and the rule that a skill requiring
        // certification names at least one. Runs before the save, so a refusal stores nothing.
        await _certifications.SyncSkillCertificationsAsync(entity, createDto.Certifications ?? new List<SkillCertificationInputDto>());
        await _skillRepository.SaveChangesAsync();

        _logger.LogInformation("Skill created: {SkillId} ({Name})", entity.Id, entity.Name);

        var created = MapToDto(entity);
        created.Certifications = (await _certifications.GetSkillCertificationsAsync(entity.Id)).ToList();
        return created;
    }

    public async Task<SkillDto> UpdateSkillAsync(Guid id, CreateSkillDto updateDto)
    {
        var entity = await _skillRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
        {
            throw new InvalidOperationException($"Skill with ID {id} not found.");
        }

        if (!string.Equals(entity.Name, updateDto.Name, StringComparison.Ordinal) && await _skillRepository.NameExistsAsync(updateDto.Name))
        {
            throw new InvalidOperationException($"Skill name '{updateDto.Name}' already exists.");
        }

        entity.Name = updateDto.Name;
        entity.Description = updateDto.Description;
        entity.Category = updateDto.Category;
        entity.RequiresCertification = updateDto.RequiresCertification;
        entity.IsActive = updateDto.IsActive;

        // Null = the caller did not send the set (an older client); the rule is still checked
        // against what is stored.
        await _certifications.SyncSkillCertificationsAsync(entity, updateDto.Certifications);

        await _skillRepository.UpdateAsync(entity);
        await _skillRepository.SaveChangesAsync();

        _logger.LogInformation("Skill updated: {SkillId} ({Name})", entity.Id, entity.Name);

        var updated = MapToDto(entity);
        updated.Certifications = (await _certifications.GetSkillCertificationsAsync(entity.Id)).ToList();
        return updated;
    }

    public async Task<bool> DeleteSkillAsync(Guid id)
    {
        var entity = await _skillRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return false;

        await _skillRepository.DeleteAsync(id);
        await _skillRepository.SaveChangesAsync();

        _logger.LogInformation("Skill deleted (soft): {SkillId}", id);
        return true;
    }

    public Task<bool> NameExistsAsync(string name)
        => _skillRepository.NameExistsAsync(name);

    public async Task<IEnumerable<string>> GetSkillCategoriesAsync()
    {
        var tenantId = GetTenantId();
        var all = await _skillRepository.GetAllAsync();
        return all
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c)
            .Select(c => c!)
            .ToList();
    }

    private static SkillDto MapToDto(Skill entity)
    {
        return new SkillDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            RequiresCertification = entity.RequiresCertification,
            IsActive = entity.IsActive
        };
    }
}
