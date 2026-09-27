using ErpSystem.Core.DTOs.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

public partial class PhysicalCountsController
{
    [HttpGet("counter-options")]
    public async Task<ActionResult<IReadOnlyList<PhysicalCountCounterOptionDto>>> GetCounterOptions(
        [FromQuery] Guid warehouseId, [FromQuery] Guid? locationId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        try { return Ok(await _countService.GetCounterOptionsAsync(warehouseId, locationId, search, cancellationToken)); }
        catch (Exception exception) { return ControlledError(exception, "read count counter options", Guid.Empty); }
    }

    [HttpPut("{id:guid}/counters")]
    public async Task<ActionResult> AssignCounters(Guid id, [FromBody] AssignPhysicalCountCountersRequest request)
    {
        try { await _countService.AssignCountersAsync(id, GetCurrentUserId(), request); return NoContent(); }
        catch (Exception exception) { return ControlledError(exception, "assign count counters", id); }
    }
}
