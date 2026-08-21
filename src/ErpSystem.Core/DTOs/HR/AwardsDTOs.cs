using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

#region Award Type DTOs

/// <summary>
/// DTO for award type read operations
/// </summary>
public class AwardTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AwardCategory Category { get; set; }
    public bool IsTeamAward { get; set; }
    public string CategoryName => Category.ToString();
    public AwardFrequency Frequency { get; set; }
    public string FrequencyName => Frequency.ToString();
    
    // Eligibility
    public int? MinServiceYears { get; set; }
    public int? MaxServiceYears { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    
    // Limits
    public int? MaxAwardsPerPeriod { get; set; }
    public int? MaxAwardsPerEmployee { get; set; }
    
    // Reward configuration
    public bool HasMonetaryReward { get; set; }
    public decimal? MinMonetaryAmount { get; set; }
    public decimal? MaxMonetaryAmount { get; set; }
    public bool HasCertificate { get; set; }
    public bool HasTrophy { get; set; }
    public int? LeaveDaysBonus { get; set; }
    
    // Award Levels/Tiers
    public bool HasLevels { get; set; }
    
    public bool RequiresFormalReview { get; set; }
    public int? MinRequiredReviewers { get; set; }

    public AwardNominationSource NominationSource { get; set; }

    // Computed, not mapped — the convention already used by TargetTypeName below. A mapped name
    // field is one more thing a mapper can forget to set; a computed one cannot go stale.
    public string NominationSourceName => NominationSource.ToString();

    public AwardWinnerDecision WinnerDecision { get; set; }
    public string WinnerDecisionName => WinnerDecision.ToString();

    /// <summary>Whether an employee may nominate themselves. Off by default; see the entity.</summary>
    public bool AllowSelfNomination { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Summary DTO for award type list views
/// </summary>
public class AwardTypeSummaryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AwardCategory Category { get; set; }
    public bool IsTeamAward { get; set; }
    public string CategoryName => Category.ToString();
    public AwardFrequency Frequency { get; set; }
    public string FrequencyName => Frequency.ToString();
    public bool HasMonetaryReward { get; set; }
    public decimal? MinMonetaryAmount { get; set; }
    public decimal? MaxMonetaryAmount { get; set; }
    public bool HasLevels { get; set; }
    public bool RequiresFormalReview { get; set; }

    // On the summary as well as the detail: since slice 2 the selection model is the main thing
    // that distinguishes one award type from another, so a register that omitted it would list
    // several identical-looking rows and give the reader no way to tell a voted award from a
    // committee-scored one without opening each.
    public AwardNominationSource NominationSource { get; set; }
    public string NominationSourceName => NominationSource.ToString();
    public AwardWinnerDecision WinnerDecision { get; set; }
    public string WinnerDecisionName => WinnerDecision.ToString();

    public bool IsActive { get; set; }
    public int AwardCount { get; set; }
}

/// <summary>
/// DTO for creating an award type
/// </summary>
public class CreateAwardTypeDto : CreateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public AwardCategory Category { get; set; }

    public bool IsTeamAward { get; set; }

    [Required]
    public AwardFrequency Frequency { get; set; }

    [Range(0, int.MaxValue)]
    public int? MinServiceYears { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxServiceYears { get; set; }

    [Range(0, 150)]
    public int? MinAge { get; set; }

    [Range(0, 150)]
    public int? MaxAge { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxAwardsPerPeriod { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxAwardsPerEmployee { get; set; }

    public bool HasMonetaryReward { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinMonetaryAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxMonetaryAmount { get; set; }

    public bool HasCertificate { get; set; }
    public bool HasTrophy { get; set; }

    [Range(0, int.MaxValue)]
    public int? LeaveDaysBonus { get; set; }

    public bool HasLevels { get; set; }
    public bool RequiresFormalReview { get; set; }

    [Range(1, 50)]
    public int? MinRequiredReviewers { get; set; }

    /// <summary>Where the candidates come from. Defaults to open nomination.</summary>
    public AwardNominationSource NominationSource { get; set; } = AwardNominationSource.OpenNomination;

    /// <summary>
    /// How the winner is chosen. A ManagementDirect award cannot be decided by a vote or by
    /// scoring — there is no candidate list — and the service refuses that combination.
    /// </summary>
    public AwardWinnerDecision WinnerDecision { get; set; } = AwardWinnerDecision.CommitteeScore;

    /// <summary>
    /// Whether an employee may put their own name forward. Defaults to <c>false</c>, matching the
    /// enterprise norm: peer or manager nomination is the default and self-nomination is granted
    /// per award, typically to innovation and improvement awards rather than to behavioural ones.
    /// </summary>
    public bool AllowSelfNomination { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an award type
/// </summary>
public class UpdateAwardTypeDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public AwardCategory Category { get; set; }

    public bool IsTeamAward { get; set; }

    [Required]
    public AwardFrequency Frequency { get; set; }

    [Range(0, int.MaxValue)]
    public int? MinServiceYears { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxServiceYears { get; set; }

    [Range(0, 150)]
    public int? MinAge { get; set; }

    [Range(0, 150)]
    public int? MaxAge { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxAwardsPerPeriod { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxAwardsPerEmployee { get; set; }

    public bool HasMonetaryReward { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinMonetaryAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxMonetaryAmount { get; set; }

    public bool HasCertificate { get; set; }
    public bool HasTrophy { get; set; }

    [Range(0, int.MaxValue)]
    public int? LeaveDaysBonus { get; set; }

    public bool HasLevels { get; set; }
    public bool RequiresFormalReview { get; set; }

    [Range(1, 50)]
    public int? MinRequiredReviewers { get; set; }

    /// <summary>Where the candidates come from. Defaults to open nomination.</summary>
    public AwardNominationSource NominationSource { get; set; } = AwardNominationSource.OpenNomination;

    /// <summary>
    /// How the winner is chosen. A ManagementDirect award cannot be decided by a vote or by
    /// scoring — there is no candidate list — and the service refuses that combination.
    /// </summary>
    public AwardWinnerDecision WinnerDecision { get; set; } = AwardWinnerDecision.CommitteeScore;

    /// <summary>
    /// Whether an employee may put their own name forward. Defaults to <c>false</c>, matching the
    /// enterprise norm: peer or manager nomination is the default and self-nomination is granted
    /// per award, typically to innovation and improvement awards rather than to behavioural ones.
    /// </summary>
    public bool AllowSelfNomination { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Award Level DTOs

/// <summary>
/// DTO for award level read operations
/// </summary>
public class AwardLevelDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AwardTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Rank { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public int? LeaveDaysBonus { get; set; }
    public string? Benefits { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating an award level
/// </summary>
public class CreateAwardLevelDto : CreateDtoBase
{
    [Required]
    public Guid AwardTypeId { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Rank { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? LeaveDaysBonus { get; set; }

    [MaxLength(1000)]
    public string? Benefits { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an award level
/// </summary>
public class UpdateAwardLevelDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Rank { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? LeaveDaysBonus { get; set; }

    [MaxLength(1000)]
    public string? Benefits { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Award Type Target DTOs

/// <summary>
/// DTO for award type target read operations
/// </summary>
public class AwardTypeTargetDto : BaseDto
{
    /// <summary>Whether this scopes who may win the award or who may vote in it.</summary>
    public AwardTargetPurpose Purpose { get; set; }
    public string PurposeName => Purpose.ToString();

    public Guid TenantId { get; set; }
    public Guid AwardTypeId { get; set; }
    public AwardTargetType TargetType { get; set; }
    public string TargetTypeName => TargetType.ToString();
    public Guid? TargetId { get; set; }
    public string? TargetName { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public bool IsExclusion { get; set; }
    public string? Reason { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

/// <summary>
/// DTO for creating an award type target
/// </summary>
public class CreateAwardTypeTargetDto : CreateDtoBase
{
    /// <summary>
    /// Whether this target scopes who may win or who may vote. Defaults to eligibility, so a
    /// caller that predates voting keeps the behaviour it had.
    /// </summary>
    public AwardTargetPurpose Purpose { get; set; } = AwardTargetPurpose.Eligibility;

    [Required]
    public Guid AwardTypeId { get; set; }

    [Required]
    public AwardTargetType TargetType { get; set; }

    public Guid? TargetId { get; set; }

    [Range(18, 100)]
    public int? MinAge { get; set; }

    [Range(18, 100)]
    public int? MaxAge { get; set; }

    public bool IsExclusion { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

/// <summary>
/// DTO for updating an award type target
/// </summary>
public class UpdateAwardTypeTargetDto : UpdateDtoBase
{
    /// <summary>
    /// Whether this target scopes who may win or who may vote. Defaults to eligibility, so a
    /// caller that predates voting keeps the behaviour it had.
    /// </summary>
    public AwardTargetPurpose Purpose { get; set; } = AwardTargetPurpose.Eligibility;

    [Required]
    public AwardTargetType TargetType { get; set; }

    public Guid? TargetId { get; set; }

    [Range(18, 100)]
    public int? MinAge { get; set; }

    [Range(18, 100)]
    public int? MaxAge { get; set; }

    public bool IsExclusion { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

#endregion

#region Award Budget DTOs

/// <summary>
/// DTO for award budget read operations
/// </summary>
public class AwardBudgetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string BudgetCode { get; set; } = string.Empty;
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal ReservedAmount { get; set; }
    public decimal AvailableAmount => BudgetAmount - SpentAmount - ReservedAmount;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating an award budget
/// </summary>
public class CreateAwardBudgetDto : CreateDtoBase
{
    [MaxLength(50)]
    public string BudgetCode { get; set; } = string.Empty;

    [Required]
    public Guid AwardTypeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal BudgetAmount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating an award budget
/// </summary>
public class UpdateAwardBudgetDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string BudgetCode { get; set; } = string.Empty;

    [Required]
    [Range(0, double.MaxValue)]
    public decimal BudgetAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SpentAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ReservedAmount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Employee Award DTOs

/// <summary>
/// DTO for employee award read operations
/// </summary>
public class EmployeeAwardDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AwardNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? DepartmentName { get; set; }
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;
    
    // Award Details

    public DateTime AwardDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Citation { get; set; }
    
    // Presentation
    public DateTime? PresentationDate { get; set; }
    public string? PresentationVenue { get; set; }
    public Guid? PresentedById { get; set; }
    public string? PresentedByName { get; set; }
    
    // Reward
    public decimal? MonetaryAmount { get; set; }
    public string? CertificateNumber { get; set; }
    public bool CertificateIssued { get; set; }
    public bool TrophyIssued { get; set; }
    
    // Payment
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    
    // Publicity
    public bool PublishToIntranet { get; set; }
    public bool PublishToWebsite { get; set; }
    public string? PublicationNotes { get; set; }
}

/// <summary>
/// Summary DTO for employee award list views
/// </summary>
public class EmployeeAwardSummaryDto
{
    public Guid Id { get; set; }
    public string AwardNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;

    public DateTime AwardDate { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public DateTime? PresentationDate { get; set; }
}

/// <summary>
/// Detailed DTO for employee award with attachments
/// </summary>
public class EmployeeAwardDetailDto : EmployeeAwardDto
{
    public List<AwardAttachmentDto> Attachments { get; set; } = new();
}

/// <summary>
/// DTO for creating an employee award
/// </summary>
public class CreateEmployeeAwardDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid AwardTypeId { get; set; }

    [Required]
    public DateTime AwardDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Citation { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }
}

/// <summary>
/// DTO for updating an employee award
/// </summary>
public class UpdateEmployeeAwardDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid AwardTypeId { get; set; }

    [Required]
    public DateTime AwardDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Citation { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }

    [MaxLength(50)]
    public string? CertificateNumber { get; set; }

    public bool CertificateIssued { get; set; }
    public bool TrophyIssued { get; set; }
    public bool PublishToIntranet { get; set; }
    public bool PublishToWebsite { get; set; }

    [MaxLength(2000)]
    public string? PublicationNotes { get; set; }
}

/// <summary>
/// DTO for approving an employee award
/// </summary>
public class ApproveEmployeeAwardDto
{
    [Required]
    public Guid AwardId { get; set; }

    [MaxLength(2000)]
    public string? ApprovalComments { get; set; }
}

/// <summary>
/// DTO for scheduling award presentation
/// </summary>
public class ScheduleAwardPresentationDto
{
    [Required]
    public Guid AwardId { get; set; }

    [Required]
    public DateTime PresentationDate { get; set; }

    [MaxLength(70)]
    public string? PresentationVenue { get; set; }

    public Guid? PresentedById { get; set; }
}

/// <summary>
/// DTO for processing award payment
/// </summary>
public class ProcessAwardPaymentDto
{
    [Required]
    public Guid AwardId { get; set; }

    [Required]
    [MaxLength(100)]
    public string PaymentReference { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating an employee award from an approved nomination
/// </summary>
public class CreateEmployeeAwardFromNominationDto : CreateDtoBase
{
    [Required]
    public DateTime AwardDate { get; set; }

    [MaxLength(2000)]
    public string? AdditionalCitation { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }
}

/// <summary>
/// DTO for recording award presentation details
/// </summary>
public class RecordAwardPresentationDto
{
    [Required]
    public DateTime PresentationDate { get; set; }

    [MaxLength(200)]
    public string? PresentationVenue { get; set; }

    [MaxLength(2000)]
    public string? PresentationNotes { get; set; }

    public bool CertificateIssued { get; set; }
    public bool TrophyIssued { get; set; }

    [MaxLength(50)]
    public string? CertificateNumber { get; set; }
}

#endregion

#region Team Award Recipient DTOs

/// <summary>
/// DTO for team award recipient read operations
/// </summary>
public class TeamAwardRecipientDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AwardId { get; set; }
    public string AwardNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Role { get; set; }
    public decimal? SharePercentage { get; set; }
}

#endregion

#region Award Attachment DTOs

/// <summary>
/// DTO for award attachment read operations
/// </summary>
public class AwardAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AwardId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public AwardAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName => AttachmentType.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// DTO for creating an award attachment
/// </summary>
public class CreateAwardAttachmentDto : CreateDtoBase
{
    [Required]
    public Guid AwardId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public AwardAttachmentType AttachmentType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// DTO for updating an award attachment
/// </summary>
public class UpdateAwardAttachmentDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public AwardAttachmentType AttachmentType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

#endregion

#region Award Nomination DTOs

/// <summary>
/// DTO for award nomination read operations
/// </summary>
public class AwardNominationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;

    /// <summary>The run this belongs to. Null for a nomination raised before cycles existed.</summary>
    public Guid? AwardCycleId { get; set; }
    public string? AwardCycleName { get; set; }

    public Guid? AwardLevelId { get; set; }
    public string? AwardLevelName { get; set; }
    public Guid? NomineeId { get; set; }
    public string NomineeName { get; set; } = string.Empty;
    public string? NomineeEmployeeNumber { get; set; }
    public string? NomineeDepartment { get; set; }
    public Guid NominatedById { get; set; }
    public string NominatedByName { get; set; } = string.Empty;
    public DateTime NominationDate { get; set; }
    public int Year { get; set; }
    public string? TeamName { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }
    public string Justification { get; set; } = string.Empty;
    public decimal? ProposedMonetaryAmount { get; set; }
    public int? ProposedLeaveDays { get; set; }
    public AwardNominationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    
    // Outcome
    public DateTime? OutcomeDate { get; set; }
    public string? OutcomeReason { get; set; }
    public Guid? AwardId { get; set; }
    public string? AwardNumber { get; set; }
}

/// <summary>
/// Summary DTO for award nomination list views
/// </summary>
public class AwardNominationSummaryDto
{
    public Guid Id { get; set; }
    public string NominationNumber { get; set; } = string.Empty;
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;
    public string NomineeName { get; set; } = string.Empty;
    public string NominatedByName { get; set; } = string.Empty;
    public DateTime NominationDate { get; set; }
    public int Year { get; set; }
    public string? TeamName { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }
    public AwardNominationStatus Status { get; set; }
    public string StatusName => Status.ToString();

}

/// <summary>
/// DTO for creating an award nomination
/// </summary>
public class CreateAwardNominationDto : CreateDtoBase
{
    [Required]
    public Guid AwardTypeId { get; set; }

    /// <summary>
    /// The run this nomination belongs to. Optional on the DTO but required in practice for any
    /// award that has a nomination stage — the cycle is what says whether nominations are open at
    /// all, so a nomination without one cannot be timed, listed for voting, or closed.
    /// </summary>
    public Guid? AwardCycleId { get; set; }

    public Guid? AwardLevelId { get; set; }

    public Guid? NomineeId { get; set; }

    [MaxLength(150)]
    public string? TeamName { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Justification { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? ProposedMonetaryAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? ProposedLeaveDays { get; set; }
}

/// <summary>
/// DTO for updating an award nomination
/// </summary>
public class UpdateAwardNominationDto : UpdateDtoBase
{
    [Required]
    public Guid AwardTypeId { get; set; }

    public Guid? AwardLevelId { get; set; }

    public Guid? NomineeId { get; set; }

    [MaxLength(150)]
    public string? TeamName { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Justification { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? ProposedMonetaryAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? ProposedLeaveDays { get; set; }
}

/// <summary>
/// DTO for reviewing an award nomination
/// </summary>
public class ReviewAwardNominationDto
{
    [Required]
    public Guid NominationId { get; set; }

    [Required]
    public AwardNominationStatus Status { get; set; }

    [MaxLength(2000)]
    public string? OutcomeReason { get; set; }
}

/// <summary>
/// Detailed DTO for award nomination with related data
/// </summary>
public class AwardNominationDetailDto : AwardNominationDto
{
    public Guid? CommitteeId { get; set; }
    public string? CommitteeName { get; set; }
    // public Guid? AwardLevelId { get; set; }
    // public string? AwardLevelName { get; set; }
    public List<TeamAwardNomineeDto> TeamNominees { get; set; } = new();
    public List<AwardCommitteeReviewDto> CommitteeReviews { get; set; } = new();
}

/// <summary>
/// DTO for setting nomination outcome
/// </summary>
public class SetNominationOutcomeDto
{
    [Required]
    public AwardNominationStatus Status { get; set; }

    [MaxLength(2000)]
    public string? OutcomeReason { get; set; }

    public Guid? AwardLevelId { get; set; }
}

#endregion

#region Team Award Nominee DTOs

/// <summary>
/// DTO for team award nominee read operations
/// </summary>
public class TeamAwardNomineeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid NominationId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Role { get; set; }
    public string? ContributionSummary { get; set; }
    public decimal? RewardPercentage { get; set; }
}

/// <summary>
/// DTO for creating a team award nominee
/// </summary>
public class CreateTeamAwardNomineeDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [MaxLength(200)]
    public string? Role { get; set; }

    [MaxLength(500)]
    public string? ContributionSummary { get; set; }

    [Range(0, 100)]
    public decimal? RewardPercentage { get; set; }
}

/// <summary>
/// DTO for updating a team award nominee
/// </summary>
public class UpdateTeamAwardNomineeDto : UpdateDtoBase
{
    [MaxLength(200)]
    public string? Role { get; set; }

    [MaxLength(500)]
    public string? ContributionSummary { get; set; }

    [Range(0, 100)]
    public decimal? RewardPercentage { get; set; }
}

#endregion

#region Award Nominee Contribution DTOs

/// <summary>
/// DTO for nominee contribution read operations
/// </summary>
public class AwardNomineeContributionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AwardNominationId { get; set; }

    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating a nominee contribution
/// </summary>
public class CreateAwardNomineeContributionDto : CreateDtoBase
{
    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating a nominee contribution
/// </summary>
public class UpdateAwardNomineeContributionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
}

#endregion

#region Award Nomination Attachment DTOs

/// <summary>
/// DTO for nomination attachment read operations
/// </summary>
public class AwardNominationAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AwardNominationId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public AwardAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName => AttachmentType.ToString();

    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
}

/// <summary>
/// DTO for creating a nomination attachment
/// </summary>
public class CreateAwardNominationAttachmentDto : CreateDtoBase
{
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public AwardAttachmentType AttachmentType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// DTO for updating a nomination attachment
/// </summary>
public class UpdateAwardNominationAttachmentDto : UpdateDtoBase
{
    [Required]
    public AwardAttachmentType AttachmentType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

#endregion

#region Award Committee DTOs

/// <summary>
/// DTO for award committee read operations
/// </summary>
public class AwardCommitteeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int QuorumRequired { get; set; }
    public int ReviewDeadlineDays { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
    public List<AwardCommitteeMemberDto> Members { get; set; } = new();
}

/// <summary>
/// DTO for creating an award committee
/// </summary>
public class CreateAwardCommitteeDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(1, 50)]
    public int QuorumRequired { get; set; }

    [Range(1, 365)]
    public int ReviewDeadlineDays { get; set; } = 14;

    [Required]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an award committee
/// </summary>
public class UpdateAwardCommitteeDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(1, 50)]
    public int QuorumRequired { get; set; }

    [Range(1, 365)]
    public int ReviewDeadlineDays { get; set; } = 14;

    [Required]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Award Committee Member DTOs

/// <summary>
/// DTO for award committee member read operations
/// </summary>
public class AwardCommitteeMemberDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid CommitteeId { get; set; }
    public string CommitteeName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Role { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating an award committee member
/// </summary>
public class CreateAwardCommitteeMemberDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [MaxLength(100)]
    public string? Role { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an award committee member
/// </summary>
public class UpdateAwardCommitteeMemberDto : UpdateDtoBase
{
    [MaxLength(100)]
    public string? Role { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Award Committee Review DTOs

/// <summary>
/// DTO for award committee review read operations
/// </summary>
public class AwardCommitteeReviewDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AwardNominationId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public DateTime? ReviewDate { get; set; }
    public bool? Approved { get; set; }
    public int? Score { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// DTO for submitting a committee review
/// </summary>
public class SubmitCommitteeReviewDto : CreateDtoBase
{
    [Required]
    public bool Approved { get; set; }

    [Range(1, 100)]
    public int? Score { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Comments { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating a committee review
/// </summary>
public class UpdateCommitteeReviewDto : UpdateDtoBase
{
    [Required]
    public bool Approved { get; set; }

    [Range(1, 100)]
    public int? Score { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Comments { get; set; } = string.Empty;
}

#endregion

#region Long Service Award DTOs

/// <summary>
/// DTO for long service award read operations
/// </summary>
public class LongServiceAwardDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? DepartmentName { get; set; }
    public Guid AwardTypeId { get; set; }
    public string? AwardTypeName { get; set; }
    public Guid? EmployeeAwardId { get; set; }
    public int YearsOfService { get; set; }
    public DateTime ServiceStartDate { get; set; }
    public DateTime MilestoneDate { get; set; }
    public string AwardDescription { get; set; } = string.Empty;
    public decimal? MonetaryAmount { get; set; }
    public int? LeaveDaysBonus { get; set; }
    public string? OtherBenefits { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public DateTime? PresentationDate { get; set; }
    public string? PresentationNotes { get; set; }
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public bool LeaveProcessed { get; set; }
    public DateTime? LeaveProcessedDate { get; set; }
    public Guid? LeaveId { get; set; }
}

/// <summary>
/// Summary DTO for long service award list views
/// </summary>
public class LongServiceAwardSummaryDto
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public int YearsOfService { get; set; }
    public DateTime MilestoneDate { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? PresentationDate { get; set; }
}

/// <summary>
/// DTO for creating a long service award
/// </summary>
public class CreateLongServiceAwardDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid AwardTypeId { get; set; }

    public Guid? EmployeeAwardId { get; set; }

    [Required]
    [Range(1, 100)]
    public int YearsOfService { get; set; }

    [Required]
    public DateTime ServiceStartDate { get; set; }

    [Required]
    public DateTime MilestoneDate { get; set; }

    [Required]
    [MaxLength(2000)]
    public string AwardDescription { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? LeaveDaysBonus { get; set; }

    [MaxLength(2000)]
    public string? OtherBenefits { get; set; }
}

/// <summary>
/// DTO for updating a long service award
/// </summary>
public class UpdateLongServiceAwardDto : UpdateDtoBase
{
    public Guid? EmployeeAwardId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string AwardDescription { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? LeaveDaysBonus { get; set; }

    [MaxLength(2000)]
    public string? OtherBenefits { get; set; }

    public bool IsProcessed { get; set; }
    public DateTime? PresentationDate { get; set; }

    [MaxLength(2000)]
    public string? PresentationNotes { get; set; }
}

/// <summary>
/// DTO for processing a long service award
/// </summary>
public class ProcessLongServiceAwardDto
{
    [Required]
    public Guid AwardId { get; set; }

    public DateTime? PresentationDate { get; set; }

    [MaxLength(2000)]
    public string? PresentationNotes { get; set; }
}

#endregion


#region Award Cycle DTOs

/// <summary>An award cycle, with its windows and whether they are open right now.</summary>
public class AwardCycleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;

    /// <summary>How this award's winner is chosen — it decides which windows the cycle must carry.</summary>
    public AwardWinnerDecision WinnerDecision { get; set; }
    public string WinnerDecisionName => WinnerDecision.ToString();
    public AwardNominationSource NominationSource { get; set; }
    public string NominationSourceName => NominationSource.ToString();

    public int Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }

    public DateTime? NominationOpensOn { get; set; }
    public DateTime? NominationClosesOn { get; set; }
    public DateTime? VotingOpensOn { get; set; }
    public DateTime? VotingClosesOn { get; set; }

    public AwardCycleStatus Status { get; set; }
    public string StatusName => Status.ToString();

    /// <summary>
    /// Derived, never stored. A stored "nominations are open" flag and a nomination window are two
    /// facts about one thing, and they drift apart the moment the clock passes the close date.
    /// </summary>
    public bool IsNominationOpen { get; set; }
    public bool IsVotingOpen { get; set; }

    /// <summary>What a caller may do right now, in words, when neither window is open.</summary>
    public string? WindowState { get; set; }

    public int NominationCount { get; set; }
    public string? Notes { get; set; }
}

public class AwardCycleSummaryDto
{
    public Guid Id { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }
    public AwardCycleStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsNominationOpen { get; set; }
    public bool IsVotingOpen { get; set; }
    public int NominationCount { get; set; }
}

public class CreateAwardCycleDto : CreateDtoBase
{
    [MaxLength(50)]
    public string CycleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid AwardTypeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    public DateTime? NominationOpensOn { get; set; }
    public DateTime? NominationClosesOn { get; set; }
    public DateTime? VotingOpensOn { get; set; }
    public DateTime? VotingClosesOn { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateAwardCycleDto : UpdateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 4)]
    public int? Quarter { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    public DateTime? NominationOpensOn { get; set; }
    public DateTime? NominationClosesOn { get; set; }
    public DateTime? VotingOpensOn { get; set; }
    public DateTime? VotingClosesOn { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Cancelling a cycle requires saying why — the record outlives the person who cancelled it.</summary>
public class CancelAwardCycleDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region Award Eligibility DTOs

/// <summary>Who qualifies for an award, and why anyone else does not.</summary>
public class AwardEligibilityResultDto
{
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;
    public DateTime AsOf { get; set; }

    /// <summary>Active employees considered — the denominator behind the counts below.</summary>
    public int ConsideredCount { get; set; }
    public int EligibleCount { get; set; }
    public int IneligibleCount { get; set; }

    public List<AwardEligibilityVerdictDto> Eligible { get; set; } = new();

    /// <summary>
    /// Included on purpose. TDC's note has management "set the criteria and then it will qualify
    /// some employees"; whoever sets them needs to see why an expected name is missing, or a
    /// mis-set rule looks identical to a correct one.
    /// </summary>
    public List<AwardEligibilityVerdictDto> Ineligible { get; set; } = new();
}

public class AwardEligibilityVerdictDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public bool IsEligible { get; set; }
    public List<string> Reasons { get; set; } = new();
}

#endregion


#region Award Voting DTOs

/// <summary>What an employee sees when they come to vote.</summary>
public class AwardBallotDto
{
    public Guid AwardCycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; } = string.Empty;

    public DateTime? VotingOpensOn { get; set; }
    public DateTime? VotingClosesOn { get; set; }
    public bool IsVotingOpen { get; set; }

    /// <summary>Whether this employee is in the electorate for this award.</summary>
    public bool IsInElectorate { get; set; }

    /// <summary>What they have already voted for, if anything. Null means they have not voted.</summary>
    public Guid? MyVoteNominationId { get; set; }

    public List<AwardBallotEntryDto> Nominees { get; set; } = new();
}

/// <summary>
/// One name on the ballot.
/// </summary>
/// <remarks>
/// Carries no vote count. The tally is withheld until voting closes, and a per-entry count here
/// would be the same disclosure by another route.
/// </remarks>
public class AwardBallotEntryDto
{
    public Guid NominationId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;
    public Guid? NomineeId { get; set; }
    public string NomineeName { get; set; } = string.Empty;
    public bool IsTeam { get; set; }
    public string? TeamName { get; set; }

    /// <summary>Why they were nominated — what the voter is being asked to judge.</summary>
    public string? Justification { get; set; }

    public bool IsMyVote { get; set; }
}

public class CastAwardVoteDto
{
    [Required]
    public Guid AwardNominationId { get; set; }

    /// <summary>
    /// Optional. TDC's note offers it — "in the portal nomination or voting, people can state their
    /// justification or reason" — and offering is not requiring: a mandatory paragraph before
    /// somebody may vote suppresses turnout.
    /// </summary>
    [MaxLength(2000)]
    public string? Justification { get; set; }
}

public class AwardVoteDto
{
    public Guid Id { get; set; }
    public Guid AwardCycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public Guid AwardNominationId { get; set; }
    public string NomineeName { get; set; } = string.Empty;
    public Guid VoterId { get; set; }
    public DateTime CastOn { get; set; }
    public string? Justification { get; set; }
}

/// <summary>The result of a staff vote, or the reason it is not being shown.</summary>
public class AwardVoteResultDto
{
    public Guid AwardCycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public string AwardTypeName { get; set; } = string.Empty;
    public DateTime? VotingClosesOn { get; set; }
    public bool IsVotingOpen { get; set; }

    /// <summary>Turnout. Safe to show while voting is open: it says nothing about who is winning.</summary>
    public int VotesCast { get; set; }

    /// <summary>False while voting is open — see <see cref="WithheldReason"/>.</summary>
    public bool ResultsAvailable { get; set; }

    /// <summary>Why there is no result: voting still open, not yet opened, no votes, or a tie.</summary>
    public string? WithheldReason { get; set; }

    public List<AwardVoteTallyDto> Tally { get; set; } = new();

    public Guid? WinningNominationId { get; set; }
    public string? WinnerName { get; set; }

    /// <summary>A tie is reported, never broken — see the service for why.</summary>
    public bool IsTied { get; set; }
    public List<Guid> TiedNominationIds { get; set; } = new();
}

public class AwardVoteTallyDto
{
    public Guid NominationId { get; set; }
    public Guid? NomineeId { get; set; }
    public string NomineeName { get; set; } = string.Empty;
    public int Votes { get; set; }
}

#endregion
