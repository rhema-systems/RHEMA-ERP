using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/contract-commercial-terms")]
public sealed class QuantitySurveyContractCommercialTermsController(
    IQuantitySurveyContractCommercialTermsService service) : ControllerBase
{
    [HttpGet("{contractId:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> Workspace(Guid contractId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await service.GetWorkspaceAsync(contractId, cancellationToken)));

    [HttpPut("{contractId:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public Task<IActionResult> Configure(Guid contractId,
        [FromBody] ConfigureQuantitySurveyContractCommercialTermsRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
            Ok(await service.ConfigureAsync(contractId, request, HttpContext.TraceIdentifier, cancellationToken)));

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (QuantitySurveyContractCommercialTermsNotFoundException exception)
        { return NotFound(Problem(404, "QS contract commercial terms not found", exception.Message)); }
        catch (QuantitySurveyContractCommercialTermsConflictException exception)
        { return Conflict(Problem(409, "QS contract commercial terms conflict", exception.Message)); }
        catch (QuantitySurveyContractCommercialTermsValidationException exception)
        { return BadRequest(Problem(400, "QS contract commercial terms validation failed", exception.Message)); }
        catch (UnauthorizedAccessException exception)
        { return StatusCode(403, Problem(403, "QS contract commercial terms access forbidden", exception.Message)); }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-contract-commercial-terms-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_CONTRACT_COMMERCIAL_TERMS_{status}",
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}
