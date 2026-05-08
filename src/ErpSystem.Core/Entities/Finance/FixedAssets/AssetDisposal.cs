using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Finance.FixedAssets
{
    public class AssetDisposal : TenantEntity
    {
        [Required]
        public Guid FixedAssetId { get; set; }
        public virtual FixedAsset FixedAsset { get; set; } = null!;

        [Required]
        public DateTime DisposalDate { get; set; }

        [Required]
        public DisposalType DisposalType { get; set; } = DisposalType.Sale;

        [Required]
        public AssetDisposalStatus Status { get; set; } = AssetDisposalStatus.Draft;

        [MaxLength(1000)]
        public string? Reason { get; set; }

        // --- Financial Impact ---

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaleProceeds { get; set; } = 0; // Amount received (Cash/AR)

        [Column(TypeName = "decimal(18,2)")]
        public decimal DisposalCost { get; set; } = 0; // Costs to sell/remove

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBookValueAtDisposal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GainOrLoss { get; set; } 
        // Calculated: (SaleProceeds - DisposalCost) - NetBookValueAtDisposal

        [MaxLength(100)]
        public string? BuyerName { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        public Guid? RequestedById { get; set; }
        public virtual Employee? RequestedBy { get; set; }

        public Guid? ApprovedById { get; set; }
        public virtual Employee? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [MaxLength(1000)]
        public string? Comments { get; set; }

        public Guid? JournalEntryId { get; set; }
        public virtual JournalEntry? JournalEntry { get; set; }
    }
}
