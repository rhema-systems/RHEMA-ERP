using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/contract-claims")]
public sealed class QuantitySurveyContractClaimsController(IQuantitySurveyContractClaimService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, false, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, false, token)));

    [HttpPost("{id:guid}/evidence"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ClaimsManage), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid id, [FromForm] Guid clientRequestId, [FromForm] string title,
        [FromForm] IFormFile file, CancellationToken token) => ExecuteAsync(async () =>
    {
        if (file is null) return BadRequest(Problem(400, "QS claim validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadEvidenceAsync(id, clientRequestId, title, file.FileName,
            file.ContentType, file.Length, () => stream, false, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> OpenEvidence(Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, false, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpPost("{id:guid}/vet"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ClaimsManage)]
    public Task<IActionResult> Vet(Guid id, [FromBody] VetQuantitySurveyContractClaimRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.VetAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit-approval"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ClaimsManage)]
    public Task<IActionResult> SubmitApproval(Guid id, [FromBody] QuantitySurveyContractClaimActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitApprovalAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id, [FromBody] QuantitySurveyContractClaimActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideAsync(id, request, true, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id, [FromBody] QuantitySurveyContractClaimActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecideAsync(id, request, false, CorrelationId, token)));

    [HttpPost("{id:guid}/dispute/accept"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> AcceptDispute(Guid id, [FromBody] QuantitySurveyContractClaimActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ResolveDisputeAsync(id, request, true, CorrelationId, token)));

    [HttpPost("{id:guid}/dispute/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> RejectDispute(Guid id, [FromBody] QuantitySurveyContractClaimActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ResolveDisputeAsync(id, request, false, CorrelationId, token)));

    [HttpPost("{id:guid}/settle"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ClaimsManage)]
    public Task<IActionResult> Settle(Guid id, [FromBody] SettleQuantitySurveyContractClaimRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SettleAsync(id, request, CorrelationId, token)));

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyContractClaimNotFoundException exception)
        { return NotFound(Problem(404, "QS claim not found", exception.Message)); }
        catch (QuantitySurveyContractClaimConflictException exception)
        { return Conflict(Problem(409, "QS claim conflict", exception.Message)); }
        catch (QuantitySurveyContractClaimValidationException exception)
        { return BadRequest(Problem(400, "QS claim validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS claim evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS claim access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-contract-claim-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_CONTRACT_CLAIM_{status}", ["correlationId"] = CorrelationId }
    };
}

[ApiController]
[Authorize]
[Route("api/projects/external/my-projects/{projectId:guid}/contract-claims")]
public sealed class ExternalProjectContractClaimsController(IQuantitySurveyContractClaimService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Workspace(Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(projectId, true, token)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid projectId, Guid id, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.GetAsync(id, true, token));

    [HttpPut]
    public Task<IActionResult> Save(Guid projectId, [FromBody] SaveQuantitySurveyContractClaimRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SaveExternalAsync(projectId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/evidence"), RequestSizeLimit(52_428_800)]
    public Task<IActionResult> Evidence(Guid projectId, Guid id, [FromForm] Guid clientRequestId,
        [FromForm] string title, [FromForm] IFormFile file, CancellationToken token) => ExecuteAsync(async () =>
    {
        var current = await service.GetAsync(id, true, token);
        if (current.ProjectId != projectId) return NotFound(Problem(404, "QS claim not found", "The claim was not found in this project."));
        if (file is null) return BadRequest(Problem(400, "QS claim validation failed", "Select an evidence file."));
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadEvidenceAsync(id, clientRequestId, title, file.FileName,
            file.ContentType, file.Length, () => stream, true, CorrelationId, token));
    });

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content")]
    public Task<IActionResult> OpenEvidence(Guid projectId, Guid id, Guid evidenceId, CancellationToken token) => ExecuteAsync(async () =>
    {
        var current = await service.GetAsync(id, true, token);
        if (current.ProjectId != projectId) return NotFound(Problem(404, "QS claim not found", "The claim was not found in this project."));
        await using var content = await service.OpenEvidenceAsync(id, evidenceId, true, token);
        await using var buffer = new MemoryStream(); await content.Content.CopyToAsync(buffer, token);
        return File(buffer.ToArray(), content.ContentType, content.FileName);
    });

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid projectId, Guid id, [FromBody] QuantitySurveyContractClaimActionRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.SubmitExternalAsync(id, request, CorrelationId, token));

    [HttpPost("{id:guid}/dispute")]
    public Task<IActionResult> Dispute(Guid projectId, Guid id, [FromBody] QuantitySurveyContractClaimActionRequest request, CancellationToken token) =>
        ExecuteForProjectAsync(projectId, id, () => service.OpenDisputeExternalAsync(id, request, CorrelationId, token));

    private Task<IActionResult> ExecuteForProjectAsync(Guid projectId, Guid id,
        Func<Task<QuantitySurveyContractClaimDto>> action) => ExecuteAsync(async () =>
    {
        var current = await service.GetAsync(id, true);
        if (current.ProjectId != projectId)
            return NotFound(Problem(404, "QS claim not found", "The claim was not found in this project."));
        return Ok(await action());
    });

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyContractClaimNotFoundException exception)
        { return NotFound(Problem(404, "QS claim not found", exception.Message)); }
        catch (QuantitySurveyContractClaimConflictException exception)
        { return Conflict(Problem(409, "QS claim conflict", exception.Message)); }
        catch (QuantitySurveyContractClaimValidationException exception)
        { return BadRequest(Problem(400, "QS claim validation failed", exception.Message)); }
        catch (ControlledFileUploadException exception)
        { return StatusCode(exception.StatusCode, Problem(exception.StatusCode, "QS claim evidence rejected", exception.Message, exception.Code)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS claim access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-external-contract-claim-{status}",
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code ?? $"QS_EXTERNAL_CONTRACT_CLAIM_{status}", ["correlationId"] = CorrelationId }
    };
}
