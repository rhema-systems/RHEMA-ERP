using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Manages the tenant's single <see cref="CompanyHrPolicySettings"/> record. Read returns
/// coded defaults when nothing is persisted; update performs a get-or-create upsert scoped
/// to the current tenant.
/// </summary>
public class CompanyHrPolicySettingsService : ICompanyHrPolicySettingsService
{
    private readonly IGenericRepository<CompanyHrPolicySettings> _repository;
    // The salary-structure rules read the live structure: a switch to two-tier has to know whether
    // any grade actually holds more than one level.
    private readonly IGenericRepository<SalaryGrade> _salaryGrades;
    // Same reasoning as the salary structure above: the leave-year rule has to know whether the
    // tenant already holds leave data that is labelled under the current year (decision D-9).
    private readonly IGenericRepository<LeaveRequest> _leaveRequests;
    private readonly IGenericRepository<LeaveBalance> _leaveBalances;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CompanyHrPolicySettingsService> _logger;

    public CompanyHrPolicySettingsService(
        IGenericRepository<CompanyHrPolicySettings> repository,
        IGenericRepository<SalaryGrade> salaryGrades,
        IGenericRepository<LeaveRequest> leaveRequests,
        IGenericRepository<LeaveBalance> leaveBalances,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<CompanyHrPolicySettingsService> logger)
    {
        _repository = repository;
        _salaryGrades = salaryGrades;
        _leaveRequests = leaveRequests;
        _leaveBalances = leaveBalances;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    public Task<CompanyHrPolicySettingsDto> GetAsync(CancellationToken cancellationToken = default)
        => GetForTenantAsync(GetTenantId(), cancellationToken);

    public async Task<CompanyHrPolicySettingsDto> GetForTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required to read the HR policy settings.", nameof(tenantId));

        var entity = await _repository.GetQueryable()
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is not null)
            return entity.ToDto();

