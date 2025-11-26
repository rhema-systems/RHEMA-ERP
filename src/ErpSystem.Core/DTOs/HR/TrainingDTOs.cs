using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class TrainingProgramListDto
{
    public Guid Id { get; set; }
    public string ProgramCode { get; set; }
    public string ProgramName { get; set; }
    public TrainingCategory Category { get; set; }
    public string CategoryName { get; set; }
    public TrainingType Type { get; set; }
    public string TypeName { get; set; }
    public TrainingLevel Level { get; set; }
    public string LevelName { get; set; }
    public int DurationHours { get; set; }
    public decimal? CostPerParticipant { get; set; }
    public bool IsActive { get; set; }
    public int UpcomingSchedules { get; set; }
    public int TotalParticipants { get; set; }
}

// Detail DTO
public class TrainingProgramDetailDto
{
    public Guid Id { get; set; }
    public string ProgramCode { get; set; }
    public string ProgramName { get; set; }
    public string Description { get; set; }

    // Classification
    public TrainingCategory Category { get; set; }
    public string CategoryName { get; set; }
    public TrainingType Type { get; set; }
    public string TypeName { get; set; }
    public TrainingLevel Level { get; set; }
    public string LevelName { get; set; }

    // Details
    public int DurationHours { get; set; }
    public int? MaxParticipants { get; set; }
    public int? MinParticipants { get; set; }
    public string Prerequisites { get; set; }
    public string LearningObjectives { get; set; }
    public string TargetAudience { get; set; }

    // Provider
    public bool IsInternal { get; set; }
    public Guid? InternalTrainerId { get; set; }
    public string InternalTrainerName { get; set; }
    public string ExternalProvider { get; set; }
    public string ProviderContact { get; set; }

    // Cost
    public decimal? CostPerParticipant { get; set; }
    public string Currency { get; set; }

    // Certification
    public bool ProvidesCertificate { get; set; }
    public string CertificateName { get; set; }
    public int? CertificateValidityMonths { get; set; }

    public bool IsActive { get; set; }

