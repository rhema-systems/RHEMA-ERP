using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.MobilePos;

/// <summary>
/// Tenant-owned sales/collection policy aggregate. HR Location remains the operating-site master,
/// Warehouse remains the stock master, and Finance dimensions remain the accounting authority.
/// </summary>
public sealed class MobilePosStore : TenantEntity
{
    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public MobilePosStoreStatus Status { get; set; } = MobilePosStoreStatus.Draft;

    public Guid? CompanyProfileId { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }

    [Required]
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Required, MaxLength(100)]
    public string TimeZoneId { get; set; } = "Africa/Accra";

    [Required]
    public Guid DefaultWalkInBusinessPartnerId { get; set; }
    public BusinessPartner DefaultWalkInBusinessPartner { get; set; } = null!;

    [Required]
    public Guid DefaultWalkInBusinessPartnerRoleId { get; set; }
    public BusinessPartnerRole DefaultWalkInBusinessPartnerRole { get; set; } = null!;

    public Guid? OfflinePolicyId { get; set; }
    public MobilePosOfflinePolicy? OfflinePolicy { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<MobilePosStoreDimensionDefault> DimensionDefaults { get; set; } =
        new List<MobilePosStoreDimensionDefault>();
    public ICollection<MobilePosTill> Tills { get; set; } = new List<MobilePosTill>();
}

/// <summary>One default Finance dimension value inherited by Mobile POS source documents.</summary>
public sealed class MobilePosStoreDimensionDefault : TenantEntity
{
    public Guid MobilePosStoreId { get; set; }
    public MobilePosStore MobilePosStore { get; set; } = null!;

    public Guid FinanceDimensionDefinitionId { get; set; }
    public FinanceDimensionDefinition FinanceDimensionDefinition { get; set; } = null!;

    public Guid FinanceDimensionValueId { get; set; }
    public FinanceDimensionValue FinanceDimensionValue { get; set; } = null!;
}

/// <summary>A physical collection station. The referenced CashTill account owns cash custody.</summary>
public sealed class MobilePosTill : TenantEntity
{
    public Guid MobilePosStoreId { get; set; }
    public MobilePosStore MobilePosStore { get; set; } = null!;

    [Required, MaxLength(50)]
    public string TillNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public MobilePosTillStatus Status { get; set; } = MobilePosTillStatus.Draft;

    public Guid LiquidityAccountId { get; set; }
    public LiquidityAccount LiquidityAccount { get; set; } = null!;

    public DateTime? LastActivatedAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<MobilePosTillPaymentMethod> PaymentMethods { get; set; } =
        new List<MobilePosTillPaymentMethod>();
}

/// <summary>Configures one HQ-owned payment method for one till.</summary>
public sealed class MobilePosTillPaymentMethod : TenantEntity
{
    public Guid MobilePosTillId { get; set; }
    public MobilePosTill MobilePosTill { get; set; } = null!;

    public Guid PaymentMethodId { get; set; }
    public ErpSystem.Core.Entities.Finance.PaymentMethod PaymentMethod { get; set; } = null!;

    public bool AllowOnline { get; set; } = true;
    public bool AllowOffline { get; set; }
    public bool RequireExternalAuthorizationReference { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Effective-dated assignment. Only one active assignment is permitted per tenant/user.</summary>
public sealed class MobilePosUserStoreAssignment : TenantEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid MobilePosStoreId { get; set; }
    public MobilePosStore MobilePosStore { get; set; } = null!;

    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Tenant-configured bounds copied into every issued offline authorization grant.</summary>
public sealed class MobilePosOfflinePolicy : TenantEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int AuthorizationWindowMinutes { get; set; } = 480;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaximumTransactionAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaximumAggregateAmount { get; set; }

    public int? MaximumTransactionCount { get; set; }
    public int MaximumOfflineAgeMinutes { get; set; } = 480;

    public bool AllowCashSale { get; set; } = true;
    public bool AllowCashReceipt { get; set; } = true;
    public bool AllowPartialPayment { get; set; } = true;
    public bool AllowReturns { get; set; }
    public bool AllowReversals { get; set; }
    public bool AllowProvisionalReceipt { get; set; } = true;
    public bool AllowDayEndSubmissionWithPendingSync { get; set; }
    public bool RequireExternalReferenceForElectronicTender { get; set; } = true;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Persisted mobile installation enrollment and current HQ assignment.</summary>
public sealed class MobilePosDevice : TenantEntity
{
    [Required, MaxLength(64)]
    public string InstallationIdHash { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? PublicKeyThumbprint { get; set; }

    [Required, MaxLength(100)]
    public string DeviceName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(50)]
    public string? OperatingSystemVersion { get; set; }

    [MaxLength(50)]
    public string? AppVersion { get; set; }

    [MaxLength(50)]
    public string? PrinterAdapterKey { get; set; }

    [MaxLength(50)]
    public string? ScannerAdapterKey { get; set; }

    public MobilePosDeviceStatus Status { get; set; } = MobilePosDeviceStatus.Pending;

    public Guid RequestedByUserId { get; set; }
    public ApplicationUser RequestedByUser { get; set; } = null!;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? MobilePosStoreId { get; set; }
    public MobilePosStore? MobilePosStore { get; set; }

    public Guid? MobilePosTillId { get; set; }
    public MobilePosTill? MobilePosTill { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? RevokedByUserId { get; set; }
    public ApplicationUser? RevokedByUser { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    [MaxLength(1000)]
    public string? StatusReason { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastSyncAtUtc { get; set; }
    public long RevocationEpoch { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Immutable assignment history used to explain which device represented a till.</summary>
public sealed class MobilePosDeviceAssignmentHistory : TenantEntity
{
    public Guid MobilePosDeviceId { get; set; }
    public MobilePosDevice MobilePosDevice { get; set; } = null!;
    public Guid? PreviousStoreId { get; set; }
    public Guid? PreviousTillId { get; set; }
    public Guid? NewStoreId { get; set; }
    public Guid? NewTillId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Server record of a signed, bounded authorization to accept offline mutations.</summary>
public sealed class MobilePosOfflineGrant : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid MobilePosDeviceId { get; set; }
    public MobilePosDevice MobilePosDevice { get; set; } = null!;
    public Guid MobilePosStoreId { get; set; }
    public Guid MobilePosTillId { get; set; }
    public Guid CashierTillSessionId { get; set; }
    public Guid MobilePosOfflinePolicyId { get; set; }
    public MobilePosOfflinePolicy MobilePosOfflinePolicy { get; set; } = null!;

    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public MobilePosOfflineGrantStatus Status { get; set; } = MobilePosOfflineGrantStatus.Active;
    public long RevocationEpoch { get; set; }

    [Required]
    public string PolicySnapshotJson { get; set; } = "{}";

    [Required, MaxLength(64)]
    public string PolicySnapshotHash { get; set; } = string.Empty;

    public DateTime? RevokedAtUtc { get; set; }
    public Guid? RevokedByUserId { get; set; }

    [MaxLength(1000)]
    public string? RevocationReason { get; set; }
}
