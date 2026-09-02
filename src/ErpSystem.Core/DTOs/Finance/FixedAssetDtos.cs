using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.DTOs.Finance;

public class FixedAssetCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepreciationMethod DefaultMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; }
    public decimal DefaultResidualValuePercent { get; set; }
    public decimal DefaultDiminishingBalanceRatePercent { get; set; }
    public decimal DefaultLifetimeProductionCapacity { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public Guid? GainOnDisposalAccountId { get; set; }
    public Guid? LossOnDisposalAccountId { get; set; }
    public Guid? DisposalProceedsClearingAccountId { get; set; }
    public Guid? RevaluationSurplusAccountId { get; set; }
    public Guid? RevaluationLossAccountId { get; set; }
    public Guid? ImpairmentLossAccountId { get; set; }
    public Guid? AccumulatedImpairmentAccountId { get; set; }
    public Guid? ImpairmentReversalAccountId { get; set; }
    public Guid? AucAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateFixedAssetCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepreciationMethod DefaultMethod { get; set; } = DepreciationMethod.StraightLine;
    public int DefaultUsefulLifeMonths { get; set; } = 36;
    public decimal DefaultResidualValuePercent { get; set; }
    public decimal DefaultDiminishingBalanceRatePercent { get; set; }
    public decimal DefaultLifetimeProductionCapacity { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public Guid? GainOnDisposalAccountId { get; set; }
    public Guid? LossOnDisposalAccountId { get; set; }
    public Guid? DisposalProceedsClearingAccountId { get; set; }
    public Guid? RevaluationSurplusAccountId { get; set; }
    public Guid? RevaluationLossAccountId { get; set; }
    public Guid? ImpairmentLossAccountId { get; set; }
    public Guid? AccumulatedImpairmentAccountId { get; set; }
    public Guid? ImpairmentReversalAccountId { get; set; }
    public Guid? AucAccountId { get; set; }
}

public class UpdateFixedAssetCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepreciationMethod DefaultMethod { get; set; } = DepreciationMethod.StraightLine;
    public int DefaultUsefulLifeMonths { get; set; } = 36;
    public decimal DefaultResidualValuePercent { get; set; }
    public decimal DefaultDiminishingBalanceRatePercent { get; set; }
    public decimal DefaultLifetimeProductionCapacity { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public Guid? GainOnDisposalAccountId { get; set; }
    public Guid? LossOnDisposalAccountId { get; set; }
    public Guid? DisposalProceedsClearingAccountId { get; set; }
    public Guid? RevaluationSurplusAccountId { get; set; }
    public Guid? RevaluationLossAccountId { get; set; }
    public Guid? ImpairmentLossAccountId { get; set; }
    public Guid? AccumulatedImpairmentAccountId { get; set; }
    public Guid? ImpairmentReversalAccountId { get; set; }
    public Guid? AucAccountId { get; set; }
}

public class FixedAssetDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public Guid? CurrentCustodianId { get; set; }
    public string? CurrentCustodianName { get; set; }
    public string? CurrentSegmentString { get; set; }
    public Guid? CurrentSegmentLookupValueId { get; set; }
    public Guid FixedAssetCategoryId { get; set; }
    public string? FixedAssetCategoryName { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal InstallationCost { get; set; }
    public decimal TaxAmount { get; set; }
    public DateTime? CapitalizationDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal NetBookValue { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public DepreciationConvention DepreciationConvention { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public decimal DiminishingBalanceRatePercent { get; set; }
    public decimal LifetimeProductionCapacity { get; set; }
    public decimal AccumulatedProductionUnits { get; set; }
    public FixedAssetStatus Status { get; set; }
    public DateTime? DisposalDate { get; set; }
    public string FunctionalCurrencyCode { get; set; } = "GHS";
    public string? TransactionCurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public DateTime? CapitalizedAt { get; set; }
    public FixedAssetCapitalizationApprovalSnapshotDto? CapitalizationApprovalSnapshot { get; set; }
    public string? CapitalizationApprovalSnapshotHash { get; set; }
    public Guid? CapitalizationApprovalWorkflowInstanceId { get; set; }
    public Guid? CapitalizationApprovalSubmittedByUserId { get; set; }
    public DateTime? CapitalizationApprovalSubmittedAt { get; set; }
    public Guid? CapitalizationApprovalApprovedByUserId { get; set; }
    public DateTime? CapitalizationApprovalApprovedAt { get; set; }
    public DateTime? CapitalizationApprovalInvalidatedAt { get; set; }
    public string? CapitalizationApprovalInvalidationReason { get; set; }
    public Guid? CapitalizationReversalJournalEntryId { get; set; }
    public Guid? CapitalizationReversalPostingEventId { get; set; }
    public DateTime? CapitalizationReversedAt { get; set; }
    public string? CapitalizationReversalReason { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public List<FixedAssetBookValueDto> BookValues { get; set; } = new();
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
}

/// <summary>
/// Authoritative Fixed Assets registration request produced by a governed stores issue.
/// Finance has already posted the balanced inventory-to-asset journal identified below;
/// this contract records the physical asset/custody side without creating a second journal.
/// </summary>
public sealed class RegisterInventoryIssueFixedAssetDto
{
    public Guid IssueVoucherId { get; set; }
    public Guid IssueVoucherLineId { get; set; }
    public string IssueVoucherNumber { get; set; } = string.Empty;
    public Guid FixedAssetCategoryId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public Guid CustodianEmployeeId { get; set; }
    public DateTime IssueDate { get; set; }
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
}

/// <summary>
/// Compensating register update for a full, governed return of an inventory-issued asset.
/// The journal is posted by the central Finance engine before this owner is invoked.
/// </summary>
public sealed class ReverseInventoryIssueFixedAssetDto
{
    public Guid ReturnVoucherId { get; set; }
    public Guid ReturnVoucherLineId { get; set; }
    public string ReturnVoucherNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
}

/// <summary>
/// Reinstates a returned inventory-issued asset when the return voucher is reversed.
/// </summary>
public sealed class ReinstateInventoryIssueFixedAssetDto
{
    public Guid ReturnVoucherId { get; set; }
    public Guid ReturnVoucherLineId { get; set; }
    public string ReturnVoucherNumber { get; set; } = string.Empty;
    public DateTime ReversalDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
}

public class FixedAssetBookValueDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public Guid AccountingBookId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public string? AccountingBookName { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public decimal ResidualValue { get; set; }
    public int UsefulLifeMonths { get; set; }
    public int? RemainingUsefulLifeMonths { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public DepreciationConvention DepreciationConvention { get; set; }
    public decimal DiminishingBalanceRatePercent { get; set; }
    public decimal LifetimeProductionCapacity { get; set; }
    public decimal AccumulatedProductionUnits { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public DateTime? OpeningAsOfDate { get; set; }
    public decimal OpeningYtdDepreciation { get; set; }
    public DateTime? LastDepreciationDate { get; set; }
    public bool OpeningPostedToGl { get; set; }
    public DateTime? OpeningPostedDate { get; set; }
    public string OpeningSource { get; set; } = "Manual";
    public DateTime? CapitalizationDate { get; set; }
    public Guid? CapitalizationJournalEntryId { get; set; }
    public Guid? CapitalizationPostingEventId { get; set; }
    public Guid? CapitalizationReversalJournalEntryId { get; set; }
    public Guid? CapitalizationReversalPostingEventId { get; set; }
    public DateTime? CapitalizationReversedAt { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
}

public class CreateFixedAssetDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public Guid FixedAssetCategoryId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal InstallationCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public DepreciationConvention DepreciationConvention { get; set; } = DepreciationConvention.FullMonth;
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public decimal DiminishingBalanceRatePercent { get; set; }
    public decimal LifetimeProductionCapacity { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public string? SerialNumber { get; set; }
}

public class UpdateFixedAssetDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public Guid FixedAssetCategoryId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal InstallationCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public DepreciationConvention DepreciationConvention { get; set; } = DepreciationConvention.FullMonth;
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public decimal DiminishingBalanceRatePercent { get; set; }
    public decimal LifetimeProductionCapacity { get; set; }
    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Draft;
    public DateTime? DisposalDate { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public string? SerialNumber { get; set; }
}

public class CapitalizeFixedAssetDto
{
    public DateTime CapitalizationDate { get; set; } = DateTime.UtcNow;
    public Guid? CreditAccountId { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
    public string? Reference { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? TransactionCurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

/// <summary>
/// Exact direct-capitalization journal proposal submitted for independent approval. Posting does
/// not accept replacement accounting values; it consumes the immutable snapshot created here.
/// </summary>
public sealed class SubmitFixedAssetCapitalizationDto
{
    public DateTime CapitalizationDate { get; set; } = DateTime.UtcNow.Date;
    public Guid? CreditAccountId { get; set; }
    public string? Reference { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public decimal? Amount { get; set; }
    public string? TransactionCurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

/// <summary>
/// Canonical evidence shown to the checker and later consumed by the posting service.
/// </summary>
public sealed class FixedAssetCapitalizationApprovalSnapshotDto
{
    public int Version { get; set; } = 1;
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public Guid FixedAssetCategoryId { get; set; }
    public Guid DebitAccountId { get; set; }
    public Guid CreditAccountId { get; set; }
    public bool UsesCategoryAucAccount { get; set; }
    public DateTime CapitalizationDate { get; set; }
    public decimal TransactionAmount { get; set; }
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public string TransactionCurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public DateTime ExchangeRateDate { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = "FixedAsset";
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
    public string AssetEvidenceHash { get; set; } = string.Empty;
}

public sealed class RequestFixedAssetCapitalizationReversalDto
{
    public DateTime ReversalDate { get; set; } = DateTime.UtcNow.Date;
    public string Reason { get; set; } = string.Empty;
    public string ImpactAssessment { get; set; } = string.Empty;
}

public sealed class ReviewFixedAssetCapitalizationReversalDto
{
    public bool Approved { get; set; }
    public string ReviewComment { get; set; } = string.Empty;
}

public sealed class FixedAssetCapitalizationReversalDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public Guid OriginalPostingEventId { get; set; }
    public Guid OriginalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ImpactAssessment { get; set; } = string.Empty;
    public DateTime RequestedReversalDate { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByUserName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? FailureReason { get; set; }
}

public class RunDepreciationDto
{
    public Guid FiscalPeriodId { get; set; }
    public Guid? FixedAssetId { get; set; }
    public string? BookClassification { get; set; }
    public bool PostToGl { get; set; } = true;
    public DateTime? PostingDate { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }

    /// <summary>
    /// Verified output/usage evidence for units-of-production assets included in this run. The
    /// depreciation run's existing approval workflow independently approves these values before GL
    /// posting, avoiding an ungoverned second usage-entry path.
    /// </summary>
    public List<FixedAssetProductionUsageDto> ProductionUsageEntries { get; set; } = new();
}

public sealed class FixedAssetProductionUsageDto
{
    public Guid FixedAssetId { get; set; }
    public string? BookClassification { get; set; }
    public decimal UnitsConsumed { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string? EvidenceNotes { get; set; }
}

public class AssetDepreciationScheduleDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public Guid? FixedAssetDepreciationRunId { get; set; }
    public Guid? AssetDisposalId { get; set; }
    public Guid? AccountingBookId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public Guid FiscalPeriodId { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciationBefore { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValueBefore { get; set; }
    public decimal NetBookValue { get; set; }
    public decimal DepreciableAmount { get; set; }
    public decimal ResidualValueSnapshot { get; set; }
    public int UsefulLifeMonthsSnapshot { get; set; }
    public DepreciationMethod DepreciationMethodSnapshot { get; set; }
    public decimal DiminishingBalanceRatePercentSnapshot { get; set; }
    public decimal LifetimeProductionCapacitySnapshot { get; set; }
    public decimal PeriodProductionUnits { get; set; }
    public decimal CumulativeProductionUnitsBefore { get; set; }
    public decimal CumulativeProductionUnitsAfter { get; set; }
    public string? ProductionEvidenceReference { get; set; }
    public string? ProductionEvidenceNotes { get; set; }
    public DateTime? PlacedInServiceDateSnapshot { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? PostingDate { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public bool IsProjected { get; set; }
    public int CorrectionSequence { get; set; }
    public bool IsReversed { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? DepreciationReversalId { get; set; }
}

public class FixedAssetDepreciationRunDto
{
    public Guid Id { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid? FixedAssetId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public DateTime PostingDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalDepreciationAmount { get; set; }
    public int CorrectionSequence { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public DateTime? CalculatedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public List<AssetDepreciationScheduleDto> Lines { get; set; } = new();
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
}

public sealed class RequestFixedAssetDepreciationReversalDto
{
    public DateTime ReversalDate { get; set; } = DateTime.UtcNow.Date;
    public string Reason { get; set; } = string.Empty;
    public string ImpactAssessment { get; set; } = string.Empty;
}

public sealed class ReviewFixedAssetDepreciationReversalDto
{
    public bool Approved { get; set; }
    public string ReviewComment { get; set; } = string.Empty;
}

public sealed class FixedAssetDepreciationReversalDto
{
    public Guid Id { get; set; }
    public Guid OriginalDepreciationRunId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string BookClassification { get; set; } = string.Empty;
    public decimal TotalDepreciationAmount { get; set; }
    public int OriginalCorrectionSequence { get; set; }
    public Guid OriginalPostingEventId { get; set; }
    public Guid OriginalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ImpactAssessment { get; set; } = string.Empty;
    public DateTime RequestedReversalDate { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByUserName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? FailureReason { get; set; }
}

public class FixedAssetGlAccountOptionDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AccountCategory { get; set; }
    public string? AccountSubCategory { get; set; }
}

public class FixedAssetGlAccountOptionsDto
{
    public List<FixedAssetGlAccountOptionDto> AssetAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> AccumulatedDepreciationAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> DepreciationExpenseAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> GainOnDisposalAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> LossOnDisposalAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> DisposalProceedsClearingAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> RevaluationSurplusAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> RevaluationLossAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> ImpairmentLossAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> AccumulatedImpairmentAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> ImpairmentReversalAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> AucAccounts { get; set; } = new();
}

public class AssetTransferDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public string? FixedAssetName { get; set; }
    public string? AssetCode { get; set; }
    public DateTime TransferDate { get; set; }
    public AssetTransferType TransferType { get; set; }
    public AssetTransferStatus Status { get; set; }
    public string? FromLocation { get; set; }
    public Guid? FromCustodianId { get; set; }
    public string? FromCustodianName { get; set; }
    public string ToLocation { get; set; } = string.Empty;
    public Guid? ToCustodianId { get; set; }
    public string? ToCustodianName { get; set; }
    public string? FromSegmentString { get; set; }
    public Guid? FromSegmentLookupValueId { get; set; }
    public string? ToSegmentString { get; set; }
    public Guid? ToSegmentLookupValueId { get; set; }
    public Guid? FromFixedAssetCategoryId { get; set; }
    public string? FromFixedAssetCategoryName { get; set; }
    public Guid? ToFixedAssetCategoryId { get; set; }
    public string? ToFixedAssetCategoryName { get; set; }
    public Guid? AccountingBookId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public Guid? FromAssetAccountId { get; set; }
    public Guid? ToAssetAccountId { get; set; }
    public Guid? FromAccumulatedDepreciationAccountId { get; set; }
    public Guid? ToAccumulatedDepreciationAccountId { get; set; }
    public Guid? FromAccumulatedImpairmentAccountId { get; set; }
    public Guid? ToAccumulatedImpairmentAccountId { get; set; }
    public Guid? FromRevaluationSurplusAccountId { get; set; }
    public Guid? ToRevaluationSurplusAccountId { get; set; }
    public decimal ReclassificationAssetCarryingAmount { get; set; }
    public decimal ReclassificationAccumulatedDepreciation { get; set; }
    public decimal ReclassificationAccumulatedImpairment { get; set; }
    public decimal ReclassificationRevaluationSurplus { get; set; }
    public DateTime? AccountingDate { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public string? Reason { get; set; }
    public decimal? TransferCost { get; set; }
    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? Comments { get; set; }
    public string? FailureReason { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public DateTime CreatedAt { get; set; }
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
}

public class RequestAssetTransferDto
{
    public Guid FixedAssetId { get; set; }
    public DateTime TransferDate { get; set; }
    public AssetTransferType TransferType { get; set; } = AssetTransferType.Internal;
    public string ToLocation { get; set; } = string.Empty;
    public Guid? ToCustodianId { get; set; }
    public string? ToSegmentString { get; set; }
    public Guid? ToSegmentLookupValueId { get; set; }
    /// <summary>
    /// Target category for a GL reclassification. It is optional only for a pure dimension move,
    /// where current balances stay in the same accounts but move to a different segment.
    /// </summary>
    public Guid? ToFixedAssetCategoryId { get; set; }
    public Guid? AccountingBookId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public DateTime? AccountingDate { get; set; }
    public string? Reason { get; set; }
    public decimal? TransferCost { get; set; }
    public string? IdempotencyKey { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class ApproveAssetTransferDto
{
    public string? Comments { get; set; }
}

public class AssetDisposalDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public string? FixedAssetName { get; set; }
    public string? AssetCode { get; set; }
    public DateTime DisposalDate { get; set; }
    public DisposalType DisposalType { get; set; }
    public AssetDisposalStatus Status { get; set; }
    public AssetDisposalScope DisposalScope { get; set; }
    public decimal DisposedPortionPercent { get; set; }
    public string? ComponentReference { get; set; }
    public string? ComponentDescription { get; set; }
    public string? AllocationEvidenceReference { get; set; }
    public string? AllocationEvidenceNotes { get; set; }
    public DateTime? AccountingDate { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public Guid? AccountingBookId { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public string? Reason { get; set; }
    public decimal? SaleProceeds { get; set; }
    public decimal? DisposalCost { get; set; }
    public decimal NetProceeds { get; set; }
    public string ProceedsCurrencyCode { get; set; } = "GHS";
    public decimal ProceedsFunctionalAmount { get; set; }
    public Guid? ProceedsExchangeRateId { get; set; }
    public decimal ProceedsExchangeRateValue { get; set; }
    public string ProceedsExchangeRateSource { get; set; } = "Functional currency";
    public DateTime ProceedsExchangeRateDate { get; set; }
    public ExchangeRateType ProceedsExchangeRateType { get; set; }
    public ExchangeRateQuoteSide ProceedsExchangeRateQuoteSide { get; set; }
    public Guid? ProceedsAccountId { get; set; }
    public decimal CostAtDisposal { get; set; }
    public decimal AcquisitionCostAllocated { get; set; }
    public decimal RevaluationAdjustmentAllocated { get; set; }
    public decimal ResidualValueAllocated { get; set; }
    public decimal ProductionCapacityAllocated { get; set; }
    public decimal AccumulatedProductionUnitsAllocated { get; set; }
    public decimal AccumulatedDepreciationAtDisposal { get; set; }
    public decimal FinalDepreciationAmount { get; set; }
    public DateTime? FinalDepreciationFromDate { get; set; }
    public DateTime? FinalDepreciationToDate { get; set; }
    public int FinalDepreciationPeriodDays { get; set; }
    public int FinalDepreciationEligibleDays { get; set; }
    public string? FinalDepreciationProrationBasis { get; set; }
    public DepreciationMethod? FinalDepreciationMethodSnapshot { get; set; }
    public Guid? FinalDepreciationScheduleId { get; set; }
    public decimal FinalDepreciationProductionUnits { get; set; }
    public decimal FinalDepreciationDiminishingRatePercent { get; set; }
    public decimal FinalDepreciationLifetimeProductionCapacity { get; set; }
    public decimal FinalDepreciationCumulativeProductionUnitsBefore { get; set; }
    public decimal FinalDepreciationCumulativeProductionUnitsAfter { get; set; }
    public string? FinalDepreciationEvidenceReference { get; set; }
    public string? FinalDepreciationEvidenceNotes { get; set; }
    public decimal AccumulatedImpairmentAtDisposal { get; set; }
    public decimal RevaluationSurplusAtDisposal { get; set; }
    public Guid? RevaluationSurplusAccountId { get; set; }
    public Guid? RetainedEarningsAccountId { get; set; }
    public decimal RevaluationSurplusTransferAmount { get; set; }
    public decimal NetBookValueAtDisposal { get; set; }
    public decimal GainOrLoss { get; set; }
    public decimal RemainingAcquisitionCostAfterDisposal { get; set; }
    public decimal RemainingAccumulatedDepreciationAfterDisposal { get; set; }
    public decimal RemainingNetBookValueAfterDisposal { get; set; }
    public string? BuyerName { get; set; }
    public Guid? BuyerBusinessPartnerId { get; set; }
    public AssetDisposalSettlementMode SettlementMode { get; set; }
    public AssetDisposalSettlementStatus SettlementStatus { get; set; }
    public Guid? SaleTaxGroupId { get; set; }
    public TaxTreatment SaleTaxTreatment { get; set; }
    public Guid? SettlementPaymentTermId { get; set; }
    public Guid? SettlementPaymentMethodId { get; set; }
    public Guid? SettlementBankAccountId { get; set; }
    public Guid? SettlementLiquidityAccountId { get; set; }
    public string? SettlementReference { get; set; }
    public Guid? CustomerInvoiceId { get; set; }
    public Guid? CustomerPaymentId { get; set; }
    public decimal SettlementInvoiceAmount { get; set; }
    public decimal SettlementTaxAmount { get; set; }
    public DateTime? SettlementCompletedAt { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? Comments { get; set; }
    public string? FailureReason { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
}

public class RequestAssetDisposalDto
{
    public Guid FixedAssetId { get; set; }
    public DateTime DisposalDate { get; set; }
    public DisposalType DisposalType { get; set; }
    public AssetDisposalScope DisposalScope { get; set; } = AssetDisposalScope.WholeAsset;
    public decimal DisposedPortionPercent { get; set; } = 100m;
    public string? ComponentReference { get; set; }
    public string? ComponentDescription { get; set; }
    public string? AllocationEvidenceReference { get; set; }
    public string? AllocationEvidenceNotes { get; set; }
    public string? Reason { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal DisposalCost { get; set; }
    public string? ProceedsCurrencyCode { get; set; }
    /// <summary>
    /// Optional explicit daily rate. When omitted, Finance selects the latest approved rate that
    /// satisfies the tenant's receipt-side quote policy on the disposal date.
    /// </summary>
    public Guid? ProceedsExchangeRateId { get; set; }
    public Guid? ProceedsAccountId { get; set; }
    public string? BuyerName { get; set; }
    /// <summary>
    /// Canonical customer/business-partner identity used to create the linked AR invoice.
    /// Required for every sale with positive proceeds.
    /// </summary>
    public Guid? BuyerBusinessPartnerId { get; set; }
    public AssetDisposalSettlementMode SettlementMode { get; set; } = AssetDisposalSettlementMode.NotApplicable;
    public Guid? SaleTaxGroupId { get; set; }
    public TaxTreatment SaleTaxTreatment { get; set; } = TaxTreatment.Standard;
    public Guid? SettlementPaymentTermId { get; set; }
    public Guid? SettlementPaymentMethodId { get; set; }
    public Guid? SettlementBankAccountId { get; set; }
    public Guid? SettlementLiquidityAccountId { get; set; }
    public string? SettlementReference { get; set; }
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Verified usage is mandatory only when the selected asset book uses units of production.
    /// The disposal approval governs this evidence and the resulting final charge together.
    /// </summary>
    public decimal? FinalDepreciationProductionUnits { get; set; }
    public string? FinalDepreciationEvidenceReference { get; set; }
    public string? FinalDepreciationEvidenceNotes { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class ApproveAssetDisposalDto
{
    public string? Comments { get; set; }
}

public class AssetVerificationSessionDto
{
    public Guid Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public VerificationSessionStatus Status { get; set; }
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public int TotalItems { get; set; }
    public int VerifiedItems { get; set; }
}

public class CreateAssetVerificationSessionDto
{
    public string SessionName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string? Description { get; set; }
    public List<Guid>? AssetIds { get; set; } // If null, include all active assets
}

public class AssetVerificationItemDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid FixedAssetId { get; set; }
    public string? FixedAssetName { get; set; }
    public string? AssetCode { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public AssetCondition Condition { get; set; }
    public string? CurrentLocation { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

public class VerifyAssetDto
{
    public AssetCondition Condition { get; set; }
    public string? CurrentLocation { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

// Bulk Import DTOs
public class BulkImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public bool IsDryRun { get; set; }
    public List<BulkImportErrorDto> Errors { get; set; } = new();
    public List<string> SuccessfulAssetCodes { get; set; } = new();
}

public class BulkImportErrorDto
{
    public int RowNumber { get; set; }
    public string? AssetCode { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public class BulkAssetImportRowDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public string? BookCode { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? InstallationCost { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? AccumulatedDepreciation { get; set; }
    public decimal? NetBookValue { get; set; }
    public DateTime? OpeningAsOfDate { get; set; }
    public decimal? OpeningYtdDepreciation { get; set; }
    public int? RemainingUsefulLifeMonths { get; set; }
    public int? UsefulLifeMonths { get; set; }
    public decimal? ResidualValue { get; set; }
    public string? SerialNumber { get; set; }
    public FixedAssetStatus? Status { get; set; }
    public string? RawStatus { get; set; }
}
