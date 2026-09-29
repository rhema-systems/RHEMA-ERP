using ErpSystem.Core.DTOs.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

public partial class PhysicalCountsController
{
    [HttpPost("{id:guid}/recounts")]
    public async Task<ActionResult<PhysicalCountDto>> CreateRecount(Guid id, [FromBody] CreatePhysicalCountRecountRequest request)
    {
        try { return Ok(await _countService.CreateRecountAsync(id, GetCurrentUserId(), request)); }
        catch (Exception exception) { return ControlledError(exception, "create recount sheet", id); }
    }
}
