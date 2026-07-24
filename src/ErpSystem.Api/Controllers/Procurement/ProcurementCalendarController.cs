using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/calendar")]
[Authorize]
public sealed class ProcurementCalendarController : ControllerBase
{
    private readonly IProcurementCalendarService _service;

    public ProcurementCalendarController(IProcurementCalendarService service) => _service = service;

    [HttpGet("summary")]
    public Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet("time-zones")]
    public IActionResult GetTimeZones() => Ok(_service.GetTimeZones());

    [HttpGet("profiles")]
    public Task<IActionResult> GetProfiles(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetProfilesAsync(cancellationToken)));

    [HttpGet("profiles/{id:guid}")]
    public Task<IActionResult> GetProfile(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetProfileAsync(id, cancellationToken)));

    [HttpPost("profiles")]
    public Task<IActionResult> CreateProfile([FromBody] SaveProcurementCalendarProfileRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var created = await _service.CreateProfileAsync(request, CorrelationId, cancellationToken);
        return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
    });

    [HttpPut("profiles/{id:guid}")]
    public Task<IActionResult> UpdateProfile(Guid id, [FromBody] SaveProcurementCalendarProfileRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdateProfileAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("profiles/{id:guid}/clone")]
    public Task<IActionResult> CloneProfile(Guid id, [FromBody] CloneProcurementCalendarProfileRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var created = await _service.CloneProfileAsync(id, request, CorrelationId, cancellationToken);
        return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
    });

    [HttpPost("profiles/{id:guid}/publish")]
    public Task<IActionResult> PublishProfile(Guid id, [FromBody] ProcurementCalendarLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.PublishProfileAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("profiles/{id:guid}/retire")]
    public Task<IActionResult> RetireProfile(Guid id, [FromBody] ProcurementCalendarLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RetireProfileAsync(id, request, CorrelationId, cancellationToken)));

    [HttpDelete("profiles/{id:guid}")]
    public Task<IActionResult> DeleteDraft(Guid id, [FromBody] ProcurementCalendarLifecycleRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        await _service.DeleteDraftAsync(id, request, CorrelationId, cancellationToken);
        return NoContent();
    });

    [HttpGet("occurrences")]
    public Task<IActionResult> GetOccurrences([FromQuery] ProcurementCalendarOccurrenceSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchOccurrencesAsync(request, cancellationToken)));

    [HttpGet("occurrences/{id:guid}")]
    public Task<IActionResult> GetOccurrence(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetOccurrenceAsync(id, cancellationToken)));

    [HttpPost("occurrences/{id:guid}/acknowledge")]
    public Task<IActionResult> Acknowledge(Guid id, [FromBody] ProcurementCalendarOccurrenceActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.AcknowledgeAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("occurrences/{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, [FromBody] ProcurementCalendarOccurrenceActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.CompleteAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("occurrences/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody] ProcurementCalendarOccurrenceActionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.CancelAsync(id, request, CorrelationId, cancellationToken)));

    [HttpGet("runs")]
    public Task<IActionResult> GetRuns([FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.GetRunsAsync(take, cancellationToken)));

    [HttpPost("runs")]
    public Task<IActionResult> Run([FromBody] ProcurementCalendarRunRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RunAsync(request, CorrelationId, cancellationToken)));

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied) ? HttpContext.TraceIdentifier : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ProcurementCalendarNotFoundException exception)
        {
            return NotFound(Problem(404, "Procurement calendar record not found", exception.Message));
        }
        catch (ProcurementCalendarAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "Procurement calendar access forbidden", exception.Message));
        }
        catch (ProcurementCalendarConflictException exception)
        {
            return Conflict(Problem(409, "Procurement calendar conflict", exception.Message));
        }
        catch (ProcurementCalendarValidationException exception)
        {
            var details = new ValidationProblemDetails(new Dictionary<string, string[]> { [exception.Code] = [exception.Message] })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Procurement calendar validation failed",
                Detail = exception.Message,
                Instance = Request.Path
            };
            details.Extensions["code"] = exception.Code;
            details.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(details);
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = Request.Path,
        Extensions = { ["correlationId"] = CorrelationId }
    };
}
