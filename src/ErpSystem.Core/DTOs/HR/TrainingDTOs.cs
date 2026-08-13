using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// TRAINING VENDOR DTOs
// ============================================================================

#region Training Vendor

public class TrainingVendorDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TrainingVendorType VendorType { get; set; }
    public string VendorTypeName => VendorType.ToString();

    public bool IsActive { get; set; }
    public bool IsPreferred { get; set; }
    public DateTime? PreferredSince { get; set; }
    public bool IsBlacklisted { get; set; }
    public DateTime? BlacklistedDate { get; set; }
    public string? BlacklistReason { get; set; }

    public string? AccreditationBody { get; set; }
    public string? AccreditationNumber { get; set; }
    public VendorAccreditationStatus? AccreditationStatus { get; set; }
    public string? AccreditationStatusName => AccreditationStatus?.ToString();
    public DateTime? AccreditationExpiryDate { get; set; }
    public bool IsAccreditationExpired => AccreditationExpiryDate.HasValue && AccreditationExpiryDate.Value < DateTime.UtcNow;

    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }

    public string Currency { get; set; } = "GHS";
    public decimal? DefaultDailyRate { get; set; }
    public string? ContractReference { get; set; }
    public string? ContractDocumentPath { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public bool IsContractActive => ContractStartDate.HasValue && ContractEndDate.HasValue
        && ContractStartDate.Value <= DateTime.UtcNow && ContractEndDate.Value >= DateTime.UtcNow;

    public string? Notes { get; set; }
    public int TotalTrainersCount { get; set; }
}

public class TrainingVendorSummaryDto
{
    public Guid Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TrainingVendorType VendorType { get; set; }
    public string VendorTypeName => VendorType.ToString();
    public bool IsActive { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsBlacklisted { get; set; }
    public VendorAccreditationStatus? AccreditationStatus { get; set; }
    public string? AccreditationStatusName => AccreditationStatus?.ToString();
    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public int TotalTrainersCount { get; set; }
}

public class CreateTrainingVendorDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string VendorCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TrainingVendorType VendorType { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsPreferred { get; set; }
    public DateTime? PreferredSince { get; set; }

    [MaxLength(200)]
    public string? AccreditationBody { get; set; }

    [MaxLength(100)]
    public string? AccreditationNumber { get; set; }

    public VendorAccreditationStatus? AccreditationStatus { get; set; }
    public DateTime? AccreditationExpiryDate { get; set; }

    [MaxLength(200)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(256)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; set; }

    [MaxLength(20)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(500)]
    public string? Website { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Range(0, double.MaxValue)]
    public decimal? DefaultDailyRate { get; set; }

    [MaxLength(100)]
    public string? ContractReference { get; set; }
    public string? ContractDocumentPath { get; set; }

    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateTrainingVendorDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TrainingVendorType VendorType { get; set; }

    public bool IsActive { get; set; }
    public bool IsPreferred { get; set; }
    public DateTime? PreferredSince { get; set; }

    [MaxLength(200)]
    public string? AccreditationBody { get; set; }

    [MaxLength(100)]
    public string? AccreditationNumber { get; set; }

    public VendorAccreditationStatus? AccreditationStatus { get; set; }
    public DateTime? AccreditationExpiryDate { get; set; }

    [MaxLength(200)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(256)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; set; }

    [MaxLength(20)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(500)]
    public string? Website { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Range(0, double.MaxValue)]
    public decimal? DefaultDailyRate { get; set; }

    [MaxLength(100)]
    public string? ContractReference { get; set; }
    public string? ContractDocumentPath { get; set; }

    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class BlacklistVendorDto
{
    [Required]
    public Guid VendorId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string BlacklistReason { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// TRAINER PROFILE DTOs
// ============================================================================

#region Trainer Profile

public class TrainerProfileDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }

    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }

    public string? Bio { get; set; }
    public string? Contact { get; set; }
    public string? ExpertiseAreas { get; set; }

    public int TotalTrainingHoursDelivered { get; set; }
    public int TotalSessionsDelivered { get; set; }
    public decimal? AverageRating { get; set; }
    public int TotalRatingsCount { get; set; }
    public bool IsActive { get; set; }

    public List<TrainerSkillDto> Skills { get; set; } = new();
    public List<TrainerAvailabilityDto> Availability { get; set; } = new();
}

public class TrainerProfileSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? ExpertiseAreas { get; set; }
    public decimal? AverageRating { get; set; }
    public int TotalSessionsDelivered { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTrainerProfileDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }

    // Nullable to match the entity: an internal trainer (linked via EmployeeId) has no vendor. A
    // non-nullable Guid here previously forced Guid.Empty onto TrainerProfile.VendorId instead of
    // null for that case, which the FK constraint rejects (SQL 547) — found while scoping this
    // area's UI, never previously exercised.
    public Guid? VendorId { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }

    [MaxLength(200)]
    public string? Contact { get; set; }

    [MaxLength(1000)]
    public string? ExpertiseAreas { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateTrainerProfileDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }

    public Guid? VendorId { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }

    [MaxLength(200)]
    public string? Contact { get; set; }

    [MaxLength(1000)]
    public string? ExpertiseAreas { get; set; }

    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// TRAINER SKILL DTOs
// ============================================================================

#region Trainer Skill

public class TrainerSkillDto : BaseDto
{
    public Guid TrainerProfileId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? SkillCategory { get; set; }
    public ProficiencyLevel TrainerProficiency { get; set; }
    public string TrainerProficiencyName => TrainerProficiency.ToString();
    public bool IsCertifiedToTrain { get; set; }
    public string? CertificateNumber { get; set; }
    public string? CertificateName { get; set; }
    public string? CertificateFilePath { get; set; }
}

public class CreateTrainerSkillDto : CreateDtoBase
{
    [Required]
    public Guid TrainerProfileId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public ProficiencyLevel TrainerProficiency { get; set; }

    public bool IsCertifiedToTrain { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [MaxLength(200)]
    public string? CertificateName { get; set; }

    [MaxLength(1000)]
    public string? CertificateFilePath { get; set; }
}

public class UpdateTrainerSkillDto : UpdateDtoBase
{
    [Required]
    public ProficiencyLevel TrainerProficiency { get; set; }

    public bool IsCertifiedToTrain { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [MaxLength(200)]
    public string? CertificateName { get; set; }

    [MaxLength(1000)]
    public string? CertificateFilePath { get; set; }
}

#endregion

// ============================================================================
// TRAINER AVAILABILITY DTOs
// ============================================================================

#region Trainer Availability

public class TrainerAvailabilityDto : BaseDto
{
    public Guid TrainerProfileId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool IsAvailable { get; set; }
    public TrainerEngagementType? EngagementType { get; set; }
    public string? EngagementTypeName => EngagementType?.ToString();
    public string? Notes { get; set; }
}

public class CreateTrainerAvailabilityDto : CreateDtoBase
{
    [Required]
    public Guid TrainerProfileId { get; set; }

    [Required]
    public DateTime FromDate { get; set; }

    [Required]
    public DateTime ToDate { get; set; }

    public bool IsAvailable { get; set; } = true;

