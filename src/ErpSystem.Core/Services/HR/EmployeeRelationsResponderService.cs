using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffGrievance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The employee-relations responder matrix — area 9c slice 5, FR-HR-084. See
/// <see cref="IEmployeeRelationsResponderService"/> for why this is maintained rather than derived.
/// </summary>
public class EmployeeRelationsResponderService : IEmployeeRelationsResponderService
{
    /// <summary>Every rung of FR-HR-181's ladder, for the coverage view.</summary>
    private static readonly GrievanceEscalationLevel[] AllLevels = Enum
        .GetValues<GrievanceEscalationLevel>()
        .OrderBy(l => (int)l)
        .ToArray();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeRelationsResponderService> _logger;

    public EmployeeRelationsResponderService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeRelationsResponderService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IQueryable<EmployeeRelationsResponder> Scoped(Guid tenantId) =>
        _unitOfWork.Repository<EmployeeRelationsResponder>()
            .GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.ResponderEmployee)
            .Include(r => r.OrganizationUnit);

    /// <summary>Whether a row is the one in force on <paramref name="on"/>.</summary>
    private static bool IsInForce(EmployeeRelationsResponder r, DateTime on)
        => (r.EffectiveFrom == null || r.EffectiveFrom <= on)
           && (r.EffectiveTo == null || r.EffectiveTo >= on);

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<EmployeeRelationsResponderDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(GetTenantId()).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Defaults first — they are what applies where nothing more specific exists, so a reader
        // scanning the screen sees the fallback before the exceptions to it.
        return rows
            .OrderBy(r => r.OrganizationUnitId.HasValue)
            .ThenBy(r => r.OrganizationUnit?.Name)
            .ThenBy(r => (int)r.Level)
            .ThenBy(r => r.EffectiveFrom ?? DateTime.MinValue)
            .Select(r => ToDto(r, now))
            .ToList();
    }

    public async Task<IEnumerable<EmployeeRelationsResponderDto>> GetForUnitAsync(
        Guid? organizationUnitId, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(GetTenantId())
            .Where(r => r.OrganizationUnitId == organizationUnitId)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        return rows
            .OrderBy(r => (int)r.Level)
            .ThenBy(r => r.EffectiveFrom ?? DateTime.MinValue)
            .Select(r => ToDto(r, now))
            .ToList();
    }

    public async Task<ResponderResolutionDto> ResolveAsync(
        Guid? organizationUnitId, GrievanceEscalationLevel level, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var candidates = await Scoped(GetTenantId())
            .Where(r => r.Level == level
                        && (r.OrganizationUnitId == organizationUnitId || r.OrganizationUnitId == null))
            .ToListAsync(cancellationToken);

        var inForce = candidates.Where(r => IsInForce(r, now)).ToList();

        // Most specific first: a unit's own row beats the tenant default, always.
        var match = inForce.FirstOrDefault(r => r.OrganizationUnitId == organizationUnitId && organizationUnitId != null)
                    ?? inForce.FirstOrDefault(r => r.OrganizationUnitId == null);

        // ⚠ An unmatched lookup returns "None" rather than throwing. The admin screen's whole
        // purpose is showing WHERE the matrix resolves to nobody, and a lookup that throws cannot
        // be rendered as a gap.
        return new ResponderResolutionDto
        {
            OrganizationUnitId = organizationUnitId,
            Level = level,
            ResponderEmployeeId = match?.ResponderEmployeeId,
            ResponderName = match?.ResponderEmployee?.FullName,
            ResolvedBy = match == null ? "None" : match.OrganizationUnitId == null ? "Default" : "Unit",
        };
    }

    public async Task<ResponderCoverageDto> GetCoverageAsync(
        Guid? organizationUnitId, CancellationToken cancellationToken = default)
    {
        var levels = new List<ResponderResolutionDto>();
        foreach (var level in AllLevels)
            levels.Add(await ResolveAsync(organizationUnitId, level, cancellationToken));

        string? unitName = null;
        if (organizationUnitId is Guid unitId)
        {
            var unit = await _unitOfWork.Repository<OrganizationUnit>().GetByIdAsync(unitId);
            unitName = unit?.Name;
        }

        return new ResponderCoverageDto
        {
            OrganizationUnitId = organizationUnitId,
            OrganizationUnitName = unitName,
            Levels = levels,
        };
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    public async Task<EmployeeRelationsResponderDto> CreateAsync(
        UpsertEmployeeRelationsResponderDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await ValidateAsync(tenantId, dto, existingId: null, cancellationToken);

        var row = new EmployeeRelationsResponder
        {
            TenantId = tenantId,
            OrganizationUnitId = dto.OrganizationUnitId,
            Level = dto.Level,
            ResponderEmployeeId = dto.ResponderEmployeeId,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
        };

        await _unitOfWork.Repository<EmployeeRelationsResponder>().AddAsync(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ER responder named for {Level} in {Scope}",
            dto.Level, dto.OrganizationUnitId?.ToString() ?? "all units");

        return ToDto(await GetOwnedAsync(row.Id, cancellationToken), DateTime.UtcNow);
    }

    public async Task<EmployeeRelationsResponderDto> UpdateAsync(
        Guid id, UpsertEmployeeRelationsResponderDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await GetOwnedAsync(id, cancellationToken);
        await ValidateAsync(tenantId, dto, existingId: id, cancellationToken);

        row.OrganizationUnitId = dto.OrganizationUnitId;
        row.Level = dto.Level;
        row.ResponderEmployeeId = dto.ResponderEmployeeId;
        row.EffectiveFrom = dto.EffectiveFrom;
        row.EffectiveTo = dto.EffectiveTo;
        row.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        row.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(id, cancellationToken), DateTime.UtcNow);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await GetOwnedAsync(id, cancellationToken);
        var now = DateTime.UtcNow;

        // Soft delete: who answered a rung last year is a fact about cases decided last year, and
        // the matrix is read by the audit trail as well as by the router.
        row.IsDeleted = true;
        row.DeletedAt = now;
        row.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<EmployeeRelationsResponder> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
        => await Scoped(GetTenantId()).FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
           ?? throw new ArgumentException($"Responder assignment with ID '{id}' was not found.");

    private async Task ValidateAsync(
        Guid tenantId, UpsertEmployeeRelationsResponderDto dto, Guid? existingId, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(dto.ResponderEmployeeId);
        if (employee == null || employee.IsDeleted || employee.TenantId != tenantId)
            throw new ArgumentException($"The responder employee with ID '{dto.ResponderEmployeeId}' was not found.");

        if (dto.OrganizationUnitId is Guid unitId)
        {
            var unit = await _unitOfWork.Repository<OrganizationUnit>().GetByIdAsync(unitId);
            if (unit == null || unit.IsDeleted || unit.TenantId != tenantId)
                throw new ArgumentException($"The organisation unit with ID '{unitId}' was not found.");
        }

        if (dto.EffectiveFrom.HasValue && dto.EffectiveTo.HasValue && dto.EffectiveTo < dto.EffectiveFrom)
            throw new InvalidOperationException("The end of the effective period cannot be before its start.");

        // ⚠ Overlap, not uniqueness. A slot legitimately has several rows over time — an acting
        // arrangement while the usual responder is on leave is exactly the case this table is for —
        // so what must be refused is two rows in force on the SAME DAY, which no unique index can
        // express. Two people answering one rung for one unit on one day is not a policy; it is a
        // bug that would resolve arbitrarily depending on row order.
        var siblings = await _unitOfWork.Repository<EmployeeRelationsResponder>()
            .GetQueryable()
            // ⚠ Include the employee: the refusal below names whoever already holds the slot, and
            // without this it read "Somebody already answers Supervisor for this scope", which tells
            // an HR officer nothing about whose assignment they have to end first.
            .Include(r => r.ResponderEmployee)
            .Where(r => r.TenantId == tenantId && !r.IsDeleted
                        && r.Level == dto.Level
                        && r.OrganizationUnitId == dto.OrganizationUnitId)
            .ToListAsync(cancellationToken);

        var clash = siblings
            .Where(r => existingId == null || r.Id != existingId)
            .FirstOrDefault(r => Overlaps(r.EffectiveFrom, r.EffectiveTo, dto.EffectiveFrom, dto.EffectiveTo));

        if (clash != null)
            throw new InvalidOperationException(
                $"{clash.ResponderEmployee?.FullName ?? "Somebody"} already answers {dto.Level} for this "
                + "scope over an overlapping period. End that assignment first, or choose different dates.");
    }

    /// <summary>Two open-ended-capable date ranges overlap. Null start = -∞, null end = +∞.</summary>
    private static bool Overlaps(DateTime? aFrom, DateTime? aTo, DateTime? bFrom, DateTime? bTo)
        => (aFrom ?? DateTime.MinValue) <= (bTo ?? DateTime.MaxValue)
           && (bFrom ?? DateTime.MinValue) <= (aTo ?? DateTime.MaxValue);

    private static EmployeeRelationsResponderDto ToDto(EmployeeRelationsResponder r, DateTime now) => new()
    {
        Id = r.Id,
        OrganizationUnitId = r.OrganizationUnitId,
        OrganizationUnitName = r.OrganizationUnit?.Name,
        Level = r.Level,
        ResponderEmployeeId = r.ResponderEmployeeId,
        ResponderName = r.ResponderEmployee?.FullName,
        ResponderEmployeeNumber = r.ResponderEmployee?.EmployeeNumber,
        EffectiveFrom = r.EffectiveFrom,
        EffectiveTo = r.EffectiveTo,
        Notes = r.Notes,
        IsCurrent = IsInForce(r, now),
    };
}
