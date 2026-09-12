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

    /// <summary>
    /// Whether a disciplinary record exempts an employee from this award (AWD-15).
    /// </summary>
    /// <remarks>
    /// TDC stated this rule under their Long Service heading, so it is implemented — but applying it
    /// to an Employee of the Month award would be extending a policy they did not write. Off by
    /// default; HR turns it on for the awards it should govern. A <i>negative record</i> is a
    /// disciplinary action that reached a decision and was not dismissed: a draft case is one nobody
    /// has been formally accused in, a dismissed one is an exoneration.
    /// </remarks>
    public bool DisqualifyOnDisciplinaryRecord { get; set; }

    /// <summary>
    /// How far back a disciplinary record reaches, in months. Null means the whole service period.
    /// </summary>
    /// <remarks>
    /// Null is the note read literally — <i>"any negative records"</i>, with no horizon — so a value
    /// here <b>relaxes</b> the rule rather than tightening it. That direction is deliberate: a window
    /// is a softening TDC has not asked for, and defaulting to one would grant an amnesty nobody
    /// approved.
    /// </remarks>
    public int? DisqualifyingDisciplineMonths { get; set; }

    /// <summary>Minimum appraisal score for automatic candidacy. See the entity for the data caveat.</summary>
    public decimal? MinPerformanceScore { get; set; }

    /// <summary>Minimum completed goals for automatic candidacy.</summary>
    public int? MinGoalsAchieved { get; set; }

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

    /// <summary>
    /// Whether a disciplinary record exempts an employee from this award (AWD-15).
    /// </summary>
    /// <remarks>
    /// TDC stated this rule under their Long Service heading, so it is implemented — but applying it
    /// to an Employee of the Month award would be extending a policy they did not write. Off by
    /// default; HR turns it on for the awards it should govern. A <i>negative record</i> is a
    /// disciplinary action that reached a decision and was not dismissed: a draft case is one nobody
    /// has been formally accused in, a dismissed one is an exoneration.
    /// </remarks>
    public bool DisqualifyOnDisciplinaryRecord { get; set; }

    /// <summary>
    /// How far back a disciplinary record reaches, in months. Null means the whole service period.
    /// </summary>
    /// <remarks>
    /// Null is the note read literally — <i>"any negative records"</i>, with no horizon — so a value
    /// here <b>relaxes</b> the rule rather than tightening it. That direction is deliberate: a window
    /// is a softening TDC has not asked for, and defaulting to one would grant an amnesty nobody
    /// approved.
    /// </remarks>
    public int? DisqualifyingDisciplineMonths { get; set; }

    /// <summary>
    /// Minimum appraisal score for automatic candidacy, when the award's candidates are
    /// performance-triggered. Combines with <see cref="MinGoalsAchieved"/>: set both and a candidate
    /// must satisfy both.
    /// </summary>
    [Range(0, 100)]
    public decimal? MinPerformanceScore { get; set; }

    /// <summary>Minimum completed goals for automatic candidacy.</summary>
    [Range(1, 100)]
    public int? MinGoalsAchieved { get; set; }

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

    /// <summary>
    /// Whether a disciplinary record exempts an employee from this award (AWD-15).
    /// </summary>
    /// <remarks>
    /// TDC stated this rule under their Long Service heading, so it is implemented — but applying it
    /// to an Employee of the Month award would be extending a policy they did not write. Off by
    /// default; HR turns it on for the awards it should govern. A <i>negative record</i> is a
    /// disciplinary action that reached a decision and was not dismissed: a draft case is one nobody
    /// has been formally accused in, a dismissed one is an exoneration.
    /// </remarks>
    public bool DisqualifyOnDisciplinaryRecord { get; set; }

    /// <summary>
    /// How far back a disciplinary record reaches, in months. Null means the whole service period.
    /// </summary>
    /// <remarks>
    /// Null is the note read literally — <i>"any negative records"</i>, with no horizon — so a value
    /// here <b>relaxes</b> the rule rather than tightening it. That direction is deliberate: a window
    /// is a softening TDC has not asked for, and defaulting to one would grant an amnesty nobody
    /// approved.
    /// </remarks>
    public int? DisqualifyingDisciplineMonths { get; set; }

    /// <summary>
    /// Minimum appraisal score for automatic candidacy, when the award's candidates are
    /// performance-triggered. Combines with <see cref="MinGoalsAchieved"/>: set both and a candidate
    /// must satisfy both.
    /// </summary>
    [Range(0, 100)]
    public decimal? MinPerformanceScore { get; set; }

    /// <summary>Minimum completed goals for automatic candidacy.</summary>
    [Range(1, 100)]
    public int? MinGoalsAchieved { get; set; }

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
    /// <summary>
    /// The tier this award was conferred at, for an award that has levels.
    /// </summary>
    /// <remarks>
    /// Slice 0 found that a levelled award could not be conferred AT a level, because the create DTO
    /// carried none. Slice 8 added it — and then found the award could not report the level it had
    /// been given either: the column was written, the read DTO had no such field and the mapper
    /// carried nothing. A Gold award and a Bronze award were indistinguishable on every read.
    /// </remarks>
    public Guid? AwardLevelId { get; set; }
    public string? AwardLevelName { get; set; }


    /// <summary>
    /// The nomination this award came from, where it came from one.
    /// </summary>
    /// <remarks>
    /// The service has always written this column and no read surface exposed it, so the two ends of
    /// the nomination-award link were both set in the database and invisible over the API — nothing
    /// could follow an award back to the case made for it. The third instance of this shape in the
    /// area, after the committee assignment and the award's own cycle.
    /// </remarks>
    public Guid? AwardNominationId { get; set; }
    public string? NominationNumber { get; set; }


    /// <summary>
    /// The run this award was conferred in, where there was one.
    /// </summary>
    /// <remarks>
    /// Added with the column in slice 8. Without it, conferring an award into a cycle produced a
    /// response that did not mention the cycle — the same shape as the committee assignment in
    /// slice 6, where the operation that set a field did not show the field it had set.
    /// </remarks>
    public Guid? AwardCycleId { get; set; }
    public string? AwardCycleName { get; set; }

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
    /// <summary>The tier, so a register can tell a Gold award from a Bronze one.</summary>
    public Guid? AwardLevelId { get; set; }
    public string? AwardLevelName { get; set; }

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

    /// <summary>
    /// The tier being conferred, for an award that has levels.
    /// </summary>
    /// <remarks>
    /// ⚠ Added in slice 8. Slice 0 found that <c>AwardType.HasLevels</c>, <c>AwardLevel</c> and
    /// <c>EmployeeAward.AwardLevelId</c> all existed while this DTO carried no level at all — so a
    /// levelled award could be conferred only at no level, and the created award came back with
    /// <c>awardLevelId: null</c>. The level could then be set by a later edit, if anyone noticed.
    /// </remarks>
    public Guid? AwardLevelId { get; set; }

    /// <summary>The cycle this award belongs to, where it was conferred through one.</summary>
    public Guid? AwardCycleId { get; set; }

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

    /// <summary>
    /// What was actually paid, when it differs from the amount the award carries.
    /// </summary>
    /// <remarks>
    /// Optional: leave it out and the award's own <c>MonetaryAmount</c> is taken as paid. It exists
    /// because the sum that leaves the bank is the sum that should consume the budget, and the two
    /// can differ — a rounding, a partial payment, a currency conversion. The award's
    /// <c>MonetaryAmount</c> is what was promised; this is what was paid.
    ///
    /// ⚠ This is a RECORD, not a posting. Per the HR-Finance split it does not touch the general
    /// ledger; the amount is registered in docs/HR-FINANCE-INTEGRATION-BACKLOG.md for the sweep that
    /// runs after the module.
    /// </remarks>
    [Range(0, double.MaxValue)]
    public decimal? AmountPaid { get; set; }
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

    /// <summary>
    /// ⚠ <b>Never a link.</b> The file lives outside the web root and the download needs the
    /// bearer token, so this is for the server's own use; a screen must call the download route.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    public long? FileSizeBytes { get; set; }
    public AwardAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName => AttachmentType.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string? UploadedByName { get; set; }
}

