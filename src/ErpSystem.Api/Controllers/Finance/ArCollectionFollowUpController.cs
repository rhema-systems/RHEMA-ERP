using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Finance AR collection workspace for FR-AR-009. The controller exposes only
/// Finance-owned follow-up behavior; Legal case management and external delivery
/// provider configuration remain explicit hand-off boundaries.
/// </summary>
[ApiController]
[Route("api/finance/ar/collections")]
[Authorize]
public sealed class ArCollectionFollowUpController : ControllerBase
{
    private readonly IArCollectionFollowUpService _service;

    public ArCollectionFollowUpController(IArCollectionFollowUpService service)
    {
        _service = service;
    }

    [HttpGet("work-queue")]
    [Authorize(Policy = FinancePermissions.ViewArCollections)]
    public async Task<ActionResult<ArCollectionWorkQueueDto>> GetWorkQueue(
        [FromQuery] DateTime? asOfDate = null,
        [FromQuery] int minimumDaysOverdue = 1,
        [FromQuery] string? status = null,
        [FromQuery] Guid? assignedToId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetWorkQueueAsync(
            asOfDate,
            minimumDaysOverdue,
            status,
            assignedToId,
            search,
            page,
            pageSize,
            cancellationToken));

    [HttpGet("summary")]
    [Authorize(Policy = FinancePermissions.ViewArCollections)]
    public async Task<ActionResult<ArCollectionSummaryDto>> GetSummary(
        [FromQuery] DateTime? asOfDate = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetSummaryAsync(asOfDate, cancellationToken));

    [HttpGet("assignees")]
    [Authorize(Policy = FinancePermissions.ViewArCollections)]
    public async Task<ActionResult<IReadOnlyList<ArCollectionAssigneeDto>>> GetAssignees(
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAssigneesAsync(cancellationToken));

    [HttpPost("tasks/generate")]
    [Authorize(Policy = FinancePermissions.ManageArCollections)]
    public async Task<ActionResult<GenerateArCollectionTasksResultDto>> GenerateTasks(
        [FromBody] GenerateArCollectionTasksDto request,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GenerateTasksAsync(request, cancellationToken));

    [HttpPost("tasks")]
    [Authorize(Policy = FinancePermissions.ManageArCollections)]
    public async Task<ActionResult<ArCollectionWorkItemDto>> CreateTask(
        [FromBody] CreateArCollectionTaskDto request,
        CancellationToken cancellationToken = default)
    {
        var item = await _service.CreateTaskAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetHistory), new { taskId = item.TaskId }, item);
    }

    [HttpPut("tasks/{taskId:guid}")]
    [Authorize(Policy = FinancePermissions.ManageArCollections)]
    public async Task<ActionResult<ArCollectionWorkItemDto>> UpdateTask(
        Guid taskId,
        [FromBody] UpdateArCollectionTaskDto request,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.UpdateTaskAsync(taskId, request, cancellationToken));

    [HttpPost("tasks/{taskId:guid}/reminders")]
    [Authorize(Policy = FinancePermissions.RecordArCollectionReminders)]
    public async Task<ActionResult<ArCollectionHistoryItemDto>> RecordReminder(
        Guid taskId,
        [FromBody] RecordArCollectionReminderDto request,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.RecordReminderAsync(taskId, request, cancellationToken));

    [HttpGet("tasks/{taskId:guid}/history")]
    [Authorize(Policy = FinancePermissions.ViewArCollections)]
    public async Task<ActionResult<IReadOnlyList<ArCollectionHistoryItemDto>>> GetHistory(
        Guid taskId,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetHistoryAsync(taskId, cancellationToken));
}
