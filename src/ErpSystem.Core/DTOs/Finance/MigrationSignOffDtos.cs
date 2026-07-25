namespace ErpSystem.Core.DTOs.Finance;

public sealed class PostingBackReferenceRepairRequestDto
{
    public bool Repair { get; set; }
    public IReadOnlyList<string> SourceDocumentTypes { get; set; } = Array.Empty<string>();
}

public sealed class PostingBackReferenceRepairResultDto
{
    public Guid TenantId { get; set; }
    public bool RepairMode { get; set; }
    public int ExaminedCount { get; set; }
    public int RepairableCount { get; set; }
    public int RepairedCount { get; set; }
    public int AmbiguousCount { get; set; }
    public int UnsupportedCount { get; set; }
    public IReadOnlyList<PostingBackReferenceRepairItemDto> Items { get; set; } = Array.Empty<PostingBackReferenceRepairItemDto>();
}

public sealed class PostingBackReferenceRepairItemDto
{
    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string? SourceReference { get; set; }
    public Guid? CurrentJournalEntryId { get; set; }
    public Guid? CurrentPostingEventId { get; set; }
    public Guid? CandidateJournalEntryId { get; set; }
    public Guid? CandidatePostingEventId { get; set; }
    public int CandidatePostingEventCount { get; set; }
    public bool SupportsPostingEventBackReference { get; set; }
    public bool CanRepair { get; set; }
    public bool WasRepaired { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
}

public sealed class BankSnapshotRebuildRequestDto
{
    public bool Repair { get; set; }
    public Guid? BankAccountId { get; set; }
}

public sealed class BankSnapshotRebuildResultDto
{
    public Guid TenantId { get; set; }
    public bool RepairMode { get; set; }
    public int ExaminedCount { get; set; }
    public int VarianceCount { get; set; }
    public int RepairedCount { get; set; }
    public IReadOnlyList<BankSnapshotDiagnosticDto> Items { get; set; } = Array.Empty<BankSnapshotDiagnosticDto>();
}

public sealed class BankSnapshotDiagnosticDto
{
    public Guid BankAccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid? GlAccountId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal StoredCurrentBalance { get; set; }
    public decimal StoredAvailableBalance { get; set; }
    public decimal StoredOpeningBalance { get; set; }
    public decimal PostedGlBalance { get; set; }
    public decimal Variance { get; set; }
    public bool CanRepair { get; set; }
    public bool WasRepaired { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
}

public sealed class SubledgerOpeningMigrationDecisionDto
{
    public Guid TenantId { get; set; }
    public bool GlTrialBalanceOpeningSupported { get; set; } = true;
    public bool ApOpeningInvoicesSupportedByGlBatch { get; set; }
    public bool ArOpeningInvoicesSupportedByGlBatch { get; set; }
    public bool FixedAssetOpeningRegisterSupportedByGlBatch { get; set; }
    public IReadOnlyList<SubledgerOpeningMigrationScopeDto> Scope { get; set; } = Array.Empty<SubledgerOpeningMigrationScopeDto>();
}

public sealed class SubledgerOpeningMigrationScopeDto
{
    public string Area { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public bool BlocksFinalSignOffIfRequired { get; set; }
    public string Rationale { get; set; } = string.Empty;
}

public sealed class FinalMigrationSignOffRunRequestDto
{
    public string RunType { get; set; } = "DryRun";
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow.Date;
    public Guid? FiscalPeriodId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public bool RequirePostedOpeningBalanceBatch { get; set; } = true;
    public bool AcceptBankSnapshotVariance { get; set; }
    public bool GenerateEvidenceExports { get; set; }
    public CutoverDataShapeDto CutoverDataShape { get; set; } = new();
    public IReadOnlyList<string> AcceptedLimitationIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> NotApplicableLimitationIds { get; set; } = Array.Empty<string>();
}

public sealed class CutoverDataShapeDto
{
    public bool HasOpenApSupplierInvoices { get; set; }
    public bool HasOpenArCustomerInvoices { get; set; }
    public bool HasUnappliedApPaymentsOrSupplierAdvances { get; set; }
    public bool HasUnappliedArReceiptsOrCustomerAdvances { get; set; }
    public bool HasFixedAssetOpeningRegisterBalances { get; set; }
    public bool HasWithholdingCertificateBalances { get; set; }
    public bool HasForeignCurrencyOpenApArBalances { get; set; }
    public bool HasBankBalancesRequiringCashbookDetail { get; set; }
}

public sealed class FinalMigrationSignOffRunDto
{
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public string RunReference { get; set; } = string.Empty;
    public string RunType { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public string Status { get; set; } = "Running";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CheckCount { get; set; }
    public int BlockingFindingsCount { get; set; }
    public int WarningCount { get; set; }
    public int AcceptedLimitationsCount { get; set; }
    public int NotApplicableLimitationsCount { get; set; }
    public IReadOnlyList<FinalMigrationSignOffCheckDto> Checks { get; set; } = Array.Empty<FinalMigrationSignOffCheckDto>();
    public IReadOnlyList<FinalMigrationSignOffEvidenceExportDto> EvidenceExports { get; set; } = Array.Empty<FinalMigrationSignOffEvidenceExportDto>();
    public IReadOnlyList<LimitationAcceptanceMatrixItemDto> LimitationAcceptanceMatrix { get; set; } = Array.Empty<LimitationAcceptanceMatrixItemDto>();
    public CutoverDataShapeEvaluationDto CutoverDataShapeEvaluation { get; set; } = new();
}

public sealed class FinalMigrationSignOffCheckDto
{
    public string Area { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = "Passed";
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public int? Count { get; set; }
    public string? SourceReference { get; set; }
    public Guid? SourceId { get; set; }
}

public sealed class FinalMigrationSignOffEvidenceExportDto
{
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = FinanceReportExportFormats.Csv;
    public string Status { get; set; } = "Available";
    public string SourceOfTruthMode { get; set; } = string.Empty;
    public bool UsesSettlementReadModel { get; set; }
    public int? RowCount { get; set; }
    public string? FileName { get; set; }
    public string? Message { get; set; }
}

public sealed class LimitationAcceptanceMatrixItemDto
{
    public string LimitationId { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Classification { get; set; } = "GoLiveBlocking";
    public bool GoLiveBlocking { get; set; } = true;
    public bool BlocksSignOff { get; set; } = true;
    public string RequiredAction { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
}

public sealed class CutoverDataShapeEvaluationDto
{
    public bool RequiresSubledgerOpeningMigration { get; set; }
    public bool FinLim0048BlocksSignOff { get; set; }
    public IReadOnlyList<string> RequiredSourceOpeningAreas { get; set; } = Array.Empty<string>();
    public string Decision { get; set; } = string.Empty;
}

public sealed class SignOffReviewRequestDto
{
    public Guid RunId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? ReviewerComments { get; set; }
}

public sealed class SignOffReviewResultDto
{
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
}
