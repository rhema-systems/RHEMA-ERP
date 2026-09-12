using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — CONTROLLED DOCUMENT REGISTER (slice 16)
// Domain: W. SHE Controlled Document Register (FR-SHE-246 version control,
//         FR-SHE-170 filing and retrieval; SRS §14 / SoW Module 14).
// Version rows are projections of the central DMS's CentralDocumentVersion —
// the register keeps no parallel file store.
// ============================================================================

public class SheControlledDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheControlledDocumentCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public SheControlledDocumentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Keywords { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public string? CurrentVersionLabel { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int? ReviewFrequencyMonths { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid? ArchivedById { get; set; }
    public string? ArchivedByName { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public string? ArchiveReason { get; set; }
    public string? Notes { get; set; }

    /// <summary>Document history (FR-SHE-246), newest first.</summary>
    public List<SheControlledDocumentVersionDto> Versions { get; set; } = new();
}

public class SheControlledDocumentSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheControlledDocumentCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public SheControlledDocumentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string OwnerName { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public string? CurrentVersionLabel { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
}

/// <summary>A register document's version row, projected from the central DMS.</summary>
public class SheControlledDocumentVersionDto
{
    public Guid Id { get; set; }
    public string VersionNumber { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public string? ChangeSummary { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateSheControlledDocumentDto : CreateDtoBase
{
    /// <summary>Optional — server-assigned SHE-DOC-YYYY-NNNN when blank.</summary>
    [MaxLength(30)]
    public string? DocumentNumber { get; set; }

    [Required, MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public SheControlledDocumentCategory Category { get; set; }

    [MaxLength(500)]
    public string? Keywords { get; set; }

    [Required]
    public Guid OwnerId { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public Guid? LocationId { get; set; }

    [Range(1, 120)]
    public int? ReviewFrequencyMonths { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Metadata only — versions, approval and archival move through their own actions.</summary>
public class UpdateSheControlledDocumentDto : UpdateDtoBase
{
    [Required, MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public SheControlledDocumentCategory Category { get; set; }

    [MaxLength(500)]
    public string? Keywords { get; set; }

    [Required]
    public Guid OwnerId { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public Guid? LocationId { get; set; }

    [Range(1, 120)]
    public int? ReviewFrequencyMonths { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// The approval step (FR-SHE-246 approval workflow): Draft or UnderReview →
/// Active. Refused while the document has no uploaded version.
/// </summary>
public class ActivateSheControlledDocumentDto
{
    [Required]
    public Guid DocumentId { get; set; }

    /// <summary>Defaults to today.</summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Defaults to EffectiveDate + ReviewFrequencyMonths when the register row
    /// carries a frequency; otherwise stays unset (no forced review cycle).
    /// </summary>
    public DateTime? NextReviewDate { get; set; }
}

public class ArchiveSheControlledDocumentDto
{
    [Required]
    public Guid DocumentId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}
