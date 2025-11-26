using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.DTOs.Maintenance;

public class AssetAdmissionDto
{
    public Guid Id { get; set; }
    public string AdmissionNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public DateTime AdmissionDate { get; set; }
    public Guid AdmittedById { get; set; }
    public string AdmittedBy { get; set; } = string.Empty;
    public string AdmissionType { get; set; } = string.Empty;
    public string AssetConditionOnAdmission { get; set; } = string.Empty;
    public string? AdmissionNotes { get; set; }
    public string? ObservedProblems { get; set; }
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }
    public string? AdmissionChecklistJson { get; set; }
    public string? AdmissionLocation { get; set; }
    public string? BayOrStation { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    public DateTime? EstimatedDischargeDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? DischargeId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAssetAdmissionDto
{
    [Required]
    public Guid AssetId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    [MaxLength(20)]
    public string AdmissionType { get; set; } = "Scheduled";

    [Required]
    [MaxLength(20)]
    public string AssetConditionOnAdmission { get; set; } = "Good";

    public string? AdmissionNotes { get; set; }
    public string? ObservedProblems { get; set; }
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }
    public string? AdmissionChecklistJson { get; set; }
    public string? AdmissionLocation { get; set; }
    public string? BayOrStation { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    public DateTime? EstimatedDischargeDate { get; set; }
}

public class UpdateAssetAdmissionDto
{
    [MaxLength(20)]
    public string? AdmissionType { get; set; }
    [MaxLength(20)]
    public string? AssetConditionOnAdmission { get; set; }
    public string? AdmissionNotes { get; set; }
    public string? ObservedProblems { get; set; }
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }
    public string? AdmissionChecklistJson { get; set; }
    public string? AdmissionLocation { get; set; }
    public string? BayOrStation { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    public DateTime? EstimatedDischargeDate { get; set; }
}

public class AssetDischargeDto
{
    public Guid Id { get; set; }
    public string DischargeNumber { get; set; } = string.Empty;
    public Guid AdmissionId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public DateTime DischargeDate { get; set; }
    public Guid DischargedById { get; set; }
    public string DischargedBy { get; set; } = string.Empty;
    public string AssetConditionOnDischarge { get; set; } = string.Empty;
    public string? DischargeNotes { get; set; }
    public string? WorkCompleted { get; set; }
    public string? RemainingIssues { get; set; }
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }
    public bool QualityCheckPassed { get; set; }
    public Guid? QualityCheckedById { get; set; }
    public string? QualityCheckedBy { get; set; }
    public DateTime? QualityCheckDate { get; set; }
    public string? QualityCheckNotes { get; set; }
    public string? DischargeChecklistJson { get; set; }
    public bool CertificateGenerated { get; set; }
    public DateTime? CertificateGeneratedDate { get; set; }
    public string? CertificatePath { get; set; }
    public bool CustomerAcceptance { get; set; }
    public Guid? AcceptedById { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public string? AcceptanceNotes { get; set; }
    public bool RequiresFollowUp { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? FollowUpInstructions { get; set; }
    public int WarrantyDays { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    public string? WarrantyTerms { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAssetDischargeDto
{
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    [MaxLength(20)]
    public string AssetConditionOnDischarge { get; set; } = "Good";

    public string? DischargeNotes { get; set; }
    public string? WorkCompleted { get; set; }
    public string? RemainingIssues { get; set; }
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }
    public bool QualityCheckPassed { get; set; }
    public string? QualityCheckNotes { get; set; }
    public string? DischargeChecklistJson { get; set; }
    public bool CustomerAcceptance { get; set; }
    public string? AcceptanceNotes { get; set; }
    public bool RequiresFollowUp { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? FollowUpInstructions { get; set; }
    public int? WarrantyDays { get; set; }
    public string? WarrantyTerms { get; set; }
}

public class UpdateAssetDischargeDto
{
    [MaxLength(20)]
    public string? AssetConditionOnDischarge { get; set; }
    public string? DischargeNotes { get; set; }
    public string? WorkCompleted { get; set; }
    public string? RemainingIssues { get; set; }
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }
    public bool? QualityCheckPassed { get; set; }
    public string? QualityCheckNotes { get; set; }
    public string? DischargeChecklistJson { get; set; }
    public bool? CustomerAcceptance { get; set; }
    public string? AcceptanceNotes { get; set; }
    public bool? RequiresFollowUp { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? FollowUpInstructions { get; set; }
    public int? WarrantyDays { get; set; }
    public string? WarrantyTerms { get; set; }
}

