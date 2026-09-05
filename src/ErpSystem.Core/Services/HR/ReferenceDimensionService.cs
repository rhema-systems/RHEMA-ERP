using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The three reference dimensions lane 3b introduced: the qualification ladder, the certifying-body
/// catalogue, and the per-register staff-numbering rules.
/// </summary>
public interface IReferenceDimensionService
{
    Task<IEnumerable<QualificationLevelDto>> GetQualificationLevelsAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<QualificationLevelDto> CreateQualificationLevelAsync(CreateQualificationLevelDto dto, CancellationToken ct = default);
    Task<QualificationLevelDto> UpdateQualificationLevelAsync(UpdateQualificationLevelDto dto, CancellationToken ct = default);
    Task<bool> DeleteQualificationLevelAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<CertifyingBodyDto>> GetCertifyingBodiesAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<CertifyingBodyDto> CreateCertifyingBodyAsync(CreateCertifyingBodyDto dto, CancellationToken ct = default);
    Task<CertifyingBodyDto> UpdateCertifyingBodyAsync(UpdateCertifyingBodyDto dto, CancellationToken ct = default);
    Task<bool> DeleteCertifyingBodyAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<StaffNumberFormatDto>> GetStaffNumberFormatsAsync(CancellationToken ct = default);
    Task<StaffNumberFormatDto> CreateStaffNumberFormatAsync(CreateStaffNumberFormatDto dto, CancellationToken ct = default);
    Task<StaffNumberFormatDto> UpdateStaffNumberFormatAsync(UpdateStaffNumberFormatDto dto, CancellationToken ct = default);
    Task<bool> DeleteStaffNumberFormatAsync(Guid id, CancellationToken ct = default);
    string PreviewStaffNumberFormat(PreviewStaffNumberFormatDto dto);
}

