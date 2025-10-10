namespace ErpSystem.Core.DTOs.Maintenance;

public class TechnicianDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public List<TechnicianSkillDto> Skills { get; set; } = new();
}

public class TechnicianSkillDto
{
    public string SkillName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public bool IsCertified { get; set; }
    public int UserCount { get; set; }
}

public class TechnicianAvailabilityDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TimeSlotDto> AvailableSlots { get; set; } = new();
    public double TotalAvailableHours { get; set; }
    public double ScheduledHours { get; set; }
    public double Utilization { get; set; }
}

public class TechnicianScheduleDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid WorkOrderId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class TechnicianRecommendationDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public double Score { get; set; }
    public TechnicianWorkloadDto CurrentWorkload { get; set; } = new();
    public bool IsAvailable { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public string ReasonForRecommendation { get; set; } = string.Empty;
}

public class TechnicianWorkloadDto
{
    public Guid TechnicianId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TotalScheduledHours { get; set; }
    public double TotalAvailableHours { get; set; }
    public double UtilizationPercentage { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int PendingWorkOrders { get; set; }
}

public class SetAvailabilityDto
{
    public Guid TechnicianId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string AvailabilityType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
    public string RecurrencePattern { get; set; } = string.Empty;
    public double AvailableHours { get; set; }
    public double CapacityPercentage { get; set; }
}

public class TechnicianAvailabilityRecordDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string AvailabilityType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
    public string RecurrencePattern { get; set; } = string.Empty;
    public double AvailableHours { get; set; }
    public double CapacityPercentage { get; set; }
}

public class TimeSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double AvailableHours { get; set; }
}

public class ScheduleWorkOrderDto
{
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public bool ReserveParts { get; set; } = true;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double EstimatedHours { get; set; }
    public string Notes { get; set; } = string.Empty;
}
