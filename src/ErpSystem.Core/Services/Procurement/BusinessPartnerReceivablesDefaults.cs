using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public static class BusinessPartnerReceivablesDefaults
{
    public static BusinessPartnerReceivablesDefaultsDto FromPartner(BusinessPartner partner) => new()
    {
        DefaultArAccountId = partner.DefaultArAccountId,
        SalesAccountId = partner.CustomerSalesAccountId,
        CostOfSalesAccountId = partner.CustomerCostOfSalesAccountId,
        InventoryAccountId = partner.CustomerInventoryAccountId,
        TermsDiscountsTakenAccountId = partner.CustomerTermsDiscountsTakenAccountId,
        SalesReturnsAccountId = partner.CustomerSalesReturnsAccountId,
        FinanceChargesAccountId = partner.CustomerFinanceChargesAccountId,
        WriteoffAccountId = partner.CustomerWriteoffAccountId,
        OverpaymentWriteoffAccountId = partner.CustomerOverpaymentWriteoffAccountId
    };

    public static void Apply(BusinessPartner partner, BusinessPartnerReceivablesDefaultsDto value)
    {
        if (value.ProvidedFields.Contains(nameof(value.DefaultArAccountId))) partner.DefaultArAccountId = value.DefaultArAccountId;
        // Preserve newly introduced mappings when an older client submits only AR control.
        if (value.ProvidedFields.Contains(nameof(value.SalesAccountId))) partner.CustomerSalesAccountId = value.SalesAccountId;
        if (value.ProvidedFields.Contains(nameof(value.CostOfSalesAccountId))) partner.CustomerCostOfSalesAccountId = value.CostOfSalesAccountId;
        if (value.ProvidedFields.Contains(nameof(value.InventoryAccountId))) partner.CustomerInventoryAccountId = value.InventoryAccountId;
        if (value.ProvidedFields.Contains(nameof(value.TermsDiscountsTakenAccountId))) partner.CustomerTermsDiscountsTakenAccountId = value.TermsDiscountsTakenAccountId;
        if (value.ProvidedFields.Contains(nameof(value.SalesReturnsAccountId))) partner.CustomerSalesReturnsAccountId = value.SalesReturnsAccountId;
        if (value.ProvidedFields.Contains(nameof(value.FinanceChargesAccountId))) partner.CustomerFinanceChargesAccountId = value.FinanceChargesAccountId;
        if (value.ProvidedFields.Contains(nameof(value.WriteoffAccountId))) partner.CustomerWriteoffAccountId = value.WriteoffAccountId;
        if (value.ProvidedFields.Contains(nameof(value.OverpaymentWriteoffAccountId))) partner.CustomerOverpaymentWriteoffAccountId = value.OverpaymentWriteoffAccountId;
    }
}
