using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance
{
    // ========== Valuation DTOs ==========

    /// <summary>
    /// DTO for creating a single asset revaluation or impairment
    /// </summary>
    public class CreateAssetValuationDto
    {
        public required Guid FixedAssetId { get; set; }
        public required DateTime ValuationDate { get; set; }
        public required ValuationType ValuationType { get; set; }
        public required decimal FairValue { get; set; }
        /// <summary>
        /// Required for an impairment reversal. Identifies the posted impairment whose remaining
        /// balance is being released, rather than allowing an unlinked carrying-value increase.
        /// </summary>
        public Guid? SourceImpairmentValuationId { get; set; }
        /// <summary>
        /// IAS 36 carrying-value ceiling supported by the accountant's depreciation roll-forward.
        /// </summary>
        public decimal? UnimpairedCarryingAmountCap { get; set; }
        public int? RevisedUsefulLifeMonths { get; set; }
        public string? ValuerName { get; set; }
        public string? ValuationMethod { get; set; }
        public string? ValuationReportReference { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
    }

    /// <summary>
    /// DTO for creating a bulk revaluation using an index percentage
    /// </summary>
    public class CreateBulkAssetValuationDto
    {
        public required List<Guid> FixedAssetIds { get; set; } = new();
        public required DateTime ValuationDate { get; set; }
        public required ValuationType ValuationType { get; set; }

        /// <summary>
        /// The percentage to adjust the NBV (e.g., 5.0 for 5% increase, -10.0 for 10% decrease)
        /// </summary>
        public required decimal IndexPercentage { get; set; }

        public string? ValuerName { get; set; }
        public string? ValuationMethod { get; set; }
        public string? ValuationReportReference { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
    }

    /// <summary>
    /// DTO for asset valuation response
    /// </summary>
    public class AssetValuationDto
    {
        public Guid Id { get; set; }
        public Guid FixedAssetId { get; set; }
        public string? AssetCode { get; set; }
        public string? AssetName { get; set; }
        public Guid? AccountingBookId { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public Guid? FiscalPeriodId { get; set; }
        public DateTime ValuationDate { get; set; }
        public DateTime AccountingDate { get; set; }
        public ValuationType ValuationType { get; set; }
        public decimal CarryingAmountBefore { get; set; }
        public decimal AccumulatedDepreciationBefore { get; set; }
        public decimal NetBookValueBefore { get; set; }
        public decimal FairValue { get; set; }
        public decimal CarryingAmountAfter { get; set; }
        public decimal RevaluationSurplus { get; set; }
        public decimal RevaluationDeficit { get; set; }
        public decimal ImpairmentLoss { get; set; }
        public decimal ImpairmentReversal { get; set; }
        public Guid? SourceImpairmentValuationId { get; set; }
        public decimal OutstandingImpairmentBefore { get; set; }
        public decimal UnimpairedCarryingAmountCap { get; set; }
        public decimal AdjustmentAmount { get; set; }
        public decimal RevaluationSurplusApplied { get; set; }
        public decimal RevaluationLossRecognized { get; set; }
        public int? RevisedUsefulLifeMonths { get; set; }
        public int UsefulLifeMonthsBefore { get; set; }
        public int? RemainingUsefulLifeMonthsBefore { get; set; }
        public string? ValuerName { get; set; }
        public string? ValuationMethod { get; set; }
        public string? ValuationReportReference { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public bool IsPostedToGL { get; set; }
        public Guid? JournalEntryId { get; set; }
        public Guid? PostingEventId { get; set; }
        public bool IsCorrected { get; set; }
        public Guid? CorrectionId { get; set; }
        public DateTime? CorrectedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? IdempotencyKey { get; set; }
        public DateTime? PostedDate { get; set; }
        public DateTime? PostedAt { get; set; }
        public DateTime? FailedAt { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
    }

    public sealed class RequestAssetValuationCorrectionDto
    {
        public string Reason { get; set; } = string.Empty;
        public string ImpactAssessment { get; set; } = string.Empty;
        public DateTime? ReversalDate { get; set; }
    }

    public sealed class ReviewAssetValuationCorrectionDto
    {
        public bool Approved { get; set; }
        public string ReviewComment { get; set; } = string.Empty;
    }

    public sealed class AssetValuationCorrectionDto
    {
        public Guid Id { get; set; }
        public Guid OriginalValuationId { get; set; }
        public Guid FixedAssetId { get; set; }
        public string? AssetCode { get; set; }
        public string ValuationType { get; set; } = string.Empty;
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
        public Guid OriginalPostingEventId { get; set; }
        public Guid OriginalJournalEntryId { get; set; }
        public Guid? ReversalPostingEventId { get; set; }
        public Guid? ReversalJournalEntryId { get; set; }
        public DateTime? PostedAt { get; set; }
        public string? FailureReason { get; set; }
    }

    /// <summary>
    /// Generic result for bulk operations (transfers, disposals, revaluations)
    /// </summary>
    public class BulkOperationResultDto<T>
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<T> SuccessfulItems { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
