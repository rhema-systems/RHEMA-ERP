using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public static class BusinessPartnerPostingDefaults
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static BusinessPartnerPostingDefaultsDto FromPartner(BusinessPartner partner) => new()
    {
        SubjectToWithholdingDeduction = partner.SubjectToWithholdingDeduction,
        WithholdingTaxRate = partner.WithholdingTaxRate,
        DefaultWithholdingTaxId = partner.DefaultWithholdingTaxId,
        DefaultTaxGroupId = partner.DefaultTaxGroupId,
        DefaultBankAccountId = partner.DefaultBankAccountId,
        CashAccountSource = partner.CashAccountSource,
        DefaultApAccountId = partner.DefaultApAccountId,
        DefaultExpenseAccountId = partner.DefaultExpenseAccountId,
        DefaultCashAccountId = partner.DefaultCashAccountId,
        DefaultTermsDiscountsAvailableAccountId = partner.DefaultTermsDiscountsAvailableAccountId,
        DefaultTermsDiscountsTakenAccountId = partner.DefaultTermsDiscountsTakenAccountId,
        DefaultFinanceChargesAccountId = partner.DefaultFinanceChargesAccountId,
        DefaultTradeDiscountAccountId = partner.DefaultTradeDiscountAccountId,
        DefaultMiscellaneousAccountId = partner.DefaultMiscellaneousAccountId,
        DefaultFreightAccountId = partner.DefaultFreightAccountId,
        DefaultTaxAccountId = partner.DefaultTaxAccountId,
        DefaultWriteoffAccountId = partner.DefaultWriteoffAccountId,
        DefaultAccruedPurchasesAccountId = partner.DefaultAccruedPurchasesAccountId,
        DefaultPurchasePriceVarianceAccountId = partner.DefaultPurchasePriceVarianceAccountId,
    };

    public static void Apply(BusinessPartner partner, BusinessPartnerPostingDefaultsDto defaults)
    {
        partner.SubjectToWithholdingDeduction = defaults.SubjectToWithholdingDeduction;
        partner.WithholdingTaxRate = defaults.WithholdingTaxRate;
        partner.DefaultWithholdingTaxId = defaults.DefaultWithholdingTaxId;
        partner.DefaultTaxGroupId = defaults.DefaultTaxGroupId;
        partner.DefaultBankAccountId = defaults.DefaultBankAccountId;
        partner.CashAccountSource = defaults.CashAccountSource;
        partner.DefaultApAccountId = defaults.DefaultApAccountId;
        partner.DefaultExpenseAccountId = defaults.DefaultExpenseAccountId;
        partner.DefaultCashAccountId = defaults.DefaultCashAccountId;
        partner.DefaultTermsDiscountsAvailableAccountId = defaults.DefaultTermsDiscountsAvailableAccountId;
        partner.DefaultTermsDiscountsTakenAccountId = defaults.DefaultTermsDiscountsTakenAccountId;
        partner.DefaultFinanceChargesAccountId = defaults.DefaultFinanceChargesAccountId;
        partner.DefaultTradeDiscountAccountId = defaults.DefaultTradeDiscountAccountId;
        partner.DefaultMiscellaneousAccountId = defaults.DefaultMiscellaneousAccountId;
        partner.DefaultFreightAccountId = defaults.DefaultFreightAccountId;
        partner.DefaultTaxAccountId = defaults.DefaultTaxAccountId;
        partner.DefaultWriteoffAccountId = defaults.DefaultWriteoffAccountId;
        partner.DefaultAccruedPurchasesAccountId = defaults.DefaultAccruedPurchasesAccountId;
        partner.DefaultPurchasePriceVarianceAccountId = defaults.DefaultPurchasePriceVarianceAccountId;
        if (!partner.SubjectToWithholdingDeduction) partner.WithholdingTaxRate = 0;
    }

    public static PurchaseOrderSupplierDefaultsDto Snapshot(BusinessPartner partner) => new()
    {
        BusinessPartnerId = partner.Id,
        PaymentTermId = partner.PaymentTermId,
        PaymentTerms = partner.PaymentTerms,
        PaymentTermsDays = partner.PaymentTerm?.DueDays,
        Tin = partner.TaxIdentificationNumber,
        CreditLimit = partner.CreditLimit,
        Currency = partner.Currency,
        PostingDefaults = FromPartner(partner)
    };

    public static string SerializeSnapshot(BusinessPartner partner) => JsonSerializer.Serialize(Snapshot(partner), JsonOptions);

    public static PurchaseOrderSupplierDefaultsDto? ReadSnapshot(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PurchaseOrderSupplierDefaultsDto>(json, JsonOptions);
}
