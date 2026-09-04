using ErpSystem.Core.DTOs.Reference;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Reference;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Reference;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Administrative geography — shared reference data.
//  See docs/GEOGRAPHY-REFERENCE-DESIGN.md for the decisions this implements.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Maintains the division schemes, their tiers and their areas, and answers the two questions
/// consumers actually ask: "what does an address form for this country look like?" and "which area
/// does this name mean?".
/// </summary>
public interface IGeographyService
{
    // Schemes
    Task<IEnumerable<GeoSchemeDto>> GetSchemesAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<GeoSchemeDetailDto> GetSchemeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The scheme an address form should render for a country — its default, or its only one.
    /// Returns null when the country has no scheme, which the widget shows as free-text fallback
    /// rather than an error.
    /// </summary>
    Task<GeoSchemeDetailDto?> GetSchemeForCountryAsync(Guid countryId, CancellationToken ct = default);

    Task<GeoSchemeDto> CreateSchemeAsync(CreateGeoSchemeDto dto, CancellationToken ct = default);
    Task<GeoSchemeDto> UpdateSchemeAsync(UpdateGeoSchemeDto dto, CancellationToken ct = default);
    Task<bool> DeleteSchemeAsync(Guid id, CancellationToken ct = default);

    // Levels
    Task<IEnumerable<GeoLevelDto>> GetLevelsAsync(Guid schemeId, bool activeOnly = false, CancellationToken ct = default);
    Task<GeoLevelDto> CreateLevelAsync(CreateGeoLevelDto dto, CancellationToken ct = default);
    Task<GeoLevelDto> UpdateLevelAsync(UpdateGeoLevelDto dto, CancellationToken ct = default);
    Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default);

    // Areas
    Task<IEnumerable<GeoAreaDto>> GetAreasAsync(
        Guid schemeId, Guid? levelId = null, Guid? parentId = null, bool includeHistorical = false,
        string? search = null, CancellationToken ct = default);
    Task<GeoAreaDto> GetAreaAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<GeoAreaTreeNodeDto>> GetAreaTreeAsync(Guid schemeId, bool includeHistorical = false, CancellationToken ct = default);

    /// <summary>
    /// The cascade the address widget drives: areas at one tier, optionally under one parent.
    /// Historical areas are never offered — a form must not let someone pick a district that no
    /// longer exists.
    /// </summary>
    Task<IEnumerable<GeoAreaOptionDto>> GetAreaOptionsAsync(Guid levelId, Guid? parentId, CancellationToken ct = default);

    /// <summary>Broadest-first ancestors of an area, including the area itself.</summary>
    Task<IEnumerable<GeoAreaOptionDto>> GetAncestorsAsync(Guid areaId, CancellationToken ct = default);

    /// <summary>
    /// The free-text address snapshot a consumer writes alongside its <c>GeoAreaId</c> — the
    /// region name for its <c>State</c> column and the town (or district) name for its <c>City</c>.
    /// </summary>
    /// <remarks>
    /// <para>Exists so every consumer spells the snapshot the same way. HR is the first caller;
    /// the employee import is the second, and CompanyProfile and the medical facilities follow in
    /// phase 4. A consumer computing this for itself is how the four different spellings of
    /// "region" got here in the first place.</para>
    ///
    /// <para>Returns <c>(null, null)</c> when the area cannot be read, so a caller can leave what
    /// the record already said rather than blanking it.</para>
    /// </remarks>
    Task<(string? Region, string? City)> GetAddressSnapshotAsync(Guid geoAreaId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a name to an area, by name then by alias. Used by imports and by backfill, which is
    /// why it matches aliases: a spreadsheet that still says "Brong Ahafo" must resolve.
    /// </summary>
    Task<IEnumerable<GeoAreaResolutionDto>> ResolveAsync(
        string name, Guid? schemeId = null, Guid? levelId = null, Guid? parentId = null, CancellationToken ct = default);

    Task<GeoAreaDto> CreateAreaAsync(CreateGeoAreaDto dto, CancellationToken ct = default);
    Task<GeoAreaDto> UpdateAreaAsync(UpdateGeoAreaDto dto, CancellationToken ct = default);
    Task<bool> DeleteAreaAsync(Guid id, CancellationToken ct = default);

    // Aliases
    Task<IEnumerable<GeoAreaAliasDto>> GetAliasesAsync(Guid areaId, CancellationToken ct = default);
    Task<GeoAreaAliasDto> CreateAliasAsync(CreateGeoAreaAliasDto dto, CancellationToken ct = default);
    Task<GeoAreaAliasDto> UpdateAliasAsync(UpdateGeoAreaAliasDto dto, CancellationToken ct = default);
    Task<bool> DeleteAliasAsync(Guid id, CancellationToken ct = default);
}

