namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// One producer-owned economic intent. Book applicability is deliberately absent: Finance resolves and
/// freezes that authority after accepting the stable source identity and balanced economic lines.
/// </summary>
public sealed class ProducerAccountingIntentDto
{
    public Guid? AccountingEventId { get; set; }
    public string EventKind { get; set; } = "Original";
    public Guid? SupersedesAccountingEventId { get; set; }
    public Guid? CorrectsAccountingEventId { get; set; }
    public Guid? ReversesAccountingEventId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ParticipantIdentity { get; set; } = string.Empty;
    public ProducerOwnerEffectIdentityDto ExpectedOwnerEffect { get; set; } = new();
    public ProducerFinancePostingRequestDto PostingRequest { get; set; } = new();
}

/// <summary>Immutable owner mutation identity prepared alongside the neutral accounting economics.</summary>
public class ProducerOwnerEffectIdentityDto
{
    public string ParticipantCode { get; set; } = string.Empty;
    public string OwnerEntityType { get; set; } = string.Empty;
    public Guid OwnerEntityId { get; set; }
    public string OwnerAction { get; set; } = string.Empty;
    public string EffectFingerprint { get; set; } = string.Empty;
}

/// <summary>Deterministic acknowledgement that the prepared owner mutation was staged in Finance's ambient transaction.</summary>
public sealed class ProducerOwnerEffectReceiptDto : ProducerOwnerEffectIdentityDto
{
    public Guid TenantId { get; set; }
}

/// <summary>Finance posting economics with no accounting-book selector.</summary>
public sealed class ProducerFinancePostingRequestDto : FinancePostingCommandDto;

public sealed class DecideProducerAccountingIntentDto
{
    public string Reason { get; set; } = string.Empty;
}
