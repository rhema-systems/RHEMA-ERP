using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed partial class ProcurementReceiptInspectionService
{
    private sealed record ReceiptCostRate(Guid? Id, string PurchaseCurrency, string FunctionalCurrency, decimal Rate, DateTime Date);

    private async Task<ReceiptCostRate> ResolveReceiptCostRateAsync(PurchaseOrderReceipt receipt, CancellationToken ct)
    {
        var settings = await _unitOfWork.Repository<FinanceSettings>().GetQueryable(x =>
            x.TenantId == _currentUser.TenantId && !x.IsDeleted).AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw Validation("RCV_FUNCTIONAL_CURRENCY_REQUIRED", "Configure the Finance functional currency before accepting stock.");
        var functional = settings.BaseCurrency?.Trim().ToUpperInvariant();
        var purchase = receipt.PurchaseOrder.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(functional) || string.IsNullOrWhiteSpace(purchase))
            throw Validation("RCV_COST_CURRENCY_REQUIRED", "The purchase order and Finance functional currencies are required.");
        if (purchase == functional) return new(null, purchase, functional, 1m, receipt.ReceiptDate.Date);

        var selected = await ProcurementReceiptExchangeRatePolicy.ResolveAsync(_unitOfWork.Repository<ExchangeRate>().GetQueryable(),
            settings, purchase, receipt.ReceiptDate, ct)
            ?? throw Validation("RCV_COST_CURRENCY_REQUIRED", "Foreign receipt rate evidence is required.");
        selected.HasBeenUsedInTransactions = true;
        selected.TransactionCount++;
        selected.FirstUsedDate ??= DateTime.UtcNow;
        selected.LastUsedDate = DateTime.UtcNow;
        await _unitOfWork.Repository<ExchangeRate>().UpdateAsync(selected);
        return new(selected.Id, purchase, functional, selected.InverseRate, selected.EffectiveDate.Date);
    }
}