/// <summary>
/// DTO for creating an award attachment
/// </summary>
/// <summary>
/// The metadata beside an uploaded award file.
/// </summary>
/// <remarks>
/// ⚠ <b>`FileName` and `FilePath` are gone deliberately (ledger D-39)</b> — see
/// <see cref="CreateAwardNominationAttachmentDto"/>. The award id comes from the route.
/// </remarks>
public class CreateAwardAttachmentDto : CreateDtoBase
{
    public Guid AwardId { get; set; }

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

    /// <summary>
    /// The committee reviewing it, once assigned.
    /// </summary>
    /// <remarks>
    /// Moved up from <c>AwardNominationDetailDto</c> in slice 6. <c>AssignToCommitteeAsync</c>
    /// returns this DTO, so before the move the operation that assigned a committee did not show
    /// the committee it had just assigned — the caller had to re-fetch a different endpoint to see
    /// whether their own call had worked.
    /// </remarks>
    public Guid? CommitteeId { get; set; }
    public string? CommitteeName { get; set; }

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
    // CommitteeId / CommitteeName now live on the base DTO - see the remarks there.
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

    /// <summary>⚠ Never a link — see AwardAttachmentDto.FilePath.</summary>
    public string FilePath { get; set; } = string.Empty;

    public long? FileSizeBytes { get; set; }

