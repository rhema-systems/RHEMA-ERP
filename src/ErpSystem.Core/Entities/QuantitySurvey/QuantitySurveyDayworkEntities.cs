using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyDayworkSheetStatus { Draft = 0, ContractorSigned = 1, Verified = 2, Rejected = 3 }
public enum QuantitySurveyDayworkLineType { Labour = 0, Material = 1, Plant = 2 }

[Table("QuantitySurveyDayworkSheets")]
public sealed class QuantitySurveyDayworkSheet : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid VariationOrderId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ContractorBusinessPartnerId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string SheetNumber { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    [Required, StringLength(200)] public string WorkLocation { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
    public QuantitySurveyDayworkSheetStatus Status { get; set; }
    [Required, StringLength(3)] public string Currency { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public Guid VariationDecisionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid? ContractorSignedById { get; set; }
    public DateTime? ContractorSignedAt { get; set; }
    [StringLength(64)] public string? ContractorSignatureHash { get; set; }
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }
    [StringLength(64)] public string? VerifierSignatureHash { get; set; }
    [StringLength(2000)] public string? VerificationNote { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectVariationOrder VariationOrder { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public BusinessPartner ContractorBusinessPartner { get; set; } = null!;
    public ICollection<QuantitySurveyDayworkLine> Lines { get; set; } = new List<QuantitySurveyDayworkLine>();
    public ICollection<QuantitySurveyDayworkEvidence> Evidence { get; set; } = new List<QuantitySurveyDayworkEvidence>();
    public ICollection<QuantitySurveyDayworkRevision> Revisions { get; set; } = new List<QuantitySurveyDayworkRevision>();
}

[Table("QuantitySurveyDayworkLines")]
public sealed class QuantitySurveyDayworkLine : TenantEntity
{
    public Guid DayworkSheetId { get; set; }
    public int Sequence { get; set; }
    public QuantitySurveyDayworkLineType LineType { get; set; }
    public Guid RateLibraryRateId { get; set; }
    public Guid RateLibraryItemId { get; set; }
    [Required, StringLength(50)] public string ItemCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ItemNameSnapshot { get; set; } = string.Empty;
    public Guid UnitOfMeasureId { get; set; }
    [Required, StringLength(50)] public string UnitOfMeasureSnapshot { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal UnitRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
    [Required, StringLength(64)] public string SourceHash { get; set; } = string.Empty;
    public QuantitySurveyDayworkSheet DayworkSheet { get; set; } = null!;
    public QuantitySurveyRateLibraryRate RateLibraryRate { get; set; } = null!;
}

[Table("QuantitySurveyDayworkEvidence")]
public sealed class QuantitySurveyDayworkEvidence : TenantEntity
{
    public Guid DayworkSheetId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Required, StringLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public QuantitySurveyDayworkSheet DayworkSheet { get; set; } = null!;
}

[Table("QuantitySurveyDayworkRevisions")]
public sealed class QuantitySurveyDayworkRevision : TenantEntity
{
    public Guid DayworkSheetId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? ActorBusinessPartnerId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;
    public QuantitySurveyDayworkSheet DayworkSheet { get; set; } = null!;
}
