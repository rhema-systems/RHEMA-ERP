using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for evaluating maintenance schedule triggers and auto-generating work orders
/// </summary>
public class MaintenanceTriggerEvaluationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkOrderService _workOrderService;
    private readonly IAssetUsageTrackingService _usageTrackingService;
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly IMaintenanceNotificationService _notificationService;
    private readonly ILogger<MaintenanceTriggerEvaluationService> _logger;
    private readonly ICurrentUserService _currentUserService;

    public MaintenanceTriggerEvaluationService(
        IUnitOfWork unitOfWork,
        IWorkOrderService workOrderService,
        IAssetUsageTrackingService usageTrackingService,
        IMaintenanceScheduleService scheduleService,
        IMaintenanceNotificationService notificationService,
        ILogger<MaintenanceTriggerEvaluationService> logger,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _workOrderService = workOrderService;
        _usageTrackingService = usageTrackingService;
        _scheduleService = scheduleService;
        _notificationService = notificationService;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Evaluates all active schedules and generates work orders for triggered schedules
    /// </summary>
    /// <param name="tenantId">Optional tenant ID. If null, evaluates schedules for all tenants (background service mode)</param>
    public async Task<int> EvaluateAllSchedulesAsync(Guid? tenantId = null)
    {
        _logger.LogInformation("Starting maintenance schedule evaluation for {TenantMode}",
            tenantId.HasValue ? $"tenant {tenantId}" : "all tenants");
        var generatedCount = 0;

        try
        {
            // If tenantId is provided, filter by it. Otherwise, evaluate all tenants (background service)
            var schedules = tenantId.HasValue
                ? await _unitOfWork.Repository<MaintenanceSchedule>()
                    .FindAsync(s => s.IsActive && s.AutoGenerateWorkOrders && s.TenantId == tenantId.Value)
                : await _unitOfWork.Repository<MaintenanceSchedule>()
                    .FindAsync(s => s.IsActive && s.AutoGenerateWorkOrders);

            foreach (var schedule in schedules)
            {
                try
                {
                    var triggered = await EvaluateScheduleTriggerAsync(schedule);
                    if (triggered)
                    {
                        await GenerateWorkOrderFromScheduleAsync(schedule);
                        generatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating schedule {ScheduleId}", schedule.Id);
                }
            }

            _logger.LogInformation("Schedule evaluation completed. Generated {Count} work orders", generatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during schedule evaluation");
        }

        return generatedCount;
    }

    /// <summary>
    /// Evaluates a specific schedule to determine if it should trigger
    /// </summary>
    public async Task<bool> EvaluateScheduleTriggerAsync(MaintenanceSchedule schedule)
    {
        return schedule.PrimaryTriggerType switch
        {
            "Time" => await EvaluateTimeBasedTriggerAsync(schedule),
            "Usage" => await EvaluateUsageBasedTriggerAsync(schedule),
            "Condition" => await EvaluateConditionBasedTriggerAsync(schedule),
            "Combined" => await EvaluateCombinedTriggerAsync(schedule),
            _ => false
        };
    }

    private async Task<bool> EvaluateTimeBasedTriggerAsync(MaintenanceSchedule schedule)
    {
        // Check if NextDueDate has passed
        if (DateTime.UtcNow >= schedule.NextDueDate)
        {
            _logger.LogInformation("Time-based trigger met for schedule {ScheduleId}", schedule.Id);
            return true;
        }

        return false;
    }

    private async Task<bool> EvaluateUsageBasedTriggerAsync(MaintenanceSchedule schedule)
    {
        var usageSummary = await _usageTrackingService.GetAssetUsageSummaryAsync(schedule.AssetId, schedule.TenantId);

        // Check mileage trigger
        if (schedule.MileageTrigger.HasValue && usageSummary.CurrentMileage.HasValue)
        {
            if (!schedule.LastUsageValue.HasValue)
            {
                // First time - set baseline to current mileage
                schedule.LastUsageValue = usageSummary.CurrentMileage.Value;
                await _unitOfWork.Repository<MaintenanceSchedule>().UpdateAsync(schedule);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Mileage baseline set for schedule {ScheduleId}: {BaselineMileage}", schedule.Id, schedule.LastUsageValue);
            }
            else
            {
                var mileageDelta = usageSummary.CurrentMileage.Value - schedule.LastUsageValue.Value;
                if (mileageDelta >= schedule.MileageTrigger.Value)
                {
                    _logger.LogInformation("Mileage trigger met for schedule {ScheduleId}. Current: {Current}, Baseline: {Baseline}, Delta: {Delta}, Trigger: {Trigger}",
                        schedule.Id, usageSummary.CurrentMileage.Value, schedule.LastUsageValue.Value, mileageDelta, schedule.MileageTrigger.Value);
                    return true;
                }
            }
        }

        // Check operating hours trigger
        if (schedule.OperatingHoursTrigger.HasValue && usageSummary.CurrentOperatingHours.HasValue)
        {
            if (!schedule.LastUsageValue.HasValue)
            {
                // First time - set baseline to current operating hours
                schedule.LastUsageValue = usageSummary.CurrentOperatingHours.Value;
                await _unitOfWork.Repository<MaintenanceSchedule>().UpdateAsync(schedule);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Operating hours baseline set for schedule {ScheduleId}: {BaselineHours}", schedule.Id, schedule.LastUsageValue);
            }
            else
            {
                var hoursDelta = usageSummary.CurrentOperatingHours.Value - schedule.LastUsageValue.Value;
                if (hoursDelta >= schedule.OperatingHoursTrigger.Value)
                {
                    _logger.LogInformation("Operating hours trigger met for schedule {ScheduleId}. Current: {Current}, Baseline: {Baseline}, Delta: {Delta}, Trigger: {Trigger}",
                        schedule.Id, usageSummary.CurrentOperatingHours.Value, schedule.LastUsageValue.Value, hoursDelta, schedule.OperatingHoursTrigger.Value);
                    return true;
                }
            }
        }

        // Check cycle trigger
        if (schedule.CycleTrigger.HasValue && usageSummary.CurrentCycles.HasValue)
        {
            if (!schedule.LastUsageValue.HasValue)
            {
                // First time - set baseline to current cycles
                schedule.LastUsageValue = usageSummary.CurrentCycles.Value;
                await _unitOfWork.Repository<MaintenanceSchedule>().UpdateAsync(schedule);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Cycles baseline set for schedule {ScheduleId}: {BaselineCycles}", schedule.Id, schedule.LastUsageValue);
            }
            else
            {
                var cyclesDelta = usageSummary.CurrentCycles.Value - (int)schedule.LastUsageValue.Value;
                if (cyclesDelta >= schedule.CycleTrigger.Value)
                {
                    _logger.LogInformation("Cycles trigger met for schedule {ScheduleId}. Current: {Current}, Baseline: {Baseline}, Delta: {Delta}, Trigger: {Trigger}",
                        schedule.Id, usageSummary.CurrentCycles.Value, (int)schedule.LastUsageValue.Value, cyclesDelta, schedule.CycleTrigger.Value);
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> EvaluateConditionBasedTriggerAsync(MaintenanceSchedule schedule)
    {
        if (string.IsNullOrEmpty(schedule.ConditionCriteria))
        {
            return false;
        }

        try
        {
            List<ConditionCriterion>? criteria = null;

            try
            {
                // Try to deserialize as a list first
                criteria = JsonSerializer.Deserialize<List<ConditionCriterion>>(schedule.ConditionCriteria);

                // If that fails, try as a single object and wrap it in a list
                if (criteria == null)
                {
                    var singleCriterion = JsonSerializer.Deserialize<ConditionCriterion>(schedule.ConditionCriteria);
                    if (singleCriterion != null)
                    {
                        criteria = new List<ConditionCriterion> { singleCriterion };
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Invalid JSON in ConditionCriteria for schedule {ScheduleId}: {CriteriaJson}",
                    schedule.Id, schedule.ConditionCriteria);
                return false;
            }

            if (criteria == null || !criteria.Any())
            {
                return false;
            }

            // Get latest usage record for condition data
            var usageRecords = await _usageTrackingService.GetUsageRecordsAsync(schedule.AssetId);
            var latestRecord = usageRecords.FirstOrDefault();

            if (latestRecord == null || string.IsNullOrEmpty(latestRecord.AdditionalMetrics))
            {
                return false;
            }

            var metrics = JsonSerializer.Deserialize<Dictionary<string, object>>(latestRecord.AdditionalMetrics);
            if (metrics == null)
            {
                return false;
            }

            // Evaluate all criteria
            var allCriteriaMet = true;
            foreach (var criterion in criteria)
            {
                if (!metrics.ContainsKey(criterion.Parameter))
                {
                    allCriteriaMet = false;
                    break;
                }

                var metricValue = Convert.ToDecimal(metrics[criterion.Parameter]);
                var criterionValue = Convert.ToDecimal(criterion.Value);

                var criterionMet = criterion.Operator switch
                {
                    ">" => metricValue > criterionValue,
                    ">=" => metricValue >= criterionValue,
                    "<" => metricValue < criterionValue,
                    "<=" => metricValue <= criterionValue,
                    "==" => metricValue == criterionValue,
                    "!=" => metricValue != criterionValue,
                    _ => false
                };

                if (!criterionMet)
                {
                    allCriteriaMet = false;
                    break;
                }
            }

            if (allCriteriaMet)
            {
                _logger.LogInformation("Condition-based trigger met for schedule {ScheduleId}", schedule.Id);

                // Update last condition check
                schedule.LastConditionCheckResult = true;
                schedule.LastConditionCheckDate = DateTime.UtcNow;
                await _unitOfWork.Repository<MaintenanceSchedule>().UpdateAsync(schedule);
                await _unitOfWork.SaveChangesAsync();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition-based trigger for schedule {ScheduleId}", schedule.Id);
        }

        return false;
    }

    private async Task<bool> EvaluateCombinedTriggerAsync(MaintenanceSchedule schedule)
    {
        var primaryTriggered = schedule.PrimaryTriggerType switch
        {
            "Time" => await EvaluateTimeBasedTriggerAsync(schedule),
            "Usage" => await EvaluateUsageBasedTriggerAsync(schedule),
            "Condition" => await EvaluateConditionBasedTriggerAsync(schedule),
            _ => false
        };

        if (string.IsNullOrEmpty(schedule.SecondaryTriggerType))
        {
            return primaryTriggered;
        }

        var secondaryTriggered = schedule.SecondaryTriggerType switch
        {
            "Time" => await EvaluateTimeBasedTriggerAsync(schedule),
            "Usage" => await EvaluateUsageBasedTriggerAsync(schedule),
            "Condition" => await EvaluateConditionBasedTriggerAsync(schedule),
            _ => false
        };

        // Apply trigger logic (AND/OR)
        return schedule.TriggerLogic?.ToUpper() == "AND"
            ? primaryTriggered && secondaryTriggered
            : primaryTriggered || secondaryTriggered;
    }

    private async Task GenerateWorkOrderFromScheduleAsync(MaintenanceSchedule schedule)
    {
        try
        {
            _logger.LogInformation("Generating work order from schedule {ScheduleId}", schedule.Id);

            // Get a valid priority level - use schedule's priority or default to first available
            var priorityLevelId = schedule.PriorityLevelId;
            if (!priorityLevelId.HasValue || priorityLevelId.Value == Guid.Empty)
            {
                // If no priority, get the first priority level from database
                var priorityLevels = await _unitOfWork.Repository<PriorityLevel>()
                    .FindAsync(p => p.TenantId == schedule.TenantId);
                priorityLevelId = priorityLevels.FirstOrDefault()?.Id ?? throw new InvalidOperationException("No priority levels found");
            }

            // Get a valid work order type
            var workOrderTypes = await _unitOfWork.Repository<WorkOrderType>()
                .FindAsync(w => w.TenantId == schedule.TenantId);
            var workOrderTypeId = workOrderTypes.FirstOrDefault()?.Id ?? throw new InvalidOperationException("No work order types found");

            var workOrderDto = new CreateWorkOrderDto
            {
                Title = $"{schedule.Name} - Scheduled Maintenance",
                Description = $"{schedule.Description}\n\n[Auto-generated from maintenance schedule: {schedule.Name} ({schedule.Code}). Next due date was: {schedule.NextDueDate:yyyy-MM-dd}]",
                AssetId = schedule.AssetId,
                MaintenanceTypeId = schedule.MaintenanceTypeId,
                PriorityLevelId = priorityLevelId.Value,
                WorkOrderTypeId = workOrderTypeId,
                EstimatedHours = (double)schedule.EstimatedHours,
                EstimatedCost = schedule.EstimatedCost,
                AssignedTechnicianId = schedule.AssignedTechnicianId,
                AssignedTeamId = schedule.AssignedTeamId,
                Instructions = schedule.Instructions,
                SafetyNotes = schedule.SafetyNotes,
                RequiredSkills = schedule.RequiredSkills,
                MaintenanceScheduleId = schedule.Id,
                ScheduledStartDate = DateTime.UtcNow,
                RequestedCompletionDate = DateTime.UtcNow.AddDays(7),
                TenantId = schedule.TenantId  // Pass schedule's tenant for background service context
            };

            var workOrder = await _workOrderService.CreateWorkOrderAsync(workOrderDto);

            // Send notification for work order assignment (non-blocking)
            try
            {
                if (schedule.AssignedTechnicianId.HasValue)
                {
                    var notificationDto = new CreateMaintenanceNotificationDto
                    {
                        NotificationType = "WorkOrderAssignment",
                        EntityType = "WorkOrder",
                        EntityId = workOrder.Id,
                        RecipientId = schedule.AssignedTechnicianId.Value,
                        Title = "New Auto-Generated Work Order Assignment",
                        Message = $"Work order '{workOrder.Title}' has been automatically assigned to you from maintenance schedule '{schedule.Name}'.",
                        Priority = schedule.Priority,
                        ScheduledFor = DateTime.UtcNow,
                        TenantId = schedule.TenantId  // Pass schedule's tenant for background service context
                    };

                    await _notificationService.CreateNotificationAsync(notificationDto);
                    _logger.LogInformation("Notification sent for auto-generated work order {WorkOrderId}", workOrder.Id);
                }
            }
            catch (Exception notifEx)
            {
                // Log notification failure but don't fail the whole operation
                _logger.LogWarning(notifEx, "Failed to send work order assignment notification for WO {WorkOrderId}", workOrder.Id);
            }

            // Update schedule
            schedule.LastCompletedDate = DateTime.UtcNow;
            schedule.NextDueDate = CalculateNextDueDate(schedule);

            // Update LastUsageValue if usage-based
            if (schedule.PrimaryTriggerType == "Usage")
            {
                var usageSummary = await _usageTrackingService.GetAssetUsageSummaryAsync(schedule.AssetId);
                if (schedule.MileageTrigger.HasValue && usageSummary.CurrentMileage.HasValue)
                {
                    schedule.LastUsageValue = usageSummary.CurrentMileage.Value;
                }
                else if (schedule.OperatingHoursTrigger.HasValue && usageSummary.CurrentOperatingHours.HasValue)
                {
                    schedule.LastUsageValue = usageSummary.CurrentOperatingHours.Value;
                }
                else if (schedule.CycleTrigger.HasValue && usageSummary.CurrentCycles.HasValue)
                {
                    schedule.LastUsageValue = usageSummary.CurrentCycles.Value;
                }
            }

            await _unitOfWork.Repository<MaintenanceSchedule>().UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            // Record in history
            await RecordScheduleHistoryAsync(schedule, workOrder.Id, "WorkOrderGenerated");

            _logger.LogInformation("Work order {WorkOrderId} generated from schedule {ScheduleId}", workOrder.Id, schedule.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating work order from schedule {ScheduleId}", schedule.Id);
            throw;
        }
    }

    private static DateTime CalculateNextDueDate(MaintenanceSchedule schedule)
    {
        if (schedule.PrimaryTriggerType == "Time")
        {
            return schedule.FrequencyUnit.ToLower() switch
            {
                "days" => schedule.NextDueDate.AddDays(schedule.FrequencyValue),
                "weeks" => schedule.NextDueDate.AddDays(schedule.FrequencyValue * 7),
                "months" => schedule.NextDueDate.AddMonths(schedule.FrequencyValue),
                "years" => schedule.NextDueDate.AddYears(schedule.FrequencyValue),
                _ => schedule.NextDueDate.AddDays(30)
            };
        }

        // For usage-based and condition-based, keep the same due date until manually updated
        return schedule.NextDueDate;
    }

    private async Task RecordScheduleHistoryAsync(MaintenanceSchedule schedule, Guid workOrderId, string changeType)
    {
        var history = new MaintenanceScheduleHistory
        {
            Id = Guid.NewGuid(),
            TenantId = schedule.TenantId,
            ScheduleId = schedule.Id,
            ChangeType = changeType,
            PreviousValues = null,
            NewValues = JsonSerializer.Serialize(new { WorkOrderId = workOrderId }),
            ChangeReason = "Automatic work order generation",
            ChangedById = Guid.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<MaintenanceScheduleHistory>().AddAsync(history);
        await _unitOfWork.SaveChangesAsync();
    }

    private class ConditionCriterion
    {
        public string Parameter { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public object Value { get; set; } = string.Empty;
    }
}
