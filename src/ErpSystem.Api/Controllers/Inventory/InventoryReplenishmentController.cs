using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/replenishment")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryReplenishmentController : ControllerBase
{
    private readonly IInventoryReplenishmentService _service;
    private readonly ILogger<InventoryReplenishmentController> _logger;

    public InventoryReplenishmentController(
        IInventoryReplenishmentService service,
        ILogger<InventoryReplenishmentController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public Task<ActionResult<IReadOnlyList<InventoryReplenishmentRecommendationDto>>> Get(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] InventoryReplenishmentRecommendationStatus? status = null,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryReplenishmentRecommendationDto>>(async () =>
            Ok(await _service.GetAsync(warehouseId, status, take, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<ActionResult<InventoryReplenishmentRecommendationDto>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryReplenishmentRecommendationDto>(async () =>
            Ok(await _service.GetByIdAsync(id, cancellationToken)));

    [HttpPost("generate")]
    public Task<ActionResult<IReadOnlyList<InventoryReplenishmentRecommendationDto>>> Generate(
        [FromBody] GenerateInventoryReplenishmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<IReadOnlyList<InventoryReplenishmentRecommendationDto>>(async () =>
        {
            request.CorrelationId = Correlation(request.CorrelationId);
            return Ok(await _service.GenerateAsync(request, cancellationToken));
        });

    [HttpPost("{id:guid}/submit")]
    public Task<ActionResult<InventoryReplenishmentRecommendationDto>> Submit(
        Guid id,
        [FromBody] SubmitInventoryReplenishmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryReplenishmentRecommendationDto>(async () =>
        {
            request.CorrelationId = Correlation(request.CorrelationId);
            return Ok(await _service.SubmitAsync(id, request, cancellationToken));
        });

    [HttpPost("{id:guid}/decision")]
    public Task<ActionResult<InventoryReplenishmentRecommendationDto>> Decide(
        Guid id,
        [FromBody] DecideInventoryReplenishmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryReplenishmentRecommendationDto>(async () =>
        {
            request.CorrelationId = Correlation(request.CorrelationId);
            return Ok(await _service.DecideAsync(id, request, cancellationToken));
        });

    [HttpPost("{id:guid}/purchase-requisition")]
    public Task<ActionResult<InventoryReplenishmentRecommendationDto>> Convert(
        Guid id,
        [FromBody] ConvertInventoryReplenishmentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryReplenishmentRecommendationDto>(async () =>
        {
            request.CorrelationId = Correlation(request.CorrelationId);
            return Ok(await _service.ConvertToPurchaseRequisitionAsync(id, request, cancellationToken));
        });

    private string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier
        : value;

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try { return await action(); }
        catch (InventoryReplenishmentAuthorizationException exception)
        {
            return StatusCode(403, Problem(403, exception.Code, exception.Message));
        }
        catch (InventoryReplenishmentNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code, exception.Message));
        }
        catch (InventoryReplenishmentControlException exception)
        {
            var status = exception.Code.Contains("CONCURRENCY", StringComparison.Ordinal) ||
                         exception.Code.Contains("IDEMPOTENCY", StringComparison.Ordinal) ||
                         exception.Code.Contains("STATUS", StringComparison.Ordinal)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, Problem(status, exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(409, "INV_REPLENISHMENT_CONCURRENCY_CONFLICT",
                "The recommendation changed after it was loaded. Refresh and retry."));
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Replenishment persistence conflict for {CorrelationId}",
                HttpContext.TraceIdentifier);
            return Conflict(Problem(409, "INV_REPLENISHMENT_PERSISTENCE_CONFLICT",
                "The request conflicts with a concurrently saved recommendation. Refresh and retry."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Replenishment request failed for {CorrelationId}",
                HttpContext.TraceIdentifier);
            return StatusCode(500, Problem(500, "INV_REPLENISHMENT_UNEXPECTED",
                "The replenishment request could not be completed."));
        }
    }

    private ProblemDetails Problem(int status, string code, string detail) => new()
    {
        Status = status,
        Title = code,
        Detail = detail,
        Instance = Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = HttpContext.TraceIdentifier }
    };
}
