using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance; // For MaintenanceAsset integration
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    public class FixedAsset : TenantEntity
    {
        [Required]
        [MaxLength(50)]
        public string AssetCode { get; set; } = string.Empty; // e.g., FA-0001

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Location { get; set; }

        public Guid? CurrentCustodianId { get; set; }
        public virtual Employee? CurrentCustodian { get; set; }

        [MaxLength(500)]
        public string? CurrentSegmentString { get; set; }

        public Guid? CurrentSegmentLookupValueId { get; set; }
        public virtual SegmentLookupValue? CurrentSegmentLookupValue { get; set; }

        [Required]
        public Guid FixedAssetCategoryId { get; set; }
        public virtual FixedAssetCategory Category { get; set; } = null!;

        // --- Financial Details ---

        [Required]
        public DateTime PurchaseDate { get; set; }

        public DateTime? PlacedInServiceDate { get; set; } // Depreciation starts here

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InstallationCost { get; set; } = 0; // Capitalized costs

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;

        public DateTime? CapitalizationDate { get; set; }

        /// <summary>
        /// Total capitalized value (Basis for depreciation)
        /// PurchasePrice + Installation + etc.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AcquisitionCost { get; set; }

        /// <summary>
        /// Current Book Value after total accumulated depreciation
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBookValue { get; set; }

        // --- Depreciation Configuration ---
        
        public DepreciationMethod DepreciationMethod { get; set; }
        public DepreciationConvention DepreciationConvention { get; set; } = DepreciationConvention.FullMonth;

        public int UsefulLifeMonths { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal ResidualValue { get; set; } // Scrap value at end of life

        /// <summary>
        /// Approved annual rate for diminishing-balance depreciation. Double-declining balance can
        /// leave this at zero to use the transparent 200% / useful-life calculation.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal DiminishingBalanceRatePercent { get; set; }

        /// <summary>
        /// Estimated lifetime output used only by units-of-production depreciation.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal LifetimeProductionCapacity { get; set; }

        /// <summary>
        /// Default-book usage snapshot. Book-specific accumulated usage remains authoritative in
        /// FixedAssetBookValue; this field supports the register and operator workspace.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal AccumulatedProductionUnits { get; set; }

        // --- Tracking ---
        
        public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Draft;
        public DateTime? DisposalDate { get; set; }

        [MaxLength(3)]
        public string FunctionalCurrencyCode { get; set; } = "GHS";

        [MaxLength(3)]
        public string? TransactionCurrencyCode { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? ExchangeRate { get; set; }

        public Guid? ExchangeRateId { get; set; }

        public DateTime? ExchangeRateDate { get; set; }

        [MaxLength(50)]
        public string? SourceDocumentType { get; set; }

        public Guid? SourceDocumentId { get; set; }

        public Guid? SourceDocumentLineId { get; set; }

        public Guid? JournalEntryId { get; set; }

        public Guid? PostingEventId { get; set; }

        public DateTime? CapitalizedAt { get; set; }

        /// <summary>
        /// Canonical Finance-owned proposal reviewed by the maker-checker workflow before a
        /// direct capitalization is allowed to post. The JSON and hash deliberately remain on
        /// the asset after posting so the approval can be reconciled to the resulting journal.
        /// Procurement-owned capitalization uses its dedicated handoff evidence instead.
        /// </summary>
        public string? CapitalizationApprovalSnapshotJson { get; set; }

        [MaxLength(64)]
        public string? CapitalizationApprovalSnapshotHash { get; set; }

        public Guid? CapitalizationApprovalExchangeRateId { get; set; }
        public Guid? CapitalizationApprovalWorkflowInstanceId { get; set; }
        public Guid? CapitalizationApprovalSubmittedByUserId { get; set; }
        public DateTime? CapitalizationApprovalSubmittedAt { get; set; }
        public Guid? CapitalizationApprovalApprovedByUserId { get; set; }
        public DateTime? CapitalizationApprovalApprovedAt { get; set; }
        public DateTime? CapitalizationApprovalInvalidatedAt { get; set; }

        [MaxLength(1000)]
        public string? CapitalizationApprovalInvalidationReason { get; set; }

        /// <summary>
        /// The latest compensating Finance event, when the current capitalization was reversed.
        /// Original capitalization IDs deliberately remain above so register-to-GL lineage is not
        /// destroyed; a later capitalization replaces the current-cycle fields while historical
        /// request and AssetTransaction rows retain every earlier cycle.
        /// </summary>
        public Guid? CapitalizationReversalJournalEntryId { get; set; }
        public Guid? CapitalizationReversalPostingEventId { get; set; }
        public DateTime? CapitalizationReversedAt { get; set; }

        [MaxLength(1000)]
        public string? CapitalizationReversalReason { get; set; }

        // --- Integration (Link to Operations/Maintenance) ---
        
        public Guid? MaintenanceAssetId { get; set; }
        /// <summary>
        /// Link to the physical asset/equipment record in Operations module
        /// (One-to-One relationship)
        /// </summary>
        public virtual MaintenanceAsset? MaintenanceAsset { get; set; }

        [MaxLength(100)]
        public string? SerialNumber { get; set; } // Redundant if linked, but useful if standalone

        // --- Navigation ---
        public virtual ICollection<FixedAssetBookValue> BookValues { get; set; } = new List<FixedAssetBookValue>();
        public virtual ICollection<AssetDepreciationSchedule> DepreciationSchedules { get; set; } = new List<AssetDepreciationSchedule>();
        public virtual ICollection<AssetTransaction> Transactions { get; set; } = new List<AssetTransaction>();
        public virtual ICollection<AssetValuation> Valuations { get; set; } = new List<AssetValuation>();
        public virtual ICollection<FixedAssetCapitalizationReversal> CapitalizationReversals { get; set; } = new List<FixedAssetCapitalizationReversal>();
    }
}
