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
    private readonly ICashPositionReportService _cashPositionReportService;
    private readonly IGeneralLedgerService _generalLedgerService;

    public CashReportsController(
        ICashPositionReportService cashPositionReportService,
        IGeneralLedgerService generalLedgerService)
    {
        _cashPositionReportService = cashPositionReportService;
        _generalLedgerService = generalLedgerService;
    }

    [HttpGet("position")]
    public async Task<ActionResult<CashPositionSummaryDto>> GetPosition(CancellationToken cancellationToken)
        => Ok(await _cashPositionReportService.GetCurrentPositionAsync(cancellationToken));

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

}
