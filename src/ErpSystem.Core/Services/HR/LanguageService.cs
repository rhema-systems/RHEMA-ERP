using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="ILanguageService"/>
public class LanguageService : ILanguageService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<LanguageService> _logger;

    public LanguageService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider, ILogger<LanguageService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IGenericRepository<Language> Repo => _unitOfWork.Repository<Language>();

    private async Task<Language> GetOwnedAsync(Guid id)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Language not found.");
        return entity;
    }

    public async Task<IEnumerable<LanguageDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = Repo.GetQueryable().Where(l => l.TenantId == tenantId);
        if (activeOnly) query = query.Where(l => l.IsActive);
        var items = await query.OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ToListAsync(cancellationToken);
        var counts = await CountUsagesAsync(tenantId, items.Select(l => l.Id).ToList(), cancellationToken);
        return items.Select(l => ToDto(l, counts.GetValueOrDefault(l.Id))).ToList();
    }

    public async Task<IEnumerable<LanguageDto>> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = await Repo.GetQueryable()
            .Where(l => l.TenantId == tenantId && l.IsActive)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);
        return items.Select(l => ToDto(l, 0)).ToList();
    }

    public async Task<LanguageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;
        var counts = await CountUsagesAsync(entity.TenantId, new List<Guid> { id }, cancellationToken);
        return ToDto(entity, counts.GetValueOrDefault(id));
    }

    public async Task<LanguageDto> CreateAsync(CreateLanguageDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();
        await RequireNameAndCodeFreeAsync(tenantId, name, code, null, cancellationToken);
        var entity = new Language
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
        };
        await Repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Language created: {Name} ({Code})", entity.Name, entity.Code);
        return ToDto(entity, 0);
    }

    public async Task<LanguageDto> UpdateAsync(Guid id, UpdateLanguageDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await GetOwnedAsync(id);
        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();
        await RequireNameAndCodeFreeAsync(entity.TenantId, name, code, id, cancellationToken);
        entity.Name = name;
        entity.Code = code;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken) ?? ToDto(entity, 0);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        entity.IsActive = false;
        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Language retired: {Name}", entity.Name);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var counts = await CountUsagesAsync(entity.TenantId, new List<Guid> { id }, cancellationToken);
        if (counts.TryGetValue(id, out var used) && used > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' cannot be deleted: {used} candidate language record{(used == 1 ? "" : "s")} still "
                + "name it. Retire it instead — it stops being offered on new records and the ones already using it keep it.");
        await Repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Language deleted: {Name}", entity.Name);
    }

    private async Task RequireNameAndCodeFreeAsync(Guid tenantId, string name, string? code, Guid? exceptId, CancellationToken cancellationToken)
    {
        var nameTaken = await Repo.GetQueryable()
            .AnyAsync(l => l.TenantId == tenantId && l.Id != exceptId && !l.IsDeleted && l.Name.ToLower() == name.ToLower(), cancellationToken);
        if (nameTaken) throw new InvalidOperationException($"A language named '{name}' already exists.");
        if (code != null)
        {
            var codeTaken = await Repo.GetQueryable()
                .AnyAsync(l => l.TenantId == tenantId && l.Id != exceptId && !l.IsDeleted && l.Code == code, cancellationToken);
            if (codeTaken) throw new InvalidOperationException($"The code '{code}' is already used by another language.");
        }
    }

    /// <summary>The one consumer today: candidate language rows. An employee-side record joins here when it is built.</summary>
    private async Task<Dictionary<Guid, int>> CountUsagesAsync(Guid tenantId, List<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return new Dictionary<Guid, int>();
        return await _unitOfWork.Repository<JobCandidateLanguage>().GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.LanguageId != null && ids.Contains(x.LanguageId!.Value))
            .GroupBy(x => x.LanguageId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
    }

    private static LanguageDto ToDto(Language l, int usage) => new()
    {
        Id = l.Id, Name = l.Name, Code = l.Code, Description = l.Description,
        SortOrder = l.SortOrder, IsActive = l.IsActive, UsageCount = usage,
    };
}
