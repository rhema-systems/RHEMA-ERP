using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

/// <summary>
/// DTO for creating a new job card
/// </summary>
public class CreateJobCardDto
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? ProblemDescription { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    [MaxLength(20)]
    public string MaintenanceLocation { get; set; } = "Internal";

    public DateTime? RequiredCompletionDate { get; set; }

    public double EstimatedHours { get; set; } = 0;

    public decimal EstimatedCost { get; set; } = 0;

    public Guid? PreferredTechnicianId { get; set; }
    public Guid? PreferredTeamId { get; set; }
    public Guid? ContractorId { get; set; }

    public bool RequiresSpecialTools { get; set; } = false;
    public bool RequiresShutdown { get; set; } = false;
    public bool RequiresSafetyPermit { get; set; } = false;

    [MaxLength(1000)]
    public string? SpecialInstructions { get; set; }

    [MaxLength(1000)]
    public string? SafetyRequirements { get; set; }

    // Asset Admission (optional at creation)
    [MaxLength(20)]
    public string? AssetConditionOnAdmission { get; set; }
    
    public decimal? MileageReadingOnAdmission { get; set; }
    public decimal? HoursReadingOnAdmission { get; set; }
    public decimal? FuelLevelOnAdmission { get; set; }
    
    [MaxLength(2000)]
    public string? AdmissionNotes { get; set; }
    
    [MaxLength(100)]
    public string? BayOrStation { get; set; }

    public Dictionary<string, object>? CustomFieldValues { get; set; }
}

/// <summary>
/// DTO for updating an existing job card
/// </summary>
public class UpdateJobCardDto
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? ProblemDescription { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    [MaxLength(20)]
    public string MaintenanceLocation { get; set; } = "Internal";

    public DateTime? RequiredCompletionDate { get; set; }

    public double EstimatedHours { get; set; } = 0;

    public decimal EstimatedCost { get; set; } = 0;

    public Guid? PreferredTechnicianId { get; set; }
    public Guid? PreferredTeamId { get; set; }
    public Guid? ContractorId { get; set; }

    public bool RequiresSpecialTools { get; set; } = false;
    public bool RequiresShutdown { get; set; } = false;
    public bool RequiresSafetyPermit { get; set; } = false;

    [MaxLength(1000)]
    public string? SpecialInstructions { get; set; }

    [MaxLength(1000)]
    public string? SafetyRequirements { get; set; }

    public Dictionary<string, object>? CustomFieldValues { get; set; }
}

