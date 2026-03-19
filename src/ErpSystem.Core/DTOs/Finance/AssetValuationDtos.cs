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
        public int? RevisedUsefulLifeMonths { get; set; }
        public string? ValuerName { get; set; }
        public string? ValuationMethod { get; set; }
        public string? ValuationReportReference { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
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
        public DateTime ValuationDate { get; set; }
        public ValuationType ValuationType { get; set; }
        public decimal CarryingAmountBefore { get; set; }
        public decimal FairValue { get; set; }
        public decimal CarryingAmountAfter { get; set; }
        public decimal RevaluationSurplus { get; set; }
        public decimal RevaluationDeficit { get; set; }
        public decimal ImpairmentLoss { get; set; }
        public decimal ImpairmentReversal { get; set; }
        public int? RevisedUsefulLifeMonths { get; set; }
        public string? ValuerName { get; set; }
        public string? ValuationMethod { get; set; }
        public string? ValuationReportReference { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public bool IsPostedToGL { get; set; }
        public Guid? JournalEntryId { get; set; }
        public DateTime? PostedDate { get; set; }
        public DateTime CreatedAt { get; set; }
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
