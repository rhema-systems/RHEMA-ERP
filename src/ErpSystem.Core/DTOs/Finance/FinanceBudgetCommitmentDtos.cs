namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceBudgetCellQueryDto
{
    public DateTime BudgetDate { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public Guid? SegmentValueId { get; set; }
}

public class FinanceBudgetCellDto
{
    public Guid BudgetScenarioId { get; set; }
    public string BudgetScenarioName { get; set; } = string.Empty;
    public int BudgetScenarioVersion { get; set; }
    public Guid BudgetReturnId { get; set; }
    public Guid BudgetEntryId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid FiscalYearId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string FiscalPeriodCode { get; set; } = string.Empty;
    public Guid? SegmentValueId { get; set; }
    public string? SegmentValue { get; set; }
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public decimal ApprovedAmount { get; set; }
    public decimal PostedActualAmount { get; set; }
    public decimal ReservedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public DateTime PositionAsOfUtc { get; set; }
}

public sealed class FinanceBudgetPositionDto : FinanceBudgetCellDto;

public sealed class FinanceBudgetCommitmentLineDto
{
    public string SourceLineId { get; set; } = string.Empty;
    public Guid BudgetEntryId { get; set; }
    public Guid AccountId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid? SegmentValueId { get; set; }
    public decimal TransactionAmount { get; set; }
    public string TransactionCurrencyCode { get; set; } = string.Empty;
    public Guid? ExchangeRateId { get; set; }
}

public sealed class FinanceBudgetCommitmentRequestDto
{
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentReference { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public DateTime BudgetDate { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public IReadOnlyList<FinanceBudgetCommitmentLineDto> Lines { get; set; } = Array.Empty<FinanceBudgetCommitmentLineDto>();
}

public sealed class FinanceBudgetCommitmentEvaluationDto
{
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public string EvaluationHash { get; set; } = string.Empty;
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public decimal TotalRequestedFunctionalAmount { get; set; }
    public decimal TotalShortfallAmount { get; set; }
    public IReadOnlyList<FinanceBudgetCommitmentEvaluationLineDto> Lines { get; set; } = Array.Empty<FinanceBudgetCommitmentEvaluationLineDto>();
}

public sealed class FinanceBudgetCommitmentEvaluationLineDto
{
    public Guid BudgetScenarioId { get; set; }
    public Guid BudgetReturnId { get; set; }
    public Guid BudgetEntryId { get; set; }
    public Guid AccountId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid? SegmentValueId { get; set; }
    public IReadOnlyList<string> SourceLineIds { get; set; } = Array.Empty<string>();
    public string TransactionCurrencyCode { get; set; } = string.Empty;
    public decimal TransactionAmount { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public decimal FunctionalConversionRate { get; set; }
    public decimal RequestedFunctionalAmount { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal PostedActualAmount { get; set; }
    public decimal OtherReservationsAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal ShortfallAmount { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class FinanceBudgetCommitmentResultDto
{
    public bool IdempotentReplay { get; set; }
    public string EvaluationHash { get; set; } = string.Empty;
    public IReadOnlyList<FinanceBudgetReservationDto> Reservations { get; set; } = Array.Empty<FinanceBudgetReservationDto>();
}

public sealed class FinanceBudgetReservationDto
{
    public Guid Id { get; set; }
    public Guid BudgetEntryId { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string? SourceVersion { get; set; }
    public DateTime BudgetDate { get; set; }
    public IReadOnlyList<string> SourceLineIds { get; set; } = Array.Empty<string>();
    public string TransactionCurrencyCode { get; set; } = string.Empty;
    public decimal TransactionAmount { get; set; }
    public decimal FunctionalAmount { get; set; }
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public bool IdempotentReplay { get; set; }
}

public sealed class SetFinanceBudgetReservationAmountDto
{
    public decimal DesiredTransactionAmount { get; set; }
    public int ExpectedVersion { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class ReleaseFinanceBudgetReservationDto
{
    public string Reason { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class ApplyFinanceBudgetPostingOutcomeDto
{
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
    public string PostingSourceDocumentType { get; set; } = string.Empty;
    public Guid PostingSourceDocumentId { get; set; }
    public string PostingAction { get; set; } = string.Empty;
    public decimal RemainingTransactionAmount { get; set; }
    public int ExpectedVersion { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}
