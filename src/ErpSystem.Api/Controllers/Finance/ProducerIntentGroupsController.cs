using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/producer-intent-groups")]
public sealed class ProducerIntentGroupsController(IFinanceProducerIntentGroupService service) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewAccountingEvents)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = FinancePermissions.PrepareAccountingEvents)]
    public async Task<IActionResult> Prepare([FromBody] ProducerIntentGroupRequestDto request, CancellationToken ct) =>
        Ok(await service.PrepareAsync(request, ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = FinancePermissions.OrchestrateAccountingEvents)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecideProducerIntentGroupRequestDto request, CancellationToken ct) =>
        Ok(await service.ApproveAsync(id, request, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = FinancePermissions.OrchestrateAccountingEvents)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecideProducerIntentGroupRequestDto request, CancellationToken ct) =>
        Ok(await service.RejectAsync(id, request, ct));
}
