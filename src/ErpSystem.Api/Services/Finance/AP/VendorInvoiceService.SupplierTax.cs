using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private async Task ValidateSupplierTaxFallbackAsync(Guid accountId, DateTime invoiceDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = await _unitOfWork.Repository<Account>().FirstOrDefaultAsync(account =>
            account.Id == accountId && account.TenantId == TenantId && !account.IsDeleted);
        if (account == null || account.Status != AccountStatus.Active ||
            (account.AccountType != AccountType.Asset && account.AccountType != AccountType.Liability) ||
            (!account.AllowDirectPosting && !account.IsControlAccount) ||
            (account.EffectiveDate.HasValue && account.EffectiveDate.Value.Date > invoiceDate.Date) ||
            (account.ExpirationDate.HasValue && account.ExpirationDate.Value.Date <= invoiceDate.Date))
            throw new InvalidOperationException("The supplier input-tax fallback must be an active, postable asset or liability account in the current tenant on the invoice date.");
    }
}
