using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.MobilePos;

public sealed class MobilePosStoreUpsertDto
{
    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public MobilePosStoreStatus Status { get; set; } = MobilePosStoreStatus.Draft;
    public Guid? CompanyProfileId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? WarehouseId { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Required, MaxLength(100)]
    public string TimeZoneId { get; set; } = "Africa/Accra";

    public Guid DefaultWalkInBusinessPartnerId { get; set; }
    public Guid DefaultWalkInBusinessPartnerRoleId { get; set; }
    public Guid? OfflinePolicyId { get; set; }
    public string? Notes { get; set; }
    public string? RowVersion { get; set; }
    public List<MobilePosStoreDimensionDefaultInputDto> DimensionDefaults { get; set; } = [];
}

public sealed class MobilePosStoreDimensionDefaultInputDto
{
    public Guid FinanceDimensionDefinitionId { get; set; }
    public Guid FinanceDimensionValueId { get; set; }
}

public sealed class MobilePosStoreDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MobilePosStoreStatus Status { get; set; }
    public Guid? CompanyProfileId { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public Guid DefaultWalkInBusinessPartnerId { get; set; }
    public Guid DefaultWalkInBusinessPartnerRoleId { get; set; }
    public string DefaultWalkInCustomerCode { get; set; } = string.Empty;
    public string DefaultWalkInCustomerName { get; set; } = string.Empty;
    public Guid? OfflinePolicyId { get; set; }
    public string? OfflinePolicyName { get; set; }
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<MobilePosStoreDimensionDefaultDto> DimensionDefaults { get; set; } = [];
}

