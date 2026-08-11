using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyVariationSourceType { SiteInstruction, ChangeRequest, DirectVariation, ChangeOrder }

[Table("QuantitySurveyVariationValuationLines")]
public sealed class QuantitySurveyVariationValuationLine : TenantEntity
{
    public Guid VariationOrderId { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid BoqLineKey { get; set; }
    public int Sequence { get; set; }
    [Required, StringLength(50)] public string LineReferenceSnapshot { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string DescriptionSnapshot { get; set; } = string.Empty;
    [StringLength(20)] public string? UnitSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal QuantityChange { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal UnitRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Required, StringLength(1000)] public string ValuationReason { get; set; } = string.Empty;
    [Required, StringLength(64)] public string SourceHash { get; set; } = string.Empty;
    public ProjectVariationOrder VariationOrder { get; set; } = null!;
    public ProjectBoqVersionLine ProjectBoqVersionLine { get; set; } = null!;
}

[Table("QuantitySurveyVariationEvidence")]
public sealed class QuantitySurveyVariationEvidence : TenantEntity
{
    public Guid VariationOrderId { get; set; }
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
    public ProjectVariationOrder VariationOrder { get; set; } = null!;
}

[Table("QuantitySurveyVariationRevisions")]
public sealed class QuantitySurveyVariationRevision : TenantEntity
{
    public Guid VariationOrderId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;
    public ProjectVariationOrder VariationOrder { get; set; } = null!;
}
