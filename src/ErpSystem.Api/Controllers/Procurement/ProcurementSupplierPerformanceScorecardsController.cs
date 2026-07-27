using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-performance-scorecards")]
[Authorize]
public sealed class ProcurementSupplierPerformanceScorecardsController :
    ControllerBase
{
    private readonly IProcurementSupplierPerformanceScorecardService _service;

    public ProcurementSupplierPerformanceScorecardsController(
        IProcurementSupplierPerformanceScorecardService service) =>
        _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("supplier-options")]
    public Task<IActionResult> SupplierOptions(
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.GetSupplierOptionsAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementSupplierPerformanceSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
            Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("suppliers/{businessPartnerId:guid}/current")]
    public Task<IActionResult> Current(
        Guid businessPartnerId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetCurrentStateAsync(
            businessPartnerId, cancellationToken)));

    [HttpPost("calculate")]
    public Task<IActionResult> Calculate(
        [FromBody] CalculateProcurementSupplierPerformanceRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await _service.CalculateAsync(
                request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(
            Request.Headers["X-Correlation-ID"].FirstOrDefault())
            ? HttpContext.TraceIdentifier
            : Request.Headers["X-Correlation-ID"].First()!;

    private async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementSupplierPerformanceNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Supplier performance record not found", exception.Message));
        }
        catch (ProcurementSupplierPerformanceAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "SUPPLIER_PERFORMANCE_ACCESS_FORBIDDEN",
                    "Supplier performance access denied", exception.Message));
        }
        catch (ProcurementSupplierPerformanceConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Supplier performance conflict", exception.Message));
        }
        catch (ProcurementSupplierPerformanceValidationException exception)
        {
            return UnprocessableEntity(Problem(422, exception.Code,
                "Supplier performance validation failed", exception.Message));
        }
    }

    private ProblemDetails Problem(
        int status,
        string code,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationId;
        return problem;
    }
}
