using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/finance/cash-reports")]
public class CashReportsController : ControllerBase
{
    private readonly IBankAccountService _bankAccountService;
    private readonly IGeneralLedgerService _generalLedgerService;
    private readonly ITenantSettingsService _tenantSettingsService;

    public CashReportsController(
        IBankAccountService bankAccountService,
        IGeneralLedgerService generalLedgerService,
        ITenantSettingsService tenantSettingsService)
    {
        _bankAccountService = bankAccountService;
        _generalLedgerService = generalLedgerService;
        _tenantSettingsService = tenantSettingsService;
    }

    [HttpGet("position")]
    public async Task<ActionResult<CashPositionSummaryDto>> GetPosition(CancellationToken cancellationToken)
    {
        var accounts = (await _bankAccountService.GetActiveAccountsAsync()).ToList();
        var baseCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
        var asOfDate = DateTime.UtcNow.Date;
        var ledger = await _generalLedgerService.GenerateCashBankLedgerAsync(new CashBankLedgerRequestDto
        {
            StartDate = asOfDate,
            EndDate = asOfDate
        });
        var ledgerBalances = ledger.Accounts.ToDictionary(account => account.BankAccountId, account => account.ClosingBalance);

        var summary = new CashPositionSummaryDto
        {
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

        return Ok(summary);
    }

    [HttpGet("ledger")]
    public async Task<ActionResult<CashBankLedgerReportDto>> GetLedger([FromQuery] CashBankLedgerRequestDto request)
    {
        return Ok(await _generalLedgerService.GenerateCashBankLedgerAsync(request));
    }

    /// <summary>
    /// Cash-book movement summary (opening balance, receipts, payments, transfers, closing
    /// balance) for a date range, derived from the posted cash/bank ledger.
    /// </summary>
    [HttpGet("cash-flow")]
    public async Task<ActionResult<CashFlowSummaryDto>> GetCashFlow(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var endDate = (toDate ?? DateTime.UtcNow).Date;
        var startDate = (fromDate ?? endDate.AddDays(-30)).Date;
        if (endDate < startDate)
        {
            return BadRequest("The end date must be on or after the start date.");
        }

        var ledger = await _generalLedgerService.GenerateCashBankLedgerAsync(new CashBankLedgerRequestDto
        {
            StartDate = startDate,
            EndDate = endDate
        });

        // Transfers move cash between own accounts; report their gross movement separately
        // so the receipts/payments figures reflect external cash flow.
        var transfers = ledger.Accounts
            .SelectMany(account => account.Lines)
            .Where(line => string.Equals(line.SourceDocumentType, "CashBankTransfer", StringComparison.OrdinalIgnoreCase))
            .Sum(line => line.DebitAmount);

        return Ok(new CashFlowSummaryDto
        {
            Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            OpeningBalance = ledger.TotalOpeningBalance,
            Receipts = ledger.TotalReceipts,
            Payments = ledger.TotalPayments,
            Transfers = transfers,
            ClosingBalance = ledger.TotalClosingBalance,
            NetChange = ledger.TotalClosingBalance - ledger.TotalOpeningBalance
        });
    }

    private static string NormalizeCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "GHS"
            : currency.Trim().ToUpperInvariant();
    }
}
