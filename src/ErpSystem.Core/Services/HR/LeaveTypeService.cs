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
    private readonly IGenericRepository<LeaveTypeAllowance> _leaveTypeAllowanceRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveTypeService> _logger;

    public LeaveTypeService(
        ILeaveTypeRepository leaveTypeRepository,
        IGenericRepository<LeaveSubType> leaveSubTypeRepository,
        IGenericRepository<LeaveCategoryAllocation> allocationRepository,
        IGenericRepository<LeaveTypeEligibility> eligibilityRepository,
        IGenericRepository<LeaveAccrualPolicy> accrualPolicyRepository,
        IGenericRepository<LeaveTypeAllowance> leaveTypeAllowanceRepository,
        IGenericRepository<Employee> employeeRepository,
        IUnitOfWork unitOfWork,
        ILogger<LeaveTypeService> logger)
    {
        _leaveTypeRepository = leaveTypeRepository;
        _leaveSubTypeRepository = leaveSubTypeRepository;
        _allocationRepository = allocationRepository;
        _eligibilityRepository = eligibilityRepository;
        _accrualPolicyRepository = accrualPolicyRepository;
        _leaveTypeAllowanceRepository = leaveTypeAllowanceRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ─── Leave Type ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveTypeDto>> GetAllLeaveTypesAsync(bool activeOnly = true)
    {
        var query = _leaveTypeRepository.GetQueryable();
        if (activeOnly)
            query = query.Where(lt => lt.IsActive);
        var items = await query.OrderBy(lt => lt.Name).ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveTypeDto> GetLeaveTypeByIdAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave type '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<LeaveTypeDetailDto> GetLeaveTypeDetailAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave type '{id}' not found.");

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
            RequiresReliever = entity.RequiresReliever,
            MinServiceMonthsToAccess = entity.MinServiceMonthsToAccess,
            CarryOverExpiryMonths = entity.CarryOverExpiryMonths,
            ForfeitUnusedAfterMonths = entity.ForfeitUnusedAfterMonths,
            MandatoryAnnualLeave = entity.MandatoryAnnualLeave,
            EncashmentRateBasis = entity.EncashmentRateBasis,
            EncashmentRatePerDay = entity.EncashmentRatePerDay,
            EncashmentWorkingDaysPerMonth = entity.EncashmentWorkingDaysPerMonth,
            IsActive = entity.IsActive,
            SubTypes = (await GetSubTypesAsync(id)).ToList(),
            Allocations = (await GetAllocationsAsync(id)).ToList(),
            Eligibilities = (await GetEligibilityRulesAsync(id)).ToList(),
            AccrualPolicies = (await GetAccrualPoliciesAsync(id)).ToList(),
            AllowanceComponentIds = await _leaveTypeAllowanceRepository
                .GetQueryable()
                .Where(la => la.LeaveTypeId == id)
                .Select(la => la.PayComponentId)
                .ToListAsync()
        };
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var existing = await _leaveTypeRepository.GetByCodeAsync(dto.Code);
            if (existing != null)
                throw new InvalidOperationException($"A leave type with code '{dto.Code}' already exists.");
        }

        var entity = dto.ToEntity();
        await _leaveTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await SyncAllowanceLinksAsync(entity.Id, dto.AllowanceComponentIds);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave type created: {name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<LeaveTypeDto> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeDto dto)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave type '{id}' not found.");

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code != entity.Code)
        {
            var existing = await _leaveTypeRepository.GetByCodeAsync(dto.Code);
            if (existing != null && existing.Id != id)
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
        entity.HasSubTypes = dto.HasSubTypes;
        entity.AllowCarryOver = dto.AllowCarryOver;
        entity.MaxCarryOverDays = dto.MaxCarryOverDays;
        entity.CountWeekendsAsLeave = dto.CountWeekendsAsLeave;
        entity.CountHolidaysAsLeave = dto.CountHolidaysAsLeave;
        entity.AllowCashConversion = dto.AllowCashConversion;
        entity.RequiresReliever = dto.RequiresReliever;
        entity.MinServiceMonthsToAccess = dto.MinServiceMonthsToAccess;
        entity.CarryOverExpiryMonths = dto.CarryOverExpiryMonths;
        entity.ForfeitUnusedAfterMonths = dto.ForfeitUnusedAfterMonths;
        entity.MandatoryAnnualLeave = dto.MandatoryAnnualLeave;
        entity.EncashmentRateBasis = dto.EncashmentRateBasis;
        entity.EncashmentRatePerDay = dto.EncashmentRatePerDay;
        entity.EncashmentWorkingDaysPerMonth = dto.EncashmentWorkingDaysPerMonth;
        entity.IsActive = dto.IsActive;

        await _leaveTypeRepository.UpdateAsync(entity);
        await SyncAllowanceLinksAsync(entity.Id, dto.AllowanceComponentIds);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    /// <summary>Replaces the leave type's allowance-component links with the supplied set.</summary>
    private async Task SyncAllowanceLinksAsync(Guid leaveTypeId, List<Guid> componentIds)
    {
        var existing = await _leaveTypeAllowanceRepository
            .GetQueryable()
            .Where(la => la.LeaveTypeId == leaveTypeId)
            .ToListAsync();

        var desired = (componentIds ?? new List<Guid>()).Distinct().ToList();

        foreach (var stale in existing.Where(e => !desired.Contains(e.PayComponentId)))
            await _leaveTypeAllowanceRepository.DeleteAsync(stale);

        var existingIds = existing.Select(e => e.PayComponentId).ToHashSet();
        foreach (var add in desired.Where(d => !existingIds.Contains(d)))
            await _leaveTypeAllowanceRepository.AddAsync(new LeaveTypeAllowance
            {
                LeaveTypeId = leaveTypeId,
                PayComponentId = add
            });
    }

    public async Task DeactivateLeaveTypeAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave type '{id}' not found.");

        entity.IsActive = false;
        await _leaveTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Leave type deactivated: {id}", id);
    }

    // ─── Sub Types ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveSubTypeDto>> GetSubTypesAsync(Guid leaveTypeId)
    {
        var items = await _leaveSubTypeRepository
            .GetQueryable()
            .Include(st => st.LeaveType)
            .Where(st => st.LeaveTypeId == leaveTypeId)
            .OrderBy(st => st.SubTypeName)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveSubTypeDto> CreateSubTypeAsync(CreateLeaveSubTypeDto dto)
    {
        var leaveType = await _leaveTypeRepository.GetByIdAsync(dto.LeaveTypeId);
        if (leaveType == null)
            throw new ArgumentException($"Leave type '{dto.LeaveTypeId}' not found.");
        if (!leaveType.HasSubTypes)
            throw new InvalidOperationException("This leave type is not configured to have sub-types.");

        var entity = dto.ToEntity();
        await _leaveSubTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _leaveSubTypeRepository.GetQueryable()
            .Include(st => st.LeaveType)
            .FirstOrDefaultAsync(st => st.Id == entity.Id))!.ToDto();
    }

    public async Task<LeaveSubTypeDto> UpdateSubTypeAsync(Guid id, CreateLeaveSubTypeDto dto)
    {
        var entity = await _leaveSubTypeRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave sub-type '{id}' not found.");

        entity.SubTypeName = dto.SubTypeName;
        entity.Description = dto.Description;
        entity.MaxDaysAllowed = dto.MaxDaysAllowed;
        entity.IsActive = dto.IsActive;

        await _leaveSubTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _leaveSubTypeRepository.GetQueryable()
            .Include(st => st.LeaveType)
            .FirstOrDefaultAsync(st => st.Id == id))!.ToDto();
    }

    public async Task DeleteSubTypeAsync(Guid id)
    {
        var entity = await _leaveSubTypeRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave sub-type '{id}' not found.");

        await _leaveSubTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Category Allocations ────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveCategoryAllocationDto>> GetAllocationsAsync(Guid leaveTypeId)
    {
        var items = await _allocationRepository
            .GetQueryable()
            .Include(a => a.LeaveType)
            .Include(a => a.LeaveSubType)
            .Include(a => a.StaffLevel)
            .Where(a => a.LeaveTypeId == leaveTypeId)
            .OrderBy(a => a.StaffLevelId)
            .ThenBy(a => a.EffectiveFrom)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveCategoryAllocationDto> CreateAllocationAsync(CreateLeaveCategoryAllocationDto dto)
    {
        var entity = dto.ToEntity();
        await _allocationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _allocationRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .Include(a => a.LeaveSubType)
            .Include(a => a.StaffLevel)
            .FirstOrDefaultAsync(a => a.Id == entity.Id))!.ToDto();
    }

    public async Task<LeaveCategoryAllocationDto> UpdateAllocationAsync(Guid id, CreateLeaveCategoryAllocationDto dto)
    {
        var entity = await _allocationRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave category allocation '{id}' not found.");

        entity.LeaveTypeId = dto.LeaveTypeId;
        entity.LeaveSubTypeId = dto.LeaveSubTypeId;
        entity.StaffLevelId = dto.StaffLevelId;
        entity.AllocationDays = dto.AllocationDays;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;

        await _allocationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _allocationRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .Include(a => a.LeaveSubType)
            .Include(a => a.StaffLevel)
            .FirstOrDefaultAsync(a => a.Id == id))!.ToDto();
    }

    public async Task DeleteAllocationAsync(Guid id)
    {
        var entity = await _allocationRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Leave category allocation '{id}' not found.");

        await _allocationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Eligibility Rules ───────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveTypeEligibilityDto>> GetEligibilityRulesAsync(Guid leaveTypeId)
    {
        var items = await _eligibilityRepository
            .GetQueryable()
            .Include(e => e.LeaveType)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Where(e => e.LeaveTypeId == leaveTypeId)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveTypeEligibilityDto> CreateEligibilityRuleAsync(CreateLeaveTypeEligibilityDto dto)
    {
        var entity = dto.ToEntity();
        await _eligibilityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _eligibilityRepository.GetQueryable()
            .Include(e => e.LeaveType)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == entity.Id))!.ToDto();
    }

    public async Task DeleteEligibilityRuleAsync(Guid id)
    {
        var entity = await _eligibilityRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Eligibility rule '{id}' not found.");

        await _eligibilityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Accrual Policies ────────────────────────────────────────────────────

    public async Task<IEnumerable<LeaveAccrualPolicyDto>> GetAccrualPoliciesAsync(Guid leaveTypeId)
    {
        var items = await _accrualPolicyRepository
            .GetQueryable()
            .Include(a => a.LeaveType)
            .Where(a => a.LeaveTypeId == leaveTypeId)
            .ToListAsync();
        return items.ToDtoList();
    }

    public async Task<LeaveAccrualPolicyDto> CreateAccrualPolicyAsync(CreateLeaveAccrualPolicyDto dto)
    {
        var entity = dto.ToEntity();
        await _accrualPolicyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _accrualPolicyRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .FirstOrDefaultAsync(a => a.Id == entity.Id))!.ToDto();
    }

    public async Task<LeaveAccrualPolicyDto> UpdateAccrualPolicyAsync(Guid id, CreateLeaveAccrualPolicyDto dto)
    {
        var entity = await _accrualPolicyRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Accrual policy '{id}' not found.");

        entity.LeaveTypeId = dto.LeaveTypeId;
        entity.Frequency = dto.Frequency;
        entity.Mode = dto.Mode;
        entity.AccrualRate = dto.AccrualRate;
        entity.MinServiceMonths = dto.MinServiceMonths;
        entity.ProRateOnJoin = dto.ProRateOnJoin;
        entity.ProRateOnExit = dto.ProRateOnExit;

        await _accrualPolicyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await _accrualPolicyRepository.GetQueryable()
            .Include(a => a.LeaveType)
            .FirstOrDefaultAsync(a => a.Id == id))!.ToDto();
    }

    public async Task DeleteAccrualPolicyAsync(Guid id)
    {
        var entity = await _accrualPolicyRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Accrual policy '{id}' not found.");

        await _accrualPolicyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Employee Eligibility Evaluation ─────────────────────────────────────

    public async Task<bool> IsEmployeeEligibleAsync(Guid leaveTypeId, Guid employeeId)
    {
        var rules = await _eligibilityRepository
            .GetQueryable()
            .Where(r => r.LeaveTypeId == leaveTypeId)
            .ToListAsync();

        // No rules configured → every employee is eligible
        if (!rules.Any())
            return true;

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null)
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
