using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class SaveQuantitySurveySubcontractChargeRequest
{
    public Guid? Id { get; init; }
    public Guid ClientRequestId { get; init; }
    [Required] public string ChargeType { get; init; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Reason { get; init; } = string.Empty;
    public DateTime NoticeDate { get; init; }
    public DateTime ResponseDueDate { get; init; }
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal ProposedAmount { get; init; }
    public string? RowVersion { get; init; }
}

public sealed class RespondQuantitySurveySubcontractChargeRequest : QuantitySurveySubcontractActionRequest
{
    [Required] public string ResponseStatus { get; init; } = string.Empty;
}

public sealed class DecideQuantitySurveySubcontractChargeRequest : QuantitySurveySubcontractActionRequest
{
    [Range(typeof(decimal), "0", "999999999999")] public decimal ApprovedAmount { get; init; }
}

public sealed class QuantitySurveySubcontractChargeEvidenceDto
{
    public Guid Id { get; init; }
    public Guid ChargeNoticeId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
}

public sealed class QuantitySurveySubcontractChargeDto
{
    public Guid Id { get; init; }
    public Guid SubcontractId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid SubcontractorBusinessPartnerId { get; init; }
    public Guid? AppliedValuationId { get; init; }
    public string? AppliedValuationNumber { get; init; }
    public string NoticeNumber { get; init; } = string.Empty;
    public string ChargeType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime NoticeDate { get; init; }
    public DateTime ResponseDueDate { get; init; }
    public decimal ProposedAmount { get; init; }
    public decimal? ApprovedAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string ResponseStatus { get; init; } = string.Empty;
    public string? ResponseNote { get; init; }
    public DateTime? RespondedAt { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime? IssuedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? AppliedAt { get; init; }
    public string CommunicationStatus { get; init; } = string.Empty;
    public DateTime? CommunicationRequestedAt { get; init; }
    public int CommunicationRequestCount { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveySubcontractChargeEvidenceDto> Evidence { get; init; } = [];
}

public sealed class QuantitySurveySubcontractChargeRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string AfterJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
