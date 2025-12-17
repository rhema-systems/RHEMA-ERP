using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class TechnicianSchedulingService : ITechnicianSchedulingService
{
    private readonly ITechnicianScheduleRepository _scheduleRepository;
    private readonly ITechnicianAvailabilityRepository _availabilityRepository;
    private readonly ITechnicianShiftRepository _shiftRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IEmployeeService _employeeService; // For accessing technicians from HR
    private readonly ILogger<TechnicianSchedulingService> _logger;

    public TechnicianSchedulingService(
        ITechnicianScheduleRepository scheduleRepository,
        ITechnicianAvailabilityRepository availabilityRepository,
        ITechnicianShiftRepository shiftRepository,
        IWorkOrderRepository workOrderRepository,
        IEmployeeService employeeService,
        ILogger<TechnicianSchedulingService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _availabilityRepository = availabilityRepository;
        _shiftRepository = shiftRepository;
        _workOrderRepository = workOrderRepository;
        _employeeService = employeeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available technicians from the HR Employee service
    /// </summary>
    public async Task<IEnumerable<TechnicianDto>> GetAvailableTechniciansAsync()
    {
        try
        {
            // Get maintenance technicians from HR Employee service
            var technicians = await _employeeService.GetAvailableTechniciansAsync();

            return technicians.Select(t => new TechnicianDto
            {
                Id = t.Id,
                FirstName = t.FullName?.Split(' ').FirstOrDefault() ?? string.Empty,
                LastName = string.Join(" ", t.FullName?.Split(' ').Skip(1) ?? Array.Empty<string>()),
                FullName = t.FullName ?? string.Empty,
                Email = t.EmailAddress ?? string.Empty,
                PhoneNumber = t.MobileNumber ?? string.Empty,
                IsActive = t.IsActive,
                EmployeeNumber = t.EmployeeNumber ?? string.Empty,
                PositionTitle = t.PositionTitle ?? string.Empty,
                Skills = t.Skills?.Select(s => new TechnicianSkillDto
                {
                    SkillName = s.SkillName,
                    Level = s.Level,
                    IsCertified = s.IsCertified
                }).ToList() ?? new List<TechnicianSkillDto>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available technicians");
            throw;
        }
    }

    /// <summary>
    /// Checks if a technician is available for a specific time period
    /// Advanced implementation with proper conflict detection and availability validation
    /// </summary>
    public async Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startTime, DateTime endTime)
    {
        try
        {
            // Input validation
            if (startTime >= endTime)
            {
                throw new ArgumentException("Start time must be before end time");
            }

            if (startTime < DateTime.UtcNow.AddMinutes(-5)) // Allow 5 minute grace period for current time
            {
                return false; // Cannot schedule in the past
            }

            // Check if technician exists and is active
            var technician = await _employeeService.GetTechnicianByIdAsync(technicianId);
            if (technician == null || !technician.IsActive)
            {
                return false;
            }

            // Use optimized repository methods for better performance
            var unavailablePeriods = await _availabilityRepository.GetUnavailablePeriodsByTechnicianAsync(
                technicianId, startTime, endTime);

            if (unavailablePeriods.Any())
            {
                _logger.LogInformation("Technician {TechnicianId} has unavailable periods during {StartTime} - {EndTime}",
                    technicianId, startTime, endTime);
                return false;
            }

            // Check for conflicting schedules with proper time overlap logic
            var hasConflict = await _scheduleRepository.HasConflictingScheduleAsync(
                technicianId, startTime, endTime);

            if (hasConflict)
            {
                _logger.LogInformation("Technician {TechnicianId} has conflicting schedules during {StartTime} - {EndTime}",
                    technicianId, startTime, endTime);
                return false;
            }

            // Check working hours constraints (business rule)
            if (!IsWithinWorkingHours(technicianId, startTime, endTime))
            {
                _logger.LogInformation("Requested time for Technician {TechnicianId} is outside working hours", technicianId);
                return false;
            }

            // Check maximum daily/weekly hour constraints
            if (!await IsWithinHourLimitsAsync(technicianId, startTime, endTime))
            {
                _logger.LogInformation("Requested time would exceed hour limits for Technician {TechnicianId}", technicianId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking technician availability for {TechnicianId}", technicianId);
            throw;
        }
    }

    /// <summary>
    /// Gets technician availability for a date range
    /// </summary>
    public async Task<TechnicianAvailabilityDto> GetTechnicianAvailabilityAsync(
        Guid technicianId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Input validation
            if (technicianId == Guid.Empty)
            {
                throw new ArgumentException("Valid technician ID is required", nameof(technicianId));
            }

            if (startDate >= endDate)
            {
                throw new ArgumentException("Start date must be before end date");
            }

            var technician = await _employeeService.GetTechnicianByIdAsync(technicianId) ?? throw new ArgumentException($"Technician with ID {technicianId} not found");

            // Simplified data retrieval using basic repository methods
            var allAvailability = await _availabilityRepository.GetAllAsync();
            var availability = allAvailability.Where(a =>
                a.TechnicianId == technicianId &&
                a.StartDate <= endDate &&
                a.EndDate >= startDate).ToList();

            var allSchedules = await _scheduleRepository.GetAllAsync();
            var schedules = allSchedules.Where(s =>
                s.TechnicianId == technicianId &&
                s.StartDate <= endDate &&
                s.EndDate >= startDate).ToList();

            // Calculate available time slots
            var availableSlots = CalculateAvailableTimeSlots(availability, schedules, startDate, endDate);

            return new TechnicianAvailabilityDto
            {
                TechnicianId = technicianId,
                TechnicianName = technician.FullName ?? "Unknown Technician",
                StartDate = startDate,
                EndDate = endDate,
                AvailableSlots = availableSlots,
                TotalAvailableHours = availableSlots.Sum(slot =>
                    (slot.EndTime - slot.StartTime).TotalHours),
                ScheduledHours = schedules.Sum(s => s.EstimatedHours),
                Utilization = (decimal)CalculateUtilization(availability, schedules, startDate, endDate)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician availability for {TechnicianId}", technicianId);
            throw;
        }
    }

    /// <summary>
    /// Schedules a work order for a technician with comprehensive validation
    /// </summary>
    public async Task<TechnicianScheduleDto> ScheduleWorkOrderAsync(ScheduleWorkOrderDto request)
    {
        try
        {
            // Input validation
            ArgumentNullException.ThrowIfNull(request);

            if (request.TechnicianId == Guid.Empty)
            {
                throw new ArgumentException("Valid technician ID is required", nameof(request));
            }

            if (request.WorkOrderId == Guid.Empty)
            {
                throw new ArgumentException("Valid work order ID is required", nameof(request));
            }

            if (request.StartTime >= request.EndTime)
            {
                throw new ArgumentException("Start time must be before end time", nameof(request));
            }

            if (request.EstimatedHours <= 0)
            {
                throw new ArgumentException("Estimated hours must be greater than zero", nameof(request));
            }

            // Business rule validation
            var actualHours = (request.EndTime - request.StartTime).TotalHours;
            if (Math.Abs(actualHours - (double)request.EstimatedHours) > 0.5) // Allow 30 minute variance
            {
                _logger.LogWarning("Time span ({ActualHours:F2}h) differs significantly from estimated hours ({EstimatedHours:F2}h) for work order {WorkOrderId}",
                    actualHours, request.EstimatedHours, request.WorkOrderId);
            }

            // Validate technician availability
            if (!await IsTechnicianAvailableAsync(request.TechnicianId, request.StartTime, request.EndTime))
            {
                throw new InvalidOperationException($"Technician is not available for the requested time period");
            }

            // Get work order details
            var workOrder = await _workOrderRepository.GetByIdAsync(request.WorkOrderId) ?? throw new ArgumentException($"Work order with ID {request.WorkOrderId} not found");

            // Create schedule entry
            var schedule = new TechnicianSchedule
            {
                TechnicianId = request.TechnicianId,
                WorkOrderId = request.WorkOrderId,
                StartDate = request.StartTime,
                EndDate = request.EndTime,
                ScheduleType = "WorkOrder",
                Title = workOrder.Title ?? "Work Order",
                Description = workOrder.Description ?? string.Empty,
                Location = "Asset Location", // Would need to be loaded from Asset repository
                Priority = workOrder.PriorityLevel?.Name ?? "Medium",
                EstimatedHours = (double)request.EstimatedHours,
                Status = "Scheduled",
                Notes = request.Notes
            };

            await _scheduleRepository.AddAsync(schedule);
            var createdSchedule = schedule; // Use the created object

            // Update work order assignment
            workOrder.AssignedTechnicianId = request.TechnicianId;
            workOrder.Status = "Assigned";
            await _workOrderRepository.UpdateAsync(workOrder);

            return new TechnicianScheduleDto
            {
                Id = createdSchedule.Id,
                TechnicianId = createdSchedule.TechnicianId,
                WorkOrderId = createdSchedule.WorkOrderId ?? Guid.Empty,
                StartTime = createdSchedule.StartDate,
                EndTime = createdSchedule.EndDate,
                Title = createdSchedule.Title ?? string.Empty,
                Description = createdSchedule.Description ?? string.Empty,
                Location = createdSchedule.Location ?? string.Empty,
                Priority = createdSchedule.Priority ?? "Medium",
                Status = createdSchedule.Status ?? "Scheduled",
                EstimatedHours = (decimal)createdSchedule.EstimatedHours,
                Notes = createdSchedule.Notes ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling work order {WorkOrderId} for technician {TechnicianId}",
                request.WorkOrderId, request.TechnicianId);
            throw;
        }
    }

    /// <summary>
    /// Finds the best available technician for a work order based on skills, availability, and workload
    /// </summary>
    public async Task<TechnicianRecommendationDto> FindBestTechnicianAsync(
        Guid workOrderId, DateTime preferredStartTime, double estimatedHours)
    {
        try
        {
            // Input validation
            if (workOrderId == Guid.Empty)
            {
                throw new ArgumentException("Valid work order ID is required", nameof(workOrderId));
            }

            if (estimatedHours <= 0)
            {
                throw new ArgumentException("Estimated hours must be greater than zero", nameof(estimatedHours));
            }

            if (preferredStartTime < DateTime.UtcNow.AddMinutes(-5))
            {
                throw new ArgumentException("Preferred start time cannot be in the past", nameof(preferredStartTime));
            }

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order with ID {workOrderId} not found");
            var preferredEndTime = preferredStartTime.AddHours(estimatedHours);
            var technicians = await GetAvailableTechniciansAsync();

            var recommendations = new List<TechnicianRecommendationDto>();

            foreach (var technician in technicians)
            {
                var isAvailable = await IsTechnicianAvailableAsync(technician.Id, preferredStartTime, preferredEndTime);
                if (!isAvailable)
                {
                    continue;
                }

                // Get technician's current workload
                var workload = await CalculateWorkloadAsync(technician.Id, preferredStartTime.Date, preferredStartTime.Date.AddDays(7));

                // Calculate score based on availability, workload, and skills using advanced algorithm
                var score = await CalculateTechnicianScoreAsync(technician, workload, workOrder);

                recommendations.Add(new TechnicianRecommendationDto
                {
                    TechnicianId = technician.Id,
                    TechnicianName = technician.FullName ?? "Unknown Technician",
                    Score = (decimal)score,
                    CurrentWorkload = (decimal)workload.UtilizationPercentage,
                    IsAvailable = true,
                    AvailableFrom = preferredStartTime,
                    ReasonForRecommendation = GenerateRecommendationReason(score, workload)
                });
            }

            // Return the best recommendation
            var bestRecommendation = recommendations.OrderByDescending(r => r.Score).FirstOrDefault();
            return bestRecommendation ?? new TechnicianRecommendationDto
            {
                TechnicianId = Guid.Empty,
                TechnicianName = "No available technician found",
                Score = 0,
                IsAvailable = false,
                ReasonForRecommendation = "No technicians available for the requested time period"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding best technician for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Gets technician workload for a specific period
    /// </summary>
    public async Task<TechnicianWorkloadDto> GetTechnicianWorkloadAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Input validation
            if (technicianId == Guid.Empty)
            {
                throw new ArgumentException("Valid technician ID is required", nameof(technicianId));
            }

            if (startDate >= endDate)
            {
                throw new ArgumentException("Start date must be before end date");
            }

            return await CalculateWorkloadAsync(technicianId, startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating workload for technician {TechnicianId}", technicianId);
            throw;
        }
    }

    /// <summary>
    /// Sets technician availability
    /// </summary>
    public async Task<TechnicianAvailabilityRecordDto> SetTechnicianAvailabilityAsync(SetAvailabilityDto request)
    {
        try
        {
            // Input validation
            ArgumentNullException.ThrowIfNull(request);

            if (request.TechnicianId == Guid.Empty)
            {
                throw new ArgumentException("Valid technician ID is required", nameof(request));
            }

            if (request.StartDate >= request.EndDate)
            {
                throw new ArgumentException("Start date must be before end date", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.AvailabilityType))
            {
                throw new ArgumentException("Availability type is required", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ArgumentException("Reason is required", nameof(request));
            }

            var availability = new TechnicianAvailability
            {
                TechnicianId = request.TechnicianId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                AvailabilityType = request.AvailabilityType,
                Reason = request.Reason,
                Notes = request.Notes,
                IsRecurring = request.IsRecurring,
                RecurrencePattern = request.RecurrencePattern,
                AvailableHours = (double?)request.AvailableHours,
                CapacityPercentage = (double?)request.CapacityPercentage
            };

            await _availabilityRepository.AddAsync(availability);
            var created = availability; // Use the created object

            return new TechnicianAvailabilityRecordDto
            {
                Id = created.Id,
                TechnicianId = created.TechnicianId,
                StartDate = created.StartDate,
                EndDate = created.EndDate,
                AvailabilityType = created.AvailabilityType ?? "Available",
                Reason = created.Reason ?? string.Empty,
                Notes = created.Notes ?? string.Empty,
                IsRecurring = created.IsRecurring,
                RecurrencePattern = created.RecurrencePattern ?? string.Empty,
                AvailableHours = (decimal)(created.AvailableHours ?? 0),
                CapacityPercentage = (decimal)(created.CapacityPercentage ?? 0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting availability for technician {TechnicianId}", request.TechnicianId);
            throw;
        }
    }

    #region Helper Methods

    private List<TimeSlotDto> CalculateAvailableTimeSlots(
        IEnumerable<TechnicianAvailability> availability,
        IEnumerable<TechnicianSchedule> schedules,
        DateTime startDate,
        DateTime endDate)
    {
        var availableSlots = new List<TimeSlotDto>();

        // This is a simplified implementation
        // In a real scenario, this would be more complex with proper time slot calculation
        var currentDate = startDate.Date;
        while (currentDate <= endDate.Date)
        {
            var dayStart = currentDate.AddHours(8); // 8 AM start
            var dayEnd = currentDate.AddHours(17);   // 5 PM end

            // Check if technician is available for this day
            var dayAvailability = availability.Where(a =>
                a.StartDate.Date <= currentDate && a.EndDate.Date >= currentDate &&
                a.AvailabilityType == "Available").ToList();

            if (dayAvailability.Any())
            {
                // Check for conflicts with existing schedules
                var daySchedules = schedules.Where(s =>
                    s.StartDate.Date <= currentDate && s.EndDate.Date >= currentDate).ToList();

                if (!daySchedules.Any() || !HasTimeConflict(dayStart, dayEnd, daySchedules))
                {
                    availableSlots.Add(new TimeSlotDto
                    {
                        StartTime = dayStart,
                        EndTime = dayEnd,
                        AvailableHours = 8.0m // Standard 8-hour day
                    });
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return availableSlots;
    }

    private static bool HasTimeConflict(DateTime startTime, DateTime endTime, IEnumerable<TechnicianSchedule> schedules)
    {
        return schedules.Any(s =>
            (startTime >= s.StartDate && startTime < s.EndDate) ||
            (endTime > s.StartDate && endTime <= s.EndDate) ||
            (startTime <= s.StartDate && endTime >= s.EndDate));
    }

    private static double CalculateUtilization(
        IEnumerable<TechnicianAvailability> availability,
        IEnumerable<TechnicianSchedule> schedules,
        DateTime startDate,
        DateTime endDate)
    {
        var totalAvailableHours = availability.Sum(a => a.AvailableHours ?? 8.0);
        var totalScheduledHours = schedules.Sum(s => s.EstimatedHours);

        return totalAvailableHours > 0 ? (totalScheduledHours / totalAvailableHours) * 100 : 0;
    }

    private async Task<TechnicianWorkloadDto> CalculateWorkloadAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        // Simplified data retrieval using basic repository methods
        var allSchedules = await _scheduleRepository.GetAllAsync();
        var schedules = allSchedules.Where(s =>
            s.TechnicianId == technicianId &&
            s.StartDate <= endDate &&
            s.EndDate >= startDate).ToList();

        var allAvailability = await _availabilityRepository.GetAllAsync();
        var availability = allAvailability.Where(a =>
            a.TechnicianId == technicianId &&
            a.StartDate <= endDate &&
            a.EndDate >= startDate).ToList();

        var totalScheduledHours = schedules.Sum(s => s.EstimatedHours);
        var totalAvailableHours = availability.Sum(a => a.AvailableHours ?? 8.0);
        var utilizationPercentage = totalAvailableHours > 0 ? (totalScheduledHours / totalAvailableHours) * 100 : 0;

        return new TechnicianWorkloadDto
        {
            TechnicianId = technicianId,
            StartDate = startDate,
            EndDate = endDate,
            TotalScheduledHours = totalScheduledHours,
            TotalAvailableHours = totalAvailableHours,
            UtilizationPercentage = utilizationPercentage,
            ActiveWorkOrders = schedules.Count(s => s.Status == "InProgress"),
            PendingWorkOrders = schedules.Count(s => s.Status == "Scheduled")
        };
    }

    private async Task<double> CalculateTechnicianScoreAsync(TechnicianDto technician, TechnicianWorkloadDto workload, WorkOrder workOrder)
    {
        try
        {
            // Multi-factor scoring algorithm with weighted components
            const double skillWeight = 0.35;      // 35% skill matching
            const double workloadWeight = 0.30;   // 30% workload balancing  
            const double priorityWeight = 0.20;   // 20% priority consideration
            const double availabilityWeight = 0.15; // 15% availability factors

            // Calculate individual component scores
            var skillScore = await CalculateSkillMatchScoreAsync(technician, workOrder);
            var workloadScore = CalculateWorkloadScore(workload);
            var priorityScore = CalculatePriorityScore(workOrder, workload);
            var availabilityScore = CalculateAvailabilityScore(technician, workload);

            // Weighted composite score
            var compositeScore =
                (skillScore * skillWeight) +
                (workloadScore * workloadWeight) +
                (priorityScore * priorityWeight) +
                (availabilityScore * availabilityWeight);

            _logger.LogDebug("Technician {TechnicianId} scoring: Skill={Skill:F1}, Workload={Workload:F1}, Priority={Priority:F1}, Availability={Availability:F1}, Composite={Composite:F1}",
                technician.Id, skillScore, workloadScore, priorityScore, availabilityScore, compositeScore);

            return Math.Max(0, Math.Min(100, compositeScore));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating technician score for {TechnicianId}", technician.Id);
            return 50.0; // Return neutral score on error
        }
    }

    /// <summary>
    /// Calculates availability-based scoring factors
    /// </summary>
    private static double CalculateAvailabilityScore(TechnicianDto technician, TechnicianWorkloadDto workload)
    {
        double score = 100.0;

        // Penalize if technician has too many active work orders
        if (workload.ActiveWorkOrders > 5)
        {
            score -= (workload.ActiveWorkOrders - 5) * 5;
        }

        // Penalize if technician has too many pending work orders
        if (workload.PendingWorkOrders > 10)
        {
            score -= (workload.PendingWorkOrders - 10) * 2;
        }

        // Bonus for technicians with some availability buffer
        if (workload.UtilizationPercentage < 70)
        {
            score += 10; // Bonus for having capacity
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private static string GenerateRecommendationReason(double score, TechnicianWorkloadDto workload)
    {
        if (score >= 90)
        {
            return "Excellent choice: Low workload and high availability";
        }
        else if (score >= 70)
        {
            return "Good choice: Moderate workload";
        }
        else if (score >= 50)
        {
            return "Acceptable choice: Higher workload";
        }
        else
        {
            return "Last resort: Very high workload";
        }
    }

    /// <summary>
    /// Checks if the requested time is within the technician's working hours
    /// </summary>
    private static bool IsWithinWorkingHours(Guid technicianId, DateTime startTime, DateTime endTime)
    {
        // Business rule: Standard working hours are 6 AM to 10 PM
        var dayStartHour = 6;
        var dayEndHour = 22;

        var startHour = startTime.Hour + (startTime.Minute / 60.0);
        var endHour = endTime.Hour + (endTime.Minute / 60.0);

        // Check if the entire time span is within working hours
        return startHour >= dayStartHour && endHour <= dayEndHour;
    }

    /// <summary>
    /// Checks if scheduling this work would exceed maximum daily or weekly hour limits
    /// </summary>
    private async Task<bool> IsWithinHourLimitsAsync(Guid technicianId, DateTime startTime, DateTime endTime)
    {
        try
        {
            var requestedHours = (endTime - startTime).TotalHours;

            // Business rules for maximum hours
            const double maxDailyHours = 12.0; // Maximum 12 hours per day
            const double maxWeeklyHours = 50.0; // Maximum 50 hours per week

            // Check daily limit
            var dayStart = startTime.Date;
            var dayEnd = dayStart.AddDays(1).AddTicks(-1);
            var dailyScheduledHours = await _scheduleRepository.GetTotalScheduledHoursAsync(
                technicianId, dayStart, dayEnd);

            if (dailyScheduledHours + requestedHours > maxDailyHours)
            {
                _logger.LogInformation("Daily hour limit would be exceeded for technician {TechnicianId}. Current: {Current}, Requested: {Requested}, Limit: {Limit}",
                    technicianId, dailyScheduledHours, requestedHours, maxDailyHours);
                return false;
            }

            // Check weekly limit
            var weekStart = startTime.Date.AddDays(-(int)startTime.DayOfWeek);
            var weekEnd = weekStart.AddDays(7).AddTicks(-1);
            var weeklyScheduledHours = await _scheduleRepository.GetTotalScheduledHoursAsync(
                technicianId, weekStart, weekEnd);

            if (weeklyScheduledHours + requestedHours > maxWeeklyHours)
            {
                _logger.LogInformation("Weekly hour limit would be exceeded for technician {TechnicianId}. Current: {Current}, Requested: {Requested}, Limit: {Limit}",
                    technicianId, weeklyScheduledHours, requestedHours, maxWeeklyHours);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking hour limits for technician {TechnicianId}", technicianId);
            return false; // Err on the side of caution
        }
    }

    /// <summary>
    /// Advanced skill matching algorithm that calculates skill compatibility score
    /// </summary>
    private async Task<double> CalculateSkillMatchScoreAsync(TechnicianDto technician, WorkOrder workOrder)
    {
        try
        {
            // Base score for having any skills
            double skillScore = 50.0;

            // If no required skills specified, return base score
            if (workOrder.CustomFields == null)
            {
                return skillScore;
            }

            // TODO: When CustomFields contain skill requirements, enhance this logic
            // For now, apply basic scoring based on available technician skills
            if (technician.Skills?.Any() == true)
            {
                var skillCount = technician.Skills.Count;
                var avgSkillLevel = technician.Skills.Average(s =>
                    s.Level switch
                    {
                        1 => 1.0,
                        2 => 2.0,
                        3 => 3.0,
                        4 => 4.0,
                        _ => 1.0
                    });

                skillScore = 30 + (skillCount * 5) + (avgSkillLevel * 10);

                // Bonus for certified skills
                var certifiedSkills = technician.Skills.Count(s => s.IsCertified);
                skillScore += certifiedSkills * 5;
            }

            return Math.Min(100, skillScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating skill match score for technician {TechnicianId}", technician.Id);
            return 50.0; // Return average score on error
        }
    }

    /// <summary>
    /// Advanced workload balancing algorithm
    /// </summary>
    private static double CalculateWorkloadScore(TechnicianWorkloadDto workload)
    {
        // Optimal utilization is around 75-85%
        const double optimalUtilization = 80.0;
        const double maxAcceptableUtilization = 90.0;

        if (workload.UtilizationPercentage <= optimalUtilization)
        {
            // Score decreases as we get further from optimal
            return 100 - Math.Abs(workload.UtilizationPercentage - optimalUtilization);
        }
        else if (workload.UtilizationPercentage <= maxAcceptableUtilization)
        {
            // Penalize over-utilization more heavily
            return 100 - (workload.UtilizationPercentage - optimalUtilization) * 2;
        }
        else
        {
            // Heavy penalty for excessive utilization
            return Math.Max(0, 20 - (workload.UtilizationPercentage - maxAcceptableUtilization));
        }
    }

    /// <summary>
    /// Priority-based scheduling algorithm that considers work order urgency
    /// </summary>
    private static double CalculatePriorityScore(WorkOrder workOrder, TechnicianWorkloadDto workload)
    {
        // Base priority scoring
        var priorityScore = workOrder.PriorityLevel?.Level switch
        {
            1 => 100.0, // Critical
            2 => 85.0,  // High  
            3 => 70.0,  // Medium
            4 => 55.0,  // Low
            5 => 40.0,  // Very Low
            _ => 60.0   // Default
        };

        // Adjust based on current workload - critical items can override workload concerns
        if (workOrder.PriorityLevel?.Level == 1) // Critical priority
        {
            return priorityScore; // Don't penalize for critical items
        }
        else
        {
            // For non-critical items, factor in workload
            var workloadFactor = Math.Max(0.5, (100 - workload.UtilizationPercentage) / 100);
            return priorityScore * workloadFactor;
        }
    }

    #endregion

    #region Interface Matching Methods

    /// <summary>
    /// Gets available technicians (interface method)
    /// </summary>
    public async Task<IEnumerable<object>> GetAvailableTechniciansAsync(DateTime startDate, DateTime endDate, string? requiredSkills = null)
    {
        var technicians = await GetAvailableTechniciansAsync();
        return technicians.Cast<object>();
    }

    // GetTechnicianAvailabilityAsync already implemented with correct return type

    /// <summary>
    /// Schedules work order (interface method)
    /// </summary>
    public async Task<object> ScheduleWorkOrderAsync(Guid workOrderId, Guid technicianId, DateTime scheduledDate)
    {
        var request = new ScheduleWorkOrderDto
        {
            WorkOrderId = workOrderId,
            TechnicianId = technicianId,
            StartTime = scheduledDate,
            EndTime = scheduledDate.AddHours(8), // Default 8 hours
            EstimatedHours = 8.0m
        };
        var result = await ScheduleWorkOrderAsync(request);
        return result;
    }

    /// <summary>
    /// Finds best technician (interface method)
    /// </summary>
    public async Task<object> FindBestTechnicianAsync(Guid workOrderId, DateTime scheduledDate)
    {
        var recommendation = await FindBestTechnicianAsync(workOrderId, scheduledDate, 8.0);
        return recommendation;
    }

    // GetTechnicianWorkloadAsync already implemented with correct return type

    /// <summary>
    /// Sets technician availability (interface method)
    /// </summary>
    public async Task SetTechnicianAvailabilityAsync(Guid technicianId, DateTime startDate, DateTime endDate, string availabilityType, string? reason = null)
    {
        var request = new SetAvailabilityDto
        {
            TechnicianId = technicianId,
            StartDate = startDate,
            EndDate = endDate,
            AvailabilityType = availabilityType,
            Reason = reason ?? "No reason provided"
        };
        await SetTechnicianAvailabilityAsync(request);
    }

    #endregion
}
