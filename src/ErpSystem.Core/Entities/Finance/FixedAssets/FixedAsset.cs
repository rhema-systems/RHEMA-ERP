using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
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

        // --- Tracking ---
        
        public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Draft;
        public DateTime? DisposalDate { get; set; }

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
        public virtual ICollection<AssetDepreciationSchedule> DepreciationSchedules { get; set; } = new List<AssetDepreciationSchedule>();
        public virtual ICollection<AssetTransaction> Transactions { get; set; } = new List<AssetTransaction>();
        public virtual ICollection<AssetValuation> Valuations { get; set; } = new List<AssetValuation>();
    }
}
