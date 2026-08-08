using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/onboarding-plans")]
[Authorize]
public class OnboardingPlanController : ControllerBase
{
    private readonly IOnboardingPlanService _service;
    private readonly ICurrentUserService _currentUser;

    public OnboardingPlanController(IOnboardingPlanService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // PLAN QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OnboardingPlanDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<OnboardingPlanDto?>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<OnboardingPlanDetailDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<OnboardingPlanSummaryDto>>> GetByStatus(OnboardingStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("{planId:guid}/overdue-tasks/count")]
    public async Task<ActionResult<int>> GetOverdueTasksCount(Guid planId)
        => Ok(await _service.GetOverdueTasksCountAsync(planId));

    // =========================================================================
    // PLAN CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<OnboardingPlanDto>> Create([FromBody] CreateOnboardingPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OnboardingPlanDto>> Update(Guid id, [FromBody] UpdateOnboardingPlanDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // PLAN WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.StartAsync(id, employeeId.Value);
        return Ok(new { message = "Onboarding plan started." });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CompleteAsync(id, employeeId.Value);
        return Ok(new { message = "Onboarding plan completed." });
    }

    // =========================================================================
    // TASKS
    // =========================================================================

    [HttpGet("{planId:guid}/tasks")]
    public async Task<ActionResult<IEnumerable<OnboardingTaskDto>>> GetTasks(Guid planId)
        => Ok(await _service.GetTasksAsync(planId));

    [HttpGet("{planId:guid}/tasks/status/{status}")]
    public async Task<ActionResult<IEnumerable<OnboardingTaskDto>>> GetTasksByStatus(
        Guid planId, OnboardingTaskStatus status)
        => Ok(await _service.GetTasksByStatusAsync(status, planId));

    [HttpGet("tasks/status/{status}")]
    public async Task<ActionResult<IEnumerable<OnboardingTaskDto>>> GetAllTasksByStatus(
        OnboardingTaskStatus status)
        => Ok(await _service.GetTasksByStatusAsync(status, null));

    [HttpGet("tasks/overdue")]
    public async Task<ActionResult<IEnumerable<OnboardingTaskDto>>> GetOverdueTasks()
        => Ok(await _service.GetOverdueTasksAsync());

    [HttpGet("tasks/assignee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<OnboardingTaskDto>>> GetTasksByAssignee(Guid employeeId)
        => Ok(await _service.GetTasksByAssigneeAsync(employeeId));

    [HttpPost("{planId:guid}/tasks")]
    public async Task<ActionResult<OnboardingTaskDto>> AddTask(
        Guid planId, [FromBody] CreateOnboardingTaskDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddTaskAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("tasks/{taskId:guid}")]
    public async Task<ActionResult<OnboardingTaskDto>> UpdateTask(
        Guid taskId, [FromBody] UpdateOnboardingTaskDto dto)
    {
        if (taskId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateTaskAsync(dto, employeeId.Value));
    }

    [HttpPost("tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteTask(Guid taskId)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CompleteTaskAsync(taskId, employeeId.Value);
        return Ok(new { message = "Task completed." });
    }

    // =========================================================================
    // TASK COMMENTS
    // =========================================================================

    [HttpGet("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<IEnumerable<OnboardingTaskCommentDto>>> GetTaskComments(Guid taskId)
        => Ok(await _service.GetTaskCommentsAsync(taskId));

    [HttpPost("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<OnboardingTaskCommentDto>> AddTaskComment(
        Guid taskId, [FromBody] CreateOnboardingTaskCommentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddTaskCommentAsync(dto, tenantId.Value, employeeId.Value));
    }

    // =========================================================================
    // ASSET ITEMS
    // =========================================================================

    [HttpGet("{planId:guid}/assets")]
    public async Task<ActionResult<IEnumerable<OnboardingAssetDto>>> GetAssetItems(Guid planId)
        => Ok(await _service.GetAssetItemsAsync(planId));

    [HttpGet("{planId:guid}/assets/status/{status}")]
    public async Task<ActionResult<IEnumerable<OnboardingAssetDto>>> GetAssetsByStatus(
        Guid planId, OnboardingAssetProvisionStatus status)
        => Ok(await _service.GetAssetsByStatusAsync(status, planId));

    [HttpGet("assets/status/{status}")]
    public async Task<ActionResult<IEnumerable<OnboardingAssetDto>>> GetAllAssetsByStatus(
        OnboardingAssetProvisionStatus status)
        => Ok(await _service.GetAssetsByStatusAsync(status, null));

    [HttpPost("{planId:guid}/assets")]
    public async Task<ActionResult<OnboardingAssetDto>> AddAssetItem(
        Guid planId, [FromBody] CreateOnboardingAssetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddAssetItemAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("assets/{assetItemId:guid}")]
    public async Task<ActionResult<OnboardingAssetDto>> UpdateAssetItem(
        Guid assetItemId, [FromBody] UpdateOnboardingAssetDto dto)
    {
        if (assetItemId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAssetItemAsync(dto, employeeId.Value));
    }
}
