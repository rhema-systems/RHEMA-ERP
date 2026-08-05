using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Benefits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Employee benefit enrollment service. Owns the in-force enrollment ledger, the reconcile-from-
/// position-entitlements operation, and the Benefit→Payroll bridge. Valuation is delegated to
/// <see cref="GhanaBikValuator"/>; basic pay / cash emoluments come from <see cref="IEmolumentService"/>.
/// </summary>
public class EmployeeBenefitEnrollmentService : IEmployeeBenefitEnrollmentService
{
    private readonly IGenericRepository<EmployeeBenefitEnrollment> _enrollmentRepository;
    private readonly IBenefitPolicyRepository _benefitPolicyRepository;
    private readonly IGenericRepository<EmployeePositionBenefit> _positionBenefitRepository;
    private readonly IGenericRepository<BenefitGradeValue> _gradeValueRepository;
    private readonly IGenericRepository<EmployeeDependentBenefit> _dependentBenefitRepository;
    private readonly IGenericRepository<EmployeeDependent> _employeeDependentRepository;
    private readonly IGenericRepository<BenefitBeneficiary> _beneficiaryRepository;
    private readonly IGenericRepository<BenefitUtilization> _utilizationRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IEmolumentService _emolumentService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeBenefitEnrollmentService> _logger;

