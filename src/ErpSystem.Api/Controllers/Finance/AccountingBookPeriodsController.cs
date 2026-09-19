using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/accounting-books/{accountingBookId:guid}/periods")]
public sealed class AccountingBookPeriodsController(IAccountingBookPeriodService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> Get(Guid accountingBookId, CancellationToken ct) => Ok(await service.GetAsync(accountingBookId, ct));

    [HttpPost]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookPeriods)]
    public async Task<IActionResult> Create(Guid accountingBookId, [FromBody] CreateAccountingBookPeriodDto request, CancellationToken ct) => Ok(await service.CreateAsync(accountingBookId, request, ct));

    [HttpPost("{id:guid}/transitions")]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookPeriods)]
    public async Task<IActionResult> RequestTransition(Guid accountingBookId, Guid id, [FromBody] RequestAccountingBookPeriodTransitionDto request, CancellationToken ct)
        => Ok(await service.RequestTransitionAsync(accountingBookId, id, request, ct));

    [HttpPost("{id:guid}/transitions/approve")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookPeriods)]
    public async Task<IActionResult> Approve(Guid accountingBookId, Guid id, [FromBody] DecideAccountingBookPeriodTransitionDto request, CancellationToken ct)
        => Ok(await service.ApproveAsync(accountingBookId, id, request, ct));

    [HttpPost("{id:guid}/transitions/reject")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookPeriods)]
    public async Task<IActionResult> Reject(Guid accountingBookId, Guid id, [FromBody] DecideAccountingBookPeriodTransitionDto request, CancellationToken ct)
        => Ok(await service.RejectAsync(accountingBookId, id, request, ct));
}
