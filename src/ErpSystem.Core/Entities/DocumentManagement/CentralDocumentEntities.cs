using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.DocumentManagement;

[Table("CentralDocumentRecords")]
public class CentralDocumentRecord : TenantEntity
{
    [Required]
    [StringLength(80)]
    public string DocumentReference { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string SourceModule { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string SourceLabel { get; set; } = string.Empty;

    [StringLength(150)]
    public string? SourceEntityType { get; set; }

    [StringLength(180)]
    public string? SourceRecordReference { get; set; }

    public Guid? SourceRecordId { get; set; }

    [StringLength(80)]
    public string? MetadataTemplateCode { get; set; }

    [StringLength(80)]
    public string RepositoryStatus { get; set; } = "Not linked";

    [StringLength(500)]
    public string? RepositoryPath { get; set; }

    [StringLength(500)]
    public string? ExternalDocumentUrl { get; set; }

    [StringLength(120)]
    public string? CurrentVersion { get; set; }

    [StringLength(80)]
    public string VersionStatus { get; set; } = "Draft";

    [StringLength(80)]
    public string AnnotationStatus { get; set; } = "Not required";

    [StringLength(80)]
    public string CommentStatus { get; set; } = "No comments";

    [StringLength(120)]
    public string AccessProfile { get; set; } = "Module restricted";

    [StringLength(120)]
    public string RetentionStatus { get; set; } = "Current";

    [StringLength(80)]
    public string LifecycleStatus { get; set; } = "Active";

    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ReviewDate { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public virtual ICollection<CentralDocumentVersion> Versions { get; set; } = new List<CentralDocumentVersion>();
    public virtual ICollection<CentralDocumentAnnotationReview> AnnotationReviews { get; set; } = new List<CentralDocumentAnnotationReview>();
    public virtual ICollection<CentralDocumentMetadataValue> MetadataValues { get; set; } = new List<CentralDocumentMetadataValue>();
}

[Table("CentralDocumentMetadataValues")]
public class CentralDocumentMetadataValue : TenantEntity
{
    public Guid DocumentRecordId { get; set; }

    [StringLength(80)]
    public string? TemplateCode { get; set; }

    [Required]
    [StringLength(200)]
    public string FieldKey { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string FieldLabel { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? FieldValue { get; set; }

    [StringLength(50)]
    public string ValueType { get; set; } = "text";

    [StringLength(80)]
    public string Source { get; set; } = "Manual";

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public Guid? CapturedById { get; set; }

    public virtual CentralDocumentRecord DocumentRecord { get; set; } = null!;
}

[Table("CentralDocumentVersions")]
public class CentralDocumentVersion : TenantEntity
{
    public Guid DocumentRecordId { get; set; }

    [Required]
    [StringLength(80)]
    public string VersionNumber { get; set; } = "v1.0";

    [StringLength(80)]
    public string Status { get; set; } = "Draft";

    [StringLength(500)]
    public string? RepositoryPath { get; set; }

    [StringLength(500)]
    public string? RenditionPath { get; set; }

    [StringLength(250)]
    public string? FileName { get; set; }

    [StringLength(200)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }

    [StringLength(1000)]
    public string? ChangeSummary { get; set; }

    public virtual CentralDocumentRecord DocumentRecord { get; set; } = null!;
}

[Table("CentralDocumentMetadataTemplates")]
public class CentralDocumentMetadataTemplate : TenantEntity
{
    [Required]
    [StringLength(120)]
    public string Module { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string TemplateCode { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string SourceLabel { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string RequiredFieldsJson { get; set; } = "[]";

    [Column(TypeName = "nvarchar(max)")]
    public string RelationshipsJson { get; set; } = "[]";

    [StringLength(250)]
    public string RetentionRule { get; set; } = string.Empty;

    [StringLength(150)]
    public string AccessProfile { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
}

[Table("CentralDocumentGenerationTemplates")]
public class CentralDocumentGenerationTemplate : TenantEntity
{
    [Required]
    [StringLength(80)]
    public string TemplateCode { get; set; } = string.Empty;

    [Required]
    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string TitleTemplate { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Module { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string SourceLabel { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string MetadataTemplateCode { get; set; } = string.Empty;

    [StringLength(150)]
    public string AccessProfile { get; set; } = "Module restricted";

    [Column(TypeName = "nvarchar(max)")]
    public string MergeFieldsJson { get; set; } = "[]";

    [Column(TypeName = "nvarchar(max)")]
    public string Body { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; } = true;

    [StringLength(120)]
    public string? ApprovalRole { get; set; }

    [StringLength(120)]
    public string? SignatureRole { get; set; }

    [StringLength(120)]
    public string? DefaultDispatchChannel { get; set; }

    public Guid? TemplateFileUploadRecordId { get; set; }

    [StringLength(500)]
    public string? TemplateRepositoryPath { get; set; }

    [StringLength(250)]
    public string? TemplateFileName { get; set; }

    [StringLength(200)]
    public string? TemplateContentType { get; set; }

    public long? TemplateFileSize { get; set; }
}

[Table("CentralDocumentAnnotationReviews")]
public class CentralDocumentAnnotationReview : TenantEntity
{
    public Guid DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }

    [Required]
    [StringLength(150)]
    public string ReviewTitle { get; set; } = string.Empty;

    [StringLength(80)]
    public string Status { get; set; } = "Open";

    [StringLength(80)]
    public string SyncfusionAnnotationStatus { get; set; } = "Not started";

    public Guid? AssignedReviewerId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }

    [StringLength(1000)]
    public string? ReviewNotes { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AnnotationStateJson { get; set; }

    public virtual CentralDocumentRecord DocumentRecord { get; set; } = null!;
    public virtual CentralDocumentVersion? DocumentVersion { get; set; }
}

[Table("CentralDocumentAccessRules")]
public class CentralDocumentAccessRule : TenantEntity
{
    [Required]
    [StringLength(120)]
    public string AccessProfile { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Module { get; set; }

    [StringLength(150)]
    public string? RoleName { get; set; }

    [StringLength(150)]
    public string? PermissionKey { get; set; }

    public bool CanView { get; set; } = true;
    public bool CanUpload { get; set; }
    public bool CanAnnotate { get; set; }
    public bool CanApprove { get; set; }
    public bool CanArchive { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("CentralDocumentRetentionPolicies")]
public class CentralDocumentRetentionPolicy : TenantEntity
{
    [Required]
    [StringLength(120)]
    public string PolicyCode { get; set; } = string.Empty;

    [Required]
    [StringLength(180)]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Module { get; set; }

    [StringLength(150)]
    public string? DocumentType { get; set; }

    public int RetentionDays { get; set; } = 2555;
    public bool RequiresLegalHoldReview { get; set; }
    public bool AllowArchive { get; set; } = true;
    public bool AllowDestruction { get; set; }
    public bool IsActive { get; set; } = true;

    [StringLength(1000)]
    public string? Notes { get; set; }
}
