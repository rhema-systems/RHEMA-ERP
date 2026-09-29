using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Shared;
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
    private readonly IFinanceAccessScopeService _financeAccessScopeService;

    public CashReportsController(
        ICashPositionReportService cashPositionReportService,
        IGeneralLedgerService generalLedgerService,
        IFinanceAccessScopeService financeAccessScopeService)
    {
        _cashPositionReportService = cashPositionReportService;
        _generalLedgerService = generalLedgerService;
        _financeAccessScopeService = financeAccessScopeService;
    }

    [HttpGet("position")]
    [Authorize(Policy = FinancePermissions.RunFinanceReports)]
    public async Task<ActionResult<CashPositionSummaryDto>> GetPosition(CancellationToken cancellationToken)
        => Ok(await _cashPositionReportService.GetCurrentPositionAsync(cancellationToken));

    [HttpGet("ledger")]
    [Authorize(Policy = FinancePermissions.RunFinanceReports)]
    public async Task<ActionResult<CashBankLedgerReportDto>> GetLedger(
        [FromQuery] CashBankLedgerRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!await ApplyReadScopeAsync(request, cancellationToken))
            return Forbid();
        return Ok(await _generalLedgerService.GenerateCashBankLedgerAsync(request));
    }

    /// <summary>
    /// Cash-book movement summary (opening balance, receipts, payments, transfers, closing
    /// balance) for a date range, derived from the posted cash/bank ledger.
    /// </summary>
    [HttpGet("cash-flow")]
    [Authorize(Policy = FinancePermissions.RunFinanceReports)]
    public async Task<ActionResult<CashFlowSummaryDto>> GetCashFlow(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var endDate = (toDate ?? DateTime.UtcNow).Date;
        var startDate = (fromDate ?? endDate.AddDays(-30)).Date;
        if (endDate < startDate)
        {
            return BadRequest("The end date must be on or after the start date.");
        }

        var request = new CashBankLedgerRequestDto
        {
            StartDate = startDate,
            EndDate = endDate
        };
        if (!await ApplyReadScopeAsync(request, cancellationToken))
            return Forbid();
        var ledger = await _generalLedgerService.GenerateCashBankLedgerAsync(request);

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

    private async Task<bool> ApplyReadScopeAsync(
        CashBankLedgerRequestDto request,
        CancellationToken cancellationToken)
    {
        var permittedIds = await _financeAccessScopeService.GetPermittedBankAccountIdsAsync(
            FinanceAccessLevel.Read,
            cancellationToken);
        if (permittedIds == null)
            return true;
        if (permittedIds.Count == 0)
            return false;

        var requestedIds = request.BankAccountIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();
        if (requestedIds.Length > 0 && requestedIds.Any(id => !permittedIds.Contains(id)))
            return false;

        request.BankAccountIds = requestedIds.Length == 0
            ? permittedIds.ToList()
            : requestedIds.ToList();
        return true;
    }

}
