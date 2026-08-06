using ErpSystem.Core.Entities;

namespace ErpSystem.Core.DTOs.Compliance;

public sealed record AuditRecordDescriptor(
    string StoreKey,
    string StoreName,
    Guid RecordId,
    Guid TenantId,
    DateTime OccurredAtUtc,
    string Reference,
    object Snapshot,
    string? IntegrityHash = null);

public sealed class AuditLifecycleActionDto
{
    public Guid Id { get; set; }
    public int SequenceNumber { get; set; }
    public AuditLifecycleActionType Action { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? ArchiveReference { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime RetainUntilUtc { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public bool IntegrityValid { get; set; }
}

public sealed class AuditRecordGovernanceDto
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime SourceOccurredAtUtc { get; set; }
    public DateTime RetainUntilUtc { get; set; }
    public int RetentionDays { get; set; }
    public bool IsImmutable { get; set; } = true;
    public bool IsLegalHold { get; set; }
    public bool IsArchived { get; set; }
    public string? ArchiveReference { get; set; }
    public IReadOnlyList<AuditLifecycleActionDto> Actions { get; set; } = [];
}

public sealed class AuditLifecycleCommandDto
{
    public string Reason { get; set; } = string.Empty;
    public string RequestKey { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public sealed record AuditEventCoverageDefinitionDto(
    string Module,
    AuditOperationKind Operation,
    string Control,
    IReadOnlyList<string> EmittedActions);

public sealed class AuditEventCoverageReportDto
{
    public DateTime VerifiedAtUtc { get; set; }
    public IReadOnlyList<AuditOperationKind> RequiredOperations { get; set; } = [];
    public IReadOnlyList<AuditEventCoverageDefinitionDto> Definitions { get; set; } = [];
    public IReadOnlyList<AuditOperationKind> MissingOperations { get; set; } = [];
    public bool IsComplete => MissingOperations.Count == 0;
}
