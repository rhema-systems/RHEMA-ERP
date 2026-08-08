using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyBoqFileDto
{
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public sealed class QuantitySurveyBoqImportIssueDto
{
    public int? RowNumber { get; set; }
    public string? ClientLineKey { get; set; }
    public string? Field { get; set; }
    public string Severity { get; set; } = "Error";
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class QuantitySurveyBoqImportLinePreviewDto
{
    public int RowNumber { get; set; }
    public string ClientLineKey { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public string? SectionCode { get; set; }
    public string? TradeCode { get; set; }
    public string? CostCode { get; set; }
    public string? MeasurementStandard { get; set; }
    public string? MeasurementCode { get; set; }
    public string? LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public string ItemType { get; set; } = "Item";
    public string Description { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? UnitRate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? LineTotal { get; set; }
}

public sealed class QuantitySurveyBoqImportPreviewDto
{
    public Guid SessionId { get; set; }
    public Guid ProjectId { get; set; }
    public string PreviewToken { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int LineCount { get; set; }
    public int ErrorCount { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public IReadOnlyList<QuantitySurveyBoqImportLinePreviewDto> Lines { get; set; } = [];
    public IReadOnlyList<QuantitySurveyBoqImportIssueDto> Issues { get; set; } = [];
}

public sealed class CommitQuantitySurveyBoqImportDto
{
    [Required, StringLength(128)] public string PreviewToken { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public bool ReconciliationConfirmed { get; set; }
}

public sealed class QuantitySurveyBoqImportCommitResultDto
{
    public Guid SessionId { get; set; }
    public Guid ProjectId { get; set; }
    public int CommittedLineCount { get; set; }
    public DateTime CommittedAt { get; set; }
    public string ReconciledBy { get; set; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; set; }
}
