using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>The same approved purchase-side rate supplies receipt preview and actual acceptance.</summary>
public static class ProcurementReceiptExchangeRatePolicy
{
    public static async Task<ExchangeRate?> ResolveAsync(IQueryable<ExchangeRate> rates, FinanceSettings settings,
        string? purchaseCurrency, DateTime receiptDate, CancellationToken ct = default)
    {
        var functional = settings.BaseCurrency?.Trim().ToUpperInvariant();
        var purchase = purchaseCurrency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(functional) || string.IsNullOrWhiteSpace(purchase))
            throw new InvalidOperationException("RCV_COST_CURRENCY_REQUIRED: configure the purchase order and Finance functional currencies.");
        if (purchase == functional) return null;
        var side = settings.DirectionalExchangeRatePolicyEnabled ? settings.ApInvoiceQuoteSide : ExchangeRateQuoteSide.Mid;
        var date = receiptDate.Date;
        var candidates = await rates.Where(x => x.TenantId == settings.TenantId && !x.IsDeleted && x.IsActive &&
            (x.ApprovalStatus == RateApprovalStatus.Approved || x.ApprovalStatus == RateApprovalStatus.AutoApproved) &&
            x.BaseCurrencyCode == functional && x.TargetCurrencyCode == purchase && x.RateType == ExchangeRateType.Daily &&
            x.QuoteSide == side && x.Rate > 0 && x.InverseRate > 0 && x.EffectiveDate.Date <= date &&
            (!x.EndDate.HasValue || x.EndDate.Value.Date >= date))
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.Priority).ToListAsync(ct);
        var selected = candidates.FirstOrDefault() ?? throw new InvalidOperationException(
            "RCV_APPROVED_EXCHANGE_RATE_REQUIRED: an approved, effective purchase exchange rate is required for this receipt.");
        if (candidates.Count(x => x.EffectiveDate == selected.EffectiveDate && x.Priority == selected.Priority) != 1)
            throw new InvalidOperationException("RCV_EXCHANGE_RATE_AMBIGUOUS: Finance must resolve equally preferred receipt rates.");
        return selected;
    }
}
