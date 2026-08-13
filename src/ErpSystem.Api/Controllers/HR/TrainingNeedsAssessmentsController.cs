using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-needs-assessments")]
[Authorize]
[TrainingBusinessRulesAttribute]
public class TrainingNeedsAssessmentsController : ControllerBase
{
    private readonly ITrainingNeedsAssessmentService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingNeedsAssessmentsController(ITrainingNeedsAssessmentService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingNeedsAssessmentSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingNeedsAssessmentDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("year/{year:int}")]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetByYear(int year, CancellationToken ct)
        => Ok(await _service.GetByYearAsync(year, ct));

    [HttpGet("unfulfilled")]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetUnfulfilled(CancellationToken ct)
        => Ok(await _service.GetUnfulfilledAsync(ct));

    [HttpGet("priority/{priority}")]
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSummaryDto>>> GetByPriority(TrainingPriority priority, CancellationToken ct)
        => Ok(await _service.GetByPriorityAsync(priority, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
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
    public async Task<ActionResult<TrainingNeedsAssessmentDto>> Update(Guid id, [FromBody] UpdateTrainingNeedsAssessmentDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // RECOMMENDED PROGRAMS
    // =========================================================================

    [HttpPost("{id:guid}/programs")]
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
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentProgramDto>>> GetRecommendedPrograms(Guid id, CancellationToken ct)
        => Ok(await _service.GetRecommendedProgramsAsync(id, ct));

    [HttpDelete("programs/{programId:guid}")]
    public async Task<IActionResult> DeleteRecommendedProgram(Guid programId, CancellationToken ct)
    {
        await _service.DeleteRecommendedProgramAsync(programId, ct);
        return NoContent();
    }

    // =========================================================================
    // SKILL GAPS
    // =========================================================================

    [HttpPost("{id:guid}/skill-gaps")]
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
    public async Task<ActionResult<IEnumerable<TrainingNeedsAssessmentSkillDto>>> GetSkillGaps(Guid id, CancellationToken ct)
        => Ok(await _service.GetSkillGapsAsync(id, ct));

    [HttpDelete("skill-gaps/{skillGapId:guid}")]
    public async Task<IActionResult> DeleteSkillGap(Guid skillGapId, CancellationToken ct)
    {
        await _service.DeleteSkillGapAsync(skillGapId, ct);
        return NoContent();
    }
}
