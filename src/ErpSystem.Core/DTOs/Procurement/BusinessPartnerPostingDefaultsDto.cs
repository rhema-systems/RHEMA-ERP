using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>Optional posting defaults; null account IDs retain the configured Finance fallback.</summary>
public sealed class BusinessPartnerPostingDefaultsDto
{
    public bool SubjectToWithholdingDeduction { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal WithholdingTaxRate { get; set; }
    public Guid? DefaultWithholdingTaxId { get; set; }
    public Guid? DefaultTaxGroupId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }
    [RegularExpression("^(Chequebook|BusinessPartner)$")]
    public string CashAccountSource { get; set; } = "Chequebook";
    public Guid? DefaultApAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public Guid? DefaultCashAccountId { get; set; }
    public Guid? DefaultTermsDiscountsAvailableAccountId { get; set; }
    public Guid? DefaultTermsDiscountsTakenAccountId { get; set; }
    public Guid? DefaultFinanceChargesAccountId { get; set; }
    public Guid? DefaultTradeDiscountAccountId { get; set; }
    public Guid? DefaultMiscellaneousAccountId { get; set; }
    public Guid? DefaultFreightAccountId { get; set; }
    public Guid? DefaultTaxAccountId { get; set; }
    public Guid? DefaultWriteoffAccountId { get; set; }
    public Guid? DefaultAccruedPurchasesAccountId { get; set; }
    public Guid? DefaultPurchasePriceVarianceAccountId { get; set; }
}

/// <summary>Defaults copied when a PO is created or its supplier changes; never a live master-data lookup.</summary>
public sealed class PurchaseOrderSupplierDefaultsDto
{
    public Guid BusinessPartnerId { get; set; }
    public Guid? PaymentTermId { get; set; }
    public string? PaymentTerms { get; set; }
    public int? PaymentTermsDays { get; set; }
    public string? Tin { get; set; }
    public decimal? CreditLimit { get; set; }
    public string? Currency { get; set; }
    public BusinessPartnerPostingDefaultsDto PostingDefaults { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupplierWithholdingDefaultDto? WithholdingDefault { get; set; }
}

/// <summary>Read-only AP calculation context; never persisted as a purchase-order tax decision.</summary>
public sealed class SupplierWithholdingDefaultDto
{
    public bool Required { get; set; }
    public Guid? TaxId { get; set; }
    public decimal Rate { get; set; }
    public Guid? TaxPayableAccountId { get; set; }
    public string? Message { get; set; }
}
