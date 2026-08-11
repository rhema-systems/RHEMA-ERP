using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyPriceIndexFileDto
{
    public byte[] Content { get; init; } = [];
    public string ContentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
}

public sealed class QuantitySurveyPriceIndexImportIssueDto
{
    public int? RowNumber { get; init; }
    public string Field { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "Error";
}

public sealed class QuantitySurveyPriceIndexValueDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public DateTime IndexPeriod { get; init; }
    public decimal IndexValue { get; init; }
    public DateTime PublicationDate { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
    public Guid ValueKey { get; init; }
    public int Version { get; init; }
    public Guid? SupersedesValueId { get; init; }
}

public sealed class QuantitySurveyPriceIndexImportDto
{
    public Guid Id { get; init; }
    public Guid IndexFamilyId { get; init; }
    public string IndexFamilyCode { get; init; } = string.Empty;
    public string IndexFamilyName { get; init; } = string.Empty;
    public QuantitySurveyIndexSource IndexSource { get; init; }
    public string ImportFormat { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string EvidenceLabel { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public Guid ConfigurationDecisionId { get; init; }
    public Guid ApprovalWorkflowDefinitionId { get; init; }
    public Guid AuthorityRoleId { get; init; }
    public string AuthorityRoleName { get; init; } = string.Empty;
    public int LineCount { get; init; }
    public int ErrorCount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid PreparedById { get; init; }
    public DateTime PreparedAt { get; init; }
    public Guid? SubmittedById { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyPriceIndexValueDto> Values { get; init; } = [];
    public IReadOnlyList<QuantitySurveyPriceIndexImportIssueDto> Issues { get; init; } = [];
}

public sealed class QuantitySurveyPriceIndexImportListRequest
{
    public Guid? IndexFamilyId { get; init; }
    [StringLength(30)]
    public string? Status { get; init; }
    [StringLength(200)]
    public string? Search { get; init; }
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 25;
}

public sealed class QuantitySurveyPriceIndexImportPageDto
{
    public IReadOnlyList<QuantitySurveyPriceIndexImportDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class StageQuantitySurveyPriceIndexImportRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid AuthorityRoleId { get; init; }
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyPriceIndexImportLifecycleRequest
{
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyPriceIndexImportRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public DateTime CreatedAt { get; init; }
}
