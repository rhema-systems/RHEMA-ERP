using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/learning-paths")]
[Authorize]
public class LearningPathsController : ControllerBase
{
    private readonly ILearningPathService _service;
    private readonly ICurrentUserService _currentUser;

    public LearningPathsController(ILearningPathService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LearningPathSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LearningPathDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<LearningPathSummaryDto>>> GetActive(CancellationToken ct)
        => Ok(await _service.GetActiveAsync(ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<LearningPathSummaryDto>>> GetByStatus(LearningPathStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<LearningPathSummaryDto>>> GetByPosition(Guid positionId, CancellationToken ct)
        => Ok(await _service.GetByPositionAsync(positionId, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<LearningPathDto>> Create([FromBody] CreateLearningPathDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LearningPathDto>> Update(Guid id, [FromBody] UpdateLearningPathDto dto, CancellationToken ct)
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
    // PROGRAMS
    // =========================================================================

    [HttpPost("{id:guid}/programs")]
    public async Task<ActionResult<LearningPathProgramDto>> AddProgram(Guid id, [FromBody] CreateLearningPathProgramDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.LearningPathId = id;
        return Ok(await _service.AddProgramAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/programs")]
    public async Task<ActionResult<IEnumerable<LearningPathProgramDto>>> GetPrograms(Guid id, CancellationToken ct)
        => Ok(await _service.GetProgramsAsync(id, ct));

    [HttpPut("programs/{programId:guid}")]
    public async Task<ActionResult<LearningPathProgramDto>> UpdateProgram(Guid programId, [FromBody] UpdateLearningPathProgramDto dto, CancellationToken ct)
    {
        if (programId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateProgramAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("programs/{programId:guid}")]
    public async Task<IActionResult> DeleteProgram(Guid programId, CancellationToken ct)
    {
        await _service.DeleteProgramAsync(programId, ct);
        return NoContent();
    }

    // =========================================================================
    // SKILLS
    // =========================================================================

    [HttpPost("{id:guid}/skills")]
    public async Task<ActionResult<LearningPathSkillDto>> AddSkill(Guid id, [FromBody] CreateLearningPathSkillDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.LearningPathId = id;
        return Ok(await _service.AddSkillAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/skills")]
    public async Task<ActionResult<IEnumerable<LearningPathSkillDto>>> GetSkills(Guid id, CancellationToken ct)
        => Ok(await _service.GetSkillsAsync(id, ct));

    [HttpDelete("skills/{skillId:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid skillId, CancellationToken ct)
    {
        await _service.DeleteSkillAsync(skillId, ct);
        return NoContent();
    }

    // =========================================================================
    // ENROLLMENTS
    // =========================================================================

    [HttpGet("{id:guid}/enrollments")]
    public async Task<ActionResult<IEnumerable<EmployeeLearningPathSummaryDto>>> GetEnrollmentsByPath(Guid id, CancellationToken ct)
        => Ok(await _service.GetEnrollmentsByPathIdAsync(id, ct));

    [HttpGet("enrollments")]
    public async Task<ActionResult<IEnumerable<EnrollmentListItemDto>>> GetAllEnrollments(CancellationToken ct)
        => Ok(await _service.GetAllEnrollmentsAsync(ct));

    [HttpDelete("enrollments/{enrollmentId:guid}")]
    public async Task<IActionResult> RemoveEnrollment(Guid enrollmentId, CancellationToken ct)
    {
        await _service.RemoveEnrollmentAsync(enrollmentId, ct);
        return NoContent();
    }

    [HttpPut("enrollments/{enrollmentId:guid}")]
    public async Task<ActionResult<EmployeeLearningPathDto>> UpdateEnrollment(Guid enrollmentId, [FromBody] UpdateEnrollmentDto dto, CancellationToken ct)
    {
        if (enrollmentId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateEnrollmentAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("enroll")]
    public async Task<ActionResult<EmployeeLearningPathDto>> EnrollEmployee([FromBody] EnrollEmployeeInLearningPathDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.EnrollEmployeeAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("enrollments/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeLearningPathSummaryDto>>> GetEnrollmentsForEmployee(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetEnrollmentsForEmployeeAsync(employeeId, ct));

    [HttpGet("enrollments/{enrollmentId:guid}")]
    public async Task<ActionResult<EmployeeLearningPathDto>> GetEnrollmentById(Guid enrollmentId, CancellationToken ct)
        => Ok(await _service.GetEnrollmentByIdAsync(enrollmentId, ct));

    [HttpPut("enrollments/steps/{stepId:guid}")]
    public async Task<ActionResult<EmployeeLearningPathStepDto>> UpdateStep(Guid stepId, [FromBody] UpdateLearningPathStepDto dto, CancellationToken ct)
    {
        if (stepId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateStepAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("enrollments/{enrollmentId:guid}/recalculate-progress")]
    public async Task<IActionResult> RecalculateProgress(Guid enrollmentId, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecalculateProgressAsync(enrollmentId, employeeId.Value, ct);
        return Ok(new { message = "Learning path progress recalculated." });
    }

    [HttpGet("steps/{stepId:guid}/detail")]
    public async Task<ActionResult<StepDetailPageDto>> GetStepDetail(Guid stepId, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.GetStepDetailAsync(stepId, employeeId.Value, ct));
    }
}
