namespace ErpSystem.Core.DTOs.Finance;

public sealed class CreateOpeningBalanceBatchDto
{
    public string BatchNumber { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? Description { get; set; }
    public DateTime OpeningDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public string? IdempotencyKey { get; set; }
    public IReadOnlyList<CreateOpeningBalanceLineDto> Lines { get; set; } = Array.Empty<CreateOpeningBalanceLineDto>();
}

public sealed class CreateOpeningBalanceLineDto
{
    public Guid AccountId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? TransactionCurrencyCode { get; set; }
    public string? FunctionalCurrencyCode { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public string? SegmentString { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? CounterpartyType { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
}

public sealed class SubmitOpeningBalanceBatchDto
{
    public string? Comment { get; set; }
}

public sealed class PostOpeningBalanceBatchDto
{
    public string? Comment { get; set; }
}

public sealed class OpeningBalanceBatchDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? Description { get; set; }
    public DateTime OpeningDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string FiscalPeriodCode { get; set; } = string.Empty;
    public string BookClassification { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Difference { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? FailureReason { get; set; }
    public IReadOnlyList<OpeningBalanceLineDto> Lines { get; set; } = Array.Empty<OpeningBalanceLineDto>();
}

public sealed class OpeningBalanceLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string TransactionCurrencyCode { get; set; } = string.Empty;
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public Guid? ExchangeRateId { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public string? SegmentString { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? CounterpartyType { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
}

public sealed class OpeningBalanceValidationResultDto
{
    public Guid BatchId { get; set; }
    public bool IsValid { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Difference { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class OpeningBalanceDiagnosticDto
{
    public string DiagnosticCode { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public Guid? BatchId { get; set; }
    public string? Reference { get; set; }
    public string Message { get; set; } = string.Empty;
}
