using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyMeasurementSourceType
{
    Design = 0,
    Site = 1
}

public enum QuantitySurveyMeasurementFormulaType
{
    Count = 0,
    Length = 1,
    Area = 2,
    Volume = 3
}

public enum QuantitySurveyMeasurementEvidenceType
{
    DrawingMarkup = 0,
    SitePhoto = 1,
    MeasurementWorkbook = 2,
    SupportingDocument = 3
}

[Table("QuantitySurveyMeasurementSheets")]
public sealed class QuantitySurveyMeasurementSheet : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid BoqLineKey { get; set; }
    public Guid? ProjectDrawingId { get; set; }
    public QuantitySurveyMeasurementSourceType SourceType { get; set; }
    [Required, StringLength(40)] public string SheetReference { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    public DateTime MeasurementDate { get; set; }
    [StringLength(300)] public string? SiteLocation { get; set; }
    [StringLength(100)] public string? DrawingNumberSnapshot { get; set; }
    [StringLength(30)] public string? DrawingRevisionSnapshot { get; set; }
    [StringLength(40)] public string? DrawingStatusSnapshot { get; set; }
    [StringLength(50)] public string? BoqLineNumberSnapshot { get; set; }
    [StringLength(50)] public string? BoqItemCodeSnapshot { get; set; }
    [Required, StringLength(1000)] public string BoqDescriptionSnapshot { get; set; } = string.Empty;
    [StringLength(20)] public string? UnitOfMeasureSnapshot { get; set; }
    [StringLength(30)] public string? MeasurementStandardSnapshot { get; set; }
    [StringLength(50)] public string? MeasurementCodeSnapshot { get; set; }
    [StringLength(2000)] public string? MeasurementRuleSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal BoqQuantitySnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal TotalMeasuredQuantity { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Draft";
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string EvidenceMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    public Guid PreparedById { get; set; }
    [Required, StringLength(300)] public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; }
    public Guid? RecordedById { get; set; }
    [StringLength(300)] public string? RecordedByName { get; set; }
    public DateTime? RecordedAt { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectBoqVersion ProjectBoqVersion { get; set; } = null!;
    public ProjectBoqVersionLine ProjectBoqVersionLine { get; set; } = null!;
    public ProjectDrawing? ProjectDrawing { get; set; }
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentMetadataTemplate EvidenceMetadataTemplate { get; set; } = null!;
    public ICollection<QuantitySurveyMeasurementLine> Lines { get; set; } = new List<QuantitySurveyMeasurementLine>();
    public ICollection<QuantitySurveyMeasurementAttachment> Attachments { get; set; } = new List<QuantitySurveyMeasurementAttachment>();
}

[Table("QuantitySurveyMeasurementLines")]
public sealed class QuantitySurveyMeasurementLine : TenantEntity
{
    public Guid MeasurementSheetId { get; set; }
    public Guid ClientLineKey { get; set; }
    public int Sequence { get; set; }
    [Required, StringLength(500)] public string Description { get; set; } = string.Empty;
    public QuantitySurveyMeasurementFormulaType FormulaType { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Timesing { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? Length { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? Width { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? Height { get; set; }
    public bool IsDeduction { get; set; }
    [Required, StringLength(200)] public string FormulaSnapshot { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal CalculatedQuantity { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }

    public QuantitySurveyMeasurementSheet MeasurementSheet { get; set; } = null!;
}

[Table("QuantitySurveyMeasurementAttachments")]
public sealed class QuantitySurveyMeasurementAttachment : TenantEntity
{
    public Guid MeasurementSheetId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public QuantitySurveyMeasurementEvidenceType EvidenceType { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(120)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Required, StringLength(64)] public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid FileUploadRecordId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid UploadedById { get; set; }
    [Required, StringLength(300)] public string UploadedByName { get; set; } = string.Empty;

    public QuantitySurveyMeasurementSheet MeasurementSheet { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
}

[Table("QuantitySurveyMeasurementRevisions")]
public sealed class QuantitySurveyMeasurementRevision : TenantEntity
{
    public Guid MeasurementSheetId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }

    public QuantitySurveyMeasurementSheet MeasurementSheet { get; set; } = null!;
}
