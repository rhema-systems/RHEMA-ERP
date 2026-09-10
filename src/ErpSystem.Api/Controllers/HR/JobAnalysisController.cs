using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class JobAnalysisController : ControllerBase
{
    private readonly IJobDescriptionService _jobDescriptionService;
    private readonly IManpowerBudgetService _manpowerBudgetService;

    public JobAnalysisController(
        IJobDescriptionService jobDescriptionService,
        IManpowerBudgetService manpowerBudgetService)
    {
        _jobDescriptionService = jobDescriptionService;
        _manpowerBudgetService = manpowerBudgetService;
    }

    #region Job Descriptions

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions")]
    public async Task<ActionResult<IEnumerable<JobDescriptionDto>>> GetJobDescriptions()
        => Ok(await _jobDescriptionService.GetAllAsync());

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/paged")]
    public async Task<ActionResult<PagedResult<JobDescriptionDto>>> GetJobDescriptionsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _jobDescriptionService.GetPagedAsync(pageNumber, pageSize));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{id:guid}")]
    public async Task<ActionResult<JobDescriptionDto>> GetJobDescription(Guid id)
        => Ok(await _jobDescriptionService.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{id:guid}/details")]
    public async Task<ActionResult<JobDescriptionDetailDto>> GetJobDescriptionDetails(Guid id)
        => Ok(await _jobDescriptionService.GetDetailByIdAsync(id));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetByPosition(Guid positionId)
        => Ok(await _jobDescriptionService.GetByPositionIdAsync(positionId));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/status/{status}")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetByStatus(JobDescriptionStatus status)
        => Ok(await _jobDescriptionService.GetByStatusAsync(status));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/position/{positionId:guid}/current")]
    public async Task<ActionResult<JobDescriptionDto?>> GetCurrentForPosition(Guid positionId)
        => Ok(await _jobDescriptionService.GetCurrentVersionForPositionAsync(positionId));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/due-review")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _jobDescriptionService.GetDueForReviewAsync(daysAhead));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("analytics")]
    public async Task<ActionResult<JobAnalyticsDto>> GetAnalytics()
        => Ok(await _jobDescriptionService.GetAnalyticsAsync());

    /// <summary>Positions with no approved job description (FR-HR-134 coverage).</summary>
    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("positions/uncovered")]
    public async Task<ActionResult<IEnumerable<UncoveredPositionDto>>> GetUncoveredPositions()
        => Ok(await _jobDescriptionService.GetUncoveredPositionsAsync());

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/position/{positionId:guid}/history")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetVersionHistory(Guid positionId)
        => Ok(await _jobDescriptionService.GetVersionHistoryAsync(positionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions")]
    public async Task<ActionResult<JobDescriptionDto>> CreateJobDescription(
        [FromBody] CreateJobDescriptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var preparedById = GetCurrentEmployeeId();
        if (preparedById == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        var created = await _jobDescriptionService.CreateAsync(dto, preparedById.Value);
        return CreatedAtAction(nameof(GetJobDescription), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("descriptions/{id:guid}")]
    public async Task<ActionResult<JobDescriptionDto>> UpdateJobDescription(Guid id, [FromBody] UpdateJobDescriptionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _jobDescriptionService.UpdateAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{id:guid}/submit")]
    public async Task<IActionResult> SubmitForReview(Guid id, [FromBody] SubmitJobDescriptionForReviewDto dto)
    {
        dto.JobDescriptionId = id;
        await _jobDescriptionService.SubmitForReviewAsync(dto);
        return Ok(new { message = "Submitted for review" });
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{id:guid}/review")]
    public async Task<IActionResult> ReviewJobDescription(Guid id, [FromBody] ReviewJobDescriptionDto dto)
    {
        var reviewedById = GetCurrentEmployeeId();
        if (reviewedById == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.JobDescriptionId = id;
        await _jobDescriptionService.ReviewAsync(dto, reviewedById.Value);
        return Ok(new { message = "Review recorded" });
    }

    /// <summary>Approves the current workflow step for a job description (area 17 slice 3).</summary>
    /// <remarks>
    /// <para>⚠ <b>Plain <c>[Authorize]</c>, and that is the whole point of the slice.</b> The
    /// approver is whoever the tenant named in the workflow definition — a job-family owner, a
    /// department head — and they may hold no HR permission at all. This was first written gated on
    /// <c>HR.JobArchitecture.Write</c>, which made the action reachable by <b>nobody</b>: the people
    /// with the permission are refused by the engine because they are not the assigned approver,
    /// and the assigned approver is refused by the permission. The harness caught it because it
    /// mints an approver holding role <c>Employee</c> and nothing else.</para>
    ///
    /// <para>What guards this is not weaker than a permission, it is stronger and more specific:
    /// <c>CanUserApproveAsync</c> asks whether <i>this caller</i> is the assigned approver of the
    /// step in front of <i>this record</i>. Trap 3 in <c>hr-area-authz-pattern</c>: a permission
    /// gate is the wrong tool when the actor is defined by the record.</para>
    /// </remarks>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("descriptions/{id:guid}/workflow/approve")]
    public async Task<IActionResult> ApproveJobDescriptionOnWorkflow(Guid id)
    {
        var approvedById = GetCurrentEmployeeId();
        if (approvedById == null)
            return BadRequest("Your user account is not linked to an employee record.");
        await _jobDescriptionService.ApproveViaWorkflowAsync(id, approvedById.Value);
        return Ok(new { message = "Job description approval step processed" });
    }

    /// <summary>
    /// Rejects the current workflow step, returning the job description to its author. Plain
    /// <c>[Authorize]</c> for the same reason as the approval above.
    /// </summary>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("descriptions/{id:guid}/workflow/reject")]
    public async Task<IActionResult> RejectJobDescriptionOnWorkflow(Guid id, [FromBody] RejectJobDescriptionDto dto)
    {
        await _jobDescriptionService.RejectViaWorkflowAsync(id, dto?.Reason);
        return Ok(new { message = "Job description returned for revision" });
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpPost("descriptions/{id:guid}/approve")]
    public async Task<IActionResult> ApproveJobDescription(Guid id, [FromBody] ApproveJobDescriptionDto dto)
    {
        var approvedById = GetCurrentEmployeeId();
        if (approvedById == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.JobDescriptionId = id;
        await _jobDescriptionService.ApproveAsync(dto, approvedById.Value);
        return Ok(new { message = "Job description approved" });
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{id:guid}/version")]
    public async Task<ActionResult<JobDescriptionDto>> CreateNewVersion(
        Guid id,
        [FromBody] CreateJobDescriptionVersionDto dto)
    {
        var preparedById = GetCurrentEmployeeId();
        if (preparedById == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.OriginalJobDescriptionId = id;
        var created = await _jobDescriptionService.CreateNewVersionAsync(dto, preparedById.Value);
        return CreatedAtAction(nameof(GetJobDescription), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{id:guid}/clone")]
    public async Task<ActionResult<JobDescriptionDto>> CloneJobDescription(Guid id)
    {
        var created = await _jobDescriptionService.CloneAsync(id, GetCurrentEmployeeId());
        return CreatedAtAction(nameof(GetJobDescription), new { id = created.Id }, created);
    }

    private Guid? GetCurrentEmployeeId()
    {
        var claim = User.FindFirst("employee_id")?.Value;
        return Guid.TryParse(claim, out var id) && id != Guid.Empty ? id : null;
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("descriptions/{id:guid}")]
    public async Task<IActionResult> DeleteJobDescription(Guid id)
    {
        await _jobDescriptionService.DeleteAsync(id);
        return NoContent();
    }

    #region Responsibilities

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/responsibilities")]
    public async Task<ActionResult<JobResponsibilityDto>> AddResponsibility(Guid jobDescriptionId, [FromBody] CreateJobResponsibilityDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddResponsibilityAsync(dto);
        return CreatedAtAction(nameof(GetResponsibilities), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/responsibilities")]
    public async Task<ActionResult<IEnumerable<JobResponsibilityDto>>> GetResponsibilities(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetResponsibilitiesAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("responsibilities/{id:guid}")]
    public async Task<ActionResult<JobResponsibilityDto>> UpdateResponsibility(Guid id, [FromBody] UpdateJobResponsibilityDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateResponsibilityAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("responsibilities/{id:guid}")]
    public async Task<IActionResult> DeleteResponsibility(Guid id)
    {
        await _jobDescriptionService.DeleteResponsibilityAsync(id);
        return NoContent();
    }

    #endregion

    #region Qualifications

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/qualifications")]
    public async Task<ActionResult<JobQualificationDto>> AddQualification(Guid jobDescriptionId, [FromBody] CreateJobQualificationDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddQualificationAsync(dto);
        return CreatedAtAction(nameof(GetQualifications), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/qualifications")]
    public async Task<ActionResult<IEnumerable<JobQualificationDto>>> GetQualifications(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetQualificationsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("qualifications/{id:guid}")]
    public async Task<ActionResult<JobQualificationDto>> UpdateQualification(Guid id, [FromBody] UpdateJobQualificationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateQualificationAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("qualifications/{id:guid}")]
    public async Task<IActionResult> DeleteQualification(Guid id)
    {
        await _jobDescriptionService.DeleteQualificationAsync(id);
        return NoContent();
    }

    #endregion

    #region Competencies

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/competencies")]
    public async Task<ActionResult<JobCompetencyDto>> AddCompetency(Guid jobDescriptionId, [FromBody] CreateJobCompetencyDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddCompetencyAsync(dto);
        return CreatedAtAction(nameof(GetCompetencies), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/competencies")]
    public async Task<ActionResult<IEnumerable<JobCompetencyDto>>> GetCompetencies(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetCompetenciesAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("competencies/{id:guid}")]
    public async Task<ActionResult<JobCompetencyDto>> UpdateCompetency(Guid id, [FromBody] UpdateJobCompetencyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateCompetencyAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("competencies/{id:guid}")]
    public async Task<IActionResult> DeleteCompetency(Guid id)
    {
        await _jobDescriptionService.DeleteCompetencyAsync(id);
        return NoContent();
    }

    #endregion

    #region Physical Demands

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/physical-demands")]
    public async Task<ActionResult<JobPhysicalDemandDto>> AddPhysicalDemand(Guid jobDescriptionId, [FromBody] CreateJobPhysicalDemandDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddPhysicalDemandAsync(dto);
        return CreatedAtAction(nameof(GetPhysicalDemands), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/physical-demands")]
    public async Task<ActionResult<IEnumerable<JobPhysicalDemandDto>>> GetPhysicalDemands(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetPhysicalDemandsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("physical-demands/{id:guid}")]
    public async Task<ActionResult<JobPhysicalDemandDto>> UpdatePhysicalDemand(Guid id, [FromBody] UpdateJobPhysicalDemandDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdatePhysicalDemandAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("physical-demands/{id:guid}")]
    public async Task<IActionResult> DeletePhysicalDemand(Guid id)
    {
        await _jobDescriptionService.DeletePhysicalDemandAsync(id);
        return NoContent();
    }

    #endregion

    #region Working Conditions

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/working-conditions")]
    public async Task<ActionResult<JobWorkingConditionDto>> AddWorkingCondition(Guid jobDescriptionId, [FromBody] CreateJobWorkingConditionDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddWorkingConditionAsync(dto);
        return CreatedAtAction(nameof(GetWorkingConditions), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/working-conditions")]
    public async Task<ActionResult<IEnumerable<JobWorkingConditionDto>>> GetWorkingConditions(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetWorkingConditionsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("working-conditions/{id:guid}")]
    public async Task<ActionResult<JobWorkingConditionDto>> UpdateWorkingCondition(Guid id, [FromBody] UpdateJobWorkingConditionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateWorkingConditionAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("working-conditions/{id:guid}")]
    public async Task<IActionResult> DeleteWorkingCondition(Guid id)
    {
        await _jobDescriptionService.DeleteWorkingConditionAsync(id);
        return NoContent();
    }

    #endregion

    #region Equipment Tools

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/equipment-tools")]
    public async Task<ActionResult<JobEquipmentToolDto>> AddEquipmentTool(Guid jobDescriptionId, [FromBody] CreateJobEquipmentToolDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddEquipmentToolAsync(dto);
        return CreatedAtAction(nameof(GetEquipmentTools), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/equipment-tools")]
    public async Task<ActionResult<IEnumerable<JobEquipmentToolDto>>> GetEquipmentTools(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetEquipmentToolsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("equipment-tools/{id:guid}")]
    public async Task<ActionResult<JobEquipmentToolDto>> UpdateEquipmentTool(Guid id, [FromBody] UpdateJobEquipmentToolDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateEquipmentToolAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("equipment-tools/{id:guid}")]
    public async Task<IActionResult> DeleteEquipmentTool(Guid id)
    {
        await _jobDescriptionService.DeleteEquipmentToolAsync(id);
        return NoContent();
    }

    #endregion

    #region Reporting Relationships

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/reporting-relationships")]
    public async Task<ActionResult<JobReportingRelationshipDto>> AddReportingRelationship(Guid jobDescriptionId, [FromBody] CreateJobReportingRelationshipDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddReportingRelationshipAsync(dto);
        return CreatedAtAction(nameof(GetReportingRelationships), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/reporting-relationships")]
    public async Task<ActionResult<IEnumerable<JobReportingRelationshipDto>>> GetReportingRelationships(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetReportingRelationshipsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("reporting-relationships/{id:guid}")]
    public async Task<ActionResult<JobReportingRelationshipDto>> UpdateReportingRelationship(Guid id, [FromBody] UpdateJobReportingRelationshipDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateReportingRelationshipAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("reporting-relationships/{id:guid}")]
    public async Task<IActionResult> DeleteReportingRelationship(Guid id)
    {
        await _jobDescriptionService.DeleteReportingRelationshipAsync(id);
        return NoContent();
    }

    #endregion

    #region Duty Items

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/duty-items")]
    public async Task<ActionResult<JobDutyItemDto>> AddDutyItem(Guid jobDescriptionId, [FromBody] CreateJobDutyItemDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddDutyItemAsync(dto);
        return CreatedAtAction(nameof(GetDutyItems), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/duty-items")]
    public async Task<ActionResult<IEnumerable<JobDutyItemDto>>> GetDutyItems(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetDutyItemsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("duty-items/{id:guid}")]
    public async Task<ActionResult<JobDutyItemDto>> UpdateDutyItem(Guid id, [FromBody] UpdateJobDutyItemDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateDutyItemAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("duty-items/{id:guid}")]
    public async Task<IActionResult> DeleteDutyItem(Guid id)
    {
        await _jobDescriptionService.DeleteDutyItemAsync(id);
        return NoContent();
    }

    #endregion

    #region PPE Requirements

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/ppe-requirements")]
    public async Task<ActionResult<JobPpeRequirementDto>> AddPpeRequirement(Guid jobDescriptionId, [FromBody] CreateJobPpeRequirementDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddPpeRequirementAsync(dto);
        return CreatedAtAction(nameof(GetPpeRequirements), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/ppe-requirements")]
    public async Task<ActionResult<IEnumerable<JobPpeRequirementDto>>> GetPpeRequirements(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetPpeRequirementsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("ppe-requirements/{id:guid}")]
    public async Task<ActionResult<JobPpeRequirementDto>> UpdatePpeRequirement(Guid id, [FromBody] UpdateJobPpeRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdatePpeRequirementAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("ppe-requirements/{id:guid}")]
    public async Task<IActionResult> DeletePpeRequirement(Guid id)
    {
        await _jobDescriptionService.DeletePpeRequirementAsync(id);
        return NoContent();
    }

    #endregion

    #region Equipment Training

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("equipment-tools/{equipmentToolId:guid}/training")]
    public async Task<ActionResult<JobEquipmentTrainingDto>> AddEquipmentTraining(Guid equipmentToolId, [FromBody] CreateJobEquipmentTrainingDto dto)
    {
        dto.JobEquipmentToolId = equipmentToolId;
        var created = await _jobDescriptionService.AddEquipmentTrainingAsync(dto);
        return CreatedAtAction(nameof(GetEquipmentTrainings), new { equipmentToolId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("equipment-tools/{equipmentToolId:guid}/training")]
    public async Task<ActionResult<IEnumerable<JobEquipmentTrainingDto>>> GetEquipmentTrainings(Guid equipmentToolId)
        => Ok(await _jobDescriptionService.GetEquipmentTrainingsAsync(equipmentToolId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("equipment-training/{id:guid}")]
    public async Task<ActionResult<JobEquipmentTrainingDto>> UpdateEquipmentTraining(Guid id, [FromBody] UpdateJobEquipmentTrainingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateEquipmentTrainingAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("equipment-training/{id:guid}")]
    public async Task<IActionResult> DeleteEquipmentTraining(Guid id)
    {
        await _jobDescriptionService.DeleteEquipmentTrainingAsync(id);
        return NoContent();
    }

    #endregion

    #region Medical Requirements

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/medical-requirements")]
    public async Task<ActionResult<JobMedicalRequirementDto>> AddMedicalRequirement(Guid jobDescriptionId, [FromBody] CreateJobMedicalRequirementDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddMedicalRequirementAsync(dto);
        return CreatedAtAction(nameof(GetMedicalRequirements), new { jobDescriptionId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/medical-requirements")]
    public async Task<ActionResult<IEnumerable<JobMedicalRequirementDto>>> GetMedicalRequirements(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetMedicalRequirementsAsync(jobDescriptionId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("medical-requirements/{id:guid}")]
    public async Task<ActionResult<JobMedicalRequirementDto>> UpdateMedicalRequirement(Guid id, [FromBody] UpdateJobMedicalRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateMedicalRequirementAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("medical-requirements/{id:guid}")]
    public async Task<IActionResult> DeleteMedicalRequirement(Guid id)
    {
        await _jobDescriptionService.DeleteMedicalRequirementAsync(id);
        return NoContent();
    }

    #endregion

    #region Valuation

    /// <summary>What the role is worth. Safe — computes and returns, changes nothing.</summary>
    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("descriptions/{jobDescriptionId:guid}/valuation")]
    public async Task<ActionResult<JobValuationSummaryDto>> GetValuation(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetValuationAsync(jobDescriptionId));

    /// <summary>Works the valuation out and stores it on the job description.</summary>
    /// <remarks>
    /// ⚠ The GET above did this until 2026-09-07, which made every retry, refetch and cache
    /// revalidation an UPDATE — and stamped the record's <c>UpdatedAt</c> each time, approved
    /// documents included. Storing an estimate is an act: it is a POST, it is gated on Write
    /// rather than Read, and it refuses an approved description like every other edit does.
    /// </remarks>
    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("descriptions/{jobDescriptionId:guid}/valuation")]
    public async Task<ActionResult<JobValuationSummaryDto>> RecalculateValuation(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.RecalculateValuationAsync(jobDescriptionId));

    #endregion

    #region Responsibility KPIs

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("responsibilities/{responsibilityId:guid}/kpis")]
    public async Task<ActionResult<JobResponsibilityKpiDto>> AddResponsibilityKpi(Guid responsibilityId, [FromBody] CreateJobResponsibilityKpiDto dto)
    {
        dto.JobResponsibilityId = responsibilityId;
        var created = await _jobDescriptionService.AddResponsibilityKpiAsync(dto);
        return CreatedAtAction(nameof(GetResponsibilityKpis), new { responsibilityId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("responsibilities/{responsibilityId:guid}/kpis")]
    public async Task<ActionResult<IEnumerable<JobResponsibilityKpiDto>>> GetResponsibilityKpis(Guid responsibilityId)
        => Ok(await _jobDescriptionService.GetResponsibilityKpisAsync(responsibilityId));

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("kpis/{id:guid}")]
    public async Task<ActionResult<JobResponsibilityKpiDto>> UpdateResponsibilityKpi(Guid id, [FromBody] UpdateJobResponsibilityKpiDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateResponsibilityKpiAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("kpis/{id:guid}")]
    public async Task<IActionResult> DeleteResponsibilityKpi(Guid id)
    {
        await _jobDescriptionService.DeleteResponsibilityKpiAsync(id);
        return NoContent();
    }

    #endregion

    #endregion

    #region Establishment (FR-HR-136)

    /// <summary>
    /// Sets a position's approved establishment directly, for posts no manpower budget covers.
    /// </summary>
    /// <remarks>
    /// <para>Decision D-2 makes an approved manpower budget the establishment, and that is the route
    /// that should carry most of the organisation. This is the other half of it: an establishment
    /// that can only be set by a budget cannot be set at all for a position no budget names, and
    /// there will always be some — a post created mid-year, a unit that budgets annually while
    /// hiring quarterly.</para>
    ///
    /// <para>⚠ Admin-tier, and deliberately so: this writes the same authorised number that
    /// FR-HR-135's three-step chain produces, so it is the one place the chain can be bypassed. It
    /// is auditable — <c>EstablishmentSourceBudgetId</c> stays null, which is exactly how a screen
    /// tells "approved by budget X" from "set by HR".</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.ManpowerBudgetAdminPolicy)]
    [HttpPut("establishment/position/{positionId:guid}")]
    public async Task<ActionResult<PositionEstablishmentResultDto>> SetPositionEstablishment(
        Guid positionId, [FromBody] SetPositionEstablishmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _manpowerBudgetService.SetPositionEstablishmentAsync(positionId, dto));
    }

    /// <summary>
    /// Withdraws a position's approved establishment, returning it to unconstrained.
    /// </summary>
    /// <remarks>
    /// Admin-tier, like setting one. An establishment that cannot be withdrawn is a trap: a wrong
    /// number, once approved, would refuse every requisition and movement against the post forever.
    /// </remarks>
    [Authorize(Policy = HrPermissions.ManpowerBudgetAdminPolicy)]
    [HttpDelete("establishment/position/{positionId:guid}")]
    public async Task<ActionResult<PositionEstablishmentResultDto>> WithdrawPositionEstablishment(
        Guid positionId, [FromQuery] string? reason)
        => Ok(await _manpowerBudgetService.WithdrawPositionEstablishmentAsync(
            positionId, string.IsNullOrWhiteSpace(reason) ? "No reason given." : reason));

    /// <summary>What a position's establishment is, and where the number came from.</summary>
    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("establishment/position/{positionId:guid}")]
    public async Task<ActionResult<PositionEstablishmentResultDto>> GetPositionEstablishment(Guid positionId)
        => Ok(await _manpowerBudgetService.GetPositionEstablishmentAsync(positionId));

    #endregion

    #region Manpower Budgets

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetDto>>> GetBudgets()
        => Ok(await _manpowerBudgetService.GetAllAsync());

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/paged")]
    public async Task<ActionResult<PagedResult<ManpowerBudgetDto>>> GetBudgetsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _manpowerBudgetService.GetPagedAsync(pageNumber, pageSize));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/{id:guid}")]
    public async Task<ActionResult<ManpowerBudgetDto>> GetBudget(Guid id)
        => Ok(await _manpowerBudgetService.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/{id:guid}/details")]
    public async Task<ActionResult<ManpowerBudgetDetailDto>> GetBudgetDetails(Guid id)
        => Ok(await _manpowerBudgetService.GetDetailByIdAsync(id));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/year/{fiscalYear:int}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByFiscalYear(int fiscalYear)
        => Ok(await _manpowerBudgetService.GetByFiscalYearAsync(fiscalYear));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/organization-unit/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByOrganizationUnit(Guid organizationUnitId)
        => Ok(await _manpowerBudgetService.GetByOrganizationUnitIdAsync(organizationUnitId));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/organization-level/{organizationLevelId:guid}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByOrganizationLevel(Guid organizationLevelId)
        => Ok(await _manpowerBudgetService.GetByOrganizationLevelIdAsync(organizationLevelId));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/status/{status}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByStatus(ManpowerBudgetStatus status)
        => Ok(await _manpowerBudgetService.GetByStatusAsync(status));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/organization-unit/{organizationUnitId:guid}/current")]
    public async Task<ActionResult<ManpowerBudgetDto?>> GetCurrentOrganizationUnitBudget(Guid organizationUnitId)
        => Ok(await _manpowerBudgetService.GetCurrentBudgetForOrganizationUnitAsync(organizationUnitId));

    /// <summary>
    /// What the system knows about a unit's subtree before a budget is typed for it (round 2b, R2):
    /// serving headcount, estimated salary cost, exits due in the period, each post against its
    /// establishment. The create form pre-fills from it; the detail page shows it live.
    /// </summary>
    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/planning-baseline")]
    public async Task<ActionResult<ManpowerPlanningBaselineDto>> GetPlanningBaseline(
        [FromQuery] Guid organizationUnitId,
        [FromQuery] DateOnly periodStart,
        [FromQuery] DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        if (organizationUnitId == Guid.Empty) return BadRequest("organizationUnitId is required.");
        return Ok(await _manpowerBudgetService.GetPlanningBaselineAsync(organizationUnitId, periodStart, periodEnd, cancellationToken));
    }

    /// <summary>"The salary that goes with this position": the grade it carries, for the line picker (round 2b, R3).</summary>
    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("positions/{positionId:guid}/salary-reference")]
    public async Task<ActionResult<PositionSalaryReferenceDto>> GetPositionSalaryReference(Guid positionId, CancellationToken cancellationToken)
        => Ok(await _manpowerBudgetService.GetPositionSalaryReferenceAsync(positionId, cancellationToken));

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/pending-approvals")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetPendingBudgetApprovals()
        => Ok(await _manpowerBudgetService.GetPendingApprovalsAsync());

    [Authorize(Policy = HrPermissions.ManpowerBudgetWritePolicy)]
    [HttpPost("budgets")]
    public async Task<ActionResult<ManpowerBudgetDto>> CreateBudget([FromBody] CreateManpowerBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _manpowerBudgetService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetBudget), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetWritePolicy)]
    [HttpPut("budgets/{id:guid}")]
    public async Task<ActionResult<ManpowerBudgetDto>> UpdateBudget(Guid id, [FromBody] UpdateManpowerBudgetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _manpowerBudgetService.UpdateAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetWritePolicy)]
    [HttpPost("budgets/{id:guid}/submit")]
    public async Task<IActionResult> SubmitBudget(Guid id)
    {
        await _manpowerBudgetService.SubmitForApprovalAsync(id);
        return Ok(new { message = "Budget submitted" });
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetAdminPolicy)]
    /// <summary>Approves a manpower budget directly (only when no workflow is published).</summary>
    /// <remarks>
    /// ⚠ The approver used to arrive as <c>[FromQuery] Guid approvedById</c> — caller-declared, on
    /// the endpoint that authorises headcount and the money behind it, and (decision D-2) sets the
    /// establishment that gates vacancy approval. Anyone able to call it could record any employee
    /// as having approved the budget. It now comes from the token, like every other act performed
    /// by the caller at the moment of the call.
    /// </remarks>
    [HttpPost("budgets/{id:guid}/approve")]
    public async Task<IActionResult> ApproveBudget(Guid id, [FromBody] ApproveManpowerBudgetDto dto)
    {
        var approvedById = GetCurrentEmployeeId();
        if (approvedById == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.BudgetId = id;
        await _manpowerBudgetService.ApproveAsync(dto, approvedById.Value);
        return Ok(new { message = "Budget approved" });
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetAdminPolicy)]
    /// <summary>Rejects a manpower budget directly (only when no workflow is published).</summary>
    /// <remarks>
    /// ⚠ This took <c>[FromBody] string</c> — a bare JSON string, which means a form has to send
    /// <c>"the reason"</c> quotes and all rather than an object, and no typed client would produce
    /// it by accident. Taking a DTO makes it a normal payload, and the reason is now stored rather
    /// than discarded (see <c>ManpowerBudget.RejectionReason</c>).
    /// </remarks>
    [HttpPost("budgets/{id:guid}/reject")]
    public async Task<IActionResult> RejectBudget(Guid id, [FromBody] RejectManpowerBudgetDto dto)
    {
        await _manpowerBudgetService.RejectAsync(id, dto?.Reason ?? string.Empty);
        return Ok(new { message = "Budget rejected" });
    }

    /// <summary>
    /// Approves the current workflow step for a manpower budget (FR-HR-135). Plain
    /// <c>[Authorize]</c>: the approver is whoever the tenant named — a department head, then HR,
    /// then the Managing Director — and the engine's <c>CanUserApproveAsync</c> is the check. A
    /// permission gate here would refuse exactly those people; see the job description equivalent.
    /// </summary>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("budgets/{id:guid}/workflow/approve")]
    public async Task<IActionResult> ApproveBudgetOnWorkflow(Guid id)
    {
        var approvedById = GetCurrentEmployeeId();
        if (approvedById == null)
            return BadRequest("Your user account is not linked to an employee record.");
        await _manpowerBudgetService.ApproveViaWorkflowAsync(id, approvedById.Value);
        return Ok(new { message = "Manpower budget approval step processed" });
    }

    /// <summary>Rejects the current workflow step for a manpower budget, keeping the reason.</summary>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("budgets/{id:guid}/workflow/reject")]
    public async Task<IActionResult> RejectBudgetOnWorkflow(Guid id, [FromBody] RejectManpowerBudgetDto dto)
    {
        await _manpowerBudgetService.RejectViaWorkflowAsync(id, dto?.Reason);
        return Ok(new { message = "Manpower budget rejected" });
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetAdminPolicy)]
    [HttpDelete("budgets/{id:guid}")]
    public async Task<IActionResult> DeleteBudget(Guid id)
    {
        await _manpowerBudgetService.DeleteAsync(id);
        return NoContent();
    }

    #region Budget Lines

    [Authorize(Policy = HrPermissions.ManpowerBudgetWritePolicy)]
    [HttpPost("budgets/{budgetId:guid}/lines")]
    public async Task<ActionResult<ManpowerBudgetLineDto>> AddBudgetLine(Guid budgetId, [FromBody] CreateManpowerBudgetLineDto dto)
    {
        dto.ManpowerBudgetId = budgetId;
        var created = await _manpowerBudgetService.AddBudgetLineAsync(dto);
        return CreatedAtAction(nameof(GetBudgetLines), new { budgetId }, created);
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/{budgetId:guid}/lines")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetLineDto>>> GetBudgetLines(Guid budgetId)
        => Ok(await _manpowerBudgetService.GetBudgetLinesAsync(budgetId));

    [Authorize(Policy = HrPermissions.ManpowerBudgetWritePolicy)]
    [HttpPut("lines/{lineId:guid}")]
    public async Task<ActionResult<ManpowerBudgetLineDto>> UpdateBudgetLine(Guid lineId, [FromBody] UpdateManpowerBudgetLineDto dto)
    {
        if (lineId != dto.Id) return BadRequest("ID mismatch");
        var updated = await _manpowerBudgetService.UpdateBudgetLineAsync(dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetAdminPolicy)]
    [HttpDelete("lines/{lineId:guid}")]
    public async Task<IActionResult> DeleteBudgetLine(Guid lineId)
    {
        await _manpowerBudgetService.DeleteBudgetLineAsync(lineId);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.ManpowerBudgetReadPolicy)]
    [HttpGet("budgets/{budgetId:guid}/critical-positions")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetLineDto>>> GetCriticalPositions(Guid budgetId)
        => Ok(await _manpowerBudgetService.GetCriticalPositionsAsync(budgetId));

    #endregion

    #endregion
}

