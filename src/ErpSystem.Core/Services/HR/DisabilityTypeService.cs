using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc />
/// <remarks>
/// Round 3, lane P2 (register row E-5). The relationship-type service, one axis simpler: there is
/// no enum bridge, and the two consumers are the employee and the dependant.
/// </remarks>
public class DisabilityTypeService : IDisabilityTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<DisabilityTypeService> _logger;

    public DisabilityTypeService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider, ILogger<DisabilityTypeService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Every read and write below scopes to the authenticated tenant
    // explicitly, per the RHEMA convention.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IGenericRepository<DisabilityType> Repo => _unitOfWork.Repository<DisabilityType>();

    private async Task<DisabilityType> GetOwnedAsync(Guid id)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Disability type not found.");
        return entity;
    }

    public async Task<IEnumerable<DisabilityTypeDto>> GetAllAsync(
        bool activeOnly = false, IEnumerable<DisabilityCategory>? categories = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = Repo.GetQueryable().Where(t => t.TenantId == tenantId);
        if (activeOnly) query = query.Where(t => t.IsActive);
        var wanted = categories?.Distinct().ToList();
        if (wanted is { Count: > 0 }) query = query.Where(t => wanted.Contains(t.Category));

        var items = await query.OrderBy(t => t.Category).ThenBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(cancellationToken);
        var counts = await CountUsagesAsync(tenantId, items.Select(t => t.Id).ToList(), cancellationToken);
        return items.Select(t => ToDto(t, counts.TryGetValue(t.Id, out var n) ? n : 0));
    }

    public async Task<DisabilityTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;
        var counts = await CountUsagesAsync(entity.TenantId, new List<Guid> { id }, cancellationToken);
        return ToDto(entity, counts.TryGetValue(id, out var n) ? n : 0);
    }

    public async Task<DisabilityTypeDto> CreateAsync(CreateDisabilityTypeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();
        await RequireNameAndCodeFreeAsync(tenantId, name, code, null, cancellationToken);

        var entity = new DisabilityType
        {
            // Set explicitly: the context auto-stamp is inert, and an unstamped row fails the Tenants FK.
            TenantId = tenantId,
            Name = name,
            Code = code,
            Category = dto.Category,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
        };
        await Repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Disability type created: {Name} ({Category})", entity.Name, entity.Category);
        return ToDto(entity, 0);
    }

    public async Task<DisabilityTypeDto> UpdateAsync(Guid id, UpdateDisabilityTypeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await GetOwnedAsync(id);
        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();
        await RequireNameAndCodeFreeAsync(entity.TenantId, name, code, id, cancellationToken);

        entity.Name = name;
        entity.Code = code;
        entity.Category = dto.Category;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Records naming the row read its CURRENT name (there is no mirrored text here): a rename
        // shows everywhere at once, which for a medical term is the right behaviour.
        return await GetByIdAsync(id, cancellationToken) ?? ToDto(entity, 0);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        entity.IsActive = false;
        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Disability type retired: {Name}", entity.Name);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var counts = await CountUsagesAsync(entity.TenantId, new List<Guid> { id }, cancellationToken);
        if (counts.TryGetValue(id, out var used) && used > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' cannot be deleted: {used} record{(used == 1 ? "" : "s")} still "
                + "name it. Retire it instead — it stops being offered on new records and the ones "
                + "already using it keep it.");
        await Repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Disability type deleted: {Name}", entity.Name);
    }

    public async Task EnsureUsableAsync(Guid? disabilityTypeId, bool hasDisability, CancellationToken cancellationToken = default)
    {
        if (disabilityTypeId is not { } id) return;
        if (!hasDisability)
            throw new InvalidOperationException("A disability type was given but the record does not say the person has a disability — tick it, or leave the type empty.");
        var tenantId = GetTenantId();
        var row = await Repo.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
        if (row is null)
            throw new InvalidOperationException("That disability type is not on this organisation's list.");
        if (!row.IsActive)
            throw new InvalidOperationException($"'{row.Name}' has been retired and cannot be put on a new record. Pick a current type, or reactivate it first.");
    }

    /// <summary>One grouped count per consumer — the employee and the dependant — keyed by type id.</summary>
    private async Task<Dictionary<Guid, int>> CountUsagesAsync(Guid tenantId, List<Guid> ids, CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, int>();
        if (ids.Count == 0) return totals;
        await CountInto<Employee>(totals, tenantId, ids, cancellationToken);
        await CountInto<EmployeeDependent>(totals, tenantId, ids, cancellationToken);
        return totals;
    }

    private async Task CountInto<T>(Dictionary<Guid, int> totals, Guid tenantId, List<Guid> ids, CancellationToken cancellationToken)
        where T : TenantEntity, IDisabilityTypeConsumer
    {
        var grouped = await _unitOfWork.Repository<T>().GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.DisabilityTypeId != null && ids.Contains(x.DisabilityTypeId.Value))
            .GroupBy(x => x.DisabilityTypeId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var row in grouped)
            totals[row.Id] = totals.TryGetValue(row.Id, out var n) ? n + row.Count : row.Count;
    }

    private async Task RequireNameAndCodeFreeAsync(Guid tenantId, string name, string? code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var query = Repo.GetQueryable().Where(t => t.TenantId == tenantId);
        if (excludeId.HasValue) query = query.Where(t => t.Id != excludeId.Value);
        if (await query.AnyAsync(t => t.Name == name, cancellationToken))
            throw new InvalidOperationException($"A disability type named '{name}' already exists.");
        if (code != null && await query.AnyAsync(t => t.Code == code, cancellationToken))
            throw new InvalidOperationException($"A disability type with the code '{code}' already exists.");
    }

    private static DisabilityTypeDto ToDto(DisabilityType e, int usageCount) => new()
    {
        Id = e.Id, Name = e.Name, Code = e.Code, Category = e.Category, Description = e.Description,
        SortOrder = e.SortOrder, IsActive = e.IsActive, UsageCount = usageCount,
    };
}