public sealed class MobilePosStoreDimensionDefaultDto
{
    public Guid FinanceDimensionDefinitionId { get; set; }
    public string DimensionCode { get; set; } = string.Empty;
    public Guid FinanceDimensionValueId { get; set; }
    public string ValueCode { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
}

public sealed class MobilePosOfflinePolicyUpsertDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    [Range(15, 1440)]
    public int AuthorizationWindowMinutes { get; set; } = 480;
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal? MaximumTransactionAmount { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal? MaximumAggregateAmount { get; set; }
    [Range(1, 100000)]
    public int? MaximumTransactionCount { get; set; }
    [Range(15, 1440)]
    public int MaximumOfflineAgeMinutes { get; set; } = 480;
    public bool AllowCashSale { get; set; } = true;
    public bool AllowCashReceipt { get; set; } = true;
    public bool AllowPartialPayment { get; set; } = true;
    public bool AllowReturns { get; set; }
    public bool AllowReversals { get; set; }
    public bool AllowProvisionalReceipt { get; set; } = true;
    public bool AllowDayEndSubmissionWithPendingSync { get; set; }
    public bool RequireExternalReferenceForElectronicTender { get; set; } = true;
    public string? RowVersion { get; set; }
}

public sealed class MobilePosOfflinePolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int AuthorizationWindowMinutes { get; set; }
    public decimal? MaximumTransactionAmount { get; set; }
    public decimal? MaximumAggregateAmount { get; set; }
    public int? MaximumTransactionCount { get; set; }
    public int MaximumOfflineAgeMinutes { get; set; }
    public bool AllowCashSale { get; set; }
    public bool AllowCashReceipt { get; set; }
    public bool AllowPartialPayment { get; set; }
    public bool AllowReturns { get; set; }
    public bool AllowReversals { get; set; }
    public bool AllowProvisionalReceipt { get; set; }
    public bool AllowDayEndSubmissionWithPendingSync { get; set; }
    public bool RequireExternalReferenceForElectronicTender { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class MobilePosTillUpsertDto
{
    public Guid MobilePosStoreId { get; set; }
    [Required, MaxLength(50)]
    public string TillNumber { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public MobilePosTillStatus Status { get; set; } = MobilePosTillStatus.Draft;
    public Guid LiquidityAccountId { get; set; }
    public string? Notes { get; set; }
    public string? RowVersion { get; set; }
    public List<MobilePosTillPaymentMethodInputDto> PaymentMethods { get; set; } = [];
}

public sealed class MobilePosTillPaymentMethodInputDto
{
    public Guid PaymentMethodId { get; set; }
    public bool AllowOnline { get; set; } = true;
    public bool AllowOffline { get; set; }
    public bool RequireExternalAuthorizationReference { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class MobilePosTillDto
{
    public Guid Id { get; set; }
    public Guid MobilePosStoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string TillNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MobilePosTillStatus Status { get; set; }
    public Guid LiquidityAccountId { get; set; }
    public string LiquidityAccountCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<MobilePosPaymentMethodDto> PaymentMethods { get; set; } = [];
}

public sealed class MobilePosPaymentMethodDto
{
    public Guid PaymentMethodId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool RequiresBankAccount { get; set; }
    public bool RequiresReference { get; set; }
    public bool AllowOnline { get; set; }
    public bool AllowOffline { get; set; }
    public bool RequireExternalAuthorizationReference { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class MobilePosUserStoreAssignmentUpsertDto
{
    public Guid UserId { get; set; }
    public Guid MobilePosStoreId { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class MobilePosDeviceEnrollmentRequestDto
{
    [Required, StringLength(200, MinimumLength = 16)]
    public string InstallationId { get; set; } = string.Empty;
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
    [MaxLength(128)]
    public string? PublicKeyThumbprint { get; set; }
    [MaxLength(50)]
    public string? PrinterAdapterKey { get; set; }
    [MaxLength(50)]
    public string? ScannerAdapterKey { get; set; }
}

public sealed class MobilePosDeviceApprovalDto
{
    public Guid MobilePosStoreId { get; set; }
    public Guid MobilePosTillId { get; set; }
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class MobilePosDeviceStatusChangeDto
{
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class MobilePosHeartbeatDto
{
    [Required, StringLength(200, MinimumLength = 16)]
    public string InstallationId { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? AppVersion { get; set; }
    [MaxLength(50)]
    public string? OperatingSystemVersion { get; set; }
    [MaxLength(50)]
    public string? PrinterAdapterKey { get; set; }
    [MaxLength(50)]
    public string? ScannerAdapterKey { get; set; }
    public DateTime? LastSyncAtUtc { get; set; }
}

public sealed class MobilePosOfflineGrantRequestDto
{
    [Required, StringLength(200, MinimumLength = 16)]
    public string InstallationId { get; set; } = string.Empty;
}

public sealed class MobilePosCustomerSearchResultDto
{
    public Guid BusinessPartnerId { get; set; }
    public Guid BusinessPartnerRoleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsDefaultWalkInCustomer { get; set; }
}

public sealed class MobilePosOfflinePaymentMethodSnapshotDto
{
    public Guid PaymentMethodId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool RequiresReference { get; set; }
    public bool RequireExternalAuthorizationReference { get; set; }
}

public sealed class MobilePosOfflineGrantPolicySnapshotDto
{
    public Guid PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public DateTime PolicyVersionUtc { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid DefaultWalkInBusinessPartnerId { get; set; }
    public Guid DefaultWalkInBusinessPartnerRoleId { get; set; }
    public decimal? MaximumTransactionAmount { get; set; }
    public decimal? MaximumAggregateAmount { get; set; }
    public int? MaximumTransactionCount { get; set; }
    public int MaximumOfflineAgeMinutes { get; set; }
    public bool AllowPartialPayment { get; set; }
    public bool AllowProvisionalReceipt { get; set; }
    public bool AllowDayEndSubmissionWithPendingSync { get; set; }
    public IReadOnlyList<string> AllowedCommandTypes { get; set; } = [];
    public IReadOnlyList<MobilePosOfflinePaymentMethodSnapshotDto> AllowedPaymentMethods { get; set; } = [];
}

public sealed class MobilePosOfflineGrantDto
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid MobilePosDeviceId { get; set; }
    public Guid MobilePosStoreId { get; set; }
    public Guid MobilePosTillId { get; set; }
    public Guid CashierTillSessionId { get; set; }
    public Guid MobilePosOfflinePolicyId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public long RevocationEpoch { get; set; }
    public string PolicySnapshotHash { get; set; } = string.Empty;
    public MobilePosOfflineGrantPolicySnapshotDto Policy { get; set; } = new();
}

public sealed class MobilePosDeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? OperatingSystemVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? PrinterAdapterKey { get; set; }
    public string? ScannerAdapterKey { get; set; }
    public MobilePosDeviceStatus Status { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid? MobilePosStoreId { get; set; }
    public string? StoreName { get; set; }
    public Guid? MobilePosTillId { get; set; }
    public string? TillNumber { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? StatusReason { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastSyncAtUtc { get; set; }
    public long RevocationEpoch { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class MobilePosBootstrapDto
{
    public string EnvironmentName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public MobilePosDeviceDto Device { get; set; } = new();
    public MobilePosStoreDto Store { get; set; } = new();
    public MobilePosTillDto Till { get; set; } = new();
    public MobilePosOfflinePolicyDto? OfflinePolicy { get; set; }
    public Guid? CurrentTillSessionId { get; set; }
    public DateTime ServerTimeUtc { get; set; }
}

public sealed class MobilePosReferenceOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Secondary { get; set; }
}

public sealed class MobilePosCustomerReferenceDto
{
    public Guid BusinessPartnerId { get; set; }
    public Guid BusinessPartnerRoleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class MobilePosDimensionReferenceDto
{
    public Guid DefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<MobilePosReferenceOptionDto> Values { get; set; } = [];
}

public sealed class MobilePosAdministrationReferencesDto
{
    public IReadOnlyList<MobilePosCustomerReferenceDto> Customers { get; set; } = [];
    public IReadOnlyList<MobilePosReferenceOptionDto> Locations { get; set; } = [];
    public IReadOnlyList<MobilePosReferenceOptionDto> Warehouses { get; set; } = [];
    public IReadOnlyList<MobilePosReferenceOptionDto> CompanyProfiles { get; set; } = [];
    public IReadOnlyList<MobilePosReferenceOptionDto> CashTills { get; set; } = [];
    public IReadOnlyList<MobilePosReferenceOptionDto> PaymentMethods { get; set; } = [];
    public IReadOnlyList<MobilePosReferenceOptionDto> Users { get; set; } = [];
    public IReadOnlyList<MobilePosDimensionReferenceDto> Dimensions { get; set; } = [];
}

public sealed class MobilePosCompleteSaleRequestDto
{
    [Required, MaxLength(200)]
    public string InstallationId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ClientMutationId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LocalReference { get; set; } = string.Empty;

    public Guid? BusinessPartnerId { get; set; }
    public Guid? BusinessPartnerRoleId { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public decimal ExpectedSubTotal { get; set; }
    public decimal ExpectedTaxAmount { get; set; }
    public decimal ExpectedDiscountAmount { get; set; }
    public decimal ExpectedTotalAmount { get; set; }
    public List<MobilePosSaleLineInputDto> Lines { get; set; } = [];
    public List<MobilePosTenderInputDto> Tenders { get; set; } = [];
}

public sealed class MobilePosSaleLineInputDto
{
    public Guid ClientLineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public Guid? TaxGroupId { get; set; }
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;
}

public sealed class MobilePosTenderInputDto
{
    public Guid PaymentMethodId { get; set; }
    public decimal Amount { get; set; }

    [MaxLength(150)]
    public string? ExternalReference { get; set; }

    public Guid? LiquidityAccountId { get; set; }
    public Guid? BankAccountId { get; set; }
}

public sealed class MobilePosSaleResultDto
{
    public Guid SaleId { get; set; }
    public Guid MutationReceiptId { get; set; }
    public bool IsReplay { get; set; }
    public string LocalReference { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceStatus { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<MobilePosSaleTenderResultDto> Tenders { get; set; } = [];
}

public sealed class MobilePosSaleTenderResultDto
{
    public Guid TenderId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public Guid CustomerPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class MobilePosReceiptReprintRequestDto
{
    [Required, StringLength(200, MinimumLength = 16)]
    public string InstallationId { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string ClientEventId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class MobilePosReceiptDto
{
    public Guid ReceiptId { get; set; }
    public string CopyType { get; set; } = "ORIGINAL";
    public int CopyNumber { get; set; }
    public int ReprintCount { get; set; }
    public Guid? AuditEventId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string? ReprintReason { get; set; }
    public string QrReference { get; set; } = string.Empty;

    public Guid TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    public Guid StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public Guid TillId { get; set; }
    public string TillNumber { get; set; } = string.Empty;
    public string TillName { get; set; } = string.Empty;
    public Guid TillSessionId { get; set; }
    public string TillSessionNumber { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;

    public Guid CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public Guid BusinessPartnerRoleId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool UsedStoreDefaultCustomer { get; set; }

    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceStatus { get; set; } = string.Empty;
    public string LocalReference { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<MobilePosReceiptLineDto> Lines { get; set; } = [];
    public IReadOnlyList<MobilePosReceiptTenderDto> Tenders { get; set; } = [];
}

public sealed class MobilePosReceiptLineDto
{
    public int Sequence { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
}

public sealed class MobilePosReceiptTenderDto
{
    public int Sequence { get; set; }
    public string PaymentMethodCode { get; set; } = string.Empty;
    public string PaymentMethodName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ExternalReference { get; set; }
    public Guid CustomerPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class MobilePosCatalogueItemDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Barcode { get; set; }
    public string? AlternateBarcode { get; set; }
    public string? QrCode { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public Guid? UnitOfMeasureId { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid? DefaultTaxGroupId { get; set; }
    public decimal? AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

public sealed class MobilePosSalePreviewRequestDto
{
    [Required, MaxLength(200)]
    public string InstallationId { get; set; } = string.Empty;

    public Guid? BusinessPartnerId { get; set; }
    public Guid? BusinessPartnerRoleId { get; set; }
    public List<MobilePosSalePreviewLineInputDto> Lines { get; set; } = [];
}

public sealed class MobilePosSalePreviewLineInputDto
{
    public Guid ClientLineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal DiscountPercentage { get; set; }
}

public sealed class MobilePosSalePreviewDto
{
    public Guid BusinessPartnerId { get; set; }
    public Guid BusinessPartnerRoleId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool UsedStoreDefaultCustomer { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int CurrencyDecimalPlaces { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public IReadOnlyList<MobilePosSalePreviewLineDto> Lines { get; set; } = [];
}

public sealed class MobilePosSalePreviewLineDto
{
    public Guid ClientLineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public Guid? TaxGroupId { get; set; }
    public TaxTreatment TaxTreatment { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
}
