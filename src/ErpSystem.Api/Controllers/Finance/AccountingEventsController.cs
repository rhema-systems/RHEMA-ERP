using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/accounting-events")]
public sealed class AccountingEventsController(IAccountingEventService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = FinancePermissions.PrepareAccountingEvents)]
    public async Task<IActionResult> Create([FromBody] CreateAccountingEventDto request, CancellationToken ct) =>
        Ok(await service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/release")]
    [Authorize(Policy = FinancePermissions.OrchestrateAccountingEvents)]
    public async Task<IActionResult> Release(Guid id, [FromBody] ReleaseAccountingEventDto request, CancellationToken ct) =>
        Ok(await service.ReleaseAsync(id, request, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewAccountingEvents)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpGet("{id:guid}/books/{accountingBookId:guid}")]
    [Authorize(Policy = FinancePermissions.ViewAccountingEvents)]
    public async Task<IActionResult> GetBook(Guid id, Guid accountingBookId, CancellationToken ct) =>
        Ok(await service.GetBookAsync(id, accountingBookId, ct));
}
