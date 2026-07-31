using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementGhanepsConfiguredMappingDto
{
    [Required, StringLength(100)] public string MappingKey { get; set; } = string.Empty;
    public ProcurementGhanepsEventFamily EventFamily { get; set; } =
        (ProcurementGhanepsEventFamily)(-1);
    public ProcurementGhanepsExchangeDirection Direction { get; set; } =
        (ProcurementGhanepsExchangeDirection)(-1);
    [MinLength(1)] public List<ProcurementGhanepsSourceType> SourceTypes { get; set; } = new();
    public List<string> SourceVariants { get; set; } = new();
    [Required, StringLength(100)] public string ExternalEventCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string TemplateReference { get; set; } = string.Empty;
    [Required, StringLength(200)] public string SchemaReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string PayloadVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ReferenceField { get; set; } = string.Empty;
    [Required, StringLength(100)] public string PayloadContentType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AcknowledgementContentType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AcknowledgementPermissionCode { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ReconciliationPermissionCode { get; set; } = string.Empty;
    public bool AcknowledgementRequired { get; set; } = true;
    public bool ReconciliationRequired { get; set; } = true;
    [Range(0, 100)] public int MaximumRetryAttempts { get; set; } = 3;
}

public abstract class ProcurementGhanepsRouteBoundMutationRequest
{
    [JsonIgnore] public ProcurementGhanepsSourceType? RouteSourceType { get; set; }
    [JsonIgnore] public Guid? RouteSourceId { get; set; }
}

public abstract class ProcurementGhanepsPayloadRequest :
    ProcurementGhanepsRouteBoundMutationRequest
{
    // Mutation requests are intentionally validation-neutral transport contracts.
    // Core performs the authoritative validation so every deserializable denial is
    // tenant-audited with a stable GHANEPS error code instead of being short-circuited
    // by ApiController model validation.
    public ProcurementGhanepsSourceType? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public ProcurementGhanepsEventFamily? EventFamily { get; set; }
    public string? MappingKey { get; set; }
    public string? EventReference { get; set; }
    public string? PayloadContent { get; set; }
    public string? FileName { get; set; }
    public string? ExpectedPayloadChecksumSha256 { get; set; }
    public string? EvidenceReference { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class PrepareProcurementGhanepsExportRequest : ProcurementGhanepsPayloadRequest
{
}

public sealed class RecordProcurementGhanepsImportRequest : ProcurementGhanepsPayloadRequest
{
    public string? TransportReference { get; set; }
}

public sealed class RecordProcurementGhanepsAttemptRequest :
    ProcurementGhanepsRouteBoundMutationRequest
{
    public Guid? PayloadId { get; set; }
    public ProcurementGhanepsAttemptOutcome? Outcome { get; set; }
    public string? TransportReference { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? EvidenceReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ExpectedRowVersion { get; set; }
}

public sealed class RetryProcurementGhanepsExchangeRequest :
    ProcurementGhanepsRouteBoundMutationRequest
{
    public Guid? PayloadId { get; set; }
    public ProcurementGhanepsAttemptOutcome? Outcome { get; set; }
    public string? TransportReference { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? ReplacementPayloadContent { get; set; }
    public string? FileName { get; set; }
    public string? ExpectedPayloadChecksumSha256 { get; set; }
    public string? EvidenceReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ExpectedRowVersion { get; set; }
}

public sealed class RecordProcurementGhanepsAcknowledgementRequest :
    ProcurementGhanepsRouteBoundMutationRequest
{
    public ProcurementGhanepsAcknowledgementOutcome? Outcome { get; set; }
    public string? AcknowledgementReference { get; set; }
    public string? ExternalStatusCode { get; set; }
    public string? AcknowledgementContent { get; set; }
    public string? ExpectedAcknowledgementChecksumSha256 { get; set; }
    public string? EvidenceReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ExpectedRowVersion { get; set; }
}

public sealed class ReconcileProcurementGhanepsExchangeRequest :
    ProcurementGhanepsRouteBoundMutationRequest
{
    public bool ResolveExistingMismatch { get; set; }
    public string? ActualReference { get; set; }
    public string? ActualChecksumSha256 { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ExpectedRowVersion { get; set; }
}

public sealed class ProcurementGhanepsExchangeMappingOptionDto
{
    public string MappingKey { get; init; } = string.Empty;
    public ProcurementGhanepsEventFamily EventFamily { get; init; }
    public ProcurementGhanepsExchangeDirection Direction { get; init; }
    public string ExternalEventCode { get; init; } = string.Empty;
    public string TemplateReference { get; init; } = string.Empty;
    public string SchemaReference { get; init; } = string.Empty;
    public string PayloadVersion { get; init; } = string.Empty;
    public string ReferenceField { get; init; } = string.Empty;
    public string PayloadContentType { get; init; } = string.Empty;
    public string AcknowledgementContentType { get; init; } = string.Empty;
    public string AcknowledgementPermissionCode { get; init; } = string.Empty;
    public string ReconciliationPermissionCode { get; init; } = string.Empty;
    public bool AcknowledgementRequired { get; init; }
    public bool ReconciliationRequired { get; init; }
    public int MaximumRetryAttempts { get; init; }
}

public sealed class ProcurementGhanepsExchangeOptionsDto
{
    public ProcurementGhanepsSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string SourceVariant { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public string ConfigurationProfileCode { get; init; } = string.Empty;
    public int ConfigurationProfileVersion { get; init; }
    public Guid ConfigurationDecisionId { get; init; }
    public string ExchangeProfileCode { get; init; } = string.Empty;
    public string ConfigurationValueHash { get; init; } = string.Empty;
    public DateTime EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }
    public string Frequency { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string AcknowledgementRule { get; init; } = string.Empty;
    public string ReconciliationRule { get; init; } = string.Empty;
    public IReadOnlyList<ProcurementGhanepsExchangeMappingOptionDto> Mappings { get; init; } =
        Array.Empty<ProcurementGhanepsExchangeMappingOptionDto>();
    public IReadOnlyList<string> AllowedActions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BlockedReasons { get; init; } = Array.Empty<string>();
}

public sealed class ProcurementGhanepsExchangePayloadDto
{
    public Guid Id { get; init; }
    public int Version { get; init; }
    public ProcurementGhanepsExchangeDirection Direction { get; init; }
    public string TemplateReference { get; init; } = string.Empty;
    public string SchemaReference { get; init; } = string.Empty;
    public string ExternalPayloadVersion { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string PayloadContent { get; init; } = string.Empty;
    public string PayloadChecksumSha256 { get; init; } = string.Empty;
    public DateTime RecordedAtUtc { get; init; }
    public Guid RecordedByUserId { get; init; }
    public string RecordedByName { get; init; } = string.Empty;
    public string? EvidenceReference { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementGhanepsExchangeAttemptDto
{
    public Guid Id { get; init; }
    public Guid PayloadId { get; init; }
    public int AttemptNumber { get; init; }
    public bool IsRetry { get; init; }
    public Guid? SupersedesAttemptId { get; init; }
    public ProcurementGhanepsAttemptOutcome Outcome { get; init; }
    public string? TransportReference { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
    public string RequestFingerprint { get; init; } = string.Empty;
    public DateTime AttemptedAtUtc { get; init; }
    public Guid AttemptedByUserId { get; init; }
    public string AttemptedByName { get; init; } = string.Empty;
    public string? EvidenceReference { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementGhanepsExchangeAcknowledgementDto
{
    public Guid Id { get; init; }
    public Guid AttemptId { get; init; }
    public Guid PayloadId { get; init; }
    public int Sequence { get; init; }
    public ProcurementGhanepsAcknowledgementOutcome Outcome { get; init; }
    public string AcknowledgementReference { get; init; } = string.Empty;
    public string? ExternalStatusCode { get; init; }
    public string ContentType { get; init; } = string.Empty;
    public string AcknowledgementContent { get; init; } = string.Empty;
    public string AcknowledgementChecksumSha256 { get; init; } = string.Empty;
    public string RequestFingerprint { get; init; } = string.Empty;
    public DateTime AcknowledgedAtUtc { get; init; }
    public Guid AcknowledgedByUserId { get; init; }
    public string AcknowledgedByName { get; init; } = string.Empty;
    public string EvidenceReference { get; init; } = string.Empty;
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementGhanepsExchangeReconciliationDto
{
    public Guid Id { get; init; }
    public Guid AttemptId { get; init; }
    public Guid PayloadId { get; init; }
    public int Sequence { get; init; }
    public ProcurementGhanepsReconciliationOutcome Outcome { get; init; }
    public string ExpectedReference { get; init; } = string.Empty;
    public string? ActualReference { get; init; }
    public string ExpectedChecksumSha256 { get; init; } = string.Empty;
    public string? ActualChecksumSha256 { get; init; }
    public string RequestFingerprint { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime ReconciledAtUtc { get; init; }
    public Guid ReconciledByUserId { get; init; }
    public string ReconciledByName { get; init; } = string.Empty;
    public string EvidenceReference { get; init; } = string.Empty;
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementGhanepsExchangeEventDto
{
    public Guid Id { get; init; }
    public ProcurementGhanepsSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string SourceVariant { get; init; } = string.Empty;
    public DateTime SourceOccurredAtUtc { get; init; }
    public string SourceIntegrityHash { get; init; } = string.Empty;
    public ProcurementGhanepsEventFamily EventFamily { get; init; }
    public ProcurementGhanepsExchangeDirection Direction { get; init; }
    public string MappingKey { get; init; } = string.Empty;
    public string ExternalEventCode { get; init; } = string.Empty;
    public string EventReference { get; init; } = string.Empty;
    public string ReferenceField { get; init; } = string.Empty;
    public string PayloadContentType { get; init; } = string.Empty;
    public string AcknowledgementContentType { get; init; } = string.Empty;
    public string AcknowledgementPermissionCode { get; init; } = string.Empty;
    public string ReconciliationPermissionCode { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public string ConfigurationProfileCode { get; init; } = string.Empty;
    public int ConfigurationProfileVersion { get; init; }
    public Guid ConfigurationDecisionId { get; init; }
    public string ExchangeProfileCode { get; init; } = string.Empty;
    public string ConfigurationValueHash { get; init; } = string.Empty;
    public string MappingIntegrityHash { get; init; } = string.Empty;
    public DateTime ConfigurationEffectiveFromUtc { get; init; }
    public DateTime? ConfigurationEffectiveToUtc { get; init; }
    public string Frequency { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string AcknowledgementRule { get; init; } = string.Empty;
    public string ReconciliationRule { get; init; } = string.Empty;
    public bool AcknowledgementRequired { get; init; }
    public bool ReconciliationRequired { get; init; }
    public int MaximumRetryAttempts { get; init; }
    public ProcurementGhanepsExchangeStatus Status { get; init; }
    public string RequestFingerprint { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public Guid PreparedByUserId { get; init; }
    public string PreparedByName { get; init; } = string.Empty;
    public DateTime PreparedAtUtc { get; init; }
    public string? EvidenceReference { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedActions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BlockedReasons { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcurementGhanepsExchangePayloadDto> Payloads { get; init; } =
        Array.Empty<ProcurementGhanepsExchangePayloadDto>();
    public IReadOnlyList<ProcurementGhanepsExchangeAttemptDto> Attempts { get; init; } =
        Array.Empty<ProcurementGhanepsExchangeAttemptDto>();
    public IReadOnlyList<ProcurementGhanepsExchangeAcknowledgementDto> Acknowledgements { get; init; } =
        Array.Empty<ProcurementGhanepsExchangeAcknowledgementDto>();
    public IReadOnlyList<ProcurementGhanepsExchangeReconciliationDto> Reconciliations { get; init; } =
        Array.Empty<ProcurementGhanepsExchangeReconciliationDto>();
    public IReadOnlyList<ProcurementGhanepsExchangeHistoryItemDto> History { get; init; } =
        Array.Empty<ProcurementGhanepsExchangeHistoryItemDto>();
}

public sealed class ProcurementGhanepsExchangeHistoryItemDto
{
    public Guid ExchangeEventId { get; init; }
    public ProcurementGhanepsEventFamily EventFamily { get; init; }
    public string EventReference { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public Guid RecordId { get; init; }
    public int Sequence { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public string? EvidenceReference { get; init; }
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementGhanepsExchangeOverviewDto
{
    public ProcurementGhanepsSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string SourceVariant { get; init; } = string.Empty;
    public IReadOnlyList<ProcurementGhanepsExchangeEventDto> Events { get; init; } =
        Array.Empty<ProcurementGhanepsExchangeEventDto>();
}

public sealed class ProcurementGhanepsComplianceDto
{
    public ProcurementGhanepsSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public Guid ConfigurationDecisionId { get; init; }
    public string ConfigurationValueHash { get; init; } = string.Empty;
    public bool HasApplicableMapping { get; init; }
    public bool IsCompliant { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<ProcurementGhanepsComplianceMappingDto> Mappings { get; init; } =
        Array.Empty<ProcurementGhanepsComplianceMappingDto>();
}

public sealed class ProcurementGhanepsComplianceMappingDto
{
    public string MappingKey { get; init; } = string.Empty;
    public bool AcknowledgementRequired { get; init; }
    public bool ReconciliationRequired { get; init; }
    public Guid? ExchangeEventId { get; init; }
    public string? EventReference { get; init; }
    public ProcurementGhanepsExchangeStatus? Status { get; init; }
    public bool SuccessfulTransfer { get; init; }
    public bool AcceptedAcknowledgement { get; init; }
    public bool CompletedReconciliation { get; init; }
    public bool EvidenceAvailable { get; init; }
    public bool IsCompliant { get; init; }
    public string Message { get; init; } = string.Empty;
}
