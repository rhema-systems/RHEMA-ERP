using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/price-index-imports")]
public sealed class QuantitySurveyPriceIndexImportsController(
    IQuantitySurveyPriceIndexImportService service) : ControllerBase
{
    [HttpGet("template/{indexFamilyId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Template(Guid indexFamilyId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await service.CreateTemplateAsync(indexFamilyId, cancellationToken);
            return File(value.Content, value.ContentType, value.FileName);
        });

    [HttpPost("stage/{indexFamilyId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    [Consumes("multipart/form-data")]
    // The governed source file limit is 10 MiB. Allow multipart framing above
    // that value so a valid 10 MiB file reaches the service-level size guard.
    [RequestSizeLimit(11_534_336)]
    public Task<IActionResult> Stage(
        Guid indexFamilyId,
        [FromForm] IFormFile file,
        [FromForm] Guid clientRequestId,
        [FromForm] Guid authorityRoleId,
        [FromForm] string reason,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        if (file is null || file.Length == 0)
            return BadRequest(Problem(400, "Price-index source required", "Select a non-empty controlled price-index source file."));

        await using var stream = file.OpenReadStream();
        return Ok(await service.StageAsync(
            indexFamilyId,
            stream,
            Path.GetFileName(file.FileName),
            file.ContentType,
            new StageQuantitySurveyPriceIndexImportRequest
            {
                ClientRequestId = clientRequestId,
                AuthorityRoleId = authorityRoleId,
                Reason = reason
            },
            CorrelationId,
            cancellationToken));
    });

    [HttpGet]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> List(
        [FromQuery] QuantitySurveyPriceIndexImportListRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.RatesManage)]
    public Task<IActionResult> Submit(
        Guid id,
        [FromBody] QuantitySurveyPriceIndexImportLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await service.SubmitAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Approve(
        Guid id,
        [FromBody] QuantitySurveyPriceIndexImportLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await service.ApproveAsync(id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<IActionResult> Reject(
        Guid id,
        [FromBody] QuantitySurveyPriceIndexImportLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await service.RejectAsync(id, request, CorrelationId, cancellationToken)));

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.AuditRead)]
    public Task<IActionResult> History(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await service.HistoryAsync(id, cancellationToken)));

    private string CorrelationId => HttpContext.TraceIdentifier;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ControlledFileUploadException exception)
        {
            return StatusCode(exception.StatusCode, Problem(
                exception.StatusCode,
                "Price-index source upload failed",
                exception.Message,
                exception.Code));
        }
        catch (QuantitySurveyPriceIndexImportNotFoundException exception)
        {
            return NotFound(Problem(404, "Price-index import not found", exception.Message));
        }
        catch (QuantitySurveyPriceIndexImportConflictException exception)
        {
            return Conflict(Problem(409, "Price-index import conflict", exception.Message));
        }
        catch (QuantitySurveyPriceIndexImportValidationException exception)
        {
            return BadRequest(Problem(400, "Price-index import validation failed", exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "Price-index import access forbidden", exception.Message));
        }
    }

    private ProblemDetails Problem(int status, string title, string detail, string? code = null) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-price-index-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code ?? $"QS_PRICE_INDEX_{status}",
            ["correlationId"] = CorrelationId
        }
    };
}
