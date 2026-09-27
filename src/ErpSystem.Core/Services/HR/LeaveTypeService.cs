using ErpSystem.Application.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class LeaveTypeService : ILeaveTypeService
{
    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly IGenericRepository<LeaveSubType> _leaveSubTypeRepository;
    private readonly IGenericRepository<LeaveCategoryAllocation> _allocationRepository;
    private readonly IGenericRepository<LeaveTypeEligibility> _eligibilityRepository;
    private readonly IGenericRepository<LeaveAccrualPolicy> _accrualPolicyRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveTypeService> _logger;

    public LeaveTypeService(
        ILeaveTypeRepository leaveTypeRepository,
        IGenericRepository<LeaveSubType> leaveSubTypeRepository,
        IGenericRepository<LeaveCategoryAllocation> allocationRepository,
        IGenericRepository<LeaveTypeEligibility> eligibilityRepository,
        IGenericRepository<LeaveAccrualPolicy> accrualPolicyRepository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<LeaveTypeService> logger)
    {
        _leaveTypeRepository = leaveTypeRepository;
        _leaveSubTypeRepository = leaveSubTypeRepository;
        _allocationRepository = allocationRepository;
        _eligibilityRepository = eligibilityRepository;
        _accrualPolicyRepository = accrualPolicyRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A leave type owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave type '{id}' not found.");
        return entity;
    }

    private async Task<LeaveSubType> GetOwnedSubTypeAsync(Guid id)
    {
        var entity = await _leaveSubTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave sub-type '{id}' not found.");
        return entity;
    }

    private async Task<LeaveCategoryAllocation> GetOwnedAllocationAsync(Guid id)
    {
        var entity = await _allocationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave category allocation '{id}' not found.");
        return entity;
    }

    private async Task<LeaveTypeEligibility> GetOwnedEligibilityAsync(Guid id)
    {
        var entity = await _eligibilityRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Eligibility rule '{id}' not found.");
        return entity;
    }

    private async Task<LeaveAccrualPolicy> GetOwnedAccrualPolicyAsync(Guid id)
    {
        var entity = await _accrualPolicyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Accrual policy '{id}' not found.");
        return entity;
    }

    // ─── Leave Type ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveTypeDto>> GetAllLeaveTypesAsync(bool activeOnly = true)
    {
        var tenantId = GetTenantId();
        var query = _leaveTypeRepository.GetQueryable().Where(lt => lt.TenantId == tenantId);
        if (activeOnly)
            query = query.Where(lt => lt.IsActive);
        var items = await query.OrderBy(lt => lt.Name).ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveTypeDto> GetLeaveTypeByIdAsync(Guid id)
    {
        var entity = await GetOwnedLeaveTypeAsync(id);
        return entity.ToDto();
    }

    public async Task<LeaveTypeDetailDto> GetLeaveTypeDetailAsync(Guid id)
    {
        var entity = await GetOwnedLeaveTypeAsync(id);

        return new LeaveTypeDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            IsPaid = entity.IsPaid,
            DefaultDaysPerYear = entity.DefaultDaysPerYear,
            MaxDaysPerYear = entity.MaxDaysPerYear,
            MinDaysNotice = entity.MinDaysNotice,
            RequiresApproval = entity.RequiresApproval,
            CalendarColor = entity.CalendarColor,
            HasSubTypes = entity.HasSubTypes,
            AllowCarryOver = entity.AllowCarryOver,
            MaxCarryOverDays = entity.MaxCarryOverDays,
            CountWeekendsAsLeave = entity.CountWeekendsAsLeave,
            CountHolidaysAsLeave = entity.CountHolidaysAsLeave,
            AllowCashConversion = entity.AllowCashConversion,
            RequiresMedicalCertificate = entity.RequiresMedicalCertificate,
            SelfCertificationDays = entity.SelfCertificationDays,
            MedicalBoardThresholdDays = entity.MedicalBoardThresholdDays,
            RequiresReliever = entity.RequiresReliever,
            MinServiceMonthsToAccess = entity.MinServiceMonthsToAccess,
            CarryOverExpiryMonths = entity.CarryOverExpiryMonths,
            ForfeitUnusedAfterMonths = entity.ForfeitUnusedAfterMonths,
            // ⚠ This projection is hand-written and is the THIRD place a LeaveType field has to be
            // listed (entity, mapper, here). Harness finding 13 was exactly this shape: the column,
            // the entity, the enum, the DTO and the parameter were all correct and the MAPPER was
            // missed, so the value read back as its default however it was set.
            YearEndBasis = entity.YearEndBasis,
            ProRateFirstYearEntitlement = entity.ProRateFirstYearEntitlement,
            Category = entity.Category,
            AllowOffsetAgainstAnnual = entity.AllowOffsetAgainstAnnual,
            IsActive = entity.IsActive,
            SubTypes = (await GetSubTypesAsync(id)).ToList(),
            Allocations = (await GetAllocationsAsync(id)).ToList(),
            Eligibilities = (await GetEligibilityRulesAsync(id)).ToList(),
            AccrualPolicies = (await GetAccrualPoliciesAsync(id)).ToList(),
        };
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto)
    {
        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var existing = await _leaveTypeRepository.GetQueryable()
                .AnyAsync(lt => lt.TenantId == tenantId && lt.Code == dto.Code);
            if (existing)
                throw new InvalidOperationException($"A leave type with code '{dto.Code}' already exists.");
        }

        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        if (entity.Category == LeaveTypeCategory.Annual && entity.IsActive)
            await RefuseSecondActiveAnnualAsync(tenantId, exceptId: null);
        RefuseOffsetWhereItCannotBind(entity);
        await _leaveTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave type created: {name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<LeaveTypeDto> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeDto dto)
    {
        var entity = await GetOwnedLeaveTypeAsync(id);
        var tenantId = entity.TenantId;

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code != entity.Code)
        {
            var duplicate = await _leaveTypeRepository.GetQueryable()
                .AnyAsync(lt => lt.TenantId == tenantId && lt.Code == dto.Code && lt.Id != id);
            if (duplicate)
                throw new InvalidOperationException($"A leave type with code '{dto.Code}' already exists.");
        }

        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Description = dto.Description;
        entity.IsPaid = dto.IsPaid;
        entity.DefaultDaysPerYear = dto.DefaultDaysPerYear;
        entity.MaxDaysPerYear = dto.MaxDaysPerYear;
        entity.MinDaysNotice = dto.MinDaysNotice;
        entity.RequiresApproval = dto.RequiresApproval;
        entity.CalendarColor = dto.CalendarColor;
        // HasSubTypes is derived from the sub-types themselves (lane N2), not written by a save.
        entity.AllowCarryOver = dto.AllowCarryOver;
        entity.MaxCarryOverDays = dto.MaxCarryOverDays;
        entity.CountWeekendsAsLeave = dto.CountWeekendsAsLeave;
        entity.CountHolidaysAsLeave = dto.CountHolidaysAsLeave;
        entity.AllowCashConversion = dto.AllowCashConversion;
        entity.RequiresMedicalCertificate = dto.RequiresMedicalCertificate;
        entity.SelfCertificationDays = dto.SelfCertificationDays;
        entity.MedicalBoardThresholdDays = dto.MedicalBoardThresholdDays;
        entity.RequiresReliever = dto.RequiresReliever;
        entity.MinServiceMonthsToAccess = dto.MinServiceMonthsToAccess;
        entity.CarryOverExpiryMonths = dto.CarryOverExpiryMonths;
        entity.ForfeitUnusedAfterMonths = dto.ForfeitUnusedAfterMonths;
        entity.YearEndBasis = dto.YearEndBasis;
        // ⚠ The other door: pro-rating switched on for a type that already accrues incrementally.
        await RefuseProrationWithIncrementalAccrualAsync(
            id, tenantId, dto.ProRateFirstYearEntitlement);
        entity.ProRateFirstYearEntitlement = dto.ProRateFirstYearEntitlement;
        // ⚠ Null leaves the kind as it is: an older caller echoing the type back without it must
        // not turn the tenant's Annual Leave into Other (see CreateLeaveTypeDto.Category).
        if (dto.Category is LeaveTypeCategory category)
            entity.Category = category;
        // Null leaves it as it is, for the same reason (round 5, lane H).
        if (dto.AllowOffsetAgainstAnnual is bool offset)
            entity.AllowOffsetAgainstAnnual = offset;
        entity.IsActive = dto.IsActive;

        // Both doors: making a type Annual, and re-activating an Annual one.
        if (entity.Category == LeaveTypeCategory.Annual && entity.IsActive)
            await RefuseSecondActiveAnnualAsync(tenantId, exceptId: id);

        // Both doors again: switching the offset on, and changing the kind or the approval under it.
        RefuseOffsetWhereItCannotBind(entity);

        await _leaveTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeactivateLeaveTypeAsync(Guid id)
    {
        var entity = await GetOwnedLeaveTypeAsync(id);

        entity.IsActive = false;
        await _leaveTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Leave type deactivated: {id}", id);
    }

    // ─── Sub Types ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sub-types of a leave type. <paramref name="activeOnly"/> defaults to false so the rulebook
    /// tab keeps showing retired rows (they are what the Status column is for); the request forms
    /// pass true, because <c>LeaveSubType.IsActive</c> was honoured by nothing and a retired
    /// sub-type stayed pickable for ever (closure plan L-34).
    /// </summary>
    public async Task<IEnumerable<LeaveSubTypeDto>> GetSubTypesAsync(Guid leaveTypeId, bool activeOnly = false)
    {
        await GetOwnedLeaveTypeAsync(leaveTypeId);
        var tenantId = GetTenantId();
        var query = _leaveSubTypeRepository
            .GetQueryable()
            .Include(st => st.LeaveType)
            .Where(st => st.TenantId == tenantId && st.LeaveTypeId == leaveTypeId);

        if (activeOnly)
            query = query.Where(st => st.IsActive);

        var items = await query.OrderBy(st => st.SubTypeName).ToListAsync();
        return items.ToDtoList();
    }

    /// <summary>
    /// <c>HasSubTypes</c> is derived: a type has sub-types when it has an active one (round 5, lane
    /// N2).
    /// </summary>
    /// <remarks>
    /// It was a checkbox whose only effect was to refuse creating a sub-type while it was off, so
    /// it said nothing true about the type. Set here after every sub-type change; a type save no
    /// longer writes it.
    /// </remarks>
    private async Task SyncHasSubTypesAsync(Guid leaveTypeId)
    {
        var tenantId = GetTenantId();
        var leaveType = await GetOwnedLeaveTypeAsync(leaveTypeId);
        var hasActive = await _leaveSubTypeRepository.GetQueryable()
            .AnyAsync(st => st.TenantId == tenantId && st.LeaveTypeId == leaveTypeId && st.IsActive);
        if (leaveType.HasSubTypes == hasActive) return;

        leaveType.HasSubTypes = hasActive;
        await _leaveTypeRepository.UpdateAsync(leaveType);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<LeaveSubTypeDto> CreateSubTypeAsync(CreateLeaveSubTypeDto dto)
    {
        await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);

        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        await _leaveSubTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await SyncHasSubTypesAsync(entity.LeaveTypeId);
        return (await _leaveSubTypeRepository.GetQueryable()
            .Include(st => st.LeaveType)
            .FirstOrDefaultAsync(st => st.TenantId == tenantId && st.Id == entity.Id))!.ToDto();
    }

    public async Task<LeaveSubTypeDto> UpdateSubTypeAsync(Guid id, CreateLeaveSubTypeDto dto)
    {
        var entity = await GetOwnedSubTypeAsync(id);
        var tenantId = entity.TenantId;

        entity.SubTypeName = dto.SubTypeName;
        entity.Description = dto.Description;
        entity.MaxDaysAllowed = dto.MaxDaysAllowed;
        entity.IsActive = dto.IsActive;

        await _leaveSubTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await SyncHasSubTypesAsync(entity.LeaveTypeId);
        return (await _leaveSubTypeRepository.GetQueryable()
            .Include(st => st.LeaveType)
            .FirstOrDefaultAsync(st => st.TenantId == tenantId && st.Id == id))!.ToDto();
    }

    public async Task DeleteSubTypeAsync(Guid id)
    {
        var entity = await GetOwnedSubTypeAsync(id);

        await _leaveSubTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await SyncHasSubTypesAsync(entity.LeaveTypeId);
    }

    // ─── Category Allocations ────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveCategoryAllocationDto>> GetAllocationsAsync(Guid leaveTypeId)
    {
        await GetOwnedLeaveTypeAsync(leaveTypeId);
        var tenantId = GetTenantId();
        var items = await _allocationRepository
            .GetQueryable()
            .Include(a => a.LeaveType)
            .Include(a => a.StaffLevel)
            .Where(a => a.TenantId == tenantId && a.LeaveTypeId == leaveTypeId)
            .OrderBy(a => a.StaffLevelId)
            .ThenBy(a => a.EffectiveFrom)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveCategoryAllocationDto> CreateAllocationAsync(CreateLeaveCategoryAllocationDto dto)
    {
        await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        await _allocationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _allocationRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .Include(a => a.StaffLevel)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == entity.Id))!.ToDto();
    }

    public async Task<LeaveCategoryAllocationDto> UpdateAllocationAsync(Guid id, CreateLeaveCategoryAllocationDto dto)
    {
        var entity = await GetOwnedAllocationAsync(id);
        await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        var tenantId = entity.TenantId;

        entity.LeaveTypeId = dto.LeaveTypeId;
        // Always the whole type (round 5, lane N2); there is no sub-type column any more (L-76).
        entity.StaffLevelId = dto.StaffLevelId;
        entity.AllocationDays = dto.AllocationDays;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;

        await _allocationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _allocationRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .Include(a => a.StaffLevel)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id))!.ToDto();
    }

    public async Task DeleteAllocationAsync(Guid id)
    {
        var entity = await GetOwnedAllocationAsync(id);

        await _allocationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Eligibility Rules ───────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveTypeEligibilityDto>> GetEligibilityRulesAsync(Guid leaveTypeId)
    {
        await GetOwnedLeaveTypeAsync(leaveTypeId);
        var tenantId = GetTenantId();
        var items = await _eligibilityRepository
            .GetQueryable()
            .Include(e => e.LeaveType)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Where(e => e.TenantId == tenantId && e.LeaveTypeId == leaveTypeId)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveTypeEligibilityDto> CreateEligibilityRuleAsync(CreateLeaveTypeEligibilityDto dto)
    {
        await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        await _eligibilityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _eligibilityRepository.GetQueryable()
            .Include(e => e.LeaveType)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == entity.Id))!.ToDto();
    }

    public async Task DeleteEligibilityRuleAsync(Guid id)
    {
        var entity = await GetOwnedEligibilityAsync(id);

        await _eligibilityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Accrual Policies ────────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveAccrualPolicyDto>> GetAccrualPoliciesAsync(Guid leaveTypeId)
    {
        await GetOwnedLeaveTypeAsync(leaveTypeId);
        var tenantId = GetTenantId();
        var items = await _accrualPolicyRepository
            .GetQueryable()
            .Include(a => a.LeaveType)
            .Where(a => a.TenantId == tenantId && a.LeaveTypeId == leaveTypeId)
            .ToListAsync();
        return items.ToDtoList();
    }

    /// <summary>
    /// ⚠ <b>A leave type may have ONE active accrual policy, and this is what enforces it.</b>
    /// </summary>
    /// <remarks>
    /// <para>Entitlement plan A2. <c>LeaveEntitlementService</c> selects the policy with
    /// <c>FirstOrDefault</c> over the active ones — so a second policy did not produce an error, it
    /// produced a <b>non-deterministic accrual figure</b> that could differ between two reads of the
    /// same balance.</para>
    ///
    /// <para>⚠ <b>The natural thing an administrator wants here is a different rate per staff
    /// level</b>, and the natural thing to try is a second policy. Until that is built (plan B4) the
    /// honest answer is a refusal that says so, rather than silently accepting a row that makes the
    /// answer depend on row order.</para>
    /// </remarks>
    private async Task RefuseSecondActiveAccrualPolicyAsync(
        Guid leaveTypeId, Guid tenantId, Guid? exceptId = null)
    {
        var existing = await _accrualPolicyRepository
            .GetQueryable()
            .Where(a => a.TenantId == tenantId
                     && a.LeaveTypeId == leaveTypeId
                     && a.IsActive
                     && (exceptId == null || a.Id != exceptId))
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (existing != Guid.Empty)
            throw new InvalidOperationException(
                "This leave type already has an active accrual policy, and a leave type may only have one. "
                + "Edit the existing policy, or remove it first. "
                + "(An accrual rate that varies by staff level is not supported yet — a second policy "
                + "would make the accrued figure depend on which row the database returned first.)");
    }

    /// <summary>
    /// ⚠ <b>At most one active Annual leave type per tenant</b> (round 5, decision A4).
    /// </summary>
    /// <remarks>
    /// The plans, in-service encashment, the compliance register and the untaken-leave reminder each
    /// read "the annual leave", and the balances view and the leaver's settlement will (round 5 lanes
    /// J and L2). Two would make every one of them guess, and a guess over <c>FirstOrDefault</c> is a
    /// figure that can differ between two reads: the shape
    /// <see cref="RefuseSecondActiveAccrualPolicyAsync"/> exists to refuse, mirrored here.
    /// </remarks>
    private async Task RefuseSecondActiveAnnualAsync(Guid tenantId, Guid? exceptId)
    {
        var existing = await _leaveTypeRepository.GetQueryable()
            .Where(lt => lt.TenantId == tenantId
                      && lt.Category == LeaveTypeCategory.Annual
                      && lt.IsActive
                      && (exceptId == null || lt.Id != exceptId))
            .Select(lt => lt.Name)
            .FirstOrDefaultAsync();

        if (existing != null)
            throw new InvalidOperationException(
                $"'{existing}' is already this organisation's annual leave, and there can only be one. "
                + "Make this a different kind, or retire the other first.");
    }

    /// <summary>
    /// ⚠ <b>Days beyond the limit go to annual leave only from an Other kind that requires
    /// approval</b> (round 5, lane H, decision A5).
    /// </summary>
    /// <remarks>
    /// Annual leave has nothing to overflow into, and maternity leave is statutory: its days are its
    /// own. The request is split at its final approval, which is where HR decides the charge, so a
    /// type approved automatically would charge annual leave with nobody deciding. Refused rather
    /// than saved and ignored.
    /// </remarks>
    private static void RefuseOffsetWhereItCannotBind(LeaveType type)
    {
        if (!type.AllowOffsetAgainstAnnual) return;

        if (type.Category != LeaveTypeCategory.Other)
            throw new InvalidOperationException(
                "Only leave of the Other kind can charge the days beyond its limit to annual leave, and this is "
                + (type.Category == LeaveTypeCategory.Annual ? "the annual leave itself." : "maternity leave."));

        if (!type.RequiresApproval)
            throw new InvalidOperationException(
                "The days beyond the limit are charged to annual leave when the request is approved, and HR "
                + "decides it there. A leave type that allows it must require approval.");
    }

    /// <summary>
    /// ⚠ <b><c>PerPayPeriod</c> is retired</b> (entitlement plan B5, decision D-6).
    /// </summary>
    /// <remarks>
    /// <para>The accrual engine maps it to <b>twelve periods a year</b> and counts elapsed
    /// <i>months</i> for it — so on a fortnightly or weekly payroll it is simply wrong, while its
    /// name is an active claim that it is not. It was a synonym for <c>Monthly</c> wearing a more
    /// specific label.</para>
    ///
    /// <para><b>Retired rather than implemented, on purpose.</b> How often a tenant pays is
    /// <b>payroll's fact</b>, and modelling it here would create a second rulebook for something
    /// another module owns — the shape <c>HR-PAYROLL-BOUNDARY.md</c> exists to prevent. If a client
    /// needs real pay-period accrual, it is a cross-module contract, not an HR setting.</para>
    ///
    /// <para>⚠ <b>The enum value stays.</b> Rows already carrying it still read and still accrue
    /// exactly as they did; what is refused is choosing it anew. Deleting the value would break
    /// stored data to tidy a picker.</para>
    /// </remarks>
    private static void RefusePerPayPeriod(AccrualFrequency frequency)
    {
        if (frequency == AccrualFrequency.PerPayPeriod)
            throw new InvalidOperationException(
                "Accrual per pay period is not available. It was only ever a synonym for Monthly — the "
                + "engine credits one period a month whatever the payroll cycle is — so choose Monthly "
                + "if that is what you mean. Accruing on a real fortnightly or weekly cycle needs the "
                + "pay calendar, which payroll owns.");
    }

    /// <summary>
    /// ⚠ <b>First-year pro-rating and incremental accrual cannot both be on</b> (entitlement plan
    /// B3). Refused at both doors, because either one can be switched on second.
    /// </summary>
    /// <remarks>
    /// <para>Incremental accrual already limits a joiner to the part of the year they were present
    /// for — it opens the accrual window at their hire date. Scaling the entitlement as well deducts
    /// for the same months a second time, and because the derived per-period rate is
    /// <i>entitlement ÷ periods</i>, a third. A joiner on 1 October entitled to 12 days would end
    /// the year with <b>0.75</b> days instead of 3.</para>
    ///
    /// <para><b>Refused rather than silently ignored</b>, which is this plan's whole premise: a
    /// setting that saves and does not bind is worse than one that was never offered.</para>
    /// </remarks>
    private async Task RefuseProrationWithIncrementalAccrualAsync(
        Guid leaveTypeId, Guid tenantId, bool proRateFirstYear,
        AccrualMode? incomingMode = null, Guid? ignorePolicyId = null)
    {
        if (!proRateFirstYear) return;

        var hasIncremental = incomingMode == AccrualMode.AccrueIncrementally
            || await _accrualPolicyRepository
                .GetQueryable()
                .AnyAsync(a => a.TenantId == tenantId
                            && a.LeaveTypeId == leaveTypeId
                            && a.IsActive
                            && a.Frequency != AccrualFrequency.None
                            && a.Mode == AccrualMode.AccrueIncrementally
                            && (ignorePolicyId == null || a.Id != ignorePolicyId));

        if (hasIncremental)
            throw new InvalidOperationException(
                "First-year pro-rating cannot be combined with incremental accrual. Accrual already "
                + "limits a joiner to the part of the year they were here for, so scaling the "
                + "entitlement as well would deduct for the same months twice over. Use one or the "
                + "other: pro-rating for leave that is granted, accrual for leave that is earned.");
    }

    /// <summary>
    /// ⚠ <b>Incremental <c>Annual</c> accrual is retired</b> (round 5, lane N2), the way
    /// <c>PerPayPeriod</c> was.
    /// </summary>
    /// <remarks>
    /// One period a year, credited when the period completes, is the whole year's leave on its last
    /// day: none of it can be taken during the year it is for. A full grant on eligibility is what
    /// "once a year" means for leave that is taken as it goes. Rows already carrying it stay
    /// editable; choosing it anew is refused.
    /// </remarks>
    private static void RefuseIncrementalAnnual(AccrualFrequency frequency, AccrualMode mode)
    {
        if (frequency == AccrualFrequency.Annual && mode == AccrualMode.AccrueIncrementally)
            throw new InvalidOperationException(
                "Accruing once a year, incrementally, credits the whole year's leave on its last day, "
                + "so none of it could be taken during the year. Choose Monthly, or a full grant on "
                + "eligibility if the year's leave should be available at once.");
    }

    public async Task<LeaveAccrualPolicyDto> CreateAccrualPolicyAsync(CreateLeaveAccrualPolicyDto dto)
    {
        await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        var tenantId = GetTenantId();
        RefusePerPayPeriod(dto.Frequency);
        RefuseIncrementalAnnual(dto.Frequency, dto.Mode);
        // A policy saved switched off takes nobody's place (lane N1), so only an active one meets
        // the one-active-policy rule.
        if (dto.IsActive ?? true)
            await RefuseSecondActiveAccrualPolicyAsync(dto.LeaveTypeId, tenantId);
        var owner = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        // A switched-off policy accrues nothing, so its mode clashes with nothing.
        await RefuseProrationWithIncrementalAccrualAsync(
            dto.LeaveTypeId, tenantId, owner.ProRateFirstYearEntitlement, (dto.IsActive ?? true) ? dto.Mode : null);
        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        await _accrualPolicyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _accrualPolicyRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == entity.Id))!.ToDto();
    }

    public async Task<LeaveAccrualPolicyDto> UpdateAccrualPolicyAsync(Guid id, CreateLeaveAccrualPolicyDto dto)
    {
        var entity = await GetOwnedAccrualPolicyAsync(id);
        await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        var tenantId = entity.TenantId;

        // ⚠ Only when the edit CHANGES it: a row already carrying PerPayPeriod must stay editable,
        // or retiring the option would strand the policies that have it — which is the opposite of
        // what "the enum value stays" is for.
        if (dto.Frequency != entity.Frequency) RefusePerPayPeriod(dto.Frequency);
        // The same courtesy for incremental Annual: refused only when the edit makes it so.
        if (dto.Frequency != entity.Frequency || dto.Mode != entity.Mode)
            RefuseIncrementalAnnual(dto.Frequency, dto.Mode);

        // Null leaves the switch as it is (lane N1).
        var willBeActive = dto.IsActive ?? entity.IsActive;

        // ⚠ The payload carries a leave type, so an edit can MOVE a policy onto a type that already
        // has one. Same rule as create, excluding this row from its own check. Switching a policy
        // ON is the other way to reach two, so the rule follows the switch, not the create.
        if (willBeActive)
            await RefuseSecondActiveAccrualPolicyAsync(dto.LeaveTypeId, tenantId, id);

        var target = await GetOwnedLeaveTypeAsync(dto.LeaveTypeId);
        await RefuseProrationWithIncrementalAccrualAsync(
            dto.LeaveTypeId, tenantId, target.ProRateFirstYearEntitlement, willBeActive ? dto.Mode : null, id);

        entity.LeaveTypeId = dto.LeaveTypeId;
        entity.Frequency = dto.Frequency;
        entity.Mode = dto.Mode;
        entity.AccrualRate = dto.AccrualRate;
        entity.MinServiceMonths = dto.MinServiceMonths;
        entity.ProRateOnJoin = dto.ProRateOnJoin;
        entity.ProRateOnExit = dto.ProRateOnExit;
        entity.IsActive = willBeActive;

        await _accrualPolicyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _accrualPolicyRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id))!.ToDto();
    }

    public async Task DeleteAccrualPolicyAsync(Guid id)
    {
        var entity = await GetOwnedAccrualPolicyAsync(id);

        await _accrualPolicyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Employee Eligibility Evaluation ─────────────────────────────────────

    public async Task<bool> IsEmployeeEligibleAsync(Guid leaveTypeId, Guid employeeId)
    {
        await GetOwnedLeaveTypeAsync(leaveTypeId);
        var tenantId = GetTenantId();

        var rules = await _eligibilityRepository
            .GetQueryable()
            .Where(r => r.TenantId == tenantId && r.LeaveTypeId == leaveTypeId)
            .ToListAsync();

        // No rules configured → every employee is eligible
        if (!rules.Any())
            return true;

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
            return false;

        // Employee is eligible when they match at least one rule
        return rules.Any(rule => MatchesRule(rule, employee));
    }

    /// <summary>
    /// Evaluates a single eligibility rule against an employee.
    /// 
    /// Rules of type Gender perform a gender-only match.
    /// Rules of type OrganizationLevel / OrganizationUnit / Position match the specified
    /// scope, and — if a Gender qualifier is also set on the rule — the employee's gender
    /// must additionally match (AND logic within the rule).
    /// </summary>
    private static bool MatchesRule(LeaveTypeEligibility rule, Employee employee)
    {
        bool scopeMatches = rule.EligibilityType switch
        {
            LeaveEligibilityType.Gender =>
                // Gender-only rule: no org scope, match purely on gender
                rule.Gender.HasValue && employee.Gender == rule.Gender,

            LeaveEligibilityType.OrganizationLevel =>
                rule.OrganizationLevelId.HasValue &&
                employee.OrganizationLevelId == rule.OrganizationLevelId,

            LeaveEligibilityType.OrganizationUnit =>
                rule.OrganizationUnitId.HasValue &&
                employee.OrganizationUnitId == rule.OrganizationUnitId,

            LeaveEligibilityType.Position =>
                rule.PositionId.HasValue &&
                employee.PositionId == rule.PositionId,

            _ => false
        };

        if (!scopeMatches)
            return false;

        // For org-type rules, an optional Gender qualifier applies as an AND condition
        if (rule.EligibilityType != LeaveEligibilityType.Gender && rule.Gender.HasValue)
            return employee.Gender == rule.Gender;

        return true;
    }
}
