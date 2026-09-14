using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/accounting-books/{accountingBookId:guid}/initialization")]
public sealed class AccountingBookInitializationController(IAccountingBookInitializationService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> Get(Guid accountingBookId, CancellationToken ct) => Ok(await service.GetAsync(accountingBookId, ct));

    [HttpGet("readiness")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> GetReadiness(Guid accountingBookId, CancellationToken ct) => Ok(await service.GetReadinessAsync(accountingBookId, ct));

    [HttpGet("preparation")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> Prepare(Guid accountingBookId, [FromQuery] string mode, [FromQuery] DateTime cutoffDate,
        [FromQuery] Guid? sourceAccountingBookId, CancellationToken ct) =>
        Ok(await service.PrepareAsync(accountingBookId, mode, cutoffDate, sourceAccountingBookId, ct));

    [HttpPut]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookInitialization)]
    public async Task<IActionResult> Configure(Guid accountingBookId, [FromBody] ConfigureAccountingBookInitializationDto request, CancellationToken ct) => Ok(await service.ConfigureAsync(accountingBookId, request, ct));

    [HttpPost("submit")]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookInitialization)]
    public async Task<IActionResult> Submit(Guid accountingBookId, CancellationToken ct) => Ok(await service.SubmitAsync(accountingBookId, ct));

    [HttpPost("approve")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookInitialization)]
    public async Task<IActionResult> Approve(Guid accountingBookId, [FromBody] DecideAccountingBookInitializationDto request, CancellationToken ct) => Ok(await service.ApproveAsync(accountingBookId, request, ct));

    [HttpPost("reject")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookInitialization)]
    public async Task<IActionResult> Reject(Guid accountingBookId, [FromBody] DecideAccountingBookInitializationDto request, CancellationToken ct) => Ok(await service.RejectAsync(accountingBookId, request, ct));
}
