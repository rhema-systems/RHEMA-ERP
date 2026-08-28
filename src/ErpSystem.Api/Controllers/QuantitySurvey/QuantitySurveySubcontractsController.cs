using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/subcontracts")]
public sealed class QuantitySurveySubcontractsController(IQuantitySurveySubcontractService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, false, token)));

    [HttpPut, Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public Task<IActionResult> Save([FromQuery] Guid projectId, [FromBody] SaveQuantitySurveySubcontractRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SaveAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/evidence"), Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid id, [FromForm] Guid? valuationId, [FromForm] Guid clientRequestId,
        [FromForm] string title, [FromForm] IFormFile file, CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS subcontract validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadEvidenceAsync(id, valuationId, clientRequestId, title, file.FileName,
            file.ContentType, file.Length, () => stream, false, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> OpenEvidence(Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, false, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public Task<IActionResult> Submit(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitSubcontractAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideSubcontractAsync(id, request, true, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideSubcontractAsync(id, request, false, CorrelationId, token)));

    [HttpPut("{id:guid}/valuations"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> SaveValuation(Guid id, [FromBody] SaveQuantitySurveySubcontractValuationRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SaveValuationAsync(id, request, false, CorrelationId, token)));

    [HttpPost("valuations/{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> SubmitValuation(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitValuationAsync(id, request, false, CorrelationId, token)));

    [HttpPost("valuations/{id:guid}/assess"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> Assess(Guid id, [FromBody] AssessQuantitySurveySubcontractValuationRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.AssessValuationAsync(id, request, CorrelationId, token)));

    [HttpPost("valuations/{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> ApproveValuation(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideValuationAsync(id, request, true, CorrelationId, token)));

    [HttpPost("valuations/{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> RejectValuation(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideValuationAsync(id, request, false, CorrelationId, token)));

    [HttpPost("valuations/{id:guid}/handoff-ap"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> Handoff(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HandoffToApAsync(id, request, CorrelationId, token)));

    [HttpPost("valuations/{id:guid}/refresh-payment"), Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public Task<IActionResult> RefreshPayment(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RefreshPaymentAsync(id, CorrelationId, token)));

    [HttpPost("{id:guid}/close"), Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public Task<IActionResult> Close(Guid id, [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CloseAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveySubcontractNotFoundException exception) { return NotFound(Problem(404, "QS subcontract not found", exception.Message)); }
        catch (QuantitySurveySubcontractConflictException exception) { return Conflict(Problem(409, "QS subcontract conflict", exception.Message)); }
        catch (QuantitySurveySubcontractValidationException exception) { return BadRequest(Problem(400, "QS subcontract validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception) { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS subcontract evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "QS subcontract access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    { Status = status, Title = title, Detail = detail, Type = $"https://tdc.gov.gh/problems/quantity-survey-subcontract-{status}",
        Instance = HttpContext.Request.Path, Extensions = { ["code"] = code ?? $"QS_SUBCONTRACT_{status}", ["correlationId"] = CorrelationId } };
}

[ApiController]
[Authorize]
[Route("api/projects/external/my-projects/{projectId:guid}/subcontracts")]
public sealed class ExternalProjectSubcontractsController(IQuantitySurveySubcontractService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Workspace(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, true, token)));

    [HttpPut("{id:guid}/valuations")]
    public Task<IActionResult> SaveValuation(Guid projectId, Guid id, [FromBody] SaveQuantitySurveySubcontractValuationRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.SaveValuationAsync(id, request, true, CorrelationId, token));

    [HttpPost("{id:guid}/valuations/{valuationId:guid}/evidence"), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid projectId, Guid id, Guid valuationId, [FromForm] Guid clientRequestId,
        [FromForm] string title, [FromForm] IFormFile file, CancellationToken token) => ExecuteAsync(async () =>
    {
        var workspace = await service.GetWorkspaceAsync(projectId, true, token);
        if (!workspace.Subcontracts.Any(value => value.Id == id)) return NotFound(Problem(404, "QS subcontract not found", "The subcontract was not found in this project."));
        if (file is null) return BadRequest(Problem(400, "QS subcontract validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadEvidenceAsync(id, valuationId, clientRequestId, title, file.FileName,
            file.ContentType, file.Length, () => stream, true, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content")]
    public Task<IActionResult> OpenEvidence(Guid projectId, Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        var workspace = await service.GetWorkspaceAsync(projectId, true, token);
        if (!workspace.Subcontracts.Any(value => value.Id == id)) return NotFound(Problem(404, "QS subcontract not found", "The subcontract was not found in this project."));
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, true, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpPost("{id:guid}/valuations/{valuationId:guid}/submit")]
    public Task<IActionResult> Submit(Guid projectId, Guid id, Guid valuationId,
        [FromBody] QuantitySurveySubcontractActionRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.SubmitValuationAsync(valuationId, request, true, CorrelationId, token));

    private Task<IActionResult> ExecuteForProjectAsync(Guid projectId, Guid id,
        Func<Task<QuantitySurveySubcontractValuationDto>> action) => ExecuteAsync(async () =>
    {
        var workspace = await service.GetWorkspaceAsync(projectId, true);
        if (!workspace.Subcontracts.Any(value => value.Id == id)) return NotFound(Problem(404, "QS subcontract not found", "The subcontract was not found in this project."));
        return Ok(await action());
    });
    private string CorrelationId => HttpContext.TraceIdentifier;
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveySubcontractNotFoundException exception) { return NotFound(Problem(404, "QS subcontract not found", exception.Message)); }
        catch (QuantitySurveySubcontractConflictException exception) { return Conflict(Problem(409, "QS subcontract conflict", exception.Message)); }
        catch (QuantitySurveySubcontractValidationException exception) { return BadRequest(Problem(400, "QS subcontract validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception) { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS subcontract evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, Problem(403, "QS subcontract access forbidden", exception.Message)); }
    }
    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    { Status = status, Title = title, Detail = detail, Type = $"https://tdc.gov.gh/problems/quantity-survey-external-subcontract-{status}",
        Instance = HttpContext.Request.Path, Extensions = { ["code"] = code ?? $"QS_EXTERNAL_SUBCONTRACT_{status}", ["correlationId"] = CorrelationId } };
}
