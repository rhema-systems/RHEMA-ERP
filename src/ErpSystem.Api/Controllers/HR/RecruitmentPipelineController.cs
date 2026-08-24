using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>Request item for stage reorder operations.</summary>
public record StageOrderItem(Guid StageId, int NewOrder);

/// <summary>Request body for cloning a pipeline.</summary>
public record ClonePipelineRequest(string NewName);

/// <summary>
/// Recruitment pipelines and their stages — the setup that defines how applications progress.
///
/// <para>Reads stay open to the tenant: a stage name is what every pipeline board, application card
/// and stage-history row renders, so anyone who can see an application needs to resolve them.
/// Everything that changes a pipeline is HR's — a stage's <c>Order</c>, <c>CanRepeat</c> and
/// <c>MaxAttempts</c> are the transition rules <c>ApplicationPipelineService</c> enforces, so editing
/// a stage rewrites the rules every in-flight application is being moved under.</para>
/// </summary>
[ApiController]
[Route("api/recruitment-pipelines")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class RecruitmentPipelineController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IRecruitmentPipelineService _service;
    private readonly ICurrentUserService _currentUser;

    public RecruitmentPipelineController(IRecruitmentPipelineService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // PIPELINE QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecruitmentPipelineDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<RecruitmentPipelineSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("default")]
    public async Task<ActionResult<RecruitmentPipelineDto?>> GetDefault()
        => Ok(await _service.GetDefaultPipelineAsync());

    [HttpGet("name/{name}")]
    public async Task<ActionResult<RecruitmentPipelineDto?>> GetByName(string name)
        => Ok(await _service.GetByNameAsync(name));

    // =========================================================================
    // PIPELINE CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentPipelineDto>> Create([FromBody] CreateRecruitmentPipelineDto dto)
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

    /// <summary>Clones an existing pipeline (with its stages) into a new one for editing.</summary>
    [HttpPost("{id:guid}/clone")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentPipelineDto>> Clone(Guid id, [FromBody] ClonePipelineRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            var created = await _service.ClonePipelineAsync(id, request?.NewName ?? string.Empty, tenantId.Value, employeeId.Value);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentPipelineDto>> Update(Guid id, [FromBody] UpdateRecruitmentPipelineDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // STAGE QUERIES
    // =========================================================================

    [HttpGet("{pipelineId:guid}/stages")]
    public async Task<ActionResult<IEnumerable<RecruitmentPipelineStageDto>>> GetStages(Guid pipelineId)
        => Ok(await _service.GetStagesAsync(pipelineId));

    [HttpGet("{pipelineId:guid}/stages/final")]
    public async Task<ActionResult<RecruitmentPipelineStageDto?>> GetFinalStage(Guid pipelineId)
        => Ok(await _service.GetFinalStageAsync(pipelineId));

    [HttpGet("{pipelineId:guid}/stages/max-order")]
    public async Task<ActionResult<int>> GetMaxStageOrder(Guid pipelineId)
        => Ok(await _service.GetMaxStageOrderAsync(pipelineId));

    [HttpGet("stages/type/{stageType}")]
    public async Task<ActionResult<IEnumerable<RecruitmentPipelineStageDto>>> GetStagesByType(
        RecruitmentPipelineStageType stageType)
        => Ok(await _service.GetStagesByTypeAsync(stageType));

    // =========================================================================
    // STAGE CRUD
    // =========================================================================

    [HttpPost("{pipelineId:guid}/stages")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentPipelineStageDto>> AddStage(
        Guid pipelineId, [FromBody] CreateRecruitmentPipelineStageDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // The pipeline comes from the route; the DTO also declares it and the service read only that,
        // so a POST to /{A}/stages carrying recruitmentPipelineId: B added the stage to pipeline B.
        dto.RecruitmentPipelineId = pipelineId;
        return Ok(await _service.AddStageAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("stages/{stageId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentPipelineStageDto>> UpdateStage(
        Guid stageId, [FromBody] UpdateRecruitmentPipelineStageDto dto)
    {
        if (stageId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateStageAsync(dto, employeeId.Value));
    }

    [HttpDelete("stages/{stageId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteStage(Guid stageId)
    {
        await _service.DeleteStageAsync(stageId);
        return NoContent();
    }

    [HttpPost("{pipelineId:guid}/stages/reorder")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ReorderStages(
        Guid pipelineId, [FromBody] List<StageOrderItem> stageOrders)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ReorderStagesAsync(
            pipelineId,
            stageOrders.Select(x => (x.StageId, x.NewOrder)),
            employeeId.Value);

        return Ok(new { message = "Stages reordered." });
    }
}
