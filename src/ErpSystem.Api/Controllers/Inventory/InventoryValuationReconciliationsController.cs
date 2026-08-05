using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/valuation-reconciliations")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryValuationReconciliationsController : ControllerBase
{
    private readonly IInventoryValuationReconciliationService _service;
    private readonly ILogger<InventoryValuationReconciliationsController> _logger;

    public InventoryValuationReconciliationsController(
        IInventoryValuationReconciliationService service,
        ILogger<InventoryValuationReconciliationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public Task<ActionResult<IReadOnlyList<InventoryValuationReconciliationDto>>> Get(
        [FromQuery] Guid? fiscalPeriodId = null,
        [FromQuery] InventoryValuationReconciliationStatus? status = null,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryValuationReconciliationDto>>(async () =>
            Ok(await _service.GetAsync(fiscalPeriodId, status, take, cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public Task<ActionResult<InventoryValuationReconciliationDto>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryValuationReconciliationDto>(async () =>
            Ok(await _service.GetByIdAsync(id, cancellationToken)));

    [HttpPost("generate")]
    [Authorize(Policy = FinancePermissions.RunFinanceReports)]
    public Task<ActionResult<InventoryValuationReconciliationDto>> Generate(
        [FromBody] GenerateInventoryValuationReconciliationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryValuationReconciliationDto>(async () =>
        {
            request.CorrelationId = Correlation(request.CorrelationId);
            return Ok(await _service.GenerateAsync(request, cancellationToken));
        });

    [HttpPost("{id:guid}/freeze")]
    [Authorize(Policy = FinancePermissions.CloseAccountingPeriods)]
    public Task<ActionResult<InventoryValuationReconciliationDto>> Freeze(
        Guid id,
        [FromBody] FreezeInventoryValuationReconciliationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryValuationReconciliationDto>(async () =>
        {
            request.CorrelationId = Correlation(request.CorrelationId);
            return Ok(await _service.FreezeAsync(id, request, cancellationToken));
        });

    private string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier
        : value;

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try { return await action(); }
        catch (InventoryValuationReconciliationNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code, exception.Message));
        }
        catch (InventoryValuationReconciliationException exception)
        {
            var status = exception.Code.Contains("CONCURRENCY", StringComparison.Ordinal) ||
                         exception.Code.Contains("IDEMPOTENCY", StringComparison.Ordinal) ||
                         exception.Code.Contains("STALE", StringComparison.Ordinal) ||
                         exception.Code.Contains("NOT_LATEST", StringComparison.Ordinal)
                ? StatusCodes.Status409Conflict
                : exception.Code.Contains("FORBIDDEN", StringComparison.Ordinal) ||
                  exception.Code.Contains("SOD", StringComparison.Ordinal)
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, Problem(status, exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(409, "INV_VALUATION_RECONCILIATION_CONCURRENCY_CONFLICT",
                "The reconciliation changed after it was loaded. Refresh and retry."));
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Inventory valuation reconciliation persistence conflict");
            return Conflict(Problem(409, "INV_VALUATION_RECONCILIATION_PERSISTENCE_CONFLICT",
                "The request conflicts with concurrently saved reconciliation data."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Inventory valuation reconciliation request failed");
            return StatusCode(500, Problem(500, "INV_VALUATION_RECONCILIATION_UNEXPECTED",
                "The inventory valuation reconciliation request could not be completed."));
        }
    }

    private ProblemDetails Problem(int status, string code, string detail) => new()
    {
        Status = status, Title = code, Detail = detail, Instance = Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = HttpContext.TraceIdentifier }
    };
}
