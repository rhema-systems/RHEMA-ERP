using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/joint-measurements")]
public sealed class QuantitySurveyJointMeasurementsController(IQuantitySurveyJointMeasurementService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, false, token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] QuantitySurveyJointMeasurementListRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(request, false, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, false, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Create([FromBody] CreateQuantitySurveyJointMeasurementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, false, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveyJointMeasurementLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, false, CorrelationId, token)));

    [HttpPost("{id:guid}/schedule"), Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleQuantitySurveyJointMeasurementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ScheduleAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/measurement"), Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> LinkMeasurement(Guid id, [FromBody] LinkQuantitySurveyJointMeasurementSheetRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.LinkMeasurementAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/participants/{participantId:guid}/attendance"),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> Attend(Guid id, Guid participantId,
        [FromBody] AttendQuantitySurveyJointMeasurementRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.AttendAsync(id, participantId, request, false, CorrelationId, token)));

    [HttpPost("{id:guid}/review"), Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> SubmitForApproval(Guid id,
        [FromBody] QuantitySurveyJointMeasurementLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitForApprovalAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyJointMeasurementLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyJointMeasurementLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/evidence"), RequestSizeLimit(501L * 1024 * 1024),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    public Task<IActionResult> AddEvidence(Guid id, [FromForm] IFormFile file, [FromForm] Guid clientRequestId,
        [FromForm] QuantitySurveyJointMeasurementEvidenceType evidenceType, [FromForm] string title,
        CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS joint measurement validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.AddEvidenceAsync(id, stream, file.FileName, file.ContentType,
            new AddQuantitySurveyJointMeasurementEvidenceRequest
            {
                ClientRequestId = clientRequestId, EvidenceType = evidenceType, Title = title
            }, false, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content"),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> OpenEvidence(Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, false, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyJointMeasurementNotFoundException exception)
        { return NotFound(Problem(404, "QS joint measurement not found", exception.Message)); }
        catch (QuantitySurveyJointMeasurementConflictException exception)
        { return Conflict(Problem(409, "QS joint measurement conflict", exception.Message)); }
        catch (QuantitySurveyJointMeasurementValidationException exception)
        { return BadRequest(Problem(400, "QS joint measurement validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS joint measurement evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS joint measurement access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-joint-measurement-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_JOINT_MEASUREMENT_{status}", ["correlationId"] = CorrelationId }
    };
}

[ApiController]
[Authorize]
[Route("api/projects/external/my-projects/{projectId:guid}/joint-measurements")]
public sealed class ExternalProjectJointMeasurementsController(IQuantitySurveyJointMeasurementService service) : ControllerBase
{
    [HttpGet("lookups")]
    public Task<IActionResult> Lookups(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, true, token)));

    [HttpGet]
    public Task<IActionResult> List(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(new QuantitySurveyJointMeasurementListRequest
        { ProjectId = projectId, Page = 1, PageSize = 100 }, true, token)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid projectId, Guid id, CancellationToken token) => ExecuteAsync(async () =>
    {
        var value = await service.GetAsync(id, true, token);
        return value.ProjectId == projectId ? Ok(value) : NotFound(Problem(404, "QS joint measurement not found", "The joint measurement was not found in this project."));
    });

    [HttpPost]
    public Task<IActionResult> Create(Guid projectId, [FromBody] CreateQuantitySurveyJointMeasurementRequest request,
        CancellationToken token) => ExecuteAsync(async () =>
    {
        if (request.ProjectId != projectId)
            return BadRequest(Problem(400, "QS joint measurement validation failed", "The route project does not match the request project."));
        return Ok(await service.CreateAsync(request, true, CorrelationId, token));
    });

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid projectId, Guid id,
        [FromBody] QuantitySurveyJointMeasurementLifecycleRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.SubmitAsync(id, request, true, CorrelationId, token));

    [HttpPost("{id:guid}/participants/{participantId:guid}/attendance")]
    public Task<IActionResult> Attend(Guid projectId, Guid id, Guid participantId,
        [FromBody] AttendQuantitySurveyJointMeasurementRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.AttendAsync(id, participantId, request, true, CorrelationId, token));

    [HttpPost("{id:guid}/participants/{participantId:guid}/endorse")]
    public Task<IActionResult> Endorse(Guid projectId, Guid id, Guid participantId,
        [FromBody] EndorseQuantitySurveyJointMeasurementRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.EndorseAsync(id, participantId, request, true, CorrelationId, token));

    [HttpPost("{id:guid}/evidence"), RequestSizeLimit(501L * 1024 * 1024)]
    public Task<IActionResult> AddEvidence(Guid projectId, Guid id, [FromForm] IFormFile file,
        [FromForm] Guid clientRequestId, [FromForm] QuantitySurveyJointMeasurementEvidenceType evidenceType,
        [FromForm] string title, CancellationToken token) => ExecuteAsync(async () =>
    {
        var value = await service.GetAsync(id, true, token);
        if (value.ProjectId != projectId) return NotFound(Problem(404, "QS joint measurement not found", "The joint measurement was not found in this project."));
        if (file is null) return BadRequest(Problem(400, "QS joint measurement validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.AddEvidenceAsync(id, stream, file.FileName, file.ContentType,
            new AddQuantitySurveyJointMeasurementEvidenceRequest
            { ClientRequestId = clientRequestId, EvidenceType = evidenceType, Title = title },
            true, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content")]
    public Task<IActionResult> OpenEvidence(Guid projectId, Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        var value = await service.GetAsync(id, true, token);
        if (value.ProjectId != projectId) return NotFound(Problem(404, "QS joint measurement not found", "The joint measurement was not found in this project."));
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, true, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    private Task<IActionResult> ExecuteForProjectAsync(Guid projectId, Guid id,
        Func<Task<QuantitySurveyJointMeasurementDto>> action) => ExecuteAsync(async () =>
    {
        var current = await service.GetAsync(id, true);
        if (current.ProjectId != projectId) return NotFound(Problem(404, "QS joint measurement not found", "The joint measurement was not found in this project."));
        return Ok(await action());
    });

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyJointMeasurementNotFoundException exception)
        { return NotFound(Problem(404, "QS joint measurement not found", exception.Message)); }
        catch (QuantitySurveyJointMeasurementConflictException exception)
        { return Conflict(Problem(409, "QS joint measurement conflict", exception.Message)); }
        catch (QuantitySurveyJointMeasurementValidationException exception)
        { return BadRequest(Problem(400, "QS joint measurement validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS joint measurement evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS joint measurement access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-joint-measurement-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_JOINT_MEASUREMENT_{status}", ["correlationId"] = CorrelationId }
    };
}
