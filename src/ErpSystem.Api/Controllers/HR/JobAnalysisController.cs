using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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

    [HttpGet("descriptions")]
    public async Task<ActionResult<IEnumerable<JobDescriptionDto>>> GetJobDescriptions()
        => Ok(await _jobDescriptionService.GetAllAsync());

    [HttpGet("descriptions/paged")]
    public async Task<ActionResult<PagedResult<JobDescriptionDto>>> GetJobDescriptionsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _jobDescriptionService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("descriptions/{id:guid}")]
    public async Task<ActionResult<JobDescriptionDto>> GetJobDescription(Guid id)
        => Ok(await _jobDescriptionService.GetByIdAsync(id));

    [HttpGet("descriptions/{id:guid}/details")]
    public async Task<ActionResult<JobDescriptionDetailDto>> GetJobDescriptionDetails(Guid id)
        => Ok(await _jobDescriptionService.GetDetailByIdAsync(id));

    [HttpGet("descriptions/position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetByPosition(Guid positionId)
        => Ok(await _jobDescriptionService.GetByPositionIdAsync(positionId));

    [HttpGet("descriptions/status/{status}")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetByStatus(JobDescriptionStatus status)
        => Ok(await _jobDescriptionService.GetByStatusAsync(status));

    [HttpGet("descriptions/position/{positionId:guid}/current")]
    public async Task<ActionResult<JobDescriptionDto?>> GetCurrentForPosition(Guid positionId)
        => Ok(await _jobDescriptionService.GetCurrentVersionForPositionAsync(positionId));

    [HttpGet("descriptions/due-review")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _jobDescriptionService.GetDueForReviewAsync(daysAhead));

    [HttpGet("analytics")]
    public async Task<ActionResult<JobAnalyticsDto>> GetAnalytics()
        => Ok(await _jobDescriptionService.GetAnalyticsAsync());

    [HttpGet("descriptions/position/{positionId:guid}/history")]
    public async Task<ActionResult<IEnumerable<JobDescriptionSummaryDto>>> GetVersionHistory(Guid positionId)
        => Ok(await _jobDescriptionService.GetVersionHistoryAsync(positionId));

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

    [HttpPut("descriptions/{id:guid}")]
    public async Task<ActionResult<JobDescriptionDto>> UpdateJobDescription(Guid id, [FromBody] UpdateJobDescriptionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _jobDescriptionService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpPost("descriptions/{id:guid}/submit")]
    public async Task<IActionResult> SubmitForReview(Guid id, [FromBody] SubmitJobDescriptionForReviewDto dto)
    {
        dto.JobDescriptionId = id;
        await _jobDescriptionService.SubmitForReviewAsync(dto);
        return Ok(new { message = "Submitted for review" });
    }

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

    [HttpDelete("descriptions/{id:guid}")]
    public async Task<IActionResult> DeleteJobDescription(Guid id)
    {
        await _jobDescriptionService.DeleteAsync(id);
        return NoContent();
    }

    #region Responsibilities

    [HttpPost("descriptions/{jobDescriptionId:guid}/responsibilities")]
    public async Task<ActionResult<JobResponsibilityDto>> AddResponsibility(Guid jobDescriptionId, [FromBody] CreateJobResponsibilityDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddResponsibilityAsync(dto);
        return CreatedAtAction(nameof(GetResponsibilities), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/responsibilities")]
    public async Task<ActionResult<IEnumerable<JobResponsibilityDto>>> GetResponsibilities(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetResponsibilitiesAsync(jobDescriptionId));

    [HttpPut("responsibilities/{id:guid}")]
    public async Task<ActionResult<JobResponsibilityDto>> UpdateResponsibility(Guid id, [FromBody] UpdateJobResponsibilityDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateResponsibilityAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("responsibilities/{id:guid}")]
    public async Task<IActionResult> DeleteResponsibility(Guid id)
    {
        await _jobDescriptionService.DeleteResponsibilityAsync(id);
        return NoContent();
    }

    #endregion

    #region Qualifications

    [HttpPost("descriptions/{jobDescriptionId:guid}/qualifications")]
    public async Task<ActionResult<JobQualificationDto>> AddQualification(Guid jobDescriptionId, [FromBody] CreateJobQualificationDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddQualificationAsync(dto);
        return CreatedAtAction(nameof(GetQualifications), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/qualifications")]
    public async Task<ActionResult<IEnumerable<JobQualificationDto>>> GetQualifications(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetQualificationsAsync(jobDescriptionId));

    [HttpPut("qualifications/{id:guid}")]
    public async Task<ActionResult<JobQualificationDto>> UpdateQualification(Guid id, [FromBody] UpdateJobQualificationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateQualificationAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("qualifications/{id:guid}")]
    public async Task<IActionResult> DeleteQualification(Guid id)
    {
        await _jobDescriptionService.DeleteQualificationAsync(id);
        return NoContent();
    }

    #endregion

    #region Competencies

    [HttpPost("descriptions/{jobDescriptionId:guid}/competencies")]
    public async Task<ActionResult<JobCompetencyDto>> AddCompetency(Guid jobDescriptionId, [FromBody] CreateJobCompetencyDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddCompetencyAsync(dto);
        return CreatedAtAction(nameof(GetCompetencies), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/competencies")]
    public async Task<ActionResult<IEnumerable<JobCompetencyDto>>> GetCompetencies(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetCompetenciesAsync(jobDescriptionId));

    [HttpPut("competencies/{id:guid}")]
    public async Task<ActionResult<JobCompetencyDto>> UpdateCompetency(Guid id, [FromBody] UpdateJobCompetencyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateCompetencyAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("competencies/{id:guid}")]
    public async Task<IActionResult> DeleteCompetency(Guid id)
    {
        await _jobDescriptionService.DeleteCompetencyAsync(id);
        return NoContent();
    }

    #endregion

    #region Physical Demands

    [HttpPost("descriptions/{jobDescriptionId:guid}/physical-demands")]
    public async Task<ActionResult<JobPhysicalDemandDto>> AddPhysicalDemand(Guid jobDescriptionId, [FromBody] CreateJobPhysicalDemandDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddPhysicalDemandAsync(dto);
        return CreatedAtAction(nameof(GetPhysicalDemands), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/physical-demands")]
    public async Task<ActionResult<IEnumerable<JobPhysicalDemandDto>>> GetPhysicalDemands(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetPhysicalDemandsAsync(jobDescriptionId));

    [HttpPut("physical-demands/{id:guid}")]
    public async Task<ActionResult<JobPhysicalDemandDto>> UpdatePhysicalDemand(Guid id, [FromBody] UpdateJobPhysicalDemandDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdatePhysicalDemandAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("physical-demands/{id:guid}")]
    public async Task<IActionResult> DeletePhysicalDemand(Guid id)
    {
        await _jobDescriptionService.DeletePhysicalDemandAsync(id);
        return NoContent();
    }

    #endregion

    #region Working Conditions

    [HttpPost("descriptions/{jobDescriptionId:guid}/working-conditions")]
    public async Task<ActionResult<JobWorkingConditionDto>> AddWorkingCondition(Guid jobDescriptionId, [FromBody] CreateJobWorkingConditionDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddWorkingConditionAsync(dto);
        return CreatedAtAction(nameof(GetWorkingConditions), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/working-conditions")]
    public async Task<ActionResult<IEnumerable<JobWorkingConditionDto>>> GetWorkingConditions(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetWorkingConditionsAsync(jobDescriptionId));

    [HttpPut("working-conditions/{id:guid}")]
    public async Task<ActionResult<JobWorkingConditionDto>> UpdateWorkingCondition(Guid id, [FromBody] UpdateJobWorkingConditionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateWorkingConditionAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("working-conditions/{id:guid}")]
    public async Task<IActionResult> DeleteWorkingCondition(Guid id)
    {
        await _jobDescriptionService.DeleteWorkingConditionAsync(id);
        return NoContent();
    }

    #endregion

    #region Equipment Tools

    [HttpPost("descriptions/{jobDescriptionId:guid}/equipment-tools")]
    public async Task<ActionResult<JobEquipmentToolDto>> AddEquipmentTool(Guid jobDescriptionId, [FromBody] CreateJobEquipmentToolDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddEquipmentToolAsync(dto);
        return CreatedAtAction(nameof(GetEquipmentTools), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/equipment-tools")]
    public async Task<ActionResult<IEnumerable<JobEquipmentToolDto>>> GetEquipmentTools(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetEquipmentToolsAsync(jobDescriptionId));

    [HttpPut("equipment-tools/{id:guid}")]
    public async Task<ActionResult<JobEquipmentToolDto>> UpdateEquipmentTool(Guid id, [FromBody] UpdateJobEquipmentToolDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateEquipmentToolAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("equipment-tools/{id:guid}")]
    public async Task<IActionResult> DeleteEquipmentTool(Guid id)
    {
        await _jobDescriptionService.DeleteEquipmentToolAsync(id);
        return NoContent();
    }

    #endregion

    #region Reporting Relationships

    [HttpPost("descriptions/{jobDescriptionId:guid}/reporting-relationships")]
    public async Task<ActionResult<JobReportingRelationshipDto>> AddReportingRelationship(Guid jobDescriptionId, [FromBody] CreateJobReportingRelationshipDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddReportingRelationshipAsync(dto);
        return CreatedAtAction(nameof(GetReportingRelationships), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/reporting-relationships")]
    public async Task<ActionResult<IEnumerable<JobReportingRelationshipDto>>> GetReportingRelationships(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetReportingRelationshipsAsync(jobDescriptionId));

    [HttpPut("reporting-relationships/{id:guid}")]
    public async Task<ActionResult<JobReportingRelationshipDto>> UpdateReportingRelationship(Guid id, [FromBody] UpdateJobReportingRelationshipDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateReportingRelationshipAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("reporting-relationships/{id:guid}")]
    public async Task<IActionResult> DeleteReportingRelationship(Guid id)
    {
        await _jobDescriptionService.DeleteReportingRelationshipAsync(id);
        return NoContent();
    }

    #endregion

    #region Duty Items

    [HttpPost("descriptions/{jobDescriptionId:guid}/duty-items")]
    public async Task<ActionResult<JobDutyItemDto>> AddDutyItem(Guid jobDescriptionId, [FromBody] CreateJobDutyItemDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddDutyItemAsync(dto);
        return CreatedAtAction(nameof(GetDutyItems), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/duty-items")]
    public async Task<ActionResult<IEnumerable<JobDutyItemDto>>> GetDutyItems(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetDutyItemsAsync(jobDescriptionId));

    [HttpPut("duty-items/{id:guid}")]
    public async Task<ActionResult<JobDutyItemDto>> UpdateDutyItem(Guid id, [FromBody] UpdateJobDutyItemDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateDutyItemAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("duty-items/{id:guid}")]
    public async Task<IActionResult> DeleteDutyItem(Guid id)
    {
        await _jobDescriptionService.DeleteDutyItemAsync(id);
        return NoContent();
    }

    #endregion

    #region PPE Requirements

    [HttpPost("descriptions/{jobDescriptionId:guid}/ppe-requirements")]
    public async Task<ActionResult<JobPpeRequirementDto>> AddPpeRequirement(Guid jobDescriptionId, [FromBody] CreateJobPpeRequirementDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddPpeRequirementAsync(dto);
        return CreatedAtAction(nameof(GetPpeRequirements), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/ppe-requirements")]
    public async Task<ActionResult<IEnumerable<JobPpeRequirementDto>>> GetPpeRequirements(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetPpeRequirementsAsync(jobDescriptionId));

    [HttpPut("ppe-requirements/{id:guid}")]
    public async Task<ActionResult<JobPpeRequirementDto>> UpdatePpeRequirement(Guid id, [FromBody] UpdateJobPpeRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdatePpeRequirementAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("ppe-requirements/{id:guid}")]
    public async Task<IActionResult> DeletePpeRequirement(Guid id)
    {
        await _jobDescriptionService.DeletePpeRequirementAsync(id);
        return NoContent();
    }

    #endregion

    #region Equipment Training

    [HttpPost("equipment-tools/{equipmentToolId:guid}/training")]
    public async Task<ActionResult<JobEquipmentTrainingDto>> AddEquipmentTraining(Guid equipmentToolId, [FromBody] CreateJobEquipmentTrainingDto dto)
    {
        dto.JobEquipmentToolId = equipmentToolId;
        var created = await _jobDescriptionService.AddEquipmentTrainingAsync(dto);
        return CreatedAtAction(nameof(GetEquipmentTrainings), new { equipmentToolId }, created);
    }

    [HttpGet("equipment-tools/{equipmentToolId:guid}/training")]
    public async Task<ActionResult<IEnumerable<JobEquipmentTrainingDto>>> GetEquipmentTrainings(Guid equipmentToolId)
        => Ok(await _jobDescriptionService.GetEquipmentTrainingsAsync(equipmentToolId));

    [HttpPut("equipment-training/{id:guid}")]
    public async Task<ActionResult<JobEquipmentTrainingDto>> UpdateEquipmentTraining(Guid id, [FromBody] UpdateJobEquipmentTrainingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateEquipmentTrainingAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("equipment-training/{id:guid}")]
    public async Task<IActionResult> DeleteEquipmentTraining(Guid id)
    {
        await _jobDescriptionService.DeleteEquipmentTrainingAsync(id);
        return NoContent();
    }

    #endregion

    #region Medical Requirements

    [HttpPost("descriptions/{jobDescriptionId:guid}/medical-requirements")]
    public async Task<ActionResult<JobMedicalRequirementDto>> AddMedicalRequirement(Guid jobDescriptionId, [FromBody] CreateJobMedicalRequirementDto dto)
    {
        dto.JobDescriptionId = jobDescriptionId;
        var created = await _jobDescriptionService.AddMedicalRequirementAsync(dto);
        return CreatedAtAction(nameof(GetMedicalRequirements), new { jobDescriptionId }, created);
    }

    [HttpGet("descriptions/{jobDescriptionId:guid}/medical-requirements")]
    public async Task<ActionResult<IEnumerable<JobMedicalRequirementDto>>> GetMedicalRequirements(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetMedicalRequirementsAsync(jobDescriptionId));

    [HttpPut("medical-requirements/{id:guid}")]
    public async Task<ActionResult<JobMedicalRequirementDto>> UpdateMedicalRequirement(Guid id, [FromBody] UpdateJobMedicalRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateMedicalRequirementAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("medical-requirements/{id:guid}")]
    public async Task<IActionResult> DeleteMedicalRequirement(Guid id)
    {
        await _jobDescriptionService.DeleteMedicalRequirementAsync(id);
        return NoContent();
    }

    #endregion

    #region Valuation

    [HttpGet("descriptions/{jobDescriptionId:guid}/valuation")]
    public async Task<ActionResult<JobValuationSummaryDto>> GetValuation(Guid jobDescriptionId)
        => Ok(await _jobDescriptionService.GetValuationAsync(jobDescriptionId));

    #endregion

    #region Responsibility KPIs

    [HttpPost("responsibilities/{responsibilityId:guid}/kpis")]
    public async Task<ActionResult<JobResponsibilityKpiDto>> AddResponsibilityKpi(Guid responsibilityId, [FromBody] CreateJobResponsibilityKpiDto dto)
    {
        dto.JobResponsibilityId = responsibilityId;
        var created = await _jobDescriptionService.AddResponsibilityKpiAsync(dto);
        return CreatedAtAction(nameof(GetResponsibilityKpis), new { responsibilityId }, created);
    }

    [HttpGet("responsibilities/{responsibilityId:guid}/kpis")]
    public async Task<ActionResult<IEnumerable<JobResponsibilityKpiDto>>> GetResponsibilityKpis(Guid responsibilityId)
        => Ok(await _jobDescriptionService.GetResponsibilityKpisAsync(responsibilityId));

    [HttpPut("kpis/{id:guid}")]
    public async Task<ActionResult<JobResponsibilityKpiDto>> UpdateResponsibilityKpi(Guid id, [FromBody] UpdateJobResponsibilityKpiDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _jobDescriptionService.UpdateResponsibilityKpiAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("kpis/{id:guid}")]
    public async Task<IActionResult> DeleteResponsibilityKpi(Guid id)
    {
        await _jobDescriptionService.DeleteResponsibilityKpiAsync(id);
        return NoContent();
    }

    #endregion

    #endregion

    #region Manpower Budgets

    [HttpGet("budgets")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetDto>>> GetBudgets()
        => Ok(await _manpowerBudgetService.GetAllAsync());

    [HttpGet("budgets/paged")]
    public async Task<ActionResult<PagedResult<ManpowerBudgetDto>>> GetBudgetsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _manpowerBudgetService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("budgets/{id:guid}")]
    public async Task<ActionResult<ManpowerBudgetDto>> GetBudget(Guid id)
        => Ok(await _manpowerBudgetService.GetByIdAsync(id));

    [HttpGet("budgets/{id:guid}/details")]
    public async Task<ActionResult<ManpowerBudgetDetailDto>> GetBudgetDetails(Guid id)
        => Ok(await _manpowerBudgetService.GetDetailByIdAsync(id));

    [HttpGet("budgets/year/{fiscalYear:int}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByFiscalYear(int fiscalYear)
        => Ok(await _manpowerBudgetService.GetByFiscalYearAsync(fiscalYear));

    [HttpGet("budgets/organization-unit/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByOrganizationUnit(Guid organizationUnitId)
        => Ok(await _manpowerBudgetService.GetByOrganizationUnitIdAsync(organizationUnitId));

    [HttpGet("budgets/organization-level/{organizationLevelId:guid}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByOrganizationLevel(Guid organizationLevelId)
        => Ok(await _manpowerBudgetService.GetByOrganizationLevelIdAsync(organizationLevelId));

    [HttpGet("budgets/status/{status}")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetBudgetsByStatus(ManpowerBudgetStatus status)
        => Ok(await _manpowerBudgetService.GetByStatusAsync(status));

    [HttpGet("budgets/organization-unit/{organizationUnitId:guid}/current")]
    public async Task<ActionResult<ManpowerBudgetDto?>> GetCurrentOrganizationUnitBudget(Guid organizationUnitId)
        => Ok(await _manpowerBudgetService.GetCurrentBudgetForOrganizationUnitAsync(organizationUnitId));

    [HttpGet("budgets/pending-approvals")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetSummaryDto>>> GetPendingBudgetApprovals()
        => Ok(await _manpowerBudgetService.GetPendingApprovalsAsync());

    [HttpPost("budgets")]
    public async Task<ActionResult<ManpowerBudgetDto>> CreateBudget([FromBody] CreateManpowerBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _manpowerBudgetService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetBudget), new { id = created.Id }, created);
    }

    [HttpPut("budgets/{id:guid}")]
    public async Task<ActionResult<ManpowerBudgetDto>> UpdateBudget(Guid id, [FromBody] UpdateManpowerBudgetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _manpowerBudgetService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpPost("budgets/{id:guid}/submit")]
    public async Task<IActionResult> SubmitBudget(Guid id)
    {
        await _manpowerBudgetService.SubmitForApprovalAsync(id);
        return Ok(new { message = "Budget submitted" });
    }

    [HttpPost("budgets/{id:guid}/approve")]
    public async Task<IActionResult> ApproveBudget(Guid id, [FromBody] ApproveManpowerBudgetDto dto, [FromQuery] Guid approvedById)
    {
        dto.BudgetId = id;
        await _manpowerBudgetService.ApproveAsync(dto, approvedById);
        return Ok(new { message = "Budget approved" });
    }

    [HttpPost("budgets/{id:guid}/reject")]
    public async Task<IActionResult> RejectBudget(Guid id, [FromBody] string reason)
    {
        await _manpowerBudgetService.RejectAsync(id, reason);
        return Ok(new { message = "Budget rejected" });
    }

    [HttpDelete("budgets/{id:guid}")]
    public async Task<IActionResult> DeleteBudget(Guid id)
    {
        await _manpowerBudgetService.DeleteAsync(id);
        return NoContent();
    }

    #region Budget Lines

    [HttpPost("budgets/{budgetId:guid}/lines")]
    public async Task<ActionResult<ManpowerBudgetLineDto>> AddBudgetLine(Guid budgetId, [FromBody] CreateManpowerBudgetLineDto dto)
    {
        dto.ManpowerBudgetId = budgetId;
        var created = await _manpowerBudgetService.AddBudgetLineAsync(dto);
        return CreatedAtAction(nameof(GetBudgetLines), new { budgetId }, created);
    }

    [HttpGet("budgets/{budgetId:guid}/lines")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetLineDto>>> GetBudgetLines(Guid budgetId)
        => Ok(await _manpowerBudgetService.GetBudgetLinesAsync(budgetId));

    [HttpPut("lines/{lineId:guid}")]
    public async Task<ActionResult<ManpowerBudgetLineDto>> UpdateBudgetLine(Guid lineId, [FromBody] UpdateManpowerBudgetLineDto dto)
    {
        if (lineId != dto.Id) return BadRequest("ID mismatch");
        var updated = await _manpowerBudgetService.UpdateBudgetLineAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("lines/{lineId:guid}")]
    public async Task<IActionResult> DeleteBudgetLine(Guid lineId)
    {
        await _manpowerBudgetService.DeleteBudgetLineAsync(lineId);
        return NoContent();
    }

    [HttpGet("budgets/{budgetId:guid}/critical-positions")]
    public async Task<ActionResult<IEnumerable<ManpowerBudgetLineDto>>> GetCriticalPositions(Guid budgetId)
        => Ok(await _manpowerBudgetService.GetCriticalPositionsAsync(budgetId));

    #endregion

    #endregion
}

