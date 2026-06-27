using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize(Policy = "InternalOnly")]
[ApiController]
[Route("api/sales/setup")]
public class SalesSetupController : ControllerBase
{
    private readonly ISalesSetupService _salesSetupService;

    public SalesSetupController(ISalesSetupService salesSetupService)
    {
        _salesSetupService = salesSetupService;
    }

    [HttpGet("saleable-sources")]
    public async Task<ActionResult<IReadOnlyCollection<SalesSaleableSourceDto>>> GetSaleableSources([FromQuery] bool includeInactive = false)
        => Ok(await _salesSetupService.GetSaleableSourcesAsync(includeInactive));

    [HttpGet("saleable-sources/{id:guid}/items")]
    public async Task<ActionResult<IReadOnlyCollection<SalesSaleableItemDto>>> SearchSaleableSourceItems(
        Guid id,
        [FromQuery] string? search = null,
        [FromQuery] int take = 50)
        => Ok(await _salesSetupService.SearchSaleableItemsAsync(id, search, take));

    [HttpPost("saleable-sources")]
    [Authorize(Roles = "admin,SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<SalesSaleableSourceDto>> CreateSaleableSource([FromBody] UpsertSalesSaleableSourceDto dto)
        => Ok(await _salesSetupService.CreateSaleableSourceAsync(dto));

    [HttpPut("saleable-sources/{id:guid}")]
    [Authorize(Roles = "admin,SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<SalesSaleableSourceDto>> UpdateSaleableSource(Guid id, [FromBody] UpsertSalesSaleableSourceDto dto)
        => Ok(await _salesSetupService.UpdateSaleableSourceAsync(id, dto));

    [HttpPost("saleable-sources/{id:guid}/activate")]
    [Authorize(Roles = "admin,SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<SalesSaleableSourceDto>> ActivateSaleableSource(Guid id)
        => Ok(await _salesSetupService.SetSaleableSourceActiveStateAsync(id, true));

    [HttpPost("saleable-sources/{id:guid}/deactivate")]
    [Authorize(Roles = "admin,SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<SalesSaleableSourceDto>> DeactivateSaleableSource(Guid id)
        => Ok(await _salesSetupService.SetSaleableSourceActiveStateAsync(id, false));

    [HttpDelete("saleable-sources/{id:guid}")]
    [Authorize(Roles = "admin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> DeleteSaleableSource(Guid id)
    {
        await _salesSetupService.DeleteSaleableSourceAsync(id);
        return NoContent();
    }

    [HttpPost("saleable-sources/seed-defaults")]
    [Authorize(Roles = "admin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> SeedSaleableSourceDefaults()
    {
        await _salesSetupService.SeedDefaultSaleableSourcesAsync();
        return NoContent();
    }
}
