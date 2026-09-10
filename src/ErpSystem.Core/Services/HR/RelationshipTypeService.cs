using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc />
public class RelationshipTypeService : IRelationshipTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<RelationshipTypeService> _logger;

    public RelationshipTypeService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<RelationshipTypeService> logger)
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

    private IGenericRepository<RelationshipType> Repo => _unitOfWork.Repository<RelationshipType>();

    private async Task<RelationshipType> GetOwnedAsync(Guid id)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Relationship type not found.");
        return entity;
    }

    public async Task<IEnumerable<RelationshipTypeDto>> GetAllAsync(
        bool activeOnly = false,
        IEnumerable<RelationshipCategory>? categories = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = Repo.GetQueryable().Where(t => t.TenantId == tenantId);
        if (activeOnly) query = query.Where(t => t.IsActive);

        var wanted = categories?.Distinct().ToList();
        if (wanted is { Count: > 0 }) query = query.Where(t => wanted.Contains(t.Category));

        var items = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var counts = await CountUsagesAsync(tenantId, items.Select(t => t.Id).ToList(), cancellationToken);

        return items.Select(t => ToDto(t, counts.TryGetValue(t.Id, out var n) ? n : 0));
    }

    public async Task<RelationshipTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;

        var counts = await CountUsagesAsync(entity.TenantId, new List<Guid> { id }, cancellationToken);
        return ToDto(entity, counts.TryGetValue(id, out var n) ? n : 0);
    }

    public async Task<RelationshipTypeDto> CreateAsync(
        CreateRelationshipTypeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();

        await RequireNameAndCodeFreeAsync(tenantId, name, code, null, cancellationToken);
        RequireCoherentDependentMapping(dto.Category, dto.MapsToDependentRelationship);

        var entity = new RelationshipType
        {
            // Set explicitly: the context auto-stamp is inert, and an unstamped row fails
            // FK_RelationshipTypes_Tenants_TenantId.
            TenantId = tenantId,
            Name = name,
            Code = code,
            Category = dto.Category,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            MapsToDependentRelationship = dto.MapsToDependentRelationship,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
        };

        await Repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Relationship type created: {Name} ({Category})", entity.Name, entity.Category);
        return ToDto(entity, 0);
    }

    public async Task<RelationshipTypeDto> UpdateAsync(
        Guid id, UpdateRelationshipTypeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await GetOwnedAsync(id);

        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();

        await RequireNameAndCodeFreeAsync(entity.TenantId, name, code, id, cancellationToken);
        RequireCoherentDependentMapping(dto.Category, dto.MapsToDependentRelationship);

        entity.Name = name;
        entity.Code = code;
        entity.Category = dto.Category;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.MapsToDependentRelationship = dto.MapsToDependentRelationship;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;

        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ⚠ Records already pointing at this row keep the words they mirrored when they were saved.
        // Renaming "Wife" to "Spouse" does NOT rewrite the next of kin who chose it last year, and
        // it must not: the record is a statement of what was said at the time. New saves take the
        // new name.
        return await GetByIdAsync(id, cancellationToken) ?? ToDto(entity, 0);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        entity.IsActive = false;
        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Relationship type retired: {Name}", entity.Name);
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

        _logger.LogInformation("Relationship type deleted: {Name}", entity.Name);
    }

    /// <summary>
    /// One grouped count per consumer rather than a count per row, keyed by relationship-type id.
    /// </summary>
    /// <remarks>
    /// ⚠ All four consumers, and the list of four is the maintenance burden of this design: a fifth
    /// table that gains a <c>RelationshipTypeId</c> and is not added here would let its values be
    /// deleted out from under it, and the Restrict foreign key would then surface as a raw SQL
    /// constraint error on a screen that had promised the delete would work.
    /// </remarks>
    private async Task<Dictionary<Guid, int>> CountUsagesAsync(
        Guid tenantId, List<Guid> ids, CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, int>();
        if (ids.Count == 0) return totals;

        // ⚠ The GroupBy is on the ENTITY's own property, not on a projected scalar. `Select` then
        // `GroupBy(x => x)` reads more naturally and EF usually translates it, but "usually" is not
        // good enough for a query that runs on every read of this screen — a client evaluation here
        // would pull every referee, guarantor and next of kin in the tenant into memory to count them.
        await CountInto<EmployeeReferee>(totals, tenantId, ids, cancellationToken);
        await CountInto<EmployeeGuarantor>(totals, tenantId, ids, cancellationToken);
        await CountInto<EmployeeEmergencyContact>(totals, tenantId, ids, cancellationToken);
        await CountInto<JobCandidateReferee>(totals, tenantId, ids, cancellationToken);

        return totals;
    }

    /// <summary>
    /// Adds one consumer's per-value counts into <paramref name="totals"/>.
    /// </summary>
    /// <remarks>
    /// Generic over the entity rather than four near-identical copies, which is what let the four
    /// free-text columns this catalogue replaces drift apart in the first place. The constraint is
    /// <see cref="IRelationshipTypeConsumer"/>, so a fifth table that gains the column and forgets
    /// to say so cannot be added here by accident — it will not compile.
    /// </remarks>
    private async Task CountInto<T>(
        Dictionary<Guid, int> totals, Guid tenantId, List<Guid> ids, CancellationToken cancellationToken)
        where T : TenantEntity, IRelationshipTypeConsumer
    {
        var grouped = await _unitOfWork.Repository<T>().GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                     && x.RelationshipTypeId != null && ids.Contains(x.RelationshipTypeId.Value))
            .GroupBy(x => x.RelationshipTypeId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var row in grouped)
            totals[row.Id] = totals.TryGetValue(row.Id, out var n) ? n + row.Count : row.Count;
    }

    private async Task RequireNameAndCodeFreeAsync(
        Guid tenantId, string name, string? code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var query = Repo.GetQueryable().Where(t => t.TenantId == tenantId);
        if (excludeId.HasValue) query = query.Where(t => t.Id != excludeId.Value);

        if (await query.AnyAsync(t => t.Name == name, cancellationToken))
            throw new InvalidOperationException($"A relationship type named '{name}' already exists.");

        if (code != null && await query.AnyAsync(t => t.Code == code, cancellationToken))
            throw new InvalidOperationException($"A relationship type with the code '{code}' already exists.");
    }

    /// <remarks>
    /// ⚠ Refused rather than silently dropped. A professional row carrying
    /// <c>MapsToDependentRelationship = Spouse</c> would be a mapping nothing could ever use, and a
    /// field that accepts input and does nothing with it is the exact defect lane D1 spent its
    /// budget removing from the probation form.
    /// </remarks>
    private static void RequireCoherentDependentMapping(
        RelationshipCategory category, DependentRelationship? mapping)
    {
        if (mapping is null) return;
        if (category == RelationshipCategory.Familial) return;

        throw new InvalidOperationException(
            $"Only a familial relationship can map to a dependant relationship; '{category}' cannot. "
            + "Either change the category or leave the dependant mapping empty.");
    }

    private static RelationshipTypeDto ToDto(RelationshipType e, int usageCount) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Code = e.Code,
        Category = e.Category,
        Description = e.Description,
        MapsToDependentRelationship = e.MapsToDependentRelationship,
        SortOrder = e.SortOrder,
        IsActive = e.IsActive,
        UsageCount = usageCount,
    };
}
