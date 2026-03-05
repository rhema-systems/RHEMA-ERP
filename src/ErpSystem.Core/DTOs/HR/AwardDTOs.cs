using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// Award Type DTOs
public class AwardTypeListDto
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public AwardCategory Category { get; set; }
    public string CategoryName { get; set; }
    public AwardFrequency Frequency { get; set; }
    public string FrequencyName { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public bool IsActive { get; set; }
    public int TotalAwarded { get; set; }
}

public class AwardTypeDetailDto
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public AwardCategory Category { get; set; }
    public string CategoryName { get; set; }
    public AwardFrequency Frequency { get; set; }
    public string FrequencyName { get; set; }
    public string EligibilityCriteria { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public string OtherBenefits { get; set; }
    public bool RequiresNomination { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAwardTypeDto
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public AwardCategory Category { get; set; }
    public AwardFrequency Frequency { get; set; }
    public string EligibilityCriteria { get; set; }
    public decimal? MonetaryAmount { get; set; }
    public string OtherBenefits { get; set; }
    public bool RequiresNomination { get; set; }
}

// Employee Award DTOs
public class EmployeeAwardListDto
{
    public Guid Id { get; set; }
    public string AwardNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; }
    public AwardCategory Category { get; set; }
    public string CategoryName { get; set; }
    public int Year { get; set; }
    public AwardStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime? PresentationDate { get; set; }
    public decimal? MonetaryAmount { get; set; }
}

public class EmployeeAwardDetailDto
{
    public Guid Id { get; set; }
    public string AwardNumber { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }

    // Award Type Info
    public Guid AwardTypeId { get; set; }
    public string AwardTypeName { get; set; }
    public AwardCategory Category { get; set; }
    public string CategoryName { get; set; }

    // Award Details
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }
    public AwardStatus Status { get; set; }
    public string StatusName { get; set; }
    public string Reason { get; set; }
    public string Citation { get; set; }

    // Nomination
    public Guid? NominatedById { get; set; }
    public string NominatedByName { get; set; }
    public DateTime? NominationDate { get; set; }
    public string NominationJustification { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string ApprovalComments { get; set; }

    // Presentation
    public DateTime? PresentationDate { get; set; }
    public string PresentationVenue { get; set; }
    public Guid? PresentedById { get; set; }
    public string PresentedByName { get; set; }

    // Award Items
    public decimal? MonetaryAmount { get; set; }
    public string CertificateNumber { get; set; }
    public bool TrophyGiven { get; set; }
    public bool PlaqueGiven { get; set; }

    // Payment
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string PaymentReference { get; set; }

    // Publication
    public bool PublishedInternally { get; set; }
    public bool PublishedExternally { get; set; }
    public string PublicationNotes { get; set; }

    // Collections
    public List<AwardAttachmentDto> Attachments { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateEmployeeAwardDto
{
    public Guid EmployeeId { get; set; }
    public Guid AwardTypeId { get; set; }
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }
    public string Reason { get; set; }
    public string Citation { get; set; }
    public Guid? NominatedById { get; set; }
    public string NominationJustification { get; set; }
    public decimal? MonetaryAmount { get; set; }
}

public class ApproveAwardDto
{
    public Guid Id { get; set; }
    public string ApprovalComments { get; set; }
}

public class RecordAwardPresentationDto
{
    public Guid Id { get; set; }
    public DateTime PresentationDate { get; set; }
    public string PresentationVenue { get; set; }
    public Guid PresentedById { get; set; }
    public string CertificateNumber { get; set; }
    public bool TrophyGiven { get; set; }
    public bool PlaqueGiven { get; set; }
    public bool PublishedInternally { get; set; }
    public bool PublishedExternally { get; set; }
    public string PublicationNotes { get; set; }
}

public class AwardAttachmentDto
{
    public Guid Id { get; set; }
    public AttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Award Nomination DTOs
public class AwardNominationListDto
{
    public Guid Id { get; set; }
    public string NominationNumber { get; set; }
    public Guid NomineeId { get; set; }
    public string NomineeName { get; set; }
    public string NomineeNumber { get; set; }
    public string AwardTypeName { get; set; }
    public int Year { get; set; }
    public NominationStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime NominationDate { get; set; }
    public string NominatedByName { get; set; }
}

public class CreateAwardNominationDto
{
    public Guid AwardTypeId { get; set; }
    public Guid NomineeId { get; set; }
    public int Year { get; set; }
    public string Justification { get; set; }
    public string SupportingEvidence { get; set; }
}

// Dashboard DTO
public class AwardsDashboardDto
{
    public int TotalAwardsThisYear { get; set; }
    public int PendingNominations { get; set; }
    public int ScheduledPresentations { get; set; }
    public decimal TotalMonetaryValueDisbursed { get; set; }
    public Dictionary<AwardCategory, int> AwardsByCategory { get; set; }
    public List<EmployeeAwardListDto> RecentAwards { get; set; }
    public List<AwardNominationListDto> PendingApprovals { get; set; }
}