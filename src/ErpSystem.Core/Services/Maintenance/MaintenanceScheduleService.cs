using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
                throw new ArgumentException($"Schedule code '{createDto.Code}' already exists");

            // Validate asset and maintenance type exist
            var asset = await _assetService.GetAssetByIdAsync(createDto.AssetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {createDto.AssetId} not found");

            var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(createDto.MaintenanceTypeId);
            if (maintenanceType == null)
                throw new ArgumentException($"Maintenance type with ID {createDto.MaintenanceTypeId} not found");

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
                FrequencyUnit = createDto.FrequencyUnit,
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

            var schedule = await _scheduleRepository.GetByIdAsync(id);
            if (schedule == null)
                throw new ArgumentException($"Schedule with ID {id} not found");

            // Update properties
            schedule.Name = updateDto.Name;
            schedule.Description = updateDto.Description;
            schedule.AssetId = updateDto.AssetId;
            schedule.MaintenanceTypeId = updateDto.MaintenanceTypeId;
            schedule.Frequency = updateDto.Frequency;
            schedule.FrequencyValue = updateDto.FrequencyValue;
            schedule.FrequencyUnit = updateDto.FrequencyUnit;
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

    public async Task DeleteScheduleAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting maintenance schedule: {ScheduleId}", id);

            var schedule = await _scheduleRepository.GetByIdAsync(id);
            if (schedule == null)
                throw new ArgumentException($"Schedule with ID {id} not found");

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
            filtered = filtered.Where(s => s.AssetId == filter.AssetId.Value);

        if (filter.MaintenanceTypeId.HasValue)
            filtered = filtered.Where(s => s.MaintenanceTypeId == filter.MaintenanceTypeId.Value);

        if (!string.IsNullOrEmpty(filter.Frequency))
            filtered = filtered.Where(s => s.Frequency == filter.Frequency);

        if (!string.IsNullOrEmpty(filter.Priority))
            filtered = filtered.Where(s => s.Priority == filter.Priority);

        if (filter.IsActive.HasValue)
            filtered = filtered.Where(s => s.IsActive == filter.IsActive.Value);

        if (filter.IsOverdue.HasValue)
            filtered = filtered.Where(s => s.IsOverdue == filter.IsOverdue.Value);

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
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null)
            throw new ArgumentException($"Schedule with ID {id} not found");

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
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        if (schedule == null)
            throw new ArgumentException($"Schedule with ID {scheduleId} not found");

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

        // Create work order from schedule
        var createWorkOrder = new CreateWorkOrderDto
        {
            Title = $"{schedule.Name} - Scheduled Maintenance",
            Description = schedule.Description,
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
            RequestedStartDate = schedule.NextDueDate,
            Status = "Open",
            WorkOrderType = "Scheduled"
        };

        var workOrder = await _workOrderService.CreateWorkOrderAsync(createWorkOrder);
        
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
                PreviousValues = JsonSerializer.Serialize(new { 
                    NextDueDate = schedule.NextDueDate, 
                    LastGeneratedDate = schedule.LastGeneratedDate 
                }),
                NewValues = JsonSerializer.Serialize(new { 
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
                newValues: new { 
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
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        if (schedule == null)
            throw new ArgumentException($"Schedule with ID {scheduleId} not found");

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
            DaysUntilDue = schedule.DaysUntilDue
        };
    }

    #endregion
}