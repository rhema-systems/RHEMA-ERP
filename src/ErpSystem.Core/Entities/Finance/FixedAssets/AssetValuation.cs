using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
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

        [Required]
        public DateTime ValuationDate { get; set; }

        [Required]
        public ValuationType ValuationType { get; set; }

        /// <summary>
        /// Net Book Value before this valuation
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CarryingAmountBefore { get; set; }

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
        /// Revised useful life in months (optional — allows adjusting remaining life on revaluation)
        /// </summary>
        public int? RevisedUsefulLifeMonths { get; set; }

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
        public Guid? JournalEntryId { get; set; }

        // --- Audit ---

        [Required]
        public Guid PerformedByUserId { get; set; }
    }
}