    public EmployeeBenefitEnrollmentService(
        IGenericRepository<EmployeeBenefitEnrollment> enrollmentRepository,
        IBenefitPolicyRepository benefitPolicyRepository,
        IGenericRepository<EmployeePositionBenefit> positionBenefitRepository,
        IGenericRepository<BenefitGradeValue> gradeValueRepository,
        IGenericRepository<EmployeeDependentBenefit> dependentBenefitRepository,
        IGenericRepository<EmployeeDependent> employeeDependentRepository,
        IGenericRepository<BenefitBeneficiary> beneficiaryRepository,
        IGenericRepository<BenefitUtilization> utilizationRepository,
        IGenericRepository<Employee> employeeRepository,
        IEmolumentService emolumentService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeBenefitEnrollmentService> logger)
    {
        _enrollmentRepository = enrollmentRepository ?? throw new ArgumentNullException(nameof(enrollmentRepository));
        _benefitPolicyRepository = benefitPolicyRepository ?? throw new ArgumentNullException(nameof(benefitPolicyRepository));
        _positionBenefitRepository = positionBenefitRepository ?? throw new ArgumentNullException(nameof(positionBenefitRepository));
        _gradeValueRepository = gradeValueRepository ?? throw new ArgumentNullException(nameof(gradeValueRepository));
        _dependentBenefitRepository = dependentBenefitRepository ?? throw new ArgumentNullException(nameof(dependentBenefitRepository));
        _employeeDependentRepository = employeeDependentRepository ?? throw new ArgumentNullException(nameof(employeeDependentRepository));
        _beneficiaryRepository = beneficiaryRepository ?? throw new ArgumentNullException(nameof(beneficiaryRepository));
        _utilizationRepository = utilizationRepository ?? throw new ArgumentNullException(nameof(utilizationRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _emolumentService = emolumentService ?? throw new ArgumentNullException(nameof(emolumentService));
        _currentUserProvider = currentUserProvider ?? throw new ArgumentNullException(nameof(currentUserProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
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

    /// <summary>
    /// Loads an enrollment owned by the current tenant. Wrong-tenant ids are reported as missing
    /// rather than forbidden so callers do not confirm the id exists elsewhere.
    /// </summary>
    private async Task<EmployeeBenefitEnrollment?> FindOwnedEnrollmentAsync(
        Guid id,
        Func<IQueryable<EmployeeBenefitEnrollment>, IQueryable<EmployeeBenefitEnrollment>>? shape = null)
    {
        var tenantId = GetTenantId();
        IQueryable<EmployeeBenefitEnrollment> query = _enrollmentRepository
            .GetQueryable(e => e.Id == id && e.TenantId == tenantId);

        if (shape is not null)
        {
            query = shape(query);
        }

        return await query.FirstOrDefaultAsync();
    }

    private async Task<EmployeeBenefitEnrollment> GetOwnedEnrollmentAsync(
        Guid id,
        Func<IQueryable<EmployeeBenefitEnrollment>, IQueryable<EmployeeBenefitEnrollment>>? shape = null)
    {
        var entity = await FindOwnedEnrollmentAsync(id, shape);
        if (entity is null)
            throw new ArgumentException($"Enrollment '{id}' not found.");
        return entity;
    }

    private async Task<BenefitUtilization> GetOwnedUtilizationAsync(Guid claimId)
    {
        var tenantId = GetTenantId();
        var claim = await _utilizationRepository
            .GetQueryable(u => u.Id == claimId && u.TenantId == tenantId)
            .Include(u => u.EmployeeDependent)
            .FirstOrDefaultAsync();

        if (claim is null)
            throw new ArgumentException($"Claim '{claimId}' not found.");

        return claim;
    }

    /// <inheritdoc />
    public async Task<EmployeeBenefitEnrollmentDto?> GetByIdAsync(Guid id)
    {
        var entity = await QueryWithGraph()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity is null)
        {
            return null;
        }

        var dto = entity.ToDto();
        var balance = await ComputeBalanceReadOnlyAsync(entity);
        dto.CoverageLimit = balance.Limit;
        dto.UtilizedAmount = balance.Used;
        dto.RemainingAmount = balance.Remaining;
        dto.CurrentPeriodStart = balance.Start;
        dto.CurrentPeriodEnd = balance.End;
        return dto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeBenefitEnrollmentListDto>> GetByEmployeeAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var rows = await _enrollmentRepository
            .GetQueryable(e => e.TenantId == tenantId && e.EmployeeId == employeeId)
            .AsNoTracking()
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Employee).ThenInclude(emp => emp.Position)
            .Include(e => e.Utilizations)
            .OrderByDescending(e => e.EffectiveFrom)
            .ToListAsync();

        var list = new List<EmployeeBenefitEnrollmentListDto>(rows.Count);
        foreach (var row in rows)
        {
            var dto = ToListDto(row);
            var balance = await ComputeBalanceReadOnlyAsync(row);
            dto.CoverageLimit = balance.Limit;
            dto.UtilizedAmount = balance.Used;
            dto.RemainingAmount = balance.Remaining;
            dto.CurrentPeriodStart = balance.Start;
            dto.CurrentPeriodEnd = balance.End;
            list.Add(dto);
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeBenefitEnrollmentListDto>> GetByPolicyAsync(Guid benefitPolicyId)
    {
        var tenantId = GetTenantId();
        var rows = await _enrollmentRepository
            .GetQueryable(e => e.TenantId == tenantId && e.BenefitPolicyId == benefitPolicyId)
            .AsNoTracking()
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Employee)
            .OrderByDescending(e => e.EffectiveFrom)
            .ToListAsync();

        return rows.Select(ToListDto).ToList();
    }

    /// <inheritdoc />
    public async Task<EmployeeBenefitEnrollmentDto> CreateAsync(CreateEmployeeBenefitEnrollmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var tenantId = GetTenantId();

        var policy = await _benefitPolicyRepository
            .GetQueryable(p => p.Id == dto.BenefitPolicyId && p.TenantId == tenantId)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Benefit policy '{dto.BenefitPolicyId}' not found.");

        var employee = await _employeeRepository
            .GetQueryable(e => e.Id == dto.EmployeeId && e.TenantId == tenantId)
            .Include(e => e.Position)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Employee '{dto.EmployeeId}' not found.");

        await EnsureEligibleAsync(policy, employee);

        var asOf = DateOnly.FromDateTime(dto.EffectiveFrom);
        var valuation = await ResolveValuationAsync(policy, employee, asOf, dto.AssessedValueOverride);
        var (employerContribution, employeeContribution) = await ResolveContributionsAsync(policy, employee, asOf, valuation.AssessedValue);

        var entity = new EmployeeBenefitEnrollment
        {
            EmployeeId = dto.EmployeeId,
            BenefitPolicyId = dto.BenefitPolicyId,
            EnrollmentDate = DateTime.UtcNow,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            CurrentPeriodStart = dto.EffectiveFrom,
            Status = EmployeeBenefitEnrollmentStatus.Draft,
            Source = BenefitEnrollmentSource.Manual,
            AssessedValue = valuation.AssessedValue,
            TaxableValue = valuation.TaxableValue,
            EmployerContribution = employerContribution,
            EmployeeContribution = employeeContribution,
            Currency = policy.Currency,
            IsValueOverridden = dto.AssessedValueOverride.HasValue,
            Notes = dto.Notes,
            TenantId = tenantId
        };

        await _enrollmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await AddDependentsAsync(entity, policy, dto.Dependents);
        await AddBeneficiariesAsync(entity, dto.Beneficiaries);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created benefit enrollment {EnrollmentId} for employee {EmployeeId}.", entity.Id, entity.EmployeeId);

        return (await GetByIdAsync(entity.Id))!;
    }

    /// <inheritdoc />
    public async Task<EmployeeBenefitEnrollmentDto> UpdateAsync(Guid id, UpdateEmployeeBenefitEnrollmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await GetOwnedEnrollmentAsync(id, q => q
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Employee).ThenInclude(e => e.Position));

        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.Notes = dto.Notes;

        var asOf = DateOnly.FromDateTime(dto.EffectiveFrom);

        if (dto.AssessedValueOverride.HasValue)
        {
            // Explicit override: keep the supplied value and recompute only the taxable portion.
            entity.AssessedValue = dto.AssessedValueOverride.Value;
            entity.TaxableValue = ResolveTaxableForOverride(entity.BenefitPolicy, dto.AssessedValueOverride.Value);
            entity.IsValueOverridden = true;
        }
        else if (!entity.IsValueOverridden)
        {
            var valuation = await ResolveValuationAsync(entity.BenefitPolicy, entity.Employee, asOf, null);
            entity.AssessedValue = valuation.AssessedValue;
            entity.TaxableValue = valuation.TaxableValue;
            var (employer, employeeContribution) = await ResolveContributionsAsync(entity.BenefitPolicy, entity.Employee, asOf, valuation.AssessedValue);
            entity.EmployerContribution = employer;
            entity.EmployeeContribution = employeeContribution;
        }

        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    /// <inheritdoc />
    public async Task<EmployeeBenefitEnrollmentDto> ChangeStatusAsync(Guid id, EnrollmentStatusChangeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await GetOwnedEnrollmentAsync(id);

        entity.Status = dto.Status;

        if (dto.Status == EmployeeBenefitEnrollmentStatus.Active && entity.ApprovedDate is null)
        {
            entity.ApprovedDate = DateTime.UtcNow;
        }

        if (dto.Status is EmployeeBenefitEnrollmentStatus.Terminated or EmployeeBenefitEnrollmentStatus.Rejected)
        {
            entity.TerminationReason = dto.Reason;
        }

        await _enrollmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    /// <inheritdoc />
    public async Task<int> ReconcilePositionEnrollmentsAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();

        var employee = await _employeeRepository
            .GetQueryable(e => e.Id == employeeId && e.TenantId == tenantId)
            .Include(e => e.Position)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Employee '{employeeId}' not found.");

        var positionBenefits = await _positionBenefitRepository
            .GetQueryable(pb => pb.TenantId == tenantId && pb.PositionId == employee.PositionId)
            .Include(pb => pb.BenefitPolicy)
            .ToListAsync();

        var existing = await _enrollmentRepository
            .GetQueryable(e => e.TenantId == tenantId && e.EmployeeId == employeeId)
            .ToListAsync();

        var created = 0;

        foreach (var positionBenefit in positionBenefits)
        {
            var policy = positionBenefit.BenefitPolicy;
            if (policy is null || !policy.IsActive || policy.TenantId != tenantId)
            {
                continue;
            }

            if (!IsEligible(policy, employee))
            {
                continue;
            }

            var match = existing.FirstOrDefault(e => e.BenefitPolicyId == policy.Id);

            // Leave manual, overridden or opted-out (suspended/terminated) enrollments alone.
            if (match is not null)
            {
                if (match.Source != BenefitEnrollmentSource.Position || match.IsValueOverridden
                    || match.Status is EmployeeBenefitEnrollmentStatus.Suspended or EmployeeBenefitEnrollmentStatus.Terminated)
                {
                    continue;
                }

                var asOfRefresh = DateOnly.FromDateTime(DateTime.UtcNow);
                var refreshed = await ResolveValuationAsync(policy, employee, asOfRefresh, positionBenefit.PositionAmount);
                match.AssessedValue = refreshed.AssessedValue;
                match.TaxableValue = refreshed.TaxableValue;
                var (emp, empl) = await ResolveContributionsAsync(policy, employee, asOfRefresh, refreshed.AssessedValue);
                match.EmployerContribution = emp;
                match.EmployeeContribution = empl;
                await _enrollmentRepository.UpdateAsync(match);
                continue;
            }

            var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
            var valuation = await ResolveValuationAsync(policy, employee, asOf, positionBenefit.PositionAmount);
            var (employer, employeeContribution) = await ResolveContributionsAsync(policy, employee, asOf, valuation.AssessedValue);

            var enrollment = new EmployeeBenefitEnrollment
            {
                EmployeeId = employeeId,
                BenefitPolicyId = policy.Id,
                EnrollmentDate = DateTime.UtcNow,
                EffectiveFrom = DateTime.UtcNow,
                CurrentPeriodStart = DateTime.UtcNow,
                Status = policy.IsMandatory ? EmployeeBenefitEnrollmentStatus.Active : EmployeeBenefitEnrollmentStatus.PendingApproval,
                Source = policy.IsMandatory ? BenefitEnrollmentSource.Mandatory : BenefitEnrollmentSource.Position,
                SourcePositionBenefitId = positionBenefit.Id,
                AssessedValue = valuation.AssessedValue,
                TaxableValue = valuation.TaxableValue,
                EmployerContribution = employer,
                EmployeeContribution = employeeContribution,
                Currency = policy.Currency,
                TenantId = tenantId
            };

            await _enrollmentRepository.AddAsync(enrollment);
            created++;
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Reconciled position benefits for employee {EmployeeId}: {Created} new enrollment(s).", employeeId, created);

        return created;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeBenefitPayrollLineDto>> GetEmployeeBenefitPayrollLinesAsync(Guid employeeId, DateTime asOf)
    {
        var tenantId = GetTenantId();
        var enrollments = await _enrollmentRepository
            .GetQueryable(e => e.TenantId == tenantId
                && e.EmployeeId == employeeId
                && e.Status == EmployeeBenefitEnrollmentStatus.Active
                && e.EffectiveFrom <= asOf
                && (e.EffectiveTo == null || e.EffectiveTo >= asOf))
            .AsNoTracking()
            .Include(e => e.BenefitPolicy).ThenInclude(p => p.PayComponent)
            .ToListAsync();

        return enrollments.Select(e =>
        {
            var policy = e.BenefitPolicy;
            return new EmployeeBenefitPayrollLineDto
            {
                EnrollmentId = e.Id,
                EmployeeId = e.EmployeeId,
                BenefitPolicyId = e.BenefitPolicyId,
                BenefitName = policy?.PolicyName ?? string.Empty,
                PayComponentCode = policy?.PayComponent?.Code,
                DeliveryType = policy?.DeliveryType ?? BenefitDeliveryType.Cash,
                GrossValue = e.AssessedValue,
                TaxableValue = e.TaxableValue,
                EmployerContribution = e.EmployerContribution,
                EmployeeContribution = e.EmployeeContribution,
                IsPensionable = policy?.IsPensionable ?? false,
                AffectsGrossPay = policy?.AffectsGrossPay ?? true,
                AffectsNetPay = policy?.AffectsNetPay ?? true,
                Frequency = policy?.Frequency ?? PayFrequency.Monthly,
                Currency = e.Currency,
                EffectiveFrom = e.EffectiveFrom,
                EffectiveTo = e.EffectiveTo
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitUtilizationDto>> GetUtilizationsAsync(Guid enrollmentId)
    {
        var tenantId = GetTenantId();

        // Wrong-tenant enrollment ids yield an empty list (list convention; no existence leak).
        var enrollmentExists = await _enrollmentRepository
            .GetQueryable(e => e.Id == enrollmentId && e.TenantId == tenantId)
            .AnyAsync();

        if (!enrollmentExists)
        {
            return Array.Empty<BenefitUtilizationDto>();
        }

        var rows = await _utilizationRepository
            .GetQueryable(u => u.TenantId == tenantId && u.EnrollmentId == enrollmentId)
            .AsNoTracking()
            .Include(u => u.EmployeeDependent)
            .OrderByDescending(u => u.ClaimDate)
            .ThenByDescending(u => u.CreatedAt)
            .ToListAsync();

        return rows.Select(ToUtilizationDto).ToList();
    }

    /// <inheritdoc />
    public async Task<EnrollmentBalanceDto> GetBalanceAsync(Guid enrollmentId)
    {
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId, q => q
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Employee).ThenInclude(emp => emp.Position)
            .Include(e => e.Dependents)
            .Include(e => e.Utilizations));

        var (limit, used, remaining, start, end) = await ApplyPeriodAndRecomputeAsync(enrollment, persist: true);
        await _unitOfWork.SaveChangesAsync();

        return new EnrollmentBalanceDto
        {
            EnrollmentId = enrollment.Id,
            CoverageLimit = limit,
            UtilizedAmount = used,
            RemainingAmount = remaining,
            LimitPeriod = enrollment.BenefitPolicy?.LimitPeriod ?? BenefitLimitPeriod.Annual,
            PeriodStart = start,
            PeriodEnd = end,
            Currency = enrollment.Currency
        };
    }

    /// <inheritdoc />
    public async Task<BenefitUtilizationDto> RecordUtilizationAsync(CreateBenefitUtilizationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var enrollment = await GetOwnedEnrollmentAsync(dto.EnrollmentId, q => q
            .Include(e => e.Dependents));

        if (enrollment.Status != EmployeeBenefitEnrollmentStatus.Active)
        {
            throw new InvalidOperationException("Claims can only be recorded against an active enrollment.");
        }

        // Cover must be live, not merely on record. Checking existence alone would let claims keep
        // flowing for a dependent whose cover had been ended, which is precisely what ending it means
        // to prevent — and ending cover is how a dependent with claim history is removed.
        if (dto.EmployeeDependentId.HasValue
            && !enrollment.Dependents.Any(d => d.EmployeeDependentId == dto.EmployeeDependentId.Value
                && d.TenantId == enrollment.TenantId
                && d.IsActive))
        {
            throw new ArgumentException("The selected dependent is not currently covered under this enrollment.");
        }

        var entity = new BenefitUtilization
        {
            EnrollmentId = enrollment.Id,
            EmployeeDependentId = dto.EmployeeDependentId,
            ClaimDate = dto.ClaimDate,
            Amount = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero),
            Type = dto.Type,
            Status = BenefitClaimStatus.Pending,
            Description = dto.Description,
            ReferenceNumber = dto.ReferenceNumber,
            TenantId = enrollment.TenantId
        };

        await _utilizationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return ToUtilizationDto(entity);
    }

    /// <inheritdoc />
    public async Task<BenefitUtilizationDto> ChangeClaimStatusAsync(Guid claimId, ClaimStatusChangeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var claim = await GetOwnedUtilizationAsync(claimId);

        var enrollment = await GetOwnedEnrollmentAsync(claim.EnrollmentId, q => q
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Employee).ThenInclude(emp => emp.Position)
            .Include(e => e.Dependents)
            .Include(e => e.Utilizations));

        // Apply the transition on the tracked claim instance from the enrollment graph.
        var tracked = enrollment.Utilizations.First(u => u.Id == claim.Id);
        tracked.Status = dto.Status;

        if (dto.Status is BenefitClaimStatus.Approved or BenefitClaimStatus.Paid)
        {
            tracked.ApprovedDate ??= DateTime.UtcNow;

            // Block if approving/paying this claim would push the period total past the coverage limit.
            var (limit, used, _, _, _) = await ApplyPeriodAndRecomputeAsync(enrollment, persist: false);
            if (used > limit)
            {
                throw new InvalidOperationException(
                    $"Claim exceeds the remaining benefit balance (limit {limit:N2}, would be used {used:N2}).");
            }
        }
        else if (dto.Status is BenefitClaimStatus.Rejected or BenefitClaimStatus.Cancelled)
        {
            tracked.RejectionReason = dto.Reason;
        }

        // Refresh denormalized caches to reflect the new claim state, then persist.
        await ApplyPeriodAndRecomputeAsync(enrollment, persist: true);
        await _unitOfWork.SaveChangesAsync();

        return ToUtilizationDto(tracked);
    }

    // ─────────────────────── covered dependents ───────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnrollmentDependentDto>> GetDependentsAsync(Guid enrollmentId)
    {
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId, q => q
            .Include(e => e.Dependents).ThenInclude(d => d.EmployeeDependent));

        return MapDependents(enrollment);
    }

    /// <inheritdoc />
    public async Task<EnrollmentDependentDto> AddDependentAsync(Guid enrollmentId, CreateEnrollmentDependentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId, q => q
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Dependents));

        EnsureCoverageEditable(enrollment);

        var policy = enrollment.BenefitPolicy
            ?? throw new InvalidOperationException("The enrollment's benefit policy could not be loaded.");

        await EnsureCoverableDependentAsync(
            policy,
            enrollment.EmployeeId,
            enrollment.TenantId,
            dto.EmployeeDependentId,
            enrollment.Dependents.Where(d => d.TenantId == enrollment.TenantId && !d.IsDeleted).ToList());

        var entity = new EmployeeDependentBenefit
        {
            EmployeeDependentId = dto.EmployeeDependentId,
            PolicyId = policy.Id,
            EnrollmentId = enrollment.Id,
            EnrolledDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CoverageStartDate = dto.CoverageStartDate,
            CoverageEndDate = dto.CoverageEndDate,
            IsActive = true,
            TenantId = enrollment.TenantId
        };

        await _dependentBenefitRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Added dependent {EmployeeDependentId} to benefit enrollment {EnrollmentId}.",
            dto.EmployeeDependentId, enrollmentId);

        return await ReadDependentAsync(entity.Id, enrollment.TenantId);
    }

    /// <inheritdoc />
    public async Task<EnrollmentDependentDto> UpdateDependentAsync(Guid enrollmentId, Guid dependentBenefitId, UpdateEnrollmentDependentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId, q => q
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Dependents));

        EnsureCoverageEditable(enrollment);

        var row = FindCoveredDependent(enrollment, dependentBenefitId);

        // Re-activating consumes a slot, so it has to re-clear the policy's cap — otherwise a cap of
        // 2 could be exceeded by deactivating, adding, then re-activating.
        if (dto.IsActive && !row.IsActive)
        {
            var policy = enrollment.BenefitPolicy
                ?? throw new InvalidOperationException("The enrollment's benefit policy could not be loaded.");

            await EnsureCoverableDependentAsync(
                policy,
                enrollment.EmployeeId,
                enrollment.TenantId,
                row.EmployeeDependentId,
                enrollment.Dependents.Where(d => d.TenantId == enrollment.TenantId && !d.IsDeleted && d.Id != row.Id).ToList());
        }

        row.CoverageStartDate = dto.CoverageStartDate;
        row.CoverageEndDate = dto.CoverageEndDate;
        row.IsActive = dto.IsActive;

        await _dependentBenefitRepository.UpdateAsync(row);
        await _unitOfWork.SaveChangesAsync();

        return await ReadDependentAsync(row.Id, enrollment.TenantId);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveDependentAsync(Guid enrollmentId, Guid dependentBenefitId)
    {
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId, q => q
            .Include(e => e.Dependents));

        EnsureCoverageEditable(enrollment);

        var row = FindCoveredDependent(enrollment, dependentBenefitId);

        var hasClaims = await _utilizationRepository
            .GetQueryable(u => u.TenantId == enrollment.TenantId
                && u.EnrollmentId == enrollment.Id
                && u.EmployeeDependentId == row.EmployeeDependentId)
            .AnyAsync();

        if (hasClaims)
        {
            // Deleting would strand claims that were made in this dependent's name. Ending cover has
            // the same forward effect — no further claims can be recorded — without losing the trail.
            row.IsActive = false;
            row.CoverageEndDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
            await _dependentBenefitRepository.UpdateAsync(row);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Dependent {EmployeeDependentId} on enrollment {EnrollmentId} has claims; ended cover instead of deleting.",
                row.EmployeeDependentId, enrollmentId);

            return false;
        }

