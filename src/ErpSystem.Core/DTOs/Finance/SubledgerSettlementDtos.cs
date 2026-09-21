namespace ErpSystem.Core.DTOs.Finance;

public sealed class SubledgerSettlementRebuildRequestDto
{
    public string SourceModule { get; set; } = "Both";
    public DateTime? AsOfDate { get; set; }
    public bool RecordAudit { get; set; } = true;
}

public sealed class SubledgerSettlementRebuildResultDto
{
    public Guid RebuildBatchId { get; set; }
    public DateTime AsOfDate { get; set; }
    public DateTime RebuiltAt { get; set; }
    public int ApDocumentCount { get; set; }
    public int ArDocumentCount { get; set; }
    public int ApUnappliedSettlementCount { get; set; }
    public int ArUnappliedSettlementCount { get; set; }
    public int ApplicationCount { get; set; }
    public int DiagnosticCount { get; set; }
    public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; set; } = new();
}

public sealed class SubledgerSettlementBalanceDto
{
    public Guid Id { get; set; }
    public string SourceModule { get; set; } = string.Empty;
    public Guid CounterpartyId { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;
    public Guid? SourcePostingEventId { get; set; }
    public Guid? SourceJournalEntryId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string DocumentCurrencyCode { get; set; } = string.Empty;
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public decimal OriginalDocumentAmount { get; set; }
    public decimal OriginalFunctionalAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal CreditedAmount { get; set; }
    public decimal WithheldAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string SettlementStatus { get; set; } = string.Empty;
    public bool HasDiagnostics { get; set; }
    public string? DiagnosticFlags { get; set; }
    public decimal OperationalOutstandingSnapshot { get; set; }
    public decimal OperationalVariance { get; set; }
    public List<SubledgerSettlementApplicationDto> Applications { get; set; } = new();
}

public sealed class SubledgerUnappliedSettlementBalanceDto
{
    public Guid Id { get; set; }
    public string SourceModule { get; set; } = string.Empty;
    public Guid CounterpartyId { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public string SettlementSourceType { get; set; } = string.Empty;
    public Guid SettlementSourceId { get; set; }
    public string SettlementSourceNumber { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public Guid? SettlementPostingEventId { get; set; }
    public Guid? SettlementJournalEntryId { get; set; }
    public DateTime SettlementDate { get; set; }
    public string DocumentCurrencyCode { get; set; } = string.Empty;
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal UnappliedAmount { get; set; }
    public bool HasDiagnostics { get; set; }
    public string? DiagnosticFlags { get; set; }
}

public sealed class SubledgerUnappliedSettlementReportDto
{
    public string SourceModule { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public bool UsesSettlementReadModel { get; set; } = true;
    public decimal TotalUnappliedAmount { get; set; }
    public int CounterpartyCount { get; set; }
    public List<SubledgerUnappliedSettlementBalanceDto> Lines { get; set; } = new();
    public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; set; } = new();
}

public sealed class SubledgerSettlementApplicationDto
{
    public Guid Id { get; set; }
    public string SettlementSourceType { get; set; } = string.Empty;
    public Guid SettlementSourceId { get; set; }
    public Guid? SettlementAllocationId { get; set; }
    public Guid? SettlementPostingEventId { get; set; }
    public Guid? SettlementJournalEntryId { get; set; }
    public DateTime SettlementDate { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal CreditedAmount { get; set; }
    public decimal WithheldAmount { get; set; }
    public Guid? FxRealizedSettlementId { get; set; }
    public string? Notes { get; set; }
}

public sealed class SubledgerControlReconciliationDto
{
    public string SourceModule { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public Guid? AccountingBookId { get; set; }
    public string? AccountingBookCode { get; set; }
    public string? AccountingBookName { get; set; }
    public Guid? ControlAccountId { get; set; }
    public string? ControlAccountNumber { get; set; }
    public string? ControlAccountName { get; set; }
    public decimal ReadModelOutstanding { get; set; }
    public decimal PostedGlControlBalance { get; set; }
    public decimal Variance { get; set; }
    public int DocumentCount { get; set; }
    public int DiagnosticCount { get; set; }
    public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; set; } = new();
}

public sealed class SubledgerSettlementDiagnosticDto
{
    public string SourceModule { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? SourceDocumentId { get; set; }
    public Guid? SettlementSourceId { get; set; }
    public Guid? PostingEventId { get; set; }
    public decimal? VarianceAmount { get; set; }
}
