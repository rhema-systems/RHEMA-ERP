using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;  // PayrollEmployeeProfile — read-only, for the negotiated pay basis (lane E1b)
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class EmolumentService : IEmolumentService
{
    private const int DefaultWorkingDaysPerMonth = 22;

    private readonly IGenericRepository<PayComponent> _componentRepo;
    private readonly IGenericRepository<PositionPayComponent> _positionCompRepo;
    private readonly IGenericRepository<EmployeePayComponent> _employeeCompRepo;
    private readonly IGenericRepository<Employee> _employeeRepo;
    private readonly IGenericRepository<EmployeeSalaryAssignment> _salaryAssignmentRepo;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepo;
    private readonly IPayComponentProjectionService _componentProjection;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EmolumentService> _logger;
    private readonly ICompanyHrPolicyProvider _policyProvider;

    public EmolumentService(
        IGenericRepository<PayComponent> componentRepo,
        IGenericRepository<PositionPayComponent> positionCompRepo,
        IGenericRepository<EmployeePayComponent> employeeCompRepo,
        IGenericRepository<Employee> employeeRepo,
        IGenericRepository<EmployeeSalaryAssignment> salaryAssignmentRepo,
        IGenericRepository<LeaveType> leaveTypeRepo,
        IPayComponentProjectionService componentProjection,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ILogger<EmolumentService> logger,
        ICompanyHrPolicyProvider policyProvider)
    {
        _componentRepo = componentRepo;
        _positionCompRepo = positionCompRepo;
        _employeeCompRepo = employeeCompRepo;
        _employeeRepo = employeeRepo;
        _salaryAssignmentRepo = salaryAssignmentRepo;
        _leaveTypeRepo = leaveTypeRepo;
        _componentProjection = componentProjection;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
        _policyProvider = policyProvider;
    }

    /// <summary>
    /// Provenance marker the projection stamps into <see cref="PayComponent.Description"/>.
    /// Duplicated here rather than shared so the projection stays the only thing that writes it.
    /// </summary>
    private const string PayrollProvenanceMarker = "Defined in Payroll";

    private static bool IsPayrollDefined(PayComponent component) =>
        component.Description is not null &&
        component.Description.Contains(PayrollProvenanceMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Brings the mirror up to date before a read. Payroll is another team's module and never
    /// pushes, so HR pulls on read; a projection failure must not take the read down with it.
    /// </summary>
    private async Task EnsureComponentsCurrentAsync()
    {
        try
        {
            await _componentProjection.EnsureCurrentAsync(GetTenantId());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pay component projection failed; serving the mirror as it stands.");
        }
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

    // Aggregates owned by another tenant are reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<PayComponent> GetOwnedPayComponentAsync(Guid id)
    {
        var entity = await _componentRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pay component '{id}' not found.");
        return entity;
    }

    private async Task<PositionPayComponent> GetOwnedPositionPayComponentAsync(Guid id)
    {
        var entity = await _positionCompRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Position pay component '{id}' not found.");
        return entity;
    }

    private async Task<EmployeePayComponent> GetOwnedEmployeePayComponentAsync(Guid id)
    {
        var entity = await _employeeCompRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employee pay component '{id}' not found.");
        return entity;
    }

    // ─── Pay Component master ──────────────────────────────────────────────────

    public async Task<IEnumerable<PayComponentDto>> GetPayComponentsAsync(bool activeOnly = true)
    {
        await EnsureComponentsCurrentAsync();

        var tenantId = GetTenantId();
        var query = _componentRepo.GetQueryable().Where(c => c.TenantId == tenantId);
        if (activeOnly) query = query.Where(c => c.IsActive);
        var items = await query.OrderBy(c => c.ComponentType).ThenBy(c => c.Name).ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<PayComponentDto> GetPayComponentByIdAsync(Guid id)
    {
        await EnsureComponentsCurrentAsync();

        var entity = await GetOwnedPayComponentAsync(id);
        return ToDto(entity);
    }

    public async Task<PayComponentProjectionResultDto> SyncPayComponentsAsync(CancellationToken ct = default)
        => await _componentProjection.ReconcileAsync(GetTenantId(), ct);

    /// <summary>
    /// Creating a pay component in HR is not supported: payroll is the single source of truth for
    /// the component master, and anything created here would not exist in payroll to be paid.
    /// </summary>
    public Task<PayComponentDto> CreatePayComponentAsync(CreatePayComponentDto dto) =>
        throw new PayrollOwnedException(
            "Pay components are defined in Payroll and mirrored into HR. Create the component in " +
            "Payroll (Administration → HR → Payroll → Components), then sync.");

    /// <summary>
    /// Full update. Refused for mirrored components — the next projection pass would overwrite the
    /// change, so <see cref="UpdatePayComponentHrAttributesAsync"/> is the only way to edit those.
    ///
    /// Components HR defined itself are still fully editable. The emolument seeder creates six
    /// (HOUSING, TRANSPORT, MEDICAL, RESP, PAYE, PENSION) that payroll does not know about and
    /// never will; freezing them would leave things like PENSION's 5.5% rate uncorrectable.
    /// </summary>
    public async Task<PayComponentDto> UpdatePayComponentAsync(Guid id, UpdatePayComponentDto dto)
    {
        var entity = await GetOwnedPayComponentAsync(id);

        if (IsPayrollDefined(entity))
            throw new PayrollOwnedException(
                $"'{entity.Name}' is defined in Payroll and mirrored into HR, so its code, name, type, " +
                "calculation basis, amount, taxability and active flag would be overwritten by the next " +
                "sync. Edit those in Payroll. Pension, tax treatment, gross-pay effect and effective " +
                "dates are HR-owned and can be changed here.");

        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code != entity.Code)
        {
            var clash = await _componentRepo.GetQueryable()
                .AnyAsync(c => c.TenantId == tenantId && c.Code == dto.Code && c.Id != id);
            if (clash)
                throw new InvalidOperationException($"A pay component with code '{dto.Code}' already exists.");
            entity.Code = dto.Code.Trim();
        }

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.ComponentType = dto.ComponentType;
        entity.CalculationBasis = dto.CalculationBasis;
        entity.DefaultAmount = dto.DefaultAmount;
        entity.IsTaxable = dto.IsTaxable;
        if (dto.EffectiveFrom != default) entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsActive = dto.IsActive;

        await _componentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(entity);
    }

    /// <summary>
    /// Refused for mirrored components — HR mirrors payroll's active flag, so the next sync would
    /// simply restore it. HR-defined components can still be deactivated here.
    /// </summary>
    public async Task DeactivatePayComponentAsync(Guid id)
    {
        var entity = await GetOwnedPayComponentAsync(id);

        if (IsPayrollDefined(entity))
            throw new PayrollOwnedException(
                $"'{entity.Name}' is defined in Payroll. Deactivate it there — HR mirrors the active " +
                "flag and the next sync would restore it.");

        entity.IsActive = false;
        await _componentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the fields HR owns. Payroll models none of them, so the projection never touches
    /// them and they stay editable on mirrored components — otherwise SSNIT and tax treatment
    /// would be stuck at their defaults on every component.
    /// </summary>
    public async Task<PayComponentDto> UpdatePayComponentHrAttributesAsync(
        Guid id, UpdatePayComponentHrAttributesDto dto)
    {
        var entity = await GetOwnedPayComponentAsync(id);

        if (dto.EffectiveTo.HasValue && dto.EffectiveFrom != default && dto.EffectiveTo < dto.EffectiveFrom)
            throw new InvalidOperationException("The effective-to date cannot be before the effective-from date.");

        entity.IsPensionable = dto.IsPensionable;
        entity.AffectsGrossPay = dto.AffectsGrossPay;
        entity.StatutoryTreatment = dto.StatutoryTreatment;
        if (dto.EffectiveFrom != default) entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;

        await _componentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Pay component HR attributes updated: {code}", entity.Code);
        return ToDto(entity);
    }

    // ─── Position-level assignment ─────────────────────────────────────────────

    public async Task<IEnumerable<PositionPayComponentDto>> GetPositionComponentsAsync(Guid positionId)
    {
        var tenantId = GetTenantId();
        var items = await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent)
            .Include(pc => pc.Position)
            .Where(pc => pc.TenantId == tenantId && pc.PositionId == positionId)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<PositionPayComponentDto> AssignPositionComponentAsync(CreatePositionPayComponentDto dto)
    {
        var tenantId = GetTenantId();

        var component = await _componentRepo.GetByIdAsync(dto.PayComponentId);
        if (component == null || component.TenantId != tenantId)
            throw new ArgumentException($"Pay component '{dto.PayComponentId}' not found.");

        var clash = await _positionCompRepo.GetQueryable()
            .AnyAsync(pc => pc.TenantId == tenantId
                && pc.PositionId == dto.PositionId
                && pc.PayComponentId == dto.PayComponentId);
        if (clash)
            throw new InvalidOperationException("This component is already assigned to the position.");

        var entity = new PositionPayComponent
        {
            TenantId = tenantId,
            PositionId = dto.PositionId,
            PayComponentId = dto.PayComponentId,
            Amount = dto.Amount,
            IsActive = true
        };
        await _positionCompRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent).Include(pc => pc.Position)
            .FirstAsync(pc => pc.TenantId == tenantId && pc.Id == entity.Id));
    }

    public async Task<PositionPayComponentDto> UpdatePositionComponentAsync(Guid id, decimal? amount, bool isActive)
    {
        var entity = await GetOwnedPositionPayComponentAsync(id);
        var tenantId = GetTenantId();
        entity.Amount = amount;
        entity.IsActive = isActive;
        await _positionCompRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent).Include(pc => pc.Position)
            .FirstAsync(pc => pc.TenantId == tenantId && pc.Id == id));
    }

    public async Task RemovePositionComponentAsync(Guid id)
    {
        var entity = await GetOwnedPositionPayComponentAsync(id);
        await _positionCompRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Employee-level override / addition ────────────────────────────────────

    public async Task<IEnumerable<EmployeePayComponentDto>> GetEmployeeComponentsAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var items = await _employeeCompRepo.GetQueryable()
            .Include(ec => ec.PayComponent)
            .Include(ec => ec.Employee)
            .Where(ec => ec.TenantId == tenantId && ec.EmployeeId == employeeId)
            .OrderByDescending(ec => ec.EffectiveFrom)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<EmployeePayComponentDto> AssignEmployeeComponentAsync(CreateEmployeePayComponentDto dto)
    {
        var tenantId = GetTenantId();

        var employee = await _employeeRepo.GetByIdAsync(dto.EmployeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw new ArgumentException($"Employee '{dto.EmployeeId}' not found.");

        var component = await _componentRepo.GetByIdAsync(dto.PayComponentId);
        if (component == null || component.TenantId != tenantId)
            throw new ArgumentException($"Pay component '{dto.PayComponentId}' not found.");

        var entity = new EmployeePayComponent
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            PayComponentId = dto.PayComponentId,
            Amount = dto.Amount,
            EffectiveFrom = dto.EffectiveFrom == default ? _clock.UtcNow : dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = true
        };
        await _employeeCompRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(await _employeeCompRepo.GetQueryable()
            .Include(ec => ec.PayComponent).Include(ec => ec.Employee)
            .FirstAsync(ec => ec.TenantId == tenantId && ec.Id == entity.Id));
    }

    public async Task<EmployeePayComponentDto> UpdateEmployeeComponentAsync(Guid id, UpdateEmployeePayComponentDto dto)
    {
        var entity = await GetOwnedEmployeePayComponentAsync(id);
        var tenantId = GetTenantId();

        var component = await _componentRepo.GetByIdAsync(dto.PayComponentId);
        if (component == null || component.TenantId != tenantId)
            throw new ArgumentException($"Pay component '{dto.PayComponentId}' not found.");

        entity.PayComponentId = dto.PayComponentId;
        entity.Amount = dto.Amount;
        if (dto.EffectiveFrom != default) entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsActive = dto.IsActive;
        await _employeeCompRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(await _employeeCompRepo.GetQueryable()
            .Include(ec => ec.PayComponent).Include(ec => ec.Employee)
            .FirstAsync(ec => ec.TenantId == tenantId && ec.Id == id));
    }

    public async Task RemoveEmployeeComponentAsync(Guid id)
    {
        var entity = await GetOwnedEmployeePayComponentAsync(id);
        await _employeeCompRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Consolidated / derived ────────────────────────────────────────────────

    public async Task<EmployeeEmolumentSummaryDto> GetEmployeeEmolumentSummaryAsync(Guid employeeId, DateOnly? asOf = null)
    {
        var asOfDate = asOf ?? _clock.TodayUtc;
        var tenantId = GetTenantId();

        var employee = await _employeeRepo.GetQueryable()
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        var basic = await GetMonthlyBasicPayAsync(employeeId, asOfDate);
        var components = await ComputeEffectiveComponentsAsync(employee, basic, asOfDate);

        var allowances = components.Where(c => c.ComponentType == PayComponentType.Allowance).Sum(c => c.Amount);
        var deductions = components.Where(c => c.ComponentType == PayComponentType.Deduction).Sum(c => c.Amount);

        return new EmployeeEmolumentSummaryDto
        {
            EmployeeId = employeeId,
            EmployeeName = employee.FullName,
            PositionTitle = employee.Position?.Title,
            AsOfDate = asOfDate,
            MonthlyBasicPay = basic,
            TotalAllowances = allowances,
            TotalDeductions = deductions,
            GrossMonthly = basic + allowances,
            NetMonthly = basic + allowances - deductions,
            Components = components
        };
    }

    /// <remarks>
    /// <para>⚠ <b>This had its own copy of the basic-pay rule and it had never heard of the pay
    /// basis</b> — so a negotiated employee's benefit contribution was computed from a notch amount
    /// HR had explicitly said was not their pay. Lane E1b made <see cref="HrBasicPay"/> the single
    /// resolution and pointed this, the reconciliation and the separation settlement at it.</para>
    ///
    /// <para>Zero, not null, is still the answer for somebody the run does not pay — that is what
    /// makes an encashment, a benefit contribution or a costing come out as "nothing from payroll",
    /// and callers depend on it.</para>
    /// </remarks>
    public async Task<decimal> GetMonthlyBasicPayAsync(Guid employeeId, DateOnly asOf)
    {
        var asOfDt = asOf.ToDateTime(TimeOnly.MinValue);
        var tenantId = GetTenantId();

        var employee = await _employeeRepo.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId || !employee.IsOnPayroll)
            return 0m;

        // ⚠ WithdrawnAt is what keeps a placement withdrawn before its start date out of this. It
        // has no window to exclude it: see EmployeeSalaryAssignment.WithdrawnAt.
        var assignment = await _salaryAssignmentRepo.GetQueryable()
            .Include(a => a.Notch)
            .Include(a => a.Level)
            .Include(a => a.Grade)
            .Where(a => a.TenantId == tenantId
                && a.EmployeeId == employeeId
                && !a.IsDeleted
                && a.WithdrawnAt == null
                && a.EffectiveDate <= asOfDt
                && (a.EffectiveTo == null || a.EffectiveTo >= asOfDt))
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync();

        // Payroll's basis, for the negotiated branch. ⚠ Payroll keeps ONE basis row and mutates it
        // in place (round-2 plan § 7.1 item 3), so this is its present figure whatever asOf says —
        // the limitation HrBasicPay documents rather than hides.
        var payrollBasic = await _unitOfWork.Repository<PayrollEmployeeProfile>().GetQueryable()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.EmployeeId == employeeId)
            .Select(p => p.SalaryBasis != null && p.SalaryBasis.IsActive
                ? (decimal?)p.SalaryBasis.MonthlyBasicSalary
                : null)
            .FirstOrDefaultAsync();

        return HrBasicPay.Resolve(employee, assignment, payrollBasic).Amount ?? 0m;
    }

    public async Task<EncashmentDailyRate> GetEncashmentDailyRateAsync(Guid employeeId, Guid leaveTypeId, DateOnly asOf)
    {
        var tenantId = GetTenantId();

        var leaveType = await _leaveTypeRepo.GetQueryable()
            .Include(lt => lt.LeaveTypeAllowances)
            .FirstOrDefaultAsync(lt => lt.TenantId == tenantId && lt.Id == leaveTypeId);
        if (leaveType == null) throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");

        if (leaveType.EncashmentRateBasis == EncashmentRateBasis.Manual)
        {
            var manual = leaveType.EncashmentRatePerDay ?? 0m;
            return new EncashmentDailyRate(manual,
                manual > 0m
                    ? $"A fixed rate of {manual:N4} per day, set on the '{leaveType.Name}' leave type."
                    : $"No rate is set on the '{leaveType.Name}' leave type, which is configured to use a fixed one.");
        }

        var employee = await _employeeRepo.GetQueryable()
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        var basic = await GetMonthlyBasicPayAsync(employeeId, asOf);
        var components = await ComputeEffectiveComponentsAsync(employee, basic, asOf);

        var linkedIds = leaveType.LeaveTypeAllowances.Select(a => a.PayComponentId).ToHashSet();
        var linkedAllowances = components
            .Where(c => c.ComponentType == PayComponentType.Allowance && linkedIds.Contains(c.PayComponentId))
            .Sum(c => c.Amount);

        // ⚠ The fallback is the TENANT'S number now, not a private const. It was
        // `DefaultWorkingDaysPerMonth = 22` in this file — the last genuinely hardcoded piece of the
        // encashment rate, and the one every leave type lands on until somebody edits its own
        // divisor. A constant that decides what a day of leave is worth is a policy, and policy
        // belongs in settings where a client can see and change it (residue plan G2).
        var policy = await _policyProvider.GetAsync();
        var divisor = leaveType.EncashmentWorkingDaysPerMonth > 0
            ? leaveType.EncashmentWorkingDaysPerMonth
            : policy.EncashmentWorkingDaysPerMonth;

        // Guard the fallback's fallback: a settings row edited to 0 would divide by zero, and
        // [Range] only binds on the way in through the API.
        if (divisor <= 0) divisor = DefaultWorkingDaysPerMonth;

        var monthly = basic + linkedAllowances;
        var rate = Math.Round(monthly / divisor, 2);

        // ⚠ Built from the very number used above, so the words can never describe a basis other
        // than the one that produced the figure beside them.
        var source = leaveType.EncashmentWorkingDaysPerMonth > 0
            ? $"the '{leaveType.Name}' leave type"
            : "HR policy settings";

        return new EncashmentDailyRate(rate,
            $"{monthly:N2} (basic + linked allowances) ÷ {divisor} working days = {rate:N2} per day, "
            + $"per {source}.");
    }

    // ─── Effective-component composition ───────────────────────────────────────

    /// <summary>
    /// Composes the employee's effective components: position-level defaults first, then
    /// employee-level rows overriding (same component) or adding to them. Percentage-of-basic
    /// components are resolved to a monetary amount against <paramref name="basic"/>.
    /// </summary>
    private async Task<List<EffectivePayComponentDto>> ComputeEffectiveComponentsAsync(
        Employee employee, decimal basic, DateOnly asOf)
    {
        var asOfDt = asOf.ToDateTime(TimeOnly.MinValue);
        var tenantId = GetTenantId();
        var byComponent = new Dictionary<Guid, EffectivePayComponentDto>();

        // Position-level defaults
        var positionComps = await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent)
            .Where(pc => pc.TenantId == tenantId
                && pc.PositionId == employee.PositionId
                && pc.IsActive
                && pc.PayComponent.IsActive
                && pc.PayComponent.EffectiveFrom <= asOfDt
                && (pc.PayComponent.EffectiveTo == null || pc.PayComponent.EffectiveTo >= asOfDt))
            .ToListAsync();

        foreach (var pc in positionComps)
        {
            var raw = pc.Amount ?? pc.PayComponent.DefaultAmount ?? 0m;
            byComponent[pc.PayComponentId] = BuildEffective(pc.PayComponent, raw, basic, "Position");
        }

        // Employee-level overrides / additions
        var employeeComps = await _employeeCompRepo.GetQueryable()
            .Include(ec => ec.PayComponent)
            .Where(ec => ec.TenantId == tenantId
                && ec.EmployeeId == employee.Id
                && ec.IsActive
                && ec.PayComponent.IsActive
                && ec.EffectiveFrom <= asOfDt
                && (ec.EffectiveTo == null || ec.EffectiveTo >= asOfDt))
            .ToListAsync();

        foreach (var ec in employeeComps)
            byComponent[ec.PayComponentId] = BuildEffective(ec.PayComponent, ec.Amount, basic, "Employee");

        return byComponent.Values
            .OrderBy(c => c.ComponentType)
            .ThenBy(c => c.Name)
            .ToList();
    }

    private static EffectivePayComponentDto BuildEffective(PayComponent comp, decimal raw, decimal basic, string source)
    {
        var monetary = comp.CalculationBasis == PayComponentCalculationBasis.PercentageOfBasic
            ? Math.Round(basic * raw / 100m, 2)
            : raw;

        return new EffectivePayComponentDto
        {
            PayComponentId = comp.Id,
            Code = comp.Code,
            Name = comp.Name,
            ComponentType = comp.ComponentType,
            CalculationBasis = comp.CalculationBasis,
            Amount = monetary,
            IsTaxable = comp.IsTaxable,
            Source = source
        };
    }

    // ─── Mapping ───────────────────────────────────────────────────────────────

    private static PayComponentDto ToDto(PayComponent e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        ComponentType = e.ComponentType,
        CalculationBasis = e.CalculationBasis,
        DefaultAmount = e.DefaultAmount,
        IsTaxable = e.IsTaxable,
        IsActive = e.IsActive,
        // HR-owned — the projection leaves these alone.
        IsPensionable = e.IsPensionable,
        AffectsGrossPay = e.AffectsGrossPay,
        StatutoryTreatment = e.StatutoryTreatment,
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo,
        IsPayrollDefined = IsPayrollDefined(e)
    };

    private static PositionPayComponentDto ToDto(PositionPayComponent e) => new()
    {
        Id = e.Id,
        PositionId = e.PositionId,
        PositionTitle = e.Position?.Title ?? string.Empty,
        PayComponentId = e.PayComponentId,
        PayComponentName = e.PayComponent?.Name ?? string.Empty,
        ComponentType = e.PayComponent?.ComponentType ?? PayComponentType.Allowance,
        Amount = e.Amount,
        DefaultAmount = e.PayComponent?.DefaultAmount,
        IsActive = e.IsActive
    };

    private static EmployeePayComponentDto ToDto(EmployeePayComponent e) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        PayComponentId = e.PayComponentId,
        PayComponentName = e.PayComponent?.Name ?? string.Empty,
        ComponentType = e.PayComponent?.ComponentType ?? PayComponentType.Allowance,
        Amount = e.Amount,
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo,
        IsActive = e.IsActive
    };
}