        await _dependentBenefitRepository.DeleteAsync(row);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    // ───────────────────────── beneficiaries ──────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitBeneficiaryDto>> GetBeneficiariesAsync(Guid enrollmentId)
    {
        // Proves ownership first, so a wrong-tenant id reads as missing rather than empty.
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId);
        return await ReadBeneficiariesAsync(enrollment.Id, enrollment.TenantId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitBeneficiaryDto>> ReplaceBeneficiariesAsync(Guid enrollmentId, ReplaceBenefitBeneficiariesDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Deliberately loaded WITHOUT the beneficiary navigation: the rows are queried and written
        // through their own repository below, and tracking them twice — once via the graph, once
        // directly — is what turns a straightforward replace into an identity-map conflict.
        var enrollment = await GetOwnedEnrollmentAsync(enrollmentId);

        EnsureCoverageEditable(enrollment);

        var existing = await _beneficiaryRepository
            .GetQueryable(b => b.TenantId == enrollment.TenantId && b.EnrollmentId == enrollment.Id)
            .ToListAsync();

        foreach (var row in existing)
        {
            await _beneficiaryRepository.DeleteAsync(row);
        }

        await AddBeneficiariesAsync(enrollment, dto.Beneficiaries);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Replaced the beneficiary set on enrollment {EnrollmentId} with {Count} nomination(s).",
            enrollmentId, dto.Beneficiaries?.Count ?? 0);

        return await ReadBeneficiariesAsync(enrollment.Id, enrollment.TenantId);
    }

