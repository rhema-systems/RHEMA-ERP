using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/control-events")]
[Authorize(Roles = Readers)]
public sealed class ProcurementControlEventsController : ControllerBase
{
    private const string Readers = "SuperAdmin,TenantAdmin,TDC_INTERNAL_AUDIT";
    private readonly IProcurementControlEventService _service;

    public ProcurementControlEventsController(IProcurementControlEventService service) => _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] ProcurementControlEventSearchRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("correlations/{correlationId}")]
    public Task<IActionResult> GetCorrelation(string correlationId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetCorrelationAsync(correlationId, cancellationToken)));

    [HttpPost("integrity/verify")]
    public Task<IActionResult> VerifyIntegrity([FromQuery] int take = 1000, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.VerifyIntegrityAsync(take, cancellationToken)));

    private string CorrelationId => string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
        ? Guid.NewGuid().ToString("N")
        : HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementControlEventNotFoundException exception)
        {
            return NotFound(Problem(404, "Procurement control event not found", exception.Message));
        }
        catch (ProcurementControlEventAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "Procurement control-event access forbidden", exception.Message));
        }
        catch (ProcurementControlEventConflictException exception)
        {
            return Conflict(Problem(409, "Procurement control-event conflict", exception.Message));
        }
        catch (ProcurementControlEventValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = new[] { exception.Message }
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement control-event validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = { ["correlationId"] = CorrelationId }
    };
}
