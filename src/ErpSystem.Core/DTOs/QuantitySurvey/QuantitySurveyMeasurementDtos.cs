using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyMeasurementLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class QuantitySurveyMeasurementLookupsDto
{
    public IReadOnlyList<QuantitySurveyMeasurementLookupDto> ApprovedBoqLines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyMeasurementLookupDto> ApprovedDrawings { get; init; } = [];
}

public sealed record QuantitySurveyMeasurementLineInputDto
{
    public Guid ClientLineKey { get; init; }
    [Range(1, 999)] public int Sequence { get; init; }
    [Required, StringLength(500), MinLength(2)] public string Description { get; init; } = string.Empty;
    public QuantitySurveyMeasurementFormulaType FormulaType { get; init; }
    [Range(typeof(decimal), "0.0001", "999999999.9999")] public decimal Timesing { get; init; }
    [Range(typeof(decimal), "0.0001", "999999999.9999")] public decimal? Length { get; init; }
    [Range(typeof(decimal), "0.0001", "999999999.9999")] public decimal? Width { get; init; }
    [Range(typeof(decimal), "0.0001", "999999999.9999")] public decimal? Height { get; init; }
    public bool IsDeduction { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
}

public sealed class CreateQuantitySurveyMeasurementRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid? ProjectDrawingId { get; init; }
    public QuantitySurveyMeasurementSourceType SourceType { get; init; }
    [Required, StringLength(200), MinLength(3)] public string Title { get; init; } = string.Empty;
    public DateTime MeasurementDate { get; init; }
    [StringLength(300)] public string? SiteLocation { get; init; }
    [MinLength(1), MaxLength(100)] public List<QuantitySurveyMeasurementLineInputDto> Lines { get; init; } = [];
}

public sealed class UpdateQuantitySurveyMeasurementRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    public Guid? ProjectDrawingId { get; init; }
    public QuantitySurveyMeasurementSourceType SourceType { get; init; }
    [Required, StringLength(200), MinLength(3)] public string Title { get; init; } = string.Empty;
    public DateTime MeasurementDate { get; init; }
    [StringLength(300)] public string? SiteLocation { get; init; }
    [MinLength(1), MaxLength(100)] public List<QuantitySurveyMeasurementLineInputDto> Lines { get; init; } = [];
}

public sealed class RecordQuantitySurveyMeasurementRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class AddQuantitySurveyMeasurementAttachmentRequest
{
    public Guid ClientRequestId { get; init; }
    public QuantitySurveyMeasurementEvidenceType EvidenceType { get; init; }
    [Required, StringLength(200), MinLength(3)] public string Title { get; init; } = string.Empty;
}

public sealed class QuantitySurveyMeasurementListRequest
{
    public Guid? ProjectId { get; init; }
    public string? Status { get; init; }
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 50;
}

public sealed class QuantitySurveyMeasurementLineDto
{
    public Guid Id { get; init; }
    public Guid ClientLineKey { get; init; }
    public int Sequence { get; init; }
    public string Description { get; init; } = string.Empty;
    public QuantitySurveyMeasurementFormulaType FormulaType { get; init; }
    public decimal Timesing { get; init; }
    public decimal? Length { get; init; }
    public decimal? Width { get; init; }
    public decimal? Height { get; init; }
    public bool IsDeduction { get; init; }
    public string Formula { get; init; } = string.Empty;
    public decimal CalculatedQuantity { get; init; }
    public string? Notes { get; init; }
}

public sealed class QuantitySurveyMeasurementAttachmentDto
{
    public Guid Id { get; init; }
    public QuantitySurveyMeasurementEvidenceType EvidenceType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string UploadedByName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class QuantitySurveyMeasurementDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public Guid ProjectBoqVersionId { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid BoqLineKey { get; init; }
    public string BoqLineLabel { get; init; } = string.Empty;
    public decimal BoqQuantity { get; init; }
    public string? UnitOfMeasure { get; init; }
    public Guid? ProjectDrawingId { get; init; }
    public string? DrawingLabel { get; init; }
    public QuantitySurveyMeasurementSourceType SourceType { get; init; }
    public string SheetReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime MeasurementDate { get; init; }
    public string? SiteLocation { get; init; }
    public decimal TotalMeasuredQuantity { get; init; }
    public string Status { get; init; } = string.Empty;
    public string EvidenceMetadataTemplateCode { get; init; } = string.Empty;
    public Guid PreparedById { get; init; }
    public string PreparedByName { get; init; } = string.Empty;
    public DateTime PreparedAt { get; init; }
    public string? RecordedByName { get; init; }
    public DateTime? RecordedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyMeasurementLineDto> Lines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyMeasurementAttachmentDto> Attachments { get; init; } = [];
}

public sealed class QuantitySurveyMeasurementPageDto
{
    public IReadOnlyList<QuantitySurveyMeasurementDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class QuantitySurveyMeasurementRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public DateTime CreatedAt { get; init; }
}