    // ─────────────────────────── helpers ───────────────────────────

    private static EmployeeDependentBenefit FindCoveredDependent(EmployeeBenefitEnrollment enrollment, Guid dependentBenefitId)
        => enrollment.Dependents.FirstOrDefault(d => d.Id == dependentBenefitId && d.TenantId == enrollment.TenantId && !d.IsDeleted)
            ?? throw new ArgumentException($"Dependent cover '{dependentBenefitId}' was not found on this enrollment.");

    /// <summary>
    /// Re-reads a coverage row through the graph so the response carries the dependent's name. The
    /// navigation is not populated on a row we just built or on one whose FK was only now assigned.
    /// </summary>
    private async Task<EnrollmentDependentDto> ReadDependentAsync(Guid dependentBenefitId, Guid tenantId)
    {
        var row = await _dependentBenefitRepository
            .GetQueryable(d => d.Id == dependentBenefitId && d.TenantId == tenantId)
            .AsNoTracking()
            .Include(d => d.EmployeeDependent)
            .FirstAsync();

        return ToDependentDto(row);
    }

    private static EnrollmentDependentDto ToDependentDto(EmployeeDependentBenefit d) => new()
    {
        Id = d.Id,
        EmployeeDependentId = d.EmployeeDependentId,
        DependentName = DependentDisplayName(d.EmployeeDependent),
        Relationship = d.EmployeeDependent?.Relationship ?? default,
        PolicyId = d.PolicyId,
        EnrolledDate = d.EnrolledDate,
        CoverageStartDate = d.CoverageStartDate,
        CoverageEndDate = d.CoverageEndDate,
        IsActive = d.IsActive,
        BenefitAmountUsed = d.BenefitAmountUsed
    };

