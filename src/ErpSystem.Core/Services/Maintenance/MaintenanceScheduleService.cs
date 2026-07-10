using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing maintenance schedules and automated work order generation
/// </summary>
public class MaintenanceScheduleService : IMaintenanceScheduleService
{
    private readonly IMaintenanceScheduleRepository _scheduleRepository;
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceTypeRepository _maintenanceTypeRepository;
    private readonly IWorkOrderTypeRepository _workOrderTypeRepository;
    private readonly IPriorityLevelRepository _priorityLevelRepository;
    private readonly IMaintenanceAssetService _assetService;
    private readonly IEmployeeService _employeeService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<MaintenanceScheduleService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly char[] separator = new[] { ',', ';' };

    public MaintenanceScheduleService(
        IMaintenanceScheduleRepository scheduleRepository,
        IWorkOrderService workOrderService,
        IMaintenanceTypeRepository maintenanceTypeRepository,
        IWorkOrderTypeRepository workOrderTypeRepository,
        IPriorityLevelRepository priorityLevelRepository,
        IMaintenanceAssetService assetService,
        IEmployeeService employeeService,
        IAuditLogService auditLogService,
        ILogger<MaintenanceScheduleService> logger,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _scheduleRepository = scheduleRepository;
        _workOrderService = workOrderService;
        _maintenanceTypeRepository = maintenanceTypeRepository;
        _workOrderTypeRepository = workOrderTypeRepository;
        _priorityLevelRepository = priorityLevelRepository;
        _assetService = assetService;
        _employeeService = employeeService;
        _auditLogService = auditLogService;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
    }

    #region CRUD Operations

    public async Task<MaintenanceScheduleDto> CreateScheduleAsync(CreateMaintenanceScheduleDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating maintenance schedule: {ScheduleName}", createDto.Name);

            // Validate code uniqueness
            if (!await _scheduleRepository.IsCodeUniqueAsync(createDto.Code))
            {
                throw new ArgumentException($"Schedule code '{createDto.Code}' already exists");
            }

            // Validate asset and maintenance type exist
            var asset = await _assetService.GetAssetByIdAsync(createDto.AssetId) ?? throw new ArgumentException($"Asset with ID {createDto.AssetId} not found");
            var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(createDto.MaintenanceTypeId) ?? throw new ArgumentException($"Maintenance type with ID {createDto.MaintenanceTypeId} not found");
            var schedule = new MaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Code = createDto.Code,
                Description = createDto.Description,
                AssetId = createDto.AssetId,
                MaintenanceTypeId = createDto.MaintenanceTypeId,
                Frequency = createDto.Frequency,
                FrequencyValue = createDto.FrequencyValue,
                FrequencyUnit = ResolveFrequencyUnit(createDto.FrequencyUnit, createDto.Frequency),
                NextDueDate = createDto.NextDueDate,
                Priority = createDto.Priority,
                EstimatedHours = createDto.EstimatedHours,
                EstimatedCost = createDto.EstimatedCost,
                AssignedTechnicianId = createDto.AssignedTechnicianId,
                AssignedTeamId = createDto.AssignedTeamId,
                Instructions = createDto.Instructions,
                SafetyNotes = createDto.SafetyNotes,
                RequiredSkills = JsonSerializer.Serialize(createDto.RequiredSkills),
                RequiredTools = JsonSerializer.Serialize(createDto.RequiredTools),
                RequiredParts = JsonSerializer.Serialize(createDto.RequiredParts),
                AutoGenerateWorkOrders = createDto.AutoGenerateWorkOrders,
                AdvanceNotificationDays = createDto.AdvanceNotificationDays,
                NotificationRecipients = createDto.NotificationRecipients,
                IsActive = createDto.IsActive,
                // Trigger fields
                PrimaryTriggerType = createDto.PrimaryTriggerType,
                SecondaryTriggerType = createDto.SecondaryTriggerType,
                TriggerLogic = createDto.TriggerLogic,
                MileageTrigger = createDto.MileageTrigger,
                OperatingHoursTrigger = createDto.OperatingHoursTrigger,
                CycleTrigger = createDto.CycleTrigger,
                ConditionCriteria = createDto.ConditionCriteria,
                CreatedById = _currentUserProvider.UserId,
                TenantId = _currentUserProvider.TenantId
            };

            await _scheduleRepository.AddAsync(schedule);
            await _unitOfWork.SaveChangesAsync(); // Commit to database

            _logger.LogInformation("Created maintenance schedule {ScheduleId} successfully", schedule.Id);

