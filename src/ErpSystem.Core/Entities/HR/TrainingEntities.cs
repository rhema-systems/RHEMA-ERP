using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Training;

/// <summary>
/// Training program/course offered
/// </summary>
public class TrainingProgram : TenantEntity
{
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Classification
    public TrainingCategory Category { get; set; } // Technical, Soft Skills, Leadership, Compliance
    public TrainingType Type { get; set; } // Internal, External, Online, Workshop
    public TrainingLevel Level { get; set; } // Beginner, Intermediate, Advanced

    // Details
    public int DurationDays { get; set; }
    public int DurationHours { get; set; }
    public string? Prerequisites { get; set; }
    public string? LearningObjectives { get; set; }
    public string? TargetAudience { get; set; }

    // Provider
    public bool IsInternal { get; set; }
    public Guid? InternalTrainerId { get; set; }
    public Employee? InternalTrainer { get; set; }

    public string? ExternalProvider { get; set; }
    public string? ProviderContact { get; set; }

    // Cost
    public decimal CostPerParticipant { get; set; }
    public string Currency { get; set; } = "GHS";
    public bool IncludesAccommodation { get; set; }
    public bool IncludesMeals { get; set; }
    public bool IncludesTransport { get; set; }

    // Certification
    public bool ProvidesCertificate { get; set; }
    public string? CertificateName { get; set; }
    public int? CertificateValidityMonths { get; set; }

    // Capacity
    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }

    // Status
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }

    // Relations
    public ICollection<TrainingSchedule> Schedules { get; set; } = new List<TrainingSchedule>();
    public ICollection<TrainingMaterial> Materials { get; set; } = new List<TrainingMaterial>();
}

/// <summary>
/// Scheduled training session
/// </summary>
public class TrainingSchedule : TenantEntity
{
    public string ScheduleNumber { get; set; } = string.Empty;

    public Guid ProgramId { get; set; }
    public TrainingProgram Program { get; set; } = null!;

    // Schedule Details
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Venue { get; set; }
    public string? VenueAddress { get; set; }
    public string? OnlineLink { get; set; }

    // Timing
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    // Trainer
    public Guid? TrainerId { get; set; }
    public Employee? Trainer { get; set; }
    public string? ExternalTrainerName { get; set; }

    // Capacity
    public int MaxParticipants { get; set; }
    public int CurrentEnrollment { get; set; }
    public int WaitlistCount { get; set; }

    // Registration
    public DateTime RegistrationOpenDate { get; set; }
    public DateTime RegistrationCloseDate { get; set; }

    // Status
    public ScheduleStatus Status { get; set; }

    // Budget
    public decimal TotalBudget { get; set; }
    public decimal ActualCost { get; set; }
    public string? BudgetNotes { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Completion
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }

    // Relations
    public ICollection<TrainingNomination> Nominations { get; set; } = new List<TrainingNomination>();
    public ICollection<TrainingAttendance> Attendance { get; set; } = new List<TrainingAttendance>();
    public ICollection<TrainingEvaluation> Evaluations { get; set; } = new List<TrainingEvaluation>();
}

/// <summary>
/// Employee nomination/application for training
/// </summary>
public class TrainingNomination : TenantEntity
{
    public string NominationNumber { get; set; } = string.Empty;

    public Guid ScheduleId { get; set; }
    public TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // Nomination Details
    public NominationType Type { get; set; } // Self, Supervisor, HR, Management
    public Guid? NominatedById { get; set; }
    public Employee? NominatedBy { get; set; }
    public DateTime NominationDate { get; set; }

    public string? Justification { get; set; }
    public bool IsWaitlisted { get; set; }

    // Approval Workflow
    public NominationStatus Status { get; set; }

    public Guid? SupervisorApprovedById { get; set; }
    public Employee? SupervisorApprovedBy { get; set; }
    public DateTime? SupervisorApprovalDate { get; set; }
    public string? SupervisorComments { get; set; }