    // The !IsDeleted guards below are deliberate. The global filter already excludes soft-deleted
    // rows from queries, but these collections are read off a tracked graph, where EF's navigation
    // fixup can re-attach an entity we deleted earlier in the same unit of work.
    private static IReadOnlyList<EnrollmentDependentDto> MapDependents(EmployeeBenefitEnrollment enrollment)
        => enrollment.Dependents
            .Where(d => d.TenantId == enrollment.TenantId && !d.IsDeleted)
            .OrderByDescending(d => d.IsActive)
            .ThenBy(d => DependentDisplayName(d.EmployeeDependent))
            .Select(ToDependentDto)
            .ToList();

    /// <summary>
    /// Reads the nomination set from its own table rather than off the enrollment graph, so a
    /// replace that just soft-deleted and re-inserted rows reports what is actually stored.
    /// </summary>
    private async Task<IReadOnlyList<BenefitBeneficiaryDto>> ReadBeneficiariesAsync(Guid enrollmentId, Guid tenantId)
    {
        var rows = await _beneficiaryRepository
            .GetQueryable(b => b.TenantId == tenantId && b.EnrollmentId == enrollmentId)
            .AsNoTracking()
            .OrderByDescending(b => b.Percentage)
            .ThenBy(b => b.FullName)
            .ToListAsync();

        return rows.Select(b => new BenefitBeneficiaryDto
        {
            Id = b.Id,
            FullName = b.FullName,
            Relationship = b.Relationship,
            EmployeeDependentId = b.EmployeeDependentId,
            PhoneNumber = b.PhoneNumber,
            Percentage = b.Percentage,
            IsActive = b.IsActive
        }).ToList();
    }

    private IQueryable<EmployeeBenefitEnrollment> QueryWithGraph()
        => _enrollmentRepository.GetQueryable(e => e.TenantId == GetTenantId())
            .Include(e => e.BenefitPolicy)
            .Include(e => e.Employee).ThenInclude(emp => emp.Position)
            // The dependent's own record supplies the name and relationship the coverage list shows.
            .Include(e => e.Dependents).ThenInclude(d => d.EmployeeDependent)
            .Include(e => e.Beneficiaries)
            .Include(e => e.Utilizations);

    private async Task<BenefitValuation> ResolveValuationAsync(BenefitPolicy policy, Employee employee, DateOnly asOf, decimal? overrideValue)
    {
        if (overrideValue.HasValue)
        {
            return new BenefitValuation(overrideValue.Value, ResolveTaxableForOverride(policy, overrideValue.Value));
        }

        var (basic, cashEmoluments) = await GetPayBasesAsync(employee.Id, asOf);
        BenefitGradeValue? gradeValue = null;

        if (policy.ValuationMethod == BenefitValuationMethod.GradeBased)
        {
            gradeValue = await ResolveGradeValueAsync(policy.Id, employee);
        }

        return GhanaBikValuator.Value(policy, basic, cashEmoluments, gradeValue);
    }

    private async Task<BenefitGradeValue?> ResolveGradeValueAsync(Guid policyId, Employee employee)
    {
        var tenantId = GetTenantId();
        var rows = await _gradeValueRepository
            .GetQueryable(g => g.TenantId == tenantId && g.BenefitPolicyId == policyId && g.IsActive)
            .ToListAsync();

        var salaryGradeId = employee.Position?.SalaryGradeId;
        var staffLevelId = employee.Position?.StaffLevelId;

        return rows.FirstOrDefault(g => salaryGradeId.HasValue && g.SalaryGradeId == salaryGradeId)
            ?? rows.FirstOrDefault(g => staffLevelId.HasValue && g.StaffLevelId == staffLevelId);
    }

