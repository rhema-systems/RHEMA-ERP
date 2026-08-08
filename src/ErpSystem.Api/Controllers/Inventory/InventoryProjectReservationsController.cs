using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/project-reservations")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryProjectReservationsController : ControllerBase
{
    private readonly IInventoryProjectReservationService _service;
    private readonly ILogger<InventoryProjectReservationsController> _logger;

    public InventoryProjectReservationsController(
        IInventoryProjectReservationService service,
        ILogger<InventoryProjectReservationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public Task<ActionResult<IReadOnlyList<InventoryProjectReservationDto>>> Get(
        [FromQuery] Guid? projectId = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] InventoryProjectReservationStatus? status = null,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryProjectReservationDto>>(async () =>
            Ok(await _service.GetAsync(projectId, departmentId, status, take, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<ActionResult<InventoryProjectReservationDto>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryProjectReservationDto>(async () =>
            Ok(await _service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    public Task<ActionResult<InventoryProjectReservationDto>> Reserve(
        [FromBody] CreateInventoryProjectReservationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryProjectReservationDto>(async () =>
        {
            request.CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? CorrelationId : request.CorrelationId;
            var value = await _service.ReserveAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = value.Id }, value);
        });

    [HttpPost("{id:guid}/release")]
    public Task<ActionResult<InventoryProjectReservationDto>> Release(
        Guid id,
        [FromBody] ReleaseInventoryProjectReservationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryProjectReservationDto>(async () =>
        {
            request.CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? CorrelationId : request.CorrelationId;
            return Ok(await _service.ReleaseAsync(id, request, cancellationToken));
        });

    [HttpPost("{id:guid}/substitute")]
    public Task<ActionResult<InventoryProjectReservationDto>> Substitute(
        Guid id,
        [FromBody] SubstituteInventoryProjectReservationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryProjectReservationDto>(async () =>
        {
            request.CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? CorrelationId : request.CorrelationId;
            return Ok(await _service.SubstituteAsync(id, request, cancellationToken));
        });

    private string CorrelationId =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (InventoryProjectReservationAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "INV_PROJECT_RESERVATION_FORBIDDEN", exception.Message));
        }
        catch (InventoryProjectReservationNotFoundException exception)
        {
            return NotFound(Problem(404, "INV_PROJECT_RESERVATION_NOT_FOUND", exception.Message));
        }
        catch (InventoryProjectReservationControlException exception)
        {
            var status = exception.Code.Contains("CONCURRENCY", StringComparison.Ordinal) ||
                         exception.Code.Contains("IDEMPOTENCY", StringComparison.Ordinal) ||
                         exception.Code.Contains("ACTIVE_EXISTS", StringComparison.Ordinal) ||
                         exception.Code.Contains("TERMINAL", StringComparison.Ordinal)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, Problem(status, exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(409, "INV_PROJECT_RESERVATION_CONCURRENCY_CONFLICT",
                "The reservation changed after it was loaded. Refresh and retry."));
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception,
                "Project reservation persistence conflict for correlation {CorrelationId}", CorrelationId);
            return Conflict(Problem(409, "INV_PROJECT_RESERVATION_PERSISTENCE_CONFLICT",
                "The request conflicts with a concurrently saved reservation. Refresh and retry."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Project reservation request failed for correlation {CorrelationId}", CorrelationId);
            return StatusCode(500, Problem(500, "INV_PROJECT_RESERVATION_UNEXPECTED",
                "The project reservation request could not be completed."));
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
