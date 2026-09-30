using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Sales;

public class SalesSaleableSourceDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string AdapterKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Icon { get; set; } = "Package";
    public string ColorCode { get; set; } = "#2563EB";
    public int SortOrder { get; set; }
    public string SupportedTransactionTypes { get; set; } = "SalesOrder";
    public string DefaultCurrency { get; set; } = "GHS";
    public string? DefaultWorkflowEntityType { get; set; }
    public bool AllowSalesOrders { get; set; }
    public bool AllowSalesAgreements { get; set; }
    public bool AllowReservations { get; set; }
    public bool RequiresExternalModule { get; set; }
    public bool IsSystemSource { get; set; }
    public string? SettingsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class SalesSaleableSourceAdapterDefinitionDto
{
    public string AdapterKey { get; set; } = string.Empty;
    public IReadOnlyCollection<SalesSaleableSourceFilterDefinitionDto> Filters { get; set; }
        = Array.Empty<SalesSaleableSourceFilterDefinitionDto>();
}

public sealed class SalesSaleableSourceFilterDefinitionDto
{
    public string Field { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ValueType { get; set; } = "text";
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? HelpText { get; set; }
    public IReadOnlyCollection<string> Options { get; set; } = Array.Empty<string>();
}

public class SalesSaleableItemDto
{
    public Guid SourceId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string AdapterKey { get; set; } = string.Empty;
    public string SourceItemId { get; set; } = string.Empty;
    public string? ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? Status { get; set; }
    public string? CommercialStatus { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal? EstimatedValue { get; set; }
    public string? Currency { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public string? PropertyReference { get; set; }
    public bool CanCreateSalesOrder { get; set; }
    public bool CanCreateSalesAgreement { get; set; }
    public bool CanCreateLeaseAgreement { get; set; }
    public string? SuggestedOrderType { get; set; }
    public string? SuggestedAgreementType { get; set; }
    public string? SuggestedLeaseAgreementType { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectTitle { get; set; }
    public Guid? ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string? ProjectUnitName { get; set; }
    public string? HandoverStatus { get; set; }
    public Guid? InventoryItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal? CurrentQuantity { get; set; }
    public decimal? AvailableQuantity { get; set; }
    public decimal? AllocatedQuantity { get; set; }
    public bool ShouldCreateSalesAllocation { get; set; } = true;
    public Guid? ActiveAllocationId { get; set; }
    public string? ActiveAllocationStatus { get; set; }
    public DateTime? ActiveAllocationReservedUntil { get; set; }
    public string? ActiveAllocationCustomerName { get; set; }
    public bool HasActiveAllocation { get; set; }
}

public class UpsertSalesSaleableSourceDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(80)]
    public string SourceType { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string AdapterKey { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [MaxLength(80)]
    public string Icon { get; set; } = "Package";

    [MaxLength(20)]
    public string ColorCode { get; set; } = "#2563EB";

    public int SortOrder { get; set; } = 10;

    [MaxLength(300)]
    public string SupportedTransactionTypes { get; set; } = "SalesOrder";

    [MaxLength(10)]
    public string DefaultCurrency { get; set; } = "GHS";

    [MaxLength(80)]
    public string? DefaultWorkflowEntityType { get; set; }

    public bool AllowSalesOrders { get; set; } = true;

    public bool AllowSalesAgreements { get; set; } = false;

    public bool AllowReservations { get; set; } = true;

    public bool RequiresExternalModule { get; set; }

    public string? SettingsJson { get; set; }
}