    /// <summary>
    /// Resolves the coverage ceiling for an enrollment: the grade/level row's CoverageLimit when the
    /// policy is grade-based and one applies, otherwise the policy's flat CoverageLimit.
    /// </summary>
    private async Task<decimal> ResolveCoverageLimitAsync(BenefitPolicy policy, Employee? employee)
    {
        if (employee is not null
            && (policy.ValuationMethod == BenefitValuationMethod.GradeBased
                || policy.CalculationBasis == BenefitCalculationBasis.GradeBandTable))
        {
            var gradeValue = await ResolveGradeValueAsync(policy.Id, employee);
            if (gradeValue?.CoverageLimit is { } gradeLimit && gradeLimit > 0m)
            {
                return gradeLimit;
            }
        }

        return policy.CoverageLimit;
    }

    /// <summary>Read-only balance computation for list/detail views (does not persist the reset).</summary>
    private async Task<(decimal Limit, decimal Used, decimal Remaining, DateTime? Start, DateTime? End)> ComputeBalanceReadOnlyAsync(EmployeeBenefitEnrollment e)
    {
        var policy = e.BenefitPolicy;
        if (policy is null)
        {
            return (0m, e.UtilizedAmount, 0m, e.CurrentPeriodStart, null);
        }

        var tenantId = GetTenantId();
        var anchor = e.CurrentPeriodStart ?? e.EffectiveFrom;
        var (start, end) = ResolvePeriodWindow(policy.LimitPeriod, anchor, DateTime.UtcNow);
        var used = SumUtilized(e.Utilizations, tenantId, ToDateOnly(start), ToDateOnly(end));
        var limit = await ResolveCoverageLimitAsync(policy, e.Employee);
        return (limit, used, Math.Max(0m, limit - used), start, end);
    }

    /// <summary>
    /// Applies the lazy periodic reset and recomputes the denormalized used-amount caches on the
    /// (tracked) enrollment and its dependents. Returns the resulting balance figures.
    /// </summary>
    private async Task<(decimal Limit, decimal Used, decimal Remaining, DateTime? Start, DateTime? End)> ApplyPeriodAndRecomputeAsync(EmployeeBenefitEnrollment e, bool persist)
    {
        var policy = e.BenefitPolicy
            ?? throw new InvalidOperationException("Enrollment policy must be loaded to compute balance.");

        var tenantId = GetTenantId();
        var anchor = e.CurrentPeriodStart ?? e.EffectiveFrom;
        var (start, end) = ResolvePeriodWindow(policy.LimitPeriod, anchor, DateTime.UtcNow);
        var startDate = ToDateOnly(start);
        var endDate = ToDateOnly(end);

        var used = SumUtilized(e.Utilizations, tenantId, startDate, endDate);
        var limit = await ResolveCoverageLimitAsync(policy, e.Employee);

        if (persist)
        {
            e.CurrentPeriodStart = start ?? e.CurrentPeriodStart;
            e.UtilizedAmount = used;

            foreach (var dependent in e.Dependents.Where(d => d.TenantId == tenantId))
            {
                dependent.BenefitAmountUsed = SumUtilized(e.Utilizations, tenantId, startDate, endDate, dependent.EmployeeDependentId);
            }
        }

        return (limit, used, Math.Max(0m, limit - used), start, end);
    }

    /// <summary>Sums Approved/Paid claims within the period window (Reversal returns amount), same-tenant only.</summary>
    private static decimal SumUtilized(IEnumerable<BenefitUtilization>? claims, Guid tenantId, DateOnly? start, DateOnly? end, Guid? dependentId = null)
    {
        if (claims is null)
        {
            return 0m;
        }

        var sum = claims
            .Where(u => u.TenantId == tenantId)
            .Where(u => u.Status is BenefitClaimStatus.Approved or BenefitClaimStatus.Paid)
            .Where(u => !dependentId.HasValue || u.EmployeeDependentId == dependentId)
            .Where(u => (!start.HasValue || u.ClaimDate >= start.Value) && (!end.HasValue || u.ClaimDate < end.Value))
            .Sum(u => u.Type == BenefitUtilizationType.Reversal ? -u.Amount : u.Amount);

        return Math.Max(0m, sum);
    }

    /// <summary>
    /// Computes the current usage window from the policy's LimitPeriod, anchored at <paramref name="anchor"/>
    /// and rolled forward to contain <paramref name="asOf"/>. Lifetime returns an open window (no reset).
    /// </summary>
    private static (DateTime? Start, DateTime? End) ResolvePeriodWindow(BenefitLimitPeriod period, DateTime anchor, DateTime asOf)
        => period switch
        {
            BenefitLimitPeriod.Lifetime => (null, null),
            BenefitLimitPeriod.Monthly => RollWindow(anchor, asOf, 1),
            _ => RollWindow(anchor, asOf, 12)
        };

    private static (DateTime? Start, DateTime? End) RollWindow(DateTime anchor, DateTime asOf, int months)
    {
        var start = anchor;
        var end = start.AddMonths(months);
        while (asOf >= end)
        {
            start = end;
            end = start.AddMonths(months);
        }

        return (start, end);
    }

