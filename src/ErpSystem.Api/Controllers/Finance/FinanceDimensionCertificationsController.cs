using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/finance/dimensions/certifications")]
public sealed class FinanceDimensionCertificationsController : ControllerBase
{
    private readonly IFinanceDimensionCertificationService _service;

    public FinanceDimensionCertificationsController(IFinanceDimensionCertificationService service) =>
        _service = service;

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<FinanceDimensionRouteCertificationDto>>> GetRoutes(
        CancellationToken cancellationToken) => Ok(await _service.GetRoutesAsync(cancellationToken));

    [HttpPost("{routeId}/readiness")]
    [Authorize(Policy = FinancePermissions.ManageDimensionCertification)]
    public async Task<ActionResult<FinanceDimensionReadinessAssessmentDto>> AssessReadiness(
        FinanceDimensionRouteId routeId,
        [FromBody] CreateFinanceDimensionReadinessAssessmentDto request,
        CancellationToken cancellationToken) =>
        Ok(await _service.AssessReadinessAsync(routeId, request.TargetState, cancellationToken));

    [HttpGet("readiness/{assessmentId:guid}")]
    [Authorize(Policy = FinancePermissions.ManageDimensionCertification)]
    public async Task<ActionResult<FinanceDimensionReadinessAssessmentDto>> GetAssessment(
        Guid assessmentId,
        CancellationToken cancellationToken) => Ok(await _service.GetAssessmentAsync(assessmentId, cancellationToken));

    [HttpGet("readiness/{assessmentId:guid}/csv")]
    [Authorize(Policy = FinancePermissions.ManageDimensionCertification)]
    public async Task<IActionResult> ExportReadiness(Guid assessmentId, CancellationToken cancellationToken) =>
        File(await _service.ExportReadinessCsvAsync(assessmentId, cancellationToken), "text/csv; charset=utf-8",
            $"finance-dimension-readiness-{assessmentId:N}.csv");

    [HttpPost("{routeId}/promote")]
    [Authorize(Policy = FinancePermissions.ManageDimensionCertification)]
    public async Task<ActionResult<FinanceDimensionRouteCertificationDto>> Promote(
        FinanceDimensionRouteId routeId,
        [FromBody] PromoteFinanceDimensionRouteDto request,
        CancellationToken cancellationToken) => Ok(await _service.PromoteAsync(routeId, request, cancellationToken));
}