    public AwardAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName => AttachmentType.ToString();

    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string? UploadedByName { get; set; }
}

/// <summary>
/// DTO for creating a nomination attachment
/// </summary>
/// <summary>
/// The metadata beside an uploaded nomination file.
/// </summary>
/// <remarks>
/// ⚠ <b>`FileName` and `FilePath` are gone deliberately (ledger D-39).</b> They were taken from the
/// caller as JSON and stored, so an "attachment" was a string somebody typed and the list rendered
/// it beautifully. The file now arrives as multipart through the controlled gate, which sets both.
/// </remarks>
public class CreateAwardNominationAttachmentDto : CreateDtoBase
{
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

    /// <summary>This member's score, 1–100. The winner is decided on the average of these.</summary>
    public int Score { get; set; }

    public string? Comments { get; set; }
}

/// <summary>
/// DTO for submitting a committee review
/// </summary>
public class SubmitCommitteeReviewDto : CreateDtoBase
{
    /// <summary>
    /// This member's score, 1–100. Required — an unscored review cannot contribute to an average,
    /// and TDC's note decides the winner on the average.
    /// </summary>
    [Required]
    [Range(1, 100)]
    public int Score { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Comments { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating a committee review
/// </summary>
public class UpdateCommitteeReviewDto : UpdateDtoBase
{
    /// <summary>Revised score, 1–100.</summary>
    [Required]
    [Range(1, 100)]
    public int Score { get; set; }

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

    /// <summary>
    /// One page of verdicts, filtered by <see cref="Filter"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>This was two unbounded lists until decision D-9.</b> Every active employee got a
    /// verdict and both lists came back whole — measured on the live tenant that is <b>5,579</b>
    /// rows, most of them ineligible and each carrying its own reasons, on an endpoint a screen calls
    /// as soon as somebody picks an award. Paging is not a nicety here.</para>
    ///
    /// <para><b>The ineligible are still in it, with their reasons</b>, which was the other half of
    /// D-9. TDC's note has management <i>"set the criteria and then it will qualify some
    /// employees"</i>; whoever sets them needs to see why an expected name is missing, or a mis-set
    /// rule and a correct one produce the same screen. Filtering them out is the reader's choice, not
    /// the endpoint's.</para>
    ///
    /// <para>The two lists became one because a screen shows one table. Paging two lists against a
    /// single page number would have meant a page 3 that held the third page of one list beside the
    /// third page of the other — a shape with no reading.</para>
    /// </remarks>
    public List<AwardEligibilityVerdictDto> Items { get; set; } = new();

    /// <summary><c>all</c>, <c>eligible</c> or <c>ineligible</c> — what <see cref="Items"/> holds.</summary>
    public string Filter { get; set; } = "all";

    public int Page { get; set; }
    public int PageSize { get; set; }

    /// <summary>
    /// Verdicts matching <see cref="Filter"/> - what the pages divide.
    /// </summary>
    /// <remarks>
    /// Named to match <c>PagedResult&lt;T&gt;</c>, which every other paged HR endpoint returns:
    /// <c>totalCount</c> / <c>hasNext</c> / <c>hasPrevious</c>. This DTO cannot BE a
    /// <c>PagedResult</c> because it carries the three unfiltered counts as well, but it can at
    /// least speak the same language. The frontend already carries a warning comment about HR and
    /// Finance disagreeing on these names; a third dialect inside one module would be worse than
    /// either.
    /// </remarks>
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNext { get; set; }
    public bool HasPrevious { get; set; }
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


#region Award Committee Result DTOs

/// <summary>
/// What the committee decided, or why it has not decided yet.
/// </summary>
/// <remarks>
/// TDC's note: <i>"the committee members will score, and the winner will be the one with the highest
/// average score"</i>. The average is reported alongside how many members produced it, because an
/// average of one score is not the same claim as an average of five and a screen that showed only
/// the number would not let anyone tell them apart.
/// </remarks>
public class AwardCommitteeResultDto
{
    public Guid AwardCycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public string AwardTypeName { get; set; } = string.Empty;

    /// <summary>How many members must score a nomination before it counts. Null means no minimum.</summary>
    public int? MinRequiredReviewers { get; set; }

    public bool ResultAvailable { get; set; }

    /// <summary>Why there is no winner: nothing scored, too few reviewers, or a tie.</summary>
    public string? WithheldReason { get; set; }

    public List<AwardCommitteeScoreDto> Scores { get; set; } = new();

    public Guid? WinningNominationId { get; set; }
    public string? WinnerName { get; set; }

    public bool IsTied { get; set; }
    public List<Guid> TiedNominationIds { get; set; } = new();
}

public class AwardCommitteeScoreDto
{
    public Guid NominationId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;
    public Guid? NomineeId { get; set; }
    public string NomineeName { get; set; } = string.Empty;

    /// <summary>The average of every score this nomination received. Null when nobody has scored it.</summary>
    public double? AverageScore { get; set; }

    public int ReviewerCount { get; set; }

    /// <summary>
    /// False when fewer members have scored than the award requires. Such a nomination is listed
    /// with its partial average rather than hidden — the committee needs to see who is still
    /// outstanding — but it cannot win.
    /// </summary>
    public bool MeetsReviewerMinimum { get; set; }
}

#endregion


#region Award Generation DTOs

/// <summary>What a generation run did, and what it found to work with.</summary>
public class AwardGenerationResultDto
{
    public Guid AwardCycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public string AwardTypeName { get; set; } = string.Empty;

    public decimal? MinPerformanceScore { get; set; }
    public int? MinGoalsAchieved { get; set; }

    /// <summary>
    /// How much evidence existed to judge against. Reported so that a run finding nobody can be
    /// told apart from a rule that is wrong — measured 2026-08-21, only 18 of 4,328 appraisals
    /// carry a score at all.
    /// </summary>
    public int AppraisalsExamined { get; set; }
    public int GoalsExamined { get; set; }

    /// <summary>Employees the triggers matched, before eligibility is applied.</summary>
    public int MatchedByPerformance { get; set; }

    /// <summary>Of those, how many the award's eligibility criteria then excluded.</summary>
    public int ExcludedByEligibility { get; set; }

    /// <summary>Of those, how many already had a nomination in this cycle.</summary>
    public int AlreadyNominated { get; set; }

    public int NominationsCreated { get; set; }

    public List<AwardGeneratedNomineeDto> Created { get; set; } = new();

    /// <summary>Set when nothing could be generated, and why.</summary>
    public string? Note { get; set; }
}

public class AwardGeneratedNomineeDto
{
    public Guid NominationId { get; set; }
    public string NominationNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

#endregion

#region Long Service Milestone DTOs

/// <summary>One rung of a long-service ladder.</summary>
public class LongServiceMilestoneDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AwardTypeId { get; set; }
    public string? AwardTypeName { get; set; }

    public int Years { get; set; }
    public string? Name { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public int? LeaveDaysBonus { get; set; }
    public string? Benefits { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLongServiceMilestoneDto : CreateDtoBase
{
    [Required]
    public Guid AwardTypeId { get; set; }

    [Required]
    [Range(1, 100)]
    public int Years { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }

    [Range(0, 365)]
    public int? LeaveDaysBonus { get; set; }

    [MaxLength(1000)]
    public string? Benefits { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateLongServiceMilestoneDto : UpdateDtoBase
{
    [Required]
    [Range(1, 100)]
    public int Years { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MonetaryAmount { get; set; }

    [Range(0, 365)]
    public int? LeaveDaysBonus { get; set; }

    [MaxLength(1000)]
    public string? Benefits { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>What a seed run did, and what it left alone.</summary>
/// <remarks>
/// <b>Seeding is additive and never overwrites.</b> A rung HR has already priced must survive a
/// second press of the button, so an existing year is reported as skipped rather than reset to an
/// empty default — which would silently delete the money.
/// </remarks>
public class LongServiceLadderSeedResultDto
{
    public Guid AwardTypeId { get; set; }

    /// <summary>Where the years came from: the company HR policy, or the built-in default.</summary>
    public string Source { get; set; } = string.Empty;

    public List<int> Created { get; set; } = new();
    public List<int> AlreadyPresent { get; set; } = new();

    /// <summary>
    /// The company-wide milestone list, offered rather than applied.
    /// </summary>
    /// <remarks>
    /// <c>CompanyHrPolicy.LongServiceMilestoneYears</c> is the tenant's shared notion of service
    /// milestones and its documentation has always claimed to feed awards. It is reported here so a
    /// screen can offer "use the company list" in one click — but it is not applied silently,
    /// because the provider behind it cannot tell a value HR chose from its own coded default.
    /// </remarks>
    public List<int> CompanyPolicyYears { get; set; } = new();
}

/// <summary>One employee at one rung, as the sweep sees them.</summary>
public class LongServiceCandidateDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public int YearsOfService { get; set; }
    public int MilestoneYears { get; set; }
    public Guid MilestoneId { get; set; }
    public DateTime? ServiceStartDate { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Which years to seed a ladder with. An empty list means "use the default".</summary>
public class SeedLongServiceLadderDto
{
    public List<int> Years { get; set; } = new();
}

/// <summary>The result of a sweep — the same shape whether it was a preview or a run.</summary>
public class LongServiceSweepResultDto
{
    public Guid AwardTypeId { get; set; }
    public DateTime AsOf { get; set; }

    /// <summary>False for a preview. True when awards were actually written.</summary>
    public bool Committed { get; set; }

    public int MilestonesConfigured { get; set; }
    public int EmployeesConsidered { get; set; }
    public int WithoutEmploymentDate { get; set; }
    /// <summary>
    /// Whether the disciplinary exemption was applied at all (AWD-15).
    /// </summary>
    /// <remarks>
    /// This is a different fact from how many people it caught, which <c>Disqualified</c> already
    /// lists. It separates "the rule ran and exempted nobody" from "the rule is switched off for
    /// this award" — two results that look identical on screen and mean opposite things.
    /// </remarks>
    public bool DisciplinaryCheckApplied { get; set; }

    /// <summary>Awards created by this run. Always zero on a preview.</summary>
    public int AwardsCreated { get; set; }

    public List<LongServiceCandidateDto> Qualified { get; set; } = new();
    public List<LongServiceCandidateDto> Disqualified { get; set; } = new();
}

#endregion
