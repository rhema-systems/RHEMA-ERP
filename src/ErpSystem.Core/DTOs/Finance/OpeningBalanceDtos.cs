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

public sealed class UpdateOpeningBalanceBatchDto
{
    public string? SourceReference { get; set; }
    public string? Description { get; set; }
    public DateTime OpeningDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public IReadOnlyList<CreateOpeningBalanceLineDto> Lines { get; set; } = Array.Empty<CreateOpeningBalanceLineDto>();
}

public sealed class SubmitOpeningBalanceBatchDto
{
    public string? Comment { get; set; }
}

public sealed class PostOpeningBalanceBatchDto
{
    public string? Comment { get; set; }
}

/// <summary>
/// Creates a controlled GL opening-balance batch from fixed-asset book values already loaded by
/// the existing asset import. The request deliberately identifies assets, not user-entered GL
/// accounts: Finance derives the cost and accumulated-depreciation accounts from the approved
/// category configuration so the register and ledger cannot be mapped differently at cutover.
/// </summary>
public sealed class CreateFixedAssetOpeningBalanceBatchDto
{
    public string? BatchNumber { get; set; }
    public string? SourceReference { get; set; }
    public string? Description { get; set; }
    public DateTime OpeningDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public string? IdempotencyKey { get; set; }
    /// <summary>
    /// Identifies the exact imported book-value rows selected by the operator. Asset IDs are not
    /// sufficiently precise because one asset can carry IFRS, Tax, or other parallel books; using
    /// the book-value key prevents a later header-book change from silently selecting different
    /// accounting evidence for the same asset.
    /// </summary>
    public IReadOnlyList<Guid> FixedAssetBookValueIds { get; set; } = Array.Empty<Guid>();
}

public sealed class SubledgerOpeningBalanceReadinessDto
{
    public int ApOpeningInvoiceCount { get; set; }
    public int PostedApOpeningInvoiceCount { get; set; }
    public decimal ApOpeningInvoiceFunctionalAmount { get; set; }
    public int ArOpeningInvoiceCount { get; set; }
    public int PostedArOpeningInvoiceCount { get; set; }
    public decimal ArOpeningInvoiceFunctionalAmount { get; set; }
    public int FixedAssetOpeningBookValueCount { get; set; }
    public int PostedFixedAssetOpeningBookValueCount { get; set; }
    public decimal FixedAssetOpeningCost { get; set; }
    public decimal FixedAssetOpeningAccumulatedDepreciation { get; set; }
    public decimal FixedAssetOpeningNetBookValue { get; set; }
    public IReadOnlyList<FixedAssetOpeningBalanceCandidateDto> FixedAssetCandidates { get; set; }
        = Array.Empty<FixedAssetOpeningBalanceCandidateDto>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class FixedAssetOpeningBalanceCandidateDto
{
    public Guid FixedAssetId { get; set; }
    public Guid FixedAssetBookValueId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string BookClassification { get; set; } = string.Empty;
    public DateTime? OpeningAsOfDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public bool OpeningPostedToGl { get; set; }
    public Guid? OpeningJournalEntryId { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
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
