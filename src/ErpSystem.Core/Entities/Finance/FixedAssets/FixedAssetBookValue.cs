using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    /// <summary>
    /// Book-specific carrying values for a fixed asset. This is the source of truth
    /// for fixed asset cost, depreciation reserve, and NBV in parallel accounting books.
    /// </summary>
    public class FixedAssetBookValue : TenantEntity
    {
        [Required]
        public Guid FixedAssetId { get; set; }
        public virtual FixedAsset FixedAsset { get; set; } = null!;

        [Required]
        public Guid AccountingBookId { get; set; }
        public virtual AccountingBook AccountingBook { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string BookClassification { get; set; } = "IFRS";

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AcquisitionCost { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedDepreciation { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBookValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ResidualValue { get; set; }

        public int UsefulLifeMonths { get; set; }

        public int? RemainingUsefulLifeMonths { get; set; }

        public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;

        public DepreciationConvention DepreciationConvention { get; set; } = DepreciationConvention.FullMonth;

        public DateTime? PlacedInServiceDate { get; set; }

        public DateTime? OpeningAsOfDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningYtdDepreciation { get; set; }

        public DateTime? LastDepreciationDate { get; set; }

        public Guid? OpeningJournalEntryId { get; set; }

        public bool OpeningPostedToGl { get; set; }

        public DateTime? OpeningPostedDate { get; set; }

        [MaxLength(50)]
        public string OpeningSource { get; set; } = "Manual";

        public DateTime? CapitalizationDate { get; set; }

        public Guid? CapitalizationJournalEntryId { get; set; }

        public Guid? CapitalizationPostingEventId { get; set; }

        // Current-cycle reversal lineage is retained beside the original capitalization evidence.
        public Guid? CapitalizationReversalJournalEntryId { get; set; }
        public Guid? CapitalizationReversalPostingEventId { get; set; }
        public DateTime? CapitalizationReversedAt { get; set; }

        [MaxLength(50)]
        public string? SourceDocumentType { get; set; }

        public Guid? SourceDocumentId { get; set; }

        public Guid? SourceDocumentLineId { get; set; }
    }
}