    public TrainerEngagementType? EngagementType { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateTrainerAvailabilityDto : UpdateDtoBase
{
    [Required]
    public DateTime FromDate { get; set; }

    [Required]
    public DateTime ToDate { get; set; }

    public bool IsAvailable { get; set; }

    public TrainerEngagementType? EngagementType { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// TRAINING PROGRAM DTOs
// ============================================================================

#region Training Program

public class TrainingProgramDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid? CategoryOptionId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }

    public Guid? ProgramGroupId { get; set; }
    public string? GroupName { get; set; }
    public string? GroupColor { get; set; }

    public TrainingType Type { get; set; }
    public string TypeName => Type.ToString();
    public TrainingSource Source { get; set; }
    public string SourceName => Source.ToString();
    public TrainingLevel Level { get; set; }
    public string LevelName => Level.ToString();

    public int DurationDays { get; set; }
    public int DurationHours { get; set; }
    public string? Prerequisites { get; set; }
    public string? LearningObjectives { get; set; }

    public decimal CostPerParticipant { get; set; }
    public string Currency { get; set; } = "GHS";
    public bool IncludesAccommodation { get; set; }
    public bool IncludesMeals { get; set; }
    public bool IncludesTransport { get; set; }

    public bool ProvidesCertificate { get; set; }
    public string? CertificateName { get; set; }
    public int? CertificateValidityMonths { get; set; }

    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }

    public bool RequiresServiceBond { get; set; }
    public int? ServiceBondMonths { get; set; }
    public string? ServiceBondTerms { get; set; }

    public List<TrainingProgramCompetencyDto> Competencies { get; set; } = new();
    public List<TrainingProgramSkillDto> Skills { get; set; } = new();
    public List<TrainingMaterialDto> Materials { get; set; } = new();
}

public class TrainingProgramSummaryDto
{
    public Guid Id { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public Guid? CategoryOptionId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public Guid? ProgramGroupId { get; set; }
    public string? GroupName { get; set; }
    public string? GroupColor { get; set; }
    public TrainingType Type { get; set; }
    public string TypeName => Type.ToString();
    public TrainingSource Source { get; set; }
    public string SourceName => Source.ToString();
    public TrainingLevel Level { get; set; }
    public string LevelName => Level.ToString();
    public int DurationDays { get; set; }
    public int DurationHours { get; set; }
    public decimal CostPerParticipant { get; set; }
    public string Currency { get; set; } = "GHS";
    public bool ProvidesCertificate { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Training Category Option (user-configurable program categories)
// ─────────────────────────────────────────────────────────────────────────────

public class TrainingCategoryOptionDto : BaseDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int ProgramsCount { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Training Program Group (optional curriculum grouping above Program)
// ─────────────────────────────────────────────────────────────────────────────

public class TrainingProgramGroupDto : BaseDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int ProgramsCount { get; set; }
}

public class CreateTrainingProgramGroupDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(9)]
    [RegularExpression(Shared.Constants.Colors.HexPattern, ErrorMessage = Shared.Constants.Colors.HexMessage)]
    public string? ColorHex { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpdateTrainingProgramGroupDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(9)]
    [RegularExpression(Shared.Constants.Colors.HexPattern, ErrorMessage = Shared.Constants.Colors.HexMessage)]
    public string? ColorHex { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class CreateTrainingCategoryOptionDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(9)]
    [RegularExpression(Shared.Constants.Colors.HexPattern, ErrorMessage = Shared.Constants.Colors.HexMessage)]
    public string? ColorHex { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpdateTrainingCategoryOptionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(9)]
    [RegularExpression(Shared.Constants.Colors.HexPattern, ErrorMessage = Shared.Constants.Colors.HexMessage)]
    public string? ColorHex { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class CreateTrainingProgramDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string ProgramCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public Guid? CategoryOptionId { get; set; }

    public Guid? ProgramGroupId { get; set; }

    [Required]
    public TrainingType Type { get; set; }

    [Required]
    public TrainingSource Source { get; set; }

    [Required]
    public TrainingLevel Level { get; set; }

    [Range(0, 365)]
    public int DurationDays { get; set; }

    [Range(0, 2920)]
    public int DurationHours { get; set; }

    [MaxLength(2000)]
    public string? Prerequisites { get; set; }

    [MaxLength(4000)]
    public string? LearningObjectives { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CostPerParticipant { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public bool IncludesAccommodation { get; set; }
    public bool IncludesMeals { get; set; }
    public bool IncludesTransport { get; set; }

    public bool ProvidesCertificate { get; set; }

    [MaxLength(200)]
    public string? CertificateName { get; set; }

    [Range(1, 120)]
    public int? CertificateValidityMonths { get; set; }

    [Range(1, 1000)]
    public int? MinParticipants { get; set; }

    [Range(1, 1000)]
    public int? MaxParticipants { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; }

    public bool RequiresServiceBond { get; set; }

    [Range(1, 120)]
    public int? ServiceBondMonths { get; set; }

    [MaxLength(4000)]
    public string? ServiceBondTerms { get; set; }
}

public class UpdateTrainingProgramDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public Guid? CategoryOptionId { get; set; }

    public Guid? ProgramGroupId { get; set; }

    [Required]
    public TrainingType Type { get; set; }

    [Required]
    public TrainingSource Source { get; set; }

    [Required]
    public TrainingLevel Level { get; set; }

    [Range(0, 365)]
    public int DurationDays { get; set; }

    [Range(0, 2920)]
    public int DurationHours { get; set; }

    [MaxLength(2000)]
    public string? Prerequisites { get; set; }

    [MaxLength(4000)]
    public string? LearningObjectives { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CostPerParticipant { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public bool IncludesAccommodation { get; set; }
    public bool IncludesMeals { get; set; }
    public bool IncludesTransport { get; set; }

    public bool ProvidesCertificate { get; set; }

    [MaxLength(200)]
    public string? CertificateName { get; set; }

    [Range(1, 120)]
    public int? CertificateValidityMonths { get; set; }

    [Range(1, 1000)]
    public int? MinParticipants { get; set; }

    [Range(1, 1000)]
    public int? MaxParticipants { get; set; }

    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }

    public bool RequiresServiceBond { get; set; }

    [Range(1, 120)]
    public int? ServiceBondMonths { get; set; }

    [MaxLength(4000)]
    public string? ServiceBondTerms { get; set; }
}

#endregion

// ============================================================================
// TRAINING MATERIAL DTOs
// ============================================================================

#region Training Material

public class TrainingMaterialDto : BaseDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public MaterialType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? FilePath { get; set; }
    public string? ExternalUrl { get; set; }
    public bool IsPublic { get; set; }
    public bool IsActive { get; set; }
    public DateTime UploadDate { get; set; }
}

public class CreateTrainingMaterialDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(200)]
    public string MaterialName { get; set; } = string.Empty;

    [Required]
    public MaterialType Type { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }

    [MaxLength(1000)]
    public string? ExternalUrl { get; set; }

    public bool IsPublic { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
}

public class UpdateTrainingMaterialDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string MaterialName { get; set; } = string.Empty;

    [Required]
    public MaterialType Type { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }

    [MaxLength(1000)]
    public string? ExternalUrl { get; set; }

    public bool IsPublic { get; set; }
    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// TRAINING PROGRAM COMPETENCY / SKILL DTOs
// ============================================================================

#region Training Program Competency & Skill

public class TrainingProgramCompetencyDto : BaseDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public Guid CompetencyId { get; set; }
    public string CompetencyCode { get; set; } = string.Empty;
    public string CompetencyName { get; set; } = string.Empty;

    [Range(1, 5)]
    public int TargetLevel { get; set; }
}

public class CreateTrainingProgramCompetencyDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public Guid CompetencyId { get; set; }

    [Required]
    [Range(1, 5)]
    public int TargetLevel { get; set; }
}

public class TrainingProgramSkillDto : BaseDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public ProficiencyLevel TargetProficiency { get; set; }
    public string TargetProficiencyName => TargetProficiency.ToString();
}

public class CreateTrainingProgramSkillDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public ProficiencyLevel TargetProficiency { get; set; }
}

#endregion

// ============================================================================
// TRAINING SCHEDULE DTOs
// ============================================================================

#region Training Schedule

public class TrainingScheduleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;

    public Guid ProgramId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Venue { get; set; }
    public string? VenueAddress { get; set; }
    public string? OnlineLink { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    public Guid? TrainerProfileId { get; set; }
    public string? TrainerName { get; set; }
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }

    public int MaxParticipants { get; set; }
    public TrainingPriority Priority { get; set; } = TrainingPriority.Medium;
    public int ConfirmedParticipantsCount { get; set; }
    public int SlotsAvailable => MaxParticipants - ConfirmedParticipantsCount;

    public DateTime RegistrationOpenDate { get; set; }
    public DateTime RegistrationCloseDate { get; set; }

    public ScheduleStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public decimal ActualCost { get; set; }
    public string? BudgetNotes { get; set; }
    public Guid? TrainingBudgetId { get; set; }
    public string? BudgetCode { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }

    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledDate { get; set; }

    // The cancellation audit was half-exposed: the DTO carried when and why but never who, while the
    // approval side above exposes both ApprovedById and ApprovedByName. Only the id is offered here —
    // TrainingSchedule.CancelledById is a bare scalar with no paired navigation, so there is no
    // Employee to read a name from without a model change. Resolve it client-side if a name is needed.
    public Guid? CancelledById { get; set; }

    public List<TrainingSessionDto> Sessions { get; set; } = new();
}

public class TrainingScheduleSummaryDto
{
    public Guid Id { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Venue { get; set; }
    public string? TrainerName { get; set; }
    public string? VendorName { get; set; }
    public int MaxParticipants { get; set; }
    public TrainingPriority Priority { get; set; } = TrainingPriority.Medium;
    public int ConfirmedParticipantsCount { get; set; }
    public ScheduleStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime RegistrationCloseDate { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Trainer availability / conflict check
// ─────────────────────────────────────────────────────────────────────────────

public class TrainerAvailabilityCheckDto
{
    public Guid TrainerProfileId { get; set; }
    public bool HasConflicts { get; set; }
    public List<TrainerScheduleConflictDto> ConflictingSchedules { get; set; } = new();
    public List<TrainerBlockedPeriodDto> BlockedPeriods { get; set; } = new();
}

public class TrainerScheduleConflictDto
{
    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TrainingPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public ScheduleStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class TrainerBlockedPeriodDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public TrainerEngagementType? EngagementType { get; set; }
    public string? EngagementTypeName => EngagementType?.ToString();
    public string? Notes { get; set; }
}

public class CreateTrainingScheduleDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [MaxLength(200)]
    public string? Venue { get; set; }

    [MaxLength(500)]
    public string? VenueAddress { get; set; }

    [MaxLength(1000)]
    public string? OnlineLink { get; set; }

    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    public Guid? TrainerProfileId { get; set; }
    public Guid? VendorId { get; set; }

    [Required]
    [Range(1, 10000)]
    public int MaxParticipants { get; set; }
    public TrainingPriority Priority { get; set; } = TrainingPriority.Medium;

    [Required]
    public DateTime RegistrationOpenDate { get; set; }

    [Required]
    public DateTime RegistrationCloseDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ActualCost { get; set; }

    [MaxLength(1000)]
    public string? BudgetNotes { get; set; }

    public Guid? TrainingBudgetId { get; set; }
}

public class UpdateTrainingScheduleDto : UpdateDtoBase
{
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [MaxLength(200)]
    public string? Venue { get; set; }

    [MaxLength(500)]
    public string? VenueAddress { get; set; }

    [MaxLength(1000)]
    public string? OnlineLink { get; set; }

    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    public Guid? TrainerProfileId { get; set; }
    public Guid? VendorId { get; set; }

    [Range(1, 10000)]
    public int MaxParticipants { get; set; }
    public TrainingPriority Priority { get; set; } = TrainingPriority.Medium;

    [Required]
    public DateTime RegistrationOpenDate { get; set; }

    [Required]
    public DateTime RegistrationCloseDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ActualCost { get; set; }

    [MaxLength(1000)]
    public string? BudgetNotes { get; set; }

    public Guid? TrainingBudgetId { get; set; }
}

public class ApproveTrainingScheduleDto
{
    [Required]
    public Guid ScheduleId { get; set; }

    // Approver and approval timestamp come from the authenticated user / server clock.
}

public class CancelTrainingScheduleDto
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;

    // Canceller and cancellation timestamp come from the authenticated user / server clock, matching
    // ApproveTrainingScheduleDto above. CancelledById used to be a [Required] non-nullable Guid taken
    // straight off the body — which is a no-op attribute on a value type (an omitted value became
    // Guid.Empty), and let any caller attribute a cancellation to another employee.
}

public class CompleteTrainingScheduleDto
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public DateTime CompletionDate { get; set; }

    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }
}

#endregion

// ============================================================================
// TRAINING SESSION DTOs
// ============================================================================

#region Training Session

