using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed record ProcurementControlEventWriteRequest
{
    public string EventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public ProcurementControlEventResult Result { get; set; }
    public string? RuleCode { get; set; }
    public Guid? RuleId { get; set; }
    public string? RuleVersion { get; set; }
    public List<string> DecisionKeys { get; set; } = new();
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public object? InputValues { get; set; }
    public object? ResultValues { get; set; }
    public object? Before { get; set; }
    public object? After { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? CausationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementControlEventEvidenceReference
{
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Reference { get; set; }
    public string? Label { get; set; }
    public string? RequirementKey { get; set; }
}

public sealed class ProcurementControlEventSearchRequest
{
    public string? EventType { get; set; }
    public string? Action { get; set; }
    public ProcurementControlEventResult? Result { get; set; }
    public string? RuleCode { get; set; }
    public string? SourceType { get; set; }
    public string? SourceReference { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class ProcurementControlEventPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementControlEventDto> Items { get; set; } = new();
}

public sealed class ProcurementControlEventSummaryDto
{
    public int TotalCount { get; set; }
    public int AllowedCount { get; set; }
    public int DeniedOrRejectedCount { get; set; }
    public int FailedCount { get; set; }
    public int EvidenceLinkedCount { get; set; }
    public DateTime? LatestOccurredAtUtc { get; set; }
    public Dictionary<string, int> ByEventType { get; set; } = new();
}

public sealed class ProcurementControlEventDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public AuditOperationKind Operation { get; set; }
    public ProcurementControlEventResult Result { get; set; }
    public string? RuleCode { get; set; }
    public Guid? RuleId { get; set; }
    public string? RuleVersion { get; set; }
    public List<string> DecisionKeys { get; set; } = new();
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public List<string> ActorRoles { get; set; } = new();
    public string? Reason { get; set; }
    public string? InputValuesJson { get; set; }
    public string? ResultValuesJson { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? CausationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public bool IntegrityValid { get; set; }
    public List<ProcurementControlEventEvidenceDto> Evidence { get; set; } = new();
}

public sealed class ProcurementControlEventEvidenceDto
{
    public Guid Id { get; set; }
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? ReferenceId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? RequirementKey { get; set; }
    public string? FileName { get; set; }
    public string? Sha256 { get; set; }
    public string? VerificationStatus { get; set; }
    public bool ReferenceAvailable { get; set; }
}

public sealed class ProcurementControlEventIntegrityDto
{
    public int CheckedCount { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }
    public bool IsValid { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
    public List<ProcurementControlEventIntegrityIssueDto> Issues { get; set; } = new();
}

public sealed class ProcurementControlEventIntegrityIssueDto
{
    public Guid EventId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string ExpectedHash { get; set; } = string.Empty;
    public string ActualHash { get; set; } = string.Empty;
}
