using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EmolumentService> _logger;

    public EmolumentService(
        IGenericRepository<PayComponent> componentRepo,
        IGenericRepository<PositionPayComponent> positionCompRepo,
        IGenericRepository<EmployeePayComponent> employeeCompRepo,
        IGenericRepository<Employee> employeeRepo,
        IGenericRepository<EmployeeSalaryAssignment> salaryAssignmentRepo,
        IGenericRepository<LeaveType> leaveTypeRepo,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ILogger<EmolumentService> logger)
    {
        _componentRepo = componentRepo;
        _positionCompRepo = positionCompRepo;
        _employeeCompRepo = employeeCompRepo;
        _employeeRepo = employeeRepo;
        _salaryAssignmentRepo = salaryAssignmentRepo;
        _leaveTypeRepo = leaveTypeRepo;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    // ─── Pay Component master ──────────────────────────────────────────────────

    public async Task<IEnumerable<PayComponentDto>> GetPayComponentsAsync(bool activeOnly = true)
    {
        var query = _componentRepo.GetQueryable();
        if (activeOnly) query = query.Where(c => c.IsActive);
        var items = await query.OrderBy(c => c.ComponentType).ThenBy(c => c.Name).ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<PayComponentDto> GetPayComponentByIdAsync(Guid id)
    {
        var entity = await _componentRepo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Pay component '{id}' not found.");
        return ToDto(entity);
    }

    public async Task<PayComponentDto> CreatePayComponentAsync(CreatePayComponentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new InvalidOperationException("A code is required.");

        var exists = await _componentRepo.GetQueryable().AnyAsync(c => c.Code == dto.Code);
        if (exists)
            throw new InvalidOperationException($"A pay component with code '{dto.Code}' already exists.");

        var entity = new PayComponent
        {
            Code = dto.Code.Trim(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            ComponentType = dto.ComponentType,
            CalculationBasis = dto.CalculationBasis,
            DefaultAmount = dto.DefaultAmount,
            IsTaxable = dto.IsTaxable,
            EffectiveFrom = dto.EffectiveFrom == default ? _clock.UtcNow : dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = true
        };
        await _componentRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Pay component created: {code}", entity.Code);
        return ToDto(entity);
    }

    public async Task<PayComponentDto> UpdatePayComponentAsync(Guid id, UpdatePayComponentDto dto)
    {
        var entity = await _componentRepo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Pay component '{id}' not found.");

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code != entity.Code)
        {
            var clash = await _componentRepo.GetQueryable().AnyAsync(c => c.Code == dto.Code && c.Id != id);
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

    public async Task DeactivatePayComponentAsync(Guid id)
    {
        var entity = await _componentRepo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Pay component '{id}' not found.");
        entity.IsActive = false;
        await _componentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Position-level assignment ─────────────────────────────────────────────

    public async Task<IEnumerable<PositionPayComponentDto>> GetPositionComponentsAsync(Guid positionId)
    {
        var items = await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent)
            .Include(pc => pc.Position)
            .Where(pc => pc.PositionId == positionId)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<PositionPayComponentDto> AssignPositionComponentAsync(CreatePositionPayComponentDto dto)
    {
        var clash = await _positionCompRepo.GetQueryable()
            .AnyAsync(pc => pc.PositionId == dto.PositionId && pc.PayComponentId == dto.PayComponentId);
        if (clash)
            throw new InvalidOperationException("This component is already assigned to the position.");

        var entity = new PositionPayComponent
        {
            PositionId = dto.PositionId,
            PayComponentId = dto.PayComponentId,
            Amount = dto.Amount,
            IsActive = true
        };
        await _positionCompRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent).Include(pc => pc.Position)
            .FirstAsync(pc => pc.Id == entity.Id));
    }

    public async Task<PositionPayComponentDto> UpdatePositionComponentAsync(Guid id, decimal? amount, bool isActive)
    {
        var entity = await _positionCompRepo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Position pay component '{id}' not found.");
        entity.Amount = amount;
        entity.IsActive = isActive;
        await _positionCompRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent).Include(pc => pc.Position)
            .FirstAsync(pc => pc.Id == id));
    }

    public async Task RemovePositionComponentAsync(Guid id)
    {
        var entity = await _positionCompRepo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Position pay component '{id}' not found.");
        await _positionCompRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Employee-level override / addition ────────────────────────────────────

    public async Task<IEnumerable<EmployeePayComponentDto>> GetEmployeeComponentsAsync(Guid employeeId)
    {
        var items = await _employeeCompRepo.GetQueryable()
            .Include(ec => ec.PayComponent)
            .Include(ec => ec.Employee)
            .Where(ec => ec.EmployeeId == employeeId)
            .OrderByDescending(ec => ec.EffectiveFrom)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<EmployeePayComponentDto> AssignEmployeeComponentAsync(CreateEmployeePayComponentDto dto)
    {
        var entity = new EmployeePayComponent
        {
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
            .FirstAsync(ec => ec.Id == entity.Id));
    }

    public async Task<EmployeePayComponentDto> UpdateEmployeeComponentAsync(Guid id, UpdateEmployeePayComponentDto dto)
    {
        var entity = await _employeeCompRepo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Employee pay component '{id}' not found.");
        entity.PayComponentId = dto.PayComponentId;
        entity.Amount = dto.Amount;
        if (dto.EffectiveFrom != default) entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsActive = dto.IsActive;
        await _employeeCompRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(await _employeeCompRepo.GetQueryable()
            .Include(ec => ec.PayComponent).Include(ec => ec.Employee)
            .FirstAsync(ec => ec.Id == id));
    }

    public async Task RemoveEmployeeComponentAsync(Guid id)
    {
        var entity = await _employeeCompRepo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException($"Employee pay component '{id}' not found.");
        await _employeeCompRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─── Consolidated / derived ────────────────────────────────────────────────

    public async Task<EmployeeEmolumentSummaryDto> GetEmployeeEmolumentSummaryAsync(Guid employeeId, DateOnly? asOf = null)
    {
        var asOfDate = asOf ?? _clock.TodayUtc;

        var employee = await _employeeRepo.GetQueryable()
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == employeeId);
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

    public async Task<decimal> GetMonthlyBasicPayAsync(Guid employeeId, DateOnly asOf)
    {
        var asOfDt = asOf.ToDateTime(TimeOnly.MinValue);

        var assignment = await _salaryAssignmentRepo.GetQueryable()
            .Include(a => a.Notch)
            .Where(a => a.EmployeeId == employeeId
                && a.EffectiveDate <= asOfDt
                && (a.EffectiveTo == null || a.EffectiveTo >= asOfDt))
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync();

        if (assignment?.Notch != null)
            return assignment.Notch.SalaryAmount;

        var employee = await _employeeRepo.GetByIdAsync(employeeId);
        return employee?.Salary ?? 0m;
    }

    public async Task<decimal> GetEncashmentDailyRateAsync(Guid employeeId, Guid leaveTypeId, DateOnly asOf)
    {
        var leaveType = await _leaveTypeRepo.GetQueryable()
            .Include(lt => lt.LeaveTypeAllowances)
            .FirstOrDefaultAsync(lt => lt.Id == leaveTypeId);
        if (leaveType == null) throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");

        if (leaveType.EncashmentRateBasis == EncashmentRateBasis.Manual)
            return leaveType.EncashmentRatePerDay ?? 0m;

        var employee = await _employeeRepo.GetQueryable()
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        var basic = await GetMonthlyBasicPayAsync(employeeId, asOf);
        var components = await ComputeEffectiveComponentsAsync(employee, basic, asOf);

        var linkedIds = leaveType.LeaveTypeAllowances.Select(a => a.PayComponentId).ToHashSet();
        var linkedAllowances = components
            .Where(c => c.ComponentType == PayComponentType.Allowance && linkedIds.Contains(c.PayComponentId))
            .Sum(c => c.Amount);

        var divisor = leaveType.EncashmentWorkingDaysPerMonth > 0
            ? leaveType.EncashmentWorkingDaysPerMonth
            : DefaultWorkingDaysPerMonth;

        return Math.Round((basic + linkedAllowances) / divisor, 2);
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
        var byComponent = new Dictionary<Guid, EffectivePayComponentDto>();

        // Position-level defaults
        var positionComps = await _positionCompRepo.GetQueryable()
            .Include(pc => pc.PayComponent)
            .Where(pc => pc.PositionId == employee.PositionId
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
            .Where(ec => ec.EmployeeId == employee.Id
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
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo,
        IsActive = e.IsActive
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
