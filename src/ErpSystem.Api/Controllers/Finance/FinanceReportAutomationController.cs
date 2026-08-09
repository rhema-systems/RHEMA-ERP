using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Governed Finance report scheduling and retained-delivery workspace for
/// FR-RP-010. Reports remain discoverable under the shared Reports menu.
/// </summary>
[Authorize]
[ApiController]
[Route("api/finance/report-automation")]
public sealed class FinanceReportAutomationController : ControllerBase
{
    private readonly IFinanceReportAutomationService _service;

    public FinanceReportAutomationController(IFinanceReportAutomationService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewReportSchedules)]
    public async Task<ActionResult<FinanceReportAutomationWorkspaceDto>> GetWorkspace(CancellationToken cancellationToken) =>
        Ok(await _service.GetWorkspaceAsync(cancellationToken));

    [HttpPost("schedules")]
    [Authorize(Policy = FinancePermissions.ManageReportSchedules)]
    public async Task<ActionResult<FinanceReportScheduleDto>> Create(
        [FromBody] CreateFinanceReportScheduleDto request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetWorkspace), new { id = result.Id }, result);
    }

    [HttpPut("schedules/{id:guid}")]
    [Authorize(Policy = FinancePermissions.ManageReportSchedules)]
    public async Task<ActionResult<FinanceReportScheduleDto>> Update(
        Guid id, [FromBody] UpdateFinanceReportScheduleDto request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/pause")]
    [Authorize(Policy = FinancePermissions.ManageReportSchedules)]
    public async Task<ActionResult<FinanceReportScheduleDto>> Pause(
        Guid id, [FromBody] FinanceReportScheduleDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.PauseAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/resume")]
    [Authorize(Policy = FinancePermissions.ManageReportSchedules)]
    public async Task<ActionResult<FinanceReportScheduleDto>> Resume(
        Guid id, [FromBody] FinanceReportScheduleDecisionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ResumeAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/run-now")]
    [Authorize(Policy = FinancePermissions.RunReportSchedules)]
    public async Task<ActionResult<FinanceReportAutomationProcessResultDto>> RunNow(
        Guid id, CancellationToken cancellationToken) => Ok(await _service.RunNowAsync(id, cancellationToken));

    [HttpPost("process-due")]
    [Authorize(Policy = FinancePermissions.RunReportSchedules)]
    public async Task<ActionResult<FinanceReportAutomationProcessResultDto>> ProcessDue(CancellationToken cancellationToken) =>
        Ok(await _service.ProcessDueAsync(DateTime.UtcNow, cancellationToken));

    [HttpGet("artifacts/{exportId:guid}")]
    [Authorize(Policy = FinancePermissions.ViewReportSchedules)]
    public async Task<IActionResult> DownloadArtifact(Guid exportId, CancellationToken cancellationToken)
    {
        // The file is streamed only after the service checks both tenant and
        // export ownership; the storage path is never exposed to the browser.
        var artifact = await _service.DownloadArtifactAsync(exportId, cancellationToken);
        return File(artifact.Content, artifact.ContentType, artifact.FileName);
    }
}
