using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    /// <summary>
    /// Immutable record of any event changing the financial value or status of an asset.
    /// Used for Audit Trails and Historical Reporting.
    /// </summary>
    public class AssetTransaction : TenantEntity
    {
        [Required]
        public Guid FixedAssetId { get; set; }
        public virtual FixedAsset FixedAsset { get; set; } = null!;

        public Guid? AccountingBookId { get; set; }
        public virtual AccountingBook? AccountingBook { get; set; }

        [MaxLength(20)]
        public string BookClassification { get; set; } = "IFRS";

        [Required]
        public DateTime TransactionDate { get; set; } // Date the event occurred

        [Required]
        [MaxLength(50)]
        public string TransactionType { get; set; } = string.Empty; 
        // e.g., "Acquisition", "Depreciation", "Revaluation", "Disposal", "Transfer"

        [MaxLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // Impact amount (positive or negative)

        /// <summary>
        /// Asset Net Book Value after this transaction
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ResultingBookValue { get; set; }

        public Guid? RelatedEntityId { get; set; } 
        // ID of related record (e.g., AssetDisposalId, JournalEntryId)

        [Required]
        public Guid PerformedByUserId { get; set; }
    }
}
