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

    /// <summary>
    /// Whether an employee may put their own name forward for this award.
    /// </summary>
    /// <remarks>
    /// <para><b>Defaults to false, which is the enterprise norm.</b> Peer or manager nomination is
    /// the default in practice and self-nomination is an exception granted per award, because the
    /// two kinds of award ask different questions. An innovation or cost-saving award rests on an
    /// achievement the nominee can evidence themselves; an employee-of-the-month or values award is
    /// a judgement about how somebody is seen by others, which is not a claim one can sensibly make
    /// about oneself. Awards decided by a staff vote are barred almost everywhere, for the obvious
    /// reason.</para>
    ///
    /// <para>The fairness argument HR usually gives is about participation rather than judgement:
    /// self-nomination over-represents people comfortable promoting themselves, which varies by
    /// personality, seniority and culture in ways unrelated to the work. The counter-argument is
    /// that a nomination-only scheme leaves recognition dependent on having an attentive manager.
    /// Per-award configuration is how both are usually satisfied.</para>
    ///
    /// <para>TDC's note does not state a rule. This makes the position a visible setting rather
    /// than a hidden default, so their answer becomes a data change. See
    /// <c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c>.</para>
    /// </remarks>
    public bool AllowSelfNomination { get; set; }


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

    /// <summary>
    /// Whether this target scopes who may win the award or who may vote in it.
    /// See <see cref="AwardTargetPurpose"/>. Defaults to eligibility, so every target written
    /// before voting existed keeps the meaning it had.
    /// </summary>
    public AwardTargetPurpose Purpose { get; set; } = AwardTargetPurpose.Eligibility;

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
/// One run of an award: the period it covers and the windows in which people may take part.
/// </summary>
/// <remarks>
/// <para><b>Why this exists (area 14 slice 3, decision D-4).</b> TDC's note requires the awards to
/// be <i>"listed for people to nominate before the voting takes place"</i> — a nomination window,
/// then a voting window. The nomination entity could only say <c>Year</c>, <c>Quarter</c> and
/// <c>Month</c>, which cannot express "nominations close on Friday and voting opens on Monday".
/// <c>AwardType.Frequency</c> stays what it was: the template that says how often a cycle recurs.
/// This is the run itself.</para>
///
/// <para><b>Open and closed are derived from the dates, not stored.</b> <see cref="Status"/> is a
/// lifecycle flag — is this cycle a draft, published, cancelled or finished — and whether
/// nominations are open right now is <c>Status == Published &amp;&amp; now is inside the window</c>.
/// Storing "NominationsOpen" as a status as well would create two facts that can disagree, which is
/// the defect shape this area has produced four times already (a duplicated employee id, a
/// decorative flag, a name field nothing wrote).</para>
///
/// <para><b>Which windows are required depends on slice 2's selection model.</b> An award decided
/// by <c>StaffVote</c> must carry a voting window; one decided any other way must not, because a
/// voting window on an award nobody votes on is a claim about a ballot that will never happen. An
/// award whose candidates come from a <c>ManagementDirect</c> selection has no nomination stage, so
/// it carries no nomination window either.</para>
/// </remarks>
public class AwardCycle : TenantEntity
{
    [MaxLength(50)]
    public string CycleCode { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public Guid AwardTypeId { get; set; }

    /// <summary>The period the award is for — not the window in which people nominate.</summary>
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }

    public DateTime? NominationOpensOn { get; set; }
    public DateTime? NominationClosesOn { get; set; }

    public DateTime? VotingOpensOn { get; set; }
    public DateTime? VotingClosesOn { get; set; }

    public AwardCycleStatus Status { get; set; } = AwardCycleStatus.Draft;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;

    public virtual ICollection<AwardNomination> Nominations { get; set; } = new List<AwardNomination>();
}

/// <summary>
/// One ballot: one employee's choice of who should win one cycle.
/// </summary>
/// <remarks>
/// <para><b>Why this exists (area 14 slice 5).</b> The staff vote is the centre of TDC's note —
/// <i>"then staff can vote for who is supposed to win"</i>, <i>"a section of the employees or all of
/// them can vote on the nominees with regards to who will win"</i> — and nothing modelled it. A
/// grep for <c>AwardVote</c>, <c>Ballot</c> or <c>CastVote</c> across the solution returned nothing
/// at all.</para>
///
/// <para><b>One vote per voter per cycle, not per nomination.</b> The question the note asks is
/// "who is supposed to win", which is a single choice among the nominees rather than approval of
/// each in turn. The unique index is therefore on (cycle, voter), and changing your mind means
/// updating the ballot you already cast rather than adding a second one.</para>
///
/// <para><b>The justification is kept because TDC asked for it</b> — <i>"in the portal nomination or
/// voting, people can state their justification or reason"</i>. It is optional: requiring a
/// paragraph before somebody may vote suppresses turnout, and the note presents it as something
/// people <i>can</i> do.</para>
///
/// <para><b>A ballot is not secret from the system, and is not shown to colleagues.</b> The voter is
/// recorded because one-vote-per-person cannot be enforced otherwise, and because a disputed result
/// has to be auditable. No read surface returns who voted for whom; the tally is a count.</para>
/// </remarks>
public class AwardVote : TenantEntity
{
    public Guid AwardCycleId { get; set; }

    /// <summary>The nomination being voted for.</summary>
    public Guid AwardNominationId { get; set; }

    /// <summary>The employee casting it, taken from the token and never from the payload.</summary>
    public Guid VoterId { get; set; }

    public DateTime CastOn { get; set; }

    /// <summary>Optional. TDC's note offers it; requiring it would suppress turnout.</summary>
    [MaxLength(2000)]
    public string? Justification { get; set; }

    [ForeignKey(nameof(AwardCycleId))]
    public virtual AwardCycle AwardCycle { get; set; } = null!;

    [ForeignKey(nameof(AwardNominationId))]
    public virtual AwardNomination AwardNomination { get; set; } = null!;

    [ForeignKey(nameof(VoterId))]
    public virtual Employee Voter { get; set; } = null!;
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

    /// <summary>
    /// The run this nomination belongs to (area 14 slice 3). Nullable: nominations raised before
    /// cycles existed have none, and an award taken by direct management selection never has one.
    /// </summary>
    public Guid? AwardCycleId { get; set; }

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

    [ForeignKey(nameof(AwardCycleId))]
    public virtual AwardCycle? AwardCycle { get; set; }

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
