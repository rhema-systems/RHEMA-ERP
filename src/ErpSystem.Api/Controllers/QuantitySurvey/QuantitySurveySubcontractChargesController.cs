using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/subcontract-charges")]
public sealed class QuantitySurveySubcontractChargesController(
    IQuantitySurveySubcontractChargeService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get([FromQuery] Guid projectId, [FromQuery] Guid subcontractId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(projectId, subcontractId, false, token)));

    [HttpPut("{subcontractId:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> Save(Guid subcontractId, [FromBody] SaveQuantitySurveySubcontractChargeRequest request,
        CancellationToken token) => ExecuteAsync(async () => Ok(await service.SaveAsync(subcontractId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/evidence"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid id, [FromForm] Guid clientRequestId, [FromForm] string title,
        [FromForm] IFormFile file, CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS subcontract charge validation failed", "Select an evidence file."));
        return Ok(await service.UploadEvidenceAsync(id, clientRequestId, title, file.FileName, file.ContentType,
            file.Length, file.OpenReadStream, false, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> OpenEvidence(Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, false, token);
        await using var buffer = new MemoryStream();
        await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpPost("{id:guid}/issue"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> Issue(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.IssueAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] DecideQuantitySurveySubcontractChargeRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideAsync(id, request, true, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] DecideQuantitySurveySubcontractChargeRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideAsync(id, request, false, CorrelationId, token)));

    [HttpPost("{id:guid}/retry-communication"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> RetryCommunication(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RetryCommunicationAsync(id, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveySubcontractNotFoundException exception) { return NotFound(Problem(404, "QS subcontract charge not found", exception.Message)); }
        catch (QuantitySurveySubcontractConflictException exception) { return Conflict(Problem(409, "QS subcontract charge conflict", exception.Message)); }
        catch (QuantitySurveySubcontractValidationException exception) { return BadRequest(Problem(400, "QS subcontract charge validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception) { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS subcontract charge evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "QS subcontract charge access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-subcontract-charge-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_SUBCONTRACT_CHARGE_{status}", ["correlationId"] = CorrelationId }
    };
}

[ApiController]
[Authorize]
[Route("api/projects/external/my-projects/{projectId:guid}/subcontracts/{subcontractId:guid}/charge-notices")]
public sealed class ExternalProjectSubcontractChargesController(
    IQuantitySurveySubcontractChargeService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(Guid projectId, Guid subcontractId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(projectId, subcontractId, true, token)));

    [HttpPost("{id:guid}/evidence"), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid projectId, Guid subcontractId, Guid id,
        [FromForm] Guid clientRequestId, [FromForm] string title, [FromForm] IFormFile file,
        CancellationToken token) => ExecuteForChargeAsync(projectId, subcontractId, id, async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS subcontract charge validation failed", "Select an evidence file."));
        return Ok(await service.UploadEvidenceAsync(id, clientRequestId, title, file.FileName, file.ContentType,
            file.Length, file.OpenReadStream, true, CorrelationId, token));
    }, token);

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content")]
    public Task<IActionResult> OpenEvidence(Guid projectId, Guid subcontractId, Guid id, Guid evidenceId,
        CancellationToken token) => ExecuteForChargeAsync(projectId, subcontractId, id, async () =>
    {
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, true, token);
        await using var buffer = new MemoryStream();
        await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    }, token);

    [HttpPost("{id:guid}/respond")]
    public Task<IActionResult> Respond(Guid projectId, Guid subcontractId, Guid id,
        [FromBody] RespondQuantitySurveySubcontractChargeRequest request, CancellationToken token) =>
        ExecuteForChargeAsync(projectId, subcontractId, id,
            async () => Ok(await service.RespondAsync(id, request, CorrelationId, token)), token);

    private Task<IActionResult> ExecuteForChargeAsync(Guid projectId, Guid subcontractId, Guid id,
        Func<Task<IActionResult>> action, CancellationToken token) => ExecuteAsync(async () =>
    {
        var values = await service.GetAsync(projectId, subcontractId, true, token);
        if (!values.Any(value => value.Id == id))
            return NotFound(Problem(404, "QS subcontract charge not found", "The charge notice was not found in this subcontract."));
        return await action();
    });

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveySubcontractNotFoundException exception) { return NotFound(Problem(404, "QS subcontract charge not found", exception.Message)); }
        catch (QuantitySurveySubcontractConflictException exception) { return Conflict(Problem(409, "QS subcontract charge conflict", exception.Message)); }
        catch (QuantitySurveySubcontractValidationException exception) { return BadRequest(Problem(400, "QS subcontract charge validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception) { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS subcontract charge evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "QS subcontract charge access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-external-subcontract-charge-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_EXTERNAL_SUBCONTRACT_CHARGE_{status}", ["correlationId"] = CorrelationId }
    };
}
