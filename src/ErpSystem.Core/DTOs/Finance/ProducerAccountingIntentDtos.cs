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
    public ProducerFinancePostingRequestDto PostingRequest { get; set; } = new();
}

/// <summary>Finance posting economics with no accounting-book selector.</summary>
public sealed class ProducerFinancePostingRequestDto : FinancePostingCommandDto;

public sealed class DecideProducerAccountingIntentDto
{
    public string Reason { get; set; } = string.Empty;
}
