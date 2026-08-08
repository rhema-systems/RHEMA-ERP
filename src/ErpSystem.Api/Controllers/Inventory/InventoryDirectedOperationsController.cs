using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/directed-operations")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryDirectedOperationsController : ControllerBase
{
    private readonly IInventoryDirectedOperationService _service;
    private readonly ILogger<InventoryDirectedOperationsController> _logger;

    public InventoryDirectedOperationsController(
        IInventoryDirectedOperationService service,
        ILogger<InventoryDirectedOperationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("assignees")]
    public Task<ActionResult<IReadOnlyList<InventoryDirectedAssigneeDto>>> GetAssignees(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] InventoryDirectedTaskType? taskType = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryDirectedAssigneeDto>>(async () =>
            Ok(await _service.GetAssigneesAsync(warehouseId, taskType, cancellationToken)));

    [HttpGet("suggestions")]
    public Task<ActionResult<IReadOnlyList<InventoryDirectedSuggestionDto>>> GetSuggestions(
        [FromQuery] Guid warehouseId,
        [FromQuery] InventoryDirectedTaskType? taskType = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryDirectedSuggestionDto>>(async () =>
            Ok(await _service.GetSuggestionsAsync(warehouseId, taskType, take, cancellationToken)));

    [HttpGet("tasks")]
    public Task<ActionResult<IReadOnlyList<InventoryDirectedTaskDto>>> GetTasks(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] InventoryDirectedTaskType? taskType = null,
        [FromQuery] InventoryDirectedTaskStatus? status = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryDirectedTaskDto>>(async () =>
            Ok(await _service.GetTasksAsync(warehouseId, taskType, status, take, cancellationToken)));

    [HttpGet("tasks/{id:guid}")]
    public Task<ActionResult<InventoryDirectedTaskDto>> GetTask(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryDirectedTaskDto>(async () => Ok(await _service.GetTaskAsync(id, cancellationToken)));

    [HttpPost("tasks")]
    public Task<ActionResult<InventoryDirectedTaskDto>> CreateTask(
        [FromBody] CreateInventoryDirectedTaskRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryDirectedTaskDto>(async () =>
        {
            var created = await _service.CreateTaskAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(GetTask), new { id = created.Id }, created);
        });

    [HttpPost("tasks/{id:guid}/start")]
    public Task<ActionResult<InventoryDirectedTaskDto>> StartTask(
        Guid id,
        [FromBody] StartInventoryDirectedTaskRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryDirectedTaskDto>(async () =>
            Ok(await _service.StartTaskAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("tasks/{id:guid}/confirm")]
    public Task<ActionResult<InventoryDirectedTaskDto>> ConfirmTask(
        Guid id,
        [FromBody] ConfirmInventoryDirectedTaskRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryDirectedTaskDto>(async () =>
            Ok(await _service.ConfirmTaskAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("tasks/{id:guid}/reconcile")]
    public Task<ActionResult<InventoryDirectedTaskDto>> ReconcileTask(
        Guid id,
        [FromBody] ReconcileInventoryDirectedTaskRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryDirectedTaskDto>(async () =>
            Ok(await _service.ReconcileTaskAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("tasks/{id:guid}/cancel")]
    public Task<ActionResult<InventoryDirectedTaskDto>> CancelTask(
        Guid id,
        [FromBody] CancelInventoryDirectedTaskRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryDirectedTaskDto>(async () =>
            Ok(await _service.CancelTaskAsync(id, request, CorrelationId, cancellationToken)));

    private string CorrelationId =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try { return await action(); }
        catch (InventoryDirectedOperationAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(403, exception.Code, exception.Message));
        }
        catch (InventoryDirectedOperationNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code, exception.Message));
        }
        catch (InventoryDirectedOperationConflictException exception)
        {
            var status = exception.Code.Contains("REQUIRED", StringComparison.Ordinal) ||
                         exception.Code.Contains("INVALID", StringComparison.Ordinal) ? 422 : 409;
            return StatusCode(status, Problem(status, exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem(409, "INV_DIRECTED_CONCURRENCY_CONFLICT",
                "The directed task changed after it was loaded. Refresh and retry."));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Problem(400, "INV_DIRECTED_ARGUMENT_INVALID", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return UnprocessableEntity(Problem(422, "INV_DIRECTED_OWNER_REJECTED", exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Directed warehouse operation failed for correlation {CorrelationId}", CorrelationId);
            return StatusCode(500, Problem(500, "INV_DIRECTED_UNEXPECTED",
                "The directed warehouse operation could not be completed."));
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