        // Nothing persisted yet — surface coded defaults without creating a row.
        return new CompanyHrPolicySettings { TenantId = tenantId }.ToDto();
    }

    /// <summary>
    /// ⚠ <b>The leave year start month is change-once-at-setup</b> (entitlement plan C1,
    /// decision D-9).
    /// </summary>
    /// <remarks>
    /// <para>Moving the boundary changes <b>which leave year some dates fall in</b>, while every
    /// stored row keeps the label it was written with. A request dated 10 February 2026 sits in
    /// leave year 2026 under a January start and in 2025 under an April one — but the balance it
    /// fed still says 2026.</para>
    ///
    /// <para><b>⚠ And one figure cannot be repaired afterwards, which is why this is a refusal
    /// rather than a warning.</b> Used, pending, adjustment and encashed days all re-derive.
    /// Entitlement became repairable in W1a. <c>CarriedOverDays</c> did not: it was computed by a
    /// year-end run against boundaries that no longer exist, and nothing can reconstruct what it
    /// should have been. A change that half-corrects the ledger is worse than one that is refused.
    /// </para>
    ///
    /// <para>Freely settable while the tenant holds no leave data, which is the case during setup —
    /// the only time anybody should be choosing it.</para>
    /// </remarks>
    private async Task ValidateLeaveYearStartMonthAsync(
        Guid tenantId, CompanyHrPolicySettings? existing,
        UpdateCompanyHrPolicySettingsDto dto, CancellationToken ct)
    {
        // ⚠ Leave settings audit 2, L-95: a tenant with no settings row still HAS a leave year — the
        // default — and its leave is labelled under it. This returned early for such a tenant, so its
        // first save could move the boundary under records already written. Nothing changing is the
        // only reason to stop here. The fallback is the same unsaved default CompanyHrPolicyProvider
        // hands every leave reader when the row is missing.
        var current = existing?.LeaveYearStartMonth ?? new CompanyHrPolicySettings().LeaveYearStartMonth;
        if (current == dto.LeaveYearStartMonth) return;

        var requests = await _leaveRequests.GetQueryable()
            .CountAsync(r => r.TenantId == tenantId && !r.IsDeleted, ct);
        var balances = await _leaveBalances.GetQueryable()
            .CountAsync(b => b.TenantId == tenantId && !b.IsDeleted, ct);
        // ⚠ L-95 too: a plan carries its leave year (LeavePlan.Year), and was not counted.
        var plans = await _unitOfWork.Repository<LeavePlan>().GetQueryable()
            .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct);

        if (requests == 0 && balances == 0 && plans == 0) return;

        throw new InvalidOperationException(
            $"The leave year cannot be moved once leave data exists. This company already holds "
            + $"{requests} leave request(s), {balances} leave balance(s) and {plans} leave plan(s), all labelled "
            + $"under a leave year starting in month {current}. Moving the start month "
            + "would change which leave year some dates fall in while those records keep their "
            + "current labels, and a carry-over already run cannot be recomputed against the new "
            + "boundaries. Set it during setup, before any leave is recorded.");
    }

    public async Task<CompanyHrPolicySettingsDto> UpdateAsync(UpdateCompanyHrPolicySettingsDto dto, CancellationToken cancellationToken = default)
    {
        ValidateRetirementAges(dto);
        ValidateConsistency(dto);

        var tenantId = GetTenantId();
        await ValidateSalaryStructureAsync(tenantId, dto, cancellationToken);
        var entity = await _repository.GetQueryable()
            .Where(s => !s.IsDeleted && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        await ValidateLeaveYearStartMonthAsync(tenantId, entity, dto, cancellationToken);

        if (entity is null)
        {
            entity = new CompanyHrPolicySettings { TenantId = tenantId };
            entity.ApplyUpdate(dto);
            entity.StampCreated(_currentUser);
            await _repository.AddAsync(entity);
            _logger.LogInformation("Company HR policy settings created for tenant {TenantId}", entity.TenantId);
        }
        else
        {
            entity.ApplyUpdate(dto);
            entity.StampUpdated(_currentUser);
            await _repository.UpdateAsync(entity);
            _logger.LogInformation("Company HR policy settings updated for tenant {TenantId}", entity.TenantId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    /// <summary>
    /// The two salary-structure switches that would otherwise leave a setting saying something the
    /// data cannot honour.
    /// </summary>
    /// <remarks>
    /// <para><b>Three-tier while Payroll is the source</b> is refused outright. Payroll's structure
    /// is grade → notch with no level concept, and HR's structure writes answer 409 while payroll is
    /// the master — so there would be no door through which a second level could ever be created.
    /// The sentence names both ways out: payroll grows a level tier (an ask to the payroll owner), or
    /// the tenant moves the structure to HR.</para>
    /// <para><b>Two-tier while a grade holds several levels</b> is refused naming the grades. Nothing
    /// is collapsed silently: which level's notches survive is a decision, not a default.</para>
    /// <para>Moving the source from HR back to Payroll is allowed in two-tier: the next read
    /// re-projects, and payroll's rows overwrite HR-authored rows whose codes match. That is what
    /// "payroll is the master" means, and it is said on the settings screen.</para>
    /// </remarks>
    private async Task ValidateSalaryStructureAsync(
        Guid tenantId, UpdateCompanyHrPolicySettingsDto dto, CancellationToken cancellationToken)
    {
        if (dto.SalaryStructureTiers == SalaryStructureTiers.GradeLevelAndNotch
            && dto.SalaryStructureSource == SalaryStructureSource.Payroll)
            throw new InvalidOperationException(
                "Payroll defines the salary structure for this organisation and has no level tier — its scale "
                + "is grade and notch only — so a three-tier structure cannot be maintained while Payroll is the "
                + "source. Either ask the payroll owner for a level tier, or set the salary structure source to HR.");

        if (dto.SalaryStructureTiers == SalaryStructureTiers.GradeAndNotch)
        {
            var crowded = await _salaryGrades.GetQueryable()
                .Where(g => g.TenantId == tenantId && !g.IsDeleted && g.IsActive
                         && g.Levels.Count(l => !l.IsDeleted && l.IsActive) > 1)
                .OrderBy(g => g.Code)
                .Select(g => g.Code)
                .ToListAsync(cancellationToken);

            if (crowded.Count > 0)
                throw new InvalidOperationException(
                    "A two-tier structure has one level per grade, but "
                    + (crowded.Count == 1 ? $"grade {crowded[0]} holds" : $"grades {string.Join(", ", crowded)} hold")
                    + " more than one active level. Retire the extra levels first — which notches survive is a "
                    + "decision, not something to collapse silently.");
        }
    }

    private static void ValidateRetirementAges(UpdateCompanyHrPolicySettingsDto dto)
    {
        if (dto.VoluntaryRetirementAge > dto.CompulsoryRetirementAge)
            throw new InvalidOperationException(
                "Voluntary retirement age cannot exceed the compulsory retirement age.");

        if (dto.UseGenderSpecificRetirementAge &&
            dto.MaleRetirementAge is null && dto.FemaleRetirementAge is null)
            throw new InvalidOperationException(
                "Gender-specific retirement is enabled but no male/female retirement age was provided.");
    }

    /// <summary>
    /// The three rules that stop a saved setting meaning something other than what it says.
    /// </summary>
    /// <remarks>
    /// Each of these was reachable before: the value saved cleanly, the screen read it back, and the
    /// engine behaved as though a different value had been set.
    /// </remarks>
    private static void ValidateConsistency(UpdateCompanyHrPolicySettingsDto dto)
    {
        // All four weights at zero saves fine and then silently loses to FitScoreWeights.Default in
        // SuccessionCandidateSearchService — so the screen would show 0/0/0/0 while succession ranked
        // candidates on 35/30/20/15. The weights are relative and need not sum to anything; they
        // just cannot all be nothing.
        if (dto.FitWeightPerformance + dto.FitWeightCompetency
            + dto.FitWeightPotential + dto.FitWeightTenure <= 0)
            throw new InvalidOperationException(
                "At least one succession fit weight must be greater than zero, "
                + "otherwise candidate ranking silently falls back to the built-in weights.");

        // Free text with a length cap and nothing else. The long-service reader drops unparseable
        // entries rather than throwing — correct of it — but that means "ten,fifteen" saves happily
        // and reads back as no milestones at all.
        if (!string.IsNullOrWhiteSpace(dto.LongServiceMilestoneYears))
        {
            var parts = dto.LongServiceMilestoneYears
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0 || parts.Any(p => !int.TryParse(p, out var n) || n is <= 0 or > 100))
                throw new InvalidOperationException(
                    "Long-service milestone years must be a comma-separated list of whole years "
                    + "between 1 and 100, for example \"5,10,15,20,25\".");
        }

        // Stored uppercased and used wherever no explicit currency is set. A two- or four-character
        // value is not an ISO 4217 code, and the column would truncate a longer one.
        var currency = (dto.DefaultCurrencyCode ?? string.Empty).Trim();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            throw new InvalidOperationException(
                "Default currency code must be a three-letter ISO 4217 code, for example \"GHS\".");
    }
}