/// <summary>
/// DTO for job card list view
/// </summary>
public class JobCardListDto
{
    public Guid Id { get; set; }
    public string JobCardNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProblemDescription { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public Guid MaintenanceTypeId { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public Guid PriorityLevelId { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string PriorityColor { get; set; } = string.Empty;
    public string JobCardStatus { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public DateTime? RequiredCompletionDate { get; set; }
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool RequiresShutdown { get; set; }
    public bool RequiresSafetyPermit { get; set; }
    public Guid? GeneratedWorkOrderId { get; set; }
    public DateTime? WorkOrderGeneratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for detailed job card view
/// </summary>
public class JobCardDto
{
    public Guid Id { get; set; }
    public string JobCardNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProblemDescription { get; set; }
    
    // Asset Information
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
    
    // Maintenance Details
    public Guid MaintenanceTypeId { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string MaintenanceCategory { get; set; } = string.Empty;
    public Guid PriorityLevelId { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string PriorityColor { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    
    public string MaintenanceLocation { get; set; } = string.Empty;
    
    // Request Information
    public Guid RequestedById { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public DateTime? RequiredCompletionDate { get; set; }
    
    // Estimates
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    
    // Assignment
    public Guid? PreferredTechnicianId { get; set; }
    public string? PreferredTechnician { get; set; }
    public Guid? PreferredTeamId { get; set; }
    public string? PreferredTeam { get; set; }
    public Guid? ContractorId { get; set; }
    public string? Contractor { get; set; }
    
    // Requirements
    public bool RequiresSpecialTools { get; set; }
    public bool RequiresShutdown { get; set; }
    public bool RequiresSafetyPermit { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? SafetyRequirements { get; set; }
    
    // Status Information
    public string JobCardStatus { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedBy { get; set; }
    
    // Planning (filled during approval)
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnician { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public string? AssignedTeam { get; set; }
    
    // Work Order Generation
    public Guid? GeneratedWorkOrderId { get; set; }
    public string? GeneratedWorkOrderNumber { get; set; }
    public DateTime? WorkOrderGeneratedAt { get; set; }
    
    // Asset Admission
    public string? AssetConditionOnAdmission { get; set; }
    public decimal? MileageReadingOnAdmission { get; set; }
    public decimal? HoursReadingOnAdmission { get; set; }
    public decimal? FuelLevelOnAdmission { get; set; }
    public string? AdmissionNotes { get; set; }
    public string? BayOrStation { get; set; }
    
    // Job Card Completion
    public DateTime? CompletedDate { get; set; }
    public string? CompletionNotes { get; set; }
    public string? AssetConditionOnCompletion { get; set; }
    public decimal? MileageReadingOnCompletion { get; set; }
    public decimal? HoursReadingOnCompletion { get; set; }
    public decimal? FuelLevelOnCompletion { get; set; }
    public string? WorkCompletedSummary { get; set; }
    public string? RemainingIssues { get; set; }
    
    // Quality Control
    public bool QualityCheckPassed { get; set; }
    public Guid? QualityCheckedById { get; set; }
    public string? QualityCheckedBy { get; set; }
    public DateTime? QualityCheckDate { get; set; }
    public string? QualityCheckNotes { get; set; }
    
    // Certificate
    public bool CertificateGenerated { get; set; }
    public DateTime? CertificateGeneratedDate { get; set; }
    
    // Customer Acceptance
    public bool CustomerAcceptance { get; set; }
    public Guid? AcceptedById { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public string? AcceptanceNotes { get; set; }
    
    // Follow-up
    public bool RequiresFollowUp { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? FollowUpInstructions { get; set; }
    
    // Warranty
    public int WarrantyDays { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    public string? WarrantyTerms { get; set; }
    
    // Attachments and Documents
    public List<JobCardDocumentDto> Documents { get; set; } = new();
    public List<JobCardCommentDto> Comments { get; set; } = new();
    public List<JobCardApprovalStepDto> ApprovalSteps { get; set; } = new();
    public List<JobCardCertificateDto> Certificates { get; set; } = new();
    
    // Custom Fields
    public Dictionary<string, object>? CustomFieldValues { get; set; }
    
    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for job card documents/attachments
/// </summary>
public class JobCardDocumentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}

/// <summary>
/// DTO for job card comments
/// </summary>
public class JobCardCommentDto
{
    public Guid Id { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string CommentType { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CommentDate { get; set; }
    public string CommentBy { get; set; } = string.Empty;
}

/// <summary>
/// DTO for approval steps
/// </summary>
public class JobCardApprovalStepDto
{
    public Guid Id { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ActionDate { get; set; }
    public string? Comments { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// DTO for job card filtering
/// </summary>
public class JobCardFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? ApprovalStatus { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public Guid? PriorityLevelId { get; set; }
    public Guid? RequestedById { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public DateTime? RequestedFrom { get; set; }
    public DateTime? RequestedTo { get; set; }
    public DateTime? RequiredFrom { get; set; }
    public DateTime? RequiredTo { get; set; }
    public bool? RequiresApproval { get; set; }
    public bool? HasWorkOrder { get; set; }
}

/// <summary>
/// DTO for job card approval actions
/// </summary>
public class JobCardApprovalActionDto
{
    [Required]
    public string Action { get; set; } = string.Empty; // "Approve", "Reject", "RequestChanges"
    
    [MaxLength(2000)]
    public string? Comments { get; set; }
    
    // Planning details (for approval)
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    
    // Updated estimates (if needed during approval)
    public double? RevisedEstimatedHours { get; set; }
    public decimal? RevisedEstimatedCost { get; set; }
}

/// <summary>
/// DTO for submitting job card for approval
/// </summary>
public class SubmitJobCardDto
{
    [MaxLength(1000)]
    public string? SubmissionNotes { get; set; }
    
    public bool ConfirmReadiness { get; set; } = false;
}

/// <summary>
/// DTO for adding comments to job card
/// </summary>
public class AddJobCardCommentDto
{
    [Required]
    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string CommentType { get; set; } = "General";
    
    public bool IsInternal { get; set; } = true;
}

/// <summary>
/// DTO for recording asset admission details when job card is created
/// </summary>
public class JobCardAdmissionDto
{
    [MaxLength(20)]
    public string? AssetConditionOnAdmission { get; set; }
    
    public decimal? MileageReadingOnAdmission { get; set; }
    public decimal? HoursReadingOnAdmission { get; set; }
    public decimal? FuelLevelOnAdmission { get; set; }
    
    [MaxLength(2000)]
    public string? AdmissionNotes { get; set; }
    
    [MaxLength(100)]
    public string? BayOrStation { get; set; }
}

/// <summary>
/// DTO for completing a job card
/// </summary>
public class CompleteJobCardDto
{
    [Required]
    [MaxLength(2000)]
    public string CompletionNotes { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string? AssetConditionOnCompletion { get; set; }
    
    public decimal? MileageReadingOnCompletion { get; set; }
    public decimal? HoursReadingOnCompletion { get; set; }
    public decimal? FuelLevelOnCompletion { get; set; }
    
    [MaxLength(2000)]
    public string? WorkCompletedSummary { get; set; }
    
    [MaxLength(2000)]
    public string? RemainingIssues { get; set; }
    
    // Warranty
    public int WarrantyDays { get; set; } = 0;
    
    [MaxLength(1000)]
    public string? WarrantyTerms { get; set; }
    
    // Follow-up
    public bool RequiresFollowUp { get; set; } = false;
    public DateTime? FollowUpDate { get; set; }
    
    [MaxLength(1000)]
    public string? FollowUpInstructions { get; set; }
}

/// <summary>
/// DTO for quality check of completed job card
/// </summary>
public class JobCardQualityCheckDto
{
    [Required]
    public bool QualityCheckPassed { get; set; }
    
    [Required]
    [MaxLength(2000)]
    public string QualityCheckNotes { get; set; } = string.Empty;
}

/// <summary>
/// DTO for customer/user acceptance
/// </summary>
public class JobCardAcceptanceDto
{
    [Required]
    public bool Accepted { get; set; }
    
    [MaxLength(1000)]
    public string? AcceptanceNotes { get; set; }
}

/// <summary>
/// DTO for generating certificate
/// </summary>
public class GenerateJobCardCertificateDto
{
    [Required]
    [MaxLength(100)]
    public string CertificateType { get; set; } = "Maintenance Completion";
    
    public DateTime? ValidUntil { get; set; }
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    public Dictionary<string, object>? CertificateData { get; set; }
}

/// <summary>
/// DTO for job card certificate display
/// </summary>
public class JobCardCertificateDto
{
    public Guid Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public Guid JobCardId { get; set; }
    public string JobCardNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string FileFormat { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
