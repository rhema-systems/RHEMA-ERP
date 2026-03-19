using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Finance.FixedAssets;

public class AssetVerificationSession : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string SessionName { get; set; } = string.Empty;

    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [Required]
    public VerificationSessionStatus Status { get; set; } = VerificationSessionStatus.Draft;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public Guid? VerifiedById { get; set; }
    public virtual Employee? VerifiedBy { get; set; }

    public virtual ICollection<AssetVerificationItem> Items { get; set; } = new List<AssetVerificationItem>();
}

public class AssetVerificationItem : TenantEntity
{
    public Guid SessionId { get; set; }
    public virtual AssetVerificationSession Session { get; set; } = null!;

    public Guid FixedAssetId { get; set; }
    public virtual FixedAsset FixedAsset { get; set; } = null!;

    public bool IsVerified { get; set; } = false;
    public DateTime? VerificationDate { get; set; }

    public AssetCondition Condition { get; set; } = AssetCondition.Good;

    [MaxLength(200)]
    public string? CurrentLocation { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public string? ImageUrl { get; set; }
}
