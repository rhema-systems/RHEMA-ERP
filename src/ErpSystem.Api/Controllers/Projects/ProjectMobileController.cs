using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[Authorize(Policy = "InternalOnly")]
[ApiController]
[Route("api/mobile/projects")]
public class ProjectMobileController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectMobileController(IProjectService projectService, ICurrentUserProvider currentUserProvider)
    {
        _projectService = projectService;
        _currentUserProvider = currentUserProvider;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ProjectMobileSummaryDto>> GetSummary()
        => Ok(await _projectService.GetMobileSummaryAsync(_currentUserProvider.UserId));

    [HttpPost("{projectId:guid}/timesheets")]
    public async Task<ActionResult<ProjectTimesheetEntryDto>> SubmitTimesheet(Guid projectId, [FromBody] CreateProjectTimesheetEntryDto dto)
    {
        if (!dto.WorkItemId.HasValue)
        {
            return BadRequest("A work item is required for mobile timesheet submission.");
        }

        return Ok(await _projectService.SubmitMobileTimesheetAsync(projectId, dto.WorkItemId.Value, dto, _currentUserProvider.UserId));
    }

    [HttpPost("{projectId:guid}/expenses")]
    public async Task<ActionResult<ProjectExpenseDto>> SubmitExpense(Guid projectId, [FromBody] CreateProjectExpenseDto dto)
    {
        if (!dto.WorkItemId.HasValue)
        {
            return BadRequest("A work item is required for mobile expense submission.");
        }

        return Ok(await _projectService.SubmitMobileExpenseAsync(projectId, dto.WorkItemId.Value, dto, _currentUserProvider.UserId));
    }

    [HttpPut("{projectId:guid}/work-items/{workItemId:guid}/progress")]
    public async Task<ActionResult<ProjectWorkItemDto>> UpdateProgress(Guid projectId, Guid workItemId, [FromBody] UpdateProjectWorkItemProgressDto dto)
        => Ok(await _projectService.UpdateMobileWorkItemProgressAsync(projectId, workItemId, dto, _currentUserProvider.UserId));
}
