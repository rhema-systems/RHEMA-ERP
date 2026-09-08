namespace ErpSystem.Core.DTOs.Finance;

/// <summary>A complete ordered producer operation; members contain economics and source identity, never books.</summary>
public sealed class ProducerIntentGroupRequestDto
{
    public Guid? ProducerIntentGroupId { get; set; }
    public string GroupKind { get; set; } = "Original";
    public Guid? SupersedesProducerIntentGroupId { get; set; }
    public Guid? CorrectsProducerIntentGroupId { get; set; }
    public Guid? ReversesProducerIntentGroupId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ParticipantIdentity { get; set; } = string.Empty;
    public ProducerOwnerEffectIdentityDto ExpectedOwnerEffect { get; set; } = new();
    public IReadOnlyList<ProducerAccountingIntentDto> Members { get; set; } = [];
}

public sealed class DecideProducerIntentGroupRequestDto
{
    public ProducerIntentGroupRequestDto Group { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public sealed class ProducerIntentGroupDto
{
    public Guid Id { get; set; }
    public Guid RootProducerIntentGroupId { get; set; }
    public int Version { get; set; }
    public string GroupKind { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ParticipantIdentity { get; set; } = string.Empty;
    public ProducerOwnerEffectIdentityDto ExpectedOwnerEffect { get; set; } = new();
    public string GroupFingerprint { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? FailureMessage { get; set; }
    public ProducerOwnerEffectReceiptDto? OwnerEffectReceipt { get; set; }
    public IReadOnlyList<ProducerIntentGroupMemberDto> Members { get; set; } = [];
    public IReadOnlyList<ProducerIntentGroupAttemptDto> Attempts { get; set; } = [];
}

public sealed class ProducerIntentGroupMemberDto
{
    public int MemberOrder { get; set; }
    public string MemberFingerprint { get; set; } = string.Empty;
    public AccountingEventDto AccountingEvent { get; set; } = new();
}

public sealed class ProducerIntentGroupAttemptDto
{
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? FailedMemberOrder { get; set; }
    public Guid? FailedAccountingEventId { get; set; }
    public string? FailureMessage { get; set; }
}
