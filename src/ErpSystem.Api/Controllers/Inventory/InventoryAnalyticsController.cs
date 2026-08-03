using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/analytics")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryAnalyticsController : ControllerBase
{
    private readonly IInventoryAnalyticsService _service;
    private readonly ILogger<InventoryAnalyticsController> _logger;

    public InventoryAnalyticsController(
        IInventoryAnalyticsService service,
        ILogger<InventoryAnalyticsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<InventoryAnalyticsDto>> Get(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] int slowMovingDays = 90,
        [FromQuery] int nonMovingDays = 180,
        [FromQuery] int expiryWarningDays = 90,
        [FromQuery] int take = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetAsync(warehouseId, categoryId, slowMovingDays,
                nonMovingDays, expiryWarningDays, take, cancellationToken));
        }
        catch (InventoryAnalyticsAuthorizationException exception)
        {
            return StatusCode(403, Problem(403, exception.Code, exception.Message));
        }
        catch (InventoryAnalyticsException exception)
        {
            return UnprocessableEntity(Problem(422, exception.Code, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Inventory analytics request failed");
            return StatusCode(500, Problem(500, "INV_ANALYTICS_UNEXPECTED",
                "The inventory analytics request could not be completed."));
        }
    }

    private ProblemDetails Problem(int status, string code, string detail) => new()
    {
        Status = status, Title = code, Detail = detail, Instance = Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = HttpContext.TraceIdentifier }
    };
}
