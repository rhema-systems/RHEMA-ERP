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
[Route("api/quantity-survey/valuation-worksheets")]
public sealed class QuantitySurveyValuationWorksheetsController(
    IQuantitySurveyValuationWorksheetService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet("{interimValuationId:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid interimValuationId, [FromQuery] Guid? projectBoqVersionId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(interimValuationId, projectBoqVersionId, token)));

    [HttpPut("{interimValuationId:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Save(Guid interimValuationId,
        [FromBody] SaveQuantitySurveyValuationWorksheetRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SaveAsync(interimValuationId, request, HttpContext.TraceIdentifier, token)));

    [HttpGet("{interimValuationId:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid interimValuationId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(interimValuationId, token)));

    [HttpPost("worksheets/{id:guid}/vet"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Vet(Guid id, [FromBody] QuantitySurveyValuationLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.VetAsync(id, request, HttpContext.TraceIdentifier, token)));

    [HttpPost("worksheets/{id:guid}/submit-approval"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> SubmitApproval(Guid id, [FromBody] QuantitySurveyValuationLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitApprovalAsync(id, request, HttpContext.TraceIdentifier, token)));

    [HttpPost("worksheets/{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyValuationLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, HttpContext.TraceIdentifier, token)));

    [HttpPost("worksheets/{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyValuationLifecycleRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, HttpContext.TraceIdentifier, token)));

    [HttpPost("worksheets/{id:guid}/evidence"), RequestSizeLimit(501L * 1024 * 1024),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> AddEvidence(Guid id, [FromForm] IFormFile file, [FromForm] Guid clientRequestId,
        [FromForm] QuantitySurveyValuationEvidenceType evidenceType, [FromForm] string title,
        CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS valuation worksheet validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.AddEvidenceAsync(id, stream, file.FileName, file.ContentType,
            new AddQuantitySurveyValuationEvidenceRequest
            { ClientRequestId = clientRequestId, EvidenceType = evidenceType, Title = title },
            false, HttpContext.TraceIdentifier, token));
    });

    [HttpGet("worksheets/{id:guid}/evidence/{evidenceId:guid}/content"),
     Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> OpenEvidence(Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, false, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyValuationWorksheetNotFoundException exception)
        { return NotFound(Problem(404, "QS valuation worksheet not found", exception.Message)); }
        catch (QuantitySurveyValuationWorksheetConflictException exception)
        { return Conflict(Problem(409, "QS valuation worksheet conflict", exception.Message)); }
        catch (QuantitySurveyValuationWorksheetValidationException exception)
        { return BadRequest(Problem(400, "QS valuation worksheet validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS valuation evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS valuation worksheet access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-valuation-worksheet-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_VALUATION_WORKSHEET_{status}", ["correlationId"] = HttpContext.TraceIdentifier }
    };
}

[ApiController]
[Authorize]
[Route("api/projects/external/my-projects/{projectId:guid}/valuation-worksheets")]
public sealed class ExternalProjectValuationWorksheetsController(
    IQuantitySurveyValuationWorksheetService service) : ControllerBase
{
    [HttpGet("lookups")]
    public Task<IActionResult> Lookups(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetExternalLookupsAsync(projectId, token)));

    [HttpGet]
    public Task<IActionResult> List(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetExternalProjectValuationsAsync(projectId, token)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid projectId, Guid id, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.GetExternalAsync(id, token));

    [HttpPut("{id:guid}/claim")]
    public Task<IActionResult> SaveClaim(Guid projectId, Guid id,
        [FromBody] SaveQuantitySurveyValuationWorksheetRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.SaveContractorClaimAsync(id, request, CorrelationId, token));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> SubmitClaim(Guid projectId, Guid id,
        [FromBody] QuantitySurveyValuationEndorsementRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.SubmitContractorClaimAsync(id, request, CorrelationId, token));

    [HttpPost("{id:guid}/endorse")]
    public Task<IActionResult> Endorse(Guid projectId, Guid id,
        [FromBody] QuantitySurveyValuationEndorsementRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.EndorseConsultantAsync(id, request, CorrelationId, token));

    [HttpPost("{id:guid}/evidence"), RequestSizeLimit(501L * 1024 * 1024)]
    public Task<IActionResult> AddEvidence(Guid projectId, Guid id, [FromForm] IFormFile file,
        [FromForm] Guid clientRequestId, [FromForm] QuantitySurveyValuationEvidenceType evidenceType,
        [FromForm] string title, CancellationToken token) => ExecuteAsync(async () =>
    {
        var current = await service.GetExternalAsync(id, token);
        if (current.ProjectId != projectId) return NotFound(Problem(404, "QS valuation not found", "The valuation was not found in this project."));
        if (file is null) return BadRequest(Problem(400, "QS valuation validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.AddEvidenceAsync(id, stream, file.FileName, file.ContentType,
            new AddQuantitySurveyValuationEvidenceRequest
            { ClientRequestId = clientRequestId, EvidenceType = evidenceType, Title = title },
            true, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content")]
    public Task<IActionResult> OpenEvidence(Guid projectId, Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        var current = await service.GetExternalAsync(id, token);
        if (current.ProjectId != projectId) return NotFound(Problem(404, "QS valuation not found", "The valuation was not found in this project."));
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, true, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    private Task<IActionResult> ExecuteForProjectAsync(Guid projectId, Guid id,
        Func<Task<QuantitySurveyValuationWorksheetDto>> action) => ExecuteAsync(async () =>
    {
        var current = await service.GetExternalAsync(id);
        if (current.ProjectId != projectId)
            return NotFound(Problem(404, "QS valuation not found", "The valuation was not found in this project."));
        return Ok(await action());
    });

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyValuationWorksheetNotFoundException exception)
        { return NotFound(Problem(404, "QS valuation not found", exception.Message)); }
        catch (QuantitySurveyValuationWorksheetConflictException exception)
        { return Conflict(Problem(409, "QS valuation conflict", exception.Message)); }
        catch (QuantitySurveyValuationWorksheetValidationException exception)
        { return BadRequest(Problem(400, "QS valuation validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS valuation evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS valuation access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-valuation-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_VALUATION_{status}", ["correlationId"] = CorrelationId }
    };
}
