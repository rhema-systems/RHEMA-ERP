using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/producer-accounting-intents")]
public sealed class ProducerAccountingIntentsController(IFinanceProducerIntentService service) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewAccountingEvents)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = FinancePermissions.PrepareAccountingEvents)]
    public async Task<IActionResult> Prepare([FromBody] ProducerAccountingIntentDto intent, CancellationToken ct) =>
        Ok(await service.PrepareAsync(intent, ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = FinancePermissions.OrchestrateAccountingEvents)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecideProducerAccountingIntentDto decision, CancellationToken ct) =>
        Ok(await service.ApprovePreparedAsync(id, decision, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = FinancePermissions.OrchestrateAccountingEvents)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecideProducerAccountingIntentDto decision, CancellationToken ct) =>
        Ok(await service.RejectPreparedAsync(id, decision, ct));
}