    public Guid? HrApprovedById { get; set; }
    public Employee? HrApprovedBy { get; set; }
    public DateTime? HrApprovalDate { get; set; }
    public string? HrComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Participation
    public bool Attended { get; set; }
    public string? NonAttendanceReason { get; set; }
    public bool Completed { get; set; }

    // Post-Training
    public bool CertificateIssued { get; set; }
    public DateTime? CertificateIssuedDate { get; set; }
    public string? CertificateNumber { get; set; }

    // Cost
    public decimal? ActualCost { get; set; }
    public bool EmployeeContributed { get; set; }
    public decimal? EmployeeContribution { get; set; }
}

/// <summary>
/// Daily attendance for training sessions
/// </summary>
public class TrainingAttendance : TenantEntity
{
    public Guid ScheduleId { get; set; }
    public TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime AttendanceDate { get; set; }
    public bool IsPresent { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }

    public string? Notes { get; set; }

    public Guid? MarkedById { get; set; }
    public Employee? MarkedBy { get; set; }
}

/// <summary>
/// Training evaluation/feedback
/// </summary>
public class TrainingEvaluation : TenantEntity
{
    public Guid ScheduleId { get; set; }
    public TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // Ratings (1-5 scale)
    public int? ContentRelevanceRating { get; set; }
    public int? TrainerKnowledgeRating { get; set; }
    public int? DeliveryMethodRating { get; set; }
    public int? MaterialQualityRating { get; set; }
    public int? VenueFacilitiesRating { get; set; }
    public int? OverallSatisfactionRating { get; set; }

    // Feedback
    public string? StrengthsOfTraining { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? SuggestionsForFuture { get; set; }
    public string? AdditionalComments { get; set; }

    // Impact Assessment
    public bool WouldRecommend { get; set; }
    public string? ExpectedApplicationOnJob { get; set; }

    public DateTime EvaluationDate { get; set; }
}

/// <summary>
/// Training materials/resources
/// </summary>
public class TrainingMaterial : TenantEntity
{
    public Guid ProgramId { get; set; }
    public TrainingProgram Program { get; set; } = null!;

    public string MaterialName { get; set; } = string.Empty;
    public MaterialType Type { get; set; } // Handbook, Slides, Video, Document
    public string? Description { get; set; }

    public string? FilePath { get; set; }
    public string? ExternalUrl { get; set; }

    public bool IsPublic { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Annual training plan
/// </summary>
public class TrainingPlan : TenantEntity
{
    public string PlanNumber { get; set; } = string.Empty;
    public int Year { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public TrainingPlanStatus Status { get; set; }

    public decimal TotalBudget { get; set; }
    public decimal AllocatedBudget { get; set; }
    public decimal SpentBudget { get; set; }

    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    public ICollection<TrainingPlanItem> Items { get; set; } = new List<TrainingPlanItem>();
}

public class TrainingPlanItem : TenantEntity
{
    public Guid PlanId { get; set; }
    public TrainingPlan Plan { get; set; } = null!;

    public Guid? ProgramId { get; set; }
    public TrainingProgram? Program { get; set; }

    public string TrainingTitle { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int Quarter { get; set; } // 1-4
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }

    public int EstimatedParticipants { get; set; }
    public decimal EstimatedCost { get; set; }

    public string? TargetDepartments { get; set; }
    public string? TargetPositions { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
    public int? ActualParticipants { get; set; }
    public decimal? ActualCost { get; set; }
}

/// <summary>
/// Training needs assessment
/// </summary>
public class TrainingNeedsAssessment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int Year { get; set; }
    public AssessmentSource Source { get; set; } // Performance Review, Self Assessment, Manager Request

    public string IdentifiedGaps { get; set; } = string.Empty;
    public string RecommendedTraining { get; set; } = string.Empty;
    public TrainingPriority Priority { get; set; }

    public Guid? IdentifiedById { get; set; }
    public Employee? IdentifiedBy { get; set; }
    public DateTime IdentifiedDate { get; set; }

    public bool TrainingProvided { get; set; }
    public Guid? ProvidedScheduleId { get; set; }
    public DateTime? TrainingProvidedDate { get; set; }
}