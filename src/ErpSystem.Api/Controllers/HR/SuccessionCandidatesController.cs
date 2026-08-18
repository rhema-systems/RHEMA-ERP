using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/succession-candidates")]
[Authorize(Policy = HrPermissions.SuccessionReadPolicy)]
public class SuccessionCandidatesController : ControllerBase
{
    private readonly ISuccessionCandidateService _service;
    private readonly ICurrentUserService _currentUser;

    public SuccessionCandidatesController(ISuccessionCandidateService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SuccessionCandidateDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("plan/{planId:guid}")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateSummaryDto>>> GetByPlan(Guid planId)
        => Ok(await _service.GetByPlanIdAsync(planId));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("plan/{planId:guid}/readiness/{readiness}")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateSummaryDto>>> GetByReadiness(Guid planId, ReadinessLevel readiness)
        => Ok(await _service.GetByReadinessAsync(planId, readiness));

    [HttpGet("plan/{planId:guid}/ready-now")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateSummaryDto>>> GetReadyNow(Guid planId)
        => Ok(await _service.GetReadyNowCandidatesForPlanAsync(planId));

    [HttpGet("plan/{planId:guid}/emergency")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateSummaryDto>>> GetEmergency(Guid planId)
        => Ok(await _service.GetEmergencyCandidatesForPlanAsync(planId));

    [HttpGet("plan/{planId:guid}/selected")]
    public async Task<ActionResult<SuccessionCandidateDto?>> GetSelected(Guid planId)
        => Ok(await _service.GetSelectedCandidateForPlanAsync(planId));

    [HttpGet("plan/{planId:guid}/retention-risk/{risk}")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateSummaryDto>>> GetByRetentionRisk(Guid planId, RetentionRisk risk)
        => Ok(await _service.GetByRetentionRiskAsync(planId, risk));

    // =========================================================================
    // CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<SuccessionCandidateDto>> Create([FromBody] CreateSuccessionCandidateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SuccessionCandidateDto>> Update(Guid id, [FromBody] UpdateSuccessionCandidateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/assess")]
    public async Task<IActionResult> Assess(Guid id, [FromBody] AssessCandidateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.CandidateId = id;
        await _service.AssessAsync(dto);
        return Ok(new { message = "Candidate assessment recorded." });
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/select")]
    public async Task<IActionResult> Select(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.SelectCandidateAsync(id, employeeId.Value);
        return Ok(new { message = "Candidate selected." });
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPatch("bulk-rank")]
    public async Task<IActionResult> BulkUpdateRanks([FromBody] IEnumerable<CandidateRankUpdateDto> updates)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        try
        {
            await _service.BulkUpdateRanksAsync(updates, employeeId.Value);
            return Ok(new { message = "Candidate rankings updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // =========================================================================
    // COMPETENCY GAPS
    // =========================================================================

    [HttpGet("{id:guid}/gaps")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateGapDto>>> GetGaps(Guid id)
        => Ok(await _service.GetCompetencyGapsAsync(id));

    [HttpGet("{id:guid}/gaps/unaddressed")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateGapDto>>> GetUnaddressedGaps(Guid id)
        => Ok(await _service.GetUnaddressedGapsAsync(id));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/gaps")]
    public async Task<ActionResult<SuccessionCandidateGapDto>> AddGap(
        Guid id, [FromBody] CreateSuccessionCandidateGapDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CandidateId = id;
        var created = await _service.AddCompetencyGapAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetGaps), new { id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("gaps/{gapId:guid}")]
    public async Task<ActionResult<SuccessionCandidateGapDto>> UpdateGap(
        Guid gapId, [FromBody] UpdateSuccessionCandidateGapDto dto)
    {
        if (gapId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateCompetencyGapAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("gaps/{gapId:guid}")]
    public async Task<IActionResult> DeleteGap(Guid gapId)
    {
        await _service.DeleteCompetencyGapAsync(gapId);
        return NoContent();
    }

    /// <summary>Auto-generate gaps from the target position's competency requirements vs the employee's own levels.</summary>
    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/gaps/generate-from-position")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateGapDto>>> GenerateGapsFromPosition(Guid id)
    {
        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            var created = await _service.GenerateGapsFromPositionAsync(id, tenantId.Value, employeeId.Value);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // =========================================================================
    // REVIEWER FEEDBACK
    // =========================================================================

    [HttpGet("{id:guid}/feedback")]
    public async Task<ActionResult<IEnumerable<SuccessionCandidateFeedbackDto>>> GetFeedback(Guid id)
        => Ok(await _service.GetFeedbackAsync(id));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/feedback")]
    public async Task<ActionResult<SuccessionCandidateFeedbackDto>> AddFeedback(
        Guid id, [FromBody] CreateSuccessionCandidateFeedbackDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            var created = await _service.AddFeedbackAsync(id, dto, tenantId.Value, employeeId.Value);
            return CreatedAtAction(nameof(GetFeedback), new { id }, created);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("feedback/{feedbackId:guid}")]
    public async Task<IActionResult> DeleteFeedback(Guid feedbackId)
    {
        await _service.DeleteFeedbackAsync(feedbackId);
        return NoContent();
    }

    // =========================================================================
    // DEVELOPMENT ACTIVITIES
    // =========================================================================

    [HttpGet("{id:guid}/development-activities")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivitySummaryDto>>> GetDevelopmentActivities(Guid id)
        => Ok(await _service.GetDevelopmentActivitiesAsync(id));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/development-activities")]
    public async Task<ActionResult<SuccessionDevelopmentActivityDto>> AddDevelopmentActivity(
        Guid id, [FromBody] CreateSuccessionDevelopmentActivityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CandidateId = id;
        var created = await _service.AddDevelopmentActivityAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetDevelopmentActivities), new { id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("development-activities/{activityId:guid}")]
    public async Task<ActionResult<SuccessionDevelopmentActivityDto>> UpdateDevelopmentActivity(
        Guid activityId, [FromBody] UpdateSuccessionDevelopmentActivityDto dto)
    {
        if (activityId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateDevelopmentActivityAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("development-activities/{activityId:guid}")]
    public async Task<IActionResult> DeleteDevelopmentActivity(Guid activityId)
    {
        await _service.DeleteDevelopmentActivityAsync(activityId);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<IEnumerable<SuccessionDocumentDto>>> GetDocuments(Guid id)
        => Ok(await _service.GetDocumentsAsync(id));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<SuccessionDocumentDto>> AddDocument(
        Guid id, [FromBody] CreateSuccessionDocumentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CandidateId = id;
        var created = await _service.AddDocumentAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetDocuments), new { id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}