    private static DateOnly? ToDateOnly(DateTime? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private static string? DependentName(EmployeeDependent? d)
        => d is null ? null : EmployeeBenefitEnrollmentMappingExtensions.DependentDisplayName(d);

    private static string DependentDisplayName(EmployeeDependent? d)
        => EmployeeBenefitEnrollmentMappingExtensions.DependentDisplayName(d);

    private static BenefitUtilizationDto ToUtilizationDto(BenefitUtilization u) => new()
    {
        Id = u.Id,
        EnrollmentId = u.EnrollmentId,
        EmployeeDependentId = u.EmployeeDependentId,
        DependentName = DependentName(u.EmployeeDependent),
        ClaimDate = u.ClaimDate,
        Amount = u.Amount,
        Type = u.Type,
        Status = u.Status,
        Description = u.Description,
        ReferenceNumber = u.ReferenceNumber,
        ApprovedDate = u.ApprovedDate,
        RejectionReason = u.RejectionReason
    };

    private async Task<(decimal employer, decimal employee)> ResolveContributionsAsync(BenefitPolicy policy, Employee employee, DateOnly asOf, decimal assessedValue)
    {
        // Percentage-based bases need basic / gross pay.
        decimal basis = 0m;
        if (policy.CalculationBasis is BenefitCalculationBasis.PercentageOfBasic or BenefitCalculationBasis.PercentageOfGross)
        {
            var (basic, gross) = await GetPayBasesAsync(employee.Id, asOf);
            basis = policy.CalculationBasis == BenefitCalculationBasis.PercentageOfBasic ? basic : gross;
        }

        decimal employer = policy.CalculationBasis switch
        {
            BenefitCalculationBasis.PercentageOfBasic or BenefitCalculationBasis.PercentageOfGross
                => Math.Round((policy.EmployerContributionRate ?? 0m) / 100m * basis, 2, MidpointRounding.AwayFromZero),
            _ => policy.EmployerContribution ?? 0m
        };

        decimal employeeShare = policy.CalculationBasis switch
        {
            BenefitCalculationBasis.PercentageOfBasic or BenefitCalculationBasis.PercentageOfGross
                => Math.Round((policy.EmployeeContributionRate ?? 0m) / 100m * basis, 2, MidpointRounding.AwayFromZero),
            _ => policy.EmployeeContribution ?? 0m
        };

        // Honour the responsibility setting for unambiguous all-employer / all-employee cases.
        switch (policy.ContributionResponsibility)
        {
            case BenefitContributionResponsibility.EmployerPaysAll:
                if (employer == 0m) employer = assessedValue;
                employeeShare = 0m;
                break;
            case BenefitContributionResponsibility.EmployeePaysAll:
                if (employeeShare == 0m) employeeShare = assessedValue;
                employer = 0m;
                break;
        }

        return (employer, employeeShare);
    }

    private async Task<(decimal basic, decimal cashEmoluments)> GetPayBasesAsync(Guid employeeId, DateOnly asOf)
    {
        try
        {
            var basic = await _emolumentService.GetMonthlyBasicPayAsync(employeeId, asOf);
            var summary = await _emolumentService.GetEmployeeEmolumentSummaryAsync(employeeId, asOf);
            return (basic, summary.GrossMonthly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve pay bases for employee {EmployeeId}; defaulting to zero.", employeeId);
            return (0m, 0m);
        }
    }

    private static decimal ResolveTaxableForOverride(BenefitPolicy policy, decimal assessed)
    {
        if (!policy.IsTaxable || policy.TaxTreatment == BenefitTaxTreatment.TaxExempt)
        {
            return 0m;
        }

        var taxableBase = policy.TaxExemptThreshold.HasValue
            ? Math.Max(0m, assessed - policy.TaxExemptThreshold.Value)
            : assessed;

        return policy.TaxTreatment == BenefitTaxTreatment.PartiallyTaxable
            ? Math.Round((policy.TaxablePercentage ?? 0m) / 100m * taxableBase, 2, MidpointRounding.AwayFromZero)
            : taxableBase;
    }

    private async Task EnsureEligibleAsync(BenefitPolicy policy, Employee employee)
    {
        await Task.CompletedTask;
        if (!IsEligible(policy, employee))
        {
            throw new InvalidOperationException("Employee is not eligible for this benefit (service length or probation rule).");
        }
    }

    private static bool IsEligible(BenefitPolicy policy, Employee employee)
    {
        if (!policy.AvailableDuringProbation && employee.IsOnProbation)
        {
            return false;
        }

        if (policy.MinServiceMonths is > 0 && employee.DateEmployed.HasValue)
        {
            var months = MonthsBetween(employee.DateEmployed.Value, DateOnly.FromDateTime(DateTime.UtcNow));
            if (months < policy.MinServiceMonths.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static int MonthsBetween(DateOnly from, DateOnly to)
        => Math.Max(0, ((to.Year - from.Year) * 12) + to.Month - from.Month - (to.Day < from.Day ? 1 : 0));

    private async Task AddDependentsAsync(EmployeeBenefitEnrollment enrollment, BenefitPolicy policy, IEnumerable<CreateEnrollmentDependentDto> dependents)
    {
        // Accumulates as we go, so the cap and the duplicate check see the rows added earlier in this
        // same call — not just whatever was already persisted.
        var covered = new List<EmployeeDependentBenefit>();

        foreach (var dep in dependents ?? Enumerable.Empty<CreateEnrollmentDependentDto>())
        {
            if (dep is null) continue;

            await EnsureCoverableDependentAsync(policy, enrollment.EmployeeId, enrollment.TenantId, dep.EmployeeDependentId, covered);

            var entity = new EmployeeDependentBenefit
            {
                EmployeeDependentId = dep.EmployeeDependentId,
                PolicyId = policy.Id,
                EnrollmentId = enrollment.Id,
                EnrolledDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CoverageStartDate = dep.CoverageStartDate,
                CoverageEndDate = dep.CoverageEndDate,
                IsActive = true,
                TenantId = enrollment.TenantId
            };

            await _dependentBenefitRepository.AddAsync(entity);
            covered.Add(entity);
        }
    }

    private async Task AddBeneficiariesAsync(EmployeeBenefitEnrollment enrollment, IEnumerable<CreateBenefitBeneficiaryDto> beneficiaries)
    {
        foreach (var b in beneficiaries ?? Enumerable.Empty<CreateBenefitBeneficiaryDto>())
        {
            if (b is null) continue;

            // A beneficiary may optionally point at a registered dependent; when it does, that
            // dependent must be the employee's own, for the same reason cover is.
            if (b.EmployeeDependentId.HasValue)
            {
                await EnsureOwnDependentAsync(enrollment.EmployeeId, enrollment.TenantId, b.EmployeeDependentId.Value);
            }

            // Added through the repository, not by appending to enrollment.Beneficiaries. BaseEntity
            // assigns an Id in its initializer, so a new child discovered through a navigation off an
            // ALREADY-TRACKED enrollment is resolved as Modified — EF issues an UPDATE for a row that
            // was never inserted and SaveChanges throws DbUpdateConcurrencyException. Going through
            // AddAsync states the intent outright, and matches how dependents are added.
            await _beneficiaryRepository.AddAsync(new BenefitBeneficiary
            {
                EnrollmentId = enrollment.Id,
                FullName = b.FullName,
                Relationship = b.Relationship,
                EmployeeDependentId = b.EmployeeDependentId,
                PhoneNumber = b.PhoneNumber,
                Percentage = b.Percentage,
                IsActive = true,
                TenantId = enrollment.TenantId
            });
        }
    }

    /// <summary>
    /// Loads a dependent and proves it is the employee's own. Without this any dependent id in the
    /// tenant would be accepted, quietly extending one employee's benefit to another's family.
    /// </summary>
    private async Task<EmployeeDependent> EnsureOwnDependentAsync(Guid employeeId, Guid tenantId, Guid employeeDependentId)
    {
        var dependent = await _employeeDependentRepository
            .GetQueryable(d => d.Id == employeeDependentId && d.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Dependent '{employeeDependentId}' not found.");

        if (dependent.EmployeeId != employeeId)
        {
            throw new ArgumentException("The selected dependent is not registered to this employee.");
        }

        return dependent;
    }

    /// <summary>
    /// The full gate a dependent must pass before cover is extended to them: the policy must cover
    /// dependents at all, the dependent must be the employee's own and living, they must not already
    /// be covered, and the policy's dependent cap must have room.
    /// </summary>
    private async Task<EmployeeDependent> EnsureCoverableDependentAsync(
        BenefitPolicy policy,
        Guid employeeId,
        Guid tenantId,
        Guid employeeDependentId,
        IReadOnlyCollection<EmployeeDependentBenefit> alreadyCovered)
    {
        if (policy.Recipient == BenefitRecipient.Staff)
        {
            throw new InvalidOperationException(
                $"Policy '{policy.PolicyName}' covers staff only; dependents cannot be added to it.");
        }

        var dependent = await EnsureOwnDependentAsync(employeeId, tenantId, employeeDependentId);

        if (dependent.IsDeceased)
        {
            throw new InvalidOperationException(
                $"{DependentDisplayName(dependent)} is recorded as deceased and cannot be covered.");
        }

        if (alreadyCovered.Any(d => d.EmployeeDependentId == employeeDependentId && d.IsActive))
        {
            throw new InvalidOperationException(
                $"{DependentDisplayName(dependent)} is already covered under this enrollment.");
        }

        // MaxDependents is declared on the policy but was previously never enforced, so a policy
        // capped at 2 would happily accept 10.
        if (policy.MaxDependents is > 0)
        {
            var activeCount = alreadyCovered.Count(d => d.IsActive);
            if (activeCount >= policy.MaxDependents.Value)
            {
                throw new InvalidOperationException(
                    $"Policy '{policy.PolicyName}' covers at most {policy.MaxDependents.Value} dependent(s); {activeCount} are already covered.");
            }
        }

        return dependent;
    }

    /// <summary>
    /// Cover and nominations describe an enrollment that is still going somewhere. Once it has been
    /// terminated, rejected or has expired, editing them would rewrite settled history.
    /// </summary>
    private static void EnsureCoverageEditable(EmployeeBenefitEnrollment enrollment)
    {
        if (enrollment.Status is EmployeeBenefitEnrollmentStatus.Terminated
            or EmployeeBenefitEnrollmentStatus.Rejected
            or EmployeeBenefitEnrollmentStatus.Expired)
        {
            throw new InvalidOperationException(
                $"Cover cannot be changed on a {enrollment.Status.ToString().ToLowerInvariant()} enrollment.");
        }
    }

    private static EmployeeBenefitEnrollmentListDto ToListDto(EmployeeBenefitEnrollment e) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        BenefitPolicyId = e.BenefitPolicyId,
        BenefitPolicyName = e.BenefitPolicy?.PolicyName ?? string.Empty,
        Status = e.Status,
        Source = e.Source,
        AssessedValue = e.AssessedValue,
        TaxableValue = e.TaxableValue,
        Currency = e.Currency,
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo
    };
}

/// <summary>Inline entity→DTO mapping for benefit enrollments.</summary>
internal static class EmployeeBenefitEnrollmentMappingExtensions
{
    public static EmployeeBenefitEnrollmentDto ToDto(this EmployeeBenefitEnrollment e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        BenefitPolicyId = e.BenefitPolicyId,
        BenefitPolicyName = e.BenefitPolicy?.PolicyName ?? string.Empty,
        EnrollmentDate = e.EnrollmentDate,
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo,
        Status = e.Status,
        Source = e.Source,
        SourcePositionBenefitId = e.SourcePositionBenefitId,
        AssessedValue = e.AssessedValue,
        TaxableValue = e.TaxableValue,
        EmployerContribution = e.EmployerContribution,
        EmployeeContribution = e.EmployeeContribution,
        Currency = e.Currency,
        UtilizedAmount = e.UtilizedAmount,
        IsValueOverridden = e.IsValueOverridden,
        ApprovedById = e.ApprovedById,
        ApprovedDate = e.ApprovedDate,
        TerminationReason = e.TerminationReason,
        Notes = e.Notes,
        Dependents = (e.Dependents ?? new List<EmployeeDependentBenefit>())
            .Where(d => d.TenantId == e.TenantId && !d.IsDeleted)
            .Select(d => new EnrollmentDependentDto
            {
                Id = d.Id,
                EmployeeDependentId = d.EmployeeDependentId,
                DependentName = DependentDisplayName(d.EmployeeDependent),
                Relationship = d.EmployeeDependent?.Relationship ?? default,
                PolicyId = d.PolicyId,
                EnrolledDate = d.EnrolledDate,
                CoverageStartDate = d.CoverageStartDate,
                CoverageEndDate = d.CoverageEndDate,
                IsActive = d.IsActive,
                BenefitAmountUsed = d.BenefitAmountUsed
            }).ToList(),
        Beneficiaries = (e.Beneficiaries ?? new List<BenefitBeneficiary>())
            .Where(b => b.TenantId == e.TenantId && !b.IsDeleted)
            .Select(b => new BenefitBeneficiaryDto
            {
                Id = b.Id,
                FullName = b.FullName,
                Relationship = b.Relationship,
                EmployeeDependentId = b.EmployeeDependentId,
                PhoneNumber = b.PhoneNumber,
                Percentage = b.Percentage,
                IsActive = b.IsActive
            }).ToList()
    };

    /// <summary>
    /// Full name of a dependent, tolerating a missing middle name and an unloaded navigation.
    /// Shared with the enrollment service so a dependent reads the same everywhere they appear.
    /// </summary>
    internal static string DependentDisplayName(EmployeeDependent? d)
    {
        if (d is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(d.MiddleName)
            ? $"{d.FirstName} {d.LastName}".Trim()
            : $"{d.FirstName} {d.MiddleName} {d.LastName}".Trim();
    }
}
