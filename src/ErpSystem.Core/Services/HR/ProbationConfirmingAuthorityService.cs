using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Maintains and resolves the confirming-authority map — who signs off probation for a given unit
/// and staff category (FR-HR-032, decision D-2).
/// </summary>
public class ProbationConfirmingAuthorityService : IProbationConfirmingAuthorityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ProbationConfirmingAuthorityService> _logger;

    public ProbationConfirmingAuthorityService(
        IUnitOfWork unitOfWork,
        IEmployeeRepository employeeRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<ProbationConfirmingAuthorityService> logger)
    {
        _unitOfWork = unitOfWork;
        _employeeRepository = employeeRepository;
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

    private IQueryable<ProbationConfirmingAuthority> Scoped(Guid tenantId)
        => _unitOfWork.Repository<ProbationConfirmingAuthority>().GetQueryable()
            .Include(a => a.OrganizationUnit)
            .Include(a => a.StaffLevel)
            .Include(a => a.AuthorityEmployee)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ProbationConfirmingAuthorityDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(GetTenantId()).ToListAsync(cancellationToken);
        // Most specific first, so the list reads in the order resolution applies it.
        return rows.Select(ToDto).OrderByDescending(d => d.Specificity).ThenBy(d => d.Scope).ToList();
    }

    public async Task<ProbationConfirmingAuthorityDto> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => ToDto(await GetOwnedAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<ResolvedConfirmingAuthorityDto> ResolveForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var employee = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw ProbationWorkflowException.NotFound($"Employee '{employeeId}' was not found.");

        var resolved = await ResolveInternalAsync(tenantId, employee, cancellationToken);

        return new ResolvedConfirmingAuthorityDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            AuthorityEmployeeId = resolved?.AuthorityEmployeeId,
            AuthorityEmployeeName = resolved?.AuthorityEmployee?.FullName,
            MatchedRuleId = resolved?.Id,
            MatchedScope = resolved is null ? null : DescribeScope(resolved),
            MatchedSpecificity = resolved is null ? null : SpecificityOf(resolved),
            UnresolvedReason = resolved is not null
                ? null
                : employee.OrganizationUnitId is null
                    ? "This employee is not assigned to an organisation unit, and no tenant-wide confirming authority is set."
                    : "No confirming authority covers this employee's unit or staff category, and no tenant-wide default is set.",
        };
    }

    /// <summary>
    /// The matching rule for an employee, most specific first, or null.
    /// </summary>
    /// <remarks>
    /// Returns null rather than falling back to "HR" on purpose. A silent fallback would make an
    /// unconfigured tenant look configured, and FR-HR-032's routing would appear to work while
    /// going nowhere in particular — the failure mode this whole table exists to avoid.
    /// </remarks>
    public async Task<ProbationConfirmingAuthority?> ResolveInternalAsync(
        Guid tenantId, Employee employee, CancellationToken cancellationToken = default)
    {
        var unitId = employee.OrganizationUnitId;
        var levelId = employee.Position?.StaffLevelId;

        var candidates = await Scoped(tenantId)
            .Where(a => a.IsActive
                     && (a.OrganizationUnitId == null || a.OrganizationUnitId == unitId)
                     && (a.StaffLevelId == null || a.StaffLevelId == levelId))
            .ToListAsync(cancellationToken);

        return candidates
            .OrderByDescending(SpecificityOf)
            .FirstOrDefault();
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    public async Task<ProbationConfirmingAuthorityDto> CreateAsync(
        CreateProbationConfirmingAuthorityDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await EnsureAuthorityEmployeeExistsAsync(tenantId, dto.AuthorityEmployeeId);

        // ⚠ Checked here as well as by the unique index, so the caller gets a sentence rather than
        // a SQL violation — and the check counts only LIVE rows, matching the index's IsDeleted filter.
        var clash = await Scoped(tenantId).AnyAsync(
            a => a.OrganizationUnitId == dto.OrganizationUnitId && a.StaffLevelId == dto.StaffLevelId,
            cancellationToken);
        if (clash)
            throw ProbationWorkflowException.Conflict(
                "A confirming authority is already set for that unit and staff category. Edit the existing rule instead of adding a second.");

        var entity = new ProbationConfirmingAuthority
        {
            TenantId = tenantId,
            OrganizationUnitId = dto.OrganizationUnitId,
            StaffLevelId = dto.StaffLevelId,
            AuthorityEmployeeId = dto.AuthorityEmployeeId,
            IsActive = dto.IsActive,
            Notes = dto.Notes,
        };

        await _unitOfWork.Repository<ProbationConfirmingAuthority>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Probation confirming authority {Id} set to employee {AuthorityId}", entity.Id, dto.AuthorityEmployeeId);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ProbationConfirmingAuthorityDto> UpdateAsync(
        Guid id, UpdateProbationConfirmingAuthorityDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync(id, cancellationToken);
        await EnsureAuthorityEmployeeExistsAsync(tenantId, dto.AuthorityEmployeeId);

        entity.AuthorityEmployeeId = dto.AuthorityEmployeeId;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ProbationConfirmingAuthority> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await Scoped(GetTenantId()).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return entity ?? throw ProbationWorkflowException.NotFound($"Confirming authority rule '{id}' was not found.");
    }

    private async Task EnsureAuthorityEmployeeExistsAsync(Guid tenantId, Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw ProbationWorkflowException.NotFound(
                $"Employee '{employeeId}' was not found, so they cannot be named as a confirming authority.");
    }

    private static int SpecificityOf(ProbationConfirmingAuthority a)
        => (a.OrganizationUnitId.HasValue ? 2 : 0) + (a.StaffLevelId.HasValue ? 1 : 0);

    private static string DescribeScope(ProbationConfirmingAuthority a)
    {
        var unit = a.OrganizationUnit?.Name ?? (a.OrganizationUnitId.HasValue ? "Unit" : "All units");
        var level = a.StaffLevel?.Name ?? (a.StaffLevelId.HasValue ? "Staff level" : "all levels");
        return $"{unit} — {level}";
    }

    private static ProbationConfirmingAuthorityDto ToDto(ProbationConfirmingAuthority a) => new()
    {
        Id = a.Id,
        OrganizationUnitId = a.OrganizationUnitId,
        OrganizationUnitName = a.OrganizationUnit?.Name,
        StaffLevelId = a.StaffLevelId,
        StaffLevelName = a.StaffLevel?.Name,
        AuthorityEmployeeId = a.AuthorityEmployeeId,
        AuthorityEmployeeName = a.AuthorityEmployee?.FullName ?? string.Empty,
        AuthorityEmployeeNumber = a.AuthorityEmployee?.EmployeeNumber ?? string.Empty,
        IsActive = a.IsActive,
        Notes = a.Notes,
        Specificity = SpecificityOf(a),
        Scope = DescribeScope(a),
    };
}
