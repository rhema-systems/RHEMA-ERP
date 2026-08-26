using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Services.Finance.Cash;

/// <summary>
/// Produces the single canonical current cash-position DTO from posted functional-currency GL
/// balances. Native bank currencies remain grouping labels; they are never arithmetically mixed.
/// </summary>
public sealed class CashPositionReportService : ICashPositionReportService
{
    private readonly IBankAccountService _bankAccountService;
    private readonly IGeneralLedgerService _generalLedgerService;
    private readonly ITenantSettingsService _tenantSettingsService;

    public CashPositionReportService(
        IBankAccountService bankAccountService,
        IGeneralLedgerService generalLedgerService,
        ITenantSettingsService tenantSettingsService)
    {
        _bankAccountService = bankAccountService;
        _generalLedgerService = generalLedgerService;
        _tenantSettingsService = tenantSettingsService;
    }

    public async Task<CashPositionSummaryDto> GetCurrentPositionAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = (await _bankAccountService.GetActiveAccountsAsync()).ToList();
        var baseCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
        var asOfDate = DateTime.UtcNow.Date;
        var ledger = await _generalLedgerService.GenerateCashBankLedgerAsync(new CashBankLedgerRequestDto
        {
            StartDate = asOfDate,
            EndDate = asOfDate
        });
        var ledgerBalances = ledger.Accounts.ToDictionary(
            account => account.BankAccountId,
            account => account.ClosingBalance);

        return new CashPositionSummaryDto
        {
            AsOfDate = asOfDate,
            TotalBalance = ledger.TotalClosingBalance,
            Currency = baseCurrency,
            AccountCount = accounts.Count,
            ByAccountType = accounts
                .GroupBy(account => account.AccountType)
                .Select(group => new CashPositionByAccountTypeDto
                {
                    Type = group.Key,
                    Balance = group.Sum(account => ledgerBalances.GetValueOrDefault(account.Id)),
                    Count = group.Count()
                })
                .OrderBy(item => item.Type.ToString())
                .ToList(),
            ByCurrency = accounts
                .GroupBy(account => NormalizeCurrency(account.Currency))
                .Select(group => new CashPositionByCurrencyDto
                {
                    Currency = group.Key,
                    Balance = group.Sum(account => ledgerBalances.GetValueOrDefault(account.Id)),
                    Count = group.Count()
                })
                .OrderBy(item => item.Currency)
                .ToList()
        };
    }

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "GHS" : currency.Trim().ToUpperInvariant();
}
