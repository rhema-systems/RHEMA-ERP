using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Sales;

public sealed class GenerateSalesOrderInvoiceRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public Guid? BusinessPartnerRoleId { get; set; }
    [Required, MinLength(1)] public List<SalesOrderInvoiceLineInput> Lines { get; set; } = new();
    public Guid? FreightAccountId { get; set; }
    public Guid? FreightTaxGroupId { get; set; }
    public TaxTreatment? FreightTaxTreatment { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public sealed class SalesOrderInvoiceLineInput
{
    public Guid SalesOrderLineId { get; set; }
    public Guid? GLAccountId { get; set; }
    public Guid? TaxGroupId { get; set; }
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.PendingReview;
}

public sealed class SalesOrderInvoiceDetailDto
{
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = string.Empty;
    public InvoiceDto Invoice { get; set; } = new();
    public bool CanSubmit { get; set; }
    public bool CanPost { get; set; }
}
