using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyEscalationDisputeLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class CreateQuantitySurveyEscalationDisputeRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid CalculationRunId { get; init; }
    [Required, StringLength(200), MinLength(5)] public string Subject { get; init; } = string.Empty;
    [Required, StringLength(4000), MinLength(10)] public string DisputeReason { get; init; } = string.Empty;
}

public sealed class RespondQuantitySurveyEscalationDisputeRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(4000), MinLength(10)] public string Response { get; init; } = string.Empty;
}

public sealed class ResolveQuantitySurveyEscalationDisputeRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    public QuantitySurveyEscalationDisputeOutcome Outcome { get; init; }
    [Required, StringLength(4000), MinLength(10)] public string ResolutionNotes { get; init; } = string.Empty;
}

public sealed class AddQuantitySurveyEscalationDisputeAttachmentRequest
{
    public Guid ClientRequestId { get; init; }
    public QuantitySurveyEscalationDisputeAttachmentType AttachmentType { get; init; }
    [Required, StringLength(200), MinLength(3)] public string Title { get; init; } = string.Empty;
}

public sealed class QuantitySurveyEscalationDisputeListRequest
{
    public Guid? ProjectId { get; init; }
    public Guid? CalculationRunId { get; init; }
    public string? Status { get; init; }
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 50;
}

public sealed class QuantitySurveyEscalationDisputeAttachmentDto
{
    public Guid Id { get; init; }
    public QuantitySurveyEscalationDisputeAttachmentType AttachmentType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public Guid UploadedById { get; init; }
    public string UploadedByName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class QuantitySurveyEscalationDisputeDto
{
    public Guid Id { get; init; }
    public Guid CalculationRunId { get; init; }
    public string CalculationRunReference { get; init; } = string.Empty;
    public string CalculationSnapshotHash { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public Guid ContractorBusinessPartnerId { get; init; }
    public string ContractorName { get; init; } = string.Empty;
    public string DisputeReference { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string DisputeReason { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid OpenedById { get; init; }
    public DateTime OpenedAt { get; init; }
    public string? ContractorResponse { get; init; }
    public Guid? ContractorRespondedById { get; init; }
    public DateTime? ContractorRespondedAt { get; init; }
    public QuantitySurveyEscalationDisputeOutcome? Outcome { get; init; }
    public string? ResolutionNotes { get; init; }
    public Guid? ResolvedById { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyEscalationDisputeAttachmentDto> Attachments { get; init; } = [];
}

public sealed class QuantitySurveyEscalationDisputePageDto
{
    public IReadOnlyList<QuantitySurveyEscalationDisputeDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class QuantitySurveyEscalationDisputeRevisionDto
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
