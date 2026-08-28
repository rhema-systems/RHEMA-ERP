using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    /// <summary>
    /// Records a revaluation or impairment event for a fixed asset (IAS 16 / IAS 36).
    /// Immutable after GL posting.
    /// </summary>
    public class AssetValuation : TenantEntity
    {
        [Required]
        public Guid FixedAssetId { get; set; }
        public virtual FixedAsset FixedAsset { get; set; } = null!;

        public Guid? AccountingBookId { get; set; }
        public virtual AccountingBook? AccountingBook { get; set; }

        [MaxLength(20)]
        public string BookClassification { get; set; } = "IFRS";

        public Guid? FiscalPeriodId { get; set; }
        public virtual FiscalPeriod? FiscalPeriod { get; set; }

        [Required]
        public DateTime ValuationDate { get; set; }

        public DateTime AccountingDate { get; set; }

        [Required]
        public ValuationType ValuationType { get; set; }

        /// <summary>
        /// Net Book Value before this valuation
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CarryingAmountBefore { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedDepreciationBefore { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBookValueBefore { get; set; }

        /// <summary>
        /// Assessed fair value / recoverable amount
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FairValue { get; set; }

        /// <summary>
        /// Carrying amount after applying the valuation
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CarryingAmountAfter { get; set; }

        // --- Calculated impact ---

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevaluationSurplus { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevaluationDeficit { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ImpairmentLoss { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ImpairmentReversal { get; set; } = 0;

        /// <summary>
        /// Links an IAS 36 reversal to the posted impairment whose remaining balance it releases.
        /// This explicit lineage prevents a reversal from being applied more than once or against
        /// an unrelated asset/book impairment.
        /// </summary>
        public Guid? SourceImpairmentValuationId { get; set; }
        public virtual AssetValuation? SourceImpairmentValuation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingImpairmentBefore { get; set; }

        /// <summary>
        /// Accountant-supported IAS 36 ceiling: the carrying amount that would have existed at
        /// the reversal date had the source impairment never been recognised, net of depreciation.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnimpairedCarryingAmountCap { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AdjustmentAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevaluationSurplusApplied { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevaluationLossRecognized { get; set; } = 0;

        /// <summary>
        /// Revised useful life in months (optional — allows adjusting remaining life on revaluation)
        /// </summary>
        public int? RevisedUsefulLifeMonths { get; set; }

        // These snapshots make a later approved correction deterministic. A correction restores
        // the exact pre-valuation useful-life state rather than guessing from current master data.
        public int UsefulLifeMonthsBefore { get; set; }
        public int? RemainingUsefulLifeMonthsBefore { get; set; }

        // --- Valuation details ---

        [MaxLength(200)]
        public string? ValuerName { get; set; }

        [MaxLength(200)]
        public string? ValuationMethod { get; set; }

        [MaxLength(200)]
        public string? ValuationReportReference { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // --- GL Posting ---

        public bool IsPostedToGL { get; set; } = false;
        public DateTime? PostedDate { get; set; }
        public DateTime? PostedAt { get; set; }
        public Guid? JournalEntryId { get; set; }
        public virtual JournalEntry? JournalEntry { get; set; }

        public Guid? PostingEventId { get; set; }
        public virtual FinancePostingEvent? PostingEvent { get; set; }

        public bool IsCorrected { get; set; }
        public Guid? CorrectionId { get; set; }
        public DateTime? CorrectedAt { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = "Calculated";

        [MaxLength(150)]
        public string? IdempotencyKey { get; set; }

        public DateTime? FailedAt { get; set; }

        [MaxLength(1000)]
        public string? FailureReason { get; set; }

        // --- Audit ---

        [Required]
        public Guid PerformedByUserId { get; set; }
    }
}
