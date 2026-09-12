using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class CreateInventoryReturnCreditDto
{
    public Guid OriginalVendorInvoiceId { get; set; }
    [Required, MaxLength(100)] public string SupplierCreditNoteReference { get; set; } = string.Empty;
    public DateTime CreditDate { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public sealed class InventoryReturnCreditSourceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? SupplierInvoiceNumber { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal OutstandingAmount { get; set; }
}

public sealed class UpdateInventoryReturnCreditHeaderDto
{
    [Required, MaxLength(100)] public string SupplierCreditNoteReference { get; set; } = string.Empty;
    public DateTime CreditDate { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
