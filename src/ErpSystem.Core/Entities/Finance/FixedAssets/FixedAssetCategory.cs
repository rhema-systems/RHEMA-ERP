using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    public class FixedAssetCategory : TenantEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Code { get; set; } = string.Empty; // e.g. "FA-VEH"

        [MaxLength(500)]
        public string? Description { get; set; }

        // --- Default Depreciation Config (Templates for new Assets) ---
        public DepreciationMethod DefaultMethod { get; set; } = DepreciationMethod.StraightLine;
        
        public int DefaultUsefulLifeMonths { get; set; } = 36; // Default 3 years
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultResidualValuePercent { get; set; } = 0; // e.g. 10%

        // --- GL Account Mapping ---
        // These accounts are critical for automating the financial impact of assets in this category.

        [Required]
        public Guid AssetAccountId { get; set; }
        /// <summary>
        /// Checking Account (Balance Sheet) -> Dr when acquired
        /// </summary>
        public virtual Account AssetAccount { get; set; } = null!;

        [Required]
        public Guid AccumulatedDepreciationAccountId { get; set; }
        /// <summary>
        /// Contra Asset Account (Balance Sheet) -> Cr when depreciated
        /// </summary>
        public virtual Account AccumulatedDepreciationAccount { get; set; } = null!;

        [Required]
        public Guid DepreciationExpenseAccountId { get; set; }
        /// <summary>
        /// Expense Account (P&L) -> Dr when depreciated
        /// </summary>
        public virtual Account DepreciationExpenseAccount { get; set; } = null!;

        public Guid? GainOnDisposalAccountId { get; set; }
        public virtual Account? GainOnDisposalAccount { get; set; }

        public Guid? LossOnDisposalAccountId { get; set; }
        public virtual Account? LossOnDisposalAccount { get; set; }

        // --- Navigation ---
        public virtual ICollection<FixedAsset> Assets { get; set; } = new List<FixedAsset>();
    }
}