public class ReferenceDimensionService : IReferenceDimensionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ReferenceDimensionService> _logger;

    public ReferenceDimensionService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ReferenceDimensionService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
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

    // ═══════════════════════════════════════════════════════════════════════
    //  Qualification levels
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<QualificationLevelDto>> GetQualificationLevelsAsync(
        bool activeOnly = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var levels = await _unitOfWork.Repository<QualificationLevel>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && (!activeOnly || l.IsActive))
            // Ordered by Rank, because the ladder's whole purpose is that it has an order. A list
            // sorted by name would present it as just another unordered lookup.
            .OrderBy(l => l.Rank).ThenBy(l => l.Name)
            .ToListAsync(ct);

        // Counted so a screen can warn before retiring a rung that qualifications still sit on.
        var counts = await _unitOfWork.Repository<Qualification>().GetQueryable()
            .Where(q => q.TenantId == tenantId && !q.IsDeleted && q.QualificationLevelId != null)
            .GroupBy(q => q.QualificationLevelId!.Value)
            .Select(g => new { LevelId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return levels.Select(l => Map(l, counts.FirstOrDefault(c => c.LevelId == l.Id)?.Count ?? 0)).ToList();
    }

    public async Task<QualificationLevelDto> CreateQualificationLevelAsync(
        CreateQualificationLevelDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireUnusedLevelNameAsync(tenantId, dto.Name, null, ct);

        var entity = new QualificationLevel
        {
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Rank = dto.Rank,
            IsActive = dto.IsActive,
        };

        await _unitOfWork.Repository<QualificationLevel>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(entity, 0);
    }

    public async Task<QualificationLevelDto> UpdateQualificationLevelAsync(
        UpdateQualificationLevelDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<QualificationLevel>(dto.Id, "Qualification level", ct);
        await RequireUnusedLevelNameAsync(tenantId, dto.Name, dto.Id, ct);

        entity.Name = dto.Name.Trim();
        entity.Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.Rank = dto.Rank;
        entity.IsActive = dto.IsActive;

        await _unitOfWork.Repository<QualificationLevel>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        var count = await _unitOfWork.Repository<Qualification>().GetQueryable()
            .CountAsync(q => q.TenantId == tenantId && !q.IsDeleted && q.QualificationLevelId == entity.Id, ct);
        return Map(entity, count);
    }

    public async Task<bool> DeleteQualificationLevelAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<QualificationLevel>(id, "Qualification level", ct);

        // ⚠ Refused while qualifications still sit on the rung. The FK is Restrict, so the database
        // would refuse anyway — but with a constraint-violation message nobody can act on. This says
        // how many and what to do about them.
        var inUse = await _unitOfWork.Repository<Qualification>().GetQueryable()
            .CountAsync(q => q.TenantId == tenantId && !q.IsDeleted && q.QualificationLevelId == id, ct);
        if (inUse > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is the level of {inUse} qualification{(inUse == 1 ? "" : "s")}, so it cannot be "
                + "removed. Move them to another level first, or make this one inactive to keep it off new records "
                + "while leaving the existing ones as they are.");

        await _unitOfWork.Repository<QualificationLevel>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task RequireUnusedLevelNameAsync(Guid tenantId, string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var clash = await _unitOfWork.Repository<QualificationLevel>().GetQueryable()
            .AnyAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.Name == trimmed
                        && (exceptId == null || l.Id != exceptId), ct);
        if (clash)
            throw new InvalidOperationException($"A qualification level called '{trimmed}' already exists.");
    }

    private static QualificationLevelDto Map(QualificationLevel e, int qualificationCount) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Name = e.Name,
        Code = e.Code,
        Description = e.Description,
        Rank = e.Rank,
        IsActive = e.IsActive,
        QualificationCount = qualificationCount,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  Certifying bodies
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<CertifyingBodyDto>> GetCertifyingBodiesAsync(
        bool activeOnly = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var bodies = await _unitOfWork.Repository<CertifyingBody>().GetQueryable()
            .Include(b => b.Country)
            .Where(b => b.TenantId == tenantId && !b.IsDeleted && (!activeOnly || b.IsActive))
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

        return bodies.Select(Map).ToList();
    }

    public async Task<CertifyingBodyDto> CreateCertifyingBodyAsync(
        CreateCertifyingBodyDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireUnusedBodyNameAsync(tenantId, dto.Name, null, ct);

        var entity = new CertifyingBody
        {
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            Abbreviation = string.IsNullOrWhiteSpace(dto.Abbreviation) ? null : dto.Abbreviation.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            CountryId = dto.CountryId,
            Website = string.IsNullOrWhiteSpace(dto.Website) ? null : dto.Website.Trim(),
            IsActive = dto.IsActive,
        };

        await _unitOfWork.Repository<CertifyingBody>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReadBackBodyAsync(entity.Id, ct);
    }

    public async Task<CertifyingBodyDto> UpdateCertifyingBodyAsync(
        UpdateCertifyingBodyDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<CertifyingBody>(dto.Id, "Certifying body", ct);
        await RequireUnusedBodyNameAsync(tenantId, dto.Name, dto.Id, ct);

        entity.Name = dto.Name.Trim();
        entity.Abbreviation = string.IsNullOrWhiteSpace(dto.Abbreviation) ? null : dto.Abbreviation.Trim();
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.CountryId = dto.CountryId;
        entity.Website = string.IsNullOrWhiteSpace(dto.Website) ? null : dto.Website.Trim();
        entity.IsActive = dto.IsActive;

        await _unitOfWork.Repository<CertifyingBody>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReadBackBodyAsync(entity.Id, ct);
    }

    public async Task<bool> DeleteCertifyingBodyAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<CertifyingBody>(id, "Certifying body", ct);

        var inUse = await _unitOfWork.Repository<EmployeeSkill>().GetQueryable()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.CertifyingBodyId == id, ct);
        if (inUse > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' certified {inUse} recorded skill{(inUse == 1 ? "" : "s")}, so it cannot be removed. "
                + "Make it inactive instead — it will stay off new records and the existing ones keep their certifier.");

        await _unitOfWork.Repository<CertifyingBody>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task RequireUnusedBodyNameAsync(Guid tenantId, string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var clash = await _unitOfWork.Repository<CertifyingBody>().GetQueryable()
            .AnyAsync(b => b.TenantId == tenantId && !b.IsDeleted && b.Name == trimmed
                        && (exceptId == null || b.Id != exceptId), ct);
        if (clash)
            throw new InvalidOperationException($"A certifying body called '{trimmed}' already exists.");
    }

    /// <summary>Re-reads with the country included, so the write response names it.</summary>
    /// <remarks>
    /// ⚠ A freshly-constructed entity has no navigation loaded, so mapping it straight back would
    /// answer with a null country name while the list beside it resolves one — the write-response
    /// shape this module has met repeatedly.
    /// </remarks>
    private async Task<CertifyingBodyDto> ReadBackBodyAsync(Guid id, CancellationToken ct)
    {
        var saved = await _unitOfWork.Repository<CertifyingBody>().GetQueryable()
            .Include(b => b.Country)
            .FirstAsync(b => b.Id == id, ct);
        return Map(saved);
    }

    private static CertifyingBodyDto Map(CertifyingBody e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Name = e.Name,
        Abbreviation = e.Abbreviation,
        Description = e.Description,
        CountryId = e.CountryId,
        CountryName = e.Country?.Name,
        Website = e.Website,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  Staff number formats
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<StaffNumberFormatDto>> GetStaffNumberFormatsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var rules = await _unitOfWork.Repository<StaffNumberFormat>().GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            // The default rule last: it is the fallback, and reading it first implies it wins.
            .OrderBy(r => r.AppliesToEmploymentType == null).ThenBy(r => r.Name)
            .ToListAsync(ct);

        return rules.Select(Map).ToList();
    }

    public async Task<StaffNumberFormatDto> CreateStaffNumberFormatAsync(
        CreateStaffNumberFormatDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireUnclaimedRegisterAsync(tenantId, dto.AppliesToEmploymentType, null, dto.IsActive, ct);
        await RequireDistinguishableFormatAsync(tenantId, dto, null, ct);

        var entity = new StaffNumberFormat
        {
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            AppliesToEmploymentType = dto.AppliesToEmploymentType,
            Prefix = dto.Prefix?.Trim() ?? "",
            Separator = dto.Separator ?? "",
            IncludeYear = dto.IncludeYear,
            YearDigits = dto.YearDigits,
            SequenceDigits = dto.SequenceDigits,
            Suffix = dto.Suffix?.Trim() ?? "",
            AutoGenerate = dto.AutoGenerate,
            SequenceKey = dto.SequenceKey.Trim(),
            IsActive = dto.IsActive,
        };

        await _unitOfWork.Repository<StaffNumberFormat>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<StaffNumberFormatDto> UpdateStaffNumberFormatAsync(
        UpdateStaffNumberFormatDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync<StaffNumberFormat>(dto.Id, "Staff number format", ct);
        await RequireUnclaimedRegisterAsync(tenantId, dto.AppliesToEmploymentType, dto.Id, dto.IsActive, ct);
        await RequireDistinguishableFormatAsync(tenantId, dto, dto.Id, ct);

        entity.Name = dto.Name.Trim();
        entity.AppliesToEmploymentType = dto.AppliesToEmploymentType;
        entity.Prefix = dto.Prefix?.Trim() ?? "";
        entity.Separator = dto.Separator ?? "";
        entity.IncludeYear = dto.IncludeYear;
        entity.YearDigits = dto.YearDigits;
        entity.SequenceDigits = dto.SequenceDigits;
        entity.Suffix = dto.Suffix?.Trim() ?? "";
        entity.AutoGenerate = dto.AutoGenerate;
        entity.SequenceKey = dto.SequenceKey.Trim();
        entity.IsActive = dto.IsActive;

        await _unitOfWork.Repository<StaffNumberFormat>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<bool> DeleteStaffNumberFormatAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync<StaffNumberFormat>(id, "Staff number format", ct);

        // Deleting a rule is allowed and means "this register goes back to manual" — the absence of
        // a rule IS the manual setting. Numbers already issued under it are unaffected: they are
        // recorded on the employee, not derived from the rule.
        await _unitOfWork.Repository<StaffNumberFormat>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public string PreviewStaffNumberFormat(PreviewStaffNumberFormatDto dto)
    {
        var probe = new StaffNumberFormat
        {
            Prefix = dto.Prefix ?? "",
            Separator = dto.Separator ?? "",
            IncludeYear = dto.IncludeYear,
            YearDigits = dto.YearDigits,
            SequenceDigits = dto.SequenceDigits,
            Suffix = dto.Suffix ?? "",
        };
        return probe.Example(DateTime.UtcNow.Year);
    }

    /// <summary>One active rule per register, so "which number does this person get" has one answer.</summary>
    private async Task RequireUnclaimedRegisterAsync(
        Guid tenantId, EmploymentType? register, Guid? exceptId, bool isActive, CancellationToken ct)
    {
        // Only active rules compete. An inactive one governs nothing, so it cannot conflict.
        if (!isActive) return;

        var clash = await _unitOfWork.Repository<StaffNumberFormat>().GetQueryable()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive
                                   && r.AppliesToEmploymentType == register
                                   && (exceptId == null || r.Id != exceptId), ct);
        if (clash == null) return;

        throw new InvalidOperationException(register == null
            ? $"'{clash.Name}' is already the default numbering rule, and there can only be one — two would "
              + "leave it undecided which number a new employee gets. Edit that rule, or retire it first."
            : $"'{clash.Name}' already numbers {register} staff, and there can only be one rule per register. "
              + "Edit that rule, or retire it first.");
    }

    /// <summary>
    /// Refuses two active rules that would compose the same number for the same counter value.
    /// </summary>
    /// <remarks>
    /// ⚠ Each register counts on its own sequence, so two registers WILL reach the same counter
    /// value. That is harmless only while their formats differ — otherwise both produce the same
    /// string and the second one collides on the unique index, surfacing as a failed create for
    /// whichever employee happened to be second.
    /// </remarks>
    private async Task RequireDistinguishableFormatAsync(
        Guid tenantId, CreateStaffNumberFormatDto dto, Guid? exceptId, CancellationToken ct)
    {
        if (!dto.IsActive) return;

        var others = await _unitOfWork.Repository<StaffNumberFormat>().GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive
                     && (exceptId == null || r.Id != exceptId))
            .ToListAsync(ct);

        var candidate = new StaffNumberFormat
        {
            Prefix = dto.Prefix?.Trim() ?? "",
            Separator = dto.Separator ?? "",
            IncludeYear = dto.IncludeYear,
            YearDigits = dto.YearDigits,
            SequenceDigits = dto.SequenceDigits,
            Suffix = dto.Suffix?.Trim() ?? "",
        };

        var year = DateTime.UtcNow.Year;
        var sample = candidate.Compose(1, year);

        var twin = others.FirstOrDefault(o => o.Compose(1, year) == sample);
        if (twin != null)
            throw new InvalidOperationException(
                $"This format produces '{sample}', which is exactly what '{twin.Name}' produces. Each register "
                + "counts separately, so two registers sharing a format will eventually issue the same number to "
                + "two people. Give this one a different prefix, separator or width.");
    }

    private static StaffNumberFormatDto Map(StaffNumberFormat e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Name = e.Name,
        AppliesToEmploymentType = e.AppliesToEmploymentType,
        AppliesToName = e.AppliesToEmploymentType?.ToString() ?? "All other staff",
        Prefix = e.Prefix,
        Separator = e.Separator,
        IncludeYear = e.IncludeYear,
        YearDigits = e.YearDigits,
        SequenceDigits = e.SequenceDigits,
        Suffix = e.Suffix,
        AutoGenerate = e.AutoGenerate,
        SequenceKey = e.SequenceKey,
        IsActive = e.IsActive,
        Example = e.Example(DateTime.UtcNow.Year),
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };

    // ═══════════════════════════════════════════════════════════════════════

    private async Task<T> GetOwnedAsync<T>(Guid id, string label, CancellationToken ct)
        where T : Entities.TenantEntity
    {
        var entity = await _unitOfWork.Repository<T>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"{label} with ID '{id}' not found.");

        return entity;
    }
}
