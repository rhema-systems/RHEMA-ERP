using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[ApiController]
[Authorize]
[Route("api/projects/civil-engineering/design-cases")]
public sealed class CivilEngineeringDesignCasesController(ICivilEngineeringDesignService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, token)));

    [HttpGet("{id:guid}/commercial-readiness"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> CommercialReadiness(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetCommercialReadinessAsync(id, token)));

    [HttpPost, Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public Task<IActionResult> Create(
        [FromBody] CreateCivilEngineeringDesignCaseRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateAsync(request, CorrelationId, token)));

    [HttpPost("{id:guid}/transition"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public Task<IActionResult> Transition(
        Guid id,
        [FromBody] CivilEngineeringDesignTransitionRequest request,
        CancellationToken token)
    {
        if (request.Action is CivilEngineeringDesignAction.Approve or CivilEngineeringDesignAction.Reject)
            return Task.FromResult<IActionResult>(BadRequest(Problem(
                400,
                "Civil design transition validation failed",
                "Use the protected decision endpoint for final approval or rejection.")));
        return ExecuteAsync(async () => Ok(await service.TransitionAsync(id, request, CorrelationId, token)));
    }

    [HttpPost("{id:guid}/decision"), Authorize(Policy = CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Decide(
        Guid id,
        [FromBody] CivilEngineeringDesignTransitionRequest request,
        CancellationToken token)
    {
        if (request.Action is not (CivilEngineeringDesignAction.Approve or CivilEngineeringDesignAction.Reject))
            return Task.FromResult<IActionResult>(BadRequest(Problem(
                400,
                "Civil design decision validation failed",
                "Select Approve or Reject for the final decision.")));
        return ExecuteAsync(async () => Ok(await service.TransitionAsync(id, request, CorrelationId, token)));
    }

    [HttpGet("{id:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    [HttpGet("{id:guid}/reconnaissance"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> ListReconnaissance(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListReconnaissanceAsync(id, token)));

    [HttpPost("{id:guid}/reconnaissance"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public Task<IActionResult> CreateReconnaissance(
        Guid id,
        [FromBody] CreateCivilEngineeringReconnaissanceRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateReconnaissanceAsync(id, request, CorrelationId, token)));

    [HttpPut("{id:guid}/reconnaissance/{reportId:guid}"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public Task<IActionResult> UpdateReconnaissance(
        Guid id,
        Guid reportId,
        [FromBody] UpdateCivilEngineeringReconnaissanceRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.UpdateReconnaissanceAsync(id, reportId, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reconnaissance/{reportId:guid}/complete"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public Task<IActionResult> CompleteReconnaissance(
        Guid id,
        Guid reportId,
        [FromBody] CompleteCivilEngineeringReconnaissanceRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CompleteReconnaissanceAsync(id, reportId, request, CorrelationId, token)));

    [HttpGet("{id:guid}/information-requests"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> ListInformationRequests(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListInformationRequestsAsync(id, token)));

    [HttpGet("information-requests/assigned"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignInputRespond)]
    public Task<IActionResult> ListAssignedInformationRequests(CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAssignedInformationRequestsAsync(token)));

    [HttpPost("{id:guid}/information-requests"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public Task<IActionResult> CreateInformationRequest(
        Guid id,
        [FromBody] CreateCivilEngineeringDesignInputRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateInformationRequestAsync(id, request, CorrelationId, token)));

    [HttpPost("information-requests/{requestId:guid}/response"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignInputRespond)]
    public Task<IActionResult> SubmitInformationResponse(
        Guid requestId,
        [FromBody] SubmitCivilEngineeringDesignInputResponseRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitInformationResponseAsync(requestId, request, CorrelationId, token)));

    [HttpPost("information-requests/{requestId:guid}/review"), Authorize(Policy = CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> ReviewInformationResponse(
        Guid requestId,
        [FromBody] ReviewCivilEngineeringDesignInputRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ReviewInformationResponseAsync(requestId, request, CorrelationId, token)));

    [HttpGet("{id:guid}/documents/lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> DocumentLookups(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetDocumentLookupsAsync(id, token)));

    [HttpGet("{id:guid}/documents"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> ListDocuments(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListDocumentsAsync(id, token)));

    [HttpPost("{id:guid}/documents"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DocumentsManage)]
    public Task<IActionResult> CreateDocument(
        Guid id,
        [FromBody] CreateCivilEngineeringDocumentRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreateDocumentAsync(id, request, CorrelationId, token)));

    [HttpPost("documents/{documentId:guid}/submit"), Authorize(Policy = CivilEngineeringAccessControlRegistry.DocumentsManage)]
    public Task<IActionResult> SubmitDocument(
        Guid documentId,
        [FromBody] SubmitCivilEngineeringDocumentRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitDocumentAsync(documentId, request, CorrelationId, token)));

    [HttpPost("documents/{documentId:guid}/decision"), Authorize(Policy = CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> ReviewDocument(
        Guid documentId,
        [FromBody] ReviewCivilEngineeringDocumentRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ReviewDocumentAsync(documentId, request, CorrelationId, token)));

    [HttpGet("{id:guid}/planning-gis/lookups"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> PlanningGisLookups(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetPlanningGisLookupsAsync(id, token)));

    [HttpGet("{id:guid}/planning-gis"), Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> ListPlanningGis(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListPlanningGisValidationsAsync(id, token)));

    [HttpPost("{id:guid}/planning-gis"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> CreatePlanningGis(
        Guid id,
        [FromBody] CreateCivilEngineeringPlanningGisValidationRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.CreatePlanningGisValidationAsync(id, request, CorrelationId, token)));

    [HttpPost("planning-gis/{validationId:guid}/submit"), Authorize(Policy = CivilEngineeringAccessControlRegistry.PermittingManage)]
    public Task<IActionResult> SubmitPlanningGis(
        Guid validationId,
        [FromBody] CivilEngineeringPlanningGisSubmitRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitPlanningGisValidationAsync(validationId, request, CorrelationId, token)));

    [HttpPost("planning-gis/{validationId:guid}/decision"), Authorize(Policy = CivilEngineeringAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> DecidePlanningGis(
        Guid validationId,
        [FromBody] CivilEngineeringPlanningGisDecisionRequest request,
        CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.DecidePlanningGisValidationAsync(validationId, request, CorrelationId, token)));

    [HttpGet("planning-gis/{validationId:guid}/history"), Authorize(Policy = CivilEngineeringAccessControlRegistry.AuditRead)]
    public Task<IActionResult> PlanningGisHistory(Guid validationId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.PlanningGisHistoryAsync(validationId, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (CivilEngineeringDesignNotFoundException exception)
        { return NotFound(Problem(404, "Civil design case not found", exception.Message)); }
        catch (CivilEngineeringDesignConflictException exception)
        { return Conflict(Problem(409, "Civil design case conflict", exception.Message)); }
        catch (CivilEngineeringDesignValidationException exception)
        { return BadRequest(Problem(400, "Civil design case validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "Civil design case access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/civil-engineering-design-case-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"CIVIL_DESIGN_CASE_{status}",
            ["correlationId"] = CorrelationId
        }
    };
}
