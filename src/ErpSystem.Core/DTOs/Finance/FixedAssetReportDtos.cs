using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public class FixedAssetReportQueryDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public FixedAssetStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public string? BookClassification { get; set; }
    public string? Location { get; set; }
    public string? SegmentString { get; set; }
}

public class FixedAssetRegisterDto
{
    public List<FixedAssetRegisterItemDto> Items { get; set; } = new();
    public decimal TotalCost { get; set; }
    public decimal TotalAccumulatedDepreciation { get; set; }
    public decimal TotalNetBookValue { get; set; }
}

public class FixedAssetRegisterItemDto
{
    public Guid Id { get; set; }
    public Guid? BookValueId { get; set; }
    public Guid CategoryId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime AcquisitionDate { get; set; }
    public DateTime? CapitalizationDate { get; set; }
    public decimal Cost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public FixedAssetStatus Status { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public string? CurrentSegmentString { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public decimal PostedGlCostMovement { get; set; }
    public decimal PostedGlAccumulatedDepreciationMovement { get; set; }
    public decimal ReconciliationVariance { get; set; }
    public bool HasPostedGlReference { get; set; }
    public bool IsApSourced => string.Equals(SourceDocumentType, "VendorInvoice", StringComparison.OrdinalIgnoreCase);
}

public class AssetDisposalReportDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public DisposalType DisposalType { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal DisposalCost { get; set; }
    public decimal NetProceeds { get; set; }
    public decimal CostAtDisposal { get; set; }
    public decimal AccumulatedDepreciationAtDisposal { get; set; }
    public decimal AccumulatedImpairmentAtDisposal { get; set; }
    public decimal RevaluationSurplusAtDisposal { get; set; }
    public decimal NetBookValue { get; set; }
    public decimal GainLoss { get; set; }
    public string? BuyerName { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public bool HasPostedGlReference { get; set; }
    public string GainLossPresentation { get; set; } = string.Empty;
    public string? PresentationWarning { get; set; }
}

public class AssetTransferReportDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public AssetTransferType TransferType { get; set; }
    public AssetTransferStatus Status { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public string? FromSegmentString { get; set; }
    public string? ToSegmentString { get; set; }
    public string? Reason { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public bool HasGlImpact { get; set; }
    public bool HasPostedGlReference { get; set; }
}

public class FixedAssetAdditionsReportDto
{
    public List<FixedAssetAdditionReportItemDto> Items { get; set; } = new();
    public decimal TotalCapitalizedCost { get; set; }
    public decimal TotalPostedGlCost { get; set; }
    public decimal TotalVariance { get; set; }
}

public class FixedAssetAdditionReportItemDto
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime? CapitalizationDate { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public string FunctionalCurrencyCode { get; set; } = "GHS";
    public string? TransactionCurrencyCode { get; set; }
    public decimal CapitalizedCost { get; set; }
    public decimal PostedGlCost { get; set; }
    public decimal Variance { get; set; }
    public bool IsApSourced { get; set; }
    public bool IsDirectCapitalization { get; set; }
    public bool MissingPostingReference { get; set; }
}

public class FixedAssetDepreciationReportDto
{
    public List<FixedAssetDepreciationReportItemDto> Items { get; set; } = new();
    public decimal TotalDepreciation { get; set; }
    public decimal TotalPostedExpense { get; set; }
    public decimal TotalPostedAccumulatedDepreciation { get; set; }
    public decimal TotalVariance { get; set; }
}

public class FixedAssetDepreciationReportItemDto
{
    public Guid ScheduleId { get; set; }
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public Guid? DepreciationRunId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public DateTime? PostingDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValueBefore { get; set; }
    public decimal NetBookValue { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public decimal PostedExpenseDebit { get; set; }
    public decimal PostedAccumulatedDepreciationCredit { get; set; }
    public decimal Variance { get; set; }
    public bool HasPostedGlReference { get; set; }
}

public class FixedAssetAccumulatedDepreciationReportDto
{
    public List<FixedAssetAccumulatedDepreciationReportItemDto> Items { get; set; } = new();
    public decimal TotalSubledgerAccumulatedDepreciation { get; set; }
    public decimal TotalPostedGlAccumulatedDepreciation { get; set; }
    public decimal TotalVariance { get; set; }
}

public class FixedAssetAccumulatedDepreciationReportItemDto
{
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string BookClassification { get; set; } = "IFRS";
    public decimal SubledgerAccumulatedDepreciation { get; set; }
    public decimal PostedGlAccumulatedDepreciation { get; set; }
    public decimal Variance { get; set; }
}

public class FixedAssetValuationMovementReportDto
{
    public List<FixedAssetValuationMovementReportItemDto> Items { get; set; } = new();
    public decimal TotalRevaluationIncrease { get; set; }
    public decimal TotalRevaluationDecrease { get; set; }
    public decimal TotalImpairmentLoss { get; set; }
    public decimal TotalPostedGlMovement { get; set; }
}

public class FixedAssetValuationMovementReportItemDto
{
    public Guid ValuationId { get; set; }
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public ValuationType ValuationType { get; set; }
    public DateTime ValuationDate { get; set; }
    public DateTime AccountingDate { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public decimal CarryingAmountBefore { get; set; }
    public decimal CarryingAmountAfter { get; set; }
    public decimal RevaluationSurplus { get; set; }
    public decimal RevaluationDeficit { get; set; }
    public decimal ImpairmentLoss { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public decimal PostedGlMovement { get; set; }
    public bool HasPostedGlReference { get; set; }
}

public class FixedAssetRollForwardReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<FixedAssetRollForwardRowDto> Rows { get; set; } = new();
    public FixedAssetRollForwardRowDto Totals { get; set; } = new();
}

public class FixedAssetRollForwardRowDto
{
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = "Total";
    public string BookClassification { get; set; } = "IFRS";
    public int AssetCount { get; set; }
    public decimal OpeningCost { get; set; }
    public decimal Additions { get; set; }
    public decimal RevaluationIncrease { get; set; }
    public decimal RevaluationDecrease { get; set; }
    public decimal ImpairmentAdditions { get; set; }
    public decimal DepreciationCharge { get; set; }
    public decimal AccumulatedDepreciationMovement { get; set; }
    public decimal Disposals { get; set; }
    public decimal AccumulatedDepreciationCleared { get; set; }
    public decimal AccumulatedImpairmentCleared { get; set; }
    public decimal ClosingCost { get; set; }
    public decimal ClosingAccumulatedDepreciation { get; set; }
    public decimal ClosingAccumulatedImpairment { get; set; }
    public decimal ClosingNetBookValue { get; set; }
}

public class FixedAssetGlReconciliationReportDto
{
    public DateTime? AsOfDate { get; set; }
    public List<FixedAssetGlReconciliationRowDto> Rows { get; set; } = new();
    public List<FixedAssetReportingDiagnosticDto> Diagnostics { get; set; } = new();
    public decimal TotalGlBalance { get; set; }
    public decimal TotalSubledgerBalance { get; set; }
    public decimal TotalVariance { get; set; }
    public bool IsReconciled => Math.Abs(TotalVariance) < 0.01m && Diagnostics.All(d => d.Severity != "Critical");
}

public class FixedAssetGlReconciliationRowDto
{
    public string Area { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public AccountType? AccountType { get; set; }
    public decimal GlBalance { get; set; }
    public decimal SubledgerBalance { get; set; }
    public decimal Variance { get; set; }
    public int SourceDocumentCount { get; set; }
    public int MissingPostingReferenceCount { get; set; }
    public int GlWithoutSourceReferenceCount { get; set; }
    public int SubledgerWithoutGlCount { get; set; }
    public string? PresentationWarning { get; set; }
}

public class FixedAssetReportingDiagnosticDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public Guid? FixedAssetId { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? AccountId { get; set; }
}

public class VerificationSummaryDto
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TotalItems { get; set; }
    public int VerifiedItems { get; set; }
    public int MissingItems { get; set; }
    public List<AssetConditionSummaryDto> ConditionSummary { get; set; } = new();
    public List<AssetVerificationItemDto> DetailedItems { get; set; } = new();
}

public class AssetConditionSummaryDto
{
    public AssetCondition Condition { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}
