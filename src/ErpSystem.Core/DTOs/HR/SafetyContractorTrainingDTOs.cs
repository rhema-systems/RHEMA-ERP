using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — CONTRACTOR SHE & TRAINING DTOs
// Domains: H. Contractor SHE Management   I. SHE Training & Awareness
// ============================================================================

// ============================================================================
// H. CONTRACTOR SHE MANAGEMENT
// ============================================================================

#region Contractor

public class SheContractorDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ContractorCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public SheContractorStatus SheStatus { get; set; }
    public string SheStatusName => SheStatus.ToString();
    public int? PreQualificationScore { get; set; }
    public DateTime? PreQualificationDate { get; set; }
    public DateTime? PreQualificationExpiryDate { get; set; }
    public Guid? PreQualifiedById { get; set; }
    public string? PreQualifiedByName { get; set; }
    public string? SheConditions { get; set; }
    public bool IsActive { get; set; }

    public List<SheContractorInductionDto> Inductions { get; set; } = new();
    public List<SheContractorInspectionDto> SheInspections { get; set; } = new();
    public List<SheContractorNonComplianceDto> NonCompliances { get; set; } = new();
    public List<SheContractorDocumentDto> Documents { get; set; } = new();
}

public class SheContractorSummaryDto
{
    public Guid Id { get; set; }
    public string ContractorCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? PrimaryContactName { get; set; }
    public SheContractorStatus SheStatus { get; set; }
    public string SheStatusName => SheStatus.ToString();
    public int? PreQualificationScore { get; set; }
    public DateTime? PreQualificationExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public int OpenNonComplianceCount { get; set; }
}

public class CreateSheContractorDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string ContractorCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TradingName { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(50)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactEmail { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSheContractorDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TradingName { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(50)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactEmail { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Records the SHE pre-qualification assessment outcome for a contractor.</summary>
public class PreQualifySheContractorDto
{
    [Required]
    public Guid ContractorId { get; set; }

    [Required]
    public SheContractorStatus SheStatus { get; set; }

    [Range(0, 100)]
    public int? PreQualificationScore { get; set; }

    public DateTime PreQualificationDate { get; set; } = DateTime.UtcNow;
    public DateTime? PreQualificationExpiryDate { get; set; }

    [Required]
    public Guid PreQualifiedById { get; set; }

    [MaxLength(1000)]
    public string? SheConditions { get; set; }
}

public class SheContractorInductionDto : BaseDto
{
    public Guid ContractorId { get; set; }
    public string WorkerName { get; set; } = string.Empty;
    public string? WorkerIdOrPassport { get; set; }
    public string? Trade { get; set; }
    public DateTime InductionDate { get; set; }
    public Guid ConductedById { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
    public bool InductionPassed { get; set; }
    public DateTime? InductionExpiryDate { get; set; }
    public string? SignaturePath { get; set; }
    public string? Notes { get; set; }
}

public class CreateSheContractorInductionDto : CreateDtoBase
{
    [Required]
    public Guid ContractorId { get; set; }

    [Required, MaxLength(200)]
    public string WorkerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? WorkerIdOrPassport { get; set; }

    [MaxLength(100)]
    public string? Trade { get; set; }

    [Required]
    public DateTime InductionDate { get; set; }

    [Required]
    public Guid ConductedById { get; set; }

    public bool InductionPassed { get; set; }
    public DateTime? InductionExpiryDate { get; set; }

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateSheContractorInductionDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string WorkerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? WorkerIdOrPassport { get; set; }

    [MaxLength(100)]
    public string? Trade { get; set; }

    [Required]
    public DateTime InductionDate { get; set; }

