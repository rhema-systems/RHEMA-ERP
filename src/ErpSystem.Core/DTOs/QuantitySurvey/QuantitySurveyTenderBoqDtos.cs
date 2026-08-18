using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyTenderBoqContextDto
{
    public Guid TenderBidId { get; init; }
    public string BidNumber { get; init; } = string.Empty;
    public Guid TenderId { get; init; }
    public string TenderNumber { get; init; } = string.Empty;
    public string TenderTitle { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public Guid TenderBoqVersionId { get; init; }
    public int TenderBoqVersionNumber { get; init; }
    public string TenderBoqSnapshotHash { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public int LineCount { get; init; }
    public decimal TenderBoqTotal { get; init; }
    public bool CanUpload { get; init; }
    public bool RequiresSignature { get; init; }
    public int MaximumFileSizeMb { get; init; }
    public string ReconciliationDeclaration { get; init; } = string.Empty;
}

public sealed class QuantitySurveyTenderBoqIssueDto
{
    public int? RowNumber { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = "Error";
    public string Message { get; init; } = string.Empty;
}

public sealed class QuantitySurveyTenderBoqLineDto
{
    public Guid TenderItemId { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid LineKey { get; init; }
    public int RowNumber { get; init; }
    public string? LineNumber { get; init; }
    public string? ItemCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? UnitOfMeasure { get; init; }
    public decimal TenderQuantity { get; init; }
    public decimal OfferedQuantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal SubmittedLineTotal { get; init; }
    public decimal CalculatedLineTotal { get; init; }
    public decimal ArithmeticDifference { get; init; }
    public string ComparisonStatus { get; init; } = "Matched";
    public IReadOnlyList<QuantitySurveyTenderBoqIssueDto> Findings { get; init; } =
        Array.Empty<QuantitySurveyTenderBoqIssueDto>();
}

public sealed class QuantitySurveyTenderBoqSubmissionDto
{
    public Guid Id { get; init; }
    public Guid TenderBidId { get; init; }
    public Guid TenderBoqVersionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string VettingStatus { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public int LineCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int CommittedLineCount { get; init; }
    public decimal TenderBoqTotal { get; init; }
    public decimal SubmittedTotal { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? CommittedAt { get; init; }
    public DateTime? VettedAt { get; init; }
    public string? VettingNote { get; init; }
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public string? PreviewToken { get; init; }
    public IReadOnlyList<QuantitySurveyTenderBoqIssueDto> Issues { get; init; } =
        Array.Empty<QuantitySurveyTenderBoqIssueDto>();
    public IReadOnlyList<QuantitySurveyTenderBoqLineDto> Lines { get; init; } =
        Array.Empty<QuantitySurveyTenderBoqLineDto>();
}

public sealed class CommitQuantitySurveyTenderBoqSubmissionDto
{
    [Required] public string PreviewToken { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ReconciliationDeclaration { get; set; } = string.Empty;
    [StringLength(200)] public string? SignatoryName { get; set; }
}

public sealed class VetQuantitySurveyTenderBoqSubmissionDto
{
    [Required, RegularExpression("^(Accepted|Rejected)$")]
    public string Decision { get; set; } = string.Empty;

    [Required, StringLength(1000, MinimumLength = 3)]
    public string Note { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
