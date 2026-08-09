using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// FR-RP-012 HTTP boundary. This controller deliberately accepts only declarative field,
/// filter, sort, and aggregation keys; SQL remains private to the server-owned Finance catalogue.
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/ad-hoc-reports")]
public sealed class FinanceAdHocReportsController : ControllerBase
{
    private readonly IFinanceAdHocReportService _adHocReports;
    private readonly IReportsService _reports;
    private readonly ICurrentUserService _currentUser;

    public FinanceAdHocReportsController(
        IFinanceAdHocReportService adHocReports,
        IReportsService reports,
        ICurrentUserService currentUser)
    {
        _adHocReports = adHocReports;
        _reports = reports;
        _currentUser = currentUser;
    }

    [HttpGet("workspace")]
    [Authorize(Policy = FinancePermissions.BuildAdHocReports)]
    public async Task<ActionResult<FinanceAdHocReportWorkspaceDto>> GetWorkspace(
        CancellationToken cancellationToken = default) =>
        Ok(await _adHocReports.GetWorkspaceAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.BuildAdHocReports)]
    public async Task<ActionResult<FinanceAdHocReportDefinitionDto>> Get(
        Guid id, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _adHocReports.GetAsync(id, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = FinancePermissions.BuildAdHocReports)]
    public async Task<ActionResult<FinanceAdHocReportDefinitionDto>> Create(
        [FromBody] CreateFinanceAdHocReportDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await _adHocReports.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = definition.Id }, definition);
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, new { message = exception.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = FinancePermissions.BuildAdHocReports)]
    public async Task<ActionResult<FinanceAdHocReportDefinitionDto>> Update(
        Guid id,
        [FromBody] UpdateFinanceAdHocReportDto request,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _adHocReports.UpdateAsync(id, request, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, new { message = exception.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = FinancePermissions.BuildAdHocReports)]
    public async Task<IActionResult> Delete(
        Guid id, [FromQuery] string rowVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            await _adHocReports.DeleteAsync(id, rowVersion, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, new { message = exception.Message }); }
    }

    [HttpPost("{id:guid}/execute")]
    [Authorize(Policy = FinancePermissions.RunFinanceReports)]
    public async Task<ActionResult<ReportResultDto>> Execute(
        Guid id, [FromBody] ExecuteReportDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve through the Finance service first, then use the shared engine so execution
            // history, elapsed time, and all provider security checks remain consistent.
            var definition = await _adHocReports.GetAsync(id, cancellationToken);
            return Ok(await _reports.ExecuteReportAsync(
                definition.ReportId, request, TenantId(), UserId(), IsReportAdministrator()));
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, new { message = exception.Message }); }
    }

    [HttpPost("{id:guid}/export")]
    [Authorize(Policy = FinancePermissions.ExportFinanceReports)]
    public async Task<IActionResult> Export(
        Guid id, [FromBody] ExportReportDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await _adHocReports.GetAsync(id, cancellationToken);
            var result = await _reports.ExportReportAsync(
                definition.ReportId, request, TenantId(), UserId(), IsReportAdministrator());
            return File(result.Data, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(403, new { message = exception.Message }); }
    }

    private Guid TenantId() => _currentUser.GetRequiredFinanceTenantId();
    private Guid UserId() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id : throw new InvalidOperationException("Finance ad hoc reporting requires an authenticated user.");
    private bool IsReportAdministrator() => _currentUser.IsInRole("SuperAdmin")
        || _currentUser.IsInRole("TenantAdmin") || _currentUser.IsInRole("SystemAdmin");
}