public class TrainingSessionDto : BaseDto
{
    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? Description { get; set; }
}

public class CreateTrainingSessionDto : CreateDtoBase
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }
}

public class UpdateTrainingSessionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }
}

#endregion

// ============================================================================
// TRAINING NOMINATION DTOs
// ============================================================================

#region Training Nomination

public class TrainingNominationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;

    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;

    // Named the programme but never identified it, so a caller holding a nomination could not issue
    // a certificate against it (IssueCertificateDto needs ProgramId) without a second round trip.
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public DateTime TrainingStartDate { get; set; }
    public DateTime TrainingEndDate { get; set; }

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? EmployeeDepartment { get; set; }
    public string? EmployeePosition { get; set; }

    public NominationType Type { get; set; }
    public string TypeName => Type.ToString();

    public Guid? NominatedById { get; set; }
    public string? NominatedByName { get; set; }

    public DateTime NominationDate { get; set; }
    public string? Justification { get; set; }

    public Guid? TrainingNeedsAssessmentId { get; set; }

    public NominationStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public Guid? SupervisorApprovedById { get; set; }
    public string? SupervisorApprovedByName { get; set; }
    public DateTime? SupervisorApprovalDate { get; set; }
    public string? SupervisorComments { get; set; }

    public Guid? HrApprovedById { get; set; }
    public string? HrApprovedByName { get; set; }
    public DateTime? HrApprovalDate { get; set; }
    public string? HrComments { get; set; }

    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    public decimal? ActualCost { get; set; }
    public bool EmployeeContributed { get; set; }
    public decimal? EmployeeContribution { get; set; }

    public bool HasCompletionRecord { get; set; }
    public TrainingCompletionSummaryDto? CompletionRecord { get; set; }
}

public class TrainingNominationSummaryDto
{
    public Guid Id { get; set; }
    public string NominationNumber { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    // The summary named the employee but never identified them, so a caller holding a schedule's
    // nominee list could not act on a row (mark attendance, record a completion) without fetching
    // each nomination individually. ScheduleId is here for the same reason.
    public Guid EmployeeId { get; set; }
    public Guid ScheduleId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public NominationType Type { get; set; }
    public string TypeName => Type.ToString();
    public NominationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime NominationDate { get; set; }
    public DateTime TrainingStartDate { get; set; }
}

public class CreateTrainingNominationDto : CreateDtoBase
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public NominationType Type { get; set; }

    // NominatedById is taken from the authenticated employee, not the request body.

    public DateTime NominationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? Justification { get; set; }

    public Guid? TrainingNeedsAssessmentId { get; set; }

    /// <summary>When true, the nomination is saved as Draft instead of being immediately submitted.</summary>
    public bool SaveAsDraft { get; set; }
}

/// <summary>Nominate multiple employees to one schedule in a single request.</summary>
public class BulkCreateTrainingNominationDto
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> EmployeeIds { get; set; } = new();

    [Required]
    public NominationType Type { get; set; }

    // NominatedById is taken from the authenticated employee, not the request body.

    [MaxLength(2000)]
    public string? Justification { get; set; }

    public bool SaveAsDraft { get; set; }
}

public class BulkNominationResultDto
{
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public List<BulkNominationSkipDto> Skipped { get; set; } = new();
}

public class NomineeAvailabilityCheckRequestDto
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> EmployeeIds { get; set; } = new();
}

