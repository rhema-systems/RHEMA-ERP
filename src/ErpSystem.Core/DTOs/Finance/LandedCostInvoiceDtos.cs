using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class CreateLandedCostInvoicesDto
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequireAllVoucherCharges { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UseSavedBillingDetails { get; set; }
    public DateTime InvoiceDate { get; set; }
    public List<LandedCostInvoiceChargeDto> Charges { get; set; } = new();
}

public sealed class LandedCostInvoiceChargeDto
{
    public Guid CostItemId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public TaxTreatment? TaxTreatment { get; set; }
    public Guid? TaxGroupId { get; set; }
}

public sealed class PostLandedCostDto
{
    public DateTime InvoiceDate { get; set; }
    public List<LandedCostBillingChargeDto> Charges { get; set; } = new();
}

public sealed class LandedCostBillingChargeDto
{
    public Guid CostItemId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
}

public sealed class PostLandedCostResultDto
{
    public bool InventoryPosted { get; set; }
    public bool InvoicesPending { get; set; }
    public string? Message { get; set; }
    public List<VendorInvoiceDto> Invoices { get; set; } = new();
}
