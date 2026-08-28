using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    /// <summary>
    /// Records the transfer of a fixed asset between locations, departments, or custodians.
    /// </summary>
    public class AssetTransfer : TenantEntity
    {
        [Required]
        public Guid FixedAssetId { get; set; }
        public virtual FixedAsset FixedAsset { get; set; } = null!;

        [Required]
        public DateTime TransferDate { get; set; }

        public DateTime? AccountingDate { get; set; }

        public Guid? FiscalPeriodId { get; set; }
        public virtual FiscalPeriod? FiscalPeriod { get; set; }

        [Required]
        public AssetTransferType TransferType { get; set; } = AssetTransferType.Internal;

        [Required]
        public AssetTransferStatus Status { get; set; } = AssetTransferStatus.Draft;

        // --- From Details (Historical snapshot at time of request) ---
        [MaxLength(500)]
        public string? FromLocation { get; set; }

        public Guid? FromCustodianId { get; set; }
        public virtual Employee? FromCustodian { get; set; }

        [MaxLength(500)]
        public string? FromSegmentString { get; set; }

        public Guid? FromSegmentLookupValueId { get; set; }
        public virtual SegmentLookupValue? FromSegmentLookupValue { get; set; }

        // --- To Details (Target) ---
        [Required]
        [MaxLength(500)]
        public string ToLocation { get; set; } = string.Empty;

        public Guid? ToCustodianId { get; set; }
        public virtual Employee? ToCustodian { get; set; }

        [MaxLength(500)]
        public string? ToSegmentString { get; set; }

        public Guid? ToSegmentLookupValueId { get; set; }
        public virtual SegmentLookupValue? ToSegmentLookupValue { get; set; }

        // GL reclassification transfers snapshot both categories. The asset's live category is
        // changed only after the compensating reclassification journal posts successfully, while
        // these references preserve what the maker requested even if category setup later changes.
        public Guid? FromFixedAssetCategoryId { get; set; }
        public virtual FixedAssetCategory? FromFixedAssetCategory { get; set; }

        public Guid? ToFixedAssetCategoryId { get; set; }
        public virtual FixedAssetCategory? ToFixedAssetCategory { get; set; }

        public Guid? AccountingBookId { get; set; }
        public virtual AccountingBook? AccountingBook { get; set; }

        [MaxLength(20)]
        public string BookClassification { get; set; } = "IFRS";

        // Account IDs and balances are immutable posting evidence. Rebuilding a pending request
        // from mutable category configuration could otherwise move a different balance than the
        // independent checker approved.
        public Guid? FromAssetAccountId { get; set; }
        public Guid? ToAssetAccountId { get; set; }
        public Guid? FromAccumulatedDepreciationAccountId { get; set; }
        public Guid? ToAccumulatedDepreciationAccountId { get; set; }
        public Guid? FromAccumulatedImpairmentAccountId { get; set; }
        public Guid? ToAccumulatedImpairmentAccountId { get; set; }
        public Guid? FromRevaluationSurplusAccountId { get; set; }
        public Guid? ToRevaluationSurplusAccountId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReclassificationAssetCarryingAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReclassificationAccumulatedDepreciation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReclassificationAccumulatedImpairment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReclassificationRevaluationSurplus { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TransferCost { get; set; }

        public Guid? RequestedById { get; set; }
        public virtual Employee? RequestedBy { get; set; }

        public Guid? ApprovedById { get; set; }
        public virtual Employee? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? PostedAt { get; set; }

        public DateTime? FailedAt { get; set; }

        [MaxLength(2000)]
        public string? Comments { get; set; }

        [MaxLength(1000)]
        public string? FailureReason { get; set; }

        [MaxLength(50)]
        public string? ReferenceNumber { get; set; } // Internal transfer order number

        [MaxLength(150)]
        public string? IdempotencyKey { get; set; }

        public Guid? WorkflowInstanceId { get; set; }

        public Guid? JournalEntryId { get; set; }
        public virtual JournalEntry? JournalEntry { get; set; }

        public Guid? PostingEventId { get; set; }
        public virtual FinancePostingEvent? PostingEvent { get; set; }
    }
}
