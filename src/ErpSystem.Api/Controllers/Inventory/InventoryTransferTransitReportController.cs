using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/transfers/transit-stock")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryTransferTransitReportController(IInventoryTransferService transfers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InventoryTransitStockReportDto>> Get(CancellationToken cancellationToken)
        => Ok(await transfers.GetTransitStockAsync(cancellationToken));
}
