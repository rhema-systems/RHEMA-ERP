using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.DTOs.Maintenance;

#region Common DTOs

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public IEnumerable<T> Data => Items; // Alias for backward compatibility
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

#endregion

#region Technician DTOs

/// <summary>
/// Technician DTO for maintenance operations
/// </summary>
public class TechnicianDto
{
    public Guid Id { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? CertificationLevel { get; set; }
    public string? ExperienceLevel { get; set; }
    public DateTime? HireDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAvailable { get; set; }
    public List<TechnicianSkillDto> Skills { get; set; } = new();
    public int CurrentWorkOrders { get; set; }
    public decimal WorkloadScore { get; set; }
    public decimal CurrentWorkload { get; set; }
    public decimal MaxWorkload { get; set; }
    public int SkillsCount { get; set; }
    public decimal AverageRating { get; set; }
    public int CompletedWorkOrders { get; set; }
    public string? BadgeNumber { get; set; }
    public string? ShiftName { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Additional properties for controller compatibility
    public string? JobTitle { get; set; }
    public string? EmploymentStatus { get; set; }
    public int ActiveWorkOrdersCount { get; set; }
    public decimal PerformanceRating { get; set; }
    public string? Location { get; set; }
    public string? ShiftSchedule { get; set; }
    public decimal HourlyRate { get; set; }
    public string? Supervisor { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal WorkloadCapacity { get; set; }
    public int CompletedWorkOrdersCount { get; set; }
    public TimeSpan AverageCompletionTime { get; set; }
    public bool IsFromHRModule { get; set; }
    public string? Notes { get; set; }
    public List<string> Specializations { get; set; } = new();
    public List<TechnicianSkillAssignmentDto> SkillAssignments { get; set; } = new();
    public List<object> Certifications { get; set; } = new();
}

/// <summary>
/// DTO for creating technicians
/// </summary>
public class CreateTechnicianDto
{
    [Required]
    public Guid EmployeeId { get; set; }
    
    [StringLength(50)]
    public string? EmployeeNumber { get; set; }
    
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }
    
    [StringLength(20)]
    public string? Phone { get; set; }
    
    [StringLength(100)]
    public string? Department { get; set; }
    
    [StringLength(100)]
    public string? Position { get; set; }
    
    [StringLength(500)]
    public string? Specialization { get; set; }
    
    [StringLength(500)]
    public string? Specializations { get; set; }
    
    [StringLength(100)]
    public string? CertificationLevel { get; set; }
    
    [StringLength(50)]
    public string? ExperienceLevel { get; set; }
    
    public DateTime? HireDate { get; set; }
    
    [Range(0, 100)]
    public decimal MaxWorkload { get; set; } = 40;
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating technicians
/// </summary>
public class UpdateTechnicianDto
{
    [StringLength(100)]
    public string? FirstName { get; set; }
    
    [StringLength(100)]
    public string? LastName { get; set; }
    
    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }
    
    [StringLength(20)]
    public string? Phone { get; set; }
    
    [StringLength(100)]
    public string? Department { get; set; }
    
    [StringLength(100)]
    public string? Position { get; set; }
    
    [StringLength(500)]
    public string? Specialization { get; set; }
    
    [StringLength(500)]
    public string? Specializations { get; set; }
    
    [StringLength(100)]
    public string? CertificationLevel { get; set; }
    
    [StringLength(50)]
    public string? ExperienceLevel { get; set; }
    
    [Range(0, 100)]
    public decimal? MaxWorkload { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Technician skill DTO
/// </summary>
public class TechnicianSkillDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int Level { get; set; }
    public bool IsActive { get; set; }
    public bool IsCertified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Additional property for mapping profile compatibility
    public int UserCount { get; set; }
}

/// <summary>
/// Technician skill assignment DTO
/// </summary>
public class TechnicianSkillAssignmentDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int ProficiencyLevel { get; set; }
    public string? ProficiencyDescription { get; set; }
    public DateTime? CertificationDate { get; set; }
    public DateTime? AcquiredDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? CertifyingBody { get; set; }
    public string? CertificationNumber { get; set; }
    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? LastAssessmentDate { get; set; }
    public string? Notes { get; set; }
    public TechnicianDto? Technician { get; set; }
    public TechnicianSkillDto? Skill { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon => CertificationExpiry.HasValue && CertificationExpiry.Value < DateTime.UtcNow.AddDays(30);
    public int? DaysUntilExpiration { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating technician skill assignments
/// </summary>
public class CreateTechnicianSkillAssignmentDto
{
    [Required]
    public Guid TechnicianId { get; set; }
    
    [Required]
    public Guid SkillId { get; set; }
    
    [Range(1, 4)]
    public int ProficiencyLevel { get; set; } = 1;
    
    [StringLength(500)]
    public string? ProficiencyDescription { get; set; }
    
    public DateTime? CertificationDate { get; set; }
    public DateTime? AcquiredDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }
    public DateTime? ExpirationDate { get; set; }
    
    [StringLength(200)]
    public string? CertifyingBody { get; set; }
    
    [StringLength(100)]
    public string? CertificationNumber { get; set; }
    
    public bool IsVerified { get; set; }
    
    [StringLength(200)]
    public string? VerifiedBy { get; set; }
    
    public DateTime? LastAssessmentDate { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating technician skill assignments
/// </summary>
public class UpdateTechnicianSkillAssignmentDto
{
    [Range(1, 4)]
    public int ProficiencyLevel { get; set; }
    
    [StringLength(500)]
    public string? ProficiencyDescription { get; set; }
    
    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }
    public DateTime? ExpirationDate { get; set; }
    
    [StringLength(200)]
    public string? CertifyingBody { get; set; }
    
    [StringLength(100)]
    public string? CertificationNumber { get; set; }
    
    public bool IsVerified { get; set; }
    
    [StringLength(200)]
    public string? VerifiedBy { get; set; }
    
    public DateTime? LastAssessmentDate { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Missing List DTOs

/// <summary>
/// List DTO for technicians
/// </summary>
public class TechnicianListDto
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string EmploymentStatus { get; set; } = string.Empty;
    public string ExperienceLevel { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int ActiveWorkOrdersCount { get; set; }
    public int SkillsCount { get; set; }
    public int CertificationsCount { get; set; }
    public int ExpiredCertificationsCount { get; set; }
    public int ExpiringCertificationsCount { get; set; }
    public decimal PerformanceRating { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime? LastSyncDate { get; set; }
}

/// <summary>
/// List DTO for technical skills
/// </summary>
public class TechnicalSkillListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public int EstimatedLearningHours { get; set; }
    public bool IsActive { get; set; }
    public bool IsFromHRModule { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Additional properties for controller compatibility
    public int TechniciansCount { get; set; }
    public decimal AverageRating { get; set; }
}

/// <summary>
/// List DTO for safety protocols
/// </summary>
public class SafetyProtocolListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string RegulatoryStandard { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsMandatory { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public bool IsReviewDue { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    public decimal CompliancePercentage { get; set; }
}

/// <summary>
/// List DTO for maintenance schedules
/// </summary>
public class MaintenanceScheduleListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string MaintenanceTypeName { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime NextDueDate { get; set; }
    public string Priority { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysUntilDue { get; set; }
    public string AssignedTechnicianName { get; set; } = string.Empty;
    public decimal CompliancePercentage { get; set; }
}

#endregion

#region Missing Filter DTOs

/// <summary>
/// Filter DTO for technicians
/// </summary>
public class TechnicianFilterDto
{
    public string? SearchTerm { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsAvailable { get; set; }
    public string? Department { get; set; }
    public string? Specialization { get; set; }
    public string? CertificationLevel { get; set; }
    public Guid? DepartmentId { get; set; }
    public List<Guid>? SkillIds { get; set; }
    
    // Additional filter properties
    public string? JobTitle { get; set; }
    public string? EmploymentStatus { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? ShiftSchedule { get; set; }
    public Guid? SkillId { get; set; }
    public string? Location { get; set; }
    public bool? HasExpiredCertifications { get; set; }
    public bool? HasExpiringCertifications { get; set; }
    
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Filter DTO for technical skills
/// </summary>
public class TechnicalSkillFilterDto
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? SkillLevel { get; set; }
    public string? Complexity { get; set; }
    public string? RiskLevel { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFromHRModule { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Filter DTO for safety protocols
/// </summary>
public class SafetyProtocolFilterDto
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? RiskLevel { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsMandatory { get; set; }
    public bool? IsRegulatory { get; set; }
    public bool? NeedsReview { get; set; }
    public bool? IsExpired { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int PageNumber { get; set; } = 1;
    
    // Additional properties for controller compatibility
    public string? RegulatoryStandard { get; set; }
    public string? ApprovalStatus { get; set; }
    public bool? ReviewDue { get; set; }
}

#endregion

#region Missing Missing DTOs

/// <summary>
/// Technician certification DTO
/// </summary>
public class TechnicianCertificationDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public string CertificationNumber { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Work order assignment DTO
/// </summary>
public class WorkOrderAssignmentDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public double EstimatedHours { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

/// <summary>
/// Skill utilization DTO
/// </summary>
public class SkillUtilizationDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty; // Property expected by service
    public string Category { get; set; } = string.Empty;
    public int TechniciansCount { get; set; }
    public int TechniciansWithSkill { get; set; } // Property expected by service
    public int WorkOrdersCount { get; set; }
    public int WorkOrdersRequiringSkill { get; set; } // Property expected by service
    public double TotalHoursUsed { get; set; }
    public double AverageHoursPerWorkOrder { get; set; }
    public decimal UtilizationPercentage { get; set; } // Expected as decimal by service
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
}


#endregion

#region Missing Scheduling DTOs

/// <summary>
/// Schedule work order DTO
/// </summary>
public class ScheduleWorkOrderDto
{
    [Required]
    public Guid WorkOrderId { get; set; }
    
    [Required]
    public Guid TechnicianId { get; set; }
    
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    
    public DateTime ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal EstimatedHours { get; set; }
    public bool ReserveParts { get; set; } = false;
    public string? Notes { get; set; }
}

/// <summary>
/// Technician schedule DTO
/// </summary>
public class TechnicianScheduleDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid WorkOrderId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string WorkOrderType { get; set; } = string.Empty;
    public decimal EstimatedHours { get; set; }
    public string? Notes { get; set; }
    public TechnicianDto? Technician { get; set; }
    public WorkOrderDto? WorkOrder { get; set; }
}

/// <summary>
/// Technician availability DTO
/// </summary>
public class TechnicianAvailabilityDto
{
    public Guid TechnicianId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAvailable { get; set; }
    public double AvailableHours { get; set; }
    public double ScheduledHours { get; set; }
    public double RemainingHours { get; set; }
    public double TotalAvailableHours { get; set; }
    public List<TimeSlotDto> AvailableSlots { get; set; } = new();
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableUntil { get; set; }
    public string? UnavailabilityReason { get; set; }
    public int CurrentWorkOrders { get; set; }
    public decimal WorkloadPercentage { get; set; }
    public decimal Utilization { get; set; }
    
    // Additional properties for controller compatibility
    public string AvailabilityStatus { get; set; } = string.Empty;
    public string? ShiftSchedule { get; set; }
    public string? ReasonForUnavailability { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Technician workload DTO
/// </summary>
public class TechnicianWorkloadDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TotalHours { get; set; }
    public double ScheduledHours { get; set; }
    public double TotalScheduledHours { get; set; }
    public double AvailableHours { get; set; }
    public double TotalAvailableHours { get; set; }
    public double UtilizationPercentage { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int ScheduledWorkOrders { get; set; }
    public int PendingWorkOrders { get; set; }
    public int CompletedThisWeek { get; set; }
    public TimeSpan TotalScheduledTimeSpan { get; set; }
    public TimeSpan AvailableTimeSpan { get; set; }
    
    // Additional properties for controller compatibility
    public double EstimatedHours { get; set; }
    public double CapacityUtilization { get; set; }
    public bool IsOverloaded { get; set; }
    public DateTime? NextAvailableDate { get; set; }
    public List<WorkOrderAssignmentDto> CurrentAssignments { get; set; } = new();
}

/// <summary>
/// Technician recommendation DTO
/// </summary>
public class TechnicianRecommendationDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public decimal Score { get; set; }
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public decimal CurrentWorkload { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime? NextAvailable { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
    public string ReasonForRecommendation { get; set; } = string.Empty;
}

/// <summary>
/// Set availability DTO
/// </summary>
public class SetAvailabilityDto
{
    [Required]
    public Guid TechnicianId { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? Reason { get; set; }
    public string AvailabilityType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public decimal AvailableHours { get; set; }
    public decimal CapacityPercentage { get; set; }
}

/// <summary>
/// Technician availability record DTO
/// </summary>
public class TechnicianAvailabilityRecordDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
    public string AvailabilityType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public decimal AvailableHours { get; set; }
    public decimal CapacityPercentage { get; set; }
    public TechnicianDto? Technician { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Time slot DTO
/// </summary>
public class TimeSlotDto
{
    public DateTime Start { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime End { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
    public decimal AvailableHours { get; set; }
}

#endregion

#region Missing Performance DTOs

/// <summary>
/// Technician performance DTO
/// </summary>
public class TechnicianPerformanceDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int WorkOrdersCompleted { get; set; }
    public double AverageCompletionTime { get; set; }
    public double OnTimeCompletionRate { get; set; }
    public decimal TotalCost { get; set; }
    public double UtilizationRate { get; set; }
    public double QualityScore { get; set; }
    
    // Additional properties for controller compatibility
    public int CompletedWorkOrders { get; set; }
    public double AverageQualityRating { get; set; }
    public int OnTimeCompletions { get; set; }
    public int LateCompletions { get; set; }
    public double OnTimePercentage { get; set; }
    public int SafetyIncidents { get; set; }
    public DateTime? LastIncidentDate { get; set; }
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public List<object> SkillUtilization { get; set; } = new();
}

/// <summary>
/// Technician stats DTO
/// </summary>
public class TechnicianStatsDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public int TotalWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public int InProgressWorkOrders { get; set; }
    public double CompletionRate { get; set; }
    public double AverageRating { get; set; }
    public TimeSpan TotalHoursWorked { get; set; }
    public decimal TotalCostSaved { get; set; }
    
    // Additional properties for controller compatibility
    public int TotalTechnicians { get; set; }
    public int ActiveTechnicians { get; set; }
    public int AvailableTechnicians { get; set; }
    public int TechniciansOnAssignment { get; set; }
    public int TechniciansOnLeave { get; set; }
    public double AverageExperience { get; set; }
    public double AveragePerformanceRating { get; set; }
    public int CertificationsExpiring { get; set; }
    public int ExpiredCertifications { get; set; }
    public DateTime? LastSyncFromHR { get; set; }
}

#endregion

#region Missing Safety Compliance DTOs

/// <summary>
/// Safety compliance report DTO
/// </summary>
public class SafetyComplianceReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double OverallComplianceRate { get; set; }
    public int TotalProtocols { get; set; }
    public int ComplianceChecks { get; set; }
    public int Violations { get; set; }
    public List<ProtocolComplianceDto> ProtocolCompliance { get; set; } = new();
    public List<SafetyViolationSummaryDto> TopViolations { get; set; } = new();
    
    // Additional properties for controller compatibility
    public int ActiveProtocols { get; set; }
    public int MandatoryProtocols { get; set; }
    public int ProtocolsDueForReview { get; set; }
    public int OverdueProtocols { get; set; }
    public decimal OverallCompliancePercentage { get; set; }
    public int TotalIncidents { get; set; }
    public int ViolationsThisMonth { get; set; }
    public DateTime ReportGeneratedDate { get; set; }
    public List<CategoryComplianceDto> CategoryCompliance { get; set; } = new();
}

/// <summary>
/// Protocol compliance DTO
/// </summary>
public class ProtocolComplianceDto
{
    public Guid ProtocolId { get; set; }
    public string ProtocolName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double ComplianceRate { get; set; }
    public int TotalChecks { get; set; }
    public int ComplianceCount { get; set; }
    public int ViolationCount { get; set; }
    
    // Additional properties for controller compatibility
    public decimal CompliancePercentage { get; set; }
    public DateTime? LastViolationDate { get; set; }
}

/// <summary>
/// Safety violation summary DTO
/// </summary>
public class SafetyViolationSummaryDto
{
    public string ViolationType { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Severity { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    
    // Additional properties for controller compatibility
    public Guid Id { get; set; }
    public string ProtocolCode { get; set; } = string.Empty;
    public string ProtocolName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
}

#endregion

#region Maintenance-Inventory Integration DTOs

// Parts Allocation DTOs
public class MaintenancePartsAllocationResult
{
    public Guid WorkOrderId { get; set; }
    public int TotalPartsRequested { get; set; }
    public int SuccessfulAllocations { get; set; }
    public int FailedAllocationCount { get; set; }
    public DateTime AllocationDate { get; set; }
    public List<WorkOrderPartAllocationDto> AllocatedParts { get; set; } = new();
    public List<PartAllocationFailure> FailedAllocations { get; set; } = new();
}

public class WorkOrderPartAllocationDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public Guid AllocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public DateTime AllocatedAt { get; set; }
}

public class PartAllocationFailure
{
    public Guid PartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Parts Consumption DTOs
public class MaintenancePartsConsumptionResult
{
    public Guid WorkOrderId { get; set; }
    public int TotalPartsProcessed { get; set; }
    public int SuccessfulConsumptions { get; set; }
    public int FailedConsumptions { get; set; }
    public DateTime ConsumptionDate { get; set; }
    public List<PartConsumptionResultDto> ConsumedParts { get; set; } = new();
    public List<PartConsumptionFailure> ConsumptionFailures { get; set; } = new();
}

public class PartConsumptionDto
{
    public Guid PartId { get; set; }
    public decimal QuantityConsumed { get; set; }
}

public class PartConsumptionResultDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityConsumed { get; set; }
    public decimal TotalQuantityUsed { get; set; }
    public DateTime ConsumptionDate { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class PartConsumptionFailure
{
    public Guid PartId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Parts Return DTOs
public class MaintenancePartsReturnResult
{
    public Guid WorkOrderId { get; set; }
    public int TotalPartsProcessed { get; set; }
    public int SuccessfulReturns { get; set; }
    public int FailedReturns { get; set; }
    public DateTime ReturnDate { get; set; }
    public List<PartReturnResultDto> ReturnedParts { get; set; } = new();
    public List<PartReturnFailure> ReturnFailures { get; set; } = new();
}

public class PartReturnDto
{
    public Guid PartId { get; set; }
    public decimal QuantityReturned { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
}

public class PartReturnResultDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal TotalQuantityReturned { get; set; }
    public DateTime ReturnDate { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
}

public class PartReturnFailure
{
    public Guid PartId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Work Order Parts Status DTOs
public class WorkOrderPartsStatusDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public int TotalParts { get; set; }
    public int AllocatedParts { get; set; }
    public int ConsumedParts { get; set; }
    public int ReturnedParts { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public List<WorkOrderPartStatusDto> Parts { get; set; } = new();
}

public class WorkOrderPartStatusDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal QuantityReturned { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public Guid? AllocationId { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? Notes { get; set; }
}

// Parts Availability DTOs
public class PartsAvailabilityCheckResult
{
    public Guid WorkOrderId { get; set; }
    public bool AllPartsAvailable { get; set; }
    public int TotalParts { get; set; }
    public int AvailableParts { get; set; }
    public int UnavailableParts { get; set; }
    public DateTime CheckDate { get; set; }
    public List<PartAvailabilityDto> PartAvailability { get; set; } = new();
}

public class PartAvailabilityDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public decimal AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public string? AvailabilityIssue { get; set; }
}

// Work Order Lifecycle DTOs
public class WorkOrderStartResult
{
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTime StartedAt { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int PartsAllocated { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<PartAvailabilityDto> PartsAvailabilityIssues { get; set; } = new();
    public List<PartAllocationFailure> AllocationFailures { get; set; } = new();
}

public class CompleteWorkOrderDto
{
    public Guid WorkOrderId { get; set; }
    public string? CompletionNotes { get; set; }
    public double? ActualHours { get; set; }
    public decimal? ActualCost { get; set; }
    public string? PartsUsed { get; set; }
    public string? WorkPerformed { get; set; }
    public string? FailureCode { get; set; }
    public string? CauseCode { get; set; }
    public string? ActionCode { get; set; }
    public string? AssetStatusUpdate { get; set; }
    public List<PartConsumptionDto> PartsConsumed { get; set; } = new();
    public List<PartReturnDto> PartsReturned { get; set; } = new();
}

public class WorkOrderCompletionResult
{
    public Guid WorkOrderId { get; set; }
    public Guid CompletedById { get; set; }
    public DateTime CompletionDate { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int PartsConsumed { get; set; }
    public int PartsReturned { get; set; }
    public decimal TotalPartsCost { get; set; }
    public decimal TotalCost { get; set; }
    public double TotalHours { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<PartConsumptionFailure> ConsumptionFailures { get; set; } = new();
    public List<PartReturnFailure> ReturnFailures { get; set; } = new();
    public QualityValidationResult? QualityValidationResult { get; set; }
}

#region Quality Control DTOs

public class QualityValidationResult
{
    public Guid WorkOrderId { get; set; }
    public bool CanComplete { get; set; }
    public DateTime ValidationDate { get; set; }
    public Guid? ValidatedById { get; set; }
    public string? ValidatorName { get; set; }
    public string? OverallResult { get; set; } // Pass, Fail, ConditionalPass
    public string? ValidationNotes { get; set; }
    public bool RequiresInspectionOfficerApproval { get; set; }
    public List<string> ValidationMessages { get; set; } = new();
    public List<string> ValidationFailures { get; set; } = new();
    public List<RequiredInspectionDto> RequiredInspections { get; set; } = new();
    public List<QualityChecklistResultDto>? ChecklistResults { get; set; }
}

public class RequiredInspectionDto
{
    public Guid InspectionTemplateId { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public string InspectionName { get; set; } = string.Empty;
    public bool IsRegulatory { get; set; }
    public string? Description { get; set; }
}

public class QualityChecklistItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class QualityChecklistResultDto
{
    public string ItemId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty; // Pass, Fail, N/A
    public string? Comments { get; set; }
    public Guid? CheckedById { get; set; }
    public DateTime? CheckedDate { get; set; }
}

public class RecordQualityValidationDto
{
    public string OverallResult { get; set; } = string.Empty; // Pass, Fail, ConditionalPass
    public string? ValidationNotes { get; set; }
    public List<QualityChecklistResultDto>? ChecklistResults { get; set; }
}

#endregion

#region Maintenance Setup DTOs

// Maintenance Types DTOs
public class MaintenanceTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty; // Internal, External, Onsite, Offsite
    public string Priority { get; set; } = string.Empty;
    public decimal EstimatedDuration { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresDowntime { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = string.Empty;
    public string SafetyRequirements { get; set; } = string.Empty;
    public string ToolsRequired { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateMaintenanceTypeDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string Location { get; set; } = "Internal"; // Internal, External, Onsite, Offsite
    
    [Required, StringLength(20)]
    public string Priority { get; set; } = string.Empty;
    
    [Range(0.1, 24)]
    public decimal EstimatedDuration { get; set; } = 1.0m;
    
    public bool IsActive { get; set; } = true;
    public bool RequiresDowntime { get; set; } = false;
    
    [StringLength(10)]
    public string Color { get; set; } = "#3b82f6";
    
    [StringLength(50)]
    public string Icon { get; set; } = "wrench";
    
    [StringLength(50)]
    public string Frequency { get; set; } = "As Needed";
    
    [StringLength(50)]
    public string SkillLevel { get; set; } = "Basic";
    
    [StringLength(1000)]
    public string SafetyRequirements { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string ToolsRequired { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

public class UpdateMaintenanceTypeDto : CreateMaintenanceTypeDto
{
}

public class MaintenanceTypeFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
}

// Priority Levels DTOs
public class PriorityLevelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsActive { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int ResponseTime { get; set; }
    public int EscalationTime { get; set; }
    public bool RequiresApproval { get; set; }
    public string NotificationRules { get; set; } = string.Empty;
    public int SlaHours { get; set; }
    public bool AutoAssign { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePriorityLevelDto
{
    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;
    
    [Required, StringLength(10)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Range(1, 10)]
    public int Level { get; set; } = 1;
    
    public bool IsActive { get; set; } = true;
    
    [StringLength(10)]
    public string Color { get; set; } = "#3b82f6";
    
    [StringLength(50)]
    public string Icon { get; set; } = "alert-triangle";
    
    [Range(1, 43200)] // Up to 30 days in minutes
    public int ResponseTime { get; set; } = 60;
    
    [Range(1, 43200)]
    public int EscalationTime { get; set; } = 120;
    
    public bool RequiresApproval { get; set; } = false;
    
    [StringLength(1000)]
    public string NotificationRules { get; set; } = string.Empty;
    
    [Range(1, 8760)] // Up to 1 year in hours
    public int SlaHours { get; set; } = 24;
    
    public bool AutoAssign { get; set; } = false;
}

public class UpdatePriorityLevelDto : CreatePriorityLevelDto
{
}

public class PriorityLevelFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public bool? IsActive { get; set; }
    public int? Level { get; set; }
}

// Inspection Templates DTOs
public class InspectionTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int EstimatedDuration { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresSignature { get; set; }
    public bool AllowPhotos { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public List<string> AssetTypes { get; set; } = new();
    public List<string> InspectorRoles { get; set; } = new();
    public List<InspectionChecklistItemDto> ChecklistItems { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class InspectionChecklistItemDto
{
    public Guid Id { get; set; }
    public string Item { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // checklist, text, number, measurement
    public bool Required { get; set; }
    public int Order { get; set; }
}

public class CreateInspectionTemplateDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;
    
    [Required, StringLength(50)]
    public string Frequency { get; set; } = string.Empty;
    
    [Range(1, 1440)] // Up to 24 hours in minutes
    public int EstimatedDuration { get; set; } = 60;
    
    public bool IsActive { get; set; } = true;
    public bool RequiresSignature { get; set; } = false;
    public bool AllowPhotos { get; set; } = false;
    
    [StringLength(10)]
    public string Version { get; set; } = "1.0";
    
    [Required, StringLength(20)]
    public string Priority { get; set; } = "Medium";
    
    public List<string> AssetTypes { get; set; } = new();
    public List<string> InspectorRoles { get; set; } = new();
    public List<CreateInspectionChecklistItemDto> ChecklistItems { get; set; } = new();
}

public class CreateInspectionChecklistItemDto
{
    [Required, StringLength(200)]
    public string Item { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string Type { get; set; } = "checklist";
    
    public bool Required { get; set; } = false;
    public int Order { get; set; }
}

public class UpdateInspectionTemplateDto : CreateInspectionTemplateDto
{
}

public class InspectionTemplateFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? Frequency { get; set; }
    public bool? IsActive { get; set; }
}

// Maintenance Schedules DTOs
public class MaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string MaintenanceTypeName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int FrequencyValue { get; set; }
    public string FrequencyUnit { get; set; } = string.Empty;
    public int FrequencyInterval { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? NextDue { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime NextScheduledDate { get; set; }
    public DateTime? LastCompleted { get; set; }
    public DateTime? LastCompletedDate { get; set; }
    public int EstimatedDuration { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string AssignedTechnicianName { get; set; } = string.Empty;
    public Guid? AssignedTeamId { get; set; }
    public string AssignedTeam { get; set; } = string.Empty;
    public string AssignedTeamName { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string SafetyNotes { get; set; } = string.Empty;
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> RequiredTools { get; set; } = new();
    public List<string> RequiredParts { get; set; } = new();
    public bool IsActive { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysUntilDue { get; set; }
    public decimal CompliancePercentage { get; set; }
    public bool AutoCreate { get; set; }
    public bool AutoGenerateWorkOrders { get; set; }
    public int LeadTime { get; set; }
    public int? AdvanceNotificationDays { get; set; }
    public string? NotificationRecipients { get; set; }
    public int MaxDelayDays { get; set; }
    public int CompletedCount { get; set; }
    public int CompletedWorkOrdersCount { get; set; }
    public int OverdueCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    
    // Additional properties for mapping profile compatibility
    public MaintenanceAssetDto? Asset { get; set; }
    public TechnicianDto? DefaultTechnician { get; set; }
    public TechnicianTeamDto? DefaultTeam { get; set; }
    public PriorityLevelDto? PriorityLevel { get; set; }
}

public class CreateMaintenanceScheduleDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public Guid AssetId { get; set; }
    
    [Required]
    public Guid MaintenanceTypeId { get; set; }
    
    [Required, StringLength(50)]
    public string MaintenanceType { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string Priority { get; set; } = string.Empty;
    
    [Required, StringLength(50)]
    public string Frequency { get; set; } = string.Empty;
    
    public int FrequencyValue { get; set; } = 1;
    
    [Required, StringLength(20)]
    public string FrequencyUnit { get; set; } = string.Empty;
    
    [Range(1, 365)]
    public int FrequencyInterval { get; set; } = 1;
    
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime NextDueDate { get; set; } = DateTime.Today.AddDays(30);
    
    [Range(1, 1440)]
    public int EstimatedDuration { get; set; } = 60;
    
    public decimal EstimatedHours { get; set; } = 4;
    public decimal EstimatedCost { get; set; } = 0;
    
    [StringLength(100)]
    public string AssignedTeam { get; set; } = string.Empty;
    
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    
    [StringLength(100)]
    public string AssetCategory { get; set; } = string.Empty;
    
    [StringLength(2000)]
    public string Instructions { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string SafetyNotes { get; set; } = string.Empty;
    
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> RequiredTools { get; set; } = new();
    public List<string> RequiredParts { get; set; } = new();
    
    public bool IsActive { get; set; } = true;
    public bool AutoCreate { get; set; } = true;
    public bool AutoGenerateWorkOrders { get; set; } = true;
    
    [Range(0, 365)]
    public int LeadTime { get; set; } = 5;
    
    public int? AdvanceNotificationDays { get; set; } = 7;
    
    [StringLength(500)]
    public string? NotificationRecipients { get; set; }
    
    [Range(0, 30)]
    public int MaxDelayDays { get; set; } = 3;
    
    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

public class UpdateMaintenanceScheduleDto : CreateMaintenanceScheduleDto
{
}

public class MaintenanceScheduleFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public string? Priority { get; set; }
    public string? Frequency { get; set; }
    public bool? IsActive { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public bool? IsOverdue { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
}

// Technical Skills DTOs
public class TechnicalSkillDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string> Certifications { get; set; } = new();
    public int EstimatedLearningHours { get; set; }
    public string Complexity { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> ToolsRequired { get; set; } = new();
    public string SafetyRequirements { get; set; } = string.Empty;
    public List<string> CompetencyAreas { get; set; } = new();
    public List<string> RelatedMaintenanceTypes { get; set; } = new();
    public int TechniciansCount { get; set; }
    public decimal AverageRating { get; set; }
    public bool IsFromHRModule { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    
    // Additional property for mapping profile compatibility
    public int UserCount { get; set; }
}

public class CreateTechnicalSkillDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string SkillLevel { get; set; } = "Basic";
    
    public bool IsActive { get; set; } = true;
    public List<string> Prerequisites { get; set; } = new();
    public List<string> Certifications { get; set; } = new();
    
    [Range(1, 1000)]
    public int EstimatedLearningHours { get; set; } = 40;
    
    [Required, StringLength(20)]
    public string Complexity { get; set; } = "Low";
    
    [Required, StringLength(20)]
    public string RiskLevel { get; set; } = "Low";
    
    public List<string> ToolsRequired { get; set; } = new();
    
    [StringLength(1000)]
    public string SafetyRequirements { get; set; } = string.Empty;
    
    public List<string> CompetencyAreas { get; set; } = new();
    public List<string> RelatedMaintenanceTypes { get; set; } = new();
}

public class UpdateTechnicalSkillDto : CreateTechnicalSkillDto
{
}

// TechnicalSkillFilterDto - duplicate removed, using definition from above

// Safety Protocols DTOs
public class SafetyProtocolDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsRegulatory { get; set; }
    public string Version { get; set; } = string.Empty;
    public string DocumentVersion { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public string ReviewFrequency { get; set; } = string.Empty;
    public int ReviewFrequencyMonths { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string ReviewedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime? ApprovalDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<string> ApplicableAreas { get; set; } = new();
    public List<string> ApplicableEnvironments { get; set; } = new();
    public List<string> RequiredTraining { get; set; } = new();
    public List<string> RequiredCertifications { get; set; } = new();
    public List<string> Procedures { get; set; } = new();
    public List<string> EmergencyProcedures { get; set; } = new();
    public List<string> ComplianceCheckpoints { get; set; } = new();
    public List<string> RegulatorySources { get; set; } = new();
    public List<string> EquipmentTypes { get; set; } = new();
    public List<string> MaintenanceTypes { get; set; } = new();
    public string MinimumTrainingLevel { get; set; } = string.Empty;
    public int EstimatedTime { get; set; }
    public List<string> Steps { get; set; } = new();
    public List<string> RequiredPPE { get; set; } = new();
    public List<string> EmergencyContacts { get; set; } = new();
    public List<string> Documents { get; set; } = new();
    public int TrainingRecords { get; set; }
    public decimal ComplianceRate { get; set; }
    public decimal ComplianceScore { get; set; }
    public int TotalViolations { get; set; }
    public int IncidentsLastYear { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    
    // Additional properties for controller compatibility
    public string Severity { get; set; } = string.Empty;
    public string RegulatoryStandard { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    public decimal CompliancePercentage { get; set; }
    public List<string> RequiredEquipment { get; set; } = new();
    public List<string> PreventiveMeasures { get; set; } = new();
    public List<string> ApplicableMaintenanceTypes { get; set; } = new();
    public List<string> ApplicableAssetTypes { get; set; } = new();
}

public class CreateSafetyProtocolDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;
    
    [Required, StringLength(20)]
    public string RiskLevel { get; set; } = "Medium";
    
    public bool IsActive { get; set; } = true;
    public bool IsMandatory { get; set; } = false;
    public bool IsRegulatory { get; set; } = false;
    
    [StringLength(10)]
    public string Version { get; set; } = "1.0";
    
    [StringLength(10)]
    public string DocumentVersion { get; set; } = "1.0";
    
    [Required, StringLength(20)]
    public string ReviewFrequency { get; set; } = "Annual";
    
    public int ReviewFrequencyMonths { get; set; } = 12;
    
    public List<string> ApplicableAreas { get; set; } = new();
    public List<string> ApplicableEnvironments { get; set; } = new();
    public List<string> RequiredTraining { get; set; } = new();
    public List<string> RequiredCertifications { get; set; } = new();
    public List<string> Procedures { get; set; } = new();
    public List<string> EmergencyProcedures { get; set; } = new();
    public List<string> ComplianceCheckpoints { get; set; } = new();
    public List<string> RegulatorySources { get; set; } = new();
    public List<string> EquipmentTypes { get; set; } = new();
    public List<string> MaintenanceTypes { get; set; } = new();
    
    [Range(1, 1440)]
    public int EstimatedTime { get; set; } = 15;
    
    [StringLength(50)]
    public string MinimumTrainingLevel { get; set; } = string.Empty;
    
    public List<string> Steps { get; set; } = new();
    public List<string> RequiredPPE { get; set; } = new();
    public List<string> EmergencyContacts { get; set; } = new();
    public List<string> Documents { get; set; } = new();
    
    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    
    [StringLength(100)]
    public string ReviewedBy { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string ApprovedBy { get; set; } = string.Empty;
}

public class UpdateSafetyProtocolDto : CreateSafetyProtocolDto
{
}

// SafetyProtocolFilterDto - duplicate removed, using definition from above

#endregion

public class WorkOrderStatusChangeResult
{
    public Guid WorkOrderId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ChangeDate { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int PartsAllocated { get; set; }
    public int PartsReturned { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
}

public class CompleteTaskDto
{
    public Guid TaskId { get; set; }
    public string? CompletionNotes { get; set; }
    public double ActualHours { get; set; }
}

public class TaskCompletionResult
{
    public Guid TaskId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid CompletedById { get; set; }
    public DateTime CompletionDate { get; set; }
    public bool Success { get; set; }
    public bool AllTasksCompleted { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
}

// Work Order Scheduling DTOs

public class RescheduleWorkOrderDto
{
    public Guid WorkOrderId { get; set; }
    public DateTime? NewStartDate { get; set; }
    public DateTime? NewCompletionDate { get; set; }
    public Guid? NewAssignedTechnicianId { get; set; }
    public Guid? NewAssignedTeamId { get; set; }
    public string RescheduleReason { get; set; } = string.Empty;
    public bool UpdateReservations { get; set; } = true;
}

public class WorkOrderSchedulingResult
{
    public Guid WorkOrderId { get; set; }
    public Guid ScheduledById { get; set; }
    public DateTime SchedulingDate { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledCompletionDate { get; set; }
    public DateTime? PreviousStartDate { get; set; }
    public DateTime? PreviousCompletionDate { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public int PartsReserved { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<ReservedPartDto> ReservedParts { get; set; } = new();
    public List<PartReservationFailure> ReservationFailures { get; set; } = new();
    public List<PartAvailabilityDto> PartsAvailabilityIssues { get; set; } = new();
    public List<WorkOrderConflictDto> ResourceConflicts { get; set; } = new();
}

public class WorkOrderForSchedulingDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public int RequiredParts { get; set; }
    public bool AllPartsAvailable { get; set; }
    public int UnavailableParts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }
}

public class ReservedPartDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityReserved { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime ReservedAt { get; set; }
}

public class PartReservationFailure
{
    public Guid PartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class TechnicianAvailabilityResult
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAvailable { get; set; }
    public double AvailableHours { get; set; }
    public double BookedHours { get; set; }
    public List<WorkOrderConflictDto> ConflictingWorkOrders { get; set; } = new();
}

public class WorkOrderConflictDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public double EstimatedHours { get; set; }
}

// Inventory Integration DTOs
public class InventoryItemSummaryDto
{
    public Guid Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal StandardCost { get; set; }
    public decimal AverageCost { get; set; }
    public bool IsSerialTracked { get; set; }
    public bool IsLotTracked { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? PrimarySupplier { get; set; }
    public int LeadTimeDays { get; set; }
}

#endregion

#region Asset Management DTOs

// MaintenanceAsset DTOs
public class MaintenanceAssetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetCategoryId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public string? WarrantyProvider { get; set; }
    public Guid? ParentAssetId { get; set; }
    public double? OperatingHours { get; set; }
    public DateTime? LastOperatingHoursUpdate { get; set; }
    public double? Mileage { get; set; }
    public DateTime? LastMileageUpdate { get; set; }

    // Navigation properties
    public MaintenanceAssetCategoryDto? AssetCategory { get; set; }
    public MaintenanceAssetDto? ParentAsset { get; set; }
    public ICollection<MaintenanceAssetDto> ChildAssets { get; set; } = new List<MaintenanceAssetDto>();

    // Additional properties
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Additional properties for mobile compatibility
    public string? AssetTag { get; set; }
    public string? QrCode { get; set; }
    public string? AssetType { get; set; }
}

public class MaintenanceAssetListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? AssetType { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string? Location { get; set; }
    public decimal? CurrentValue { get; set; }
    public int ActiveWorkOrdersCount { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public DateTime? WarrantyStartDate { get; set; }
    public string? SerialNumber { get; set; }
}

public class CreateMaintenanceAssetDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string AssetNumber { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetCategoryId { get; set; }

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(50)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active";

    [Required]
    [StringLength(20)]
    public string Criticality { get; set; } = "Medium";

    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }

    [StringLength(200)]
    public string? WarrantyProvider { get; set; }

    public Guid? ParentAssetId { get; set; }
    public double? OperatingHours { get; set; }
    public double? Mileage { get; set; }
    public string? Specifications { get; set; }
    public string? DocumentLinks { get; set; }
    public string? Images { get; set; }
}

public class UpdateMaintenanceAssetDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetCategoryId { get; set; }

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(50)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Criticality { get; set; } = string.Empty;

    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }

    [StringLength(200)]
    public string? WarrantyProvider { get; set; }

    public Guid? ParentAssetId { get; set; }
    public string? Specifications { get; set; }
    public string? DocumentLinks { get; set; }
    public string? Images { get; set; }
}

// MaintenanceAssetCategory DTOs
public class MaintenanceAssetCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public int AssetCount { get; set; }
    
    // Asset Type Classification
    public string? AssetType { get; set; }
    
    // Parent-child relationship
    public Guid? ParentCategoryId { get; set; }
    public MaintenanceAssetCategoryDto? ParentCategory { get; set; }
    public ICollection<MaintenanceAssetCategoryDto> ChildCategories { get; set; } = new List<MaintenanceAssetCategoryDto>();
    
    // Maintenance Schedule Configuration
    public string MaintenanceScheduleType { get; set; } = "single";
    
    // Primary Maintenance Criteria
    public string MaintenanceType { get; set; } = "Time";
    public string? MaintenanceFrequency { get; set; }
    public double? MaintenanceValue { get; set; }
    public string? MaintenanceUnit { get; set; }
    
    // Secondary Maintenance Criteria
    public string? SecondaryMaintenanceType { get; set; }
    public string? SecondaryMaintenanceFrequency { get; set; }
    public double? SecondaryMaintenanceValue { get; set; }
    public string? SecondaryMaintenanceUnit { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateMaintenanceAssetCategoryDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    
    // Asset Type Classification
    [StringLength(50)]
    public string? AssetType { get; set; }
    
    // Parent-child relationship
    public Guid? ParentCategoryId { get; set; }
    
    // Maintenance Schedule Configuration
    [StringLength(20)]
    public string MaintenanceScheduleType { get; set; } = "single";
    
    // Primary Maintenance Criteria
    [StringLength(20)]
    public string MaintenanceType { get; set; } = "Time";
    
    [StringLength(50)]
    public string? MaintenanceFrequency { get; set; }
    
    public double? MaintenanceValue { get; set; }
    
    [StringLength(20)]
    public string? MaintenanceUnit { get; set; }
    
    // Secondary Maintenance Criteria
    [StringLength(20)]
    public string? SecondaryMaintenanceType { get; set; }
    
    [StringLength(50)]
    public string? SecondaryMaintenanceFrequency { get; set; }
    
    public double? SecondaryMaintenanceValue { get; set; }
    
    [StringLength(20)]
    public string? SecondaryMaintenanceUnit { get; set; }
}

public class UpdateMaintenanceAssetCategoryDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    
    // Asset Type Classification
    [StringLength(50)]
    public string? AssetType { get; set; }
    
    // Parent-child relationship
    public Guid? ParentCategoryId { get; set; }
    
    // Maintenance Schedule Configuration
    [StringLength(20)]
    public string MaintenanceScheduleType { get; set; } = "single";
    
    // Primary Maintenance Criteria
    [StringLength(20)]
    public string MaintenanceType { get; set; } = "Time";
    
    [StringLength(50)]
    public string? MaintenanceFrequency { get; set; }
    
    public double? MaintenanceValue { get; set; }
    
    [StringLength(20)]
    public string? MaintenanceUnit { get; set; }
    
    // Secondary Maintenance Criteria
    [StringLength(20)]
    public string? SecondaryMaintenanceType { get; set; }
    
    [StringLength(50)]
    public string? SecondaryMaintenanceFrequency { get; set; }
    
    public double? SecondaryMaintenanceValue { get; set; }
    
    [StringLength(20)]
    public string? SecondaryMaintenanceUnit { get; set; }
}

// Filter DTOs - PriorityLevelFilterDto duplicate removed, using definition from above

public class WorkOrderTypeFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public bool? IsActive { get; set; }
    public bool? RequiresApproval { get; set; }
}

#endregion

#region Work Order DTOs

public class WorkOrderDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? JobCardId { get; set; }
    public string? JobCardNumber { get; set; }
    public Guid AssetId { get; set; }
    public Guid WorkOrderTypeId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public Guid PriorityLevelId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string MaintenanceLocation { get; set; } = "Internal";
    public string Priority { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RequiredSkills { get; set; } = string.Empty;
    public string SafetyNotes { get; set; } = string.Empty;
    public string AssignedTechnicianName { get; set; } = string.Empty;
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public Guid? RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? CompletionNotes { get; set; }
    public string? FailureCode { get; set; }
    public string? CauseCode { get; set; }
    public string? ActionCode { get; set; }
    public string? SafetyRequirements { get; set; }
    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }
    public Guid? ParentWorkOrderId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public bool IsRecurring { get; set; }

    // Navigation properties
    public MaintenanceAssetDto? Asset { get; set; }
    public WorkOrderTypeDto? WorkOrderType { get; set; }
    public MaintenanceTypeDto? MaintenanceType { get; set; }
    public PriorityLevelDto? PriorityLevel { get; set; }
    public EmployeeDto? AssignedTechnician { get; set; }
    public TechnicianTeamDto? AssignedTeam { get; set; }
    public EmployeeDto? RequestedBy { get; set; }
    public EmployeeDto? ApprovedBy { get; set; }
    public WorkOrderDto? ParentWorkOrder { get; set; }
    public ICollection<WorkOrderTaskDto> Tasks { get; set; } = new List<WorkOrderTaskDto>();
    public ICollection<WorkOrderPartDto> Parts { get; set; } = new List<WorkOrderPartDto>();
    public ICollection<WorkOrderLaborDto> Labor { get; set; } = new List<WorkOrderLaborDto>();
    public ICollection<WorkOrderToolDto> Tools { get; set; } = new List<WorkOrderToolDto>();
    public ICollection<WorkOrderCommentDto> Comments { get; set; } = new List<WorkOrderCommentDto>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Additional properties for mobile and analytics
    public DateTime? DueDate { get; set; }
    public double? EstimatedDuration { get; set; }
}

public class WorkOrderListDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? JobCardId { get; set; }
    public string? JobCardNumber { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid WorkOrderTypeId { get; set; }
    public string WorkOrderTypeName { get; set; } = string.Empty;
    public Guid MaintenanceTypeId { get; set; }
    public string MaintenanceTypeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MaintenanceLocation { get; set; } = "Internal";
    public string Priority { get; set; } = string.Empty;
    public string PriorityName { get; set; } = string.Empty;
    public Guid PriorityLevelId { get; set; }
    public int PriorityLevel { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public string? AssignedTeamName { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public bool IsOverdue { get; set; }
    public int TasksCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public double CompletionPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CreatedDate { get; set; }
    
    // Additional properties for mobile and analytics
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public double? EstimatedDuration { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? WorkOrderSource { get; set; }
}

public class CreateWorkOrderDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid WorkOrderTypeId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    public string Type { get; set; } = string.Empty;
    public string WorkOrderType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string MaintenanceLocation { get; set; } = "Internal";
    
    public string Instructions { get; set; } = string.Empty;
    public string SafetyNotes { get; set; } = string.Empty;
    public string RequiredSkills { get; set; } = string.Empty;
    public bool IsEmergency { get; set; }

    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }

    public decimal EstimatedCost { get; set; }
    public double EstimatedHours { get; set; }

    [StringLength(500)]
    public string? SafetyRequirements { get; set; }

    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }

    public Guid? ParentWorkOrderId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public Guid? JobCardId { get; set; }
    
    // Custom field values (JSON serialized)
    public Dictionary<string, object>? CustomFieldValues { get; set; }
}

public class UpdateWorkOrderDto : IValidatableObject
{
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public string Instructions { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string? MaintenanceLocation { get; set; }

    public Guid? WorkOrderTypeId { get; set; }

    public Guid? MaintenanceTypeId { get; set; }

    public Guid? PriorityLevelId { get; set; }

    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }

    public decimal EstimatedCost { get; set; }
    public double EstimatedHours { get; set; }

    [StringLength(500)]
    public string? SafetyRequirements { get; set; }

    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Ensure empty GUIDs (Guid.Empty) are treated as null
        if (WorkOrderTypeId.HasValue && WorkOrderTypeId.Value == Guid.Empty)
            WorkOrderTypeId = null;
        
        if (MaintenanceTypeId.HasValue && MaintenanceTypeId.Value == Guid.Empty)
            MaintenanceTypeId = null;
        
        if (PriorityLevelId.HasValue && PriorityLevelId.Value == Guid.Empty)
            PriorityLevelId = null;
        
        if (AssignedTechnicianId.HasValue && AssignedTechnicianId.Value == Guid.Empty)
            AssignedTechnicianId = null;
        
        if (AssignedTeamId.HasValue && AssignedTeamId.Value == Guid.Empty)
            AssignedTeamId = null;

        return Enumerable.Empty<ValidationResult>();
    }
}

public class UpdateWorkOrderStatusRequest
{
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class ApproveWorkOrderRequest
{
    [StringLength(1000)]
    public string? Notes { get; set; }
}

// Duplicate CompleteWorkOrderDto removed - using the more comprehensive version above

public class WorkOrderFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public Guid? AssetId { get; set; }
    public string? Status { get; set; }
    public Guid? WorkOrderTypeId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public Guid? PriorityLevelId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsOverdue { get; set; }
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

// Work Order Type DTOs
public class WorkOrderTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
    public int DefaultPriority { get; set; }
}

public class CreateWorkOrderTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; }

    [Range(1, 4)]
    public int DefaultPriority { get; set; } = 3;
}

public class UpdateWorkOrderTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; }

    [Range(1, 4)]
    public int DefaultPriority { get; set; } = 3;
}

// Maintenance Type DTOs - duplicate removed, using original definitions from above

// Priority Level DTOs - duplicate removed, using original definitions from above

#endregion

#region Work Order Detail DTOs

// Work Order Task DTOs
public class WorkOrderTaskDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionNotes { get; set; }
    public bool IsRequired { get; set; }

    public EmployeeDto? AssignedTechnician { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateWorkOrderTaskDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    [StringLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int Sequence { get; set; } = 1;
    public double EstimatedHours { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public bool IsRequired { get; set; } = true;
}

public class UpdateWorkOrderTaskDto
{
    [Required]
    [StringLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public bool IsRequired { get; set; }
}

// Work Order Part DTOs
public class WorkOrderPartDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    
    // Inventory Integration
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Quantities
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal QuantityReturned { get; set; }
    
    // Costing
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    // Location and tracking
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public string? WarehouseLocationCode { get; set; }
    public string? WarehouseLocationName { get; set; }
    
    // Status and allocation tracking
    public string Status { get; set; } = string.Empty;
    public Guid? AllocationId { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? PickedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? Notes { get; set; }
    
    // Inventory item details
    public InventoryItemSummaryDto? InventoryItem { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateWorkOrderPartDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal QuantityRequired { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }
    
    public Guid? WarehouseLocationId { get; set; }
    
    [StringLength(100)]
    public string? SerialNumber { get; set; }
    
    [StringLength(100)]
    public string? LotNumber { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateWorkOrderPartDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal QuantityRequired { get; set; }

    [Range(0, double.MaxValue)]
    public decimal QuantityUsed { get; set; }

    [Range(0, double.MaxValue)]
    public decimal QuantityReturned { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    public Guid? WarehouseLocationId { get; set; }
    
    [StringLength(100)]
    public string? SerialNumber { get; set; }
    
    [StringLength(100)]
    public string? LotNumber { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

// Work Order Labor DTOs
public class WorkOrderLaborDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public string LaborType { get; set; } = string.Empty;

    public EmployeeDto? Technician { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateWorkOrderLaborDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [Range(0, double.MaxValue)]
    public decimal HourlyRate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string LaborType { get; set; } = "Regular";
}

public class UpdateWorkOrderLaborDto
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Hours { get; set; }

    [Range(0, double.MaxValue)]
    public decimal HourlyRate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string LaborType { get; set; } = string.Empty;
}

// Work Order Comment DTOs
public class WorkOrderCommentDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string CommentType { get; set; } = string.Empty;
    public bool IsInternal { get; set; }

    public EmployeeDto? Employee { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateWorkOrderCommentDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Comment { get; set; } = string.Empty;

    [StringLength(20)]
    public string CommentType { get; set; } = "General";

    public bool IsInternal { get; set; } = true;
}

/// <summary>
/// Date period DTO
/// </summary>
public class DatePeriodDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Period { get; set; } = string.Empty;
}


#endregion

#region Analytics and Reporting DTOs

public class AssetMetricsDto
{
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int MaintenanceAssets { get; set; }
    public int RetiredAssets { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageValue { get; set; }
    public Dictionary<string, int> AssetsByCategory { get; set; } = new();
    public Dictionary<string, int> AssetsByStatus { get; set; } = new();
    public Dictionary<string, int> AssetsByCriticality { get; set; } = new();
    
    // Additional properties for analytics
    public int CriticalAssets { get; set; }
    public int AssetsRequiringMaintenance { get; set; }
}

public class CategoryStatisticsDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageValue { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int OverdueMaintenanceCount { get; set; }
}


public class MaintenanceDashboardDto
{
    public AssetMetricsDto AssetMetrics { get; set; } = new();
    public WorkOrderMetricsDto WorkOrderMetrics { get; set; } = new();
    public IEnumerable<WorkOrderListDto> RecentWorkOrders { get; set; } = new List<WorkOrderListDto>();
    public IEnumerable<WorkOrderListDto> OverdueWorkOrders { get; set; } = new List<WorkOrderListDto>();
    public IEnumerable<MaintenanceAssetListDto> AssetsRequiringMaintenance { get; set; } = new List<MaintenanceAssetListDto>();
    public IEnumerable<AssetDowntimeDto> ActiveDowntime { get; set; } = new List<AssetDowntimeDto>();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    // Additional properties for MaintenanceAnalyticsService compatibility
    public DashboardSummaryDto Summary { get; set; } = new();
    public Dictionary<string, int> WorkOrdersByStatus { get; set; } = new();
    public Dictionary<string, int> WorkOrdersByPriority { get; set; } = new();
    public Dictionary<string, int> AssetsByStatus { get; set; } = new();
    public MaintenanceKPIsDto MaintenanceKPIs { get; set; } = new();
    public List<MaintenanceScheduleDto> UpcomingMaintenance { get; set; } = new();
    public List<MaintenanceAlertDto> RecentAlerts { get; set; } = new();
    public List<string> TopIssues { get; set; } = new();
}

public class MaintenanceKPIsDto
{
    public double PlannedMaintenancePercentage { get; set; }
    public double ScheduleCompliance { get; set; }
    public double MeanTimeBetweenFailure { get; set; }
    public double MeanTimeToRepair { get; set; }
    public double OverallEquipmentEffectiveness { get; set; }
    public double MaintenanceCostPercentage { get; set; }
    public double TechnicianUtilization { get; set; }
    public double WorkOrderCompletionRate { get; set; }
    public double PreventiveMaintenanceCompliance { get; set; }
    public double AssetAvailability { get; set; }
    
    // Additional properties for service compatibility
    public double MTTR { get; set; }
    public double MTBF { get; set; }
    public double FirstTimeFixRate { get; set; }
    public decimal MaintenanceCostPerAsset { get; set; }
    public double AverageWorkOrderDuration { get; set; }
    public double PreventiveMaintenanceRatio { get; set; }
}

public class LaborReportDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TotalHours { get; set; }
    public decimal TotalCost { get; set; }
    public int WorkOrdersCompleted { get; set; }
    public double RegularHours { get; set; }
    public double OvertimeHours { get; set; }
    public double EmergencyHours { get; set; }
    public double UtilizationPercentage { get; set; }
}

/// <summary>
/// Dashboard summary DTO
/// </summary>
public class DashboardSummaryDto
{
    public int TotalAssets { get; set; }
    public int TotalWorkOrders { get; set; }
    public int PendingWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public decimal TotalMaintenanceCost { get; set; }
    public double AverageCompletionTime { get; set; }
    public double SystemAvailability { get; set; }
    
    // Additional properties for analytics service
    public int ActiveAssets { get; set; }
    public int CriticalAssets { get; set; }
    public int AssetsRequiringMaintenance { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int TotalTechnicians { get; set; }
    public int AvailableTechnicians { get; set; }
    public int ActiveProtocols { get; set; }
    public decimal ComplianceRate { get; set; }
}


#endregion

#region Maintenance Scheduling DTOs

// MaintenanceScheduleDto and related DTOs - duplicate removed, using original definitions from above

public class ScheduleComplianceReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalSchedules { get; set; }
    public int CompletedOnTime { get; set; }
    public int CompletedLate { get; set; }
    public int Missed { get; set; }
    public double CompliancePercentage { get; set; }
    public Dictionary<string, double> ComplianceByAssetCategory { get; set; } = new();
    public Dictionary<string, double> ComplianceByMaintenanceType { get; set; } = new();
    
    // Additional properties for service compatibility
    public int ActiveSchedules { get; set; }
    public int OverdueSchedules { get; set; }
    public int SchedulesDueToday { get; set; }
    public int SchedulesDueThisWeek { get; set; }
    public decimal OverallCompliancePercentage { get; set; }
    public decimal OnTimeCompletionRate { get; set; }
    public int TotalWorkOrdersGenerated { get; set; }
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public DateTime ReportGeneratedDate { get; set; }
    public List<ScheduleComplianceDetail> ScheduleCompliance { get; set; } = new();
    public List<AssetComplianceDto> AssetCompliance { get; set; } = new();
}

public class ScheduleComplianceDetail
{
    public Guid ScheduleId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string ComplianceStatus { get; set; } = string.Empty;
    public int DaysOverdue { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int WorkOrdersGenerated { get; set; }
    public int CompletedOnTime { get; set; }
    public decimal CompliancePercentage { get; set; }
    public DateTime? NextDueDate { get; set; }
}

public class AssetComplianceDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public int TotalSchedules { get; set; }
    public int CompletedOnTime { get; set; }
    public int CompletedLate { get; set; }
    public int Missed { get; set; }
    public decimal CompliancePercentage { get; set; }
    public string Category { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public int ScheduleCount { get; set; }
    public decimal AverageCompliance { get; set; }
    public int OverdueSchedules { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
}

public class SafetyComplianceRecordDto
{
    public Guid Id { get; set; }
    public Guid SafetyProtocolId { get; set; }
    public Guid ProtocolId => SafetyProtocolId; // Alias for compatibility
    public string SafetyProtocolName { get; set; } = string.Empty;
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public DateTime ComplianceDate { get; set; }
    public DateTime CheckDate => ComplianceDate; // Alias for compatibility
    public string ComplianceStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsCompliant { get; set; }
    public string? ViolationType { get; set; }
    public string? CorrectiveActions { get; set; }
    public DateTime? CorrectiveActionDueDate { get; set; }
    
    // Additional properties for service compatibility
    public List<ComplianceChecklistItemDto>? ChecklistItems { get; set; }
    public List<ComplianceViolationDto>? Violations { get; set; }
    public Guid? InspectorId { get; set; }
    public string? InspectorNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CreatedDate => CreatedAt; // Alias for compatibility
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateSafetyComplianceRecordDto
{
    [Required]
    public Guid SafetyProtocolId { get; set; }
    
    // Alias for compatibility
    public Guid ProtocolId 
    {
        get => SafetyProtocolId;
        set => SafetyProtocolId = value;
    }
    
    [Required]
    public Guid TechnicianId { get; set; }
    
    public Guid? WorkOrderId { get; set; }
    
    [Required]
    public DateTime ComplianceDate { get; set; }
    
    // Alias for compatibility
    public DateTime CheckDate 
    {
        get => ComplianceDate;
        set => ComplianceDate = value;
    }
    
    [Required]
    [MaxLength(20)]
    public string ComplianceStatus { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Notes { get; set; }
    
    public bool IsCompliant { get; set; }
    
    [MaxLength(50)]
    public string? ViolationType { get; set; }
    
    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }
    
    public DateTime? CorrectiveActionDueDate { get; set; }
    
    // Additional properties for service compatibility
    public List<ComplianceChecklistItemDto>? ChecklistItems { get; set; }
    public List<ComplianceViolationDto>? Violations { get; set; }
    public Guid? InspectorId { get; set; }
    
    [MaxLength(2000)]
    public string? InspectorNotes { get; set; }
}

public class UpdateSafetyComplianceRecordDto
{
    [Required]
    [MaxLength(20)]
    public string ComplianceStatus { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Notes { get; set; }
    
    public bool IsCompliant { get; set; }
    
    [MaxLength(50)]
    public string? ViolationType { get; set; }
    
    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }
    
    public DateTime? CorrectiveActionDueDate { get; set; }
    
    // Additional properties for service compatibility
    public List<ComplianceChecklistItemDto>? ChecklistItems { get; set; }
    public List<ComplianceViolationDto>? Violations { get; set; }
    public Guid? InspectorId { get; set; }
    
    [MaxLength(2000)]
    public string? InspectorNotes { get; set; }
}

public class ComplianceChecklistItemDto
{
    public Guid Id { get; set; }
    public string Item { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CompletedBy { get; set; }
}

public class ComplianceViolationDto
{
    public Guid Id { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime IdentifiedDate { get; set; }
    public string? CorrectiveAction { get; set; }
    public DateTime? CorrectiveDueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? IdentifiedBy { get; set; }
}

public class ComplianceReportDto
{
    public DateTime ReportDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalProtocols { get; set; }
    public int TotalChecks { get; set; }
    public int CompliantRecords { get; set; }
    public int CompliantChecks { get; set; }
    public int ViolationRecords { get; set; }
    public int NonCompliantChecks { get; set; }
    public int PartiallyCompliantChecks { get; set; }
    public decimal ComplianceRate { get; set; }
    public Dictionary<string, int> ViolationsByProtocol { get; set; } = new();
    public DateTime GeneratedDate { get; set; }
    public IEnumerable<SafetyProtocolComplianceDto> ProtocolCompliance { get; set; } = new List<SafetyProtocolComplianceDto>();
    public IEnumerable<CategoryComplianceDto> CategoryCompliance { get; set; } = new List<CategoryComplianceDto>();
}

public class SafetyProtocolComplianceDto
{
    public Guid ProtocolId { get; set; }
    public string ProtocolName { get; set; } = string.Empty;
    public string ProtocolTitle { get; set; } = string.Empty;
    public string ProtocolCode { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int CompliantRecords { get; set; }
    public int ViolationRecords { get; set; }
    public int TotalAdherence { get; set; }
    public int TotalViolations { get; set; }
    public decimal ComplianceRate { get; set; }
    public DateTime? LastAuditDate { get; set; }
}

public class CategoryComplianceDto
{
    public string Category { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int CompliantRecords { get; set; }
    public int TotalProtocols { get; set; }
    public int TotalAdherence { get; set; }
    public int TotalViolations { get; set; }
    public decimal ComplianceRate { get; set; }
    
    // Additional properties for controller compatibility
    public int ProtocolCount { get; set; }
    public decimal AverageCompliance { get; set; }
}

public class SafetyAnalyticsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalIncidents { get; set; }
    public int TotalViolations { get; set; }
    public int TotalProtocols { get; set; }
    public int ActiveProtocols { get; set; }
    public int RegulatoryProtocols { get; set; }
    public int ProtocolsDueForReview { get; set; }
    public int ExpiredProtocols { get; set; }
    public int TotalComplianceChecks { get; set; }
    public decimal OverallComplianceRate { get; set; }
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public IEnumerable<TrendDataDto> ComplianceTrends { get; set; } = new List<TrendDataDto>();
    public IEnumerable<ViolationTypeDto> ViolationsByType { get; set; } = new List<ViolationTypeDto>();
    
    // Additional properties for controller compatibility
    public int CriticalViolations { get; set; }
    public Dictionary<string, int> ProtocolsByCategory { get; set; } = new();
    public List<SafetyViolationSummaryDto> RecentViolations { get; set; } = new();
}

public class TrendDataDto
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string Metric { get; set; } = string.Empty;
}

public class ViolationTypeDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class TechnicianAnalyticsDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int CompletedWorkOrders { get; set; }
    public decimal TotalHours { get; set; }
    public decimal UtilizationRate { get; set; }
    public decimal AverageTimePerOrder { get; set; }
    public decimal ComplianceRate { get; set; }
    public IEnumerable<SkillUtilizationDto> SkillUtilization { get; set; } = new List<SkillUtilizationDto>();
    public int TotalTechnicians { get; set; }
    public int ActiveTechnicians { get; set; }
    public int TechniciansWithSkills { get; set; }
    public decimal AverageSkillsPerTechnician { get; set; }
    public Dictionary<string, int> DepartmentBreakdown { get; set; } = new();
    public Dictionary<string, int> SpecializationBreakdown { get; set; } = new();
    public WorkloadAnalysisDto? WorkloadAnalysis { get; set; }
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public int TotalWorkOrders { get; set; }
    public decimal AverageCompletionTime { get; set; }
    public decimal EfficiencyRating { get; set; }
    public decimal TotalHoursWorked { get; set; }
}

public class WorkloadAnalysisDto
{
    public int TotalTechnicians { get; set; }
    public int OverloadedTechnicians { get; set; }
    public int UnderutilizedTechnicians { get; set; }
    public int OptimallyUtilizedTechnicians { get; set; }
    public decimal AverageUtilization { get; set; }
    public decimal MaxWorkloadCapacity { get; set; }
}

public class ApprovalRequestDto
{
    [Required]
    [MaxLength(2000)]
    public string Comments { get; set; } = string.Empty;
    
    [Required]
    public Guid ApproverId { get; set; }
    
    public string ApproverName { get; set; } = string.Empty;
    
    public DateTime ApprovalDate { get; set; } = DateTime.UtcNow;
}

public class CreateProtocolAdherenceDto
{
    [Required]
    public Guid ProtocolId { get; set; }
    
    [Required]
    public Guid TechnicianId { get; set; }
    
    public Guid? WorkOrderId { get; set; }
    
    [Required]
    public DateTime AdherenceDate { get; set; }
    
    [MaxLength(20)]
    public string AdherenceLevel { get; set; } = "Full";
    
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CreateProtocolViolationDto
{
    [Required]
    public Guid ProtocolId { get; set; }
    
    [Required]
    public Guid TechnicianId { get; set; }
    
    public Guid? WorkOrderId { get; set; }
    
    [Required]
    public DateTime ViolationDate { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ViolationType { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }
    
    [MaxLength(2000)]
    public string? CorrectiveAction { get; set; }
    
    public DateTime? CorrectiveActionDueDate { get; set; }
}

public class CreateProtocolTrainingDto
{
    [Required]
    public Guid ProtocolId { get; set; }
    
    [Required]
    public Guid TechnicianId { get; set; }
    
    [Required]
    public DateTime TrainingDate { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TrainingType { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string CompletionStatus { get; set; } = "NotStarted";
    
    public int? Score { get; set; }
    
    public bool CertificationIssued { get; set; } = false;
    
    [MaxLength(200)]
    public string? TrainerName { get; set; }
    
    [MaxLength(2000)]
    public string? Notes { get; set; }
    
    public bool Passed { get; set; }
    
    public DateTime? CertificationExpiry { get; set; }
}

public class ProtocolAdherenceDto
{
    public Guid Id { get; set; }
    public Guid ProtocolId { get; set; }
    public string ProtocolName { get; set; } = string.Empty;
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public DateTime AdherenceDate { get; set; }
    public string AdherenceLevel { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class ProtocolViolationDto
{
    public Guid Id { get; set; }
    public Guid ProtocolId { get; set; }
    public string ProtocolName { get; set; } = string.Empty;
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public DateTime ViolationDate { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CorrectiveActions { get; set; }
    public string? CorrectiveAction { get; set; }
    public DateTime? CorrectiveActionDueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class ProtocolTrainingDto
{
    public Guid Id { get; set; }
    public Guid ProtocolId { get; set; }
    public string ProtocolName { get; set; } = string.Empty;
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime TrainingDate { get; set; }
    public string TrainingType { get; set; } = string.Empty;
    public string CompletionStatus { get; set; } = string.Empty;
    public int? Score { get; set; }
    public bool CertificationIssued { get; set; }
    public string? TrainerName { get; set; }
    public string? Notes { get; set; }
    public bool Passed { get; set; }
    public DateTime? CertificationExpiry { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

#endregion

#region Inspection DTOs

// InspectionTemplateDto and related DTOs - duplicate removed, using original definitions from above

public class AssetInspectionDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid InspectionTemplateId { get; set; }
    public Guid InspectorId { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OverallResult { get; set; }
    public string InspectionData { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RecommendedActions { get; set; }
    public DateTime? NextInspectionDue { get; set; }
    public bool IsRegulatoryRequired { get; set; }
    public string? RegulatoryStandard { get; set; }

    public MaintenanceAssetDto? Asset { get; set; }
    public InspectionTemplateDto? InspectionTemplate { get; set; }
    public string? Inspector { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAssetInspectionDto
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid InspectionTemplateId { get; set; }

    [Required]
    public Guid InspectorId { get; set; }

    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool IsRegulatoryRequired { get; set; }

    [StringLength(100)]
    public string? RegulatoryStandard { get; set; }
}

public class UpdateAssetInspectionDto
{
    [Required]
    public Guid InspectorId { get; set; }

    public DateTime InspectionDate { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool IsRegulatoryRequired { get; set; }

    [StringLength(100)]
    public string? RegulatoryStandard { get; set; }
}

public class CompleteInspectionDto
{
    [Required]
    [StringLength(20)]
    public string OverallResult { get; set; } = string.Empty;

    public string InspectionData { get; set; } = "{}";

    [StringLength(2000)]
    public string? Notes { get; set; }

    [StringLength(2000)]
    public string? RecommendedActions { get; set; }

    public DateTime? NextInspectionDue { get; set; }
}

public class InspectionComplianceReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalInspections { get; set; }
    public int PassedInspections { get; set; }
    public int FailedInspections { get; set; }
    public int OverdueInspections { get; set; }
    public double PassRate { get; set; }
    public Dictionary<string, double> PassRateByAssetCategory { get; set; } = new();
    public Dictionary<string, double> PassRateByInspectionType { get; set; } = new();
}

#endregion

#region Resource Management DTOs

public class TechnicianTeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? TeamLeaderId { get; set; }
    public string Status { get; set; } = string.Empty;

    public string? TeamLeader { get; set; }
    public ICollection<TechnicianTeamMemberDto> Members { get; set; } = new List<TechnicianTeamMemberDto>();

    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTechnicianTeamDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? TeamLeaderId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = "Active";
}

public class UpdateTechnicianTeamDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? TeamLeaderId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
}

public class TechnicianTeamMemberDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    public DateTime? LeftDate { get; set; }
    public bool IsActive { get; set; }

    public TechnicianTeamDto? Team { get; set; }
    public string? Technician { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTechnicianTeamMemberDto
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [StringLength(50)]
    public string Role { get; set; } = "Member";

    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class UpdateTechnicianTeamMemberDto
{
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;

    public DateTime? LeftDate { get; set; }
    public bool IsActive { get; set; }
}


public class CreateTechnicianSkillDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateTechnicianSkillDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; }
}

public class UserTechnicianSkillDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int ProficiencyLevel { get; set; }
    public int Level { get; set; }
    public bool IsCertified { get; set; }
    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }
    public string? CertifyingBody { get; set; }
    public string? CertificationNumber { get; set; }

    public EmployeeDto? Employee { get; set; }
    public TechnicianSkillDto? Skill { get; set; }

    public bool IsExpired => CertificationExpiry.HasValue && CertificationExpiry.Value < DateTime.UtcNow;
    public bool IsExpiringSoon => CertificationExpiry.HasValue && CertificationExpiry.Value < DateTime.UtcNow.AddDays(30);

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateUserTechnicianSkillDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Range(1, 4)]
    public int ProficiencyLevel { get; set; } = 1;

    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }

    [StringLength(200)]
    public string? CertifyingBody { get; set; }

    [StringLength(100)]
    public string? CertificationNumber { get; set; }
}

public class UpdateUserTechnicianSkillDto
{
    [Range(1, 4)]
    public int ProficiencyLevel { get; set; }

    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }

    [StringLength(200)]
    public string? CertifyingBody { get; set; }

    [StringLength(100)]
    public string? CertificationNumber { get; set; }
}

public class TeamPerformanceReportDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int WorkOrdersCompleted { get; set; }
    public double TotalHours { get; set; }
    public decimal TotalCost { get; set; }
    public double AverageCompletionTime { get; set; }
    public double OnTimeCompletionRate { get; set; }
    public Dictionary<Guid, int> MemberWorkOrderCounts { get; set; } = new();
    public Dictionary<Guid, double> MemberHours { get; set; } = new();
}

public class TeamWorkloadReportDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int OpenWorkOrders { get; set; }
    public int AssignedWorkOrders { get; set; }
    public int InProgressWorkOrders { get; set; }
    public double CurrentWorkloadHours { get; set; }
    public double WeeklyCapacityHours { get; set; }
    public double UtilizationPercentage { get; set; }
    public Dictionary<Guid, double> MemberUtilization { get; set; } = new();
}

public class SkillGapAnalysisDto
{
    public Dictionary<string, int> RequiredSkillsCount { get; set; } = new();
    public Dictionary<string, int> AvailableSkillsCount { get; set; } = new();
    public Dictionary<string, int> SkillGaps { get; set; } = new();
    public Dictionary<string, double> AverageProficiencyLevels { get; set; } = new();
    public List<string> SkillsWithShortages { get; set; } = new();
    public List<UserTechnicianSkillDto> ExpiringCertifications { get; set; } = new();
    public List<SkillDeficitDto> SkillDeficits { get; set; } = new();
    public List<OverstaffedSkillDto> OverstaffedSkills { get; set; } = new();
    public List<CriticalSkillDto> CriticalSkills { get; set; } = new();
    public int TotalSkillsAnalyzed { get; set; }
    public DateTime AnalysisDate { get; set; }
}

public class SkillDeficitDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public int AvailableCount { get; set; }
    public int DeficitCount { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal ImpactScore { get; set; }
    
    // Additional properties for controller compatibility
    public int RequiredTechnicians { get; set; }
    public int CurrentTechnicians { get; set; }
    public int Deficit { get; set; }
    public string Priority { get; set; } = "Medium";
}

public class OverstaffedSkillDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public int AvailableCount { get; set; }
    public int ExcessCount { get; set; }
    public string Category { get; set; } = string.Empty;
    
    // Additional properties for controller compatibility
    public int RequiredTechnicians { get; set; }
    public int CurrentTechnicians { get; set; }
    public int Excess { get; set; }
}

public class CriticalSkillDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int TechniciansWithSkill { get; set; }
    public decimal CriticalityScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    
    // Additional properties for controller compatibility
    public int TechnicianCount { get; set; }
    public string MitegationActions { get; set; } = string.Empty;
}

#endregion

#region Asset Downtime DTOs

public class AssetDowntimeDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? DowntimeHours { get; set; }
    public string DowntimeType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal Duration { get; set; }
    public Guid? RelatedWorkOrderId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal EstimatedCostImpact { get; set; }
    public string Status { get; set; } = string.Empty;

    public MaintenanceAssetDto? Asset { get; set; }
    public WorkOrderDto? WorkOrder { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAssetDowntimeDto
{
    [Required]
    public Guid AssetId { get; set; }

    public Guid? WorkOrderId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(50)]
    public string DowntimeType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Priority { get; set; } = string.Empty;

    public Guid? RelatedWorkOrderId { get; set; }

    [Required]
    [StringLength(50)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public decimal EstimatedCostImpact { get; set; }
}

public class UpdateAssetDowntimeDto
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [Required]
    [StringLength(50)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public decimal EstimatedCostImpact { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
}

public class DowntimeAnalyticsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TotalDowntimeHours { get; set; }
    public decimal TotalCostImpact { get; set; }
    public double AverageDowntimePerIncident { get; set; }
    public int TotalIncidents { get; set; }
    public Dictionary<string, double> DowntimeByReason { get; set; } = new();
    public Dictionary<string, double> DowntimeByAsset { get; set; } = new();
    public Dictionary<string, int> IncidentsByReason { get; set; } = new();
    public double OverallAvailabilityPercentage { get; set; }
}

public class AssetDowntimeReportDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public double TotalDowntimeHours { get; set; }
    public decimal TotalCostImpact { get; set; }
    public int IncidentCount { get; set; }
    public double AverageIncidentDuration { get; set; }
    public double AvailabilityPercentage { get; set; }
    public string MostCommonFailureReason { get; set; } = string.Empty;
}

#endregion

#region Staff Schedule and Expense DTOs

public class MaintenanceStaffScheduleDto
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string ScheduleType { get; set; } = "WorkOrder"; // WorkOrder, Available, Training, Leave, Travel
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled
    
    // Work assignment
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public Guid? JobCardId { get; set; }
    public string? JobCardNumber { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    
    // Location information
    public string? WorkLocation { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    
    // Travel information
    public bool RequiresTravel { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int? EstimatedTravelMinutes { get; set; }
    public int? ActualTravelMinutes { get; set; }
    
    // Vehicle/transportation
    public Guid? AssignedVehicleId { get; set; }
    public string? VehicleName { get; set; }
    public string? TransportationType { get; set; }
    
    public string? Notes { get; set; }
    
    // Time tracking
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateMaintenanceStaffScheduleDto
{
    [Required]
    public Guid TechnicianId { get; set; }
    
    [Required]
    public DateTime StartDateTime { get; set; }
    
    [Required]
    public DateTime EndDateTime { get; set; }
    
    [Required]
    [StringLength(50)]
    public string ScheduleType { get; set; } = "WorkOrder";
    
    [StringLength(20)]
    public string Status { get; set; } = "Scheduled";
    
    public Guid? WorkOrderId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? TeamId { get; set; }
    
    [StringLength(200)]
    public string? WorkLocation { get; set; }
    
    [StringLength(200)]
    public string? Address { get; set; }
    
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    
    public bool RequiresTravel { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int? EstimatedTravelMinutes { get; set; }
    
    public Guid? AssignedVehicleId { get; set; }
    
    [StringLength(100)]
    public string? TransportationType { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateMaintenanceStaffScheduleDto
{
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    
    [StringLength(20)]
    public string? Status { get; set; }
    
    [StringLength(200)]
    public string? WorkLocation { get; set; }
    
    [StringLength(200)]
    public string? Address { get; set; }
    
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    
    public bool? RequiresTravel { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int? EstimatedTravelMinutes { get; set; }
    public int? ActualTravelMinutes { get; set; }
    
    public Guid? AssignedVehicleId { get; set; }
    
    [StringLength(100)]
    public string? TransportationType { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
}

public class MaintenanceExpenseDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public Guid? ScheduleId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    
    public string ExpenseType { get; set; } = "Travel"; // Travel, Fuel, Accommodation, Meals, Tools, Parts, Other
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    
    // Mileage tracking
    public decimal? MileageDriven { get; set; }
    public decimal? MileageRate { get; set; }
    
    // Fuel tracking
    public decimal? FuelQuantity { get; set; }
    public decimal? FuelPricePerUnit { get; set; }
    
    // Vehicle tracking
    public Guid? VehicleId { get; set; }
    public string? VehicleName { get; set; }
    
    // Receipt and documentation
    public string? ReceiptPath { get; set; }
    public string? VendorName { get; set; }
    public string? ReferenceNumber { get; set; }
    
    // Approval and reimbursement
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Reimbursed
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovalNotes { get; set; }
    
    public bool IsReimbursable { get; set; }
    public bool IsReimbursed { get; set; }
    public DateTime? ReimbursedDate { get; set; }
    
    // Location information
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateMaintenanceExpenseDto
{
    [Required]
    public Guid WorkOrderId { get; set; }
    
    public Guid? ScheduleId { get; set; }
    public Guid? TechnicianId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string ExpenseType { get; set; } = "Travel";
    
    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }
    
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    
    public decimal? MileageDriven { get; set; }
    public decimal? MileageRate { get; set; }
    
    public decimal? FuelQuantity { get; set; }
    public decimal? FuelPricePerUnit { get; set; }
    
    public Guid? VehicleId { get; set; }
    
    [StringLength(500)]
    public string? ReceiptPath { get; set; }
    
    [StringLength(100)]
    public string? VendorName { get; set; }
    
    [StringLength(50)]
    public string? ReferenceNumber { get; set; }
    
    public bool IsReimbursable { get; set; } = true;
    
    [StringLength(200)]
    public string? Location { get; set; }
    
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class UpdateMaintenanceExpenseDto
{
    [StringLength(50)]
    public string? ExpenseType { get; set; }
    
    [StringLength(200)]
    public string? Description { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? Amount { get; set; }
    
    public DateTime? ExpenseDate { get; set; }
    
    public decimal? MileageDriven { get; set; }
    public decimal? MileageRate { get; set; }
    
    public decimal? FuelQuantity { get; set; }
    public decimal? FuelPricePerUnit { get; set; }
    
    public Guid? VehicleId { get; set; }
    
    [StringLength(500)]
    public string? ReceiptPath { get; set; }
    
    [StringLength(100)]
    public string? VendorName { get; set; }
    
    [StringLength(50)]
    public string? ReferenceNumber { get; set; }
    
    [StringLength(20)]
    public string? Status { get; set; }
    
    [StringLength(1000)]
    public string? ApprovalNotes { get; set; }
    
    public bool? IsReimbursable { get; set; }
    public bool? IsReimbursed { get; set; }
    public DateTime? ReimbursedDate { get; set; }
    
    [StringLength(200)]
    public string? Location { get; set; }
    
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class ApproveExpenseDto
{
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Approved"; // Approved or Rejected
    
    [StringLength(1000)]
    public string? ApprovalNotes { get; set; }
}

#endregion

#region Report Filter DTOs

public class WorkOrderReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
    public string? Status { get; set; }
    public Guid? WorkOrderTypeId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public string Format { get; set; } = "PDF"; // PDF, Excel, CSV
}

public class AssetReportFilterDto
{
    public Guid? AssetCategoryId { get; set; }
    public string? Status { get; set; }
    public string? Criticality { get; set; }
    public string? Location { get; set; }
    public bool IncludeMaintenanceHistory { get; set; }
    public bool IncludeWorkOrders { get; set; }
    public string Format { get; set; } = "PDF";
}

public class ScheduleReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public string? ScheduleType { get; set; }
    public bool ActiveOnly { get; set; } = true;
    public string Format { get; set; } = "PDF";
}

public class DowntimeReportFilterDto
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-1);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public Guid? AssetId { get; set; }
    public string? Reason { get; set; }
    public bool IncludeCostAnalysis { get; set; } = true;
    public string Format { get; set; } = "PDF";
}

public class InspectionReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? InspectionTemplateId { get; set; }
    public string? InspectionType { get; set; }
    public string? OverallResult { get; set; }
    public bool RegulatoryOnly { get; set; }
    public string Format { get; set; } = "PDF";
}

public class CostReportFilterDto
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-12);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public Guid? AssetId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string? CostType { get; set; } // Labor, Parts, Total
    public bool IncludeBudgetComparison { get; set; }
    public string Format { get; set; } = "Excel";
}

#endregion

#region Asset Type DTOs

/// <summary>
/// Asset type information for lists and selections
/// </summary>
public class AssetTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    
    // Configuration flags
    public bool RequiresLocation { get; set; }
    public bool RequiresOperatingHours { get; set; }
    public bool RequiresMileageTracking { get; set; }
    public bool RequiresLicensing { get; set; }
    public bool RequiresInspections { get; set; }
    public bool SupportsHierarchy { get; set; }
    public bool RequiresSpecializedFields { get; set; }
    
    // Maintenance Configuration
    public int DefaultMaintenanceIntervalDays { get; set; }
    public bool RequiresPreventiveMaintenance { get; set; }
    public bool RequiresConditionMonitoring { get; set; }
    
    // Safety and Compliance
    public bool RequiresSafetyChecks { get; set; }
    public bool RequiresLockoutTagout { get; set; }
    public bool RequiresPermits { get; set; }
    
    // Workflow Configuration
    public int DefaultWorkOrderPriority { get; set; }
    public double DefaultEstimatedHours { get; set; }
    public string? DefaultWorkInstructions { get; set; }
    
    // Custom fields configuration
    public string? CustomFieldsConfig { get; set; }
    
    // Asset count for this type
    public int AssetCount { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating new asset types
/// </summary>
public class CreateAssetTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [StringLength(7)] // Hex color code
    public string? Color { get; set; }
    
    [StringLength(50)]
    public string? Icon { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Configuration flags
    public bool RequiresLocation { get; set; } = true;
    public bool RequiresOperatingHours { get; set; } = false;
    public bool RequiresMileageTracking { get; set; } = false;
    public bool RequiresLicensing { get; set; } = false;
    public bool RequiresInspections { get; set; } = false;
    public bool SupportsHierarchy { get; set; } = false;
    public bool RequiresSpecializedFields { get; set; } = false;
    
    // Maintenance Configuration
    public int DefaultMaintenanceIntervalDays { get; set; } = 90;
    public bool RequiresPreventiveMaintenance { get; set; } = true;
    public bool RequiresConditionMonitoring { get; set; } = false;
    
    // Safety and Compliance
    public bool RequiresSafetyChecks { get; set; } = false;
    public bool RequiresLockoutTagout { get; set; } = false;
    public bool RequiresPermits { get; set; } = false;
    
    // Workflow Configuration
    [Range(1, 5)]
    public int DefaultWorkOrderPriority { get; set; } = 3; // 1=Critical, 5=Low
    
    [Range(0.1, 9999.0)]
    public double DefaultEstimatedHours { get; set; } = 2.0;
    
    [StringLength(2000)]
    public string? DefaultWorkInstructions { get; set; }
    
    // Custom Fields Configuration (JSON)
    public string? CustomFieldsConfig { get; set; }
}

/// <summary>
/// DTO for updating asset types
/// </summary>
public class UpdateAssetTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [StringLength(7)] // Hex color code
    public string? Color { get; set; }
    
    [StringLength(50)]
    public string? Icon { get; set; }
    
    public bool IsActive { get; set; }
    
    // Configuration flags
    public bool RequiresLocation { get; set; }
    public bool RequiresOperatingHours { get; set; }
    public bool RequiresMileageTracking { get; set; }
    public bool RequiresLicensing { get; set; }
    public bool RequiresInspections { get; set; }
    public bool SupportsHierarchy { get; set; }
    public bool RequiresSpecializedFields { get; set; }
    
    // Maintenance Configuration
    public int DefaultMaintenanceIntervalDays { get; set; }
    public bool RequiresPreventiveMaintenance { get; set; }
    public bool RequiresConditionMonitoring { get; set; }
    
    // Safety and Compliance
    public bool RequiresSafetyChecks { get; set; }
    public bool RequiresLockoutTagout { get; set; }
    public bool RequiresPermits { get; set; }
    
    // Workflow Configuration
    [Range(1, 5)]
    public int DefaultWorkOrderPriority { get; set; }
    
    [Range(0.1, 9999.0)]
    public double DefaultEstimatedHours { get; set; }
    
    [StringLength(2000)]
    public string? DefaultWorkInstructions { get; set; }
    
    // Custom Fields Configuration (JSON)
    public string? CustomFieldsConfig { get; set; }
}

#endregion

#region Attachment DTOs

/// <summary>
/// Maintenance attachment information
/// </summary>
public class MaintenanceAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public string AttachmentType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTime UploadedDate { get; set; }
    public string UploadedByUserName { get; set; } = string.Empty;
    public bool IsMainImage { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? ThumbnailPath { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public string? DocumentVersion { get; set; }
    public List<AttachmentTagDto> Tags { get; set; } = new List<AttachmentTagDto>();
    public string FileSizeFormatted => FormatFileSize(FileSizeBytes);

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// DTO for creating maintenance attachments
/// </summary>
public class CreateMaintenanceAttachmentDto
{
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string FilePath { get; set; } = string.Empty;
    
    [Required]
    public string ContentType { get; set; } = string.Empty;
    
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    
    [Required]
    public string AttachmentType { get; set; } = string.Empty;
    
    [Required]
    public string EntityType { get; set; } = string.Empty;
    
    [Required]
    public Guid EntityId { get; set; }
    
    public bool IsMainImage { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? ThumbnailPath { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// DTO for updating maintenance attachments
/// </summary>
public class UpdateMaintenanceAttachmentDto
{
    public string? Description { get; set; }
    public bool? IsMainImage { get; set; }
    public string? LocationDescription { get; set; }
    public List<AttachmentTagDto>? Tags { get; set; }
}

/// <summary>
/// Attachment tag information
/// </summary>
public class AttachmentTagDto
{
    public Guid Id { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? TagValue { get; set; }
}

/// <summary>
/// DTO for adding attachment tags
/// </summary>
public class AddAttachmentTagsDto
{
    [Required]
    public List<AttachmentTagDto> Tags { get; set; } = new();
}

/// <summary>
/// Attachment access log information
/// </summary>
public class AttachmentAccessLogDto
{
    public Guid Id { get; set; }
    public Guid AttachmentId { get; set; }
    public Guid AccessedByUserId { get; set; }
    public string AccessedByUserName { get; set; } = string.Empty;
    public DateTime AccessedDate { get; set; }
    public string AccessType { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

#endregion
