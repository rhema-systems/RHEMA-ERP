using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

#region Checklist Template DTOs

public class AwardVerificationChecklistTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? MinContractValue { get; set; }
    public decimal? MaxContractValue { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AwardVerificationChecklistItemDto> Items { get; set; } = new();
}

public class AwardVerificationChecklistItemDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string ItemText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool RequiresDocument { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAwardVerificationChecklistTemplateDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public decimal? MinContractValue { get; set; }
    public decimal? MaxContractValue { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;

    public List<CreateAwardVerificationChecklistItemDto> Items { get; set; } = new();
}

public class CreateAwardVerificationChecklistItemDto
{
    [Required]
    [MaxLength(200)]
    public string ItemText { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; } = 0;
    public bool IsRequired { get; set; } = true;
    public bool RequiresDocument { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateAwardVerificationChecklistTemplateDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public decimal? MinContractValue { get; set; }
    public decimal? MaxContractValue { get; set; }

    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
}

#endregion

#region Verification DTOs

public class TenderAwardVerificationDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? StartedByName { get; set; }
    public string? CompletedByName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TenderAwardVerificationBidderDto> Bidders { get; set; } = new();
}

public class TenderAwardVerificationBidderDto
{
    public Guid Id { get; set; }
    public Guid VerificationId { get; set; }
    public Guid TenderBidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? VerifiedDate { get; set; }
    public string? VerifiedByName { get; set; }
    public string? OverallComments { get; set; }
    public int TotalItems { get; set; }
    public int VerifiedItems { get; set; }
    public int PassedItems { get; set; }
    public int FailedItems { get; set; }
    public List<TenderAwardVerificationItemResultDto> ItemResults { get; set; } = new();
}

public class TenderAwardVerificationItemResultDto
{
    public Guid Id { get; set; }
    public Guid BidderId { get; set; }
    public Guid ChecklistItemId { get; set; }
    public string ItemText { get; set; } = string.Empty;
    public string? ItemDescription { get; set; }
    public string? ItemCategory { get; set; }
    public bool IsRequired { get; set; }
    public bool RequiresDocument { get; set; }
    public bool IsVerified { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Comments { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? VerifiedByName { get; set; }
    public List<TenderAwardVerificationItemDocumentDto> Documents { get; set; } = new();
}

public class TenderAwardVerificationItemDocumentDto
{
    public Guid Id { get; set; }
    public Guid ItemResultId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string DocumentType { get; set; } = "General";
    public string? Description { get; set; }
    public Guid UploadedById { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedDate { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}

public class UploadVerificationItemDocumentDto
{
    [Required]
    public Guid ItemResultId { get; set; }

    [MaxLength(50)]
    public string DocumentType { get; set; } = "General";

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class UploadVerificationDocumentDto
{
    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = "Other";

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class StartAwardVerificationDto
{
    [Required]
    public Guid TenderId { get; set; }

    public Guid? TemplateId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> SelectedBidIds { get; set; } = new();

    public string? Notes { get; set; }
}

public class VerifyChecklistItemDto
{
    [Required]
    public Guid BidderId { get; set; }

    [Required]
    public Guid ChecklistItemId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Passed"; // Passed, Failed, NotApplicable

    public string? Comments { get; set; }
}

public class CompleteBidderVerificationDto
{
    [Required]
    public Guid BidderId { get; set; }

    public string? OverallComments { get; set; }
}

public class CompleteVerificationDto
{
    public string? Notes { get; set; }
}

#endregion
