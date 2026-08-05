using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/negative-stock-controls")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryNegativeStockControlsController : ControllerBase
{
    private readonly IInventoryNegativeStockControlService _service;
    private readonly ILogger<InventoryNegativeStockControlsController> _logger;

    public InventoryNegativeStockControlsController(
        IInventoryNegativeStockControlService service,
        ILogger<InventoryNegativeStockControlsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("policy")]
    public Task<ActionResult<InventoryNegativeStockPolicyDto>> GetPolicy(CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryNegativeStockPolicyDto>(async () =>
            Ok(await _service.GetEffectivePolicyAsync(cancellationToken)));

    [HttpGet("overrides")]
    public Task<ActionResult<IReadOnlyList<InventoryNegativeStockOverrideDto>>> GetOverrides(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryNegativeStockOverrideDto>>(async () =>
            Ok(await _service.GetOverridesAsync(take, cancellationToken)));

    [HttpPost("overrides")]
    public Task<ActionResult<InventoryNegativeStockOverrideDto>> RegisterOverride(
        [FromBody] RegisterInventoryNegativeStockOverrideRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryNegativeStockOverrideDto>(async () =>
        {
            var value = await _service.RegisterApprovedOverrideAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetOverrides), new { id = value.Id }, value);
        });

    private string CorrelationId =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (InventoryNegativeStockAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "INV_NEGATIVE_FORBIDDEN", exception.Message));
        }
        catch (InventoryNegativeStockControlException exception)
        {
            var status = exception.Code.EndsWith("NOT_FOUND", StringComparison.Ordinal) ? 404 :
                exception.Code.Contains("REUSED", StringComparison.Ordinal) ||
                exception.Code.Contains("STALE", StringComparison.Ordinal) ||
                exception.Code.Contains("CONFLICT", StringComparison.Ordinal) ? 409 : 422;
            return StatusCode(status, Problem(status, exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(409, "INV_NEGATIVE_CONCURRENCY_CONFLICT",
                "The override changed after it was loaded. Refresh and retry."));
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception,
                "Negative-stock persistence conflict for correlation {CorrelationId}", CorrelationId);
            return Conflict(Problem(409, "INV_NEGATIVE_PERSISTENCE_CONFLICT",
                "The request conflicts with a concurrently saved override. Refresh and retry."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Negative-stock control request failed for correlation {CorrelationId}", CorrelationId);
            return StatusCode(500, Problem(500, "INV_NEGATIVE_UNEXPECTED",
                "The negative-stock control request could not be completed."));
        }
    }

    private ProblemDetails Problem(int status, string code, string detail) => new()
    {
        Status = status,
        Title = code,
        Detail = detail,
        Instance = Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = CorrelationId }
    };
}
