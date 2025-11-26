using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Awards;

/// <summary>
/// Award type/category definition
/// </summary>
public class AwardType : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public AwardCategory Category { get; set; } // Performance, Service, Innovation, Safety
    public AwardFrequency Frequency { get; set; } // Monthly, Quarterly, Annual, Ad-hoc

    // Eligibility
    public int? MinServiceYears { get; set; }
    public string? EligibilityCriteria { get; set; }

    // Award Details
    public bool HasMonetaryReward { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public bool HasCertificate { get; set; }
    public bool HasTrophy { get; set; }
    public string? OtherBenefits { get; set; }

    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<EmployeeAward> Awards { get; set; } = new List<EmployeeAward>();
}

/// <summary>
/// Award given to an employee
/// </summary>
public class EmployeeAward : TenantEntity
{
    public string AwardNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid AwardTypeId { get; set; }
    public AwardType AwardType { get; set; } = null!;

    // Award Details
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }

    public DateTime AwardDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Citation { get; set; } // Formal citation text

    // Nomination
    public Guid? NominatedById { get; set; }
    public Employee? NominatedBy { get; set; }
    public DateTime? NominationDate { get; set; }
    public string? NominationJustification { get; set; }

    // Approval
    public AwardStatus Status { get; set; }
    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }

    // Award Presentation
    public DateTime? PresentationDate { get; set; }
    public string? PresentationVenue { get; set; }
    public Guid? PresentedById { get; set; }
    public Employee? PresentedBy { get; set; }

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

    public ICollection<AwardAttachment> Attachments { get; set; } = new List<AwardAttachment>();
}

public class AwardAttachment : TenantEntity
{
    public Guid AwardId { get; set; }
    public EmployeeAward Award { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public AttachmentType Type { get; set; } // Photo, Certificate, Citation
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Award nomination for committee review
/// </summary>
public class AwardNomination : TenantEntity
{
    public string NominationNumber { get; set; } = string.Empty;

    public Guid AwardTypeId { get; set; }
    public AwardType AwardType { get; set; } = null!;

    public Guid NomineeId { get; set; }
    public Employee Nominee { get; set; } = null!;

    public Guid NominatedById { get; set; }
    public Employee NominatedBy { get; set; } = null!;
    public DateTime NominationDate { get; set; }

    public int Year { get; set; }
    public int? Period { get; set; } // Quarter or Month

    public string Justification { get; set; } = string.Empty;
    public string? SupportingEvidence { get; set; }

    public AwardNominationStatus Status { get; set; }

    // Committee Review
    public DateTime? ReviewDate { get; set; }
    public Guid? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public string? ReviewComments { get; set; }

    // Outcome
    public bool IsApproved { get; set; }
    public DateTime? OutcomeDate { get; set; }
    public string? OutcomeReason { get; set; }

    // If approved, link to actual award
    public Guid? AwardId { get; set; }
    public EmployeeAward? Award { get; set; }
}

/// <summary>
/// Long service awards tracker
/// </summary>
public class LongServiceAward : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int YearsOfService { get; set; }
    public DateTime ServiceStartDate { get; set; }
    public DateTime MilestoneDate { get; set; }

    public string AwardDescription { get; set; } = string.Empty;
    public decimal? MonetaryGift { get; set; }
    public int? LeaveDaysBonus { get; set; }
    public string? OtherBenefits { get; set; }

    public bool IsProcessed { get; set; }
    public DateTime? ProcessedDate { get; set; }

    public DateTime? PresentationDate { get; set; }
    public string? PresentationNotes { get; set; }
}
