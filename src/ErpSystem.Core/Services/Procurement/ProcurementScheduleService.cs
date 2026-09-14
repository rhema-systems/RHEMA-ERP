using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ProcurementScheduleService : IProcurementScheduleService
{
    private readonly IProcurementScheduleRepository _scheduleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ProcurementScheduleService> _logger;

    public ProcurementScheduleService(
        IProcurementScheduleRepository scheduleRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ProcurementScheduleService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<ProcurementScheduleDetailDto?> GetByIdAsync(Guid id)
    {
        var schedule = await _scheduleRepository.GetWithFullDetailsAsync(id);
        return schedule == null ? null : MapToDetailDto(schedule);
    }

    public async Task<ProcurementScheduleDto?> GetByScheduleCodeAsync(string scheduleCode)
    {
        var schedules = await _scheduleRepository.GetUpcomingSchedulesAsync(365);
        var schedule = schedules.FirstOrDefault(s => s.ScheduleCode == scheduleCode);
        return schedule == null ? null : MapToDto(schedule);
    }

    public async Task<PagedResult<ProcurementScheduleDto>> GetSchedulesAsync(
        int page, int pageSize, string? search = null, string? status = null,
        Guid? departmentId = null, Guid? planId = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        var result = await _scheduleRepository.GetSchedulesAsync(page, pageSize, search, status, departmentId, planId, startDate, endDate);
        return new PagedResult<ProcurementScheduleDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<ProcurementScheduleDto>> GetByPlanIdAsync(Guid planId)
    {
        var schedules = await _scheduleRepository.GetByPlanIdAsync(planId);
        return schedules.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementScheduleDto>> GetByDepartmentAsync(Guid departmentId)
    {
        var schedules = await _scheduleRepository.GetByDepartmentAsync(departmentId);
        return schedules.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementScheduleDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var schedules = await _scheduleRepository.GetByDateRangeAsync(startDate, endDate);
        return schedules.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementScheduleDto>> GetUpcomingSchedulesAsync(int daysAhead = 30)
    {
        var schedules = await _scheduleRepository.GetUpcomingSchedulesAsync(daysAhead);
        return schedules.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementScheduleDto>> GetOverdueSchedulesAsync()
    {
        var schedules = await _scheduleRepository.GetOverdueSchedulesAsync();
        return schedules.Select(MapToDto);
    }

    public async Task<IEnumerable<ProcurementScheduleDto>> GetConsolidationOpportunitiesAsync()
    {
        var schedules = await _scheduleRepository.GetConsolidationOpportunitiesAsync();
        return schedules.Select(MapToDto);
    }

    public async Task<ProcurementScheduleDetailDto> CreateAsync(CreateProcurementScheduleDto dto)
    {
        var organizationUnitId = await ResolveOrganizationUnitIdAsync(dto.ProcurementPlanId, dto.OrganizationUnitId);
        var scheduleCode = await _scheduleRepository.GenerateScheduleCodeAsync();
        var schedule = new ProcurementSchedule
        {
            ScheduleCode = scheduleCode,
            Title = dto.Title,
            Description = dto.Description,
            ProcurementPlanId = dto.ProcurementPlanId,
            ProcurementPlanItemId = dto.ProcurementPlanItemId,
            DepartmentId = null,
            OrganizationUnitId = organizationUnitId,
            ScheduleType = dto.ScheduleType,
            PlannedStartDate = dto.PlannedStartDate,
            PlannedEndDate = dto.PlannedEndDate,
            IsOptimalTiming = dto.IsOptimalTiming,
            TimingRationale = dto.TimingRationale,
            ConsiderSeasonalPricing = dto.ConsiderSeasonalPricing,
            ConsiderCashFlow = dto.ConsiderCashFlow,
            ConsolidationOpportunity = dto.ConsolidationOpportunity,
            Notes = dto.Notes,
            Status = "Planned",
            TenantId = _currentUserProvider.TenantId
        };

        await _scheduleRepository.AddAsync(schedule);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created procurement schedule {ScheduleCode}", scheduleCode);

        return await GetByIdAsync(schedule.Id) ?? throw new InvalidOperationException("Failed to retrieve created schedule");
    }

    public async Task<ProcurementScheduleDetailDto> UpdateAsync(Guid id, CreateProcurementScheduleDto dto)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null) throw new KeyNotFoundException($"Schedule with ID {id} not found");

        var organizationUnitId = await ResolveOrganizationUnitIdAsync(dto.ProcurementPlanId, dto.OrganizationUnitId);

        schedule.Title = dto.Title;
        schedule.Description = dto.Description;
        schedule.ProcurementPlanId = dto.ProcurementPlanId;
        schedule.ProcurementPlanItemId = dto.ProcurementPlanItemId;
        schedule.DepartmentId = null;
        schedule.OrganizationUnitId = organizationUnitId;
        schedule.ScheduleType = dto.ScheduleType;
        schedule.PlannedStartDate = dto.PlannedStartDate;
        schedule.PlannedEndDate = dto.PlannedEndDate;
        schedule.IsOptimalTiming = dto.IsOptimalTiming;
        schedule.TimingRationale = dto.TimingRationale;
        schedule.ConsiderSeasonalPricing = dto.ConsiderSeasonalPricing;
        schedule.ConsiderCashFlow = dto.ConsiderCashFlow;
        schedule.ConsolidationOpportunity = dto.ConsolidationOpportunity;
        schedule.Notes = dto.Notes;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _scheduleRepository.UpdateAsync(schedule);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated schedule");
    }

    public async Task<ProcurementScheduleDetailDto> StartScheduleAsync(Guid id)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null) throw new KeyNotFoundException($"Schedule with ID {id} not found");

        schedule.Status = "InProgress";
        schedule.ActualStartDate = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _scheduleRepository.UpdateAsync(schedule);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve schedule");
    }

    public async Task<ProcurementScheduleDetailDto> CompleteScheduleAsync(Guid id)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null) throw new KeyNotFoundException($"Schedule with ID {id} not found");

        schedule.Status = "Completed";
        schedule.ActualEndDate = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _scheduleRepository.UpdateAsync(schedule);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve schedule");
    }

    public async Task<ProcurementScheduleDetailDto> CancelScheduleAsync(Guid id, string reason)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null) throw new KeyNotFoundException($"Schedule with ID {id} not found");

        schedule.Status = "Cancelled";
        schedule.Notes = $"{schedule.Notes}\nCancelled on {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {reason}";
        schedule.UpdatedAt = DateTime.UtcNow;

        await _scheduleRepository.UpdateAsync(schedule);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve schedule");
    }

    public async Task<ProcurementScheduleDto> UpdateStatusAsync(Guid id, string status)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null) throw new KeyNotFoundException($"Schedule with ID {id} not found");

        schedule.Status = status;
        schedule.UpdatedAt = DateTime.UtcNow;

        if (status == "InProgress")
            schedule.ActualStartDate = DateTime.UtcNow;
        else if (status == "Completed")
            schedule.ActualEndDate = DateTime.UtcNow;

        await _scheduleRepository.UpdateAsync(schedule);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve schedule");
    }

    public async Task DeleteAsync(Guid id)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null) throw new KeyNotFoundException($"Schedule with ID {id} not found");

        schedule.IsDeleted = true;
        schedule.UpdatedAt = DateTime.UtcNow;
        await _scheduleRepository.UpdateAsync(schedule);
        await _unitOfWork.SaveChangesAsync();
    }

    #region Mapping Methods

    private static ProcurementScheduleDto MapToDto(ProcurementSchedule schedule)
    {
        return new ProcurementScheduleDto
        {
            Id = schedule.Id,
            ScheduleCode = schedule.ScheduleCode,
            Title = schedule.Title,
            Description = schedule.Description,
            ProcurementPlanId = schedule.ProcurementPlanId,
            ProcurementPlanNumber = schedule.ProcurementPlan?.PlanNumber,
            ProcurementPlanItemId = schedule.ProcurementPlanItemId,
            DepartmentId = schedule.DepartmentId,
            DepartmentName = schedule.OrganizationUnit?.Name ?? schedule.Department?.Name,
            OrganizationUnitId = schedule.OrganizationUnitId,
            OrganizationUnitName = schedule.OrganizationUnit?.Name,
            ScheduleType = schedule.ScheduleType,
            PlannedStartDate = schedule.PlannedStartDate,
            PlannedEndDate = schedule.PlannedEndDate,
            ActualStartDate = schedule.ActualStartDate,
            ActualEndDate = schedule.ActualEndDate,
            IsOptimalTiming = schedule.IsOptimalTiming,
            TimingRationale = schedule.TimingRationale,
            ConsiderSeasonalPricing = schedule.ConsiderSeasonalPricing,
            ConsiderCashFlow = schedule.ConsiderCashFlow,
            Status = schedule.Status,
            ConsolidationOpportunity = schedule.ConsolidationOpportunity,
            CreatedAt = schedule.CreatedAt
        };
    }

    private static ProcurementScheduleDetailDto MapToDetailDto(ProcurementSchedule schedule)
    {
        return new ProcurementScheduleDetailDto
        {
            Id = schedule.Id,
            ScheduleCode = schedule.ScheduleCode,
            Title = schedule.Title,
            Description = schedule.Description,
            ProcurementPlanId = schedule.ProcurementPlanId,
            ProcurementPlanNumber = schedule.ProcurementPlan?.PlanNumber,
            ProcurementPlanItemId = schedule.ProcurementPlanItemId,
            DepartmentId = schedule.DepartmentId,
            DepartmentName = schedule.OrganizationUnit?.Name ?? schedule.Department?.Name,
            OrganizationUnitId = schedule.OrganizationUnitId,
            OrganizationUnitName = schedule.OrganizationUnit?.Name,
            ScheduleType = schedule.ScheduleType,
            PlannedStartDate = schedule.PlannedStartDate,
            PlannedEndDate = schedule.PlannedEndDate,
            ActualStartDate = schedule.ActualStartDate,
            ActualEndDate = schedule.ActualEndDate,
            IsOptimalTiming = schedule.IsOptimalTiming,
            TimingRationale = schedule.TimingRationale,
            ConsiderSeasonalPricing = schedule.ConsiderSeasonalPricing,
            ConsiderCashFlow = schedule.ConsiderCashFlow,
            Status = schedule.Status,
            ConsolidationOpportunity = schedule.ConsolidationOpportunity,
            CreatedAt = schedule.CreatedAt,
            Notes = schedule.Notes
        };
    }

    private async Task<Guid?> ResolveOrganizationUnitIdAsync(
        Guid? procurementPlanId,
        Guid? requestedOrganizationUnitId)
    {
        if (procurementPlanId.HasValue && procurementPlanId.Value != Guid.Empty)
        {
            var plan = await _unitOfWork.Repository<ProcurementPlan>()
                .GetQueryable(value => value.Id == procurementPlanId.Value &&
                                       value.TenantId == _currentUserProvider.TenantId &&
                                       !value.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync();
            if (plan == null)
                throw new KeyNotFoundException("The selected procurement plan was not found in the current tenant.");
            if (!plan.OrganizationUnitId.HasValue)
                throw new InvalidOperationException("The selected procurement plan must be assigned to an HR organisation unit before a schedule can be created.");
            if (requestedOrganizationUnitId.HasValue &&
                requestedOrganizationUnitId.Value != plan.OrganizationUnitId.Value)
                throw new InvalidOperationException("The schedule organisation unit must match its procurement plan.");

            return plan.OrganizationUnitId.Value;
        }

        if (!requestedOrganizationUnitId.HasValue || requestedOrganizationUnitId.Value == Guid.Empty)
            return null;

        var active = await _unitOfWork.Repository<OrganizationUnit>()
            .GetQueryable(unit => unit.Id == requestedOrganizationUnitId.Value &&
                                  unit.TenantId == _currentUserProvider.TenantId &&
                                  unit.IsActive &&
                                  !unit.IsDeleted)
            .AnyAsync();
        if (!active)
            throw new InvalidOperationException("The selected HR organisation unit is inactive or is not in the current tenant.");

        return requestedOrganizationUnitId.Value;
    }

    #endregion
}
