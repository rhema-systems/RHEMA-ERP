using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyTenderBoqSubmissionStatus
{
    ValidationFailed = 0,
    Validated = 1,
    Committed = 2,
    Vetted = 3,
    Rejected = 4
}

public enum QuantitySurveyTenderBoqVettingStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}

[Table("QuantitySurveyTenderBoqSubmissions")]
public sealed class QuantitySurveyTenderBoqSubmission : TenantEntity
{
    public Guid TenderBidId { get; set; }
    public Guid TenderId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TenderBoqVersionId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public QuantitySurveyExternalSubmissionChannel Channel { get; set; }
    public QuantitySurveyTenderBoqSubmissionStatus Status { get; set; }
    public QuantitySurveyTenderBoqVettingStatus VettingStatus { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }

    [Required, StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string FileHash { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string NormalizedPayloadHash { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string PreviewTokenHash { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string TenderBoqSnapshotHash { get; set; } = string.Empty;

    [StringLength(100)]
    public string? IdempotencyKey { get; set; }

    public Guid SubmittedByUserId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int LineCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int CommittedLineCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TenderBoqTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubmittedTotal { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")]
    public string NormalizedPayloadJson { get; set; } = "[]";

    [Required, Column(TypeName = "nvarchar(max)")]
    public string IssuesJson { get; set; } = "[]";

    [Required, StringLength(100)]
    public string AuditAction { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string ActorRoles { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ReconciliationDeclaration { get; set; }

    [StringLength(200)]
    public string? SignatoryName { get; set; }

    public Guid? CommittedByUserId { get; set; }
    public DateTime? CommittedAt { get; set; }
    public Guid? VettedByUserId { get; set; }
    public DateTime? VettedAt { get; set; }

    [StringLength(1000)]
    public string? VettingNote { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public TenderBid TenderBid { get; set; } = null!;
    public Tender Tender { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ProjectBoqVersion TenderBoqVersion { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public FileUploadRecord? FileUploadRecord { get; set; }
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public ICollection<QuantitySurveyTenderBoqSubmissionLine> Lines { get; set; } =
        new List<QuantitySurveyTenderBoqSubmissionLine>();
}

[Table("QuantitySurveyTenderBoqSubmissionLines")]
public sealed class QuantitySurveyTenderBoqSubmissionLine : TenantEntity
{
    public Guid SubmissionId { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid TenderItemId { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid LineKey { get; set; }
    public int RowNumber { get; set; }

    [StringLength(50)] public string? LineNumber { get; set; }
    [StringLength(50)] public string? ItemCode { get; set; }
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    [StringLength(20)] public string? UnitOfMeasure { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal TenderQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal OfferedQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SubmittedLineTotal { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CalculatedLineTotal { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ArithmeticDifference { get; set; }
    [Required, StringLength(30)] public string ComparisonStatus { get; set; } = "Matched";
    [Column(TypeName = "nvarchar(max)")] public string FindingsJson { get; set; } = "[]";

    public QuantitySurveyTenderBoqSubmission Submission { get; set; } = null!;
    public TenderBid TenderBid { get; set; } = null!;
    public TenderItem TenderItem { get; set; } = null!;
    public ProjectBoqVersionLine ProjectBoqVersionLine { get; set; } = null!;
}
