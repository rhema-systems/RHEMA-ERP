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

/// <summary>
/// Server-side source and audit envelope for one Mobile POS sale. Finance Invoice remains the
/// accounting document; this row binds the canonical result to its store, till, device, cashier,
/// customer, client mutation, and policy evidence.
/// </summary>
public sealed class MobilePosSale : TenantEntity
{
    public Guid MobilePosStoreId { get; set; }
    public MobilePosStore MobilePosStore { get; set; } = null!;

    public Guid MobilePosTillId { get; set; }
    public MobilePosTill MobilePosTill { get; set; } = null!;

    public Guid CashierTillSessionId { get; set; }
    public CashierTillSession CashierTillSession { get; set; } = null!;

    public Guid MobilePosDeviceId { get; set; }
    public MobilePosDevice MobilePosDevice { get; set; } = null!;

    public Guid OperatorUserId { get; set; }
    public ApplicationUser OperatorUser { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ClientMutationId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LocalReference { get; set; } = string.Empty;

    public Guid BusinessPartnerId { get; set; }
    public BusinessPartner BusinessPartner { get; set; } = null!;

    public Guid BusinessPartnerRoleId { get; set; }
    public BusinessPartnerRole BusinessPartnerRole { get; set; } = null!;

    public bool UsedStoreDefaultCustomer { get; set; }

    public DateTime BusinessDate { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(20,4)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal TotalAmount { get; set; }

    public MobilePosSaleStatus Status { get; set; } = MobilePosSaleStatus.Pending;

    public Guid? MobilePosOfflineGrantId { get; set; }
    public MobilePosOfflineGrant? MobilePosOfflineGrant { get; set; }

    [MaxLength(64)]
    public string? OfflinePolicySnapshotHash { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [MaxLength(50)]
    public string? InvoiceNumber { get; set; }

    public DateTime? SynchronizedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<MobilePosSaleLine> Lines { get; set; } = new List<MobilePosSaleLine>();
    public ICollection<MobilePosTender> Tenders { get; set; } = new List<MobilePosTender>();
}

/// <summary>
/// Immutable commercial source evidence submitted for one Mobile POS sale line. Canonical Finance
/// and Inventory services still validate and calculate the authoritative document values.
/// </summary>
public sealed class MobilePosSaleLine : TenantEntity
{
    public Guid MobilePosSaleId { get; set; }
    public MobilePosSale MobilePosSale { get; set; } = null!;

    public Guid ClientLineId { get; set; }
    public int Sequence { get; set; }

    public Guid? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal LineTotal { get; set; }

    public Guid? TaxGroupId { get; set; }
    public TaxGroup? TaxGroup { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    [MaxLength(20)]
    public string? UnitOfMeasureCode { get; set; }
}

/// <summary>
/// One tender in a Mobile POS sale. Each completed tender links to one canonical CustomerPayment;
/// split tender is represented by multiple rows committed by the sale orchestrator.
/// </summary>
public sealed class MobilePosTender : TenantEntity
{
    public Guid MobilePosSaleId { get; set; }
    public MobilePosSale MobilePosSale { get; set; } = null!;

    public int Sequence { get; set; }

    public Guid PaymentMethodId { get; set; }
    public ErpSystem.Core.Entities.Finance.PaymentMethod PaymentMethod { get; set; } = null!;

    [Column(TypeName = "decimal(20,4)")]
    public decimal Amount { get; set; }

    [MaxLength(150)]
    public string? ExternalReference { get; set; }

    public Guid? LiquidityAccountId { get; set; }
    public LiquidityAccount? LiquidityAccount { get; set; }

    public Guid? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public Guid? CustomerPaymentId { get; set; }
    public CustomerPayment? CustomerPayment { get; set; }

    [MaxLength(50)]
    public string? PaymentNumber { get; set; }

    [MaxLength(50)]
    public string? ProviderStatus { get; set; }

    [MaxLength(150)]
    public string? ProviderReference { get; set; }

    public bool WasRecordedOffline { get; set; }
    public MobilePosTenderStatus Status { get; set; } = MobilePosTenderStatus.Pending;
}

/// <summary>
/// Server-side source and audit envelope for a Mobile POS receipt collected against one or more
/// existing AR invoices. CustomerPayment and PaymentAllocation remain the accounting authority.
/// </summary>
public sealed class MobilePosCollection : TenantEntity
{
    public Guid MobilePosStoreId { get; set; }
    public MobilePosStore MobilePosStore { get; set; } = null!;

    public Guid MobilePosTillId { get; set; }
    public MobilePosTill MobilePosTill { get; set; } = null!;

    public Guid CashierTillSessionId { get; set; }
    public CashierTillSession CashierTillSession { get; set; } = null!;

    public Guid MobilePosDeviceId { get; set; }
    public MobilePosDevice MobilePosDevice { get; set; } = null!;

    public Guid OperatorUserId { get; set; }
    public ApplicationUser OperatorUser { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ClientMutationId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LocalReference { get; set; } = string.Empty;

    public Guid BusinessPartnerId { get; set; }
    public BusinessPartner BusinessPartner { get; set; } = null!;

    public Guid BusinessPartnerRoleId { get; set; }
    public BusinessPartnerRole BusinessPartnerRole { get; set; } = null!;

    public DateTime BusinessDate { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(20,4)")]
    public decimal TotalAmount { get; set; }

    public MobilePosCollectionStatus Status { get; set; } = MobilePosCollectionStatus.Pending;

    public Guid? MobilePosOfflineGrantId { get; set; }
    public MobilePosOfflineGrant? MobilePosOfflineGrant { get; set; }

    [MaxLength(64)]
    public string? OfflinePolicySnapshotHash { get; set; }

    public DateTime? SynchronizedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<MobilePosCollectionAllocation> Allocations { get; set; } =
        new List<MobilePosCollectionAllocation>();
    public ICollection<MobilePosCollectionTender> Tenders { get; set; } =
        new List<MobilePosCollectionTender>();
}

/// <summary>Immutable invoice allocation requested and completed by one Mobile POS collection.</summary>
public sealed class MobilePosCollectionAllocation : TenantEntity
{
    public Guid MobilePosCollectionId { get; set; }
    public MobilePosCollection MobilePosCollection { get; set; } = null!;

    public int Sequence { get; set; }
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(20,4)")]
    public decimal Amount { get; set; }
}

/// <summary>
/// One tender in a Mobile POS collection. Each row links to one canonical CustomerPayment whose
/// Finance allocations identify the invoices settled by that tender.
/// </summary>
public sealed class MobilePosCollectionTender : TenantEntity
{
    public Guid MobilePosCollectionId { get; set; }
    public MobilePosCollection MobilePosCollection { get; set; } = null!;

    public int Sequence { get; set; }
    public Guid PaymentMethodId { get; set; }
    public ErpSystem.Core.Entities.Finance.PaymentMethod PaymentMethod { get; set; } = null!;

    [Column(TypeName = "decimal(20,4)")]
    public decimal Amount { get; set; }

    [MaxLength(150)]
    public string? ExternalReference { get; set; }

    public Guid? LiquidityAccountId { get; set; }
    public LiquidityAccount? LiquidityAccount { get; set; }
    public Guid? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public Guid CustomerPaymentId { get; set; }
    public CustomerPayment CustomerPayment { get; set; } = null!;

    [Required, MaxLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PaymentStatus { get; set; }

    public bool WasRecordedOffline { get; set; }
    public MobilePosTenderStatus Status { get; set; } = MobilePosTenderStatus.Pending;
}

/// <summary>
/// Durable server idempotency decision for one device/client mutation pair. A completed or rejected
/// request is replayed only when the command type, schema version, and server-computed request hash
/// match exactly.
/// </summary>
public sealed class MobileMutationReceipt : TenantEntity
{
    public Guid MobilePosDeviceId { get; set; }
    public MobilePosDevice MobilePosDevice { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ClientMutationId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string CommandType { get; set; } = string.Empty;

    public int SchemaVersion { get; set; }

    [Required, MaxLength(64)]
    public string RequestHash { get; set; } = string.Empty;

    public MobileMutationReceiptStatus Status { get; set; } = MobileMutationReceiptStatus.Processing;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime LastAttemptAtUtc { get; set; }
    public int ReplayCount { get; set; }

    public string? ResultJson { get; set; }

    [MaxLength(64)]
    public string? ResultHash { get; set; }

    [MaxLength(100)]
    public string? ErrorCode { get; set; }

    [MaxLength(1000)]
    public string? ErrorDetail { get; set; }

    public Guid? MobilePosSaleId { get; set; }
    public MobilePosSale? MobilePosSale { get; set; }

    public Guid? MobilePosCollectionId { get; set; }
    public MobilePosCollection? MobilePosCollection { get; set; }

    public Guid? CanonicalInvoiceId { get; set; }
    public Invoice? CanonicalInvoice { get; set; }

    public string? CanonicalCustomerPaymentIdsJson { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Mobile day-end evidence attached to the canonical Finance cashier session. Counted cash remains
/// on CashierTillSession; this record preserves device, policy and unresolved local-work evidence
/// needed to prevent final approval while Mobile POS work is ambiguous.
/// </summary>
public sealed class MobilePosTillCloseSubmission : TenantEntity
{
    public Guid CashierTillSessionId { get; set; }
    public CashierTillSession CashierTillSession { get; set; } = null!;

    public Guid MobilePosStoreId { get; set; }
    public MobilePosStore MobilePosStore { get; set; } = null!;

    public Guid MobilePosTillId { get; set; }
    public MobilePosTill MobilePosTill { get; set; } = null!;

    public Guid MobilePosDeviceId { get; set; }
    public MobilePosDevice MobilePosDevice { get; set; } = null!;

    public Guid? MobilePosOfflinePolicyId { get; set; }
    public MobilePosOfflinePolicy? MobilePosOfflinePolicy { get; set; }

    public Guid SubmittedByUserId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public bool PolicyAllowedPendingSync { get; set; }
    public int PendingMutationCount { get; set; }

    [Required]
    public string PendingMutationIdsJson { get; set; } = "[]";

    [Required, MaxLength(64)]
    public string PendingMutationDigest { get; set; } = string.Empty;

    public MobilePosTillCloseSubmissionStatus Status { get; set; } =
        MobilePosTillCloseSubmissionStatus.ReadyForReview;

    public DateTime? SyncExceptionResolvedAtUtc { get; set; }
    public Guid? SyncExceptionResolvedByUserId { get; set; }

    [MaxLength(1000)]
    public string? SyncExceptionResolutionReason { get; set; }

    public DateTime? FinalizedAtUtc { get; set; }
    public Guid? FinalizedByUserId { get; set; }

    public Guid? BankDepositBatchId { get; set; }
    public BankDepositBatch? BankDepositBatch { get; set; }
    public DateTime? BankDepositProposedAtUtc { get; set; }
    public Guid? BankDepositProposedByUserId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
