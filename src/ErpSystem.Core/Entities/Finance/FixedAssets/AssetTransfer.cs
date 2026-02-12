using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
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

        [Required]
        public AssetTransferType TransferType { get; set; } = AssetTransferType.Internal;

        [Required]
        public AssetTransferStatus Status { get; set; } = AssetTransferStatus.Draft;

        // --- From Details (Historical snapshot at time of request) ---
        [MaxLength(500)]
        public string? FromLocation { get; set; }

        public Guid? FromCustodianId { get; set; }
        public virtual Employee? FromCustodian { get; set; }

        // --- To Details (Target) ---
        [Required]
        [MaxLength(500)]
        public string ToLocation { get; set; } = string.Empty;

        public Guid? ToCustodianId { get; set; }
        public virtual Employee? ToCustodian { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TransferCost { get; set; }

        public Guid? RequestedById { get; set; }
        public virtual Employee? RequestedBy { get; set; }

        public Guid? ApprovedById { get; set; }
        public virtual Employee? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [MaxLength(2000)]
        public string? Comments { get; set; }

        [MaxLength(50)]
        public string? ReferenceNumber { get; set; } // Internal transfer order number
    }
}
