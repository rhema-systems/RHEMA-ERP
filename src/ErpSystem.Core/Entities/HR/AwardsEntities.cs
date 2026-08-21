using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Awards;

/// <summary>
/// Award type/category definition
/// </summary>
public class AwardType : TenantEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public AwardCategory Category { get; set; }
    public AwardFrequency Frequency { get; set; }

    // Eligibility
    public int? MinServiceYears { get; set; }
    public int? MaxServiceYears { get; set; } // For eligibility window
	public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    // Limits
    public int? MaxAwardsPerPeriod { get; set; } // Max awards given in a period
    public int? MaxAwardsPerEmployee { get; set; } // Max times same employee can win

    // Reward configuration
    public bool HasMonetaryReward { get; set; }
    public decimal? MinMonetaryAmount { get; set; }
    public decimal? MaxMonetaryAmount { get; set; }

    public bool HasCertificate { get; set; }
    public bool HasTrophy { get; set; }
    public int? LeaveDaysBonus { get; set; }

    // Award Levels/Tiers
    public bool HasLevels { get; set; }

    public bool IsTeamAward { get; set; } // Indicates if this award can be given to teams

    public bool RequiresFormalReview { get; set; }
    public int? MinRequiredReviewers { get; set; }

    /// <summary>Where this award's candidates come from. See <see cref="AwardNominationSource"/>.</summary>
    public AwardNominationSource NominationSource { get; set; } = AwardNominationSource.OpenNomination;

    /// <summary>How this award's winner is chosen. See <see cref="AwardWinnerDecision"/>.</summary>
    public AwardWinnerDecision WinnerDecision { get; set; } = AwardWinnerDecision.CommitteeScore;


	[MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

	public virtual ICollection<AwardLevel> Levels { get; set; } = new List<AwardLevel>();
	public virtual ICollection<AwardBudget> Budgets { get; set; } = new List<AwardBudget>();
    public virtual ICollection<AwardTypeTarget> Targets { get; set; } = new List<AwardTypeTarget>();
	public virtual ICollection<AwardNomination> Nominations { get; set; } = new List<AwardNomination>();
    public virtual ICollection<EmployeeAward> Awards { get; set; } = new List<EmployeeAward>();
}

