using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementGhanepsExchangeEvents")]
public sealed class ProcurementGhanepsExchangeEvent : TenantEntity
{
    public ProcurementGhanepsSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    [Required, StringLength(200)] public string SourceReference { get; set; } = string.Empty;
    [Required, StringLength(50)] public string SourceVariant { get; set; } = string.Empty;
    public DateTime SourceOccurredAtUtc { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SourceSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string SourceIntegrityHash { get; set; } = string.Empty;

    public ProcurementGhanepsEventFamily EventFamily { get; set; }
    public ProcurementGhanepsExchangeDirection Direction { get; set; }
    [Required, StringLength(100)] public string MappingKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ExternalEventCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string EventReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ReferenceField { get; set; } = string.Empty;
    [Required, StringLength(100)] public string PayloadContentType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AcknowledgementContentType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AcknowledgementPermissionCode { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ReconciliationPermissionCode { get; set; } = string.Empty;

    public Guid ConfigurationProfileId { get; set; }
    [Required, StringLength(50)] public string ConfigurationProfileCode { get; set; } = string.Empty;
    public int ConfigurationProfileVersion { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public int ConfigurationDecisionSchemaVersion { get; set; }
    [Required, StringLength(100)] public string ExchangeProfileCode { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string ConfigurationValueJson { get; set; } = "{}";
    [Required, StringLength(64)] public string ConfigurationValueHash { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string MappingSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string MappingIntegrityHash { get; set; } = string.Empty;
    public DateTime ConfigurationEffectiveFromUtc { get; set; }
    public DateTime? ConfigurationEffectiveToUtc { get; set; }
    [Required, StringLength(100)] public string Frequency { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Owner { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string AcknowledgementRule { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string ReconciliationRule { get; set; } = string.Empty;
    public bool AcknowledgementRequired { get; set; }
    public bool ReconciliationRequired { get; set; }
    public int MaximumRetryAttempts { get; set; }

    public ProcurementGhanepsExchangeStatus Status { get; set; }
    [Required, StringLength(64)] public string RequestFingerprint { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    [Required, StringLength(300)] public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedAtUtc { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public ProcurementConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<ProcurementGhanepsExchangePayload> Payloads { get; set; } =
        new List<ProcurementGhanepsExchangePayload>();
    public ICollection<ProcurementGhanepsExchangeAttempt> Attempts { get; set; } =
        new List<ProcurementGhanepsExchangeAttempt>();
    public ICollection<ProcurementGhanepsExchangeAcknowledgement> Acknowledgements { get; set; } =
        new List<ProcurementGhanepsExchangeAcknowledgement>();
    public ICollection<ProcurementGhanepsExchangeReconciliation> Reconciliations { get; set; } =
        new List<ProcurementGhanepsExchangeReconciliation>();
}

[Table("ProcurementGhanepsExchangePayloads")]
public sealed class ProcurementGhanepsExchangePayload : TenantEntity
{
    public Guid ExchangeEventId { get; set; }
    public int Version { get; set; }
    public ProcurementGhanepsExchangeDirection Direction { get; set; }
    [Required, StringLength(200)] public string TemplateReference { get; set; } = string.Empty;
    [Required, StringLength(200)] public string SchemaReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ExternalPayloadVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ContentType { get; set; } = "application/json";
    [StringLength(260)] public string? FileName { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string PayloadContent { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PayloadChecksumSha256 { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public Guid RecordedByUserId { get; set; }
    [Required, StringLength(300)] public string RecordedByName { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementGhanepsExchangeEvent ExchangeEvent { get; set; } = null!;
    public ICollection<ProcurementGhanepsExchangeAttempt> Attempts { get; set; } =
        new List<ProcurementGhanepsExchangeAttempt>();
}

[Table("ProcurementGhanepsExchangeAttempts")]
public sealed class ProcurementGhanepsExchangeAttempt : TenantEntity
{
    public Guid ExchangeEventId { get; set; }
    public Guid PayloadId { get; set; }
    public int AttemptNumber { get; set; }
    public bool IsRetry { get; set; }
    public Guid? SupersedesAttemptId { get; set; }
    public ProcurementGhanepsAttemptOutcome Outcome { get; set; }
    [StringLength(200)] public string? TransportReference { get; set; }
    [StringLength(100)] public string? FailureCode { get; set; }
    [StringLength(2000)] public string? FailureMessage { get; set; }
    [Required, StringLength(64)] public string PayloadChecksumSha256 { get; set; } = string.Empty;
    [Required, StringLength(64)] public string RequestFingerprint { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public Guid AttemptedByUserId { get; set; }
    [Required, StringLength(300)] public string AttemptedByName { get; set; } = string.Empty;
    public DateTime AttemptedAtUtc { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementGhanepsExchangeEvent ExchangeEvent { get; set; } = null!;
    public ProcurementGhanepsExchangePayload Payload { get; set; } = null!;
    public ProcurementGhanepsExchangeAttempt? SupersedesAttempt { get; set; }
}

[Table("ProcurementGhanepsExchangeAcknowledgements")]
public sealed class ProcurementGhanepsExchangeAcknowledgement : TenantEntity
{
    public Guid ExchangeEventId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid PayloadId { get; set; }
    public int Sequence { get; set; }
    public ProcurementGhanepsAcknowledgementOutcome Outcome { get; set; }
    [Required, StringLength(200)] public string AcknowledgementReference { get; set; } = string.Empty;
    [StringLength(100)] public string? ExternalStatusCode { get; set; }
    [Required, StringLength(100)] public string ContentType { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string AcknowledgementContent { get; set; } = string.Empty;
    [Required, StringLength(64)] public string AcknowledgementChecksumSha256 { get; set; } = string.Empty;
    [Required, StringLength(64)] public string RequestFingerprint { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public Guid AcknowledgedByUserId { get; set; }
    [Required, StringLength(300)] public string AcknowledgedByName { get; set; } = string.Empty;
    public DateTime AcknowledgedAtUtc { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementGhanepsExchangeEvent ExchangeEvent { get; set; } = null!;
    public ProcurementGhanepsExchangeAttempt Attempt { get; set; } = null!;
    public ProcurementGhanepsExchangePayload Payload { get; set; } = null!;
}

[Table("ProcurementGhanepsExchangeReconciliations")]
public sealed class ProcurementGhanepsExchangeReconciliation : TenantEntity
{
    public Guid ExchangeEventId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid PayloadId { get; set; }
    public int Sequence { get; set; }
    public ProcurementGhanepsReconciliationOutcome Outcome { get; set; }
    [Required, StringLength(200)] public string ExpectedReference { get; set; } = string.Empty;
    [StringLength(200)] public string? ActualReference { get; set; }
    [Required, StringLength(64)] public string ExpectedChecksumSha256 { get; set; } = string.Empty;
    [StringLength(64)] public string? ActualChecksumSha256 { get; set; }
    [Required, StringLength(64)] public string RequestFingerprint { get; set; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public Guid ReconciledByUserId { get; set; }
    [Required, StringLength(300)] public string ReconciledByName { get; set; } = string.Empty;
    public DateTime ReconciledAtUtc { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementGhanepsExchangeEvent ExchangeEvent { get; set; } = null!;
    public ProcurementGhanepsExchangeAttempt Attempt { get; set; } = null!;
    public ProcurementGhanepsExchangePayload Payload { get; set; } = null!;
}
