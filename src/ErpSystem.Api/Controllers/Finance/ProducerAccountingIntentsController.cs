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
    [HttpPost]
    [Authorize(Policy = FinancePermissions.PrepareAccountingEvents)]
    public async Task<IActionResult> Prepare([FromBody] ProducerAccountingIntentDto intent, CancellationToken ct) =>
        Ok(await service.PrepareAsync(intent, ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = FinancePermissions.OrchestrateAccountingEvents)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecideProducerIntentRequest request, CancellationToken ct) =>
        Ok(await service.ApproveAsync(id, request.Intent, request.Decision, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = FinancePermissions.OrchestrateAccountingEvents)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecideProducerIntentRequest request, CancellationToken ct) =>
        Ok(await service.RejectAsync(id, request.Intent, request.Decision, ct));
}

public sealed class DecideProducerIntentRequest
{
    public ProducerAccountingIntentDto Intent { get; set; } = new();
    public DecideProducerAccountingIntentDto Decision { get; set; } = new();
}