/// <summary>A conflicting commitment that overlaps a proposed training window for a nominee.</summary>
public class NomineeConflictDto
{
    public Guid EmployeeId { get; set; }
    public string Source { get; set; } = string.Empty; // Leave, Travel, Training
    public string Description { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class BulkNominationSkipDto
{
    public Guid EmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ApproveNominationDto
{
    [Required]
    public Guid NominationId { get; set; }

    /// <summary>The role making this approval decision ("Supervisor" or "HR").</summary>
    [Required]
    public string ApproverRole { get; set; } = string.Empty;

    // Approver and approval timestamp come from the authenticated employee / server clock.

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class RejectNominationDto
{
    [Required]
    public Guid NominationId { get; set; }

    // Rejector is taken from the authenticated employee; rejection timestamp is server UTC.

    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

public class WithdrawNominationDto
{
    [Required]
    public Guid NominationId { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }
}

public class UpdateTrainingNominationDto
{
    [Required]
    public NominationType Type { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    public Guid? TrainingNeedsAssessmentId { get; set; }

    public decimal? ActualCost { get; set; }

    public bool EmployeeContributed { get; set; }

    public decimal? EmployeeContribution { get; set; }
}

#endregion

// ============================================================================
// TRAINING COMPLETION DTOs
// ============================================================================

#region Training Completion

public class TrainingCompletionDto : BaseDto
{
    public Guid NominationId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public string ProgramName { get; set; } = string.Empty;

    public DateTime CompletionDate { get; set; }
    public TrainingCompletionStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public decimal? FinalScore { get; set; }
    public decimal? PreAssessmentScore { get; set; }
    public decimal? PostAssessmentScore { get; set; }
    public bool IsPassed { get; set; }

    public bool IsVerifiedByManager { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerificationDate { get; set; }
    public string? VerificationNotes { get; set; }
}

public class TrainingCompletionSummaryDto
{
    public Guid Id { get; set; }
    public DateTime CompletionDate { get; set; }
    public TrainingCompletionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal? FinalScore { get; set; }
    public bool IsPassed { get; set; }
    public bool IsVerifiedByManager { get; set; }
}

public class RecordTrainingCompletionDto : CreateDtoBase
{
    [Required]
    public Guid NominationId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateTime CompletionDate { get; set; }

    [Required]
    public TrainingCompletionStatus Status { get; set; }

    [Range(0, 100)]
    public decimal? FinalScore { get; set; }

    [Range(0, 100)]
    public decimal? PreAssessmentScore { get; set; }

    [Range(0, 100)]
    public decimal? PostAssessmentScore { get; set; }

    public bool IsPassed { get; set; }
}

/// <summary>Record completion for many participants of one schedule in a single request.</summary>
public class BulkRecordCompletionDto
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    [MinLength(1)]
    public List<BulkCompletionItemDto> Items { get; set; } = new();
}

public class BulkCompletionItemDto
{
    [Required]
    public Guid NominationId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public DateTime CompletionDate { get; set; } = DateTime.UtcNow;
    public TrainingCompletionStatus Status { get; set; } = TrainingCompletionStatus.Completed;

    [Range(0, 100)]
    public decimal? FinalScore { get; set; }

    public bool IsPassed { get; set; } = true;
}

public class BulkCompletionResultDto
{
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public List<BulkCompletionSkipDto> Skipped { get; set; } = new();
}

public class BulkCompletionSkipDto
{
    public Guid NominationId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class VerifyTrainingCompletionDto
{
    [Required]
    public Guid CompletionId { get; set; }

    // Verifier and verification timestamp come from the authenticated employee / server clock.

    [MaxLength(2000)]
    public string? VerificationNotes { get; set; }
}

public class UpdateTrainingCompletionDto : UpdateDtoBase
{
    [Required]
    public DateTime CompletionDate { get; set; }

    [Required]
    public TrainingCompletionStatus Status { get; set; }

    [Range(0, 100)]
    public decimal? FinalScore { get; set; }

    [Range(0, 100)]
    public decimal? PreAssessmentScore { get; set; }

    [Range(0, 100)]
    public decimal? PostAssessmentScore { get; set; }

    public bool IsPassed { get; set; }
}

#endregion

// ============================================================================
// TRAINING ATTENDANCE DTOs
// ============================================================================

#region Training Attendance

public class TrainingAttendanceDto : BaseDto
{
    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public Guid? NominationId { get; set; }
    public string? NominationNumber { get; set; }

    public DateTime AttendanceDate { get; set; }
    public bool IsPresent { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    public string? AbsenceReason { get; set; }
    public string? Notes { get; set; }

    public Guid? MarkedById { get; set; }
    public string? MarkedByName { get; set; }
    public DateTime? MarkedAt { get; set; }
}

public class MarkAttendanceDto : CreateDtoBase
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? NominationId { get; set; }

    [Required]
    public DateTime AttendanceDate { get; set; }

    public bool IsPresent { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }

    [MaxLength(500)]
    public string? AbsenceReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // MarkedById / MarkedAt come from the authenticated employee / server clock.
}

public class BulkMarkAttendanceDto
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public DateTime AttendanceDate { get; set; }

    // MarkedById comes from the authenticated employee.

    [Required]
    public List<AttendanceEntryDto> Entries { get; set; } = new();
}

public class AttendanceEntryDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    public bool IsPresent { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }

    [MaxLength(500)]
    public string? AbsenceReason { get; set; }
}

#endregion

// ============================================================================
// TRAINING FEEDBACK DTOs
// ============================================================================

#region Training Feedback

public class TrainingFeedbackDto : BaseDto
{
    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    public Guid? NominationId { get; set; }

    public int? ContentRelevanceRating { get; set; }
    public int? TrainerKnowledgeRating { get; set; }
    public int? DeliveryMethodRating { get; set; }
    public int? MaterialQualityRating { get; set; }
    public int? VenueFacilitiesRating { get; set; }
    public int? OverallSatisfactionRating { get; set; }
    public decimal? AverageRating => (ContentRelevanceRating.HasValue || TrainerKnowledgeRating.HasValue
        || DeliveryMethodRating.HasValue || MaterialQualityRating.HasValue || OverallSatisfactionRating.HasValue)
        ? (decimal?)new[] {
            ContentRelevanceRating ?? 0, TrainerKnowledgeRating ?? 0, DeliveryMethodRating ?? 0,
            MaterialQualityRating ?? 0, OverallSatisfactionRating ?? 0
          }.Where(r => r > 0).DefaultIfEmpty(0).Average()
        : null;

    public string? StrengthsOfTraining { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? SuggestionsForFuture { get; set; }
    public string? AdditionalComments { get; set; }

    public bool WouldRecommend { get; set; }
    public int? LikelihoodToApply { get; set; }
    public string? ExpectedApplicationOnJob { get; set; }
    public string? BarriersToApplication { get; set; }

    public DateTime FeedbackDate { get; set; }
}

public class SubmitTrainingFeedbackDto : CreateDtoBase
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? NominationId { get; set; }

    [Range(1, 5)]
    public int? ContentRelevanceRating { get; set; }

    [Range(1, 5)]
    public int? TrainerKnowledgeRating { get; set; }

    [Range(1, 5)]
    public int? DeliveryMethodRating { get; set; }

    [Range(1, 5)]
    public int? MaterialQualityRating { get; set; }

    [Range(1, 5)]
    public int? VenueFacilitiesRating { get; set; }

    [Range(1, 5)]
    public int? OverallSatisfactionRating { get; set; }

    [MaxLength(2000)]
    public string? StrengthsOfTraining { get; set; }

    [MaxLength(2000)]
    public string? AreasForImprovement { get; set; }

    [MaxLength(2000)]
    public string? SuggestionsForFuture { get; set; }

    [MaxLength(2000)]
    public string? AdditionalComments { get; set; }

    public bool WouldRecommend { get; set; }

    [Range(1, 5)]
    public int? LikelihoodToApply { get; set; }

    [MaxLength(2000)]
    public string? ExpectedApplicationOnJob { get; set; }

    [MaxLength(2000)]
    public string? BarriersToApplication { get; set; }

    public DateTime FeedbackDate { get; set; } = DateTime.UtcNow;
}

#endregion

// ============================================================================
// TRAINING FOLLOW-UP ASSESSMENT DTOs
// ============================================================================

#region Training Follow-Up Assessment

public class TrainingFollowUpAssessmentDto : BaseDto
{
    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    public Guid? NominationId { get; set; }

    public TrainingAssessmentType AssessmentType { get; set; }
    public string AssessmentTypeName => AssessmentType.ToString();
    public DateTime AssessmentDate { get; set; }

    public string? KeyLearningsTaken { get; set; }
    public string? ConceptsStillUnclear { get; set; }

    public int? FrequencyOfUse { get; set; }
    public string? HowSkillsApplied { get; set; }
    public string? BarriersToApplication { get; set; }
    public string? SupportNeeded { get; set; }

    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerObservationNotes { get; set; }
    public DateTime? ManagerSubmittedDate { get; set; }

    public bool RecommendFurtherTraining { get; set; }
    public string? RecommendedFollowUp { get; set; }
}

public class SubmitFollowUpAssessmentDto : CreateDtoBase
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? NominationId { get; set; }

    [Required]
    public TrainingAssessmentType AssessmentType { get; set; }

    public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;

    [MaxLength(4000)]
    public string? KeyLearningsTaken { get; set; }

    [MaxLength(2000)]
    public string? ConceptsStillUnclear { get; set; }

    [Range(1, 5)]
    public int? FrequencyOfUse { get; set; }

    [MaxLength(2000)]
    public string? HowSkillsApplied { get; set; }

    [MaxLength(2000)]
    public string? BarriersToApplication { get; set; }

    [MaxLength(2000)]
    public string? SupportNeeded { get; set; }

    public bool RecommendFurtherTraining { get; set; }

    [MaxLength(2000)]
    public string? RecommendedFollowUp { get; set; }
}

public class SubmitManagerObservationDto
{
    [Required]
    public Guid AssessmentId { get; set; }

    // ManagerId and submitted timestamp come from the authenticated employee / server clock.

    [Required]
    [MaxLength(2000)]
    public string ManagerObservationNotes { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// TRAINING CERTIFICATE DTOs
// ============================================================================

#region Training Certificate

public class TrainingCertificateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string? VerificationCode { get; set; }

    public Guid NominationId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;

    public string CertificateName { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ValidityMonths { get; set; }
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;
    public int? DaysUntilExpiry => ExpiryDate.HasValue
        ? (int?)(ExpiryDate.Value - DateTime.UtcNow).TotalDays
        : null;

    public CertificateStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? RevokedDate { get; set; }
    public string? RevokedReason { get; set; }

    public string? FilePath { get; set; }
    public string? ExternalUrl { get; set; }

    public bool IsRenewal { get; set; }
    public Guid? PreviousCertificateId { get; set; }
    public string? PreviousCertificateNumber { get; set; }

    public Guid? IssuedById { get; set; }
    public string? IssuedByName { get; set; }
}

public class TrainingCertificateSummaryDto
{
    public Guid Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string? VerificationCode { get; set; }

    // The summary named the holder and the programme but identified neither, so a certificates list
    // could not link through to either. Mapped from the FKs, so they survive a missed include.
    public Guid EmployeeId { get; set; }
    public Guid ProgramId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string CertificateName { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;
    public CertificateStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

/// <summary>
/// Public (anonymous) certificate-verification result. Deliberately exposes only what a third
/// party needs to trust a presented certificate — no internal ids, tenant, or contact data.
/// </summary>
public class CertificateVerificationResultDto
{
    /// <summary>False when no certificate matches the supplied code.</summary>
    public bool Found { get; set; }

    /// <summary>True only when the certificate is Active and not expired/revoked.</summary>
    public bool IsValid { get; set; }

    /// <summary>Active / Revoked / Expired / NotFound.</summary>
    public string Status { get; set; } = "NotFound";

    public string? CertificateNumber { get; set; }
    public string? VerificationCode { get; set; }
    public string? CertificateName { get; set; }
    public string? EmployeeName { get; set; }
    public string? ProgramName { get; set; }
    public string? IssuingOrganization { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsRevoked { get; set; }
    public string? RevokedReason { get; set; }
}

public class IssueCertificateDto : CreateDtoBase
{
    [Required]
    public Guid NominationId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CertificateName { get; set; } = string.Empty;

    [Required]
    public DateTime IssuedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [Range(1, 120)]
    public int? ValidityMonths { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }

    [MaxLength(1000)]
    public string? ExternalUrl { get; set; }

    public bool IsRenewal { get; set; }
    public Guid? PreviousCertificateId { get; set; }

    // IssuedById is taken from the authenticated employee, not the request body.
}

public class RevokeCertificateDto
{
    [Required]
    public Guid CertificateId { get; set; }

    // Revoker and revocation timestamp come from the authenticated employee / server clock.

    [Required]
    [MaxLength(1000)]
    public string RevokedReason { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// EMPLOYEE CERTIFICATE DTOs
// ============================================================================

#region Employee Certificate

public class EmployeeCertificateDto : BaseDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public string CertificateName { get; set; } = string.Empty;
    public string IssuingBody { get; set; } = string.Empty;
    public string? CertificateNumber { get; set; }

    public DateTime IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;

    public CertificateStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public SkillCategory? Category { get; set; }
    public string? CategoryName => Category?.ToString();
    public string? Description { get; set; }
    public string? FilePath { get; set; }

    public bool IsVerified { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedDate { get; set; }
}

public class EmployeeCertificateSummaryDto
{
    public Guid Id { get; set; }

    // Same gap: the unverified queue is org-wide, so a row has to say whose certificate it is.
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string CertificateName { get; set; } = string.Empty;
    public string IssuingBody { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;
    public CertificateStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsVerified { get; set; }
}

public class CreateEmployeeCertificateDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CertificateName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string IssuingBody { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [Required]
    public DateTime IssuedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public SkillCategory? Category { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }
}

public class VerifyEmployeeCertificateDto
{
    [Required]
    public Guid CertificateId { get; set; }

    // Verifier and verification timestamp come from the authenticated employee / server clock.
}

public class UpdateEmployeeCertificateDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string CertificateName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string IssuingBody { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [Required]
    public DateTime IssuedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public SkillCategory? Category { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }
}

#endregion

// ============================================================================
// COMPLIANCE TRAINING REQUIREMENT DTOs
// ============================================================================

#region Compliance Training Requirement

public class ComplianceTrainingRequirementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RegulatoryReference { get; set; }

    public Guid ProgramId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }

    public ComplianceFrequency Frequency { get; set; }
    public string FrequencyName => Frequency.ToString();
    public int? CustomFrequencyDays { get; set; }
    public int? GracePeriodDays { get; set; }

    public bool IsActive { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? NonComplianceConsequences { get; set; }

    public int TotalAssignedEmployees { get; set; }
    public int CompliantEmployeesCount { get; set; }
    public decimal ComplianceRate => TotalAssignedEmployees > 0
        ? Math.Round((decimal)CompliantEmployeesCount / TotalAssignedEmployees * 100, 1)
        : 0;
}

public class ComplianceTrainingRequirementSummaryDto
{
    public Guid Id { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public ComplianceFrequency Frequency { get; set; }
    public string FrequencyName => Frequency.ToString();
    public bool IsActive { get; set; }
    public DateTime EffectiveDate { get; set; }
    public int TotalAssignedEmployees { get; set; }
    public decimal ComplianceRate { get; set; }
}

public class CreateComplianceTrainingRequirementDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string RequirementCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RequirementName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? RegulatoryReference { get; set; }

    [Required]
    public Guid ProgramId { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }

    [Required]
    public ComplianceFrequency Frequency { get; set; }

    [Range(1, 3650)]
    public int? CustomFrequencyDays { get; set; }

    [Range(0, 365)]
    public int? GracePeriodDays { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? NonComplianceConsequences { get; set; }
}

public class UpdateComplianceTrainingRequirementDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string RequirementName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? RegulatoryReference { get; set; }

    [Required]
    public Guid ProgramId { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }

    [Required]
    public ComplianceFrequency Frequency { get; set; }

    [Range(1, 3650)]
    public int? CustomFrequencyDays { get; set; }

    [Range(0, 365)]
    public int? GracePeriodDays { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? NonComplianceConsequences { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE COMPLIANCE RECORD DTOs
// ============================================================================

#region Employee Compliance Record

public class EmployeeComplianceRecordDto : BaseDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public Guid RequirementId { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public ComplianceStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime AssignedDate { get; set; }
    public DateTime? LastCompletedDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public DateTime? GracePeriodExpiry { get; set; }
    public bool IsOverdue => NextDueDate.HasValue && NextDueDate.Value < DateTime.UtcNow && Status != ComplianceStatus.Compliant;

    public Guid? FulfillingNominationId { get; set; }
    public string? FulfillingNominationNumber { get; set; }

    public bool IsExempt { get; set; }
    public string? ExemptionReason { get; set; }
    public Guid? ExemptedById { get; set; }
    public string? ExemptedByName { get; set; }
    public DateTime? ExemptionDate { get; set; }
    public DateTime? ExemptionExpiryDate { get; set; }

    public DateTime? ReminderSentDate { get; set; }
    public string? Notes { get; set; }
}

public class EmployeeComplianceRecordSummaryDto
{
    public Guid Id { get; set; }

    // The non-compliant and overdue queues are org-wide, so a row has to identify both sides —
    // otherwise a caller cannot open the employee or the requirement it is complaining about.
    public Guid EmployeeId { get; set; }
    public Guid RequirementId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;

    // The queues that consume this summary show which programme satisfies the requirement, and an
    // "Exempt" column is meaningless without saying who granted it.
    public string ProgramName { get; set; } = string.Empty;
    public string? ExemptedByName { get; set; }
    public ComplianceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? NextDueDate { get; set; }
    public bool IsOverdue => NextDueDate.HasValue && NextDueDate.Value < DateTime.UtcNow && Status != ComplianceStatus.Compliant;
    public bool IsExempt { get; set; }
}

public class ExemptEmployeeComplianceDto
{
    [Required]
    public Guid RecordId { get; set; }

    // Exemptor and exemption timestamp come from the authenticated employee / server clock.

    [Required]
    [MaxLength(1000)]
    public string ExemptionReason { get; set; } = string.Empty;

    public DateTime? ExemptionExpiryDate { get; set; }
}

#endregion

// ============================================================================
// TRAINING BUDGET DTOs
// ============================================================================

#region Training Budget

public class TrainingBudgetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string BudgetCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public string PeriodDescription => Quarter.HasValue ? $"Q{Quarter}/{Year}" : $"FY{Year}";

    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    public string Currency { get; set; } = "GHS";
    public decimal AllocatedAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount => AllocatedAmount - SpentAmount - CommittedAmount;
    public decimal UtilizationRate => AllocatedAmount > 0
        ? Math.Round(SpentAmount / AllocatedAmount * 100, 1)
        : 0;

    public TrainingBudgetStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? GLAccountCode { get; set; }
    public string? CostCenterCode { get; set; }
    public string? Notes { get; set; }
}

public class TrainingBudgetSummaryDto
{
    public Guid Id { get; set; }
    public string BudgetCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public string PeriodDescription => Quarter.HasValue ? $"Q{Quarter}/{Year}" : $"FY{Year}";
    public string? OrganizationUnitName { get; set; }
    public string Currency { get; set; } = "GHS";
    public decimal AllocatedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public TrainingBudgetStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateTrainingBudgetDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string BudgetCode { get; set; } = string.Empty;

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Required]
    [Range(0, double.MaxValue)]
    public decimal AllocatedAmount { get; set; }

    [MaxLength(50)]
    public string? GLAccountCode { get; set; }

    [MaxLength(50)]
    public string? CostCenterCode { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateTrainingBudgetDto : UpdateDtoBase
{
    [Range(0, double.MaxValue)]
    public decimal AllocatedAmount { get; set; }

    [MaxLength(50)]
    public string? GLAccountCode { get; set; }

    [MaxLength(50)]
    public string? CostCenterCode { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ApproveTrainingBudgetDto
{
    [Required]
    public Guid BudgetId { get; set; }

    // Approver and approval timestamp come from the authenticated employee / server clock.
}

#endregion

// ============================================================================
// TRAINING BUDGET TRANSACTION DTOs
// ============================================================================

#region Training Budget Transaction

public class TrainingBudgetTransactionDto : BaseDto
{
    public Guid BudgetId { get; set; }
    public string BudgetCode { get; set; } = string.Empty;

    public Guid? ScheduleId { get; set; }
    public string? ScheduleNumber { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountType => Amount >= 0 ? "Debit" : "Credit";
    public DateTime TransactionDate { get; set; }

    public Guid? RecordedById { get; set; }
    public string? RecordedByName { get; set; }
    public string? Reference { get; set; }
    public string? GLAccountCode { get; set; }
    public string? VoucherNumber { get; set; }
    public string? Notes { get; set; }
}

public class CreateTrainingBudgetTransactionDto : CreateDtoBase
{
    [Required]
    public Guid BudgetId { get; set; }

    public Guid? ScheduleId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    public Guid RecordedById { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    [MaxLength(50)]
    public string? GLAccountCode { get; set; }

    [MaxLength(50)]
    public string? VoucherNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// TRAINING PLAN DTOs
// ============================================================================

#region Training Plan

public class TrainingPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public int Year { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    public TrainingPlanStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Notes { get; set; }

    public int TotalItemsCount { get; set; }
    public int CompletedItemsCount { get; set; }
    public decimal CompletionRate => TotalItemsCount > 0
        ? Math.Round((decimal)CompletedItemsCount / TotalItemsCount * 100, 1)
        : 0;

    public List<TrainingPlanItemDto> Items { get; set; } = new();
    public List<TrainingPlanBudgetLineDto> BudgetLines { get; set; } = new();
}

public class TrainingPlanSummaryDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? OrganizationUnitName { get; set; }
    public TrainingPlanStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int TotalItemsCount { get; set; }
    public int CompletedItemsCount { get; set; }
    public DateTime? ApprovalDate { get; set; }
}

public class CreateTrainingPlanDto : CreateDtoBase
{
    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateTrainingPlanDto : UpdateDtoBase
{
    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ApproveTrainingPlanDto
{
    [Required]
    public Guid PlanId { get; set; }

    // Approver and approval timestamp come from the authenticated employee / server clock.
}

#endregion

// ============================================================================
// TRAINING PLAN ITEM DTOs
// ============================================================================

#region Training Plan Item

public class TrainingPlanItemDto : BaseDto
{
    public Guid PlanId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;

    public Guid? ProgramId { get; set; }
    public string? ProgramCode { get; set; }

    public string TrainingTitle { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int Quarter { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }

    public int EstimatedParticipants { get; set; }
    public decimal EstimatedCost { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
    public int? ActualParticipants { get; set; }
    public decimal? ActualCost { get; set; }

    public Guid? FulfilledByScheduleId { get; set; }
    public string? FulfilledByScheduleNumber { get; set; }
}

public class CreateTrainingPlanItemDto : CreateDtoBase
{
    [Required]
    public Guid PlanId { get; set; }

    public Guid? ProgramId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TrainingTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Range(1, 4)]
    public int Quarter { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }

    [Range(0, 10000)]
    public int EstimatedParticipants { get; set; }

    [Range(0, double.MaxValue)]
    public decimal EstimatedCost { get; set; }
}

public class UpdateTrainingPlanItemDto : UpdateDtoBase
{
    public Guid? ProgramId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TrainingTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Range(1, 4)]
    public int Quarter { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }

    [Range(0, 10000)]
    public int EstimatedParticipants { get; set; }

    [Range(0, double.MaxValue)]
    public decimal EstimatedCost { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }

    [Range(0, 10000)]
    public int? ActualParticipants { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    public Guid? FulfilledByScheduleId { get; set; }
}

#endregion

// ============================================================================
// TRAINING PLAN BUDGET LINE DTOs
// ============================================================================

#region Training Plan Budget Line

public class TrainingPlanBudgetLineDto : BaseDto
{
    public Guid PlanId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal Variance => BudgetedAmount - ActualAmount - CommittedAmount;
    public string? Notes { get; set; }
}

public class CreateTrainingPlanBudgetLineDto : CreateDtoBase
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal BudgetedAmount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateTrainingPlanBudgetLineDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal BudgetedAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ActualAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CommittedAmount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// TRAINING NEEDS ASSESSMENT DTOs
// ============================================================================

#region Training Needs Assessment

public class TrainingNeedsAssessmentDto : BaseDto
{
    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? EmployeeDepartment { get; set; }
    public string? EmployeePosition { get; set; }

    public int Year { get; set; }

    public AssessmentSource Source { get; set; }
    public string SourceName => Source.ToString();

    public string IdentifiedGaps { get; set; } = string.Empty;

    public TrainingPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();

    public Guid? IdentifiedById { get; set; }
    public string? IdentifiedByName { get; set; }
    public DateTime IdentifiedDate { get; set; }

    public string? AdditionalNotes { get; set; }

    public bool TrainingProvided { get; set; }
    public DateTime? TrainingProvidedDate { get; set; }

    public List<TrainingNeedsAssessmentProgramDto> RecommendedPrograms { get; set; } = new();
    public List<TrainingNeedsAssessmentSkillDto> SkillGaps { get; set; } = new();
}

public class TrainingNeedsAssessmentSummaryDto
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public AssessmentSource Source { get; set; }
    public string SourceName => Source.ToString();
    public TrainingPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public bool TrainingProvided { get; set; }
    public int RecommendedProgramsCount { get; set; }
    public int SkillGapsCount { get; set; }
}

public class CreateTrainingNeedsAssessmentDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    public AssessmentSource Source { get; set; }

    [Required]
    [MaxLength(4000)]
    public string IdentifiedGaps { get; set; } = string.Empty;

    [Required]
    public TrainingPriority Priority { get; set; }

    [Required]
    public Guid IdentifiedById { get; set; }

    public DateTime IdentifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
}

/// <summary>Create the same needs-assessment for many employees at once (bulk).</summary>
public class BulkCreateTrainingNeedsAssessmentDto
{
    [Required]
    [MinLength(1)]
    public List<Guid> EmployeeIds { get; set; } = new();

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    public AssessmentSource Source { get; set; }

    [Required]
    [MaxLength(4000)]
    public string IdentifiedGaps { get; set; } = string.Empty;

    [Required]
    public TrainingPriority Priority { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
}

public class BulkNeedsAssessmentResultDto
{
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
}

public class UpdateTrainingNeedsAssessmentDto : UpdateDtoBase
{
    [Required]
    public AssessmentSource Source { get; set; }

    [Required]
    [MaxLength(4000)]
    public string IdentifiedGaps { get; set; } = string.Empty;

    [Required]
    public TrainingPriority Priority { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }

    public bool TrainingProvided { get; set; }
    public DateTime? TrainingProvidedDate { get; set; }
}

#endregion

// ============================================================================
// TRAINING NEEDS ASSESSMENT PROGRAM DTOs
// ============================================================================

#region Training Needs Assessment Program & Skill

public class TrainingNeedsAssessmentProgramDto : BaseDto
{
    public Guid AssessmentId { get; set; }
    public Guid ProgramId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public TrainingPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public string? Rationale { get; set; }
}

public class CreateTrainingNeedsAssessmentProgramDto : CreateDtoBase
{
    [Required]
    public Guid AssessmentId { get; set; }

    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public TrainingPriority Priority { get; set; }

    [MaxLength(2000)]
    public string? Rationale { get; set; }
}

public class TrainingNeedsAssessmentSkillDto : BaseDto
{
    public Guid AssessmentId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public ProficiencyLevel CurrentProficiency { get; set; }
    public string CurrentProficiencyName => CurrentProficiency.ToString();
    public ProficiencyLevel RequiredProficiency { get; set; }
    public string RequiredProficiencyName => RequiredProficiency.ToString();
    public TrainingPriority GapPriority { get; set; }
    public string GapPriorityName => GapPriority.ToString();
}

public class CreateTrainingNeedsAssessmentSkillDto : CreateDtoBase
{
    [Required]
    public Guid AssessmentId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public ProficiencyLevel CurrentProficiency { get; set; }

    [Required]
    public ProficiencyLevel RequiredProficiency { get; set; }

    [Required]
    public TrainingPriority GapPriority { get; set; }
}

#endregion

// ============================================================================
// TRAINING WAITLIST DTOs
// ============================================================================

#region Training Waitlist

public class TrainingWaitlistDto : BaseDto
{
    public Guid ScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public int Position { get; set; }
    public DateTime AddedDate { get; set; }
    public TrainingWaitlistStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? OfferDate { get; set; }
    public DateTime? OfferExpiryDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public bool? OfferAccepted { get; set; }
    public string? Notes { get; set; }

    public Guid? CreatedNominationId { get; set; }
    public string? CreatedNominationNumber { get; set; }
}

public class CreateTrainingWaitlistDto : CreateDtoBase
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class OfferWaitlistPositionDto
{
    [Required]
    public Guid WaitlistId { get; set; }

    [Required]
    public DateTime OfferDate { get; set; }

    [Required]
    public DateTime OfferExpiryDate { get; set; }
}

public class RespondToWaitlistOfferDto
{
    [Required]
    public Guid WaitlistId { get; set; }

    [Required]
    public bool OfferAccepted { get; set; }

    public DateTime ResponseDate { get; set; } = DateTime.UtcNow;
}

#endregion

// ============================================================================
// TRAINING REQUEST DTOs
// ============================================================================

#region Training Request

public class TrainingRequestDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public string RequestedTrainingTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Justification { get; set; }
    public DateTime RequestDate { get; set; }

    public TrainingRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }

    public Guid? LinkedProgramId { get; set; }
    public string? LinkedProgramName { get; set; }
    public string? LinkedProgramCode { get; set; }
}

public class TrainingRequestSummaryDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string RequestedTrainingTitle { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public TrainingRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? LinkedProgramName { get; set; }
}

public class CreateTrainingRequestDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequestedTrainingTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    public Guid? LinkedProgramId { get; set; }
}

public class ApproveTrainingRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    // Approver and approval timestamp come from the authenticated employee / server clock.

    public Guid? LinkedProgramId { get; set; }
}

public class RejectTrainingRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    // Rejector is taken from the authenticated employee.

    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

public class UpdateTrainingRequestDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string RequestedTrainingTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    public Guid? LinkedProgramId { get; set; }
}

#endregion

// ============================================================================
// LEARNING PATH DTOs
// ============================================================================

#region Learning Path

public class LearningPathDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }

    public LearningPathStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public int? EstimatedDurationDays { get; set; }
    public int? EstimatedDurationHours { get; set; }
    public bool ProvidesCertificate { get; set; }
    public string? CompletionCertificateName { get; set; }

    public int TotalProgramsCount { get; set; }
    public int EnrollmentsCount { get; set; }

    public List<LearningPathProgramDto> Programs { get; set; } = new();
    public List<LearningPathSkillDto> TargetSkills { get; set; } = new();
}

public class LearningPathSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public string? PositionTitle { get; set; }
    public LearningPathStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? EstimatedDurationDays { get; set; }
    public int TotalProgramsCount { get; set; }
    public bool ProvidesCertificate { get; set; }
}

public class CreateLearningPathDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }

    public LearningPathStatus Status { get; set; } = LearningPathStatus.Active;

    [Range(0, 3650)]
    public int? EstimatedDurationDays { get; set; }

    [Range(0, 87600)]
    public int? EstimatedDurationHours { get; set; }

    public bool ProvidesCertificate { get; set; }

    [MaxLength(200)]
    public string? CompletionCertificateName { get; set; }
}

public class UpdateLearningPathDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }

    [Required]
    public LearningPathStatus Status { get; set; }

    [Range(0, 3650)]
    public int? EstimatedDurationDays { get; set; }

    [Range(0, 87600)]
    public int? EstimatedDurationHours { get; set; }

    public bool ProvidesCertificate { get; set; }

    [MaxLength(200)]
    public string? CompletionCertificateName { get; set; }
}

#endregion

// ============================================================================
// LEARNING PATH PROGRAM / SKILL DTOs
// ============================================================================

#region Learning Path Program & Skill

public class LearningPathProgramDto : BaseDto
{
    public Guid LearningPathId { get; set; }
    public string LearningPathName { get; set; } = string.Empty;
    public Guid ProgramId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
    public Guid? PrerequisitePathProgramId { get; set; }
    public string? PrerequisiteProgramName { get; set; }
}

public class CreateLearningPathProgramDto : CreateDtoBase
{
    [Required]
    public Guid LearningPathId { get; set; }

    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    [Range(1, 1000)]
    public int SequenceOrder { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? PrerequisitePathProgramId { get; set; }
}

public class UpdateLearningPathProgramDto : UpdateDtoBase
{
    [Required]
    [Range(1, 1000)]
    public int SequenceOrder { get; set; }

    public bool IsMandatory { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? PrerequisitePathProgramId { get; set; }
}

public class LearningPathSkillDto : BaseDto
{
    public Guid LearningPathId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public ProficiencyLevel TargetProficiency { get; set; }
    public string TargetProficiencyName => TargetProficiency.ToString();
}

public class CreateLearningPathSkillDto : CreateDtoBase
{
    [Required]
    public Guid LearningPathId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public ProficiencyLevel TargetProficiency { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE LEARNING PATH DTOs
// ============================================================================

#region Employee Learning Path

public class EmployeeLearningPathDto : BaseDto
{
    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public Guid LearningPathId { get; set; }
    public string LearningPathName { get; set; } = string.Empty;

    public DateTime EnrolledDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }

    public int ProgressPercentage { get; set; }
    public bool IsCompleted { get; set; }

    public Guid? AssignedById { get; set; }
    public string? AssignedByName { get; set; }
    public string? Notes { get; set; }

    public List<EmployeeLearningPathStepDto> Steps { get; set; } = new();
}

public class EmployeeLearningPathSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    // Named the path but never identified it, so a "My Learning" row could not open the path it
    // refers to. From the FK, so a missed include cannot blank it.
    public Guid LearningPathId { get; set; }
    public string LearningPathName { get; set; } = string.Empty;
    public DateTime EnrolledDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public int ProgressPercentage { get; set; }
    public bool IsCompleted { get; set; }
}

public class EnrollEmployeeInLearningPathDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid LearningPathId { get; set; }

    public DateTime EnrolledDate { get; set; } = DateTime.UtcNow;
    public DateTime? TargetCompletionDate { get; set; }

    public Guid? AssignedById { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>Richer summary used by the global enrollment management page.</summary>
public class EnrollmentListItemDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public string? PositionTitle { get; set; }
    public Guid LearningPathId { get; set; }
    public string LearningPathName { get; set; } = string.Empty;
    public DateTime EnrolledDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public int ProgressPercentage { get; set; }
    public bool IsCompleted { get; set; }
    public string? AssignedByName { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Updates the mutable fields of an enrollment (target date and notes).</summary>
public class UpdateEnrollmentDto : UpdateDtoBase
{
    public DateTime? TargetCompletionDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE LEARNING PATH STEP DTOs
// ============================================================================

#region Employee Learning Path Step

public class EmployeeLearningPathStepDto : BaseDto
{
    public Guid EmployeeLearningPathId { get; set; }
    public Guid LearningPathProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? NominationId { get; set; }
    public string? NominationNumber { get; set; }
    /// <summary>If set, this step is locked until the step referencing this LearningPathProgramId is completed.</summary>
    public Guid? PrerequisitePathProgramId { get; set; }
    public string? PrerequisiteProgramName { get; set; }
}

public class UpdateLearningPathStepDto : UpdateDtoBase
{
    public bool IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? NominationId { get; set; }

    /// <summary>
    /// Why HR is completing a step that has no attendance or completion record behind it.
    ///
    /// Mandatory for that case and ignored otherwise — it cannot be a <c>[Required]</c> attribute
    /// because it is only required conditionally, so the service enforces it.
    /// </summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

#endregion

// ============================================================================
// MENTORING PROGRAM DTOs
// ============================================================================

#region Mentoring Program

public class MentoringProgramDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Objectives { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int? SessionsPerMonth { get; set; }
    public int? MinutesPerSession { get; set; }
    public bool IsActive { get; set; }

    public Guid? CoordinatedById { get; set; }
    public string? CoordinatedByName { get; set; }

    public int TotalPairsCount { get; set; }
    public int ActivePairsCount { get; set; }
}

public class MentoringProgramSummaryDto
{
    public Guid Id { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? CoordinatedByName { get; set; }
    public int TotalPairsCount { get; set; }
    public int ActivePairsCount { get; set; }
}

public class CreateMentoringProgramDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Objectives { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Range(1, 30)]
    public int? SessionsPerMonth { get; set; }

    [Range(15, 480)]
    public int? MinutesPerSession { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? CoordinatedById { get; set; }
}

public class UpdateMentoringProgramDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Objectives { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Range(1, 30)]
    public int? SessionsPerMonth { get; set; }

    [Range(15, 480)]
    public int? MinutesPerSession { get; set; }

    public bool IsActive { get; set; }
    public Guid? CoordinatedById { get; set; }
}

#endregion

// ============================================================================
// MENTORING PAIR DTOs
// ============================================================================

#region Mentoring Pair

public class MentoringPairDto : BaseDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;

    public Guid MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string MentorNumber { get; set; } = string.Empty;
    public string? MentorPosition { get; set; }

    public Guid MenteeId { get; set; }
    public string MenteeName { get; set; } = string.Empty;
    public string MenteeNumber { get; set; } = string.Empty;
    public string? MenteePosition { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public MentoringStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public string? Goals { get; set; }
    public string? FocusAreas { get; set; }
    public string? ClosureNotes { get; set; }

    public int? MentorRating { get; set; }
    public int? MenteeRating { get; set; }

    public int TotalSessionsCount { get; set; }
    public int TotalMinutes { get; set; }

    /// <summary>
    /// Which side of this relationship the caller is on. The screen needs this to tell "there is no
    /// note" apart from "the note is not yours to read" — both arrive as null, and a UI that guesses
    /// from null-ness will eventually offer someone an empty box to overwrite another person's words.
    /// A coordinator or HR viewer is neither.
    /// </summary>
    public bool ViewerIsMentor { get; set; }
    public bool ViewerIsMentee { get; set; }
}

public class MentoringPairSummaryDto
{
    public Guid Id { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string MenteeName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public MentoringStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int TotalSessionsCount { get; set; }
}

public class CreateMentoringPairDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public Guid MentorId { get; set; }

    [Required]
    public Guid MenteeId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(2000)]
    public string? Goals { get; set; }

    [MaxLength(1000)]
    public string? FocusAreas { get; set; }
}

public class UpdateMentoringPairDto : UpdateDtoBase
{
    public DateTime? EndDate { get; set; }

    [Required]
    public MentoringStatus Status { get; set; }

    [MaxLength(2000)]
    public string? Goals { get; set; }

    [MaxLength(1000)]
    public string? FocusAreas { get; set; }

    [MaxLength(2000)]
    public string? ClosureNotes { get; set; }

    [Range(1, 5)]
    public int? MentorRating { get; set; }

    [Range(1, 5)]
    public int? MenteeRating { get; set; }
}

#endregion

// ============================================================================
// MENTORING SESSION DTOs
// ============================================================================

#region Mentoring Session

public class MentoringSessionDto : BaseDto
{
    public Guid PairId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string MenteeName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;

    public DateTime SessionDate { get; set; }
    public int DurationMinutes { get; set; }
    public MentoringSessionFormat? Format { get; set; }
    public string? TopicsDiscussed { get; set; }
    public string? ActionItems { get; set; }
    public string? MentorNotes { get; set; }
    public string? MenteeNotes { get; set; }
    public bool AttendedByMentor { get; set; }
    public bool AttendedByMentee { get; set; }
}

public class CreateMentoringSessionDto : CreateDtoBase
{
    [Required]
    public Guid PairId { get; set; }

    [Required]
    public DateTime SessionDate { get; set; }

    [Required]
    [Range(1, 1440)]
    public int DurationMinutes { get; set; }

    public MentoringSessionFormat? Format { get; set; }

    [MaxLength(4000)]
    public string? TopicsDiscussed { get; set; }

    [MaxLength(2000)]
    public string? ActionItems { get; set; }

    [MaxLength(2000)]
    public string? MentorNotes { get; set; }

    [MaxLength(2000)]
    public string? MenteeNotes { get; set; }

    public bool AttendedByMentor { get; set; } = true;
    public bool AttendedByMentee { get; set; } = true;
}

public class UpdateMentoringSessionDto : UpdateDtoBase
{
    [Required]
    public DateTime SessionDate { get; set; }

    [Required]
    [Range(1, 1440)]
    public int DurationMinutes { get; set; }

    public MentoringSessionFormat? Format { get; set; }

    [MaxLength(4000)]
    public string? TopicsDiscussed { get; set; }

    [MaxLength(2000)]
    public string? ActionItems { get; set; }

    [MaxLength(2000)]
    public string? MentorNotes { get; set; }

    [MaxLength(2000)]
    public string? MenteeNotes { get; set; }

    public bool AttendedByMentor { get; set; }
    public bool AttendedByMentee { get; set; }
}

#endregion

// ============================================================================
// TRAINING DASHBOARD DTOs
// ============================================================================

#region Training Dashboard

public class TrainingDashboardDto
{
    public int TotalProgramsActive { get; set; }
    public int TotalSchedulesThisYear { get; set; }
    public int UpcomingSchedulesCount { get; set; }
    public int TotalNominationsThisYear { get; set; }
    public int PendingNominationsCount { get; set; }
    public int CompletedTrainingsThisYear { get; set; }
    public decimal OverallPassRate { get; set; }
    public int EmployeesCertifiedThisYear { get; set; }
    public int ExpiringCertificatesIn30Days { get; set; }
    public decimal OverallComplianceRate { get; set; }
    /// <summary>
    /// The denominator behind <see cref="OverallComplianceRate"/>. Without it a screen cannot tell
    /// "0% because nobody complies" from "0% because nobody has been assigned anything" — and the
    /// second reads as a five-alarm fire when it means the programme has not started.
    /// </summary>
    public int ComplianceRecordsCount { get; set; }
    public int NonCompliantEmployeesCount { get; set; }
    public int ActiveMentoringPairsCount { get; set; }
    public int ActiveLearningPathEnrollmentsCount { get; set; }
    public decimal BudgetUtilizationRate { get; set; }
    public decimal BudgetSpent { get; set; }
    public decimal BudgetAllocated { get; set; }

    public List<TrainingCategoryBreakdownDto> CategoryBreakdown { get; set; } = new();
    public List<TrainingNominationSummaryDto> RecentNominations { get; set; } = new();
    public List<TrainingScheduleSummaryDto> UpcomingSchedules { get; set; } = new();
}

public class TrainingCategoryBreakdownDto
{
    public Guid? CategoryOptionId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
    public int ProgramsCount { get; set; }
    public int CompletionsCount { get; set; }
    public decimal TotalSpent { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Training Analytics (combined operations + Kirkpatrick effectiveness dashboard)
// ─────────────────────────────────────────────────────────────────────────────

public class TrainingAnalyticsDto
{
    public int Year { get; set; }
    public string Currency { get; set; } = "GHS";

    // ── Operations ──────────────────────────────────────────────────────────
    public int ActiveProgramsCount { get; set; }
    public int CompletionsYtd { get; set; }
    public int PassedYtd { get; set; }
    public decimal PassRate { get; set; }
    public decimal ComplianceRate { get; set; }
    /// <summary>The denominator behind <see cref="ComplianceRate"/> — see the dashboard DTO for why.</summary>
    public int ComplianceRecordsCount { get; set; }
    public int CertificatesIssuedYtd { get; set; }
    public decimal BudgetAllocated { get; set; }
    public decimal BudgetSpent { get; set; }
    public decimal BudgetUtilizationRate { get; set; }

    public List<TrainingCategoryBreakdownDto> CategoryBreakdown { get; set; } = new();
    public List<TrainerUtilizationDto> TopTrainers { get; set; } = new();
    public List<MonthlyCompletionPointDto> MonthlyCompletions { get; set; } = new();

    // ── Effectiveness (Kirkpatrick) ─────────────────────────────────────────
    public KirkpatrickFunnelDto Funnel { get; set; } = new();
    public int FeedbackResponses { get; set; }
    public decimal? AvgSatisfaction { get; set; }        // L1 overall satisfaction, 1–5
    public decimal WouldRecommendRate { get; set; }       // % of feedback
    public decimal? AvgLikelihoodToApply { get; set; }    // 1–5
    public decimal? AvgPreScore { get; set; }
    public decimal? AvgPostScore { get; set; }
    public decimal? AvgScoreGain { get; set; }            // post − pre
    public int FollowUpsCompleted { get; set; }
    public decimal? CostPerCompletion { get; set; }
}

public class TrainerUtilizationDto
{
    public Guid TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SessionsDelivered { get; set; }
    public int HoursDelivered { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingsCount { get; set; }
}

public class MonthlyCompletionPointDto
{
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Passed { get; set; }
}

/// <summary>Kirkpatrick training-effectiveness funnel counts (widest → narrowest).</summary>
public class KirkpatrickFunnelDto
{
    public int Nominated { get; set; }     // approved / confirmed nominations
    public int Attended { get; set; }      // distinct employees marked present
    public int Completed { get; set; }     // recorded completions
    public int FeedbackL1 { get; set; }    // Level 1 feedback responses
    public int FollowUpL23 { get; set; }   // Level 2/3 follow-up assessments
}

public class EmployeeTrainingSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    /// <summary>Every recorded completion, passed or not. <see cref="TrainingsPassed"/> is the subset.</summary>
    public int TotalTrainingsCompleted { get; set; }
    public int TrainingsPassed { get; set; }
    public int TotalTrainingHours { get; set; }
    public int ActiveCertificatesCount { get; set; }
    public int ExpiringCertificatesCount { get; set; }
    public int ComplianceRequirementsCount { get; set; }
    public int CompliantRequirementsCount { get; set; }
    public decimal ComplianceRate => ComplianceRequirementsCount > 0
        ? Math.Round((decimal)CompliantRequirementsCount / ComplianceRequirementsCount * 100, 1)
        : 100;
    public int LearningPathsEnrolledCount { get; set; }
    public int LearningPathsCompletedCount { get; set; }
    public bool HasActiveMentoringPair { get; set; }

    public List<TrainingNominationSummaryDto> RecentTrainings { get; set; } = new();
    /// <summary>
    /// Certificates awarded from training we ran — the same population the counts above describe.
    /// This was declared as <c>EmployeeCertificateSummaryDto</c> (externally-held qualifications,
    /// a different entity) while the counts came from <c>TrainingCertificate</c>, so the list and
    /// the numbers beside it described different things. Nothing consumed it either way.
    /// </summary>
    public List<TrainingCertificateSummaryDto> Certificates { get; set; } = new();
    public List<EmployeeComplianceRecordSummaryDto> ComplianceRecords { get; set; } = new();
}

#endregion

// ============================================================================
// STEP DETAIL PAGE DTOs (employee self-service learning step page)
// ============================================================================

#region Step Detail Page

/// <summary>Full data package for an employee's step detail page.</summary>
public class StepDetailPageDto
{
    // ── Step context ───────────────────────────────────────────────────────
    public Guid      StepId                  { get; set; }
    public Guid      EnrollmentId            { get; set; }
    public string    LearningPathName        { get; set; } = string.Empty;
    public int       StepSequence            { get; set; }
    public int       TotalSteps              { get; set; }
    public bool      IsCompleted             { get; set; }
    public DateTime? CompletedDate           { get; set; }
    public bool      IsLocked                { get; set; }
    public string?   PrerequisiteProgramName { get; set; }
    public bool      IsMandatory             { get; set; }
    /// <summary>True when the enrolled employee has attendance records or a completion record — enabling "Mark as Complete".</summary>
    public bool      CanMarkComplete         { get; set; }
    /// <summary>Who is enrolled, which is not always who is looking — HR can open this from the org-wide list.</summary>
    public Guid      LearnerId               { get; set; }
    public string    LearnerName             { get; set; } = string.Empty;
    /// <summary>False when someone other than the learner is viewing, so the page can stop saying "your".</summary>
    public bool      IsOwnStep               { get; set; }

    // ── Program detail ─────────────────────────────────────────────────────
    public Guid         ProgramId           { get; set; }
    public string       ProgramCode         { get; set; } = string.Empty;
    public string       ProgramName         { get; set; } = string.Empty;
    public string       Description         { get; set; } = string.Empty;
    public TrainingLevel Level              { get; set; }
    public string       LevelName           => Level.ToString();
    public int          DurationDays        { get; set; }
    public int          DurationHours       { get; set; }
    public string?      LearningObjectives  { get; set; }
    public string?      Prerequisites       { get; set; }
    public bool         ProvidesCertificate { get; set; }
    public string?      CertificateName     { get; set; }

    // ── Materials ──────────────────────────────────────────────────────────
    public List<StepMaterialDto>    Materials          { get; set; } = new();

    // ── Available schedules for this program ──────────────────────────────
    public List<StepScheduleSummaryDto> AvailableSchedules { get; set; } = new();

    // ── Employee's own nomination for this step ────────────────────────────
    public StepNominationDto? MyNomination { get; set; }

    // ── Attendance records ─────────────────────────────────────────────────
    public List<StepAttendanceDto> MyAttendance { get; set; } = new();

    // ── Completion record ──────────────────────────────────────────────────
    public StepCompletionDto? MyCompletion { get; set; }

    // ── Feedback already submitted ─────────────────────────────────────────
    public StepFeedbackDto? MyFeedback { get; set; }
}

public class StepMaterialDto
{
    public Guid         Id           { get; set; }
    public string       MaterialName { get; set; } = string.Empty;
    public MaterialType Type         { get; set; }
    public string       TypeName     => Type.ToString();
    public string?      FilePath     { get; set; }
    public string?      ExternalUrl  { get; set; }
    public bool         IsPublic     { get; set; }
}

public class StepScheduleSummaryDto
{
    public Guid           Id                         { get; set; }
    public string         ScheduleNumber             { get; set; } = string.Empty;
    public DateTime       StartDate                  { get; set; }
    public DateTime       EndDate                    { get; set; }
    public string?        Venue                      { get; set; }
    public string?        VenueAddress               { get; set; }
    public string?        OnlineLink                 { get; set; }
    public string?        TrainerName                { get; set; }
    public string?        VendorName                 { get; set; }
    public int            MaxParticipants            { get; set; }
    public int            ConfirmedParticipantsCount { get; set; }
    public int            SlotsAvailable             => MaxParticipants - ConfirmedParticipantsCount;
    public DateTime       RegistrationCloseDate      { get; set; }
    public ScheduleStatus Status                     { get; set; }
    public string         StatusName                 => Status.ToString();
    public bool           IsRegistrationOpen         { get; set; }
}

public class StepNominationDto
{
    public Guid             Id               { get; set; }
    public string           NominationNumber { get; set; } = string.Empty;
    public Guid             ScheduleId       { get; set; }
    public string           ScheduleNumber   { get; set; } = string.Empty;
    public DateTime         TrainingStartDate { get; set; }
    public DateTime         TrainingEndDate   { get; set; }
    public NominationStatus Status           { get; set; }
    public string           StatusName       => Status.ToString();
    public DateTime         NominationDate   { get; set; }
    public string?          Justification    { get; set; }
}

public class StepAttendanceDto
{
    public Guid     Id             { get; set; }
    public DateTime AttendanceDate { get; set; }
    public bool     IsPresent      { get; set; }
    public string?  AbsenceReason  { get; set; }
    public TimeSpan? CheckInTime   { get; set; }
    public TimeSpan? CheckOutTime  { get; set; }
}

public class StepCompletionDto
{
    public Guid     Id             { get; set; }
    public DateTime CompletionDate { get; set; }
    public decimal? FinalScore     { get; set; }
    public bool     IsPassed       { get; set; }
    public TrainingCompletionStatus Status { get; set; }
    public string   StatusName     => Status.ToString();
    public bool     IsVerifiedByManager { get; set; }
}

public class StepFeedbackDto
{
    public Guid     Id                          { get; set; }
    public int?     ContentRelevanceRating      { get; set; }
    public int?     TrainerKnowledgeRating       { get; set; }
    public int?     DeliveryMethodRating         { get; set; }
    public int?     MaterialQualityRating        { get; set; }
    public int?     OverallSatisfactionRating    { get; set; }
    public string?  StrengthsOfTraining          { get; set; }
    public string?  AreasForImprovement          { get; set; }
    public string?  SuggestionsForFuture         { get; set; }
    public bool     WouldRecommend               { get; set; }
    public DateTime FeedbackDate                 { get; set; }
}

#endregion

// ============================================================================
// TRAINING SERVICE BOND DTOs
// ============================================================================

#region Training Service Bond

public class TrainingServiceBondDto : BaseDto
{
    public Guid NominationId { get; set; }
    public string? NominationNumber { get; set; }

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public Guid ProgramId { get; set; }
    public string? ProgramName { get; set; }

    public int BondDurationMonths { get; set; }
    public decimal BondAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public string? TermsText { get; set; }

    public TrainingBondStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public bool AcceptedByEmployee { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public Guid? AcceptanceRecordedById { get; set; }
    public string? AcceptanceRecordedByName { get; set; }
    public string? AcceptanceNotes { get; set; }

    public DateTime? BondStartDate { get; set; }
    public DateTime? BondEndDate { get; set; }

    public DateTime? ExitDate { get; set; }
    public decimal? RepaymentAmount { get; set; }
    public DateTime? SettledDate { get; set; }

    public DateTime? WaivedDate { get; set; }
    public Guid? WaivedById { get; set; }
    public string? WaiverReason { get; set; }

    public string? Notes { get; set; }

    /// <summary>Whole months remaining on the obligation as of today (0 when served/ended).</summary>
    public int MonthsRemaining { get; set; }
}

/// <summary>Manual HR attach of a bond to an existing nomination.</summary>
public class CreateTrainingServiceBondDto : CreateDtoBase
{
    [Required]
    public Guid NominationId { get; set; }

    [Range(1, 120)]
    public int BondDurationMonths { get; set; }

    [Range(0, double.MaxValue)]
    public decimal BondAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [MaxLength(4000)]
    public string? TermsText { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>Edit bond terms while still pending acceptance.</summary>
public class UpdateTrainingServiceBondDto : UpdateDtoBase
{
    [Range(1, 120)]
    public int BondDurationMonths { get; set; }

    [Range(0, double.MaxValue)]
    public decimal BondAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [MaxLength(4000)]
    public string? TermsText { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>Accept the bond terms — by the employee (self) or recorded by HR on their behalf.</summary>
public class AcceptTrainingServiceBondDto
{
    [Required]
    public Guid BondId { get; set; }

    /// <summary>Optional explicit obligation start; defaults server-side to completion/acceptance date.</summary>
    public DateTime? BondStartDate { get; set; }

    [MaxLength(1000)]
    public string? AcceptanceNotes { get; set; }
}

/// <summary>Record an early exit — computes the pro-rated repayment owed.</summary>
public class RecordBondExitDto
{
    [Required]
    public Guid BondId { get; set; }

    [Required]
    public DateTime ExitDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class WaiveTrainingServiceBondDto
{
    [Required]
    public Guid BondId { get; set; }

    [MaxLength(1000)]
    public string? WaiverReason { get; set; }
}

public class SettleTrainingServiceBondDto
{
    [Required]
    public Guid BondId { get; set; }

    public DateTime? SettledDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// TRAINING STATUS HISTORY DTOs (audit trail)
// ============================================================================

#region Training Status History

public class TrainingStatusHistoryDto : BaseDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? EntityReference { get; set; }
    public int? FromStatus { get; set; }
    public string? FromStatusName { get; set; }
    public int ToStatus { get; set; }
    public string ToStatusName { get; set; } = string.Empty;
    public Guid? ChangedByEmployeeId { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
}

#endregion