    public bool InductionPassed { get; set; }
    public DateTime? InductionExpiryDate { get; set; }

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class SheContractorInspectionDto : BaseDto
{
    public Guid ContractorId { get; set; }
    public string InspectionNumber { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }
    public Guid InspectorId { get; set; }
    public string InspectorName { get; set; } = string.Empty;
    public int? ComplianceScore { get; set; }
    public SheInspectionResult Result { get; set; }
    public string ResultName => Result.ToString();
    public string? Findings { get; set; }
    public string? RecommendedActions { get; set; }
    public DateTime? NextInspectionDate { get; set; }
    public SheInspectionStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateSheContractorInspectionDto : CreateDtoBase
{
    [Required]
    public Guid ContractorId { get; set; }

    [Required, MaxLength(30)]
    public string InspectionNumber { get; set; } = string.Empty;

    [Required]
    public DateTime InspectionDate { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required]
    public Guid InspectorId { get; set; }

    [Range(0, 100)]
    public int? ComplianceScore { get; set; }

    [Required]
    public SheInspectionResult Result { get; set; }

    [MaxLength(3000)]
    public string? Findings { get; set; }

    [MaxLength(2000)]
    public string? RecommendedActions { get; set; }

    public DateTime? NextInspectionDate { get; set; }

    [Required]
    public SheInspectionStatus Status { get; set; }
}

public class UpdateSheContractorInspectionDto : UpdateDtoBase
{
    [Required]
    public DateTime InspectionDate { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Range(0, 100)]
    public int? ComplianceScore { get; set; }

    [Required]
    public SheInspectionResult Result { get; set; }

    [MaxLength(3000)]
    public string? Findings { get; set; }

    [MaxLength(2000)]
    public string? RecommendedActions { get; set; }

    public DateTime? NextInspectionDate { get; set; }

    [Required]
    public SheInspectionStatus Status { get; set; }
}

public class SheContractorNonComplianceDto : BaseDto
{
    public Guid ContractorId { get; set; }
    public string NoticeNumber { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public Guid IssuedById { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public string ViolationDescription { get; set; } = string.Empty;
    public SheNonComplianceSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public DateTime RectificationDeadline { get; set; }
    public bool IsRepeatViolation { get; set; }
    public int RepeatCount { get; set; }
    public SheNonComplianceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? RectificationDate { get; set; }
    public string? ContractorResponse { get; set; }
    public string? ClosureNotes { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public SheContractorSanction? SanctionApplied { get; set; }
    public string? SanctionAppliedName => SanctionApplied?.ToString();
}

public class CreateSheContractorNonComplianceDto : CreateDtoBase
{
    [Required]
    public Guid ContractorId { get; set; }

    [Required, MaxLength(30)]
    public string NoticeNumber { get; set; } = string.Empty;

    [Required]
    public DateTime IssuedDate { get; set; }

    [Required]
    public Guid IssuedById { get; set; }

    [Required, MaxLength(2000)]
    public string ViolationDescription { get; set; } = string.Empty;

    [Required]
    public SheNonComplianceSeverity Severity { get; set; }

    [Required]
    public DateTime RectificationDeadline { get; set; }
}

public class UpdateSheContractorNonComplianceDto : UpdateDtoBase
{
    [Required, MaxLength(2000)]
    public string ViolationDescription { get; set; } = string.Empty;

    [Required]
    public SheNonComplianceSeverity Severity { get; set; }

    [Required]
    public DateTime RectificationDeadline { get; set; }

    [Required]
    public SheNonComplianceStatus Status { get; set; }

    public DateTime? RectificationDate { get; set; }

    [MaxLength(1000)]
    public string? ContractorResponse { get; set; }

    public SheContractorSanction? SanctionApplied { get; set; }
}

/// <summary>Closes a contractor non-compliance notice.</summary>
public class CloseSheContractorNonComplianceDto
{
    [Required]
    public Guid NonComplianceId { get; set; }

    [Required]
    public Guid ClosedById { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;
    public DateTime? RectificationDate { get; set; }

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }
}

public class SheContractorDocumentDto : BaseDto
{
    public Guid ContractorId { get; set; }
    public SheContractorDocumentType DocumentType { get; set; }
    public string DocumentTypeName => DocumentType.ToString();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime? DocumentDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsVerified { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? VerificationNotes { get; set; }
    public DateTime UploadedDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateSheContractorDocumentDto : CreateDtoBase
{
    [Required]
    public Guid ContractorId { get; set; }

    [Required]
    public SheContractorDocumentType DocumentType { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Title { get; set; }

    public DateTime? DocumentDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [Required]
    public Guid UploadedById { get; set; }
}

/// <summary>Marks a contractor document as verified.</summary>
public class VerifySheContractorDocumentDto
{
    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    public Guid VerifiedById { get; set; }

    public DateTime VerifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(300)]
    public string? VerificationNotes { get; set; }
}

#endregion

// ============================================================================
// I. SHE TRAINING & AWARENESS
// ============================================================================

#region Training Plan

public class SheTrainingPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public SheTrainingPlanStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid PreparedById { get; set; }
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? Notes { get; set; }
    public List<SheTrainingProgramSummaryDto> Programs { get; set; } = new();
}

public class CreateSheTrainingPlanDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string PlanNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public Guid PreparedById { get; set; }

    public DateTime PreparedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateSheTrainingPlanDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public SheTrainingPlanStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Training Program

public class SheTrainingProgramDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheTrainingCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public Guid? PlanId { get; set; }
    public string? PlanNumber { get; set; }
    public SheTrainingDeliveryMethod DeliveryMethod { get; set; }
    public string DeliveryMethodName => DeliveryMethod.ToString();
    public int DurationMinutes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? TrainerId { get; set; }
    public string? TrainerName { get; set; }
    public string? ExternalTrainerName { get; set; }
    public string? ExternalTrainerOrganization { get; set; }
    public SheTrainingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? MaxParticipants { get; set; }
    public int? ActualAttendees { get; set; }
    public string? MaterialPath { get; set; }
    public bool WasEvaluated { get; set; }
    public string? EvaluationSummary { get; set; }
    public Guid? EvaluatedById { get; set; }
    public string? EvaluatedByName { get; set; }
    public DateTime? EvaluationDate { get; set; }
    public List<SheTrainingAttendanceDto> Attendances { get; set; } = new();
}

public class SheTrainingProgramSummaryDto
{
    public Guid Id { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheTrainingCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public SheTrainingDeliveryMethod DeliveryMethod { get; set; }
    public string DeliveryMethodName => DeliveryMethod.ToString();
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public SheTrainingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? ActualAttendees { get; set; }
}

public class CreateSheTrainingProgramDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string ProgramCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public SheTrainingCategory Category { get; set; }

    public Guid? PlanId { get; set; }

    [Required]
    public SheTrainingDeliveryMethod DeliveryMethod { get; set; }

    public int DurationMinutes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? TrainerId { get; set; }

    [MaxLength(200)]
    public string? ExternalTrainerName { get; set; }

    [MaxLength(200)]
    public string? ExternalTrainerOrganization { get; set; }

    public int? MaxParticipants { get; set; }

    [MaxLength(500)]
    public string? MaterialPath { get; set; }
}

public class UpdateSheTrainingProgramDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public SheTrainingCategory Category { get; set; }

    public Guid? PlanId { get; set; }

    [Required]
    public SheTrainingDeliveryMethod DeliveryMethod { get; set; }

    public int DurationMinutes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? TrainerId { get; set; }

    [MaxLength(200)]
    public string? ExternalTrainerName { get; set; }

    [MaxLength(200)]
    public string? ExternalTrainerOrganization { get; set; }

    [Required]
    public SheTrainingStatus Status { get; set; }

    public int? MaxParticipants { get; set; }
    public int? ActualAttendees { get; set; }

    [MaxLength(500)]
    public string? MaterialPath { get; set; }
}

/// <summary>Records the post-delivery effectiveness evaluation of a program.</summary>
public class EvaluateSheTrainingProgramDto
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public Guid EvaluatedById { get; set; }

    public DateTime EvaluationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? EvaluationSummary { get; set; }
}

public class SheTrainingAttendanceDto : BaseDto
{
    public Guid ProgramId { get; set; }
    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }
    public string AttendanceName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public bool Attended { get; set; }
    public DateTime? SignedDate { get; set; }
    public string? SignaturePath { get; set; }
    public bool? AssessmentPassed { get; set; }
    public int? AssessmentScore { get; set; }
    public DateTime? CertificateExpiryDate { get; set; }
    public string? CertificateDocumentPath { get; set; }

    // Program context (populated when the program navigation is loaded — e.g. "my training").
    public string? ProgramCode { get; set; }
    public string? ProgramTitle { get; set; }
    public SheTrainingCategory? ProgramCategory { get; set; }
    public string? ProgramCategoryName => ProgramCategory?.ToString();
    public DateTime? ProgramDate { get; set; }
    public SheTrainingStatus? ProgramStatus { get; set; }
    public string? ProgramStatusName => ProgramStatus?.ToString();
}

public class CreateSheTrainingAttendanceDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }

    [Required, MaxLength(200)]
    public string AttendanceName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    public bool Attended { get; set; }
    public DateTime? SignedDate { get; set; }

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    public bool? AssessmentPassed { get; set; }
    public int? AssessmentScore { get; set; }
    public DateTime? CertificateExpiryDate { get; set; }

    [MaxLength(200)]
    public string? CertificateDocumentPath { get; set; }
}

public class UpdateSheTrainingAttendanceDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string AttendanceName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    public bool Attended { get; set; }
    public DateTime? SignedDate { get; set; }

    [MaxLength(500)]
    public string? SignaturePath { get; set; }

    public bool? AssessmentPassed { get; set; }
    public int? AssessmentScore { get; set; }
    public DateTime? CertificateExpiryDate { get; set; }

    [MaxLength(200)]
    public string? CertificateDocumentPath { get; set; }
}

#endregion