public class GeographyService : IGeographyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<GeographyService> _logger;

    /// <summary>
    /// Every module that stores a <c>GeoAreaId</c>, so an area in use cannot be deleted.
    /// </summary>
    /// <remarks>
    /// ⚠ Empty is a legitimate state — a deployment with no consumer wired yet — but it means the
    /// only protection is the children/successor check below. See <see cref="IGeoAreaConsumer"/>
    /// for why the foreign key does not cover this.
    /// </remarks>
    private readonly IEnumerable<IGeoAreaConsumer> _consumers;

    public GeographyService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IEnumerable<IGeoAreaConsumer> consumers,
        ILogger<GeographyService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _consumers = consumers;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global filter and TenantId
    // auto-stamp are inert. Every read and write scopes to the tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<T> GetOwnedAsync<T>(Guid id, string label, CancellationToken ct)
        where T : Entities.TenantEntity
    {
        var entity = await _unitOfWork.Repository<T>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

        // Reported as missing rather than forbidden, so the endpoints do not confirm that an id
        // exists on another tenant.
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"{label} with ID '{id}' not found.");

        return entity;
    }

    private static bool IsHistorical(GeoArea area) =>
        area.EffectiveTo.HasValue && area.EffectiveTo.Value < DateOnly.FromDateTime(DateTime.UtcNow);

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Schemes
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<GeoSchemeDto>> GetSchemesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var schemes = await _unitOfWork.Repository<GeoScheme>().GetQueryable()
            .Include(s => s.Country)
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && (!activeOnly || s.IsActive))
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var ids = schemes.Select(s => s.Id).ToList();

        var levelCounts = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && ids.Contains(l.SchemeId))
            .GroupBy(l => l.SchemeId)
            .Select(g => new { SchemeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var areaCounts = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.SchemeId))
            .GroupBy(a => a.SchemeId)
            .Select(g => new { SchemeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return schemes.Select(s => Map(
            s,
            levelCounts.FirstOrDefault(c => c.SchemeId == s.Id)?.Count ?? 0,
            areaCounts.FirstOrDefault(c => c.SchemeId == s.Id)?.Count ?? 0)).ToList();
    }

    public async Task<GeoSchemeDetailDto> GetSchemeAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var scheme = await _unitOfWork.Repository<GeoScheme>().GetQueryable()
            .Include(s => s.Country)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted && s.TenantId == tenantId, ct);

        if (scheme == null)
            throw new ArgumentException($"Division scheme with ID '{id}' not found.");

        return await BuildDetailAsync(scheme, tenantId, ct);
    }

    public async Task<GeoSchemeDetailDto?> GetSchemeForCountryAsync(Guid countryId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        // The default first, then any single active scheme. A country with two non-default schemes
        // is ambiguous, so nothing is guessed — the form falls back to free text and an
        // administrator is expected to mark one as the default.
        var candidates = await _unitOfWork.Repository<GeoScheme>().GetQueryable()
            .Include(s => s.Country)
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive && s.CountryId == countryId)
            .ToListAsync(ct);

        var scheme = candidates.FirstOrDefault(s => s.IsDefault)
                     ?? (candidates.Count == 1 ? candidates[0] : null);

        if (scheme == null)
        {
            if (candidates.Count > 1)
                _logger.LogWarning(
                    "Country {CountryId} has {Count} active division schemes and none is marked default; "
                    + "address forms will fall back to free text.", countryId, candidates.Count);
            return null;
        }

        return await BuildDetailAsync(scheme, tenantId, ct);
    }

    private async Task<GeoSchemeDetailDto> BuildDetailAsync(GeoScheme scheme, Guid tenantId, CancellationToken ct)
    {
        var levels = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SchemeId == scheme.Id)
            .OrderBy(l => l.LevelNumber)
            .ToListAsync(ct);

        var levelIds = levels.Select(l => l.Id).ToList();

        var areaCounts = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && levelIds.Contains(a.GeoLevelId))
            .GroupBy(a => a.GeoLevelId)
            .Select(g => new { LevelId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var areaTotal = areaCounts.Sum(c => c.Count);
        var basic = Map(scheme, levels.Count, areaTotal);

        return new GeoSchemeDetailDto
        {
            Id = basic.Id,
            CreatedAt = basic.CreatedAt,
            CreatedBy = basic.CreatedBy,
            UpdatedAt = basic.UpdatedAt,
            UpdatedBy = basic.UpdatedBy,
            CountryId = basic.CountryId,
            CountryName = basic.CountryName,
            CountryCode = basic.CountryCode,
            Name = basic.Name,
            Code = basic.Code,
            Description = basic.Description,
            IsDefault = basic.IsDefault,
            IsActive = basic.IsActive,
            LevelCount = basic.LevelCount,
            AreaCount = basic.AreaCount,
            Levels = levels
                .Select(l => Map(l, scheme.Name, areaCounts.FirstOrDefault(c => c.LevelId == l.Id)?.Count ?? 0))
                .ToList(),
        };
    }

    public async Task<GeoSchemeDto> CreateSchemeAsync(CreateGeoSchemeDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireCountryAsync(tenantId, dto.CountryId, ct);
        await RequireUnusedSchemeCodeAsync(tenantId, dto.Code, null, ct);

        var entity = new GeoScheme
        {
            TenantId = tenantId,
            CountryId = dto.CountryId,
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim().ToUpperInvariant(),
            Description = Blank(dto.Description),
            IsDefault = dto.IsDefault,
            IsActive = dto.IsActive,
        };

        if (dto.IsDefault)
            await ClearOtherDefaultsAsync(tenantId, dto.CountryId, null, ct);

        await _unitOfWork.Repository<GeoScheme>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReadSchemeAsync(entity.Id, tenantId, ct);
    }

    public async Task<GeoSchemeDto> UpdateSchemeAsync(UpdateGeoSchemeDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<GeoScheme>(dto.Id, "Division scheme", ct);
        await RequireCountryAsync(tenantId, dto.CountryId, ct);
        await RequireUnusedSchemeCodeAsync(tenantId, dto.Code, dto.Id, ct);

        // ⚠ Refused rather than cascaded. Moving a scheme to another country would leave every area
        // under it describing a place in a country it no longer belongs to, and every address that
        // resolved through it silently wrong.
        if (entity.CountryId != dto.CountryId)
        {
            var areas = await _unitOfWork.Repository<GeoArea>().GetQueryable()
                .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SchemeId == dto.Id, ct);
            if (areas > 0)
                throw new InvalidOperationException(
                    $"'{entity.Name}' already holds {areas} area{(areas == 1 ? "" : "s")}, so its country cannot be "
                    + "changed. Create a scheme for the other country instead.");
        }

        entity.CountryId = dto.CountryId;
        entity.Name = dto.Name.Trim();
        entity.Code = dto.Code.Trim().ToUpperInvariant();
        entity.Description = Blank(dto.Description);
        entity.IsActive = dto.IsActive;

        if (dto.IsDefault && !entity.IsDefault)
            await ClearOtherDefaultsAsync(tenantId, dto.CountryId, dto.Id, ct);
        entity.IsDefault = dto.IsDefault;

        await _unitOfWork.Repository<GeoScheme>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReadSchemeAsync(entity.Id, tenantId, ct);
    }

    public async Task<bool> DeleteSchemeAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<GeoScheme>(id, "Division scheme", ct);

        var levels = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .CountAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.SchemeId == id, ct);
        if (levels > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' still defines {levels} tier{(levels == 1 ? "" : "s")}, so it cannot be removed. "
                + "Remove the tiers first, or make the scheme inactive to keep it off new address forms while "
                + "leaving existing records able to resolve through it.");

        await _unitOfWork.Repository<GeoScheme>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<GeoSchemeDto> ReadSchemeAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var scheme = await _unitOfWork.Repository<GeoScheme>().GetQueryable()
            .Include(s => s.Country)
            .FirstAsync(s => s.Id == id && s.TenantId == tenantId, ct);

        var levelCount = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .CountAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.SchemeId == id, ct);
        var areaCount = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SchemeId == id, ct);

        return Map(scheme, levelCount, areaCount);
    }

    private async Task RequireCountryAsync(Guid tenantId, Guid countryId, CancellationToken ct)
    {
        var exists = await _unitOfWork.Repository<Country>().GetQueryable()
            .AnyAsync(c => c.Id == countryId && c.TenantId == tenantId && !c.IsDeleted, ct);
        if (!exists)
            throw new ArgumentException($"Country with ID '{countryId}' not found.");
    }

    private async Task RequireUnusedSchemeCodeAsync(Guid tenantId, string code, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = code.Trim().ToUpperInvariant();
        var clash = await _unitOfWork.Repository<GeoScheme>().GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Code == trimmed
                        && (exceptId == null || s.Id != exceptId), ct);
        if (clash)
            throw new InvalidOperationException($"A division scheme with code '{trimmed}' already exists.");
    }

    /// <summary>
    /// A country has exactly one default scheme. Setting a new one unsets the old rather than being
    /// refused — the caller is telling us which it is, not asking permission.
    /// </summary>
    private async Task ClearOtherDefaultsAsync(Guid tenantId, Guid countryId, Guid? exceptId, CancellationToken ct)
    {
        var others = await _unitOfWork.Repository<GeoScheme>().GetQueryable()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.CountryId == countryId && s.IsDefault
                     && (exceptId == null || s.Id != exceptId))
            .ToListAsync(ct);

        foreach (var other in others)
        {
            other.IsDefault = false;
            await _unitOfWork.Repository<GeoScheme>().UpdateAsync(other);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Levels
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<GeoLevelDto>> GetLevelsAsync(Guid schemeId, bool activeOnly = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var scheme = await GetOwnedAsync<GeoScheme>(schemeId, "Division scheme", ct);

        var levels = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SchemeId == schemeId && (!activeOnly || l.IsActive))
            .OrderBy(l => l.LevelNumber)
            .ToListAsync(ct);

        var levelIds = levels.Select(l => l.Id).ToList();
        var counts = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && levelIds.Contains(a.GeoLevelId))
            .GroupBy(a => a.GeoLevelId)
            .Select(g => new { LevelId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return levels
            .Select(l => Map(l, scheme.Name, counts.FirstOrDefault(c => c.LevelId == l.Id)?.Count ?? 0))
            .ToList();
    }

    public async Task<GeoLevelDto> CreateLevelAsync(CreateGeoLevelDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var scheme = await GetOwnedAsync<GeoScheme>(dto.SchemeId, "Division scheme", ct);
        await RequireUnusedLevelAsync(tenantId, dto.SchemeId, dto.Code, dto.LevelNumber, null, ct);

        var entity = new GeoLevel
        {
            TenantId = tenantId,
            SchemeId = dto.SchemeId,
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim().ToUpperInvariant(),
            Description = Blank(dto.Description),
            LevelNumber = dto.LevelNumber,
            IsRequiredInAddress = dto.IsRequiredInAddress,
            AllowsAddressAssignment = dto.AllowsAddressAssignment,
            IsActive = dto.IsActive,
        };

        await _unitOfWork.Repository<GeoLevel>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return Map(entity, scheme.Name, 0);
    }

    public async Task<GeoLevelDto> UpdateLevelAsync(UpdateGeoLevelDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<GeoLevel>(dto.Id, "Tier", ct);
        var scheme = await GetOwnedAsync<GeoScheme>(entity.SchemeId, "Division scheme", ct);
        await RequireUnusedLevelAsync(tenantId, entity.SchemeId, dto.Code, dto.LevelNumber, dto.Id, ct);

        // ⚠ Refused once areas exist. LevelNumber is what orders the cascade and what parent
        // validation compares; renumbering a populated tier would reorder the address form under
        // the data rather than with it.
        var areaCount = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.GeoLevelId == dto.Id, ct);

        if (entity.LevelNumber != dto.LevelNumber && areaCount > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' already holds {areaCount} area{(areaCount == 1 ? "" : "s")}, so its position in "
                + "the hierarchy cannot be changed. The tier's depth is what the address cascade and every parent "
                + "check are built on.");

        // The scheme is deliberately not movable: a tier belongs to the scheme that defines it.
        entity.Name = dto.Name.Trim();
        entity.Code = dto.Code.Trim().ToUpperInvariant();
        entity.Description = Blank(dto.Description);
        entity.LevelNumber = dto.LevelNumber;
        entity.IsRequiredInAddress = dto.IsRequiredInAddress;
        entity.AllowsAddressAssignment = dto.AllowsAddressAssignment;
        entity.IsActive = dto.IsActive;

        await _unitOfWork.Repository<GeoLevel>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return Map(entity, scheme.Name, areaCount);
    }

    public async Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<GeoLevel>(id, "Tier", ct);

        var areas = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.GeoLevelId == id, ct);
        if (areas > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is the tier of {areas} area{(areas == 1 ? "" : "s")}, so it cannot be removed. "
                + "Remove those areas first, or make the tier inactive to keep it off new address forms.");

        await _unitOfWork.Repository<GeoLevel>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task RequireUnusedLevelAsync(
        Guid tenantId, Guid schemeId, string code, int levelNumber, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = code.Trim().ToUpperInvariant();

        var codeClash = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .AnyAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.SchemeId == schemeId && l.Code == trimmed
                        && (exceptId == null || l.Id != exceptId), ct);
        if (codeClash)
            throw new InvalidOperationException($"This scheme already has a tier with code '{trimmed}'.");

        var numberClash = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .AnyAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.SchemeId == schemeId
                        && l.LevelNumber == levelNumber && (exceptId == null || l.Id != exceptId), ct);
        if (numberClash)
            throw new InvalidOperationException(
                $"This scheme already has a tier at depth {levelNumber}. Two tiers cannot share a depth — that is "
                + "what tells the address form which dropdown comes first.");
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Areas
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<GeoAreaDto>> GetAreasAsync(
        Guid schemeId, Guid? levelId = null, Guid? parentId = null, bool includeHistorical = false,
        string? search = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync<GeoScheme>(schemeId, "Division scheme", ct);

        var query = _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Include(a => a.GeoLevel)
            .Include(a => a.Scheme)
            .Include(a => a.ParentArea)
            .Include(a => a.SupersededByArea)
            .Include(a => a.Aliases)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.SchemeId == schemeId);

        if (levelId.HasValue)
            query = query.Where(a => a.GeoLevelId == levelId.Value);

        if (parentId.HasValue)
            query = query.Where(a => a.ParentAreaId == parentId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Name.Contains(term) || a.Code.Contains(term));
        }

        var areas = await query
            .OrderBy(a => a.GeoLevel.LevelNumber)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);

        if (!includeHistorical)
            areas = areas.Where(a => !IsHistorical(a)).ToList();

        var ids = areas.Select(a => a.Id).ToList();
        var childCounts = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.ParentAreaId != null && ids.Contains(a.ParentAreaId.Value))
            .GroupBy(a => a.ParentAreaId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return areas
            .Select(a => Map(a, childCounts.FirstOrDefault(c => c.ParentId == a.Id)?.Count ?? 0))
            .ToList();
    }

    public async Task<GeoAreaDto> GetAreaAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var area = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Include(a => a.GeoLevel)
            .Include(a => a.Scheme)
            .Include(a => a.ParentArea)
            .Include(a => a.SupersededByArea)
            .Include(a => a.Aliases)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted && a.TenantId == tenantId, ct);

        if (area == null)
            throw new ArgumentException($"Area with ID '{id}' not found.");

        var childCount = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.ParentAreaId == id, ct);

        return Map(area, childCount);
    }

    public async Task<IEnumerable<GeoAreaTreeNodeDto>> GetAreaTreeAsync(
        Guid schemeId, bool includeHistorical = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync<GeoScheme>(schemeId, "Division scheme", ct);

        // The whole scheme in one query, assembled in memory. A country's divisions are in the low
        // thousands of rows — one round trip beats a recursive query per node.
        var areas = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Include(a => a.GeoLevel)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.SchemeId == schemeId)
            .OrderBy(a => a.GeoLevel.LevelNumber)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);

        if (!includeHistorical)
            areas = areas.Where(a => !IsHistorical(a)).ToList();

        var nodes = areas.ToDictionary(
            a => a.Id,
            a => new GeoAreaTreeNodeDto
            {
                Id = a.Id,
                GeoLevelId = a.GeoLevelId,
                GeoLevelName = a.GeoLevel?.Name ?? string.Empty,
                LevelNumber = a.GeoLevel?.LevelNumber ?? 0,
                ParentAreaId = a.ParentAreaId,
                Name = a.Name,
                Code = a.Code,
                IsActive = a.IsActive,
                IsHistorical = IsHistorical(a),
            });

        var roots = new List<GeoAreaTreeNodeDto>();
        foreach (var node in nodes.Values)
        {
            // An orphan — parent filtered out as historical, or soft-deleted — is surfaced at the
            // root rather than dropped. Silently losing a branch is the worse failure.
            if (node.ParentAreaId.HasValue && nodes.TryGetValue(node.ParentAreaId.Value, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        return roots.OrderBy(r => r.LevelNumber).ThenBy(r => r.Name).ToList();
    }

    public async Task<IEnumerable<GeoAreaOptionDto>> GetAreaOptionsAsync(
        Guid levelId, Guid? parentId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.GeoLevelId == levelId && a.IsActive)
            // ⚠ Historical areas are never offered. A form must not let someone pick a district
            // that no longer exists; resolving one that a stored record already points at is a
            // different question, answered by ResolveAsync.
            .Where(a => a.EffectiveTo == null || a.EffectiveTo >= today);

        query = parentId.HasValue
            ? query.Where(a => a.ParentAreaId == parentId.Value)
            : query.Where(a => a.ParentAreaId == null);

        return await query
            .OrderBy(a => a.Name)
            .Select(a => new GeoAreaOptionDto
            {
                Id = a.Id,
                Name = a.Name,
                Code = a.Code,
                GeoLevelId = a.GeoLevelId,
                ParentAreaId = a.ParentAreaId,
            })
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<GeoAreaOptionDto>> GetAncestorsAsync(Guid areaId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var chain = await LoadAncestorChainAsync(areaId, tenantId, ct);

        return chain.Select(a => new GeoAreaOptionDto
        {
            Id = a.Id,
            Name = a.Name,
            Code = a.Code,
            GeoLevelId = a.GeoLevelId,
            ParentAreaId = a.ParentAreaId,
        }).ToList();
    }

    public async Task<(string? Region, string? City)> GetAddressSnapshotAsync(
        Guid geoAreaId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        List<GeoArea> chain;
        try
        {
            chain = await LoadAncestorChainAsync(geoAreaId, tenantId, ct);
        }
        catch (ArgumentException)
        {
            // Not found, or another tenant's. The caller keeps whatever the record already said
            // rather than having it blanked by a bad id.
            return (null, null);
        }

        var levelIds = chain.Select(a => a.GeoLevelId).Distinct().ToList();
        var levels = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .Where(l => levelIds.Contains(l.Id))
            .Select(l => new { l.Id, l.LevelNumber })
            .ToListAsync(ct);

        var depthOf = levels.ToDictionary(l => l.Id, l => l.LevelNumber);
        string? NameAtDepth(int depth) =>
            chain.FirstOrDefault(a => depthOf.TryGetValue(a.GeoLevelId, out var d) && d == depth)?.Name;

        // Region is always tier 1 — the broadest tier of any scheme.
        var region = NameAtDepth(1);

        // City is the town where the scheme has one, and the district where it does not. A
        // three-tier country therefore writes its town, a four-tier one writes its town rather than
        // the community, and a two-tier one writes its district — each the nearest thing that
        // reads like a city on a printed address.
        var city = NameAtDepth(3) ?? NameAtDepth(2);

        return (region, city);
    }

    /// <summary>Broadest first, ending with the area itself.</summary>
    private async Task<List<GeoArea>> LoadAncestorChainAsync(Guid areaId, Guid tenantId, CancellationToken ct)
    {
        var chain = new List<GeoArea>();
        var seen = new HashSet<Guid>();
        Guid? cursor = areaId;

        // Bounded by the seen-set rather than trusting the data: a cycle introduced outside this
        // service (a direct SQL edit, a bad import) would otherwise hang the request.
        while (cursor.HasValue && seen.Add(cursor.Value))
        {
            var current = await _unitOfWork.Repository<GeoArea>().GetQueryable()
                .FirstOrDefaultAsync(a => a.Id == cursor.Value && !a.IsDeleted && a.TenantId == tenantId, ct);
            if (current == null) break;

            chain.Insert(0, current);
            cursor = current.ParentAreaId;
        }

        if (chain.Count == 0)
            throw new ArgumentException($"Area with ID '{areaId}' not found.");

        return chain;
    }

    public async Task<IEnumerable<GeoAreaResolutionDto>> ResolveAsync(
        string name, Guid? schemeId = null, Guid? levelId = null, Guid? parentId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Array.Empty<GeoAreaResolutionDto>();

        var tenantId = GetTenantId();
        var term = name.Trim();

        var query = _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Include(a => a.GeoLevel)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

        if (schemeId.HasValue) query = query.Where(a => a.SchemeId == schemeId.Value);
        if (levelId.HasValue) query = query.Where(a => a.GeoLevelId == levelId.Value);
        if (parentId.HasValue) query = query.Where(a => a.ParentAreaId == parentId.Value);

        var byName = await query.Where(a => a.Name == term).ToListAsync(ct);

        // Aliases are consulted only when the name itself does not match, so a current name always
        // beats someone else's former name for the same string.
        var matchedAliases = new Dictionary<Guid, string>();
        if (byName.Count == 0)
        {
            var aliasHits = await _unitOfWork.Repository<GeoAreaAlias>().GetQueryable()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Alias == term)
                .Select(x => new { x.GeoAreaId, x.Alias })
                .ToListAsync(ct);

            if (aliasHits.Count > 0)
            {
                var aliasIds = aliasHits.Select(x => x.GeoAreaId).ToList();
                byName = await query.Where(a => aliasIds.Contains(a.Id)).ToListAsync(ct);
                foreach (var hit in aliasHits)
                    matchedAliases[hit.GeoAreaId] = hit.Alias;
            }
        }

        var results = new List<GeoAreaResolutionDto>();
        foreach (var area in byName)
        {
            var chain = await LoadAncestorChainAsync(area.Id, tenantId, ct);
            results.Add(new GeoAreaResolutionDto
            {
                Id = area.Id,
                Name = area.Name,
                GeoLevelId = area.GeoLevelId,
                GeoLevelName = area.GeoLevel?.Name ?? string.Empty,
                Ancestors = chain.Select(a => new GeoAreaOptionDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Code = a.Code,
                    GeoLevelId = a.GeoLevelId,
                    ParentAreaId = a.ParentAreaId,
                }).ToList(),
                MatchedAlias = matchedAliases.TryGetValue(area.Id, out var alias) ? alias : null,
                IsHistorical = IsHistorical(area),
                SupersededByGeoAreaId = area.SupersededByGeoAreaId,
            });
        }

        return results;
    }

    public async Task<GeoAreaDto> CreateAreaAsync(CreateGeoAreaDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync<GeoScheme>(dto.SchemeId, "Division scheme", ct);
        var level = await GetOwnedAsync<GeoLevel>(dto.GeoLevelId, "Tier", ct);

        if (level.SchemeId != dto.SchemeId)
            throw new InvalidOperationException("That tier belongs to a different division scheme.");

        await RequireUnusedAreaCodeAsync(tenantId, dto.SchemeId, dto.Code, null, ct);
        var parent = await ValidateParentAsync(tenantId, dto.SchemeId, level, dto.ParentAreaId, null, ct);
        await ValidateSupersedesAsync(tenantId, dto.SupersededByGeoAreaId, null, ct);
        ValidateEffectiveRange(dto.EffectiveFrom, dto.EffectiveTo);

        var entity = new GeoArea
        {
            TenantId = tenantId,
            SchemeId = dto.SchemeId,
            GeoLevelId = dto.GeoLevelId,
            ParentAreaId = dto.ParentAreaId,
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim().ToUpperInvariant(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            PolygonCoordinatesJson = Blank(dto.PolygonCoordinatesJson),
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            SupersededByGeoAreaId = dto.SupersededByGeoAreaId,
            IsActive = dto.IsActive,
            Notes = Blank(dto.Notes),
        };

        await _unitOfWork.Repository<GeoArea>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        // Path needs the id, so it is stamped after the insert.
        entity.Path = BuildPath(parent, entity.Id);
        await _unitOfWork.Repository<GeoArea>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetAreaAsync(entity.Id, ct);
    }

    public async Task<GeoAreaDto> UpdateAreaAsync(UpdateGeoAreaDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<GeoArea>(dto.Id, "Area", ct);
        var level = await GetOwnedAsync<GeoLevel>(dto.GeoLevelId, "Tier", ct);

        if (level.SchemeId != entity.SchemeId)
            throw new InvalidOperationException("That tier belongs to a different division scheme.");

        await RequireUnusedAreaCodeAsync(tenantId, entity.SchemeId, dto.Code, dto.Id, ct);
        var parent = await ValidateParentAsync(tenantId, entity.SchemeId, level, dto.ParentAreaId, dto.Id, ct);
        await ValidateSupersedesAsync(tenantId, dto.SupersededByGeoAreaId, dto.Id, ct);
        ValidateEffectiveRange(dto.EffectiveFrom, dto.EffectiveTo);

        var reparented = entity.ParentAreaId != dto.ParentAreaId;

        // ⚠ The scheme is NOT taken from the DTO. An area belongs to the scheme it was created in;
        // moving one between schemes would strand its children.
        entity.GeoLevelId = dto.GeoLevelId;
        entity.ParentAreaId = dto.ParentAreaId;
        entity.Name = dto.Name.Trim();
        entity.Code = dto.Code.Trim().ToUpperInvariant();
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
        entity.PolygonCoordinatesJson = Blank(dto.PolygonCoordinatesJson);
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.SupersededByGeoAreaId = dto.SupersededByGeoAreaId;
        entity.IsActive = dto.IsActive;
        entity.Notes = Blank(dto.Notes);
        entity.Path = BuildPath(parent, entity.Id);

        await _unitOfWork.Repository<GeoArea>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        // ⚠ Every descendant's Path embeds this area's, so a reparent that stops here leaves the
        // whole branch describing an ancestry it no longer has — and Path is what roll-up reads.
        if (reparented)
            await RestampDescendantPathsAsync(entity, tenantId, ct);

        return await GetAreaAsync(entity.Id, ct);
    }

    public async Task<bool> DeleteAreaAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<GeoArea>(id, "Area", ct);

        var children = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.ParentAreaId == id, ct);
        if (children > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' has {children} area{(children == 1 ? "" : "s")} beneath it, so it cannot be "
                + "removed. Remove or move those first. If the area has ceased to exist, end-date it instead — "
                + "that keeps records which already point at it resolvable.");

        var supersedes = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SupersededByGeoAreaId == id, ct);
        if (supersedes > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is named as the successor of {supersedes} historical "
                + $"area{(supersedes == 1 ? "" : "s")}, so it cannot be removed without breaking that trail.");

        // ⚠ The foreign keys do NOT cover this, and assuming they did cost a seeded community and
        // an employee's address on 2026-09-03. Deletes here are SOFT, so no constraint is ever
        // consulted: the row is flagged, vanishes from every read, and each record pointing at it
        // keeps a dangling id while silently losing the address it resolved to. Each consumer
        // answers for itself — see IGeoAreaConsumer for why this is inverted.
        var inUse = new List<string>();
        foreach (var consumer in _consumers)
        {
            var count = await consumer.CountUsagesAsync(id, tenantId, ct);
            if (count > 0)
                inUse.Add($"{count} {(count == 1 ? consumer.ResourceNameSingular : consumer.ResourceName)}");
        }

        if (inUse.Count > 0)
        {
            // "1 site, 1 company profile and 1 healthcare facility" — not "a and b and c".
            var listed = inUse.Count == 1
                ? inUse[0]
                : $"{string.Join(", ", inUse.Take(inUse.Count - 1))} and {inUse[^1]}";

            throw new InvalidOperationException(
                $"'{entity.Name}' is the recorded location of {listed}, so it cannot be "
                + "removed. Move them somewhere else first. If the area has genuinely ceased to exist, "
                + "end-date it instead — that keeps every record which already points at it resolvable.");
        }

        await _unitOfWork.Repository<GeoArea>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private static string BuildPath(GeoArea? parent, Guid id) =>
        parent == null ? $"/{id}" : $"{parent.Path}/{id}";

    private async Task RestampDescendantPathsAsync(GeoArea root, Guid tenantId, CancellationToken ct)
    {
        var all = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.SchemeId == root.SchemeId)
            .ToListAsync(ct);

        var byParent = all
            .Where(a => a.ParentAreaId.HasValue)
            .GroupBy(a => a.ParentAreaId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var queue = new Queue<GeoArea>();
        queue.Enqueue(root);
        var touched = new List<GeoArea>();
        var seen = new HashSet<Guid> { root.Id };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!byParent.TryGetValue(current.Id, out var children)) continue;

            foreach (var child in children)
            {
                if (!seen.Add(child.Id)) continue; // defensive against a pre-existing cycle
                child.Path = $"{current.Path}/{child.Id}";
                touched.Add(child);
                queue.Enqueue(child);
            }
        }

        if (touched.Count == 0) return;

        await _unitOfWork.Repository<GeoArea>().UpdateRangeAsync(touched);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Reparenting area {AreaId} restamped {Count} descendant path(s).", root.Id, touched.Count);
    }

    private async Task RequireUnusedAreaCodeAsync(
        Guid tenantId, Guid schemeId, string code, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = code.Trim().ToUpperInvariant();
        var clash = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .AnyAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SchemeId == schemeId && a.Code == trimmed
                        && (exceptId == null || a.Id != exceptId), ct);
        if (clash)
            throw new InvalidOperationException(
                $"This scheme already has an area with code '{trimmed}'. The code is what makes re-running a "
                + "geography seed a no-op instead of a duplicate, so it has to stay unique.");
    }

    /// <summary>
    /// A parent must be in the same scheme and strictly shallower. The top tier takes no parent;
    /// every other tier requires one, so an orphaned district cannot be created by omission.
    /// </summary>
    private async Task<GeoArea?> ValidateParentAsync(
        Guid tenantId, Guid schemeId, GeoLevel level, Guid? parentId, Guid? selfId, CancellationToken ct)
    {
        var topLevelNumber = await _unitOfWork.Repository<GeoLevel>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SchemeId == schemeId && l.IsActive)
            .MinAsync(l => (int?)l.LevelNumber, ct);

        var isTopTier = topLevelNumber.HasValue && level.LevelNumber == topLevelNumber.Value;

        if (parentId == null)
        {
            if (!isTopTier)
                throw new InvalidOperationException(
                    $"An area at the '{level.Name}' tier needs a parent — only the broadest tier of a scheme "
                    + "stands on its own.");
            return null;
        }

        if (isTopTier)
            throw new InvalidOperationException(
                $"'{level.Name}' is the broadest tier of this scheme, so an area at it cannot sit under another.");

        var parent = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .Include(a => a.GeoLevel)
            .FirstOrDefaultAsync(a => a.Id == parentId.Value && !a.IsDeleted && a.TenantId == tenantId, ct);

        if (parent == null)
            throw new ArgumentException($"Parent area with ID '{parentId}' not found.");

        if (parent.SchemeId != schemeId)
            throw new InvalidOperationException("The parent area belongs to a different division scheme.");

        if (parent.GeoLevel != null && parent.GeoLevel.LevelNumber >= level.LevelNumber)
            throw new InvalidOperationException(
                $"'{parent.Name}' sits at the '{parent.GeoLevel.Name}' tier, which is not above '{level.Name}'. "
                + "A parent has to be broader than its child.");

        // ⚠ Without this, reparenting an area under its own descendant makes an unreachable ring:
        // the rows survive, but the tree walk and every Path roll-up lose the branch.
        if (selfId.HasValue)
        {
            var chain = await LoadAncestorChainAsync(parent.Id, tenantId, ct);
            if (chain.Any(a => a.Id == selfId.Value))
                throw new InvalidOperationException(
                    "An area cannot be moved underneath itself or one of its own descendants.");
        }

        return parent;
    }

    private async Task ValidateSupersedesAsync(Guid tenantId, Guid? supersededById, Guid? selfId, CancellationToken ct)
    {
        if (supersededById == null) return;

        if (selfId.HasValue && supersededById.Value == selfId.Value)
            throw new InvalidOperationException("An area cannot be its own successor.");

        var exists = await _unitOfWork.Repository<GeoArea>().GetQueryable()
            .AnyAsync(a => a.Id == supersededById.Value && !a.IsDeleted && a.TenantId == tenantId, ct);
        if (!exists)
            throw new ArgumentException($"Successor area with ID '{supersededById}' not found.");
    }

    private static void ValidateEffectiveRange(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue && to.Value < from.Value)
            throw new InvalidOperationException("An area cannot stop existing before it started.");
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Aliases
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<GeoAreaAliasDto>> GetAliasesAsync(Guid areaId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync<GeoArea>(areaId, "Area", ct);

        var aliases = await _unitOfWork.Repository<GeoAreaAlias>().GetQueryable()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.GeoAreaId == areaId)
            .OrderBy(x => x.Kind).ThenBy(x => x.Alias)
            .ToListAsync(ct);

        return aliases.Select(Map).ToList();
    }

    public async Task<GeoAreaAliasDto> CreateAliasAsync(CreateGeoAreaAliasDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync<GeoArea>(dto.GeoAreaId, "Area", ct);
        await RequireUnusedAliasAsync(tenantId, dto.GeoAreaId, dto.Alias, null, ct);

        var entity = new GeoAreaAlias
        {
            TenantId = tenantId,
            GeoAreaId = dto.GeoAreaId,
            Alias = dto.Alias.Trim(),
            Kind = dto.Kind,
            Notes = Blank(dto.Notes),
        };

        await _unitOfWork.Repository<GeoAreaAlias>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<GeoAreaAliasDto> UpdateAliasAsync(UpdateGeoAreaAliasDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<GeoAreaAlias>(dto.Id, "Alias", ct);
        await RequireUnusedAliasAsync(tenantId, entity.GeoAreaId, dto.Alias, dto.Id, ct);

        entity.Alias = dto.Alias.Trim();
        entity.Kind = dto.Kind;
        entity.Notes = Blank(dto.Notes);

        await _unitOfWork.Repository<GeoAreaAlias>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<bool> DeleteAliasAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync<GeoAreaAlias>(id, "Alias", ct);
        await _unitOfWork.Repository<GeoAreaAlias>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task RequireUnusedAliasAsync(
        Guid tenantId, Guid areaId, string alias, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = alias.Trim();
        var clash = await _unitOfWork.Repository<GeoAreaAlias>().GetQueryable()
            .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.GeoAreaId == areaId && x.Alias == trimmed
                        && (exceptId == null || x.Id != exceptId), ct);
        if (clash)
            throw new InvalidOperationException($"This area already answers to '{trimmed}'.");
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    //  Mapping
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static GeoSchemeDto Map(GeoScheme e, int levelCount, int areaCount) => new()
    {
        Id = e.Id,
        CountryId = e.CountryId,
        CountryName = e.Country?.Name ?? string.Empty,
        CountryCode = e.Country?.Code ?? string.Empty,
        Name = e.Name,
        Code = e.Code,
        Description = e.Description,
        IsDefault = e.IsDefault,
        IsActive = e.IsActive,
        LevelCount = levelCount,
        AreaCount = areaCount,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };

    private static GeoLevelDto Map(GeoLevel e, string schemeName, int areaCount) => new()
    {
        Id = e.Id,
        SchemeId = e.SchemeId,
        SchemeName = schemeName,
        Name = e.Name,
        Code = e.Code,
        Description = e.Description,
        LevelNumber = e.LevelNumber,
        IsRequiredInAddress = e.IsRequiredInAddress,
        AllowsAddressAssignment = e.AllowsAddressAssignment,
        IsActive = e.IsActive,
        AreaCount = areaCount,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };

    private static GeoAreaDto Map(GeoArea e, int childCount) => new()
    {
        Id = e.Id,
        SchemeId = e.SchemeId,
        SchemeName = e.Scheme?.Name ?? string.Empty,
        GeoLevelId = e.GeoLevelId,
        GeoLevelName = e.GeoLevel?.Name ?? string.Empty,
        LevelNumber = e.GeoLevel?.LevelNumber ?? 0,
        ParentAreaId = e.ParentAreaId,
        ParentAreaName = e.ParentArea?.Name,
        Name = e.Name,
        Code = e.Code,
        Path = e.Path,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        PolygonCoordinatesJson = e.PolygonCoordinatesJson,
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo,
        SupersededByGeoAreaId = e.SupersededByGeoAreaId,
        SupersededByAreaName = e.SupersededByArea?.Name,
        IsActive = e.IsActive,
        Notes = e.Notes,
        IsHistorical = IsHistorical(e),
        ChildCount = childCount,
        Aliases = e.Aliases?.Where(a => !a.IsDeleted).Select(Map).ToList() ?? new List<GeoAreaAliasDto>(),
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };

    private static GeoAreaAliasDto Map(GeoAreaAlias e) => new()
    {
        Id = e.Id,
        GeoAreaId = e.GeoAreaId,
        Alias = e.Alias,
        Kind = e.Kind,
        KindLabel = e.Kind switch
        {
            GeoAreaAliasKind.FormerName => "Former name",
            GeoAreaAliasKind.Spelling => "Alternate spelling",
            GeoAreaAliasKind.Abbreviation => "Abbreviation",
            GeoAreaAliasKind.Vernacular => "Local name",
            _ => "Other",
        },
        Notes = e.Notes,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };
}