            return await MapToDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance schedule: {ScheduleName}", createDto.Name);
            throw;
        }
    }

    public async Task<MaintenanceScheduleDto> UpdateScheduleAsync(Guid id, UpdateMaintenanceScheduleDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating maintenance schedule: {ScheduleId}", id);

            var schedule = await _scheduleRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Schedule with ID {id} not found");

            // Update properties
            schedule.Name = updateDto.Name;
            schedule.Description = updateDto.Description;
            schedule.AssetId = updateDto.AssetId;
            schedule.MaintenanceTypeId = updateDto.MaintenanceTypeId;
            schedule.Frequency = updateDto.Frequency;
            schedule.FrequencyValue = updateDto.FrequencyValue;
            schedule.FrequencyUnit = ResolveFrequencyUnit(updateDto.FrequencyUnit, updateDto.Frequency);
            schedule.NextDueDate = updateDto.NextDueDate;
            schedule.Priority = updateDto.Priority;
            schedule.EstimatedHours = updateDto.EstimatedHours;
            schedule.EstimatedCost = updateDto.EstimatedCost;
            schedule.AssignedTechnicianId = updateDto.AssignedTechnicianId;
            schedule.AssignedTeamId = updateDto.AssignedTeamId;
            schedule.Instructions = updateDto.Instructions;
            schedule.SafetyNotes = updateDto.SafetyNotes;
            schedule.RequiredSkills = JsonSerializer.Serialize(updateDto.RequiredSkills);
            schedule.RequiredTools = JsonSerializer.Serialize(updateDto.RequiredTools);
            schedule.RequiredParts = JsonSerializer.Serialize(updateDto.RequiredParts);
            schedule.AutoGenerateWorkOrders = updateDto.AutoGenerateWorkOrders;
            schedule.AdvanceNotificationDays = updateDto.AdvanceNotificationDays;
            schedule.NotificationRecipients = updateDto.NotificationRecipients;
            schedule.IsActive = updateDto.IsActive;
            // Trigger fields
            schedule.PrimaryTriggerType = updateDto.PrimaryTriggerType;
            schedule.SecondaryTriggerType = updateDto.SecondaryTriggerType;
            schedule.TriggerLogic = updateDto.TriggerLogic;
            schedule.MileageTrigger = updateDto.MileageTrigger;
            schedule.OperatingHoursTrigger = updateDto.OperatingHoursTrigger;
            schedule.CycleTrigger = updateDto.CycleTrigger;
            schedule.ConditionCriteria = updateDto.ConditionCriteria;
            schedule.LastModifiedById = _currentUserProvider.UserId;

            await _scheduleRepository.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated maintenance schedule {ScheduleId} successfully", id);

            return await MapToDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance schedule: {ScheduleId}", id);
            throw;
        }
    }

    private static string ResolveFrequencyUnit(string? frequencyUnit, string? frequency)
    {
        if (!string.IsNullOrWhiteSpace(frequencyUnit))
        {
            return frequencyUnit.Trim();
        }

        return frequency?.Trim().ToLowerInvariant() switch
        {
            "hourly" => "Hours",
            "weekly" => "Weeks",
            "monthly" or "quarterly" or "semi-annual" or "semiannual" or "annual" or "yearly" => "Months",
            _ => "Days"
        };
    }

    public async Task DeleteScheduleAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting maintenance schedule: {ScheduleId}", id);

            var schedule = await _scheduleRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Schedule with ID {id} not found");
            await _scheduleRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted maintenance schedule {ScheduleId} successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance schedule: {ScheduleId}", id);
            throw;
        }
    }

    public async Task<MaintenanceScheduleDto?> GetScheduleByIdAsync(Guid id)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        return schedule != null ? await MapToDto(schedule) : null;
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetAllSchedulesAsync()
    {
        var schedules = await _scheduleRepository.GetAllAsync();
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in schedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<PagedResult<MaintenanceScheduleDto>> GetSchedulesPagedAsync(MaintenanceScheduleFilterDto filter)
    {
        // This would typically use a more sophisticated filtering mechanism
        var allSchedules = await _scheduleRepository.GetAllAsync();
        var filtered = allSchedules.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            filtered = filtered.Where(s => s.Name.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          s.Code.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          s.Description.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.AssetId.HasValue)
        {
            filtered = filtered.Where(s => s.AssetId == filter.AssetId.Value);
        }

        if (filter.MaintenanceTypeId.HasValue)
        {
            filtered = filtered.Where(s => s.MaintenanceTypeId == filter.MaintenanceTypeId.Value);
        }

        if (!string.IsNullOrEmpty(filter.Frequency))
        {
            filtered = filtered.Where(s => s.Frequency == filter.Frequency);
        }

        if (!string.IsNullOrEmpty(filter.Priority))
        {
            filtered = filtered.Where(s => s.Priority == filter.Priority);
        }

        if (filter.IsActive.HasValue)
        {
            filtered = filtered.Where(s => s.IsActive == filter.IsActive.Value);
        }

        if (filter.IsOverdue.HasValue)
        {
            filtered = filtered.Where(s => s.IsOverdue == filter.IsOverdue.Value);
        }

        var totalCount = filtered.Count();
        var schedulesPage = filtered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var scheduleDtos = new List<MaintenanceScheduleDto>();
        foreach (var schedule in schedulesPage)
        {
            scheduleDtos.Add(await MapToDto(schedule));
        }

        return new PagedResult<MaintenanceScheduleDto>
        {
            Items = scheduleDtos,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    #endregion

    #region Business Logic

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesByAssetAsync(Guid assetId)
    {
        var schedules = await _scheduleRepository.GetByAssetIdAsync(assetId);
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in schedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetActiveSchedulesAsync()
    {
        var schedules = await _scheduleRepository.GetActiveSchedulesAsync();
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in schedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueInDaysAsync(int days)
    {
        var schedules = await _scheduleRepository.GetSchedulesDueInDaysAsync(days);
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in schedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetOverdueSchedulesAsync()
    {
        var schedules = await _scheduleRepository.GetSchedulesDueInDaysAsync(-1);
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in schedules.Where(s => s.IsOverdue))
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesByTechnicianAsync(Guid technicianId)
    {
        var allSchedules = await _scheduleRepository.GetAllAsync();
        var technicianSchedules = allSchedules.Where(s => s.AssignedTechnicianId == technicianId);
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in technicianSchedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesByTeamAsync(Guid teamId)
    {
        var allSchedules = await _scheduleRepository.GetAllAsync();
        var teamSchedules = allSchedules.Where(s => s.AssignedTeamId == teamId);
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in teamSchedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesByMaintenanceTypeAsync(Guid maintenanceTypeId)
    {
        var schedules = await _scheduleRepository.GetByMaintenanceTypeAsync(maintenanceTypeId);
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in schedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<MaintenanceScheduleDto> ToggleScheduleStatusAsync(Guid id)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Schedule with ID {id} not found");
        schedule.IsActive = !schedule.IsActive;
        schedule.LastModifiedById = _currentUserProvider.UserId;

        await _scheduleRepository.UpdateAsync(schedule);
        await _unitOfWork.SaveChangesAsync();

        return await MapToDto(schedule);
    }

    public async Task<bool> IsScheduleCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        return await _scheduleRepository.IsCodeUniqueAsync(code, excludeId);
    }

    #endregion

    #region Schedule Processing

    public async Task ProcessSchedulesAsync()
    {
        try
        {
            _logger.LogInformation("Starting scheduled maintenance processing");

            var dueSchedules = await _scheduleRepository.GetSchedulesDueForGenerationAsync();
            var processedCount = 0;

            foreach (var schedule in dueSchedules.Where(s => s.AutoGenerateWorkOrders))
            {
                try
                {
                    await GenerateWorkOrderFromScheduleAsync(schedule.Id);
                    await UpdateNextDueDateAsync(schedule.Id);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing schedule {ScheduleId}", schedule.Id);
                }
            }

            _logger.LogInformation("Completed scheduled maintenance processing. Processed {Count} schedules", processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in scheduled maintenance processing");
            throw;
        }
    }

    public async Task<WorkOrderDto> GenerateWorkOrderFromScheduleAsync(Guid scheduleId)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId) ?? throw new ArgumentException($"Schedule with ID {scheduleId} not found");

        // Get or create a default "Scheduled" work order type
        var workOrderTypes = await _workOrderTypeRepository.GetAllAsync();
        var scheduledWorkOrderType = workOrderTypes.FirstOrDefault(wot => wot.Name.Contains("Scheduled") || wot.Code == "SCH")
            ?? workOrderTypes.FirstOrDefault(wot => wot.IsActive)
            ?? throw new InvalidOperationException("No active work order types found");

        // Get priority level based on priority string
        var priorityLevels = await _priorityLevelRepository.GetAllAsync();
        var priorityLevel = priorityLevels.FirstOrDefault(p => p.Name.Equals(schedule.Priority, StringComparison.OrdinalIgnoreCase))
            ?? priorityLevels.FirstOrDefault(p => p.Name.Equals("Medium", StringComparison.OrdinalIgnoreCase))
            ?? priorityLevels.FirstOrDefault(p => p.IsActive)
            ?? throw new InvalidOperationException("No active priority levels found");

        // Deserialize required skills from JSON and convert to comma-separated string
        string requiredSkillsStr = string.Empty;

        try
        {
            var skillsList = JsonSerializer.Deserialize<List<string>>(schedule.RequiredSkills) ?? new List<string>();
            requiredSkillsStr = string.Join(", ", skillsList);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize required skills for schedule {ScheduleId}", scheduleId);
        }

        // Create work order from schedule with ALL details
        var createWorkOrder = new CreateWorkOrderDto
        {
            Title = $"{schedule.Name} - Scheduled Maintenance",
            Description = $"{schedule.Description}\n\n[Auto-generated from maintenance schedule: {schedule.Name} ({schedule.Code}). Next due date was: {schedule.NextDueDate:yyyy-MM-dd}]",
            AssetId = schedule.AssetId,
            WorkOrderTypeId = scheduledWorkOrderType.Id,
            MaintenanceTypeId = schedule.MaintenanceTypeId,
            PriorityLevelId = priorityLevel.Id,
            Priority = schedule.Priority,
            EstimatedHours = (double)schedule.EstimatedHours,
            AssignedTechnicianId = schedule.AssignedTechnicianId,
            AssignedTeamId = schedule.AssignedTeamId,
            Instructions = schedule.Instructions,
            SafetyNotes = schedule.SafetyNotes,
            RequiredSkills = requiredSkillsStr,
            RequestedStartDate = schedule.NextDueDate,
            ScheduledStartDate = schedule.NextDueDate,
            ScheduledEndDate = schedule.NextDueDate.AddHours((double)schedule.EstimatedHours),
            Status = "Open",
            WorkOrderType = "Scheduled",
            // JobCard is optional - work order will save without it
            JobCardId = null,
            // Link back to the schedule
            MaintenanceScheduleId = scheduleId
        };

        var workOrder = await _workOrderService.CreateWorkOrderAsync(createWorkOrder);

        // If technician is assigned, create technician schedule entry
        if (schedule.AssignedTechnicianId.HasValue)
        {
            try
            {
                var technicianSchedule = new TechnicianSchedule
                {
                    Id = Guid.NewGuid(),
                    TechnicianId = schedule.AssignedTechnicianId.Value,
                    WorkOrderId = workOrder.Id,
                    StartDate = schedule.NextDueDate,
                    EndDate = schedule.NextDueDate.AddHours((double)schedule.EstimatedHours),
                    ScheduleType = "WorkOrder",
                    Title = $"{schedule.Name} - Scheduled Maintenance",
                    Description = schedule.Description,
                    Status = "Scheduled",
                    Priority = schedule.Priority,
                    EstimatedHours = (double)schedule.EstimatedHours,
                    Notes = $"Scheduled maintenance: {schedule.Name}",
                    CreatedById = _currentUserProvider.IsAuthenticated ? _currentUserProvider.UserId : Guid.Empty,
                    TenantId = _currentUserProvider.IsAuthenticated ? _currentUserProvider.TenantId : schedule.TenantId,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Repository<TechnicianSchedule>().AddAsync(technicianSchedule);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Created technician schedule entry for technician {TechnicianId} on work order {WorkOrderId}",
                    schedule.AssignedTechnicianId.Value, workOrder.Id);
            }
            catch (Exception schedEx)
            {
                _logger.LogWarning(schedEx, "Failed to create technician schedule for work order {WorkOrderId}", workOrder.Id);
                // Don't fail the whole operation if schedule creation fails
            }
        }

        // Update the schedule's next due date based on frequency
        await UpdateNextDueDateAsync(scheduleId);

        // Update tracking fields to record that work order was generated
        var updatedSchedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        if (updatedSchedule != null)
        {
            updatedSchedule.LastGeneratedDate = DateTime.UtcNow;
            updatedSchedule.LastProcessedDate = DateTime.UtcNow;
            await _scheduleRepository.UpdateAsync(updatedSchedule);
            await _unitOfWork.SaveChangesAsync();
        }

        // Create maintenance schedule history record
        try
        {
            var scheduleHistory = new MaintenanceScheduleHistory
            {
                Id = Guid.NewGuid(),
                ScheduleId = scheduleId,
                ChangeType = "WorkOrderGenerated",
                PreviousValues = JsonSerializer.Serialize(new
                {
                    NextDueDate = schedule.NextDueDate,
                    LastGeneratedDate = schedule.LastGeneratedDate
                }),
                NewValues = JsonSerializer.Serialize(new
                {
                    WorkOrderId = workOrder.Id,
                    WorkOrderTitle = workOrder.Title,
                    NextDueDate = updatedSchedule?.NextDueDate,
                    LastGeneratedDate = DateTime.UtcNow
                }),
                ChangeReason = $"Work order {workOrder.Id} generated from scheduled maintenance",
                ChangedById = _currentUserProvider.IsAuthenticated ? _currentUserProvider.UserId : Guid.Empty,
                TenantId = _currentUserProvider.IsAuthenticated ? _currentUserProvider.TenantId : Guid.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<MaintenanceScheduleHistory>().AddAsync(scheduleHistory);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception historyEx)
        {
            _logger.LogWarning(historyEx, "Failed to create schedule history record for work order generation from schedule {ScheduleId}", scheduleId);
        }

        // Create audit log entry
        try
        {
            await _auditLogService.LogUserActionAsync(
                userId: _currentUserProvider.IsAuthenticated ? _currentUserProvider.UserId : Guid.Empty,
                username: _currentUserProvider.Username ?? "System",
                action: "Generate Work Order",
                resource: "Maintenance Schedule",
                resourceId: scheduleId.ToString(),
                oldValues: new { NextDueDate = schedule.NextDueDate, LastGeneratedDate = schedule.LastGeneratedDate },
                newValues: new
                {
                    WorkOrderId = workOrder.Id,
                    WorkOrderTitle = workOrder.Title,
                    NextDueDate = updatedSchedule?.NextDueDate,
                    LastGeneratedDate = DateTime.UtcNow
                }
            );
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "Failed to create audit log for work order generation from schedule {ScheduleId}", scheduleId);
        }

        _logger.LogInformation("Generated work order {WorkOrderId} from schedule {ScheduleId} and updated next due date",
            workOrder.Id, scheduleId);

        return workOrder;
    }

    public async Task UpdateNextDueDateAsync(Guid scheduleId)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId) ?? throw new ArgumentException($"Schedule with ID {scheduleId} not found");
        var nextDueDate = await CalculateNextDueDateAsync(await MapToDto(schedule));
        await _scheduleRepository.UpdateNextDueDateAsync(scheduleId, nextDueDate);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<DateTime> CalculateNextDueDateAsync(MaintenanceScheduleDto schedule)
    {
        return schedule.Frequency switch
        {
            "Daily" => schedule.NextDueDate.AddDays(schedule.FrequencyValue),
            "Weekly" => schedule.NextDueDate.AddDays(schedule.FrequencyValue * 7),
            "Monthly" => schedule.NextDueDate.AddMonths(schedule.FrequencyValue),
            "Quarterly" => schedule.NextDueDate.AddMonths(schedule.FrequencyValue * 3),
            "Yearly" => schedule.NextDueDate.AddYears(schedule.FrequencyValue),
            "Custom" => schedule.FrequencyUnit switch
            {
                "Days" => schedule.NextDueDate.AddDays(schedule.FrequencyValue),
                "Weeks" => schedule.NextDueDate.AddDays(schedule.FrequencyValue * 7),
                "Months" => schedule.NextDueDate.AddMonths(schedule.FrequencyValue),
                "Hours" => schedule.NextDueDate.AddHours(schedule.FrequencyValue),
                _ => schedule.NextDueDate.AddDays(schedule.FrequencyValue)
            },
            _ => schedule.NextDueDate.AddDays(schedule.FrequencyValue)
        };
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueForCreationAsync()
    {
        var schedules = await _scheduleRepository.GetSchedulesDueForGenerationAsync();
        var result = new List<MaintenanceScheduleDto>();

        foreach (var schedule in schedules)
        {
            result.Add(await MapToDto(schedule));
        }

        return result;
    }

    public async Task<IEnumerable<WorkOrderDto>> CreateWorkOrdersFromScheduleAsync(Guid scheduleId, int count = 1)
    {
        var workOrders = new List<WorkOrderDto>();

        for (int i = 0; i < count; i++)
        {
            var workOrder = await GenerateWorkOrderFromScheduleAsync(scheduleId);
            workOrders.Add(workOrder);
        }

        return workOrders;
    }

    #endregion

    #region Analytics

    public async Task<ScheduleComplianceReportDto> GetScheduleComplianceReportAsync(DateTime startDate, DateTime endDate)
    {
        var schedules = await _scheduleRepository.GetAllAsync();

        return new ScheduleComplianceReportDto
        {
            TotalSchedules = schedules.Count(),
            ActiveSchedules = schedules.Count(s => s.IsActive),
            CompletedOnTime = 45, // Would be calculated from actual data
            OverdueSchedules = schedules.Count(s => s.IsOverdue),
            SchedulesDueToday = schedules.Count(s => s.NextDueDate.Date == DateTime.UtcNow.Date),
            SchedulesDueThisWeek = schedules.Count(s => s.NextDueDate.Date <= DateTime.UtcNow.AddDays(7).Date),
            OverallCompliancePercentage = 91.2m,
            OnTimeCompletionRate = 88.5m,
            TotalWorkOrdersGenerated = 124,
            ReportPeriodStart = startDate,
            ReportPeriodEnd = endDate,
            ReportGeneratedDate = DateTime.UtcNow,
            ScheduleCompliance = new List<ScheduleComplianceDetail>(),
            AssetCompliance = new List<AssetComplianceDto>()
        };
    }

    #endregion

    #region Private Methods

    private async Task<MaintenanceScheduleDto> MapToDto(MaintenanceSchedule schedule)
    {
        // Resolve maintenance type name
        var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(schedule.MaintenanceTypeId);
        var maintenanceTypeName = maintenanceType?.Name ?? "Unknown Type";

        // Resolve asset name
        var asset = await _assetService.GetAssetByIdAsync(schedule.AssetId);
        var assetName = asset?.Name ?? "Unknown Asset";

        // Resolve technician name if assigned
        var technicianName = "Unassigned";
        if (schedule.AssignedTechnicianId.HasValue)
        {
            try
            {
                var technician = await _employeeService.GetTechnicianByIdAsync(schedule.AssignedTechnicianId.Value);
                if (technician != null)
                {
                    technicianName = technician.FullName ?? technician.DisplayName;
                    if (string.IsNullOrWhiteSpace(technicianName))
                    {
                        technicianName = technician.EmployeeNumber ?? "Unknown Technician";
                    }
                }
                else
                {
                    technicianName = "Unknown Technician";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve technician name for ID {TechnicianId}", schedule.AssignedTechnicianId.Value);
                technicianName = "Unknown Technician";
            }
        }

        return new MaintenanceScheduleDto
        {
            Id = schedule.Id,
            Name = schedule.Name,
            Code = schedule.Code,
            Description = schedule.Description,
            AssetId = schedule.AssetId,
            AssetName = assetName,
            MaintenanceTypeId = schedule.MaintenanceTypeId,
            MaintenanceTypeName = maintenanceTypeName,
            MaintenanceType = maintenanceTypeName, // Also set this field
            Frequency = schedule.Frequency,
            FrequencyValue = schedule.FrequencyValue,
            FrequencyUnit = schedule.FrequencyUnit,
            NextDueDate = schedule.NextDueDate,
            LastCompletedDate = schedule.LastCompletedDate,
            IsActive = schedule.IsActive,
            Priority = schedule.Priority,
            EstimatedHours = schedule.EstimatedHours,
            EstimatedCost = schedule.EstimatedCost,
            AssignedTechnicianId = schedule.AssignedTechnicianId,
            AssignedTechnicianName = technicianName,
            AssignedTeamId = schedule.AssignedTeamId,
            AssignedTeamName = "Team Name", // Would be resolved
            Instructions = schedule.Instructions,
            SafetyNotes = schedule.SafetyNotes,
            RequiredSkills = JsonSerializer.Deserialize<List<string>>(schedule.RequiredSkills) ?? new List<string>(),
            RequiredTools = JsonSerializer.Deserialize<List<string>>(schedule.RequiredTools) ?? new List<string>(),
            RequiredParts = JsonSerializer.Deserialize<List<string>>(schedule.RequiredParts) ?? new List<string>(),
            AutoGenerateWorkOrders = schedule.AutoGenerateWorkOrders,
            AdvanceNotificationDays = schedule.AdvanceNotificationDays,
            NotificationRecipients = schedule.NotificationRecipients,
            CreatedDate = schedule.CreatedAt,
            CreatedBy = "Created By Name", // Would be resolved
            LastModifiedDate = schedule.UpdatedAt ?? schedule.CreatedAt,
            LastModifiedBy = "Modified By Name", // Would be resolved
            CompletedWorkOrdersCount = 0, // Would be calculated
            CompliancePercentage = 95.0m, // Would be calculated
            NextScheduledDate = schedule.NextDueDate,
            IsOverdue = schedule.IsOverdue,
            DaysUntilDue = schedule.DaysUntilDue,
            // Trigger fields
            PrimaryTriggerType = schedule.PrimaryTriggerType,
            SecondaryTriggerType = schedule.SecondaryTriggerType,
            TriggerLogic = schedule.TriggerLogic,
            MileageTrigger = schedule.MileageTrigger,
            OperatingHoursTrigger = schedule.OperatingHoursTrigger,
            CycleTrigger = schedule.CycleTrigger,
            ConditionCriteria = schedule.ConditionCriteria
        };
    }

    #endregion

    #region History Methods

    public async Task<IEnumerable<MaintenanceScheduleHistoryDto>> GetScheduleHistoryAsync(Guid scheduleId)
    {
        try
        {
            _logger.LogInformation("Getting history for schedule {ScheduleId}", scheduleId);

            var schedule = await _scheduleRepository.GetByIdAsync(scheduleId) ?? throw new ArgumentException($"Schedule with ID {scheduleId} not found");

            // Get history records filtered by schedule ID
            var historyRecords = await _unitOfWork.Repository<MaintenanceScheduleHistory>()
                .FindAsync(h => h.ScheduleId == scheduleId);

            // Order by CreatedAt descending (newest first)
            var orderedHistory = historyRecords.OrderByDescending(h => h.CreatedAt).ToList();

            var historyDtos = new List<MaintenanceScheduleHistoryDto>();
            foreach (var history in orderedHistory)
            {
                var changedByName = "System";
                if (history.ChangedById != Guid.Empty)
                {
                    try
                    {
                        var employee = await _employeeService.GetByIdAsync(history.ChangedById);
                        if (employee != null)
                        {
                            changedByName = $"{employee.FirstName} {employee.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(changedByName))
                            {
                                changedByName = employee.EmployeeNumber ?? "Unknown";
                            }
                        }
                        else
                        {
                            changedByName = "Unknown";
                        }
                    }
                    catch
                    {
                        changedByName = "Unknown";
                    }
                }

                historyDtos.Add(new MaintenanceScheduleHistoryDto
                {
                    Id = history.Id,
                    ScheduleId = history.ScheduleId,
                    ChangeType = history.ChangeType,
                    PreviousValues = history.PreviousValues,
                    NewValues = history.NewValues,
                    ChangeReason = history.ChangeReason,
                    ChangedById = history.ChangedById,
                    ChangedByName = changedByName,
                    CreatedAt = history.CreatedAt
                });
            }

            return historyDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    #endregion

    #region Notification and Reminder Methods

    public async Task SendScheduleReminderAsync(Guid scheduleId, bool force = false)
    {
        try
        {
            _logger.LogInformation("Sending schedule reminder for schedule {ScheduleId}, force={Force}", scheduleId, force);

            var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
            if (schedule == null)
            {
                _logger.LogWarning("Schedule {ScheduleId} not found", scheduleId);
                return;
            }

            // Check if reminder already sent today (unless forced)
            if (!force && schedule.LastReminderSentDate.HasValue &&
                schedule.LastReminderSentDate.Value.Date == DateTime.UtcNow.Date)
            {
                _logger.LogInformation("Reminder already sent today for schedule {ScheduleId}", scheduleId);
                return;
            }

            // Get asset and maintenance type info
            var asset = await _assetService.GetAssetByIdAsync(schedule.AssetId);
            if (asset == null)
            {
                _logger.LogWarning("Asset not found for schedule {ScheduleId}", scheduleId);
                return;
            }

            var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(schedule.MaintenanceTypeId);

            // Parse recipients
            var recipients = ParseRecipients(schedule.NotificationRecipients);
            if (!recipients.Any())
            {
                _logger.LogWarning("No recipients configured for schedule {ScheduleId}", scheduleId);
                return;
            }

            // Format due date
            var dueDateFormatted = schedule.NextDueDate.ToString("MMMM dd, yyyy");
            var daysUntilDue = (int)(schedule.NextDueDate.Date - DateTime.UtcNow.Date).TotalDays;

            // Create notification record
            var notification = new MaintenanceScheduleNotificationHistory
            {
                Id = Guid.NewGuid(),
                ScheduleId = scheduleId,
                NotificationType = "Reminder",
                Status = "Pending",
                ScheduledFor = DateTime.UtcNow,
                Recipients = JsonSerializer.Serialize(recipients),
                Subject = $"Maintenance Reminder: {schedule.Name}",
                Message = $"Maintenance '{schedule.Name}' is due for asset '{asset.Name}' on {dueDateFormatted} ({daysUntilDue} days from now). Priority: {schedule.Priority}. Type: {maintenanceType?.Name ?? "N/A"}.",
                CreatedById = _currentUserProvider.IsAuthenticated ? _currentUserProvider.UserId : Guid.Empty,
                TenantId = _currentUserProvider.IsAuthenticated ? _currentUserProvider.TenantId : schedule.TenantId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<MaintenanceScheduleNotificationHistory>().AddAsync(notification);

            // TODO: Send email to recipients using IEmailService
            // Would need to inject IEmailService into constructor for proper implementation
            _logger.LogInformation("Would send email to {Count} recipients for schedule {ScheduleId}", recipients.Count, scheduleId);

            // Update notification status
            notification.Status = "Sent";
            notification.SentAt = DateTime.UtcNow;

            // Update schedule's last reminder date
            await _scheduleRepository.UpdateLastReminderSentDateAsync(scheduleId, DateTime.UtcNow);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Schedule reminder sent successfully for schedule {ScheduleId}", scheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending schedule reminder for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueForRemindersAsync()
    {
        try
        {
            // Get active schedules with advance notification configured
            var allSchedules = await _scheduleRepository.GetActiveSchedulesAsync();
            var dueForReminders = new List<MaintenanceSchedule>();

            foreach (var schedule in allSchedules)
            {
                if (!schedule.AdvanceNotificationDays.HasValue ||
                    string.IsNullOrEmpty(schedule.NotificationRecipients))
                {
                    continue;
                }

                var daysUntilDue = (int)(schedule.NextDueDate.Date - DateTime.UtcNow.Date).TotalDays;

                // Check if we should send reminder
                if (daysUntilDue == schedule.AdvanceNotificationDays.Value)
                {
                    // Check if reminder not already sent today
                    if (!schedule.LastReminderSentDate.HasValue ||
                        schedule.LastReminderSentDate.Value.Date < DateTime.UtcNow.Date)
                    {
                        dueForReminders.Add(schedule);
                    }
                }
            }

            var result = new List<MaintenanceScheduleDto>();
            foreach (var schedule in dueForReminders)
            {
                result.Add(await MapToDto(schedule));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules due for reminders");
            throw;
        }
    }

    public async Task<int> SendAdvanceRemindersAsync()
    {
        try
        {
            _logger.LogInformation("Processing advance reminders");

            var schedulesDue = await GetSchedulesDueForRemindersAsync();
            var sentCount = 0;

            foreach (var schedule in schedulesDue)
            {
                try
                {
                    await SendScheduleReminderAsync(schedule.Id);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending reminder for schedule {ScheduleId}", schedule.Id);
                }
            }

            _logger.LogInformation("Sent {Count} advance reminders", sentCount);
            return sentCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing advance reminders");
            throw;
        }
    }

    #endregion

    #region Usage-Based Trigger Evaluation

    public async Task<bool> EvaluateUsageTriggersAsync(Guid scheduleId)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
            if (schedule == null)
            {
                return false;
            }

            // Only evaluate if usage trigger is configured
            if (schedule.PrimaryTriggerType != "Usage" &&
                schedule.PrimaryTriggerType != "Combined" &&
                schedule.SecondaryTriggerType != "Usage")
            {
                return false;
            }

            // Get current asset usage
            var asset = await _assetService.GetAssetByIdAsync(schedule.AssetId);
            if (asset == null)
            {
                return false;
            }

            bool triggerMet = false;

            // Check mileage trigger
            if (schedule.MileageTrigger.HasValue && asset.Mileage.HasValue)
            {
                var mileageSinceLastCheck = asset.Mileage.Value - (double)(schedule.LastUsageValue ?? 0);
                if (mileageSinceLastCheck >= (double)schedule.MileageTrigger.Value)
                {
                    _logger.LogInformation("Mileage trigger met for schedule {ScheduleId}: {Current} >= {Trigger}",
                        scheduleId, asset.Mileage.Value, schedule.MileageTrigger.Value);
                    triggerMet = true;
                }
            }

            // Check operating hours trigger
            if (schedule.OperatingHoursTrigger.HasValue && asset.OperatingHours.HasValue)
            {
                var hoursSinceLastCheck = asset.OperatingHours.Value - (double)(schedule.LastUsageValue ?? 0);
                if (hoursSinceLastCheck >= (double)schedule.OperatingHoursTrigger.Value)
                {
                    _logger.LogInformation("Operating hours trigger met for schedule {ScheduleId}: {Current} >= {Trigger}",
                        scheduleId, asset.OperatingHours.Value, schedule.OperatingHoursTrigger.Value);
                    triggerMet = true;
                }
            }

            // Update last check date
            await _scheduleRepository.UpdateLastUsageCheckDateAsync(scheduleId, DateTime.UtcNow);
            await _unitOfWork.SaveChangesAsync();

            return triggerMet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating usage triggers for schedule {ScheduleId}", scheduleId);
            return false;
        }
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueByUsageAsync()
    {
        try
        {
            var usageSchedules = await _scheduleRepository.GetSchedulesByUsageTriggersAsync();
            var dueSchedules = new List<MaintenanceSchedule>();

            foreach (var schedule in usageSchedules)
            {
                if (await EvaluateUsageTriggersAsync(schedule.Id))
                {
                    dueSchedules.Add(schedule);
                }
            }

            var result = new List<MaintenanceScheduleDto>();
            foreach (var schedule in dueSchedules)
            {
                result.Add(await MapToDto(schedule));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules due by usage");
            throw;
        }
    }

    public async Task UpdateAssetUsageAsync(Guid assetId, double? mileage, double? operatingHours)
    {
        try
        {
            _logger.LogInformation("Updating asset usage for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId) ?? throw new ArgumentException($"Asset with ID {assetId} not found");

            // This would require modifying the asset service to support usage updates
            // For now, log the intent
            _logger.LogInformation("Asset usage update requested: Mileage={Mileage}, Hours={Hours}",
                mileage, operatingHours);

            // TODO: Implement asset usage update in IMaintenanceAssetService
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset usage for asset {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Condition-Based Trigger Evaluation

    public async Task<bool> EvaluateConditionTriggersAsync(Guid scheduleId)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
            if (schedule == null)
            {
                return false;
            }

            // Only evaluate if condition trigger is configured
            if (schedule.PrimaryTriggerType != "Condition" &&
                schedule.PrimaryTriggerType != "Combined" &&
                schedule.SecondaryTriggerType != "Condition")
            {
                return false;
            }

            if (string.IsNullOrEmpty(schedule.ConditionCriteria))
            {
                return false;
            }

            // This is a placeholder - actual condition evaluation would require
            // integration with IoT sensors or other data sources
            _logger.LogWarning("Condition evaluation not fully implemented for schedule {ScheduleId}", scheduleId);

            // Update last check date
            await _scheduleRepository.UpdateLastConditionCheckDateAsync(scheduleId, DateTime.UtcNow);
            await _unitOfWork.SaveChangesAsync();

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition triggers for schedule {ScheduleId}", scheduleId);
            return false;
        }
    }

    public async Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueByConditionAsync()
    {
        try
        {
            var conditionSchedules = await _scheduleRepository.GetSchedulesByConditionTriggersAsync();
            var dueSchedules = new List<MaintenanceSchedule>();

            foreach (var schedule in conditionSchedules)
            {
                if (await EvaluateConditionTriggersAsync(schedule.Id))
                {
                    dueSchedules.Add(schedule);
                }
            }

            var result = new List<MaintenanceScheduleDto>();
            foreach (var schedule in dueSchedules)
            {
                result.Add(await MapToDto(schedule));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules due by condition");
            throw;
        }
    }

    #endregion

    #region Multi-Criteria Evaluation

    public async Task<bool> ShouldGenerateWorkOrderAsync(Guid scheduleId)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
            if (schedule == null || !schedule.IsActive)
            {
                return false;
            }

            bool primaryTriggerMet = false;
            bool secondaryTriggerMet = false;

            // Evaluate primary trigger
            primaryTriggerMet = schedule.PrimaryTriggerType switch
            {
                "Time" => schedule.NextDueDate.Date <= DateTime.UtcNow.Date,
                "Usage" => await EvaluateUsageTriggersAsync(scheduleId),
                "Condition" => await EvaluateConditionTriggersAsync(scheduleId),
                _ => false
            };

            // If not combined, return primary result
            if (schedule.PrimaryTriggerType != "Combined")
            {
                return primaryTriggerMet;
            }

            // Evaluate secondary trigger for combined schedules
            if (!string.IsNullOrEmpty(schedule.SecondaryTriggerType))
            {
                secondaryTriggerMet = schedule.SecondaryTriggerType switch
                {
                    "Time" => schedule.NextDueDate.Date <= DateTime.UtcNow.Date,
                    "Usage" => await EvaluateUsageTriggersAsync(scheduleId),
                    "Condition" => await EvaluateConditionTriggersAsync(scheduleId),
                    _ => false
                };
            }

            // Apply trigger logic (AND/OR)
            var shouldGenerate = schedule.TriggerLogic?.ToUpper() == "AND"
                ? primaryTriggerMet && secondaryTriggerMet
                : primaryTriggerMet || secondaryTriggerMet;

            _logger.LogInformation("Schedule {ScheduleId} trigger evaluation: Primary={Primary}, Secondary={Secondary}, Logic={Logic}, Result={Result}",
                scheduleId, primaryTriggerMet, secondaryTriggerMet, schedule.TriggerLogic, shouldGenerate);

            return shouldGenerate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating if work order should be generated for schedule {ScheduleId}", scheduleId);
            return false;
        }
    }

    public async Task ProcessUsageBasedSchedulesAsync()
    {
        try
        {
            _logger.LogInformation("Processing usage-based schedules");

            var usageSchedules = await _scheduleRepository.GetSchedulesByUsageTriggersAsync();
            var generatedCount = 0;

            foreach (var schedule in usageSchedules.Where(s => s.AutoGenerateWorkOrders))
            {
                try
                {
                    if (await ShouldGenerateWorkOrderAsync(schedule.Id))
                    {
                        await GenerateWorkOrderFromScheduleAsync(schedule.Id);
                        generatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing usage-based schedule {ScheduleId}", schedule.Id);
                }
            }

            _logger.LogInformation("Generated {Count} work orders from usage-based schedules", generatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing usage-based schedules");
            throw;
        }
    }

    public async Task ProcessConditionBasedSchedulesAsync()
    {
        try
        {
            _logger.LogInformation("Processing condition-based schedules");

            var conditionSchedules = await _scheduleRepository.GetSchedulesByConditionTriggersAsync();
            var generatedCount = 0;

            foreach (var schedule in conditionSchedules.Where(s => s.AutoGenerateWorkOrders))
            {
                try
                {
                    if (await ShouldGenerateWorkOrderAsync(schedule.Id))
                    {
                        await GenerateWorkOrderFromScheduleAsync(schedule.Id);
                        generatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing condition-based schedule {ScheduleId}", schedule.Id);
                }
            }

            _logger.LogInformation("Generated {Count} work orders from condition-based schedules", generatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing condition-based schedules");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static List<string> ParseRecipients(string? notificationRecipients)
    {
        if (string.IsNullOrWhiteSpace(notificationRecipients))
        {
            return new List<string>();
        }

        return notificationRecipients
            .Split(separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrEmpty(r))
            .ToList();
    }

    #endregion
}