/// <summary>
/// Award levels/tiers (Bronze, Silver, Gold, etc.)
/// </summary>
public class AwardLevel : TenantEntity
{
    public Guid AwardTypeId { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public int Rank { get; set; } // 1, 2, 3, 4 (lower is higher)

    public decimal? MonetaryAmount { get; set; }
    public int? LeaveDaysBonus { get; set; }
    
    [MaxLength(1000)]
    public string? Benefits { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;

    public virtual ICollection<EmployeeAward> Awards { get; set; } = new List<EmployeeAward>();
    public virtual ICollection<AwardNomination> Nominations { get; set; } = new List<AwardNomination>();
}

/// <summary>
/// Defines specific targets (units, positions, etc.) eligible for an award type
/// </summary>
public class AwardTypeTarget : TenantEntity
{
    public Guid AwardTypeId { get; set; }

    public AwardTargetType TargetType { get; set; }
    public Guid? TargetId { get; set; }

    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public bool IsExclusion { get; set; } = false;
	
	[MaxLength(500)]
    public string? Reason { get; set; }
	
	public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;
}

/// <summary>
/// Budget tracking per award type
/// </summary>
public class AwardBudget : TenantEntity
{
    [MaxLength(50)]
    public string BudgetCode { get; set; } = string.Empty;

    public Guid AwardTypeId { get; set; }

    public int Year { get; set; }

    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal ReservedAmount { get; set; } // Approved but not paid
	
	public DateOnly? BudgetStartDate { get; set; }
    public DateOnly? BudgetEndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
	
	public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;
}

/// <summary>
/// Award nomination for committee review
/// </summary>
public class AwardNomination : TenantEntity
{
    [MaxLength(50)]
    public string NominationNumber { get; set; } = string.Empty;

    public Guid AwardTypeId { get; set; }
    public Guid? AwardLevelId { get; set; }

    public Guid? NomineeId { get; set; } // Nullable for team nominations
    public Guid NominatedById { get; set; }

    [MaxLength(150)]
    public string? TeamName { get; set; }
    
    public DateTime NominationDate { get; set; }

    public int Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }

    [MaxLength(2000)]
    public string Justification { get; set; } = string.Empty;
	
	public decimal? ProposedMonetaryAmount { get; set; }
    public int? ProposedLeaveDays { get; set; }

    public AwardNominationStatus Status { get; set; }

    public Guid? CommitteeId { get; set; }

    // Outcome
	public DateTime? OutcomeDate { get; set; }
    
	[MaxLength(2000)]
    public string? OutcomeReason { get; set; }
	
	public Guid? EmployeeAwardId { get; set; }

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;
	
	[ForeignKey(nameof(AwardLevelId))]
    public virtual AwardLevel? AwardLevel { get; set; }

    [ForeignKey(nameof(NomineeId))]
    public virtual Employee? Nominee { get; set; }

    [ForeignKey(nameof(NominatedById))]
    public virtual Employee NominatedBy { get; set; } = null!;

    [ForeignKey(nameof(EmployeeAwardId))]
    public virtual EmployeeAward? Award { get; set; }

    [ForeignKey(nameof(CommitteeId))]
    public virtual AwardCommittee? Committee { get; set; }

    public virtual ICollection<AwardNomineeContribution> NomineeContributions { get; set; } = new List<AwardNomineeContribution>();
    public virtual ICollection<AwardNominationAttachment> Attachments { get; set; } = new List<AwardNominationAttachment>();
    public virtual ICollection<TeamAwardNominee> TeamNominees { get; set; } = new List<TeamAwardNominee>();
    public virtual ICollection<AwardNominationReview> Reviews { get; set; } = new List<AwardNominationReview>();
}

public class AwardNomineeContribution : TenantEntity
{
    public Guid AwardNominationId { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [ForeignKey(nameof(AwardNominationId))]
    public AwardNomination Nomination { get; set; } = null!;
}

public class AwardNominationAttachment : TenantEntity
{
    public Guid AwardNominationId { get; set; }

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public AwardAttachmentType AttachmentType { get; set; }
    
    public DateTime UploadDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(AwardNominationId))]
    public AwardNomination Nomination { get; set; } = null!;

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

/// <summary>
/// Team members in a nomination
/// </summary>
public class TeamAwardNominee : TenantEntity
{
    public Guid NominationId { get; set; }
    public Guid EmployeeId { get; set; }

    [MaxLength(100)]
    public string? Role { get; set; }

    [MaxLength(500)]
    public string? ContributionSummary { get; set; }
	
	public decimal? RewardPercentage { get; set; } // % of monetary reward

