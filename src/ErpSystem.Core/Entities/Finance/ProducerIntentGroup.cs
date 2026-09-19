using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Finance-owned zero-or-all authority over an immutable ordered set of producer AccountingEvents.</summary>
public sealed class ProducerIntentGroup : TenantEntity
{
    [MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(20)] public string GroupKind { get; set; } = AccountingEventKinds.Original;
    public int Version { get; set; } = 1;
    public Guid RootProducerIntentGroupId { get; set; }
    public Guid? SupersedesProducerIntentGroupId { get; set; }
    public Guid? CorrectsProducerIntentGroupId { get; set; }
    public Guid? ReversesProducerIntentGroupId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = ProducerIntentGroupStatuses.PendingApproval;
    public int MemberCount { get; set; }
    [MaxLength(100)] public string ParticipantCode { get; set; } = string.Empty;
    [MaxLength(100)] public string OwnerEntityType { get; set; } = string.Empty;
    public Guid OwnerEntityId { get; set; }
    [MaxLength(60)] public string OwnerAction { get; set; } = string.Empty;
    [MaxLength(64)] public string ExpectedOwnerEffectFingerprint { get; set; } = string.Empty;
    public string RequestSnapshotJson { get; set; } = string.Empty;
    [MaxLength(64)] public string RequestSnapshotHash { get; set; } = string.Empty;
    [MaxLength(64)] public string GroupFingerprint { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [MaxLength(500)] public string? DecisionReason { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [MaxLength(1000)] public string? FailureMessage { get; set; }

    public ProducerIntentGroup? RootProducerIntentGroup { get; set; }
    public ProducerIntentGroup? SupersedesProducerIntentGroup { get; set; }
    public ProducerIntentGroup? CorrectsProducerIntentGroup { get; set; }
    public ProducerIntentGroup? ReversesProducerIntentGroup { get; set; }
    public ICollection<ProducerIntentGroupMember> Members { get; set; } = new List<ProducerIntentGroupMember>();
    public ICollection<ProducerIntentGroupAttempt> Attempts { get; set; } = new List<ProducerIntentGroupAttempt>();
    public ProducerIntentGroupReceipt? Receipt { get; set; }
}

/// <summary>Immutable ordered membership binding one normal C6 event to its canonical group fingerprint.</summary>
public sealed class ProducerIntentGroupMember : TenantEntity
{
    public Guid ProducerIntentGroupId { get; set; }
    public Guid AccountingEventId { get; set; }
    public int MemberOrder { get; set; }
    [MaxLength(64)] public string MemberFingerprint { get; set; } = string.Empty;
    public ProducerIntentGroup ProducerIntentGroup { get; set; } = null!;
    public AccountingEvent AccountingEvent { get; set; } = null!;
}

/// <summary>One immutable owner-effect acknowledgement bound to the complete approved group.</summary>
public sealed class ProducerIntentGroupReceipt : TenantEntity
{
    public Guid ProducerIntentGroupId { get; set; }
    [MaxLength(100)] public string ParticipantCode { get; set; } = string.Empty;
    [MaxLength(100)] public string OwnerEntityType { get; set; } = string.Empty;
    public Guid OwnerEntityId { get; set; }
    [MaxLength(60)] public string OwnerAction { get; set; } = string.Empty;
    [MaxLength(64)] public string EffectFingerprint { get; set; } = string.Empty;
    [MaxLength(64)] public string GroupFingerprint { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
    public Guid RecordedByUserId { get; set; }
    public ProducerIntentGroup ProducerIntentGroup { get; set; } = null!;
}

/// <summary>Append-only durable outcome for one complete-group execution attempt.</summary>
public sealed class ProducerIntentGroupAttempt : TenantEntity
{
    public Guid ProducerIntentGroupId { get; set; }
    public int AttemptNumber { get; set; }
    [MaxLength(64)] public string GroupFingerprint { get; set; } = string.Empty;
    [MaxLength(20)] public string Status { get; set; } = AccountingEventStatuses.Pending;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? FailedMemberOrder { get; set; }
    public Guid? FailedAccountingEventId { get; set; }
    [MaxLength(1000)] public string? FailureMessage { get; set; }
    public ProducerIntentGroup ProducerIntentGroup { get; set; } = null!;
}

public static class ProducerIntentGroupStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Posted = "Posted";
    public const string Failed = "Failed";
}
