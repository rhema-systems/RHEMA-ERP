using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/disposals")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryDisposalsController : ControllerBase
{
    private readonly IInventoryDisposalService _service;
    private readonly ILogger<InventoryDisposalsController> _logger;

    public InventoryDisposalsController(IInventoryDisposalService service, ILogger<InventoryDisposalsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public Task<ActionResult<IReadOnlyList<InventoryDisposalDto>>> Get(
        [FromQuery] InventoryDisposalStatus? status = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<InventoryDisposalDto>>(async () =>
            Ok(await _service.GetAsync(status, warehouseId, take, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<ActionResult<InventoryDisposalDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync<InventoryDisposalDto>(async () => Ok(await _service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    public Task<ActionResult<InventoryDisposalDto>> Create(
        [FromBody] CreateInventoryDisposalRequest request,
        CancellationToken cancellationToken) => ExecuteAsync<InventoryDisposalDto>(async () =>
    {
        request.CorrelationId = Correlation(request.CorrelationId);
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    });

    [HttpPost("{id:guid}/audit-verification")]
    public Task<ActionResult<InventoryDisposalDto>> Verify(Guid id, [FromBody] VerifyInventoryDisposalRequest request,
        CancellationToken cancellationToken) => ExecuteMutationAsync(id, request, _service.VerifyAsync, cancellationToken);

    [HttpPost("{id:guid}/committee/schedule")]
    public Task<ActionResult<InventoryDisposalDto>> Schedule(Guid id, [FromBody] ScheduleInventoryDisposalCommitteeRequest request,
        CancellationToken cancellationToken) => ExecuteMutationAsync(id, request, _service.ScheduleCommitteeAsync, cancellationToken);

    [HttpPost("{id:guid}/committee/vote")]
    public Task<ActionResult<InventoryDisposalDto>> Vote(Guid id, [FromBody] VoteInventoryDisposalRequest request,
        CancellationToken cancellationToken) => ExecuteMutationAsync(id, request, _service.VoteAsync, cancellationToken);

    [HttpPost("{id:guid}/submit")]
    public Task<ActionResult<InventoryDisposalDto>> Submit(Guid id, [FromBody] SubmitInventoryDisposalRequest request,
        CancellationToken cancellationToken) => ExecuteMutationAsync(id, request, _service.SubmitAsync, cancellationToken);

    [HttpPost("{id:guid}/decision")]
    public Task<ActionResult<InventoryDisposalDto>> Decide(Guid id, [FromBody] DecideInventoryDisposalRequest request,
        CancellationToken cancellationToken) => ExecuteMutationAsync(id, request, _service.DecideAsync, cancellationToken);

    [HttpPost("{id:guid}/execution/stage")]
    public Task<ActionResult<InventoryDisposalDto>> StageExecution(Guid id, [FromBody] StageInventoryDisposalExecutionRequest request,
        CancellationToken cancellationToken) => ExecuteMutationAsync(id, request, _service.StageExecutionAsync, cancellationToken);

    [HttpPost("{id:guid}/execution/complete")]
    public Task<ActionResult<InventoryDisposalDto>> Complete(Guid id, [FromBody] CompleteInventoryDisposalRequest request,
        CancellationToken cancellationToken) => ExecuteMutationAsync(id, request, _service.CompleteAsync, cancellationToken);

    private Task<ActionResult<InventoryDisposalDto>> ExecuteMutationAsync<TRequest>(
        Guid id,
        TRequest request,
        Func<Guid, TRequest, CancellationToken, Task<InventoryDisposalDto>> action,
        CancellationToken cancellationToken) where TRequest : InventoryDisposalMutationRequest =>
        ExecuteAsync<InventoryDisposalDto>(async () =>
        {
            request.CorrelationId = Correlation(request.CorrelationId);
            return Ok(await action(id, request, cancellationToken));
        });

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try { return await action(); }
        catch (InventoryDisposalNotFoundException exception)
        {
            return NotFound(Problem(exception.Code, exception.Message));
        }
        catch (InventoryDisposalAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem("INV_DISPOSAL_FORBIDDEN", exception.Message));
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem("INV_DISPOSAL_FORBIDDEN", exception.Message));
        }
        catch (InventoryDisposalException exception)
        {
            var status = exception.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         exception.Code.Contains("CONCURRENCY", StringComparison.OrdinalIgnoreCase) ||
                         exception.Code.Contains("STATE", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, Problem(exception.Code, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Inventory disposal request failed at {Path}", HttpContext.Request.Path);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("INV_DISPOSAL_UNEXPECTED", "The inventory disposal request could not be completed."));
        }
    }

    private object Problem(string code, string message) => new
    {
        code, message, correlationId = HttpContext.TraceIdentifier
    };

    private string Correlation(string? requested)
    {
        var header = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        var value = !string.IsNullOrWhiteSpace(requested) ? requested.Trim() :
            !string.IsNullOrWhiteSpace(header) ? header.Trim() : HttpContext.TraceIdentifier;
        return value.Length <= 100 ? value : value[..100];
    }
}