    [ForeignKey(nameof(NominationId))]
    public virtual AwardNomination Nomination { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Committee configuration
/// </summary>
public class AwardCommittee : TenantEntity
{
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public int QuorumRequired { get; set; } // Minimum reviewers needed
	public int ReviewDeadlineDays { get; set; } = 14; // Default deadline

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<AwardCommitteeMember> Members { get; set; } = new List<AwardCommitteeMember>();
    public virtual ICollection<AwardNomination> Nominations { get; set; } = new List<AwardNomination>();
}

/// <summary>
/// Committee members
/// </summary>
public class AwardCommitteeMember : TenantEntity
{
    public Guid CommitteeId { get; set; }
    public Guid EmployeeId { get; set; }

    public string Role { get; set; } = "Member"; // Member, Chair, Secretary

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(CommitteeId))]
    public virtual AwardCommittee Committee { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Reviews for a nomination (many-to-many)
/// </summary>
public class AwardNominationReview : TenantEntity
{
    public Guid AwardNominationId { get; set; }
    
    public Guid ReviewerId { get; set; }
    
    public bool? Approved { get; set; } // Vote: Yes/No
	
	[MaxLength(2000)]
    public string? Comments { get; set; }
	
	public DateTime? ReviewDate { get; set; }
    
    [ForeignKey(nameof(AwardNominationId))]
    public virtual AwardNomination AwardNomination { get; set; } = null!;
    
    [ForeignKey(nameof(ReviewerId))]
    public virtual Employee Reviewer { get; set; } = null!;
}

/// <summary>
/// Award given to an employee
/// </summary>
public class EmployeeAward : TenantEntity
{
    [MaxLength(50)]
    public string AwardNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public Guid AwardTypeId { get; set; }
    public Guid? AwardLevelId { get; set; }
	
	public Guid? AwardNominationId { get; set; }

    public DateTime AwardDate { get; set; }

    [MaxLength(2000)]
    public string? Citation { get; set; } // Formal citation text

    // Award Presentation
    public DateTime? PresentationDate { get; set; }

    [MaxLength(70)]
    public string? PresentationVenue { get; set; }
    
    public Guid? PresentedById { get; set; }

    // Reward
    public decimal? MonetaryAmount { get; set; }
    public int? LeaveDaysAwarded { get; set; }
    
    public bool CertificateIssued { get; set; }
    
    [MaxLength(50)]
    public string? CertificateNumber { get; set; }
    
    public bool TrophyIssued { get; set; }
	
	[MaxLength(1000)]
    public string? OtherBenefits { get; set; }

    // Payment
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    public bool LeaveProcessed { get; set; }
    
    public DateTime? LeaveProcessedDate { get; set; }
	
	public Guid? LeaveId { get; set; }

    // Publicity
    public bool PublishToWebsite { get; set; }

    [MaxLength(2000)]
    public string? PublicationNotes { get; set; }

    public DateTime? PublicationDate { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;

    [ForeignKey(nameof(PresentedById))]
    public virtual Employee? PresentedBy { get; set; }

    [ForeignKey(nameof(AwardLevelId))]
    public virtual AwardLevel? AwardLevel { get; set; }

    [ForeignKey(nameof(AwardNominationId))]
    public virtual AwardNomination? AwardNomination { get; set; }

	public virtual ICollection<TeamAwardRecipient> TeamRecipients { get; set; } = new List<TeamAwardRecipient>();
    public virtual ICollection<AwardAttachment> Attachments { get; set; } = new List<AwardAttachment>();
}

/// <summary>
/// Team award recipients (for team awards)
/// </summary>
public class TeamAwardRecipient : TenantEntity
{
    public Guid AwardId { get; set; }
    public Guid EmployeeId { get; set; }

    [MaxLength(100)]
    public string? Role { get; set; } // Team Lead, Member, etc.

    [MaxLength(500)]
    public string? Contribution { get; set; }
	
	public decimal? MonetaryShare { get; set; } // Amount for this recipient
    public int? LeaveDaysShare { get; set; }

    [ForeignKey(nameof(AwardId))]
    public virtual EmployeeAward Award { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

public class AwardAttachment : TenantEntity
{
    public Guid AwardId { get; set; }
    
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    public AwardAttachmentType AttachmentType { get; set; } // Photo, Certificate, Citation
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public DateTime UploadDate { get; set; }

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(AwardId))]
    public virtual EmployeeAward Award { get; set; } = null!;

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

/// <summary>
/// Long service awards tracker
/// </summary>
public class LongServiceAward : TenantEntity
{
    public Guid EmployeeId { get; set; }
	
	public Guid AwardTypeId { get; set; }

    public Guid? EmployeeAwardId { get; set; }

    public int YearsOfService { get; set; }
    
    public DateTime ServiceStartDate { get; set; }
    
    public DateTime MilestoneDate { get; set; }

    [MaxLength(2000)]
    public string AwardDescription { get; set; } = string.Empty;
    
    public decimal? MonetaryAmount { get; set; }

    public int? LeaveDaysBonus { get; set; }
    
    [MaxLength(2000)]
    public string? OtherBenefits { get; set; }

    public bool IsProcessed { get; set; }
    
    public DateTime? ProcessedDate { get; set; }

    public DateTime? PresentationDate { get; set; }
    
    [MaxLength(2000)]
    public string? PresentationNotes { get; set; }

    // Payment tracking
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    // Leave processing
    public bool LeaveProcessed { get; set; }
    public DateTime? LeaveProcessedDate { get; set; }
	public Guid? LeaveId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;

    [ForeignKey(nameof(EmployeeAwardId))]
    public virtual EmployeeAward? EmployeeAward { get; set; }
}
