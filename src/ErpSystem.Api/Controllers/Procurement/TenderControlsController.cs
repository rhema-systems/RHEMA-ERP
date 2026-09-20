using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/tenders/{tenderId:guid}/controls")]
public sealed class TenderControlsController : ControllerBase
{
    private readonly IProcurementTenderControlService _service;

    public TenderControlsController(IProcurementTenderControlService service) => _service = service;

    [HttpGet]
    public Task<IActionResult> Get(Guid tenderId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.GetAsync(tenderId, cancellationToken));

    [HttpPost("advertise")]
    [Authorize(Policy = "procurement.tender.administer")]
    public Task<IActionResult> Advertise(Guid tenderId, PublishProcurementTenderRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.PublishAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("document-issues")]
    [Authorize(Policy = "procurement.tender.administer")]
    public Task<IActionResult> IssueDocument(Guid tenderId, IssueProcurementTenderDocumentRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.IssueDocumentAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("opening")]
    [Authorize(Policy = "procurement.tender.administer")]
    public Task<IActionResult> CompleteOpening(Guid tenderId, CompleteProcurementTenderOpeningRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.CompleteOpeningAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPut("technical-evaluation")]
    [Authorize(Policy = "procurement.tender.evaluate")]
    public Task<IActionResult> SaveTechnical(Guid tenderId, SaveProcurementTenderTechnicalEvaluationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SaveTechnicalEvaluationAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPut("financial-evaluation")]
    [Authorize(Policy = "procurement.tender.evaluate")]
    public Task<IActionResult> SaveFinancial(Guid tenderId, SaveProcurementTenderFinancialEvaluationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SaveFinancialEvaluationAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("approval/submit")]
    [Authorize(Policy = "procurement.tender.approve")]
    public Task<IActionResult> SubmitApproval(Guid tenderId, SubmitProcurementTenderApprovalRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.SubmitApprovalAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("approval/decision")]
    [Authorize(Policy = "procurement.tender.approve")]
    public Task<IActionResult> DecideApproval(Guid tenderId, DecideProcurementTenderApprovalRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.DecideApprovalAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("award")]
    [Authorize(Policy = "procurement.tender.approve")]
    public Task<IActionResult> RecordAward(Guid tenderId, RecordProcurementTenderAwardRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordAwardAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("contract")]
    [Authorize(Policy = "procurement.contract.manage")]
    public Task<IActionResult> RecordContract(Guid tenderId, RecordProcurementTenderContractRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordContractAsync(tenderId, request, Correlation(), cancellationToken));

    [HttpPost("acceptance")]
    [Authorize(Policy = "procurement.contract.manage")]
    public Task<IActionResult> RecordAcceptance(Guid tenderId, RecordProcurementTenderAcceptanceRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => _service.RecordAcceptanceAsync(tenderId, request, Correlation(), cancellationToken));

    private string Correlation() =>
        Request.Headers.TryGetValue("X-Correlation-ID", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.NewGuid().ToString("N");

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (TenderEvaluationConfigurationException configurationError)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = 422, Title = "Evaluation configuration needs correction", Detail = configurationError.Message,
                Extensions = { ["code"] = configurationError.Code }
            });
        }
        catch (ProcurementTenderControlNotFoundException exception)
        { return NotFound(ControlProblem(404, exception.Code, exception.Message)); }
        catch (ProcurementTenderControlConflictException exception)
        { return Conflict(ControlProblem(409, exception.Code, exception.Message)); }
        catch (ProcurementTenderControlValidationException exception)
        { return UnprocessableEntity(ControlProblem(422, exception.Code, exception.Message)); }
        catch (ProcurementTenderControlAuthorizationException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, ControlProblem(403, "TENDER_CONTROL_FORBIDDEN", exception.Message)); }
        catch (ProcurementAwardReadinessNotFoundException exception)
        { return NotFound(ReadinessProblem(404, exception.Code, exception.Message)); }
        catch (ProcurementAwardReadinessAuthorizationException exception)
        { return StatusCode(StatusCodes.Status403Forbidden, ReadinessProblem(403, "AWARD_READINESS_ACCESS_FORBIDDEN", exception.Message)); }
        catch (ProcurementAwardReadinessConflictException exception)
        { return Conflict(ReadinessProblem(409, exception.Code, exception.Message)); }
        catch (ProcurementAwardReadinessBlockedException exception)
        { return UnprocessableEntity(ReadinessProblem(422, exception.Code, exception.Message, exception.Decision)); }
        catch (ProcurementAwardReadinessValidationException exception)
        { return UnprocessableEntity(ReadinessProblem(422, exception.Code, exception.Message)); }
    }

    private object ReadinessProblem(int status, string code, string message, object? decision = null) => new
    {
        status,
        title = status == 422 ? "Award is not ready" : "Award-readiness check failed",
        detail = message,
        instance = Request.Path.Value,
        code,
        correlationId = Correlation(),
        decision
    };

    private ProblemDetails ControlProblem(int status, string code, string detail) => new()
    {
        Status = status,
        Title = status == 422 ? "Tender lifecycle control failed" : "Tender lifecycle request failed",
        Detail = detail,
        Instance = Request.Path.Value,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = Correlation()
        }
    };
}
