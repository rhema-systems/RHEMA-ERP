using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/payment-certificates")]
public sealed class QuantitySurveyPaymentCertificatesController(
    IQuantitySurveyPaymentCertificateService service) : ControllerBase
{
    [HttpGet("lookups"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Lookups([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetLookupsAsync(projectId, token)));

    [HttpGet, Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(projectId, token)));

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, token)));

    [HttpPost, Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Generate([FromQuery] Guid projectId,
        [FromBody] GenerateQuantitySurveyPaymentCertificateRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.GenerateAsync(projectId, request, CorrelationId, token)));

    [HttpPut("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Update(Guid id,
        [FromBody] UpdateQuantitySurveyPaymentCertificateRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.UpdateAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/submit"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> Submit(Guid id,
        [FromBody] QuantitySurveyPaymentCertificateActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(Guid id,
        [FromBody] QuantitySurveyPaymentCertificateActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(Guid id,
        [FromBody] QuantitySurveyPaymentCertificateActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/handoff-ap"), Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public Task<IActionResult> HandoffToAp(Guid id,
        [FromBody] QuantitySurveyPaymentCertificateActionRequest request, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HandoffToApAsync(id, request, CorrelationId, token)));

    [HttpPost("{id:guid}/refresh-payment"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> RefreshPayment(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.RefreshPaymentStatusAsync(id, CorrelationId, token)));

    [HttpGet("{id:guid}/document"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> Document(Guid id, CancellationToken token) => ExecuteAsync(async () =>
    {
        var document = await service.RenderAsync(id, CorrelationId, token);
        return File(document.Content, document.ContentType, document.FileName);
    });

    [HttpGet("{id:guid}/history"), Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken token) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, token)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyPaymentCertificateNotFoundException exception)
        { return NotFound(Problem(404, "QS payment certificate not found", exception.Message)); }
        catch (QuantitySurveyPaymentCertificateConflictException exception)
        { return Conflict(Problem(409, "QS payment certificate conflict", exception.Message)); }
        catch (QuantitySurveyPaymentCertificateValidationException exception)
        { return BadRequest(Problem(400, "QS payment certificate validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS payment certificate access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status, Title = title, Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-payment-certificate-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_PAYMENT_CERTIFICATE_{status}",
            ["correlationId"] = CorrelationId
        }
    };
}