    // Collections
    public List<TrainingMaterialDto> Materials { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateTrainingProgramDto
{
    public string ProgramCode { get; set; }
    public string ProgramName { get; set; }
    public string Description { get; set; }
    public TrainingCategory Category { get; set; }
    public TrainingType Type { get; set; }
    public TrainingLevel Level { get; set; }
    public int DurationHours { get; set; }
    public int? MaxParticipants { get; set; }
    public int? MinParticipants { get; set; }
    public string Prerequisites { get; set; }
    public string LearningObjectives { get; set; }
    public string TargetAudience { get; set; }
    public bool IsInternal { get; set; }
    public Guid? InternalTrainerId { get; set; }
    public string ExternalProvider { get; set; }
    public string ProviderContact { get; set; }
    public decimal? CostPerParticipant { get; set; }
    public string Currency { get; set; }
    public bool ProvidesCertificate { get; set; }
    public string CertificateName { get; set; }
    public int? CertificateValidityMonths { get; set; }
}

// Update DTO
public class UpdateTrainingProgramDto
{
    public Guid Id { get; set; }
    public string ProgramName { get; set; }
    public string Description { get; set; }
    public TrainingCategory Category { get; set; }
    public TrainingType Type { get; set; }
    public TrainingLevel Level { get; set; }
    public int DurationHours { get; set; }
    public int? MaxParticipants { get; set; }
    public int? MinParticipants { get; set; }
    public string Prerequisites { get; set; }
    public string LearningObjectives { get; set; }
    public string TargetAudience { get; set; }
    public Guid? InternalTrainerId { get; set; }
    public string ExternalProvider { get; set; }
    public string ProviderContact { get; set; }
    public decimal? CostPerParticipant { get; set; }
    public bool ProvidesCertificate { get; set; }
    public string CertificateName { get; set; }
    public int? CertificateValidityMonths { get; set; }
}

// Material DTO
public class TrainingMaterialDto
{
    public Guid Id { get; set; }
    public MaterialType MaterialType { get; set; }
    public string MaterialTypeName { get; set; }
    public string MaterialName { get; set; }
    public string Description { get; set; }
    public string FilePath { get; set; }
    public string ExternalUrl { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Training Schedule DTOs
public class TrainingScheduleListDto
{
    public Guid Id { get; set; }
    public string ScheduleNumber { get; set; }
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; }
    public string ProgramCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ScheduleStatus Status { get; set; }
    public string StatusName { get; set; }
    public string Venue { get; set; }
    public bool IsOnline { get; set; }
    public string TrainerName { get; set; }
    public int MaxParticipants { get; set; }
    public int CurrentParticipants { get; set; }
    public int ConfirmedParticipants { get; set; }
    public decimal? TotalBudget { get; set; }
}

public class TrainingScheduleDetailDto
{
    public Guid Id { get; set; }
    public string ScheduleNumber { get; set; }

    // Program Info
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; }
    public string ProgramCode { get; set; }
    public string ProgramDescription { get; set; }
    public int ProgramDurationHours { get; set; }

    // Schedule Details
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public ScheduleStatus Status { get; set; }
    public string StatusName { get; set; }

    // Venue
    public string Venue { get; set; }
    public string VenueAddress { get; set; }
    public bool IsOnline { get; set; }
    public string OnlineLink { get; set; }

    // Trainer
    public Guid? TrainerId { get; set; }
    public string TrainerName { get; set; }
    public string ExternalTrainerName { get; set; }

    // Capacity
    public int MaxParticipants { get; set; }
    public int CurrentParticipants { get; set; }
    public int ConfirmedParticipants { get; set; }

    // Registration
    public DateTime? RegistrationOpenDate { get; set; }
    public DateTime? RegistrationCloseDate { get; set; }

    // Budget
    public decimal? TotalBudget { get; set; }
    public decimal? ActualCost { get; set; }
    public string BudgetNotes { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // Completion
    public DateTime? CompletionDate { get; set; }
    public string CompletionNotes { get; set; }

    // Collections
    public List<TrainingNominationListDto> Nominations { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTrainingScheduleDto
{
    public Guid ProgramId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Venue { get; set; }
    public string VenueAddress { get; set; }
    public bool IsOnline { get; set; }
    public string OnlineLink { get; set; }
    public Guid? TrainerId { get; set; }
    public string ExternalTrainerName { get; set; }
    public int MaxParticipants { get; set; }
    public DateTime? RegistrationOpenDate { get; set; }
    public DateTime? RegistrationCloseDate { get; set; }
    public decimal? TotalBudget { get; set; }
    public string BudgetNotes { get; set; }
}

// Training Nomination DTOs
public class TrainingNominationListDto
{
    public Guid Id { get; set; }
    public string NominationNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }
    public NominationType NominationType { get; set; }
    public string NominationTypeName { get; set; }
    public NominationStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime NominationDate { get; set; }
    public bool AttendedTraining { get; set; }
}

public class TrainingNominationDetailDto
{
    public Guid Id { get; set; }
    public string NominationNumber { get; set; }

    // Schedule Info
    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; }
    public string ProgramName { get; set; }
    public DateTime TrainingStartDate { get; set; }
    public DateTime TrainingEndDate { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }

    // Nomination Details
    public NominationType NominationType { get; set; }
    public string NominationTypeName { get; set; }
    public NominationStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime NominationDate { get; set; }
    public Guid? NominatedById { get; set; }
    public string NominatedByName { get; set; }
    public string Justification { get; set; }

    // Approval Workflow
    public DateTime? SupervisorApprovalDate { get; set; }
    public Guid? SupervisorApprovedById { get; set; }
    public string SupervisorApprovedByName { get; set; }
    public string SupervisorComments { get; set; }

    public DateTime? HrApprovalDate { get; set; }
    public Guid? HrApprovedById { get; set; }
    public string HrApprovedByName { get; set; }
    public string HrComments { get; set; }

    public string RejectionReason { get; set; }

    // Attendance
    public bool AttendedTraining { get; set; }
    public DateTime? AttendanceConfirmedDate { get; set; }
    public string NonAttendanceReason { get; set; }

    // Certification
    public bool CertificateIssued { get; set; }
    public string CertificateNumber { get; set; }
    public DateTime? CertificateIssueDate { get; set; }

    // Cost
    public decimal? ActualCost { get; set; }
    public decimal? EmployeeContribution { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTrainingNominationDto
{
    public Guid ScheduleId { get; set; }
    public Guid EmployeeId { get; set; }
    public NominationType NominationType { get; set; }
    public string Justification { get; set; }
}

// Training Evaluation DTO
public class TrainingEvaluationDto
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public int ContentRating { get; set; }
    public int TrainerRating { get; set; }
    public int MaterialsRating { get; set; }
    public int VenueRating { get; set; }
    public int OverallRating { get; set; }
    public bool WouldRecommend { get; set; }
    public string StrengthsOfTraining { get; set; }
    public string AreasForImprovement { get; set; }
    public string SuggestionsForFuture { get; set; }
    public string AdditionalComments { get; set; }
    public string ExpectedApplicationOnJob { get; set; }
    public DateTime SubmittedDate { get; set; }
}

public class CreateTrainingEvaluationDto
{
    public Guid ScheduleId { get; set; }
    public int ContentRating { get; set; }
    public int TrainerRating { get; set; }
    public int MaterialsRating { get; set; }
    public int VenueRating { get; set; }
    public int OverallRating { get; set; }
    public bool WouldRecommend { get; set; }
    public string StrengthsOfTraining { get; set; }
    public string AreasForImprovement { get; set; }
    public string SuggestionsForFuture { get; set; }
    public string AdditionalComments { get; set; }
    public string ExpectedApplicationOnJob { get; set; }
}

// Dashboard DTO
public class TrainingDashboardDto
{
    public int TotalPrograms { get; set; }
    public int UpcomingTrainings { get; set; }
    public int OngoingTrainings { get; set; }
    public int CompletedTrainings { get; set; }
    public int TotalParticipants { get; set; }
    public decimal TotalBudgetSpent { get; set; }
    public Dictionary<TrainingCategory, int> TrainingsByCategory { get; set; }
    public List<TrainingScheduleListDto> UpcomingSchedules { get; set; }
}