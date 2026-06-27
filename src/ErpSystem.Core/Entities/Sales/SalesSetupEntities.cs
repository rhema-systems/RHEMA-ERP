using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Sales;

/// <summary>
/// Tenant configuration for inventory/property sources that Sales can sell, lease, or reserve.
/// </summary>
public class SalesSaleableSource : TenantEntity
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

    public bool IsSystemSource { get; set; }

    public string? SettingsJson { get; set; }
}
