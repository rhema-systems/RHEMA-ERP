using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Procurement;

public static class BusinessPartnerPostingDefaultValidation
{
    public static async Task ValidateAsync(BusinessPartnerPostingDefaultsDto defaults, string partnerType, IUnitOfWork unitOfWork, ICurrentUserProvider currentUser)
    {
        if (currentUser.IsExternalUser)
            throw new UnauthorizedAccessException("Posting defaults are maintained by internal business-partner administrators.");
        var tenantId = currentUser.TenantId;
        if (tenantId == Guid.Empty) throw new UnauthorizedAccessException("Select a tenant before editing posting defaults.");
        if (defaults.SubjectToWithholdingDeduction && (defaults.WithholdingTaxRate <= 0 || defaults.WithholdingTaxRate > 100))
            throw new InvalidOperationException("Enter a WHT rate greater than zero and no more than 100.");
        if (!defaults.SubjectToWithholdingDeduction) defaults.WithholdingTaxRate = 0;
        if (defaults.DefaultWithholdingTaxId.HasValue)
        {
            var withholding = await unitOfWork.Repository<Tax>().GetByIdAsync(defaults.DefaultWithholdingTaxId.Value);
            if (withholding == null || withholding.TenantId != tenantId || withholding.IsDeleted || !withholding.IsActive ||
                withholding.Category != TaxCategory.Withholding ||
                withholding.Applicability is not (TaxApplicability.Purchases or TaxApplicability.Both))
                throw new InvalidOperationException("Select an active purchase WHT rule from the current tenant.");
            if (!withholding.TaxPayableAccountId.HasValue)
                throw new InvalidOperationException("The selected WHT rule has no payable GL account configured.");
            await ValidatePostingAccountAsync(unitOfWork, currentUser, withholding.TaxPayableAccountId,
                "WHT payable", true, AccountType.Liability);
        }
        if (defaults.CashAccountSource is not ("Chequebook" or "BusinessPartner"))
            throw new InvalidOperationException("Select Chequebook or BusinessPartner as the cash account source.");

        if (defaults.DefaultTaxGroupId.HasValue)
        {
            var group = await unitOfWork.Repository<TaxGroup>().GetByIdAsync(defaults.DefaultTaxGroupId.Value);
            var applicability = partnerType.Equals("Customer", StringComparison.OrdinalIgnoreCase)
                ? TaxApplicability.Sales : TaxApplicability.Purchases;
            if (group == null || group.TenantId != tenantId || group.IsDeleted || !group.IsActive ||
                (group.Applicability != TaxApplicability.Both && group.Applicability != applicability))
                throw new InvalidOperationException("Select an active tax schedule applicable to this business partner in the current tenant.");
        }
        if (defaults.DefaultBankAccountId.HasValue)
        {
            var bank = await unitOfWork.Repository<BankAccount>().GetByIdAsync(defaults.DefaultBankAccountId.Value);
            if (bank == null || bank.TenantId != tenantId || bank.IsDeleted || !bank.IsActive)
                throw new InvalidOperationException("Select an active ChequeBook ID from the current tenant.");
        }

        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultApAccountId, "Accounts Payable", true, AccountType.Liability);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultAccruedPurchasesAccountId, "Accrued Purchases", true, AccountType.Liability);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultCashAccountId, "Cash", false, AccountType.Asset);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultExpenseAccountId, "Purchases", false, AccountType.Asset, AccountType.Expense);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultTaxAccountId, "Tax", false, AccountType.Asset, AccountType.Liability, AccountType.Expense);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultPurchasePriceVarianceAccountId, "Purchase Price Variance", false, AccountType.Expense);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultTermsDiscountsAvailableAccountId, "Terms Discounts Available", false);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultTermsDiscountsTakenAccountId, "Terms Discounts Taken", false);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultFinanceChargesAccountId, "Finance Charges", false);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultTradeDiscountAccountId, "Trade Discount", false);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultMiscellaneousAccountId, "Miscellaneous", false);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultFreightAccountId, "Freight", false);
        await ValidatePostingAccountAsync(unitOfWork, currentUser, defaults.DefaultWriteoffAccountId, "Writeoffs", false);
    }

    private static async Task ValidatePostingAccountAsync(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser, Guid? accountId, string purpose, bool allowControl, params AccountType[] types)
    {
        if (!accountId.HasValue) return;
        var account = await unitOfWork.Accounts.GetByIdAsync(accountId.Value);
        if (account == null || account.TenantId != currentUser.TenantId ||
            account.IsDeleted || account.Status != AccountStatus.Active ||
            (account.EffectiveDate.HasValue && account.EffectiveDate > DateTime.UtcNow) ||
            (account.ExpirationDate.HasValue && account.ExpirationDate <= DateTime.UtcNow) ||
            (!account.AllowDirectPosting && !(allowControl && account.IsControlAccount)) ||
            (!allowControl && account.IsControlAccount) ||
            (types.Length > 0 && !types.Contains(account.AccountType)))
            throw new InvalidOperationException($"Select an active, eligible {purpose} GL account from the current tenant.");
    }
}
