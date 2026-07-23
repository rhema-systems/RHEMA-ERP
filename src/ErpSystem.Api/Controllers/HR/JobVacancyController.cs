using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/job-vacancies")]
[Authorize]
public class JobVacancyController : ControllerBase
{
    private readonly IJobVacancyService _service;
    private readonly ICurrentUserService _currentUser;

    public JobVacancyController(IJobVacancyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<JobVacancySummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobVacancyDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{vacancyNumber}")]
    public async Task<ActionResult<JobVacancyDto?>> GetByVacancyNumber(string vacancyNumber)
        => Ok(await _service.GetByVacancyNumberAsync(vacancyNumber));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<JobVacancyDetailDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByStatus(JobVacancyStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetActive()
        => Ok(await _service.GetActiveVacanciesAsync());

    [HttpGet("published")]
    public async Task<ActionResult<IEnumerable<JobVacancyDto>>> GetPublished(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        return Ok(await _service.GetPublishedForJobBoardAsync(tenantId.Value, ct));
    }

    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByPosition(Guid positionId)
        => Ok(await _service.GetByPositionAsync(positionId));

    [HttpGet("hiring-manager/{hiringManagerId:guid}")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByHiringManager(Guid hiringManagerId)
        => Ok(await _service.GetByHiringManagerAsync(hiringManagerId));

    [HttpGet("recruiter/{recruiterId:guid}")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByRecruiter(Guid recruiterId)
        => Ok(await _service.GetByRecruiterAsync(recruiterId));

    [HttpGet("requisition/{requisitionId:guid}")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByRequisition(Guid requisitionId)
        => Ok(await _service.GetByRequisitionAsync(requisitionId));

    [HttpGet("deadline-approaching")]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetDeadlineApproaching(
        [FromQuery] int daysAhead = 7)
        => Ok(await _service.GetWithDeadlineApproachingAsync(daysAhead));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<JobVacancyDto>> Create([FromBody] CreateJobVacancyDto dto)
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
    public async Task<ActionResult<JobVacancyDto>> Update(Guid id, [FromBody] UpdateJobVacancyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    /// <summary>
    /// Atomically saves field updates AND applies a status transition in a single DB transaction.
    /// Use this from any transition button on the edit form instead of a separate UpdateAsync
    /// + ChangeStatusAsync pair to avoid partial-failure risk.
    /// </summary>
    [HttpPost("{id:guid}/transition")]
    public async Task<ActionResult<JobVacancyDto>> Transition(Guid id, [FromBody] TransitionJobVacancyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var result = await _service.TransitionAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(result);
    }

    [HttpPost("{id:guid}/change-status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeJobVacancyStatusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ChangeStatusAsync(dto, employeeId.Value);
        return Ok(new { message = "Vacancy status updated." });
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseJobVacancyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CloseAsync(dto, employeeId.Value);
        return Ok(new { message = "Vacancy cancelled." });
    }

    [HttpPost("{id:guid}/close-for-applications")]
    public async Task<IActionResult> CloseForApplications(Guid id, [FromBody] CloseForApplicationsDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CloseForApplicationsAsync(dto, employeeId.Value);
        return Ok(new { message = "Vacancy closed for applications." });
    }

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    [HttpGet("{vacancyId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<JobVacancyAttachmentDto>>> GetAttachments(Guid vacancyId)
        => Ok(await _service.GetAttachmentsAsync(vacancyId));

    [HttpPost("{vacancyId:guid}/attachments")]
    public async Task<ActionResult<JobVacancyAttachmentDto>> AddAttachment(
        Guid vacancyId, [FromBody] CreateJobVacancyAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddAttachmentAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        await _service.DeleteAttachmentAsync(attachmentId);
        return NoContent();
    }

    // =========================================================================
    // STATUS HISTORY
    // =========================================================================

    [HttpGet("{vacancyId:guid}/status-history")]
    public async Task<ActionResult<IEnumerable<JobVacancyStatusHistoryDto>>> GetStatusHistory(Guid vacancyId)
        => Ok(await _service.GetStatusHistoryAsync(vacancyId));

    [HttpGet("{vacancyId:guid}/status-history/latest")]
    public async Task<ActionResult<JobVacancyStatusHistoryDto?>> GetLatestStatusHistory(Guid vacancyId)
        => Ok(await _service.GetLatestStatusHistoryAsync(vacancyId));

    // =========================================================================
    // SHORTLISTING CRITERIA
    // =========================================================================

    [HttpGet("{vacancyId:guid}/criteria")]
    public async Task<ActionResult<IEnumerable<JobShortlistingCriteriaDto>>> GetCriteria(Guid vacancyId)
        => Ok(await _service.GetCriteriaAsync(vacancyId));

    [HttpGet("{vacancyId:guid}/criteria/mandatory")]
    public async Task<ActionResult<IEnumerable<JobShortlistingCriteriaDto>>> GetMandatoryCriteria(Guid vacancyId)
        => Ok(await _service.GetMandatoryCriteriaAsync(vacancyId));

    [HttpPost("{vacancyId:guid}/criteria")]
    public async Task<ActionResult<JobShortlistingCriteriaDto>> AddCriteria(
        Guid vacancyId, [FromBody] CreateJobShortlistingCriteriaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddCriteriaAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("criteria/{criteriaId:guid}")]
    public async Task<ActionResult<JobShortlistingCriteriaDto>> UpdateCriteria(
        Guid criteriaId, [FromBody] UpdateJobShortlistingCriteriaDto dto)
    {
        if (criteriaId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateCriteriaAsync(dto, employeeId.Value));
    }

    [HttpDelete("criteria/{criteriaId:guid}")]
    public async Task<IActionResult> DeleteCriteria(Guid criteriaId)
    {
        await _service.DeleteCriteriaAsync(criteriaId);
        return NoContent();
    }

    // ── Pipeline stage assignments ────────────────────────────────────────────

    [HttpGet("{vacancyId:guid}/stage-assignments")]
    public async Task<ActionResult<IEnumerable<VacancyPipelineStageAssignmentDto>>> GetStageAssignments(Guid vacancyId)
        => Ok(await _service.GetStageAssignmentsAsync(vacancyId));

    [HttpPost("{vacancyId:guid}/stage-assignments")]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> UpsertStageAssignment(
        Guid vacancyId, [FromBody] CreateVacancyPipelineStageAssignmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.JobVacancyId != vacancyId) return BadRequest("VacancyId mismatch.");

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var result = await _service.UpsertStageAssignmentAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(result);
    }

    [HttpPut("stage-assignments/{id:guid}")]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> UpdateStageAssignment(
        Guid id, [FromBody] UpdateVacancyPipelineStageAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateStageAssignmentAsync(dto, employeeId.Value));
    }

    [HttpPatch("stage-assignments/{id:guid}/complete")]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> CompleteStageAssignment(
        Guid id, [FromBody] CompleteVacancyPipelineStageAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.CompleteStageAssignmentAsync(dto, employeeId.Value));
    }

    [HttpPatch("stage-assignments/{id:guid}/skip")]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> SkipStageAssignment(
        Guid id, [FromBody] SkipVacancyPipelineStageAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.SkipStageAssignmentAsync(dto, employeeId.Value));
    }

    [HttpDelete("stage-assignments/{id:guid}")]
    public async Task<IActionResult> DeleteStageAssignment(Guid id)
    {
        var deleted = await _service.DeleteStageAssignmentAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
