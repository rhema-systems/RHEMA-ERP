namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinancePostingRequestDto
{
    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceDocumentTenantId { get; set; }
    public Guid? ExistingJournalEntryId { get; set; }
    public Guid? ReversalOfJournalEntryId { get; set; }
    public string? ReversalReason { get; set; }
    public string? ReversalType { get; set; }
    public string PostingAction { get; set; } = "Post";
    public string? SourceDocumentReference { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; } = DateTime.UtcNow;
    public Guid? FiscalPeriodId { get; set; }
    public string JournalType { get; set; } = "System Generated";
    public string BookClassification { get; set; } = "IFRS";
    public string FunctionalCurrencyCode { get; set; } = "GHS";
    public string? IdempotencyKey { get; set; }
    public bool ReturnExistingOnDuplicate { get; set; } = true;

    /// <summary>
    /// Year-end closing entries must post into the year's final period after every period is
    /// closed, so the engine's open-period gate cannot apply. Honored only for the GL
    /// year-end close/reversal source document types; all other requests are still rejected.
    /// </summary>
    public bool AllowPostingToClosedPeriod { get; set; }

    public IReadOnlyList<FinancePostingLineDto> Lines { get; set; } = Array.Empty<FinancePostingLineDto>();
    public IReadOnlyList<FinanceTaxCalculationSnapshotDto> TaxCalculationSnapshots { get; set; } = Array.Empty<FinanceTaxCalculationSnapshotDto>();
}

public sealed class FinancePostingLineDto
{
    public Guid AccountId { get; set; }
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? TransactionCurrency { get; set; }
    public decimal? TransactionDebitAmount { get; set; }
    public decimal? TransactionCreditAmount { get; set; }
    public decimal? ForeignCurrencyAmount { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? ExchangeRateSource { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public string? SourceReferenceNumber { get; set; }
    public int? LineNumber { get; set; }
    public string? SegmentString { get; set; }
    public string? Notes { get; set; }
    public string? TransactionTag { get; set; }
}

public sealed class FinancePostingResultDto
{
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
    public string JournalEntryNumber { get; set; } = string.Empty;
    public string PostingStatus { get; set; } = string.Empty;
    public bool WasDuplicate { get; set; }
    public decimal TotalDebitAmount { get; set; }
    public decimal TotalCreditAmount { get; set; }
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string PostingAction { get; set; } = string.Empty;
}

public sealed class FinanceReversalPlanDto
{
    public bool IsDefined { get; set; }
    public Guid OriginalPostingEventId { get; set; }
    public Guid OriginalJournalEntryId { get; set; }
    public string PostingAction { get; set; } = "Reverse";
    public DateTime ReversalDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public IReadOnlyList<FinancePostingLineDto> ReversalLines { get; set; } = Array.Empty<FinancePostingLineDto>();
}

public sealed class FinanceTaxCalculationSnapshotDto
{
    public string DocumentType { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid TaxId { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public ErpSystem.Core.Enums.CompoundBasis CompoundBasis { get; set; }
    public int CalculationOrder { get; set; }
    public DateTime CalculationDate { get; set; } = DateTime.UtcNow;
    public bool IsManualOverride { get; set; }
    public string? OverrideReason { get; set; }
}
