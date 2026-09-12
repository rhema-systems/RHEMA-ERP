using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-needs-assessments")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingNeedsAssessmentsController : ControllerBase
{
    private readonly ITrainingNeedsAssessmentService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingNeedsAssessmentsController(
        ITrainingNeedsAssessmentService service,
        ICurrentUserService currentUser,
        IAuthorizationService authorization)
    {
        _service = service;
        _currentUser = currentUser;
        _authorization = authorization;
    }

    private readonly IAuthorizationService _authorization;

    /// <summary>Self-or-permission (W3), as on LeavesController — see the remarks there.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUser.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingNeedsAssessmentSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingNeedsAssessmentDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
    {
        // W3: an employee may see their own needs assessments; anyone else's need the desk read.
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("year/{year:int}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetByYear(int year, CancellationToken ct)
        => Ok(await _service.GetByYearAsync(year, ct));

    [HttpGet("unfulfilled")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetUnfulfilled(CancellationToken ct)
        => Ok(await _service.GetUnfulfilledAsync(ct));

    [HttpGet("priority/{priority}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetByPriority(TrainingPriority priority, CancellationToken ct)
        => Ok(await _service.GetByPriorityAsync(priority, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingNeedsAssessmentDto>> Create([FromBody] CreateTrainingNeedsAssessmentDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("bulk")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<BulkNeedsAssessmentResultDto>> BulkCreate([FromBody] BulkCreateTrainingNeedsAssessmentDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.BulkCreateAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingNeedsAssessmentDto>> Update(Guid id, [FromBody] UpdateTrainingNeedsAssessmentDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // RECOMMENDED PROGRAMS
    // =========================================================================

    [HttpPost("{id:guid}/programs")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingNeedsAssessmentProgramDto>> AddRecommendedProgram(Guid id, [FromBody] CreateTrainingNeedsAssessmentProgramDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.AssessmentId = id;
        return Ok(await _service.AddRecommendedProgramAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/programs")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentProgramDto>>> GetRecommendedPrograms(Guid id, CancellationToken ct)
        => Ok(await _service.GetRecommendedProgramsAsync(id, ct));

    [HttpDelete("programs/{programId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> DeleteRecommendedProgram(Guid programId, CancellationToken ct)
    {
        await _service.DeleteRecommendedProgramAsync(programId, ct);
        return NoContent();
    }

    // =========================================================================
    // SKILL GAPS
    // =========================================================================

    [HttpPost("{id:guid}/skill-gaps")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingNeedsAssessmentSkillDto>> AddSkillGap(Guid id, [FromBody] CreateTrainingNeedsAssessmentSkillDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.AssessmentId = id;
        return Ok(await _service.AddSkillGapAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/skill-gaps")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSkillDto>>> GetSkillGaps(Guid id, CancellationToken ct)
        => Ok(await _service.GetSkillGapsAsync(id, ct));

    [HttpDelete("skill-gaps/{skillGapId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> DeleteSkillGap(Guid skillGapId, CancellationToken ct)
    {
        await _service.DeleteSkillGapAsync(skillGapId, ct);
        return NoContent();
    }
}
